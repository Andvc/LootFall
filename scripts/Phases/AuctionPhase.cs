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
    const float BarX = 676, BarY = 392, BarW = 552, BarH = 27;

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
        Add(UiKit.MakeBackdrop("courtyard_night", 0.85f));
        var c = State.Chest;
        // A pool of lamplight on the flagstones, in hard steps, so the chest reads against the backdrop.
        Add(new Painter(p =>
        {
            for (int i = 0; i < 4; i++)
            {
                p.DrawSetTransform(new Vector2(330, 408), 0, new Vector2(1, 0.32f));
                p.DrawCircle(Vector2.Zero, 250 - i * 50, new Color("#ffd27a") with { A = 0.07f });
            }
            p.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }) { Material = Fx.Additive() });
        _chest = Add(new ChestView { Position = new Vector2(330, 330), PixelSize = 14, Skin = c.Provenance.Skin });

        Add(UiKit.MakeLabel($"LOT {State.LotNumber}", 16, Palette.Muted, outline: 4).Place(72, 470, 520, 20));
        Add(UiKit.MakeLabel(c.Provenance.Name, 32, Palette.Text, outline: 4).Place(72, 490, 520, 40));
        Add(UiKit.MakeLabel(c.Provenance.Blurb, 16, Palette.Muted, outline: 4).Wrapped().Place(72, 532, 520, 44));
        Add(UiKit.MakeLabel("Inspect, bid, or let it go. Two inspections per lot.", 16, Palette.Muted, outline: 4)
            .Place(72, 652, 560, 20));
    }

    void BuildInspection()
    {
        Add(UiKit.MakePanel().Place(652, 66, 600, 640));
        Add(Header("INSPECT", 676, 82));
        _inspectionsLeft = Add(UiKit.MakeLabel("", 16, Palette.Gold, HorizontalAlignment.Right).Place(976, 82, 252, 20));

        var rows = new[] { (Inspection.Shake, "Shake it"), (Inspection.Weigh, "Weigh it"), (Inspection.Lock, "Examine lock") };
        for (int i = 0; i < rows.Length; i++)
        {
            var (kind, text) = rows[i];
            float y = 108 + i * 64;
            var button = Add(UiKit.MakeButton(text, Palette.Gold, 16, () => Inspect(kind)).Place(676, y, 168, 52));
            var result = Add(UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(856, y - 6, 376, 64));
            result.VerticalAlignment = VerticalAlignment.Center;
            _inspect[kind] = (button, result);
        }
    }

    void BuildAppraisal()
    {
        Add(Header("APPRAISAL", 676, 306));
        _estimateText = Add(UiKit.MakeLabel("", 24, Palette.Text).Place(676, 326, 556, 30));
        _estimateNote = Add(UiKit.MakeLabel("", 16, Palette.Muted).Place(676, 358, 556, 20));
        _bar = Add(new Painter(DrawBar) { Position = new Vector2(BarX, BarY) });
        Add(UiKit.MakeLabel("0g", 16, Palette.Muted).Place(BarX, BarY + BarH + 6, 100, 20));
        Add(UiKit.MakeLabel($"{State.Appraiser.ScaleMax}g+", 16, Palette.Muted, HorizontalAlignment.Right)
            .Place(BarX + BarW - 120, BarY + BarH + 6, 120, 20));
    }

    void BuildBidding()
    {
        var rival = State.Rival.Def;
        Add(Header("AUCTION", 676, 456));
        var rivalColor = new Color(rival.ColorHex);
        Add(new Painter(p =>
        {
            p.DrawRect(new Rect2(-24, -24, 48, 48), Colors.Black);
            p.DrawRect(new Rect2(-21, -21, 42, 42), rivalColor.Darkened(0.4f));
            p.DrawRect(new Rect2(-18, -18, 36, 36), rivalColor);
            p.DrawString(UiKit.Font, new Vector2(-12, 12), rival.Name[..1], HorizontalAlignment.Center, 24, 32, Palette.Bg);
        }) { Position = new Vector2(702, 504) });
        Add(UiKit.MakeLabel(rival.Name, 24, rivalColor).Place(740, 480, 480, 28));
        Add(UiKit.MakeLabel(rival.Tagline, 16, Palette.Muted).Place(740, 510, 496, 20));
        _quip = Add(UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(676, 536, 556, 40));

        Add(UiKit.MakeLabel("Current bid", 16, Palette.Muted).Place(676, 584, 200, 20));
        _priceLabel = Add(UiKit.MakeLabel("", 40, Palette.Gold, outline: 4).Place(676, 602, 190, 48));
        _leaderLabel = Add(UiKit.MakeLabel("", 16, Palette.Muted).Place(676, 654, 240, 20));
        _bid = Add(UiKit.MakeButton("", Palette.Good, 24, Bid).Place(884, 596, 196, 60));
        _pass = Add(UiKit.MakeButton("Pass", Palette.Bad, 24, Pass).Place(1092, 596, 136, 60));
        _bidHint = Add(UiKit.MakeLabel("", 16, Palette.Muted).Place(884, 664, 344, 20));
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
                _chest.RefreshLock();
                _chest.Wobble(0.2f);
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
        _estimateNote.Text = $"Best guess {est.Median}g. Right about 4 times in 5.";

        _priceLabel.Text = $"{_bidPrice}g";
        (_leaderLabel.Text, var leaderColor) = _leader switch
        {
            Leader.Player => ("You lead", Palette.Good),
            Leader.Rival => ($"{State.Rival.Def.Name} leads", Palette.Warn),
            _ => ("No bids yet", Palette.Muted),
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
        var panel = Add(UiKit.MakePanel(Palette.Gold).Place(320, 180, 640, 360));
        panel.PivotOffset = new Vector2(320, 180);
        panel.Scale = Vector2.One * 0.85f;
        panel.Modulate = Colors.Transparent;
        var tween = panel.CreateTween().SetParallel();
        tween.TweenProperty(panel, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel, "modulate", Colors.White, 0.2f);

        string title = rivalTookIt ? $"{State.Rival.Def.Name} wins Lot {State.LotNumber} for {_bidPrice}g" : "No takers. The lot is withdrawn.";
        panel.AddChild(UiKit.MakeLabel(title, 24, Palette.Text, HorizontalAlignment.Center).Place(20, 24, 600, 32));
        panel.AddChild(UiKit.MakeLabel("Word gets around about what was inside:", 16, Palette.Muted, HorizontalAlignment.Center).Place(20, 64, 600, 20));

        var best = chest.Items.OrderByDescending(i => i.Value).Take(3)
            .Select(i => $"{i.Def.Name} ({i.Def.Rarity}, {i.Value}g)");
        panel.AddChild(UiKit.MakeLabel(string.Join("\n", best), 16, Palette.Text, HorizontalAlignment.Center).Place(20, 94, 600, 76));
        panel.AddChild(UiKit.MakeLabel($"Total: {total}g", 32, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(20, 180, 600, 40));

        string verdict;
        Color verdictColor;
        if (!rivalTookIt) (verdict, verdictColor) = ("Nobody wanted it. Should you have?", Palette.Muted);
        else if (total < _bidPrice) (verdict, verdictColor) = ("They overpaid. You dodged a bullet.", Palette.Good);
        else if (total > _bidPrice * 1.5f) (verdict, verdictColor) = ("That one got away...", Palette.Bad);
        else (verdict, verdictColor) = ("A fair price, more or less.", Palette.Text);
        panel.AddChild(UiKit.MakeLabel(verdict, 16, verdictColor, HorizontalAlignment.Center).Place(20, 228, 600, 24));
        panel.AddChild(UiKit.MakeButton("Next lot", Palette.Gold, 24, NextLot).Place(210, 276, 220, 56));
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
        var panel = Add(UiKit.MakePanel(Palette.Bad).Place(390, 230, 500, 240));
        panel.AddChild(UiKit.MakeLabel("Out of gold", 32, Palette.Bad, HorizontalAlignment.Center).Place(20, 30, 460, 40));
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
