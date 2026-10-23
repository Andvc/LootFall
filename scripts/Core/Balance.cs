using System;

namespace LootFall.Core;

/// <summary>
/// Every number that sets the pace, in one place. Static fields rather than constants so the simulation can try others
/// (see docs/design.md for why each is what it is, and the sim's report for what they add up to).
/// </summary>
public static class Balance
{
    // The knight with nothing on.
    public static double HeroAtk = 6, HeroHp = 80, HeroSpeed = 0.9, HeroCrit = 0.05, HeroCritDmg = 1.5;

    /// <summary>Seconds from a swing starting to the blow landing (the animation is timed to it), and before a second strike.</summary>
    public static double HeroWindup = 0.15, DoubleDelay = 0.16, MonsterWindup = 0.3;

    /// <summary>Caps that keep a stat from breaking the game.</summary>
    public static double MaxSpeed = 3.5, MaxCrit = 0.75, MaxDodge = 0.5, MaxDouble = 0.5, MaxStun = 0.3, MaxDefCut = 0.8;

    /// <summary>Out of a fight (between waves) the knight gets his breath back: this share of max HP per second.</summary>
    public static double RestHeal = 0.15;

    // Gear: each item level multiplies main stats (and sale value) by this. Items come out at the chest level's item
    // level: two levels a chest level (so every chest level makes new loot about 28% stronger), one more for each
    // zone reached, and a little more from better kinds of chest.
    public static double GearGrowth = 1.13;
    public static int LevelsPerChestLevel = 2;
    public static int ItemLevel(int chestLevel, ChestKind kind, int maxStage) =>
        1 + LevelsPerChestLevel * (chestLevel - 1) + Chests.Get(kind).LevelBonus + (maxStage - 1) / StagesPerZone;

    /// <summary>A drop can come out a level or two above that: chances of +0, +1 and +2.</summary>
    public static double[] LevelSpread = { 0.6, 0.3, 0.1 };
    public static double GearScale(int level) => Math.Pow(GearGrowth, level - 1);
    public static double SellBase = 0.6;

    // Monsters: each stage multiplies their health and attack by this. A chest level's 40% carries the knight about
    // two and a half stages further; training and rarity carry the rest.
    public static double MonsterHp = 10, MonsterAtk = 2.5, MonsterGrowth = 1.18;
    public static double MonsterScale(int stage) => Math.Pow(MonsterGrowth, stage - 1);

    /// <summary>Stage bosses, zone bosses (every 10th stage) and the wandering mimic, as multiples of a stage's monster.</summary>
    public static double BossHp = 16, BossAtk = 1.6, ZoneBossHp = 22, ZoneBossAtk = 1.8, MimicHp = 6, MimicAtk = 1.2;

    /// <summary>Seconds the knight has to beat a boss.</summary>
    public static double BossTime = 30;

    public static int KillsPerStage = 10;
    public static int StagesPerZone = 10;

    /// <summary>
    /// After a lost boss fight, Auto-Boss tries again once the knight is this much stronger (and at least the first
    /// time has passed), or anyway after the second.
    /// </summary>
    public static double BossRetryPower = 1.04, BossRetryMin = 8, BossRetryMax = 90;

    /// <summary>A new wave walks in for this long (each foe after the first a little later); a wave's last foe falls, and the next comes after this.</summary>
    public static double ArriveTime = 0.6, ArriveStagger = 0.45, WaveGap = 0.35, ReviveTime = 2.0;

    /// <summary>Chance that a wave (from stage 3) is a wandering mimic.</summary>
    public static double MimicChance = 0.01;

    // Loot
    public static double ChestChance = 0.42;

    /// <summary>A new game starts with a few chests, and its first kills always drop one, so the loop starts at once.</summary>
    public static int StartChests = 2, SureDrops = 3;
    public static double IronShare = 0.06, GoldenShare = 0.008;
    public static double GoldGrowth = 1.089;
    public static double GoldPerKill(int stage) => 1.5 * Math.Pow(GoldGrowth, stage - 1);
    public static double BossGold = 8, ZoneBossGold = 20, MimicGold = 15;

    // Rarity: a chest level adds this much to the log-odds of each step up (see Chests.Odds).
    public static double[] RarityWeights = { 1000, 300, 60, 10, 1.2, 0.1 };
    public static double RarityPerLevel = 0.055;

    // Upgrades (see Upgrades.cs)
    public static double ChestLevelCost = 145, ChestLevelGrowth = 1.35;

    /// <summary>The first chest level is cheap, so a new player meets it in their first couple of minutes.</summary>
    public static double FirstChestLevelCost = 60;
    public static int MaxChestLevel = 45;
    public static double TrainCost = 10, TrainGrowth = 1.16, TrainBonus = 0.05;
    public static double HasteCost = 40, HasteGrowth = 1.35, HasteBonus = 0.02;
    public static int MaxHaste = 30;

    /// <summary>Auto-open: unlocked at this stage, for this much gold; one chest every so often.</summary>
    public static int AutoOpenStage = 10;
    public static double AutoOpenCost = 2500, AutoOpenEvery = 0.8;

    /// <summary>
    /// Time away is farmed at this share of what the knight does live, up to this long; at most this many chests wait
    /// for you (the rest are paid as the gold they'd sell for), so coming back is a treat rather than a chore.
    /// </summary>
    public static double OfflineRate = 0.75, OfflineCap = 8 * 3600;
    public static int OfflineChests = 100;
}
