using System;
using Godot;

namespace LootFall.Ui;

/// <summary>
/// The settings, over whatever screen you're on (it keeps working while the game is paused): volume, fullscreen,
/// screen shake and flashes, and Uncle Bram's tips. Changes apply at once and are saved when it closes.
/// </summary>
public partial class SettingsView : Control
{
    const float W = 640, H = 380, X = (1280 - W) / 2, Y = (720 - H) / 2, ControlX = X + 320;

    public event Action Closed;

    Button _fullscreen, _shake, _tips;
    Label _volume;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var blocker = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920);
        AddChild(blocker);
        AddChild(UiKit.MakePanel(Palette.Gold.Darkened(0.3f)).Place(X, Y, W, H));
        AddChild(UiKit.MakeLabel("SETTINGS", 32, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(X, Y + 16, W, 40));

        float row = Y + 84;
        Row("Volume", row);
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, Value = Settings.Volume * 100, FocusMode = FocusModeEnum.None };
        UiKit.StyleSlider(slider);
        slider.ValueChanged += v =>
        {
            Settings.Volume = (float)v / 100f;
            Settings.Apply();
            Refresh();
        };
        AddChild(slider.Place(ControlX, row + 2, 200, 24));
        _volume = UiKit.MakeLabel("", 16, Palette.Text, HorizontalAlignment.Right).Place(ControlX + 206, row + 4, 72, 20);
        AddChild(_volume);

        row += 56;
        Row("Fullscreen", row);
        _fullscreen = Put(Toggle(row, () =>
        {
            Settings.Fullscreen = !Settings.Fullscreen;
            Settings.Apply();
        }));

        row += 56;
        Row("Screen shake and flashes", row);
        _shake = Put(Toggle(row, () => Settings.Shake = Settings.Shake >= 1f ? 0.5f : Settings.Shake > 0f ? 0f : 1f));

        row += 56;
        Row("Uncle Bram's tips", row);
        _tips = Put(Toggle(row, () =>
        {
            Settings.Tips = !Settings.Tips;
            if (Main.Instance.State != null) Main.Instance.State.Guided = Settings.Tips;
        }));

        AddChild(UiKit.MakeButton("Done", Palette.Gold, 24, Close).Place(X + W / 2 - 100, Y + H - 68, 200, 48));
        Refresh();
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.12f);
    }

    void Row(string text, float y) => AddChild(UiKit.MakeLabel(text, 16, Palette.Text).Place(X + 40, y + 4, 270, 20));

    Button Toggle(float y, Action change) => UiKit.MakeButton("", Palette.PanelBorder.Lightened(0.2f), 16, () =>
    {
        change();
        Refresh();
    }).Place(ControlX, y - 4, 200, 36);

    T Put<T>(T node) where T : Node
    {
        AddChild(node);
        return node;
    }

    void Refresh()
    {
        _volume.Text = $"{Mathf.RoundToInt(Settings.Volume * 100)}%";
        _fullscreen.Text = Settings.Fullscreen ? "On" : "Off";
        _shake.Text = Settings.ShakeWord;
        _tips.Text = Settings.Tips ? "On" : "Off";
    }

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
