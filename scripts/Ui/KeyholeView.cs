using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// Holding a lantern to the keyhole: a dim glimpse of one random thing inside (or a lump of rock, if someone
/// packed the chest with stones). Click anywhere to look away.
/// </summary>
public partial class KeyholeView : Node2D
{
    static readonly Vector2 Center = new(640, 330);

    readonly Chest _chest;
    readonly Random _rng = new();
    public event Action Closed;

    public KeyholeView() { }

    public KeyholeView(Chest chest)
    {
        _chest = chest;
    }

    public override void _Ready()
    {
        // Blocks clicks from reaching the counter underneath; any click on it looks away.
        var blocker = new ColorRect { Color = new Color(0, 0, 0, 0.94f), MouseFilter = Control.MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920);
        blocker.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) Close();
        };
        AddChild(blocker);

        // The view through the keyhole: dark interior, lantern light, and one thing caught in it.
        var hole = new Painter(DrawKeyhole) { Position = Center };
        AddChild(hole);
        hole.AddChild(new Painter(p =>
        {
            for (int i = 5; i >= 1; i--) p.DrawCircle(new Vector2(0, -20), 20 + i * 17, new Color("#ffb347") with { A = 0.06f });
        }) { Material = Fx.Additive() });

        int choice = _rng.Next(_chest.Items.Count + _chest.Rocks.Count);
        if (choice < _chest.Items.Count)
        {
            var item = _chest.Items[choice];
            var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{item.Def.Key}.png");
            int scale = Math.Max(1, (int)MathF.Min(140f / texture.GetWidth(), 120f / texture.GetHeight()));
            var sprite = new Sprite2D { Texture = texture, Position = new Vector2(0, -16), Scale = Vector2.One * scale, Modulate = new Color(0.6f, 0.5f, 0.42f) };
            hole.AddChild(sprite);
            var bob = sprite.CreateTween().SetLoops();
            bob.TweenProperty(sprite, "position:y", -22f, 0.9f).SetTrans(Tween.TransitionType.Sine);
            bob.TweenProperty(sprite, "position:y", -16f, 0.9f).SetTrans(Tween.TransitionType.Sine);
        }
        else
        {
            hole.AddChild(new Painter(p =>
            {
                p.DrawColoredPolygon(new[] { new Vector2(-50, 30), new Vector2(-62, -20), new Vector2(-20, -56), new Vector2(40, -50), new Vector2(64, 0), new Vector2(36, 36) }, new Color("#5b5650"));
                p.DrawColoredPolygon(new[] { new Vector2(-30, -20), new Vector2(-12, -40), new Vector2(18, -34), new Vector2(4, -12) }, new Color("#77716a"));
            }));
        }

        AddChild(UiKit.MakeLabel("You hold a lantern to the keyhole...", 24, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 80, 1280, 32));
        AddChild(UiKit.MakeLabel("Click to look away", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 620, 1280, 24));

        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.25f);
    }

    static void DrawKeyhole(Painter p)
    {
        var rim = new Color("#8a6a2a");
        var inside = new Color("#1a0f0a");
        p.DrawCircle(new Vector2(0, -20), 118, rim);
        p.DrawColoredPolygon(new[] { new Vector2(-78, 40), new Vector2(78, 40), new Vector2(128, 258), new Vector2(-128, 258) }, rim);
        p.DrawCircle(new Vector2(0, -20), 110, inside);
        p.DrawColoredPolygon(new[] { new Vector2(-70, 40), new Vector2(70, 40), new Vector2(120, 250), new Vector2(-120, 250) }, inside);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void Close()
    {
        if (!IsInsideTree()) return;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.Transparent, 0.2f);
        tween.TweenCallback(Callable.From(() =>
        {
            Closed?.Invoke();
            QueueFree();
        }));
    }
}
