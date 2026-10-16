using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public sealed record ChestItem(ItemDef Def)
{
    public int Value => Def.Value;
}

/// <summary>Something loose in a chest that makes noise when it's shaken.</summary>
public sealed record Rattler(ItemDef Item)
{
    public Signature Sound => Item.Sound;
    public float Weight => Item.Weight;
}

public sealed class Chest
{
    /// <summary>Every chest in town comes from the same cooper, so an empty one always weighs the same.</summary>
    public const float EmptyWeight = 6f;

    public Provenance Provenance { get; }
    public LockTier Lock { get; }
    public IReadOnlyList<ChestItem> Items { get; }

    /// <summary>A hidden compartment. Padded, so it never rattles, but its contents weigh and it knocks hollow.</summary>
    public ChestItem FalseBottom { get; }

    /// <summary>Stones a con artist wrapped in sacking so they wouldn't rattle: dead weight to make a chest feel full.</summary>
    public IReadOnlyList<float> Stones { get; }

    public bool Tampered => Stones.Count > 0;
    public bool HasFragile { get; }

    /// <summary>Raw value of the loose contents, before lockpicking modifiers. The false bottom is a bonus on top.</summary>
    public int Value { get; }

    /// <summary>What the shop scale reads. It's set so an empty chest reads zero.</summary>
    public float ContentsWeight { get; }

    public float Weight => EmptyWeight + ContentsWeight;

    public IEnumerable<Rattler> Rattlers => Items.Select(i => new Rattler(i.Def));

    public Chest(Provenance provenance, LockTier lockTier, IEnumerable<ChestItem> items, ChestItem falseBottom = null,
        IReadOnlyList<float> stones = null)
    {
        Provenance = provenance;
        Lock = lockTier;
        Items = items.ToList();
        FalseBottom = falseBottom;
        Stones = stones ?? Array.Empty<float>();
        Value = Items.Sum(i => i.Value);
        HasFragile = Items.Any(i => i.Def.Fragile);
        float contents = Items.Sum(i => i.Def.Weight) + (falseBottom?.Def.Weight ?? 0f) + Stones.Sum();
        ContentsWeight = MathF.Round(contents * 10f) / 10f;
    }

    /// <summary>Builds a chest with exactly these items (for the guided first day).</summary>
    public static Chest Of(string provenance, int lockIndex, string[] items, string falseBottom = null, float[] stones = null) =>
        new(Provenances.Get(provenance), LockTiers.All[lockIndex], items.Select(k => new ChestItem(ItemCatalog.Get(k))),
            falseBottom == null ? null : new ChestItem(ItemCatalog.Get(falseBottom)), stones);
}

public static class ChestGenerator
{
    public const float FalseBottomChance = 0.15f;

    /// <param name="wealth">Forces the hidden wealth roll (0..1) instead of rolling it.</param>
    public static Chest Generate(Random rng, Provenance provenance, float? wealth = null)
    {
        // A hidden "wealth" roll drives both the lock the owner bought and the rarity of what's inside.
        float w = wealth ?? Math.Clamp(MathF.Pow(rng.NextFloat(), 1.5f) + provenance.WealthBias * 0.25f, 0f, 1f);

        // Usually the lock matches the wealth, but sometimes a fancy lock guards junk (or the reverse).
        int lockIndex = rng.NextFloat() < 0.8f
            ? Math.Clamp((int)(w * 4f + rng.Between(-0.6f, 0.6f)), 0, 3)
            : rng.Next(4);

        // Never two things with the same sound in one chest: each sound you hear is exactly one item.
        int count = 2 + rng.Next(3);
        var items = new List<ChestItem>(count);
        var sounds = new HashSet<Signature>();
        for (int attempt = 0; items.Count < count && attempt < 40; attempt++)
        {
            var def = RollItem(rng, provenance, w, 0f, d => !sounds.Contains(d.Sound));
            if (def == null) continue;
            items.Add(new ChestItem(def));
            sounds.Add(def.Sound);
        }

        var falseBottom = rng.NextFloat() < FalseBottomChance ? RollItem(rng, provenance, w, 0.25f) : null;
        return new Chest(provenance, LockTiers.All[lockIndex], items, falseBottom == null ? null : new ChestItem(falseBottom));
    }

    /// <summary>
    /// A con artist's job. Stones wrapped in rags so they don't rattle: either a whole sack of them so the chest feels
    /// full, or (the clever version) just enough to make a copper purse weigh what a purse of gold would. The scale
    /// can't tell that apart; a look inside can.
    /// </summary>
    public static Chest Tamper(Random rng, Chest chest)
    {
        if (rng.NextFloat() < 0.5f)
        {
            var stones = Enumerable.Range(0, 1 + rng.Next(2)).Select(_ => MathF.Round(rng.Between(3f, 5f), 1)).ToList();
            return new Chest(chest.Provenance, chest.Lock, chest.Items, chest.FalseBottom, stones);
        }
        var copper = ItemCatalog.Get("copper_purse");
        var gold = ItemCatalog.Get("gold_purse");
        var items = chest.Items.Where(i => i.Def.Sound != copper.Sound).Take(2).Append(new ChestItem(copper));
        return new Chest(chest.Provenance, chest.Lock, items, chest.FalseBottom, new[] { gold.Weight - copper.Weight });
    }

    static ItemDef RollItem(Random rng, Provenance provenance, float wealth, float boost, Func<ItemDef, bool> allowed = null)
    {
        float roll = rng.NextFloat() * 0.32f + wealth * 0.68f + boost;
        var rarity = roll switch
        {
            < 0.33f => Rarity.Common,
            < 0.55f => Rarity.Uncommon,
            < 0.74f => Rarity.Rare,
            < 0.92f => Rarity.Epic,
            _ => Rarity.Legendary,
        };
        return ItemCatalog.Pick(rng, provenance.RollFamily(rng), rarity, allowed);
    }
}
