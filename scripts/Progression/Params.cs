namespace LootFall.Progression;

/// <summary>
/// Every tunable number of the progression framework, in one place, so the simulator and the tuner can vary them and
/// nothing hides in code. Grouped by system; units are in the comments. "LE" (level-equivalent) is the framework's unit
/// of relative power: a multiplier m is worth log(m)/log(R) LE, the number of depths of monster growth it makes up for.
/// See docs/progression.md for the reasoning behind each group.
/// </summary>
public sealed class Params
{
    // ---------------------------------------------------------------- absolute scale
    /// <summary>Everything absolute (monster health and attack, item stats, gold, XP) grows by R per depth.</summary>
    public double R = 1.10;

    // ---------------------------------------------------------------- monsters
    public double MonsterHp = 20, MonsterAtk = 3, MonsterInterval = 1.5;
    /// <summary>Monsters per wave (they fight together), seconds between waves, and the fastest a monster can die.</summary>
    public double WaveSize = 2, WaveGap = 1.0, MinKillTime = 0.25;
    public int KillsPerDepth = 10;
    /// <summary>A depth's boss (and every tenth depth's guardian): health and attack multipliers, and the time limit.</summary>
    public double BossHp = 8, BossAtk = 1.5, GuardianHp = 20, GuardianAtk = 2, BossTime = 30;

    /// <summary>
    /// The difficulty premium: how much harder (in LE) depth d is than the gear found there makes up for. Gear tracks
    /// the absolute scale by itself (items drop at the depth's level), so this premium is what every other system has
    /// to pay for: Premium(d) = Rate * (d - 1) + Curve * (d - 1)^Exp. Health premium slows kills, attack premium hurts.
    /// </summary>
    public double PremiumHpRate = 0.12, PremiumHpCurve = 0.0, PremiumHpExp = 1.2;
    public double PremiumAtkRate = 0.11, PremiumAtkCurve = 0.0, PremiumAtkExp = 1.2;

    // ---------------------------------------------------------------- the hero's base
    public double BaseDamage = 4, BaseHp = 60, BaseSpeed = 1, BaseCrit = 0.05, BaseCritDamage = 0.5;
    /// <summary>Attack speed bonus is soft-capped: effective = Cap * s / (Cap + s).</summary>
    public double SpeedCap = 3.0;

    // ---------------------------------------------------------------- ratings (diminishing returns against depth)
    // chance = Cap / (1 + K * R^(d-1) / rating): a rating keeps its worth as monsters scale, and never passes Cap.
    public double CritK = 3, CritCap = 0.75;
    public double MultiHitK = 6, MultiHitCap = 0.5;
    public double ArmorK = 2, ArmorCap = 0.85;
    public double DodgeK = 6, DodgeCap = 0.6;
    public double BlockK = 6, BlockCap = 0.5, BlockAmount = 0.5;
    /// <summary>Regeneration (share of health per second) and leech (share of damage dealt) caps.</summary>
    public double RegenCap = 0.05, LeechCap = 0.08;

    // ---------------------------------------------------------------- farming and pushing
    /// <summary>Farm where a monster dies within this many seconds and a wave takes at most this share of health.</summary>
    public double FarmKillTime = 1.5, FarmWaveDamage = 0.5;
    /// <summary>A push needs the boss dead in time and each wave to take at most this share of health.</summary>
    public double PushWaveDamage = 0.9;

    // ---------------------------------------------------------------- rewards
    /// <summary>Gold per kill = GoldBase * R^(GoldGrowth * (d-1)): with GoldGrowth below 1, gold falls behind the
    /// absolute scale a little each depth, which is what keeps gold-bought power from snowballing.</summary>
    public double GoldBase = 1, GoldGrowth = 0.9;
    public double XpBase = 1, XpGrowth = 0.8;
    public double DropChance = 0.35;
    /// <summary>A boss pays this many kills' worth (a guardian twice that).</summary>
    public double BossReward = 10;

    // ---------------------------------------------------------------- gear
    /// <summary>Flat stats a slot of weight 1 gives per unit of budget R^(IPL-1).</summary>
    public double DamagePerBudget = 1.0, HpPerBudget = 10, ArmorPerBudget = 1.0, RatingPerBudget = 1.0;
    /// <summary>LE per rarity tier and for a perfect quality roll (quality is uniform 0..1).</summary>
    public double RarityLE = 3.0, QualityLE = 2.0;
    /// <summary>Each tier is this many times rarer than the one below (before item find).</summary>
    public double RarityFalloff = 4.0;
    /// <summary>Item find tilts the odds: weight of tier r multiplied by (1 + item find)^(r * ItemFindTilt).</summary>
    public double ItemFindTilt = 0.25;
    /// <summary>Percent affixes: base size times 1 + TierGrowth * ln(1 + ilvl / TierScale) (they grow slowly with depth).</summary>
    public double AffixTierGrowth = 0.6, AffixTierScale = 20;
    /// <summary>Flat affixes (added damage, health, ratings) are this share of the slot's base budget each.</summary>
    public double FlatAffixShare = 0.35;
    /// <summary>Slot weights grow by this much per slot in unlock order (so late slots still add a share).</summary>
    public double SlotWeightGrowth = 1.03;

    // ---------------------------------------------------------------- forge (gold sink, per slot, reset by ascension)
    /// <summary>Level e of a slot costs ForgeBase * ForgeGrowth^e gold and multiplies the slot's flat stats by (1 + ForgeStep).
    /// Gold per kill grows by R^GoldGrowth a depth, so forge levels follow depth at GoldGrowth ln R / ln ForgeGrowth levels
    /// per depth: a "slope" of log_R(1 + ForgeStep) times that, in LE per depth, which must stay well under the premium.</summary>
    public double ForgeBase = 5, ForgeGrowth = 1.45, ForgeStep = 0.012;

    // ---------------------------------------------------------------- slot ranks (materials, persistent)
    /// <summary>Rank k of a slot costs RankCost * (k+1)^RankExp materials and adds RankLE to the slot's flat stats.
    /// Materials come from salvage, MaterialBase * MaterialRarity^rarity per item, counted rather than scaled with depth,
    /// so ranks grow with time played (a power law), not with depth.</summary>
    public double RankLE = 0.35, RankCost = 20, RankExp = 2.2, MaterialBase = 1, MaterialRarity = 1.25;

    // ---------------------------------------------------------------- levels and talents (persistent)
    /// <summary>XP from level L to L+1 = LevelXp * LevelGrowth^(L-1).</summary>
    public double LevelXp = 12, LevelGrowth = 1.25;
    /// <summary>Talent node sizes: every node's amounts times TalentScale, times 1 + TalentRingGrowth * ring outward.</summary>
    public double TalentScale = 1.0, TalentRingGrowth = 0.1;
    /// <summary>"More" (damage or health, by sector) per minor node and per notable, in LE at ring 0, growing with the ring.
    /// The whole tree is worth about 250 LE.</summary>
    public double TalentMinorLE = 0.02, TalentNotableLE = 0.2;
    /// <summary>Paragon: once ParagonAfter of the tree is allocated, each point can buy a rank instead, multiplying damage
    /// or health by this LE: the endless tail after the tree.</summary>
    public double ParagonLE = 0.03, ParagonAfter = 0.9;

    // ---------------------------------------------------------------- mastery (persistent, time-driven)
    /// <summary>Each equipped slot gains MasteryRate XP an hour (focused slots x MasteryFocus); level m needs
    /// MasteryXp * m^MasteryCurve more; each level multiplies the slot's flat stats by R^MasteryLE.</summary>
    public double MasteryRate = 30, MasteryFocus = 5, MasteryXp = 1, MasteryCurve = 1.5, MasteryLE = 1.0;
    public int MasteryFocusSlots = 4;

    // ---------------------------------------------------------------- codex (persistent collection)
    /// <summary>Each (slot, rarity, quality band, depth era) first found multiplies damage and health by R^CodexLE. Eras
    /// double: items below level 100, then 100-199, 200-399, ... so the collection keeps opening as the hero goes deeper.</summary>
    public double CodexLE = 0.004;

    // ---------------------------------------------------------------- ascension (prestige 1)
    /// <summary>Kills at or past AscendDepth earn AetherPerKill * (depth / AscendDepth)^AetherExp aether, banked until an
    /// ascension (at least AscendMinAether) pays it out.</summary>
    public double AscendDepth = 80, AetherPerKill = 0.01, AetherExp = 2.0, AscendMinAether = 10;
    /// <summary>Ascension tree: rank k of a node costs NodeCost * (k+1)^AscCostExp aether.</summary>
    public double AscCostExp = 3.0;

    // ---------------------------------------------------------------- transcendence (prestige 2)
    /// <summary>Transcendence is open after TransMinAscensions ascensions and TransAether total aether; it pays
    /// Stardust = StardustBase * (total aether / TransAether)^StardustExp.</summary>
    public int TransMinAscensions = 12;
    public double TransAether = 2e4, StardustBase = 5, StardustExp = 0.6, TransCostExp = 2.2, TransMinStardust = 5;

    // ---------------------------------------------------------------- the simulated player
    /// <summary>The policy values utility stats at this share of an equal offensive bonus.</summary>
    public double UtilityValue = 0.3;

    public Params Clone() => (Params)MemberwiseClone();
}
