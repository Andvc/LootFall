using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// What the player has heard from one chest: which sounds came at which shake, with the magnet on or off, which
/// glowed under the witchlight, and what a drop to the floor sounded like. The counter screen fills it from real
/// shaking; the balance simulation fills it from the rules in <see cref="Chest"/>. <see cref="Read"/> is how a careful
/// player turns it into an appraisal slip.
/// </summary>
public sealed class Hearing
{
    readonly HashSet<(Shake Level, bool Magnet)> _tried = new();
    readonly HashSet<(Shake Level, bool Magnet, Category Category)> _heard = new();
    readonly HashSet<Category> _glowed = new();
    readonly HashSet<Category> _broke = new();

    /// <summary>The chest has been dropped, and whether it boomed hollow.</summary>
    public bool Dropped { get; private set; }
    public bool Hollow { get; private set; }

    public void HeardDrop(bool hollow, IEnumerable<Category> broke)
    {
        Dropped = true;
        Hollow |= hollow;
        _broke.UnionWith(broke);
    }

    /// <summary>You heard glass break in there when it hit the floor.</summary>
    public bool Broke(Category category) => _broke.Contains(category);

    public bool HeardAny(Category category) => _heard.Any(h => h.Category == category);

    public void Tried(Shake level, bool magnet)
    {
        if (level > Shake.Still) _tried.Add((level, magnet));
    }

    public void Heard(Category category, Shake level, bool magnet, bool glowed)
    {
        Tried(level, magnet);
        _heard.Add((level, magnet, category));
        if (glowed) _glowed.Add(category);
    }

    public bool HasTried(Shake level, bool magnet) => _tried.Contains((level, magnet));

    public bool HeardAt(Category category, Shake level, bool magnet) => _heard.Contains((level, magnet, category));

    public IEnumerable<Category> At(Shake level, bool magnet) =>
        _heard.Where(h => h.Level == level && h.Magnet == magnet).Select(h => h.Category).Distinct().OrderBy(c => c);

    public bool Glowed(Category category) => _glowed.Contains(category);

    /// <summary>
    /// A careful read: whatever made a sound is in there; anything that glowed is magic; a jingle or tinkle that goes
    /// quiet once the magnet is on is gilded iron, so it doesn't count; glass you heard break is worth nothing now; a
    /// hollow boom is a false bottom.
    /// </summary>
    public Appraisal Read(Rarity chest, int tier)
    {
        var slip = new Appraisal(chest, tier) { Hidden = Hollow };
        foreach (var category in _heard.Select(h => h.Category).Distinct())
        {
            var def = Categories.Get(category);
            bool silencedByMagnet = !def.Iron && _tried.Any(t => t.Magnet && t.Level >= def.StirsAt) && !_heard.Any(h => h.Magnet && h.Category == category);
            if (!silencedByMagnet && !Broke(category)) slip[category] = Glowed(category) ? Mark.Magic : Mark.Plain;
        }
        return slip;
    }

    /// <summary>What a player perceives by shaking at these levels, by the rules.</summary>
    /// <param name="lamp">The witchlight is on: magic glows when it sounds.</param>
    /// <param name="magnetLevels">Levels also tried with the magnet on.</param>
    public static Hearing Simulate(Chest chest, IEnumerable<Shake> levels, bool lamp = false, IEnumerable<Shake> magnetLevels = null)
    {
        var hearing = new Hearing();
        void Shake(Shake level, bool magnet)
        {
            hearing.Tried(level, magnet);
            foreach (var item in chest.HeardAt(level, magnet)) hearing.Heard(item.Category, level, magnet, lamp && item.Magic);
        }
        foreach (var level in levels) Shake(level, false);
        foreach (var level in magnetLevels ?? Enumerable.Empty<Shake>()) Shake(level, true);
        return hearing;
    }
}
