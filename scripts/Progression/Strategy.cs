using System;
using System.Collections.Generic;

namespace LootFall.Progression;

/// <summary>
/// A sensible player's choices for one world: what each stat is worth now (by finite differences of how deep the hero
/// pushes and farms), which drops to put on, how to spend gold, materials, talent points and the prestige currencies,
/// where mastery focus goes, and when a reset pays. The simulator plays with it; the game uses it for its autopilot
/// and its auto-equip, and to preview what a choice would give.
/// </summary>
public sealed class Strategy : ILootTaker
{
    public readonly World W;
    readonly Hero H;
    readonly Params P;
    readonly double[] _w = new double[Stats.Count];
    readonly Sheet _old = new(), _new = new(), _trial = new();
    /// <summary>A batch's trades waiting for <see cref="Trade"/>: slot and score, and the item by slot.</summary>
    readonly List<(int Slot, double Score)> _trades = new();
    readonly Item[] _tradeItems;
    readonly double[] _forgeScore, _forgeCost;
    readonly PriorityQueue<int, double> _heap = new();

    /// <summary>When set, transcendence decisions are written here (progsim --debug-prestige).</summary>
    public List<string> Log;
    double _nextLog;

    public Strategy(World w)
    {
        W = w;
        H = w.H;
        P = w.P;
        int n = H.Gear.Length;
        _tradeItems = new Item[n];
        _tradePicks = new Item[n];
        _forgeScore = new double[n];
        _forgeCost = new double[n];
    }

    public double[] Weights => _w;

    /// <summary>LE per unit of more damage and health (reach per LE), for valuing prestige perks.</summary>
    public double WeightMore { get; private set; }

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
    public double Objective(in Combat.Base b) => Objective(H.Total, b);

    public double Objective(Sheet t, in Combat.Base b) => 0.5 * (H.Fight.Reach(t, b) + H.Fight.FarmReach(t, b));

    /// <summary>Before the hero can clear depth 1: offense plus defense at depth 1 stands in for reach.</summary>
    double PowerAtOne(in Combat.Base b)
    {
        var f = H.Fight.At(H.Total, b, 1);
        return 0.5 * (f.Dps + f.Hp);
    }

    // ---------------------------------------------------------------- gear

    public bool DiscoverNow => true;

    /// <summary>When set, every item put on is listed here (slot, the item offered that went on, what it replaced, its
    /// score), for the game's reveal.</summary>
    public List<(int Slot, Item Picked, Item Replaced, double Score)> Applied;
    readonly Item[] _tradePicks;

    Item _pick;
    double _pickScore;

    /// <summary>Equips a batch of drops: an item at least as good in every stat goes on at once; items that trade some
    /// stats for others wait for <see cref="Trade"/>; everything is salvaged for materials. Returns whether gear changed.</summary>
    public bool Take(LootRoll roll)
    {
        bool changed = false;
        foreach (int s in roll.Slots)
        {
            int count = roll.CandidateCount[s];
            if (count == 0) continue;
            Begin(s);
            for (int k = 0; k < count; k++) Offer(s, roll.Candidate(s, k));
            changed |= Settle(s);
        }
        H.Materials += roll.Materials;
        return Finish(changed);
    }

    /// <summary>Equips the best of <paramref name="items"/> (slot and item, grouped by slot) the same way: what's at least
    /// as good in every stat at once, trades together if they leave the hero ahead. Returns whether gear changed.</summary>
    public bool Equip(IReadOnlyList<(int Slot, Item Item)> items)
    {
        bool changed = false;
        for (int i = 0; i < items.Count;)
        {
            int s = items[i].Slot;
            Begin(s);
            for (; i < items.Count && items[i].Slot == s; i++) Offer(s, items[i].Item);
            changed |= Settle(s);
        }
        return Finish(changed);
    }

    void Begin(int s)
    {
        _old.Clear();
        if (H.Gear[s] != null) H.Contribute(s, H.Gear[s], _old);
        _pick = null;
        _pickScore = 0;
    }

    void Offer(int s, Item cand)
    {
        double score = Score(s, cand);
        if (H.Gear[s] == null && _pick == null || score > _pickScore)
        {
            _pickScore = score;
            _pick = cand;
        }
    }

    bool Settle(int s)
    {
        if (_pick == null || H.Gear[s] != null && _pickScore <= 1e-9) return false;
        if (H.Gear[s] == null || Dominates(s, _pick))
        {
            Applied?.Add((s, _pick, H.Gear[s] == null ? null : Copy(H.Gear[s]), _pickScore));
            H.Gear[s] ??= new Item();
            H.Gear[s].CopyFrom(_pick);
            W.Events?.Upgrade(_pickScore);
            return true;
        }
        (_tradeItems[s] ??= new Item()).CopyFrom(_pick);
        _tradePicks[s] = _pick;
        _trades.Add((s, _pickScore));
        return false;
    }

    bool Finish(bool changed)
    {
        if (changed) H.RebuildGear();
        if (_trades.Count > 0 && Trade())
        {
            H.RebuildGear();
            changed = true;
        }
        return changed;
    }

    static Item Copy(Item it)
    {
        var c = new Item();
        c.CopyFrom(it);
        return c;
    }

    /// <summary>What putting <paramref name="it"/> in slot <paramref name="s"/> would do: its score (reach by the weights) and
    /// whether it is at least as good as what the slot wears in every stat.</summary>
    public (double Score, bool Dominates) Judge(int s, Item it)
    {
        _old.Clear();
        if (H.Gear[s] != null) H.Contribute(s, H.Gear[s], _old);
        double score = Score(s, it);
        return (score, H.Gear[s] == null || Dominates(s, it));
    }

    /// <summary>Whether an item is at least as good as what slot <paramref name="s"/> wears in every stat, so it can't
    /// make the hero weaker whatever the balance (<c>_old</c> holds what the slot gives now).</summary>
    bool Dominates(int s, Item it)
    {
        _new.Clear();
        H.Contribute(s, it, _new);
        for (int i = 0; i < Stats.Count; i++)
            if (_new.V[i] < _old.V[i] - 1e-12) return false;
        return true;
    }

    /// <summary>Puts on the batch's trades (items better by the weights, worse in some stat) if together they leave the
    /// hero ahead by the objective itself, else the best half of them, a quarter and so on. Scored one by one against the
    /// same totals, a hundred slots' trades add up past the balance between damage and health the weights were taken
    /// at, and the hero ends up weaker (a player away for hours would come back to that).</summary>
    bool Trade()
    {
        H.Rebuild();
        double before = Objective(H.Total, H.Base);
        _trades.Sort((a, b) => b.Score.CompareTo(a.Score));
        bool done = false;
        for (int take = _trades.Count; take > 0 && !done; take /= 2)
        {
            _trial.CopyFrom(H.Total);
            for (int j = 0; j < take; j++)
            {
                int s = _trades[j].Slot;
                _old.Clear();
                H.Contribute(s, H.Gear[s], _old);
                _new.Clear();
                H.Contribute(s, _tradeItems[s], _new);
                for (int i = 0; i < Stats.Count; i++)
                {
                    double o = _old.V[i], n = _new.V[i];
                    if (!Stats.IsFlat((Stat)i)) _trial.V[i] += n - o;
                    else if (o > Scale.None || n > Scale.None) _trial.V[i] = H.S.Add(H.S.Sub(_trial.V[i], o), n);
                }
            }
            if (Objective(_trial, H.Fight.Prepare(_trial)) < before) continue;
            for (int j = 0; j < take; j++)
            {
                int s = _trades[j].Slot;
                Applied?.Add((s, _tradePicks[s], Copy(H.Gear[s]), _trades[j].Score));
                H.Gear[s].CopyFrom(_tradeItems[s]);
                W.Events?.Upgrade(_trades[j].Score);
            }
            done = true;
        }
        _trades.Clear();
        return done;
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

    // ---------------------------------------------------------------- spending

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

    /// <summary>
    /// Forge levels, all at once. Costs grow by ForgeGrowth a level and value by only 1 + ForgeStep, so the k-th next
    /// level of slot s is worth log(value/cost) - k * slope (log10 units): the best buys fill every slot up to a common
    /// "water level", which a bisection finds for the gold on hand. That is what buying the best level one at a time
    /// does, without millions of single purchases.
    /// </summary>
    public bool SpendForge()
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
            W.Events?.Count(Ev.Forge, k);
            bought = true;
        }
        return bought;
    }

    /// <summary>The reach a level of rank or mastery in slot <paramref name="s"/> is worth, per LE.</summary>
    public double PoolValue(int s)
    {
        var (d, h) = H.PoolSplit(s);
        return d * _w[(int)Stat.MoreDamage] + h * _w[(int)Stat.MoreHp];
    }

    /// <summary>Ranks: the best value per material first. Every slot's rank is worth about the same, so this evens them out,
    /// cheapest (newest) slots first.</summary>
    public bool SpendRanks()
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
            W.Events?.Count(Ev.Rank);
            _heap.Enqueue(s, -(PoolValue(s) / H.RankCost(s)));
        }
        return bought;
    }

    /// <summary>Focus goes where a level is cheapest for what it's worth: the least-mastered worn slots.</summary>
    public void ChooseFocus()
    {
        Array.Clear(H.Focus);
        _heap.Clear();
        for (int s = 0; s < H.Gear.Length; s++)
            if (H.Open[s] && H.Gear[s] != null) _heap.Enqueue(s, -(PoolValue(s) / H.MasteryNeed(s)));
        for (int k = 0; k < H.FocusSlots && _heap.Count > 0; k++) H.Focus[_heap.Dequeue()] = true;
    }

    /// <summary>Spends the talent points on what's worth most now.</summary>
    public void AllocateTalents() => H.Talents.Allocate(_w, H.S);

    // ---------------------------------------------------------------- prestige

    /// <summary>
    /// The reset decision, like a player's: not before the hero is back near the last run's peak (98% of it, unless the
    /// run has gone on four times as long as the last), not while the run is still climbing (less than 5% deeper over
    /// its last quarter, at least ten minutes), and then only for a gain worth how long they've waited (3% deeper for a
    /// run of <paramref name="patience"/> seconds, less in proportion for longer runs). The reset is a transcendence when
    /// that clears the same bar and gives at least half what the run's aether would (what the stardust buys, less what
    /// wiping the ascension tree costs after the echo: its gain lasts, an ascension's only until the next
    /// transcendence), else an ascension.
    /// </summary>
    public bool TryPrestige(bool transcend, double patience)
    {
        switch (ChoosePrestige(transcend, patience))
        {
            case Reset.Transcend:
                Transcend();
                return true;
            case Reset.Ascend:
                Ascend(W.AetherNow());
                return true;
            default:
                return false;
        }
    }

    public enum Reset : byte { None, Ascend, Transcend }

    /// <summary>The reset <see cref="TryPrestige"/> would make now, without making it (it notes the run's depth for the
    /// next decision, as a check-in does).</summary>
    public Reset ChoosePrestige(bool transcend, double patience)
    {
        double runTime = W.Time - W.RunStart;
        W.RunDepths.Add((W.Time, H.RunDepth));
        if (runTime < 300) return Reset.None;
        var gain = W.AetherNow();
        if (gain < Big.Of(P.AscendMinAether)) return Reset.None;
        double back = W.Time - Math.Max(600, 0.25 * runTime);
        int then = H.RunDepth;
        foreach (var (t, d) in W.RunDepths)
            if (t <= back) then = d;
            else break;
        if (H.RunDepth > 1.05 * then) return Reset.None;
        double reach = Math.Max(1, H.Reach());
        if (reach < 0.98 * W.LastPeak && runTime < 4 * Math.Max(patience, W.LastRun)) return Reset.None;
        double bar = reach * Math.Max(0.0005, 0.03 * Math.Min(1, patience / runTime));
        double ascend = Preview(H.Ascension, gain);
        if (transcend && TranscendNet(gain) is var net && net >= Math.Max(bar, 0.5 * ascend)) return Reset.Transcend;
        return ascend < bar ? Reset.None : Reset.Ascend;
    }

    /// <summary>How much deeper (in depth) spending <paramref name="extra"/> more of a layer's currency would take the
    /// hero: the same greedy order as SpendLayer, with every node valued as it is now, and nothing bought.</summary>
    public double Preview(LayerState layer, Big extra)
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

    /// <summary>Ascends for this run's aether and spends it.</summary>
    public void Ascend(Big gain)
    {
        W.PayAscension(gain);
        W.ResetRun();
        UpdateWeights();
        SpendLayer(H.Ascension);
        H.UpdateUnlocks();
        H.Rebuild();
    }

    /// <summary>What a transcendence would take the hero deeper by now (negative for a setback): what the stardust buys,
    /// less what wiping the ascension tree costs once the echo has bought part of it back. Only once the stardust would
    /// add at least a quarter to all that was ever earned.</summary>
    public double TranscendNet(Big runAether)
    {
        var stardust = W.StardustNow();
        if (stardust < Big.Of(P.TransMinStardust) || stardust < H.Transcendence.Lifetime * 0.25) return double.NegativeInfinity;
        // The echo is a share of the cycle's aether including this run's, against the tree the cycle's aether bought.
        var cycle = H.Ascension.CycleEarned;
        double echo = W.EchoShare() * (cycle.IsZero ? 1 : ((cycle + runAether) / cycle).ToDouble());
        double lose = AscensionWorth() * (1 - Math.Pow(Math.Min(1, echo), 1 / (P.AscCostExp + 1)));
        double get = Preview(H.Transcendence, stardust);
        if (Log != null && W.Time >= _nextLog)
        {
            _nextLog = W.Time + 0.05 * W.Time;
            Log.Add($"{Report.Time(W.Hours),6} cycle {Report.Time((W.Time - W.CycleStart) / 3600),5}: stardust {stardust} (lifetime {H.Transcendence.Lifetime}) buys {get:0} depth, wipe costs {lose:0} (echo {echo:0.00}), reach {H.Reach():0}");
        }
        return get - lose;
    }

    /// <summary>Transcends: pays the stardust and spends it, starts the cycle with the echo and spends that, and starts a
    /// new run.</summary>
    public void Transcend()
    {
        var gain = W.StardustNow();
        double before = Log != null ? Objective(H.Base) : 0;
        var cycleAether = W.PayTranscendence();
        SpendLayer(H.Transcendence);
        // The echo (with any Aether Echo just bought) starts the new cycle with a share of the last one's aether.
        W.StartCycle(cycleAether);
        SpendLayer(H.Ascension);
        if (Log != null)
        {
            H.Rebuild();
            Log.Add($"{Report.Time(W.Hours),6} TRANSCEND for {gain} stardust: objective {before:0} before, {Objective(H.Base):0} after (same gear), record {H.EverDepth}");
        }
        W.ResetRun();
        UpdateWeights();
        H.UpdateUnlocks();
        H.Rebuild();
    }

    /// <summary>How much deeper (in the policy's objective) the ascension tree's stats take the hero now.</summary>
    public double AscensionWorth()
    {
        double with = Objective(H.Base);
        H.Knock = Sys.Ascension;
        H.Rebuild(gear: false);
        double without = Objective(H.Base);
        H.Knock = Sys.Count;
        H.Rebuild(gear: false);
        return Math.Max(0, with - without);
    }

    /// <summary>Buys a layer's ranks: the best value per cost first, while affordable. When the best buy is a one-off
    /// (a node with a single rank, like a rarity tier) that's out of reach, it saves for it instead of spending on
    /// small ranks, as a player would.</summary>
    public void SpendLayer(LayerState layer)
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
            W.Events?.Count(Ev.LayerRank);
        }
        H.UpdateUnlocks();
        H.Rebuild();
    }

    /// <summary>What a prestige rank is worth in reach (stats by their weights; perks by what they do to their system).</summary>
    public double NodeValue(LayerNode n)
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
