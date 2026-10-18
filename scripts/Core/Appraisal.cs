using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>How one thing in an opened chest compares with what the slip said.</summary>
public enum ReadKind { Right, RightMagic, SpottedFake, SpottedBroken, FoundHidden, Missed, MissedMagic, NotMagic, Fooled, Broken, MissedHidden }

public sealed record ItemRead(Item Item, ReadKind Kind, bool Hidden = false)
{
    public bool Right => Kind is ReadKind.Right or ReadKind.RightMagic or ReadKind.SpottedFake or ReadKind.SpottedBroken or ReadKind.FoundHidden;

    public string Note => Kind switch
    {
        ReadKind.RightMagic => "enchanted!",
        ReadKind.SpottedFake => "fake, as you thought",
        ReadKind.SpottedBroken => "broken, as you knew",
        ReadKind.FoundHidden => "the false bottom!",
        ReadKind.Missed => "you missed it",
        ReadKind.MissedMagic => "enchanted: missed the glow",
        ReadKind.NotMagic => "not enchanted",
        ReadKind.Fooled => "FAKE",
        ReadKind.Broken => "broken",
        ReadKind.MissedHidden => "a false bottom!",
        _ => "",
    };
}

/// <summary>The slip checked against what was really inside.</summary>
/// <param name="Ghosts">One entry for each thing the slip counted that wasn't in the chest.</param>
/// <param name="GhostHidden">A false bottom marked on the slip that wasn't there.</param>
public sealed record ReadReport(IReadOnlyList<ItemRead> Items, IReadOnlyList<Category> Ghosts, bool GhostHidden)
{
    public bool Perfect => Items.All(r => r.Right) && Ghosts.Count == 0 && !GhostHidden;
}

/// <summary>
/// The player's appraisal slip: how many of each kind of thing they think are in the chest, whether one of them is
/// enchanted, and whether there's a false bottom. It starts with what the seller says is in it; the player corrects it from
/// what they hear themselves. It only adds up what the player marked, at what such a thing is worth if it's as good as
/// the chest's colour says: the chest itself still has to be opened to see how good each thing really is.
/// </summary>
public sealed class Appraisal
{
    readonly int[] _count = new int[Categories.All.Length];
    readonly bool[] _magic = new bool[Categories.All.Length];

    /// <summary>The chest's colour: what each thing is probably worth.</summary>
    public Rarity Chest { get; }

    /// <summary>The shop tier, which decides what a false bottom might hold and whether to count.</summary>
    public int Tier { get; }

    /// <summary>The player thinks there's a false bottom.</summary>
    public bool Hidden
    {
        get => _hidden;
        set
        {
            _hidden = value;
            _outcomes = null;
        }
    }

    bool _hidden;

    // The outcomes, worked out once per state of the slip (the odds bar asks again whenever the offer slider moves).
    IReadOnlyList<(int Value, float Chance)> _outcomes;
    Func<Category, float> _outcomesSale;

    /// <summary>The most of one kind the slip counts: two or three of a kind turn up from the town shop on.</summary>
    public int MaxCount => Tier >= Market.PairsTier ? 3 : 1;

    public Appraisal(Rarity chest, int tier, IEnumerable<Category> marked = null)
    {
        Chest = chest;
        Tier = tier;
        foreach (var category in marked ?? Enumerable.Empty<Category>())
            _count[(int)category] = Math.Min(MaxCount, _count[(int)category] + 1);
    }

    public Appraisal(Visit visit, int tier) : this(visit.Chest.Quality, tier, visit.Claim)
    {
        Hidden = visit.ClaimsHidden;
    }

    public int Count(Category category) => _count[(int)category];

    /// <summary>One of them glowed: it's magic.</summary>
    public bool Magic(Category category) => _magic[(int)category] && _count[(int)category] > 0;

    public void Set(Category category, int count, bool magic = false)
    {
        _count[(int)category] = Math.Clamp(count, 0, MaxCount);
        _magic[(int)category] = magic && count > 0;
        _outcomes = null;
    }

    /// <summary>One more of this kind, or back to none after the most there can be.</summary>
    public void CycleCount(Category category)
    {
        int count = Count(category) >= MaxCount ? 0 : Count(category) + 1;
        Set(category, count, Magic(category));
    }

    /// <summary>Marks one of them magic, or not. Marking a kind you hadn't ticked ticks one.</summary>
    public void ToggleMagic(Category category) => Set(category, Math.Max(1, Count(category)), !Magic(category));

    public IEnumerable<Category> Marked => Categories.All.Select(c => c.Category).Where(c => Count(c) > 0);

    /// <summary>What one of this kind is worth if it's as good as the chest's colour.</summary>
    public int OneValue(Category category, bool magic = false) =>
        Categories.Nominal(category, Chest) * (magic ? Categories.MagicMultiplier : 1);

    /// <summary>What the slip says this kind adds up to.</summary>
    public int ValueOf(Category category)
    {
        int count = Count(category);
        if (count == 0) return 0;
        return Magic(category) ? (count - 1) * OneValue(category) + OneValue(category, true) : count * OneValue(category);
    }

    public int HiddenValue => Market.HiddenValue(Chest, Tier);

    /// <summary>What the chest is worth if the slip is right and everything is as good as its colour.</summary>
    public int Worth => Marked.Sum(ValueOf) + (Hidden ? HiddenValue : 0);

    /// <summary>
    /// What it's worth on average if the slip is right, allowing for things coming out better or worse than the colour.
    /// The careful player's model knows this; the screen doesn't show it.
    /// </summary>
    public int ExpectedWorth => Marked.Sum(c => Categories.Expected(c, Chest) * (Count(c) + (Magic(c) ? Categories.MagicMultiplier - 1 : 0))) +
                                (Hidden ? Market.HiddenValue(Chest, Tier, expected: true) : 0);

    /// <summary>
    /// Everything the chest could turn out to be worth if the slip is right, with how likely each total is: each thing's
    /// quality lands around the chest's colour (<see cref="Categories.Odds"/>), and a false bottom might hold any of the
    /// things people hide. Mostly it's what the colour promises; sometimes less; now and then a lot more.
    /// </summary>
    /// <param name="sale">What one thing of a kind fetches for each gold it's worth (the shop's synergies).</param>
    public IReadOnlyList<(int Value, float Chance)> Outcomes(Func<Category, float> sale = null)
    {
        if (_outcomes != null && Equals(sale, _outcomesSale)) return _outcomes;
        _outcomesSale = sale;
        return _outcomes = Work(sale ?? (_ => 1f));
    }

    IReadOnlyList<(int Value, float Chance)> Work(Func<Category, float> sale)
    {
        var totals = new Dictionary<float, float> { [0f] = 1f };
        void Add(IEnumerable<(float Value, float Chance)> thing)
        {
            var next = new Dictionary<float, float>();
            foreach (var (total, p) in totals)
            foreach (var (value, q) in thing)
                next[total + value] = next.GetValueOrDefault(total + value) + p * q;
            totals = next;
        }
        IEnumerable<(float, float)> One(Category kind, bool magic) =>
            Categories.Odds(Chest).Select(o => (Categories.Value(kind, o.Quality, magic) * sale(kind), o.Chance));

        foreach (var kind in Marked)
            for (int i = 0; i < Count(kind); i++)
                Add(One(kind, Magic(kind) && i == 0));
        if (Hidden)
            Add(Market.HiddenOdds(Tier).SelectMany(h => One(h.Kind, false).Select(o => (o.Item1, o.Item2 * h.Chance))));
        return totals.GroupBy(t => (int)MathF.Round(t.Key)).Select(g => (g.Key, g.Sum(t => t.Value))).OrderBy(o => o.Key).ToList();
    }

    /// <summary>
    /// What the chest is more likely than not worth at least, if the slip is right: the most you can pay and still have
    /// the odds bar on your side.
    /// </summary>
    public int LikelyWorth(Func<Category, float> sale = null)
    {
        float below = 0;
        foreach (var (value, chance) in Outcomes(sale))
            if ((below += chance) >= 0.5f) return value;
        return 0;
    }

    /// <summary>The chance the chest is worth more than this price, if the slip is right.</summary>
    public float ChanceAbove(int price, Func<Category, float> sale = null) =>
        Outcomes(sale).Where(o => o.Value > price).Sum(o => o.Chance);

    /// <summary>
    /// Checks the slip against the chest. Of each kind, the slip's count goes to the real, whole things first (magic
    /// first if it marked one magic), then broken glass, then fakes; whatever's left over is a ghost.
    /// </summary>
    public ReadReport Check(Chest chest)
    {
        var reads = new List<ItemRead>();
        var ghosts = new List<Category>();
        foreach (var kind in Categories.All.Select(c => c.Category))
        {
            var items = chest.Items.Where(i => i.Category == kind)
                .OrderBy(i => i.Fake ? 2 : i.Broken ? 1 : 0).ThenBy(i => Magic(kind) && i.Magic ? 0 : 1).ToList();
            int marked = Count(kind);
            bool magicLeft = Magic(kind);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                bool counted = i < marked;
                ReadKind read;
                if (item.Fake) read = counted ? ReadKind.Fooled : ReadKind.SpottedFake;
                else if (item.Broken) read = counted ? ReadKind.Broken : ReadKind.SpottedBroken;
                else if (!counted) read = item.Magic ? ReadKind.MissedMagic : ReadKind.Missed;
                else if (item.Magic) read = magicLeft ? ReadKind.RightMagic : ReadKind.MissedMagic;
                else read = ReadKind.Right;
                if (read == ReadKind.RightMagic) magicLeft = false;
                reads.Add(new ItemRead(item, read));
            }
            // Marked magic but nothing magic was there: the first one counted wasn't.
            if (magicLeft && !items.Any(i => i.Magic && !i.Fake))
            {
                int first = reads.FindIndex(r => r.Item.Category == kind && r.Kind == ReadKind.Right);
                if (first >= 0) reads[first] = reads[first] with { Kind = ReadKind.NotMagic };
            }
            for (int extra = items.Count; extra < marked; extra++) ghosts.Add(kind);
        }
        if (chest.Hidden != null)
            reads.Add(new ItemRead(chest.Hidden, Hidden ? ReadKind.FoundHidden : ReadKind.MissedHidden, true));
        return new ReadReport(reads, ghosts, Hidden && chest.Hidden == null);
    }
}
