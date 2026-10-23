using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

public enum CurioRarity { Common, Uncommon, Rare }

/// <summary>
/// A curio: an oddment from the Locking, from Nim the peddler, that bends one of the shop's rules for as long as you own it. You have room
/// for a few (more with a curio case), so which ones you keep shapes how you trade: what to buy, who to haggle with,
/// whether to gamble or play safe. None of them tells you what's in a chest before you buy it, and none adds a share to
/// what things sell for: the shop grows by your judgment, and a few percent on every sale would grow it for you.
/// </summary>
/// <param name="Icon">Icon under res://assets/ui/curios/.</param>
/// <param name="MinTier">Nim only offers it from this shop tier (where it does anything).</param>
public sealed record CurioDef(string Key, string Name, string Blurb, CurioRarity Rarity, string Icon, int MinTier = 1);

public static class Curios
{
    // How much each effect does, for the rules below and the blurbs.
    public const float InterestRate = 0.05f, HookHidden = 0.05f, DraughtMagic = 0.03f, TicketRate = 0.8f, LoupeFakes = 0.25f,
        WatchPatience = 2f, TokenSwing = 0.25f;

    public const int InterestPerTier = 25, CataloguePerTier = 10, BookPerPoint = 2, ShellPityAfter = 7;

    public static readonly CurioDef[] All =
    {
        new("coin", "Lucky Coin", "The first chest you open each day: everything in it gets two tries at its quality.", CurioRarity.Common, "coin"),
        new("shell", "Lucky Shell", "Luck evens out: after a run of plain finds, a surprise comes twice as soon.", CurioRarity.Common, "shell"),
        new("lantern", "Shop Lantern", "Open late on the Guild's night: one more customer that day.", CurioRarity.Common, "lantern"),
        new("catalogue", "Collector's Catalogue", $"Every new find in your journal pays {CataloguePerTier}g per shop tier.",
            CurioRarity.Common, "catalogue"),
        new("crate", "Padded Crate", "Glass never breaks when you drop a chest.", CurioRarity.Common, "crate", MinTier: 2),
        new("oil", "Locksmith's Oil", "Every lock has one pin fewer.", CurioRarity.Common, "oil", MinTier: 2),
        new("purse", "Rainy-Day Purse", "Put by for a rainy day: Bram's stash has one more dip in it.", CurioRarity.Common, "purse"),
        new("ticket", "Pawn Ticket", "Pawning on a short night fetches four-fifths of the price, not three-fifths.",
            CurioRarity.Common, "ticket"),

        new("signet", "Merchant's Signet", "Hagglers hear you out: an offer too low gets a second try, not a slammed door.",
            CurioRarity.Uncommon, "signet"),
        new("jar", "Savings Jar", $"Every night: a twentieth of your gold as interest (up to {InterestPerTier}g per shop tier).",
            CurioRarity.Uncommon, "jar"),
        new("letter", "Letter of Introduction", "Regulars take what you deliver a quality lower than they asked for.", CurioRarity.Uncommon, "letter"),
        new("knife", "Burglar's Knife", "Forcing a lock breaks nothing and scratches nothing.", CurioRarity.Uncommon, "knife", MinTier: 2),
        new("hook", "Smuggler's Hook", "Smugglers come your way: false bottoms turn up a little more often.", CurioRarity.Uncommon, "hook", MinTier: 2),
        new("draught", "Witch's Draught", "Enchanted things turn up a little more often.", CurioRarity.Uncommon, "draught", MinTier: 3),
        new("watch", "Pocket Watch", "Impatient sellers give you twice as long.", CurioRarity.Uncommon, "watch", MinTier: 4),
        new("token", "Gambler's Token", "Luck swings both ways: twice as many surprises, good and bad.",
            CurioRarity.Uncommon, "token"),
        new("crescent", "Cursed Crescent", "No surprises: everything comes out exactly as its chest promised.",
            CurioRarity.Uncommon, "crescent"),
        new("marker", "Guildsman's Marker", "Bram's word is good twice a run, not once.", CurioRarity.Uncommon, "marker"),
        new("gauntlet", "Smith's Glove", "You mend weapons: they never come out worse than their chest promised.", CurioRarity.Uncommon, "gauntlet"),
        new("cap", "Hatter's Cap", "You mend cloaks: they never come out worse than their chest promised.", CurioRarity.Uncommon, "cap"),
        new("loupe", "Jeweller's Loupe", "Paste gems and demon gold sell as costume pieces: a quarter of the real thing.",
            CurioRarity.Uncommon, "loupe", MinTier: 4),

        new("clover", "Four-Leaf Clover", "The first chest you open each week: everything in it is a step better.",
            CurioRarity.Rare, "clover"),
        new("book", "Visitors' Book", $"Every night, visitors leave {BookPerPoint}g for each point of your exhibition.", CurioRarity.Rare, "book",
            MinTier: 2),
        new("gloves", "Kid Gloves", "Nobody minds a hard shake, a drop or the lodestone on their chest.",
            CurioRarity.Rare, "gloves", MinTier: 2),
        new("dome", "Bell Jar", "Room to display one more thing.", CurioRarity.Rare, "dome", MinTier: 2),
    };

    public static CurioDef Get(string key) => All.First(c => c.Key == key);

    public static bool Exists(string key) => All.Any(c => c.Key == key);

    /// <summary>Room for this many curios, more with a curio case.</summary>
    public const int Slots = 3;

    /// <summary>Nim's first visit is in the second week.</summary>
    public const int FirstWeek = 2;

    /// <summary>A curio's price as a share of the week's due: about what it's worth to a shop that's keeping up.</summary>
    static float Share(CurioRarity rarity) => rarity switch
    {
        CurioRarity.Common => 0.1f,
        CurioRarity.Uncommon => 0.25f,
        _ => 0.6f,
    };

    /// <summary>
    /// What Nim asks for one, this week. Prices follow the Guild's due, which follows what a shop that's keeping up
    /// makes, so a curio costs about the same share of your means in week 3 as in week 8: never a fortune early, never
    /// pocket change late. On his first visit the common ones are a single coin, to get you started.
    /// </summary>
    public static int Price(CurioDef curio, int week)
    {
        if (curio.Rarity == CurioRarity.Common && week <= FirstWeek) return 1;
        return Math.Max(1, Money.Round(Guild.Due(Math.Max(FirstWeek, week)) * Share(curio.Rarity)));
    }

    /// <summary>
    /// What Nim lays out on a visit: three curios (four for a familiar face) you don't own and could use, rarer ones more
    /// often as the weeks go by. Leaves out what he's just shown you.
    /// </summary>
    public static List<CurioDef> Stock(Random rng, GameState state, int count = 3, ICollection<string> shown = null)
    {
        var pool = All.Where(c => c.MinTier <= state.Tier && !state.HasCurio(c.Key) && !state.NoUse(c) && (shown == null || !shown.Contains(c.Key))).ToList();
        var stock = new List<CurioDef>();
        float rare = Math.Min(0.3f, 0.05f + 0.04f * state.Week), uncommon = 0.35f;
        while (stock.Count < count && pool.Count > 0)
        {
            var pick = rng.PickWeighted(pool.ToArray(), c => c.Rarity switch
            {
                CurioRarity.Rare => rare,
                CurioRarity.Uncommon => uncommon,
                _ => 1 - rare - uncommon,
            } / Math.Max(1, pool.Count(o => o.Rarity == c.Rarity)));
            stock.Add(pick);
            pool.Remove(pick);
        }
        // Nim's first visit always has something anyone can afford.
        var commons = pool.Where(c => c.Rarity == CurioRarity.Common).ToArray();
        if (state.Week <= FirstWeek && stock.Count > 0 && stock.All(c => c.Rarity != CurioRarity.Common) && commons.Length > 0)
            stock[^1] = rng.PickOne(commons);
        return stock;
    }

    public static string RarityName(CurioRarity rarity) => rarity.ToString().ToLowerInvariant();

    /// <summary>What the opening says a curio (or a Heartsong on display) did to a chest: "Lucky Coin: 2 things came out better!"</summary>
    public static string LuckLine(string key, int things)
    {
        string n = $"{things} thing{(things == 1 ? "" : "s")}";
        string name = Exists(key) ? Get(key).Name : Heartsongs.Get(key).Name;
        return key switch
        {
            "token" => $"{name}: {n} swung!",
            "gauntlet" or "cap" => $"{name}: {n} mended.",
            Heartsongs.Egg => $"{name}: {n} guarded.",
            _ => $"{name}: {n} came out better!",
        };
    }
}

/// <summary>The peddler who sells curios.</summary>
public static class Peddler
{
    public const string Name = "Nim the Peddler";

    public static readonly string[] Greetings =
    {
        "Psst. Curios! Lucky, cursed, and everything in between.",
        "Back again! Nim's got just the thing. Several things, actually.",
        "Don't tell the Guild I was here. Now, what catches your eye?",
        "Fresh from a dead wizard's attic. Mostly fresh.",
    };

    public static readonly string[] Thanks = { "A fine choice. Mostly harmless.", "Pleasure doing business!", "It likes you. I can tell." };
    public static readonly string[] Bye = { "Next week, then. Same time, roughly.", "Off I go. Mind the Guild." };
}
