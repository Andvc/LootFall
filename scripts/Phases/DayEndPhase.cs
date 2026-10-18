using System;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// After the last customer: how the day went, then what to spend it on. Until the stall has earned its keep there's
/// nothing to buy, just how far it has to go. Then renovations bring new customers, new things and new sounds to
/// learn; tools tell you more about each chest. From the village shop, cabinets show off what you keep, and the
/// exhibition along the bottom draws more and better customers (click something in it to sell it). From the town
/// shop, the Synergies button opens the journal, where you choose which of the shop's synergies are active. Bram
/// introduces each as it opens up.
/// </summary>
public partial class DayEndPhase : Phase
{
    const float CardW = 380, CardH = 168, CardGap = 16, CardsX = 50, CardsY = 222;

    Node2D _cards;
    Label _goldLabel, _header;
    MentorView _mentor;
    Button _synergies;
    LedgerView _ledger;
    int _topUp;

    public MentorView Mentor => _mentor;

    public override void _Ready()
    {
        bool shopWasOpen = State.UpgradesOpen;
        _topUp = State.EndDay();
        Add(UiKit.MakeShop(State.Tier, 0.38f));
        Game.Hud.SetDay(State.Day, State.Tier);
        Game.Hud.SetGold(State.Gold);

        var today = State.Today;
        Add(UiKit.MakeLabel($"END OF DAY {State.Day}", 32, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 70, 1280, 40));
        BuildSummary(today);
        if (_topUp > 0)
            Add(UiKit.MakeLabel($"Uncle Bram slipped you {_topUp}g to keep the shop going.", 16, Palette.Warn, HorizontalAlignment.Center, outline: 4)
                .Place(0, 166, 1280, 20));

        _header = Add(Header("", CardsX, 192));
        _goldLabel = Add(UiKit.MakeLabel("", 16, Palette.Gold, HorizontalAlignment.Right).Place(830, 192, 400, 20));
        _cards = Add(new Node2D());
        BuildCards();

        Add(UiKit.MakeButton($"Open the shop: Day {State.Day + 1}", Palette.Gold, 24, NextDay).Place(900, 626, 340, 64));
        _synergies = Add(UiKit.MakeButton("", Palette.Magic.Darkened(0.2f), 16, OpenSynergies).Place(900, 584, 340, 36));
        RefreshSynergyButton();
        _mentor = Add(new MentorView { Position = new Vector2(40, 440), ZIndex = 20 });
        Teach(State.UpgradesOpen ? null : "dayend");
        if (!shopWasOpen && State.UpgradesOpen) Celebrate("The shop's open for business!");
        if (State.UpgradesOpen) Teach("upgrades");
        Unlocked();
        if (State.Display.Count > 0) Teach("exhibition");
        Sfx.Play(today.Profit >= 0 ? "win" : "lose", 1f, -6);
        Game.Moment("09_dayend", 0.5);
    }

    /// <summary>The day in figures, side by side: what you bought and passed on, spent and took in, and the profit.</summary>
    void BuildSummary(DayStats today)
    {
        var figures = new System.Collections.Generic.List<(string Label, string Value, Color Color)>
        {
            ("Bought", today.Bought.ToString(), Palette.Text),
            ("Passed", today.Passed.ToString(), Palette.Text),
            ("Spent", $"{today.Spent}g", Palette.Text),
            ("The chests held", $"{today.Earned}g", Palette.Gold),
            ("Profit", $"{(today.Profit > 0 ? "+" : "")}{today.Profit}g", today.Profit > 0 ? Palette.Good : today.Profit < 0 ? Palette.Bad : Palette.Text),
        };
        if (today.PerfectReads > 0) figures.Add(("Perfect reads", today.PerfectReads.ToString(), Palette.Gold));
        if (today.NewFinds > 0) figures.Add(("New finds", today.NewFinds.ToString(), Palette.Magic));
        const float w = 150, gap = 10;
        float x0 = 640 - (figures.Count * w + (figures.Count - 1) * gap) / 2;
        for (int i = 0; i < figures.Count; i++)
        {
            var (label, value, color) = figures[i];
            var chip = Add(UiKit.MakePanel(label == "Profit" ? color.Darkened(0.2f) : null).Place(x0 + i * (w + gap), 110, w, 52));
            chip.AddChild(UiKit.MakeLabel(label, 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 4, w, 20));
            chip.AddChild(UiKit.MakeLabel(value, 24, color, HorizontalAlignment.Center, outline: 4).Place(0, 20, w, 30));
        }
    }

    void Teach(string key)
    {
        if (key != null && State.FirstTime(key)) _mentor.Say(Hints.Get(key));
    }

    /// <summary>What the shop's grown into brings, said once: a shelf to display things on, synergies, the warrant.</summary>
    void Unlocked()
    {
        if (State.DisplayOpen) Teach("display");
        if (State.SynergiesOpen) Teach("synergy");
        if (State.Tier >= ShopTiers.Max) Teach("warrant");
    }

    /// <summary>A banner across the top for something that just opened up.</summary>
    void Celebrate(string text)
    {
        Sfx.Play("fanfare", 1f, -6);
        FloatText(text, new Vector2(640, 168), Palette.Gold, 32);
    }

    /// <summary>
    /// Renovation first, then tools by price. Things you can't afford yet still show, so you can plan. Before the
    /// stall has earned its keep: how far it has to go.
    /// </summary>
    void BuildCards()
    {
        foreach (var child in _cards.GetChildren()) child.QueueFree();
        _goldLabel.Text = $"You have {State.Gold}g";
        _header.Text = State.UpgradesOpen ? "SPEND IT ON THE SHOP" : "FIX UP THE STALL";
        if (!State.UpgradesOpen)
        {
            _cards.AddChild(MakeGoal());
            return;
        }
        var offers = Upgrades.Available(State).OrderByDescending(u => u.IsRenovation).ThenBy(u => u.Cost).Take(6).ToList();
        if (offers.Count == 0)
            _cards.AddChild(UiKit.MakeLabel("Nothing left to buy. The shop is complete.", 24, Palette.Muted, HorizontalAlignment.Center)
                .Place(0, CardsY + 80, 1280, 30));
        for (int i = 0; i < offers.Count; i++)
        {
            float x = CardsX + i % 3 * (CardW + CardGap), y = CardsY + i / 3 * (CardH + CardGap);
            _cards.AddChild(MakeCard(offers[i], x, y));
        }

        BuildExhibition();
    }

    /// <summary>What's on display, what it does for the shop, and a click to sell any of it.</summary>
    void BuildExhibition()
    {
        if (!State.DisplayOpen) return;
        const float x0 = 40, y0 = 584;
        int score = State.Exhibition;
        string effect = score == 0 ? "Keep grand things when you open a chest, and they'll show here."
            : $"{score} points: +{State.ExhibitionCustomers} customer{(State.ExhibitionCustomers == 1 ? "" : "s")} a day, " +
              $"better chests {Synergies.Pct(State.BetterChests)} of the time.";
        _cards.AddChild(UiKit.MakeLabel($"EXHIBITION {State.Display.Count}/{State.DisplaySlots}", 16, Palette.Magic).Place(x0, y0, 300, 20));
        _cards.AddChild(UiKit.MakeLabel(effect, 16, Palette.Muted).Place(x0 + 210, y0, 640, 20));
        for (int i = 0; i < State.DisplaySlots; i++)
        {
            bool filled = i < State.Display.Count;
            var slot = UiKit.MakePanel(filled ? Palette.Of(State.Display[i].Rarity) : Palette.PanelBorder).Place(x0 + i * 56, y0 + 26, 50, 50);
            _cards.AddChild(slot);
            if (!filled) continue;
            var item = State.Display[i];
            slot.AddChild(UiKit.MakeItemIcon(item, new Vector2(25, 25), 44));
            var sell = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, TooltipText = $"{item.Name}, {item.Prestige} points{(item.Magic ? " (double: enchanted)" : "")}. Click to sell it for {item.Value}g." };
            sell.Pressed += () => SellFromDisplay(item);
            slot.AddChild(sell.Place(0, 0, 50, 50));
        }
        string found = $"Collection: {State.Found.Count} of {Collection.Size} found.";
        string warrant = State.Has("warrant") || State.Tier < 4 ? ""
            : score >= GameState.WarrantExhibition ? " The Crown is impressed by your exhibition."
            : $" The Crown wants an exhibition of {GameState.WarrantExhibition} before the warrant.";
        _cards.AddChild(UiKit.MakeLabel(found + warrant, 16, score >= GameState.WarrantExhibition ? Palette.Good : Palette.Muted)
            .Place(x0, y0 + 84, 840, 20));
    }

    public void SellFromDisplay(Item item)
    {
        State.SellFromDisplay(item);
        Game.Hud.SetGold(State.Gold);
        Sfx.PlayGroup("pay");
        FloatText($"+{item.Value}g", new Vector2(640, 560), Palette.Gold, 24);
        BuildCards();
    }

    /// <summary>Before renovations: what the stall has earned so far, against what it needs, and what's coming.</summary>
    Control MakeGoal()
    {
        const float w = 3 * CardW + 2 * CardGap, h = CardH;
        var card = UiKit.MakePanel(Palette.Gold.Darkened(0.2f)).Place(CardsX, CardsY, w, h);
        card.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/ui/icons/shop.png"), MouseFilter = Control.MouseFilterEnum.Ignore }
            .Place(20, 20, 48, 48));
        card.AddChild(UiKit.MakeLabel("Earn your keep", 24, Palette.Gold).Place(84, 16, 600, 30));
        card.AddChild(UiKit.MakeLabel($"Once the stall has earned {GameState.UpgradesAt}g, all told, Uncle Bram will help you fix it up: " +
                                      "a Village Shop brings more customers, richer chests and a shelf for your best finds.", 16, Palette.Muted)
            .Wrapped().Place(84, 50, w - 110, 44));
        int earned = Math.Clamp(State.TotalEarned, 0, GameState.UpgradesAt);
        float fill = earned / (float)GameState.UpgradesAt;
        card.AddChild(new Painter(p =>
        {
            p.DrawRect(new Rect2(0, 0, w - 104, 24), new Color("#120e1a"));
            p.DrawRect(new Rect2(0, 0, (w - 104) * fill, 24), Palette.Gold.Darkened(0.25f));
            p.DrawRect(new Rect2(0, 0, (w - 104) * fill, 5), Palette.Gold.Lightened(0.2f));
            p.DrawRect(new Rect2(0, 0, w - 104, 24), Palette.PanelBorder, false, 3);
        }) { Position = new Vector2(84, 112) });
        card.AddChild(UiKit.MakeLabel($"The chests have brought in {State.TotalEarned}g of {GameState.UpgradesAt}g", 16, Palette.Text, HorizontalAlignment.Center,
            outline: 4).Place(84, 114, w - 104, 20));
        return card;
    }

    Control MakeCard(Upgrade upgrade, float x, float y)
    {
        bool affordable = State.Gold >= upgrade.Cost;
        var card = UiKit.MakePanel(upgrade.IsRenovation ? Palette.Gold : Palette.PanelBorder).Place(x, y, CardW, CardH);
        card.AddChild(new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://assets/ui/icons/{upgrade.Icon}.png"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        }.Place(16, 16, 48, 48));
        card.AddChild(UiKit.MakeLabel(upgrade.Name, 24, upgrade.IsRenovation ? Palette.Gold : Palette.Text).Place(78, 12, 220, 30));
        card.AddChild(UiKit.MakeLabel($"{upgrade.Cost}g", 24, affordable ? Palette.Gold : Palette.Muted, HorizontalAlignment.Right)
            .Place(240, 12, 124, 30));
        card.AddChild(UiKit.MakeLabel(upgrade.Blurb, 16, Palette.Muted).Wrapped().Place(78, 44, 288, 80));
        if (affordable)
            card.AddChild(UiKit.MakeButton("Buy", Palette.Good, 16, () => Buy(upgrade)).Place(CardW - 128, CardH - 44, 112, 34));
        else
            card.AddChild(UiKit.MakeLabel($"{upgrade.Cost - State.Gold}g more", 16, Palette.Muted, HorizontalAlignment.Right)
                .Place(CardW - 200, CardH - 34, 184, 20));
        return card;
    }

    public void Buy(Upgrade upgrade)
    {
        if (!State.CanBuy(upgrade)) return;
        State.BuyUpgrade(upgrade);
        Game.Hud.SetGold(State.Gold);
        Game.Hud.SetDay(State.Day, State.Tier);
        Sfx.PlayGroup("pay");
        if (upgrade.IsRenovation)
        {
            Sfx.Play("fanfare", 1f, -4);
            Game.Flash(Palette.Gold, 0.35f);
            Game.Shake(0.3f);
            FloatText($"{ShopTiers.Name(State.Tier)}!", new Vector2(640, 150), Palette.Gold, 40);
            Unlocked();
        }
        else if (upgrade.SynergySlots > 0) Teach("slots");
        else
        {
            Sfx.Play("sting", 1.2f, -4);
            FloatText($"-{upgrade.Cost}g", new Vector2(640, 150), Palette.Gold, 32);
        }
        BuildCards();
        if (State.Won) ShowWin();
    }

    /// <summary>All the upgrades this player would buy in order, while the money lasts (the bot uses this).</summary>
    public void BuyInOrder(string[] order, int reserve)
    {
        foreach (var key in order)
        {
            var upgrade = Upgrades.Get(key);
            if ((State.Gold - upgrade.Cost >= reserve || key == "warrant") && State.CanBuy(upgrade)) Buy(upgrade);
        }
    }

    void ShowWin()
    {
        var panel = Add(UiKit.MakePanel(Palette.Gold).Place(290, 200, 700, 300));
        panel.ZIndex = 10;
        panel.AddChild(UiKit.MakeLabel("APPRAISER TO THE CROWN", 32, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(0, 30, 700, 40));
        panel.AddChild(UiKit.MakeLabel($"From a roadside stall to the finest shop in the kingdom in {State.Day} days, " +
                                       $"and {State.Met.Count} regulars who'll never shake a chest as well as you.",
            16, Palette.Text, HorizontalAlignment.Center).Wrapped().Place(40, 96, 620, 60));
        panel.AddChild(UiKit.MakeLabel("Keep trading as long as you like.", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 170, 700, 20));
        panel.AddChild(UiKit.MakeButton("Carry on", Palette.Gold, 24, () => panel.QueueFree()).Place(230, 214, 240, 56));
        Sfx.Play("fanfare");
        Game.Moment("99_win", 0.5);
    }

    /// <summary>The button says how many synergies are active, and stands out when a reached one is waiting for a slot.</summary>
    void RefreshSynergyButton()
    {
        int reached = Synergies.All.Count(x => x.Level(State) > 0), active = State.ActiveSynergies.Count;
        _synergies.Text = $"Synergies: {active} of {State.SynergySlots} active";
        _synergies.Visible = State.SynergiesOpen && reached > 0;
        bool waiting = active < State.SynergySlots && reached > active;
        _synergies.Modulate = waiting ? new Color(1.3f, 1.3f, 1.3f) : Colors.White;
        if (waiting) _synergies.Text += " (choose!)";
    }

    /// <summary>Opens the journal on the synergies page, where they can be switched on and off.</summary>
    public void OpenSynergies()
    {
        if (_ledger != null) return;
        _ledger = Add(new LedgerView(State, page: 3, choosing: true) { ZIndex = 30 });
        _ledger.Closed += () =>
        {
            _ledger = null;
            RefreshSynergyButton();
            BuildCards();
        };
        Game.Moment("09_synergies", 0.4);
    }

    public void CloseSynergies() => _ledger?.Close();

    /// <summary>Keeps the highest synergies active, the way the balance simulation's careful player does (the bot uses this).</summary>
    public void ChooseSynergies()
    {
        Debug.Simulation.ChooseSynergies(State);
        _ledger?.ShowPage(3);
        RefreshSynergyButton();
    }

    public void NextDay()
    {
        Sfx.Play("bid");
        Game.StartDay();
    }
}
