using System;
using System.Collections.Generic;

namespace LootFall.Core;

/// <summary>A small seeded random source (xorshift64*), so a run and the simulation can be replayed from a seed.</summary>
public sealed class Rng
{
    ulong _state;

    public Rng(int seed)
    {
        _state = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL;
        if (_state == 0) _state = 1;
        for (int i = 0; i < 4; i++) Next();
    }

    ulong Next()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>0 (inclusive) to 1 (exclusive).</summary>
    public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

    /// <summary>min (inclusive) to max (exclusive).</summary>
    public int Range(int min, int max) => max <= min ? min : min + (int)(NextDouble() * (max - min));

    public double Range(double min, double max) => min + (max - min) * NextDouble();

    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

    /// <summary>An index chosen with probability proportional to its weight.</summary>
    public int Weighted(IReadOnlyList<double> weights)
    {
        double total = 0;
        foreach (var w in weights) total += Math.Max(0, w);
        double roll = NextDouble() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            roll -= Math.Max(0, weights[i]);
            if (roll < 0) return i;
        }
        return weights.Count - 1;
    }
}
