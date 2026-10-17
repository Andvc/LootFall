using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// One seller, one chest, one puzzle. The seller says how hard they shook it and what they heard, and prices it their
/// way: a fixed price, a haggle, or "what'll you give me?". Shake it yourself (gently, firmly, hard), drop it to listen
/// for a false bottom, use your tools, and correct the appraisal slip; it adds up what you ticked at what such things
/// are worth on average in a chest of that colour. Pass, and they show you what was inside. Nothing on this screen
/// knows what the chest is worth.
/// </summary>
public partial class CounterPhase : Phase
{
    static readonly Vector2 ChestRest = new(556, 416);
    // Tall, so a chest can be lifted high and dropped.
    static readonly Rect2 DragBounds = new(452, 130, 232, 286);
    const float PanelX = 844, PanelW = 384, RowH = 38, RowStep = 42, RowsY = 150;

    /// <summary>Shaking at a strength this long counts as having tried it.</summary>
    const double TryTime = 0.5;

    sealed class Row
    {
        public Category? Category;
        public Button Button;
        public Label Tick, Value, Word;
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
    ColorRect _dark;
    Label _hint, _notes, _worth, _askLabel, _ask, _profit, _footer, _timer;
    Button _buy, _pass, _offer, _lampButton, _magnetButton, _ledgerButton;
    HSlider _slider;
    Control _reveal;
    bool _busy, _lamp, _magnet;
    double _patience;
    readonly List<string> _noteLines = new();

    public Visit Visit => _visit;
    public Appraisal Slip => _slip;
    public Hearing Hearing => _hearing;
    public ShakeBox Shake => _shake;
    public MentorView Mentor => _mentor;
    public bool Concluded => !_visit.Deciding;
    public bool Busy => _busy || _ledger != null;

    /// <summary>Seconds an impatient seller will still wait, or 0 if they'll wait all day.</summary>
    public double TimeLeft => _visit.Seller.Patience > 0 ? _patience : 0;

    public override void _Ready()
    {
        _visit = State.Current;
        _slip = new Appraisal(_visit, State.Tier);
        _patience = _visit.Seller.Patience;
        Add(UiKit.MakeBackdrop("cellar_night", 0.75f));
        Add(new Painter(DrawCounter));
        BuildShelf();

        _seller = Add(new SellerView(_visit.Seller) { Position = new Vector2(150, 196) });
        _shake = Add(new ShakeBox(_visit.Chest, DragBounds) { Position = ChestRest });
        _shake.View.LockColor = Palette.Lock(_visit.Chest.Lock);
        _shake.Heard += OnHeard;
        _shake.Landed += OnLanded;
        _hint = Add(UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(316, 452, 484, 20));
        _hint.ZIndex = 10;
        _notes = Add(UiKit.MakeLabel("", 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Wrapped().Place(316, 492, 484, 88));
        _notes.ZIndex = 10;
        _timer = Add(UiKit.MakeLabel("", 32, Palette.Warn, HorizontalAlignment.Center, outline: 6).Place(206, 92, 64, 40));
        _timer.ZIndex = 10;

        // The witchlight: the room goes dark so magic can glow. The panel and the words stay lit.
        _dark = Add(new ColorRect { Color = new Color(0.03f, 0f, 0.08f, 0.78f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false }
            .Place(-100, -100, 1480, 920));
        _dark.ZIndex = 5;

        BuildQueue();
        BuildPanel();
        _mentor = Add(new MentorView { Position = new Vector2(322, 588), ZIndex = 40 });

        Sfx.PlayGroup("door_open", 1f, -6);
        _seller.Enter();
        _seller.Say(_visit.Pitch);
        Game.Hud.SetDay(State.Day, State.Tier);
        Game.Hud.SetGold(State.Gold, animate: false);
        GreetingHints();
        Note(ChestNote());
        Refresh();
        Game.Moment("01_counter", 1.0);
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
        if (firstOfDay && State.Tier > 1) Teach($"tier{State.Tier}");
        if (met) Teach($"seller_{_visit.Seller.Key}");
        Teach(_visit.Trade switch { Trade.Haggle => "haggle", Trade.Offer => "offer", _ => "fixed" });
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
        var level = _shake.Level;
        if (_shake.Dragging && level > Core.Shake.Still)
        {
            var key = (level, _magnet);
            double before = _shaken.GetValueOrDefault(key);
            _shaken[key] = before + delta;
            if (before < TryTime && _shaken[key] >= TryTime) _hearing.Tried(level, _magnet);
        }
        _hint.Text = Busy || Concluded ? ""
            : _shake.Hover ? "Drag to shake it. Lift it high and let go to drop it."
            : _magnet ? "Magnet on: iron can't move"
            : _lamp ? "Witchlight on: magic glows"
            : _shake.HeardAnything ? "" : "Drag the chest to shake it";

        // An impatient seller's clock runs while you're deciding (not while Bram is talking).
        if (_visit.Seller.Patience > 0 && !Concluded && !_busy && !_mentor.Showing)
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

    void DrawCounter(Painter p)
    {
        const float top = 476, right = 816;
        p.DrawRect(new Rect2(0, top, right, 18), new Color("#93602f"));
        p.DrawRect(new Rect2(0, top, right, 3), new Color("#b07a40"));
        p.DrawRect(new Rect2(0, top + 18, right, 720 - top), new Color("#5e3718"));
        for (float x = 0; x < right; x += 96) p.DrawRect(new Rect2(x, top + 18, 3, 720 - top), new Color("#4a2a14"));
        p.DrawRect(new Rect2(0, top + 18, right, 6), new Color(0, 0, 0, 0.35f));
    }

    /// <summary>The shelf behind the counter, with whatever you've kept on display.</summary>
    void BuildShelf()
    {
        const float y = 196, x0 = 300, w = 504;
        Add(new Painter(p =>
        {
            p.DrawRect(new Rect2(x0, y, w, 8), new Color("#6e4520"));
            p.DrawRect(new Rect2(x0, y, w, 2), new Color("#93602f"));
            p.DrawRect(new Rect2(x0 + 8, y + 8, 6, 14), new Color("#4e2c12"));
            p.DrawRect(new Rect2(x0 + w - 14, y + 8, 6, 14), new Color("#4e2c12"));
        }));
        var shown = State.Display;
        for (int i = 0; i < shown.Count; i++)
        {
            var sprite = Add(UiKit.MakeSprite($"res://assets/sprites/items/{shown[i].Sprite}.png", Vector2.Zero, 1));
            float slot = w / Math.Max(6, shown.Count);
            sprite.Position = new Vector2(x0 + slot * (i + 0.5f), y - sprite.Texture.GetHeight() / 2f);
            sprite.Modulate = new Color(0.85f, 0.85f, 0.9f);
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
        string who = _visit.Claim.Count == 0 ? $"{_visit.Seller.Short} heard nothing, so nothing's ticked."
            : $"Ticked: what {_visit.Seller.Short} heard.";
        panel.AddChild(UiKit.MakeLabel($"{who} Values are averages for a {Categories.ChestSkin(_visit.Chest.Quality)} chest.", 16, Palette.Muted)
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

        bool lamp = State.Has("lamp"), magnet = State.Has("magnet");
        if (lamp || magnet)
        {
            float w = lamp && magnet ? (PanelW - 8) / 2 : PanelW;
            float x = PanelX;
            if (lamp)
            {
                _lampButton = ToolButton("lamp", ToggleLamp).Place(x, y + 2, w, 36);
                panel.AddChild(_lampButton);
                x += w + 8;
            }
            if (magnet)
            {
                _magnetButton = ToolButton("magnet", ToggleMagnet).Place(x, y + 2, w, 36);
                panel.AddChild(_magnetButton);
            }
        }

        panel.AddChild(UiKit.MakeLabel("If you're right, it's worth about", 16, Palette.Muted).Place(PanelX, 500, 280, 20));
        _worth = UiKit.MakeLabel("", 24, Palette.Text, HorizontalAlignment.Right, outline: 4).Place(PanelX + 260, 494, 124, 30);
        panel.AddChild(_worth);
        _askLabel = UiKit.MakeLabel("", 16, Palette.Muted).Place(PanelX, 530, 280, 20);
        panel.AddChild(_askLabel);
        _ask = UiKit.MakeLabel("", 24, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(PanelX + 260, 524, 124, 30);
        panel.AddChild(_ask);

        _slider = new HSlider { Step = 1, MinValue = 1, FocusMode = Control.FocusModeEnum.None };
        StyleSlider(_slider);
        _slider.ValueChanged += _ => Refresh();
        panel.AddChild(_slider.Place(PanelX, 558, PanelW, 24));
        _profit = UiKit.MakeLabel("", 16, Palette.Text, HorizontalAlignment.Right).Place(PanelX, 554, PanelW, 20);
        panel.AddChild(_profit);

        _offer = UiKit.MakeButton("", Palette.Good, 16, () => MakeOffer((int)_slider.Value)).Place(PanelX, 590, 188, 50);
        _buy = UiKit.MakeButton("", Palette.Gold, 16, Buy).Place(PanelX, 590, 188, 50);
        _pass = UiKit.MakeButton("Pass", Palette.Bad, 24, Pass).Place(PanelX + 196, 590, 188, 50);
        panel.AddChild(_offer);
        panel.AddChild(_buy);
        panel.AddChild(_pass);
        _footer = UiKit.MakeLabel("", 16, Palette.Muted).Wrapped().Place(PanelX, 648, PanelW, 44);
        panel.AddChild(_footer);

        int guess = Math.Max(1, _visit.Ask > 0 ? Money.Round(_visit.Ask * 0.55f) : Money.Round(_slip.Worth * 0.5f));
        _slider.MaxValue = Math.Max(2, Math.Max(_visit.Ask, _slip.Worth * 2));
        _slider.Value = guess;
    }

    Button MakeRow(Category? category, string name, string word, float y)
    {
        var row = new Row { Category = category };
        var button = UiKit.MakeButton("", Palette.PanelBorder, 16, () => Cycle(category)).Place(PanelX, y, PanelW, RowH);
        row.Button = button;
        row.Box = new Painter(p => DrawBox(p, MarkOf(category))) { Position = new Vector2(10, 7) };
        button.AddChild(row.Box);
        row.Tick = UiKit.MakeLabel("", 16, Palette.Bg, HorizontalAlignment.Center).Place(10, 9, 24, 20);
        button.AddChild(row.Tick);
        button.AddChild(category is Category kind ? UiKit.MakeKindIcon(kind, new Vector2(60, RowH / 2)) : UiKit.MakeKindIcon("hidden", new Vector2(60, RowH / 2)));
        button.AddChild(UiKit.MakeLabel(name, 16, Palette.Text).Place(88, 9, 140, 20));
        row.Word = UiKit.MakeLabel(word, 16, Palette.Muted.Darkened(0.2f)).Place(212, 9, 80, 20);
        button.AddChild(row.Word);
        if (category is Category claimed && _visit.Claim.Contains(claimed))
            button.AddChild(UiKit.MakeLabel("said", 16, Palette.Muted).Place(278, 9, 50, 20));
        row.Value = UiKit.MakeLabel("", 16, Palette.Gold, HorizontalAlignment.Right).Place(PanelW - 112, 9, 100, 20);
        button.AddChild(row.Value);
        _rows.Add(row);
        return button;
    }

    Mark MarkOf(Category? category) => category is Category kind ? _slip[kind] : _slip.Hidden ? Mark.Plain : Mark.None;

    static void DrawBox(Painter p, Mark mark)
    {
        var color = mark switch { Mark.Plain => Palette.Gold, Mark.Magic => Palette.Magic, _ => Palette.Muted.Darkened(0.3f) };
        p.DrawRect(new Rect2(0, 0, 24, 24), new Color(0, 0, 0, 0.5f));
        if (mark != Mark.None) p.DrawRect(new Rect2(3, 3, 18, 18), color);
        p.DrawRect(new Rect2(0, 0, 24, 24), color, false, 3);
    }

    static Button ToolButton(string icon, Action use)
    {
        var button = UiKit.MakeButton("", Palette.Magic.Darkened(0.45f), 16, use);
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/icons/{icon}.png");
        button.ExpandIcon = false;
        button.AddThemeConstantOverride("icon_max_width", 24);
        return button;
    }

    static void StyleSlider(HSlider slider)
    {
        var coin = GD.Load<Texture2D>("res://assets/sprites/items/gold_doubloons.png");
        var track = new StyleBoxFlat { BgColor = new Color("#120e1a"), BorderColor = Palette.PanelBorder, AntiAliasing = false };
        track.SetBorderWidthAll(3);
        track.SetContentMarginAll(6);
        var fill = new StyleBoxFlat { BgColor = Palette.Good.Darkened(0.45f), AntiAliasing = false };
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
        foreach (var row in _rows)
        {
            var mark = MarkOf(row.Category);
            if (row.Category is Category kind)
            {
                int value = _slip.ValueOf(kind, mark == Mark.None ? Mark.Plain : mark);
                row.Value.Text = mark == Mark.Magic ? $"x{Categories.MagicMultiplier} ~{value}g" : $"~{value}g";
                bool heard = _hearing.HeardAny(kind);
                row.Word.AddThemeColorOverride("font_color", _hearing.Broke(kind) ? Palette.Bad
                    : _hearing.Glowed(kind) ? Palette.Magic : heard ? Palette.Word : Palette.Muted.Darkened(0.2f));
                row.Word.Text = _hearing.Broke(kind) ? "broke!" : Categories.Get(kind).Word;
            }
            else
            {
                row.Value.Text = $"~{_slip.HiddenValue}g";
                row.Word.AddThemeColorOverride("font_color", _hearing.Hollow ? Palette.Word : Palette.Muted.Darkened(0.2f));
            }
            row.Value.AddThemeColorOverride("font_color", mark switch { Mark.Magic => Palette.Magic, Mark.Plain => Palette.Gold, _ => Palette.Muted.Darkened(0.2f) });
            row.Tick.Text = mark == Mark.Magic ? "*" : "";
            row.Box.QueueRedraw();
            row.Button.Disabled = !open;
            row.Button.Modulate = mark == Mark.None ? new Color(1, 1, 1, 0.75f) : Colors.White;
        }

        int worth = _slip.Worth;
        _worth.Text = $"~{worth}g";
        string who = _visit.Seller.Short;
        bool offering = _visit.CanOffer;
        _askLabel.Text = _visit.Trade switch
        {
            Trade.Offer when _visit.Ask == 0 => $"{who} wants you to name a price",
            Trade.Haggle when _visit.OffersMade > 0 => $"{who}'s final price",
            Trade.Haggle => $"{who} asks (make an offer?)",
            _ => $"{who} asks",
        };
        _ask.Text = _visit.Ask > 0 ? $"{_visit.Ask}g" : "?";

        _slider.Visible = offering && open;
        _offer.Visible = offering;
        int offer = (int)_slider.Value;
        _slider.MaxValue = Math.Max(2, Math.Min(State.Gold, Math.Max(_visit.Ask, worth * 2)));
        _offer.Text = $"Offer {offer}g";
        _offer.Disabled = !open || offer > State.Gold;
        // With an offer to make, the buy button moves aside (hagglers) or isn't there (name-a-price).
        _buy.Visible = _visit.Ask > 0 && !(offering && _visit.Trade == Trade.Offer);
        _buy.Text = $"Buy {_visit.Ask}g";
        _buy.Disabled = !open || State.Gold < _visit.Ask;
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
        _pass.Disabled = !open;

        int price = offering ? offer : _visit.Ask;
        int margin = worth - price;
        _profit.Visible = !_slider.Visible;
        _profit.Text = worth == 0 ? "Tick what you think is inside." : $"{(margin > 0 ? "+" : "")}{margin}g on average, if you're right";
        _profit.AddThemeColorOverride("font_color", worth == 0 ? Palette.Muted : margin > 0 ? Palette.Good : Palette.Bad);
        _ledgerButton.Disabled = Busy;
        if (_lampButton != null)
        {
            _lampButton.Text = _lamp ? "Witchlight on" : "Witchlight";
            _lampButton.Disabled = !open;
        }
        if (_magnetButton != null)
        {
            _magnetButton.Text = _magnet ? "Magnet on" : "Magnet";
            _magnetButton.Disabled = !open;
        }

        int left = State.Queue.Count;
        _footer.Text = (offering ? $"Offer: {offer}g, about {(margin > 0 ? "+" : "")}{margin}g if you're right. " : "") +
                       (left == 0 ? "Last customer today." : $"{left} more waiting today.");
    }

    void Note(string line)
    {
        _noteLines.Add(line);
        _notes.Text = string.Join("\n", _noteLines.TakeLast(3));
    }

    void OnHeard(Item item, Shake level)
    {
        bool glowed = _lamp && item.Magic;
        bool first = !_hearing.HeardAny(item.Category);
        _hearing.Heard(item.Category, level, _magnet, glowed);
        if (first || glowed) Refresh();
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
        var (hollow, broke) = _visit.Chest.Drop(_rng);
        _hearing.HeardDrop(hollow, broke.Select(i => i.Category));
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
    }

    /// <summary>Clicking a row: ticked, (with a witchlight) magic, unticked. The false bottom row is just ticked or not.</summary>
    public void Cycle(Category? category)
    {
        if (Busy || Concluded) return;
        if (category is Category kind) _slip.Cycle(kind, State.Has("lamp"));
        else _slip.Hidden = !_slip.Hidden;
        Sfx.Play("tick", 1f + 0.2f * (int)MarkOf(category), -4);
        Refresh();
        Teach("slip");
        Game.Moment("02_slip", 0.2);
    }

    /// <summary>Sets a row directly (the bot uses this).</summary>
    public void SetMark(Category category, Mark mark)
    {
        if (Busy || Concluded) return;
        _slip[category] = mark;
        Sfx.Play("tick", 1f + 0.2f * (int)mark, -4);
        Refresh();
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

    public void ToggleLamp()
    {
        if (Busy || Concluded || !State.Has("lamp")) return;
        _lamp = !_lamp;
        _shake.Witchlight = _lamp;
        _dark.Visible = _lamp;
        Sfx.Play("lantern", _lamp ? 1f : 0.8f);
        if (_lamp) Teach("lamp");
        Refresh();
        Game.Moment("04_witchlight", 0.6);
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
        if (Busy || !_visit.CanOffer || amount > State.Gold) return;
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
        if (Busy || !_visit.CanBuy || State.Gold < _visit.Ask) return;
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

    /// <summary>The seller stormed off with their chest: no showing you what was in it.</summary>
    async void Leave()
    {
        _busy = true;
        State.Pass();
        Refresh();
        await Wait(1.6);
        if (!Alive) return;
        var exit = _shake.CreateTween();
        exit.TweenProperty(_shake, "modulate:a", 0f, 0.3f);
        Sfx.PlayGroup("door_close", 1f, -4);
        await ToSignal(_seller.Leave(), Tween.SignalName.Finished);
        if (!Alive) return;
        Game.NextCustomer();
    }

    public void Pass()
    {
        if (Busy || Concluded) return;
        _seller.Say(_visit.Pass(State.Rng));
        State.Pass();
        ShowWhatWasInside();
    }

    /// <summary>Passing still teaches: the seller opens the lid to show you, and each thing is checked against your slip.</summary>
    async void ShowWhatWasInside()
    {
        _busy = true;
        Refresh();
        await Wait(0.6);
        if (!Alive) return;

        var lid = _shake.View.DetachLid();
        _shake.View.ShowOpen();
        Sfx.PlayGroup("latch", 1f, -4);
        var fling = lid.CreateTween().SetParallel();
        fling.TweenProperty(lid, "position", lid.Position + new Vector2(150, -40), 0.35f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        fling.TweenProperty(lid, "rotation", 0.6f, 0.35f);
        fling.TweenProperty(lid, "modulate:a", 0f, 0.3f).SetDelay(0.1f);

        var chest = _visit.Chest;
        var report = _slip.Check(chest);
        _reveal = Add(UiKit.MakePanel(Palette.PanelBorder).Place(312, 72, 492, 262));
        _reveal.ZIndex = 20;
        _reveal.AddChild(UiKit.MakeLabel($"{_visit.Seller.Short} shows you what was inside", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 10, 492, 20));
        float colW = 492f / report.Items.Count;
        for (int i = 0; i < report.Items.Count; i++)
        {
            var read = report.Items[i];
            var at = new Vector2(colW * (i + 0.5f), 70);
            var icon = UiKit.MakeSprite($"res://assets/sprites/items/{read.Item.Sprite}.png", at, 1);
            int scale = Math.Max(1, (int)MathF.Min(48f / icon.Texture.GetWidth(), 48f / icon.Texture.GetHeight()));
            _reveal.AddChild(icon);
            Pop(icon, scale, i * 0.15f);
            _reveal.AddChild(UiKit.MakeLabel(read.Item.Name, 16, Palette.Of(read.Item.Rarity), HorizontalAlignment.Center)
                .Wrapped().Place(at.X - colW / 2 + 4, 100, colW - 8, 40));
            _reveal.AddChild(UiKit.MakeLabel($"{read.Item.Value}g", 16, Palette.Gold, HorizontalAlignment.Center).Place(at.X - 60, 140, 120, 20));
            _reveal.AddChild(UiKit.MakeCheck(read.Right, new Vector2(at.X + 34, 52), 1));
        }
        int worth = chest.Value, price = Math.Max(_visit.Ask, 1), diff = worth - price;
        string verdict = _visit.Ask == 0 ? $"Worth {worth}g."
            : diff > 0 ? $"Worth {worth}g for {price}g: you'd have made {diff}g."
            : $"Worth {worth}g for {price}g. Good pass.";
        _reveal.AddChild(UiKit.MakeLabel(verdict, 16, diff > 0 ? Palette.Warn : Palette.Good, HorizontalAlignment.Center).Place(0, 166, 492, 20));
        var ghosts = report.Ghosts.Select(c => $"no {Categories.Get(c).Name.ToLowerInvariant()}").ToList();
        if (report.GhostHidden) ghosts.Add("no false bottom");
        string why = ghosts.Count > 0 ? $"{_visit.Explain()} (Your slip: {string.Join(", ", ghosts)}.)" : _visit.Explain();
        _reveal.AddChild(UiKit.MakeLabel(why, 16, Palette.Muted, HorizontalAlignment.Center).Wrapped().Place(16, 190, 460, 64));
        foreach (var read in report.Items) Sfx.PlayGroup(read.Item.Def.Sound, read.Item.Def.Pitch, -6);

        bool last = State.Queue.Count == 0;
        _buy.Visible = _pass.Visible = _offer.Visible = _slider.Visible = false;
        var next = UiKit.MakeButton(last ? "Close up for the day" : "Next customer", Palette.Gold, 24, Continue).Place(PanelX, 590, PanelW, 50);
        var layer = Add(new Node2D { ZIndex = 10 });
        layer.AddChild(next);
        Teach("passed");
        Game.Moment("03_passed", 0.8);
    }

    static void Pop(Node2D node, float scale, float delay)
    {
        node.Scale = Vector2.Zero;
        var tween = node.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(node, "scale", Vector2.One * scale, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>After a pass: on to the next customer, or the end of the day.</summary>
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
