using System;
using Godot;

namespace LootFall.Ui;

/// <summary>Particle presets, flights and pixel effects: the "juice".</summary>
public static class Fx
{
    static Texture2D _pixel, _coin;
    static readonly Random Rng = new();

    /// <summary>A hard 2x2 square: scaled up, particles read as chunky pixels rather than soft dots.</summary>
    public static Texture2D Pixel => _pixel ??= MakeSquare(2);

    public static Texture2D Coin => _coin ??= GD.Load<Texture2D>("res://assets/sprites/coin.png");

    public static CanvasItemMaterial Additive() => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    /// <summary>Lets go of the cached textures (before quitting).</summary>
    public static void Release() => _pixel = _coin = null;

    static float R(float a, float b) => a + (b - a) * (float)Rng.NextDouble();

    public static CpuParticles2D Sparks(Color color, int amount, float speedMin, float speedMax,
        float spread = 70f, float lifetime = 1.1f, float gravity = 600f, float size = 3f)
    {
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 0.5f, 1f };
        ramp.Colors = new[] { color.Lightened(0.7f), color, color with { A = 0f } };
        return new CpuParticles2D
        {
            Emitting = false, OneShot = true, Amount = Math.Max(1, amount), Lifetime = lifetime, Explosiveness = 0.95f,
            Texture = Pixel, Material = Additive(), Direction = Vector2.Up, Spread = spread,
            InitialVelocityMin = speedMin, InitialVelocityMax = speedMax, Gravity = new Vector2(0, gravity),
            DampingMin = 20, DampingMax = 80, ScaleAmountMin = size * 0.5f, ScaleAmountMax = size, ColorRamp = ramp, ZIndex = 25,
        };
    }

    /// <summary>A one-shot spark burst that frees itself.</summary>
    public static void Burst(Node parent, Vector2 position, Color color, int amount = 40, float speed = 260f,
        float spread = 180f, float gravity = 200f, float lifetime = 0.8f, float size = 3f)
    {
        if (amount <= 0 || !GodotObject.IsInstanceValid(parent)) return;
        var particles = Sparks(color, amount, speed * 0.4f, speed, spread, lifetime, gravity, size);
        particles.Position = position;
        parent.AddChild(particles);
        particles.Emitting = true;
        parent.GetTree().CreateTimer(lifetime + 0.4, false).Timeout += particles.QueueFree;
    }

    /// <summary>A puff of dust where something lands, freed when done.</summary>
    public static void Dust(Node parent, Vector2 position, float strength = 1f, Color? color = null)
    {
        var c = color ?? new Color("#d8c6a6");
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 1f };
        ramp.Colors = new[] { c with { A = 0.85f }, c with { A = 0f } };
        var dust = new CpuParticles2D
        {
            Emitting = false, OneShot = true, Amount = (int)(14 * strength) + 4, Lifetime = 0.6f, Explosiveness = 1f,
            Texture = Pixel, Direction = Vector2.Up, Spread = 80, InitialVelocityMin = 60 * strength,
            InitialVelocityMax = 200 * strength, Gravity = new Vector2(0, 260), DampingMin = 120, DampingMax = 240,
            ScaleAmountMin = 2f, ScaleAmountMax = 4f, ColorRamp = ramp, Position = position, ZIndex = 18,
        };
        parent.AddChild(dust);
        dust.Emitting = true;
        parent.GetTree().CreateTimer(1.0, false).Timeout += dust.QueueFree;
    }

    /// <summary>A shockwave ring expanding from a spot.</summary>
    public static void Ring(Node parent, Vector2 at, Color color, float radius = 160f, float duration = 0.45f, float width = 6f)
    {
        float r = 8f;
        var ring = new Painter(p => p.DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 48, color, width)) { Position = at, Material = Additive(), ZIndex = 26 };
        parent.AddChild(ring);
        var tween = ring.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(v =>
        {
            r = v;
            ring.QueueRedraw();
        }), 8f, radius, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ring, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(ring.QueueFree));
    }

    /// <summary>A beam of light standing on a spot, fading out.</summary>
    public static void Pillar(Node parent, Vector2 at, Color color, float width = 56f, float height = 560f, float duration = 1.1f)
    {
        var beam = new Painter(p =>
        {
            for (int band = 3; band >= 1; band--)
            {
                float w = width * band / 3f;
                var bottom = color with { A = 0.22f * (4 - band) };
                p.DrawPolygon(new[] { new Vector2(-w / 2, 0), new Vector2(w / 2, 0), new Vector2(w / 2, -height), new Vector2(-w / 2, -height) },
                    new[] { bottom, bottom, color with { A = 0 }, color with { A = 0 } });
            }
        }) { Position = at, Material = Additive(), Scale = new Vector2(0.2f, 1f), ZIndex = 4 };
        parent.AddChild(beam);
        var tween = beam.CreateTween();
        tween.TweenProperty(beam, "scale:x", 1f, 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(beam, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(beam.QueueFree));
    }

    /// <summary>Slowly turning rays of light behind something. Returns them, so they can be faded out.</summary>
    public static Node2D Rays(Node parent, Vector2 at, Color color, float radius = 150f, int count = 12, float alpha = 0.35f, float spin = 9f)
    {
        var rays = new Painter(p =>
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.Tau / count;
                p.DrawPolygon(new[] { Vector2.Zero, Vector2.FromAngle(a - 0.09f) * radius, Vector2.FromAngle(a + 0.09f) * radius },
                    new[] { color with { A = alpha }, color with { A = 0 }, color with { A = 0 } });
            }
        }) { Position = at, Material = Additive(), ZIndex = 3 };
        parent.AddChild(rays);
        rays.CreateTween().SetLoops().TweenProperty(rays, "rotation", Mathf.Tau, spin).AsRelative();
        return rays;
    }

    /// <summary>A soft glow that breathes. Returns it, so it can be removed.</summary>
    public static Node2D Aura(Node parent, Vector2 at, Color color, float radius = 70f, float alpha = 0.09f)
    {
        var aura = new Painter(p =>
        {
            for (int i = 4; i >= 1; i--) p.DrawCircle(Vector2.Zero, radius * i / 4f, color with { A = alpha });
        }) { Position = at, Material = Additive(), ZIndex = 2 };
        parent.AddChild(aura);
        var pulse = aura.CreateTween().SetLoops();
        pulse.TweenProperty(aura, "scale", Vector2.One * 1.15f, 0.7f).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(aura, "scale", Vector2.One * 0.9f, 0.7f).SetTrans(Tween.TransitionType.Sine);
        return aura;
    }

    /// <summary>Jagged bolts crackling out of a spot, redrawn every frame for a moment.</summary>
    public static void Lightning(Node parent, Vector2 at, Color color, int bolts = 5, float reach = 150f, float duration = 0.5f)
    {
        var bolt = new Painter(p =>
        {
            for (int b = 0; b < bolts; b++)
            {
                float angle = Mathf.Tau * b / bolts + R(0, 0.8f);
                var dir = Vector2.FromAngle(angle);
                var points = new Vector2[7];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / (float)(points.Length - 1);
                    points[i] = dir * reach * t + dir.Orthogonal() * R(-1, 1) * 18f * t;
                }
                p.DrawPolyline(points, color.Lightened(0.5f), 4f);
                p.DrawPolyline(points, Colors.White, 2f);
            }
        }) { Position = at, Material = Additive(), ZIndex = 26 };
        parent.AddChild(bolt);
        var tween = bolt.CreateTween();
        tween.TweenMethod(Callable.From<float>(_ => bolt.QueueRedraw()), 0f, 1f, duration);
        tween.Parallel().TweenProperty(bolt, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(bolt.QueueFree));
    }

    /// <summary>A shower of many-coloured confetti.</summary>
    public static void Confetti(Node parent, Vector2 at, int amount = 90)
    {
        var colours = new Gradient();
        colours.Offsets = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
        colours.Colors = new[] { new Color("#ff6b6b"), new Color("#ffb52e"), new Color("#6fdc8c"), new Color("#4ea3ff"), new Color("#c792ff") };
        var confetti = new CpuParticles2D
        {
            Emitting = false, OneShot = true, Amount = amount, Lifetime = 2.2f, Explosiveness = 0.9f, Texture = Pixel,
            Direction = Vector2.Up, Spread = 75, InitialVelocityMin = 380, InitialVelocityMax = 820, Gravity = new Vector2(0, 700),
            DampingMin = 60, DampingMax = 140, AngularVelocityMin = -360, AngularVelocityMax = 360,
            ScaleAmountMin = 2.5f, ScaleAmountMax = 4.5f, ColorInitialRamp = colours, Position = at, ZIndex = 40,
        };
        parent.AddChild(confetti);
        confetti.Emitting = true;
        parent.GetTree().CreateTimer(2.6, false).Timeout += confetti.QueueFree;
    }

    /// <summary>
    /// Flies a node from where it is to <paramref name="to"/> along an arc (bulging up by <paramref name="arc"/>), then
    /// calls <paramref name="arrive"/> and frees it.
    /// </summary>
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

    /// <summary>
    /// A spray of coins from a spot that, after a short hang, fly to <paramref name="to"/> (the gold counter); each
    /// arrival calls <paramref name="each"/> (a clink, a bump of the counter).
    /// </summary>
    public static void CoinsTo(Node parent, Vector2 from, Vector2 to, int count, Action each = null, float delay = 0.15f)
    {
        count = Math.Clamp(count, 1, 16);
        for (int i = 0; i < count; i++)
        {
            var coin = new Sprite2D { Texture = Coin, Position = from, Scale = Vector2.One * 2, ZIndex = 60 };
            parent.AddChild(coin);
            var spray = from + new Vector2(R(-60, 60), R(-70, -10));
            var t = coin.CreateTween();
            t.TweenProperty(coin, "position", spray, 0.18f + R(0, 0.1f)).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            t.TweenInterval(delay + i * 0.035f);
            t.TweenCallback(Callable.From(() => Fly(coin, to, 0.45f + R(0, 0.15f), R(40, 120), each)));
        }
    }

    /// <summary>Text that pops up, drifts and fades: damage numbers, gold, "LUCKY!".</summary>
    public static Label Float(Node parent, string text, Vector2 at, Color color, int size = 24, float rise = 60f,
        float duration = 0.9f, float drift = 0f, float pop = 1.35f)
    {
        var label = UiKit.MakeLabel(text, size, color, HorizontalAlignment.Center, outline: 6).Place(at.X - 150, at.Y - 18, 300, 40);
        label.PivotOffset = new Vector2(150, 20);
        label.Scale = Vector2.One * 0.5f;
        label.ZIndex = 50;
        parent.AddChild(label);
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "scale", Vector2.One * pop, 0.08f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenProperty(label, "scale", Vector2.One, 0.12f);
        tween.TweenProperty(label, "position:y", label.Position.Y - rise, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        if (drift != 0) tween.TweenProperty(label, "position:x", label.Position.X + drift, duration).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, duration * 0.35f).SetDelay(duration * 0.65f);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
        return label;
    }

    /// <summary>A little bump in scale, for counters and slots that just changed.</summary>
    public static void Bump(CanvasItem item, float amount = 1.25f, float duration = 0.18f)
    {
        if (item is Control c) c.PivotOffset = c.Size / 2;
        var prop = "scale";
        var t = item.CreateTween();
        t.TweenProperty(item, prop, Vector2.One * amount, duration * 0.35f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        t.TweenProperty(item, prop, Vector2.One, duration * 0.65f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    static Texture2D MakeSquare(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }
}
