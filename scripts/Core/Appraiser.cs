using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// What a typical chest from a seller is like, from many simulated ones. Sellers use it to set prices. Never shown to
/// the player: they judge each chest from what they hear, weigh and see.
/// </summary>
public sealed class Appraiser
{
    const int PoolSize = 4000;
    static readonly Dictionary<string, Appraiser> Cache = new();

    public float MeanValue { get; }
    public float MeanCount { get; }
    public float MeanWeight { get; }

    public static Appraiser For(SellerDef seller)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(seller.Key, out var appraiser))
                Cache[seller.Key] = appraiser = new Appraiser(seller);
            return appraiser;
        }
    }

    Appraiser(SellerDef seller)
    {
        var rng = new Random(seller.Key.Sum(c => c) * 31);
        // A con artist prices as if it were the real thing: a noble's chest.
        var origin = seller.Rule == PriceRule.Con ? Origins.Manor : seller.Origin;
        var chests = Enumerable.Range(0, PoolSize).Select(_ => ChestGenerator.Generate(rng, origin, seller.Tier)).ToList();
        MeanValue = (float)chests.Average(c => c.Value);
        MeanCount = (float)chests.Average(c => c.Items.Count);
        MeanWeight = (float)chests.Average(c => c.ContentsWeight);
    }
}

/// <summary>What the player found out about one chest, tool by tool.</summary>
public sealed class Inspection
{
    /// <summary>Hears the small differences within a family (an ear trumpet, or a practiced ear).</summary>
    public bool Variants;

    /// <summary>Items seen through the keyhole.</summary>
    public HashSet<ChestItem> Seen = new();

    /// <summary>Compass reading (chest's total magic), if one was used.</summary>
    public int? Magic;

    /// <summary>Lodestone reading (pounds of iron and steel), if one was used.</summary>
    public float? Iron;

    /// <summary>Scale reading, if weighed.</summary>
    public float? Weight;

    /// <summary>Knock result, if knocked.</summary>
    public bool? Hollow;
}

/// <summary>
/// A reasonable player's guess at a chest's worth from what they've found out and what's in their ledger: every way
/// the sounds could be explained by things they know, weighted by how common those things are and by how well they
/// fit the scale, compass and lodestone. Used by the balance simulation and the autoplay bot, never shown in the game.
/// </summary>
public static class Estimator
{
    const int MaxCombinations = 6000;

    sealed record Option(int Value, float Weight, int Magic, float Iron, float Odds, float WeightSpread);

    /// <summary>Returns null when the clues give away a con.</summary>
    public static float? Estimate(Chest chest, SellerDef seller, Inspection seen, Ledger ledger)
    {
        var pool = ItemCatalog.UpTo(seller.Tier).ToArray();

        // Fakes: gold that a lodestone grabs with no metal heard, or rag bundles seen through the keyhole.
        if (seen.Iron is float iron && iron > 0.1f && !chest.Items.Any(i => i.Def.Family == Family.Metal)) return null;
        if (seen.Seen.Count > 0 && chest.Stones.Count > 0) return null;

        var options = chest.Items.Select(item => OptionsFor(item, pool, seen, ledger)).ToList();
        float hidden = seen.Hollow == true ? 80f : 0f;
        float hiddenWeight = seen.Hollow == true ? 1.5f : 0f;

        long combinations = options.Aggregate(1L, (n, o) => n * o.Length);
        if (combinations > MaxCombinations)
            return options.Sum(o => o.Sum(x => x.Value * x.Odds) / o.Sum(x => x.Odds)) + hidden;

        double total = 0, weightSum = 0, bestFit = 0;
        foreach (var combo in Combine(options, 0, new Option[options.Count]))
        {
            double odds = combo.Aggregate(1.0, (p, x) => p * x.Odds);
            if (seen.Magic is int magic && combo.Sum(x => x.Magic) != magic) continue;
            if (seen.Iron is float pull && MathF.Abs(combo.Sum(x => x.Iron) - pull) > 0.3f) continue;
            double fit = 1;
            if (seen.Weight is float w)
            {
                double spread = 0.3 + combo.Sum(x => x.WeightSpread) + (seen.Hollow == true ? 1 : 0);
                double off = w - combo.Sum(x => x.Weight) - hiddenWeight;
                fit = Math.Exp(-off * off / (2 * spread * spread));
                bestFit = Math.Max(bestFit, fit);
            }
            total += odds * fit * combo.Sum(x => x.Value);
            weightSum += odds * fit;
        }
        // Far more weight than anything you heard could explain, and it doesn't knock hollow: stones.
        if (seen.Weight.HasValue && bestFit < 0.01) return null;
        if (weightSum <= 0) return options.Sum(o => o.Sum(x => x.Value * x.Odds) / o.Sum(x => x.Odds)) + hidden;
        return (float)(total / weightSum) + hidden;
    }

    static Option[] OptionsFor(ChestItem item, ItemDef[] pool, Inspection seen, Ledger ledger)
    {
        if (seen.Seen.Contains(item))
            return new[] { Of(item.Def, 1f) };
        var known = pool.Where(d => d.Family == item.Def.Family && ledger.Knows(d)
                                    && (!seen.Variants || d.Variant == item.Def.Variant)).ToArray();
        if (known.Length > 0)
            return known.Select(d => Of(d, ItemCatalog.Commonness(d))).ToArray();
        // A sound you can't match to anything you know: guess it's an average thing of its family, give or take.
        var family = pool.Where(d => d.Family == item.Def.Family).ToArray();
        return new[]
        {
            new Option((int)family.Average(d => d.Value) * 7 / 10, family.Average(d => d.Weight), 0, 0f, 1f, 1f),
        };
    }

    static Option Of(ItemDef d, float odds) => new(d.Value, d.Weight, d.Magic, d.Magnetic ? d.Weight : 0f, odds, 0f);

    static IEnumerable<Option[]> Combine(List<Option[]> options, int index, Option[] current)
    {
        if (index == options.Count)
        {
            yield return current;
            yield break;
        }
        foreach (var option in options[index])
        {
            current[index] = option;
            foreach (var combo in Combine(options, index + 1, current)) yield return combo;
        }
    }
}
