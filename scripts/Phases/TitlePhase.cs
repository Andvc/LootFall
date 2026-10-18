using System;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The title screen: your shop as you left it (or the roadside stall), a chest that won't keep still, and Continue,
/// New game, Settings and Quit. Starting over when there's a saved shop asks first.
/// </summary>
public partial class TitlePhase : Phase
{
    const float ButtonsX = 490, ButtonW = 300;

    readonly SaveData _save;
    ChestView _chest;
    float _time, _nextRattle = 2.5f;
    Control _confirm;
    SettingsView _settings;

    public TitlePhase(SaveData save = null)
    {
        _save = save;
    }

    public TitlePhase() { }

    public override void _Ready()
    {
        Add(UiKit.MakeShop(_save?.Tier ?? 1, 0.45f));
        Add(new Painter(p =>
        {
            // Darker bands behind the name and the buttons, in hard steps, so they read over any shop.
            p.DrawRect(new Rect2(0, 0, 1280, 720), new Color(0, 0, 0, 0.2f));
            for (int i = 0; i < 3; i++)
            {
                p.DrawRect(new Rect2(0, 60 + i * 8, 1280, 140 - i * 16), new Color(0, 0, 0, 0.18f));
                p.DrawRect(new Rect2(400 + i * 16, 360 + i * 8, 480 - i * 32, 260 - i * 16), new Color(0, 0, 0, 0.16f));
            }
        }));

        var logo = Add(UiKit.MakeLabel("LOOTFALL", 80, Palette.Gold, HorizontalAlignment.Center, outline: 10).Place(0, 64, 1280, 96));
        logo.PivotOffset = new Vector2(640, 48);
        Add(UiKit.MakeLabel("Shake it. Read the signs. Strike a deal. Pay the Guild.", 16, Palette.Text, HorizontalAlignment.Center, outline: 4)
            .Place(0, 164, 1280, 24));

        _chest = Add(new ChestView { Position = new Vector2(640, 300), PixelSize = 8, Skin = "purple", GlowColor = Palette.Of(Rarity.Legendary) });

        float y = 380;
        if (_save != null)
        {
            Add(UiKit.MakeButton("Continue", Palette.Gold, 24, () => Game.ContinueGame(_save)).Place(ButtonsX, y, ButtonW, 56));
            Add(UiKit.MakeLabel(_save.Summary, 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(ButtonsX, y + 60, ButtonW, 20));
            y += 92;
        }
        Add(UiKit.MakeButton("New game", _save == null ? Palette.Gold : Palette.Text, _save == null ? 24 : 16, NewGame)
            .Place(ButtonsX, y, ButtonW, _save == null ? 56 : 44));
        y += _save == null ? 70 : 56;
        Add(UiKit.MakeButton("Settings", Palette.Text, 16, OpenSettings).Place(ButtonsX, y, ButtonW, 44));
        y += 56;
        Add(UiKit.MakeButton("Quit", Palette.Text, 16, () => GetTree().Quit()).Place(ButtonsX, y, ButtonW, 44));

        Add(UiKit.MakeLabel(Main.Version, 16, Palette.Muted, outline: 4).Place(24, 684, 400, 20));
        if (Game.Profile?.Summary is string record)
            Add(UiKit.MakeLabel(record, 16, Palette.Gold.Darkened(0.15f), HorizontalAlignment.Center, outline: 4).Place(0, 196, 1280, 20));
        Add(UiKit.MakeLabel("Art: Pixel-boy.  Sounds and font: Kenney.  All CC0.", 16, Palette.Muted, HorizontalAlignment.Right, outline: 4)
            .Place(656, 684, 600, 20));
        Game.Moment("00_title", 1.0);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        // Every few seconds something inside stirs: a rumble, a crack of light, and it settles again.
        float since = _time - _nextRattle;
        float pulse = since is > 0 and < 1.2f ? MathF.Sin(since / 1.2f * MathF.PI) : 0f;
        _chest.Rumble = pulse * 0.6f;
        _chest.LidGap = pulse * 6f;
        _chest.Glow = 0.25f + pulse * 0.75f;
        if (since >= 1.2f)
        {
            _nextRattle = _time + 2.5f + (float)GD.RandRange(0, 2.5);
            Sfx.PlayGroup("jingle", 0.9f + (float)GD.RandRange(0, 0.3), -14);
        }
    }

    void NewGame()
    {
        if (_save == null)
        {
            Game.NewGame();
            return;
        }
        if (_confirm != null) return;
        // Starting over loses the saved shop: ask first.
        _confirm = Add(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, 1280, 720));
        _confirm.ZIndex = 20;
        _confirm.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920));
        _confirm.AddChild(UiKit.MakePanel(Palette.Bad).Place(390, 250, 500, 220));
        _confirm.AddChild(UiKit.MakeLabel("Start over?", 32, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(390, 272, 500, 40));
        _confirm.AddChild(UiKit.MakeLabel($"Your shop ({_save.Summary}) will be gone for good.", 16, Palette.Muted, HorizontalAlignment.Center)
            .Wrapped().Place(420, 320, 440, 44));
        _confirm.AddChild(UiKit.MakeButton("Start over", Palette.Bad, 16, Game.NewGame).Place(420, 392, 200, 48));
        _confirm.AddChild(UiKit.MakeButton("Keep my shop", Palette.Gold, 16, () =>
        {
            _confirm.QueueFree();
            _confirm = null;
        }).Place(660, 392, 200, 48));
    }

    void OpenSettings()
    {
        if (_settings != null) return;
        _settings = Add(new SettingsView());
        _settings.ZIndex = 30;
        _settings.Closed += () => _settings = null;
    }
}
