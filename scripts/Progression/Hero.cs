using System;
using System.Collections.Generic;

namespace LootFall.Progression;

/// <summary>An item: its level, rarity, quality roll and affix rolls. Its numbers are computed from these and the slot
/// it's in (Hero.Contribute), never stored, so the same item reads right wherever the formulas move.</summary>
public sealed class Item
{
    public const int MaxAffixes = 12;
    public double Ilvl;
    public int Rarity;
    public double Quality;
    public int Affixes;
    public readonly Stat[] Stats = new Stat[MaxAffixes];
    public readonly double[] Rolls = new double[MaxAffixes];

    public void CopyFrom(Item o)
    {
        Ilvl = o.Ilvl;
        Rarity = o.Rarity;
        Quality = o.Quality;
        Affixes = o.Affixes;
        Array.Copy(o.Stats, Stats, MaxAffixes);
        Array.Copy(o.Rolls, Rolls, MaxAffixes);
    }
}

/// <summary>
/// Everything the hero has, from gear to prestige layers, and how it adds up to one sheet of stats. The systems and what
/// resets them:
/// <list type="bullet">
/// <item>Gear (about 550 slots, docs/equipment.md): the best item found for each kind of piece, by depth, rarity,
/// quality and affixes; only what's worn counts (<see cref="Worn"/>, <see cref="Active"/>). Reset by ascension.</item>
/// <item>Forge (gold, per slot): +ForgeStep a level to the slot's flat stats, exponential cost; gold scales with depth,
/// so forge levels track depth (a "slope" system). Reset by ascension.</item>
/// <item>Slot ranks (materials, per slot): polynomial cost; materials are counted, not scaled, so ranks grow with time
/// played (a power law). Kept.</item>
/// <item>Mastery (time, per slot): XP an hour while the slot is worn, polynomial curve; a power law in time. Kept.</item>
/// <item>Ranks and mastery feed the hero as a whole, not the slot's item: flat stats add up across slots, so a bonus that
/// grows without bound on one slot's flats would make that slot all that matters (sum of exponentials). As a pool, every
/// level in every slot is worth the same, and a newly opened slot is the cheapest to raise.</item>
/// <item>Levels and talents (XP): a point a level into the 3,259-node tree, then paragon. Kept.</item>
/// <item>Codex (first finds of slot x rarity x quality band): a little "more" each. Kept.</item>
/// <item>Ascension and transcendence: prestige layers with their own trees.</item>
/// </list>
/// </summary>
public sealed class Hero
{
    public readonly Params P;
    public readonly Content C;
    public readonly Scale S;
    public readonly Combat Fight;
    public readonly TalentTree Tree;
    public readonly TalentState Talents;
    public readonly LayerState Ascension, Transcendence;

    /// <summary>ReferenceSlots / slot count: what each rank, mastery level and codex entry is worth, relative to its
    /// budgeted value.</summary>
    public readonly double PoolScale;

    /// <summary>The best item found for each slot (kept whether or not it's worn).</summary>
    public readonly Item[] Gear;
    public readonly int[] Forge, Rank, Mastery;
    public readonly double[] MasteryXp;
    public readonly bool[] Open, Focus;
    /// <summary>Pieces put on: on the body, or set in a board (a board's pieces stay set while its host is off).</summary>
    public readonly bool[] Worn;
    /// <summary>Pieces that count now: worn, open, with an item, and (on a board) the board's host counts and opens
    /// every cell they fill. See <see cref="UpdateActive"/>.</summary>
    public readonly bool[] Active;
    /// <summary>Each board's host now (a slot index, -1 when nothing worn brings it).</summary>
    public readonly int[] HostOf;
    public int OpenCount;

    public const int QualityBands = 4, Eras = 16;

    /// <summary>The codex era of an item level: 0 below 100, then one per doubling.</summary>
    public static int Era(double ilvl) => ilvl < 100 ? 0 : Math.Min(Eras - 1, 1 + (int)Math.Log2(ilvl / 100));
    public readonly bool[] Codex;
    public int CodexCount;
    /// <summary>The codex entries found, each weighted by its rarity (CodexRarityGrowth^rarity).</summary>
    public double CodexValue;

    public int RunDepth = 1, EverDepth = 1, Level = 1;
    public Big Xp = Big.Zero, Gold = Big.Zero;
    public double Materials;
    public int MaxRarity;

    /// <summary>A system switched off for a knockout measurement (Sys.Count means none).</summary>
    public Sys Knock = Sys.Count;

    /// <summary>The hero's stats from each source, and in total.</summary>
    public readonly Sheet GearSheet = new(), SlotSheet = new(), CodexSheet = new(), Total = new();
    public Combat.Base Base;

    // Precomputed LE constants.
    readonly double _forgeStepLe, _damageBudget, _hpBudget, _armorBudget, _ratingBudget, _affixShare;
    readonly double[] _slotWeightLe, _slotDamageLe, _slotHpLe, _slotArmorLe;

    public Hero(Params p, Content c, TalentTree tree)
    {
        P = p;
        C = c;
        S = new Scale(p.R);
        Fight = new Combat(p);
        Tree = tree;
        Talents = new TalentState(tree, p);
        Ascension = new LayerState(LayerTree.Ascension(p));
        Transcendence = new LayerState(LayerTree.Transcendence(p));
        int n = c.SlotCount;
        PoolScale = p.ReferenceSlots / n;
        Gear = new Item[n];
        Forge = new int[n];
        Rank = new int[n];
        Mastery = new int[n];
        MasteryXp = new double[n];
        Open = new bool[n];
        Focus = new bool[n];
        Worn = new bool[n];
        Active = new bool[n];
        HostOf = new int[c.Boards.Length];
        Codex = new bool[n * Content.RarityNames.Length * QualityBands * Eras];
        _forgeStepLe = S.Le(1 + p.ForgeStep);
        _damageBudget = S.Le(p.DamagePerBudget);
        _hpBudget = S.Le(p.HpPerBudget);
        _armorBudget = S.Le(p.ArmorPerBudget);
        _ratingBudget = S.Le(p.RatingPerBudget);
        _affixShare = S.Le(p.FlatAffixShare);
        _slotWeightLe = new double[n];
        _slotDamageLe = new double[n];
        _slotHpLe = new double[n];
        _slotArmorLe = new double[n];
        for (int s = 0; s < n; s++)
        {
            var d = c.Slots[s];
            _slotWeightLe[s] = S.Le(d.Weight);
            _slotDamageLe[s] = d.Offense > 0 ? S.Le(d.Offense) + _damageBudget : Scale.None;
            _slotHpLe[s] = d.Offense < 1 ? S.Le(1 - d.Offense) + _hpBudget : Scale.None;
            _slotArmorLe[s] = d.Offense < 1 ? S.Le(1 - d.Offense) + _armorBudget : Scale.None;
        }
        UpdateUnlocks();
        Rebuild();
    }

    // ---------------------------------------------------------------- unlocks

    /// <summary>Opens the slots and rarity tiers the deepest depth and the layers allow. Returns how many slots opened.</summary>
    public int UpdateUnlocks()
    {
        int opened = 0;
        int celestial = (int)Transcendence.PerkTotal(Perk.SlotUnlock);
        int celestialSeen = 0;
        foreach (var d in C.Slots)
        {
            bool open = d.Prestige ? celestialSeen++ < celestial : EverDepth >= d.UnlockDepth;
            if (open && !Open[d.Index])
            {
                Open[d.Index] = true;
                OpenCount++;
                opened++;
            }
        }
        foreach (var r in C.Rarities)
            if (EverDepth >= r.UnlockDepth && Ascension.Resets >= r.Ascensions && Transcendence.Resets >= r.Transcendences) MaxRarity = Math.Max(MaxRarity, r.Tier);
        return opened;
    }

    // ---------------------------------------------------------------- what a slot gives

    /// <summary>LE added to every flat stat of a slot by its forge.</summary>
    public double SlotBonus(int s) => Knock == Sys.Forge ? 0 : Forge[s] * _forgeStepLe;

    /// <summary>"More" a level of rank or mastery in slot <paramref name="s"/> gives: damage and health by the slot's offense
    /// share, twice the LE in all (so the mean of the two, the power, rises by the LE).</summary>
    public (double Damage, double Hp) PoolSplit(int s) => (2 * C.Slots[s].Offense, 2 * (1 - C.Slots[s].Offense));

    /// <summary>Percent affixes grow slowly with item level: 1 + growth * ln(1 + ilvl / scale).</summary>
    public double TierMult(double ilvl) => 1 + P.AffixTierGrowth * Math.Log(1 + ilvl / P.AffixTierScale);

    /// <summary>Adds what <paramref name="it"/> gives in slot <paramref name="s"/> to <paramref name="into"/>.</summary>
    public void Contribute(int s, Item it, Sheet into, bool slotBonus = true)
    {
        double ipl = it.Ilvl - 1 + (Knock == Sys.Rarity ? 0 : P.RarityLE * it.Rarity + P.QualityLE * it.Quality);
        double b = ipl + _slotWeightLe[s] + (slotBonus ? SlotBonus(s) : 0);
        if (_slotDamageLe[s] > Scale.None) into.Add(S, Stat.FlatDamage, b + _slotDamageLe[s]);
        if (_slotHpLe[s] > Scale.None)
        {
            into.Add(S, Stat.FlatHp, b + _slotHpLe[s]);
            into.Add(S, Stat.Armor, b + _slotArmorLe[s]);
        }
        // Percent affixes scale with the piece's size, like its flat stats (a nose stud's are a fifth of a breastplate's).
        double tier = TierMult(it.Ilvl) * C.Slots[s].Size;
        for (int i = 0; i < (Knock == Sys.Affixes ? 0 : it.Affixes); i++)
        {
            var stat = it.Stats[i];
            double m = 0.7 + 0.6 * it.Rolls[i];
            if (Stats.IsFlat(stat))
            {
                double per = stat switch
                {
                    Stat.FlatDamage => _damageBudget,
                    Stat.FlatHp => _hpBudget,
                    Stat.Armor => _armorBudget,
                    _ => _ratingBudget,
                };
                into.Add(S, stat, b + _affixShare + per + S.Le(m));
            }
            else into.Add(S, stat, C.PercentBase(stat) * tier * m);
        }
    }

    // ---------------------------------------------------------------- totals

    /// <summary>Recomputes the gear sheet (after any change to gear, what's worn, forge, ranks or mastery).</summary>
    public void RebuildGear()
    {
        UpdateActive();
        GearSheet.Clear();
        for (int s = 0; s < Gear.Length; s++)
            if (Active[s]) Contribute(s, Gear[s], GearSheet);
    }

    /// <summary>
    /// Works out what counts from what's worn, from the body down: a piece on the body counts when it's worn, open and
    /// has an item; a board's host is the counting piece that brings it; a piece on a board counts when its host opens
    /// every cell it fills.
    /// </summary>
    public void UpdateActive()
    {
        Array.Fill(HostOf, -1);
        int deepest = 2;
        foreach (var d in C.Slots) deepest = Math.Max(deepest, d.Level);
        for (int level = 2; level <= deepest; level++)
            foreach (var d in C.Slots)
            {
                if (d.Level != level) continue;
                int s = d.Index;
                bool on = Worn[s] && Open[s] && Gear[s] != null;
                if (on && d.Board != Anatomy.Body)
                {
                    int host = HostOf[d.Board];
                    on = host >= 0 && C.Boards[d.Board].Opened(host, d);
                }
                Active[s] = on;
                if (on)
                    foreach (var (b, _) in d.Brings) HostOf[b] = s;
            }
    }

    /// <summary>Whether two slots can't be worn together (same board, one fills a cell the other fills or keeps free).</summary>
    public bool Conflict(int a, int b) => Array.BinarySearch(C.Slots[a].Conflicts, b) >= 0;

    /// <summary>Recomputes what ranks and mastery give the hero (after either changes).</summary>
    public void RebuildSlots()
    {
        double damage = 0, hp = 0;
        for (int s = 0; s < Gear.Length; s++)
        {
            if (!Open[s]) continue;
            double le = ((Knock == Sys.Ranks ? 0 : Rank[s] * P.RankLE) + (Knock == Sys.Mastery ? 0 : Mastery[s] * P.MasteryLE)) * PoolScale;
            if (le == 0) continue;
            var (d, h) = PoolSplit(s);
            damage += d * le;
            hp += h * le;
        }
        SlotSheet.Clear();
        SlotSheet[Stat.MoreDamage] = damage;
        SlotSheet[Stat.MoreHp] = hp;
    }

    public void RebuildCodex()
    {
        CodexSheet.Clear();
        double le = Knock == Sys.Codex ? 0 : CodexValue * P.CodexLE * PoolScale * (1 + Transcendence.PerkTotal(Perk.CodexPower));
        CodexSheet[Stat.MoreDamage] = le;
        CodexSheet[Stat.MoreHp] = le;
    }

    /// <summary>Recomputes the total sheet and the depth-independent combat numbers.</summary>
    public void Rebuild(bool gear = true)
    {
        if (gear) RebuildGear();
        RebuildSlots();
        RebuildCodex();
        Total.CopyFrom(GearSheet);
        Total.AddAll(S, SlotSheet);
        // The talent sheet holds the tree and the paragon ranks; a knockout takes away one or the other.
        if (Knock != Sys.Talents) Total.AddAll(S, Talents.Sheet);
        double sign = Knock == Sys.Talents ? 1 : Knock == Sys.Paragon ? -1 : 0;
        Total[Stat.MoreDamage] += sign * Talents.ParagonDamage * P.ParagonLE;
        Total[Stat.MoreHp] += sign * Talents.ParagonHp * P.ParagonLE;
        if (Knock != Sys.Ascension) Total.AddAll(S, Ascension.Sheet);
        if (Knock != Sys.Transcendence) Total.AddAll(S, Transcendence.Sheet);
        Total.AddAll(S, CodexSheet);
        Base = Fight.Prepare(Total);
    }

    /// <summary>How deep the hero can push now (continuous).</summary>
    public double Reach() => Fight.Reach(Total, Base);

    // ---------------------------------------------------------------- items

    readonly double[] _rarityOdds = new double[Content.RarityNames.Length];

    /// <summary>The chance of each rarity tier for one drop, from the falloff and item find.</summary>
    public double[] RarityOdds()
    {
        double find = Total[Stat.ItemFind];
        double tilt = Math.Log(1 + Math.Max(0, find)) * P.ItemFindTilt;
        double total = 0;
        for (int r = 0; r < _rarityOdds.Length; r++)
        {
            _rarityOdds[r] = r > MaxRarity ? 0 : Math.Exp(-r * Math.Log(P.RarityFalloff) + r * tilt);
            total += _rarityOdds[r];
        }
        for (int r = 0; r < _rarityOdds.Length; r++) _rarityOdds[r] /= total;
        return _rarityOdds;
    }

    /// <summary>Rolls an item for slot <paramref name="s"/> with the given level, rarity and quality.</summary>
    public void Roll(Rng rng, int s, double ilvl, int rarity, double quality, Item into)
    {
        var def = C.Slots[s];
        into.Ilvl = ilvl;
        into.Rarity = rarity;
        into.Quality = quality;
        into.Affixes = Math.Min(Item.MaxAffixes, C.Rarities[rarity].Affixes);
        for (int i = 0; i < into.Affixes; i++)
        {
            double pick = rng.NextDouble() * def.PoolTotal;
            int k = 0;
            while (k < def.Pool.Length - 1 && pick >= def.PoolWeights[k]) pick -= def.PoolWeights[k++];
            into.Stats[i] = def.Pool[k];
            into.Rolls[i] = rng.NextDouble();
        }
    }

    public static int Band(double quality) => quality < 0.8 ? 0 : quality < 0.95 ? 1 : quality < 0.99 ? 2 : 3;

    static readonly double[] BandWidth = { 0.8, 0.15, 0.04, 0.01 };
    static readonly double[] BandLow = { 0, 0.8, 0.95, 0.99 };

    /// <summary>Marks codex entries found among <paramref name="count"/> drops of one slot and rarity. Returns new entries.</summary>
    public int Discover(Rng rng, int s, int rarity, double count, double ilvl)
    {
        int found = 0;
        int baseKey = CodexKey(Era(ilvl), s, rarity, 0);
        for (int b = 0; b < QualityBands; b++)
        {
            if (Codex[baseKey + b]) continue;
            double p = 1 - Math.Pow(1 - BandWidth[b], count);
            if (!rng.Chance(p)) continue;
            Mark(baseKey + b);
            found++;
        }
        return found;
    }

    /// <summary>The same draws as the other overload, but the entries found are listed in <paramref name="found"/> for
    /// whoever opens the drops to <see cref="Mark"/>, not marked now.</summary>
    public void Discover(Rng rng, int s, int rarity, double count, double ilvl, List<int> found)
    {
        int baseKey = CodexKey(Era(ilvl), s, rarity, 0);
        for (int b = 0; b < QualityBands; b++)
        {
            if (Codex[baseKey + b]) continue;
            double p = 1 - Math.Pow(1 - BandWidth[b], count);
            if (rng.Chance(p)) found.Add(baseKey + b);
        }
    }

    /// <summary>The codex entry for an era, slot, rarity and quality band.</summary>
    public int CodexKey(int era, int slot, int rarity, int band) => ((era * C.SlotCount + slot) * Content.RarityNames.Length + rarity) * QualityBands + band;

    /// <summary>The era, slot, rarity and quality band of a codex entry.</summary>
    public (int Era, int Slot, int Rarity, int Band) CodexEntry(int key)
    {
        int band = key % QualityBands;
        key /= QualityBands;
        int rarity = key % Content.RarityNames.Length;
        key /= Content.RarityNames.Length;
        return (key / C.SlotCount, key % C.SlotCount, rarity, band);
    }

    /// <summary>Marks a codex entry found; false if it already was.</summary>
    public bool Mark(int key)
    {
        if (Codex[key]) return false;
        Codex[key] = true;
        CodexCount++;
        CodexValue += Math.Pow(P.CodexRarityGrowth, CodexEntry(key).Rarity);
        return true;
    }

    public static double BandStart(int band) => BandLow[band];

    // ---------------------------------------------------------------- costs

    public Big ForgeCost(int s) => Big.Of(P.ForgeBase) * Big.Pow(P.ForgeGrowth, Forge[s]) * (1 - Math.Min(0.5, Ascension.PerkTotal(Perk.ForgeDiscount)));

    public double RankCost(int s) => P.RankCost * Math.Pow(Rank[s] + 1, P.RankExp);

    public Big XpToNext() => Big.Of(P.LevelXp) * Big.Pow(P.LevelGrowth, Level - 1);

    /// <summary>Gold, XP and materials per kill at depth d (boss kills are worth BossReward kills).</summary>
    public Big GoldPerKill(double d) => Big.Pow10(Math.Log10(P.GoldBase) + P.GoldGrowth * (d - 1) * Math.Log10(P.R)) * (1 + Total[Stat.GoldFind]);
    public Big XpPerKill(double d) => Big.Pow10(Math.Log10(P.XpBase) + P.XpGrowth * (d - 1) * Math.Log10(P.R)) * (1 + Total[Stat.XpGain]);

    public double MaterialPerItem(int rarity) => P.MaterialBase * Math.Pow(P.MaterialRarity, rarity) * (1 + Total[Stat.MaterialFind]) * (1 + Ascension.PerkTotal(Perk.MaterialGain));

    public double MasteryNeed(int s) => P.MasteryXp * Math.Pow(Mastery[s] + 1, P.MasteryCurve);

    public int FocusSlots => P.MasteryFocusSlots + (int)Ascension.PerkTotal(Perk.ExtraFocus);
    public double MasteryGain => 1 + Ascension.PerkTotal(Perk.MasteryGain) + Transcendence.PerkTotal(Perk.MasteryGain);
}
