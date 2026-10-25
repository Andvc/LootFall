using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The armoury (G): the second level of the gear view (docs/art-direction.md, "Items and icons"). A tab per slot kind;
/// the kind's slots grouped by where they sit (head to toe, a hand of five rings, a foot of five toe rings), only the
/// open ones and the next few to open drawn; and the chosen slot in detail: what's worn, its stats, and its forge, rank
/// and mastery with their buttons.
/// </summary>
public partial class Armoury : Overlay
{
    const float Tile = 84, Pitch = 94;
    readonly Panel _p;
    readonly Canvas _grid;
    readonly Control _detail;
    readonly List<(int Slot, Rect2 Rect)> _tiles = new();
    readonly List<(string Region, Vector2 At)> _headers = new();
    readonly Dictionary<SlotKind, Button> _tabs = new();
    SlotKind _kind;
    int _selected = -1;
    float _t;
    Label _summary;

    public Armoury(GameScreen g, SlotKind kind) : base(g)
    {
        _p = Frame("Armoury");
        // Tabs, one per kind.
        for (int k = 0; k < 8; k++)
        {
            var kk = (SlotKind)k;
            var b = Ui.Button(Describe.Kind(kk), () => Show(kk), size: Look.Body).Place(24, 90 + k * 66, 220, 56);
            _p.AddChild(b);
            _tabs[kk] = b;
        }
        _grid = new Canvas(DrawGrid) { MouseFilter = MouseFilterEnum.Stop }.Place(268, 84, 980, 800);
        _grid.GuiInput += GridInput;
        _p.AddChild(_grid);
        _detail = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(1268, 84, 470, 860);
        _p.AddChild(_detail);
        _summary = Ui.Text("", Look.Body, Look.Muted, Look.UiBold).Place(268, 896, 1000, 40);
        _p.AddChild(_summary);
        var forgeAll = Ui.Button("Forge all", () => { if (G.S.ForgeAll()) Refresh(); }, Look.Gold, Look.Body).Place(24, 640, 220, 56);
        var rankAll = Ui.Button("Rank all", () => { if (G.S.RankAll()) Refresh(); }, Look.Shards, Look.Body).Place(24, 706, 220, 56);
        _p.AddChild(forgeAll);
        _p.AddChild(rankAll);
        forgeAll.Visible = G.S.Unlocked(Feature.Forge);
        rankAll.Visible = G.S.Unlocked(Feature.Ranks);
        Show(kind);
    }

    void Show(SlotKind kind)
    {
        _kind = kind;
        foreach (var (k, b) in _tabs) Ui.Style(b, k == kind ? Look.Gold : Look.EdgeLight, Look.Body);
        Layout();
        _selected = _tiles.Count > 0 ? _tiles[0].Slot : -1;
        // Start on the rarest thing worn.
        int best = -1;
        foreach (var (s, _) in _tiles)
        {
            var it = G.S.H.Gear[s];
            if (it != null && it.Rarity > best)
            {
                best = it.Rarity;
                _selected = s;
            }
        }
        Refresh();
    }

    /// <summary>Lays the kind's slots out by region: the open ones, and up to three still to open (with their depths).</summary>
    void Layout()
    {
        _tiles.Clear();
        _headers.Clear();
        var h = G.S.H;
        var slots = G.S.C.Slots.Where(s => s.Kind == _kind).ToList();
        int locked = 0;
        var shown = new List<SlotDef>();
        foreach (var s in slots.OrderBy(s => s.Index))
        {
            if (h.Open[s.Index]) shown.Add(s);
            else if (locked++ < 3) shown.Add(s);
        }
        // Regions are blocks packed left to right, a new row when the next won't fit.
        const float Width = 980, Gap = 36;
        float x = 0, y = 0, rowH = 0;
        foreach (var group in shown.GroupBy(Describe.Region))
        {
            var list = group.OrderBy(s => SideOrder(s.Name)).ToList();
            int perRow = Math.Max(1, Math.Min(list.Count, (int)(Width / Pitch)));
            float w = perRow * Pitch, hgt = 32 + (int)Math.Ceiling(list.Count / (double)perRow) * (Pitch + 14);
            if (x > 0 && x + w > Width)
            {
                x = 0;
                y += rowH + 10;
                rowH = 0;
            }
            _headers.Add((group.Key, new Vector2(x, y + 22)));
            for (int i = 0; i < list.Count; i++)
                _tiles.Add((list[i].Index, new Rect2(x + i % perRow * Pitch, y + 32 + i / perRow * (Pitch + 14), Tile, Tile)));
            x += w + Gap;
            rowH = Math.Max(rowH, hgt);
        }
        int open = slots.Count(s => h.Open[s.Index]);
        _summary.Text = $"{open} of {slots.Count} {Describe.Kind(_kind).ToLowerInvariant()} slots open" + (locked > 3 ? $", {locked - 3} more not shown yet" : "");
    }

    /// <summary>Left before right, thumb to little finger, in the order a hand is read.</summary>
    static int SideOrder(string name)
    {
        string[] fingers = { "Thumb", "Index", "Middle", "Ring Ring", "Little" };
        int f = Array.FindIndex(fingers, w => name.Contains(w.Split(' ')[0] + " "));
        return (name.StartsWith("Right") ? 100 : 0) + (f >= 0 ? f : 50);
    }

    void GridInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        foreach (var (s, r) in _tiles)
        {
            if (!r.HasPoint(mb.Position)) continue;
            _selected = s;
            Main.Instance?.Sfx?.PlayGroup("click", 1.1f, -12);
            Refresh();
            return;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _grid.QueueRedraw();
    }

    void DrawGrid(Canvas c)
    {
        var h = G.S.H;
        foreach (var (region, at) in _headers)
            c.DrawString(Look.UiBold, at, region.ToUpperInvariant(), HorizontalAlignment.Left, -1, 18, Look.Muted);
        foreach (var (s, r) in _tiles)
        {
            var def = G.S.C.Slots[s];
            var it = h.Gear[s];
            bool open = h.Open[s];
            if (s == _selected) c.DrawStyleBox(Ui.Frame(Colors.Transparent, Look.Gold, 14, 3, 0, 0), r.Grow(5));
            ItemTile.Draw(c, r, Describe.Family(def.Name), open && it != null ? it.Rarity : -1, it?.Affixes ?? 0, forge: open ? h.Forge[s] : 0,
                rank: open ? h.Rank[s] : 0, locked: !open, time: _t, tier: ItemTile.Tier(it));
            if (open && h.Focus[s]) Ink.Glint(c, r.Position + new Vector2(Tile - 10, 10), 9, Look.Xp);
            if (!open)
                c.DrawString(Look.Numbers, r.Position + new Vector2(0, Tile + 16), def.UnlockDepth > 0 ? $"depth {Format.Whole(def.UnlockDepth)}" : "transcend",
                    HorizontalAlignment.Center, Tile, 14, Look.Dim);
        }
    }

    // ------------------------------------------------------------------------------------------------ the chosen slot

    void Refresh()
    {
        foreach (var child in _detail.GetChildren()) child.QueueFree();
        _grid.QueueRedraw();
        if (_selected < 0) return;
        var h = G.S.H;
        int s = _selected;
        var def = G.S.C.Slots[s];
        var it = h.Gear[s];
        bool open = h.Open[s];
        var face = Ui.Box(new Color("#100c16"), Look.Edge, 12).Place(0, 0, 470, 860);
        _detail.AddChild(face);
        float y = 20;
        var tile = new Canvas(q => ItemTile.Draw(q, new Rect2(0, 0, 128, 128), Describe.Family(def.Name), open && it != null ? it.Rarity : -1, it?.Affixes ?? 0,
            locked: !open, time: _t, tier: ItemTile.Tier(it))).Place(20, y, 128, 128);
        _detail.AddChild(tile);
        _detail.AddChild(Ui.Text(def.Name, Look.Sub, Look.Text, Look.Display).Wrapped().Place(164, y, 290, 80));
        _detail.AddChild(Ui.Text(Describe.Region(def), Look.Small, Look.Muted, Look.Ui).Place(164, y + 84, 290, 26));
        y += 150;
        if (!open)
        {
            _detail.AddChild(Ui.Text(def.UnlockDepth > 0 ? $"Opens when you first reach depth {Format.Whole(def.UnlockDepth)}." : "Opened by the transcendence tree's Celestial Slots.",
                Look.Body, Look.Muted, Look.Ui).Wrapped().Place(20, y, 430, 80));
            return;
        }
        if (it == null)
        {
            _detail.AddChild(Ui.Text("Nothing worn here yet. The next chest may have something.", Look.Body, Look.Muted, Look.Ui).Wrapped().Place(20, y, 430, 80));
        }
        else
        {
            _detail.AddChild(Ui.Text(Describe.Name(G.S.C, s, it), Look.Sub, Look.TierText(it.Rarity), Look.UiBold).Wrapped().Place(20, y, 430, 70));
            y += 70;
            _detail.AddChild(Ui.Text($"{Look.TierName(it.Rarity)} {Look.Numeral(it.Rarity)}  |  item level {Format.Whole((long)it.Ilvl)}  |  {Describe.Era(it.Ilvl)} era",
                Look.Small, Look.Muted, Look.Numbers).Place(20, y, 430, 26));
            y += 34;
            foreach (var line in Describe.Lines(h, s, it).Take(9))
            {
                _detail.AddChild(Ui.Text(line, Look.Body, Look.Text, Look.Ui).Place(20, y, 430, 28));
                y += 28;
            }
        }
        y = Math.Max(y + 16, 520);
        // Forge.
        if (G.S.Unlocked(Feature.Forge))
        {
            _detail.AddChild(Ui.Text($"Forge +{h.Forge[s]}", Look.Body, Look.Gold, Look.UiBold).Place(20, y, 200, 30));
            if (it != null)
            {
                var cost1 = G.S.ForgeCost(s, 1);
                int can = G.S.ForgeAffordable(s);
                var b1 = Ui.Button($"+1  ({Format.Num(cost1)})", () => { if (G.S.BuyForge(s, 1)) Bought(Look.Gold); }, Look.Gold, Look.Small).Place(170, y - 6, 150, 44);
                b1.Disabled = can < 1;
                _detail.AddChild(b1);
                var bm = Ui.Button(can > 1 ? $"+{can}" : "max", () => { if (G.S.BuyForge(s, G.S.ForgeAffordable(s))) Bought(Look.Gold); }, Look.Gold, Look.Small).Place(330, y - 6, 120, 44);
                bm.Disabled = can < 2;
                _detail.AddChild(bm);
            }
            y += 56;
        }
        // Rank.
        if (G.S.Unlocked(Feature.Ranks))
        {
            _detail.AddChild(Ui.Text($"Rank {h.Rank[s]}", Look.Body, Look.Shards, Look.UiBold).Place(20, y, 200, 30));
            double cost = h.RankCost(s);
            var b = Ui.Button($"Rank up  ({Format.Num(cost)} shards)", () => { if (G.S.RankUp(s)) Bought(Look.Shards); }, Look.Shards, Look.Small).Place(170, y - 6, 280, 44);
            b.Disabled = cost > h.Materials;
            _detail.AddChild(b);
            y += 56;
        }
        // Mastery.
        if (G.S.Unlocked(Feature.Mastery))
        {
            _detail.AddChild(Ui.Text($"Mastery {h.Mastery[s]}", Look.Body, Look.Xp, Look.UiBold).Place(20, y, 200, 30));
            var bar = new Bar(Look.Xp).Place(170, y + 6, 160, 14);
            bar.Set((float)Math.Clamp(h.MasteryXp[s] / Math.Max(1e-9, h.MasteryNeed(s)), 0, 1), true);
            _detail.AddChild(bar);
            var f = Ui.Button(h.Focus[s] ? "Focused" : $"Focus ({G.S.FocusCount}/{h.FocusSlots})", () => { G.S.ToggleFocus(s); Refresh(); }, h.Focus[s] ? Look.Xp : Look.EdgeLight, Look.Small)
                .Place(340, y - 6, 110, 44);
            f.Disabled = !h.Focus[s] && (it == null || G.S.FocusCount >= h.FocusSlots);
            _detail.AddChild(f);
            y += 56;
            _detail.AddChild(Ui.Text("Worn slots learn with time; focused ones much faster.", Look.Small, Look.Muted, Look.Ui).Wrapped().Place(20, y, 430, 50));
        }
    }

    void Bought(Color c)
    {
        Main.Instance?.Sfx?.PlayGroup("equip_metal", 1.1f, -8);
        G.Battle.Hero.Flash(c, 0.4f);
        Refresh();
    }

    public override void _Ready() => Main.Instance?.Moment("armoury_" + _kind.ToString().ToLowerInvariant(), 0.4);
}
