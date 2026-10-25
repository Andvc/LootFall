using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The knight as a paper doll (docs/equipment.md, "The figure"): facing you or turned round, with every piece that counts
/// drawn where it sits, layer over layer (ink on the skin, then cloth, mail, plate, what's over it, jewellery, what's held,
/// what's grown, what floats around), and what's set in a piece's board drawn on it. The camera frames the whole figure or
/// zooms into a region, where each slot gets a marker. Drawn in code at any zoom, in screen space, so outlines stay crisp.
/// </summary>
public partial class PaperDoll : Control
{
    readonly Session _s;
    /// <summary>Turned round (the back's sites, cloaks and wings from behind).</summary>
    public bool Back;
    /// <summary>The camera: how far in (1: the whole figure) and the figure point (front view) at the middle.</summary>
    public float Zoom = 1;
    public Vector2 Focus = new(0, (Figure.Top + Figure.Bottom) / 2);
    float _fromZoom, _toZoom = 1, _move = 1;
    Vector2 _fromFocus, _toFocus = new(0, (Figure.Top + Figure.Bottom) / 2);

    /// <summary>The region zoomed into (its slot markers show), and the region the mouse is over on the whole figure.</summary>
    public string Region;
    public string Hover;
    /// <summary>The slot markers: whether they show, and the one picked. Every slot has its own marker.</summary>
    public bool Markers = true;
    public int PickedCell = -1;
    /// <summary>On the whole figure, a small marker for every slot on this side of it (otherwise only a zoomed region's).</summary>
    public bool ShowAll = true;
    /// <summary>Slots to light (a piece's cells while it's chosen).</summary>
    public readonly HashSet<int> Lit = new();
    /// <summary>Compact: the main screen's small figure (no markers, a lighter outline).</summary>
    public bool Small;
    /// <summary>A line of help at the top left (how to move the camera), on a dark strip so it reads over the figure.</summary>
    public string Hint;

    public event Action<string> RegionClicked;
    public event Action<int> CellClicked;

    float _t;
    readonly Dictionary<string, float> _flash = new();
    readonly Dictionary<string, Color> _flashColor = new();
    // What's drawn: pieces that count, by when they're drawn; what fills and keeps free each cell; each host's pieces set
    // in its boards (deeper ones under the piece on the body they're on).
    readonly List<int> _pieces = new();
    int[] _fillOf, _keepOf;
    readonly Dictionary<int, List<int>> _modsOf = new();
    readonly List<(Vector2 At, int Cell)> _markers = new();
    // Where each marker is drawn: at its slot, pushed clear of the others (worked out again when the camera moves), and
    // the one under the mouse.
    readonly List<Vector2> _markerScreen = new();
    (float Zoom, Vector2 Focus, bool Back, Vector2 Size, int Count) _laidOut;
    int _hoverMarker = -1;
    Vector2 _drag;
    bool _dragging, _dragged;

    public PaperDoll(Session s)
    {
        _s = s;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        Refresh();
    }

    Hero H => _s.H;
    Content C => _s.C;

    // ------------------------------------------------------------------------------------------------ camera

    float Fit => Mathf.Min(Size.X / 640f, Size.Y / (Figure.Bottom - Figure.Top + 20));
    float K => Fit * Zoom;

    /// <summary>A figure point (front view) on the screen.</summary>
    public Vector2 ToScreen(Vector2 f) => (Figure.Mirror(f, Back) - Figure.Mirror(Focus, Back)) * K + Size / 2;
    public Vector2 ToFigure(Vector2 screen) => Figure.Mirror((screen - Size / 2) / K + Figure.Mirror(Focus, Back), Back);
    /// <summary>A figure point in the screen's coordinates (for things flying to it).</summary>
    public Vector2 GlobalOf(Vector2 f) => GetGlobalTransformWithCanvas() * ToScreen(f);

    /// <summary>Moves the camera to frame a region (or the whole figure for null), turning round for the back.</summary>
    public void Frame(string region, bool instant = false)
    {
        Region = region;
        var rect = region == null ? new Rect2(-320, Figure.Top, 640, Figure.Bottom - Figure.Top) : Figure.RegionRect(region);
        if (region == "Back") Back = true;
        else if (region != null && region != "Beyond") Back = false;
        float zoom = region == null ? 1 : Mathf.Clamp(Mathf.Min(Size.X * 0.9f / rect.Size.X, Size.Y * 0.86f / rect.Size.Y) / Mathf.Max(0.01f, Fit), 1, 9);
        FlyTo(rect.GetCenter(), zoom, instant);
        BuildMarkers();
    }

    void FlyTo(Vector2 focus, float zoom, bool instant)
    {
        _fromFocus = Focus;
        _fromZoom = Zoom;
        _toFocus = focus;
        _toZoom = zoom;
        _move = instant ? 1 : 0;
        if (instant)
        {
            Focus = focus;
            Zoom = zoom;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_move < 1)
        {
            _move = Mathf.Min(1, _move + (float)delta / 0.45f);
            float e = 1 - Mathf.Pow(1 - _move, 3);
            // Zoom in log space so it feels even.
            Zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(_fromZoom), Mathf.Log(_toZoom), e));
            Focus = _fromFocus.Lerp(_toFocus, e);
        }
        foreach (var k in _flash.Keys.ToList())
        {
            _flash[k] -= (float)delta * 1.6f;
            if (_flash[k] <= 0) _flash.Remove(k);
        }
        QueueRedraw();
    }

    /// <summary>Lights a site for a moment (a piece just put on or taken off there).</summary>
    public void FlashSite(string site, Color color)
    {
        _flash[site] = 1;
        _flashColor[site] = color;
    }

    /// <summary>Re-reads what's worn (after anything changes it).</summary>
    public void Refresh()
    {
        _pieces.Clear();
        _modsOf.Clear();
        _fillOf ??= new int[C.Cells.Count];
        _keepOf ??= new int[C.Cells.Count];
        Array.Fill(_fillOf, -1);
        Array.Fill(_keepOf, -1);
        for (int s = 0; s < C.SlotCount; s++)
        {
            if (!H.Active[s]) continue;
            _pieces.Add(s);
            var d = C.Slots[s];
            foreach (int c in d.Takes) _fillOf[c] = s;
            foreach (int c in d.Keeps) _keepOf[c] = s;
            if (d.Board == Anatomy.Body) continue;
            int host = H.HostOf[d.Board];
            for (int guard = 0; guard < 6 && host >= 0 && C.Slots[host].Board != Anatomy.Body; guard++) host = H.HostOf[C.Slots[host].Board];
            if (host < 0) continue;
            if (!_modsOf.TryGetValue(host, out var list)) _modsOf[host] = list = new List<int>();
            list.Add(s);
        }
        // Drawn in order of their layer (the deepest first), bigger pieces under smaller ones.
        _pieces.Sort((a, b) =>
        {
            int la = DrawOrder(a), lb = DrawOrder(b);
            return la != lb ? la.CompareTo(lb) : C.Slots[b].Takes.Length.CompareTo(C.Slots[a].Takes.Length);
        });
        BuildMarkers();
    }

    int DrawOrder(int s)
    {
        var d = C.Slots[s];
        if (d.Board != Anatomy.Body) return 20 + d.Level;
        return C.Cells[d.Takes[0]].Layer switch
        {
            Layer.Skin => 1, Layer.Under => 2, Layer.Mail => 3, Layer.Armor => 4, Layer.Over => 5, Layer.Adorn => 7,
            Layer.Hold => 8, Layer.Graft => 9, _ => 10,
        };
    }

    // ------------------------------------------------------------------------------------------------ markers

    /// <summary>A marker for each slot that shows: a zoomed region's, or on the whole figure every slot on this side.</summary>
    void BuildMarkers()
    {
        _markers.Clear();
        _laidOut = default;
        _hoverMarker = -1;
        if (Small || !Markers || (Region == null && !ShowAll)) return;
        var perSite = new Dictionary<string, int>();
        foreach (var cell in C.Cells)
            if (cell.Board == Anatomy.Body) perSite[cell.Site] = perSite.GetValueOrDefault(cell.Site) + 1;
        foreach (var cell in C.Cells)
        {
            if (cell.Board != Anatomy.Body || (Region != null && cell.Region != Region) || !Figure.Marked(cell.Site, Back)) continue;
            _markers.Add((Figure.MarkerSpot(cell, perSite[cell.Site]), cell.Id));
        }
    }

    /// <summary>Shows or hides the whole figure's slot markers.</summary>
    public void ShowSlots(bool on)
    {
        ShowAll = on;
        BuildMarkers();
    }

    /// <summary>Where a cell is on the screen, in the screen's coordinates (for flying things to it): its marker if it has
    /// one, else its spot on the figure.</summary>
    public Vector2 GlobalCellPoint(int cell)
    {
        for (int i = 0; i < _markers.Count && i < _markerScreen.Count; i++)
            if (_markers[i].Cell == cell) return GetGlobalTransformWithCanvas() * _markerScreen[i];
        return GlobalOf(Figure.Anchor(C.Cells[cell]));
    }

    /// <summary>A marker's place kept on the stage, clear of the buttons along the top and the legend along the bottom.</summary>
    Vector2 Inside(Vector2 p) => new(Mathf.Clamp(p.X, MarkerRadius + 8, Mathf.Max(MarkerRadius + 8, Size.X - MarkerRadius - 8)),
        Mathf.Clamp(p.Y, 64 + MarkerRadius, Mathf.Max(64 + MarkerRadius, Size.Y - 62 - MarkerRadius)));

    /// <summary>Lays the markers out: each at its slot, then pushed apart until no two overlap (a line leads back to the
    /// slot from one that moved). Only when the camera or the markers change.</summary>
    void LayoutMarkers()
    {
        var key = (Zoom, Focus, Back, Size, _markers.Count);
        if (key == _laidOut && _markerScreen.Count == _markers.Count) return;
        _laidOut = key;
        _markerScreen.Clear();
        foreach (var (at, _) in _markers) _markerScreen.Add(Inside(ToScreen(at)));
        float min = MarkerRadius * 2 + (Overview ? 3 : 10);
        for (int pass = 0; pass < (Overview ? 16 : 40); pass++)
        {
            bool moved = false;
            for (int i = 0; i < _markerScreen.Count; i++)
                for (int j = i + 1; j < _markerScreen.Count; j++)
                {
                    var d = _markerScreen[j] - _markerScreen[i];
                    float l = d.Length();
                    if (l >= min) continue;
                    // Markers on the same spot part along a fixed direction, so the layout doesn't flicker.
                    var push = l < 0.5f ? Vector2.FromAngle(0.7f + 2.4f * j) : d / l;
                    _markerScreen[i] = Inside(_markerScreen[i] - push * (min - l) / 2);
                    _markerScreen[j] = Inside(_markerScreen[j] + push * (min - l) / 2);
                    moved = true;
                }
            if (!moved) break;
        }
    }

    /// <summary>What fills a cell now (the piece counting there), or -1.</summary>
    public int Filling(int cell) => _fillOf[cell];

    /// <summary>Which counting piece keeps a cell free, or -1.</summary>
    public int Keeping(int cell) => _keepOf[cell];

    // ------------------------------------------------------------------------------------------------ input

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseMotion m:
                if (_dragging)
                {
                    var d = m.Relative / K;
                    if (d.Length() > 0.5f) _dragged = true;
                    Focus -= Back ? new Vector2(-d.X, d.Y) : d;
                    _move = 1;
                    AcceptEvent();
                    return;
                }
                Hover = Region == null && !Small ? Figure.RegionAt(ToFigure(m.Position), Back) : null;
                _hoverMarker = MarkerAt(m.Position);
                MouseDefaultCursorShape = Hover != null || _hoverMarker >= 0 ? CursorShape.PointingHand : CursorShape.Arrow;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } w when !Small:
                ZoomAt(w.Position, 1.18f);
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } w when !Small:
                ZoomAt(w.Position, 1 / 1.18f);
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } b:
                if (b.Pressed)
                {
                    _dragging = !Small && Region != null;
                    _dragged = false;
                    _drag = b.Position;
                }
                else
                {
                    _dragging = false;
                    if (_dragged) break;
                    int marker = MarkerAt(b.Position);
                    if (marker >= 0)
                    {
                        // A slot on the whole figure: into its region first, then the slot.
                        int cell = _markers[marker].Cell;
                        if (Region == null) RegionClicked?.Invoke(C.Cells[cell].Region);
                        PickedCell = cell;
                        CellClicked?.Invoke(cell);
                    }
                    else
                    {
                        string r = Figure.RegionAt(ToFigure(b.Position), Back);
                        if (r != null && r != Region) RegionClicked?.Invoke(r);
                    }
                }
                AcceptEvent();
                break;
        }
    }

    void ZoomAt(Vector2 screen, float factor)
    {
        var before = ToFigure(screen);
        Zoom = Mathf.Clamp(Zoom * factor, 0.8f, 12);
        var after = ToFigure(screen);
        Focus += before - after;
        _move = 1;
    }

    /// <summary>The marker under a point (the nearest within reach), or -1.</summary>
    int MarkerAt(Vector2 screen)
    {
        int best = -1;
        float reach = MarkerRadius + (Overview ? 3 : 4), bestD = reach;
        for (int i = 0; i < _markers.Count && i < _markerScreen.Count; i++)
        {
            float d = _markerScreen[i].DistanceTo(screen);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Every slot on the whole figure, as small markers.</summary>
    bool Overview => Region == null;
    float MarkerRadius => Overview ? 5.5f : 15;

    // ------------------------------------------------------------------------------------------------ drawing helpers (figure space in, screen out)

    float Px(float figureUnits) => figureUnits * K;
    float Line => Small ? 1.4f : Mathf.Clamp(1.6f + 0.25f * Zoom, 1.8f, 3.2f);

    Vector2[] S(Vector2[] pts)
    {
        var o = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++) o[i] = ToScreen(pts[i]);
        return o;
    }

    void Fill(Vector2[] fig, Color color, bool outline = true)
    {
        if (fig == null || fig.Length < 3) return;
        var pts = S(fig);
        DrawColoredPolygon(pts, color);
        if (outline) Ink.Outline(this, pts, Line);
    }

    void Shade(Vector2[] fig, Ink.Ramp r, bool outline = true)
    {
        if (fig == null || fig.Length < 3) return;
        var pts = S(fig);
        var centre = Ink.Centre(pts);
        float k = Mathf.Clamp(Px(3), 1, 6);
        DrawColoredPolygon(pts, r.Shadow);
        DrawColoredPolygon(Ink.Scale(pts, centre, 0.86f, new Vector2(-k, -k)), r.Mid);
        DrawColoredPolygon(Ink.Scale(pts, centre, 0.55f, new Vector2(-2 * k, -2 * k)), r.Light.Lerp(r.Mid, 0.45f));
        if (outline) Ink.Outline(this, pts, Line);
    }

    void Edge(Vector2[] fig, Color color, float width = 0, float inset = 0.9f)
    {
        if (fig == null || fig.Length < 3) return;
        var pts = S(fig);
        var c = Ink.Centre(pts);
        var e = Ink.Scale(pts, c, inset, Vector2.Zero);
        var closed = new Vector2[e.Length + 1];
        Array.Copy(e, closed, e.Length);
        closed[^1] = e[0];
        DrawPolyline(closed, color, width > 0 ? width : Mathf.Max(1.2f, Line * 0.8f), true);
    }

    void Dot(Vector2 fig, float radius, Color color, bool outline = true)
    {
        var p = ToScreen(fig);
        float r = Mathf.Max(1.2f, Px(radius));
        DrawCircle(p, r, color, true, -1, true);
        if (outline && r > 2) DrawArc(p, r, 0, Mathf.Tau, 14, Look.Ink, Mathf.Max(1, Line * 0.6f), true);
    }

    void Seg(Vector2 a, Vector2 b, Color color, float figWidth, bool screenMin = true)
    {
        float w = Px(figWidth);
        if (screenMin) w = Mathf.Max(1.2f, w);
        DrawLine(ToScreen(a), ToScreen(b), color, w, true);
    }

    void Glow(Vector2 fig, float radius, Color color)
    {
        var p = ToScreen(fig);
        for (int i = 3; i >= 1; i--) DrawCircle(p, Px(radius) * (0.5f + 0.25f * i), new Color(color, 0.12f), true, -1, true);
    }

    static Vector2[] Inflate(Vector2[] pts, float k)
    {
        if (pts == null) return null;
        return Ink.Scale(pts, Ink.Centre(pts), k, Vector2.Zero);
    }

    // ------------------------------------------------------------------------------------------------ the whole

    public override void _Draw()
    {
        // The floor and a soft spotlight.
        var floor = ToScreen(new Vector2(0, 985));
        DrawSetTransform(floor, 0, new Vector2(1, 0.16f));
        DrawCircle(Vector2.Zero, Px(230), new Color(0, 0, 0, 0.32f), true, -1, true);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawBackdrop();
        DrawBehind(false);
        DrawBody();
        foreach (int s in _pieces) DrawPiece(s);
        DrawBehind(true);
        DrawFront();
        foreach (var (site, k) in _flash)
        {
            var shape = Figure.Site(site);
            var at = shape != null ? Ink.Centre(shape) : Figure.SiteAnchor(site);
            Glow(at, 60, new Color(_flashColor[site], k));
            if (shape != null) Edge(shape, new Color(_flashColor[site], k), 3, 1.08f);
        }
        // The chosen piece's cells, outlined over everything (seen through what covers them).
        foreach (int c in Lit)
        {
            var cell = C.Cells[c];
            if (cell.Board != Anatomy.Body || !(Back ? !Figure.FrontOnly(cell.Site) : !Figure.BackOnly(cell.Site))) continue;
            var shape = Figure.Site(cell.Site);
            if (shape != null) Edge(Inflate(shape, 1.12f), new Color(Look.Gold, 0.6f + 0.4f * Mathf.Sin(_t * 4)), 3, 1);
            else Glow(Figure.Anchor(cell), 14, Look.Gold);
        }
        if (Region == null && Hover != null && !Small) DrawRegionHint(Hover);
        if (_markers.Count > 0)
        {
            LayoutMarkers();
            DrawMarkers();
            DrawLegend();
            if (_hoverMarker >= 0 && _hoverMarker < _markers.Count) DrawSlotName(_hoverMarker);
        }
        if (!string.IsNullOrEmpty(Hint) && !Small) DrawHint();
    }

    /// <summary>A dark strip behind help text, so it reads over whatever the camera shows.</summary>
    void Strip(Rect2 r) => DrawStyleBox(Ui.Frame(new Color("#0d0a13", 0.86f), new Color(Look.Edge, 0.8f), 8, 1, 0, 0), r);

    void DrawHint()
    {
        var font = Look.Ui;
        float w = font.GetStringSize(Hint, HorizontalAlignment.Left, -1, 16).X;
        Strip(new Rect2(12, 12, w + 24, 32));
        DrawString(font, new Vector2(24, 34), Hint, HorizontalAlignment.Left, -1, 16, Look.Muted);
    }

    /// <summary>What the markers mean, along the bottom.</summary>
    void DrawLegend()
    {
        var items = new (Color Ring, Color Face, string Label, string Text)[]
        {
            (Look.Tier(4), new Color(Look.Tier(4), 0.35f), "", "worn"),
            (Look.Text, Panel, "+", "free"),
            (Look.Bad, Panel, "x", "covered by a piece"),
            (Look.Dim, Panel, "-", "not open yet"),
        };
        var font = Look.Ui;
        float x = 24, y = Size.Y - 30, width = 12;
        foreach (var it in items) width += 34 + font.GetStringSize(it.Text, HorizontalAlignment.Left, -1, 16).X + 22;
        Strip(new Rect2(12, Size.Y - 52, width, 44));
        foreach (var (ring, face, label, text) in items)
        {
            var p = new Vector2(x + 12, y);
            DrawCircle(p, 12, face, true, -1, true);
            DrawArc(p, 12, 0, Mathf.Tau, 20, ring, 2, true);
            if (label.Length > 0) DrawString(Look.Numbers, p + new Vector2(-14, 5), label, HorizontalAlignment.Center, 28, 14, Look.Text);
            else Tick(p, 5);
            x += 30;
            DrawString(font, new Vector2(x, y + 6), text, HorizontalAlignment.Left, -1, 16, Look.Muted);
            x += font.GetStringSize(text, HorizontalAlignment.Left, -1, 16).X + 26;
        }
    }

    static readonly Color Panel = new("#1a1524");

    /// <summary>A tick: something's worn here.</summary>
    void Tick(Vector2 p, float k) =>
        DrawPolyline(new[] { p + new Vector2(-k, 0), p + new Vector2(-k * 0.3f, k * 0.75f), p + new Vector2(k, -k * 0.65f) }, Colors.White, 2.6f, true);

    void DrawRegionHint(string region)
    {
        var r = Figure.RegionRect(region);
        var a = ToScreen(r.Position);
        var b = ToScreen(r.End);
        var rect = new Rect2(new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)), (b - a).Abs());
        DrawStyleBox(Ui.Frame(new Color(Look.Gold, 0.08f), new Color(Look.Gold, 0.7f), 12, 2, 0, 0), rect.Grow(4));
        DrawString(Look.UiBold, rect.Position + new Vector2(6, -8), region.ToUpperInvariant(), HorizontalAlignment.Left, -1, 18, Look.Gold);
    }

    void DrawMarkers()
    {
        // Leader lines first, from markers that were pushed aside back to their slots.
        if (!Overview)
            for (int m = 0; m < _markers.Count; m++)
            {
                var anchor = ToScreen(_markers[m].At);
                if (anchor.DistanceTo(_markerScreen[m]) < 6) continue;
                DrawLine(anchor, _markerScreen[m], new Color(Look.Ink, 0.85f), 3.5f, true);
                DrawLine(anchor, _markerScreen[m], new Color(Look.Muted, 0.9f), 1.5f, true);
                DrawCircle(anchor, 3.5f, Look.Ink, true, -1, true);
                DrawCircle(anchor, 2.2f, Look.Muted, true, -1, true);
            }
        for (int m = 0; m < _markers.Count; m++) DrawMarkerAt(m, _markerScreen[m]);
    }

    /// <summary>A slot's marker: what's worn there (a disc in its rarity, with a tick), or why nothing is: + free, x covered
    /// by a piece, - not open yet. On the whole figure, a small dot in the same colours.</summary>
    void DrawMarkerAt(int m, Vector2 p)
    {
        int cell = _markers[m].Cell;
        int f = Filling(cell);
        bool worn = f >= 0, kept = !worn && Keeping(cell) >= 0, locked = !worn && !kept && !AnyOpen(cell);
        var ring = worn ? Look.Tier(H.Gear[f].Rarity) : kept ? Look.Bad : locked ? Look.Dim : Look.Text;
        bool picked = cell == PickedCell, hover = m == _hoverMarker;
        float r = MarkerRadius * (picked || hover ? 1.18f : 1);
        if (Overview)
        {
            DrawCircle(p, r + 1.5f, new Color(Look.Ink, 0.85f), true, -1, true);
            DrawCircle(p, r, worn ? ring : locked ? new Color(Panel, 0.85f) : Panel, true, -1, true);
            if (!worn) DrawArc(p, r, 0, Mathf.Tau, 12, ring, 1.6f, true);
            return;
        }
        DrawCircle(p, r + 3, new Color(Look.Ink, 0.75f), true, -1, true);
        DrawCircle(p, r, worn ? new Color(ring, 0.35f) : Panel, true, -1, true);
        DrawArc(p, r, 0, Mathf.Tau, 24, ring, picked ? 3.5f : 2, true);
        if (worn) Tick(p, 6);
        else DrawString(Look.Numbers, p + new Vector2(-r, 6), kept ? "x" : locked ? "-" : "+", HorizontalAlignment.Center, r * 2, 15, Look.Text);
    }

    /// <summary>The slot under the mouse, named next to its marker (and what's worn there).</summary>
    void DrawSlotName(int m)
    {
        int cell = _markers[m].Cell;
        int f = Filling(cell);
        var c = C.Cells[cell];
        string text = (Overview ? c.SiteName + ": " : "") + c.Name + (f >= 0 && H.Gear[f] != null ? " - " + Describe.Name(C, f, H.Gear[f]) : "");
        Label(_markerScreen[m] + new Vector2(0, -MarkerRadius - 12), text, f >= 0 && H.Gear[f] != null ? Look.TierText(H.Gear[f].Rarity) : Look.Text);
    }

    /// <summary>A short line of text centred at a point, on a dark strip.</summary>
    void Label(Vector2 centre, string text, Color colour)
    {
        var font = Look.UiBold;
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, 17).X;
        var at = new Vector2(Mathf.Clamp(centre.X - w / 2, 8, Mathf.Max(8, Size.X - w - 8)), centre.Y);
        Strip(new Rect2(at + new Vector2(-10, -20), new Vector2(w + 20, 28)));
        DrawString(font, at, text, HorizontalAlignment.Left, -1, 17, colour);
    }

    bool AnyOpen(int cell)
    {
        foreach (var s in C.Slots)
            if (s.Board == Anatomy.Body && Array.IndexOf(s.Takes, cell) >= 0 && H.Open[s.Index]) return true;
        return false;
    }

    // ------------------------------------------------------------------------------------------------ the body

    static readonly Color Linen = new("#b9ad9b"), LinenDark = new("#8f8270");

    void DrawBody()
    {
        var skin = Ink.Skin;
        // Legs, then the torso (a plain linen undershirt and shorts), then arms and hands, the neck and the head.
        foreach (float s in new[] { -1f, 1f })
        {
            Shade(Ink.Capsule(Figure.Knee(s), Figure.Ankle(s), 25, 18), skin);
            Shade(Ink.Capsule(Figure.Hip(s), Figure.Knee(s), 34, 26), skin);
            var (foot, toes, radii) = Figure.Foot(s);
            Shade(foot, skin);
            for (int t = 0; t < 5; t++) Fill(Ink.Ellipse(toes[t], radii[t], radii[t], 10), skin.Mid);
        }
        Shade(Figure.Torso(), new Ink.Ramp(LinenDark, Linen, new Color("#ddd3c4")));
        Fill(new[] { new Vector2(-84, 466), new Vector2(84, 466), new Vector2(88, 540), new Vector2(10, 548), new Vector2(0, 520), new Vector2(-10, 548), new Vector2(-88, 540) }, LinenDark);
        foreach (float s in new[] { -1f, 1f })
        {
            Shade(Ink.Capsule(Figure.Shoulder(s), Figure.Elbow(s), 26, 21), skin);
            Shade(Ink.Capsule(Figure.Elbow(s), Figure.Wrist(s), 21, 16), skin);
            DrawHand(s, skin.Mid, skin.Shadow, 1);
        }
        Shade(Figure.Site("neck"), skin);
        DrawHead();
    }

    void DrawHand(float s, Color fill, Color shadow, float fingerLength, bool outline = true)
    {
        var h = Figure.Hand(s);
        for (int f = 0; f < 5; f++)
        {
            var tip = h.Base[f].Lerp(h.Tip[f], fingerLength);
            Fill(Ink.Capsule(h.Base[f], tip, h.Width[f], h.Width[f] * 0.9f, 4), f == 0 ? shadow.Lerp(fill, 0.5f) : fill, outline);
        }
        Fill(h.Palm, fill, outline);
    }

    void DrawHead()
    {
        var skin = Ink.Skin;
        var hair = Ink.Hair;
        if (Back)
        {
            Shade(Figure.HeadShape(), hair);
            return;
        }
        foreach (float s in new[] { -1f, 1f }) Fill(Ink.Ellipse(new Vector2(s * 52, 104), 9, 16, 12), skin.Mid);
        Shade(Figure.HeadShape(), skin);
        // Hair over the top, unless a helm or a hood covers it.
        if (!Covered("scalp.head")) Shade(Figure.Site("hair"), hair);
        // The face.
        foreach (float s in new[] { -1f, 1f })
        {
            Fill(Ink.Ellipse(new Vector2(s * 19, 102), 8, 5, 12), new Color("#f4efe6"));
            Dot(new Vector2(s * 19 + 1, 102), 3.2f, new Color("#2c2140"), false);
            Seg(new Vector2(s * 11, 89), new Vector2(s * 29, 87), hair.Shadow, 3.5f);
        }
        Seg(new Vector2(0, 108), new Vector2(-4, 124), skin.Shadow, 2.5f);
        Seg(new Vector2(-4, 124), new Vector2(4, 125), skin.Shadow, 2.5f);
        Seg(new Vector2(-11, 142), new Vector2(11, 142), new Color("#7a3b3b"), 3);
    }

    /// <summary>Whether a body cell is filled by something that counts now.</summary>
    bool Covered(string cellKey) => _fillOf[C.Anatomy.CellId(cellKey)] >= 0;
}
