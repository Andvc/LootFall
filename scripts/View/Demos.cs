using System;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// Scripted runs for screenshots and checks (--demo=NAME, with --shots=DIR):
/// opening: a chest of each tier opened in turn, from Common to Transcendent (screens reveal_*), and the pause holding
///   one; title: the title, settings, a new game and the pause menu; welcome: a save written and read back in memory as
///   if two hours had passed (checks the round trip, shows the welcome back); screens: the lane, then every screen over
///   it in turn (each kind's armoury, talents, codex, prestige, settings). --hours=N starts each where a sensible player
///   would be after N hours.
/// </summary>
public static class Demos
{
    public static void Run(Main m, string name, double hours)
    {
        switch (name)
        {
            case "opening":
                Opening(m, Math.Max(hours, 0.3));
                break;
            case "title":
                Title(m);
                break;
            case "welcome":
                Welcome(m, Math.Max(hours, 0.5));
                break;
            case "screens":
                Screens(m, hours);
                break;
            default:
                GD.PrintErr($"No demo called {name}");
                m.QuitGame();
                break;
        }
    }

    static SignalAwaiter Wait(Main m, double t) => m.ToSignal(m.GetTree().CreateTimer(t, true), SceneTreeTimer.SignalName.Timeout);

    static async void Opening(Main m, double hours)
    {
        var s = Main.FastForward(m.Seed, hours);
        m.StartPlaying(s, null);
        await Wait(m, 1.5);
        var rng = new Rng(m.Seed);
        for (int r = 0; r < Content.RarityNames.Length; r++)
        {
            var g = m.Game;
            if (g == null) return;
            // A chest whose rarest find is tier r, with a few lesser things beside it.
            var chest = new Chest { Kind = r >= 8 ? ChestKind.Guardian : r >= 4 ? ChestKind.Boss : ChestKind.Monster, Depth = s.H.RunDepth };
            var item = new Item();
            int added = 0;
            for (int slot = 0; slot < s.H.Gear.Length && added < 5; slot++)
            {
                if (!s.H.Open[slot]) continue;
                int rarity = added == 0 ? r : Math.Max(0, r - 1 - added % 3);
                s.H.Roll(rng, slot, s.H.RunDepth + 5, rarity, 0.7 + 0.3 * rng.NextDouble(), item);
                chest.Add(slot, item);
                chest.Counts[rarity]++;
                added++;
            }
            chest.Materials = 10 * (r + 1);
            s.Pile.Waiting.Insert(0, chest);
            g.OpenNext();
            while (IsInstanceValid(g) && g.Reveal != null) await Wait(m, 0.1);
            await Wait(m, 0.6);
            m.Moment($"after_{Look.TierName(r).ToLowerInvariant()}");
        }
        await Wait(m, 1.0);
        m.QuitGame();
    }

    static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);

    static async void Title(Main m)
    {
        m.SwitchTo(new TitleScreen(null));
        await Wait(m, 2.5);
        m.Moment("title");
        await Wait(m, 0.3);
        m.Screen.AddChild(new SettingsView(null));
        await Wait(m, 0.6);
        m.Moment("settings");
        await Wait(m, 0.3);
        m.StartPlaying(Session.NewGame(m.Seed), null);
        await Wait(m, 3.0);
        m.Moment("new_game");
        m.Game?.OpenPause();
        await Wait(m, 0.8);
        m.Moment("pause");
        await Wait(m, 0.3);
        m.QuitGame();
    }

    /// <summary>Every overlay once, after <paramref name="hours"/> of sensible play (screens lane, armoury_*, talents,
    /// codex, prestige, settings).</summary>
    static async void Screens(Main m, double hours)
    {
        var s = Main.FastForward(m.Seed, hours);
        m.StartPlaying(s, null);
        await Wait(m, 2.5);
        m.Moment("lane");
        await Wait(m, 0.3);
        var g = m.Game;
        void Show(Control c) => g.Open(c);
        for (int k = 0; k < 8; k++)
        {
            Show(new Armoury(g, (SlotKind)k));
            await Wait(m, 0.7);
            g.CloseOverlay();
            await Wait(m, 0.25);
        }
        Show(new TalentScreen(g));
        await Wait(m, 0.8);
        m.Moment("talents");
        await Wait(m, 0.3);
        g.CloseOverlay();
        await Wait(m, 0.25);
        Show(new CodexScreen(g));
        await Wait(m, 0.7);
        m.Moment("codex");
        await Wait(m, 0.3);
        g.CloseOverlay();
        await Wait(m, 0.25);
        Show(new PrestigeScreen(g));
        await Wait(m, 0.7);
        m.Moment("prestige");
        await Wait(m, 0.3);
        g.CloseOverlay();
        await Wait(m, 0.25);
        Show(new SettingsView(g));
        await Wait(m, 0.6);
        g.CloseOverlay();
        await Wait(m, 0.3);
        m.QuitGame();
    }

    static async void Welcome(Main m, double hours)
    {
        var played = Main.FastForward(m.Seed, hours);
        var data = SaveGame.Capture(played, m.Seed, (long)Time.GetUnixTimeFromSystem() - 2 * 3600);
        var json = SaveGame.ToJson(data);
        var loaded = SaveGame.Restore(SaveGame.FromJson(json));
        bool same = loaded.H.RunDepth == played.H.RunDepth && loaded.H.EverDepth == played.H.EverDepth && loaded.H.Level == played.H.Level
                    && loaded.H.Gold.Equals(played.H.Gold) && loaded.H.CodexCount == played.H.CodexCount;
        GD.Print($"Save round trip {(same ? "ok" : "MISMATCH")}: {json.Length / 1024} KB, depth {loaded.H.RunDepth}, level {loaded.H.Level}, {loaded.H.CodexCount} first finds");
        var away = loaded.CatchUp(2 * 3600);
        GD.Print($"Time away: 2h, depth {away.DepthBefore} -> {away.DepthAfter}, {away.Chests} chests, {away.Equipped} upgrades");
        m.StartPlaying(loaded, away);
        await Wait(m, 2.5);
        m.Moment("welcome");
        await Wait(m, 0.5);
        m.QuitGame();
    }
}
