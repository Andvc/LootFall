using Godot;

namespace LootFall.Ui;

/// <summary>Esc or the HUD's Menu button: the game stops; resume, settings, back to the title, or quit (it saves first).</summary>
public partial class PauseMenu : CanvasLayer
{
    public override void _Ready()
    {
        Layer = 60;
        ProcessMode = ProcessModeEnum.Always;
        GetTree().Paused = true;
        AddChild(UiKit.MakeShade(0.6f));
        var panel = UiKit.MakePanel(Palette.Gold.Darkened(0.2f)).Place(480, 190, 320, 340);
        AddChild(panel);
        panel.AddChild(UiKit.MakeLabel("PAUSED", 32, Palette.Gold, HorizontalAlignment.Center, outline: 6).Place(0, 20, 320, 40));
        float y = 80;
        panel.AddChild(UiKit.MakeButton("RESUME", Palette.Good.Darkened(0.3f), 16, Close).Place(60, y, 200, 44));
        y += 58;
        panel.AddChild(UiKit.MakeButton("SETTINGS", new Color("#4a4458"), 16, () => AddChild(new SettingsView())).Place(60, y, 200, 44));
        y += 58;
        panel.AddChild(UiKit.MakeButton("TITLE SCREEN", new Color("#4a4458"), 16, () =>
        {
            Close();
            Main.Instance.ToTitle();
        }).Place(60, y, 200, 44));
        y += 58;
        panel.AddChild(UiKit.MakeButton("QUIT GAME", Palette.Bad.Darkened(0.35f), 16, () => Main.Instance.QuitGame()).Place(60, y, 200, 44));
        Main.Instance.Save();
    }

    void Close()
    {
        GetTree().Paused = false;
        QueueFree();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            if (GetChildren().Count > 0 && GetChild(GetChildCount() - 1) is SettingsView settings) settings.QueueFree();
            else Close();
        }
    }
}
