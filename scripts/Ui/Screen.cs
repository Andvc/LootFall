using Godot;
using LootFall.Audio;

namespace LootFall.Ui;

/// <summary>Base for a whole screen (title, play). Main swaps them in and out.</summary>
public partial class Screen : Node2D
{
    protected static Main Game => Main.Instance;
    protected static Sfx Sfx => Main.Instance.Sfx;

    /// <summary>False once this screen has been swapped out; check it after every await.</summary>
    protected bool Alive => IsInstanceValid(this) && IsInsideTree();

    protected SignalAwaiter Wait(double seconds) =>
        ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
