using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report: `godot --headless --path . -- --sim [--n=200]`. Plays whole campaigns with simple player
/// styles, buying upgrades between days. The design works if careful play climbs the shop tiers at a steady pace,
/// every tool pays for itself, and mindless play stalls.
/// </summary>
public static class Simulation
{
    const int Days = 40;

    /// <summary>The order a sensible player buys things in.</summary>
    public static readonly string[] BuyOrder =
        { "scale", "shop2", "peephole", "trumpet", "sign", "shop3", "lantern", "compass", "picks", "shop4", "periscope", "lodestone", "hammer", "warrant" };

    sealed record Style(string Name, bool Inspects, bool UsesTools, bool PracticedEar, bool Upgrades, bool PaysAsk = false);

    static readonly Style[] Styles =
    {
        new("pays the ask", false, false, false, true, PaysAsk: true),
        new("haggles blind", false, false, false, true),
        new("listens, no tools", true, false, false, true),
        new("listens + tools", true, true, false, true),
        new("practiced ear + tools", true, true, true, true),
    };

    sealed class Log
    {
        public int Customers, Bought, GoodBuys, ConsBought;
        public readonly int[] ReachedTier = { 1, 0, 0, 0, 0 };
        public int WonDay;
        public readonly float[] ProfitByTier = new float[5];
        public readonly int[] CustomersByTier = new int[5];
    }

    public static void Run(int campaigns)
    {
        GD.Print($"=== LootFall balance report ({campaigns} campaigns x {Days} days) ===");
        PrintSellers();

        GD.Print("\nStyle                   tier 2   tier 3   tier 4    warrant   gold day 20   good buys   cons   profit/customer at tier 1 2 3 4");
        foreach (var style in Styles)
        {
            var logs = Enumerable.Range(0, campaigns).Select(i => Campaign(style, 1000 + i, out _)).ToList();
            string Reach(int tier) => Median(logs.Select(l => l.ReachedTier[tier]).ToList());
            var perTier = Enumerable.Range(1, 4).Select(t =>
            {
                int n = logs.Sum(l => l.CustomersByTier[t]);
                return n == 0 ? "   -" : $"{logs.Sum(l => l.ProfitByTier[t]) / n,4:0}";
            });
            GD.Print($"{style.Name,-22} {Reach(2),7}  {Reach(3),7}  {Reach(4),7}  {Median(logs.Select(l => l.WonDay).ToList()),9}  " +
                     $"{logs.Average(l => GoldOnDay20(l)),12:0}  {100f * logs.Sum(l => l.GoodBuys) / Math.Max(1, logs.Sum(l => l.Bought)),9:0}%  " +
                     $"{logs.Average(l => l.ConsBought),5:0.0}   {string.Join(" ", perTier)}");
        }
        GD.Print("\n(tier columns: median day the renovation was bought; '-' = fewer than half the campaigns got there in " + Days + " days)");
    }

    static readonly Dictionary<Log, int> Day20Gold = new();
    static int GoldOnDay20(Log log) => Day20Gold.TryGetValue(log, out var g) ? g : 0;

    static Log Campaign(Style style, int seed, out GameState state)
    {
        state = new GameState(seed) { Guided = false };
        var log = new Log();
        var rng = new Random(seed);
        for (int day = 1; day <= Days; day++)
        {
            state.StartDay();
            while (state.NextCustomer())
            {
                var visit = state.Current;
                int tier = state.Tier;
                log.Customers++;
                log.CustomersByTier[tier]++;
                Decide(state, visit, style);
                if (visit.Outcome != VisitOutcome.Bought)
                {
                    state.Pass();
                    continue;
                }
                var purchase = state.Buy(visit);
                log.Bought++;
                if (visit.Chest.Tampered) log.ConsBought++;
                var grade = !visit.Chest.Lock.Locked ? Core.PickGrade.Clean : RollGrade(rng, state.Has("picks"));
                var haul = Loot.Resolve(visit.Chest, grade);
                int value = haul.Sum(i => i.Value);
                state.Earn(value);
                foreach (var item in haul) state.Ledger.Learn(item.Def);
                if (value > purchase.Paid) log.GoodBuys++;
                log.ProfitByTier[tier] += value - purchase.Paid;
            }
            state.EndDay();
            if (day == 20) Day20Gold[log] = state.Gold;

            if (!style.Upgrades) continue;
            foreach (var key in BuyOrder)
            {
                var upgrade = Upgrades.Get(key);
                bool tool = !upgrade.IsRenovation && key != "sign" && key != "warrant";
                if (tool && !style.UsesTools) continue;
                int reserve = 20 + 15 * state.Tier;
                if (state.Gold - upgrade.Cost < reserve && key != "warrant") continue;
                if (!state.CanBuy(upgrade)) continue;
                state.BuyUpgrade(upgrade);
                if (upgrade.IsRenovation) log.ReachedTier[upgrade.SetsTier] = day;
                if (key == "warrant") log.WonDay = day;
            }
            if (state.Won) break;
        }
        return log;
    }

    static PickGrade RollGrade(Random rng, bool finePicks)
    {
        float r = rng.NextFloat();
        float masterful = finePicks ? 0.3f : 0.15f, forced = finePicks ? 0.05f : 0.12f;
        return r < masterful ? Core.PickGrade.Masterful : r < 1 - forced ? Core.PickGrade.Clean : Core.PickGrade.Forced;
    }

    /// <summary>Inspects with whatever the style uses, then buys, haggles or passes.</summary>
    static void Decide(GameState s, Visit v, Style style)
    {
        if (style.PaysAsk)
        {
            if (s.Gold >= v.Ask) v.Buy(s.Rng);
            else v.Pass(s.Rng);
            return;
        }

        float? estimate;
        if (!style.Inspects)
        {
            estimate = Appraiser.For(v.Seller).MeanValue;
        }
        else
        {
            var seen = Inspect(s, v, style);
            estimate = Estimator.Estimate(v.Chest, v.Seller, seen, s.Ledger);
        }
        if (estimate is not float worth || s.Gold < 5)
        {
            v.Pass(s.Rng);
            return;
        }

        // One offer at a comfortable margin, then take the final price only if it still leaves profit.
        if (v.CanOffer && s.CanHaggle)
        {
            int offer = Math.Min(s.Gold, Money.RoundTo5(Math.Min(worth * 0.7f, v.Ask * 0.85f)));
            if (offer < v.Ask)
            {
                v.Offer(offer, s.Rng);
                if (v.Outcome != VisitOutcome.Deciding) return;
            }
        }
        if (v.Ask <= worth * 0.95f && s.Gold >= v.Ask) v.Buy(s.Rng);
        else v.Pass(s.Rng);
    }

    static Inspection Inspect(GameState s, Visit v, Style style)
    {
        var seen = new Inspection { Variants = style.PracticedEar || (style.UsesTools && s.Has("trumpet")) };
        if (!style.UsesTools) return seen;
        var chest = v.Chest;
        if (s.Has("scale")) seen.Weight = chest.ContentsWeight;
        if (s.Has("compass")) seen.Magic = chest.Magic;
        if (s.Has("lodestone")) seen.Iron = chest.IronWeight;
        if (s.Has("hammer")) seen.Hollow = chest.FalseBottom != null;
        // Peek when the ear can't settle it: cloth and loose paper, or when it smells like a con.
        bool worthAPeek = chest.Items.Any(i => i.Def.Family is Family.Cloth or Family.Paper) || v.Seller.Rule == PriceRule.Con;
        if (worthAPeek && s.UsePeek())
            foreach (var item in chest.Items.OrderBy(_ => s.Rng.Next()).Take(s.PeekReach))
                seen.Seen.Add(item);
        return seen;
    }

    static void PrintSellers()
    {
        var rng = new Random(42);
        GD.Print("\nSeller               tier  asks/value  final/value  value p50  worth more than ask");
        foreach (var seller in Sellers.All)
        {
            var visits = Enumerable.Range(0, 1500).Select(_ => Market.CreateVisit(rng, seller, 4)).ToList();
            float Ratio(Func<Visit, float> f) => MedianF(visits.Select(f).ToList());
            GD.Print($"{seller.Name,-20} {seller.Tier,4}  {Ratio(v => v.OpeningAsk / (float)Math.Max(1, v.Chest.Value)),9:0.00}x  " +
                     $"{Ratio(v => v.Bottom / (float)Math.Max(1, v.Chest.Value)),10:0.00}x  {MedianF(visits.Select(v => (float)v.Chest.Value).ToList()),8:0}g  " +
                     $"{100f * visits.Count(v => v.Chest.Value > v.OpeningAsk) / visits.Count,17:0}%");
        }
    }

    /// <summary>Median day something happened, counting campaigns where it never did as "never".</summary>
    static string Median(List<int> days)
    {
        var sorted = days.Select(d => d > 0 ? d : int.MaxValue).OrderBy(d => d).ToList();
        int middle = sorted[sorted.Count / 2];
        return middle == int.MaxValue ? "-" : $"day {middle}";
    }

    static float MedianF(List<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }
}
