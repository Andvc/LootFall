using System;
using System.Linq;

namespace LootFall.Core;

/// <summary>How a seller values their chest and haggles. Players learn these by meeting the same people again.</summary>
public enum Archetype
{
    /// <summary>Has no idea what's inside; prices on a hunch. Honest.</summary>
    Clueless,
    /// <summary>Judges by the lock, like an old scavenger. Fair, firm.</summary>
    Cautious,
    /// <summary>Thinks every heirloom is priceless. Sometimes secretly broke.</summary>
    Braggart,
    /// <summary>Knows exactly what's inside, but never knocks for false bottoms.</summary>
    Collector,
    /// <summary>Names one price. Take it or leave it.</summary>
    Salvager,
    /// <summary>Often sells junk dressed up as treasure. Folds fast when pushed.</summary>
    Con,
    /// <summary>Needs money today. Sells cheap.</summary>
    Desperate,
    /// <summary>Nervous apprentice selling fragile tower stock below its worth.</summary>
    Novice,
}

public sealed record SellerDef(
    string Key,
    string Name,
    Archetype Type,
    int Patience,
    float Concession,
    string[] Origins,
    string[] Pitches,
    string[] Counters,
    string[] Accepts,
    string[] Insulted,
    string[] Leaves,
    string[] Nervous);

public static class Sellers
{
    // Shared lines; {price}, {counter} and {origin} are filled in per visit.
    static readonly string[] Shrug = { "Suit yourself.", "Your loss.", "Another time, then." };

    public static readonly SellerDef[] All =
    {
        new("hob", "Hob the Farmer", Archetype.Clueless, 3, 0.5f,
            new[] { "Farmhouse Cellar", "Soldier's Footlocker" },
            new[] { "Dug this up under the turnips. No idea what's in it. {price}?", "Found it in the old barn. Heavy thing. Say, {price}?" },
            new[] { "Hmm. Meet me at {counter}?", "My wife'd skin me. {counter}." },
            new[] { "Deal! Wait till I tell the missus.", "Done. Easy money!" },
            new[] { "That won't buy a sack of seed.", "Now that's just rude." },
            new[] { "I'll try the next town over.", "Forget it, then." },
            Array.Empty<string>()),
        new("marta", "Old Marta", Archetype.Cautious, 3, 0.4f,
            new[] { "Shipwreck Salvage", "Noble Estate", "Soldier's Footlocker", "Wizard's Tower", "Farmhouse Cellar" },
            new[] { "Look at that lock, dear. That's not junk. {price}.", "I know a lock when I see one. {price}." },
            new[] { "{counter}, and my knees thank you.", "I'll come down to {counter}. No further." },
            new[] { "Fair's fair.", "Take good care of it." },
            new[] { "I was haggling before your mother was born.", "Don't insult an old woman." },
            new[] { "I'll sell it to someone with manners.", "Good day." },
            Array.Empty<string>()),
        new("vex", "Baron Vex", Archetype.Braggart, 2, 0.3f,
            new[] { "Noble Estate" },
            new[] { "A Vex family heirloom. Priceless, really. But for you, {price}.", "From my own estate! Practically a steal at {price}." },
            new[] { "A Vex does not haggle. ...{counter}.", "Absurd! {counter}, and not a copper less." },
            new[] { "You have the eye of a gentleman.", "Splendid. Splendid!" },
            new[] { "Do you know who I AM?", "Peasant arithmetic!" },
            new[] { "I shall take my heirloom elsewhere!", "Unbelievable. Good DAY." },
            Array.Empty<string>()),
        new("pip", "Pip the Sharp", Archetype.Collector, 2, 0.2f,
            new[] { "Noble Estate", "Wizard's Tower", "Shipwreck Salvage" },
            new[] { "I know what's in this one. {price}, and it's fair.", "Quality piece. {price}." },
            new[] { "{counter}. I know its worth.", "Mm. {counter}." },
            new[] { "Pleasure.", "You won't regret it. Probably." },
            new[] { "Don't waste my time.", "Cute." },
            new[] { "I'll find a buyer who knows quality.", "Pass." },
            Array.Empty<string>()),
        new("gull", "Captain Gull", Archetype.Salvager, 1, 0f,
            new[] { "Shipwreck Salvage" },
            new[] { "Hauled it off the Gullmouth reef. {price}. Take it or leave it.", "Sea gave it up this morning. {price}, no haggling." },
            Array.Empty<string>(),
            new[] { "Aye.", "Done. Tide's turning, I'm off." },
            Array.Empty<string>(),
            new[] { "Said take it or leave it. I'm leaving it.", "Waste of a tide." },
            Array.Empty<string>()),
        new("silas", "Silas", Archetype.Con, 3, 0.8f,
            new[] { "Noble Estate", "Wizard's Tower" },
            new[] { "From the {origin}, friend. Very rare. {price}.", "Don't tell anyone where I got this. {origin}. {price}." },
            new[] { "You drive a hard bargain. {counter}?", "For you, friend... {counter}." },
            new[] { "Wonderful! No refunds, of course.", "A pleasure. I must be going." },
            new[] { "Friend, friend. Be serious.", "Ha! No." },
            new[] { "Plenty of other buyers, friend.", "I don't need this." },
            new[] { "Careful with that! It's... very old.", "No need to look so closely, friend.", "Ha. Ha. Is it warm in here?" }),
        new("penn", "Widow Penn", Archetype.Desperate, 4, 0.7f,
            new[] { "Soldier's Footlocker", "Noble Estate" },
            new[] { "It was my husband's. I need the money... {price}?", "Rent's due. Would you give {price} for it?" },
            new[] { "Could you do {counter}? Please?", "{counter}... I suppose." },
            new[] { "Thank you. Truly.", "Bless you." },
            new[] { "Oh. I see.", "That's... very little." },
            new[] { "I'm sorry, I can't.", "I'll manage somehow." },
            Array.Empty<string>()),
        new("wren", "Wren the Apprentice", Archetype.Novice, 3, 0.5f,
            new[] { "Wizard's Tower" },
            new[] { "Master said to sell this. I think it's potions? {price}?", "Um. From the tower. Is {price} okay?" },
            new[] { "Um. {counter}?", "Master will be cross. {counter}?" },
            new[] { "Oh good! Careful, it clinks.", "Thank you! Don't tell Master the price." },
            new[] { "That seems low? I think?", "Oh no." },
            new[] { "I should ask Master first. Sorry!", "I'll come back. Maybe." },
            Array.Empty<string>()),
    };

    public static SellerDef Get(string key) => All.First(s => s.Key == key);

    public static string Fill(string line, int price, int counter, string origin) =>
        line.Replace("{price}", $"{price}g").Replace("{counter}", $"{counter}g").Replace("{origin}", origin);

    public static string PassLine(Random rng) => rng.PickOne(Shrug);
}

public enum ResponseKind { Accept, EagerAccept, Counter, Insulted, Leave }

public readonly record struct Response(ResponseKind Kind, int Price, string Line);

public enum VisitOutcome { Haggling, Bought, Walked, Passed }

/// <summary>
/// One seller at the counter with one sealed chest. Their belief, walk-away price and patience are hidden;
/// the player only sees the asking price, the story, the chest, and how the seller reacts.
/// </summary>
public sealed class Visit
{
    public SellerDef Seller { get; }
    public Chest Chest { get; }

    /// <summary>Where the seller says it's from. A con artist lies.</summary>
    public Provenance Claimed { get; }

    public int OpeningAsk { get; }
    public int Ask { get; private set; }
    public int Patience { get; private set; }
    public int MaxPatience { get; }
    public VisitOutcome Outcome { get; private set; } = VisitOutcome.Haggling;
    public int PricePaid { get; private set; }
    public string Pitch { get; }

    /// <summary>Hidden: the least the seller will take.</summary>
    internal int Reservation { get; }

    public bool Lying => Claimed != Chest.Provenance || Chest.Tampered;

    /// <summary>Set on the guided first day: the clue this customer is here to teach.</summary>
    public Lesson Lesson { get; init; }

    public Visit(SellerDef seller, Chest chest, Provenance claimed, int ask, int reservation, Random rng, string pitch = null)
    {
        Seller = seller;
        Chest = chest;
        Claimed = claimed;
        OpeningAsk = Ask = Math.Max(10, Money.RoundTo5(ask));
        Reservation = Math.Clamp(Money.RoundTo5(reservation), 5, OpeningAsk);
        Patience = MaxPatience = seller.Patience;
        Pitch = pitch ?? Sellers.Fill(rng.PickOne(seller.Pitches), OpeningAsk, 0, claimed.Name);
    }

    /// <summary>
    /// The seller's answer to an offer. A seller with patience to spare pushes back once even on an acceptable offer,
    /// so "they countered" doesn't simply mean "too low". Insults cost twice the patience.
    /// </summary>
    public Response Offer(int amount, Random rng)
    {
        if (Outcome != VisitOutcome.Haggling) throw new InvalidOperationException("Visit is over");
        amount = Math.Max(0, amount);

        if (amount >= Ask)
            return Sell(Ask, ResponseKind.EagerAccept, rng);

        // "Take it or leave it" means it.
        if (Seller.Concession <= 0)
            return Walk(rng);

        if (amount >= Reservation)
        {
            bool tryOnce = Patience >= 2 && Seller.Concession > 0 && amount < Reservation + 0.4f * (Ask - Reservation);
            if (!tryOnce) return Sell(amount, ResponseKind.Accept, rng);
            return CounterOffer(amount, rng);
        }

        if (amount >= Reservation * 0.6f && Seller.Concession > 0)
            return CounterOffer(amount, rng);

        Patience -= Seller.Concession > 0 ? 2 : 1;
        if (Patience <= 0) return Walk(rng);
        return new Response(ResponseKind.Insulted, Ask, rng.PickOne(Seller.Insulted));
    }

    /// <summary>Pay what they're asking right now.</summary>
    public Response PayAsk(Random rng) => Sell(Ask, ResponseKind.Accept, rng);

    public string Pass(Random rng)
    {
        Outcome = VisitOutcome.Passed;
        return Sellers.PassLine(rng);
    }

    /// <summary>When the player looks hard at a con artist's chest, they tend to crack.</summary>
    public string NervousLine(Random rng) =>
        Lying && Seller.Nervous.Length > 0 && rng.NextFloat() < 0.7f ? rng.PickOne(Seller.Nervous) : null;

    /// <summary>0 calm, 1 annoyed, 2 about to leave. Shown as the seller's mood, never as a number.</summary>
    public int Mood => Patience >= MaxPatience ? 0 : Patience >= 2 ? 1 : 2;

    Response CounterOffer(int amount, Random rng)
    {
        Patience--;
        if (Patience <= 0) return Walk(rng);
        int counter = Math.Max(Reservation, Money.RoundTo5(Ask - (Ask - amount) * Seller.Concession));
        if (counter >= Ask) counter = Math.Max(Reservation, Ask - 5);
        Ask = counter;
        return new Response(ResponseKind.Counter, Ask, Sellers.Fill(rng.PickOne(Seller.Counters), amount, Ask, Claimed.Name));
    }

    Response Sell(int price, ResponseKind kind, Random rng)
    {
        Outcome = VisitOutcome.Bought;
        PricePaid = price;
        return new Response(kind, price, rng.PickOne(Seller.Accepts));
    }

    Response Walk(Random rng)
    {
        Outcome = VisitOutcome.Walked;
        return new Response(ResponseKind.Leave, Ask, rng.PickOne(Seller.Leaves));
    }
}

public static class Money
{
    public static int RoundTo5(float value) => Math.Max(5, (int)MathF.Round(value / 5f) * 5);
}
