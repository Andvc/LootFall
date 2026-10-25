using System;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The codex (C): every first find, 149 slots by 12 rarities by 4 quality bands by 16 eras (114,432 entries), shown the
/// only way that size can be (docs/art-direction.md): how far each kind is, a heat map of rarity against era, and the
/// chosen kind's slots with a rarity bar each. What can't be found yet (later eras, rarer tiers) stays out of the counts,
/// so the first screen never reads "0.01%".
/// </summary>
public partial class CodexScreen : Overlay
{
    readonly Canvas _canvas;
    SlotKind _kind = SlotKind.Weapon;
    readonly int _eras, _rarities;

    public CodexScreen(GameScreen g) : base(g)
    {
        var p = Frame("Codex");
        var h = g.S.H;
        _eras = Math.Min(Hero.Eras, Hero.Era(h.EverDepth) + 1);
        _rarities = h.MaxRarity + 1;
        double power = h.CodexSheet[Stat.MoreDamage];
        p.AddChild(Ui.Text($"{Format.Whole(h.CodexCount)} first finds  |  {Describe.Line(Stat.MoreDamage, power, h.S)} and {Describe.Line(Stat.MoreHp, power, h.S)}",
            Look.Body, Look.Text, Look.UiBold).Place(32, 76, 1300, 34));
        p.AddChild(Ui.Text("Every slot, rarity, quality band and era is an entry; a first find of each adds to the codex's power. Rarer finds count for more.",
            Look.Small, Look.Muted, Look.Ui).Place(32, 108, 1500, 28));
        for (int k = 0; k < 8; k++)
        {
            var kind = (SlotKind)k;
            var b = Ui.Button(Describe.Kind(kind), () => { _kind = kind; _canvas.QueueRedraw(); }, size: Look.Body).Place(32, 156 + k * 62, 220, 52);
            p.AddChild(b);
        }
        _canvas = new Canvas(DrawCodex).Place(270, 150, 1470, 790);
        p.AddChild(_canvas);
    }

    (int Found, int Total) Count(Func<int, int, int, int, bool> where)
    {
        var h = G.S.H;
        int found = 0, total = 0;
        int slots = G.S.C.SlotCount;
        for (int era = 0; era < _eras; era++)
            for (int s = 0; s < slots; s++)
                for (int r = 0; r < _rarities; r++)
                    for (int b = 0; b < Hero.QualityBands; b++)
                    {
                        // Only open slots can be found in, so only they count toward what can be found by now.
                        if (!h.Open[s] || !where(era, s, r, b)) continue;
                        total++;
                        if (h.Codex[h.CodexKey(era, s, r, b)]) found++;
                    }
        return (found, total);
    }

    void DrawCodex(Canvas c)
    {
        var h = G.S.H;
        var slots = G.S.C.Slots;
        // Each kind's share of what can be found by now.
        c.DrawString(Look.UiBold, new Vector2(0, 18), "BY KIND (what can be found by now)", HorizontalAlignment.Left, -1, 18, Look.Muted);
        for (int k = 0; k < 8; k++)
        {
            var kind = (SlotKind)k;
            var (f, t) = Count((e, s, r, b) => slots[s].Kind == kind);
            float y = 40 + k * 34;
            c.DrawString(Look.UiBold, new Vector2(0, y + 20), Describe.Kind(kind), HorizontalAlignment.Left, 150, 18, kind == _kind ? Look.Gold : Look.Text);
            var bar = new Rect2(160, y + 6, 360, 18);
            c.DrawRect(bar, new Color("#1a1524"));
            if (t > 0) c.DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * f / t, bar.Size.Y)), new Color("#c48a22"));
            c.DrawString(Look.Numbers, new Vector2(530, y + 20), t > 0 ? $"{Format.Whole(f)} / {Format.Whole(t)}" : "-", HorizontalAlignment.Left, -1, 18, Look.Muted);
        }
        // Rarity against era.
        float hx = 760, hy = 40, cw = Math.Min(44, 660f / Math.Max(1, _eras)), ch = 26;
        c.DrawString(Look.UiBold, new Vector2(hx, 18), "RARITY BY ERA", HorizontalAlignment.Left, -1, 18, Look.Muted);
        for (int era = 0; era < _eras; era++)
            c.DrawString(Look.Ui, new Vector2(hx + 110 + era * cw, hy + 14), Describe.Eras[era][..Math.Min(4, Describe.Eras[era].Length)], HorizontalAlignment.Center, cw, 13, Look.Muted);
        for (int r = 0; r < _rarities; r++)
        {
            float y = hy + 24 + r * ch;
            c.DrawString(Look.UiBold, new Vector2(hx, y + 18), Look.TierName(r), HorizontalAlignment.Left, 104, 16, Look.TierText(r));
            for (int era = 0; era < _eras; era++)
            {
                var (f, t) = Count((e, s, rr, b) => e == era && rr == r);
                float share = t > 0 ? (float)f / t : 0;
                var cell = new Rect2(hx + 110 + era * cw, y + 2, cw - 4, ch - 4);
                c.DrawRect(cell, new Color("#1a1524"));
                if (share > 0) c.DrawRect(cell, new Color(Look.Tier(r), 0.15f + 0.85f * share));
            }
        }
        // The chosen kind's slots, a rarity bar each.
        float sy = 360;
        c.DrawString(Look.UiBold, new Vector2(0, sy), $"{Describe.Kind(_kind).ToUpperInvariant()}: EACH SLOT'S RARITIES FOUND (ANY ERA)", HorizontalAlignment.Left, -1, 18, Look.Muted);
        int row = 0;
        foreach (var s in slots.Where(s => s.Kind == _kind && h.Open[s.Index]))
        {
            int col = row / 12;
            float x = col * 490, y = sy + 20 + (row % 12) * 34;
            if (x > 1000) break;
            ItemTile.Draw(c, new Rect2(x, y, 28, 28), Describe.Family(s.Name), h.Gear[s.Index]?.Rarity ?? -1, numeral: false, tier: ItemTile.Tier(h.Gear[s.Index]));
            c.DrawString(Look.Ui, new Vector2(x + 36, y + 21), s.Name, HorizontalAlignment.Left, 190, 16, Look.Text);
            for (int r = 0; r < Look.Tiers; r++)
            {
                bool any = false;
                for (int era = 0; era < Hero.Eras && !any; era++)
                    for (int b = 0; b < Hero.QualityBands && !any; b++)
                        any = h.Codex[h.CodexKey(era, s.Index, r, b)];
                var cell = new Rect2(x + 230 + r * 20, y + 6, 16, 16);
                c.DrawRect(cell, r < _rarities ? new Color("#1a1524") : new Color("#120e18"));
                if (any) c.DrawRect(cell, Look.Tier(r));
            }
            row++;
        }
    }

    public override void _Ready() => Main.Instance?.Moment("codex", 0.4);
}
