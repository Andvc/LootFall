using System;
using System.Linq;

namespace LootFall.Core;

/// <summary>Where a seller's chests come from: what's likely inside, and the colour of the chest.</summary>
public sealed class Origin
{
    public string Name { get; }

    /// <summary>Chest colour (a sprite set under res://assets/sprites/chest/).</summary>
    public string Skin { get; }

    readonly float[] _familyWeights;

    public Origin(string name, string skin, float coins, float metal, float bottles, float cloth, float paper, float jewels)
    {
        Name = name;
        Skin = skin;
        _familyWeights = new[] { coins, metal, bottles, cloth, paper, jewels };
    }

    public float Weight(Family family) => _familyWeights[(int)family];
}

public static class Origins
{
    public static readonly Origin Farm = new("Farm", "wood", coins: 2, metal: 3, bottles: 3, cloth: 3, paper: 0.5f, jewels: 0);
    public static readonly Origin Village = new("Village", "green", coins: 3, metal: 1, bottles: 2, cloth: 3, paper: 1, jewels: 0.5f);
    public static readonly Origin Barracks = new("Barracks", "wood", coins: 2, metal: 5, bottles: 1.5f, cloth: 1, paper: 1, jewels: 0.3f);
    public static readonly Origin Market = new("Market", "blue", coins: 3, metal: 2, bottles: 2, cloth: 2, paper: 2, jewels: 1.5f);
    public static readonly Origin Tower = new("Tower", "purple", coins: 1, metal: 1, bottles: 4, cloth: 2, paper: 4, jewels: 1.5f);
    public static readonly Origin Manor = new("Manor", "red", coins: 3, metal: 1, bottles: 1.5f, cloth: 3, paper: 2, jewels: 3);
    public static readonly Origin Sea = new("Sea", "blue", coins: 4, metal: 2, bottles: 2, cloth: 0.5f, paper: 1, jewels: 2);
}

/// <summary>A lock's maker sets how hard it is to pick. Index 0 is a plain crate with no lock at all.</summary>
public sealed record LockTier(int Index, string Name, string Difficulty, int Pins, float ZoneDegrees, float NeedleSpeed)
{
    public bool Locked => Pins > 0;
}

public static class LockTiers
{
    public static readonly LockTier[] All =
    {
        new(0, "No lock", "just lift the lid", 0, 0f, 0f),
        new(1, "Rusty Padlock", "easy to pick", 2, 56f, 120f),
        new(2, "Guild Lock", "fair to pick", 3, 44f, 145f),
        new(3, "Dwarven Tumbler", "hard to pick", 4, 34f, 175f),
        new(4, "Royal Seal", "very hard to pick", 5, 26f, 205f),
    };

    /// <summary>A lock fit for what's inside, give or take: owners guard good things better.</summary>
    public static LockTier For(Random rng, int sellerTier, int value)
    {
        if (sellerTier <= 1) return All[0];
        int index = Math.Clamp(1 + (int)(value / 150f + rng.Between(-0.8f, 0.8f)), 1, 4);
        return All[Math.Min(index, sellerTier)];
    }
}

public static class RandomExtensions
{
    public static float NextFloat(this Random rng) => (float)rng.NextDouble();
    public static float Between(this Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();
    public static T PickOne<T>(this Random rng, T[] items) => items[rng.Next(items.Length)];

    /// <summary>Picks by weight. Returns default if nothing has weight.</summary>
    public static T PickWeighted<T>(this Random rng, T[] items, Func<T, float> weight)
    {
        float total = items.Sum(weight);
        if (total <= 0) return default;
        float roll = rng.NextFloat() * total;
        foreach (var item in items)
        {
            roll -= weight(item);
            if (roll <= 0) return item;
        }
        return items[^1];
    }
}
