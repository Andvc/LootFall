using System;
using System.Collections.Generic;

namespace LootFall.Progression;

/// <summary>Things that happen, counted per hour of play.</summary>
public enum Ev : byte
{
    Upgrade, Level, Forge, Rank, Mastery, Codex, SlotUnlock, Record, Ascension, Transcendence, LayerRank, Noticed, Major, Count,
}

/// <summary>The hero at one moment.</summary>
public sealed class Snap
{
    public double Hours, Reach, PowerLe, AetherLog, StardustLog, GoldLog, Materials, ForgeAvg, RankAvg, MasteryAvg, KillRate;
    public int RunDepth, EverDepth, FarmDepth, Level, Nodes, Paragon, Slots, MaxRarity, Codex, Ascensions, Transcendences;
    /// <summary>LE each system gives (power lost without it at the current reach), in Sys order, or null.</summary>
    public double[] Knockout;
    /// <summary>How many of each event so far (Ev order).</summary>
    public double[] Totals;
}

/// <summary>The systems knockouts measure, in order.</summary>
public enum Sys : byte { Rarity, Affixes, Forge, Ranks, Mastery, Talents, Paragon, Codex, Ascension, Transcendence, Count }

/// <summary>
/// What the simulator records: snapshots at log-spaced times (with knockouts, the reach each system is worth, and the
/// running count of each event), and how often the player sees progress: a "noticed" gain is reach up 0.1 depth
/// (about 1% more power) since the last one, a "major" gain a whole depth (about 11%), a record a depth never reached
/// before. The gaps between them are the framework's measure of whether progress stays tangible.
/// </summary>
public sealed class Metrics
{
    readonly Sim _sim;
    /// <summary>Knockouts at every snapshot instead of every fourth (for the tuner, which averages them).</summary>
    public bool KnockoutEverySnapshot;
    public double Kills;
    public int FarmDepth;
    public readonly List<Snap> Snaps = new();
    public readonly List<(double Hours, double Gap)> NoticedGaps = new(), MajorGaps = new(), RecordGaps = new();
    public readonly List<(double Hours, double Seconds, int Depth, Ev Kind)> Prestiges = new();
    public readonly List<(double Hours, int Depth)> Records = new();
    public readonly List<(double Hours, double Score)> Upgrades = new();
    public int Allocated, Paragon;

    double _noticedBase, _noticedTime, _majorBase, _majorTime, _recordTime, _nextSnap = 60;
    int _snapIndex;
    public double LastReach;

    public Metrics(Sim sim) => _sim = sim;

    readonly double[] _totals = new double[(int)Ev.Count];

    public void Count(Ev e, double n = 1) => _totals[(int)e] += n;

    public void Upgrade(double score)
    {
        Count(Ev.Upgrade);
        if (Upgrades.Count < 2_000_000) Upgrades.Add((_sim.Hours, score));
    }

    public void Talents(int allocated, int paragon)
    {
        Allocated = allocated;
        Paragon = paragon;
    }

    public void DepthRecord(int depth)
    {
        Count(Ev.Record);
        RecordGaps.Add((_sim.Hours, _sim.Time - _recordTime));
        Records.Add((_sim.Hours, depth));
        _recordTime = _sim.Time;
    }

    public void Prestige(Ev kind, double seconds, int depth)
    {
        Count(kind);
        Prestiges.Add((_sim.Hours, seconds, depth, kind));
        // A new run starts from scratch: progress is measured from where it starts.
        _resetPending = true;
    }

    bool _resetPending = true;

    public void AfterStep(double dt)
    {
        double reach = _sim.H.Reach();
        LastReach = reach;
        if (_resetPending)
        {
            _noticedBase = _majorBase = reach;
            _noticedTime = _majorTime = _sim.Time;
            _resetPending = false;
        }
        if (reach >= _noticedBase + 0.1)
        {
            Count(Ev.Noticed);
            NoticedGaps.Add((_sim.Hours, _sim.Time - _noticedTime));
            _noticedBase = reach;
            _noticedTime = _sim.Time;
        }
        if (reach >= _majorBase + 1)
        {
            Count(Ev.Major);
            MajorGaps.Add((_sim.Hours, _sim.Time - _majorTime));
            _majorBase = reach;
            _majorTime = _sim.Time;
        }
        if (_sim.Time >= _nextSnap) Snapshot();
    }

    /// <summary>Takes a snapshot (every sixteenth of a decade of time, plus every 100 hours late); knockouts every fourth.</summary>
    public void Snapshot(bool force = false)
    {
        if (!force)
        {
            _snapIndex++;
            _nextSnap = Math.Min(60 * Math.Pow(10, _snapIndex / 16.0), Math.Ceiling((_sim.Time + 1) / 360000) * 360000);
        }
        var h = _sim.H;
        double reach = h.Reach();
        var f = h.Fight.At(h.Total, h.Base, Math.Max(1, reach));
        double forge = 0, rank = 0, mastery = 0;
        int open = 0;
        for (int s = 0; s < h.Gear.Length; s++)
        {
            if (!h.Open[s]) continue;
            open++;
            forge += h.Forge[s];
            rank += h.Rank[s];
            mastery += h.Mastery[s];
        }
        var snap = new Snap
        {
            Hours = _sim.Hours,
            Reach = reach,
            PowerLe = 0.5 * (f.Dps + f.Hp),
            RunDepth = h.RunDepth,
            EverDepth = h.EverDepth,
            FarmDepth = FarmDepth,
            Level = h.Level,
            Nodes = Allocated,
            Paragon = Paragon,
            Slots = h.OpenCount,
            MaxRarity = h.MaxRarity,
            Codex = h.CodexCount,
            Ascensions = h.Ascension.Resets,
            Transcendences = h.Transcendence.Resets,
            AetherLog = h.Ascension.Lifetime.IsZero ? 0 : h.Ascension.Lifetime.Log10,
            StardustLog = h.Transcendence.Lifetime.IsZero ? 0 : h.Transcendence.Lifetime.Log10,
            GoldLog = h.Gold.IsZero ? 0 : h.Gold.Log10,
            Materials = h.Materials,
            ForgeAvg = open == 0 ? 0 : forge / open,
            RankAvg = open == 0 ? 0 : rank / open,
            MasteryAvg = open == 0 ? 0 : mastery / open,
            KillRate = h.Fight.KillRate(h.Fight.At(h.Total, h.Base, Math.Max(1, FarmDepth)), Math.Max(1, FarmDepth)),
            Totals = (double[])_totals.Clone(),
        };
        if (force || KnockoutEverySnapshot || _snapIndex % 4 == 0) snap.Knockout = Knockouts(reach);
        Snaps.Add(snap);
    }

    /// <summary>The LE each system gives: the hero's power (mean of damage and health LE at the current reach) with
    /// everything, minus the same without that system.</summary>
    public double[] Knockouts(double reach)
    {
        var h = _sim.H;
        double d = Math.Max(1, reach);
        double Power() { var f = h.Fight.At(h.Total, h.Base, d); return 0.5 * (f.Dps + f.Hp); }
        double all = Power();
        var k = new double[(int)Sys.Count];
        for (int i = 0; i < k.Length; i++)
        {
            h.Knock = (Sys)i;
            h.Rebuild();
            k[i] = all - Power();
        }
        h.Knock = Sys.Count;
        h.Rebuild();
        return k;
    }
}
