using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>Shop hours and what each action costs. Time is the budget that makes inspecting a choice.</summary>
public static class ShopClock
{
    public const int Open = 9 * 60;
    public const int Close = 17 * 60;

    public const int Greeting = 20;
    public const int Peek = 60;
    public const int Knock = 20;
    public const int ExamineLock = 30;
    public const int Offer = 10;

    public static string Format(int minute) => $"{minute / 60}:{minute % 60:00}";
}

public static class Rent
{
    public const int EveryDays = 3;

    /// <summary>Rent for the Nth payment (0-based): it climbs, so the shop has to keep finding bargains.</summary>
    public static int Amount(int paymentIndex) => 150 + 100 * paymentIndex;
}

/// <summary>Builds each visit: who's selling, what's really inside, and what they believe it's worth.</summary>
public static class Market
{
    public static Visit CreateVisit(Random rng, SellerDef seller, Func<Provenance, Appraiser> appraiserFor)
    {
        var origin = Provenances.Get(rng.PickOne(seller.Origins));
        // Sellers price off the average chest from that origin; the long tail of legendaries is part of the price.
        float prior = appraiserFor(origin).Prior.Mean;
        var chest = ChestGenerator.Generate(rng, origin);
        var claimed = origin;
        float ask, reservation;

        switch (seller.Type)
        {
            case Archetype.Clueless:
            {
                float belief = prior * rng.Between(0.6f, 1.4f);
                ask = belief * rng.Between(1.05f, 1.3f);
                reservation = belief * rng.Between(0.6f, 0.8f);
                break;
            }
            case Archetype.Cautious:
            {
                float belief = appraiserFor(origin).Estimate(chest, new Clues(false, false, true)).Mean;
                ask = belief * 1.25f;
                reservation = belief * rng.Between(0.75f, 0.9f);
                break;
            }
            case Archetype.Braggart:
            {
                float belief = prior * rng.Between(1.5f, 2.2f);
                ask = belief * rng.Between(1.0f, 1.15f);
                reservation = belief * rng.Between(0.55f, 0.7f);
                if (rng.NextFloat() < 0.3f) reservation *= 0.5f; // secretly in debt
                break;
            }
            case Archetype.Collector:
            {
                // Knows the contents, but never checks for a false bottom.
                float belief = chest.Value * rng.Between(0.9f, 1.1f);
                ask = belief * rng.Between(1.25f, 1.4f);
                reservation = belief * rng.Between(0.85f, 0.95f);
                break;
            }
            case Archetype.Salvager:
            {
                ask = reservation = prior * rng.Between(0.7f, 1.3f);
                break;
            }
            case Archetype.Con:
            {
                var farmhouse = Provenances.Get("Farmhouse Cellar");
                if (rng.NextFloat() < 0.7f)
                {
                    // Junk from a cellar, ballasted with stones and wearing a fake Royal Seal.
                    chest = ChestGenerator.Tamper(rng, ChestGenerator.Generate(rng, farmhouse, rng.Between(0f, 0.25f)));
                    ask = appraiserFor(claimed).Prior.Mean * rng.Between(0.9f, 1.3f);
                    reservation = ask * rng.Between(0.2f, 0.3f);
                }
                else
                {
                    chest = ChestGenerator.Generate(rng, farmhouse);
                    claimed = farmhouse;
                    ask = appraiserFor(farmhouse).Prior.Mean * rng.Between(1.0f, 1.3f);
                    reservation = ask * 0.6f;
                }
                break;
            }
            case Archetype.Desperate:
            {
                ask = prior * rng.Between(0.7f, 0.9f);
                reservation = prior * rng.Between(0.3f, 0.45f);
                break;
            }
            default: // Novice
            {
                float belief = prior * rng.Between(0.5f, 1.2f);
                ask = belief * rng.Between(0.9f, 1.1f);
                reservation = belief * rng.Between(0.5f, 0.7f);
                break;
            }
        }

        return new Visit(seller, chest, claimed, (int)ask, (int)reservation, rng);
    }
}

/// <summary>A chest bought today, waiting in the back room.</summary>
public sealed class Purchase
{
    public Visit Visit { get; }
    public Chest Chest => Visit.Chest;
    public int Paid => Visit.PricePaid;
    public bool Shaken { get; set; }
    public bool Opened { get; set; }
    public int Haul { get; set; }

    public Purchase(Visit visit)
    {
        Visit = visit;
    }
}

public sealed class GameState
{
    public const int StartingGold = 250;

    public int Seed { get; }
    public int Gold { get; set; } = StartingGold;
    public int Day { get; private set; }
    public int Minute { get; private set; } = ShopClock.Open;
    public int RentPayments { get; private set; }
    public Random Rng { get; private set; } = new();

    public List<Visit> Queue { get; } = new();
    public Visit Current { get; private set; }
    public List<Purchase> Purchases { get; } = new();

    /// <summary>The purchase being opened in the back room.</summary>
    public Purchase Opening { get; set; }
    public Chest Chest => Opening?.Chest;
    public int PricePaid => Opening?.Paid ?? 0;
    public PickGrade Grade { get; set; }
    public bool HeardFragile => Opening is { Shaken: true } && Opening.Chest.HasFragile;

    public int RentDueDay => (RentPayments + 1) * Rent.EveryDays;
    public int RentDue => Rent.Amount(RentPayments);
    public int MinutesLeft => ShopClock.Close - Minute;

    public GameState(int seed)
    {
        Seed = seed;
    }

    public Appraiser AppraiserFor(Provenance provenance) => Appraiser.For(provenance);

    public void StartDay()
    {
        Day++;
        Minute = ShopClock.Open;
        Rng = new Random(unchecked(Seed * 7919 + Day));
        Purchases.Clear();
        Queue.Clear();
        Current = null;
        int count = 6 + (Rng.NextFloat() < 0.5f ? 1 : 0);
        foreach (var seller in Sellers.All.OrderBy(_ => Rng.Next()).Take(count))
            Queue.Add(Market.CreateVisit(Rng, seller, AppraiserFor));
    }

    /// <summary>Calls in the next customer, if the shop is still open and anyone is waiting.</summary>
    public bool NextCustomer()
    {
        Current = null;
        if (Queue.Count == 0 || MinutesLeft < ShopClock.Greeting) return false;
        Current = Queue[0];
        Queue.RemoveAt(0);
        Spend(ShopClock.Greeting);
        return true;
    }

    public bool CanAfford(int minutes) => MinutesLeft >= minutes;

    public void Spend(int minutes) => Minute = Math.Min(ShopClock.Close, Minute + minutes);

    public Purchase Buy(Visit visit)
    {
        Gold -= visit.PricePaid;
        var purchase = new Purchase(visit);
        Purchases.Add(purchase);
        return purchase;
    }

    /// <summary>Pays rent if it's due today. Returns false if the shop can't cover it.</summary>
    public bool PayRentIfDue(out int paid)
    {
        paid = 0;
        if (Day < RentDueDay) return true;
        paid = RentDue;
        if (Gold < paid) return false;
        Gold -= paid;
        RentPayments++;
        return true;
    }

    public void Reset()
    {
        Gold = StartingGold;
        Day = 0;
        RentPayments = 0;
    }
}
