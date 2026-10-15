using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>What an item sounds like when the chest is shaken. Clink always means fragile.</summary>
public enum Sound { Jingle, Clink, Clank, Thud, Rustle }

public sealed record ItemDef(string Name, Sound Sound, Rarity Rarity, int BaseValue, float Weight, string Flavor)
{
    public bool Fragile => Sound == Sound.Clink;
}

public static class ItemCatalog
{
    public static readonly ItemDef[] All =
    {
        // Jingle: coins and jewelry
        new("Pouch of Copper Bits", Sound.Jingle, Rarity.Common, 12, 0.4f, "Mostly copper. Mostly."),
        new("Bundle of Silver Crowns", Sound.Jingle, Rarity.Uncommon, 45, 0.7f, "Each one stamped with a king nobody remembers."),
        new("Tarnished Signet Ring", Sound.Jingle, Rarity.Uncommon, 60, 0.1f, "The family crest has been filed off. Interesting."),
        new("Sunken Doubloons", Sound.Jingle, Rarity.Rare, 150, 1.0f, "They still smell faintly of the sea."),
        new("Pearl Necklace", Sound.Jingle, Rarity.Rare, 230, 0.2f, "Forty-one pearls. There were forty-two."),
        new("Emerald Brooch", Sound.Jingle, Rarity.Epic, 410, 0.2f, "Green as envy, and twice as expensive."),
        new("Crown of the Drowned Prince", Sound.Jingle, Rarity.Legendary, 1200, 1.5f, "Heavier than it looks. Heavier than it should be."),

        // Clink: glass and potions (fragile)
        new("Cracked Hand Mirror", Sound.Clink, Rarity.Common, 8, 0.6f, "Seven years' bad luck, already used up."),
        new("Bottle of Old Brandy", Sound.Clink, Rarity.Common, 25, 1.2f, "Vintage: 'before the war.' Which war?"),
        new("Perfume Vial", Sound.Clink, Rarity.Uncommon, 55, 0.2f, "Smells like money and bad decisions."),
        new("Potion of Mild Levitation", Sound.Clink, Rarity.Rare, 180, 0.4f, "Shake gently. Or don't."),
        new("Stained-Glass Saint", Sound.Clink, Rarity.Epic, 380, 2.0f, "Saint Ottilde, patron of lost keys."),
        new("Phoenix Tear in Crystal", Sound.Clink, Rarity.Legendary, 950, 0.3f, "Warm to the touch, even now."),

        // Clank: arms, armor and instruments
        new("Rusty Dagger", Sound.Clank, Rarity.Common, 10, 0.8f, "Rust is just patina with ambition."),
        new("Soldier's Helm", Sound.Clank, Rarity.Common, 22, 2.2f, "Dented exactly where you'd hope it would be."),
        new("Engraved Rapier", Sound.Clank, Rarity.Uncommon, 75, 1.4f, "The engraving reads: 'Return to Gustav.'"),
        new("Dwarven Warhammer Head", Sound.Clank, Rarity.Rare, 230, 5.5f, "The handle was apparently optional."),
        new("Brass Astrolabe", Sound.Clank, Rarity.Epic, 450, 2.0f, "Points to a star that isn't there anymore."),
        new("Knight-Commander's Gauntlet", Sound.Clank, Rarity.Epic, 470, 2.5f, "Still clenched."),

        // Thud: heavy, solid things
        new("Lump of Pig Iron", Sound.Thud, Rarity.Common, 6, 4.0f, "Someone locked this up. Why?"),
        new("Wooden Toy Horse", Sound.Thud, Rarity.Common, 4, 0.5f, "Well loved."),
        new("Carved Idol", Sound.Thud, Rarity.Uncommon, 70, 2.8f, "It's smiling. It wasn't smiling a minute ago."),
        new("Ancient Stone Tablet", Sound.Thud, Rarity.Rare, 260, 6.0f, "Laws, or possibly a recipe. Scholars disagree."),
        new("Petrified Dragon Egg", Sound.Thud, Rarity.Legendary, 1500, 7.0f, "Petrified. Probably."),

        // Rustle: paper and cloth
        new("Bundle of Love Letters", Sound.Rustle, Rarity.Common, 5, 0.3f, "Never sent."),
        new("Moth-Eaten Cloak", Sound.Rustle, Rarity.Common, 9, 1.0f, "The moths were well fed."),
        new("Merchant's Ledger", Sound.Rustle, Rarity.Uncommon, 40, 0.9f, "Two sets of books. One for the tax man."),
        new("Silk Tapestry", Sound.Rustle, Rarity.Rare, 190, 1.8f, "Depicts a feast. Makes you hungry."),
        new("Treasure Map Fragment", Sound.Rustle, Rarity.Epic, 400, 0.1f, "One corner of something much bigger."),
        new("Royal Deed of Land", Sound.Rustle, Rarity.Epic, 520, 0.1f, "Grants the bearer one (1) duchy. Terms apply."),
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
