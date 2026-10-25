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
        var layout = new TreeLayout(new TalentTree(new Params()));
        var tree = new TalentTree(new Params());
        var (gap, ga, gb) = layout.Closest();
        Check("the talent tree's map has room for every node", gap >= 30, $"closest two {gap:0} px apart ({tree.Nodes[ga].Kind} r{tree.Nodes[ga].Ring} s{tree.Nodes[ga].Sector}, {tree.Nodes[gb].Kind} r{tree.Nodes[gb].Ring} s{tree.Nodes[gb].Sector}), {2 * layout.Radius:0} px across");
        StageCheck(seed, Check);

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

    /// <summary>
    /// The battle as the screen shows it (<see cref="Stage"/>), at 30 frames a second: ten minutes of a new game, then a
    /// run after an ascension. Every depth cleared shows its boss falling (or a leap), every monster that comes on falls,
    /// the hero never falls, and overkill after a reset leaps depths by the hundred.
    /// </summary>
    static void StageCheck(int seed, Action<string, bool, string> check)
    {
        var s = Session.NewGame(seed);
        var stage = new Stage(s);
        var cues = new int[Enum.GetValues<Cue>().Length];
        double lowest = 1;
        int leapt = 0;
        void Watch(double seconds)
        {
            Array.Clear(cues);
            lowest = 1;
            leapt = 0;
            const double dt = 1.0 / 30;
            for (double t = 0; t < seconds; t += dt)
            {
                s.Tick(dt);
                s.Events.Clear();
                if (s.Pile.Waiting.Count > 0) s.OpenAll();
                stage.Advance(dt);
                foreach (var c in stage.Cues)
                {
                    cues[(int)c.Kind]++;
                    if (c.Kind == Cue.Leap) leapt += c.Count;
                }
                lowest = Math.Min(lowest, stage.HeroHp);
            }
        }
        int before = s.H.RunDepth;
        Watch(600);
        int cleared = s.H.RunDepth - before;
        check("the battle shows each depth's boss fall", cues[(int)Cue.BossDown] + leapt == cleared && cleared > 5,
            $"{cleared} depths, {cues[(int)Cue.BossDown]} bosses down, {cues[(int)Cue.Leap]} leaps");
        check("every monster on the lane falls, the hero never", cues[(int)Cue.Kill] >= cues[(int)Cue.Spawn] - 2 && lowest > 0,
            $"{cues[(int)Cue.Spawn]} came, {cues[(int)Cue.Kill]} fell, {cues[(int)Cue.Hit]} blows, hero at {lowest:P0} or more");

        // Overkill after a reset: on to the second ascension, then five minutes of the run that follows.
        while (s.H.Ascension.Resets < 2 && s.Time < 60 * 3600) Play(s, 1);
        stage = new Stage(s);
        before = s.H.RunDepth;
        Watch(300);
        check("after a reset, overkill leaps depths by the hundred", leapt >= 100,
            $"{s.H.Ascension.Resets} ascensions: depth {before} -> {s.H.RunDepth} in 5m, {leapt} in {cues[(int)Cue.Leap]} leaps");
    }

    static void Play(Session s, double seconds) => SensiblePlayer.Play(s, seconds);

    static bool Same(Session a, Session b) =>
        a.H.RunDepth == b.H.RunDepth && a.H.EverDepth == b.H.EverDepth && a.H.Level == b.H.Level && a.H.Gold.Equals(b.H.Gold)
        && a.H.CodexCount == b.H.CodexCount && a.H.Talents.AllocatedCount == b.H.Talents.AllocatedCount
        && a.H.Ascension.Resets == b.H.Ascension.Resets && Math.Abs(a.H.Materials - b.H.Materials) < 1e-6 * Math.Max(1, a.H.Materials)
        && a.H.Gear.Select((g, i) => g == null ? b.H.Gear[i] == null : b.H.Gear[i] != null && g.Ilvl == b.H.Gear[i].Ilvl && g.Rarity == b.H.Gear[i].Rarity).All(x => x)
        && a.Pile.Waiting.Count == b.Pile.Waiting.Count;
}
