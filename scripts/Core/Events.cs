using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <param name="Bram">What Bram says about it the first time (and the day card shows every time).</param>
/// <param name="MinTier">Only from this shop tier, where it makes a difference.</param>
public sealed record EventDef(string Key, string Name, string Blurb, int MinTier = 1);

/// <summary>
/// Once a week, from the second, something happens in town for a day: a fair, Locking Day, the Crown's buyer, a
/// royal chest, the Stranger with a chained chest, a storm, a barge aground. Each changes what a good day looks like.
/// </summary>
public static class Events
{
    public const int FromWeek = 2;

    public const int MarketCustomers = 3, StormCustomers = -2;
    public const float FestivalSales = 1.25f, BuyerSales = 2f, WreckPairs = 0.35f;

    public static readonly EventDef[] All =
    {
        new("market", "Fair Day", "The whole county's in town for the fair: three more customers today."),
        new("festival", "Locking Day", "Three hundred years since the Locking, and everyone's celebrating: everything sells for a quarter more today."),
        new("buyer", "The Crown's Buyer", "The Crown's buyer is in town, paying double for {kind} today."),
        new("gilded", "A Royal Chest", "Someone's bringing in one of King Aldric's own chests, gilded, with the royal seal. They think the gold is paint."),
        new("stranger", "The Stranger", "A stranger in black is coming by with a chest wrapped in iron chains. Nothing in it can rattle."),
        new("storm", "A Storm", "Few will brave the rain, but those who do bring their best: chests come in a colour better."),
        new("wreck", "A Barge Aground", "A treasure barge ran aground on the Aldwater: today's lots come in twos and threes.", MinTier: 3),
    };

    public static EventDef Get(string key) => All.First(e => e.Key == key);

    /// <summary>The event in a few words, for the top bar (click it for the whole story).</summary>
    public static string Tag(EventDef e, Category kind) => e.Key switch
    {
        "market" => "Fair Day: +3 customers",
        "festival" => "Locking Day: sales +25%",
        "buyer" => $"Crown's Buyer: {Categories.Get(kind).Name.ToLowerInvariant()} x2",
        "gilded" => "A royal chest today",
        "stranger" => "The Stranger's in town",
        "storm" => "Storm: better chests",
        "wreck" => "Barge aground: lots of pairs",
        _ => e.Name,
    };

    /// <summary>The event's blurb, with the buyer's kind filled in.</summary>
    public static string Describe(EventDef e, Category kind) =>
        e.Blurb.Replace("{kind}", Categories.Get(kind).Name.ToLowerInvariant());

    /// <summary>This week's event: one you haven't had lately (they all come round before any comes twice).</summary>
    public static EventDef Pick(Random rng, int tier, ICollection<string> seen)
    {
        var fresh = All.Where(e => e.MinTier <= tier && !seen.Contains(e.Key)).ToArray();
        if (fresh.Length == 0)
        {
            seen.Clear();
            fresh = All.Where(e => e.MinTier <= tier).ToArray();
        }
        var pick = rng.PickOne(fresh);
        seen.Add(pick.Key);
        return pick;
    }

    /// <summary>
    /// The Stranger: never says where it's from, never lets you open it first. It's wrapped tight in iron chains, so
    /// nothing inside can move; all you can tell is how heavy it is. Mostly junk, sometimes a fair lot, now and then a fortune.
    /// </summary>
    public static readonly SellerDef Stranger = new()
    {
        Key = "stranger", Name = "The Stranger", Tier = 1, Judge = Judgment.Clueless,
        Kinds = new[] { 1f, 1, 1, 1, 1, 1 }, Colours = new[] { 1f, 0, 0, 0 },
        Habit = "Sells chests wrapped in iron chains: nothing inside can move, so nothing rattles. Heavy ones hold more.",
        Pitches = new[] { "Chained tight. No rattles, no questions. {price}.", "Where from? Somewhere that wanted it kept shut. {price}.",
                          "Some chests are better left shut. Not this one. {price}." },
        Accepts = new[] { "Wise.", "We'll meet again." },
        Leaves = new[] { "Another time." },
    };

    public const float StrangerJunk = 0.45f, StrangerTreasure = 0.2f;

    /// <summary>The Stranger's sealed chest, priced at what an ordinary chest at this shop holds.</summary>
    public static Visit StrangerVisit(Random rng, int tier)
    {
        var colour = (Rarity)Math.Clamp(tier - 1, 0, 3);
        var kinds = Categories.UpTo(tier).Select(c => c.Category).ToArray();
        float roll = rng.NextFloat();
        List<Item> items;
        if (roll < StrangerJunk)
        {
            // Junk: a light thing or two, the worst of them.
            int n = rng.Next(1, 3);
            items = Enumerable.Range(0, n).Select(_ => new Item(rng.PickWeighted(kinds, k => 1 / Categories.Heft(k)), Rarity.Common)).ToList();
        }
        else if (roll < 1 - StrangerTreasure)
        {
            int n = rng.Next(2, 4);
            items = Enumerable.Range(0, n).Select(_ => new Item(rng.PickOne(kinds), Categories.RollQuality(rng, colour))).ToList();
        }
        else
        {
            // A fortune: heavy things, two colours better than the shop's, one of them enchanted.
            int n = rng.Next(3, 5);
            var best = (Rarity)Math.Min(4, (int)colour + 2);
            items = Enumerable.Range(0, n).Select(i => new Item(rng.PickWeighted(kinds, Categories.Heft), Categories.RollQuality(rng, best),
                Magic: i == 0)).ToList();
        }
        var chest = new Chest(items, colour, isSealed: true);
        int ask = Money.Round(Market.TypicalChest(Sellers.All.First(s => s.Tier == tier), colour, tier) * 1.1f);
        return Market.Price(rng, Stranger, chest, tier, ask, Trade.Fixed, claim: Array.Empty<Category>());
    }

    /// <summary>
    /// Gilds a visit: the chest is one of King Aldric's own, a colour better than its seller thinks (gold, if it was
    /// purple), with something enchanted in it. The seller's price stays what it was.
    /// </summary>
    public static Visit Gild(Random rng, Visit visit)
    {
        var old = visit.Chest;
        var colour = (Rarity)Math.Min(4, (int)old.Quality + 1);
        var items = old.Items.Select(i => i with { Quality = Categories.RollQuality(rng, colour) }).ToList();
        if (items.Count == 0) items.Add(new Item(Category.Coins, Categories.RollQuality(rng, colour)));
        int magic = rng.Next(items.Count);
        items[magic] = items[magic] with { Magic = true, Fake = false };
        var chest = new Chest(items, colour, old.Hidden, old.Lock, gilded: true, packed: old.Packed);
        return new Visit(visit.Seller, chest, visit.Claim, visit.Trade, visit.Ask, visit.Floor, visit.Pitch)
        {
            Origin = visit.Origin, Fee = visit.Fee, Clue = visit.Clue,
        };
    }
}
