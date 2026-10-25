using System;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>The pause menu (Esc): the game stops under it.</summary>
public partial class PauseMenu : Overlay
{
    public PauseMenu(GameScreen g) : base(g, 0.7f)
    {
        ProcessMode = ProcessModeEnum.Always;
        var p = Ui.Box(new Color("#14101c"), Look.EdgeLight, 14).Place(Look.W / 2 - 220, 250, 440, 520);
        p.MouseFilter = MouseFilterEnum.Stop;
        AddChild(p);
        p.AddChild(Ui.Text("PAUSED", Look.Title, Look.Gold, Look.Display, HorizontalAlignment.Center).Place(0, 30, 440, 64));
        float y = 130;
        Button B(string text, Action a)
        {
            var b = Ui.Button(text, a, size: Look.Sub).Place(60, y, 320, 64);
            b.ProcessMode = ProcessModeEnum.Always;
            p.AddChild(b);
            y += 80;
            return b;
        }
        B("Resume", Close);
        B("Settings", () => { Close(); g.Open(new SettingsView(g)); });
        B("Save and quit to title", () => { Unpause(); Main.Instance?.ToTitle(); });
        B("Quit the game", () => { Unpause(); Main.Instance?.QuitGame(); });
        p.AddChild(Ui.Text(Main.Version, Look.Small, Look.Dim, Look.Ui, HorizontalAlignment.Center).Place(0, 470, 440, 30));
    }

    public override void _Ready()
    {
        GetTree().Paused = true;
        Main.Instance?.Save();
        Main.Instance?.Moment("pause", 0.3);
    }

    void Unpause()
    {
        if (IsInsideTree()) GetTree().Paused = false;
    }

    public override void Close()
    {
        Unpause();
        base.Close();
    }
}

/// <summary>Settings: volumes, fullscreen, shake, flashes, damage numbers and the notation, kept in user://settings.cfg.</summary>
public partial class SettingsView : Overlay
{
    readonly Panel _p;
    float _y = 100;

    public SettingsView(GameScreen g) : base(g, 0.7f)
    {
        _p = Frame("Settings", Look.W / 2 - 420, 140, 840, 800);
        Slider("Master volume", Settings.Volume, v => Settings.Volume = v);
        Slider("Music", Settings.MusicVolume, v => Settings.MusicVolume = v);
        Slider("Effects", Settings.SfxVolume, v => Settings.SfxVolume = v);
        Choice("Screen shake", () => Settings.Level(Settings.Shake), () => Settings.Shake = Next(Settings.Shake));
        Choice("Flashes", () => Settings.Level(Settings.Flashes), () => Settings.Flashes = Next(Settings.Flashes));
        Choice("Damage numbers", () => Settings.DamageNumbers ? "On" : "Off", () =>
        {
            Settings.DamageNumbers = !Settings.DamageNumbers;
            if (g != null) g.Battle.Numbers.Enabled = Settings.DamageNumbers;
        });
        Choice("Numbers", () => Settings.Notation switch
        {
            Notation.Mixed => "1.23M, 4.56Dc, then 7.89e45",
            Notation.Letters => "1.23M, then 4.56aa",
            Notation.Scientific => "1.23e6",
            _ => "1.23e6, 456e33",
        }, () => Settings.Notation = (Notation)(((int)Settings.Notation + 1) % 4));
        Choice("Window", () => Settings.Fullscreen ? "Fullscreen" : "Windowed", () => Settings.Fullscreen = !Settings.Fullscreen);
    }

    static float Next(float v) => v >= 1f ? 0.5f : v > 0f ? 0f : 1f;

    void Slider(string name, float value, Action<float> set)
    {
        _p.AddChild(Ui.Text(name, Look.Body, Look.Text, Look.UiBold).Place(40, _y + 10, 300, 40));
        var s = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = value, FocusMode = FocusModeEnum.None }.Place(360, _y + 16, 420, 30);
        s.ValueChanged += v =>
        {
            set((float)v);
            Settings.Apply();
            Settings.Save();
        };
        _p.AddChild(s);
        _y += 74;
    }

    void Choice(string name, Func<string> show, Action next)
    {
        _p.AddChild(Ui.Text(name, Look.Body, Look.Text, Look.UiBold).Place(40, _y + 10, 300, 40));
        Button b = null;
        b = Ui.Button(show(), () =>
        {
            next();
            b.Text = show();
            Settings.Apply();
            Settings.Save();
        }, size: Look.Body).Place(360, _y + 4, 420, 54);
        _p.AddChild(b);
        _y += 74;
    }

    public override void _Ready() => Main.Instance?.Moment("settings", 0.3);
}

/// <summary>
/// Back after time away (docs/art-direction.md, "time away"): how long, how far the knight went, what he found, and the
/// best finds shown as cards; "Nothing happened" when that's the truth.
/// </summary>
public partial class WelcomeBack : Overlay
{
    public WelcomeBack(GameScreen g, AwaySummary a) : base(g, 0.75f)
    {
        var p = Frame("While you were away", Look.W / 2 - 560, 150, 1120, 720);
        p.AddChild(Ui.Text($"for {Format.Time(a.Seconds)}", Look.Sub, Look.Muted, Look.UiBold).Place(40, 70, 800, 40));
        if (a.Chests == 0 && a.DepthAfter == a.DepthBefore && a.Levels == 0)
        {
            p.AddChild(Ui.Text("Nothing happened.", Look.Header, Look.Text, Look.Display, HorizontalAlignment.Center).Place(0, 300, 1120, 60));
            return;
        }
        var lines = new[]
        {
            ("Depth", $"{Format.Whole(a.DepthBefore)}  >  {Format.Whole(a.DepthAfter)}", Look.Text),
            ("Deepest ever", $"{Format.Whole(a.RecordBefore)}  >  {Format.Whole(a.RecordAfter)}", a.RecordAfter > a.RecordBefore ? Look.Good : Look.Text),
            ("Monsters slain", Format.Num(a.Kills), Look.Text),
            ("Gold", "+" + Format.Num(a.Gold), Look.Gold),
            ("Chests opened", Format.Int(a.Chests), Look.Text),
            ("Upgrades put on", Format.Int(a.Equipped), Look.Good),
            ("Levels", "+" + a.Levels, Look.Xp),
            ("First finds", Format.Int(a.NewEntries), Look.Gold),
        };
        float y = 130;
        foreach (var (name, value, colour) in lines)
        {
            p.AddChild(Ui.Text(name, Look.Body, Look.Muted, Look.UiBold).Place(60, y, 300, 34));
            p.AddChild(Ui.Text(value, Look.Body, colour, Look.Numbers).Place(330, y, 300, 34));
            y += 40;
        }
        if (a.Ascensions > 0) p.AddChild(Ui.Text($"Ascended {a.Ascensions} times", Look.Body, Look.Aether, Look.UiBold).Place(60, y + 6, 400, 34));
        // The best finds as cards.
        p.AddChild(Ui.Text("BEST FINDS", 20, Look.Muted, Look.UiBold).Place(660, 130, 300, 30));
        var best = a.Best.Take(6).ToList();
        for (int i = 0; i < best.Count; i++)
        {
            var c = best[i];
            var family = Describe.Family(g.S.C.Slots[c.Slot].Name);
            float t = 0;
            var tile = new Canvas(q => ItemTile.Draw(q, new Rect2(0, 0, 120, 120), family, c.Item.Rarity, c.Item.Affixes, c.New, c.Equipped, time: t += 0.016f))
                .Place(660 + (i % 3) * 140, 170 + (i / 3) * 190, 120, 120);
            tile.TooltipText = Describe.Name(g.S.C, c.Slot, c.Item);
            tile.MouseFilter = MouseFilterEnum.Pass;
            p.AddChild(tile);
            p.AddChild(Ui.Text(Look.TierName(c.Item.Rarity), 16, Look.TierText(c.Item.Rarity), Look.Strong, HorizontalAlignment.Center).Place(650 + (i % 3) * 140, 296 + (i / 3) * 190, 140, 24));
        }
        p.AddChild(Ui.Button("Back to it", Close, Look.Gold, Look.Sub, Look.Strong).Place(560 - 150, 620, 300, 70));
    }

    public override void _Ready() => Main.Instance?.Moment("welcome", 0.5);
}
