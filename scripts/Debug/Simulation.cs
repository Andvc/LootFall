using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report: run with `godot --headless --path . -- --sim [--n=2000]`.
/// Checks that clues narrow the estimate without making it exact, and that careful play is profitable.
/// </summary>
public static class Simulation
{
    static readonly (string Name, Clues Clues)[] ClueSets =
    {
        ("none", new Clues()),
        ("shake", new Clues(true, false, false)),
        ("weigh", new Clues(false, true, false)),
        ("lock", new Clues(false, false, true)),
        ("shake+weigh", new Clues(true, true, false)),
        ("shake+lock", new Clues(true, false, true)),
        ("weigh+lock", new Clues(false, true, true)),
    };

    public static void Run(int lotCount)
    {
        var rng = new Random(42);
        var appraisers = Provenances.All.ToDictionary(p => p, p => new Appraiser(p, 7));
        var lots = new List<(Chest Chest, Appraiser Appraiser)>();
        for (int i = 0; i < lotCount; i++)
        {
            var provenance = rng.PickOne(Provenances.All);
            lots.Add((ChestGenerator.Generate(rng, provenance), appraisers[provenance]));
        }

        GD.Print($"=== LootFall balance report ({lotCount} lots) ===");
        PrintDistribution(lots.Select(l => l.Chest).ToList());
        PrintCalibration(lots);
        PrintStrategies(lots, rng);
    }

    static void PrintDistribution(List<Chest> chests)
    {
        var values = chests.Select(c => c.Value).OrderBy(v => v).ToArray();
        GD.Print($"\nChest value  p10={P(values, .1f)}  p50={P(values, .5f)}  p90={P(values, .9f)}  " +
                 $"mean={values.Average():0}  max={values[^1]}");

        var tops = chests.GroupBy(c => c.Items.Max(i => i.Def.Rarity)).OrderBy(g => g.Key)
            .Select(g => $"{g.Key}={100f * g.Count() / chests.Count:0.0}%");
        GD.Print("Best item    " + string.Join("  ", tops));

        var locks = chests.GroupBy(c => c.Lock.Name).OrderBy(g => g.First().Lock.Index)
            .Select(g => $"{g.Key}={100f * g.Count() / chests.Count:0}% (median {P(g.Select(c => c.Value).OrderBy(v => v).ToArray(), .5f)}g)");
        GD.Print("Locks        " + string.Join("  ", locks));
        GD.Print($"False bottom {100f * chests.Count(c => c.FalseBottom != null) / chests.Count:0.0}%   " +
                 $"Fragile {100f * chests.Count(c => c.HasFragile) / chests.Count:0}%");
    }

    static void PrintCalibration(List<(Chest Chest, Appraiser Appraiser)> lots)
    {
        GD.Print("\nClues        in-range  width/median  median-error");
        foreach (var (name, clues) in ClueSets)
        {
            int hits = 0;
            var widths = new List<float>();
            var errors = new List<float>();
            foreach (var (chest, appraiser) in lots)
            {
                var e = appraiser.Estimate(chest, clues);
                if (chest.Value >= e.Low && chest.Value <= e.High) hits++;
                widths.Add((e.High - e.Low) / (float)Math.Max(1, e.Median));
                errors.Add(MathF.Abs(MathF.Log((chest.Value + 1f) / (e.Median + 1f))));
            }
            GD.Print($"{name,-12} {100f * hits / lots.Count,6:0}%  {Median(widths),10:0.00}x  " +
                     $"{(MathF.Exp(Median(errors)) - 1) * 100,10:0}%");
        }
    }

    /// <summary>Player inspects (shake+weigh), then bids up to k times the estimated median.</summary>
    static void PrintStrategies(List<(Chest Chest, Appraiser Appraiser)> lots, Random rng)
    {
        GD.Print("\nStrategy (shake+weigh, clean pick)  win%  avg profit/lot  avg profit/win");
        var clues = new Clues(true, true, false);
        foreach (float k in new[] { 0.5f, 0.7f, 0.9f, 1.1f, 1.3f })
        {
            int wins = 0;
            long profit = 0;
            foreach (var (chest, appraiser) in lots)
            {
                var prior = appraiser.Prior;
                var rival = Rivals.Create(rng, chest, appraiser);
                int playerMax = (int)(appraiser.Estimate(chest, clues).Median * k);
                int? price = SimulateAuction(Auction.OpeningPrice(prior), playerMax, rival.MaxBid);
                if (price is not int paid) continue;
                wins++;
                profit += chest.Value + (chest.FalseBottom?.Value ?? 0) - paid;
            }
            GD.Print($"bid to {k:0.0}x median {100f * wins / lots.Count,17:0}%  {profit / (float)lots.Count,14:0}g  " +
                     $"{(wins > 0 ? profit / (float)wins : 0),14:0}g");
        }
    }

    /// <summary>Returns the price the player pays, or null if they don't win the lot.</summary>
    static int? SimulateAuction(int opening, int playerMax, int rivalMax)
    {
        if (playerMax < opening) return null;
        int price = opening;
        bool playerLeads = true;
        while (true)
        {
            int next = price + Auction.Increment(price);
            int challengerMax = playerLeads ? rivalMax : playerMax;
            if (next > challengerMax) break;
            price = next;
            playerLeads = !playerLeads;
        }
        return playerLeads ? price : null;
    }

    /// <summary>Prints seeds whose first lot makes a good demo: rich, varied, and winnable.</summary>
    public static void FindDemoSeeds()
    {
        int found = 0;
        for (int seed = 1; seed < 5000 && found < 12; seed++)
        {
            var state = new GameState(seed);
            state.NextLot();
            var c = state.Chest;
            bool rich = c.Items.Max(i => i.Def.Rarity) >= Rarity.Epic;
            bool varied = c.Items.Count >= 4 && c.Items.Any(i => i.Def.Sound == Sound.Jingle);
            bool winnable = state.Rival.MaxBid + Auction.Increment(state.Rival.MaxBid) < GameState.StartingGold;
            if (!rich || !varied || !winnable) continue;
            found++;
            GD.Print($"seed={seed}  {c.Provenance.Name}, {c.Lock.Name}, value={c.Value}g, fb={(c.FalseBottom != null)}, " +
                     $"rival={state.Rival.Def.Name} max={state.Rival.MaxBid}  items: " +
                     string.Join(", ", c.Items.Select(i => $"{i.Def.Name} ({i.Def.Rarity})")));
        }
    }

    static int P(int[] sorted, float p) => Appraiser.Percentile(sorted, p);

    static float Median(List<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }
}
