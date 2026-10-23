using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>The title: chests raining onto a meadow, the name, and Continue / New Game / Settings / Quit.</summary>
public partial class TitleScreen : Screen
{
    readonly SaveData _save;
    Node2D _rain;
    double _nextChest;
    readonly Random _rng = new();

    public TitleScreen(SaveData save)
    {
        _save = save;
    }

    public override void _Ready()
    {
        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>("res://assets/backgrounds/title.png"), Centered = false, Scale = Vector2.One * 2 });
        AddChild(new ColorRect { Color = new Color(0.05f, 0.03f, 0.1f, 0.25f), MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, 1280, 720));
        _rain = new Node2D();
        AddChild(_rain);

        // The knight, and a golden chest glowing beside him.
        var knight = ActorView.Make(null, "knight", 3, faceRight: true);
        knight.PlaceAt(new Vector2(250, 640));
        knight.SetGear(Rarity.Legendary, Rarity.Epic);
        AddChild(knight);
        var chestAt = new Vector2(1020, 560);
        Fx.Rays(this, chestAt, Palette.Gold, 220, 14, 0.22f, 14f);
        Fx.Aura(this, chestAt, Palette.Gold, 150, 0.08f);
        var chest = new Sprite2D { Texture = GD.Load<Texture2D>("res://assets/sprites/chests/golden_closed.png"), Position = chestAt, Scale = Vector2.One * 2 };
        AddChild(chest);
        var bob = chest.CreateTween().SetLoops();
        bob.TweenProperty(chest, "position:y", chestAt.Y - 8, 0.9f).SetTrans(Tween.TransitionType.Sine);
        bob.TweenProperty(chest, "position:y", chestAt.Y, 0.9f).SetTrans(Tween.TransitionType.Sine);

        var title = UiKit.MakeLabel("LOOTFALL", 96, Palette.Gold, HorizontalAlignment.Center, outline: 12).Place(0, 110, 1280, 110);
        AddChild(title);
        AddChild(UiKit.MakeLabel("Open chests.  Gear up.  Fight on.", 24, Palette.Text, HorizontalAlignment.Center, outline: 6).Place(0, 224, 1280, 30));
        title.PivotOffset = new Vector2(640, 55);
        var pulse = title.CreateTween().SetLoops();
        pulse.TweenProperty(title, "scale", Vector2.One * 1.03f, 1.2f).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(title, "scale", Vector2.One, 1.2f).SetTrans(Tween.TransitionType.Sine);

        float y = 300;
        if (_save != null)
        {
            AddChild(UiKit.MakeButton("CONTINUE", Palette.Good.Darkened(0.3f), 24, () => Game.Continue(_save)).Place(520, y, 240, 52));
            AddChild(UiKit.MakeLabel($"{World.StageName(_save.MaxStage)}  -  Chest Lv {_save.ChestLevel}  -  {Num.Time(_save.PlayTime)} played", 16, Palette.Text,
                HorizontalAlignment.Center, outline: 4).Place(0, y + 56, 1280, 20));
            y += 92;
        }
        AddChild(UiKit.MakeButton("NEW GAME", new Color("#7a5a2a"), 24, NewGame).Place(520, y, 240, 52));
        y += 64;
        AddChild(UiKit.MakeButton("SETTINGS", new Color("#4a4458"), 16, () => AddChild(new SettingsView())).Place(560, y, 160, 40));
        y += 52;
        AddChild(UiKit.MakeButton("QUIT", new Color("#4a4458"), 16, () => Game.QuitGame()).Place(560, y, 160, 40));

        AddChild(UiKit.MakeLabel($"{Main.Version}  -  art and sound: Pixel-boy (Superpowers) and Kenney, CC0", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4)
            .Place(0, 690, 1280, 20));
        Game.Music.Play("title");
        Game.Moment("title", 1.0);
    }

    void NewGame()
    {
        if (_save == null)
        {
            Game.NewGame();
            return;
        }
        // Starting over throws away the saved knight: ask first.
        var layer = new Node2D { ZIndex = 50 };
        AddChild(layer);
        layer.AddChild(UiKit.MakeShade());
        var panel = UiKit.MakePanel(Palette.Bad).Place(400, 260, 480, 180);
        layer.AddChild(panel);
        panel.AddChild(UiKit.MakeLabel("Start over?", 32, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 20, 480, 40));
        panel.AddChild(UiKit.MakeLabel("Your saved knight and all their loot will be gone.", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 70, 480, 20));
        panel.AddChild(UiKit.MakeButton("START OVER", Palette.Bad.Darkened(0.3f), 16, Game.NewGame).Place(60, 116, 170, 40));
        panel.AddChild(UiKit.MakeButton("KEEP PLAYING", Palette.Good.Darkened(0.3f), 16, layer.QueueFree).Place(250, 116, 170, 40));
    }

    public override void _Process(double delta)
    {
        // Lootfall: chests tumble out of the sky and bounce off the ground.
        _nextChest -= delta;
        if (_nextChest > 0) return;
        _nextChest = 0.18 + _rng.NextDouble() * 0.3;
        var kind = (ChestKind)(_rng.NextDouble() < 0.7 ? 0 : _rng.NextDouble() < 0.6 ? 1 : _rng.NextDouble() < 0.7 ? 2 : 3);
        var chest = new Sprite2D
        {
            Texture = GD.Load<Texture2D>($"res://assets/ui/chests/{Chests.Get(kind).Skin}.png"),
            Position = new Vector2((float)_rng.NextDouble() * 1280, -40), Scale = Vector2.One * (1 + (float)_rng.NextDouble() * 1.5f),
            Modulate = new Color(1, 1, 1, 0.85f),
        };
        _rain.AddChild(chest);
        float ground = 640 + (float)_rng.NextDouble() * 60;
        var t = chest.CreateTween();
        float fall = 1.6f + (float)_rng.NextDouble();
        t.TweenProperty(chest, "position:y", ground, fall).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        t.Parallel().TweenProperty(chest, "rotation", (float)(_rng.NextDouble() - 0.5) * 8, fall);
        t.TweenProperty(chest, "position:y", ground - 40, 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        t.TweenProperty(chest, "position:y", ground, 0.25f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        t.TweenProperty(chest, "modulate:a", 0f, 0.6f).SetDelay(0.4f);
        t.TweenCallback(Callable.From(chest.QueueFree));
    }
}
