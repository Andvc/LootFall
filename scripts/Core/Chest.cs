using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public sealed record ChestItem(ItemDef Def, int Value);

/// <summary>Something loose in a chest that makes noise when it's shaken.</summary>
/// <param name="Sound">Sound group under res://assets/audio/.</param>
/// <param name="Word">Onomatopoeia shown where it hits (the visual twin of the sound).</param>
public sealed record Rattler(string Sound, string Word, float Weight);

public static class Rattle
{
    /// <summary>
    /// Each material sounds different, and fine (Rare+) items ring brighter than cheap ones. That's the whole
    /// vocabulary a player learns to read; there is no number behind it.
    /// </summary>
    public static Rattler For(ItemDef def)
    {
        bool fine = def.Rarity >= Rarity.Rare;
        var (sound, word) = def.Sound switch
        {
            Sound.Jingle => fine ? ("jingle_fine", "CHING") : ("jingle_cheap", "tik"),
            Sound.Clink => fine ? ("clink_fine", "TIIING") : ("clink_cheap", "clink"),
            Sound.Clank => fine ? ("clank_fine", "CLANG") : ("clank_cheap", "tonk"),
            Sound.Thud => fine ? ("thud_fine", "THUD") : ("thud_cheap", "bok"),
            _ => fine ? ("rustle_fine", "FWIP") : ("rustle_cheap", "flmp"),
        };
        return new Rattler(sound, word, def.Weight);
    }

    public static Rattler Rock(float weight) => new("rock", "krrk", weight);
}

public sealed class Chest
{
    public Provenance Provenance { get; }
    public LockTier Lock { get; }
    public IReadOnlyList<ChestItem> Items { get; }

    /// <summary>A hidden compartment. Padded, so it never rattles, but it adds weight and knocks hollow.</summary>
    public ChestItem FalseBottom { get; }

    /// <summary>Stones a con artist packed in to fake the weight. Worthless, and they grind when shaken.</summary>
    public IReadOnlyList<float> Rocks { get; }

    /// <summary>The lock as it looks from outside. A con artist's chest wears a painted copy of a fancy one.</summary>
    public LockTier ShownLock { get; }

    public bool Tampered => Rocks.Count > 0 || ShownLock != Lock;

    public float Weight { get; }
    public Sound DominantSound { get; }
    public bool HasFragile { get; }
    public bool ManyItems { get; }

    /// <summary>Whether the loudest kind of item includes something Rare or better.</summary>
    public bool SoundsFine { get; }

    /// <summary>Raw value of the visible contents, before lockpicking modifiers.</summary>
    public int Value { get; }

    public IEnumerable<Rattler> Rattlers => Items.Select(i => Rattle.For(i.Def)).Concat(Rocks.Select(Rattle.Rock));

    public Chest(Provenance provenance, LockTier lockTier, List<ChestItem> items, ChestItem falseBottom,
        IReadOnlyList<float> rocks = null, LockTier shownLock = null)
    {
        Provenance = provenance;
        Lock = lockTier;
        ShownLock = shownLock ?? lockTier;
        Items = items;
        FalseBottom = falseBottom;
        Rocks = rocks ?? Array.Empty<float>();
        Value = items.Sum(i => i.Value);
        HasFragile = items.Any(i => i.Def.Fragile);
        ManyItems = items.Count >= 4;
        DominantSound = items
            .GroupBy(i => i.Def.Sound)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Sum(i => i.Def.Weight))
            .First().Key;
        SoundsFine = items.Any(i => i.Def.Sound == DominantSound && i.Def.Rarity >= Rarity.Rare);

        float contents = items.Sum(i => i.Def.Weight) + (falseBottom?.Def.Weight ?? 0f) + Rocks.Sum();
        Weight = MathF.Round((ChestGenerator.EmptyWeight(lockTier) + contents) * 10f) / 10f;
    }
}

public static class ChestGenerator
{
    public const float FalseBottomChance = 0.12f;
    public const float EmptyWeightMin = 5.5f;
    public const float EmptyWeightMax = 7.3f;

    public static float EmptyWeight(LockTier lockTier) => EmptyWeightMin + 0.6f * lockTier.Index;

    /// <param name="wealth">Forces the hidden wealth roll (0..1) instead of rolling it.</param>
    public static Chest Generate(Random rng, Provenance provenance, float? wealth = null)
    {
        // A hidden "wealth" roll drives both the lock the owner bought and the rarity of what's inside.
        float w = wealth ?? Math.Clamp(MathF.Pow(rng.NextFloat(), 1.5f) + provenance.WealthBias * 0.25f, 0f, 1f);

        // Usually the lock matches the wealth, but sometimes a fancy lock guards junk (or the reverse).
        int lockIndex = rng.NextFloat() < 0.8f
            ? Math.Clamp((int)(w * 4f + rng.Between(-0.6f, 0.6f)), 0, 3)
            : rng.Next(4);

        int count = 2 + rng.Next(4);
        var items = new List<ChestItem>(count);
        for (int i = 0; i < count; i++)
            items.Add(RollItem(rng, provenance, w, 0f));

        var falseBottom = rng.NextFloat() < FalseBottomChance ? RollItem(rng, provenance, w, 0.25f) : null;
        return new Chest(provenance, LockTiers.All[lockIndex], items, falseBottom);
    }

    /// <summary>A con artist's job: pack in stones so it feels full, and fit a painted copy of a Royal Seal.</summary>
    public static Chest Tamper(Random rng, Chest chest)
    {
        var rocks = Enumerable.Range(0, 1 + rng.Next(3)).Select(_ => MathF.Round(rng.Between(2f, 4f), 1)).ToList();
        return new Chest(chest.Provenance, chest.Lock, chest.Items.ToList(), chest.FalseBottom, rocks, LockTiers.All[3]);
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
