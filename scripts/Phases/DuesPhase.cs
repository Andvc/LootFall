using System;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The Guild's night, at the end of each week: Mister Vane comes for the due. Pay it and he's gone until next week (and
/// says what next week's will be). Short, and you can pawn what's on display or sell back your curios at the Guild's
/// price; once a run, Bram can vouch for you and roll the rest into next week, with interest. Otherwise the Guild takes
/// the shop. On the sixth week Vane offers to take the map off your hands for half the due.
/// </summary>
public partial class DuesPhase : Phase
{
    const float PanelX = 500, PanelW = 740, PawnW = 176, PawnH = 64;

    MentorView _mentor;
    Node2D _track;
    SellerView _vane;
    Control _board;
    Label _have;
    bool _offerOpen, _paid;

    public MentorView Mentor => _mentor;

    /// <summary>The due has been settled (paid, or vouched for), or the shop is lost: there's only a button to go on.</summary>
    public bool Settled => _paid || State.Foreclosed;

    public bool OfferOpen => _offerOpen;

    public override void _Ready()
    {
        Add(UiKit.MakeShop(State.Tier, 0.3f));
        Add(new Painter(p => p.DrawRect(new Rect2(0, 0, 1280, 720), new Color(0.05f, 0.05f, 0.1f, 0.35f))));
        Add(UiKit.MakeLabel($"THE GUILD'S DUE: WEEK {State.Week}", 32, Palette.Text, HorizontalAlignment.Center, outline: 6).Place(0, 62, 1280, 40));
        _vane = Add(new SellerView(new SellerDef { Key = Story.Vane, Name = Guild.Collector }) { Position = new Vector2(220, 300) });
        _vane.Enter();
        Sfx.PlayGroup("door_open", 0.8f, -4);
        _vane.Say(Guild.Arrival(State.Week));
        Game.Hud.SetGold(State.Gold, animate: false);
        _mentor = Add(new MentorView { Position = new Vector2(24, 588), ZIndex = 20 });

        _board = Add(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, 1280, 720));
        _track = Add(new Node2D());
        BuildTrack();
        if (State.OfferTonight) ShowOffer();
        else Build();
        Game.Moment("21_dues", 0.8);
    }

    /// <summary>
    /// Under the title, the whole debt week by week: what's been paid, tonight's, and what's still to come, growing;
    /// and the map's four pieces.
    /// </summary>
    void BuildTrack()
    {
        foreach (var child in _track.GetChildren()) child.QueueFree();
        const float y = 108, w = 112, gap = 8;
        int first = State.Week > Guild.Weeks ? State.Week - 7 : 1;
        for (int i = 0; i < 8; i++)
        {
            int week = first + i;
            bool paid = week < State.Week || (week == State.Week && State.PaidThisWeek);
            bool tonight = week == State.Week && !paid;
            var color = paid ? Palette.Good : tonight ? Palette.Warn : Palette.PanelBorder;
            var box = UiKit.MakePanel(color).Place(40 + i * (w + gap), y, w, 56);
            box.AddChild(UiKit.MakeLabel($"Week {week}", 16, paid ? Palette.Good : tonight ? Palette.Warn : Palette.Muted, HorizontalAlignment.Center)
                .Place(0, 6, w, 20));
            box.AddChild(UiKit.MakeLabel(paid ? "paid" : $"{State.DueFor(week)}g", 16, paid ? Palette.Good : tonight ? Palette.Gold : Palette.Text,
                HorizontalAlignment.Center, outline: 4).Place(0, 28, w, 20));
            _track.AddChild(box);
        }
        // The map: Bram's piece and the Hooded Woman's three.
        if (State.Week < 2) return;
        float mx = 40 + 8 * (w + gap) + 12;
        _track.AddChild(UiKit.MakeLabel(State.MapGiven ? "MAP: THE GUILD'S" : $"MAP {State.MapPieces}/{Story.Pieces}", 16, Palette.Magic)
            .Place(mx, y - 2, 220, 20));
        for (int i = 0; i < Story.Pieces; i++)
        {
            var piece = UiKit.MakeSprite("res://assets/sprites/items/icons/treasure_map.png", new Vector2(mx + 16 + i * 34, y + 36), 1);
            if (i >= State.MapPieces) piece.Modulate = new Color(0.25f, 0.22f, 0.3f);
            _track.AddChild(piece);
        }
    }

    void Clear()
    {
        foreach (var child in _board.GetChildren()) child.QueueFree();
    }

    T Put<T>(T node) where T : Node
    {
        _board.AddChild(node);
        return node;
    }

    /// <summary>The Guild's offer for the map: half tonight's due, and no vault.</summary>
    void ShowOffer()
    {
        _offerOpen = true;
        var lines = Story.Offer(State.MapPieces);
        _vane.Say(lines[0].Text + " " + lines[1].Text, Palette.Warn);
        var panel = Put(UiKit.MakePanel(Palette.Warn.Darkened(0.2f)).Place(PanelX, 180, PanelW, 260));
        panel.AddChild(UiKit.MakeLabel("THE GUILD'S OFFER", 24, Palette.Warn, HorizontalAlignment.Center, outline: 4).Place(0, 16, PanelW, 30));
        panel.AddChild(UiKit.MakeLabel($"Hand over your {State.MapPieces} map piece{(State.MapPieces == 1 ? "" : "s")} and tonight's due is " +
                                       $"halved, from {State.DueNow}g to {Money.Round(State.DueNow / 2f)}g. Without the map there's no vault.",
            16, Palette.Text, HorizontalAlignment.Center).Wrapped().Place(40, 60, PanelW - 80, 60));
        panel.AddChild(UiKit.MakeLabel($"You have {State.Gold}g.", 16, State.Gold >= State.DueNow ? Palette.Good : Palette.Bad,
            HorizontalAlignment.Center).Place(0, 130, PanelW, 20));
        panel.AddChild(UiKit.MakeButton("Give him the map", Palette.Warn, 16, () => AnswerOffer(true)).Place(80, 180, 280, 52));
        panel.AddChild(UiKit.MakeButton("Keep the map", Palette.Gold, 16, () => AnswerOffer(false)).Place(PanelW - 360, 180, 280, 52));
        Game.Moment("21_offer", 0.6);
    }

    public void AnswerOffer(bool give)
    {
        if (!_offerOpen) return;
        _offerOpen = false;
        if (give)
        {
            State.GiveMap();
            Sfx.Play("deflate", 1f, -4);
            BuildTrack();
        }
        _vane.Say(give ? Story.OfferTaken : Story.OfferRefused, give ? Palette.Text : Palette.Warn);
        Clear();
        Build();
    }

    /// <summary>What's due, what you have, and the ways to make up the difference.</summary>
    void Build()
    {
        Clear();
        int due = State.DueNow;
        bool enough = State.Gold >= due;
        var panel = Put(UiKit.MakePanel(enough ? Palette.Gold.Darkened(0.2f) : Palette.Bad.Darkened(0.2f)).Place(PanelX, 180, PanelW, 200));
        panel.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/ui/guild.png"), MouseFilter = Control.MouseFilterEnum.Ignore }
            .Place(28, 24, 44, 48));
        panel.AddChild(UiKit.MakeLabel("DUE TONIGHT", 16, Palette.Muted).Place(96, 20, 300, 20));
        panel.AddChild(UiKit.MakeLabel($"{due}g", 48, enough ? Palette.Gold : Palette.Bad, outline: 6).Place(96, 38, 400, 56));
        if (State.Arrears > 0)
            panel.AddChild(UiKit.MakeLabel($"including {State.Arrears}g left over from last week", 16, Palette.Warn).Place(96, 96, 500, 20));
        _have = UiKit.MakeLabel($"You have {State.Gold}g", 24, enough ? Palette.Good : Palette.Bad, HorizontalAlignment.Right, outline: 4)
            .Place(PanelW - 360, 50, 330, 30);
        panel.AddChild(_have);
        float fill = Math.Clamp(State.Gold / (float)Math.Max(1, due), 0, 1);
        var bar = new Painter(p =>
        {
            p.DrawRect(new Rect2(0, 0, PanelW - 60, 22), new Color("#120e1a"));
            p.DrawRect(new Rect2(0, 0, (PanelW - 60) * fill, 22), (enough ? Palette.Gold : Palette.Bad).Darkened(0.25f));
            p.DrawRect(new Rect2(0, 0, PanelW - 60, 22), Palette.PanelBorder, false, 3);
        }) { Position = new Vector2(30, 126) };
        panel.AddChild(bar);
        panel.AddChild(UiKit.MakeLabel(WeekLine(), 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 160, PanelW, 20));

        if (enough)
        {
            Put(UiKit.MakeButton($"Pay {due}g", Palette.Gold, 24, Pay).Place(PanelX + PanelW / 2 - 160, 404, 320, 64));
            return;
        }
        Teach("pawn");
        BuildPawn(due);
    }

    string WeekLine() => State.Week < Guild.Weeks
        ? $"Week {State.Week} of {Guild.Weeks}. Bram's debt still to pay after tonight: {RemainingDebt()}g."
        : State.Week == Guild.Weeks ? "The last week of Bram's debt." : $"Week {State.Week}: the Guild's interest, for as long as you last.";

    int RemainingDebt()
    {
        int left = 0;
        for (int w = State.Week + 1; w <= Guild.Weeks; w++) left += State.DueFor(w);
        return left;
    }

    /// <summary>Short: what's on display and your curios, at the Guild's price; Bram's word; or the end.</summary>
    void BuildPawn(int due)
    {
        Put(UiKit.MakeLabel($"SHORT BY {due - State.Gold}g", 24, Palette.Bad, outline: 4).Place(PanelX, 392, 400, 30));
        Put(UiKit.MakeLabel("Pawn what's on display at the Guild's price, or sell a curio back:", 16, Palette.Muted).Place(PanelX, 426, PanelW, 20));
        // The plainest things first: what you'd miss least. Two rows at most.
        var things = State.Display.OrderBy(i => i.Prestige).Select(i => (object)i).Concat(State.CurioKeys.Select(k => (object)k)).Take(8).ToList();
        for (int i = 0; i < things.Count; i++)
        {
            float x = PanelX + i % 4 * (PawnW + 12), y = 452 + i / 4 * (PawnH + 8);
            Button button;
            if (things[i] is Item item)
            {
                button = Put(UiKit.MakeButton($"+{State.PawnValue(item)}g", Palette.Of(item.Rarity).Darkened(0.3f), 16, () => Pawn(item))
                    .Place(x, y, PawnW, PawnH));
                button.TooltipText = $"Pawn the {item.Name} ({item.Prestige} exhibition points) for {State.PawnValue(item)}g";
                button.AddChild(UiKit.MakeItemIcon(item, new Vector2(36, PawnH / 2), 48));
            }
            else
            {
                var curio = Curios.Get((string)things[i]);
                button = Put(UiKit.MakeButton($"+{State.CurioValue(curio.Key)}g", Palette.Magic.Darkened(0.4f), 16, () => SellCurio(curio.Key))
                    .Place(x, y, PawnW, PawnH));
                button.TooltipText = $"Sell the {curio.Name} back for {State.CurioValue(curio.Key)}g";
                button.AddChild(UiKit.MakeCurioIcon(curio, new Vector2(36, PawnH / 2)));
            }
            button.Alignment = HorizontalAlignment.Right;
        }
        if (things.Count == 0)
            Put(UiKit.MakeLabel("Nothing to pawn.", 16, Palette.Muted).Place(PanelX, 460, 300, 20));

        float by = 452 + Math.Max(1, (things.Count + 3) / 4) * (PawnH + 8) + 4;
        if (State.CanAskGrace)
        {
            var grace = Put(UiKit.MakeButton("Bram vouches for you", Palette.Warn, 16, AskGrace).Place(PanelX, by, 370, 52));
            grace.TooltipText = $"Everything you have goes to the Guild; the rest, and half as much again, is added to next week's due. Once a run.";
            Put(UiKit.MakeLabel($"(once a run: you pay {State.Gold}g now, {(int)MathF.Ceiling((due - State.Gold) * GameState.GraceInterest)}g more next week)",
                16, Palette.Muted).Place(PanelX, by + 56, PanelW, 20));
        }
        if (!State.CanCover)
            Put(UiKit.MakeButton("Let the Guild take the shop", Palette.Bad, 16, Foreclose).Place(PanelX + PanelW - 370, by, 370, 52));
    }

    public void Pawn(Item item)
    {
        int value = State.Pawn(item);
        if (value <= 0) return;
        Sfx.PlayGroup("pay");
        Game.Hud.SetGold(State.Gold);
        FloatText($"+{value}g", new Vector2(PanelX + PanelW / 2, 370), Palette.Gold, 24);
        Build();
    }

    public void SellCurio(string key)
    {
        int value = State.SellCurio(key);
        if (value <= 0) return;
        Sfx.PlayGroup("pay");
        Game.Hud.SetGold(State.Gold);
        FloatText($"+{value}g", new Vector2(PanelX + PanelW / 2, 370), Palette.Gold, 24);
        Build();
    }

    public void Pay()
    {
        if (_paid || !State.PayDues()) return;
        _paid = true;
        Sfx.PlayGroup("pay");
        Sfx.Play("coin", 0.8f, -4);
        Game.Hud.SetGold(State.Gold);
        Game.Hud.SetDue(State);
        BuildTrack();
        Game.Shake(0.15f);
        _vane.Say(Guild.Paid(State.Week), Palette.Good);
        _vane.Bob();
        Teach("dues");
        AfterSettled(null);
    }

    public void AskGrace()
    {
        if (_paid || !State.CanAskGrace) return;
        int owed = State.AskGrace();
        _paid = true;
        Sfx.Play("lose", 0.9f, -4);
        Game.Hud.SetGold(State.Gold);
        Game.Hud.SetDue(State);
        BuildTrack();
        _vane.Say("Bram's word. How touching. One week, then: the rest, with interest.", Palette.Warn);
        AfterSettled($"{owed}g goes on next week's due.");
    }

    /// <summary>Paid (or vouched for): what's due next week, and on.</summary>
    void AfterSettled(string note)
    {
        Clear();
        var panel = Put(UiKit.MakePanel(Palette.Good.Darkened(0.3f)).Place(PanelX, 180, PanelW, 200));
        panel.AddChild(UiKit.MakeLabel(State.DebtCleared && State.WeeksPaid == Guild.Weeks ? "BRAM'S DEBT IS PAID!" : "PAID",
            40, Palette.Good, HorizontalAlignment.Center, outline: 6).Place(0, 24, PanelW, 50));
        int next = State.NextDue;
        string ahead = State.WeeksPaid < Guild.Weeks
            ? $"Next week the Guild wants {next}g: in {Guild.DaysPerWeek} days."
            : $"The Guild's interest next week: {next}g.";
        panel.AddChild(UiKit.MakeLabel(ahead, 24, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 92, PanelW, 30));
        if (note != null) panel.AddChild(UiKit.MakeLabel(note, 16, Palette.Warn, HorizontalAlignment.Center).Place(0, 140, PanelW, 20));
        Put(UiKit.MakeButton("Good night", Palette.Gold, 24, Next).Place(PanelX + PanelW / 2 - 160, 404, 320, 64));
        if (State.DebtCleared)
        {
            Sfx.Play("fanfare", 1f, -2);
            Fx.Confetti(this, new Vector2(PanelX + PanelW / 2, 240), 120);
        }
        Game.Moment("21_paid", 0.5);
    }

    public void Foreclose()
    {
        if (_paid || State.CanCover) return;
        State.Foreclose();
        Sfx.Play("boom", 0.8f, -2);
        Game.Shake(0.4f);
        _vane.Say(Guild.Foreclosed, Palette.Bad);
        Clear();
        Put(UiKit.MakeButton("...", Palette.Bad, 24, Next).Place(PanelX + PanelW / 2 - 160, 404, 320, 64));
    }

    /// <summary>Whatever comes after the Guild's visit: the next day, the end of the debt, or the end of the shop.</summary>
    public void Next()
    {
        if (!Settled) return;
        Sfx.Play("bid");
        Game.AfterDues();
    }

    void Teach(string key)
    {
        if (State.FirstTime(key)) _mentor.Say(Hints.Get(key));
    }
}
