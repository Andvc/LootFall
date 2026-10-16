using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>Uncle Bram, at your elbow on the first day: a face and a short tip about what's in front of you.</summary>
public partial class MentorView : Control
{
    public const float W = 456, H = 128;
    Label _text;

    public override void _Ready()
    {
        Size = new Vector2(W, H);
        MouseFilter = MouseFilterEnum.Ignore;
        PivotOffset = new Vector2(0, H);
        var panel = UiKit.MakePanel(Palette.Gold.Darkened(0.25f)).Place(0, 0, W, H);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(panel);
        AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>("res://assets/sprites/sellers/bram.png"),
            Position = new Vector2(52, 58),
            Scale = Vector2.One * 2,
        });
        AddChild(UiKit.MakeLabel(Lessons.Mentor.ToUpperInvariant(), 16, Palette.Gold, HorizontalAlignment.Center).Place(4, 98, 96, 20));
        _text = UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(104, 10, W - 116, H - 20);
        _text.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_text);
        Visible = false;
    }

    public void Say(string text)
    {
        Visible = true;
        _text.Text = text;
        Scale = new Vector2(0.96f, 0.9f);
        CreateTween().TweenProperty(this, "scale", Vector2.One, 0.14f).SetTrans(Tween.TransitionType.Back);
        Main.Instance.Sfx.Play("blip", 0.8f, -10);
    }
}
