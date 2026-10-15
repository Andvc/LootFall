using System;
using Godot;

namespace LootFall.Ui;

/// <summary>Small helpers for building UI in code with consistent styling.</summary>
public static class UiKit
{
    public static Label MakeLabel(string text, int size, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (outline > 0)
        {
            label.AddThemeConstantOverride("outline_size", outline);
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        }
        return label;
    }

    public static Label Wrapped(this Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int borderWidth = 2, int radius = 10, float padding = 10)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(padding);
        return box;
    }

    public static Panel MakePanel(Color bg, Color border, int borderWidth = 2, int radius = 12)
    {
        var panel = new Panel();
        panel.AddThemeStyleboxOverride("panel", Box(bg, border, borderWidth, radius));
        return panel;
    }

    public static Button MakeButton(string text, Color accent, int size = 20, Action onPressed = null)
    {
        // FocusMode.None keeps Space free for lockpicking and holding the lid.
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeStyleboxOverride("normal", Box(accent.Darkened(0.6f), accent, 2, 8));
        button.AddThemeStyleboxOverride("hover", Box(accent.Darkened(0.4f), accent.Lightened(0.25f), 2, 8));
        button.AddThemeStyleboxOverride("pressed", Box(accent.Darkened(0.7f), accent, 2, 8));
        button.AddThemeStyleboxOverride("disabled", Box(new Color("#1c1826"), new Color("#2e2840"), 2, 8));
        button.AddThemeColorOverride("font_color", Palette.Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Palette.Text);
        button.AddThemeColorOverride("font_disabled_color", Palette.Muted.Darkened(0.3f));
        if (onPressed != null) button.Pressed += onPressed;
        return button;
    }

    public static T Place<T>(this T control, float x, float y, float width, float height) where T : Control
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
        return control;
    }
}
