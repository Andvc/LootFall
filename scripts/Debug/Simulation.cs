using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report: `godot --headless --path . -- --sim [--n=200]`. Plays whole campaigns with simple player
/// styles, buying upgrades between days. The design works if careful listening climbs the shop tiers at a steady pace,
/// each tool pays for itself, and careless play stalls.
/// </summary>
public static class Simulation
{
    const int Days = 30;

    /// <summary>The order a sensible player buys things in.</summary>
    public static readonly string[] BuyOrder = { "shop2", "sign", "shop3", "lamp", "shop4", "magnet", "warrant" };

    static readonly Shake[] Every = { Shake.Gentle, Shake.Firm, Shake.Hard };

    /// <summary>How a simulated player handles a chest and decides.</summary>
    sealed record Style(string Name, Func<Visit, GameState, Appraisal> Read, bool BuysTools = true, bool BuysAll = false);

    static readonly Style[] Styles =
    {
        new("buys everything", (v, _) => new Appraisal(v), BuysTools: false, BuysAll: true),
        new("trusts the seller", TrustsSeller, BuysTools: false),
        new("one hard shake", HardShake, BuysTools: false),
        new("every strength", (v, _) => Hearing.Simulate(v.Chest, Every).Read(v.Grade), BuysTools: false),
        new("every strength + tools", Careful),
    };

    /// <summary>Takes the seller's word, plus whatever a hard shake turns up.</summary>
    static Appraisal TrustsSeller(Visit visit, GameState state) => new(visit);

    static Appraisal HardShake(Visit visit, GameState state)
    {
        var slip = new Appraisal(visit);
        foreach (var item in visit.Chest.HeardAt(Shake.Hard)) slip[item.Category] = Mark.Plain;
        return slip;
    }

    /// <summary>Every strength, the witchlight always on, and a gentle shake with the magnet on.</summary>
    public static Appraisal Careful(Visit visit, GameState state)
    {
        bool lamp = state.Has("lamp"), magnet = state.Has("magnet");
        return Hearing.Simulate(visit.Chest, Every, lamp, magnet ? new[] { Shake.Gentle } : null).Read(visit.Grade);
    }

    sealed class Log
    {
        public int Bought, GoodBuys, Perfect, Gold20;
        public readonly int[] ReachedTier = { 1, 1, 0, 0, 0 };
        public int WonDay;
        public readonly float[] ProfitByTier = new float[5];
        public readonly int[] CustomersByTier = new int[5];
    }

    public static void Run(int campaigns)
    {
        GD.Print($"=== LootFall balance report ({campaigns} campaigns x {Days} days) ===");
        GD.Print("\nStyle                    tier 2  tier 3  tier 4  warrant  gold day 20  good buys  perfect   profit/customer at tier 1 2 3 4");
        foreach (var style in Styles)
        {
            var logs = Enumerable.Range(0, campaigns).Select(i => Campaign(style, 1000 + i)).ToList();
            string Reach(int tier) => Median(logs.Select(l => l.ReachedTier[tier]).ToList());
            var perTier = Enumerable.Range(1, 4).Select(t =>
            {
                int n = logs.Sum(l => l.CustomersByTier[t]);
                return n == 0 ? "   -" : $"{logs.Sum(l => l.ProfitByTier[t]) / n,4:0}";
            });
            int bought = Math.Max(1, logs.Sum(l => l.Bought));
            GD.Print($"{style.Name,-23} {Reach(2),6}  {Reach(3),6}  {Reach(4),6}  {Median(logs.Select(l => l.WonDay).ToList()),7}  " +
                     $"{logs.Average(l => l.Gold20),11:0}  {100f * logs.Sum(l => l.GoodBuys) / bought,8:0}%  {100f * logs.Sum(l => l.Perfect) / bought,6:0}%   " +
                     string.Join(" ", perTier));
        }
        GD.Print("\n(tier columns: median day the renovation was bought; '-' = fewer than half the campaigns got there in " + Days + " days)");
        PrintSellers(campaigns);
    }

    /// <summary>Per seller, for a careful player with every tool: how often a chest is worth buying, and the profit.</summary>
    static void PrintSellers(int campaigns)
    {
        GD.Print("\nSeller (careful player, all tools)   buys   profit/buy   profit/visit   missed by blind buyer");
        var rng = new Random(5);
        var state = new GameState(1) { Guided = false };
        state.SkipTo(4, 1000);
        state.BuyUpgrade(Upgrades.Get("magnet"));
        foreach (var seller in Sellers.All)
        {
            int buys = 0, n = 400;
            float profit = 0, blind = 0;
            for (int i = 0; i < n; i++)
            {
                var visit = Market.CreateVisit(rng, seller, Math.Max(seller.Tier, 1));
                var slip = Careful(visit, state);
                blind += visit.Chest.Value - visit.Ask;
                if (slip.Worth <= visit.Ask) continue;
                buys++;
                profit += visit.Chest.Value - visit.Ask;
            }
            GD.Print($"{seller.Name,-22} ({seller.ShakeWord,-6})  {100f * buys / n,5:0}%   {profit / Math.Max(1, buys),9:0}   {profit / n,11:0}   {blind / n,11:0}");
        }
    }

    static Log Campaign(Style style, int seed)
    {
        var state = new GameState(seed) { Guided = false };
        var log = new Log();
        for (int day = 1; day <= Days; day++)
        {
            state.StartDay();
            while (state.NextCustomer())
            {
                var visit = state.Current;
                int tier = state.Tier;
                log.CustomersByTier[tier]++;
                var slip = style.Read(visit, state);
                bool buy = state.Gold >= visit.Ask && (style.BuysAll || slip.Worth > visit.Ask);
                if (!buy)
                {
                    visit.Pass(state.Rng);
                    state.Pass();
                    continue;
                }
                visit.Buy(state.Rng);
                state.Buy(visit, slip);
                int value = visit.Chest.Value;
                state.Earn(value);
                log.Bought++;
                if (value > visit.PricePaid) log.GoodBuys++;
                if (slip.Check(visit.Chest).Perfect) log.Perfect++;
                log.ProfitByTier[tier] += value - visit.PricePaid;
            }
            state.EndDay();
            BuyUpgrades(state, style.BuysTools);
            for (int t = 2; t <= 4; t++)
                if (log.ReachedTier[t] == 0 && state.Tier >= t) log.ReachedTier[t] = day;
            if (log.WonDay == 0 && state.Won) log.WonDay = day;
            if (day == 20) log.Gold20 = state.Gold;
        }
        return log;
    }

    /// <summary>Buys upgrades in order while keeping some cash for tomorrow's chests.</summary>
    public static void BuyUpgrades(GameState state, bool tools = true)
    {
        foreach (var key in BuyOrder)
        {
            if (!tools && key is "lamp" or "magnet") continue;
            var upgrade = Upgrades.Get(key);
            int reserve = key == "warrant" ? 0 : 10 + 15 * state.Tier;
            if (state.Gold - upgrade.Cost >= reserve && state.CanBuy(upgrade)) state.BuyUpgrade(upgrade);
        }
    }

    static string Median(List<int> days)
    {
        var reached = days.Where(d => d > 0).OrderBy(d => d).ToList();
        if (reached.Count < days.Count / 2) return "-";
        return $"day {days.Select(d => d == 0 ? int.MaxValue : d).OrderBy(d => d).ElementAt(days.Count / 2)}";
    }
}
