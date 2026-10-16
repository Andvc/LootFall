using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The shop scale, built into the counter: a brass pan on top, a dial on the front. It's set so an empty chest reads
/// zero, so the needle shows what the contents weigh. It's a measurement, not a verdict: what makes the weight is
/// for the player to work out.
/// </summary>
public partial class ScaleView : Node2D
{
    /// <summary>The dial's full sweep, in pounds.</summary>
    const float MaxPounds = 12f;
    const float PanHalf = 88, BodyHalf = 64, BodyHeight = 64;
    const float StartAngle = Mathf.Pi * 0.75f, Sweep = Mathf.Pi * 1.5f;
    static readonly Vector2 DialCenter = new(0, 38);
    const float DialRadius = 26;

    /// <summary>Lights the pan while a chest is carried over it.</summary>
    public bool Highlight;

    /// <summary>Where a chest's seam sits when it rests on the pan (relative to this node).</summary>
    public const float ChestOffsetY = -63f;

    /// <summary>Fires when the needle has settled on a load.</summary>
    public event Action<float> Settled;

    Painter _painter;
    Label _reading;
    float _needle, _needleVel, _target;
    float? _load;
    double _settleTimer = -1;

    public override void _Ready()
    {
        _painter = new Painter(DrawScale);
        AddChild(_painter);
        _reading = UiKit.MakeLabel("", 24, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(-110, 90, 220, 30);
        AddChild(_reading);
        AddChild(UiKit.MakeLabel("SCALE", 16, Palette.Muted, HorizontalAlignment.Center).Place(-60, 120, 120, 20));
    }

    /// <summary>Puts a load on the pan (pounds of contents), or takes it off (null).</summary>
    public void SetLoad(float? pounds)
    {
        _load = pounds;
        _target = pounds ?? 0f;
        _reading.Text = "";
        _settleTimer = pounds.HasValue ? 0.9 : -1;
        if (pounds.HasValue)
        {
            Main.Instance.Sfx.PlayGroup("scale_pan", 0.9f, -4);
            Main.Instance.Sfx.Play("scale", 1f, -6);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        // A sprung needle: it swings past the reading and settles, which sells the weight.
        float goal = Mathf.Clamp(_target / MaxPounds, 0f, 1.04f);
        _needleVel += (goal - _needle) * 90f * dt;
        _needleVel *= MathF.Exp(-5f * dt);
        _needle += _needleVel * dt;

        if (_settleTimer > 0)
        {
            _settleTimer -= delta;
            if (_settleTimer <= 0 && _load is float load)
            {
                _reading.Text = $"{Describe.Pounds(load)} inside";
                _reading.Scale = Vector2.One * 1.2f;
                _reading.PivotOffset = new Vector2(110, 15);
                _reading.CreateTween().TweenProperty(_reading, "scale", Vector2.One, 0.2f);
                Settled?.Invoke(load);
            }
        }
        _painter.QueueRedraw();
    }

    void DrawScale(Painter p)
    {
        var brass = new Color("#c9a24a");
        var brassDark = new Color("#8a6a2a");
        var iron = new Color("#3a3644");
        var ironLight = new Color("#57526a");

        // Body standing on the counter, with a darker plinth.
        p.DrawRect(new Rect2(-BodyHalf, 8, BodyHalf * 2, BodyHeight), iron);
        p.DrawRect(new Rect2(-BodyHalf, 8, BodyHalf * 2, 3), ironLight);
        p.DrawRect(new Rect2(-BodyHalf - 6, BodyHeight + 2, BodyHalf * 2 + 12, 6), new Color("#25212d"));
        // Pan: brass plate with a lip; glows when a chest is held over it.
        p.DrawRect(new Rect2(-12, 2, 24, 8), brassDark);
        p.DrawRect(new Rect2(-PanHalf, -3, PanHalf * 2, 6), Highlight ? brass.Lightened(0.35f) : brass);
        p.DrawRect(new Rect2(-PanHalf, 3, PanHalf * 2, 3), brassDark);
        if (Highlight)
            p.DrawRect(new Rect2(-PanHalf, -9, PanHalf * 2, 3), Palette.Gold with { A = 0.6f });

        // Dial: brass rim, cream face, a tick per pound, a red needle.
        p.DrawCircle(DialCenter, DialRadius + 4, brassDark);
        p.DrawCircle(DialCenter, DialRadius + 2, brass);
        p.DrawCircle(DialCenter, DialRadius, new Color("#efe3c4"));
        for (int lb = 0; lb <= (int)MaxPounds; lb++)
        {
            var dir = Vector2.FromAngle(StartAngle + Sweep * lb / MaxPounds);
            float inner = lb % 4 == 0 ? DialRadius - 8 : DialRadius - 4;
            p.DrawLine(DialCenter + dir * inner, DialCenter + dir * (DialRadius - 1), new Color("#4a3a2a"), lb % 4 == 0 ? 3 : 2);
        }
        var needle = Vector2.FromAngle(StartAngle + Sweep * _needle);
        p.DrawLine(DialCenter, DialCenter + needle * (DialRadius - 3), new Color("#c0392b"), 3);
        p.DrawCircle(DialCenter, 4, brassDark);
    }
}
