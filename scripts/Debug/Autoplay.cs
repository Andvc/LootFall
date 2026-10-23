using System;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Debug;

/// <summary>
/// Plays the game through its own screen, the way a sensible player would (see <see cref="Policy"/>): opens chests by
/// clicking the chest, reads each card for a moment, equips upgrades and sells the rest with the card's buttons, and
/// buys upgrades with the panel's buttons, so every effect the player would see plays. Logs as it goes and quits
/// after its minutes of play (printing "Autoplay finished", which the CI smoke test looks for).
/// </summary>
public partial class Autoplay : Node
{
    public double Minutes = 5;
    public double Speed = 1;
    /// <summary>Saves for Auto-Open once it shows, buys it, sets it to stop for rare finds, and lets it open.</summary>
    public bool AutoOpen;

    PlayScreen _play;
    double _played, _think, _cardSeen, _fpsLog = 5;
    bool _done;
    int _lastStage;

    public void OnScreen(Ui.Screen screen)
    {
        _play = screen as PlayScreen;
        if (_play != null) _lastStage = _play.G.MaxStage;
    }

    public override void _Ready()
    {
        Engine.TimeScale = Speed;
        GD.Print($"Autoplay: {Minutes} minutes at speed {Speed}");
    }

    public override void _Process(double delta)
    {
        if (_done || _play == null || !IsInstanceValid(_play) || GetTree().Paused) return;
        var g = _play.G;
        _played += delta;
        if ((_fpsLog -= delta) <= 0)
        {
            _fpsLog = 20;
            GD.Print($"[{_played / 60:0.0}m] fps {Engine.GetFramesPerSecond():0}, {GetTree().GetNodeCount()} nodes");
        }
        if (g.MaxStage != _lastStage)
        {
            _lastStage = g.MaxStage;
            GD.Print($"[{_played / 60:0.0}m] reached {World.StageName(g.MaxStage)}  power {Num.Short(g.Power)}  chest lv {g.ChestLevel}  gold {Num.Short(g.Gold)}");
        }
        if (_played >= Minutes * 60)
        {
            _done = true;
            GD.Print($"Autoplay finished {Minutes:0.#} minutes: {World.StageName(g.MaxStage)}, power {Num.Short(g.Power)}, chest level {g.ChestLevel}, " +
                     $"{g.ChestsOpened} chests opened, {g.TotalKills} kills, found {string.Join(" ", Rarities.All.Select(r => $"{r.Name[0]}{g.Found[(int)r.Rarity]}"))}");
            Main.Instance.Moment("final_state");
            GetTree().CreateTimer(1.0, true, false, true).Timeout += () => Main.Instance.QuitGame();
            return;
        }

        _think -= delta;
        if (_think > 0) return;
        _think = 0.25;

        var chest = _play.Chest;
        if (chest.Deciding)
        {
            // Read the card for a moment, as a player would, then take the recommended choice with its button.
            _cardSeen += 0.25;
            if (_cardSeen < 0.75) return;
            _cardSeen = 0;
            var card = chest.Card;
            bool equip = Policy.ShouldEquip(card.Opening);
            GD.Print($"[{_played / 60:0.0}m] {(equip ? "equip" : "sell ")} {card.Opening.Item.Rarity,-9} {card.Opening.Item.Name} (power {(card.Opening.PowerDelta >= 0 ? "+" : "")}{Num.Short(card.Opening.PowerDelta)})");
            (equip ? card.EquipButton : card.SellButton).EmitSignal(BaseButton.SignalName.Pressed);
            return;
        }

        if (AutoOpen && g.HasAutoOpen && g.AutoStopAt != Rarity.Rare)
        {
            chest.StopButton.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print($"[{_played / 60:0.0}m] auto-open stops for {g.AutoStopAt}+");
            return;
        }

        bool saving = AutoOpen && Upgrades.Visible(g, Upgrade.AutoOpen) && Upgrades.Open(g, Upgrade.AutoOpen);
        var buy = saving ? (g.Gold >= Upgrades.Cost(g, Upgrade.AutoOpen) ? Upgrade.AutoOpen : null) : Policy.NextBuy(g);
        if (buy is Upgrade u && Upgrades.Visible(g, u))
        {
            _play.Upgrades.ButtonFor(u).EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print($"[{_played / 60:0.0}m] bought {Upgrades.Get(u).Name} -> {Upgrades.Level(g, u)}");
            return;
        }

        if (!chest.Busy && g.ChestTotal > 0 && !(g.HasAutoOpen && g.AutoOpen)) chest.Primary();
    }
}
