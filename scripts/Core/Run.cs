using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>
/// The run around the days: five-day weeks, each ending with the Guild's due; curios that bend the shop's rules;
/// commissions from regulars; one event a week; and the map to King Aldric's vault.
/// </summary>
public sealed partial class GameState
{
    // ---- Weeks and the Guild's dues ----

    /// <summary>The week today is in, from 1.</summary>
    public int Week => Math.Max(0, Day - 1) / Guild.DaysPerWeek + 1;

    /// <summary>Today's place in the week, 1 to <see cref="Guild.DaysPerWeek"/> (0 before the first day).</summary>
    public int DayOfWeek => Day <= 0 ? 0 : (Day - 1) % Guild.DaysPerWeek + 1;

    /// <summary>The Guild comes tonight.</summary>
    public bool DuesTonight => Day > 0 && DayOfWeek == Guild.DaysPerWeek;

    /// <summary>Days of trading after today before the Guild comes.</summary>
    public int DaysToDue => Guild.DaysPerWeek - Math.Max(1, DayOfWeek);

    public int WeeksPaid { get; private set; }
    public bool Foreclosed { get; private set; }

    /// <summary>All eight weeks of Bram's debt are paid. The Guild keeps coming (endless), but the run is won.</summary>
    public bool DebtCleared => WeeksPaid >= Guild.Weeks;

    /// <summary>This week's due, with any curio's effect (and the Guild's half-price deal for the map).</summary>
    public int DueNow => Money.Round(DueFor(Week) * (_halfDue ? 0.5f : 1)) + Arrears;

    /// <summary>A week's due with your curios as they are now.</summary>
    public int DueFor(int week) => Money.Round(Guild.Due(week) * (HasCurio("receipt") ? Curios.ReceiptDues : 1) *
                                               (HasCurio("crescent") ? Curios.CrescentDues : 1));

    bool _halfDue;

    /// <summary>What's left of last week's due after Bram vouched for you, with the Guild's interest.</summary>
    public int Arrears { get; private set; }

    /// <summary>Bram can vouch for you once a run: a short night rolls over to next week instead of costing the shop.</summary>
    public bool GraceLeft { get; private set; } = true;

    /// <summary>The Guild's interest on a due that rolls over.</summary>
    public const float GraceInterest = 1.5f;

    /// <summary>Bram's word is good for one week, but not for the last of the debt.</summary>
    public bool CanAskGrace => GraceLeft && Gold < DueNow && Week < Guild.Weeks;

    /// <summary>
    /// Bram vouches for you: everything you have goes to the Guild, and the rest (with interest) is added to next week's
    /// due. Once a run.
    /// </summary>
    public int AskGrace()
    {
        if (!CanAskGrace) return 0;
        int owed = (int)MathF.Ceiling((DueNow - Gold) * GraceInterest);
        Gold = 0;
        GraceLeft = false;
        WeeksPaid++;
        _halfDue = false;
        Arrears = owed;
        LastTopUp = TopUp();
        return owed;
    }

    /// <summary>This week's due is paid (it's the Guild's night, and they've been).</summary>
    public bool PaidThisWeek => WeeksPaid >= Week;

    /// <summary>The next due the Guild will come for, and the days of trading left before it.</summary>
    public int NextDue => PaidThisWeek ? DueFor(Week + 1) : DueNow;
    public int DaysToNextDue => PaidThisWeek ? Guild.DaysPerWeek : DaysToDue;

    /// <summary>What pawning a displayed thing fetches tonight: the Guild's price.</summary>
    public int PawnValue(Item item) => (int)MathF.Round(SaleValue(item) * Guild.PawnRate);

    /// <summary>What a curio fetches if you sell it back.</summary>
    public int CurioValue(string key) => Curios.SellPrice(Curios.Get(key), Week);

    /// <summary>Everything that could be pawned to make up a due.</summary>
    public int PawnableWorth => Display.Sum(PawnValue) + _curios.Sum(CurioValue);

    /// <summary>Whether there's any way to pay tonight, pawning everything.</summary>
    public bool CanCover => Gold + PawnableWorth >= DueNow;

    /// <summary>Pawns something on display to make up the due.</summary>
    public int Pawn(Item item)
    {
        if (!Display.Remove(item)) return 0;
        int value = PawnValue(item);
        Gold += value;
        return value;
    }

    /// <summary>Sells a curio back (to Nim, or to the Guild on a short night) for half what it costs now.</summary>
    public int SellCurio(string key)
    {
        if (!_curios.Remove(key)) return 0;
        int value = CurioValue(key);
        Gold += value;
        return value;
    }

    /// <summary>Pays the week's due, if there's enough gold. Bram tops you up afterwards if it leaves you nearly broke.</summary>
    public bool PayDues()
    {
        int due = DueNow;
        if (Gold < due) return false;
        Gold -= due;
        WeeksPaid++;
        _halfDue = false;
        Arrears = 0;
        LastTopUp = TopUp();
        return true;
    }

    /// <summary>What Bram slipped you after the last due, if anything.</summary>
    public int LastTopUp { get; private set; }

    /// <summary>The Guild takes the shop: the run is over.</summary>
    public void Foreclose() => Foreclosed = true;

    // ---- The map ----

    /// <summary>Bram's own piece of the map (from the second week).</summary>
    public bool BramPiece { get; private set; }

    /// <summary>Pieces the Hooded Woman has traded you.</summary>
    public int HoodPieces { get; private set; }

    /// <summary>You gave the map to the Guild for half a week's due.</summary>
    public bool MapGiven { get; private set; }

    public bool VaultOpened { get; set; }

    public int MapPieces => MapGiven ? 0 : (BramPiece ? 1 : 0) + HoodPieces;

    /// <summary>The Guild's offer for the map comes tonight.</summary>
    public bool OfferTonight => DuesTonight && Week == Story.OfferWeek && MapPieces > 0 && !MapGiven;

    /// <summary>Hands the map to Vane: half tonight's due, and no vault.</summary>
    public void GiveMap()
    {
        MapGiven = true;
        _halfDue = true;
        Commissions.RemoveAll(c => c.Story);
    }

    /// <summary>The vault is waiting: the debt is paid and the map is whole.</summary>
    public bool VaultDue => DebtCleared && MapPieces >= Story.Pieces && !VaultOpened;

    /// <summary>
    /// King Aldric's vault: the best of everything, much of it enchanted, behind a dwarven lock. Mara and Bram bring it
    /// to the counter; nobody's asking a price.
    /// </summary>
    public Visit VaultVisit()
    {
        var items = new[]
        {
            new Item(Category.Coins, Rarity.Legendary, Magic: true), new Item(Category.Jewels, Rarity.Legendary, Magic: true),
            new Item(Category.Metal, Rarity.Legendary), new Item(Category.Cloth, Rarity.Legendary), new Item(Category.Bottles, Rarity.Epic, Magic: true),
            new Item(Category.Paper, Rarity.Legendary), new Item(Category.Jewels, Rarity.Epic), new Item(Category.Coins, Rarity.Epic),
        };
        var chest = new Chest(items, Rarity.Legendary, new Item(Category.Jewels, Rarity.Legendary), LockTiers.All[3], gilded: true);
        return new Visit(Story.VaultSeller, chest, Array.Empty<Category>(), Trade.Fixed, 0, 1, "King Aldric's vault.");
    }

    // ---- Curios ----

    readonly List<string> _curios = new();

    public IReadOnlyList<string> CurioKeys => _curios;
    public IEnumerable<CurioDef> OwnedCurios => _curios.Select(Curios.Get);
    public bool HasCurio(string key) => _curios.Contains(key);
    public int CurioSlots => Curios.Slots + Upgrades.All.Where(u => Has(u.Key)).Sum(u => u.CurioSlots);
    public bool CuriosFull => _curios.Count >= CurioSlots;

    public bool BuyCurio(CurioDef curio, int price)
    {
        if (CuriosFull || HasCurio(curio.Key) || Gold < price) return false;
        Gold -= price;
        _curios.Add(curio.Key);
        Today.CurioSpent += price;
        return true;
    }

    /// <summary>The day of the week Nim comes by this week (0: not this week).</summary>
    public int PeddlerDay { get; private set; }

    public bool PeddlerToday => PeddlerDay > 0 && DayOfWeek == PeddlerDay;

    /// <summary>What Nim has today.</summary>
    public List<CurioDef> PeddlerStock { get; } = new();

    /// <summary>What a curio's luck did to a chest you've just bought: which curio, and how many things it improved.</summary>
    (string Curio, int Improved) Luck(Chest chest)
    {
        if (chest.Sealed || chest.Items.Count == 0) return (null, 0);
        int improved = 0;
        if (HasCurio("coin") && Today.Bought == 1)
        {
            chest.Improve(i =>
            {
                if (i.Fake || i.Broken) return i;
                var again = Categories.RollQuality(Rng, chest.Quality);
                if (again <= i.Quality) return i;
                improved++;
                return i with { Quality = again };
            });
            if (improved > 0) return ("coin", improved);
        }
        if (HasCurio("shell") && Rng.NextFloat() < Curios.ShellChance)
        {
            chest.Improve(i =>
            {
                if (i.Fake || i.Broken || i.Quality == Rarity.Legendary) return i;
                improved++;
                return i with { Quality = i.Quality + 1 };
            });
            if (improved > 0) return ("shell", improved);
        }
        return (null, 0);
    }

    /// <summary>What a pick grade does to a thing's price: forcing with a burglar's knife scratches nothing.</summary>
    public float GradeMultiplier(PickGrade grade) => grade == PickGrade.Forced && HasCurio("knife") ? 1f : Picks.Multiplier(grade);

    /// <summary>How much less likely glass is to break when you force a lock (a burglar's knife: it never does).</summary>
    public float ForceSturdier => HasCurio("knife") ? 1f : Sturdier;

    /// <summary>What curios add to the price of a thing of this kind, and what today's event does.</summary>
    float CurioSales(Category kind)
    {
        float kindBonus = kind switch
        {
            Category.Coins or Category.Jewels when HasCurio("loupe") => Curios.KindBonus,
            Category.Metal when HasCurio("gauntlet") => Curios.KindBonus,
            Category.Cloth or Category.Paper when HasCurio("cap") => Curios.KindBonus,
            _ => 0,
        };
        return kindBonus;
    }

    float EventSales(Category kind) => (HasCurio("crescent") ? Curios.CrescentSales : 1) *
                                       (EventIs("festival") ? Events.FestivalSales : 1) *
                                       (EventIs("buyer") && kind == BuyerKind ? Events.BuyerSales : 1);

    // ---- Commissions ----

    /// <summary>This week's commissions (they lapse when the Guild comes).</summary>
    public List<Commission> Commissions { get; } = new();

    /// <summary>A commission this thing would do for, if any.</summary>
    public Commission WantedBy(Item item) => Commissions.FirstOrDefault(c => c.Wants(item));

    public int CommissionReward(Commission c) => Money.Round(c.Reward * (HasCurio("letter") ? 1 + Curios.LetterBonus : 1));

    /// <summary>
    /// Hands something over for a commission instead of selling it (giving up <paramref name="forgo"/>, its sale price,
    /// if that was already counted). Returns what it paid (once it's complete; 0 for a lot still short).
    /// </summary>
    public int Deliver(Item item, Commission commission, int forgo = 0)
    {
        if (!commission.Wants(item)) return 0;
        Gold -= forgo;
        Today.Earned -= forgo;
        commission.Delivered++;
        Today.Delivered++;
        if (!commission.Done) return 0;
        int reward = CommissionReward(commission);
        Gold += reward;
        Today.Commissions += reward;
        if (commission.Story) HoodPieces++;
        return reward;
    }

    /// <summary>
    /// When the Hooded Woman wants her next piece's price: week 3, 5 and 7 (or the week after, if you missed one), until
    /// the debt's last week. After that she's gone looking on her own.
    /// </summary>
    static int HoodScheduled(int week) => week >= 7 ? 3 : week >= 5 ? 2 : week >= 3 ? 1 : 0;

    // ---- Events ----

    /// <summary>This week's event and the day of the week it happens.</summary>
    public string WeekEvent { get; private set; }
    public int EventDay { get; private set; }

    /// <summary>The kind the Royal Buyer pays double for.</summary>
    public Category BuyerKind { get; private set; }

    readonly List<string> _eventsSeen = new();

    public EventDef TodayEvent => WeekEvent != null && EventDay > 0 && DayOfWeek == EventDay ? Events.Get(WeekEvent) : null;

    public bool EventIs(string key) => TodayEvent?.Key == key;

    int EventCustomers => EventIs("market") ? Events.MarketCustomers : EventIs("storm") ? Events.StormCustomers : 0;

    /// <summary>
    /// A new week: last week's commissions lapse; from the second week, the regulars ask for things, the Hooded Woman
    /// for hers, an event is set for one day and Nim for another. All from the seed, so a saved shop gets the same week.
    /// </summary>
    void StartWeek()
    {
        var rng = new Random(unchecked(Seed * 104729 + Week * 7907));
        Commissions.Clear();
        WeekEvent = null;
        EventDay = 0;
        PeddlerDay = 0;
        if (Week < 2) return;

        BramPiece = true;
        Commissions.AddRange(Patrons.Roll(rng, this));
        if (!MapGiven && Week <= Guild.Weeks && HoodPieces < Story.Pieces - 1 && HoodPieces < HoodScheduled(Week))
            Commissions.Insert(0, Patrons.Hood(HoodPieces + 1, Tier));

        var e = Events.Pick(rng, Tier, _eventsSeen);
        WeekEvent = e.Key;
        // The first event comes later in its week: that week's first morning already brings Nim and the commissions.
        EventDay = rng.Next(Week == Events.FromWeek ? 3 : 1, Guild.DaysPerWeek + 1);
        var kinds = Categories.UpTo(Tier).Select(c => c.Category).ToArray();
        BuyerKind = rng.PickOne(kinds);
        // Nim's first visit is the morning after the Guild's first: something new just when the shop's found its feet.
        PeddlerDay = Week == 2 ? 1 : rng.Next(1, Guild.DaysPerWeek);
    }

    /// <summary>Today's event, if it brings someone: the Stranger joins the queue, or one chest is gilded.</summary>
    void StageEvent()
    {
        if (EventIs("stranger"))
            Queue.Insert(Rng.Next(1, Queue.Count + 1), Events.StrangerVisit(Rng, Tier));
        if (EventIs("gilded"))
        {
            var plain = Queue.Select((v, i) => (v, i)).Where(p => p.v.Lesson == null).ToArray();
            if (plain.Length > 0)
            {
                var (visit, index) = plain[Rng.Next(plain.Length)];
                Queue[index] = Events.Gild(Rng, visit);
            }
        }
    }

    // ---- Each night ----

    /// <summary>What curios bring in overnight: interest on savings, visitors' coins. Paid at the end of the day.</summary>
    void Overnight()
    {
        if (HasCurio("jar"))
        {
            int interest = Math.Min((int)(Gold * Curios.InterestRate), Curios.InterestPerTier * Tier);
            Gold += interest;
            Today.Curios += interest;
        }
        if (HasCurio("book"))
        {
            int coins = Curios.BookPerPoint * Exhibition;
            Gold += coins;
            Today.Curios += coins;
        }
    }

    /// <summary>A new find in the journal: a collector's catalogue pays for it.</summary>
    void Catalogue()
    {
        if (!HasCurio("catalogue")) return;
        int fee = Curios.CataloguePerTier * Tier;
        Gold += fee;
        Today.Curios += fee;
    }
}
