using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>Small helpers for building pixel-art UI in code with consistent styling.</summary>
public static class UiKit
{
    /// <summary>Screen pixels per UI art pixel. The 9-slice frames in assets/ui are pre-scaled by this.</summary>
    public const int Px = 3;

    /// <summary>9-slice margin: outline + bevel + one pixel of fill.</summary>
    const int Slice = 3 * Px;

    static Font _font;
    static Texture2D _panel, _frame, _button, _buttonPressed, _slot;

    public static Font Font => _font ??= GD.Load<Font>("res://assets/fonts/kenney_mini.ttf");
    static Texture2D PanelTex => _panel ??= GD.Load<Texture2D>("res://assets/ui/panel.png");
    static Texture2D FrameTex => _frame ??= GD.Load<Texture2D>("res://assets/ui/frame.png");
    static Texture2D ButtonTex => _button ??= GD.Load<Texture2D>("res://assets/ui/button.png");
    static Texture2D ButtonPressedTex => _buttonPressed ??= GD.Load<Texture2D>("res://assets/ui/button_pressed.png");
    public static Texture2D SlotTex => _slot ??= GD.Load<Texture2D>("res://assets/ui/slot.png");

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

    public static T Place<T>(this T control, float x, float y, float width, float height) where T : Control
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
        return control;
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

    /// <summary>A dark panel, optionally with a coloured border.</summary>
    public static Panel MakePanel(Color? border = null)
    {
        var panel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", PanelBox());
        if (border is Color color) panel.AddChild(MakeFrame(color));
        return panel;
    }

    /// <summary>A coloured 1-art-pixel border that fills its parent Control.</summary>
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

    /// <summary>A sunken square for a gear slot.</summary>
    public static NinePatchRect MakeSlot()
    {
        var slot = new NinePatchRect
        {
            Texture = SlotTex,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            PatchMarginLeft = Slice, PatchMarginTop = Slice, PatchMarginRight = Slice, PatchMarginBottom = Slice,
        };
        slot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return slot;
    }

    public static Button MakeButton(string text, Color accent, int size = 16, Action onPressed = null)
    {
        // Buttons never take focus: Space and Enter are the chest's (open, and take the recommended choice).
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        StyleButton(button, accent, size);
        button.Pressed += () => Main.Instance?.Sfx?.PlayGroup("click", 1f, -12);
        if (onPressed != null) button.Pressed += onPressed;
        return button;
    }

    public static void StyleButton(Button button, Color accent, int size = 16)
    {
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
    }

    public static Texture2D Icon(string name) => GD.Load<Texture2D>($"res://assets/ui/icons/{name}.png");

    /// <summary>A 32px UI icon (16px art at 2x), centred on <paramref name="center"/>.</summary>
    public static Sprite2D MakeIcon(string name, Vector2 center, float scale = 1) => new()
    {
        Texture = Icon(name), Position = center, Scale = Vector2.One * scale,
    };

    /// <summary>A 32px icon as a Control (for rows of labels).</summary>
    public static TextureRect MakeIconRect(string name, float x, float y, float size = 32) => new TextureRect
    {
        Texture = Icon(name), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore,
    }.Place(x, y, size, size);

    public static Texture2D ItemTexture(Item item) => GD.Load<Texture2D>($"res://assets/sprites/items/{item.Sprite}.png");

    /// <summary>
    /// An item's picture centred on <paramref name="center"/>, at the largest integer scale that fits a
    /// <paramref name="box"/>-pixel square (at least 1x, so a big sword may overhang its slot), with a soft glow of its
    /// rarity behind it from rare up.
    /// </summary>
    public static Node2D MakeItemIcon(Item item, Vector2 center, float box, bool glow = true)
    {
        var texture = ItemTexture(item);
        int scale = Math.Max(1, (int)(box / Math.Max(texture.GetWidth(), texture.GetHeight())));
        var root = new Node2D { Position = center };
        if (glow && item.Rarity >= Rarity.Rare)
        {
            var c = Palette.Of(item.Rarity);
            float r = box * 0.5f;
            root.AddChild(new Painter(p =>
            {
                for (int i = 3; i >= 1; i--) p.DrawCircle(Vector2.Zero, r * i / 3f, c with { A = 0.1f + 0.03f * (int)item.Rarity });
            }));
        }
        root.AddChild(new Sprite2D { Texture = texture, Scale = Vector2.One * scale });
        return root;
    }

    /// <summary>A bar that fills left to right, with a trailing "was" bar that catches up (health bars, timers).</summary>
    public static Bar MakeBar(float width, float height, Color fill, Color back) => new(width, height, fill, back);

    /// <summary>A full-width darkening of the screen behind a popup, which also swallows clicks.</summary>
    public static ColorRect MakeShade(float alpha = 0.55f) => new ColorRect
    {
        Color = new Color(0, 0, 0, alpha), MouseFilter = Control.MouseFilterEnum.Stop,
    }.Place(0, 0, 1280, 720);

    /// <summary>A stepped vignette: hard bands read as pixel-art lighting rather than a smooth gradient.</summary>
    public static Painter Vignette(float alpha, float width = 1280, float height = 720) => new(p =>
    {
        for (int i = 0; i < 4; i++)
        {
            float inset = i * 20;
            p.DrawRect(new Rect2(-100, -100, width + 200, 100 + inset), new Color(0, 0, 0, alpha));
            p.DrawRect(new Rect2(-100, -100, 100 + inset, height + 200), new Color(0, 0, 0, alpha));
            p.DrawRect(new Rect2(width - inset, -100, 100 + inset, height + 200), new Color(0, 0, 0, alpha));
        }
    });
}

/// <summary>A health or progress bar: a dark back, a bright fill, and a pale "lag" that slides down to it after a hit.</summary>
public partial class Bar : Node2D
{
    readonly float _w, _h;
    public Color Fill, Back, LagColor = new(1, 1, 1, 0.75f), Extra = Palette.Barrier;
    float _value = 1, _lag = 1, _extra;
    public float Value => _value;

    public Bar(float width, float height, Color fill, Color back)
    {
        _w = width;
        _h = height;
        Fill = fill;
        Back = back;
    }

    /// <summary>0 to 1. A drop leaves the lag behind for a moment; a rise moves straight up.</summary>
    public void Set(float value, bool instant = false)
    {
        value = Mathf.Clamp(value, 0, 1);
        if (instant || value > _value) _lag = Mathf.Max(_lag, value);
        if (instant) _lag = value;
        _value = value;
        QueueRedraw();
    }

    /// <summary>A second layer over the fill (the knight's barrier), as a share of the bar.</summary>
    public void SetExtra(float share)
    {
        _extra = Mathf.Clamp(share, 0, 1);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_lag > _value)
        {
            _lag = Mathf.Max(_value, _lag - (float)delta * Mathf.Max(0.35f, (_lag - _value) * 2.5f));
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-2, -2, _w + 4, _h + 4), Colors.Black);
        DrawRect(new Rect2(0, 0, _w, _h), Back);
        DrawRect(new Rect2(0, 0, Mathf.Round(_w * _lag), _h), LagColor);
        DrawRect(new Rect2(0, 0, Mathf.Round(_w * _value), _h), Fill);
        DrawRect(new Rect2(0, 0, Mathf.Round(_w * _value), Mathf.Max(1, _h / 3)), Fill.Lightened(0.3f));
        if (_extra > 0) DrawRect(new Rect2(0, 0, Mathf.Round(_w * _extra), _h), Extra with { A = 0.85f });
    }
}
