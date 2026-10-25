using System;
using System.Collections.Generic;
using Godot;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The paper doll's geometry (docs/equipment.md, "The figure"): a knight a thousand units tall facing you, arms a little
/// out, every body site's shape and every cell's place on it. The character's right is on your left; seen from behind
/// (<see cref="Mirror"/>) it's the other way round. All in figure units: x from the middle, y down from above the head.
/// </summary>
public static class Figure
{
    public const float Top = -40, Bottom = 1010;

    static readonly Vector2 HeadC = new(0, 98);
    const float HeadRx = 50, HeadRy = 64;

    /// <summary>The side sign of a site or cell key: -1 for the right ("R."), 1 for the left ("L."), 0 for neither.</summary>
    public static float SideOf(string key) => key.StartsWith("R.") ? -1 : key.StartsWith("L.") ? 1 : 0;
    static string Base(string key) => key.Length > 2 && key[1] == '.' && (key[0] == 'R' || key[0] == 'L') ? key[2..] : key;

    /// <summary>Seen from behind, left and right swap places.</summary>
    public static Vector2 Mirror(Vector2 p, bool back) => back ? new Vector2(-p.X, p.Y) : p;

    // ------------------------------------------------------------------------------------------------ joints

    public static Vector2 Shoulder(float s) => new(s * 108, 232);
    public static Vector2 Elbow(float s) => new(s * 156, 370);
    public static Vector2 Wrist(float s) => new(s * 186, 488);
    public static Vector2 Hip(float s) => new(s * 44, 494);
    public static Vector2 Knee(float s) => new(s * 52, 702);
    public static Vector2 Ankle(float s) => new(s * 56, 905);

    /// <summary>A hand: its wrist, the way it points, the way to the body, the palm and the five fingers (thumb first).</summary>
    public readonly struct HandShape
    {
        public readonly Vector2 Wrist, Dir, Inner;
        public readonly Vector2[] Palm;
        public readonly Vector2[] Base, Tip;
        public readonly float[] Width;

        public HandShape(float s)
        {
            Wrist = Figure.Wrist(s);
            Dir = (Wrist - Elbow(s)).Normalized();
            Inner = new Vector2(-s * Dir.Y, s * Dir.X);
            const float L = 46;
            Palm = new[] { Wrist + Inner * 17, Wrist + Dir * L + Inner * 21, Wrist + Dir * L - Inner * 21, Wrist - Inner * 16 };
            Base = new Vector2[5];
            Tip = new Vector2[5];
            Width = new[] { 5.6f, 5f, 5.2f, 5f, 4.3f };
            var knuckles = Wrist + Dir * L;
            float[] along = { 0, 16, 5, -6, -16 };
            float[] angle = { 0.8f, 0.1f, 0f, -0.09f, -0.2f };
            float[] length = { 30, 34, 38, 35, 28 };
            for (int f = 0; f < 5; f++)
            {
                var start = f == 0 ? Wrist + Dir * 16 + Inner * 19 : knuckles + Inner * along[f];
                var dir = (Dir * Mathf.Cos(angle[f]) + Inner * Mathf.Sin(angle[f])).Normalized();
                Base[f] = start;
                Tip[f] = start + dir * length[f];
            }
        }

        public Vector2 Along(int finger, float t) => Base[finger].Lerp(Tip[finger], t);
        public Vector2 Centre => Wrist + Dir * 24;
    }

    static readonly Dictionary<float, HandShape> Hands = new();
    public static HandShape Hand(float s)
    {
        if (!Hands.TryGetValue(s, out var h)) Hands[s] = h = new HandShape(s);
        return h;
    }

    /// <summary>A foot seen from the front: its outline, and its five toes from the big toe in (centres and radii).</summary>
    public static (Vector2[] Shape, Vector2[] Toes, float[] Radii) Foot(float s)
    {
        var a = Ankle(s);
        float inner = -s;
        var shape = new[] { a + new Vector2(-16, 0), a + new Vector2(16, 0), a + new Vector2(s * 6 + 28, 64), a + new Vector2(s * 6 - 28, 64) };
        var toes = new Vector2[5];
        var radii = new[] { 7.5f, 6.2f, 5.8f, 5.2f, 4.6f };
        float[] across = { 18, 8, -1, -9.5f, -17 };
        float[] up = { 0, 0, 1, 2.5f, 5 };
        for (int t = 0; t < 5; t++) toes[t] = new Vector2(a.X + s * 6 + inner * across[t], a.Y + 66 - up[t]);
        return (shape, toes, radii);
    }

    // ------------------------------------------------------------------------------------------------ shapes

    static Vector2[] Cap(Vector2 c, float rx, float ry, float cutY, int n = 28)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= n; i++)
        {
            float t = Mathf.Pi + Mathf.Pi * i / n;
            var p = c + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry);
            if (p.Y <= cutY) pts.Add(p);
        }
        return pts.ToArray();
    }

    static Vector2[] Below(Vector2 c, float rx, float ry, float cutY, int n = 32)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= n; i++)
        {
            float t = Mathf.Tau * i / n;
            var p = c + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry);
            if (p.Y >= cutY) pts.Add(p);
        }
        pts.Sort((a, b) => Mathf.Atan2(a.Y - c.Y, a.X - c.X).CompareTo(Mathf.Atan2(b.Y - c.Y, b.X - c.X)));
        return pts.ToArray();
    }

    public static Vector2[] HeadShape() => Ink.Ellipse(HeadC, HeadRx, HeadRy, 32);
    public static Vector2[] Torso() => new[]
    {
        new Vector2(-100, 214), new Vector2(100, 214), new Vector2(96, 296), new Vector2(76, 410), new Vector2(84, 466),
        new Vector2(44, 512), new Vector2(-44, 512), new Vector2(-84, 466), new Vector2(-76, 410), new Vector2(-96, 296),
    };

    /// <summary>A site's shape (front view), or null for one that is only a point.</summary>
    public static Vector2[] Site(string key)
    {
        float s = SideOf(key);
        switch (Base(key))
        {
            case "scalp": return Cap(HeadC, HeadRx + 2, HeadRy + 2, 84);
            case "hair": return Cap(HeadC + new Vector2(0, -2), HeadRx + 5, HeadRy + 5, 88);
            case "brow": return new[] { new Vector2(-42, 74), new Vector2(42, 74), new Vector2(44, 90), new Vector2(-44, 90) };
            case "eye": return Ink.Ellipse(new Vector2(s * 19, 102), 11, 7, 14);
            case "ear": return Ink.Ellipse(new Vector2(s * 53, 104), 9, 16, 14);
            case "cheek": return Ink.Ellipse(new Vector2(s * 30, 126), 13, 10, 14);
            case "face": return Below(HeadC, HeadRx + 1, HeadRy + 1, 86);
            case "nose": return new[] { new Vector2(0, 106), new Vector2(8, 126), new Vector2(-8, 126) };
            case "mouth": return Ink.Ellipse(new Vector2(0, 141), 14, 6, 14);
            case "chin": return Ink.Ellipse(new Vector2(0, 155), 19, 9, 14);
            case "nape": return new[] { new Vector2(-24, 140), new Vector2(24, 140), new Vector2(22, 172), new Vector2(-22, 172) };
            case "horns": return null;
            case "halo": return Ink.Ellipse(new Vector2(0, 16), 56, 13, 28);
            case "neck": return new[] { new Vector2(-20, 152), new Vector2(20, 152), new Vector2(22, 218), new Vector2(-22, 218) };
            case "chest": return new[] { new Vector2(-100, 216), new Vector2(100, 216), new Vector2(95, 302), new Vector2(-95, 302) };
            case "heart": return Ink.Ellipse(new Vector2(26, 258), 12, 12, 12);
            case "belly": return new[] { new Vector2(-95, 302), new Vector2(95, 302), new Vector2(77, 420), new Vector2(-77, 420) };
            case "back":
                return new[]
                {
                    new Vector2(-100, 216), new Vector2(100, 216), new Vector2(95, 302), new Vector2(77, 420), new Vector2(-77, 420), new Vector2(-95, 302),
                };
            case "tail": return null;
            case "waist": return new[] { new Vector2(-78, 416), new Vector2(78, 416), new Vector2(84, 454), new Vector2(-84, 454) };
            case "hip": return new[] { new Vector2(s * 58, 448), new Vector2(s * 90, 448), new Vector2(s * 96, 506), new Vector2(s * 56, 506) };
            case "groin": return new[] { new Vector2(-46, 448), new Vector2(46, 448), new Vector2(26, 518), new Vector2(-26, 518) };
            case "shoulder": return Ink.Ellipse(Shoulder(s) + new Vector2(0, -4), 32, 30, 20);
            case "armpit": return Ink.Ellipse(new Vector2(s * 88, 264), 12, 17, 14);
            case "upperarm": return Ink.Capsule(Shoulder(s), Elbow(s), 26, 21);
            case "elbow": return Ink.Ellipse(Elbow(s), 22, 22, 18);
            case "forearm": return Ink.Capsule(Elbow(s), Wrist(s), 21, 16);
            case "wrist":
            {
                var h = Hand(s);
                return Ink.Capsule(h.Wrist - h.Dir * 7, h.Wrist + h.Dir * 5, 17, 17, 5);
            }
            case "hand": return Hand(s).Palm;
            case "palm": return Ink.Scale(Hand(s).Palm, Ink.Centre(Hand(s).Palm), 0.6f, Vector2.Zero);
            case "grip": return Ink.Ellipse(Hand(s).Centre + Hand(s).Dir * 6, 12, 12, 12);
            case "thumb": case "index": case "middle": case "ring": case "little":
            {
                var h = Hand(s);
                int f = Array.IndexOf(Fingers, Base(key));
                return Ink.Capsule(h.Base[f], h.Tip[f], h.Width[f], h.Width[f] * 0.9f, 4);
            }
            case "thigh": return Ink.Capsule(Hip(s), Knee(s), 34, 26);
            case "knee": return Ink.Ellipse(Knee(s), 25, 24, 18);
            case "shin": return Ink.Capsule(Knee(s), Ankle(s), 25, 18);
            case "ankle": return Ink.Capsule(Ankle(s) + new Vector2(0, -12), Ankle(s) + new Vector2(0, 6), 18, 18, 5);
            case "foot": return Foot(s).Shape;
            case "sole":
            {
                var a = Ankle(s);
                return new[] { a + new Vector2(s * 6 - 30, 60), a + new Vector2(s * 6 + 30, 60), a + new Vector2(s * 6 + 31, 74), a + new Vector2(s * 6 - 31, 74) };
            }
            case "toetips":
            {
                var a = Ankle(s);
                return new[] { a + new Vector2(s * 6 - 28, 70), a + new Vector2(s * 6 + 28, 70), a + new Vector2(s * 6 + 26, 76), a + new Vector2(s * 6 - 26, 76) };
            }
            case "bigtoe": case "toe2": case "toe3": case "toe4": case "littletoe":
            {
                var (_, toes, radii) = Foot(s);
                int t = Array.IndexOf(ToeKeys, Base(key));
                return Ink.Ellipse(toes[t], radii[t], radii[t], 10);
            }
            default: return null;
        }
    }

    public static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "little" };
    public static readonly string[] ToeKeys = { "bigtoe", "toe2", "toe3", "toe4", "littletoe" };

    /// <summary>Sites seen from behind (the rest from the front; the head, arms and legs from both).</summary>
    public static bool BackOnly(string site) => site is "back" or "tail" or "nape";
    public static bool FrontOnly(string site)
    {
        string b = Base(site);
        return b is "eye" or "cheek" or "face" or "nose" or "mouth" or "chin" or "brow" or "chest" or "heart" or "belly" or "groin" or "palm";
    }

    /// <summary>Where a site's marker goes (front view).</summary>
    public static Vector2 SiteAnchor(string key)
    {
        float s = SideOf(key);
        switch (Base(key))
        {
            case "scalp": return new Vector2(0, 50);
            case "hair": return new Vector2(-30, 52);
            case "brow": return new Vector2(0, 82);
            case "nose": return new Vector2(0, 119);
            case "horns": return new Vector2(s == 0 ? 0 : s * 34, 34);
            case "halo": return new Vector2(0, 14);
            case "neck": return new Vector2(0, 190);
            case "nape": return new Vector2(0, 158);
            case "back": return new Vector2(0, 300);
            case "tail": return new Vector2(36, 560);
            case "heart": return new Vector2(26, 258);
            case "hand": return Hand(s).Centre;
            case "palm": return Hand(s).Centre + Hand(s).Inner * 6 + Hand(s).Dir * 6;
            case "grip": return Hand(s).Centre + Hand(s).Dir * 8;
            case "aura": return new Vector2(-170, 560);
            case "orbit": return new Vector2(108, 70);
            case "shadow": return new Vector2(-150, 990);
            case "companion": return new Vector2(170, 950);
            case "heavens": return new Vector2(-200, 120);
        }
        var shape = Site(key);
        return shape != null ? Ink.Centre(shape) : Vector2.Zero;
    }

    /// <summary>Where a body cell's marker goes (front view): its own spot on a finger, an ear or a toe, the site's otherwise.</summary>
    public static Vector2 Anchor(Cell c)
    {
        string site = c.Site, cell = c.Key[(c.Key.LastIndexOf('.') + 1)..];
        float s = SideOf(site);
        string b = Base(site);
        if (Array.IndexOf(Fingers, b) is var f && f >= 0)
        {
            var h = Hand(s);
            return cell switch { "band" => h.Along(f, 0.3f), "knuckle" => h.Along(f, 0.62f), "tip" => h.Along(f, 0.94f), _ => h.Along(f, 0.46f) };
        }
        if (Array.IndexOf(ToeKeys, b) is var t && t >= 0) return Foot(s).Toes[t];
        switch (b, cell)
        {
            case ("ear", "lobe"): return new Vector2(s * 54, 118);
            case ("ear", "helix"): return new Vector2(s * 59, 96);
            case ("ear", "cuff"): return new Vector2(s * 60, 106);
            case ("ear", "tip"): return new Vector2(s * 58, 86);
            case ("nose", "septum"): return new Vector2(0, 127);
            case ("nose", "nostrilR"): return new Vector2(-7, 124);
            case ("nose", "nostrilL"): return new Vector2(7, 124);
            case ("mouth", "lip"): return new Vector2(5, 146);
            case ("mouth", "teeth"): return new Vector2(-5, 141);
            case ("hair", "pin1"): return new Vector2(-34, 48);
            case ("hair", "pin2"): return new Vector2(34, 46);
            case ("hair", "ribbon"): return new Vector2(0, 34);
            case ("hair", "beads"): return new Vector2(-52, 132);
            case ("brow", "jewel"): return new Vector2(0, 76);
            case ("chest", "pin1"): return new Vector2(42, 238);
            case ("chest", "pin2"): return new Vector2(-44, 240);
            case ("chest", "pin3"): return new Vector2(-50, 262);
            case ("chest", "pin4"): return new Vector2(-38, 282);
            case ("belly", "navel"): return new Vector2(0, 392);
            case ("wrist", "bangle1"): return Hand(s).Wrist - Hand(s).Dir * 5;
            case ("wrist", "bangle2"): return Hand(s).Wrist - Hand(s).Dir * 14;
            case ("ankle", "anklet1"): return Ankle(s) + new Vector2(0, -4);
            case ("ankle", "anklet2"): return Ankle(s) + new Vector2(0, -16);
            case ("upperarm", "adorn"): return Shoulder(s).Lerp(Elbow(s), 0.35f);
            case ("thigh", "adorn"): return Hip(s).Lerp(Knee(s), 0.55f);
            case ("forearm", "adorn"): return Elbow(s).Lerp(Wrist(s), 0.45f) + new Vector2(s * 10, 0);
            case ("shoulder", "perch"): return Shoulder(s) + new Vector2(s * 4, -40);
            case ("waist", "belt"): return new Vector2(0, 436);
            case ("back", "mount"): return new Vector2(-40, 250);
            case ("back", "wings"): return new Vector2(70, 250);
            case ("companion", "familiar"): return new Vector2(160, 960);
            case ("companion", "steed"): return new Vector2(-235, 940);
        }
        if (b == "orbit")
        {
            int i = int.Parse(cell[^1..]) - 1;
            float a = -Mathf.Pi / 2 + i * Mathf.Tau / 6;
            return HeadC + new Vector2(Mathf.Cos(a) * 100, Mathf.Sin(a) * 30 - 10);
        }
        if (b == "heavens")
        {
            if (cell == "comet") return new Vector2(230, 70);
            if (cell == "eclipse") return new Vector2(-235, 60);
            int i = int.Parse(cell[^1..]) - 1;
            return new Vector2((i % 2 == 0 ? -1 : 1) * (150 + 40 * (i / 2 % 2)), 170 + 80 * (i / 2));
        }
        return SiteAnchor(site);
    }

    // ------------------------------------------------------------------------------------------------ regions

    public static readonly string[] Regions =
    {
        "Head", "Neck", "Torso", "Back", "Waist", "Right arm", "Left arm", "Right hand", "Left hand", "Right leg", "Left leg",
        "Right foot", "Left foot", "Beyond",
    };

    /// <summary>The part of the figure a region's zoom frames (front view; the back's from behind).</summary>
    public static Rect2 RegionRect(string region) => region switch
    {
        "Head" => new Rect2(-95, -20, 190, 220),
        "Neck" => new Rect2(-80, 130, 160, 110),
        "Torso" => new Rect2(-135, 200, 270, 330),
        "Back" => new Rect2(-170, 120, 340, 520),
        "Waist" => new Rect2(-115, 400, 230, 135),
        "Right arm" => new Rect2(-245, 185, 180, 330),
        "Left arm" => new Rect2(65, 185, 180, 330),
        "Right hand" => new Rect2(-252, 460, 112, 124),
        "Left hand" => new Rect2(140, 460, 112, 124),
        "Right leg" => new Rect2(-120, 460, 150, 470),
        "Left leg" => new Rect2(-30, 460, 150, 470),
        "Right foot" => new Rect2(-115, 880, 105, 105),
        "Left foot" => new Rect2(10, 880, 105, 105),
        _ => new Rect2(-300, -40, 600, 1050),
    };

    /// <summary>Which region's area a figure point (front view) is in, for clicking the whole figure; null for none.</summary>
    public static string RegionAt(Vector2 p, bool back)
    {
        if (back)
        {
            if (Geometry2D.IsPointInPolygon(p, Site("back"))) return "Back";
            if (p.Y > 440 && p.Y < 700 && Mathf.Abs(p.X - 36) < 60) return "Back";
        }
        // Small, specific regions first.
        foreach (var r in new[] { "Right hand", "Left hand", "Right foot", "Left foot", "Head", "Neck", "Waist" })
            if (RegionRect(r).HasPoint(p)) return r;
        if (Geometry2D.IsPointInPolygon(p, Site("chest")) || Geometry2D.IsPointInPolygon(p, Site("belly"))) return back ? "Back" : "Torso";
        foreach (var r in new[] { "Right arm", "Left arm", "Right leg", "Left leg" })
            if (RegionRect(r).HasPoint(p)) return r;
        return null;
    }
}
