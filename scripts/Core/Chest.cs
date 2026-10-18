using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// A chest on the counter: a few things (from the town shop on, sometimes two or three of a kind), maybe one more
/// sealed under a false bottom. What you hear depends only on how hard it's shaken, by the same rules for everyone,
/// sellers included: a thing sounds once the shake is strong enough to stir it; while metal clanks, delicate things
/// can't be heard; if someone packed it with cloth (from the village shop on), the cloth muffles the hard things (metal,
/// coins, jewelry), so they only sound at a shake one strength harder, unless you listen through an ear trumpet; a
/// magnet holds iron still, so it makes no sound at all. Whatever is under a false bottom is padded and never rattles, but the hollow booms if the chest is dropped.
/// </summary>
public sealed class Chest
{
    readonly List<Item> _items;

    public IReadOnlyList<Item> Items => _items;

    /// <summary>The chest's colour: the quality to expect inside.</summary>
    public Rarity Quality { get; }

    /// <summary>A padded thing under a false bottom, or null. It never rattles; nobody but you will ever know.</summary>
    public Item Hidden { get; private set; }

    public LockTier Lock { get; }

    readonly bool _packed;

    public string Skin => Categories.ChestSkin(Quality);

    /// <summary>Everything, including what's under the false bottom.</summary>
    public IEnumerable<Item> Everything => Hidden == null ? _items : _items.Append(Hidden);

    public int Value => Everything.Sum(i => i.Value);

    /// <param name="packed">Packed with its cloth around the rest (only means anything if there's cloth in it).</param>
    public Chest(IEnumerable<Item> items, Rarity quality = Rarity.Common, Item hidden = null, LockTier lockTier = null, bool packed = false)
    {
        _items = items.OrderBy(i => i.Category).ToList();
        Quality = quality;
        Hidden = hidden;
        Lock = lockTier ?? LockTiers.All[0];
        _packed = packed;
    }

    public Item Get(Category category) => _items.FirstOrDefault(i => i.Category == category);

    public bool Has(Category category) => Get(category) != null;

    /// <summary>How many of this kind are in it (not counting a false bottom).</summary>
    public int Count(Category category) => _items.Count(i => i.Category == category);

    /// <summary>The thing as it is now (it may have broken since you last looked).</summary>
    public Item Now(Item item) => Everything.FirstOrDefault(i => i.Id == item.Id) ?? item;

    /// <summary>Whether this thing moves at this shake.</summary>
    public static bool Stirs(Item item, Shake level, bool magnet = false) => level >= item.StirsAt && !(magnet && item.Iron);

    /// <summary>Its cloth is packed around the rest, which muffles the hard things. Loose cloth doesn't.</summary>
    public bool Padded => _packed && _items.Any(i => i.Def.Pads && !i.Fake);

    /// <summary>
    /// Cloth packed around it keeps it from rattling until the shake is one strength harder than usual. Through an ear
    /// trumpet you hear it anyway.
    /// </summary>
    public bool Muffled(Item item, Shake level, bool trumpet = false) =>
        !trumpet && Padded && item.Def.Muffled && level < item.StirsAt + 1;

    /// <summary>It moves and you'd hear it, if nothing louder drowned it out.</summary>
    public bool Sounds(Item item, Shake level, bool magnet = false, bool trumpet = false) =>
        Stirs(item, level, magnet) && !Muffled(item, level, trumpet);

    /// <summary>Something loud is clanking at this shake: it drowns out anything delicate.</summary>
    public bool Clanks(Shake level, bool magnet = false, bool trumpet = false) =>
        _items.Any(i => i.Def.Loud && Sounds(i, level, magnet, trumpet));

    public bool Audible(Item item, Shake level, bool magnet = false, bool trumpet = false) =>
        Sounds(item, level, magnet, trumpet) && !(item.Def.Delicate && Clanks(level, magnet, trumpet));

    public IEnumerable<Item> HeardAt(Shake level, bool magnet = false, bool trumpet = false) =>
        _items.Where(i => Audible(i, level, magnet, trumpet));

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
