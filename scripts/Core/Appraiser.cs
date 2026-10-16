using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public readonly record struct Estimate(int Low, int Median, int High, int Mean, int Samples);

/// <summary>
/// What sellers (and the balance simulation) believe a chest from somewhere is worth, from many simulated chests of
/// the same provenance. Never shown to the player: they judge chests from what they hear, weigh and see.
/// </summary>
public sealed class Appraiser
{
    const int PoolSize = 12000;
    const int AbsoluteMinSamples = 8;

    static readonly Dictionary<Provenance, Appraiser> Cache = new();

    readonly Chest[] _pool;

    /// <summary>One shared appraiser per provenance; building the sample pool is the expensive part.</summary>
    public static Appraiser For(Provenance provenance)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(provenance, out var appraiser))
                Cache[provenance] = appraiser = new Appraiser(provenance, 7 + Array.IndexOf(Provenances.All, provenance));
            return appraiser;
        }
    }

    public Estimate Prior { get; }

    public Appraiser(Provenance provenance, int seed)
    {
        var rng = new Random(seed);
        _pool = new Chest[PoolSize];
        for (int i = 0; i < PoolSize; i++)
            _pool[i] = ChestGenerator.Generate(rng, provenance);

        Prior = Summarize(_pool);
    }

    /// <summary>What an old hand who only reads the lock plate would expect.</summary>
    public Estimate ByLock(LockTier lockTier) => Summarize(KeepIfEnough(_pool.ToList(), c => c.Lock.Index == lockTier.Index));

    static List<Chest> KeepIfEnough(List<Chest> source, Func<Chest, bool> predicate)
    {
        var kept = source.Where(predicate).ToList();
        return kept.Count >= AbsoluteMinSamples ? kept : source;
    }

    static Estimate Summarize(IEnumerable<Chest> chests)
    {
        var values = chests.Select(c => c.Value).OrderBy(v => v).ToArray();
        int mean = values.Length == 0 ? 0 : (int)values.Average();
        return new Estimate(Percentile(values, 0.1f), Percentile(values, 0.5f), Percentile(values, 0.9f), mean, values.Length);
    }

    public static int Percentile(int[] sorted, float p)
    {
        if (sorted.Length == 0) return 0;
        int index = Math.Clamp((int)MathF.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[index];
    }
}
