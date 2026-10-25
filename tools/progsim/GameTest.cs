using System;
using System.Diagnostics;
using System.Linq;
using LootFall.Game;
using LootFall.Progression;

/// <summary>
/// progsim --game [--hours=N] [--seed=N]: plays the game's session (scripts/Game) headless the way a sensible player
/// would (opens every chest, spends gold and materials where they're worth most, resets by the simulator's rule), and
/// checks what the game adds to the framework: that its pace keeps up with the simulated player's, that a save
/// restores to the same game, and that time away is caught up.
/// </summary>
static class GameTest
{
    static readonly double[] Marks = { 0.25, 1, 3, 10, 30, 100, 300, 1000 };

    public static int Run(double hours, int seed)
    {
        var clock = Stopwatch.StartNew();
        int failures = 0;
        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"  {(ok ? "pass" : "FAIL")}  {name,-56} {detail}");
            if (!ok) failures++;
        }

        Console.WriteLine($"Game check: a new game played for {Report.Time(hours)} (seed {seed})");
        var s = Session.NewGame(seed);
        var sim = new Sim(new Params(), new Content(new Params()), new TalentTree(new Params()), Profile.Active(), seed);
        int mark = 0;
        Console.WriteLine("      time  game depth  record  sim record  chests  upgrades  level  asc trans");
        while (s.Time < hours * 3600)
        {
            Play(s, 60);
            if (mark < Marks.Length && s.Time >= Marks[mark] * 3600 - 1e-6)
            {
                sim.Run(Marks[mark]);
                Console.WriteLine($"  {Report.Time(Marks[mark]),8} {s.H.RunDepth,11} {s.H.EverDepth,7} {sim.H.EverDepth,11} {s.Totals.Opened,7} {s.Totals.Upgrades,9} {s.H.Level,6} {s.H.Ascension.Resets,4} {s.H.Transcendence.Resets,5}");
                mark++;
            }
        }
        sim.Run(hours);
        double ratio = (double)s.H.EverDepth / sim.H.EverDepth;
        Check("keeps up with the simulated player (record within 25%)", ratio is > 0.75 and < 1.33, $"{s.H.EverDepth} vs {sim.H.EverDepth} ({ratio:0.00})");
        Check("every chest opened went somewhere", s.Pile.Waiting.Count == 0 && s.Totals.Opened > 0, $"{s.Totals.Opened} opened, {s.Pile.Waiting.Count} waiting");
        Check("codex filling", s.H.CodexCount > 10, $"{s.H.CodexCount} entries");

        // A save restores to the same game: both copies, played on for an hour, end up in the same place.
        var json = SaveGame.ToJson(SaveGame.Capture(s, seed, 0));
        var t = SaveGame.Restore(SaveGame.FromJson(json));
        Check("a save restores the hero", Same(s, t), $"{json.Length / 1024} KB");
        Play(s, 3600);
        Play(t, 3600);
        Check("a restored game plays on the same", Same(s, t), $"record {s.H.EverDepth} vs {t.H.EverDepth}, gold {s.H.Gold} vs {t.H.Gold}");

        // Time away is caught up with what the player has unlocked.
        var away = SaveGame.Restore(SaveGame.FromJson(json));
        var sum = away.CatchUp(8 * 3600);
        Console.WriteLine($"  away 8h: depth {sum.DepthBefore} -> {sum.DepthAfter}, record {sum.RecordBefore} -> {sum.RecordAfter}, {sum.Chests} chests, {sum.Equipped} upgrades, {sum.Levels} levels, {sum.NewEntries} first finds, {sum.Ascensions} ascensions, best {string.Join(", ", sum.Best.Take(3).Select(c => Content.RarityNames[c.Item.Rarity]))}");
        Check("time away moves the hero on", sum.RecordAfter >= sum.RecordBefore && sum.Chests > 0, $"{sum.Chests} chests in 8h");

        Console.WriteLine(failures == 0 ? $"All game checks pass ({clock.Elapsed.TotalSeconds:0}s)." : $"{failures} game checks FAIL ({clock.Elapsed.TotalSeconds:0}s).");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Plays <paramref name="seconds"/> as a sensible active player.</summary>
    static void Play(Session s, double seconds)
    {
        double end = s.Time + seconds;
        while (s.Time < end - 1e-6)
        {
            s.Tick(1.0);
            s.Events.Clear();
            if (s.Pile.Waiting.Count > 0) s.OpenAll();
            if ((int)s.Time % 10 == 0)
            {
                if (s.Unlocked(Feature.Forge)) s.ForgeAll();
                if (s.Unlocked(Feature.Ranks)) s.RankAll();
            }
            if ((int)s.Time % 60 == 0) s.AutoPrestige(transcend: true);
        }
    }

    static bool Same(Session a, Session b) =>
        a.H.RunDepth == b.H.RunDepth && a.H.EverDepth == b.H.EverDepth && a.H.Level == b.H.Level && a.H.Gold.Equals(b.H.Gold)
        && a.H.CodexCount == b.H.CodexCount && a.H.Talents.AllocatedCount == b.H.Talents.AllocatedCount
        && a.H.Ascension.Resets == b.H.Ascension.Resets && Math.Abs(a.H.Materials - b.H.Materials) < 1e-6 * Math.Max(1, a.H.Materials)
        && a.H.Gear.Select((g, i) => g == null ? b.H.Gear[i] == null : b.H.Gear[i] != null && g.Ilvl == b.H.Gear[i].Ilvl && g.Rarity == b.H.Gear[i].Rarity).All(x => x)
        && a.Pile.Waiting.Count == b.Pile.Waiting.Count;
}
