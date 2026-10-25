using System;
using Godot;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The art direction's tokens (docs/art-direction.md): the canvas and the lane, colours, the rarity ladder, fonts and
/// the type scale. Nothing on screen picks a colour, a font or a size anywhere else.
/// </summary>
public static class Look
{
    // ------------------------------------------------------------------------------------------------ the canvas

    public const float W = 1920, H = 1080;
    /// <summary>The lane: y 0 to <see cref="LaneH"/>, feet on <see cref="Ground"/>, the knight's feet at
    /// <see cref="HeroX"/>, monsters engaging at <see cref="FrontX"/> and <see cref="BackX"/>, walking in from
    /// <see cref="SpawnX"/>.</summary>
    public const float LaneH = 640, Ground = 575, HeroX = 480, FrontX = 760, BackX = 940, SpawnX = 2080;
    /// <summary>The knight's height on screen; monsters' sizes are relative to it.</summary>
    public const float HeroHeight = 280;

    // ------------------------------------------------------------------------------------------------ colours

    public static readonly Color Ink = new("#15101f");
    public static readonly Color Bg = new("#110f17");
    public static readonly Color Panel = new(0.090f, 0.075f, 0.122f, 0.94f);
    public static readonly Color PanelLight = new("#231c30");
    public static readonly Color Edge = new("#3a3150");
    public static readonly Color EdgeLight = new("#5d5079");
    public static readonly Color Text = new("#efe9f7");
    public static readonly Color Muted = new("#a59cb8");
    public static readonly Color Dim = new("#6d6484");
    public static readonly Color Gold = new("#ffd166");
    public static readonly Color Good = new("#6be38b");
    public static readonly Color Bad = new("#ff6b6b");
    /// <summary>Monster threats and damage taken: the red family, never used for the knight.</summary>
    public static readonly Color Threat = new("#ff5a3c");
    /// <summary>The knight's blows: cool white and pale gold.</summary>
    public static readonly Color Blow = new("#fff6dc");
    public static readonly Color Crit = new("#ffd23f");
    public static readonly Color Heal = new("#7dffa0");
    public static readonly Color Xp = new("#7ad7ff");
    public static readonly Color Shards = new("#8fd3ff");
    public static readonly Color Aether = new("#c9a2ff");
    public static readonly Color Stardust = new("#ffe08a");

    // ------------------------------------------------------------------------------------------------ the rarity ladder

    static readonly Color[] TierColors =
    {
        new("#c9c9c9"), new("#1eff00"), new("#0070dd"), new("#a335ee"), new("#ff8000"), new("#e8283c"),
        new("#ff4fd8"), new("#00ccff"), new("#e6cc80"), new("#8a5cff"), new("#5fe0ff"), new("#ffffff"),
    };

    public static int Tiers => TierColors.Length;

    /// <summary>A tier's colour, for frames, beams and backgrounds. Transcendent cycles through every hue.</summary>
    public static Color Tier(int r)
    {
        r = Math.Clamp(r, 0, TierColors.Length - 1);
        if (r == TierColors.Length - 1) return Color.FromHsv((float)(Time.GetTicksMsec() / 4000.0 % 1.0), 0.55f, 1f);
        return TierColors[r];
    }

    /// <summary>A tier's colour for text on the dark panels: lightened until it reads.</summary>
    public static Color TierText(int r)
    {
        var c = Tier(r);
        float lum = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        return lum < 0.5f ? c.Lerp(Colors.White, (0.5f - lum) * 0.9f) : c;
    }

    /// <summary>The four bands: A (Common to Rare), B (Epic, Legendary), C (Mythic to Celestial), D (Divine and up).</summary>
    public static int Band(int r) => r <= 2 ? 0 : r <= 4 ? 1 : r <= 7 ? 2 : 3;

    /// <summary>The face behind an item of this tier: dark for most, Primordial's dark violet, Eternal's silver.</summary>
    public static Color TierFace(int r) => r switch
    {
        9 => new Color("#140b1f"),
        10 => new Color("#d9dee9"),
        _ => Tier(r).Darkened(0.78f).Lerp(PanelLight, 0.35f),
    };

    public static string TierName(int r) => Content.RarityNames[Math.Clamp(r, 0, Content.RarityNames.Length - 1)];

    public static string Numeral(int r) => Roman(r + 1);

    public static string Roman(int n)
    {
        if (n <= 0) return "0";
        string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
        return n < 100 ? tens[n / 10] + ones[n % 10] : n.ToString();
    }

    // ------------------------------------------------------------------------------------------------ fonts

    /// <summary>The type scale at 1080p.</summary>
    public const int Small = 18, Body = 22, Sub = 28, Header = 36, Title = 48, Banner = 72;

    static Font _display, _ui, _uiBold, _strong, _numbers, _pop;

    /// <summary>Cinzel, bold: titles and headers.</summary>
    public static Font Display => _display ??= Variation("res://assets/fonts/Cinzel.ttf", weight: 700);
    /// <summary>Barlow Semi Condensed: UI text.</summary>
    public static Font Ui => _ui ??= GD.Load<FontFile>("res://assets/fonts/BarlowSemiCondensed-Medium.ttf");
    public static Font UiBold => _uiBold ??= GD.Load<FontFile>("res://assets/fonts/BarlowSemiCondensed-SemiBold.ttf");
    public static Font Strong => _strong ??= GD.Load<FontFile>("res://assets/fonts/BarlowSemiCondensed-Bold.ttf");
    /// <summary>Barlow Semi Condensed with tabular digits, so counters don't jitter as they change.</summary>
    public static Font Numbers => _numbers ??= Variation("res://assets/fonts/BarlowSemiCondensed-SemiBold.ttf", tabular: true);
    /// <summary>Lilita One: floating numbers and banners.</summary>
    public static Font Pop => _pop ??= GD.Load<FontFile>("res://assets/fonts/LilitaOne-Regular.ttf");

    static Font Variation(string path, int weight = 0, bool tabular = false)
    {
        var v = new FontVariation { BaseFont = GD.Load<FontFile>(path) };
        if (weight > 0) v.VariationOpentype = new Godot.Collections.Dictionary { { "wght", weight } };
        if (tabular) v.OpentypeFeatures = new Godot.Collections.Dictionary { { "tnum", 1 } };
        return v;
    }

    /// <summary>Lets go of the fonts (before quitting).</summary>
    public static void Release() => _display = _ui = _uiBold = _strong = _numbers = _pop = null;
}
