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

/// <summary>A chest bought today. It's opened straight away.</summary>
public sealed class Purchase
{
    public Visit Visit { get; }
    public Chest Chest => Visit.Chest;
    public int Paid => Visit.PricePaid;
    public bool Shaken { get; set; }
    public int Haul { get; set; }

    public Purchase(Visit visit)
    {
        Visit = visit;
    }
}

/// <summary>How the day went, for the end-of-day summary.</summary>
public sealed class DayStats
{
    public int Bought, Passed, Spent, Earned;
    public int Profit => Earned - Spent;
}

public sealed class GameState
{
    public const int StartingGold = 60;

    /// <summary>If you end a day broke, Bram tides you over with this much.</summary>
    public const int Allowance = 30;

    public int Seed { get; }
    public int Gold { get; set; } = StartingGold;
    public int Day { get; private set; }
    public int Tier { get; private set; } = 1;
    public Random Rng { get; private set; } = new();

    /// <summary>What the player has learned. Knowledge is the real progression.</summary>
    public Ledger Ledger { get; } = new();

    /// <summary>Whether Bram's tips are shown (and the first customer is a gentle one).</summary>
    public bool Guided { get; set; } = true;

    public HashSet<string> HintsSeen { get; } = new();
    readonly HashSet<string> _owned = new();

    public List<Visit> Queue { get; } = new();
    public Visit Current { get; private set; }
    public DayStats Today { get; private set; } = new();
    public int PeeksLeft { get; private set; }

    /// <summary>The purchase being opened.</summary>
    public Purchase Opening { get; set; }
    public Chest Chest => Opening?.Chest;
    public int PricePaid => Opening?.Paid ?? 0;
    public PickGrade Grade { get; set; }
    public bool HeardFragile => Opening is { Shaken: true } && Opening.Chest.HasFragile;

    public GameState(int seed)
    {
        Seed = seed;
        // Uncle Bram's notes: the everyday things any pawnbroker knows. Everything past the stall you learn yourself.
        foreach (var item in ItemCatalog.UpTo(1)) Ledger.Learn(item);
    }

    public bool Has(string upgrade) => _owned.Contains(upgrade);

    /// <summary>Haggling opens up on the second day, once the basics have sunk in.</summary>
    public bool CanHaggle => Day >= 2 || !Guided || Tier > 1;

    public int CustomersPerDay => 3 + Tier + (Has("sign") ? 1 : 0);
    public int PeeksPerDay => Has("periscope") ? 4 : Has("lantern") ? 3 : Has("peephole") ? 2 : 0;

    /// <summary>How many things a peek shows, nearest the keyhole first.</summary>
    public int PeekReach => Has("periscope") ? 99 : Has("lantern") ? 2 : 1;

    public bool Won => Has("warrant");

    public bool CanBuy(Upgrade upgrade) => Gold >= upgrade.Cost && Upgrades.Available(this).Contains(upgrade);

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
        foreach (var upgrade in Upgrades.All.Where(u => u.MinTier < tier || (u.IsRenovation && u.SetsTier <= tier)))
            if (upgrade.Key != "warrant") _owned.Add(upgrade.Key);
        Tier = tier;
        Gold = gold;
        foreach (var item in ItemCatalog.UpTo(tier - 1)) Ledger.Learn(item);
    }

    public void StartDay()
    {
        Day++;
        Rng = new Random(unchecked(Seed * 7919 + Day));
        Queue.Clear();
        Current = null;
        Today = new DayStats();
        PeeksLeft = PeeksPerDay;

        if (Day == 1 && Guided)
        {
            // A gentle first customer: three different sounds, all of them easy, and a fair price.
            var chest = Chest.Of(Origins.Farm, 0, "copper_pouch", "honey_jar", "wool_coat");
            Queue.Add(new Visit(Sellers.Get("hob"), chest, 25, 20, Rng, "Found this in the barn. No idea what's in it. 25g?"));
        }

        // Each renovation brings its own crowd; the old regulars still drop by now and then. Nobody comes twice in a
        // day unless everyone else already has.
        var sellers = Sellers.All.Where(s => s.Tier <= Tier).ToArray();
        while (Queue.Count < CustomersPerDay)
        {
            var fresh = sellers.Where(s => Queue.All(v => v.Seller != s)).ToArray();
            var pool = fresh.Length > 0 ? fresh : sellers.Where(s => Queue[^1].Seller != s).ToArray();
            var seller = Rng.PickWeighted(pool, s => s.Tier == Tier ? 3f : s.Tier == Tier - 1 ? 1f : 0.3f);
            Queue.Add(Market.CreateVisit(Rng, seller, Tier));
        }
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

    public bool UsePeek()
    {
        if (PeeksLeft <= 0) return false;
        PeeksLeft--;
        return true;
    }

    public Purchase Buy(Visit visit)
    {
        Gold -= visit.PricePaid;
        Today.Bought++;
        Today.Spent += visit.PricePaid;
        return new Purchase(visit);
    }

    public void Pass() => Today.Passed++;

    public void Earn(int amount)
    {
        Gold += amount;
        Today.Earned += amount;
    }

    /// <summary>Closes the day. Returns the allowance paid if the shop was broke, else 0.</summary>
    public int EndDay()
    {
        if (Gold >= Allowance) return 0;
        int topUp = Allowance - Gold;
        Gold = Allowance;
        return topUp;
    }
}
