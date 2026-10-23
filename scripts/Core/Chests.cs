using System;
using System.Linq;

namespace LootFall.Core;

public enum ChestKind { Wooden, Iron, Golden, Royal }

/// <summary>
/// A kind of chest. Shift adds to the chest level's pull toward rarer things; nothing comes out below Floor; items come
/// out LevelBonus levels above the stage you've reached.
/// </summary>
public sealed record ChestDef(ChestKind Kind, string Name, string Skin, double Shift, Rarity Floor, int LevelBonus);

public static class Chests
{
    public static readonly ChestDef[] All =
    {
        new(ChestKind.Wooden, "Wooden Chest", "wooden", 0, Rarity.Common, 0),
        new(ChestKind.Iron, "Iron Chest", "iron", 0.3, Rarity.Uncommon, 1),
        new(ChestKind.Golden, "Golden Chest", "golden", 0.5, Rarity.Rare, 2),
        new(ChestKind.Royal, "Royal Chest", "royal", 0.7, Rarity.Epic, 3),
    };

    public const int Count = 4;

    public static ChestDef Get(ChestKind kind) => All[(int)kind];

    /// <summary>
    /// The chance of each rarity: a base weight per rarity (1000, 300, 60, 10, 1.2, 0.1), each step up multiplied by
    /// e^(0.08 per chest level above 1, plus the chest's shift), nothing below the chest's floor. At chest level 1 a
    /// wooden chest is 73% common and 0.1% legendary; a royal one is 78% epic, 19% legendary, 3% mythic.
    /// </summary>
    public static double[] Odds(int chestLevel, ChestKind kind)
    {
        var def = Get(kind);
        double beta = Balance.RarityPerLevel * (chestLevel - 1) + def.Shift;
        var w = new double[Rarities.Count];
        for (int r = 0; r < w.Length; r++)
            w[r] = r < (int)def.Floor ? 0 : Balance.RarityWeights[r] * Math.Exp(beta * r);
        double total = w.Sum();
        for (int r = 0; r < w.Length; r++) w[r] /= total;
        return w;
    }

    public static Rarity Roll(Rng rng, int chestLevel, ChestKind kind) => (Rarity)rng.Weighted(Odds(chestLevel, kind));
}
