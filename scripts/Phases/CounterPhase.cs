using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// One seller, one chest, one puzzle. The seller says how hard they shook it and what they heard, and asks a price for
/// that. Shake it yourself at every strength: the meter under the chest records what you heard at each. Correct the
/// appraisal slip (tick what's there, untick what isn't, mark what glowed as magic) and buy if it's worth more than
/// the ask. Pass, and they show you what was inside. Nothing on this screen knows what the chest is worth: the slip only
/// adds up what you ticked.
/// </summary>
public partial class CounterPhase : Phase
{
    static readonly Vector2 ChestRest = new(556, 416);
    static readonly Rect2 DragBounds = new(452, 220, 232, 196);
    const float PanelX = 844, PanelW = 384, RowH = 40, RowStep = 46, RowsY = 150;

    /// <summary>Shaking at a strength this long counts as having tried it.</summary>
    const double TryTime = 0.5;

    sealed class Row
    {
        public Category Category;
        public Button Button;
        public Label Tick, Value;
        public Painter Box;
    }

    Visit _visit;
    Appraisal _slip;
    readonly Hearing _hearing = new();
    readonly Dictionary<(Shake, bool), double> _shaken = new();
    readonly List<Row> _rows = new();
    SellerView _seller;
    ShakeBox _shake;
    HearingChart _chart;
    MentorView _mentor;
    LedgerView _ledger;
    ColorRect _dark;
    Label _hint, _worth, _profit, _footer;
    Button _buy, _pass, _lampButton, _magnetButton, _ledgerButton;
    Control _reveal;
    bool _busy, _lamp, _magnet;

    public Visit Visit => _visit;
    public Appraisal Slip => _slip;
    public Hearing Hearing => _hearing;
    public ShakeBox Shake => _shake;
    public MentorView Mentor => _mentor;
    public bool Concluded => _visit.Outcome != VisitOutcome.Deciding;
    public bool Busy => _busy || _ledger != null;

    public override void _Ready()
    {
        _visit = State.Current;
        _slip = new Appraisal(_visit);
        Add(UiKit.MakeBackdrop("cellar_night", 0.75f));
        Add(new Painter(DrawCounter));

        _seller = Add(new SellerView(_visit.Seller) { Position = new Vector2(150, 196) });
        _shake = Add(new ShakeBox(_visit.Chest, DragBounds) { Position = ChestRest });
        _shake.Heard += OnHeard;
        _chart = Add(new HearingChart { Position = new Vector2(316, 486), ZIndex = 10 });
        _hint = Add(UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(316, 452, HearingChart.W, 20));
        _hint.ZIndex = 10;

        // The witchlight: the room goes dark so magic can glow. The panel, the meter and the words stay lit.
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
        RefreshChart();
        Refresh();
        Game.Moment("01_counter", 1.0);
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
        if (firstOfDay && State.Tier > 1) Teach($"tier{State.Tier}");
        if (met) Teach($"seller_{_visit.Seller.Key}");
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
        _chart.SetShake(_shake.Speed, level);
        if (_shake.Dragging && level > Core.Shake.Still)
        {
            var key = (level, _magnet);
            double before = _shaken.GetValueOrDefault(key);
            _shaken[key] = before + delta;
            if (before < TryTime && _shaken[key] >= TryTime)
            {
                _hearing.Tried(level, _magnet);
                RefreshChart();
            }
        }
        _hint.Text = Busy || Concluded ? ""
            : _shake.Dragging ? $"Shaking {Categories.Adverb(level)}{(_magnet ? ", magnet on" : "")}"
            : _shake.Hover ? "Drag to shake: slowly for gentle, fast for hard"
            : _magnet ? "Magnet on: iron can't move"
            : _lamp ? "Witchlight on: magic glows"
            : _shake.HeardAnything ? "" : "Drag the chest to shake it";
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
        panel.AddChild(UiKit.MakeLabel($"{who} Click a row to change it.", 16, Palette.Muted).Wrapped().Place(PanelX, 106, PanelW, 40));

        var kinds = Categories.UpTo(State.Tier).ToList();
        for (int i = 0; i < kinds.Count; i++) panel.AddChild(MakeRow(kinds[i], RowsY + i * RowStep));

        float y = RowsY + kinds.Count * RowStep + 4;
        bool lamp = State.Has("lamp"), magnet = State.Has("magnet");
        if (lamp || magnet)
        {
            float w = lamp && magnet ? (PanelW - 8) / 2 : PanelW;
            float x = PanelX;
            if (lamp)
            {
                _lampButton = ToolButton("lamp", ToggleLamp).Place(x, y, w, 40);
                panel.AddChild(_lampButton);
                x += w + 8;
            }
            if (magnet)
            {
                _magnetButton = ToolButton("magnet", ToggleMagnet).Place(x, y, w, 40);
                panel.AddChild(_magnetButton);
            }
        }

        panel.AddChild(UiKit.MakeLabel("If you're right, it's worth", 16, Palette.Muted).Place(PanelX, 500, 260, 20));
        _worth = UiKit.MakeLabel("", 32, Palette.Text, HorizontalAlignment.Right, outline: 4).Place(PanelX + 220, 488, 164, 40);
        panel.AddChild(_worth);
        panel.AddChild(UiKit.MakeLabel($"{_visit.Seller.Short} asks", 16, Palette.Muted).Place(PanelX, 540, 260, 20));
        panel.AddChild(UiKit.MakeLabel($"{_visit.Ask}g", 32, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(PanelX + 220, 528, 164, 40));
        _profit = UiKit.MakeLabel("", 16, Palette.Text, HorizontalAlignment.Right).Place(PanelX, 570, PanelW, 20);
        panel.AddChild(_profit);
        _buy = UiKit.MakeButton("", Palette.Gold, 24, Buy).Place(PanelX, 598, 188, 52);
        _pass = UiKit.MakeButton("Pass", Palette.Bad, 24, Pass).Place(PanelX + 196, 598, 188, 52);
        panel.AddChild(_buy);
        panel.AddChild(_pass);
        _footer = UiKit.MakeLabel("", 16, Palette.Muted).Wrapped().Place(PanelX, 658, PanelW, 44);
        panel.AddChild(_footer);
    }

    Button MakeRow(CategoryDef kind, float y)
    {
        var row = new Row { Category = kind.Category };
        var button = UiKit.MakeButton("", Palette.PanelBorder, 16, () => Cycle(kind.Category)).Place(PanelX, y, PanelW, RowH);
        row.Button = button;
        row.Box = new Painter(p => DrawBox(p, _slip[kind.Category])) { Position = new Vector2(10, 8) };
        button.AddChild(row.Box);
        row.Tick = UiKit.MakeLabel("", 16, Palette.Bg, HorizontalAlignment.Center).Place(10, 10, 24, 20);
        button.AddChild(row.Tick);
        button.AddChild(UiKit.MakeKindIcon(kind.Category, new Vector2(60, RowH / 2)));
        button.AddChild(UiKit.MakeLabel(kind.Name, 16, Palette.Text).Place(88, 10, 120, 20));
        button.AddChild(UiKit.MakeLabel(kind.Word, 16, Palette.Word.Darkened(0.25f)).Place(196, 10, 80, 20));
        button.AddChild(UiKit.MakeLabel(_visit.Claim.Contains(kind.Category) ? "said" : "", 16, Palette.Muted).Place(270, 10, 50, 20));
        row.Value = UiKit.MakeLabel("", 16, Palette.Gold, HorizontalAlignment.Right).Place(PanelW - 112, 10, 100, 20);
        button.AddChild(row.Value);
        _rows.Add(row);
        return button;
    }

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

    void Refresh()
    {
        bool open = !Concluded && !Busy;
        foreach (var row in _rows)
        {
            var mark = _slip[row.Category];
            int value = _slip.ValueOf(row.Category, mark == Mark.None ? Mark.Plain : mark);
            row.Value.Text = mark == Mark.Magic ? $"x{Categories.MagicMultiplier} {value}g" : $"{value}g";
            row.Value.AddThemeColorOverride("font_color", mark switch { Mark.Magic => Palette.Magic, Mark.Plain => Palette.Gold, _ => Palette.Muted.Darkened(0.2f) });
            row.Tick.Text = mark == Mark.Magic ? "*" : "";
            row.Box.QueueRedraw();
            row.Button.Disabled = !open;
            row.Button.Modulate = mark == Mark.None ? new Color(1, 1, 1, 0.75f) : Colors.White;
        }

        int worth = _slip.Worth;
        int margin = worth - _visit.Ask;
        _worth.Text = $"{worth}g";
        _profit.Text = worth == 0 ? "Tick what you think is inside."
            : margin > 0 ? $"+{margin}g if you're right"
            : $"{margin}g if you're right";
        _profit.AddThemeColorOverride("font_color", worth == 0 ? Palette.Muted : margin > 0 ? Palette.Good : Palette.Bad);
        _buy.Text = $"Buy {_visit.Ask}g";
        _buy.Disabled = !open || State.Gold < _visit.Ask;
        _pass.Disabled = !open;
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
        _footer.Text = (left == 0 ? "Last customer today." : $"{left} more waiting today.") +
                       (State.Gold < _visit.Ask ? $" You have {State.Gold}g." : "");
    }

    void RefreshChart() => _chart.Show(_hearing, _magnet);

    void OnHeard(Item item, Shake level)
    {
        bool glowed = _lamp && item.Magic;
        _hearing.Heard(item.Category, level, _magnet, glowed);
        RefreshChart();
        Teach($"heard_{item.Category}");
        if (glowed)
        {
            Teach("glow");
            Game.Moment("04_glow", 0.1);
        }
        Game.Moment("01_shake", 0.1);
    }

    /// <summary>Clicking a row: ticked, (with a witchlight) magic, unticked.</summary>
    public void Cycle(Category category)
    {
        if (Busy || Concluded) return;
        _slip.Cycle(category, State.Has("lamp"));
        Sfx.Play("tick", 1f + 0.2f * (int)_slip[category], -4);
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

    public void ToggleLamp()
    {
        if (Busy || Concluded || !State.Has("lamp")) return;
        _lamp = !_lamp;
        _shake.Witchlight = _lamp;
        _dark.Visible = _lamp;
        Sfx.Play("lantern", _lamp ? 1f : 0.8f);
        if (_lamp) Teach("lamp");
        RefreshChart();
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
        RefreshChart();
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

    public void Buy()
    {
        if (Busy || Concluded || State.Gold < _visit.Ask) return;
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
        Game.SwitchTo(new OpeningPhase());
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
        float colW = 492f / chest.Items.Count;
        for (int i = 0; i < report.Items.Count; i++)
        {
            var read = report.Items[i];
            var at = new Vector2(colW * (i + 0.5f), 70);
            var icon = UiKit.MakeSprite($"res://assets/sprites/items/{read.Item.Sprite}.png", at, 1);
            int scale = Math.Max(1, (int)MathF.Min(48f / icon.Texture.GetWidth(), 48f / icon.Texture.GetHeight()));
            _reveal.AddChild(icon);
            Pop(icon, scale, i * 0.15f);
            _reveal.AddChild(UiKit.MakeLabel(read.Item.Name, 16, read.Item.Magic ? Palette.Magic : Palette.Text, HorizontalAlignment.Center)
                .Wrapped().Place(at.X - colW / 2 + 4, 100, colW - 8, 40));
            _reveal.AddChild(UiKit.MakeLabel($"{read.Item.Value}g", 16, Palette.Gold, HorizontalAlignment.Center).Place(at.X - 60, 140, 120, 20));
            _reveal.AddChild(UiKit.MakeCheck(read.Right, new Vector2(at.X + 34, 52), 1));
        }
        int worth = chest.Value, diff = worth - _visit.Ask;
        string verdict = diff > 0 ? $"Worth {worth}g for {_visit.Ask}g: you'd have made {diff}g."
            : $"Worth {worth}g for {_visit.Ask}g. Good pass.";
        _reveal.AddChild(UiKit.MakeLabel(verdict, 16, diff > 0 ? Palette.Warn : Palette.Good, HorizontalAlignment.Center).Place(0, 166, 492, 20));
        string ghosts = string.Join(", ", report.Ghosts.Select(c => $"no {Categories.Get(c).Name.ToLowerInvariant()}"));
        string why = ghosts.Length > 0 ? $"{_visit.Explain()} (Your slip: {ghosts}.)" : _visit.Explain();
        _reveal.AddChild(UiKit.MakeLabel(why, 16, Palette.Muted, HorizontalAlignment.Center).Wrapped().Place(16, 190, 460, 64));
        foreach (var read in report.Items) Sfx.PlayGroup(read.Item.Def.Sound, read.Item.Def.Pitch, -6);

        bool last = State.Queue.Count == 0;
        _buy.Visible = _pass.Visible = false;
        var next = UiKit.MakeButton(last ? "Close up for the day" : "Next customer", Palette.Gold, 24, Continue).Place(PanelX, 598, PanelW, 52);
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
