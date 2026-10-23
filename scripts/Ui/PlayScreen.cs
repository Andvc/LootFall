using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The game: the battle lane on top, gear, chest and upgrades below. Each frame it steps the game state and hands
/// every event to whoever shows it, and it does the flying between panels: chests falling from the battle into the
/// pile, coins to the gold counter, a new item from its card to its slot (and the knight lighting up).
/// Keys: Space or Enter opens a chest or takes the recommended choice, E equips, S sells, B fights the boss.
/// </summary>
public partial class PlayScreen : Screen
{
    public GameState G { get; }
    public BattleView Battle { get; private set; }
    public GearPanel Gear { get; private set; }
    public ChestPanel Chest { get; private set; }
    public UpgradePanel Upgrades { get; private set; }
    public Hud Hud { get; private set; }

    Node2D _flyLayer;
    double _refreshTimer;
    readonly OfflineReport _welcome;
    readonly Random _rng = new();

    public PlayScreen(GameState g, OfflineReport welcome = null)
    {
        G = g;
        _welcome = welcome;
    }

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = Palette.Bg, MouseFilter = Control.MouseFilterEnum.Ignore }.Place(-100, -100, 1480, 920));
        Battle = new BattleView(G);
        AddChild(Battle);
        // The floor under the lane.
        AddChild(new ColorRect { Color = new Color("#16121f"), MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 420, 1280, 300));
        AddChild(new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 418, 1280, 4));

        Gear = new GearPanel(G) { Position = new Vector2(8, 432) };
        AddChild(Gear);
        Chest = new ChestPanel(G) { Position = new Vector2(460, 432) };
        AddChild(Chest);
        Upgrades = new UpgradePanel(G) { Position = new Vector2(832, 432) };
        AddChild(Upgrades);
        Hud = new Hud(G);
        AddChild(Hud);
        _flyLayer = new Node2D { ZIndex = 80 };
        AddChild(_flyLayer);

        Hud.MenuPressed += () => Game.OpenPause();
        Battle.ChestDropped += DropChest;
        Battle.GoldDropped += DropGold;
        Chest.Equipped += OnEquipped;
        Chest.Sold += OnSold;
        Chest.Revealed += OnRevealed;
        Upgrades.Bought += OnBought;

        if (_welcome != null) ShowWelcome(_welcome);
        else if (G.ChestsOpened == 0 && G.FirstTime("open")) Hint("Open the chest!  Click it, or press SPACE.", 5);
    }

    /// <summary>A tip over the lane, the first time something happens (remembered with the save).</summary>
    void Tip(string key, string text, float seconds = 4)
    {
        if (G.FirstTime(key)) Hint(text, seconds);
    }

    void OnRevealed(Opening o)
    {
        Gear.Highlight(o.Item.Slot);
        if (o.Item.Rarity >= Rarity.Legendary) Tip("legendary", "A LEGENDARY!  Its power goes off in the fight.", 4);
        else Tip("card", "Better than what he wears? EQUIP it.  Worse? SELL it for gold.  SPACE picks the glowing one.", 6);
    }

    public override void _Process(double delta)
    {
        G.Update(delta);
        foreach (var e in G.Events) Handle(e);
        G.Events.Clear();

        _refreshTimer -= delta;
        if (_refreshTimer <= 0)
        {
            _refreshTimer = 0.2;
            Upgrades.Refresh();
            Hud.Refresh();
            if (G.ChestLevel == 1 && G.Gold >= Core.Upgrades.Cost(G, Upgrade.ChestLevel))
                Tip("chestlevel", "You can afford CHEST LEVEL 2: stronger, rarer loot from every chest.  (Upgrades, right)", 5);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: var echo } key) return;
        switch (key.Keycode)
        {
            case Key.Space:
            case Key.Enter:
            case Key.KpEnter:
                // Holding Space keeps opening (a repeat only starts an opening, never takes a choice).
                if (!echo || !Chest.Busy) Chest.Primary();
                break;
            case Key.E when !echo:
                Chest.PressEquip();
                break;
            case Key.S when !echo:
                Chest.PressSell();
                break;
            case Key.B when !echo:
                if (G.ChallengeBoss()) Sfx.PlayGroup("boss", 1f, -4);
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    void Handle(GameEvent e)
    {
        Battle.Handle(e);
        switch (e)
        {
            case BossAvailable when !G.AutoBoss:
                Sfx.PlayGroup("boss", 1.2f, -10);
                Hint("The boss is ready.  Press FIGHT BOSS (or B).", 3);
                break;
            case BossAvailable:
                Tip("boss", "Ten kills: here comes the boss.  Beat it within 30 seconds to move on.", 4);
                break;
            case BossStarted b:
                Battle.Banner(b.Boss.ZoneBoss ? b.Boss.Name.ToUpperInvariant() : "BOSS!", new Color("#ff5a6a"),
                    b.Boss.ZoneBoss ? $"Guardian of {World.Zone(G.Stage).Name}  -  {Balance.BossTime:0}s to win" : $"{b.Boss.Name}  -  {Balance.BossTime:0}s to win", 1.0f);
                Sfx.PlayGroup("boss", 0.9f, -2);
                Game.Shake(0.4f);
                Game.Music.Play("boss", 0.6f);
                Game.Moment("boss_fight", 1.5);
                break;
            case BossFailed f:
                Battle.Banner(f.TimedOut ? "TIME'S UP" : "DEFEATED", Palette.Bad, "Open chests and train, then try again", 1.2f);
                Tip("bossfail", "Auto-Boss tries again once he's stronger.  Chest Level and training get him there.", 5);
                Sfx.Play("down", 1f, -4);
                Game.Music.Play(World.Zone(G.Stage).Music, 1.5f);
                break;
            case StageCleared c:
                Battle.Banner(c.ZoneBoss ? "ZONE CLEAR!" : "STAGE CLEAR", Palette.Gold, null, 0.9f);
                Sfx.PlayGroup("victory", 1f, -4);
                if (c.ZoneBoss) Fx.Confetti(_flyLayer, new Vector2(640, 300), 140);
                Game.Music.Play(World.Zone(G.Stage + 1).Music, 1.5f);
                Game.Moment(c.ZoneBoss ? "zone_clear" : "stage_clear", 0.3);
                break;
            case StageEntered s:
                if (s.NewZone) EnterZone(s.Stage);
                Hud.Refresh();
                Game.Save();
                break;
            case Retreated r:
                Hint($"Too strong for now: back to {World.StageName(r.Stage)} to gear up.", 3);
                break;
            case MimicAppeared:
                Battle.Banner("A MIMIC!", Palette.Of(Rarity.Legendary), "It's guarding royal treasure", 0.8f);
                Sfx.Play("sparkle", 0.8f, -2);
                Game.Moment("mimic", 1.2);
                break;
            case FeatureUnlocked { Key: "autoopen" }:
                Hint("New upgrade: AUTO-OPEN.  It opens chests for you and stops for the big ones.", 5);
                Sfx.PlayGroup("unlock", 1f, -4);
                break;
            case AutoOpened a:
                OnAutoOpened(a);
                break;
            case AutoHeld h:
                Chest.PresentHeld(h.Opening);
                break;
            case HeroDown:
                Game.Moment("knight_down", 0.5);
                break;
        }
    }

    void EnterZone(int stage)
    {
        Battle.SetZone(stage);
        var zone = World.Zone(stage);
        Battle.Banner(zone.Name.ToUpperInvariant(), Palette.Text, $"Zone {World.ZoneIndex(stage) + 1}  -  new monsters, loot +1 level", 1.6f);
        Game.Moment($"zone_{zone.Key}", 2.0);
    }

    // ------------------------------------------------------------------------------------------------- loot flying

    /// <summary>A chest pops out of a monster and falls down into the pile ("lootfall").</summary>
    void DropChest(ChestKind kind, Vector2 from)
    {
        var chest = new Sprite2D { Texture = GD.Load<Texture2D>($"res://assets/ui/chests/{Chests.Get(kind).Skin}.png"), Position = from, Scale = Vector2.One * 1.5f };
        _flyLayer.AddChild(chest);
        if (kind != ChestKind.Wooden) Fx.Burst(_flyLayer, from, Palette.OfChest(kind), 24, 260);
        Chest.Falling[(int)kind]++;
        Sfx.Play("pop", 1f + (float)_rng.NextDouble() * 0.2f, -8);
        var to = Chest.PileSpot + new Vector2((float)_rng.NextDouble() * 40 - 20, 0);
        var up = chest.CreateTween();
        up.TweenProperty(chest, "position", from + new Vector2(-20 + (float)_rng.NextDouble() * 40, -70), 0.22f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        up.TweenCallback(Callable.From(() => Fx.Fly(chest, to, 0.55f, 40, () =>
        {
            Chest.Falling[(int)kind] = Math.Max(0, Chest.Falling[(int)kind] - 1);
            Chest.ChestArrived(kind);
            Hud.ChestsChanged();
            Fx.Dust(_flyLayer, to, 0.6f);
            Sfx.PlayGroup("chest_land", 1f, -12, 0.08f);
        }, spin: (float)(_rng.NextDouble() - 0.5) * 6)));
    }

    void DropGold(double amount, Vector2 from)
    {
        Fx.Float(_flyLayer, "+" + Num.Short(amount), from + new Vector2(0, -30), Palette.Gold, 16, rise: 40, duration: 0.8f);
        int coins = amount >= G.Stats.Atk * 4 ? 6 : 2;
        Fx.CoinsTo(_flyLayer, from, Hud.GoldSpot, coins, () =>
        {
            Hud.CoinIn();
            Sfx.PlayGroup("coin", 1.4f, -18, 0.05f);
        });
    }

    void OnEquipped(Opening o, double sold, Vector2 from)
    {
        Gear.Highlight(null);
        var item = o.Item;
        var icon = UiKit.MakeItemIcon(item, from, 64, glow: false);
        _flyLayer.AddChild(icon);
        var to = Gear.SlotCenter(item.Slot);
        Sfx.Play("whoosh", 1.2f, -8);
        var color = Palette.Of(item.Rarity);
        Fx.Fly(icon, to, 0.32f, 80, () =>
        {
            Gear.Refresh();
            Gear.Flash(item.Slot, color);
            Gear.BumpPower();
            Fx.Ring(_flyLayer, to, color, 70, 0.35f, 4);
            Sfx.PlayGroup(Slots.Get(item.Slot).Metal ? "equip_metal" : "equip_cloth", 1f, -4);
            Sfx.Play("upgrade", 1f + 0.04f * (int)item.Rarity, -8);
            Battle.RefreshGear();
            Battle.PowerUp(Math.Max(0, o.PowerDelta), color);
            if (sold > 0)
                Fx.CoinsTo(_flyLayer, to, Hud.GoldSpot, 4, () =>
                {
                    Hud.CoinIn();
                    Sfx.PlayGroup("coin", 1.2f, -14, 0.05f);
                });
            Game.Moment($"equipped_{item.Rarity.ToString().ToLowerInvariant()}", 0.4);
            Tip("equip", "Every piece shows in the fight: watch the numbers, and his blade and cape.", 4);
        });
        Upgrades.Refresh();
        Hud.Refresh();
    }

    void OnSold(Opening o, double gold, Vector2 from)
    {
        Gear.Highlight(null);
        Fx.Burst(_flyLayer, from, Palette.Of(o.Item.Rarity), 20, 200);
        Fx.Float(_flyLayer, "+" + Num.Short(gold) + "g", from + new Vector2(0, -30), Palette.Gold, 24, rise: 50, duration: 0.9f);
        Sfx.PlayGroup("sell", 1f, -6);
        Fx.CoinsTo(_flyLayer, from, Hud.GoldSpot, (int)Math.Clamp(2 + (int)o.Item.Rarity * 2, 2, 12), () =>
        {
            Hud.CoinIn();
            Sfx.PlayGroup("coin", 1.3f, -14, 0.05f);
        }, delay: 0.05f);
        Upgrades.Refresh();
        Hud.Refresh();
    }

    /// <summary>Auto-open did one quietly: a little icon pops off the chest toward its slot (kept) or the gold (sold).</summary>
    void OnAutoOpened(AutoOpened a)
    {
        var item = a.Opening.Item;
        var from = Chest.ChestCenter + new Vector2(0, -40);
        var icon = UiKit.MakeItemIcon(item, from, 48, glow: false);
        icon.Scale = Vector2.One * 0.75f;
        _flyLayer.AddChild(icon);
        Chest.Refresh();
        Hud.ChestsChanged();
        if (a.Equipped)
        {
            var to = Gear.SlotCenter(item.Slot);
            Fx.Fly(icon, to, 0.35f, 60, () =>
            {
                Gear.Refresh();
                Gear.Flash(item.Slot, Palette.Of(item.Rarity));
                Battle.RefreshGear();
                Battle.PowerUp(Math.Max(0, a.Opening.PowerDelta), Palette.Of(item.Rarity));
                Sfx.PlayGroup(Slots.Get(item.Slot).Metal ? "equip_metal" : "equip_cloth", 1f, -8);
            });
        }
        else
        {
            Fx.Fly(icon, Hud.GoldSpot, 0.4f, 50, () =>
            {
                Hud.CoinIn();
                Sfx.PlayGroup("coin", 1.3f, -16, 0.05f);
            });
        }
    }

    void OnBought(Upgrade u)
    {
        Gear.Refresh();
        Hud.Refresh();
        Chest.Refresh();
        if (u == Upgrade.ChestLevel)
        {
            var at = Chest.ChestCenter;
            Fx.Ring(_flyLayer, at, Palette.Gold, 200, 0.6f, 8);
            Fx.Burst(_flyLayer, at, Palette.Gold, 60, 420);
            Fx.Float(_flyLayer, $"CHEST LEVEL {G.ChestLevel}!", at + new Vector2(0, -110), Palette.Gold, 32, rise: 50, duration: 1.6f);
            Sfx.Play("fanfare", 1.1f, -6);
            Game.Shake(0.15f);
            Game.Moment("chest_level", 0.4);
        }
        else if (u == Upgrade.AutoOpen)
        {
            Hint("Auto-Open on: it stops for anything Epic or better.  Toggle it on the chest.", 4);
        }
        else Battle.PowerUp(0, Palette.Good);
    }

    // ------------------------------------------------------------------------------------------------- messages

    readonly System.Collections.Generic.List<Label> _hints = new();

    /// <summary>A line of advice over the lane for a few seconds (stacking under any still showing).</summary>
    public void Hint(string text, float seconds)
    {
        _hints.RemoveAll(h => !IsInstanceValid(h));
        var label = UiKit.MakeLabel(text, 16, Palette.Text, HorizontalAlignment.Center, outline: 6).Place(140, 120 + 26 * _hints.Count, 1000, 24);
        label.ZIndex = 70;
        AddChild(label);
        _hints.Add(label);
        label.Modulate = Colors.Transparent;
        var t = label.CreateTween();
        t.TweenProperty(label, "modulate", Colors.White, 0.25f);
        t.TweenInterval(seconds);
        t.TweenProperty(label, "modulate:a", 0f, 0.4f);
        t.TweenCallback(Callable.From(label.QueueFree));
    }

    void ShowWelcome(OfflineReport r)
    {
        var layer = new Node2D { ZIndex = 200 };
        AddChild(layer);
        var panel = UiKit.MakePanel(Palette.Gold).Place(390, 196, 500, 222);
        layer.AddChild(panel);
        panel.AddChild(UiKit.MakeLabel("WELCOME BACK", 32, Palette.Gold, HorizontalAlignment.Center, outline: 6).Place(0, 16, 500, 40));
        panel.AddChild(UiKit.MakeLabel($"Your knight kept fighting for {Num.Time(r.Seconds)}.", 16, Palette.Text, HorizontalAlignment.Center).Place(0, 64, 500, 20));
        panel.AddChild(UiKit.MakeLabel($"{Num.Short(r.Kills)} monsters slain", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 92, 500, 20));
        panel.AddChild(UiKit.MakeLabel($"+{Num.Short(r.Gold)} gold    +{r.Chests.Sum()} chests", 24, Palette.Good, HorizontalAlignment.Center, outline: 4).Place(0, 116, 500, 30));
        if (r.ChestGold > 0)
            panel.AddChild(UiKit.MakeLabel($"(the chests past {Balance.OfflineChests} were sold for {Num.Short(r.ChestGold)})", 16, Palette.Muted,
                HorizontalAlignment.Center).Place(0, 146, 500, 20));
        var ok = UiKit.MakeButton("COLLECT", Palette.Good.Darkened(0.3f), 16, () =>
        {
            Sfx.PlayGroup("coins", 1f, -2);
            Fx.CoinsTo(_flyLayer, new Vector2(640, 330), Hud.GoldSpot, 14, () => Hud.CoinIn());
            Chest.Refresh();
            layer.QueueFree();
        }).Place(170, 172, 160, 36);
        panel.AddChild(ok);
        Sfx.Play("chime", 1f, -6);
    }
}
