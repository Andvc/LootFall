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
        string name = Seller.Short;
        var lines = new List<string>();
        if (Chest.Items.Count == 0 && Chest.Hidden == null)
            return "Empty! Nothing rattled because there was nothing there.";

        var fakes = Chest.Items.Where(i => i.Fake).ToList();
        if (fakes.Count > 0)
            lines.Add($"{Join(fakes.Select(i => i.Name))}: gilded iron. A magnet would have silenced {(fakes.Count == 1 ? "it" : "them")}.");
        else if (Seller.Judge == Judgment.Crook)
            lines.Add($"All genuine this time, and {name} sold it for half.");

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

        // What a listener couldn't hear, and why.
        if (Seller.Judge is Judgment.Ear or Judgment.Counter or Judgment.Miner)
        {
            var noticed = Seller.Notices(Chest).ToHashSet();
            var unheard = Chest.Items.Where(i => !noticed.Contains(i) && !i.Fake).ToList();
            if (unheard.Count > 0)
            {
                string why = unheard.All(i => i.Def.Delicate && Chest.Clanks(Seller.Shakes)) ? $"The clank drowned out the {Join(unheard.Select(i => i.Word))} for {name}"
                    : unheard.All(i => i.Def.StirsAt > Seller.Shakes) ? $"{name} didn't shake hard enough to stir the {Join(unheard.Select(i => i.Word))}"
                    : $"{name} shook {Categories.Adverb(Seller.Shakes)} and missed the {Join(unheard.Select(i => i.Word))}";
                lines.Add($"{why}: {Join(unheard.Select(i => i.Name))}, unpriced.");
            }
        }

        if (Seller.ClaimsHidden)
            lines.Add(Chest.Hidden != null ? "The false bottom was real." : $"There was no false bottom: {name} charged for one anyway.");
        else if (Chest.Hidden != null)
            lines.Add($"A false bottom nobody knew about: {Chest.Hidden.Name}.");
        var magic = Chest.Everything.Where(i => i.Magic).ToList();
        if (magic.Count > 0 && !Seller.SensesMagic)
            lines.Add($"Nobody can hear magic: the {Join(magic.Select(i => i.Name))} went for the price of plain goods.");
        if (lines.Count == 0)
            lines.Add(Trade == Trade.Fixed
                ? $"{name} heard everything, and always asks a bit more than it's worth."
                : $"{name} heard everything. The deal was in the price you agreed.");
        return string.Join(" ", lines);
    }

    static string Join(IEnumerable<string> parts) => Sellers.Join(parts.ToList());
}
