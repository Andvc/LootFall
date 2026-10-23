using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LootFall.Core;

/// <summary>
/// What kind of thing it is: coin, weapons, cloaks, potions, spells, gems (Arms, Garb and Scrolls in code, so saves carry
/// over). Each has one sound, the one you'd expect. Heartsongs (<see cref="Heartsongs"/>) are a kind of their own: never
/// rolled with the others, never on a commission, each with its own song.
/// </summary>
public enum Category { Coins, Arms, Garb, Potions, Scrolls, Gems, Heartsong }

/// <summary>How hard the chest is being shaken. Things only make a sound once the shake is strong enough to move them.</summary>
public enum Shake { Still = 0, Gentle = 1, Firm = 2, Hard = 3 }

/// <summary>
/// How good a thing is, from barrow silver to a dragon's hoard. Also the colour of a chest: what to expect inside. Mythic
/// (red) is the Heartsongs' alone: no chest promises it and nothing else reaches it.
/// </summary>
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

/// <summary>
/// A kind of thing and its rules, which match everyday experience: light, hard things (coin, potions, gems) rattle at
/// the gentlest shake; weapons only move when you shake firmly, and their clanking drowns out anything delicate; soft
/// things (cloaks, spellbooks and scrolls) only make a sound when you shake hard. The Locking's ward lets sound through
/// and nothing else (see docs/world.md).
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
/// <param name="Song">
/// Which Heartsong it is (<see cref="Heartsongs"/>), for the Heartsong kind. Its <paramref name="Quality"/> is the colour
/// of the chest it came in: a merchant prices it as a trinket that fine. To a Keywright it's mythic.
/// </param>
public sealed record Item(Category Category, Rarity Quality, bool Magic = false, bool Fake = false, bool Broken = false, string Song = null)
{
    static int _lastId;

    /// <summary>Which one this is: two purses alike are still two things. Kept when it breaks.</summary>
    public int Id { get; init; } = Interlocked.Increment(ref _lastId);

    public CategoryDef Def => Categories.Get(Category);

    /// <summary>One of the nine Heartsongs.</summary>
    public bool IsSong => Category == Category.Heartsong;

    public HeartsongDef SongDef => IsSong ? Heartsongs.Get(Song) : null;

    public string Name => IsSong ? SongDef.Name : Broken ? $"Broken {Def.Names[(int)Quality]}" : Fake ? Def.FakeName : Magic ? $"Enchanted {Entry}" : Entry;

    /// <summary>
    /// The name in the collection: broken things count as what they were, and an enchanted one fills the same entry
    /// (the journal stars it).
    /// </summary>
    public string Entry => IsSong ? SongDef.Name : Fake ? Def.FakeName : Def.Names[(int)Quality];

    public string Sprite => IsSong ? SongDef.Sprite : Fake ? Def.FakeSprite : Def.Sprites[(int)Quality];
    public int Value => Broken ? 0 : Fake ? 1 : Categories.Value(Category, Quality, Magic);
    public bool Iron => Def.Iron || (Fake && Def.FakeIron);

    /// <summary>The gentlest shake that moves it: sand pours sooner than paper rustles.</summary>
    public Shake StirsAt => Fake && Def.FakeStirsAt != Shake.Still ? Def.FakeStirsAt : Def.StirsAt;
    public string Word => IsSong ? SongDef.Word : Def.Word;

    /// <summary>Its colour when revealed: its quality (an enchanted one glows as well); a Heartsong's is mythic red.</summary>
    public Rarity Rarity => IsSong ? Rarity.Mythic : Fake || Broken ? Rarity.Common : Quality;

    /// <summary>
    /// How much it adds to the shop's exhibition when displayed. Fakes and broken things add nothing; a Heartsong draws
    /// no crowd (it does something else).
    /// </summary>
    public int Prestige => IsSong || Fake || Broken ? 0 : (1 << (int)Quality) * (Magic ? 2 : 1);

    public bool Displayable => !Fake && !Broken;
}

public static class Categories
{
    /// <summary>How much more a better thing of the same kind is worth: common, uncommon, rare, epic, legendary.</summary>
    public static readonly int[] QualityMultiplier = { 1, 3, 10, 30, 100, 100 };

    public const int MagicMultiplier = 2;

    public static readonly CategoryDef[] All =
    {
        new(Category.Coins, "Coin", "jingle", "jingle", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: false, 12, 1,
            new[] { "Barrow Silver", "Moonsilver Marks", "Dwarf-Gold Crowns", "Sunken Sovereigns", "Dragon Gold" },
            new[] { "barrow_silver", "moonsilver_marks", "dwarf_gold_crowns", "sunken_sovereigns", "dragon_gold" },
            "Demon Gold", "demon_gold"),
        new(Category.Arms, "Weapons", "clank", "clank", 1f, Shake.Firm, Delicate: false, Loud: true, Iron: true, Fragile: false, 5, 1,
            new[] { "Goblin Cleaver", "Dwarven Axe", "Warden's Blade", "Flamebrand", "Dragonslayer" },
            new[] { "goblin_cleaver", "dwarven_axe", "wardens_blade", "flamebrand", "dragonslayer" }),
        new(Category.Garb, "Cloaks", "flump", "flump", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, Fragile: false, 6, 1,
            new[] { "Wolfskin Cloak", "Elven Cloak", "Cloak of Shadows", "Archmage's Mantle", "Phoenix Mantle" },
            new[] { "wolfskin_cloak", "elven_cloak", "cloak_of_shadows", "archmages_mantle", "phoenix_mantle" }),
        new(Category.Potions, "Potions", "slosh", "slosh", 1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: true, 8, 2,
            new[] { "Healing Draught", "Elixir of Vigour", "Invisibility Draught", "Mermaid's Breath", "Ambrosia" },
            new[] { "healing_draught", "elixir_of_vigour", "invisibility_draught", "mermaids_breath", "ambrosia" }),
        new(Category.Scrolls, "Spells", "rustle", "rustle", 1f, Shake.Hard, Delicate: false, Loud: false, Iron: false, Fragile: false, 5, 2,
            new[] { "Scroll of Sparks", "Scroll of Fireball", "Summoner's Grimoire", "Lich's Tome", "Keyhold Codex" },
            new[] { "scroll_of_sparks", "scroll_of_fireball", "summoners_grimoire", "lichs_tome", "keyhold_codex" },
            "Scroll Case of Sand", "sand_case", FakeIron: false, FakeStirsAt: Shake.Gentle),
        new(Category.Gems, "Gems", "tinkle", "tinkle", 1.1f, Shake.Gentle, Delicate: true, Loud: false, Iron: false, Fragile: false, 20, 3,
            new[] { "Moonstone", "Elfstone", "Blood Ruby", "Starstone", "Dragon's Eye" },
            new[] { "moonstone", "elfstone", "blood_ruby", "starstone", "dragons_eye" },
            "Paste Gem", "paste_gem"),
        // Heartsongs sing at the gentlest shake, and nothing drowns them out or stills them: the song isn't in the box,
        // it's in a Keywright's ear. Never rolled with the six (see Heartsongs.Sing); each has its own name and song.
        new(Category.Heartsong, "Heartsong", "song", "song", 1f, Shake.Gentle, Delicate: false, Loud: false, Iron: false, Fragile: false,
            Heartsongs.BaseValue, int.MaxValue, Array.Empty<string>(), Array.Empty<string>()),
    };

    /// <summary>The six ordinary kinds of thing, without the Heartsongs.</summary>
    public static IEnumerable<CategoryDef> Kinds => All.Where(c => c.Category != Category.Heartsong);

    public static CategoryDef Get(Category category) => All[(int)category];

    public static int Value(Category category, Rarity quality, bool magic) =>
        Get(category).BaseValue * QualityMultiplier[(int)quality] * (magic ? MagicMultiplier : 1);

    /// <summary>
    /// How far a thing's quality lands from its chest's colour, and how often. A step better is a treat, two a rare
    /// event; a long dry spell is softened at purchase (<see cref="GameState.DrySpell"/>), not here.
    /// </summary>
    static readonly (int Steps, float Chance)[] Spread = { (-1, 0.16f), (0, 0.77f), (1, 0.06f), (2, 0.008f), (3, 0.002f) };

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

    /// <summary>
    /// What one plain thing of this kind is worth on average in a chest of this colour. Sellers price by this. A Heartsong
    /// is priced as a trinket exactly as fine as its chest.
    /// </summary>
    public static int Expected(Category category, Rarity chest) => category == Category.Heartsong ? Nominal(category, chest)
        : (int)MathF.Round(Odds(chest).Sum(o => o.Chance * Value(category, o.Quality, false)));

    /// <summary>What one plain thing of this kind is worth if it's as good as its chest's colour. The slip shows this.</summary>
    public static int Nominal(Category category, Rarity chest) => Value(category, chest, false);

    /// <summary>Kinds of things that turn up at a shop (or seller) tier (never the Heartsongs).</summary>
    public static IEnumerable<CategoryDef> UpTo(int tier) => Kinds.Where(c => c.Tier <= tier);

    /// <summary>How heavy one is: weapons and coin weigh a lot, spells and gems next to nothing, a Heartsong nothing at all.</summary>
    public static float Heft(Category category) => category switch
    {
        Category.Arms => 3f,
        Category.Coins => 2f,
        Category.Potions => 1.5f,
        Category.Garb => 1.5f,
        Category.Scrolls => 0.5f,
        Category.Heartsong => 0f,
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

    /// <summary>
    /// What a kind of thing looks like in Mother Bramble's dreams: a magpie for coin (they steal it), a thorn for a
    /// weapon, a raven's wing for a cloak, a river for a potion, an owl for a spell, a star for a gem.
    /// </summary>
    public static string Sign(Category kind) => kind switch
    {
        Category.Coins => "a magpie",
        Category.Arms => "a thorn",
        Category.Garb => "a raven's wing",
        Category.Potions => "a river",
        Category.Scrolls => "an owl",
        Category.Gems => "a star",
        _ => "a song",
    };

    /// <summary>Every item sprite the game can show, for the importer and the collection.</summary>
    public static IEnumerable<string> AllSprites() =>
        All.SelectMany(c => c.Sprites.Append(c.FakeSprite)).Concat(Heartsongs.All.Select(h => h.Sprite)).Where(s => s != null).Distinct();

    /// <summary>
    /// Collection names from before 0.17's things, so a saved shop's journal carries over: old name, then new (the
    /// kind and quality are the same).
    /// </summary>
    public static readonly Dictionary<string, string> Renamed = new()
    {
        ["Copper Pennies"] = "Barrow Silver", ["Silver Marks"] = "Moonsilver Marks", ["Gold Crowns"] = "Dwarf-Gold Crowns",
        ["Old Sovereigns"] = "Sunken Sovereigns", ["Rusty Dagger"] = "Goblin Cleaver", ["Soldier's Helm"] = "Dwarven Axe",
        ["Steel Longsword"] = "Warden's Blade", ["Knight's Gauntlet"] = "Flamebrand", ["Runeblade"] = "Dragonslayer",
        ["Moth-Eaten Cloak"] = "Wolfskin Cloak", ["Hunter's Coat"] = "Elven Cloak", ["Silk Robe"] = "Cloak of Shadows",
        ["Royal Ermine"] = "Phoenix Mantle", ["Swamp Tonic"] = "Healing Draught", ["Healing Draught"] = "Elixir of Vigour",
        ["Elixir of Vigour"] = "Invisibility Draught", ["Phoenix Tears"] = "Mermaid's Breath", ["Tattered Map"] = "Scroll of Sparks",
        ["Spell Scroll"] = "Scroll of Fireball", ["Bestiary"] = "Summoner's Grimoire", ["Grimoire"] = "Lich's Tome",
        ["Locking Codex"] = "Keyhold Codex", ["Brass Ring"] = "Moonstone", ["Silver Amulet"] = "Elfstone", ["Ruby Ring"] = "Blood Ruby",
    };
}

/// <summary>Every kind of thing there is to find, for the ledger's collection page.</summary>
public static class Collection
{
    /// <summary>Kind by kind: each quality, then (for coin, spells and gems) the fake. Heartsongs have their own page.</summary>
    public static IEnumerable<Item> Entries => Categories.Kinds.SelectMany(c =>
        Enumerable.Range(0, 5).Select(q => new Item(c.Category, (Rarity)q))
            .Concat(c.FakeName != null ? new[] { new Item(c.Category, Rarity.Common, Fake: true) } : Array.Empty<Item>()));

    public static int Size => Entries.Count();
}
