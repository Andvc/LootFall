using Godot;

namespace LootFall.Ui;

/// <summary>
/// Top bar: title, week, day and shop, the Guild's next due, gold, and a Menu button (the same as Esc). Lives on its
/// own layer so screen shake doesn't move it.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Where the purse is on screen: coins you earn fly here.</summary>
    public static readonly Vector2 Purse = new(992, 27);

    Label _lot, _due, _when;
    Control _dueChip;
    Label _gold;
    float _shownGold;

    public override void _Ready()
    {
        Layer = 10;
        AddChild(new Painter(p =>
        {
            p.DrawRect(new Rect2(0, 0, 1280, 54), new Color("#0d0a13") with { A = 0.92f });
            p.DrawRect(new Rect2(0, 54, 1280, 3), Palette.PanelBorder);
            p.DrawRect(new Rect2(0, 57, 1280, 3), new Color(0, 0, 0, 0.6f));
        }));
        AddChild(UiKit.MakeSprite("res://assets/sprites/items/gold_doubloons.png", Purse, 2));
        AddChild(UiKit.MakeLabel("LOOTFALL", 32, Palette.Gold, outline: 4).Place(24, 8, 300, 40));
        AddChild(UiKit.MakeLabel("early access", 16, Palette.Muted).Place(196, 20, 160, 20));
        _lot = UiKit.MakeLabel("", 24, Palette.Text, HorizontalAlignment.Center).Place(330, 12, 420, 30);
        AddChild(_lot);
        // The Guild's next due, and when: it turns orange when you haven't got it yet.
        _dueChip = UiKit.MakePanel(Palette.PanelBorder).Place(760, 5, 200, 46);
        _dueChip.TooltipText = "The Grey Guild's next due. Pay it when Mister Vane comes, or lose the shop.";
        var emblem = UiKit.MakeSprite("res://assets/ui/guild.png", new Vector2(24, 23), 1);
        emblem.Scale = Vector2.One * 0.5f;
        _dueChip.AddChild(emblem);
        _due = UiKit.MakeLabel("", 16, Palette.Gold, outline: 4).Place(44, 2, 150, 20);
        _when = UiKit.MakeLabel("", 16, Palette.Muted).Place(44, 22, 150, 20);
        _dueChip.AddChild(_due);
        _dueChip.AddChild(_when);
        _dueChip.Visible = false;
        AddChild(_dueChip);
        _gold = UiKit.MakeLabel("", 24, Palette.Gold, outline: 4).Place(1014, 12, 140, 30);
        _gold.PivotOffset = new Vector2(30, 15);
        AddChild(_gold);
        AddChild(UiKit.MakeButton("Menu", Palette.PanelBorder.Lightened(0.15f), 16, () => Main.Instance.OpenPause()).Place(1164, 10, 100, 34));
    }

    public void SetDay(int day, int tier) =>
        _lot.Text = day <= 0 ? Core.ShopTiers.Name(tier) : $"Day {day}  -  {Core.ShopTiers.Name(tier)}";

    /// <summary>Shows the Guild's next due: how much, and how soon.</summary>
    public void SetDue(Core.GameState state)
    {
        _dueChip.Visible = state.Day > 0;
        int due = state.NextDue;
        _due.Text = $"Due {due}g";
        _due.AddThemeColorOverride("font_color", state.Gold >= due ? Palette.Gold : Palette.Warn);
        _when.Text = state.PaidThisWeek ? "next week" : state.DaysToDue switch
        {
            0 => "tonight!",
            1 => "tomorrow night",
            int days => $"in {days + 1} days",
        };
        _when.AddThemeColorOverride("font_color", state.DuesTonight && !state.PaidThisWeek ? Palette.Warn : Palette.Muted);
    }

    public void SetGold(int gold, bool animate = true)
    {
        if (!animate)
        {
            _shownGold = gold;
            _gold.Text = $"{gold}g";
            return;
        }
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(v =>
        {
            _shownGold = v;
            _gold.Text = $"{Mathf.RoundToInt(v)}g";
        }), _shownGold, (float)gold, 0.8f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);

        var pulse = CreateTween();
        pulse.TweenProperty(_gold, "scale", Vector2.One * 1.25f, 0.1f);
        pulse.TweenProperty(_gold, "scale", Vector2.One, 0.25f);
    }
}
