using System;

namespace LootFall.Progression;

/// <summary>
/// The fight in closed form: from a sheet of stats to damage per second, effective health and sustain at a depth, and
/// from those to time to kill, damage taken per wave, whether the depth's boss falls in time, how deep the hero can
/// push, where they farm and how fast they kill. Everything is in LE (log base R), so the absolute scale (1e800 at
/// depth 20,000) never appears and nothing overflows. The tick-by-tick battle in Core/Battle.cs is the same fight with
/// animations; this is what the simulator runs for thousands of hours.
/// </summary>
public sealed class Combat
{
    readonly Params P;
    readonly Scale S;
    readonly double _monsterHp, _monsterDps, _baseDamage, _baseHp, _bossHp, _guardianHp, _bossAtk, _guardianAtk;
    readonly double _waveExposure;

    public Combat(Params p)
    {
        P = p;
        S = new Scale(p.R);
        _monsterHp = S.Le(p.MonsterHp);
        _monsterDps = S.Le(p.MonsterAtk / p.MonsterInterval);
        _baseDamage = S.Le(p.BaseDamage);
        _baseHp = S.Le(p.BaseHp);
        _bossHp = S.Le(p.BossHp);
        _guardianHp = S.Le(p.GuardianHp);
        _bossAtk = S.Le(p.BossAtk);
        _guardianAtk = S.Le(p.GuardianAtk);
        // A wave of n fights together and dies one at a time: the first lives one kill time, the last n of them.
        _waveExposure = p.WaveSize * (p.WaveSize + 1) / 2;
    }

    public Scale Scale => S;

    // ---------------------------------------------------------------- the monsters

    public double PremiumHp(double d) => P.PremiumHpRate * (d - 1) + (P.PremiumHpCurve > 0 ? P.PremiumHpCurve * Math.Pow(Math.Max(0, d - 1), P.PremiumHpExp) : 0);
    public double PremiumAtk(double d) => P.PremiumAtkRate * (d - 1) + (P.PremiumAtkCurve > 0 ? P.PremiumAtkCurve * Math.Pow(Math.Max(0, d - 1), P.PremiumAtkExp) : 0);

    /// <summary>A monster's health at depth d, as LE.</summary>
    public double MonsterHpLe(double d) => _monsterHp + (d - 1) + PremiumHp(d);

    /// <summary>A monster's damage per second at depth d, as LE.</summary>
    public double MonsterDpsLe(double d) => _monsterDps + (d - 1) + PremiumAtk(d);

    public static bool IsGuardian(int depth) => depth % 10 == 0;

    // ---------------------------------------------------------------- the hero at a depth

    /// <summary>What the hero brings to a fight at one depth.</summary>
    public struct Frame
    {
        /// <summary>Damage per second and health, as LE.</summary>
        public double Dps, Hp;
        /// <summary>Share of a monster's damage that lands (after armor, dodge, block).</summary>
        public double Taken;
        /// <summary>Health recovered per second, as a share of health (regeneration and leech).</summary>
        public double Sustain;
        public double Crit, Dodge, Armor;
        /// <summary>Chances of a block and of a blow striking twice (for the screen; already in Taken and Dps).</summary>
        public double Block, Multi;
    }

    /// <summary>The depth-independent parts of offense and defense (flat totals and buckets), computed once per sheet.</summary>
    public struct Base
    {
        public double Damage, Hp, Speed, CritDamage;
    }

    public Base Prepare(Sheet s)
    {
        double speed = s[Stat.AttackSpeed];
        speed = P.BaseSpeed * (1 + P.SpeedCap * speed / (P.SpeedCap + speed));
        return new Base
        {
            Damage = S.Add(s[Stat.FlatDamage], _baseDamage) + S.Le(1 + s[Stat.IncDamage]) + S.Le(1 + s[Stat.Might]) + S.Le(speed) + s[Stat.MoreDamage],
            Hp = S.Add(s[Stat.FlatHp], _baseHp) + S.Le(1 + s[Stat.IncHp]) + S.Le(1 + s[Stat.Fortitude]) + s[Stat.MoreHp],
            Speed = speed,
            CritDamage = P.BaseCritDamage + s[Stat.CritDamage],
        };
    }

    public Frame At(Sheet s, in Base b, double d)
    {
        double crit = Math.Min(P.CritCap, P.BaseCrit + S.Chance(P.CritCap, P.CritK, s[Stat.CritRating] + S.Le(1 + s[Stat.IncCrit]), d));
        double multi = S.Chance(P.MultiHitCap, P.MultiHitK, s[Stat.MultiHit] + S.Le(1 + s[Stat.IncMultiHit]), d);
        double dps = b.Damage + S.Le(1 + crit * b.CritDamage) + S.Le(1 + multi);
        double armor = S.Chance(P.ArmorCap, P.ArmorK, s[Stat.Armor] + S.Le(1 + s[Stat.IncArmor]), d);
        double dodge = S.Chance(P.DodgeCap, P.DodgeK, s[Stat.Dodge] + S.Le(1 + s[Stat.IncDodge]), d);
        double block = S.Chance(P.BlockCap, P.BlockK, s[Stat.Block] + S.Le(1 + s[Stat.IncBlock]), d);
        double leech = Math.Min(P.LeechCap, s[Stat.Leech] * S.Pow(Math.Min(50, dps - b.Hp)));
        return new Frame
        {
            Dps = dps,
            Hp = b.Hp,
            Taken = (1 - armor) * (1 - dodge) * (1 - block * P.BlockAmount),
            Sustain = Math.Min(P.RegenCap, s[Stat.Regen]) + leech,
            Crit = crit,
            Dodge = dodge,
            Armor = armor,
            Block = block,
            Multi = multi,
        };
    }

    /// <summary>Seconds to kill one monster at depth d (not below the animation floor).</summary>
    public double KillTime(in Frame f, double d) => Math.Max(P.MinKillTime, S.Pow(Math.Min(60, MonsterHpLe(d) - f.Dps)));

    /// <summary>Share of health a wave at depth d takes, net of what's recovered during it (can be negative).</summary>
    public double WaveDamage(in Frame f, double d)
    {
        double ttk = KillTime(f, d);
        double hit = S.Pow(Math.Min(60, MonsterDpsLe(d) - f.Hp)) * f.Taken;
        return hit * ttk * _waveExposure - f.Sustain * ttk * P.WaveSize;
    }

    /// <summary>Seconds to kill depth d's boss (or guardian), and the share of health that fight takes.</summary>
    public (double Time, double Damage) Boss(in Frame f, double d, bool guardian)
    {
        double time = S.Pow(Math.Min(60, MonsterHpLe(d) + (guardian ? _guardianHp : _bossHp) - f.Dps));
        double hit = S.Pow(Math.Min(60, MonsterDpsLe(d) + (guardian ? _guardianAtk : _bossAtk) - f.Hp)) * f.Taken;
        double fight = Math.Min(time, P.BossTime);
        return (time, hit * fight - f.Sustain * fight);
    }

    /// <summary>Whether the hero beats depth d's boss (or guardian) and survives its waves.</summary>
    public bool CanClear(Sheet s, in Base b, int depth)
    {
        var f = At(s, b, depth);
        if (WaveDamage(f, depth) > P.PushWaveDamage) return false;
        var (time, damage) = Boss(f, depth, IsGuardian(depth));
        return time <= P.BossTime && damage <= P.PushWaveDamage;
    }

    /// <summary>
    /// How deep the hero can push, as a continuous depth: the deepest d whose (ordinary) boss falls in time and whose
    /// waves and boss leave them standing. Every margin falls as d grows, so a bisection finds it. Guardians (every tenth
    /// depth, stronger) are walls on top of this, checked by <see cref="CanClear"/>.
    /// </summary>
    public double Reach(Sheet s, in Base b)
    {
        if (!Fits(s, b, 1)) return 1;
        double lo = 1, hi = 2;
        while (Fits(s, b, hi))
        {
            lo = hi;
            hi *= 2;
            if (hi > 1e9) return hi;
        }
        for (int i = 0; i < 40 && hi - lo > 1e-3; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (Fits(s, b, mid)) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>The deepest depth (continuous) the hero farms comfortably: kills within the farm time, waves within the
    /// farm share of health. Offense counts here even when a push is limited by defense, as it does for a player.</summary>
    public double FarmReach(Sheet s, in Base b)
    {
        if (!Comfortable(s, b, 1)) return 1;
        double lo = 1, hi = 2;
        while (Comfortable(s, b, hi))
        {
            lo = hi;
            hi *= 2;
            if (hi > 1e9) return hi;
        }
        for (int i = 0; i < 40 && hi - lo > 1e-3; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (Comfortable(s, b, mid)) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    bool Fits(Sheet s, in Base b, double d)
    {
        var f = At(s, b, d);
        if (WaveDamage(f, d) > P.PushWaveDamage) return false;
        var (time, damage) = Boss(f, d, false);
        return time <= P.BossTime && damage <= P.PushWaveDamage;
    }

    /// <summary>The deepest depth up to <paramref name="max"/> the hero farms comfortably: kills within the farm time,
    /// waves cost at most the farm share of health. Depth 1 if nothing is comfortable.</summary>
    public int FarmDepth(Sheet s, in Base b, int max)
    {
        int lo = 1, hi = Math.Max(1, max);
        if (Comfortable(s, b, hi)) return hi;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (Comfortable(s, b, mid)) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    bool Comfortable(Sheet s, in Base b, double d)
    {
        var f = At(s, b, d);
        return KillTime(f, d) <= P.FarmKillTime && WaveDamage(f, d) <= P.FarmWaveDamage;
    }

    /// <summary>
    /// How many depths, starting at d, the hero clears at once by sheer overkill: while even a guardian would die faster
    /// than the animation floor, depths go by a wave and a boss at a time (like the zone skips of Clicker Heroes and Tap
    /// Titans), so climbing back after a reset takes minutes, not hours. 1 when there's no overkill.
    /// </summary>
    public int Skip(in Frame f, double d)
    {
        double guardianTime = MonsterHpLe(d) + _guardianHp - f.Dps;
        double spare = S.Le(P.MinKillTime) - guardianTime;
        if (spare <= 0) return 1;
        return 1 + (int)Math.Min(100000, spare / (1 + P.PremiumHpRate));
    }

    /// <summary>Seconds a skip takes: one wave at the animation floor, the walk, and the boss.</summary>
    public double SkipTime => P.WaveSize * P.MinKillTime + P.WaveGap + P.MinKillTime;

    /// <summary>Kills per second at depth d (waves, the walk between them, the animation floor).</summary>
    public double KillRate(in Frame f, double d)
    {
        double ttk = KillTime(f, d);
        return P.WaveSize / (P.WaveSize * ttk + P.WaveGap);
    }
}
