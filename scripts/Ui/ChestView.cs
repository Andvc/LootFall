using System;
using Godot;

namespace LootFall.Ui;

/// <summary>
/// The chest, drawn in tools/custom_art.py and split into lid and body by tools/import_art.py,
/// plus the light that leaks from the seam. The node's origin is the centre of the lid seam.
/// </summary>
public partial class ChestView : Node2D
{
    const int ArtWidth = 16, LidRows = 7, BodyRows = 5, OpenRimRows = 5;

    /// <summary>Screen pixels per art pixel. Set before adding to the tree.</summary>
    public int PixelSize = 12;

    /// <summary>Sprite set under res://assets/sprites/chest/ (red, blue, purple, wood, green).</summary>
    public string Skin = "red";

    /// <summary>The lock plate's colour, by maker; null leaves the plain brass hasp of an unlocked chest.</summary>
    public Color? LockColor;

    /// <summary>0..1 continuous jitter while the lid strains.</summary>
    public float Rumble;

    /// <summary>How far (screen px) the lid is lifted off the body.</summary>
    public float LidGap;

    /// <summary>0..1 intensity of the light leaking through the seam.</summary>
    public float Glow;

    public Color GlowColor = Colors.White;

    public float Width => ArtWidth * PixelSize;
    public float Top => -LidRows * PixelSize;
    public float Bottom => BodyRows * PixelSize;

    readonly Random _rng = new();
    Node2D _rig;
    Sprite2D _lid, _body;
    Painter _shadow, _backGlow, _seamLight, _lidLock, _bodyLock;
    float _time;
    float _wobble;
    bool _lidDetached, _open;

    public override void _Ready()
    {
        _rig = new Node2D();
        AddChild(_rig);
        float half = Width / 2;

        _shadow = new Painter(DrawShadow);
        _backGlow = new Painter(DrawBackGlow) { Material = Fx.Additive() };
        _body = new Sprite2D
        {
            Texture = Load("body"),
            Centered = false,
            Position = new Vector2(-half, 0),
            Scale = Vector2.One * PixelSize,
        };
        _lid = new Sprite2D
        {
            Texture = Load("lid"),
            Scale = Vector2.One * PixelSize,
        };
        _seamLight = new Painter(DrawSeamLight) { Material = Fx.Additive() };

        // Lock plate overlays, drawn in art pixels on the sprites they belong to.
        _lidLock = new Painter(DrawLidLock);
        _bodyLock = new Painter(DrawBodyLock);
        _lid.AddChild(_lidLock);
        _body.AddChild(_bodyLock);

        _rig.AddChild(_shadow);
        _rig.AddChild(_backGlow);
        _rig.AddChild(_body);
        _rig.AddChild(_lid);
        _rig.AddChild(_seamLight);
        PlaceLid();
    }

    Texture2D Load(string part) => GD.Load<Texture2D>($"res://assets/sprites/chest/{Skin}_{part}.png");

    /// <summary>A quick side-to-side rattle, e.g. when shaken.</summary>
    public void Wobble(float amount) => _wobble = Mathf.Max(_wobble, amount);

    /// <summary>Hands the lid over to the caller (to fling it off) and stops positioning it.</summary>
    public Node2D DetachLid()
    {
        _lidDetached = true;
        return _lid;
    }

    /// <summary>Swaps the body for the open sprite: gold rim and the dark interior.</summary>
    public void ShowOpen()
    {
        _open = true;
        _body.Texture = Load("open");
        _body.Position = new Vector2(-Width / 2, -OpenRimRows * PixelSize);
        _bodyLock.Visible = false;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _wobble = Mathf.Max(0, _wobble - dt * 1.6f);

        float jitter = Rumble * Rumble * 5f;
        _rig.Position = new Vector2(Rand() * jitter, Rand() * jitter * 0.5f);
        _rig.Rotation = MathF.Sin(_time * 38f) * 0.08f * _wobble + Rand() * 0.012f * Rumble;
        if (!_lidDetached) PlaceLid();

        _backGlow.QueueRedraw();
        _seamLight.QueueRedraw();
    }

    void PlaceLid() => _lid.Position = new Vector2(0, -LidRows * PixelSize / 2f - LidGap);

    float Rand() => (float)(_rng.NextDouble() * 2 - 1);

    void DrawShadow(Painter p)
    {
        float y = Bottom + PixelSize * 0.5f;
        float w = Width * 0.5f;
        // Stepped oval: three flat rectangles read as a pixel-art shadow.
        p.DrawRect(new Rect2(-w, y - PixelSize * 0.5f, w * 2, PixelSize), new Color(0, 0, 0, 0.35f));
        p.DrawRect(new Rect2(-w * 0.85f, y - PixelSize, w * 1.7f, PixelSize * 2), new Color(0, 0, 0, 0.2f));
    }

    // The lock sits on the gold band: lid row 6 (cols 7-9), body row 0 (cols 6 and 9, keyhole 7-8), body row 1 (cols 6-9).
    void DrawLidLock(Painter p)
    {
        if (LockColor is not Color c) return;
        p.DrawRect(new Rect2(7 - ArtWidth / 2f, 6 - LidRows / 2f, 3, 1), c);
    }

    void DrawBodyLock(Painter p)
    {
        if (LockColor is not Color c) return;
        p.DrawRect(new Rect2(6, 0, 1, 1), c);
        p.DrawRect(new Rect2(9, 0, 1, 1), c);
        p.DrawRect(new Rect2(6, 1, 4, 1), c.Darkened(0.3f));
    }

    void DrawBackGlow(Painter p)
    {
        if (Glow <= 0.01f) return;
        var center = new Vector2(0, -PixelSize * 2);
        for (int i = 6; i >= 1; i--)
        {
            float r = PixelSize * (3 + i * 2.6f * (0.6f + Glow * 0.7f));
            p.DrawCircle(center, r, GlowColor with { A = 0.05f * Glow });
        }
        // Bright band behind the seam: visible only through the gap as the lid lifts.
        if (!_open)
            p.DrawRect(new Rect2(-Width / 2 + PixelSize, -LidGap - 1, Width - 2 * PixelSize, LidGap + 2), GlowColor.Lightened(0.6f) with { A = Glow });
    }

    void DrawSeamLight(Painter p)
    {
        if (Glow <= 0.01f) return;
        var c = GlowColor.Lightened(0.4f);
        float y = _open ? -OpenRimRows * PixelSize + PixelSize : -LidGap * 0.5f;
        float half = Width / 2 - PixelSize;
        p.DrawRect(new Rect2(-half, y - PixelSize / 4f, half * 2, PixelSize / 2f), c with { A = 0.6f * Glow });

        const int shafts = 7;
        for (int i = 0; i < shafts; i++)
        {
            float x = -half * 0.85f + i * (half * 1.7f / (shafts - 1));
            float flicker = 0.6f + 0.4f * MathF.Sin(_time * 9 + i * 1.7f);
            float h = PixelSize * (2 + 9 * Glow * flicker);
            float w = PixelSize * 0.5f;
            var shaft = new[]
            {
                new Vector2(x - w, y), new Vector2(x + w, y),
                new Vector2(x + w * 2.5f, y - h), new Vector2(x - w * 2.5f, y - h),
            };
            var colors = new[] { c with { A = 0.25f * Glow }, c with { A = 0.25f * Glow }, c with { A = 0f }, c with { A = 0f } };
            p.DrawPolygon(shaft, colors);
        }
    }
}
