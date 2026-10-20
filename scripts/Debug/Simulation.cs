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
        "shop2", "sign", "cabinet1", "picks", "cabinet2", "shop3", "lamp", "charter", "case", "cabinet3", "cabinet4", "shop4", "magnet",
        "cabinet5", "seal", "cabinet6", "night", "patrons", "velvet",
    };


    enum Care { Blind, OneShake, Listens, Tools }

    /// <param name="Skill">
    /// How well a learner plays (1: the careful player). Below 1 they skip strengths, miscount, believe what sellers
    /// say, forget the drop and the magnet, misjudge what a chest is worth and pick curios on a whim, less often the
    /// better they get (<see cref="Slip"/>).
    /// </param>
    sealed record Style(string Name, Care Care, bool Collects, bool Curios = true, bool Synergies = true, string Character = Characters.Kit,
        int Challenge = 0, string[] Talents = null, float Skill = 1);

    static readonly Style[] Styles =
    {
        new("buys at their price", Care.Blind, false),
        new("one hard shake", Care.OneShake, false),
        new("every strength, drops", Care.Listens, false),
        new("+ witchlight, magnet", Care.Tools, false),
        new("+ keeps grand things", Care.Tools, true),
    };

    /// <summary>The careful player with both tools, without curios, synergies or both: what they're worth.</summary>
    static readonly Style[] Without =
    {
        new("no curios", Care.Tools, false, Curios: false),
        new("no synergies", Care.Tools, false, Synergies: false),
        new("neither", Care.Tools, false, Curios: false, Synergies: false),
    };

    public static readonly string[] AllTalents = Talents.All.Select(t => t.Key).ToArray();

    sealed class Log
    {
        public int Bought, GoodBuys, Perfect, FirstSynergy;
        public readonly int[] ReachedTier = { 1, 1, 0, 0, 0 };

        /// <summary>Dues paid; the week the shop was lost (0 if never); the day the debt was cleared and the vault opened.</summary>
        public int WeeksPaid, LostWeek, ClearedDay, VaultDay, Pieces, MapGiven, PaidOffWeek;

        /// <summary>Per week: gold on the Guild's night before paying, the due, the shop tier, whether it was paid.</summary>
        public readonly int[] GoldAtDues = new int[MaxWeeks + 1], Due = new int[MaxWeeks + 1], TierAtDues = new int[MaxWeeks + 1];
        public readonly bool[] Paid = new bool[MaxWeeks + 1];

        public int Commissions, CommissionGold, CurioGold, CurioSpent, Pawned, Short, Grace, Stash, Mara;

        /// <summary>The weeks a night was short and things were pawned.</summary>
        public readonly List<int> ShortWeeks = new();

        /// <summary>The run's score had it retired after the debt's eighth week (or when it was lost).</summary>
        public int Score, Renown;

        /// <summary>Things opened; how many came out a step (or two or more) better than their chest; what that added.</summary>
        public int Opened, BetterOne, BetterTwo;
        public long Held, SurpriseGold;
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
            var cleared = logs.Where(l => l.ClearedDay > 0).ToList();
            string When(int w) => $"{100f * cleared.Count(l => (l.ClearedDay - 1) / Guild.DaysPerWeek + 1 == w) / logs.Count:0}%";
            GD.Print($"debt cleared in week 5: {When(5)}, week 6: {When(6)}, week 7: {When(7)}, week 8: {When(8)} " +
                     $"(paid off early: {100f * logs.Count(l => l.PaidOffWeek > 0 && l.PaidOffWeek <= Guild.Weeks) / logs.Count:0}% of runs)");
            GD.Print($"per run: {logs.Average(l => l.Commissions):0.0} commissions ({logs.Average(l => l.CommissionGold):0}g), curios earned " +
                     $"{logs.Average(l => l.CurioGold):0}g for {logs.Average(l => l.CurioSpent):0}g, pawned on {logs.Average(l => l.Short):0.0} nights, " +
                     $"map pieces {logs.Average(l => l.Pieces):0.0}, map given {100f * logs.Count(l => l.MapGiven > 0) / logs.Count:0}%, " +
                     $"Bram's stash opened {logs.Average(l => l.Stash):0.0} times, " +
                     $"Bram vouched in {100f * logs.Count(l => l.Grace > 0) / logs.Count:0}% (median week " +
                     $"{logs.Where(l => l.Grace > 0).Select(l => l.Grace).DefaultIfEmpty(0).OrderBy(w => w).ElementAt(logs.Count(l => l.Grace > 0) / 2)})");
        }

        GD.Print("\nSurprises (things better than their chest's colour), share of things opened, and of what the chests held:");
        foreach (var style in Styles.Skip(2))
        {
            var logs = all[style];
            float opened = Math.Max(1, logs.Sum(l => l.Opened));
            GD.Print($"  {style.Name,-22} a step better {100f * logs.Sum(l => l.BetterOne) / opened,5:0.0}%   two or more {100f * logs.Sum(l => l.BetterTwo) / opened,5:0.0}%   " +
                     $"worth {100f * logs.Sum(l => l.SurpriseGold) / Math.Max(1, logs.Sum(l => l.Held)),4:0}% of what the chests held");
        }

        GD.Print("\nWhat curios and synergies are worth to the careful player with both tools (cleared; median gold on the Guild's night, weeks 4/6/8)");
        foreach (var style in Without.Prepend(Styles[^2]))
        {
            var logs = style == Styles[^2] ? all[style] : Enumerable.Range(0, campaigns).Select(i => Play(style, 1000 + i).Log).ToList();
            string Gold(int w) => logs.Where(l => l.Due[w] > 0).Select(l => l.GoldAtDues[w]).OrderBy(g => g).Skip(logs.Count(l => l.Due[w] > 0) / 2)
                .DefaultIfEmpty(0).First().ToString();
            var days = logs.Where(l => l.ClearedDay > 0).Select(l => l.ClearedDay).OrderBy(d => d).ToList();
            GD.Print($"  {(style == Styles[^2] ? "with both" : style.Name),-14} cleared {100f * logs.Count(l => l.ClearedDay > 0) / logs.Count,4:0}% " +
                     $"(median day {(days.Count == 0 ? "-" : days[days.Count / 2].ToString()),3})   gold {Gold(4),7} {Gold(6),7} {Gold(8),7}");
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
        Meta(Math.Max(50, campaigns / 2));
        Learners(Math.Max(50, campaigns / 2));
        Careers(40, 30);
        LessonTest();
    }

    /// <summary>
    /// Players still learning (Kit, level 0), with no talents and with every one: how far they get. The design works if
    /// a first-timer gets through the first weeks and loses in the middle ones, better judgment and talents each push
    /// that later, and only the two together clear the debt more often than not.
    /// </summary>
    public static void Learners(int campaigns)
    {
        GD.Print($"\nLearners ({campaigns} runs each, Kit at level 0): cleared, lost first (median week), median clear day, score, renown, " +
                 "and % still trading after each due");
        GD.Print("skill  talents   cleared   lost first   day   score  renown");
        foreach (float skill in new[] { 0.15f, 0.5f, 0.75f, 1f })
        foreach (bool talents in new[] { false, true })
            GD.Print($"{skill,5:0.00}  {(talents ? "all" : "none"),-7}  {Trial(Characters.Kit, 0, talents ? AllTalents : null, campaigns, skill)}");
    }

    /// <summary>
    /// Bram's kin and the Guild's terms: each character played by the careful collector with both tools, with every
    /// talent and with none, at each challenge level. The design works if each character usually clears at level 0,
    /// talents raise the clear rate more than they shorten the run, and the top level is a wall for the best of them.
    /// </summary>
    public static void Meta(int campaigns)
    {
        GD.Print($"\nBram's kin ({campaigns} runs each, the careful collector with both tools): cleared %, lost (median week), " +
                 "median clear day, score and renown a run");
        GD.Print("character   talents  level   cleared   lost first   day   score  renown");
        foreach (var kin in Characters.All)
        foreach (var (talents, level) in new[] { (false, 0), (true, 0), (true, 2), (true, 4), (true, 6), (true, Challenges.Max) })
            GD.Print($"{kin.Name,-10}  {(talents ? "all" : "none"),-7}  {level,5}   {Trial(kin.Key, level, talents ? AllTalents : null, campaigns)}");
        GD.Print($"(lost first: foreclosed before the debt was cleared, and the median week; every talent costs {Talents.Total} renown, all told)");
    }

    /// <summary>One line of the meta report: this character on these terms with these talents, over so many runs.</summary>
    public static string Trial(string character, int challenge, IEnumerable<string> talents, int campaigns, float skill = 1)
    {
        var style = Learner(skill, character, challenge, talents?.ToArray());
        var logs = Enumerable.Range(0, campaigns).Select(i => Play(style, 1000 + i).Log).ToList();
        var lost = logs.Where(l => l.LostWeek > 0 && l.ClearedDay == 0).Select(l => l.LostWeek).OrderBy(w => w).ToList();
        var days = logs.Where(l => l.ClearedDay > 0).Select(l => l.ClearedDay).OrderBy(d => d).ToList();
        string paid = string.Join(" ", Enumerable.Range(1, Guild.Weeks).Select(w => $"{100f * logs.Count(l => l.WeeksPaid >= w) / logs.Count,3:0}"));
        return $"{100f * days.Count / logs.Count,6:0}%   {(lost.Count == 0 ? "-" : $"{100 * lost.Count / logs.Count}%, wk {lost[lost.Count / 2]}"),-11} " +
               $"{(days.Count == 0 ? "-" : days[days.Count / 2].ToString()),4}  {logs.Average(l => l.Score),6:0}  {logs.Average(l => l.Renown),6:0}   paid {paid}";
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
    static int? Deal(Visit visit, GameState state, Random rng, Care care, out Appraisal slip, float skill = 1)
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
            if (state.MindsHardShake(visit.Seller)) visit.Anger(rng);
            slip = new Appraisal(visit, tier);
            foreach (var kind in chest.HeardAt(Shake.Hard).GroupBy(i => i.Category))
                slip.Set(kind.Key, Math.Max(slip.Count(kind.Key), 1));
        }
        else
        {
            hearing = Listen(visit, state, rng, care == Care.Tools, skill);
            slip = hearing.Read(chest.Quality, tier);
            if (state.Weighs) Strategy.Weigh(slip, hearing, visit, state);
            if (skill >= 1 || rng.NextFloat() < skill) Strategy.OnFaith(visit, slip, hearing, state);
            if (skill < 1) Slip(visit, slip, rng, 1 - skill);
        }

        int worth = Strategy.Worth(visit, slip, hearing, state);
        // A learner's sense of what it's worth wanders, more the less they know.
        if (skill < 1) worth = (int)(worth * MathF.Exp(Normal(rng) * JudgmentNoise * (1 - skill)));
        switch (visit.Trade)
        {
            case Trade.Fixed:
                return worth > visit.Ask && state.Gold >= visit.Ask ? Paid(visit, rng) : null;
            case Trade.Haggle:
            {
                int offer = Math.Min(state.Gold, Strategy.FirstOffer(visit, worth));
                if (offer <= 0 || worth <= offer) return null;
                var response = visit.Offer(offer, rng);
                // A haggler who hears you out lets you try again after a cheeky first offer.
                if (response.Kind == ResponseKind.TryAgain) response = visit.Offer(Math.Min(state.Gold, Strategy.FirstOffer(visit, worth)), rng);
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

    /// <summary>
    /// A careful player's handling of a chest: every strength the seller allows, the magnet, the drop. A learner
    /// (<paramref name="skill"/> under 1) skips strengths, forgets the magnet, and drops when they shouldn't or doesn't
    /// when they should.
    /// </summary>
    static Hearing Listen(Visit visit, GameState state, Random rng, bool tools, float skill = 1)
    {
        var chest = visit.Chest;
        bool lamp = tools && state.Has("lamp");
        float e = 1 - skill;
        var levels = Strategy.Levels(visit, state);
        if (e > 0)
        {
            var kept = levels.Where(_ => rng.NextFloat() >= SkipShake * e).ToArray();
            levels = kept.Length > 0 ? kept : new[] { levels[^1] };
        }
        var hearing = Hearing.Simulate(chest, levels, lamp);
        if (tools && (e <= 0 || rng.NextFloat() < skill)) hearing.Listen(chest, Strategy.MagnetLevels(visit, hearing, state), lamp, magnet: true);
        bool drop = Strategy.ShouldDrop(visit, hearing, state.Tier, state);
        if (e > 0)
            drop = drop ? rng.NextFloat() >= ForgetDrop * e
                : state.Tier >= 2 && state.CanDrop && !hearing.Dropped && !state.MindsDrop(visit.Seller) && rng.NextFloat() < CarelessDrop * e;
        if (drop)
        {
            var (hollow, broke) = chest.Drop(rng, state.DropSturdier);
            hearing.HeardDrop(hollow, broke);
        }
        return hearing;
    }

    // A learner's mistakes, at skill 0 (they shrink in proportion as skill grows to 1). Guesses at a first-timer who
    // has had Bram's lessons: they know the rules, but don't yet apply them every time.
    const float SkipShake = 0.4f, ForgetDrop = 0.5f, CarelessDrop = 0.25f, Miscount = 0.5f, Believe = 0.35f, MissBoom = 0.3f, MissGlow = 0.5f,
        JudgmentNoise = 0.45f, Whim = 0.7f;

    /// <summary>A learner's slip: a pair counted as one, a sound the seller claimed but nobody heard, a boom or a glow not ticked.</summary>
    static void Slip(Visit visit, Appraisal slip, Random rng, float e)
    {
        foreach (var kind in Categories.UpTo(slip.Tier).Select(k => k.Category))
        {
            if (slip.Count(kind) >= 2 && rng.NextFloat() < Miscount * e) slip.Set(kind, slip.Count(kind) - 1, slip.Magic(kind));
            if (slip.Magic(kind) && rng.NextFloat() < MissGlow * e) slip.Set(kind, slip.Count(kind));
        }
        if (rng.NextFloat() < Believe * e)
            foreach (var kind in visit.Claim.Where(k => slip.Count(k) == 0)) slip.Set(kind, 1);
        if (slip.Hidden && rng.NextFloat() < MissBoom * e) slip.Hidden = false;
    }

    /// <summary>A standard normal draw.</summary>
    static float Normal(Random rng) => MathF.Sqrt(-2 * MathF.Log(1 - rng.NextFloat())) * MathF.Cos(2 * MathF.PI * rng.NextFloat());

    /// <summary>Picks the lock (careful players are better at it) and adds up what came out, at what it sells for.</summary>
    static int Open(Visit visit, GameState state, Random rng, Care care)
    {
        var grade = Pick(visit, state, rng, care);
        return visit.Chest.Everything.Sum(i => state.SaleValue(i, grade));
    }

    /// <summary>How the lock comes open: careful players are better at picking; with a burglar's knife, anyone just forces it.</summary>
    static PickGrade Pick(Visit visit, GameState state, Random rng, Care care, float skill = 1)
    {
        var grade = PickGrade.Clean;
        if (visit.Chest.Lock.Locked)
        {
            float roll = rng.NextFloat(), oil = state.HasCurio("oil") ? 0.1f : 0f;
            grade = state.HasCurio("knife") ? PickGrade.Forced
                : skill < 1 ? (roll < 0.3f * skill + oil ? PickGrade.Masterful : roll < 0.6f + 0.25f * skill + oil / 2 ? PickGrade.Clean : PickGrade.Forced)
                : care >= Care.Listens ? (roll < 0.3f + oil ? PickGrade.Masterful : roll < 0.85f + oil / 2 ? PickGrade.Clean : PickGrade.Forced)
                : roll < 0.6f ? PickGrade.Clean : PickGrade.Forced;
        }
        if (grade == PickGrade.Forced) visit.Chest.Force(rng, state.ForceSturdier);
        return grade;
    }

    static GameState CampaignState(Style style, int seed) => Play(style, seed, 30).State;

    /// <summary>One run in this style, until the Guild takes the shop or the days run out.</summary>
    static (Log Log, GameState State) Play(Style style, int seed, int days = Days)
    {
        var state = GameState.NewRun(seed, style.Character, style.Challenge, style.Talents, guided: false);
        var log = new Log();
        var rng = new Random(seed);
        for (int day = 1; day <= days; day++)
        {
            if (!PlayDay(state, style, rng, log))
            {
                log.LostWeek = state.Week;
                break;
            }
            if (day == Guild.Weeks * Guild.DaysPerWeek) Scored(state, log);
            for (int t = 2; t <= 4; t++)
                if (log.ReachedTier[t] == 0 && state.Tier >= t) log.ReachedTier[t] = day;
        }
        if (log.LostWeek > 0 || days < Guild.Weeks * Guild.DaysPerWeek) Scored(state, log);
        log.WeeksPaid = state.WeeksPaid;
        log.Stash = state.StashUsed;
        log.Pieces = state.MapPieces;
        log.MapGiven = state.MapGiven ? 1 : 0;
        log.Curios.UnionWith(state.CurioKeys);
        return (log, state);
    }

    /// <summary>
    /// Week by week for a learner of this skill (Kit, level 0, no talents): gold on the Guild's night against the due,
    /// how many pawned or leaned on Bram's word that night, the shop they had, and what they'd bought by then.
    /// </summary>
    public static void Diagnose(float skill, int campaigns, string[] talents = null)
    {
        var logs = Enumerable.Range(0, campaigns).Select(i => Play(Learner(skill, talents: talents), 1000 + i)).ToList();
        GD.Print($"\nskill {skill:0.00}: week, due, gold on the night (25th / median), paid, pawned, Bram's word, at tier 1 2 3 4 (%)");
        for (int w = 1; w <= Guild.Weeks; w++)
        {
            var night = logs.Where(l => l.Log.Due[w] > 0).ToList();
            if (night.Count == 0) break;
            int Pct(float p) => night.Select(l => l.Log.GoldAtDues[w]).OrderBy(x => x).ElementAt((int)(night.Count * p));
            var tiers = Enumerable.Range(1, 4).Select(t => $"{100f * night.Count(l => l.Log.TierAtDues[w] == t) / night.Count,3:0}");
            GD.Print($"{w,3} {night.Select(l => l.Log.Due[w]).OrderBy(x => x).ElementAt(night.Count / 2),6} {Pct(0.25f),7} {Pct(0.5f),7}   " +
                     $"{100f * night.Count(l => l.Log.Paid[w]) / night.Count,4:0}%  {100f * night.Count(l => l.Log.ShortWeeks.Contains(w)) / night.Count,4:0}%  " +
                     $"{100f * night.Count(l => l.Log.Grace == w) / night.Count,4:0}%   {string.Join(" ", tiers)}");
        }
        GD.Print($"stash opened {logs.Average(l => l.State.StashUsed):0.0} times; bought {logs.Average(l => l.Log.Bought):0} chests, " +
                 $"{100f * logs.Sum(l => l.Log.GoodBuys) / Math.Max(1, logs.Sum(l => l.Log.Bought)):0}% good; curios {logs.Average(l => l.Log.Curios.Count):0.0}");
    }

    /// <summary>A learner of this skill: the careful collector's way of playing, with a learner's mistakes.</summary>
    static Style Learner(float skill, string character = Characters.Kit, int challenge = 0, string[] talents = null) =>
        Styles[^1] with { Name = $"skill {skill:0.00}", Skill = skill, Character = character, Challenge = challenge, Talents = talents };

    /// <summary>
    /// Whole careers: players who start out knowing the rules but not yet applying them (skill 0 to 0.3), and get better
    /// run by run toward their own best (0.75 to 1), at their own pace. Each run is Kit's at the highest challenge level
    /// open; it ends with the eighth week (or the shop); its renown is banked and spent on the cheapest talent going,
    /// as far as it goes. Prints, run by run, how they fare, and when they first clear the debt at each level.
    /// </summary>
    public static void Careers(int careers, int runs)
    {
        var records = new List<(int Run, float Skill, bool Cleared, int Lost, int Level, int Talents, int Renown, int Score)>();
        var firstClear = new List<int>[Challenges.Max + 1];
        for (int l = 0; l <= Challenges.Max; l++) firstClear[l] = new List<int>();
        for (int c = 0; c < careers; c++)
        {
            var who = new Random(77 + c * 31);
            float start = who.Between(0f, 0.3f), best = who.Between(0.75f, 1f), pace = who.Between(0.1f, 0.3f);
            var profile = new Profile();
            for (int r = 0; r < runs; r++)
            {
                float skill = best - (best - start) * MathF.Exp(-pace * r);
                int level = profile.MaxChallenge(Characters.Kit);
                var (log, state) = Play(Learner(skill, Characters.Kit, level, profile.Talents.ToArray()), 500000 + c * 1000 + r, Guild.Weeks * Guild.DaysPerWeek);
                profile.Runs++;
                profile.Note(state);
                if (state.DebtCleared)
                {
                    profile.Cleared++;
                    profile.ClearedAt(state);
                }
                if (state.VaultOpened) profile.Vaults++;
                int renown = profile.Bank(state);
                while (Talents.All.Where(profile.CanLearn).OrderBy(t => t.Cost).FirstOrDefault() is TalentDef next) profile.Learn(next);
                records.Add((r + 1, skill, state.DebtCleared, state.DebtCleared ? 0 : state.Week, level, profile.Talents.Count, renown, Score.Total(state)));
            }
            // The run each level was first cleared in (by this career's own record).
            var mine = records.Skip(c * runs).ToList();
            for (int l = 0; l <= Challenges.Max; l++)
            {
                var first = mine.FirstOrDefault(x => x.Cleared && x.Level == l);
                firstClear[l].Add(first.Run == 0 ? int.MaxValue : first.Run);
            }
        }

        GD.Print($"\nCareers ({careers} players, {runs} runs each, Kit at the highest level open, talents bought cheapest first)");
        GD.Print("run   skill   cleared   lost (median wk)   level   talents   renown   score");
        foreach (var run in records.GroupBy(x => x.Run))
        {
            if (run.Key > 12 && run.Key % 4 != 0) continue;
            var lost = run.Where(x => !x.Cleared).Select(x => x.Lost).OrderBy(w => w).ToList();
            GD.Print($"{run.Key,3}   {run.Average(x => x.Skill),5:0.00}   {100f * run.Count(x => x.Cleared) / run.Count(),6:0}%   " +
                     $"{(lost.Count == 0 ? "-" : $"{100 * lost.Count / run.Count()}%, wk {lost[lost.Count / 2]}"),-16}   {run.Average(x => x.Level),5:0.0}   " +
                     $"{run.Average(x => x.Talents),7:0.0}   {run.Average(x => x.Renown),6:0}   {run.Average(x => x.Score),5:0}");
        }
        string When(List<int> runsTo) => runsTo.Count(r => r < int.MaxValue) * 2 < runsTo.Count ? $"half not by run {runs}"
            : $"run {runsTo.OrderBy(r => r).ElementAt(runsTo.Count / 4)} / {runsTo.OrderBy(r => r).ElementAt(runsTo.Count / 2)} / " +
              (runsTo.Count(r => r < int.MaxValue) * 4 < runsTo.Count * 3 ? "-" : $"{runsTo.OrderBy(r => r).ElementAt(runsTo.Count * 3 / 4)}");
        GD.Print("first cleared the debt (quarter / half / three quarters of players):");
        foreach (int l in new[] { 0, 1, 2, 4, 6, 8 })
            GD.Print($"  level {l}: {When(firstClear[l])}   ({100f * firstClear[l].Count(r => r < int.MaxValue) / careers:0}% by run {runs})");
    }

    static void Scored(GameState state, Log log)
    {
        log.Score = Score.Total(state);
        log.Renown = Score.Renown(state);
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
        if (state.PeddlerToday && style.Curios) VisitPeddler(state, log, rng, style.Skill);
        while (state.NextCustomer())
        {
            var visit = state.Current;
            int tier = state.Tier;
            log.CustomersByTier[tier]++;
            int? paid = Deal(visit, state, rng, style.Care, out var slip, style.Skill);
            if (paid is not int price)
            {
                if (visit.Deciding) visit.Pass(rng);
                state.Pass();
                continue;
            }
            state.Buy(visit, slip);
            bool perfect = slip.Check(visit.Chest).Perfect;
            var grade = Pick(visit, state, rng, style.Care, style.Skill);
            int value = 0, earned = 0;
            foreach (var item in visit.Chest.Everything)
            {
                int sale = state.SaleValue(item, grade);
                value += sale;
                if (!item.Fake && !item.Broken)
                {
                    log.Opened++;
                    int steps = item.Quality - visit.Chest.Quality;
                    if (steps == 1) log.BetterOne++;
                    if (steps >= 2) log.BetterTwo++;
                    log.Held += item.Value;
                    if (steps > 0) log.SurpriseGold += item.Value - Categories.Value(item.Category, visit.Chest.Quality, item.Magic);
                }
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
                if (style.Collects || state.Is(Characters.Isolde))
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
        if (Strategy.ShouldPayOff(state))
        {
            state.PayOff();
            log.PaidOffWeek = state.Week;
        }
        if (state.DebtCleared && log.ClearedDay == 0) log.ClearedDay = state.Day;
        if (state.VaultDue)
        {
            var vault = state.VaultPurchase();
            state.VaultOpened = true;
            state.Earn(vault.Chest.Everything.Sum(i => state.SaleValue(i)));
            log.VaultDay = state.Day;
        }
        BuyUpgrades(state, style.Care == Care.Tools, style.Collects);
        if (style.Care >= Care.Listens && style.Synergies) ChooseSynergies(state);
        if (log.FirstSynergy == 0 && Synergies.All.Any(s => s.Level(state) > 0)) log.FirstSynergy = state.Day;
        return true;
    }

    /// <summary>Nim's morning: buy what a careful player would, keeping enough back for the Guild.</summary>
    static void VisitPeddler(GameState state, Log log, Random rng = null, float skill = 1)
    {
        for (int i = 0; i < 2; i++)
        {
            var (curio, sell) = Strategy.ChooseCurio(state, state.PeddlerStock);
            // A learner picks on a whim: whatever catches the eye and leaves a little in the purse.
            if (skill < 1 && rng.NextFloat() < Whim * (1 - skill))
            {
                var whim = state.PeddlerStock.Where(c => !state.HasCurio(c.Key) && state.Gold - state.CuriosPrice(c) >= ShopTiers.Working(state.Tier)).ToArray();
                curio = whim.Length > 0 && !state.CuriosFull ? rng.PickOne(whim) : null;
                sell = null;
            }
            // Nothing worth it on the counter: ask Nim for a second look, if he'll give one.
            if (curio == null && state.CanRerollPeddler)
            {
                state.RerollPeddler();
                (curio, sell) = Strategy.ChooseCurio(state, state.PeddlerStock);
            }
            if (curio == null) return;
            if (sell != null) state.SellCurio(sell);
            state.BuyCurio(curio, state.CuriosPrice(curio));
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
            if (order == null && state.CanAskMara)
            {
                log.Mara = week;
                state.AskMara();
            }
            else if (order == null && state.CanAskGrace)
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
                log.ShortWeeks.Add(week);
                foreach (var thing in order)
                    log.Pawned += thing is Item item ? state.Pawn(item) : state.SellCurio((string)thing);
            }
        }
        if (!state.PaidThisWeek) state.PayDues();
        if (week <= MaxWeeks) log.Paid[week] = true;
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
