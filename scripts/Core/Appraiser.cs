using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public sealed class Clues
{
    public bool Shaken;
    public bool Weighed;
    public bool LockExamined;

    public Clues() { }
    public Clues(bool shaken, bool weighed, bool lockExamined)
    {
        Shaken = shaken;
        Weighed = weighed;
        LockExamined = lockExamined;
    }
}

public readonly record struct Estimate(int Low, int Median, int High, int Samples);

/// <summary>
/// Estimates a chest's value by simulating many chests from the same provenance and keeping the ones
/// that match every clue the player has uncovered. The range is the 10th to 90th percentile of what's left,
/// so it is honest: more (or better) clues give a tighter range.
/// </summary>
public sealed class Appraiser
{
    const int PoolSize = 12000;
    const int MinSamples = 40;
    const int AbsoluteMinSamples = 8;

    readonly Chest[] _pool;

    public Estimate Prior { get; }

    /// <summary>A fixed upper bound for drawing estimate bars for this provenance.</summary>
    public int ScaleMax { get; }

    public Appraiser(Provenance provenance, int seed)
    {
        var rng = new Random(seed);
        _pool = new Chest[PoolSize];
        for (int i = 0; i < PoolSize; i++)
            _pool[i] = ChestGenerator.Generate(rng, provenance);

        Prior = Summarize(_pool);
        var sorted = _pool.Select(c => c.Value).OrderBy(v => v).ToArray();
        ScaleMax = NiceCeiling(Percentile(sorted, 0.95f) * 1.1f);
    }

    public Estimate Estimate(Chest actual, Clues clues)
    {
        List<Chest> matches = _pool.ToList();

        // Apply the sharpest filters first; skip any filter that would leave too few samples to be meaningful.
        if (clues.Shaken)
            matches = KeepIfEnough(matches, c => c.DominantSound == actual.DominantSound
                                                 && c.HasFragile == actual.HasFragile
                                                 && c.ManyItems == actual.ManyItems
                                                 && c.SoundsFine == actual.SoundsFine);
        if (clues.LockExamined)
            matches = KeepIfEnough(matches, c => c.Lock.Index == actual.Lock.Index);
        if (clues.Weighed)
        {
            for (float tolerance = 0.4f; tolerance < 40f; tolerance *= 1.5f)
            {
                var near = matches.Where(c => MathF.Abs(c.Weight - actual.Weight) <= tolerance).ToList();
                if (near.Count >= MinSamples)
                {
                    matches = near;
                    break;
                }
            }
        }

        return Summarize(matches);
    }

    static List<Chest> KeepIfEnough(List<Chest> source, Func<Chest, bool> predicate)
    {
        var kept = source.Where(predicate).ToList();
        return kept.Count >= AbsoluteMinSamples ? kept : source;
    }

    static Estimate Summarize(IEnumerable<Chest> chests)
    {
        var values = chests.Select(c => c.Value).OrderBy(v => v).ToArray();
        return new Estimate(Percentile(values, 0.1f), Percentile(values, 0.5f), Percentile(values, 0.9f), values.Length);
    }

    public static int Percentile(int[] sorted, float p)
    {
        if (sorted.Length == 0) return 0;
        int index = Math.Clamp((int)MathF.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[index];
    }

    static int NiceCeiling(float value)
    {
        float step = value > 1000 ? 250 : value > 400 ? 100 : 50;
        return (int)(MathF.Ceiling(value / step) * step);
    }
}
