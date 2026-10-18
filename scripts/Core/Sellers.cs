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
    /// <summary>Hefts it: heavy means valuable. Metal and coins weigh a lot; paper and jewelry next to nothing.</summary>
    Weight,
    /// <summary>Judges by the chest's colour alone: a blue chest is worth what a blue chest usually holds.</summary>
    Colour,
    /// <summary>Names a number that came to them in a dream. It has nothing to do with the chest.</summary>
    Dreamer,
    /// <summary>Knows metal; prices everything else at half, except jewelry, which he takes for diamonds.</summary>
    Miner,
    /// <summary>Knows her jewelry (at what she paid for it) and nothing else in the chest.</summary>
    Jeweller,
    /// <summary>Can't tell a coin from a coat, but can feel magic, and knows it's worth a lot.</summary>
    MagicSense,
    /// <summary>Never shook it, has no idea, takes a few coins.</summary>
    Clueless,
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
/// Someone who brings a chest to the counter. Each has one way of judging it, one way of trading, maybe one thing they
/// won't stand for, and a reward for putting up with them. Together with the sounds in the chest, that makes each visit
/// its own small puzzle: what did they miss, misjudge or make up, and is the price right?
/// </summary>
public sealed class SellerDef
{
    public string Key { get; init; }
    public string Name { get; init; }

    /// <summary>The shop tier at which they start coming in.</summary>
    public int Tier { get; init; }

    /// <summary>How hard they shake it, for judgments that listen.</summary>
    public Shake Shakes { get; init; } = Shake.Hard;

    public Judgment Judge { get; init; } = Judgment.Ear;
    public Trade Trade { get; init; } = Trade.Fixed;

    /// <summary>How likely each kind of thing is in their chests, by Category.</summary>
    public float[] Kinds { get; init; }

    /// <summary>How likely each chest colour is, common to epic.</summary>
    public float[] Colours { get; init; }

    /// <summary>What you learn about them, in a sentence.</summary>
    public string Habit { get; init; }

    /// <summary>Lines with {heard}, {count}, {heft}, {colour}, {magic} and {price}.</summary>
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

    /// <summary>Shake their chest hard and they put the price up by half.</summary>
    public bool HatesHardShake { get; init; }

    /// <summary>Talks up a chest: often claims a sound that isn't there.</summary>
    public bool Talker { get; init; }

    /// <summary>Always swears there's a false bottom, and prices it in. Sometimes there is one.</summary>
    public bool ClaimsHidden { get; init; }

    /// <summary>Feels magic, and prices it in.</summary>
    public bool SensesMagic { get; init; }

    public float MagicChance { get; init; } = 0.05f;

    /// <summary>How often their chest has a false bottom (from the village shop on).</summary>
    public float HiddenChance { get; init; } = 0.08f;

    /// <summary>How often their chest is locked (from the village shop on).</summary>
    public float LockChance { get; init; } = 0.3f;

    /// <summary>The kinds they pass off fakes of: gilded iron for coins and jewelry, sand for paper.</summary>
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

    /// <summary>Smiths and soldiers: they bring a lot of metal.</summary>
    public bool Metalworker => Kinds[(int)Category.Metal] >= 4;

    /// <summary>Whether they say how many of a kind (and price each), or just which kinds they heard.</summary>
    public bool Counts => Judge is Judgment.Counter or Judgment.Thorough or Judgment.Crook or Judgment.Jeweller;

    /// <summary>What this seller says is in the chest: what they heard, or (crooks, jewellers) what they packed.</summary>
    public IEnumerable<Item> Notices(Chest chest) => Judge switch
    {
        Judgment.Crook => chest.Items,
        Judgment.Jeweller => chest.Items.Where(i => i.Category == Category.Jewels),
        Judgment.Thorough => chest.HeardAt(Shake.Gentle).Union(chest.HeardAt(Shake.Hard)),
        _ when Listens => chest.HeardAt(Shakes),
        _ => Enumerable.Empty<Item>(),
    };

    /// <summary>How they put it in the ledger: how they judge a chest.</summary>
    public string JudgeWord => Judge switch
    {
        Judgment.Ear => $"listens ({Categories.Adverb(Shakes)})",
        Judgment.Thorough => "listens gently, then hard",
        Judgment.Counter => "counts the rattles",
        Judgment.Weight => "goes by weight",
        Judgment.Colour => "goes by the chest's colour",
        Judgment.Dreamer => "dreams up a price",
        Judgment.Miner => "knows only metal",
        Judgment.Jeweller => "knows only her jewelry",
        Judgment.MagicSense => "feels for magic",
        Judgment.Clueless => "has no idea",
        _ => "knows exactly",
    };

    static readonly string[] Titles = { "Little", "Widow", "Old", "Sergeant", "Magister", "Baron", "Captain", "Lady", "Granny", "Madame" };

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
        // The roadside stall: farmers and villagers. Listening, haggling, counting, and a boy who has no idea.
        new()
        {
            Key = "tam", Name = "Little Tam", Tier = 1, Judge = Judgment.Clueless, Trade = Trade.Offer, EmptyChance = 0.3f,
            Kinds = new[] { 3f, 2, 2, 2, 1, 0.5f }, Colours = Stall,
            Habit = "Never shakes, has no idea. A few coins buys anything, but his chests are sometimes empty.",
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
            Habit = "Listens with a gentle shake: heavy and soft things make no sound for her.",
            Pitches = new[] { "I gave it a gentle shake, dear. {heard}. Would {price} do?",
                              "Just a gentle shake, my wrists aren't what they were. {heard}. {price}?" },
            Accepts = new[] { "Thank you. Truly.", "Bless you." },
        },
        new()
        {
            Key = "hob", Name = "Hob the Farmer", Tier = 1, Shakes = Shake.Hard, Trade = Trade.Haggle,
            Kinds = new[] { 2f, 4, 3, 2, 1, 0.3f }, Colours = Stall,
            Habit = "Listens with a hard shake, and asks about double. Offer him about half.",
            Pitches = new[] { "Gave it a right hard shake. {heard}. {price}, and that's a bargain!",
                              "Shook it hard as a hay bale. {heard}. Say, {price}?" },
            Accepts = new[] { "Deal! Wait till I tell the missus.", "Done. Easy money!" },
            Counters = new[] { "Hmm. {price}, and that's my last word.", "My wife'd skin me. {price}." },
            Leaves = new[] { "Daylight robbery! Forget it." },
        },
        new()
        {
            Key = "mags", Name = "Mags the Fishwife", Tier = 1, Shakes = Shake.Firm, Judge = Judgment.Counter, Markup = (1.3f, 1.6f),
            Kinds = new[] { 3f, 3, 2, 3, 1, 0.5f }, Colours = Stall,
            Habit = "Counts the rattles at a firm shake and charges the same for each. A rusty knife costs what a purse does.",
            Pitches = new[] { "Gave it a good firm shake, love. {count}. A rattle's a rattle: {price}.",
                              "{count}, like fish in a crate. {price}, love." },
            Accepts = new[] { "Pleasure doing business!", "Done, love." },
        },

        // The village shop: soldiers and craftsfolk. Weight, tall tales, false bottoms, and a granny who won't have it shaken.
        new()
        {
            Key = "brask", Name = "Sergeant Brask", Tier = 2, Judge = Judgment.Weight, Markup = (0.9f, 1.1f),
            Kinds = new[] { 2f, 4, 2, 2, 3, 0.5f }, Colours = Village, LockChance = 0.8f,
            Habit = "Goes by weight: heavy means valuable. Metal and coins are heavy, paper and jewelry next to nothing.",
            Pitches = new[] { "Feel the weight of that! {heft}. {price}.", "Army lockbox. {heft}. Weight's worth: {price}." },
            Accepts = new[] { "Done.", "Pleasure, citizen." },
        },
        new()
        {
            Key = "tobias", Name = "Tobias the Tinker", Tier = 2, Shakes = Shake.Firm, Trade = Trade.Haggle, Talker = true,
            Kinds = new[] { 3f, 4, 1, 2, 1, 1 }, Colours = Village,
            Habit = "Listens with a firm shake and talks it up: he often claims a sound that isn't there. Haggles.",
            Pitches = new[] { "Firm shake, and I've a good ear. {heard}. {price}.", "Gave it a firm shake. {heard}, I'm sure of it. {price}?" },
            Accepts = new[] { "Done!", "Good doing business." },
            Counters = new[] { "{price}, and you're robbing me.", "Tch. {price}, final." },
            Leaves = new[] { "Hmph. Next time." },
        },
        new()
        {
            Key = "marta", Name = "Old Marta", Tier = 2, Shakes = Shake.Gentle, HiddenChance = 0.45f, Markup = (1.6f, 2f),
            Kinds = new[] { 2f, 2, 2, 2, 3, 1 }, Colours = Village,
            Habit = "Listens gently. Old family chests, often with false bottoms she's forgotten about. Drop one and listen.",
            Pitches = new[] { "My mother's chest. A gentle shake: {heard}. {price}, dear.", "Gently does it. {heard}. {price}." },
            Accepts = new[] { "Fair's fair.", "Take good care of it." },
        },
        new()
        {
            Key = "wick", Name = "Granny Wick", Tier = 2, Shakes = Shake.Firm, HatesHardShake = true, MissRate = 0.45f, Markup = (1.8f, 2.1f),
            Kinds = new[] { 1f, 2, 5, 1, 3, 0.5f }, Colours = Village,
            Habit = "A seamstress: listens firmly, never hard, so she never hears her own sewing (cloth only flumps at a hard " +
                    "shake). There's usually some in her chests. Shake one hard and it's half again.",
            Pitches = new[] { "A firm shake is all it needs, dearie. {heard}. {price}. And don't you shake it hard!",
                              "Mind my sewing. Firm, never hard. {heard}. Just {price}." },
            Accepts = new[] { "Lovely.", "Mind you fold it nicely." },
            Angry = new[] { "Not so HARD! You'll crease the silk. Half again, for that." },
        },

        // The town shop: a dreamer, a fortune-teller, a miner and an apprentice. Fewer listeners, stranger prices.
        new()
        {
            Key = "wren", Name = "Wren the Apprentice", Tier = 3, Judge = Judgment.MagicSense, SensesMagic = true, Trade = Trade.Offer, MagicChance = 0.25f,
            Kinds = new[] { 1f, 2, 3, 3, 3, 2 }, Colours = new[] { 0.2f, 0.6f, 0.2f, 0 },
            Habit = "Can't tell a coin from a coat, but feels magic. Name a price: she takes a little, unless something's enchanted.",
            Pitches = new[] { "Master said to sell it. I don't know what's in it. What would you give?",
                              "I can't tell what's in it, but I'll know if it's special. What's it worth to you?" },
            Accepts = new[] { "Oh good! Don't tell Master.", "Thank you!" },
            Refusals = new[] { "No... there's something special in there. More, please." },
            Leaves = new[] { "I should ask Master first. Sorry!" },
        },
        new()
        {
            Key = "orrin", Name = "Magister Orrin", Tier = 3, Judge = Judgment.Dreamer, MagicChance = 0.2f, Markup = (1f, 1f),
            Kinds = new[] { 1f, 3, 3, 3, 3, 2 }, Colours = Town,
            Habit = "Dreams up a price. It has nothing to do with the chest: sometimes a steal, sometimes absurd.",
            Pitches = new[] { "The number came to me in a dream: {price}.", "I consulted the stars. {price}, no more, no less." },
            Accepts = new[] { "As foretold.", "Very well." },
        },
        new()
        {
            Key = "grit", Name = "Grit the Miner", Tier = 3, Shakes = Shake.Hard, Judge = Judgment.Miner, Trade = Trade.Haggle,
            Bottom = (0.85f, 1f), Kinds = new[] { 1f, 5, 1, 0.5f, 1, 4 }, Colours = Town,
            Habit = "Knows metal. Prices everything else at half, except jewelry, which he takes for diamonds. Haggles.",
            Pitches = new[] { "Hard shake, miner's hands. {heard}. {price}.", "Dug it out of the old seam. {heard}. {price}." },
            Accepts = new[] { "Done. Back to the pit.", "Ha! Done." },
            Counters = new[] { "{price}. Lower and I'll eat my helmet." },
            Leaves = new[] { "You take me for a fool? Good day." },
        },
        new()
        {
            Key = "zara", Name = "Madame Zara", Tier = 3, Judge = Judgment.Colour, SensesMagic = true, HatesDrop = true, MagicChance = 0.15f,
            HiddenChance = 0.5f, Markup = (1.15f, 1.4f),
            Kinds = new[] { 2f, 2, 2, 3, 3, 3 }, Colours = Town,
            Habit = "Prices by the chest's colour and feels magic. Half her trick boxes hide a false bottom she never prices. " +
                    "Drop one: half again.",
            Pitches = new[] { "A {colour} chest. The spirits say {magic}. {price}. And don't you drop it.",
                              "I see a {colour} chest... and {magic}. {price}. Handle it with respect." },
            Accepts = new[] { "The spirits approve.", "It was meant to be." },
            Angry = new[] { "You dropped my trick box! The spirits demand half again." },
        },
        new()
        {
            Key = "pell", Name = "Pell the Pawnbroker", Tier = 3, Shakes = Shake.Firm, PairChance = 0.6f, MissRate = 0.45f, Markup = (2f, 2.5f),
            Kinds = new[] { 4f, 3, 2, 2, 1, 3 }, Colours = Town,
            Habit = "Sells unredeemed pawn by the lot: two or three of a kind. Says which kinds he heard, never how many. Count them.",
            Pitches = new[] { "Unredeemed lot. Shook it firm: {heard}. {price}, and I don't count.",
                              "Pawned and forgotten. {heard}, in there somewhere. {price}." },
            Accepts = new[] { "Done. No refunds.", "Sold, as seen. Well, as heard." },
        },
        new()
        {
            Key = "shelly", Name = "Shelly the Beachcomber", Tier = 3, Shakes = Shake.Hard, Fakes = new[] { Category.Paper }, FakeChance = 0.5f,
            Kinds = new[] { 2f, 1, 1, 4, 5, 2 }, Colours = new[] { 0.1f, 0.5f, 0.35f, 0.05f },
            Habit = "Combs the beach and swears every rustle is a treasure map. Her chests are often full of sand, which pours " +
                    "at the gentlest shake. Paper never does.",
            Pitches = new[] { "Washed up at low tide! Shook it hard: {heard}. A treasure map, I'd bet my boots. {price}.",
                              "Straight off the beach. {heard}. Maps, letters, who knows! {price}." },
            Accepts = new[] { "Yo ho!", "The sea provides." },
        },

        // The Emporium: a hurried baron, a smuggler, a sharp dealer, a lady with jewels and a crook.
        new()
        {
            Key = "vex", Name = "Baron Vex", Tier = 4, Judge = Judgment.Colour, Patience = 12f, QualityBoost = 0.4f, Markup = (1.5f, 1.8f),
            Kinds = new[] { 4f, 1, 2, 2, 1, 4 }, Colours = new[] { 0, 0, 0.2f, 0.8f },
            Habit = "Prices by the colour, never looks inside, and gives you 12 seconds. His things are often better than their colour.",
            Pitches = new[] { "A {colour} chest. A {colour} chest goes for {price}. You have twelve seconds.",
                              "I don't look inside chests. {colour}: {price}. Quickly." },
            Accepts = new[] { "Splendid.", "You have the eye of a gentleman." },
            Leaves = new[] { "Too slow. Good DAY." },
        },
        new()
        {
            Key = "gull", Name = "Captain Gull", Tier = 4, Shakes = Shake.Hard, Trade = Trade.Haggle, ClaimsHidden = true, HiddenChance = 0.55f,
            MissRate = 0.5f, Kinds = new[] { 3f, 4, 1, 3, 2, 3 }, Colours = Emporium,
            Habit = "Listens hard and always swears there's a smuggler's false bottom, and prices it in. Sometimes there is. Haggles.",
            Pitches = new[] { "Shook it like a gale. {heard}. And a smuggler's false bottom, I'll wager. {price}.",
                              "Off the reef. {heard}, and a secret compartment! {price}." },
            Accepts = new[] { "Aye.", "Tide's turning, I'm off." },
            Counters = new[] { "{price}. Not a copper less." },
            Leaves = new[] { "Waste of a tide." },
        },
        new()
        {
            Key = "pip", Name = "Pip the Sharp", Tier = 4, Judge = Judgment.Thorough, MissRate = 0f, MagicChance = 0.2f, HiddenChance = 0.3f,
            Markup = (1.2f, 1.45f),
            Kinds = new[] { 3f, 3, 2, 2, 2, 3 }, Colours = Emporium,
            Habit = "Listens gently, then hard: hears everything. But enchanted things and false bottoms get past her, and she has plenty of both.",
            Pitches = new[] { "Shook it gently, then hard. {heard}. {price}, and it's fair.", "I listened properly. {heard}. {price}." },
            Accepts = new[] { "Pleasure.", "You won't regret it. Probably." },
        },
        new()
        {
            Key = "ermine", Name = "Lady Ermine", Tier = 4, Judge = Judgment.Jeweller, Always = Category.Jewels, HatesMagnet = true, Fakes = new[] { Category.Jewels }, FakeChance = 0.25f, Markup = (1.4f, 1.6f),
            Kinds = new[] { 2f, 1, 3, 2, 2, 6 }, Colours = Emporium,
            Habit = "Knows only her jewelry, at what she paid; whatever else the maid packed comes free. Some jewels are paste. " +
                    "Use a magnet and it's half again.",
            Pitches = new[] { "My jewels: {heard}. And whatever my maid packed. {price}.", "The jewelry alone is worth {price}. The rest, I couldn't say." },
            Accepts = new[] { "Charming.", "Do look after them." },
            Angry = new[] { "A magnet? On MY jewels? Half again, for the insult." },
        },
        new()
        {
            Key = "silas", Name = "Silas", Tier = 4, Judge = Judgment.Crook, MissRate = 0f, Fakes = new[] { Category.Coins, Category.Jewels }, FakeChance = 0.6f, HiddenChance = 0f,
            Kinds = new[] { 5f, 1, 1, 1, 1, 5 }, Colours = Emporium,
            Habit = "Sells gilded iron as gold and jewels, for half. Real goods come cheap; fakes are worth nothing.",
            Pitches = new[] { "{heard}, friend. Genuine. I'm leaving town tonight, so {price}.", "{heard}. Very rare. For you, just {price}." },
            Accepts = new[] { "Wonderful! No refunds.", "A pleasure. I must be going." },
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
