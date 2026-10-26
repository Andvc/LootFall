using System;
using System.Collections.Generic;
using System.Linq;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// What's worn where, as the figure shows it (docs/equipment.md, "The figure"). Each piece that counts covers the sites
/// of the slots it fills (and what they cover: a mask the face, a shirt the torso front and back); where pieces overlap
/// the outermost shows (ink, then cloth, mail, plate, what's over it). A slot doesn't show while something keeps it free,
/// or while something on a layer above it covers its site: the helm hides the scalp's slots, and the helm's own slots
/// show on it instead. A worn piece is one marker, wherever the slots it fills are.
/// </summary>
public sealed class Coverage
{
    readonly Content C;
    readonly Hero H;
    /// <summary>For each cell: the piece that counts filling it, and the one keeping it free (-1 for none).</summary>
    public readonly int[] FillOf, KeepOf;
    /// <summary>For each leaf site: the piece whose fill shows there, its layer and how far down its site it reaches.</summary>
    public readonly Dictionary<string, (int Piece, Layer Layer, int Depth)> Top = new();
    /// <summary>For each leaf site: every piece over it, outermost first.</summary>
    public readonly Dictionary<string, List<(int Piece, Layer Layer, int Depth)>> Stack = new();
    /// <summary>The pieces that count.</summary>
    public readonly List<int> Pieces = new();

    public Coverage(Content c, Hero h)
    {
        C = c;
        H = h;
        FillOf = new int[c.Cells.Count];
        KeepOf = new int[c.Cells.Count];
        Refresh();
    }

    public static bool Fills(Layer l) => l <= Layer.Over;
    static bool Wraps(Layer l) => l is Layer.Under or Layer.Mail or Layer.Armor or Layer.Over;

    public void Refresh()
    {
        Array.Fill(FillOf, -1);
        Array.Fill(KeepOf, -1);
        Pieces.Clear();
        Top.Clear();
        Stack.Clear();
        for (int s = 0; s < C.SlotCount; s++)
        {
            if (!H.Active[s]) continue;
            Pieces.Add(s);
            var d = C.Slots[s];
            foreach (int c in d.Takes) FillOf[c] = s;
            foreach (int c in d.Keeps) KeepOf[c] = s;
            if (d.Board != Anatomy.Body) continue;
            foreach (int c in d.Takes)
            {
                var cell = C.Cells[c];
                if (!Fills(cell.Layer)) continue;
                foreach (var (leaf, depth, wrap) in EquipmentMap.Covers(cell.Site))
                {
                    if (wrap && !Wraps(cell.Layer)) continue;
                    if (!Top.TryGetValue(leaf, out var t) || cell.Layer > t.Layer || (cell.Layer == t.Layer && depth < t.Depth))
                        Top[leaf] = (s, cell.Layer, depth);
                    if (!Stack.TryGetValue(leaf, out var list)) Stack[leaf] = list = new List<(int, Layer, int)>();
                    list.Add((s, cell.Layer, depth));
                }
            }
        }
        foreach (var list in Stack.Values)
        {
            // Outermost first: the higher layer, then the site reached most directly; ties keep their order.
            var sorted = list.Select((e, i) => (e, i)).OrderByDescending(x => x.e.Layer).ThenBy(x => x.e.Depth).ThenBy(x => x.i).Select(x => x.e).ToList();
            list.Clear();
            list.AddRange(sorted);
        }
    }

    /// <summary>The outermost piece over a leaf site, leaving some out (those drawn by pictures of their own), or -1.</summary>
    public (int Piece, Layer Layer, int Depth) TopExcept(string leaf, HashSet<int> skip)
    {
        if (Stack.TryGetValue(leaf, out var list))
            foreach (var e in list)
                if (!skip.Contains(e.Piece)) return e;
        return (-1, Layer.Skin, 0);
    }

    /// <summary>The leaf sites a cell's site covers on a view, for its layer.</summary>
    public static List<string> Leaves(MapView v, Cell cell)
    {
        var list = new List<string>();
        foreach (var (leaf, _, wrap) in EquipmentMap.Covers(cell.Site))
            if ((!wrap || Wraps(cell.Layer)) && v.Has(leaf)) list.Add(leaf);
        return list;
    }

    /// <summary>The leaf sites a piece fills on a view (where its fill shows), and the ones it keeps free.</summary>
    public (HashSet<string> Fills, HashSet<string> Keeps) Area(MapView v, int piece)
    {
        var fills = new HashSet<string>();
        var keeps = new HashSet<string>();
        var d = C.Slots[piece];
        foreach (int c in d.Takes)
            if (C.Cells[c].Board == Anatomy.Body)
                foreach (var l in Leaves(v, C.Cells[c])) fills.Add(l);
        foreach (int c in d.Keeps)
            if (C.Cells[c].Board == Anatomy.Body)
                foreach (var l in Leaves(v, C.Cells[c]))
                    if (!fills.Contains(l)) keeps.Add(l);
        return (fills, keeps);
    }

    /// <summary>What hides a body cell now: the piece keeping it free, or the one worn over its site on a higher layer
    /// (on this view, where it has an area); -1 if it shows.</summary>
    public int HiddenBy(int cell, MapView v)
    {
        if (KeepOf[cell] >= 0) return KeepOf[cell];
        var c = C.Cells[cell];
        if (c.Board != Anatomy.Body || !Fills(c.Layer)) return -1;
        var leaves = Leaves(v, c);
        if (leaves.Count == 0) return -1;
        int by = -1;
        foreach (var leaf in leaves)
        {
            if (!Top.TryGetValue(leaf, out var t) || t.Layer <= c.Layer || t.Piece == FillOf[cell]) return -1;
            by = t.Piece;
        }
        return by;
    }

    /// <summary>The body piece a piece on a board is set in (its host's host, up to the body), or itself.</summary>
    public int BodyHost(int piece)
    {
        var d = C.Slots[piece];
        for (int guard = 0; guard < 8 && d.Board != Anatomy.Body; guard++)
        {
            int host = H.HostOf[d.Board] >= 0 ? H.HostOf[d.Board] : C.Boards[d.Board].Hosts.Length > 0 ? C.Boards[d.Board].Hosts[0] : -1;
            if (host < 0) return -1;
            d = C.Slots[host];
        }
        return d.Index;
    }

    /// <summary>The cells of the boards a counting piece brings that it opens (its own slots, shown on it).</summary>
    public List<int> OwnSlots(int piece)
    {
        var list = new List<int>();
        var d = C.Slots[piece];
        foreach (var (b, opens) in d.Brings)
            if (H.HostOf[b] == piece) list.AddRange(opens);
        return list;
    }

    /// <summary>The cells under a piece on this view: the body slots its fill hides or it keeps free.</summary>
    public List<int> Beneath(int piece, MapView v)
    {
        var list = new List<int>();
        foreach (var cell in C.Cells)
        {
            if (cell.Board != Anatomy.Body || FillOf[cell.Id] == piece) continue;
            if (HiddenBy(cell.Id, v) == piece) list.Add(cell.Id);
        }
        return list;
    }
}
