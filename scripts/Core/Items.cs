using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>What an item is made of. It decides what kind of sound it makes and whether it can shatter.</summary>
public enum Family { Coins, Glass, Steel, Wood, Paper }

/// <summary>
/// The sound an item makes when it hits the inside of a chest: one recording group at a fixed pitch, and the word
/// that pops out with it. Every item keeps its signature for the whole game, so learning it pays off. Only
/// deliberate sound-alikes share one, and each pair can be told apart another way (see <see cref="Tell"/>).
/// </summary>
/// <param name="Group">Sound group under res://assets/audio/.</param>
public sealed record Signature(string Group, float Pitch, string Word, float VolumeDb = 0f);

/// <summary>How to tell an item from its sound-alike.</summary>
public enum Tell
{
    None,
    /// <summary>Same sound, different weight: the scale tells them apart.</summary>
    Weight,
    /// <summary>Same sound, different look: you have to see it through the keyhole.</summary>
    Look,
    /// <summary>Same sound, but one glows: obvious in the dark behind the keyhole.</summary>
    Glow,
}

/// <param name="Key">Sprite name under res://assets/sprites/items/ (see tools/import_art.py).</param>
/// <param name="Value">What it always sells for. Values never vary, so the ledger stays true.</param>
/// <param name="Weight">Pounds.</param>
public sealed record ItemDef(string Key, string Name, Family Family, Rarity Rarity, int Value, float Weight, Signature Sound, string Flavor)
{
    public bool Fragile => Family == Family.Glass;

    /// <summary>Gives off light, which you'd see in the dark behind the keyhole.</summary>
    public bool Glows { get; init; }

    /// <summary>Sprite seen through the keyhole, when it looks like something else (a closed purse is a closed purse).</summary>
    public string LooksLike { get; init; }

    public string LookKey => LooksLike ?? Key;
}

public static class ItemCatalog
{
    // Shared by sound-alike pairs; everything else has its own.
    static readonly Signature Purse = new("purse", 0.85f, "chink");
    static readonly Signature Vial = new("vial", 1.35f, "clink");
    static readonly Signature Paper = new("paper", 1.0f, "shff");
    static readonly Signature Cloth = new("cloth", 1.0f, "flmp", 2f);

    public static readonly ItemDef[] All =
    {
        // Coins and jewelry
        new("copper_purse", "Purse of Copper", Family.Coins, Rarity.Common, 12, 1.0f, Purse, "Mostly copper. Mostly."),
        new("silver_crowns", "Silver Crowns", Family.Coins, Rarity.Uncommon, 45, 1.0f, new("crowns", 1.25f, "tiktik"), "Each one stamped with a king nobody remembers."),
        new("signet_ring", "Tarnished Signet Ring", Family.Coins, Rarity.Uncommon, 60, 0.1f, new("ring", 1.5f, "ting"), "The family crest has been filed off. Interesting."),
        new("doubloons", "Sunken Doubloons", Family.Coins, Rarity.Rare, 150, 2.0f, new("doubloons", 0.7f, "CHUNK"), "They still smell faintly of the sea."),
        new("moonstone_pendant", "Moonstone Pendant", Family.Coins, Rarity.Rare, 230, 0.2f, new("pendant", 1.0f, "tsilin"), "Cold to the touch, even in summer."),
        new("gold_purse", "Purse of Gold", Family.Coins, Rarity.Epic, 320, 2.5f, Purse, "Same purse, same chink. Twice the weight.") { LooksLike = "copper_purse" },
        new("sunfire_diamond", "Sunfire Diamond", Family.Coins, Rarity.Legendary, 1200, 0.1f, new("diamond", 1.5f, "PLING"), "Catches light that isn't there."),

        // Glass: fragile, so a crowbar ruins it
        new("hand_mirror", "Cracked Hand Mirror", Family.Glass, Rarity.Common, 8, 0.6f, new("mirror", 0.9f, "krik"), "Seven years' bad luck, already used up."),
        new("old_brandy", "Bottle of Old Brandy", Family.Glass, Rarity.Common, 25, 2.5f, new("brandy", 0.8f, "glonk"), "Vintage: 'before the war.' Which war?"),
        new("perfume_vial", "Perfume Vial", Family.Glass, Rarity.Uncommon, 55, 0.3f, Vial, "Smells like money and bad decisions."),
        new("liquid_sunlight", "Vial of Liquid Sunlight", Family.Glass, Rarity.Epic, 380, 0.5f, new("sunlight", 0.8f, "shimm"), "Glows faintly. Smells like noon.") { Glows = true },
        new("phoenix_tear", "Phoenix Tear", Family.Glass, Rarity.Legendary, 950, 0.3f, Vial, "Warm to the touch, even now.") { Glows = true },

        // Arms and armor
        new("rusty_dagger", "Rusty Dagger", Family.Steel, Rarity.Common, 10, 1.0f, new("dagger", 1.2f, "tank"), "Rust is just patina with ambition."),
        new("soldiers_helm", "Soldier's Helm", Family.Steel, Rarity.Common, 22, 3.0f, new("helm", 0.85f, "BONK"), "Dented exactly where you'd hope it would be."),
        new("engraved_rapier", "Engraved Rapier", Family.Steel, Rarity.Uncommon, 75, 2.0f, new("rapier", 1.0f, "shiing"), "The engraving reads: 'Return to Gustav.'"),
        new("commander_gauntlet", "Knight-Commander's Gauntlet", Family.Steel, Rarity.Epic, 470, 2.5f, new("gauntlet", 1.0f, "KLAKK"), "Still clenched."),
        new("drowned_king_helm", "Helm of the Drowned King", Family.Steel, Rarity.Legendary, 1200, 4.0f, new("kinghelm", 0.75f, "BWONG"), "Heavier than it looks. Heavier than it should be."),

        // Wood and stone: heavy, solid things
        new("splintered_buckler", "Splintered Buckler", Family.Wood, Rarity.Common, 4, 2.0f, new("buckler", 1.0f, "bok"), "Took one hit too many. Probably the last one."),
        new("rusted_lantern", "Rusted Lantern", Family.Wood, Rarity.Common, 6, 2.0f, new("lantern", 1.2f, "klunk", -4f), "Still smells of lamp oil."),
        new("carved_idol", "Carved Idol", Family.Wood, Rarity.Uncommon, 70, 2.5f, new("idol", 1.0f, "toktok"), "It's smiling. It wasn't smiling a minute ago."),
        new("spiral_fossil", "Spiral Fossil", Family.Wood, Rarity.Rare, 260, 5.0f, new("fossil", 0.8f, "GRONK"), "Older than the mountains it was dug out of."),
        new("dragon_scale", "Petrified Dragon Scale", Family.Wood, Rarity.Legendary, 1500, 6.0f, new("dragonscale", 0.6f, "KRANG"), "Petrified. Probably."),

        // Paper and cloth: soft, and it all sounds much the same
        new("love_letters", "Bundle of Love Letters", Family.Paper, Rarity.Common, 5, 0.2f, Paper, "Never sent."),
        new("moth_eaten_cloak", "Moth-Eaten Cloak", Family.Paper, Rarity.Common, 9, 1.5f, Cloth, "The moths were well fed."),
        new("merchants_ledger", "Merchant's Ledger", Family.Paper, Rarity.Uncommon, 40, 2.0f, new("ledger", 1.0f, "BAP"), "Two sets of books. One for the tax man."),
        new("golden_mantle", "Golden Silk Mantle", Family.Paper, Rarity.Rare, 190, 1.5f, Cloth, "Woven for a queen who never wore it."),
        new("treasure_map", "Treasure Map Fragment", Family.Paper, Rarity.Epic, 400, 0.2f, Paper, "One corner of something much bigger."),
        new("royal_deed", "Royal Deed of Land", Family.Paper, Rarity.Epic, 520, 0.2f, new("deed", 0.9f, "pok"), "Rolled in a scroll case. Grants the bearer one (1) duchy. Terms apply."),
    };

    static readonly Dictionary<string, ItemDef> ByKey = All.ToDictionary(i => i.Key);

    static readonly Dictionary<(Family, Rarity), ItemDef[]> ByKind = All
        .GroupBy(i => (i.Family, i.Rarity))
        .ToDictionary(g => g.Key, g => g.ToArray());

    public static ItemDef Get(string key) => ByKey[key];

    public static string FamilyName(Family family) => family switch
    {
        Family.Coins => "Coins and jewelry",
        Family.Glass => "Glass",
        Family.Steel => "Arms and armor",
        Family.Wood => "Wood and stone",
        _ => "Paper and cloth",
    };

    /// <summary>Other items that make exactly the same sound.</summary>
    public static IEnumerable<ItemDef> SoundAlikes(ItemDef item) => All.Where(i => i != item && i.Sound == item.Sound);

    /// <summary>How to tell this item from its sound-alike(s), if it has any.</summary>
    public static Tell TellFor(ItemDef item)
    {
        var other = SoundAlikes(item).FirstOrDefault();
        if (other == null) return Tell.None;
        if (other.Glows != item.Glows) return Tell.Glow;
        if (other.LookKey == item.LookKey) return Tell.Weight;
        return Tell.Look;
    }

    /// <summary>Picks an item of the given family, falling back to the nearest rarity (lower first) if none exists.</summary>
    public static ItemDef Pick(Random rng, Family family, Rarity rarity, Func<ItemDef, bool> allowed = null)
    {
        for (int step = 0; step <= 4; step++)
        {
            foreach (int r in new[] { (int)rarity - step, (int)rarity + step })
            {
                if (r < 0 || r > (int)Rarity.Legendary) continue;
                if (!ByKind.TryGetValue((family, (Rarity)r), out var pool)) continue;
                var options = allowed == null ? pool : pool.Where(allowed).ToArray();
                if (options.Length > 0) return options[rng.Next(options.Length)];
            }
        }
        return null;
    }
}
