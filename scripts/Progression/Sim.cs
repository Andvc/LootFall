using System;
using System.Collections.Generic;

namespace LootFall.Progression;

/// <summary>How a simulated player plays: how often they check in (equipping is automatic, like the game's auto-equip),
/// and whether they use the prestige layers.</summary>
public sealed class Profile
{
    public string Name = "active";
    /// <summary>At most this many seconds between decisions (talents, forge, ranks, focus, prestige); 0 means every step.
    /// Check-ins are a tenth of the time played apart until they reach it: everyone plays the first hour.</summary>
    public double CheckIn;
    public bool Ascends = true, Transcends = true;

    public static Profile Active() => new() { Name = "active", CheckIn = 0 };
    public static Profile Idle() => new() { Name = "idle", CheckIn = 4 * 3600 };
    public static Profile NoPrestige() => new() { Name = "no-prestige", CheckIn = 0, Ascends = false, Transcends = false };
}

/// <summary>
/// The long-horizon simulator: one hero, played by a sensible policy, for thousands of hours, in steps that grow from a
/// second to ten minutes. Fights are the closed form in <see cref="Combat"/>, drops are sampled in aggregate (a Poisson
/// count per slot and rarity, and the best quality of n drops in one draw), so a 5,000-hour run takes seconds.
/// </summary>
public sealed class Sim
{
    public readonly Hero H;
    public readonly Profile Pr;
    public readonly Metrics M;
    readonly Params P;
    readonly Rng _rng;
    readonly double[] _w = new double[Stats.Count];
    readonly Sheet _old = new(), _new = new();
    readonly Item _cand = new(), _best = new();

    public double Time;
    double _nextCheck, _runStart, _cycleStart, _depthProgress;
    /// <summary>Aether this run has earned so far (paid out by an ascension).</summary>
    Big _pendingAether = Big.Zero;
    int _ascensionsThisCycle;

    public Sim(Params p, Content c, TalentTree tree, Profile profile, int seed)
    {
        P = p;
        Pr = profile;
        _rng = new Rng(seed);
        H = new Hero(p, c, tree);
        M = new Metrics(this);
        UpdateWeights();
    }

    public double Hours => Time / 3600;

    public void Run(double hours, double maxStep = 600)
    {
        double end = hours * 3600;
        M.Snapshot(force: true);
        while (Time < end)
        {
            double dt = Math.Clamp(Time / 400, 1, maxStep);
            dt = Math.Min(dt, end - Time);
            Step(dt);
            Time += dt;
            M.AfterStep(dt);
        }
        M.Snapshot(force: true);
    }

    // ---------------------------------------------------------------- one step

    void Step(double dt)
    {
        var b = H.Base;
        double timeLeft = dt;
        double pushKills = 0, farmKills = 0;
        Big gold = Big.Zero, xp = Big.Zero;
        int pushTop = H.RunDepth;

        // Push: clear the next depth (its kills and boss) while it can be beaten and time remains.
        for (int guard = 0; guard < 20000 && timeLeft > 0; guard++)
        {
            int next = H.RunDepth + 1;
            if (!H.Fight.CanClear(H.Total, b, next)) { _depthProgress = 0; break; }
            var f = H.Fight.At(H.Total, b, next);
            double rate = H.Fight.KillRate(f, next);
            bool guardian = Combat.IsGuardian(next);
            double need = P.KillsPerDepth / rate + Math.Min(P.BossTime, H.Fight.Boss(f, next, guardian).Time);
            // Overkill: clear several depths in one go (checking the last one can be cleared at all).
            int skip = _depthProgress > 0 ? 1 : H.Fight.Skip(f, next);
            if (skip > 1 && !H.Fight.CanClear(H.Total, b, next + skip - 1)) skip = 1;
            if (skip > 1)
            {
                next += skip - 1;
                need = H.Fight.SkipTime;
                guardian = false;
            }
            double share = Math.Min(1 - _depthProgress, timeLeft / need);
            _depthProgress += share;
            timeLeft -= share * need;
            double k = share * (P.KillsPerDepth + P.BossReward * (guardian ? 2 : 1));
            pushKills += k;
            gold += H.GoldPerKill(next) * k;
            xp += H.XpPerKill(next) * k;
            _pendingAether += AetherPerKill(next) * k;
            if (_depthProgress < 1 - 1e-9) break;
            _depthProgress = 0;
            H.RunDepth = next;
            pushTop = next;
            if (next > H.EverDepth)
            {
                H.EverDepth = next;
                M.DepthRecord(next);
                int opened = H.UpdateUnlocks();
                if (opened > 0) M.Count(Ev.SlotUnlock, opened);
            }
        }

        // Farm with the rest: the deepest comfortable depth reached this run.
        int farm = H.Fight.FarmDepth(H.Total, b, H.RunDepth);
        if (timeLeft > 0)
        {
            var f = H.Fight.At(H.Total, b, farm);
            farmKills = H.Fight.KillRate(f, farm) * timeLeft;
            gold += H.GoldPerKill(farm) * farmKills;
            xp += H.XpPerKill(farm) * farmKills;
            _pendingAether += AetherPerKill(farm) * farmKills;
        }
        M.Kills += pushKills + farmKills;
        M.FarmDepth = farm;

        H.Gold += gold;
        Gain(xp);

        // Loot: auto-equip strict upgrades, salvage the rest, note first finds.
        bool gearChanged = false;
        double quantity = P.DropChance * (1 + H.Total[Stat.Quantity]);
        if (farmKills > 0) gearChanged |= Drops(farmKills * quantity, farm);
        if (pushKills > 0) gearChanged |= Drops(pushKills * quantity, pushTop);

        // Mastery: worn slots learn with time, focused ones faster.
        double gain = dt / 3600 * P.MasteryRate * H.MasteryGain;
        for (int s = 0; s < H.Gear.Length; s++)
        {
            if (!H.Open[s] || H.Gear[s] == null) continue;
            H.MasteryXp[s] += gain * (H.Focus[s] ? P.MasteryFocus : 1);
            while (H.MasteryXp[s] >= H.MasteryNeed(s))
            {
                H.MasteryXp[s] -= H.MasteryNeed(s);
                H.Mastery[s]++;
                gearChanged = true;
                M.Count(Ev.Mastery);
            }
        }

        if (gearChanged) H.Rebuild();

        // Decisions, at check-ins.
        if (Time >= _nextCheck)
        {
            _nextCheck = Time + Math.Min(Pr.CheckIn, Math.Max(60, Time / 10));
            Decide();
        }
    }

    void Gain(Big xp)
    {
        H.Xp += xp;
        for (int guard = 0; guard < 100000; guard++)
        {
            var need = H.XpToNext();
            if (H.Xp < need) break;
            H.Xp -= need;
            H.Level++;
            H.Talents.Points++;
            M.Count(Ev.Level);
        }
    }

    // ---------------------------------------------------------------- loot

    /// <summary>Samples <paramref name="count"/> drops at item level <paramref name="ilvl"/>: a Poisson count per slot,
    /// then per rarity; only the best couple of candidates per slot are rolled. Returns whether gear changed.</summary>
    bool Drops(double count, double ilvl)
    {
        if (count <= 0 || H.OpenCount == 0) return false;
        var odds = H.RarityOdds();
        double perSlot = count / H.OpenCount;
        double materials = 0;
        bool changed = false;
        for (int s = 0; s < H.Gear.Length; s++)
        {
            if (!H.Open[s]) continue;
            long n = _rng.Poisson(perSlot);
            if (n == 0) continue;
            double bestScore = 0;
            bool any = false;
            int tiersRolled = 0;
            long higher = 0;
            _old.Clear();
            if (H.Gear[s] != null) H.Contribute(s, H.Gear[s], _old);
            for (int r = H.MaxRarity; r >= 0; r--)
            {
                long c = r == 0 ? Math.Max(0, n - higher) : _rng.Poisson(n * odds[r]);
                higher += c;
                if (c <= 0) continue;
                materials += c * H.MaterialPerItem(r);
                int found = H.Discover(_rng, s, r, c, ilvl);
                if (found > 0) M.Count(Ev.Codex, found);
                if (tiersRolled >= 2) continue;
                tiersRolled++;
                for (int i = 0; i < Math.Min(2, c); i++)
                {
                    double q = i == 0 ? _rng.MaxOfUniform(c) : _rng.NextDouble();
                    H.Roll(_rng, s, ilvl, r, q, _cand);
                    double score = Score(s, _cand);
                    if (H.Gear[s] == null && !any || score > bestScore)
                    {
                        bestScore = score;
                        _best.CopyFrom(_cand);
                        any = true;
                    }
                }
            }
            if (any && (H.Gear[s] == null || bestScore > 1e-9))
            {
                H.Gear[s] ??= new Item();
                H.Gear[s].CopyFrom(_best);
                changed = true;
                M.Upgrade(bestScore);
            }
        }
        H.Materials += materials;
        if (changed) H.RebuildGear();
        return changed;
    }

    /// <summary>How much reach an item adds over what the slot wears (stat weights times the exact change in each total).</summary>
    double Score(int s, Item it)
    {
        _new.Clear();
        H.Contribute(s, it, _new);
        double score = 0;
        var t = H.Total.V;
        for (int i = 0; i < Stats.Count; i++)
        {
            double o = _old.V[i], n = _new.V[i];
            if (Stats.IsFlat((Stat)i))
            {
                if (o <= Scale.None && n <= Scale.None) continue;
                double after = H.S.Add(H.S.Sub(t[i], o), n);
                score += _w[i] * (after - t[i]);
            }
            else score += _w[i] * (n - o);
        }
        return score;
    }

    // ---------------------------------------------------------------- weights

    static double Probe(Stat s) => s switch
    {
        Stat.Regen => 0.002,
        Stat.Leech => 0.004,
        Stat.AttackSpeed => 0.02,
        _ when Stats.IsPercent(s) => 0.05,
        _ => 0.25,
    };

    /// <summary>The reach a unit of each stat is worth now, by finite differences. Utility stats (income only) are worth
    /// UtilityValue of the same multiplier on damage and health.</summary>
    public void UpdateWeights()
    {
        double r0 = Objective(H.Base);
        bool stuck = H.Reach() <= 1.0001;
        if (stuck) r0 = PowerAtOne(H.Base);
        for (int i = 0; i < Stats.Count; i++)
        {
            var s = (Stat)i;
            if (Stats.IsUtility(s)) continue;
            double d = Probe(s);
            H.Total.V[i] += d;
            var b = H.Fight.Prepare(H.Total);
            double r1 = stuck ? PowerAtOne(b) : Objective(b);
            H.Total.V[i] -= d;
            _w[i] = Math.Max(0, (r1 - r0) / d);
        }
        double more = 0.5 * (_w[(int)Stat.MoreDamage] + _w[(int)Stat.MoreHp]);
        for (int i = (int)Stat.GoldFind; i <= (int)Stat.Quantity; i++)
            _w[i] = P.UtilityValue * more / ((1 + Math.Max(0, H.Total.V[i])) * H.S.LnR);
        WeightMore = more;
    }

    /// <summary>What the policy maximizes: half how deep the hero can push, half how deep they farm comfortably (kill speed
    /// is income, so offense matters even when a push is held back by defense).</summary>
    double Objective(in Combat.Base b) => 0.5 * (H.Fight.Reach(H.Total, b) + H.Fight.FarmReach(H.Total, b));

    /// <summary>LE per unit of more damage and health (reach per LE), for valuing prestige perks.</summary>
    public double WeightMore { get; private set; }

    /// <summary>Before the hero can clear depth 1: offense plus defense at depth 1 stands in for reach.</summary>
    double PowerAtOne(in Combat.Base b)
    {
        var f = H.Fight.At(H.Total, b, 1);
        return 0.5 * (f.Dps + f.Hp);
    }

    public double[] Weights => _w;

    // ---------------------------------------------------------------- decisions

    void Decide()
    {
        UpdateWeights();
        H.Talents.Allocate(_w, H.S);
        int notes = H.Talents.AllocatedCount;
        M.Talents(notes, H.Talents.Paragon);
        bool changed = SpendForge() | SpendRanks();
        ChooseFocus();
        if (changed) H.Rebuild();
        else H.Rebuild(gear: false);
        if (Pr.Transcends && TryTranscend()) return;
        if (Pr.Ascends) TryAscend();
    }

    /// <summary>The reach one more step of a slot's flat stats (forge or rank) is worth, per LE of step.</summary>
    double SlotValue(int s)
    {
        if (H.Gear[s] == null) return 0;
        _new.Clear();
        H.Contribute(s, H.Gear[s], _new);
        double v = 0;
        var t = H.Total.V;
        for (int i = 0; i <= (int)Stat.MultiHit; i++)
        {
            if (_new.V[i] <= Scale.None) continue;
            v += _w[i] * H.S.Pow(_new.V[i] - t[i]);
        }
        return v;
    }

    readonly PriorityQueue<int, double> _heap = new();

    bool SpendForge()
    {
        bool bought = false;
        for (int round = 0; round < 50; round++)
        {
            _heap.Clear();
            double step = H.S.Le(1 + P.ForgeStep);
            for (int s = 0; s < H.Gear.Length; s++)
            {
                if (!H.Open[s] || H.Gear[s] == null) continue;
                double v = SlotValue(s) * step;
                if (v <= 0) continue;
                _heap.Enqueue(s, -(Math.Log10(v) - H.ForgeCost(s).Log10));
            }
            int boughtThisRound = 0;
            while (_heap.Count > 0 && boughtThisRound < 64)
            {
                int s = _heap.Dequeue();
                var cost = H.ForgeCost(s);
                if (cost > H.Gold) break;
                H.Gold -= cost;
                H.Forge[s]++;
                boughtThisRound++;
                M.Count(Ev.Forge);
                double v = SlotValue(s) * step;
                if (v > 0) _heap.Enqueue(s, -(Math.Log10(v) - H.ForgeCost(s).Log10));
            }
            if (boughtThisRound == 0) break;
            bought = true;
            H.Rebuild();
        }
        return bought;
    }

    bool SpendRanks()
    {
        bool bought = false;
        for (int round = 0; round < 20; round++)
        {
            _heap.Clear();
            for (int s = 0; s < H.Gear.Length; s++)
            {
                if (!H.Open[s] || H.Gear[s] == null) continue;
                double v = SlotValue(s) * P.RankLE;
                if (v <= 0) continue;
                _heap.Enqueue(s, -(v / H.RankCost(s)));
            }
            int boughtThisRound = 0;
            while (_heap.Count > 0 && boughtThisRound < 64)
            {
                int s = _heap.Dequeue();
                double cost = H.RankCost(s);
                if (cost > H.Materials) break;
                H.Materials -= cost;
                H.Rank[s]++;
                boughtThisRound++;
                M.Count(Ev.Rank);
                double v = SlotValue(s) * P.RankLE;
                if (v > 0) _heap.Enqueue(s, -(v / H.RankCost(s)));
            }
            if (boughtThisRound == 0) break;
            bought = true;
            H.Rebuild();
        }
        return bought;
    }

    void ChooseFocus()
    {
        Array.Clear(H.Focus);
        _heap.Clear();
        for (int s = 0; s < H.Gear.Length; s++)
            if (H.Open[s] && H.Gear[s] != null) _heap.Enqueue(s, -SlotValue(s));
        for (int k = 0; k < H.FocusSlots && _heap.Count > 0; k++) H.Focus[_heap.Dequeue()] = true;
    }

    // ---------------------------------------------------------------- prestige

    /// <summary>Aether a kill at depth d earns (nothing above the ascension depth): AetherPerKill (d / AscendDepth)^AetherExp.
    /// Deep kills earn far more, so the run's aether rate climbs with it and plateaus where the hero stalls.</summary>
    Big AetherPerKill(double d) => d < P.AscendDepth ? Big.Zero
        : Big.Of(P.AetherPerKill) * Big.Pow(d / P.AscendDepth, P.AetherExp) * (1 + H.Transcendence.PerkTotal(Perk.AetherGain));

    /// <summary>Aether an ascension would pay now (what this run has earned).</summary>
    public Big AetherNow() => H.RunDepth >= P.AscendDepth || H.EverDepth >= P.AscendDepth ? _pendingAether : Big.Zero;

    public Big StardustNow()
    {
        if (_ascensionsThisCycle < P.TransMinAscensions || H.Ascension.CycleEarned < Big.Of(P.TransAether)) return Big.Zero;
        return Big.Of(P.StardustBase) * Big.Pow10((H.Ascension.CycleEarned.Log10 - Math.Log10(P.TransAether)) * P.StardustExp);
    }

    void TryAscend()
    {
        var gain = AetherNow();
        if (gain < Big.Of(P.AscendMinAether)) return;
        // Like a player: reset once it would double this cycle's aether, or once the run has gone on a while (a tenth of
        // all the time played, at least an hour) and pays a quarter of it.
        double runTime = Time - _runStart;
        bool doubles = gain >= H.Ascension.CycleEarned;
        bool long_ = runTime >= Math.Max(3600, 0.1 * Time) && gain >= H.Ascension.CycleEarned * 0.25;
        if (doubles || long_) Ascend(gain);
    }

    void Ascend(Big gain)
    {
        var a = H.Ascension;
        a.Currency += gain;
        a.CycleEarned += gain;
        a.Lifetime += gain;
        a.Resets++;
        _ascensionsThisCycle++;
        M.Prestige(Ev.Ascension, Time - _runStart, H.RunDepth);
        ResetRun();
        SpendLayer(a);
        H.UpdateUnlocks();
        H.Rebuild();
    }

    bool TryTranscend()
    {
        var gain = StardustNow();
        if (gain < Big.Of(P.TransMinStardust)) return false;
        // Transcend once it would double all the stardust ever earned (cycles lengthen as the total grows).
        if (gain < H.Transcendence.Lifetime) return false;
        var t = H.Transcendence;
        t.Currency += gain;
        t.CycleEarned += gain;
        t.Lifetime += gain;
        t.Resets++;
        M.Prestige(Ev.Transcendence, Time - _cycleStart, H.RunDepth);
        H.Ascension.Wipe(H.S);
        _ascensionsThisCycle = 0;
        _cycleStart = Time;
        ResetRun();
        SpendLayer(t);
        H.UpdateUnlocks();
        H.Rebuild();
        return true;
    }

    /// <summary>A new run: back to the start depth (deeper with Head Start, with basic gear there), no gear, forge or gold.</summary>
    void ResetRun()
    {
        double head = Math.Min(0.5, H.Ascension.PerkTotal(Perk.HeadStart));
        int start = Math.Max(1, (int)(H.EverDepth * head));
        H.RunDepth = start;
        _depthProgress = 0;
        H.Gold = Big.Zero;
        double keep = Math.Min(0.5, H.Ascension.PerkTotal(Perk.ForgeKeep));
        for (int s = 0; s < H.Forge.Length; s++) H.Forge[s] = (int)(H.Forge[s] * keep);
        for (int s = 0; s < H.Gear.Length; s++)
        {
            H.Gear[s] = null;
            if (start > 1 && H.Open[s])
            {
                H.Gear[s] = new Item();
                H.Roll(_rng, s, start, Math.Min(2, H.MaxRarity), 0.5, H.Gear[s]);
            }
        }
        _runStart = Time;
        _pendingAether = Big.Zero;
        H.Rebuild();
        UpdateWeights();
    }

    /// <summary>Buys a layer's ranks: the best value per cost first, while affordable.</summary>
    void SpendLayer(LayerState layer)
    {
        H.Rebuild();
        UpdateWeights();
        for (int guard = 0; guard < 10000; guard++)
        {
            LayerNode best = null;
            double bestScore = double.NegativeInfinity;
            foreach (var n in layer.Tree.Nodes)
            {
                if (!layer.CanBuy(n, H.EverDepth)) continue;
                double v = NodeValue(n);
                if (v <= 0) continue;
                double score = Math.Log10(v) - n.Cost(layer.Ranks[n.Id]).Log10;
                if (score > bestScore) { bestScore = score; best = n; }
            }
            if (best == null) break;
            layer.Buy(best, H.S);
            M.Count(Ev.LayerRank);
        }
        H.UpdateUnlocks();
        H.Rebuild();
    }

    /// <summary>What a prestige rank is worth in reach (stats by their weights; perks by what they do to their system).</summary>
    double NodeValue(LayerNode n)
    {
        double more = Math.Max(1e-6, WeightMore);
        switch (n.Perk)
        {
            case Perk.None:
                return n.Amount * _w[(int)n.Stat];
            case Perk.MasteryGain:
            {
                // Mastery level grows like (gain)^(1/(curve+1)); the ranks' share of that.
                double le = MasteryLeTotal();
                return n.Amount / H.MasteryGain * le / (P.MasteryCurve + 1) * more + 0.05 * more;
            }
            case Perk.MaterialGain:
            {
                double le = RankLeTotal();
                return n.Amount / (1 + H.Ascension.PerkTotal(Perk.MaterialGain)) * le / (P.RankExp + 1) * more + 0.05 * more;
            }
            case Perk.AetherGain:
                return n.Amount / (1 + H.Transcendence.PerkTotal(Perk.AetherGain)) * Math.Max(1, H.Ascension.Lifetime.Log10) * more;
            case Perk.CodexPower:
                return n.Amount * H.CodexCount * P.CodexLE * more;
            case Perk.HeadStart:
                return 0.5 * more;
            case Perk.ForgeDiscount:
                return 0.05 * more;
            case Perk.ExtraFocus:
                return 1.0 * more;
            case Perk.ForgeKeep:
            {
                double forge = 0;
                int open = 0;
                for (int s = 0; s < H.Forge.Length; s++) if (H.Open[s]) { forge += H.Forge[s]; open++; }
                return n.Amount * forge / Math.Max(1, open) * H.S.Le(1 + P.ForgeStep) * more;
            }
            case Perk.RarityUnlock:
                return n.Amount > H.MaxRarity ? 100 * more : 0;
            case Perk.SlotUnlock:
                return 20 * more;
            default:
                return 0;
        }
    }

    double MasteryLeTotal()
    {
        double sum = 0;
        int n = 0;
        for (int s = 0; s < H.Mastery.Length; s++)
            if (H.Open[s]) { sum += H.Mastery[s] * P.MasteryLE; n++; }
        return n == 0 ? 0 : sum / n;
    }

    double RankLeTotal()
    {
        double sum = 0;
        int n = 0;
        for (int s = 0; s < H.Rank.Length; s++)
            if (H.Open[s]) { sum += H.Rank[s] * P.RankLE; n++; }
        return n == 0 ? 0 : sum / n;
    }
}
