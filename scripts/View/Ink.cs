using System;
using Godot;

namespace LootFall.View;

/// <summary>
/// Drawing in the inked cel style (docs/art-direction.md, "Style rules"): flat fills in a material's shadow, mid and
/// light steps, lit from the top left, inside one uniform ink outline. The code-drawn stand-ins (the knight's parts,
/// frames, the chest) all go through here, so they follow the same rules the AI art will.
/// </summary>
public static class Ink
{
    /// <summary>The outline on screen (1.5% of the knight's height, rounded).</summary>
    public const float Line = 3.5f;

    /// <summary>A material: shadow, mid, light and a highlight.</summary>
    public readonly struct Ramp
    {
        public readonly Color Shadow, Mid, Light, Hi;

        public Ramp(string shadow, string mid, string light, string hi = null)
        {
            Shadow = new Color(shadow);
            Mid = new Color(mid);
            Light = new Color(light);
            Hi = hi != null ? new Color(hi) : Light.Lightened(0.35f);
        }

        public Ramp(Color shadow, Color mid, Color light)
        {
            Shadow = shadow;
            Mid = mid;
            Light = light;
            Hi = light.Lightened(0.35f);
        }

        public Ramp(Color mid)
        {
            Mid = mid;
            Shadow = mid.Darkened(0.4f);
            Light = mid.Lightened(0.28f);
            Hi = mid.Lightened(0.6f);
        }
    }

    public static readonly Ramp Iron = new("#3b3f4a", "#646b78", "#959dab", "#d4dae4");
    public static readonly Ramp Steel = new("#47506a", "#7a87a3", "#b4c0d6", "#eef3fb");
    public static readonly Ramp Mithril = new("#5a6f86", "#9fc0d8", "#e2f2ff", "#ffffff");
    public static readonly Ramp Starsteel = new("#262338", "#433e5e", "#6f6896", "#b9b2e8");
    public static readonly Ramp Void = new("#160f29", "#33215c", "#5f45a0", "#b39cff");
    public static readonly Ramp Leather = new("#4a2f22", "#7a5038", "#a77552");
    public static readonly Ramp Cloth = new("#3a2b45", "#5f4a72", "#8a76a0");
    public static readonly Ramp Gold = new("#7a4f12", "#c48a22", "#f2c45a", "#fff2bf");
    public static readonly Ramp Wood = new("#4b3220", "#7b5534", "#a77a4e");
    public static readonly Ramp Skin = new("#9c5f48", "#d08b6a", "#f0b693");
    public static readonly Ramp Hair = new("#2e1d17", "#4d3226", "#6e4a38");
    public static readonly Ramp Linen = new("#5e5468", "#8e8498", "#c2b9cc");

    /// <summary>The metal of an item level's art tier (docs/art-direction.md, "Items and icons").</summary>
    public static Ramp Metal(int artTier) => artTier switch { 0 => Iron, 1 => Steel, 2 => Mithril, 3 => Starsteel, _ => Void };

    /// <summary>A codex era's art tier: T1 for eras 0-1, T2 for 2-3, T3 for 4-5, T4 for 6-7, T5 beyond.</summary>
    public static int ArtTier(int era) => Math.Min(4, era / 2);

    // ------------------------------------------------------------------------------------------------ primitives

    public static void Poly(CanvasItem c, Vector2[] pts, Color fill, float line = Line)
    {
        c.DrawColoredPolygon(pts, fill);
        Outline(c, pts, line);
    }

    public static void Outline(CanvasItem c, Vector2[] pts, float line = Line)
    {
        var closed = new Vector2[pts.Length + 1];
        Array.Copy(pts, closed, pts.Length);
        closed[^1] = pts[0];
        c.DrawPolyline(closed, Look.Ink, line, true);
    }

    /// <summary>A shape shaded in its ramp: the shadow fills it, the mid tone sits up and to the left of it, a light stripe
    /// runs along its lit edge, and the outline goes round the whole.</summary>
    public static void Shaded(CanvasItem c, Vector2[] pts, Ramp r, float line = Line, float inset = 0.16f)
    {
        c.DrawColoredPolygon(pts, r.Shadow);
        var centre = Centre(pts);
        var mid = Scale(pts, centre, 1 - inset, new Vector2(-2.5f, -2.5f));
        c.DrawColoredPolygon(mid, r.Mid);
        var light = Scale(pts, centre, 1 - inset * 2.6f, new Vector2(-5f, -5f));
        c.DrawColoredPolygon(light, r.Light.Lerp(r.Mid, 0.45f));
        Outline(c, pts, line);
    }

    public static void Circle(CanvasItem c, Vector2 at, float radius, Color fill, float line = Line)
    {
        c.DrawCircle(at, radius, fill, true, -1, true);
        c.DrawArc(at, radius, 0, Mathf.Tau, Math.Max(16, (int)radius), Look.Ink, line, true);
    }

    public static void ShadedCircle(CanvasItem c, Vector2 at, float radius, Ramp r, float line = Line)
    {
        c.DrawCircle(at, radius, r.Shadow, true, -1, true);
        c.DrawCircle(at + new Vector2(-radius * 0.12f, -radius * 0.12f), radius * 0.84f, r.Mid, true, -1, true);
        c.DrawCircle(at + new Vector2(-radius * 0.3f, -radius * 0.3f), radius * 0.38f, r.Light.Lerp(r.Mid, 0.4f), true, -1, true);
        c.DrawArc(at, radius, 0, Mathf.Tau, Math.Max(16, (int)radius), Look.Ink, line, true);
    }

    /// <summary>A limb between two points: a capsule of the given radius.</summary>
    public static Vector2[] Capsule(Vector2 a, Vector2 b, float ra, float rb, int arc = 7)
    {
        var dir = (b - a).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.Down;
        float angle = dir.Angle();
        var pts = new Vector2[(arc + 1) * 2];
        for (int i = 0; i <= arc; i++)
        {
            float t = angle - Mathf.Pi / 2 + Mathf.Pi * i / arc;
            pts[i] = b + Vector2.FromAngle(t) * rb;
        }
        for (int i = 0; i <= arc; i++)
        {
            float t = angle + Mathf.Pi / 2 + Mathf.Pi * i / arc;
            pts[arc + 1 + i] = a + Vector2.FromAngle(t) * ra;
        }
        return pts;
    }

    public static Vector2[] Ellipse(Vector2 c, float rx, float ry, int n = 24)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float t = Mathf.Tau * i / n;
            pts[i] = c + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry);
        }
        return pts;
    }

    public static Vector2 Centre(Vector2[] pts)
    {
        var s = Vector2.Zero;
        foreach (var p in pts) s += p;
        return s / pts.Length;
    }

    public static Vector2[] Scale(Vector2[] pts, Vector2 about, float k, Vector2 shift)
    {
        var o = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++) o[i] = about + (pts[i] - about) * k + shift;
        return o;
    }

    public static Vector2[] Move(Vector2[] pts, Vector2 by)
    {
        var o = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++) o[i] = pts[i] + by;
        return o;
    }

    /// <summary>A four-pointed glint, for rings, gems and sparkles.</summary>
    public static void Glint(CanvasItem c, Vector2 at, float size, Color color)
    {
        var pts = new[]
        {
            at + new Vector2(0, -size), at + new Vector2(size * 0.22f, -size * 0.22f), at + new Vector2(size, 0),
            at + new Vector2(size * 0.22f, size * 0.22f), at + new Vector2(0, size), at + new Vector2(-size * 0.22f, size * 0.22f),
            at + new Vector2(-size, 0), at + new Vector2(-size * 0.22f, -size * 0.22f),
        };
        c.DrawColoredPolygon(pts, color);
    }
}
