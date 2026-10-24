using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using LootFall.Progression;

// progsim [--hours=3000] [--profile=active|idle|no-prestige] [--seed=1]
var arg = (string name, string fallback) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1] ?? fallback;
double hours = double.Parse(arg("hours", "3000"), CultureInfo.InvariantCulture);
int seed = int.Parse(arg("seed", "1"), CultureInfo.InvariantCulture);
var profile = arg("profile", "active") switch
{
    "idle" => Profile.Idle(),
    "no-prestige" => Profile.NoPrestige(),
    _ => Profile.Active(),
};

var p = new Params();
var content = new Content(p);
var tree = new TalentTree(p);
var sim = new Sim(p, content, tree, profile, seed);
var watch = Stopwatch.StartNew();
sim.Run(hours);
Console.WriteLine(Report.Write(sim, content, tree));
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
