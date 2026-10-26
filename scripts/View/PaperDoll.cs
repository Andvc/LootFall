using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The knight on the equipment screen (docs/equipment.md, "The figure"): the delivered art, front and back side by side
/// for the whole body, and each region's page (the head, hands and feet on their close-ups, the rest on the figure, both
/// sides where both have slots). What's worn is painted over the sites it covers (<see cref="Coverage"/>, the figure
/// shader) with an ink line round it; every slot that shows has a marker, a worn piece one marker for all the slots it
/// fills, with its own slots on it, and what it covers or keeps free doesn't show. Pointing at anything lights its
/// area. Pages change by zooming: a region's page grows out of its place on the whole figure (a close-up out of the
/// head, hand or foot, its other views unfolding beside it), and shrinks back into it; the wheel zooms in and out of
/// pages too.
/// </summary>
public partial class PaperDoll : Control
{
    readonly Session _s;
    public readonly Coverage Cov;
    /// <summary>The main screen's small figure: the front only, no markers.</summary>
    public bool Small;
    /// <summary>The region whose page shows (null: the whole body).</summary>
    public string Region { get; private set; }
    /// <summary>Whether the whole body shows every slot (the overview's dots).</summary>
    public bool ShowAll = true;
    /// <summary>The slot chosen in the panel; the piece shown in detail (the rest dims); a piece pointed at in the
    /// panel's lists (its area lit); a slot pointed at there; a piece shown in detail that isn't worn (where it would go).</summary>
    public int PickedCell = -1, FocusPiece = -1, PreviewPiece = -1, PointedCell = -1, GhostPiece = -1;
    /// <summary>A region pointed at in the panel's list (lit on the whole body).</summary>
    public string PointedRegion;
    /// <summary>A line of help at the top left.</summary>
    public string Hint;

    public event Action<string> RegionClicked;
    public event Action<int> CellClicked;
    public event Action<int> PieceClicked;
    /// <summary>A step back asked for: a right click, or the wheel turned out past a page.</summary>
    public event Action BackClicked;
    /// <summary>A click on nothing (the stage round the figure).</summary>
    public event Action NothingClicked;

    Hero H => _s.H;
    Content C => _s.C;

    readonly Dictionary<string, string> _regionOf = new();
    // The body's slots by key (the map names them; one the content no longer has is left out).
    readonly Dictionary<string, int> _cellIds = new();
    readonly List<Shown> _shown = new();
    readonly List<Marker> _markers = new();
    // The page before's markers, fading out with it while the page changes.
    readonly List<Marker> _leaving = new();
    readonly Control _top;
    float _t, _move = 1;
    const float MoveTime = 0.6f;
    float _zoom = 1;
    Vector2 _pan;
    Vector2 _mouse = new(-1, -1);
    string _hoverRegion;
    (Shown P, int Site) _hoverSite;
    int _hoverMarker = -1, _hoverPiece = -1, _outPull;
    double _lastWheel;
    bool _dragging, _dragged, _markersDirty = true;
    readonly Dictionary<string, (float K, Color C)> _flash = new();

    public PaperDoll(Session s)
    {
        _s = s;
        Cov = new Coverage(s.C, s.H);
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        foreach (var c in s.C.Cells)
            if (c.Board == Anatomy.Body)
            {
                _regionOf[c.Site] = c.Region;
                _cellIds[c.Key] = c.Id;
            }
        _top = new Canvas(DrawTop) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_top);
        Resized += () =>
        {
            _top.Size = Size;
            Relayout();
        };
    }

    public override void _Ready()
    {
        _top.Size = Size;
        if (_shown.Count == 0) Show(Region, true);
    }

    // ------------------------------------------------------------------------------------------------ pages and panels

    /// <summary>Where a panel is: the screen rectangle it fills, its scale (screen pixels per art pixel), the art point at
    /// its middle, how visible it is.</summary>
    sealed class Place
    {
        public Rect2 Clip;
        public float Scale = 1, Alpha = 1;
        public Vector2 Anchor;

        public Vector2 Offset => Clip.GetCenter() - Anchor * Scale;
        public Vector2 Map(Vector2 tex) => Offset + tex * Scale;
        public Vector2 Unmap(Vector2 screen) => (screen - Offset) / Scale;

        public static Place Lerp(Place a, Place b, float t) => new()
        {
            Clip = new Rect2(a.Clip.Position.Lerp(b.Clip.Position, t), a.Clip.Size.Lerp(b.Clip.Size, t)),
            Scale = Mathf.Exp(Mathf.Lerp(Mathf.Log(a.Scale), Mathf.Log(b.Scale), t)),
            Anchor = a.Anchor.Lerp(b.Anchor, t), Alpha = Mathf.Lerp(a.Alpha, b.Alpha, t),
        };
    }

    /// <summary>When in a page change a panel fades (as a share of the move): in, in late (a close-up's other views,
    /// unfolding once the main one has grown), out, out early.</summary>
    static readonly Vector2 FadeIn = new(0.12f, 0.55f), FadeInLate = new(0.42f, 0.9f), FadeOut = new(0.3f, 0.8f), FadeOutEarly = new(0f, 0.4f);

    /// <summary>A view on screen: its picture (drawn through the figure shader), where it was, is going and is.</summary>
    sealed class Shown
    {
        public MapView V;
        public FigurePanel Node;
        public Place From, To, Now;
        public float AlphaFrom, AlphaTo = 1;
        public Vector2 Fade = FadeIn;
        public bool Leaving;
        // Outlines mapped to the screen, kept while the panel doesn't move.
        public readonly Dictionary<Vector2[], (int Version, Vector2[] Dst)> Mapped = new();
        // The pieces drawn by pictures of their own here (no fill, no ink line round their sites), and those pictures
        // with the piece each is drawn for and where it lies in this view's pixels, innermost first.
        public readonly HashSet<int> ArtPieces = new();
        public readonly List<(PieceArt Art, int Piece, Rect2 Rect)> Art = new();
        // The layers painted again among the pictures (their ink lines go with them).
        public readonly HashSet<int> Passes = new();
        public int Version, LinesDrawn;
        public Vector2 MappedOffset;
        public float MappedScale;
    }

    // Each close-up grows out of the figure: the view of the figure it's zoomed from, the sites matched up on both, and
    // whether it's the main view (the others unfold from the same place once it's grown).
    static readonly Dictionary<string, (string Figure, string[] Sites, bool Main)> Origins = new()
    {
        ["head_front"] = ("front", new[] { "scalp", "chin", "R.ear", "L.ear" }, true),
        ["head_right"] = ("front", new[] { "scalp", "chin" }, false), ["head_left"] = ("front", new[] { "scalp", "chin" }, false),
        ["hand_R_back"] = ("front", Hand("R", "hand"), true), ["hand_R_palm"] = ("back", Hand("R", "palm"), false),
        ["hand_L_back"] = ("front", Hand("L", "hand"), true), ["hand_L_palm"] = ("back", Hand("L", "palm"), false),
        ["foot_R_top"] = ("front", new[] { "R.foot", "R.ankle" }, true), ["foot_R_sole"] = ("back", new[] { "R.heel", "R.ankle" }, false),
        ["foot_L_top"] = ("front", new[] { "L.foot", "L.ankle" }, true), ["foot_L_sole"] = ("back", new[] { "L.heel", "L.ankle" }, false),
    };

    static string[] Hand(string side, string back) =>
        new[] { $"{side}.{back}", $"{side}.thumb", $"{side}.index", $"{side}.middle", $"{side}.ring", $"{side}.little", $"{side}.wrist" };

    static Rect2 Box(MapView v, IEnumerable<string> sites)
    {
        Rect2? box = null;
        foreach (var s in sites)
            if (v.Index.TryGetValue(s, out int id))
                box = box is { } b ? b.Merge(v.Sites[id].Box) : v.Sites[id].Box;
        return box ?? new Rect2(Vector2.Zero, v.Size);
    }

    /// <summary>The stage the panels are laid out in: below the buttons along the top, above the legend.</summary>
    Rect2 StageArea => Small ? new Rect2(Vector2.Zero, Size) : new Rect2(18, 66, Size.X - 36, Size.Y - 66 - 60);

    List<MapPanel> Page() => Small ? new List<MapPanel> { EquipmentMap.Page(null)[0] } : EquipmentMap.Page(Region);

    /// <summary>Where a page's panels go: side by side at one scale, as big as the stage allows, centred.</summary>
    List<(MapPanel P, Place At)> Layout(List<MapPanel> page)
    {
        var area = StageArea;
        float gap = page.Count > 1 ? 36 : 0;
        float w = page.Sum(p => p.Rect.Size.X), h = page.Max(p => p.Rect.Size.Y);
        float scale = Mathf.Min((area.Size.X - gap * (page.Count - 1)) / Mathf.Max(1, w), area.Size.Y / Mathf.Max(1, h));
        float x = area.Position.X + (area.Size.X - (w * scale + gap * (page.Count - 1))) / 2;
        var list = new List<(MapPanel, Place)>();
        foreach (var p in page)
        {
            var size = p.Rect.Size * scale;
            var clip = new Rect2(x, area.Position.Y + (area.Size.Y - size.Y) / 2, size);
            list.Add((p, new Place { Clip = clip, Scale = scale, Anchor = p.Rect.GetCenter() }));
            x += size.X + gap;
        }
        return list;
    }

    /// <summary>Where a close-up sits inside the figure, given where the figure is (so it grows out of it).</summary>
    static Place Inside(MapView closeup, string[] sites, Place figure, MapView fig)
    {
        var inFig = Box(fig, sites);
        var mine = Box(closeup, sites);
        var screen = new Rect2(figure.Map(inFig.Position), inFig.Size * figure.Scale);
        float scale = screen.Size.Y / Mathf.Max(1, mine.Size.Y);
        var clipSize = closeup.Size * scale;
        var centre = screen.GetCenter() + (closeup.Size / 2 - mine.GetCenter()) * scale;
        return new Place { Clip = new Rect2(centre - clipSize / 2, clipSize), Scale = scale, Anchor = closeup.Size / 2, Alpha = 0 };
    }

    /// <summary>Where the figure sits round a close-up (the figure zoomed so its head, hand or foot is where the close-up
    /// shows it).</summary>
    static Place Around(MapView fig, string[] sites, Place closeup, MapView close)
    {
        var inFig = Box(fig, sites);
        var mine = Box(close, sites);
        var screen = new Rect2(closeup.Map(mine.Position), mine.Size * closeup.Scale);
        float scale = screen.Size.Y / Mathf.Max(1, inFig.Size.Y);
        var clipSize = fig.Size * scale;
        var centre = screen.GetCenter() + (fig.Size / 2 - inFig.GetCenter()) * scale;
        return new Place { Clip = new Rect2(centre - clipSize / 2, clipSize), Scale = scale, Anchor = fig.Size / 2, Alpha = 0 };
    }

    /// <summary>A place moved as the camera moves a guide panel from one place to another (the same zoom and pan).</summary>
    static Place Moved(Place q, Place from, Place to, float alpha)
    {
        float s = to.Scale / from.Scale;
        Vector2 M(Vector2 x) => to.Offset + (x - from.Offset) * s;
        return new Place { Clip = new Rect2(M(q.Clip.Position), q.Clip.Size * s), Scale = q.Scale * s, Anchor = q.Anchor, Alpha = alpha };
    }

    /// <summary>Where a view of the figure's space sits so it lines up exactly with another of that space at a place
    /// (a plate over its region of the figure, the figure round a plate, one plate beside another).</summary>
    static Place Aligned(MapView b, Place at, MapView a)
    {
        var corner = a.FromSpace(b.ToSpace(Vector2.Zero));
        float scale = at.Scale * a.SpaceScale / b.SpaceScale;
        var offset = at.Offset + corner * at.Scale;
        return new Place { Clip = new Rect2(offset, b.Size * scale), Scale = scale, Anchor = b.Size / 2, Alpha = 0 };
    }

    static Place Grown(Place p, float k, float alpha) => new()
    {
        Clip = new Rect2(p.Clip.GetCenter() - p.Clip.Size * k / 2, p.Clip.Size * k), Scale = p.Scale * k, Anchor = p.Anchor, Alpha = alpha,
    };

    /// <summary>Shows a region's page (null: the whole body), zooming from the page before.</summary>
    public void Show(string region, bool instant = false)
    {
        Region = region;
        _outPull = 0;
        if (!IsInsideTree() || Size.X < 10)
        {
            _markersDirty = true;
            return;
        }
        var layout = Layout(Page());
        // The page before as it is now on screen (mid-move too), the camera folded in.
        foreach (var p in _shown.Where(p => p.Leaving).ToList()) Drop(p);
        foreach (var p in _shown) p.From = Cam(p.Now);
        _zoom = 1;
        _pan = Vector2.Zero;
        var old = _shown.ToList();
        var next = new List<Shown>();
        // Panels with a place of their own to come from or go to; the rest move with the camera (below).
        var placed = new HashSet<Shown>();
        foreach (var (mp, at) in layout)
        {
            var v = EquipmentMap.View(mp.View);
            var mine = old.FirstOrDefault(p => p.V == v);
            if (mine != null)
            {
                old.Remove(mine);
                mine.To = at;
                mine.AlphaFrom = mine.From.Alpha;
                mine.AlphaTo = 1;
                mine.Fade = FadeIn;
                next.Add(mine);
                placed.Add(mine);
                continue;
            }
            var p = new Shown { V = v, To = at };
            p.Node = new FigurePanel(v) { MouseFilter = MouseFilterEnum.Ignore, Size = Size };
            var shown = p;
            p.Node.Lines.OnDraw = c => DrawLines(c, shown);
            p.Node.DrawInks = (c, layer) => DrawInks(c, shown, layer);
            AddChild(p.Node);
            MoveChild(p.Node, _top.GetIndex());
            // A close-up grows out of the figure; the figure shrinks back out of the close-up it was showing.
            Place from = null;
            if (Origins.TryGetValue(v.Name, out var o) && old.FirstOrDefault(q => q.V.Name == o.Figure) is { } figure)
            {
                from = Inside(v, o.Sites, figure.From, figure.V);
                p.Fade = o.Main ? FadeIn : FadeInLate;
            }
            else if (old.FirstOrDefault(q => Origins.TryGetValue(q.V.Name, out var oq) && oq.Main && oq.Figure == v.Name) is { } close)
                from = Around(v, Origins[close.V.Name].Sites, close.From, close.V);
            // A plate grows out of the figure (or the figure round a plate, or one plate out of another) lined up exactly.
            else if (v.Space != null && old.FirstOrDefault(q => q.V.Space == v.Space) is { } same)
                from = Aligned(v, same.From, same.V);
            if (from != null) placed.Add(p);
            p.From = from;
            p.AlphaFrom = 0;
            p.AlphaTo = 1;
            next.Add(p);
        }
        // What the page before showed that this one doesn't: back into the figure, or out round the close-up.
        foreach (var p in old)
        {
            p.Leaving = true;
            Place to = null;
            p.Fade = FadeOut;
            if (Origins.TryGetValue(p.V.Name, out var o) && next.FirstOrDefault(q => q.V.Name == o.Figure) is { } figure)
            {
                to = Inside(p.V, o.Sites, figure.To, figure.V);
                p.Fade = o.Main ? FadeOut : FadeOutEarly;
            }
            else if (next.FirstOrDefault(q => Origins.TryGetValue(q.V.Name, out var oq) && oq.Main && oq.Figure == p.V.Name) is { } close)
                to = Around(p.V, Origins[close.V.Name].Sites, close.To, close.V);
            else if (p.V.Space != null && next.FirstOrDefault(q => !q.Leaving && q.V.Space == p.V.Space) is { } same)
                to = Aligned(p.V, same.To, same.V);
            if (to != null) placed.Add(p);
            p.To = to;
            p.AlphaFrom = p.From.Alpha;
            p.AlphaTo = 0;
            next.Insert(0, p);
        }
        // The camera's move: that of a figure that stays or zooms into (or out of) its close-up, else of a close-up.
        var guide = next.Where(placed.Contains).OrderBy(p => p.V.Closeup).ThenBy(p => p.Leaving).FirstOrDefault(p => p.From != null && p.To != null);
        foreach (var p in next)
        {
            if (placed.Contains(p)) continue;
            if (p.Leaving)
                p.To = guide != null ? Moved(p.From, guide.From, guide.To, 0) : Grown(p.From, 1.05f, 0);
            else
            {
                p.From = guide != null ? Moved(p.To, guide.To, guide.From, 0) : Grown(p.To, 0.94f, 0);
                p.Now = p.From;
            }
        }
        foreach (var p in next.Where(p => !p.Leaving && p.Now == null)) p.Now = p.From;
        _shown.Clear();
        _shown.AddRange(next);
        _leaving.Clear();
        if (!instant)
            foreach (var m in _markers)
            {
                // Where it showed, as an offset from its slot's point (pushed aside), kept while its panel moves.
                m.Offset = m.Screen - m.Anchor;
                _leaving.Add(m);
            }
        _move = instant ? 1 : 0;
        if (instant)
        {
            foreach (var p in _shown.Where(p => p.Leaving).ToList()) Drop(p);
            foreach (var p in _shown)
            {
                p.Now = p.To;
                p.Now.Alpha = 1;
            }
        }
        else Step(0);
        _hoverRegion = null;
        _hoverSite = default;
        _hoverMarker = _hoverPiece = -1;
        _markersDirty = true;
        _pagesVersion++;
        UpdateArt();
        UpdateLuts();
    }

    void Drop(Shown p)
    {
        _shown.Remove(p);
        p.Node?.QueueFree();
    }

    /// <summary>The page laid out again (after a resize), without a zoom.</summary>
    void Relayout()
    {
        if (_shown.Count == 0) return;
        foreach (var p in _shown.Where(p => p.Leaving).ToList()) Drop(p);
        var layout = Layout(Page());
        foreach (var p in _shown)
        {
            p.Node.Size = Size;
            var at = layout.FirstOrDefault(l => l.P.View == p.V.Name).At;
            if (at == null) continue;
            p.From = p.To = p.Now = at;
            p.AlphaFrom = p.AlphaTo = 1;
        }
        _move = 1;
        _markersDirty = true;
        _pagesVersion++;
    }

    /// <summary>The panels at this point of a page change.</summary>
    void Step(float t)
    {
        float e = t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        foreach (var p in _shown)
        {
            p.Now = Place.Lerp(p.From, p.To, e);
            float f = Mathf.SmoothStep(p.Fade.X, p.Fade.Y, t);
            p.Now.Alpha = Mathf.Lerp(p.AlphaFrom, p.AlphaTo, f);
        }
    }

    // ------------------------------------------------------------------------------------------------ the camera inside a page

    Vector2 CamCentre => StageArea.GetCenter();

    Place Cam(Place p)
    {
        if (Mathf.IsEqualApprox(_zoom, 1) && _pan == Vector2.Zero) return p;
        var c = CamCentre;
        var centre = c + (p.Clip.GetCenter() - c) * _zoom + _pan;
        return new Place { Clip = new Rect2(centre - p.Clip.Size * _zoom / 2, p.Clip.Size * _zoom), Scale = p.Scale * _zoom, Anchor = p.Anchor, Alpha = p.Alpha };
    }

    void ZoomAt(Vector2 screen, float factor)
    {
        var c = CamCentre;
        var before = (screen - c - _pan) / _zoom;
        _zoom = Mathf.Clamp(_zoom * factor, 1, 5);
        _pan = screen - c - before * _zoom;
        ClampPan();
    }

    void ClampPan()
    {
        var area = StageArea;
        float limitX = area.Size.X * (_zoom - 1) / 2 + 40, limitY = area.Size.Y * (_zoom - 1) / 2 + 40;
        _pan = new Vector2(Mathf.Clamp(_pan.X, -limitX, limitX), Mathf.Clamp(_pan.Y, -limitY, limitY));
        if (_zoom <= 1.001f) _pan = Vector2.Zero;
    }

    // ------------------------------------------------------------------------------------------------ what's worn

    /// <summary>Re-reads what's worn (after anything changes it).</summary>
    public void Refresh()
    {
        Cov.Refresh();
        _wornVersion++;
        _areas.Clear();
        _pieceLoops.Clear();
        _regionCounts.Clear();
        _artState = -1;
        _markersDirty = true;
        UpdateLuts();
    }

    /// <summary>Shows or hides the whole body's slots.</summary>
    public void ShowSlots(bool on)
    {
        ShowAll = on;
        _markersDirty = true;
    }

    /// <summary>Lights a site for a moment (a piece just put on or taken off there).</summary>
    public void FlashSite(string site, Color color) => _flash[site] = (1, color);

    /// <summary>What fills a cell now (the piece counting there), or -1.</summary>
    public int Filling(int cell) => Cov.FillOf[cell];

    /// <summary>Which counting piece keeps a cell free, or -1.</summary>
    public int Keeping(int cell) => Cov.KeepOf[cell];

    static readonly Color[] ClothColors =
    {
        new("#6d4c3d"), new("#4f5d75"), new("#7a3b3b"), new("#3f5e4a"), new("#8a7a5a"), new("#5b4a72"), new("#3c4a5c"), new("#8c6d46"),
    };

    /// <summary>A piece's fill: its material's middle tone (the era's metal for mail and plate, its family's cloth, dark
    /// ink), its material, its shine and its rarity's colour.</summary>
    (Color Colour, int Material, float Shine, Color Rarity) FillOf(int piece, Layer layer)
    {
        var d = C.Slots[piece];
        var it = H.Gear[piece];
        var rarity = it != null ? Look.Tier(it.Rarity) : Look.Muted;
        float shine = it != null ? Look.Band(it.Rarity) / 3f : 0;
        var cloth = ClothColors[StableHash(d.Family) % ClothColors.Length];
        var metal = Ink.Metal(ItemTile.Tier(it)).Mid.Lerp(rarity, 0.08f);
        return layer switch
        {
            Layer.Skin => (new Color("#231a33"), 1, shine, rarity),
            Layer.Under => (cloth.Lerp(new Color("#cfc5b4"), 0.3f), 2, shine, rarity),
            Layer.Mail => (metal, 3, shine, rarity),
            Layer.Armor => (metal, 4, shine, rarity),
            _ => (cloth.Darkened(0.12f), 5, shine, rarity),
        };
    }

    static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char ch in s) h = h * 31 + ch;
            return h & 0x7fffffff;
        }
    }

    /// <summary>What shows of a worn piece on a view: the sites where it's outermost, its layer there, the regions
    /// those are in, and the lines round them (kept once traced).</summary>
    sealed class Area
    {
        public MapView V;
        public readonly List<int> Ids = new();
        public Layer Layer;
        public readonly HashSet<string> Regions = new();
        Vector2[][] _fine, _coarse, _trim;
        float _inset = -1;

        public Vector2[][] Loops(bool fine) => fine ? _fine ??= Outlines.Of(V, Ids) : _coarse ??= Outlines.Of(V, Ids, coarse: true);

        public Vector2[][] Trim(float inset)
        {
            if (_trim == null || !Mathf.IsEqualApprox(inset, _inset)) (_trim, _inset) = (Outlines.Of(V, Ids, inset), inset);
            return _trim;
        }
    }

    readonly Dictionary<string, Dictionary<int, Area>> _areas = new();

    Dictionary<int, Area> Areas(MapView v)
    {
        if (_areas.TryGetValue(v.Name, out var a)) return a;
        a = new Dictionary<int, Area>();
        foreach (var (leaf, top) in Cov.Top)
        {
            if (!v.Index.TryGetValue(leaf, out int id)) continue;
            if (!a.TryGetValue(top.Piece, out var e)) a[top.Piece] = e = new Area { V = v, Layer = top.Layer };
            e.Ids.Add(id);
            if (top.Layer > e.Layer) e.Layer = top.Layer;
            if (_regionOf.TryGetValue(leaf, out var r)) e.Regions.Add(r);
        }
        _areas[v.Name] = a;
        return a;
    }

    /// <summary>The piece whose area is lit: one pointed at in the lists, else under the mouse, else the piece shown in
    /// detail when it isn't worn.</summary>
    int Showing => PreviewPiece >= 0 ? PreviewPiece : _hoverPiece >= 0 ? _hoverPiece : GhostPiece;

    /// <summary>The slot whose site is lit: pointed at in the list, else the marker under the mouse, else the one chosen.</summary>
    int LitCell
    {
        get
        {
            if (PointedCell >= 0) return PointedCell;
            if (_hoverMarker >= 0 && _hoverMarker < _markers.Count && _markers[_hoverMarker].Kind == Kind.Free) return _markers[_hoverMarker].Cell;
            return PickedCell;
        }
    }

    string LitRegion => _hoverRegion ?? PointedRegion;

    int _wornVersion;

    /// <summary>Everything the panels' lines show, in one number (with the time while a glow pulses).</summary>
    int LinesState
    {
        get
        {
            bool pulsing = (FocusPiece >= 0 && H.Active[FocusPiece]) || (PickedCell >= 0 && PointedCell != PickedCell);
            return HashCode.Combine(Showing, FocusPiece, LitCell, LitRegion, _hoverSite.Site, _wornVersion, Region, pulsing ? (int)(_t * 30) : 0);
        }
    }

    /// <summary>Each panel's table for the figure shader: what's worn over each site, what's lit, what's dimmed.</summary>
    void UpdateLuts()
    {
        int focus = FocusPiece >= 0 && H.Active[FocusPiece] ? FocusPiece : -1;
        int show = Showing;
        float pulse = 0.75f + 0.25f * Mathf.Sin(_t * 4);
        foreach (var p in _shown)
        {
            if (p.Node == null) continue;
            var v = p.V;
            var node = p.Node;
            HashSet<string> focusFill = null, showFill = null, showKeep = null;
            if (focus >= 0) (focusFill, _) = Cov.Area(v, focus);
            if (show >= 0) (showFill, showKeep) = Cov.Area(v, show);
            var lit = new HashSet<string>();
            int cell = LitCell;
            if (cell >= 0 && C.Cells[cell].Board == Anatomy.Body) lit.UnionWith(Coverage.Leaves(v, C.Cells[cell]));
            if (_hoverSite.P == p && _hoverSite.Site > 0) lit.Add(v.Sites[_hoverSite.Site].Key);
            string region = LitRegion;
            var flash = new Dictionary<string, (float K, Color C)>();
            foreach (var (site, f) in _flash)
                foreach (var leaf in Coverage.Leaves(v, Fake(site)).Append(site))
                    if (!flash.TryGetValue(leaf, out var g) || g.K < f.K) flash[leaf] = f;
            for (int i = 0; i < 256; i++)
            {
                Color fill = default, light = default, extra = default, rarity = default, busy = default, box = default;
                if (i > 0 && i < v.Sites.Count)
                {
                    var site = v.Sites[i];
                    string key = site.Key;
                    box = new Color(site.Box.Position.X / v.Size.X, site.Box.Position.Y / v.Size.Y, site.Box.End.X / v.Size.X, site.Box.End.Y / v.Size.Y);
                    // The outermost piece here that has no picture of its own on this panel (those are drawn over it),
                    // and its layer, for the pass that paints it over the pictures below it.
                    var top = Cov.TopExcept(key, p.ArtPieces);
                    busy = new Color(top.Piece >= 0 ? ((int)top.Layer + 1) / 255f : 0, 0, site.Busy ? 1 : 0, 0);
                    if (top.Piece >= 0)
                    {
                        var (c, mat, shine, r) = FillOf(top.Piece, top.Layer);
                        fill = new Color(c.R, c.G, c.B, mat / 255f);
                        rarity = new Color(r.R, r.G, r.B, 1);
                        extra = new Color(shine, 0, 0, 0);
                    }
                    // On a region's page what's outside it dims; while a piece is in detail, all but it.
                    string of = _regionOf.GetValueOrDefault(key);
                    if (Region != null && Region != "Beyond" && of != Region) extra.A = v.Closeup ? 0.3f : 0.78f;
                    if (Region == "Beyond") extra.A = 0.45f;
                    if (focusFill != null && !focusFill.Contains(key)) extra.A = Mathf.Max(extra.A, 0.55f);
                    if (showFill != null && showFill.Contains(key)) light = Tint(show, 0.62f);
                    else if (showKeep != null && showKeep.Contains(key)) light = new Color(Look.Bad, 0.38f);
                    else if (focusFill != null && focusFill.Contains(key)) light = Tint(focus, 0.28f * pulse);
                    if (lit.Contains(key)) light = new Color(Look.Gold, 0.55f);
                    if (region != null && of == region) light = new Color(Look.Gold, 0.36f);
                    if (flash.TryGetValue(key, out var fl)) light = new Color(fl.C, Mathf.Max(light.A, 0.8f * fl.K));
                }
                node.Set(i, 0, fill);
                node.Set(i, 1, light);
                node.Set(i, 2, extra);
                node.Set(i, 3, rarity);
                node.Set(i, 4, busy);
                node.Set(i, 5, box);
            }
            p.Node.Commit();
        }
    }

    Color Tint(int piece, float strength)
    {
        var it = H.Gear[piece];
        var c = it != null ? Look.Tier(it.Rarity) : Look.Gold;
        return new Color(c.Lerp(Colors.White, 0.2f), strength);
    }

    int _artState = -1, _pagesVersion;

    /// <summary>Each panel's pieces' pictures (docs/art-handoff-equipment-pieces.md): every counting piece that has one
    /// on its view (or, on the figure and its plates, on another view of that side), innermost layer first, in its era's
    /// metal, dimmed off the page's region; and the piece shown where it would go, faint. Rebuilt when what's worn,
    /// shown or lit changes.</summary>
    void UpdateArt()
    {
        int focus = FocusPiece >= 0 && H.Active[FocusPiece] ? FocusPiece : -1;
        int show = Showing;
        int state = HashCode.Combine(_wornVersion, _pagesVersion, Region, focus, show, _shown.Count);
        if (state == _artState) return;
        _artState = state;
        foreach (var p in _shown)
        {
            if (p.Node == null) continue;
            var v = p.V;
            p.ArtPieces.Clear();
            p.Art.Clear();
            var seen = new HashSet<PieceArt>();
            // From behind, his braid hangs over what he wears and under what's on his head, so his head's pieces go
            // over the rest there.
            bool back = v.Space == "back";
            var list = new List<(PieceArt Art, int Piece, float Fade, bool Head)>();
            foreach (int s in Cov.Pieces)
            {
                var d = C.Slots[s];
                if (d.Board != Anatomy.Body || EquipmentMap.ArtOn(d.Name, v) is not { } art) continue;
                p.ArtPieces.Add(s);
                if (seen.Add(art)) list.Add((art, s, 1, back && Describe.Region(C, d) == "Head"));
            }
            if (show >= 0 && !H.Active[show] && C.Slots[show].Board == Anatomy.Body && EquipmentMap.ArtOn(C.Slots[show].Name, v) is { } ghost
                && seen.Add(ghost))
                list.Add((ghost, show, 0.5f, back && Describe.Region(C, C.Slots[show]) == "Head"));
            list.Sort((a, b) => a.Head != b.Head ? a.Head.CompareTo(b.Head)
                : a.Art.Under(b.Art) is var c && c != 0 ? c : a.Piece.CompareTo(b.Piece));
            // Behind him the order turns round: what's further out on his back is further from us.
            var draws = new List<ArtDraw>();
            var behind = new List<ArtDraw>();
            foreach (var (art, s, fade, head) in list)
            {
                var rect = v.FromView(EquipmentMap.View(art.View), art.Rect);
                p.Art.Add((art, s, rect));
                var draw = new ArtDraw
                {
                    Art = art, ViewRect = rect, Era = Ink.Metal(ItemTile.Tier(H.Gear[s])), Dim = PieceDim(s, v, focus), Fade = fade,
                    Glow = s == show ? Tint(s, 0.62f) : s == focus ? Tint(s, 0.28f) : fade < 1 ? new Color(Look.Gold, 0.3f) : default,
                    UnderHair = back && !head,
                };
                if (art.Behind != null) behind.Insert(0, draw with { Behind = true });
                if (art.Texture != null) draws.Add(draw);
            }
            // What's painted by its layer over a piece with a picture is painted again after the pictures below it.
            p.Passes.Clear();
            var fills = new SortedSet<Layer>();
            foreach (var site in v.Sites.Skip(1))
                if (Cov.TopExcept(site.Key, p.ArtPieces) is { Piece: >= 0 } top) fills.Add(top.Layer);
            foreach (var layer in fills)
            {
                if (!draws.Any(d => d.Art != null && d.Art.Layer < layer)) continue;
                int at = draws.FindIndex(d => d.Art != null && d.Art.Layer >= layer);
                draws.Insert(at < 0 ? draws.Count : at, ArtDraw.FillPass(layer));
                p.Passes.Add((int)layer);
            }
            // What he holds stands just behind him, nearer than what hangs down his back.
            var all = behind.Where(d => !d.Art.Held).Concat(behind.Where(d => d.Art.Held)).ToList();
            all.AddRange(draws);
            p.Node.SetArt(all);
        }
    }

    /// <summary>How much a piece's picture dims where it's off the body, as the sites round it do: outside the page's
    /// region, or while another piece is shown in detail.</summary>
    float PieceDim(int piece, MapView v, int focus)
    {
        float dim = 0;
        if (Region == "Beyond") dim = 0.45f;
        else if (Region != null && Describe.Region(C, C.Slots[piece]) != Region) dim = v.Closeup ? 0.3f : 0.78f;
        if (focus >= 0 && piece != focus) dim = Mathf.Max(dim, 0.55f);
        return dim;
    }

    // Pictures' outlines in a view's pixels (moved from the view they were painted on), kept.
    readonly Dictionary<(string, PieceArt), Vector2[][]> _artLoops = new();

    Vector2[][] ArtLoops(MapView v, PieceArt art)
    {
        if (_artLoops.TryGetValue((v.Name, art), out var got)) return got;
        var src = EquipmentMap.View(art.View);
        got = src == v ? art.Loops : art.Loops.Select(l => l.Select(q => v.FromSpace(src.ToSpace(q))).ToArray()).ToArray();
        _artLoops[(v.Name, art)] = got;
        return got;
    }

    /// <summary>The outline lit round a piece on a panel: its pictures' own where it has them, else its sites'.</summary>
    Vector2[][] FillLoops(Shown p, int piece)
    {
        if (p.ArtPieces.Contains(piece) || (piece == Showing && !H.Active[piece]))
        {
            var loops = new List<Vector2[]>();
            foreach (var (art, s, _) in p.Art)
                if (s == piece || Array.IndexOf(art.Pieces, C.Slots[piece].Name) >= 0) loops.AddRange(ArtLoops(p.V, art));
            if (loops.Count > 0) return loops.ToArray();
        }
        return PieceLoops(p.V, piece).Fills;
    }

    /// <summary>The worn piece whose picture is under a point on screen (the topmost), with its panel.</summary>
    (Shown P, int Piece) ArtUnder(Vector2 screen)
    {
        for (int i = _shown.Count - 1; i >= 0; i--)
        {
            var p = _shown[i];
            if (p.Leaving || p.Now.Alpha < 0.5f || p.Node == null) continue;
            var now = Cam(p.Now);
            if (p.Node.ClipArt && !now.Clip.HasPoint(screen)) continue;
            var at = now.Unmap(screen);
            for (int j = p.Art.Count - 1; j >= 0; j--)
            {
                var (art, piece, rect) = p.Art[j];
                if (!H.Active[piece] || !rect.HasPoint(at)) continue;
                if (InsideLoops(ArtLoops(p.V, art), at)) return (p, piece);
            }
        }
        return (null, -1);
    }

    /// <summary>Whether a point is inside loops (even-odd, so holes count out).</summary>
    static bool InsideLoops(Vector2[][] loops, Vector2 q)
    {
        bool inside = false;
        foreach (var l in loops)
            for (int i = 0, j = l.Length - 1; i < l.Length; j = i++)
                if ((l[i].Y > q.Y) != (l[j].Y > q.Y) && q.X < (l[j].X - l[i].X) * (q.Y - l[i].Y) / (l[j].Y - l[i].Y) + l[i].X)
                    inside = !inside;
        return inside;
    }

    static readonly Dictionary<string, Cell> Fakes = new();

    /// <summary>A stand-in cell for a site on its outermost layer (what a flash on it covers).</summary>
    static Cell Fake(string site)
    {
        if (!Fakes.TryGetValue(site, out var c)) Fakes[site] = c = new Cell { Site = site, Layer = Layer.Over, Board = Anatomy.Body };
        return c;
    }

    // ------------------------------------------------------------------------------------------------ markers

    enum Kind { Free, Worn, Own }

    sealed class Marker
    {
        public Shown P;
        public Kind Kind;
        public Vector2 At;
        public int Cell = -1, Piece = -1, Host = -1;
        public int Hidden;
        public Vector2 Screen, Anchor, Offset;
        public float R;
    }

    /// <summary>How visible the markers are while the page changes: the page before's go first, the new page's come
    /// once its panels have nearly settled.</summary>
    float MarkerFade => _move >= 1 ? 1 : Mathf.SmoothStep(0.62f, 1f, _move);
    float LeavingFade => 1 - Mathf.SmoothStep(0f, 0.3f, _move);

    bool Overview => Region == null;
    bool CloseupPage => Region != null && _shown.Any(p => !p.Leaving && p.V.Closeup);
    float FreeR => Overview ? 5.2f : CloseupPage ? 11.5f : 13;
    float WornR => Overview ? 6.8f : CloseupPage ? 15.5f : 17;
    float OwnR => Overview ? 3.8f : CloseupPage ? 8.5f : 10;

    void BuildMarkers()
    {
        _markersDirty = false;
        _markersVersion++;
        _markers.Clear();
        _hoverMarker = -1;
        if (Small || (Overview && !ShowAll)) return;
        foreach (var p in _shown)
            if (!p.Leaving) BuildMarkers(p);
    }

    bool InPage(Cell c) => Overview || c.Region == Region;

    void BuildMarkers(Shown p)
    {
        var v = p.V;
        var pieces = new Dictionary<int, List<Vector2>>();
        var under = new Dictionary<int, int>();
        foreach (var (key, at) in v.Points)
        {
            if (!_cellIds.TryGetValue(key, out int id)) continue;
            var cell = C.Cells[id];
            if (!InPage(cell)) continue;
            int fill = Cov.FillOf[id];
            if (fill >= 0)
            {
                if (!pieces.TryGetValue(fill, out var list)) pieces[fill] = list = new List<Vector2>();
                list.Add(at);
                continue;
            }
            int by = Cov.HiddenBy(id, v);
            if (by >= 0)
            {
                under[by] = under.GetValueOrDefault(by) + 1;
                continue;
            }
            _markers.Add(new Marker { P = p, Kind = Kind.Free, At = at, Cell = id, R = FreeR });
        }
        foreach (var (piece, points) in pieces)
        {
            // A piece worn under another doesn't show either (its first slot is covered).
            var d = C.Slots[piece];
            int over = v.Points.ContainsKey(C.Cells[d.Takes[0]].Key) ? Cov.HiddenBy(d.Takes[0], v) : -1;
            if (over >= 0)
            {
                under[over] = under.GetValueOrDefault(over) + 1;
                continue;
            }
            var (fills, _) = Cov.Area(v, piece);
            var mean = points.Aggregate(Vector2.Zero, (a, b) => a + b) / points.Count;
            var at = Snap(v, fills, mean);
            // Its own slots go where its first slot shows (on the whole body, the side it's on; on a page, its panel).
            bool primary = v.Points.ContainsKey(C.Cells[d.Takes[0]].Key)
                           || !_shown.Any(q => q != p && !q.Leaving && q.V.Points.ContainsKey(C.Cells[d.Takes[0]].Key));
            _markers.Add(new Marker { P = p, Kind = Kind.Worn, At = at, Cell = d.Takes[0], Piece = piece, R = WornR });
            var own = Cov.OwnSlots(piece);
            if (primary && own.Count > 0 && (Overview || Describe.Region(C, d) == Region)) OwnMarkers(p, piece, at, own, fills);
        }
        foreach (var m in _markers)
            if (m.P == p && m.Kind == Kind.Worn) m.Hidden = under.GetValueOrDefault(m.Piece);
    }

    /// <summary>The nearest point of a piece's area to a point (its marker sits on what it covers).</summary>
    static Vector2 Snap(MapView v, HashSet<string> sites, Vector2 at)
    {
        if (sites.Count == 0) return at;
        if (v.SiteAt(at) is { } s && sites.Contains(s)) return at;
        float best = float.MaxValue;
        var got = at;
        foreach (var site in sites)
            foreach (var q in v.Samples(v.Index[site]))
            {
                float d = q.DistanceSquaredTo(at);
                if (d < best)
                {
                    best = d;
                    got = q;
                }
            }
        return got;
    }

    /// <summary>A piece's own slots: spread over what it covers (each in a part of its area), or round its marker when
    /// it covers nothing (jewellery); and the slots of what's set in them, round those.</summary>
    void OwnMarkers(Shown p, int host, Vector2 at, List<int> own, HashSet<string> area)
    {
        var v = p.V;
        var pts = new List<Vector2>();
        foreach (var site in area) pts.AddRange(v.Samples(v.Index[site]));
        float scale = Mathf.Max(0.05f, p.To.Scale);
        float clear = (WornR + OwnR + 6) / scale;
        pts.RemoveAll(q => q.DistanceTo(at) < clear);
        var spots = new List<Vector2>();
        if (pts.Count >= own.Count * 3)
        {
            // Start far apart (each the sample farthest from those chosen), then a few rounds of k-means.
            var centres = new List<Vector2> { pts.OrderBy(q => q.DistanceSquaredTo(at)).ElementAt(Math.Min(pts.Count - 1, pts.Count / 6)) };
            while (centres.Count < own.Count)
                centres.Add(pts.OrderByDescending(q => centres.Min(c => q.DistanceSquaredTo(c))).First());
            for (int round = 0; round < 6; round++)
            {
                var sum = new Vector2[centres.Count];
                var n = new int[centres.Count];
                foreach (var q in pts)
                {
                    int bi = 0;
                    float bd = float.MaxValue;
                    for (int i = 0; i < centres.Count; i++)
                    {
                        float d = q.DistanceSquaredTo(centres[i]);
                        if (d < bd)
                        {
                            bd = d;
                            bi = i;
                        }
                    }
                    sum[bi] += q;
                    n[bi]++;
                }
                for (int i = 0; i < centres.Count; i++)
                    if (n[i] > 0) centres[i] = Snap(v, area, sum[i] / n[i]);
            }
            // Round the piece from the top, so a helm's crest is over it and its lining under.
            spots = centres.OrderBy(c => Mathf.PosMod(Mathf.Atan2(c.Y - at.Y, c.X - at.X) + Mathf.Pi / 2 + 0.3f, Mathf.Tau)).ToList();
        }
        else
        {
            float r = (WornR + OwnR + 10) / scale;
            for (int i = 0; i < own.Count; i++)
            {
                float a = -Mathf.Pi / 2 + Mathf.Tau * i / own.Count;
                spots.Add(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        }
        for (int i = 0; i < own.Count; i++)
        {
            int cell = own[i];
            int fill = Cov.FillOf[cell];
            _markers.Add(new Marker { P = p, Kind = Kind.Own, At = spots[i], Cell = cell, Host = host, Piece = fill, R = OwnR });
            // What's set there may have slots of its own: round it.
            if (fill >= 0 && Cov.OwnSlots(fill) is { Count: > 0 } deeper)
            {
                float r = (OwnR * 2 + 6) / scale;
                for (int j = 0; j < deeper.Count; j++)
                {
                    float a = -Mathf.Pi / 2 + Mathf.Tau * j / deeper.Count;
                    _markers.Add(new Marker
                    {
                        P = p, Kind = Kind.Own, At = spots[i] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, Cell = deeper[j], Host = fill,
                        Piece = Cov.FillOf[deeper[j]], R = OwnR * 0.85f,
                    });
                }
            }
        }
    }

    int _markersVersion, _laidOut;

    /// <summary>Each marker at its point, then pushed apart until none overlap (a line leads back from one that moved);
    /// only when the markers or the panels have moved.</summary>
    void LayoutMarkers()
    {
        var key = new HashCode();
        key.Add(_markersVersion);
        key.Add(_zoom);
        key.Add(_pan);
        foreach (var p in _shown)
        {
            key.Add(p.Now.Clip);
            key.Add(p.Now.Scale);
        }
        int hash = key.ToHashCode();
        if (hash == _laidOut) return;
        _laidOut = hash;
        foreach (var m in _markers)
        {
            m.Anchor = Cam(m.P.Now).Map(m.At);
            m.Screen = m.Anchor;
        }
        int n = _markers.Count;
        if (n == 0) return;
        float pad = Overview ? 2.5f : 7;
        var area = StageArea;
        float cell = (WornR + pad) * 2.2f;
        var grid = new Dictionary<(int, int), List<int>>();
        for (int pass = 0; pass < (Overview ? 10 : 40); pass++)
        {
            grid.Clear();
            for (int i = 0; i < n; i++)
            {
                var k = ((int)Mathf.Floor(_markers[i].Screen.X / cell), (int)Mathf.Floor(_markers[i].Screen.Y / cell));
                if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<int>();
                list.Add(i);
            }
            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                var mi = _markers[i];
                int cx = (int)Mathf.Floor(mi.Screen.X / cell), cy = (int)Mathf.Floor(mi.Screen.Y / cell);
                for (int gx = cx - 1; gx <= cx + 1; gx++)
                    for (int gy = cy - 1; gy <= cy + 1; gy++)
                    {
                        if (!grid.TryGetValue((gx, gy), out var list)) continue;
                        foreach (int j in list)
                        {
                            if (j <= i) continue;
                            var mj = _markers[j];
                            float min = mi.R + mj.R + pad;
                            var d = mj.Screen - mi.Screen;
                            float l = d.Length();
                            if (l >= min) continue;
                            var push = l < 0.5f ? Vector2.FromAngle(0.7f + 2.4f * j) : d / l;
                            // Worn pieces hold their place; slots give way.
                            float wi = mi.Kind == Kind.Worn ? 0.25f : 0.5f, wj = mj.Kind == Kind.Worn ? 0.25f : 0.5f;
                            mi.Screen -= push * (min - l) * wi / (wi + wj);
                            mj.Screen += push * (min - l) * wj / (wi + wj);
                            moved = true;
                        }
                    }
            }
            if (!moved) break;
        }
        foreach (var m in _markers)
            m.Screen = new Vector2(Mathf.Clamp(m.Screen.X, area.Position.X + m.R, area.End.X - m.R), Mathf.Clamp(m.Screen.Y, area.Position.Y + m.R, area.End.Y - m.R));
    }

    int MarkerAt(Vector2 screen)
    {
        int best = -1;
        float bestD = float.MaxValue;
        if (_move < 0.9f) return -1;
        for (int i = 0; i < _markers.Count; i++)
        {
            var m = _markers[i];
            if (m.P.Now.Alpha < 0.5f) continue;
            float d = m.Screen.DistanceTo(screen);
            if (d <= m.R + (Overview ? 3 : 4) && d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Where a cell shows on screen, in the screen's coordinates (for flying things to it): its marker, the
    /// marker of the piece filling it, or its point.</summary>
    public Vector2 GlobalCellPoint(int cell)
    {
        if (_markersDirty) BuildMarkers();
        LayoutMarkers();
        foreach (var m in _markers)
            if (m.Cell == cell || (m.Kind == Kind.Worn && Cov.FillOf[cell] == m.Piece && m.Piece >= 0))
                return GetGlobalTransformWithCanvas() * m.Screen;
        var key = C.Cells[cell].Key;
        foreach (var p in _shown)
            if (!p.Leaving && p.V.Points.TryGetValue(key, out var at))
                return GetGlobalTransformWithCanvas() * Cam(p.Now).Map(at);
        return GetGlobalTransformWithCanvas() * (Size / 2);
    }

    /// <summary>Where a piece shows (or the body piece it's set in), in the screen's coordinates.</summary>
    public Vector2 GlobalSpot(int piece)
    {
        int body = Cov.BodyHost(piece);
        var d = C.Slots[body >= 0 ? body : piece];
        if (d.Board == Anatomy.Body)
            foreach (var p in _shown)
            {
                if (p.Leaving) continue;
                var pts = d.Takes.Select(c => C.Cells[c].Key).Where(k => p.V.Points.ContainsKey(k)).Select(k => p.V.Points[k]).ToList();
                if (pts.Count == 0) continue;
                var mean = pts.Aggregate(Vector2.Zero, (a, b) => a + b) / pts.Count;
                return GetGlobalTransformWithCanvas() * Cam(p.Now).Map(mean);
            }
        return GetGlobalTransformWithCanvas() * (Size / 2);
    }

    // ------------------------------------------------------------------------------------------------ frame by frame

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (_shown.Count == 0 && IsInsideTree() && Size.X > 10) Show(Region, true);
        if (_move < 1)
        {
            _move = Mathf.Min(1, _move + dt / MoveTime);
            Step(_move);
            if (_move >= 1)
            {
                foreach (var p in _shown.Where(p => p.Leaving).ToList()) Drop(p);
                _leaving.Clear();
            }
        }
        foreach (var k in _flash.Keys.ToList())
        {
            var (f, c) = _flash[k];
            f -= dt * 1.4f;
            if (f <= 0) _flash.Remove(k);
            else _flash[k] = (f, c);
        }
        if (_markersDirty) BuildMarkers();
        LayoutMarkers();
        UpdateArt();
        UpdateLuts();
        foreach (var p in _shown)
        {
            var now = Cam(p.Now);
            // The lines are drawn again only when they change: the panel moves, what's lit changes, or a glow pulses.
            int lines = HashCode.Combine(now.Clip, now.Scale, now.Alpha, LinesState);
            p.Node.Show(now.Clip, now.Unmap(now.Clip.Position), now.Clip.Size / now.Scale, now.Alpha, _t, lines != p.LinesDrawn);
            p.LinesDrawn = lines;
        }
        QueueRedraw();
        _top.QueueRedraw();
    }

    // ------------------------------------------------------------------------------------------------ input

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseMotion m:
                _mouse = m.Position;
                if (_dragging)
                {
                    if (m.Relative.Length() > 0.5f) _dragged = true;
                    _pan += m.Relative;
                    ClampPan();
                    AcceptEvent();
                    return;
                }
                Hover(m.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } w when !Small:
                // On the whole body the wheel zooms into the part of him under the mouse; on a page, closer.
                if (Overview)
                {
                    if (RegionUnder(w.Position) is { } r) RegionClicked?.Invoke(r);
                }
                else ZoomAt(w.Position, 1.18f);
                _outPull = 0;
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } w when !Small:
                // Out past a page's widest view: back to the page before (two turns, so a flick doesn't).
                double now = Time.GetTicksMsec() / 1000.0;
                if (!Overview && _zoom <= 1.001f)
                {
                    _outPull = now - _lastWheel < 0.7 ? _outPull + 1 : 1;
                    if (_outPull >= 2)
                    {
                        _outPull = 0;
                        BackClicked?.Invoke();
                    }
                }
                else ZoomAt(w.Position, 1 / 1.18f);
                _lastWheel = now;
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } when !Small:
                BackClicked?.Invoke();
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } b:
                if (b.Pressed)
                {
                    _dragging = !Small && _zoom > 1.01f;
                    _dragged = false;
                }
                else
                {
                    _dragging = false;
                    if (!_dragged) Click(b.Position);
                }
                AcceptEvent();
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationMouseExit) return;
        _mouse = new Vector2(-1, -1);
        _hoverRegion = null;
        _hoverSite = default;
        _hoverMarker = _hoverPiece = -1;
    }

    /// <summary>The panel and site under a point on screen.</summary>
    (Shown P, int Site) SiteUnder(Vector2 screen)
    {
        for (int i = _shown.Count - 1; i >= 0; i--)
        {
            var p = _shown[i];
            if (p.Leaving || p.Now.Alpha < 0.5f) continue;
            var now = Cam(p.Now);
            if (!now.Clip.HasPoint(screen)) continue;
            int id = p.V.SiteIdAt(now.Unmap(screen));
            if (id > 0) return (p, id);
        }
        return (null, 0);
    }

    string RegionUnder(Vector2 screen)
    {
        int m = MarkerAt(screen);
        if (m >= 0) return RegionOf(_markers[m]);
        var (p, site) = SiteUnder(screen);
        return p != null ? _regionOf.GetValueOrDefault(p.V.Sites[site].Key) : null;
    }

    string RegionOf(Marker m) => m.Kind == Kind.Free ? C.Cells[m.Cell].Region : Describe.Region(C, C.Slots[m.Piece >= 0 ? m.Piece : m.Host]);

    void Hover(Vector2 at)
    {
        _hoverMarker = Small ? -1 : MarkerAt(at);
        var (p, site) = SiteUnder(at);
        string key = p != null ? p.V.Sites[site].Key : null;
        var (_, art) = _hoverMarker >= 0 ? (null, -1) : ArtUnder(at);
        _hoverRegion = null;
        _hoverSite = default;
        _hoverPiece = -1;
        if (_hoverMarker >= 0)
        {
            var m = _markers[_hoverMarker];
            if (m.Kind == Kind.Worn) _hoverPiece = m.Piece;
            else if (Overview) _hoverRegion = RegionOf(m);
        }
        // A piece's picture under the mouse (on him, or beside him: a sword, a cloak's drape).
        else if (art >= 0 && (Overview || Small)) _hoverRegion = Describe.Region(C, C.Slots[art]);
        else if (art >= 0 && InPage(C.Cells[C.Slots[art].Takes[0]])) _hoverPiece = art;
        else if (key != null)
        {
            if (Overview || Small) _hoverRegion = _regionOf.GetValueOrDefault(key);
            else if (Cov.Top.TryGetValue(key, out var top) && InPage(C.Cells[C.Slots[top.Piece].Takes[0]])) _hoverPiece = top.Piece;
            else _hoverSite = (p, site);
        }
        MouseDefaultCursorShape = _hoverMarker >= 0 || _hoverRegion != null || _hoverPiece >= 0 || _hoverSite.P != null ? CursorShape.PointingHand : CursorShape.Arrow;
    }

    void Click(Vector2 at)
    {
        int marker = Small ? -1 : MarkerAt(at);
        if (marker >= 0)
        {
            var m = _markers[marker];
            if (Overview) RegionClicked?.Invoke(RegionOf(m));
            if (m.Kind == Kind.Worn) PieceClicked?.Invoke(m.Piece);
            else
            {
                PickedCell = m.Cell;
                CellClicked?.Invoke(m.Cell);
            }
            return;
        }
        var (_, art) = ArtUnder(at);
        if (art >= 0 && (Overview || Small))
        {
            RegionClicked?.Invoke(Describe.Region(C, C.Slots[art]));
            return;
        }
        if (art >= 0 && InPage(C.Cells[C.Slots[art].Takes[0]]))
        {
            PieceClicked?.Invoke(art);
            return;
        }
        var (p, site) = SiteUnder(at);
        if (p == null)
        {
            NothingClicked?.Invoke();
            return;
        }
        string key = p.V.Sites[site].Key;
        string region = _regionOf.GetValueOrDefault(key);
        if (Overview || Small)
        {
            if (region != null) RegionClicked?.Invoke(region);
            return;
        }
        if (Cov.Top.TryGetValue(key, out var top) && InPage(C.Cells[C.Slots[top.Piece].Takes[0]]))
        {
            PieceClicked?.Invoke(top.Piece);
            return;
        }
        if (region != null && region != Region)
        {
            RegionClicked?.Invoke(region);
            return;
        }
        // A bare site: its first slot that shows.
        foreach (var m in _markers)
            if (m.Kind == Kind.Free && C.Cells[m.Cell].Site == key)
            {
                PickedCell = m.Cell;
                CellClicked?.Invoke(m.Cell);
                return;
            }
    }

    // ------------------------------------------------------------------------------------------------ drawing: the stage

    public override void _Draw()
    {
        if (Small)
        {
            foreach (var p in _shown) Floor(p);
            return;
        }
        // The fitting room behind the figure, the workbench behind the close-ups, the floor under his feet.
        float close = 0, total = 0;
        foreach (var p in _shown)
        {
            total += p.Now.Alpha;
            if (p.V.Closeup) close += p.Now.Alpha;
        }
        float bench = total > 0 ? close / total : 0;
        Backdrop(EquipmentMap.Stage, 1);
        if (bench > 0.01f) Backdrop(EquipmentMap.Board, bench);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.04f, 0.03f, 0.06f, 0.28f));
        foreach (var p in _shown) Floor(p);
        Aura();
    }

    /// <summary>A backdrop filling the stage, drifting a little with the camera.</summary>
    void Backdrop(Texture2D tex, float alpha)
    {
        if (tex == null || alpha <= 0.01f) return;
        var ts = tex.GetSize();
        float k = Mathf.Max(Size.X / ts.X, Size.Y / ts.Y) * 1.08f;
        var size = ts * k;
        var pos = (Size - size) / 2 - _pan * 0.04f;
        DrawTextureRect(tex, new Rect2(pos, size), false, new Color(1, 1, 1, alpha));
    }

    static readonly string[] Feet = { "R.foot", "L.foot", "R.heel", "L.heel" };

    /// <summary>A soft shadow under a figure's feet (on a plate, where its feet are).</summary>
    void Floor(Shown p)
    {
        if (p.V.Closeup || !Feet.Any(p.V.Has)) return;
        var now = Cam(p.Now);
        var feet = Box(p.V, Feet);
        var c = now.Map(new Vector2(feet.GetCenter().X, feet.End.Y));
        float w = feet.Size.X * now.Scale * 0.75f;
        DrawSetTransform(c, 0, new Vector2(1, 0.14f));
        DrawCircle(Vector2.Zero, w, new Color(0, 0, 0, 0.35f * now.Alpha), true, -1, true);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>An aura worn: a glow behind the front of the figure.</summary>
    void Aura()
    {
        int aura = Cov.Pieces.FirstOrDefault(s => C.Slots[s].Family == "aura", -1);
        if (aura < 0) return;
        var front = _shown.FirstOrDefault(p => p.V.Name == "front");
        if (front == null) return;
        var now = Cam(front.Now);
        var body = Box(front.V, new[] { "scalp", "R.foot", "L.foot", "R.hand", "L.hand" });
        var c = now.Map(body.GetCenter());
        var colour = Look.Tier(H.Gear[aura].Rarity);
        float r = body.Size.Y * now.Scale * 0.55f;
        for (int i = 5; i >= 1; i--)
            DrawCircle(c, r * (0.45f + 0.12f * i) * (1 + 0.03f * Mathf.Sin(_t * 1.5f + i)), new Color(colour, 0.05f * now.Alpha), true, -1, true);
    }

    // ------------------------------------------------------------------------------------------------ drawing: lines on a panel

    /// <summary>Where a panel's lines go on screen now (the mapped loops are kept until it moves).</summary>
    (float Alpha, float Scale) LinesAt(Shown p)
    {
        var now = Cam(p.Now);
        var off = now.Offset - now.Clip.Position;
        float k = now.Scale;
        if (p.MappedOffset != off || !Mathf.IsEqualApprox(p.MappedScale, k))
        {
            p.Version++;
            p.MappedOffset = off;
            p.MappedScale = k;
        }
        if (p.Mapped.Count > 800) p.Mapped.Clear();
        return (now.Alpha, k);
    }

    /// <summary>The ink lines round the pieces painted by their layer, drawn with the paint (under the pictures, or
    /// with their layer's pass among them), and a trim inside plate and heavy cloth on a page. A piece with its own
    /// picture here has its own ink line.</summary>
    void DrawInks(Canvas c, Shown p, int pass)
    {
        if (Small) return;
        var (a, k) = LinesAt(p);
        if (a < 0.02f) return;
        bool fine = k > 0.8f;
        float ink = Mathf.Clamp(0.8f + k * 0.75f, 1.1f, 3.2f);
        float inset = Mathf.Snapped(3.4f / Mathf.Max(0.3f, p.To.Scale), 0.25f);
        foreach (var (piece, area) in Areas(p.V))
        {
            if (p.ArtPieces.Contains(piece) || (p.Passes.Contains((int)area.Layer) ? (int)area.Layer : -1) != pass) continue;
            // Outside the page's region (dimmed), a faint line and no trim.
            bool here = Overview || Region == "Beyond" || area.Regions.Contains(Region);
            foreach (var loop in area.Loops(fine)) c.DrawPolyline(Map(p, loop), new Color(Look.Ink, (here ? 0.9f : 0.4f) * a), ink, true);
            if (!fine || Overview || !here || area.Layer is not (Layer.Armor or Layer.Over)) continue;
            var trim = area.Layer == Layer.Over ? Look.Tier(H.Gear[piece]?.Rarity ?? 0).Lerp(Colors.Black, 0.15f)
                : Ink.Metal(ItemTile.Tier(H.Gear[piece])).Light.Lerp(Colors.White, 0.2f);
            foreach (var loop in area.Trim(inset)) c.DrawPolyline(Map(p, loop), new Color(trim, 0.75f * a), Mathf.Clamp(k * 0.9f, 1.2f, 2.4f), true);
        }
    }

    /// <summary>What's lit on a panel, over everything and clipped to it: the piece pointed at in its colour, what it
    /// keeps free in red, a slot or a region in gold, the piece in detail.</summary>
    void DrawLines(Canvas c, Shown p)
    {
        var (a, _) = LinesAt(p);
        if (a < 0.02f) return;
        var v = p.V;
        // What's lit.
        float pulse = 0.7f + 0.3f * Mathf.Sin(_t * 4);
        string region = LitRegion;
        if (region != null && (Overview || Small)) Glow(c, p, RegionLoops(v, region), Look.Gold, a);
        if (Small) return;
        int focus = FocusPiece >= 0 && H.Active[FocusPiece] ? FocusPiece : -1;
        int show = Showing;
        if (focus >= 0 && focus != show) Glow(c, p, FillLoops(p, focus), RarityOf(focus), a * pulse);
        if (show >= 0)
        {
            Glow(c, p, PieceLoops(v, show).Keeps, Look.Bad, a * 0.8f, 0.6f);
            Glow(c, p, FillLoops(p, show), RarityOf(show), a);
        }
        int cell = LitCell;
        if (cell >= 0 && C.Cells[cell].Board == Anatomy.Body)
            Glow(c, p, CellLoops(v, cell), Look.Gold, a * (cell == PickedCell && cell != PointedCell ? pulse : 1));
        if (_hoverSite.P == p && _hoverSite.Site > 0) Glow(c, p, Outlines.Of(v, new[] { _hoverSite.Site }), Look.Gold, a * 0.8f, 0.7f);
    }

    // The loops lit, kept: a region's and a slot's for good, a piece's until what's worn changes.
    readonly Dictionary<(string, string), Vector2[][]> _regionLoops = new();
    readonly Dictionary<(string, int), Vector2[][]> _cellLoops = new();
    readonly Dictionary<(string, int), (Vector2[][] Fills, Vector2[][] Keeps)> _pieceLoops = new();

    Vector2[][] RegionLoops(MapView v, string region)
    {
        if (!_regionLoops.TryGetValue((v.Name, region), out var loops))
            _regionLoops[(v.Name, region)] = loops = Outlines.Of(v, v.Sites.Skip(1).Where(s => _regionOf.GetValueOrDefault(s.Key) == region).Select(s => s.Id));
        return loops;
    }

    Vector2[][] CellLoops(MapView v, int cell)
    {
        if (!_cellLoops.TryGetValue((v.Name, cell), out var loops)) _cellLoops[(v.Name, cell)] = loops = Outlines.Of(v, Coverage.Leaves(v, C.Cells[cell]));
        return loops;
    }

    (Vector2[][] Fills, Vector2[][] Keeps) PieceLoops(MapView v, int piece)
    {
        if (!_pieceLoops.TryGetValue((v.Name, piece), out var loops))
        {
            var (fills, keeps) = Cov.Area(v, piece);
            _pieceLoops[(v.Name, piece)] = loops = (Outlines.Of(v, fills), Outlines.Of(v, keeps));
        }
        return loops;
    }

    Color RarityOf(int piece) => H.Gear[piece] != null ? Look.Tier(H.Gear[piece].Rarity) : Look.Gold;

    /// <summary>Loops in a panel's lines' coordinates (kept until the panel moves).</summary>
    static Vector2[] Map(Shown p, Vector2[] src)
    {
        if (p.Mapped.TryGetValue(src, out var e) && e.Version == p.Version) return e.Dst;
        var dst = e.Dst ?? new Vector2[src.Length];
        for (int i = 0; i < src.Length; i++) dst[i] = p.MappedOffset + src[i] * p.MappedScale;
        p.Mapped[src] = (p.Version, dst);
        return dst;
    }

    /// <summary>A glowing line round an area.</summary>
    void Glow(Canvas c, Shown p, Vector2[][] loops, Color colour, float alpha, float width = 1)
    {
        if (alpha < 0.02f) return;
        foreach (var loop in loops)
        {
            var pts = Map(p, loop);
            c.DrawPolyline(pts, new Color(colour, 0.14f * alpha), 9 * width, true);
            c.DrawPolyline(pts, new Color(colour, 0.3f * alpha), 5 * width, true);
            c.DrawPolyline(pts, new Color(colour.Lerp(Colors.White, 0.25f), alpha), 2.2f * width, true);
        }
    }

    // ------------------------------------------------------------------------------------------------ drawing: over everything

    static readonly Color Disc = new("#1a1524");

    /// <summary>Over the figure: the markers, the names of what's pointed at, the legend and the hint.</summary>
    void DrawTop(Canvas c)
    {
        if (Small)
        {
            if (_hoverRegion != null) Label(c, _mouse + new Vector2(0, -26), _hoverRegion.ToUpperInvariant(), Look.Gold);
            return;
        }
        Halo(c);
        // Lines back to the slots of markers that were pushed aside.
        if (!Overview)
            foreach (var m in _markers)
            {
                if (m.Anchor.DistanceTo(m.Screen) < m.R * 0.6f) continue;
                float a = m.P.Now.Alpha;
                c.DrawLine(m.Anchor, m.Screen, new Color(Look.Ink, 0.8f * a), 3.5f, true);
                c.DrawLine(m.Anchor, m.Screen, new Color(Look.Muted, 0.85f * a), 1.4f, true);
                c.DrawCircle(m.Anchor, 3, new Color(Look.Ink, a), true, -1, true);
                c.DrawCircle(m.Anchor, 1.8f, new Color(Look.Muted, a), true, -1, true);
            }
        foreach (var m in _leaving)
        {
            var now = Cam(m.P.Now);
            m.Screen = now.Map(m.At) + m.Offset * (now.Scale / Mathf.Max(0.01f, m.P.From.Scale));
            DrawMarker(c, m, false, LeavingFade, true);
        }
        for (int i = 0; i < _markers.Count; i++) DrawMarker(c, _markers[i], i == _hoverMarker, MarkerFade, Overview);
        if (_hoverMarker >= 0 && _hoverMarker < _markers.Count && !Overview) MarkerLabel(c, _markers[_hoverMarker]);
        else if (_hoverMarker >= 0 && _hoverMarker < _markers.Count) OverviewLabel(c, _markers[_hoverMarker]);
        else if (_hoverPiece >= 0) PieceLabel(c, _hoverPiece, _mouse + new Vector2(0, -30));
        else if (_hoverRegion != null) RegionLabel(c, _hoverRegion, _mouse + new Vector2(0, -30));
        else if (_hoverSite.P != null) Label(c, _mouse + new Vector2(0, -30), SiteName(_hoverSite.P.V.Sites[_hoverSite.Site].Key), Look.Text);
        Legend(c);
        if (!string.IsNullOrEmpty(Hint)) HintBox(c);
    }

    string SiteName(string site)
    {
        foreach (var cell in C.Cells)
            if (cell.Site == site) return cell.SiteName;
        return site;
    }

    /// <summary>A halo over the head, when one's worn.</summary>
    void Halo(Canvas c)
    {
        var front = _shown.FirstOrDefault(p => !p.Leaving && (p.V.Name == "front" || p.V.Name == "head_front"));
        if (front == null) return;
        var now = Cam(front.Now);
        var head = Box(front.V, new[] { "scalp", "chin" });
        var top = now.Map(new Vector2(head.GetCenter().X, head.Position.Y));
        float w = head.Size.X * now.Scale;
        foreach (int s in Cov.Pieces)
        {
            if (C.Slots[s].Family is not ("halo" or "starcrown")) continue;
            var col = Look.Tier(H.Gear[s].Rarity);
            c.DrawSetTransform(top + new Vector2(0, -w * 0.18f), 0, new Vector2(1, 0.28f));
            c.DrawArc(Vector2.Zero, w * 0.42f, 0, Mathf.Tau, 40, new Color(col, 0.35f * now.Alpha), w * 0.08f, true);
            c.DrawArc(Vector2.Zero, w * 0.42f, 0, Mathf.Tau, 40, new Color(col.Lerp(Colors.White, 0.5f), now.Alpha), Mathf.Max(2, w * 0.025f), true);
            c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    void DrawMarker(Canvas c, Marker m, bool hover, float fade, bool dot)
    {
        float a = m.P.Now.Alpha * fade;
        if (a < 0.02f) return;
        var p = m.Screen;
        bool picked = (m.Kind != Kind.Worn && m.Cell == PickedCell) || (m.Kind == Kind.Worn && m.Piece == FocusPiece);
        float r = m.R * (hover || picked ? 1.18f : 1);
        var cell = C.Cells[m.Cell];
        if (m.Kind == Kind.Worn || (m.Kind == Kind.Own && m.Piece >= 0))
        {
            var it = H.Gear[m.Piece];
            var col = it != null ? Look.Tier(it.Rarity) : Look.Muted;
            if (dot)
            {
                c.DrawCircle(p, r + 1.6f, new Color(Look.Ink, 0.9f * a), true, -1, true);
                c.DrawCircle(p, r, new Color(col, a), true, -1, true);
                c.DrawCircle(p, r * 0.42f, new Color(Look.TierFace(it?.Rarity ?? 0), a), true, -1, true);
                return;
            }
            if (it != null && Look.Band(it.Rarity) >= 2)
                c.DrawCircle(p, r + 5 + 1.5f * Mathf.Sin(_t * 3), new Color(col, 0.18f * a), true, -1, true);
            c.DrawCircle(p, r + 3, new Color(Look.Ink, 0.9f * a), true, -1, true);
            c.DrawCircle(p, r, new Color(Look.TierFace(it?.Rarity ?? 0), a), true, -1, true);
            if (Assets.Icon(C.Slots[m.Piece].Family, ItemTile.Tier(it)) is { } tex)
            {
                float s = r * 1.3f;
                c.DrawTextureRect(tex, new Rect2(p - new Vector2(s, s) / 2, new Vector2(s, s)), false, new Color(1, 1, 1, a));
            }
            c.DrawArc(p, r, 0, Mathf.Tau, 32, new Color(col, a), picked ? 4 : 2.6f, true);
            if (m.Kind == Kind.Worn && m.Hidden > 0)
            {
                var b = p + new Vector2(r * 0.78f, r * 0.78f);
                c.DrawCircle(b, 8.5f, new Color(Look.Ink, a), true, -1, true);
                c.DrawCircle(b, 7, new Color(Disc, a), true, -1, true);
                c.DrawString(Look.Numbers, b + new Vector2(-8, 4.5f), m.Hidden.ToString(), HorizontalAlignment.Center, 16, 12, new Color(Look.Muted, a));
            }
            if (picked) c.DrawArc(p, r + 6 + 2 * Mathf.Sin(_t * 5), 0, Mathf.Tau, 36, new Color(Look.Gold, a), 2.5f, true);
            return;
        }
        // A free slot (or one of a piece's own): why nothing's there, in its ring; what kind of slot, in its glyph.
        bool own = m.Kind == Kind.Own;
        int open = 0, found = 0;
        foreach (int s in C.Boards[cell.Board].Slots)
        {
            if (Array.IndexOf(C.Slots[s].Takes, m.Cell) < 0) continue;
            if (H.Open[s]) open++;
            if (H.Open[s] && H.Gear[s] != null) found++;
        }
        var ring = open == 0 ? Look.Dim : found > 0 ? Look.Text : Look.Muted;
        if (own && m.Host >= 0 && H.Gear[m.Host] != null) ring = ring.Lerp(Look.Tier(H.Gear[m.Host].Rarity), 0.55f);
        if (dot)
        {
            c.DrawCircle(p, r + 1.5f, new Color(Look.Ink, 0.85f * a), true, -1, true);
            c.DrawCircle(p, r, new Color(Disc, (open == 0 ? 0.7f : 0.95f) * a), true, -1, true);
            c.DrawArc(p, r, 0, Mathf.Tau, 12, new Color(hover ? Look.Gold : ring, a), own ? 1.2f : 1.6f, true);
            return;
        }
        c.DrawCircle(p, r + 2.5f, new Color(Look.Ink, 0.8f * a), true, -1, true);
        c.DrawCircle(p, r, new Color(Disc, (open == 0 ? 0.72f : 0.94f) * a), true, -1, true);
        c.DrawArc(p, r, 0, Mathf.Tau, 28, new Color(hover ? Look.Gold : ring, a), picked ? 3.5f : own ? 1.6f : 2, true);
        if (EquipmentMap.Glyphs is { } glyphs)
        {
            float s = r * 1.15f;
            var g = EquipmentMap.GlyphRect(own ? "mod" : EquipmentMap.GlyphOf(cell.Layer));
            c.DrawTextureRectRegion(glyphs, new Rect2(p - new Vector2(s, s) / 2, new Vector2(s, s)), g, new Color(ring, (open == 0 ? 0.55f : 0.92f) * a));
        }
        if (picked) c.DrawArc(p, r + 6 + 2 * Mathf.Sin(_t * 5), 0, Mathf.Tau, 36, new Color(Look.Gold, a), 2.5f, true);
    }

    void MarkerLabel(Canvas c, Marker m)
    {
        var at = m.Screen + new Vector2(0, -m.R - 14);
        if (m.Kind == Kind.Worn)
        {
            PieceLabel(c, m.Piece, at);
            return;
        }
        var cell = C.Cells[m.Cell];
        string where = cell.Board == Anatomy.Body ? cell.SiteName + ": " + cell.Name : Describe.Base(C.Slots[m.Host].Name) + ": " + cell.Name;
        if (m.Piece >= 0 && H.Gear[m.Piece] is { } it)
        {
            Box(c, at, new List<(string, Color)> { (where, Look.Text), (Describe.Name(C, m.Piece, it), Look.TierText(it.Rarity)) });
            return;
        }
        Box(c, at, new List<(string, Color)> { (where, Look.Text), (StateOf(m.Cell), Look.Muted) });
    }

    /// <summary>A marker pointed at on the whole body: the slot (or piece), and its region a click away.</summary>
    void OverviewLabel(Canvas c, Marker m)
    {
        var at = m.Screen + new Vector2(0, -m.R - 12);
        string region = RegionOf(m);
        if (m.Kind == Kind.Worn && H.Gear[m.Piece] is { } it)
        {
            Box(c, at, new List<(string, Color)> { (Describe.Name(C, m.Piece, it), Look.TierText(it.Rarity)), ($"{region}: click to see it", Look.Dim) });
            return;
        }
        var cell = C.Cells[m.Cell];
        string where = cell.Board == Anatomy.Body ? cell.SiteName + ": " + cell.Name : Describe.Base(C.Slots[m.Host].Name) + ": " + cell.Name;
        Box(c, at, new List<(string, Color)> { (where, Look.Text), (StateOf(m.Cell), Look.Muted), ($"{region}: click to go there", Look.Dim) });
    }

    /// <summary>Why nothing fills a slot: free, nothing found yet, or when it opens.</summary>
    string StateOf(int cell)
    {
        var slots = C.Boards[C.Cells[cell].Board].Slots.Where(s => Array.IndexOf(C.Slots[s].Takes, cell) >= 0).ToList();
        if (slots.Any(s => H.Open[s] && H.Gear[s] != null)) return "free";
        if (slots.Any(s => H.Open[s])) return "nothing found yet";
        int first = slots.Where(s => !C.Slots[s].Prestige).Select(s => C.Slots[s].UnlockDepth).DefaultIfEmpty(0).Min();
        return first > 0 ? $"opens at depth {Format.Whole(first)}" : "opened by transcending";
    }

    void PieceLabel(Canvas c, int piece, Vector2 at)
    {
        var it = H.Gear[piece];
        if (it == null) return;
        var lines = new List<(string, Color)> { (Describe.Name(C, piece, it), Look.TierText(it.Rarity)) };
        int own = Cov.OwnSlots(piece).Count;
        int beneath = 0;
        foreach (var m in _markers)
            if (m.Kind == Kind.Worn && m.Piece == piece) beneath += m.Hidden;
        var bits = new List<string>();
        if (own > 0) bits.Add($"{own} slot{(own == 1 ? "" : "s")} of its own");
        if (beneath > 0) bits.Add($"{beneath} slot{(beneath == 1 ? "" : "s")} under it");
        if (bits.Count > 0) lines.Add((string.Join(", ", bits), Look.Muted));
        lines.Add(("Click to see it", Look.Dim));
        Box(c, at, lines);
    }

    readonly Dictionary<string, (int Slots, int Worn)> _regionCounts = new();

    void RegionLabel(Canvas c, string region, Vector2 at)
    {
        if (!_regionCounts.TryGetValue(region, out var n))
        {
            int slots = C.Cells.Count(x => x.Board == Anatomy.Body && x.Region == region);
            int worn = Cov.Pieces.Count(s => Describe.Region(C, C.Slots[s]) == region);
            _regionCounts[region] = n = (slots, worn);
        }
        Box(c, at, new List<(string, Color)> { (region.ToUpperInvariant(), Look.Gold), ($"{n.Slots} slots, {n.Worn} pieces worn", Look.Muted) });
    }

    void Label(Canvas c, Vector2 centre, string text, Color colour) => Box(c, centre, new List<(string, Color)> { (text, colour) });

    void Box(Canvas c, Vector2 centre, List<(string Text, Color Colour)> lines)
    {
        var font = Look.UiBold;
        float w = lines.Select((l, i) => (i == 0 ? font : Look.Ui).GetStringSize(l.Text, HorizontalAlignment.Left, -1, i == 0 ? 17 : 15).X).Max();
        float h = lines.Count * 21 + 12;
        var pos = new Vector2(Mathf.Clamp(centre.X - w / 2 - 12, 8, Mathf.Max(8, Size.X - w - 32)), Mathf.Clamp(centre.Y - h, 8, Size.Y - h - 8));
        c.DrawStyleBox(Ui.Frame(new Color("#0d0a13", 0.92f), new Color(Look.EdgeLight, 0.9f), 8, 1, 0, 0), new Rect2(pos, new Vector2(w + 24, h)));
        for (int i = 0; i < lines.Count; i++)
            c.DrawString(i == 0 ? font : Look.Ui, pos + new Vector2(12, 24 + 21 * i), lines[i].Text, HorizontalAlignment.Left, -1, i == 0 ? 17 : 15, lines[i].Colour);
    }

    void Legend(Canvas c)
    {
        if (_markers.Count == 0) return;
        var font = Look.Ui;
        var items = new (string Glyph, Color Ring, bool Worn, string Text)[]
        {
            (null, Look.Tier(3), true, "worn"), ("adorn", Look.Text, false, "free"), ("mod", Look.Tier(4).Lerp(Look.Text, 0.45f), false, "a piece's own slot"),
            ("adorn", Look.Dim, false, "not open yet"),
        };
        float x = 24, y = Size.Y - 30, width = 12;
        foreach (var it in items) width += 34 + font.GetStringSize(it.Text, HorizontalAlignment.Left, -1, 16).X + 22;
        c.DrawStyleBox(Ui.Frame(new Color("#0d0a13", 0.86f), new Color(Look.Edge, 0.8f), 8, 1, 0, 0), new Rect2(12, Size.Y - 52, width, 44));
        foreach (var (glyph, ring, worn, text) in items)
        {
            var p = new Vector2(x + 12, y);
            c.DrawCircle(p, 12, worn ? Look.TierFace(3) : Disc, true, -1, true);
            c.DrawArc(p, 12, 0, Mathf.Tau, 20, ring, 2, true);
            if (glyph != null && EquipmentMap.Glyphs is { } g)
                c.DrawTextureRectRegion(g, new Rect2(p - new Vector2(7, 7), new Vector2(14, 14)), EquipmentMap.GlyphRect(glyph), ring);
            x += 30;
            c.DrawString(font, new Vector2(x, y + 6), text, HorizontalAlignment.Left, -1, 16, Look.Muted);
            x += font.GetStringSize(text, HorizontalAlignment.Left, -1, 16).X + 26;
        }
    }

    void HintBox(Canvas c)
    {
        var font = Look.Ui;
        float w = font.GetStringSize(Hint, HorizontalAlignment.Left, -1, 16).X;
        c.DrawStyleBox(Ui.Frame(new Color("#0d0a13", 0.86f), new Color(Look.Edge, 0.8f), 8, 1, 0, 0), new Rect2(12, 12, w + 24, 32));
        c.DrawString(font, new Vector2(24, 34), Hint, HorizontalAlignment.Left, -1, 16, Look.Muted);
    }
}
