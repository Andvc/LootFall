using System;
using System.Linq;
using System.Text.Json;

namespace LootFall.Core;

/// <summary>A piece of gear as saved.</summary>
public sealed class ItemData
{
    public int Slot { get; set; }
    public int Rarity { get; set; }
    public int Level { get; set; }
    public string Name { get; set; } = "";
    public double[][] Main { get; set; } = Array.Empty<double[]>();
    public double[][] Affixes { get; set; } = Array.Empty<double[]>();
    public int Effect { get; set; }

    public static ItemData From(Item item) => item == null ? null : new ItemData
    {
        Slot = (int)item.Slot, Rarity = (int)item.Rarity, Level = item.Level, Name = item.Name, Effect = (int)item.Effect,
        Main = item.Main.Select(a => new[] { (double)(int)a.Stat, a.Value }).ToArray(),
        Affixes = item.Affixes.Select(a => new[] { (double)(int)a.Stat, a.Value }).ToArray(),
    };

    public Item ToItem() => new()
    {
        Slot = (Slot)Slot, Rarity = (Rarity)Rarity, Level = Level, Name = Name, Effect = (Effect)Effect,
        Main = Main.Select(a => new Affix((Stat)(int)a[0], a[1])).ToArray(),
        Affixes = Affixes.Select(a => new Affix((Stat)(int)a[0], a[1])).ToArray(),
    };
}

/// <summary>Everything that survives closing the game, as JSON (user://save.json).</summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public int Seed { get; set; }
    public int Stage { get; set; } = 1;
    public int MaxStage { get; set; } = 1;
    public int Kills { get; set; }
    public double Gold { get; set; }
    public int[] Chests { get; set; } = new int[Core.Chests.Count];
    public ItemData[] Equipped { get; set; } = new ItemData[Slots.Count];
    public int ChestLevel { get; set; } = 1;
    public int Might { get; set; }
    public int Vigor { get; set; }
    public int Haste { get; set; }
    public bool HasAutoOpen { get; set; }
    public bool AutoBoss { get; set; } = true;
    public bool AutoOpen { get; set; } = true;
    public int AutoStopAt { get; set; } = (int)Rarity.Epic;
    public double PlayTime { get; set; }
    public long TotalKills { get; set; }
    public long ChestsOpened { get; set; }
    public long BossesBeaten { get; set; }
    public long[] Found { get; set; } = new long[Rarities.Count];
    public double GoldEarned { get; set; }
    public ItemData Pending { get; set; }
    public int PendingKind { get; set; }

    /// <summary>When it was saved (Unix seconds), so the time away can be paid out.</summary>
    public double SavedAt { get; set; }

    public static SaveData From(GameState g) => new()
    {
        Seed = g.Seed, Stage = g.Stage, MaxStage = g.MaxStage, Kills = g.Kills, Gold = g.Gold,
        Chests = (int[])g.Chests.Clone(), Equipped = g.Equipped.Select(ItemData.From).ToArray(),
        ChestLevel = g.ChestLevel, Might = g.Might, Vigor = g.Vigor, Haste = g.Haste, HasAutoOpen = g.HasAutoOpen,
        AutoBoss = g.AutoBoss, AutoOpen = g.AutoOpen, AutoStopAt = (int)g.AutoStopAt,
        PlayTime = g.PlayTime, TotalKills = g.TotalKills, ChestsOpened = g.ChestsOpened, BossesBeaten = g.BossesBeaten,
        Found = (long[])g.Found.Clone(), GoldEarned = g.GoldEarned,
        Pending = ItemData.From(g.Pending?.Item), PendingKind = (int)(g.Pending?.Kind ?? ChestKind.Wooden),
    };

    public void ApplyTo(GameState g)
    {
        g.Stage = Math.Max(1, Math.Min(Stage, MaxStage));
        g.MaxStage = Math.Max(1, MaxStage);
        g.Kills = Kills;
        g.Gold = Gold;
        for (int i = 0; i < g.Chests.Length && i < Chests.Length; i++) g.Chests[i] = Math.Max(0, Chests[i]);
        for (int i = 0; i < g.Equipped.Length && i < Equipped.Length; i++) g.Equipped[i] = Equipped[i]?.ToItem();
        g.ChestLevel = Math.Max(1, ChestLevel);
        g.Might = Might;
        g.Vigor = Vigor;
        g.Haste = Haste;
        g.HasAutoOpen = HasAutoOpen;
        g.AutoBoss = AutoBoss;
        g.AutoOpen = AutoOpen;
        g.AutoStopAt = (Rarity)Math.Clamp(AutoStopAt, 0, Rarities.Count - 1);
        g.PlayTime = PlayTime;
        g.TotalKills = TotalKills;
        g.ChestsOpened = ChestsOpened;
        g.BossesBeaten = BossesBeaten;
        for (int i = 0; i < g.Found.Length && i < Found.Length; i++) g.Found[i] = Found[i];
        g.GoldEarned = GoldEarned;
    }

    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>The save in this text, or null if it isn't one (or it's from a version this build can't read).</summary>
    public static SaveData FromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(json, Options);
            return data != null && data.Version == CurrentVersion ? data : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
