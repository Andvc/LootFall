using Godot;
using LootFall.Game;

namespace LootFall.View;

/// <summary>The title: the knight before Greenwood, the name, and Continue (with how far the saved knight got), New Game,
/// Settings and Quit. A save this build can't read is never overwritten without asking.</summary>
public partial class TitleScreen : Control
{
    readonly SaveData _save;
    readonly HeroRig _hero;
    readonly Backdrop _backdrop;
    float _scroll;
    bool _confirmNew;
    Button _new;

    public TitleScreen(SaveData save)
    {
        _save = save;
        this.Place(0, 0, Look.W, Look.H);
        _backdrop = new Backdrop();
        _backdrop.SetDepth(1);
        AddChild(_backdrop);
        AddChild(Ui.Gradient(new Color(Look.Bg, 0f), new Color(Look.Bg, 1f), 0, 380, Look.W, 280));
        AddChild(new ColorRect { Color = Look.Bg, MouseFilter = MouseFilterEnum.Ignore }.Place(0, 640, Look.W, 440));
        _hero = new HeroRig { Position = new Vector2(560, Look.Ground), Scale = Vector2.One * 1.25f, ZIndex = 12 };
        AddChild(_hero);
        var title = Ui.Text("LOOTFALL", 120, Look.Gold, Look.Display, HorizontalAlignment.Center, 18).Place(0, 120, Look.W, 150);
        AddChild(title);
        AddChild(Ui.Text("Deeper. Richer. Forever.", Look.Sub, Look.Text, Look.UiBold, HorizontalAlignment.Center, 6).Place(0, 270, Look.W, 40));

        float y = 680;
        if (save != null)
        {
            var b = Ui.Button("Continue", () => Main.Instance?.Continue(save), Look.Gold, Look.Header, Look.Strong).Place(Look.W / 2 - 220, y, 440, 84);
            AddChild(b);
            AddChild(Ui.Text($"Deepest {Format.Whole(save.EverDepth)}  |  level {save.Level}", Look.Body, Look.Muted, Look.Numbers, HorizontalAlignment.Center).Place(0, y + 90, Look.W, 30));
            y += 130;
        }
        _new = Ui.Button("New game", NewGame, size: Look.Sub).Place(Look.W / 2 - 180, y, 360, 64);
        AddChild(_new);
        y += 78;
        AddChild(Ui.Button("Settings", () => AddChild(new SettingsView(null)), size: Look.Sub).Place(Look.W / 2 - 180, y, 360, 64));
        y += 78;
        AddChild(Ui.Button("Quit", () => Main.Instance?.QuitGame(), size: Look.Sub).Place(Look.W / 2 - 180, y, 360, 64));
        AddChild(Ui.Text(Main.Version, Look.Small, Look.Dim, Look.Ui).Place(24, Look.H - 40, 400, 30));
        if (Main.SaveUnreadable)
            AddChild(Ui.Text("There's a save this version can't read. Starting a new game will ask before replacing it.", Look.Small, Look.Bad, Look.Ui, HorizontalAlignment.Center).Place(0, Look.H - 80, Look.W, 30));
    }

    void NewGame()
    {
        if ((_save != null || Main.SaveUnreadable) && !_confirmNew)
        {
            _confirmNew = true;
            _new.Text = "Replace the saved game?";
            Ui.Style(_new, Look.Bad, Look.Sub);
            return;
        }
        Main.Instance?.NewGame();
    }

    public override void _Process(double delta)
    {
        _scroll += (float)delta * 60;
        _backdrop.Scroll((float)delta * 60);
        _hero.SetWalk(0);
    }
}
