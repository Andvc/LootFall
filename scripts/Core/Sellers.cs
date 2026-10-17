using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>How a seller decides what's in their chest.</summary>
public enum Judgment
{
    /// <summary>Shakes it at their own strength and believes their ears.</summary>
    Shaker,
    /// <summary>Shakes it gently, then hard: hears everything there is to hear.</summary>
    Thorough,
    /// <summary>Never shook it and has no idea.</summary>
    Clueless,
    /// <summary>Knows metal; takes everything else for junk, except jewelry, which he takes for diamonds.</summary>
    Miner,
    /// <summary>Knows exactly what's in it, because he packed the fakes himself.</summary>
    Crook,
}

/// <summary>How a seller trades.</summary>
public enum Trade
{
    /// <summary>Names a price. Take it or leave it.</summary>
    Fixed,
    /// <summary>Asks high. You get one offer: they take it, name a final price, or storm off if it's insulting.</summary>
    Haggle,
    /// <summary>Asks you to name a price. Two tries.</summary>
    Offer,
}

/// <summary>
/// Someone who brings a chest to the counter. Each has one habit you can learn, a believable reason for it, and a
/// reward for putting up with it: Vex gives you twelve seconds but brings the finest chests; Marta hates having her
/// chest dropped, and hers are the ones with false bottoms.
/// </summary>
public sealed class SellerDef
{
    public string Key { get; init; }
    public string Name { get; init; }

    /// <summary>The shop tier at which they start coming in.</summary>
    public int Tier { get; init; }

    public Shake Shakes { get; init; }
    public Judgment Judge { get; init; } = Judgment.Shaker;
    public Trade Trade { get; init; } = Trade.Fixed;

    /// <summary>How likely each kind of thing is in their chests, by Category.</summary>
    public float[] Kinds { get; init; }

    /// <summary>How likely each chest colour is, common to epic.</summary>
    public float[] Colours { get; init; }

    /// <summary>What you learn about them, in a sentence.</summary>
    public string Habit { get; init; }

    /// <summary>Lines with {heard} (what they heard) and {price}.</summary>
    public string[] Pitches { get; init; }
    public string[] Accepts { get; init; } = { "Done.", "Pleasure." };
    public string[] Counters { get; init; } = { "{price}, and that's my last word." };
    public string[] Refusals { get; init; } = { "More than that, surely." };
    public string[] Leaves { get; init; } = { "Forget it, then." };
    public string[] Angry { get; init; } = { "Careful! That'll cost you extra." };

    /// <summary>Seconds they'll wait for an answer. 0: all day.</summary>
    public float Patience { get; init; }

    /// <summary>Drop their chest and they put the price up by half.</summary>
    public bool HatesDrop { get; init; }

    /// <summary>Put a magnet to their chest and they put the price up by half.</summary>
    public bool HatesMagnet { get; init; }

    /// <summary>Talks up a chest: often claims a sound that isn't there.</summary>
    public bool Talker { get; init; }

    public float MagicChance { get; init; } = 0.05f;

    /// <summary>How often their chest has a false bottom (from the village shop on).</summary>
    public float HiddenChance { get; init; } = 0.08f;

    /// <summary>How often their chest is locked (from the village shop on).</summary>
    public float LockChance { get; init; } = 0.3f;

    /// <summary>How likely each thing that could be faked is gilded iron.</summary>
    public float FakeChance { get; init; }

    /// <summary>How often their chest holds something their shaking can't hear.</summary>
    public float MissRate { get; init; } = 0.55f;

    /// <summary>The chance each thing they bring is one quality better than its chest's colour suggests.</summary>
    public float QualityBoost { get; init; }

    /// <summary>For a fixed price: how much over what they think it's worth they ask.</summary>
    public (float Min, float Max) Markup { get; init; } = (Market.MarkupMin, Market.MarkupMax);

    /// <summary>What this seller believes is in the chest, by their own shaking (or their own packing).</summary>
    public IEnumerable<Item> Notices(Chest chest) => Judge switch
    {
        Judgment.Crook => chest.Items,
        Judgment.Clueless => Enumerable.Empty<Item>(),
        Judgment.Thorough => chest.HeardAt(Shake.Gentle).Union(chest.HeardAt(Shake.Hard)),
        _ => chest.HeardAt(Shakes),
    };

    /// <summary>What they think one thing of this kind in this chest is worth.</summary>
    public int Guess(Category category, Rarity chest) => Judge switch
    {
        Judgment.Miner when category == Category.Metal => Categories.Expected(category, chest),
        Judgment.Miner when category == Category.Jewels => Categories.Value(category, Rarity.Legendary, false),
        Judgment.Miner => Categories.Expected(category, chest) / 2,
        _ => Categories.Expected(category, chest),
    };

    public string ShakeWord => Judge switch
    {
        Judgment.Thorough => "gently, then hard",
        Judgment.Crook => "doesn't need to",
        _ => Categories.Adverb(Shakes),
    };

    static readonly string[] Titles = { "Little", "Widow", "Old", "Sergeant", "Magister", "Baron", "Captain", "Lady" };

    /// <summary>What Bram calls them: "Penn", "Hob", "Brask".</summary>
    public string Short => Name.Split(' ').First(w => !Titles.Contains(w));
}

public static class Sellers
{
    static readonly string[] Shrug = { "Suit yourself.", "Your loss.", "Another time, then." };

    // Chest colours by shop: farmers bring wood, nobles bring purple.
    static readonly float[] Stall = { 0.75f, 0.25f, 0, 0 }, Village = { 0.15f, 0.7f, 0.15f, 0 },
        Town = { 0, 0.25f, 0.65f, 0.1f }, Emporium = { 0, 0, 0.45f, 0.55f };

    //                          coins metal cloth bottles paper jewels
    public static readonly SellerDef[] All =
    {
        // The roadside stall: farmers and villagers. Each brings one way of trading.
        new()
        {
            Key = "tam", Name = "Little Tam", Tier = 1, Shakes = Shake.Still, Judge = Judgment.Clueless, Trade = Trade.Offer,
            Kinds = new[] { 3f, 2, 2, 2, 1, 0.5f }, Colours = Stall, MissRate = 1f,
            Habit = "Never shakes anything, has no idea of prices. Name a few coins.",
            Pitches = new[] { "Found it by the river. I didn't shake it, it might break! What'll you give me?",
                              "Mum says never shake things. How much will you give me for it?" },
            Accepts = new[] { "Yay!", "I'm buying sweets!" },
            Refusals = new[] { "Is that all? Um... a bit more?" },
            Leaves = new[] { "Aww. I'll ask someone else." },
        },
        new()
        {
            Key = "penn", Name = "Widow Penn", Tier = 1, Shakes = Shake.Gentle,
            Kinds = new[] { 2f, 3, 3, 1, 1, 0.5f }, Colours = Stall,
            Habit = "Shakes gently: heavy and soft things make no sound for her.",
            Pitches = new[] { "I gave it a gentle shake, dear. {heard}. Would {price} do?",
                              "Just a gentle shake, my wrists aren't what they were. {heard}. {price}?" },
            Accepts = new[] { "Thank you. Truly.", "Bless you." },
        },
        new()
        {
            Key = "hob", Name = "Hob the Farmer", Tier = 1, Shakes = Shake.Hard, Trade = Trade.Haggle,
            Kinds = new[] { 2f, 4, 3, 2, 1, 0.3f }, Colours = Stall,
            Habit = "Shakes hard and asks about double. Offer him about half.",
            Pitches = new[] { "Gave it a right hard shake. {heard}. {price}, and that's a bargain!",
                              "Shook it hard as a hay bale. {heard}. Say, {price}?" },
            Accepts = new[] { "Deal! Wait till I tell the missus.", "Done. Easy money!" },
            Counters = new[] { "Hmm. {price}, and that's my last word.", "My wife'd skin me. {price}." },
            Leaves = new[] { "Daylight robbery! Forget it." },
        },
        new()
        {
            Key = "mags", Name = "Mags the Fishwife", Tier = 1, Shakes = Shake.Firm,
            Kinds = new[] { 3f, 3, 1, 3, 1, 0.5f }, Colours = Stall,
            Habit = "Shakes firmly: soft things stay quiet, clanks drown delicate ones.",
            Pitches = new[] { "Gave it a good firm shake, love. {heard}. {price}.", "A firm shake, like a fish crate. {heard}. {price}, love." },
            Accepts = new[] { "Pleasure doing business!", "Done, love." },
        },

        // The village shop: soldiers and merchants. Locks and false bottoms.
        new()
        {
            Key = "brask", Name = "Sergeant Brask", Tier = 2, Shakes = Shake.Hard,
            Kinds = new[] { 2f, 5, 2, 2, 2, 0.5f }, Colours = Village, LockChance = 0.8f,
            Habit = "Shakes hard, never haggles. Army lockboxes, nearly always locked.",
            Pitches = new[] { "Shook it like a new recruit. {heard}. {price}.", "Army lockbox. Hard shake: {heard}. {price}, and cheap at that." },
            Accepts = new[] { "Done.", "Pleasure, citizen." },
        },
        new()
        {
            Key = "tobias", Name = "Tobias the Tinker", Tier = 2, Shakes = Shake.Firm, Trade = Trade.Haggle, Talker = true,
            Kinds = new[] { 3f, 4, 1, 2, 1, 1 }, Colours = Village,
            Habit = "Shakes firmly, haggles, and often claims a sound that isn't there.",
            Pitches = new[] { "Firm shake, and I've a good ear. {heard}. {price}.", "Gave it a firm shake. {heard}, I'm sure of it. {price}?" },
            Accepts = new[] { "Done!", "Good doing business." },
            Counters = new[] { "{price}, and you're robbing me.", "Tch. {price}, final." },
            Leaves = new[] { "Hmph. Next time." },
        },
        new()
        {
            Key = "marta", Name = "Old Marta", Tier = 2, Shakes = Shake.Gentle, HatesDrop = true,
            Kinds = new[] { 2f, 2, 2, 2, 3, 1 }, Colours = Village, HiddenChance = 0.4f,
            Habit = "Shakes gently. Old chests, often with false bottoms. Drop one: price +50%.",
            Pitches = new[] { "My mother's chest. A gentle shake: {heard}. {price}, dear.", "Gently does it. {heard}. {price}." },
            Accepts = new[] { "Fair's fair.", "Take good care of it." },
            Angry = new[] { "That was my mother's chest! For that, it's half again." },
        },

        // The town shop: mages, and a miner with jewels he doesn't understand.
        new()
        {
            Key = "wren", Name = "Wren the Apprentice", Tier = 3, Shakes = Shake.Gentle, Trade = Trade.Offer, MagicChance = 0.15f,
            Kinds = new[] { 1f, 2, 3, 3, 3, 2 }, Colours = Town,
            Habit = "Shakes gently, asks you to name a price. Brings magic unknowingly.",
            Pitches = new[] { "I shook it gently, like Master says. {heard}. What would you give for it?",
                              "Gently does it. {heard}. Um, what's it worth to you?" },
            Accepts = new[] { "Oh good! Don't tell Master.", "Thank you!" },
            Refusals = new[] { "Master said not to take too little. A bit more?" },
            Leaves = new[] { "I should ask Master first. Sorry!" },
        },
        new()
        {
            Key = "orrin", Name = "Magister Orrin", Tier = 3, Shakes = Shake.Hard, MagicChance = 0.15f, Markup = (1.8f, 2.2f),
            Kinds = new[] { 1f, 4, 3, 3, 3, 2 }, Colours = Town,
            Habit = "Shakes hard, asks a proud price, never checks for magic.",
            Pitches = new[] { "A vigorous shake. {heard}. {price}.", "Surplus from my study. I shook it hard. {heard}. {price}." },
            Accepts = new[] { "Very well.", "Mind the scrolls." },
        },
        new()
        {
            Key = "grit", Name = "Grit the Miner", Tier = 3, Shakes = Shake.Hard, Judge = Judgment.Miner, Trade = Trade.Haggle,
            Kinds = new[] { 1f, 5, 1, 0.5f, 1, 4 }, Colours = Town,
            Habit = "Knows metal. Prices the rest at half, but takes jewelry for diamonds.",
            Pitches = new[] { "Hard shake, miner's hands. {heard}. {price}.", "Dug it out of the old seam. {heard}. {price}." },
            Accepts = new[] { "Done. Back to the pit.", "Ha! Done." },
            Counters = new[] { "{price}. Lower and I'll eat my helmet." },
            Leaves = new[] { "You take me for a fool? Good day." },
        },

        // The Emporium: nobles, adventurers, a sharp dealer and a crook.
        new()
        {
            Key = "vex", Name = "Baron Vex", Tier = 4, Shakes = Shake.Firm, Patience = 12f, QualityBoost = 0.4f, MissRate = 0.45f, Markup = (1.7f, 2.1f),
            Kinds = new[] { 4f, 1, 2, 2, 1, 4 }, Colours = new[] { 0, 0, 0.2f, 0.8f },
            Habit = "Gives you 12 seconds. Finest chests, often better than their colour.",
            Pitches = new[] { "My butler gave it a firm shake. {heard}. {price}. You have twelve seconds.",
                              "A Vex heirloom. {heard}. {price}. Quickly, I have a ball to attend." },
            Accepts = new[] { "Splendid.", "You have the eye of a gentleman." },
            Leaves = new[] { "Too slow. Good DAY." },
        },
        new()
        {
            Key = "gull", Name = "Captain Gull", Tier = 4, Shakes = Shake.Hard, Trade = Trade.Haggle, HiddenChance = 0.25f, MissRate = 0.45f,
            Kinds = new[] { 3f, 4, 1, 3, 2, 3 }, Colours = Emporium,
            Habit = "Shakes hard, haggles. Sea chests, often with a false bottom.",
            Pitches = new[] { "Shook it like a gale. {heard}. {price}.", "Hauled it off the reef and shook it hard. {heard}. {price}." },
            Accepts = new[] { "Aye.", "Tide's turning, I'm off." },
            Counters = new[] { "{price}. Not a copper less." },
            Leaves = new[] { "Waste of a tide." },
        },
        new()
        {
            Key = "pip", Name = "Pip the Sharp", Tier = 4, Shakes = Shake.Hard, Judge = Judgment.Thorough, MissRate = 0f, HiddenChance = 0.2f,
            Kinds = new[] { 3f, 3, 2, 2, 2, 3 }, Colours = Emporium,
            Habit = "Hears everything. Only magic and false bottoms get past him.",
            Pitches = new[] { "Shook it gently, then hard. {heard}. {price}, and it's fair.", "I listened properly. {heard}. {price}." },
            Accepts = new[] { "Pleasure.", "You won't regret it. Probably." },
        },
        new()
        {
            Key = "ermine", Name = "Lady Ermine", Tier = 4, Shakes = Shake.Gentle, HatesMagnet = true, FakeChance = 0.25f, Markup = (1.1f, 1.3f),
            Kinds = new[] { 3f, 1, 2, 2, 1, 5 }, Colours = Emporium,
            Habit = "Modest prices, but some jewels are paste. Use a magnet: price +50%.",
            Pitches = new[] { "I shook it gently, of course. {heard}. {price}.", "My jewels. A gentle shake: {heard}. {price}." },
            Accepts = new[] { "Charming.", "Do look after them." },
            Angry = new[] { "A magnet? On MY jewels? Half again, for the insult." },
        },
        new()
        {
            Key = "silas", Name = "Silas", Tier = 4, Shakes = Shake.Hard, Judge = Judgment.Crook, MissRate = 0f, FakeChance = 0.6f,
            HiddenChance = 0f, Kinds = new[] { 5f, 1, 1, 1, 1, 5 }, Colours = Emporium,
            Habit = "Sells gilded iron as gold and jewels, cheap.",
            Pitches = new[] { "{heard}, friend. Genuine. I'm leaving town tonight, so {price}.", "{heard}. Very rare. For you, just {price}." },
            Accepts = new[] { "Wonderful! No refunds.", "A pleasure. I must be going." },
        },
    };

    public static SellerDef Get(string key) => All.First(s => s.Key == key);

    public static string PassLine(Random rng) => rng.PickOne(Shrug);

    /// <summary>"Heard a jingle and a clank", or "Not a sound".</summary>
    public static string Heard(IReadOnlyList<Category> claim)
    {
        if (claim.Count == 0) return "Not a sound";
        var words = claim.Select(c => $"a {Categories.Get(c).Word}").ToList();
        string list = words.Count == 1 ? words[0] : string.Join(", ", words.Take(words.Count - 1)) + " and " + words[^1];
        return $"Heard {list}";
    }
}

/// <summary>Builds a visit: what the seller brings, what they say is in it, and how they price it.</summary>
public static class Market
{
    /// <summary>Fixed-price sellers ask this much over what they think it's worth.</summary>
    public const float MarkupMin = 1.4f, MarkupMax = 1.8f;

    /// <summary>Hagglers open this high, and will come down to their bottom.</summary>
    public const float HaggleAskMin = 1.7f, HaggleAskMax = 2.1f, BottomMin = 1.05f, BottomMax = 1.25f;

    /// <summary>An offer this far under the asking price is an insult.</summary>
    public const float Insult = 0.35f;

    /// <summary>A crook asks this much of what the real thing would be worth: too good to be true.</summary>
    public const float CrookMin = 0.5f, CrookMax = 0.7f;

    /// <summary>How often a talker claims a sound that isn't there.</summary>
    public const float PhantomChance = 0.6f;

    /// <summary>Angry sellers put the price up by this much.</summary>
    public const float AngerMarkup = 1.5f;

    /// <summary>How many things a seller of each tier brings, with weights.</summary>
    static readonly (int Count, float Weight)[][] Counts =
    {
        null,
        new[] { (1, 0.35f), (2, 0.55f), (3, 0.1f) },
        new[] { (2, 0.4f), (3, 0.6f) },
        new[] { (2, 0.5f), (3, 0.5f) },
        new[] { (3, 0.6f), (4, 0.4f) },
    };

    /// <param name="betterChance">From the shop's exhibition: the chance the chest is one colour better.</param>
    public static Visit CreateVisit(Random rng, SellerDef seller, int shopTier, float betterChance = 0f)
    {
        var colour = (Rarity)rng.PickWeighted(Enumerable.Range(0, 4).ToArray(), i => seller.Colours[i]);
        if (colour < Rarity.Epic && rng.NextFloat() < betterChance) colour++;

        // Whether this one holds something the seller's shaking can't hear is rolled first, so each seller's blind spot
        // turns up about as often as their MissRate: often enough to pay for listening, not so often that buying
        // blind pays.
        bool missing = rng.NextFloat() < seller.MissRate;
        var chest = Roll(rng, seller, shopTier, colour);
        for (int attempt = 0; attempt < 40 && seller.Notices(chest).Count() < chest.Items.Count != missing; attempt++)
            chest = Roll(rng, seller, shopTier, colour);
        return Price(rng, seller, chest, shopTier);
    }

    static Chest Roll(Random rng, SellerDef seller, int shopTier, Rarity colour)
    {
        var kinds = Categories.UpTo(shopTier).Select(c => c.Category).ToList();
        int count = Math.Min(kinds.Count, rng.PickWeighted(Counts[seller.Tier], c => c.Weight).Count);
        var picked = new List<Category>();
        if (seller.Judge == Judgment.Crook) picked.Add(rng.PickWeighted(kinds.Where(Fakeable).ToArray(), c => seller.Kinds[(int)c]));
        while (picked.Count < count)
            picked.Add(rng.PickWeighted(kinds.Except(picked).ToArray(), c => seller.Kinds[(int)c]));

        float magicChance = shopTier >= 3 ? seller.MagicChance : 0f;
        var items = picked.Select(c =>
        {
            bool fake = Fakeable(c) && rng.NextFloat() < seller.FakeChance;
            bool magic = !fake && rng.NextFloat() < magicChance;
            var quality = Categories.RollQuality(rng, colour);
            if (quality < Rarity.Legendary && rng.NextFloat() < seller.QualityBoost) quality++;
            return new Item(c, quality, magic, fake);
        }).ToList();

        // People hide money, jewels and papers under a false bottom.
        Item hidden = null;
        if (shopTier >= 2 && rng.NextFloat() < seller.HiddenChance)
        {
            var hideable = HiddenKinds.Where(k => Categories.Get(k.Kind).Tier <= shopTier).ToArray();
            hidden = new Item(rng.PickWeighted(hideable, k => k.Weight).Kind, Categories.RollQuality(rng, colour));
        }
        var lockTier = shopTier >= 2 && rng.NextFloat() < seller.LockChance ? LockTiers.For(colour) : null;
        return new Chest(items, colour, hidden, lockTier);
    }

    static readonly (Category Kind, float Weight)[] HiddenKinds =
        { (Category.Coins, 3f), (Category.Jewels, 3f), (Category.Paper, 2f) };

    /// <summary>What a false bottom is worth on average in a chest of this colour, at this shop. The slip shows this.</summary>
    public static int ExpectedHidden(Rarity chest, int shopTier)
    {
        var kinds = HiddenKinds.Where(k => Categories.Get(k.Kind).Tier <= shopTier).ToArray();
        return (int)MathF.Round(kinds.Sum(k => k.Weight * Categories.Expected(k.Kind, chest)) / kinds.Sum(k => k.Weight));
    }

    static bool Fakeable(Category category) => Categories.Get(category).FakeName != null;

    /// <summary>The seller judges the chest their way, says what they heard, and prices it their way.</summary>
    /// <param name="ask">A scripted asking price (the first day).</param>
    /// <param name="trade">A scripted way of trading (the first day).</param>
    /// <param name="pitch">A scripted line, with {price}.</param>
    public static Visit Price(Random rng, SellerDef seller, Chest chest, int shopTier, int? ask = null, Trade? trade = null, string pitch = null)
    {
        var claim = seller.Notices(chest).Select(i => i.Category).ToList();
        if (seller.Talker && rng.NextFloat() < PhantomChance)
        {
            var absent = Categories.UpTo(shopTier).Select(c => c.Category).Where(c => !chest.Has(c) && !claim.Contains(c)).ToArray();
            if (absent.Length > 0) claim.Add(rng.PickWeighted(absent, c => seller.Kinds[(int)c]));
        }
        claim.Sort();

        // Everyone prices what they think is there as plain goods: nobody can hear magic, or what's under a false bottom.
        float estimate = claim.Sum(c => seller.Guess(c, chest.Quality));
        var format = trade ?? seller.Trade;
        float price, floor;
        switch (format)
        {
            case Trade.Haggle when estimate > 0:
                price = estimate * rng.Between(HaggleAskMin, HaggleAskMax);
                // A miner has no idea what his "diamonds" are worth, so he folds easily.
                floor = estimate * (seller.Judge == Judgment.Miner ? rng.Between(0.85f, 1f) : rng.Between(BottomMin, BottomMax));
                break;
            case Trade.Haggle:
                price = seller.Tier * rng.Between(4f, 8f);
                floor = price * 0.5f;
                break;
            case Trade.Offer:
                price = 0;
                floor = estimate > 0 ? estimate * rng.Between(0.9f, 1.15f) : seller.Tier * rng.Between(2f, 6f);
                break;
            default:
                price = seller.Judge == Judgment.Crook ? estimate * rng.Between(CrookMin, CrookMax)
                    : estimate == 0 ? seller.Tier * rng.Between(2f, 4f)
                    : estimate * rng.Between(seller.Markup.Min, seller.Markup.Max);
                floor = price;
                break;
        }
        int asking = ask ?? Money.Round(price);
        string line = (pitch ?? rng.PickOne(seller.Pitches)).Replace("{heard}", Sellers.Heard(claim)).Replace("{price}", $"{asking}g");
        return new Visit(seller, chest, claim, format, format == Trade.Offer ? 0 : asking, Money.Round(floor), line);
    }
}

public enum VisitOutcome { Deciding, Bought, Passed, Left }

public enum ResponseKind { Accept, Counter, TryAgain, Leave }

public readonly record struct Response(ResponseKind Kind, int Price, string Line);

/// <summary>
/// One seller at the counter with one chest, what they say is in it, and how they trade. Fixed: buy at their price or
/// pass. Haggle: one offer, which they take, answer with a final price, or storm off at if it's insulting. Offer: you
/// name a price, twice at most.
/// </summary>
public sealed class Visit
{
    public SellerDef Seller { get; }
    public Chest Chest { get; }

    /// <summary>The kinds of things the seller says they heard. Their price is for these.</summary>
    public IReadOnlyList<Category> Claim { get; }

    public Trade Trade { get; }

    /// <summary>The asking price; 0 while they're waiting for you to name one.</summary>
    public int Ask { get; private set; }

    public string Pitch { get; }
    public VisitOutcome Outcome { get; private set; } = VisitOutcome.Deciding;
    public int PricePaid { get; private set; }
    public int OffersMade { get; private set; }
    public bool Angered { get; private set; }

    /// <summary>Hidden: the least they'll take.</summary>
    internal int Floor { get; private set; }

    public Visit(SellerDef seller, Chest chest, IReadOnlyList<Category> claim, Trade trade, int ask, int floor, string pitch)
    {
        Seller = seller;
        Chest = chest;
        Claim = claim;
        Trade = trade;
        Ask = ask;
        Floor = Math.Max(1, floor);
        Pitch = pitch;
    }

    public bool Deciding => Outcome == VisitOutcome.Deciding;

    /// <summary>Whether you can make an offer now.</summary>
    public bool CanOffer => Deciding && (Trade == Trade.Haggle ? OffersMade == 0 : Trade == Trade.Offer && Ask == 0);

    /// <summary>Whether there's a price on the table you can just pay.</summary>
    public bool CanBuy => Deciding && Ask > 0;

    public Response Offer(int amount, Random rng)
    {
        if (!CanOffer) throw new InvalidOperationException("No offer to make");
        OffersMade++;
        amount = Math.Max(1, amount);
        if (amount >= Floor) return new Response(ResponseKind.Accept, amount, Sell(amount, rng));
        if (Trade == Trade.Offer)
        {
            if (OffersMade == 1) return new Response(ResponseKind.TryAgain, 0, rng.PickOne(Seller.Refusals));
            return Leave(rng);
        }
        if (amount < Ask * Market.Insult) return Leave(rng);
        // Not enough: they meet you partway, never below what they'd really take, and that's final.
        Ask = Math.Max(Floor, Money.Round((amount + Ask) / 2f));
        return new Response(ResponseKind.Counter, Ask, rng.PickOne(Seller.Counters).Replace("{price}", $"{Ask}g"));
    }

    Response Leave(Random rng)
    {
        Outcome = VisitOutcome.Left;
        return new Response(ResponseKind.Leave, 0, rng.PickOne(Seller.Leaves));
    }

    public string Buy(Random rng) => Sell(Ask, rng);

    public string Pass(Random rng)
    {
        Outcome = VisitOutcome.Passed;
        return Sellers.PassLine(rng);
    }

    /// <summary>They ran out of patience and took their chest home.</summary>
    public string TimeUp(Random rng) => Leave(rng).Line;

    /// <summary>Dropping or magnetizing what they asked you not to: up goes the price, once.</summary>
    public string Anger(Random rng)
    {
        if (Angered || !Deciding) return null;
        Angered = true;
        if (Ask > 0) Ask = Money.Round(Ask * Market.AngerMarkup);
        Floor = Money.Round(Floor * Market.AngerMarkup);
        return rng.PickOne(Seller.Angry);
    }

    string Sell(int price, Random rng)
    {
        Outcome = VisitOutcome.Bought;
        PricePaid = price;
        return rng.PickOne(Seller.Accepts);
    }

    /// <summary>
    /// Why this chest was a good or bad deal, in a sentence or two: what the seller missed and why, what they made up,
    /// faked or misjudged, and what nobody could have heard.
    /// </summary>
    public string Explain()
    {
        string name = Seller.Short;
        var fakes = Chest.Items.Where(i => i.Fake).ToList();
        var lines = new List<string>();
        if (fakes.Count > 0)
            lines.Add($"{Join(fakes.Select(i => i.Name))}: gilded iron. A magnet would have silenced {(fakes.Count == 1 ? "it" : "them")}.");
        else if (Seller.Judge == Judgment.Crook)
            lines.Add($"All genuine this time, and {name} sold it for less than it's worth.");

        var phantoms = Claim.Where(c => !Chest.Has(c)).ToList();
        if (phantoms.Count > 0)
            lines.Add($"{name} made up the {Categories.Get(phantoms[0]).Word}: there was no {Categories.Get(phantoms[0]).Name.ToLowerInvariant()}.");

        if (Seller.Judge == Judgment.Miner)
        {
            var jewels = Chest.Get(Category.Jewels);
            if (jewels != null && Claim.Contains(Category.Jewels))
                lines.Add($"{name} priced the {jewels.Name} like a diamond fit for a crown.");
            var junk = Chest.Items.Where(i => Claim.Contains(i.Category) && i.Category is not (Category.Metal or Category.Jewels)).ToList();
            if (junk.Count > 0) lines.Add($"He took the {Join(junk.Select(i => i.Word))} for junk.");
        }

        var noticed = Seller.Notices(Chest).ToHashSet();
        var unheard = Chest.Items.Where(i => !noticed.Contains(i) && !i.Fake).ToList();
        if (unheard.Count > 0 && Seller.Judge != Judgment.Crook)
        {
            string why = Seller.Judge == Judgment.Clueless ? $"{name} never shook it"
                : unheard.All(i => i.Def.Delicate && Chest.Clanks(Seller.Shakes)) ? $"The clank drowned out the {Join(unheard.Select(i => i.Word))} for {name}"
                : unheard.All(i => i.Def.StirsAt > Seller.Shakes) ? $"{name} didn't shake hard enough to stir the {Join(unheard.Select(i => i.Word))}"
                : $"{name} shook {Categories.Adverb(Seller.Shakes)} and missed the {Join(unheard.Select(i => i.Word))}";
            lines.Add($"{why}: {Join(unheard.Select(i => i.Name))}, unpriced.");
        }
        if (Chest.Hidden != null) lines.Add($"A false bottom nobody knew about: {Chest.Hidden.Name}.");
        var magic = Chest.Everything.Where(i => i.Magic).ToList();
        if (magic.Count > 0) lines.Add($"Nobody can hear magic: the {Join(magic.Select(i => i.Name))} went for the price of plain goods.");
        if (lines.Count == 0)
            lines.Add(Trade == Trade.Fixed
                ? $"{name} heard everything, and always asks a bit more than it's worth."
                : $"{name} heard everything. The deal was in the price you agreed.");
        return string.Join(" ", lines);
    }

    static string Join(IEnumerable<string> parts)
    {
        var list = parts.ToList();
        return list.Count <= 1 ? list.FirstOrDefault() ?? "" : string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1];
    }
}
