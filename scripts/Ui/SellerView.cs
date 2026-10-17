using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>The person across the counter: face, name, and what they're saying, in a bubble under them.</summary>
public partial class SellerView : Node2D
{
    const int FaceScale = 4;
    public const float BubbleW = 280, BubbleH = 150;

    readonly SellerDef _seller;
    Node2D _body;
    Label _text;
    Panel _bubble;

    public SellerView() { }

    public SellerView(SellerDef seller)
    {
        _seller = seller;
    }

    public override void _Ready()
    {
        _body = new Node2D();
        AddChild(_body);
        _body.AddChild(UiKit.MakePanel(Palette.PanelBorder).Place(-88, -88, 176, 176));
        _body.AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>($"res://assets/sprites/sellers/{_seller.Key}.png"),
            Scale = Vector2.One * FaceScale,
        });
        _body.AddChild(UiKit.MakeLabel(_seller.Name, 24, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(-140, 94, 280, 30));

        _bubble = UiKit.MakePanel(Palette.Text);
        _bubble.Place(-BubbleW / 2, 136, BubbleW, BubbleH);
        _body.AddChild(_bubble);
        _body.AddChild(new Painter(p =>
            p.DrawColoredPolygon(new[] { new Vector2(-14, 139), new Vector2(14, 139), new Vector2(0, 124) }, Palette.Text)));
        _text = UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(-BubbleW / 2 + 16, 146, BubbleW - 32, BubbleH - 20);
        _text.VerticalAlignment = VerticalAlignment.Center;
        _body.AddChild(_text);
    }

    public void Say(string line, Color? color = null)
    {
        _text.Text = line;
        _text.AddThemeColorOverride("font_color", color ?? Palette.Text);
        _bubble.PivotOffset = new Vector2(BubbleW / 2, 0);
        _bubble.Scale = new Vector2(0.96f, 0.9f);
        _bubble.CreateTween().TweenProperty(_bubble, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Back);
    }

    public void Enter()
    {
        _body.Position = new Vector2(-420, 0);
        _body.CreateTween().TweenProperty(_body, "position:x", 0f, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public Tween Leave()
    {
        var tween = _body.CreateTween();
        tween.TweenProperty(_body, "position:x", -460f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        return tween;
    }

    /// <summary>A little hop, e.g. when a deal is struck.</summary>
    public void Bob(float amount = 6)
    {
        var tween = _body.CreateTween();
        tween.TweenProperty(_body, "position:y", -amount, 0.06f);
        tween.TweenProperty(_body, "position:y", amount * 0.5f, 0.08f);
        tween.TweenProperty(_body, "position:y", 0f, 0.08f);
    }
}
