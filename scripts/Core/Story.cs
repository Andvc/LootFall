using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>One line in a scene: who says it (a portrait key) and what they say.</summary>
public sealed record Line(string Speaker, string Text);

/// <summary>
/// What's going on behind the counter. Twenty years ago Bram borrowed from the Grey Guild to buy a quarter of a map to
/// King Aldric's lost vault. The debt comes with the stall. The Guild doesn't want the gold so much as the map; the
/// Hooded Woman has the other pieces, and wants things only a good appraiser could find. Scenes play at the start of
/// some weeks, when the Guild comes, and at the end.
/// </summary>
public static class Story
{
    public const string Bram = "bram", Vane = "vane", Hood = "hood", Nim = "nim", StrangerKey = "stranger";

    public const string HoodName = "The Hooded Woman";

    /// <summary>The map comes in four pieces: Bram's, and three the Hooded Woman trades for.</summary>
    public const int Pieces = 4;

    /// <summary>The week the Guild offers to take the map off your hands for half that week's due.</summary>
    public const int OfferWeek = 6;

    /// <summary>Who brings the vault to the counter (nobody's selling: it's yours).</summary>
    public static readonly SellerDef VaultSeller = new()
    {
        Key = Hood, Name = HoodName, Tier = 4, Judge = Judgment.Clueless,
        Kinds = new[] { 1f, 1, 1, 1, 1, 1 }, Colours = new[] { 0f, 0, 0, 1 },
        Habit = "Bram's old partner. She's been looking for King Aldric's vault for twenty years.",
        Pitches = new[] { "King Aldric's vault." },
    };

    public static string Name(string speaker) => speaker switch
    {
        Bram => Hints.Mentor,
        Vane => Guild.Collector,
        Hood => HoodName,
        Nim => Peddler.Name,
        StrangerKey => Events.Stranger.Name,
        _ => speaker,
    };

    /// <summary>The first evening: the Guild introduces itself.</summary>
    public static List<Line> Intro() => new()
    {
        new(Vane, $"Evening. {Guild.Collector}, of the Grey Guild. Your uncle owes us {Guild.Debt}g, and the stall is his surety."),
        new(Bram, "That was twenty years ago! I've paid most of it..."),
        new(Vane, $"Interest, Bram. The Guild will take it in {Guild.Weeks} weekly payments, starting small. " +
                  $"{Guild.Due(1)}g at the end of the week. Each week, more."),
        new(Vane, "Miss one, and the shop is ours. Good evening."),
        new(Bram, $"Every {Guild.DaysPerWeek} days. Don't worry: a good eye pays any debt. We'll talk in the morning."),
    };

    /// <summary>The morning a week begins (from the second): who comes by, and what they want.</summary>
    public static List<Line> WeekStart(GameState state)
    {
        int week = state.Week;
        var lines = new List<Line>();
        switch (week)
        {
            case 2:
                lines.Add(new(Bram, "About the Guild. Twenty years ago I borrowed from them to buy this: a quarter of a map."));
                lines.Add(new(Bram, "King Aldric's vault. Everything the old king hid from the war, never found. The other three pieces " +
                                    "went missing."));
                lines.Add(new(Bram, "It's yours now. Keep it safe. And keep the Guild paid: every week they'll want more, so take " +
                                    "every commission you can."));
                break;
            case 3:
                lines.Add(new(Hood, "You're Bram's kin. You have his ear for a chest, they say."));
                lines.Add(new(Hood, "I have something your uncle has wanted for twenty years. Find me what I ask for, and it's yours."));
                break;
            case 4:
                lines.Add(state.MapPieces >= 2
                    ? new(Bram, "That's... the second piece. Where did she get it? Never mind. Never mind!")
                    : new(Bram, "That woman in the hood. I know that voice. Do what she asks."));
                lines.Add(new(Bram, "And word is the Guild's been buying up every old record in the county. Funny, for moneylenders."));
                break;
            case 5:
                lines.Add(new(Hood, "The Guild doesn't want your gold. It wants the vault. Your uncle's debt was a leash."));
                lines.Add(new(Hood, "Keep paying them. And keep finding me things."));
                break;
            case 6:
                lines.Add(new(Bram, "Vane's been asking about the map. Whatever he offers you, think hard."));
                break;
            case 7:
                if (state.MapGiven)
                {
                    lines.Add(new(Hood, "You gave them the map. Then the Guild will find the vault, and nobody will stop them."));
                    lines.Add(new(Hood, "Pay your debt, then. It's all you have left."));
                }
                else
                {
                    lines.Add(new(Hood, "My name is Mara. Your uncle and I found the first piece together, a long time ago."));
                    lines.Add(new(Hood, "The Guild made him choose: me, or the stall. He kept the stall. I kept looking."));
                }
                break;
            case 8:
                lines.Add(new(Bram, "The last week. Pay this, and the stall is ours. Truly ours."));
                if (state.MapPieces >= Pieces)
                    lines.Add(new(Bram, "And with all four pieces... Mara says she knows where the vault is."));
                break;
            case Guild.Weeks + 1:
                lines.Add(new(Vane, "The debt is paid. But the Guild has decided you owe us for our patience. Interest, you understand."));
                lines.Add(new(Bram, "They'll keep coming, and every week they'll want half as much again. Let's see how long we can make them wait."));
                break;
        }
        // Whenever she wants something for her next piece, she says what.
        var ask = state.Commissions.FirstOrDefault(c => c.Story);
        if (ask != null)
            lines.Add(new(Hood, $"{HoodAsks(state.HoodPieces + 1)} Bring me {ask.Phrase} before the Guild comes again, and the next piece is yours."));
        return lines;
    }

    /// <summary>What the Hooded Woman says when she asks for the next piece's price.</summary>
    public static string HoodAsks(int piece) => piece switch
    {
        1 => "Old papers. The king's clerks wrote everything down.",
        2 => "A ring. Aldric sealed the vault with one.",
        _ => "Something with magic in it. The vault's lock knows magic.",
    };

    public static string HoodThanks(int piece) => piece switch
    {
        1 => "Good. Here: the second piece. Don't show the Guild.",
        2 => "The third piece. One more, and the vault is ours.",
        _ => "The last piece. Bram will know what to do. So will I.",
    };

    /// <summary>The Guild's offer, on the night of <see cref="OfferWeek"/>.</summary>
    public static List<Line> Offer(int pieces) => new()
    {
        new(Vane, $"Before you pay. The Guild knows about the map. {Count(pieces)}, I hear."),
        new(Vane, "Give them to me, and the Guild will forget half of tonight's due. Generous, no?"),
    };

    static string Count(int pieces)
    {
        string n = Sellers.Number(pieces);
        return char.ToUpperInvariant(n[0]) + n[1..] + (pieces == 1 ? " piece" : " pieces");
    }

    public const string OfferTaken = "A sensible choice. The Guild remembers its friends.";
    public const string OfferRefused = "Pity. The Guild remembers that too.";

    /// <summary>The last due is paid.</summary>
    public static List<Line> Cleared(GameState state)
    {
        var lines = new List<Line>
        {
            new(Vane, Guild.Paid(Guild.Weeks)),
            new(Bram, "Free and clear! Twenty years, and the stall is ours."),
        };
        if (state.MapPieces >= Pieces)
        {
            lines.Add(new(Hood, "Bram. It's been a long time."));
            lines.Add(new(Bram, "Mara. You found them. All of them."));
            lines.Add(new(Hood, "We found them. Your kin did the finding. Come: the vault's under the old mint, and the Guild is " +
                                "a day behind us."));
        }
        else if (state.MapGiven)
            lines.Add(new(Bram, "And the Guild has the map. Somewhere under the old mint, they're digging. Let them dig."));
        else
            lines.Add(new(Bram, "The vault can wait. The map's not going anywhere, and neither are we."));
        return lines;
    }

    /// <summary>After the vault's been opened.</summary>
    public static List<Line> VaultEpilogue() => new()
    {
        new(Bram, "King Aldric's own hoard. On our counter."),
        new(Hood, "The Guild will be furious. Let them be."),
        new(Bram, "They'll keep coming, you know. Debts are funny things. But now we can afford to make them wait."),
    };

    public static List<Line> Foreclosed(GameState state) => new()
    {
        new(Vane, Guild.Foreclosed),
        new(Bram, state.Week <= 2 ? "So soon? Well. We'll start again somewhere. A good eye is never broke for long."
            : $"{state.Week} weeks. You did better than I ever did. We'll start again somewhere."),
    };
}
