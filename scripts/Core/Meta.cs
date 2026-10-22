using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// One of Bram's kin, who comes to mind the stall. Each reads chests or runs the shop their own way: one rule of the
/// trade that's different for them, and what it costs them. The rest of the game is the same for everyone.
/// </summary>
/// <param name="Title">What they're known for, in two or three words.</param>
/// <param name="Blurb">Who they are, in a sentence.</param>
/// <param name="Rules">What's different when you play them, one line each.</param>
/// <param name="Unlock">What unlocks them, for the character screen (null: from the start).</param>
public sealed record CharacterDef(string Key, string Name, string Title, string Blurb, string[] Rules, string Unlock, Func<Profile, bool> Unlocked);

public static class Characters
{
    public const string Kit = "kit", Hesk = "hesk", Rosa = "rosa", Isolde = "isolde";

    public static readonly CharacterDef[] All =
    {
        new(Kit, "Kit", "The Ear", "Bram's niece, who grew up behind his counter, with the Key-mark and his ear for a chest.",
            new[]
            {
                "Shakes at every strength and drops a chest to hear what's under the floor.",
                "With tips on, Uncle Bram teaches the trade as each new seller arrives.",
            }, null, _ => true),
        new(Hesk, "Hesk", "The Scale", "Bram's nephew, a grocer from the river towns, with the Key-mark, a bad back and an honest scale.",
            new[]
            {
                "A bad back: he can't shake a chest hard, or lift it to drop it.",
                "Every chest sits on his scale: he knows what everything in it weighs, to the half pound.",
                $"Coin {Pounds(Category.Coins)} lb, arms {Pounds(Category.Arms)}, garb {Pounds(Category.Garb)}, potions {Pounds(Category.Potions)}, " +
                $"scrolls {Pounds(Category.Scrolls)}, gems {Pounds(Category.Gems)}. Demon gold weighs half what gold does; sand is heavier than a scroll.",
            }, "Pay the Guild's third due to unlock.", p => p.BestWeek >= 4),
        new(Rosa, "Rosa", "The Tongue", "Bram's niece from the Crownhold markets. She'd haggle with a statue, and win.",
            new[]
            {
                "Everybody haggles with Rosa: sellers who'd name a fixed price ask high and hear an offer instead.",
                "They know her: they open a little higher and come down a little less. An offer too low gets a second try.",
                "But word gets round: a seller who walks out on her won't come back that week.",
            }, "Clear Bram's debt to unlock.", p => p.Cleared >= 1),
        new(Isolde, "Isolde", "The Collection", "Bram's niece by marriage, a lady who has never sold a thing she liked.",
            new[]
            {
                $"A shelf from the start: she can display {IsoldeShelf} things at Bram's stall.",
                $"Visitors pay to see her collection: every night, {AdmissionPerPoint}g per point of exhibition.",
                "Her collection is not for pawning: on a short night, the Guild can only take her curios.",
            }, "Open the Crown Chest to unlock.", p => p.Vaults >= 1),
    };

    public static CharacterDef Get(string key) => All.FirstOrDefault(c => c.Key == key) ?? All[0];

    public static bool Exists(string key) => All.Any(c => c.Key == key);

    /// <summary>Isolde's shelf at the stall: room for this many things.</summary>
    public const int IsoldeShelf = 2;

    /// <summary>
    /// What Isolde's visitors pay, per exhibition point, every night. Not more in a grander shop: the door money is a
    /// return on what she keeps, and one that grew with the shop cleared her runs days early (sim: day 33 against 36).
    /// </summary>
    public const int AdmissionPerPoint = 1;

    /// <summary>What one thing of this kind weighs on Hesk's scale, in pounds.</summary>
    public static float Pounds(Category kind) => kind switch
    {
        Category.Arms => 6,
        Category.Coins => 4,
        Category.Garb => 3,
        Category.Potions => 3,
        _ => 1,
    };

    /// <summary>
    /// What a fake weighs: gilded iron half what the gold it apes does (gold is heavy, iron isn't), a bag of sand three
    /// times a bundle of papers.
    /// </summary>
    public static float FakePounds(Category kind) => kind == Category.Scrolls ? 3 : Pounds(kind) / 2;

    public static float Pounds(Item item) => item.Fake ? FakePounds(item.Category) : Pounds(item.Category);

    /// <summary>"13.5 lb", "4 lb".</summary>
    public static string Weight(float pounds) => $"{(pounds % 1 == 0 ? $"{pounds:0}" : $"{pounds:0.0}")} lb";
}

/// <summary>
/// A challenge level's term: one more of the Guild's harsh terms, on top of every level below it. Clearing Bram's debt
/// at a level opens the next for that character, and each level adds a quarter to the renown a run earns.
/// </summary>
public sealed record ChallengeDef(string Key, string Name, string Rule)
{
    /// <summary>The level it comes in at: every level above keeps it.</summary>
    public int Level => Array.IndexOf(Challenges.All, this) + 1;
}

public static class Challenges
{
    public const int Max = 8;

    public const string LeanStart = "lean", Stretched = "stretched", FussySellers = "fussy", SharpSellers = "sharp", Forgers = "forgers",
        Hurried = "hurried", Interest = "interest", ThinCrowds = "thin";

    /// <summary>Sharp sellers ask this much more, and take no less.</summary>
    public const float SharpPrices = 1.05f;

    /// <summary>The Guild's interest on every due.</summary>
    public const float InterestRate = 1.1f;

    /// <summary>With demon gold about: the chance any coin from any seller is gilded iron.</summary>
    public const float SlugChance = 0.05f;

    /// <summary>When everyone's in a hurry: seconds each seller gives you.</summary>
    public const float HurriedPatience = 20f;

    public static readonly ChallengeDef[] All =
    {
        new(Stretched, "Bram's stretched thin", "Bram's secret stash has one dip fewer, and his word is good one time fewer."),
        new(FussySellers, "Fussy sellers", "Every seller minds a drop, and puts the price up by half."),
        new(Forgers, "Demon gold about", "One coin in twenty is demon gold, whoever brings it."),
        new(LeanStart, "Lean start", "You start with half the gold."),
        new(ThinCrowds, "Thin crowds", "One customer fewer every day at Bram's stall."),
        new(Interest, "The Guild's interest", "Every week's due is a tenth higher."),
        new(SharpSellers, "Sharp sellers", "Every seller asks a twentieth more, and takes a twentieth more."),
        new(Hurried, "Hurried", $"Every seller gives you {HurriedPatience:0} seconds."),
    };

    public static ChallengeDef Get(int level) => All[Math.Clamp(level, 1, Max) - 1];

    /// <summary>The level a term comes in at.</summary>
    public static int LevelOf(string key) => Array.FindIndex(All, c => c.Key == key) + 1;

    /// <summary>The terms in force at a level: every one up to it.</summary>
    public static IEnumerable<ChallengeDef> Upto(int level) => All.Where(c => c.Level <= level);

    /// <summary>What a level does to the renown a run earns: a quarter more for each.</summary>
    public static float Multiplier(int level) => 1 + 0.25f * Math.Clamp(level, 0, Max);
}

/// <summary>
/// A talent: something Bram's kin learn once and keep for every run after, bought with renown. A talent widens your
/// choices or softens a bad night; none adds a share to what things sell for or makes the debt smaller, so a run still
/// grows by your judgment.
/// </summary>
/// <param name="Branch">The branch of the tree it's on (null: the root).</param>
/// <param name="Requires">The talent before it on its branch.</param>
public sealed record TalentDef(string Key, string Name, string Blurb, int Cost, string Branch, string Requires);

public static class Talents
{
    public const string Lessons = "Bram's Lessons", Favour = "Nim's Favour", Trade = "The Trade";

    public static readonly string[] Branches = { Lessons, Favour, Trade };

    public static readonly TalentDef[] All =
    {
        new("kin", "Bram's Kin", "Start every run with 10g more.", 50, null, null),

        new("rainy", "Rainy Day", "Bram's secret stash has one more dip in it.", 100, Lessons, "kin"),
        new("pawn", "Pawnbroker's Friend", "Pawned things fetch seven-tenths of their price, not three-fifths.", 250, Lessons, "rainy"),
        new("word", "Good Word", "Bram can vouch for you twice a run.", 500, Lessons, "pawn"),
        new("mara", "Mara's Favour", "Once a run, on a night nothing you own can cover, Mara pays what's short (not in the last week).", 1000,
            Lessons, "word"),

        new("nim4", "Familiar Face", "Nim lays out four curios, not three.", 100, Favour, "kin"),
        new("reroll", "Second Look", "Once a visit, Nim will show you something else.", 250, Favour, "nim4"),
        new("keepsake", "Keepsake", "On Nim's first visit, one curio of your choice is a gift.", 500, Favour, "reroll"),
        new("curio", "Curio Cabinet", "Room for one more curio.", 1000, Favour, "keepsake"),

        new("ledger", "Bram's Ledger", "Every seller's habits are in your ledger before you meet them.", 100, Trade, "kin"),
        new("early", "Early Name", "Synergies open at Thornwick, not Crownhold.", 250, Trade, "ledger"),
        new("tools", "Trade Secrets", "The witchlight and the lodestone cost half.", 500, Trade, "early"),
        new("favour", "Royal Favour", "One more synergy active at once.", 1000, Trade, "tools"),
    };

    public static TalentDef Get(string key) => All.First(t => t.Key == key);

    public static bool Exists(string key) => All.Any(t => t.Key == key);

    public static IEnumerable<TalentDef> On(string branch) => All.Where(t => t.Branch == branch);

    /// <summary>Every talent's cost, all told.</summary>
    public static int Total => All.Sum(t => t.Cost);

    // What the talents do.
    public const int KinGold = 10, MoreStock = 1;
    public const float PawnRate = 0.7f;
}

/// <summary>
/// How a run went, in points: the Guild's dues paid, the debt cleared (and how early), the vault, the map, the shop, the
/// collection, and every week after the debt you kept the Guild paid. Points times the challenge level's multiplier are
/// the renown the run earns.
/// </summary>
public static class Score
{
    public const int PerDue = 10, Cleared = 100, PerDaySpare = 5, Vault = 100, PerPiece = 10, PerRenovation = 25, PerFind = 2, PerEndlessWeek = 25;

    /// <summary>The run's points, line by line (lines worth nothing are left out).</summary>
    public static List<(string Label, int Points)> Lines(GameState state)
    {
        var lines = new List<(string, int)>();
        void Add(string label, int points)
        {
            if (points > 0) lines.Add((label, points));
        }
        int dues = Math.Min(state.WeeksPaid, Guild.Weeks);
        Add($"Guild dues paid ({dues} of {Guild.Weeks})", dues * PerDue);
        if (state.DebtCleared)
        {
            Add("Bram's debt cleared", Cleared);
            int spare = Math.Max(0, Guild.Weeks * Guild.DaysPerWeek - state.ClearedDay);
            Add($"Days to spare ({spare})", spare * PerDaySpare);
        }
        if (state.VaultOpened) Add("The Crown Chest", Vault);
        else Add($"Key shards ({state.MapPieces})", state.MapPieces * PerPiece);
        Add($"Moves ({state.Tier - 1})", (state.Tier - 1) * PerRenovation);
        Add($"Collection ({state.Found.Count} of {Collection.Size})", state.Found.Count * PerFind);
        int endless = Math.Max(0, state.WeeksPaid - Guild.Weeks);
        Add($"Weeks after the debt ({endless})", endless * PerEndlessWeek);
        return lines;
    }

    public static int Total(GameState state) => Lines(state).Sum(l => l.Points);

    /// <summary>What the run earns: its points, times its challenge level's multiplier.</summary>
    public static int Renown(GameState state) => (int)MathF.Round(Total(state) * Challenges.Multiplier(state.Challenge));
}
