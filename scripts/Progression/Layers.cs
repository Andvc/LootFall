using System;
using System.Collections.Generic;

namespace LootFall.Progression;

/// <summary>Effects of prestige nodes that aren't stats.</summary>
public enum Perk : byte
{
    None,
    /// <summary>Mastery XP +x per rank.</summary>
    MasteryGain,
    /// <summary>Aether +x per rank (transcendence only).</summary>
    AetherGain,
    /// <summary>Each run starts at this share of the deepest depth ever reached (with basic gear at that level).</summary>
    HeadStart,
    /// <summary>Forge costs x less per rank (multiplicative).</summary>
    ForgeDiscount,
    /// <summary>Materials +x per rank.</summary>
    MaterialGain,
    /// <summary>Codex entries count x more per rank.</summary>
    CodexPower,
    /// <summary>Opens Amount more celestial slots per rank.</summary>
    SlotUnlock,
    /// <summary>One more mastery focus slot per rank.</summary>
    ExtraFocus,
    /// <summary>Keep this share of forge levels through an ascension (per rank).</summary>
    ForgeKeep,
    /// <summary>Adds this share to the last cycle's aether each new cycle starts with (Params.TransEcho).</summary>
    AetherEcho,
}

public sealed class LayerNode
{
    public int Id;
    public string Name;
    public Stat Stat;
    public Perk Perk;
    /// <summary>Effect per rank (a stat amount, or the perk's amount).</summary>
    public double Amount;
    /// <summary>Rank k costs CostBase * (k + 1)^CostExp.</summary>
    public double CostBase, CostExp;
    /// <summary>0 means no limit.</summary>
    public int MaxRank;
    /// <summary>Only buyable once the deepest depth ever reached is at least this.</summary>
    public int RequiresDepth;

    public Big Cost(int rank) => Big.Of(CostBase) * Big.Pow(rank + 1, CostExp);
}

/// <summary>
/// A prestige layer's tree. Ranks cost a polynomial in the rank (k + 1)^e, never an exponential: a layer's power then
/// grows like a power of its total currency (about T^(1/(e+1)) ranks), which keeps growing for as long as the player
/// keeps prestiging but can't run away (Pecorella's prestige analysis; Clicker Heroes moved its second layer to a log
/// for the same reason). Families repeat in tiers: each tier costs ten times more and does more per rank, so deeper
/// tiers open up as the shallow ones get expensive.
/// </summary>
public sealed class LayerTree
{
    public readonly List<LayerNode> Nodes = new();
    public readonly string Name;

    LayerTree(string name) => Name = name;

    void Add(string name, Stat stat, Perk perk, double amount, double cost, double exp, int max = 0, int depth = 0) =>
        Nodes.Add(new LayerNode { Id = Nodes.Count, Name = name, Stat = stat, Perk = perk, Amount = amount, CostBase = cost, CostExp = exp, MaxRank = max, RequiresDepth = depth });

    public static LayerTree Ascension(Params p)
    {
        var t = new LayerTree("Ascension");
        double e = p.AscCostExp;
        string[] tiers = { "I", "II", "III", "IV", "V" };
        for (int k = 0; k < tiers.Length; k++)
        {
            double c = Math.Pow(10, k), g = 1 + k, m = p.AscensionPower;
            t.Add($"Ascendant Might {tiers[k]}", Stat.MoreDamage, Perk.None, 0.6 * g * m, 1 * c, e);
            t.Add($"Ascendant Vigor {tiers[k]}", Stat.MoreHp, Perk.None, 0.6 * g * m, 1 * c, e);
            t.Add($"Golden Path {tiers[k]}", Stat.GoldFind, Perk.None, 0.25 * g, 2 * c, e);
            t.Add($"Scholar {tiers[k]}", Stat.XpGain, Perk.None, 0.2 * g, 2 * c, e);
            t.Add($"Seeker {tiers[k]}", Stat.ItemFind, Perk.None, 0.15 * g, 3 * c, e);
            t.Add($"Bounty {tiers[k]}", Stat.Quantity, Perk.None, 0.08 * g, 3 * c, e);
            t.Add($"Discipline {tiers[k]}", Stat.MoreDamage, Perk.MasteryGain, 0.15 * g, 2 * c, e);
            t.Add($"Salvager {tiers[k]}", Stat.MoreDamage, Perk.MaterialGain, 0.2 * g, 2 * c, e);
        }
        // The first ascensions' payoff: cheap, strong and soon full, so the first reset is worth it at once.
        t.Add("Awakened Edge", Stat.MoreDamage, Perk.None, 1.0, 2, 1.5, 6);
        t.Add("Awakened Heart", Stat.MoreHp, Perk.None, 1.0, 2, 1.5, 6);
        t.Add("Head Start", Stat.MoreDamage, Perk.HeadStart, 0.02, 5, 1.6, 25);
        t.Add("Forgemaster", Stat.MoreDamage, Perk.ForgeDiscount, 0.05, 10, 2.0, 10);
        t.Add("Second Focus", Stat.MoreDamage, Perk.ExtraFocus, 1, 50, 3.0, 4);
        t.Add("Forge Memory", Stat.MoreDamage, Perk.ForgeKeep, 0.05, 8, 2.0, 10);
        return t;
    }

    public static LayerTree Transcendence(Params p)
    {
        var t = new LayerTree("Transcendence");
        double e = p.TransCostExp, pe = p.TransPerkCostExp;
        string[] tiers = { "I", "II", "III", "IV" };
        for (int k = 0; k < tiers.Length; k++)
        {
            double c = Math.Pow(10, k), g = 1 + k, m = p.TranscendencePower;
            t.Add($"Starforged Might {tiers[k]}", Stat.MoreDamage, Perk.None, 2.0 * g * m, 1 * c, e);
            t.Add($"Starforged Vigor {tiers[k]}", Stat.MoreHp, Perk.None, 2.0 * g * m, 1 * c, e);
            t.Add($"Aether Flow {tiers[k]}", Stat.MoreDamage, Perk.AetherGain, 1.0 * g, 2 * c, pe);
            t.Add($"Eternal Discipline {tiers[k]}", Stat.MoreDamage, Perk.MasteryGain, 0.3 * g, 2 * c, pe);
            t.Add($"Resonance {tiers[k]}", Stat.MoreDamage, Perk.CodexPower, 0.05 * g, 3 * c, pe);
            t.Add($"Stellar Fortune {tiers[k]}", Stat.ItemFind, Perk.None, 0.3 * g, 3 * c, e);
        }
        t.Add("Celestial Slots", Stat.MoreDamage, Perk.SlotUnlock, 2, 4, 1.8, 8);
        t.Add("Aether Echo", Stat.MoreDamage, Perk.AetherEcho, 0.05, 3, 1.5, 6);
        t.Add("Starlit Path", Stat.MoreDamage, Perk.HeadStart, 0.04, 4, 1.5, 5);
        return t;
    }
}

/// <summary>A layer's progress: its currency (unspent, earned this cycle, earned ever), its ranks and their effects.</summary>
public sealed class LayerState
{
    public readonly LayerTree Tree;
    public readonly int[] Ranks;
    public Big Currency, CycleEarned, Lifetime;
    public int Resets;
    public readonly Sheet Sheet = new();
    readonly double[] _perks = new double[Enum.GetValues<Perk>().Length];

    public LayerState(LayerTree tree)
    {
        Tree = tree;
        Ranks = new int[tree.Nodes.Count];
    }

    public double PerkTotal(Perk perk) => _perks[(int)perk];

    public bool CanBuy(LayerNode n, int maxDepthEver) =>
        (n.MaxRank == 0 || Ranks[n.Id] < n.MaxRank) && maxDepthEver >= n.RequiresDepth && Currency >= n.Cost(Ranks[n.Id]);

    public void Buy(LayerNode n, Scale sc)
    {
        Currency -= n.Cost(Ranks[n.Id]);
        Ranks[n.Id]++;
        Rebuild(sc);
    }

    public void Rebuild(Scale sc)
    {
        Sheet.Clear();
        Array.Clear(_perks);
        foreach (var n in Tree.Nodes)
        {
            int r = Ranks[n.Id];
            if (r == 0) continue;
            if (n.Perk == Perk.None) Sheet.Add(sc, n.Stat, n.Amount * r);
            else _perks[(int)n.Perk] += n.Amount * r;
        }
    }

    /// <summary>Back to nothing (a higher layer reset this one): ranks and currency go; the lifetime total and the count
    /// of resets stay.</summary>
    public void Wipe(Scale sc)
    {
        Array.Clear(Ranks);
        Currency = Big.Zero;
        CycleEarned = Big.Zero;
        Rebuild(sc);
    }
}
