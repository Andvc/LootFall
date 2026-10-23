using Godot;
using LootFall.Core;

namespace LootFall.Ui;

public static class Palette
{
    public static readonly Color Bg = new("#110f18");
    public static readonly Color Panel = new("#1f1a2b");
    public static readonly Color PanelBorder = new("#403654");
    public static readonly Color Text = new("#ece6f5");
    public static readonly Color Muted = new("#9b92ad");
    public static readonly Color Dim = new("#6a6280");
    public static readonly Color Gold = new("#f7c64a");
    public static readonly Color Good = new("#6fdc8c");
    public static readonly Color Bad = new("#ff6b6b");
    public static readonly Color Warn = new("#ffa94d");
    public static readonly Color Heal = new("#7dffa0");
    public static readonly Color Crit = new("#ffe14d");
    public static readonly Color Thunder = new("#a8d8ff");
    public static readonly Color Burn = new("#ff9a3c");
    public static readonly Color Barrier = new("#7fc8ff");
    public static readonly Color Hurt = new("#ff5a5a");

    public static Color Of(Rarity rarity) => rarity switch
    {
        Rarity.Common => new Color("#c9c4d4"),
        Rarity.Uncommon => new Color("#62d46f"),
        Rarity.Rare => new Color("#4ea3ff"),
        Rarity.Epic => new Color("#b46bff"),
        Rarity.Legendary => new Color("#ffb52e"),
        _ => new Color("#ff4f6d"),
    };

    /// <summary>The colour the knight's blade and slash take for a weapon of this rarity (a dull grey with none).</summary>
    public static Color Blade(Rarity? rarity) => rarity switch
    {
        null => new Color("#9a9aa6"),
        Rarity.Common => new Color("#e4e8f0"),
        Rarity.Uncommon => new Color("#8bff9a"),
        Rarity.Rare => new Color("#7cc8ff"),
        Rarity.Epic => new Color("#d49bff"),
        Rarity.Legendary => new Color("#ffd35a"),
        _ => new Color("#ff7a8f"),
    };

    /// <summary>The colour of the knight's cape and plume for armour of this rarity (worn brown with none).</summary>
    public static Color Cape(Rarity? rarity) => rarity switch
    {
        null => new Color("#7a5a44"),
        Rarity.Common => new Color("#a08060"),
        Rarity.Uncommon => new Color("#3fa45a"),
        Rarity.Rare => new Color("#3a74d0"),
        Rarity.Epic => new Color("#8a46d8"),
        Rarity.Legendary => new Color("#e8962a"),
        _ => new Color("#d6283e"),
    };

    public static Color OfChest(ChestKind kind) => kind switch
    {
        ChestKind.Wooden => new Color("#c9945a"),
        ChestKind.Iron => new Color("#a8bdd6"),
        ChestKind.Golden => new Color("#ffd35a"),
        _ => new Color("#c48cff"),
    };
}
