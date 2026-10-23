using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report (godot --headless --path . -- --sim [--n=12] [--hours=6]): a sensible player (see
/// <see cref="Policy"/>) plays several seeds from scratch, opening every chest at once, and the report says how the
/// run paces out: when each stage and rarity first comes, how often a chest is an upgrade, where the walls are, and
/// what gold buys. Tune the numbers in Balance.cs against the targets in docs/design.md.
/// </summary>
public static class Simulation
{
    /// <summary>Seconds of game time per step: coarser than the screen's, which doesn't change the outcome much.</summary>
    const double Step = 0.05;

    sealed class Run
    {
        public int Seed;
        public readonly Dictionary<int, double> StageAt = new();
        public readonly double[] FirstFound = Enumerable.Repeat(double.NaN, Rarities.Count).ToArray();
        public readonly List<(double T, bool Upgrade, Rarity R)> Opened = new();
        public readonly List<(double T, int Stage, double Power, int ChestLevel, int Might, int Vigor, int Haste, string Gear)> Samples = new();
        public int BossTries, BossFails, Deaths, Retreats, Mimics;
        public readonly List<double> BountiesAt = new();
        public double LongestWall;
        public int LongestWallStage;
        public GameState Final;
    }

    public static void Report(int n, double hours, TextWriter output = null)
    {
        output ??= Console.Out;
        GameState.TickSize = Step;
        var sb = new StringBuilder();
        var runs = new List<Run>();
        for (int i = 0; i < n; i++) runs.Add(Play(1000 + i * 7919, hours));

        sb.AppendLine($"LootFall balance report: {n} runs of {hours:0.#}h, a sensible player opening every chest at once.");
        sb.AppendLine();
        sb.AppendLine("== When each stage is first reached (minutes: median, 10th and 90th percentile)");
        foreach (int s in new[] { 2, 3, 5, 10, 11, 15, 20, 21, 25, 30, 31, 35, 40, 41, 45, 50, 51, 55, 60, 61, 70, 80 })
        {
            var times = runs.Select(r => r.StageAt.TryGetValue(s, out var t) ? t / 60 : double.NaN).ToList();
            int reached = times.Count(t => !double.IsNaN(t));
            if (reached == 0) break;
            sb.AppendLine($"  stage {s,3} ({World.StageName(s),-22}) {Q(times, 0.5),7:0.0}m  [{Q(times, 0.1),6:0.0} .. {Q(times, 0.9),6:0.0}]  reached by {reached}/{n}");
        }

        sb.AppendLine();
        sb.AppendLine("== First find of each rarity (minutes, median)");
        for (int r = 1; r < Rarities.Count; r++)
        {
            var times = runs.Select(x => x.FirstFound[r] / 60).ToList();
            sb.AppendLine($"  {Rarities.All[r].Name,-10} {Q(times, 0.5),7:0.0}m  [{Q(times, 0.1),6:0.0} .. {Q(times, 0.9),6:0.0}]  found in {times.Count(t => !double.IsNaN(t))}/{n}");
        }

        sb.AppendLine();
        sb.AppendLine("== Chests: how many, and how often one is an upgrade (all runs pooled)");
        var windows = new[] { (0.0, 2.0), (2, 5), (5, 10), (10, 20), (20, 40), (40, 60), (60, 90), (90, 120), (120, 180), (180, 240), (240, 360), (360, 600) };
        foreach (var (a, b) in windows)
        {
            if (a >= hours * 60) break;
            var slice = runs.SelectMany(r => r.Opened).Where(o => o.T >= a * 60 && o.T < b * 60).ToList();
            if (slice.Count == 0) continue;
            double perMin = slice.Count / (double)n / (Math.Min(b, hours * 60) - a);
            var mix = string.Join(" ", Enumerable.Range(0, Rarities.Count).Select(r => $"{Rarities.All[r].Name[0]}{100.0 * slice.Count(o => (int)o.R == r) / slice.Count,4:0.0}"));
            sb.AppendLine($"  {a,4:0}-{b,-4:0}m  {perMin,5:0.0}/min  upgrades {100.0 * slice.Count(o => o.Upgrade) / slice.Count,5:0.0}%   rarity% {mix}");
        }

        sb.AppendLine();
        sb.AppendLine("== The knight over time (median run by stage at the end)");
        var median = runs.OrderBy(r => r.Final.MaxStage).ElementAt(n / 2);
        foreach (var s in median.Samples)
            sb.AppendLine($"  {s.T / 60,5:0}m  stage {s.Stage,3}  power {Num.Short(s.Power),7}  chest lv {s.ChestLevel,2}  might {s.Might,3} vigor {s.Vigor,3} haste {s.Haste,2}  gear {s.Gear}");

        sb.AppendLine();
        sb.AppendLine("== Walls and fights (median per run)");
        var worst = runs.OrderBy(r => r.LongestWall).Last();
        sb.AppendLine($"  longest stay on one stage: {Q(runs.Select(r => r.LongestWall / 60).ToList(), 0.5):0.0}m (median, at stages " +
                      $"{string.Join(",", runs.Select(r => r.LongestWallStage).OrderBy(x => x))}), worst {worst.LongestWall / 60:0.0}m at stage {worst.LongestWallStage}");
        double M(Func<Run, double> f) => Q(runs.Select(f).ToList(), 0.5);
        sb.AppendLine($"  boss tries {M(r => r.BossTries):0}, failed {M(r => r.Final.BossFails):0} (out of time {M(r => r.Final.BossTimeouts):0}); " +
                      $"knight down {M(r => r.Final.Deaths):0} ({M(r => r.Final.Deaths - (r.Final.BossFails - r.Final.BossTimeouts)):0} outside boss fights), " +
                      $"retreats {M(r => r.Retreats):0}; mimics {M(r => r.Mimics):0}");
        var finals = runs.Select(r => r.Final).ToList();
        sb.AppendLine($"  at the end: stage {Q(finals.Select(f => (double)f.MaxStage).ToList(), 0.5):0}, chest level {Q(finals.Select(f => (double)f.ChestLevel).ToList(), 0.5):0}, " +
                      $"chests opened {Q(finals.Select(f => (double)f.ChestsOpened).ToList(), 0.5):0}, kills {Q(finals.Select(f => (double)f.TotalKills).ToList(), 0.5):0}, gold earned {Num.Short(Q(finals.Select(f => f.GoldEarned).ToList(), 0.5))}, " +
                      $"bounties {Q(finals.Select(f => (double)f.BountiesDone).ToList(), 0.5):0}");
        foreach (var minutes in new[] { 10, 30, 60, 120 })
        {
            var at = runs.Select(r => (double)r.BountiesAt.Count(t => t <= minutes * 60)).ToList();
            sb.Append($"  bounties by {minutes}m: {Q(at, 0.5):0}");
        }
        sb.AppendLine();

        output.Write(sb.ToString());
        output.Flush();
    }

    /// <summary>One sensible player from scratch, for <paramref name="hours"/> of play.</summary>
    static Run Play(int seed, double hours)
    {
        var run = new Run { Seed = seed };
        var g = GameState.New(seed);
        g.CollectEvents = true;
        double t = 0, end = hours * 3600, lastStageAt = 0, nextSample = 0;
        int lastStage = 1;
        run.StageAt[1] = 0;
        while (t < end)
        {
            g.Update(Step);
            t += Step;
            foreach (var e in g.Events)
            {
                switch (e)
                {
                    case BossStarted: run.BossTries++; break;
                    case BossFailed: run.BossFails++; break;
                    case HeroDown:
                        run.Deaths++;
                        if (Environment.GetEnvironmentVariable("SIM_DEATHS") != null && !g.Battle.BossFight)
                            Console.Error.WriteLine($"{t / 60:0.0}m stage {g.Stage}/{g.MaxStage} kills {g.Kills} wave [{string.Join(",", g.Battle.Foes.Select(f => $"{f.Monster.Key}{(f.Alive ? "" : "+")}"))}] " +
                                                    $"hp {Num.Short(g.Stats.Hp)} def {Num.Short(g.Stats.Def)} cut {g.Stats.DefCut(Balance.MonsterAtk * Balance.MonsterScale(g.Stage)):0.00} atk {Num.Short(g.Stats.Atk)} spd {g.Stats.Speed:0.00} monsterHp {Num.Short(Balance.MonsterHp * Balance.MonsterScale(g.Stage))} atk {Num.Short(Balance.MonsterAtk * Balance.MonsterScale(g.Stage))}");
                        break;
                    case Retreated: run.Retreats++; break;
                    case MimicAppeared: run.Mimics++; break;
                    case BountyDone: run.BountiesAt.Add(t); break;
                }
            }
            g.Events.Clear();
            Policy.OpenAll(g, (o, equip) =>
            {
                run.Opened.Add((t, o.Upgrade, o.Item.Rarity));
                if (double.IsNaN(run.FirstFound[(int)o.Item.Rarity])) run.FirstFound[(int)o.Item.Rarity] = t;
            });
            if ((int)(t / Step) % 20 == 0) Policy.Spend(g);
            if (g.MaxStage != lastStage)
            {
                if (t - lastStageAt > run.LongestWall)
                {
                    run.LongestWall = t - lastStageAt;
                    run.LongestWallStage = lastStage;
                }
                lastStageAt = t;
                lastStage = g.MaxStage;
                run.StageAt.TryAdd(g.MaxStage, t);
            }
            if (t >= nextSample)
            {
                nextSample += t < 1800 ? 300 : 1800;
                run.Samples.Add((t, g.MaxStage, g.Power, g.ChestLevel, g.Might, g.Vigor, g.Haste, Gear(g)));
            }
        }
        run.LongestWall = Math.Max(run.LongestWall, t - lastStageAt);
        run.Final = g;
        return run;
    }

    /// <summary>For tuning: the median minutes at which each of <paramref name="stages"/> is first reached (NaN where fewer than half got there).</summary>
    public static double[] StageMinutes(int n, double hours, int[] stages)
    {
        GameState.TickSize = Step;
        var runs = Enumerable.Range(0, n).Select(i => Play(1000 + i * 7919, hours)).ToList();
        return stages.Select(s =>
        {
            var times = runs.Select(r => r.StageAt.TryGetValue(s, out var t) ? t / 60 : double.NaN).ToList();
            return times.Count(t => !double.IsNaN(t)) * 2 > n ? Q(times, 0.5) : double.NaN;
        }).ToArray();
    }

    /// <summary>What's worn, as one letter per slot's rarity (- for nothing): "CURCUC-U".</summary>
    static string Gear(GameState g) => new(g.Equipped.Select(i => i == null ? '-' : "CURELM"[(int)i.Rarity]).ToArray());

    /// <summary>Plays a fresh game with the sensible player until it reaches a stage (or runs out of time), for debugging shortcuts.</summary>
    public static GameState FastForward(int seed, int stage, double maxHours = 12)
    {
        var g = GameState.New(seed);
        g.CollectEvents = false;
        for (double t = 0; t < maxHours * 3600 && g.MaxStage < stage; t += Step)
        {
            g.Update(Step);
            Policy.OpenAll(g);
            Policy.Spend(g);
        }
        g.CollectEvents = true;
        return g;
    }

    /// <summary>The q-th quantile of the values that aren't NaN (NaN if none are).</summary>
    static double Q(List<double> values, double q)
    {
        var v = values.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
        if (v.Count == 0) return double.NaN;
        double pos = q * (v.Count - 1);
        int i = (int)Math.Floor(pos);
        return i + 1 < v.Count ? v[i] + (v[i + 1] - v[i]) * (pos - i) : v[i];
    }
}
