using System;
using System.Collections.Generic;
using Godot;
using LootFall.Game;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>
/// The lane (docs/art-direction.md, "Combat presentation"): the backdrop, the knight, the monsters and the effects,
/// animated from the stage's cues so that every blow, fall and leap matches the rules. How a fight is shown follows the
/// kill time: a duel shows every blow and its number; waves merge numbers and keep a kill tally; leaps race the lane and
/// roll the depth; a gate shows what the next boss needs. Bosses always play in full. Only the lane shakes, and hit-stop
/// freezes only the knight and his target.
/// </summary>
public partial class BattleView : Node2D
{
    /// <summary>Kill times at least this long are shown as a duel (every blow and its number).</summary>
    public const double DuelKillTime = 0.8;

    readonly Session _s;
    public readonly Stage Stage;
    readonly Node2D _world;
    readonly Backdrop _backdrop;
    public readonly HeroRig Hero;
    readonly Node2D _actors, _fx;
    public readonly Numbers Numbers;
    readonly Dictionary<int, MonsterActor> _foes = new();
    readonly Bar _heroHp;
    readonly Painter _gate;
    readonly Label _tally, _gateLabel;
    float _trauma, _shakeT;
    double _lastCritStop, _tallyTime;
    int _tallyCount;
    readonly Random _rng = new();

    /// <summary>Where the last monster fell (chests leave from there).</summary>
    public Vector2 DropPoint { get; private set; } = new(Look.FrontX, Look.Ground - 120);

    /// <summary>The boss on the lane (null between bosses), for the HUD's bar and timer.</summary>
    public Foe Boss { get; private set; }
    public double BossClock { get; private set; }

    public event Action<Foe> BossEntered;
    public event Action<Foe, int> BossFell;
    public event Action<int, int> Leapt;

    public BattleView(Session s)
    {
        _s = s;
        Stage = new Stage(s);
        _world = new Node2D();
        AddChild(_world);
        _backdrop = new Backdrop();
        _world.AddChild(_backdrop);
        _gate = new Painter(DrawGate) { ZIndex = 2 };
        _world.AddChild(_gate);
        _actors = new Node2D { ZIndex = 10, YSortEnabled = true };
        _world.AddChild(_actors);
        Hero = new HeroRig { Position = new Vector2(Look.HeroX, Look.Ground), ZIndex = 12 };
        _world.AddChild(Hero);
        _fx = new Node2D { ZIndex = 30 };
        _world.AddChild(_fx);
        Numbers = new Numbers { ZIndex = 50, Enabled = Settings.DamageNumbers };
        _world.AddChild(Numbers);
        _heroHp = new Bar(Look.Good) { Radius = 3 }.Place(Look.HeroX - 60, Look.Ground + 18, 120, 10);
        _world.AddChild(_heroHp);
        _tally = Ui.Text("", 40, Look.Blow, Look.Pop, HorizontalAlignment.Center, 10).Place(Look.HeroX - 120, Look.Ground - 400, 240, 60);
        _tally.Modulate = Colors.Transparent;
        _world.AddChild(_tally);
        _gateLabel = Ui.Text("", 24, Look.Text, Look.UiBold, HorizontalAlignment.Center, 6).Place(1460, 300, 420, 60);
        _gateLabel.Visible = false;
        _world.AddChild(_gateLabel);
        _backdrop.SetDepth(Stage.Depth);
        RefreshGear();
    }

    public FightInfo Fight => Stage.F;

    public bool Duel => Stage.F.KillTime >= DuelKillTime || Stage.Mode == StageMode.Farm && Stage.F.KillTime >= DuelKillTime;

    public void RefreshGear() => Hero.Gear = GearLook.Of(_s.H);

    /// <summary>Called by the screen every frame, after the session's tick.</summary>
    public void Step(double delta)
    {
        Stage.Advance(delta);
        foreach (var cue in Stage.Cues) Handle(cue);
        Arrange((float)delta);
        _heroHp.Set((float)Stage.HeroHp);
        if (Boss != null) BossClock += delta;
        UpdateShake((float)delta);
        if (_tallyCount > 0 && (_tallyTime -= delta) <= 0)
        {
            _tallyCount = 0;
            _tally.CreateTween().TweenProperty(_tally, "modulate:a", 0f, 0.3f);
        }
        UpdateGate();
    }

    // ------------------------------------------------------------------------------------------------ cues

    void Handle(StageCue c)
    {
        switch (c.Kind)
        {
            case Cue.Spawn:
            {
                var a = new MonsterActor(c.Foe) { Position = new Vector2(Look.SpawnX + c.Foe.Slot * 140, Look.Ground) };
                _foes[c.Foe.Id] = a;
                _actors.AddChild(a);
                break;
            }
            case Cue.Windup:
                Hero.Windup((float)Stage.Lead);
                break;
            case Cue.Hit:
                Hit(c);
                break;
            case Cue.Kill:
                Kill(c.Foe);
                break;
            case Cue.Swipe:
                Swipe(c);
                break;
            case Cue.BossEnter:
            {
                var a = new MonsterActor(c.Foe) { Position = new Vector2(Look.SpawnX, Look.Ground) };
                _foes[c.Foe.Id] = a;
                _actors.AddChild(a);
                Boss = c.Foe;
                BossClock = 0;
                BossEntered?.Invoke(c.Foe);
                break;
            }
            case Cue.BossDown:
                BossDown(c);
                break;
            case Cue.Leap:
                Leap(c);
                break;
            case Cue.Reset:
                ClearLane();
                _backdrop.SetDepth(Stage.Depth);
                Main.Instance?.Flash(Look.Aether, 0.35f, 0.6f);
                break;
            case Cue.ModeChanged:
                ClearLane();
                break;
        }
    }

    void Hit(StageCue c)
    {
        if (c.Twice) Hero.Strike();
        else Hero.Strike();
        if (!_foes.TryGetValue(c.Foe.Id, out var a)) return;
        a.Struck(c.Crit);
        var at = a.HitPoint;
        var weapon = Hero.Gear.Weapon >= 0 ? Look.Tier(Hero.Gear.Weapon) : Look.Blow;
        Fx.Slash(_fx, at, weapon.Lerp(Look.Blow, 0.5f), c.Crit ? 1.4f : 1f, c.Twice);
        Fx.Burst(_fx, at, c.Crit ? Look.Crit : Look.Blow, c.Crit ? 18 : 9, c.Crit ? 520 : 360, 120, 500, 0.4f, 0.07f, "spark2", new Vector2(1, -0.3f));
        Main.Instance?.Sfx?.PlayGroup(c.Crit ? "crit" : "hit", 1f + (float)_rng.NextDouble() * 0.1f, c.Crit ? -4 : -9, 0.04f);
        if (c.Crit)
        {
            Shake(0.12f);
            double now = Time.GetTicksMsec() / 1000.0;
            if (now - _lastCritStop > 0.5)
            {
                _lastCritStop = now;
                Hero.Freeze(0.04);
                a.Freeze(0.04);
            }
        }
        bool showNumber = Duel || c.Foe.IsBoss || c.Crit || c.Foe.HitsDoneLast();
        if (!showNumber) return;
        string text = Describe.Amount(c.Le, _s.H.S);
        if (c.Overkill) text += "!";
        var colour = c.Crit ? Look.Crit : Look.Blow;
        int size = c.Crit ? 46 : c.Foe.IsBoss ? 36 : 32;
        Numbers.Show(text, at + new Vector2((float)_rng.NextDouble() * 40 - 20, -40), colour, size, 80, 0.8f, (float)_rng.NextDouble() * 40 - 10);
        if (c.Overkill && Duel) Numbers.Show("OVERKILL", at + new Vector2(0, -95), Look.Crit, 22, 50, 0.6f);
    }

    void Kill(Foe f)
    {
        if (!_foes.TryGetValue(f.Id, out var a)) return;
        _foes.Remove(f.Id);
        DropPoint = a.HitPoint;
        a.Fall();
        Fx.Burst(_fx, a.HitPoint, new Color(1f, 0.85f, 0.6f), 16, 420, 180, 700, 0.6f, 0.09f, "spark1");
        Fx.Puff(_fx, a.Position + new Vector2(0, -20), new Color(0.85f, 0.8f, 0.75f, 0.5f), 6, 0.45f);
        Main.Instance?.Sfx?.PlayGroup("kill", 1f, -10, 0.05f);
        if (!Duel)
        {
            _tallyCount++;
            _tallyTime = 1.2;
            if (_tallyCount >= 2)
            {
                _tally.Text = $"x{_tallyCount}";
                _tally.Modulate = Colors.White;
                Fx.Bump(_tally, 1.15f, 0.15f);
            }
        }
    }

    void Swipe(StageCue c)
    {
        if (_foes.TryGetValue(c.Foe.Id, out var a)) a.Lunge();
        var at = Hero.Position + new Vector2(-60, -Look.HeroHeight - 20);
        if (c.Dodged)
        {
            Numbers.Show("Dodge", at, Look.Muted, 26, 50, 0.6f, -20);
            Main.Instance?.Sfx?.Play("dodge", 1f, -12);
            return;
        }
        Hero.Hurt(Look.Threat);
        Fx.Burst(_fx, at + new Vector2(20, 10), Look.Threat, 8, 300, 100, 600, 0.35f, 0.06f, "spark3", new Vector2(-1, -0.4f));
        Numbers.Show((c.Blocked ? "Blocked " : "") + Describe.Amount(c.Le, _s.H.S), at, Look.Threat, 26, 60, 0.7f, -30);
        Main.Instance?.Sfx?.PlayGroup(c.Blocked ? "block" : "hurt", 1f, -14, 0.08f);
    }

    void BossDown(StageCue c)
    {
        var foe = c.Foe ?? Boss;
        Boss = null;
        if (foe != null && _foes.TryGetValue(foe.Id, out var a))
        {
            _foes.Remove(foe.Id);
            DropPoint = a.HitPoint;
            a.Freeze(0.12);
            Hero.Freeze(0.12);
            var at = a.HitPoint;
            GetTree().CreateTimer(0.12, false).Timeout += () =>
            {
                if (!IsInstanceValid(a)) return;
                a.Fall();
                Fx.Ring(_fx, at, Look.Blow, 260, 0.45f, 8);
                Fx.Burst(_fx, at, Look.Crit, 40, 700, 180, 600, 0.9f, 0.12f, "star1");
                Fx.Puff(_fx, a.Position + new Vector2(0, -30), new Color(0.9f, 0.85f, 0.8f, 0.6f), 12, 0.7f, 200);
            };
            Shake(foe.Role == FoeRole.Guardian ? 0.5f : 0.3f);
            Main.Instance?.Flash(Colors.White, foe.Role == FoeRole.Guardian ? 0.3f : 0.18f, 0.35f);
            Main.Instance?.Sfx?.PlayGroup("boss", 1f, -6);
            Hero.Cheer();
        }
        BossFell?.Invoke(foe, c.Depth);
        _backdrop.SetDepth(Stage.Depth);
    }

    void Leap(StageCue c)
    {
        Boss = null;
        foreach (var a in _foes.Values)
        {
            Fx.Burst(_fx, a.HitPoint, Look.Blow, 6, 300, 180, 500, 0.3f, 0.06f);
            a.Fall();
        }
        _foes.Clear();
        Fx.Ring(_fx, Hero.StrikePoint, Look.Blow, 200, 0.3f, 5);
        Leapt?.Invoke(c.Depth - c.Count, c.Depth);
        _backdrop.SetDepth(Stage.Depth);
    }

    void ClearLane()
    {
        foreach (var a in _foes.Values)
            if (IsInstanceValid(a)) a.QueueFree();
        _foes.Clear();
        foreach (var child in _actors.GetChildren()) child.QueueFree();
        Boss = null;
    }

    // ------------------------------------------------------------------------------------------------ placement

    /// <summary>Moves everyone to where the stage says: monsters walk in from the right toward their places, the lane
    /// scrolls while they come (the knight walks), and races in a leap.</summary>
    void Arrange(float dt)
    {
        bool approaching = false;
        int place = 0;
        // Monsters in wave order: the front one engages nearest the knight.
        foreach (var f in Stage.Foes)
        {
            if (!_foes.TryGetValue(f.Id, out var a) || a.Falling) continue;
            float engage = f.IsBoss ? Look.FrontX + a.HalfWidth * 0.55f : Look.FrontX + place * 170 + a.HalfWidth * 0.45f;
            if (!f.IsBoss && !f.Dead) place++;
            float x = Mathf.Lerp(Look.SpawnX + f.Slot * 140, engage, (float)Smooth(f.Approach));
            a.Position = new Vector2(Mathf.MoveToward(a.Position.X, x, dt * 2400), Look.Ground);
            if (f.Approach < 1) approaching = true;
        }
        float speed = Stage.Mode == StageMode.Leap ? 1800 : approaching ? 380 : 0;
        Hero.SetWalk(Stage.Mode == StageMode.Leap ? 2 : approaching ? 1 : 0);
        if (speed > 0) _backdrop.Scroll(speed * dt);
    }

    static double Smooth(double u) => u * u * (3 - 2 * u);

    // ------------------------------------------------------------------------------------------------ the gate

    void UpdateGate()
    {
        bool farm = Stage.Mode == StageMode.Farm;
        _gate.Visible = farm;
        _gateLabel.Visible = farm;
        if (!farm) return;
        var f = Stage.F;
        string need = f.NeedDamage > 1 && f.NeedDamage >= f.NeedHealth ? $"needs x{Format.Num(f.NeedDamage)} damage"
            : f.NeedHealth > 1 ? $"needs x{Format.Num(f.NeedHealth)} health" : "almost";
        _gateLabel.Text = $"{(f.BlockedBy == "guardian" ? "Guardian" : "Boss")} of depth {Format.Whole(f.Next)}\n{need}";
        _gate.QueueRedraw();
    }

    void DrawGate(Painter p)
    {
        // A stone arch at the lane's far end with the waiting boss's shadow in it.
        float x = 1670, w = 230, h = 330;
        var stone = new Ink.Ramp("#3b3546", "#585066", "#7a7088");
        var outer = new[] { new Vector2(x - w / 2, Look.Ground), new Vector2(x - w / 2, Look.Ground - h + 70), new Vector2(x, Look.Ground - h), new Vector2(x + w / 2, Look.Ground - h + 70), new Vector2(x + w / 2, Look.Ground) };
        var inner = new[] { new Vector2(x - w / 2 + 34, Look.Ground), new Vector2(x - w / 2 + 34, Look.Ground - h + 92), new Vector2(x, Look.Ground - h + 36), new Vector2(x + w / 2 - 34, Look.Ground - h + 92), new Vector2(x + w / 2 - 34, Look.Ground) };
        Ink.Shaded(p, outer, stone);
        p.DrawColoredPolygon(inner, new Color("#140f1d"));
        Ink.Outline(p, inner);
        var boss = Bestiary.Boss(Stage.F.Next);
        var art = Assets.Monster(boss.Key);
        if (art.Texture != null)
        {
            float hh = h * 0.62f, k = hh / art.Height;
            var size = new Vector2(art.Texture.GetWidth(), art.Texture.GetHeight()) * k;
            p.DrawTextureRect(art.Texture, new Rect2(new Vector2(x - size.X / 2, Look.Ground - size.Y), size), false, new Color(0.25f, 0.12f, 0.2f, 0.9f));
        }
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 300f);
        p.DrawArc(new Vector2(x, Look.Ground - h * 0.55f), 70, 0, Mathf.Tau, 48, new Color(Look.Threat, 0.25f + 0.2f * pulse), 4, true);
    }

    // ------------------------------------------------------------------------------------------------ shake

    /// <summary>Adds trauma to the lane's shake (only the lane shakes; strength is trauma squared, capped).</summary>
    public void Shake(float amount) => _trauma = Mathf.Min(0.8f, _trauma + amount * Settings.Shake);

    void UpdateShake(float dt)
    {
        _trauma = Mathf.Max(0, _trauma - dt * 1.6f);
        _shakeT += dt * 40;
        float s = _trauma * _trauma * 26;
        _world.Position = new Vector2(Mathf.Sin(_shakeT * 1.1f) * s, Mathf.Cos(_shakeT * 1.7f) * s * 0.7f);
    }
}

static class FoeExtensions
{
    /// <summary>Whether this blow was the one that felled it.</summary>
    public static bool HitsDoneLast(this Foe f) => f.Dead;
}
