using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// One seller, one sealed chest. Everything you learn about the chest comes from handling it: shake it and listen,
/// click it to knock, set it on the scale, or click the keyhole and look inside with a lantern (that one costs shop
/// time). The ledger holds what you've learned so far. Then haggle. Nothing on this screen states what the chest is
/// worth.
/// </summary>
public partial class CounterPhase : Phase
{
    static readonly Vector2 ChestRest = new(430, 450);
    static readonly Vector2 ScaleAt = new(700, 440);
    static readonly Vector2 ScaleSpot = ScaleAt + new Vector2(0, ScaleView.ChestOffsetY);
    static readonly Rect2 DragBounds = new(330, 200, 430, 250);
    const float PanelX = 844, PanelW = 384;

    Visit _visit;
    SellerView _seller;
    ShakeBox _shake;
    ScaleView _scale;
    MentorView _mentor;
    Tutor _tutor;
    Label _notes, _hint, _ask, _wasAsk, _footer;
    HSlider _slider;
    Button _ledgerButton, _offer, _pay, _pass;
    KeyholeView _keyhole;
    LedgerView _ledger;
    bool _busy, _onScale, _heardAll;
    readonly List<string> _heardWords = new();
    readonly HashSet<ItemDef> _heardItems = new();
    float? _weighed;
    bool? _knockedHollow;
    int _lookedMinutes;

    public Visit Visit => _visit;
    public ShakeBox Shake => _shake;
    public bool Concluded => _visit.Outcome != VisitOutcome.Haggling;
    public bool Busy => _busy || _keyhole != null || _ledger != null;
    public KeyholeView Keyhole => _keyhole;
    public LedgerView Ledger => _ledger;

    public override void _Ready()
    {
        _visit = State.Current;
        Add(UiKit.MakeBackdrop("cellar_night", 0.75f));
        Add(new Painter(DrawCounter));

        _seller = Add(new SellerView(_visit.Seller) { Position = new Vector2(176, 330) });
        _scale = Add(new ScaleView { Position = ScaleAt });
        _scale.Settled += OnWeighed;
        _shake = Add(new ShakeBox(_visit.Chest, DragBounds)
        {
            Position = ChestRest,
            RestFor = at => MathF.Abs(at.X - ScaleAt.X) < 100 ? ScaleSpot : ChestRest,
        });
        // The maker's plate is plain to see: tint the lock by its maker, as the notes say.
        _shake.View.LockColor = Palette.Lock(_visit.Chest.Lock.Index);
        _shake.View.RefreshLock();
        _shake.Impacted += OnHeard;
        _shake.Clicked += Knock;
        _shake.KeyholeClicked += Peek;
        _shake.Lifted += () =>
        {
            if (_onScale) _scale.SetLoad(null);
            _onScale = false;
        };
        _shake.Landed += spot =>
        {
            _onScale = spot.DistanceTo(ScaleSpot) < 1;
            if (_onScale) _scale.SetLoad(_visit.Chest.ContentsWeight);
        };

        Add(UiKit.MakeLabel(_visit.Seller.Name, 24, Palette.Gold, outline: 4).Place(40, 534, 360, 30));
        _hint = Add(UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(180, 540, 500, 20));
        BuildQueue();
        BuildPanel();
        // Above the keyhole and ledger overlays, so Bram can comment on what you see there.
        _mentor = Add(new MentorView { Position = new Vector2(352, 586), ZIndex = 20 });

        Sfx.PlayGroup("door_open", 1f, -6);
        _seller.Enter();
        _seller.Say(_visit.Pitch);
        Game.Hud.SetClock(State.Day, State.Minute);
        if (_visit.Lesson != null)
        {
            _tutor = new Tutor(_visit.Lesson);
            Teach(Cue.Arrive);
        }
        Refresh();
    }

    public override void _Process(double delta)
    {
        _scale.Highlight = _shake.Dragging && MathF.Abs(_shake.View.Position.X - ScaleAt.X) < 100;
        _hint.Text = _shake.Hover switch
        {
            _ when Busy || Concluded => "",
            ShakeBox.Part.Keyhole => "Click the keyhole to look inside (the clock runs)",
            ShakeBox.Part.Body => _onScale ? "Drag to shake. Click to knock." : "Drag to shake. Click to knock. Drop it on the scale to weigh it.",
            _ => _shake.HeardAnything ? "" : "Drag the chest to shake it",
        };
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.L } && !Busy)
        {
            GetViewport().SetInputAsHandled();
            OpenLedger();
        }
    }

    void Teach(Cue cue, string key = null)
    {
        if (_tutor == null || !_tutor.Notify(cue, key)) return;
        string text = _tutor.Current.Text.Replace("{weight}", _weighed is float w ? Describe.Pounds(w) : "?");
        _mentor.Say(text);
        Game.Moment($"1{Array.IndexOf(Lessons.FirstDay, _visit.Lesson)}_{_visit.Seller.Key}_{cue}", 0.3);
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

        Add(Header("WHAT YOU KNOW", PanelX, 82));
        _ledgerButton = Add(UiKit.MakeButton("Ledger  [L]", Palette.Gold, 16, OpenLedger).Place(PanelX + 224, 74, 160, 40));
        _notes = Add(UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(PanelX, 124, PanelW, 216));

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
        _ledgerButton.Disabled = Busy;
        _slider.Editable = open;
        _footer.Text = $"Closing at {ShopClock.Format(ShopClock.Close)}. Rent {State.RentDue}g due after Day {State.RentDueDay}." +
                       (State.Gold < _visit.Ask ? " You can't cover the asking price." : "");

        // Only facts and what you perceived. Judging them is your job.
        var chest = _visit.Chest;
        string difficulty = chest.Lock.Index switch { 0 => "easy to pick", 1 => "fair to pick", 2 => "hard to pick", _ => "very hard to pick" };
        var lines = new List<string> { $"Lock: {chest.Lock.Name}, {difficulty}." };
        lines.Add(_heardWords.Count == 0 ? "Heard: nothing yet." : "Heard: " + string.Join("  ", _heardWords.Select(w => $"'{w}'")));
        if (_weighed is float w) lines.Add($"Scale: {Describe.Pounds(w)} inside.");
        if (_knockedHollow is bool hollow) lines.Add(hollow ? "Knock: hollow! Something's under the floor." : "Knock: solid.");
        if (_lookedMinutes > 0) lines.Add($"Looked inside for {_lookedMinutes} min.");
        _notes.Text = string.Join("\n", lines);
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

    void OnHeard(Rattler rattler, float strength)
    {
        if (!_heardItems.Add(rattler.Item)) return;
        _heardWords.Add(rattler.Sound.Word);
        Refresh();
        if (!_heardAll && _heardItems.Count == _visit.Chest.Items.Count)
        {
            _heardAll = true;
            Teach(Cue.HeardAll);
        }
    }

    void OnWeighed(float pounds)
    {
        _weighed = pounds;
        Refresh();
        Teach(Cue.Weighed);
        WatchSeller();
        Game.Moment("03_scale", 0.2);
    }

    /// <summary>Raps on the chest. Free: it's your counter.</summary>
    public void Knock()
    {
        if (Busy || Concluded) return;
        _shake.Knock();
        bool hollow = _visit.Chest.FalseBottom != null;
        GetTree().CreateTimer(hollow ? 0.9 : 0.5).Timeout += () =>
        {
            if (!Alive) return;
            _knockedHollow = hollow;
            Refresh();
            Teach(hollow ? Cue.KnockedHollow : Cue.KnockedSolid);
            WatchSeller();
        };
    }

    /// <summary>Carries the chest onto the scale (the bot uses this; players drag it).</summary>
    public void Weigh()
    {
        if (Busy || Concluded || _onScale) return;
        _shake.MoveTo(ScaleSpot);
    }

    /// <summary>Holds a lantern to the keyhole. The shop clock runs while you look.</summary>
    public void Peek()
    {
        if (Busy || Concluded || !State.CanAfford(ShopClock.Offer + 1)) return;
        _keyhole = Add(new KeyholeView(_shake.Contents()));
        _keyhole.MinutePassed += () =>
        {
            SpendTime(1);
            _lookedMinutes++;
            if (!State.CanAfford(ShopClock.Offer + 1)) _keyhole?.Close();
        };
        _keyhole.Seen += key => Teach(Cue.SawItem, key);
        _keyhole.Closed += () =>
        {
            _keyhole = null;
            WatchSeller();
            Refresh();
        };
        Game.Moment("04_keyhole", 1.4);
        Refresh();
    }

    public void CloseKeyhole() => _keyhole?.Close();

    public void OpenLedger()
    {
        if (Busy) return;
        _ledger = Add(new LedgerView(State.Ledger));
        _ledger.Closed += () =>
        {
            _ledger = null;
            Refresh();
        };
        Teach(Cue.LedgerOpened);
        Game.Moment("02_ledger", 0.4);
        Refresh();
    }

    public void CloseLedger() => _ledger?.Close();

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
        Game.Moment("05_haggle", 0.3);
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
            chestExit.TweenProperty(_shake, "position:y", 400f, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        else
            chestExit.TweenProperty(_shake, "modulate:a", 0f, 0.3f);
        Sfx.PlayGroup("door_close", 1f, -6);
        await ToSignal(_seller.Leave(), Tween.SignalName.Finished);
        if (!Alive) return;
        Game.NextCustomer();
    }
}
