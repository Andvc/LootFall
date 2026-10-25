using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LootFall.Progression;

/// <summary>
/// progsim --anatomy: what the equipment anatomy holds (cells, boards, pieces by level, kind and tier, when they open,
/// their sizes) and a check of the loadout search: on random small problems drawn from the anatomy (a region's or a
/// board's pieces, hosts with their boards' pieces) it must find exactly the best set a brute force finds, and on the
/// whole anatomy it must be quick.
/// </summary>
static class AnatomyReport
{
    public static int Run(int seed, double play = 0)
    {
        var p = new Params();
        var clock = Stopwatch.StartNew();
        var c = new Content(p);
        Console.WriteLine($"Content built in {clock.ElapsedMilliseconds} ms");
        var a = c.Anatomy;
        Console.WriteLine($"Cells: {a.Cells.Count} ({a.Cells.Count(x => x.Board == 0)} on the body, {a.Cells.Count(x => x.Board != 0)} on {c.Boards.Length - 1} boards)");
        Console.WriteLine($"Pieces: {c.SlotCount}: " + string.Join(", ", c.Slots.GroupBy(s => s.Level).OrderBy(g => g.Key).Select(g => $"level {g.Key}: {g.Count()}")));
        Console.WriteLine("  by kind: " + string.Join(", ", c.Slots.GroupBy(s => s.Kind).Select(g => $"{g.Key} {g.Count()}")));
        Console.WriteLine("  by region: " + string.Join(", ", c.Slots.Where(s => s.Region != null).GroupBy(s => s.Region).Select(g => $"{g.Key} {g.Count()}")));
        Console.WriteLine($"  families: {c.Slots.Select(s => s.Family).Distinct().Count()}, prestige: {c.Slots.Count(s => s.Prestige)}");
        foreach (int d in new[] { 1, 10, 50, 100, 300, 1000, 3000, 6000, 12000 })
            Console.WriteLine($"  open by depth {d,5}: {c.Slots.Count(s => !s.Prestige && s.UnlockDepth <= d),4}");
        Console.WriteLine("  the first 30: " + string.Join(", ", c.Slots.Take(30).Select(s => $"{s.Name} ({s.UnlockDepth})")));
        var sizes = c.Slots.Select(s => s.Size).OrderBy(x => x).ToArray();
        Console.WriteLine($"  sizes: min {sizes[0]:0.000}, median {sizes[sizes.Length / 2]:0.000}, max {sizes[^1]:0.000}");
        var maxConflicts = c.Slots.OrderByDescending(s => s.Conflicts.Length).First();
        Console.WriteLine($"  most conflicts: {maxConflicts.Name} ({maxConflicts.Conflicts.Length})");

        // The largest set worn at once (by size).
        var lo = new Loadout(c);
        var all = c.Slots.Select(_ => true).ToArray();
        var chosen = new bool[c.SlotCount];
        double full = lo.Solve(c.Slots.Select(s => s.Size).ToArray(), all, chosen);
        Console.WriteLine($"Largest set: {chosen.Count(x => x)} pieces, size {full:0.0} (budget {p.ReferenceSlots}), {lo.Nodes} search nodes");

        // Exactness against brute force.
        var rng = new Random(seed);
        int trials = 300, wrong = 0;
        for (int t = 0; t < trials; t++)
        {
            var cand = new bool[c.SlotCount];
            var value = new double[c.SlotCount];
            var picked = Draw(c, rng, 10 + rng.Next(9));
            foreach (int s in picked)
            {
                cand[s] = true;
                value[s] = rng.NextDouble() < 0.15 ? -rng.NextDouble() : rng.NextDouble() * (1 + c.Slots[s].Size);
            }
            double got = lo.Solve(value, cand, new bool[c.SlotCount]);
            double want = Brute(c, picked, value);
            if (Math.Abs(got - want) > 1e-9)
            {
                wrong++;
                if (wrong <= 5) Console.WriteLine($"  MISMATCH: search {got:0.0000}, brute force {want:0.0000} on " + string.Join(", ", picked.Select(s => c.Slots[s].Name)));
            }
        }
        Console.WriteLine($"Exactness: {trials - wrong} of {trials} random problems match the brute force");

        // Speed on the whole anatomy.
        clock.Restart();
        int runs = 200, maxNodes = 0, fell = 0;
        for (int r = 0; r < runs; r++)
        {
            var value = c.Slots.Select(s => rng.NextDouble() * s.Size * Math.Pow(4, rng.Next(6))).ToArray();
            lo.Solve(value, all, chosen);
            maxNodes = Math.Max(maxNodes, lo.Nodes);
            if (lo.FellBack) fell++;
        }
        Console.WriteLine($"Speed: {clock.Elapsed.TotalMilliseconds / runs:0.00} ms a search over all {c.SlotCount} pieces, at most {maxNodes} nodes, {fell} fell back");
        if (play > 0) Worn(play, seed);
        return wrong == 0 && fell == 0 ? 0 : 1;
    }

    /// <summary>Why the first chest's pieces do or don't go on: values, weights and the objective before and after.</summary>
    public static void DebugFirst(int seed)
    {
        var s = LootFall.Game.Session.NewGame(seed);
        var h = s.H;
        var chest = s.Pile.Next();
        s.S.UpdateWeights();
        Console.WriteLine($"weights: " + string.Join(", ", Enumerable.Range(0, Stats.Count).Where(i => s.S.Weights[i] != 0).Select(i => $"{(Stat)i}={s.S.Weights[i]:G3}")));
        Console.WriteLine($"reach {h.Reach():0.000}, objective {s.S.Objective(h.Base):0.000}, total dmg {h.Total[Stat.FlatDamage]:G4} hp {h.Total[Stat.FlatHp]:G4}");
        s.Open(chest);
        for (int i = 0; i < s.C.SlotCount; i++)
            if (h.Gear[i] != null) Console.WriteLine($"  {s.C.Slots[i].Name}: worth {s.S.Worth(i):G4}, worn {h.Worn[i]}, active {h.Active[i]}");
        Console.WriteLine($"after open: reach {h.Reach():0.000}, objective {s.S.Objective(h.Base):0.000}");
        for (int i = 0; i < s.C.SlotCount; i++) if (h.Gear[i] != null) h.Worn[i] = true;
        h.Rebuild();
        Console.WriteLine($"all worn: reach {h.Reach():0.000}, objective {s.S.Objective(h.Base):0.000}, total dmg {h.Total[Stat.FlatDamage]:G4} hp {h.Total[Stat.FlatHp]:G4}");
    }

    /// <summary>A sensible player's game after some hours: what's worn, region by region, and what's kept but not worn.</summary>
    static void Worn(double hours, int seed)
    {
        var s = LootFall.Game.Session.NewGame(seed);
        LootFall.Game.SensiblePlayer.Play(s, hours * 3600);
        var h = s.H;
        var c = s.C;
        Console.WriteLine($"After {hours}h: depth {h.RunDepth}, {h.Active.Count(x => x)} pieces worn, {Enumerable.Range(0, c.SlotCount).Count(i => h.Open[i] && h.Gear[i] != null && !h.Active[i])} kept but not worn, {h.OpenCount} open");
        foreach (var g in Enumerable.Range(0, c.SlotCount).Where(i => h.Active[i]).GroupBy(i => LootFall.Game.Describe.Region(c, c.Slots[i])))
            Console.WriteLine($"  {g.Key,-11} " + string.Join(", ", g.Select(i => $"{c.Slots[i].Name} ({Content.RarityNames[h.Gear[i].Rarity][..3]})")));
        var kept = Enumerable.Range(0, c.SlotCount).Where(i => h.Open[i] && h.Gear[i] != null && !h.Active[i]).ToList();
        Console.WriteLine("  kept, not worn: " + string.Join(", ", kept.Take(40).Select(i =>
        {
            var blocker = c.Slots[i].Conflicts.FirstOrDefault(x => h.Worn[x], -1);
            return $"{c.Slots[i].Name} ({Content.RarityNames[h.Gear[i].Rarity][..3]}{(blocker >= 0 ? ", by " + c.Slots[blocker].Name : "")})";
        })));
    }

    /// <summary>A small problem with dense conflicts: pieces from one region or board, and a few hosts with their boards.</summary>
    static List<int> Draw(Content c, Random rng, int n)
    {
        var pool = new List<int>();
        var regions = c.Slots.Where(s => s.Region != null).Select(s => s.Region).Distinct().ToList();
        string region = regions[rng.Next(regions.Count)];
        pool.AddRange(c.Slots.Where(s => s.Region == region).Select(s => s.Index));
        // Some hosts of boards and their boards' pieces.
        foreach (var b in c.Boards.Skip(1).OrderBy(_ => rng.Next()).Take(2))
        {
            pool.AddRange(b.Hosts);
            pool.AddRange(b.Slots);
            foreach (int s in b.Slots)
                foreach (var (sub, _) in c.Slots[s].Brings) pool.AddRange(c.Boards[sub].Slots);
        }
        return pool.Distinct().OrderBy(_ => rng.Next()).Take(n).ToList();
    }

    static double Brute(Content c, List<int> picked, double[] value)
    {
        int n = picked.Count;
        double best = 0;
        for (int mask = 1; mask < 1 << n; mask++)
        {
            var set = new List<int>();
            for (int i = 0; i < n; i++)
                if ((mask >> i & 1) != 0) set.Add(picked[i]);
            bool ok = true;
            for (int i = 0; i < set.Count && ok; i++)
                for (int j = i + 1; j < set.Count && ok; j++)
                    if (c.Slots[set[i]].Board == c.Slots[set[j]].Board && c.Slots[set[i]].Conflicts.Contains(set[j])) ok = false;
            // Every piece on a board needs a host in the set that opens its cells.
            foreach (int s in set)
            {
                if (!ok) break;
                var d = c.Slots[s];
                if (d.Board == 0) continue;
                ok = set.Any(h => c.Boards[d.Board].Opened(h, d));
            }
            if (!ok) continue;
            double v = set.Sum(s => value[s]);
            if (v > best) best = v;
        }
        return best;
    }
}
