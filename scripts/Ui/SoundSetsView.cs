using System;
using System.Collections.Generic;
using Godot;
using LootFall.Audio;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// Settings > Item sounds: for each kind of thing, which sound it makes when shaken. Click a kind's choice to cycle
/// through the options (it plays as it changes), or Play to hear it again. Saved with the settings when it closes.
/// </summary>
public partial class SoundSetsView : Control
{
    const float W = 760, H = 500, X = (1280 - W) / 2, Y = (720 - H) / 2;

    public event Action Closed;

    readonly Dictionary<Category, Button> _choices = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920));
        AddChild(UiKit.MakePanel(Palette.Gold.Darkened(0.3f)).Place(X, Y, W, H));
        AddChild(UiKit.MakeLabel("ITEM SOUNDS", 32, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(X, Y + 16, W, 40));
        AddChild(UiKit.MakeLabel("What each kind of thing sounds like in a shaken chest. Click a choice to try the next one.", 16, Palette.Muted,
            HorizontalAlignment.Center).Place(X, Y + 60, W, 20));

        float row = Y + 96;
        foreach (var def in Categories.All)
        {
            var kind = def.Category;
            AddChild(UiKit.MakeKindIcon(kind, new Vector2(X + 52, row + 18)));
            AddChild(UiKit.MakeLabel($"{def.Name} ({def.Word})", 16, Palette.Text).Place(X + 80, row + 8, 220, 20));
            var choice = UiKit.MakeButton("", Palette.PanelBorder.Lightened(0.2f), 16, () => Cycle(kind)).Place(X + 300, row, 300, 36);
            AddChild(choice);
            _choices[kind] = choice;
            AddChild(UiKit.MakeButton("Play", Palette.Gold, 16, () => Play(kind)).Place(X + 616, row, 104, 36));
            row += 52;
        }
        AddChild(UiKit.MakeButton("Done", Palette.Gold, 24, Close).Place(X + W / 2 - 100, Y + H - 68, 200, 48));
        Refresh();
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.12f);
    }

    void Refresh()
    {
        foreach (var (kind, button) in _choices)
        {
            var options = SoundSets.For(kind);
            button.Text = $"{SoundSets.Chosen(kind).Name}  ({Math.Clamp(Settings.SoundSet(kind), 0, options.Count - 1) + 1}/{options.Count})";
        }
    }

    /// <summary>The next option for this kind, played straight away.</summary>
    public void Cycle(Category kind)
    {
        int count = SoundSets.For(kind).Count;
        Settings.SetSoundSet(kind, (Settings.SoundSet(kind) + 1) % count);
        Refresh();
        Play(kind);
    }

    void Play(Category kind) => Main.Instance?.Sfx?.PlayGroup(SoundSets.Group(kind), Categories.Get(kind).Pitch, 2f);

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void Close()
    {
        Settings.Save();
        Closed?.Invoke();
        QueueFree();
    }
}
