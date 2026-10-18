using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum VisitOutcome { Deciding, Bought, Passed, Left }

public enum ResponseKind { Accept, Counter, TryAgain, Leave }

public readonly record struct Response(ResponseKind Kind, int Price, string Line);

/// <summary>
/// One seller at the counter with one chest, what they say is in it, and how they trade. Fixed: buy at their price or
/// pass. Haggle: one offer, which they take, answer with a final price, or storm off at if it's insulting. Offer: you
/// name a price, twice at most.
/// </summary>
public sealed class Visit
{
    public SellerDef Seller { get; }
    public Chest Chest { get; }

    /// <summary>The kinds of things the seller says are inside (what they heard, or what they packed).</summary>
    public IReadOnlyList<Category> Claim { get; }

    /// <summary>The seller swears there's a false bottom.</summary>
    public bool ClaimsHidden => Seller.ClaimsHidden;

    public Trade Trade { get; }

    /// <summary>The asking price; 0 while they're waiting for you to name one.</summary>
    public int Ask { get; private set; }

    public string Pitch { get; }
    public VisitOutcome Outcome { get; private set; } = VisitOutcome.Deciding;
    public int PricePaid { get; private set; }
    public int OffersMade { get; private set; }
    public bool Angered { get; private set; }

    /// <summary>Hidden: the least they'll take.</summary>
    internal int Floor { get; private set; }

    /// <summary>The lesson this visit teaches, if it's a seller's first visit.</summary>
    public Lesson Lesson { get; set; }

    /// <summary>A lesson's scripted least they'll take.</summary>
    internal void SetFloor(int floor) => Floor = Math.Max(1, floor);

    /// <summary>What they'd have sold it for: the price on the table, or the least they'd have taken.</summary>
    public int Price => Ask > 0 ? Ask : Floor;

    public Visit(SellerDef seller, Chest chest, IReadOnlyList<Category> claim, Trade trade, int ask, int floor, string pitch)
    {
        Seller = seller;
        Chest = chest;
        Claim = claim;
        Trade = trade;
        Ask = ask;
        Floor = Math.Max(1, floor);
        Pitch = pitch;
    }

    public bool Deciding => Outcome == VisitOutcome.Deciding;

    /// <summary>Whether you can make an offer now.</summary>
    public bool CanOffer => Deciding && (Trade == Trade.Haggle ? OffersMade == 0 : Trade == Trade.Offer && Ask == 0);

    /// <summary>Whether there's a price on the table you can just pay.</summary>
    public bool CanBuy => Deciding && Ask > 0;

    public Response Offer(int amount, Random rng)
    {
        if (!CanOffer) throw new InvalidOperationException("No offer to make");
        OffersMade++;
        amount = Math.Max(1, amount);
        if (amount >= Floor) return new Response(ResponseKind.Accept, amount, Sell(amount, rng));
        if (Trade == Trade.Offer)
        {
            if (OffersMade == 1) return new Response(ResponseKind.TryAgain, 0, rng.PickOne(Seller.Refusals));
            return Leave(rng);
        }
        if (amount < Ask * Market.Insult) return Leave(rng);
        // Not enough: they meet you partway, never below what they'd really take, and that's final.
        Ask = Math.Max(Floor, Money.Round((amount + Ask) / 2f));
        return new Response(ResponseKind.Counter, Ask, rng.PickOne(Seller.Counters).Replace("{price}", $"{Ask}g"));
    }

    Response Leave(Random rng)
    {
        Outcome = VisitOutcome.Left;
        return new Response(ResponseKind.Leave, 0, rng.PickOne(Seller.Leaves));
    }

    public string Buy(Random rng) => Sell(Ask, rng);

    public string Pass(Random rng)
    {
        Outcome = VisitOutcome.Passed;
        return Sellers.PassLine(rng);
    }

    /// <summary>They ran out of patience and took their chest home.</summary>
    public string TimeUp(Random rng) => Leave(rng).Line;

    /// <summary>Doing what they asked you not to: up goes the price, once.</summary>
    public string Anger(Random rng)
    {
        if (Angered || !Deciding) return null;
        Angered = true;
        if (Ask > 0) Ask = Money.Round(Ask * Market.AngerMarkup);
        Floor = Money.Round(Floor * Market.AngerMarkup);
        return rng.PickOne(Seller.Angry);
    }

    string Sell(int price, Random rng)
    {
        Outcome = VisitOutcome.Bought;
        PricePaid = price;
        return rng.PickOne(Seller.Accepts);
    }

    /// <summary>
    /// Why this chest was a good or bad deal, in a sentence or two, in terms of the seller's habit: what they missed,
    /// made up, misjudged or faked, and what nobody could have heard.
    /// </summary>
    public string Explain()
    {
        if (Seller == Story.VaultSeller) return "King Aldric's own hoard, sealed since the war. Every bit of it is yours.";
        string name = Seller.Short;
        var lines = new List<string>();
        if (Chest.Items.Count == 0 && Chest.Hidden == null)
            return "Empty! Nothing rattled because there was nothing there.";

        var gilded = Chest.Items.Where(i => i.Fake && i.Def.FakeIron).ToList();
        var sand = Chest.Items.Where(i => i.Fake && !i.Def.FakeIron).ToList();
        if (gilded.Count > 0)
            lines.Add($"{Join(gilded.Select(i => i.Name))}: gilded iron. A magnet would have silenced {(gilded.Count == 1 ? "it" : "them")}.");
        else if (Seller.Judge == Judgment.Crook)
            lines.Add($"All genuine this time, and {name} sold it for half.");
        if (sand.Count > 0)
            lines.Add($"The {(sand.Count == 1 ? "rustle was" : "rustles were")} sand: it pours at a gentle shake, where paper never rustles.");

        switch (Seller.Judge)
        {
            case Judgment.Counter:
                lines.Add($"{name} charged the same for every rattle she heard: {Claim.Count} of them, " +
                          $"whatever they were.");
                break;
            case Judgment.Weight:
            {
                var heavy = Chest.Items.Where(i => Categories.Heft(i.Category) >= 2).ToList();
                var light = Chest.Items.Where(i => Categories.Heft(i.Category) <= 0.5f).ToList();
                if (heavy.Count > 0) lines.Add($"{name} went by weight: the {Join(heavy.Select(i => i.Word))} made it heavy.");
                if (light.Count > 0) lines.Add($"The {Join(light.Select(i => i.Word))} weighed next to nothing to {name}.");
                break;
            }
            case Judgment.Colour:
                lines.Add($"{name} priced it as an ordinary {Categories.ChestSkin(Chest.Quality)} chest" +
                          (Seller.SensesMagic ? ", and felt for magic." : ", without looking inside."));
                break;
            case Judgment.Dreamer:
                lines.Add($"{name}'s price came from a dream. Only the chest could tell you what it was worth.");
                break;
            case Judgment.MagicSense:
                lines.Add(Chest.Items.Any(i => i.Magic)
                    ? $"{name} felt the magic and wanted its price; the rest she couldn't tell."
                    : $"{name} felt no magic, so she took what you offered.");
                break;
            case Judgment.Jeweller:
                lines.Add($"{name} priced her jewelry at what she paid for it; everything else came free.");
                break;
            case Judgment.Miner:
            {
                var jewels = Chest.Get(Category.Jewels);
                if (jewels != null && Claim.Contains(Category.Jewels)) lines.Add($"{name} priced the {jewels.Name} like a diamond fit for a crown.");
                var junk = Chest.Items.Where(i => Claim.Contains(i.Category) && i.Category is not (Category.Metal or Category.Jewels)).ToList();
                if (junk.Count > 0) lines.Add($"He priced the {Join(junk.Select(i => i.Word))} at half.");
                break;
            }
            case Judgment.Clueless:
                lines.Add($"{name} had no idea what was inside.");
                break;
        }

        var phantoms = Claim.Where(c => !Chest.Has(c)).ToList();
        if (phantoms.Count > 0)
        {
            string kind = Categories.Get(phantoms[0]).Name.ToLowerInvariant();
            lines.Add($"{name} made up the {Categories.Get(phantoms[0]).Word}: there {(kind.EndsWith('s') ? "were" : "was")} no {kind}.");
        }

        // What a listener couldn't hear, and why; and what they heard but never counted.
        if (Seller.Judge is Judgment.Ear or Judgment.Counter or Judgment.Miner)
        {
            var noticed = Seller.Notices(Chest).ToHashSet();
            var unheard = Chest.Items.Where(i => !noticed.Contains(i) && !i.Fake).ToList();
            if (unheard.Count > 0)
            {
                string words = Join(unheard.Select(i => i.Word).Distinct());
                string why = unheard.All(i => i.Def.Delicate && Chest.Clanks(Seller.Shakes)) ? $"The clank drowned out the {words} for {name}"
                    : unheard.All(i => i.StirsAt > Seller.Shakes) ? $"{name} didn't shake hard enough to stir the {words}"
                    : $"{name} shook {Categories.Adverb(Seller.Shakes)} and missed the {words}";
                lines.Add($"{why}: {Join(unheard.Select(i => i.Name))}, unpriced.");
            }
            if (!Seller.Counts)
                foreach (var group in noticed.Where(i => !i.Fake).GroupBy(i => i.Category).Where(g => g.Count() > 1))
                    lines.Add($"{name} heard the {group.First().Word} but never counted: {Sellers.Number(group.Count())} for the price of one.");
        }

        if (Seller.ClaimsHidden)
            lines.Add(Chest.Hidden != null ? "The false bottom was real." : $"There was no false bottom: {name} charged for one anyway.");
        else if (Chest.Hidden != null)
            lines.Add($"A false bottom nobody knew about: {Chest.Hidden.Name}.");
        var magic = Chest.Everything.Where(i => i.Magic).ToList();
        if (magic.Count > 0 && !Seller.SensesMagic)
            lines.Add($"Nobody can hear enchantment: the {Join(magic.Select(i => i.Name))} went for the price of plain goods.");
        if (lines.Count == 0)
            lines.Add(Trade == Trade.Fixed
                ? $"{name} heard everything, and always asks a bit more than it's worth."
                : $"{name} heard everything. The deal was in the price you agreed.");
        return string.Join(" ", lines);
    }

    static string Join(IEnumerable<string> parts) => Sellers.Join(parts.ToList());
}

/// <summary>
/// How a deal sits in your stomach. After a pass you never see inside, but your gut has an opinion, from what the
/// chest really held against what they wanted for it; after an opening, against what you paid.
/// </summary>
public static class Gut
{
    // From "you were right to walk away" to "you walked away from a fortune": what it held against their price.
    static readonly (float Below, string[] Lines)[] AfterPass =
    {
        (0.35f, new[] { "You have a feeling you dodged a cart.", "Somewhere, your purse sighs with relief.", "You have a feeling you made the right call." }),
        (0.8f, new[] { "Nothing to lose sleep over.", "Your gut shrugs. Fair enough.", "It felt overpriced. It probably was." }),
        (1.25f, new[] { "Could have gone either way.", "Your gut says nothing. It's sulking a little.", "Meh. A wash, probably." }),
        (2f, new[] { "Maybe that wasn't the best idea.", "A small voice whispers: you should've bought that.", "Your gut grumbles." }),
        (4f, new[] { "You have a nasty feeling a bargain just walked out the door.", "Your gut is writing you a strongly worded letter.",
                     "Somewhere, a better shopkeeper is laughing." }),
        (float.MaxValue, new[] { "Even the heavens pity you.", "Uncle Bram, somewhere, is weeping softly.", "Your gut has stopped speaking to you." }),
    };

    // From "fleeced" to "a steal": what it held against what you paid.
    static readonly (float Below, string[] Lines)[] AfterBuy =
    {
        (0.3f, new[] { "Even the heavens pity you.", "You feel like you just paid for the privilege of being robbed." }),
        (0.7f, new[] { "You feel like you just got fleeced.", "Your purse feels lighter. Your heart, heavier." }),
        (1f, new[] { "Maybe that wasn't the best idea.", "Close, but the house won. You are the house." }),
        (1.5f, new[] { "Nothing to lose sleep over.", "A fair day's trade." }),
        (3f, new[] { "You have a feeling you made the right call.", "Your gut does a little dance." }),
        (float.MaxValue, new[] { "Highway robbery. The legal kind.", "The heavens smile on you. Don't let it go to your head." }),
    };

    public static string Pass(int held, int price, Random rng) => Pick(AfterPass, held / (float)Math.Max(1, price), rng);

    public static string Bought(int held, int paid, Random rng) => Pick(AfterBuy, held / (float)Math.Max(1, paid), rng);

    /// <summary>0 for the best feeling after a pass, up to 5 for the worst.</summary>
    public static int PassMood(int held, int price) => Array.FindIndex(AfterPass, b => held / (float)Math.Max(1, price) < b.Below);

    /// <summary>
    /// How good a call passing was, from 0 (a fortune walked out the door: it held four times their price or more) to 1
    /// (you dodged a cart: it held a third of it or less), with an even trade at one half.
    /// </summary>
    public static float PassScore(int held, int price) => 1 - Score(held / (float)Math.Max(1, price), 0.35f, 4f);

    /// <summary>How good a buy it was, from 0 (fleeced: it held under a third of what you paid) to 1 (a steal: three times).</summary>
    public static float BuyScore(int held, int paid) => Score(held / (float)Math.Max(1, paid), 0.3f, 3f);

    /// <summary>Where a ratio sits between its worst and best on a log scale: 0.5 at even.</summary>
    static float Score(float ratio, float low, float high)
    {
        float r = MathF.Log(Math.Max(ratio, 0.001f));
        return 0.5f + 0.5f * (r >= 0 ? Math.Min(1, r / MathF.Log(high)) : -Math.Min(1, r / MathF.Log(low)));
    }

    static string Pick((float Below, string[] Lines)[] bands, float ratio, Random rng) =>
        rng.PickOne(bands.First(b => ratio < b.Below).Lines);
}
