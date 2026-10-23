using System;
using System.Globalization;
using System.Linq;
using Godot;
using LootFall.Audio;
using LootFall.Core;
using LootFall.Debug;
using LootFall.Ui;

namespace LootFall;

/// <summary>
/// Root of the game: owns the game state, sound, music and screen effects, and swaps between the title and the game.
/// The knight is saved to user://save.json every half minute, on reaching a stage, at pause and on quitting; time away
/// is paid out on Continue.
/// Command-line flags (after `--`): --sim [--n=N] [--hours=H] (balance report), --autoplay (the bot plays; nothing is
/// saved), --seed=N, --stage=N (start at a stage with the gear a sensible player would have by then), --minutes=N (the
/// bot stops after N minutes of play), --speed=N (game speed for the bot), --shots=DIR (screenshot each named moment
/// the first time it happens), --opening-demo (opens one chest of each rarity, then quits), --title-demo (title,
/// settings, a new game, the pause menu), --export-sounds=DIR.
/// </summary>
public partial class Main : Node2D
{
    public static Main Instance { get; private set; }

    public const string Version = "Prototype 0.1";
    const string SavePath = "user://save.json";

    public GameState State { get; private set; }
    public Sfx Sfx { get; private set; }
    public Music Music { get; private set; }

    readonly Random _rng = new();
    Camera2D _camera;
    ColorRect _flash, _fade;
    Screen _screen;
    Autoplay _bot;
    string _shotsDir;
    float _trauma;
    int _seed;
    double _autosave;
    bool _quitting;

    /// <summary>A real game (not the bot or a debugging shortcut), so the knight is saved.</summary>
    bool _saving;

    public PlayScreen Play => _screen as PlayScreen;

    public override void _Ready()
    {
        Instance = this;
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--sim"))
        {
            int code = 0;
            try
            {
                Simulation.Report(int.Parse(Arg(args, "--n") ?? "12"), double.Parse(Arg(args, "--hours") ?? "6", CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                GD.PrintErr(e.ToString());
                code = 1;
            }
            GetTree().Quit(code);
            return;
        }

        GetTree().AutoAcceptQuit = false;
        Settings.Load();
        GetTree().Root.Theme = MakeTheme();
        _seed = int.TryParse(Arg(args, "--seed"), out var s) ? s : (int)(Time.GetUnixTimeFromSystem() % 1_000_000);
        GD.Print($"LootFall seed {_seed}");

        _camera = new Camera2D { Position = new Vector2(640, 360) };
        AddChild(_camera);
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

        var overlay = new CanvasLayer { Layer = 50 };
        AddChild(overlay);
        _flash = new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Ignore };
        _flash.Size = new Vector2(1280, 720);
        _fade = new ColorRect { Color = new Color(Palette.Bg, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.Size = new Vector2(1280, 720);
        overlay.AddChild(_flash);
        overlay.AddChild(_fade);

        _shotsDir = Arg(args, "--shots");
        if (_shotsDir != null) DirAccess.MakeDirRecursiveAbsolute(_shotsDir);

        if (args.Contains("--autoplay"))
        {
            _bot = new Autoplay
            {
                Minutes = double.TryParse(Arg(args, "--minutes"), NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 5,
                Speed = double.TryParse(Arg(args, "--speed"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sp) ? sp : 1,
            };
            AddChild(_bot);
        }

        int stage = int.TryParse(Arg(args, "--stage"), out var st) ? st : 0;
        if (args.Contains("--title-demo")) TitleDemo();
        else if (args.Contains("--opening-demo")) OpeningDemo();
        else if (_bot != null || stage > 1)
        {
            State = stage > 1 ? Simulation.FastForward(_seed, stage) : GameState.New(_seed);
            if (stage > 1) GD.Print($"Fast-forwarded to {World.StageName(State.MaxStage)}, chest level {State.ChestLevel}");
            State.CollectEvents = true;
            StartPlaying(null);
        }
        else ToTitle();
    }

    // ------------------------------------------------------------------------------------------------- screens

    public void ToTitle()
    {
        if (_saving) Save();
        _saving = false;
        SwitchTo(new TitleScreen(ReadSave()));
    }

    public void NewGame()
    {
        State = GameState.New(_seed++);
        _saving = true;
        Save();
        StartPlaying(null);
    }

    public void Continue(SaveData save)
    {
        State = GameState.Load(save);
        _saving = true;
        double away = Time.GetUnixTimeFromSystem() - save.SavedAt;
        var report = save.SavedAt > 0 ? State.ApplyOffline(away) : null;
        Save();
        StartPlaying(report);
    }

    void StartPlaying(OfflineReport welcome)
    {
        _autosave = 30;
        SwitchTo(new PlayScreen(State, welcome));
    }

    public async void SwitchTo(Screen next)
    {
        if (_screen != null)
        {
            var fadeOut = CreateTween();
            fadeOut.TweenProperty(_fade, "color:a", 1f, 0.2f);
            await ToSignal(fadeOut, Tween.SignalName.Finished);
            _screen.QueueFree();
        }
        _screen = next;
        AddChild(next);
        MoveChild(next, 1);
        CreateTween().TweenProperty(_fade, "color:a", 0f, 0.25f);
        _bot?.OnScreen(next);
    }

    /// <summary>Esc, or the HUD's Menu button: pause (only in the game).</summary>
    public void OpenPause()
    {
        if (_screen is not PlayScreen || GetTree().Paused) return;
        AddChild(new PauseMenu());
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            OpenPause();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitGame();
    }

    // ------------------------------------------------------------------------------------------------- saving

    public static SaveData ReadSave()
    {
        if (!FileAccess.FileExists(SavePath)) return null;
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        return file == null ? null : SaveData.FromJson(file.GetAsText());
    }

    /// <summary>Writes the knight to user://save.json (a real game only).</summary>
    public void Save()
    {
        if (!_saving || State == null) return;
        var data = State.Save();
        data.SavedAt = Time.GetUnixTimeFromSystem();
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(data.ToJson());
    }

    /// <summary>
    /// Quits cleanly: saves, lets go of the screen and the bot first, gives anything waiting on them a frame or two,
    /// and collects what the managed side still holds, so the engine doesn't shut down around live objects.
    /// </summary>
    public async void QuitGame()
    {
        if (_quitting) return;
        _quitting = true;
        Save();
        GetTree().Paused = false;
        Engine.TimeScale = 1;
        _screen?.QueueFree();
        _screen = null;
        _bot?.QueueFree();
        _bot = null;
        Music?.Stop();
        Sfx?.Release();
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit();
    }

    // ------------------------------------------------------------------------------------------------- effects

    /// <summary>Adds screen-shake "trauma" (0..1). Shake strength is trauma squared, so small hits stay subtle.</summary>
    public void Shake(float amount) => _trauma = Mathf.Min(1f, _trauma + amount * Settings.Shake);

    public void Flash(Color color, float alpha, float duration = 0.35f)
    {
        _flash.Color = color with { A = alpha * Settings.Shake };
        CreateTween().TweenProperty(_flash, "color:a", 0f, duration).SetEase(Tween.EaseType.Out);
    }

    double _hitStopUntil;

    /// <summary>Freezes time for a moment on impact, which sells the weight of a hit.</summary>
    public async void HitStop(double seconds)
    {
        if (Settings.Shake <= 0) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _hitStopUntil) return;
        _hitStopUntil = now + seconds;
        double before = Engine.TimeScale;
        Engine.TimeScale = 0.05;
        await ToSignal(GetTree().CreateTimer(seconds, true, false, true), SceneTreeTimer.SignalName.Timeout);
        Engine.TimeScale = _bot?.Speed ?? 1.0;
    }

    /// <summary>Saves a screenshot the first time a moment happens, when running with --shots. No-op otherwise.</summary>
    public async void Moment(string name, double delay = 0)
    {
        if (_shotsDir == null) return;
        if (delay > 0) await ToSignal(GetTree().CreateTimer(delay, true, false, true), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = $"{_shotsDir}/{name}.png";
        if (FileAccess.FileExists(path)) return; // keep the first occurrence of each moment
        GetViewport().GetTexture().GetImage().SavePng(path);
    }

    public override void _Process(double delta)
    {
        if (_camera == null) return;
        _trauma = Mathf.Max(0f, _trauma - (float)delta * 1.8f);
        float strength = _trauma * _trauma;
        _camera.Offset = new Vector2(Rand(), Rand()) * 22f * strength;
        if (_saving && _screen is PlayScreen && !GetTree().Paused && (_autosave -= delta) <= 0)
        {
            _autosave = 30;
            Save();
        }
    }

    float Rand() => (float)(_rng.NextDouble() * 2 - 1);

    /// <summary>The look of things Godot draws itself, like tooltips: a framed panel and the pixel font.</summary>
    static Theme MakeTheme()
    {
        var theme = new Theme { DefaultFont = UiKit.Font, DefaultFontSize = 16 };
        theme.SetStylebox("panel", "TooltipPanel", UiKit.PanelBox(10));
        theme.SetColor("font_color", "TooltipLabel", Palette.Text);
        return theme;
    }

    // ------------------------------------------------------------------------------------------------- demos

    /// <summary>Opens one chest of each rarity in turn (the last two lucky), for screenshots and recordings.</summary>
    async void OpeningDemo()
    {
        State = Simulation.FastForward(_seed, 6);
        State.CollectEvents = true;
        StartPlaying(null);
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        foreach (var rarity in Enum.GetValues<Rarity>())
        {
            var play = Play;
            if (play == null) return;
            var opening = State.OpenAs(rarity, rarity >= Rarity.Epic ? ChestKind.Golden : ChestKind.Wooden, lucky: rarity >= Rarity.Legendary);
            play.Chest.PresentHeld(opening);
            while (IsInstanceValid(play) && !play.Chest.Deciding) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Moment($"demo_{rarity.ToString().ToLowerInvariant()}_card");
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            if (opening.Upgrade) play.Chest.PressEquip();
            else play.Chest.PressSell();
            await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
            Moment($"demo_{rarity.ToString().ToLowerInvariant()}_after");
        }
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        QuitGame();
    }

    /// <summary>The title screen, the settings, a new game's first moments and the pause menu, one screenshot each.</summary>
    async void TitleDemo()
    {
        SignalAwaiter Wait(double t) => ToSignal(GetTree().CreateTimer(t, true), SceneTreeTimer.SignalName.Timeout);
        SwitchTo(new TitleScreen(null));
        await Wait(2.5);
        Moment("title_demo");
        await Wait(0.3);
        var settings = new SettingsView();
        _screen.AddChild(settings);
        await Wait(0.5);
        Moment("settings");
        await Wait(0.3);
        settings.QueueFree();
        State = GameState.New(_seed);
        StartPlaying(null);
        await Wait(2.0);
        Moment("new_game");
        await Wait(0.3);
        OpenPause();
        await Wait(0.5);
        Moment("pause");
        await Wait(0.3);
        QuitGame();
    }

    static string Arg(string[] args, string name) =>
        args.FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..];
}
