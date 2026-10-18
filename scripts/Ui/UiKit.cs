using System;
using Godot;

namespace LootFall.Ui;

/// <summary>Small helpers for building pixel-art UI in code with consistent styling.</summary>
public static class UiKit
{
    /// <summary>Screen pixels per UI art pixel. The 9-slice frames in assets/ui are pre-scaled by this.</summary>
    public const int Px = 3;

    /// <summary>9-slice margin: outline + bevel + one pixel of fill.</summary>
    const int Slice = 3 * Px;

    static Font _font;
    static Texture2D _panel, _frame, _button, _buttonPressed;

    public static Font Font => _font ??= GD.Load<Font>("res://assets/fonts/kenney_mini.ttf");
    static Texture2D PanelTex => _panel ??= GD.Load<Texture2D>("res://assets/ui/panel.png");
    static Texture2D FrameTex => _frame ??= GD.Load<Texture2D>("res://assets/ui/frame.png");
    static Texture2D ButtonTex => _button ??= GD.Load<Texture2D>("res://assets/ui/button.png");
    static Texture2D ButtonPressedTex => _buttonPressed ??= GD.Load<Texture2D>("res://assets/ui/button_pressed.png");

    /// <summary>Kenney Mini is drawn on an 8px grid; other sizes render with uneven pixels.</summary>
    public static int SnapFont(int size) => Math.Max(16, (int)MathF.Round(size / 8f) * 8);

    public static Label MakeLabel(string text, int size, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", SnapFont(size));
        label.AddThemeColorOverride("font_color", color);
        if (outline > 0)
        {
            label.AddThemeConstantOverride("outline_size", 4 * ((outline + 3) / 4));
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        }
        return label;
    }

    public static Label Wrapped(this Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    static StyleBoxTexture Slice9(Texture2D texture, Color modulate, float padding)
    {
        var box = new StyleBoxTexture { Texture = texture, ModulateColor = modulate };
        box.SetTextureMarginAll(Slice);
        box.SetContentMarginAll(padding);
        return box;
    }

    /// <summary>The dark beveled panel background.</summary>
    public static StyleBoxTexture PanelBox(float padding = 12) => Slice9(PanelTex, Colors.White, padding);

    /// <summary>A dark panel, optionally with a colored border.</summary>
    public static Panel MakePanel(Color? border = null)
    {
        var panel = new Panel();
        panel.AddThemeStyleboxOverride("panel", PanelBox());
        if (border is Color color) panel.AddChild(MakeFrame(color));
        return panel;
    }

    /// <summary>A colored 1-art-pixel border that fills its parent Control.</summary>
    public static NinePatchRect MakeFrame(Color color)
    {
        var frame = new NinePatchRect
        {
            Texture = FrameTex,
            Modulate = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            PatchMarginLeft = Slice, PatchMarginTop = Slice, PatchMarginRight = Slice, PatchMarginBottom = Slice,
        };
        frame.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return frame;
    }

    public static Button MakeButton(string text, Color accent, int size = 16, Action onPressed = null)
    {
        // FocusMode.None keeps Space free for lockpicking and holding the lid.
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", SnapFont(size));
        var face = accent.Darkened(0.15f);
        button.AddThemeStyleboxOverride("normal", Slice9(ButtonTex, face, 8));
        button.AddThemeStyleboxOverride("hover", Slice9(ButtonTex, face.Lightened(0.25f), 8));
        button.AddThemeStyleboxOverride("pressed", Slice9(ButtonPressedTex, face, 8));
        button.AddThemeStyleboxOverride("disabled", Slice9(ButtonTex, new Color("#3a3448"), 8));
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Palette.Text);
        button.AddThemeColorOverride("font_disabled_color", Palette.Muted.Darkened(0.2f));
        button.AddThemeConstantOverride("outline_size", 4);
        button.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        if (onPressed != null) button.Pressed += onPressed;
        return button;
    }

    public static T Place<T>(this T control, float x, float y, float width, float height) where T : Control
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
        return control;
    }

    /// <summary>A pixel sprite at an integer scale, centered on <paramref name="center"/>.</summary>
    public static Sprite2D MakeSprite(string path, Vector2 center, int scale)
    {
        return new Sprite2D
        {
            Texture = GD.Load<Texture2D>(path),
            Position = center,
            Scale = Vector2.One * scale,
        };
    }

    /// <summary>
    /// A thing's own sprite for a small slot, the same picture it had when it came out of the chest: its icon (the sprite,
    /// halved if it's big) at the largest integer scale that fits a <paramref name="box"/>-pixel square, centered on
    /// <paramref name="center"/> (or standing on it). Something enchanted glows behind it; fakes and broken things look dull.
    /// </summary>
    public static Node2D MakeItemIcon(Core.Item item, Vector2 center, float box, bool standing = false)
    {
        var texture = GD.Load<Texture2D>($"res://assets/sprites/items/icons/{item.Sprite}.png");
        int scale = Math.Max(1, (int)(box / Math.Max(texture.GetWidth(), texture.GetHeight())));
        var middle = standing ? new Vector2(0, -texture.GetHeight() * scale / 2f) : Vector2.Zero;
        var icon = new Node2D { Position = center };
        if (item.Magic && !item.Broken)
            icon.AddChild(new Painter(p =>
            {
                for (int i = 3; i >= 1; i--) p.DrawCircle(middle, box * 0.5f * i / 3f, Palette.Magic with { A = 0.16f });
            }));
        var sprite = new Sprite2D { Texture = texture, Scale = Vector2.One * scale, Position = middle };
        if (item.Fake || item.Broken) sprite.Modulate = new Color(0.6f, 0.6f, 0.65f);
        icon.AddChild(sprite);
        return icon;
    }

    /// <summary>
    /// How your gut rates a call: a strip running from red (a bad call) to green (a great one), with a marker that slides
    /// out from the middle to <paramref name="score"/> (0 to 1). The words at either end say what each end means.
    /// </summary>
    public static Control MakeGutMeter(float score, float width, string worst, string best)
    {
        const float h = 16, words = 120;
        var meter = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(width + 2 * words, 30) };
        meter.AddChild(MakeLabel(worst, 16, Palette.Bad, HorizontalAlignment.Right, outline: 4).Place(0, 0, words - 8, 20));
        meter.AddChild(MakeLabel(best, 16, Palette.Good, outline: 4).Place(words + width + 8, 0, words, 20));
        float at = 0.5f;
        var strip = new Painter(p =>
        {
            for (int x = 0; x < width; x += 4) p.DrawRect(new Rect2(x, 0, 4, h), Palette.Gradient(x / width).Darkened(0.2f));
            p.DrawRect(new Rect2(0, 0, width, h), Palette.PanelBorder, false, 3);
            // The marker: a bright notch with a little pointer above and below.
            float mx = Mathf.Round(width * at);
            var c = Palette.Gradient(at).Lightened(0.35f);
            p.DrawRect(new Rect2(mx - 3, -4, 6, h + 8), Colors.Black);
            p.DrawRect(new Rect2(mx - 1, -2, 2, h + 4), c);
        }) { Position = new Vector2(words, 2) };
        meter.AddChild(strip);
        meter.CreateTween().TweenMethod(Callable.From<float>(t =>
        {
            at = t;
            strip.QueueRedraw();
        }), 0.5f, Mathf.Clamp(score, 0f, 1f), 0.6f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(0.2f);
        return meter;
    }

    /// <summary>The 32px icon for a kind of thing (coins, metal...), centered on <paramref name="center"/>.</summary>
    public static Sprite2D MakeKindIcon(Core.Category category, Vector2 center) => MakeKindIcon(category.ToString().ToLowerInvariant(), center);

    /// <summary>A 32px row icon by name, e.g. "hidden" for a false bottom.</summary>
    public static Sprite2D MakeKindIcon(string name, Vector2 center) => MakeSprite($"res://assets/ui/kinds/{name}.png", center, 1);

    /// <summary>A chunky pixel tick (right) or cross (wrong), 24px at scale 1, centered on <paramref name="center"/>.</summary>
    public static Painter MakeCheck(bool right, Vector2 center, int scale)
    {
        var color = right ? Palette.Good : Palette.Bad;
        // Drawn on an 8x8 grid of 3px squares so it reads as pixel art next to the font.
        string[] rows = right
            ? new[] { "........", ".......#", "......##", "#....##.", "##..##..", ".####...", "..##....", "........" }
            : new[] { "##....##", ".##..##.", "..####..", "...##...", "..####..", ".##..##.", "##....##", "........" };
        return new Painter(p =>
        {
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                if (rows[y][x] != '#') continue;
                p.DrawRect(new Rect2(x * 3 - 13, y * 3 - 11, 5, 5), new Color(0, 0, 0, 0.8f));
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                if (rows[y][x] == '#') p.DrawRect(new Rect2(x * 3 - 12, y * 3 - 12, 3, 3), color);
        }) { Position = center, Scale = Vector2.One * scale };
    }

    /// <summary>A full-screen pixel-art backdrop (640x480 source at 2x), dimmed so UI and effects stand out.</summary>
    public static Node2D MakeBackdrop(string name, float brightness = 0.6f)
    {
        var root = new Node2D();
        root.AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>($"res://assets/backgrounds/{name}.png"),
            Position = new Vector2(640, 360),
            Scale = Vector2.One * 2,
            Modulate = new Color(brightness, brightness, brightness * 1.05f),
        });
        root.AddChild(new Painter(p =>
        {
            // Stepped vignette: hard bands read as pixel-art lighting rather than a smooth gradient.
            for (int i = 0; i < 5; i++)
            {
                float inset = i * 24;
                float a = 0.12f;
                p.DrawRect(new Rect2(-100, -100, 1480, 100 + inset), new Color(0, 0, 0, a));
                p.DrawRect(new Rect2(-100, 720 - inset, 1480, 100 + inset), new Color(0, 0, 0, a));
                p.DrawRect(new Rect2(-100, -100, 100 + inset, 920), new Color(0, 0, 0, a));
                p.DrawRect(new Rect2(1280 - inset, -100, 100 + inset, 920), new Color(0, 0, 0, a));
            }
        }));
        return root;
    }
}
