using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// A lantern held to the keyhole. Inside it's dark: move the mouse to aim the light, and whatever it falls on shows,
/// dim and warm, where it's lying. Things that glow show even in the dark. The shop clock runs while you look.
/// Click anywhere (or press a key) to look away.
/// </summary>
public partial class KeyholeView : Node2D
{
    const float ViewScale = 3f;
    const float BeamRadius = 78f;
    static readonly Vector2 Center = new(640, 382);

    readonly List<Thing> _things = new();
    Rect2 _cavity;
    Vector2 _beam;
    Painter _light, _frame;
    double _minuteClock, _sweepLeft, _sweepTime;
    bool _closing;

    /// <summary>A shop minute passed while looking.</summary>
    public event Action MinutePassed;

    /// <summary>Got a proper look at something: its item key, or "stone".</summary>
    public event Action<string> Seen;

    public event Action Closed;

    sealed class Thing
    {
        public ItemDef Item;
        public Vector2 Position;
        public float Radius;
        public Node2D Node;
        public Vector2 Half;
        public double LitFor;
        public bool Seen;
        public string Key => Item?.Key ?? "stone";
    }

    public KeyholeView() { }

    public KeyholeView(IEnumerable<(ItemDef Item, Vector2 Position, float Radius)> contents)
    {
        foreach (var (item, position, radius) in contents)
            _things.Add(new Thing { Item = item, Position = position, Radius = radius });
    }

    public override void _Ready()
    {
        var cavity = ShakeBox.Cavity;
        _cavity = new Rect2(Center + cavity.Position * ViewScale, cavity.Size * ViewScale);
        _beam = _cavity.GetCenter();

        // Blocks clicks from reaching the counter underneath; any click looks away.
        var blocker = new ColorRect { Color = new Color(0, 0, 0, 0.96f), MouseFilter = Control.MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920);
        blocker.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) Close();
        };
        AddChild(blocker);

        AddChild(new Painter(DrawInterior));
        _light = new Painter(DrawLight) { Material = Fx.Additive() };
        AddChild(_light);

        foreach (var thing in _things)
        {
            thing.Node = thing.Item == null ? MakeStone(thing) : MakeItem(thing);
            AddChild(thing.Node);
        }
        LayOut();

        _frame = new Painter(DrawFrame);
        AddChild(_frame);
        AddChild(UiKit.MakeLabel("Through the keyhole", 24, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 76, 1280, 32));
        AddChild(UiKit.MakeLabel("Move the light around. The clock runs while you look. Click to look away.", 16, Palette.Muted,
            HorizontalAlignment.Center).Place(0, 112, 1280, 22));

        Sfx.Play("lantern");
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.2f);
    }

    static Audio.Sfx Sfx => Main.Instance.Sfx;

    Vector2 ToView(Vector2 cavityPoint) => Center + cavityPoint * ViewScale;

    /// <summary>
    /// Things lie on the chest floor, roughly where the shaking left them, nudged apart so each one can be made out
    /// (in the dark a pile would just be a lump).
    /// </summary>
    void LayOut()
    {
        const float gap = 10;
        var row = _things.OrderBy(t => t.Position.X).ToList();
        float floor = _cavity.End.Y - 8, left = _cavity.Position.X + 12, right = _cavity.End.X - 12;
        float x = left;
        foreach (var t in row)
        {
            x = MathF.Max(x, ToView(t.Position).X - t.Half.X);
            t.Node.Position = new Vector2(x + t.Half.X, floor - t.Half.Y);
            x += t.Half.X * 2 + gap;
        }
        float overflow = x - gap - right;
        if (overflow > 0)
            foreach (var t in row)
                t.Node.Position = new Vector2(MathF.Max(left + t.Half.X, t.Node.Position.X - overflow), t.Node.Position.Y);
    }

    Node2D MakeItem(Thing thing)
    {
        var item = thing.Item;
        var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{item.LookKey}.png");
        int scale = Math.Clamp((int)(76f / Math.Max(texture.GetWidth(), texture.GetHeight())), 1, 3);
        thing.Half = new Vector2(texture.GetWidth(), texture.GetHeight()) * scale / 2f;
        float radius = thing.Half.Length() / ViewScale;
        var holder = new Node2D();
        if (item.Glows)
        {
            var glow = new Painter(p =>
            {
                for (int i = 5; i >= 1; i--) p.DrawCircle(Vector2.Zero, radius * ViewScale * (0.6f + i * 0.3f), new Color("#ffb347") with { A = 0.07f });
            }) { Material = Fx.Additive() };
            holder.AddChild(glow);
            var pulse = glow.CreateTween().SetLoops();
            pulse.TweenProperty(glow, "modulate:a", 0.55f, 0.7f).SetTrans(Tween.TransitionType.Sine);
            pulse.TweenProperty(glow, "modulate:a", 1f, 0.7f).SetTrans(Tween.TransitionType.Sine);
        }
        holder.AddChild(new Sprite2D { Texture = texture, Scale = Vector2.One * scale });
        return holder;
    }

    /// <summary>A stone in a rag, tied off with string: lumpy, dull and heavy-looking.</summary>
    static Node2D MakeStone(Thing thing)
    {
        float r = thing.Radius * ViewScale * 0.6f;
        thing.Half = new Vector2(r * 1.05f, r);
        return new Painter(p => DrawStone(p, r));
    }

    static void DrawStone(Painter p, float r)
    {
        var sack = new[]
        {
            new Vector2(-r, r * 0.55f), new Vector2(-r * 1.05f, -r * 0.1f), new Vector2(-r * 0.6f, -r * 0.7f),
            new Vector2(-r * 0.12f, -r * 0.82f), new Vector2(r * 0.12f, -r * 1.05f), new Vector2(r * 0.3f, -r * 0.78f),
            new Vector2(r * 0.85f, -r * 0.5f), new Vector2(r * 1.05f, r * 0.2f), new Vector2(r * 0.7f, r * 0.75f),
        };
        p.DrawColoredPolygon(sack, new Color("#7d6a4c"));
        p.DrawColoredPolygon(new[] { new Vector2(-r * 0.7f, -r * 0.2f), new Vector2(-r * 0.2f, -r * 0.55f), new Vector2(r * 0.1f, -r * 0.2f), new Vector2(-r * 0.4f, r * 0.1f) },
            new Color("#9a8562"));
        p.DrawLine(new Vector2(-r * 0.25f, -r * 0.78f), new Vector2(r * 0.35f, -r * 0.82f), new Color("#4a3a26"), 4);
        for (int i = 0; i < 4; i++)
            p.DrawLine(new Vector2(-r * 0.8f + i * r * 0.45f, r * 0.5f), new Vector2(-r * 0.6f + i * r * 0.45f, r * 0.1f), new Color("#5e4e36"), 2);
    }

    /// <summary>Sweeps the light across everything inside for a while (used by the autoplay bot).</summary>
    public void AutoSweep(double seconds)
    {
        _sweepLeft = seconds;
        _sweepTime = 0;
    }

    public override void _Process(double delta)
    {
        if (_closing) return;
        Vector2 aim;
        if (_sweepLeft > 0)
        {
            _sweepLeft -= delta;
            _sweepTime += delta;
            var targets = _things.Count > 0 ? _things.Select(t => t.Node.Position).OrderBy(v => v.X).ToList() : new List<Vector2> { _cavity.GetCenter() };
            float u = (float)(_sweepTime / 0.9) % targets.Count;
            int i = (int)u;
            aim = targets[i].Lerp(targets[(i + 1) % targets.Count], Mathf.SmoothStep(0.5f, 1f, u - i));
        }
        else
        {
            aim = GetGlobalMousePosition();
        }
        aim = new Vector2(Mathf.Clamp(aim.X, _cavity.Position.X, _cavity.End.X), Mathf.Clamp(aim.Y, _cavity.Position.Y, _cavity.End.Y));
        _beam = _beam.Lerp(aim, 1f - MathF.Exp(-14f * (float)delta));
        _light.QueueRedraw();

        foreach (var thing in _things)
        {
            float distance = thing.Node.Position.DistanceTo(_beam) - thing.Half.X * 0.5f;
            float lit = Mathf.Clamp(1f - (distance - 12f) / BeamRadius, 0f, 1f);
            if (thing.Item is { Glows: true }) lit = MathF.Max(lit, 0.55f);
            float warm = lit * lit;
            thing.Node.Modulate = new Color(warm, warm * 0.86f, warm * 0.68f);
            if (lit > 0.6f && !thing.Seen)
            {
                thing.LitFor += delta;
                if (thing.LitFor > 0.3)
                {
                    thing.Seen = true;
                    Seen?.Invoke(thing.Key);
                }
            }
        }

        _minuteClock += delta * ShopClock.LanternMinutesPerSecond;
        while (_minuteClock >= 1 && !_closing)
        {
            _minuteClock -= 1;
            MinutePassed?.Invoke();
        }
    }

    void DrawInterior(Painter p)
    {
        p.DrawRect(_cavity, new Color("#0c0705"));
        for (float y = _cavity.Position.Y + 48; y < _cavity.End.Y; y += 48)
            p.DrawRect(new Rect2(_cavity.Position.X, y, _cavity.Size.X, 3), new Color("#050302"));
    }

    void DrawLight(Painter p)
    {
        for (int i = 6; i >= 1; i--)
            p.DrawCircle(_beam, BeamRadius * (0.35f + i * 0.17f), new Color("#ffb060") with { A = 0.05f });
        p.DrawCircle(_beam, BeamRadius * 0.3f, new Color("#ffc890") with { A = 0.06f });
    }

    /// <summary>Thick dark wood around the cavity hides the light's spill and frames the view like a keyhole would.</summary>
    void DrawFrame(Painter p)
    {
        var dark = new Color("#000000");
        const float t = 400;
        p.DrawRect(new Rect2(_cavity.Position.X - t, _cavity.Position.Y - t, _cavity.Size.X + 2 * t, t), dark);
        p.DrawRect(new Rect2(_cavity.Position.X - t, _cavity.End.Y, _cavity.Size.X + 2 * t, t), dark);
        p.DrawRect(new Rect2(_cavity.Position.X - t, _cavity.Position.Y, t, _cavity.Size.Y), dark);
        p.DrawRect(new Rect2(_cavity.End.X, _cavity.Position.Y, t, _cavity.Size.Y), dark);
        var rim = new Color("#6e4520");
        p.DrawRect(new Rect2(_cavity.Position.X - 9, _cavity.Position.Y - 9, _cavity.Size.X + 18, 9), rim);
        p.DrawRect(new Rect2(_cavity.Position.X - 9, _cavity.End.Y, _cavity.Size.X + 18, 9), rim);
        p.DrawRect(new Rect2(_cavity.Position.X - 9, _cavity.Position.Y, 9, _cavity.Size.Y), rim);
        p.DrawRect(new Rect2(_cavity.End.X, _cavity.Position.Y, 9, _cavity.Size.Y), rim);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void Close()
    {
        if (_closing || !IsInsideTree()) return;
        _closing = true;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.Transparent, 0.15f);
        tween.TweenCallback(Callable.From(() =>
        {
            Closed?.Invoke();
            QueueFree();
        }));
    }
}
