using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>Colour band for the opening glow and cards, from an item's value.</summary>
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>
/// What an item is, as far as your ears can tell. Everything in a family makes the same kind of sound, the one you'd
/// expect in real life: coins jingle, metal clanks, bottles slosh, cloth flumps, paper rustles, jewelry tinkles.
/// </summary>
public enum Family { Coins, Metal, Bottles, Cloth, Paper, Jewels }

/// <summary>
/// How an item sounds when it hits the inside of a chest: its family's recording at a pitch (bigger is lower), plus an
/// optional extra layer (a fizz, a hum, a soft thump). Within a family the differences are small and always the same.
/// </summary>
/// <param name="Group">Sound group: a recorded group under res://assets/audio/ or a synthesized sound.</param>
/// <param name="Layer">A second sound played with it, or null.</param>
public sealed record Signature(string Group, float Pitch, float VolumeDb = 0f, string Layer = null);

/// <param name="Key">Sprite name under res://assets/sprites/items/ (see tools/import_art.py).</param>
/// <param name="Tier">The shop tier at which this starts turning up.</param>
/// <param name="Value">What it always sells for.</param>
/// <param name="Weight">Pounds.</param>
/// <param name="Variant">The small difference within its family that a trained ear (or the ear trumpet) picks out.</param>
public sealed record ItemDef(string Key, string Name, Family Family, int Tier, int Value, float Weight, string Variant,
    Signature Sound, string Flavor)
{
    /// <summary>0 none, 1 faint, 2 strong: how hard a mana compass needle swings toward it.</summary>
    public int Magic { get; init; }

    /// <summary>Iron or steel: a lodestone tugs at it.</summary>
    public bool Magnetic { get; init; }

    /// <summary>Gives off light, plain to see through a keyhole.</summary>
    public bool Glows { get; init; }

    /// <summary>A con artist's fake. Never comes from honest sellers.</summary>
    public bool Fake { get; init; }

    /// <summary>Sprite seen through the keyhole, when it looks like something else.</summary>
    public string LooksLike { get; init; }

    public string LookKey => LooksLike ?? Key;
    public bool Fragile => Family == Family.Bottles;
    public string Word => Families.Word(Family);

    public Rarity Rarity => Value switch
    {
        < 20 => Rarity.Common,
        < 60 => Rarity.Uncommon,
        < 150 => Rarity.Rare,
        < 400 => Rarity.Epic,
        _ => Rarity.Legendary,
    };
}

/// <summary>The six sound families and the rules for telling things apart within each.</summary>
public static class Families
{
    public static readonly Family[] All = Enum.GetValues<Family>();

    public static string Word(Family family) => family switch
    {
        Family.Coins => "jingle",
        Family.Metal => "clank",
        Family.Bottles => "slosh",
        Family.Cloth => "flump",
        Family.Paper => "rustle",
        _ => "tinkle",
    };

    public static string Name(Family family) => family switch
    {
        Family.Coins => "Coins",
        Family.Metal => "Metal",
        Family.Bottles => "Bottles",
        Family.Cloth => "Cloth",
        Family.Paper => "Paper",
        _ => "Jewelry",
    };

    /// <summary>The shop tier at which this family first turns up.</summary>
    public static int Tier(Family family) => family switch
    {
        Family.Paper => 2,
        Family.Jewels => 3,
        _ => 1,
    };

    /// <summary>The first thing to know about a family: what it sounds like and why.</summary>
    public static string Rule(Family family) => family switch
    {
        Family.Coins => "Coins jingle. Count the jingles.",
        Family.Metal => "Metal clanks. Heavy, and worth little unless it's good steel.",
        Family.Bottles => "Bottles slosh. Glass, so a crowbar breaks them.",
        Family.Cloth => "Cloth barely makes a sound: a soft flump. You have to see it to know it.",
        Family.Paper => "Paper rustles. Books add a thump.",
        _ => "Jewelry tinkles: tiny, light and often worth a lot.",
    };

    /// <summary>The word for a sound when you can hear its variant: "bright jingle", "fizzy slosh".</summary>
    public static string Describe(Family family, string variant)
    {
        string adjective = variant switch
        {
            "rings" => "ringing",
            "hums" => "humming",
            "fizzing" => "fizzy",
            "sparkle" => "sparkly",
            "book" => "thumpy",
            "loose" => "papery",
            _ => variant,
        };
        return $"{adjective} {Word(family)}";
    }

    /// <summary>What a variant means within its family, once you've learned it.</summary>
    public static string VariantRule(Family family, string variant) => (family, variant) switch
    {
        (Family.Coins, "dull") => "Dull jingle: copper.",
        (Family.Coins, "bright") => "Bright jingle: silver.",
        (Family.Coins, "heavy") => "Heavy jingle: gold. Weighs a lot.",
        (Family.Metal, "dull") => "Dull clank: old iron.",
        (Family.Metal, "rings") => "A clank that rings on: good steel.",
        (Family.Metal, "hums") => "Rings and hums: enchanted.",
        (Family.Bottles, "big") => "Big, deep slosh: a full bottle.",
        (Family.Bottles, "thick") => "Slow, thick slosh: honey or oil.",
        (Family.Bottles, "small") => "Small, quick slosh: a potion vial.",
        (Family.Bottles, "fizzing") => "Slosh with a fizz: something magic.",
        (Family.Cloth, "heavy") => "Heavier flump with a thump: thick wool.",
        (Family.Cloth, "light") => "Light flump: rags, or fine silk. Look before you trust it.",
        (Family.Paper, "loose") => "Plain rustle: loose paper. Letters, scrolls, maps.",
        (Family.Paper, "book") => "Rustle and thump: a book.",
        (Family.Jewels, "dull") => "Dull tinkle: brass.",
        (Family.Jewels, "bright") => "Bright tinkle: silver or gold.",
        (Family.Jewels, "sparkle") => "A tinkle that sparkles: a gem.",
        _ => variant,
    };
}

public static class ItemCatalog
{
    // Family recordings at a pitch per variant. Bigger or heavier things sound lower.
    static Signature Jingle(float pitch) => new("jingle", pitch);
    static Signature Clank(float pitch) => new("clank", pitch);
    static Signature Ring(float pitch, string layer = null) => new("clank_ring", pitch, 0f, layer);
    static Signature Slosh(float pitch, string layer = null) => new("slosh", pitch, 2f, layer);
    static Signature Flump(float pitch, string layer = null) => new("flump", pitch, 4f, layer);
    static Signature Rustle(string layer = null) => new("rustle", 1f, 0f, layer);
    static Signature Book(string layer = null) => new("rustle_book", 1f, 0f, layer ?? "bookthump");
    static Signature Tinkle(float pitch) => new("tinkle", pitch);

    public static readonly ItemDef[] All =
    {
        // Tier 1: what farmers and villagers have lying around.
        new("copper_pouch", "Pouch of Copper", Family.Coins, 1, 15, 1.0f, "dull", Jingle(0.8f), "Mostly copper. Mostly."),
        new("rusty_knife", "Rusty Knife", Family.Metal, 1, 3, 1.0f, "dull", Clank(1.15f), "Rust is just patina with ambition.") { Magnetic = true },
        new("iron_lantern", "Iron Lantern", Family.Metal, 1, 12, 2.0f, "dull", Clank(0.85f), "Still smells of lamp oil.") { Magnetic = true },
        new("ale_bottle", "Bottle of Ale", Family.Bottles, 1, 8, 2.0f, "big", Slosh(0.85f), "Brewed 'last spring'. Which spring?"),
        new("honey_jar", "Jar of Honey", Family.Bottles, 1, 18, 1.5f, "thick", Slosh(0.65f), "Sticky, golden, and worth more than it looks."),
        new("moth_eaten_cloak", "Moth-Eaten Cloak", Family.Cloth, 1, 2, 0.5f, "light", Flump(1.15f), "The moths were well fed."),
        new("wool_coat", "Wool Coat", Family.Cloth, 1, 25, 1.5f, "heavy", Flump(0.8f, "thump"), "Warm, heavy, barely worn."),

        // Tier 2: soldiers and merchants bring silver, steel and books.
        new("silver_crowns", "Silver Crowns", Family.Coins, 2, 45, 1.0f, "bright", Jingle(1.25f), "Each one stamped with a king nobody remembers."),
        new("steel_sword", "Steel Sword", Family.Metal, 2, 60, 3.0f, "rings", Ring(1.3f), "Balanced, sharp, and somebody's pride.") { Magnetic = true },
        new("soldiers_helm", "Soldier's Helm", Family.Metal, 2, 30, 3.0f, "rings", Ring(1.0f), "Dented exactly where you'd hope it would be.") { Magnetic = true },
        new("love_letters", "Bundle of Letters", Family.Paper, 2, 4, 0.3f, "loose", Rustle(), "Never sent."),
        new("merchants_ledger", "Merchant's Ledger", Family.Paper, 2, 35, 2.0f, "book", Book(), "Two sets of books. One for the tax man."),

        // Tier 3: mages bring potions, scrolls and magic; jewelry starts turning up.
        new("healing_potion", "Healing Potion", Family.Bottles, 3, 45, 0.5f, "small", Slosh(1.35f), "Tastes like pennies. Works, though."),
        new("mana_potion", "Mana Potion", Family.Bottles, 3, 130, 0.5f, "fizzing", Slosh(1.35f, "fizz"), "It hums when you hold it.") { Magic = 1 },
        new("wizard_hat", "Wizard's Hat", Family.Cloth, 3, 80, 0.5f, "light", Flump(1.15f), "Pointy. Faintly smells of ozone.") { Magic = 1 },
        new("spell_scroll", "Spell Scroll", Family.Paper, 3, 110, 0.3f, "loose", Rustle(), "Do not read aloud.") { Magic = 1 },
        new("spellbook", "Spellbook", Family.Paper, 3, 200, 2.5f, "book", Book(), "Some of the pages turn by themselves.") { Magic = 2 },
        new("rune_blade", "Rune Blade", Family.Metal, 3, 240, 3.0f, "hums", Ring(1.3f, "hum"), "The runes glow when it's drawn.") { Magic = 2, Magnetic = true },
        new("brass_ring", "Brass Ring", Family.Jewels, 3, 10, 0.1f, "dull", Tinkle(1.2f), "Turns your finger green."),
        new("gold_ring", "Gold Ring", Family.Jewels, 3, 120, 0.1f, "bright", Tinkle(1.7f), "Somebody's wedding ring."),
        new("moonstone_pendant", "Moonstone Pendant", Family.Jewels, 3, 200, 0.2f, "bright", Tinkle(1.55f), "Cold to the touch, even in summer."),

        // Tier 4: nobles, adventurers and crooks. Gold, gems, legends, and fakes.
        new("gold_doubloons", "Gold Doubloons", Family.Coins, 4, 180, 2.5f, "heavy", Jingle(0.95f), "They still smell faintly of the sea."),
        new("golden_mantle", "Golden Silk Mantle", Family.Cloth, 4, 260, 1.0f, "light", Flump(1.15f), "Woven for a queen who never wore it."),
        new("treasure_map", "Treasure Map", Family.Paper, 4, 380, 0.2f, "loose", Rustle(), "One corner of something much bigger."),
        new("phoenix_tear", "Phoenix Tear", Family.Bottles, 4, 650, 0.3f, "fizzing", Slosh(1.35f, "fizz"), "Warm to the touch, even now.") { Magic = 2, Glows = true },
        new("commander_gauntlet", "Knight-Commander's Gauntlet", Family.Metal, 4, 420, 2.5f, "rings", Ring(0.9f), "Still clenched.") { Magnetic = true },
        new("sunfire_diamond", "Sunfire Diamond", Family.Jewels, 4, 950, 0.1f, "sparkle", new("tinkle_gem", 1.4f), "Catches light that isn't there."),

        // Only from con artists: iron slugs gilded to pass for gold. Same jingle, same weight; a lodestone grabs them.
        new("gilded_slugs", "Gilded Iron Slugs", Family.Coins, 4, 2, 2.5f, "heavy", Jingle(0.95f), "Gold paint on iron. Rubs off on your thumb.")
            { Fake = true, Magnetic = true, LooksLike = "gold_doubloons" },
    };

    static readonly Dictionary<string, ItemDef> ByKey = All.ToDictionary(i => i.Key);

    public static ItemDef Get(string key) => ByKey[key];

    /// <summary>Honest goods up to a tier.</summary>
    public static IEnumerable<ItemDef> UpTo(int tier) => All.Where(i => !i.Fake && i.Tier <= tier);

    /// <summary>How likely an item is to turn up, among those a seller could bring. Pricier things are rarer.</summary>
    public static float Commonness(ItemDef item) => 1f / (1f + item.Value / 60f);
}
