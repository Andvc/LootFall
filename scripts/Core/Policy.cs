using System;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// How a sensible player plays, for the simulation and the autoplay bot: open every chest at once, wear whatever adds
/// power, and spend gold on the next chest level whenever it's affordable, with training bought while it's cheap next
/// to that.
/// </summary>
public static class Policy
{
    /// <summary>Training is bought while it costs at most this share of the next chest level.</summary>
    public static double TrainShare = 0.3;

    public static bool ShouldEquip(Opening o) => o.Upgrade;

    /// <summary>Opens and decides on every chest waiting. Returns how many were opened.</summary>
    public static int OpenAll(GameState g, Action<Opening, bool> seen = null)
    {
        int n = 0;
        // Auto-open may have left something big waiting.
        while ((g.Pending ?? g.Open()) is Opening o)
        {
            n++;
            bool equip = ShouldEquip(o);
            if (equip) g.Equip();
            else g.Sell();
            seen?.Invoke(o, equip);
        }
        return n;
    }

    /// <summary>The next thing worth buying now, or null to save up.</summary>
    public static Upgrade? NextBuy(GameState g)
    {
        if (Upgrades.Open(g, Upgrade.AutoOpen) && g.Gold >= Upgrades.Cost(g, Upgrade.AutoOpen) * 1.5) return Upgrade.AutoOpen;
        bool chestOpen = Upgrades.Open(g, Upgrade.ChestLevel);
        double chest = chestOpen ? Upgrades.Cost(g, Upgrade.ChestLevel) : double.MaxValue;
        if (chestOpen && g.Gold >= chest) return Upgrade.ChestLevel;
        // Haste is worth about two levels of Might, so it's weighed at half its price.
        var cheapest = new[] { Upgrade.Might, Upgrade.Vigor, Upgrade.Haste }
            .Where(u => Upgrades.Open(g, u))
            .OrderBy(u => Upgrades.Cost(g, u) * (u == Upgrade.Haste ? 0.5 : 1))
            .Cast<Upgrade?>()
            .FirstOrDefault();
        if (cheapest is Upgrade t && g.Gold >= Upgrades.Cost(g, t) && (!chestOpen || Upgrades.Cost(g, t) <= chest * TrainShare))
            return t;
        return null;
    }

    public static void Spend(GameState g)
    {
        for (int guard = 0; guard < 200 && NextBuy(g) is Upgrade u; guard++)
            if (!g.Buy(u)) break;
    }
}
