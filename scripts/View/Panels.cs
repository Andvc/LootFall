using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The chest tray (bottom left): the pile of chests waiting, Open (Space) and Open all, and the auto-open switch once it
/// opens, with which chests still wait for the player (<see cref="Automation.HoldAt"/>). Chests land here after falling
/// out of the monster that dropped them, with a beam in the colour of the rarest thing inside; with auto-open on, opened
/// chests burst here and their upgrades fly to the armoury.
/// </summary>
public partial class ChestTray : Control
{
    readonly GameScreen _g;
    readonly Canvas _pile;
    readonly Label _count, _hint, _holdTitle, _hold;
    readonly Button _open, _all, _auto, _holdLess, _holdMore;
    float _t, _bump;
    public static readonly Vector2 PileAt = new(150, 280);

    public ChestTray(GameScreen g)
    {
        _g = g;
        MouseFilter = MouseFilterEnum.Ignore;
        this.Place(0, Look.LaneH, 520, Look.H - Look.LaneH);
        AddChild(Ui.Text("CHESTS", 20, Look.Muted, Look.UiBold).Place(28, 16, 200, 28));
        _pile = new Canvas(DrawPile).Place(0, 0, 300, 440);
        AddChild(_pile);
        _count = Ui.Text("", 28, Look.Text, Look.Numbers, HorizontalAlignment.Center, 6).Place(30, 330, 240, 40);
        AddChild(_count);
        _open = Ui.Button("Open", () => g.OpenNext(), Look.Gold, 30, Look.Strong).Place(300, 70, 196, 84);
        AddChild(_open);
        _all = Ui.Button("Open all", () => g.OpenAll(), size: Look.Body).Place(300, 166, 196, 56);
        AddChild(_all);
        _auto = Ui.Button("", ToggleAuto, size: Look.Body).Place(300, 234, 196, 56);
        AddChild(_auto);
        _holdTitle = Ui.Text("Waiting for you:", Look.Small, Look.Muted, Look.Ui).Place(300, 298, 196, 26);
        _holdLess = Ui.Button("<", () => StepHold(-1), size: Look.Small).Place(300, 326, 40, 40);
        _hold = Ui.Text("", Look.Small, Look.Text, Look.UiBold, HorizontalAlignment.Center).Place(340, 333, 116, 26);
        _holdMore = Ui.Button(">", () => StepHold(1), size: Look.Small).Place(456, 326, 40, 40);
        foreach (var c in new Control[] { _holdTitle, _holdLess, _hold, _holdMore }) AddChild(c);
        _hint = Ui.Text("", Look.Small, Look.Muted, Look.Ui).Wrapped().Place(300, 296, 200, 120);
        AddChild(_hint);
        Refresh();
    }

    /// <summary>Steps which chests wait for the player while auto-open is on: from Rare and rarer, through each tier, to
    /// the newest tier open (the default, which follows new tiers as they open), to none.</summary>
    void StepHold(int dir)
    {
        var auto = _g.S.Auto;
        var options = new List<int>();
        for (int r = 2; r < _g.S.H.MaxRarity; r++) options.Add(r);
        options.Add(Automation.HoldNewest);
        options.Add(Automation.HoldNone);
        int i = options.IndexOf(auto.HoldAt);
        if (i < 0) i = options.Count - 2;
        auto.HoldAt = options[Math.Clamp(i + dir, 0, options.Count - 1)];
        Refresh();
    }

    void ToggleAuto()
    {
        _g.S.Auto.OpenChests = !_g.S.Auto.OpenChests;
        Refresh();
    }

    public void Refresh()
    {
        int n = _g.S.Pile.Waiting.Count;
        _count.Text = n == 0 ? "none waiting" : n == 1 ? "1 chest" : $"{n} chests";
        _open.Disabled = n == 0;
        _all.Disabled = n < 2;
        bool can = _g.S.Unlocked(Feature.AutoOpen);
        _auto.Visible = can;
        _auto.Text = _g.S.Auto.OpenChests ? "Auto-open: on" : "Auto-open: off";
        Ui.Style(_auto, _g.S.Auto.OpenChests ? Look.Good : Look.EdgeLight, Look.Body);
        bool holding = can && _g.S.Auto.OpenChests;
        foreach (var c in new Control[] { _holdTitle, _holdLess, _hold, _holdMore }) c.Visible = holding;
        int hold = _g.S.Auto.HoldAt;
        if (holding)
        {
            int tier = Math.Min(_g.S.HoldTier, Look.Tiers - 1);
            _hold.Text = hold == Automation.HoldNone ? "nothing" : $"{Look.TierName(tier)}+";
            _hold.AddThemeColorOverride("font_color", hold == Automation.HoldNone ? Look.Muted : Look.TierText(tier));
        }
        _hint.Position = new Vector2(300, holding ? 374 : 296);
        _hint.Text = !can ? "Space opens the next chest. Auto-open comes at depth 10."
            : !holding ? "Space opens the next chest."
            : hold == Automation.HoldNewest ? "The newest tier waits, whichever it is."
            : hold == Automation.HoldNone ? "Every chest opens as it drops."
            : "Chests holding that or rarer wait.";
        _pile.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _bump = Mathf.MoveToward(_bump, 0, (float)delta * 4);
        _pile.QueueRedraw();
    }

    void DrawPile(Canvas c)
    {
        var waiting = _g.S.Pile.Waiting;
        int n = Math.Min(4, waiting.Count);
        var at = new Vector2(150, 300);
        if (n == 0)
        {
            c.DrawColoredPolygon(Ink.Ellipse(at, 110, 18), new Color(0, 0, 0, 0.3f));
            c.DrawArc(at + new Vector2(0, -60), 60, 0, Mathf.Tau, 40, new Color(Look.Edge, 0.6f), 3, true);
            return;
        }
        c.DrawColoredPolygon(Ink.Ellipse(at + new Vector2(0, 4), 130, 20), new Color(0, 0, 0, 0.35f));
        var next = _g.S.Pile.Next();
        for (int i = n - 1; i >= 0; i--)
        {
            var chest = i == 0 ? next : waiting[waiting.Count - 1 - i];
            float x = at.X + (i % 2 == 0 ? -1 : 1) * (i * 16), y = at.Y - i * 26;
            float s = 1 + (i == 0 ? _bump * 0.12f : 0);
            float w = (i == 0 ? 200 : 170) * s;
            var foot = new Vector2(i == 0 ? at.X : x, i == 0 ? at.Y : y - 6);
            if (i == 0) foot.Y = at.Y - Math.Min(3, n - 1) * 26 - 4;
            ChestArt.Draw(c, foot, w, chest.Kind, chest.BestRarity, 0, i == 0 ? 1 : 0, _t);
        }
    }

    /// <summary>A chest leaves the monster that dropped it (a beam for Epic and up) and falls into the pile.</summary>
    public void Drop(Chest chest, Vector2 from)
    {
        var node = new Painter { Position = from };
        float t0 = _t;
        node.OnDraw = p => ChestArt.Draw(p, Vector2.Zero, 90, chest.Kind, chest.BestRarity, 0, 0.8f, _t - t0);
        _g.Fly.AddChild(node);
        if (chest.BestRarity >= 3) Fx.Beam(_g.Fly, from + new Vector2(0, 30), Look.Tier(chest.BestRarity), 80, 640, 1.4f);
        Fx.Burst(_g.Fly, from, chest.BestRarity >= 0 ? Look.Tier(chest.BestRarity) : Look.Gold, 14, 380, 120, 600, 0.6f, 0.08f, "star1");
        Main.Instance?.Sfx?.Play("pop", 1f, -8);
        var land = Position + PileAt + new Vector2(0, -40);
        var up = node.CreateTween();
        up.TweenProperty(node, "position", from + new Vector2(30, -110), 0.28f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        up.TweenCallback(Callable.From(() => Fx.Fly(node, land, 0.55f, 160, () =>
        {
            _bump = 1;
            Main.Instance?.Sfx?.PlayGroup("chest_land", 1f, -10, 0.08f);
            Fx.Puff(_g.Fly, land + new Vector2(0, 30), new Color(0.6f, 0.55f, 0.5f, 0.5f), 5, 0.35f);
            Refresh();
        }, true, 6.28f)));
    }

    /// <summary>Auto-open burst a chest here: a pop, and its upgrades fly to their kind's tile.</summary>
    public void AutoOpened(Opening o)
    {
        var at = Position + PileAt + new Vector2(0, -60);
        int best = o.Cards.Count > 0 ? o.Cards.Max(c => c.Item.Rarity) : 0;
        Fx.Burst(_g.Fly, at, Look.Tier(best), 10, 300, 160, 500, 0.5f, 0.07f, "star2");
        int flown = 0;
        foreach (var card in o.Cards.Where(c => c.Equipped).OrderByDescending(c => c.Item.Rarity))
        {
            if (flown++ >= 4) break;
            var family = Describe.Family(_g.S.C.Slots[card.Slot].Name);
            int rarity = card.Item.Rarity, tier = ItemTile.Tier(card.Item);
            var icon = new Painter(p => ItemTile.Draw(p, new Rect2(-28, -28, 56, 56), family, rarity, numeral: false, tier: tier)) { Position = at };
            _g.Fly.AddChild(icon);
            var target = _g.Tiles.TileCentre(_g.S.C.Slots[card.Slot].Kind);
            Fx.Fly(icon, target, 0.6f + flown * 0.06f, 140, () => _g.Tiles.Ping(_g.S.C.Slots[card.Slot].Kind, rarity));
        }
        _g.Tiles.AddFinds(o);
        Refresh();
    }
}

/// <summary>
/// The armoury at a glance (bottom middle): one tile per slot kind (open slots out of all, the rarest worn, how the
/// rarities spread), and the latest finds below. A tile opens the armoury on its kind (docs/art-direction.md: an
/// overview by kind, then a grid by body region).
/// </summary>
public partial class KindTiles : Control
{
    readonly GameScreen _g;
    readonly Canvas _canvas;
    readonly List<(Card Card, double Time)> _finds = new();
    readonly float[] _ping = new float[8];
    readonly int[] _pingRarity = new int[8];
    float _t;
    const float X0 = 540, TileW = 206, TileH = 124, Gap = 10, Y0 = 52;

    public KindTiles(GameScreen g)
    {
        _g = g;
        this.Place(0, Look.LaneH, Look.W, Look.H - Look.LaneH);
        MouseFilter = MouseFilterEnum.Ignore;
        _canvas = new Canvas(DrawTiles).Place(0, 0, Look.W, Look.H - Look.LaneH);
        AddChild(_canvas);
        for (int k = 0; k < 8; k++)
        {
            var kind = (SlotKind)k;
            var b = new Button { Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = $"Open the armoury: {Describe.Kind(kind)}" }
                .Place(X0 + (k % 4) * (TileW + Gap), Y0 + (k / 4) * (TileH + Gap), TileW, TileH);
            b.Pressed += () => g.Open(new Armoury(g, kind));
            AddChild(b);
        }
    }

    public void Refresh() => _canvas.QueueRedraw();

    public Vector2 TileCentre(SlotKind k) => Position + new Vector2(X0 + ((int)k % 4) * (TileW + Gap) + TileW / 2, Y0 + ((int)k / 4) * (TileH + Gap) + TileH / 2);

    public void Ping(SlotKind k, int rarity)
    {
        _ping[(int)k] = 1;
        _pingRarity[(int)k] = rarity;
    }

    public void AddFinds(Opening o)
    {
        foreach (var c in o.Cards.Where(c => c.Equipped || c.New).OrderBy(c => c.Item.Rarity))
            _finds.Add((c, Time.GetTicksMsec() / 1000.0));
        while (_finds.Count > 10) _finds.RemoveAt(0);
        _canvas.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        for (int i = 0; i < 8; i++) _ping[i] = Mathf.MoveToward(_ping[i], 0, (float)delta * 1.5f);
        _canvas.QueueRedraw();
    }

    void DrawTiles(Canvas c)
    {
        var h = _g.S.H;
        c.DrawString(Look.UiBold, new Vector2(X0, 38), "ARMOURY", HorizontalAlignment.Left, -1, 20, Look.Muted);
        for (int k = 0; k < 8; k++)
        {
            var kind = (SlotKind)k;
            var r = new Rect2(X0 + (k % 4) * (TileW + Gap), Y0 + (k / 4) * (TileH + Gap), TileW, TileH);
            int open = 0, total = 0, best = -1;
            var counts = new int[Look.Tiers];
            string family = null;
            for (int s = 0; s < h.Gear.Length; s++)
            {
                if (_g.S.C.Slots[s].Kind != kind) continue;
                total++;
                if (!h.Open[s]) continue;
                open++;
                var it = h.Gear[s];
                if (it == null) continue;
                counts[it.Rarity]++;
                if (it.Rarity > best)
                {
                    best = it.Rarity;
                    family = Describe.Family(_g.S.C.Slots[s].Name);
                }
            }
            var edge = best >= 0 ? Look.Tier(best) : Look.Edge;
            var box = Ui.Frame(open > 0 ? Look.PanelLight : new Color("#151019"), open > 0 ? edge.Darkened(0.2f) : Look.Edge, 10, 2, 4, 0);
            c.DrawStyleBox(box, r);
            if (_ping[k] > 0)
            {
                var glow = Ui.Frame(Colors.Transparent, new Color(Look.Tier(_pingRarity[k]), _ping[k]), 12, 4, 0, 0);
                c.DrawStyleBox(glow, r.Grow(3));
            }
            if (open == 0)
            {
                c.DrawString(Look.UiBold, r.Position + new Vector2(14, 34), Describe.Kind(kind).ToUpperInvariant(), HorizontalAlignment.Left, -1, 19, Look.Dim);
                int first = FirstUnlock(kind);
                c.DrawString(Look.Ui, r.Position + new Vector2(14, 64), first > 0 ? $"opens at depth {Format.Whole(first)}" : "opened by transcending", HorizontalAlignment.Left, TileW - 20, 18, Look.Dim);
                continue;
            }
            ItemTile.Draw(c, new Rect2(r.Position + new Vector2(TileW - 84, 12), new Vector2(72, 72)), family, best, numeral: false, time: _t);
            c.DrawString(Look.UiBold, r.Position + new Vector2(14, 34), Describe.Kind(kind).ToUpperInvariant(), HorizontalAlignment.Left, TileW - 100, 19, Look.Text);
            c.DrawString(Look.Numbers, r.Position + new Vector2(14, 66), $"{open} / {total}", HorizontalAlignment.Left, -1, 24, Look.Text);
            c.DrawString(Look.Ui, r.Position + new Vector2(14, 88), "slots open", HorizontalAlignment.Left, -1, 16, Look.Muted);
            // The spread of rarities worn: a bar in tier colours.
            float x = r.Position.X + 14, wsum = TileW - 28;
            int worn = counts.Sum();
            if (worn > 0)
                for (int t = 0; t < counts.Length; t++)
                {
                    if (counts[t] == 0) continue;
                    float w = wsum * counts[t] / worn;
                    c.DrawRect(new Rect2(x, r.Position.Y + TileH - 22, w, 10), Look.Tier(t));
                    x += w;
                }
        }
        // The latest finds.
        c.DrawString(Look.UiBold, new Vector2(X0, Y0 + 2 * (TileH + Gap) + 20), "LATEST FINDS", HorizontalAlignment.Left, -1, 18, Look.Muted);
        double now = Time.GetTicksMsec() / 1000.0;
        for (int i = 0; i < _finds.Count; i++)
        {
            var (card, when) = _finds[_finds.Count - 1 - i];
            float age = (float)(now - when);
            var r = new Rect2(X0 + i * 86, Y0 + 2 * (TileH + Gap) + 30, 78, 78);
            if (r.End.X > X0 + 4 * (TileW + Gap)) break;
            ItemTile.Draw(c, r, Describe.Family(_g.S.C.Slots[card.Slot].Name), card.Item.Rarity, card.Item.Affixes, card.New && age < 30, card.Equipped && age < 30, numeral: true, time: _t, tier: ItemTile.Tier(card.Item));
        }
    }

    int FirstUnlock(SlotKind kind)
    {
        int best = int.MaxValue;
        foreach (var s in _g.S.C.Slots)
            if (s.Kind == kind && s.UnlockDepth > 0) best = Math.Min(best, s.UnlockDepth);
        return best == int.MaxValue ? 0 : best;
    }
}

/// <summary>
/// The dock (bottom right): the systems, each a button with a badge saying whether it has something for you (talent
/// points to spend, gold enough to forge, aether to collect). A system shows once it opens (Session.Unlocked), with a
/// one-time highlight.
/// </summary>
public partial class Dock : Control
{
    readonly GameScreen _g;
    public readonly Button Armoury, Talents, Forge, Ranks, Codex, Ascend, Settings;
    readonly Dictionary<Button, Label> _badges = new();
    readonly HashSet<Button> _seen = new();

    public Dock(GameScreen g)
    {
        _g = g;
        MouseFilter = MouseFilterEnum.Ignore;
        this.Place(1420, Look.LaneH, 500, Look.H - Look.LaneH);
        AddChild(Ui.Text("SYSTEMS", 20, Look.Muted, Look.UiBold).Place(8, 16, 200, 28));
        Button B(string text, int col, int row, Action press, Color accent)
        {
            var b = Ui.Button(text, press, accent, Look.Sub, Look.UiBold).Place(8 + col * 240, 52 + row * 92, 228, 80);
            AddChild(b);
            var badge = Ui.Text("", Look.Small, Look.Text, Look.Numbers, HorizontalAlignment.Center, 4).Place(8 + col * 240, 52 + row * 92 + 56, 228, 24);
            AddChild(badge);
            _badges[b] = badge;
            return b;
        }
        Armoury = B("Armoury", 0, 0, () => g.Open(new Armoury(g, SlotKind.Weapon)), new Color("#7a87a3"));
        Talents = B("Talents", 1, 0, () => g.Open(new TalentScreen(g)), new Color("#3fbf9a"));
        Forge = B("Forge all", 0, 1, () => { if (g.S.ForgeAll()) { g.Battle.Hero.Flash(Look.Gold, 0.5f); Main.Instance?.Sfx?.PlayGroup("equip_metal", 1f, -8); } }, Look.Gold);
        Ranks = B("Rank all", 1, 1, () => { if (g.S.RankAll()) { g.Battle.Hero.Flash(Look.Shards, 0.5f); Main.Instance?.Sfx?.PlayGroup("powerup", 1.2f, -10); } }, Look.Shards);
        Codex = B("Codex", 0, 2, () => g.Open(new CodexScreen(g)), new Color("#c48a22"));
        Ascend = B("Ascend", 1, 2, () => g.Open(new PrestigeScreen(g)), Look.Aether);
        Settings = Ui.Button("Settings", () => g.Open(new SettingsView(g)), size: Look.Body).Place(8, 330, 468, 50);
        AddChild(Settings);
    }

    public void Press(Button b)
    {
        if (b.Visible && !b.Disabled) b.EmitSignal(BaseButton.SignalName.Pressed);
    }

    public override void _Process(double delta)
    {
        var s = _g.S;
        Show(Talents, s.Unlocked(Feature.Talents), s.H.Talents.Points > 0 ? $"{s.H.Talents.Points} points" : "");
        Show(Forge, s.Unlocked(Feature.Forge), s.Unlocked(Feature.AutoForge) && s.Auto.Forge ? "automatic" : Affordable(s) ? "gold to spend" : "");
        Show(Ranks, s.Unlocked(Feature.Ranks), s.Unlocked(Feature.AutoRanks) && s.Auto.Ranks ? "automatic" : s.H.Materials >= MinRank(s) ? "shards to spend" : "");
        Show(Codex, s.Unlocked(Feature.Codex), $"{Format.Int(s.H.CodexCount)} found");
        Show(Ascend, s.Unlocked(Feature.Ascension), s.CanAscend ? $"+{Format.Num(s.AetherNow)} aether" : "");
        Show(Armoury, true, "");
    }

    void Show(Button b, bool open, string badge)
    {
        if (b.Visible != open)
        {
            b.Visible = open;
            _badges[b].Visible = open;
            if (open && _seen.Add(b) && _g.IsNodeReady()) Fx.Bump(b, 1.15f, 0.4f);
        }
        _badges[b].Text = badge;
    }

    static bool Affordable(Session s)
    {
        for (int i = 0; i < s.H.Gear.Length; i++)
            if (s.H.Open[i] && s.H.Gear[i] != null && s.H.ForgeCost(i) <= s.H.Gold) return true;
        return false;
    }

    static double MinRank(Session s)
    {
        double best = double.MaxValue;
        for (int i = 0; i < s.H.Gear.Length; i++)
            if (s.H.Open[i]) best = Math.Min(best, s.H.RankCost(i));
        return best;
    }
}
