using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The equipment screen (G; docs/equipment.md, "The screen"): the knight on the left (<see cref="PaperDoll"/>), the slots
/// on the right. The whole body first, front and back, every slot a dot: click a part of him, or a dot, to zoom into its
/// region's page (the head, hands and feet on their close-ups). There every slot that shows has a marker and is listed,
/// site by site; a worn piece is one marker over what it covers, with its own slots on it (and listed under it), and the
/// slots it covers or keeps free don't show. Choose a slot to see every piece that can fill it, what wearing it would do
/// and what it would take off; choose a worn piece to see it in detail. Pointing at a row lights its place on the
/// figure. Putting something on flies it onto the figure, and what it takes off flies away. Auto-equip wears the
/// strongest set the slots allow; a choice made here turns it off. Esc steps out a level at a time.
/// </summary>
public partial class EquipmentScreen : Overlay
{
    const float StageX = 24, StageY = 124, StageW = 1080, StageH = 812, SideX = 1124, SideW = 612;
    readonly Panel _p;
    readonly PaperDoll _doll;
    /// <summary>The figure (for the demos, which point at it).</summary>
    public PaperDoll Doll => _doll;
    readonly HBoxContainer _crumbs;
    readonly Control _head;
    readonly ScrollContainer _scroll;
    readonly VBoxContainer _list;
    readonly Button _whole, _slots;
    string _region;
    int _piece = -1, _cell = -1;
    readonly Dictionary<int, Control> _cellRows = new();
    /// <summary>A line about the last thing done (shown once, under the body's heading).</summary>
    string _note;

    Session S => G.S;
    Hero H => G.S.H;
    Content C => G.S.C;

    public EquipmentScreen(GameScreen g, string region = null) : base(g)
    {
        _p = Frame("Equipment");
        _p.AddChild(Ui.Box(new Color("#0d0a13"), Look.Edge, 12).Place(StageX, StageY, StageW, StageH));
        _doll = new PaperDoll(g.S).Place(StageX + 4, StageY + 4, StageW - 8, StageH - 8);
        _doll.RegionClicked += r => ShowRegion(r);
        _doll.CellClicked += PickCell;
        _doll.PieceClicked += ShowPiece;
        _doll.BackClicked += Back;
        // A click beside the figure lets go of the slot chosen.
        _doll.NothingClicked += () =>
        {
            if (_cell < 0) return;
            _cell = -1;
            Rebuild();
        };
        _p.AddChild(_doll);
        _crumbs = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }.Place(StageX, 72, StageW, 44);
        _crumbs.AddThemeConstantOverride("separation", 6);
        _p.AddChild(_crumbs);
        _whole = Ui.Button("Whole body", ShowBody, size: Look.Small).Place(StageX + StageW - 186, StageY + 14, 170, 44);
        _slots = Ui.Button("Hide slots", () =>
        {
            _doll.ShowSlots(!_doll.ShowAll);
            _slots.Text = _doll.ShowAll ? "Hide slots" : "Show slots";
        }, size: Look.Small).Place(StageX + StageW - 186, StageY + 14, 170, 44);
        _p.AddChild(_slots);
        _p.AddChild(_whole);
        _head = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(SideX, 72, SideW, 150);
        _p.AddChild(_head);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.Place(SideX, 226, SideW, 712);
        _list = new VBoxContainer { CustomMinimumSize = new Vector2(SideW - 18, 0) };
        _list.AddThemeConstantOverride("separation", 4);
        _scroll.AddChild(_list);
        _p.AddChild(_scroll);
        if (region != null) ShowRegion(region, true);
        else ShowBody();
    }

    public override void _Ready() => Main.Instance?.Moment(_region == null ? "equipment" : "equipment_" + _region.Replace(' ', '_').ToLowerInvariant(), 0.8);

    double _check;
    int _signature;

    /// <summary>What's worn, in one number: auto-equip may change it while the screen is open (after a chest), and then
    /// the screen shows the new set.</summary>
    int Signature()
    {
        unchecked
        {
            int h = 17;
            for (int s = 0; s < C.SlotCount; s++)
                if (H.Active[s]) h = h * 31 + s * 7 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(H.Gear[s]);
            return h;
        }
    }

    public override void _Process(double delta)
    {
        _check -= delta;
        if (_check > 0) return;
        _check = 0.4;
        int sig = Signature();
        if (sig == _signature) return;
        bool first = _signature == 0;
        _signature = sig;
        if (!first) Rebuild();
    }

    /// <summary>Esc: out of a piece (to the piece it's set in, or its region), then out of the region, then closed.</summary>
    public override void Back()
    {
        if (_piece >= 0)
        {
            var d = C.Slots[_piece];
            int host = d.Board == Anatomy.Body ? -1 : H.HostOf[d.Board];
            if (host >= 0) ShowPiece(host);
            else ShowRegion(_region ?? Describe.Region(C, d));
        }
        else if (_cell >= 0) ShowRegion(_region);
        else if (_region != null) ShowBody();
        else Close();
    }

    // ------------------------------------------------------------------------------------------------ where we are

    public void ShowBody()
    {
        _region = null;
        _piece = _cell = -1;
        _doll.Show(null);
        Rebuild();
    }

    public void ShowRegion(string region) => ShowRegion(region, false);

    public void ShowRegion(string region, bool instant)
    {
        if (region == null)
        {
            ShowBody();
            return;
        }
        if (_region != region) _doll.Show(region, instant);
        _region = region;
        _piece = _cell = -1;
        Rebuild();
    }

    /// <summary>A piece in detail: its region's page, its area lit and the rest dimmed, its own slots listed.</summary>
    public void ShowPiece(int s)
    {
        var d = C.Slots[s];
        string region = Describe.Region(C, d);
        if (_region != region) _doll.Show(region);
        _region = region;
        _piece = s;
        _cell = -1;
        Rebuild();
    }

    /// <summary>A slot chosen (on the figure or in the list): on the body, its row opens in its region; a piece's own,
    /// in that piece's detail.</summary>
    public void PickCell(int cell)
    {
        var c = C.Cells[cell];
        if (c.Board != Anatomy.Body)
        {
            int host = H.HostOf[c.Board] >= 0 ? H.HostOf[c.Board] : C.Boards[c.Board].Hosts[0];
            if (_piece != host) ShowPiece(host);
            _cell = _cell == cell ? -1 : cell;
            Rebuild();
            return;
        }
        if (_region != c.Region) _doll.Show(c.Region);
        _region = c.Region;
        _piece = -1;
        _cell = _cell == cell ? -1 : cell;
        Rebuild();
    }

    /// <summary>Points at a piece, as the mouse over its row would (its area lit on the figure).</summary>
    public void Preview(int s) => _doll.PreviewPiece = s;

    // ------------------------------------------------------------------------------------------------ the panel

    void Rebuild()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        foreach (var c in _head.GetChildren()) c.QueueFree();
        _cellRows.Clear();
        _doll.Refresh();
        _whole.Visible = _region != null;
        _slots.Visible = _region == null;
        _doll.Hint = _region == null ? "Click a part of him (or turn the wheel over it) to zoom in, or a dot to go straight to that slot"
            : "Point at anything to see what it is. Wheel to zoom, right-click or Esc to step back";
        // The slot chosen, or the one the piece in detail is set in (its marker on its host).
        _doll.PickedCell = _cell >= 0 ? _cell : _piece >= 0 && C.Slots[_piece].Board != Anatomy.Body ? C.Slots[_piece].Takes[0] : -1;
        _doll.FocusPiece = _piece >= 0 ? (C.Slots[_piece].Board == Anatomy.Body ? _piece : _doll.Cov.BodyHost(_piece)) : -1;
        // A piece in detail that isn't worn: where it would go, lit.
        _doll.GhostPiece = _piece >= 0 && !H.Active[_piece] && C.Slots[_piece].Board == Anatomy.Body ? _piece : -1;
        _doll.PointedCell = _doll.PreviewPiece = -1;
        _doll.PointedRegion = null;
        Crumbs();
        if (_piece >= 0) PiecePanel(_piece);
        else if (_region != null) RegionPanel();
        else BodyPanel();
        G.Battle.RefreshGear();
        G.Tiles.Refresh();
    }

    void Head(string title, string sub)
    {
        _head.AddChild(Ui.Text(title, Look.Header, Look.Gold, Look.Display).Place(0, 0, SideW, 50));
        if (sub != null) _head.AddChild(Ui.Text(sub, Look.Small, Look.Muted, Look.Ui).Wrapped().Place(0, 54, SideW - 10, 96));
    }

    /// <summary>A row of wrapped text, as tall as the text.</summary>
    void TextRow(string text, int size, Color colour)
    {
        float w = SideW - 40;
        float h = Look.Ui.GetMultilineStringSize(text, HorizontalAlignment.Left, w, size).Y + 8;
        Row(h).AddChild(Ui.Text(text, size, colour, Look.Ui).Wrapped().Place(8, 2, w, h));
    }

    Control Row(float height)
    {
        var r = new Control { CustomMinimumSize = new Vector2(SideW - 22, height), MouseFilter = MouseFilterEnum.Pass };
        _list.AddChild(r);
        return r;
    }

    void Crumbs()
    {
        foreach (var c in _crumbs.GetChildren()) c.QueueFree();
        void Crumb(string text, Action go, bool last)
        {
            var b = Ui.Button(text, go, last ? Look.Gold : Look.EdgeLight, Look.Small);
            b.CustomMinimumSize = new Vector2(0, 42);
            _crumbs.AddChild(b);
            if (!last) _crumbs.AddChild(Ui.Text(">", Look.Body, Look.Dim, Look.UiBold));
        }
        var path = new List<(string, Action)> { ("Body", ShowBody) };
        if (_region != null)
        {
            string r = _region;
            path.Add((r, () => ShowRegion(r)));
        }
        if (_piece >= 0)
        {
            // The chain of pieces down to this one: the piece on the body, what's set in it, and so on.
            var chain = new List<int>();
            for (int h = _piece, guard = 0; h >= 0 && guard < 6; guard++)
            {
                chain.Insert(0, h);
                var hd = C.Slots[h];
                h = hd.Board == Anatomy.Body ? -1 : H.HostOf[hd.Board] >= 0 ? H.HostOf[hd.Board] : C.Boards[hd.Board].Hosts[0];
            }
            foreach (int h in chain) path.Add((Describe.Base(C.Slots[h].Name), () => ShowPiece(h)));
        }
        for (int i = 0; i < path.Count; i++) Crumb(path[i].Item1, path[i].Item2, i == path.Count - 1);
    }

    // ------------------------------------------------------------------------------------------------ level 1: the body

    void BodyPanel()
    {
        int worn = 0, kept = 0, open = 0;
        for (int s = 0; s < C.SlotCount; s++)
        {
            if (H.Open[s]) open++;
            if (H.Active[s]) worn++;
            else if (H.Open[s] && H.Gear[s] != null) kept++;
        }
        Head("The body", _note ?? $"{worn} pieces worn and {kept} more kept for other sets. {open} of the {C.SlotCount} kinds of piece are open. Click a part of the body to see its slots.");
        _note = null;
        var row = Row(118);
        row.AddChild(new Switch("Auto-equip", () => S.Auto.Equip, v =>
        {
            S.Auto.Equip = v;
            if (v) WearBest();
        }, Look.Body).Place(0, 4, 260, 50));
        row.AddChild(Ui.Button("Wear the best set", WearBest, Look.Gold, Look.Body).Place(272, 4, 300, 50));
        row.AddChild(Ui.Text("Auto-equip weighs every set the slots allow, boards and all, and wears the strongest. Putting something on by hand turns it off.",
            Look.Small, Look.Muted, Look.Ui).Wrapped().Place(0, 62, SideW - 30, 56));
        foreach (var region in EquipmentMap.Regions) RegionRow(region);
    }

    void RegionRow(string region)
    {
        int worn = 0, open = 0, total = 0, best = -1;
        var counts = new int[Look.Tiers];
        foreach (var d in C.Slots)
        {
            if (Describe.Region(C, d) != region) continue;
            total++;
            if (H.Open[d.Index]) open++;
            if (!H.Active[d.Index]) continue;
            worn++;
            int r = H.Gear[d.Index].Rarity;
            counts[r]++;
            best = Math.Max(best, r);
        }
        var row = Row(56);
        var canvas = new Canvas(c =>
        {
            var rect = new Rect2(0, 0, SideW - 22, 52);
            c.DrawStyleBox(Ui.Frame(Look.PanelLight, best >= 0 ? Look.Tier(best).Darkened(0.2f) : Look.Edge, 10, 2, 4, 0), rect);
            c.DrawString(Look.UiBold, new Vector2(16, 33), region.ToUpperInvariant(), HorizontalAlignment.Left, 220, 19, open > 0 ? Look.Text : Look.Dim);
            c.DrawString(Look.Numbers, new Vector2(230, 33), $"{worn} worn  |  {open}/{total} open", HorizontalAlignment.Left, 220, 18, Look.Muted);
            float x = 440, w = SideW - 22 - 456;
            if (worn > 0)
                for (int t = 0; t < counts.Length; t++)
                {
                    if (counts[t] == 0) continue;
                    float ww = w * counts[t] / worn;
                    c.DrawRect(new Rect2(x, 22, ww, 10), Look.Tier(t));
                    x += ww;
                }
        }) { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 0, SideW - 22, 52);
        row.AddChild(canvas);
        var b = new Button { Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand }.Place(0, 0, SideW - 22, 52);
        b.Pressed += () => ShowRegion(region);
        b.MouseEntered += () => _doll.PointedRegion = region;
        b.MouseExited += () =>
        {
            if (_doll.PointedRegion == region) _doll.PointedRegion = null;
        };
        row.AddChild(b);
    }

    // ------------------------------------------------------------------------------------------------ level 2: a region

    void RegionPanel()
    {
        var cells = C.Cells.Where(c => c.Board == Anatomy.Body && c.Region == _region).ToList();
        int worn = C.Slots.Count(d => H.Active[d.Index] && Describe.Region(C, d) == _region);
        int open = C.Slots.Count(d => H.Open[d.Index] && Describe.Region(C, d) == _region);
        Head(_region, $"{cells.Count} slots, {worn} pieces worn, {open} kinds of piece open. Choose a slot (here or on the figure) to see what can fill it, or a worn piece to see it.");
        string site = null;
        foreach (var cell in cells)
        {
            if (cell.Site != site)
            {
                site = cell.Site;
                var head = Row(30);
                head.AddChild(Ui.Text(cell.SiteName.ToUpperInvariant(), 17, Look.Muted, Look.UiBold).Place(4, 6, SideW - 30, 24));
            }
            CellRow(cell);
            if (_cell == cell.Id) Candidates(cell);
            // A worn piece's own slots, under the first slot it fills.
            int fill = _doll.Filling(cell.Id);
            if (fill >= 0 && C.Slots[fill].Takes[0] == cell.Id)
                foreach (int own in _doll.Cov.OwnSlots(fill))
                {
                    CellRow(C.Cells[own], 28);
                    if (_cell == own) Candidates(C.Cells[own]);
                }
        }
        if (_cell >= 0 && _cellRows.TryGetValue(_cell, out var row)) Callable.From(() => _scroll.EnsureControlVisible(row)).CallDeferred();
    }

    /// <summary>A slot: what fills it, or why nothing does. Click to see what could; point at it to light it on the
    /// figure.</summary>
    void CellRow(Cell cell, float indent = 0)
    {
        int fill = _doll.Filling(cell.Id), keep = _doll.Keeping(cell.Id);
        int host = cell.Board == Anatomy.Body ? -1 : _piece >= 0 && C.Slots[_piece].Brings.Any(b => b.Board == cell.Board) ? _piece : H.HostOf[cell.Board];
        int under = -1;
        if (cell.Board == Anatomy.Body && fill < 0 && keep < 0)
            foreach (var v in new[] { "front", "back" })
                if (EquipmentMap.View(v) is { } mv && mv.Points.ContainsKey(cell.Key) && _doll.Cov.HiddenBy(cell.Id, mv) is var by && by >= 0) under = by;
        // A board's slot: what's set there, even if it doesn't count now.
        if (cell.Board != Anatomy.Body)
        {
            fill = -1;
            foreach (int s in C.Boards[cell.Board].Slots)
                if (H.Worn[s] && Array.IndexOf(C.Slots[s].Takes, cell.Id) >= 0) fill = s;
            keep = -1;
            foreach (int s in C.Boards[cell.Board].Slots)
                if (H.Worn[s] && Array.IndexOf(C.Slots[s].Keeps, cell.Id) >= 0) keep = s;
        }
        var candidates = Pieces(cell);
        bool anyOpen = candidates.Any(s => H.Open[s]);
        bool opened = cell.Board == Anatomy.Body || host < 0 || C.Boards[cell.Board].Opens1(host, cell.Id);
        string state;
        Color colour = Look.Muted;
        if (fill >= 0)
        {
            var it = H.Gear[fill];
            state = it != null ? Describe.Name(C, fill, it) : C.Slots[fill].Name;
            colour = it != null ? Look.TierText(it.Rarity) : Look.Text;
            if (!H.Active[fill]) state += " (waiting)";
        }
        else if (!opened) state = $"not on this {Describe.Base(C.Slots[host].Name).ToLowerInvariant()}";
        else if (keep >= 0) state = $"kept free by the {Describe.Base(C.Slots[keep].Name).ToLowerInvariant()}";
        else if (under >= 0 && candidates.Any(s => H.Open[s] && H.Gear[s] != null)) state = $"free, under the {Describe.Base(C.Slots[under].Name).ToLowerInvariant()}";
        else if (!anyOpen)
        {
            int first = candidates.Where(s => !C.Slots[s].Prestige).Select(s => C.Slots[s].UnlockDepth).DefaultIfEmpty(0).Min();
            state = first > 0 ? $"opens at depth {Format.Whole(first)}" : "opened by transcending";
        }
        else if (candidates.All(s => H.Gear[s] == null)) state = "nothing found yet";
        else state = "free";
        bool picked = _cell == cell.Id;
        var row = Row(50);
        _cellRows[cell.Id] = row;
        int shown = fill;
        float w = SideW - 22 - indent;
        var canvas = new Canvas(c =>
        {
            var rect = new Rect2(0, 0, w, 48);
            c.DrawStyleBox(Ui.Frame(picked ? new Color("#241c33") : new Color("#16111f"), picked ? Look.Gold : fill >= 0 ? Look.EdgeLight : Look.Edge, 8, picked ? 2 : 1, 0, 0), rect);
            if (shown >= 0 && H.Gear[shown] != null)
                ItemTile.Draw(c, new Rect2(6, 4, 40, 40), Describe.Family(C.Slots[shown]), H.Gear[shown].Rarity, numeral: false, tier: ItemTile.Tier(H.Gear[shown]));
            else
            {
                c.DrawStyleBox(Ui.Frame(Colors.Transparent, keep >= 0 ? new Color(Look.Bad, 0.6f) : Look.Edge, 6, 1, 0, 0), new Rect2(6, 4, 40, 40));
                if (EquipmentMap.Glyphs is { } g)
                    c.DrawTextureRectRegion(g, new Rect2(14, 12, 24, 24), EquipmentMap.GlyphRect(cell.Board == Anatomy.Body ? EquipmentMap.GlyphOf(cell.Layer) : "mod"),
                        new Color(Look.Muted, 0.8f));
            }
            c.DrawString(Look.UiBold, new Vector2(56, 22), cell.Name, HorizontalAlignment.Left, 170, 17, Look.Text);
            c.DrawString(Look.Ui, new Vector2(56, 42), state, HorizontalAlignment.Left, w - 66, 16, colour);
            c.DrawString(Look.Ui, new Vector2(w - 150, 22), cell.Board == Anatomy.Body ? Layer(cell) : "its own slot", HorizontalAlignment.Right, 140, 14, Look.Dim);
        }) { MouseFilter = MouseFilterEnum.Ignore }.Place(indent, 0, w, 48);
        row.AddChild(canvas);
        if (indent > 0) row.AddChild(new Canvas(c => c.DrawLine(new Vector2(indent / 2, -4), new Vector2(indent / 2, 24), Look.Edge, 2)) { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 0, indent, 48));
        var b = new Button { Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand }.Place(indent, 0, w, 48);
        b.Pressed += () =>
        {
            _cell = _cell == cell.Id ? -1 : cell.Id;
            Rebuild();
        };
        b.MouseEntered += () => _doll.PointedCell = cell.Id;
        b.MouseExited += () =>
        {
            if (_doll.PointedCell == cell.Id) _doll.PointedCell = -1;
        };
        row.AddChild(b);
    }

    static string Layer(Cell c) => c.Layer switch
    {
        Progression.Layer.Skin => "on the skin", Progression.Layer.Under => "under", Progression.Layer.Mail => "mail",
        Progression.Layer.Armor => "plate", Progression.Layer.Over => "over", Progression.Layer.Adorn => "jewellery",
        Progression.Layer.Hold => "held", Progression.Layer.Graft => "grown", Progression.Layer.Beyond => "around you",
        _ => "",
    };

    /// <summary>The pieces that fill a cell, worn first, then open ones, then by when they open.</summary>
    List<int> Pieces(Cell cell) => C.Boards[cell.Board].Slots.Where(s => Array.IndexOf(C.Slots[s].Takes, cell.Id) >= 0)
        .OrderByDescending(s => H.Worn[s]).ThenByDescending(s => H.Open[s]).ThenByDescending(s => H.Gear[s] != null).ThenBy(s => C.Slots[s].UnlockDepth).ToList();

    void Candidates(Cell cell)
    {
        foreach (int s in Pieces(cell)) PieceRow(s);
    }

    /// <summary>A piece: its item (or why it has none), whether it's worn, and Wear or Take off (with what that would do).</summary>
    void PieceRow(int s)
    {
        var d = C.Slots[s];
        var it = H.Gear[s];
        var row = Row(70);
        bool open = H.Open[s];
        string sub = !open ? (d.Prestige ? "opened by transcending" : $"opens at depth {Format.Whole(d.UnlockDepth)}")
            : it == null ? "none found yet" : Describe.Name(C, s, it);
        var (state, stateColour) = State(s);
        var canvas = new Canvas(c =>
        {
            var rect = new Rect2(24, 0, SideW - 46, 66);
            c.DrawStyleBox(Ui.Frame(new Color("#120e19"), H.Worn[s] ? Look.Good.Darkened(0.3f) : Look.Edge, 8, 1, 0, 0), rect);
            ItemTile.Draw(c, new Rect2(32, 6, 54, 54), Describe.Family(d), open && it != null ? it.Rarity : -1, it?.Affixes ?? 0, locked: !open, tier: ItemTile.Tier(it));
            c.DrawString(Look.UiBold, new Vector2(96, 24), d.Name, HorizontalAlignment.Left, 230, 17, open ? Look.Text : Look.Dim);
            c.DrawString(Look.Ui, new Vector2(96, 44), sub, HorizontalAlignment.Left, 230, 15, it != null && open ? Look.TierText(it.Rarity) : Look.Dim);
            c.DrawString(Look.Ui, new Vector2(96, 62), state, HorizontalAlignment.Left, 230, 14, stateColour);
        }) { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 0, SideW - 22, 68);
        row.AddChild(canvas);
        var whole = new Button { Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand }.Place(24, 0, SideW - 46, 66);
        whole.Pressed += () => ShowPiece(s);
        row.AddChild(whole);
        if (open && it != null)
        {
            // The button says what it would do to the knight's power.
            if (H.Worn[s])
            {
                double power = S.PreviewTakeOff(s);
                var off = Ui.Button($"Take off  {Format.Percent(power - 1, sign: true)}", null, Look.EdgeLight, Look.Small).Place(SideW - 22 - 254, 12, 150, 44);
                off.TooltipText = "Takes it off; the slots it fills stay empty until you wear something else.";
                off.Pressed += () => TakeOff(s, off);
                row.AddChild(off);
            }
            else
            {
                var (power, gone) = S.PreviewWear(s);
                var on = Ui.Button($"Wear  {Format.Percent(power - 1, sign: true)}", null, power > 1.0005 ? Look.Good : Look.EdgeLight, Look.Small).Place(SideW - 22 - 254, 12, 150, 44);
                on.TooltipText = gone.Count > 0 ? "Takes off: " + string.Join(", ", gone.Select(o => C.Slots[o].Name)) : "Takes nothing off.";
                on.Pressed += () => Wear(s, on);
                row.AddChild(on);
            }
        }
        var more = Ui.Button(">", () => ShowPiece(s), size: Look.Small).Place(SideW - 22 - 96, 12, 70, 44);
        more.TooltipText = "This piece in detail";
        row.AddChild(more);
        // Pointing at the piece lights on the figure what it would cover and keep free.
        foreach (var c in row.GetChildren())
            if (c is Control b && b.MouseFilter != MouseFilterEnum.Ignore)
            {
                b.MouseEntered += () => _doll.PreviewPiece = s;
                b.MouseExited += () =>
                {
                    if (_doll.PreviewPiece == s) _doll.PreviewPiece = -1;
                };
            }
    }

    /// <summary>Whether a piece is worn, and if not why not.</summary>
    (string, Color) State(int s)
    {
        var d = C.Slots[s];
        if (!H.Open[s] || H.Gear[s] == null) return ("", Look.Dim);
        if (H.Active[s]) return ("worn", Look.Good);
        if (H.Worn[s])
        {
            int host = H.HostOf[d.Board];
            return host < 0 ? ($"set, waiting for a {C.Boards[d.Board].Name.ToLowerInvariant()}", Look.Gold)
                : ($"set, but this {Describe.Base(C.Slots[host].Name).ToLowerInvariant()} has no room for it", Look.Gold);
        }
        foreach (int c in d.Conflicts)
            if (H.Worn[c]) return ($"kept: the {Describe.Base(C.Slots[c].Name).ToLowerInvariant()} is in its way", Look.Muted);
        return ("kept, not worn", Look.Muted);
    }

    // ------------------------------------------------------------------------------------------------ a piece in detail

    void PiecePanel(int s)
    {
        var d = C.Slots[s];
        var it = H.Gear[s];
        bool open = H.Open[s];
        Head(d.Name, Describe.Where(C, d) + (d.Level > 2 ? $"  |  level {d.Level}" : ""));
        var top = Row(150);
        int sh = s;
        top.AddChild(new Canvas(c => ItemTile.Draw(c, new Rect2(0, 0, 132, 132), Describe.Family(C.Slots[sh]), open && H.Gear[sh] != null ? H.Gear[sh].Rarity : -1,
            H.Gear[sh]?.Affixes ?? 0, locked: !open, tier: ItemTile.Tier(H.Gear[sh]))) { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 6, 132, 132));
        var (state, colour) = State(s);
        if (!open) top.AddChild(Ui.Text(d.Prestige ? "Opened by the transcendence tree's Celestial Slots." : $"Opens when you first reach depth {Format.Whole(d.UnlockDepth)}.",
            Look.Body, Look.Muted, Look.Ui).Wrapped().Place(150, 10, SideW - 180, 80));
        else if (it == null) top.AddChild(Ui.Text("None found yet. The next chest may hold one.", Look.Body, Look.Muted, Look.Ui).Wrapped().Place(150, 10, SideW - 180, 80));
        else
        {
            top.AddChild(Ui.Text(Describe.Name(C, s, it), Look.Sub, Look.TierText(it.Rarity), Look.UiBold).Wrapped().Place(150, 0, SideW - 180, 64));
            top.AddChild(Ui.Text($"{Look.TierName(it.Rarity)} {Look.Numeral(it.Rarity)}  |  item level {Format.Whole((long)it.Ilvl)}  |  {Describe.Era(it.Ilvl)} era",
                Look.Small, Look.Muted, Look.Numbers).Place(150, 66, SideW - 180, 26));
            top.AddChild(Ui.Text(state, Look.Body, colour, Look.UiBold).Wrapped().Place(150, 92, SideW - 180, 56));
        }
        if (open && it != null)
        {
            var actions = Row(56);
            if (H.Worn[s])
            {
                var off = Ui.Button("Take off", null, Look.EdgeLight, Look.Body).Place(0, 4, 200, 48);
                off.Pressed += () => TakeOff(s, off);
                actions.AddChild(off);
                actions.AddChild(Ui.Text($"Taking it off: power {Format.Percent(S.PreviewTakeOff(s) - 1, sign: true)}", Look.Small, Look.Muted, Look.Ui).Wrapped().Place(214, 4, SideW - 240, 48));
            }
            else
            {
                var (power, gone) = S.PreviewWear(s);
                var on = Ui.Button("Wear", null, power >= 1 ? Look.Good : Look.EdgeLight, Look.Body).Place(0, 4, 200, 48);
                on.Pressed += () => Wear(s, on);
                actions.AddChild(on);
                string what = $"Wearing it: power {Format.Percent(power - 1, sign: true)}" + (gone.Count > 0 ? "; takes off " + string.Join(", ", gone.Select(o => C.Slots[o].Name)) : "");
                actions.AddChild(Ui.Text(what, Look.Small, Look.Muted, Look.Ui).Wrapped().Place(214, 0, SideW - 240, 56));
            }
            foreach (var line in Describe.Lines(H, s, it).Take(8))
            {
                var l = Row(26);
                l.AddChild(Ui.Text(line, Look.Body, Look.Text, Look.Ui).Place(8, 0, SideW - 40, 26));
            }
        }
        // Where it goes.
        TextRow("Fills: " + Describe.Cells(C, d.Takes), Look.Small, Look.Text);
        if (d.Keeps.Length > 0) TextRow("Keeps free: " + Describe.Cells(C, d.Keeps), Look.Small, Look.Bad.Lerp(Look.Text, 0.4f));
        // Its own slots: the ones it opens first, then the rest of its board's (another piece of the kind may open them).
        foreach (var (b, opens) in d.Brings)
        {
            var board = C.Boards[b];
            int set = board.Slots.Count(x => H.Worn[x] && H.Active[x]);
            var head = Row(34);
            head.AddChild(Ui.Text($"ITS SLOTS: {board.Name.ToUpperInvariant()}", 17, Look.Gold, Look.UiBold).Place(4, 8, 330, 24));
            head.AddChild(Ui.Text($"{opens.Length} of {board.Size} open, {set} filled", Look.Small, Look.Muted, Look.Ui, HorizontalAlignment.Right).Place(330, 8, SideW - 360, 24));
            foreach (int id in opens.Concat(C.Anatomy.Boards[b].Cells.Except(opens)))
            {
                var cell = C.Cells[id];
                CellRow(cell, 12);
                if (_cell == cell.Id) Candidates(cell);
            }
        }
        // What it covers: the slots under it, still there to fill.
        if (d.Board == Anatomy.Body && H.Active[s])
        {
            var under = new List<int>();
            foreach (var v in new[] { "front", "back" })
                if (EquipmentMap.View(v) is { } mv)
                    foreach (int c in _doll.Cov.Beneath(s, mv))
                        if (!under.Contains(c) && mv.Points.ContainsKey(C.Cells[c].Key)) under.Add(c);
            if (under.Count > 0)
            {
                var head = Row(34);
                head.AddChild(Ui.Text("UNDER IT OR KEPT FREE BY IT", 17, Look.Muted, Look.UiBold).Place(4, 8, SideW - 30, 24));
                foreach (int c in under)
                {
                    CellRow(C.Cells[c], 12);
                    if (_cell == c) Candidates(C.Cells[c]);
                }
            }
        }
        if (open) Upgrades(s);
        if (_cell >= 0 && _cellRows.TryGetValue(_cell, out var picked)) Callable.From(() => _scroll.EnsureControlVisible(picked)).CallDeferred();
    }

    /// <summary>Forge, rank and mastery for a piece (what the armoury's detail had).</summary>
    void Upgrades(int s)
    {
        var it = H.Gear[s];
        if (S.Unlocked(Feature.Forge))
        {
            var row = Row(52);
            row.AddChild(Ui.Text($"Forge +{H.Forge[s]}", Look.Body, Look.Gold, Look.UiBold).Place(8, 10, 200, 30));
            if (it != null)
            {
                int can = S.ForgeAffordable(s);
                var b1 = Ui.Button($"+1  ({Format.Num(S.ForgeCost(s, 1))})", () => { if (S.BuyForge(s, 1)) Bought(Look.Gold); }, Look.Gold, Look.Small).Place(200, 4, 190, 44);
                b1.Disabled = can < 1;
                row.AddChild(b1);
                var bm = Ui.Button(can > 1 ? $"+{can}" : "max", () => { if (S.BuyForge(s, S.ForgeAffordable(s))) Bought(Look.Gold); }, Look.Gold, Look.Small).Place(400, 4, 150, 44);
                bm.Disabled = can < 2;
                row.AddChild(bm);
            }
        }
        if (S.Unlocked(Feature.Ranks))
        {
            var row = Row(52);
            row.AddChild(Ui.Text($"Rank {H.Rank[s]}", Look.Body, Look.Shards, Look.UiBold).Place(8, 10, 200, 30));
            double cost = H.RankCost(s);
            var b = Ui.Button($"Rank up  ({Format.Num(cost)} shards)", () => { if (S.RankUp(s)) Bought(Look.Shards); }, Look.Shards, Look.Small).Place(200, 4, 350, 44);
            b.Disabled = cost > H.Materials;
            row.AddChild(b);
        }
        if (S.Unlocked(Feature.Mastery))
        {
            var row = Row(52);
            row.AddChild(Ui.Text($"Mastery {H.Mastery[s]}", Look.Body, Look.Xp, Look.UiBold).Place(8, 10, 200, 30));
            var bar = new Bar(Look.Xp).Place(200, 18, 180, 14);
            bar.Set((float)Math.Clamp(H.MasteryXp[s] / Math.Max(1e-9, H.MasteryNeed(s)), 0, 1), true);
            row.AddChild(bar);
            var f = Ui.Button(H.Focus[s] ? "Focused" : $"Focus ({S.FocusCount}/{H.FocusSlots})", () =>
            {
                S.ToggleFocus(s);
                Rebuild();
            }, H.Focus[s] ? Look.Xp : Look.EdgeLight, Look.Small).Place(400, 4, 150, 44);
            f.Disabled = !H.Focus[s] && (!H.Active[s] || S.FocusCount >= H.FocusSlots);
            row.AddChild(f);
        }
    }

    void Bought(Color c)
    {
        Main.Instance?.Sfx?.PlayGroup("equip_metal", 1.1f, -8);
        G.Battle.Hero.Flash(c, 0.4f);
        Rebuild();
    }

    // ------------------------------------------------------------------------------------------------ putting on and taking off

    void WearBest()
    {
        var before = (bool[])H.Worn.Clone();
        if (S.WearBest())
        {
            Main.Instance?.Sfx?.PlayGroup("equip_metal", 1f, -6);
            int changed = 0;
            for (int s = 0; s < C.SlotCount; s++)
                if (before[s] != H.Worn[s] && C.Slots[s].Board == Anatomy.Body && changed++ < 30)
                    _doll.FlashSite(C.Cells[C.Slots[s].Takes[0]].Site, H.Worn[s] && H.Gear[s] != null ? Look.Tier(H.Gear[s].Rarity) : Look.Muted);
            _note = $"The best set is on: {changed} pieces changed.";
        }
        else _note = "The best set is already on.";
        Rebuild();
    }

    void Wear(int s, Control from)
    {
        var off = S.Wear(s);
        if (off == null) return;
        Fly(s, from, true);
        foreach (int o in off) Fly(o, null, false);
        Rebuild();
    }

    void TakeOff(int s, Control from)
    {
        if (!S.TakeOff(s)) return;
        Fly(s, from, false);
        Rebuild();
    }

    /// <summary>A piece flies from its button onto the doll (or its board's slot), or off it.</summary>
    void Fly(int s, Control from, bool on)
    {
        var d = C.Slots[s];
        var it = H.Gear[s];
        if (it == null) return;
        int cell = d.Takes[0];
        Vector2 spot = _doll.GlobalCellPoint(cell);
        var panel = from != null ? from.GetGlobalRect().GetCenter() : new Vector2(SideX + 80 + 80, 600);
        var icon = new Painter(p => ItemTile.Draw(p, new Rect2(-30, -30, 60, 60), d.Family, it.Rarity, numeral: false, tier: ItemTile.Tier(it))) { Position = on ? panel : spot, ZIndex = 50 };
        AddChild(icon);
        var colour = Look.Tier(it.Rarity);
        bool metal = d.Kind is SlotKind.Armor or SlotKind.Weapon;
        Fx.Fly(icon, on ? spot : panel + new Vector2(0, 40), 0.42f, on ? 120 : 60, () =>
        {
            if (!IsInstanceValid(this)) return;
            if (on)
            {
                Fx.Burst(this, spot, colour, 14, 280, 180, 300, 0.45f, 0.08f, "star1");
                Fx.Ring(this, spot, colour, 70, 0.35f, 4);
                Main.Instance?.Sfx?.PlayGroup(metal ? "equip_metal" : "equip_cloth", 1f + 0.05f * it.Rarity, -6);
                if (d.Board == Anatomy.Body) _doll.FlashSite(C.Cells[cell].Site, colour);
            }
        });
        if (!on)
        {
            var t = icon.CreateTween();
            t.TweenProperty(icon, "modulate:a", 0f, 0.42f);
        }
    }
}
