using Godot;
using LootFall.Audio;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>Base for one screen of the loop (auction, lockpick, opening). Main swaps them in and out.</summary>
public partial class Phase : Node2D
{
    protected static Main Game => Main.Instance;
    protected static GameState State => Main.Instance.State;
    protected static Sfx Sfx => Main.Instance.Sfx;

    /// <summary>False once this phase has been swapped out; check it after every await.</summary>
    protected bool Alive => IsInstanceValid(this) && IsInsideTree();

    protected SignalAwaiter Wait(double seconds) =>
        ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    protected T Add<T>(T node) where T : Node
    {
        AddChild(node);
        return node;
    }

    /// <summary>Text that pops up, drifts upward and fades.</summary>
    protected void FloatText(string text, Vector2 center, Color color, int size = 30)
    {
        var label = Add(UiKit.MakeLabel(text, size, color, HorizontalAlignment.Center, outline: 6)
            .Place(center.X - 200, center.Y - 20, 400, 44));
        label.PivotOffset = new Vector2(200, 22);
        label.Scale = Vector2.One * 0.6f;
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "position:y", label.Position.Y - 50, 0.9f).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.35f).SetDelay(0.6f);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }

    protected static Label Header(string text, float x, float y) =>
        UiKit.MakeLabel(text, 16, Palette.Gold.Darkened(0.2f)).Place(x, y, 300, 20);
}
