using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

public enum Slot { Weapon, Helmet, Armor, Gloves, Boots, Ring, Amulet, Shield }

/// <summary>
/// Everything gear can give. The first three are flat and grow with item level; the rest are shares, the same at any
/// level. Gold Find, Chest Find and Luck don't fight: they feed the loop (more gold, more chests, better rarities).
/// </summary>
public enum Stat { Atk, Hp, Def, AtkPct, HpPct, Speed, Crit, CritDmg, Lifesteal, Dodge, Regen, Double, Stun, GoldFind, ChestFind, Luck }

/// <summary>What a legendary or mythic thing does besides its numbers (see <see cref="Effects"/>).</summary>
public enum Effect { None, Thunder, Ember, Cleave, Meteor, HolyLight, Thorns, Bulwark, Vampire, Midas, Prospector, Fortune, Swiftness, Executioner, SecondWind }

public readonly record struct Affix(Stat Stat, double Value);

public sealed record RarityDef(Rarity Rarity, string Name, double Mult, int Affixes, double SellMult);

public static class Rarities
{
    public static readonly RarityDef[] All =
    {
        new(Rarity.Common, "Common", 1.0, 0, 1),
        new(Rarity.Uncommon, "Uncommon", 1.25, 1, 1.5),
        new(Rarity.Rare, "Rare", 1.55, 2, 2.5),
        new(Rarity.Epic, "Epic", 1.95, 3, 4),
        new(Rarity.Legendary, "Legendary", 2.45, 3, 7),
        new(Rarity.Mythic, "Mythic", 3.1, 4, 12),
    };

    public const int Count = 6;

    public static RarityDef Get(Rarity r) => All[(int)r];

    public static Rarity Up(Rarity r, int steps = 1) => (Rarity)Math.Min(Count - 1, (int)r + steps);
}

public sealed record StatDef(Stat Stat, string Name, string Short, bool Percent, string Icon, string Adjective,
    double Min = 0, double Max = 0, bool Loot = false);

public static class Stats
{
    /// <summary>Affix ranges are an uncommon roll; each rarity above adds a third (<see cref="AffixScale"/>).</summary>
    public static readonly StatDef[] All =
    {
        new(Stat.Atk, "Attack", "ATK", false, "atk", "Mighty"),
        new(Stat.Hp, "Health", "HP", false, "hp", "Hale"),
        new(Stat.Def, "Defense", "DEF", false, "def", "Sturdy"),
        new(Stat.AtkPct, "Attack", "ATK", true, "atk", "Mighty", 0.05, 0.09),
        new(Stat.HpPct, "Health", "HP", true, "hp", "Stalwart", 0.05, 0.09),
        new(Stat.Speed, "Attack Speed", "SPD", true, "spd", "Swift", 0.04, 0.07),
        new(Stat.Crit, "Crit Chance", "CRIT", true, "crit", "Keen", 0.03, 0.05),
        new(Stat.CritDmg, "Crit Damage", "CRIT DMG", true, "critdmg", "Brutal", 0.12, 0.22),
        new(Stat.Lifesteal, "Lifesteal", "STEAL", true, "lifesteal", "Vampiric", 0.015, 0.03),
        new(Stat.Dodge, "Dodge", "DODGE", true, "dodge", "Nimble", 0.02, 0.035),
        new(Stat.Regen, "Regeneration", "REGEN", true, "regen", "Verdant", 0.004, 0.008),
        new(Stat.Double, "Double Strike", "DOUBLE", true, "double", "Twin", 0.03, 0.05),
        new(Stat.Stun, "Stun Chance", "STUN", true, "stun", "Thundering", 0.025, 0.04),
        new(Stat.GoldFind, "Gold Find", "GOLD", true, "gold", "Gilded", 0.08, 0.15, Loot: true),
        new(Stat.ChestFind, "Chest Find", "CHESTS", true, "chest", "Prospector's", 0.05, 0.09, Loot: true),
        new(Stat.Luck, "Luck", "LUCK", true, "luck", "Lucky", 0.012, 0.022, Loot: true),
    };

    public static StatDef Get(Stat s) => All[(int)s];

    public static double AffixScale(Rarity r) => 1 + 0.34 * Math.Max(0, (int)r - 1);

    /// <summary>"+340 HP", "+6% ATK", "+4% Crit Chance", "+0.6% HP /s".</summary>
    public static string Describe(Affix a) => $"+{Value(a)} {Label(a.Stat)}";

    /// <summary>The number alone: "340", "6%", "1.2K".</summary>
    public static string Value(Affix a) => Get(a.Stat).Percent ? Num.Pct(a.Value) : Num.Short(Math.Round(a.Value));

    public static string Label(Stat stat) => stat switch
    {
        Stat.Atk or Stat.Hp or Stat.Def or Stat.AtkPct or Stat.HpPct => Get(stat).Short,
        Stat.Regen => "HP /s",
        _ => Get(stat).Name,
    };
}

public sealed record SlotDef(Slot Slot, string Name, (Stat Stat, double Base)[] Main, (Stat Stat, double Weight)[] Pool, string[] Names, bool Metal);

public static class Slots
{
    public static readonly SlotDef[] All =
    {
        new(Slot.Weapon, "Weapon", new[] { (Stat.Atk, 6.0) },
            new[] { (Stat.AtkPct, 3.0), (Stat.Crit, 3.0), (Stat.CritDmg, 3.0), (Stat.Speed, 2.0), (Stat.Lifesteal, 2.0), (Stat.Double, 2.0), (Stat.Stun, 1.0) },
            new[] { "Rusty Dagger", "Steel Dagger", "Scimitar", "Duelist's Rapier", "Crystal Greatsword", "Starfall Greatsword" }, true),
        new(Slot.Helmet, "Helmet", new[] { (Stat.Hp, 18.0), (Stat.Def, 1.2) },
            new[] { (Stat.HpPct, 3.0), (Stat.Regen, 2.0), (Stat.Crit, 1.5), (Stat.Dodge, 1.5), (Stat.Luck, 1.5), (Stat.ChestFind, 1.5) },
            new[] { "Horned Cap", "Iron Helm", "Plumed Helm", "Visored Helm", "Golden Helm", "Crimson Warhelm" }, true),
        new(Slot.Armor, "Armor", new[] { (Stat.Hp, 30.0), (Stat.Def, 2.0) },
            new[] { (Stat.HpPct, 3.0), (Stat.Regen, 2.0), (Stat.Dodge, 1.5), (Stat.Lifesteal, 1.0), (Stat.GoldFind, 1.5) },
            new[] { "Padded Vest", "Iron Cuirass", "Pauldron Mail", "Bastion Plate", "Golden Cuirass", "Dragonheart Plate" }, true),
        new(Slot.Gloves, "Gloves", new[] { (Stat.Atk, 2.5) },
            new[] { (Stat.AtkPct, 2.0), (Stat.Crit, 2.5), (Stat.CritDmg, 2.0), (Stat.Speed, 2.5), (Stat.Double, 2.0), (Stat.GoldFind, 1.5) },
            new[] { "Leather Glove", "Brawler's Bracer", "Flame Gloves", "Spellweave Gloves", "Gilded Gauntlets", "Archon's Grip" }, false),
        new(Slot.Boots, "Boots", new[] { (Stat.Hp, 14.0), (Stat.Def, 0.8) },
            new[] { (Stat.Speed, 3.0), (Stat.Dodge, 2.5), (Stat.HpPct, 1.5), (Stat.ChestFind, 2.0), (Stat.Luck, 1.5) },
            new[] { "Cloth Shoes", "Iron Boots", "Tidewalkers", "Shadowstep Boots", "Golden Greaves", "Phoenix Greaves" }, true),
        new(Slot.Ring, "Ring", new[] { (Stat.Atk, 3.0) },
            new[] { (Stat.Crit, 2.5), (Stat.CritDmg, 2.5), (Stat.AtkPct, 2.0), (Stat.Lifesteal, 1.5), (Stat.Luck, 2.0), (Stat.GoldFind, 1.5) },
            new[] { "Iron Band", "Ruby Ring", "Jeweled Band", "Spiral Ring", "Moon Ring", "Eclipse Ring" }, false),
        new(Slot.Amulet, "Amulet", new[] { (Stat.Atk, 1.5), (Stat.Hp, 12.0) },
            new[] { (Stat.HpPct, 2.0), (Stat.AtkPct, 2.0), (Stat.Regen, 1.5), (Stat.Luck, 2.0), (Stat.ChestFind, 2.0), (Stat.GoldFind, 1.5) },
            new[] { "Vial Pendant", "Silver Locket", "Diamond Charm", "Sapphire Charm", "Sun Locket", "Heart of the Abyss" }, false),
        new(Slot.Shield, "Shield", new[] { (Stat.Def, 3.0), (Stat.Hp, 10.0) },
            new[] { (Stat.HpPct, 2.5), (Stat.Dodge, 2.0), (Stat.Stun, 2.0), (Stat.Regen, 1.5), (Stat.Lifesteal, 1.0) },
            new[] { "Wooden Buckler", "Steel Buckler", "Golden Buckler", "Kite Shield", "Crested Aegis", "Aegis of Dawn" }, true),
    };

    public const int Count = 8;

    public static SlotDef Get(Slot s) => All[(int)s];
}

/// <summary>
/// One legendary power. Magnitude is 1 on a legendary and 1.5 on a mythic (two of the same add up). Offense and Defense
/// are what it's worth to the power score, as a share added to each side (see <see cref="HeroStats.Power"/>).
/// </summary>
public sealed record EffectDef(Effect Effect, string Name, string Title, Slot[] Slots, double Offense, double Defense,
    Func<double, string> Describe);

public static class Effects
{
    public static readonly EffectDef[] All =
    {
        new(Effect.None, "", "", Array.Empty<Slot>(), 0, 0, _ => ""),
        new(Effect.Thunder, "Thunder", "of Storms", new[] { Slot.Weapon, Slot.Gloves, Slot.Ring }, 0.27, 0,
            m => $"{Num.Pct(ThunderChance)} on hit: lightning for {Num.Pct(ThunderDamage * m)} ATK"),
        new(Effect.Ember, "Ember", "of Embers", new[] { Slot.Weapon, Slot.Gloves }, 0.4, 0,
            m => $"Hits burn for {Num.Pct(EmberShare * m)} more over 2s"),
        new(Effect.Cleave, "Cleave", "of Cleaving", new[] { Slot.Weapon }, 0.2, 0,
            m => $"Blows also hit every other foe for {Num.Pct(Math.Min(1, CleaveShare * m))}"),
        new(Effect.Meteor, "Meteor", "of Meteors", new[] { Slot.Ring, Slot.Amulet, Slot.Gloves }, 0.4, 0,
            m => $"Every {MeteorEvery}th attack: a meteor hits all foes for {Num.Pct(MeteorDamage * m)} ATK"),
        new(Effect.HolyLight, "Holy Light", "of Dawn", new[] { Slot.Helmet, Slot.Armor, Slot.Amulet }, 0, 0.25,
            m => $"Every {HolyEvery}s: heal {Num.Pct(HolyHeal * m)} of max HP"),
        new(Effect.Thorns, "Thorns", "of Thorns", new[] { Slot.Armor, Slot.Shield }, 0.1, 0.05,
            m => $"Attackers take {Num.Pct(ThornsShare * m)} of the damage they deal"),
        new(Effect.Bulwark, "Bulwark", "of the Bulwark", new[] { Slot.Shield, Slot.Helmet }, 0, 0.25,
            m => $"Every {BulwarkEvery}s: a barrier of {Num.Pct(BulwarkShare * m)} max HP"),
        new(Effect.Vampire, "Vampire", "of the Vampire", new[] { Slot.Weapon, Slot.Ring, Slot.Amulet }, 0, 0.2,
            m => $"+{Num.Pct(VampireSteal * m)} lifesteal"),
        new(Effect.Midas, "Midas", "of Midas", new[] { Slot.Gloves, Slot.Ring, Slot.Amulet, Slot.Boots }, 0, 0,
            m => $"+{Num.Pct(MidasGold * m)} gold from every kill"),
        new(Effect.Prospector, "Prospector", "of the Prospector", new[] { Slot.Boots, Slot.Helmet, Slot.Amulet }, 0, 0,
            m => $"+{Num.Pct(ProspectorChests * m)} chest find"),
        new(Effect.Fortune, "Fortune", "of Fortune", new[] { Slot.Ring, Slot.Amulet, Slot.Helmet }, 0, 0,
            m => $"+{Num.Pct(FortuneLuck * m)} luck"),
        new(Effect.Swiftness, "Swiftness", "of the Wind", new[] { Slot.Boots, Slot.Gloves, Slot.Weapon }, 0.22, 0,
            m => $"+{Num.Pct(SwiftSpeed * m)} attack speed; first blow on each wave is instant"),
        new(Effect.Executioner, "Executioner", "of the Headsman", new[] { Slot.Weapon, Slot.Gloves }, 0.15, 0,
            m => $"Foes (not bosses) below {Num.Pct(ExecuteBelow * m)} HP die at once"),
        new(Effect.SecondWind, "Second Wind", "of Second Wind", new[] { Slot.Armor, Slot.Amulet, Slot.Helmet }, 0, 0.3,
            m => $"Once a fight, below 30% HP: heal {Num.Pct(SecondWindHeal * m)}"),
    };

    public const double ThunderChance = 0.18, ThunderDamage = 1.5, EmberShare = 0.4, CleaveShare = 0.6, MeteorDamage = 2.5;
    public const int MeteorEvery = 6;
    public const double HolyEvery = 8, HolyHeal = 0.2, ThornsShare = 0.4, BulwarkEvery = 10, BulwarkShare = 0.25;
    public const double VampireSteal = 0.08, MidasGold = 0.6, ProspectorChests = 0.25, FortuneLuck = 0.05, SwiftSpeed = 0.2;
    public const double ExecuteBelow = 0.2, SecondWindHeal = 0.5;

    public static EffectDef Get(Effect e) => All[(int)e];

    public static double Magnitude(Rarity r) => r >= Rarity.Mythic ? 1.5 : r >= Rarity.Legendary ? 1 : 0;
}

/// <summary>A piece of gear: what slot, how rare, what level, and what it gives.</summary>
public sealed class Item
{
    public Slot Slot { get; init; }
    public Rarity Rarity { get; init; }
    public int Level { get; init; }
    public string Name { get; init; } = "";
    public Affix[] Main { get; init; } = Array.Empty<Affix>();
    public Affix[] Affixes { get; init; } = Array.Empty<Affix>();
    public Effect Effect { get; init; }

    /// <summary>The picture: res://assets/sprites/items/{Sprite}.png.</summary>
    public string Sprite => $"{Slot.ToString().ToLowerInvariant()}_{(int)Rarity}";

    public IEnumerable<Affix> All => Main.Concat(Affixes);

    public double Get(Stat stat)
    {
        double total = 0;
        foreach (var a in Main) if (a.Stat == stat) total += a.Value;
        foreach (var a in Affixes) if (a.Stat == stat) total += a.Value;
        return total;
    }

    public double SellValue => Math.Max(1, Math.Round(Balance.SellBase * Balance.GearScale(Level) * Rarities.Get(Rarity).SellMult));
}

/// <summary>Makes gear: main stats from the slot, scaled by level and rarity; affixes from the slot's pool; a legendary effect.</summary>
public static class ItemGen
{
    public static Item Roll(Rng rng, Slot slot, Rarity rarity, int level)
    {
        var def = Slots.Get(slot);
        double scale = Balance.GearScale(level) * Rarities.Get(rarity).Mult;
        var main = def.Main.Select(m => new Affix(m.Stat, m.Base * scale * rng.Range(0.92, 1.08))).ToArray();

        var affixes = new List<Affix>();
        var pool = def.Pool.ToList();
        for (int i = 0; i < Rarities.Get(rarity).Affixes && pool.Count > 0; i++)
        {
            int pick = rng.Weighted(pool.Select(p => p.Weight).ToArray());
            var stat = pool[pick].Stat;
            pool.RemoveAt(pick);
            var sd = Stats.Get(stat);
            affixes.Add(new Affix(stat, rng.Range(sd.Min, sd.Max) * Stats.AffixScale(rarity)));
        }

        var effect = Effect.None;
        if (rarity >= Rarity.Legendary)
        {
            var options = Effects.All.Where(e => e.Slots.Contains(slot)).ToArray();
            effect = rng.Pick(options).Effect;
        }
        return new Item
        {
            Slot = slot, Rarity = rarity, Level = level, Main = main, Affixes = affixes.ToArray(), Effect = effect,
            Name = NameFor(def, rarity, affixes, effect),
        };
    }

    /// <summary>"Steel Dagger", "Keen Scimitar" (its strongest affix), "Moon Ring of Storms" (its legendary power).</summary>
    static string NameFor(SlotDef def, Rarity rarity, List<Affix> affixes, Effect effect)
    {
        string name = def.Names[(int)rarity];
        if (effect != Effect.None) return $"{name} {Effects.Get(effect).Title}";
        if (affixes.Count == 0) return name;
        var best = affixes.OrderByDescending(a => a.Value / Math.Max(1e-9, Stats.Get(a.Stat).Max)).First();
        return $"{Stats.Get(best.Stat).Adjective} {name}";
    }
}
