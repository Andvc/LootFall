using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>The shop's size: who comes in, how many, and what they bring.</summary>
public static class ShopTiers
{
    public const int Max = 4;

    public static string Name(int tier) => tier switch
    {
        1 => "Roadside Stall",
        2 => "Village Shop",
        3 => "Town Shop",
        _ => "The Emporium",
    };
}

/// <summary>A chest bought today, with the slip the player bought it on. It's opened straight away.</summary>
public sealed class Purchase
{
    public Visit Visit { get; }
    public Appraisal Slip { get; }
    public Chest Chest => Visit.Chest;
    public int Paid => Visit.PricePaid;

    /// <summary>How the lock came open (Clean if there was none).</summary>
    public PickGrade Grade { get; set; } = PickGrade.Clean;

    public Purchase(Visit visit, Appraisal slip)
    {
        Visit = visit;
        Slip = slip;
    }
}

/// <summary>How the day went, for the end-of-day summary.</summary>
public sealed class DayStats
{
    public int Bought, Passed, Spent, Earned, PerfectReads, NewFinds;
    public int Profit => Earned - Spent;
}

public sealed class GameState
{
    public const int StartingGold = 40;

    /// <summary>Display space you start with: a shelf behind the counter.</summary>
    public const int ShelfSlots = 2;

    /// <summary>Exhibition points per extra customer a day (up to two).</summary>
    public const int ExhibitionPerCustomer = 10;

    /// <summary>The Crown wants to see a collection before it grants the warrant.</summary>
    public const int WarrantExhibition = 40;

    /// <summary>If you end a day with less than this, Bram tops you up to it.</summary>
    public const int Allowance = 20;

    public int Seed { get; }
    public int Gold { get; set; } = StartingGold;
    public int Day { get; private set; }
    public int Tier { get; private set; } = 1;
    public Random Rng { get; private set; } = new();

    /// <summary>Whether Bram's tips are shown (and the first day is the scripted one).</summary>
    public bool Guided { get; set; } = true;

    public HashSet<string> HintsSeen { get; } = new();

    /// <summary>Regulars you've met: their habits go in the ledger.</summary>
    public HashSet<string> Met { get; } = new();

    /// <summary>Every kind of thing you've found, by its collection name.</summary>
    public HashSet<string> Found { get; } = new();

    /// <summary>What you kept to show off in the shop instead of selling.</summary>
    public List<Item> Display { get; } = new();

    public int DisplaySlots => ShelfSlots + Upgrades.All.Where(u => Has(u.Key)).Sum(u => u.Slots);
    public bool DisplayFull => Display.Count >= DisplaySlots;

    /// <summary>The exhibition: what your displayed things are worth to visitors' eyes, not their purses.</summary>
    public int Exhibition => Display.Sum(i => i.Prestige);

    /// <summary>People come to see the exhibition: one more customer a day per so many points, up to two.</summary>
    public int ExhibitionCustomers => Math.Min(2, Exhibition / ExhibitionPerCustomer);

    /// <summary>And a fine exhibition draws a better class of customer: the chance a chest is one colour better.</summary>
    public float BetterChests => MathF.Min(0.4f, Exhibition * 0.01f);

    readonly HashSet<string> _owned = new();

    public List<Visit> Queue { get; } = new();
    public Visit Current { get; private set; }
    public DayStats Today { get; private set; } = new();

    /// <summary>The purchase being opened.</summary>
    public Purchase Opening { get; set; }

    public GameState(int seed)
    {
        Seed = seed;
    }

    public bool Has(string upgrade) => _owned.Contains(upgrade);

    public int CustomersPerDay => 3 + Tier + (Has("sign") ? 1 : 0) + ExhibitionCustomers;

    public bool Won => Has("warrant");

    public bool CanBuy(Upgrade upgrade) => Gold >= upgrade.Cost && Upgrades.Available(this).Contains(upgrade) &&
                                           (upgrade.Key != "warrant" || Exhibition >= WarrantExhibition);

    public void BuyUpgrade(Upgrade upgrade)
    {
        if (!CanBuy(upgrade)) return;
        Gold -= upgrade.Cost;
        _owned.Add(upgrade.Key);
        if (upgrade.SetsTier > 0) Tier = upgrade.SetsTier;
    }

    /// <summary>Hands out a hint key once. Returns false if it's been shown before or tips are off.</summary>
    public bool FirstTime(string hint) => Guided && HintsSeen.Add(hint);

    /// <summary>For debugging and screenshots: jump ahead with the tools a player would have by then.</summary>
    public void SkipTo(int tier, int gold)
    {
        foreach (var upgrade in Upgrades.All.Where(u => u.Key != "warrant" && (u.IsRenovation ? u.SetsTier <= tier : u.MinTier < tier)))
            _owned.Add(upgrade.Key);
        Tier = tier;
        Gold = gold;
    }

    public void StartDay()
    {
        Day++;
        Rng = new Random(unchecked(Seed * 7919 + Day));
        Queue.Clear();
        Current = null;
        Today = new DayStats();

        if (Day == 1 && Guided && Tier == 1)
        {
            Queue.AddRange(FirstDay(Rng));
            return;
        }

        // Each renovation brings its own crowd; the old regulars still drop by now and then. Nobody comes twice in a
        // day unless everyone else already has.
        var sellers = Sellers.All.Where(s => s.Tier <= Tier).ToArray();
        while (Queue.Count < CustomersPerDay)
        {
            var fresh = sellers.Where(s => Queue.All(v => v.Seller != s)).ToArray();
            var pool = fresh.Length > 0 ? fresh : sellers.Where(s => Queue[^1].Seller != s).ToArray();
            var seller = Rng.PickWeighted(pool, s => s.Tier == Tier ? 3f : s.Tier == Tier - 1 ? 0.6f : 0.15f);
            Queue.Add(Market.CreateVisit(Rng, seller, Tier, BetterChests));
        }
    }

    /// <summary>
    /// The first day teaches one thing per customer: a gentle shake and the slip (Tam never shook his chest), a hard
    /// shake (Penn shook hers too gently to hear the coat), a gentle shake under a clank (Hob shook hard and the
    /// knife drowned out the coins), and passing (Mags heard everything there was).
    /// </summary>
    static IEnumerable<Visit> FirstDay(Random rng)
    {
        Visit Scripted(string seller, int ask, params Category[] kinds) =>
            Market.Price(rng, Sellers.Get(seller), new Chest(kinds.Select(k => new Item(k, Rarity.Common))), 1, ask, Trade.Fixed);

        // Tam names a price today, so the first lesson is only about listening; from tomorrow he asks you to name one.
        yield return Market.Price(rng, Sellers.Get("tam"), new Chest(new[] { new Item(Category.Coins, Rarity.Common) }), 1, 4, Trade.Fixed,
            "Found it by the river. I didn't shake it, it might break! Is {price} okay?");
        yield return Scripted("penn", 3, Category.Cloth);
        yield return Scripted("hob", 7, Category.Coins, Category.Metal);
        yield return Scripted("mags", 7, Category.Metal);
    }

    /// <summary>Calls in the next customer, if anyone is waiting.</summary>
    public bool NextCustomer()
    {
        Current = null;
        if (Queue.Count == 0) return false;
        Current = Queue[0];
        Queue.RemoveAt(0);
        return true;
    }

    /// <summary>Records meeting a seller. True the first time.</summary>
    public bool Meet(SellerDef seller) => Met.Add(seller.Key);

    public Purchase Buy(Visit visit, Appraisal slip)
    {
        Gold -= visit.PricePaid;
        Today.Bought++;
        Today.Spent += visit.PricePaid;
        return new Purchase(visit, slip);
    }

    /// <summary>Notes a find in the collection. True the first time this thing turns up.</summary>
    public bool Collect(Item item)
    {
        if (item.Broken || !Found.Add(item.Entry)) return false;
        Today.NewFinds++;
        return true;
    }

    /// <summary>Puts something on display instead of selling it: you give up its price.</summary>
    public bool Keep(Item item, int value)
    {
        if (!item.Displayable || DisplayFull || Gold < value) return false;
        Display.Add(item);
        Gold -= value;
        Today.Earned -= value;
        return true;
    }

    /// <summary>Takes something off display and sells it.</summary>
    public void SellFromDisplay(Item item)
    {
        if (!Display.Remove(item)) return;
        Gold += item.Value;
    }

    public void Pass() => Today.Passed++;

    public void Earn(int amount)
    {
        Gold += amount;
        Today.Earned += amount;
    }

    /// <summary>Closes the day. Returns the allowance paid if the shop was nearly broke, else 0.</summary>
    public int EndDay()
    {
        if (Gold >= Allowance) return 0;
        int topUp = Allowance - Gold;
        Gold = Allowance;
        return topUp;
    }
}
