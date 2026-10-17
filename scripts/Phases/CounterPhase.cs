using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// One seller, one chest. Shake it and listen: every family of things has its own everyday sound. Tools you've bought
/// tell you more: the scale, the keyhole lens, the compass, the lodestone, the hammer. Then buy, make one offer, or
/// pass. Whatever you buy is opened straight away. Nothing on this screen states what the chest is worth.
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
    Label _notes, _hint, _ask, _wasAsk, _offerNote, _footer;
    HSlider _slider;
    Button _ledgerButton, _offer, _buy, _pass, _peekButton;
    readonly List<Button> _toolButtons = new();
    PeepholeView _peephole;
    LedgerView _ledger;
    Node2D _gauge;
    bool _busy, _onScale, _heardAll;
    readonly List<int> _heard = new();
    readonly HashSet<int> _seen = new();
    float? _weighed, _iron;
    int? _magic;
    bool? _hollow;

    public Visit Visit => _visit;
    public ShakeBox Shake => _shake;
    public bool Concluded => _visit.Outcome != VisitOutcome.Deciding;
    public bool Busy => _busy || _peephole != null || _ledger != null;
    public PeepholeView Peephole => _peephole;
    public LedgerView Ledger => _ledger;
    public MentorView Mentor => _mentor;

    /// <summary>Everything found out about this chest so far (the bot reasons from this).</summary>
    public Inspection Inspection => new()
    {
        Variants = State.Has("trumpet"),
        Seen = _seen.Where(i => i < _visit.Chest.Items.Count).Select(i => _visit.Chest.Items[i]).ToHashSet(),
        Weight = _weighed,
        Magic = _magic,
        Iron = _iron,
        Hollow = _hollow,
    };

    public override void _Ready()
    {
        _visit = State.Current;
        Add(UiKit.MakeBackdrop("cellar_night", 0.75f));
        Add(new Painter(DrawCounter));

        _seller = Add(new SellerView(_visit.Seller) { Position = new Vector2(176, 330) });
        if (State.Has("scale"))
        {
            _scale = Add(new ScaleView { Position = ScaleAt });
            _scale.Settled += OnWeighed;
        }
        _shake = Add(new ShakeBox(_visit.Chest, DragBounds)
        {
            Position = ChestRest,
            RestFor = at => _scale != null && MathF.Abs(at.X - ScaleAt.X) < 100 ? ScaleSpot : ChestRest,
            ShowVariants = State.Has("trumpet"),
        });
        _shake.View.LockColor = Palette.Lock(_visit.Chest.Lock.Index);
        _shake.View.RefreshLock();
        _shake.Impacted += (index, _) => OnHeard(index);
        _shake.Clicked += Knock;
        _shake.KeyholeClicked += Peek;
        _shake.Lifted += () =>
        {
            if (_onScale) _scale?.SetLoad(null);
            _onScale = false;
        };
        _shake.Landed += spot =>
        {
            _onScale = _scale != null && spot.DistanceTo(ScaleSpot) < 1;
            if (_onScale) _scale.SetLoad(_visit.Chest.ContentsWeight);
        };

        Add(UiKit.MakeLabel(_visit.Seller.Name, 24, Palette.Gold, outline: 4).Place(40, 534, 360, 30));
        _hint = Add(UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(180, 540, 500, 20));
        BuildQueue();
        BuildPanel();
        // Above the peephole and ledger overlays, so Bram can comment on what you see there.
        _mentor = Add(new MentorView { Position = new Vector2(352, 586), ZIndex = 20 });

        Sfx.PlayGroup("door_open", 1f, -6);
        _seller.Enter();
        _seller.Say(_visit.Pitch);
        Game.Hud.SetDay(State.Day, State.Tier);
        Game.Hud.SetGold(State.Gold, animate: false);
        GreetingHints();
        Refresh();
    }

    /// <summary>Bram's tips for whatever is new about this customer: the day, the tools, the person, the lock.</summary>
    void GreetingHints()
    {
        bool firstOfDay = State.Today.Bought + State.Today.Passed == 0;
        if (firstOfDay && State.Day == 1) Teach("intro");
        if (firstOfDay && State.Tier > 1) Teach($"tier{State.Tier}");
        if (firstOfDay && State.Day >= 2) Teach("haggle");
        foreach (var tool in new[] { "scale", "peephole", "lantern", "periscope", "trumpet", "compass", "lodestone", "hammer" })
            if (State.Has(tool)) Teach(tool);
        if (State.Ledger.Meet(_visit.Seller) && !(State.Day == 1 && firstOfDay)) Teach($"seller_{_visit.Seller.Key}");
        if (_visit.Chest.Lock.Locked) Teach("locked");
    }

    void Teach(string key)
    {
        if (!State.FirstTime(key)) return;
        _mentor.Say(Hints.Get(key));
        Game.Moment($"1_hint_{key}", 0.4);
    }

    public override void _Process(double delta)
    {
        if (_scale != null) _scale.Highlight = _shake.Dragging && MathF.Abs(_shake.View.Position.X - ScaleAt.X) < 100;
        _hint.Text = _shake.Hover switch
        {
            _ when Busy || Concluded => "",
            ShakeBox.Part.Keyhole when State.PeeksPerDay > 0 => "Click the keyhole to peek inside",
            ShakeBox.Part.Body or ShakeBox.Part.Keyhole => (State.Has("hammer") ? "Drag to shake. Click to knock." : "Drag to shake it.") +
                                                           (_scale != null && !_onScale ? " Drop it on the scale to weigh it." : ""),
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
        _notes = Add(UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(PanelX, 122, PanelW, 206));

        // Only the tools you own. The scale is used by dragging the chest onto it.
        var tools = new List<(string Icon, string Text, Action Use)>();
        bool crowded = new[] { "compass", "lodestone", "hammer" }.Count(State.Has) >= 2;
        if (State.PeeksPerDay > 0) tools.Add(("lens", "Peek", Peek));
        if (State.Has("compass")) tools.Add(("compass", crowded ? "Magic" : "Compass", UseCompass));
        if (State.Has("lodestone")) tools.Add(("magnet", crowded ? "Iron" : "Lodestone", UseLodestone));
        if (State.Has("hammer")) tools.Add(("hammer", crowded ? "Tap" : "Knock", Knock));
        if (tools.Count > 0)
        {
            Add(Header("TOOLS", PanelX, 332));
            float w = (PanelW - (tools.Count - 1) * 8) / tools.Count;
            for (int i = 0; i < tools.Count; i++)
            {
                var button = Add(ToolButton(tools[i].Icon, tools[i].Text, tools[i].Use, crowded).Place(PanelX + i * (w + 8), 354, w, 44));
                _toolButtons.Add(button);
                if (tools[i].Icon == "lens") _peekButton = button;
            }
        }

        Add(Header("DEAL", PanelX, 410));
        Add(UiKit.MakeLabel("Asking", 16, Palette.Muted).Place(PanelX, 432, 200, 20));
        _ask = Add(UiKit.MakeLabel("", 40, Palette.Gold, outline: 4).Place(PanelX, 448, 200, 48));
        _wasAsk = Add(UiKit.MakeLabel("", 16, Palette.Muted).Place(PanelX + 200, 466, 184, 20));

        _slider = new HSlider { Step = 5, MinValue = 5, FocusMode = Control.FocusModeEnum.None };
        StyleSlider(_slider);
        _slider.ValueChanged += _ => Refresh();
        Add(_slider.Place(PanelX, 504, PanelW, 28));
        _offer = Add(UiKit.MakeButton("", Palette.Good, 16, MakeOffer).Place(PanelX, 538, PanelW, 48));
        _offerNote = Add(UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Center).Wrapped().Place(PanelX, 508, PanelW, 72));
        _offerNote.VerticalAlignment = VerticalAlignment.Center;
        _buy = Add(UiKit.MakeButton("", Palette.Gold, 16, Buy).Place(PanelX, 596, 188, 52));
        _pass = Add(UiKit.MakeButton("Pass", Palette.Bad, 16, Pass).Place(PanelX + 196, 596, 188, 52));
        _footer = Add(UiKit.MakeLabel("", 16, Palette.Muted).Wrapped().Place(PanelX, 656, PanelW, 44));

        _slider.MaxValue = Math.Max(5, _visit.Ask - 5);
        _slider.Value = Money.RoundTo5(_visit.Ask * 0.7f);
    }

    static Button ToolButton(string icon, string text, Action use, bool crowded)
    {
        var button = UiKit.MakeButton(text, Palette.Gold, 16, use);
        button.Alignment = HorizontalAlignment.Right;
        button.ClipText = true;
        button.AddThemeConstantOverride("h_separation", crowded ? 2 : 6);
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/icons/{icon}.png");
        button.ExpandIcon = false;
        button.AddThemeConstantOverride("icon_max_width", crowded ? 24 : 32);
        return button;
    }

    static void StyleSlider(HSlider slider)
    {
        var coin = GD.Load<Texture2D>("res://assets/sprites/items/gold_doubloons.png");
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
        bool open = !Concluded && !Busy;
        _ask.Text = $"{_visit.Ask}g";
        _wasAsk.Text = _visit.Ask != _visit.OpeningAsk ? $"was {_visit.OpeningAsk}g" : "";
        _buy.Text = $"Buy {_visit.Ask}g";
        _buy.Disabled = !open || State.Gold < _visit.Ask;
        _pass.Disabled = !open;
        _ledgerButton.Disabled = Busy;
        foreach (var button in _toolButtons) button.Disabled = !open;
        if (_peekButton != null)
        {
            _peekButton.Text = _toolButtons.Count >= 4 ? "Peek" : $"Peek ({State.PeeksLeft})";
            _peekButton.Disabled = !open || State.PeeksLeft <= 0;
        }

        // One offer per customer, from the second day on.
        bool canOffer = State.CanHaggle && _visit.CanOffer && !Concluded;
        _slider.Visible = _offer.Visible = canOffer;
        _offerNote.Visible = !canOffer;
        _offerNote.Text = !State.CanHaggle ? "Today: buy or pass. Bram will show you how to haggle tomorrow."
            : _visit.Seller.OnePrice ? $"{_visit.Seller.Name} never haggles."
            : Concluded ? "" : "You've made your offer. Buy at their price, or pass.";
        if (canOffer)
        {
            _slider.MaxValue = Math.Max(5, Math.Min(_visit.Ask - 5, State.Gold));
            int offer = (int)_slider.Value;
            _offer.Text = $"Offer {offer}g (one try)";
            _offer.Disabled = !open || offer <= 0;
        }

        int left = State.Queue.Count;
        _footer.Text = $"{(left == 0 ? "Last customer today." : $"{left} more waiting today.")}" +
                       (State.PeeksPerDay > 0 ? $" Peeks left: {State.PeeksLeft}." : "") +
                       (State.Gold < _visit.Ask ? $" You have {State.Gold}g." : "");
        _notes.Text = string.Join("\n", Notes());
    }

    /// <summary>Only facts and what you perceived. Judging them is your job.</summary>
    IEnumerable<string> Notes()
    {
        var chest = _visit.Chest;
        if (State.Ledger.Knows(_visit.Seller)) yield return $"{_visit.Seller.Name}: {_visit.Seller.Quirk.Split(". ")[0].TrimEnd('.')}.";
        bool variants = State.Has("trumpet");
        var words = _heard.Select(i => variants ? Families.Describe(chest.Items[i].Def.Family, chest.Items[i].Def.Variant) : chest.Items[i].Def.Word);
        yield return _heard.Count == 0 ? "Heard: nothing yet. Shake it!" : $"Heard {_heard.Count} thing{(_heard.Count == 1 ? "" : "s")}: {string.Join(", ", words)}";
        string lockLine = chest.Lock.Locked ? $"{chest.Lock.Name}, {chest.Lock.Difficulty}." : "No lock.";
        yield return _weighed is float w ? $"{lockLine} Scale: {Describe.Pounds(w)} inside." : lockLine;
        if (_seen.Count > 0)
        {
            var names = _seen.OrderBy(i => i).Select(i => i >= chest.Items.Count ? "a heavy bundle of rags" : SeenName(chest.Items[i].Def));
            yield return $"Seen: {string.Join(", ", names)}.";
        }
        var readings = new List<string>();
        if (_magic is int m) readings.Add($"Compass: {MagicWords(m)}.");
        if (_iron is float iron) readings.Add($"Lodestone: {IronWords(iron)}.");
        if (_hollow is bool hollow) readings.Add(hollow ? "Knock: HOLLOW, something's under the floor!" : "Knock: solid.");
        if (readings.Count > 0) yield return string.Join(" ", readings);
    }

    string SeenName(ItemDef item)
    {
        var looks = item.LooksLike != null ? ItemCatalog.Get(item.LooksLike) : item;
        return State.Ledger.Knows(looks) ? looks.Name : "something new";
    }

    static string MagicWords(int magic) => magic switch
    {
        0 => "the needle doesn't move",
        1 => "a twitch",
        2 => "swings hard",
        _ => "spins wildly",
    };

    static string IronWords(float iron) => iron switch
    {
        < 0.1f => "nothing",
        < 2f => "a faint tug",
        < 5f => "a firm pull",
        _ => "pulls hard",
    };

    void OnHeard(int index)
    {
        var chest = _visit.Chest;
        if (index >= chest.Items.Count || _heard.Contains(index)) return;
        _heard.Add(index);
        var family = chest.Items[index].Def.Family;
        if (State.Ledger.Hear(family) && Families.Tier(family) > 1) Teach($"family_{family}");
        Refresh();
        if (!_heardAll && _heard.Count == chest.Items.Count)
        {
            _heardAll = true;
            Teach("heard");
            Teach("deal");
        }
    }

    void OnWeighed(float pounds)
    {
        _weighed = pounds;
        Refresh();
        Game.Moment("03_scale", 0.2);
    }

    /// <summary>Raps on the chest with the tapping hammer. Without one, a knock tells you nothing.</summary>
    public void Knock()
    {
        if (Busy || Concluded || !State.Has("hammer")) return;
        _shake.Knock();
        bool hollow = _visit.Chest.FalseBottom != null;
        GetTree().CreateTimer(hollow ? 0.9 : 0.5).Timeout += () =>
        {
            if (!Alive) return;
            _hollow = hollow;
            Refresh();
        };
    }

    /// <summary>Carries the chest onto the scale (the bot uses this; players drag it).</summary>
    public void Weigh()
    {
        if (Busy || Concluded || _onScale || _scale == null) return;
        _shake.MoveTo(ScaleSpot);
    }

    /// <summary>Looks through the keyhole with whatever lens you own. Uses one of the day's peeks.</summary>
    public void Peek()
    {
        if (Busy || Concluded || State.PeeksPerDay == 0 || !State.UsePeek()) return;
        _peephole = Add(new PeepholeView(_shake.Contents(), State.PeekReach, State.PeeksLeft, State.Ledger));
        _seen.UnionWith(_peephole.Seen);
        _peephole.Closed += () =>
        {
            _peephole = null;
            Refresh();
        };
        Game.Moment("04_peek", 0.5);
        Refresh();
    }

    public void ClosePeephole() => _peephole?.Close();

    /// <summary>Holds the mana compass to the chest: the needle swings toward magic.</summary>
    public void UseCompass()
    {
        if (Busy || Concluded || !State.Has("compass")) return;
        _magic = _visit.Chest.Magic;
        float swing = Mathf.DegToRad(new[] { 0f, 35f, 75f, 110f }[Math.Min(3, _magic.Value)]);
        Sfx.Play("compass");
        ShowGauge("compass", p =>
        {
            var c = new Vector2(0, 0);
            p.DrawCircle(c, 34, new Color("#8a6a2a"));
            p.DrawCircle(c, 30, new Color("#efe3c4"));
            float wobble = MathF.Sin(Time.GetTicksMsec() / 90f) * (swing > 0 ? 0.08f : 0.02f);
            var dir = Vector2.FromAngle(-Mathf.Pi / 2 + swing + wobble);
            p.DrawLine(c, c + dir * 26, new Color("#c0392b"), 4);
            p.DrawLine(c, c - dir * 14, new Color("#4a4a58"), 4);
            p.DrawCircle(c, 4, new Color("#4a3a2a"));
        }, $"Compass: {MagicWords(_magic.Value)}");
        Refresh();
    }

    /// <summary>Holds the lodestone to the chest: it tugs toward iron and steel.</summary>
    public void UseLodestone()
    {
        if (Busy || Concluded || !State.Has("lodestone")) return;
        _iron = _visit.Chest.IronWeight;
        float pull = Mathf.Clamp(_iron.Value * 6f, 0f, 30f);
        if (pull > 0)
        {
            Sfx.PlayGroup("clack", 1.2f, -2);
            var tug = _shake.CreateTween();
            tug.TweenProperty(_shake, "position:x", -pull, 0.12f).SetTrans(Tween.TransitionType.Back);
            tug.TweenProperty(_shake, "position:x", 0f, 0.5f).SetDelay(0.6f);
        }
        ShowGauge("magnet", null, $"Lodestone: {IronWords(_iron.Value)}");
        Refresh();
    }

    /// <summary>A tool held up next to the chest for a couple of seconds, with what it shows.</summary>
    void ShowGauge(string icon, Action<Painter> draw, string reading)
    {
        _gauge?.QueueFree();
        // Held up just above the chest, wherever it's sitting.
        _gauge = Add(new Node2D { Position = _shake.Resting + new Vector2(0, -150) });
        if (draw != null)
        {
            var painter = new Painter(draw);
            _gauge.AddChild(painter);
            var redraw = painter.CreateTween().SetLoops();
            redraw.TweenCallback(Callable.From(painter.QueueRedraw)).SetDelay(0.03f);
        }
        else
        {
            _gauge.AddChild(UiKit.MakeSprite($"res://assets/ui/icons/{icon}.png", Vector2.Zero, 1));
        }
        _gauge.AddChild(UiKit.MakeLabel(reading, 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(-160, -66, 320, 20));
        var fade = _gauge.CreateTween();
        fade.TweenInterval(2.6f);
        fade.TweenProperty(_gauge, "modulate:a", 0f, 0.4f);
        fade.TweenCallback(Callable.From(_gauge.QueueFree));
        Game.Moment($"04_{icon}", 0.6);
    }

    public void OpenLedger()
    {
        if (Busy) return;
        _ledger = Add(new LedgerView(State.Ledger, State.Tier));
        _ledger.Closed += () =>
        {
            _ledger = null;
            Refresh();
        };
        Game.Moment("02_ledger", 0.4);
        Refresh();
    }

    public void CloseLedger() => _ledger?.Close();

    public void MakeOffer() => MakeOffer((int)_slider.Value);

    public void MakeOffer(int amount)
    {
        if (Busy || Concluded || !State.CanHaggle || !_visit.CanOffer) return;
        var response = _visit.Offer(Math.Min(amount, State.Gold), State.Rng);
        Sfx.Play("bid");
        _seller.Say(response.Line, response.Kind switch
        {
            ResponseKind.Accept => Palette.Good,
            ResponseKind.Leave => Palette.Bad,
            _ => (Color?)null,
        });
        if (response.Kind == ResponseKind.Counter) _seller.SetMood(1);
        Game.Moment("05_offer", 0.3);
        if (Concluded) AfterDeal();
        else Refresh();
    }

    public void Buy()
    {
        if (Busy || Concluded || State.Gold < _visit.Ask) return;
        _seller.Say(_visit.Buy(State.Rng).Line, Palette.Good);
        AfterDeal();
    }

    public void Pass()
    {
        if (Busy || Concluded) return;
        _seller.Say(_visit.Pass(State.Rng));
        AfterDeal();
    }

    async void AfterDeal()
    {
        _busy = true;
        Refresh();
        bool bought = _visit.Outcome == VisitOutcome.Bought;
        if (bought)
        {
            var purchase = State.Buy(_visit);
            purchase.Shaken = _shake.HeardAnything;
            State.Opening = purchase;
            State.Grade = PickGrade.Clean;
            Game.Hud.SetGold(State.Gold);
            Sfx.PlayGroup("pay");
            FloatText($"-{_visit.PricePaid}g", new Vector2(600, 300), Palette.Gold, 32);
        }
        else
        {
            State.Pass();
        }

        await Wait(1.4);
        if (!Alive) return;
        var chestExit = _shake.CreateTween();
        if (bought)
            chestExit.TweenProperty(_shake, "position:y", 60f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        else
            chestExit.TweenProperty(_shake, "modulate:a", 0f, 0.3f);
        Sfx.PlayGroup("door_close", 1f, -6);
        await ToSignal(_seller.Leave(), Tween.SignalName.Finished);
        if (!Alive) return;

        // Bought chests are opened right away: pick the lock if there is one, then lift the lid.
        if (bought) Game.SwitchTo(_visit.Chest.Lock.Locked ? new LockpickPhase() : new OpeningPhase());
        else Game.NextCustomer();
    }
}
