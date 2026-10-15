using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>What an item sounds like when the chest is shaken. Clink always means fragile.</summary>
public enum Sound { Jingle, Clink, Clank, Thud, Rustle }

/// <param name="Key">Sprite name under res://assets/sprites/items/ (see tools/import_art.py).</param>
public sealed record ItemDef(string Key, string Name, Sound Sound, Rarity Rarity, int BaseValue, float Weight, string Flavor)
{
    public bool Fragile => Sound == Sound.Clink;
}

public static class ItemCatalog
{
    public static readonly ItemDef[] All =
    {
        // Jingle: coins and jewelry
        new("copper_pouch", "Pouch of Copper Bits", Sound.Jingle, Rarity.Common, 12, 0.4f, "Mostly copper. Mostly."),
        new("silver_crowns", "Bundle of Silver Crowns", Sound.Jingle, Rarity.Uncommon, 45, 0.7f, "Each one stamped with a king nobody remembers."),
        new("signet_ring", "Tarnished Signet Ring", Sound.Jingle, Rarity.Uncommon, 60, 0.1f, "The family crest has been filed off. Interesting."),
        new("doubloons", "Sunken Doubloons", Sound.Jingle, Rarity.Rare, 150, 1.0f, "They still smell faintly of the sea."),
        new("moonstone_pendant", "Moonstone Pendant", Sound.Jingle, Rarity.Rare, 230, 0.2f, "Cold to the touch, even in summer."),
        new("gilded_chalice", "Gilded Chalice", Sound.Jingle, Rarity.Rare, 260, 0.9f, "Someone drank from this at a coronation. Probably wine."),
        new("amethyst_signet", "Amethyst Signet", Sound.Jingle, Rarity.Epic, 410, 0.1f, "Purple as royalty, and twice as expensive."),
        new("sunfire_diamond", "Sunfire Diamond", Sound.Jingle, Rarity.Legendary, 1200, 0.1f, "Catches light that isn't there."),

        // Clink: glass and potions (fragile)
        new("hand_mirror", "Cracked Hand Mirror", Sound.Clink, Rarity.Common, 8, 0.6f, "Seven years' bad luck, already used up."),
        new("old_brandy", "Bottle of Old Brandy", Sound.Clink, Rarity.Common, 25, 1.2f, "Vintage: 'before the war.' Which war?"),
        new("perfume_vial", "Perfume Vial", Sound.Clink, Rarity.Uncommon, 55, 0.2f, "Smells like money and bad decisions."),
        new("levitation_potion", "Potion of Mild Levitation", Sound.Clink, Rarity.Rare, 180, 0.4f, "Shake gently. Or don't."),
        new("liquid_sunlight", "Vial of Liquid Sunlight", Sound.Clink, Rarity.Epic, 380, 0.4f, "Glows faintly. Smells like noon."),
        new("phoenix_tear", "Phoenix Tear in Crystal", Sound.Clink, Rarity.Legendary, 950, 0.3f, "Warm to the touch, even now."),

        // Clank: arms and armor
        new("rusty_dagger", "Rusty Dagger", Sound.Clank, Rarity.Common, 10, 0.8f, "Rust is just patina with ambition."),
        new("soldiers_helm", "Soldier's Helm", Sound.Clank, Rarity.Common, 22, 2.2f, "Dented exactly where you'd hope it would be."),
        new("engraved_rapier", "Engraved Rapier", Sound.Clank, Rarity.Uncommon, 75, 1.4f, "The engraving reads: 'Return to Gustav.'"),
        new("orcish_scimitar", "Orcish Scimitar", Sound.Clank, Rarity.Rare, 230, 2.5f, "Nicked, notched, and still terrifying."),
        new("sun_shield", "Gilded Sun Shield", Sound.Clank, Rarity.Epic, 450, 4.0f, "Polished so bright it counts as a weapon."),
        new("commander_gauntlet", "Knight-Commander's Gauntlet", Sound.Clank, Rarity.Epic, 470, 2.5f, "Still clenched."),
        new("drowned_king_helm", "Helm of the Drowned King", Sound.Clank, Rarity.Legendary, 1200, 2.0f, "Heavier than it looks. Heavier than it should be."),

        // Thud: heavy, solid things
        new("splintered_buckler", "Splintered Buckler", Sound.Thud, Rarity.Common, 4, 1.5f, "Took one hit too many. Probably the last one."),
        new("rusted_lantern", "Rusted Lantern", Sound.Thud, Rarity.Common, 6, 2.0f, "Still smells of lamp oil."),
        new("carved_idol", "Carved Idol", Sound.Thud, Rarity.Uncommon, 70, 2.8f, "It's smiling. It wasn't smiling a minute ago."),
        new("spiral_fossil", "Ancient Spiral Fossil", Sound.Thud, Rarity.Rare, 260, 6.0f, "Older than the mountains it was dug out of."),
        new("dragon_scale", "Petrified Dragon Scale", Sound.Thud, Rarity.Legendary, 1500, 7.0f, "Petrified. Probably."),

        // Rustle: paper and cloth
        new("love_letters", "Bundle of Love Letters", Sound.Rustle, Rarity.Common, 5, 0.3f, "Never sent."),
        new("moth_eaten_cloak", "Moth-Eaten Cloak", Sound.Rustle, Rarity.Common, 9, 1.0f, "The moths were well fed."),
        new("merchants_ledger", "Merchant's Ledger", Sound.Rustle, Rarity.Uncommon, 40, 0.9f, "Two sets of books. One for the tax man."),
        new("golden_mantle", "Golden Silk Mantle", Sound.Rustle, Rarity.Rare, 190, 1.8f, "Woven for a queen who never wore it."),
        new("treasure_map", "Treasure Map Fragment", Sound.Rustle, Rarity.Epic, 400, 0.1f, "One corner of something much bigger."),
        new("royal_deed", "Royal Deed of Land", Sound.Rustle, Rarity.Epic, 520, 0.1f, "Grants the bearer one (1) duchy. Terms apply."),
    };

    static readonly Dictionary<(Sound, Rarity), ItemDef[]> ByKey = All
        .GroupBy(i => (i.Sound, i.Rarity))
        .ToDictionary(g => g.Key, g => g.ToArray());

    /// <summary>Picks an item of the given sound, falling back to the nearest rarity (lower first) if none exists.</summary>
    public static ItemDef Pick(Random rng, Sound sound, Rarity rarity)
    {
        for (int step = 0; step <= 4; step++)
        {
            foreach (int r in new[] { (int)rarity - step, (int)rarity + step })
            {
                if (r < 0 || r > (int)Rarity.Legendary) continue;
                if (ByKey.TryGetValue((sound, (Rarity)r), out var pool))
                    return pool[rng.Next(pool.Length)];
            }
        }
        throw new InvalidOperationException($"No items with sound {sound}");
    }
}
