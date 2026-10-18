using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LootFall.Core;

/// <summary>
/// Everything that carries over from one day to the next, kept as plain data so it can be written to a file. The shop
/// keeps its books at the start of each day: today's customers are rolled again from the seed when you come back.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string SavedAt { get; set; }

    public int Seed { get; set; }
    public int Gold { get; set; }
    public int Day { get; set; }
    public int Tier { get; set; }
    public bool Guided { get; set; }
    public int TotalEarned { get; set; }
    public bool UpgradesOpen { get; set; }
    public int MagicFound { get; set; }
    public int PerfectStreak { get; set; }
    public int BestStreak { get; set; }

    public List<string> Owned { get; set; } = new();
    public List<string> HintsSeen { get; set; } = new();
    public List<string> Met { get; set; } = new();
    public List<string> Found { get; set; } = new();
    public List<string> FoundEnchanted { get; set; } = new();
    public List<string> Tutored { get; set; } = new();
    public List<string> ActiveSynergies { get; set; } = new();
    public List<SavedItem> Display { get; set; } = new();

    /// <summary>The journal's tally: kind by kind, five qualities each.</summary>
    public List<int> Acquired { get; set; } = new();

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>The save in this text, or null if it isn't one this version can read.</summary>
    public static SaveData FromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(json);
            return data is { Version: CurrentVersion } && data.Tier >= 1 ? data : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A line for the title screen: "Day 12, Town Shop, 840g".</summary>
    public string Summary => $"Day {Day + 1}, {ShopTiers.Name(Tier)}, {Gold}g";
}

/// <summary>Something on display, as it's saved.</summary>
public sealed class SavedItem
{
    public Category Category { get; set; }
    public Rarity Quality { get; set; }
    public bool Magic { get; set; }
}
