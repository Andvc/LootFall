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

    public Origin Origin { get; }
    public LockTier Lock { get; }
    public IReadOnlyList<ChestItem> Items { get; }

    /// <summary>A hidden compartment. Padded, so it never rattles, but its contents weigh and it knocks hollow.</summary>
    public ChestItem FalseBottom { get; }

    /// <summary>Stones a con artist wrapped in rags so they wouldn't rattle: dead weight to make a chest feel full.</summary>
    public IReadOnlyList<float> Stones { get; }

    public bool Tampered => Stones.Count > 0 || Items.Any(i => i.Def.Fake);
    public bool HasFragile => Items.Any(i => i.Def.Fragile);

    /// <summary>Raw value of the loose contents, before lockpicking modifiers. The false bottom is a bonus on top.</summary>
    public int Value { get; }

    /// <summary>What the shop scale reads. It's set so an empty chest reads zero.</summary>
    public float ContentsWeight { get; }

    public float Weight => EmptyWeight + ContentsWeight;

    /// <summary>How hard a mana compass needle swings: 0 still, 1 a twitch, 2 or more a hard swing.</summary>
    public int Magic => Items.Sum(i => i.Def.Magic) + (FalseBottom?.Def.Magic ?? 0);

    /// <summary>How much iron and steel a lodestone feels, in pounds.</summary>
    public float IronWeight => Items.Where(i => i.Def.Magnetic).Sum(i => i.Def.Weight);

    public IEnumerable<Rattler> Rattlers => Items.Select(i => new Rattler(i.Def));

    public Chest(Origin origin, LockTier lockTier, IEnumerable<ChestItem> items, ChestItem falseBottom = null,
        IReadOnlyList<float> stones = null)
    {
        Origin = origin;
        Lock = lockTier;
        Items = items.ToList();
        FalseBottom = falseBottom;
        Stones = stones ?? Array.Empty<float>();
        Value = Items.Sum(i => i.Value);
        float contents = Items.Sum(i => i.Def.Weight) + (falseBottom?.Def.Weight ?? 0f) + Stones.Sum();
        ContentsWeight = MathF.Round(contents * 10f) / 10f;
    }

    /// <summary>Builds a chest with exactly these items (for scripted customers).</summary>
    public static Chest Of(Origin origin, int lockIndex, params string[] items) =>
        new(origin, LockTiers.All[lockIndex], items.Select(k => new ChestItem(ItemCatalog.Get(k))));
}

public static class ChestGenerator
{
    public const float FalseBottomChance = 0.15f;

    /// <param name="itemTier">Highest item tier this seller has access to.</param>
    /// <param name="falseBottoms">Whether hidden compartments turn up yet (they're a late-game surprise).</param>
    public static Chest Generate(Random rng, Origin origin, int itemTier, bool falseBottoms = false)
    {
        var pool = ItemCatalog.UpTo(itemTier).ToArray();
        int count = 2 + rng.Next(itemTier >= 3 ? 4 : 3);
        var items = new List<ChestItem>(count);
        for (int i = 0; i < count; i++)
            items.Add(new ChestItem(Roll(rng, origin, pool)));

        int value = items.Sum(i => i.Value);
        var falseBottom = falseBottoms && rng.NextFloat() < FalseBottomChance
            ? new ChestItem(rng.PickWeighted(pool.Where(d => d.Value >= 40).ToArray(), d => ItemCatalog.Commonness(d)))
            : null;
        return new Chest(origin, LockTiers.For(rng, itemTier, value), items, falseBottom);
    }

    static ItemDef Roll(Random rng, Origin origin, ItemDef[] pool)
    {
        var families = Families.All.Where(f => pool.Any(d => d.Family == f)).ToArray();
        var family = rng.PickWeighted(families, origin.Weight);
        return rng.PickWeighted(pool.Where(d => d.Family == family).ToArray(), ItemCatalog.Commonness);
    }

    /// <summary>
    /// A con artist's job. Either junk with a sack of rag-wrapped stones so it feels full, or iron slugs gilded to pass
    /// for gold doubloons: same jingle, same weight. A look inside shows the rags; a lodestone grabs the slugs.
    /// </summary>
    public static Chest Tamper(Random rng, Chest junk)
    {
        if (rng.NextFloat() < 0.5f)
        {
            var stones = Enumerable.Range(0, 1 + rng.Next(2)).Select(_ => MathF.Round(rng.Between(3f, 5f), 1)).ToList();
            return new Chest(Origins.Manor, junk.Lock, junk.Items, null, stones);
        }
        var slugs = new ChestItem(ItemCatalog.Get("gilded_slugs"));
        return new Chest(Origins.Manor, junk.Lock, junk.Items.Take(2).Append(slugs), null);
    }
}
