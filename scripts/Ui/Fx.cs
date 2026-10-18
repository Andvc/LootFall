using Godot;

namespace LootFall.Ui;

/// <summary>Particle presets and pixel effects for the "juice".</summary>
public static class Fx
{
    const int BurstFrames = 6;

    static Texture2D _pixel;
    static Texture2D _coin;
    static SpriteFrames _burst;

    /// <summary>A hard 2x2 square: scaled up, particles read as chunky pixels rather than soft dots.</summary>
    public static Texture2D Pixel => _pixel ??= MakeSquare(2);
    public static Texture2D Coin => _coin ??= GD.Load<Texture2D>("res://assets/sprites/items/gold_doubloons.png");

    public static CanvasItemMaterial Additive() => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    public static CpuParticles2D Sparks(Color color, int amount, float speedMin, float speedMax,
        float spread = 70f, float lifetime = 1.1f, float gravity = 600f)
    {
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 0.5f, 1f };
        ramp.Colors = new[] { color.Lightened(0.7f), color, color with { A = 0f } };
        return new CpuParticles2D
        {
            Emitting = false,
            OneShot = true,
            Amount = amount,
            Lifetime = lifetime,
            Explosiveness = 0.95f,
            Texture = Pixel,
            Material = Additive(),
            Direction = Vector2.Up,
            Spread = spread,
            InitialVelocityMin = speedMin,
            InitialVelocityMax = speedMax,
            Gravity = new Vector2(0, gravity),
            DampingMin = 20,
            DampingMax = 80,
            ScaleAmountMin = 1.5f,
            ScaleAmountMax = 3f,
            ColorRamp = ramp,
        };
    }

    public static CpuParticles2D Coins(int amount) => new()
    {
        Emitting = false,
        OneShot = true,
        Amount = amount,
        Lifetime = 1.8f,
        Explosiveness = 0.9f,
        Texture = Coin,
        Direction = Vector2.Up,
        Spread = 50,
        InitialVelocityMin = 450,
        InitialVelocityMax = 850,
        Gravity = new Vector2(0, 1400),
        ScaleAmountMin = 1.5f,
        ScaleAmountMax = 2f,
    };

    /// <summary>A one-shot spark burst that frees itself.</summary>
    public static void Burst(Node parent, Vector2 position, Color color, int amount = 40, float speed = 260f)
    {
        var particles = Sparks(color, amount, speed * 0.4f, speed, spread: 180f, lifetime: 0.8f, gravity: 200f);
        particles.Position = position;
        parent.AddChild(particles);
        particles.Emitting = true;
        parent.GetTree().CreateTimer(1.2).Timeout += particles.QueueFree;
    }

    /// <summary>A puff of dust where something lands, freed when done.</summary>
    public static void Dust(Node parent, Vector2 position, float strength = 1f)
    {
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 1f };
        ramp.Colors = new[] { new Color("#c9b79a") with { A = 0.8f }, new Color("#c9b79a") with { A = 0f } };
        var dust = new CpuParticles2D
        {
            Emitting = false,
            OneShot = true,
            Amount = (int)(14 * strength) + 4,
            Lifetime = 0.6f,
            Explosiveness = 1f,
            Texture = Pixel,
            Direction = Vector2.Up,
            Spread = 80,
            InitialVelocityMin = 60 * strength,
            InitialVelocityMax = 200 * strength,
            Gravity = new Vector2(0, 260),
            DampingMin = 120,
            DampingMax = 240,
            ScaleAmountMin = 2f,
            ScaleAmountMax = 4f,
            ColorRamp = ramp,
            Position = position,
        };
        parent.AddChild(dust);
        dust.Emitting = true;
        parent.GetTree().CreateTimer(1.0).Timeout += dust.QueueFree;
    }

    /// <summary>The pixel explosion ring (Superpowers fx #1), played once and freed.</summary>
    public static void Explosion(Node parent, Vector2 position, int scale, Color tint)
    {
        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = ExplosionFrames(),
            Position = position,
            Scale = Vector2.One * scale,
            Modulate = tint,
            Material = Additive(),
        };
        parent.AddChild(sprite);
        sprite.AnimationFinished += sprite.QueueFree;
        sprite.Play("burst");
    }

    static SpriteFrames ExplosionFrames()
    {
        if (_burst != null) return _burst;
        var sheet = GD.Load<Texture2D>("res://assets/sprites/fx/burst.png");
        int w = sheet.GetWidth() / BurstFrames, h = sheet.GetHeight();
        _burst = new SpriteFrames();
        _burst.RemoveAnimation("default");
        _burst.AddAnimation("burst");
        _burst.SetAnimationLoopMode("burst", SpriteFrames.LoopMode.None);
        _burst.SetAnimationSpeed("burst", 16);
        for (int i = 0; i < BurstFrames; i++)
            _burst.AddFrame("burst", new AtlasTexture { Atlas = sheet, Region = new Rect2(i * w, 0, w, h) });
        return _burst;
    }

    /// <summary>A beam of light standing on a spot (a rare find), fading out.</summary>
    public static void Pillar(Node parent, Vector2 at, Color color, float width = 56f, float height = 560f, float duration = 1.1f)
    {
        var beam = new Painter(p =>
        {
            // Three nested bands, brightest in the middle, each fading to nothing at the top.
            for (int band = 3; band >= 1; band--)
            {
                float w = width * band / 3f;
                var bottom = color with { A = 0.22f * (4 - band) };
                p.DrawPolygon(new[] { new Vector2(-w / 2, 0), new Vector2(w / 2, 0), new Vector2(w / 2, -height), new Vector2(-w / 2, -height) },
                    new[] { bottom, bottom, color with { A = 0 }, color with { A = 0 } });
            }
        }) { Position = at, Material = Additive(), Scale = new Vector2(0.2f, 1f) };
        parent.AddChild(beam);
        var tween = beam.CreateTween();
        tween.TweenProperty(beam, "scale:x", 1f, 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(beam, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(beam.QueueFree));
    }

    /// <summary>A shockwave ring expanding from a spot.</summary>
    public static void Ring(Node parent, Vector2 at, Color color, float radius = 160f, float duration = 0.45f, float width = 6f)
    {
        float r = 8f;
        var ring = new Painter(p => p.DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 48, color, width)) { Position = at, Material = Additive() };
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

    /// <summary>Jagged bolts crackling out of a spot (an epic find), redrawn every frame for a moment.</summary>
    public static void Lightning(Node parent, Vector2 at, Color color, int bolts = 5, float reach = 150f, float duration = 0.5f)
    {
        var rng = new System.Random();
        var bolt = new Painter(p =>
        {
            for (int b = 0; b < bolts; b++)
            {
                float angle = Mathf.Tau * b / bolts + (float)rng.NextDouble() * 0.8f;
                var dir = Vector2.FromAngle(angle);
                var points = new Vector2[7];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / (float)(points.Length - 1);
                    points[i] = dir * reach * t + dir.Orthogonal() * ((float)rng.NextDouble() * 2 - 1) * 18f * t;
                }
                p.DrawPolyline(points, color.Lightened(0.5f), 4f);
                p.DrawPolyline(points, Colors.White, 2f);
            }
        }) { Position = at, Material = Additive() };
        parent.AddChild(bolt);
        var tween = bolt.CreateTween();
        tween.TweenMethod(Callable.From<float>(_ => bolt.QueueRedraw()), 0f, 1f, duration);
        tween.Parallel().TweenProperty(bolt, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(bolt.QueueFree));
    }

    /// <summary>A soft glow that stays behind something and breathes. Returns it, so it can be removed.</summary>
    public static Node2D Aura(Node parent, Vector2 at, Color color, float radius = 70f)
    {
        var aura = new Painter(p =>
        {
            for (int i = 4; i >= 1; i--) p.DrawCircle(Vector2.Zero, radius * i / 4f, color with { A = 0.09f });
        }) { Position = at, Material = Additive(), ZIndex = 4 };
        parent.AddChild(aura);
        var pulse = aura.CreateTween().SetLoops();
        pulse.TweenProperty(aura, "scale", Vector2.One * 1.15f, 0.7f).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(aura, "scale", Vector2.One * 0.9f, 0.7f).SetTrans(Tween.TransitionType.Sine);
        return aura;
    }

    /// <summary>Slowly turning rays of light behind something (a legendary find). Returns them.</summary>
    public static Node2D Rays(Node parent, Vector2 at, Color color, float radius = 150f, int count = 12, float alpha = 0.35f)
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
        rays.CreateTween().SetLoops().TweenProperty(rays, "rotation", Mathf.Tau, 9f).AsRelative();
        return rays;
    }

    /// <summary>A shower of many-coloured confetti (a find two or more qualities better than its chest).</summary>
    public static void Confetti(Node parent, Vector2 at, int amount = 90)
    {
        var colours = new Gradient();
        colours.Offsets = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
        colours.Colors = new[] { new Color("#ff6b6b"), new Color("#ffb52e"), new Color("#6fdc8c"), new Color("#4ea3ff"), new Color("#c792ff") };
        var confetti = new CpuParticles2D
        {
            Emitting = false,
            OneShot = true,
            Amount = amount,
            Lifetime = 2.2f,
            Explosiveness = 0.9f,
            Texture = Pixel,
            Direction = Vector2.Up,
            Spread = 75,
            InitialVelocityMin = 380,
            InitialVelocityMax = 820,
            Gravity = new Vector2(0, 700),
            DampingMin = 60,
            DampingMax = 140,
            AngularVelocityMin = -360,
            AngularVelocityMax = 360,
            ScaleAmountMin = 2.5f,
            ScaleAmountMax = 4.5f,
            ColorInitialRamp = colours,
            Position = at,
        };
        parent.AddChild(confetti);
        confetti.Emitting = true;
        parent.GetTree().CreateTimer(2.6).Timeout += confetti.QueueFree;
    }

    static Texture2D MakeSquare(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }
}
