using System;
using System.Collections.Generic;
using Godot;
using LootFall.Progression;
using Side = LootFall.Progression.Side;

namespace LootFall.View;

/// <summary>How each piece is drawn on the paper doll: a shape of its own for the ones you'd look for first (helms,
/// cloaks, gauntlets, boots, weapons, wings), and otherwise by the layer of each cell it fills (ink, cloth, mail, plate,
/// what's over, jewellery); what's set in a piece's board on top of it (gems, plumes, spikes, paint, enchantment).</summary>
public partial class PaperDoll
{
    static readonly Color[] ClothColors =
    {
        new("#6d4c3d"), new("#4f5d75"), new("#7a3b3b"), new("#3f5e4a"), new("#8a7a5a"), new("#5b4a72"), new("#3c4a5c"), new("#8c6d46"),
    };

    Item It(int s) => H.Gear[s];
    Color Rar(int s) => Look.Tier(H.Gear[s].Rarity);
    Ink.Ramp MetalOf(int s) => Ink.Metal(ItemTile.Tier(H.Gear[s]));

    Ink.Ramp ClothOf(int s, bool rich = false)
    {
        var d = C.Slots[s];
        var c = ClothColors[(int)((uint)d.Family.GetHashCode() % ClothColors.Length)];
        if (rich) c = c.Lerp(Rar(s), 0.25f);
        if (PaintOf(s) is Color paint) c = c.Lerp(paint, 0.6f);
        return new Ink.Ramp(c);
    }

    Ink.Ramp Plated(int s)
    {
        var m = MetalOf(s);
        return PaintOf(s) is Color paint ? new Ink.Ramp(m.Mid.Lerp(paint, 0.45f)) : m;
    }

    /// <summary>The colour a piece's paint (or heraldry) gives it, if it has one set.</summary>
    Color? PaintOf(int host)
    {
        if (!_modsOf.TryGetValue(host, out var mods)) return null;
        foreach (int m in mods)
            if (C.Slots[m].Family is "paint" or "heraldry") return Look.Tier(H.Gear[m].Rarity).Lerp(new Color("#7a2f2f"), 0.35f);
        return null;
    }

    bool Shows(string site) => Back ? !Figure.FrontOnly(site) : !Figure.BackOnly(site);

    // ------------------------------------------------------------------------------------------------ behind and around

    void DrawBackdrop()
    {
        foreach (int s in _pieces)
        {
            var d = C.Slots[s];
            switch (d.Family)
            {
                case "constellation":
                {
                    var at = Figure.Anchor(C.Cells[d.Takes[0]]);
                    var rng = new Random(d.Index);
                    Vector2 prev = at;
                    for (int i = 0; i < 5; i++)
                    {
                        var star = at + new Vector2(rng.Next(-40, 40), rng.Next(-30, 30));
                        if (i > 0) Seg(prev, star, new Color(Rar(s), 0.45f), 1.2f);
                        Ink.Glint(this, ToScreen(star), Mathf.Max(3, Px(5 + 2 * Mathf.Sin(_t * 2 + i))), Colors.White.Lerp(Rar(s), 0.4f));
                        prev = star;
                    }
                    break;
                }
                case "comet":
                {
                    var at = Figure.Anchor(C.Cells[d.Takes[0]]);
                    Seg(at, at + new Vector2(70, -40), new Color(Rar(s), 0.4f), 10);
                    Dot(at, 8, Colors.White.Lerp(Rar(s), 0.3f), false);
                    break;
                }
                case "eclipse":
                {
                    var at = Figure.Anchor(C.Cells[d.Takes[0]]);
                    Glow(at, 46, Rar(s));
                    Dot(at, 22, new Color("#0b0712"));
                    break;
                }
                case "aura":
                {
                    var c = Rar(s);
                    float pulse = 0.85f + 0.15f * Mathf.Sin(_t * 2);
                    for (int i = 4; i >= 1; i--)
                    {
                        var e = Ink.Ellipse(new Vector2(0, 520), 170 + 30 * i * pulse, 470 + 30 * i * pulse, 40);
                        DrawColoredPolygon(S(e), new Color(c, 0.05f));
                    }
                    break;
                }
            }
        }
    }

    /// <summary>What hangs behind the body: drawn before it from the front, over it from behind (<paramref name="over"/>).</summary>
    void DrawBehind(bool over)
    {
        if (Back != over) return;
        foreach (int s in _pieces)
        {
            var d = C.Slots[s];
            var r = Rar(s);
            switch (d.Family)
            {
                case "wings":
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var root = new Vector2(side * 40, 250);
                        float flap = 0.08f * Mathf.Sin(_t * 1.6f);
                        var tip = new Vector2(side * 300, 60 + 40 * flap * 10);
                        var wing = new[] { root, root + new Vector2(side * 120, -150), tip, tip + new Vector2(-side * 30, 90), root + new Vector2(side * 210, 150), root + new Vector2(side * 120, 220), root + new Vector2(side * 30, 60) };
                        Shade(wing, new Ink.Ramp(new Color("#d8d4e6").Lerp(r, 0.25f)));
                        for (int i = 1; i <= 3; i++) Seg(root + new Vector2(side * 20, 10), root + new Vector2(side * (90 + 50 * i), -40 + 60 * i), new Color(Look.Ink, 0.5f), 2.5f);
                        Edge(wing, r, 2, 0.94f);
                    }
                    DrawMods(s, new Vector2(0, 160), 120);
                    break;
                case "tail":
                {
                    float sway = 12 * Mathf.Sin(_t * 1.3f);
                    var pts = new List<Vector2>();
                    for (int i = 0; i <= 12; i++)
                    {
                        float t = i / 12f;
                        pts.Add(new Vector2(40 + 120 * t + sway * t, 480 + 300 * t - 160 * t * t));
                    }
                    for (int i = 0; i < pts.Count - 1; i++) Seg(pts[i], pts[i + 1], Look.Ink, 22 - 14 * i / 12f + 4);
                    for (int i = 0; i < pts.Count - 1; i++) Seg(pts[i], pts[i + 1], new Color("#6b4a3a").Lerp(r, 0.2f), 22 - 14 * i / 12f);
                    DrawMods(s, pts[^1], 30);
                    break;
                }
                case "cloak": case "cape":
                {
                    bool longOne = d.Family == "cloak";
                    var shape = new[] { new Vector2(-110, 220), new Vector2(110, 220), new Vector2(longOne ? 150 : 130, longOne ? 820 : 640), new Vector2(0, longOne ? 850 : 665), new Vector2(longOne ? -150 : -130, longOne ? 820 : 640) };
                    Shade(shape, ClothOf(s, true));
                    Edge(new[] { shape[2], shape[3], shape[4] }, r, 3, 1);
                    break;
                }
                case "robe":
                    Shade(new[] { new Vector2(-104, 220), new Vector2(104, 220), new Vector2(130, 740), new Vector2(-130, 740) }, ClothOf(s, true));
                    break;
                case "steed":
                {
                    var at = Figure.Anchor(C.Cells[d.Takes[0]]);
                    var body = Ink.Ellipse(at + new Vector2(0, -40), 60, 30, 20);
                    Shade(body, new Ink.Ramp(new Color("#6a4b36")));
                    Shade(new[] { at + new Vector2(40, -60), at + new Vector2(80, -110), at + new Vector2(96, -100), at + new Vector2(64, -40) }, new Ink.Ramp(new Color("#6a4b36")));
                    foreach (float x in new[] { -40f, -20f, 25f, 45f }) Seg(at + new Vector2(x, -20), at + new Vector2(x, 30), new Color("#4a3426"), 8);
                    Edge(body, r, 2, 0.9f);
                    DrawMods(s, at + new Vector2(0, -60), 40);
                    break;
                }
                case "shadow_cloak":
                {
                    var at = Figure.Anchor(C.Cells[d.Takes[0]]);
                    var shape = new[] { at + new Vector2(-30, 0), at + new Vector2(60, -150), at + new Vector2(90, -140), at + new Vector2(70, 0) };
                    DrawColoredPolygon(S(shape), new Color("#0d0816", 0.75f));
                    Edge(shape, new Color(r, 0.6f), 2, 1);
                    break;
                }
            }
            if (Back) continue;
            // From the front, what's carried on the back shows over the shoulders.
            switch (d.Family)
            {
                case "quiver":
                    for (int i = 0; i < 4; i++) Seg(new Vector2(60 + 8 * i, 230), new Vector2(80 + 10 * i, 150 - 4 * i), new Color("#8b6a45"), 4);
                    for (int i = 0; i < 4; i++) Fill(new[] { new Vector2(76 + 10 * i, 150 - 4 * i), new Vector2(84 + 10 * i, 140 - 4 * i), new Vector2(88 + 10 * i, 154 - 4 * i) }, r.Lerp(Colors.White, 0.4f));
                    break;
                case "back_scabbard":
                    Seg(new Vector2(-70, 230), new Vector2(-96, 120), Look.Ink, 12);
                    Seg(new Vector2(-70, 230), new Vector2(-96, 120), new Color("#5b4a3a"), 8);
                    Seg(new Vector2(-108, 128), new Vector2(-84, 116), Ink.Gold.Mid, 6);
                    break;
                case "war_banner":
                {
                    Seg(new Vector2(70, 240), new Vector2(110, -20), new Color("#6b4a2f"), 6);
                    var flag = new[] { new Vector2(108, -14), new Vector2(190, 6 + 6 * Mathf.Sin(_t * 3)), new Vector2(180, 60), new Vector2(104, 50) };
                    Shade(flag, ClothOf(s, true));
                    DrawMods(s, new Vector2(146, 24), 20);
                    break;
                }
                case "backpack":
                    foreach (float side in new[] { -1f, 1f }) Seg(new Vector2(side * 70, 214), new Vector2(side * 76, 330), new Color("#5a3d2a"), 8);
                    break;
            }
        }
    }

    void DrawFront()
    {
        foreach (int s in _pieces)
        {
            var d = C.Slots[s];
            var r = Rar(s);
            switch (d.Family)
            {
                case "halo": case "starcrown":
                {
                    var ring = Ink.Ellipse(new Vector2(0, 14), 58, 13, 32);
                    var pts = S(ring);
                    var closed = new Vector2[pts.Length + 1];
                    Array.Copy(pts, closed, pts.Length);
                    closed[^1] = pts[0];
                    DrawPolyline(closed, new Color(r, 0.35f), Px(14), true);
                    DrawPolyline(closed, Colors.White.Lerp(r, 0.5f), Mathf.Max(2, Px(5)), true);
                    if (d.Family == "starcrown")
                        for (int i = 0; i < 7; i++)
                        {
                            float a = Mathf.Pi + Mathf.Pi * i / 6;
                            Ink.Glint(this, ToScreen(new Vector2(Mathf.Cos(a) * 58, 14 + Mathf.Sin(a) * 13 - 10)), Mathf.Max(3, Px(9)), Colors.White.Lerp(r, 0.3f));
                        }
                    DrawMods(s, new Vector2(0, 14), 50, ring: true);
                    break;
                }
                case "ioun_stone":
                {
                    int i = C.Cells[d.Takes[0]].Key[^1] - '1';
                    float a = _t * 0.8f + i * Mathf.Tau / 6;
                    var at = new Vector2(0, 92) + new Vector2(Mathf.Cos(a) * 100, Mathf.Sin(a) * 30 - 10);
                    Glow(at, 18, r);
                    Fill(new[] { at + new Vector2(0, -9), at + new Vector2(7, 0), at + new Vector2(0, 9), at + new Vector2(-7, 0) }, r.Lerp(Colors.White, 0.2f));
                    break;
                }
                case "familiar":
                {
                    var at = Figure.Anchor(C.Cells[d.Takes[0]]);
                    float hop = Mathf.Abs(Mathf.Sin(_t * 2.4f)) * 6;
                    var body = Ink.Ellipse(at + new Vector2(0, -18 - hop), 26, 18, 16);
                    Shade(body, new Ink.Ramp(new Color("#c27a3e")));
                    Fill(Ink.Ellipse(at + new Vector2(-20, -40 - hop), 14, 13, 12), new Color("#c27a3e"));
                    Fill(new[] { at + new Vector2(-28, -50 - hop), at + new Vector2(-24, -66 - hop), at + new Vector2(-18, -52 - hop) }, new Color("#c27a3e"));
                    Fill(new[] { at + new Vector2(-14, -52 - hop), at + new Vector2(-8, -66 - hop), at + new Vector2(-6, -50 - hop) }, new Color("#c27a3e"));
                    Dot(at + new Vector2(-24, -42 - hop), 2.2f, Look.Ink, false);
                    Edge(body, r, 2, 1);
                    DrawMods(s, at + new Vector2(0, -24 - hop), 22);
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ one piece

    void DrawPiece(int s)
    {
        var d = C.Slots[s];
        if (d.Board != Anatomy.Body) return;
        if (!Special(s, d))
            foreach (int c in d.Takes) Generic(s, d, C.Cells[c]);
        if (_modsOf.ContainsKey(s) && !ModsDrawn(d.Family))
        {
            var shape = Figure.Site(C.Cells[d.Takes[0]].Site);
            DrawMods(s, shape != null ? Ink.Centre(shape) : Figure.Anchor(C.Cells[d.Takes[0]]), shape != null ? 18 : 10);
        }
    }

    static bool ModsDrawn(string family) => family is "wings" or "tail" or "steed" or "familiar" or "halo" or "starcrown" or "war_banner"
        or "great_helm" or "sallet" or "nasal_helm" or "horned_helm" or "main_hand" or "great_weapon" or "shield" or "belt" or "girdle" or "cloak" or "cape"
        or "breastplate" or "cuirass" or "brigandine" or "full_plate_harness" or "tabard" or "surcoat" or "robe" or "gauntlet" or "claw_gauntlet" or "pauldron"
        or "spaulder" or "full_arm_harness" or "boot" or "armoured_boot" or "horns" or "grafted_horns";

    void Generic(int s, SlotDef d, Cell cell)
    {
        if (!Shows(cell.Site)) return;
        var shape = Figure.Site(cell.Site);
        var r = Rar(s);
        switch (cell.Layer)
        {
            case Layer.Skin:
                if (shape != null) Tattoo(shape, r, d.Index);
                break;
            case Layer.Under:
                if (shape == null) break;
                Shade(Inflate(shape, 1.05f), ClothOf(s));
                Edge(Inflate(shape, 1.05f), new Color(r, 0.8f), 1.5f, 0.92f);
                break;
            case Layer.Mail:
                if (shape != null) Mail(Inflate(shape, 1.07f), s);
                break;
            case Layer.Armor:
                if (shape == null) break;
                Shade(Inflate(shape, 1.1f), Plated(s));
                Edge(Inflate(shape, 1.1f), r, 0, 0.86f);
                break;
            case Layer.Over:
                if (shape == null) break;
                Shade(Inflate(shape, 1.14f), ClothOf(s, true));
                Edge(Inflate(shape, 1.14f), r, 2, 0.95f);
                break;
            case Layer.Adorn:
                Jewel(cell, r, d.Family);
                break;
            case Layer.Hold:
                Weapon(s, cell, Figure.Anchor(cell), 1);
                break;
            case Layer.Graft:
                Glow(Figure.Anchor(cell), 16, r);
                Dot(Figure.Anchor(cell), 8, r);
                break;
            case Layer.Beyond:
                Glow(Figure.Anchor(cell), 20, r);
                Dot(Figure.Anchor(cell), 6, r.Lerp(Colors.White, 0.3f));
                break;
        }
    }

    void Tattoo(Vector2[] shape, Color rarity, int seed)
    {
        var c = Ink.Centre(shape);
        float size = 0;
        foreach (var p in shape) size = Mathf.Max(size, p.DistanceTo(c));
        var rng = new Random(seed);
        var ink = new Color("#1d2440", 0.75f);
        for (int i = 0; i < 3; i++)
        {
            var a = c + new Vector2((float)(rng.NextDouble() - 0.5) * size, (float)(rng.NextDouble() - 0.5) * size) * 0.9f;
            var b = a + new Vector2((float)(rng.NextDouble() - 0.5) * size, (float)(rng.NextDouble() - 0.5) * size) * 0.8f;
            var m = (a + b) / 2 + new Vector2(size * 0.15f, -size * 0.1f);
            Seg(a, m, ink, size * 0.06f);
            Seg(m, b, ink, size * 0.06f);
        }
        Seg(c - new Vector2(size * 0.3f, 0), c + new Vector2(size * 0.3f, 0), new Color(rarity, 0.6f), size * 0.03f);
    }

    void Mail(Vector2[] shape, int s)
    {
        var steel = MetalOf(s);
        Fill(shape, steel.Shadow.Lerp(steel.Mid, 0.4f));
        // Rows of little rings.
        var c = Ink.Centre(shape);
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in shape)
        {
            minX = Mathf.Min(minX, p.X);
            maxX = Mathf.Max(maxX, p.X);
            minY = Mathf.Min(minY, p.Y);
            maxY = Mathf.Max(maxY, p.Y);
        }
        float step = Mathf.Max(5, 8 / Mathf.Max(1, Zoom * 0.5f));
        int rows = 0;
        for (float y = minY + step / 2; y < maxY && rows < 60; y += step, rows++)
            for (float x = minX + step / 2 + (rows % 2) * step / 2; x < maxX; x += step)
            {
                var p = new Vector2(x, y);
                if (!Geometry2D.IsPointInPolygon(p, shape)) continue;
                var q = ToScreen(p);
                DrawArc(q, Mathf.Max(1.2f, Px(step * 0.38f)), 0, Mathf.Pi, 5, steel.Light, 1, true);
            }
        Edge(shape, Rar(s), 1.5f, 0.92f);
        _ = c;
    }

    /// <summary>Jewellery: a band round a finger, toe, wrist, ankle or arm (across the limb), a stud or ring elsewhere.</summary>
    void Jewel(Cell cell, Color rarity, string family)
    {
        var at = Figure.Anchor(cell);
        var dir = LimbDir(cell);
        var gold = Ink.Gold;
        if (dir != Vector2.Zero)
        {
            var across = new Vector2(-dir.Y, dir.X);
            float half = BandHalf(cell);
            Seg(at - across * half, at + across * half, Look.Ink, half * 0.7f + 2.4f);
            Seg(at - across * half, at + across * half, gold.Light, half * 0.7f);
            Dot(at + across * half * 0.2f, Mathf.Max(1.2f, half * 0.35f), rarity, false);
            return;
        }
        switch (family)
        {
            case "necklace": case "choker": case "torc": case "chain_of_office":
            {
                float y = family == "choker" ? 196 : family == "torc" ? 206 : 236;
                float w = family == "choker" ? 22 : family == "torc" ? 30 : family == "chain_of_office" ? 70 : 44;
                var prev = new Vector2(-w, y - 24);
                for (int i = 1; i <= 10; i++)
                {
                    float t = i / 10f;
                    var p = new Vector2(-w + 2 * w * t, y - 24 + 30 * Mathf.Sin(Mathf.Pi * t));
                    Seg(prev, p, family == "torc" ? gold.Mid : gold.Light, family == "torc" || family == "chain_of_office" ? 5 : 2.4f);
                    prev = p;
                }
                if (family == "chain_of_office") Dot(new Vector2(0, y + 10), 10, gold.Mid);
                break;
            }
            case "amulet":
                Seg(new Vector2(-30, 214), new Vector2(0, 262), gold.Light, 2);
                Seg(new Vector2(30, 214), new Vector2(0, 262), gold.Light, 2);
                Fill(new[] { new Vector2(0, 254), new Vector2(10, 268), new Vector2(0, 284), new Vector2(-10, 268) }, rarity);
                break;
            case "circlet": case "diadem":
                Seg(new Vector2(-48, 82), new Vector2(48, 82), Look.Ink, 7);
                Seg(new Vector2(-48, 82), new Vector2(48, 82), gold.Light, 4.5f);
                if (family == "diadem") Fill(new[] { new Vector2(-12, 80), new Vector2(0, 62), new Vector2(12, 80) }, gold.Mid);
                Dot(new Vector2(0, 80), 4.5f, rarity);
                break;
            case "belt":
                break;
            default:
            {
                // Hoops for ear lobes and lips, studs elsewhere.
                bool hoop = cell.Key.EndsWith(".lobe") || cell.Key.EndsWith(".lip") || cell.Key.EndsWith("septum") || cell.Key.EndsWith(".helix");
                if (hoop)
                {
                    var p = ToScreen(at + new Vector2(0, 4));
                    DrawArc(p, Mathf.Max(2.5f, Px(5)), 0, Mathf.Tau, 12, Look.Ink, Mathf.Max(2, Px(3.4f)), true);
                    DrawArc(p, Mathf.Max(2.5f, Px(5)), 0, Mathf.Tau, 12, gold.Light, Mathf.Max(1.2f, Px(2)), true);
                }
                Dot(at, 3.4f, rarity);
                break;
            }
        }
    }

    float BandHalf(Cell cell)
    {
        string b = cell.Site.Length > 2 && cell.Site[1] == '.' ? cell.Site[2..] : cell.Site;
        return b switch
        {
            "wrist" => 18, "ankle" => 19, "upperarm" => 24, "thigh" => 30, "neck" => 21,
            "bigtoe" or "toe2" or "toe3" or "toe4" or "littletoe" => 5,
            _ => 6,
        };
    }

    /// <summary>The way the limb runs at a cell (for bands across it), or zero for a cell that isn't on a limb.</summary>
    Vector2 LimbDir(Cell cell)
    {
        float s = Figure.SideOf(cell.Site);
        string b = s != 0 ? cell.Site[2..] : cell.Site;
        int f = Array.IndexOf(Figure.Fingers, b);
        if (f >= 0)
        {
            var h = Figure.Hand(s);
            return (h.Tip[f] - h.Base[f]).Normalized();
        }
        if (Array.IndexOf(Figure.ToeKeys, b) >= 0) return Vector2.Down;
        return b switch
        {
            "wrist" => Figure.Hand(s).Dir,
            "ankle" => Vector2.Down,
            "upperarm" => (Figure.Elbow(s) - Figure.Shoulder(s)).Normalized(),
            "thigh" => (Figure.Knee(s) - Figure.Hip(s)).Normalized(),
            _ => Vector2.Zero,
        };
    }

    // ------------------------------------------------------------------------------------------------ pieces with a shape of their own

    bool Special(int s, SlotDef d)
    {
        var r = Rar(s);
        float side = d.Side == Side.Right ? -1 : d.Side == Side.Left ? 1 : 0;
        switch (d.Family)
        {
            // What's drawn behind or around the body has been drawn.
            case "wings": case "tail": case "cloak": case "cape": case "robe": case "steed": case "shadow_cloak": case "quiver": case "back_scabbard":
            case "war_banner": case "backpack": case "halo": case "starcrown": case "ioun_stone": case "familiar": case "constellation": case "comet":
            case "eclipse": case "aura":
                if (d.Family is "cloak" or "cape" && !Back) Mantle(s, d.Family == "cloak");
                if (d.Family == "robe") Robe(s);
                return true;
            case "great_helm": case "horned_helm": GreatHelm(s, d.Family == "horned_helm"); return true;
            case "sallet": Sallet(s); return true;
            case "nasal_helm": NasalHelm(s); return true;
            case "horns": Horns(s, Rar(s), new Color("#e8dcc0")); DrawHelmMods(s, true); return true;
            case "coif":
                if (Back) Mail(Figure.HeadShape(), s);
                else foreach (var half in Ring(new Vector2(0, 98), 60, 74, new Vector2(0, 116), 36, 46)) Mail(half, s);
                return true;
            case "arming_cap": Shade(Figure.Site("scalp"), ClothOf(s)); return true;
            case "hood":
                if (Back) Shade(Ink.Ellipse(new Vector2(0, 98), 64, 80, 28), ClothOf(s, true));
                else
                {
                    ShadeRing(Ring(new Vector2(0, 92), 64, 80, new Vector2(0, 114), 38, 48), ClothOf(s, true));
                    Edge(Ink.Ellipse(new Vector2(0, 114), 38, 48, 24), r, 2, 1);
                }
                return true;
            case "wizard_hat":
            {
                var cloth = ClothOf(s, true);
                Shade(new[] { new Vector2(-50, 50), new Vector2(50, 50), new Vector2(42 + 6 * Mathf.Sin(_t), -90), new Vector2(30, -96) }, cloth);
                Shade(Ink.Ellipse(new Vector2(0, 52), 86, 15, 28), cloth);
                Edge(Ink.Ellipse(new Vector2(0, 52), 86, 15, 28), r, 2, 0.8f);
                return true;
            }
            case "crown":
            {
                var gold = Ink.Gold;
                var band = new[] { new Vector2(-50, 40), new Vector2(50, 40), new Vector2(52, 62), new Vector2(-52, 62) };
                for (int i = 0; i < 5; i++)
                {
                    float x = -44 + 22 * i;
                    Fill(new[] { new Vector2(x - 10, 42), new Vector2(x, 10), new Vector2(x + 10, 42) }, gold.Light);
                }
                Shade(band, gold);
                DrawMods(s, new Vector2(0, 50), 34);
                return true;
            }
            case "third_eye":
                if (Back) return true;
                Fill(Ink.Ellipse(new Vector2(0, 74), 7, 10, 14), new Color("#f4efe6"));
                Dot(new Vector2(0, 74), 4, r);
                return true;
            case "spectacles": case "goggles":
            {
                if (Back) return true;
                bool g = d.Family == "goggles";
                var lens = new Color("#cfe8ff", 0.35f);
                if (_modsOf.TryGetValue(s, out var mods))
                    foreach (int m in mods)
                        if (C.Slots[m].Family == "lens_tint") lens = new Color(Look.Tier(H.Gear[m].Rarity), 0.45f);
                if (g) Seg(new Vector2(-52, 100), new Vector2(52, 100), new Color("#4a3426"), 6);
                foreach (float x in new[] { -19f, 19f })
                {
                    var p = ToScreen(new Vector2(x, 102));
                    float rad = Px(g ? 14 : 11);
                    DrawCircle(p, rad, lens, true, -1, true);
                    DrawArc(p, rad, 0, Mathf.Tau, 20, Look.Ink, Mathf.Max(2, Px(g ? 5 : 3)), true);
                    DrawArc(p, rad, 0, Mathf.Tau, 20, g ? Ink.Gold.Mid : Ink.Gold.Light, Mathf.Max(1.2f, Px(g ? 3 : 1.6f)), true);
                }
                Seg(new Vector2(-8, 100), new Vector2(8, 100), Ink.Gold.Light, 2);
                return true;
            }
            case "monocle":
            {
                if (Back) return true;
                var p = ToScreen(new Vector2(-19, 102));
                DrawCircle(p, Px(12), new Color("#cfe8ff", 0.35f), true, -1, true);
                DrawArc(p, Px(12), 0, Mathf.Tau, 20, Ink.Gold.Light, Mathf.Max(1.5f, Px(2.5f)), true);
                Seg(new Vector2(-19, 114), new Vector2(-34, 150), Ink.Gold.Light, 1.5f);
                return true;
            }
            case "eyepatch":
                if (Back) return true;
                Seg(new Vector2(-50, 76), new Vector2(50, 116), Look.Ink, 3);
                Fill(Ink.Ellipse(new Vector2(19, 102), 13, 10, 14), new Color("#1d1620"));
                return true;
            case "mask":
                if (Back) return true;
                Shade(new[] { new Vector2(-46, 90), new Vector2(46, 90), new Vector2(40, 140), new Vector2(0, 162), new Vector2(-40, 140) }, ClothOf(s, true));
                foreach (float x in new[] { -19f, 19f }) Fill(Ink.Ellipse(new Vector2(x, 102), 9, 6, 12), new Color("#140d18"));
                DrawMods(s, new Vector2(0, 126), 16);
                return true;
            case "kohl":
                if (Back) return true;
                Fill(Ink.Ellipse(new Vector2(side * 19, 102), 12, 8, 14), new Color("#1d1620", 0.55f), false);
                return true;
            case "war_paint":
                if (Back) return true;
                foreach (float x in new[] { -1f, 1f })
                    for (int i = 0; i < 2; i++) Seg(new Vector2(x * 22, 118 + 8 * i), new Vector2(x * 44, 114 + 8 * i), r.Darkened(0.2f), 4);
                Seg(new Vector2(-14, 80), new Vector2(14, 80), r.Darkened(0.2f), 4);
                return true;
            case "fang_caps":
                if (Back) return true;
                foreach (float x in new[] { -7f, 7f }) Fill(new[] { new Vector2(x - 3, 143), new Vector2(x + 3, 143), new Vector2(x, 154) }, new Color("#f4efe6"));
                return true;
            case "ear_tip":
                Fill(new[] { new Vector2(side * 56, 92), new Vector2(side * 74, 62), new Vector2(side * 62, 98) }, Ink.Skin.Mid.Lerp(r, 0.25f));
                return true;
            case "nose_chain":
                if (Back) return true;
                Seg(new Vector2(-7, 124), new Vector2(-40, 132), Ink.Gold.Light, 1.4f);
                Seg(new Vector2(-40, 132), new Vector2(-54, 118), Ink.Gold.Light, 1.4f);
                Dot(new Vector2(-7, 124), 2.5f, r);
                return true;
            case "ear_chain":
                Seg(new Vector2(side * 54, 118), new Vector2(side * 60, 96), Ink.Gold.Light, 1.4f);
                Dot(new Vector2(side * 54, 118), 2.5f, r);
                return true;
            case "hand_chain":
            {
                var h = Figure.Hand(side);
                Seg(h.Along(2, 0.3f), h.Centre, Ink.Gold.Light, 1.2f);
                Seg(h.Centre, h.Wrist - h.Dir * 5, Ink.Gold.Light, 1.2f);
                Dot(h.Centre, 3, r);
                return true;
            }
            case "foot_chain":
            {
                var (_, toes, _) = Figure.Foot(side);
                Seg(toes[1], Figure.Ankle(side) + new Vector2(0, 30), Ink.Gold.Light, 1.2f);
                Seg(Figure.Ankle(side) + new Vector2(-14, 4), Figure.Ankle(side) + new Vector2(14, 4), Ink.Gold.Light, 1.6f);
                Dot(Figure.Ankle(side) + new Vector2(0, 30), 3, r);
                return true;
            }
            case "bevor":
                if (Back) return true;
                Shade(new[] { new Vector2(-40, 134), new Vector2(40, 134), new Vector2(30, 214), new Vector2(-30, 214) }, Plated(s));
                Edge(new[] { new Vector2(-40, 134), new Vector2(40, 134), new Vector2(30, 214), new Vector2(-30, 214) }, r, 0, 0.86f);
                return true;
            case "gorget":
            {
                var shape = new[] { new Vector2(-26, 180), new Vector2(26, 180), new Vector2(60, 222), new Vector2(-60, 222) };
                Shade(shape, Plated(s));
                Edge(shape, r, 0, 0.85f);
                return true;
            }
            case "scarf":
                Shade(new[] { new Vector2(-30, 190), new Vector2(30, 190), new Vector2(34, 214), new Vector2(-34, 214) }, ClothOf(s, true));
                Shade(new[] { new Vector2(10, 206), new Vector2(26, 206), new Vector2(34, 270), new Vector2(18, 270) }, ClothOf(s, true));
                return true;
            case "breastplate": case "cuirass": case "brigandine": case "full_plate_harness": case "plackart":
                Plate(s, d);
                return true;
            case "tabard": case "surcoat":
            {
                if (Back) return true;
                bool longOne = d.Family == "surcoat";
                var shape = new[] { new Vector2(-62, 214), new Vector2(62, 214), new Vector2(66, longOne ? 600 : 520), new Vector2(-66, longOne ? 600 : 520) };
                Shade(shape, ClothOf(s, true));
                Edge(shape, r, 3, 0.94f);
                DrawMods(s, new Vector2(0, 300), 30);
                return true;
            }
            case "mantle":
                Mantle(s, false);
                return true;
            case "belt": case "girdle": case "sash":
            {
                var band = new[] { new Vector2(-82, 424), new Vector2(82, 424), new Vector2(86, d.Family == "girdle" ? 462 : 448), new Vector2(-86, d.Family == "girdle" ? 462 : 448) };
                Shade(band, d.Family == "girdle" ? Plated(s) : d.Family == "sash" ? ClothOf(s, true) : Ink.Leather);
                if (d.Family != "sash") Fill(new[] { new Vector2(-10, 426), new Vector2(10, 426), new Vector2(10, 446), new Vector2(-10, 446) }, Ink.Gold.Light);
                else Shade(new[] { new Vector2(40, 440), new Vector2(56, 440), new Vector2(62, 510), new Vector2(46, 510) }, ClothOf(s, true));
                Edge(band, r, 1.5f, 0.9f);
                DrawBeltMods(s);
                return true;
            }
            case "loincloth":
                if (Back) return true;
                Shade(new[] { new Vector2(-30, 448), new Vector2(30, 448), new Vector2(34, 600), new Vector2(-34, 600) }, ClothOf(s, true));
                return true;
            case "scabbard":
            {
                var hip = new Vector2(side * 84, 470);
                Seg(hip, hip + new Vector2(side * 30, 150), Look.Ink, 14);
                Seg(hip, hip + new Vector2(side * 30, 150), new Color("#5b4a3a"), 10);
                Seg(hip + new Vector2(-side * 10, -10), hip + new Vector2(side * 10, 6), Ink.Gold.Mid, 6);
                Seg(hip + new Vector2(0, -2), hip + new Vector2(-side * 6, -30), new Color("#3b2a20"), 6);
                return true;
            }
            case "thigh_sheath":
            {
                var at = Figure.Hip(side).Lerp(Figure.Knee(side), 0.5f) + new Vector2(side * 24, 0);
                Seg(at + new Vector2(0, -40), at + new Vector2(0, 40), Look.Ink, 10);
                Seg(at + new Vector2(0, -40), at + new Vector2(0, 40), new Color("#5b4a3a"), 7);
                Seg(at + new Vector2(-30, -10), at + new Vector2(30, -10), new Color("#4a3426"), 4);
                return true;
            }
            case "wrist_blade":
            {
                var w = Figure.Wrist(side);
                var e = Figure.Elbow(side);
                Seg(e.Lerp(w, 0.3f) + new Vector2(side * 18, 0), w + (w - e).Normalized() * 50 + new Vector2(side * 16, 0), Look.Ink, 7);
                Seg(e.Lerp(w, 0.3f) + new Vector2(side * 18, 0), w + (w - e).Normalized() * 50 + new Vector2(side * 16, 0), MetalOf(s).Light, 4);
                return true;
            }
            case "shoulder_familiar":
            {
                var at = Figure.Shoulder(side) + new Vector2(side * 6, -44);
                var body = Ink.Ellipse(at, 16, 22, 14);
                Shade(body, new Ink.Ramp(new Color("#6b5f52")));
                Fill(new[] { at + new Vector2(-side * 10, -16), at + new Vector2(-side * 26, -10), at + new Vector2(-side * 10, -6) }, Ink.Gold.Mid);
                Dot(at + new Vector2(-side * 4, -12), 2.2f, Look.Ink, false);
                Edge(body, r, 1.5f, 1);
                DrawMods(s, at, 14);
                return true;
            }
            case "shoulder_lantern":
            {
                var at = Figure.Shoulder(side) + new Vector2(side * 20, -30);
                Seg(at, at + new Vector2(0, -24), Look.Ink, 2);
                Glow(at + new Vector2(0, 8), 26, new Color("#ffcf6b"));
                Fill(new[] { at + new Vector2(-8, 0), at + new Vector2(8, 0), at + new Vector2(8, 18), at + new Vector2(-8, 18) }, new Color("#ffe7a8"));
                return true;
            }
            case "glove": case "fingerless_glove":
            {
                if (side == 0) return false;
                var leather = ClothOf(s, false);
                DrawHand(side, Ink.Leather.Mid.Lerp(leather.Mid, 0.3f), Ink.Leather.Shadow, d.Family == "glove" ? 1 : 0.38f);
                Edge(Figure.Hand(side).Palm, r, 1.5f, 0.85f);
                return true;
            }
            case "gauntlet": case "claw_gauntlet":
            {
                if (side == 0) return false;
                var metal = Plated(s);
                var h = Figure.Hand(side);
                Shade(Ink.Capsule(h.Wrist - h.Dir * 24, h.Wrist + h.Dir * 4, 21, 19, 5), metal);
                for (int f = 0; f < 5; f++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var a = h.Along(f, k / 3f);
                        var b = h.Along(f, (k + 1) / 3f + 0.05f);
                        Fill(Ink.Capsule(a, b, h.Width[f] + 1.5f, h.Width[f] + 1.2f, 3), k % 2 == 0 ? metal.Mid : metal.Light.Lerp(metal.Mid, 0.5f));
                    }
                    if (d.Family == "claw_gauntlet")
                    {
                        var tip = h.Tip[f];
                        var dir = (h.Tip[f] - h.Base[f]).Normalized();
                        Fill(new[] { tip - new Vector2(-dir.Y, dir.X) * 4, tip + dir * 22, tip + new Vector2(-dir.Y, dir.X) * 4 }, new Color("#e8e2d6"));
                    }
                }
                Shade(h.Palm, metal);
                Edge(h.Palm, r, 0, 0.82f);
                DrawGauntletMods(s, side);
                return true;
            }
            case "hand_plate":
            {
                var h = Figure.Hand(side);
                var plate = Ink.Scale(h.Palm, Ink.Centre(h.Palm), 0.86f, Vector2.Zero);
                Shade(plate, Plated(s));
                Edge(plate, r, 0, 0.8f);
                return true;
            }
            case "talons": case "toe_talons":
            {
                if (d.Family == "talons")
                {
                    var h = Figure.Hand(side);
                    for (int f = 0; f < 5; f++)
                    {
                        var dir = (h.Tip[f] - h.Base[f]).Normalized();
                        var n = new Vector2(-dir.Y, dir.X);
                        Fill(new[] { h.Tip[f] - n * 4, h.Tip[f] + dir * 18, h.Tip[f] + n * 4 }, new Color("#2b2233"));
                    }
                }
                else
                {
                    var (_, toes, radii) = Figure.Foot(side);
                    for (int t = 0; t < 5; t++) Fill(new[] { toes[t] + new Vector2(-radii[t] * 0.6f, 2), toes[t] + new Vector2(0, radii[t] + 10), toes[t] + new Vector2(radii[t] * 0.6f, 2) }, new Color("#2b2233"));
                }
                return true;
            }
            case "nail_lacquer": case "toenail_paint":
            {
                if (d.Family == "nail_lacquer")
                {
                    var h = Figure.Hand(side);
                    for (int f = 0; f < 5; f++) Dot(h.Along(f, 0.92f), h.Width[f] * 0.65f, r);
                }
                else
                {
                    var (_, toes, radii) = Figure.Foot(side);
                    for (int t = 0; t < 5; t++) Dot(toes[t] + new Vector2(0, radii[t] * 0.4f), radii[t] * 0.5f, r);
                }
                return true;
            }
            case "thimble":
            {
                var h = Figure.Hand(side);
                Fill(Ink.Capsule(h.Along(2, 0.82f), h.Tip[2], h.Width[2] + 1, h.Width[2] + 1, 3), MetalOf(s).Light);
                return true;
            }
            case "knuckleduster":
            {
                var h = Figure.Hand(side);
                Seg(h.Along(1, 0.28f), h.Along(4, 0.28f), Look.Ink, 9);
                Seg(h.Along(1, 0.28f), h.Along(4, 0.28f), MetalOf(s).Light, 6);
                for (int f = 1; f < 5; f++) Dot(h.Along(f, 0.28f), 4, MetalOf(s).Mid);
                return true;
            }
            case "palm_crystal":
                Glow(Figure.SiteAnchor((side < 0 ? "R" : "L") + ".palm"), 18, r);
                return true;
            case "knuckle_letters":
            {
                var h = Figure.Hand(side);
                for (int f = 1; f < 5; f++) Seg(h.Along(f, 0.4f), h.Along(f, 0.52f), new Color("#1d2440", 0.8f), 3);
                return true;
            }
            case "heartstone":
                Glow(new Vector2(26, 258), 26, r);
                Fill(new[] { new Vector2(26, 246), new Vector2(36, 258), new Vector2(26, 272), new Vector2(16, 258) }, r);
                return true;
            case "main_hand": case "great_weapon": case "off_hand": case "shield": case "focus_orb": case "torch":
                Weapon(s, C.Cells[d.Takes[0]], Figure.SiteAnchor(C.Cells[d.Takes[0]].Site), 1);
                return true;
            case "boot": case "armoured_boot": case "sabaton": case "sandal": case "sock":
                Footwear(s, d, side);
                return true;
            case "pauldron": case "spaulder": case "full_arm_harness":
            {
                if (side == 0) return false;
                float big = d.Family == "spaulder" ? 0.9f : 1.15f;
                var dome = Ink.Ellipse(Figure.Shoulder(side) + new Vector2(side * 2, -6), 34 * big, 30 * big, 22);
                if (d.Family == "full_arm_harness")
                    foreach (string site in new[] { "upperarm", "elbow", "forearm", "armpit" })
                    {
                        var sh = Inflate(Figure.Site((side < 0 ? "R." : "L.") + site), 1.1f);
                        Shade(sh, Plated(s));
                        Edge(sh, r, 0, 0.86f);
                    }
                Shade(dome, Plated(s));
                for (int i = 1; i <= 2; i++) Edge(Ink.Ellipse(Figure.Shoulder(side) + new Vector2(side * 2, -6 + 10 * i), 30 * big, 18 * big, 16), new Color(Look.Ink, 0.4f), 1.5f, 1);
                Edge(dome, r, 0, 0.86f);
                DrawPauldronMods(s, side, big);
                return true;
            }
        }
        return false;
    }

    /// <summary>A ring between two ellipses as its two halves, each a plain outline (the fill takes no holes).</summary>
    static Vector2[][] Ring(Vector2 outer, float orx, float ory, Vector2 inner, float irx, float iry, int n = 16)
    {
        var halves = new Vector2[2][];
        for (int h = 0; h < 2; h++)
        {
            var pts = new List<Vector2>();
            float from = -Mathf.Pi / 2 + h * Mathf.Pi;
            for (int i = 0; i <= n; i++)
            {
                float t = from + Mathf.Pi * i / n;
                pts.Add(outer + new Vector2(Mathf.Cos(t) * orx, Mathf.Sin(t) * ory));
            }
            for (int i = n; i >= 0; i--)
            {
                float t = from + Mathf.Pi * i / n;
                pts.Add(inner + new Vector2(Mathf.Cos(t) * irx, Mathf.Sin(t) * iry));
            }
            halves[h] = pts.ToArray();
        }
        return halves;
    }

    void ShadeRing(Vector2[][] halves, Ink.Ramp r)
    {
        foreach (var half in halves) Fill(half, r.Mid);
    }

    // ------------------------------------------------------------------------------------------------ helms

    void GreatHelm(int s, bool horned)
    {
        var metal = Plated(s);
        var r = Rar(s);
        var shell = new[]
        {
            new Vector2(-56, 160), new Vector2(-58, 70), new Vector2(-44, 36), new Vector2(0, 26), new Vector2(44, 36), new Vector2(58, 70), new Vector2(56, 160),
            new Vector2(30, 176), new Vector2(-30, 176),
        };
        if (horned) Horns(s, r, new Color("#e8dcc0"));
        Shade(shell, metal);
        if (!Back)
        {
            Seg(new Vector2(-40, 100), new Vector2(40, 100), Look.Ink, 9);
            Seg(new Vector2(0, 40), new Vector2(0, 170), new Color(metal.Light, 0.5f), 3);
            for (int i = 0; i < 4; i++) Dot(new Vector2(22 + (i % 2) * 8, 126 + 9 * (i / 2)), 1.8f, Look.Ink, false);
        }
        Seg(new Vector2(-57, 86), new Vector2(57, 86), r, 3);
        Edge(shell, r, 0, 0.9f);
        DrawHelmMods(s, horned);
    }

    void Sallet(int s)
    {
        var metal = Plated(s);
        var r = Rar(s);
        var dome = new[] { new Vector2(-58, 98), new Vector2(-52, 52), new Vector2(-28, 30), new Vector2(0, 26), new Vector2(28, 30), new Vector2(52, 52), new Vector2(58, 98), new Vector2(70, 120), new Vector2(46, 108), new Vector2(-46, 108), new Vector2(-70, 120) };
        if (Back) dome = new[] { new Vector2(-58, 98), new Vector2(-52, 52), new Vector2(0, 26), new Vector2(52, 52), new Vector2(58, 98), new Vector2(76, 168), new Vector2(-76, 168) };
        Shade(dome, metal);
        if (!Back)
            foreach (float x in new[] { -1f, 1f })
            {
                var cheek = new[] { new Vector2(x * 46, 104), new Vector2(x * 56, 104), new Vector2(x * 50, 150), new Vector2(x * 34, 150) };
                Shade(cheek, metal);
            }
        Edge(dome, r, 0, 0.88f);
        DrawHelmMods(s, false);
    }

    void NasalHelm(int s)
    {
        var metal = Plated(s);
        var r = Rar(s);
        var cone = new[] { new Vector2(-56, 92), new Vector2(-44, 50), new Vector2(0, 8), new Vector2(44, 50), new Vector2(56, 92) };
        Shade(cone, metal);
        if (!Back) Shade(new[] { new Vector2(-5, 90), new Vector2(5, 90), new Vector2(4, 128), new Vector2(-4, 128) }, metal);
        Edge(cone, r, 0, 0.86f);
        DrawHelmMods(s, false);
    }

    void Horns(int s, Color rarity, Color bone)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            var horn = new[] { new Vector2(side * 22, 46), new Vector2(side * 40, 36), new Vector2(side * 70, 0), new Vector2(side * 84, -40), new Vector2(side * 66, -10), new Vector2(side * 30, 22) };
            Shade(horn, new Ink.Ramp(bone));
            Edge(horn, new Color(rarity, 0.8f), 1.5f, 0.85f);
        }
    }

    // ------------------------------------------------------------------------------------------------ body armour, drapes, footwear, weapons

    void Plate(int s, SlotDef d)
    {
        var r = Rar(s);
        var metal = Plated(s);
        bool chest = Array.Exists(d.Takes, c => C.Cells[c].Key == "chest.armor");
        bool belly = Array.Exists(d.Takes, c => C.Cells[c].Key == "belly.armor");
        bool back = Array.Exists(d.Takes, c => C.Cells[c].Key == "back.armor");
        // Pieces of a harness beyond the torso: plain plates on their sites.
        foreach (int c in d.Takes)
        {
            var cell = C.Cells[c];
            if (cell.Key is "chest.armor" or "belly.armor" or "back.armor") continue;
            Generic(s, d, cell);
        }
        if (Back)
        {
            if (!back) return;
            var shape = Inflate(Figure.Site("back"), 1.08f);
            Shade(shape, metal);
            Edge(shape, r, 0, 0.88f);
            DrawMods(s, new Vector2(0, 300), 40);
            return;
        }
        if (chest || belly)
        {
            var top = chest ? 210f : 300f;
            var bottom = belly ? 424f : 304f;
            var shape = new[] { new Vector2(-104, top), new Vector2(104, top), new Vector2(belly ? 82 : 98, bottom), new Vector2(0, bottom + 10), new Vector2(belly ? -82 : -98, bottom) };
            Shade(shape, metal);
            Seg(new Vector2(0, top + 6), new Vector2(0, bottom), new Color(metal.Light, 0.6f), 3);
            if (d.Family == "brigandine")
                for (int y = (int)top + 20; y < bottom; y += 24)
                    for (int x = -70; x <= 70; x += 28) Dot(new Vector2(x, y), 2.2f, Ink.Gold.Mid, false);
            Edge(shape, r, 0, 0.9f);
            if (d.Family != "plackart") DrawMods(s, new Vector2(0, chest ? 262 : 362), 44);
        }
    }

    void Mantle(int s, bool cloak)
    {
        var r = Rar(s);
        foreach (float side in new[] { -1f, 1f })
        {
            var drape = new[] { new Vector2(side * 30, 206), new Vector2(side * 128, 224), new Vector2(side * 138, cloak ? 300 : 280), new Vector2(side * 90, 272), new Vector2(side * 40, 230) };
            Shade(drape, ClothOf(s, true));
            Edge(drape, r, 2, 0.9f);
        }
        if (_modsOf.ContainsKey(s)) DrawMods(s, new Vector2(0, 214), 16);
    }

    void Robe(int s)
    {
        if (Back) return;
        var shape = new[] { new Vector2(-64, 214), new Vector2(64, 214), new Vector2(120, 730), new Vector2(-120, 730) };
        Shade(shape, ClothOf(s, true));
        Seg(new Vector2(0, 230), new Vector2(0, 720), new Color(Look.Ink, 0.4f), 2);
        Edge(new[] { shape[2], shape[3] }, Rar(s), 3, 1);
        DrawMods(s, new Vector2(0, 320), 30);
    }

    void Footwear(int s, SlotDef d, float side)
    {
        if (side == 0) return;
        var r = Rar(s);
        var (foot, toes, radii) = Figure.Foot(side);
        var a = Figure.Ankle(side);
        switch (d.Family)
        {
            case "sandal":
                Fill(new[] { a + new Vector2(side * 6 - 30, 62), a + new Vector2(side * 6 + 30, 62), a + new Vector2(side * 6 + 31, 76), a + new Vector2(side * 6 - 31, 76) }, Ink.Leather.Mid);
                Seg(a + new Vector2(-14, 20), a + new Vector2(16, 50), Ink.Leather.Shadow, 3);
                Seg(a + new Vector2(14, 20), a + new Vector2(-16, 50), Ink.Leather.Shadow, 3);
                break;
            case "sock":
                Shade(Inflate(foot, 1.04f), ClothOf(s));
                for (int t = 0; t < 5; t++) Fill(Ink.Ellipse(toes[t], radii[t] + 1, radii[t] + 1, 10), ClothOf(s).Mid);
                break;
            default:
            {
                bool metal = d.Family != "boot";
                var ramp = metal ? Plated(s) : PaintOf(s) is Color p ? new Ink.Ramp(Ink.Leather.Mid.Lerp(p, 0.5f)) : Ink.Leather;
                var shaft = new[] { a + new Vector2(-22, -80), a + new Vector2(22, -80), a + new Vector2(20, 10), a + new Vector2(-20, 10) };
                var shoe = new[] { a + new Vector2(-20, -6), a + new Vector2(20, -6), a + new Vector2(side * 6 + 34, 66), a + new Vector2(side * 6 + 30, 80), a + new Vector2(side * 6 - 30, 80), a + new Vector2(side * 6 - 34, 66) };
                if (d.Family != "sabaton") Shade(shaft, ramp);
                Shade(shoe, ramp);
                if (metal) for (int i = 1; i <= 3; i++) Edge(new[] { a + new Vector2(-24, 10 + 16 * i), a + new Vector2(24, 10 + 16 * i) }, new Color(Look.Ink, 0.4f), 1.5f, 1);
                Edge(shoe, r, 0, 0.86f);
                DrawBootMods(s, side);
                break;
            }
        }
    }

    /// <summary>What's held: a sword in the right hand, a blade, shield, orb or torch in the left (or a great weapon).</summary>
    void Weapon(int s, Cell cell, Vector2 at, float scale)
    {
        var d = C.Slots[s];
        var r = Rar(s);
        float side = Figure.SideOf(cell.Site);
        if (side == 0) return;
        var h = Figure.Hand(side);
        var grip = h.Centre + h.Dir * 8;
        // Blades point down and a little out.
        var dir = (h.Dir + new Vector2(side * 0.35f, 0)).Normalized();
        var across = new Vector2(-dir.Y, dir.X);
        var metal = MetalOf(s);
        switch (d.Family)
        {
            case "main_hand": case "great_weapon": case "off_hand":
            {
                float length = d.Family == "great_weapon" ? 330 : d.Family == "off_hand" ? 130 : 240;
                float width = d.Family == "great_weapon" ? 15 : d.Family == "off_hand" ? 7 : 10;
                var b0 = grip + dir * 24;
                var tip = b0 + dir * length;
                var blade = new[] { b0 + across * width, tip - dir * width * 2 + across * width * 0.8f, tip, tip - dir * width * 2 - across * width * 0.8f, b0 - across * width };
                Shade(blade, metal);
                Seg(b0, tip - dir * width * 2, new Color(metal.Hi, 0.6f), 2);
                Edge(blade, r, 1.5f, 0.82f);
                Seg(grip - dir * 18, grip + dir * 20, Look.Ink, 10);
                Seg(grip - dir * 18, grip + dir * 20, new Color("#4a3426"), 7);
                Seg(b0 - across * 26, b0 + across * 26, Look.Ink, 9);
                Seg(b0 - across * 26, b0 + across * 26, Ink.Gold.Mid, 6);
                Dot(grip - dir * 24, 7, Ink.Gold.Mid);
                if (_modsOf.TryGetValue(s, out var mods))
                {
                    int rune = 0, gem = 0;
                    foreach (int m in mods)
                    {
                        var f = C.Slots[m].Family;
                        var mc = Look.Tier(H.Gear[m].Rarity);
                        if (f == "rune") Rune(b0 + dir * (50 + 40 * rune++), dir, across, mc);
                        else if (f == "gem") Dot(b0 + across * (gem++ == 0 ? 0 : 14) + dir * 6, 4, mc);
                        else if (f == "enchantment") Edge(blade, new Color(mc, 0.5f + 0.3f * Mathf.Sin(_t * 3)), 4, 1.05f);
                        else if (f == "tassel") Seg(grip - dir * 24, grip - dir * 24 + new Vector2(side * 6, 40), mc, 3);
                        else if (f is "pommel") Dot(grip - dir * 24, 8, mc);
                        else if (f == "crossguard") Seg(b0 - across * 32, b0 + across * 32, mc, 3);
                        else Dot(b0 + dir * 20 + across * 10, 3, mc);
                    }
                }
                break;
            }
            case "shield":
            {
                var centre = h.Wrist + new Vector2(side * 30, -20);
                var face = Ink.Ellipse(centre, 66, 72, 28);
                Shade(face, PaintOf(s) is Color p ? new Ink.Ramp(new Color("#7b5534").Lerp(p, 0.6f)) : Ink.Wood);
                var pts = S(face);
                var closed = new Vector2[pts.Length + 1];
                Array.Copy(pts, closed, pts.Length);
                closed[^1] = pts[0];
                DrawPolyline(closed, metal.Mid, Mathf.Max(2, Px(8)), true);
                DrawPolyline(closed, r, Mathf.Max(1.2f, Px(2.5f)), true);
                Dot(centre, 16, metal.Light);
                DrawMods(s, centre, 40);
                break;
            }
            case "focus_orb":
            {
                var at2 = h.Centre + new Vector2(side * 10, -40 + 6 * Mathf.Sin(_t * 2));
                Glow(at2, 40, r);
                Dot(at2, 18, r.Lerp(Colors.White, 0.35f));
                DrawMods(s, at2, 20);
                break;
            }
            case "torch":
            {
                Seg(grip, grip + new Vector2(side * 10, -110), Look.Ink, 10);
                Seg(grip, grip + new Vector2(side * 10, -110), Ink.Wood.Mid, 7);
                var flame = grip + new Vector2(side * 10, -130);
                Glow(flame, 50, new Color("#ff9a3c"));
                Fill(new[] { flame + new Vector2(-10, 14), flame + new Vector2(0, -24 - 6 * Mathf.Sin(_t * 9)), flame + new Vector2(10, 14) }, new Color("#ffd27a"));
                break;
            }
            default:
                Glow(at, 14, r);
                Dot(at, 6, r);
                break;
        }
    }

    /// <summary>A rune cut into a blade: a stroke along it and two across, faintly lit in its rarity.</summary>
    void Rune(Vector2 at, Vector2 dir, Vector2 across, Color c)
    {
        DrawCircle(ToScreen(at), Mathf.Max(3, Px(10)), new Color(c, 0.16f), true, -1, true);
        Seg(at - dir * 9, at + dir * 9, c, 2.2f);
        Seg(at - dir * 5 - across * 5, at + dir * 1 + across * 5, c, 1.8f);
        Seg(at + dir * 3 - across * 5, at + dir * 9 + across * 5, c, 1.8f);
    }

    // ------------------------------------------------------------------------------------------------ what's set in a piece's board

    /// <summary>A piece's attachments as small marks round a point: gems as gems, runes and enchantments as glows, the rest
    /// as studs in their rarity (paint and heraldry tint the piece itself).</summary>
    void DrawMods(int host, Vector2 at, float spread, bool ring = false)
    {
        if (!_modsOf.TryGetValue(host, out var mods)) return;
        int i = 0;
        foreach (int m in mods)
        {
            var f = C.Slots[m].Family;
            if (f is "paint" or "heraldry") continue;
            var c = Look.Tier(H.Gear[m].Rarity);
            float a = i * 2.4f;
            var p = ring ? at + new Vector2(Mathf.Cos(Mathf.Pi + Mathf.Pi * (i + 0.5f) / Math.Max(1, mods.Count)) * spread, Mathf.Sin(Mathf.Pi + Mathf.Pi * (i + 0.5f) / Math.Max(1, mods.Count)) * spread * 0.25f)
                : at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spread * (0.3f + 0.12f * (i % 5));
            i++;
            switch (f)
            {
                case "gem":
                    Fill(new[] { p + new Vector2(0, -5), p + new Vector2(4, 0), p + new Vector2(0, 5), p + new Vector2(-4, 0) }, c);
                    break;
                case "rune": case "engraving": case "enchantment":
                    Dot(p, 3.5f, c.Lerp(Colors.White, 0.3f), false);
                    Glow(p, 6, c);
                    break;
                case "spike":
                    Fill(new[] { p + new Vector2(-4, 3), p + new Vector2(0, -9), p + new Vector2(4, 3) }, MetalOf(m).Light);
                    break;
                default:
                    Dot(p, 3, c);
                    break;
            }
        }
    }

    void DrawHelmMods(int s, bool horned)
    {
        if (!_modsOf.TryGetValue(s, out var mods)) return;
        int gem = 0;
        foreach (int m in mods)
        {
            var d = C.Slots[m];
            var c = Look.Tier(H.Gear[m].Rarity);
            switch (d.Family)
            {
                case "plume":
                {
                    float sway = 4 * Mathf.Sin(_t * 2);
                    var plume = new[] { new Vector2(0, 30), new Vector2(-14 + sway, -20), new Vector2(-4 + sway, -60), new Vector2(20 + sway, -40), new Vector2(12, 20) };
                    Shade(plume, new Ink.Ramp(c.Lerp(new Color("#e0d6c8"), 0.3f)));
                    break;
                }
                case "crest": case "dragon_crest":
                {
                    var crest = new[] { new Vector2(-8, 34), new Vector2(-6, 0), new Vector2(0, -16), new Vector2(6, 0), new Vector2(8, 34) };
                    if (d.Family == "dragon_crest") crest = new[] { new Vector2(-30, 32), new Vector2(-10, -20), new Vector2(10, -40), new Vector2(30, -14), new Vector2(20, 30) };
                    Shade(crest, MetalOf(m));
                    Edge(crest, c, 1.5f, 0.86f);
                    break;
                }
                case "visor":
                    if (!Back)
                    {
                        Shade(new[] { new Vector2(-44, 92), new Vector2(44, 92), new Vector2(40, 150), new Vector2(-40, 150) }, MetalOf(m));
                        Seg(new Vector2(-34, 104), new Vector2(34, 104), Look.Ink, 5);
                    }
                    break;
                case "aventail":
                    Mail(new[] { new Vector2(-54, 150), new Vector2(54, 150), new Vector2(64, 214), new Vector2(-64, 214) }, m);
                    break;
                case "cheek_fitting":
                {
                    float x = d.Name.StartsWith("Left") ? 1 : -1;
                    Shade(new[] { new Vector2(x * 44, 112), new Vector2(x * 60, 112), new Vector2(x * 56, 160), new Vector2(x * 36, 160) }, MetalOf(m));
                    break;
                }
                case "gem":
                {
                    var at = d.Name.StartsWith("Crown") ? new Vector2(0, 44) : new Vector2(-24 + 24 * gem++, 86);
                    Fill(new[] { at + new Vector2(0, -6), at + new Vector2(5, 0), at + new Vector2(0, 6), at + new Vector2(-5, 0) }, c);
                    break;
                }
                case "horn_ring":
                {
                    float x = d.Name.StartsWith("Left") ? 1 : -1;
                    float k = d.Name.EndsWith("II") ? 0.7f : 0.4f;
                    var at = new Vector2(x * 22, 46).Lerp(new Vector2(x * 84, -40), k);
                    Seg(at + new Vector2(-6, -4), at + new Vector2(6, 4), Ink.Gold.Light, 4);
                    break;
                }
                case "horn_tip":
                {
                    float x = d.Name.StartsWith("Left") ? 1 : -1;
                    Fill(new[] { new Vector2(x * 78, -30), new Vector2(x * 92, -62), new Vector2(x * 86, -28) }, MetalOf(m).Light);
                    break;
                }
                case "enchantment":
                    Edge(Ink.Ellipse(new Vector2(0, 92), 64, 78, 32), new Color(c, 0.55f + 0.35f * Mathf.Sin(_t * 2.5f)), 3, 1);
                    break;
                case "engraving": case "lining": case "paint":
                    break;
                default:
                    Dot(new Vector2(0, 60), 3, c);
                    break;
            }
        }
        _ = horned;
    }

    void DrawBeltMods(int s)
    {
        if (!_modsOf.TryGetValue(s, out var mods)) return;
        int i = 0;
        foreach (int m in mods)
        {
            var d = C.Slots[m];
            var c = Look.Tier(H.Gear[m].Rarity);
            float x = -70 + 20 * (i++ % 8);
            switch (d.Family)
            {
                case "pouch":
                    Shade(new[] { new Vector2(x - 8, 446), new Vector2(x + 8, 446), new Vector2(x + 10, 470), new Vector2(x - 10, 470) }, Ink.Leather);
                    Dot(new Vector2(x, 452), 2, c, false);
                    break;
                case "potion":
                    Fill(new[] { new Vector2(x - 3, 446), new Vector2(x + 3, 446), new Vector2(x + 6, 470), new Vector2(x - 6, 470) }, new Color(c, 0.85f));
                    break;
                case "buckle":
                    Fill(new[] { new Vector2(-12, 424), new Vector2(12, 424), new Vector2(12, 448), new Vector2(-12, 448) }, c.Lerp(Ink.Gold.Light, 0.5f));
                    break;
                case "keepsake":
                    break;
                default:
                    Dot(new Vector2(x, 458), 3.5f, c);
                    break;
            }
        }
    }

    void DrawGauntletMods(int s, float side)
    {
        if (!_modsOf.TryGetValue(s, out var mods)) return;
        var h = Figure.Hand(side);
        int spike = 0;
        foreach (int m in mods)
        {
            var c = Look.Tier(H.Gear[m].Rarity);
            switch (C.Slots[m].Family)
            {
                case "spike":
                {
                    var at = h.Along(1 + spike++ % 4, 0.05f);
                    Fill(new[] { at - h.Dir * 5, at + h.Inner * 4, at + h.Dir * 5, at - h.Inner * 4 }, MetalOf(m).Light);
                    break;
                }
                case "gem":
                    Dot(h.Wrist - h.Dir * 10, 4, c);
                    break;
                case "enchantment":
                    Edge(h.Palm, new Color(c, 0.55f + 0.35f * Mathf.Sin(_t * 2.5f)), 2.5f, 1.15f);
                    break;
                case "rune":
                    Dot(h.Centre, 3, c);
                    break;
            }
        }
    }

    void DrawPauldronMods(int s, float side, float big)
    {
        if (!_modsOf.TryGetValue(s, out var mods)) return;
        var at = Figure.Shoulder(side) + new Vector2(side * 2, -6);
        int spike = 0;
        foreach (int m in mods)
        {
            var c = Look.Tier(H.Gear[m].Rarity);
            switch (C.Slots[m].Family)
            {
                case "spike":
                {
                    var p = at + new Vector2(side * (10 + 14 * spike++), -24 * big);
                    Fill(new[] { p + new Vector2(-7, 6), p + new Vector2(side * 4, -22), p + new Vector2(7, 6) }, MetalOf(m).Light);
                    break;
                }
                case "crest":
                    Fill(new[] { at + new Vector2(-6, -28), at + new Vector2(0, -50), at + new Vector2(6, -28) }, c);
                    break;
                case "gem":
                    Dot(at + new Vector2(0, 4), 5, c);
                    break;
                case "enchantment":
                    Edge(Ink.Ellipse(at, 36 * big, 32 * big, 22), new Color(c, 0.55f + 0.35f * Mathf.Sin(_t * 2.5f)), 2.5f, 1.05f);
                    break;
            }
        }
    }

    void DrawBootMods(int s, float side)
    {
        if (!_modsOf.TryGetValue(s, out var mods)) return;
        var a = Figure.Ankle(side);
        foreach (int m in mods)
        {
            var c = Look.Tier(H.Gear[m].Rarity);
            switch (C.Slots[m].Family)
            {
                case "spur":
                    Seg(a + new Vector2(-side * 20, 20), a + new Vector2(-side * 40, 26), MetalOf(m).Light, 3);
                    Dot(a + new Vector2(-side * 42, 26), 5, Ink.Gold.Mid);
                    break;
                case "buckle":
                    Fill(new[] { a + new Vector2(-6, -40), a + new Vector2(6, -40), a + new Vector2(6, -30), a + new Vector2(-6, -30) }, c.Lerp(Ink.Gold.Light, 0.5f));
                    break;
                case "toe_cap":
                    Fill(new[] { a + new Vector2(side * 6 - 30, 66), a + new Vector2(side * 6 + 30, 66), a + new Vector2(side * 6 + 26, 80), a + new Vector2(side * 6 - 26, 80) }, MetalOf(m).Light);
                    break;
                case "spike":
                    Fill(new[] { a + new Vector2(side * 6 - 4, 72), a + new Vector2(side * 6, 96), a + new Vector2(side * 6 + 4, 72) }, MetalOf(m).Light);
                    break;
                case "enchantment":
                    Edge(Ink.Ellipse(a + new Vector2(side * 6, 34), 34, 50, 20), new Color(c, 0.55f + 0.35f * Mathf.Sin(_t * 2.5f)), 2.5f, 1);
                    break;
                default:
                    Dot(a + new Vector2(0, -10), 3, c);
                    break;
            }
        }
    }
}
