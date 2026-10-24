using System;
using System.Globalization;

namespace LootFall.Progression;

/// <summary>
/// A positive number of any size: a mantissa in [1, 10) and a 64-bit exponent (like break_infinity.js), so values past
/// double's 1.8e308 work: monster health at depth 20,000 is about 1e830. Zero has mantissa 0. Subtraction never goes
/// below zero (it's for spending). Precision is a double's (15-16 digits), plenty for a game's numbers.
/// </summary>
public readonly struct Big : IComparable<Big>, IEquatable<Big>
{
    public readonly double Mantissa;
    public readonly long Exponent;

    public static readonly Big Zero = new(0, 0);
    public static readonly Big One = new(1, 0);

    Big(double mantissa, long exponent)
    {
        Mantissa = mantissa;
        Exponent = exponent;
    }

    static Big Normalize(double m, long e)
    {
        if (m <= 0 || double.IsNaN(m)) return Zero;
        if (double.IsInfinity(m)) throw new OverflowException("Big from an infinite mantissa");
        if (m >= 1 && m < 10) return new Big(m, e);
        int shift = (int)Math.Floor(Math.Log10(m));
        m /= Math.Pow(10, shift);
        // Rounding can leave 10.0 or 0.999...: settle it.
        if (m >= 10) { m /= 10; shift++; }
        else if (m < 1) { m *= 10; shift--; }
        return new Big(m, e + shift);
    }

    public static Big Of(double value) => Normalize(value, 0);

    /// <summary>10 to the power <paramref name="log10"/>.</summary>
    public static Big Pow10(double log10)
    {
        if (double.IsNegativeInfinity(log10)) return Zero;
        if (double.IsNaN(log10) || double.IsPositiveInfinity(log10)) throw new OverflowException($"Big.Pow10({log10})");
        double floor = Math.Floor(log10);
        return Normalize(Math.Pow(10, log10 - floor), (long)floor);
    }

    /// <summary><paramref name="b"/> to the power <paramref name="p"/>, both as doubles (b &gt; 0).</summary>
    public static Big Pow(double b, double p) => Pow10(Math.Log10(b) * p);

    public bool IsZero => Mantissa == 0;

    /// <summary>log10 of the value (negative infinity for zero).</summary>
    public double Log10 => Mantissa == 0 ? double.NegativeInfinity : Exponent + Math.Log10(Mantissa);

    /// <summary>The natural log (negative infinity for zero).</summary>
    public double Ln => Log10 * Math.Log(10);

    /// <summary>The value as a double (infinity past 1.8e308, 0 below 1e-308).</summary>
    public double ToDouble() => Mantissa == 0 ? 0 : Exponent > 308 ? double.PositiveInfinity : Exponent < -324 ? 0 : Mantissa * Math.Pow(10, Exponent);

    public static Big operator *(Big a, Big b) => a.IsZero || b.IsZero ? Zero : Normalize(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
    public static Big operator *(Big a, double b) => a.IsZero || b <= 0 ? Zero : Normalize(a.Mantissa * b, a.Exponent);
    public static Big operator /(Big a, Big b)
    {
        if (b.IsZero) throw new DivideByZeroException();
        return a.IsZero ? Zero : Normalize(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
    }
    public static Big operator /(Big a, double b) => a * (1 / b);

    public static Big operator +(Big a, Big b)
    {
        if (a.IsZero) return b;
        if (b.IsZero) return a;
        if (a.Exponent < b.Exponent) (a, b) = (b, a);
        long diff = a.Exponent - b.Exponent;
        if (diff > 17) return a;
        return Normalize(a.Mantissa + b.Mantissa * Math.Pow(10, -diff), a.Exponent);
    }

    /// <summary>a - b, never below zero.</summary>
    public static Big operator -(Big a, Big b)
    {
        if (b.IsZero) return a;
        if (a <= b) return Zero;
        long diff = a.Exponent - b.Exponent;
        if (diff > 17) return a;
        return Normalize(a.Mantissa - b.Mantissa * Math.Pow(10, -diff), a.Exponent);
    }

    public int CompareTo(Big other)
    {
        if (IsZero || other.IsZero) return IsZero ? (other.IsZero ? 0 : -1) : 1;
        if (Exponent != other.Exponent) return Exponent < other.Exponent ? -1 : 1;
        return Mantissa.CompareTo(other.Mantissa);
    }

    public static bool operator <(Big a, Big b) => a.CompareTo(b) < 0;
    public static bool operator >(Big a, Big b) => a.CompareTo(b) > 0;
    public static bool operator <=(Big a, Big b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Big a, Big b) => a.CompareTo(b) >= 0;
    public bool Equals(Big other) => Mantissa == other.Mantissa && Exponent == other.Exponent;
    public override bool Equals(object obj) => obj is Big b && Equals(b);
    public override int GetHashCode() => HashCode.Combine(Mantissa, Exponent);
    public static Big Max(Big a, Big b) => a >= b ? a : b;
    public static Big Min(Big a, Big b) => a <= b ? a : b;

    static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

    /// <summary>"950", "12.3K", "4.56Dc", then "1.23e456".</summary>
    public override string ToString()
    {
        if (IsZero) return "0";
        if (Exponent < 0) return ToDouble().ToString("0.###", CultureInfo.InvariantCulture);
        if (Exponent < 3) return (Mantissa * Math.Pow(10, Exponent)).ToString(Exponent == 0 ? "0.##" : "0", CultureInfo.InvariantCulture);
        long group = Exponent / 3;
        if (group < Suffixes.Length)
        {
            double shown = Mantissa * Math.Pow(10, Exponent % 3);
            return shown.ToString(shown < 10 ? "0.00" : shown < 100 ? "0.0" : "0", CultureInfo.InvariantCulture) + Suffixes[group];
        }
        return Mantissa.ToString("0.00", CultureInfo.InvariantCulture) + "e" + Exponent.ToString(CultureInfo.InvariantCulture);
    }
}
