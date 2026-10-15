using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>Inspect a sealed lot, watch the estimate tighten, then outbid (or let go to) a rival.</summary>
public partial class AuctionPhase : Phase
{
    public enum Inspection { Shake, Weigh, Lock }
    enum Leader { None, Player, Rival }

    const int Inspections = 2;
    const float BarX = 684, BarY = 362, BarW = 532, BarH = 26;

    readonly Dictionary<Inspection, (Button Button, Label Result)> _inspect = new();
    ChestView _chest;
    Label _inspectionsLeft, _estimateText, _estimateNote, _quip, _priceLabel, _leaderLabel, _bidHint;
    Painter _bar;
    Button _bid, _pass;
    float _bandLow, _bandHigh, _bandMid;
    int _bidPrice;
    int _left = Inspections;
    Leader _leader = Leader.None;

    public bool Over { get; private set; }
    public bool Busy { get; private set; }
    public bool PlayerLeads => _leader == Leader.Player;
    int NextBid => _leader == Leader.None ? _bidPrice : _bidPrice + Auction.Increment(_bidPrice);
    public bool CanBid => !Over && !Busy && _leader != Leader.Player && NextBid <= State.Gold;

    public override void _Ready()
    {
        State.NextLot();
        Game.Hud.SetLot(State.LotNumber);
        _bidPrice = State.OpeningPrice;
        var est = State.Estimate;
        (_bandLow, _bandMid, _bandHigh) = (est.Low, est.Median, est.High);

        BuildLot();
        BuildInspection();
        BuildAppraisal();
        BuildBidding();
        Refresh();

        if (State.Gold < State.OpeningPrice && State.Gold < 20) ShowBankrupt();
    }

    void BuildLot()
    {
        Add(new Painter(p =>
        {
            for (int i = 8; i >= 1; i--) p.DrawCircle(new Vector2(330, 340), 40 + i * 30, Palette.BgGlow with { A = 0.08f });
        }));
        _chest = Add(new ChestView { Position = new Vector2(330, 360), Scale = Vector2.One * 1.25f });

        var c = State.Chest;
        Add(UiKit.MakeLabel($"LOT {State.LotNumber}", 14, Palette.Muted).Place(90, 500, 480, 20));
        Add(UiKit.MakeLabel(c.Provenance.Name, 28, Palette.Text).Place(90, 520, 480, 38));
        Add(UiKit.MakeLabel(c.Provenance.Blurb, 16, Palette.Muted).Wrapped().Place(90, 560, 480, 48));
        Add(UiKit.MakeLabel("Inspect, bid, or let it go. You get two inspections per lot.", 14, Palette.Muted.Darkened(0.2f))
            .Place(90, 640, 520, 20));
    }

    void BuildInspection()
    {
        Add(UiKit.MakePanel(Palette.Panel, Palette.PanelBorder).Place(660, 72, 580, 610));
        Add(Header("INSPECT", 684, 88));
        _inspectionsLeft = Add(UiKit.MakeLabel("", 14, Palette.Gold, HorizontalAlignment.Right).Place(1016, 88, 200, 20));

        var rows = new[] { (Inspection.Shake, "Shake it"), (Inspection.Weigh, "Weigh it"), (Inspection.Lock, "Examine lock") };
        for (int i = 0; i < rows.Length; i++)
        {
            var (kind, text) = rows[i];
            float y = 116 + i * 52;
            var button = Add(UiKit.MakeButton(text, Palette.Gold, 17, () => Inspect(kind)).Place(684, y, 150, 42));
            var result = Add(UiKit.MakeLabel("", 14, Palette.Text).Wrapped().Place(846, y - 4, 372, 50));
            result.VerticalAlignment = VerticalAlignment.Center;
            _inspect[kind] = (button, result);
        }
    }

    void BuildAppraisal()
    {
        Add(Header("APPRAISAL", 684, 278));
        _estimateText = Add(UiKit.MakeLabel("", 22, Palette.Text).Place(684, 298, 540, 30));
        _estimateNote = Add(UiKit.MakeLabel("", 13, Palette.Muted).Place(684, 330, 540, 20));
        _bar = Add(new Painter(DrawBar) { Position = new Vector2(BarX, BarY) });
        Add(UiKit.MakeLabel("0g", 12, Palette.Muted).Place(BarX, BarY + 30, 100, 16));
        Add(UiKit.MakeLabel($"{State.Appraiser.ScaleMax}g+", 12, Palette.Muted, HorizontalAlignment.Right)
            .Place(BarX + BarW - 100, BarY + 30, 100, 16));
    }

    void BuildBidding()
    {
        var rival = State.Rival.Def;
        Add(Header("AUCTION", 684, 420));
        var rivalColor = new Color(rival.ColorHex);
        Add(new Painter(p =>
        {
            p.DrawCircle(Vector2.Zero, 24, rivalColor.Darkened(0.5f));
            p.DrawCircle(Vector2.Zero, 21, rivalColor);
            p.DrawString(ThemeDB.FallbackFont, new Vector2(-8, 8), rival.Name[..1], HorizontalAlignment.Left, -1, 24, Palette.Bg);
        }) { Position = new Vector2(710, 470) });
        Add(UiKit.MakeLabel(rival.Name, 19, rivalColor).Place(746, 448, 400, 26));
        Add(UiKit.MakeLabel(rival.Tagline, 13, Palette.Muted).Place(746, 474, 470, 20));
        _quip = Add(UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(684, 500, 540, 44));

        Add(UiKit.MakeLabel("Current bid", 13, Palette.Muted).Place(684, 552, 200, 18));
        _priceLabel = Add(UiKit.MakeLabel("", 36, Palette.Gold).Place(684, 568, 140, 46));
        _leaderLabel = Add(UiKit.MakeLabel("", 15, Palette.Muted).Place(684, 614, 240, 22));
        _bid = Add(UiKit.MakeButton("", Palette.Good, 20, Bid).Place(890, 568, 180, 52));
        _pass = Add(UiKit.MakeButton("Pass", Palette.Bad, 20, Pass).Place(1080, 568, 136, 52));
        _bidHint = Add(UiKit.MakeLabel("", 12, Palette.Muted).Place(890, 626, 330, 18));
    }

    public void Inspect(Inspection kind)
    {
        if (Over || _left <= 0 || _inspect[kind].Button.Disabled) return;
        _left--;
        var chest = State.Chest;
        var (button, result) = _inspect[kind];
        button.Disabled = true;

        switch (kind)
        {
            case Inspection.Shake:
                State.Clues.Shaken = true;
                _chest.Wobble(1f);
                Sfx.Play("shake_" + chest.DominantSound.ToString().ToLowerInvariant());
                result.Text = Describe.Shake(chest);
                break;
            case Inspection.Weigh:
                State.Clues.Weighed = true;
                Sfx.Play("scale");
                _chest.Wobble(0.3f);
                result.Text = Describe.Weigh(chest);
                break;
            case Inspection.Lock:
                State.Clues.LockExamined = true;
                Sfx.Play("inspect_lock");
                _chest.LockColor = Palette.Lock(chest.Lock.Index);
                result.Text = Describe.Lock(chest);
                break;
        }

        result.Modulate = Colors.Transparent;
        result.CreateTween().TweenProperty(result, "modulate", Colors.White, 0.4f);
        if (_left == 0)
            foreach (var (b, _) in _inspect.Values.Where(v => !v.Button.Disabled))
                b.Disabled = true;

        UpdateEstimate();
        Refresh();
    }

    void UpdateEstimate()
    {
        State.RefreshEstimate();
        var est = State.Estimate;
        var tween = CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(v => _bandLow = v), _bandLow, (float)est.Low, 0.7f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenMethod(Callable.From<float>(v => _bandHigh = v), _bandHigh, (float)est.High, 0.7f).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenMethod(Callable.From<float>(v => _bandMid = v), _bandMid, (float)est.Median, 0.7f).SetTrans(Tween.TransitionType.Cubic);
        Sfx.Play("blip", 0.6f, -8);
    }

    public async void Bid()
    {
        if (!CanBid) return;
        _bidPrice = NextBid;
        _leader = Leader.Player;
        Busy = true;
        Sfx.Play("bid");
        Refresh();

        await Wait(0.8);
        if (!Alive) return;

        var rival = State.Rival;
        int counter = _bidPrice + Auction.Increment(_bidPrice);
        if (counter <= rival.MaxBid)
        {
            _bidPrice = counter;
            _leader = Leader.Rival;
            Say(rival.Def.RaiseLines);
            Sfx.Play("bid", 0.75f);
            Busy = false;
            Refresh();
        }
        else
        {
            Say(rival.Def.FoldLines);
            Sfx.Play("fold");
            Busy = false;
            await Win();
        }
    }

    public void Pass()
    {
        if (Over || Busy || _leader == Leader.Player) return;
        Over = true;
        bool rivalTakesIt = _leader == Leader.Rival || State.Rival.MaxBid >= _bidPrice;
        if (rivalTakesIt) Say(State.Rival.Def.WinLines);
        Sfx.Play(rivalTakesIt ? "gavel" : "fold");
        Refresh();
        ShowMissedLot(rivalTakesIt);
    }

    async Task Win()
    {
        Over = true;
        foreach (var (button, _) in _inspect.Values) button.Disabled = true;
        State.PricePaid = _bidPrice;
        State.Gold -= _bidPrice;
        Game.Hud.SetGold(State.Gold);
        Refresh();

        Sfx.Play("gavel");
        Game.Shake(0.3f);
        _chest.Wobble(0.6f);
        FloatText("SOLD!", new Vector2(330, 200), Palette.Gold, 52);
        Game.Moment("02_sold", 0.2);

        await Wait(1.4);
        if (!Alive) return;
        Game.SwitchTo(new LockpickPhase());
    }

    /// <summary>Ends inspection and dims the screen behind a modal panel.</summary>
    void CloseLot()
    {
        foreach (var (button, _) in _inspect.Values) button.Disabled = true;
        var scrim = Add(new ColorRect { Color = new Color(0, 0, 0, 0.6f) }.Place(-100, -100, 1480, 920));
        scrim.Modulate = Colors.Transparent;
        scrim.CreateTween().TweenProperty(scrim, "modulate", Colors.White, 0.25f);
    }

    void Say(string[] lines) => _quip.Text = $"\"{State.Rival.Say(lines, State.Rng, _bidPrice)}\"";

    void Refresh()
    {
        var est = State.Estimate;
        _inspectionsLeft.Text = _left == 0 ? "Out of time to inspect" : $"{_left} inspection{(_left == 1 ? "" : "s")} left";
        _estimateText.Text = $"Worth roughly {est.Low}g - {est.High}g";
        _estimateNote.Text = $"Best guess {est.Median}g. Based on {est.Samples:N0} similar lots; right about 4 times in 5.";

        _priceLabel.Text = $"{_bidPrice}g";
        (_leaderLabel.Text, var leaderColor) = _leader switch
        {
            Leader.Player => ("You lead", Palette.Good),
            Leader.Rival => ($"{State.Rival.Def.Name} leads", Palette.Warn),
            _ => ("Opening bid, no takers yet", Palette.Muted),
        };
        _leaderLabel.AddThemeColorOverride("font_color", leaderColor);

        _bid.Text = $"Bid {NextBid}g";
        _bid.Disabled = !CanBid;
        _pass.Disabled = Over || Busy || _leader == Leader.Player;
        _bidHint.Text = NextBid > State.Gold && !Over ? "You can't afford the next bid." : $"Bids rise by {Auction.Increment(_bidPrice)}g.";
        _bar.QueueRedraw();
    }

    public override void _Process(double delta) => _bar.QueueRedraw();

    void DrawBar(Painter p)
    {
        float max = State.Appraiser.ScaleMax;
        float X(float v) => BarW * Mathf.Clamp(v / max, 0f, 1f);

        p.DrawRect(new Rect2(0, 0, BarW, BarH), Palette.Bg);
        float x0 = X(_bandLow), x1 = X(_bandHigh);
        p.DrawRect(new Rect2(x0, 2, Mathf.Max(3, x1 - x0), BarH - 4), Palette.Gold with { A = 0.3f });
        p.DrawRect(new Rect2(x0, 2, Mathf.Max(3, x1 - x0), BarH - 4), Palette.Gold, false, 2);
        float xm = X(_bandMid);
        p.DrawLine(new Vector2(xm, 0), new Vector2(xm, BarH), Palette.Gold, 3);
        p.DrawRect(new Rect2(0, 0, BarW, BarH), Palette.PanelBorder, false, 2);

        // Current price marker: green while it's a bargain, red once it's past your best guess.
        var price = (float)_bidPrice;
        var color = price <= _bandLow ? Palette.Good : price <= _bandMid ? Palette.Text : price <= _bandHigh ? Palette.Warn : Palette.Bad;
        float xp = X(price);
        p.DrawLine(new Vector2(xp, -6), new Vector2(xp, BarH + 6), color, 3);
        p.DrawColoredPolygon(new[] { new Vector2(xp - 7, -14), new Vector2(xp + 7, -14), new Vector2(xp, -5) }, color);
    }

    void ShowMissedLot(bool rivalTookIt)
    {
        var chest = State.Chest;
        int total = chest.Value + (chest.FalseBottom?.Value ?? 0);
        CloseLot();
        var panel = Add(UiKit.MakePanel(Palette.Panel, Palette.PanelBorder, 3).Place(340, 190, 600, 340));
        panel.PivotOffset = new Vector2(300, 170);
        panel.Scale = Vector2.One * 0.85f;
        panel.Modulate = Colors.Transparent;
        var tween = panel.CreateTween().SetParallel();
        tween.TweenProperty(panel, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel, "modulate", Colors.White, 0.2f);

        string title = rivalTookIt ? $"{State.Rival.Def.Name} wins Lot {State.LotNumber} for {_bidPrice}g" : "No takers. The lot is withdrawn.";
        panel.AddChild(UiKit.MakeLabel(title, 22, Palette.Text, HorizontalAlignment.Center).Place(20, 20, 560, 32));
        panel.AddChild(UiKit.MakeLabel("Word gets around about what was inside:", 14, Palette.Muted, HorizontalAlignment.Center).Place(20, 60, 560, 20));

        var best = chest.Items.OrderByDescending(i => i.Value).Take(3)
            .Select(i => $"{i.Def.Name} ({i.Def.Rarity}, {i.Value}g)");
        panel.AddChild(UiKit.MakeLabel(string.Join("\n", best), 16, Palette.Text, HorizontalAlignment.Center).Place(20, 88, 560, 76));
        panel.AddChild(UiKit.MakeLabel($"Total: {total}g", 26, Palette.Gold, HorizontalAlignment.Center).Place(20, 170, 560, 36));

        string verdict;
        Color verdictColor;
        if (!rivalTookIt) (verdict, verdictColor) = ("Nobody wanted it. Should you have?", Palette.Muted);
        else if (total < _bidPrice) (verdict, verdictColor) = ("They overpaid. You dodged a bullet.", Palette.Good);
        else if (total > _bidPrice * 1.5f) (verdict, verdictColor) = ("That one got away...", Palette.Bad);
        else (verdict, verdictColor) = ("A fair price, more or less.", Palette.Text);
        panel.AddChild(UiKit.MakeLabel(verdict, 18, verdictColor, HorizontalAlignment.Center).Place(20, 214, 560, 26));
        panel.AddChild(UiKit.MakeButton("Next lot", Palette.Gold, 20, NextLot).Place(200, 266, 200, 50));
        Game.Moment("08_missed", 0.5);
    }

    public void NextLot()
    {
        Sfx.Play("bid");
        Game.SwitchTo(new AuctionPhase());
    }

    void ShowBankrupt()
    {
        Over = true;
        Refresh();
        CloseLot();
        var panel = Add(UiKit.MakePanel(Palette.Panel, Palette.Bad, 3).Place(390, 230, 500, 240));
        panel.AddChild(UiKit.MakeLabel("Out of gold", 30, Palette.Bad, HorizontalAlignment.Center).Place(20, 30, 460, 40));
        panel.AddChild(UiKit.MakeLabel("The guild lends you a fresh 300g. Try not to tell anyone.", 16, Palette.Text, HorizontalAlignment.Center)
            .Wrapped().Place(30, 80, 440, 50));
        panel.AddChild(UiKit.MakeButton("Start over", Palette.Gold, 20, () =>
        {
            State.Reset();
            Game.Hud.SetGold(State.Gold);
            Game.SwitchTo(new AuctionPhase());
        }).Place(150, 160, 200, 50));
    }
}
