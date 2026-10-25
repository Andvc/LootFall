using System;
using System.Collections.Generic;
using LootFall.Progression;

namespace LootFall.Game;

/// <summary>Where a chest came from: a depth's boss, a guardian (every tenth depth), the monsters of a farmed depth, or
/// the chest a new hero starts with.</summary>
public enum ChestKind : byte { Monster, Boss, Guardian, Starter }

/// <summary>
/// A chest: the drops of a depth's worth of kills (its waves and its boss), waiting to be opened. It holds the items
/// that could matter (at most <see cref="PerSlot"/> per slot, the best kept), how many items of each rarity dropped in
/// all (the rest are salvaged on opening), the materials they salvage for, and the first finds they made.
/// </summary>
public sealed class Chest
{
    public const int PerSlot = 4;

    public ChestKind Kind;
    public int Depth;
    public readonly List<(int Slot, Item Item)> Items = new();
    public readonly long[] Counts = new long[Content.RarityNames.Length];
    public double Materials;
    public readonly List<int> Found = new();

    public long ItemCount
    {
        get
        {
            long n = 0;
            foreach (var c in Counts) n += c;
            return n;
        }
    }

    /// <summary>The rarest item inside (-1 when empty): what the opening climbs to.</summary>
    public int BestRarity
    {
        get
        {
            for (int r = Counts.Length - 1; r >= 0; r--)
                if (Counts[r] > 0) return r;
            return -1;
        }
    }

    public bool IsEmpty => Items.Count == 0 && Materials <= 0;

    /// <summary>Adds an item that could matter; past <see cref="PerSlot"/> for its slot, it replaces the worst kept one
    /// if it's better (rarer, then a better roll, then a higher level).</summary>
    public void Add(int slot, Item item)
    {
        int kept = 0, worst = -1;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Slot != slot) continue;
            kept++;
            if (worst < 0 || Better(Items[worst].Item, Items[i].Item)) worst = i;
        }
        if (kept < PerSlot)
        {
            var copy = new Item();
            copy.CopyFrom(item);
            Items.Add((slot, copy));
        }
        else if (Better(item, Items[worst].Item)) Items[worst].Item.CopyFrom(item);
    }

    static bool Better(Item a, Item b) =>
        a.Rarity != b.Rarity ? a.Rarity > b.Rarity : a.Quality != b.Quality ? a.Quality > b.Quality : a.Ilvl > b.Ilvl;

    /// <summary>Takes in a batch of drops (counts, materials and first finds; the candidates as items).</summary>
    public void Absorb(LootRoll roll)
    {
        int rarities = Content.RarityNames.Length;
        foreach (int s in roll.Slots)
        {
            for (int k = 0; k < roll.CandidateCount[s]; k++) Add(s, roll.Candidate(s, k));
            for (int r = 0; r < rarities; r++) Counts[r] += roll.Counts[s * rarities + r];
        }
        Materials += roll.Materials;
        Found.AddRange(roll.Found);
    }

    /// <summary>Takes in another chest (when the pile is full, new drops go into the last chest).</summary>
    public void Absorb(Chest other)
    {
        foreach (var (s, it) in other.Items) Add(s, it);
        for (int r = 0; r < Counts.Length; r++) Counts[r] += other.Counts[r];
        Materials += other.Materials;
        Found.AddRange(other.Found);
        if (other.Kind > Kind && other.Kind != ChestKind.Starter) Kind = other.Kind;
        Depth = Math.Max(Depth, other.Depth);
    }
}

/// <summary>
/// The chests waiting to be opened, and the one being filled. Drops go into the filling chest as monsters die; it's
/// sealed when a depth's boss falls (or, while farming, after a depth's worth of kills) and joins the pile. The pile
/// holds at most <see cref="Cap"/> chests; past that, sealed chests are poured into the last one, so nothing is lost.
/// </summary>
public sealed class ChestPile : ILootTaker
{
    public const int Cap = 100;

    public readonly List<Chest> Waiting = new();
    Chest _filling = new();
    /// <summary>Kills (a boss counts as Params.BossReward) since the filling chest was started.</summary>
    public double Kills;

    public bool DiscoverNow => false;

    public bool Take(LootRoll roll)
    {
        _filling.Absorb(roll);
        return false;
    }

    public Chest Filling => _filling;

    /// <summary>Seals the filling chest (if anything is in it) and adds it to the pile; returns it, or null if empty.</summary>
    public Chest Seal(ChestKind kind, int depth)
    {
        Kills = 0;
        if (_filling.IsEmpty) return null;
        var chest = _filling;
        chest.Kind = kind;
        chest.Depth = depth;
        _filling = new Chest();
        Add(chest);
        return chest;
    }

    public void Add(Chest chest)
    {
        if (Waiting.Count < Cap) Waiting.Add(chest);
        else Waiting[^1].Absorb(chest);
    }

    /// <summary>The next chest to open: the best kind first (a guardian's before a boss's), oldest first among them.</summary>
    public Chest Next()
    {
        Chest best = null;
        foreach (var c in Waiting)
            if (best == null || Rank(c) > Rank(best)) best = c;
        return best;
    }

    static int Rank(Chest c) => c.Kind switch { ChestKind.Starter => 3, ChestKind.Guardian => 2, ChestKind.Boss => 1, _ => 0 };

    /// <summary>A new hero's first chest: one common item for each open slot.</summary>
    public static Chest Starter(Hero h, Rng rng)
    {
        var chest = new Chest { Kind = ChestKind.Starter, Depth = 1 };
        var item = new Item();
        for (int s = 0; s < h.Gear.Length; s++)
        {
            if (!h.Open[s] || h.Gear[s] != null) continue;
            h.Roll(rng, s, 1, 0, 0.5 + 0.4 * rng.NextDouble(), item);
            chest.Add(s, item);
            chest.Counts[0]++;
            chest.Found.Add(h.CodexKey(Hero.Era(1), s, 0, Hero.Band(item.Quality)));
        }
        return chest;
    }
}

/// <summary>One item shown when a chest opens.</summary>
public sealed class Card
{
    public int Slot;
    public Item Item;
    /// <summary>Whether it was kept (the best found for its piece now), what it replaced (null for none) and its score
    /// (reach by the weights); whether it's worn once the best set is chosen.</summary>
    public bool Kept, Equipped;
    public Item Replaced;
    public double Score;
    /// <summary>Whether it was a first find for the codex.</summary>
    public bool New;
}

/// <summary>What opening a chest did, for the reveal: the items in it (rarest last), what went on, the first finds,
/// the materials, and how deep the hero can push before and after.</summary>
public sealed class Opening
{
    public Chest Chest;
    public readonly List<Card> Cards = new();
    public int NewEntries;
    public long Salvaged;
    public double Materials;
    public double ReachBefore, ReachAfter;
    public int Kept, Equipped;
}
