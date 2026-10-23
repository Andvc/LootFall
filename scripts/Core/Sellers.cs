using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// How a seller decides what their chest is worth. Each is something a real person might do, and each has a blind spot
/// a careful listener can see through.
/// </summary>
public enum Judgment
{
    /// <summary>Shakes it at their own strength and prices what they heard.</summary>
    Ear,
    /// <summary>Shakes it gently, then hard: hears everything there is to hear.</summary>
    Thorough,
    /// <summary>Counts the rattles and charges the same for each, whatever is rattling.</summary>
    Counter,
    /// <summary>Hefts it: heavy means valuable. Weapons and coin weigh a lot; spells and gems next to nothing.</summary>
    Weight,
    /// <summary>Judges by the chest's colour alone: a blue chest is worth what a blue chest usually holds.</summary>
    Colour,
    /// <summary>Names a number that came to them in a dream. It has nothing to do with the chest.</summary>
    Dreamer,
    /// <summary>Knows weapons; prices everything else at half, except gems, which he takes for diamonds.</summary>
    Miner,
    /// <summary>Knows her own gems (at what she paid for them) and nothing else in the chest.</summary>
    Jeweller,
    /// <summary>Can't tell a coin from a cloak, but can feel magic, and knows it's worth a lot.</summary>
    MagicSense,
    /// <summary>Never shook it, has no idea, takes a few coins.</summary>
    Clueless,
    /// <summary>Knows exactly what's in it, because he packed the fakes himself.</summary>
    Crook,
}

/// <summary>What a seller tells you besides what they heard: always true, and worth working out.</summary>
public enum Clue
{
    None,
    /// <summary>The pawn ticket: how many things were pledged (Pell).</summary>
    Articles,
    /// <summary>The weight on a game scale, to the half pound (Wenna).</summary>
    Pounds,
    /// <summary>A dream of the dearest thing inside, as a sign (Mother Bramble).</summary>
    Dream,
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
/// Someone who brings a chest to the counter: who they are, where their chests come from (and they say so: the place is
/// a stated clue to what's inside), one way of judging what it's worth, one way of trading, and one rule that changes
/// what you can do, what you know or what the deal is (a clock, a rotten chest, a price that climbs with every shake, a
/// straw-packed chest, a pawn ticket, a scale, a dream, a wager, a claim on what's inside).
/// Together with the sounds in the chest, that makes each visit its own small puzzle. See docs/world.md.
/// </summary>
public sealed class SellerDef
{
    public string Key { get; init; }
    public string Name { get; init; }

    /// <summary>The shop tier at which they start coming in.</summary>
    public int Tier { get; init; }

    /// <summary>The share of days they come in at all (1: every day; the King one day in four, the Maw one in ten).</summary>
    public float Visits { get; init; } = 1f;

    /// <summary>Where their chests come from, and how often each.</summary>
    public (string Origin, float Weight)[] From { get; init; } = Array.Empty<(string, float)>();

    /// <summary>What they say about a chest from each place, with {price} and the like. Where it came from is always true.</summary>
    public Dictionary<string, string[]> Stories { get; init; } = new();

    /// <summary>Their chests are packed in straw (<see cref="Chest.Packed"/>): everything stirs a strength later.</summary>
    public bool Packs { get; init; }

    /// <summary>What they tell you besides what they heard.</summary>
    public Clue States { get; init; }

    /// <summary>Their chests hang on a packhorse that spooks at a hard shake: nobody shakes them hard (Wenna's).</summary>
    public bool Packhorse { get; init; }

    /// <summary>Their chests are half rotten: this many seconds of shaking, all told, and no more (0: as long as you like).</summary>
    public float ShakeLimit { get; init; }

    /// <summary>Every strength you shake at after the first puts their price up by this share.</summary>
    public float ShakeFee { get; init; }

    /// <summary>They give back this share of the price if your slip turns out exactly right.</summary>
    public float Wager { get; init; }

    /// <summary>A kind of thing that's theirs by right: if any is inside, the best of it goes back to them, for a price they name.</summary>
    public Category? Heirloom { get; init; }

    /// <summary>The dearest thing inside is theirs.</summary>
    public bool TakesDearest { get; init; }

    /// <summary>Their price is whatever your slip says the chest holds (the King's game).</summary>
    public bool SlipPrice { get; init; }

    /// <summary>Buy from them, and everyone who comes to the counter after them today asks this much more (the Maw's gaze).</summary>
    public float Gaze { get; init; }

    /// <summary>What they say when their price goes up after a shake, with {price}.</summary>
    public string[] PriceUp { get; init; } = { "{price}, now." };

    /// <summary>What Bram calls them, when the first word of their name won't do.</summary>
    public string Nick { get; init; }

    /// <summary>How hard they shake it, for judgments that listen.</summary>
    public Shake Shakes { get; init; } = Shake.Hard;

    public Judgment Judge { get; init; } = Judgment.Ear;
    public Trade Trade { get; init; } = Trade.Fixed;

    float[] _kinds;

    /// <summary>
    /// How likely each kind of thing is in their chests, by Category: what their places hold, all told (or set directly,
    /// for the Stranger and the vault).
    /// </summary>
    public float[] Kinds
    {
        get => _kinds ??= Enumerable.Range(0, Categories.All.Length)
            .Select(k => From.Sum(f => f.Weight * Origins.Get(f.Origin).Kinds.ElementAtOrDefault(k)) / Math.Max(0.01f, From.Sum(f => f.Weight))).ToArray();
        init => _kinds = value;
    }

    /// <summary>How likely each chest colour is, common to epic.</summary>
    public float[] Colours { get; init; }

    /// <summary>What you learn about them, in a sentence.</summary>
    public string Habit { get; init; }

    /// <summary>For sellers with no stories (the Stranger, the vault): lines with {heard}, {count}, {heft}, {colour}, {magic} and {price}.</summary>
    public string[] Pitches { get; init; } = Array.Empty<string>();
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

    /// <summary>Shake their chest hard and they put the price up by half.</summary>
    public bool HatesHardShake { get; init; }

    /// <summary>Feels magic, and prices it in.</summary>
    public bool SensesMagic { get; init; }

    public float MagicChance { get; init; } = 0.05f;

    /// <summary>How often their chest has a false bottom (from the village shop on).</summary>
    public float HiddenChance { get; init; } = 0.08f;

    /// <summary>How often their chest is locked (from the village shop on).</summary>
    public float LockChance { get; init; } = 0.3f;

    /// <summary>The kinds they pass off fakes of: gilded iron for coin and gems, sand for spells.</summary>
    public Category[] Fakes { get; init; } = Array.Empty<Category>();

    /// <summary>How likely each thing of a kind they fake is the fake.</summary>
    public float FakeChance { get; init; }

    /// <summary>From the town shop on: how often each kind they bring comes two (or three) of a kind.</summary>
    public float PairChance { get; init; } = 0.08f;

    /// <summary>A kind they always bring: a lady her jewels.</summary>
    public Category? Always { get; init; }

    /// <summary>How often their chest has nothing in it at all.</summary>
    public float EmptyChance { get; init; }

    /// <summary>For judgments that listen: how often their chest holds something their shaking can't hear.</summary>
    public float MissRate { get; init; } = 0.55f;

    /// <summary>The chance each thing they bring is one quality better than its chest's colour suggests.</summary>
    public float QualityBoost { get; init; }

    /// <summary>For a fixed price: how much over what they think it's worth they ask.</summary>
    public (float Min, float Max) Markup { get; init; } = (Market.MarkupMin, Market.MarkupMax);

    /// <summary>For a haggle: the least they'll take, against what they think it's worth.</summary>
    public (float Min, float Max) Bottom { get; init; } = (Market.BottomMin, Market.BottomMax);

    /// <summary>Whether they shake it at all (and so what you can learn from what they say they heard).</summary>
    public bool Listens => Judge is Judgment.Ear or Judgment.Thorough or Judgment.Counter or Judgment.Miner;

    /// <summary>Soldiers, knights and dwarves: they bring a lot of weapons (a smithing name draws them).</summary>
    public bool Metalworker => Kinds[(int)Category.Arms] / Kinds.Sum() >= 0.3f;

    /// <summary>Whether they say how many of a kind (and price each), or just which kinds they heard.</summary>
    public bool Counts => Judge is Judgment.Counter or Judgment.Thorough or Judgment.Crook or Judgment.Jeweller;

    /// <summary>
    /// What this seller says is in the chest: what they heard, or (crooks, jewellers) what they packed. Never a Heartsong:
    /// only a Keywright hears one.
    /// </summary>
    public IEnumerable<Item> Notices(Chest chest) => (Judge switch
    {
        Judgment.Crook => chest.Items,
        Judgment.Jeweller => chest.Items.Where(i => i.Category == Category.Gems),
        Judgment.Thorough => chest.HeardAt(Shake.Gentle).Union(chest.HeardAt(Shake.Hard)),
        _ when Listens => chest.HeardAt(Shakes),
        _ => Enumerable.Empty<Item>(),
    }).Where(i => !i.IsSong);

    /// <summary>How they put it in the ledger: how they judge a chest.</summary>
    public string JudgeWord => Judge switch
    {
        Judgment.Ear => $"listens ({Categories.Adverb(Shakes)})",
        Judgment.Thorough => "listens gently, then hard",
        Judgment.Counter => "counts the rattles",
        Judgment.Weight => "goes by weight",
        Judgment.Colour => "goes by the chest's colour",
        Judgment.Dreamer => "dreams up a price",
        Judgment.Miner => "knows only weapons",
        Judgment.Jeweller => "knows only her own gems",
        Judgment.MagicSense => "feels for magic",
        Judgment.Clueless => "has no idea",
        _ => "knows exactly",
    };

    static readonly string[] Titles = { "Little", "Widow", "Old", "Mother", "Brother", "Archmage", "Lady", "Sir", "The" };

    /// <summary>What Bram calls them: "Penn", "Hob", "Bramble".</summary>
    public string Short => Nick ?? Name.Split(' ').First(w => !Titles.Contains(w));

    /// <summary>The same, to start a sentence with: "The Maw asks".</summary>
    public string Who => char.ToUpperInvariant(Short[0]) + Short[1..];
}

public static class Sellers
{
    static readonly string[] Shrug = { "Suit yourself.", "Your loss.", "Another time, then." };

    // Chest colours by shop: villagers bring wood, the court brings purple.
    static readonly float[] Stall = { 0.75f, 0.25f, 0, 0 }, Village = { 0.1f, 0.65f, 0.25f, 0 },
        Town = { 0, 0.25f, 0.65f, 0.1f }, Emporium = { 0, 0, 0.45f, 0.55f };

    static Dictionary<string, string[]> Tell(params (string Origin, string[] Lines)[] stories) => stories.ToDictionary(s => s.Origin, s => s.Lines);

    public static readonly SellerDef[] All =
    {
        // Brindle Crossroads: villagers, an innkeeper, and a young adventurer back from the Barrow.
        new()
        {
            Key = "tam", Name = "Little Tam", Tier = 1, Judge = Judgment.Clueless, Trade = Trade.Offer, EmptyChance = 0.3f, Colours = Stall,
            From = new[] { ("millpond", 3f), ("attic", 1f) },
            Stories = Tell(
                ("millpond", new[] { "Fished it out of the millpond! It's still dripping. Mum says never shake things. What'll you give me?",
                                     "It was stuck in the mill wheel. Dad says it's full of frogs. How much will you give me?" }),
                ("attic", new[] { "It was under Gran's bed. She says I can sell it if I don't tell Mum. What's it worth?",
                                  "Found it in our hayloft behind the cat. The cat didn't want it. How much?" })),
            Habit = "A village boy who never shakes anything and has no idea. A few coins buys anything, but his millpond chests are sometimes empty.",
            Accepts = new[] { "Yay!", "I'm buying sweets!" },
            Refusals = new[] { "Is that all? Um... a bit more?" },
            Leaves = new[] { "Aww. I'll ask someone else." },
        },
        new()
        {
            Key = "penn", Name = "Widow Penn", Tier = 1, Shakes = Shake.Gentle, Colours = Stall,
            From = new[] { ("kitbag", 3f), ("attic", 1f) },
            Stories = Tell(
                ("kitbag", new[] { "My Tobb's old soldiering chest. I gave it a gentle shake: {heard}. I can't bear to open it. Would {price} do?",
                                   "Tobb carried this through two wars, dear. A gentle shake: {heard}. {price}?" }),
                ("attic", new[] { "From the attic: Tobb's mother's. Just a gentle shake, my wrists aren't what they were. {heard}. {price}?" })),
            Habit = "A soldier's widow selling his things. Shakes gently, so heavy weapons and soft cloaks make no sound for her, and a kitbag usually holds both.",
            Accepts = new[] { "Thank you. Truly.", "Bless you." },
        },
        new()
        {
            Key = "hob", Name = "Hob the Farmer", Tier = 1, Shakes = Shake.Hard, Trade = Trade.Haggle, Colours = Stall,
            From = new[] { ("field", 1f) },
            Stories = Tell(("field", new[] { "Turned up under the plough on the old battlefield. Shook it hard: {heard}. {price}, and that's a bargain!",
                                             "Every spring the field coughs up another. Shook it like a hay bale: {heard}. Say, {price}?" })),
            Habit = "Ploughs chests up out of the old battlefield and shakes them hard, so a clank drowns the coin out for him. Haggles: asks about double.",
            Accepts = new[] { "Deal! Wait till I tell the missus.", "Done. Easy money!" },
            Counters = new[] { "Hmm. {price}, and that's my last word.", "My wife'd skin me. {price}." },
            Leaves = new[] { "Daylight robbery! Forget it." },
        },
        new()
        {
            Key = "mags", Name = "Mags the Innkeeper", Tier = 1, Shakes = Shake.Firm, Judge = Judgment.Counter, Markup = (1.3f, 1.6f), Colours = Stall,
            From = new[] { ("tab", 1f) },
            Stories = Tell(("tab", new[] { "Some adventurer left this for his tab. Gave it a firm shake, love: {count}. A rattle's a rattle: {price}.",
                                           "Three nights' ale and a broken bench, and he paid me in chest. {count}. {price}, love." })),
            Habit = "Keeps the Crossed Keys inn and takes chests for unpaid tabs. Counts the rattles at a firm shake and charges the same for each: a goblin cleaver costs what a purse does.",
            Accepts = new[] { "Pleasure doing business!", "Done, love." },
        },
        new()
        {
            Key = "dorrin", Name = "Dorrin the Delver", Tier = 1, Judge = Judgment.Colour, ShakeLimit = 2f, Markup = (0.9f, 1.15f), Visits = 0.5f, Colours = Stall,
            From = new[] { ("barrow", 1f) },
            Stories = Tell(("barrow", new[] { "Dragged it up from the Barrow's second vault. The damp's got into it: one short shake, mind. {price}.",
                                              "From the Barrow. Lost my torch and my nerve, kept the chest. It's rotten, so shake it once. {price}.",
                                              "I ran past a barrow-wight for this. It's falling apart, so one shake. {price}." })),
            Habit = "A young adventurer back from the Barrow with chests so rotten they stand one short shake. Grave goods: coin, weapons, gems.",
            Accepts = new[] { "Drinks are on me!", "Back to the Barrow!" },
        },

        // Thornwick Market: a goblin, a huntress, a druid, a monk and a smuggler.
        new()
        {
            Key = "snikkit", Name = "Snikkit the Goblin", Tier = 2, Shakes = Shake.Gentle, ShakeFee = 0.1f, MissRate = 0.6f, Markup = (1.25f, 1.5f), Visits = 0.5f, Colours = Village,
            From = new[] { ("warren", 1f) },
            Stories = Tell(("warren", new[] { "From the warren, yes yes. Very shiny. {heard}. {price}. You shake more, price go up!",
                                              "Snikkit's cousins pinch it, Snikkit sells it. {heard}. {price}. Shake again, you like it, price go up!",
                                              "Warren chest. Smells of goblin. Is fine. {price}, more if you shake." })),
            PriceUp = new[] { "Ooh, you like! Price go up: {price}.", "More shaking, more gold! {price}.", "Hehe. {price} now." },
            Habit = "A goblin selling what his cousins pinch: warren chests, mostly coin. Hears only what a gentle shake stirs; each strength you try after your first costs a tenth more.",
            Accepts = new[] { "Shiny! Shiny!", "Hehehe. Sucker. I mean, friend." },
        },
        new()
        {
            Key = "wenna", Name = "Wenna the Huntress", Tier = 2, Judge = Judgment.Weight, States = Clue.Pounds, Packhorse = true,
            Markup = (0.85f, 1.05f), Colours = Village, LockChance = 0.8f,
            From = new[] { ("camp", 2f), ("den", 1f) },
            Stories = Tell(
                ("camp", new[] { "Took it off an outlaws' camp on the Thornwick road. My game scale says {pounds}. {price}. Gently: the mare spooks.",
                                 "The outlaws won't be needing it. {pounds} on my game scale: fair weight, fair price. {price}." }),
                ("den", new[] { "Found it in a wolf den. The wolves had eaten whoever carried it. {pounds}, on my scale. {price}.",
                                "Wolves don't hoard, but their dinners did. {pounds} exactly. {price}. Not hard: the mare." })),
            Habit = "A huntress who weighs every chest on her game scale and prices by the pound, so light things (spells, gems) come cheap. " +
                    "Her chests hang on a mare who spooks at a hard shake, so cloaks and spells never stir: the weight left over is theirs.",
            Accepts = new[] { "Done.", "Good hunting." },
        },
        new()
        {
            Key = "bramble", Name = "Mother Bramble", Tier = 2, Judge = Judgment.Dreamer, States = Clue.Dream, Markup = (1.2f, 1.2f), Colours = Village,
            HiddenChance = 0.25f,
            From = new[] { ("wood", 1f) },
            Stories = Tell(("wood", new[] { "The Old Wood let this one go. I slept on it and dreamt of {dream}. The oaks whispered its price: {price}.",
                                            "Moss grew on it for a hundred years. Last night I dreamt of {dream}, and of a price: {price}." })),
            Habit = "A druid of the Old Wood. Her price comes from a dream and has nothing to do with the chest, but she always dreams true of the dearest " +
                    "thing inside, as a sign: a magpie for coin, a thorn for a weapon, a raven's wing for a cloak, a river for a potion, an owl for a spell, a star for a gem.",
            Accepts = new[] { "As the Wood wills.", "The oaks approve." },
        },
        new()
        {
            Key = "ansel", Name = "Brother Ansel", Tier = 2, Shakes = Shake.Firm, HatesHardShake = true, MissRate = 0.55f, Markup = (1.7f, 2f), Colours = Village,
            From = new[] { ("crypt", 1f) },
            Stories = Tell(("crypt", new[] { "From the abbey crypt, by the Abbot's leave. A firm shake: {heard}. {price}. Not hard, please: the saints are resting.",
                                             "Brother Cuthbert's reliquary. I shook it firmly, as is seemly: {heard}. {price}, for the roof fund." })),
            Habit = "A young monk selling the crypt's chests for the abbey roof. Shakes firmly, and won't have them shaken hard, so the holy cloaks inside go unheard and unpriced.",
            Accepts = new[] { "Bless you.", "The roof thanks you." },
            Angry = new[] { "Brother! The saints! Half again, for the sacrilege." },
        },
        new()
        {
            Key = "fennick", Name = "Fennick the Smuggler", Tier = 2, Shakes = Shake.Firm, Packs = true, Trade = Trade.Haggle, HiddenChance = 0.45f,
            Bottom = (0.95f, 1.15f), Colours = Village,
            From = new[] { ("road", 1f) },
            Stories = Tell(("road", new[] { "Packed in straw, so nothing clinked at the Wardens' toll. A firm shake: {heard}. {price}, no questions.",
                                            "Smugglers' road. Don't ask. Straw muffles everything, so I gave it a firm shake: {heard}. {price}." })),
            Habit = "Runs brandy past the Wardens' toll, every chest packed in straw so nothing clinks: everything stirs a strength later than it would, " +
                    "and cloaks and spells not at all. Hears what a firm shake stirs, never a false bottom. Haggles.",
            Accepts = new[] { "Pleasure. We never met.", "Done. Mind the Wardens." },
            Counters = new[] { "{price}, and you're robbing me.", "Tch. {price}, final." },
            Leaves = new[] { "Hmph. Plenty of other buyers." },
        },

        // Crownhold: an archmage and his apprentice, a dwarf, a noblewoman, a pawnbroker, a mudlark and a knight.
        new()
        {
            Key = "wren", Name = "Wren the Apprentice", Tier = 3, Judge = Judgment.MagicSense, SensesMagic = true, Trade = Trade.Offer, MagicChance = 0.25f,
            Colours = new[] { 0.2f, 0.6f, 0.2f, 0 },
            From = new[] { ("arcanum", 1f) },
            Stories = Tell(("arcanum", new[] { "Master Orrin said to clear the Arcanum stores. I can't tell what's in it, but I'll know if it's special. What's it worth?",
                                               "From the third cellar of the Arcanum. Don't tell Master. What would you give?" })),
            Habit = "An archmage's apprentice. Can't tell a coin from a cloak, but feels magic. Name a price: she takes a little, unless something's enchanted.",
            Accepts = new[] { "Oh good! Don't tell Master.", "Thank you!" },
            Refusals = new[] { "No... there's something special in there. More, please." },
            Leaves = new[] { "I should ask Master first. Sorry!" },
        },
        new()
        {
            Key = "orrin", Name = "Archmage Orrin", Tier = 3, Judge = Judgment.Colour, Wager = 1f / 3, MagicChance = 0.2f, Markup = (1.3f, 1.5f), Colours = Town,
            From = new[] { ("arcanum", 1f) },
            Stories = Tell(("arcanum", new[] { "I packed this myself, when the Locking was new. {price}. Tell me exactly what's in it, and a third comes back to you.",
                                               "A {colour} chest from my own stores. {price}. Read it true, Keywright, and I'll return a third. I like to see the craft done well." })),
            Habit = "An archmage old enough to remember the Locking. Prices by the colour, and wagers: read his chest exactly right and he returns a third of your price.",
            Accepts = new[] { "Let us see.", "We shall see what you hear." },
        },
        new()
        {
            Key = "grit", Name = "Grit the Miner", Tier = 3, Shakes = Shake.Hard, Judge = Judgment.Miner, Trade = Trade.Haggle, Bottom = (0.85f, 1f), Colours = Town,
            From = new[] { ("hold", 1f) },
            Stories = Tell(("hold", new[] { "Dug it out of the deep seam under Ironroot. Hard shake, dwarf's hands: {heard}. {price}.",
                                            "Ironroot hold, sealed since my great-great-grandmother's day. {heard}. {price}." })),
            Habit = "A dwarf of Ironroot. Knows weapons; prices everything else at half, except gems, which he takes for diamonds. Haggles.",
            Accepts = new[] { "Done. Back to the deep.", "Ha! Done." },
            Counters = new[] { "{price}. Lower and I'll eat my helmet." },
            Leaves = new[] { "You take me for a fool? Good day." },
        },
        new()
        {
            Key = "corisande", Name = "Lady Corisande", Tier = 3, Judge = Judgment.Colour, SensesMagic = true, HatesDrop = true, MagicChance = 0.15f,
            HiddenChance = 0.5f, Markup = (1.05f, 1.3f), Colours = Town,
            From = new[] { ("strongroom", 1f) },
            Stories = Tell(("strongroom", new[] { "One of grandmother's trick boxes, from the de Vere strongroom. A {colour} chest, and {magic}. {price}. You'll not drop it.",
                                                  "Grandmother hid things in everything. A {colour} chest; {magic}. {price}. Handle it as a lady would." })),
            Habit = "A noblewoman selling her grandmother's trick boxes: half hide a false bottom she never prices. Prices by colour and feels magic. Drop one and it's half again.",
            Accepts = new[] { "How delightful.", "Grandmother would approve." },
            Angry = new[] { "You DROPPED a de Vere heirloom? Half again." },
        },
        new()
        {
            Key = "pell", Name = "Pell the Pawnbroker", Tier = 3, Shakes = Shake.Firm, States = Clue.Articles, HatesDrop = true, HiddenChance = 0.35f,
            PairChance = 0.6f, MissRate = 0.45f, Markup = (2.3f, 2.7f), Colours = Town,
            From = new[] { ("pawn", 1f) },
            Stories = Tell(("pawn", new[] { "Pawned twenty years ago, never redeemed. The ticket says {articles}. Shook it firm: {heard}. {price}, and I don't count.",
                                            "Ticket four thousand and something: {articles}, it says. {heard}, in there somewhere. {price}. Don't drop pledges." })),
            Habit = "Sells lots nobody redeemed, two or three of a kind. Says which kinds he heard, never how many. The ticket counts every article pledged, " +
                    "false bottom and all, and he won't have pledges dropped: count what you hear against it.",
            Accepts = new[] { "Done. No refunds.", "Sold, as heard." },
        },
        new()
        {
            Key = "shelly", Name = "Shelly the Mudlark", Tier = 3, Shakes = Shake.Hard, Fakes = new[] { Category.Scrolls }, FakeChance = 0.5f,
            Colours = new[] { 0.1f, 0.5f, 0.35f, 0.05f },
            From = new[] { ("river", 1f) },
            Stories = Tell(("river", new[] { "Out of the Aldwater mud at low tide! Shook it hard: {heard}. A drowned wizard's spellbook, I'd bet my boots. {price}.",
                                             "The river gives, the river takes. {heard}. Spellbooks, potions, who knows! {price}." })),
            Habit = "Combs the Aldwater mud and swears every rustle is a drowned wizard's spellbook. Her scroll cases are often full of sand, which pours at the gentlest shake. Parchment never does.",
            Accepts = new[] { "Mud's good to me!", "The river provides." },
        },
        new()
        {
            Key = "aldous", Name = "Sir Aldous", Tier = 3, Judge = Judgment.Colour, Heirloom = Category.Arms, Markup = (1.1f, 1.3f), Colours = Town,
            From = new[] { ("warchest", 1f) },
            Stories = Tell(("warchest", new[] { "Grandfather's war chest. If his sword's in it, it's mine, and I'll pay you {fee} for it. The rest is yours for {price}.",
                                                "The de Lacy war chest, from the siege of Harrowgate. Any blade in it is family: {fee} for the best. {price} for the chest." })),
            Habit = "A knight seeking his grandfather's sword. If there are weapons inside, the best of them is his, for the price he names; value what's left.",
            Accepts = new[] { "Honour is satisfied.", "For the de Lacys!" },
        },

        // The Royal Emporium, by the Gilded Gate: demons, a court lady, a fence, the King, and once in a while the Maw.
        new()
        {
            Key = "vexis", Name = "Vexis the Imp", Tier = 4, Judge = Judgment.Colour, Patience = 12f, QualityBoost = 0.2f, Markup = (1.35f, 1.65f),
            Colours = new[] { 0, 0, 0.2f, 0.8f },
            From = new[] { ("gate", 1f) },
            Stories = Tell(("gate", new[] { "Through the Gate, from the Maw's own hoard. {price}. My hourglass says twelve seconds.",
                                            "Hot off the Gilded Gate. A {colour} chest: {price}. Tick, tock." })),
            Habit = "An imp with an hourglass. Prices by colour, never looks inside, gives you twelve seconds. Things from the Maw's hoard are often better than their colour.",
            Accepts = new[] { "Delicious.", "A pleasure, mortal." },
            Leaves = new[] { "Too slow! Back through the Gate I go." },
        },
        new()
        {
            Key = "malbranche", Name = "Malbranche", Tier = 4, Judge = Judgment.Colour, TakesDearest = true, Markup = (0.5f, 0.65f), Colours = Emporium,
            From = new[] { ("gate", 1f) },
            Stories = Tell(("gate", new[] { "From the Maw's hoard. {price}, a pittance. My only condition: the dearest thing inside is mine.",
                                            "A bargain, mortal. {price}. When it opens, I take the best of it. Everything else is yours." })),
            Habit = "A skeletal devil who deals in bargains. His price is low, but the dearest thing inside is his. Many middling things suit his deal; one grand thing doesn't.",
            Accepts = new[] { "Signed and sealed.", "A pleasure doing business. It always is." },
        },
        new()
        {
            Key = "skrae", Name = "Skrae", Tier = 4, Judge = Judgment.Thorough, MissRate = 0f, MagicChance = 0.2f, HiddenChance = 0.3f, Markup = (1.2f, 1.45f),
            Colours = Emporium,
            From = new[] { ("gate", 1f) },
            Stories = Tell(("gate", new[] { "I listened gently, then hard. I hear every coin in the realm. {heard}. {price}, fair.",
                                            "My ears never miss a jingle. {heard}. {price}. The magic, I can't hear. The floor, I never check." })),
            Habit = "A demon with ears like a bat: listens gently, then hard, and prices what it hears fairly. Only enchantment and false bottoms get past it, and it brings plenty of both.",
            Accepts = new[] { "Fair.", "You won't regret it. Probably." },
        },
        new()
        {
            Key = "ermine", Name = "Lady Ermine", Tier = 4, Judge = Judgment.Jeweller, Always = Category.Gems, HatesMagnet = true, Fakes = new[] { Category.Gems },
            FakeChance = 0.25f, Markup = (1.5f, 1.7f), Colours = Emporium,
            From = new[] { ("strongroom", 1f) },
            Stories = Tell(("strongroom", new[] { "From my strongroom. My gems: {heard}. And whatever my maid packed. {price}.",
                                                  "The gems alone are worth {price}. The rest, I couldn't say. My maid packs." })),
            Habit = "A lady of the court. Knows only her own gems, at what she paid; whatever else her maid packed comes free. Some gems are paste. Bring a lodestone near them and it's half again.",
            Accepts = new[] { "Charming.", "Do look after them." },
            Angry = new[] { "A lodestone? On MY gems? Half again, for the insult." },
        },
        new()
        {
            Key = "silas", Name = "Silas Gilt", Tier = 4, Judge = Judgment.Crook, MissRate = 0f, Fakes = new[] { Category.Coins, Category.Gems }, FakeChance = 0.6f,
            HiddenChance = 0f, Colours = Emporium,
            From = new[] { ("gate", 1f) },
            Stories = Tell(("gate", new[] { "{heard}, friend. Out of the Maw's hoard, through a crack in the Gate. I'm leaving town tonight, so {price}.",
                                            "{heard}. Hoard goods, very rare. For you, just {price}." })),
            Habit = "Fence for the Maw's hoard. Sells demon gold (gilded iron) as gold and gems, cheap, and calls it all real. Real goods come cheap; fakes are worth nothing.",
            Accepts = new[] { "Wonderful! No refunds.", "A pleasure. I must be going." },
        },
        new()
        {
            Key = "aldo", Name = "Old Aldo", Tier = 4, Judge = Judgment.Clueless, SlipPrice = true, Visits = 0.25f, PairChance = 0.35f,
            MagicChance = 0.2f, HiddenChance = 0.25f, Colours = new[] { 0, 0, 0.3f, 0.7f },
            From = new[] { ("treasury", 1f) },
            Stories = Tell(("treasury", new[] { "From an old house with a great many cellars. I've no idea what's in it. You tell me: whatever you say it holds, that's my price.",
                                                "Old things from an old house. Name what's inside, and that's what it costs. Truly, and I take a quarter off; wrongly, and I want what it holds and a quarter more." })),
            Habit = "A tired old man in a plain cloak, with a very fine ring. His price is your slip: read it exactly and he takes a quarter off; wrong, and you pay what it holds and a quarter more.",
            Accepts = new[] { "Well. We shall see.", "Hm. Let's see what you heard." },
        },
        new()
        {
            Key = "maw", Name = "Avarax, the Gilded Maw", Nick = "the Maw", Tier = 4, Judge = Judgment.Colour, Gaze = 2f, Visits = 0.1f,
            Colours = new[] { 0, 0, 0.5f, 0.5f },
            From = new[] { ("gate", 1f) },
            Stories = Tell(("gate", new[] { "A trifle from my hoard. One coin. All I ask is that you open it here, where I can see.",
                                            "One coin, little Keywright. And then everyone in this city will want twice as much today. Isn't that fun?" })),
            Habit = "The demon of greed himself, in a borrowed body dripping with rings. Sells a chest for one coin, and everyone who comes to your counter after him that day asks twice as much.",
            Accepts = new[] { "Yesss.", "Open it. Let me see." },
        },
    };

    public static SellerDef Get(string key) => All.First(s => s.Key == key);

    public static string PassLine(Random rng) => rng.PickOne(Shrug);

    /// <summary>"Heard a jingle and a clank", "Heard two jingles", or "Not a sound".</summary>
    public static string Heard(IReadOnlyList<Category> claim)
    {
        if (claim.Count == 0) return "Not a sound";
        var words = claim.GroupBy(c => c).Select(g => g.Count() switch
        {
            1 => $"a {Categories.Get(g.Key).Word}",
            2 => $"two {Categories.Get(g.Key).Word}s",
            _ => $"{Number(g.Count())} {Categories.Get(g.Key).Word}s",
        }).ToList();
        return $"Heard {Join(words)}";
    }

    public static string Number(int n) => n switch { 1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", _ => n.ToString() };

    public static string Join(IReadOnlyList<string> words) =>
        words.Count <= 1 ? words.FirstOrDefault() ?? "" : string.Join(", ", words.Take(words.Count - 1)) + " and " + words[^1];
}
