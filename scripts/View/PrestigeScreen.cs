using System;
using System.Linq;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The prestige layers (A): ascension (a new run for aether, which buys permanent power) and, once it opens,
/// transcendence (the ascension tree and its aether given up for stardust and a stronger next cycle). Each tab says what a
/// reset would pay and how much deeper it would take you, has the reset button and auto, and the layer's tree as cards
/// with their ranks, effects and costs.
/// </summary>
public partial class PrestigeScreen : Overlay
{
    readonly Panel _p;
    readonly Control _body;
    bool _trans;

    public PrestigeScreen(GameScreen g) : base(g)
    {
        _p = Frame("Ascension");
        var a = Ui.Button("Ascension", () => Show(false), Look.Aether, Look.Body).Place(32, 76, 220, 52);
        _p.AddChild(a);
        if (g.S.Unlocked(Feature.Transcendence))
            _p.AddChild(Ui.Button("Transcendence", () => Show(true), Look.Stardust, Look.Body).Place(264, 76, 240, 52));
        _body = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(0, 140, 1760, 820);
        _p.AddChild(_body);
        Show(false);
    }

    void Show(bool trans)
    {
        _trans = trans;
        foreach (var c in _body.GetChildren()) c.QueueFree();
        var s = G.S;
        var h = s.H;
        var layer = trans ? h.Transcendence : h.Ascension;
        var colour = trans ? Look.Stardust : Look.Aether;
        string currency = trans ? "stardust" : "aether";
        bool open = trans ? s.Unlocked(Feature.Transcendence) : s.Unlocked(Feature.Ascension);
        // What a reset pays now, and what it means.
        var gain = trans ? s.StardustNow : s.AetherNow;
        bool can = trans ? s.CanTranscend : s.CanAscend;
        string what = trans
            ? "Transcend: give up the ascension tree and its aether for stardust, which buys power that lasts through every cycle; the next cycle starts with an echo of this one's aether."
            : $"Ascend: start a new run from depth {Format.Whole(Math.Max(1, (int)(h.EverDepth * Math.Min(0.5, h.Ascension.PerkTotal(Perk.HeadStart) + h.Transcendence.PerkTotal(Perk.HeadStart)))))} with no gear and no gold, and keep the aether, which buys power for good.";
        _body.AddChild(Ui.Text(what, Look.Body, Look.Text, Look.Ui).Wrapped().Place(32, 0, 1000, 60));
        if (!open)
        {
            _body.AddChild(Ui.Text(trans ? "Transcendence opens once you've earned enough aether." : $"Ascension opens at depth {Format.Whole((long)s.P.AscendDepth)}.", Look.Sub, Look.Muted, Look.UiBold).Place(32, 90, 1000, 40));
            return;
        }
        _body.AddChild(Ui.Text($"{Format.Count(layer.Currency)} {currency}", Look.Header, colour, Look.Numbers).Place(1080, -4, 600, 46));
        _body.AddChild(Ui.Text($"{Format.Count(layer.Lifetime)} earned in all, {layer.Resets} resets", Look.Small, Look.Muted, Look.Ui).Place(1080, 42, 600, 26));
        string preview = "";
        if (!trans && gain > Big.Zero)
        {
            double deeper = s.AscendPreview();
            preview = deeper > 0 ? $", about {Format.Percent(deeper)} deeper next run" : "";
        }
        _body.AddChild(Ui.Text(gain > Big.Zero ? $"A reset now pays +{Format.Count(gain)} {currency}{preview}." : $"No {currency} earned yet this {(trans ? "cycle" : "run")}.",
            Look.Sub, gain > Big.Zero ? colour : Look.Muted, Look.UiBold).Place(32, 70, 1000, 40));
        var reset = Ui.Button(trans ? "Transcend now" : "Ascend now", () =>
        {
            bool done = trans ? G.S.Transcend() : G.S.Ascend();
            if (done) Close();
        }, colour, Look.Sub, Look.Strong).Place(1080, 76, 300, 64);
        reset.Disabled = !can;
        _body.AddChild(reset);
        _body.AddChild(Ui.Button("Spend the best way", () => { G.S.SpendLayer(layer); Show(_trans); }, colour, Look.Body).Place(1400, 76, 300, 64));
        if (!trans && s.Unlocked(Feature.Autopilot))
        {
            var auto = Ui.Button(s.Auto.Ascend ? "Auto-ascend: on" : "Auto-ascend: off", null, s.Auto.Ascend ? Look.Good : Look.EdgeLight, Look.Small).Place(1080, 150, 300, 44);
            auto.Pressed += () => { s.Auto.Ascend = !s.Auto.Ascend; Show(_trans); };
            _body.AddChild(auto);
        }
        // The tree as cards.
        var nodes = layer.Tree.Nodes;
        float cw = 330, ch = 120;
        int cols = 5;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.Place(32, 210, 1700, 600);
        var grid = new Control { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(1680, (nodes.Count + cols - 1) / cols * (ch + 12)) };
        scroll.AddChild(grid);
        _body.AddChild(scroll);
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            int rank = layer.Ranks[n.Id];
            float x = i % cols * (cw + 12), y = i / cols * (ch + 12);
            bool locked = h.EverDepth < n.RequiresDepth;
            bool full = n.MaxRank > 0 && rank >= n.MaxRank;
            var card = Ui.Box(rank > 0 ? new Color("#1d1630") : new Color("#140f1c"), rank > 0 ? colour.Darkened(0.2f) : Look.Edge, 10).Place(x, y, cw, ch);
            grid.AddChild(card);
            card.AddChild(Ui.Text(n.Name, Look.Body, rank > 0 ? Look.Text : Look.Muted, Look.UiBold).Place(14, 8, cw - 90, 30));
            card.AddChild(Ui.Text(n.MaxRank > 0 ? $"{rank}/{n.MaxRank}" : $"{rank}", Look.Body, colour, Look.Numbers, HorizontalAlignment.Right).Place(cw - 90, 8, 76, 30));
            card.AddChild(Ui.Text(Effect(n, Math.Max(1, rank)) + (rank == 0 ? " a rank" : ""), Look.Small, Look.Text, Look.Ui).Wrapped().Place(14, 38, cw - 28, 44));
            if (locked)
            {
                card.AddChild(Ui.Text($"after depth {Format.Whole(n.RequiresDepth)}", Look.Small, Look.Dim, Look.Ui).Place(14, 84, cw - 28, 26));
                continue;
            }
            if (full)
            {
                card.AddChild(Ui.Text("complete", Look.Small, Look.Good, Look.UiBold).Place(14, 84, cw - 28, 26));
                continue;
            }
            var cost = n.Cost(rank);
            var buy = Ui.Button($"+1  ({Format.Count(cost)})", () => { if (G.S.BuyNode(layer, n)) Show(_trans); }, colour, Look.Small).Place(cw - 170, 78, 156, 36);
            buy.Disabled = !layer.CanBuy(n, h.EverDepth);
            card.AddChild(buy);
        }
    }

    string Effect(LayerNode n, int ranks)
    {
        var sc = G.S.H.S;
        double a = n.Amount * ranks;
        return n.Perk switch
        {
            Perk.None => Describe.Line(n.Stat, a, sc),
            Perk.MasteryGain => $"{Format.Percent(a, true)} mastery",
            Perk.AetherGain => $"{Format.Percent(a, true)} aether",
            Perk.HeadStart => $"runs start at {Format.Percent(Math.Min(0.5, a))} of your deepest depth",
            Perk.ForgeDiscount => $"forging {Format.Percent(Math.Min(0.5, a))} cheaper",
            Perk.MaterialGain => $"{Format.Percent(a, true)} shards",
            Perk.CodexPower => $"codex entries count {Format.Percent(a)} more",
            Perk.SlotUnlock => $"opens {a:0} celestial slots",
            Perk.ExtraFocus => $"{a:0} more mastery focus slots",
            Perk.ForgeKeep => $"keep {Format.Percent(Math.Min(0.5, a))} of forge levels through an ascension",
            Perk.AetherEcho => $"the next cycle starts with {Format.Percent(a)} more of this one's aether",
            _ => n.Perk.ToString(),
        };
    }

    public override void _Ready() => Main.Instance?.Moment("prestige", 0.4);
}
