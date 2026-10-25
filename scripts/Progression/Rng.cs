using System;

namespace LootFall.Progression;

/// <summary>A small seeded generator (xorshift64*) with the draws the simulator needs: uniform, Poisson, normal.</summary>
public sealed class Rng
{
    ulong _s;

    public Rng(int seed) => _s = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xBF58476D1CE4E5B9UL | 1UL;

    /// <summary>The generator's state, to save and restore (never zero).</summary>
    public ulong State
    {
        get => _s;
        set => _s = value == 0 ? 1UL : value;
    }

    ulong NextU()
    {
        _s ^= _s >> 12;
        _s ^= _s << 25;
        _s ^= _s >> 27;
        return _s * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double NextDouble() => (NextU() >> 11) * (1.0 / (1UL << 53));

    public int Next(int n) => (int)(NextDouble() * n);

    public bool Chance(double p) => NextDouble() < p;

    /// <summary>Standard normal (Box-Muller).</summary>
    public double Normal()
    {
        double u = 1 - NextDouble(), v = NextDouble();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
    }

    /// <summary>A Poisson count with mean <paramref name="mean"/> (exact for small means, normal beyond 40).</summary>
    public long Poisson(double mean)
    {
        if (mean <= 0) return 0;
        if (mean > 40) return Math.Max(0, (long)Math.Round(mean + Math.Sqrt(mean) * Normal()));
        double l = Math.Exp(-mean), p = 1;
        long k = 0;
        do
        {
            k++;
            p *= NextDouble();
        } while (p > l);
        return k - 1;
    }

    /// <summary>The largest of <paramref name="n"/> uniform draws, in one draw: U^(1/n).</summary>
    public double MaxOfUniform(double n) => n <= 1 ? NextDouble() : Math.Pow(NextDouble(), 1 / n);
}
