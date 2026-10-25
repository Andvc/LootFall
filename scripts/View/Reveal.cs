using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// Opening a chest (docs/art-direction.md, "The rarity ladder"): the chest climbs through the tiers' colours up to the
/// rarest thing inside, a ting a little higher at each, shaking harder; it bursts in the way its band calls for (a
/// colour and a sound for A; a beam for B; a flash, shake and rays for C; for D a held reveal, the card glowing face
/// down before it turns). The cards fan out above it, rarest last: what went on and what it replaced, first finds marked
/// NEW, the rest salvaged into shards. Space or a click hurries it; then upgrades fly to the armoury and the rest to the
/// shard counter. The outcome was settled before it began; the reveal only shows it.
/// </summary>
public partial class Reveal : Overlay
{
    const int MaxCards = 8;
    const float CardW = 176, CardH = 248;

    readonly List<Opening> _openings;
    readonly List<Card> _cards;
    readonly int _best;
    readonly Painter _chest;
    readonly Vector2 _chestAt = new(Look.W / 2, 590);
    readonly Control _cardLayer;
    readonly List<Control> _cardNodes = new();
    readonly Label _summary;
    float _t, _lid, _glow, _shake;
    int _shownTier = -1;
    bool _hurry, _done, _leaving;
    double _hold;
    Node2D _rays;

    public Reveal(GameScreen g, Opening one) : this(g, new List<Opening> { one }) { }

    public Reveal(GameScreen g, List<Opening> openings) : base(g, 0f)
    {
        _openings = openings;
        // Over the lane only: the bottom panel stays in view so the finds can fly to it.
        GetChild<ColorRect>(0).Place(0, 0, Look.W, Look.LaneH);
        GetChild<ColorRect>(0).Color = new Color(0.02f, 0.01f, 0.04f, 0.68f);
        var all = openings.SelectMany(o => o.Cards).ToList();
        _best = all.Count > 0 ? all.Max(c => c.Item.Rarity) : 0;
        // What's worth a card: upgrades and first finds first, then the rarest; shown rarest last.
        _cards = all.OrderByDescending(c => c.Equipped || c.New).ThenByDescending(c => c.Item.Rarity).ThenByDescending(c => c.Score)
            .Take(MaxCards).OrderBy(c => c.Item.Rarity).ThenBy(c => c.Equipped).ToList();
        _chest = new Painter(DrawChest);
        AddChild(_chest);
        _cardLayer = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 0, Look.W, Look.H);
        AddChild(_cardLayer);
        _summary = Ui.Text("", 24, Look.Text, Look.UiBold, HorizontalAlignment.Center, 6).Place(0, 396, Look.W, 40);
        _summary.Modulate = new Color(1, 1, 1, 0);
        AddChild(_summary);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready() => Run();

    public void Hurry()
    {
        if (_done) Leave();
        else _hurry = true;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true })
        {
            Hurry();
            AcceptEvent();
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _shake = Mathf.MoveToward(_shake, 0, (float)delta * 2);
        _chest.QueueRedraw();
        if (_done && !_leaving && (_hold -= delta) <= 0) Leave();
    }

    SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(_hurry ? Math.Min(seconds, 0.02) : seconds, false), SceneTreeTimer.SignalName.Timeout);

    async void Run()
    {
        var sfx = Main.Instance?.Sfx;
        sfx?.PlayGroup("creak", 1f, -6);
        _chest.Scale = Vector2.One * 0.3f;
        _chest.Position = Vector2.Zero;
        var pop = CreateTween();
        pop.TweenProperty(_chest, "scale", Vector2.One, 0.22f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await Wait(0.22);
        // The climb: one step per tier up to the best, slower and harder in the higher bands.
        int band = Look.Band(_best);
        for (int r = 0; r <= _best; r++)
        {
            if (!IsInstanceValid(this)) return;
            _shownTier = r;
            _glow = 0.4f + 0.6f * r / Math.Max(1, _best);
            _shake = 0.25f + 0.12f * Look.Band(r);
            sfx?.Play("ting", 0.85f + r * 0.09f, -10 + Look.Band(r) * 2);
            if (Look.Band(r) >= 2 && r == _best) sfx?.Play("rumble", 1f, -6);
            double step = Look.Band(r) switch { 0 => 0.09, 1 => 0.16, 2 => 0.22, _ => 0.32 };
            await Wait(step);
        }
        if (!IsInstanceValid(this)) return;
        // The burst.
        var colour = Look.Tier(_best);
        var lid = CreateTween();
        lid.TweenMethod(Callable.From<float>(v => _lid = v), 0f, 1f, 0.16f).SetEase(Tween.EaseType.Out);
        sfx?.Play("burst", 1f, -6);
        sfx?.PlayGroup("latch", 1f, -8);
        Fx.Burst(this, _chestAt + new Vector2(0, -80), colour, 30 + band * 20, 600 + band * 120, 160, 500, 0.9f, 0.13f, "star1");
        Fx.Ring(this, _chestAt + new Vector2(0, -60), colour, 280 + band * 60, 0.45f, 8);
        if (band >= 1) Fx.Beam(this, _chestAt + new Vector2(0, -40), colour, 110 + band * 30, 700, 2.2f);
        if (band >= 2)
        {
            Main.Instance?.Flash(colour, band >= 3 ? 0.3f : 0.2f, 0.4f);
            G.Battle.Shake(band >= 3 ? 0.45f : 0.3f);
            _rays = Fx.Rays(this, _chestAt + new Vector2(0, -120), colour, 420, 16, 0.22f, 14);
            _rays.ZIndex = 1;
        }
        sfx?.Play(band >= 2 ? "mythic" : band == 1 ? "fanfare" : "chime", 1f, band >= 1 ? -6 : -10);
        await Wait(0.18);
        // The cards.
        float spread = Math.Min(CardW + 16, (Look.W - 200) / Math.Max(1, _cards.Count));
        float x0 = Look.W / 2 - spread * (_cards.Count - 1) / 2;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (!IsInstanceValid(this)) return;
            var card = _cards[i];
            var node = MakeCard(card);
            var target = new Vector2(x0 + i * spread - CardW / 2, 112 + Math.Abs(i - (_cards.Count - 1) / 2f) * 8);
            node.Position = _chestAt + new Vector2(-CardW / 2, -CardH / 2 - 60);
            node.Scale = new Vector2(0.1f, 0.1f);
            node.PivotOffset = new Vector2(CardW / 2, CardH / 2);
            _cardLayer.AddChild(node);
            _cardNodes.Add(node);
            bool held = Look.Band(card.Item.Rarity) == 3 && card.Item.Rarity == _best;
            var t = node.CreateTween().SetParallel();
            t.TweenProperty(node, "position", target, 0.24f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t.TweenProperty(node, "scale", new Vector2(held ? 0.02f : 1f, 1f), 0.24f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            sfx?.PlayGroup("sell", 1.1f + 0.05f * i, -14, 0.02f);
            if (held)
            {
                // The held reveal: it glows face down in its colour, then turns.
                var glow = Fx.Glow(this, target + new Vector2(CardW / 2, CardH / 2), Look.Tier(card.Item.Rarity), 3.2f, 3);
                await Wait(0.7);
                if (!IsInstanceValid(this)) return;
                var flip = node.CreateTween();
                flip.TweenProperty(node, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                Main.Instance?.Flash(Look.Tier(card.Item.Rarity), 0.3f, 0.5f);
                sfx?.Play("mythic", 1.2f, -4);
                Fx.Burst(this, target + new Vector2(CardW / 2, CardH / 2), Look.Tier(card.Item.Rarity), 50, 700, 180, 400, 1f, 0.14f, "star2");
                glow.CreateTween().TweenProperty(glow, "modulate:a", 0f, 1.2f);
            }
            if (card.Equipped) Fx.Burst(this, target + new Vector2(CardW / 2, 40), Look.Good, 8, 200, 180, 300, 0.4f, 0.06f, "spark1");
            await Wait(Look.Band(card.Item.Rarity) >= 1 ? 0.12 : 0.07);
        }
        if (!IsInstanceValid(this)) return;
        // The summary.
        long items = _openings.Sum(o => o.Chest.ItemCount);
        int equipped = _openings.Sum(o => o.Equipped), first = _openings.Sum(o => o.NewEntries);
        double shards = _openings.Sum(o => o.Materials);
        double before = _openings[0].ReachBefore, after = _openings[^1].ReachAfter;
        var parts = new List<string> { _openings.Count > 1 ? $"{_openings.Count} chests, {Format.Int(items)} items" : $"{Format.Int(items)} items" };
        if (equipped > 0) parts.Add($"{equipped} equipped");
        if (first > 0) parts.Add($"{first} first finds");
        if (shards > 0) parts.Add($"+{Format.Num(shards)} shards");
        if (after >= before + 1) parts.Add($"can reach depth {Format.Whole((long)after)}");
        _summary.Text = string.Join("   |   ", parts);
        _summary.CreateTween().TweenProperty(_summary, "modulate:a", 1f, 0.2f);
        _done = true;
        _hold = _hurry ? 0.6 : 1.6 + 0.5 * band;
        Main.Instance?.Moment(_best >= 3 ? $"reveal_{Look.TierName(_best).ToLowerInvariant()}" : "reveal", 0.1);
    }

    Control MakeCard(Card card)
    {
        var it = card.Item;
        var slot = G.S.C.Slots[card.Slot];
        var family = Describe.Family(slot.Name);
        var name = Describe.Name(G.S.C, card.Slot, it);
        var c = new Canvas(p =>
        {
            var r = new Rect2(0, 0, CardW, CardH);
            var face = Ui.Frame(Look.TierFace(it.Rarity).Lerp(new Color("#120e18"), 0.25f), Look.Tier(it.Rarity), 12, 3, 10, 0);
            p.DrawStyleBox(face, r);
            ItemTile.Draw(p, new Rect2(CardW / 2 - 56, 18, 112, 112), family, it.Rarity, it.Affixes, card.New, card.Equipped, time: _t, tier: ItemTile.Tier(it));
        }).Place(0, 0, CardW, CardH);
        // A light face (Eternal's silver) takes dark ink.
        bool light = Look.TierFace(it.Rarity).Luminance > 0.55f;
        var ink = light ? Look.Ink : Look.Text;
        c.AddChild(Ui.Text(Look.TierName(it.Rarity).ToUpperInvariant(), 15, light ? Look.Tier(it.Rarity).Darkened(0.55f) : Look.TierText(it.Rarity), Look.Strong, HorizontalAlignment.Center).Place(0, 132, CardW, 20));
        // A long name steps down a size or two to keep to two lines; one that still needs three takes the slot's line
        // (the tooltip names the slot).
        int size = 18;
        float Height(int at) => Look.UiBold.GetMultilineStringSize(name, HorizontalAlignment.Center, CardW - 16, at).Y;
        while (size > 15 && Height(size) > 48) size--;
        bool tall = Height(size) > 48;
        c.AddChild(Ui.Text(name, size, ink, Look.UiBold, HorizontalAlignment.Center).Wrapped().Place(8, 152, CardW - 16, tall ? 68 : 48));
        if (!tall)
            c.AddChild(Ui.Text(slot.Name, 15, light ? new Color(Look.Ink, 0.7f) : Look.Muted, Look.Ui, HorizontalAlignment.Center).Place(0, 198, CardW, 20));
        string verdict = card.Equipped ? (card.Replaced == null ? "EQUIPPED" : $"EQUIPPED  > {Look.TierName(card.Replaced.Rarity)}") : "salvaged";
        c.AddChild(Ui.Text(verdict, 16, card.Equipped ? (light ? new Color("#1f7a3a") : Look.Good) : Look.Dim, Look.Strong, HorizontalAlignment.Center).Place(0, 220, CardW, 22));
        c.TooltipText = string.Join("\n", Describe.Lines(G.S.H, card.Slot, it));
        c.MouseFilter = MouseFilterEnum.Pass;
        return c;
    }

    void DrawChest(Painter p)
    {
        var shake = new Vector2(Mathf.Sin(_t * 61) * 6, Mathf.Cos(_t * 47) * 3) * _shake;
        var colour = _shownTier >= 0 ? Look.Tier(_shownTier) : Look.Blow;
        if (_glow > 0)
            for (int i = 4; i >= 1; i--)
                p.DrawCircle(_chestAt + new Vector2(0, -70), 70 + i * 34 * _glow * (0.9f + 0.1f * Mathf.Sin(_t * 8)), new Color(colour, 0.07f * _glow), true, -1, true);
        var kind = _openings.Count == 1 ? _openings[0].Chest.Kind : _openings.Max(o => o.Chest.Kind);
        ChestArt.Draw(p, _chestAt + shake, 240, kind == ChestKind.Starter ? ChestKind.Boss : kind, _shownTier, _lid, _glow, _t);
    }

    void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        var shardTarget = new Vector2(372, 72);
        for (int i = 0; i < _cardNodes.Count; i++)
        {
            var node = _cardNodes[i];
            var card = _cards[i];
            var t = node.CreateTween().SetParallel();
            if (card.Equipped)
            {
                var to = G.Tiles.TileCentre(G.S.C.Slots[card.Slot].Kind) - new Vector2(CardW / 2, CardH / 2);
                t.TweenProperty(node, "position", to, 0.45f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In).SetDelay(i * 0.04);
                t.TweenProperty(node, "scale", new Vector2(0.25f, 0.25f), 0.45f).SetDelay(i * 0.04);
                int rarity = card.Item.Rarity;
                var kind = G.S.C.Slots[card.Slot].Kind;
                t.Chain().TweenCallback(Callable.From(() => G.Tiles.Ping(kind, rarity)));
            }
            else
            {
                t.TweenProperty(node, "scale", new Vector2(0.05f, 0.05f), 0.3f).SetDelay(i * 0.03);
                t.TweenProperty(node, "modulate:a", 0f, 0.3f).SetDelay(i * 0.03);
                var from = node.Position + new Vector2(CardW / 2, CardH / 2);
                GetTree().CreateTimer(0.1 + i * 0.03, false).Timeout += () =>
                {
                    if (!IsInstanceValid(G)) return;
                    var shard = new Painter(q => Glyphs.Shard(q, Vector2.Zero, 10)) { Position = from };
                    G.Fly.AddChild(shard);
                    Fx.Fly(shard, shardTarget, 0.5f, 120);
                };
            }
        }
        foreach (var o in _openings) G.Tiles.AddFinds(o);
        if (_rays != null) _rays.CreateTween().TweenProperty(_rays, "modulate:a", 0f, 0.3f);
        var fade = CreateTween();
        fade.TweenInterval(0.5);
        fade.TweenProperty(this, "modulate:a", 0f, 0.2f);
        fade.TweenCallback(Callable.From(() =>
        {
            G.AfterReveal();
            QueueFree();
        }));
        if (_openings.Any(o => o.Equipped > 0)) Main.Instance?.Sfx?.PlayGroup("equip_metal", 1f, -8);
    }

    public override void Close() => Hurry();
}
