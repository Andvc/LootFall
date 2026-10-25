using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The talent tree (T): all 3,259 nodes on their map (TreeLayout), in Path of Exile's grammar (docs/art-direction.md:
/// size gives class, frames take three states, connectors two). Six colour-coded sectors; drag to pan, the wheel to
/// zoom (far out only sectors, keystones and notables carry names); hovering traces the path a click would take, as far
/// as the points go. The side panel sums what the tree gives, and holds auto-spend, the respec and paragon.
/// </summary>
public partial class TalentScreen : Overlay
{
    static TreeLayout _layout;
    static Dictionary<(int, int), List<int>> _grid;
    const float Cell = 80;

    public static readonly Color[] SectorColors =
    {
        new("#e8643c"), new("#6a8fd8"), new("#4fd1a0"), new("#d0344e"), new("#e8c04a"), new("#9a5ce8"),
    };

    readonly Canvas _map;
    readonly SubViewport _viewport;
    readonly Control _side;
    readonly TalentTree _tree;
    Vector2 _pan;
    float _zoom = 0.35f;
    int _hover = -1;
    List<int> _path = new();
    bool _dragging;
    Vector2 _dragFrom;
    float _dragDist;
    Label _points, _info;
    RichTextLabel _hoverText;

    public TalentScreen(GameScreen g) : base(g)
    {
        _tree = g.S.Tree;
        _layout ??= new TreeLayout(_tree);
        _grid ??= BuildGrid();
        var p = Frame("Talents");
        // The map draws thousands of shapes: it renders in its own viewport, shown as one texture, so it never crowds
        // the screen's own draw buffers.
        _viewport = new SubViewport { Size = new Vector2I(1340, 860), Disable3D = true, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _map = new Canvas(DrawMap).Place(0, 0, 1340, 860);
        _viewport.AddChild(_map);
        AddChild(_viewport);
        var view = new TextureRect { Texture = _viewport.GetTexture(), MouseFilter = MouseFilterEnum.Stop }.Place(400, 80, 1340, 860);
        view.GuiInput += MapInput;
        p.AddChild(view);
        _side = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(24, 80, 360, 860);
        p.AddChild(_side);
        BuildSide();
        // Open on what's taken: zoomed so the allocated part of the wheel fills the view.
        double r = 0;
        var h = g.S.H;
        for (int i = 0; i < _tree.Nodes.Count; i++)
            if (h.Talents.Allocated[i]) r = Math.Max(r, Math.Sqrt(_layout.X[i] * _layout.X[i] + _layout.Y[i] * _layout.Y[i]));
        _zoom = (float)Math.Clamp(400 / (r + 180), 0.12, 1.2);
        _pan = Vector2.Zero;
    }

    static Dictionary<(int, int), List<int>> BuildGrid()
    {
        var grid = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < _layout.X.Length; i++)
        {
            var k = ((int)Math.Floor(_layout.X[i] / Cell), (int)Math.Floor(_layout.Y[i] / Cell));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
            l.Add(i);
        }
        return grid;
    }

    void BuildSide()
    {
        _side.AddChild(Ui.Box(new Color("#100c16"), Look.Edge, 12).Place(0, 0, 360, 860));
        _points = Ui.Text("", Look.Header, Look.Good, Look.Numbers).Place(20, 16, 320, 46);
        _side.AddChild(_points);
        _info = Ui.Text("", Look.Small, Look.Muted, Look.Ui).Wrapped().Place(20, 64, 320, 60);
        _side.AddChild(_info);
        var auto = Ui.Button("", null, size: Look.Body).Place(20, 128, 320, 52);
        void Show() { auto.Text = G.S.Auto.Talents ? "Auto-spend: on" : "Auto-spend: off"; Ui.Style(auto, G.S.Auto.Talents ? Look.Good : Look.EdgeLight, Look.Body); }
        auto.Pressed += () => { G.S.Auto.Talents = !G.S.Auto.Talents; Show(); };
        Show();
        _side.AddChild(auto);
        _side.AddChild(Ui.Button("Spend the best way now", () => { G.S.AutoTalents(); Changed(); }, Look.Good, Look.Body).Place(20, 188, 320, 52));
        _side.AddChild(Ui.Button("Refund every point", () => { G.S.RefundTalents(); Changed(); }, Look.Bad, Look.Body).Place(20, 248, 320, 52));
        _side.AddChild(Ui.Text("THE TREE GIVES", 18, Look.Muted, Look.UiBold).Place(20, 316, 320, 26));
        _hoverText = Ui.Rich("", Look.Small, Look.Text).Place(20, 600, 320, 240);
        _side.AddChild(_hoverText);
        Refresh();
    }

    Control _totals;

    void Refresh()
    {
        var h = G.S.H;
        _points.Text = h.Talents.Points == 1 ? "1 point" : $"{Format.Int(h.Talents.Points)} points";
        _info.Text = $"{Format.Whole(h.Talents.AllocatedCount)} of {Format.Whole(_tree.Nodes.Count)} taken" +
                     (h.Talents.Paragon > 0 ? $", paragon {h.Talents.ParagonDamage} damage / {h.Talents.ParagonHp} health" : "") +
                     ". A level gives a point.";
        _totals?.QueueFree();
        _totals = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(20, 346, 320, 250);
        _side.AddChild(_totals);
        float y = 0;
        var sheet = h.Talents.Sheet;
        foreach (var stat in new[] { Stat.MoreDamage, Stat.MoreHp, Stat.IncDamage, Stat.IncHp, Stat.CritDamage, Stat.AttackSpeed, Stat.IncCrit, Stat.IncArmor, Stat.IncDodge, Stat.IncBlock, Stat.IncMultiHit, Stat.Leech, Stat.Regen, Stat.GoldFind, Stat.ItemFind, Stat.XpGain, Stat.Quantity, Stat.MaterialFind })
        {
            double v = sheet[stat];
            if (Math.Abs(v) < 1e-9 || y > 220) continue;
            _totals.AddChild(Ui.Text(LootFall.Game.Describe.Line(stat, v, h.S), Look.Small, Look.Text, Look.Ui).Place(0, y, 320, 24));
            y += 24;
        }
        if (y == 0) _totals.AddChild(Ui.Text("Nothing yet: spend a point.", Look.Small, Look.Muted, Look.Ui).Place(0, 0, 320, 24));
        _map.QueueRedraw();
    }

    void Changed()
    {
        Refresh();
        Main.Instance?.Sfx?.PlayGroup("powerup", 1.1f, -10);
    }

    // ------------------------------------------------------------------------------------------------ input

    Vector2 ToMap(Vector2 screen) => (screen - _map.Size / 2) / _zoom + _pan;

    Vector2 ToScreen(int id) => (new Vector2((float)_layout.X[id], (float)_layout.Y[id]) - _pan) * _zoom + _map.Size / 2;

    void MapInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } w:
                ZoomAt(w.Position, 1.15f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } w:
                ZoomAt(w.Position, 1 / 1.15f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } b:
                if (b.Pressed)
                {
                    _dragging = true;
                    _dragFrom = b.Position;
                    _dragDist = 0;
                }
                else
                {
                    _dragging = false;
                    if (_dragDist < 6 && _hover >= 0) Click(_hover);
                }
                break;
            case InputEventMouseMotion m:
                if (_dragging)
                {
                    _pan -= m.Relative / _zoom;
                    _dragDist += m.Relative.Length();
                }
                Hover(m.Position);
                _map.QueueRedraw();
                break;
        }
    }

    void ZoomAt(Vector2 at, float k)
    {
        var before = ToMap(at);
        _zoom = Math.Clamp(_zoom * k, 0.08f, 1.8f);
        _pan += before - ToMap(at);
        _map.QueueRedraw();
    }

    void Hover(Vector2 at)
    {
        var m = ToMap(at);
        int best = -1;
        double bestD = 26 / _zoom + 8;
        var k = ((int)Math.Floor(m.X / Cell), (int)Math.Floor(m.Y / Cell));
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!_grid.TryGetValue((k.Item1 + dx, k.Item2 + dy), out var list)) continue;
                foreach (int i in list)
                {
                    double d = Math.Sqrt((_layout.X[i] - m.X) * (_layout.X[i] - m.X) + (_layout.Y[i] - m.Y) * (_layout.Y[i] - m.Y));
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }
            }
        if (best == _hover) return;
        _hover = best;
        _path = best >= 0 ? G.S.H.Talents.PathTo(best) : new List<int>();
        ShowNode(best);
    }

    void ShowNode(int id)
    {
        if (id < 0)
        {
            _hoverText.Text = "";
            return;
        }
        var n = _tree.Nodes[id];
        var c = n.Sector >= 0 ? SectorColors[n.Sector] : Look.Gold;
        string kind = n.Kind switch { NodeKind.Keystone => "Keystone", NodeKind.Notable => "Notable", NodeKind.Path => "Path", NodeKind.Start => "Origin", _ => "Minor" };
        var lines = new List<string> { $"[b][color=#{c.ToHtml(false)}]{n.Name}[/color][/b]", $"[color=#a59cb8]{kind}" + (n.Sector >= 0 ? $" of {TalentTree.SectorNames[n.Sector]}, ring {n.Ring + 1}" : "") + "[/color]" };
        for (int i = 0; i < n.Stats.Length; i++) lines.Add(LootFall.Game.Describe.Line(n.Stats[i], n.Amounts[i], G.S.H.S));
        bool taken = G.S.H.Talents.Allocated[id];
        lines.Add(taken ? "[color=#6be38b]Taken[/color]" : _path.Count == 0 ? "[color=#6d6484]Out of reach[/color]" : $"[color=#ffd166]{_path.Count} point{(_path.Count == 1 ? "" : "s")} away[/color]");
        _hoverText.Text = string.Join("\n", lines);
    }

    void Click(int id)
    {
        if (G.S.H.Talents.Allocated[id]) return;
        if (G.S.H.Talents.Points <= 0)
        {
            Main.Instance?.Sfx?.PlayGroup("error", 1f, -10);
            return;
        }
        if (G.S.TakeTalent(id) > 0)
        {
            Changed();
            _path = G.S.H.Talents.PathTo(id);
            ShowNode(id);
        }
    }

    // ------------------------------------------------------------------------------------------------ the map

    void DrawMap(Canvas c)
    {
        var size = c.Size;
        c.DrawRect(new Rect2(Vector2.Zero, size), new Color("#0c0912"));
        var h = G.S.H;
        var alloc = h.Talents.Allocated;
        var centre = ToScreen(0);
        // Sector wedges and names.
        float radius = (float)_layout.Radius * _zoom + 60;
        for (int s = 0; s < TalentTree.Sectors; s++)
        {
            float a0 = -Mathf.Pi / 2 + s * Mathf.Tau / 6, a1 = a0 + Mathf.Tau / 6;
            var pts = new List<Vector2> { centre };
            for (int i = 0; i <= 16; i++) pts.Add(centre + Vector2.FromAngle(a0 + (a1 - a0) * i / 16) * radius);
            c.DrawColoredPolygon(pts.ToArray(), new Color(SectorColors[s], 0.045f));
            c.DrawLine(centre, centre + Vector2.FromAngle(a0) * radius, new Color(SectorColors[s], 0.15f), 2, true);
        }
        var view = new Rect2(ToMap(Vector2.Zero), size / _zoom).Grow(60 / _zoom);
        var onPath = new HashSet<int>(_path);
        // Connectors: taken (both ends), on the hovered path, or not; one batched call per kind (thousands of separate
        // lines overflow the renderer's command buffer and the whole layer goes undrawn).
        var takenLines = new List<Vector2>();
        var pathLines = new List<Vector2>();
        var otherLines = new List<Vector2>();
        for (int i = 0; i < _tree.Nodes.Count; i++)
        {
            var a = new Vector2((float)_layout.X[i], (float)_layout.Y[i]);
            foreach (int j in _tree.Nodes[i].Links)
            {
                if (j < i) continue;
                var b = new Vector2((float)_layout.X[j], (float)_layout.Y[j]);
                if (!view.HasPoint(a) && !view.HasPoint(b)) continue;
                bool taken = alloc[i] && alloc[j];
                bool path = (onPath.Contains(i) || alloc[i] || i == 0) && (onPath.Contains(j) || alloc[j]) && (onPath.Contains(i) || onPath.Contains(j));
                var list = taken ? takenLines : path ? pathLines : otherLines;
                list.Add(ToScreen(i));
                list.Add(ToScreen(j));
            }
        }
        float lw = Math.Min(1, _zoom * 2);
        if (otherLines.Count > 0) c.DrawMultiline(otherLines.ToArray(), new Color("#3a3150"), Math.Max(1f, 2f * lw));
        if (takenLines.Count > 0) c.DrawMultiline(takenLines.ToArray(), new Color("#e9d39a"), Math.Max(1.5f, 4f * lw));
        if (pathLines.Count > 0) c.DrawMultiline(pathLines.ToArray(), new Color(1, 1, 1, 0.85f), Math.Max(1.5f, 3f * lw));
        // Nodes: size by class, frame by state. The thousands of minors go into one mesh first (a rim disc under a fill
        // disc each), then the notables and keystones over them, then the names over everything.
        var verts = new List<Vector2>();
        var cols = new List<Color>();
        void Disc(Vector2 at, float r, Color col)
        {
            const int Seg = 10;
            for (int k = 0; k < Seg; k++)
            {
                verts.Add(at);
                verts.Add(at + Vector2.FromAngle(k * Mathf.Tau / Seg) * r);
                verts.Add(at + Vector2.FromAngle((k + 1) * Mathf.Tau / Seg) * r);
                cols.Add(col);
                cols.Add(col);
                cols.Add(col);
            }
        }
        (Color Fill, Color Rim, Color Sector, bool Taken) Style(int i)
        {
            var n = _tree.Nodes[i];
            var sc = n.Sector >= 0 ? SectorColors[n.Sector] : Look.Gold;
            bool taken = alloc[i];
            bool reachable = !taken && Adjacent(i, alloc);
            var fill = taken ? sc : reachable ? sc.Darkened(0.55f) : new Color("#1d1727");
            var rim = taken ? sc.Lightened(0.45f) : reachable ? sc.Lerp(Colors.White, 0.2f) : new Color("#4a4060");
            if (onPath.Contains(i)) rim = Colors.White;
            return (fill, rim, sc, taken);
        }
        float Radius(NodeKind k) => k switch { NodeKind.Keystone => 24, NodeKind.Notable => 16, NodeKind.Start => 30, NodeKind.Path => 13, _ => 10 } * Math.Max(0.35f, _zoom);
        var shown = new List<int>();
        for (int i = 0; i < _tree.Nodes.Count; i++)
        {
            if (!view.HasPoint(new Vector2((float)_layout.X[i], (float)_layout.Y[i]))) continue;
            if (_tree.Nodes[i].Kind != NodeKind.Minor)
            {
                shown.Add(i);
                continue;
            }
            var (fill, rim, _, _) = Style(i);
            var at = ToScreen(i);
            float r = Radius(NodeKind.Minor);
            Disc(at, r + Math.Max(1.2f, 2.5f * _zoom), rim);
            Disc(at, r, fill);
        }
        if (verts.Count > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = cols.ToArray();
            _discs ??= new ArrayMesh();
            _discs.ClearSurfaces();
            _discs.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            c.DrawMesh(_discs, null);
        }
        foreach (int i in shown)
        {
            var n = _tree.Nodes[i];
            var (fill, rim, sc, taken) = Style(i);
            var at = ToScreen(i);
            float r = Radius(n.Kind);
            if (n.Kind == NodeKind.Keystone)
            {
                var pts = new Vector2[8];
                for (int k = 0; k < 8; k++) pts[k] = at + Vector2.FromAngle(Mathf.Pi / 8 + k * Mathf.Tau / 8) * r;
                if (taken) c.DrawCircle(at, r * 1.7f, new Color(sc, 0.18f), true, -1, true);
                c.DrawColoredPolygon(pts, fill);
                var closed = new Vector2[9];
                Array.Copy(pts, closed, 8);
                closed[8] = pts[0];
                c.DrawPolyline(closed, rim, Math.Max(1.5f, 4 * _zoom), true);
            }
            else
            {
                if (taken) c.DrawCircle(at, r * 1.6f, new Color(sc, 0.16f), true, -1, true);
                c.DrawCircle(at, r, fill, true, -1, true);
                c.DrawArc(at, r, 0, Mathf.Tau, 24, rim, Math.Max(1.2f, 4 * _zoom), true);
            }
        }
        if (_hover >= 0) c.DrawArc(ToScreen(_hover), Radius(_tree.Nodes[_hover].Kind) + 6, 0, Mathf.Tau, 28, Look.Gold, 2.5f, true);
        foreach (int i in shown)
        {
            var n = _tree.Nodes[i];
            if (!((n.Kind == NodeKind.Keystone && _zoom > 0.16f) || (n.Kind == NodeKind.Notable && _zoom > 0.55f))) continue;
            var at = ToScreen(i) + new Vector2(-100, Radius(n.Kind) + 20);
            int fs = n.Kind == NodeKind.Keystone ? 18 : 14;
            c.DrawString(Look.UiBold, at + new Vector2(1.5f, 1.5f), n.Name, HorizontalAlignment.Center, 200, fs, Look.Ink);
            c.DrawString(Look.UiBold, at, n.Name, HorizontalAlignment.Center, 200, fs, alloc[i] ? Look.Text : Look.Muted);
        }
        if (_zoom < 0.3f)
            for (int s = 0; s < TalentTree.Sectors; s++)
            {
                float a = -Mathf.Pi / 2 + (s + 0.5f) * Mathf.Tau / 6;
                var at = centre + Vector2.FromAngle(a) * radius * 0.66f - new Vector2(130, 0);
                c.DrawString(Look.Display, at + new Vector2(2, 2), TalentTree.SectorNames[s].ToUpperInvariant(), HorizontalAlignment.Center, 260, 32, Look.Ink);
                c.DrawString(Look.Display, at, TalentTree.SectorNames[s].ToUpperInvariant(), HorizontalAlignment.Center, 260, 32, SectorColors[s].Lightened(0.2f));
            }
        c.DrawString(Look.Ui, new Vector2(16, size.Y - 16), "Drag to move  |  wheel to zoom  |  click a node to take the path to it", HorizontalAlignment.Left, -1, 16, Look.Dim);
    }

    ArrayMesh _discs;

    bool Adjacent(int i, bool[] alloc)
    {
        foreach (int j in _tree.Nodes[i].Links)
            if (alloc[j] || j == 0) return true;
        return false;
    }

    public override void _Process(double delta)
    {
        // Points come in while it's open (levels); keep the count fresh.
        if (_points != null && (int)(Time.GetTicksMsec() / 500) % 2 == 0) _points.Text = G.S.H.Talents.Points == 1 ? "1 point" : $"{Format.Int(G.S.H.Talents.Points)} points";
    }
}
