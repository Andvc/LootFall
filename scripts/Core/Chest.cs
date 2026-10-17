using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// A chest on the counter: a few things, at most one of each kind. What you hear depends only on how hard it's shaken,
/// by the same three rules for everyone, sellers included:
/// a thing sounds once the shake is strong enough to stir it; while metal clanks, delicate things can't be heard;
/// a magnet holds iron still, so it makes no sound at all.
/// </summary>
public sealed class Chest
{
    public IReadOnlyList<Item> Items { get; }

    /// <summary>Chest colour (a sprite set under res://assets/sprites/chest/).</summary>
    public string Skin { get; }

    public int Value => Items.Sum(i => i.Value);

    public Chest(IEnumerable<Item> items, string skin = "wood")
    {
        Items = items.OrderBy(i => i.Category).ToList();
        Skin = skin;
    }

    public Item Get(Category category) => Items.FirstOrDefault(i => i.Category == category);

    public bool Has(Category category) => Get(category) != null;

    /// <summary>Whether this thing moves at this shake.</summary>
    public static bool Stirs(Item item, Shake level, bool magnet = false) => level >= item.Def.StirsAt && !(magnet && item.Iron);

    /// <summary>Something loud is clanking at this shake: it drowns out anything delicate.</summary>
    public bool Clanks(Shake level, bool magnet = false) => Items.Any(i => i.Def.Loud && Stirs(i, level, magnet));

    public bool Audible(Item item, Shake level, bool magnet = false) =>
        Stirs(item, level, magnet) && !(item.Def.Delicate && Clanks(level, magnet));

    public IEnumerable<Item> HeardAt(Shake level, bool magnet = false) => Items.Where(i => Audible(i, level, magnet));
}
