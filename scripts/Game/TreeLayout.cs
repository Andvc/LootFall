using System;
using System.Collections.Generic;
using LootFall.Progression;

namespace LootFall.Game;

/// <summary>
/// Where each talent node sits on the tree's map: a wheel of six sectors around the origin, a ring per tier of the
/// tree, every cluster (four minors to a notable, a branch of two to the side) drawn as a small hook inside its ring's
/// band, keystones just outside the notable they hang from. Rings are far enough apart, and wide enough, that no two
/// nodes touch (<see cref="MinGap"/>); the whole wheel is about 4,100 px across at zoom 1.
/// </summary>
public sealed class TreeLayout
{
    public const double FirstRing = 330, RingGap = 130, PathRadius = 175;

    public readonly double[] X, Y;
    public readonly double Radius;

    public TreeLayout(TalentTree tree)
    {
        int n = tree.Nodes.Count;
        X = new double[n];
        Y = new double[n];
        // Each cluster's seven nodes in the order the tree makes them: entry, m1, m2, m3, b1, b2, notable; offsets are
        // (along the ring, outward) in pixels from the cluster's centre.
        (double T, double R)[] hook =
        {
            (0, -40), (-42, -18), (-50, 12), (-28, 38), (22, 0), (54, -22), (14, 44),
        };
        var counts = new Dictionary<(int Sector, int Ring), int>();
        var seen = new Dictionary<(int Sector, int Ring), int>();
        foreach (var node in tree.Nodes)
            if (node.Kind == NodeKind.Notable) counts[(node.Sector, node.Ring)] = counts.GetValueOrDefault((node.Sector, node.Ring)) + 1;

        // Each notable's cluster centre (angle, radius), for the keystones.
        var clusterOf = new Dictionary<int, (double Angle, double Radius)>();
        int inCluster = 0;
        double angle = 0, radius = 0;
        foreach (var node in tree.Nodes)
        {
            switch (node.Kind)
            {
                case NodeKind.Start:
                    Place(node.Id, 0, 0);
                    continue;
                case NodeKind.Path:
                    Polar(node.Id, SectorMid(node.Sector), PathRadius);
                    continue;
                case NodeKind.Keystone:
                {
                    // In the gap between the cluster of the notable it hangs from (its only link) and the next one.
                    var (a, r) = clusterOf[node.Links[0]];
                    Polar(node.Id, a + 0.5 * ClusterWidth(node.Sector, node.Ring, counts) / r, r + 30);
                    continue;
                }
            }
            if (inCluster == 0)
            {
                // A new cluster: its place around the ring.
                var key = (node.Sector, node.Ring);
                int j = seen.GetValueOrDefault(key);
                seen[key] = j + 1;
                int clusters = counts[key];
                radius = FirstRing + RingGap * node.Ring;
                angle = SectorStart(node.Sector) + (j + 0.5) / clusters * SectorSpan;
            }
            var (t, dr) = hook[inCluster];
            double rr = radius + dr;
            Polar(node.Id, angle + t / rr, rr);
            if (node.Kind == NodeKind.Notable) clusterOf[node.Id] = (angle, radius);
            inCluster = (inCluster + 1) % hook.Length;
        }
        double max = 0;
        for (int i = 0; i < n; i++) max = Math.Max(max, Math.Sqrt(X[i] * X[i] + Y[i] * Y[i]));
        Radius = max;
    }

    const double SectorSpan = Math.PI * 2 / TalentTree.Sectors;

    /// <summary>Sectors run clockwise from the top.</summary>
    static double SectorStart(int s) => -Math.PI / 2 + s * SectorSpan;

    static double SectorMid(int s) => SectorStart(s) + SectorSpan / 2;

    static double ClusterWidth(int sector, int ring, Dictionary<(int, int), int> counts) =>
        SectorSpan * (FirstRing + RingGap * ring) / counts[(sector, ring)];

    void Place(int id, double x, double y)
    {
        X[id] = x;
        Y[id] = y;
    }

    void Polar(int id, double a, double r) => Place(id, r * Math.Cos(a), r * Math.Sin(a));

    /// <summary>The smallest distance between two nodes' centres (for the check that none overlap).</summary>
    public double MinGap() => Closest().Gap;

    /// <summary>The two nodes closest together, and how far apart.</summary>
    public (double Gap, int A, int B) Closest()
    {
        int pa = -1, pb = -1;
        int n = X.Length;
        double best = double.MaxValue;
        // A grid of buckets keeps this linear.
        const double cell = 64;
        var grid = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < n; i++)
        {
            var k = ((int)Math.Floor(X[i] / cell), (int)Math.Floor(Y[i] / cell));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
            l.Add(i);
        }
        foreach (var (k, list) in grid)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!grid.TryGetValue((k.Item1 + dx, k.Item2 + dy), out var other)) continue;
                    foreach (int a in list)
                        foreach (int b in other)
                        {
                            if (b <= a) continue;
                            double d = Math.Sqrt((X[a] - X[b]) * (X[a] - X[b]) + (Y[a] - Y[b]) * (Y[a] - Y[b]));
                            if (d < best) { best = d; pa = a; pb = b; }
                        }
                }
        return (best, pa, pb);
    }
}
