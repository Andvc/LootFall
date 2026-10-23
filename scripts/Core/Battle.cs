using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>One side of the fight: the knight, or a monster.</summary>
public sealed class Fighter
{
    static int _nextId;

    public int Id { get; } = ++_nextId;
    public bool IsHero { get; init; }
    public MonsterDef Monster { get; init; }
    public bool Boss { get; init; }
    public bool ZoneBoss { get; init; }
    public bool Mimic { get; init; }
    public string Name { get; init; } = "";

    public double MaxHp, Hp, Atk, Interval;

    /// <summary>Seconds until its next attack (counts down only while it can act).</summary>
    public double Timer;

    /// <summary>Seconds still walking in (it can't act or be hit until then), stunned, and the knight's barrier.</summary>
    public double Arrive, Stunned, Barrier;

    public bool SecondWindUsed;

    /// <summary>Damage still to come from burns: (per second, seconds left).</summary>
    public readonly List<(double Dps, double Left)> Burns = new();

    public bool Alive => Hp > 0;
    public bool Ready => Alive && Arrive <= 0;
    public double HpShare => MaxHp <= 0 ? 0 : Math.Clamp(Hp / MaxHp, 0, 1);
}

public enum HitKind { Normal, Double, Thunder, Burn, Cleave, Meteor, Thorns, Execute, Monster }

/// <summary>Things that happened in the battle, in order; the screen animates them and the game state pays out for them.</summary>
public abstract record BattleEvent : GameEvent;

public sealed record WaveSpawned(IReadOnlyList<Fighter> Foes, bool Boss, bool Mimic) : BattleEvent;
public sealed record AttackStarted(Fighter Attacker, bool Second) : BattleEvent;
public sealed record HitLanded(Fighter Attacker, Fighter Target, double Amount, HitKind Kind, bool Crit) : BattleEvent;
public sealed record Dodged(Fighter Attacker) : BattleEvent;
public sealed record Healed(Fighter Target, double Amount, Effect Source) : BattleEvent;
public sealed record BarrierRaised(double Amount) : BattleEvent;
public sealed record StunnedFoe(Fighter Target) : BattleEvent;
public sealed record MeteorFalling(IReadOnlyList<Fighter> Targets) : BattleEvent;
public sealed record FighterDied(Fighter Fighter) : BattleEvent;
public sealed record HeroDown : BattleEvent;
public sealed record HeroRevived : BattleEvent;
public sealed record BossTimedOut(Fighter Boss) : BattleEvent;

/// <summary>
/// The fight, stepped in small slices of time: the knight against a wave of monsters or a boss. It knows nothing of
/// stages or loot; <see cref="GameState"/> starts waves and pays out when things die. The same code runs on screen and
/// in the simulation.
/// </summary>
public sealed class Battle
{
    readonly Rng _rng;
    readonly List<(double At, Action Act)> _pending = new();
    double _now, _holy, _bulwark;
    int _swings;

    public Fighter Hero { get; }
    public List<Fighter> Foes { get; } = new();
    public HeroStats Stats { get; private set; }
    public int Stage { get; private set; }
    public bool BossFight { get; private set; }
    public double BossTimeLeft { get; private set; }

    /// <summary>Seconds until the knight gets up again (0 while he's standing).</summary>
    public double DownFor { get; private set; }

    public readonly List<BattleEvent> Events = new();

    public Battle(Rng rng, HeroStats stats)
    {
        _rng = rng;
        Stats = stats;
        Hero = new Fighter { IsHero = true, Name = "Knight", MaxHp = stats.Hp, Hp = stats.Hp };
        _holy = Effects.HolyEvery;
        _bulwark = Effects.BulwarkEvery;
    }

    public bool InFight => FirstAlive() != null;

    /// <summary>New gear: attack changes at once; max health changes, and a bigger one fills in the difference.</summary>
    public void SetStats(HeroStats stats)
    {
        Stats = stats;
        double gain = stats.Hp - Hero.MaxHp;
        Hero.MaxHp = stats.Hp;
        if (Hero.Alive) Hero.Hp = Math.Clamp(Hero.Hp + Math.Max(0, gain), 1, Hero.MaxHp);
    }

    /// <summary>Sends in a wave (or a boss), at this stage's strength.</summary>
    public void Spawn(IEnumerable<MonsterDef> monsters, int stage, bool boss = false, bool zoneBoss = false, bool mimic = false, string bossName = null)
    {
        Foes.Clear();
        _pending.Clear();
        Stage = stage;
        BossFight = boss;
        double scale = Balance.MonsterScale(stage);
        double hpMult = boss ? zoneBoss ? Balance.ZoneBossHp : Balance.BossHp : mimic ? Balance.MimicHp : 1;
        double atkMult = boss ? zoneBoss ? Balance.ZoneBossAtk : Balance.BossAtk : mimic ? Balance.MimicAtk : 1;
        foreach (var m in monsters)
        {
            var f = new Fighter
            {
                Monster = m, Boss = boss, ZoneBoss = zoneBoss, Mimic = mimic, Name = bossName ?? m.Name,
                MaxHp = Balance.MonsterHp * scale * m.Hp * hpMult,
                Atk = Balance.MonsterAtk * scale * m.Atk * atkMult,
                Interval = m.Interval,
                Arrive = Balance.ArriveTime + Balance.ArriveStagger * Foes.Count,
            };
            f.Hp = f.MaxHp;
            // Monsters don't all swing at once: each waits a little before its first attack.
            f.Timer = m.Interval * _rng.Range(0.5, 1.0);
            Foes.Add(f);
        }
        if (boss)
        {
            BossTimeLeft = Balance.BossTime;
            Hero.Hp = Hero.MaxHp;
            Hero.SecondWindUsed = false;
        }
        // Swiftness: the first blow on a wave comes as soon as the wave arrives.
        Hero.Timer = Stats.Has(Effect.Swiftness) ? 0 : Math.Max(Hero.Timer, 0.2);
        Events.Add(new WaveSpawned(Foes.ToList(), boss, mimic));
    }

    /// <summary>Puts a wave back as it came in (the knight fell, so it starts over when he's up).</summary>
    public void ResetWave()
    {
        for (int i = 0; i < Foes.Count; i++)
        {
            var f = Foes[i];
            f.Hp = f.MaxHp;
            f.Burns.Clear();
            f.Stunned = 0;
            f.Arrive = Balance.ArriveTime + Balance.ArriveStagger * i;
        }
        _pending.Clear();
    }

    public void Step(double dt)
    {
        _now += dt;
        RunPending();

        if (DownFor > 0)
        {
            DownFor -= dt;
            if (DownFor <= 0)
            {
                DownFor = 0;
                Hero.Hp = Hero.MaxHp;
                Hero.SecondWindUsed = false;
                ResetWave();
                Events.Add(new HeroRevived());
            }
            return;
        }

        var ready = _ready;
        ready.Clear();
        foreach (var f in Foes)
        {
            if (f.Ready) ready.Add(f);
            else if (f.Alive && f.Arrive > 0) f.Arrive -= dt;
        }

        // Out of a fight the knight catches his breath; in one, regeneration and the timed effects tick.
        if (ready.Count == 0) Heal(Hero.MaxHp * Balance.RestHeal * dt, Effect.None, quiet: true);
        else
        {
            Heal(Hero.MaxHp * Stats.Regen * dt, Effect.None, quiet: true);
            TickEffects(dt);
        }

        Burn(dt);

        if (BossFight && ready.Count > 0)
        {
            BossTimeLeft -= dt;
            if (BossTimeLeft <= 0)
            {
                var boss = FirstAlive();
                if (boss != null) Events.Add(new BossTimedOut(boss));
                return;
            }
        }

        // The knight swings at the foe in front.
        if (ready.Count > 0)
        {
            Hero.Timer -= dt;
            if (Hero.Timer <= 0)
            {
                Hero.Timer += 1 / Stats.Speed;
                if (Hero.Timer < 0) Hero.Timer = 0;
                Swing(false);
                if (_rng.Chance(Stats.Double)) Schedule(Balance.DoubleDelay, () => Swing(true));
            }
        }
        else Hero.Timer = Math.Min(Hero.Timer, 0.25);

        // Every monster that's arrived attacks on its own clock.
        foreach (var f in ready)
        {
            if (f.Stunned > 0)
            {
                f.Stunned -= dt;
                continue;
            }
            f.Timer -= dt;
            if (f.Timer > 0) continue;
            f.Timer += f.Interval;
            var attacker = f;
            Events.Add(new AttackStarted(attacker, false));
            Schedule(Balance.MonsterWindup, () => MonsterHits(attacker));
        }
    }

    readonly List<Fighter> _ready = new();

    /// <summary>The foe in front: the first one that's arrived and still standing.</summary>
    Fighter Front
    {
        get
        {
            foreach (var f in Foes) if (f.Ready) return f;
            return null;
        }
    }

    Fighter FirstAlive()
    {
        foreach (var f in Foes) if (f.Alive) return f;
        return null;
    }

    void Swing(bool second)
    {
        if (Front == null || !Hero.Alive) return;
        Events.Add(new AttackStarted(Hero, second));
        Schedule(Balance.HeroWindup, () =>
        {
            var target = Front;
            if (target == null || !Hero.Alive) return;
            HeroHits(target, second);
        });
    }

    void HeroHits(Fighter target, bool second)
    {
        _swings++;
        bool crit = _rng.Chance(Stats.Crit);
        double dmg = Stats.Atk * _rng.Range(0.9, 1.1) * (crit ? Stats.CritDmg : 1);
        Damage(target, dmg, second ? HitKind.Double : HitKind.Normal, crit);

        if (Stats.Has(Effect.Ember) && target.Alive)
            target.Burns.Add((dmg * Effects.EmberShare * Stats.Mag(Effect.Ember) / 2, 2));
        if (Stats.Has(Effect.Cleave))
            foreach (var other in Foes.Where(f => f.Ready && f != target).ToList())
                Damage(other, dmg * Math.Min(1, Effects.CleaveShare * Stats.Mag(Effect.Cleave)), HitKind.Cleave, crit);
        if (Stats.Has(Effect.Thunder) && target.Alive && _rng.Chance(Effects.ThunderChance))
            Damage(target, Stats.Atk * Effects.ThunderDamage * Stats.Mag(Effect.Thunder), HitKind.Thunder, false);
        if (Stats.Has(Effect.Meteor) && _swings % Effects.MeteorEvery == 0)
        {
            var targets = Foes.Where(f => f.Ready).ToList();
            Events.Add(new MeteorFalling(targets));
            double meteor = Stats.Atk * Effects.MeteorDamage * Stats.Mag(Effect.Meteor);
            Schedule(0.35, () =>
            {
                foreach (var t in targets) if (t.Alive) Damage(t, meteor, HitKind.Meteor, false);
            });
        }
        if (Stats.Has(Effect.Executioner) && target.Alive && !target.Boss &&
            target.HpShare < Effects.ExecuteBelow * Stats.Mag(Effect.Executioner))
            Damage(target, target.Hp, HitKind.Execute, false);
        if (target.Alive && _rng.Chance(Stats.Stun))
        {
            target.Stunned = 1.0;
            Events.Add(new StunnedFoe(target));
        }
    }

    void MonsterHits(Fighter f)
    {
        if (!f.Alive || !Hero.Alive || DownFor > 0) return;
        if (_rng.Chance(Stats.Dodge))
        {
            Events.Add(new Dodged(f));
            return;
        }
        double hit = f.Atk * _rng.Range(0.9, 1.1);
        hit *= 1 - Stats.DefCut(hit);
        if (Hero.Barrier > 0)
        {
            double absorbed = Math.Min(Hero.Barrier, hit);
            Hero.Barrier -= absorbed;
            hit -= absorbed;
        }
        Hero.Hp -= hit;
        Events.Add(new HitLanded(f, Hero, hit, HitKind.Monster, false));
        if (Stats.Has(Effect.Thorns) && hit > 0)
            Damage(f, hit * Effects.ThornsShare * Stats.Mag(Effect.Thorns), HitKind.Thorns, false);
        if (Hero.Hp <= 0 && Stats.Has(Effect.SecondWind) && !Hero.SecondWindUsed)
        {
            Hero.Hp = 1;
        }
        if (Hero.Hp > 0 && Hero.HpShare < 0.3 && Stats.Has(Effect.SecondWind) && !Hero.SecondWindUsed)
        {
            Hero.SecondWindUsed = true;
            Heal(Hero.MaxHp * Effects.SecondWindHeal * Stats.Mag(Effect.SecondWind), Effect.SecondWind);
        }
        if (Hero.Hp <= 0)
        {
            Hero.Hp = 0;
            DownFor = Balance.ReviveTime;
            _pending.Clear();
            Events.Add(new FighterDied(Hero));
            Events.Add(new HeroDown());
        }
    }

    void Damage(Fighter target, double amount, HitKind kind, bool crit)
    {
        if (!target.Alive || amount <= 0) return;
        amount = Math.Min(amount, target.Hp);
        target.Hp -= amount;
        Events.Add(new HitLanded(Hero, target, amount, kind, crit));
        if (kind != HitKind.Thorns && kind != HitKind.Burn && Stats.Lifesteal > 0)
            Heal(amount * Stats.Lifesteal, Effect.Vampire, quiet: true);
        if (target.Hp <= 1e-9)
        {
            target.Hp = 0;
            target.Burns.Clear();
            Events.Add(new FighterDied(target));
        }
    }

    void Heal(double amount, Effect source, bool quiet = false)
    {
        if (!Hero.Alive || amount <= 0 || Hero.Hp >= Hero.MaxHp) return;
        amount = Math.Min(amount, Hero.MaxHp - Hero.Hp);
        Hero.Hp += amount;
        if (!quiet) Events.Add(new Healed(Hero, amount, source));
    }

    void TickEffects(double dt)
    {
        if (Stats.Has(Effect.HolyLight))
        {
            _holy -= dt;
            if (_holy <= 0)
            {
                _holy += Effects.HolyEvery;
                Heal(Hero.MaxHp * Effects.HolyHeal * Stats.Mag(Effect.HolyLight), Effect.HolyLight);
            }
        }
        if (Stats.Has(Effect.Bulwark))
        {
            _bulwark -= dt;
            if (_bulwark <= 0)
            {
                _bulwark += Effects.BulwarkEvery;
                Hero.Barrier = Math.Max(Hero.Barrier, Hero.MaxHp * Effects.BulwarkShare * Stats.Mag(Effect.Bulwark));
                Events.Add(new BarrierRaised(Hero.Barrier));
            }
        }
    }

    double _burnTick;

    void Burn(double dt)
    {
        _burnTick += dt;
        if (_burnTick < 0.5) return;
        double tick = _burnTick;
        _burnTick = 0;
        foreach (var f in Foes)
        {
            if (!f.Alive || f.Burns.Count == 0) continue;
            double total = 0;
            for (int i = f.Burns.Count - 1; i >= 0; i--)
            {
                var (dps, left) = f.Burns[i];
                double t = Math.Min(tick, left);
                total += dps * t;
                if (left - tick <= 0) f.Burns.RemoveAt(i);
                else f.Burns[i] = (dps, left - tick);
            }
            Damage(f, total, HitKind.Burn, false);
        }
    }

    void Schedule(double delay, Action act) => _pending.Add((_now + delay, act));

    void RunPending()
    {
        for (int guard = 0; guard < 64 && _pending.Count > 0; guard++)
        {
            int due = -1;
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].At <= _now)
                {
                    due = i;
                    break;
                }
            if (due < 0) return;
            var act = _pending[due].Act;
            _pending.RemoveAt(due);
            act();
        }
    }
}
