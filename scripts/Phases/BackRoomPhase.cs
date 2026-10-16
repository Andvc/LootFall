using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// After closing: the day's chests wait on the table. Open them one by one (lockpick, then the payoff),
/// then count up the day and pay rent when it's due.
/// </summary>
public partial class BackRoomPhase : Phase
{
    Button _next;

    public bool HasUnopened => State.Purchases.Any(p => !p.Opened);
    public bool SummaryShown { get; private set; }
    public bool Bankrupt { get; private set; }

    public override void _Ready()
    {
        Add(UiKit.MakeBackdrop("cellar_night", 0.45f));
        Game.Hud.SetClock(State.Day, ShopClock.Close);
        Add(UiKit.MakeLabel("THE BACK ROOM", 32, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 72, 1280, 40));
        Add(UiKit.MakeLabel($"Day {State.Day}, after closing", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(0, 112, 1280, 20));

        var purchases = State.Purchases;
        if (purchases.Count == 0)
        {
            Add(UiKit.MakeLabel("Nothing bought today.", 24, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(0, 260, 1280, 32));
        }
        float spacing = 250, x0 = 640 - (purchases.Count - 1) * spacing / 2;
        for (int i = 0; i < purchases.Count; i++)
        {
            var p = purchases[i];
            float x = x0 + i * spacing;
            var chest = Add(new ChestView { Position = new Vector2(x, 290), PixelSize = 8, Skin = p.Chest.Provenance.Skin });
            if (p.Opened) chest.Modulate = new Color(0.45f, 0.45f, 0.5f);
            Add(UiKit.MakeLabel(p.Visit.Seller.Name, 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(x - 120, 350, 240, 20));
            Add(UiKit.MakeLabel($"Paid {p.Paid}g", 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(x - 120, 372, 240, 20));
            if (p.Opened)
            {
                int profit = p.Haul - p.Paid;
                Add(UiKit.MakeLabel($"{(profit >= 0 ? "+" : "")}{profit}g", 24, profit >= 0 ? Palette.Good : Palette.Bad,
                    HorizontalAlignment.Center, outline: 4).Place(x - 120, 196, 240, 30));
            }
        }

        if (HasUnopened)
        {
            _next = Add(UiKit.MakeButton("Open the next chest", Palette.Gold, 24, OpenNext).Place(440, 452, 400, 64));
            if (purchases.Any(p => p.Visit.Lesson != null))
                Add(new MentorView { Position = new Vector2(412, 548) }).Say(Lessons.BackRoom);
            Game.Moment("06_backroom", 0.4);
        }
        else
        {
            ShowSummary();
        }
    }

    public void OpenNext()
    {
        var purchase = State.Purchases.FirstOrDefault(p => !p.Opened);
        if (purchase == null) return;
        if (_next != null) _next.Disabled = true;
        State.Opening = purchase;
        State.Grade = PickGrade.Clean;
        Sfx.PlayGroup("creak", 0.9f, -4);
        Game.SwitchTo(new LockpickPhase());
    }

    void ShowSummary()
    {
        SummaryShown = true;
        int spent = State.Purchases.Sum(p => p.Paid);
        int haul = State.Purchases.Sum(p => p.Haul);
        int profit = haul - spent;
        bool rentDay = State.Day >= State.RentDueDay;
        int rentAmount = State.RentDue;
        bool paid = State.PayRentIfDue(out _);
        Bankrupt = !paid;
        Game.Hud.SetGold(State.Gold);

        var panel = Add(UiKit.MakePanel(Bankrupt ? Palette.Bad : Palette.PanelBorder).Place(340, 410, 600, 290));
        void Row(float y, string label, string value, Color color)
        {
            panel.AddChild(UiKit.MakeLabel(label, 16, Palette.Muted).Place(40, y + 4, 300, 24));
            panel.AddChild(UiKit.MakeLabel(value, 24, color, HorizontalAlignment.Right).Place(260, y, 300, 30));
        }

        Row(24, "Spent at the counter", $"-{spent}g", Palette.Text);
        Row(58, "Haul", $"+{haul}g", Palette.Gold);
        Row(92, "Profit", $"{(profit >= 0 ? "+" : "")}{profit}g", profit >= 0 ? Palette.Good : Palette.Bad);

        string rentLine;
        if (Bankrupt)
            rentLine = $"Rent of {rentAmount}g is due, and you have {State.Gold}g. The landlord changes the locks.";
        else if (rentDay)
            rentLine = $"Rent paid: {rentAmount}g. Next: {State.RentDue}g after Day {State.RentDueDay}.";
        else
            rentLine = $"Rent of {State.RentDue}g is due after Day {State.RentDueDay}.";
        panel.AddChild(UiKit.MakeLabel(rentLine, 16, Bankrupt ? Palette.Bad : Palette.Text, HorizontalAlignment.Center)
            .Wrapped().Place(30, 134, 540, 48));

        if (Bankrupt)
            panel.AddChild(UiKit.MakeButton("Start over", Palette.Bad, 24, StartOver).Place(170, 200, 260, 64));
        else
            panel.AddChild(UiKit.MakeButton($"Open the shop: Day {State.Day + 1}", Palette.Gold, 24, NextDay).Place(130, 200, 340, 64));

        Sfx.Play(Bankrupt ? "lose" : "win", 1f, -4);
        Game.Moment("09_summary", 0.4);
    }

    public void NextDay()
    {
        if (Bankrupt) return;
        Game.StartDay();
    }

    public void StartOver()
    {
        State.Reset();
        Game.Hud.SetGold(State.Gold);
        Game.StartDay();
    }
}
