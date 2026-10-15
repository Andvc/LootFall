using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>A revealed item. Hover it to read the flavor text.</summary>
public partial class ItemCard : Panel
{
    public const float W = 170;
    public const float H = 164;

    public RevealedItem Item { get; }
    Label _value;

    public ItemCard() { }

    public ItemCard(RevealedItem item)
    {
        Item = item;
    }

    public override void _Ready()
    {
        Size = new Vector2(W, H);
        PivotOffset = Size / 2;
        TooltipText = Item.Def.Flavor;

        var rarityColor = Palette.Of(Item.Def.Rarity);
        var accent = Item.Shattered ? Palette.Bad : rarityColor;
        AddThemeStyleboxOverride("panel", UiKit.Box(Palette.Panel, accent, 3, 10));

        var iconColor = Item.Shattered ? Palette.Muted.Darkened(0.2f) : rarityColor;
        AddChild(new Painter(p => ItemIcons.Draw(p, Item.Def.Sound, iconColor)) { Position = new Vector2(W / 2, 34) });
        if (Item.Shattered)
        {
            AddChild(new Painter(p =>
            {
                p.DrawPolyline(new[] { new Vector2(-22, -20), new Vector2(-4, -4), new Vector2(-10, 6), new Vector2(18, 22) }, Palette.Bad, 3);
                p.DrawPolyline(new[] { new Vector2(20, -18), new Vector2(4, 0), new Vector2(12, 8) }, Palette.Bad, 2);
            }) { Position = new Vector2(W / 2, 34) });
        }

        string tag = Item.Shattered ? "SHATTERED"
            : Item.Def.Rarity.ToString().ToUpperInvariant() + (Item.Pristine ? " · PRISTINE" : "");
        AddChild(UiKit.MakeLabel(tag, 12, accent, HorizontalAlignment.Center).Place(0, 58, W, 18));

        var name = UiKit.MakeLabel(Item.Def.Name, 14, Palette.Text, HorizontalAlignment.Center).Wrapped().Place(6, 76, W - 12, 46);
        name.VerticalAlignment = VerticalAlignment.Center;
        AddChild(name);

        _value = UiKit.MakeLabel("0g", 20, Item.Shattered ? Palette.Bad : Palette.Gold, HorizontalAlignment.Center).Place(0, 128, W, 26);
        AddChild(_value);

        if (Item.FromFalseBottom)
        {
            var ribbon = UiKit.MakeLabel("HIDDEN", 11, Palette.Bg, HorizontalAlignment.Center).Place(W - 64, 6, 58, 16);
            var bg = UiKit.MakePanel(Palette.Gold, Palette.Gold, 0, 4).Place(W - 64, 6, 58, 16);
            bg.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(bg);
            AddChild(ribbon);
        }
    }

    public void CountUp(float duration)
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(v => _value.Text = $"{Mathf.RoundToInt(v)}g"), 0f, (float)Item.Value, duration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
    }
}
