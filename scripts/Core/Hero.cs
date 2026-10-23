using System;
using System.Collections.Generic;

namespace LootFall.Core;

/// <summary>Levels bought with gold: Might (attack), Vigor (health), Haste (attack speed).</summary>
public readonly record struct Training(int Might, int Vigor, int Haste);

/// <summary>
/// What the knight fights with: base stats, plus gear, times training. Shares add up within a stat (two +5% ATK
/// affixes are +10%); training multiplies on top. Legendary effects add their magnitudes.
/// </summary>
public sealed class HeroStats
{
    public double Atk, Hp, Def, Speed, Crit, CritDmg, Lifesteal, Dodge, Regen, Double, Stun, GoldFind, ChestFind, Luck;
    readonly double[] _effects = new double[Effects.All.Length];

    public double Mag(Effect e) => _effects[(int)e];

    public bool Has(Effect e) => _effects[(int)e] > 0;

    public static HeroStats Compute(IEnumerable<Item> gear, Training training)
    {
        var s = new HeroStats();
        double atk = Balance.HeroAtk, hp = Balance.HeroHp, def = 0, atkPct = 0, hpPct = 0, speed = 0;
        double crit = Balance.HeroCrit, critDmg = Balance.HeroCritDmg;
        foreach (var item in gear)
        {
            if (item == null) continue;
            foreach (var a in item.All)
            {
                switch (a.Stat)
                {
                    case Stat.Atk: atk += a.Value; break;
                    case Stat.Hp: hp += a.Value; break;
                    case Stat.Def: def += a.Value; break;
                    case Stat.AtkPct: atkPct += a.Value; break;
                    case Stat.HpPct: hpPct += a.Value; break;
                    case Stat.Speed: speed += a.Value; break;
                    case Stat.Crit: crit += a.Value; break;
                    case Stat.CritDmg: critDmg += a.Value; break;
                    case Stat.Lifesteal: s.Lifesteal += a.Value; break;
                    case Stat.Dodge: s.Dodge += a.Value; break;
                    case Stat.Regen: s.Regen += a.Value; break;
                    case Stat.Double: s.Double += a.Value; break;
                    case Stat.Stun: s.Stun += a.Value; break;
                    case Stat.GoldFind: s.GoldFind += a.Value; break;
                    case Stat.ChestFind: s.ChestFind += a.Value; break;
                    case Stat.Luck: s.Luck += a.Value; break;
                }
            }
            if (item.Effect != Effect.None) s._effects[(int)item.Effect] += Effects.Magnitude(item.Rarity);
        }
        s.Lifesteal += Effects.VampireSteal * s.Mag(Effect.Vampire);
        s.GoldFind += Effects.MidasGold * s.Mag(Effect.Midas);
        s.ChestFind += Effects.ProspectorChests * s.Mag(Effect.Prospector);
        s.Luck += Effects.FortuneLuck * s.Mag(Effect.Fortune);
        speed += Effects.SwiftSpeed * s.Mag(Effect.Swiftness);

        s.Atk = atk * (1 + atkPct) * (1 + Balance.TrainBonus * training.Might);
        s.Hp = hp * (1 + hpPct) * (1 + Balance.TrainBonus * training.Vigor);
        s.Def = def;
        s.Speed = Math.Min(Balance.MaxSpeed, Balance.HeroSpeed * (1 + speed) * (1 + Balance.HasteBonus * training.Haste));
        s.Crit = Math.Min(Balance.MaxCrit, crit);
        s.CritDmg = critDmg;
        s.Dodge = Math.Min(Balance.MaxDodge, s.Dodge);
        s.Double = Math.Min(Balance.MaxDouble, s.Double);
        s.Stun = Math.Min(Balance.MaxStun, s.Stun);
        return s;
    }

    /// <summary>How much a hit of <paramref name="hit"/> is cut by defense: def / (def + 3 hits), up to 80%.</summary>
    public double DefCut(double hit) => Def <= 0 ? 0 : Math.Min(Balance.MaxDefCut, Def / (Def + 3 * Math.Max(1e-9, hit)));

    /// <summary>Damage per second against one foe, crits, double strikes and effects included.</summary>
    public double Offense
    {
        get
        {
            double effects = 0;
            foreach (var e in Effects.All) effects += e.Offense * Mag(e.Effect);
            return Atk * Speed * (1 + Crit * (CritDmg - 1)) * (1 + Double) * (1 + effects);
        }
    }

    /// <summary>Effective health against the monsters of a stage: defense, dodge, lifesteal, regeneration and effects.</summary>
    public double Defense(int stage)
    {
        double effects = 0;
        foreach (var e in Effects.All) effects += e.Defense * Mag(e.Effect);
        double hit = Balance.MonsterAtk * Balance.MonsterScale(stage);
        return Hp / (1 - DefCut(hit)) / (1 - Dodge) * (1 + 3 * Lifesteal + 10 * Regen) * (1 + effects);
    }

    /// <summary>One number for how strong the knight is at a stage: the geometric mean of offense and defense.</summary>
    public double Power(int stage) => Math.Sqrt(Offense * Defense(stage));
}
