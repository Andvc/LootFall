using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The bounty board over the top left of the battle: three goals with their progress and the chest each pays. A
/// finished one flashes gold and its chest flies to the pile (the screen does the flying); the next slides in.
/// </summary>
public partial class BountyBoard : Node2D
{
    public const float W = 372, RowH = 28;

    sealed class Row
    {
        public Control Root;
        public TextureRect Icon;
        public Label Text, Tally;
        public Sprite2D Reward;
        public Bar Progress;
    }

    readonly GameState _g;
    readonly Row[] _rows = new Row[Bounties.Active];

    public BountyBoard(GameState g)
    {
        _g = g;
    }

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.45f), MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, W, 22 + RowH * Bounties.Active));
        AddChild(UiKit.MakeLabel("BOUNTIES", 16, Palette.Gold.Darkened(0.1f), outline: 4).Place(8, 2, 200, 20));
        for (int i = 0; i < _rows.Length; i++)
        {
            var row = new Row { Root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 22 + i * RowH, W, RowH) };
            row.Icon = UiKit.MakeIconRect("atk", 6, 2, 24);
            row.Text = UiKit.MakeLabel("", 16, Palette.Text, outline: 4).Place(34, 3, 220, 20);
            row.Tally = UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Right, outline: 4).Place(232, 3, 92, 20);
            row.Reward = new Sprite2D { Position = new Vector2(W - 22, 14) };
            row.Progress = UiKit.MakeBar(W - 44, 2, Palette.Gold, new Color(0, 0, 0, 0.4f));
            row.Progress.LagColor = Colors.Transparent;
            row.Progress.Position = new Vector2(34, 24);
            row.Root.AddChild(row.Icon);
            row.Root.AddChild(row.Text);
            row.Root.AddChild(row.Tally);
            row.Root.AddChild(row.Reward);
            row.Root.AddChild(row.Progress);
            AddChild(row.Root);
            _rows[i] = row;
        }
        Refresh();
    }

    /// <summary>Where a row's reward chest sits, in screen coordinates.</summary>
    public Vector2 RewardSpot(int slot) => Position + new Vector2(W - 22, 22 + slot * RowH + 14);

    /// <summary>Rows still showing the bounty just finished (they change once it has slid out).</summary>
    readonly bool[] _frozen = new bool[Bounties.Active];

    public void Refresh()
    {
        for (int i = 0; i < _rows.Length && i < _g.Board.Count; i++)
        {
            if (_frozen[i]) continue;
            var b = _g.Board[i];
            var row = _rows[i];
            row.Icon.Texture = UiKit.Icon(b.Icon);
            row.Text.Text = b.Text;
            row.Tally.Text = b.Tally(_g);
            row.Reward.Texture = GD.Load<Texture2D>($"res://assets/ui/chests/{Chests.Get(b.Reward).Skin}.png");
            row.Progress.Set((float)(b.Progress(_g) / b.Target));
        }
    }

    /// <summary>A bounty was finished: its row fills and flashes, a word pops up, and the next one slides in.</summary>
    public void Completed(int slot)
    {
        var row = _rows[slot];
        _frozen[slot] = true;
        row.Progress.Set(1);
        row.Tally.Text = "DONE";
        row.Tally.AddThemeColorOverride("font_color", Palette.Gold);
        var flash = new ColorRect { Color = Palette.Gold with { A = 0.6f }, MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, W, RowH);
        row.Root.AddChild(flash);
        var t = flash.CreateTween();
        t.TweenProperty(flash, "color:a", 0f, 0.6f);
        t.TweenCallback(Callable.From(flash.QueueFree));
        Fx.Float(this, "BOUNTY!", new Vector2(W / 2, 22 + slot * RowH + 10), Palette.Gold, 24, rise: 30, duration: 1.0f);
        var slide = row.Root.CreateTween();
        slide.TweenInterval(0.7f);
        slide.TweenProperty(row.Root, "position:x", -W, 0.15f).SetEase(Tween.EaseType.In);
        slide.TweenCallback(Callable.From(() =>
        {
            _frozen[slot] = false;
            row.Tally.AddThemeColorOverride("font_color", Palette.Muted);
            Refresh();
        }));
        slide.TweenProperty(row.Root, "position:x", 0f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }
}
