using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <param name="Bram">What Bram says about it the first time (and the day card shows every time).</param>
/// <param name="MinTier">Only from this shop tier, where it makes a difference.</param>
public sealed record EventDef(string Key, string Name, string Blurb, int MinTier = 1);

/// <summary>
/// Once a week, from the second, something happens in town for a day: a crowd, a festival, a buyer for one kind of
/// thing, a gilded chest, a stranger with a sealed chest, a storm, a shipwreck. Each changes what a good day looks like.
/// </summary>
public static class Events
{
    public const int FromWeek = 2;

    public const int MarketCustomers = 3, StormCustomers = -2;
    public const float FestivalSales = 1.25f, BuyerSales = 2f, WreckPairs = 0.35f;

    public static readonly EventDef[] All =
    {
        new("market", "Market Day", "The whole county's in town: three more customers today."),
        new("festival", "Harvest Festival", "Everyone's in a spending mood: everything sells for a quarter more today."),
        new("buyer", "The Royal Buyer", "The Crown's buyer is in town, paying double for {kind} today."),
        new("gilded", "A Gilded Chest", "Someone's bringing in one of the old royal chests. They think the gold is paint."),
        new("stranger", "The Stranger", "A stranger in black is coming by with a sealed chest. Nothing in it can rattle."),
        new("storm", "A Storm", "Few will brave the rain, but those who do bring their best: chests come in a colour better."),
        new("wreck", "A Shipwreck", "A merchantman ran aground: today's lots come in twos and threes.", MinTier: 3),
    };

    public static EventDef Get(string key) => All.First(e => e.Key == key);

    /// <summary>The event in a few words, for the corner of the counter.</summary>
    public static string Tag(EventDef e, Category kind) => e.Key switch
    {
        "market" => "Market Day: 3 more customers",
        "festival" => "Festival: everything sells +25%",
        "buyer" => $"Royal Buyer: {Categories.Get(kind).Name.ToLowerInvariant()} sells x2",
        "gilded" => "A gilded chest is coming",
        "stranger" => "The Stranger is in town",
        "storm" => "Storm: chests a colour better",
        "wreck" => "Shipwreck: lots in twos and threes",
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
    /// The Stranger: never says where it's from, never lets you open it first. It's packed tight in straw, so nothing
    /// inside can move; all you can tell is how heavy it is. Mostly junk, sometimes a fair lot, now and then a fortune.
    /// </summary>
    public static readonly SellerDef Stranger = new()
    {
        Key = "stranger", Name = "The Stranger", Tier = 1, Judge = Judgment.Clueless,
        Kinds = new[] { 1f, 1, 1, 1, 1, 1 }, Colours = new[] { 1f, 0, 0, 0 },
        Habit = "Sells sealed chests packed tight in straw: nothing rattles. Heavy ones hold more.",
        Pitches = new[] { "Packed tight. No rattles, no questions. {price}.", "Sealed. You'll know when you open it. {price}." },
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
    /// Gilds a visit: the chest is one of the old royal ones, a colour better than its seller thinks (gold, if it was
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
        var chest = new Chest(items, colour, old.Hidden, old.Lock, gilded: true);
        return new Visit(visit.Seller, chest, visit.Claim, visit.Trade, visit.Ask, visit.Floor, visit.Pitch);
    }
}
