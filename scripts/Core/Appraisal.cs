using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>How a kind of thing is marked on the appraisal slip.</summary>
public enum Mark { None, Plain, Magic }

/// <summary>How one thing in an opened chest compares with what the slip said.</summary>
public enum ReadKind { Right, RightMagic, SpottedFake, SpottedBroken, FoundHidden, Missed, MissedMagic, NotMagic, Fooled, Broken, MissedHidden }

public sealed record ItemRead(Item Item, ReadKind Kind, bool Hidden = false)
{
    public bool Right => Kind is ReadKind.Right or ReadKind.RightMagic or ReadKind.SpottedFake or ReadKind.SpottedBroken or ReadKind.FoundHidden;

    public string Note => Kind switch
    {
        ReadKind.RightMagic => "magic!",
        ReadKind.SpottedFake => "fake, as you thought",
        ReadKind.SpottedBroken => "broken, as you knew",
        ReadKind.FoundHidden => "the false bottom!",
        ReadKind.Missed => "you missed it",
        ReadKind.MissedMagic => "magic: missed the glow",
        ReadKind.NotMagic => "not magic",
        ReadKind.Fooled => "FAKE",
        ReadKind.Broken => "broken",
        ReadKind.MissedHidden => "a false bottom!",
        _ => "",
    };
}

/// <summary>The slip checked against what was really inside.</summary>
/// <param name="Ghosts">Kinds marked on the slip that weren't in the chest.</param>
/// <param name="GhostHidden">A false bottom marked on the slip that wasn't there.</param>
public sealed record ReadReport(IReadOnlyList<ItemRead> Items, IReadOnlyList<Category> Ghosts, bool GhostHidden)
{
    public bool Perfect => Items.All(r => r.Right) && Ghosts.Count == 0 && !GhostHidden;
}

/// <summary>
/// The player's appraisal slip: which kinds of things they think are in the chest, which are magic, and whether there's
/// a false bottom. It starts with what the seller says they heard; the player corrects it from what they hear
/// themselves. It only adds up what the player marked, at what a thing of that kind is worth on average in a chest of
/// that colour: the chest itself still has to be opened to see how good each thing really is.
/// </summary>
public sealed class Appraisal
{
    readonly Mark[] _marks = new Mark[Categories.All.Length];

    /// <summary>The chest's colour: what each thing is worth on average.</summary>
    public Rarity Chest { get; }

    /// <summary>The shop tier, which decides what a false bottom might hold.</summary>
    public int Tier { get; }

    /// <summary>The player thinks there's a false bottom.</summary>
    public bool Hidden { get; set; }

    public Appraisal(Rarity chest, int tier, IEnumerable<Category> marked = null)
    {
        Chest = chest;
        Tier = tier;
        foreach (var category in marked ?? Enumerable.Empty<Category>()) _marks[(int)category] = Mark.Plain;
    }

    public Appraisal(Visit visit, int tier) : this(visit.Chest.Quality, tier, visit.Claim)
    {
        Hidden = visit.ClaimsHidden;
    }

    public Mark this[Category category]
    {
        get => _marks[(int)category];
        set => _marks[(int)category] = value;
    }

    /// <summary>None, then there, then (with a witchlight) magic, then back to none.</summary>
    public void Cycle(Category category, bool magicAllowed) => this[category] = this[category] switch
    {
        Mark.None => Mark.Plain,
        Mark.Plain when magicAllowed => Mark.Magic,
        _ => Mark.None,
    };

    public IEnumerable<Category> Marked => Categories.All.Select(c => c.Category).Where(c => this[c] != Mark.None);

    public int ValueOf(Category category, Mark mark) =>
        mark == Mark.None ? 0 : Categories.Expected(category, Chest) * (mark == Mark.Magic ? Categories.MagicMultiplier : 1);

    public int HiddenValue => Market.ExpectedHidden(Chest, Tier);

    /// <summary>What the chest is worth on average if the slip is right.</summary>
    public int Worth => Marked.Sum(c => ValueOf(c, this[c])) + (Hidden ? HiddenValue : 0);

    public ReadReport Check(Chest chest)
    {
        var reads = chest.Items.Select(item => new ItemRead(item, Read(item, this[item.Category]))).ToList();
        if (chest.Hidden != null)
            reads.Add(new ItemRead(chest.Hidden, Hidden ? ReadKind.FoundHidden : ReadKind.MissedHidden, true));
        var ghosts = Marked.Where(c => !chest.Has(c)).ToList();
        return new ReadReport(reads, ghosts, Hidden && chest.Hidden == null);
    }

    static ReadKind Read(Item item, Mark mark)
    {
        if (item.Fake) return mark == Mark.None ? ReadKind.SpottedFake : ReadKind.Fooled;
        if (item.Broken) return mark == Mark.None ? ReadKind.SpottedBroken : ReadKind.Broken;
        if (mark == Mark.None) return ReadKind.Missed;
        if (item.Magic) return mark == Mark.Magic ? ReadKind.RightMagic : ReadKind.MissedMagic;
        return mark == Mark.Magic ? ReadKind.NotMagic : ReadKind.Right;
    }
}
