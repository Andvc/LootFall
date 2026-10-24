using System;
using System.Collections.Generic;

namespace LootFall.Progression;

public enum NodeKind : byte { Start, Path, Minor, Notable, Keystone }

public sealed class TalentNode
{
    public int Id;
    public NodeKind Kind;
    public int Sector, Ring;
    public string Name;
    public Stat[] Stats;
    public double[] Amounts;
    public readonly List<int> Links = new();
}

/// <summary>
/// The talent tree: about 3,250 nodes in six sectors (Blade, Bulwark, Gale, Blood, Fortune, Ruin) and fourteen rings,
/// like Path of Exile's 1,325-node tree but more than twice its size. A cluster is a short chain of minor nodes ending
/// in a notable, with a side branch; the notables (a "more" multiplier each, so they keep their worth however full the
/// percent buckets get) are the gateways to the next ring out, the clusters of a ring are
/// chained around it, and neighbouring sectors meet at every ring, so hybrid builds have paths. Eighteen keystones (three
/// per sector) carry the big "more" effects, each with a price. Node sizes grow with the ring, so the outer tree is worth
/// walking to. All amounts are percents or more-LE: the tree never gives flat stats, which the absolute scale would
/// make worthless.
/// </summary>
public sealed class TalentTree
{
    public readonly List<TalentNode> Nodes = new();
    public const int Sectors = 6;
    public readonly int Rings;

    public static readonly string[] SectorNames = { "Blade", "Bulwark", "Gale", "Blood", "Fortune", "Ruin" };

    static readonly Stat[][] Families =
    {
        new[] { Stat.IncDamage, Stat.IncCrit, Stat.CritDamage },
        new[] { Stat.IncHp, Stat.IncArmor, Stat.IncBlock },
        new[] { Stat.AttackSpeed, Stat.IncDodge, Stat.IncMultiHit },
        new[] { Stat.Leech, Stat.Regen, Stat.IncHp, Stat.IncDamage },
        new[] { Stat.GoldFind, Stat.ItemFind, Stat.XpGain, Stat.Quantity, Stat.MaterialFind },
        new[] { Stat.IncDamage, Stat.IncMultiHit, Stat.AttackSpeed, Stat.CritDamage },
    };

    /// <summary>Which "more" each sector's minor nodes feed.</summary>
    static readonly Stat[] Side = { Stat.MoreDamage, Stat.MoreHp, Stat.MoreDamage, Stat.MoreHp, Stat.MoreDamage, Stat.MoreDamage };

    /// <summary>A minor node's amount per stat, at ring 0 (talent scale 1).</summary>
    static double MinorBase(Stat s) => s switch
    {
        Stat.IncDamage => 0.06, Stat.IncHp => 0.06, Stat.CritDamage => 0.08, Stat.AttackSpeed => 0.02,
        Stat.Regen => 0.0008, Stat.Leech => 0.0015,
        Stat.IncArmor or Stat.IncDodge or Stat.IncBlock or Stat.IncCrit or Stat.IncMultiHit => 0.08,
        Stat.GoldFind => 0.06, Stat.ItemFind => 0.05, Stat.XpGain => 0.05, Stat.Quantity => 0.03, Stat.MaterialFind => 0.06,
        _ => 0.05,
    };

    /// <summary>The keystones, by sector and position (rings 4, 8 and the outermost): name, then stat/amount pairs.</summary>
    static readonly (string Name, (Stat, double)[] Effects)[][] Keystones =
    {
        new[]
        {
            ("Executioner", new[] { (Stat.MoreDamage, 2.0), (Stat.MoreHp, -0.7) }),
            ("Deathblow", new[] { (Stat.CritDamage, 1.0), (Stat.IncCrit, 0.5), (Stat.MoreHp, -0.5) }),
            ("Sovereign Blade", new[] { (Stat.MoreDamage, 4.0), (Stat.MoreHp, -1.5) }),
        },
        new[]
        {
            ("Iron Skin", new[] { (Stat.MoreHp, 2.0), (Stat.MoreDamage, -0.7) }),
            ("Unbreakable", new[] { (Stat.IncArmor, 1.5), (Stat.IncBlock, 1.0), (Stat.AttackSpeed, -0.1) }),
            ("Living Fortress", new[] { (Stat.MoreHp, 4.0), (Stat.MoreDamage, -1.5) }),
        },
        new[]
        {
            ("Whirlwind", new[] { (Stat.AttackSpeed, 0.3), (Stat.MoreHp, -0.5) }),
            ("Phantom", new[] { (Stat.IncDodge, 2.0), (Stat.MoreHp, -0.5) }),
            ("Tempest", new[] { (Stat.MoreDamage, 2.5), (Stat.AttackSpeed, 0.2), (Stat.MoreHp, -1.2) }),
        },
        new[]
        {
            ("Bloodthirst", new[] { (Stat.Leech, 0.02), (Stat.MoreHp, -0.3) }),
            ("Undying", new[] { (Stat.Regen, 0.01), (Stat.MoreHp, 1.5), (Stat.MoreDamage, -1.0) }),
            ("Sanguine Pact", new[] { (Stat.MoreDamage, 2.5), (Stat.MoreHp, 1.0), (Stat.GoldFind, -0.5) }),
        },
        new[]
        {
            ("Midas", new[] { (Stat.GoldFind, 1.0), (Stat.MoreDamage, -0.3) }),
            ("Treasure Hunter", new[] { (Stat.ItemFind, 0.8), (Stat.Quantity, 0.3), (Stat.MoreHp, -0.3) }),
            ("Fortune's Favor", new[] { (Stat.GoldFind, 1.5), (Stat.ItemFind, 1.0), (Stat.XpGain, 1.0), (Stat.MaterialFind, 1.0) }),
        },
        new[]
        {
            ("Overwhelm", new[] { (Stat.MoreDamage, 1.5), (Stat.IncMultiHit, 1.0), (Stat.MoreHp, -0.8) }),
            ("Cascade", new[] { (Stat.IncMultiHit, 2.0), (Stat.AttackSpeed, 0.1), (Stat.MoreHp, -0.5) }),
            ("Apocalypse", new[] { (Stat.MoreDamage, 5.0), (Stat.MoreHp, -2.5) }),
        },
    };

    readonly double _notable;

    public TalentTree(Params p, int rings = 14)
    {
        Rings = rings;
        _notable = p.TalentNotableLE;
        var start = NewNode(NodeKind.Start, -1, -1, "Origin", Array.Empty<Stat>(), Array.Empty<double>());
        double scale = p.TalentScale;
        for (int s = 0; s < Sectors; s++)
        {
            var family = Families[s];
            var path = NewNode(NodeKind.Path, s, -1, $"Way of {SectorNames[s]}", Array.Empty<Stat>(), Array.Empty<double>());
            Link(start, path);
            var previousRing = new List<(double Angle, TalentNode Notable)>();
            int minorCount = 0;
            int[] keystoneRings = { 4, 8, rings - 1 };
            for (int k = 0; k < rings; k++)
            {
                int clusters = 2 + (int)(k * 0.6);
                double grow = 1 + p.TalentRingGrowth * k;
                var thisRing = new List<(double, TalentNode)>();
                TalentNode lastTail = null;
                for (int j = 0; j < clusters; j++)
                {
                    double angle = (j + 0.5) / clusters;
                    TalentNode Minor()
                    {
                        // A percent of the sector's family (texture), and a little "more" on the sector's side, so every
                        // node keeps a worth however full the percent buckets get.
                        var stat = family[minorCount++ % family.Length];
                        return NewNode(NodeKind.Minor, s, k, Stats.Name(stat), new[] { stat, Side[s] },
                            new[] { MinorBase(stat) * grow * scale, p.TalentMinorLE * grow * (s == 4 ? 0.5 : 1) });
                    }
                    var entry = Minor();
                    var m1 = Minor();
                    var m2 = Minor();
                    var m3 = Minor();
                    var b1 = Minor();
                    var b2 = Minor();
                    var notable = MakeNotable(s, k, grow * scale);
                    Link(entry, m1);
                    Link(m1, m2);
                    Link(m2, m3);
                    Link(m3, notable);
                    Link(m2, b1);
                    Link(b1, b2);
                    // In from the ring inside (through its nearest notable), or from the sector's path at ring 0.
                    if (k == 0) Link(path, entry);
                    else Link(Nearest(previousRing, angle), entry);
                    // Around the ring.
                    if (lastTail != null) Link(lastTail, entry);
                    lastTail = m3;
                    thisRing.Add((angle, notable));
                }
                for (int i = 0; i < keystoneRings.Length; i++)
                {
                    if (keystoneRings[i] != k) continue;
                    var (name, effects) = Keystones[s][i];
                    var key = NewNode(NodeKind.Keystone, s, k, name, Array.ConvertAll(effects, e => e.Item1), Array.ConvertAll(effects, e => e.Item2));
                    Link(thisRing[thisRing.Count / 2].Item2, key);
                }
                previousRing = thisRing;
            }
        }
        // Neighbouring sectors meet at every ring: the last cluster of one links to the first of the next.
        var bySectorRing = new Dictionary<(int, int), List<TalentNode>>();
        foreach (var n in Nodes)
        {
            if (n.Kind != NodeKind.Notable) continue;
            if (!bySectorRing.TryGetValue((n.Sector, n.Ring), out var l)) bySectorRing[(n.Sector, n.Ring)] = l = new List<TalentNode>();
            l.Add(n);
        }
        for (int s = 0; s < Sectors; s++)
            for (int k = 0; k < rings; k++)
            {
                var a = bySectorRing[(s, k)];
                var b = bySectorRing[((s + 1) % Sectors, k)];
                Link(a[^1], b[0]);
            }
    }

    TalentNode MakeNotable(int sector, int ring, double grow)
    {
        string name = $"{SectorNames[sector]} Notable {ring + 1}";
        return sector switch
        {
            0 => NewNode(NodeKind.Notable, sector, ring, name, new[] { Stat.MoreDamage, Stat.CritDamage }, new[] { _notable * grow, 0.08 * grow }),
            1 => NewNode(NodeKind.Notable, sector, ring, name, new[] { Stat.MoreHp, Stat.IncArmor }, new[] { _notable * grow, 0.1 * grow }),
            2 => NewNode(NodeKind.Notable, sector, ring, name, new[] { Stat.MoreDamage, Stat.AttackSpeed }, new[] { 0.8 * _notable * grow, 0.03 * grow }),
            3 => NewNode(NodeKind.Notable, sector, ring, name, new[] { Stat.MoreHp, Stat.Leech }, new[] { 0.8 * _notable * grow, 0.003 * grow }),
            4 => NewNode(NodeKind.Notable, sector, ring, name, new[] { Stat.GoldFind, Stat.ItemFind, Stat.XpGain }, new[] { 0.15 * grow, 0.12 * grow, 0.12 * grow }),
            _ => NewNode(NodeKind.Notable, sector, ring, name, new[] { Stat.MoreDamage, Stat.IncMultiHit }, new[] { _notable * grow, 0.1 * grow }),
        };
    }

    static TalentNode Nearest(List<(double Angle, TalentNode Node)> ring, double angle)
    {
        var best = ring[0];
        foreach (var r in ring)
            if (Math.Abs(r.Angle - angle) < Math.Abs(best.Angle - angle)) best = r;
        return best.Node;
    }

    TalentNode NewNode(NodeKind kind, int sector, int ring, string name, Stat[] stats, double[] amounts)
    {
        var n = new TalentNode { Id = Nodes.Count, Kind = kind, Sector = sector, Ring = ring, Name = name, Stats = stats, Amounts = amounts };
        Nodes.Add(n);
        return n;
    }

    static void Link(TalentNode a, TalentNode b)
    {
        a.Links.Add(b.Id);
        b.Links.Add(a.Id);
    }
}

/// <summary>
/// The hero's talents: what's allocated, the unspent points, the paragon ranks, and the sheet of their effects.
/// <see cref="Allocate"/> spends points the way a sensible player would: the best value per point among the paths within
/// a few steps of what's taken (so notables and keystones get walked to), or a paragon rank when that's worth more.
/// </summary>
public sealed class TalentState
{
    readonly TalentTree _tree;
    readonly Params P;
    public readonly bool[] Allocated;
    public int Points, Spent, ParagonDamage, ParagonHp, AllocatedCount;
    public readonly Sheet Sheet = new();

    // BFS scratch.
    readonly int[] _dist, _parent;
    readonly double[] _value;
    readonly Queue<int> _queue = new();
    readonly List<int> _seen = new();

    public TalentState(TalentTree tree, Params p)
    {
        _tree = tree;
        P = p;
        Allocated = new bool[tree.Nodes.Count];
        _dist = new int[tree.Nodes.Count];
        _parent = new int[tree.Nodes.Count];
        _value = new double[tree.Nodes.Count];
        Allocated[0] = true;
        AllocatedCount = 1;
        Rebuild(new Scale(p.R));
    }

    public int Paragon => ParagonDamage + ParagonHp;

    double NodeValue(TalentNode n, double[] w)
    {
        double v = 0;
        for (int i = 0; i < n.Stats.Length; i++) v += n.Amounts[i] * w[(int)n.Stats[i]];
        return v;
    }

    /// <summary>Spends the unspent points. <paramref name="w"/> is the value of one unit of each stat (from the policy).</summary>
    public void Allocate(double[] w, Scale sc, int lookahead = 8)
    {
        bool changed = false;
        while (Points > 0)
        {
            double paragonDamage = P.ParagonLE * w[(int)Stat.MoreDamage], paragonHp = P.ParagonLE * w[(int)Stat.MoreHp];
            double paragon = Math.Max(paragonDamage, paragonHp);
            var (target, score) = BestPath(w, lookahead);
            // Paragon opens once most of the tree is taken: the tree is the build, paragon the endless tail.
            bool paragonOpen = AllocatedCount >= P.ParagonAfter * _tree.Nodes.Count;
            if (target < 0 || paragonOpen && score <= paragon)
            {
                if (!paragonOpen) break;
                // Ranks are cheap to decide: put the rest in paragon, on whichever side is worth more now.
                if (paragonDamage >= paragonHp) ParagonDamage += Points;
                else ParagonHp += Points;
                Spent += Points;
                Points = 0;
                changed = true;
                break;
            }
            // Walk the path to the target as far as the points go.
            var path = new List<int>();
            for (int v = target; v >= 0 && !Allocated[v]; v = _parent[v]) path.Add(v);
            path.Reverse();
            foreach (int v in path)
            {
                if (Points == 0) break;
                Allocated[v] = true;
                AllocatedCount++;
                Points--;
                Spent++;
                changed = true;
            }
        }
        if (changed) Rebuild(sc);
    }

    (int Target, double Score) BestPath(double[] w, int lookahead)
    {
        foreach (int v in _seen) _dist[v] = 0;
        _seen.Clear();
        _queue.Clear();
        for (int i = 0; i < Allocated.Length; i++)
        {
            if (!Allocated[i]) continue;
            foreach (int nb in _tree.Nodes[i].Links)
            {
                if (Allocated[nb] || _dist[nb] != 0) continue;
                _dist[nb] = 1;
                _parent[nb] = -1;
                _value[nb] = NodeValue(_tree.Nodes[nb], w);
                _seen.Add(nb);
                _queue.Enqueue(nb);
            }
        }
        int best = -1;
        double bestScore = double.NegativeInfinity;
        while (_queue.Count > 0)
        {
            int v = _queue.Dequeue();
            double score = _value[v] / _dist[v];
            if (score > bestScore) { bestScore = score; best = v; }
            if (_dist[v] >= lookahead) continue;
            foreach (int nb in _tree.Nodes[v].Links)
            {
                if (Allocated[nb] || _dist[nb] != 0) continue;
                _dist[nb] = _dist[v] + 1;
                _parent[nb] = v;
                _value[nb] = _value[v] + NodeValue(_tree.Nodes[nb], w);
                _seen.Add(nb);
                _queue.Enqueue(nb);
            }
        }
        return (best, bestScore);
    }

    /// <summary>Recomputes the sheet from the allocated nodes and paragon ranks.</summary>
    public void Rebuild(Scale sc)
    {
        Sheet.Clear();
        for (int i = 0; i < Allocated.Length; i++)
        {
            if (!Allocated[i]) continue;
            var n = _tree.Nodes[i];
            for (int j = 0; j < n.Stats.Length; j++) Sheet.Add(sc, n.Stats[j], n.Amounts[j]);
        }
        Sheet[Stat.MoreDamage] += ParagonDamage * P.ParagonLE;
        Sheet[Stat.MoreHp] += ParagonHp * P.ParagonLE;
    }
}
