using System;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// Plays the game through its screen the way a sensible player would: opens chests with the tray's button (watching each
/// reveal), spends gold and shards with the dock, takes talents, turns auto-open on once it can, ascends by the
/// simulator's rule, and peeks into each overlay once for a screenshot. Logs as it goes and quits after its minutes,
/// printing "Autoplay finished" (the CI smoke test looks for it).
/// </summary>
public partial class Bot : Node
{
    public double Minutes = 5, Speed = 1;
    GameScreen _game;
    double _played, _think, _fpsLog = 5;
    bool _done;
    int _lastDepth, _peeks;

    public void OnScreen(Control screen)
    {
        _game = screen as GameScreen;
        if (_game != null) _lastDepth = _game.S.H.RunDepth;
    }

    public override void _Ready()
    {
        Engine.TimeScale = Speed;
        ProcessMode = ProcessModeEnum.Always;
        GD.Print($"Autoplay: {Minutes} minutes at speed {Speed}");
    }

    public override void _Process(double delta)
    {
        if (_done || _game == null || !IsInstanceValid(_game)) return;
        var s = _game.S;
        _played += delta;
        if ((_fpsLog -= delta) <= 0)
        {
            _fpsLog = 20;
            GD.Print($"[{_played / 60:0.0}m] fps {Engine.GetFramesPerSecond():0}, {GetTree().GetNodeCount()} nodes");
        }
        if (s.H.RunDepth != _lastDepth)
        {
            _lastDepth = s.H.RunDepth;
            if (_lastDepth % 5 == 0) GD.Print($"[{_played / 60:0.0}m] depth {_lastDepth} (record {s.H.EverDepth}), level {s.H.Level}, {s.H.OpenCount} slots, {Format.Num(s.H.Gold)} gold");
        }
        if (_played >= Minutes * 60)
        {
            _done = true;
            GD.Print($"Autoplay finished {Minutes:0.#} minutes: depth {s.H.RunDepth}, record {s.H.EverDepth}, level {s.H.Level}, {s.Totals.Opened} chests opened, " +
                     $"{s.Totals.Upgrades} upgrades, {s.H.CodexCount} first finds, {s.H.Ascension.Resets} ascensions, found {string.Join(" ", s.Totals.Found.Select((n, r) => n > 0 ? $"{Look.TierName(r)[0]}{Look.TierName(r)[1]}{n}" : null).Where(x => x != null))}");
            Main.Instance?.Moment("final_state");
            GetTree().CreateTimer(1.0, true, false, true).Timeout += () => Main.Instance?.QuitGame();
            return;
        }
        if (GetTree().Paused) return;
        if ((_think -= delta) > 0) return;
        _think = 0.4;

        // Close what's open after a look (the armoury and the rest get one screenshot each).
        if (_game.OverlayOpen && _game.Reveal == null)
        {
            _game.CloseOverlay();
            return;
        }
        if (_game.Reveal != null) return;

        if (s.Unlocked(Feature.AutoOpen) && !s.Auto.OpenChests && s.Totals.Opened > 20)
        {
            _game.Tray.GetChildren().OfType<Button>().FirstOrDefault(b => b.Text.StartsWith("Auto"))?.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print($"[{_played / 60:0.0}m] auto-open on");
            return;
        }
        if (s.Pile.Waiting.Count > 0)
        {
            var chest = s.Pile.Next();
            GD.Print($"[{_played / 60:0.0}m] open a {chest.Kind} chest (depth {chest.Depth}, {chest.ItemCount} items, best {Look.TierName(Math.Max(0, chest.BestRarity))})");
            _game.OpenNext();
            return;
        }
        if (s.Unlocked(Feature.Forge) && !s.Unlocked(Feature.AutoForge) && _game.Dock.Forge.Visible && _played % 10 < 0.5) _game.Dock.Press(_game.Dock.Forge);
        if (s.Unlocked(Feature.Ranks) && !s.Unlocked(Feature.AutoRanks) && _game.Dock.Ranks.Visible && _played % 10 < 0.5) _game.Dock.Press(_game.Dock.Ranks);
        if ((int)_played % 60 == 0 && s.AutoPrestige(transcend: true) != Strategy.Reset.None) GD.Print($"[{_played / 60:0.0}m] reset: {s.H.Ascension.Resets} ascensions");

        // A look into each system once, for the screenshots.
        if (_played > 20 + _peeks * 15 && _peeks < 5)
        {
            switch (_peeks++)
            {
                case 0: _game.Open(new Armoury(_game, SlotKind.Weapon)); Main.Instance?.Moment("armoury", 0.5); break;
                case 1: if (s.Unlocked(Feature.Talents)) { _game.Open(new TalentScreen(_game)); Main.Instance?.Moment("talents", 0.6); } break;
                case 2: if (s.Unlocked(Feature.Codex)) { _game.Open(new CodexScreen(_game)); Main.Instance?.Moment("codex", 0.5); } break;
                case 3: _game.Open(new PrestigeScreen(_game)); Main.Instance?.Moment("prestige", 0.5); break;
                case 4: _game.Open(new SettingsView(_game)); break;
            }
        }
    }
}
