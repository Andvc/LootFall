using Godot;

namespace LootFall.Ui;

/// <summary>
/// Top bar: title, day and shop, gold, and a Menu button (the same as Esc). Lives on its own layer so screen shake
/// doesn't move it.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Where the purse is on screen: coins you earn fly here.</summary>
    public static readonly Vector2 Purse = new(992, 27);

    Label _lot;
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
        _lot = UiKit.MakeLabel("", 24, Palette.Text, HorizontalAlignment.Center).Place(440, 12, 400, 30);
        AddChild(_lot);
        _gold = UiKit.MakeLabel("", 24, Palette.Gold, outline: 4).Place(1014, 12, 140, 30);
        _gold.PivotOffset = new Vector2(30, 15);
        AddChild(_gold);
        AddChild(UiKit.MakeButton("Menu", Palette.PanelBorder.Lightened(0.15f), 16, () => Main.Instance.OpenPause()).Place(1164, 10, 100, 34));
    }

    public void SetDay(int day, int tier) => _lot.Text = $"Day {day}  -  {Core.ShopTiers.Name(tier)}";

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
