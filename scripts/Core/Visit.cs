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

    /// <summary>Where the chest really came from (null for the vault and the Stranger).</summary>
    public OriginDef Origin { get; init; }

    /// <summary>What the seller states besides what they heard (<see cref="Market.Clue"/>), or null.</summary>
    public string Clue { get; init; }

    /// <summary>What an heirloom-hunter pays for the best thing of theirs inside (0 if they don't).</summary>
    public int Fee { get; init; }

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
        Ask = _firstAsk = ask;
        Floor = Math.Max(1, floor);
        Pitch = pitch;
    }

    readonly int _firstAsk;

    /// <summary>They walked out once and came back (the Shepherd's Pipe): they won't come back twice.</summary>
    public bool Returned { get; private init; }

    /// <summary>The same seller with the same chest, back at the end of the day, calmer, at their first price.</summary>
    public Visit Again() => new(Seller, Chest, Claim, Trade, _firstAsk, Floor,
        _firstAsk > 0 ? $"I came back. {_firstAsk}g, and no hard feelings." : "I came back. Make me an offer, then.")
    {
        Origin = Origin, Fee = Fee, HearsOut = HearsOut, Returned = true, Clue = Clue,
    };

    public bool Deciding => Outcome == VisitOutcome.Deciding;

    /// <summary>A haggler who hears you out (a merchant's signet): an offer too low gets a second try, not a slammed door.</summary>
    public bool HearsOut { get; set; }

    bool _retry;

    /// <summary>Whether you can make an offer now.</summary>
    public bool CanOffer => Deciding && (Trade == Trade.Haggle ? OffersMade == 0 || _retry : Trade == Trade.Offer && Ask == 0);

    /// <summary>Whether there's a price on the table you can just pay.</summary>
    public bool CanBuy => Deciding && Ask > 0;

    public Response Offer(int amount, Random rng)
    {
        if (!CanOffer) throw new InvalidOperationException("No offer to make");
        OffersMade++;
        _retry = false;
        amount = Math.Max(1, amount);
        if (amount >= Floor) return new Response(ResponseKind.Accept, amount, Sell(amount, rng));
        if (Trade == Trade.Offer)
        {
            if (OffersMade == 1) return new Response(ResponseKind.TryAgain, 0, rng.PickOne(Seller.Refusals));
            return Leave(rng);
        }
        if (amount < Ask * Market.Insult)
        {
            if (!HearsOut || OffersMade > 1) return Leave(rng);
            _retry = true;
            return new Response(ResponseKind.TryAgain, 0, rng.PickOne(Seller.Refusals));
        }
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

    readonly HashSet<Shake> _shook = new();

    /// <summary>
    /// You've tried another strength. A seller who charges for shaking puts the price up for every strength after the
    /// first. Returns what they say about it (null if the price didn't change).
    /// </summary>
    public string Shook(Shake level, Random rng)
    {
        if (Seller.ShakeFee <= 0 || !Deciding || level == Shake.Still || !_shook.Add(level) || _shook.Count < 2) return null;
        Raise(1 + Seller.ShakeFee);
        return rng.PickOne(Seller.PriceUp).Replace("{price}", $"{Price}g");
    }

    /// <summary>The King's game: the price is what your slip says it holds.</summary>
    public void SetSlipPrice(int worth)
    {
        if (!Seller.SlipPrice || !Deciding) return;
        Ask = Math.Max(1, worth);
        Floor = Ask;
    }

    /// <summary>Puts the price up (the Maw's gaze, a goblin's fee).</summary>
    public void Raise(float factor)
    {
        if (!Deciding) return;
        if (Ask > 0) Ask = Money.Round(Ask * factor);
        Floor = Money.Round(Floor * factor);
    }

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
    /// misjudged or faked, what nobody could have heard, and whether their story about where it came from was true.
    /// </summary>
    public string Explain()
    {
        if (Seller == Story.VaultSeller) return "King Aldric's own chest, sealed since the Locking. The Gate's gone quiet. Every bit of it is yours.";
        string name = Seller.Short;
        var lines = new List<string>();
        if (Chest.Items.All(i => i.IsSong) && Chest.Hidden == null)
        {
            lines.Add(Chest.Song is Item only
                ? $"Nothing rattled because there was nothing there, but {only.Name}. Only a Keywright hears a Heartsong sing."
                : "Empty! Nothing rattled because there was nothing there.");
            return string.Join(" ", lines);
        }
        var gilded = Chest.Items.Where(i => i.Fake && i.Def.FakeIron).ToList();
        var sand = Chest.Items.Where(i => i.Fake && !i.Def.FakeIron).ToList();
        if (gilded.Count > 0)
            lines.Add($"{Join(gilded.Select(i => i.Name))}: gilded iron from the Maw's hoard. A lodestone would have silenced {(gilded.Count == 1 ? "it" : "them")}.");
        else if (Seller.Judge == Judgment.Crook)
            lines.Add($"All genuine this time, and {name} sold it cheap.");
        if (sand.Count > 0)
            lines.Add($"The {(sand.Count == 1 ? "rustle was" : "rustles were")} sand: it pours at a gentle shake, where parchment never rustles.");
        switch (Seller.Judge)
        {
            case Judgment.Counter:
                lines.Add($"{Capital(name)} charged the same for every rattle she heard: {Claim.Count} of them, whatever they were.");
                break;
            case Judgment.Weight:
            {
                var heavy = Chest.Items.Where(i => Categories.Heft(i.Category) >= 2).ToList();
                var light = Chest.Items.Where(i => Categories.Heft(i.Category) <= 0.5f && !i.IsSong).ToList();
                if (heavy.Count > 0) lines.Add($"{Capital(name)} went by weight: the {Join(heavy.Select(i => i.Word))} made it heavy.");
                if (light.Count > 0) lines.Add($"The {Join(light.Select(i => i.Word))} weighed next to nothing to {name}.");
                break;
            }
            case Judgment.Colour when Seller.Gaze > 0:
                lines.Add($"One coin for a {Categories.ChestSkin(Chest.Quality)} chest. The price comes later, from everyone after.");
                break;
            case Judgment.Colour:
                lines.Add($"{Capital(name)} priced it as an ordinary {Categories.ChestSkin(Chest.Quality)} chest" +
                          (Seller.SensesMagic ? ", and felt for magic." : ", without looking inside."));
                break;
            case Judgment.Dreamer:
                lines.Add($"{Capital(name)}'s price came from a dream. Only the chest could tell you what it was worth.");
                break;
            case Judgment.MagicSense:
                lines.Add(Chest.Items.Any(i => i.Magic)
                    ? $"{Capital(name)} felt the magic and wanted its price; the rest she couldn't tell."
                    : $"{Capital(name)} felt no magic, so she took what you offered.");
                break;
            case Judgment.Jeweller:
                lines.Add($"{Capital(name)} priced her gems at what she paid for them; everything else came free.");
                break;
            case Judgment.Miner:
            {
                var gems = Chest.Get(Category.Gems);
                if (gems != null && Claim.Contains(Category.Gems)) lines.Add($"{Capital(name)} priced the {gems.Name} like a diamond fit for a crown.");
                var junk = Chest.Items.Where(i => Claim.Contains(i.Category) && i.Category is not (Category.Arms or Category.Gems)).ToList();
                if (junk.Count > 0) lines.Add($"He priced the {Join(junk.Select(i => i.Word))} at half.");
                break;
            }
            case Judgment.Clueless when Seller.SlipPrice:
                lines.Add($"{Capital(name)} let your slip set the price.");
                break;
            case Judgment.Clueless:
                lines.Add($"{Capital(name)} had no idea what was inside.");
                break;
        }
        // What a listener couldn't hear, and why; and what they heard but never counted.
        if (Seller.Judge is Judgment.Ear or Judgment.Counter or Judgment.Miner)
        {
            var noticed = Seller.Notices(Chest).ToHashSet();
            var unheard = Chest.Items.Where(i => !noticed.Contains(i) && !i.Fake && !i.IsSong).ToList();
            if (unheard.Count > 0)
            {
                string words = Join(unheard.Select(i => i.Word).Distinct());
                string why = unheard.All(i => i.Def.Delicate && Chest.Clanks(Seller.Shakes)) ? $"The clank drowned out the {words} for {name}"
                    : Chest.Packed && unheard.All(i => Chest.Needs(i) > (int)Shake.Hard) ? $"Nothing stirs the {words} through straw"
                    : Chest.Packed && unheard.All(i => Chest.Needs(i) > (int)Seller.Shakes) ? $"Through the straw, {name}'s shake was too gentle to stir the {words}"
                    : unheard.All(i => Chest.Needs(i) > (int)Seller.Shakes) ? $"{Capital(name)} didn't shake hard enough to stir the {words}"
                    : $"{Capital(name)} shook {Categories.Adverb(Seller.Shakes)} and missed the {words}";
                lines.Add($"{why}: {Join(unheard.Select(i => i.Name))}, unpriced.");
            }
            if (!Seller.Counts)
                foreach (var group in noticed.Where(i => !i.Fake).GroupBy(i => i.Category).Where(g => g.Count() > 1))
                    lines.Add($"{Capital(name)} heard the {group.First().Word} but never counted: {Sellers.Number(group.Count())} for the price of one.");
        }
        if (Chest.Hidden != null)
            lines.Add(Seller.States == Core.Clue.Articles ? $"The ticket counted the false bottom, but {name} never reads tickets: {Chest.Hidden.Name}."
                : $"A false bottom nobody knew about: {Chest.Hidden.Name}.");
        if (Seller.States == Core.Clue.Dream && Market.Dearest(Chest) is Item dearest)
            lines.Add($"{Capital(Categories.Sign(dearest.Category))} in her dream: the {dearest.Name}, the dearest thing inside.");
        if (Chest.Song is Item song)
            lines.Add($"Only a Keywright hears a Heartsong sing: nobody knew {song.Name} was in it.");
        var magic = Chest.Everything.Where(i => i.Magic).ToList();
        if (magic.Count > 0 && !Seller.SensesMagic)
            lines.Add($"Nobody can hear enchantment: the {Join(magic.Select(i => i.Name))} went for the price of plain goods.");
        if (lines.Count == 0)
            lines.Add(Trade == Trade.Fixed
                ? $"{Capital(name)} heard everything, and always asks a bit more than it's worth."
                : $"{Capital(name)} heard everything. The deal was in the price you agreed.");
        return string.Join(" ", lines);
    }

    static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];

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
