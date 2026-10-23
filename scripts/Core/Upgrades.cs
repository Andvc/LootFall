using System;

namespace LootFall.Core;

public enum Upgrade { ChestLevel, Might, Vigor, Haste, AutoOpen }

public sealed record UpgradeDef(Upgrade Upgrade, string Name, string Icon, string Blurb);

/// <summary>What gold buys: better chests, training, and (from zone 2) a chest-opening helper.</summary>
public static class Upgrades
{
    public static readonly UpgradeDef[] All =
    {
        new(Upgrade.ChestLevel, "Chest Level", "chest", "Rarer loot from every chest"),
        new(Upgrade.Might, "Might", "atk", $"+{Num.Pct(Balance.TrainBonus)} attack per level"),
        new(Upgrade.Vigor, "Vigor", "hp", $"+{Num.Pct(Balance.TrainBonus)} health per level"),
        new(Upgrade.Haste, "Haste", "spd", $"+{Num.Pct(Balance.HasteBonus)} attack speed per level"),
        new(Upgrade.AutoOpen, "Auto-Open", "auto", "Opens chests for you; stops for the big ones"),
    };

    public static UpgradeDef Get(Upgrade u) => All[(int)u];

    public static int Level(GameState g, Upgrade u) => u switch
    {
        Upgrade.ChestLevel => g.ChestLevel,
        Upgrade.Might => g.Might,
        Upgrade.Vigor => g.Vigor,
        Upgrade.Haste => g.Haste,
        _ => g.HasAutoOpen ? 1 : 0,
    };

    public static double Cost(GameState g, Upgrade u) => Math.Round(u switch
    {
        Upgrade.ChestLevel => Balance.ChestLevelCost * Math.Pow(Balance.ChestLevelGrowth, g.ChestLevel - 1),
        Upgrade.Might => Balance.TrainCost * Math.Pow(Balance.TrainGrowth, g.Might),
        Upgrade.Vigor => Balance.TrainCost * Math.Pow(Balance.TrainGrowth, g.Vigor),
        Upgrade.Haste => Balance.HasteCost * Math.Pow(Balance.HasteGrowth, g.Haste),
        _ => Balance.AutoOpenCost,
    });

    /// <summary>Shown in the upgrade panel (auto-open appears once the knight reaches its zone).</summary>
    public static bool Visible(GameState g, Upgrade u) => u != Upgrade.AutoOpen || g.MaxStage >= Balance.AutoOpenStage;

    /// <summary>Still something to buy (not maxed, not already owned).</summary>
    public static bool Open(GameState g, Upgrade u) => Visible(g, u) && u switch
    {
        Upgrade.ChestLevel => g.ChestLevel < Balance.MaxChestLevel,
        Upgrade.Haste => g.Haste < Balance.MaxHaste,
        Upgrade.AutoOpen => !g.HasAutoOpen,
        _ => true,
    };
}
