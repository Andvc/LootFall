using System;
using System.Collections.Generic;

namespace LootFall.Progression;

/// <summary>What a world reports as it runs: the simulator's metrics count these, the game's screen shows them.</summary>
public interface IWorldEvents
{
    void Count(Ev e, double n = 1);
    void Record(int depth);
    void Upgrade(double score);
    void Prestige(Ev kind, double seconds, int depth);
}

/// <summary>Where a step's drops go: the simulator's strategy equips them at once, the game puts them in chests.</summary>
public interface ILootTaker
{
    /// <summary>Whether drops mark their first finds in the codex as they're rolled (the simulator) or leave that to
    /// whoever opens them (the game's chests).</summary>
    bool DiscoverNow { get; }

    /// <summary>Takes a batch of drops; returns whether the hero's gear changed.</summary>
    bool Take(LootRoll roll);
}

/// <summary>
/// One batch of drops, rolled: for each open slot that got any, the best candidates of the two highest rarity tiers it
/// rolled (at most four, in the order rolled), how many items of each rarity dropped, the materials they salvage for, and
/// the codex entries they found. Only the candidates can matter for gear (the rest are worse rolls of the same tiers or
/// lower ones), which is what lets thousands of drops be sampled at once.
/// </summary>
public sealed class LootRoll
{
    public const int MaxCandidates = 4;

    public double Ilvl, Materials;
    /// <summary>The slots that got drops, in slot order.</summary>
    public readonly List<int> Slots = new();
    /// <summary>Candidates by slot: Candidates[slot * MaxCandidates + k] for k below CandidateCount[slot].</summary>
    public readonly Item[] Candidates;
    public readonly int[] CandidateCount;
    /// <summary>Items dropped by slot and rarity: Counts[slot * rarities + rarity].</summary>
    public readonly long[] Counts;
    /// <summary>Codex entries found (keys into Hero.Codex); when discovered later, some may already be marked.</summary>
    public readonly List<int> Found = new();
    public long Items;

    public LootRoll(int slots)
    {
        Candidates = new Item[slots * MaxCandidates];
        for (int i = 0; i < Candidates.Length; i++) Candidates[i] = new Item();
        CandidateCount = new int[slots];
        Counts = new long[slots * Content.RarityNames.Length];
    }

    public void Clear()
    {
        foreach (int s in Slots)
        {
            CandidateCount[s] = 0;
            Array.Clear(Counts, s * Content.RarityNames.Length, Content.RarityNames.Length);
        }
        Slots.Clear();
        Found.Clear();
        Materials = 0;
        Items = 0;
    }

    public Item Candidate(int slot, int k) => Candidates[slot * MaxCandidates + k];
}

/// <summary>What one step of the world did, for whoever shows it.</summary>
public struct StepOutcome
{
    public double PushKills, FarmKills;
    /// <summary>Where the hero farmed with the time the push left, and the deepest depth the push reached.</summary>
    public int FarmDepth, PushTop;
    /// <summary>Depths cleared this step, and whether the last of them went by in an overkill skip.</summary>
    public int Cleared;
    public bool Skipped;
    public Big Gold, Xp, Aether;
    public int Levels, MasteryLevels;
}

/// <summary>
/// One hero's world: the run (depth, waves, bosses, overkill skips), income, drops, mastery and levels, and the
/// mechanics of the prestige layers (aether and stardust owed, paying them out, starting a new run). It makes no
/// choices: what to equip, buy and when to reset are a <see cref="Strategy"/>'s (the simulator's sensible player and
/// the game's autopilot) or the player's. The simulator and the game run the same world.
/// </summary>
public sealed class World
{
    public readonly Params P;
    public readonly Hero H;
    public readonly Rng Rng;
    public IWorldEvents Events;

    /// <summary>Seconds played.</summary>
    public double Time;
    /// <summary>How far through the next depth the push is (0 to 1), when it's still fighting there.</summary>
    public double DepthProgress;
    /// <summary>How many depths the push under way clears at once (decided when it starts, by overkill; 0 between
    /// depths). Kept until it's done, so the size of a step doesn't decide whether a skip happens.</summary>
    public int Skip;
    /// <summary>When this run and this transcendence cycle started, the run's depth at each decision (to tell climbing
    /// from a plateau), and the last run's peak and length.</summary>
    public double RunStart, CycleStart;
    public readonly List<(double Time, int Depth)> RunDepths = new();
    public int LastPeak;
    public double LastRun;
    /// <summary>Aether this run has earned so far (paid out by an ascension).</summary>
    public Big PendingAether = Big.Zero;

    readonly LootRoll _roll;

    public World(Params p, Content c, TalentTree tree, int seed)
    {
        P = p;
        Rng = new Rng(seed);
        H = new Hero(p, c, tree);
        _roll = new LootRoll(c.SlotCount);
    }

    public double Hours => Time / 3600;

    // ---------------------------------------------------------------- one step

    /// <summary>Advances the world by <paramref name="dt"/> seconds: the push clears depths while it can, the rest of
    /// the time farms the deepest comfortable depth; gold, XP and aether come in; drops go to <paramref name="loot"/>;
    /// worn slots gain mastery.</summary>
    public StepOutcome Step(double dt, ILootTaker loot)
    {
        var o = new StepOutcome();
        var b = H.Base;
        double timeLeft = dt;
        double pushKills = 0, farmKills = 0;
        Big gold = Big.Zero, xp = Big.Zero, aether = Big.Zero;
        int pushTop = H.RunDepth;

        // Push: clear the next depth (its kills and boss) while it can be beaten and time remains.
        for (int guard = 0; guard < 20000 && timeLeft > 0; guard++)
        {
            int next = H.RunDepth + 1;
            if (!H.Fight.CanClear(H.Total, b, next))
            {
                DepthProgress = 0;
                Skip = 0;
                break;
            }
            var f = H.Fight.At(H.Total, b, next);
            double rate = H.Fight.KillRate(f, next);
            bool guardian = Combat.IsGuardian(next);
            double need = P.KillsPerDepth / rate + Math.Min(P.BossTime, H.Fight.Boss(f, next, guardian).Time);
            // Overkill: clear several depths in one go.
            int skip = Skip = PlannedSkip(f, next);
            if (skip > 1)
            {
                next += skip - 1;
                need = H.Fight.SkipTime;
                guardian = false;
            }
            double share = Math.Min(1 - DepthProgress, timeLeft / need);
            DepthProgress += share;
            timeLeft -= share * need;
            double k = share * (P.KillsPerDepth + P.BossReward * (guardian ? 2 : 1));
            pushKills += k;
            gold += H.GoldPerKill(next) * k;
            xp += H.XpPerKill(next) * k;
            var a = AetherPerKill(next) * k;
            PendingAether += a;
            aether += a;
            if (DepthProgress < 1 - 1e-9) break;
            DepthProgress = 0;
            Skip = 0;
            o.Cleared += next - H.RunDepth;
            o.Skipped = skip > 1;
            H.RunDepth = next;
            pushTop = next;
            if (next > H.EverDepth)
            {
                H.EverDepth = next;
                Events?.Record(next);
                int opened = H.UpdateUnlocks();
                if (opened > 0) Events?.Count(Ev.SlotUnlock, opened);
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
            var a = AetherPerKill(farm) * farmKills;
            PendingAether += a;
            aether += a;
        }
        o.PushKills = pushKills;
        o.FarmKills = farmKills;
        o.FarmDepth = farm;
        o.PushTop = pushTop;
        o.Gold = gold;
        o.Xp = xp;
        o.Aether = aether;

        H.Gold += gold;
        o.Levels = Gain(xp);

        // Loot: whoever takes it equips, salvages and notes first finds.
        bool gearChanged = false;
        double quantity = P.DropChance * (1 + H.Total[Stat.Quantity]);
        if (farmKills > 0)
        {
            Roll(farmKills * quantity, farm, _roll, loot.DiscoverNow);
            gearChanged |= loot.Take(_roll);
        }
        if (pushKills > 0)
        {
            Roll(pushKills * quantity, pushTop, _roll, loot.DiscoverNow);
            gearChanged |= loot.Take(_roll);
        }

        // Mastery: worn slots learn with time, focused ones faster.
        double gain = dt / 3600 * P.MasteryRate * H.MasteryGain;
        for (int s = 0; s < H.Gear.Length; s++)
        {
            if (!H.Active[s]) continue;
            H.MasteryXp[s] += gain * (H.Focus[s] ? P.MasteryFocus : 1);
            while (H.MasteryXp[s] >= H.MasteryNeed(s))
            {
                H.MasteryXp[s] -= H.MasteryNeed(s);
                H.Mastery[s]++;
                gearChanged = true;
                o.MasteryLevels++;
                Events?.Count(Ev.Mastery);
            }
        }

        if (gearChanged) H.Rebuild();
        return o;
    }

    /// <summary>How many depths the push from <paramref name="next"/> clears at once: the skip under way, or a new one
    /// decided by overkill (checking the last depth it would reach can be cleared at all).</summary>
    public int PlannedSkip(in Combat.Frame f, int next)
    {
        if (DepthProgress > 0 && Skip > 0) return Skip;
        int skip = H.Fight.Skip(f, next);
        if (skip > 1 && !H.Fight.CanClear(H.Total, H.Base, next + skip - 1)) skip = 1;
        return skip;
    }

    /// <summary>Adds XP and takes the levels it buys (a talent point each); returns how many.</summary>
    public int Gain(Big xp)
    {
        H.Xp += xp;
        int levels = 0;
        for (int guard = 0; guard < 100000; guard++)
        {
            var need = H.XpToNext();
            if (H.Xp < need) break;
            H.Xp -= need;
            H.Level++;
            H.Talents.Points++;
            levels++;
            Events?.Count(Ev.Level);
        }
        return levels;
    }

    // ---------------------------------------------------------------- loot

    /// <summary>Samples <paramref name="count"/> drops at item level <paramref name="ilvl"/> into <paramref name="into"/>: a
    /// Poisson count per slot, then per rarity; only the best couple of candidates of the two highest tiers are rolled.
    /// With <paramref name="discover"/>, first finds go into the codex at once; otherwise their keys are kept for later.</summary>
    public void Roll(double count, double ilvl, LootRoll into, bool discover)
    {
        into.Clear();
        into.Ilvl = ilvl;
        if (count <= 0 || H.OpenCount == 0) return;
        var odds = H.RarityOdds();
        double perSlot = count / H.OpenCount;
        int rarities = Content.RarityNames.Length;
        for (int s = 0; s < H.Gear.Length; s++)
        {
            if (!H.Open[s]) continue;
            long n = Rng.Poisson(perSlot);
            if (n == 0) continue;
            into.Slots.Add(s);
            into.Items += n;
            int tiersRolled = 0;
            long higher = 0;
            for (int r = H.MaxRarity; r >= 0; r--)
            {
                long c = r == 0 ? Math.Max(0, n - higher) : Rng.Poisson(n * odds[r]);
                higher += c;
                if (c <= 0) continue;
                into.Counts[s * rarities + r] += c;
                into.Materials += c * H.MaterialPerItem(r);
                if (discover)
                {
                    int found = H.Discover(Rng, s, r, c, ilvl);
                    if (found > 0) Events?.Count(Ev.Codex, found);
                }
                else H.Discover(Rng, s, r, c, ilvl, into.Found);
                if (tiersRolled >= 2) continue;
                tiersRolled++;
                for (int i = 0; i < Math.Min(2, c); i++)
                {
                    double q = i == 0 ? Rng.MaxOfUniform(c) : Rng.NextDouble();
                    H.Roll(Rng, s, ilvl, r, q, into.Candidates[s * LootRoll.MaxCandidates + into.CandidateCount[s]++]);
                }
            }
        }
    }

    // ---------------------------------------------------------------- prestige

    /// <summary>Aether a kill at depth d earns (nothing above the ascension depth): AetherPerKill (d / AscendDepth)^AetherExp.
    /// Deep kills earn far more, so the run's aether rate climbs with it and plateaus where the hero stalls.</summary>
    public Big AetherPerKill(double d) => d < P.AscendDepth ? Big.Zero
        : Big.Of(P.AetherPerKill) * Big.Pow(d / P.AscendDepth, P.AetherExp) * (1 + H.Transcendence.PerkTotal(Perk.AetherGain));

    /// <summary>Aether an ascension would pay now (what this run has earned).</summary>
    public Big AetherNow() => H.RunDepth >= P.AscendDepth || H.EverDepth >= P.AscendDepth ? PendingAether : Big.Zero;

    /// <summary>Stardust a transcendence would pay now: what all the aether ever earned (including this run's, which a
    /// transcendence banks first) is worth, less what earlier transcendences paid out. Waiting never forfeits any.</summary>
    public Big StardustNow()
    {
        var life = H.Ascension.Lifetime + AetherNow();
        if (life < Big.Of(P.TransAether)) return Big.Zero;
        return Big.Of(P.StardustBase) * Big.Pow10((life.Log10 - Math.Log10(P.TransAether)) * P.StardustExp) - H.Transcendence.Lifetime;
    }

    /// <summary>The share of a cycle's aether the next cycle starts with.</summary>
    public double EchoShare() => Math.Min(P.TransEchoCap, P.TransEcho + H.Transcendence.PerkTotal(Perk.AetherEcho));

    /// <summary>Pays out an ascension's aether (the run itself is reset by <see cref="ResetRun"/>).</summary>
    public void PayAscension(Big gain)
    {
        var a = H.Ascension;
        a.Currency += gain;
        a.CycleEarned += gain;
        a.Lifetime += gain;
        a.Resets++;
        Events?.Prestige(Ev.Ascension, Time - RunStart, H.RunDepth);
    }

    /// <summary>Pays out a transcendence's stardust, banks the run's aether and wipes the ascension tree. Returns the
    /// cycle's aether, of which <see cref="StartCycle"/> gives the echo back.</summary>
    public Big PayTranscendence()
    {
        var gain = StardustNow();
        var t = H.Transcendence;
        t.Currency += gain;
        t.CycleEarned += gain;
        t.Lifetime += gain;
        t.Resets++;
        Events?.Prestige(Ev.Transcendence, Time - CycleStart, H.RunDepth);
        var run = AetherNow();
        H.Ascension.Lifetime += run;
        var cycleAether = H.Ascension.CycleEarned + run;
        H.Ascension.Wipe(H.S);
        CycleStart = Time;
        return cycleAether;
    }

    /// <summary>Starts a transcendence cycle with the echo of the last one's aether (with any Aether Echo bought since).</summary>
    public void StartCycle(Big cycleAether) => H.Ascension.Currency = cycleAether * EchoShare();

    /// <summary>A new run: back to the start depth (deeper with Head Start, with basic gear there), no gear, forge or gold,
    /// nothing worn (whoever plays chooses the set again).</summary>
    public void ResetRun()
    {
        // The run that ends: how deep it got (depth only rises within a run) and how long it lasted.
        LastPeak = H.RunDepth;
        LastRun = Time - RunStart;
        double head = Math.Min(0.5, H.Ascension.PerkTotal(Perk.HeadStart) + H.Transcendence.PerkTotal(Perk.HeadStart));
        int start = Math.Max(1, (int)(H.EverDepth * head));
        H.RunDepth = start;
        DepthProgress = 0;
        Skip = 0;
        H.Gold = Big.Zero;
        double keep = Math.Min(0.5, H.Ascension.PerkTotal(Perk.ForgeKeep));
        for (int s = 0; s < H.Forge.Length; s++) H.Forge[s] = (int)(H.Forge[s] * keep);
        Array.Clear(H.Worn);
        for (int s = 0; s < H.Gear.Length; s++)
        {
            H.Gear[s] = null;
            if (start > 1 && H.Open[s])
            {
                H.Gear[s] = new Item();
                H.Roll(Rng, s, start, Math.Min(2, H.MaxRarity), 0.5, H.Gear[s]);
            }
        }
        RunStart = Time;
        RunDepths.Clear();
        PendingAether = Big.Zero;
        H.Rebuild();
    }
}
