using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// Nim the peddler drops by before the shop opens, once a week from the second: three curios laid out on the counter,
/// each bending one rule of the trade, at prices that climb with the weeks. Buy what suits how you deal (there's room
/// for a few), sell back what doesn't for half, and open the shop.
/// </summary>
public partial class PeddlerPhase : Phase
{
    const float CardH = 316, CardsY = 96, CardGap = 20, CardsRight = 1260;

    // Three cards to the right of Nim; four (a familiar face) start nearer him and are a little narrower.
    float CardW => State.PeddlerCount > 3 ? 225 : 260;
    float CardsX => CardsRight - State.PeddlerCount * (CardW + CardGap) + CardGap;

    SellerView _nim;
    MentorView _mentor;
    Node2D _board;
    bool _leaving;

    public MentorView Mentor => _mentor;

    /// <summary>What's on the counter, and what each costs today.</summary>
    public System.Collections.Generic.IEnumerable<CurioDef> Stock => State.PeddlerStock.Where(c => !State.HasCurio(c.Key));

    public override void _Ready()
    {
        Add(UiKit.MakeShop(State.Tier, 0.6f));
        var nim = new SellerDef { Key = Story.Nim, Name = Peddler.Name };
        _nim = Add(new SellerView(nim) { Position = new Vector2(190, 196) });
        _nim.Enter();
        _nim.Say(Peddler.Greetings[(State.Week - 2 + Peddler.Greetings.Length) % Peddler.Greetings.Length]);
        Sfx.PlayGroup("door_open", 1.2f, -6);
        Add(UiKit.MakeLabel("NIM'S CURIOS", 32, Palette.Magic, HorizontalAlignment.Center, outline: 6).Place(CardsX, 58, CardsRight - CardsX, 40));
        _board = Add(new Node2D());
        _mentor = Add(new MentorView { Position = new Vector2(24, 560), ZIndex = 20 });
        Build();
        if (State.FirstTime("peddler")) _mentor.Say(Hints.Get("peddler"));
        Game.Hud.SetGold(State.Gold, animate: false);
        Game.Moment("22_peddler", 0.8);
    }

    void Build()
    {
        foreach (var child in _board.GetChildren()) child.QueueFree();
        var stock = State.PeddlerStock;
        for (int i = 0; i < stock.Count; i++) _board.AddChild(MakeCard(stock[i], CardsX + i * (CardW + CardGap), CardsY));
        if (stock.Count == 0)
            _board.AddChild(UiKit.MakeLabel("Nim's sold out: you own everything Nim carries!", 24, Palette.Muted, HorizontalAlignment.Center)
                .Place(CardsX, 220, CardsRight - CardsX, 30));
        if (State.Keepsake && stock.Count > 0)
            _board.AddChild(UiKit.MakeLabel("Keepsake: one of these is a gift, whichever you like.", 16, Palette.Good, HorizontalAlignment.Center, outline: 4)
                .Place(CardsX, CardsY + CardH + 6, CardsRight - CardsX, 20));
        // Second Look (a talent): once a visit, Nim packs these away and lays out something else.
        if (State.CanRerollPeddler)
            _board.AddChild(UiKit.MakeButton("Show me something else", Palette.Magic, 16, Reroll).Place(660, 610, 300, 64));

        // Your curios: click one to sell it back for half.
        const float y = 440;
        _board.AddChild(UiKit.MakeLabel($"YOUR CURIOS {State.CurioKeys.Count}/{State.CurioSlots}", 16, Palette.Magic).Place(CardsX, y, 300, 20));
        _board.AddChild(UiKit.MakeLabel("Click one to sell it back to Nim for half what you paid.", 16, Palette.Muted).Place(CardsX + 220, y, 600, 20));
        for (int i = 0; i < State.CurioSlots; i++)
        {
            float x = CardsX + i * 76;
            var slot = UiKit.MakePanel(i < State.CurioKeys.Count ? Palette.Magic.Darkened(0.2f) : Palette.PanelBorder).Place(x, y + 26, 68, 68);
            _board.AddChild(slot);
            if (i >= State.CurioKeys.Count) continue;
            var curio = Curios.Get(State.CurioKeys[i]);
            slot.AddChild(UiKit.MakeCurioIcon(curio, new Vector2(34, 34)));
            var sell = new Button
            {
                Flat = true, FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"{curio.Name}: {curio.Blurb}\nClick to sell it back for {State.CurioValue(curio.Key)}g.",
            };
            sell.Pressed += () => SellBack(curio.Key);
            slot.AddChild(sell.Place(0, 0, 68, 68));
        }
        _board.AddChild(UiKit.MakeButton("Open the shop", Palette.Gold, 24, Leave).Place(980, 610, 280, 64));
    }

    Control MakeCard(CurioDef curio, float x, float y)
    {
        bool owned = State.HasCurio(curio.Key);
        int price = State.CuriosPrice(curio);
        var color = RarityColor(curio.Rarity);
        var card = UiKit.MakePanel(owned ? Palette.PanelBorder : color).Place(x, y, CardW, CardH);
        var shine = new Painter(p =>
        {
            for (int i = 3; i >= 1; i--) p.DrawCircle(new Vector2(CardW / 2, 62), 20 + i * 10, color with { A = 0.08f });
        });
        card.AddChild(shine);
        card.AddChild(UiKit.MakeCurioIcon(curio, new Vector2(CardW / 2, 62)));
        // Four narrower cards: the name in the small size, so the blurb has room.
        bool narrow = State.PeddlerCount > 3;
        float nameH = narrow ? 40 : 60;
        card.AddChild(UiKit.MakeLabel(curio.Name, narrow ? 16 : 24, owned ? Palette.Muted : Palette.Text, HorizontalAlignment.Center, outline: 4)
            .Wrapped().Place(10, 100, CardW - 20, nameH));
        card.AddChild(UiKit.MakeLabel(Curios.RarityName(curio.Rarity), 16, color, HorizontalAlignment.Center).Place(0, 100 + nameH, CardW, 20));
        card.AddChild(UiKit.MakeLabel(curio.Blurb, 16, Palette.Muted, HorizontalAlignment.Center).Wrapped()
            .Place(14, 124 + nameH, CardW - 28, CardH - 60 - 124 - nameH));
        if (owned)
        {
            card.AddChild(UiKit.MakeLabel("Yours", 24, Palette.Good, HorizontalAlignment.Center, outline: 4).Place(0, CardH - 50, CardW, 30));
            return card;
        }
        var buy = UiKit.MakeButton(State.CuriosFull ? "No room" : price == 0 ? "Take it, free" : $"Buy {price}g", price == 0 ? Palette.Good : Palette.Gold, 16,
            () => Buy(curio)).Place(30, CardH - 56, CardW - 60, 44);
        buy.Disabled = State.CuriosFull || State.Gold < price;
        if (State.CuriosFull) buy.TooltipText = "Sell a curio back to Nim first to make room.";
        card.AddChild(buy);
        return card;
    }

    static Color RarityColor(CurioRarity rarity) => rarity switch
    {
        CurioRarity.Rare => Palette.Of(Rarity.Legendary),
        CurioRarity.Uncommon => Palette.Of(Rarity.Rare),
        _ => Palette.Of(Rarity.Uncommon),
    };

    public bool Buy(CurioDef curio)
    {
        int price = State.CuriosPrice(curio);
        if (!State.BuyCurio(curio, price)) return false;
        Sfx.PlayGroup("pay");
        Sfx.Play("sting", 1.2f, -4);
        Game.Hud.SetGold(State.Gold);
        _nim.Say(Peddler.Thanks[State.CurioKeys.Count % Peddler.Thanks.Length], Palette.Good);
        _nim.Bob();
        FloatText($"{curio.Name}!", new Vector2(CardsX + 1.5f * CardW + CardGap, 560), Palette.Magic, 24);
        Build();
        Game.Moment("22_bought", 0.4);
        return true;
    }

    public void Reroll()
    {
        if (!State.CanRerollPeddler) return;
        State.RerollPeddler();
        Sfx.PlayGroup("clack", 1.1f, -4);
        _nim.Say("Not to your taste? Nim has more. Nim always has more.");
        Build();
    }

    public void SellBack(string key)
    {
        int value = State.SellCurio(key);
        if (value <= 0) return;
        Sfx.PlayGroup("pay");
        Game.Hud.SetGold(State.Gold);
        _nim.Say("Back it comes. Nim never forgets a curio.");
        FloatText($"+{value}g", new Vector2(CardsX + 120, 420), Palette.Gold, 24);
        Build();
    }

    public async void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        _nim.Say(Peddler.Bye[State.Week % Peddler.Bye.Length]);
        Sfx.PlayGroup("door_close", 1.2f, -6);
        await ToSignal(_nim.Leave(), Tween.SignalName.Finished);
        if (!Alive) return;
        Game.OpenShop();
    }
}
