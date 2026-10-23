using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>Something the screen should show, in the order it happened (battle events are game events too).</summary>
public abstract record GameEvent;

public sealed record GoldGained(double Amount, Fighter From) : GameEvent;
public sealed record ChestGained(ChestKind Kind, Fighter From) : GameEvent;
public sealed record BossAvailable : GameEvent;
public sealed record BossStarted(Fighter Boss) : GameEvent;
public sealed record BossFailed(bool TimedOut) : GameEvent;
public sealed record StageCleared(int Stage, bool ZoneBoss) : GameEvent;
public sealed record StageEntered(int Stage, bool NewZone) : GameEvent;
public sealed record Retreated(int Stage) : GameEvent;
public sealed record MimicAppeared(Fighter Mimic) : GameEvent;
public sealed record FeatureUnlocked(string Key) : GameEvent;
public sealed record AutoOpened(Opening Opening, bool Equipped) : GameEvent;

/// <summary>A bounty was finished (its chests are in the pile) and <paramref name="Next"/> took its place on the board.</summary>
public sealed record BountyDone(Bounty Bounty, int Slot, Bounty Next) : GameEvent;

/// <summary>Auto-open found something at least as rare as its stop setting, and left it waiting for the player.</summary>
public sealed record AutoHeld(Opening Opening) : GameEvent;

/// <summary>
/// A chest just opened: what came out (and the rarity it rolled before luck bumped it), what's in that slot now, and
/// the knight's power before and with it.
/// </summary>
public sealed record Opening(ChestKind Kind, Item Item, Rarity Rolled, Item Current, double PowerNow, double PowerWith)
{
    public bool Lucky => Item.Rarity > Rolled;
    public bool Upgrade => Current == null || PowerWith > PowerNow * 1.0001;
    public double PowerDelta => PowerWith - PowerNow;
}

/// <summary>What the knight did while the game was closed: <paramref name="ChestGold"/> is the part of the gold paid for chests past the cap.</summary>
public sealed record OfflineReport(double Seconds, int Kills, double Gold, int[] Chests, double ChestGold);

/// <summary>
/// The whole game: where the knight is, what he wears, his gold and chests, the upgrades bought, and the battle. The
/// screen calls <see cref="Update"/> every frame and drains <see cref="Events"/>; the simulation does the same without
/// a screen. Engine-free, so it can run headless.
/// </summary>
public sealed class GameState
{
    public Rng Rng { get; private set; }
    public int Seed { get; private set; }

    /// <summary>The stage being fought, and the furthest reached.</summary>
    public int Stage = 1, MaxStage = 1;

    /// <summary>Monsters slain at this stage toward its boss.</summary>
    public int Kills;

    public double Gold;
    public readonly int[] Chests = new int[Core.Chests.Count];
    public readonly Item[] Equipped = new Item[Slots.Count];

    public int ChestLevel = 1, Might, Vigor, Haste;
    public bool HasAutoOpen;

    /// <summary>Player switches: fight bosses as soon as they're ready; open chests automatically (once bought).</summary>
    public bool AutoBoss = true, AutoOpen = true;

    /// <summary>Auto-open stops and shows anything this rare or better.</summary>
    public Rarity AutoStopAt = Rarity.Epic;

    /// <summary>One-time tips already shown (the screen's, kept with the save).</summary>
    public readonly HashSet<string> TipsSeen = new();

    /// <summary>True the first time a tip is asked for (and marks it seen).</summary>
    public bool FirstTime(string tip) => TipsSeen.Add(tip);

    /// <summary>The chest just opened, waiting for Equip or Sell.</summary>
    public Opening Pending { get; private set; }

    // Records, for the title screen and the simulation.
    public double PlayTime;
    public long TotalKills, ChestsOpened, BossesBeaten, BossFails, BossTimeouts, Deaths, UpgradesEquipped;

    /// <summary>Things found that were rare or better.</summary>
    public long RareFinds => Found[(int)Rarity.Rare] + Found[(int)Rarity.Epic] + Found[(int)Rarity.Legendary] + Found[(int)Rarity.Mythic];

    /// <summary>The bounty board (see <see cref="Bounties"/>), and how many bounties have been posted so far.</summary>
    public readonly List<Bounty> Board = new();
    public int BountiesPosted, BountiesDone;
    public readonly long[] Found = new long[Rarities.Count];
    public double GoldEarned;

    public HeroStats Stats { get; private set; }
    public Battle Battle { get; private set; }
    public readonly List<GameEvent> Events = new();

    /// <summary>Whether the screen wants events (the simulation doesn't, and skips collecting them).</summary>
    public bool CollectEvents = true;

    double _waveGap, _bossCooldown, _powerAtFail, _autoTimer, _climbWait, _powerAtRetreat;
    bool _bossRequested, _farmAfterRevive;
    int _deathsHere;

    public static GameState New(int seed)
    {
        var g = new GameState { Rng = new Rng(seed), Seed = seed };
        g.Chests[(int)ChestKind.Wooden] = Balance.StartChests;
        g.Begin();
        g.FillBoard();
        return g;
    }

    void Begin()
    {
        Recompute();
        Battle = new Battle(Rng, Stats);
        SpawnWave();
    }

    public Training Training => new(Might, Vigor, Haste);

    public double Power => Stats.Power(MaxStage);

    public bool BossReady => Stage == MaxStage && Kills >= Balance.KillsPerStage;
    public bool InBossFight => Battle.BossFight && Battle.InFight;
    public double BossCooldown => _bossCooldown;
    public int ChestTotal
    {
        get
        {
            int n = 0;
            foreach (var c in Chests) n += c;
            return n;
        }
    }

    /// <summary>The best kind of chest waiting (they're opened best first), or null if there are none.</summary>
    public ChestKind? NextChest
    {
        get
        {
            for (int k = Core.Chests.Count - 1; k >= 0; k--)
                if (Chests[k] > 0) return (ChestKind)k;
            return null;
        }
    }

    public Item Worn(Slot slot) => Equipped[(int)slot];

    void Recompute()
    {
        Stats = HeroStats.Compute(Equipped, Training);
        Battle?.SetStats(Stats);
    }

    // ------------------------------------------------------------------------------------------------- time passing

    /// <summary>The longest slice of time the battle is stepped by (the simulation uses coarser ones).</summary>
    public static double TickSize = 1.0 / 30;

    public void Update(double dt)
    {
        PlayTime += dt;
        while (dt > 1e-9)
        {
            double s = Math.Min(TickSize, dt);
            dt -= s;
            Tick(s);
        }
    }

    void Tick(double dt)
    {
        Battle.Step(dt);
        // By index: handling one (a boss beaten, the knight down) can start a wave, which adds more.
        for (int i = 0; i < Battle.Events.Count; i++) Handle(Battle.Events[i]);
        Battle.Events.Clear();

        if (_bossCooldown > 0) _bossCooldown -= dt;
        if (_climbWait > 0) _climbWait -= dt;

        if (!Battle.InFight && Battle.DownFor <= 0)
        {
            _waveGap += dt;
            if (_waveGap >= Balance.WaveGap)
            {
                _waveGap = 0;
                NextWave();
            }
        }

        CheckBounties();

        if (HasAutoOpen && AutoOpen && Pending == null && ChestTotal > 0)
        {
            _autoTimer += dt;
            if (_autoTimer >= Balance.AutoOpenEvery)
            {
                _autoTimer = 0;
                AutoOpenOne();
            }
        }
    }

    void Emit(GameEvent e)
    {
        if (CollectEvents) Events.Add(e);
    }

    void Handle(GameEvent e)
    {
        Emit(e);
        switch (e)
        {
            case FighterDied { Fighter: var f } when !f.IsHero:
                OnKill(f);
                break;
            case HeroDown:
                OnHeroDown();
                break;
            case HeroRevived when _farmAfterRevive:
                _farmAfterRevive = false;
                SpawnWave();
                break;
            case BossTimedOut:
                BossOver(timedOut: true);
                break;
        }
    }

    void NextWave()
    {
        _deathsHere = 0;
        if (BossReady && (_bossRequested || AutoBoss && BossRetryReady))
        {
            StartBoss();
            return;
        }
        // Back below the furthest stage (after a retreat): ten kills, and once he's a little stronger (or has waited a
        // while), the knight moves up again.
        if (Stage < MaxStage && Kills >= Balance.KillsPerStage && (_climbWait <= 0 || Power >= _powerAtRetreat * Balance.BossRetryPower))
        {
            Stage++;
            Kills = 0;
            Emit(new StageEntered(Stage, World.StageInZone(Stage) == 1));
        }
        SpawnWave();
    }

    /// <summary>Auto-Boss may try again: long enough after a loss, and stronger than then (or it's been a long while).</summary>
    bool BossRetryReady => _bossCooldown <= 0 ||
                           _bossCooldown <= Balance.BossRetryMax - Balance.BossRetryMin && Power >= _powerAtFail * Balance.BossRetryPower;

    void SpawnWave()
    {
        _waveGap = 0;
        if (Stage >= 3 && Rng.Chance(Balance.MimicChance))
        {
            Battle.Spawn(new[] { World.Monster("mimic") }, Stage, mimic: true, bossName: "Mimic");
            Emit(new MimicAppeared(Battle.Foes[0]));
            return;
        }
        Battle.Spawn(World.Wave(Rng, Stage), Stage);
    }

    /// <summary>The boss button: fight the stage's boss now (when it's ready).</summary>
    public bool ChallengeBoss()
    {
        if (!BossReady || InBossFight || Battle.DownFor > 0) return false;
        StartBoss();
        return true;
    }

    void StartBoss()
    {
        _bossRequested = false;
        _waveGap = 0;
        bool zone = World.IsZoneBoss(Stage);
        Battle.Spawn(new[] { World.Boss(Stage) }, Stage, boss: true, zoneBoss: zone, bossName: World.BossName(Stage));
        Emit(new BossStarted(Battle.Foes[0]));
    }

    void OnKill(Fighter f)
    {
        TotalKills++;
        double gold = Balance.GoldPerKill(Stage) * (1 + Stats.GoldFind) *
                      (f.Boss ? f.ZoneBoss ? Balance.ZoneBossGold : Balance.BossGold : f.Mimic ? Balance.MimicGold : 1);
        AddGold(gold, f);

        if (f.Boss)
        {
            if (f.ZoneBoss)
            {
                GiveChest(ChestKind.Golden, f);
                GiveChest(ChestKind.Iron, f);
                GiveChest(ChestKind.Iron, f);
            }
            else
            {
                GiveChest(ChestKind.Iron, f);
                GiveChest(ChestKind.Wooden, f);
            }
            BossBeaten();
            return;
        }
        if (f.Mimic)
        {
            GiveChest(ChestKind.Royal, f);
            GiveChest(ChestKind.Golden, f);
        }
        else if (TotalKills <= Balance.SureDrops || Rng.Chance(Math.Min(1, Balance.ChestChance * (1 + Stats.ChestFind))))
        {
            double roll = Rng.NextDouble();
            GiveChest(roll < Balance.GoldenShare ? ChestKind.Golden : roll < Balance.GoldenShare + Balance.IronShare ? ChestKind.Iron : ChestKind.Wooden, f);
        }
        if (Stage == MaxStage && Kills < Balance.KillsPerStage)
        {
            Kills++;
            if (Kills == Balance.KillsPerStage) Emit(new BossAvailable());
        }
        else if (Stage < MaxStage) Kills = Math.Min(Balance.KillsPerStage, Kills + 1);
    }

    void AddGold(double amount, Fighter from)
    {
        Gold += amount;
        GoldEarned += amount;
        Emit(new GoldGained(amount, from));
    }

    void GiveChest(ChestKind kind, Fighter from)
    {
        Chests[(int)kind]++;
        Emit(new ChestGained(kind, from));
    }

    void BossBeaten()
    {
        BossesBeaten++;
        bool zone = World.IsZoneBoss(Stage);
        Emit(new StageCleared(Stage, zone));
        Stage++;
        MaxStage = Math.Max(MaxStage, Stage);
        Kills = 0;
        _bossCooldown = 0;
        Emit(new StageEntered(Stage, World.StageInZone(Stage) == 1));
        if (MaxStage == Balance.AutoOpenStage && !HasAutoOpen) Emit(new FeatureUnlocked("autoopen"));
    }

    /// <summary>A boss fight lost: the knight fell, or time ran out. Back to the stage's monsters for a while.</summary>
    void BossOver(bool timedOut)
    {
        BossFails++;
        if (timedOut) BossTimeouts++;
        Emit(new BossFailed(timedOut));
        _bossCooldown = Balance.BossRetryMax;
        _powerAtFail = Power;
        if (timedOut) SpawnWave();
        else _farmAfterRevive = true;
    }

    void OnHeroDown()
    {
        Deaths++;
        if (Battle.BossFight)
        {
            BossOver(timedOut: false);
            return;
        }
        // Falling twice at a stage without clearing a wave: step back a stage (he climbs again after ten kills).
        _deathsHere++;
        if (_deathsHere >= 2 && Stage > 1)
        {
            _deathsHere = 0;
            Stage--;
            Kills = 0;
            _climbWait = Balance.BossRetryMax;
            _powerAtRetreat = Power;
            Emit(new Retreated(Stage));
            _farmAfterRevive = true;
        }
    }

    // ------------------------------------------------------------------------------------------------- bounties

    /// <summary>Posts bounties until the board is full.</summary>
    internal void FillBoard()
    {
        while (Board.Count < Bounties.Active) Board.Add(Bounties.Make(this, BountiesPosted++, Board));
    }

    /// <summary>Pays out any bounty that's done and posts the next in its place.</summary>
    void CheckBounties()
    {
        for (int i = 0; i < Board.Count; i++)
        {
            var b = Board[i];
            if (!b.Done(this)) continue;
            BountiesDone++;
            for (int c = 0; c < b.Count; c++) GiveChest(b.Reward, null);
            var rest = Board.Where((_, j) => j != i).ToList();
            var next = Bounties.Make(this, BountiesPosted++, rest);
            Board[i] = next;
            Emit(new BountyDone(b, i, next));
        }
    }

    // ------------------------------------------------------------------------------------------------- chests and gear

    /// <summary>Opens the best chest waiting (or one of <paramref name="kind"/>); null if none, or one is still waiting for a decision.</summary>
    public Opening Open(ChestKind? kind = null)
    {
        if (Pending != null) return null;
        var k = kind ?? NextChest;
        if (k == null || Chests[(int)k.Value] <= 0) return null;
        Chests[(int)k.Value]--;
        ChestsOpened++;

        var def = Core.Chests.Get(k.Value);
        var rolled = Core.Chests.Roll(Rng, ChestLevel, k.Value);
        var rarity = rolled < Rarity.Mythic && Rng.Chance(Stats.Luck) ? Rarities.Up(rolled) : rolled;
        var slot = (Slot)Rng.Range(0, Slots.Count);
        var item = ItemGen.Roll(Rng, slot, rarity, Balance.ItemLevel(ChestLevel, k.Value, MaxStage) + Rng.Weighted(Balance.LevelSpread));
        Found[(int)rarity]++;

        var current = Worn(slot);
        var with = (Item[])Equipped.Clone();
        with[(int)slot] = item;
        double now = Power, after = HeroStats.Compute(with, Training).Power(MaxStage);
        Pending = new Opening(k.Value, item, rolled, current, now, after);
        return Pending;
    }

    /// <summary>
    /// For the opening demo: opens a chest of <paramref name="kind"/> (it needn't be in the pile) that comes out
    /// <paramref name="rarity"/>, rolled a step lower and bumped by luck if <paramref name="lucky"/>.
    /// </summary>
    public Opening OpenAs(Rarity rarity, ChestKind kind = ChestKind.Wooden, bool lucky = false)
    {
        if (Pending != null) return null;
        var slot = (Slot)Rng.Range(0, Slots.Count);
        var item = ItemGen.Roll(Rng, slot, rarity, Balance.ItemLevel(ChestLevel, kind, MaxStage));
        var with = (Item[])Equipped.Clone();
        with[(int)slot] = item;
        var rolled = lucky && rarity > Rarity.Common ? (Rarity)((int)rarity - 1) : rarity;
        Pending = new Opening(kind, item, rolled, Worn(slot), Power, HeroStats.Compute(with, Training).Power(MaxStage));
        return Pending;
    }

    /// <summary>Puts the waiting item on; whatever it replaces is sold. Returns the gold from that sale.</summary>
    public double Equip()
    {
        if (Pending == null) return 0;
        var item = Pending.Item;
        var old = Worn(item.Slot);
        if (Pending.Upgrade) UpgradesEquipped++;
        Equipped[(int)item.Slot] = item;
        Pending = null;
        Recompute();
        if (old == null) return 0;
        Gold += old.SellValue;
        GoldEarned += old.SellValue;
        return old.SellValue;
    }

    /// <summary>Sells the waiting item. Returns the gold.</summary>
    public double Sell()
    {
        if (Pending == null) return 0;
        double value = Pending.Item.SellValue;
        Gold += value;
        GoldEarned += value;
        Pending = null;
        return value;
    }

    void AutoOpenOne()
    {
        var o = Open();
        if (o == null) return;
        // Something big: leave it waiting, so the player gets the moment.
        if (o.Item.Rarity >= AutoStopAt)
        {
            Emit(new AutoHeld(o));
            return;
        }
        bool equip = o.Upgrade;
        if (equip) Equip();
        else Sell();
        Emit(new AutoOpened(o, equip));
    }

    // ------------------------------------------------------------------------------------------------- upgrades

    public bool Buy(Upgrade u)
    {
        if (!Upgrades.Open(this, u)) return false;
        double cost = Upgrades.Cost(this, u);
        if (Gold < cost) return false;
        Gold -= cost;
        switch (u)
        {
            case Upgrade.ChestLevel: ChestLevel++; break;
            case Upgrade.Might: Might++; break;
            case Upgrade.Vigor: Vigor++; break;
            case Upgrade.Haste: Haste++; break;
            case Upgrade.AutoOpen: HasAutoOpen = true; break;
        }
        Recompute();
        return true;
    }

    // ------------------------------------------------------------------------------------------------- time away

    /// <summary>
    /// The knight kept farming while the game was closed: a short fight here measures how fast he kills at this stage,
    /// and the time away (up to 8 hours) pays out at three quarters of that, in gold and chests. Null under a minute.
    /// </summary>
    public OfflineReport ApplyOffline(double seconds)
    {
        if (seconds < 60) return null;
        seconds = Math.Min(seconds, Balance.OfflineCap);
        var rng = new Rng(Seed ^ (int)TotalKills);
        var trial = new Battle(rng, Stats);
        int kills = 0;
        const double sample = 90, step = 1.0 / 30;
        double gap = 0;
        trial.Spawn(World.Wave(rng, Stage), Stage);
        for (double t = 0; t < sample; t += step)
        {
            trial.Step(step);
            foreach (var e in trial.Events)
                if (e is FighterDied { Fighter.IsHero: false }) kills++;
            trial.Events.Clear();
            if (!trial.InFight && trial.DownFor <= 0 && (gap += step) >= Balance.WaveGap)
            {
                gap = 0;
                trial.Spawn(World.Wave(rng, Stage), Stage);
            }
        }
        int total = (int)(kills / sample * seconds * Balance.OfflineRate);
        double gold = total * Balance.GoldPerKill(Stage) * (1 + Stats.GoldFind);
        double chestRate = Math.Min(1, Balance.ChestChance * (1 + Stats.ChestFind));
        int chests = (int)Math.Round(total * chestRate);
        double chestGold = 0;
        if (chests > Balance.OfflineChests)
        {
            chestGold = (chests - Balance.OfflineChests) * ExpectedSale();
            gold += chestGold;
            chests = Balance.OfflineChests;
        }
        var got = new int[Core.Chests.Count];
        got[(int)ChestKind.Golden] = (int)Math.Round(chests * Balance.GoldenShare);
        got[(int)ChestKind.Iron] = (int)Math.Round(chests * Balance.IronShare);
        got[(int)ChestKind.Wooden] = chests - got[1] - got[2];
        for (int k = 0; k < got.Length; k++) Chests[k] += got[k];
        Gold += gold;
        GoldEarned += gold;
        TotalKills += total;
        return new OfflineReport(seconds, total, gold, got, chestGold);
    }

    /// <summary>What a wooden chest's item sells for on average at the current chest level.</summary>
    public double ExpectedSale()
    {
        var odds = Core.Chests.Odds(ChestLevel, ChestKind.Wooden);
        double mult = 0;
        for (int r = 0; r < odds.Length; r++) mult += odds[r] * Rarities.All[r].SellMult;
        return Balance.SellBase * Balance.GearScale(Balance.ItemLevel(ChestLevel, ChestKind.Wooden, MaxStage)) * mult;
    }

    // ------------------------------------------------------------------------------------------------- saving

    public SaveData Save() => SaveData.From(this);

    public static GameState Load(SaveData data)
    {
        var g = new GameState { Rng = new Rng(data.Seed + (int)(data.ChestsOpened % 100000)), Seed = data.Seed };
        data.ApplyTo(g);
        g.Begin();
        g.FillBoard();
        if (data.Pending != null) g.RestorePending(data.Pending.ToItem(), (ChestKind)data.PendingKind);
        return g;
    }

    /// <summary>Restores a chest that was opened but not decided on when the game was saved.</summary>
    internal void RestorePending(Item item, ChestKind kind)
    {
        var current = Worn(item.Slot);
        var with = (Item[])Equipped.Clone();
        with[(int)item.Slot] = item;
        Pending = new Opening(kind, item, item.Rarity, current, Power, HeroStats.Compute(with, Training).Power(MaxStage));
    }
}
