using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LootFall.Progression;

namespace LootFall.Game;

/// <summary>
/// A saved game as plain data: the world (time, the run, aether owed), the hero (gear, forge, ranks, mastery, codex,
/// level, gold, materials), talents, both prestige layers, the chests, the automation and the totals. What the rules
/// can work out again (open slots, rarity tiers, the codex's worth, the stat sheets) isn't stored. Everything kept per
/// piece is matched up by the pieces' names (<see cref="SlotNames"/>), so the equipment can change between versions:
/// a piece added starts empty and one taken away is dropped. Bump <see cref="CurrentVersion"/> only when an older save
/// can't be read as it is.
/// </summary>
public sealed class SaveData
{
    /// <summary>3: pieces saved by name (2: the equipment anatomy replaced the 149 fixed slots).</summary>
    public const int CurrentVersion = 3;

    public int Version = CurrentVersion;
    /// <summary>When it was written (Unix seconds), to work out the time away.</summary>
    public long SavedAt;
    public int Seed;

    public double Time, DepthProgress, RunStart, CycleStart, LastRun;
    /// <summary>The depths the push under way clears at once (0 when none is under way).</summary>
    public int Skip;
    public double Accum, NextChores, NextAutopilot, NextFocus;
    public int LastPeak;
    public List<double[]> RunDepths = new();
    public BigData PendingAether;
    public ulong Rng;

    public int RunDepth, EverDepth, Level;
    public BigData Xp, Gold;
    public double Materials;
    public ItemData[] Gear;
    public int[] Forge, Rank, Mastery;
    public double[] MasteryXp;
    public bool[] Focus;
    /// <summary>What's put on, by piece.</summary>
    public bool[] Worn;
    /// <summary>The pieces' names in the order everything per piece above (and the codex, and the chests) was saved in.</summary>
    public string[] SlotNames;
    public string Codex;
    /// <summary>The codex's worth as summed when found (summing it again in another order differs in the last digits).</summary>
    public double CodexValue;

    public string Talents;
    public int Points, ParagonDamage, ParagonHp;

    public LayerData Ascension, Transcendence;

    public List<ChestData> Chests = new();
    public ChestData Filling;
    public double FillingKills;

    public Automation Auto;
    public TallyData Totals;
}

public struct BigData
{
    public double M;
    public long E;

    public static BigData Of(Big b) => new() { M = b.Mantissa, E = b.Exponent };
    public Big ToBig() => Big.FromParts(M, E);
}

public sealed class ItemData
{
    public double Ilvl, Quality;
    public int Rarity;
    public int[] Stats;
    public double[] Rolls;

    public static ItemData Of(Item it) => it == null ? null : new ItemData
    {
        Ilvl = it.Ilvl, Quality = it.Quality, Rarity = it.Rarity,
        Stats = it.Stats.Take(it.Affixes).Select(s => (int)s).ToArray(),
        Rolls = it.Rolls.Take(it.Affixes).ToArray(),
    };

    public Item ToItem()
    {
        var it = new Item { Ilvl = Ilvl, Quality = Quality, Rarity = Rarity, Affixes = Math.Min(Item.MaxAffixes, Stats?.Length ?? 0) };
        for (int i = 0; i < it.Affixes; i++)
        {
            it.Stats[i] = (Stat)Stats[i];
            it.Rolls[i] = Rolls[i];
        }
        return it;
    }
}

public sealed class LayerData
{
    public int[] Ranks;
    public BigData Currency, CycleEarned, Lifetime;
    public int Resets;

    public static LayerData Of(LayerState l) => new()
    {
        Ranks = (int[])l.Ranks.Clone(), Currency = BigData.Of(l.Currency), CycleEarned = BigData.Of(l.CycleEarned),
        Lifetime = BigData.Of(l.Lifetime), Resets = l.Resets,
    };

    public void Into(LayerState l, Scale sc)
    {
        Array.Clear(l.Ranks);
        for (int i = 0; i < l.Ranks.Length && i < (Ranks?.Length ?? 0); i++) l.Ranks[i] = Ranks[i];
        l.Currency = Currency.ToBig();
        l.CycleEarned = CycleEarned.ToBig();
        l.Lifetime = Lifetime.ToBig();
        l.Resets = Resets;
        l.Rebuild(sc);
    }
}

public sealed class ChestData
{
    public int Kind, Depth;
    public int[] Slots;
    public ItemData[] Items;
    public long[] Counts;
    public double Materials;
    public int[] Found;

    public static ChestData Of(Chest c) => new()
    {
        Kind = (int)c.Kind, Depth = c.Depth,
        Slots = c.Items.Select(x => x.Slot).ToArray(), Items = c.Items.Select(x => ItemData.Of(x.Item)).ToArray(),
        Counts = (long[])c.Counts.Clone(), Materials = c.Materials, Found = c.Found.ToArray(),
    };

    /// <summary>The chest, its pieces and codex entries renumbered for this build (<paramref name="map"/>: a saved piece's
    /// index now, or -1 for one that's gone, whose items are dropped).</summary>
    public Chest ToChest(int[] map, Func<int, int> codexKey)
    {
        var c = new Chest { Kind = (ChestKind)Kind, Depth = Depth, Materials = Materials };
        for (int i = 0; i < (Slots?.Length ?? 0); i++)
            if (Slots[i] >= 0 && Slots[i] < map.Length && map[Slots[i]] >= 0) c.Items.Add((map[Slots[i]], Items[i].ToItem()));
        for (int r = 0; r < c.Counts.Length && r < (Counts?.Length ?? 0); r++) c.Counts[r] = Counts[r];
        if (Found != null)
            foreach (int k in Found)
                if (codexKey(k) is var key && key >= 0) c.Found.Add(key);
        return c;
    }
}

public sealed class TallyData
{
    public double Kills;
    public BigData Gold;
    public long Chests, Opened, Upgrades, NewEntries, Ascensions, Transcendences;
    public int Record;
    public long[] Found;
}

public static class SaveGame
{
    static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    public static SaveData Capture(Session s, int seed, long now)
    {
        var h = s.H;
        var w = s.W;
        return new SaveData
        {
            SavedAt = now, Seed = seed,
            Time = w.Time, Accum = s.Accum, NextChores = s.NextChores, NextAutopilot = s.NextAutopilot, NextFocus = s.NextFocus,
            DepthProgress = w.DepthProgress, Skip = w.Skip, RunStart = w.RunStart, CycleStart = w.CycleStart, LastRun = w.LastRun,
            LastPeak = w.LastPeak, RunDepths = w.RunDepths.Select(x => new[] { x.Time, x.Depth }).ToList(),
            PendingAether = BigData.Of(w.PendingAether), Rng = w.Rng.State,
            RunDepth = h.RunDepth, EverDepth = h.EverDepth, Level = h.Level, Xp = BigData.Of(h.Xp), Gold = BigData.Of(h.Gold),
            Materials = h.Materials, Gear = h.Gear.Select(ItemData.Of).ToArray(),
            Forge = (int[])h.Forge.Clone(), Rank = (int[])h.Rank.Clone(), Mastery = (int[])h.Mastery.Clone(),
            MasteryXp = (double[])h.MasteryXp.Clone(), Focus = (bool[])h.Focus.Clone(), Worn = (bool[])h.Worn.Clone(), Codex = Bits(h.Codex), CodexValue = h.CodexValue,
            SlotNames = s.C.Slots.Select(x => x.Name).ToArray(),
            Talents = Bits(h.Talents.Allocated), Points = h.Talents.Points, ParagonDamage = h.Talents.ParagonDamage, ParagonHp = h.Talents.ParagonHp,
            Ascension = LayerData.Of(h.Ascension), Transcendence = LayerData.Of(h.Transcendence),
            Chests = s.Pile.Waiting.Select(ChestData.Of).ToList(), Filling = ChestData.Of(s.Pile.Filling), FillingKills = s.Pile.Kills,
            Auto = s.Auto,
            Totals = new TallyData
            {
                Kills = s.Totals.Kills, Gold = BigData.Of(s.Totals.Gold), Chests = s.Totals.Chests, Opened = s.Totals.Opened,
                Upgrades = s.Totals.Upgrades, NewEntries = s.Totals.NewEntries, Ascensions = s.Totals.Ascensions,
                Transcendences = s.Totals.Transcendences, Record = s.Totals.Record, Found = (long[])s.Totals.Found.Clone(),
            },
        };
    }

    public static string ToJson(SaveData d) => JsonSerializer.Serialize(d, Options);

    public static SaveData FromJson(string json) => JsonSerializer.Deserialize<SaveData>(json, Options);

    /// <summary>Rebuilds a session from saved data.</summary>
    public static Session Restore(SaveData d)
    {
        var s = new Session(d.Seed);
        var h = s.H;
        var w = s.W;
        w.Time = d.Time;
        s.Accum = d.Accum;
        s.NextChores = d.NextChores;
        s.NextAutopilot = d.NextAutopilot;
        s.NextFocus = d.NextFocus;
        w.DepthProgress = d.DepthProgress;
        w.Skip = d.Skip;
        w.RunStart = d.RunStart;
        w.CycleStart = d.CycleStart;
        w.LastRun = d.LastRun;
        w.LastPeak = d.LastPeak;
        w.RunDepths.Clear();
        if (d.RunDepths != null) foreach (var x in d.RunDepths) w.RunDepths.Add((x[0], (int)x[1]));
        w.PendingAether = d.PendingAether.ToBig();
        if (d.Rng != 0) w.Rng.State = d.Rng;

        // The layers first: they decide which slots and tiers are open.
        d.Ascension?.Into(h.Ascension, h.S);
        d.Transcendence?.Into(h.Transcendence, h.S);
        h.RunDepth = Math.Max(1, d.RunDepth);
        h.EverDepth = Math.Max(h.RunDepth, d.EverDepth);
        h.Level = Math.Max(1, d.Level);
        h.Xp = d.Xp.ToBig();
        h.Gold = d.Gold.ToBig();
        h.Materials = d.Materials;
        h.UpdateUnlocks();
        // Saved pieces by name: map[saved index] is the piece's index now, or -1 for one that's gone.
        var map = SlotMap(d, s.C);
        for (int i = 0; i < map.Length; i++)
        {
            int j = map[i];
            if (j < 0) continue;
            h.Gear[j] = d.Gear != null && i < d.Gear.Length ? d.Gear[i]?.ToItem() : null;
            h.Forge[j] = Get(d.Forge, i);
            h.Rank[j] = Get(d.Rank, i);
            h.Mastery[j] = Get(d.Mastery, i);
            h.MasteryXp[j] = d.MasteryXp != null && i < d.MasteryXp.Length ? d.MasteryXp[i] : 0;
            h.Focus[j] = d.Focus != null && i < d.Focus.Length && d.Focus[i];
            h.Worn[j] = d.Worn != null && i < d.Worn.Length && d.Worn[i];
        }
        int savedSlots = map.Length;
        var codex = Unbits(d.Codex, savedSlots * Content.RarityNames.Length * Hero.QualityBands * Hero.Eras);
        for (int k = 0; k < codex.Length; k++)
        {
            if (!codex[k]) continue;
            int key = Remap(k, map, h);
            if (key >= 0) h.Mark(key);
        }
        if (d.CodexValue > 0 && map.All(j => j >= 0) && map.Length == s.C.SlotCount) h.CodexValue = d.CodexValue;
        h.Talents.Restore(Unbits(d.Talents, h.Talents.Allocated.Length), d.Points, d.ParagonDamage, d.ParagonHp, h.S);

        foreach (var c in d.Chests ?? new()) s.Pile.Waiting.Add(c.ToChest(map, k => Remap(k, map, h)));
        if (d.Filling != null) s.Pile.Filling.Absorb(d.Filling.ToChest(map, k => Remap(k, map, h)));
        s.Pile.Kills = d.FillingKills;
        if (d.Auto != null)
        {
            s.Auto.OpenChests = d.Auto.OpenChests;
            s.Auto.Equip = d.Auto.Equip;
            s.Auto.Forge = d.Auto.Forge;
            s.Auto.Ranks = d.Auto.Ranks;
            s.Auto.Talents = d.Auto.Talents;
            s.Auto.Focus = d.Auto.Focus;
            s.Auto.SpendPrestige = d.Auto.SpendPrestige;
            s.Auto.Ascend = d.Auto.Ascend;
            s.Auto.HoldAt = d.Auto.HoldAt;
        }
        if (d.Totals != null)
        {
            var t = s.Totals;
            t.Kills = d.Totals.Kills;
            t.Gold = d.Totals.Gold.ToBig();
            t.Chests = d.Totals.Chests;
            t.Opened = d.Totals.Opened;
            t.Upgrades = d.Totals.Upgrades;
            t.NewEntries = d.Totals.NewEntries;
            t.Ascensions = d.Totals.Ascensions;
            t.Transcendences = d.Totals.Transcendences;
            t.Record = d.Totals.Record;
            for (int r = 0; r < t.Found.Length && r < (d.Totals.Found?.Length ?? 0); r++) t.Found[r] = d.Totals.Found[r];
        }
        h.Rebuild();
        s.AfterRestore();
        return s;
    }

    static int Get(int[] a, int i) => a != null && i < a.Length ? a[i] : 0;

    /// <summary>Each saved piece's index now (by name), or -1 for one this build doesn't have. A save without names is
    /// taken to be in this build's order.</summary>
    static int[] SlotMap(SaveData d, Content c)
    {
        if (d.SlotNames == null) return Enumerable.Range(0, c.SlotCount).ToArray();
        var now = new Dictionary<string, int>();
        foreach (var x in c.Slots) now[x.Name] = x.Index;
        return d.SlotNames.Select(n => n != null && now.TryGetValue(n, out int j) ? j : -1).ToArray();
    }

    /// <summary>A codex entry saved with the saved pieces' order, as this build numbers it (-1 if its piece is gone).</summary>
    static int Remap(int key, int[] map, Hero h)
    {
        int band = key % Hero.QualityBands;
        key /= Hero.QualityBands;
        int rarity = key % Content.RarityNames.Length;
        key /= Content.RarityNames.Length;
        int era = key / map.Length, slot = key % map.Length;
        return map[slot] < 0 ? -1 : h.CodexKey(era, map[slot], rarity, band);
    }

    static string Bits(bool[] bits)
    {
        var bytes = new byte[(bits.Length + 7) / 8];
        for (int i = 0; i < bits.Length; i++)
            if (bits[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
        return Convert.ToBase64String(bytes);
    }

    static bool[] Unbits(string s, int length)
    {
        var bits = new bool[length];
        if (string.IsNullOrEmpty(s)) return bits;
        var bytes = Convert.FromBase64String(s);
        for (int i = 0; i < length && (i >> 3) < bytes.Length; i++) bits[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        return bits;
    }
}
