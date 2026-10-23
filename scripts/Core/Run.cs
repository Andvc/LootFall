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

    /// <summary>Vane has introduced the Guild (the first evening): until then, nothing shows the due.</summary>
    public bool GuildIntroduced => Day > 1 || WeeksPaid > 0;

    public bool Foreclosed { get; private set; }

    /// <summary>All eight weeks of Bram's debt are paid. The Guild keeps coming (endless), but the run is won.</summary>
    public bool DebtCleared => WeeksPaid >= Guild.Weeks;

    /// <summary>The day the debt was cleared, on the eighth Guild night or paid off early (0 until it is).</summary>
    public int ClearedDay { get; private set; }

    /// <summary>The week the debt was cleared in (0 until it is).</summary>
    public int ClearedWeek => ClearedDay <= 0 ? 0 : (ClearedDay - 1) / Guild.DaysPerWeek + 1;

    /// <summary>This week's due (with the Guild's half-price deal for the map, and anything rolled over from last week).</summary>
    public int DueNow => Money.Round(DueFor(Week) * (_halfDue ? 0.5f : 1)) + Arrears;

    /// <summary>A week's due (a fifth more, under the Guild's interest).</summary>
    public int DueFor(int week) => Under(Challenges.Interest) ? Money.Round(Guild.Due(week) * Challenges.InterestRate) : Guild.Due(week);

    bool _halfDue;

    /// <summary>What's left of last week's due after Bram vouched for you, with the Guild's interest.</summary>
    public int Arrears { get; private set; }

    /// <summary>How many times Bram has vouched for you this run.</summary>
    public int GraceUsed { get; private set; }

    /// <summary>
    /// Bram can vouch for you once a run (once more with a guildsman's marker, and with Good Word; once less when he's
    /// stretched thin): a short night rolls over to next week instead of costing the shop.
    /// </summary>
    public bool GraceLeft => GraceUsed < Graces;

    public int Graces => 1 + (HasCurio("marker") ? 1 : 0) + (Knows("word") ? 1 : 0) - (Under(Challenges.Stretched) ? 1 : 0);

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
        GraceUsed++;
        WeeksPaid++;
        _halfDue = false;
        Arrears = owed;
        return owed;
    }

    /// <summary>This week's due is paid (it's the Guild's night, and they've been).</summary>
    public bool PaidThisWeek => WeeksPaid >= Week;

    /// <summary>The week of the next due the Guild will come for: this one, or the first not paid yet.</summary>
    public int NextDueWeek => PaidThisWeek ? WeeksPaid + 1 : Week;

    /// <summary>The next due the Guild will come for, and the days of trading left before it.</summary>
    public int NextDue => PaidThisWeek ? DueFor(NextDueWeek) + Arrears : DueNow;
    public int DaysToNextDue => (NextDueWeek - Week) * Guild.DaysPerWeek + DaysToDue;

    /// <summary>What pawning a displayed thing fetches tonight: the Guild's price (better for a pawnbroker's friend, better still with a pawn ticket).</summary>
    public int PawnValue(Item item) => (int)MathF.Round(SaleValue(item) * PawnRate);

    float PawnRate => HasCurio("ticket") ? Curios.TicketRate : Knows("pawn") ? Core.Talents.PawnRate : Guild.PawnRate;

    /// <summary>What a curio fetches if you sell it back: half what you paid for it.</summary>
    public int CurioValue(string key) => Math.Max(1, (_curioPaid.TryGetValue(key, out int paid) ? paid : Curios.Price(Curios.Get(key), Week)) / 2);

    /// <summary>Everything that could be pawned to make up a due.</summary>
    public int PawnableWorth => Display.Where(CanPawn).Sum(PawnValue) + _curios.Sum(CurioValue);

    /// <summary>Whether there's any way to pay tonight, pawning everything.</summary>
    public bool CanCover => Gold + PawnableWorth >= DueNow;

    /// <summary>Pawns something on display to make up the due.</summary>
    public int Pawn(Item item)
    {
        if (!CanPawn(item) || !Display.Remove(item)) return 0;
        int value = PawnValue(item);
        Gold += value;
        return value;
    }

    /// <summary>Sells a curio back (to Nim, or to the Guild on a short night) for half what you paid.</summary>
    public int SellCurio(string key)
    {
        int value = CurioValue(key);
        if (!_curios.Remove(key)) return 0;
        _curioPaid.Remove(key);
        Gold += value;
        return value;
    }

    /// <summary>Pays the week's due, if there's enough gold.</summary>
    public bool PayDues()
    {
        int due = DueNow;
        if (Gold < due) return false;
        Gold -= due;
        WeeksPaid++;
        _halfDue = false;
        Arrears = 0;
        if (DebtCleared && ClearedDay == 0) ClearedDay = Day;
        return true;
    }

    /// <summary>
    /// What's left of Bram's debt: this week's due if it's still to pay (with anything rolled over), and every week's
    /// after it, to the eighth. Nothing once it's cleared.
    /// </summary>
    public int DebtLeft
    {
        get
        {
            if (DebtCleared) return 0;
            int left = PaidThisWeek ? Arrears : DueNow;
            for (int week = Math.Max(Week, WeeksPaid) + 1; week <= Guild.Weeks; week++) left += DueFor(week);
            return left;
        }
    }

    /// <summary>You could pay off the whole debt now, and finish ahead of time.</summary>
    public bool CanPayOff => GuildIntroduced && !DebtCleared && !Foreclosed && Gold >= DebtLeft;

    /// <summary>
    /// Pays off everything that's left of the debt at once: the run is won, however early. If you keep trading, the
    /// Guild leaves you alone until its interest starts, after the eighth week.
    /// </summary>
    public int PayOff()
    {
        if (!CanPayOff) return 0;
        int paid = DebtLeft;
        Gold -= paid;
        WeeksPaid = Guild.Weeks;
        Arrears = 0;
        _halfDue = false;
        ClearedDay = Day;
        return paid;
    }

    // ---- Bram's secret stash ----

    /// <summary>
    /// How many times Bram will dip into his secret stash for you in a run (once more with a rainy-day purse, and with
    /// Rainy Day; once less when he's stretched thin).
    /// </summary>
    public const int StashTimes = 3;

    public int StashUsed { get; private set; }

    public int StashLeft => Math.Max(0, StashTimes + (HasCurio("purse") ? 1 : 0) + (Knows("rainy") ? 1 : 0) - (Under(Challenges.Stretched) ? 1 : 0) - StashUsed);

    /// <summary>
    /// Start a day with less than <c>Below</c> and Bram slips you <c>Gift</c> from his secret stash, while it lasts: a
    /// little at the stall, more for a bigger shop, whose chests cost more.
    /// </summary>
    public static (int Below, int Gift) Stash(int tier) => tier switch
    {
        1 => (20, 30),
        2 => (40, 60),
        3 => (80, 120),
        _ => (150, 250),
    };

    /// <summary>Nearly broke at the start of a day: Bram helps out, if he still can.</summary>
    void DipIntoStash()
    {
        var (below, gift) = Stash(Tier);
        if (StashLeft <= 0 || Gold >= below) return;
        StashUsed++;
        Gold += gift;
        Today.Stash = gift;
    }

    // ---- Surprises ----

    /// <summary>Things opened since the last one that came out better than its chest promised.</summary>
    public int DrySpell { get; private set; }

    /// <summary>
    /// After this many things without a surprise, each chest you buy gets a growing chance (this much per thing more)
    /// that something in it is a step better than its colour, up to a cap: a long dry spell doesn't last forever.
    /// </summary>
    public const int PityAfter = 15;
    public const float PityStep = 0.03f, PityMax = 0.5f;

    /// <summary>A lucky shell makes a dry spell end sooner.</summary>
    int PityStart => HasCurio("shell") ? Curios.ShellPityAfter : PityAfter;

    public float PityChance => Math.Clamp((DrySpell - PityStart) * PityStep, 0f, PityMax);

    /// <summary>
    /// A chest you've just bought: after a long dry spell, maybe one thing in it is a step better. Then the spell ends if
    /// anything in it beat its chest's colour, or grows by what's in it.
    /// </summary>
    void Pity(Chest chest)
    {
        var eligible = chest.Everything.Where(i => !i.Fake && !i.Broken && !i.IsSong).ToList();
        var lift = eligible.Where(i => i.Quality < Rarity.Legendary).ToArray();
        if (lift.Length > 0 && Rng.NextFloat() < PityChance)
        {
            var pick = Rng.PickOne(lift);
            chest.Improve(i => i.Id == pick.Id ? i with { Quality = i.Quality + 1 } : i);
        }
        if (chest.Everything.Any(i => !i.Fake && !i.Broken && i.Quality > chest.Quality)) DrySpell = 0;
        else DrySpell += eligible.Count;
    }

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
    public Purchase VaultPurchase()
    {
        var visit = VaultVisit();
        visit.Buy(Rng);
        var chest = visit.Chest;
        // Mara knows what's in it: the slip lists it all, magic and the false bottom included.
        var slip = new Appraisal(chest.Quality, Tier, chest.Items.Select(i => i.Category)) { Hidden = true };
        foreach (var kind in chest.Items.Where(i => i.Magic).Select(i => i.Category).Distinct())
            slip.Set(kind, slip.Count(kind), magic: true);
        return new Purchase(visit, slip);
    }

    Visit VaultVisit()
    {
        var items = new[]
        {
            new Item(Category.Coins, Rarity.Legendary, Magic: true), new Item(Category.Gems, Rarity.Legendary, Magic: true),
            new Item(Category.Arms, Rarity.Legendary), new Item(Category.Garb, Rarity.Legendary), new Item(Category.Potions, Rarity.Epic, Magic: true),
            new Item(Category.Scrolls, Rarity.Legendary), new Item(Category.Gems, Rarity.Epic), new Item(Category.Coins, Rarity.Epic),
        };
        var chest = new Chest(items, Rarity.Legendary, new Item(Category.Gems, Rarity.Legendary), LockTiers.All[3], gilded: true);
        return new Visit(Story.VaultSeller, chest, Array.Empty<Category>(), Trade.Fixed, 0, 1, "The Crown Chest.");
    }

    // ---- Curios ----

    readonly List<string> _curios = new();

    /// <summary>What you paid for each curio: Nim (or the Guild) gives half of it back.</summary>
    readonly Dictionary<string, int> _curioPaid = new();

    public IReadOnlyList<string> CurioKeys => _curios;
    public IEnumerable<CurioDef> OwnedCurios => _curios.Select(Curios.Get);
    public bool HasCurio(string key) => _curios.Contains(key);
    public int CurioSlots => Curios.Slots + Upgrades.All.Where(u => Has(u.Key)).Sum(u => u.CurioSlots) + (Knows("curio") ? 1 : 0);
    public bool CuriosFull => _curios.Count >= CurioSlots;

    public bool BuyCurio(CurioDef curio, int price)
    {
        if (CuriosFull || HasCurio(curio.Key) || Gold < price) return false;
        Gold -= price;
        _curios.Add(curio.Key);
        _curioPaid[curio.Key] = price;
        Today.CurioSpent += price;
        if (Keepsake) KeepsakeTaken = true;
        return true;
    }

    /// <summary>The day of the week Nim comes by this week (0: not this week).</summary>
    public int PeddlerDay { get; private set; }

    public bool PeddlerToday => PeddlerDay > 0 && DayOfWeek == PeddlerDay;

    /// <summary>What Nim has today.</summary>
    public List<CurioDef> PeddlerStock { get; } = new();

    /// <summary>
    /// What a curio's luck did to a chest you've just bought: which curio, and how many things it changed. The first that
    /// does anything is the one the opening announces.
    /// </summary>
    (string Curio, int Changed) Luck(Chest chest)
    {
        if (chest.Sealed || chest.Items.Count == 0) return (null, 0);
        string curio = null;
        int changed = 0;
        void Apply(string key, Func<Item, Item> change, bool song = false)
        {
            if (song ? !Shows(key) : !HasCurio(key)) return;
            int n = 0;
            chest.Improve(i =>
            {
                if (i.Fake || i.Broken) return i;
                var next = change(i);
                if (next.Quality != i.Quality) n++;
                return next;
            });
            if (n == 0 || curio != null) return;
            curio = key;
            changed = n;
        }

        // The day's first few chests: two tries at each thing's quality. The week's first: a step better all round.
        if (Today.Bought <= Curios.CoinChests)
            Apply("coin", i => Categories.RollQuality(Rng, chest.Quality) is var again && again > i.Quality ? i with { Quality = again } : i);
        if (HasCurio("clover") && CloverWeek != Week)
        {
            CloverWeek = Week;
            Apply("clover", i => i.Quality < Rarity.Legendary ? i with { Quality = i.Quality + 1 } : i);
        }
        // A gambler's luck: some of what came out as promised swings, better or (more often) worse.
        Apply("token", i => i.Quality == chest.Quality && Rng.NextFloat() < Curios.TokenSwing ? i with { Quality = Categories.Swing(Rng, i.Quality) } : i);
        // A smith or a hatter mends what came out worse than the chest promised.
        Apply("gauntlet", i => i.Category == Category.Arms && i.Quality < chest.Quality ? i with { Quality = chest.Quality } : i);
        Apply("cap", i => i.Category == Category.Garb && i.Quality < chest.Quality ? i with { Quality = chest.Quality } : i);
        // Heartsongs on display: the Fey Chimes' luck.
        Apply(Heartsongs.Chimes, i => i.Quality == chest.Quality && i.Quality < Rarity.Legendary && Rng.NextFloat() < Heartsongs.ChimesLuck
            ? i with { Quality = i.Quality + 1 } : i, song: true);
        // The Imp's Ember tempers what it warms (nothing comes out worse than promised), and burns the lie out of demon
        // gold, paste and sand: every fake comes out real.
        Apply(Heartsongs.Ember, i => i.Quality < chest.Quality ? i with { Quality = chest.Quality } : i, song: true);
        if (Shows(Heartsongs.Ember) && chest.Items.Count(i => i.Fake) is var fakes && fakes > 0)
        {
            chest.Improve(i => i.Fake ? i with { Fake = false } : i);
            if (curio == null) (curio, changed) = (Heartsongs.Ember, fakes);
        }
        return (curio, changed);
    }

    /// <summary>The week a four-leaf clover last brought luck: once a week, to the first chest you open.</summary>
    public int CloverWeek { get; private set; }

    /// <summary>A cursed crescent: everything comes out exactly as its chest promised (after any other luck).</summary>
    void Steady(Chest chest)
    {
        if (!HasCurio("crescent") || chest.Sealed) return;
        chest.Improve(i => i.Fake || i.Broken ? i : i with { Quality = chest.Quality });
    }

    /// <summary>What a pick grade does to a thing's price: forcing with a burglar's knife scratches nothing.</summary>
    public float GradeMultiplier(PickGrade grade) => grade == PickGrade.Forced && HasCurio("knife") ? 1f : Picks.Multiplier(grade);

    /// <summary>How much less likely glass is to break when you force a lock (a burglar's knife or the Imp's Ember: it never does).</summary>
    public float ForceSturdier => HasCurio("knife") || Shows(Heartsongs.Ember) ? 1f : Sturdier;

    /// <summary>What today's event does to the price of a thing of this kind.</summary>
    float EventSales(Category kind) => (EventIs("festival") ? Events.FestivalSales : 1) *
                                       (EventIs("buyer") && kind == BuyerKind ? Events.BuyerSales : 1);

    /// <summary>What a fake sells for: nothing much, unless a jeweller's loupe knows good paste from a bag of sand.</summary>
    float FakeValue(Item item) => HasCurio("loupe") && item.Category is Category.Coins or Category.Gems
        ? Categories.Value(item.Category, item.Quality, false) * Curios.LoupeFakes
        : item.Value;

    // ---- Commissions ----

    /// <summary>This week's commissions (they lapse when the Guild comes).</summary>
    public List<Commission> Commissions { get; } = new();

    /// <summary>A commission this thing would do for, if any.</summary>
    public Commission WantedBy(Item item) => Commissions.FirstOrDefault(c => Takes(c, item));

    /// <summary>
    /// Whether a commission would take this thing: what it asks for, or (with a letter of introduction, for the regulars)
    /// a quality lower.
    /// </summary>
    public bool Takes(Commission c, Item item) => c.Wants(item, LetterLeeway(c));

    /// <summary>How many qualities lower than asked a commission will take.</summary>
    public int LetterLeeway(Commission c) => HasCurio("letter") && !c.Story ? 1 : 0;

    public int CommissionReward(Commission c) => Shows(Heartsongs.Lark) ? (int)MathF.Round(c.Reward * Heartsongs.LarkPay) : c.Reward;

    /// <summary>
    /// Hands something over for a commission instead of selling it (giving up <paramref name="forgo"/>, its sale price,
    /// if that was already counted). Returns what it paid (once it's complete; 0 for a lot still short).
    /// </summary>
    public int Deliver(Item item, Commission commission, int forgo = 0)
    {
        if (!Takes(commission, item)) return 0;
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
        _snubbed.Clear();
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
            Queue.Insert(Rng.Next(1, Queue.Count + 1), Sing(Events.StrangerVisit(Rng, Tier)));
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

    /// <summary>
    /// What comes in overnight: interest on savings and visitors' coins (curios), and the door money for Isolde's
    /// collection. Paid at the end of the day.
    /// </summary>
    void Overnight()
    {
        if (Admission > 0)
        {
            Today.Admission = Admission;
            Gold += Today.Admission;
        }
        if (HasCurio("jar"))
        {
            int interest = Math.Min((int)(Gold * Curios.InterestRate), Curios.InterestPerTier * ShopTiers.Scale(Tier));
            Gold += interest;
            Today.Curios += interest;
        }
        if (HasCurio("book"))
        {
            int coins = Curios.BookPerPoint * ShopTiers.Scale(Tier) * Exhibition;
            Gold += coins;
            Today.Curios += coins;
        }
    }

    /// <summary>A new find in the journal: a collector's catalogue pays for it.</summary>
    void Catalogue()
    {
        if (!HasCurio("catalogue")) return;
        int fee = Curios.CataloguePerTier * ShopTiers.Scale(Tier);
        Gold += fee;
        Today.Curios += fee;
    }
}
