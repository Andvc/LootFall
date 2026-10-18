using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// What the player has heard from one chest: which things sounded at which shake, with the magnet on or off, which
/// glowed under the witchlight, and what a drop to the floor sounded like. Each thing sounds from its own spot above
/// the chest, so two purses are two jingles: the hearing keeps them apart. The counter screen fills it from real
/// shaking; the balance simulation fills it from the rules in <see cref="Chest"/>. <see cref="Read"/> is how a careful
/// player turns it into an appraisal slip.
/// </summary>
public sealed class Hearing
{
    readonly HashSet<(Shake Level, bool Magnet)> _tried = new();
    readonly HashSet<(Shake Level, bool Magnet, int Item)> _heard = new();
    readonly Dictionary<int, Category> _kinds = new();
    readonly HashSet<int> _glowed = new();
    readonly HashSet<int> _broke = new();

    /// <summary>The chest has been dropped, and whether it boomed hollow.</summary>
    public bool Dropped { get; private set; }
    public bool Hollow { get; private set; }

    public void HeardDrop(bool hollow, IEnumerable<Item> broke)
    {
        Dropped = true;
        Hollow |= hollow;
        foreach (var item in broke)
        {
            _broke.Add(item.Id);
            _kinds[item.Id] = item.Category;
        }
    }

    /// <summary>You heard glass of this kind break when the chest hit the floor.</summary>
    public bool Broke(Category category) => _broke.Any(id => _kinds[id] == category);

    public bool HeardAny(Category category) => _heard.Any(h => _kinds[h.Item] == category);

    public void Tried(Shake level, bool magnet)
    {
        if (level > Shake.Still) _tried.Add((level, magnet));
    }

    public void Heard(Item item, Shake level, bool magnet, bool glowed)
    {
        Tried(level, magnet);
        _heard.Add((level, magnet, item.Id));
        _kinds[item.Id] = item.Category;
        if (glowed) _glowed.Add(item.Id);
    }

    public bool HasTried(Shake level, bool magnet) => _tried.Contains((level, magnet));

    public bool HeardAt(Category category, Shake level, bool magnet) => _heard.Any(h => h.Level == level && h.Magnet == magnet && _kinds[h.Item] == category);

    public bool Glowed(Category category) => _glowed.Any(id => _kinds[id] == category);

    /// <summary>The gentlest shake (without the magnet) it sounded at, or Still if it never did.</summary>
    Shake FirstHeard(int item) => _heard.Where(h => h.Item == item && !h.Magnet).Select(h => h.Level).DefaultIfEmpty(Shake.Still).Min();

    /// <summary>
    /// Went quiet under the magnet at a shake where it had sounded without: it's iron. Real coins and jewelry only get
    /// louder once the metal is held still.
    /// </summary>
    bool Silenced(int item) => !Categories.Get(_kinds[item]).Iron &&
        _heard.Any(h => h.Item == item && !h.Magnet && HasTried(h.Level, true) && !_heard.Contains((h.Level, true, item)));

    /// <summary>Sounded at a gentler shake than its kind ever does: a rustle at a gentle shake is sand pouring, not paper.</summary>
    bool TooSoon(int item)
    {
        var first = FirstHeard(item);
        return first != Shake.Still && first < Categories.Get(_kinds[item]).StirsAt;
    }

    /// <summary>
    /// A careful read: every separate thing that made a sound is in there, counted kind by kind; anything that glowed is
    /// enchanted; a jingle or tinkle that goes quiet once the magnet is on is gilded iron, and a rustle at a gentle shake is
    /// sand, so neither counts; glass you heard break is worth nothing now; a hollow boom is a false bottom.
    /// </summary>
    public Appraisal Read(Rarity chest, int tier)
    {
        var slip = new Appraisal(chest, tier) { Hidden = Hollow };
        var real = _heard.Select(h => h.Item).Distinct().Where(id => !Silenced(id) && !TooSoon(id) && !_broke.Contains(id)).ToList();
        foreach (var kind in real.GroupBy(id => _kinds[id]))
            slip.Set(kind.Key, kind.Count(), kind.Any(_glowed.Contains));
        return slip;
    }

    /// <summary>What a player perceives by shaking at these levels, by the rules.</summary>
    /// <param name="lamp">The witchlight is lit: anything enchanted glows when it sounds.</param>
    /// <param name="magnetLevels">Levels also tried with the magnet on.</param>
    public static Hearing Simulate(Chest chest, IEnumerable<Shake> levels, bool lamp = false, IEnumerable<Shake> magnetLevels = null)
    {
        var hearing = new Hearing();
        hearing.Listen(chest, levels, lamp);
        hearing.Listen(chest, magnetLevels ?? Enumerable.Empty<Shake>(), lamp, magnet: true);
        return hearing;
    }

    /// <summary>Adds more shaking, by the rules.</summary>
    public void Listen(Chest chest, IEnumerable<Shake> levels, bool lamp, bool magnet = false)
    {
        foreach (var level in levels)
        {
            Tried(level, magnet);
            foreach (var item in chest.HeardAt(level, magnet)) Heard(item, level, magnet, lamp && item.Magic);
        }
    }
}
