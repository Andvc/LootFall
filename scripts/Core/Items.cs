using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LootFall.Core;

/// <summary>What kind of thing it is. Each kind has one sound, the one you'd expect in real life.</summary>
public enum Category { Coins, Metal, Cloth, Bottles, Paper, Jewels }

/// <summary>How hard the chest is being shaken. Things only make a sound once the shake is strong enough to move them.</summary>
public enum Shake { Still = 0, Gentle = 1, Firm = 2, Hard = 3 }

/// <summary>How good a thing is, from a copper pouch to a dragon's hoard. Also the colour of a chest: what to expect inside.</summary>
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>
/// A kind of thing and its rules, which match everyday experience: light, hard things (coins, bottles, jewelry)
/// rattle at the gentlest shake; heavy metal only moves when you shake firmly, and its clanking drowns out anything
/// delicate; soft things (cloth, paper) only make a sound when you shake hard.
/// </summary>
/// <param name="StirsAt">The gentlest shake that makes it move and sound.</param>
/// <param name="Delicate">Quiet enough that clanking drowns it out.</param>
/// <param name="Loud">Drowns out delicate things when it clanks.</param>
/// <param name="Iron">A magnet holds it still.</param>
/// <param name="Fragile">Glass: it can break if the chest is dropped or forced open.</param>
/// <param name="BaseValue">What a common one is worth; better ones are worth a multiple (<see cref="Categories.QualityMultiplier"/>).</param>
/// <param name="Tier">The shop tier at which this kind starts turning up.</param>
/// <param name="Names">Name at each quality, common to legendary.</param>
/// <param name="Sprites">Sprite at each quality, under res://assets/sprites/items/.</param>
/// <param name="FakeName">What sounds just like this kind of thing but is worth nothing.</param>
/// <param name="FakeIron">The fake is gilded iron, so a magnet holds it still.</param>
/// <param name="FakeStirsAt">The fake moves at a gentler shake than the real thing (sand pours), or Still if it doesn't.</param>
public sealed record CategoryDef(
    Category Category,
    string Name,
    string Word,
    string Sound,
    float Pitch,
    Shake StirsAt,
    bool Delicate,
    bool Loud,
    bool Iron,
    bool Fragile,
    int BaseValue,
    int Tier,
    string[] Names,
    string[] Sprites,
    string FakeName = null,
    string FakeSprite = null,
    bool FakeIron = true,
    Shake FakeStirsAt = Shake.Still);

/// <summary>One thing in a chest.</summary>
/// <param name="Magic">
/// Enchanted: any thing can be. Worth twice as much, and counts double on display. It sounds like any other; under a
/// witchlight it glows.
/// </param>
/// <param name="Fake">
/// Sounds just like the real thing, worth nothing: gilded iron (a magnet holds it) or a bag of sand (which pours at the
/// gentlest shake, where paper never rustles).
/// </param>
/// <param name="Broken">Glass that broke when the chest was dropped or forced: worth nothing now.</param>
public sealed record Item(Category Category, Rarity Quality, bool Magic = false, bool Fake = false, bool Broken = false)
{
    static int _lastId;

    /// <summary>Which one this is: two purses alike are still two things. Kept when it breaks.</summary>
    public int Id { get; init; } = Interlocked.Increment(ref _lastId);

    public CategoryDef Def => Categories.Get(Category);
    public string Name => Broken ? $"Broken {Def.Names[(int)Quality]}" : Fake ? Def.FakeName : Magic ? $"Enchanted {Entry}" : Entry;

    /// <summary>
    /// The name in the collection: broken things count as what they were, and an enchanted one fills the same entry
    /// (the journal stars it).
    /// </summary>
    public string Entry => Fake ? Def.FakeName : Def.Names[(int)Quality];

    public string Sprite => Fake ? Def.FakeSprite : Def.Sprites[(int)Quality];
    public int Value => Broken ? 0 : Fake ? 1 : Categories.Value(Category, Quality, Magic);
    public bool Iron => Def.Iron || (Fake && Def.FakeIron);

    /// <summary>The gentlest shake that moves it: sand pours sooner than paper rustles.</summary>
    public Shake StirsAt => Fake && Def.FakeStirsAt != Shake.Still ? Def.FakeStirsAt : Def.StirsAt;
    public string Word => Def.Word;

    /// <summary>Its colour when revealed: its quality (an enchanted one glows as well).</summary>
    public Rarity Rarity => Fake || Broken ? Rarity.Common : Quality;

    /// <summary>How much it adds to the shop's exhibition when displayed. Fakes and broken things add nothing.</summary>
    public int Prestige => Fake || Broken ? 0 : (1 << (int)Quality) * (Magic ? 2 : 1);

    public bool Displayable => !Fake && !Broken;
}

public static class Categories
{
    /// <summary>How much more a better thing of the same kind is worth: common, uncommon, rare, epic, legendary.</summary>
    public static readonly int[] QualityMultiplier = { 1, 3, 8, 20, 50 };

    public const int MagicMultiplier = 2;

    public static readonly CategoryDef[] All =
    {
        new(Category.Coins, "Coins", "jingle", "jingle", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: false, 12, 1,
            new[] { "Pouch of Copper", "Silver Crowns", "Gold Doubloons", "Royal Gold", "Dragon's Hoard" },
            new[] { "copper_pouch", "silver_crowns", "gold_doubloons", "royal_gold", "dragon_hoard" },
            "Gilded Slugs", "gilded_slugs"),
        new(Category.Metal, "Metal", "clank", "clank", 1f, Shake.Firm, Delicate: false, Loud: true, Iron: true, Fragile: false, 5, 1,
            new[] { "Rusty Knife", "Soldier's Helm", "Steel Sword", "Knight's Gauntlet", "Royal Rapier" },
            new[] { "rusty_knife", "soldiers_helm", "steel_sword", "knights_gauntlet", "royal_rapier" }),
        new(Category.Cloth, "Cloth", "flump", "flump", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, Fragile: false, 6, 1,
            new[] { "Moth-Eaten Cloak", "Wool Coat", "Silk Cloak", "Golden Mantle", "Royal Robe" },
            new[] { "moth_eaten_cloak", "wool_coat", "silk_cloak", "golden_mantle", "royal_robe" }),
        new(Category.Bottles, "Bottles", "slosh", "slosh", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: true, 8, 2,
            new[] { "Bottle of Ale", "Jar of Honey", "Vintage Brandy", "Elven Wine", "Ambrosia" },
            new[] { "ale_bottle", "honey_jar", "vintage_brandy", "elven_wine", "ambrosia" }),
        new(Category.Paper, "Paper", "rustle", "rustle", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, Fragile: false, 5, 2,
            new[] { "Bundle of Letters", "Merchant's Ledger", "Treasure Map", "Illuminated Codex", "Royal Charter" },
            new[] { "love_letters", "merchants_ledger", "treasure_map", "illuminated_codex", "royal_charter" },
            "Bag of Sand", "sand_bag", FakeIron: false, FakeStirsAt: Shake.Gentle),
        new(Category.Jewels, "Jewelry", "tinkle", "tinkle", 1.1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: false, 20, 3,
            new[] { "Brass Ring", "Silver Locket", "Gold Ring", "Sunfire Diamond", "Jeweled Chalice" },
            new[] { "brass_ring", "silver_locket", "gold_ring", "sunfire_diamond", "jeweled_chalice" },
            "Paste Diamond", "paste_diamond"),
    };

    public static CategoryDef Get(Category category) => All[(int)category];

    public static int Value(Category category, Rarity quality, bool magic) =>
        Get(category).BaseValue * QualityMultiplier[(int)quality] * (magic ? MagicMultiplier : 1);

    /// <summary>
    /// How far a thing's quality lands from its chest's colour, and how often. A step better is a treat, two a rare
    /// event; a long dry spell is softened at purchase (<see cref="GameState.DrySpell"/>), not here.
    /// </summary>
    static readonly (int Steps, float Chance)[] Spread = { (-1, 0.15f), (0, 0.8f), (1, 0.045f), (2, 0.004f), (3, 0.001f) };

    /// <summary>
    /// What's inside a chest of a given colour: mostly that quality, sometimes one worse, now and then a step better, and
    /// once in a long while two or three steps better. Nothing is worse than common or better than legendary: those chances
    /// stay at the end they'd fall off.
    /// </summary>
    public static (Rarity Quality, float Chance)[] Odds(Rarity chest) =>
        Spread.GroupBy(s => (Rarity)Math.Clamp((int)chest + s.Steps, 0, 4))
            .Select(g => (g.Key, g.Sum(s => s.Chance))).OrderBy(o => o.Key).ToArray();

    public static Rarity RollQuality(Random rng, Rarity chest) => rng.PickWeighted(Odds(chest), o => o.Chance).Quality;

    /// <summary>A swing away from a quality, as the spread swings: a step worse most often, sometimes better.</summary>
    public static Rarity Swing(Random rng, Rarity quality)
    {
        var steps = rng.PickWeighted(Spread.Where(s => s.Steps != 0).ToArray(), s => s.Chance).Steps;
        return (Rarity)Math.Clamp((int)quality + steps, 0, 4);
    }

    /// <summary>What one plain thing of this kind is worth on average in a chest of this colour. Sellers price by this.</summary>
    public static int Expected(Category category, Rarity chest) =>
        (int)MathF.Round(Odds(chest).Sum(o => o.Chance * Value(category, o.Quality, false)));

    /// <summary>What one plain thing of this kind is worth if it's as good as its chest's colour. The slip shows this.</summary>
    public static int Nominal(Category category, Rarity chest) => Value(category, chest, false);

    /// <summary>Kinds of things that turn up at a shop (or seller) tier.</summary>
    public static IEnumerable<CategoryDef> UpTo(int tier) => All.Where(c => c.Tier <= tier);

    /// <summary>How heavy one is: metal and coins weigh a lot, paper and jewelry next to nothing.</summary>
    public static float Heft(Category category) => category switch
    {
        Category.Metal => 3f,
        Category.Coins => 2f,
        Category.Bottles => 1.5f,
        Category.Cloth => 1.5f,
        Category.Paper => 0.5f,
        _ => 0.3f,
    };

    public static string HeftWord(Category category) => Heft(category) switch
    {
        >= 2 => "heavy",
        >= 1 => "middling",
        _ => "light",
    };

    public static string LevelName(Shake level) => level switch
    {
        Shake.Gentle => "gentle",
        Shake.Firm => "firm",
        Shake.Hard => "hard",
        _ => "still",
    };

    public static string Adverb(Shake level) => level switch
    {
        Shake.Gentle => "gently",
        Shake.Firm => "firmly",
        Shake.Hard => "hard",
        _ => "not at all",
    };

    /// <summary>
    /// A chest's colour: wood, green, blue, purple (and, for the old royal chests, gold). It tells you what to expect,
    /// not what you'll get.
    /// </summary>
    public static string ChestSkin(Rarity chest) => chest switch
    {
        Rarity.Common => "wood",
        Rarity.Uncommon => "green",
        Rarity.Rare => "blue",
        Rarity.Epic => "purple",
        _ => "gold",
    };

    public static string QualityName(Rarity quality) => quality.ToString().ToLowerInvariant();

    /// <summary>Every item sprite the game can show, for the importer and the collection.</summary>
    public static IEnumerable<string> AllSprites() =>
        All.SelectMany(c => c.Sprites.Append(c.FakeSprite)).Where(s => s != null).Distinct();
}

/// <summary>Every kind of thing there is to find, for the ledger's collection page.</summary>
public static class Collection
{
    /// <summary>Kind by kind: each quality, then (for coins, paper and jewelry) the fake.</summary>
    public static IEnumerable<Item> Entries => Categories.All.SelectMany(c =>
        Enumerable.Range(0, 5).Select(q => new Item(c.Category, (Rarity)q))
            .Concat(c.FakeName != null ? new[] { new Item(c.Category, Rarity.Common, Fake: true) } : Array.Empty<Item>()));

    public static int Size => Entries.Count();
}
