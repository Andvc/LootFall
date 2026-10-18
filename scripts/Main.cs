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
/// Root of the game: owns the game state, audio, HUD and screen effects, and swaps between phases.
/// Command-line flags (after `--`): --seed=N, --sim [--n=N], --shake-test, --export-sounds=DIR, --autoplay,
/// --shots=DIR, --quit-at-end, --days=N, --no-tutorial (no tips, no scripted first day),
/// --start-tier=N and --gold=N (jump ahead with the tools you'd have by then), --opening-demo (opens a blue chest
/// holding one thing of each quality, two of them better than the chest, to preview the opening's effects; quits
/// after with --quit-at-end). Autoplay sets --days=N to stop after that many days.
/// </summary>
public partial class Main : Node2D
{
    public static Main Instance { get; private set; }

    public GameState State { get; private set; }
    public Sfx Sfx { get; private set; }
    public Hud Hud { get; private set; }

    readonly Random _rng = new();
    Camera2D _camera;
    ColorRect _flash, _fade;
    Phase _phase;
    Autoplay _bot;
    string _shotsDir;
    float _trauma;

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

        int seed = int.TryParse(Arg(args, "--seed"), out var s) ? s : (int)(Time.GetUnixTimeFromSystem() % 1_000_000);
        State = new GameState(seed) { Guided = !args.Contains("--no-tutorial") };
        if (int.TryParse(Arg(args, "--start-tier"), out var startTier))
            State.SkipTo(Math.Clamp(startTier, 1, ShopTiers.Max), int.TryParse(Arg(args, "--gold"), out var g) ? g : 500);
        GD.Print($"LootFall seed {seed}");

        AddChild(new Painter(DrawBackground));
        _camera = new Camera2D { Position = new Vector2(640, 360) };
        AddChild(_camera);

        Sfx = new Sfx();
        AddChild(Sfx);
        if (Arg(args, "--export-sounds") is string soundDir)
        {
            Sfx.Export(soundDir);
            GD.Print($"Exported synthesized sounds to {soundDir}");
            GetTree().Quit();
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
            _bot = new Autoplay
            {
                QuitAtEnd = args.Contains("--quit-at-end"),
                MaxDays = int.TryParse(Arg(args, "--days"), out var days) ? days : 0,
            };
            AddChild(_bot);
        }
        _shotsDir = Arg(args, "--shots");
        if (_shotsDir != null) DirAccess.MakeDirRecursiveAbsolute(_shotsDir);

        Hud.SetGold(State.Gold, animate: false);
        if (args.Contains("--opening-demo")) OpeningDemo(args.Contains("--quit-at-end"));
        else StartDay();
    }

    /// <summary>
    /// Opens a blue chest with one thing of each quality (the epic one a step better than the chest, the legendary
    /// one two steps) and a false bottom, so the opening's effects can be seen and recorded together.
    /// </summary>
    async void OpeningDemo(bool quit)
    {
        State.SkipTo(ShopTiers.Max, 1000);
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
        if (quit) GetTree().Quit();
    }

    /// <summary>Opens the shop for a new day and calls in the first customer.</summary>
    public void StartDay()
    {
        State.StartDay();
        if (_bot != null && !_bot.OnDayStarted(State.Day)) return;
        NextCustomer();
    }

    /// <summary>Calls the next customer to the counter, or closes up for the day.</summary>
    public void NextCustomer()
    {
        Hud.SetDay(State.Day, State.Tier);
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
    public void Shake(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

    public void Flash(Color color, float alpha, float duration = 0.35f)
    {
        _flash.Color = color with { A = alpha };
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
