using System;
using System.Linq;

namespace LootFall.Core;

/// <summary>Where a lot came from. Public information that shapes what is likely inside.</summary>
public sealed class Provenance
{
    public string Name { get; }
    public string Blurb { get; }

    /// <summary>Chest color for lots from here (a sprite set under res://assets/sprites/chest/).</summary>
    public string Skin { get; }

    public float WealthBias { get; }
    readonly float[] _familyWeights;

    public Provenance(string name, string blurb, string skin, float wealthBias,
        float coins, float glass, float steel, float wood, float paper)
    {
        Name = name;
        Blurb = blurb;
        Skin = skin;
        WealthBias = wealthBias;
        _familyWeights = new[] { coins, glass, steel, wood, paper };
    }

    public Family RollFamily(Random rng)
    {
        float total = 0;
        foreach (var w in _familyWeights) total += w;
        float roll = rng.NextFloat() * total;
        for (int i = 0; i < _familyWeights.Length; i++)
        {
            roll -= _familyWeights[i];
            if (roll <= 0) return (Family)i;
        }
        return (Family)(_familyWeights.Length - 1);
    }
}

public static class Provenances
{
    public static readonly Provenance[] All =
    {
        new("Shipwreck Salvage", "Dredged from the Gullmouth reef. Barnacles included free.", "blue", 0.15f,
            coins: 3, glass: 2, steel: 1, wood: 2, paper: 0.5f),
        new("Noble Estate", "Seized from a family whose debts outlived them.", "red", 0.3f,
            coins: 3, glass: 2, steel: 1, wood: 1, paper: 3),
        new("Soldier's Footlocker", "Unclaimed after the last campaign. Name scratched off.", "wood", -0.05f,
            coins: 2, glass: 1, steel: 4, wood: 1, paper: 2),
        new("Wizard's Tower", "From a tower that isn't there anymore. Handle with care.", "purple", 0.2f,
            coins: 1, glass: 4, steel: 0.5f, wood: 3, paper: 2),
        new("Farmhouse Cellar", "Found under a pile of turnips. Optimism advised.", "green", -0.3f,
            coins: 1, glass: 2, steel: 1, wood: 3, paper: 3),
    };

    public static Provenance Get(string name) => All.First(p => p.Name == name);
}

/// <summary>A lock's maker sets both how valuable the owner thought the contents were and how hard it is to pick.</summary>
public sealed record LockTier(int Index, string Name, string Description, int Pins, float ZoneDegrees, float NeedleSpeed);

public static class LockTiers
{
    public static readonly LockTier[] All =
    {
        new(0, "Rusty Padlock", "A farmer's padlock. Nobody guards treasure with this... right?", 2, 56f, 120f),
        new(1, "Guild Standard", "A merchant's honest tumbler lock, guild-stamped.", 3, 44f, 145f),
        new(2, "Dwarven Tumbler", "Dwarven make. Dwarves don't waste good locks on junk.", 4, 34f, 175f),
        new(3, "Royal Seal", "A royal seal. Someone very rich wanted this kept shut.", 5, 26f, 205f),
    };
}

public static class RandomExtensions
{
    public static float NextFloat(this Random rng) => (float)rng.NextDouble();
    public static float Between(this Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();
    public static T PickOne<T>(this Random rng, T[] items) => items[rng.Next(items.Length)];
}
