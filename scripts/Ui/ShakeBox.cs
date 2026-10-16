using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Audio;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// A closed chest you can pick up and shake. Whatever is loose inside is simulated as invisible physics bodies in
/// a box that follows the chest; every hit plays that material's recording and pops its word out of the chest.
/// Heavier chests lag behind the mouse and land harder. Nothing here states a value: the player listens.
/// </summary>
public partial class ShakeBox : Node2D
{
    const float CavityHalfWidth = 72f, CavityTop = -70f, CavityBottom = 40f, Wall = 48f;
    const float ImpactThreshold = 110f;

    public ChestView View { get; private set; }

    /// <summary>Fires on every audible hit inside, with how hard it was (px/s of velocity change).</summary>
    public event Action<Rattler, float> Impacted;

    /// <summary>True once the player has shaken hard enough to hear something.</summary>
    public bool HeardAnything { get; private set; }

    public bool Dragging => _dragging || _autoShakeLeft > 0;

    readonly Chest _chest;
    readonly Rect2 _dragBounds;
    readonly Random _rng = new();
    readonly List<(RigidBody2D Body, Rattler Rattler)> _items = new();
    readonly Dictionary<RigidBody2D, (Vector2 Velocity, double Cooldown)> _last = new();
    Vector2 _rest, _pos, _vel, _target;
    AnimatableBody2D _box;
    Node2D _words;
    bool _dragging, _airborne;
    double _autoShakeLeft, _autoShakeTime, _wordCooldown;
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

        foreach (var rattler in _chest.Rattlers)
        {
            var body = new RigidBody2D
            {
                Mass = 0.2f + rattler.Weight,
                CollisionLayer = 2,
                CollisionMask = 1 | 2,
                ContactMonitor = true,
                MaxContactsReported = 4,
                CanSleep = false,
                ContinuousCd = RigidBody2D.CcdMode.CastShape,
                PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.3f, Friction = 0.5f },
                Position = _rest + new Vector2(Rand() * (CavityHalfWidth - 20), CavityBottom - 20 - (float)_rng.NextDouble() * 40),
            };
            float radius = 7f + MathF.Min(10f, rattler.Weight * 2.2f);
            body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
            AddChild(body);
            _items.Add((body, rattler));
            _last[body] = (Vector2.Zero, 0);
        }

        _words = new Node2D();
        AddChild(_words);
    }

    void AddWall(Vector2 offset, Vector2 size) =>
        _box.AddChild(new CollisionShape2D { Position = offset, Shape = new RectangleShape2D { Size = size } });

    float Rand() => (float)(_rng.NextDouble() * 2 - 1);

    /// <summary>Shakes the chest by itself for a while (used by the autoplay bot).</summary>
    public void AutoShake(double seconds)
    {
        _autoShakeLeft = seconds;
        _autoShakeTime = 0;
    }

    /// <summary>A heavy rap on the bottom: two knocks, and a hollow echo if there's a false bottom.</summary>
    public void Knock()
    {
        bool hollow = _chest.FalseBottom != null;
        View.Wobble(0.25f);
        var tween = CreateTween();
        for (int i = 0; i < 2; i++)
        {
            tween.TweenCallback(Callable.From(() =>
            {
                Sfx.PlayGroup(hollow ? "knock_hollow" : "knock_solid", hollow ? 0.7f : 1.1f, 2);
                PopWord(hollow ? "BONNG" : "tok", View.Position + new Vector2(Rand() * 40, 30), hollow ? 24 : 16, Palette.Text);
            }));
            tween.TweenInterval(0.28f);
        }
        if (hollow)
            tween.TweenCallback(Callable.From(() =>
            {
                Sfx.PlayGroup("knock_hollow", 0.6f, -10);
                PopWord("...ong", View.Position + new Vector2(50, 20), 16, Palette.Muted);
            }));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            if (button.Pressed && HitTest(GetGlobalMousePosition()))
            {
                _dragging = true;
                Sfx.PlayGroup("latch", 1.3f, -12);
                GetViewport().SetInputAsHandled();
            }
            else if (!button.Pressed && _dragging)
            {
                _dragging = false;
                _airborne = true;
            }
        }
    }

    bool HitTest(Vector2 global)
    {
        var local = global - GlobalPosition - _pos;
        return local.X > -View.Width / 2 && local.X < View.Width / 2 && local.Y > View.Top - 10 && local.Y < View.Bottom + 10;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _wordCooldown -= delta;
        _settling -= delta;

        if (_autoShakeLeft > 0)
        {
            _autoShakeLeft -= delta;
            _autoShakeTime += delta;
            float t = (float)_autoShakeTime;
            _target = _rest + new Vector2(MathF.Sin(t * 13f) * 90f, -60f + MathF.Sin(t * 21f) * 50f);
            if (_autoShakeLeft <= 0) _airborne = true;
        }
        else if (_dragging)
        {
            var mouse = GetGlobalMousePosition() - GlobalPosition;
            _target = new Vector2(
                Mathf.Clamp(mouse.X, _dragBounds.Position.X, _dragBounds.End.X),
                Mathf.Clamp(mouse.Y, _dragBounds.Position.Y, _dragBounds.End.Y));
        }

        var previous = _pos;
        if (Dragging)
        {
            // Heavier chests follow the hand more slowly: you feel the weight.
            float rate = 22f / (1f + _totalWeight / 5f);
            _pos = _pos.Lerp(_target, 1f - MathF.Exp(-rate * dt));
        }
        else if (_airborne)
        {
            // Set down: fall back onto the counter and land with a thunk that scales with weight.
            _vel.Y += 2600f * dt;
            _pos.X = Mathf.Lerp(_pos.X, _rest.X, 1f - MathF.Exp(-8f * dt));
            _pos.Y += _vel.Y * dt;
            if (_pos.Y >= _rest.Y)
            {
                float impact = _vel.Y;
                _pos.Y = _rest.Y;
                _airborne = false;
                _vel = Vector2.Zero;
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

        foreach (var (body, rattler) in _items)
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
            if (_settling <= 0 && hit > ImpactThreshold && cooldown <= 0 && body.GetContactCount() > 0)
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
        // Rattles are the main clue, so even light taps stay clearly audible.
        Sfx.PlayGroup(rattler.Sound, 0.92f + 0.16f * (float)_rng.NextDouble(), Mathf.LinearToDb(0.4f + 0.6f * loud) + 3f);
        if (_wordCooldown <= 0)
        {
            _wordCooldown = 0.11;
            bool big = loud > 0.5f || rattler.Word.ToUpperInvariant() == rattler.Word;
            // Words burst outward from the chest so overlapping hits stay readable.
            var outward = (at - _pos).Normalized();
            var spot = _pos + outward * 120f + new Vector2(Rand() * 30, -20 + Rand() * 24);
            PopWord(rattler.Word + (loud > 0.7f ? "!" : ""), spot, big ? 32 : 24, new Color("#fff4dc"));
        }
        Impacted?.Invoke(rattler, strength);
    }

    void PopWord(string text, Vector2 at, int size, Color color)
    {
        if (_words.GetChildCount() > 8) return;
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
