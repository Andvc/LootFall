namespace LootFall.Core;

/// <summary>Player-facing text for the few facts the game states outright.</summary>
public static class Describe
{
    public static string Pounds(float weight) => $"{weight:0.#} lb";
}
