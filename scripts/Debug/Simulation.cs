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
    {
        "shop2", "sign", "cabinet1", "picks", "shop3", "lamp", "charter", "cabinet2", "shop4", "magnet", "cabinet3", "seal",
        "warrant",
    };


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
        public int Bought, GoodBuys, Perfect, Gold20, Exhibition20, SynergyLevels30, FirstSynergy;
        public readonly int[] ReachedTier = { 1, 1, 0, 0, 0 };
        public int WonDay;
        public readonly float[] ProfitByTier = new float[5];
        public readonly int[] CustomersByTier = new int[5];
    }

    public static void Run(int campaigns)
    {
        GD.Print($"=== LootFall balance report ({campaigns} campaigns x {Days} days) ===");
        GD.Print("\nStyle                   tier 2  tier 3  tier 4  warrant  gold d20  show d20  good buys  perfect  synergy  levels d30   profit/customer at tier 1 2 3 4");
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
                     $"{100f * logs.Sum(l => l.Perfect) / bought,6:0}%  {Median(logs.Select(l => l.FirstSynergy).ToList()),7}  " +
                     $"{logs.Average(l => l.SynergyLevels30),10:0.0}   " + string.Join(" ", perTier));
        }
        GD.Print("\n(tier columns: median day the renovation was bought; '-' = fewer than half the campaigns got there in " + Days + " days;");
        GD.Print(" synergy: median day the first one reached level 1; levels d30: total levels of active synergies on day 30)");
        PrintSynergies(campaigns);
        PrintSellers();
        LessonTest();
    }

    /// <summary>For the collector: how many campaigns reached each level of each synergy in 30 days.</summary>
    static void PrintSynergies(int campaigns)
    {
        var states = Enumerable.Range(0, campaigns).Select(i => CampaignState(Styles[^1], 1000 + i)).ToList();
        GD.Print("\nSynergy (collector, day 30)   reached level 1  2  3  4  5   (% of campaigns)");
        foreach (var synergy in Synergies.All)
        {
            var reached = Enumerable.Range(1, SynergyDef.MaxLevel).Select(l => 100f * states.Count(s => synergy.Level(s) >= l) / campaigns);
            GD.Print($"{synergy.Name,-28} " + string.Join(" ", reached.Select(r => $"{r,4:0}")));
        }
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
                blind += BlindValue(visit, state, rng);
                visit = Market.CreateVisit(rng, seller, seller.Tier);
                int? paid = Deal(visit, state, rng, Care.Tools, out _);
                if (paid is not int price) continue;
                buys++;
                profit += Open(visit, state, rng, Care.Tools) - price;
            }
            GD.Print($"{seller.Name,-22} {seller.Trade,-7} {100f * buys / n,5:0}%   {profit / Math.Max(1, buys),9:0}   {profit / n,11:0}   {blind / n,14:0}");
        }
    }

    static float BlindValue(Visit visit, GameState state, Random rng)
    {
        int price = visit.Trade == Trade.Offer ? Math.Max(1, visit.Seller.Tier * 6) : visit.Ask;
        if (visit.Trade == Trade.Offer)
        {
            var response = visit.Offer(price, rng);
            if (response.Kind != ResponseKind.Accept) return 0;
        }
        return Open(visit, state, rng, Care.Blind) - price;
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
            foreach (var kind in chest.HeardAt(Shake.Hard).GroupBy(i => i.Category))
                slip.Set(kind.Key, Math.Max(slip.Count(kind.Key), 1));
        }
        else
        {
            hearing = Listen(visit, state, rng, care == Care.Tools);
            slip = hearing.Read(chest.Quality, tier);
            Strategy.OnFaith(visit, slip, hearing);
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

    /// <summary>A careful player's handling of a chest: every strength the seller allows, the magnet, the drop.</summary>
    static Hearing Listen(Visit visit, GameState state, Random rng, bool tools)
    {
        var chest = visit.Chest;
        bool lamp = tools && state.Has("lamp");
        var hearing = Hearing.Simulate(chest, Strategy.Levels(visit), lamp);
        if (tools) hearing.Listen(chest, Strategy.MagnetLevels(visit, hearing, state), lamp, magnet: true);
        if (Strategy.ShouldDrop(visit, hearing, state.Tier))
        {
            var (hollow, broke) = chest.Drop(rng, state.Sturdier);
            hearing.HeardDrop(hollow, broke);
        }
        return hearing;
    }

    /// <summary>Picks the lock (careful players are better at it) and adds up what came out, at what it sells for.</summary>
    static int Open(Visit visit, GameState state, Random rng, Care care)
    {
        var grade = PickGrade.Clean;
        if (visit.Chest.Lock.Locked)
        {
            float roll = rng.NextFloat();
            grade = care >= Care.Listens ? (roll < 0.3f ? PickGrade.Masterful : roll < 0.85f ? PickGrade.Clean : PickGrade.Forced)
                : roll < 0.6f ? PickGrade.Clean : PickGrade.Forced;
        }
        if (grade == PickGrade.Forced) visit.Chest.Force(rng, state.Sturdier);
        return visit.Chest.Everything.Sum(i => state.SaleValue(i, grade));
    }

    static Log Campaign(Style style, int seed) => Play(style, seed).Log;

    static GameState CampaignState(Style style, int seed) => Play(style, seed).State;

    static (Log Log, GameState State) Play(Style style, int seed)
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
                int value = Open(visit, state, rng, style.Care);
                if (perfect) value += state.KeenFee(value);
                state.RecordRead(perfect);
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
            if (style.Care >= Care.Listens) ChooseSynergies(state);
            if (log.FirstSynergy == 0 && Synergies.All.Any(s => s.Level(state) > 0)) log.FirstSynergy = day;
            for (int t = 2; t <= 4; t++)
                if (log.ReachedTier[t] == 0 && state.Tier >= t) log.ReachedTier[t] = day;
            if (log.WonDay == 0 && state.Won) log.WonDay = day;
            if (day == 20)
            {
                log.Gold20 = state.Gold;
                log.Exhibition20 = state.Exhibition;
            }
        }
        log.SynergyLevels30 = state.ActiveSynergies.Sum(state.SynergyLevel);
        return (log, state);
    }

    /// <summary>A careful player keeps the highest synergies active (money first, when it's a tie).</summary>
    public static void ChooseSynergies(GameState state)
    {
        var best = Synergies.All.Where(s => s.Level(state) > 0)
            .OrderByDescending(s => s.Level(state)).ThenBy(s => Array.IndexOf(Preference, s.Key)).Take(state.SynergySlots).Select(s => s.Key).ToList();
        foreach (var key in state.ActiveSynergies.ToList().Where(k => !best.Contains(k))) state.ToggleSynergy(key);
        foreach (var key in best.Where(k => !state.ActiveSynergies.Contains(k))) state.ToggleSynergy(key);
    }

    static readonly string[] Preference = { "jewelry", "keen", "smith", "arcane", "archivist", "vintner", "tailor" };

    /// <summary>
    /// Plays every seller's first-visit lesson the careful way (with the tools a player would have at that shop) and
    /// checks each step gets done, in order, by what the careful player does anyway; and that each lesson's chest has
    /// what it teaches. Prints one line per lesson.
    /// </summary>
    public static void LessonTest()
    {
        GD.Print("\nLessons (played the careful way, at the seller's own shop, with and without that shop's tools)");
        int failed = 0;
        foreach (var lesson in Lessons.All)
        foreach (bool tools in new[] { true, false })
        {
            var seller = Sellers.Get(lesson.Seller);
            var state = new GameState(3) { Guided = true };
            state.SkipTo(seller.Tier, 5000);
            if (tools)
                foreach (var key in new[] { "lamp", "magnet" })
                    if (Upgrades.Get(key).MinTier <= seller.Tier) state.BuyUpgrade(Upgrades.Get(key));
            var rng = new Random(7);
            var visit = lesson.Make(rng, state.Tier);
            visit.Lesson = lesson;
            var steps = lesson.For(state);
            var hearing = new Hearing();
            var slip = new Appraisal(visit, state.Tier);
            int step = 0;
            var check = () => new LessonCheck { Visit = visit, Slip = slip, Hearing = hearing, State = state };
            // Steps are checked every frame in the game: here, after every action. Reading a tip counts as done.
            void Advance()
            {
                while (step < steps.Count && (steps[step].Done == null || steps[step].Done(check()))) step++;
            }
            bool lamp = state.Has("lamp");
            Advance();
            foreach (var level in Strategy.Levels(visit))
            {
                hearing.Listen(visit.Chest, new[] { level }, lamp);
                Advance();
            }
            hearing.Listen(visit.Chest, Strategy.MagnetLevels(visit, hearing, state), lamp, magnet: true);
            Advance();
            if (Strategy.ShouldDrop(visit, hearing, state.Tier))
            {
                var (hollow, broke) = visit.Chest.Drop(rng);
                hearing.HeardDrop(hollow, broke);
                Advance();
            }
            var read = hearing.Read(visit.Chest.Quality, state.Tier);
            Strategy.OnFaith(visit, read, hearing);
            foreach (var kind in Categories.UpTo(state.Tier).Select(k => k.Category))
            {
                slip.Set(kind, read.Count(kind), read.Magic(kind));
                Advance();
            }
            slip.Hidden = read.Hidden;
            Advance();
            int worth = Strategy.Worth(visit, slip, hearing);
            if (visit.CanOffer)
            {
                visit.Offer(Strategy.FirstOffer(visit, worth), rng);
                Advance();
                if (visit.CanOffer) visit.Offer(Strategy.SecondOffer(worth), rng);
                Advance();
            }
            bool done = step >= steps.Count;
            if (!done) failed++;
            GD.Print($"  {seller.Name,-24}{(tools ? "tools   " : "no tools")}  {(done ? "done" : $"STUCK at step {step + 1}: {steps[step].Say}")}");
        }
        GD.Print(failed == 0 ? "  Every lesson completes." : $"  {failed} lessons get stuck.");
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
