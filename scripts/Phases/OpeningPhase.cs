using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The payoff, right after you buy. The chest opens by itself: it rumbles, glows in its own colour, and bursts. The lid
/// flies off and everything comes out, worst first. Things as good as the chest promised (or worse) land quickly on the
/// table; something better than its chest rises out glowing in the chest's colour and climbs, quality by quality, to
/// what it really is before it slams down, with more fuss for every step (two or three steps up stops the clock). Each
/// quality lands with its own effect: dust, sparkles, a pillar of light, crackling lightning, turning rays and a coin
/// fountain. A false bottom gives up its secret last. Then the verdict slams down with your gut's opinion, the coins fly
/// into your purse, and a perfect read gets its banner. Anything grand can be kept for display, and anything a
/// commission wants can be delivered instead of sold.
/// </summary>
public partial class OpeningPhase : Phase
{
    enum Step { Build, Burst, Reveal, Done }

    const float BuildSeconds = 0.9f;
    const int ChestPixel = 14;
    const float LandY = 564;

    /// <summary>The chest's origin is its lid seam, where light and loot come out.</summary>
    static readonly Vector2 SeamPos = new(640, 400);

    /// <summary>Where a better-than-expected find hangs while it climbs.</summary>
    static readonly Vector2 HoverPos = new(640, 236);

    Step _step = Step.Build;
    Purchase _purchase;
    ReadReport _report;
    MentorView _mentor;
    readonly Random _rng = new();
    Rarity _colour;
    int _worth;
    ChestView _chest;
    Painter _rays;
    float _raysAlpha, _raysAngle, _time;
    Label _worthLabel;
    CpuParticles2D _sparks;
    ColorRect _dim;
    readonly List<(Item Item, Vector2 At, int Value)> _landed = new();

    public float Progress { get; private set; }
    public MentorView Mentor => _mentor;
    public bool Finished => _step == Step.Done;

    public override void _Ready()
    {
        _purchase = State.Opening;
        _report = _purchase.Slip.Check(_purchase.Chest);
        _colour = _purchase.Chest.Quality;

        Add(UiKit.MakeShop(State.Tier, 0.42f));
        _rays = Add(new Painter(DrawRays) { Position = SeamPos, Material = Fx.Additive() });
        _chest = Add(new ChestView { Position = SeamPos, PixelSize = ChestPixel, Skin = _purchase.Chest.Skin, GlowColor = Palette.Of(_colour) });
        // The burst is in the chest's own colour: what's better than that is for the things inside to show.
        _sparks = Add(Fx.Sparks(Palette.Of(_colour), 140, 280, 820, spread: 70f, lifetime: 1.4f));
        _sparks.Position = SeamPos;
        // Darkens the room while a surprise climbs.
        _dim = Add(new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore }.Place(-100, -100, 1480, 920));
        _dim.ZIndex = 6;

        // The tally stays put while the camera leans in.
        var tally = Add(new CanvasLayer { Layer = 1 });
        bool vault = _purchase.Visit.Seller == Story.VaultSeller;
        tally.AddChild(UiKit.MakeLabel(vault ? "King Aldric's vault" : $"Paid {_purchase.Visit.Seller.Short}", 16, Palette.Muted).Place(40, 80, 200, 20));
        tally.AddChild(UiKit.MakeLabel($"{_purchase.Paid}g", 24, Palette.Text, HorizontalAlignment.Right, outline: 4).Place(200, 74, 120, 30));
        tally.AddChild(UiKit.MakeLabel("Worth", 16, Palette.Muted).Place(40, 114, 200, 20));
        _worthLabel = UiKit.MakeLabel("0g", 24, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(200, 108, 120, 30);
        tally.AddChild(_worthLabel);
        // What's making things sell for more today, and a curio's luck with what's inside.
        var bonuses = new List<string>();
        int sale = State.Active("jewelry");
        if (sale > 0) bonuses.Add($"Jewelry Tycoon +{Synergies.Pct(Synergies.SaleBonus(sale))}");
        if (State.EventIs("festival")) bonuses.Add("Festival +25%");
        if (State.EventIs("buyer")) bonuses.Add($"Royal Buyer: {Categories.Get(State.BuyerKind).Name.ToLowerInvariant()} x2");
        if (State.HasCurio("crescent")) bonuses.Add("Crescent +50%");
        float y = 140;
        foreach (var bonus in bonuses)
        {
            tally.AddChild(UiKit.MakeLabel(bonus, 16, Palette.Magic).Place(40, y, 320, 20));
            y += 22;
        }
        if (_purchase.Luck != null)
            tally.AddChild(UiKit.MakeLabel($"{Curios.Get(_purchase.Luck).Name}: {_purchase.Improved} thing{(_purchase.Improved == 1 ? "" : "s")} came out better!",
                16, Palette.Good, outline: 4).Place(40, y, 420, 20));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _raysAngle += dt * 0.25f;
        _rays.QueueRedraw();
        if (_step != Step.Build) return;

        // It opens by itself; a click or Space hurries it along.
        bool hurry = Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left);
        Progress = Mathf.Min(1f, Progress + dt / BuildSeconds * (hurry ? 3f : 1f));

        // Everything builds together: rumble, a widening crack of light, the hum, and the camera leaning in.
        _chest.Rumble = Progress;
        _chest.LidGap = 14f * Progress * Progress;
        _chest.Glow = Progress;
        Sfx.Hum(Progress);
        Game.Zoom(1f + 0.14f * Progress * Progress, SeamPos);
        if (Progress > 0.2f) Game.Shake(dt * 0.5f * Progress);
        if (Progress > 0.75f && Progress - dt / BuildSeconds <= 0.75f) Game.Moment("07_build");
        if (Progress >= 1f) Burst();
    }

    async void Burst()
    {
        _step = Step.Burst;
        Sfx.Hum(0);

        int tier = (int)_colour;
        var color = Palette.Of(_colour);
        Sfx.Play("burst", 1f, -2);
        Sfx.Play("whoosh", 1f, -4);
        Sfx.PlayGroup("land", 0.6f, 2);

        Game.HitStop(0.08 + 0.02 * tier);
        Game.Shake(0.45f + 0.08f * tier);
        Game.Flash(color.Lightened(0.6f), 0.85f, 0.45f);
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
        CreateTween().TweenMethod(Callable.From<float>(v => _raysAlpha = v), 0f, 0.15f + 0.06f * tier, 0.4f);
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

    /// <summary>How many qualities better than its chest promised (0 if not, or if it's fake or broken).</summary>
    int StepsUp(Item item) => item.Fake || item.Broken ? 0 : Math.Max(0, item.Quality - _colour);

    /// <summary>Worse than its chest promised (fakes and broken glass have their own letdowns).</summary>
    bool Worse(Item item) => !item.Fake && !item.Broken && item.Quality < _colour;

    async Task FlyOut()
    {
        _step = Step.Reveal;
        // The letdowns first, then what the chest promised, worst to best; then the surprises, smallest first; whatever
        // was under a false bottom comes out last of all.
        var reads = _report.Items.OrderBy(r => r.Hidden).ThenBy(r => StepsUp(r.Item)).ThenBy(r => r.Item.Value).ToList();
        int n = reads.Count;
        float spacing = n == 0 ? 0 : Math.Min(232f, 1180f / n);
        _spacing = spacing;
        for (int i = 0; i < n; i++)
        {
            bool last = i == n - 1;
            float x = 640 + (i - (n - 1) / 2f) * spacing;
            if (reads[i].Hidden)
            {
                _chest.Wobble(1f);
                Sfx.Play("hollow", 1.2f, -2);
                Sfx.Play("strain", 1.1f, -2);
                FloatText("...wait. A false bottom!", SeamPos + new Vector2(0, -150), Palette.Gold, 32);
                await Wait(0.9);
                if (!Alive) return;
            }
            if (StepsUp(reads[i].Item) > 0)
            {
                await Climb(reads[i], x, i, spacing);
                if (!Alive) return;
                await Wait(last ? 0.6 : 0.3);
            }
            else if (Worse(reads[i].Item))
            {
                await Sag(reads[i], x, spacing);
                if (!Alive) return;
                await Wait(last ? 0.6 : 0.15);
            }
            else
            {
                Launch(reads[i], x, i, spacing);
                await Wait(last ? 0.95 : 0.38);
            }
            if (!Alive) return;
        }
        if (n == 0)
        {
            // Nothing rattled because there was nothing there.
            _chest.Wobble(0.6f);
            Sfx.Play("thud", 0.8f, 0);
            Sfx.Play("lose", 0.8f, -6);
            FloatText("...EMPTY!", SeamPos + new Vector2(0, -150), Palette.Bad, 40);
            Game.Moment("08_empty", 0.3);
            await Wait(1.0);
            if (!Alive) return;
        }
        var ghosts = _report.Ghosts.GroupBy(c => c).Select(g => g.Count() == 1 ? $"no {Categories.Get(g.Key).Name.ToLowerInvariant()}?"
            : $"{g.Count()} fewer {Categories.Get(g.Key).Name.ToLowerInvariant()}?").ToList();
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

    /// <summary>Where a thing stands on the table, however tall it is.</summary>
    static Vector2 TableSpot(float x, Texture2D texture, int scale) => new(x, LandY - texture.GetHeight() * scale / 2f);

    static int ScaleFor(Texture2D texture) => Math.Max(1, (int)MathF.Min(64f / texture.GetHeight(), 72f / texture.GetWidth()));

    Sprite2D MakeSprite(Item item, out int scale)
    {
        var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{item.Sprite}.png");
        scale = ScaleFor(texture);
        var sprite = Add(new Sprite2D { Texture = texture, Scale = Vector2.One * scale, ZIndex = 7 });
        if (item.Fake) sprite.Modulate = new Color(0.75f, 0.75f, 0.8f);
        return sprite;
    }

    /// <summary>Something no better than its chest: flies out in an arc and lands on the table, quickly.</summary>
    void Launch(ItemRead read, float x, int index, float spacing)
    {
        var sprite = MakeSprite(read.Item, out int scale);
        var land = TableSpot(x, sprite.Texture, scale);
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
        flight.TweenCallback(Callable.From(() => Landed(read, sprite, scale, land, spacing, 0)));
    }

    /// <summary>
    /// Something worse than its chest: it pops up in the chest's colour as if all's well, then sags, the colour draining
    /// to what it really is, and flops onto the table. Quick: a letdown, not a show.
    /// </summary>
    async Task Sag(ItemRead read, float x, float spacing)
    {
        var item = read.Item;
        var sprite = MakeSprite(item, out int scale);
        var start = SeamPos + new Vector2(0, -ChestPixel * 3);
        var peak = new Vector2(Mathf.Lerp(start.X, x, 0.3f), start.Y - 170);
        sprite.Position = start;
        sprite.Modulate = Palette.Of(_colour).Lightened(0.3f);
        Sfx.Play("whoosh", 1.3f, -12);
        var up = sprite.CreateTween().SetParallel();
        up.TweenProperty(sprite, "position", peak, 0.3f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        up.TweenProperty(sprite, "scale", Vector2.One * scale * 1.5f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await ToSignal(up, Tween.SignalName.Finished);
        if (!Alive) return;

        var color = Palette.Of(item.Quality);
        Sfx.Play("deflate", 1f, -4);
        FloatText("WORSE...", peak + new Vector2(0, -70), Palette.Bad, 24);
        var sag = sprite.CreateTween().SetParallel();
        sag.TweenProperty(sprite, "modulate", color.Lightened(0.2f), 0.35f);
        sag.TweenProperty(sprite, "scale", new Vector2(scale * 1.2f, scale * 0.95f), 0.35f).SetTrans(Tween.TransitionType.Sine);
        sag.TweenProperty(sprite, "rotation", 0.3f, 0.35f).SetTrans(Tween.TransitionType.Sine);
        sag.TweenProperty(sprite, "position:y", peak.Y + 24, 0.35f).SetTrans(Tween.TransitionType.Sine);
        Game.Moment("07_worse", 0.2);
        await ToSignal(sag, Tween.SignalName.Finished);
        if (!Alive) return;

        var land = TableSpot(x, sprite.Texture, scale);
        var down = sprite.CreateTween().SetParallel();
        down.TweenProperty(sprite, "position", land, 0.26f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        down.TweenProperty(sprite, "scale", Vector2.One * scale, 0.26f);
        down.TweenProperty(sprite, "rotation", 0f, 0.26f);
        down.TweenProperty(sprite, "modulate", Colors.White, 0.26f);
        await ToSignal(down, Tween.SignalName.Finished);
        if (!Alive) return;
        Landed(read, sprite, scale, land, spacing, 0);
    }

    /// <summary>
    /// Something better than its chest: it rises out glowing in the chest's colour while the room dims, then climbs a
    /// quality at a time (each step bigger than the last) before it slams down onto the table.
    /// </summary>
    async Task Climb(ItemRead read, float x, int index, float spacing)
    {
        var item = read.Item;
        int steps = StepsUp(item);
        var sprite = MakeSprite(item, out int scale);
        var start = SeamPos + new Vector2(0, -ChestPixel * 3);
        sprite.Position = start;
        sprite.Scale = Vector2.One * scale * 0.5f;
        // It shows the colour it was expected to be until it proves otherwise.
        sprite.Modulate = Palette.Of(_colour).Lightened(0.3f);
        var aura = Fx.Aura(this, HoverPos, Palette.Of(_colour), 80f);
        aura.ZIndex = 7;

        _dim.CreateTween().TweenProperty(_dim, "color:a", 0.5f, 0.35f);
        Sfx.Play("rise", 1f, -4);
        Sfx.Play("strain", 1.2f, -6);
        var up = sprite.CreateTween().SetParallel();
        up.TweenProperty(sprite, "position", HoverPos, 0.55f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        up.TweenProperty(sprite, "scale", Vector2.One * scale * 2, 0.55f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await ToSignal(up, Tween.SignalName.Finished);
        if (!Alive) return;
        // Something's not right about it... it trembles.
        var tremble = sprite.CreateTween().SetLoops();
        tremble.TweenProperty(sprite, "position:x", HoverPos.X + 4, 0.04f);
        tremble.TweenProperty(sprite, "position:x", HoverPos.X - 4, 0.04f);
        await Wait(0.45);
        if (!Alive) return;

        string[] calls = { "BETTER!", "EVEN BETTER!!", "INCREDIBLE!!!" };
        for (int s = 1; s <= steps; s++)
        {
            var quality = _colour + s;
            var color = Palette.Of(quality);
            if (s >= 2)
            {
                // Another step: the world holds its breath first.
                Game.HitStop(0.12 + 0.06 * s);
                Sfx.Play("rise", 1.2f + 0.2f * s, -2);
                Game.Shake(0.2f);
                await Wait(0.55);
                if (!Alive) return;
            }
            sprite.Modulate = color.Lightened(0.5f);
            sprite.CreateTween().TweenProperty(sprite, "modulate", Colors.White, 0.4f);
            aura.Modulate = color;
            Game.Flash(color.Lightened(0.4f), 0.45f + 0.15f * s, 0.3f);
            Game.Shake(0.25f + 0.15f * s);
            Fx.Ring(this, HoverPos, color, 140f + 60f * s);
            Fx.Burst(this, HoverPos, color, 40 + 30 * s, 300f + 80f * s);
            Sfx.Play("upgrade", 1f + 0.12f * (s - 1), -2);
            Sfx.Play("sting", 1.2f + 0.15f * s, -4);
            FloatText(calls[Math.Min(s, calls.Length) - 1], HoverPos + new Vector2(0, -90), color, 24 + 8 * s);
            Game.Moment($"07_climb_{s}", 0.15);
            await Wait(0.55);
            if (!Alive) return;
        }
        tremble.Kill();
        aura.QueueFree();
        _dim.CreateTween().TweenProperty(_dim, "color:a", 0f, 0.3f);

        // Down it comes.
        var land = TableSpot(x, sprite.Texture, scale);
        var down = sprite.CreateTween().SetParallel();
        down.TweenProperty(sprite, "position", land, 0.28f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        down.TweenProperty(sprite, "scale", Vector2.One * scale, 0.28f);
        await ToSignal(down, Tween.SignalName.Finished);
        if (!Alive) return;
        Landed(read, sprite, scale, land, spacing, steps);
    }

    void Landed(ItemRead read, Sprite2D sprite, int scale, Vector2 at, float spacing, int stepsUp)
    {
        if (!Alive) return;
        var item = read.Item;

        // Squash on impact, then spring back.
        sprite.Scale = new Vector2(scale * 1.35f, scale * 0.65f);
        sprite.CreateTween().TweenProperty(sprite, "scale", Vector2.One * scale, 0.35f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        Sfx.PlayGroup(Audio.SoundSets.Group(item.Category), item.Def.Pitch, 4);
        QualityEffects(item, at, stepsUp);
        if (stepsUp > 0) Surprise(at, stepsUp);
        if (item.Magic && !item.Broken)
        {
            Fx.Burst(this, at, Palette.Magic, 60, 320f);
            Fx.Aura(this, at, Palette.Magic, 56f);
            Sfx.Play("sting", 1.3f, -2);
            FloatText("ENCHANTED! x2", at + new Vector2(0, -70), Palette.Magic, 24);
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
        bool isNew = State.Collect(item);
        int worth = State.SaleValue(item, _purchase.Grade);
        _landed.Add((item, at, worth));

        float w = Math.Min(220f, spacing - 6f);
        // Tight for room (a big lot, the vault): an enchanted thing's aura and note say it's enchanted, so its name needn't.
        string title = spacing < 170 && item.Magic && !item.Broken && !item.Fake ? item.Entry : item.Name;
        var name = Add(UiKit.MakeLabel(title, 16, Palette.Of(item.Rarity), HorizontalAlignment.Center, outline: 4)
            .Place(at.X - w / 2, 568, w, 20));
        name.ZIndex = 8;
        if (isNew)
        {
            var badge = Add(UiKit.MakeLabel("NEW!", 16, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(at.X - 40, 548, 80, 20));
            badge.ZIndex = 8;
        }
        var value = Add(UiKit.MakeLabel($"{worth}g", 24, Palette.Of(item.Rarity), HorizontalAlignment.Center, outline: 4)
            .Place(at.X - 70, 588, 140, 30));
        value.ZIndex = 8;
        value.PivotOffset = new Vector2(70, 15);
        value.Scale = Vector2.One * 1.8f;
        value.CreateTween().TweenProperty(value, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        name.Modulate = Colors.Transparent;
        name.CreateTween().TweenProperty(name, "modulate", Colors.White, 0.2f);

        // The mark against the slip stamps down a moment later.
        var mark = Add(UiKit.MakeCheck(read.Right, new Vector2(at.X + Math.Min(64f, w / 2 - 8), 603), 1));
        mark.ZIndex = 8;
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
        // With many things on the table, just the read (the colour already says the quality).
        string note = Narrow && read.Note.Length > 0 ? read.Note : string.Join(", ", new[] { read.Note, quality }.Where(s => s.Length > 0));
        var noteLabel = Add(UiKit.MakeLabel(note, 16, read.Right ? Palette.Good : Palette.Warn, HorizontalAlignment.Center, outline: 4)
            .Place(at.X - w / 2, 618, w, 20));
        noteLabel.ZIndex = 8;

        int from = _worth;
        _worth += worth;
        var count = CreateTween();
        count.TweenMethod(Callable.From<float>(v => _worthLabel.Text = $"{Mathf.RoundToInt(v)}g"), (float)from, (float)_worth, 0.4f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _worthLabel.PivotOffset = new Vector2(120, 15);
        _worthLabel.Scale = Vector2.One * 1.3f;
        _worthLabel.CreateTween().TweenProperty(_worthLabel, "scale", Vector2.One, 0.2f);
    }

    /// <summary>
    /// What landing looks and sounds like at each quality, so you can tell them apart without reading the colour:
    /// common, a puff of dust; uncommon, sparkles; rare, a pillar of light and a ring; epic, crackling lightning, a
    /// boom and a lingering aura; legendary, all that plus turning rays and a fountain of coins. A surprise makes it bigger.
    /// </summary>
    void QualityEffects(Item item, Vector2 at, int stepsUp)
    {
        var rarity = item.Rarity;
        var color = Palette.Of(rarity);
        float big = 1f + 0.35f * stepsUp;
        Fx.Dust(this, new Vector2(at.X, LandY), (0.8f + 0.2f * (int)rarity) * big);
        Sfx.PlayGroup("land", 1.2f - 0.1f * (int)rarity, -4);
        Game.Shake((0.06f + 0.05f * (int)rarity) * big);
        if (item.Fake || item.Broken) return;
        if (rarity >= Rarity.Uncommon)
        {
            var sparkles = Fx.Sparks(color, (int)(18 * big), 60, 160, spread: 40f, lifetime: 1.2f, gravity: -60f);
            sparkles.Position = at;
            AddChild(sparkles);
            sparkles.Emitting = true;
            GetTree().CreateTimer(1.6).Timeout += sparkles.QueueFree;
            Sfx.Play("chime", 0.8f + 0.1f * (int)rarity, -12);
        }
        if (rarity >= Rarity.Rare)
        {
            Fx.Pillar(this, new Vector2(at.X, LandY), color, 50f * big, 560f, 0.9f + 0.2f * stepsUp);
            Fx.Ring(this, at, color, 110f * big);
            Sfx.Play("chime", 1.1f, -6);
        }
        if (rarity >= Rarity.Epic)
        {
            Fx.Lightning(this, at, color, 5 + 2 * stepsUp, 130f * big);
            Fx.Aura(this, at, color, 64f);
            Sfx.Play("zap", 1f, -6);
            Sfx.Play("boom", 1f, -4);
            Game.Shake(0.25f * big);
        }
        if (rarity >= Rarity.Legendary)
        {
            Fx.Rays(this, at, color, 150f);
            var coins = Fx.Coins((int)(18 * big));
            coins.Position = at;
            AddChild(coins);
            coins.Emitting = true;
            GetTree().CreateTimer(2.2).Timeout += coins.QueueFree;
            Game.Flash(color.Lightened(0.5f), 0.6f, 0.4f);
            Game.HitStop(0.12);
            Sfx.Play("fanfare", 1f, -4);
        }
    }

    /// <summary>The payoff for a find better than its chest: a banner, and for two steps or more, the works.</summary>
    void Surprise(Vector2 at, int stepsUp)
    {
        string banner = stepsUp switch { 1 => "BETTER THAN EXPECTED!", 2 => "TWO QUALITIES UP!!", _ => "THREE QUALITIES UP!!!" };
        var color = stepsUp == 1 ? Palette.Gold : Palette.Of(Rarity.Legendary);
        FloatText(banner, at + new Vector2(0, -110), color, stepsUp == 1 ? 24 : 32);
        Sfx.Play("reveal_big", 1f + 0.1f * stepsUp, -3);
        if (stepsUp < 2) return;
        // Two or more: time slows, the room flashes white, confetti, a fanfare and a ring that fills the screen.
        Game.HitStop(0.2 + 0.1 * stepsUp);
        Game.Flash(Colors.White, 0.9f, 0.6f);
        Fx.Confetti(this, at, 70 + 40 * stepsUp);
        Fx.Ring(this, at, color, 700f, 0.8f, 10f);
        Fx.Rays(this, at, color, 260f, 16, 0.25f);
        Sfx.Play("fanfare", 0.9f + 0.1f * stepsUp, -2);
        Sfx.Play("win", 1f, -4);
        Game.Moment($"07_surprise_{stepsUp}", 0.3);
    }

    /// <summary>Profit or loss stamped across the screen with your gut's opinion, the coins flying into your purse, and the reason.</summary>
    async void Verdict()
    {
        _step = Step.Done;
        var visit = _purchase.Visit;
        bool perfect = _report.Perfect;
        // A keen eye's fee for a perfect read.
        int fee = perfect ? State.KeenFee(_worth) : 0;
        int held = _worth;
        _worth += fee;
        int profit = _worth - _purchase.Paid;
        State.Earn(_worth);
        State.RecordRead(perfect);
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
        float score = Gut.BuyScore(held, _purchase.Paid);
        var gut = Add(UiKit.MakeLabel(Gut.Bought(held, _purchase.Paid, _rng), 16, Palette.Gradient(score), HorizontalAlignment.Center, outline: 4)
            .Place(240, 188, 640, 20));
        gut.ZIndex = 10;
        var meter = Add(UiKit.MakeGutMeter(score, 212, "fleeced", "a steal").Place(334, 212, 452, 30));
        meter.ZIndex = 10;
        Game.Moment("08_results", 0.3);

        if (profit > 0) FlyCoins(new Vector2(640, 142), Math.Clamp(profit / 4, 4, 28));
        else Game.Hud.SetGold(State.Gold);

        if (perfect)
        {
            await Wait(0.35);
            if (!Alive) return;
            var banner = Add(UiKit.MakePanel(Palette.Gold).Place(-400, 56, 360, 44));
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
        if (fee > 0) grade += $" Keen Eye: a valuer's fee of {fee}g.";
        Add(UiKit.MakeLabel(visit.Explain() + grade, 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Wrapped().Place(160, 246, 800, 64));
        bool last = State.Queue.Count == 0;
        Add(UiKit.MakeButton(visit.Seller == Story.VaultSeller ? "Go on" : last ? "Close up for the day" : "Next customer", Palette.Gold, 24, Next)
            .Place(1000, 300, 260, 52));
        if (State.DisplayOpen)
            foreach (var (item, at, worth) in _landed) AddKeepButton(item, at, worth);
        foreach (var (item, at, worth) in _landed) AddDeliverButton(item, at, worth);
        if (_purchase.Luck != null)
        {
            Sfx.Play("chime", 1.2f, -4);
            Game.Moment("08_luck", 0.3);
        }

        _mentor = Add(new MentorView { Position = new Vector2(812, 72), ZIndex = 20 });
        Teach("opened");
        if (visit.Seller == Story.VaultSeller) Game.Moment("24_vault", 1.5);
        if (_landed.Any(l => l.Item.Quality != _purchase.Chest.Quality && l.Item.Displayable)) Teach("quality");
        if (State.DisplayOpen && _landed.Any(l => l.Item.Displayable && l.Item.Prestige >= 4)) Teach("keep");
        if (_deliverButtons.Count > 0) Teach("deliver");
        Game.Moment("08_verdict", 1.2);
    }

    /// <summary>
    /// A button under something grand enough to show: keep it for display instead of selling it. You give up its price;
    /// the exhibition draws more and better customers, and the Crown wants to see one. Enchanted things count double. With
    /// the display full, you pick something on display to sell to make room.
    /// </summary>
    void AddKeepButton(Item item, Vector2 at, int worth)
    {
        if (!item.Displayable) return;
        var button = UiKit.MakeButton("", Palette.Magic.Darkened(0.3f), 16).Place(at.X - ButtonW / 2, 644, ButtonW, 34);
        button.Pressed += () =>
        {
            if (State.DisplayFull) ShowSwap(item, worth, at);
            else if (State.Keep(item, worth)) Kept(item, at);
        };
        _keepButtons.Add((button, item, worth));
        Add(button);
        RefreshKeep();
    }

    readonly List<(Button Button, Item Item, int Worth)> _keepButtons = new();
    readonly List<(Button Button, Item Item, int Worth)> _deliverButtons = new();

    /// <summary>Room for each thing along the table; with many, the buttons under them are narrower and say less.</summary>
    float _spacing = 232f;

    float ButtonW => Math.Min(184f, _spacing - 8f);
    bool Narrow => ButtonW < 150f;
    readonly HashSet<Item> _delivered = new();

    /// <summary>
    /// A button under something a commission wants: hand it over instead of selling it. A piece pays at once; a lot pays
    /// when the last of it comes in. The Hooded Woman's pays a map piece too.
    /// </summary>
    void AddDeliverButton(Item item, Vector2 at, int worth)
    {
        if (State.WantedBy(item) == null) return;
        var button = UiKit.MakeButton("", Palette.Good.Darkened(0.35f), 16).Place(at.X - ButtonW / 2, 682, ButtonW, 32);
        button.Pressed += () => Deliver(item);
        _deliverButtons.Add((button, item, worth));
        Add(button);
        RefreshDeliver();
        Game.Moment("08_deliver", 0.6);
    }

    void RefreshDeliver()
    {
        foreach (var (button, item, worth) in _deliverButtons)
        {
            if (_delivered.Contains(item))
            {
                button.Text = "Delivered";
                button.Disabled = true;
                continue;
            }
            var c = State.WantedBy(item);
            button.Disabled = c == null || State.Display.Contains(item);
            if (c == null)
            {
                button.Text = "Filled";
                continue;
            }
            int reward = State.CommissionReward(c);
            button.Text = Narrow ? $"+{reward}g" : c.Needed > 1 ? $"Deliver {c.Delivered + 1}/{c.Needed}" : c.Story ? "Deliver: map!" : $"Deliver +{reward}g";
            button.TooltipText = $"{c.PatronName} wants {c.Phrase}: {reward}g{(c.Story ? " and a map piece" : "")}, instead of {worth}g for selling it.";
        }
    }

    /// <summary>Hands this thing over for the commission that wants it (the bot uses this too). Returns what it paid.</summary>
    public int Deliver(Item item)
    {
        var c = State.WantedBy(item);
        var entry = _deliverButtons.FirstOrDefault(d => d.Item == item);
        if (c == null || entry.Button == null || _delivered.Contains(item) || State.Display.Contains(item)) return 0;
        int pieces = State.MapPieces;
        int reward = State.Deliver(item, c, entry.Worth);
        _delivered.Add(item);
        var at = _landed.FirstOrDefault(l => l.Item == item).At;
        Game.Hud.SetGold(State.Gold);
        if (c.Done)
        {
            Sfx.Play("fanfare", 1.1f, -4);
            Sfx.PlayGroup("pay");
            Fx.Burst(this, at, Palette.Good, 60, 320f);
            FloatText($"Commission! +{reward}g", at + new Vector2(0, -100), Palette.Good, 24);
            if (State.MapPieces > pieces)
            {
                Fx.Confetti(this, new Vector2(640, 200), 100);
                FloatText($"MAP PIECE {State.MapPieces} OF {Story.Pieces}!", new Vector2(640, 200), Palette.Magic, 40);
                _mentor?.Say(Story.HoodThanks(State.HoodPieces));
                Game.Moment("08_map", 0.4);
            }
        }
        else
        {
            Sfx.Play("sting", 1.3f, -6);
            FloatText($"{c.Delivered}/{c.Needed}", at + new Vector2(0, -100), Palette.Good, 24);
        }
        foreach (var (button, kept, _) in _keepButtons)
            if (kept == item) button.Disabled = true;
        RefreshDeliver();
        RefreshKeep();
        return reward;
    }

    public bool Delivered(Item item) => _delivered.Contains(item);

    public IEnumerable<Item> Deliverable => _deliverButtons.Where(d => !_delivered.Contains(d.Item) && State.WantedBy(d.Item) != null).Select(d => d.Item);
    Control _swap;

    void Kept(Item item, Vector2 at)
    {
        RefreshDeliver();
        Sfx.Play("sting", 1.4f, -4);
        Game.Hud.SetGold(State.Gold);
        FloatText(item.Magic ? $"+{item.Prestige} exhibition (x2: enchanted!)" : $"+{item.Prestige} exhibition", at + new Vector2(0, -80),
            Palette.Magic, 16);
        Teach("kept");
        RefreshKeep();
    }

    void RefreshKeep()
    {
        foreach (var (button, item, worth) in _keepButtons)
        {
            if (State.Display.Contains(item))
            {
                button.Text = "On display";
                button.Disabled = true;
                continue;
            }
            if (_delivered.Contains(item))
            {
                button.Disabled = true;
                continue;
            }
            // The points it would add, so an enchanted thing's double shows before you choose.
            string points = item.Magic ? $"+{item.Prestige}*" : $"+{item.Prestige}";
            button.Disabled = _swap != null || State.Gold + (State.DisplayFull ? State.Display.Max(i => i.Value) : 0) < worth;
            button.Text = Narrow ? $"{(State.DisplayFull ? "Swap" : "Keep")} {points}" : State.DisplayFull ? $"Swap {points} (-{worth}g)" : $"Keep {points} (-{worth}g)";
            button.TooltipText = $"{item.Prestige} exhibition points for {worth}g" + (item.Magic ? " (double: it's enchanted)" : "") +
                                 (State.DisplayFull ? ". Your display is full: sell something on it to make room." : ".");
        }
    }

    /// <summary>The display is full: pick something on it to sell, and the new thing takes its place.</summary>
    void ShowSwap(Item item, int worth, Vector2 at)
    {
        if (_swap != null) return;
        var shown = State.Display.ToList();
        int columns = 3, rows = (shown.Count + columns - 1) / columns;
        float w = 984, h = 104 + rows * 48;
        // Clicking outside the chooser closes it, and nothing behind it can be pressed meanwhile.
        _swap = Add(new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = Control.MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920));
        _swap.ZIndex = 30;
        _swap.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) CloseSwap();
        };
        var panel = UiKit.MakePanel(Palette.Magic).Place(248, 296, w, h);
        _swap.AddChild(panel);
        panel.AddChild(UiKit.MakeLabel($"Your display is full. Sell something to make room for the {item.Name} (+{item.Prestige} points):",
            16, Palette.Text, HorizontalAlignment.Center).Place(0, 14, w, 20));
        for (int i = 0; i < shown.Count; i++)
        {
            var old = shown[i];
            bool affordable = State.Gold + old.Value >= worth;
            var choice = UiKit.MakeButton($"{old.Name}: +{old.Value}g, -{old.Prestige} pts", Palette.Of(old.Rarity).Darkened(0.4f), 16,
                () => Swap(item, worth, at, old)).Place(16 + i % columns * 320, 44 + i / columns * 48, 312, 40);
            choice.Disabled = !affordable;
            choice.AddChild(UiKit.MakeItemIcon(old, new Vector2(20, 20), 32));
            panel.AddChild(choice);
        }
        panel.AddChild(UiKit.MakeButton("Never mind", Palette.Text, 16, CloseSwap).Place(w / 2 - 90, h - 52, 180, 40));
        Sfx.PlayGroup("book", 1.2f, -6);
        RefreshKeep();
        Game.Moment("09_swap", 0.3);
    }

    void Swap(Item item, int worth, Vector2 at, Item old)
    {
        if (!State.Display.Contains(old)) return;
        State.SellFromDisplay(old);
        CloseSwap();
        Sfx.PlayGroup("pay");
        FloatText($"Sold the {old.Name}: +{old.Value}g", new Vector2(640, 196), Palette.Gold, 16);
        if (State.Keep(item, worth)) Kept(item, at);
        else Game.Hud.SetGold(State.Gold);
    }

    void CloseSwap()
    {
        _swap?.QueueFree();
        _swap = null;
        RefreshKeep();
    }

    /// <summary>Presses one thing's keep button (the bot uses this): it goes on display, or with the display full, the swap chooser opens.</summary>
    public bool Keep(Item item)
    {
        var entry = _keepButtons.FirstOrDefault(k => k.Item == item);
        if (entry.Button == null || entry.Button.Disabled) return false;
        entry.Button.EmitSignal(BaseButton.SignalName.Pressed);
        return State.Display.Contains(item);
    }

    /// <summary>With the swap chooser open, sells this exhibit to make room (the bot uses this).</summary>
    public void ChooseSwap(Item item, Item replace)
    {
        var entry = _keepButtons.FirstOrDefault(k => k.Item == item);
        var at = _landed.FirstOrDefault(l => l.Item == item).At;
        if (_swap != null && entry.Button != null) Swap(item, entry.Worth, at, replace);
    }

    public IEnumerable<(Item Item, int Worth)> Revealed => _landed.Select(l => (l.Item, l.Value));

    void Teach(string key)
    {
        if (State.FirstTime(key)) _mentor?.Say(Hints.Get(key));
    }

    /// <summary>Coins arc from the stamp into the purse in the corner, one tick each, and the purse counts up.</summary>
    void FlyCoins(Vector2 from, int count)
    {
        var purse = Hud.Purse;
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
        Game.AfterOpening();
    }

    void DrawRays(Painter p)
    {
        if (_raysAlpha <= 0.01f) return;
        var c = Palette.Of(_colour);
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
