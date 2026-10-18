using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// One seller, one chest, one puzzle. The seller judges the chest their way, says what they think is in it, and prices
/// it: a fixed price, a haggle, or "what'll you give me?". Shake it yourself (gently, firmly, hard), drop it to listen
/// for a false bottom, use your tools, and correct the appraisal slip: how many of each kind, which is enchanted. It adds
/// that up at what such things are worth if they're as good as the chest's colour. Pass, and they take it away: you
/// never see inside, but your gut has a feeling about it. Nothing on this screen knows what the chest is worth. A
/// seller's first visit is a lesson: Bram's steps, each checked as you do it, before the trade buttons open.
/// </summary>
public partial class CounterPhase : Phase
{
    static readonly Vector2 ChestRest = new(556, 416);
    // Tall, so a chest can be lifted high and dropped.
    static readonly Rect2 DragBounds = new(452, 130, 232, 286);
    const float PanelX = 844, PanelW = 384, RowH = 36, RowStep = 40, RowsY = 150;

    /// <summary>Shaking at a strength this long counts as having tried it.</summary>
    const double TryTime = 0.5;

    /// <summary>How long a hard shake a seller who hates them lets go before they mind.</summary>
    const double HardShakeTolerance = 0.2;

    sealed class Row
    {
        public Category? Category;
        public Button Button;
        public Label Value, Count, Star;
        public Painter Box;
    }

    Visit _visit;
    Appraisal _slip;
    readonly Hearing _hearing = new();
    readonly Dictionary<(Shake, bool), double> _shaken = new();
    readonly List<Row> _rows = new();
    readonly Random _rng = new();
    SellerView _seller;
    ShakeBox _shake;
    MentorView _mentor;
    LedgerView _ledger;
    Label _hint, _notes, _worth, _askLabel, _ask, _chanceText, _footer, _timer;
    Painter _chance;
    float _chanceShown;
    Button _buy, _pass, _offer, _magnetButton, _ledgerButton;
    HSlider _slider;
    Control _reveal;
    bool _busy, _lamp, _magnet;
    IReadOnlyList<LessonStep> _steps;
    int _step;
    double _patience;
    readonly List<string> _noteLines = new();

    public Visit Visit => _visit;
    public Appraisal Slip => _slip;
    public Hearing Hearing => _hearing;
    public ShakeBox Shake => _shake;
    public MentorView Mentor => _mentor;
    public bool Concluded => !_visit.Deciding;
    public bool Busy => _busy || _ledger != null;

    /// <summary>A lesson is under way.</summary>
    public bool InLesson => _steps != null;

    /// <summary>The trade buttons are open: no lesson, or the lesson's step is about trading.</summary>
    bool TradeOpen => !InLesson || _steps[_step].Trades;

    /// <summary>The lesson's step is done by trading (the bot uses this).</summary>
    public bool LessonTrades => InLesson && _steps[_step].Trades;

    /// <summary>What the lesson's current step says (the bot logs it if it gets stuck).</summary>
    public string LessonStep => InLesson ? _steps[_step].Say : null;

    /// <summary>Skips the rest of the lesson (the bot uses this; players click Skip lesson).</summary>
    public void SkipLesson() => EndLesson();

    /// <summary>Seconds an impatient seller will still wait, or 0 if they'll wait all day.</summary>
    public double TimeLeft => _visit.Seller.Patience > 0 ? _patience : 0;

    public override void _Ready()
    {
        _visit = State.Current;
        _slip = new Appraisal(_visit, State.Tier);
        _patience = _visit.Seller.Patience;
        Add(UiKit.MakeShop(State.Tier, 0.92f));
        BuildShelf();

        _seller = Add(new SellerView(_visit.Seller) { Position = new Vector2(150, 196) });
        _shake = Add(new ShakeBox(_visit.Chest, DragBounds) { Position = ChestRest });
        _shake.View.LockColor = Palette.Lock(_visit.Chest.Lock);
        _shake.Heard += OnHeard;
        _shake.Landed += OnLanded;
        // Once you own the witchlight it's always lit: anything enchanted glows when it makes a sound.
        _lamp = State.Has("lamp");
        _shake.Witchlight = _lamp;
        _hint = Add(UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(316, 452, 484, 20));
        _hint.ZIndex = 10;
        _notes = Add(UiKit.MakeLabel("", 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Wrapped().Place(316, 492, 484, 88));
        _notes.ZIndex = 10;
        _timer = Add(UiKit.MakeLabel("", 32, Palette.Warn, HorizontalAlignment.Center, outline: 6).Place(206, 92, 64, 40));
        _timer.ZIndex = 10;

        BuildQueue();
        BuildPanel();
        _mentor = Add(new MentorView { Position = new Vector2(322, 588), ZIndex = 40 });
        _mentor.LessonRead += NextStep;
        _mentor.LessonSkipped += EndLesson;

        Sfx.PlayGroup("door_open", 1f, -6);
        _seller.Enter();
        _seller.Say(_visit.Pitch);
        Game.Hud.SetDay(State.Day, State.Tier);
        Game.Hud.SetGold(State.Gold, animate: false);
        // What a lesson teaches, Bram needn't also tip about.
        foreach (var key in _visit.Lesson?.Covers ?? Array.Empty<string>()) State.HintsSeen.Add(key);
        GreetingHints();
        if (_visit.Lesson != null) StartLesson();
        Note(ChestNote());
        Refresh();
        Game.Moment("01_counter", 1.0);
    }

    void StartLesson()
    {
        _steps = _visit.Lesson.For(State);
        _step = 0;
        if (_steps.Count == 0) _steps = null;
        else ShowStep();
    }

    void ShowStep()
    {
        var step = _steps[_step];
        _mentor.ShowLesson(step.Say, step.Done != null);
        Game.Moment($"1_lesson_{_visit.Seller.Key}_{_step + 1}", 0.4);
        Refresh();
    }

    /// <summary>On to the next step, or the lesson's done.</summary>
    void NextStep()
    {
        if (!InLesson) return;
        if (++_step >= _steps.Count) EndLesson(learned: true);
        else
        {
            Sfx.Play("pin_perfect", 1.2f, -8);
            ShowStep();
        }
    }

    void EndLesson() => EndLesson(learned: false);

    void EndLesson(bool learned)
    {
        if (!InLesson) return;
        _steps = null;
        _mentor.EndLesson();
        if (learned)
        {
            Sfx.Play("win", 1.2f, -8);
            FloatText("Lesson learned!", new Vector2(558, 300), Palette.Good, 24);
        }
        Refresh();
    }

    /// <summary>Each frame: has the step been done? (It can be done before it's asked.)</summary>
    void CheckLesson()
    {
        if (!InLesson) return;
        if (Concluded)
        {
            // A deal struck on the last step (a lesson about trading) is the lesson done.
            EndLesson(learned: _visit.Outcome == VisitOutcome.Bought && _steps[_step].Trades && _step == _steps.Count - 1);
            return;
        }
        var step = _steps[_step];
        if (step.Done != null && step.Done(new LessonCheck { Visit = _visit, Slip = _slip, Hearing = _hearing, State = State })) NextStep();
    }

    string ChestNote()
    {
        var chest = _visit.Chest;
        string colour = $"A {Categories.ChestSkin(chest.Quality)} chest: expect {Categories.QualityName(chest.Quality)} things.";
        return chest.Lock.Locked ? $"{colour} Locked: {chest.Lock.Name}, {chest.Lock.Pins} pins." : colour;
    }

    /// <summary>Bram's tips for whatever is new about this customer.</summary>
    void GreetingHints()
    {
        bool firstOfDay = State.Today.Bought + State.Today.Passed == 0;
        bool met = State.Meet(_visit.Seller);
        if (State.Day == 1 && State.Tier == 1 && State.Guided)
        {
            Teach(_visit.Seller.Key == "tam" ? "intro" : $"first_{_visit.Seller.Key}");
            return;
        }
        if (firstOfDay && State.Day == 2) Teach("colours");
        if (!firstOfDay && State.Day >= 2) Teach("ledger");
        if (firstOfDay && State.Tier > 1) Teach($"tier{State.Tier}");
        // A seller's first visit is a lesson; one without a lesson gets Bram's tip.
        if (met && _visit.Lesson == null) Teach($"seller_{_visit.Seller.Key}");
        Teach(_visit.Trade switch { Trade.Haggle => "haggle", Trade.Offer => "offer", _ => "fixed" });
        if (_visit.Chest.Lock.Locked) Teach("locked");
        if (_lamp) Teach("lamp");
    }

    void Teach(string key)
    {
        // A lesson has Bram's attention. Anything else waits until its moment comes round again.
        if (InLesson || !State.FirstTime(key)) return;
        _mentor.Say(Hints.Get(key));
        Game.Moment($"1_hint_{key}", 0.4);
    }

    public override void _Process(double delta)
    {
        var level = _shake.Level;
        if (_shake.Dragging && level > Core.Shake.Still)
        {
            var key = (level, _magnet);
            double before = _shaken.GetValueOrDefault(key);
            _shaken[key] = before + delta;
            if (before < TryTime && _shaken[key] >= TryTime) _hearing.Tried(level, _magnet);
            // Some sellers won't have their chest shaken hard: a moment of it (not a stray flick) and up goes the price.
            if (level == Core.Shake.Hard && _visit.Seller.HatesHardShake && _shaken[key] >= HardShakeTolerance && !Concluded && !_busy)
                Angered(_visit.Anger(_rng));
        }
        CheckLesson();
        _hint.Text = Busy || Concluded ? ""
            : _shake.Hover ? "Drag to shake it. Lift it high and let go to drop it."
            : _magnet ? "Magnet on: iron can't move"
            : _shake.HeardAnything ? "" : _lamp ? "Drag the chest to shake it. The witchlight's lit." : "Drag the chest to shake it";

        // An impatient seller's clock runs while you're deciding (not while Bram is talking, but while he waits for you).
        if (_visit.Seller.Patience > 0 && !Concluded && !_busy && (!_mentor.Showing || _mentor.WaitingForAction))
        {
            _patience -= delta;
            if ((int)Math.Ceiling(_patience) != (int)Math.Ceiling(_patience + delta) && _patience > 0 && _patience <= 5)
                Sfx.Play("tick", 1.2f, -2);
            if (_patience <= 0) RanOutOfPatience();
        }
        _timer.Text = _visit.Seller.Patience > 0 && !Concluded ? $"{Math.Max(0, Math.Ceiling(_patience)):0}" : "";
        _timer.Modulate = _patience <= 5 ? new Color(1, 1, 1, 0.6f + 0.4f * MathF.Sin((float)_patience * 12f)) : Colors.White;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.L } && !Busy)
        {
            GetViewport().SetInputAsHandled();
            OpenLedger();
        }
    }

    /// <summary>Whatever you've kept on display, standing on the shop's shelf (part of the picture from the village shop on).</summary>
    void BuildShelf()
    {
        if (!State.DisplayOpen) return;
        const float y = 196, x0 = 300, w = 504;
        var shown = State.Display;
        for (int i = 0; i < shown.Count; i++)
        {
            float slot = w / Math.Max(6, shown.Count);
            var icon = Add(UiKit.MakeItemIcon(shown[i], new Vector2(x0 + slot * (i + 0.5f), y), 48, standing: true));
            icon.Modulate = new Color(0.85f, 0.85f, 0.9f);
        }
    }

    void BuildQueue()
    {
        if (State.Queue.Count == 0) return;
        Add(UiKit.MakeLabel("Waiting", 16, Palette.Muted, outline: 4).Place(24, 616, 120, 20));
        for (int i = 0; i < Math.Min(7, State.Queue.Count); i++)
        {
            var face = Add(UiKit.MakeSprite($"res://assets/sprites/sellers/{State.Queue[i].Seller.Key}.png", new Vector2(44 + i * 38, 668), 1));
            face.Modulate = new Color(0.8f, 0.8f, 0.85f);
        }
    }

    void BuildPanel()
    {
        var panel = Add(new Node2D { ZIndex = 10 });
        panel.AddChild(UiKit.MakePanel().Place(820, 66, 432, 640));
        panel.AddChild(Header("YOUR APPRAISAL", PanelX, 80));
        _ledgerButton = UiKit.MakeButton("Ledger [L]", Palette.Gold, 16, OpenLedger).Place(PanelX + 236, 72, 148, 36);
        panel.AddChild(_ledgerButton);
        var seller = _visit.Seller;
        string who = _visit.Claim.Count == 0
            ? seller.Listens ? $"{seller.Short} heard nothing, so nothing's ticked." : $"{seller.Short} didn't listen, so nothing's ticked."
            : seller.Listens ? $"Ticked: what {seller.Short} heard." : $"Ticked: what {seller.Short} says is inside.";
        panel.AddChild(UiKit.MakeLabel($"{who} Values: as good as a {Categories.ChestSkin(_visit.Chest.Quality)} chest promises.", 16, Palette.Muted)
            .Wrapped().Place(PanelX, 106, PanelW, 40));

        var kinds = Categories.UpTo(State.Tier).ToList();
        float y = RowsY;
        foreach (var kind in kinds)
        {
            panel.AddChild(MakeRow(kind.Category, kind.Name, kind.Word, y));
            y += RowStep;
        }
        if (State.Tier >= 2)
        {
            panel.AddChild(MakeRow(null, "False bottom", "boom", y));
            y += RowStep;
        }

        // The tools: the witchlight is always on (a sign), the magnet is a switch.
        var signs = new List<(string Icon, string Short, string Long)>();
        if (State.Has("lamp")) signs.Add(("lamp", "Witchlight", "Witchlight lit: enchanted things glow"));
        bool magnet = State.Has("magnet");
        int tools = signs.Count + (magnet ? 1 : 0);
        if (tools > 0)
        {
            float w = (PanelW - 8 * (tools - 1)) / tools;
            float x = PanelX;
            foreach (var (icon, shortName, longName) in signs)
            {
                var lit = UiKit.MakePanel(Palette.Magic.Darkened(0.3f)).Place(x, y + 2, w, 36);
                // Tool icons are 16px art at 3x; two thirds of that is exactly 2x, which fits the sign.
                var sign = UiKit.MakeSprite($"res://assets/ui/icons/{icon}.png", new Vector2(20, 18), 1);
                sign.Scale = Vector2.One * (2f / 3f);
                lit.AddChild(sign);
                lit.AddChild(UiKit.MakeLabel(tools == 1 ? longName : shortName, 16, Palette.Magic).Place(36, 8, w - 40, 20));
                panel.AddChild(lit);
                x += w + 8;
            }
            if (magnet)
            {
                _magnetButton = ToolButton("magnet", ToggleMagnet).Place(x, y + 2, w, 36);
                panel.AddChild(_magnetButton);
            }
        }

        panel.AddChild(UiKit.MakeLabel("As good as promised, it's worth", 16, Palette.Muted).Place(PanelX, 482, 280, 20));
        _worth = UiKit.MakeLabel("", 24, Palette.Text, HorizontalAlignment.Right, outline: 4).Place(PanelX + 260, 476, 124, 30);
        panel.AddChild(_worth);
        _askLabel = UiKit.MakeLabel("", 16, Palette.Muted).Place(PanelX, 510, 280, 20);
        panel.AddChild(_askLabel);
        _ask = UiKit.MakeLabel("", 24, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(PanelX + 260, 504, 124, 30);
        panel.AddChild(_ask);

        // The odds the deal pays off, if the slip is right: no sums to do, and it's only as good as your listening.
        _chance = new Painter(DrawChance) { Position = new Vector2(PanelX, 534) };
        panel.AddChild(_chance);
        _chanceText = UiKit.MakeLabel("", 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(PanelX, 535, PanelW, 20);
        panel.AddChild(_chanceText);

        _slider = new HSlider { Step = 1, MinValue = 1, FocusMode = Control.FocusModeEnum.None };
        UiKit.StyleSlider(_slider);
        _slider.ValueChanged += _ => Refresh();
        panel.AddChild(_slider.Place(PanelX, 560, PanelW, 24));

        _offer = UiKit.MakeButton("", Palette.Good, 16, () => MakeOffer((int)_slider.Value)).Place(PanelX, 590, 188, 50);
        _buy = UiKit.MakeButton("", Palette.Gold, 16, Buy).Place(PanelX, 590, 188, 50);
        _pass = UiKit.MakeButton("Pass", Palette.Bad, 24, Pass).Place(PanelX + 196, 590, 188, 50);
        panel.AddChild(_offer);
        panel.AddChild(_buy);
        panel.AddChild(_pass);
        _footer = UiKit.MakeLabel("", 16, Palette.Muted).Wrapped().Place(PanelX, 648, PanelW, 44);
        panel.AddChild(_footer);

        int guess = Math.Max(1, _visit.Ask > 0 ? Money.Round(_visit.Ask * 0.55f) : Money.Round(_slip.Worth * 0.5f));
        _slider.MaxValue = OfferCeiling(_slip.Worth);
        _slider.Value = guess;
    }

    /// <summary>
    /// A row of the slip: click it to count one more of that kind (up to three from the town shop on, then back to
    /// none); click its star, once you have the witchlight, to mark one of them enchanted.
    /// </summary>
    Button MakeRow(Category? category, string name, string word, float y)
    {
        var row = new Row { Category = category };
        var button = UiKit.MakeButton("", Palette.PanelBorder, 16, () => Cycle(category)).Place(PanelX, y, PanelW, RowH);
        row.Button = button;
        row.Box = new Painter(p => DrawBox(p, CountOf(category), category is Category k && _slip.Magic(k))) { Position = new Vector2(10, 6) };
        button.AddChild(row.Box);
        row.Count = UiKit.MakeLabel("", 16, Palette.Bg, HorizontalAlignment.Center).Place(10, 8, 24, 20);
        button.AddChild(row.Count);
        button.AddChild(category is Category kind ? UiKit.MakeKindIcon(kind, new Vector2(56, RowH / 2)) : UiKit.MakeKindIcon("hidden", new Vector2(56, RowH / 2)));
        button.AddChild(UiKit.MakeLabel(name, 16, Palette.Text).Place(80, 8, 120, 20));
        // Just a reminder of each kind's sound: the slip doesn't keep track of what you've heard.
        button.AddChild(UiKit.MakeLabel(word, 16, Palette.Muted.Darkened(0.2f)).Place(192, 8, 60, 20));
        if (category is Category claimed ? _visit.Claim.Contains(claimed) : _visit.ClaimsHidden)
            button.AddChild(UiKit.MakeLabel("said", 16, Palette.Muted).Place(250, 8, 40, 20));
        if (category is Category magicKind && _lamp)
        {
            row.Star = UiKit.MakeLabel("*", 32, Palette.Muted.Darkened(0.2f), HorizontalAlignment.Center).Place(288, -3, 28, 40);
            row.Star.MouseFilter = Control.MouseFilterEnum.Stop;
            row.Star.TooltipText = "Mark one of them enchanted";
            row.Star.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) ToggleMagic(magicKind);
            };
            button.AddChild(row.Star);
        }
        row.Value = UiKit.MakeLabel("", 16, Palette.Gold, HorizontalAlignment.Right).Place(PanelW - 70, 8, 60, 20);
        button.AddChild(row.Value);
        _rows.Add(row);
        return button;
    }

    int CountOf(Category? category) => category is Category kind ? _slip.Count(kind) : _slip.Hidden ? 1 : 0;

    static void DrawBox(Painter p, int count, bool magic)
    {
        var color = count == 0 ? Palette.Muted.Darkened(0.3f) : magic ? Palette.Magic : Palette.Gold;
        p.DrawRect(new Rect2(0, 0, 24, 24), new Color(0, 0, 0, 0.5f));
        if (count > 0) p.DrawRect(new Rect2(3, 3, 18, 18), color);
        p.DrawRect(new Rect2(0, 0, 24, 24), color, false, 3);
    }

    static Button ToolButton(string icon, Action use)
    {
        var button = UiKit.MakeButton("", Palette.Magic.Darkened(0.45f), 16, use);
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/icons/{icon}.png");
        button.ExpandIcon = false;
        button.AddThemeConstantOverride("icon_max_width", 32);
        button.ClipText = true;
        return button;
    }


    void Refresh()
    {
        bool open = !Concluded && !Busy;
        foreach (var row in _rows)
        {
            int count = CountOf(row.Category);
            bool magic = row.Category is Category k && _slip.Magic(k);
            // Marked: what the slip adds up for this kind. Unmarked: what one would be worth. Both as good as promised.
            row.Value.Text = row.Category is Category kind ? $"{(count > 0 ? _slip.ValueOf(kind) : _slip.OneValue(kind))}g" : $"{_slip.HiddenValue}g";
            row.Value.AddThemeColorOverride("font_color", count == 0 ? Palette.Muted.Darkened(0.2f) : magic ? Palette.Magic : Palette.Gold);
            row.Count.Text = count >= 2 ? count.ToString() : "";
            if (row.Star != null) row.Star.AddThemeColorOverride("font_color", magic ? Palette.Magic : Palette.Muted.Darkened(0.2f));
            row.Box.QueueRedraw();
            row.Button.Disabled = !open;
            row.Button.Modulate = count == 0 ? new Color(1, 1, 1, 0.75f) : Colors.White;
        }

        int worth = _slip.Worth;
        _worth.Text = $"{worth}g";
        string who = _visit.Seller.Short;
        bool offering = _visit.CanOffer;
        bool bought = _visit.Outcome == VisitOutcome.Bought;
        _askLabel.Text = _visit.Trade switch
        {
            _ when bought => "You paid",
            Trade.Offer when _visit.Ask == 0 => $"{who} wants you to name a price",
            Trade.Haggle when _visit.OffersMade > 0 => $"{who}'s final price",
            Trade.Haggle => $"{who} asks (make an offer?)",
            _ => $"{who} asks",
        };
        _ask.Text = bought ? $"{_visit.PricePaid}g" : _visit.Ask > 0 ? $"{_visit.Ask}g" : "?";

        _slider.Visible = offering && open;
        _offer.Visible = offering;
        int offer = (int)_slider.Value;
        _slider.MaxValue = OfferCeiling(worth);
        _offer.Text = $"Offer {offer}g";
        _offer.Disabled = !open || !TradeOpen || offer > State.Gold;
        // With an offer to make, the buy button moves aside (hagglers) or isn't there (name-a-price).
        _buy.Visible = !bought && _visit.Ask > 0 && !(offering && _visit.Trade == Trade.Offer);
        _buy.Text = $"Buy {_visit.Ask}g";
        _buy.Disabled = !open || !TradeOpen || State.Gold < _visit.Ask;
        if (offering && _visit.Trade == Trade.Haggle)
        {
            _offer.Place(PanelX, 590, 124, 50);
            _buy.Place(PanelX + 130, 590, 124, 50);
            _pass.Place(PanelX + 260, 590, 124, 50);
        }
        else
        {
            _offer.Place(PanelX, 590, 188, 50);
            _buy.Place(PanelX, 590, 188, 50);
            _pass.Place(PanelX + 196, 590, 188, 50);
        }
        _pass.Disabled = !open || !TradeOpen;
        if (bought) _pass.Visible = false;

        int price = bought ? _visit.PricePaid : offering ? offer : _visit.Ask;
        bool priced = price > 0 && worth > 0;
        _chanceShown = priced ? _slip.ChanceAbove(price, State.SaleMultiplier) : -1;
        _chanceText.Text = worth == 0 && _hearing.HasTried(Core.Shake.Hard, false) && _hearing.HasTried(Core.Shake.Gentle, false)
                ? "Not a sound: empty, as far as you can tell."
            : worth == 0 ? "Tick what you think is inside."
            : !priced ? "Name a price: the bar shows your odds."
            : $"Worth more than {price}g? {ChanceWords(_chanceShown)}";
        _chance.QueueRedraw();
        _ledgerButton.Disabled = Busy;
        if (_magnetButton != null)
        {
            // Short, so it fits beside the witchlight's sign; the hint under the chest says what it does.
            _magnetButton.Text = _magnet ? "On" : "Magnet";
            _magnetButton.Disabled = !open;
        }

        int left = State.Queue.Count;
        _footer.Text = left == 0 ? "Last customer today." : $"{left} more waiting today.";
    }

    /// <summary>
    /// The most the offer slider goes to: twice what the slip says (or their price), and never less than a few coins
    /// a shop tier, so there's always room to name a price, even for a chest you think is empty. No more than you have.
    /// </summary>
    int OfferCeiling(int worth) => Math.Max(2, Math.Min(State.Gold, Math.Max(12 * State.Tier, Math.Max(_visit.Ask, worth * 2))));

    /// <summary>A chance as the bar says it: "62%", with "under 1%" and "over 99%" rather than a promise.</summary>
    static string ChanceWords(float chance) =>
        chance < 0.01f ? "under 1%" : chance > 0.99f ? "over 99%" : $"{MathF.Round(chance * 100)}%";

    /// <summary>The odds bar: filled as far as the chance, from red (a long shot) through gold to green (a safe bet).</summary>
    void DrawChance(Painter p)
    {
        p.DrawRect(new Rect2(0, 0, PanelW, 22), new Color("#120e1a"));
        if (_chanceShown >= 0)
        {
            float fill = Math.Max(4, PanelW * _chanceShown);
            p.DrawRect(new Rect2(0, 0, fill, 22), Palette.Gradient(_chanceShown).Darkened(0.25f));
            p.DrawRect(new Rect2(0, 0, fill, 4), Palette.Gradient(_chanceShown).Lightened(0.15f));
        }
        p.DrawRect(new Rect2(0, 0, PanelW, 22), Palette.PanelBorder, false, 3);
    }

    void Note(string line)
    {
        _noteLines.Add(line);
        _notes.Text = string.Join("\n", _noteLines.TakeLast(3));
    }

    void OnHeard(Item item, Shake level)
    {
        bool glowed = _lamp && item.Magic;
        _hearing.Heard(item, level, _magnet, glowed);
        Teach($"heard_{item.Category}");
        if (glowed)
        {
            Teach("glow");
            Game.Moment("04_glow", 0.1);
        }
        Game.Moment("01_shake", 0.1);
    }

    /// <summary>The chest came down. Dropped from high up, it tells you what's under the floor, and breaks glass.</summary>
    void OnLanded(float height)
    {
        if (height < ShakeBox.DropHeight || Concluded || Busy) return;
        var (hollow, broke) = _visit.Chest.Drop(_rng, State.Sturdier);
        _hearing.HeardDrop(hollow, broke);
        Game.Shake(0.35f);
        if (hollow)
        {
            Sfx.Play("hollow", 1f, 2);
            _shake.Say("BOOM...", Palette.Gold, 40);
            Note("Dropped it: a hollow BOOM. Something's under the floor!");
            Teach("hollow");
        }
        else
        {
            Sfx.Play("thud", 1f, 2);
            _shake.Say("THUD", Palette.Word, 32);
            Note("Dropped it: a solid thud. No false bottom.");
        }
        if (broke.Count > 0)
        {
            Sfx.Play("shatter", 1f, 0);
            _shake.Say("CRASH!", Palette.Bad, 32, 110);
            Note($"Something made of glass broke: the {Categories.Get(broke[0].Category).Name.ToLowerInvariant()}.");
            Teach("broke");
        }
        Teach("dropped");
        if (_visit.Seller.HatesDrop) Angered(_visit.Anger(_rng));
        else _seller.Say(_rng.PickOne(new[] { "Oi! Careful with that.", "Mind the floor!", "Easy!" }));
        Refresh();
        Game.Moment("05_drop", 0.3);
    }

    void Angered(string line)
    {
        if (line == null) return;
        _seller.Say(line, Palette.Bad);
        _seller.Bob(8);
        Sfx.Play("lose", 0.9f, -4);
        FloatText($"Price +{(int)((Market.AngerMarkup - 1) * 100)}%", new Vector2(1036, 520), Palette.Bad, 24);
        Game.Moment($"05_angry_{_visit.Seller.Key}", 0.3);
    }

    /// <summary>Clicking a row: one more of that kind, or back to none. The false bottom row is just ticked or not.</summary>
    public void Cycle(Category? category)
    {
        if (Busy || Concluded) return;
        if (category is Category kind) _slip.CycleCount(kind);
        else _slip.Hidden = !_slip.Hidden;
        Sfx.Play("tick", 1f + 0.2f * CountOf(category), -4);
        Refresh();
        Teach("slip");
        if (category is Category counted && _slip.Count(counted) >= 2) Teach("counted");
        Game.Moment("02_slip", 0.2);
    }

    /// <summary>Clicking a row's star: one of them is enchanted, or not.</summary>
    public void ToggleMagic(Category kind)
    {
        if (Busy || Concluded || !_lamp) return;
        _slip.ToggleMagic(kind);
        Sfx.Play("sting", _slip.Magic(kind) ? 1.5f : 1.2f, -10);
        Refresh();
        Game.Moment("02_magic", 0.2);
    }

    /// <summary>Sets a row directly (the bot uses this), with the tips clicking it would bring.</summary>
    public void SetMark(Category category, int count, bool magic)
    {
        if (Busy || Concluded) return;
        _slip.Set(category, count, magic);
        Sfx.Play("tick", 1f + 0.2f * count, -4);
        Refresh();
        Teach("slip");
        if (count >= 2) Teach("counted");
    }

    public void SetHidden(bool hidden)
    {
        if (Busy || Concluded) return;
        _slip.Hidden = hidden;
        Refresh();
    }

    /// <summary>Lifts the chest and lets it fall (the bot uses this).</summary>
    public void DropChest()
    {
        if (Busy || Concluded) return;
        _shake.LiftAndDrop();
    }

    public void ToggleMagnet()
    {
        if (Busy || Concluded || !State.Has("magnet")) return;
        _magnet = !_magnet;
        _shake.Magnet = _magnet;
        Sfx.PlayGroup("clack", _magnet ? 1.2f : 0.9f, -2);
        _shake.View.Wobble(0.4f);
        if (_magnet) Teach("magnet");
        if (_magnet && _visit.Seller.HatesMagnet) Angered(_visit.Anger(_rng));
        Refresh();
        Game.Moment("05_magnet", 0.6);
    }

    public void OpenLedger()
    {
        if (Busy) return;
        _ledger = Add(new LedgerView(State) { ZIndex = 30 });
        _ledger.Closed += () =>
        {
            _ledger = null;
            Refresh();
        };
        Game.Moment("02_ledger", 0.4);
        Refresh();
    }

    public void CloseLedger() => _ledger?.Close();

    /// <summary>Turns the open ledger to a page (the bot uses this for screenshots).</summary>
    public void LedgerPage(int page) => _ledger?.ShowPage(page);

    public void MakeOffer(int amount)
    {
        if (Busy || !TradeOpen || !_visit.CanOffer || amount > State.Gold) return;
        var response = _visit.Offer(amount, State.Rng);
        Sfx.Play("bid");
        switch (response.Kind)
        {
            case ResponseKind.Accept:
                _seller.Say(response.Line, Palette.Good);
                _seller.Bob();
                AfterBuy();
                break;
            case ResponseKind.Counter:
                _seller.Say(response.Line, Palette.Warn);
                break;
            case ResponseKind.TryAgain:
                _seller.Say(response.Line, Palette.Warn);
                Teach("try_again");
                break;
            default:
                _seller.Say(response.Line, Palette.Bad);
                Leave();
                break;
        }
        Game.Moment("06_offer", 0.3);
        Refresh();
    }

    public void Buy()
    {
        if (Busy || !TradeOpen || !_visit.CanBuy || State.Gold < _visit.Ask) return;
        _seller.Say(_visit.Buy(State.Rng), Palette.Good);
        _seller.Bob();
        AfterBuy();
    }

    async void AfterBuy()
    {
        _busy = true;
        Refresh();
        State.Opening = State.Buy(_visit, _slip);
        Game.Hud.SetGold(State.Gold);
        Sfx.PlayGroup("pay");
        FloatText($"-{_visit.PricePaid}g", new Vector2(1036, 560), Palette.Gold, 32);

        await Wait(1.0);
        if (!Alive) return;
        var exit = _shake.CreateTween();
        exit.TweenProperty(_shake, "position:y", 60f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        Sfx.PlayGroup("door_close", 1f, -6);
        await ToSignal(_seller.Leave(), Tween.SignalName.Finished);
        if (!Alive) return;
        Game.SwitchTo(_visit.Chest.Lock.Locked ? new LockpickPhase() : new OpeningPhase());
    }

    void RanOutOfPatience()
    {
        _seller.Say(_visit.TimeUp(State.Rng), Palette.Bad);
        Sfx.Play("lose", 1f, -4);
        Leave();
    }

    /// <summary>The seller stormed off with their chest. Your gut has a feeling about what you missed.</summary>
    void Leave()
    {
        State.Pass();
        ShowGut(1.6);
    }

    public void Pass()
    {
        if (Busy || !TradeOpen || Concluded) return;
        _seller.Say(_visit.Pass(State.Rng));
        State.Pass();
        ShowGut(0.6);
    }

    /// <summary>
    /// They take the chest away unopened: you'll never know what was in it. But your gut has a feeling, from what it
    /// really held against what they wanted for it.
    /// </summary>
    async void ShowGut(double delay)
    {
        _busy = true;
        EndLesson();
        Refresh();
        await Wait(delay);
        if (!Alive) return;

        // The chest goes back under the seller's arm.
        var away = _shake.CreateTween().SetParallel();
        away.TweenProperty(_shake, "position:x", -360f, 0.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        away.TweenProperty(_shake, "modulate:a", 0f, 0.5f);
        Sfx.Play("whoosh", 0.8f, -8);
        await Wait(0.5);
        if (!Alive) return;

        int held = _visit.Chest.Value, price = _visit.Price;
        int mood = Gut.PassMood(held, price);
        string line = Gut.Pass(held, price, _rng);
        // Green for a pass that dodged a bad deal, red for one that let a bargain walk out the door.
        var color = Palette.Gradient(Gut.PassScore(held, price));
        _reveal = Add(UiKit.MakePanel(color.Darkened(0.2f)).Place(312, 96, 492, 180));
        _reveal.ZIndex = 20;
        _reveal.AddChild(UiKit.MakeLabel("YOUR GUT SAYS", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 14, 492, 20));
        var words = UiKit.MakeLabel(line, 24, color, HorizontalAlignment.Center, outline: 4).Wrapped().Place(20, 40, 452, 70);
        words.VerticalAlignment = VerticalAlignment.Center;
        _reveal.AddChild(words);
        _reveal.AddChild(UiKit.MakeGutMeter(Gut.PassScore(held, price), 212, "missed out", "right call").Place(20, 116, 452, 30));
        _reveal.AddChild(UiKit.MakeLabel("You'll never know for sure.", 16, Palette.Muted.Darkened(0.2f), HorizontalAlignment.Center).Place(0, 148, 492, 20));
        _reveal.PivotOffset = new Vector2(246, 90);
        _reveal.Scale = Vector2.One * 0.6f;
        _reveal.CreateTween().TweenProperty(_reveal, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        // A good call gets a pleased little chime; a bad one, a groan.
        if (mood <= 1) Sfx.Play("win", 1.1f, -8);
        else if (mood >= 3) Sfx.Play("lose", 0.85f + 0.05f * (5 - mood), -6);
        if (mood >= 4) Game.Shake(0.15f * (mood - 3));

        bool last = State.Queue.Count == 0;
        _buy.Visible = _pass.Visible = _offer.Visible = _slider.Visible = false;
        var next = UiKit.MakeButton(last ? "Close up for the day" : "Next customer", Palette.Gold, 24, Continue).Place(PanelX, 590, PanelW, 50);
        var layer = Add(new Node2D { ZIndex = 10 });
        layer.AddChild(next);
        Teach("passed");
        Game.Moment("03_gut", 0.6);
        Game.Moment($"03_gut_{mood}", 0.6);
    }

    /// <summary>After a pass (or a seller storming off): on to the next customer, or the end of the day.</summary>
    public async void Continue()
    {
        if (_reveal == null || !Alive) return;
        _reveal = null;
        Sfx.PlayGroup("door_close", 1f, -6);
        await ToSignal(_seller.Leave(), Tween.SignalName.Finished);
        if (!Alive) return;
        Game.NextCustomer();
    }
}
