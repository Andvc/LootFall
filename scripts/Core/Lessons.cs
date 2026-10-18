using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>What a lesson step can check: the visit, the slip, what's been heard, and the shop.</summary>
public sealed class LessonCheck
{
    public Visit Visit { get; init; }
    public Appraisal Slip { get; init; }
    public Hearing Hearing { get; init; }
    public GameState State { get; init; }

    /// <summary>The chest has been shaken (without the magnet) at all these strengths.</summary>
    public bool Tried(params Shake[] levels) => levels.All(l => Hearing.HasTried(l, false));
}

/// <summary>
/// One thing Bram asks you to do, and how to tell it's done. With no check, it's something to read: click on.
/// </summary>
/// <param name="Done">Whether it's done. Checked every frame, so it can be done before it's asked.</param>
/// <param name="Trades">The step is done by trading, so the trade buttons are open during it.</param>
/// <param name="When">Only for shops where this holds (a tool you own, or don't).</param>
public sealed record LessonStep(string Say, Func<LessonCheck, bool> Done = null, bool Trades = false, Func<GameState, bool> When = null);

/// <summary>
/// The first time a seller comes in (with tips on), their visit is a lesson: a chest built to show off their habit,
/// and Bram's steps. Until the steps are done the trade buttons wait (unless a step is about trading); the lesson can
/// be skipped. Every lesson's chest guarantees what it teaches: Marta's has a false bottom, Pell's has three purses.
/// </summary>
public sealed class Lesson
{
    public string Seller { get; init; }

    /// <summary>Builds the visit, at the shop tier it happens at.</summary>
    public Func<Random, int, Visit> Make { get; init; }

    public LessonStep[] Steps { get; init; }

    /// <summary>The steps that apply at this shop.</summary>
    public IReadOnlyList<LessonStep> For(GameState state) => Steps.Where(s => s.When == null || s.When(state)).ToList();
}

public static class Lessons
{
    static Item I(Category kind, Rarity quality, bool magic = false, bool fake = false) => new(kind, quality, magic, fake);

    static Visit Price(Random rng, string seller, Chest chest, int tier, int? ask = null, IEnumerable<Category> claim = null, int? floor = null,
        string pitch = null) =>
        Market.Price(rng, Sellers.Get(seller), chest, tier, ask, null, pitch, claim, floor);

    static bool NoTrumpet(GameState s) => !s.Has("trumpet");

    static readonly Dictionary<string, Lesson> ByKey = new Lesson[]
    {
        // The stall. Tam, Hob and Mags were met on the first day; their lessons come on their first ordinary visit.
        new()
        {
            Seller = "tam",
            Make = (rng, tier) => Price(rng, "tam", new Chest(new[] { I(Category.Coins, 0), I(Category.Cloth, 0) }), tier, floor: 2),
            Steps = new LessonStep[]
            {
                new("Tam never shakes anything and has no idea what it's worth: he'll ask you to name a price. First, shake it " +
                    "at every strength.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick what you heard. Then set the slider to a few coins and make your offer: he'll take almost anything.",
                    c => c.Visit.OffersMade > 0, Trades: true),
            },
        },
        new()
        {
            Seller = "hob",
            Make = (rng, tier) =>
            {
                var visit = Price(rng, "hob", new Chest(new[] { I(Category.Coins, 0), I(Category.Metal, 0) }), tier);
                visit.SetFloor(Money.Round(visit.Ask * 0.45f));
                return visit;
            },
            Steps = new LessonStep[]
            {
                new("Hob shook it hard and heard a clank. He haggles: he always asks about double. Shake it yourself, gently " +
                    "too.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick what you heard. Then use the slider to offer him about half his price. You get one offer.",
                    c => c.Visit.OffersMade > 0, Trades: true),
            },
        },
        new()
        {
            Seller = "mags",
            Make = (rng, tier) => Price(rng, "mags", new Chest(new[] { I(Category.Coins, 0) }), tier,
                ask: Money.Round(Categories.Nominal(Category.Metal, Rarity.Common) * 1.4f), pitch: "One rattle, like fish in a crate. A rattle's a rattle: {price}, love."),
            Steps = new LessonStep[]
            {
                new("Mags counts the rattles and charges the same for each, whatever's rattling: a purse costs what a rusty " +
                    "knife does. Shake it: what's rattling?", c => c.Tried(Shake.Gentle)),
                new("A jingle: coins, worth more than a knife. At knife prices, that's a bargain."),
            },
        },

        // The village shop.
        new()
        {
            Seller = "brask",
            Make = (rng, tier) => Price(rng, "brask",
                new Chest(new[] { I(Category.Bottles, Rarity.Uncommon), I(Category.Paper, Rarity.Uncommon) }, Rarity.Uncommon, null,
                    LockTiers.For(Rarity.Uncommon)), tier),
            Steps = new LessonStep[]
            {
                new("Brask goes by weight: heavy means valuable. But bottles and paper weigh next to nothing. Shake it at " +
                    "every strength.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("A slosh and a rustle: good things, and light. Tick them: at his price, a bargain. It's locked, mind: " +
                    "you'll pick it after.", c => c.Slip.Count(Category.Bottles) > 0 && c.Slip.Count(Category.Paper) > 0),
            },
        },
        new()
        {
            Seller = "tobias",
            Make = (rng, tier) => Price(rng, "tobias", new Chest(new[] { I(Category.Metal, Rarity.Uncommon), I(Category.Paper, Rarity.Uncommon) },
                Rarity.Uncommon), tier, claim: new[] { Category.Metal, Category.Bottles }),
            Steps = new LessonStep[]
            {
                new("Tobias says he heard a slosh. He talks things up. Bottles slosh at the gentlest shake: shake it gently.",
                    c => c.Tried(Shake.Gentle)),
                new("No slosh? He made it up. Untick Bottles.", c => c.Slip.Count(Category.Bottles) == 0),
                new("He only shook it firmly, so he'd never hear soft things. Shake it hard.", c => c.Tried(Shake.Hard)),
                new("A rustle: paper he never priced. Tick it, then haggle.", c => c.Slip.Count(Category.Paper) > 0),
            },
        },
        new()
        {
            Seller = "marta",
            Make = (rng, tier) => Price(rng, "marta", new Chest(new[] { I(Category.Coins, Rarity.Uncommon), I(Category.Paper, Rarity.Uncommon) },
                Rarity.Uncommon, I(Category.Coins, Rarity.Uncommon)), tier),
            Steps = new LessonStep[]
            {
                new("Marta's old family chests often have a false bottom she's forgotten. Lift the chest high and let go: drop " +
                    "it, and listen.", c => c.Hearing.Dropped),
                new("A hollow BOOM: something's under the floor. Tick False bottom: nobody's priced it.", c => c.Slip.Hidden),
            },
        },
        new()
        {
            Seller = "wick",
            Make = (rng, tier) => Price(rng, "wick", new Chest(new[] { I(Category.Coins, Rarity.Uncommon), I(Category.Cloth, Rarity.Uncommon) },
                Rarity.Uncommon, packed: true), tier),
            Steps = new LessonStep[]
            {
                new("Granny Wick won't have her chests shaken hard. Shake it gently.", c => c.Tried(Shake.Gentle), When: NoTrumpet),
                new("Nothing? Now firmly.", c => c.Tried(Shake.Firm), When: NoTrumpet),
                new("A jingle, but only at a firm shake. Cloth packed around things muffles them, so they sound one shake late. " +
                    "There's cloth in there: tick it.", c => c.Slip.Count(Category.Cloth) > 0, When: NoTrumpet),
                new("Padded metal only clanks at a hard shake, and she won't allow one. An Ear Trumpet hears through cloth.",
                    When: NoTrumpet),
                new("Granny Wick won't have her chests shaken hard, and they're packed with cloth that muffles things. Your " +
                    "trumpet hears through it. Shake it gently.", c => c.Tried(Shake.Gentle), When: s => s.Has("trumpet")),
                new("She only heard the jingle. Her chests are always packed with cloth, even when nobody can shake hard " +
                    "enough to hear it.", When: s => s.Has("trumpet")),
            },
        },

        // The town shop.
        new()
        {
            Seller = "wren",
            Make = (rng, tier) => Price(rng, "wren", new Chest(new[] { I(Category.Bottles, Rarity.Uncommon, magic: true), I(Category.Coins, Rarity.Uncommon) },
                Rarity.Uncommon), tier),
            Steps = new LessonStep[]
            {
                new("Wren can't tell what's in a chest, but she feels magic. Name a low price: a few coins.", c => c.Visit.OffersMade > 0,
                    Trades: true),
                new("She wants more: she can feel something special. Something in there is magic, worth four times as much. " +
                    "Offer again, if it's worth it to you.", c => c.Visit.OffersMade > 1, Trades: true),
            },
        },
        new()
        {
            Seller = "orrin",
            Make = (rng, tier) =>
            {
                var chest = new Chest(new[] { I(Category.Coins, Rarity.Rare), I(Category.Metal, Rarity.Rare), I(Category.Paper, Rarity.Rare) }, Rarity.Rare);
                return Price(rng, "orrin", chest, tier, ask: Money.Round(Market.TypicalChest(Sellers.Get("orrin"), Rarity.Rare, tier) * 0.6f));
            },
            Steps = new LessonStep[]
            {
                new("Orrin's prices come from his dreams, not his chests. Shake it at every strength and add it up yourself.",
                    c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Compare your slip with his dream. Today's dream is cheap. Tomorrow's might be absurd."),
            },
        },
        new()
        {
            Seller = "grit",
            Make = (rng, tier) => Price(rng, "grit", new Chest(new[] { I(Category.Jewels, Rarity.Uncommon), I(Category.Paper, Rarity.Rare) }, Rarity.Rare), tier),
            Steps = new LessonStep[]
            {
                new("Hear that tinkle? Jewelry. Grit's a miner: he knows metal, and thinks every tinkle is a diamond. Shake it " +
                    "gently yourself.", c => c.Tried(Shake.Gentle)),
                new("Jewelry, priced like the crown jewels. Offer what it's really worth, or let him keep it."),
            },
        },
        new()
        {
            Seller = "zara",
            Make = (rng, tier) => Price(rng, "zara", new Chest(new[] { I(Category.Bottles, Rarity.Rare), I(Category.Paper, Rarity.Rare) }, Rarity.Rare,
                I(Category.Jewels, Rarity.Rare)), tier),
            Steps = new LessonStep[]
            {
                new("Madame Zara prices by the chest's colour, and she feels magic, so that's in her price. Half her trick boxes " +
                    "hide a false bottom she never prices."),
                new("But she hates them dropped: half again if you do. Tick False bottom on faith, or drop it and pay for " +
                    "certainty. Your call."),
            },
        },
        new()
        {
            Seller = "pell",
            Make = (rng, tier) => Price(rng, "pell", new Chest(new[]
            {
                I(Category.Coins, Rarity.Rare), I(Category.Coins, Rarity.Rare), I(Category.Coins, Rarity.Rare), I(Category.Paper, Rarity.Rare),
            }, Rarity.Rare), tier),
            Steps = new LessonStep[]
            {
                new("Pell sells pawn by the lot, two or three of a kind, and only says which kinds he heard. Shake it gently and " +
                    "count: each purse jingles from its own spot.", c => c.Tried(Shake.Gentle)),
                new("Three jingles from three spots: three purses. He priced one. Click Coins until the slip says 3.",
                    c => c.Slip.Count(Category.Coins) == 3),
            },
        },
        new()
        {
            Seller = "shelly",
            Make = (rng, tier) => Price(rng, "shelly", new Chest(new[] { I(Category.Paper, Rarity.Rare, fake: true), I(Category.Bottles, Rarity.Rare) },
                Rarity.Rare), tier),
            Steps = new LessonStep[]
            {
                new("Shelly heard a rustle and swears it's a treasure map. Shake it gently.", c => c.Tried(Shake.Gentle)),
                new("A rustle at a gentle shake? Paper only rustles when it's shaken hard. That's sand pouring. Untick Paper.",
                    c => c.Slip.Count(Category.Paper) == 0),
            },
        },

        // The Emporium.
        new()
        {
            Seller = "vex",
            Make = (rng, tier) => Price(rng, "vex", new Chest(new[] { I(Category.Coins, Rarity.Epic), I(Category.Jewels, Rarity.Legendary), I(Category.Metal, Rarity.Epic) },
                Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("Baron Vex never looks inside, and gives you twelve seconds. His clock starts when you close this. Shake it " +
                    "gently, then hard. Quick!"),
                new("Gently, then hard!", c => c.Tried(Shake.Gentle, Shake.Hard)),
            },
        },
        new()
        {
            Seller = "gull",
            Make = (rng, tier) => Price(rng, "gull", new Chest(new[] { I(Category.Metal, Rarity.Epic), I(Category.Coins, Rarity.Rare) }, Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("Gull always swears there's a smuggler's false bottom, and prices it in. Drop it and listen.", c => c.Hearing.Dropped),
                new("A solid thud: no false bottom. Untick it, then haggle.", c => !c.Slip.Hidden),
            },
        },
        new()
        {
            Seller = "pip",
            Make = (rng, tier) => Price(rng, "pip", new Chest(new[] { I(Category.Metal, Rarity.Rare), I(Category.Paper, Rarity.Rare, magic: true) },
                Rarity.Rare, I(Category.Jewels, Rarity.Rare)), tier),
            Steps = new LessonStep[]
            {
                new("Pip hears everything. But she can't see magic, and she never drops a chest. Shake it at every strength under " +
                    "the witchlight.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard), When: s => s.Has("lamp")),
                new("Something glowed: click the star on its row to mark it magic.", c => c.Slip.Magic(Category.Paper), When: s => s.Has("lamp")),
                new("Pip hears everything. But she never checks for magic, and never drops a chest.", When: s => !s.Has("lamp")),
                new("Now drop it.", c => c.Hearing.Dropped),
                new("A boom! Tick False bottom: she never priced it.", c => c.Slip.Hidden),
            },
        },
        new()
        {
            Seller = "ermine",
            Make = (rng, tier) => Price(rng, "ermine", new Chest(new[] { I(Category.Jewels, Rarity.Epic), I(Category.Paper, Rarity.Epic), I(Category.Bottles, Rarity.Epic) },
                Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("Lady Ermine prices only her jewelry, at what she paid for it. Whatever else the maid packed comes free. Shake " +
                    "it at every strength.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick everything you heard: the rest is a gift. Some of her jewels are paste, though, and she won't have a " +
                    "magnet near them.", c => c.Slip.Count(Category.Paper) > 0 && c.Slip.Count(Category.Bottles) > 0),
            },
        },
        new()
        {
            Seller = "silas",
            Make = (rng, tier) => Price(rng, "silas", new Chest(new[] { I(Category.Coins, Rarity.Epic, fake: true), I(Category.Jewels, Rarity.Rare) },
                Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("Silas sells gilded iron as gold, and it sounds just the same. Turn the magnet on and shake it gently.",
                    c => c.Hearing.HasTried(Shake.Gentle, true), When: s => s.Has("magnet")),
                new("The jingle went quiet: iron, gilded. Untick Coins.", c => c.Slip.Count(Category.Coins) == 0, When: s => s.Has("magnet")),
                new("Silas sells gilded iron as gold and jewels, and it sounds just like the real thing. Only a magnet can tell. " +
                    "Too cheap to be true, usually.", When: s => !s.Has("magnet")),
            },
        },
    }.ToDictionary(l => l.Seller);

    /// <summary>A seller's first-visit lesson, or null if they have nothing new to teach.</summary>
    public static Lesson For(string seller) => ByKey.GetValueOrDefault(seller);

    public static IEnumerable<Lesson> All => ByKey.Values;
}
