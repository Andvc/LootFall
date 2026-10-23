using System;
using System.Globalization;

namespace LootFall.Core;

/// <summary>Short forms for big numbers: 950, 1.2K, 34.5M, 1.07B... ASCII only (the pixel font has nothing else).</summary>
public static class Num
{
    static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

    public static string Short(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "?";
        if (value < 0) return "-" + Short(-value);
        if (value < 1000) return value < 10 && value % 1 != 0 ? value.ToString("0.#", CultureInfo.InvariantCulture)
            : Math.Floor(value).ToString("0", CultureInfo.InvariantCulture);
        int tier = Math.Min(Suffixes.Length - 1, (int)(Math.Log10(value) / 3));
        double scaled = value / Math.Pow(1000, tier);
        // Three significant figures, never rounding up into the next tier ("1000K").
        string text = scaled >= 100 ? Math.Floor(scaled).ToString("0", CultureInfo.InvariantCulture)
            : scaled >= 10 ? (Math.Floor(scaled * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture)
            : (Math.Floor(scaled * 100) / 100).ToString("0.##", CultureInfo.InvariantCulture);
        return text + Suffixes[tier];
    }

    /// <summary>A share as a whole or one-decimal percentage: 0.05 is "5%", 0.125 is "12.5%".</summary>
    public static string Pct(double share)
    {
        double p = share * 100;
        return (Math.Abs(p - Math.Round(p)) < 0.05 ? Math.Round(p).ToString("0", CultureInfo.InvariantCulture)
            : p.ToString("0.0", CultureInfo.InvariantCulture)) + "%";
    }

    /// <summary>A duration as "45s", "12m 5s" or "3h 20m".</summary>
    public static string Time(double seconds)
    {
        if (seconds < 60) return $"{Math.Floor(seconds):0}s";
        if (seconds < 3600) return $"{Math.Floor(seconds / 60):0}m {Math.Floor(seconds % 60):0}s";
        return $"{Math.Floor(seconds / 3600):0}h {Math.Floor(seconds % 3600 / 60):0}m";
    }
}
