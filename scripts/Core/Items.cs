using System;
using System.Collections.Generic;
using System.Linq;

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
/// <param name="FakeName">What a crook passes off as this kind of thing: gilded iron that sounds just like it.</param>
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
    string MagicName,
    string MagicSprite,
    string FakeName = null,
    string FakeSprite = null);

/// <summary>One thing in a chest.</summary>
/// <param name="Magic">Enchanted: worth four times as much. It sounds like any other; under a witchlight it glows.</param>
/// <param name="Fake">A crook's gilded iron: sounds just like the real thing, worth nothing. A magnet holds it.</param>
/// <param name="Broken">Glass that broke when the chest was dropped or forced: worth nothing now.</param>
public sealed record Item(Category Category, Rarity Quality, bool Magic = false, bool Fake = false, bool Broken = false)
{
    public CategoryDef Def => Categories.Get(Category);
    public string Name => Broken ? $"Broken {Def.Names[(int)Quality]}" : Fake ? Def.FakeName : Magic ? Def.MagicName : Def.Names[(int)Quality];

    /// <summary>The name in the collection: broken things count as what they were.</summary>
    public string Entry => Fake ? Def.FakeName : Magic ? Def.MagicName : Def.Names[(int)Quality];

    public string Sprite => Fake ? Def.FakeSprite : Magic ? Def.MagicSprite : Def.Sprites[(int)Quality];
    public int Value => Broken ? 0 : Fake ? 1 : Categories.Value(Category, Quality, Magic);
    public bool Iron => Def.Iron || Fake;
    public string Word => Def.Word;

    /// <summary>Its colour when revealed: its quality, one better if it's magic.</summary>
    public Rarity Rarity => Fake || Broken ? Rarity.Common : Magic ? (Rarity)Math.Min(4, (int)Quality + 1) : Quality;

    /// <summary>How much it adds to the shop's exhibition when displayed. Fakes and broken things add nothing.</summary>
    public int Prestige => Fake || Broken ? 0 : (1 << (int)Quality) * (Magic ? 2 : 1);

    public bool Displayable => !Fake && !Broken;
}

public static class Categories
{
    /// <summary>How much more a better thing of the same kind is worth: common, uncommon, rare, epic, legendary.</summary>
    public static readonly int[] QualityMultiplier = { 1, 3, 8, 20, 50 };

    public const int MagicMultiplier = 4;

    public static readonly CategoryDef[] All =
    {
        new(Category.Coins, "Coins", "jingle", "jingle", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: false, 12, 1,
            new[] { "Pouch of Copper", "Silver Crowns", "Gold Doubloons", "Royal Gold", "Dragon's Hoard" },
            new[] { "copper_pouch", "silver_crowns", "gold_doubloons", "royal_gold", "dragon_hoard" },
            "Fairy Gold", "fairy_gold", "Gilded Slugs", "gilded_slugs"),
        new(Category.Metal, "Metal", "clank", "clank", 1f, Shake.Firm, Delicate: false, Loud: true, Iron: true, Fragile: false, 5, 1,
            new[] { "Rusty Knife", "Soldier's Helm", "Steel Sword", "Knight's Gauntlet", "Royal Rapier" },
            new[] { "rusty_knife", "soldiers_helm", "steel_sword", "knights_gauntlet", "royal_rapier" },
            "Rune Blade", "rune_blade"),
        new(Category.Cloth, "Cloth", "flump", "flump", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, Fragile: false, 6, 1,
            new[] { "Moth-Eaten Cloak", "Wool Coat", "Silk Cloak", "Golden Mantle", "Royal Robe" },
            new[] { "moth_eaten_cloak", "wool_coat", "silk_cloak", "golden_mantle", "royal_robe" },
            "Wizard's Hat", "wizard_hat"),
        new(Category.Bottles, "Bottles", "slosh", "slosh", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: true, 8, 2,
            new[] { "Bottle of Ale", "Jar of Honey", "Vintage Brandy", "Elven Wine", "Ambrosia" },
            new[] { "ale_bottle", "honey_jar", "vintage_brandy", "elven_wine", "ambrosia" },
            "Mana Potion", "mana_potion"),
        new(Category.Paper, "Paper", "rustle", "rustle", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, Fragile: false, 5, 2,
            new[] { "Bundle of Letters", "Merchant's Ledger", "Treasure Map", "Illuminated Codex", "Royal Charter" },
            new[] { "love_letters", "merchants_ledger", "treasure_map", "illuminated_codex", "royal_charter" },
            "Spell Scroll", "spell_scroll"),
        new(Category.Jewels, "Jewelry", "tinkle", "tinkle", 1.1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: false, 20, 3,
            new[] { "Brass Ring", "Silver Locket", "Gold Ring", "Sunfire Diamond", "Jeweled Chalice" },
            new[] { "brass_ring", "silver_locket", "gold_ring", "sunfire_diamond", "jeweled_chalice" },
            "Moonstone Pendant", "moonstone_pendant", "Paste Diamond", "paste_diamond"),
    };

    public static CategoryDef Get(Category category) => All[(int)category];

    public static int Value(Category category, Rarity quality, bool magic) =>
        Get(category).BaseValue * QualityMultiplier[(int)quality] * (magic ? MagicMultiplier : 1);

    /// <summary>
    /// What's inside a chest of a given colour: mostly that quality, often one worse, now and then better. A wooden
    /// chest can't hold worse than common, so it's a little more generous upward.
    /// </summary>
    public static (Rarity Quality, float Chance)[] Odds(Rarity chest)
    {
        int c = (int)chest;
        if (c == 0) return new[] { (Rarity.Common, 0.85f), (Rarity.Uncommon, 0.12f), (Rarity.Rare, 0.03f) };
        return new[]
        {
            ((Rarity)(c - 1), 0.4f),
            (chest, 0.5f),
            ((Rarity)Math.Min(4, c + 1), 0.08f),
            ((Rarity)Math.Min(4, c + 2), 0.02f),
        };
    }

    public static Rarity RollQuality(Random rng, Rarity chest) => rng.PickWeighted(Odds(chest), o => o.Chance).Quality;

    /// <summary>What one plain thing of this kind is worth on average in a chest of this colour. The slip shows this.</summary>
    public static int Expected(Category category, Rarity chest) =>
        (int)MathF.Round(Odds(chest).Sum(o => o.Chance * Value(category, o.Quality, false)));

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

    /// <summary>A chest's colour: wood, green, blue, purple. It tells you what to expect, not what you'll get.</summary>
    public static string ChestSkin(Rarity chest) => chest switch
    {
        Rarity.Common => "wood",
        Rarity.Uncommon => "green",
        Rarity.Rare => "blue",
        _ => "purple",
    };

    public static string QualityName(Rarity quality) => quality.ToString().ToLowerInvariant();

    /// <summary>Every item sprite the game can show, for the importer and the collection.</summary>
    public static IEnumerable<string> AllSprites() =>
        All.SelectMany(c => c.Sprites.Append(c.MagicSprite).Append(c.FakeSprite)).Where(s => s != null).Distinct();
}

/// <summary>Every kind of thing there is to find, for the ledger's collection page.</summary>
public static class Collection
{
    /// <summary>Kind by kind: each quality, then the magic one, then (for coins and jewelry) the crook's fake.</summary>
    public static IEnumerable<Item> Entries => Categories.All.SelectMany(c =>
        Enumerable.Range(0, 5).Select(q => new Item(c.Category, (Rarity)q))
            .Append(new Item(c.Category, Rarity.Rare, Magic: true))
            .Concat(c.FakeName != null ? new[] { new Item(c.Category, Rarity.Common, Fake: true) } : Array.Empty<Item>()));

    public static int Size => Entries.Count();
}
