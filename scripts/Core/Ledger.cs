using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// The shop's notebook: every item you've learned, with its sound, weight and price. It only ever holds what you
/// found out yourself (or inherited with the shop); it never says what a particular chest is worth.
/// </summary>
public sealed class Ledger
{
    /// <summary>Uncle Bram's entries: the everyday stuff any pawnbroker knows by ear.</summary>
    public static readonly string[] Inherited =
    {
        "copper_purse", "silver_crowns", "perfume_vial", "hand_mirror", "old_brandy", "rusty_dagger", "soldiers_helm",
        "splintered_buckler", "rusted_lantern", "love_letters", "moth_eaten_cloak",
    };

    readonly HashSet<string> _known;

    public Ledger(IEnumerable<string> known = null)
    {
        _known = new HashSet<string>(known ?? Inherited);
    }

    public int Count => _known.Count;

    public bool Knows(ItemDef item) => _known.Contains(item.Key);

    /// <summary>Writes an item down. Returns true if it's a new entry.</summary>
    public bool Learn(ItemDef item) => _known.Add(item.Key);

    public IEnumerable<ItemDef> Entries => ItemCatalog.All.Where(Knows);

    /// <summary>Bram's note in the margin: how to tell sound-alikes apart. Null for most items.</summary>
    public static string Note(ItemDef item) => item.Key switch
    {
        "copper_purse" => "A purse of GOLD makes the same chink. Gold is heavy: weigh it.",
        "gold_purse" => "Chinks like copper. Weighs 2.5 lb, not 1.",
        "perfume_vial" => "All small vials clink alike. If it glows in the dark, it isn't perfume.",
        "phoenix_tear" => "Clinks like perfume, but glows. Look through the keyhole.",
        "love_letters" => "Paper is paper to the ear. Look before you pay for a map.",
        "treasure_map" => "Rustles like old letters. You have to see it.",
        "moth_eaten_cloak" => "Cloth all flumps the same. Gold thread shines in lantern light.",
        "golden_mantle" => "Flumps like a moth-eaten cloak. Gold thread shows under the lantern.",
        _ => null,
    };
}
