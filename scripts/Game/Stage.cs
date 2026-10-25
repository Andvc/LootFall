using System;
using System.Collections.Generic;
using LootFall.Progression;

namespace LootFall.Game;

public enum FoeRole : byte { Monster, Boss, Guardian }

/// <summary>A monster on the lane, as the stage moves it.</summary>
public sealed class Foe
{
    public int Id;
    public FoeRole Role;
    public MonsterDef Def;
    public int Depth;
    /// <summary>Its place in the wave (0 in front).</summary>
    public int Slot;
    /// <summary>Health left, as a share.</summary>
    public double Hp = 1;
    /// <summary>How far it has come: 0 at the lane's far end, 1 face to face with the hero.</summary>
    public double Approach;
    public bool Dead;
    public bool IsBoss => Role != FoeRole.Monster;
    internal int Hits, HitsDone, WoundUpFor;
    /// <summary>When it's fought, in its wave's time (0 to 1).</summary>
    internal double Start, End;
}

/// <summary>How the hero is fighting: through the next depth, farming one already cleared, or leaping several depths at
/// a time by overkill.</summary>
public enum StageMode : byte { Push, Farm, Leap }

public enum Cue : byte
{
    /// <summary>A monster comes onto the lane (Foe).</summary>
    Spawn,
    /// <summary>The hero starts a blow that lands <see cref="Stage.Lead"/> seconds later (Foe: its target).</summary>
    Windup,
    /// <summary>A blow lands (Foe; Le: its damage; Crit; Twice: a multi-hit's second blow; Overkill: ten times what was
    /// left).</summary>
    Hit,
    /// <summary>A monster falls (Foe).</summary>
    Kill,
    /// <summary>A monster strikes at the hero (Foe; Le: the damage taken; Dodged; Blocked).</summary>
    Swipe,
    /// <summary>The depth's boss or guardian steps in (Foe).</summary>
    BossEnter,
    /// <summary>The boss falls and the depth is cleared (Foe; Depth: the depth cleared).</summary>
    BossDown,
    /// <summary>Overkill: Count depths cleared at once (Depth: the deepest).</summary>
    Leap,
    /// <summary>A new run starts at depth 1 (an ascension or a transcendence).</summary>
    Reset,
    /// <summary>The fight changed between pushing, farming and leaping: whatever was on the lane is gone.</summary>
    ModeChanged,
}

public struct StageCue
{
    public Cue Kind;
    public Foe Foe;
    public double Le;
    public bool Crit, Twice, Overkill, Dodged, Blocked;
    public int Depth, Count;
}

/// <summary>
/// The fight as the screen shows it, in step with the rules. The rules settle a depth in closed form (a kill time, a
/// boss time, how far through the depth the hero is); the stage turns that into waves of two walking in, blows landing
/// so that each monster falls exactly when its kill time is up, the boss's health draining with the depth's progress,
/// and leaps when overkill clears depths by the dozen. It reads the session and never changes it. Engine-free: the
/// screen animates its cues, and the tests can play it headless.
/// </summary>
public sealed class Stage
{
    /// <summary>Seconds a blow winds up before it lands (the screen starts the swing on <see cref="Cue.Windup"/>).</summary>
    public const double Lead = 0.16;

    /// <summary>The most blows a monster takes on screen; a tougher one takes the same number of bigger ones.</summary>
    const int MaxHits = 6;

    readonly Session _s;
    readonly Params P;
    readonly Scale S;
    readonly Rng _rng = new(90210);
    public FightInfo F;
    public StageMode Mode;
    public readonly List<Foe> Foes = new();
    public readonly List<StageCue> Cues = new();
    /// <summary>The hero's health as the screen shows it (a share; the rules never let a push take it all).</summary>
    public double HeroHp = 1;
    /// <summary>The depth being fought (the next one when pushing or leaping, the farmed one when farming).</summary>
    public int Depth => F.Depth;

    int _runDepth, _wave = -1, _waveDepth, _nextId;
    double _refresh, _farmClock, _bossClock, _swipeClock;
    Foe _boss;

    public Stage(Session s)
    {
        _s = s;
        P = s.P;
        S = s.H.S;
        _runDepth = s.H.RunDepth;
        F = s.Fight();
        Mode = ModeOf(F);
    }

    static StageMode ModeOf(in FightInfo f) => !f.Pushing ? StageMode.Farm : f.Skip > 1 ? StageMode.Leap : StageMode.Push;

    /// <summary>Moves the fight on by <paramref name="dt"/> seconds (after the session's own tick); the cues this frame
    /// are in <see cref="Cues"/>.</summary>
    public void Advance(double dt)
    {
        Cues.Clear();
        int depth = _s.H.RunDepth;
        bool moved = depth != _runDepth;
        if (moved) DepthChanged(depth);
        if (moved || (_refresh -= dt) <= 0)
        {
            F = _s.Fight();
            _refresh = 0.1;
        }
        var mode = ModeOf(F);
        if (mode != Mode)
        {
            Mode = mode;
            // The monsters standing fall before the fight changes kind: every one that comes on falls.
            foreach (var f in Foes)
                if (!f.Dead && !f.IsBoss) Fall(f);
            Clear();
            Cues.Add(new StageCue { Kind = Cue.ModeChanged });
        }
        switch (Mode)
        {
            case StageMode.Push:
                Push(dt);
                break;
            case StageMode.Farm:
                Farm(dt);
                break;
            default:
                Leap(dt);
                break;
        }
        Defend(dt);
    }

    void DepthChanged(int depth)
    {
        int cleared = depth - _runDepth;
        _runDepth = depth;
        if (cleared < 0)
        {
            Clear();
            HeroHp = 1;
            Cues.Add(new StageCue { Kind = Cue.Reset, Depth = depth });
            return;
        }
        // The waves still standing fall with the boss (the rules have moved on).
        foreach (var f in Foes)
            if (!f.Dead && !f.IsBoss) Fall(f);
        if (_boss != null && !_boss.Dead)
        {
            _boss.Hp = 0;
            _boss.Dead = true;
        }
        if (cleared == 1 && Mode != StageMode.Leap) Cues.Add(new StageCue { Kind = Cue.BossDown, Foe = _boss, Depth = depth });
        else Cues.Add(new StageCue { Kind = Cue.Leap, Foe = _boss, Depth = depth, Count = cleared });
        _boss = null;
        _wave = -1;
    }

    /// <summary>Everything leaves the lane at once (a reset, or the fight changing kind).</summary>
    void Clear()
    {
        Foes.Clear();
        _boss = null;
        _wave = -1;
        _bossClock = _swipeClock = 0;
    }

    // ---------------------------------------------------------------- pushing: the depth's progress drives the fight

    void Push(double dt)
    {
        if (F.Need <= 0) return;
        double p = Math.Min(0.999999, _s.W.DepthProgress + _s.Accum / F.Need);
        int waves = Math.Max(1, (int)Math.Round(P.KillsPerDepth / P.WaveSize));
        double period = P.WaveSize * F.KillTime + P.WaveGap;
        if (p < F.WaveShare)
        {
            double wf = p / F.WaveShare * waves;
            int w = Math.Min(waves - 1, (int)wf);
            Waves(F.Depth, w, wf - w, period);
        }
        else Boss(F.Depth, (p - F.WaveShare) / Math.Max(1e-9, 1 - F.WaveShare), dt);
    }

    // ---------------------------------------------------------------- farming: waves on the stage's own clock

    void Farm(double dt)
    {
        double period = P.WaveSize * F.KillTime + P.WaveGap;
        _farmClock += dt;
        double wf = _farmClock / period;
        int w = (int)wf;
        Waves(F.Depth, w, wf - w, period);
    }

    // ---------------------------------------------------------------- leaping: a wave and a boss per skip, at the floor

    void Leap(double dt)
    {
        double need = _s.W.H.Fight.SkipTime;
        double p = Math.Min(0.999999, _s.W.DepthProgress + _s.Accum / need);
        double waveTime = P.WaveSize * P.MinKillTime + P.WaveGap;
        double share = waveTime / need;
        int last = F.Depth + F.Skip - 1;
        if (p < share) Waves(last, 0, p / share, waveTime);
        else Boss(last, (p - share) / Math.Max(1e-9, 1 - share), dt);
    }

    // ---------------------------------------------------------------- waves

    /// <summary>
    /// Wave <paramref name="w"/> of a depth at <paramref name="u"/> of its way through (0 to 1; it lasts
    /// <paramref name="period"/> seconds): both walk in during the gap, then each falls after one kill time, the front
    /// one first, its blows spread evenly over its time.
    /// </summary>
    void Waves(int depth, int w, double u, double period)
    {
        double ttk = Math.Max(P.MinKillTime, Mode == StageMode.Leap ? P.MinKillTime : F.KillTime);
        if (w != _wave || depth != _waveDepth)
        {
            // The last wave's blows land before the next walks in (its last falls at the very end of its time).
            if (depth == _waveDepth) Fight(1, period);
            foreach (var f in Foes)
                if (!f.Dead) Fall(f);
            Foes.RemoveAll(f => f.Dead);
            _wave = w;
            _waveDepth = depth;
            int size = Math.Max(1, (int)P.WaveSize);
            int hits = Math.Clamp((int)Math.Ceiling(ttk * F.Speed - 1e-9), 1, MaxHits);
            for (int k = 0; k < size; k++)
            {
                var f = new Foe
                {
                    Id = ++_nextId, Role = FoeRole.Monster, Def = Bestiary.Monster(depth, w % 4096, k), Depth = depth, Slot = k,
                    Hits = hits, Start = (P.WaveGap + k * ttk) / period, End = (P.WaveGap + (k + 1) * ttk) / period,
                };
                Foes.Add(f);
                Cues.Add(new StageCue { Kind = Cue.Spawn, Foe = f });
            }
        }
        double walk = P.WaveGap / period;
        foreach (var f in Foes)
            if (!f.Dead && !f.IsBoss) f.Approach = Math.Min(1, u / Math.Max(1e-9, walk));
        Fight(u, period);
    }

    /// <summary>The wave's blows up to <paramref name="u"/> of its time: the front one is fought, the next waits its turn.</summary>
    void Fight(double u, double period)
    {
        foreach (var f in Foes)
        {
            if (f.Dead || f.IsBoss) continue;
            double next = f.Start + (f.End - f.Start) * (f.HitsDone + 1) / f.Hits;
            if (f.WoundUpFor <= f.HitsDone && (next - u) * period <= Lead)
            {
                f.WoundUpFor = f.HitsDone + 1;
                Cues.Add(new StageCue { Kind = Cue.Windup, Foe = f });
            }
            while (!f.Dead && u >= f.Start + (f.End - f.Start) * (f.HitsDone + 1) / f.Hits - 1e-9)
            {
                f.HitsDone++;
                f.Hp = 1 - (double)f.HitsDone / f.Hits;
                Blow(f);
                if (f.HitsDone >= f.Hits) Fall(f);
            }
            if (!f.Dead) break;
        }
    }

    void Fall(Foe f)
    {
        f.Hp = 0;
        f.Dead = true;
        Cues.Add(new StageCue { Kind = Cue.Kill, Foe = f });
    }

    // ---------------------------------------------------------------- the boss

    /// <summary>The depth's boss at <paramref name="b"/> of its fight: it charges in, its health drains with the depth's
    /// progress, and the hero strikes at his own speed until the rules say it's beaten.</summary>
    void Boss(int depth, double b, double dt)
    {
        if (_wave >= 0 && depth == _waveDepth) Fight(1, 1);
        foreach (var f in Foes)
            if (!f.Dead && !f.IsBoss) Fall(f);
        _wave = -1;
        if (_boss == null || _boss.Depth != depth)
        {
            _boss = new Foe
            {
                Id = ++_nextId, Role = Combat.IsGuardian(depth) && Mode != StageMode.Leap ? FoeRole.Guardian : FoeRole.Boss,
                Def = Bestiary.Boss(depth), Depth = depth,
            };
            Foes.Add(_boss);
            Cues.Add(new StageCue { Kind = Cue.BossEnter, Foe = _boss });
            _bossClock = 0;
        }
        _boss.Approach = Math.Min(1, _boss.Approach + dt / 0.45);
        _boss.Hp = Math.Max(0.01, 1 - b);
        if (_boss.Approach < 1) return;
        double interval = 1 / Math.Max(0.2, F.Speed);
        _bossClock += dt;
        if (_boss.WoundUpFor <= _boss.HitsDone && _bossClock >= interval - Lead)
        {
            _boss.WoundUpFor = _boss.HitsDone + 1;
            Cues.Add(new StageCue { Kind = Cue.Windup, Foe = _boss });
        }
        if (_bossClock >= interval)
        {
            _bossClock -= interval;
            _boss.HitsDone++;
            Blow(_boss);
        }
    }

    // ---------------------------------------------------------------- blows both ways

    /// <summary>One blow of the hero's, as damage (LE): his damage per second, taken back to a single ordinary blow.</summary>
    public double BlowLe => F.Hero.Dps - S.Le(1 + F.Hero.Crit * F.CritDamage) - S.Le(1 + F.Hero.Multi) - S.Le(Math.Max(0.05, F.Speed));

    void Blow(Foe f)
    {
        double le = BlowLe;
        bool crit = _rng.Chance(F.Hero.Crit);
        if (crit) le += S.Le(1 + F.CritDamage);
        bool overkill = !f.IsBoss && f.Hits == 1 && le - F.MonsterHpLe > S.Le(10);
        Cues.Add(new StageCue { Kind = Cue.Hit, Foe = f, Le = le, Crit = crit, Overkill = overkill });
        if (_rng.Chance(F.Hero.Multi)) Cues.Add(new StageCue { Kind = Cue.Hit, Foe = f, Le = BlowLe, Twice = true });
    }

    /// <summary>Monsters face to face with the hero strike in turn, a blow every <c>MonsterInterval</c> among them;
    /// health comes back with regeneration and leech, and all of it between waves (the rules weigh each wave and each
    /// boss on its own). The rules keep a push from taking it all, and so does this.</summary>
    void Defend(double dt)
    {
        HeroHp = Math.Min(1, HeroHp + Math.Max(0, F.Hero.Sustain) * dt);
        Foe front = null;
        int engaged = 0;
        foreach (var f in Foes)
        {
            if (f.Dead || f.Approach < 1) continue;
            engaged++;
            if (front == null || f.IsBoss) front = f;
        }
        if (front == null)
        {
            // A breather: back to full over the walk to the next wave.
            HeroHp = Math.Min(1, HeroHp + dt / Math.Max(0.2, P.WaveGap));
            _swipeClock = 0;
            return;
        }
        _swipeClock += dt * engaged;
        if (_swipeClock < P.MonsterInterval) return;
        _swipeClock -= P.MonsterInterval;
        double atk = F.MonsterDpsLe + S.Le(P.MonsterInterval) + front.Role switch
        {
            FoeRole.Guardian => S.Le(P.GuardianAtk),
            FoeRole.Boss => S.Le(P.BossAtk),
            _ => 0,
        };
        var cue = new StageCue { Kind = Cue.Swipe, Foe = front };
        if (_rng.Chance(F.Hero.Dodge)) cue.Dodged = true;
        else
        {
            cue.Blocked = _rng.Chance(F.Hero.Block);
            cue.Le = atk + S.Le(Math.Max(1e-9, 1 - F.Hero.Armor)) + (cue.Blocked ? S.Le(1 - P.BlockAmount) : 0);
            double share = S.Pow(Math.Min(60, cue.Le - F.Hero.Hp));
            HeroHp = Math.Max(0.06, HeroHp - share);
        }
        Cues.Add(cue);
    }
}
