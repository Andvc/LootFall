using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Debug;

/// <summary>
/// Checks that shaking follows the rules: `godot --headless --path . -- --shake-test [--n=40]`. Shakes random
/// Emporium chests gently, firmly and hard (and gently with the magnet on) the way the bot does, and compares what made
/// a sound with what <see cref="Chest.HeardAt"/> says should: anything expected but never heard is a miss, anything
/// heard that the rules say should be silent is a leak.
/// </summary>
public partial class ShakeTest : Node2D
{
    public int Count = 40;
    public double Seconds = 1.6;

    static readonly (Shake Level, bool Magnet)[] Trials =
        { (Shake.Gentle, false), (Shake.Firm, false), (Shake.Hard, false), (Shake.Gentle, true) };

    public override async void _Ready()
    {
        var rng = new Random(11);
        var expected = new Dictionary<(Shake, bool), int>();
        var heardRight = new Dictionary<(Shake, bool), int>();
        var leaks = new Dictionary<(Shake, bool), int>();
        var levelOk = new Dictionary<(Shake, bool), int>();
        var missedBy = new Dictionary<Category, int>();
        for (int i = 0; i < Count; i++)
        {
            var seller = rng.PickOne(Sellers.All.Where(s => s.Tier == 4).ToArray());
            var chest = Market.CreateVisit(rng, seller, 4).Chest;
            foreach (var trial in Trials)
            {
                var box = new ShakeBox(chest, new Rect2(452, 220, 232, 196)) { Position = new Vector2(556, 416), Magnet = trial.Magnet };
                var got = new HashSet<Category>();
                int frames = 0, framesAtLevel = 0;
                // Only what sounded while the shake was at the level under test: the chest passes through gentler
                // shakes as it speeds up, and what you hear then belongs to those.
                box.Heard += (item, level) =>
                {
                    if (level == trial.Level) got.Add(item.Category);
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
                var should = chest.HeardAt(trial.Level, trial.Magnet).Select(it => it.Category).ToHashSet();
                expected[trial] = expected.GetValueOrDefault(trial) + should.Count;
                heardRight[trial] = heardRight.GetValueOrDefault(trial) + should.Count(got.Contains);
                leaks[trial] = leaks.GetValueOrDefault(trial) + got.Count(c => !should.Contains(c));
                if (framesAtLevel > frames * 0.6) levelOk[trial] = levelOk.GetValueOrDefault(trial) + 1;
                foreach (var missed in should.Where(c => !got.Contains(c))) missedBy[missed] = missedBy.GetValueOrDefault(missed) + 1;
                box.QueueFree();
            }
        }
        GD.Print($"Shake test: {Count} Emporium chests, {Seconds}s per shake");
        foreach (var trial in Trials)
            GD.Print($"  {Categories.LevelName(trial.Level),-6}{(trial.Magnet ? " + magnet" : "         ")}: heard {heardRight[trial]}/{expected[trial]} that should sound, " +
                     $"{leaks[trial]} leaks, at the right level {levelOk.GetValueOrDefault(trial)}/{Count} shakes");
        foreach (var (category, n) in missedBy.OrderByDescending(p => p.Value))
            GD.Print($"  missed {category}: {n}");
        GetTree().Quit();
    }
}
