using Godot;
using LootFall.Core;

namespace LootFall.Ui;

public static class Palette
{
    public static readonly Color Bg = new("#15111d");
    public static readonly Color BgGlow = new("#2b2142");
    public static readonly Color Panel = new("#211b2d");
    public static readonly Color PanelBorder = new("#3b3150");
    public static readonly Color Text = new("#ece6f5");
    public static readonly Color Muted = new("#9b92ad");
    public static readonly Color Gold = new("#f2c14e");
    public static readonly Color Good = new("#6fdc8c");
    public static readonly Color Bad = new("#ff6b6b");
    public static readonly Color Warn = new("#ffa94d");

    public static readonly Color Wood = new("#7a4a24");
    public static readonly Color WoodDark = new("#4e2c12");
    public static readonly Color WoodLight = new("#93602f");
    public static readonly Color Metal = new("#4a4a58");
    public static readonly Color MetalLight = new("#8a8a9c");
    public static readonly Color LockUnknown = new("#77778a");

    public static Color Of(Rarity rarity) => rarity switch
    {
        Rarity.Common => new Color("#c9c4d4"),
        Rarity.Uncommon => new Color("#62d46f"),
        Rarity.Rare => new Color("#4ea3ff"),
        Rarity.Epic => new Color("#b46bff"),
        _ => new Color("#ffb52e"),
    };

    public static Color Lock(int tier) => tier switch
    {
        0 => new Color("#8a5a3a"),
        1 => new Color("#b9bac6"),
        2 => new Color("#d08a3c"),
        _ => new Color("#f5cf55"),
    };
}
