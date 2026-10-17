using System;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// After the last customer: how the day went, then what to spend it on. Renovations bring new customers, new things
/// and new sounds to learn; tools tell you more about each chest.
/// </summary>
public partial class DayEndPhase : Phase
{
    const float CardW = 380, CardH = 168, CardGap = 16, CardsX = 50, CardsY = 222;

    Node2D _cards;
    Label _goldLabel;
    MentorView _mentor;
    int _topUp;

    public override void _Ready()
    {
        _topUp = State.EndDay();
        Add(UiKit.MakeBackdrop("cellar_night", 0.45f));
        Game.Hud.SetDay(State.Day, State.Tier);
        Game.Hud.SetGold(State.Gold);

        var today = State.Today;
        Add(UiKit.MakeLabel($"END OF DAY {State.Day}", 32, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 72, 1280, 40));
        string summary = today.Bought == 0
            ? $"You passed on all {today.Passed} customers."
            : $"Bought {today.Bought}, passed on {today.Passed}. Spent {today.Spent}g, the chests held {today.Earned}g: " +
              $"{(today.Profit >= 0 ? "+" : "")}{today.Profit}g." +
              (today.PerfectReads > 0 ? $" Perfect reads: {today.PerfectReads}." : "");
        Add(UiKit.MakeLabel(summary, 24, today.Profit > 0 ? Palette.Good : today.Profit < 0 ? Palette.Bad : Palette.Text,
            HorizontalAlignment.Center, outline: 4).Place(0, 118, 1280, 30));
        if (_topUp > 0)
            Add(UiKit.MakeLabel($"Uncle Bram slipped you {_topUp}g to keep the shop going.", 16, Palette.Warn, HorizontalAlignment.Center, outline: 4)
                .Place(0, 152, 1280, 20));

        Add(Header("SPEND IT ON THE SHOP", CardsX, 192));
        _goldLabel = Add(UiKit.MakeLabel("", 16, Palette.Gold, HorizontalAlignment.Right).Place(830, 192, 400, 20));
        _cards = Add(new Node2D());
        BuildCards();

        Add(UiKit.MakeButton($"Open the shop: Day {State.Day + 1}", Palette.Gold, 24, NextDay).Place(900, 626, 340, 64));
        _mentor = Add(new MentorView { Position = new Vector2(40, 576), ZIndex = 20 });
        if (State.FirstTime("dayend")) _mentor.Say(Hints.Get("dayend"));
        Sfx.Play(today.Profit >= 0 ? "win" : "lose", 1f, -6);
        Game.Moment("09_dayend", 0.5);
    }

    /// <summary>Renovation first, then tools by price. Things you can't afford yet still show, so you can plan.</summary>
    void BuildCards()
    {
        foreach (var child in _cards.GetChildren()) child.QueueFree();
        _goldLabel.Text = $"You have {State.Gold}g";
        var offers = Upgrades.Available(State).OrderByDescending(u => u.IsRenovation).ThenBy(u => u.Cost).Take(6).ToList();
        if (offers.Count == 0)
            _cards.AddChild(UiKit.MakeLabel("Nothing left to buy. The shop is complete.", 24, Palette.Muted, HorizontalAlignment.Center)
                .Place(0, CardsY + 80, 1280, 30));
        for (int i = 0; i < offers.Count; i++)
        {
            float x = CardsX + i % 3 * (CardW + CardGap), y = CardsY + i / 3 * (CardH + CardGap);
            _cards.AddChild(MakeCard(offers[i], x, y));
        }

        var owned = Upgrades.All.Where(u => State.Has(u.Key) && !u.IsRenovation).ToList();
        if (owned.Count == 0) return;
        _cards.AddChild(UiKit.MakeLabel("Your tools", 16, Palette.Muted).Place(520, 590, 200, 20));
        for (int i = 0; i < owned.Count; i++)
            _cards.AddChild(UiKit.MakeSprite($"res://assets/ui/icons/{owned[i].Icon}.png", new Vector2(546 + i * 50, 640), 1));
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
        }
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

    public void NextDay()
    {
        Sfx.Play("bid");
        Game.StartDay();
    }
}
