using System;
using System.Collections.Generic;
using Godot;

namespace LootFall.View;

/// <summary>
/// Effects, all code-driven over a small library of greyscale textures (assets/vfx): bursts of sparks, rings, beams,
/// slashes, floating numbers, coins. Additive light in the knight's colours for his blows, the threat colours for the
/// monsters'; rarity colours only for loot (docs/art-direction.md, "Effects").
/// </summary>
public static class Fx
{
    static readonly Random Rng = new();
    static CanvasItemMaterial _add;

    public static CanvasItemMaterial Additive => _add ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    public static void Release() => _add = null;

    static float R(float a, float b) => a + (b - a) * (float)Rng.NextDouble();

    /// <summary>A one-shot burst of particles that frees itself.</summary>
    public static void Burst(Node parent, Vector2 at, Color color, int amount = 24, float speed = 380, float spread = 180,
        float gravity = 600, float life = 0.6f, float size = 0.12f, string texture = "spark1", Vector2? direction = null, bool additive = true)
    {
        if (!GodotObject.IsInstanceValid(parent) || amount <= 0) return;
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 0.4f, 1f };
        ramp.Colors = new[] { color.Lightened(0.6f), color, color with { A = 0 } };
        var p = new CpuParticles2D
        {
            Emitting = false, OneShot = true, Amount = amount, Lifetime = life, Explosiveness = 0.95f,
            Texture = Assets.Vfx(texture), Direction = direction ?? Vector2.Up, Spread = spread,
            InitialVelocityMin = speed * 0.35f, InitialVelocityMax = speed, Gravity = new Vector2(0, gravity),
            DampingMin = 40, DampingMax = 160, ScaleAmountMin = size * 0.5f, ScaleAmountMax = size, ColorRamp = ramp,
            AngleMin = 0, AngleMax = 360, Position = at, ZIndex = 30,
        };
        if (additive) p.Material = Additive;
        parent.AddChild(p);
        p.Emitting = true;
        parent.GetTree().CreateTimer(life + 0.3, false).Timeout += () => { if (GodotObject.IsInstanceValid(p)) p.QueueFree(); };
    }

    /// <summary>A soft puff of smoke or dust.</summary>
    public static void Puff(Node parent, Vector2 at, Color color, int amount = 10, float size = 0.5f, float speed = 120)
    {
        Burst(parent, at, color, amount, speed, 180, -30, 0.8f, size, "smoke1", null, additive: false);
    }

    /// <summary>A sprite flashed once at a spot: grows, fades, frees itself.</summary>
    public static Sprite2D Pop(Node parent, string texture, Vector2 at, Color color, float from, float to, float duration,
        float rotation = 0, bool additive = true, int z = 28)
    {
        var s = new Sprite2D { Texture = Assets.Vfx(texture), Position = at, Modulate = color, Scale = Vector2.One * from, Rotation = rotation, ZIndex = z };
        if (additive) s.Material = Additive;
        parent.AddChild(s);
        var t = s.CreateTween().SetParallel();
        t.TweenProperty(s, "scale", Vector2.One * to, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        t.TweenProperty(s, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        t.Chain().TweenCallback(Callable.From(s.QueueFree));
        return s;
    }

    /// <summary>The arc of a blow: a crescent sweeping through the target, in the weapon's colour.</summary>
    public static void Slash(Node parent, Vector2 at, Color color, float size = 1f, bool flip = false)
    {
        Pop(parent, "slash1", at, color, 0.5f * size, 1.15f * size, 0.18f, flip ? 2.4f : -0.6f + R(-0.2f, 0.2f));
        Pop(parent, "slash2", at, new Color(1, 1, 1, 0.8f), 0.35f * size, 0.9f * size, 0.12f, flip ? 2.6f : -0.4f);
    }

    /// <summary>A shockwave ring.</summary>
    public static void Ring(Node parent, Vector2 at, Color color, float radius = 160, float duration = 0.4f, float width = 6)
    {
        float r = 8;
        var ring = new Painter { Position = at, Material = Additive, ZIndex = 29, Modulate = color };
        ring.OnDraw = p => p.DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 64, Colors.White, width, true);
        parent.AddChild(ring);
        var t = ring.CreateTween().SetParallel();
        t.TweenMethod(Callable.From<float>(v => { r = v; ring.QueueRedraw(); }), 8f, radius, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        t.TweenProperty(ring, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        t.Chain().TweenCallback(Callable.From(ring.QueueFree));
    }

    /// <summary>A pillar of light standing on a spot (a rare chest's beam).</summary>
    public static Node2D Beam(Node parent, Vector2 at, Color color, float width = 70, float height = 700, float duration = 1.2f, bool stay = false)
    {
        var beam = new Painter(p =>
        {
            for (int band = 3; band >= 1; band--)
            {
                float w = width * band / 3f;
                var bottom = color with { A = 0.2f * (4 - band) };
                p.DrawPolygon(new[] { new Vector2(-w / 2, 0), new Vector2(w / 2, 0), new Vector2(w / 4, -height), new Vector2(-w / 4, -height) },
                    new[] { bottom, bottom, color with { A = 0 }, color with { A = 0 } });
            }
        }) { Position = at, Material = Additive, Scale = new Vector2(0.15f, 1), ZIndex = 5 };
        parent.AddChild(beam);
        var t = beam.CreateTween();
        t.TweenProperty(beam, "scale:x", 1f, 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        if (!stay)
        {
            t.TweenProperty(beam, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
            t.TweenCallback(Callable.From(beam.QueueFree));
        }
        return beam;
    }

    /// <summary>Slowly turning rays behind something; returned so they can be faded.</summary>
    public static Node2D Rays(Node parent, Vector2 at, Color color, float radius = 260, int count = 14, float alpha = 0.3f, float spin = 10)
    {
        var rays = new Painter(p =>
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.Tau / count;
                p.DrawPolygon(new[] { Vector2.Zero, Vector2.FromAngle(a - 0.08f) * radius, Vector2.FromAngle(a + 0.08f) * radius },
                    new[] { color with { A = alpha }, color with { A = 0 }, color with { A = 0 } });
            }
        }) { Position = at, Material = Additive, ZIndex = 3 };
        parent.AddChild(rays);
        rays.CreateTween().SetLoops().TweenProperty(rays, "rotation", Mathf.Tau, spin).AsRelative();
        return rays;
    }

    /// <summary>A soft glow; returned so it can be removed.</summary>
    public static Sprite2D Glow(Node parent, Vector2 at, Color color, float size = 1f, int z = 2)
    {
        var s = new Sprite2D { Texture = Assets.Vfx("glow"), Position = at, Modulate = color, Scale = Vector2.One * size, Material = Additive, ZIndex = z };
        parent.AddChild(s);
        return s;
    }

    /// <summary>Flies a node along an arc to <paramref name="to"/>, then calls <paramref name="arrive"/> and frees it.</summary>
    public static void Fly(Node2D node, Vector2 to, float duration, float arc, Action arrive = null, bool free = true, float spin = 0)
    {
        var from = node.Position;
        var mid = (from + to) / 2 + new Vector2(0, -arc);
        var t = node.CreateTween();
        t.TweenMethod(Callable.From<float>(k =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            float u = 1 - k;
            node.Position = u * u * from + 2 * u * k * mid + k * k * to;
            if (spin != 0) node.Rotation = spin * k;
        }), 0f, 1f, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        t.TweenCallback(Callable.From(() =>
        {
            arrive?.Invoke();
            if (free && GodotObject.IsInstanceValid(node)) node.QueueFree();
        }));
    }

    /// <summary>A bump in scale, for counters and tiles that just changed.</summary>
    public static void Bump(CanvasItem item, float amount = 1.2f, float duration = 0.2f)
    {
        if (!GodotObject.IsInstanceValid(item)) return;
        if (item is Control c) c.PivotOffset = c.Size / 2;
        var t = item.CreateTween();
        t.TweenProperty(item, "scale", Vector2.One * amount, duration * 0.35f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        t.TweenProperty(item, "scale", Vector2.One, duration * 0.65f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }
}

/// <summary>
/// Floating numbers, kept to the on-screen budget (docs/art-direction.md: at most ten at once; the oldest goes first).
/// Lilita One in an ink outline: white for the knight's blows, gold and bigger for crits, red-orange for damage taken.
/// </summary>
public partial class Numbers : Node2D
{
    public const int Budget = 10;
    readonly List<Label> _live = new();
    public bool Enabled = true;

    public Label Show(string text, Vector2 at, Color color, int size = 34, float rise = 70, float duration = 0.8f, float drift = 0,
        bool force = false)
    {
        if (!Enabled && !force) return null;
        _live.RemoveAll(l => !IsInstanceValid(l));
        while (_live.Count >= Budget)
        {
            var old = _live[0];
            _live.RemoveAt(0);
            if (IsInstanceValid(old)) old.QueueFree();
        }
        var label = Ui.Text(text, size, color, Look.Pop, HorizontalAlignment.Center, outline: Math.Max(6, size / 4)).Place(at.X - 200, at.Y - size, 400, size * 1.6f);
        label.PivotOffset = new Vector2(200, size * 0.8f);
        label.Scale = Vector2.One * 0.4f;
        AddChild(label);
        _live.Add(label);
        var t = label.CreateTween().SetParallel();
        t.TweenProperty(label, "scale", Vector2.One * 1.25f, 0.07f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        t.Chain().TweenProperty(label, "scale", Vector2.One, 0.12f);
        t.TweenProperty(label, "position:y", label.Position.Y - rise, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        if (drift != 0) t.TweenProperty(label, "position:x", label.Position.X + drift, duration).SetEase(Tween.EaseType.Out);
        t.TweenProperty(label, "modulate:a", 0f, duration * 0.3f).SetDelay(duration * 0.7f);
        t.Chain().TweenCallback(Callable.From(label.QueueFree));
        return label;
    }
}
