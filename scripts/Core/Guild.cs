using System;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// The Grey Guild: moneylenders who lent Uncle Bram more than he could pay, and want it back. Their man, Mister Vane,
/// comes for the week's due at the end of every fifth day, and each week's due is bigger than the last. Pay all eight
/// and Bram's debt is cleared; the Guild keeps coming after that, for whatever more it can squeeze (endless). Fall
/// short and you can pawn what's on display and your curios; if that's still not enough, the Guild takes the shop.
/// </summary>
public static class Guild
{
    public const int DaysPerWeek = 5;

    /// <summary>The weeks of Bram's debt. After the last, the dues keep coming (endless).</summary>
    public const int Weeks = 8;

    /// <summary>Each week's due: small while you learn the stall, then climbing with what a growing shop can earn.</summary>
    static readonly int[] Dues = { 40, 100, 250, 700, 2000, 6000, 13000, 22000 };

    /// <summary>After the debt: each week half as much again, for as long as you last.</summary>
    const float EndlessGrowth = 1.5f;

    /// <summary>What the Guild wants at the end of this week (before any curio's discount).</summary>
    public static int Due(int week)
    {
        if (week <= Weeks) return Dues[Math.Max(0, week - 1)];
        double due = Dues[^1] * Math.Pow(EndlessGrowth, week - Weeks);
        return (int)(Math.Round(due / 50) * 50);
    }

    /// <summary>All of Bram's debt, as Vane tells it the first evening.</summary>
    public static int Debt => Dues.Sum();

    /// <summary>Pawned things fetch this much of what they'd sell for: the Guild drives a hard bargain.</summary>
    public const float PawnRate = 0.6f;

    public const string Collector = "Mister Vane";

    /// <summary>What Vane says when he comes for a week's due, week by week (the last line repeats).</summary>
    static readonly string[] Arrivals =
    {
        "Evening. The Guild's due, if you please.",
        "Week two. You're still here. Good: the Guild hates to waste a shop.",
        "Two hundred and fifty. Your uncle never made it past week three, you know.",
        "I hear you've been buying up chests. The Guild notices things like that.",
        "My masters are curious about you. That's rarely good news.",
        "Six thousand. The Guild is starting to take you seriously.",
        "One more week after this. Then the debt's paid. Unless you'd like to sell us the shop now?",
        "The last of it. Every coin, mind.",
    };

    static readonly string[] Endless =
    {
        "Debts are funny things. They grow when you're not looking.",
        "The Guild has decided you owe us for our patience.",
        "Interest, you understand. Nothing personal.",
    };

    public static string Arrival(int week) => week <= Weeks ? Arrivals[Math.Min(week, Arrivals.Length) - 1] : Endless[(week - Weeks - 1) % Endless.Length];

    public static string Paid(int week) => week switch
    {
        Weeks => "Paid in full. The Guild has no further claim on Bram's stall. For now.",
        > Weeks => "Paid. See you next week.",
        _ => "Paid. Until next week, then.",
    };

    public const string Short = "Short? Then the Guild will take what's on your shelves. What you've got, at our price.";

    public const string Foreclosed = "Not enough. The shop belongs to the Guild now. Your uncle would be so disappointed.";
}
