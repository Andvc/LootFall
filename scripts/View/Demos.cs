using System;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// Scripted runs for screenshots and checks (--demo=NAME, with --shots=DIR):
/// opening: a chest of each tier opened in turn, from Common to Transcendent (screens reveal_*), and the pause holding
///   one; title: the title, settings, a new game and the pause menu; welcome: a save written and read back in memory as
///   if two hours had passed (checks the round trip, shows the welcome back); screens: the lane with auto-open on, then
///   every screen over it in turn (the equipment screen's body, each region, a slot, a piece and its slots, then the
///   mouse on it: a slot pointed at, the wheel into a region, a worn piece pointed at, a right click back; talents,
///   codex, prestige, settings); pieces: every delivered picture of a piece on the knight, in outfits of pieces that go
///   together, each in another era's metal, on the whole body and each region it's in (pieces_N, pieces_N_REGION).
///   --hours=N starts each where a sensible player would be after N hours.
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
            case "pieces":
                Pieces(m, Math.Max(hours, 0.5));
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

    /// <summary>Every piece with a picture of its own (docs/art-handoff-equipment-pieces.md) worn on the knight: the
    /// pieces in outfits that go together (each piece in the first that has room for it), each outfit in another era's
    /// metal, shown on the whole body and on each region it's in.</summary>
    static async void Pieces(Main m, double hours)
    {
        var s = Main.FastForward(m.Seed, hours);
        m.StartPlaying(s, null);
        s.Auto.Equip = false;
        await Wait(m, 1.5);
        var g = m.Game;
        var eq = new EquipmentScreen(g);
        g.Open(eq);
        await Wait(m, 0.8);
        var c = s.C;
        var h = s.H;
        var outfits = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
        for (int i = 0; i < c.SlotCount; i++)
        {
            if (c.Slots[i].Board != Anatomy.Body || EquipmentMap.Art(c.Slots[i].Name).Count == 0) continue;
            var outfit = outfits.Find(o => o.TrueForAll(j => Array.IndexOf(c.Slots[i].Conflicts, j) < 0));
            if (outfit == null) outfits.Add(outfit = new System.Collections.Generic.List<int>());
            outfit.Add(i);
        }
        // One item level in each of the five metals' eras.
        double[] ilvls = { 50, 300, 1200, 5000, 20000 };
        var rng = new Rng(m.Seed);
        for (int k = 0; k < outfits.Count; k++)
        {
            if (!GodotObject.IsInstanceValid(eq) || eq.Closing) break;
            for (int i = 0; i < c.SlotCount; i++) h.Worn[i] = false;
            var regions = new System.Collections.Generic.HashSet<string>();
            foreach (int i in outfits[k])
            {
                h.Open[i] = true;
                var it = new Item();
                h.Roll(rng, i, ilvls[k % ilvls.Length], 1 + (i + k) % 6, 0.8, it);
                h.Gear[i] = it;
                h.Worn[i] = true;
                regions.Add(Describe.Region(c, c.Slots[i]));
            }
            h.Rebuild();
            GD.Print($"Outfit {k + 1}: {string.Join(", ", outfits[k].ConvertAll(i => c.Slots[i].Name))}");
            eq.ShowBody();
            await Wait(m, 1.0);
            m.Moment($"pieces_{k + 1}");
            foreach (var region in EquipmentMap.Regions)
            {
                if (!regions.Contains(region) || !GodotObject.IsInstanceValid(eq) || eq.Closing) continue;
                eq.ShowRegion(region);
                await Wait(m, 0.8);
                m.Moment($"pieces_{k + 1}_" + region.Replace(' ', '_').ToLowerInvariant());
                await Wait(m, 0.1);
            }
        }
        await Wait(m, 0.3);
        m.QuitGame();
    }

    /// <summary>Every overlay once, after <paramref name="hours"/> of sensible play (screens lane, armoury_*, talents,
    /// codex, prestige, settings).</summary>
    static async void Screens(Main m, double hours)
    {
        var s = Main.FastForward(m.Seed, hours);
        m.StartPlaying(s, null);
        // With auto-open on, so the tray shows which chests wait.
        if (s.Unlocked(Feature.AutoOpen))
        {
            s.Auto.OpenChests = true;
            m.Game.Tray.Refresh();
        }
        await Wait(m, 2.5);
        m.Moment("lane");
        await Wait(m, 0.3);
        var g = m.Game;
        void Show(Control c) => g.Open(c);
        // The equipment: the whole body, then each region (zooming in), a slot, a worn piece pointed at (what it covers lit,
        // what it keeps free in red), the piece in detail, one of its own slots.
        var eq = new EquipmentScreen(g);
        Show(eq);
        await Wait(m, 0.9);
        m.Moment("equipment");
        foreach (var region in EquipmentMap.Regions)
        {
            if (!GodotObject.IsInstanceValid(eq) || eq.Closing) break;
            eq.ShowRegion(region);
            await Wait(m, 0.75);
            m.Moment("equipment_" + region.Replace(' ', '_').ToLowerInvariant());
            await Wait(m, 0.2);
        }
        if (GodotObject.IsInstanceValid(eq) && !eq.Closing)
        {
            // A slot chosen: every piece that can fill the helm's place, with what wearing each would do.
            eq.ShowRegion("Head");
            await Wait(m, 0.6);
            eq.PickCell(s.C.Anatomy.CellId("scalp.head"));
            await Wait(m, 0.4);
            m.Moment("equipment_slot");
            await Wait(m, 0.2);
        }
        if (GodotObject.IsInstanceValid(eq) && !eq.Closing)
        {
            // A worn piece with slots of its own (the headwear if there is one): pointed at, in detail, one of its slots.
            int host = -1, head = s.C.Anatomy.CellId("scalp.head");
            for (int i = 0; i < s.C.SlotCount && host < 0; i++)
                if (s.H.Active[i] && Array.IndexOf(s.C.Slots[i].Takes, head) >= 0 && s.C.Slots[i].Brings.Length > 0) host = i;
            for (int i = 0; i < s.C.SlotCount && host < 0; i++)
                if (s.H.Active[i] && s.C.Slots[i].Brings.Length > 0 && s.C.Slots[i].Board == Anatomy.Body) host = i;
            if (host >= 0)
            {
                eq.ShowRegion(Describe.Region(s.C, s.C.Slots[host]));
                await Wait(m, 0.7);
                eq.Preview(host);
                await Wait(m, 0.25);
                m.Moment("equipment_cover");
                await Wait(m, 0.2);
                eq.ShowPiece(host);
                await Wait(m, 0.7);
                m.Moment("equipment_piece");
                await Wait(m, 0.2);
                var d = s.C.Slots[host];
                if (d.Brings.Length > 0 && d.Brings[0].Opens.Length > 0)
                {
                    eq.PickCell(d.Brings[0].Opens[0]);
                    await Wait(m, 0.5);
                    m.Moment("equipment_own");
                    await Wait(m, 0.2);
                }
            }
        }
        if (GodotObject.IsInstanceValid(eq) && !eq.Closing)
        {
            // The mouse on the figure, as a player would use it: through the viewport, so the input handling is what's
            // tested.
            var vp = m.GetViewport();
            void Move(Vector2 at) => vp.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
            void Button(Vector2 at, MouseButton b)
            {
                vp.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = b, Pressed = true }, true);
                vp.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = b, Pressed = false }, true);
            }
            eq.ShowBody();
            await Wait(m, 0.9);
            // A slot on the right forearm pointed at: its site and region light, its name shows.
            var arm = eq.Doll.GlobalCellPoint(s.C.Anatomy.CellId("R.forearm.skin"));
            Move(arm + new Vector2(0, 1));
            await Wait(m, 0.3);
            m.Moment("equipment_point_slot");
            await Wait(m, 0.2);
            // The wheel turned in over the head: into its page.
            var head = eq.Doll.GlobalCellPoint(s.C.Anatomy.CellId("brow.skin"));
            Move(head);
            Button(head, MouseButton.WheelUp);
            await Wait(m, 1.0);
            m.Moment("equipment_wheel_in");
            await Wait(m, 0.2);
            // A worn piece pointed at: its area lit in its colour, what it keeps free in red, its name and slots.
            int worn = -1;
            foreach (var cell in s.C.Cells)
                if (cell.Board == Anatomy.Body && cell.Region == "Head" && eq.Doll.Filling(cell.Id) is var f && f >= 0 && s.C.Slots[f].Keeps.Length > 0)
                {
                    worn = cell.Id;
                    break;
                }
            if (worn >= 0)
            {
                Move(eq.Doll.GlobalCellPoint(worn));
                await Wait(m, 0.3);
                m.Moment("equipment_point_piece");
                await Wait(m, 0.2);
            }
            // A right click: back to the whole body.
            Button(head, MouseButton.Right);
            await Wait(m, 1.0);
            m.Moment("equipment_back_out");
            await Wait(m, 0.2);
        }
        if (GodotObject.IsInstanceValid(eq)) eq.Close();
        await Wait(m, 0.3);
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
