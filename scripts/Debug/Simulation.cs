using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report: `godot --headless --path . -- --sim [--n=200]`. Plays whole campaigns with simple player
/// styles, buying upgrades between days. The design works if careful listening climbs the shop tiers at a steady pace,
/// each tool and the exhibition pay for themselves, careless play stalls, and every seller's quirk is worth putting up with.
/// </summary>
public static class Simulation
{
    const int Days = 30;

    /// <summary>The order a sensible player buys things in.</summary>
    public static readonly string[] BuyOrder =
        { "shop2", "sign", "cabinet1", "picks", "shop3", "lamp", "cabinet2", "shop4", "magnet", "cabinet3", "warrant" };


    enum Care { Blind, OneShake, Listens, Tools }

    sealed record Style(string Name, Care Care, bool Collects);

    static readonly Style[] Styles =
    {
        new("buys at their price", Care.Blind, false),
        new("one hard shake", Care.OneShake, false),
        new("every strength, drops", Care.Listens, false),
        new("+ witchlight, magnet", Care.Tools, false),
        new("+ keeps grand things", Care.Tools, true),
    };

    sealed class Log
    {
        public int Bought, GoodBuys, Perfect, Gold20, Exhibition20;
        public readonly int[] ReachedTier = { 1, 1, 0, 0, 0 };
        public int WonDay;
        public readonly float[] ProfitByTier = new float[5];
        public readonly int[] CustomersByTier = new int[5];
    }

    public static void Run(int campaigns)
    {
        GD.Print($"=== LootFall balance report ({campaigns} campaigns x {Days} days) ===");
        GD.Print("\nStyle                   tier 2  tier 3  tier 4  warrant  gold d20  show d20  good buys  perfect   profit/customer at tier 1 2 3 4");
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
            GD.Print($"{style.Name,-22} {Reach(2),6}  {Reach(3),6}  {Reach(4),6}  {Median(logs.Select(l => l.WonDay).ToList()),7}  " +
                     $"{logs.Average(l => l.Gold20),8:0}  {logs.Average(l => l.Exhibition20),8:0}  {100f * logs.Sum(l => l.GoodBuys) / bought,8:0}%  " +
                     $"{100f * logs.Sum(l => l.Perfect) / bought,6:0}%   " + string.Join(" ", perTier));
        }
        GD.Print("\n(tier columns: median day the renovation was bought; '-' = fewer than half the campaigns got there in " + Days + " days)");
        PrintSellers();
    }

    /// <summary>Per seller, at their own shop: how often a careful player with every tool buys, and the profit, against buying blind.</summary>
    static void PrintSellers()
    {
        GD.Print("\nSeller                 trade    buys   profit/buy   profit/visit   blind buyer/visit");
        var rng = new Random(5);
        foreach (var seller in Sellers.All)
        {
            var state = new GameState(1) { Guided = false };
            state.SkipTo(seller.Tier, 100000);
            if (seller.Tier >= 3) state.BuyUpgrade(Upgrades.Get("lamp"));
            if (seller.Tier >= 4) state.BuyUpgrade(Upgrades.Get("magnet"));
            int buys = 0, n = 2000;
            float profit = 0, blind = 0;
            for (int i = 0; i < n; i++)
            {
                var visit = Market.CreateVisit(rng, seller, seller.Tier);
                blind += BlindValue(visit, rng);
                visit = Market.CreateVisit(rng, seller, seller.Tier);
                int? paid = Deal(visit, state, rng, Care.Tools, out _);
                if (paid is not int price) continue;
                buys++;
                profit += Open(visit, rng, Care.Tools) - price;
            }
            GD.Print($"{seller.Name,-22} {seller.Trade,-7} {100f * buys / n,5:0}%   {profit / Math.Max(1, buys),9:0}   {profit / n,11:0}   {blind / n,14:0}");
        }
    }

    static float BlindValue(Visit visit, Random rng)
    {
        int price = visit.Trade == Trade.Offer ? Math.Max(1, visit.Seller.Tier * 6) : visit.Ask;
        if (visit.Trade == Trade.Offer)
        {
            var response = visit.Offer(price, rng);
            if (response.Kind != ResponseKind.Accept) return 0;
        }
        return Open(visit, rng, Care.Blind) - price;
    }

    /// <summary>Handles the chest and trades for it in this style. Returns the price paid, or null if no deal.</summary>
    static int? Deal(Visit visit, GameState state, Random rng, Care care, out Appraisal slip)
    {
        int tier = state.Tier;
        var chest = visit.Chest;
        if (care == Care.Blind)
        {
            slip = new Appraisal(visit, tier);
            int blindPrice = visit.Trade == Trade.Offer ? tier * 6 : visit.Ask;
            if (state.Gold < blindPrice) return null;
            if (visit.Trade != Trade.Offer) return Paid(visit, rng);
            return visit.Offer(blindPrice, rng).Kind == ResponseKind.Accept ? blindPrice : null;
        }

        Hearing hearing;
        if (care == Care.OneShake)
        {
            hearing = Hearing.Simulate(chest, new[] { Shake.Hard });
            if (visit.Seller.HatesHardShake) visit.Anger(rng);
            slip = new Appraisal(visit, tier);
            foreach (var item in chest.HeardAt(Shake.Hard)) slip[item.Category] = Mark.Plain;
        }
        else
        {
            bool lamp = care == Care.Tools && state.Has("lamp");
            var levels = Strategy.Levels(visit);
            hearing = Hearing.Simulate(chest, levels, lamp);
            if (care == Care.Tools && Strategy.ShouldUseMagnet(visit, hearing, state))
                hearing = Hearing.Simulate(chest, levels, lamp, new[] { Shake.Gentle });
            if (Strategy.ShouldDrop(visit, hearing, tier))
            {
                var (hollow, broke) = chest.Drop(rng);
                hearing.HeardDrop(hollow, broke.Select(i => i.Category));
            }
            slip = hearing.Read(chest.Quality, tier);
        }

        int worth = Strategy.Worth(visit, slip, hearing);
        switch (visit.Trade)
        {
            case Trade.Fixed:
                return worth > visit.Ask && state.Gold >= visit.Ask ? Paid(visit, rng) : null;
            case Trade.Haggle:
            {
                int offer = Math.Min(state.Gold, Strategy.FirstOffer(visit, worth));
                if (offer <= 0 || worth <= offer) return null;
                var response = visit.Offer(offer, rng);
                if (response.Kind == ResponseKind.Accept) return response.Price;
                if (response.Kind == ResponseKind.Counter && worth > visit.Ask && state.Gold >= visit.Ask) return Paid(visit, rng);
                return null;
            }
            default:
            {
                if (worth <= 1) return null;
                var response = visit.Offer(Math.Min(state.Gold, Strategy.FirstOffer(visit, worth)), rng);
                if (response.Kind == ResponseKind.Accept) return response.Price;
                if (response.Kind != ResponseKind.TryAgain) return null;
                response = visit.Offer(Math.Min(state.Gold, Strategy.SecondOffer(worth)), rng);
                return response.Kind == ResponseKind.Accept ? response.Price : null;
            }
        }
    }

    static int Paid(Visit visit, Random rng)
    {
        visit.Buy(rng);
        return visit.PricePaid;
    }

    /// <summary>Picks the lock (careful players are better at it) and adds up what came out.</summary>
    static int Open(Visit visit, Random rng, Care care)
    {
        var grade = PickGrade.Clean;
        if (visit.Chest.Lock.Locked)
        {
            float roll = rng.NextFloat();
            grade = care >= Care.Listens ? (roll < 0.3f ? PickGrade.Masterful : roll < 0.85f ? PickGrade.Clean : PickGrade.Forced)
                : roll < 0.6f ? PickGrade.Clean : PickGrade.Forced;
        }
        if (grade == PickGrade.Forced) visit.Chest.Force();
        return visit.Chest.Everything.Sum(i => (int)MathF.Round(i.Value * Picks.Multiplier(grade)));
    }

    static Log Campaign(Style style, int seed)
    {
        var state = new GameState(seed) { Guided = false };
        var log = new Log();
        var rng = new Random(seed);
        for (int day = 1; day <= Days; day++)
        {
            state.StartDay();
            while (state.NextCustomer())
            {
                var visit = state.Current;
                int tier = state.Tier;
                log.CustomersByTier[tier]++;
                int? paid = Deal(visit, state, rng, style.Care, out var slip);
                if (paid is not int price)
                {
                    if (visit.Deciding) visit.Pass(rng);
                    state.Pass();
                    continue;
                }
                state.Buy(visit, slip);
                bool perfect = slip.Check(visit.Chest).Perfect;
                int value = Open(visit, rng, style.Care);
                state.Earn(value);
                log.Bought++;
                if (value > price) log.GoodBuys++;
                if (perfect) log.Perfect++;
                log.ProfitByTier[tier] += value - price;
                foreach (var item in visit.Chest.Everything)
                {
                    state.Collect(item);
                    if (!style.Collects) continue;
                    var (keep, replace) = Strategy.ShouldKeep(state, item, item.Value);
                    if (!keep) continue;
                    if (replace != null) state.SellFromDisplay(replace);
                    state.Keep(item, item.Value);
                }
            }
            state.EndDay();
            BuyUpgrades(state, style.Care == Care.Tools, style.Collects);
            for (int t = 2; t <= 4; t++)
                if (log.ReachedTier[t] == 0 && state.Tier >= t) log.ReachedTier[t] = day;
            if (log.WonDay == 0 && state.Won) log.WonDay = day;
            if (day == 20)
            {
                log.Gold20 = state.Gold;
                log.Exhibition20 = state.Exhibition;
            }
        }
        return log;
    }

    /// <summary>Buys upgrades in order while keeping some cash for tomorrow's chests.</summary>
    public static void BuyUpgrades(GameState state, bool tools = true, bool cabinets = true)
    {
        foreach (var key in BuyOrder)
        {
            if (!tools && key is "lamp" or "magnet") continue;
            if (!cabinets && key.StartsWith("cabinet")) continue;
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
