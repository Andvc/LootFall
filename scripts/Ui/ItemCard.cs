using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>A revealed item, with the sound it makes so the two stick together. Hover it to read the flavor text.</summary>
public partial class ItemCard : Panel
{
    public const float W = 180;
    public const float H = 236;
    const float IconWidth = 150, IconHeight = 100;

    public RevealedItem Item { get; }
    readonly bool _new;
    Label _value;

    public ItemCard() { }

    /// <param name="isNew">First time you've found one: it just went into the ledger.</param>
    public ItemCard(RevealedItem item, bool isNew = false)
    {
        Item = item;
        _new = isNew;
    }

    public override void _Ready()
    {
        Size = new Vector2(W, H);
        PivotOffset = Size / 2;
        TooltipText = Item.Def.Flavor;

        var rarityColor = Palette.Of(Item.Def.Rarity);
        var accent = Item.Shattered ? Palette.Bad : rarityColor;
        AddThemeStyleboxOverride("panel", UiKit.PanelBox());
        AddChild(UiKit.MakeFrame(accent));

        // Largest integer scale that fits, so pixels stay square.
        var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{Item.Def.Key}.png");
        int scale = Math.Max(1, (int)MathF.Min(IconWidth / texture.GetWidth(), IconHeight / texture.GetHeight()));
        var icon = new Sprite2D { Texture = texture, Position = new Vector2(W / 2, 62), Scale = Vector2.One * scale };
        if (Item.Shattered) icon.Modulate = new Color(0.45f, 0.4f, 0.5f);
        AddChild(icon);
        if (Item.Shattered)
        {
            AddChild(new Painter(p =>
            {
                p.DrawPolyline(new[] { new Vector2(-24, -24), new Vector2(-6, -6), new Vector2(-12, 6), new Vector2(18, 27) }, Palette.Bad, 6);
                p.DrawPolyline(new[] { new Vector2(24, -21), new Vector2(3, 0), new Vector2(12, 9) }, Palette.Bad, 3);
            }) { Position = new Vector2(W / 2, 62) });
        }

        string tag = Item.Shattered ? "SHATTERED" : Item.Def.Rarity.ToString().ToUpperInvariant();
        AddChild(UiKit.MakeLabel(tag, 16, accent, HorizontalAlignment.Center).Place(0, 116, W, 20));

        var name = UiKit.MakeLabel(Item.Def.Name, 16, Palette.Text, HorizontalAlignment.Center).Wrapped().Place(10, 136, W - 20, 44);
        name.VerticalAlignment = VerticalAlignment.Center;
        AddChild(name);

        // The sound it made in the chest, so the two stick together.
        string word = Families.Describe(Item.Def.Family, Item.Def.Variant);
        AddChild(UiKit.MakeLabel(_new ? $"NEW: {word}" : word, 16, _new ? Palette.Gold : Palette.Muted, HorizontalAlignment.Center)
            .Place(0, 180, W, 20));

        _value = UiKit.MakeLabel("0g", 24, Item.Shattered ? Palette.Bad : Palette.Gold, HorizontalAlignment.Center, outline: 4)
            .Place(0, 202, W, 28);
        AddChild(_value);

        if (Item.Pristine) AddRibbon("+25%", 9);

        if (Item.FromFalseBottom) AddRibbon("HIDDEN", W - 78);
    }

    void AddRibbon(string text, float x)
    {
        float width = text.Length * 11 + 12;
        AddChild(new ColorRect { Color = Palette.Gold, MouseFilter = MouseFilterEnum.Ignore }.Place(x, 9, width, 21));
        AddChild(UiKit.MakeLabel(text, 16, Palette.Bg, HorizontalAlignment.Center).Place(x, 9, width, 21));
    }

    public void CountUp(float duration)
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(v => _value.Text = $"{Mathf.RoundToInt(v)}g"), 0f, (float)Item.Value, duration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
    }
}
