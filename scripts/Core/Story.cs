using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>One line in a scene: who says it (a portrait key) and what they say.</summary>
public sealed record Line(string Speaker, string Text);

/// <summary>
/// What's going on behind the counter (docs/world.md). Three hundred years ago the Locking shut every treasure in
/// Aldmark into a warded chest, to starve Avarax, the Gilded Maw, chained behind the Gilded Gate. Only a Keywright can
/// open one, once it's rightfully theirs, and every one opened loosens the Locking. Bram opened chests unlicensed for
/// twenty years: the Gilded Guild, which licenses Keywrights and secretly serves the Maw, wants its back-tithe. Bram
/// kept one shard of the Aldric Key; Mara, the last archmage of the Keyhold, trades the others for things only a
/// Keywright's chests hold. The whole Key opens the Crown Chest, which renews the Locking. Scenes play at the start of
/// some weeks, when the Guild comes, and at the end.
/// </summary>
public static class Story
{
    public const string Bram = "bram", Vane = "vane", Hood = "hood", Nim = "nim", StrangerKey = "stranger";

    public const string HoodName = "The Hooded Woman";

    /// <summary>The Aldric Key comes in four shards: Bram's, and three the Hooded Woman trades for.</summary>
    public const int Pieces = 4;

    /// <summary>The week the Guild offers to take the shards off your hands for half that week's due.</summary>
    public const int OfferWeek = 6;

    /// <summary>Who brings the Crown Chest to the counter (nobody's selling: it's yours, by the King's gift).</summary>
    public static readonly SellerDef VaultSeller = new()
    {
        Key = Hood, Name = HoodName, Tier = 4, Judge = Judgment.Clueless,
        Kinds = new[] { 1f, 1, 1, 1, 1, 1 }, Colours = new[] { 0f, 0, 0, 1 },
        Habit = "Mara, the last archmage of the Keyhold, and Bram's old partner. She's hunted the Aldric Key for twenty years.",
        Pitches = new[] { "The Crown Chest. The King gives it to you: it's yours by right. Open it, Keywright." },
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
        new(Vane, $"Evening. {Guild.Collector}, Collector for the Gilded Guild. Every Keywright pays the Guild a tithe, to keep " +
                  "the Gate. Your uncle never did."),
        new(Bram, "Twenty years I opened chests for folk who couldn't pay a Guild Keywright! For pennies!"),
        new(Vane, $"Twenty years of tithe, Bram: {Guild.Debt}g. The Guild will take it in {Guild.Weeks} weekly payments, starting " +
                  $"small. {Guild.Due(1)}g at the end of the week. Each week, more."),
        new(Vane, "Miss one, and the Guild seals the shop. Good evening, Keywright."),
        new(Bram, $"Every {Guild.DaysPerWeek} days. Don't worry: a good ear pays any debt. We'll talk in the morning."),
    };

    /// <summary>The morning a week begins (from the second): who comes by, and what they want.</summary>
    public static List<Line> WeekStart(GameState state)
    {
        int week = state.Week;
        var lines = new List<Line>();
        switch (week)
        {
            case 2:
                lines.Add(new(Bram, "You've a right to know what the chests are. Three hundred years ago the Maw, Avarax, ruled this " +
                                    "realm through its gold. Every coin was his eye."));
                lines.Add(new(Bram, "King Aldric and the archmages locked every treasure away, and chained the Maw behind the Gilded " +
                                    "Gate. The Locking. The chests are what's left of it."));
                lines.Add(new(Bram, "And this is my one secret: a shard of the Aldric Key, broken in four when the Locking was made. " +
                                    "It's yours now. Don't show the Guild."));
                break;
            case 3:
                lines.Add(new(Hood, "You're Bram's kin. You have the mark, and his ear for a chest, they say."));
                lines.Add(new(Hood, "I have something your uncle has wanted for twenty years. Find me what I ask for, and it's yours."));
                break;
            case 4:
                lines.Add(state.MapPieces >= 2
                    ? new(Bram, "That's... a second shard. Where did she get it? Never mind. Never mind!")
                    : new(Bram, "That woman in the hood. I know that voice. Do what she asks."));
                lines.Add(new(Bram, "And word is the Guild's been buying every chest with a royal seal. Funny, for tithe-takers."));
                break;
            case 5:
                lines.Add(new(Hood, "The whole Key opens the Crown Chest: King Aldric's own. The heart of the Locking is in it."));
                lines.Add(new(Hood, "The Guild doesn't want your gold. It wants the Chest. Your uncle's debt is a leash. Keep paying them, " +
                                    "and keep finding me things."));
                break;
            case 6:
                lines.Add(new(Bram, "Vane's been asking about the shards. Whatever he offers you, think hard."));
                break;
            case 7:
                if (state.MapGiven)
                {
                    lines.Add(new(Hood, "You gave them the shards. Then the Guild will have the Crown Chest, and the Gate will open."));
                    if (!state.DebtCleared) lines.Add(new(Hood, "Pay your debt, then. It's all you have left."));
                }
                else
                {
                    lines.Add(new(Hood, "My name is Mara. I was the last archmage of the Keyhold. Your uncle and I found the first shard " +
                                        "together, a long time ago."));
                    lines.Add(new(Hood, "Every tithe the Guild takes goes down through the Gilded Gate. The Maw is buying his way out, " +
                                        "a coin at a time. And every chest you open loosens the Locking."));
                    lines.Add(state.Tier >= 4
                        ? new(Bram, $"And that old fellow at the Emporium, the one with the ring? Look at the seal on it, {state.Kin.Name}. " +
                                    "That's the King. He's been testing you.")
                        : new(Hood, "The King knows. He's looking for a Keywright he can trust with the Crown Chest. Be one."));
                }
                break;
            case 8 when state.DebtCleared:
                lines.Add(new(Bram, "The last week of the tithe, and it's already paid. Nobody's coming for anything this week."));
                if (state.MapPieces >= Pieces - 1 && !state.MapGiven)
                    lines.Add(new(Bram, "If Mara gets what she's after, the Key is whole. Let's find it for her."));
                break;
            case 8:
                lines.Add(new(Bram, "The last week. Pay this, and the stall is ours. Truly ours."));
                if (state.MapPieces >= Pieces)
                    lines.Add(new(Bram, "And with the Key whole... Mara says she knows where the Crown Chest is."));
                break;
            case Guild.Weeks + 1:
                lines.Add(new(Vane, "The tithe is paid. But the Gate is hungry, and the Guild has decided you owe us for our patience."));
                lines.Add(new(Bram, "They'll keep coming, and every week they'll want half as much again. Let's see how long we can make them wait."));
                break;
        }
        // Whenever she wants something for her next shard, she says what.
        var ask = state.Commissions.FirstOrDefault(c => c.Story);
        if (ask != null)
            lines.Add(new(Hood, $"{HoodAsks(ask)} Bring me {ask.Phrase} {(state.DebtCleared ? "this week" : "before the Guild comes again")}, " +
                                "and the next shard is yours."));
        return lines;
    }

    /// <summary>What the Hooded Woman says when she asks for the next shard's price: why she wants it.</summary>
    public static string HoodAsks(Commission ask) => ask.Kind == CommissionKind.Enchanted
        ? "Something enchanted. The last shard was hidden with magic, and only magic will find it."
        : ask.Category switch
        {
            Category.Scrolls => "Old scrolls. The Keyhold wrote down where it hid every shard.",
            Category.Gems => "A gem. The Key was set with them, and they remember where the rest of it went.",
            Category.Coins => "Old coin. The Key-smiths were paid in it, and stamped every coin with where they worked.",
            _ => "A blade. The Wardens who carried the shards away were buried with their swords.",
        };

    public static string HoodThanks(int piece) => piece switch
    {
        1 => "Good. Here: the second shard. Don't show the Guild.",
        2 => "The third shard. One more, and the Key is whole.",
        _ => "The last shard. Bram will know what to do. So will I.",
    };

    /// <summary>The Guild's offer, on the night of <see cref="OfferWeek"/>.</summary>
    public static List<Line> Offer(int pieces) => new()
    {
        new(Vane, $"Before you pay. The Guild knows about the Key. {Count(pieces)}, I hear."),
        new(Vane, "Give them to me, and the Guild will forget half of tonight's tithe. Generous, no?"),
    };

    static string Count(int pieces) => Capital(Sellers.Number(pieces)) + (pieces == 1 ? " shard" : " shards");

    static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    public const string OfferTaken = "A sensible choice. The Guild remembers its friends. So does its master.";
    public const string OfferRefused = "Pity. The Guild remembers that too.";

    /// <summary>The debt is paid: the last due, or all that's left of it at once, ahead of time.</summary>
    public static List<Line> Cleared(GameState state)
    {
        int early = Guild.Weeks - state.Week;
        string when = early > 0 ? $"{Capital(Sellers.Number(early))} week{(early == 1 ? "" : "s")} early" : "Before I've even come for it";
        var lines = early > 0 || !state.DuesTonight
            ? new List<Line>
            {
                new(Vane, $"All of it? Now? {when}. The Guild... has no further claim on Bram's stall."),
                new(Bram, "Free and clear, and ahead of time! Twenty years, and the stall is ours."),
            }
            : new List<Line>
            {
                new(Vane, Guild.Paid(Guild.Weeks)),
                new(Bram, "Free and clear! Twenty years, and the stall is ours."),
            };
        if (state.MapPieces >= Pieces) lines.AddRange(MapWhole());
        else if (state.MapGiven)
            lines.Add(new(Bram, "And the Guild has the shards. Somewhere under the palace, they're looking for the Crown Chest. Let them look."));
        else if (state.Week < Guild.Weeks)
            lines.Add(new(Bram, $"And the Key's still {state.MapPieces} shards of {Pieces}. Mara will keep asking: keep trading, and we may yet find the Crown Chest."));
        else
            lines.Add(new(Bram, "The Crown Chest can wait. The Key's not going anywhere, and neither are we."));
        return lines;
    }

    /// <summary>The Key is whole and the debt is paid: Mara comes for Bram, and the Crown Chest.</summary>
    public static List<Line> MapWhole() => new()
    {
        new(Hood, "Bram. It's been a long time."),
        new(Bram, "Mara. The Key. You found all of it."),
        new(Hood, "Your kin did the finding. The King has given us the Crown Chest: it's yours by right now, Keywright. It opens " +
                  "for nobody else. Quickly: the Guild is a day behind us."),
    };

    /// <summary>After the Crown Chest's been opened.</summary>
    public static List<Line> VaultEpilogue() => new()
    {
        new(Hood, "Listen. The Gate's gone quiet. The Locking holds: another three hundred years, if we're lucky."),
        new(Bram, "King Aldric's own hoard, on our counter. The old man said it's ours to keep."),
        new(Bram, "The Guild will keep coming, you know. Debts are funny things. But now we can afford to make them wait."),
    };

    public static List<Line> Foreclosed(GameState state) => new()
    {
        new(Vane, Guild.Foreclosed),
        new(Bram, state.DebtCleared ? $"{state.Week} weeks, and every coin of the tithe paid. Let them have the building: they can't have the mark."
            : state.Week <= 2 ? "So soon? Well. We'll start again somewhere. A good ear is never broke for long."
            : $"{state.Week} weeks. You did better than I ever did. We'll start again somewhere."),
    };
}
