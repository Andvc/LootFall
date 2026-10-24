using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LootFall.Progression;

// progsim [--hours=3000] [--profile=active|idle|daily|no-prestige] [--seed=1] [--slots=N] [--rings=N] [--debug-talents]
//   [--debug-prestige]: one player's report. Any mode takes --set=Name=value (repeatable) to change a Params field.
// progsim --check [--hours=3000] [--json=file]: the acceptance test (Checks.cs) on all four players; exits 1 if any
//   check fails. --json writes the four runs (snapshots, knockouts, prestige, gaps) and the results for charts.
// progsim --content-sweep [--hours=3000]: the check with fewer and more slots and talent rings (see Sweep below).
// progsim --tune [--hours=1000]: fits the open-ended systems' power knobs to their targets (see Tuner below).
// progsim --sweep [--hours=3000]: the check with each key parameter 20% lower and 25% higher (see Sweep below).
var arg = (string name, string fallback) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1] ?? fallback;
double hours = double.Parse(arg("hours", args.Contains("--tune") ? "1000" : "3000"), CultureInfo.InvariantCulture);
int seed = int.Parse(arg("seed", "1"), CultureInfo.InvariantCulture);
var profile = arg("profile", "active") switch
{
    "idle" => Profile.Idle(),
    "daily" => Profile.Daily(),
    "no-prestige" => Profile.NoPrestige(),
    _ => Profile.Active(),
};

// --set=Name=value: try a parameter without editing Params.cs.
Params Configured()
{
    var cp = new Params();
    foreach (var a in args.Where(a => a.StartsWith("--set=")))
    {
        var kv = a["--set=".Length..].Split('=', 2);
        var f = typeof(Params).GetField(kv[0]) ?? throw new ArgumentException($"no Params field {kv[0]}");
        f.SetValue(cp, f.FieldType == typeof(int) ? int.Parse(kv[1], CultureInfo.InvariantCulture) : double.Parse(kv[1], CultureInfo.InvariantCulture));
    }
    return cp;
}

if (args.Contains("--tune"))
{
    Tuner.Run(Configured(), hours, seed);
    return 0;
}

if (args.Contains("--sweep") || args.Contains("--content-sweep"))
{
    Sweep.Run(hours, seed, content: args.Contains("--content-sweep"));
    return 0;
}

if (args.Contains("--check"))
{
    var cp = Configured();
    var cc = new Content(cp, scale: new ContentScale { Slots = int.Parse(arg("slots", "0"), CultureInfo.InvariantCulture) });
    var ct = new TalentTree(cp, int.Parse(arg("rings", "14"), CultureInfo.InvariantCulture));
    var clock = Stopwatch.StartNew();
    var players = Sweep.Players.Select(pr => new Sim(cp, cc, ct, pr(), seed)).ToArray();
    Parallel.ForEach(players, s => s.Run(hours));
    var growth = Growth.Analyze(cp, ct);
    var results = Checks.Run(growth, players[0], players[1], players[2], players[3]);
    Console.WriteLine($"Progression check: {Report.Time(hours)} for active, idle, daily and no-prestige players (seed {seed})");
    Console.WriteLine($"  {growth}");
    foreach (var r in results) Console.WriteLine($"  {(r.Pass ? "pass" : "FAIL")}  {r.Name,-58} {r.Detail}");
    int failed = results.Count(r => !r.Pass);
    if (arg("json", null) is { } path) Export.Write(path, growth, players, results, cc, ct);
    Console.WriteLine(failed == 0 ? $"All {results.Count} checks pass ({clock.Elapsed.TotalSeconds:0}s)." : $"{failed} of {results.Count} checks FAIL ({clock.Elapsed.TotalSeconds:0}s).");
    return failed == 0 ? 0 : 1;
}

var p = Configured();
var content = new Content(p, scale: new ContentScale { Slots = int.Parse(arg("slots", "0"), CultureInfo.InvariantCulture) });
var tree = new TalentTree(p, int.Parse(arg("rings", "14"), CultureInfo.InvariantCulture));
var sim = new Sim(p, content, tree, profile, seed);
if (args.Contains("--debug-prestige")) sim.Log = new();
var watch = Stopwatch.StartNew();
sim.Run(hours);
Console.WriteLine(Report.Write(sim, content, tree));
if (sim.Log != null)
{
    foreach (var line in sim.Log) Console.WriteLine(line);
    Console.WriteLine("Longest waits for a noticeable gain (over 30 minutes, after the first hour):");
    foreach (var (h, gap) in sim.M.NoticedGaps.Where(x => x.Gap > 1800 && x.Hours >= 1))
        Console.WriteLine($"  gain at {Report.Time(h)} after {Report.Time(gap / 3600)}");
    Console.WriteLine("Longest waits for a new record (over 5% of the time played):");
    foreach (var (h, gap) in sim.M.RecordGaps.Where(x => x.Gap / 3600 > 0.05 * x.Hours && x.Hours >= 10))
        Console.WriteLine($"  record at {Report.Time(h)} after {Report.Time(gap / 3600)} ({100 * gap / 3600 / h:0}%)");
}
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
        // Transcendence outgrows ascension: it's the layer that lasts, and a transcendence for a quarter more stardust
        // only pays for wiping the ascension tree when the transcendence tree is well ahead of it.
        ("AscensionPower", Sys.Ascension, 180, p => p.AscensionPower, (p, v) => p.AscensionPower = v),
        ("TranscendencePower", Sys.Transcendence, 300, p => p.TranscendencePower, (p, v) => p.TranscendencePower = v),
        ("MasteryLE", Sys.Mastery, 200, p => p.MasteryLE, (p, v) => p.MasteryLE = v),
        ("RankLE", Sys.Ranks, 220, p => p.RankLE, (p, v) => p.RankLE = v),
        ("CodexLE", Sys.Codex, 110, p => p.CodexLE, (p, v) => p.CodexLE = v),
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

/// <summary>
/// The robustness tests. --sweep: the acceptance check with one parameter at a time 20% lower and 25% higher; a
/// framework that only works at its tuned values would fall apart here, and the table says which checks a mistuned knob
/// breaks, which is where a designer has to be careful. --content-sweep: the same with less and more content (slots,
/// talent rings), which the budgets (ReferenceSlots, TalentBudgetLE) should absorb without retuning.
/// </summary>
static class Sweep
{
    public static readonly Func<Profile>[] Players = { Profile.Active, Profile.Idle, Profile.Daily, Profile.NoPrestige };

    static readonly string[] Names =
    {
        "Premium", "MonsterHp", "MonsterAtk", "BossTime", "KillsPerDepth", "DropChance",
        "GoldGrowth", "ForgeGrowth", "ForgeStep", "XpGrowth", "LevelGrowth",
        "RarityLE", "QualityLE", "RarityFalloff", "RankLE", "RankExp", "RankCost", "MasteryLE", "MasteryCurve", "MasteryRate",
        "TalentNotableLE", "TalentMinorLE", "ParagonLE", "CodexLE",
        "AscendDepth", "AetherPerKill", "AetherExp", "AscCostExp", "AscensionPower",
        "TransAether", "StardustExp", "TransCostExp", "TransPerkCostExp", "TranscendencePower", "TransEcho",
    };

    /// <summary>Growth factors (cost or value ×g a step) are varied by their rate g - 1, which is what they mean: ×0.8
    /// on a level cost growing ×1.25 is ×1.2, not ×1.0 (a constant cost, which the stability analysis rightly calls a
    /// runaway).</summary>
    static readonly string[] Rates = { "ForgeGrowth", "LevelGrowth" };

    static void Set(Params p, string name, double factor)
    {
        if (name == "Premium")
        {
            p.PremiumHpRate *= factor;
            p.PremiumAtkRate *= factor;
            return;
        }
        var f = typeof(Params).GetField(name);
        if (f.FieldType == typeof(int)) f.SetValue(p, (int)Math.Round((int)f.GetValue(p) * factor));
        else if (Rates.Contains(name)) f.SetValue(p, 1 + ((double)f.GetValue(p) - 1) * factor);
        else f.SetValue(p, (double)f.GetValue(p) * factor);
    }

    public static void Run(double hours, int seed, bool content)
    {
        var variants = new List<(string Name, double Factor)> { ("(tuned)", 1) };
        if (content)
        {
            foreach (var slots in new[] { 60, 100, 220, 300 }) variants.Add(("slots", slots));
            foreach (var rings in new[] { 8, 11, 18, 22 }) variants.Add(("talent rings", rings));
        }
        else foreach (var n in Names) { variants.Add((n, 0.8)); variants.Add((n, 1.25)); }
        var rows = new string[variants.Count];
        var clock = Stopwatch.StartNew();
        int failedVariants = 0;
        Parallel.For(0, variants.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var (name, factor) = variants[i];
            var p = new Params();
            if (!content && factor != 1) Set(p, name, factor);
            var c = new Content(p, scale: name == "slots" ? new ContentScale { Slots = (int)factor } : null);
            var tree = name == "talent rings" ? new TalentTree(p, (int)factor) : new TalentTree(p);
            var sims = Players.Select(pr => new Sim(p, c, tree, pr(), seed)).ToArray();
            foreach (var s in sims) s.Run(hours);
            var g = Growth.Analyze(p, tree);
            var results = Checks.Run(g, sims[0], sims[1], sims[2], sims[3]);
            var fails = results.Where(r => !r.Pass).Select(r => r.Name).ToList();
            if (fails.Count > 0) Interlocked.Increment(ref failedVariants);
            double D(double h) => sims[0].M.Snaps.LastOrDefault(x => x.Hours <= h + 1e-9)?.EverDepth ?? 1;
            double exponent = Math.Log(D(hours) / D(hours / 10)) / Math.Log(10);
            string label = content ? (factor == 1 ? "(reference)" : $"{name} {factor:0}") : $"{name,-18} x{factor:0.00}";
            rows[i] = $"  {label,-24} depth {D(100),6:0} {D(1000),6:0} {D(hours),6:0}  t^{exponent:0.00} (pred {g.Alpha:0.00}, loop {g.LoopGain:0.00}, feedback {g.PrestigeFeedback:0.00})  " +
                      (fails.Count == 0 ? "all pass" : $"{fails.Count} fail: " + string.Join("; ", fails));
        });
        Console.WriteLine($"Sweep: {variants.Count} variants, {Report.Time(hours)} each (depth at 100h, 1000h, {Report.Time(hours)})");
        foreach (var r in rows) Console.WriteLine(r);
        Console.WriteLine($"{variants.Count - failedVariants} of {variants.Count} variants pass every check ({clock.Elapsed.TotalMinutes:0.0} min).");
    }
}

/// <summary>The check's runs as JSON, for charting: each player's snapshots (with knockouts where taken), prestige
/// events and longest waits, the stability analysis and the check results.</summary>
static class Export
{
    public static void Write(string path, Growth g, Sim[] players, List<Checks.Result> results, Content content, TalentTree tree)
    {
        var doc = new Dictionary<string, object>
        {
            ["content"] = new { slots = content.SlotCount, rarities = Content.RarityNames.Length, affixVariants = content.AffixVariants, talentNodes = tree.Nodes.Count },
            ["growth"] = new { g.Premium, g.ForgeSlope, g.TreeSlope, g.ParagonSlope, g.LoopGain, g.PrestigeFeedback, g.Alpha, systems = g.Systems.Select(x => new { x.System, x.Exponent }) },
            ["systems"] = Enumerable.Range(0, (int)Sys.Count).Select(i => ((Sys)i).ToString()),
            ["checks"] = results.Select(r => new { r.Name, r.Pass, r.Detail }),
            ["players"] = players.Select(s => new
            {
                profile = s.Pr.Name,
                snaps = s.M.Snaps.Select(x => new
                {
                    h = Math.Round(x.Hours, 4), ever = x.EverDepth, run = x.RunDepth, reach = Math.Round(x.Reach, 1), level = x.Level, nodes = x.Nodes,
                    paragon = x.Paragon, slots = x.Slots, rarity = x.MaxRarity, codex = x.Codex, asc = x.Ascensions, trans = x.Transcendences,
                    forge = Math.Round(x.ForgeAvg, 1), rank = Math.Round(x.RankAvg, 1), mastery = Math.Round(x.MasteryAvg, 1), power = Math.Round(x.PowerLe, 1),
                    knock = x.Knockout?.Select(k => Math.Round(k, 1)),
                    events = x.Totals,
                }),
                prestige = s.M.Prestiges.Select(x => new { h = Math.Round(x.Hours, 3), run = Math.Round(x.Seconds / 3600, 3), depth = x.Depth, kind = x.Kind.ToString() }),
                recordGaps = s.M.RecordGaps.Where(x => x.Gap > 600).Select(x => new { h = Math.Round(x.Hours, 3), gap = Math.Round(x.Gap / 3600, 3) }),
                noticedGaps = s.M.NoticedGaps.Where(x => x.Gap > 600).Select(x => new { h = Math.Round(x.Hours, 3), gap = Math.Round(x.Gap / 3600, 3) }),
            }),
        };
        System.IO.File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(doc));
        Console.WriteLine($"Wrote {path}");
    }
}
