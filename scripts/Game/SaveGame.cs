using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LootFall.Progression;

namespace LootFall.Game;

/// <summary>
/// A saved game as plain data: the world (time, the run, aether owed), the hero (gear, forge, ranks, mastery, codex,
/// level, gold, materials), talents, both prestige layers, the chests, the automation and the totals. What the rules
/// can work out again (open slots, rarity tiers, the codex's worth, the stat sheets) isn't stored. Bump
/// <see cref="CurrentVersion"/> when an older save can't be read as it is.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    /// <summary>When it was written (Unix seconds), to work out the time away.</summary>
    public long SavedAt;
    public int Seed;

    public double Time, DepthProgress, RunStart, CycleStart, LastRun;
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

    public Chest ToChest()
    {
        var c = new Chest { Kind = (ChestKind)Kind, Depth = Depth, Materials = Materials };
        for (int i = 0; i < (Slots?.Length ?? 0); i++) c.Items.Add((Slots[i], Items[i].ToItem()));
        for (int r = 0; r < c.Counts.Length && r < (Counts?.Length ?? 0); r++) c.Counts[r] = Counts[r];
        if (Found != null) c.Found.AddRange(Found);
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
            DepthProgress = w.DepthProgress, RunStart = w.RunStart, CycleStart = w.CycleStart, LastRun = w.LastRun,
            LastPeak = w.LastPeak, RunDepths = w.RunDepths.Select(x => new[] { x.Time, x.Depth }).ToList(),
            PendingAether = BigData.Of(w.PendingAether), Rng = w.Rng.State,
            RunDepth = h.RunDepth, EverDepth = h.EverDepth, Level = h.Level, Xp = BigData.Of(h.Xp), Gold = BigData.Of(h.Gold),
            Materials = h.Materials, Gear = h.Gear.Select(ItemData.Of).ToArray(),
            Forge = (int[])h.Forge.Clone(), Rank = (int[])h.Rank.Clone(), Mastery = (int[])h.Mastery.Clone(),
            MasteryXp = (double[])h.MasteryXp.Clone(), Focus = (bool[])h.Focus.Clone(), Codex = Bits(h.Codex), CodexValue = h.CodexValue,
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
        int n = h.Gear.Length;
        for (int i = 0; i < n; i++)
        {
            h.Gear[i] = d.Gear != null && i < d.Gear.Length ? d.Gear[i]?.ToItem() : null;
            h.Forge[i] = Get(d.Forge, i);
            h.Rank[i] = Get(d.Rank, i);
            h.Mastery[i] = Get(d.Mastery, i);
            h.MasteryXp[i] = d.MasteryXp != null && i < d.MasteryXp.Length ? d.MasteryXp[i] : 0;
            h.Focus[i] = d.Focus != null && i < d.Focus.Length && d.Focus[i];
        }
        var codex = Unbits(d.Codex, h.Codex.Length);
        for (int k = 0; k < codex.Length; k++)
            if (codex[k]) h.Mark(k);
        if (d.CodexValue > 0) h.CodexValue = d.CodexValue;
        h.Talents.Restore(Unbits(d.Talents, h.Talents.Allocated.Length), d.Points, d.ParagonDamage, d.ParagonHp, h.S);

        foreach (var c in d.Chests ?? new()) s.Pile.Waiting.Add(c.ToChest());
        if (d.Filling != null) s.Pile.Filling.Absorb(d.Filling.ToChest());
        s.Pile.Kills = d.FillingKills;
        if (d.Auto != null)
        {
            s.Auto.OpenChests = d.Auto.OpenChests;
            s.Auto.Forge = d.Auto.Forge;
            s.Auto.Ranks = d.Auto.Ranks;
            s.Auto.Talents = d.Auto.Talents;
            s.Auto.Focus = d.Auto.Focus;
            s.Auto.SpendPrestige = d.Auto.SpendPrestige;
            s.Auto.Ascend = d.Auto.Ascend;
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
