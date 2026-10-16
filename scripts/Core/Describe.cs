namespace LootFall.Core;

/// <summary>Player-facing text for the few facts the game states outright.</summary>
public static class Describe
{
    /// <summary>A lock's maker is a fact you can read off the plate, unless the plate is a forgery.</summary>
    public static string Lock(Chest chest)
    {
        if (chest.ShownLock != chest.Lock)
            return $"A {chest.ShownLock.Name}... no. Painted brass over a {chest.Lock.Name}. Someone faked this.";
        return $"{chest.Lock.Name}: {chest.Lock.Description} ({chest.Lock.Pins} pins)";
    }
}
