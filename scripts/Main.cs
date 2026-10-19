using System;
using System.Linq;
using Godot;
using LootFall.Audio;
using LootFall.Core;
using LootFall.Debug;
using LootFall.Phases;
using LootFall.Ui;

namespace LootFall;

/// <summary>
/// Root of the game: owns the game state, audio, HUD and screen effects, and swaps between phases. It opens on the title
/// screen; the shop is saved to user://save.json at the start of each day, and Esc pauses. A day runs: the week's scene
/// (from the second week), Nim's curios on his day, the counter, the day's end, and on the fifth day the Guild's due.
/// What you've done across runs is kept in user://profile.json.
/// Command-line flags (after `--`): --seed=N, --sim [--n=N], --shake-test, --export-sounds=DIR, --autoplay,
/// --shots=DIR, --quit-at-end, --days=N, --no-tutorial (no tips, no scripted first day),
/// --start-tier=N and --gold=N (jump ahead with the tools you'd have by then), --opening-demo (opens a blue chest
/// holding one thing of each quality, two of them better than the chest, to preview the opening's effects; quits
/// after with --quit-at-end), --shell-demo (with --shots: the title screen, a new game, the pause menu and the
/// settings, one screenshot each, then quits), --vault-demo (opens King Aldric's vault; quits after with --quit-at-end),
/// --start-week=N (a shop as a careful player would have it by then, played
/// through by the balance simulation; --gold=N then sets its purse). Autoplay sets --days=N to stop after that many days.
/// </summary>
public partial class Main : Node2D
{
    public static Main Instance { get; private set; }

    public const string Version = "Early Access 0.12";
    const string SavePath = "user://save.json", ProfilePath = "user://profile.json";

    public GameState State { get; private set; }

    /// <summary>Every run so far: for the title screen and the end of a run.</summary>
    public Profile Profile { get; private set; }
    public Sfx Sfx { get; private set; }
    public Hud Hud { get; private set; }

    readonly Random _rng = new();
    Camera2D _camera;
    ColorRect _flash, _fade;
    Phase _phase;
    Autoplay _bot;
    string _shotsDir;
    float _trauma;
    int _seed;

    /// <summary>A real game (not the bot or a debugging shortcut), so the shop is saved each day.</summary>
    bool _saving;

    public override void _Ready()
    {
        Instance = this;
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--sim"))
        {
            Simulation.Run(int.Parse(Arg(args, "--n") ?? "300"));
            GetTree().Quit();
            return;
        }
        if (args.Contains("--shake-test"))
        {
            Sfx = new Sfx();
            AddChild(Sfx);
            AddChild(new ShakeTest { Count = int.Parse(Arg(args, "--n") ?? "40") });
            return;
        }

        Settings.Load();
        Profile = ReadProfile();
        GetTree().Root.Theme = MakeTheme();
        _seed = int.TryParse(Arg(args, "--seed"), out var s) ? s : (int)(Time.GetUnixTimeFromSystem() % 1_000_000);
        // Straight into a day for the bot and the debugging shortcuts; everyone else gets the title screen.
        bool direct = args.Contains("--autoplay") || args.Contains("--opening-demo") || args.Contains("--vault-demo") ||
                      Arg(args, "--start-tier") != null || Arg(args, "--start-week") != null;
        if (direct)
        {
            State = new GameState(_seed) { Guided = !args.Contains("--no-tutorial") };
            if (int.TryParse(Arg(args, "--start-tier"), out var startTier))
                State.SkipTo(Math.Clamp(startTier, 1, ShopTiers.Max), int.TryParse(Arg(args, "--gold"), out var g) ? g : 500);
            // A shop as a careful player would have it by the start of that week (tips are taken as read).
            if (int.TryParse(Arg(args, "--start-week"), out var startWeek) && startWeek > 1)
            {
                State.Guided = false;
                if (!Simulation.FastForward(State, (startWeek - 1) * Guild.DaysPerWeek))
                    GD.Print($"--start-week: this seed's shop was foreclosed in week {State.Week}; starting from there anyway");
                State.Guided = !args.Contains("--no-tutorial");
                if (int.TryParse(Arg(args, "--gold"), out var gold)) State.Gold = gold;
                foreach (var key in new[] { "intro", "dayend", "colours", "slip", "ledger", "upgrades", "display", "synergy", "tier2", "tier3", "tier4" })
                    State.HintsSeen.Add(key);
            }
        }
        GD.Print($"LootFall seed {_seed}");

        AddChild(new Painter(DrawBackground));
        _camera = new Camera2D { Position = new Vector2(640, 360) };
        AddChild(_camera);

        Sfx = new Sfx();
        AddChild(Sfx);
        if (Arg(args, "--export-sounds") is string soundDir)
        {
            Sfx.Export(soundDir);
            GD.Print($"Exported synthesized sounds to {soundDir}");
            QuitGame();
            return;
        }
        Hud = new Hud();
        AddChild(Hud);

        var overlay = new CanvasLayer { Layer = 50 };
        AddChild(overlay);
        _flash = new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Ignore };
        _flash.Size = new Vector2(1280, 720);
        _fade = new ColorRect { Color = new Color(Palette.Bg, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.Size = new Vector2(1280, 720);
        overlay.AddChild(_flash);
        overlay.AddChild(_fade);

        if (args.Contains("--autoplay"))
        {
            MentorView.Trace = text => GD.Print($"[Bram d{State?.Day} {State?.Current?.Seller.Key ?? "-"}] {text}");
            ScenePhase.Trace = text => GD.Print($"[Scene d{State?.Day}] {text}");
            _bot = new Autoplay
            {
                QuitAtEnd = args.Contains("--quit-at-end"),
                MaxDays = int.TryParse(Arg(args, "--days"), out var days) ? days : 0,
            };
            AddChild(_bot);
        }
        _shotsDir = Arg(args, "--shots");
        if (_shotsDir != null) DirAccess.MakeDirRecursiveAbsolute(_shotsDir);

        if (args.Contains("--shell-demo")) ShellDemo();
        else if (!direct) ToTitle();
        else if (args.Contains("--opening-demo")) OpeningDemo(args.Contains("--quit-at-end"));
        else if (args.Contains("--vault-demo")) VaultDemo(args.Contains("--quit-at-end"));
        else BeginPlaying();
    }

    /// <summary>Walks through the title screen, a new game's first moments, the pause menu and the settings, for screenshots.</summary>
    async void ShellDemo()
    {
        SignalAwaiter Wait(double s) => ToSignal(GetTree().CreateTimer(s, true), SceneTreeTimer.SignalName.Timeout);
        ToTitle();
        await Wait(2.5);
        NewGame();
        _saving = false;
        await Wait(3.5);
        Moment("10_newgame");
        await Wait(0.5);
        OpenPause();
        await Wait(0.6);
        Moment("11_pause");
        await Wait(0.5);
        var pause = GetChildren().OfType<PauseMenu>().First();
        var settings = new SettingsView();
        pause.AddChild(settings);
        await Wait(0.6);
        Moment("12_settings");
        await Wait(0.5);
        settings.OpenSounds();
        await Wait(0.6);
        Moment("13_item_sounds");
        await Wait(0.8);
        QuitGame();
    }

    bool _quitting;

    /// <summary>
    /// Quits cleanly: lets go of the screen and the bot first, gives anything still waiting on them a frame or two to
    /// notice, and collects what the managed side still holds, so the engine doesn't shut down around live objects (after
    /// a long session that could crash on the way out).
    /// </summary>
    public async void QuitGame()
    {
        if (_quitting) return;
        _quitting = true;
        GetTree().Paused = false;
        _phase?.QueueFree();
        _phase = null;
        _bot?.QueueFree();
        _bot = null;
        Sfx?.Release();
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit();
    }

    /// <summary>The saved shop, or null if there isn't one (or it's from an older version).</summary>
    public static SaveData ReadSave()
    {
        if (!FileAccess.FileExists(SavePath)) return null;
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        return file == null ? null : SaveData.FromJson(file.GetAsText());
    }

    void WriteSave()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(State.Save().ToJson());
    }

    static Profile ReadProfile()
    {
        if (!FileAccess.FileExists(ProfilePath)) return new Profile();
        using var file = FileAccess.Open(ProfilePath, FileAccess.ModeFlags.Read);
        return file == null ? new Profile() : Profile.FromJson(file.GetAsText());
    }

    /// <summary>Notes the run's progress in the profile (a real game only) and writes it.</summary>
    void WriteProfile(System.Action<Profile> change = null)
    {
        if (!_saving) return;
        change?.Invoke(Profile);
        Profile.Note(State);
        using var file = FileAccess.Open(ProfilePath, FileAccess.ModeFlags.Write);
        file?.StoreString(Profile.ToJson());
    }

    /// <summary>The run is over (foreclosed, or retired): the saved shop goes, and it's back to the title.</summary>
    public void EndRun()
    {
        WriteProfile();
        if (_saving && FileAccess.FileExists(SavePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
        _saving = false;
        if (_bot != null)
        {
            QuitGame();
            return;
        }
        ToTitle();
    }

    /// <summary>Back to the title screen (from the pause menu).</summary>
    public void ToTitle()
    {
        Hud.Visible = false;
        SwitchTo(new TitlePhase(ReadSave()));
    }

    /// <summary>A new shop, saved over any old one when its first day starts.</summary>
    public void NewGame()
    {
        State = new GameState(_seed) { Guided = Settings.Tips };
        _seed++;
        _saving = true;
        WriteProfile(p => p.Runs++);
        BeginPlaying();
    }

    public void ContinueGame(SaveData save)
    {
        State = GameState.Load(save);
        _saving = true;
        BeginPlaying();
    }

    void BeginPlaying()
    {
        Hud.Visible = true;
        Hud.SetGold(State.Gold, animate: false);
        StartDay();
    }

    /// <summary>Esc during a day, or the HUD's Menu button: pause.</summary>
    public void OpenPause()
    {
        if (State == null || _phase is TitlePhase || GetTree().Paused) return;
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

    /// <summary>The look of things Godot draws itself, like tooltips: a framed panel and the pixel font.</summary>
    static Theme MakeTheme()
    {
        var theme = new Theme { DefaultFont = UiKit.Font, DefaultFontSize = 16 };
        theme.SetStylebox("panel", "TooltipPanel", UiKit.PanelBox(10));
        theme.SetColor("font_color", "TooltipLabel", Palette.Text);
        return theme;
    }

    /// <summary>
    /// Opens a blue chest with one thing of each quality (the epic one a step better than the chest, the legendary
    /// one two steps) and a false bottom, so the opening's effects can be seen and recorded together.
    /// </summary>
    async void OpeningDemo(bool quit)
    {
        State.SkipTo(ShopTiers.Max, 1000);
        Hud.SetGold(State.Gold, animate: false);
        var chest = new Chest(new[]
        {
            new Item(Category.Metal, Rarity.Common), new Item(Category.Cloth, Rarity.Uncommon), new Item(Category.Bottles, Rarity.Rare),
            new Item(Category.Coins, Rarity.Epic), new Item(Category.Jewels, Rarity.Legendary),
        }, Rarity.Rare, new Item(Category.Paper, Rarity.Rare, Magic: true));
        var visit = Market.Price(State.Rng, Sellers.Get("vex"), chest, ShopTiers.Max, ask: 300);
        visit.Buy(State.Rng);
        State.Opening = State.Buy(visit, new Appraisal(chest.Quality, ShopTiers.Max, chest.Items.Select(i => i.Category)));
        var opening = new Phases.OpeningPhase();
        SwitchTo(opening);
        while (IsInstanceValid(opening) && !opening.Finished) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        if (quit) QuitGame();
    }

    /// <summary>Opens King Aldric's vault straight away (no lock), to see the finale's opening.</summary>
    async void VaultDemo(bool quit)
    {
        State.SkipTo(ShopTiers.Max, 1000);
        Hud.SetGold(State.Gold, animate: false);
        State.Opening = State.VaultPurchase();
        var opening = new OpeningPhase();
        SwitchTo(opening);
        while (IsInstanceValid(opening) && !opening.Finished) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        if (quit) QuitGame();
    }

    /// <summary>Saves the shop (a real game only), starts a new day, and plays its morning.</summary>
    public void StartDay()
    {
        if (_saving)
        {
            WriteSave();
            WriteProfile();
        }
        State.StartDay();
        Hud.SetDay(State.Day, State.Tier);
        Hud.SetDue(State);
        Hud.SetEvent(State);
        if (_bot != null && !_bot.OnDayStarted(State.Day)) return;
        // The first morning of a week (from the second): who comes by, and what they want.
        if (State.DayOfWeek == 1 && State.Week >= 2)
        {
            var lines = Story.WeekStart(State);
            if (lines.Count > 0)
            {
                SwitchTo(new ScenePhase(lines, Peddler, $"WEEK {State.Week}"));
                return;
            }
        }
        Peddler();
    }

    /// <summary>Nim comes by before opening, on his day.</summary>
    void Peddler()
    {
        if (State.PeddlerToday && State.PeddlerStock.Count > 0) SwitchTo(new PeddlerPhase());
        else OpenShop();
    }

    /// <summary>Opens the shop: the day's card, and the first customer.</summary>
    public void OpenShop()
    {
        DayCard();
        NextCustomer();
    }

    /// <summary>After the day's end: the Guild's introduction (the first evening), its due (the fifth), or the next morning.</summary>
    public void CloseDay()
    {
        if (!State.GuildIntroduced) SwitchTo(new ScenePhase(Story.Intro(), StartDay, "THE GREY GUILD"));
        else if (State.DuesTonight && !State.PaidThisWeek) SwitchTo(new DuesPhase());
        // The debt was paid off early and the last piece of the map has just come: the vault, tonight.
        else if (State.VaultDue) SwitchTo(new ScenePhase(Story.MapWhole(), Vault, "THE WHOLE MAP"));
        else StartDay();
    }

    /// <summary>After the Guild's visit: the shop lost, Bram's debt cleared (and maybe the vault), or the next morning.</summary>
    public void AfterDues()
    {
        if (State.Foreclosed)
        {
            SwitchTo(new ScenePhase(Story.Foreclosed(State), () => SwitchTo(new RunEndPhase(cleared: false))));
            return;
        }
        if (State.DebtCleared && State.ClearedDay == State.Day)
        {
            Cleared();
            return;
        }
        StartDay();
    }

    /// <summary>
    /// Pays off the rest of Bram's debt at the end of a day, however early: the scene, then the vault (with the whole
    /// map) or the end of the run.
    /// </summary>
    public void PayOffDebt()
    {
        if (State.PayOff() <= 0) return;
        Sfx.PlayGroup("pay");
        Hud.SetGold(State.Gold);
        Hud.SetDue(State);
        Cleared();
    }

    /// <summary>The debt's just been cleared: Vane's last visit and Bram's joy, then the vault or the end of the run.</summary>
    void Cleared()
    {
        WriteProfile(p => p.Cleared++);
        SwitchTo(new ScenePhase(Story.Cleared(State), Vault, "FREE AND CLEAR"));
    }

    /// <summary>With the whole map, Mara and Bram bring King Aldric's vault to the counter: pick its lock and open it.</summary>
    void Vault()
    {
        if (!State.VaultDue)
        {
            SwitchTo(new RunEndPhase(cleared: true));
            return;
        }
        State.VaultOpened = true;
        WriteProfile(p => p.Vaults++);
        State.Opening = State.VaultPurchase();
        SwitchTo(new LockpickPhase());
        Moment("24_vault_lock", 1.5);
    }

    /// <summary>After a chest's been opened: the next customer, or (after the vault) the epilogue and the end of the debt.</summary>
    public void AfterOpening()
    {
        if (State.Opening?.Visit.Seller == Story.VaultSeller)
        {
            SwitchTo(new ScenePhase(Story.VaultEpilogue(), () => SwitchTo(new RunEndPhase(cleared: true)), "KING ALDRIC'S VAULT"));
            return;
        }
        NextCustomer();
    }

    /// <summary>
    /// The day's title card, over the first customer: "Day 5", the week and the shop's name, the Guild's due, and today's
    /// event if there is one.
    /// </summary>
    void DayCard()
    {
        var layer = new CanvasLayer { Layer = 40 };
        AddChild(layer);
        var e = State.TodayEvent;
        int stash = State.Today.Stash;
        // The Guild's due shows once Vane has introduced it (the first evening).
        bool guild = State.GuildIntroduced;
        float h = (e == null ? 136 : 216) + (stash > 0 ? 28 : 0) - (guild ? 0 : 28);
        var card = UiKit.MakePanel(Palette.Gold.Darkened(0.2f)).Place(306, 130, 500, h);
        card.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(UiKit.MakeLabel($"DAY {State.Day}", 48, Palette.Gold, HorizontalAlignment.Center, outline: 6).Place(0, 12, 500, 56));
        card.AddChild(UiKit.MakeLabel($"Week {State.Week}, day {State.DayOfWeek} of {Guild.DaysPerWeek}  -  {ShopTiers.Name(State.Tier)}", 16,
            Palette.Text, HorizontalAlignment.Center).Place(0, 72, 500, 20));
        string due = State.DuesTonight ? $"The Guild comes tonight for {State.DueNow}g."
            : $"The Guild comes for {State.DueNow}g at the end of day {State.Day + State.DaysToDue}.";
        if (guild)
            card.AddChild(UiKit.MakeLabel(due, 16, State.Gold >= State.DueNow ? Palette.Muted : Palette.Warn, HorizontalAlignment.Center)
                .Place(0, 100, 500, 20));
        float y = guild ? 132 : 104;
        if (stash > 0)
        {
            card.AddChild(UiKit.MakeLabel($"Uncle Bram slipped you {stash}g from his secret stash ({State.StashLeft} left).", 16, Palette.Good,
                HorizontalAlignment.Center).Place(0, y - 4, 500, 20));
            Hud.SetGold(State.Gold);
            y += 28;
        }
        if (e != null)
        {
            card.AddChild(UiKit.MakeLabel(e.Name.ToUpperInvariant(), 24, Palette.Magic, HorizontalAlignment.Center, outline: 4).Place(0, y, 500, 30));
            card.AddChild(UiKit.MakeLabel(Events.Describe(e, State.BuyerKind), 16, Palette.Text, HorizontalAlignment.Center).Wrapped()
                .Place(20, y + 32, 460, 44));
            Sfx.Play("chime", 1f, -6);
        }
        layer.AddChild(card);
        card.PivotOffset = new Vector2(250, h / 2);
        card.Scale = new Vector2(0.9f, 0.9f);
        card.Modulate = Colors.Transparent;
        var tween = card.CreateTween();
        tween.TweenProperty(card, "modulate", Colors.White, 0.25f).SetDelay(0.2f);
        tween.Parallel().TweenProperty(card, "scale", Vector2.One, 0.25f).SetDelay(0.2f).SetTrans(Tween.TransitionType.Back);
        tween.TweenInterval(e == null && stash == 0 ? 1.0 : 2.6);
        tween.TweenProperty(card, "modulate", Colors.Transparent, 0.35f);
        tween.TweenCallback(Callable.From(layer.QueueFree));
        Moment(e != null ? $"00_event_{e.Key}" : stash > 0 ? "00_stash" : "00_daycard", 0.6);
    }

    /// <summary>Calls the next customer to the counter, or closes up for the day.</summary>
    public void NextCustomer()
    {
        Hud.SetDay(State.Day, State.Tier);
        Hud.SetDue(State);
        Hud.SetEvent(State);
        if (State.NextCustomer()) SwitchTo(new CounterPhase());
        else SwitchTo(new DayEndPhase());
    }

    public async void SwitchTo(Phase next)
    {
        if (_phase != null)
        {
            var fadeOut = CreateTween();
            fadeOut.TweenProperty(_fade, "color:a", 1f, 0.18f);
            await ToSignal(fadeOut, Tween.SignalName.Finished);
            _phase.QueueFree();
            Zoom(1f, Center);
        }
        _phase = next;
        AddChild(next);
        CreateTween().TweenProperty(_fade, "color:a", 0f, 0.22f);
        _bot?.OnPhase(next);
    }

    static readonly Vector2 Center = new(640, 360);

    /// <summary>Zooms the camera in (above 1) or out, keeping <paramref name="focus"/> where it is on screen.</summary>
    public void Zoom(float zoom, Vector2 focus)
    {
        _camera.Zoom = Vector2.One * zoom;
        _camera.Position = focus - (focus - Center) / zoom;
    }

    /// <summary>Adds screen-shake "trauma" (0..1). Shake strength is trauma squared, so small hits stay subtle.</summary>
    public void Shake(float amount) => _trauma = Mathf.Min(1f, _trauma + amount * Settings.Shake);

    public void Flash(Color color, float alpha, float duration = 0.35f)
    {
        _flash.Color = color with { A = alpha * Settings.Shake };
        CreateTween().TweenProperty(_flash, "color:a", 0f, duration).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Freezes time for a moment on impact, which sells the weight of a hit.</summary>
    public async void HitStop(double seconds)
    {
        Engine.TimeScale = 0.05;
        await ToSignal(GetTree().CreateTimer(seconds, true, false, true), SceneTreeTimer.SignalName.Timeout);
        Engine.TimeScale = 1.0;
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
        _trauma = Mathf.Max(0f, _trauma - (float)delta * 1.6f);
        float strength = _trauma * _trauma;
        _camera.Offset = new Vector2(Rand(), Rand()) * 24f * strength;
    }

    float Rand() => (float)(_rng.NextDouble() * 2 - 1);

    static void DrawBackground(Painter p)
    {
        p.DrawRect(new Rect2(-100, -100, 1480, 920), Palette.Bg);
        for (int i = 0; i < 6; i++)
            p.DrawRect(new Rect2(-100, 560 + i * 30, 1480, 30), new Color(0, 0, 0, 0.06f * i));
    }

    static string Arg(string[] args, string name) =>
        args.FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..];
}
