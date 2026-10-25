using System;
using Godot;

namespace LootFall.View;

/// <summary>
/// Building blocks for screens built in code (absolute positions on the 1920x1080 canvas): labels in the type scale,
/// panels and buttons drawn by code (docs/art-direction.md, "UI"), bars and the screen shade. Buttons never take focus:
/// Space and Enter belong to the chest.
/// </summary>
public static class Ui
{
    public static Label Text(string text, int size, Color color, Font font = null, HorizontalAlignment align = HorizontalAlignment.Left,
        int outline = 0)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font ?? Look.Ui);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (outline > 0)
        {
            label.AddThemeConstantOverride("outline_size", outline);
            label.AddThemeColorOverride("font_outline_color", Look.Ink);
        }
        return label;
    }

    public static Label Wrapped(this Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    public static Label Centred(this Label label)
    {
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    public static T Place<T>(this T control, float x, float y, float width, float height) where T : Control
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
        return control;
    }

    public static RichTextLabel Rich(string bbcode, int size, Color color)
    {
        var r = new RichTextLabel
        {
            BbcodeEnabled = true, Text = bbcode, FitContent = true, ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        r.AddThemeFontOverride("normal_font", Look.Ui);
        r.AddThemeFontOverride("bold_font", Look.Strong);
        r.AddThemeFontSizeOverride("normal_font_size", size);
        r.AddThemeFontSizeOverride("bold_font_size", size);
        r.AddThemeColorOverride("default_color", color);
        return r;
    }

    // ------------------------------------------------------------------------------------------------ frames

    /// <summary>The panel frame: a dark face, a thin edge, rounded corners and a soft drop shadow.</summary>
    public static StyleBoxFlat Frame(Color? face = null, Color? edge = null, int radius = 10, int border = 2, int shadow = 8,
        float padding = 14)
    {
        var box = new StyleBoxFlat
        {
            BgColor = face ?? Look.Panel, BorderColor = edge ?? Look.Edge, ShadowColor = new Color(0, 0, 0, 0.45f),
            ShadowSize = shadow, AntiAliasing = true,
        };
        box.SetBorderWidthAll(border);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(padding);
        return box;
    }

    public static Panel Box(Color? face = null, Color? edge = null, int radius = 10)
    {
        var panel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Frame(face, edge, radius));
        return panel;
    }

    /// <summary>A button in the house style: a raised face in its accent, a lighter rim on hover, pressed darker.</summary>
    public static Button Button(string text, Action pressed = null, Color? accent = null, int size = Look.Body, Font font = null)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        Style(b, accent ?? Look.EdgeLight, size, font);
        b.Pressed += () => Main.Instance?.Sfx?.PlayGroup("click", 1f, -10);
        if (pressed != null) b.Pressed += pressed;
        return b;
    }

    public static void Style(Button b, Color accent, int size = Look.Body, Font font = null)
    {
        var face = accent.Darkened(0.35f);
        StyleBoxFlat S(Color bg, Color edge, int bottom)
        {
            var s = Frame(bg, edge, 9, 2, 4, 10);
            s.BorderWidthBottom = bottom;
            s.ContentMarginLeft = s.ContentMarginRight = 16;
            return s;
        }
        b.AddThemeStyleboxOverride("normal", S(face, accent, 4));
        b.AddThemeStyleboxOverride("hover", S(face.Lightened(0.12f), accent.Lightened(0.3f), 4));
        b.AddThemeStyleboxOverride("pressed", S(face.Darkened(0.2f), accent, 2));
        b.AddThemeStyleboxOverride("disabled", S(new Color("#2a2436"), Look.Edge, 4));
        b.AddThemeFontOverride("font", font ?? Look.UiBold);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", Look.Text);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeColorOverride("font_pressed_color", Look.Text);
        b.AddThemeColorOverride("font_disabled_color", Look.Dim);
        b.AddThemeConstantOverride("outline_size", 4);
        b.AddThemeColorOverride("font_outline_color", new Color(Look.Ink, 0.7f));
    }

    /// <summary>A full-screen darkening behind an overlay, which also takes the clicks.</summary>
    public static ColorRect Shade(float alpha = 0.6f) => new ColorRect
    {
        Color = new Color(0.03f, 0.02f, 0.05f, alpha), MouseFilter = Control.MouseFilterEnum.Stop,
    }.Place(0, 0, Look.W, Look.H);

    /// <summary>A soft vertical gradient (a top bar's backing, a panel's sheen).</summary>
    public static TextureRect Gradient(Color top, Color bottom, float x, float y, float w, float h)
    {
        var g = new Gradient();
        g.SetColor(0, top);
        g.SetColor(1, bottom);
        var tex = new GradientTexture2D { Gradient = g, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 64 };
        return new TextureRect { Texture = tex, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore }
            .Place(x, y, w, h);
    }
}

/// <summary>A Node2D whose drawing is supplied as a callback.</summary>
public partial class Painter : Node2D
{
    public Action<Painter> OnDraw;

    public Painter() { }

    public Painter(Action<Painter> onDraw) => OnDraw = onDraw;

    public override void _Draw() => OnDraw?.Invoke(this);
}

/// <summary>A Control whose drawing is supplied as a callback (for things that sit in the UI's layout and take input).</summary>
public partial class Canvas : Control
{
    public Action<Canvas> OnDraw;

    public Canvas() { }

    public Canvas(Action<Canvas> onDraw)
    {
        OnDraw = onDraw;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw() => OnDraw?.Invoke(this);
}

/// <summary>A bar that fills left to right, with a pale "lag" that slides down to a lower value after a hit.</summary>
public partial class Bar : Control
{
    public Color Fill, Back = new("#1a1524"), LagColor = new(1, 1, 1, 0.7f);
    public float Radius = 4;
    float _value = 1, _lag = 1;
    public float Value => _value;

    public Bar(Color fill)
    {
        Fill = fill;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Set(float value, bool instant = false)
    {
        value = Mathf.Clamp(value, 0, 1);
        if (instant || value > _value) _lag = Mathf.Max(_lag, value);
        if (instant) _lag = value;
        _value = value;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_lag > _value)
        {
            _lag = Mathf.Max(_value, _lag - (float)delta * Mathf.Max(0.4f, (_lag - _value) * 3f));
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Rounded(Look.Ink, Radius + 2), r.Grow(2));
        DrawStyleBox(Rounded(Back, Radius), r);
        if (_lag > _value) DrawStyleBox(Rounded(LagColor, Radius), new Rect2(0, 0, Size.X * _lag, Size.Y));
        if (_value > 0)
        {
            DrawStyleBox(Rounded(Fill, Radius), new Rect2(0, 0, Mathf.Max(Radius * 2, Size.X * _value), Size.Y));
            DrawStyleBox(Rounded(Fill.Lightened(0.35f), Radius), new Rect2(0, 0, Mathf.Max(Radius * 2, Size.X * _value), Mathf.Max(2, Size.Y * 0.35f)));
        }
    }

    static StyleBoxFlat Rounded(Color c, float radius)
    {
        var s = new StyleBoxFlat { BgColor = c, AntiAliasing = true };
        s.SetCornerRadiusAll((int)radius);
        return s;
    }
}
