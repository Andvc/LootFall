using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// What gold buys, at the bottom right: the chest's level (with the odds of each rarity from a wooden chest at that
/// level), Might, Vigor and Haste, and Auto-Open once the knight reaches the second zone. Buttons light up when
/// affordable.
/// </summary>
public partial class UpgradePanel : Node2D
{
    public const float W = 440, H = 280;

    sealed class Row
    {
        public Upgrade Upgrade;
        public Control Root;
        public Label Name, Detail;
        public Button Buy;
        public bool WasAffordable;
    }

    readonly GameState _g;
    readonly Row[] _rows = new Row[Upgrades.All.Length];
    Painter _odds;
    Control _oddsTip;

    /// <summary>Something was bought (the screen celebrates a chest level).</summary>
    public event Action<Upgrade> Bought;

    public UpgradePanel(GameState g)
    {
        _g = g;
    }

    public override void _Ready()
    {
        AddChild(UiKit.MakePanel(Palette.PanelBorder).Place(0, 0, W, H));
        AddChild(UiKit.MakeLabel("UPGRADES", 16, Palette.Muted).Place(14, 10, 200, 20));
        float y = 34;
        foreach (var def in Upgrades.All)
        {
            var row = MakeRow(def, y);
            _rows[(int)def.Upgrade] = row;
            y += def.Upgrade == Upgrade.ChestLevel ? 66 : 44;
        }
        Refresh();
    }

    Row MakeRow(UpgradeDef def, float y)
    {
        var row = new Row { Upgrade = def.Upgrade };
        row.Root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(10, y, W - 20, def.Upgrade == Upgrade.ChestLevel ? 62 : 40);
        AddChild(row.Root);
        row.Root.AddChild(UiKit.MakeIconRect(def.Icon, 2, 4, 32));
        row.Name = UiKit.MakeLabel("", 16, Palette.Text).Place(42, 0, 240, 20);
        row.Detail = UiKit.MakeLabel("", 16, Palette.Muted).Place(42, 19, 250, 20);
        row.Root.AddChild(row.Name);
        row.Root.AddChild(row.Detail);
        row.Buy = UiKit.MakeButton("", Palette.Good.Darkened(0.3f), 16, () => Buy(def.Upgrade)).Place(W - 20 - 124, 3, 124, 34);
        row.Buy.AddChild(new TextureRect { Texture = UiKit.Icon("gold"), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore }
            .Place(6, 5, 24, 24));
        row.Buy.Alignment = HorizontalAlignment.Right;
        row.Root.AddChild(row.Buy);
        if (def.Upgrade == Upgrade.ChestLevel)
        {
            _odds = new Painter(DrawOdds) { Position = new Vector2(42, 44) };
            row.Root.AddChild(_odds);
            _oddsTip = new Control { MouseFilter = Control.MouseFilterEnum.Pass }.Place(42, 40, 250, 18);
            row.Root.AddChild(_oddsTip);
        }
        return row;
    }

    /// <summary>A strip split by the chance of each rarity from a wooden chest at the current level.</summary>
    void DrawOdds(Painter p)
    {
        var odds = Chests.Odds(_g.ChestLevel, ChestKind.Wooden);
        const float w = 246, h = 12;
        p.DrawRect(new Rect2(-2, -2, w + 4, h + 4), Colors.Black);
        float x = 0;
        for (int r = 0; r < odds.Length; r++)
        {
            // Rare things get at least a sliver, so you can see they're there.
            float seg = Mathf.Max(odds[r] > 0 ? 3 : 0, (float)odds[r] * w);
            if (x + seg > w) seg = w - x;
            p.DrawRect(new Rect2(x, 0, seg, h), Palette.Of((Rarity)r));
            x += seg;
        }
    }

    void Buy(Upgrade u)
    {
        if (!_g.Buy(u))
        {
            Main.Instance.Sfx.PlayGroup("error", 1f, -10);
            return;
        }
        Main.Instance.Sfx.Play("upgrade", u == Upgrade.ChestLevel ? 0.85f : 1f + 0.02f * Upgrades.Level(_g, u) % 0.3f, -4);
        var row = _rows[(int)u];
        Fx.Burst(this, row.Root.Position + new Vector2(22, 20), Palette.Good, 20, 200);
        Fx.Bump(row.Name, 1.2f);
        Refresh();
        Bought?.Invoke(u);
    }

    public void Refresh()
    {
        foreach (var row in _rows)
        {
            var u = row.Upgrade;
            bool visible = Upgrades.Visible(_g, u);
            row.Root.Visible = visible;
            if (!visible) continue;
            bool open = Upgrades.Open(_g, u);
            double cost = Upgrades.Cost(_g, u);
            int level = Upgrades.Level(_g, u);
            switch (u)
            {
                case Upgrade.ChestLevel:
                    row.Name.Text = $"CHEST LEVEL {level}";
                    int now = Balance.ItemLevel(level, ChestKind.Wooden, _g.MaxStage), next = Balance.ItemLevel(level + 1, ChestKind.Wooden, _g.MaxStage);
                    row.Detail.Text = open ? $"Items Lv {now} > {next}, rarer" : $"Items Lv {now}  (max)";
                    _odds.QueueRedraw();
                    _oddsTip.TooltipText = string.Join("\n", Chests.Odds(level, ChestKind.Wooden)
                        .Select((p, r) => $"{Rarities.All[r].Name}: {(p * 100).ToString(p < 0.01 ? "0.00" : "0.0", System.Globalization.CultureInfo.InvariantCulture)}%"));
                    break;
                case Upgrade.Might:
                    row.Name.Text = $"MIGHT {level}";
                    row.Detail.Text = $"+{Num.Pct(Balance.TrainBonus * level)} attack";
                    break;
                case Upgrade.Vigor:
                    row.Name.Text = $"VIGOR {level}";
                    row.Detail.Text = $"+{Num.Pct(Balance.TrainBonus * level)} health";
                    break;
                case Upgrade.Haste:
                    row.Name.Text = $"HASTE {level}{(open ? "" : " (max)")}";
                    row.Detail.Text = $"+{Num.Pct(Balance.HasteBonus * level)} attack speed";
                    break;
                case Upgrade.AutoOpen:
                    row.Name.Text = "AUTO-OPEN";
                    row.Detail.Text = _g.HasAutoOpen ? $"Stops for {Rarities.Get(_g.AutoStopAt).Name}+" : "Opens chests for you";
                    break;
            }
            bool affordable = open && _g.Gold >= cost;
            row.Buy.Visible = open;
            row.Buy.Disabled = !affordable;
            row.Buy.Text = Num.Short(cost) + " ";
            UiKit.StyleButton(row.Buy, affordable ? Palette.Good.Darkened(0.3f) : new Color("#4a4458"));
            if (affordable && !row.WasAffordable) Fx.Bump(row.Buy, 1.12f);
            row.WasAffordable = affordable;
        }
    }

    /// <summary>The button for an upgrade, for the bot.</summary>
    public Button ButtonFor(Upgrade u) => _rows[(int)u].Buy;

    /// <summary>The middle of an upgrade's row, in screen coordinates.</summary>
    public Vector2 RowCenter(Upgrade u) => _rows[(int)u].Root.GetGlobalRect().GetCenter();
}
