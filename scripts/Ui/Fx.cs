using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>Particle presets and generated textures for the "juice".</summary>
public static class Fx
{
    static Texture2D _dot;
    static Texture2D _coin;

    public static Texture2D Dot => _dot ??= MakeDot(24);
    public static Texture2D Coin => _coin ??= MakeCoin(14);

    public static CanvasItemMaterial Additive() => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    public static CpuParticles2D Sparks(Color color, int amount, float speedMin, float speedMax,
        float spread = 70f, float lifetime = 1.1f, float gravity = 600f)
    {
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 0.55f, 1f };
        ramp.Colors = new[] { color.Lightened(0.7f), color, color with { A = 0f } };
        return new CpuParticles2D
        {
            Emitting = false,
            OneShot = true,
            Amount = amount,
            Lifetime = lifetime,
            Explosiveness = 0.95f,
            Texture = Dot,
            Material = Additive(),
            Direction = Vector2.Up,
            Spread = spread,
            InitialVelocityMin = speedMin,
            InitialVelocityMax = speedMax,
            Gravity = new Vector2(0, gravity),
            DampingMin = 20,
            DampingMax = 80,
            ScaleAmountMin = 0.3f,
            ScaleAmountMax = 1.0f,
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
        AngularVelocityMin = -720,
        AngularVelocityMax = 720,
        ScaleAmountMin = 0.8f,
        ScaleAmountMax = 1.3f,
    };

    /// <summary>A one-shot sparkle burst that frees itself.</summary>
    public static void Burst(Node parent, Vector2 position, Color color, int amount = 40, float speed = 260f)
    {
        var particles = Sparks(color, amount, speed * 0.4f, speed, spread: 180f, lifetime: 0.8f, gravity: 200f);
        particles.Position = position;
        parent.AddChild(particles);
        particles.Emitting = true;
        parent.GetTree().CreateTimer(1.2).Timeout += particles.QueueFree;
    }

    static Texture2D MakeDot(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = new Vector2(x + 0.5f - r, y + 0.5f - r).Length() / r;
            float a = Mathf.Clamp(1f - d, 0f, 1f);
            image.SetPixel(x, y, new Color(1, 1, 1, a * a));
        }
        return ImageTexture.CreateFromImage(image);
    }

    static Texture2D MakeCoin(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size / 2f;
        var rim = new Color("#a8741a");
        var face = new Color("#ffd75e");
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = new Vector2(x + 0.5f - r, y + 0.5f - r).Length();
            if (d > r) image.SetPixel(x, y, Colors.Transparent);
            else image.SetPixel(x, y, d > r - 2f ? rim : face.Lerp(Colors.White, Math.Max(0, (x - y) / (float)size) * 0.6f));
        }
        return ImageTexture.CreateFromImage(image);
    }
}

/// <summary>Placeholder item icons, one per sound family, drawn as simple shapes.</summary>
public static class ItemIcons
{
    public static void Draw(Painter p, Sound sound, Color color)
    {
        var dark = color.Darkened(0.5f);
        switch (sound)
        {
            case Sound.Jingle:
                p.DrawCircle(Vector2.Zero, 17, dark);
                p.DrawCircle(Vector2.Zero, 13, color);
                p.DrawCircle(Vector2.Zero, 6, dark);
                p.DrawLine(new Vector2(-8, -8), new Vector2(-3, -12), Colors.White with { A = 0.7f }, 2);
                break;
            case Sound.Clink:
                p.DrawRect(new Rect2(-4, -20, 8, 10), dark);
                p.DrawColoredPolygon(new[] { new Vector2(-4, -11), new Vector2(4, -11), new Vector2(13, 3), new Vector2(10, 18), new Vector2(-10, 18), new Vector2(-13, 3) }, color);
                p.DrawLine(new Vector2(-7, 0), new Vector2(-6, 12), Colors.White with { A = 0.6f }, 2);
                break;
            case Sound.Clank:
                p.DrawLine(new Vector2(-14, 16), new Vector2(14, -18), color, 5);
                p.DrawLine(new Vector2(-14, 4), new Vector2(-2, 18), dark, 5);
                p.DrawCircle(new Vector2(-16, 18), 4, dark);
                break;
            case Sound.Thud:
                p.DrawColoredPolygon(new[] { new Vector2(-14, 16), new Vector2(-17, -2), new Vector2(-6, -17), new Vector2(10, -15), new Vector2(17, 0), new Vector2(12, 16) }, color);
                p.DrawLine(new Vector2(-6, -6), new Vector2(5, 2), dark, 2);
                p.DrawLine(new Vector2(5, 2), new Vector2(2, 10), dark, 2);
                break;
            default:
                p.DrawRect(new Rect2(-15, -12, 30, 24), color);
                p.DrawCircle(new Vector2(-15, 0), 5, dark);
                p.DrawCircle(new Vector2(15, 0), 5, dark);
                for (int i = 0; i < 3; i++) p.DrawLine(new Vector2(-9, -5 + i * 5), new Vector2(9, -5 + i * 5), dark, 1.5f);
                break;
        }
    }
}
