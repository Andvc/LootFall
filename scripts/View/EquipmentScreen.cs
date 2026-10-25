using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The equipment screen (G; docs/equipment.md, "The screen"): the knight as a paper doll on the left, the slots on the
/// right. The whole body first (level 1): click a region to zoom in. In a region every slot it has is listed, layer by
/// layer, with a marker on the doll for each; choose a slot to see every piece that can fill it, what wearing it would do
/// and what it would take off. A piece with slots of its own opens its board (level 3), drawn as the piece with its
/// slots round it, and a few of those go a level deeper. Putting something on flies it onto the doll, and what it takes
/// off flies away. Auto-equip wears the strongest set the slots allow; a choice made here turns it off. Esc steps out a
/// level at a time.
/// </summary>
public partial class EquipmentScreen : Overlay
{
    const float StageX = 24, StageY = 124, StageW = 1080, StageH = 812, SideX = 1124, SideW = 612;
    readonly Panel _p;
    readonly PaperDoll _doll;
    readonly BoardView _boardView;
    readonly HBoxContainer _crumbs;
    readonly Control _head;
    readonly ScrollContainer _scroll;
    readonly VBoxContainer _list;
    readonly Button _turn, _whole;
    string _region;
    int _piece = -1, _board = -1, _host = -1, _cell = -1;
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
        _doll.CellClicked += c =>
        {
            _cell = c;
            Rebuild();
        };
        _p.AddChild(_doll);
        _boardView = new BoardView(this).Place(StageX + 4, StageY + 4, StageW - 8, StageH - 8);
        _boardView.Visible = false;
        _p.AddChild(_boardView);
        _crumbs = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }.Place(StageX, 72, StageW, 44);
        _crumbs.AddThemeConstantOverride("separation", 6);
        _p.AddChild(_crumbs);
        _turn = Ui.Button("Turn round", () =>
        {
            _doll.Back = !_doll.Back;
            _doll.Frame(_region == "Back" && !_doll.Back ? null : _region, true);
        }, size: Look.Small).Place(StageX + StageW - 186, StageY + 14, 170, 44);
        _whole = Ui.Button("Whole body", ShowBody, size: Look.Small).Place(StageX + StageW - 366, StageY + 14, 170, 44);
        _p.AddChild(_whole);
        _p.AddChild(_turn);
        _doll.Hint = "Drag to look around, wheel to zoom, Esc to step back";
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

    public override void _Ready() => Main.Instance?.Moment(_region == null ? "equipment" : "equipment_" + _region.Replace(' ', '_').ToLowerInvariant(), 0.6);

    /// <summary>Esc: out of a piece or a board, then out of the region, then closed.</summary>
    public override void Back()
    {
        if (_board >= 0)
        {
            int host = _host;
            var hd = C.Slots[host];
            if (hd.Board != Anatomy.Body) ShowBoard(hd.Board, H.HostOf[hd.Board] >= 0 ? H.HostOf[hd.Board] : C.Boards[hd.Board].Hosts[0]);
            else ShowPiece(host);
        }
        else if (_piece >= 0) ShowRegion(_region ?? Describe.Region(C, C.Slots[_piece]));
        else if (_region != null) ShowBody();
        else Close();
    }

    // ------------------------------------------------------------------------------------------------ where we are

    public void ShowBody()
    {
        _region = null;
        _piece = _board = _host = _cell = -1;
        _doll.Visible = true;
        _boardView.Visible = false;
        _doll.Lit.Clear();
        _doll.Frame(null);
        Rebuild();
    }

    public void ShowRegion(string region, bool instant = false)
    {
        _region = region;
        _piece = _board = _host = _cell = -1;
        _doll.Visible = true;
        _boardView.Visible = false;
        _doll.Lit.Clear();
        _doll.Frame(region, instant);
        Rebuild();
    }

    public void ShowPiece(int s)
    {
        var d = C.Slots[s];
        _piece = s;
        _board = _host = -1;
        _cell = -1;
        string region = Describe.Region(C, d);
        if (d.Board == Anatomy.Body)
        {
            _boardView.Visible = false;
            _doll.Visible = true;
            if (_region != region) _doll.Frame(region);
            _region = region;
            _doll.Lit.Clear();
            foreach (int c in d.Takes) _doll.Lit.Add(c);
        }
        Rebuild();
    }

    public void ShowBoard(int board, int host)
    {
        _board = board;
        _host = host;
        _piece = -1;
        _cell = -1;
        _region = Describe.Region(C, C.Slots[host]);
        _doll.Visible = false;
        _boardView.Visible = true;
        _boardView.Show(board, host);
        Rebuild();
    }

    /// <summary>A slot on the board view was clicked.</summary>
    public void PickCell(int cell)
    {
        _cell = _cell == cell ? -1 : cell;
        Rebuild();
    }

    // ------------------------------------------------------------------------------------------------ the panel

    void Rebuild()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        foreach (var c in _head.GetChildren()) c.QueueFree();
        _cellRows.Clear();
        _doll.Refresh();
        _boardView.QueueRedraw();
        _turn.Visible = _whole.Visible = !_boardView.Visible;
        _doll.PickedCell = _cell;
        Crumbs();
        if (_board >= 0) BoardPanel();
        else if (_piece >= 0) PiecePanel(_piece);
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
        if (_board >= 0)
        {
            // The chain of hosts down to this board.
            var chain = new List<int>();
            for (int h = _host, guard = 0; h >= 0 && guard < 6; guard++)
            {
                chain.Insert(0, h);
                var hd = C.Slots[h];
                h = hd.Board == Anatomy.Body ? -1 : H.HostOf[hd.Board] >= 0 ? H.HostOf[hd.Board] : C.Boards[hd.Board].Hosts[0];
            }
            for (int i = 0; i < chain.Count; i++)
            {
                int h = chain[i];
                int b = i + 1 < chain.Count ? C.Slots[chain[i + 1]].Board : _board;
                path.Add((Describe.Base(C.Slots[h].Name), () => ShowBoard(b, h)));
            }
        }
        else if (_piece >= 0)
        {
            int s = _piece;
            path.Add((Describe.Base(C.Slots[s].Name), () => ShowPiece(s)));
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
        foreach (var region in Figure.Regions) RegionRow(region);
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
        b.MouseEntered += () => _doll.Hover = region;
        row.AddChild(b);
    }

    // ------------------------------------------------------------------------------------------------ level 2: a region

    void RegionPanel()
    {
        var cells = C.Cells.Where(c => c.Board == Anatomy.Body && c.Region == _region).ToList();
        int worn = C.Slots.Count(d => H.Active[d.Index] && Describe.Region(C, d) == _region);
        int open = C.Slots.Count(d => H.Open[d.Index] && Describe.Region(C, d) == _region);
        Head(_region, $"{cells.Count} slots, {worn} pieces worn, {open} kinds of piece open. Choose a slot (here or on the figure) to see what can fill it.");
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
        }
        if (_cell >= 0 && _cellRows.TryGetValue(_cell, out var row)) Callable.From(() => _scroll.EnsureControlVisible(row)).CallDeferred();
    }

    /// <summary>A slot: what fills it, or why nothing does. Click to see what could.</summary>
    void CellRow(Cell cell)
    {
        int fill = _doll.Filling(cell.Id), keep = _doll.Keeping(cell.Id);
        // A board's slot on the board view: what's set there by its host, even if it doesn't count.
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
        bool opened = cell.Board == Anatomy.Body || _host < 0 || C.Boards[cell.Board].Opens1(_host, cell.Id);
        string state;
        Color colour = Look.Muted;
        if (fill >= 0)
        {
            var it = H.Gear[fill];
            state = it != null ? Describe.Name(C, fill, it) : C.Slots[fill].Name;
            colour = it != null ? Look.TierText(it.Rarity) : Look.Text;
            if (!H.Active[fill]) state += " (waiting)";
        }
        else if (!opened) state = $"not on this {Describe.Base(C.Slots[_host].Name).ToLowerInvariant()}";
        else if (keep >= 0) state = $"kept free by the {Describe.Base(C.Slots[keep].Name).ToLowerInvariant()}";
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
        var canvas = new Canvas(c =>
        {
            var rect = new Rect2(0, 0, SideW - 22, 48);
            c.DrawStyleBox(Ui.Frame(picked ? new Color("#241c33") : new Color("#16111f"), picked ? Look.Gold : fill >= 0 ? Look.EdgeLight : Look.Edge, 8, picked ? 2 : 1, 0, 0), rect);
            if (shown >= 0 && H.Gear[shown] != null)
                ItemTile.Draw(c, new Rect2(6, 4, 40, 40), Describe.Family(C.Slots[shown]), H.Gear[shown].Rarity, numeral: false, tier: ItemTile.Tier(H.Gear[shown]));
            else c.DrawStyleBox(Ui.Frame(Colors.Transparent, keep >= 0 ? new Color(Look.Bad, 0.6f) : Look.Edge, 6, 1, 0, 0), new Rect2(6, 4, 40, 40));
            c.DrawString(Look.UiBold, new Vector2(56, 22), cell.Name, HorizontalAlignment.Left, 170, 17, Look.Text);
            c.DrawString(Look.Ui, new Vector2(56, 42), state, HorizontalAlignment.Left, SideW - 22 - 66, 16, colour);
            c.DrawString(Look.Ui, new Vector2(SideW - 22 - 150, 22), Layer(cell), HorizontalAlignment.Right, 140, 14, Look.Dim);
        }) { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 0, SideW - 22, 48);
        row.AddChild(canvas);
        var b = new Button { Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand }.Place(0, 0, SideW - 22, 48);
        b.Pressed += () =>
        {
            _cell = _cell == cell.Id ? -1 : cell.Id;
            Rebuild();
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
        var fills = Row(54);
        fills.AddChild(Ui.Text("Fills: " + Describe.Cells(C, d.Takes), Look.Small, Look.Text, Look.Ui).Wrapped().Place(8, 2, SideW - 40, 52));
        if (d.Keeps.Length > 0)
        {
            var keeps = Row(54);
            keeps.AddChild(Ui.Text("Keeps free: " + Describe.Cells(C, d.Keeps), Look.Small, Look.Bad.Lerp(Look.Text, 0.4f), Look.Ui).Wrapped().Place(8, 2, SideW - 40, 52));
        }
        // Its own slots.
        foreach (var (b, opens) in d.Brings)
        {
            var board = C.Boards[b];
            int set = board.Slots.Count(x => H.Worn[x] && H.Active[x]);
            var row = Row(60);
            row.AddChild(Ui.Text($"Its slots: {board.Name}", Look.Body, Look.Gold, Look.UiBold).Place(8, 4, 360, 28));
            row.AddChild(Ui.Text($"{opens.Length} of the board's {board.Size} slots, {set} filled", Look.Small, Look.Muted, Look.Ui).Place(8, 30, 360, 24));
            int bb = b;
            row.AddChild(Ui.Button("Open its slots", () => ShowBoard(bb, s), Look.Gold, Look.Small).Place(SideW - 22 - 200, 8, 196, 44));
        }
        if (open) Upgrades(s);
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

    // ------------------------------------------------------------------------------------------------ level 3 and 4: a piece's board

    void BoardPanel()
    {
        var board = C.Boards[_board];
        var host = C.Slots[_host];
        bool hosting = H.HostOf[_board] == _host;
        string sub = hosting ? $"What's set in the {Describe.Base(host.Name).ToLowerInvariant()} counts while it's worn."
            : H.HostOf[_board] >= 0 ? $"The {Describe.Base(C.Slots[H.HostOf[_board]].Name).ToLowerInvariant()} holds this board now; this shows the slots the {Describe.Base(host.Name).ToLowerInvariant()} would open."
            : $"Nothing worn brings this board now: what's set waits for the {Describe.Base(host.Name).ToLowerInvariant()} (or another that brings it).";
        Head($"{board.Name}: its slots", sub);
        foreach (int cid in C.Anatomy.Boards[_board].Cells)
        {
            var cell = C.Cells[cid];
            CellRow(cell);
            if (_cell == cid) Candidates(cell);
        }
        if (_cell >= 0 && _cellRows.TryGetValue(_cell, out var row)) Callable.From(() => _scroll.EnsureControlVisible(row)).CallDeferred();
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
        Vector2 spot = _boardView.Visible && d.Board == _board ? _boardView.SlotPoint(cell)
            : d.Board == Anatomy.Body ? _doll.GlobalCellPoint(cell) : _doll.GlobalOf(Figure.SiteAnchor(C.Cells[C.Slots[H.HostOf[d.Board] >= 0 ? H.HostOf[d.Board] : C.Boards[d.Board].Hosts[0]].Takes[0]].Site));
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

    // ------------------------------------------------------------------------------------------------ the board view

    /// <summary>
    /// A piece's board drawn as the piece in the middle (its picture, large) with its slots round it: what's set in each,
    /// the slots this piece doesn't open greyed, and lines to the middle. Clicking a slot opens it in the panel.
    /// </summary>
    public partial class BoardView : Control
    {
        readonly EquipmentScreen _e;
        int _board = -1, _host = -1;
        readonly List<(int Cell, Vector2 At)> _slots = new();
        float _t;

        public BoardView(EquipmentScreen e)
        {
            _e = e;
            MouseFilter = MouseFilterEnum.Stop;
        }

        public void Show(int board, int host)
        {
            _board = board;
            _host = host;
            QueueRedraw();
        }

        Content C => _e.C;
        Hero H => _e.H;

        public Vector2 SlotPoint(int cell)
        {
            foreach (var (c, at) in _slots)
                if (c == cell) return GetGlobalTransformWithCanvas() * at;
            return GetGlobalTransformWithCanvas() * (Size / 2);
        }

        public override void _Process(double delta)
        {
            _t += (float)delta;
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } b) return;
            foreach (var (c, at) in _slots)
                if (at.DistanceTo(b.Position) < 52)
                {
                    _e.PickCell(c);
                    AcceptEvent();
                    return;
                }
        }

        public override void _Draw()
        {
            _slots.Clear();
            if (_board < 0) return;
            var def = C.Boards[_board];
            var cells = C.Anatomy.Boards[_board].Cells;
            var centre = Size / 2 + new Vector2(0, -10);
            int hi = Array.IndexOf(def.Hosts, _host);
            var host = C.Slots[_host];
            var hostItem = H.Gear[_host];
            // The slots round the piece.
            float radius = Mathf.Min(Size.X, Size.Y) * 0.36f;
            for (int i = 0; i < cells.Count; i++)
            {
                float a = -Mathf.Pi / 2 + Mathf.Tau * i / cells.Count;
                var at = centre + new Vector2(Mathf.Cos(a) * radius * 1.25f, Mathf.Sin(a) * radius);
                _slots.Add((cells[i], at));
                bool opened = hi >= 0 && def.Opens[hi][i];
                DrawLine(centre + (at - centre).Normalized() * 110, at - (at - centre).Normalized() * 46, opened ? new Color(Look.Gold, 0.35f) : new Color(Look.Dim, 0.25f), 2, true);
            }
            // The piece itself.
            for (int i = 3; i >= 1; i--) DrawCircle(centre, 96 + 18 * i, new Color(hostItem != null ? Look.Tier(hostItem.Rarity) : Look.Edge, 0.05f), true, -1, true);
            ItemTile.Draw(this, new Rect2(centre - new Vector2(90, 90), new Vector2(180, 180)), host.Family, hostItem != null && H.Open[_host] ? hostItem.Rarity : -1,
                hostItem?.Affixes ?? 0, numeral: false, time: _t, tier: ItemTile.Tier(hostItem));
            DrawString(Look.Display, centre + new Vector2(-200, 128), host.Name, HorizontalAlignment.Center, 400, 26, Look.Gold);
            DrawString(Look.Ui, centre + new Vector2(-200, 154), H.HostOf[_board] == _host ? "worn: its slots count" : "not worn: its slots wait", HorizontalAlignment.Center, 400, 17,
                H.HostOf[_board] == _host ? Look.Good : Look.Muted);
            for (int i = 0; i < _slots.Count; i++)
            {
                var (cid, at) = _slots[i];
                var cell = C.Cells[cid];
                bool opened = hi >= 0 && def.Opens[hi][i];
                int fill = -1;
                foreach (int s in def.Slots)
                    if (H.Worn[s] && Array.IndexOf(C.Slots[s].Takes, cid) >= 0) fill = s;
                var rect = new Rect2(at - new Vector2(40, 40), new Vector2(80, 80));
                if (fill >= 0 && H.Gear[fill] != null)
                {
                    ItemTile.Draw(this, rect, C.Slots[fill].Family, H.Gear[fill].Rarity, numeral: false, time: _t, tier: ItemTile.Tier(H.Gear[fill]));
                    if (!H.Active[fill]) DrawRect(rect, new Color(0, 0, 0, 0.45f));
                    if (C.Slots[fill].Brings.Length > 0) Ink.Glint(this, rect.End - new Vector2(10, 10), 8, Look.Gold);
                }
                else DrawStyleBox(Ui.Frame(new Color("#120e19"), opened ? Look.EdgeLight : new Color(Look.Dim, 0.4f), 10, opened ? 2 : 1, 0, 0), rect);
                if (_e._cell == cid) DrawStyleBox(Ui.Frame(Colors.Transparent, Look.Gold, 12, 3, 0, 0), rect.Grow(5));
                DrawString(Look.Ui, at + new Vector2(-80, 60), cell.Name, HorizontalAlignment.Center, 160, 15, opened ? Look.Text : Look.Dim);
            }
        }
    }
}
