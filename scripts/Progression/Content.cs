using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Progression;

public enum SlotKind : byte { Weapon, Armor, Jewelry, Accessory, Trinket, Tattoo, Companion, Celestial }

/// <summary>
/// One kind of piece and where it goes (docs/equipment.md): the board it's worn on (the body, or a piece's own board),
/// the cells it fills and keeps free, the boards it brings and the cells of them it opens. The framework calls it a
/// slot: what it holds is the best item found for it, and its forge, ranks, mastery and codex entries are its own. Its
/// numbers come from its kind, its size and its place in the unlock order, never by hand.
/// </summary>
public sealed class SlotDef
{
    public int Index;
    public string Name, Family;
    public SlotKind Kind;
    /// <summary>Share of the slot's flat budget that is damage; the rest is health and armor.</summary>
    public double Offense;
    /// <summary>The slot's share of the gear budget: its size, its kind's weight and its place in the unlock order.</summary>
    public double Weight;
    /// <summary>Its size (the worth of the cells it fills, a little of those it keeps free), normalised so a whole body
    /// adds up to Params.ReferenceSlots; percent affixes scale with it too, so many small pieces weigh what one big one does.</summary>
    public double Size;
    /// <summary>Unlocked when the hero first reaches this depth; 0 when a prestige layer's node unlocks it instead.</summary>
    public int UnlockDepth;
    /// <summary>Which stats its affixes can roll, with weights.</summary>
    public Stat[] Pool;
    public double[] PoolWeights;
    public double PoolTotal;

    /// <summary>The board it's worn on (0: the body) and its level (2 on the body, 3 on a piece's board, 4 deeper).</summary>
    public int Board, Level, Tier;
    public bool Prestige;
    public Side Side;
    /// <summary>Where it sits: the body region and site (null region for pieces on boards).</summary>
    public string Region, Site;
    /// <summary>Cells it fills and keeps free (Cell ids).</summary>
    public int[] Takes, Keeps;
    /// <summary>Boards it brings, with the cells of each it opens.</summary>
    public (int Board, int[] Opens)[] Brings;
    /// <summary>The slots on its board it can't be worn with.</summary>
    public int[] Conflicts;
    /// <summary>Its cells' indices within the board (for checking what a host opens).</summary>
    public int[] LocalTakes;
}

/// <summary>A board's structure for the rules: its slots, the slots that bring it, and which of its cells each opens.</summary>
public sealed class BoardDef
{
    public int Id, Level, Size;
    public string Key, Name;
    public int[] Slots;
    /// <summary>Its cells (Cell ids), in board order.</summary>
    public int[] Cells;
    public int[] Hosts;
    /// <summary>For each host (same order as <see cref="Hosts"/>), its cells opened, by index within the board.</summary>
    public bool[][] Opens;

    /// <summary>A cell's index within the board (-1 if it isn't on it).</summary>
    public int Local(int cell) => Array.IndexOf(Cells, cell);

    /// <summary>Whether host <paramref name="host"/> (a slot index) opens a cell of the board (by Cell id).</summary>
    public bool Opens1(int host, int cell)
    {
        int h = Array.IndexOf(Hosts, host), c = Local(cell);
        return h >= 0 && c >= 0 && Opens[h][c];
    }

    /// <summary>Whether host <paramref name="host"/> (a slot index) opens every cell slot <paramref name="s"/> fills.</summary>
    public bool Opened(int host, SlotDef s)
    {
        int h = Array.IndexOf(Hosts, host);
        if (h < 0) return false;
        foreach (int c in s.LocalTakes)
            if (!Opens[h][c]) return false;
        return true;
    }
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
/// The content at full scale: every piece the anatomy has (about 550, from a great helm to the stone in a ring and the
/// keepsake in a pouch on the belt; docs/equipment.md) as slots in the order they open, the boards they're worn on,
/// twelve rarity tiers, and the affix catalog. Generated from rules so the counts can grow without retuning by hand.
/// </summary>
public sealed class Content
{
    public readonly SlotDef[] Slots;
    public readonly BoardDef[] Boards;
    public readonly Anatomy Anatomy;
    public readonly RarityDef[] Rarities;
    public readonly AffixDef[] Affixes;
    readonly double[] _percentBase = new double[Stats.Count];

    public int SlotCount => Slots.Length;
    public IReadOnlyList<Cell> Cells => Anatomy.Cells;

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

    /// <summary>The order pieces open in: the start pieces, then by tier (classic, detail, exotic) with some overlap so
    /// every part of the body fills gradually; a piece on a board after the first piece that brings it; the pieces the
    /// transcendence tree opens last.</summary>
    static List<PieceSpec> UnlockOrder(Anatomy a, Rng rng)
    {
        string[] start = { "Main Hand", "Breastplate", "Sallet", "Right Boot" };
        var key = new Dictionary<PieceSpec, double>();
        foreach (var p in a.Pieces)
            key[p] = p.Tier == Anatomy.StartTier ? Array.IndexOf(start, p.Name) * 1e-3 - 1 : p.Tier + 1.3 * rng.NextDouble() + (p.Prestige ? 100 : 0);
        // Pieces on a board come after the first piece that brings it (a little after, so they don't arrive in a clump).
        for (int pass = 0; pass < 4; pass++)
        {
            bool moved = false;
            foreach (var p in a.Pieces)
            {
                if (p.Board == Anatomy.Body) continue;
                double host = a.Pieces.Where(h => h.Brings.Any(b => b.Board == p.Board)).Min(h => key[h]);
                if (key[p] <= host)
                {
                    key[p] = host + 0.2 + 0.6 * rng.NextDouble();
                    moved = true;
                }
            }
            if (!moved) break;
        }
        return a.Pieces.OrderBy(p => key[p]).ToList();
    }

    public static readonly string[] RarityNames =
    {
        "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic", "Ascended", "Celestial", "Divine", "Primordial", "Eternal", "Transcendent",
    };

    public Content(Params p, int seed = 7, ContentScale scale = null)
    {
        scale ??= new ContentScale();
        var rng = new Rng(seed);
        Anatomy = new Anatomy();
        var order = UnlockOrder(Anatomy, rng);
        // Less content (the content sweep): the last pieces to open by depth go, then any piece on a board nothing
        // left brings.
        if (scale.Slots > 0)
        {
            while (order.Count(x => !x.Prestige) > scale.Slots) order.RemoveAt(order.FindLastIndex(x => !x.Prestige));
            for (bool dropped = true; dropped;)
            {
                dropped = false;
                foreach (var x in order.ToList())
                    if (x.Board != Anatomy.Body && !order.Any(h => h.Brings.Any(b => b.Board == x.Board)))
                    {
                        order.Remove(x);
                        dropped = true;
                    }
            }
        }
        int depthSlots = order.Count(x => !x.Prestige);
        int starts = order.Count(x => x.Tier == Anatomy.StartTier);
        var slots = new List<SlotDef>();
        for (int k = 0; k < order.Count; k++)
        {
            var x = order[k];
            var rule = KindRule(x.Kind);
            int unlock = x.Prestige ? 0
                : k < starts ? 1
                : (int)Math.Round(scale.SlotFirstDepth + (scale.SlotLastDepth - scale.SlotFirstDepth) * Math.Pow((k - starts + 1) / (double)Math.Max(1, depthSlots - starts), 2));
            slots.Add(new SlotDef
            {
                Index = k, Name = x.Name, Family = x.Family, Kind = x.Kind, Offense = rule.Offense, Size = x.Size, UnlockDepth = unlock,
                Pool = rule.Pool.Select(y => y.Item1).ToArray(),
                PoolWeights = rule.Pool.Select(y => y.Item2).ToArray(),
                PoolTotal = rule.Pool.Sum(y => y.Item2),
                Board = x.Board, Level = x.Level, Tier = x.Tier, Prestige = x.Prestige, Side = x.Side, Region = x.Region, Site = x.Site,
                Takes = x.Takes, Keeps = x.Keeps, Brings = x.Brings,
            });
        }
        Slots = slots.ToArray();
        Boards = BuildBoards();
        // Sizes: the largest set that can be worn at once adds up to ReferenceSlots, so a whole body is budgeted like
        // the reference content; then each slot's share of the flat budget.
        var sizes = Slots.Select(x => x.Size).ToArray();
        double full = new Loadout(this).Solve(sizes, Slots.Select(_ => true).ToArray(), new bool[Slots.Length]);
        double growth = Math.Log(p.SlotWeightGrowth) * p.ReferenceSlots / Math.Max(1, Slots.Length);
        foreach (var x in Slots)
        {
            x.Size *= p.ReferenceSlots / full;
            x.Weight = KindRule(x.Kind).Weight * x.Size * Math.Exp(growth * x.Index);
        }

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

    /// <summary>Each board's slots and hosts, each host's opened cells, and each slot's conflicts and local cells.</summary>
    BoardDef[] BuildBoards()
    {
        var a = Anatomy;
        var local = new int[a.Cells.Count];
        foreach (var b in a.Boards)
            for (int i = 0; i < b.Cells.Count; i++) local[b.Cells[i]] = i;
        var takers = new List<int>[a.Cells.Count];
        var keepers = new List<int>[a.Cells.Count];
        for (int c = 0; c < a.Cells.Count; c++)
        {
            takers[c] = new List<int>();
            keepers[c] = new List<int>();
        }
        foreach (var s in Slots)
        {
            s.LocalTakes = s.Takes.Select(c => local[c]).ToArray();
            foreach (int c in s.Takes) takers[c].Add(s.Index);
            foreach (int c in s.Keeps) keepers[c].Add(s.Index);
        }
        foreach (var s in Slots)
        {
            var set = new HashSet<int>();
            foreach (int c in s.Takes)
            {
                set.UnionWith(takers[c]);
                set.UnionWith(keepers[c]);
            }
            foreach (int c in s.Keeps) set.UnionWith(takers[c]);
            set.Remove(s.Index);
            s.Conflicts = set.OrderBy(x => x).ToArray();
        }
        return a.Boards.Select(b =>
        {
            var hosts = Slots.Where(s => s.Brings.Any(x => x.Board == b.Id)).Select(s => s.Index).ToArray();
            return new BoardDef
            {
                Id = b.Id, Level = b.Level, Key = b.Key, Name = b.Name, Size = b.Cells.Count,
                Slots = Slots.Where(s => s.Board == b.Id).Select(s => s.Index).ToArray(),
                Cells = b.Cells.ToArray(),
                Hosts = hosts,
                Opens = hosts.Select(h =>
                {
                    var open = new bool[b.Cells.Count];
                    foreach (int c in Slots[h].Brings.First(x => x.Board == b.Id).Opens) open[local[c]] = true;
                    return open;
                }).ToArray(),
            };
        }).ToArray();
    }

    /// <summary>A percent affix's base size (0 for flat stats).</summary>
    public double PercentBase(Stat s) => _percentBase[(int)s];

    /// <summary>How many distinct affix names the catalog has across all slot kinds (for the abundance count).</summary>
    public int AffixVariants => Affixes.Sum(a => a.Names.Length) * Enum.GetValues<SlotKind>().Length;
}

/// <summary>How much content there is and how its unlocks spread over depth (separate from Params so the tuner leaves
/// the content alone).</summary>
public sealed class ContentScale
{
    /// <summary>Pieces opened by depth (0: all of the anatomy's; fewer drops the last to open).</summary>
    public int Slots;
    public double SlotFirstDepth = 3, SlotLastDepth = 12000;
}
