using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The payoff, right after you buy. Hold to build tension (the glow can upgrade mid-hold), release into a burst, reveal
/// items one by one with the best last, each with the sound it made in the chest, then the verdict: what you paid,
/// what it was worth, and why.
/// </summary>
public partial class OpeningPhase : Phase
{
    enum Step { Hold, Burst, Reveal, Done }

    const float HoldSeconds = 1.2f;
    const int ChestPixel = 14;

    /// <summary>The chest's origin is its lid seam, where light and loot come out.</summary>
    static readonly Vector2 SeamPos = new(640, 500);

    Step _step = Step.Hold;
    List<RevealedItem> _items;
    readonly List<ItemDef> _learned = new();
    readonly List<string> _newRules = new();
    MentorView _mentor;
    Rarity _top;
    int _shownTier;
    ChestView _chest;
    Painter _rays;
    float _raysAlpha, _raysAngle;
    Label _prompt;
    CpuParticles2D _sparks, _coins;
    float _time;

    public float Progress { get; private set; }
    public MentorView Mentor => _mentor;
    public bool Finished => _step == Step.Done;

    public override void _Ready()
    {
        _items = Loot.Resolve(State.Chest, State.Grade);
        _top = _items.Max(i => i.Def.Rarity);

        Add(UiKit.MakeBackdrop("cellar_night", 0.45f));
        _rays = Add(new Painter(DrawRays) { Position = SeamPos, Material = Fx.Additive() });
        _chest = Add(new ChestView
        {
            Position = SeamPos,
            PixelSize = ChestPixel,
            Skin = State.Chest.Origin.Skin,
            LockColor = Palette.Lock(State.Chest.Lock.Index),
            GlowColor = Palette.Of(Rarity.Common),
        });

        var topColor = Palette.Of(_top);
        _sparks = Add(Fx.Sparks(topColor, 120, 250, 760, spread: 75f, lifetime: 1.4f));
        _sparks.Position = SeamPos;
        _coins = Add(Fx.Coins(36));
        _coins.Position = SeamPos;

        _prompt = Add(UiKit.MakeLabel("Hold [Space] or [Left Mouse] to open", 24, Palette.Text, HorizontalAlignment.Center, outline: 4)
            .Place(0, 636, 1280, 32));
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
        if (Progress > 0.72f && Progress - dt / HoldSeconds <= 0.72f) Game.Moment("07_build");

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
        _chest.ShowOpen();
        Fx.Explosion(this, SeamPos + new Vector2(0, -ChestPixel * 2), 3, color.Lightened(0.3f));
        var fling = lid.CreateTween().SetParallel();
        fling.TweenProperty(lid, "position", lid.Position + new Vector2(80, -280), 0.6f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        fling.TweenProperty(lid, "rotation", 0.9f, 0.6f);
        fling.TweenProperty(lid, "modulate:a", 0f, 0.45f).SetDelay(0.15f);

        _sparks.Emitting = true;
        if (_items.Any(i => i.Def.Family == Family.Coins)) _coins.Emitting = true;
        if (tier >= (int)Rarity.Rare)
            CreateTween().TweenMethod(Callable.From<float>(v => _raysAlpha = v), 0f, 0.3f + 0.12f * (tier - 2), 0.4f);
        Game.Moment("07_burst", 0.4);

        await Wait(0.7);
        if (!Alive) return;
        await RevealItems();
    }

    async Task RevealItems()
    {
        _step = Step.Reveal;
        int n = _items.Count;
        const float gap = 12;
        float totalWidth = n * ItemCard.W + (n - 1) * gap;
        float x0 = 640 - totalWidth / 2;

        for (int i = 0; i < n; i++)
        {
            var item = _items[i];
            bool last = i == n - 1;

            if (item.FromFalseBottom)
            {
                FloatText("...wait. A false bottom!", SeamPos + new Vector2(0, -40), Palette.Gold, 30);
                Sfx.Play("strain", 1.1f);
                _chest.Wobble(0.8f);
                await Wait(1.0);
                if (!Alive) return;
            }
            else if (last && item.Def.Rarity >= Rarity.Rare)
            {
                // A beat of anticipation before the best item.
                _chest.Wobble(0.7f);
                Sfx.Play("strain", 1.25f, -3);
                await Wait(0.5);
                if (!Alive) return;
            }

            // Seeing it is learning it, even in pieces: it goes in the ledger with its sound.
            if (!State.Ledger.Knows(item.Def.Family, item.Def.Variant) && item.Def.Tier > 1)
                _newRules.Add(Families.VariantRule(item.Def.Family, item.Def.Variant));
            bool isNew = State.Ledger.Learn(item.Def);
            if (isNew) _learned.Add(item.Def);
            var card = Add(new ItemCard(item, isNew));
            var target = new Vector2(x0 + i * (ItemCard.W + gap), 66);
            card.Position = SeamPos - new Vector2(ItemCard.W, ItemCard.H) / 2 - new Vector2(0, ChestPixel * 3);
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
                Sfx.Play("blip", pitch, -4);
                // The sound it made in the chest, so the two stick together.
                Sfx.Play(item.Def.Sound, 2f);
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

        Game.Moment("08_reveal", 0.2);
        await Wait(0.7);
        if (!Alive) return;
        ShowResults();
    }

    void ShowResults()
    {
        _step = Step.Done;
        var settle = CreateTween().SetParallel();
        settle.TweenProperty(_chest, "modulate:a", 0f, 0.4f);
        settle.TweenProperty(_chest, "position", SeamPos + new Vector2(0, 40), 0.4f);
        settle.TweenMethod(Callable.From<float>(v => _raysAlpha = v), _raysAlpha, _raysAlpha * 0.4f, 0.6f);

        var chest = State.Chest;
        var visit = State.Opening.Visit;
        int haul = _items.Sum(i => i.Value);
        int paid = State.PricePaid;
        int profit = haul - paid;

        var panel = Add(UiKit.MakePanel(Palette.PanelBorder).Place(180, 316, 620, 340));
        panel.PivotOffset = new Vector2(310, 170);
        panel.Scale = Vector2.One * 0.85f;
        panel.Modulate = Colors.Transparent;
        var pop = panel.CreateTween().SetParallel();
        pop.TweenProperty(panel, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        pop.TweenProperty(panel, "modulate", Colors.White, 0.2f);

        AddRow(panel, 18, $"Paid {visit.Seller.Name}", $"-{paid}g", Palette.Text);
        AddRow(panel, 52, "It was worth", $"+{haul}g", Palette.Gold);
        panel.AddChild(new ColorRect { Color = Palette.PanelBorder, MouseFilter = Control.MouseFilterEnum.Ignore }.Place(40, 90, 540, 3));
        panel.AddChild(UiKit.MakeLabel("Profit", 24, Palette.Text).Place(40, 106, 200, 32));
        var profitLabel = UiKit.MakeLabel("0g", 40, profit >= 0 ? Palette.Good : Palette.Bad, HorizontalAlignment.Right, outline: 4)
            .Place(280, 96, 300, 50);
        panel.AddChild(profitLabel);
        var count = profitLabel.CreateTween();
        count.TweenInterval(0.3f);
        count.TweenMethod(Callable.From<float>(v => profitLabel.Text = $"{(v >= 0 ? "+" : "")}{Mathf.RoundToInt(v)}g"), 0f, (float)profit, 0.8f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        count.TweenCallback(Callable.From(() =>
        {
            Sfx.Play(profit >= 0 ? "win" : "lose");
            if (profit > 0) Game.Shake(Mathf.Min(0.4f, 0.1f + profit / 1000f));
        }));

        // Why: the seller's blind spot, and what the lock did to the haul.
        var (verdict, verdictColor) = Insight(visit, haul, profit);
        panel.AddChild(UiKit.MakeLabel(verdict, 16, verdictColor, HorizontalAlignment.Center).Wrapped().Place(30, 156, 560, 64));
        string pickNote = State.Grade switch
        {
            PickGrade.Masterful => "Masterful pick: everything 25% more.",
            PickGrade.Forced when _items.Any(i => i.Shattered) => "Forced open: the glass shattered.",
            PickGrade.Forced => "Forced open: everything scratched (-10%).",
            _ => "",
        };
        if (chest.FalseBottom != null && State.Grade == PickGrade.Forced)
            pickNote += " Something rattled in the empty chest... a false bottom, wrecked.";
        else if (_learned.Count > 0)
            pickNote = (pickNote + " New in the ledger: " + string.Join(", ", _learned.Select(i => i.Name)) + ".").Trim();
        panel.AddChild(UiKit.MakeLabel(pickNote, 16, Palette.Muted, HorizontalAlignment.Center).Wrapped().Place(30, 222, 560, 44));

        bool last = State.Queue.Count == 0;
        panel.AddChild(UiKit.MakeButton(last ? "Close up for the day" : "Next customer", Palette.Gold, 24, Next).Place(160, 272, 300, 56));

        State.Opening.Haul = haul;
        State.Earn(haul);
        Game.Hud.SetGold(State.Gold);

        // Bram's notes on anything new: the first opening, and each new kind of sound you've now matched to a thing.
        _mentor = Add(new MentorView { Position = new Vector2(814, 576), ZIndex = 20 });
        if (State.FirstTime("opened")) _mentor.Say(Hints.Get("opened"));
        foreach (var rule in _newRules)
            if (State.Guided) _mentor.Say($"Remember that sound. {rule}");
        Game.Moment("08_results", 1.6);
    }

    /// <summary>A sentence on why this deal went the way it did, in terms of the seller's habit.</summary>
    (string, Color) Insight(Visit visit, int haul, int profit)
    {
        var seller = visit.Seller;
        var chest = visit.Chest;
        string name = seller.Name;
        string why = seller.Rule switch
        {
            PriceRule.ByWeight => $"{name} priced it by weight: {Describe.Pounds(chest.ContentsWeight)}.",
            PriceRule.ByCount => $"{name} priced {chest.Items.Count} things the same, whatever they were.",
            PriceRule.ByFamily => $"{name} priced it by the kind of sound, not which kind.",
            PriceRule.ByLock => $"{name} priced it by the lock: {chest.Lock.Name.ToLowerInvariant()}.",
            PriceRule.ByMagic => $"{name} priced the magic high and everything else as junk.",
            PriceRule.Fair => chest.FalseBottom != null ? $"{name} priced it fair, but didn't know about the false bottom." : $"{name} prices fair.",
            PriceRule.Braggart => $"{name} always starts at double.",
            PriceRule.Con => $"{name}. Of course.",
            PriceRule.Desperate => $"{name} needed the money.",
            PriceRule.Flat => $"{name} asks the same few coins for anything.",
            _ => $"{name} was guessing.",
        };
        if (chest.Tampered) return ($"{why} Stones and fakes: you were conned.", Palette.Bad);
        string result = profit > haul / 2 && profit > 0 ? "A steal."
            : profit > 0 ? "A good buy."
            : profit == 0 ? "Broke even."
            : "You paid more than it held.";
        return ($"{why} {result}", profit > 0 ? Palette.Good : profit == 0 ? Palette.Text : Palette.Warn);
    }

    static void AddRow(Control parent, float y, string label, string value, Color valueColor)
    {
        parent.AddChild(UiKit.MakeLabel(label, 16, Palette.Muted).Place(40, y + 4, 300, 24));
        parent.AddChild(UiKit.MakeLabel(value, 24, valueColor, HorizontalAlignment.Right).Place(280, y, 300, 30));
    }

    /// <summary>On to the next customer, or the end of the day.</summary>
    public void Next()
    {
        if (_step != Step.Done) return;
        Sfx.Play("bid");
        Game.NextCustomer();
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
