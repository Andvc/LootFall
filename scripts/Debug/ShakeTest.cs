using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Debug;

/// <summary>
/// Checks that shaking follows the rules: `godot --headless --path . -- --shake-test [--n=40]`. Shakes random town
/// and Emporium chests (two of a kind, sand, gilded fakes, and every third one a Heartsong) gently, firmly and hard, and
/// gently with the magnet on, the way the bot does, and compares what made a sound,
/// thing by thing, with what <see cref="Chest.HeardAt"/> says should: anything expected but never heard is a miss,
/// anything heard that the rules say should be silent is a leak.
/// </summary>
public partial class ShakeTest : Node2D
{
    public int Count = 40;
    public double Seconds = 1.6;

    static readonly (Shake Level, bool Magnet)[] Trials =
    {
        (Shake.Gentle, false), (Shake.Firm, false), (Shake.Hard, false), (Shake.Gentle, true), (Shake.Firm, true),
    };

    public override async void _Ready()
    {
        var rng = new Random(11);
        var expected = new Dictionary<(Shake, bool), int>();
        var heardRight = new Dictionary<(Shake, bool), int>();
        var leaks = new Dictionary<(Shake, bool), int>();
        var levelOk = new Dictionary<(Shake, bool), int>();
        var missedBy = new Dictionary<Category, int>();
        int pairs = 0, sand = 0;
        for (int i = 0; i < Count; i++)
        {
            var seller = rng.PickOne(Sellers.All.Where(s => s.Tier >= 3).ToArray());
            var chest = Market.CreateVisit(rng, seller, 4).Chest;
            if (i % 3 == 0) chest.AddSong(Heartsongs.Make(rng.PickOne(Heartsongs.All), chest.Quality));
            if (chest.Items.GroupBy(it => it.Category).Any(g => g.Count() > 1)) pairs++;
            if (chest.Items.Any(it => it.Fake && !it.Def.FakeIron)) sand++;
            foreach (var trial in Trials)
            {
                var box = new ShakeBox(chest, new Rect2(452, 220, 232, 196))
                    { Position = new Vector2(556, 416), Magnet = trial.Magnet };
                var got = new HashSet<int>();
                int frames = 0, framesAtLevel = 0;
                // Only what sounded while the shake was at the level under test: the chest passes through gentler
                // shakes as it speeds up, and what you hear then belongs to those.
                box.Heard += (item, level) =>
                {
                    if (level == trial.Level) got.Add(item.Id);
                };
                AddChild(box);
                await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
                box.AutoShake(Seconds, trial.Level);
                double end = Time.GetTicksMsec() / 1000.0 + Seconds;
                while (Time.GetTicksMsec() / 1000.0 < end)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    frames++;
                    if (box.Level == trial.Level) framesAtLevel++;
                }
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                var should = chest.HeardAt(trial.Level, trial.Magnet).ToList();
                expected[trial] = expected.GetValueOrDefault(trial) + should.Count;
                heardRight[trial] = heardRight.GetValueOrDefault(trial) + should.Count(it => got.Contains(it.Id));
                leaks[trial] = leaks.GetValueOrDefault(trial) + got.Count(id => should.All(it => it.Id != id));
                if (framesAtLevel > frames * 0.6) levelOk[trial] = levelOk.GetValueOrDefault(trial) + 1;
                foreach (var missed in should.Where(it => !got.Contains(it.Id))) missedBy[missed.Category] = missedBy.GetValueOrDefault(missed.Category) + 1;
                box.QueueFree();
            }
        }
        GD.Print($"Shake test: {Count} town and Emporium chests ({pairs} with two of a kind, {sand} with sand), " +
                 $"{Seconds}s per shake");
        foreach (var trial in Trials)
            GD.Print($"  {Categories.LevelName(trial.Level),-6}{(trial.Magnet ? " + magnet" : "         ")}: " +
                     $"heard {heardRight[trial]}/{expected[trial]} that should sound, {leaks[trial]} leaks, " +
                     $"at the right level {levelOk.GetValueOrDefault(trial)}/{Count} shakes");
        foreach (var (category, n) in missedBy.OrderByDescending(p => p.Value))
            GD.Print($"  missed {category}: {n}");
        Main.Instance.QuitGame();
    }
}
