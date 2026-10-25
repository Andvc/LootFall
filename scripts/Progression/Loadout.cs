using System;
using System.Collections.Generic;
using System.Numerics;

namespace LootFall.Progression;

/// <summary>
/// The best set of pieces to wear (docs/equipment.md, "Choosing what to wear"). Each slot has a value (what wearing its
/// item is worth); a set is valid when no two pieces on a board conflict (one fills a cell the other fills or keeps
/// free) and every piece on a board is opened by the board's host. A host is worth its own value plus the best its
/// boards can hold under the cells it opens, worked out from the deepest boards up, so the choice between a great helm
/// and an open helm counts what each lets the helm's board hold. On each board the choice is a maximum-weight
/// independent set over the conflicts: split into the parts that don't touch, then branch on the piece that conflicts
/// with most (wear it, or don't), with a bound and memoised sub-problems. Exact for anything the anatomy produces;
/// a part too large to search (never seen) falls back to the best piece first.
/// </summary>
public sealed class Loadout
{
    readonly Content C;
    readonly int[] _local;
    double[] _value, _sub;
    bool[] _cand;
    /// <summary>For each host, the slots chosen on each board it brings.</summary>
    readonly List<(int Board, List<int> Slots)>[] _picks;
    /// <summary>Search nodes visited in the last solve (and whether a part had to fall back).</summary>
    public int Nodes;
    public bool FellBack;
    const int MaxNodes = 400_000, MaxPart = 256;

    public Loadout(Content c)
    {
        C = c;
        _local = new int[c.SlotCount];
        Array.Fill(_local, -1);
        _picks = new List<(int, List<int>)>[c.SlotCount];
    }

    /// <summary>
    /// Chooses what to wear: <paramref name="value"/> is what each slot's item is worth worn, <paramref name="candidate"/>
    /// whether it can be worn at all (open, with an item). Sets <paramref name="chosen"/> for every slot on the body and
    /// on the boards the chosen hosts bring (true for those chosen), leaves the rest as they were, and returns the total
    /// value. <paramref name="decided"/>, when given, is set for the boards whose slots were decided.
    /// </summary>
    public double Solve(double[] value, bool[] candidate, bool[] chosen, bool[] decided = null)
    {
        _value = value;
        _cand = candidate;
        _sub ??= new double[C.SlotCount];
        Nodes = 0;
        FellBack = false;
        // A slot's own value plus its boards', deepest first (a board's slots are a level below its hosts).
        int deepest = 0;
        foreach (var s in C.Slots) deepest = Math.Max(deepest, s.Level);
        for (int level = deepest; level >= 2; level--)
            foreach (var s in C.Slots)
            {
                if (s.Level != level) continue;
                _picks[s.Index]?.Clear();
                if (!candidate[s.Index])
                {
                    _sub[s.Index] = 0;
                    continue;
                }
                double v = value[s.Index];
                foreach (var (b, _) in s.Brings)
                {
                    var picked = new List<int>();
                    v += BoardBest(b, s.Index, picked);
                    (_picks[s.Index] ??= new List<(int, List<int>)>()).Add((b, picked));
                }
                _sub[s.Index] = v;
            }
        var body = new List<int>();
        double total = BoardBest(Anatomy.Body, -1, body);
        foreach (int s in C.Boards[Anatomy.Body].Slots) chosen[s] = false;
        if (decided != null)
        {
            Array.Clear(decided);
            decided[Anatomy.Body] = true;
        }
        foreach (int s in body) Apply(s, chosen, decided);
        return total;
    }

    void Apply(int s, bool[] chosen, bool[] decided)
    {
        chosen[s] = true;
        if (_picks[s] == null) return;
        foreach (var (b, slots) in _picks[s])
        {
            foreach (int x in C.Boards[b].Slots) chosen[x] = false;
            if (decided != null) decided[b] = true;
            foreach (int x in slots) Apply(x, chosen, decided);
        }
    }

    /// <summary>The best set on a board under a host's opened cells (any host when <paramref name="host"/> is -1: the body).</summary>
    double BoardBest(int board, int host, List<int> picked)
    {
        var def = C.Boards[board];
        var nodes = new List<int>();
        foreach (int s in def.Slots)
            if (_cand[s] && _sub[s] > 0 && (host < 0 || def.Opened(host, C.Slots[s]))) nodes.Add(s);
        if (nodes.Count == 0) return 0;
        // Split into the parts that don't conflict with each other, and solve each.
        double total = 0;
        foreach (var part in Parts(nodes)) total += Solve(part, picked);
        return total;
    }

    /// <summary>Connected parts of the conflict graph among <paramref name="nodes"/>.</summary>
    List<List<int>> Parts(List<int> nodes)
    {
        for (int i = 0; i < nodes.Count; i++) _local[nodes[i]] = i;
        var seen = new bool[nodes.Count];
        var parts = new List<List<int>>();
        var stack = new Stack<int>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (seen[i]) continue;
            var part = new List<int>();
            seen[i] = true;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int k = stack.Pop();
                part.Add(nodes[k]);
                foreach (int n in C.Slots[nodes[k]].Conflicts)
                {
                    int j = _local[n];
                    if (j < 0 || seen[j]) continue;
                    seen[j] = true;
                    stack.Push(j);
                }
            }
            parts.Add(part);
        }
        foreach (int s in nodes) _local[s] = -1;
        return parts;
    }

    // ---------------------------------------------------------------- one part: maximum-weight independent set

    int _m, _words;
    double[] _w;
    ulong[][] _adj;
    readonly Dictionary<Key, double> _memo = new();

    double Solve(List<int> part, List<int> picked)
    {
        if (part.Count == 1)
        {
            picked.Add(part[0]);
            return _sub[part[0]];
        }
        if (part.Count > MaxPart) return Greedy(part, picked);
        _m = part.Count;
        _words = (_m + 63) / 64;
        _w = new double[_m];
        _adj = new ulong[_m][];
        for (int i = 0; i < _m; i++) _local[part[i]] = i;
        for (int i = 0; i < _m; i++)
        {
            _w[i] = _sub[part[i]];
            _adj[i] = new ulong[_words];
            foreach (int n in C.Slots[part[i]].Conflicts)
            {
                int j = _local[n];
                if (j >= 0) _adj[i][j >> 6] |= 1UL << (j & 63);
            }
        }
        foreach (int s in part) _local[s] = -1;
        _memo.Clear();
        var all = new ulong[_words];
        for (int i = 0; i < _m; i++) all[i >> 6] |= 1UL << (i & 63);
        int start = Nodes;
        double best;
        try
        {
            best = Best(all);
        }
        catch (BudgetSpent)
        {
            FellBack = true;
            Nodes = start;
            return Greedy(part, picked);
        }
        var local = new List<int>();
        Collect(all, local);
        foreach (int i in local) picked.Add(part[i]);
        return best;
    }

    sealed class BudgetSpent : Exception { }

    double Best(ulong[] set)
    {
        if (IsEmpty(set)) return 0;
        var key = new Key(set);
        if (_memo.TryGetValue(key, out double known)) return known;
        if (++Nodes > MaxNodes) throw new BudgetSpent();
        double total;
        var comps = Components(set);
        if (comps.Count > 1)
        {
            total = 0;
            foreach (var c in comps) total += Best(c);
        }
        else
        {
            int v = Pivot(set, out int degree);
            var without = Without(set, v);
            if (degree == 0) total = _w[v] + Best(without);
            else
            {
                double take = _w[v] + Best(Minus(without, _adj[v]));
                total = Sum(without) <= take ? take : Math.Max(take, Best(without));
            }
        }
        _memo[key] = total;
        return total;
    }

    void Collect(ulong[] set, List<int> picked)
    {
        if (IsEmpty(set)) return;
        var comps = Components(set);
        if (comps.Count > 1)
        {
            foreach (var c in comps) Collect(c, picked);
            return;
        }
        int v = Pivot(set, out int degree);
        var without = Without(set, v);
        if (degree == 0)
        {
            picked.Add(v);
            Collect(without, picked);
            return;
        }
        var rest = Minus(without, _adj[v]);
        double take = _w[v] + Best(rest);
        if (Sum(without) <= take || take >= Best(without))
        {
            picked.Add(v);
            Collect(rest, picked);
        }
        else Collect(without, picked);
    }

    /// <summary>The piece in the set with the most conflicts in it (the most valuable among equals).</summary>
    int Pivot(ulong[] set, out int degree)
    {
        int best = -1;
        degree = -1;
        for (int w = 0; w < _words; w++)
            for (ulong bits = set[w]; bits != 0; bits &= bits - 1)
            {
                int i = (w << 6) + BitOperations.TrailingZeroCount(bits);
                int d = 0;
                for (int k = 0; k < _words; k++) d += BitOperations.PopCount(_adj[i][k] & set[k]);
                if (d > degree || d == degree && _w[i] > _w[best])
                {
                    degree = d;
                    best = i;
                }
            }
        return best;
    }

    List<ulong[]> Components(ulong[] set)
    {
        var comps = new List<ulong[]>();
        var left = (ulong[])set.Clone();
        while (!IsEmpty(left))
        {
            int w0 = 0;
            while (left[w0] == 0) w0++;
            int first = (w0 << 6) + BitOperations.TrailingZeroCount(left[w0]);
            var comp = new ulong[_words];
            var frontier = new ulong[_words];
            frontier[first >> 6] |= 1UL << (first & 63);
            while (!IsEmpty(frontier))
            {
                var next = new ulong[_words];
                for (int w = 0; w < _words; w++)
                {
                    comp[w] |= frontier[w];
                    left[w] &= ~frontier[w];
                }
                for (int w = 0; w < _words; w++)
                    for (ulong bits = frontier[w]; bits != 0; bits &= bits - 1)
                    {
                        int i = (w << 6) + BitOperations.TrailingZeroCount(bits);
                        for (int k = 0; k < _words; k++) next[k] |= _adj[i][k] & left[k];
                    }
                frontier = next;
            }
            comps.Add(comp);
        }
        return comps;
    }

    double Sum(ulong[] set)
    {
        double s = 0;
        for (int w = 0; w < _words; w++)
            for (ulong bits = set[w]; bits != 0; bits &= bits - 1) s += _w[(w << 6) + BitOperations.TrailingZeroCount(bits)];
        return s;
    }

    ulong[] Without(ulong[] set, int i)
    {
        var r = (ulong[])set.Clone();
        r[i >> 6] &= ~(1UL << (i & 63));
        return r;
    }

    ulong[] Minus(ulong[] set, ulong[] remove)
    {
        var r = new ulong[_words];
        for (int w = 0; w < _words; w++) r[w] = set[w] & ~remove[w];
        return r;
    }

    static bool IsEmpty(ulong[] set)
    {
        foreach (ulong w in set)
            if (w != 0) return false;
        return true;
    }

    /// <summary>The best piece first, then the best that still fits, and so on.</summary>
    double Greedy(List<int> part, List<int> picked)
    {
        var order = new List<int>(part);
        order.Sort((a, b) => _sub[b].CompareTo(_sub[a]));
        var taken = new HashSet<int>();
        double total = 0;
        foreach (int s in order)
        {
            bool free = true;
            foreach (int n in C.Slots[s].Conflicts)
                if (taken.Contains(n)) { free = false; break; }
            if (!free) continue;
            taken.Add(s);
            picked.Add(s);
            total += _sub[s];
        }
        return total;
    }

    /// <summary>A set as a memo key (up to four words: parts are at most <see cref="MaxPart"/> pieces).</summary>
    readonly struct Key : IEquatable<Key>
    {
        readonly ulong _a, _b, _c, _d;

        public Key(ulong[] set)
        {
            _a = set[0];
            _b = set.Length > 1 ? set[1] : 0;
            _c = set.Length > 2 ? set[2] : 0;
            _d = set.Length > 3 ? set[3] : 0;
        }

        public bool Equals(Key o) => _a == o._a && _b == o._b && _c == o._c && _d == o._d;
        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);
    }
}
