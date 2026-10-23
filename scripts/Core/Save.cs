using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Who's minding the stall, on what terms, with what talents (fixed for the run).</summary>
    public string Character { get; set; }
    public int Challenge { get; set; }
    public List<string> Talents { get; set; } = new();
    public List<string> Snubbed { get; set; } = new();
    public bool MaraUsed { get; set; }
    public bool KeepsakeTaken { get; set; }
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

    /// <summary>Places heard of, and each seller's stories checked: [told, true].</summary>
    public List<string> Places { get; set; } = new();
    public Dictionary<string, int[]> Stories { get; set; } = new();
    public List<string> ActiveSynergies { get; set; } = new();
    public List<SavedItem> Display { get; set; } = new();
    public List<string> SongsFound { get; set; } = new();

    /// <summary>The journal's tally: kind by kind, five qualities each.</summary>
    public List<int> Acquired { get; set; } = new();

    // The run: the Guild's dues, the map, curios, this week's commissions, event and peddler.
    public int WeeksPaid { get; set; }
    public int Arrears { get; set; }
    public bool GraceUsed { get; set; }
    public int GraceCount { get; set; }
    public int StashUsed { get; set; }
    public int ClearedDay { get; set; }
    public int CloverWeek { get; set; }
    public int DrySpell { get; set; }
    public bool BramPiece { get; set; }
    public int HoodPieces { get; set; }
    public bool MapGiven { get; set; }
    public bool VaultOpened { get; set; }
    public List<string> Curios { get; set; } = new();

    /// <summary>What was paid for each curio, in the same order.</summary>
    public List<int> CurioPaid { get; set; } = new();
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
    public bool Carried { get; set; }
}

/// <summary>
/// What you've done across every run, kept apart from the saved shop: runs started, debts cleared, vaults opened, the
/// furthest week reached and the most gold held; renown to spend and the talents it's bought; and for each of Bram's
/// kin, the highest challenge level open and the best score.
/// </summary>
public sealed class Profile
{
    public int Runs { get; set; }
    public int Cleared { get; set; }
    public int Vaults { get; set; }

    /// <summary>The Heartsongs Bram's kin have ever opened (the ledger's songbook): all nine change the Crown Chest's ending.</summary>
    public List<string> Songs { get; set; } = new();

    /// <summary>Crown Chests opened with all nine Heartsongs known: the Locking sung whole.</summary>
    public int SungWhole { get; set; }

    /// <summary>Notes the Heartsongs a run has opened. Returns how many of them are new to the songbook.</summary>
    public int Learn(GameState state)
    {
        int before = Songs.Count;
        foreach (var song in state.SongsFound.Where(s => !Songs.Contains(s))) Songs.Add(song);
        return Songs.Count - before;
    }
    public int BestWeek { get; set; }
    public int BestGold { get; set; }

    /// <summary>The fewest days it's taken to clear Bram's debt (0 until it's been cleared).</summary>
    public int FastestClear { get; set; }

    /// <summary>Renown to spend on talents, and all that's ever been earned.</summary>
    public int Renown { get; set; }
    public int RenownEarned { get; set; }

    public List<string> Talents { get; set; } = new();

    /// <summary>Per character: the highest challenge level open (clearing the debt at a level opens the next).</summary>
    public Dictionary<string, int> Challenge { get; set; } = new();

    /// <summary>Per character: the best score a run of theirs has made.</summary>
    public Dictionary<string, int> BestScore { get; set; } = new();

    /// <summary>Who and what level the last run was, for the character screen.</summary>
    public string LastCharacter { get; set; } = Characters.Kit;
    public int LastChallenge { get; set; }

    public bool Unlocked(CharacterDef character) => character.Unlocked(this);

    public int MaxChallenge(string character) => Challenge.GetValueOrDefault(character);

    public bool Knows(string talent) => Talents.Contains(talent);

    /// <summary>A talent that's next on its branch (what it builds on is learned) and affordable.</summary>
    public bool CanLearn(TalentDef talent) => !Knows(talent.Key) && (talent.Requires == null || Knows(talent.Requires)) && Renown >= talent.Cost;

    public bool Learn(TalentDef talent)
    {
        if (!CanLearn(talent)) return false;
        Renown -= talent.Cost;
        Talents.Add(talent.Key);
        return true;
    }

    /// <summary>The debt's been cleared at this run's level: the next level opens for this character.</summary>
    public void ClearedAt(GameState state) =>
        Challenge[state.Character] = Math.Max(MaxChallenge(state.Character), Math.Min(Challenges.Max, state.Challenge + 1));

    /// <summary>A run is over: its renown is banked, and its score noted. Returns the renown it earned.</summary>
    public int Bank(GameState state)
    {
        int renown = Score.Renown(state), score = Score.Total(state);
        Renown += renown;
        RenownEarned += renown;
        BestScore[state.Character] = Math.Max(BestScore.GetValueOrDefault(state.Character), score);
        return renown;
    }

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static Profile FromJson(string json)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<Profile>(json) ?? new Profile();
            profile.Talents.RemoveAll(t => !Core.Talents.Exists(t));
            profile.Songs.RemoveAll(s => !Heartsongs.Exists(s));
            return profile;
        }
        catch (JsonException)
        {
            return new Profile();
        }
    }

    /// <summary>Notes how far a run got (call as it goes, and when it ends).</summary>
    public void Note(GameState state)
    {
        Learn(state);
        BestWeek = Math.Max(BestWeek, state.Week);
        BestGold = Math.Max(BestGold, state.Gold);
        if (state.ClearedDay > 0 && (FastestClear == 0 || state.ClearedDay < FastestClear)) FastestClear = state.ClearedDay;
    }

    /// <summary>A line for the title screen, or null before the first run.</summary>
    public string Summary => Runs == 0 ? null
        : $"Best: week {BestWeek}   Debts cleared: {Cleared}{(FastestClear > 0 ? $" (fastest: day {FastestClear})" : "")}   Crown Chests opened: {Vaults}" +
          (Songs.Count > 0 ? $"   Heartsongs: {Songs.Count}/{Heartsongs.All.Length}" : "");
}

/// <summary>Something on display, as it's saved.</summary>
public sealed class SavedItem
{
    public Category Category { get; set; }
    public Rarity Quality { get; set; }
    public bool Magic { get; set; }

    /// <summary>Which Heartsong (for the Heartsong kind).</summary>
    public string Song { get; set; }
}
