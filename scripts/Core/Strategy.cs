using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// How a careful player plays: when to drop a chest or use the magnet, what to offer, what to keep for display. The
/// balance simulation and the autoplay bot share it, so the bot plays the way the numbers in the design notes assume.
/// </summary>
public static class Strategy
{
    static readonly Shake[] Every = { Shake.Gentle, Shake.Firm, Shake.Hard };

    /// <summary>
    /// Which strengths to shake at: all three, except gently and hard when there's no time (hard hears everything
    /// firm does), or gently and firmly when the seller won't have it shaken hard.
    /// </summary>
    public static Shake[] Levels(Visit visit) =>
        visit.Seller.HatesHardShake ? new[] { Shake.Gentle, Shake.Firm }
        : visit.Seller.Patience > 0 ? new[] { Shake.Gentle, Shake.Hard }
        : Every;

    /// <summary>Drop it to listen for a false bottom, unless there's glass in there or the seller would mind.</summary>
    public static bool ShouldDrop(Visit visit, Hearing hearing, int tier) =>
        tier >= 2 && !visit.Seller.HatesDrop && visit.Seller.Patience == 0 && !hearing.Dropped &&
        !Categories.All.Any(c => c.Fragile && hearing.HeardAny(c.Category));

    /// <summary>
    /// Where to check with the magnet: each strength at which a jingle or tinkle was heard (a gilded fake goes quiet
    /// there), unless the seller would mind or there's no time.
    /// </summary>
    public static Shake[] MagnetLevels(Visit visit, Hearing hearing, GameState state)
    {
        if (!state.Has("magnet") || visit.Seller.HatesMagnet || visit.Seller.Patience > 0) return Array.Empty<Shake>();
        var gilded = Categories.All.Where(c => c.FakeName != null && c.FakeIron).Select(c => c.Category).ToArray();
        // The gentlest strength each one sounded at: where a fake would go quiet.
        return gilded.Select(c => Every.FirstOrDefault(l => hearing.HeardAt(c, l, false))).Where(l => l != Shake.Still)
            .Distinct().OrderBy(l => l).ToArray();
    }

    public static bool ShouldUseMagnet(Visit visit, Hearing hearing, GameState state) => MagnetLevels(visit, hearing, state).Length > 0;

    /// <summary>
    /// What a careful player ticks on faith when they couldn't shake hard enough to hear it: a soft kind the seller
    /// usually brings (Granny Wick's sewing). Their habit is in the ledger.
    /// </summary>
    public static void OnFaith(Visit visit, Appraisal slip, Hearing hearing)
    {
        if (hearing.HasTried(Shake.Hard, false)) return;
        var seller = visit.Seller;
        float total = seller.Kinds.Sum();
        foreach (var kind in Categories.UpTo(slip.Tier).Where(k => k.StirsAt == Shake.Hard && slip.Count(k.Category) == 0))
            if (seller.Kinds[(int)kind.Category] / total >= FaithShare) slip.Set(kind.Category, 1);
    }

    /// <summary>How much of what a seller brings has to be one kind before a careful player takes it on faith.</summary>
    const float FaithShare = 0.35f;

    /// <summary>
    /// A haggler's first answer: about half their asking price, never more than the chest is worth to you. When they ask
    /// you to name a price: half what it's worth to you, or just a few coins to someone who has no idea.
    /// </summary>
    public static int FirstOffer(Visit visit, int worth) => visit.Trade switch
    {
        Trade.Haggle => Money.Round(Math.Min(visit.Ask * 0.55f, worth * 0.8f)),
        _ when visit.Seller.Judge == Judgment.Clueless => Money.Round(Math.Min(worth * 0.5f, visit.Seller.Tier * 4)),
        // Someone who only knows magic takes a modest sum when there's none.
        _ when visit.Seller.Judge == Judgment.MagicSense => Money.Round(Math.Min(worth * 0.5f, visit.Seller.Tier * 8)),
        _ => Money.Round(Math.Max(1, worth * 0.5f)),
    };

    /// <summary>
    /// What the chest is worth to a careful player who knows the seller: what the odds bar says it's more likely than not
    /// worth (they buy when the bar says it's better than even), plus a fair guess at what couldn't be checked (soft
    /// things in a chest they couldn't shake hard, a false bottom in a chest they couldn't drop), and a bit more from a
    /// seller whose things run better than their colour. With the shop's state: at what things sell for there (curios,
    /// synergies, today's event), and with a fair share of any commission the chest could fill.
    /// </summary>
    public static int Worth(Visit visit, Appraisal slip, Hearing hearing, GameState state = null)
    {
        var seller = visit.Seller;
        float worth = slip.LikelyWorth(state == null ? null : state.SaleMultiplier) + (state == null ? 0 : CommissionWorth(state, slip));
        if (!hearing.HasTried(Shake.Hard, false))
        {
            float total = seller.Kinds.Sum();
            foreach (var kind in Categories.UpTo(slip.Tier).Where(k => k.StirsAt == Shake.Hard && slip.Count(k.Category) == 0))
            {
                float share = seller.Kinds[(int)kind.Category] / total;
                worth += share * 2.5f * Categories.Expected(kind.Category, slip.Chest) * 0.8f;
            }
        }
        if (!hearing.Dropped && !slip.Hidden && slip.Tier >= 2)
            worth += seller.HiddenChance * Market.HiddenValue(slip.Chest, slip.Tier, expected: true);
        return (int)(worth * (1 + seller.QualityBoost * 1.2f));
    }

    /// <summary>When they want more than your first offer: most of what it's worth to you.</summary>
    public static int SecondOffer(int worth) => Money.Round(Math.Max(1, worth * 0.85f));

    /// <summary>
    /// What the commissions add to a chest: for each one the slip's kinds could fill, the chance one of them is good
    /// enough, times what the commission pays over its sale price. Half of it: it might go to another chest first.
    /// </summary>
    public static float CommissionWorth(GameState state, Appraisal slip)
    {
        float extra = 0;
        foreach (var c in state.Commissions.Where(c => !c.Done && c.Kind != CommissionKind.Enchanted))
        {
            int n = slip.Count(c.Category);
            if (n == 0) continue;
            float p = Categories.Odds(slip.Chest).Where(o => o.Quality >= c.Quality).Sum(o => o.Chance);
            float hit = 1 - MathF.Pow(1 - p, Math.Min(n, c.Needed - c.Delivered));
            float each = state.CommissionReward(c) / (float)c.Needed - Categories.Nominal(c.Category, c.Quality) * state.SaleMultiplier(c.Category);
            extra += hit * Math.Max(0, each) * (c.Story ? 1f : 0.5f);
        }
        return extra;
    }

    /// <summary>Hand it over if a commission wants it and pays more than selling it would (story ones always).</summary>
    public static Commission ShouldDeliver(GameState state, Item item, int saleValue)
    {
        var c = state.WantedBy(item);
        if (c == null) return null;
        return c.Story || state.CommissionReward(c) / (float)c.Needed >= saleValue ? c : null;
    }

    /// <summary>What a careful player's shop of each tier clears in a bad day's trading (about half a usual one).</summary>
    static readonly int[] BadDay = { 0, 15, 30, 150, 300 };

    /// <summary>What it takes to buy a day's chests at each tier.</summary>
    static readonly int[] Capital = { 0, 20, 25, 100, 250 };

    /// <summary>
    /// Gold to keep back: enough to buy tomorrow's chests, and whatever of the next due a run of bad days before the
    /// Guild comes wouldn't cover (at the shop's tier after a renovation being considered; the first day back is spent
    /// building up again, so it doesn't count).
    /// </summary>
    public static int Reserve(GameState state, int tier = 0)
    {
        tier = Math.Max(tier, state.Tier);
        return Capital[tier] + Math.Max(0, state.NextDue - Math.Max(0, state.DaysToNextDue - 1) * BadDay[tier]);
    }

    /// <summary>
    /// Whether to buy an upgrade now: a renovation is judged by what the bigger shop will make before the Guild comes;
    /// anything big has to leave the due after next as well (it pays back over weeks, not days).
    /// </summary>
    public static bool Affordable(GameState state, Upgrade upgrade)
    {
        int reserve = Reserve(state, upgrade.SetsTier);
        if (!upgrade.IsRenovation && upgrade.Cost >= 1000) reserve += state.DueFor(state.Week + (state.PaidThisWeek ? 2 : 1));
        return state.CanBuy(upgrade) && state.Gold - upgrade.Cost >= reserve;
    }

    /// <summary>A careful player's taste in curios, best first.</summary>
    public static readonly string[] CurioOrder =
    {
        "receipt", "clover", "lantern", "loupe", "letter", "coin", "book", "signet", "jar", "crescent", "shell", "gauntlet", "cap",
        "catalogue", "hook", "draught", "knife",
    };

    static int Rank(string key) => Array.IndexOf(CurioOrder, key) is var i and >= 0 ? i : CurioOrder.Length;

    /// <summary>
    /// What to buy from Nim: the best curio on offer that leaves enough for the Guild, if there's room, or if it's much
    /// better than the worst one owned (which goes back to Nim for half). Returns the curio and the one to sell, or nulls.
    /// </summary>
    public static (CurioDef Buy, string Sell) ChooseCurio(GameState state, IEnumerable<CurioDef> stock)
    {
        // The Visitors' Book is no use without an exhibition.
        var wanted = stock.Where(c => !state.HasCurio(c.Key) && (c.Key != "book" || state.Exhibition >= 10)).OrderBy(c => Rank(c.Key));
        foreach (var curio in wanted)
        {
            int price = Curios.Price(curio, state.Week);
            string sell = null;
            if (state.CuriosFull)
            {
                var worst = state.CurioKeys.OrderByDescending(Rank).First();
                if (Rank(worst) - Rank(curio.Key) < 4) continue;
                sell = worst;
                price -= state.CurioValue(worst);
            }
            if (state.Gold - price >= Reserve(state)) return (curio, sell);
        }
        return (null, null);
    }

    /// <summary>What to pawn when short, cheapest loss first: displayed things, then curios. Null when it's hopeless.</summary>
    public static List<object> PawnOrder(GameState state)
    {
        if (!state.CanCover) return null;
        var order = new List<object>();
        int gold = state.Gold;
        foreach (var item in state.Display.OrderBy(i => i.Prestige).ThenBy(state.PawnValue))
        {
            if (gold >= state.DueNow) break;
            order.Add(item);
            gold += state.PawnValue(item);
        }
        foreach (var key in state.CurioKeys.OrderByDescending(Rank))
        {
            if (gold >= state.DueNow) break;
            order.Add(key);
            gold += state.CurioValue(key);
        }
        return order;
    }

    /// <summary>The Guild's offer for the map: a careful player takes it only if it's that or the shop.</summary>
    public static bool TakeOffer(GameState state) => state.Gold < state.DueNow && state.Gold >= state.DueNow / 2;

    /// <summary>Most a careful collector gives up, in sale price, for each exhibition point.</summary>
    public const int GoldPerPoint = 20;

    /// <summary>
    /// Keep things that look grand for what they'd sell for (a fine coat shows as well as a diamond and costs a tenth of
    /// it) while there's room; swap out the plainest when a grander one turns up. Stop at the most an exhibition does for
    /// customers and chests, unless visitors pay to see it (a visitors' book).
    /// </summary>
    /// <returns>Whether to keep it, and what to sell from the display to make room (or null).</returns>
    public static (bool Keep, Item Replace) ShouldKeep(GameState state, Item item, int value)
    {
        if (!item.Displayable || item.Prestige < 2 || state.Tier < 3 || (state.Exhibition >= GameState.ExhibitionCap && !state.HasCurio("book")) ||
            value > item.Prestige * GoldPerPoint || state.Gold - value < 20 + 15 * state.Tier)
            return (false, null);
        if (!state.DisplayFull) return (true, null);
        var plainest = state.Display.OrderBy(i => i.Prestige).First();
        return plainest.Prestige < item.Prestige ? (true, plainest) : (false, null);
    }
}
