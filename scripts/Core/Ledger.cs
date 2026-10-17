using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// The shop's notebook: what you've learned. Families and their rules as you hear them, each item once you've opened
/// one, the small differences within a family once you've found them, and each regular's pricing habit once Bram has
/// told you. It never says what a particular chest is worth.
/// </summary>
public sealed class Ledger
{
    readonly HashSet<string> _items = new();
    readonly HashSet<(Family, string)> _variants = new();
    readonly HashSet<Family> _families = new();
    readonly HashSet<string> _sellers = new();

    public int ItemCount => _items.Count;

    public bool Knows(ItemDef item) => _items.Contains(item.Key);
    public bool Knows(Family family) => _families.Contains(family);
    public bool Knows(Family family, string variant) => _variants.Contains((family, variant));
    public bool Knows(SellerDef seller) => _sellers.Contains(seller.Key);

    /// <summary>Writes down an item you've opened. Returns true if it's new.</summary>
    public bool Learn(ItemDef item)
    {
        _families.Add(item.Family);
        _variants.Add((item.Family, item.Variant));
        return _items.Add(item.Key);
    }

    /// <summary>Notes a family you've heard. Returns true the first time.</summary>
    public bool Hear(Family family) => _families.Add(family);

    public bool Meet(SellerDef seller) => _sellers.Add(seller.Key);

    public IEnumerable<string> KnownVariants(Family family) => _variants.Where(v => v.Item1 == family).Select(v => v.Item2);
}
