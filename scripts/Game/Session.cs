using System;
using System.Collections.Generic;
using System.Linq;
using LootFall.Progression;

namespace LootFall.Game;

public enum EventKind : byte
{
    /// <summary>A depth cleared (Value: the depth; Flag: an overkill skip; Data: the chest its boss dropped, if any).</summary>
    DepthCleared,
    /// <summary>A new deepest depth (Value).</summary>
    Record,
    /// <summary>Levels gained (Value: how many).</summary>
    Level,
    /// <summary>Slots opened (Value: how many).</summary>
    SlotsOpened,
    /// <summary>A rarity tier opened (Value: the tier).</summary>
    RarityOpened,
    /// <summary>A chest sealed into the pile (Data: the chest).</summary>
    ChestSealed,
    /// <summary>A chest opened (Data: the Opening; Flag: opened automatically).</summary>
    ChestOpened,
    /// <summary>Mastery levels gained (Value: how many).</summary>
    Mastery,
    /// <summary>An ascension or a transcendence (Value: the depth reached; Amount: the currency paid).</summary>
    Ascended,
    Transcended,
    /// <summary>A system's automation became available (Data: its name).</summary>
    Unlocked,
}

public readonly struct GameEvent
{
    public readonly EventKind Kind;
    public readonly int Value;
    public readonly bool Flag;
    public readonly Big Amount;
    public readonly object Data;

    public GameEvent(EventKind kind, int value = 0, bool flag = false, Big amount = default, object data = null)
    {
        Kind = kind;
        Value = value;
        Flag = flag;
        Amount = amount;
        Data = data;
    }
}

/// <summary>Which chores run by themselves. Each opens at a milestone (see <see cref="Session.Unlocked"/>); the player
/// switches them on and off.</summary>
public sealed class Automation
{
    /// <summary>Open chests as they drop (no reveal; notable finds still announce themselves).</summary>
    public bool OpenChests;
    /// <summary>Spend gold on forge levels and materials on ranks, where they're worth most.</summary>
    public bool Forge = true, Ranks = true;
    /// <summary>Spend talent points (the tree, then paragon) on what's worth most now.</summary>
    public bool Talents = true;
    /// <summary>Put mastery focus on the slots where a level is cheapest for its worth.</summary>
    public bool Focus = true;
    /// <summary>Spend aether and stardust when they come in.</summary>
    public bool SpendPrestige = true;
    /// <summary>Ascend by the sensible rule (the autopilot).</summary>
    public bool Ascend = true;
    /// <summary>With auto-open on, chests holding something this rare wait for the player to open them
    /// (<see cref="HoldNewest"/>: the newest tier open; <see cref="HoldNone"/>: none wait).</summary>
    public int HoldAt = HoldNewest;

    public const int HoldNewest = -1, HoldNone = 99;
}

/// <summary>What happened while the player was away.</summary>
public sealed class AwaySummary
{
    public double Seconds;
    public int DepthBefore, DepthAfter, RecordBefore, RecordAfter, Levels, Chests, Equipped, NewEntries, Ascensions;
    public double Kills;
    public Big Gold = Big.Zero;
    /// <summary>The rarest items found, rarest first (at most eight).</summary>
    public readonly List<Card> Best = new();
    public readonly long[] Found = new long[Content.RarityNames.Length];
}

/// <summary>A game in progress: one hero's world, the chests, the automation and the statistics, and every action the
/// player can take. Engine-free: the screen reads it and calls its actions, and the bot and the tests drive it the same way.</summary>
public sealed class Session : IWorldEvents
{
    public const double TickSeconds = 0.1;

    public readonly Params P;
    public readonly Content C;
    public readonly TalentTree Tree;
    public readonly World W;
    public readonly Strategy S;
    public readonly Hero H;
    public readonly ChestPile Pile = new();
    public readonly Automation Auto = new();
    public readonly Queue<GameEvent> Events = new();
    public readonly Tally Totals = new();

    /// <summary>Play time not yet stepped, and when the chores, the focus and the autopilot next run (saved, so a
    /// restored game plays on exactly as the original would).</summary>
    internal double Accum, NextChores, NextAutopilot, NextFocus;
    int _rarity;
    bool _away;
    readonly List<(int Slot, Item Item)> _openItems = new();
    readonly List<(int Slot, Item Picked, Item Replaced, double Score)> _applied = new();

    public Session(int seed = 1) : this(new Params(), seed) { }

    public Session(Params p, int seed)
    {
        P = p;
        C = new Content(p);
        Tree = new TalentTree(p);
        W = new World(p, C, Tree, seed);
        H = W.H;
        S = new Strategy(W) { Applied = _applied };
        W.Events = this;
        _rarity = H.MaxRarity;
        S.UpdateWeights();
    }

    /// <summary>A new hero: depth 1, four slots open, nothing worn, and a chest with something for each of them.</summary>
    public static Session NewGame(int seed)
    {
        var s = new Session(seed);
        s.Pile.Add(ChestPile.Starter(s.H, s.W.Rng));
        return s;
    }

    public double Time => W.Time;

    /// <summary>After a save is loaded: the rarity tiers already open aren't announced again, and the weights are fresh.</summary>
    public void AfterRestore()
    {
        _rarity = H.MaxRarity;
        S.UpdateWeights();
    }

    // ---------------------------------------------------------------- running

    /// <summary>Advances the game by <paramref name="seconds"/> of play, in fixed steps.</summary>
    public void Tick(double seconds)
    {
        Accum += seconds;
        int guard = 0;
        while (Accum >= TickSeconds && guard++ < 100000)
        {
            Accum -= TickSeconds;
            Step(TickSeconds);
        }
    }

    void Step(double dt)
    {
        var o = W.Step(dt, Pile);
        W.Time += dt;
        Totals.Kills += o.PushKills + o.FarmKills;
        Totals.Gold += o.Gold;
        if (o.Levels > 0) Emit(new GameEvent(EventKind.Level, o.Levels));
        if (o.MasteryLevels > 0) Emit(new GameEvent(EventKind.Mastery, o.MasteryLevels));

        // A depth's boss drops the chest its kills filled; while farming, a depth's worth of kills fills one.
        Pile.Kills += o.PushKills + o.FarmKills;
        Chest dropped = null;
        if (o.Cleared > 0)
        {
            int depth = H.RunDepth;
            dropped = Pile.Seal(Combat.IsGuardian(depth) && !o.Skipped ? ChestKind.Guardian : ChestKind.Boss, depth);
            Emit(new GameEvent(EventKind.DepthCleared, depth, o.Skipped, data: dropped));
        }
        else if (Pile.Kills >= P.KillsPerDepth + P.BossReward) dropped = Pile.Seal(ChestKind.Monster, o.FarmDepth);
        if (dropped != null)
        {
            Totals.Chests++;
            Emit(new GameEvent(EventKind.ChestSealed, dropped.Depth, data: dropped));
        }

        if (Auto.OpenChests && Unlocked(Feature.AutoOpen) || _away) OpenUnheld();

        if (W.Time >= NextChores)
        {
            NextChores = W.Time + (_away ? 0 : 1.0);
            Chores();
        }
        NoteRarity();
    }

    /// <summary>The automatic spending, once a second (and every step while catching up).</summary>
    void Chores()
    {
        bool weights = false;
        void Weigh()
        {
            if (weights) return;
            S.UpdateWeights();
            weights = true;
        }
        bool changed = false;
        if (Auto.Talents && H.Talents.Points > 0)
        {
            Weigh();
            S.AllocateTalents();
            H.Rebuild(gear: false);
        }
        if (Auto.Forge && Unlocked(Feature.AutoForge) && !H.Gold.IsZero)
        {
            Weigh();
            changed |= S.SpendForge();
        }
        if (Auto.Ranks && Unlocked(Feature.AutoRanks) && H.Materials > 0)
        {
            Weigh();
            changed |= S.SpendRanks();
        }
        if (Auto.Focus && W.Time >= NextFocus)
        {
            NextFocus = W.Time + 60;
            Weigh();
            S.ChooseFocus();
        }
        if (changed) H.Rebuild();
        if (Auto.Ascend && Unlocked(Feature.Autopilot) && W.Time >= NextAutopilot)
        {
            NextAutopilot = W.Time + 60;
            Weigh();
            if (S.ChoosePrestige(transcend: false, Patience) == Strategy.Reset.Ascend) Ascend(auto: true);
        }
    }

    /// <summary>How long a run the autopilot takes as worth a 3% deeper reset (as the simulated player).</summary>
    public const double Patience = 1800;

    /// <summary>Resets when the sensible rule says so (ascending, or transcending when <paramref name="transcend"/>);
    /// returns what it did. The bot plays with this; the autopilot ascends only.</summary>
    public Strategy.Reset AutoPrestige(bool transcend)
    {
        S.UpdateWeights();
        var choice = S.ChoosePrestige(transcend, Patience);
        if (choice == Strategy.Reset.Ascend) Ascend(auto: true);
        else if (choice == Strategy.Reset.Transcend) Transcend(auto: true);
        return choice;
    }

    void NoteRarity()
    {
        while (_rarity < H.MaxRarity) Emit(new GameEvent(EventKind.RarityOpened, ++_rarity));
    }

    void Emit(GameEvent e)
    {
        if (_away) return;
        Events.Enqueue(e);
    }

    // ---------------------------------------------------------------- IWorldEvents

    public void Count(Ev e, double n = 1)
    {
        if (e == Ev.SlotUnlock) Emit(new GameEvent(EventKind.SlotsOpened, (int)n));
    }

    public void Record(int depth)
    {
        Totals.Record = Math.Max(Totals.Record, depth);
        Emit(new GameEvent(EventKind.Record, depth));
    }

    public void Upgrade(double score) => Totals.Upgrades++;

    public void Prestige(Ev kind, double seconds, int depth) { }

    // ---------------------------------------------------------------- chests

    /// <summary>Opens a chest: its best items go on (what's at least as good in every stat at once, trades if they leave
    /// the hero ahead), first finds go into the codex, the rest is salvaged for materials.</summary>
    public Opening Open(Chest chest, bool auto = false)
    {
        if (chest == null) return null;
        Pile.Waiting.Remove(chest);
        var o = new Opening { Chest = chest, Materials = chest.Materials, ReachBefore = H.Reach() };
        S.UpdateWeights();
        _openItems.Clear();
        _openItems.AddRange(chest.Items.OrderBy(x => x.Slot));
        _applied.Clear();
        S.Equip(_openItems);
        var cards = new Dictionary<Item, Card>();
        foreach (var (slot, item) in chest.Items)
        {
            var card = new Card { Slot = slot, Item = item };
            cards[item] = card;
            o.Cards.Add(card);
        }
        foreach (var (slot, picked, replaced, score) in _applied)
        {
            if (!cards.TryGetValue(picked, out var card)) continue;
            card.Equipped = true;
            card.Replaced = replaced;
            card.Score = score;
            o.Equipped++;
        }
        foreach (int key in chest.Found)
        {
            if (!H.Mark(key)) continue;
            o.NewEntries++;
            var (era, slot, rarity, band) = H.CodexEntry(key);
            var card = o.Cards.FirstOrDefault(c => !c.New && c.Slot == slot && c.Item.Rarity == rarity && Hero.Band(c.Item.Quality) == band && Hero.Era(c.Item.Ilvl) == era);
            if (card != null) card.New = true;
        }
        H.Materials += chest.Materials;
        o.Salvaged = Math.Max(0, chest.ItemCount - o.Equipped);
        H.Rebuild();
        o.ReachAfter = H.Reach();
        o.Cards.Sort((a, b) => a.Item.Rarity != b.Item.Rarity ? a.Item.Rarity.CompareTo(b.Item.Rarity) : a.Equipped.CompareTo(b.Equipped));
        Totals.Opened++;
        Totals.NewEntries += o.NewEntries;
        foreach (var c in o.Cards) Totals.Found[c.Item.Rarity]++;
        if (_away) _awaySummary?.Note(o);
        Emit(new GameEvent(EventKind.ChestOpened, o.Equipped, auto, data: o));
        return o;
    }

    /// <summary>The rarity from which a chest is held for the player (see <see cref="Automation.HoldAt"/>).</summary>
    public int HoldTier => Auto.HoldAt == Automation.HoldNewest ? H.MaxRarity : Auto.HoldAt;

    public bool Held(Chest c) => c.BestRarity >= HoldTier;

    /// <summary>Opens the waiting chests that aren't held; while away, all of them (the welcome back shows the best of
    /// what they held, so nothing waits that could have been helping).</summary>
    void OpenUnheld()
    {
        for (int i = 0; i < Pile.Waiting.Count;)
        {
            var c = Pile.Waiting[i];
            if (!_away && Held(c)) i++;
            else Open(c, auto: true);
        }
    }

    /// <summary>Opens every waiting chest; returns the openings.</summary>
    public List<Opening> OpenAll()
    {
        var list = new List<Opening>();
        while (Pile.Waiting.Count > 0) list.Add(Open(Pile.Next()));
        return list;
    }

    // ---------------------------------------------------------------- forge, ranks, focus

    /// <summary>Gold for <paramref name="levels"/> more forge levels in slot <paramref name="s"/>.</summary>
    public Big ForgeCost(int s, int levels = 1)
    {
        double g = P.ForgeGrowth;
        return H.ForgeCost(s) * ((Big.Pow(g, levels) - Big.One) / (g - 1));
    }

    /// <summary>How many forge levels slot <paramref name="s"/> can afford now.</summary>
    public int ForgeAffordable(int s)
    {
        if (!H.Open[s] || H.Gear[s] == null || H.Gold.IsZero) return 0;
        var c = H.ForgeCost(s);
        if (c > H.Gold) return 0;
        double g = P.ForgeGrowth;
        // Gold >= c (g^k - 1) / (g - 1)  =>  k <= log_g(1 + Gold (g - 1) / c)
        double k = Math.Floor((Math.Log10(1 + (H.Gold * (g - 1) / c).ToDouble())) / Math.Log10(g));
        if (double.IsInfinity(k) || k > 1e6) k = Math.Floor(((H.Gold * (g - 1) / c).Log10) / Math.Log10(g));
        int n = (int)Math.Max(0, Math.Min(1_000_000, k));
        while (n > 0 && ForgeCost(s, n) > H.Gold) n--;
        return n;
    }

    public bool BuyForge(int s, int levels = 1)
    {
        if (!H.Open[s] || H.Gear[s] == null || levels <= 0) return false;
        var cost = ForgeCost(s, levels);
        if (cost > H.Gold) return false;
        H.Gold -= cost;
        H.Forge[s] += levels;
        H.Rebuild();
        return true;
    }

    /// <summary>Spends the gold on forge levels where they're worth most.</summary>
    public bool ForgeAll()
    {
        S.UpdateWeights();
        bool bought = S.SpendForge();
        if (bought) H.Rebuild();
        return bought;
    }

    public bool RankUp(int s)
    {
        if (!H.Open[s]) return false;
        double cost = H.RankCost(s);
        if (cost > H.Materials) return false;
        H.Materials -= cost;
        H.Rank[s]++;
        H.Rebuild(gear: false);
        return true;
    }

    /// <summary>Spends the materials on ranks where they're worth most.</summary>
    public bool RankAll()
    {
        S.UpdateWeights();
        bool bought = S.SpendRanks();
        if (bought) H.Rebuild(gear: false);
        return bought;
    }

    public int FocusCount => H.Focus.Count(f => f);

    /// <summary>Focuses or unfocuses a worn slot (at most <see cref="Hero.FocusSlots"/>). A choice made by hand turns
    /// auto-focus off, so it isn't undone a minute later.</summary>
    public bool ToggleFocus(int s)
    {
        if (H.Focus[s])
        {
            H.Focus[s] = false;
            Auto.Focus = false;
            return true;
        }
        if (!H.Open[s] || H.Gear[s] == null || FocusCount >= H.FocusSlots) return false;
        H.Focus[s] = true;
        Auto.Focus = false;
        return true;
    }

    public void AutoFocus()
    {
        S.UpdateWeights();
        S.ChooseFocus();
    }

    // ---------------------------------------------------------------- talents

    public int TakeTalent(int id)
    {
        int n = H.Talents.Take(id, H.S);
        if (n > 0) H.Rebuild(gear: false);
        return n;
    }

    public int TakeParagon(bool damage, int count)
    {
        int n = H.Talents.TakeParagon(damage, count, H.S);
        if (n > 0) H.Rebuild(gear: false);
        return n;
    }

    public void AutoTalents()
    {
        S.UpdateWeights();
        S.AllocateTalents();
        H.Rebuild(gear: false);
    }

    public void RefundTalents()
    {
        H.Talents.Refund(H.S);
        H.Rebuild(gear: false);
    }

    // ---------------------------------------------------------------- prestige

    public Big AetherNow => W.AetherNow();
    public Big StardustNow => W.StardustNow();
    public bool CanAscend => W.AetherNow() >= Big.Of(P.AscendMinAether);
    public bool CanTranscend => W.StardustNow() >= Big.Of(P.TransMinStardust);

    /// <summary>How much deeper the aether this run has earned would take the hero, spent the sensible way.</summary>
    public double AscendPreview()
    {
        S.UpdateWeights();
        return S.Preview(H.Ascension, W.AetherNow());
    }

    /// <summary>Ascends: the run's aether is paid, its unopened chests are salvaged (their gear is for a run that's over),
    /// and a new run starts.</summary>
    public bool Ascend(bool auto = false)
    {
        if (!CanAscend) return false;
        var gain = W.AetherNow();
        int depth = H.RunDepth;
        Salvage();
        W.PayAscension(gain);
        W.ResetRun();
        S.UpdateWeights();
        if (Auto.SpendPrestige || auto) S.SpendLayer(H.Ascension);
        H.UpdateUnlocks();
        H.Rebuild();
        Totals.Ascensions++;
        Emit(new GameEvent(EventKind.Ascended, depth, auto, gain));
        if (H.Ascension.Resets == 1) Emit(new GameEvent(EventKind.Unlocked, data: "Auto-forge and auto-ranks"));
        return true;
    }

    /// <summary>Transcends: the stardust is paid, the ascension tree is wiped, the new cycle starts with the echo of the
    /// last one's aether, and a new run starts.</summary>
    public bool Transcend(bool auto = false)
    {
        if (!CanTranscend) return false;
        var gain = W.StardustNow();
        int depth = H.RunDepth;
        Salvage();
        var cycleAether = W.PayTranscendence();
        if (Auto.SpendPrestige || auto) S.SpendLayer(H.Transcendence);
        W.StartCycle(cycleAether);
        if (Auto.SpendPrestige || auto) S.SpendLayer(H.Ascension);
        W.ResetRun();
        S.UpdateWeights();
        H.UpdateUnlocks();
        H.Rebuild();
        Totals.Transcendences++;
        Emit(new GameEvent(EventKind.Transcended, depth, auto, gain));
        if (H.Transcendence.Resets == 1) Emit(new GameEvent(EventKind.Unlocked, data: "Autopilot"));
        return true;
    }

    /// <summary>Opens the waiting chests for their materials and first finds only (at a reset).</summary>
    void Salvage()
    {
        foreach (var c in Pile.Waiting)
        {
            H.Materials += c.Materials;
            foreach (int key in c.Found) H.Mark(key);
        }
        Pile.Waiting.Clear();
    }

    public bool BuyNode(LayerState layer, LayerNode n)
    {
        if (!layer.CanBuy(n, H.EverDepth)) return false;
        layer.Buy(n, H.S);
        H.UpdateUnlocks();
        H.Rebuild();
        return true;
    }

    public void SpendLayer(LayerState layer) => S.SpendLayer(layer);

    // ---------------------------------------------------------------- unlocks

    public bool Unlocked(Feature f) => f switch
    {
        Feature.AutoOpen => H.EverDepth >= 10 || H.Ascension.Resets > 0,
        Feature.Forge => H.EverDepth >= 3 || H.Ascension.Resets > 0,
        Feature.Ranks => H.EverDepth >= 25 || H.Ascension.Resets > 0,
        Feature.Mastery => H.EverDepth >= 40 || H.Ascension.Resets > 0,
        Feature.Talents => H.Level >= 2 || H.Ascension.Resets > 0,
        Feature.Codex => H.CodexCount >= 8,
        Feature.AutoForge or Feature.AutoRanks => H.Ascension.Resets > 0 || H.Transcendence.Resets > 0,
        Feature.Ascension => H.EverDepth >= P.AscendDepth || H.Ascension.Resets > 0,
        Feature.Transcendence => H.Ascension.Lifetime + W.AetherNow() >= Big.Of(P.TransAether) * 0.1 || H.Transcendence.Resets > 0,
        Feature.Autopilot => H.Transcendence.Resets > 0,
        _ => true,
    };

    // ---------------------------------------------------------------- away

    AwaySummary _awaySummary;

    /// <summary>Plays <paramref name="seconds"/> the player was away, with the automation they have (chests open
    /// themselves meanwhile), in steps that grow with the time away. Returns what happened.</summary>
    public AwaySummary CatchUp(double seconds)
    {
        var sum = new AwaySummary
        {
            Seconds = seconds, DepthBefore = H.RunDepth, RecordBefore = H.EverDepth,
        };
        int levels = H.Level, ascensions = H.Ascension.Resets;
        long opened = Totals.Opened, upgrades = Totals.Upgrades, entries = Totals.NewEntries;
        double kills = Totals.Kills;
        var gold = Totals.Gold;
        _away = true;
        _awaySummary = sum;
        try
        {
            double end = W.Time + seconds;
            // Up to a day in steps of a minute; beyond that ten minutes (a week is about 1,900 steps).
            while (W.Time < end - 1e-6)
            {
                double dt = Math.Min(end - W.Time, seconds > 86400 ? 600 : 60);
                Step(dt);
            }
        }
        finally
        {
            _away = false;
            _awaySummary = null;
        }
        sum.DepthAfter = H.RunDepth;
        sum.RecordAfter = H.EverDepth;
        sum.Levels = H.Level - levels;
        sum.Ascensions = H.Ascension.Resets - ascensions;
        sum.Chests = (int)(Totals.Opened - opened);
        sum.Equipped = (int)(Totals.Upgrades - upgrades);
        sum.NewEntries = (int)(Totals.NewEntries - entries);
        sum.Kills = Totals.Kills - kills;
        sum.Gold = Totals.Gold - gold;
        _rarity = H.MaxRarity;
        return sum;
    }

    // ---------------------------------------------------------------- the fight, for the screen

    /// <summary>What the hero is fighting now and how it's going, for the battle to show.</summary>
    public FightInfo Fight()
    {
        var b = H.Base;
        var fi = new FightInfo();
        int next = H.RunDepth + 1;
        fi.Pushing = H.Fight.CanClear(H.Total, b, next);
        if (fi.Pushing)
        {
            var f = H.Fight.At(H.Total, b, next);
            fi.Depth = next;
            fi.Hero = f;
            fi.Guardian = Combat.IsGuardian(next);
            fi.KillTime = H.Fight.KillTime(f, next);
            fi.KillRate = H.Fight.KillRate(f, next);
            fi.BossTime = Math.Min(P.BossTime, H.Fight.Boss(f, next, fi.Guardian).Time);
            fi.BossDamage = H.Fight.Boss(f, next, fi.Guardian).Damage;
            fi.WaveDamage = H.Fight.WaveDamage(f, next);
            fi.Skip = W.PlannedSkip(f, next);
            if (fi.Skip > 1)
            {
                // A wave and a boss at the animation floor clear them all (World.Step).
                fi.Need = H.Fight.SkipTime;
                fi.WaveShare = (P.WaveSize * P.MinKillTime + P.WaveGap) / fi.Need;
                fi.Guardian = false;
            }
            else
            {
                double waves = P.KillsPerDepth / fi.KillRate;
                fi.Need = waves + fi.BossTime;
                fi.WaveShare = waves / fi.Need;
            }
            fi.Progress = W.DepthProgress;
        }
        else
        {
            int farm = H.Fight.FarmDepth(H.Total, b, H.RunDepth);
            var f = H.Fight.At(H.Total, b, farm);
            fi.Depth = farm;
            fi.Hero = f;
            fi.KillTime = H.Fight.KillTime(f, farm);
            fi.KillRate = H.Fight.KillRate(f, farm);
            fi.WaveDamage = H.Fight.WaveDamage(f, farm);
            fi.Skip = 1;
            fi.BlockedBy = Combat.IsGuardian(next) ? "guardian" : "boss";
            // What the next depth asks: its boss in time, its waves and its boss without falling.
            var fn = H.Fight.At(H.Total, b, next);
            var (time, damage) = H.Fight.Boss(fn, next, Combat.IsGuardian(next));
            double waves = H.Fight.WaveDamage(fn, next);
            fi.NeedDamage = time / P.BossTime;
            fi.NeedHealth = Math.Max(waves, damage) / P.PushWaveDamage;
        }
        fi.Next = next;
        fi.MonsterHpLe = H.Fight.MonsterHpLe(fi.Depth);
        fi.MonsterDpsLe = H.Fight.MonsterDpsLe(fi.Depth);
        fi.Speed = b.Speed;
        fi.CritDamage = b.CritDamage;
        return fi;
    }
}

public enum Feature : byte { AutoOpen, Forge, Ranks, Mastery, Talents, Codex, AutoForge, AutoRanks, Ascension, Transcendence, Autopilot }

/// <summary>What the battle shows: where the hero fights and how fast, and how a push through the next depth is going.</summary>
public struct FightInfo
{
    /// <summary>Whether the hero is fighting through the next depth (else farming the deepest comfortable one).</summary>
    public bool Pushing;
    public int Depth;
    public bool Guardian;
    public Combat.Frame Hero;
    public double KillTime, KillRate, BossTime, BossDamage, WaveDamage, Speed;
    /// <summary>The push's progress through the depth (0 to 1), its length in seconds and the share of it that's waves
    /// (the boss is the rest).</summary>
    public double Progress, Need, WaveShare;
    /// <summary>Depths cleared at a time by overkill (1 when there's none).</summary>
    public int Skip;
    public double MonsterHpLe, MonsterDpsLe;
    /// <summary>A critical blow's extra damage (0.5: half as much again).</summary>
    public double CritDamage;
    /// <summary>The depth after the deepest cleared this run.</summary>
    public int Next;
    /// <summary>What stops the push while farming: the next depth's "boss" or "guardian".</summary>
    public string BlockedBy;
    /// <summary>While farming: how many times the damage the next boss needs (its time over the limit), and how many
    /// times the health its waves and boss need (their share of health over the most a push may take). A push goes
    /// ahead once both are at most 1.</summary>
    public double NeedDamage, NeedHealth;
}

/// <summary>The game's running totals.</summary>
public sealed class Tally
{
    public double Kills;
    public Big Gold = Big.Zero;
    public long Chests, Opened, Upgrades, NewEntries, Ascensions, Transcendences;
    public int Record;
    public readonly long[] Found = new long[Content.RarityNames.Length];
}

static class AwayNotes
{
    /// <summary>Keeps the rarest finds of an opening for the welcome-back summary.</summary>
    public static void Note(this AwaySummary sum, Opening o)
    {
        foreach (var c in o.Cards)
        {
            sum.Found[c.Item.Rarity]++;
            if (sum.Best.Count < 8 || c.Item.Rarity > sum.Best[^1].Item.Rarity)
            {
                sum.Best.Add(c);
                sum.Best.Sort((a, b) => b.Item.Rarity.CompareTo(a.Item.Rarity));
                if (sum.Best.Count > 8) sum.Best.RemoveAt(8);
            }
        }
    }
}
