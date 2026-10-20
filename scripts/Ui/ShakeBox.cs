using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Audio;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// A closed chest you can pick up and shake. How hard you shake it (how fast you drag) sets the shake level; whatever
/// is inside is simulated as invisible physics bodies in a box that follows the chest, and a hit makes its kind's
/// sound only if the shake is strong enough to stir it (<see cref="Chest.Sounds"/>). Metal clanking drowns out delicate
/// things; with the magnet on, iron makes no sound at all. Every sound pops
/// its word out of the chest, each thing from its own spot so two of a kind are two words; with the witchlight lit,
/// magic words glow.
/// </summary>
public partial class ShakeBox : Node2D
{
    const float CavityHalfWidth = 72f, CavityTop = -70f, CavityBottom = 40f, Wall = 48f;
    const float ImpactThreshold = 40f, Gravity = 2600f;

    /// <summary>Chest speed (px/s) at which each shake level starts.</summary>
    public const float GentleSpeed = 40f, FirmSpeed = 520f, HardSpeed = 1150f;

    /// <summary>A shake level holds this long after the chest last moved that fast, so it doesn't flicker as you change direction.</summary>
    const double LevelHold = 0.4;

    /// <summary>How long a clank drowns out delicate sounds.</summary>
    const double MaskSeconds = 0.6;

    /// <summary>
    /// Anything stirred makes a sound at least this often while the chest is moving, even if the physics hasn't knocked
    /// it into a wall: a slow, gentle shake rolls things around without many hard hits, but you'd still hear them.
    /// </summary>
    const double StirredSoundEvery = 0.55;

    public ChestView View { get; private set; }

    /// <summary>A thing made a sound you could hear, at this shake.</summary>
    public event Action<Item, Shake> Heard;

    /// <summary>The chest left the place it was resting.</summary>
    public event Action Lifted;

    /// <summary>The chest came back to rest after being let go this high above it (px).</summary>
    public event Action<float> Landed;

    /// <summary>
    /// Let go at least this high and it's a drop: it hits hard enough to tell you something. Setting it down after a
    /// shake isn't.
    /// </summary>
    public const float DropHeight = 150f;

    /// <summary>The magnet is on: iron can't move, so it makes no sound.</summary>
    public bool Magnet;

    /// <summary>The witchlight is lit: magic glows when it makes a sound.</summary>
    public bool Witchlight;

    /// <summary>How hard the chest is being shaken right now.</summary>
    public Shake Level { get; private set; }

    /// <summary>The hardest shake the hands holding it can manage (Hesk's back stops at firm).</summary>
    public Shake MaxLevel { get; set; } = Shake.Hard;

    /// <summary>The chest's speed (px/s), smoothed so it doesn't drop to zero as you change direction. For the meter.</summary>
    public float Speed => _envelope;

    /// <summary>Where the chest sits when it isn't being handled (in the parent's coordinates).</summary>
    public Vector2 Resting => _rest;

    public bool Busy => Dragging || _airborne;

    /// <summary>The mouse is over the chest.</summary>
    public bool Hover { get; private set; }

    public bool HeardAnything { get; private set; }

    public bool Dragging => (_dragging && _moved) || _autoShakeLeft > 0 || _liftLeft > 0;

    readonly Chest _chest;
    readonly Rect2 _dragBounds;
    readonly Random _rng = new();
    readonly List<(RigidBody2D Body, Item Item)> _items = new();
    readonly Dictionary<RigidBody2D, (Vector2 Velocity, double Cooldown)> _last = new();
    readonly Dictionary<int, double> _wordCooldown = new();
    readonly Dictionary<Item, double> _sinceSound = new();
    readonly double[] _lastAtLevel = { -9, -9, -9, -9 };
    Vector2 _rest, _pos, _vel, _target, _grab, _pressAt, _shakeFrom;
    AnimatableBody2D _box;
    Node2D _words;
    bool _dragging, _airborne, _moved;
    double _autoShakeLeft, _autoShakeTime, _liftLeft, _time, _lastClank = -9;
    double _settling = 0.8; // items drop into place when the chest appears; that shouldn't make noise
    float _fallHeight;
    Shake _autoLevel;
    float _envelope;

    public ShakeBox() { }

    /// <param name="dragBounds">Where the chest may be held, in the parent's coordinates.</param>
    public ShakeBox(Chest chest, Rect2 dragBounds)
    {
        _chest = chest;
        _dragBounds = dragBounds;
    }

    static Sfx Sfx => Main.Instance.Sfx;

    public override void _Ready()
    {
        _rest = _pos = _target = Position;
        Position = Vector2.Zero;

        View = new ChestView { Position = _rest, PixelSize = 12, Skin = _chest.Skin };
        AddChild(View);

        _box = new AnimatableBody2D { SyncToPhysics = true, Position = _rest, CollisionLayer = 1, CollisionMask = 0 };
        AddWall(new Vector2(0, CavityBottom + Wall / 2), new Vector2(CavityHalfWidth * 2 + Wall * 2, Wall));
        AddWall(new Vector2(0, CavityTop - Wall / 2), new Vector2(CavityHalfWidth * 2 + Wall * 2, Wall));
        AddWall(new Vector2(-CavityHalfWidth - Wall / 2, (CavityTop + CavityBottom) / 2), new Vector2(Wall, CavityBottom - CavityTop));
        AddWall(new Vector2(CavityHalfWidth + Wall / 2, (CavityTop + CavityBottom) / 2), new Vector2(Wall, CavityBottom - CavityTop));
        AddChild(_box);

        foreach (var item in _chest.Items) AddBody(item);

        _words = new Node2D { ZIndex = 30 };
        AddChild(_words);
    }

    void AddBody(Item item)
    {
        float heft = Categories.Heft(item.Category);
        var body = new RigidBody2D
        {
            Mass = 0.2f + heft,
            CollisionLayer = 2,
            CollisionMask = 1 | 2,
            ContactMonitor = true,
            MaxContactsReported = 4,
            CanSleep = false,
            ContinuousCd = RigidBody2D.CcdMode.CastShape,
            PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.35f, Friction = 0.2f },
            Position = _rest + new Vector2(Rand() * (CavityHalfWidth - 20), CavityBottom - 20 - (float)_rng.NextDouble() * 40),
        };
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 9f + heft * 3f } });
        AddChild(body);
        _items.Add((body, item));
        _last[body] = (Vector2.Zero, 0);
    }

    void AddWall(Vector2 offset, Vector2 size) =>
        _box.AddChild(new CollisionShape2D { Position = offset, Shape = new RectangleShape2D { Size = size } });

    float Rand() => (float)(_rng.NextDouble() * 2 - 1);

    /// <summary>Shakes the chest by itself at a given strength for a while (the bot and the shake test use this).</summary>
    public void AutoShake(double seconds, Shake level)
    {
        if (Busy) return;
        Lifted?.Invoke();
        _shakeFrom = _rest;
        _autoShakeLeft = seconds;
        _autoShakeTime = 0;
        _autoLevel = level;
    }

    /// <summary>Lifts the chest high and lets it fall (the bot uses this; players drag it up and let go).</summary>
    public void LiftAndDrop()
    {
        if (Busy) return;
        Lifted?.Invoke();
        _liftLeft = 0.6;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left } button) return;
        var mouse = GetGlobalMousePosition() - GlobalPosition;
        if (button.Pressed && _autoShakeLeft <= 0 && !_airborne && Over(mouse))
        {
            _dragging = true;
            _moved = false;
            _pressAt = mouse;
            _grab = mouse - _pos;
            _target = _pos;
            GetViewport().SetInputAsHandled();
        }
        else if (!button.Pressed && _dragging)
        {
            _dragging = false;
            if (_moved) Drop();
        }
    }

    bool Over(Vector2 local)
    {
        var p = local - _pos;
        return p.X > -View.Width / 2 && p.X < View.Width / 2 && p.Y > View.Top - 10 && p.Y < View.Bottom + 10;
    }

    /// <summary>Lets go: the chest hops up if it has to, drifts back over its spot and falls onto it.</summary>
    void Drop()
    {
        _airborne = true;
        _fallHeight = _rest.Y - _pos.Y;
        // Let go mid-shake, it hops a little rather than flying off: only lifting it high and letting go is a drop.
        _vel.Y = MathF.Max(_vel.Y, -250f);
        float rise = _pos.Y - _rest.Y + 24f;
        if (rise > 0) _vel.Y = MathF.Min(_vel.Y, -MathF.Sqrt(2f * Gravity * rise));
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += delta;
        _settling -= delta;
        foreach (var key in _wordCooldown.Keys.ToList()) _wordCooldown[key] -= delta;
        Hover = !_dragging && !_airborne && Over(GetGlobalMousePosition() - GlobalPosition);

        if (_liftLeft > 0)
        {
            // Raised slowly, so lifting it doesn't count as a shake.
            _liftLeft -= delta;
            _target = _rest + new Vector2(0, -270f * (1f - (float)Math.Max(0, _liftLeft) / 0.6f));
            if (_liftLeft <= 0) Drop();
        }
        else if (_autoShakeLeft > 0)
        {
            _autoShakeLeft -= delta;
            _autoShakeTime += delta;
            float t = (float)_autoShakeTime;
            // A side-to-side wiggle whose speed lands in the middle of the level's band.
            var (ax, ay, w) = _autoLevel switch
            {
                Shake.Gentle => (60f, 20f, 4.5f),
                Shake.Firm => (85f, 30f, 10f),
                _ => (120f, 45f, 19f),
            };
            // Eased in, so lifting the chest doesn't count as a hard jerk.
            float ease = MathF.Min(1f, t / 0.4f);
            _target = _shakeFrom + new Vector2(MathF.Sin(t * w) * ax, -50f * ease + MathF.Sin(t * w * 1.37f) * ay);
            if (_autoShakeLeft <= 0) Drop();
        }
        else if (_dragging)
        {
            var mouse = GetGlobalMousePosition() - GlobalPosition;
            if (!_moved && mouse.DistanceTo(_pressAt) > 6)
            {
                _moved = true;
                if (!_airborne) Lifted?.Invoke();
                _airborne = false;
            }
            var held = mouse - _grab;
            _target = new Vector2(
                Mathf.Clamp(held.X, _dragBounds.Position.X, _dragBounds.End.X),
                Mathf.Clamp(held.Y, _dragBounds.Position.Y, _dragBounds.End.Y));
        }

        var previous = _pos;
        if (Dragging)
        {
            _pos = _pos.Lerp(_target, 1f - MathF.Exp(-28f * dt));
        }
        else if (_airborne)
        {
            // Set down: fall back onto the counter and land with a thunk.
            _vel.Y += Gravity * dt;
            _pos.X = Mathf.Lerp(_pos.X, _rest.X, 1f - MathF.Exp(-12f * dt));
            _pos.Y += _vel.Y * dt;
            if (_vel.Y > 0 && _pos.Y >= _rest.Y)
            {
                float impact = _vel.Y;
                _pos = _rest;
                _airborne = false;
                _vel = Vector2.Zero;
                Landed?.Invoke(_fallHeight);
                if (impact > 150)
                {
                    float loud = Mathf.Clamp(impact / 1400f, 0, 1);
                    Sfx.PlayGroup("land", 1f, Mathf.LinearToDb(0.25f + 0.6f * loud));
                    Main.Instance.Shake(0.04f + 0.12f * loud);
                }
            }
        }
        if (!_airborne) _vel = (_pos - previous) / Mathf.Max(dt, 0.0001f);
        UpdateLevel(dt);

        float tilt = Dragging ? Mathf.Clamp(_vel.X * 0.0003f, -0.3f, 0.3f) : Mathf.Lerp(_box.Rotation, 0, 0.2f);
        _box.Position = _pos;
        _box.Rotation = tilt;
        View.Position = _pos;
        View.Rotation = tilt;

        for (int index = 0; index < _items.Count; index++)
        {
            var (body, item) = _items[index];
            // A very fast drag can push an item through a wall; put it back rather than lose it.
            if (body.Position.DistanceTo(_pos) > 180)
            {
                body.Position = _pos + new Vector2(0, CavityBottom - 30);
                body.LinearVelocity = Vector2.Zero;
            }

            var (lastVelocity, cooldown) = _last[body];
            cooldown -= delta;
            _sinceSound[item] = _sinceSound.GetValueOrDefault(item) + delta;
            float hit = (body.LinearVelocity - lastVelocity).Length();
            bool knocked = hit > ImpactThreshold && body.GetContactCount() > 0;
            bool rolling = _sinceSound[item] > StirredSoundEvery * (0.8 + 0.4 * _rng.NextDouble()) && _envelope > GentleSpeed;
            if (_settling <= 0 && cooldown <= 0 && Dragging && (knocked || rolling))
            {
                cooldown = 0.12;
                if (Impact(index, item, knocked ? hit : ImpactThreshold + 150f)) _sinceSound[item] = 0;
            }
            _last[body] = (body.LinearVelocity, cooldown);
        }
    }

    /// <summary>The shake level is the strongest one reached in the last moment; the meter follows a smoothed speed.</summary>
    void UpdateLevel(float dt)
    {
        float speed = Dragging ? _vel.Length() : 0f;
        _envelope = MathF.Max(speed, _envelope - 2400f * dt);
        if (speed >= GentleSpeed) _lastAtLevel[(int)Shake.Gentle] = _time;
        if (speed >= FirmSpeed) _lastAtLevel[(int)Shake.Firm] = _time;
        if (speed >= HardSpeed) _lastAtLevel[(int)Shake.Hard] = _time;
        Level = Shake.Still;
        for (var level = Shake.Hard; level >= Shake.Gentle; level--)
        {
            if (_time - _lastAtLevel[(int)level] > LevelHold) continue;
            Level = level;
            break;
        }
        if (!Dragging) Level = Shake.Still;
        if (Level > MaxLevel) Level = MaxLevel;
    }

    /// <summary>A knock inside. Returns whether it made a sound you could hear.</summary>
    bool Impact(int index, Item item, float strength)
    {
        var level = Level;
        // Glass that broke in a drop doesn't slosh any more.
        if (_chest.Now(item).Broken) return false;
        // Not shaken hard enough to stir it, or held still by the magnet: it makes no sound.
        if (!_chest.Sounds(item, level, Magnet)) return false;
        if (item.Def.Loud) _lastClank = _time;
        // While metal clanks, delicate things can't be heard over it.
        if (item.Def.Delicate && (_chest.Clanks(level, Magnet) || _time - _lastClank < MaskSeconds)) return false;

        HeardAnything = true;
        float loud = Mathf.Clamp((strength - ImpactThreshold) / 600f, 0f, 1f);
        // Always the kind's own sound, give or take a hair: things of a kind sound alike.
        Sfx.PlayGroup(SoundSets.Group(item.Category), item.Def.Pitch * (1f + 0.04f * Rand()), Mathf.LinearToDb(0.4f + 0.6f * loud) + 3f);

        if (_wordCooldown.GetValueOrDefault(index) <= 0)
        {
            _wordCooldown[index] = 0.3;
            // Each thing has its own spot above the chest, so you can count them.
            var spot = _pos + new Vector2((index % 3 - 1) * 112f + Rand() * 6f, -128f - index / 3 * 36f + Rand() * 4f);
            bool glow = Witchlight && item.Magic;
            PopWord(item.Word, spot, item.Def.Loud ? 32 : 24, glow ? Palette.Magic : Palette.Word, glow);
        }
        Heard?.Invoke(item, level);
        return true;
    }

    /// <summary>A word popped from the chest, e.g. what a drop sounded like.</summary>
    public void Say(string text, Color color, int size = 32, float dx = 0) => PopWord(text, _pos + new Vector2(dx, -150), size, color, false);

    void PopWord(string text, Vector2 at, int size, Color color, bool glow)
    {
        if (_words.GetChildCount() > 12) return;
        var label = UiKit.MakeLabel(text, size, color, HorizontalAlignment.Center, outline: 8).Place(at.X - 90, at.Y - 18, 180, 36);
        label.PivotOffset = new Vector2(90, 18);
        label.Rotation = Rand() * 0.25f;
        label.Scale = Vector2.One * 0.6f;
        _words.AddChild(label);
        if (glow)
        {
            label.AddThemeColorOverride("font_outline_color", Palette.Magic.Darkened(0.5f));
            Fx.Burst(_words, at, Palette.Magic, 16, 140f);
        }
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "scale", Vector2.One * (glow ? 1.2f : 1f), 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "position:y", label.Position.Y - 36 - (float)_rng.NextDouble() * 20, 0.8f).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "position:x", label.Position.X + Rand() * 30, 0.8f);
        tween.TweenProperty(label, "modulate:a", 0f, 0.3f).SetDelay(glow ? 0.8f : 0.5f);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
