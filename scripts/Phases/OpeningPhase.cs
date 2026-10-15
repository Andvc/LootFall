using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The payoff. Hold to build tension (the glow can upgrade mid-hold), release into a burst,
/// reveal items one by one with the best last, then settle into the profit summary.
/// </summary>
public partial class OpeningPhase : Phase
{
    enum Step { Hold, Burst, Reveal, Done }

    const float HoldSeconds = 1.6f;
    const float ChestScale = 1.3f;
    static readonly Vector2 ChestPos = new(640, 480);
    static Vector2 SeamPos => ChestPos + new Vector2(0, ChestView.SeamY * ChestScale);

    Step _step = Step.Hold;
    List<RevealedItem> _items;
    Rarity _top;
    int _shownTier;
    ChestView _chest;
    Painter _rays;
    float _raysAlpha, _raysAngle;
    Label _prompt;
    CpuParticles2D _sparks, _coins;
    float _time;

    public float Progress { get; private set; }
    public bool Finished => _step == Step.Done;

    public override void _Ready()
    {
        _items = Loot.Resolve(State.Chest, State.Grade);
        _top = _items.Max(i => i.Def.Rarity);

        _rays = Add(new Painter(DrawRays) { Position = SeamPos, Material = Fx.Additive() });
        _chest = Add(new ChestView
        {
            Position = ChestPos,
            Scale = Vector2.One * ChestScale,
            LockColor = Palette.Lock(State.Chest.Lock.Index),
            GlowColor = Palette.Of(Rarity.Common),
        });

        var topColor = Palette.Of(_top);
        _sparks = Add(Fx.Sparks(topColor, 120, 250, 760, spread: 75f, lifetime: 1.4f));
        _sparks.Position = SeamPos;
        _coins = Add(Fx.Coins(36));
        _coins.Position = SeamPos;

        _prompt = Add(UiKit.MakeLabel("Hold [Space] or [Left Mouse] to open", 22, Palette.Text, HorizontalAlignment.Center, outline: 4)
            .Place(0, 640, 1280, 32));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _raysAngle += dt * 0.25f;
        _rays.QueueRedraw();

        if (_step != Step.Hold) return;

        bool holding = Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left) || Game.AutoHold;
        Progress = holding
            ? Mathf.Min(1f, Progress + dt / HoldSeconds)
            : Mathf.Max(0f, Progress - dt / 0.9f);

        _chest.Rumble = Progress;
        _chest.LidGap = 12f * Progress * Progress;
        _chest.Glow = Progress;
        Sfx.Hum(Progress);
        _prompt.Modulate = Colors.White with { A = 0.55f + 0.45f * MathF.Sin(_time * 5f) };

        int tier = TierAt(Progress);
        if (tier > _shownTier) Upgrade(tier);
        if (Progress > 0.72f && Progress - dt / HoldSeconds <= 0.72f) Game.Moment("04_build");

        if (Progress >= 1f) Burst();
    }

    /// <summary>The glow climbs through each rarity up to the best item inside, spread across the hold.</summary>
    int TierAt(float progress)
    {
        int top = (int)_top;
        for (int k = top; k >= 1; k--)
            if (progress >= 0.3f + 0.55f * k / top) return k;
        return 0;
    }

    void Upgrade(int tier)
    {
        _shownTier = tier;
        var color = Palette.Of((Rarity)tier);
        _chest.GlowColor = color;
        _chest.Wobble(0.5f);
        Sfx.Play("sting", 1f + 0.12f * tier, -4);
        Game.Shake(0.12f + 0.05f * tier);
        Game.Flash(color, 0.18f);
    }

    async void Burst()
    {
        _step = Step.Burst;
        Sfx.Hum(0);
        _prompt.Hide();

        int tier = (int)_top;
        var color = Palette.Of(_top);
        Sfx.Play("burst", 1f, -4);
        Sfx.Play("whoosh", 1f, -6);
        if (tier >= (int)Rarity.Epic) Sfx.Play("fanfare", 1f, -4);
        else if (tier >= (int)Rarity.Rare) Sfx.Play("reveal_big", 1f, -3);

        Game.HitStop(0.06 + 0.025 * tier);
        Game.Shake(0.35f + 0.13f * tier);
        Game.Flash(color.Lightened(0.5f), 0.85f, 0.45f);

        _chest.Rumble = 0;
        _chest.LidGap = 0;
        _chest.Glow = 1f;
        _chest.GlowColor = color;
        var lid = _chest.DetachLid();
        var fling = lid.CreateTween().SetParallel();
        fling.TweenProperty(lid, "position", lid.Position + new Vector2(80, -280), 0.6f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        fling.TweenProperty(lid, "rotation", 0.9f, 0.6f);
        fling.TweenProperty(lid, "modulate:a", 0f, 0.45f).SetDelay(0.15f);

        _sparks.Emitting = true;
        if (_items.Any(i => i.Def.Sound == Sound.Jingle)) _coins.Emitting = true;
        if (tier >= (int)Rarity.Rare)
            CreateTween().TweenMethod(Callable.From<float>(v => _raysAlpha = v), 0f, 0.3f + 0.12f * (tier - 2), 0.4f);
        Game.Moment("05_burst", 0.4);

        await Wait(0.7);
        if (!Alive) return;
        await RevealItems();
    }

    async Task RevealItems()
    {
        _step = Step.Reveal;
        int n = _items.Count;
        float totalWidth = n * ItemCard.W + (n - 1) * 14;
        float x0 = 640 - totalWidth / 2;

        for (int i = 0; i < n; i++)
        {
            var item = _items[i];
            bool last = i == n - 1;

            if (item.FromFalseBottom)
            {
                FloatText("...wait. A false bottom!", SeamPos + new Vector2(0, -40), Palette.Gold, 30);
                Sfx.Play("creak", 1.1f);
                _chest.Wobble(0.8f);
                await Wait(1.0);
                if (!Alive) return;
            }
            else if (last && item.Def.Rarity >= Rarity.Rare)
            {
                // A beat of anticipation before the best item.
                _chest.Wobble(0.7f);
                Sfx.Play("creak", 1.25f, -3);
                await Wait(0.5);
                if (!Alive) return;
            }

            var card = Add(new ItemCard(item));
            var target = new Vector2(x0 + i * (ItemCard.W + 14), 66);
            card.Position = SeamPos - new Vector2(ItemCard.W, ItemCard.H) / 2;
            card.Scale = Vector2.One * 0.2f;
            card.Modulate = Colors.Transparent;
            var rise = card.CreateTween().SetParallel();
            rise.TweenProperty(card, "position", target, 0.45f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            rise.TweenProperty(card, "scale", Vector2.One, 0.45f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            rise.TweenProperty(card, "modulate", Colors.White, 0.15f);
            card.CountUp(0.6f);

            float pitch = MathF.Pow(1.09f, i);
            if (item.Shattered)
            {
                Sfx.Play("shatter");
                Game.Shake(0.15f);
            }
            else
            {
                Sfx.Play("blip", pitch);
                if (item.Def.Sound == Sound.Jingle) Sfx.Play("coin", pitch, -4);
                if (item.Def.Rarity >= Rarity.Rare)
                {
                    var center = target + new Vector2(ItemCard.W, ItemCard.H) / 2;
                    Fx.Burst(this, center, Palette.Of(item.Def.Rarity), 30 + 15 * (int)item.Def.Rarity);
                    Game.Shake(0.08f + 0.06f * (int)item.Def.Rarity);
                    if (item.Def.Rarity >= Rarity.Epic) Sfx.Play("sting", 1.3f, -2);
                }
            }

            await Wait(last ? 0.6 : 0.34);
            if (!Alive) return;
        }

        Game.Moment("06_reveal", 0.2);
        await Wait(0.7);
        if (!Alive) return;
        ShowResults();
    }

    void ShowResults()
    {
        _step = Step.Done;
        var settle = CreateTween().SetParallel();
        settle.TweenProperty(_chest, "modulate:a", 0f, 0.4f);
        settle.TweenProperty(_chest, "position", ChestPos + new Vector2(0, 40), 0.4f);
        settle.TweenMethod(Callable.From<float>(v => _raysAlpha = v), _raysAlpha, _raysAlpha * 0.4f, 0.6f);

        var chest = State.Chest;
        int haul = _items.Sum(i => i.Value);
        int paid = State.PricePaid;
        int profit = haul - paid;

        var panel = Add(UiKit.MakePanel(Palette.Panel, Palette.PanelBorder, 3).Place(330, 252, 620, 400));
        panel.PivotOffset = new Vector2(310, 200);
        panel.Scale = Vector2.One * 0.85f;
        panel.Modulate = Colors.Transparent;
        var pop = panel.CreateTween().SetParallel();
        pop.TweenProperty(panel, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        pop.TweenProperty(panel, "modulate", Colors.White, 0.2f);

        int shattered = _items.Count(i => i.Shattered);
        var (title, subtitle, titleColor) = State.Grade switch
        {
            PickGrade.Masterful => ("Masterful work!", "Pristine condition: every item is worth 25% more.", Palette.Gold),
            PickGrade.Clean => ("A clean job.", "Everything came out intact.", Palette.Good),
            _ => ("Brute force has its costs.", shattered > 0
                ? $"{shattered} fragile item{(shattered == 1 ? "" : "s")} shattered. Everything else is scratched (-10%)."
                : "Nothing fragile, luckily. Scratches cost 10%.", Palette.Bad),
        };
        panel.AddChild(UiKit.MakeLabel(title, 28, titleColor, HorizontalAlignment.Center).Place(0, 16, 620, 36));
        panel.AddChild(UiKit.MakeLabel(subtitle, 15, Palette.Muted, HorizontalAlignment.Center).Place(0, 54, 620, 22));

        AddRow(panel, 96, "Paid at auction", $"-{paid}g", Palette.Text);
        AddRow(panel, 130, "Haul value", $"+{haul}g", Palette.Gold);
        panel.AddChild(new Painter(p => p.DrawLine(new Vector2(60, 0), new Vector2(560, 0), Palette.PanelBorder, 2)) { Position = new Vector2(0, 170) });
        panel.AddChild(UiKit.MakeLabel("Profit", 24, Palette.Text).Place(60, 184, 200, 40));
        var profitLabel = UiKit.MakeLabel("0g", 38, profit >= 0 ? Palette.Good : Palette.Bad, HorizontalAlignment.Right).Place(300, 176, 260, 50);
        panel.AddChild(profitLabel);
        var count = profitLabel.CreateTween();
        count.TweenInterval(0.3f);
        count.TweenMethod(Callable.From<float>(v => profitLabel.Text = $"{(v >= 0 ? "+" : "")}{Mathf.RoundToInt(v)}g"), 0f, (float)profit, 0.9f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        count.TweenCallback(Callable.From(() =>
        {
            Sfx.Play(profit >= 0 ? "win" : "lose");
            if (profit > 0) Game.Shake(Mathf.Min(0.4f, 0.1f + profit / 2000f));
        }));

        var est = State.Estimate;
        var (verdict, verdictColor) = chest.Value < est.Low
            ? ($"Worth {chest.Value}g before the pick, under your {est.Low}-{est.High}g estimate. Overpriced hope.", Palette.Warn)
            : chest.Value > est.High
                ? ($"Worth {chest.Value}g before the pick, above your {est.Low}-{est.High}g estimate. Lucky find!", Palette.Good)
                : ($"Worth {chest.Value}g before the pick, inside your {est.Low}-{est.High}g estimate. Sharp eye.", Palette.Text);
        panel.AddChild(UiKit.MakeLabel(verdict, 15, verdictColor, HorizontalAlignment.Center).Wrapped().Place(30, 238, 560, 44));

        if (chest.FalseBottom != null && State.Grade == PickGrade.Forced)
            panel.AddChild(UiKit.MakeLabel("As you toss the empty chest aside, something rattles inside it...", 14, Palette.Muted,
                HorizontalAlignment.Center).Place(20, 286, 580, 20));

        panel.AddChild(UiKit.MakeButton("Next lot", Palette.Gold, 20, NextLot).Place(210, 324, 200, 52));

        State.Gold += haul;
        Game.Hud.SetGold(State.Gold);
        Game.Moment("07_results", 1.6);
    }

    static void AddRow(Control parent, float y, string label, string value, Color valueColor)
    {
        parent.AddChild(UiKit.MakeLabel(label, 18, Palette.Muted).Place(60, y, 300, 26));
        parent.AddChild(UiKit.MakeLabel(value, 22, valueColor, HorizontalAlignment.Right).Place(300, y - 2, 260, 30));
    }

    public void NextLot()
    {
        if (_step != Step.Done) return;
        Sfx.Play("bid");
        Game.SwitchTo(new AuctionPhase());
    }

    void DrawRays(Painter p)
    {
        if (_raysAlpha <= 0.01f) return;
        var c = Palette.Of(_top);
        const int count = 14;
        for (int i = 0; i < count; i++)
        {
            float a = _raysAngle + i * Mathf.Tau / count;
            var points = new[] { Vector2.Zero, Vector2.FromAngle(a - 0.08f) * 720, Vector2.FromAngle(a + 0.08f) * 720 };
            var colors = new[] { c with { A = _raysAlpha }, c with { A = 0f }, c with { A = 0f } };
            p.DrawPolygon(points, colors);
        }
        for (int i = 4; i >= 1; i--) p.DrawCircle(Vector2.Zero, 30 + i * 26, c with { A = _raysAlpha * 0.18f });
    }
}
