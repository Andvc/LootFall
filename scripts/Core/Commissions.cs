using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum CommissionKind
{
    /// <summary>One thing of a kind, at a quality or better.</summary>
    Piece,
    /// <summary>Several things of a kind, at a quality or better.</summary>
    Lot,
    /// <summary>Anything enchanted.</summary>
    Enchanted,
}

/// <summary>
/// Someone wants something, and will pay well over its price for it: a regular's commission, good until the Guild's
/// next visit. Anything that comes out of a chest you open can go to it instead of being sold. The Hooded Woman's pay
/// in map pieces, and she asks again the next week if you miss one.
/// </summary>
public sealed class Commission
{
    /// <summary>Who wants it: a seller's key, or <see cref="Story.Hood"/>.</summary>
    public string Patron { get; init; }

    public CommissionKind Kind { get; init; }
    public Category Category { get; init; }
    public Rarity Quality { get; init; }
    public int Needed { get; init; } = 1;
    public int Delivered { get; set; }

    /// <summary>What it pays, before any curio.</summary>
    public int Reward { get; init; }

    /// <summary>The Hooded Woman's: it pays a map piece as well.</summary>
    public bool Story { get; init; }

    public bool Done => Delivered >= Needed;

    public string PatronName => Story ? Core.Story.HoodName : Sellers.All.FirstOrDefault(s => s.Key == Patron)?.Name ?? Patron;

    /// <summary>Whether this thing would do. Fakes and broken things never do.</summary>
    public bool Wants(Item item) => !Done && !item.Fake && !item.Broken && (Kind == CommissionKind.Enchanted
        ? item.Magic
        : item.Category == Category && item.Quality >= Quality);

    /// <summary>What's wanted, in a few words: "a rare or better metal thing", "3 cloth, uncommon or better".</summary>
    public string What => Kind switch
    {
        CommissionKind.Enchanted => "anything enchanted",
        CommissionKind.Lot => $"{Needed} {Categories.Get(Category).Name.ToLowerInvariant()}, {Better}",
        _ => $"{Categories.Get(Category).Name.ToLowerInvariant()}, {Better}",
    };

    /// <summary>What's wanted, as someone would say it: "a piece of jewelry (epic or better)".</summary>
    public string Phrase => Kind == CommissionKind.Enchanted ? "something enchanted" : $"{Noun(Category, Needed)} ({Better})";

    static string Noun(Category kind, int n) => (kind, n > 1) switch
    {
        (Category.Coins, _) => n > 1 ? $"{Sellers.Number(n)} purses of coins" : "a purse of coins",
        (Category.Metal, false) => "a piece of metalwork",
        (Category.Cloth, false) => "a piece of cloth",
        (Category.Bottles, false) => "a bottle",
        (Category.Paper, false) => "some old papers",
        (Category.Jewels, false) => "a piece of jewelry",
        (Category.Metal, true) => $"{Sellers.Number(n)} pieces of metalwork",
        (Category.Cloth, true) => $"{Sellers.Number(n)} pieces of cloth",
        (Category.Bottles, true) => $"{Sellers.Number(n)} bottles",
        (Category.Paper, true) => $"{Sellers.Number(n)} lots of papers",
        _ => $"{Sellers.Number(n)} pieces of jewelry",
    };

    string Better => Quality == Rarity.Common ? "any quality" : Quality == Rarity.Legendary ? "legendary" : $"{Categories.QualityName(Quality)} or better";

    /// <summary>How far along it is, for a lot: "1/3".</summary>
    public string Progress => Needed > 1 ? $"{Delivered}/{Needed}" : "";
}

public static class Patrons
{
    /// <summary>Regulars start asking from this week (after the first visit from the Guild).</summary>
    public const int FromWeek = 2;

    public const int PerWeek = 3;

    /// <summary>A piece pays this many times what it's worth as good as asked; a lot, this many times each.</summary>
    public const float PieceReward = 2f, LotReward = 1.5f;

    /// <summary>The quality a regular asks for at each shop: a step better than its chests usually are, epic at most.</summary>
    public static Rarity Asked(int tier) => tier switch
    {
        1 => Rarity.Uncommon,
        2 => Rarity.Rare,
        _ => Rarity.Epic,
    };

    /// <summary>This week's commissions from the regulars you've met (any of the shop's crowd before you've met three).</summary>
    public static List<Commission> Roll(Random rng, GameState state)
    {
        var list = new List<Commission>();
        var crowd = Sellers.All.Where(s => s.Tier <= state.Tier && (state.Met.Contains(s.Key) || state.Met.Count < 3)).ToList();
        var kinds = Categories.UpTo(state.Tier).Select(c => c.Category).ToList();
        int wanted = PerWeek + Upgrades.All.Where(u => state.Has(u.Key)).Sum(u => u.Commissions);
        while (list.Count < wanted && crowd.Count > 0)
        {
            var patron = rng.PickWeighted(crowd.ToArray(), s => s.Tier == state.Tier ? 2f : 1f);
            crowd.Remove(patron);
            bool enchanted = state.Tier >= 3 && list.All(c => c.Kind != CommissionKind.Enchanted) && rng.NextFloat() < 0.25f;
            if (enchanted)
            {
                list.Add(new Commission
                {
                    Patron = patron.Key, Kind = CommissionKind.Enchanted,
                    Reward = Money.Round(PieceReward * 2 * Categories.Nominal(Category.Coins, (Rarity)(state.Tier - 1))),
                });
                continue;
            }
            // What they want is the kind of thing they'd bring, one kind per commission.
            var free = kinds.Where(k => list.All(c => c.Kind == CommissionKind.Enchanted || c.Category != k)).ToArray();
            if (free.Length == 0) break;
            var kind = rng.PickWeighted(free, k => patron.Kinds[(int)k] + 0.5f);
            list.Add(rng.NextFloat() < 0.4f ? Lot(kind, state.Tier, patron.Key) : Piece(kind, state.Tier, patron.Key));
        }
        return list;
    }

    static Commission Piece(Category kind, int tier, string patron)
    {
        var quality = Asked(tier);
        return new Commission { Patron = patron, Kind = CommissionKind.Piece, Category = kind, Quality = quality,
            Reward = Money.Round(PieceReward * Categories.Nominal(kind, quality)) };
    }

    static Commission Lot(Category kind, int tier, string patron)
    {
        var quality = (Rarity)Math.Max(0, (int)Asked(tier) - 1);
        int needed = tier >= 3 ? 3 : 2;
        return new Commission { Patron = patron, Kind = CommissionKind.Lot, Category = kind, Quality = quality, Needed = needed,
            Reward = Money.Round(LotReward * needed * Categories.Nominal(kind, quality)) };
    }

    /// <summary>
    /// The Hooded Woman's request for her next map piece: papers first (she's after the old king's records), then a
    /// ring, then something enchanted, a step better than the regulars ask for; something simpler if your shop is small.
    /// </summary>
    public static Commission Hood(int piece, int tier)
    {
        var (kind, enchanted) = piece switch
        {
            1 => (tier >= 2 ? Category.Paper : Category.Coins, false),
            2 => (tier >= 3 ? Category.Jewels : Category.Coins, false),
            _ => (Category.Metal, tier >= 3),
        };
        var quality = (Rarity)Math.Min(4, (int)Asked(tier) + 1);
        int worth = Categories.Nominal(kind, quality);
        return new Commission
        {
            Patron = Story.Hood, Story = true, Kind = enchanted ? CommissionKind.Enchanted : CommissionKind.Piece, Category = kind,
            Quality = enchanted ? Rarity.Common : quality, Reward = Money.Round(2 * worth),
        };
    }
}
