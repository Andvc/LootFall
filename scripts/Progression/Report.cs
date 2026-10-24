using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LootFall.Progression;

/// <summary>Turns a simulated run into tables: the hero over time, how often progress shows by window of time, what each
/// system is worth (knockouts), and the prestige history.</summary>
public static class Report
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Time(double hours)
    {
        if (hours < 1.0 / 60) return (hours * 3600).ToString("0", Inv) + "s";
        if (hours < 1) return (hours * 60).ToString("0", Inv) + "m";
        if (hours < 100) return hours.ToString(hours < 10 ? "0.0" : "0", Inv) + "h";
        return hours.ToString("0", Inv) + "h";
    }

    static string F(double v, string fmt = "0.0") => v.ToString(fmt, Inv);

    /// <summary>A number given as its log10, short: 0 as "-", else like 3.2e5.</summary>
    static string Sci(double log10) => log10 <= 0 ? "-" : $"{Math.Pow(10, log10 - Math.Floor(log10)).ToString("0.0", Inv)}e{Math.Floor(log10)}";

    public static readonly double[] Marks = { 1 / 60.0, 5 / 60.0, 0.25, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 3000, 5000, 10000 };

    public static string Write(Sim sim, Content content, TalentTree tree)
    {
        var sb = new StringBuilder();
        var m = sim.M;
        var h = sim.H;
        sb.AppendLine($"LootFall progression: {sim.Pr.Name} player, {Time(sim.Hours)} simulated");
        sb.AppendLine($"  content: {content.SlotCount} equipment slots, {Content.RarityNames.Length} rarity tiers, {content.AffixVariants} affix variants, " +
                      $"{tree.Nodes.Count} talent nodes (+ endless paragon), {h.Ascension.Tree.Nodes.Count} ascension and {h.Transcendence.Tree.Nodes.Count} transcendence nodes, " +
                      $"{h.Codex.Length} codex entries");
        sb.AppendLine($"  stability: {Growth.Analyze(h.P, tree)}");
        sb.AppendLine();

        // ---------------------------------------------------------------- timeline
        sb.AppendLine("== The hero over time");
        sb.AppendLine("      time   ever  run   reach  level  nodes paragon slots rarity codex  asc trans  forge  rank mastery  powerLE aether stardust");
        foreach (var mark in Marks)
        {
            if (mark > sim.Hours + 1e-9) break;
            var s = m.Snaps.LastOrDefault(x => x.Hours <= mark + 1e-9);
            if (s == null) continue;
            sb.AppendLine($"  {Time(mark),8} {s.EverDepth,6} {s.RunDepth,4} {F(s.Reach),7} {s.Level,6} {s.Nodes,6} {s.Paragon,7} {s.Slots,5} {Content.RarityNames[s.MaxRarity],-6} {s.Codex,5} {s.Ascensions,4} {s.Transcendences,5} {F(s.ForgeAvg),6} {F(s.RankAvg),5} {F(s.MasteryAvg),7} {F(s.PowerLe, "0"),8} {Sci(s.AetherLog),6} {Sci(s.StardustLog),8}");
        }
        sb.AppendLine();

        // ---------------------------------------------------------------- cadence
        sb.AppendLine("== Progress by window of play (per hour unless noted; gaps are the median and longest wait)");
        sb.AppendLine("      window    depth+  noticed  gap med/max     major  gap max   record gap max  upgrades levels  forge  ranks mastery codex  asc trans");
        double prev = 0;
        foreach (var mark in Marks)
        {
            if (prev >= sim.Hours - 1e-9) break;
            double end = Math.Min(mark, sim.Hours);
            if (end <= prev) { prev = mark; continue; }
            double span = end - prev;
            var startSnap = m.Snaps.LastOrDefault(x => x.Hours <= prev + 1e-9) ?? m.Snaps[0];
            var endSnap = m.Snaps.LastOrDefault(x => x.Hours <= end + 1e-9) ?? m.Snaps[^1];
            // Event rates from the running totals between the snapshots nearest the window's ends.
            double snapSpan = Math.Max(1e-9, endSnap.Hours - startSnap.Hours);
            var counts = new double[(int)Ev.Count];
            for (int i = 0; i < counts.Length; i++) counts[i] = (endSnap.Totals[i] - startSnap.Totals[i]) / snapSpan * span;
            var noticed = m.NoticedGaps.Where(g => g.Hours > prev && g.Hours <= end).Select(g => g.Gap).ToList();
            var major = m.MajorGaps.Where(g => g.Hours > prev && g.Hours <= end).Select(g => g.Gap).ToList();
            var record = m.RecordGaps.Where(g => g.Hours > prev && g.Hours <= end).Select(g => g.Gap).ToList();
            sb.AppendLine($"  {Time(prev),5}-{Time(end),-6} {endSnap.EverDepth - startSnap.EverDepth,7} {F(noticed.Count / span, "0.0"),8} {Gap(noticed, 0.5),6}/{Gap(noticed, 1),-6} {F(major.Count / span, "0.00"),8} {Gap(major, 1),7}  {F(record.Count / span, "0.0"),6} {Gap(record, 1),7} " +
                          $"{F(counts[(int)Ev.Upgrade] / span, "0"),9} {F(counts[(int)Ev.Level] / span, "0.0"),6} {F(counts[(int)Ev.Forge] / span, "0"),6} {F(counts[(int)Ev.Rank] / span, "0.0"),6} {F(counts[(int)Ev.Mastery] / span, "0.0"),7} {F(counts[(int)Ev.Codex] / span, "0.0"),5} {endSnap.Totals[(int)Ev.Ascension] - startSnap.Totals[(int)Ev.Ascension],4} {endSnap.Totals[(int)Ev.Transcendence] - startSnap.Totals[(int)Ev.Transcendence],5}");
            prev = mark;
        }
        sb.AppendLine();

        // ---------------------------------------------------------------- what each system is worth
        sb.AppendLine("== Power each system gives (LE, i.e. log base R of its multiplier, and its share of all of them)");
        sb.Append("      time    reach");
        for (int i = 0; i < (int)Sys.Count; i++) sb.Append($" {((Sys)i).ToString()[..Math.Min(8, ((Sys)i).ToString().Length)],9}");
        sb.AppendLine();
        foreach (var mark in Marks)
        {
            if (mark > sim.Hours + 1e-9) break;
            var s = m.Snaps.LastOrDefault(x => x.Hours <= mark + 1e-9 && x.Knockout != null);
            if (s == null) continue;
            sb.Append($"  {Time(s.Hours),8} {F(s.Reach),8}");
            double total = s.Knockout.Sum(x => Math.Max(0, x));
            for (int i = 0; i < (int)Sys.Count; i++) sb.Append($" {F(s.Knockout[i], "0"),4}/{F(100 * Math.Max(0, s.Knockout[i]) / Math.Max(1e-9, total), "0"),3}%");
            sb.AppendLine();
        }
        sb.AppendLine();

        // ---------------------------------------------------------------- prestige
        if (m.Prestiges.Count > 0)
        {
            sb.AppendLine("== Prestige (runs ending in each window: count, median run length, median depth reached)");
            prev = 0;
            foreach (var mark in Marks)
            {
                if (prev >= sim.Hours) break;
                var runs = m.Prestiges.Where(x => x.Hours > prev && x.Hours <= mark).ToList();
                if (runs.Count > 0)
                {
                    var asc = runs.Where(r => r.Kind == Ev.Ascension).ToList();
                    var tr = runs.Where(r => r.Kind == Ev.Transcendence).ToList();
                    sb.Append($"  {Time(prev),5}-{Time(mark),-6}");
                    if (asc.Count > 0) sb.Append($" ascensions {asc.Count,4}: run {Time(Median(asc.Select(r => r.Seconds / 3600))),6}, depth {Median(asc.Select(r => (double)r.Depth)),6:0}");
                    if (tr.Count > 0) sb.Append($"   transcendences {tr.Count,3}: cycle {Time(Median(tr.Select(r => r.Seconds / 3600))),6}, depth {Median(tr.Select(r => (double)r.Depth)),6:0}");
                    sb.AppendLine();
                }
                prev = mark;
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static string Gap(List<double> gaps, double q)
    {
        if (gaps.Count == 0) return "-";
        var sorted = gaps.OrderBy(x => x).ToList();
        double v = sorted[Math.Min(sorted.Count - 1, (int)(q * (sorted.Count - 1) + 0.5))];
        return Time(v / 3600);
    }

    static double Median(IEnumerable<double> xs)
    {
        var l = xs.OrderBy(x => x).ToList();
        return l.Count == 0 ? 0 : l[l.Count / 2];
    }
}
