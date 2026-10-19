using System;
using Godot;

namespace LootFall.Ui;

/// <summary>
/// Esc (or the Menu button) during a day: the game stops while it's up. Resume, the settings, back to the title
/// screen, or quit. The shop is saved at the start of each day, so leaving mid-day starts that day over.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    const float W = 420, H = 404, X = (1280 - W) / 2, Y = (720 - H) / 2;

    SettingsView _settings;

    public override void _Ready()
    {
        Layer = 60;
        ProcessMode = ProcessModeEnum.Always;
        GetTree().Paused = true;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, 1280, 720);
        AddChild(root);
        root.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.65f), MouseFilter = Control.MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920));
        root.AddChild(UiKit.MakePanel(Palette.Gold.Darkened(0.3f)).Place(X, Y, W, H));
        root.AddChild(UiKit.MakeLabel("PAUSED", 32, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(X, Y + 18, W, 40));
        float y = Y + 80;
        root.AddChild(UiKit.MakeButton("Resume", Palette.Gold, 24, Resume).Place(X + 60, y, W - 120, 52));
        root.AddChild(UiKit.MakeButton("Settings", Palette.Text, 16, OpenSettings).Place(X + 60, y + 66, W - 120, 44));
        root.AddChild(UiKit.MakeButton("Quit to title", Palette.Text, 16, () => Leave(toTitle: true)).Place(X + 60, y + 124, W - 120, 44));
        root.AddChild(UiKit.MakeButton("Quit game", Palette.Bad, 16, () => Leave(toTitle: false)).Place(X + 60, y + 182, W - 120, 44));
        root.AddChild(UiKit.MakeLabel("The shop is saved at the start of each day: leave now and today starts over.", 16, Palette.Muted,
            HorizontalAlignment.Center).Wrapped().Place(X + 24, y + 238, W - 48, 44));
        Main.Instance.Sfx.PlayGroup("book", 1.2f, -8);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _settings == null)
        {
            GetViewport().SetInputAsHandled();
            Resume();
        }
    }

    void OpenSettings()
    {
        if (_settings != null) return;
        _settings = new SettingsView();
        _settings.Closed += () => _settings = null;
        AddChild(_settings);
    }

    public void Resume()
    {
        GetTree().Paused = false;
        QueueFree();
    }

    void Leave(bool toTitle)
    {
        GetTree().Paused = false;
        QueueFree();
        if (toTitle) Main.Instance.ToTitle();
        else Main.Instance.QuitGame();
    }
}
