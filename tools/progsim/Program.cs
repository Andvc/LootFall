using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LootFall.Progression;

// progsim [--hours=3000] [--profile=active|idle|no-prestige] [--seed=1] [--debug-talents]: one player's report.
// progsim --check [--hours=3000]: the acceptance test (Checks.cs) on all three players; exits 1 if any check fails.
// progsim --tune [--hours=1000]: fits the open-ended systems' power knobs to their targets (see Tuner below).
var arg = (string name, string fallback) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1] ?? fallback;
double hours = double.Parse(arg("hours", args.Contains("--tune") ? "1000" : "3000"), CultureInfo.InvariantCulture);
int seed = int.Parse(arg("seed", "1"), CultureInfo.InvariantCulture);
var profile = arg("profile", "active") switch
{
    "idle" => Profile.Idle(),
    "no-prestige" => Profile.NoPrestige(),
    _ => Profile.Active(),
};

if (args.Contains("--tune"))
{
    Tuner.Run(new Params(), hours, seed);
    return 0;
}

if (args.Contains("--check"))
{
    var cp = new Params();
    var cc = new Content(cp);
    var ct = new TalentTree(cp);
    var clock = Stopwatch.StartNew();
    var players = new[] { Profile.Active(), Profile.Idle(), Profile.NoPrestige() }.Select(pr => new Sim(cp, cc, ct, pr, seed)).ToArray();
    Parallel.ForEach(players, s => s.Run(hours));
    var growth = Growth.Analyze(cp, ct);
    var results = Checks.Run(growth, players[0], players[1], players[2]);
    Console.WriteLine($"Progression check: {Report.Time(hours)} for active, idle and no-prestige players (seed {seed})");
    Console.WriteLine($"  {growth}");
    foreach (var r in results) Console.WriteLine($"  {(r.Pass ? "pass" : "FAIL")}  {r.Name,-58} {r.Detail}");
    int failed = results.Count(r => !r.Pass);
    Console.WriteLine(failed == 0 ? $"All {results.Count} checks pass ({clock.Elapsed.TotalSeconds:0}s)." : $"{failed} of {results.Count} checks FAIL ({clock.Elapsed.TotalSeconds:0}s).");
    return failed == 0 ? 0 : 1;
}

var p = new Params();
var content = new Content(p);
var tree = new TalentTree(p);
var sim = new Sim(p, content, tree, profile, seed);
if (args.Contains("--debug-prestige")) sim.Log = new();
var watch = Stopwatch.StartNew();
sim.Run(hours);
Console.WriteLine(Report.Write(sim, content, tree));
if (sim.Log != null) foreach (var line in sim.Log) Console.WriteLine(line);
if (args.Contains("--debug-talents"))
{
    var t = sim.H.Talents;
    for (int sec = 0; sec < TalentTree.Sectors; sec++)
    {
        var rings = new int[tree.Rings];
        var total = new int[tree.Rings];
        foreach (var n in tree.Nodes)
        {
            if (n.Sector != sec || n.Ring < 0) continue;
            total[n.Ring]++;
            if (t.Allocated[n.Id]) rings[n.Ring]++;
        }
        Console.WriteLine($"{TalentTree.SectorNames[sec],-8} " + string.Join(" ", rings.Select((r, i) => $"{r}/{total[i]}")));
    }
    var w = sim.Weights;
    Console.WriteLine($"weights: more damage {w[(int)Stat.MoreDamage]:0.000}  more health {w[(int)Stat.MoreHp]:0.000}  inc damage {w[(int)Stat.IncDamage]:0.0000}  paragon {p.ParagonLE * Math.Max(w[(int)Stat.MoreDamage], w[(int)Stat.MoreHp]):0.0000}");
}
Console.WriteLine($"({watch.Elapsed.TotalSeconds:0.0}s to simulate)");
return 0;

/// <summary>
/// Fits the power knobs of the open-ended systems (the ones that grow without bound: ranks, mastery, codex and the two
/// prestige layers) so that each gives its target LE to an active player at the end of the run, averaged over the
/// second half and over three seeds so a prestige cycle's phase doesn't skew it. Each system's LE is close to
/// proportional to its knob, so a damped multiplicative update converges in a handful of rounds. The bounded systems
/// (rarity, affixes, the talent tree), the depth-coupled forge and paragon (which follows depth too steeply to fit) are
/// set by design; the depth the active player reaches follows.
/// </summary>
static class Tuner
{
    static readonly (string Name, Sys System, double Target, Func<Params, double> Get, Action<Params, double> Set)[] Knobs =
    {
        ("AscensionPower", Sys.Ascension, 250, p => p.AscensionPower, (p, v) => p.AscensionPower = v),
        ("TranscendencePower", Sys.Transcendence, 220, p => p.TranscendencePower, (p, v) => p.TranscendencePower = v),
        ("MasteryLE", Sys.Mastery, 200, p => p.MasteryLE, (p, v) => p.MasteryLE = v),
        ("RankLE", Sys.Ranks, 220, p => p.RankLE, (p, v) => p.RankLE = v),
        ("CodexLE", Sys.Codex, 70, p => p.CodexLE, (p, v) => p.CodexLE = v),
    };

    public static void Run(Params p, double hours, int seed)
    {
        for (int round = 1; round <= 12; round++)
        {
            var content = new Content(p);
            var tree = new TalentTree(p);
            var sims = Enumerable.Range(seed, 3).Select(s => new Sim(p, content, tree, Profile.Active(), s)).ToArray();
            Parallel.ForEach(sims, sim =>
            {
                sim.M.KnockoutEverySnapshot = true;
                sim.Run(hours);
            });
            double[] le = new double[(int)Sys.Count];
            foreach (var sim in sims)
            {
                var late = sim.M.Snaps.Where(s => s.Knockout != null && s.Hours >= hours / 2).ToList();
                foreach (var s in late) for (int i = 0; i < le.Length; i++) le[i] += s.Knockout[i] / late.Count / sims.Length;
            }
            double worst = 0;
            Console.Write($"round {round}: depth {sims.Average(s => s.H.EverDepth):0}");
            foreach (var k in Knobs)
            {
                double got = Math.Max(1, le[(int)k.System]);
                worst = Math.Max(worst, Math.Abs(Math.Log(got / k.Target)));
                Console.Write($"  {k.System} {got:0}/{k.Target:0}");
                // Damped: the knobs interact through depth, so each moves only part of the way.
                k.Set(p, k.Get(p) * Math.Clamp(Math.Pow(k.Target / got, 0.5), 0.5, 2));
            }
            Console.WriteLine();
            if (worst < Math.Log(1.06)) break;
        }
        Console.WriteLine("Fitted:");
        foreach (var k in Knobs) Console.WriteLine($"  {k.Name} = {k.Get(p).ToString("0.####", CultureInfo.InvariantCulture)}");
    }
}
