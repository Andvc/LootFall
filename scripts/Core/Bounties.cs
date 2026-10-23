using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum BountyKind { Slay, Open, Bosses, Finds, Upgrades, Gold, Stage }

/// <summary>
/// A short goal on the bounty board: progress is counted from where the counter stood when it was posted (or, for a
/// stage, is the furthest stage), and finishing it pays chests.
/// </summary>
public sealed class Bounty
{
    public int Number { get; init; }
    public BountyKind Kind { get; init; }
    public double Target { get; init; }
    public double Start { get; init; }
    public ChestKind Reward { get; init; }
    public int Count { get; init; }

    public double Progress(GameState g) => Math.Min(Target, Kind switch
    {
        BountyKind.Slay => g.TotalKills - Start,
        BountyKind.Open => g.ChestsOpened - Start,
        BountyKind.Bosses => g.BossesBeaten - Start,
        BountyKind.Finds => g.RareFinds - Start,
        BountyKind.Upgrades => g.UpgradesEquipped - Start,
        BountyKind.Gold => g.GoldEarned - Start,
        _ => g.MaxStage,
    });

    public bool Done(GameState g) => Progress(g) >= Target;

    public string Text => Kind switch
    {
        BountyKind.Slay => $"Slay {Target:0} monsters",
        BountyKind.Open => $"Open {Target:0} chests",
        BountyKind.Bosses => Target == 1 ? "Beat a boss" : $"Beat {Target:0} bosses",
        BountyKind.Finds => $"Find {Target:0} rare or better",
        BountyKind.Upgrades => $"Equip {Target:0} upgrades",
        BountyKind.Gold => $"Earn {Num.Short(Target)} gold",
        _ => $"Reach {World.StageName((int)Target)}",
    };

    /// <summary>"12/30", or for gold "1.2K/3K".</summary>
    public string Tally(GameState g) => Kind == BountyKind.Stage
        ? $"{World.StageInZone(Math.Min(g.MaxStage, (int)Target))}/{World.StageInZone((int)Target)}"
        : $"{Num.Short(Math.Floor(Progress(g)))}/{Num.Short(Target)}";

    public string Icon => Kind switch
    {
        BountyKind.Slay => "atk",
        BountyKind.Open => "chest",
        BountyKind.Bosses => "boss",
        BountyKind.Finds => "crit",
        BountyKind.Upgrades => "power",
        BountyKind.Gold => "gold",
        _ => "spd",
    };
}

/// <summary>
/// The bounty board: three goals at a time, each paying chests (mostly iron, every fifth a golden one, every fifteenth
/// a royal one), replaced by a slightly bigger one when done. Kinds rotate so the board always asks for different
/// things; targets grow with how many have been done and, for gold, with the stage.
/// </summary>
public static class Bounties
{
    public const int Active = 3;

    static readonly BountyKind[] Order =
    {
        BountyKind.Slay, BountyKind.Open, BountyKind.Bosses, BountyKind.Finds, BountyKind.Upgrades, BountyKind.Gold, BountyKind.Stage,
    };

    /// <summary>The <paramref name="number"/>th bounty of the game (from 0), avoiding kinds already on the board.</summary>
    public static Bounty Make(GameState g, int number, IEnumerable<Bounty> board)
    {
        var taken = board.Select(b => b.Kind).ToHashSet();
        var kind = Order[number % Order.Length];
        for (int i = 0; i < Order.Length && taken.Contains(kind); i++) kind = Order[(number + i + 1) % Order.Length];
        double n = number;
        (double target, double start) = kind switch
        {
            BountyKind.Slay => (20 + 6 * n, g.TotalKills),
            BountyKind.Open => (10 + 3 * n, g.ChestsOpened),
            BountyKind.Bosses => (2 + Math.Floor(n / 6), g.BossesBeaten),
            BountyKind.Finds => (2 + Math.Floor(n / 8), g.RareFinds),
            BountyKind.Upgrades => (3 + Math.Floor(n / 8), g.UpgradesEquipped),
            BountyKind.Gold => (Math.Round(50 * Balance.GoldPerKill(g.MaxStage) * (1 + n / 12)), g.GoldEarned),
            _ => (g.MaxStage + 2, 0),
        };
        var (reward, count) = number % 15 == 14 ? (ChestKind.Royal, 1) : number % 5 == 4 ? (ChestKind.Golden, 1) : (ChestKind.Iron, 1);
        return new Bounty { Number = number, Kind = kind, Target = target, Start = start, Reward = reward, Count = count };
    }
}
