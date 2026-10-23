using System;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The chest at the bottom middle, the pile of unopened ones under it, and the opening itself: click it (or Space) and
/// it hops, glows, and climbs through the rarity colours, one ting per step, until it bursts and the item rises into
/// a <see cref="RevealCard"/>. Commons pop in half a second; a mythic takes its time. Clicking again hurries it.
/// </summary>
public partial class ChestPanel : Node2D
{
    public const float W = 360, H = 280;
    static readonly Vector2 ChestSpot = new(180, 122);

    enum State { Idle, Opening, Deciding }

    readonly GameState _g;
    State _state = State.Idle;
    Sprite2D _chest, _lid;
    Painter _glow;
    Color _glowColor = Colors.White;
    float _glowAmount, _bob, _shakeAmount;
    Label _hint, _title;
    readonly Label[] _counts = new Label[Chests.Count];
    readonly Node2D[] _pileIcons = new Node2D[Chests.Count];
    Node2D _pile;
    RevealCard _card;
    Node2D _layer;
    ChestKind _shown = ChestKind.Wooden;
    bool _hurry;
    Button _auto, _stop;

    /// <summary>The player took the item (or sold it): the screen does the flying.</summary>
    public event Action<Opening, double, Vector2> Equipped;
    public event Action<Opening, double, Vector2> Sold;

    /// <summary>A chest opened (for the bot and the screenshots).</summary>
    public event Action<Opening> Revealed;

    public bool Busy => _state != State.Idle;

    /// <summary>Chests of each kind still falling from the battle: the pile counts them once they land.</summary>
    public readonly int[] Falling = new int[Chests.Count];

    int Landed(int kind) => Math.Max(0, _g.Chests[kind] - Falling[kind]);

    /// <summary>Chests on the pile (not counting ones still in the air).</summary>
    public int OnPile
    {
        get
        {
            int n = 0;
            for (int k = 0; k < Chests.Count; k++) n += Landed(k);
            return n;
        }
    }
    public bool Deciding => _state == State.Deciding;
    public RevealCard Card => _card;

    public ChestPanel(GameState g)
    {
        _g = g;
    }

    /// <summary>Where chests land when they fall in from the battle, in screen coordinates.</summary>
    public Vector2 PileSpot => Position + new Vector2(W / 2, 214);

    public Vector2 ChestCenter => Position + ChestSpot;

    public override void _Ready()
    {
        AddChild(UiKit.MakePanel(Palette.PanelBorder).Place(0, 0, W, H));
        _title = UiKit.MakeLabel("", 16, Palette.Muted).Place(14, 10, 200, 20);
        AddChild(_title);

        _glow = new Painter(p =>
        {
            if (_glowAmount <= 0.01f) return;
            for (int i = 5; i >= 1; i--)
                p.DrawCircle(Vector2.Zero, 40 + 26 * i * _glowAmount, _glowColor with { A = 0.08f * _glowAmount });
        }) { Position = ChestSpot + new Vector2(0, 8), Material = Fx.Additive() };
        AddChild(_glow);

        _chest = new Sprite2D { Position = ChestSpot, Scale = Vector2.One * 2 };
        AddChild(_chest);

        // The whole chest is the button.
        var click = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand }
            .Place(ChestSpot.X - 130, ChestSpot.Y - 84, 260, 168);
        click.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        click.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        click.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        click.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        click.Pressed += Primary;
        AddChild(click);

        _pile = new Node2D { Position = new Vector2(0, 214) };
        AddChild(_pile);
        for (int k = 0; k < Chests.Count; k++)
        {
            var icon = new Node2D();
            icon.AddChild(new Sprite2D { Texture = GD.Load<Texture2D>($"res://assets/ui/chests/{Chests.All[k].Skin}.png") });
            _counts[k] = UiKit.MakeLabel("", 16, Palette.Text, outline: 4).Place(20, -10, 60, 20);
            icon.AddChild(_counts[k]);
            _pileIcons[k] = icon;
            _pile.AddChild(icon);
        }
        _hint = UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 244, W, 20);
        AddChild(_hint);

        _auto = UiKit.MakeButton("AUTO", new Color("#3a7a8a"), 16, () =>
        {
            _g.AutoOpen = !_g.AutoOpen;
            Main.Instance.Sfx.PlayGroup("toggle");
            Refresh();
        }).Place(W - 92, 6, 82, 28);
        AddChild(_auto);
        // What auto-open stops for: click to step through rare, epic, legendary and mythic.
        _stop = UiKit.MakeButton("", new Color("#4a4458"), 16, () =>
        {
            _g.AutoStopAt = _g.AutoStopAt >= Rarity.Mythic ? Rarity.Rare : (Rarity)((int)_g.AutoStopAt + 1);
            Main.Instance.Sfx.PlayGroup("toggle");
            Refresh();
        }).Place(W - 196, 6, 100, 28);
        _stop.TooltipText = "Auto-open stops and shows you anything this rare or better";
        AddChild(_stop);

        _layer = new Node2D { ZIndex = 30 };
        AddChild(_layer);
        Refresh();
        if (_g.Pending != null) ShowCard(_g.Pending, animate: false);
    }

    /// <summary>Space, Enter, or a click on the chest: open one, hurry an opening along, or take the recommended choice.</summary>
    public void Primary()
    {
        switch (_state)
        {
            case State.Idle: OpenNext(); break;
            case State.Opening: _hurry = true; break;
            case State.Deciding:
                if (_card.Opening.Upgrade) DoEquip();
                else DoSell();
                break;
        }
    }

    public void PressEquip()
    {
        if (_state == State.Deciding) DoEquip();
    }

    public void PressSell()
    {
        if (_state == State.Deciding) DoSell();
    }

    public void OpenNext()
    {
        if (_state != State.Idle) return;
        var opening = _g.Open();
        if (opening == null)
        {
            Fx.Bump(_hint, 1.15f);
            Main.Instance.Sfx.PlayGroup("error", 1f, -10);
            return;
        }
        PlayOpening(opening);
    }

    /// <summary>Auto-open found something big and left it: open it with the full show.</summary>
    public void PresentHeld(Opening opening)
    {
        if (_state != State.Idle) return;
        PlayOpening(opening);
    }

    /// <summary>A chest from the battle landed on the pile.</summary>
    public void ChestArrived(ChestKind kind)
    {
        Refresh();
        Fx.Bump(_pileIcons[(int)kind], 1.4f, 0.25f);
        if (_state == State.Idle) _bob = -0.6f;
    }

    // ------------------------------------------------------------------------------------------------- the opening

    async void PlayOpening(Opening o)
    {
        _state = State.Opening;
        _hurry = false;
        var sfx = Main.Instance.Sfx;
        var main = Main.Instance;
        int rarity = (int)o.Item.Rarity;
        SetChest(o.Kind, open: false);

        // The hop: it jumps, the lid strains, light leaks out.
        sfx.PlayGroup("creak", 1.1f, -6);
        _glowColor = Colors.White;
        Squash(1.15f, 0.85f, 0.08f);
        _shakeAmount = 2;
        await Glow(0.35f, 0.12f);
        if (!IsInsideTree()) return;

        // Up through the rarities: one step for each, a little higher and harder each time.
        for (int r = 1; r <= rarity; r++)
        {
            bool lucky = r > (int)o.Rolled;
            if (lucky && !_hurry)
            {
                await Wait(0.15f);
                Fx.Float(_layer, "LUCKY!", ChestSpot + new Vector2(0, -90), Palette.Good, 32, rise: 40, duration: 1.2f);
                sfx.Play("sparkle", 1.2f, -2);
            }
            var c = Palette.Of((Rarity)r);
            _glowColor = c;
            _shakeAmount = 2 + r * 1.6f;
            sfx.Play("ting", 0.85f + 0.13f * r, -4 + r);
            if (r >= 3) sfx.Play("rumble", 1f + 0.1f * r, -10 + r * 2);
            Fx.Burst(_layer, ChestSpot, c, 10 + r * 8, 160 + r * 40, lifetime: 0.6f);
            Squash(1f + 0.05f * r, 1f - 0.04f * r, 0.06f);
            main.Shake(0.04f * r);
            if (r >= 4 && !_hurry) Fx.Lightning(_layer, ChestSpot, c, 3 + r, 90 + r * 12, 0.25f);
            if (!_hurry) await Glow(0.45f + 0.1f * r, 0.2f + 0.05f * r);
            if (!IsInsideTree()) return;
        }

        // Burst.
        _shakeAmount = 0;
        var color = Palette.Of(o.Item.Rarity);
        SetChest(o.Kind, open: true);
        sfx.PlayGroup("latch", 1f, -4);
        sfx.Play("burst", 1.25f - 0.05f * rarity, -8 + rarity * 1.5f);
        sfx.PlayGroup("lid", 1.1f, -8);
        Squash(0.85f, 1.2f, 0.1f);
        _glowColor = color;
        _glowAmount = 1;
        Fx.Burst(_layer, ChestSpot + new Vector2(0, -20), color, 30 + rarity * 20, 300 + rarity * 60, spread: 70, gravity: 500, lifetime: 1f);
        Fx.Ring(_layer, ChestSpot, color, 120 + rarity * 30, 0.45f, 4 + rarity);
        main.Flash(color, 0.06f + 0.06f * rarity, 0.3f + 0.05f * rarity);
        main.Shake(0.05f + 0.07f * rarity);
        if (rarity >= (int)Rarity.Rare) Fx.Pillar(_layer, ChestSpot + new Vector2(0, 20), color, 70 + rarity * 10, 320, 0.8f + 0.2f * rarity);
        if (rarity >= (int)Rarity.Epic) sfx.Play("chime", 0.9f + 0.05f * rarity, -6);
        if (rarity >= (int)Rarity.Legendary)
        {
            sfx.Play(rarity >= (int)Rarity.Mythic ? "mythic" : "fanfare", 1f, -2);
            sfx.PlayGroup("fanfare", 1f, -8);
            Fx.Confetti(_layer, ChestSpot, 60 + 40 * (rarity - 4));
            main.HitStop(0.08 + 0.06 * (rarity - 4));
        }
        else if (rarity >= (int)Rarity.Rare) sfx.Play("sparkle", 1f, -6);

        // The item rises out of the open chest.
        var rising = UiKit.MakeItemIcon(o.Item, ChestSpot + new Vector2(0, -10), 96);
        rising.Scale = Vector2.One * 0.3f;
        _layer.AddChild(rising);
        var t = rising.CreateTween().SetParallel();
        t.TweenProperty(rising, "position", ChestSpot + new Vector2(0, -70), _hurry ? 0.12f : 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(rising, "scale", Vector2.One, _hurry ? 0.12f : 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await Wait(_hurry ? 0.12f : 0.32f + 0.05f * rarity);
        if (!IsInsideTree()) return;
        rising.QueueFree();
        ShowCard(o, animate: true);
        Revealed?.Invoke(o);
        main.Moment($"open_{o.Item.Rarity.ToString().ToLowerInvariant()}", 0.3);
    }

    void ShowCard(Opening o, bool animate)
    {
        _state = State.Deciding;
        // The card stands a little taller than the panel, over the bottom of the battle.
        _card = new RevealCard(o, DoEquip, DoSell) { Position = new Vector2(0, H - RevealCard.H - 2), ZIndex = 40 };
        AddChild(_card);
        if (animate) _card.Appear();
        _chest.Visible = false;
        _glowAmount = 0;
        _glow.QueueRedraw();
        _hint.Visible = false;
        _pile.Visible = false;
        _auto.Visible = false;
        _stop.Visible = false;
    }

    void DoEquip()
    {
        if (_state != State.Deciding || _g.Pending == null) return;
        var o = _card.Opening;
        var from = _card.GlobalPosition + RevealCard.IconSpot;
        double sold = _g.Equip();
        CloseCard();
        Equipped?.Invoke(o, sold, from);
    }

    void DoSell()
    {
        if (_state != State.Deciding || _g.Pending == null) return;
        var o = _card.Opening;
        var from = _card.GlobalPosition + RevealCard.IconSpot;
        double gold = _g.Sell();
        CloseCard();
        Sold?.Invoke(o, gold, from);
    }

    void CloseCard()
    {
        _card?.QueueFree();
        _card = null;
        _state = State.Idle;
        _chest.Visible = true;
        _hint.Visible = true;
        _pile.Visible = true;
        Refresh();
        // The next chest drops in from above.
        if (_g.ChestTotal > 0)
        {
            _chest.Position = ChestSpot + new Vector2(0, -60);
            _chest.Modulate = Colors.Transparent;
            var t = _chest.CreateTween().SetParallel();
            t.TweenProperty(_chest, "position", ChestSpot, 0.16f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            t.TweenProperty(_chest, "modulate", Colors.White, 0.08f);
            t.Chain().TweenCallback(Callable.From(() =>
            {
                Squash(1.12f, 0.88f, 0.06f);
                Main.Instance.Sfx.PlayGroup("chest_land", 1.2f, -14);
            }));
        }
    }

    // ------------------------------------------------------------------------------------------------- looks

    void SetChest(ChestKind kind, bool open)
    {
        _shown = kind;
        _chest.Texture = GD.Load<Texture2D>($"res://assets/sprites/chests/{Chests.Get(kind).Skin}_{(open ? "open" : "closed")}.png");
    }

    /// <summary>Shows the next chest and the pile's counts, or an empty chest and a hint when there are none.</summary>
    public void Refresh()
    {
        if (_state == State.Idle)
        {
            var next = _g.NextChest;
            SetChest(next ?? ChestKind.Wooden, open: next == null);
            _chest.SelfModulate = next == null ? new Color(0.45f, 0.45f, 0.5f) : Colors.White;
            _glowColor = next is ChestKind k && k != ChestKind.Wooden ? Palette.OfChest(k) : Colors.White;
            _hint.Text = next == null ? "Defeat monsters to earn chests" : OnPile <= 1 ? "Click or SPACE to open" : $"Click or SPACE to open  ({OnPile})";
        }
        _title.Text = $"CHEST LEVEL {_g.ChestLevel}";
        _auto.Visible = _g.HasAutoOpen && _state == State.Idle;
        _auto.Text = _g.AutoOpen ? "AUTO ON" : "AUTO OFF";
        UiKit.StyleButton(_auto, _g.AutoOpen ? new Color("#2f8a6a") : new Color("#4a4458"));
        _stop.Visible = _auto.Visible && _g.AutoOpen;
        _stop.Text = "STOP " + _g.AutoStopAt switch { Rarity.Rare => "RARE+", Rarity.Epic => "EPIC+", Rarity.Legendary => "LEG+", _ => "MYTHIC" };
        UiKit.StyleButton(_stop, Palette.Of(_g.AutoStopAt).Darkened(0.45f));

        // The pile: one icon per kind there is, with how many.
        int shown = 0;
        for (int k = 0; k < Chests.Count; k++) if (Landed(k) > 0) shown++;
        float spacing = 80, x = W / 2 - (shown - 1) * spacing / 2 - 12;
        for (int k = Chests.Count - 1; k >= 0; k--)
        {
            bool has = Landed(k) > 0;
            _pileIcons[k].Visible = has;
            if (!has) continue;
            _pileIcons[k].Position = new Vector2(x, 0);
            _counts[k].Text = "x" + Landed(k);
            x += spacing;
        }
    }

    void Squash(float sx, float sy, float time)
    {
        var t = _chest.CreateTween();
        t.TweenProperty(_chest, "scale", new Vector2(2 * sx, 2 * sy), time).SetTrans(Tween.TransitionType.Quad);
        t.TweenProperty(_chest, "scale", Vector2.One * 2, time * 2.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    async Task Glow(float to, float time)
    {
        var t = CreateTween();
        t.TweenMethod(Callable.From<float>(v =>
        {
            _glowAmount = v;
            _glow.QueueRedraw();
        }), _glowAmount, to, _hurry ? 0.01f : time);
        await Wait(_hurry ? 0.01f : time);
    }

    async Task Wait(float seconds)
    {
        if (seconds <= 0.011f || !IsInsideTree()) return;
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_state == State.Opening && _shakeAmount > 0)
        {
            _chest.Position = ChestSpot + new Vector2((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0)) * _shakeAmount;
            _chest.Rotation = (float)GD.RandRange(-1.0, 1.0) * _shakeAmount * 0.008f;
        }
        else if (_state == State.Idle)
        {
            _chest.Rotation = 0;
            _bob += dt * 2.4f;
            // A gentle breath while there's something to open; special chests glow.
            if (_g.ChestTotal > 0 && _chest.Position.Y >= ChestSpot.Y - 0.5f)
                _chest.Position = ChestSpot + new Vector2(0, Mathf.Round(Mathf.Sin(_bob) * 2));
            float target = _g.NextChest is ChestKind k && k != ChestKind.Wooden ? 0.45f + 0.1f * Mathf.Sin(_bob * 1.5f) : 0;
            _glowAmount = Mathf.MoveToward(_glowAmount, target, dt * 2);
            _glow.QueueRedraw();
        }
    }
}
