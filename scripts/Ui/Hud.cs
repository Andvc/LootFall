using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// Over the top of the battle: gold (counting up to what you have), the stage, chests waiting, the menu; under that
/// the stage's progress to its boss, the boss button and the Auto-Boss switch.
/// </summary>
public partial class Hud : Node2D
{
    readonly GameState _g;
    Label _gold, _stage, _chests;
    Sprite2D _goldIcon;
    double _shownGold;
    Node2D _pips;
    Button _boss, _autoBoss;
    int _kills = -1;
    bool _bossWasReady;
    Tween _bossPulse;

    public event Action MenuPressed;

    public Hud(GameState g)
    {
        _g = g;
        _shownGold = g.Gold;
    }

    /// <summary>Where gold flies to, in screen coordinates.</summary>
    public Vector2 GoldSpot => new(34, 22);

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, 1280, 44));
        _goldIcon = UiKit.MakeIcon("gold", GoldSpot);
        AddChild(_goldIcon);
        _gold = UiKit.MakeLabel("", 24, Palette.Gold, outline: 6).Place(56, 8, 220, 30);
        AddChild(_gold);
        _stage = UiKit.MakeLabel("", 24, Palette.Text, HorizontalAlignment.Center, outline: 6).Place(340, 8, 600, 30);
        AddChild(_stage);
        AddChild(UiKit.MakeIcon("chest", new Vector2(1036, 22)));
        _chests = UiKit.MakeLabel("", 24, Palette.Text, outline: 6).Place(1056, 8, 80, 30);
        AddChild(_chests);
        var menu = UiKit.MakeButton("MENU", new Color("#4a4458"), 16, () => MenuPressed?.Invoke()).Place(1168, 6, 104, 32);
        AddChild(menu);

        _pips = new Node2D { Position = new Vector2(640 - 5 * 26 + 4, 58) };
        AddChild(_pips);

        _boss = UiKit.MakeButton("BOSS", new Color("#8a2a3a"), 16, () => Main.Instance.Play?.FightBoss()).Place(1110, 52, 160, 34);
        AddChild(_boss);
        _autoBoss = UiKit.MakeButton("", new Color("#3a3448"), 16, () =>
        {
            _g.AutoBoss = !_g.AutoBoss;
            Main.Instance.Sfx.PlayGroup("toggle");
            Refresh();
        }).Place(1110, 90, 160, 28);
        AddChild(_autoBoss);
        Refresh();
    }

    public void Refresh()
    {
        _stage.Text = World.StageName(_g.Stage) + (_g.Stage < _g.MaxStage ? "  (back)" : "");
        _chests.Text = (Main.Instance?.Play?.Chest?.OnPile ?? _g.ChestTotal).ToString();
        _autoBoss.Text = _g.AutoBoss ? "AUTO-BOSS ON" : "AUTO-BOSS OFF";
        UiKit.StyleButton(_autoBoss, _g.AutoBoss ? new Color("#2f6a5a") : new Color("#4a4458"));

        bool ready = _g.BossReady && !_g.InBossFight && !_g.BossQueued;
        _boss.Disabled = !ready;
        _boss.Text = _g.InBossFight ? "FIGHTING" : _g.BossQueued ? "BOSS NEXT" : ready ? (_g.BossCooldown > 0 ? "RETRY BOSS" : "FIGHT BOSS") : "BOSS";
        if (ready && !_bossWasReady)
        {
            _bossPulse?.Kill();
            _boss.PivotOffset = _boss.Size / 2;
            _bossPulse = _boss.CreateTween().SetLoops();
            _bossPulse.TweenProperty(_boss, "scale", Vector2.One * 1.08f, 0.3f).SetTrans(Tween.TransitionType.Sine);
            _bossPulse.TweenProperty(_boss, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Sine);
        }
        else if (!ready && _bossWasReady)
        {
            _bossPulse?.Kill();
            _boss.Scale = Vector2.One;
        }
        _bossWasReady = ready;

        if (_g.Kills != _kills || _pips.Visible == _g.InBossFight)
        {
            _kills = _g.Kills;
            _pips.Visible = !_g.InBossFight;
            _pips.QueueRedraw();
            foreach (var c in _pips.GetChildren()) c.QueueFree();
            var p = new Painter(DrawPips);
            _pips.AddChild(p);
        }
    }

    void DrawPips(Painter p)
    {
        int n = Balance.KillsPerStage;
        for (int i = 0; i < n; i++)
        {
            var r = new Rect2(i * 26, 0, 20, 12);
            p.DrawRect(r.Grow(2), Colors.Black);
            p.DrawRect(r, i < _g.Kills ? (_g.Stage < _g.MaxStage ? Palette.Muted : Palette.Gold) : new Color("#2a2438"));
        }
        // The boss at the end of the row.
        var skull = UiKit.Icon(_g.BossReady ? "boss" : "skull");
        p.DrawTexture(skull, new Vector2(n * 26 + 2, -10));
    }

    public override void _Process(double delta)
    {
        // The gold counter runs up (or down) to what you have.
        if (Math.Abs(_shownGold - _g.Gold) > 0.5)
        {
            _shownGold += (_g.Gold - _shownGold) * Math.Min(1, delta * 8);
            if (Math.Abs(_shownGold - _g.Gold) < 1) _shownGold = _g.Gold;
        }
        _gold.Text = Num.Short(Math.Floor(_shownGold));
    }

    /// <summary>A coin arrived: the counter and its coin bounce.</summary>
    public void CoinIn()
    {
        _goldIcon.Scale = Vector2.One * 1.3f;
        var t = _goldIcon.CreateTween();
        t.TweenProperty(_goldIcon, "scale", Vector2.One, 0.15f);
    }

    public void ChestsChanged() => Fx.Bump(_chests, 1.3f);
}
