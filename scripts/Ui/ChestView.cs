using System;
using System.Collections.Generic;
using Godot;

namespace LootFall.Ui;

/// <summary>
/// The placeholder chest: body, lid and the light that leaks from the seam. All drawn in code.
/// Local origin is the chest's centre; the lid seam sits at y = -45.
/// </summary>
public partial class ChestView : Node2D
{
    public const float SeamY = -45f;

    public Color LockColor = Palette.LockUnknown;

    /// <summary>0..1 continuous jitter while the lid strains.</summary>
    public float Rumble;

    /// <summary>How far (px) the lid is lifted off the body.</summary>
    public float LidGap;

    /// <summary>0..1 intensity of the light leaking through the seam.</summary>
    public float Glow;

    public Color GlowColor = Colors.White;

    readonly Random _rng = new();
    Node2D _rig;
    Painter _backGlow, _body, _lid, _seamLight;
    float _time;
    float _wobble;
    bool _lidDetached;

    public override void _Ready()
    {
        _rig = new Node2D();
        AddChild(_rig);
        _backGlow = new Painter(DrawBackGlow) { Material = Fx.Additive() };
        _body = new Painter(DrawBody);
        _lid = new Painter(DrawLid) { Position = new Vector2(0, SeamY) };
        _seamLight = new Painter(DrawSeamLight) { Material = Fx.Additive() };
        _rig.AddChild(_backGlow);
        _rig.AddChild(_body);
        _rig.AddChild(_lid);
        _rig.AddChild(_seamLight);
    }

    /// <summary>A quick side-to-side rattle, e.g. when shaken.</summary>
    public void Wobble(float amount) => _wobble = Mathf.Max(_wobble, amount);

    /// <summary>Hands the lid over to the caller (to fling it off) and stops positioning it.</summary>
    public Node2D DetachLid()
    {
        _lidDetached = true;
        return _lid;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _wobble = Mathf.Max(0, _wobble - dt * 1.6f);

        float jitter = Rumble * Rumble * 5f;
        _rig.Position = new Vector2(Rand() * jitter, Rand() * jitter * 0.5f);
        _rig.Rotation = MathF.Sin(_time * 38f) * 0.1f * _wobble + Rand() * 0.015f * Rumble;
        if (!_lidDetached) _lid.Position = new Vector2(0, SeamY - LidGap);

        _backGlow.QueueRedraw();
        _body.QueueRedraw();
        _seamLight.QueueRedraw();
    }

    float Rand() => (float)(_rng.NextDouble() * 2 - 1);

    void DrawBody(Painter p)
    {
        p.DrawSetTransform(new Vector2(0, 84), 0, new Vector2(1, 0.2f));
        p.DrawCircle(Vector2.Zero, 140, new Color(0, 0, 0, 0.35f));
        p.DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        var body = new Rect2(-120, SeamY, 240, 120);
        p.DrawRect(body, Palette.Wood);
        for (int i = 1; i < 4; i++)
            p.DrawLine(new Vector2(-120, SeamY + i * 30), new Vector2(120, SeamY + i * 30), Palette.WoodDark, 3);
        p.DrawRect(new Rect2(-120, SeamY, 240, 7), Palette.WoodLight);

        foreach (float x in new[] { -88f, 72f })
        {
            p.DrawRect(new Rect2(x, SeamY, 16, 120), Palette.Metal);
            p.DrawRect(new Rect2(x + 2, SeamY, 3, 120), Palette.MetalLight with { A = 0.5f });
            for (int r = 0; r < 4; r++) p.DrawCircle(new Vector2(x + 8, SeamY + 14 + r * 30), 2.5f, Palette.MetalLight);
        }
        p.DrawRect(body, Palette.WoodDark, false, 4);

        var plate = new Rect2(-20, SeamY + 2, 40, 42);
        p.DrawRect(plate, LockColor);
        p.DrawRect(plate, LockColor.Darkened(0.55f), false, 3);
        p.DrawCircle(new Vector2(0, SeamY + 20), 6, new Color("#140f18"));
        p.DrawRect(new Rect2(-2.5f, SeamY + 22, 5, 12), new Color("#140f18"));
    }

    static float LidTop(float x) => -36f - 24f * MathF.Sin(MathF.PI * (x + 124f) / 248f);

    void DrawLid(Painter p)
    {
        var outline = new List<Vector2> { new(-124, 0) };
        for (int i = 0; i <= 20; i++)
        {
            float x = -124 + 248 * i / 20f;
            outline.Add(new Vector2(x, LidTop(x)));
        }
        outline.Add(new Vector2(124, 0));
        p.DrawColoredPolygon(outline.ToArray(), Palette.WoodLight);

        var plank = new List<Vector2>();
        for (int i = 0; i <= 20; i++)
        {
            float x = -120 + 240 * i / 20f;
            plank.Add(new Vector2(x, LidTop(x) * 0.5f));
        }
        p.DrawPolyline(plank.ToArray(), Palette.Wood, 3);

        foreach (float x in new[] { -88f, 72f })
        {
            var strap = new[] { new Vector2(x, 0), new Vector2(x, LidTop(x)), new Vector2(x + 16, LidTop(x + 16)), new Vector2(x + 16, 0) };
            p.DrawColoredPolygon(strap, Palette.Metal);
        }

        outline.Add(outline[0]);
        p.DrawPolyline(outline.ToArray(), Palette.WoodDark, 4);
        p.DrawRect(new Rect2(-11, -16, 22, 20), Palette.Metal);
        p.DrawRect(new Rect2(-11, -16, 22, 20), Palette.WoodDark, false, 2);
    }

    void DrawBackGlow(Painter p)
    {
        if (Glow <= 0.01f) return;
        for (int i = 14; i >= 1; i--)
        {
            float r = 50 + i * 17 * (0.6f + Glow * 0.7f);
            p.DrawCircle(new Vector2(0, SeamY), r, GlowColor with { A = 0.022f * Glow });
        }
        // Bright band behind the seam: visible only through the gap as the lid lifts.
        p.DrawRect(new Rect2(-118, SeamY - LidGap - 1, 236, LidGap + 3), GlowColor.Lightened(0.6f) with { A = Glow });
    }

    void DrawSeamLight(Painter p)
    {
        if (Glow <= 0.01f) return;
        var c = GlowColor.Lightened(0.4f);
        float y = SeamY - LidGap * 0.5f;
        p.DrawLine(new Vector2(-122, y), new Vector2(122, y), c with { A = 0.7f * Glow }, 2 + 4 * Glow);

        const int shafts = 7;
        for (int i = 0; i < shafts; i++)
        {
            float x = -100 + i * (200f / (shafts - 1));
            float flicker = 0.6f + 0.4f * MathF.Sin(_time * 9 + i * 1.7f);
            float h = 30 + 110 * Glow * flicker;
            var shaft = new[]
            {
                new Vector2(x - 6, y), new Vector2(x + 6, y),
                new Vector2(x + 16, y - h), new Vector2(x - 16, y - h),
            };
            var colors = new[] { c with { A = 0.22f * Glow }, c with { A = 0.22f * Glow }, c with { A = 0f }, c with { A = 0f } };
            p.DrawPolygon(shaft, colors);
        }
    }
}
