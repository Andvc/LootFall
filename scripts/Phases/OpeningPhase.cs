using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The payoff, right after you buy. Hold to wrench the lid: the chest rumbles, glows toward the best thing inside and
/// the camera leans in. Then it bursts: the lid flies off and everything comes flying out, landing on the table one by
/// one with its sound, its true quality and price, and a tick or a cross against your slip; a false bottom gives up its
/// secret last. Then the verdict slams down (profit or loss), the coins fly into your purse, and a perfect read gets
/// its banner. Anything first found goes in the collection, and anything grand can be kept for display.
/// </summary>
public partial class OpeningPhase : Phase
{
    enum Step { Hold, Burst, Reveal, Done }

    const float HoldSeconds = 1.1f;
    const int ChestPixel = 14;
    const float TableY = 476, LandY = 564;

    /// <summary>The chest's origin is its lid seam, where light and loot come out.</summary>
    static readonly Vector2 SeamPos = new(640, 400);

    Step _step = Step.Hold;
    Purchase _purchase;
    ReadReport _report;
    MentorView _mentor;
    Rarity _top;
    int _shownTier, _worth;
    ChestView _chest;
    Painter _rays;
    float _raysAlpha, _raysAngle, _time;
    Label _prompt, _worthLabel;
    CpuParticles2D _sparks;
    readonly List<(Item Item, Vector2 At, int Value)> _landed = new();

    public float Progress { get; private set; }
    public MentorView Mentor => _mentor;
    public bool Finished => _step == Step.Done;

    public override void _Ready()
    {
        _purchase = State.Opening;
        _report = _purchase.Slip.Check(_purchase.Chest);
        _top = _purchase.Chest.Everything.Max(i => i.Rarity);

        Add(UiKit.MakeBackdrop("cellar_night", 0.4f));
        _rays = Add(new Painter(DrawRays) { Position = SeamPos, Material = Fx.Additive() });
        Add(new Painter(DrawTable));
        _chest = Add(new ChestView { Position = SeamPos, PixelSize = ChestPixel, Skin = _purchase.Chest.Skin, GlowColor = Palette.Of(Rarity.Common) });
        _sparks = Add(Fx.Sparks(Palette.Of(_top), 140, 280, 820, spread: 70f, lifetime: 1.4f));
        _sparks.Position = SeamPos;

        // The tally stays put while the camera leans in.
        var tally = Add(new CanvasLayer { Layer = 1 });
        tally.AddChild(UiKit.MakeLabel($"Paid {_purchase.Visit.Seller.Short}", 16, Palette.Muted).Place(40, 80, 200, 20));
        tally.AddChild(UiKit.MakeLabel($"{_purchase.Paid}g", 24, Palette.Text, HorizontalAlignment.Right, outline: 4).Place(200, 74, 120, 30));
        tally.AddChild(UiKit.MakeLabel("Worth", 16, Palette.Muted).Place(40, 114, 200, 20));
        _worthLabel = UiKit.MakeLabel("0g", 24, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(200, 108, 120, 30);
        tally.AddChild(_worthLabel);

        _prompt = Add(UiKit.MakeLabel("Hold [Space] or the mouse to wrench it open", 24, Palette.Text, HorizontalAlignment.Center, outline: 4)
            .Place(0, 560, 1280, 32));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _raysAngle += dt * 0.25f;
        _rays.QueueRedraw();
        if (_step != Step.Hold) return;

        bool holding = Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left) || Game.AutoHold;
        Progress = holding ? Mathf.Min(1f, Progress + dt / HoldSeconds) : Mathf.Max(0f, Progress - dt / 0.8f);

        // Everything builds together: rumble, a widening crack of light, the hum, and the camera leaning in.
        _chest.Rumble = Progress;
        _chest.LidGap = 14f * Progress * Progress;
        _chest.Glow = Progress;
        Sfx.Hum(Progress);
        Game.Zoom(1f + 0.14f * Progress * Progress, SeamPos);
        if (holding && Progress > 0.2f) Game.Shake(dt * 0.5f * Progress);
        _prompt.Modulate = Colors.White with { A = 0.55f + 0.45f * MathF.Sin(_time * 5f) };

        int tier = TierAt(Progress);
        if (tier > _shownTier) Upgrade(tier);
        if (Progress > 0.75f && Progress - dt / HoldSeconds <= 0.75f) Game.Moment("07_build");
        if (Progress >= 1f) Burst();
    }

    /// <summary>The glow climbs through each rarity up to the best thing inside, spread across the hold.</summary>
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
        Game.Shake(0.1f + 0.05f * tier);
        Game.Flash(color, 0.18f);
    }

    async void Burst()
    {
        _step = Step.Burst;
        Sfx.Hum(0);
        _prompt.Hide();

        int tier = (int)_top;
        var color = Palette.Of(_top);
        Sfx.Play("burst", 1f, -2);
        Sfx.Play("whoosh", 1f, -4);
        Sfx.PlayGroup("land", 0.6f, 2);
        if (tier >= (int)Rarity.Epic) Sfx.Play("fanfare", 1f, -4);
        else if (tier >= (int)Rarity.Rare) Sfx.Play("reveal_big", 1f, -3);

        Game.HitStop(0.09 + 0.03 * tier);
        Game.Shake(0.5f + 0.1f * tier);
        Game.Flash(color.Lightened(0.6f), 0.9f, 0.5f);
        // The camera snaps back past where it started, then settles.
        CreateTween().TweenMethod(Callable.From<float>(z => Game.Zoom(z, SeamPos)), 1.14f, 1f, 0.45f)
            .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

        _chest.Rumble = 0;
        _chest.LidGap = 0;
        _chest.Glow = 1f;
        _chest.GlowColor = color;
        FlingLid(_chest.DetachLid());
        _chest.ShowOpen();
        Fx.Explosion(this, SeamPos + new Vector2(0, -ChestPixel * 2), 4, color.Lightened(0.3f));
        Fx.Dust(this, SeamPos + new Vector2(-90, 70), 1.5f);
        Fx.Dust(this, SeamPos + new Vector2(90, 70), 1.5f);
        _sparks.Emitting = true;
        CreateTween().TweenMethod(Callable.From<float>(v => _raysAlpha = v), 0f, 0.18f + 0.1f * tier, 0.4f);
        Game.Moment("07_burst", 0.3);

        await Wait(0.45);
        if (!Alive) return;
        await FlyOut();
    }

    /// <summary>The lid spins off in an arc and out of sight.</summary>
    void FlingLid(Node2D lid)
    {
        var start = lid.Position;
        float side = _time % 2 < 1 ? 1 : -1;
        var tween = lid.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(t => lid.Position = start + new Vector2(side * 520 * t, -420 * t + 900 * t * t)), 0f, 1f, 0.9f);
        tween.TweenProperty(lid, "rotation", side * 7f, 0.9f);
    }

    async Task FlyOut()
    {
        _step = Step.Reveal;
        // Worst first, best last, so the reveal climbs; whatever was under a false bottom comes out after everything else.
        var reads = _report.Items.OrderBy(r => r.Hidden).ThenBy(r => r.Item.Value).ToList();
        int n = reads.Count;
        float spacing = n >= 5 ? 196 : 232;
        for (int i = 0; i < n; i++)
        {
            bool last = i == n - 1;
            if (reads[i].Hidden)
            {
                _chest.Wobble(1f);
                Sfx.Play("hollow", 1.2f, -2);
                Sfx.Play("strain", 1.1f, -2);
                FloatText("...wait. A false bottom!", SeamPos + new Vector2(0, -150), Palette.Gold, 32);
                await Wait(0.9);
                if (!Alive) return;
            }
            else if (last && n > 1 && reads[i].Item.Rarity >= Rarity.Rare)
            {
                // A beat before the best thing.
                _chest.Wobble(0.8f);
                Sfx.Play("strain", 1.25f, -3);
                await Wait(0.45);
                if (!Alive) return;
            }
            Launch(reads[i], 640 + (i - (n - 1) / 2f) * spacing, i);
            await Wait(last ? 0.95 : 0.38);
            if (!Alive) return;
        }
        var ghosts = _report.Ghosts.Select(c => $"no {Categories.Get(c).Name.ToLowerInvariant()}?").ToList();
        if (_report.GhostHidden) ghosts.Add("no false bottom?");
        for (int g = 0; g < ghosts.Count; g++)
        {
            var ghost = UiKit.MakeLabel(ghosts[g], 16, Palette.Bad, HorizontalAlignment.Center, outline: 4)
                .Place(960, 380 + g * 26, 280, 20);
            Add(ghost);
            Add(UiKit.MakeCheck(false, new Vector2(1000, 390 + g * 26), 1));
            Sfx.Play("miss", 1f, -6);
            await Wait(0.3);
            if (!Alive) return;
        }
        Game.Moment("08_reveal", 0.1);
        await Wait(0.3);
        if (!Alive) return;
        Verdict();
    }

    /// <summary>One thing flies out of the chest in an arc and lands on the table, then gets its price and its mark.</summary>
    void Launch(ItemRead read, float x, int index)
    {
        var item = read.Item;
        var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{item.Sprite}.png");
        int scale = Math.Max(1, (int)MathF.Min(64f / texture.GetHeight(), 72f / texture.GetWidth()));
        // Standing on the table, however tall it is.
        var land = new Vector2(x, LandY - texture.GetHeight() * scale / 2f);
        var sprite = Add(new Sprite2D { Texture = texture, Scale = Vector2.One * scale, ZIndex = 5 });
        if (item.Fake) sprite.Modulate = new Color(0.75f, 0.75f, 0.8f);
        var start = SeamPos + new Vector2(0, -ChestPixel * 3);
        float height = 240 + 40 * index;
        float spin = (index % 2 == 0 ? 1 : -1) * Mathf.Tau;
        Sfx.Play("whoosh", 1.4f + 0.1f * index, -12);

        var flight = sprite.CreateTween();
        flight.TweenMethod(Callable.From<float>(t =>
        {
            sprite.Position = start.Lerp(land, t) + new Vector2(0, -height * 4 * t * (1 - t));
            sprite.Rotation = spin * (1 - t);
        }), 0f, 1f, 0.55f);
        flight.TweenCallback(Callable.From(() => Landed(read, sprite, scale, land)));
    }

    void Landed(ItemRead read, Sprite2D sprite, int scale, Vector2 at)
    {
        if (!Alive) return;
        var item = read.Item;
        int rarity = (int)item.Rarity;

        // Squash on impact, then spring back.
        sprite.Scale = new Vector2(scale * 1.35f, scale * 0.65f);
        sprite.CreateTween().TweenProperty(sprite, "scale", Vector2.One * scale, 0.35f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        Fx.Dust(this, new Vector2(at.X, LandY), 0.8f + 0.2f * rarity);
        Sfx.PlayGroup(item.Def.Sound, item.Def.Pitch, 4);
        Sfx.PlayGroup("land", 1.2f - 0.1f * rarity, -4);
        Game.Shake(0.08f + 0.06f * rarity);
        if (item.Rarity >= Rarity.Rare) Fx.Burst(this, at, Palette.Of(item.Rarity), 24 + 12 * rarity);
        if (item.Magic)
        {
            Fx.Burst(this, at, Palette.Magic, 60, 320f);
            Sfx.Play("sting", 1.3f, -2);
            FloatText("MAGIC!", at + new Vector2(0, -70), Palette.Magic, 24);
        }
        if (item.Fake)
        {
            Sfx.Play("lose", 1.2f, -6);
            FloatText("FAKE!", at + new Vector2(0, -70), Palette.Bad, 24);
        }
        if (item.Broken)
        {
            Sfx.Play("shatter", 1.1f, -6);
            sprite.Modulate = new Color(0.5f, 0.45f, 0.5f);
        }
        // Better or worse than the chest's colour promised: the surprise.
        var chestColour = _purchase.Chest.Quality;
        if (!item.Fake && !item.Broken && item.Quality > chestColour)
        {
            Sfx.Play("reveal_big", 1f + 0.1f * (item.Quality - chestColour), -4);
            FloatText("BETTER THAN EXPECTED!", at + new Vector2(0, -96), Palette.Of(item.Quality), 16);
        }
        bool isNew = State.Collect(item);
        int worth = (int)MathF.Round(item.Value * Picks.Multiplier(_purchase.Grade));
        _landed.Add((item, at, worth));

        var name = Add(UiKit.MakeLabel(item.Name, 16, Palette.Of(item.Rarity), HorizontalAlignment.Center, outline: 4)
            .Place(at.X - 110, 568, 220, 20));
        if (isNew)
        {
            var badge = Add(UiKit.MakeLabel("NEW!", 16, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(at.X - 40, 548, 80, 20));
            badge.ZIndex = 6;
        }
        var value = Add(UiKit.MakeLabel($"{worth}g", 24, Palette.Of(item.Rarity), HorizontalAlignment.Center, outline: 4)
            .Place(at.X - 70, 588, 140, 30));
        value.PivotOffset = new Vector2(70, 15);
        value.Scale = Vector2.One * 1.8f;
        value.CreateTween().TweenProperty(value, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        name.Modulate = Colors.Transparent;
        name.CreateTween().TweenProperty(name, "modulate", Colors.White, 0.2f);

        // The mark against the slip stamps down a moment later.
        var mark = Add(UiKit.MakeCheck(read.Right, new Vector2(at.X + 64, 603), 1));
        mark.Scale = Vector2.One * 3;
        mark.Modulate = Colors.Transparent;
        var stamp = mark.CreateTween();
        stamp.TweenInterval(0.18f);
        stamp.TweenCallback(Callable.From(() =>
        {
            mark.Modulate = Colors.White;
            Sfx.Play(read.Right ? "blip" : "miss", read.Right ? 1.2f : 1f, -4);
        }));
        stamp.TweenProperty(mark, "scale", Vector2.One, 0.14f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        string quality = item.Fake || item.Broken ? "" : Categories.QualityName(item.Quality);
        string note = string.Join(", ", new[] { read.Note, quality }.Where(s => s.Length > 0));
        Add(UiKit.MakeLabel(note, 16, read.Right ? Palette.Good : Palette.Warn, HorizontalAlignment.Center, outline: 4)
            .Place(at.X - 110, 618, 220, 20));

        int from = _worth;
        _worth += worth;
        var count = CreateTween();
        count.TweenMethod(Callable.From<float>(v => _worthLabel.Text = $"{Mathf.RoundToInt(v)}g"), (float)from, (float)_worth, 0.4f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _worthLabel.PivotOffset = new Vector2(120, 15);
        _worthLabel.Scale = Vector2.One * 1.3f;
        _worthLabel.CreateTween().TweenProperty(_worthLabel, "scale", Vector2.One, 0.2f);
    }

    /// <summary>Profit or loss stamped across the screen, the coins flying into your purse, and the reason.</summary>
    async void Verdict()
    {
        _step = Step.Done;
        var visit = _purchase.Visit;
        int profit = _worth - _purchase.Paid;
        bool perfect = _report.Perfect;
        State.Earn(_worth);
        if (perfect) State.Today.PerfectReads++;

        var color = profit > 0 ? Palette.Good : profit < 0 ? Palette.Bad : Palette.Text;
        string text = profit > 0 ? $"PROFIT +{profit}g" : profit < 0 ? $"LOSS {profit}g" : "BROKE EVEN";
        var stamp = Add(UiKit.MakePanel(color).Place(320, 104, 480, 76));
        stamp.ZIndex = 10;
        stamp.AddChild(UiKit.MakeLabel(text, 40, color, HorizontalAlignment.Center, outline: 6).Place(0, 14, 480, 50));
        stamp.PivotOffset = new Vector2(240, 38);
        stamp.Rotation = -0.06f;
        stamp.Scale = Vector2.One * 3f;
        stamp.Modulate = Colors.Transparent;
        var slam = stamp.CreateTween().SetParallel();
        slam.TweenProperty(stamp, "scale", Vector2.One, 0.16f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        slam.TweenProperty(stamp, "modulate", Colors.White, 0.08f);
        await ToSignal(slam, Tween.SignalName.Finished);
        if (!Alive) return;
        Sfx.PlayGroup("land", 0.55f, 4);
        Sfx.Play(profit > 0 ? "win" : "lose", 1f, -2);
        Game.Shake(profit > 0 ? Mathf.Min(0.6f, 0.3f + profit / 600f) : 0.25f);
        Game.Flash(color, 0.25f);
        Fx.Dust(this, new Vector2(640, 180), 2f);
        Game.Moment("08_results", 0.3);

        if (profit > 0) FlyCoins(new Vector2(640, 142), Math.Clamp(profit / 4, 4, 28));
        else Game.Hud.SetGold(State.Gold);

        if (perfect)
        {
            await Wait(0.35);
            if (!Alive) return;
            var banner = Add(UiKit.MakePanel(Palette.Gold).Place(-400, 188, 360, 44));
            banner.PivotOffset = Vector2.Zero;
            banner.ZIndex = 10;
            banner.AddChild(UiKit.MakeLabel("PERFECT READ", 24, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(0, 8, 360, 30));
            banner.CreateTween().TweenProperty(banner, "position:x", 380f, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            Sfx.Play("fanfare", 1.1f, -4);
            Fx.Burst(this, new Vector2(560, 210), Palette.Gold, 70, 360f);
        }

        string grade = _purchase.Grade switch
        {
            PickGrade.Masterful => " Masterful pick: everything +25%.",
            PickGrade.Forced => " Forced open: everything -10%.",
            _ => "",
        };
        Add(UiKit.MakeLabel(visit.Explain() + grade, 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Wrapped().Place(160, 238, 800, 64));
        bool last = State.Queue.Count == 0;
        Add(UiKit.MakeButton(last ? "Close up for the day" : "Next customer", Palette.Gold, 24, Next).Place(1000, 300, 260, 52));
        foreach (var (item, at, worth) in _landed) AddKeepButton(item, at, worth);

        _mentor = Add(new MentorView { Position = new Vector2(812, 72), ZIndex = 20 });
        Teach("opened");
        if (_landed.Any(l => l.Item.Quality != _purchase.Chest.Quality && l.Item.Displayable)) Teach("quality");
        if (_landed.Any(l => l.Item.Displayable && l.Item.Prestige >= 4)) Teach("keep");
        Game.Moment("08_verdict", 1.2);
    }

    /// <summary>
    /// A button under something grand enough to show: keep it for display instead of selling it. You give up its price;
    /// the exhibition draws more and better customers, and the Crown wants to see one.
    /// </summary>
    void AddKeepButton(Item item, Vector2 at, int worth)
    {
        if (!item.Displayable) return;
        var button = UiKit.MakeButton($"Keep (-{worth}g)", Palette.Magic.Darkened(0.3f), 16).Place(at.X - 80, 644, 160, 34);
        button.Pressed += () =>
        {
            if (!State.Keep(item, worth)) return;
            button.Text = "On display";
            button.Disabled = true;
            Sfx.Play("sting", 1.4f, -4);
            Game.Hud.SetGold(State.Gold);
            FloatText($"+{item.Prestige} exhibition", at + new Vector2(0, -80), Palette.Magic, 16);
            Teach("kept");
            RefreshKeep();
        };
        _keepButtons.Add((button, item, worth));
        Add(button);
        RefreshKeep();
    }

    readonly List<(Button Button, Item Item, int Worth)> _keepButtons = new();

    void RefreshKeep()
    {
        foreach (var (button, item, worth) in _keepButtons)
        {
            if (State.Display.Contains(item)) continue;
            button.Disabled = State.DisplayFull || State.Gold < worth;
            button.Text = State.DisplayFull ? "Display full" : $"Keep (-{worth}g)";
        }
    }

    /// <summary>Keeps one of the things that came out for display (the bot uses this).</summary>
    public bool Keep(Item item)
    {
        var entry = _keepButtons.FirstOrDefault(k => k.Item == item);
        if (entry.Button == null || entry.Button.Disabled) return false;
        entry.Button.EmitSignal(BaseButton.SignalName.Pressed);
        return State.Display.Contains(item);
    }

    public IEnumerable<(Item Item, int Worth)> Revealed => _landed.Select(l => (l.Item, l.Value));

    void Teach(string key)
    {
        if (State.FirstTime(key)) _mentor?.Say(Hints.Get(key));
    }

    /// <summary>Coins arc from the stamp into the purse in the corner, one tick each, and the purse counts up.</summary>
    void FlyCoins(Vector2 from, int count)
    {
        var purse = new Vector2(1084, 27);
        // Above the HUD, so the coins land in the purse rather than sliding under the bar.
        var layer = Add(new CanvasLayer { Layer = 11 });
        for (int i = 0; i < count; i++)
        {
            var coin = new Sprite2D { Texture = Fx.Coin, Scale = Vector2.One * 2, Position = from };
            layer.AddChild(coin);
            var spread = new Vector2((float)GD.RandRange(-160, 160), (float)GD.RandRange(-90, 40));
            float delay = i * 0.035f;
            bool lastCoin = i == count - 1;
            var tween = coin.CreateTween();
            tween.TweenInterval(delay);
            tween.TweenProperty(coin, "position", from + spread, 0.22f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(coin, "position", purse, 0.42f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() =>
            {
                Sfx.Play("coin", 0.9f + 0.4f * i / count, -10);
                if (lastCoin) Game.Hud.SetGold(State.Gold);
                coin.QueueFree();
            }));
        }
    }

    /// <summary>On to the next customer, or the end of the day.</summary>
    public void Next()
    {
        if (_step != Step.Done) return;
        Sfx.Play("bid");
        Game.NextCustomer();
    }

    void DrawTable(Painter p)
    {
        p.DrawRect(new Rect2(-100, TableY, 1480, 18), new Color("#93602f"));
        p.DrawRect(new Rect2(-100, TableY, 1480, 3), new Color("#b07a40"));
        p.DrawRect(new Rect2(-100, TableY + 18, 1480, 360), new Color("#5e3718"));
        for (float x = 0; x < 1280; x += 128) p.DrawRect(new Rect2(x, TableY + 18, 3, 360), new Color("#4a2a14"));
        p.DrawRect(new Rect2(-100, TableY + 18, 1480, 6), new Color(0, 0, 0, 0.35f));
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
