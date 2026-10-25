using System;
using System.Globalization;
using System.Linq;
using Godot;
using LootFall.Audio;
using LootFall.Game;
using LootFall.View;

namespace LootFall;

/// <summary>
/// Root of the game: owns the session, the sound and music, saving and the screen-wide effects, and swaps between the
/// title and the game. Saves to user://save.json every half minute, at pause and on quitting (written whole to a side
/// file and swapped in, the last save kept as save.bak); time away is played on Continue.
/// Flags (after `--`): --autoplay (the bot plays; nothing is saved) --minutes=N --speed=N --seed=N --hours=N (start where
/// a sensible player would be after N hours) --shots=DIR (a screenshot of each named moment, the first time) --demo=NAME
/// (opening, title, welcome, screens: see <see cref="Demos"/>) --export-sounds=DIR.
/// </summary>
public partial class Main : Node2D
{
    public static Main Instance { get; private set; }

    public const string Version = "Prototype 0.2";
    const string SavePath = "user://save.json", BackupPath = "user://save.bak", TempPath = "user://save.tmp";

    /// <summary>There's a save this build couldn't read: starting over asks first.</summary>
    public static bool SaveUnreadable { get; private set; }

    public Session Session { get; private set; }
    public int Seed { get; private set; }
    public Sfx Sfx { get; private set; }
    public Music Music { get; private set; }
    public Bot Bot { get; private set; }
    public Control Screen { get; private set; }
    public string ShotsDir { get; private set; }

    ColorRect _flash, _fade;
    CanvasLayer _overlay;
    double _autosave;
    bool _saving, _quitting;

    public GameScreen Game => Screen as GameScreen;

    public override void _Ready()
    {
        Instance = this;
        var args = OS.GetCmdlineUserArgs();
        GetTree().AutoAcceptQuit = false;
        Settings.Load();
        GetTree().Root.Theme = MakeTheme();
        Seed = int.TryParse(Arg(args, "--seed"), out var s) ? s : (int)(Time.GetUnixTimeFromSystem() % 1_000_000);
        GD.Print($"LootFall {Version}, seed {Seed}");

        Sfx = new Sfx();
        AddChild(Sfx);
        Music = new Music();
        AddChild(Music);
        if (Arg(args, "--export-sounds") is string soundDir)
        {
            Sfx.Export(soundDir);
            GD.Print($"Exported synthesized sounds to {soundDir}");
            QuitGame();
            return;
        }

        _overlay = new CanvasLayer { Layer = 50 };
        AddChild(_overlay);
        _flash = new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, Look.W, Look.H);
        _fade = new ColorRect { Color = new Color(Look.Bg, 0f), MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, Look.W, Look.H);
        _overlay.AddChild(_flash);
        _overlay.AddChild(_fade);

        ShotsDir = Arg(args, "--shots");
        if (ShotsDir != null) DirAccess.MakeDirRecursiveAbsolute(ShotsDir);

        if (args.Contains("--autoplay"))
        {
            Bot = new Bot
            {
                Minutes = Num(Arg(args, "--minutes"), 5),
                Speed = Num(Arg(args, "--speed"), 1),
            };
            AddChild(Bot);
        }

        double hours = Num(Arg(args, "--hours"), 0);
        if (Arg(args, "--demo") is string demo) Demos.Run(this, demo, hours);
        else if (Bot != null || hours > 0) StartPlaying(FastForward(Seed, hours), null);
        else ToTitle();
    }

    static double Num(string s, double fallback) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    /// <summary>A new game played by the sensible player for <paramref name="hours"/> (the bot's head start, the
    /// screenshots of later screens).</summary>
    public static Session FastForward(int seed, double hours)
    {
        var s = Session.NewGame(seed);
        if (hours <= 0) return s;
        SensiblePlayer.Play(s, hours * 3600);
        GD.Print($"Fast-forwarded {hours:0.##}h: depth {s.H.RunDepth}, record {s.H.EverDepth}, level {s.H.Level}, " +
                 $"{s.H.Ascension.Resets} ascensions, {s.H.Transcendence.Resets} transcendences");
        return s;
    }

    // ------------------------------------------------------------------------------------------------ screens

    public void ToTitle()
    {
        if (_saving) Save();
        _saving = false;
        Session = null;
        SwitchTo(new TitleScreen(ReadSave()));
        Music.Play("title");
    }

    public void NewGame()
    {
        _saving = true;
        StartPlaying(Session.NewGame(Seed++), null);
        Save();
    }

    public void Continue(SaveData save)
    {
        var session = SaveGame.Restore(save);
        _saving = true;
        double away = Math.Max(0, Time.GetUnixTimeFromSystem() - save.SavedAt);
        AwaySummary summary = null;
        // Away for a minute or more: the time away is played (up to a week), and shown.
        if (save.SavedAt > 0 && away >= 60) summary = session.CatchUp(Math.Min(away, 7 * 86400));
        StartPlaying(session, summary);
        Save();
    }

    public void StartPlaying(Session session, AwaySummary welcome)
    {
        Session = session;
        _autosave = 30;
        SwitchTo(new GameScreen(session, welcome));
    }

    public async void SwitchTo(Control next)
    {
        if (Screen != null)
        {
            var fadeOut = CreateTween();
            fadeOut.TweenProperty(_fade, "color:a", 1f, 0.2f);
            await ToSignal(fadeOut, Tween.SignalName.Finished);
            Screen.QueueFree();
        }
        Screen = next;
        AddChild(next);
        MoveChild(next, 0);
        CreateTween().TweenProperty(_fade, "color:a", 0f, 0.25f);
        Bot?.OnScreen(next);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            if (Game != null && !Game.CloseOverlay()) Game.OpenPause();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitGame();
    }

    // ------------------------------------------------------------------------------------------------ saving

    public static SaveData ReadSave()
    {
        var data = ReadSaveFile(SavePath) ?? ReadSaveFile(BackupPath);
        SaveUnreadable = data == null && (FileAccess.FileExists(SavePath) || FileAccess.FileExists(BackupPath));
        return data;
    }

    static SaveData ReadSaveFile(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;
        try
        {
            var d = SaveGame.FromJson(file.GetAsText());
            return d != null && d.Version <= SaveData.CurrentVersion ? d : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Writes the game (a real one only; not the bot's or a demo's): whole to a side file, then swapped in with
    /// the last save kept as save.bak, so a crash mid-write never leaves half a save.</summary>
    public void Save()
    {
        if (!_saving || Session == null) return;
        var data = SaveGame.Capture(Session, Seed, (long)Time.GetUnixTimeFromSystem());
        using (var file = FileAccess.Open(TempPath, FileAccess.ModeFlags.Write))
        {
            if (file == null) return;
            bool written = file.StoreString(SaveGame.ToJson(data));
            file.Close();
            if (!written) return;
        }
        if (FileAccess.FileExists(SavePath)) DirAccess.RenameAbsolute(SavePath, BackupPath);
        DirAccess.RenameAbsolute(TempPath, SavePath);
    }

    public bool Saving => _saving;

    /// <summary>Quits cleanly: saves, lets go of the screen, the bot and the sounds, gives the audio thread a moment, and
    /// collects what the managed side still holds, so nothing is left alive when the engine shuts down.</summary>
    public async void QuitGame()
    {
        if (_quitting) return;
        _quitting = true;
        Save();
        GetTree().Paused = false;
        Engine.TimeScale = 1;
        Screen?.QueueFree();
        Screen = null;
        Bot?.QueueFree();
        Bot = null;
        Music?.Stop();
        Sfx?.Release();
        await ToSignal(GetTree().CreateTimer(0.25, true, false, true), SceneTreeTimer.SignalName.Timeout);
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Root.Theme = null;
        Look.Release();
        Assets.Release();
        Fx.Release();
        MonsterActor.Release();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit();
    }

    // ------------------------------------------------------------------------------------------------ effects

    /// <summary>A full-screen flash, scaled by the Flashes setting and capped at 35% (docs/art-direction.md, "Feel").</summary>
    public void Flash(Color color, float alpha, float duration = 0.35f)
    {
        alpha = Mathf.Min(0.35f, alpha) * Settings.Flashes;
        if (alpha <= 0.001f) return;
        _flash.Color = color with { A = alpha };
        CreateTween().TweenProperty(_flash, "color:a", 0f, duration).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Saves a screenshot the first time a moment happens, when running with --shots.</summary>
    public async void Moment(string name, double delay = 0)
    {
        if (ShotsDir == null) return;
        if (delay > 0) await ToSignal(GetTree().CreateTimer(delay, true, false, true), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = $"{ShotsDir}/{name}.png";
        if (FileAccess.FileExists(path)) return;
        GetViewport().GetTexture().GetImage().SavePng(path);
    }

    public override void _Process(double delta)
    {
        if (_saving && Game != null && !GetTree().Paused && (_autosave -= delta) <= 0)
        {
            _autosave = 30;
            Save();
        }
    }

    /// <summary>The look of what Godot draws itself (tooltips): a framed panel and the UI font.</summary>
    static Theme MakeTheme()
    {
        var theme = new Theme { DefaultFont = Look.Ui, DefaultFontSize = Look.Body };
        theme.SetStylebox("panel", "TooltipPanel", Ui.Frame(new Color("#100c17"), Look.EdgeLight, 8, 2, 6, 12));
        theme.SetColor("font_color", "TooltipLabel", Look.Text);
        theme.SetFontSize("font_size", "TooltipLabel", Look.Small);
        return theme;
    }

    static string Arg(string[] args, string name) =>
        args.FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..];
}
