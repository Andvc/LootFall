using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// A chest on the counter: a few things (from the town shop on, sometimes two or three of a kind), maybe one more
/// sealed under a false bottom. What you hear depends only on how hard it's shaken, by the same rules for everyone,
/// sellers included: a thing sounds once the shake is strong enough to stir it; while metal clanks, delicate things
/// can't be heard; a magnet holds iron still, so it makes no sound at all. Whatever is under a false bottom is wrapped
/// tight and never rattles, but the hollow booms if the chest is dropped.
/// </summary>
public sealed class Chest
{
    readonly List<Item> _items;

    public IReadOnlyList<Item> Items => _items;

    /// <summary>The chest's colour: the quality to expect inside.</summary>
    public Rarity Quality { get; }

    /// <summary>A thing sealed under a false bottom, or null. It never rattles; nobody but you will ever know.</summary>
    public Item Hidden { get; private set; }

    public LockTier Lock { get; }

    /// <summary>Packed tight in straw (the Stranger's): nothing inside can move, so nothing makes a sound.</summary>
    public bool Sealed { get; }

    /// <summary>One of the old royal chests, gilded: a colour better than its seller thinks.</summary>
    public bool Gilded { get; }

    public string Skin => Sealed ? "black" : Gilded ? "gold" : Categories.ChestSkin(Quality);

    /// <summary>Everything, including what's under the false bottom.</summary>
    public IEnumerable<Item> Everything => Hidden == null ? _items : _items.Append(Hidden);

    public int Value => Everything.Sum(i => i.Value);

    public Chest(IEnumerable<Item> items, Rarity quality = Rarity.Common, Item hidden = null, LockTier lockTier = null, bool isSealed = false,
        bool gilded = false)
    {
        Gilded = gilded;
        _items = items.OrderBy(i => i.Category).ToList();
        Quality = quality;
        Hidden = hidden;
        Lock = lockTier ?? LockTiers.All[0];
        Sealed = isSealed;
    }

    public Item Get(Category category) => _items.FirstOrDefault(i => i.Category == category);

    public bool Has(Category category) => Get(category) != null;

    /// <summary>How many of this kind are in it (not counting a false bottom).</summary>
    public int Count(Category category) => _items.Count(i => i.Category == category);

    /// <summary>The thing as it is now (it may have broken since you last looked).</summary>
    public Item Now(Item item) => Everything.FirstOrDefault(i => i.Id == item.Id) ?? item;

    /// <summary>Whether this thing moves at this shake.</summary>
    public static bool Stirs(Item item, Shake level, bool magnet = false) => level >= item.StirsAt && !(magnet && item.Iron);

    /// <summary>It moves and you'd hear it, if nothing louder drowned it out. Nothing moves in a sealed chest.</summary>
    public bool Sounds(Item item, Shake level, bool magnet = false) => !Sealed && Stirs(item, level, magnet);

    /// <summary>Something loud is clanking at this shake: it drowns out anything delicate.</summary>
    public bool Clanks(Shake level, bool magnet = false) => _items.Any(i => i.Def.Loud && Sounds(i, level, magnet));

    public bool Audible(Item item, Shake level, bool magnet = false) =>
        Sounds(item, level, magnet) && !(item.Def.Delicate && Clanks(level, magnet));

    public IEnumerable<Item> HeardAt(Shake level, bool magnet = false) => _items.Where(i => Audible(i, level, magnet));

    /// <summary>Chance that glass breaks when the chest is dropped.</summary>
    public const float DropBreakChance = 0.5f;

    /// <summary>
    /// The chest hits the floor. A false bottom booms hollow; anything made of glass may break. Returns whether it
    /// sounded hollow and what broke.
    /// </summary>
    /// <param name="sturdier">How much less likely glass is to break (a vintner's packing).</param>
    public (bool Hollow, List<Item> Broke) Drop(Random rng, float sturdier = 0f)
    {
        var broke = _items.Where(i => i.Def.Fragile && !i.Broken && rng.NextFloat() < DropBreakChance * (1 - sturdier)).ToList();
        foreach (var item in broke) Break(item);
        return (Hidden != null, broke);
    }

    /// <summary>Forced open with a crowbar: the glass breaks, and everything else gets scratched.</summary>
    public List<Item> Force(Random rng = null, float sturdier = 0f)
    {
        var broke = Everything.Where(i => i.Def.Fragile && !i.Broken && (rng == null || rng.NextFloat() >= sturdier)).ToList();
        foreach (var item in broke) Break(item);
        return broke;
    }

    /// <summary>How heavy it feels when you lift it: what a sealed chest tells you.</summary>
    public string Weight => Everything.Sum(i => Categories.Heft(i.Category)) switch
    {
        >= 6 => "heavy",
        >= 3 => "middling",
        _ => "light",
    };

    /// <summary>Changes things inside without them becoming different things (a curio's luck with their quality).</summary>
    public void Improve(Func<Item, Item> change)
    {
        for (int i = 0; i < _items.Count; i++) _items[i] = change(_items[i]) with { Id = _items[i].Id };
        if (Hidden != null) Hidden = change(Hidden) with { Id = Hidden.Id };
    }

    void Break(Item item)
    {
        if (item == Hidden) Hidden = item with { Broken = true };
        else _items[_items.IndexOf(item)] = item with { Broken = true };
    }
}

/// <summary>A lock's maker sets how hard it is to pick. Index 0 is no lock at all.</summary>
public sealed record LockTier(int Index, string Name, int Pins, float ZoneDegrees, float NeedleSpeed)
{
    public bool Locked => Pins > 0;
}

public static class LockTiers
{
    public static readonly LockTier[] All =
    {
        new(0, "No lock", 0, 0f, 0f),
        new(1, "Rusty Padlock", 2, 56f, 120f),
        new(2, "Guild Lock", 3, 44f, 145f),
        new(3, "Dwarven Tumbler", 4, 34f, 175f),
    };

    /// <summary>Better chests have better locks.</summary>
    public static LockTier For(Rarity chest) => All[Math.Clamp((int)chest, 1, 3)];
}

/// <summary>How the lock came open, which changes what everything inside is worth.</summary>
public enum PickGrade { Masterful, Clean, Forced }

public static class Picks
{
    /// <summary>A masterful pick leaves everything pristine; forcing scratches it.</summary>
    public static float Multiplier(PickGrade grade) => grade switch
    {
        PickGrade.Masterful => 1.25f,
        PickGrade.Forced => 0.9f,
        _ => 1f,
    };
}
