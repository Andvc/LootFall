using System.Linq;

namespace LootFall.Core;

/// <summary>
/// Where a chest came from: a millpond, the Barrow, a goblin warren, the Maw's hoard. Three hundred years after the
/// Locking, chests are everywhere, and where one was found says what it tends to hold (see docs/world.md). Sellers say
/// where theirs came from; some tell the truth.
/// </summary>
/// <param name="Name">"the Barrow", as a seller would say it.</param>
/// <param name="Holds">What it tends to hold, for the ledger's Places page.</param>
/// <param name="Kinds">How likely each kind of thing is in a chest from here, by <see cref="Category"/>.</param>
public sealed record OriginDef(string Key, string Name, string Holds, float[] Kinds);

public static class Origins
{
    //                                                                                                coin weapon cloak potion spell gem
    public static readonly OriginDef[] All =
    {
        new("millpond", "the millpond", "whatever fell in; often nothing at all", new[] { 3f, 2, 2, 2, 1, 0.5f }),
        new("attic", "a farmhouse attic", "savings and Sunday clothes", new[] { 3f, 1, 4, 1, 2, 0.5f }),
        new("kitbag", "a soldier's kitbag", "weapons and a cloak", new[] { 1f, 4, 4, 1, 1, 0.3f }),
        new("field", "the old battlefield", "weapons, and the coin the dead carried", new[] { 3f, 5, 1, 1, 0.5f, 0.3f }),
        new("tab", "an unpaid tab", "a bit of everything", new[] { 3f, 3, 2, 3, 1, 0.5f }),
        new("barrow", "the Barrow", "grave goods: coin, weapons and gems", new[] { 3f, 3, 2, 1, 1, 2 }),
        new("warren", "a goblin warren", "stolen shinies: coin, mostly", new[] { 6f, 2, 1, 1, 0.5f, 2 }),
        new("camp", "an outlaws' camp", "weapons, purses and potions", new[] { 3f, 4, 1, 3, 1, 0.5f }),
        new("den", "a wolf's den", "what the wolves' dinner wore", new[] { 1f, 2, 5, 0.5f, 0.5f, 0.5f }),
        new("wood", "the Old Wood", "brews, elven cloaks and spells on birch bark", new[] { 0.5f, 0.5f, 4, 4, 3, 0.5f }),
        new("crypt", "the abbey crypt", "holy cloaks, prayer-spells and holy water", new[] { 1f, 1, 5, 2, 3, 1 }),
        new("road", "the smugglers' road", "brandy and coin, and false bottoms", new[] { 3f, 1, 1, 4, 1, 2 }),
        new("arcanum", "the Arcanum stores", "mantles, potions and spellbooks, often enchanted", new[] { 0.5f, 1, 3, 3, 4, 2 }),
        new("hold", "a dwarf-hold", "weapons and gems", new[] { 2f, 5, 0.5f, 0.5f, 1, 4 }),
        new("strongroom", "a noble's strongroom", "coin, gems and fine clothes", new[] { 4f, 1, 3, 1, 1, 5 }),
        new("pawn", "a forgotten pawn lot", "anything, in twos and threes", new[] { 4f, 3, 2, 2, 1, 3 }),
        new("river", "the Aldwater mud", "drowned wizards' potions and spellbooks (and sand)", new[] { 2f, 1, 1, 4, 5, 2 }),
        new("warchest", "a knight's war chest", "weapons and a cloak", new[] { 2f, 5, 3, 1, 1, 1 }),
        new("gate", "the Maw's hoard", "gold and gems (some of it demon gold)", new[] { 5f, 2, 1, 1, 1, 5 }),
        new("treasury", "the Royal Treasury", "the best of everything", new[] { 3f, 3, 2, 2, 2, 3 }),
    };

    public static OriginDef Get(string key) => All.First(o => o.Key == key);
}
