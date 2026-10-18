using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum CurioRarity { Common, Uncommon, Rare }

/// <summary>
/// A curio: a trinket from Nim the peddler that bends one of the shop's rules for as long as you own it. You have room
/// for a few (more with a curio case), so which ones you keep shapes how you trade: what to buy, who to haggle with,
/// whether to save or spend. None of them tells you what's in a chest before you buy it.
/// </summary>
/// <param name="Icon">Icon under res://assets/ui/curios/.</param>
/// <param name="MinTier">Nim only offers it from this shop tier (where it does anything).</param>
public sealed record CurioDef(string Key, string Name, string Blurb, CurioRarity Rarity, string Icon, int MinTier = 1);

public static class Curios
{
    // How much each effect does, for the rules below and the blurbs.
    public const float ShellChance = 1 / 6f, KindBonus = 0.5f, SignetFloor = 0.85f, CrescentSales = 1.5f,
        CrescentDues = 1.25f, ReceiptDues = 0.8f, InterestRate = 0.05f, LetterBonus = 0.5f, CloverChance = 0.15f,
        HookHidden = 0.15f, DraughtMagic = 0.15f;

    public const int InterestPerTier = 25, CataloguePerTier = 10, BookPerPoint = 3;

    public static readonly CurioDef[] All =
    {
        new("coin", "Lucky Coin", "The first chest you open each day: everything in it gets two tries at its quality.", CurioRarity.Common, "coin"),
        new("shell", "Lucky Shell", "One chest in six you open: everything in it is a step better.", CurioRarity.Common, "shell"),
        new("loupe", "Jeweller's Loupe", "Coins and jewelry sell for half as much again.", CurioRarity.Common, "loupe"),
        new("gauntlet", "Smith's Glove", "Metal sells for half as much again.", CurioRarity.Common, "gauntlet"),
        new("cap", "Hatter's Cap", "Cloth and paper sell for half as much again.", CurioRarity.Common, "cap"),
        new("lantern", "Shop Lantern", "Open late: one more customer every day.", CurioRarity.Common, "lantern"),
        new("catalogue", "Collector's Catalogue", $"Every new find in your journal pays {CataloguePerTier}g per shop tier.",
            CurioRarity.Common, "catalogue"),

        new("signet", "Merchant's Signet", "Hagglers, and those who ask you to name a price, settle for less.", CurioRarity.Uncommon, "signet"),
        new("jar", "Savings Jar", $"Every night: a twentieth of your gold as interest (up to {InterestPerTier}g per shop tier).",
            CurioRarity.Uncommon, "jar"),
        new("letter", "Letter of Introduction", "Commissions pay half as much again.", CurioRarity.Uncommon, "letter"),
        new("knife", "Burglar's Knife", "Forcing a lock breaks nothing and scratches nothing.", CurioRarity.Uncommon, "knife", MinTier: 2),
        new("hook", "Smuggler's Hook", "Smugglers come your way: false bottoms turn up far more often.", CurioRarity.Uncommon, "hook", MinTier: 2),
        new("draught", "Witch's Draught", "Enchanted things turn up far more often.", CurioRarity.Uncommon, "draught", MinTier: 3),

        new("clover", "Four-Leaf Clover", "Chests come in a colour better more often.", CurioRarity.Rare, "clover"),
        new("receipt", "Forged Receipt", "The Guild's dues are a fifth less. Vane never checks.", CurioRarity.Rare, "receipt"),
        new("book", "Visitors' Book", $"Every night, visitors leave {BookPerPoint}g for each point of your exhibition.", CurioRarity.Rare, "book",
            MinTier: 2),
        new("crescent", "Cursed Crescent", "Everything sells for half as much again. The Guild's dues are a quarter more.",
            CurioRarity.Rare, "crescent"),
    };

    public static CurioDef Get(string key) => All.First(c => c.Key == key);

    /// <summary>Room for this many curios, more with a curio case.</summary>
    public const int Slots = 3;

    /// <summary>What Nim asks for one, this week. Prices climb with the stakes.</summary>
    public static int Price(CurioDef curio, int week)
    {
        int basePrice = curio.Rarity switch { CurioRarity.Common => 50, CurioRarity.Uncommon => 100, _ => 180 };
        return Money.Round(basePrice * (1 + 0.5f * Math.Max(0, week - 2)));
    }

    /// <summary>What Nim (or the Guild) gives you for one: half what it'd cost this week.</summary>
    public static int SellPrice(CurioDef curio, int week) => Math.Max(1, Price(curio, week) / 2);

    /// <summary>What Nim lays out on a visit: three curios you don't own, rarer ones more often as the weeks go by.</summary>
    public static List<CurioDef> Stock(Random rng, GameState state, int count = 3)
    {
        var pool = All.Where(c => c.MinTier <= state.Tier && !state.HasCurio(c.Key)).ToList();
        var stock = new List<CurioDef>();
        float rare = Math.Min(0.3f, 0.05f + 0.04f * state.Week), uncommon = 0.3f;
        while (stock.Count < count && pool.Count > 0)
        {
            var pick = rng.PickWeighted(pool.ToArray(), c => c.Rarity switch
            {
                CurioRarity.Rare => rare,
                CurioRarity.Uncommon => uncommon,
                _ => 1 - rare - uncommon,
            } / Math.Max(1, pool.Count(o => o.Rarity == c.Rarity)));
            stock.Add(pick);
            pool.Remove(pick);
        }
        return stock;
    }

    public static string RarityName(CurioRarity rarity) => rarity.ToString().ToLowerInvariant();
}

/// <summary>The peddler who sells curios.</summary>
public static class Peddler
{
    public const string Name = "Nim the Peddler";

    public static readonly string[] Greetings =
    {
        "Psst. Curios! Lucky, cursed, and everything in between.",
        "Back again! Nim's got just the thing. Several things, actually.",
        "Don't tell the Guild I was here. Now, what catches your eye?",
        "Fresh from a dead wizard's attic. Mostly fresh.",
    };

    public static readonly string[] Thanks = { "A fine choice. Mostly harmless.", "Pleasure doing business!", "It likes you. I can tell." };
    public static readonly string[] Bye = { "Next week, then. Same time, roughly.", "Off I go. Mind the Guild." };
}
