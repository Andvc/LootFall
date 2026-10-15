using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public sealed record ChestItem(ItemDef Def, int Value);

public sealed class Chest
{
    public Provenance Provenance { get; }
    public LockTier Lock { get; }
    public IReadOnlyList<ChestItem> Items { get; }

    /// <summary>A hidden compartment. Padded, so it never shows up when shaken, but it does add weight.</summary>
    public ChestItem FalseBottom { get; }

    public float Weight { get; }
    public Sound DominantSound { get; }
    public bool HasFragile { get; }
    public bool ManyItems { get; }

    /// <summary>Whether the loudest kind of item includes something Rare or better. Shaking hints at it.</summary>
    public bool SoundsFine { get; }

    /// <summary>Raw value of the visible contents, before lockpicking modifiers.</summary>
    public int Value { get; }

    public Chest(Provenance provenance, LockTier lockTier, List<ChestItem> items, ChestItem falseBottom)
    {
        Provenance = provenance;
        Lock = lockTier;
        Items = items;
        FalseBottom = falseBottom;
        Value = items.Sum(i => i.Value);
        HasFragile = items.Any(i => i.Def.Fragile);
        ManyItems = items.Count >= 4;
        DominantSound = items
            .GroupBy(i => i.Def.Sound)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Sum(i => i.Def.Weight))
            .First().Key;
        SoundsFine = items.Any(i => i.Def.Sound == DominantSound && i.Def.Rarity >= Rarity.Rare);

        float contents = items.Sum(i => i.Def.Weight) + (falseBottom?.Def.Weight ?? 0f);
        Weight = MathF.Round((ChestGenerator.EmptyWeight(lockTier) + contents) * 10f) / 10f;
    }
}

public static class ChestGenerator
{
    public const float FalseBottomChance = 0.12f;
    public const float EmptyWeightMin = 5.5f;
    public const float EmptyWeightMax = 7.3f;

    public static float EmptyWeight(LockTier lockTier) => EmptyWeightMin + 0.6f * lockTier.Index;

    public static Chest Generate(Random rng, Provenance provenance)
    {
        // A hidden "wealth" roll drives both the lock the owner bought and the rarity of what's inside.
        float wealth = Math.Clamp(MathF.Pow(rng.NextFloat(), 1.5f) + provenance.WealthBias * 0.25f, 0f, 1f);

        // Usually the lock matches the wealth, but sometimes a fancy lock guards junk (or the reverse).
        int lockIndex = rng.NextFloat() < 0.8f
            ? Math.Clamp((int)(wealth * 4f + rng.Between(-0.6f, 0.6f)), 0, 3)
            : rng.Next(4);

        int count = 2 + rng.Next(4);
        var items = new List<ChestItem>(count);
        for (int i = 0; i < count; i++)
            items.Add(RollItem(rng, provenance, wealth, 0f));

        var falseBottom = rng.NextFloat() < FalseBottomChance ? RollItem(rng, provenance, wealth, 0.25f) : null;
        return new Chest(provenance, LockTiers.All[lockIndex], items, falseBottom);
    }

    static ChestItem RollItem(Random rng, Provenance provenance, float wealth, float boost)
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
        var def = ItemCatalog.Pick(rng, provenance.RollSound(rng), rarity);
        int value = (int)MathF.Round(def.BaseValue * rng.Between(0.85f, 1.15f));
        return new ChestItem(def, Math.Max(1, value));
    }
}
