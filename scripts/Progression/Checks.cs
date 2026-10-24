using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Progression;

/// <summary>
/// The framework's acceptance test: the stability conditions the parameters must meet, and what simulated active, idle
/// and no-prestige players must see over the same hours (growth without walls or runaway, prestige that pays, idle play
/// that keeps up, every system mattering, progress that keeps showing, prestige on a sensible rhythm, content that lasts).
/// A run passes when every check does; `progsim --check` exits non-zero otherwise, so CI catches a change that breaks
/// the economy. Thresholds are the targets in docs/progression.md.
/// </summary>
public static class Checks
{
    public sealed class Result
    {
        public string Name, Detail;
        public bool Pass;
    }

    public static List<Result> Run(Growth g, Sim active, Sim idle, Sim none)
    {
        var results = new List<Result>();
        void Add(string name, bool pass, string detail) => results.Add(new Result { Name = name, Pass = pass, Detail = detail });
        double hours = active.Hours;

        // ---------------------------------------------------------------- stability, from the parameters
        Add("depth loop gain at most 0.6", g.LoopGain <= 0.6, $"{g.LoopGain:0.00}");
        Add("prestige feedback at most 0.7", g.PrestigeFeedback <= 0.7, $"{g.PrestigeFeedback:0.00}");
        Add("predicted growth exponent 0.3-0.7", g.Alpha is >= 0.3 and <= 0.7, $"{g.Alpha:0.00}");

        // ---------------------------------------------------------------- growth, measured
        double from = Math.Max(100, hours / 10);
        if (hours >= 2 * from)
        {
            double measured = Exponent(active, from, hours);
            Add("measured growth exponent 0.3-0.75", measured is >= 0.3 and <= 0.75, $"{measured:0.00} from {Report.Time(from)} to {Report.Time(hours)}");
            Add("measured exponent within 0.15 of the prediction", Math.Abs(measured - g.Alpha) <= 0.15, $"{measured:0.00} vs {g.Alpha:0.00}");
            double a3 = from * Math.Pow(hours / from, 2 / 3.0), late = Exponent(active, a3, hours);
            Add("no runaway: late growth within 0.15 of the prediction", late <= g.Alpha + 0.15, $"{late:0.00} over the last third");
        }
        var stalls = new List<string>();
        for (int i = 1; i < Report.Marks.Length && Report.Marks[i] <= hours; i++)
            if (Report.Marks[i - 1] >= 1 && Depth(active, Report.Marks[i]) <= Depth(active, Report.Marks[i - 1])) stalls.Add(Report.Time(Report.Marks[i]));
        Add("no wall: a new record in every window", stalls.Count == 0, stalls.Count == 0 ? "every window" : "none by " + string.Join(", ", stalls));
        foreach (var (mark, lo, hi) in new[] { (1.0, 100, 600), (10.0, 800, 4000), (100.0, 3000, 10000), (1000.0, 8000, 25000) })
        {
            if (mark > hours) break;
            double d = Depth(active, mark);
            Add($"pacing: depth at {Report.Time(mark)} within {lo}-{hi}", d >= lo && d <= hi, $"{d:0}");
        }

        // ---------------------------------------------------------------- prestige pays, idle keeps up
        if (hours >= 500)
        {
            double ratio = Depth(active, hours) / Depth(none, hours);
            Add("prestige pays: twice the depth of never prestiging", ratio >= 2, $"x{ratio:0.0} at {Report.Time(hours)}");
        }
        var behind = Report.Marks.Where(m => m >= 20 && m <= hours && Depth(active, m) < 0.9 * Depth(none, m)).ToList();
        Add("prestige never sets the player back (from 20h)", behind.Count == 0, behind.Count == 0 ? "ahead at every mark" : "behind at " + string.Join(", ", behind.Select(Report.Time)));
        var marks = Report.Marks.Where(m => m >= 100 && m <= hours).ToList();
        if (marks.Count > 0)
        {
            double worst = marks.Min(m => Depth(idle, m) / Depth(active, m));
            Add("idle play keeps up: 70% of active from 100h", worst >= 0.7, $"worst {100 * worst:0}%");
        }

        // ---------------------------------------------------------------- every system matters
        var snap = active.M.Snaps.LastOrDefault(s => s.Knockout != null && s.Hours >= hours / 2);
        if (snap != null)
        {
            double total = snap.Knockout.Sum(x => Math.Max(0, x));
            double Share(params Sys[] sys) => sys.Sum(s => Math.Max(0, snap.Knockout[(int)s])) / Math.Max(1e-9, total);
            var groups = new (string Name, Sys[] Systems, double Min)[]
            {
                ("gear (rarity, affixes)", new[] { Sys.Rarity, Sys.Affixes }, 0.01),
                ("forge", new[] { Sys.Forge }, 0.03),
                ("ranks", new[] { Sys.Ranks }, 0.03),
                ("mastery", new[] { Sys.Mastery }, 0.03),
                ("talents and paragon", new[] { Sys.Talents, Sys.Paragon }, 0.03),
                ("codex", new[] { Sys.Codex }, 0.02),
                ("ascension", new[] { Sys.Ascension }, 0.03),
                ("transcendence", new[] { Sys.Transcendence }, 0.03),
            };
            foreach (var (name, sys, min) in groups)
            {
                double share = Share(sys);
                Add($"{name} gives {100 * min:0}-40% of power", share >= min && share <= 0.4, $"{100 * share:0.0}% at {Report.Time(snap.Hours)}");
            }
        }

        // ---------------------------------------------------------------- progress keeps showing
        foreach (var (sim, name) in new[] { (active, "active"), (idle, "idle") })
        {
            var noticed = sim.M.NoticedGaps.Where(x => x.Hours >= 1).Select(x => x.Gap).DefaultIfEmpty(0).Max() / 3600;
            Add($"{name}: a noticeable gain at least hourly (after 1h)", noticed <= 1, $"longest wait {Report.Time(noticed)}");
            var droughts = sim.M.RecordGaps.Where(x => x.Hours >= 20 && x.Gap / 3600 > 0.25 * x.Hours).ToList();
            double worstShare = sim.M.RecordGaps.Where(x => x.Hours >= 20).Select(x => x.Gap / 3600 / x.Hours).DefaultIfEmpty(0).Max();
            Add($"{name}: no record drought over a quarter of the time played", droughts.Count == 0, $"worst {100 * worstShare:0}% of the time played");
        }

        // ---------------------------------------------------------------- prestige rhythm
        // Runs lengthen as growth slows (a fixed gain takes longer the more has been played), but stay short next to the
        // time played: in every window from 50h, the median run is at most a day or 2% of the hours played by its end.
        var slow = new List<string>();
        double prev = 0;
        foreach (var mark in Report.Marks.Where(m => m <= hours))
        {
            var runs = active.M.Prestiges.Where(x => x.Kind == Ev.Ascension && x.Hours > prev && x.Hours <= mark && x.Hours >= 50).Select(x => x.Seconds / 3600).OrderBy(x => x).ToList();
            if (runs.Count > 0)
            {
                double median = runs[runs.Count / 2];
                if (median < 0.25 || median > Math.Max(24, 0.02 * mark)) slow.Add($"{Report.Time(median)} by {Report.Time(mark)}");
            }
            prev = mark;
        }
        Add("ascension runs: median 15m to max(1 day, 2% of time played)", slow.Count == 0, slow.Count == 0 ? "every window" : string.Join(", ", slow));
        var trans = active.M.Prestiges.Where(x => x.Kind == Ev.Transcendence).ToList();
        if (hours >= 300)
        {
            Add("first transcendence within 300h", trans.Count > 0 && trans[0].Hours <= 300, trans.Count > 0 ? $"at {Report.Time(trans[0].Hours)}" : "never");
            // The first cycle starts at the beginning of the game, so it doesn't count.
            double longest = trans.Skip(1).Select(x => x.Seconds / 3600 / x.Hours).DefaultIfEmpty(0).Max();
            double open = trans.Count > 0 ? (hours - trans[^1].Hours) / hours : 1;
            Add("transcendence cycles under half the time played", Math.Max(longest, open) <= 0.5, $"longest {100 * Math.Max(longest, open):0}%");
        }

        // ---------------------------------------------------------------- content lasts
        var last = active.M.Snaps[^1];
        int depthSlots = active.H.C.Slots.Count(s => s.UnlockDepth > 0);
        var firstFull = active.M.Snaps.FirstOrDefault(s => s.Nodes >= active.H.Tree.Nodes.Count);
        Add("talent tree full, but not before 100h", firstFull == null ? hours < 3000 : firstFull.Hours >= 100, firstFull == null ? $"{last.Nodes} of {active.H.Tree.Nodes.Count} nodes" : $"full at {Report.Time(firstFull.Hours)}");
        if (hours >= 3000)
        {
            int open = active.H.C.Slots.Count(s => s.UnlockDepth > 0 && active.H.Open[s.Index]);
            Add("every depth slot open by the end", open == depthSlots, $"{open} of {depthSlots}");
            Add("rarity reaches Divine or better", last.MaxRarity >= 8, Content.RarityNames[last.MaxRarity]);
        }

        // ---------------------------------------------------------------- sanity
        foreach (var (sim, name) in new[] { (active, "active"), (idle, "idle"), (none, "no-prestige") })
        {
            bool finite = sim.M.Snaps.All(s => double.IsFinite(s.Reach) && double.IsFinite(s.PowerLe) && (s.Knockout == null || s.Knockout.All(double.IsFinite)));
            bool monotone = sim.M.Snaps.Zip(sim.M.Snaps.Skip(1)).All(x => x.Second.EverDepth >= x.First.EverDepth);
            Add($"{name}: numbers finite, records never go back", finite && monotone, finite ? monotone ? "ok" : "a record went back" : "non-finite numbers");
        }
        return results;
    }

    static double Depth(Sim s, double hours) => s.M.Snaps.LastOrDefault(x => x.Hours <= hours + 1e-9)?.EverDepth ?? 1;

    static double Exponent(Sim s, double a, double b) => Math.Log(Depth(s, b) / Depth(s, a)) / Math.Log(b / a);
}
