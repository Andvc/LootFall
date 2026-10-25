using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The game: the lane on top (the battle and its top bar), the bottom panel (the chest tray, the armoury's eight kind
/// tiles with the latest finds, and the dock of systems), toasts, and the overlays (the reveal, the armoury, the talent
/// tree, the codex, the prestige trees, settings, the pause menu, the welcome back). It ticks the session and turns its
/// events into what's shown: chests falling out of the monster that dropped them, levels, new slots, new rarities.
/// </summary>
public partial class GameScreen : Control
{
    public readonly Session S;
    public readonly BattleView Battle;
    public readonly Hud Hud;
    public readonly ChestTray Tray;
    public readonly KindTiles Tiles;
    public readonly Dock Dock;
    readonly Toasts _toasts;
    readonly Control _overlays;
    /// <summary>The layers above the lane: the UI, things flying between the lane and the UI, and the overlays (so the
    /// lane's own z-order never puts a monster over a panel).</summary>
    readonly CanvasLayer _uiLayer, _flyLayer, _overlayLayer;
    /// <summary>Where chests, icons and shards fly from the lane to the panels.</summary>
    public readonly Control Fly;
    public Reveal Reveal { get; private set; }
    /// <summary>The level the level toast showing counts from (-1 when none is showing).</summary>
    int _openSlots, _toastLevelFrom = -1;
    readonly AwaySummary _welcome;

    public GameScreen(Session s, AwaySummary welcome)
    {
        S = s;
        _welcome = welcome;
        this.Place(0, 0, Look.W, Look.H);
        MouseFilter = MouseFilterEnum.Ignore;
        Battle = new BattleView(s);
        AddChild(Battle);
        _uiLayer = new CanvasLayer { Layer = 1 };
        AddChild(_uiLayer);
        _flyLayer = new CanvasLayer { Layer = 2 };
        AddChild(_flyLayer);
        _overlayLayer = new CanvasLayer { Layer = 3 };
        AddChild(_overlayLayer);
        Fly = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 0, Look.W, Look.H);
        _flyLayer.AddChild(Fly);
        Hud = new Hud(s, Battle);

        // The bottom panel.
        var panel = new Panel { MouseFilter = MouseFilterEnum.Ignore }.Place(0, Look.LaneH, Look.W, Look.H - Look.LaneH);
        var face = Ui.Frame(new Color("#120e19"), Look.Edge, 0, 0, 0, 0);
        face.BorderWidthTop = 3;
        face.BorderColor = Look.EdgeLight;
        panel.AddThemeStyleboxOverride("panel", face);
        _uiLayer.AddChild(panel);
        _uiLayer.AddChild(Ui.Gradient(new Color(0, 0, 0, 0.35f), new Color(0, 0, 0, 0), 0, Look.LaneH + 3, Look.W, 30));
        _uiLayer.AddChild(Hud);
        Tray = new ChestTray(this);
        _uiLayer.AddChild(Tray);
        Tiles = new KindTiles(this);
        _uiLayer.AddChild(Tiles);
        Dock = new Dock(this);
        _uiLayer.AddChild(Dock);
        _toasts = new Toasts();
        _uiLayer.AddChild(_toasts);
        _overlays = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 0, Look.W, Look.H);
        _overlayLayer.AddChild(_overlays);

        _openSlots = s.H.OpenCount;
        Battle.BossFell += (f, depth) => Main.Instance?.Music?.Play(Music(depth));
        Battle.BossEntered += f =>
        {
            Main.Instance?.Music?.Play("boss");
            Main.Instance?.Moment(f.Role == FoeRole.Guardian ? "guardian" : "boss", 0.6);
        };
        Battle.Leapt += (from, to) => Banner($"DEPTH {Format.Whole(from)}  >  {Format.Whole(to)}", Look.Crit, "Overkill", 1.3f);
    }

    static string Music(int depth) => Bestiary.Cycle(depth) % 2 == 0 && Hero.Era(depth) % 2 == 0 ? "battle" : "battle2";

    public override void _Ready()
    {
        Main.Instance?.Music?.Play(Music(S.H.RunDepth));
        if (_welcome != null) Open(new WelcomeBack(this, _welcome));
        Main.Instance?.Moment("game_start", 1.5);
    }

    public override void _Process(double delta)
    {
        if (GetTree().Paused) return;
        S.Tick(delta);
        while (S.Events.Count > 0) Handle(S.Events.Dequeue());
        Battle.Step(delta);
        // A full-screen overlay covers the lane: it goes on fighting, unseen (and undrawn).
        bool covered = _overlays.GetChildren().Any(c => c is Overlay o && c is not LootFall.View.Reveal && !o.Closing && o.Modulate.A > 0.99f);
        Battle.Visible = !covered;
    }

    // ------------------------------------------------------------------------------------------------ events

    void Handle(GameEvent e)
    {
        switch (e.Kind)
        {
            case EventKind.ChestSealed:
                Tray.Drop((Chest)e.Data, Battle.DropPoint);
                break;
            case EventKind.ChestOpened when e.Flag:
                Tray.AutoOpened((Opening)e.Data);
                break;
            case EventKind.Level:
                if (_toastLevelFrom < 0 || S.H.Level - e.Value < _toastLevelFrom) _toastLevelFrom = S.H.Level - e.Value;
                int gained = S.H.Level - _toastLevelFrom;
                _toasts.Show($"Level {S.H.Level}", gained > 1 ? $"+{gained} levels, +{gained} talent points" : "+1 talent point", Look.Xp, "level");
                GetTree().CreateTimer(4.0, false).Timeout += () => _toastLevelFrom = -1;
                Main.Instance?.Sfx?.PlayGroup("powerup", 1f, -8);
                Battle.Hero.Flash(Look.Xp, 0.6f);
                break;
            case EventKind.SlotsOpened:
                foreach (var name in NewSlots()) _toasts.Show("New slot", name, Look.Good);
                Tiles.Refresh();
                break;
            case EventKind.RarityOpened:
                Banner($"{Look.TierName(e.Value).ToUpperInvariant()} ITEMS NOW DROP", Look.Tier(e.Value), $"Tier {Look.Numeral(e.Value)}", 2f);
                Main.Instance?.Sfx?.Play("fanfare", 1f, -6);
                Main.Instance?.Moment($"rarity_{e.Value}", 0.4);
                break;
            case EventKind.Unlocked:
                _toasts.Show("Unlocked", (string)e.Data, Look.Gold);
                break;
            case EventKind.Ascended:
                Banner("ASCENDED", Look.Aether, $"+{Format.Num(e.Amount)} aether", 2.2f);
                Battle.RefreshGear();
                Tiles.Refresh();
                Main.Instance?.Moment("ascended", 0.8);
                break;
            case EventKind.Transcended:
                Banner("TRANSCENDED", Look.Stardust, $"+{Format.Num(e.Amount)} stardust", 2.5f);
                Battle.RefreshGear();
                Tiles.Refresh();
                Main.Instance?.Moment("transcended", 0.8);
                break;
            case EventKind.Mastery:
                break;
        }
    }

    List<string> NewSlots()
    {
        var names = new List<string>();
        int open = S.H.OpenCount;
        if (open <= _openSlots) return names;
        // The newest open slots are the last ones open in unlock order.
        for (int i = S.H.Gear.Length - 1, n = open - _openSlots; i >= 0 && names.Count < n; i--)
            if (S.H.Open[i] && S.H.Gear[i] == null) names.Add(S.C.Slots[i].Name);
        _openSlots = open;
        return names;
    }

    // ------------------------------------------------------------------------------------------------ chests

    /// <summary>Opens the next chest with the full reveal (Space or the tray's button).</summary>
    public void OpenNext()
    {
        if (Reveal != null && IsInstanceValid(Reveal))
        {
            Reveal.Hurry();
            return;
        }
        var chest = S.Pile.Next();
        if (chest == null) return;
        var opening = S.Open(chest);
        // The events this opening queued are shown by the reveal.
        Reveal = new Reveal(this, opening);
        _overlays.AddChild(Reveal);
        Tray.Refresh();
    }

    /// <summary>Opens every waiting chest at once, the reveal showing their best finds together.</summary>
    public void OpenAll()
    {
        if (Reveal != null && IsInstanceValid(Reveal)) return;
        var openings = S.OpenAll();
        if (openings.Count == 0) return;
        Reveal = new Reveal(this, openings);
        _overlays.AddChild(Reveal);
        Tray.Refresh();
    }

    public void AfterReveal()
    {
        Reveal = null;
        Battle.RefreshGear();
        Tiles.Refresh();
        Tray.Refresh();
    }

    // ------------------------------------------------------------------------------------------------ overlays

    public void Open(Control overlay)
    {
        foreach (var child in _overlays.GetChildren())
            if (child is Overlay o && child != Reveal) o.Close();
        _overlays.AddChild(overlay);
    }

    /// <summary>Esc: closes the top overlay; false when there was none.</summary>
    public bool CloseOverlay()
    {
        for (int i = _overlays.GetChildCount() - 1; i >= 0; i--)
        {
            if (_overlays.GetChild(i) is Overlay o && !o.Closing)
            {
                o.Close();
                return true;
            }
        }
        return false;
    }

    public bool OverlayOpen => _overlays.GetChildren().Any(c => c is Overlay o && !o.Closing);

    public void OpenPause() => Open(new PauseMenu(this));

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Space:
            case Key.Enter:
                if (!OverlayOpen || Reveal != null) OpenNext();
                break;
            case Key.G:
                Dock.Press(Dock.Armoury);
                break;
            case Key.T:
                Dock.Press(Dock.Talents);
                break;
            case Key.C:
                Dock.Press(Dock.Codex);
                break;
            case Key.A:
                Dock.Press(Dock.Ascend);
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------------------------------------ banners

    /// <summary>A big centred banner over the lane: a new tier, an ascension, a leap.</summary>
    public void Banner(string title, Color color, string sub = null, float hold = 1.6f)
    {
        var box = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 230, Look.W, 160);
        var shade = Ui.Gradient(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.55f), 0, 0, Look.W, 80);
        var shade2 = Ui.Gradient(new Color(0, 0, 0, 0.55f), new Color(0, 0, 0, 0), 0, 80, Look.W, 80);
        box.AddChild(shade);
        box.AddChild(shade2);
        var t = Ui.Text(title, 64, color, Look.Pop, HorizontalAlignment.Center, 14).Place(0, 20, Look.W, 84);
        box.AddChild(t);
        if (sub != null) box.AddChild(Ui.Text(sub, 26, Look.Text, Look.UiBold, HorizontalAlignment.Center, 6).Place(0, 104, Look.W, 40));
        Fly.AddChild(box);
        box.Modulate = new Color(1, 1, 1, 0);
        t.PivotOffset = new Vector2(Look.W / 2, 42);
        t.Scale = new Vector2(1.6f, 1.6f);
        var tw = box.CreateTween();
        tw.TweenProperty(box, "modulate:a", 1f, 0.15f);
        tw.Parallel().TweenProperty(t, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(hold);
        tw.TweenProperty(box, "modulate:a", 0f, 0.4f);
        tw.TweenCallback(Callable.From(box.QueueFree));
    }
}

/// <summary>A full-screen panel over the game with a shade behind it; Esc or its close button closes it.</summary>
public abstract partial class Overlay : Control
{
    public bool Closing { get; private set; }
    protected readonly GameScreen G;

    protected Overlay(GameScreen g, float alpha = 0.72f)
    {
        G = g;
        this.Place(0, 0, Look.W, Look.H);
        AddChild(Ui.Shade(alpha));
        Modulate = new Color(1, 1, 1, 0);
    }

    public override void _EnterTree() => CreateTween().TweenProperty(this, "modulate:a", 1f, 0.15f);

    public virtual void Close()
    {
        if (Closing) return;
        Closing = true;
        var t = CreateTween();
        t.TweenProperty(this, "modulate:a", 0f, 0.12f);
        t.TweenCallback(Callable.From(QueueFree));
    }

    /// <summary>A titled panel filling most of the screen, with a close button.</summary>
    protected Panel Frame(string title, float x = 80, float y = 60, float w = 1760, float h = 960)
    {
        var p = Ui.Box(new Color("#14101c"), Look.EdgeLight, 14).Place(x, y, w, h);
        p.MouseFilter = MouseFilterEnum.Stop;
        AddChild(p);
        p.AddChild(Ui.Text(title, Look.Header, Look.Gold, Look.Display, outline: 0).Place(32, 18, w - 200, 50));
        p.AddChild(Ui.Button("Close", Close, size: Look.Body).Place(w - 150, 18, 120, 50));
        return p;
    }
}

/// <summary>Short notes that slide in at the top right and leave on their own (a level, a slot, an unlock).</summary>
public partial class Toasts : Control
{
    readonly List<Control> _live = new();

    public Toasts()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        this.Place(Look.W - 420, 84, 400, 500);
    }

    readonly Dictionary<string, (Control Box, Label Title, Label Text)> _keyed = new();

    /// <summary>A toast; with a <paramref name="key"/>, one still showing with the same key is updated instead (levels
    /// that come several a minute stay one note).</summary>
    public void Show(string title, string text, Color accent, string key = null)
    {
        if (key != null && _keyed.TryGetValue(key, out var k) && IsInstanceValid(k.Box) && k.Box.Modulate.A > 0.5f)
        {
            k.Title.Text = title;
            k.Text.Text = text;
            Fx.Bump(k.Box, 1.05f, 0.15f);
            return;
        }
        _live.RemoveAll(c => !IsInstanceValid(c));
        while (_live.Count >= 5)
        {
            _live[0].QueueFree();
            _live.RemoveAt(0);
        }
        var box = Ui.Box(new Color("#16111f", 0.95f), accent, 10).Place(420, 0, 380, 64);
        var t1 = Ui.Text(title, 22, accent, Look.UiBold).Place(16, 6, 350, 28);
        var t2 = Ui.Text(text, 20, Look.Text, Look.Ui).Place(16, 32, 350, 28);
        box.AddChild(t1);
        box.AddChild(t2);
        if (key != null) _keyed[key] = (box, t1, t2);
        AddChild(box);
        _live.Add(box);
        for (int i = 0; i < _live.Count; i++)
        {
            var c = _live[i];
            c.CreateTween().TweenProperty(c, "position:y", (float)(_live.Count - 1 - i) * 72, 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        var t = box.CreateTween();
        t.TweenProperty(box, "position:x", 20f, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenInterval(3.5);
        t.TweenProperty(box, "modulate:a", 0f, 0.4f);
        t.TweenCallback(Callable.From(box.QueueFree));
    }
}
