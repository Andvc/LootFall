using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report: `godot --headless --path . -- --sim [--n=200]`. Plays whole runs with simple player styles:
/// buying chests, curios and upgrades, filling commissions, paying the Guild each week (pawning when short) until the
/// shop is foreclosed or <see cref="MaxWeeks"/> weeks have gone. The design works if careful listening usually clears
/// the debt (with the map, now and then the vault), careless play is foreclosed within a few weeks, and every seller's
/// quirk is worth putting up with.
/// </summary>
public static class Simulation
{
    /// <summary>The debt's eight weeks, then a few of the Guild's endless ones.</summary>
    const int MaxWeeks = Guild.Weeks + 4;

    const int Days = MaxWeeks * Guild.DaysPerWeek;

    /// <summary>The order a sensible player buys things in.</summary>
    public static readonly string[] BuyOrder =
    {
        "shop2", "sign", "cabinet1", "picks", "shop3", "lamp", "charter", "case", "cabinet2", "shop4", "magnet", "cabinet3", "seal",
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
        public int Bought, GoodBuys, Perfect, FirstSynergy;
        public readonly int[] ReachedTier = { 1, 1, 0, 0, 0 };

        /// <summary>Dues paid; the week the shop was lost (0 if never); the day the debt was cleared and the vault opened.</summary>
        public int WeeksPaid, LostWeek, ClearedDay, VaultDay, Pieces, MapGiven;

        /// <summary>Per week: gold on the Guild's night before paying, the due, the shop tier, whether it was paid.</summary>
        public readonly int[] GoldAtDues = new int[MaxWeeks + 1], Due = new int[MaxWeeks + 1], TierAtDues = new int[MaxWeeks + 1];
        public readonly bool[] Paid = new bool[MaxWeeks + 1];

        public int Commissions, CommissionGold, CurioGold, CurioSpent, Pawned, Short, Grace;
        public readonly HashSet<string> Curios = new();
        public readonly float[] ProfitByTier = new float[5];
        public readonly int[] CustomersByTier = new int[5];
    }

    public static void Run(int campaigns)
    {
        GD.Print($"=== LootFall balance report ({campaigns} runs per style, up to {MaxWeeks} weeks of {Guild.DaysPerWeek} days) ===");
        var all = Styles.ToDictionary(s => s, s => Enumerable.Range(0, campaigns).Select(i => Play(s, 1000 + i).Log).ToList());

        GD.Print("\nStyle                   % still trading at each due (week 1..8)        cleared  vault  lost (median wk)  " +
                 "tier 2  tier 3  tier 4   good buys  perfect");
        foreach (var style in Styles)
        {
            var logs = all[style];
            var alive = Enumerable.Range(1, Guild.Weeks).Select(w => $"{100f * logs.Count(l => l.WeeksPaid >= w) / logs.Count,4:0}");
            var lost = logs.Where(l => l.LostWeek > 0).Select(l => l.LostWeek).OrderBy(w => w).ToList();
            string Reach(int tier) => Median(logs.Select(l => l.ReachedTier[tier]).ToList());
            int bought = Math.Max(1, logs.Sum(l => l.Bought));
            GD.Print($"{style.Name,-22} {string.Join(" ", alive)}   {100f * logs.Count(l => l.ClearedDay > 0) / logs.Count,6:0}% " +
                     $"{100f * logs.Count(l => l.VaultDay > 0) / logs.Count,5:0}%  {(lost.Count == 0 ? "-" : $"{lost.Count * 100 / logs.Count}%, wk {lost[lost.Count / 2]}"),16}  " +
                     $"{Reach(2),6}  {Reach(3),6}  {Reach(4),6}   {100f * logs.Sum(l => l.GoodBuys) / bought,8:0}%  {100f * logs.Sum(l => l.Perfect) / bought,6:0}%");
        }
        GD.Print("(cleared: all eight dues paid; vault: opened King Aldric's vault; lost: share of runs foreclosed, and the median week;");
        GD.Print(" tier columns: median day the renovation was bought, '-' if fewer than half got there)");

        foreach (var style in Styles.Skip(2))
        {
            var logs = all[style];
            GD.Print($"\nWeek by week, {style.Name}: over the runs still trading");
            GD.Print("week    due   gold on the night (10th / 25th / median pct)   paid   at tier 1 2 3 4 (%)");
            for (int w = 1; w <= MaxWeeks; w++)
            {
                var night = logs.Where(l => l.Due[w] > 0).ToList();
                if (night.Count == 0) break;
                int Pct(Func<Log, int> f, float p) => night.Select(f).OrderBy(x => x).ElementAt((int)(night.Count * p));
                var tiers = Enumerable.Range(1, 4).Select(t => $"{100f * night.Count(l => l.TierAtDues[w] == t) / night.Count,3:0}");
                GD.Print($"{w,4} {Pct(l => l.Due[w], 0.5f),6} {Pct(l => l.GoldAtDues[w], 0.1f),15} {Pct(l => l.GoldAtDues[w], 0.25f),8} " +
                         $"{Pct(l => l.GoldAtDues[w], 0.5f),8}   {100f * night.Count(l => l.Paid[w]) / night.Count,15:0}%   {string.Join(" ", tiers)}");
            }
            GD.Print($"per run: {logs.Average(l => l.Commissions):0.0} commissions ({logs.Average(l => l.CommissionGold):0}g), curios earned " +
                     $"{logs.Average(l => l.CurioGold):0}g for {logs.Average(l => l.CurioSpent):0}g, pawned on {logs.Average(l => l.Short):0.0} nights, " +
                     $"map pieces {logs.Average(l => l.Pieces):0.0}, map given {100f * logs.Count(l => l.MapGiven > 0) / logs.Count:0}%, " +
                     $"Bram vouched in {100f * logs.Count(l => l.Grace > 0) / logs.Count:0}% (median week " +
                     $"{logs.Where(l => l.Grace > 0).Select(l => l.Grace).DefaultIfEmpty(0).OrderBy(w => w).ElementAt(logs.Count(l => l.Grace > 0) / 2)})");
        }

        var careful = all[Styles[^2]];
        GD.Print($"\nCurios ({Styles[^2].Name}): share of runs that bought each, and how often those runs cleared the debt");
        foreach (var curio in Curios.All)
        {
            var with = careful.Where(l => l.Curios.Contains(curio.Key)).ToList();
            string rate = with.Count == 0 ? "   -" : $"{100f * with.Count(l => l.ClearedDay > 0) / with.Count,4:0}%";
            GD.Print($"  {curio.Name,-24} {100f * with.Count / careful.Count,4:0}%   cleared {rate}");
        }

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

        int worth = Strategy.Worth(visit, slip, hearing, state);
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
        var grade = Pick(visit, state, rng, care);
        return visit.Chest.Everything.Sum(i => state.SaleValue(i, grade));
    }

    /// <summary>How the lock comes open: careful players are better at picking; with a burglar's knife, anyone just forces it.</summary>
    static PickGrade Pick(Visit visit, GameState state, Random rng, Care care)
    {
        var grade = PickGrade.Clean;
        if (visit.Chest.Lock.Locked)
        {
            float roll = rng.NextFloat();
            grade = state.HasCurio("knife") ? PickGrade.Forced
                : care >= Care.Listens ? (roll < 0.3f ? PickGrade.Masterful : roll < 0.85f ? PickGrade.Clean : PickGrade.Forced)
                : roll < 0.6f ? PickGrade.Clean : PickGrade.Forced;
        }
        if (grade == PickGrade.Forced) visit.Chest.Force(rng, state.ForceSturdier);
        return grade;
    }

    static GameState CampaignState(Style style, int seed) => Play(style, seed, 30).State;

    /// <summary>One run in this style, until the Guild takes the shop or the days run out.</summary>
    static (Log Log, GameState State) Play(Style style, int seed, int days = Days)
    {
        var state = new GameState(seed) { Guided = false };
        var log = new Log();
        var rng = new Random(seed);
        for (int day = 1; day <= days; day++)
        {
            if (!PlayDay(state, style, rng, log))
            {
                log.LostWeek = state.Week;
                break;
            }
            for (int t = 2; t <= 4; t++)
                if (log.ReachedTier[t] == 0 && state.Tier >= t) log.ReachedTier[t] = day;
        }
        log.WeeksPaid = state.WeeksPaid;
        log.Pieces = state.MapPieces;
        log.MapGiven = state.MapGiven ? 1 : 0;
        log.Curios.UnionWith(state.CurioKeys);
        return (log, state);
    }

    /// <summary>
    /// For debugging the later screens (--start-week): plays a shop through these days the way the careful collector
    /// would, as fast as the simulation does. Returns false if the Guild took it on the way.
    /// </summary>
    public static bool FastForward(GameState state, int days)
    {
        var rng = new Random(state.Seed);
        var log = new Log();
        for (int day = 0; day < days; day++)
            if (!PlayDay(state, Styles[^1], rng, log)) return false;
        return true;
    }

    /// <summary>One day in this style: Nim, the customers, the evening's upgrades, the Guild. False if foreclosed.</summary>
    static bool PlayDay(GameState state, Style style, Random rng, Log log)
    {
        state.StartDay();
        if (state.PeddlerToday) VisitPeddler(state, log);
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
            var grade = Pick(visit, state, rng, style.Care);
            int value = 0, earned = 0;
            foreach (var item in visit.Chest.Everything)
            {
                int sale = state.SaleValue(item, grade);
                value += sale;
                state.Collect(item);
                var commission = Strategy.ShouldDeliver(state, item, sale);
                if (commission != null)
                {
                    int reward = state.Deliver(item, commission);
                    if (commission.Done)
                    {
                        log.Commissions++;
                        log.CommissionGold += reward;
                    }
                    continue;
                }
                if (style.Collects)
                {
                    var (keep, replace) = Strategy.ShouldKeep(state, item, sale);
                    if (keep)
                    {
                        if (replace != null) state.SellFromDisplay(replace);
                        state.Earn(sale);
                        state.Keep(item, sale);
                        continue;
                    }
                }
                earned += sale;
            }
            if (perfect) earned += state.KeenFee(value);
            state.RecordRead(perfect);
            state.Earn(earned);
            log.Bought++;
            if (value > price) log.GoodBuys++;
            if (perfect) log.Perfect++;
            log.ProfitByTier[tier] += value - price;
        }
        state.EndDay();
        log.CurioGold += state.Today.Curios;
        log.CurioSpent += state.Today.CurioSpent;
        if (state.DuesTonight && !Dues(state, log)) return false;
        BuyUpgrades(state, style.Care == Care.Tools, style.Collects);
        if (style.Care >= Care.Listens) ChooseSynergies(state);
        if (log.FirstSynergy == 0 && Synergies.All.Any(s => s.Level(state) > 0)) log.FirstSynergy = state.Day;
        return true;
    }

    /// <summary>Nim's morning: buy what a careful player would, keeping enough back for the Guild.</summary>
    static void VisitPeddler(GameState state, Log log)
    {
        for (int i = 0; i < 2; i++)
        {
            var (curio, sell) = Strategy.ChooseCurio(state, state.PeddlerStock);
            if (curio == null) return;
            if (sell != null) state.SellCurio(sell);
            state.BuyCurio(curio, Curios.Price(curio, state.Week));
            log.Curios.Add(curio.Key);
        }
    }

    /// <summary>The Guild's night: the map offer, paying (pawning if short), and the vault once it's due. False if foreclosed.</summary>
    static bool Dues(GameState state, Log log)
    {
        int week = state.Week;
        if (state.OfferTonight && Strategy.TakeOffer(state)) state.GiveMap();
        if (week <= MaxWeeks)
        {
            log.GoldAtDues[week] = state.Gold;
            log.Due[week] = state.DueNow;
            log.TierAtDues[week] = state.Tier;
        }
        if (state.Gold < state.DueNow)
        {
            var order = Strategy.PawnOrder(state);
            if (order == null && state.CanAskGrace)
            {
                log.Grace = week;
                state.AskGrace();
            }
            else if (order == null)
            {
                state.Foreclose();
                return false;
            }
            else
            {
                log.Short++;
                foreach (var thing in order)
                    log.Pawned += thing is Item item ? state.Pawn(item) : state.SellCurio((string)thing);
            }
        }
        if (!state.PaidThisWeek) state.PayDues();
        if (week <= MaxWeeks) log.Paid[week] = true;
        if (state.DebtCleared && log.ClearedDay == 0) log.ClearedDay = state.Day;
        if (state.VaultDue)
        {
            var vault = state.VaultVisit();
            state.VaultOpened = true;
            state.Earn(vault.Chest.Everything.Sum(i => state.SaleValue(i)));
            log.VaultDay = state.Day;
        }
        return true;
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

    /// <summary>Buys upgrades in order while keeping enough back for the Guild and tomorrow's chests.</summary>
    public static void BuyUpgrades(GameState state, bool tools = true, bool cabinets = true)
    {
        foreach (var key in BuyOrder)
        {
            if (!tools && key is "lamp" or "magnet") continue;
            if (!cabinets && key.StartsWith("cabinet")) continue;
            var upgrade = Upgrades.Get(key);
            if (Strategy.Affordable(state, upgrade)) state.BuyUpgrade(upgrade);
        }
    }

    static string Median(List<int> days)
    {
        var reached = days.Where(d => d > 0).OrderBy(d => d).ToList();
        if (reached.Count < days.Count / 2) return "-";
        return $"day {days.Select(d => d == 0 ? int.MaxValue : d).OrderBy(d => d).ElementAt(days.Count / 2)}";
    }
}
