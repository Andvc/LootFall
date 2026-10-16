using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum PickGrade { Masterful, Clean, Forced }

public sealed record RevealedItem(ItemDef Def, int Value, bool Shattered, bool Pristine, bool FromFalseBottom);

public static class Loot
{
    public const float PristineBonus = 1.25f;
    public const float ForcedScratch = 0.9f;
    public const float ShatteredRemains = 0.1f;

    /// <summary>Applies the lockpicking result to the chest's contents. Ordered so the best item is revealed last.</summary>
    public static List<RevealedItem> Resolve(Chest chest, PickGrade grade)
    {
        var items = chest.Items
            .Select(i => Apply(i, grade, fromFalseBottom: false))
            .OrderBy(r => r.Def.Value)
            .ToList();

        // Only a careful pick notices the hidden compartment.
        if (chest.FalseBottom != null && grade != PickGrade.Forced)
            items.Add(Apply(chest.FalseBottom, grade, fromFalseBottom: true));

        return items;
    }

    static RevealedItem Apply(ChestItem item, PickGrade grade, bool fromFalseBottom)
    {
        bool shattered = grade == PickGrade.Forced && item.Def.Fragile;
        float multiplier = grade switch
        {
            PickGrade.Masterful => PristineBonus,
            PickGrade.Forced => shattered ? ShatteredRemains : ForcedScratch,
            _ => 1f,
        };
        int value = Math.Max(1, (int)MathF.Round(item.Value * multiplier));
        return new RevealedItem(item.Def, value, shattered, grade == PickGrade.Masterful, fromFalseBottom);
    }
}
