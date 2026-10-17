using System;
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

    /// <summary>Check a jingle or tinkle with the magnet, unless the seller would mind or there's no time.</summary>
    public static bool ShouldUseMagnet(Visit visit, Hearing hearing, GameState state) =>
        state.Has("magnet") && !visit.Seller.HatesMagnet && visit.Seller.Patience == 0 &&
        Categories.All.Any(c => c.FakeName != null && hearing.HeardAt(c.Category, Shake.Gentle, false));

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
    /// What the chest is worth to a careful player who knows the seller: the slip, plus a fair guess at what couldn't
    /// be heard (a granny who forbids a hard shake has cloth she never mentions), and a bit more from a seller whose
    /// things run better than their colour.
    /// </summary>
    public static int Worth(Visit visit, Appraisal slip, Hearing hearing)
    {
        float worth = slip.Worth;
        if (!hearing.HasTried(Shake.Hard, false))
        {
            var seller = visit.Seller;
            float total = seller.Kinds.Sum();
            foreach (var kind in Categories.UpTo(slip.Tier).Where(k => k.StirsAt == Shake.Hard && slip[k.Category] == Mark.None))
            {
                float share = seller.Kinds[(int)kind.Category] / total;
                worth += share * 2.5f * Categories.Expected(kind.Category, slip.Chest) * 0.8f;
            }
        }
        return (int)(worth * (1 + visit.Seller.QualityBoost * 1.2f));
    }

    /// <summary>When they want more than your first offer: most of what it's worth to you.</summary>
    public static int SecondOffer(int worth) => Money.Round(Math.Max(1, worth * 0.85f));

    /// <summary>Most a careful collector gives up, in sale price, for each exhibition point.</summary>
    public const int GoldPerPoint = 20;

    /// <summary>
    /// Keep things that look grand for what they'd sell for (a fine coat shows as well as a diamond and costs a tenth of
    /// it) while there's room; swap out the plainest when a grander one turns up. Stop at the Crown's mark: past it, an
    /// exhibition brings no more customers or better chests.
    /// </summary>
    /// <returns>Whether to keep it, and what to sell from the display to make room (or null).</returns>
    public static (bool Keep, Item Replace) ShouldKeep(GameState state, Item item, int value)
    {
        if (!item.Displayable || item.Prestige < 2 || state.Tier < 3 || state.Exhibition >= GameState.WarrantExhibition ||
            value > item.Prestige * GoldPerPoint || state.Gold - value < 20 + 15 * state.Tier)
            return (false, null);
        if (!state.DisplayFull) return (true, null);
        var plainest = state.Display.OrderBy(i => i.Prestige).First();
        return plainest.Prestige < item.Prestige ? (true, plainest) : (false, null);
    }
}
