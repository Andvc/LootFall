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
/// be skipped. Every lesson's chest guarantees what it teaches: Fennick's has a false bottom, Pell's has three purses.
/// </summary>
public sealed class Lesson
{
    public string Seller { get; init; }

    /// <summary>Builds the visit, at the shop tier it happens at.</summary>
    public Func<Random, int, Visit> Make { get; init; }

    public LessonStep[] Steps { get; init; }

    /// <summary>Bram's tips (<see cref="Hints"/>) this lesson already teaches, so they never come up as well.</summary>
    public string[] Covers { get; init; } = Array.Empty<string>();

    /// <summary>The steps that apply at this shop.</summary>
    public IReadOnlyList<LessonStep> For(GameState state) => Steps.Where(s => s.When == null || s.When(state)).ToList();
}

public static class Lessons
{
    static Item I(Category kind, Rarity quality, bool magic = false, bool fake = false) => new(kind, quality, magic, fake);

    static Visit Price(Random rng, string seller, Chest chest, int tier, int? ask = null, IEnumerable<Category> claim = null, int? floor = null,
        string pitch = null, bool? truth = true) =>
        Market.Price(rng, Sellers.Get(seller), chest, tier, ask, null, pitch, claim, floor, truth: truth);

    static readonly Dictionary<string, Lesson> ByKey = new Lesson[]
    {
        // Brindle Crossroads. Tam, Hob and Mags were met on the first day; their lessons come on their first ordinary visit.
        new()
        {
            Seller = "tam",
            Covers = new[] { "offer" },
            Make = (rng, tier) => Price(rng, "tam", new Chest(new[] { I(Category.Coins, 0), I(Category.Arms, 0) }), tier, floor: 2),
            Steps = new LessonStep[]
            {
                new("Tam never shakes anything and has no idea what it's worth: he'll ask you to name a price. First, shake it " +
                    "at every strength.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick what you heard. Then set the slider to a few coins and make your offer: he'll take almost anything. " +
                    "You get two tries.", c => c.Visit.OffersMade > 0, Trades: true),
            },
        },
        new()
        {
            Seller = "hob",
            Covers = new[] { "haggle" },
            Make = (rng, tier) =>
            {
                var visit = Price(rng, "hob", new Chest(new[] { I(Category.Coins, 0), I(Category.Arms, 0) }), tier);
                visit.SetFloor(Money.Round(visit.Ask * 0.45f));
                return visit;
            },
            Steps = new LessonStep[]
            {
                new("Hob shook it hard and heard a clank. He haggles: he always asks about double. Shake it yourself, gently " +
                    "too.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick what you heard. Then use the slider to offer him about half his price. You get one offer: much " +
                    "lower and he storms off.", c => c.Visit.OffersMade > 0, Trades: true),
            },
        },
        new()
        {
            Seller = "mags",
            Make = (rng, tier) => Price(rng, "mags", new Chest(new[] { I(Category.Coins, 0) }), tier,
                ask: Money.Round(Categories.Nominal(Category.Arms, Rarity.Common) * 1.4f),
                pitch: "A tinker paid his tab with it. One rattle, like a fish in a crate. A rattle's a rattle: {price}, love."),
            Steps = new LessonStep[]
            {
                new("Mags counts the rattles and charges the same for each, whatever's rattling: a purse costs what a rusty " +
                    "dagger does. Shake it: what's rattling?", c => c.Tried(Shake.Gentle)),
                new("A jingle: coins, worth more than a dagger. At dagger prices, that's a bargain."),
            },
        },
        new()
        {
            Seller = "dorrin",
            Covers = new[] { "story" },
            Make = (rng, tier) => Price(rng, "dorrin", new Chest(new[] { I(Category.Coins, 0), I(Category.Garb, 0) }), tier, truth: false),
            Steps = new LessonStep[]
            {
                new("A dragon? Dorrin's never been past the Barrow, and half his stories are tall. Barrow chests hold grave goods: " +
                    "coin, arms, a shroud. Where it really came from shows when it's open, and your ledger keeps count."),
                new("His chests are rotten through: about two seconds of shaking and no more. One strength, then. Hard stirs " +
                    "everything, but a clank would drown the coin. Shake it hard.", c => c.Tried(Shake.Hard)),
            },
        },

        // Thornwick Market.
        new()
        {
            Seller = "snikkit",
            Make = (rng, tier) => Price(rng, "snikkit", new Chest(new[] { I(Category.Coins, Rarity.Uncommon), I(Category.Arms, Rarity.Uncommon) },
                Rarity.Uncommon), tier, truth: false),
            Steps = new LessonStep[]
            {
                new("The King's bedroom! Every goblin chest is from the King's bedroom; they're all from the warren, and mostly " +
                    "coin. Snikkit shook it gently. Do the same: the first strength is free.", c => c.Tried(Shake.Gentle)),
                new("Every strength after that puts his price up a tenth. A gentle shake can't stir anything heavy, so pay once " +
                    "more for a hard one. Gentle and hard between them hear everything firm would.", c => c.Tried(Shake.Gentle, Shake.Hard)),
            },
        },
        new()
        {
            Seller = "wenna",
            Make = (rng, tier) => Price(rng, "wenna",
                new Chest(new[] { I(Category.Potions, Rarity.Uncommon), I(Category.Scrolls, Rarity.Uncommon) }, Rarity.Uncommon, null,
                    LockTiers.For(Rarity.Uncommon)), tier),
            Steps = new LessonStep[]
            {
                new("Wenna prices by weight: heavy means valuable. But potions and scrolls weigh next to nothing. Shake it at " +
                    "every strength.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("A slosh and a rustle: good things, and light. Tick them: at her price, a bargain. It's locked, mind: " +
                    "you'll pick it after.", c => c.Slip.Count(Category.Potions) > 0 && c.Slip.Count(Category.Scrolls) > 0),
            },
        },
        new()
        {
            Seller = "bramble",
            Make = (rng, tier) =>
            {
                var chest = new Chest(new[] { I(Category.Potions, Rarity.Uncommon), I(Category.Garb, Rarity.Uncommon), I(Category.Scrolls, Rarity.Uncommon) },
                    Rarity.Uncommon);
                return Price(rng, "bramble", chest, tier, ask: Money.Round(Market.TypicalChest(Sellers.Get("bramble"), Rarity.Uncommon, tier) * 0.6f));
            },
            Steps = new LessonStep[]
            {
                new("Mother Bramble's price came to her in a dream. It has nothing to do with the chest. Shake it at every " +
                    "strength and add it up yourself.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Compare your slip with her dream. Today's dream is cheap. Tomorrow's might be absurd."),
            },
        },
        new()
        {
            Seller = "ansel",
            Make = (rng, tier) => Price(rng, "ansel", new Chest(new[] { I(Category.Coins, Rarity.Uncommon), I(Category.Garb, Rarity.Uncommon) },
                Rarity.Uncommon), tier),
            Steps = new LessonStep[]
            {
                new("Brother Ansel shook it firmly, and won't have it shaken hard: the saints are resting. Half again if you do. " +
                    "Shake it gently, then firmly.", c => c.Tried(Shake.Gentle, Shake.Firm)),
                new("Crypt chests are full of vestments, and cloth only sounds at a hard shake, so he never prices it. Tick Garb " +
                    "on faith, or shake it hard and pay for certainty.", c => c.Slip.Count(Category.Garb) > 0 || c.Tried(Shake.Hard)),
            },
        },
        new()
        {
            Seller = "fennick",
            Covers = new[] { "dropped", "hollow" },
            Make = (rng, tier) => Price(rng, "fennick", new Chest(new[] { I(Category.Coins, Rarity.Uncommon), I(Category.Scrolls, Rarity.Uncommon) },
                Rarity.Uncommon, I(Category.Coins, Rarity.Uncommon)), tier),
            Steps = new LessonStep[]
            {
                new("Smugglers' chests often have a false bottom, and Fennick never prices it. Lift the chest high and let go: " +
                    "drop it, and listen.", c => c.Hearing.Dropped),
                new("A hollow BOOM: something's under the floor. Tick False bottom: nobody's priced it. Then haggle.", c => c.Slip.Hidden),
            },
        },

        // Crownhold.
        new()
        {
            Seller = "wren",
            Covers = new[] { "offer", "try_again" },
            Make = (rng, tier) => Price(rng, "wren", new Chest(new[] { I(Category.Potions, Rarity.Uncommon, magic: true), I(Category.Coins, Rarity.Uncommon) },
                Rarity.Uncommon), tier),
            Steps = new LessonStep[]
            {
                new("Wren can't tell what's in a chest, but she feels magic. Name a low price: a few coins.", c => c.Visit.OffersMade > 0,
                    Trades: true),
                new("She wants more: she can feel something special. Something in there is enchanted, worth twice as much. " +
                    "Offer again, if it's worth it to you.", c => c.Visit.OffersMade > 1, Trades: true),
            },
        },
        new()
        {
            Seller = "orrin",
            Make = (rng, tier) => Price(rng, "orrin", new Chest(new[] { I(Category.Coins, Rarity.Rare), I(Category.Arms, Rarity.Rare), I(Category.Scrolls, Rarity.Rare) },
                Rarity.Rare), tier),
            Steps = new LessonStep[]
            {
                new("Archmage Orrin wagers on your craft: read his chest exactly (every kind, every count) and he gives back " +
                    "a third of the price when it's open. Shake it at every strength.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick exactly what you heard, no more and no less. A careful read takes a third off his price.",
                    c => c.Slip.Count(Category.Coins) > 0 && c.Slip.Count(Category.Arms) > 0 && c.Slip.Count(Category.Scrolls) > 0),
            },
        },
        new()
        {
            Seller = "grit",
            Make = (rng, tier) => Price(rng, "grit", new Chest(new[] { I(Category.Gems, Rarity.Uncommon), I(Category.Scrolls, Rarity.Rare) }, Rarity.Rare), tier),
            Steps = new LessonStep[]
            {
                new("Grit heard a tinkle: a gem. He's a dwarf: he knows arms, and thinks every tinkle is a diamond. Shake it " +
                    "gently yourself.", c => c.Tried(Shake.Gentle)),
                new("A ring, priced like the crown jewels. Offer what it's really worth, or let him keep it."),
            },
        },
        new()
        {
            Seller = "corisande",
            Make = (rng, tier) => Price(rng, "corisande", new Chest(new[] { I(Category.Garb, Rarity.Rare), I(Category.Scrolls, Rarity.Rare) }, Rarity.Rare,
                I(Category.Gems, Rarity.Rare)), tier),
            Steps = new LessonStep[]
            {
                new("Lady Corisande prices by the chest's colour, and she feels magic, so that's in her price. Half her " +
                    "grandmother's trick boxes hide a false bottom she never prices."),
                new("But she won't have one dropped: half again if you do. Tick False bottom on faith, or drop it and pay for " +
                    "certainty. Your call."),
            },
        },
        new()
        {
            Seller = "pell",
            Covers = new[] { "counted" },
            Make = (rng, tier) => Price(rng, "pell", new Chest(new[]
            {
                I(Category.Coins, Rarity.Rare), I(Category.Coins, Rarity.Rare), I(Category.Coins, Rarity.Rare), I(Category.Scrolls, Rarity.Rare),
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
            Make = (rng, tier) => Price(rng, "shelly", new Chest(new[] { I(Category.Scrolls, Rarity.Rare, fake: true), I(Category.Potions, Rarity.Rare) },
                Rarity.Rare), tier),
            Steps = new LessonStep[]
            {
                new("Shelly heard a rustle and swears it's a treasure map. Shake it gently.", c => c.Tried(Shake.Gentle)),
                new("A rustle at a gentle shake? Parchment only rustles when it's shaken hard. That's river sand pouring. Untick " +
                    "Scrolls.", c => c.Slip.Count(Category.Scrolls) == 0),
            },
        },
        new()
        {
            Seller = "aldous",
            Make = (rng, tier) => Price(rng, "aldous", new Chest(new[] { I(Category.Arms, Rarity.Rare), I(Category.Coins, Rarity.Rare), I(Category.Garb, Rarity.Rare) },
                Rarity.Rare), tier),
            Steps = new LessonStep[]
            {
                new("Sir Aldous wants his grandfather's sword. If there are arms inside, the best of them goes back to him, for " +
                    "the fee he named. Shake it: is there a clank?", c => c.Tried(Shake.Firm) || c.Tried(Shake.Hard)),
                new("A clank: that blade is his. Count it at his fee, not its worth, and see whether the rest pays for the chest."),
            },
        },

        // The Royal Emporium.
        new()
        {
            Seller = "vexis",
            Make = (rng, tier) => Price(rng, "vexis", new Chest(new[] { I(Category.Coins, Rarity.Epic), I(Category.Gems, Rarity.Legendary), I(Category.Arms, Rarity.Epic) },
                Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("An imp, with an hourglass: twelve seconds, from when you close this. Things from the Maw's hoard often beat " +
                    "their colour. Shake it gently, then hard. Quick!"),
                new("Gently, then hard!", c => c.Tried(Shake.Gentle, Shake.Hard)),
            },
        },
        new()
        {
            Seller = "malbranche",
            Make = (rng, tier) => Price(rng, "malbranche", new Chest(new[]
            {
                I(Category.Coins, Rarity.Epic), I(Category.Arms, Rarity.Epic), I(Category.Garb, Rarity.Epic), I(Category.Potions, Rarity.Epic),
            }, Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("A devil's bargain: Malbranche's price is low, but the dearest thing inside is his. Shake it at every " +
                    "strength: what's in there?", c => c.Tried(Shake.Gentle) && (c.Tried(Shake.Firm) || c.Tried(Shake.Hard))),
                new("Four things: he takes the best, you keep three. With many middling things his bargain is a good one; with " +
                    "one grand thing and little else, it's his."),
            },
        },
        new()
        {
            Seller = "skrae",
            Covers = new[] { "glow" },
            Make = (rng, tier) => Price(rng, "skrae", new Chest(new[] { I(Category.Arms, Rarity.Rare), I(Category.Scrolls, Rarity.Rare, magic: true) },
                Rarity.Rare, I(Category.Gems, Rarity.Rare)), tier),
            Steps = new LessonStep[]
            {
                new("Skrae hears everything. But a demon can't see enchantment, and never checks the floor. Shake it at every " +
                    "strength under the witchlight.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard), When: s => s.Has("lamp")),
                new("Something glowed: it's enchanted, worth twice as much. Click the star on its row to mark it.",
                    c => c.Slip.Magic(Category.Scrolls), When: s => s.Has("lamp")),
                new("Skrae hears everything. But a demon never checks for enchantment, and never checks the floor.", When: s => !s.Has("lamp")),
                new("Now drop it.", c => c.Hearing.Dropped),
                new("A boom! Tick False bottom: it never priced it.", c => c.Slip.Hidden),
            },
        },
        new()
        {
            Seller = "ermine",
            Make = (rng, tier) => Price(rng, "ermine", new Chest(new[] { I(Category.Gems, Rarity.Epic), I(Category.Scrolls, Rarity.Epic), I(Category.Potions, Rarity.Epic) },
                Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("Lady Ermine prices only her gems, at what she paid for them. Whatever else the maid packed comes free. Shake " +
                    "it at every strength.", c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick everything you heard: the rest is a gift. Some of her gems are paste, though, and she won't have a " +
                    "lodestone near them.", c => c.Slip.Count(Category.Scrolls) > 0 && c.Slip.Count(Category.Potions) > 0),
            },
        },
        new()
        {
            Seller = "silas",
            Make = (rng, tier) => Price(rng, "silas", new Chest(new[] { I(Category.Coins, Rarity.Epic, fake: true), I(Category.Gems, Rarity.Rare) },
                Rarity.Epic), tier),
            Steps = new LessonStep[]
            {
                new("Silas sells demon gold: gilded iron, and it jingles just like the real thing. Turn the lodestone on and " +
                    "shake it gently.", c => c.Hearing.HasTried(Shake.Gentle, true), When: s => s.Has("magnet")),
                new("The jingle went quiet: iron, gilded. Untick Coins.", c => c.Slip.Count(Category.Coins) == 0, When: s => s.Has("magnet")),
                new("Silas sells demon gold, gilded iron, as gold and gems, and it sounds just like the real thing. Only a " +
                    "lodestone can tell. Too cheap to be true, usually.", When: s => !s.Has("magnet")),
            },
        },
        new()
        {
            Seller = "aldo",
            Make = (rng, tier) => Price(rng, "aldo", new Chest(new[] { I(Category.Coins, Rarity.Epic), I(Category.Garb, Rarity.Epic), I(Category.Gems, Rarity.Epic) },
                Rarity.Epic), tier, truth: false),
            Steps = new LessonStep[]
            {
                new("Old Aldo plays a game: his price is whatever your slip says. Read it exactly and he takes a quarter off; " +
                    "read it wrong and you pay what it truly holds, and a quarter more. Shake it at every strength.",
                    c => c.Tried(Shake.Gentle, Shake.Firm, Shake.Hard)),
                new("Tick exactly what you heard, then buy. Funny: that ring of his has the royal seal on it.",
                    c => c.Slip.Count(Category.Coins) > 0 && c.Slip.Count(Category.Gems) > 0 && c.Slip.Count(Category.Garb) > 0),
            },
        },
        new()
        {
            Seller = "maw",
            Make = (rng, tier) => Price(rng, "maw", new Chest(new[] { I(Category.Coins, Rarity.Epic), I(Category.Gems, Rarity.Epic) }, Rarity.Epic), tier,
                truth: false),
            Steps = new LessonStep[]
            {
                new("That's no merchant, Kit. That's the Maw, in a borrowed body. One coin for the chest; but buy it, and " +
                    "everyone after him today asks twice as much. Count who's still waiting before you say yes."),
            },
        },
    }.ToDictionary(l => l.Seller);

    /// <summary>A seller's first-visit lesson, or null if they have nothing new to teach.</summary>
    public static Lesson For(string seller) => ByKey.GetValueOrDefault(seller);

    public static IEnumerable<Lesson> All => ByKey.Values;
}
