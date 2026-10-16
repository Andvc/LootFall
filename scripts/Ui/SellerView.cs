using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>The person across the counter: face, name, what they're saying, and how patient they look.</summary>
public partial class SellerView : Node2D
{
    const int FaceScale = 4;

    readonly SellerDef _seller;
    Node2D _body;
    Label _text, _emote;
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
        _emote = UiKit.MakeLabel("", 24, Palette.Bad, HorizontalAlignment.Center, outline: 4).Place(52, -112, 72, 32);
        _body.AddChild(_emote);

        _bubble = UiKit.MakePanel(Palette.Text);
        _bubble.Place(-112, -244, 420, 128);
        _body.AddChild(_bubble);
        _body.AddChild(new Painter(p =>
            p.DrawColoredPolygon(new[] { new Vector2(-30, -119), new Vector2(0, -119), new Vector2(-20, -96) }, Palette.Text)));
        _text = UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(-94, -232, 384, 104);
        _text.VerticalAlignment = VerticalAlignment.Center;
        _body.AddChild(_text);
    }

    public void Say(string line, Color? color = null)
    {
        _text.Text = line;
        _text.AddThemeColorOverride("font_color", color ?? Palette.Text);
        _bubble.PivotOffset = new Vector2(0, 128);
        _bubble.Scale = new Vector2(0.96f, 0.9f);
        _bubble.CreateTween().TweenProperty(_bubble, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Back);
    }

    /// <summary>0 calm, 1 annoyed, 2 about to walk. The only hint of how much patience is left.</summary>
    public void SetMood(int mood)
    {
        (_emote.Text, var color) = mood switch
        {
            0 => ("", Palette.Text),
            1 => ("...", Palette.Warn),
            _ => ("#!", Palette.Bad),
        };
        _emote.AddThemeColorOverride("font_color", color);
        if (mood > 0) Bob(4);
    }

    /// <summary>A bead of sweat and a twitch.</summary>
    public void Sweat()
    {
        _emote.Text = "!?";
        _emote.AddThemeColorOverride("font_color", new Color("#7fd3ff"));
        Bob(6);
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

    void Bob(float amount)
    {
        var tween = _body.CreateTween();
        tween.TweenProperty(_body, "position:y", -amount, 0.06f);
        tween.TweenProperty(_body, "position:y", amount * 0.5f, 0.08f);
        tween.TweenProperty(_body, "position:y", 0f, 0.08f);
    }
}
