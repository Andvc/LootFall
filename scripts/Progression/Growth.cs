using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Progression;

/// <summary>
/// What the parameters alone say about the framework's long run, before any simulation: whether depth feeds back on
/// itself too strongly (the loop gain), whether prestige currency feeds back on itself too strongly (the prestige
/// feedback), and how fast depth will grow with time once every system has settled (the growth exponent). The checks
/// compare these predictions with what the simulator measures. See docs/progression.md, "Stability" and "Growth
/// exponents", for the derivations.
/// </summary>
public sealed class Growth
{
    /// <summary>LE per depth the hero has to find beyond what gear at the depth's level gives (mean of the health and
    /// attack premiums): the price of going deeper.</summary>
    public double Premium;

    /// <summary>LE per depth that depth gives back by itself: forge levels (gold grows with depth), talent points
    /// (levels grow with depth) while the tree lasts, paragon ranks after it.</summary>
    public double ForgeSlope, TreeSlope, ParagonSlope, LevelsPerDepth;

    /// <summary>Share of the premium that depth pays back by itself. At 1 or more, every depth pays for the next and
    /// depth runs away; below 1, depth settles where time-driven power puts it, amplified by 1 / (1 - LoopGain).</summary>
    public double LoopGain;

    /// <summary>Exponent of the feedback of aether on itself: deeper means more aether (AetherExp), more aether means
    /// more ascension power (1 / (AscCostExp + 1)), and stardust multiplies aether (Aether Flow). At 1 or more, aether
    /// grows exponentially in time; below 1, as a power of time.</summary>
    public double PrestigeFeedback;

    /// <summary>The predicted long-run exponent: depth ~ hours^Alpha.</summary>
    public double Alpha;

    /// <summary>Each open-ended system's own growth exponent in time, at Alpha.</summary>
    public readonly List<(string System, double Exponent)> Systems = new();

    public static Growth Analyze(Params p, TalentTree tree)
    {
        var g = new Growth();
        double lnR = Math.Log(p.R);
        g.Premium = 0.5 * (p.PremiumHpRate + p.PremiumAtkRate);
        g.ForgeSlope = p.GoldGrowth * Math.Log(1 + p.ForgeStep) / Math.Log(p.ForgeGrowth);
        g.LevelsPerDepth = p.XpGrowth * lnR / Math.Log(p.LevelGrowth);
        // The tree's "more" per node, as power (the mean of damage and health).
        double more = tree.Nodes.Sum(n => n.Stats.Select((s, i) => Stats.IsMore(s) ? n.Amounts[i] : 0).Sum());
        g.TreeSlope = g.LevelsPerDepth * 0.5 * more / tree.Nodes.Count;
        g.ParagonSlope = g.LevelsPerDepth * 0.5 * p.ParagonLE;
        g.LoopGain = (g.ForgeSlope + Math.Max(g.TreeSlope, g.ParagonSlope)) / g.Premium;

        double phi = p.AetherExp, beta = 1 / (p.AscCostExp + 1);
        // Aether Flow multiplies aether by its ranks, which grow like stardust^(1/(perk cost exp + 1)), and stardust grows
        // like the cycle's aether^StardustExp.
        double mu = p.StardustExp / (p.TransPerkCostExp + 1);
        g.PrestigeFeedback = phi * beta + mu;

        // Self-consistent exponent: depth ~ t^a makes aether ~ t^tau with tau = (1 + phi a) / (1 - mu); each system's power
        // then grows as below, and depth grows like the fastest of them.
        double Tau(double a) => (1 + phi * a) / (1 - mu);
        (string, double)[] At(double a)
        {
            double tau = Tau(a);
            return new[]
            {
                ("ascension", beta * tau),
                ("transcendence", p.StardustExp * tau / (p.TransCostExp + 1)),
                // Mastery XP is time times a gain the prestige trees multiply (Discipline ~ aether^beta).
                ("mastery", (1 + Math.Max(beta, mu) * tau) / (p.MasteryCurve + 1)),
                // Materials are counted per item, times a gain the ascension tree multiplies (Salvager).
                ("ranks", (1 + beta * tau) / (p.RankExp + 1)),
            };
        }
        double alpha = 0.5;
        for (int i = 0; i < 200; i++)
        {
            double next = At(alpha).Max(x => x.Item2);
            if (Math.Abs(next - alpha) < 1e-9) break;
            alpha = next;
            if (alpha > 10) break;
        }
        g.Alpha = g.PrestigeFeedback >= 1 ? double.PositiveInfinity : alpha;
        foreach (var (name, e) in At(Math.Min(alpha, 10))) g.Systems.Add((name, e));
        return g;
    }

    public override string ToString()
    {
        var parts = string.Join(", ", Systems.Select(s => $"{s.System} {s.Exponent:0.00}"));
        return $"premium {Premium:0.000} LE/depth; depth pays back forge {ForgeSlope:0.000} + talents {Math.Max(TreeSlope, ParagonSlope):0.000} " +
               $"(loop gain {LoopGain:0.00}, must be < 1); prestige feedback {PrestigeFeedback:0.00} (must be < 1); " +
               $"predicted depth ~ hours^{Alpha:0.00} ({parts})";
    }
}
