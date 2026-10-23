using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The battle lane across the top of the screen (1280x420): the zone's backdrop, the knight on the left and the
/// monsters walking in from the right, and everything the battle's events call for: swings and slashes coloured by the
/// knight's weapon, damage numbers, legendary powers going off, falls, bosses with their bar and clock. Loot leaves
/// from here: <see cref="ChestDropped"/> and <see cref="GoldDropped"/> tell the screen where it fell from.
/// </summary>
public partial class BattleView : Node2D
{
    public const float Width = 1280, Height = 420, Ground = 386, HeroX = 300, FrontX = 520;

    readonly GameState _g;
    readonly Node2D _back = new(), _actors = new() { YSortEnabled = true }, _front = new(), _numbers = new();
    Sprite2D _backdrop, _backdropOld;
    CpuParticles2D _ambience;
    ActorView _hero;
    readonly Dictionary<Fighter, ActorView> _foes = new();
    // Where this wave's fallen were, for the gold and chests that pop out of them after their views are gone.
    readonly Dictionary<Fighter, Vector2> _fallen = new();
    string _zone;
    readonly Random _rng = new();

    // The boss's bar and clock, over the lane.
    Node2D _bossUi;
    Bar _bossHp, _bossClock;
    Label _bossName;
    Fighter _boss;

    /// <summary>A chest of this kind fell out of a monster at this spot (the screen flies it to the pile).</summary>
    public event Action<ChestKind, Vector2> ChestDropped;

    /// <summary>Gold fell out of a monster here.</summary>
    public event Action<double, Vector2> GoldDropped;

    public ActorView Hero => _hero;

    public BattleView(GameState g)
    {
        _g = g;
    }

    public override void _Ready()
    {
        AddChild(_back);
        AddChild(_actors);
        AddChild(_front);
        AddChild(_numbers);
        // Everything outside the lane is cut off (sprites walking in, big effects).
        var clip = new Control { ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, 0, Width, Height);
        RemoveChild(_back);
        clip.AddChild(_back);
        AddChild(clip);
        MoveChild(clip, 0);

        _hero = ActorView.Make(_g.Battle.Hero, "knight", 2, faceRight: true);
        _hero.PlaceAt(new Vector2(HeroX, Ground));
        _actors.AddChild(_hero);
        RefreshGear();

        BuildBossUi();
        SetZone(_g.Stage, instant: true);
        foreach (var f in _g.Battle.Foes.Where(f => f.Alive)) AddFoe(f, _g.Battle.Foes.IndexOf(f), walk: false);
        Layout(walk: false);
        if (_g.InBossFight) ShowBoss(_g.Battle.Foes.First(f => f.Alive));
    }

    /// <summary>The knight's blade and cape follow his weapon and armour.</summary>
    public void RefreshGear() => _hero.SetGear(_g.Worn(Slot.Weapon)?.Rarity, _g.Worn(Slot.Armor)?.Rarity);

    // ------------------------------------------------------------------------------------------------- the zone

    /// <summary>The backdrop, the drifting ambience and the music for the stage's zone (crossfading when it changes).</summary>
    public void SetZone(int stage, bool instant = false)
    {
        var zone = World.Zone(stage);
        if (zone.Key == _zone) return;
        _zone = zone.Key;
        _backdropOld?.QueueFree();
        _backdropOld = _backdrop;
        _backdrop = new Sprite2D
        {
            Texture = GD.Load<Texture2D>($"res://assets/backgrounds/{zone.Background}.png"),
            Centered = false, Scale = Vector2.One * 2,
        };
        // Past the last zone the places come round again, darker.
        if (World.Cycle(stage) > 0) _backdrop.Modulate = new Color(0.82f, 0.78f, 0.92f);
        _back.AddChild(_backdrop);
        if (_backdropOld != null)
        {
            _backdrop.Modulate = _backdrop.Modulate with { A = 0 };
            var t = CreateTween();
            t.TweenProperty(_backdrop, "modulate:a", 1f, instant ? 0.01f : 1.2f);
            var old = _backdropOld;
            t.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(old)) old.QueueFree();
            }));
            _backdropOld = null;
        }
        _ambience?.QueueFree();
        _ambience = Ambience(zone.Key);
        _back.AddChild(_ambience);
        Main.Instance?.Music?.Play(zone.Music);
    }

    /// <summary>Something drifting through each zone: leaves, sand, sea sparkle, motes, snow, embers.</summary>
    static CpuParticles2D Ambience(string zone)
    {
        var (color, dir, speed, gravity, amount) = zone switch
        {
            "greenwood" => (new Color("#9ee05a"), new Vector2(-0.3f, 1), 40f, new Vector2(-10, 18), 26),
            "dunes" => (new Color("#f3d79a"), new Vector2(-1, 0.05f), 160f, new Vector2(-20, 4), 40),
            "coast" => (new Color("#ffffff"), new Vector2(0, -1), 10f, Vector2.Zero, 22),
            "ruins" => (new Color("#c9a6ff"), new Vector2(0, -1), 18f, new Vector2(0, -6), 24),
            "frostpeak" => (new Color("#ffffff"), new Vector2(-0.2f, 1), 50f, new Vector2(-6, 20), 60),
            _ => (new Color("#ff9a3c"), new Vector2(0.1f, -1), 50f, new Vector2(4, -20), 36),
        };
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 0.2f, 0.8f, 1f };
        ramp.Colors = new[] { color with { A = 0 }, color with { A = 0.8f }, color with { A = 0.6f }, color with { A = 0 } };
        return new CpuParticles2D
        {
            Amount = amount, Lifetime = 7, Preprocess = 7, Texture = Fx.Pixel, EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(Width * 0.6f, Height / 2), Position = new Vector2(Width / 2, Height / 2),
            Direction = dir, Spread = 20, InitialVelocityMin = speed * 0.6f, InitialVelocityMax = speed, Gravity = gravity,
            ScaleAmountMin = 1.5f, ScaleAmountMax = 3f, ColorRamp = ramp, ZIndex = 1,
        };
    }

    // ------------------------------------------------------------------------------------------------- monsters

    static int ScaleFor(Fighter f) =>
        f.Boss && f.Monster.Size == Size.Small ? 3 : 2;

    void AddFoe(Fighter f, int index, bool walk)
    {
        var view = ActorView.Make(f, f.Monster.Sprite, ScaleFor(f), faceRight: false);
        _foes[f] = view;
        _actors.AddChild(view);
        if (f.Mimic) view.ShowName("MIMIC", Palette.Of(Rarity.Legendary));
        var start = new Vector2(Width + view.Width * 0.6f + index * 160, Ground - (f.Monster.Flies ? 70 : 0));
        view.PlaceAt(start);
    }

    /// <summary>Stands the living foes in a row from the front spot, the first nearest the knight.</summary>
    void Layout(bool walk, float? seconds = null)
    {
        float x = FrontX;
        int i = 0;
        foreach (var f in _g.Battle.Foes)
        {
            if (!f.Alive || !_foes.TryGetValue(f, out var view)) continue;
            float half = Mathf.Max(60, view.Width * 0.5f);
            x += half;
            var spot = new Vector2(x, Ground - (f.Monster.Flies ? 70 : 0));
            if (walk) view.WalkTo(spot, seconds ?? (float)(Balance.ArriveTime + Balance.ArriveStagger * i));
            else view.PlaceAt(spot);
            x += half + 24;
            i++;
        }
    }

    void ClearFoes()
    {
        foreach (var v in _foes.Values)
            if (IsInstanceValid(v)) v.QueueFree();
        _foes.Clear();
        _fallen.Clear();
    }

    // ------------------------------------------------------------------------------------------------- boss bar

    void BuildBossUi()
    {
        _bossUi = new Node2D { Position = new Vector2(Width / 2 - 300, 56), Visible = false, ZIndex = 40 };
        _bossName = UiKit.MakeLabel("", 24, Palette.Gold, HorizontalAlignment.Center, outline: 6).Place(0, -6, 600, 30);
        _bossUi.AddChild(_bossName);
        _bossHp = UiKit.MakeBar(600, 14, new Color("#e0384c"), new Color("#2a1018"));
        _bossHp.Position = new Vector2(0, 28);
        _bossUi.AddChild(_bossHp);
        _bossClock = UiKit.MakeBar(600, 5, Palette.Gold, new Color("#2a2410"));
        _bossClock.LagColor = Colors.Transparent;
        _bossClock.Position = new Vector2(0, 48);
        _bossUi.AddChild(_bossClock);
        AddChild(_bossUi);
    }

    void ShowBoss(Fighter boss)
    {
        _boss = boss;
        _bossName.Text = boss.Name.ToUpperInvariant();
        _bossHp.Set(1, instant: true);
        _bossClock.Set(1, instant: true);
        _bossUi.Visible = true;
    }

    void HideBoss()
    {
        _boss = null;
        _bossUi.Visible = false;
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_boss != null)
        {
            _bossHp.Set((float)_boss.HpShare);
            _bossClock.Set((float)(_g.Battle.BossTimeLeft / Balance.BossTime));
        }
    }

    // ------------------------------------------------------------------------------------------------- events

    public void Handle(GameEvent e)
    {
        switch (e)
        {
            case WaveSpawned w: OnWave(w); break;
            case AttackStarted a: OnAttack(a); break;
            case HitLanded h: OnHit(h); break;
            case Dodged d:
                _hero.Guard();
                Number("DODGE", _hero.Top, Palette.Thunder, 24);
                Main.Instance.Sfx.Play("dodge", 1.1f, -6);
                break;
            case Healed h when h.Amount > 0:
                Number("+" + Num.Short(h.Amount), _hero.Top + new Vector2(0, 10), Palette.Heal, 24);
                Anims.PlayFx(_front, h.Source == Effect.HolyLight ? "holy" : "heal", _hero.Middle, Palette.Heal, 1.2f);
                Main.Instance.Sfx.Play("heal", 1f, -6);
                break;
            case BarrierRaised:
                Fx.Ring(_front, _hero.Middle, Palette.Barrier, 110, 0.5f, 5);
                Number("BARRIER", _hero.Top, Palette.Barrier, 16);
                Main.Instance.Sfx.Play("barrier", 1f, -6);
                break;
            case StunnedFoe s when _foes.TryGetValue(s.Target, out var v):
                Number("STUN", v.Top + new Vector2(0, -10), Palette.Crit, 16);
                Anims.PlayFx(_front, "wind", v.Top + new Vector2(0, 30), Palette.Crit, 1.6f);
                break;
            case MeteorFalling m: OnMeteor(m); break;
            case FighterDied d: OnDied(d.Fighter); break;
            case HeroRevived:
                _hero.Revive();
                Anims.PlayFx(_front, "holy", _hero.Middle, Colors.White, 1.2f);
                break;
            case BossTimedOut:
                break;
            case GoldGained g when g.From != null:
                GoldDropped?.Invoke(g.Amount, SpotOf(g.From));
                break;
            case ChestGained c when c.From != null:
                ChestDropped?.Invoke(c.Kind, SpotOf(c.From));
                break;
            case BossStarted b: ShowBoss(b.Boss); break;
            case BossFailed:
                HideBoss();
                break;
            case StageCleared:
                HideBoss();
                break;
        }
    }

    void OnWave(WaveSpawned w)
    {
        ClearFoes();
        for (int i = 0; i < w.Foes.Count; i++) AddFoe(w.Foes[i], i, walk: true);
        Layout(walk: true);
        if (w.Boss)
        {
            var boss = w.Foes[0];
            if (_foes.TryGetValue(boss, out var view))
            {
                var aura = Fx.Aura(view, new Vector2(0, -view.Height * 0.45f), new Color("#ff3048"), view.Width * 0.7f, 0.07f);
                aura.ZIndex = -1;
            }
        }
    }

    void OnAttack(AttackStarted a)
    {
        if (a.Attacker.IsHero)
        {
            _hero.Attack((float)Balance.HeroWindup, lunge: true);
            Main.Instance.Sfx.PlayGroup("swing", 1.1f, -6, 0.05f);
        }
        else if (_foes.TryGetValue(a.Attacker, out var v)) v.Attack((float)Balance.MonsterWindup, lunge: true);
    }

    void OnHit(HitLanded h)
    {
        var sfx = Main.Instance.Sfx;
        if (h.Target.IsHero)
        {
            _hero.Hurt(h.Attacker.Boss ? 1.5f : 1f);
            Number("-" + Num.Short(h.Amount), _hero.Top + new Vector2(-30, 0), Palette.Hurt, h.Attacker.Boss ? 24 : 16, drift: -30);
            sfx.PlayGroup("hurt", 1f, -8, 0.06f);
            Main.Instance.Shake(h.Attacker.Boss ? 0.22f : 0.08f);
            return;
        }
        if (!_foes.TryGetValue(h.Target, out var v)) return;
        var weapon = _g.Worn(Slot.Weapon)?.Rarity;
        var blade = Palette.Blade(weapon);
        float drift = (float)(_rng.NextDouble() * 60 - 30);
        switch (h.Kind)
        {
            case HitKind.Normal:
            case HitKind.Double:
                v.Hurt(h.Crit ? 1.6f : 1f);
                Anims.PlayFx(_front, h.Crit ? "cross" : h.Kind == HitKind.Double ? "slash" : "crescent", v.Middle, blade, 1.8f,
                    flip: _rng.Next(2) == 0, additive: weapon >= Rarity.Epic);
                if (h.Crit)
                {
                    Number(Num.Short(h.Amount) + "!", v.Top, Palette.Crit, 32, drift);
                    sfx.PlayGroup("crit", 1f, -4, 0.05f);
                    Main.Instance.Shake(0.12f);
                    Main.Instance.HitStop(0.03);
                }
                else
                {
                    Number(Num.Short(h.Amount), v.Top, Colors.White, h.Kind == HitKind.Double ? 16 : 24, drift);
                    sfx.PlayGroup("hit", 1f, -8, 0.04f);
                }
                if (weapon >= Rarity.Legendary) Fx.Burst(_front, v.Middle, blade, 10, 220, lifetime: 0.4f);
                break;
            case HitKind.Thunder:
                v.Hurt(1.3f);
                Anims.PlayFx(_front, "lightning", v.Middle + new Vector2(0, -40), Palette.Thunder, 1.6f, scale: 3);
                Fx.Lightning(_front, v.Middle, Palette.Thunder, 4, 90, 0.3f);
                Number(Num.Short(h.Amount), v.Top + new Vector2(0, -20), Palette.Thunder, 24, drift);
                sfx.Play("thunder", 1f, -6);
                Main.Instance.Flash(Palette.Thunder, 0.12f, 0.15f);
                break;
            case HitKind.Burn:
                Number(Num.Short(h.Amount), v.Top + new Vector2(0, 16), Palette.Burn, 16, drift);
                if (_rng.Next(3) == 0) Anims.PlayFx(_front, "flame", v.Middle + new Vector2(0, 20), Colors.White, 1.4f, scale: 1);
                break;
            case HitKind.Cleave:
                v.Hurt(0.8f);
                Anims.PlayFx(_front, "slash", v.Middle, blade, 2f, flip: true);
                Number(Num.Short(h.Amount), v.Top, new Color("#d8d8ff"), 16, drift);
                break;
            case HitKind.Meteor:
                v.Hurt(1.6f);
                Anims.PlayFx(_front, "burst", v.Middle, Colors.White, 1.4f, scale: 2);
                Number(Num.Short(h.Amount), v.Top + new Vector2(0, -24), Palette.Burn, 32, drift);
                Main.Instance.Shake(0.3f);
                break;
            case HitKind.Thorns:
                v.Hurt(0.6f);
                Anims.PlayFx(_front, "claws", v.Middle, new Color("#a0ff80"), 1.8f);
                Number(Num.Short(h.Amount), v.Top + new Vector2(0, 10), new Color("#a0ff80"), 16, drift);
                break;
            case HitKind.Execute:
                v.Hurt(2f);
                Anims.PlayFx(_front, "cross", v.Middle, new Color("#ff4060"), 1.6f, scale: 3);
                Number("EXECUTE", v.Top + new Vector2(0, -20), new Color("#ff4060"), 24);
                sfx.PlayGroup("slice", 0.8f, -2);
                break;
        }
    }

    void OnMeteor(MeteorFalling m)
    {
        Main.Instance.Sfx.Play("meteor", 1f, -2);
        foreach (var t in m.Targets)
        {
            if (!_foes.TryGetValue(t, out var v)) continue;
            var target = v.Middle;
            var rock = Anims.PlayFx(_front, "fireball", target + new Vector2(-260, -360), Colors.White, 1f, scale: 2, rotation: 0.95f);
            var tween = rock.CreateTween();
            tween.TweenProperty(rock, "position", target, 0.35f).SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() =>
            {
                Anims.PlayFx(_front, "rocks", target + new Vector2(0, 30), Colors.White, 1.6f);
                Fx.Burst(_front, target, Palette.Burn, 30, 380);
            }));
        }
    }

    void OnDied(Fighter f)
    {
        var sfx = Main.Instance.Sfx;
        if (f.IsHero)
        {
            _hero.Die(free: false);
            Number("DOWN!", _hero.Top, Palette.Bad, 32);
            sfx.PlayGroup("defeat", 1f, -6);
            Main.Instance.Shake(0.35f);
            return;
        }
        if (!_foes.TryGetValue(f, out var v)) return;
        _foes.Remove(f);
        _fallen[f] = v.Middle;
        v.Die();
        Anims.PlayFx(_front, "poof", v.Middle + new Vector2(0, v.Height * 0.2f), Colors.White, 1.5f, scale: f.Boss ? 3 : 2);
        sfx.PlayGroup("kill", f.Boss ? 0.7f : 1f + (float)_rng.NextDouble() * 0.2f, f.Boss ? 0 : -6);
        if (f.Boss || f.Mimic)
        {
            Fx.Burst(_front, v.Middle, Palette.Gold, 60, 520);
            Fx.Ring(_front, v.Middle, Palette.Gold, 240, 0.6f, 8);
            Main.Instance.Shake(0.5f);
            Main.Instance.Flash(Colors.White, 0.35f, 0.4f);
            Main.Instance.HitStop(0.12);
        }
        // The others close up the gap.
        Layout(walk: true, seconds: 0.35f);
    }

    /// <summary>The middle of a monster, standing or just fallen.</summary>
    Vector2 SpotOf(Fighter f) =>
        _foes.TryGetValue(f, out var v) ? v.Middle : _fallen.TryGetValue(f, out var at) ? at : new Vector2(FrontX + 80, Ground - 60);

    /// <summary>A damage number (or word) popping up from a spot, kept below the boss's bar.</summary>
    void Number(string text, Vector2 at, Color color, int size, float drift = 0)
    {
        float top = _bossUi.Visible ? 176 : 120;
        if (at.Y < top) at.Y = top + (float)_rng.NextDouble() * 20;
        Fx.Float(_numbers, text, at, color, size, rise: _bossUi.Visible ? 36 : 56, duration: 0.85f, drift: drift);
    }

    readonly Queue<(string Text, Color Color, string Sub, float Hold)> _banners = new();
    bool _bannerShowing;
    double _clock, _bannerGoneAt;
    // A banner's time in the band, besides its hold: popping in, and fading out.
    const float BannerIn = 0.25f, BannerOut = 0.3f;

    /// <summary>Seconds until the banner showing and those waiting are gone (a hint waits for them rather than overlap).</summary>
    public double BannerTimeLeft => Math.Max(0, _bannerGoneAt - _clock) + _banners.Sum(b => BannerIn + b.Hold + BannerOut);

    /// <summary>A big line of text across the lane ("BOSS", "STAGE CLEAR"); one at a time, in the order they came.</summary>
    public void Banner(string text, Color color, string sub = null, float hold = 1.2f)
    {
        _banners.Enqueue((text, color, sub, hold));
        if (!_bannerShowing) NextBanner();
    }

    void NextBanner()
    {
        if (_banners.Count == 0)
        {
            _bannerShowing = false;
            return;
        }
        _bannerShowing = true;
        var (text, color, sub, hold) = _banners.Dequeue();
        // A backlog is shown quicker.
        if (_banners.Count > 0) hold = Mathf.Min(hold, 0.6f);
        ShowBanner(text, color, sub, hold);
    }

    void ShowBanner(string text, Color color, string sub, float hold)
    {
        _bannerGoneAt = _clock + BannerIn + hold + BannerOut;
        var root = new Node2D { Position = new Vector2(0, 150), ZIndex = 60 };
        var band = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore }.Place(0, -10, Width, sub == null ? 70 : 100);
        root.AddChild(band);
        var label = UiKit.MakeLabel(text, 48, color, HorizontalAlignment.Center, outline: 8).Place(0, -4, Width, 60);
        root.AddChild(label);
        if (sub != null) root.AddChild(UiKit.MakeLabel(sub, 16, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 58, Width, 24));
        AddChild(root);
        root.Modulate = Colors.Transparent;
        label.PivotOffset = new Vector2(Width / 2, 30);
        label.Scale = new Vector2(1.6f, 1.6f);
        var t = root.CreateTween();
        t.TweenProperty(root, "modulate", Colors.White, 0.12f);
        t.Parallel().TweenProperty(label, "scale", Vector2.One, BannerIn).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenInterval(hold);
        t.TweenCallback(Callable.From(NextBanner));
        t.TweenProperty(root, "modulate:a", 0f, BannerOut);
        t.TweenCallback(Callable.From(root.QueueFree));
    }

    /// <summary>A glow, a ring and a number over the knight: he just got stronger.</summary>
    public void PowerUp(double gain, Color color)
    {
        _hero.Flash(color, 0.9f);
        Fx.Ring(_front, _hero.Middle, color, 140, 0.5f, 6);
        Fx.Burst(_front, _hero.Middle, color, 30, 300);
        if (gain > 0) Fx.Float(_numbers, $"+{Num.Short(gain)} POWER", _hero.Top + new Vector2(0, -16), Palette.Good, 24, rise: 70, duration: 1.4f);
    }
}
