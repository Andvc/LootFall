using System;

namespace LootFall.Progression;

/// <summary>
/// The stat channels every source of power feeds. However many affixes, talents and layers the content has, they all
/// land in these few channels, and the channels combine in a fixed way:
/// <list type="bullet">
/// <item>Flat stats (damage, health, ratings) are absolute and kept as LE (log base R); they add up across slots.</item>
/// <item>Percent stats add up within their bucket, and each bucket multiplies once ("increased" in ARPG terms), so
/// stacking one bucket gives diminishing returns and no number of sources can run away.</item>
/// <item>"More" stats are kept as LE and add up as LE, i.e. multiply. Only a few controlled systems (keystones, paragon,
/// codex, prestige layers) may feed them, each with a budget the simulator checks.</item>
/// </list>
/// Ratings turn into chances against the depth's scale (cap / (1 + K R^(d-1) / rating)), so percentages neither
/// vanish nor pass their caps as numbers grow.
/// </summary>
public enum Stat : byte
{
    // Flat, as LE.
    FlatDamage, FlatHp, Armor, Dodge, Block, CritRating, MultiHit,
    // Percent buckets (the Inc* rating buckets multiply the gear's flat ratings, so talents never need flat stats).
    IncDamage, IncHp, CritDamage, AttackSpeed, Regen, Leech, Might, Fortitude, IncArmor, IncDodge, IncBlock, IncCrit, IncMultiHit,
    // Percent buckets that only touch income.
    GoldFind, ItemFind, XpGain, MaterialFind, Quantity,
    // More, as LE.
    MoreDamage, MoreHp,
}

public static class Stats
{
    public const int Count = (int)Stat.MoreHp + 1;

    public static bool IsFlat(Stat s) => s <= Stat.MultiHit;
    public static bool IsPercent(Stat s) => s >= Stat.IncDamage && s <= Stat.Quantity;
    public static bool IsMore(Stat s) => s >= Stat.MoreDamage;
    public static bool IsUtility(Stat s) => s >= Stat.GoldFind && s <= Stat.Quantity;

    static readonly string[] Names =
    {
        "flat damage", "flat health", "armor", "dodge", "block", "crit rating", "multi-hit",
        "increased damage", "increased health", "crit damage", "attack speed", "regeneration", "leech", "might", "fortitude",
        "increased armor", "increased dodge", "increased block", "increased crit", "increased multi-hit",
        "gold find", "item find", "xp gain", "material find", "quantity", "more damage", "more health",
    };

    public static string Name(Stat s) => Names[(int)s];
}

/// <summary>Level-equivalent arithmetic for one value of R.</summary>
public sealed class Scale
{
    /// <summary>The LE of "nothing" (a flat stat no source gives): far below anything real, and safe in R^x.</summary>
    public const double None = -1e6;

    public readonly double R, LnR;

    public Scale(double r)
    {
        R = r;
        LnR = Math.Log(r);
    }

    /// <summary>A multiplier as LE.</summary>
    public double Le(double multiplier) => Math.Log(multiplier) / LnR;

    /// <summary>R^le, as a double (for differences of LE, which stay small).</summary>
    public double Pow(double le) => Math.Exp(le * LnR);

    /// <summary>log_R(R^a + R^b): adding two absolute amounts given as LE.</summary>
    public double Add(double a, double b)
    {
        if (a < b) (a, b) = (b, a);
        if (b <= None) return a;
        return a + Math.Log(1 + Math.Exp((b - a) * LnR)) / LnR;
    }

    /// <summary>log_R(R^a - R^b), for a &gt; b (taking an amount back out); None if nothing is left.</summary>
    public double Sub(double a, double b)
    {
        if (b <= None) return a;
        double x = 1 - Math.Exp((b - a) * LnR);
        return x <= 1e-12 ? None : a + Math.Log(x) / LnR;
    }

    /// <summary>Chance from a rating (as LE) at depth d: cap / (1 + K R^(d-1) / rating).</summary>
    public double Chance(double cap, double k, double ratingLe, double depth)
    {
        double x = ((depth - 1) + Le(k) - ratingLe) * LnR;
        return x > 700 ? 0 : cap / (1 + Math.Exp(x));
    }
}

/// <summary>A set of stat totals: flat stats as LE, percent buckets as sums, more stats as LE sums.</summary>
public sealed class Sheet
{
    public readonly double[] V = new double[Stats.Count];

    public Sheet() => Clear();

    public void Clear()
    {
        for (int i = 0; i < V.Length; i++) V[i] = Stats.IsFlat((Stat)i) ? Scale.None : 0;
    }

    public double this[Stat s]
    {
        get => V[(int)s];
        set => V[(int)s] = value;
    }

    /// <summary>Adds an amount: flat ones given as LE (combined as absolute amounts), the rest as plain sums.</summary>
    public void Add(Scale sc, Stat s, double amount)
    {
        int i = (int)s;
        V[i] = Stats.IsFlat(s) ? sc.Add(V[i], amount) : V[i] + amount;
    }

    public void AddAll(Scale sc, Sheet other)
    {
        for (int i = 0; i < V.Length; i++)
            V[i] = Stats.IsFlat((Stat)i) ? sc.Add(V[i], other.V[i]) : V[i] + other.V[i];
    }

    public void CopyFrom(Sheet other) => Array.Copy(other.V, V, V.Length);
}
