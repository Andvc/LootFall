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

    static Texture2D MakeSquare(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }
}
