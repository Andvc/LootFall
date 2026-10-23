using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// Who's minding the stall this run (one of Bram's kin), on what terms (the challenge level), and with what talents
/// learned in runs before. Each is fixed when the run starts.
/// </summary>
public sealed partial class GameState
{
    /// <summary>Which of Bram's kin is minding the stall.</summary>
    public string Character { get; init; } = Characters.Kit;

    public CharacterDef Kin => Characters.Get(Character);

    public bool Is(string character) => Character == character;

    /// <summary>The challenge level: every one of the Guild's terms up to it is in force.</summary>
    public int Challenge { get; init; }

    /// <summary>One of the Guild's terms is in force: the challenge level is at least the one it comes in at.</summary>
    public bool Under(string term) => Challenge >= Challenges.LevelOf(term);

    /// <summary>Talents learned in earlier runs (from the profile, when the run started).</summary>
    public HashSet<string> Talents { get; init; } = new();

    public bool Knows(string talent) => Talents.Contains(talent);

    /// <summary>
    /// Bram's lessons (each new seller's first visit, and the scripted first day) are for Kit, with tips on: the others
    /// come to the stall knowing the trade, though Bram still has a word for what's new.
    /// </summary>
    public bool GivesLessons => Guided && Is(Characters.Kit);

    /// <summary>A new run: who, on what terms, with which talents; and the gold and tools they start with.</summary>
    public static GameState NewRun(int seed, string character = Characters.Kit, int challenge = 0, IEnumerable<string> talents = null, bool guided = true)
    {
        var state = new GameState(seed)
        {
            Character = Characters.Exists(character) ? character : Characters.Kit,
            Challenge = Math.Clamp(challenge, 0, Challenges.Max),
            Talents = new HashSet<string>((talents ?? Enumerable.Empty<string>()).Where(Core.Talents.Exists)),
        };
        state.Guided = guided;
        state.Gold = StartGold(state);
        if (state.Knows("ledger")) state.Met.UnionWith(Sellers.All.Select(s => s.Key));
        return state;
    }

    static int StartGold(GameState state)
    {
        int gold = StartingGold + (state.Knows("kin") ? Core.Talents.KinGold : 0);
        return state.Under(Challenges.LeanStart) ? gold / 2 : gold;
    }

    // ---- Hesk: a bad back and an honest scale ----

    /// <summary>Hesk's back won't let him shake a chest hard.</summary>
    public bool CanShakeHard => !Is(Characters.Hesk);

    /// <summary>Nor lift it to drop it.</summary>
    public bool CanDrop => !Is(Characters.Hesk);

    /// <summary>Every chest goes on Hesk's scale.</summary>
    public bool Weighs => Is(Characters.Hesk);

    // ---- Rosa: everybody haggles, and word gets round ----

    /// <summary>Everybody haggles with Rosa.</summary>
    public bool HagglesAll => Is(Characters.Rosa);

    /// <summary>Sellers who walked out on Rosa this week: they won't be back until the next.</summary>
    readonly HashSet<string> _snubbed = new();

    public IReadOnlyCollection<string> Snubbed => _snubbed;

    /// <summary>
    /// The seller just gone walked out. On an offer of Rosa's, word gets round, and they stay away this week; with the
    /// Shepherd's Pipe singing, they come back at the end of the day, calmer, at their first price (once).
    /// </summary>
    void NoteWalkout(Visit visit)
    {
        if (visit is not { Outcome: VisitOutcome.Left } || visit.Lesson != null) return;
        if (Shows(Heartsongs.Pipe) && !visit.Returned) Queue.Add(visit.Again());
        else if (HagglesAll && visit.OffersMade > 0) _snubbed.Add(visit.Seller.Key);
    }

    // ---- Isolde: a collection that pays its way ----

    /// <summary>What Isolde's visitors pay tonight: so much a point of exhibition.</summary>
    public int Admission => Is(Characters.Isolde) ? Exhibition * Characters.AdmissionPerPoint : 0;

    /// <summary>
    /// Whether this displayed thing can go to the Guild on a short night (never Isolde's, and never a Heartsong: greed
    /// can't price one).
    /// </summary>
    public bool CanPawn(Item item) => !Is(Characters.Isolde) && !item.IsSong && Display.Contains(item);

    // ---- The Guild's terms ----

    /// <summary>A seller who'll put the price up if you drop their chest (everyone, with fussy sellers about).</summary>
    public bool MindsDrop(SellerDef seller) => (seller.HatesDrop || Under(Challenges.FussySellers)) && !KidGloves;

    public bool MindsMagnet(SellerDef seller) => seller.HatesMagnet && !KidGloves;

    public bool MindsHardShake(SellerDef seller) => seller.HatesHardShake && !KidGloves;

    /// <summary>A seller who won't wait all day (everyone, when the town's in a hurry).</summary>
    public bool Impatient(SellerDef seller) => Patience(seller) > 0;

    /// <summary>
    /// A seller's terms, as Bram would put them under the chest: their rule, and anything they won't stand for (with the
    /// Guild's terms and kid gloves taken into account). Null if they have none.
    /// </summary>
    public string Terms(Visit visit)
    {
        var seller = visit.Seller;
        var terms = new List<string>();
        if (seller.ShakeLimit > 0) terms.Add($"Rotten: {seller.ShakeLimit:0} seconds of shaking, all told.");
        if (seller.ShakeFee > 0) terms.Add($"Each strength after your first: price +{Synergies.Pct(seller.ShakeFee)}.");
        if (seller.Wager > 0) terms.Add("Read it exactly: a third of the price back.");
        if (seller.Heirloom is Category heirloom) terms.Add($"The best {Categories.Get(heirloom).Name.ToLowerInvariant()} inside are his, for {visit.Fee}g.");
        if (seller.TakesDearest) terms.Add("The dearest thing inside is his.");
        if (seller.SlipPrice) terms.Add("Price: your slip. Exact: 25% off. Wrong: its worth +25%.");
        if (seller.Gaze > 0) terms.Add($"Buy, and everyone after him today asks x{seller.Gaze:0.#}.");
        var minds = new List<string>();
        if (MindsHardShake(seller)) minds.Add("a hard shake");
        if (MindsDrop(seller)) minds.Add("a drop");
        if (MindsMagnet(seller)) minds.Add("the lodestone");
        if (minds.Count > 0) terms.Add($"Minds {Sellers.Join(minds)}: half again.");
        return terms.Count == 0 ? null : string.Join(" ", terms);
    }

    /// <summary>What an upgrade costs this run: its price, but the witchlight and the magnet at half with Trade Secrets.</summary>
    public int Cost(Upgrade upgrade) => Knows("tools") && upgrade.Key is "lamp" or "magnet" ? upgrade.Cost / 2 : upgrade.Cost;

    /// <summary>A curio that would do nothing for this character on these terms: Nim doesn't bother bringing it.</summary>
    public bool NoUse(CurioDef curio) => curio.Key switch
    {
        "crate" => !CanDrop,
        "signet" => HagglesAll,
        "book" => Is(Characters.Isolde),
        _ => false,
    };

    // ---- Talents ----

    /// <summary>Mara's favour (a talent): she'll cover one short night a run.</summary>
    public bool MaraUsed { get; private set; }

    /// <summary>Mara would pay what's short tonight: nothing you own can cover it, and it's not the last week.</summary>
    public bool CanAskMara => Knows("mara") && !MaraUsed && DuesTonight && !PaidThisWeek && !CanCover && Week < Guild.Weeks;

    /// <summary>Mara pays what's short, and the due is paid. Returns what she paid.</summary>
    public int AskMara()
    {
        if (!CanAskMara) return 0;
        int gift = DueNow - Gold;
        MaraUsed = true;
        Gold += gift;
        PayDues();
        return gift;
    }

    /// <summary>Nim has shown you something else on this visit (Second Look).</summary>
    public bool PeddlerRerolled { get; private set; }

    public bool CanRerollPeddler => Knows("reroll") && PeddlerToday && !PeddlerRerolled;

    /// <summary>Second Look: Nim packs up what's on the counter and lays out something else.</summary>
    public void RerollPeddler()
    {
        if (!CanRerollPeddler) return;
        PeddlerRerolled = true;
        var shown = PeddlerStock.Select(c => c.Key).ToHashSet();
        PeddlerStock.Clear();
        PeddlerStock.AddRange(Curios.Stock(new Random(unchecked(Seed * 6151 + Day * 31 + 7)), this, PeddlerCount, shown));
    }

    /// <summary>Whether Nim's keepsake (a talent) has been given: one curio, free, on his first visit.</summary>
    public bool KeepsakeTaken { get; private set; }

    /// <summary>On Nim's first visit, one curio of your choice is a gift.</summary>
    public bool Keepsake => Knows("keepsake") && !KeepsakeTaken && Week == Curios.FirstWeek && PeddlerToday;

    /// <summary>How many curios Nim lays out.</summary>
    public int PeddlerCount => 3 + (Knows("nim4") ? Core.Talents.MoreStock : 0);

    /// <summary>What Nim asks for a curio today (nothing, while the keepsake is still to give).</summary>
    public int CuriosPrice(CurioDef curio) => Keepsake ? 0 : Curios.Price(curio, Week);
}
