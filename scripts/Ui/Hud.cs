using Godot;

namespace LootFall.Ui;

/// <summary>Top bar: title, lot number and gold. Lives on its own layer so screen shake doesn't move it.</summary>
public partial class Hud : CanvasLayer
{
    Label _lot;
    Label _gold;
    float _shownGold;

    public override void _Ready()
    {
        Layer = 10;
        AddChild(new Painter(p =>
        {
            p.DrawRect(new Rect2(0, 0, 1280, 52), new Color("#0d0a13") with { A = 0.92f });
            p.DrawLine(new Vector2(0, 52), new Vector2(1280, 52), Palette.PanelBorder, 2);
            p.DrawCircle(new Vector2(1094, 26), 9, Palette.Gold);
            p.DrawCircle(new Vector2(1094, 26), 5, Palette.Gold.Darkened(0.35f));
        }));
        AddChild(UiKit.MakeLabel("LOOTFALL", 24, Palette.Gold).Place(24, 10, 300, 32));
        AddChild(UiKit.MakeLabel("prototype", 13, Palette.Muted).Place(162, 20, 120, 20));
        _lot = UiKit.MakeLabel("", 18, Palette.Text, HorizontalAlignment.Center).Place(440, 13, 400, 28);
        AddChild(_lot);
        _gold = UiKit.MakeLabel("", 22, Palette.Gold).Place(1110, 11, 160, 32);
        _gold.PivotOffset = new Vector2(30, 16);
        AddChild(_gold);
    }

    public void SetLot(int lot) => _lot.Text = $"Lot {lot}";

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
