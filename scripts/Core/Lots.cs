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
    readonly float[] _soundWeights;

    public Provenance(string name, string blurb, string skin, float wealthBias,
        float jingle, float clink, float clank, float thud, float rustle)
    {
        Name = name;
        Blurb = blurb;
        Skin = skin;
        WealthBias = wealthBias;
        _soundWeights = new[] { jingle, clink, clank, thud, rustle };
    }

    public Sound RollSound(Random rng)
    {
        float total = 0;
        foreach (var w in _soundWeights) total += w;
        float roll = rng.NextFloat() * total;
        for (int i = 0; i < _soundWeights.Length; i++)
        {
            roll -= _soundWeights[i];
            if (roll <= 0) return (Sound)i;
        }
        return (Sound)(_soundWeights.Length - 1);
    }
}

public static class Provenances
{
    public static readonly Provenance[] All =
    {
        new("Shipwreck Salvage", "Dredged from the Gullmouth reef. Barnacles included free.", "blue", 0.15f,
            jingle: 3, clink: 2, clank: 1, thud: 2, rustle: 0.5f),
        new("Noble Estate", "Seized from a family whose debts outlived them.", "red", 0.3f,
            jingle: 3, clink: 2, clank: 1, thud: 1, rustle: 3),
        new("Soldier's Footlocker", "Unclaimed after the last campaign. Name scratched off.", "wood", -0.05f,
            jingle: 2, clink: 1, clank: 4, thud: 1, rustle: 2),
        new("Wizard's Tower", "From a tower that isn't there anymore. Handle with care.", "purple", 0.2f,
            jingle: 1, clink: 4, clank: 0.5f, thud: 3, rustle: 2),
        new("Farmhouse Cellar", "Found under a pile of turnips. Optimism advised.", "green", -0.3f,
            jingle: 1, clink: 2, clank: 1, thud: 3, rustle: 3),
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
