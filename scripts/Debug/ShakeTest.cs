using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Debug;

/// <summary>
/// Checks that shaking a chest lets you hear everything in it: `godot --headless --path . -- --shake-test [--n=40]`.
/// Shakes random chests the way the bot does and reports which items never made a sound.
/// </summary>
public partial class ShakeTest : Node2D
{
    public int Count = 40;
    public double Seconds = 2.4;

    public override async void _Ready()
    {
        var rng = new Random(11);
        int items = 0, heard = 0, fullyHeard = 0;
        var missed = new Dictionary<string, int>();
        var total = new Dictionary<string, int>();
        for (int i = 0; i < Count; i++)
        {
            var chest = ChestGenerator.Generate(rng, rng.PickOne(new[] { Origins.Farm, Origins.Barracks, Origins.Tower, Origins.Manor }), 4);
            var box = new ShakeBox(chest, new Rect2(330, 200, 430, 250)) { Position = new Vector2(430, 450) };
            var got = new HashSet<int>();
            box.Impacted += (index, _) => got.Add(index);
            AddChild(box);
            await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
            box.AutoShake(Seconds);
            await ToSignal(GetTree().CreateTimer(Seconds + 0.6), SceneTreeTimer.SignalName.Timeout);
            for (int k = 0; k < chest.Items.Count; k++)
            {
                var item = chest.Items[k].Def;
                total[item.Key] = total.GetValueOrDefault(item.Key) + 1;
                if (!got.Contains(k)) missed[item.Key] = missed.GetValueOrDefault(item.Key) + 1;
            }
            items += chest.Items.Count;
            heard += got.Count;
            if (got.Count == chest.Items.Count) fullyHeard++;
            else GD.Print($"  chest {i}: {chest.Weight} lb, heard {got.Count}/{chest.Items.Count}");
            box.QueueFree();
        }
        GD.Print($"Shake test: {heard}/{items} items heard, {fullyHeard}/{Count} chests fully heard in {Seconds}s of shaking");
        foreach (var (key, n) in missed.OrderByDescending(p => p.Value))
            GD.Print($"  missed {key}: {n}/{total[key]}");
        GetTree().Quit();
    }
}
