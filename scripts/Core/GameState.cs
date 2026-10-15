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
            .OrderBy(r => r.Def.BaseValue)
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

public sealed class GameState
{
    public const int StartingGold = 300;

    readonly Dictionary<Provenance, Appraiser> _appraisers = new();

    public int Seed { get; }
    public int Gold { get; set; } = StartingGold;
    public int LotNumber { get; private set; }

    public Random Rng { get; private set; }
    public Chest Chest { get; private set; }
    public Appraiser Appraiser { get; private set; }
    public Clues Clues { get; private set; }
    public Estimate Estimate { get; private set; }
    public Rival Rival { get; private set; }
    public int OpeningPrice { get; private set; }
    public int PricePaid { get; set; }
    public PickGrade Grade { get; set; }

    public bool HeardFragile => Clues.Shaken && Chest.HasFragile;

    public GameState(int seed)
    {
        Seed = seed;
    }

    public void NextLot()
    {
        LotNumber++;
        Rng = new Random(unchecked(Seed * 7919 + LotNumber));
        var provenance = Rng.PickOne(Provenances.All);
        if (!_appraisers.TryGetValue(provenance, out var appraiser))
        {
            appraiser = new Appraiser(provenance, unchecked(Seed * 31 + Array.IndexOf(Provenances.All, provenance)));
            _appraisers[provenance] = appraiser;
        }

        Appraiser = appraiser;
        Chest = ChestGenerator.Generate(Rng, provenance);
        Clues = new Clues();
        Estimate = appraiser.Prior;
        Rival = Rivals.Create(Rng, Chest, appraiser);
        OpeningPrice = Auction.OpeningPrice(appraiser.Prior);
        PricePaid = 0;
        Grade = PickGrade.Clean;
    }

    public void RefreshEstimate() => Estimate = Appraiser.Estimate(Chest, Clues);

    public void Reset()
    {
        Gold = StartingGold;
        LotNumber = 0;
    }
}
