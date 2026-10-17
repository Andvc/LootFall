using System;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// How a seller decides what to ask. Each one has a blind spot that one of your clues can see through, which is where
/// profit comes from: you know something about the chest that they don't.
/// </summary>
public enum PriceRule
{
    /// <summary>Guesses. Sometimes way off, either way.</summary>
    Hunch,
    /// <summary>Needs money today: asks little, takes less.</summary>
    Desperate,
    /// <summary>Counts the rattles, not what's rattling.</summary>
    ByCount,
    /// <summary>The same few coins for anything.</summary>
    Flat,
    /// <summary>Heavy means valuable. The scale shows when it isn't.</summary>
    ByWeight,
    /// <summary>Knows what's inside and prices it right. Bargains only from what they can't know about.</summary>
    Fair,
    /// <summary>Judges by the lock, not what's behind it.</summary>
    ByLock,
    /// <summary>Knows what kind of thing it is by the sound, but not which: copper or silver, iron or steel.</summary>
    ByFamily,
    /// <summary>Magic is priceless, everything else is junk. The compass shows which is which.</summary>
    ByMagic,
    /// <summary>Asks far too much; folds when pushed, sometimes.</summary>
    Braggart,
    /// <summary>A crook: junk dressed up as treasure.</summary>
    Con,
}

/// <param name="Key">Portrait under res://assets/sprites/sellers/.</param>
/// <param name="Tier">The shop tier at which they start coming in.</param>
/// <param name="Quirk">What you learn about them: their pricing blind spot, in a sentence.</param>
/// <param name="OnePrice">Never haggles.</param>
public sealed record SellerDef(
    string Key,
    string Name,
    int Tier,
    Origin Origin,
    PriceRule Rule,
    string Quirk,
    bool OnePrice,
    string[] Pitches,
    string[] Counters,
    string[] Accepts,
    string[] Leaves);

public static class Sellers
{
    static readonly string[] Shrug = { "Suit yourself.", "Your loss.", "Another time, then." };

    public static readonly SellerDef[] All =
    {
        // Tier 1: the village. Everyday things, simple prices.
        new("hob", "Hob the Farmer", 1, Origins.Farm, PriceRule.Hunch, "Prices on a hunch. Sometimes way off, either way.", false,
            new[] { "Dug this up under the turnips. {price}?", "Found it in the old barn. Say, {price}?" },
            new[] { "Hmm. {counter}, and that's my last word.", "My wife'd skin me. {counter}." },
            new[] { "Deal! Wait till I tell the missus.", "Done. Easy money!" },
            new[] { "Forget it, then." }),
        new("penn", "Widow Penn", 1, Origins.Village, PriceRule.Desperate, "Needs money. Asks little and takes less.", false,
            new[] { "It was my husband's. Would you give {price}?", "Rent's due. {price}, perhaps?" },
            new[] { "Could you do {counter}? Please?", "{counter}... I can't go lower." },
            new[] { "Thank you. Truly.", "Bless you." },
            new[] { "I'll manage somehow." }),
        new("mags", "Mags the Fishwife", 1, Origins.Village, PriceRule.ByCount, "Prices by how many things rattle, not what they are.", false,
            new[] { "Hear all that? Lots in there! {price}.", "Full to the brim, love. {price}." },
            new[] { "{counter}, and I'm robbing myself.", "Fine, fine. {counter}." },
            new[] { "Pleasure doing business!", "Done, love." },
            new[] { "Your loss, love." }),
        new("tam", "Little Tam", 1, Origins.Farm, PriceRule.Flat, "Asks the same few coins for anything.", false,
            new[] { "Found it by the river. {price}? Please?", "Is {price} too much?" },
            new[] { "Um. {counter}?", "{counter}! Final offer! I think." },
            new[] { "Yay!", "I'm buying sweets!" },
            new[] { "Aww." }),

        // Tier 2: soldiers and merchants.
        new("brask", "Sergeant Brask", 2, Origins.Barracks, PriceRule.ByWeight, "Prices by weight. Heavy junk is treasure to him.", false,
            new[] { "Feel the weight of that! {price}.", "Heavy as a mule. {price}, and cheap at that." },
            new[] { "{counter}. Final.", "A soldier doesn't haggle. ...{counter}." },
            new[] { "Done.", "Pleasure, citizen." },
            new[] { "Waste of my leave." }),
        new("tobias", "Tobias the Tinker", 2, Origins.Market, PriceRule.ByFamily, "Prices by the kind of sound. Can't tell copper from silver, or iron from steel.", false,
            new[] { "Coins, a bit of metal, the usual. {price}.", "Odds and ends. {price}?" },
            new[] { "{counter}, and you're robbing me.", "Tch. {counter}." },
            new[] { "Done!", "Good doing business." },
            new[] { "Hmph. Next time." }),
        new("pip", "Pip the Sharp", 4, Origins.Market, PriceRule.Fair, "Knows exactly what's inside. A fair price, rarely a bargain.", false,
            new[] { "I know what's in this one. {price}, and it's fair.", "Quality. {price}." },
            new[] { "{counter}. I know its worth.", "Mm. {counter}." },
            new[] { "Pleasure.", "You won't regret it. Probably." },
            new[] { "I'll find a buyer who knows quality." }),
        new("marta", "Old Marta", 2, Origins.Market, PriceRule.ByLock, "Prices by the lock, not what's behind it.", false,
            new[] { "Look at that lock, dear. {price}.", "I know a lock when I see one. {price}." },
            new[] { "{counter}, and my knees thank you.", "I'll come down to {counter}. No further." },
            new[] { "Fair's fair.", "Take good care of it." },
            new[] { "I'll sell it to someone with manners." }),

        // Tier 3: mages.
        new("wren", "Wren the Apprentice", 3, Origins.Tower, PriceRule.ByMagic, "Thinks anything magic is priceless and everything else is junk.", false,
            new[] { "Master said to sell this. Um. {price}?", "From the tower. Is {price} okay?" },
            new[] { "Um. {counter}?", "Master will be cross. {counter}?" },
            new[] { "Oh good! Careful, it might clink.", "Thank you! Don't tell Master." },
            new[] { "I should ask Master first. Sorry!" }),
        new("orrin", "Magister Orrin", 3, Origins.Tower, PriceRule.ByMagic, "Prices magic right and the rest as junk. Never haggles.", true,
            new[] { "Surplus from my study. {price}.", "{price}. I don't bargain." },
            Array.Empty<string>(),
            new[] { "Very well.", "Mind the scrolls." },
            new[] { "Then we're done." }),

        // Tier 4: nobles, adventurers and crooks.
        new("vex", "Baron Vex", 4, Origins.Manor, PriceRule.Braggart, "Asks double. Comes down a long way if you push.", false,
            new[] { "A Vex family heirloom. For you, {price}.", "Priceless, really. But {price}." },
            new[] { "Absurd! ...{counter}.", "A Vex does not haggle. {counter}." },
            new[] { "You have the eye of a gentleman.", "Splendid." },
            new[] { "Peasant arithmetic! Good DAY." }),
        new("gull", "Captain Gull", 4, Origins.Sea, PriceRule.Hunch, "One price. Take it or leave it.", true,
            new[] { "Hauled it off the Gullmouth reef. {price}. No haggling.", "Sea gave it up this morning. {price}." },
            Array.Empty<string>(),
            new[] { "Aye.", "Tide's turning, I'm off." },
            new[] { "Waste of a tide." }),
        new("silas", "Silas", 4, Origins.Manor, PriceRule.Con, "Too good to be true, usually. Check everything.", false,
            new[] { "Gold, friend. Hear it? {price}.", "From a noble house. Very rare. {price}." },
            new[] { "For you, friend... {counter}.", "You drive a hard bargain. {counter}." },
            new[] { "Wonderful! No refunds.", "A pleasure. I must be going." },
            new[] { "Plenty of other buyers, friend." }),
    };

    public static SellerDef Get(string key) => All.First(s => s.Key == key);

    public static string Fill(string line, int price, int counter) =>
        line.Replace("{price}", $"{price}g").Replace("{counter}", $"{counter}g");

    public static string PassLine(Random rng) => rng.PickOne(Shrug);
}

/// <summary>Builds a visit: what the seller brings and what they ask for it.</summary>
public static class Market
{
    public static Visit CreateVisit(Random rng, SellerDef seller, int shopTier)
    {
        var typical = Appraiser.For(seller);
        bool falseBottoms = shopTier >= 4 && seller.Rule is PriceRule.Fair or PriceRule.Braggart or PriceRule.ByLock;
        var chest = seller.Rule == PriceRule.Con
            ? ChestGenerator.Tamper(rng, ChestGenerator.Generate(rng, Origins.Farm, 1))
            : ChestGenerator.Generate(rng, seller.Origin, seller.Tier, falseBottoms);

        float ask, bottom;
        switch (seller.Rule)
        {
            case PriceRule.Hunch:
                ask = typical.MeanValue * rng.Between(0.5f, 1.5f);
                bottom = ask * rng.Between(0.85f, 0.95f);
                break;
            case PriceRule.Desperate:
                ask = typical.MeanValue * rng.Between(0.7f, 1.0f);
                bottom = ask * 0.75f;
                break;
            case PriceRule.ByCount:
                ask = chest.Items.Count * typical.MeanValue / typical.MeanCount * rng.Between(0.9f, 1.1f);
                bottom = ask * 0.8f;
                break;
            case PriceRule.Flat:
                ask = typical.MeanValue * rng.Between(0.8f, 0.95f);
                bottom = ask * 0.8f;
                break;
            case PriceRule.ByWeight:
                ask = chest.ContentsWeight * typical.MeanValue / typical.MeanWeight * rng.Between(0.9f, 1.1f);
                bottom = ask * 0.8f;
                break;
            case PriceRule.Fair:
                ask = chest.Value * rng.Between(1.1f, 1.3f);
                bottom = chest.Value * rng.Between(0.95f, 1.05f);
                break;
            case PriceRule.ByLock:
                ask = typical.MeanValue * (0.4f + 0.45f * chest.Lock.Index) * rng.Between(0.9f, 1.1f);
                bottom = ask * 0.8f;
                break;
            case PriceRule.ByFamily:
            {
                // Every jingle priced as an average coin, every clank as average metal: right on average, blind to which.
                float guess = chest.Items.Sum(i => FamilyAverage(seller.Tier, i.Def.Family));
                ask = guess * rng.Between(0.85f, 1.05f);
                bottom = ask * 0.8f;
                break;
            }
            case PriceRule.ByMagic:
            {
                float magic = chest.Items.Where(i => i.Def.Magic > 0).Sum(i => i.Value);
                float mundane = chest.Items.Where(i => i.Def.Magic == 0).Sum(i => i.Value);
                ask = (magic * 1.5f + mundane * 0.5f) * rng.Between(0.9f, 1.1f);
                bottom = ask * 0.8f;
                break;
            }
            case PriceRule.Braggart:
                ask = typical.MeanValue * rng.Between(1.6f, 2.4f);
                bottom = ask * rng.Between(0.3f, 0.5f);
                break;
            default: // Con: asks like it's the real thing, takes almost anything
                ask = typical.MeanValue * rng.Between(0.8f, 1.2f);
                bottom = ask * rng.Between(0.25f, 0.4f);
                break;
        }
        if (seller.OnePrice) bottom = ask;
        return new Visit(seller, chest, (int)ask, (int)bottom, rng);
    }

    /// <summary>What a thing of this family is worth on average, among what turns up at a tier.</summary>
    public static float FamilyAverage(int tier, Family family)
    {
        var items = ItemCatalog.UpTo(tier).Where(d => d.Family == family).ToArray();
        return items.Sum(d => d.Value * ItemCatalog.Commonness(d)) / items.Sum(ItemCatalog.Commonness);
    }
}

public enum ResponseKind { Accept, Counter, Refuse, Leave }

public readonly record struct Response(ResponseKind Kind, int Price, string Line);

public enum VisitOutcome { Deciding, Bought, Walked, Passed }

/// <summary>
/// One seller at the counter with one chest. They ask a price. You can buy, pass, or make one offer: if it's at or
/// above what they'll really take, it's yours at that price; if not, they name their final price.
/// </summary>
public sealed class Visit
{
    public SellerDef Seller { get; }
    public Chest Chest { get; }
    public int OpeningAsk { get; }
    public int Ask { get; private set; }
    public int OffersLeft { get; private set; } = 1;
    public VisitOutcome Outcome { get; private set; } = VisitOutcome.Deciding;
    public int PricePaid { get; private set; }
    public string Pitch { get; }

    /// <summary>Hidden: the least the seller will take.</summary>
    internal int Bottom { get; }

    public Visit(SellerDef seller, Chest chest, int ask, int bottom, Random rng, string pitch = null)
    {
        Seller = seller;
        Chest = chest;
        OpeningAsk = Ask = Math.Max(5, Money.RoundTo5(ask));
        Bottom = Math.Clamp(Money.RoundTo5(bottom), 5, OpeningAsk);
        if (seller.OnePrice) OffersLeft = 0;
        Pitch = pitch ?? Sellers.Fill(rng.PickOne(seller.Pitches), OpeningAsk, 0);
    }

    public bool CanOffer => Outcome == VisitOutcome.Deciding && OffersLeft > 0;

    public Response Offer(int amount, Random rng)
    {
        if (!CanOffer) throw new InvalidOperationException("No offers left");
        OffersLeft--;
        amount = Math.Max(0, amount);
        if (amount >= Bottom) return Sell(Math.Min(amount, Ask), rng);
        if (Seller.Rule == PriceRule.Braggart && amount < Ask * 0.3f)
        {
            Outcome = VisitOutcome.Walked;
            return new Response(ResponseKind.Leave, Ask, rng.PickOne(Seller.Leaves));
        }
        // Too low: they meet you partway, never below what they'd really take, and that's final.
        Ask = Math.Max(Bottom, Money.RoundTo5(amount + (Ask - amount) * 0.5f));
        return new Response(ResponseKind.Counter, Ask, Sellers.Fill(rng.PickOne(Seller.Counters), amount, Ask));
    }

    public Response Buy(Random rng) => Sell(Ask, rng);

    public string Pass(Random rng)
    {
        Outcome = VisitOutcome.Passed;
        return Sellers.PassLine(rng);
    }

    Response Sell(int price, Random rng)
    {
        Outcome = VisitOutcome.Bought;
        PricePaid = price;
        return new Response(ResponseKind.Accept, price, rng.PickOne(Seller.Accepts));
    }
}

public static class Money
{
    public static int RoundTo5(float value) => Math.Max(5, (int)MathF.Round(value / 5f) * 5);
}
