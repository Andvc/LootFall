using System;
using System.Globalization;
using LootFall.Progression;

namespace LootFall.Game;

public enum Notation : byte
{
    /// <summary>Named suffixes up to decillion (K, M, B ... Dc), then scientific: the default.</summary>
    Mixed,
    /// <summary>Named suffixes, then two-letter ones (aa, ab ... zz) up to about 1e2000, then scientific.</summary>
    Letters,
    Scientific,
    /// <summary>Exponents in steps of three (123.4e45).</summary>
    Engineering,
}

/// <summary>
/// How numbers read on screen. Monster health passes 1e800 within the first ten thousand hours, so every number goes
/// through here, in the player's notation, at a fixed short width: three significant digits and a suffix or exponent.
/// </summary>
public static class Format
{
    public static Notation Notation = Notation.Mixed;

    static readonly string[] Named = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Num(Big b)
    {
        if (b.IsZero) return "0";
        double log = b.Log10;
        if (log < 3) return Small(b.ToDouble());
        long e3 = (long)Math.Floor(log / 3);
        switch (Notation)
        {
            case Notation.Scientific:
                return Sci(b);
            case Notation.Engineering:
            {
                long e = e3 * 3;
                return $"{Digits(Math.Pow(10, log - e))}e{e}";
            }
            case Notation.Letters when e3 < Named.Length + 26 * 26:
            case Notation.Mixed when e3 < Named.Length:
            {
                string suffix = e3 < Named.Length ? Named[e3] : Letters(e3 - Named.Length);
                return Digits(Math.Pow(10, log - e3 * 3)) + suffix;
            }
            default:
                return Sci(b);
        }
    }

    public static string Num(double v)
    {
        if (double.IsNaN(v)) return "-";
        if (Math.Abs(v) < 1000) return Small(v);
        if (double.IsInfinity(v)) return v > 0 ? "inf" : "-inf";
        return (v < 0 ? "-" : "") + Num(Big.Of(Math.Abs(v)));
    }

    public static string Int(long v) => v < 1000 ? v.ToString(Inv) : Num((double)v);

    /// <summary>A whole number with thousands separators (depths, levels, counts).</summary>
    public static string Whole(long v) => v.ToString("#,0", Inv);

    static string Small(double v)
    {
        double a = Math.Abs(v);
        if (a >= 100 || a == Math.Floor(a)) return Math.Round(v).ToString("0", Inv);
        if (a >= 10) return v.ToString("0.#", Inv);
        return v.ToString("0.##", Inv);
    }

    static string Digits(double m) => m >= 100 ? m.ToString("0", Inv) : m >= 10 ? m.ToString("0.#", Inv) : m.ToString("0.##", Inv);

    static string Sci(Big b)
    {
        double log = b.Log10;
        long e = (long)Math.Floor(log);
        double m = Math.Pow(10, log - e);
        if (m >= 9.995) { m = 1; e++; }
        return $"{m.ToString("0.00", Inv)}e{e}";
    }

    static string Letters(long i)
    {
        char a = (char)('a' + i / 26), b = (char)('a' + i % 26);
        return $"{a}{b}";
    }

    /// <summary>A share as a percent: "+12.5%".</summary>
    public static string Percent(double share, bool sign = false)
    {
        double p = share * 100;
        string s = Math.Abs(p) >= 100 ? Num(p) : Math.Abs(p) >= 10 ? p.ToString("0.#", Inv) : p.ToString("0.##", Inv);
        return (sign && p >= 0 ? "+" : "") + s + "%";
    }

    /// <summary>A duration: "45s", "12m", "3h 20m", "4d 6h".</summary>
    public static string Time(double seconds)
    {
        if (seconds < 60) return $"{Math.Max(0, Math.Floor(seconds)):0}s";
        if (seconds < 3600) return $"{Math.Floor(seconds / 60):0}m";
        if (seconds < 86400)
        {
            int h = (int)(seconds / 3600), m = (int)(seconds % 3600 / 60);
            return m > 0 ? $"{h}h {m}m" : $"{h}h";
        }
        int d = (int)(seconds / 86400), hh = (int)(seconds % 86400 / 3600);
        return hh > 0 ? $"{d}d {hh}h" : $"{d}d";
    }
}
