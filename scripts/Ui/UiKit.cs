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
