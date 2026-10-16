using System;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// One seller, one sealed chest. Shake it (free), knock, peek or read the lock (each costs shop time), watch the
/// seller, then make offers. Nothing on this screen states what the chest is worth.
/// </summary>
public partial class CounterPhase : Phase
{
    static readonly Vector2 ChestRest = new(600, 450);
    static readonly Rect2 DragBounds = new(440, 210, 340, 240);
    const float PanelX = 844, PanelW = 384;

    Visit _visit;
    SellerView _seller;
    ShakeBox _shake;
    Label _notes, _ask, _wasAsk, _footer;
    HSlider _slider;
    Button _knock, _peek, _lock, _offer, _pay, _pass;
    KeyholeView _keyhole;
    bool _busy;

    public Visit Visit => _visit;
    public ShakeBox Shake => _shake;
    public bool Concluded => _visit.Outcome != VisitOutcome.Haggling;
    public bool Busy => _busy || _keyhole != null;

    public override void _Ready()
    {
        _visit = State.Current;
        Add(UiKit.MakeBackdrop("cellar_night", 0.75f));
        Add(new Painter(DrawCounter));

        _seller = Add(new SellerView(_visit.Seller) { Position = new Vector2(176, 330) });
        _shake = Add(new ShakeBox(_visit.Chest, DragBounds) { Position = ChestRest });

        Add(UiKit.MakeLabel(_visit.Seller.Name, 24, Palette.Gold, outline: 4).Place(40, 534, 360, 30));
        Add(UiKit.MakeLabel("Drag the chest to shake it", 16, Palette.Muted, outline: 4).Place(440, 540, 340, 20));
        BuildQueue();
        BuildPanel();

        Sfx.PlayGroup("door_open", 1f, -6);
        _seller.Enter();
        _seller.Say(_visit.Pitch);
        Game.Hud.SetClock(State.Day, State.Minute);
        Refresh();
    }

    void DrawCounter(Painter p)
    {
        const float top = 512, right = 816;
        p.DrawRect(new Rect2(0, top, right, 18), new Color("#93602f"));
        p.DrawRect(new Rect2(0, top, right, 3), new Color("#b07a40"));
        p.DrawRect(new Rect2(0, top + 18, right, 720 - top), new Color("#5e3718"));
        for (float x = 0; x < right; x += 96) p.DrawRect(new Rect2(x, top + 18, 3, 720 - top), new Color("#4a2a14"));
        p.DrawRect(new Rect2(0, top + 18, right, 6), new Color(0, 0, 0, 0.35f));
    }

    void BuildQueue()
    {
        if (State.Queue.Count == 0) return;
        Add(UiKit.MakeLabel("Waiting", 16, Palette.Muted, outline: 4).Place(40, 600, 120, 20));
        for (int i = 0; i < State.Queue.Count; i++)
        {
            var face = Add(UiKit.MakeSprite($"res://assets/sprites/sellers/{State.Queue[i].Seller.Key}.png", new Vector2(60 + i * 46, 652), 1));
            face.Modulate = new Color(0.8f, 0.8f, 0.85f);
        }
    }

    void BuildPanel()
    {
        Add(UiKit.MakePanel().Place(820, 66, 432, 640));

        Add(Header("INSPECT", PanelX, 82));
        _knock = Add(UiKit.MakeButton($"Knock on the bottom   {ShopClock.Knock}m", Palette.Gold, 16, Knock).Place(PanelX, 106, PanelW, 48));
        _peek = Add(UiKit.MakeButton($"Peek through the keyhole   {ShopClock.Peek / 60}h", Palette.Gold, 16, Peek).Place(PanelX, 160, PanelW, 48));
        _lock = Add(UiKit.MakeButton($"Examine the lock   {ShopClock.ExamineLock}m", Palette.Gold, 16, ExamineLock).Place(PanelX, 214, PanelW, 48));
        _knock.TooltipText = "A false bottom sounds different.";
        _peek.TooltipText = "A dim look at one thing inside.";
        _lock.TooltipText = "Who made it, and how hard it'll be to pick.";
        _notes = Add(UiKit.MakeLabel("Shaking is free. Everything else costs shop time.", 16, Palette.Muted).Wrapped().Place(PanelX, 270, PanelW, 72));

        Add(Header("DEAL", PanelX, 352));
        Add(UiKit.MakeLabel("Asking", 16, Palette.Muted).Place(PanelX, 376, 200, 20));
        _ask = Add(UiKit.MakeLabel("", 40, Palette.Gold, outline: 4).Place(PanelX, 394, 200, 48));
        _wasAsk = Add(UiKit.MakeLabel("", 16, Palette.Muted).Place(PanelX + 200, 412, 184, 20));

        _slider = new HSlider { Step = 5, MinValue = 0, FocusMode = Control.FocusModeEnum.None };
        StyleSlider(_slider);
        _slider.ValueChanged += _ => Refresh();
        Add(_slider.Place(PanelX, 458, PanelW, 32));

        _offer = Add(UiKit.MakeButton("", Palette.Good, 16, MakeOffer).Place(PanelX, 500, PanelW, 52));
        _pay = Add(UiKit.MakeButton("", Palette.Gold, 16, PayAsk).Place(PanelX, 560, 188, 52));
        _pass = Add(UiKit.MakeButton("Pass", Palette.Bad, 16, Pass).Place(PanelX + 196, 560, 188, 52));
        _footer = Add(UiKit.MakeLabel("", 16, Palette.Muted).Wrapped().Place(PanelX, 624, PanelW, 70));

        _slider.MaxValue = Math.Min(_visit.Ask, State.Gold);
        _slider.Value = Money.RoundTo5(_visit.Ask * 0.6f);
    }

    static void StyleSlider(HSlider slider)
    {
        var coin = GD.Load<Texture2D>("res://assets/sprites/items/doubloons.png");
        var track = new StyleBoxFlat { BgColor = new Color("#120e1a"), BorderColor = Palette.PanelBorder, AntiAliasing = false };
        track.SetBorderWidthAll(3);
        track.SetContentMarginAll(6);
        var fill = new StyleBoxFlat { BgColor = Palette.Gold.Darkened(0.35f), AntiAliasing = false };
        fill.SetContentMarginAll(6);
        slider.AddThemeStyleboxOverride("slider", track);
        slider.AddThemeStyleboxOverride("grabber_area", fill);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        slider.AddThemeIconOverride("grabber", coin);
        slider.AddThemeIconOverride("grabber_highlight", coin);
    }

    void Refresh()
    {
        int offer = (int)_slider.Value;
        _ask.Text = $"{_visit.Ask}g";
        _wasAsk.Text = _visit.Ask != _visit.OpeningAsk ? $"was {_visit.OpeningAsk}g" : "";
        bool open = !Concluded && !Busy;
        bool timeForOffer = State.CanAfford(ShopClock.Offer);
        _offer.Text = $"Offer {offer}g   {ShopClock.Offer}m";
        _offer.Disabled = !open || !timeForOffer || offer <= 0;
        _pay.Text = $"Pay {_visit.Ask}g";
        _pay.Disabled = !open || State.Gold < _visit.Ask;
        _pass.Disabled = !open;
        _slider.Editable = open;
        _knock.Disabled = !open || !State.CanAfford(ShopClock.Knock + ShopClock.Offer);
        _peek.Disabled = !open || !State.CanAfford(ShopClock.Peek + ShopClock.Offer);
        _lock.Disabled = !open || !State.CanAfford(ShopClock.ExamineLock + ShopClock.Offer);
        _footer.Text = $"Closing at {ShopClock.Format(ShopClock.Close)}. Rent {State.RentDue}g due after Day {State.RentDueDay}." +
                       (State.Gold < _visit.Ask ? " You can't cover the asking price." : "");
    }

    void SpendTime(int minutes)
    {
        State.Spend(minutes);
        Game.Hud.SetClock(State.Day, State.Minute);
    }

    void WatchSeller()
    {
        var line = _visit.NervousLine(State.Rng);
        if (line == null) return;
        _seller.Sweat();
        _seller.Say(line);
    }

    public void Knock()
    {
        if (Busy || Concluded) return;
        SpendTime(ShopClock.Knock);
        _shake.Knock();
        _notes.Text = "You rap on the bottom of the chest.";
        WatchSeller();
        Refresh();
    }

    public void Peek()
    {
        if (Busy || Concluded) return;
        SpendTime(ShopClock.Peek);
        Sfx.PlayGroup("latch", 0.8f, -6);
        _keyhole = Add(new KeyholeView(_visit.Chest));
        _keyhole.Closed += () =>
        {
            _keyhole = null;
            WatchSeller();
            Refresh();
        };
        Game.Moment("02_keyhole", 0.5);
        Refresh();
    }

    public void CloseKeyhole() => _keyhole?.Close();

    public void ExamineLock()
    {
        if (Busy || Concluded) return;
        SpendTime(ShopClock.ExamineLock);
        Sfx.Play("inspect_lock");
        _shake.View.LockColor = Palette.Lock(_visit.Chest.ShownLock.Index);
        _shake.View.RefreshLock();
        _shake.View.Wobble(0.2f);
        bool fake = _visit.Chest.ShownLock != _visit.Chest.Lock;
        _notes.Text = Describe.Lock(_visit.Chest);
        _notes.AddThemeColorOverride("font_color", fake ? Palette.Bad : Palette.Text);
        WatchSeller();
        Refresh();
    }

    public void MakeOffer() => MakeOffer((int)_slider.Value);

    public void MakeOffer(int amount)
    {
        if (Busy || Concluded || !State.CanAfford(ShopClock.Offer)) return;
        SpendTime(ShopClock.Offer);
        amount = Math.Min(amount, State.Gold);
        var response = _visit.Offer(amount, State.Rng);
        Sfx.Play("bid");
        switch (response.Kind)
        {
            case ResponseKind.Counter:
                _seller.Say(response.Line);
                _seller.SetMood(_visit.Mood);
                break;
            case ResponseKind.Insulted:
                _seller.Say(response.Line, Palette.Bad);
                _seller.SetMood(_visit.Mood);
                Game.Shake(0.08f);
                break;
            case ResponseKind.Leave:
                _seller.Say(response.Line, Palette.Bad);
                _seller.SetMood(2);
                break;
            default:
                _seller.Say(response.Line, Palette.Good);
                break;
        }
        Game.Moment("03_haggle", 0.3);
        AfterResponse();
    }

    public void PayAsk()
    {
        if (Busy || Concluded || State.Gold < _visit.Ask) return;
        var response = _visit.PayAsk(State.Rng);
        _seller.Say(response.Line, Palette.Good);
        AfterResponse();
    }

    public void Pass()
    {
        if (Busy || Concluded) return;
        _seller.Say(_visit.Pass(State.Rng));
        AfterResponse();
    }

    async void AfterResponse()
    {
        if (_visit.Outcome == VisitOutcome.Bought)
        {
            var purchase = State.Buy(_visit);
            purchase.Shaken = _shake.HeardAnything;
            Game.Hud.SetGold(State.Gold);
            Sfx.PlayGroup("pay");
            FloatText($"-{_visit.PricePaid}g", new Vector2(600, 300), Palette.Gold, 32);
        }
        else if (_visit.Outcome == VisitOutcome.Haggling)
        {
            _slider.MaxValue = Math.Min(_visit.Ask, State.Gold);
            Refresh();
            if (State.CanAfford(ShopClock.Offer)) return;
            // Out of time: the shop closes on them.
            _seller.Say("Closing up? Another time, then.");
            _visit.Pass(State.Rng);
        }

        _busy = true;
        Refresh();
        await Wait(1.6);
        if (!Alive) return;

        // A bought chest goes to the back room; otherwise it leaves with its owner.
        var chestExit = _shake.CreateTween();
        if (_visit.Outcome == VisitOutcome.Bought)
            chestExit.TweenProperty(_shake, "position:x", 300f, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        else
            chestExit.TweenProperty(_shake, "modulate:a", 0f, 0.3f);
        Sfx.PlayGroup("door_close", 1f, -6);
        await ToSignal(_seller.Leave(), Tween.SignalName.Finished);
        if (!Alive) return;
        Game.NextCustomer();
    }
}
