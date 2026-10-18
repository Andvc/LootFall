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
    public const int CurrentVersion = 2;

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

    // The run: the Guild's dues, the map, curios, this week's commissions, event and peddler.
    public int WeeksPaid { get; set; }
    public int Arrears { get; set; }
    public bool GraceUsed { get; set; }
    public bool BramPiece { get; set; }
    public int HoodPieces { get; set; }
    public bool MapGiven { get; set; }
    public bool VaultOpened { get; set; }
    public List<string> Curios { get; set; } = new();
    public List<string> EventsSeen { get; set; } = new();
    public string WeekEvent { get; set; }
    public int EventDay { get; set; }
    public Category BuyerKind { get; set; }
    public int PeddlerDay { get; set; }
    public List<SavedCommission> Commissions { get; set; } = new();

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// The save in this text, or null if it isn't one this version can read. A shop saved before the Guild came calling
    /// (version 1) picks up as if every week so far had been paid, with Bram's piece of the map if it's past the first.
    /// </summary>
    public static SaveData FromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(json);
            if (data == null || data.Tier < 1 || data.Version > CurrentVersion || data.Version < 1) return null;
            if (data.Version == 1)
            {
                data.Version = CurrentVersion;
                data.WeeksPaid = data.Day / Guild.DaysPerWeek;
                data.BramPiece = data.Day >= Guild.DaysPerWeek;
                data.Owned.Remove("warrant");
            }
            return data;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A line for the title screen: "Week 3, day 2, Town Shop, 840g".</summary>
    public string Summary => $"Week {Day / Guild.DaysPerWeek + 1}, day {Day % Guild.DaysPerWeek + 1}, {ShopTiers.Name(Tier)}, {Gold}g";
}

/// <summary>A commission, as it's saved.</summary>
public sealed class SavedCommission
{
    public string Patron { get; set; }
    public CommissionKind Kind { get; set; }
    public Category Category { get; set; }
    public Rarity Quality { get; set; }
    public int Needed { get; set; }
    public int Delivered { get; set; }
    public int Reward { get; set; }
    public bool Story { get; set; }
}

/// <summary>
/// What you've done across every run, kept apart from the saved shop: runs started, debts cleared, vaults opened, the
/// furthest week reached and the most gold held.
/// </summary>
public sealed class Profile
{
    public int Runs { get; set; }
    public int Cleared { get; set; }
    public int Vaults { get; set; }
    public int BestWeek { get; set; }
    public int BestGold { get; set; }

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static Profile FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Profile>(json) ?? new Profile();
        }
        catch (JsonException)
        {
            return new Profile();
        }
    }

    /// <summary>Notes how far a run got (call as it goes, and when it ends).</summary>
    public void Note(GameState state)
    {
        BestWeek = Math.Max(BestWeek, state.Week);
        BestGold = Math.Max(BestGold, state.Gold);
    }

    /// <summary>A line for the title screen, or null before the first run.</summary>
    public string Summary => Runs == 0 ? null
        : $"Best: week {BestWeek}   Debts cleared: {Cleared}   Vaults opened: {Vaults}";
}

/// <summary>Something on display, as it's saved.</summary>
public sealed class SavedItem
{
    public Category Category { get; set; }
    public Rarity Quality { get; set; }
    public bool Magic { get; set; }
}
