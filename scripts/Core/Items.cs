using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>What kind of thing it is. Each kind has one sound, the one you'd expect in real life.</summary>
public enum Category { Coins, Metal, Cloth, Bottles, Paper, Jewels }

/// <summary>How hard the chest is being shaken. Things only make a sound once the shake is strong enough to move them.</summary>
public enum Shake { Still = 0, Gentle = 1, Firm = 2, Hard = 3 }

/// <summary>Colour band for the opening glow and cards, from an item's value.</summary>
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
/// <param name="Tier">The shop tier at which this kind starts turning up.</param>
/// <param name="Names">Name at each seller tier (index 1..4); farmers bring copper, nobles bring gold.</param>
/// <param name="Sprites">Sprite at each seller tier, under res://assets/sprites/items/.</param>
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
    int BaseValue,
    int Tier,
    string[] Names,
    string[] Sprites,
    string MagicName,
    string MagicSprite,
    string FakeName = null,
    string FakeSprite = null);

/// <summary>One thing in a chest.</summary>
/// <param name="Grade">The tier of the seller who brought it: the same kind of thing is worth more from richer people.</param>
/// <param name="Magic">Enchanted: worth four times as much. It sounds like any other; under a witchlight it glows.</param>
/// <param name="Fake">A crook's gilded iron: sounds just like the real thing, worth nothing. A magnet holds it.</param>
public sealed record Item(Category Category, int Grade, bool Magic = false, bool Fake = false)
{
    public CategoryDef Def => Categories.Get(Category);
    public string Name => Fake ? Def.FakeName : Magic ? Def.MagicName : Def.Names[Grade];
    public string Sprite => Fake ? Def.FakeSprite : Magic ? Def.MagicSprite : Def.Sprites[Grade];
    public int Value => Fake ? 1 : Categories.Value(Category, Grade, Magic);
    public bool Iron => Def.Iron || Fake;
    public string Word => Def.Word;

    public Rarity Rarity => Value switch
    {
        < 15 => Rarity.Common,
        < 40 => Rarity.Uncommon,
        < 100 => Rarity.Rare,
        < 300 => Rarity.Epic,
        _ => Rarity.Legendary,
    };
}

public static class Categories
{
    /// <summary>How much more the same kind of thing is worth by who brings it: farmers, villagers, townsfolk, nobles.</summary>
    public static readonly int[] GradeMultiplier = { 0, 1, 4, 8, 16 };

    public const int MagicMultiplier = 4;

    public static readonly CategoryDef[] All =
    {
        new(Category.Coins, "Coins", "jingle", "jingle", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, 12, 1,
            new[] { null, "Pouch of Copper", "Silver Crowns", "Gold Doubloons", "Royal Gold" },
            new[] { null, "copper_pouch", "silver_crowns", "gold_doubloons", "royal_gold" },
            "Fairy Gold", "fairy_gold", "Gilded Slugs", "gilded_slugs"),
        new(Category.Metal, "Metal", "clank", "clank", 1f, Shake.Firm, Delicate: false, Loud: true, Iron: true, 5, 1,
            new[] { null, "Rusty Knife", "Soldier's Helm", "Steel Sword", "Knight's Gauntlet" },
            new[] { null, "rusty_knife", "soldiers_helm", "steel_sword", "knights_gauntlet" },
            "Rune Blade", "rune_blade"),
        new(Category.Cloth, "Cloth", "flump", "flump", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, 6, 1,
            new[] { null, "Moth-Eaten Cloak", "Wool Coat", "Silk Cloak", "Golden Mantle" },
            new[] { null, "moth_eaten_cloak", "wool_coat", "silk_cloak", "golden_mantle" },
            "Wizard's Hat", "wizard_hat"),
        new(Category.Bottles, "Bottles", "slosh", "slosh", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, 8, 2,
            new[] { null, "Bottle of Ale", "Bottle of Ale", "Jar of Honey", "Vintage Brandy" },
            new[] { null, "ale_bottle", "ale_bottle", "honey_jar", "vintage_brandy" },
            "Mana Potion", "mana_potion"),
        new(Category.Paper, "Paper", "rustle", "rustle", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, 5, 2,
            new[] { null, "Bundle of Letters", "Bundle of Letters", "Merchant's Ledger", "Treasure Map" },
            new[] { null, "love_letters", "love_letters", "merchants_ledger", "treasure_map" },
            "Spell Scroll", "spell_scroll"),
        new(Category.Jewels, "Jewelry", "tinkle", "tinkle", 1.1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, 20, 3,
            new[] { null, "Brass Ring", "Brass Ring", "Gold Ring", "Sunfire Diamond" },
            new[] { null, "brass_ring", "brass_ring", "gold_ring", "sunfire_diamond" },
            "Moonstone Pendant", "moonstone_pendant", "Paste Diamond", "paste_diamond"),
    };

    public static CategoryDef Get(Category category) => All[(int)category];

    public static int Value(Category category, int grade, bool magic) =>
        Get(category).BaseValue * GradeMultiplier[grade] * (magic ? MagicMultiplier : 1);

    /// <summary>Kinds of things that turn up at a shop (or seller) tier.</summary>
    public static IEnumerable<CategoryDef> UpTo(int tier) => All.Where(c => c.Tier <= tier);

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

    /// <summary>Every item sprite the game can show, for the importer and the ledger.</summary>
    public static IEnumerable<string> AllSprites() =>
        All.SelectMany(c => c.Sprites.Where(s => s != null).Append(c.MagicSprite).Append(c.FakeSprite)).Where(s => s != null).Distinct();
}
