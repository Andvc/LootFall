using System;
using Godot;

namespace LootFall.Ui;

/// <summary>Volumes (all, music, effects), fullscreen and screen shake, saved as they change.</summary>
public partial class SettingsView : Control
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        this.Place(0, 0, 1280, 720);
        AddChild(UiKit.MakeShade(0.5f));
        var panel = UiKit.MakePanel(Palette.PanelBorder).Place(400, 150, 480, 420);
        AddChild(panel);
        panel.AddChild(UiKit.MakeLabel("SETTINGS", 32, Palette.Text, HorizontalAlignment.Center, outline: 6).Place(0, 18, 480, 40));

        float y = 80;
        Slider(panel, "Volume", Settings.Volume, v => Settings.Volume = v, y);
        y += 56;
        Slider(panel, "Music", Settings.MusicVolume, v => Settings.MusicVolume = v, y);
        y += 56;
        Slider(panel, "Effects", Settings.SfxVolume, v => Settings.SfxVolume = v, y);
        y += 64;

        Button fullscreen = null;
        fullscreen = UiKit.MakeButton("", new Color("#4a4458"), 16, () =>
        {
            Settings.Fullscreen = !Settings.Fullscreen;
            Changed();
            fullscreen.Text = $"Fullscreen: {(Settings.Fullscreen ? "On" : "Off")}";
        }).Place(100, y, 280, 40);
        fullscreen.Text = $"Fullscreen: {(Settings.Fullscreen ? "On" : "Off")}";
        panel.AddChild(fullscreen);
        y += 52;
        Button shake = null;
        shake = UiKit.MakeButton("", new Color("#4a4458"), 16, () =>
        {
            Settings.Shake = Settings.Shake >= 1 ? 0.5f : Settings.Shake > 0 ? 0 : 1;
            Changed();
            shake.Text = $"Screen shake: {Settings.ShakeWord}";
        }).Place(100, y, 280, 40);
        shake.Text = $"Screen shake: {Settings.ShakeWord}";
        panel.AddChild(shake);

        panel.AddChild(UiKit.MakeButton("BACK", Palette.Good.Darkened(0.3f), 16, QueueFree).Place(170, 360, 140, 40));
    }

    static void Changed()
    {
        Settings.Apply();
        Settings.Save();
    }

    static void Slider(Control panel, string label, float value, Action<float> set, float y)
    {
        panel.AddChild(UiKit.MakeLabel(label, 16, Palette.Text).Place(40, y + 4, 120, 20));
        var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = value, FocusMode = FocusModeEnum.None }.Place(160, y, 260, 28);
        var track = new StyleBoxFlat { BgColor = new Color("#120e1a"), BorderColor = Palette.PanelBorder, AntiAliasing = false };
        track.SetBorderWidthAll(3);
        track.SetContentMarginAll(6);
        var fill = new StyleBoxFlat { BgColor = Palette.Good.Darkened(0.45f), AntiAliasing = false };
        fill.SetContentMarginAll(6);
        slider.AddThemeStyleboxOverride("slider", track);
        slider.AddThemeStyleboxOverride("grabber_area", fill);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        var coin = UiKit.Icon("gold");
        slider.AddThemeIconOverride("grabber", coin);
        slider.AddThemeIconOverride("grabber_highlight", coin);
        slider.ValueChanged += v =>
        {
            set((float)v);
            Changed();
        };
        panel.AddChild(slider);
    }
}
