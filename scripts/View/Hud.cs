using System;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>Small inked icons for what the game counts: gold, shards (materials), aether, stardust, talent points.</summary>
public static class Glyphs
{
    public static void Coin(CanvasItem c, Vector2 at, float r)
    {
        Ink.ShadedCircle(c, at, r, Ink.Gold, 2.5f);
        c.DrawArc(at, r * 0.55f, 0, Mathf.Tau, 16, Ink.Gold.Shadow, 2, true);
    }

    public static void Shard(CanvasItem c, Vector2 at, float r)
    {
        var pts = new[] { at + new Vector2(0, -r * 1.2f), at + new Vector2(r * 0.7f, -r * 0.2f), at + new Vector2(r * 0.3f, r), at + new Vector2(-r * 0.4f, r), at + new Vector2(-r * 0.7f, -r * 0.1f) };
        Ink.Shaded(c, pts, new Ink.Ramp("#2b5f8a", "#4fa3d8", "#a8e2ff"), 2.5f);
    }

    public static void Aether(CanvasItem c, Vector2 at, float r)
    {
        var pts = new[] { at + new Vector2(0, -r * 1.2f), at + new Vector2(r * 0.8f, 0), at + new Vector2(0, r * 1.2f), at + new Vector2(-r * 0.8f, 0) };
        Ink.Shaded(c, pts, new Ink.Ramp("#5a3a8a", "#9a6ad8", "#d9c2ff"), 2.5f);
    }

    public static void Stardust(CanvasItem c, Vector2 at, float r)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
            pts[i] = at + Vector2.FromAngle(a) * (i % 2 == 0 ? r * 1.2f : r * 0.5f);
        }
        Ink.Shaded(c, pts, new Ink.Ramp("#8a6a1e", "#e0b84a", "#fff0b0"), 2.5f);
    }

    public static void Point(CanvasItem c, Vector2 at, float r)
    {
        Ink.ShadedCircle(c, at, r, new Ink.Ramp("#1f6b5a", "#3fbf9a", "#a8f0d8"), 2.5f);
        c.DrawLine(at + new Vector2(-r * 0.5f, 0), at + new Vector2(r * 0.5f, 0), Look.Ink, 3, true);
        c.DrawLine(at + new Vector2(0, -r * 0.5f), at + new Vector2(0, r * 0.5f), Look.Ink, 3, true);
    }
}

/// <summary>
/// The top bar over the lane: level and experience, gold and shards (and aether once ascension opens) on the left; the
/// depth, its realm and how the push through it is going in the middle (waves, then the boss, with its bar and the
/// 30-second clock); the menu on the right.
/// </summary>
public partial class Hud : Control
{
    readonly Session _s;
    readonly BattleView _battle;
    readonly Label _level, _xp, _gold, _shards, _aether, _depth, _realm, _status, _bossName, _bossTime;
    readonly Bar _xpBar, _bossBar;
    readonly Canvas _progress, _icons, _bossClock;
    readonly Control _boss;
    public readonly Button Menu;
    int _shownDepth = -1;
    double _bossLimit = 30;

    public Hud(Session s, BattleView battle)
    {
        _s = s;
        _battle = battle;
        MouseFilter = MouseFilterEnum.Ignore;
        this.Place(0, 0, Look.W, 200);
        AddChild(Ui.Gradient(new Color(Look.Bg, 0.92f), new Color(Look.Bg, 0f), 0, 0, Look.W, 170));

        // Left: level, experience and the purse.
        _icons = new Canvas(DrawIcons).Place(0, 0, 700, 140);
        AddChild(_icons);
        _level = Ui.Text("1", 34, Look.Text, Look.Display, HorizontalAlignment.Center, 6).Place(24, 16, 64, 64).Centred();
        AddChild(_level);
        _xpBar = new Bar(Look.Xp).Place(104, 26, 230, 14);
        AddChild(_xpBar);
        _xp = Ui.Text("", Look.Small, Look.Muted, Look.Numbers).Place(104, 42, 300, 26);
        AddChild(_xp);
        _gold = Ui.Text("0", 30, Look.Gold, Look.Numbers, outline: 4).Place(392, 14, 200, 40);
        AddChild(_gold);
        _shards = Ui.Text("0", 26, Look.Shards, Look.Numbers, outline: 4).Place(392, 54, 200, 36);
        AddChild(_shards);
        _aether = Ui.Text("", 26, Look.Aether, Look.Numbers, outline: 4).Place(592, 54, 200, 36);
        AddChild(_aether);

        // Middle: the depth and its progress.
        _depth = Ui.Text("DEPTH 1", 42, Look.Text, Look.Display, HorizontalAlignment.Center, 8).Place(560, 6, 800, 56);
        AddChild(_depth);
        _realm = Ui.Text("", 20, Look.Muted, Look.UiBold, HorizontalAlignment.Center, 4).Place(560, 58, 800, 28);
        AddChild(_realm);
        _progress = new Canvas(DrawProgress).Place(660, 92, 600, 20);
        AddChild(_progress);
        _status = Ui.Text("", 20, Look.Text, Look.UiBold, HorizontalAlignment.Center, 4).Place(560, 114, 800, 28);
        AddChild(_status);

        // The boss, while there is one.
        _boss = new Control { MouseFilter = MouseFilterEnum.Ignore, Visible = false }.Place(560, 140, 800, 70);
        AddChild(_boss);
        _bossName = Ui.Text("", 26, Look.Text, Look.Display, HorizontalAlignment.Center, 6).Place(0, 0, 800, 34);
        _boss.AddChild(_bossName);
        _bossBar = new Bar(Look.Threat) { Radius = 6 }.Place(100, 36, 600, 20);
        _boss.AddChild(_bossBar);
        _bossClock = new Canvas(DrawBossClock).Place(712, 26, 40, 40);
        _boss.AddChild(_bossClock);
        _bossTime = Ui.Text("", Look.Small, Look.Text, Look.Numbers, HorizontalAlignment.Center, 4).Place(700, 30, 64, 30);
        _boss.AddChild(_bossTime);

        Menu = Ui.Button("Menu", () => Main.Instance?.Game?.OpenPause(), size: Look.Body).Place(1776, 18, 120, 52);
        AddChild(Menu);

        battle.BossEntered += f =>
        {
            _boss.Visible = true;
            _bossName.Text = (f.Role == FoeRole.Guardian ? "Guardian: " : "") + f.Def.Name;
            _bossName.AddThemeColorOverride("font_color", f.Role == FoeRole.Guardian ? Look.Gold : Look.Text);
            _bossLimit = Math.Max(0.1, _battle.Fight.BossTime);
            Fx.Bump(_bossName, 1.15f, 0.25f);
        };
        battle.BossFell += (_, _) => _boss.Visible = false;
    }

    public override void _Process(double delta)
    {
        var h = _s.H;
        _level.Text = h.Level.ToString();
        var need = h.XpToNext();
        _xpBar.Set((float)Math.Clamp(need.IsZero ? 0 : (h.Xp / need).ToDouble(), 0, 1));
        _xp.Text = $"{Format.Count(h.Xp)} / {Format.Count(need)} xp";
        _gold.Text = Format.Count(h.Gold);
        _shards.Text = Format.Count(h.Materials);
        bool asc = _s.Unlocked(Feature.Ascension);
        _aether.Visible = asc;
        if (asc) _aether.Text = Format.Count(h.Ascension.Currency) + (_s.AetherNow.IsZero ? "" : $"  (+{Format.Count(_s.AetherNow)})");

        var st = _battle.Stage;
        int depth = st.Mode == StageMode.Farm ? h.RunDepth : st.F.Next;
        if (depth != _shownDepth)
        {
            if (_shownDepth > 0 && depth > _shownDepth) Fx.Bump(_depth, 1.08f, 0.2f);
            _shownDepth = depth;
            _depth.Text = $"DEPTH {Format.Whole(depth)}";
            _realm.Text = Bestiary.RealmName(depth) + (Combat.IsGuardian(depth) && st.Mode == StageMode.Push ? "  -  guardian depth" : "");
        }
        _status.Text = st.Mode switch
        {
            StageMode.Leap => $"Overkill: {st.F.Skip} depths at a stroke",
            StageMode.Farm => $"Farming depth {Format.Whole(st.F.Depth)}: the next boss is too strong",
            _ => "",
        };
        _status.AddThemeColorOverride("font_color", st.Mode == StageMode.Farm ? Look.Muted : Look.Crit);
        if (_battle.Boss != null)
        {
            _bossBar.Set((float)_battle.Boss.Hp);
            double left = Math.Max(0, _s.P.BossTime - _battle.BossClock);
            _bossTime.Text = $"{left:0}s";
        }
        _progress.QueueRedraw();
        _bossClock.QueueRedraw();
        _icons.QueueRedraw();
    }

    void DrawIcons(Canvas c)
    {
        // The level badge.
        Ink.ShadedCircle(c, new Vector2(56, 48), 32, new Ink.Ramp("#2a2140", "#3d3060", "#5a4a88"));
        c.DrawArc(new Vector2(56, 48), 32, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _xpBar.Value, 40, Look.Xp, 4, true);
        Glyphs.Coin(c, new Vector2(372, 34), 13);
        Glyphs.Shard(c, new Vector2(372, 72), 12);
        if (_aether.Visible) Glyphs.Aether(c, new Vector2(572, 72), 12);
    }

    void DrawProgress(Canvas c)
    {
        var st = _battle.Stage;
        var size = c.Size;
        if (st.Mode == StageMode.Farm)
        {
            c.DrawRect(new Rect2(0, 6, size.X, 8), new Color(Look.Dim, 0.4f));
            return;
        }
        double p = st.Mode == StageMode.Leap ? 1 : st.F.Progress;
        float waves = (float)st.F.WaveShare;
        var back = new Color("#1a1524");
        c.DrawRect(new Rect2(-2, -2, size.X + 4, size.Y + 4), Look.Ink);
        c.DrawRect(new Rect2(0, 0, size.X, size.Y), back);
        c.DrawRect(new Rect2(0, 0, size.X * (float)Math.Min(p, waves), size.Y), Look.Blow.Darkened(0.15f));
        if (p > waves) c.DrawRect(new Rect2(size.X * waves, 0, size.X * (float)(p - waves), size.Y), Look.Threat);
        // Wave ticks and the boss's segment.
        int n = Math.Max(1, (int)Math.Round(_s.P.KillsPerDepth / _s.P.WaveSize));
        for (int i = 1; i < n; i++) c.DrawLine(new Vector2(size.X * waves * i / n, 0), new Vector2(size.X * waves * i / n, size.Y), Look.Ink, 2);
        c.DrawLine(new Vector2(size.X * waves, -4), new Vector2(size.X * waves, size.Y + 4), Look.Ink, 4);
        var skull = new Vector2(size.X * waves + (size.X * (1 - waves)) / 2, size.Y / 2);
        c.DrawCircle(skull, 7, p > waves ? Look.Text : Look.Dim, true, -1, true);
    }

    void DrawBossClock(Canvas c)
    {
        if (_battle.Boss == null) return;
        float left = (float)Math.Clamp(1 - _battle.BossClock / _s.P.BossTime, 0, 1);
        var centre = c.Size / 2;
        c.DrawCircle(centre, 20, new Color("#1a1524"), true, -1, true);
        c.DrawArc(centre, 18, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * left, 40, left > 0.3f ? Look.Gold : Look.Threat, 5, true);
    }
}
