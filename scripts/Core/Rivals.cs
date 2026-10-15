using System;

namespace LootFall.Core;

public enum RivalStyle { Cautious, Reckless, Savvy }

public sealed record RivalDef(
    string Name,
    string Tagline,
    RivalStyle Style,
    string ColorHex,
    string[] RaiseLines,
    string[] FoldLines,
    string[] WinLines);

public sealed class Rival
{
    public RivalDef Def { get; }
    public int MaxBid { get; }

    public Rival(RivalDef def, int maxBid)
    {
        Def = def;
        MaxBid = maxBid;
    }

    public string Say(string[] lines, Random rng, int price) =>
        rng.PickOne(lines).Replace("{price}", $"{price}g");
}

public static class Rivals
{
    public static readonly RivalDef[] All =
    {
        new("Old Marta", "Careful money is kept money.", RivalStyle.Cautious, "#8fb3a4",
            new[] { "Hmph. {price}.", "I'll go {price}. Not a copper more... probably.", "{price}. My knees ache, but my purse doesn't." },
            new[] { "Too rich for an old woman.", "Take it, dear. I've seen that lock before.", "Not worth my pension." },
            new[] { "Mine, then. Patience pays." }),
        new("Baron Vex", "Never lost an auction he could afford. Or couldn't.", RivalStyle.Reckless, "#d9766b",
            new[] { "{price}! Ha!", "A Vex never backs down. {price}!", "{price}, and I'll have it gilded." },
            new[] { "Bah! Even I have limits. Apparently.", "Keep it. Probably full of socks.", "Fine! My accountant thanks you." },
            new[] { "Sold to the Baron! As is tradition." }),
        new("Pip the Sharp", "Somehow always knows what's inside.", RivalStyle.Savvy, "#c9a0ff",
            new[] { "{price}.", "Mm. {price}.", "I'll say {price}. Interesting chest." },
            new[] { "Not at that price.", "You're welcome to it.", "Pass. I know what's in there." },
            new[] { "Thank you kindly." }),
    };

    /// <summary>
    /// Each rival reads a lot differently: Marta judges by the lock, Vex goes on gut feel (the public estimate),
    /// and Pip somehow knows the real value. Their bids are clues in themselves, if you know who is bidding.
    /// </summary>
    public static Rival Create(Random rng, Chest chest, Appraiser appraiser)
    {
        var def = rng.PickOne(All);
        float max = def.Style switch
        {
            RivalStyle.Cautious => appraiser.Estimate(chest, new Clues(false, false, true)).Median * rng.Between(0.7f, 1.0f),
            RivalStyle.Reckless => appraiser.Prior.Median * rng.Between(1.0f, 2.2f),
            _ => chest.Value * rng.Between(0.8f, 1.0f),
        };
        return new Rival(def, Auction.RoundTo5(max));
    }
}

public static class Auction
{
    public static int RoundTo5(float value) => Math.Max(5, (int)MathF.Round(value / 5f) * 5);

    public static int OpeningPrice(Estimate publicEstimate) => Math.Max(10, RoundTo5(publicEstimate.Median * 0.35f));

    public static int Increment(int price) => Math.Max(5, RoundTo5(price * 0.1f));
}
