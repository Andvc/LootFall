using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Progression;

public enum SlotKind : byte { Weapon, Armor, Jewelry, Accessory, Trinket, Tattoo, Companion, Celestial }

/// <summary>One equipment slot. Its numbers come from its kind and its place in the unlock order, never by hand.</summary>
public sealed class SlotDef
{
    public int Index;
    public string Name;
    public SlotKind Kind;
    /// <summary>Share of the slot's flat budget that is damage; the rest is health and armor.</summary>
    public double Offense;
    /// <summary>The slot's share of the gear budget (grows with unlock order, so late slots still matter).</summary>
    public double Weight;
    /// <summary>Unlocked when the hero first reaches this depth; 0 when a prestige layer's node unlocks it instead.</summary>
    public int UnlockDepth;
    /// <summary>Which stats its affixes can roll, with weights.</summary>
    public Stat[] Pool;
    public double[] PoolWeights;
    public double PoolTotal;
}

/// <summary>One rarity tier: how many affixes, and the milestone that opens it.</summary>
public sealed class RarityDef
{
    public int Tier;
    public string Name;
    public int Affixes;
    /// <summary>Opens once the deepest depth reached is at least this and the hero has ascended at least Ascensions
    /// times and transcended at least Transcendences times: the late tiers are what the prestige layers open.</summary>
    public int UnlockDepth, Ascensions, Transcendences;
}

/// <summary>A percent affix's base size per stat (before the item level's tier growth and the roll).</summary>
public sealed class AffixDef
{
    public Stat Stat;
    public double Base;
    public string[] Names;
}

/// <summary>
/// The reference content at full scale: 150 equipment slots from head to toe (more than any game on the market: ten
/// rings, ten toe rings, earrings, tattoos, trinkets, a familiar and a mount, celestial slots for the late game), twelve
/// rarity tiers, and the affix catalog. Generated from a few rules so the counts can grow without retuning by hand.
/// </summary>
public sealed class Content
{
    public readonly SlotDef[] Slots;
    public readonly RarityDef[] Rarities;
    public readonly AffixDef[] Affixes;
    readonly double[] _percentBase = new double[Stats.Count];

    public int SlotCount => Slots.Length;

    static readonly (SlotKind Kind, string Name)[] SlotList = BuildSlotList();

    static (SlotKind, string)[] BuildSlotList()
    {
        var l = new List<(SlotKind, string)>();
        void Add(SlotKind k, params string[] names) { foreach (var n in names) l.Add((k, n)); }
        string[] sides = { "Left", "Right" };
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        Add(SlotKind.Weapon, "Main Hand", "Off Hand", "Sidearm", "Bow", "Quiver", "Throwing Knives");
        Add(SlotKind.Armor, "Helm", "Breastplate", "Tunic", "Cloak", "Mantle", "Tabard", "Belt", "Tassets", "Gorget", "Visor", "Hood", "Greaves", "Faulds");
        foreach (var s in sides) Add(SlotKind.Armor, $"{s} Pauldron", $"{s} Gauntlet", $"{s} Vambrace", $"{s} Elbow Guard", $"{s} Cuisse", $"{s} Kneeguard", $"{s} Shin Guard", $"{s} Boot");
        Add(SlotKind.Jewelry, "Amulet", "Necklace", "Choker", "Circlet", "Diadem", "Nose Ring", "Hair Pin");
        foreach (var s in sides) Add(SlotKind.Jewelry, $"{s} Earring", $"{s} Bracelet", $"{s} Armlet", $"{s} Anklet");
        foreach (var s in sides) foreach (var f in fingers) Add(SlotKind.Jewelry, $"{s} {f} Ring", $"{s} {f} Toe Ring");
        Add(SlotKind.Accessory, "Brooch", "Cloak Pin", "Sash", "Scabbard", "Bandolier", "Monocle", "Locket", "Signet", "Keyring", "Phylactery", "Left Pouch", "Right Pouch", "Left Garter", "Right Garter", "Spurs");
        foreach (var t in new[] { "Charm", "Relic", "Talisman", "Idol" })
            foreach (var n in new[] { "I", "II", "III", "IV", "V", "VI" }) Add(SlotKind.Trinket, $"{t} {n}");
        Add(SlotKind.Tattoo, "Brow Tattoo", "Neck Tattoo", "Chest Tattoo", "Back Tattoo");
        foreach (var s in sides) Add(SlotKind.Tattoo, $"{s} Shoulder Tattoo", $"{s} Arm Tattoo", $"{s} Hand Tattoo", $"{s} Thigh Tattoo", $"{s} Calf Tattoo");
        Add(SlotKind.Companion, "Familiar", "Familiar's Collar", "Mount", "Saddle", "Barding", "Reins", "Banner", "Lantern", "Totem", "Satchel");
        Add(SlotKind.Celestial, "Halo", "Wings", "Aura", "Starcrown", "Comet", "Nebula", "Eclipse", "Zenith");
        foreach (var c in new[] { "Ram", "Bull", "Twins", "Crab", "Lion", "Maiden", "Scales", "Archer" }) Add(SlotKind.Celestial, $"Constellation of the {c}");
        return l.ToArray();
    }

    /// <summary>Damage share, budget weight and affix pool per slot kind.</summary>
    static (double Offense, double Weight, (Stat, double)[] Pool) KindRule(SlotKind k) => k switch
    {
        SlotKind.Weapon => (1.0, 2.0, new[] { (Stat.FlatDamage, 3.0), (Stat.IncDamage, 3.0), (Stat.CritRating, 2.0), (Stat.CritDamage, 2.0), (Stat.AttackSpeed, 2.0), (Stat.MultiHit, 1.5), (Stat.Leech, 1.0), (Stat.XpGain, 0.5) }),
        SlotKind.Armor => (0.15, 1.0, new[] { (Stat.FlatHp, 3.0), (Stat.IncHp, 3.0), (Stat.Armor, 2.5), (Stat.Dodge, 1.5), (Stat.Block, 1.0), (Stat.Regen, 1.5), (Stat.IncDamage, 1.0), (Stat.GoldFind, 0.5) }),
        SlotKind.Jewelry => (0.6, 0.6, new[] { (Stat.CritRating, 2.0), (Stat.CritDamage, 2.0), (Stat.IncDamage, 2.0), (Stat.FlatDamage, 1.0), (Stat.IncHp, 1.0), (Stat.GoldFind, 1.0), (Stat.ItemFind, 1.0), (Stat.XpGain, 1.0), (Stat.Leech, 0.5) }),
        SlotKind.Accessory => (0.5, 0.7, new[] { (Stat.IncDamage, 1.5), (Stat.IncHp, 1.5), (Stat.MultiHit, 1.0), (Stat.Dodge, 1.0), (Stat.Quantity, 1.0), (Stat.MaterialFind, 1.0), (Stat.GoldFind, 1.0), (Stat.AttackSpeed, 1.0) }),
        SlotKind.Trinket => (0.5, 0.8, new[] { (Stat.IncDamage, 2.0), (Stat.IncHp, 2.0), (Stat.CritDamage, 1.0), (Stat.Regen, 1.0), (Stat.Quantity, 1.0), (Stat.ItemFind, 1.0), (Stat.MaterialFind, 1.0), (Stat.XpGain, 1.0) }),
        SlotKind.Tattoo => (0.5, 0.7, new[] { (Stat.IncDamage, 2.0), (Stat.IncHp, 2.0), (Stat.AttackSpeed, 1.5), (Stat.Armor, 1.0), (Stat.Dodge, 1.0), (Stat.CritRating, 1.0), (Stat.Block, 1.0) }),
        SlotKind.Companion => (0.5, 0.7, new[] { (Stat.GoldFind, 2.0), (Stat.ItemFind, 2.0), (Stat.XpGain, 2.0), (Stat.Quantity, 1.5), (Stat.MaterialFind, 1.5), (Stat.IncDamage, 1.0), (Stat.IncHp, 1.0) }),
        _ => (0.5, 1.5, new[] { (Stat.IncDamage, 2.0), (Stat.IncHp, 2.0), (Stat.CritDamage, 1.0), (Stat.MultiHit, 1.0), (Stat.AttackSpeed, 1.0), (Stat.Regen, 1.0), (Stat.ItemFind, 1.0), (Stat.Quantity, 1.0) }),
    };

    /// <summary>The order slots open in: the eight classics first, then the kinds interleaved so every part of the body
    /// fills gradually; celestial slots last (prestige nodes open those).</summary>
    static readonly string[] ClassicSlots = { "Main Hand", "Breastplate", "Helm", "Left Gauntlet", "Left Boot", "Left Index Ring", "Amulet", "Off Hand" };

    static int[] UnlockOrder(Rng rng)
    {
        var order = ClassicSlots.Select(n => Array.FindIndex(SlotList, s => s.Name == n)).ToList();
        var byKind = SlotList.Select((s, i) => (s.Kind, i)).Where(x => !order.Contains(x.i) && x.Kind != SlotKind.Celestial)
            .GroupBy(x => x.Kind).Select(g => new Queue<int>(g.Select(x => x.i).OrderBy(_ => rng.NextDouble()))).ToList();
        while (byKind.Any(q => q.Count > 0))
        {
            // Draw from kinds in proportion to how many they have left, so all of them run out together.
            int total = byKind.Sum(q => q.Count);
            double pick = rng.NextDouble() * total;
            foreach (var q in byKind)
            {
                if (pick < q.Count) { order.Add(q.Dequeue()); break; }
                pick -= q.Count;
            }
        }
        order.AddRange(SlotList.Select((s, i) => (s.Kind, i)).Where(x => x.Kind == SlotKind.Celestial).Select(x => x.i));
        return order.ToArray();
    }

    public static readonly string[] RarityNames =
    {
        "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic", "Ascended", "Celestial", "Divine", "Primordial", "Eternal", "Transcendent",
    };

    public Content(Params p, int seed = 7, ContentScale scale = null)
    {
        scale ??= new ContentScale();
        var rng = new Rng(seed);
        var order = UnlockOrder(rng);
        int depthSlots = order.Count(i => SlotList[i].Kind != SlotKind.Celestial);
        var slots = new List<SlotDef>();
        for (int k = 0; k < order.Length; k++)
        {
            var (kind, name) = SlotList[order[k]];
            var rule = KindRule(kind);
            int unlock = kind == SlotKind.Celestial ? 0
                : k < scale.StartSlots ? 1
                : (int)Math.Round(scale.SlotFirstDepth + (scale.SlotLastDepth - scale.SlotFirstDepth) * Math.Pow((k - scale.StartSlots + 1) / (double)Math.Max(1, depthSlots - scale.StartSlots), 2));
            slots.Add(new SlotDef
            {
                Index = k,
                Name = name,
                Kind = kind,
                Offense = name == "Off Hand" ? 0.5 : rule.Offense,
                Weight = rule.Weight * Math.Pow(p.SlotWeightGrowth, k) * (k < ClassicSlots.Length ? p.ClassicWeight : 1),
                UnlockDepth = unlock,
                Pool = rule.Pool.Select(x => x.Item1).ToArray(),
                PoolWeights = rule.Pool.Select(x => x.Item2).ToArray(),
                PoolTotal = rule.Pool.Sum(x => x.Item2),
            });
        }
        Slots = slots.ToArray();

        // Depth opens the first six tiers in the first hour; Ascended needs an ascension, and each tier after it one more
        // transcendence (and a depth that comes with it).
        int[] rarityDepth = { 1, 1, 3, 10, 30, 80, 150, 600, 1500, 3000, 6000, 10000 };
        Rarities = RarityNames.Select((n, i) => new RarityDef
        {
            Tier = i, Name = n, Affixes = i, UnlockDepth = rarityDepth[i], Ascensions = i >= 6 ? 1 : 0, Transcendences = Math.Max(0, i - 6),
        }).ToArray();

        Affixes = new[]
        {
            new AffixDef { Stat = Stat.IncDamage, Base = 0.12, Names = new[] { "Savage", "Brutal", "Cruel", "Merciless", "Vicious", "of Ruin" } },
            new AffixDef { Stat = Stat.IncHp, Base = 0.12, Names = new[] { "Stout", "Hale", "Robust", "Enduring", "of the Bear", "of Vigor" } },
            new AffixDef { Stat = Stat.CritDamage, Base = 0.15, Names = new[] { "Deadly", "Keen-edged", "Piercing", "of Slaughter", "of Agony" } },
            new AffixDef { Stat = Stat.AttackSpeed, Base = 0.05, Names = new[] { "Quick", "Swift", "Hasty", "of Alacrity", "of the Wind" } },
            new AffixDef { Stat = Stat.Regen, Base = 0.002, Names = new[] { "Mending", "Renewing", "of the Troll", "of Recovery" } },
            new AffixDef { Stat = Stat.Leech, Base = 0.004, Names = new[] { "Vampiric", "Thirsting", "of the Leech", "of Blood" } },
            new AffixDef { Stat = Stat.GoldFind, Base = 0.10, Names = new[] { "Gilded", "Golden", "of Plenty", "of the Miser" } },
            new AffixDef { Stat = Stat.ItemFind, Base = 0.08, Names = new[] { "Lucky", "Fortunate", "of Fortune", "of the Magpie" } },
            new AffixDef { Stat = Stat.XpGain, Base = 0.08, Names = new[] { "Wise", "Learned", "of Insight", "of the Sage" } },
            new AffixDef { Stat = Stat.MaterialFind, Base = 0.12, Names = new[] { "Salvager's", "Scavenging", "of Scrap" } },
            new AffixDef { Stat = Stat.Quantity, Base = 0.05, Names = new[] { "Bountiful", "Abundant", "of Abundance" } },
            // Flat affixes: their size is a share of the slot's budget (Params.FlatAffixShare), not a percent.
            new AffixDef { Stat = Stat.FlatDamage, Base = 1, Names = new[] { "Sharp", "Jagged", "Honed", "of Edges", "of Wounding" } },
            new AffixDef { Stat = Stat.FlatHp, Base = 1, Names = new[] { "Sturdy", "Thick", "of Life", "of the Oak" } },
            new AffixDef { Stat = Stat.Armor, Base = 1, Names = new[] { "Plated", "Reinforced", "of the Fortress", "of Iron" } },
            new AffixDef { Stat = Stat.Dodge, Base = 1, Names = new[] { "Evasive", "Nimble", "of the Fox", "of Shadows" } },
            new AffixDef { Stat = Stat.Block, Base = 1, Names = new[] { "Warding", "Guarding", "of the Wall", "of Parrying" } },
            new AffixDef { Stat = Stat.CritRating, Base = 1, Names = new[] { "Precise", "Accurate", "of Precision", "of the Hawk" } },
            new AffixDef { Stat = Stat.MultiHit, Base = 1, Names = new[] { "Twin", "Echoing", "of Flurries", "of Repetition" } },
        };
        foreach (var a in Affixes) _percentBase[(int)a.Stat] = a.Base;
    }

    /// <summary>A percent affix's base size (0 for flat stats).</summary>
    public double PercentBase(Stat s) => _percentBase[(int)s];

    /// <summary>How many distinct affix names the catalog has across all slot kinds (for the abundance count).</summary>
    public int AffixVariants => Affixes.Sum(a => a.Names.Length) * Enum.GetValues<SlotKind>().Length;
}

/// <summary>How the content's unlocks spread over depth (separate from Params so the tuner leaves the content alone).</summary>
public sealed class ContentScale
{
    public int StartSlots = 4;
    public double SlotFirstDepth = 3, SlotLastDepth = 12000;
}
