using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>Builds a visit: what the seller brings, what they say about it, and how they price it.</summary>
public static class Market
{
    /// <summary>Fixed-price sellers ask this much over what they think it's worth.</summary>
    public const float MarkupMin = 1.4f, MarkupMax = 1.8f;

    /// <summary>Hagglers open this high, and will come down to their bottom.</summary>
    public const float HaggleAskMin = 1.7f, HaggleAskMax = 2.1f, BottomMin = 1.05f, BottomMax = 1.25f;

    /// <summary>An offer this far under the asking price is an insult.</summary>
    public const float Insult = 0.35f;

    /// <summary>A crook asks this much of what the real thing would be worth: too good to be true.</summary>
    public const float CrookMin = 0.5f, CrookMax = 0.7f;

    /// <summary>A dreamer's price, against what a chest of that colour usually holds.</summary>
    public const float DreamMin = 0.5f, DreamMax = 2.8f;

    /// <summary>A jeweller prices her jewels at what she paid: more than they're worth on average.</summary>
    public const float JewellerPaid = 1.3f;

    /// <summary>How often a talker claims a sound that isn't there.</summary>
    public const float PhantomChance = 0.6f;

    /// <summary>Angry sellers put the price up by this much.</summary>
    public const float AngerMarkup = 1.5f;

    /// <summary>Two or three of a kind turn up from this shop tier on.</summary>
    public const int PairsTier = 3;

    /// <summary>The most things in one chest, false bottom aside.</summary>
    public const int MaxItems = 6;

    /// <summary>How many things a seller of each tier brings, with weights.</summary>
    static readonly (int Count, float Weight)[][] Counts =
    {
        null,
        new[] { (1, 0.35f), (2, 0.55f), (3, 0.1f) },
        new[] { (2, 0.4f), (3, 0.6f) },
        new[] { (2, 0.5f), (3, 0.5f) },
        new[] { (3, 0.6f), (4, 0.4f) },
    };

    /// <param name="boosts">What the shop's exhibition and synergies do to the chests people bring.</param>
    public static Visit CreateVisit(Random rng, SellerDef seller, int shopTier, Boosts boosts = null)
    {
        boosts ??= Boosts.None;
        var colour = (Rarity)rng.PickWeighted(Enumerable.Range(0, 4).ToArray(), i => seller.Colours[i]);
        if (colour < Rarity.Epic && rng.NextFloat() < boosts.BetterChests) colour++;

        // For sellers who listen, whether this one holds something they can't hear is rolled first, so each one's
        // blind spot turns up about as often as their MissRate: often enough to pay for listening, not so often that
        // buying blind pays.
        var chest = Roll(rng, seller, shopTier, colour, boosts);
        if (seller.Listens)
        {
            bool missing = rng.NextFloat() < seller.MissRate;
            for (int attempt = 0; attempt < 40 && Misses(seller, chest) != missing; attempt++)
                chest = Roll(rng, seller, shopTier, colour, boosts);
        }
        return Price(rng, seller, chest, shopTier);
    }

    /// <summary>Whether the seller's own listening leaves something unpriced: unheard, or one of several they didn't count.</summary>
    static bool Misses(SellerDef seller, Chest chest)
    {
        var noticed = seller.Notices(chest).ToList();
        if (noticed.Count < chest.Items.Count) return true;
        return !seller.Counts && noticed.GroupBy(i => i.Category).Any(g => g.Count() > 1);
    }

    static Chest Roll(Random rng, SellerDef seller, int shopTier, Rarity colour, Boosts boosts)
    {
        var lockTier = shopTier >= 2 && rng.NextFloat() < seller.LockChance ? LockTiers.For(colour) : null;
        if (rng.NextFloat() < seller.EmptyChance) return new Chest(Array.Empty<Item>(), colour, null, lockTier);

        var kinds = Categories.UpTo(shopTier).Select(c => c.Category).ToList();
        int count = Math.Min(kinds.Count, rng.PickWeighted(Counts[seller.Tier], c => c.Weight).Count);
        var picked = new List<Category>();
        // A crook always has something to fake; a lady always has her jewels.
        var fakeable = kinds.Where(seller.Fakes.Contains).ToArray();
        if (seller.Judge == Judgment.Crook && fakeable.Length > 0) picked.Add(rng.PickWeighted(fakeable, c => seller.Kinds[(int)c]));
        if (seller.Always is Category always && kinds.Contains(always)) picked.Add(always);
        while (picked.Count < count)
            picked.Add(rng.PickWeighted(kinds.Except(picked).ToArray(), c => seller.Kinds[(int)c] * boosts.Weight(c)));

        // From the town shop on, a lot can hold two or three of a kind.
        var all = new List<Category>();
        foreach (var kind in picked)
        {
            int copies = 1;
            float pairs = seller.PairChance + boosts.Pairs;
            if (shopTier >= PairsTier && rng.NextFloat() < pairs)
                copies = rng.NextFloat() < pairs * 0.5f ? 3 : 2;
            all.AddRange(Enumerable.Repeat(kind, copies));
        }
        all = all.Take(MaxItems).ToList();

        float magicChance = shopTier >= 3 ? seller.MagicChance + boosts.Magic : 0f;
        var items = all.Select(c =>
        {
            bool fake = seller.Fakes.Contains(c) && rng.NextFloat() < seller.FakeChance;
            bool magic = !fake && rng.NextFloat() < magicChance;
            var quality = Categories.RollQuality(rng, colour);
            if (quality < Rarity.Legendary && rng.NextFloat() < seller.QualityBoost) quality++;
            return new Item(c, quality, magic, fake);
        }).ToList();

        // People hide money, jewels and papers under a false bottom.
        Item hidden = null;
        if (shopTier >= 2 && rng.NextFloat() < seller.HiddenChance + boosts.Hidden)
        {
            var hideable = HiddenKinds.Where(k => Categories.Get(k.Kind).Tier <= shopTier).ToArray();
            hidden = new Item(rng.PickWeighted(hideable, k => k.Weight).Kind, Categories.RollQuality(rng, colour));
        }
        return new Chest(items, colour, hidden, lockTier);
    }

    static readonly (Category Kind, float Weight)[] HiddenKinds =
        { (Category.Coins, 3f), (Category.Jewels, 3f), (Category.Paper, 2f) };

    /// <summary>What a false bottom might hold at this shop, and how likely each kind is.</summary>
    public static IEnumerable<(Category Kind, float Chance)> HiddenOdds(int shopTier)
    {
        var kinds = HiddenKinds.Where(k => Categories.Get(k.Kind).Tier <= shopTier).ToArray();
        float total = kinds.Sum(k => k.Weight);
        return kinds.Select(k => (k.Kind, k.Weight / total));
    }

    /// <summary>
    /// What a false bottom is worth in a chest of this colour, at this shop: as good as the colour says (the slip
    /// shows this), or on average.
    /// </summary>
    public static int HiddenValue(Rarity chest, int shopTier, bool expected = false)
    {
        var kinds = HiddenKinds.Where(k => Categories.Get(k.Kind).Tier <= shopTier).ToArray();
        return (int)MathF.Round(kinds.Sum(k => k.Weight * (expected ? Categories.Expected(k.Kind, chest) : Categories.Nominal(k.Kind, chest))) /
                                kinds.Sum(k => k.Weight));
    }

    /// <summary>How many things a seller of this tier usually brings to a shop of this tier, two-of-a-kinds included.</summary>
    static float TypicalCount(SellerDef seller, int shopTier)
    {
        float kinds = Counts[seller.Tier].Sum(c => c.Count * c.Weight);
        float p = shopTier >= PairsTier ? seller.PairChance : 0f;
        return kinds * (1 + p + p * p * 0.5f);
    }

    /// <summary>What one thing this seller brings is usually worth in a chest of this colour.</summary>
    static float TypicalItem(SellerDef seller, Rarity colour, int shopTier)
    {
        var kinds = Categories.UpTo(shopTier).Select(c => c.Category).ToArray();
        return kinds.Sum(k => seller.Kinds[(int)k] * Categories.Expected(k, colour)) / kinds.Sum(k => seller.Kinds[(int)k]);
    }

    /// <summary>What a chest of this colour from this seller usually holds: what someone who goes by colour asks for.</summary>
    public static float TypicalChest(SellerDef seller, Rarity colour, int shopTier) =>
        TypicalCount(seller, shopTier) * TypicalItem(seller, colour, shopTier);

    /// <summary>The weight someone who goes by weight expects of a chest from them.</summary>
    static float TypicalHeft(SellerDef seller, int shopTier)
    {
        var kinds = Categories.UpTo(shopTier).Select(c => c.Category).ToArray();
        return TypicalCount(seller, shopTier) * kinds.Sum(k => seller.Kinds[(int)k] * Categories.Heft(k)) / kinds.Sum(k => seller.Kinds[(int)k]);
    }

    /// <summary>
    /// What the seller thinks the chest is worth, their way. Nobody but a fortune-teller or an apprentice feels magic,
    /// and nobody knows what's under a false bottom (though a smuggler always swears there's something).
    /// </summary>
    static float Estimate(Random rng, SellerDef seller, Chest chest, IReadOnlyList<Category> claim, int shopTier)
    {
        var colour = chest.Quality;
        float plain(Category c) => Categories.Expected(c, colour);
        float estimate = seller.Judge switch
        {
            Judgment.Counter => claim.Count * TypicalItem(seller, colour, shopTier),
            Judgment.Weight => chest.Items.Sum(i => Categories.Heft(i.Category)) * TypicalItem(seller, colour, shopTier) /
                               (TypicalHeft(seller, shopTier) / TypicalCount(seller, shopTier)),
            Judgment.Colour => TypicalChest(seller, colour, shopTier) + (seller.SensesMagic ? MagicExtra(chest) : 0),
            Judgment.Dreamer => TypicalChest(seller, colour, shopTier) * rng.Between(DreamMin, DreamMax),
            Judgment.Miner => claim.Sum(c => c switch
            {
                Category.Metal => plain(c),
                Category.Jewels => Categories.Value(c, Rarity.Legendary, false),
                _ => plain(c) / 2,
            }),
            Judgment.Jeweller => claim.Sum(c => plain(c) * JewellerPaid),
            Judgment.MagicSense => MagicExtra(chest) + chest.Items.Where(i => i.Magic).Sum(i => plain(i.Category)),
            Judgment.Clueless => 0,
            _ => claim.Sum(plain),
        };
        if (seller.ClaimsHidden) estimate += HiddenValue(colour, shopTier, expected: true);
        return estimate;
    }

    /// <summary>What magic adds to the chest, for those who can feel it.</summary>
    static float MagicExtra(Chest chest) =>
        chest.Items.Where(i => i.Magic).Sum(i => (Categories.MagicMultiplier - 1) * Categories.Expected(i.Category, chest.Quality));

    /// <summary>The seller judges the chest their way, says what they think is in it, and prices it.</summary>
    /// <param name="ask">A scripted asking price (the first day, lessons).</param>
    /// <param name="trade">A scripted way of trading (the first day).</param>
    /// <param name="pitch">A scripted line, with {price}.</param>
    /// <param name="claim">A scripted claim (a lesson's made-up sound).</param>
    /// <param name="floor">A scripted least they'll take (a lesson's).</param>
    public static Visit Price(Random rng, SellerDef seller, Chest chest, int shopTier, int? ask = null, Trade? trade = null, string pitch = null,
        IEnumerable<Category> claim = null, int? floor = null)
    {
        var said = claim?.ToList() ?? Claim(rng, seller, chest, shopTier);
        said.Sort();
        var visit = Price(rng, seller, chest, shopTier, said, ask, trade, pitch);
        if (floor is int least) visit.SetFloor(least);
        return visit;
    }

    /// <summary>
    /// What they say is in it: what they noticed (once per kind, unless they count), and a talker sometimes adds a
    /// sound that wasn't there.
    /// </summary>
    static List<Category> Claim(Random rng, SellerDef seller, Chest chest, int shopTier)
    {
        var noticed = seller.Notices(chest).Select(i => i.Category);
        var claim = (seller.Counts ? noticed : noticed.Distinct()).ToList();
        if (seller.Talker && rng.NextFloat() < PhantomChance)
        {
            var absent = Categories.UpTo(shopTier).Select(c => c.Category).Where(c => !chest.Has(c) && !claim.Contains(c)).ToArray();
            if (absent.Length > 0) claim.Add(rng.PickWeighted(absent, c => seller.Kinds[(int)c]));
        }
        return claim;
    }

    static Visit Price(Random rng, SellerDef seller, Chest chest, int shopTier, List<Category> claim, int? ask, Trade? trade, string pitch)
    {
        float estimate = Estimate(rng, seller, chest, claim, shopTier);
        var format = trade ?? seller.Trade;
        float price, floor;
        switch (format)
        {
            case Trade.Haggle when estimate > 0:
                price = estimate * rng.Between(HaggleAskMin, HaggleAskMax);
                floor = estimate * rng.Between(seller.Bottom.Min, seller.Bottom.Max);
                break;
            case Trade.Haggle:
                price = seller.Tier * rng.Between(4f, 8f);
                floor = price * 0.5f;
                break;
            case Trade.Offer:
                price = 0;
                // An apprentice with nothing magic to sell takes a modest sum; a boy takes pocket money.
                floor = estimate > 0 ? estimate * rng.Between(0.9f, 1.15f)
                    : seller.Judge == Judgment.MagicSense ? seller.Tier * rng.Between(5f, 10f)
                    : seller.Tier * rng.Between(2f, 6f);
                break;
            default:
                price = seller.Judge == Judgment.Crook ? estimate * rng.Between(CrookMin, CrookMax)
                    : estimate == 0 ? seller.Tier * rng.Between(2f, 4f)
                    : estimate * rng.Between(seller.Markup.Min, seller.Markup.Max);
                floor = price;
                break;
        }
        int asking = ask ?? Money.Round(price);
        string line = Fill(pitch ?? rng.PickOne(seller.Pitches), seller, chest, claim, shopTier).Replace("{price}", $"{asking}g");
        return new Visit(seller, chest, claim, format, format == Trade.Offer ? 0 : asking, Money.Round(floor), line);
    }

    static string Fill(string line, SellerDef seller, Chest chest, IReadOnlyList<Category> claim, int shopTier)
    {
        string count = claim.Count switch { 0 => "Not a rattle", 1 => "One rattle", 2 => "Two rattles", 3 => "Three rattles", _ => $"{claim.Count} rattles" };
        float heft = chest.Items.Sum(i => Categories.Heft(i.Category)) / Math.Max(0.1f, TypicalHeft(seller, shopTier));
        string heftWord = heft > 1.3f ? "Heavy as a mule" : heft < 0.7f ? "Light, mind" : "A fair weight";
        return line.Replace("{heard}", Sellers.Heard(claim)).Replace("{count}", count).Replace("{heft}", heftWord)
            .Replace("{colour}", Categories.ChestSkin(chest.Quality))
            .Replace("{magic}", chest.Items.Any(i => i.Magic) ? "there's magic in it" : "there's no magic in it");
    }
}

/// <summary>What the shop's exhibition, synergies, curios and today's event do to the chests people bring in.</summary>
/// <param name="BetterChests">The chance a chest is one colour better.</param>
/// <param name="Hidden">Added to the chance of a false bottom.</param>
/// <param name="Magic">Added to the chance each thing is magic.</param>
/// <param name="Pairs">Added to the chance (from the town shop on) of two or three of a kind.</param>
/// <param name="MoneyKinds">How much more often coins and jewelry turn up (a jewelry tycoon's name): 0.2 is a fifth more.</param>
public sealed record Boosts(float BetterChests = 0, float Hidden = 0, float Magic = 0, float Pairs = 0, float MoneyKinds = 0)
{
    public static readonly Boosts None = new();

    /// <summary>How likely a seller is to have put a thing of this kind in, against what they usually bring.</summary>
    public float Weight(Category category) => category is Category.Coins or Category.Jewels ? 1 + MoneyKinds : 1;
}
