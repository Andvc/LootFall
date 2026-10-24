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
    /// <summary>A run of this length (seconds) ascends for 5% more depth; one n times as long for 5% over n: the
    /// longer the player has waited, the smaller the gain they'll take.</summary>
    public double Patience = 1800;

    public static Profile Active() => new() { Name = "active", CheckIn = 0 };
    public static Profile Idle() => new() { Name = "idle", CheckIn = 4 * 3600 };
    public static Profile Daily() => new() { Name = "daily", CheckIn = 24 * 3600 };
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
    readonly double[] _forgeScore, _forgeCost;

    public double Time;
    /// <summary>When set, prestige decisions are written here (progsim --debug-prestige).</summary>
    public System.Collections.Generic.List<string> Log;
    double _nextLog;
    double _nextCheck, _runStart, _cycleStart, _depthProgress;
    /// <summary>The run's depth at each decision, to tell climbing from a plateau; the last run's peak and length.</summary>
    readonly List<(double Time, int Depth)> _runDepths = new();
    int _lastPeak;
    double _lastRun;
    /// <summary>Aether this run has earned so far (paid out by an ascension).</summary>
    Big _pendingAether = Big.Zero;

    public Sim(Params p, Content c, TalentTree tree, Profile profile, int seed)
    {
        P = p;
        Pr = profile;
        _rng = new Rng(seed);
        H = new Hero(p, c, tree);
        _forgeScore = new double[c.SlotCount];
        _forgeCost = new double[c.SlotCount];
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

        // Decisions, at check-ins. From the first transcendence on, the autopilot works in between: it buys forge levels
        // and ranks as auto-buyers would and ascends by the player's own rule (the automation milestone that lets idle
        // play keep up).
        if (Time >= _nextCheck)
        {
            _nextCheck = Time + Math.Min(Pr.CheckIn, Math.Max(60, Time / 10));
            Decide();
        }
        else if (H.Transcendence.Resets > 0)
        {
            if (SpendForge() | SpendRanks()) H.Rebuild();
            if (Pr.Ascends && TryPrestige(transcend: false) && (SpendForge() | SpendRanks())) H.Rebuild();
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
        if (Pr.Ascends) TryPrestige(transcend: Pr.Transcends);
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


    /// <summary>
    /// Forge levels, all at once. Costs grow by ForgeGrowth a level and value by only 1 + ForgeStep, so the k-th next
    /// level of slot s is worth log(value/cost) - k * slope (log10 units): the best buys fill every slot up to a common
    /// "water level", which a bisection finds for the gold on hand. That is what buying the best level one at a time
    /// does, without millions of single purchases.
    /// </summary>
    bool SpendForge()
    {
        if (H.Gold.IsZero) return false;
        int n = H.Gear.Length;
        double g = P.ForgeGrowth, slope = Math.Log10(g) - Math.Log10(1 + P.ForgeStep), step = H.S.Le(1 + P.ForgeStep);
        double top = double.NegativeInfinity;
        for (int s = 0; s < n; s++)
        {
            _forgeScore[s] = double.NegativeInfinity;
            if (!H.Open[s] || H.Gear[s] == null) continue;
            double v = SlotValue(s) * step;
            if (v <= 0) continue;
            _forgeCost[s] = H.ForgeCost(s).Log10;
            _forgeScore[s] = Math.Log10(v) - _forgeCost[s];
            top = Math.Max(top, _forgeScore[s]);
        }
        if (top == double.NegativeInfinity) return false;
        int Levels(int s, double level) => _forgeScore[s] < level ? 0 : (int)Math.Floor((_forgeScore[s] - level) / slope) + 1;
        Big Spent(double level)
        {
            var sum = Big.Zero;
            for (int s = 0; s < n; s++)
            {
                int k = Levels(s, level);
                if (k > 0) sum += Big.Pow10(_forgeCost[s]) * ((Big.Pow(g, k) - Big.One) / (g - 1));
            }
            return sum;
        }
        // The highest water level buys only the single best level; lower it until the gold runs out, then bisect.
        double hi = top;
        if (Spent(hi) > H.Gold) return false;
        double drop = slope, lo = top - drop;
        while (Spent(lo) <= H.Gold && drop < 1e7)
        {
            hi = lo;
            drop *= 2;
            lo = top - drop;
        }
        for (int i = 0; i < 50 && hi - lo > 1e-9; i++)
        {
            double mid = 0.5 * (hi + lo);
            if (Spent(mid) <= H.Gold) hi = mid;
            else lo = mid;
        }
        bool bought = false;
        for (int s = 0; s < n; s++)
        {
            int k = Levels(s, hi);
            if (k <= 0) continue;
            H.Gold -= Big.Pow10(_forgeCost[s]) * ((Big.Pow(g, k) - Big.One) / (g - 1));
            H.Forge[s] += k;
            M.Count(Ev.Forge, k);
            bought = true;
        }
        return bought;
    }

    /// <summary>The reach a level of rank or mastery in slot <paramref name="s"/> is worth, per LE.</summary>
    double PoolValue(int s)
    {
        var (d, h) = H.PoolSplit(s);
        return d * _w[(int)Stat.MoreDamage] + h * _w[(int)Stat.MoreHp];
    }

    /// <summary>Ranks: the best value per material first. Every slot's rank is worth about the same, so this evens them out,
    /// cheapest (newest) slots first.</summary>
    bool SpendRanks()
    {
        _heap.Clear();
        for (int s = 0; s < H.Gear.Length; s++)
            if (H.Open[s]) _heap.Enqueue(s, -(PoolValue(s) / H.RankCost(s)));
        bool bought = false;
        while (_heap.Count > 0)
        {
            int s = _heap.Dequeue();
            double cost = H.RankCost(s);
            if (cost > H.Materials) break;
            H.Materials -= cost;
            H.Rank[s]++;
            bought = true;
            M.Count(Ev.Rank);
            _heap.Enqueue(s, -(PoolValue(s) / H.RankCost(s)));
        }
        return bought;
    }

    /// <summary>Focus goes where a level is cheapest for what it's worth: the least-mastered worn slots.</summary>
    void ChooseFocus()
    {
        Array.Clear(H.Focus);
        _heap.Clear();
        for (int s = 0; s < H.Gear.Length; s++)
            if (H.Open[s] && H.Gear[s] != null) _heap.Enqueue(s, -(PoolValue(s) / H.MasteryNeed(s)));
        for (int k = 0; k < H.FocusSlots && _heap.Count > 0; k++) H.Focus[_heap.Dequeue()] = true;
    }

    // ---------------------------------------------------------------- prestige

    /// <summary>Aether a kill at depth d earns (nothing above the ascension depth): AetherPerKill (d / AscendDepth)^AetherExp.
    /// Deep kills earn far more, so the run's aether rate climbs with it and plateaus where the hero stalls.</summary>
    Big AetherPerKill(double d) => d < P.AscendDepth ? Big.Zero
        : Big.Of(P.AetherPerKill) * Big.Pow(d / P.AscendDepth, P.AetherExp) * (1 + H.Transcendence.PerkTotal(Perk.AetherGain));

    /// <summary>Aether an ascension would pay now (what this run has earned).</summary>
    public Big AetherNow() => H.RunDepth >= P.AscendDepth || H.EverDepth >= P.AscendDepth ? _pendingAether : Big.Zero;

    /// <summary>Stardust a transcendence would pay now: what all the aether ever earned (including this run's, which a
    /// transcendence banks first) is worth, less what earlier transcendences paid out. Waiting never forfeits any.</summary>
    public Big StardustNow()
    {
        var life = H.Ascension.Lifetime + AetherNow();
        if (life < Big.Of(P.TransAether)) return Big.Zero;
        return Big.Of(P.StardustBase) * Big.Pow10((life.Log10 - Math.Log10(P.TransAether)) * P.StardustExp) - H.Transcendence.Lifetime;
    }

    /// <summary>
    /// The reset decision, like a player's: not before the hero is back near the last run's peak (98% of it, unless the
    /// run has gone on four times as long as the last), not while the run is still climbing (less than 5% deeper over
    /// its last quarter, at least ten minutes), and then only for a gain worth how long they've waited (5% deeper for a
    /// run of the profile's patience, less in proportion for longer runs). The reset is a transcendence when that clears
    /// the same bar and gives at least half what the run's aether would (what the stardust buys, less what wiping the
    /// ascension tree costs after the echo: its gain lasts, an ascension's only until the next transcendence), else an
    /// ascension.
    /// </summary>
    bool TryPrestige(bool transcend)
    {
        double runTime = Time - _runStart;
        _runDepths.Add((Time, H.RunDepth));
        if (runTime < 300) return false;
        var gain = AetherNow();
        if (gain < Big.Of(P.AscendMinAether)) return false;
        double back = Time - Math.Max(600, 0.25 * runTime);
        int then = H.RunDepth;
        foreach (var (t, d) in _runDepths)
            if (t <= back) then = d;
            else break;
        if (H.RunDepth > 1.05 * then) return false;
        double reach = Math.Max(1, H.Reach());
        if (reach < 0.98 * _lastPeak && runTime < 4 * Math.Max(Pr.Patience, _lastRun)) return false;
        double bar = reach * Math.Max(0.0005, 0.05 * Math.Min(1, Pr.Patience / runTime));
        double ascend = Preview(H.Ascension, gain);
        if (transcend && TranscendNet(gain) is var net && net >= Math.Max(bar, 0.5 * ascend))
        {
            Transcend();
            return true;
        }
        if (ascend < bar) return false;
        Ascend(gain);
        return true;
    }

    /// <summary>How much deeper (in depth) spending <paramref name="extra"/> more of a layer's currency would take the
    /// hero: the same greedy order as SpendLayer, with every node valued as it is now, and nothing bought.</summary>
    double Preview(LayerState layer, Big extra)
    {
        var nodes = layer.Tree.Nodes;
        int count = nodes.Count;
        Span<double> value = stackalloc double[count], score = stackalloc double[count];
        Span<int> rank = stackalloc int[count];
        var currency = layer.Currency + extra;
        for (int i = 0; i < count; i++)
        {
            var n = nodes[i];
            rank[i] = layer.Ranks[i];
            value[i] = n.RequiresDepth > H.EverDepth ? 0 : NodeValue(n);
            score[i] = value[i] > 0 ? Math.Log10(value[i]) - n.Cost(rank[i]).Log10 : double.NegativeInfinity;
        }
        double gained = 0;
        for (int guard = 0; guard < 10000; guard++)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
                if (score[i] > double.NegativeInfinity && (best < 0 || score[i] > score[best])) best = i;
            if (best < 0) break;
            var n = nodes[best];
            var cost = n.Cost(rank[best]);
            // Spending only goes down, so a node out of reach (or at its last rank) stays out.
            if (cost > currency || n.MaxRank > 0 && rank[best] >= n.MaxRank) { score[best] = double.NegativeInfinity; continue; }
            currency -= cost;
            rank[best]++;
            gained += value[best];
            score[best] = Math.Log10(value[best]) - n.Cost(rank[best]).Log10;
        }
        return gained;
    }

    void Ascend(Big gain)
    {
        var a = H.Ascension;
        a.Currency += gain;
        a.CycleEarned += gain;
        a.Lifetime += gain;
        a.Resets++;
        M.Prestige(Ev.Ascension, Time - _runStart, H.RunDepth);
        ResetRun();
        SpendLayer(a);
        H.UpdateUnlocks();
        H.Rebuild();
    }

    /// <summary>What a transcendence would take the hero deeper by now (negative for a setback): what the stardust buys,
    /// less what wiping the ascension tree costs once the echo has bought part of it back. Only once the stardust would
    /// add at least a quarter to all that was ever earned.</summary>
    double TranscendNet(Big runAether)
    {
        var stardust = StardustNow();
        if (stardust < Big.Of(P.TransMinStardust) || stardust < H.Transcendence.Lifetime * 0.25) return double.NegativeInfinity;
        // The echo is a share of the cycle's aether including this run's, against the tree the cycle's aether bought.
        var cycle = H.Ascension.CycleEarned;
        double echo = EchoShare() * (cycle.IsZero ? 1 : ((cycle + runAether) / cycle).ToDouble());
        double lose = AscensionWorth() * (1 - Math.Pow(Math.Min(1, echo), 1 / (P.AscCostExp + 1)));
        double get = Preview(H.Transcendence, stardust);
        if (Log != null && Time >= _nextLog)
        {
            _nextLog = Time + 0.05 * Time;
            Log.Add($"{Report.Time(Hours),6} cycle {Report.Time((Time - _cycleStart) / 3600),5}: stardust {stardust} (lifetime {H.Transcendence.Lifetime}) buys {get:0} depth, wipe costs {lose:0} (echo {echo:0.00}), reach {H.Reach():0}");
        }
        return get - lose;
    }

    void Transcend()
    {
        var gain = StardustNow();
        double before = Log != null ? Objective(H.Base) : 0;
        var t = H.Transcendence;
        t.Currency += gain;
        t.CycleEarned += gain;
        t.Lifetime += gain;
        t.Resets++;
        M.Prestige(Ev.Transcendence, Time - _cycleStart, H.RunDepth);
        var run = AetherNow();
        H.Ascension.Lifetime += run;
        var cycleAether = H.Ascension.CycleEarned + run;
        H.Ascension.Wipe(H.S);
        _cycleStart = Time;
        SpendLayer(t);
        // The echo (with any Aether Echo just bought) starts the new cycle with a share of the last one's aether.
        H.Ascension.Currency = cycleAether * EchoShare();
        SpendLayer(H.Ascension);
        if (Log != null)
        {
            H.Rebuild();
            Log.Add($"{Report.Time(Hours),6} TRANSCEND for {gain} stardust: objective {before:0} before, {Objective(H.Base):0} after (same gear), record {H.EverDepth}");
        }
        ResetRun();
        H.UpdateUnlocks();
        H.Rebuild();
    }

    double EchoShare() => Math.Min(P.TransEchoCap, P.TransEcho + H.Transcendence.PerkTotal(Perk.AetherEcho));

    /// <summary>How much deeper (in the policy's objective) the ascension tree's stats take the hero now.</summary>
    double AscensionWorth()
    {
        double with = Objective(H.Base);
        H.Knock = Sys.Ascension;
        H.Rebuild(gear: false);
        double without = Objective(H.Base);
        H.Knock = Sys.Count;
        H.Rebuild(gear: false);
        return Math.Max(0, with - without);
    }

    /// <summary>A new run: back to the start depth (deeper with Head Start, with basic gear there), no gear, forge or gold.</summary>
    void ResetRun()
    {
        // The run that ends: how deep it got (depth only rises within a run) and how long it lasted.
        _lastPeak = H.RunDepth;
        _lastRun = Time - _runStart;
        double head = Math.Min(0.5, H.Ascension.PerkTotal(Perk.HeadStart) + H.Transcendence.PerkTotal(Perk.HeadStart));
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
        _runDepths.Clear();
        _pendingAether = Big.Zero;
        H.Rebuild();
        UpdateWeights();
    }

    /// <summary>Buys a layer's ranks: the best value per cost first, while affordable. When the best buy is a one-off
    /// (a node with a single rank, like a rarity tier) that's out of reach, it saves for it instead of spending on
    /// small ranks, as a player would.</summary>
    void SpendLayer(LayerState layer)
    {
        H.Rebuild();
        UpdateWeights();
        for (int guard = 0; guard < 10000; guard++)
        {
            LayerNode best = null, saveFor = null;
            double bestScore = double.NegativeInfinity, saveScore = double.NegativeInfinity;
            foreach (var n in layer.Tree.Nodes)
            {
                bool open = (n.MaxRank == 0 || layer.Ranks[n.Id] < n.MaxRank) && H.EverDepth >= n.RequiresDepth;
                if (!open) continue;
                double v = NodeValue(n);
                if (v <= 0) continue;
                double score = Math.Log10(v) - n.Cost(layer.Ranks[n.Id]).Log10;
                if (layer.CanBuy(n, H.EverDepth))
                {
                    if (score > bestScore) { bestScore = score; best = n; }
                }
                else if (n.MaxRank == 1 && score > saveScore) { saveScore = score; saveFor = n; }
            }
            if (best == null || saveFor != null && saveScore > bestScore) break;
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
                return n.Amount * H.CodexValue * P.CodexLE * H.PoolScale * more;
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
            case Perk.SlotUnlock:
                return 20 * more;
            case Perk.AetherEcho:
                return 2 * more;
            default:
                return 0;
        }
    }

    /// <summary>The power (LE) all mastery and all ranks give now.</summary>
    double MasteryLeTotal()
    {
        double sum = 0;
        for (int s = 0; s < H.Mastery.Length; s++)
            if (H.Open[s]) sum += H.Mastery[s] * P.MasteryLE;
        return sum * H.PoolScale;
    }

    double RankLeTotal()
    {
        double sum = 0;
        for (int s = 0; s < H.Rank.Length; s++)
            if (H.Open[s]) sum += H.Rank[s] * P.RankLE;
        return sum * H.PoolScale;
    }
}
