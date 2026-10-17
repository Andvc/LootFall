using System;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// How a careful player plays: when to drop a chest or use the magnet, what to offer, what to keep for display. The
/// balance simulation and the autoplay bot share it, so the bot plays the way the numbers in the design notes assume.
/// </summary>
public static class Strategy
{
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
        _ when visit.Seller.Judge == Judgment.Clueless => Money.Round(Math.Min(worth * 0.5f, visit.Seller.Tier * 5)),
        _ => Money.Round(Math.Max(1, worth * 0.5f)),
    };

    /// <summary>When they want more than your first offer: most of what it's worth to you.</summary>
    public static int SecondOffer(int worth) => Money.Round(Math.Max(1, worth * 0.85f));

    /// <summary>Most a careful collector gives up, in sale price, for each exhibition point.</summary>
    public const int GoldPerPoint = 20;

    /// <summary>
    /// Keep things that look grand for what they'd sell for (a fine coat shows as well as a diamond and costs a tenth of
    /// it) while there's room; swap out the plainest when a grander one turns up.
    /// </summary>
    /// <returns>Whether to keep it, and what to sell from the display to make room (or null).</returns>
    public static (bool Keep, Item Replace) ShouldKeep(GameState state, Item item, int value)
    {
        if (!item.Displayable || item.Prestige < 2 || state.Tier < 3 || value > item.Prestige * GoldPerPoint || state.Gold - value < 20 + 15 * state.Tier)
            return (false, null);
        if (!state.DisplayFull) return (true, null);
        var plainest = state.Display.OrderBy(i => i.Prestige).First();
        return plainest.Prestige < item.Prestige ? (true, plainest) : (false, null);
    }
}
