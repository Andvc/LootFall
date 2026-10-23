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
    public static readonly Color Magic = new("#c792ff");

    /// <summary>The words that pop out of a shaken chest.</summary>
    public static readonly Color Word = new("#fff4dc");

    public static readonly Color Wood = new("#7a4a24");
    public static readonly Color WoodDark = new("#4e2c12");
    public static readonly Color WoodLight = new("#93602f");
    public static readonly Color Metal = new("#4a4a58");
    public static readonly Color MetalLight = new("#8a8a9c");
    public static readonly Color LockUnknown = new("#77778a");

    /// <summary>A colour per shake strength, for the ledger's "how hard" column.</summary>
    public static Color Level(Shake level) => level switch
    {
        Shake.Gentle => new Color("#7fd3ff"),
        Shake.Firm => Gold,
        _ => Warn,
    };

    /// <summary>Lock plate colour by maker (see LockTiers); null for no lock.</summary>
    public static Color? Lock(LockTier lockTier) => lockTier.Index switch
    {
        0 => null,
        1 => new Color("#b9bac6"),
        2 => new Color("#5a8cff"),
        _ => new Color("#d08a3c"),
    };

    /// <summary>From red (0: a bad call) through gold to green (1: a great one), for odds and gut feelings.</summary>
    public static Color Gradient(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return t < 0.5f ? Bad.Lerp(Gold, t * 2) : Gold.Lerp(Good, t * 2 - 1);
    }

    public static Color Of(Rarity rarity) => rarity switch
    {
        Rarity.Common => new Color("#c9c4d4"),
        Rarity.Uncommon => new Color("#62d46f"),
        Rarity.Rare => new Color("#4ea3ff"),
        Rarity.Epic => new Color("#b46bff"),
        Rarity.Mythic => new Color("#ff4058"),
        _ => new Color("#ffb52e"),
    };
}
