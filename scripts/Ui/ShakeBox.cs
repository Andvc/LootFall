using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Audio;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// A closed chest you can pick up, shake, knock on and set down. Whatever is inside is simulated as invisible physics
/// bodies in a box that follows the chest; every hit plays that item's own recording and pops its word out of the
/// chest. Stones wrapped in rags take part in the physics but make no sound. Heavier chests lag behind the mouse and
/// land harder. Nothing here states a value: the player listens.
/// </summary>
public partial class ShakeBox : Node2D
{
    const float CavityHalfWidth = 72f, CavityTop = -70f, CavityBottom = 40f, Wall = 48f;
    const float ImpactThreshold = 80f, Gravity = 2600f;

    public enum Part { None, Body, Keyhole }

    public ChestView View { get; private set; }

    /// <summary>Fires on every audible hit inside, with how hard it was (px/s of velocity change).</summary>
    public event Action<Rattler, float> Impacted;

    /// <summary>A short click on the chest (not a drag): knock on it.</summary>
    public event Action Clicked;

    /// <summary>A short click on the lock plate: look through the keyhole.</summary>
    public event Action KeyholeClicked;

    /// <summary>The chest left the place it was resting.</summary>
    public event Action Lifted;

    /// <summary>The chest came to rest at this spot (in the parent's coordinates).</summary>
    public event Action<Vector2> Landed;

    /// <summary>Where a dropped chest comes to rest, given where it was let go. Defaults to where it started.</summary>
    public Func<Vector2, Vector2> RestFor;

    /// <summary>What the mouse is over, for hints.</summary>
    public Part Hover { get; private set; }

    /// <summary>Where the chest sits now (in the parent's coordinates) when it isn't being handled.</summary>
    public Vector2 Resting => _rest;

    public bool Busy => Dragging || _airborne;

    /// <summary>True once the player has shaken hard enough to hear something.</summary>
    public bool HeardAnything { get; private set; }

    public bool Dragging => (_dragging && _moved) || _autoShakeLeft > 0;

    readonly Chest _chest;
    readonly Rect2 _dragBounds;
    readonly Random _rng = new();
    readonly List<(RigidBody2D Body, Rattler Rattler, float Radius)> _items = new();
    readonly Dictionary<RigidBody2D, (Vector2 Velocity, double Cooldown)> _last = new();
    readonly Dictionary<ItemDef, double> _wordCooldown = new();
    Vector2 _rest, _pos, _vel, _target, _grab, _pressAt;
    AnimatableBody2D _box;
    Node2D _words;
    bool _dragging, _airborne, _moved, _pressedKeyhole;
    double _autoShakeLeft, _autoShakeTime, _pressTime;
    double _settling = 0.8; // items drop into place when the chest appears; that shouldn't make noise
    float _totalWeight;

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
        _totalWeight = _chest.Weight;

        View = new ChestView { Position = _rest, PixelSize = 12, Skin = _chest.Provenance.Skin };
        AddChild(View);

        _box = new AnimatableBody2D { SyncToPhysics = true, Position = _rest, CollisionLayer = 1, CollisionMask = 0 };
        AddWall(new Vector2(0, CavityBottom + Wall / 2), new Vector2(CavityHalfWidth * 2 + Wall * 2, Wall));
        AddWall(new Vector2(0, CavityTop - Wall / 2), new Vector2(CavityHalfWidth * 2 + Wall * 2, Wall));
        AddWall(new Vector2(-CavityHalfWidth - Wall / 2, (CavityTop + CavityBottom) / 2), new Vector2(Wall, CavityBottom - CavityTop));
        AddWall(new Vector2(CavityHalfWidth + Wall / 2, (CavityTop + CavityBottom) / 2), new Vector2(Wall, CavityBottom - CavityTop));
        AddChild(_box);

        foreach (var rattler in _chest.Rattlers) AddBody(rattler, rattler.Weight);
        foreach (float stone in _chest.Stones) AddBody(null, stone);

        _words = new Node2D();
        AddChild(_words);
    }

    /// <param name="rattler">Null for a rag-wrapped stone: it moves and blocks, but never makes a sound.</param>
    void AddBody(Rattler rattler, float weight)
    {
        var body = new RigidBody2D
        {
            Mass = 0.2f + weight,
            CollisionLayer = 2,
            CollisionMask = 1 | 2,
            ContactMonitor = true,
            MaxContactsReported = 4,
            CanSleep = false,
            ContinuousCd = RigidBody2D.CcdMode.CastShape,
            PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.3f, Friction = 0.5f },
            Position = _rest + new Vector2(Rand() * (CavityHalfWidth - 20), CavityBottom - 20 - (float)_rng.NextDouble() * 40),
        };
        float radius = 7f + MathF.Min(10f, weight * 2.2f);
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
        AddChild(body);
        _items.Add((body, rattler, radius));
        _last[body] = (Vector2.Zero, 0);
    }

    /// <summary>
    /// What's inside and where, in the cavity's own frame (origin at the lock, y down), for the keyhole view.
    /// Item is null for a stone.
    /// </summary>
    public IEnumerable<(ItemDef Item, Vector2 Position, float Radius)> Contents() =>
        _items.Select(e => (e.Rattler?.Item, (e.Body.Position - _pos).Rotated(-_box.Rotation), e.Radius));

    public static Rect2 Cavity => new(-CavityHalfWidth, CavityTop, CavityHalfWidth * 2, CavityBottom - CavityTop);

    void AddWall(Vector2 offset, Vector2 size) =>
        _box.AddChild(new CollisionShape2D { Position = offset, Shape = new RectangleShape2D { Size = size } });

    float Rand() => (float)(_rng.NextDouble() * 2 - 1);

    /// <summary>Shakes the chest by itself for a while (used by the autoplay bot).</summary>
    public void AutoShake(double seconds)
    {
        if (Busy) return;
        Lifted?.Invoke();
        _shakeFrom = _rest;
        _autoShakeLeft = seconds;
        _autoShakeTime = 0;
    }

    Vector2 _shakeFrom;

    /// <summary>
    /// Two raps on the side. A solid chest answers with a dry "tok"; a false bottom booms and rings on, because
    /// there's a sealed hollow under the floor.
    /// </summary>
    public void Knock()
    {
        bool hollow = _chest.FalseBottom != null;
        var tween = CreateTween();
        for (int i = 0; i < 2; i++)
        {
            tween.TweenCallback(Callable.From(() =>
            {
                View.Wobble(hollow ? 0.45f : 0.15f);
                Sfx.PlayGroup("knock", hollow ? 0.8f : 1.1f, 2);
                if (hollow) Sfx.Play("hollow", 1f, 0);
                PopWord(hollow ? "BOMMM" : "tok", _pos + new Vector2(-120 + Rand() * 20, 10 + Rand() * 10), hollow ? 32 : 24,
                    hollow ? Palette.Gold : Palette.Text);
            }));
            tween.TweenInterval(hollow ? 0.42f : 0.22f);
        }
        if (hollow)
            tween.TweenCallback(Callable.From(() => PopWord("...ommm", _pos + new Vector2(-140, 44), 16, Palette.Muted)));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left } button) return;
        var mouse = GetGlobalMousePosition() - GlobalPosition;
        if (button.Pressed && _autoShakeLeft <= 0 && !_airborne && PartAt(mouse) != Part.None)
        {
            _dragging = true;
            _moved = false;
            _pressAt = mouse;
            _pressTime = Time.GetTicksMsec() / 1000.0;
            _pressedKeyhole = PartAt(mouse) == Part.Keyhole;
            _grab = mouse - _pos;
            _target = _pos;
            GetViewport().SetInputAsHandled();
        }
        else if (!button.Pressed && _dragging)
        {
            _dragging = false;
            if (_moved)
            {
                Drop(RestFor?.Invoke(_pos) ?? _rest);
                return;
            }
            // Pressed and let go without moving it: a click.
            if (Time.GetTicksMsec() / 1000.0 - _pressTime > 0.4) return;
            if (_pressedKeyhole) KeyholeClicked?.Invoke();
            else Clicked?.Invoke();
        }
    }

    Part PartAt(Vector2 local)
    {
        var p = local - _pos;
        if (Mathf.Abs(p.X) <= 30 && p.Y >= -20 && p.Y <= 32) return Part.Keyhole;
        bool body = p.X > -View.Width / 2 && p.X < View.Width / 2 && p.Y > View.Top - 10 && p.Y < View.Bottom + 10;
        return body ? Part.Body : Part.None;
    }

    /// <summary>Carries the chest to a spot and sets it down there (for the bot, or a keyboard shortcut).</summary>
    public void MoveTo(Vector2 spot)
    {
        if (Busy) return;
        Lifted?.Invoke();
        Drop(spot);
    }

    /// <summary>Lets go: the chest hops up if it has to, drifts over the spot and falls onto it.</summary>
    void Drop(Vector2 spot)
    {
        _rest = spot;
        _airborne = true;
        float rise = _pos.Y - spot.Y + 24f;
        if (rise > 0) _vel.Y = MathF.Min(_vel.Y, -MathF.Sqrt(2f * Gravity * rise));
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _settling -= delta;
        foreach (var key in _wordCooldown.Keys.ToList()) _wordCooldown[key] -= delta;
        Hover = _dragging || _airborne ? Part.None : PartAt(GetGlobalMousePosition() - GlobalPosition);

        if (_autoShakeLeft > 0)
        {
            _autoShakeLeft -= delta;
            _autoShakeTime += delta;
            float t = (float)_autoShakeTime;
            _target = _shakeFrom + new Vector2(MathF.Sin(t * 13f) * 90f, -60f + MathF.Sin(t * 21f) * 50f);
            if (_autoShakeLeft <= 0) Drop(_shakeFrom);
        }
        else if (_dragging)
        {
            var mouse = GetGlobalMousePosition() - GlobalPosition;
            if (!_moved && mouse.DistanceTo(_pressAt) > 8)
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
            // Heavier chests follow the hand more slowly: you feel the weight.
            float rate = 22f / (1f + _totalWeight / 12f);
            _pos = _pos.Lerp(_target, 1f - MathF.Exp(-rate * dt));
        }
        else if (_airborne)
        {
            // Set down: fall back onto the counter and land with a thunk that scales with weight.
            _vel.Y += Gravity * dt;
            _pos.X = Mathf.Lerp(_pos.X, _rest.X, 1f - MathF.Exp(-12f * dt));
            _pos.Y += _vel.Y * dt;
            if (_vel.Y > 0 && _pos.Y >= _rest.Y)
            {
                float impact = _vel.Y;
                _pos = _rest;
                _airborne = false;
                _vel = Vector2.Zero;
                Landed?.Invoke(_rest);
                if (impact > 150)
                {
                    float loud = Mathf.Clamp(impact / 1400f, 0, 1) * Mathf.Clamp(_totalWeight / 18f, 0.3f, 1.2f);
                    Sfx.PlayGroup("land", 1.15f - Mathf.Clamp(_totalWeight / 40f, 0, 0.5f), Mathf.LinearToDb(0.2f + loud));
                    Main.Instance.Shake(0.05f + 0.15f * loud);
                }
            }
        }
        if (!_airborne) _vel = (_pos - previous) / Mathf.Max(dt, 0.0001f);

        float tilt = Dragging ? Mathf.Clamp(_vel.X * 0.00035f, -0.3f, 0.3f) : Mathf.Lerp(_box.Rotation, 0, 0.2f);
        _box.Position = _pos;
        _box.Rotation = tilt;
        View.Position = _pos;
        View.Rotation = tilt;

        foreach (var (body, rattler, _) in _items)
        {
            // A very fast drag can push an item through a wall; put it back rather than lose it.
            if (body.Position.DistanceTo(_pos) > 180)
            {
                body.Position = _pos + new Vector2(0, CavityBottom - 30);
                body.LinearVelocity = Vector2.Zero;
            }

            var (lastVelocity, cooldown) = _last[body];
            cooldown -= delta;
            float hit = (body.LinearVelocity - lastVelocity).Length();
            if (rattler != null && _settling <= 0 && hit > ImpactThreshold && cooldown <= 0 && body.GetContactCount() > 0)
            {
                cooldown = 0.12;
                Impact(rattler, hit, body.Position);
            }
            _last[body] = (body.LinearVelocity, cooldown);
        }
    }

    void Impact(Rattler rattler, float strength, Vector2 at)
    {
        HeardAnything = true;
        float loud = Mathf.Clamp((strength - ImpactThreshold) / 700f, 0f, 1f);
        // Always the same recording at the same pitch (give or take a hair), so each item is learnable by ear.
        var sound = rattler.Sound;
        Sfx.PlayGroup(sound.Group, sound.Pitch * (0.98f + 0.04f * (float)_rng.NextDouble()),
            Mathf.LinearToDb(0.45f + 0.55f * loud) + 3f + sound.VolumeDb);
        // Each item shows its word on its own clock, so a quiet item isn't drowned out by a noisy one.
        if (_wordCooldown.GetValueOrDefault(rattler.Item) <= 0)
        {
            _wordCooldown[rattler.Item] = 0.3;
            bool big = rattler.Sound.Word.ToUpperInvariant() == rattler.Sound.Word;
            // Words burst outward from the chest, each item in its own lane, so overlapping hits stay readable.
            var outward = (at - _pos).Normalized();
            int lane = _items.FindIndex(e => e.Rattler == rattler);
            var spot = _pos + outward * 120f + new Vector2(Rand() * 24, -44 + (lane % 4) * 30 + Rand() * 8);
            PopWord(rattler.Sound.Word, spot, big ? 32 : 24, new Color("#fff4dc"));
        }
        Impacted?.Invoke(rattler, strength);
    }

    void PopWord(string text, Vector2 at, int size, Color color)
    {
        if (_words.GetChildCount() > 10) return;
        var label = UiKit.MakeLabel(text, size, color, HorizontalAlignment.Center, outline: 8).Place(at.X - 90, at.Y - 18, 180, 36);
        label.PivotOffset = new Vector2(90, 18);
        label.Rotation = Rand() * 0.25f;
        label.Scale = Vector2.One * 0.6f;
        _words.AddChild(label);
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "scale", Vector2.One, 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "position:y", label.Position.Y - 36 - (float)_rng.NextDouble() * 20, 0.8f).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "position:x", label.Position.X + Rand() * 30, 0.8f);
        tween.TweenProperty(label, "modulate:a", 0f, 0.3f).SetDelay(0.5f);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
