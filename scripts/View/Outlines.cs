using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LootFall.View;

/// <summary>
/// Smooth outlines round areas of a picture (a set of its body sites): traced along the edges of the site map's pixels,
/// cut at the corners, simplified and rounded off, so a piece's ink line or a highlight follows its area smoothly at
/// any zoom. In the picture's pixels, each loop closed (its first point again at the end), and kept once traced.
/// </summary>
public static class Outlines
{
    static readonly Dictionary<string, Vector2[][]> Cache = new();

    /// <summary>The loops round these sites on a view; with <paramref name="inset"/>, moved that far inside the area
    /// (for a trim along its edge); <paramref name="coarse"/>, with fewer points (for when it's small on screen).</summary>
    public static Vector2[][] Of(MapView v, IEnumerable<int> ids, float inset = 0, bool coarse = false)
    {
        var sorted = ids.Where(i => i > 0).Distinct().OrderBy(i => i).ToArray();
        if (sorted.Length == 0) return Array.Empty<Vector2[]>();
        string key = $"{v.Name}:{string.Join(",", sorted)}:{inset:0.00}:{(coarse ? 'c' : 'f')}";
        if (Cache.TryGetValue(key, out var got)) return got;
        if (Cache.Count > 4000) Cache.Clear();
        var loops = inset > 0 ? Inset(Of(v, sorted), inset)
            : Trace(v, sorted).Select(l => Close(Round(Simplify(Midpoints(l), coarse ? 1.3f : 0.7f), coarse ? 1 : 2))).ToArray();
        Cache[key] = loops;
        return loops;
    }

    /// <summary>The loops round sites given by key (those the view has).</summary>
    public static Vector2[][] Of(MapView v, IEnumerable<string> sites, float inset = 0) =>
        Of(v, sites.Where(v.Has).Select(s => v.Index[s]), inset);

    public static void Release() => Cache.Clear();

    // ------------------------------------------------------------------------------------------------ tracing

    /// <summary>The edges of the area's pixels, as loops of pixel corners, each clockwise round the area (y down), so
    /// an area's outer edge has a positive signed area and a hole's a negative one.</summary>
    static List<List<Vector2I>> Trace(MapView v, int[] ids)
    {
        var inside = new bool[256];
        foreach (int i in ids) inside[i] = true;
        int w = v.Width, h = v.Height;
        var box = v.Sites[ids[0]].Box;
        foreach (int i in ids) box = box.Merge(v.Sites[i].Box);
        int x0 = Math.Max(0, (int)box.Position.X - 1), y0 = Math.Max(0, (int)box.Position.Y - 1);
        int x1 = Math.Min(w, (int)box.End.X + 1), y1 = Math.Min(h, (int)box.End.Y + 1);
        bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && inside[v.Ids[y * w + x]];
        // Each edge from one corner to the next, keyed by where it starts (a corner can start two, where two pixels of
        // the area touch only at it).
        var next = new Dictionary<long, (Vector2I To, Vector2I To2)>();
        void Edge(Vector2I a, Vector2I b)
        {
            long k = Key(a);
            next[k] = next.TryGetValue(k, out var e) ? (e.To, b) : (b, new Vector2I(-1, -1));
        }
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                if (!In(x, y)) continue;
                if (!In(x, y - 1)) Edge(new Vector2I(x, y), new Vector2I(x + 1, y));
                if (!In(x + 1, y)) Edge(new Vector2I(x + 1, y), new Vector2I(x + 1, y + 1));
                if (!In(x, y + 1)) Edge(new Vector2I(x + 1, y + 1), new Vector2I(x, y + 1));
                if (!In(x - 1, y)) Edge(new Vector2I(x, y + 1), new Vector2I(x, y));
            }
        var loops = new List<List<Vector2I>>();
        while (next.Count > 0)
        {
            var start = next.First();
            var loop = new List<Vector2I>();
            var at = new Vector2I((int)(start.Key % 100000), (int)(start.Key / 100000));
            for (int guard = 0; guard < 2_000_000; guard++)
            {
                long k = Key(at);
                if (!next.TryGetValue(k, out var e)) break;
                loop.Add(at);
                Vector2I to;
                if (e.To2.X >= 0)
                {
                    // Two ways on: take one, leave the other for its own loop.
                    to = e.To;
                    next[k] = (e.To2, new Vector2I(-1, -1));
                }
                else
                {
                    to = e.To;
                    next.Remove(k);
                }
                at = to;
            }
            if (loop.Count >= 4) loops.Add(loop);
        }
        return loops;
    }

    static long Key(Vector2I p) => (long)p.Y * 100000 + p.X;

    /// <summary>The middle of each pixel edge: the staircase with its corners cut.</summary>
    static List<Vector2> Midpoints(List<Vector2I> loop)
    {
        var o = new List<Vector2>(loop.Count);
        for (int i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            o.Add(new Vector2((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f));
        }
        return o;
    }

    /// <summary>Fewer points, none further than <paramref name="eps"/> from the line (Ramer-Douglas-Peucker, closed).</summary>
    static List<Vector2> Simplify(List<Vector2> pts, float eps)
    {
        if (pts.Count < 8) return pts;
        // Split the loop at its two most distant points and simplify each half.
        int far = 0;
        float best = -1;
        for (int i = 1; i < pts.Count; i++)
        {
            float d = pts[i].DistanceSquaredTo(pts[0]);
            if (d > best)
            {
                best = d;
                far = i;
            }
        }
        var keep = new bool[pts.Count];
        keep[0] = keep[far] = true;
        Rdp(pts, 0, far, eps * eps, keep);
        Rdp(pts, far, pts.Count, eps * eps, keep);
        var o = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++)
            if (keep[i]) o.Add(pts[i]);
        return o;
    }

    static void Rdp(List<Vector2> pts, int a, int b, float eps2, bool[] keep)
    {
        // b may be pts.Count (the loop's start again).
        var pa = pts[a];
        var pb = pts[b % pts.Count];
        int best = -1;
        float bestD = eps2;
        var ab = pb - pa;
        float len2 = Mathf.Max(ab.LengthSquared(), 1e-6f);
        for (int i = a + 1; i < b; i++)
        {
            var ap = pts[i] - pa;
            float t = Mathf.Clamp(ap.Dot(ab) / len2, 0, 1);
            float d = (ap - ab * t).LengthSquared();
            if (d > bestD)
            {
                bestD = d;
                best = i;
            }
        }
        if (best < 0) return;
        keep[best] = true;
        Rdp(pts, a, best, eps2, keep);
        Rdp(pts, best, b, eps2, keep);
    }

    /// <summary>Corners rounded off (Chaikin's cuts, closed).</summary>
    static List<Vector2> Round(List<Vector2> pts, int times)
    {
        for (int r = 0; r < times && pts.Count >= 3; r++)
        {
            var o = new List<Vector2>(pts.Count * 2);
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                o.Add(a.Lerp(b, 0.25f));
                o.Add(a.Lerp(b, 0.75f));
            }
            pts = o;
        }
        return pts;
    }

    static Vector2[] Close(List<Vector2> pts)
    {
        var o = new Vector2[pts.Count + 1];
        pts.CopyTo(o);
        o[^1] = pts[0];
        return o;
    }

    static float SignedArea(Vector2[] pts)
    {
        float a = 0;
        for (int i = 0; i + 1 < pts.Length; i++) a += pts[i].X * pts[i + 1].Y - pts[i + 1].X * pts[i].Y;
        return a / 2;
    }

    /// <summary>Each loop moved inside the area: an outer edge shrunk, a hole's edge grown.</summary>
    static Vector2[][] Inset(Vector2[][] loops, float d)
    {
        var o = new List<Vector2[]>();
        foreach (var loop in loops)
        {
            var open = loop[..^1];
            bool hole = SignedArea(loop) < 0;
            foreach (var p in Geometry2D.OffsetPolygon(open, hole ? d : -d, Geometry2D.PolyJoinType.Round))
            {
                if (p.Length < 3) continue;
                var c = new Vector2[p.Length + 1];
                p.CopyTo(c, 0);
                c[^1] = p[0];
                o.Add(c);
            }
        }
        return o.ToArray();
    }
}
