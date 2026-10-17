using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// Someone who brings a chest to the counter. Every seller shakes their chest before pricing it, as hard as they
/// like, and prices only what they heard. Their pitch says how hard they shook and what they heard, so their blind
/// spot is always in plain sight: what doesn't sound at their shake, what the clanking drowned out, and magic, which
/// nobody can hear.
/// </summary>
/// <param name="Key">Portrait under res://assets/sprites/sellers/.</param>
/// <param name="Tier">The shop tier at which they start coming in. Things are worth more from richer people.</param>
/// <param name="Shakes">How hard they shake. Still means they don't shake it at all.</param>
/// <param name="Kinds">How likely each kind of thing is in their chests, by Category.</param>
/// <param name="Habit">What you learn about them, in a sentence.</param>
/// <param name="Pitches">Lines with {heard} (what they heard) and {price}.</param>
/// <param name="Thorough">Shakes gently and then hard: hears everything except magic.</param>
/// <param name="Talker">Talks up a chest: often claims a sound that isn't there.</param>
/// <param name="Crook">Sells gilded iron as the real thing, cheap. Knows exactly what's inside.</param>
/// <param name="MagicChance">How likely each thing they bring is enchanted (from the town shop on).</param>
/// <param name="MissRate">How often their chest holds something their shaking can't hear.</param>
public sealed record SellerDef(
    string Key,
    string Name,
    int Tier,
    Shake Shakes,
    string Skin,
    float[] Kinds,
    string Habit,
    string[] Pitches,
    string[] Accepts,
    bool Thorough = false,
    bool Talker = false,
    bool Crook = false,
    float MagicChance = 0.05f,
    float MissRate = 0.55f)
{
    /// <summary>What this seller knows is in the chest, by their own shaking.</summary>
    public IEnumerable<Item> Notices(Chest chest) =>
        Crook ? chest.Items
        : Thorough ? chest.HeardAt(Shake.Gentle).Union(chest.HeardAt(Shake.Hard))
        : chest.HeardAt(Shakes);

    public string ShakeWord => Thorough ? "gently, then hard" : Shakes == Shake.Still ? "not at all" : Categories.Adverb(Shakes);

    static readonly string[] Titles = { "Little", "Widow", "Old", "Sergeant", "Magister", "Baron", "Captain" };

    /// <summary>What Bram calls them: "Penn", "Hob", "Brask".</summary>
    public string Short => Name.Split(' ').First(w => !Titles.Contains(w));
}

public static class Sellers
{
    static readonly string[] Shrug = { "Suit yourself.", "Your loss.", "Another time, then." };

    //                                            coins metal cloth bottles paper jewels
    public static readonly SellerDef[] All =
    {
        // Tier 1: the roadside stall. Farmers and villagers with everyday things.
        new("tam", "Little Tam", 1, Shake.Still, "wood", new[] { 3f, 2, 2, 2, 1, 0.5f },
            "Never shakes anything. Whatever's inside, he doesn't know.",
            new[] { "Found it by the river. I didn't shake it, it might break! {price}?", "Mum says never shake things. Is {price} okay?" },
            new[] { "Yay!", "I'm buying sweets!" },
            MissRate: 1f),
        new("penn", "Widow Penn", 1, Shake.Gentle, "green", new[] { 2f, 3, 3, 1, 1, 0.5f },
            "Only shakes gently. Heavy things and soft things make no sound for her.",
            new[] { "I gave it a gentle shake, dear. {heard}. Would {price} do?", "Just a gentle shake, my wrists aren't what they were. {heard}. {price}?" },
            new[] { "Thank you. Truly.", "Bless you." }),
        new("hob", "Hob the Farmer", 1, Shake.Hard, "wood", new[] { 2f, 4, 3, 2, 1, 0.3f },
            "Shakes hard. The clanking drowns out anything delicate.",
            new[] { "Gave it a right hard shake. {heard}. {price}?", "Shook it hard as a hay bale. {heard}. Say, {price}?" },
            new[] { "Deal! Wait till I tell the missus.", "Done. Easy money!" }),
        new("mags", "Mags the Fishwife", 1, Shake.Firm, "green", new[] { 3f, 3, 1, 3, 1, 0.5f },
            "Shakes firmly. Soft things stay quiet, and clanks drown out the delicate ones.",
            new[] { "Gave it a good firm shake, love. {heard}. {price}.", "A firm shake, like a fish crate. {heard}. {price}, love." },
            new[] { "Pleasure doing business!", "Done, love." }),

        // Tier 2: the village shop. Soldiers and merchants.
        new("brask", "Sergeant Brask", 2, Shake.Hard, "wood", new[] { 2f, 5, 2, 2, 2, 0.5f },
            "Shakes like a drill sergeant: hard. Clanks drown out the delicate things.",
            new[] { "Shook it like a new recruit. {heard}. {price}.", "Hard shake, soldier's way. {heard}. {price}, and cheap at that." },
            new[] { "Done.", "Pleasure, citizen." }),
        new("tobias", "Tobias the Tinker", 2, Shake.Firm, "blue", new[] { 3f, 4, 1, 2, 1, 1 },
            "Shakes firmly and talks it up. He often claims a sound that isn't there.",
            new[] { "Firm shake, and I've a good ear. {heard}. {price}.", "Gave it a firm shake. {heard}, I'm sure of it. {price}?" },
            new[] { "Done!", "Good doing business." },
            Talker: true),
        new("marta", "Old Marta", 2, Shake.Gentle, "blue", new[] { 2f, 2, 2, 2, 3, 1 },
            "Only shakes gently. Heavy things and soft things make no sound for her.",
            new[] { "A gentle shake, dear. I don't break things. {heard}. {price}.", "I shook it gently. {heard}. {price}, dear." },
            new[] { "Fair's fair.", "Take good care of it." }),

        // Tier 3: the town shop. Mages, whose things are often enchanted.
        new("wren", "Wren the Apprentice", 3, Shake.Gentle, "purple", new[] { 1f, 2, 3, 3, 3, 2 },
            "Shakes gently, as Master taught. Brings magic without knowing it.",
            new[] { "I shook it gently, like Master says. {heard}. Um, {price}?", "Gently does it. {heard}. Is {price} okay?" },
            new[] { "Oh good! Don't tell Master.", "Thank you!" },
            MagicChance: 0.15f),
        new("orrin", "Magister Orrin", 3, Shake.Hard, "purple", new[] { 1f, 4, 3, 3, 3, 2 },
            "Shakes hard and never looks closer. Brings magic without knowing it.",
            new[] { "A vigorous shake. {heard}. {price}.", "Surplus from my study. I shook it hard. {heard}. {price}." },
            new[] { "Very well.", "Mind the scrolls." },
            MagicChance: 0.15f, MissRate: 0.4f),

        // Tier 4: the Emporium. Nobles, adventurers and a crook.
        new("vex", "Baron Vex", 4, Shake.Firm, "red", new[] { 4f, 3, 1, 2, 1, 3 },
            "His butler shakes firmly. Soft things stay quiet, and clanks drown out the delicate ones.",
            new[] { "My butler gave it a firm shake. {heard}. {price}, naturally.", "A Vex heirloom. Firmly shaken. {heard}. {price}." },
            new[] { "You have the eye of a gentleman.", "Splendid." },
            MissRate: 0.4f),
        new("gull", "Captain Gull", 4, Shake.Hard, "blue", new[] { 3f, 4, 1, 3, 2, 3 },
            "Shakes like a storm at sea: hard. Clanks drown out the delicate things.",
            new[] { "Shook it like a gale. {heard}. {price}.", "Hauled it off the reef and shook it hard. {heard}. {price}." },
            new[] { "Aye.", "Tide's turning, I'm off." },
            MissRate: 0.4f),
        new("pip", "Pip the Sharp", 4, Shake.Hard, "blue", new[] { 3f, 3, 2, 2, 2, 3 },
            "Shakes gently, then hard. He hears everything; only magic gets past him.",
            new[] { "Shook it gently, then hard. {heard}. {price}, and it's fair.", "I listened properly. {heard}. {price}." },
            new[] { "Pleasure.", "You won't regret it. Probably." },
            Thorough: true, MissRate: 0f),
        new("silas", "Silas", 4, Shake.Hard, "red", new[] { 5f, 1, 1, 1, 1, 5 },
            "Sells gilded iron as gold and jewels, cheap. Iron sounds just like the real thing.",
            new[] { "{heard}, friend. Genuine. I'm leaving town tonight, so {price}.", "{heard}. Very rare. For you, just {price}." },
            new[] { "Wonderful! No refunds.", "A pleasure. I must be going." },
            Crook: true, MissRate: 0f),
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

/// <summary>Builds a visit: what the seller brings, what they say is in it, and what they ask.</summary>
public static class Market
{
    /// <summary>Sellers ask this much over what they think it's worth.</summary>
    public const float MarkupMin = 1.4f, MarkupMax = 1.8f;

    /// <summary>A crook asks this much of what the real thing would be worth: too good to be true.</summary>
    public const float CrookMin = 0.5f, CrookMax = 0.7f;

    /// <summary>How likely each thing in a crook's chest that could be faked is a fake.</summary>
    public const float FakeChance = 0.6f;

    /// <summary>How often a talker claims a sound that isn't there.</summary>
    public const float PhantomChance = 0.6f;

    /// <summary>How many things a seller of each tier brings, with weights.</summary>
    static readonly (int Count, float Weight)[][] Counts =
    {
        null,
        new[] { (1, 0.35f), (2, 0.55f), (3, 0.1f) },
        new[] { (2, 0.4f), (3, 0.6f) },
        new[] { (2, 0.5f), (3, 0.5f) },
        new[] { (3, 0.6f), (4, 0.4f) },
    };

    public static Visit CreateVisit(Random rng, SellerDef seller, int shopTier)
    {
        // Whether this one holds something the seller's shaking can't hear is rolled first, so each seller's blind spot
        // turns up about as often as their MissRate: often enough to pay for listening, not so often that buying
        // blind pays.
        bool missing = rng.NextFloat() < seller.MissRate;
        var chest = Roll(rng, seller, shopTier);
        for (int attempt = 0; attempt < 40 && seller.Notices(chest).Count() < chest.Items.Count != missing; attempt++)
            chest = Roll(rng, seller, shopTier);
        return Price(rng, seller, chest, shopTier);
    }

    static Chest Roll(Random rng, SellerDef seller, int shopTier)
    {
        var kinds = Categories.UpTo(shopTier).Select(c => c.Category).ToList();
        int count = Math.Min(kinds.Count, rng.PickWeighted(Counts[seller.Tier], c => c.Weight).Count);
        var picked = new List<Category>();
        if (seller.Crook) picked.Add(rng.PickWeighted(kinds.Where(Fakeable).ToArray(), c => seller.Kinds[(int)c]));
        while (picked.Count < count)
            picked.Add(rng.PickWeighted(kinds.Except(picked).ToArray(), c => seller.Kinds[(int)c]));

        float magicChance = shopTier >= 3 ? seller.MagicChance : 0f;
        var items = picked.Select(c =>
        {
            bool fake = seller.Crook && Fakeable(c) && rng.NextFloat() < FakeChance;
            bool magic = !fake && rng.NextFloat() < magicChance;
            return new Item(c, seller.Tier, magic, fake);
        }).ToList();
        return new Chest(items, seller.Skin);
    }

    static bool Fakeable(Category category) => Categories.Get(category).FakeName != null;

    /// <summary>The seller shakes it, says what they heard, and asks for what they think it's worth.</summary>
    public static Visit Price(Random rng, SellerDef seller, Chest chest, int shopTier, int? ask = null)
    {
        var claim = seller.Notices(chest).Select(i => i.Category).ToList();
        if (seller.Talker && rng.NextFloat() < PhantomChance)
        {
            var absent = Categories.UpTo(shopTier).Select(c => c.Category).Where(c => !chest.Has(c) && !claim.Contains(c)).ToArray();
            if (absent.Length > 0) claim.Add(rng.PickWeighted(absent, c => seller.Kinds[(int)c]));
        }
        claim.Sort();

        // Everyone prices what they heard as plain goods of their own kind: nobody can hear magic.
        float claimed = claim.Sum(c => Categories.Value(c, seller.Tier, false));
        float price = seller.Crook ? claimed * rng.Between(CrookMin, CrookMax)
            : claim.Count == 0 ? seller.Tier * rng.Between(2f, 4f)
            : claimed * rng.Between(MarkupMin, MarkupMax);
        int asking = ask ?? Money.Round(price);
        string pitch = rng.PickOne(seller.Pitches).Replace("{heard}", Sellers.Heard(claim)).Replace("{price}", $"{asking}g");
        return new Visit(seller, chest, claim, asking, pitch);
    }
}

public enum VisitOutcome { Deciding, Bought, Passed }

/// <summary>One seller at the counter with one chest, what they say is in it, and their price. Buy or pass.</summary>
public sealed class Visit
{
    public SellerDef Seller { get; }
    public Chest Chest { get; }

    /// <summary>The kinds of things the seller says they heard. Their price is for these.</summary>
    public IReadOnlyList<Category> Claim { get; }

    public int Ask { get; }
    public string Pitch { get; }
    public VisitOutcome Outcome { get; private set; } = VisitOutcome.Deciding;
    public int PricePaid { get; private set; }

    public Visit(SellerDef seller, Chest chest, IReadOnlyList<Category> claim, int ask, string pitch)
    {
        Seller = seller;
        Chest = chest;
        Claim = claim;
        Ask = Math.Max(1, ask);
        Pitch = pitch;
    }

    public int Grade => Seller.Tier;

    public string Buy(Random rng)
    {
        Outcome = VisitOutcome.Bought;
        PricePaid = Ask;
        return rng.PickOne(Seller.Accepts);
    }

    public string Pass(Random rng)
    {
        Outcome = VisitOutcome.Passed;
        return Sellers.PassLine(rng);
    }

    /// <summary>
    /// Why this chest was a good or bad deal, in one or two sentences: what the seller missed and why, what they made
    /// up, or what they faked.
    /// </summary>
    public string Explain()
    {
        string name = Seller.Short;
        var fakes = Chest.Items.Where(i => i.Fake).ToList();
        if (fakes.Count > 0)
            return $"{Join(fakes.Select(i => i.Name))}: gilded iron. A magnet would have silenced {(fakes.Count == 1 ? "it" : "them")}.";
        if (Seller.Crook)
            return $"All genuine this time, and {name} sold it for less than it's worth. Too good to be true, but true.";

        var lines = new List<string>();
        var phantoms = Claim.Where(c => !Chest.Has(c)).ToList();
        if (phantoms.Count > 0)
            lines.Add($"{name} made up the {Categories.Get(phantoms[0]).Word}: there was no {Categories.Get(phantoms[0]).Name.ToLowerInvariant()}.");

        var noticed = Seller.Notices(Chest).ToHashSet();
        var unheard = Chest.Items.Where(i => !noticed.Contains(i)).ToList();
        var magic = Chest.Items.Where(i => i.Magic).ToList();
        if (unheard.Count > 0)
        {
            string why = Seller.Shakes == Shake.Still ? $"{name} never shook it"
                : unheard.All(i => i.Def.Delicate && Chest.Clanks(Seller.Shakes)) ? $"The clank drowned out the {Join(unheard.Select(i => i.Word))} for {name}"
                : unheard.All(i => i.Def.StirsAt > Seller.Shakes) ? $"{name} didn't shake hard enough to stir the {Join(unheard.Select(i => i.Word))}"
                : $"{name} shook {Categories.Adverb(Seller.Shakes)} and missed the {Join(unheard.Select(i => i.Word))}";
            lines.Add($"{why}: {Join(unheard.Select(i => i.Name))}, unpriced.");
        }
        if (magic.Count > 0)
            lines.Add($"Nobody can hear magic: the {Join(magic.Select(i => i.Name))} went for the price of plain goods.");
        if (lines.Count == 0)
            lines.Add($"{name} heard everything, and always asks a bit more than it's worth.");
        return string.Join(" ", lines);
    }

    static string Join(IEnumerable<string> parts)
    {
        var list = parts.ToList();
        return list.Count <= 1 ? list.FirstOrDefault() ?? "" : string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1];
    }
}
