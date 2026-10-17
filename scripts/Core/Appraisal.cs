using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>How a kind of thing is marked on the appraisal slip.</summary>
public enum Mark { None, Plain, Magic }

/// <summary>How one thing in an opened chest compares with what the slip said.</summary>
public enum ReadKind { Right, RightMagic, SpottedFake, Missed, MissedMagic, NotMagic, Fooled }

public sealed record ItemRead(Item Item, ReadKind Kind)
{
    public bool Right => Kind is ReadKind.Right or ReadKind.RightMagic or ReadKind.SpottedFake;

    public string Note => Kind switch
    {
        ReadKind.RightMagic => "magic!",
        ReadKind.SpottedFake => "fake, as you thought",
        ReadKind.Missed => "you missed it",
        ReadKind.MissedMagic => "magic: missed the glow",
        ReadKind.NotMagic => "not magic",
        ReadKind.Fooled => "FAKE",
        _ => "",
    };
}

/// <summary>The slip checked against what was really inside.</summary>
/// <param name="Ghosts">Kinds marked on the slip that weren't in the chest.</param>
public sealed record ReadReport(IReadOnlyList<ItemRead> Items, IReadOnlyList<Category> Ghosts)
{
    public bool Perfect => Items.All(r => r.Right) && Ghosts.Count == 0;
    public int Right => Items.Count(r => r.Right);
}

/// <summary>
/// The player's appraisal slip: which kinds of things they think are in the chest, and which are magic. It starts
/// with what the seller says they heard; the player corrects it from what they hear themselves. It only adds up what
/// the player marked: the value of a kind of thing is common knowledge, the contents of the chest are not.
/// </summary>
public sealed class Appraisal
{
    readonly Mark[] _marks = new Mark[Categories.All.Length];

    /// <summary>The grade of what's inside: the seller's tier.</summary>
    public int Grade { get; }

    public Appraisal(int grade, IEnumerable<Category> marked = null)
    {
        Grade = grade;
        foreach (var category in marked ?? Enumerable.Empty<Category>()) _marks[(int)category] = Mark.Plain;
    }

    public Appraisal(Visit visit) : this(visit.Grade, visit.Claim) { }

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
        mark == Mark.None ? 0 : Categories.Value(category, Grade, mark == Mark.Magic);

    /// <summary>What the chest is worth if the slip is right.</summary>
    public int Worth => Marked.Sum(c => ValueOf(c, this[c]));

    public ReadReport Check(Chest chest)
    {
        var reads = chest.Items.Select(item => new ItemRead(item, Read(item, this[item.Category]))).ToList();
        var ghosts = Marked.Where(c => !chest.Has(c)).ToList();
        return new ReadReport(reads, ghosts);
    }

    static ReadKind Read(Item item, Mark mark)
    {
        if (item.Fake) return mark == Mark.None ? ReadKind.SpottedFake : ReadKind.Fooled;
        if (mark == Mark.None) return ReadKind.Missed;
        if (item.Magic) return mark == Mark.Magic ? ReadKind.RightMagic : ReadKind.MissedMagic;
        return mark == Mark.Magic ? ReadKind.NotMagic : ReadKind.Right;
    }
}
