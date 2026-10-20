using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFall.Core;

/// <summary>The shop's size: who comes in, how many, and what they bring.</summary>
public static class ShopTiers
{
    public const int Max = 4;

    public static string Name(int tier) => tier switch
    {
        1 => "Roadside Stall",
        2 => "Village Shop",
        3 => "Town Shop",
        _ => "The Emporium",
    };

    /// <summary>Gold to keep for a day's chests at a shop of this tier: Bram's advice before spending on the shop.</summary>
    public static int Working(int tier) => tier switch
    {
        1 => 20,
        2 => 30,
        3 => 100,
        _ => 250,
    };
}

/// <summary>A chest bought today, with the slip the player bought it on. It's opened straight away.</summary>
public sealed class Purchase
{
    public Visit Visit { get; }
    public Appraisal Slip { get; }
    public Chest Chest => Visit.Chest;
    public int Paid => Visit.PricePaid;

    /// <summary>How the lock came open (Clean if there was none).</summary>
    public PickGrade Grade { get; set; } = PickGrade.Clean;

    /// <summary>A curio whose luck improved things inside when you bought it, and how many.</summary>
    public string Luck { get; init; }
    public int Improved { get; init; }

    public Purchase(Visit visit, Appraisal slip, string luck = null, int improved = 0)
    {
        Visit = visit;
        Slip = slip;
        Luck = luck;
        Improved = improved;
    }
}

/// <summary>How the day went, for the end-of-day summary.</summary>
public sealed class DayStats
{
    public int Bought, Passed, Spent, Earned, PerfectReads, NewFinds;

    /// <summary>Things handed over for commissions, and what completed commissions paid.</summary>
    public int Delivered, Commissions;

    /// <summary>What Bram slipped you from his secret stash this morning.</summary>
    public int Stash;

    /// <summary>What curios brought in (a catalogue's fees, interest, visitors' coins) and what curios cost.</summary>
    public int Curios, CurioSpent;

    /// <summary>What Isolde's visitors paid at the door tonight.</summary>
    public int Admission;

    public int Profit => Earned - Spent;
}

public sealed partial class GameState
{
    public const int StartingGold = 40;

    /// <summary>Display space you start with: a shelf behind the counter, with room for one thing.</summary>
    public const int ShelfSlots = 1;

    /// <summary>Exhibition points per extra customer a day (up to two).</summary>
    public const int ExhibitionPerCustomer = 10;

    /// <summary>Past this, an exhibition draws no more customers and no better chests.</summary>
    public const int ExhibitionCap = 40;

    /// <summary>Bram lets you spend on the shop once the stall has earned this much, all told.</summary>
    public const int UpgradesAt = 120;

    public int Seed { get; }
    public int Gold { get; set; } = StartingGold;
    public int Day { get; private set; }
    public int Tier { get; private set; } = 1;
    public Random Rng { get; private set; } = new();

    /// <summary>Whether Bram's tips are shown (and the first day is the scripted one).</summary>
    public bool Guided { get; set; } = true;

    public HashSet<string> HintsSeen { get; } = new();

    /// <summary>Regulars you've met: their habits go in the ledger.</summary>
    public HashSet<string> Met { get; } = new();

    /// <summary>Every kind of thing you've found, by its collection name.</summary>
    public HashSet<string> Found { get; } = new();

    /// <summary>Collection entries you've also found enchanted (the journal stars them).</summary>
    public HashSet<string> FoundEnchanted { get; } = new();

    /// <summary>What you kept to show off in the shop instead of selling.</summary>
    public List<Item> Display { get; } = new();

    /// <summary>Sellers whose first-visit lesson has been given.</summary>
    public HashSet<string> Tutored { get; } = new();

    // The shop opens up a step at a time: renovations once the stall has earned its keep, a shelf to display things
    // with the village shop, synergies with the town shop, where people start to know you by what you collect.

    /// <summary>What the chests you've bought and opened have brought in, all told.</summary>
    public int TotalEarned { get; private set; }

    /// <summary>Renovations and tools are on offer: the stall has earned <see cref="UpgradesAt"/>.</summary>
    public bool UpgradesOpen { get; private set; }

    /// <summary>Keeping things for display: from the village shop, which has a shelf (Isolde brings her own).</summary>
    public bool DisplayOpen => Tier >= 2 || Is(Characters.Isolde);

    /// <summary>Synergies: from the town shop (the village shop, with an early name).</summary>
    public bool SynergiesOpen => Tier >= (Knows("early") ? 2 : 3);

    public int DisplaySlots => DisplayOpen ? (Is(Characters.Isolde) ? Characters.IsoldeShelf : ShelfSlots) + Upgrades.All.Where(u => Has(u.Key)).Sum(u => u.Slots) +
                                             (HasCurio("dome") ? 1 : 0) : 0;
    public bool DisplayFull => Display.Count >= DisplaySlots;

    /// <summary>The exhibition: what your displayed things are worth to visitors' eyes, not their purses.</summary>
    public int Exhibition => (int)MathF.Round(Display.Sum(i => i.Prestige) * (1 + Synergies.ShowBonus(Active("tailor"))));

    // The collection journal: everything acquired, by kind and quality, and the feats synergies ask for.
    readonly int[,] _acquired = new int[Categories.All.Length, 5];

    /// <summary>How many things of this kind you've acquired at this quality or better (fakes and broken glass don't count).</summary>
    public int Acquired(Category kind, Rarity atLeast)
    {
        int n = 0;
        for (int q = (int)atLeast; q < 5; q++) n += _acquired[(int)kind, q];
        return n;
    }

    public int MagicFound { get; private set; }
    public int PerfectStreak { get; private set; }
    public int BestStreak { get; private set; }

    /// <summary>The synergies in effect, no more than there are slots for.</summary>
    public HashSet<string> ActiveSynergies { get; } = new();

    /// <summary>One synergy at a time, more with a guild charter and a royal seal.</summary>
    public int SynergySlots => 1 + Upgrades.All.Where(u => Has(u.Key)).Sum(u => u.SynergySlots) + (Knows("favour") ? 1 : 0);

    public int SynergyLevel(string key) => Synergies.Get(key).Level(this);

    /// <summary>A synergy's level if it's active, else 0.</summary>
    public int Active(string key) => SynergiesOpen && ActiveSynergies.Contains(key) ? SynergyLevel(key) : 0;

    /// <summary>Turns a reached synergy on (if there's a free slot) or off. Returns whether it's active now.</summary>
    public bool ToggleSynergy(string key)
    {
        if (ActiveSynergies.Remove(key) || !SynergiesOpen) return false;
        if (ActiveSynergies.Count >= SynergySlots || SynergyLevel(key) == 0) return false;
        ActiveSynergies.Add(key);
        return true;
    }

    /// <summary>What the exhibition, synergies, the Guild's terms and who's at the counter do to the chests people bring.</summary>
    public Boosts Boosts => new(BetterChests + (EventIs("storm") ? 1 : 0),
        Synergies.HiddenBonus(Active("archivist")) + (HasCurio("hook") ? Curios.HookHidden : 0),
        Synergies.MagicBonus(Active("arcane")) + (HasCurio("draught") ? Curios.DraughtMagic : 0),
        EventIs("wreck") ? Events.WreckPairs : 0, Synergies.MoneyKinds(Active("jewelry")),
        Under(Challenges.SharpSellers) ? Challenges.SharpPrices : 1, Under(Challenges.Forgers) ? Challenges.SlugChance : 0, HagglesAll);

    /// <summary>How much less likely glass is to break (a vintner's packing).</summary>
    public float Sturdier => Synergies.Sturdier(Active("vintner"));

    /// <summary>When you drop a chest: in a padded crate, glass never breaks.</summary>
    public float DropSturdier => HasCurio("crate") ? 1f : Sturdier;

    /// <summary>A lock as you'll pick it: one pin fewer with a locksmith's oil.</summary>
    public LockTier Oiled(LockTier lockTier) => HasCurio("oil") && lockTier.Pins > 1 ? lockTier with { Pins = lockTier.Pins - 1 } : lockTier;

    /// <summary>
    /// How long a seller gives you, in seconds (0: all day): an impatient one's patience, or everyone's when the town is
    /// in a hurry; twice as long with a pocket watch.
    /// </summary>
    public float Patience(SellerDef seller)
    {
        float patience = seller.Patience;
        if (Under(Challenges.Hurried)) patience = patience > 0 ? Math.Min(patience, Challenges.HurriedPatience) : Challenges.HurriedPatience;
        return patience * (HasCurio("watch") ? Curios.WatchPatience : 1);
    }

    /// <summary>In kid gloves, a seller who won't stand for a hard shake, a drop or the magnet doesn't notice.</summary>
    public bool KidGloves => HasCurio("gloves");

    /// <summary>
    /// What a thing fetches when you sell it: its value, how cleanly the lock came open, and today's event. Curios and
    /// synergies never add a share to it.
    /// </summary>
    public int SaleValue(Item item, PickGrade grade = PickGrade.Clean) =>
        (int)MathF.Round((item.Fake ? FakeValue(item) : item.Value) * GradeMultiplier(grade) * SaleMultiplier(item.Category));

    /// <summary>What today's event does to the price of a thing of this kind (1 on most days).</summary>
    public float SaleMultiplier(Category kind) => EventSales(kind);

    /// <summary>A keen eye's valuer's fee for a perfect read: a few coins per level and shop tier, whatever the chest.</summary>
    public int KeenFee(int held) => Synergies.KeenFee(Active("keen")) * Tier;

    /// <summary>Notes something you opened in the journal.</summary>
    public void Acquire(Item item)
    {
        if (item.Fake || item.Broken) return;
        _acquired[(int)item.Category, (int)item.Quality]++;
        if (item.Magic) MagicFound++;
    }

    /// <summary>Notes whether a bought chest's slip was perfect, for the streak.</summary>
    public void RecordRead(bool perfect)
    {
        PerfectStreak = perfect ? PerfectStreak + 1 : 0;
        BestStreak = Math.Max(BestStreak, PerfectStreak);
    }

    /// <summary>People come to see the exhibition: one more customer a day per so many points, up to two.</summary>
    public int ExhibitionCustomers => Math.Min(2, Exhibition / ExhibitionPerCustomer);

    /// <summary>And a fine exhibition draws a better class of customer: the chance a chest is one colour better.</summary>
    public float BetterChests => MathF.Min(ExhibitionCap, Exhibition) * 0.01f;

    readonly HashSet<string> _owned = new();

    public List<Visit> Queue { get; } = new();
    public Visit Current { get; private set; }
    public DayStats Today { get; private set; } = new();

    /// <summary>The purchase being opened.</summary>
    public Purchase Opening { get; set; }

    public GameState(int seed)
    {
        Seed = seed;
    }

    public bool Has(string upgrade) => _owned.Contains(upgrade);

    public int CustomersPerDay => Math.Max(2, 3 + Tier + Upgrades.All.Where(u => Has(u.Key)).Sum(u => u.Customers) + ExhibitionCustomers +
                                            (HasCurio("lantern") && DuesTonight ? 1 : 0) + EventCustomers - (Under(Challenges.ThinCrowds) && Tier == 1 ? 1 : 0));

    public bool CanBuy(Upgrade upgrade) => UpgradesOpen && Gold >= Cost(upgrade) && Upgrades.Available(this).Contains(upgrade);

    public void BuyUpgrade(Upgrade upgrade)
    {
        if (!CanBuy(upgrade)) return;
        Gold -= Cost(upgrade);
        _owned.Add(upgrade.Key);
        if (upgrade.SetsTier > 0) Tier = upgrade.SetsTier;
    }

    /// <summary>Hands out a hint key once. Returns false if it's been shown before or tips are off.</summary>
    public bool FirstTime(string hint) => Guided && HintsSeen.Add(hint);

    /// <summary>What carries over to tomorrow, to be written to a save file (taken between days).</summary>
    public SaveData Save()
    {
        var data = new SaveData
        {
            SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            Seed = Seed, Gold = Gold, Day = Day, Tier = Tier, Guided = Guided, TotalEarned = TotalEarned,
            Character = Character, Challenge = Challenge, Talents = Talents.ToList(), Snubbed = _snubbed.ToList(), MaraUsed = MaraUsed,
            KeepsakeTaken = KeepsakeTaken,
            UpgradesOpen = UpgradesOpen, MagicFound = MagicFound, PerfectStreak = PerfectStreak, BestStreak = BestStreak,
            Owned = _owned.ToList(), HintsSeen = HintsSeen.ToList(), Met = Met.ToList(), Found = Found.ToList(),
            FoundEnchanted = FoundEnchanted.ToList(), Tutored = Tutored.ToList(), ActiveSynergies = ActiveSynergies.ToList(),
            Display = Display.Select(i => new SavedItem { Category = i.Category, Quality = i.Quality, Magic = i.Magic }).ToList(),
            WeeksPaid = WeeksPaid, Arrears = Arrears, GraceUsed = GraceUsed > 0, GraceCount = GraceUsed, BramPiece = BramPiece, StashUsed = StashUsed, ClearedDay = ClearedDay, CloverWeek = CloverWeek,
            CurioPaid = _curios.Select(key => _curioPaid.GetValueOrDefault(key)).ToList(),
            DrySpell = DrySpell, HoodPieces = HoodPieces, MapGiven = MapGiven, VaultOpened = VaultOpened,
            Curios = _curios.ToList(), EventsSeen = _eventsSeen.ToList(), WeekEvent = WeekEvent, EventDay = EventDay,
            BuyerKind = BuyerKind, PeddlerDay = PeddlerDay,
            Commissions = Commissions.Select(c => new SavedCommission
            {
                Patron = c.Patron, Kind = c.Kind, Category = c.Category, Quality = c.Quality, Needed = c.Needed, Delivered = c.Delivered,
                Reward = c.Reward, Story = c.Story,
            }).ToList(),
        };
        foreach (int count in _acquired) data.Acquired.Add(count);
        return data;
    }

    /// <summary>A shop as it was saved: ready to start the next day.</summary>
    public static GameState Load(SaveData data)
    {
        var state = new GameState(data.Seed)
        {
            Character = Characters.Exists(data.Character) ? data.Character : Characters.Kit,
            Challenge = Math.Clamp(data.Challenge, 0, Challenges.Max),
            Talents = new HashSet<string>(data.Talents.Where(Core.Talents.Exists)),
            MaraUsed = data.MaraUsed, KeepsakeTaken = data.KeepsakeTaken,
            Gold = data.Gold, Day = data.Day, Tier = Math.Clamp(data.Tier, 1, ShopTiers.Max), Guided = data.Guided,
            TotalEarned = data.TotalEarned, UpgradesOpen = data.UpgradesOpen, MagicFound = data.MagicFound,
            PerfectStreak = data.PerfectStreak, BestStreak = data.BestStreak,
            WeeksPaid = data.WeeksPaid, Arrears = data.Arrears, GraceUsed = Math.Max(data.GraceCount, data.GraceUsed ? 1 : 0), BramPiece = data.BramPiece,
            StashUsed = Math.Max(0, data.StashUsed), ClearedDay = data.ClearedDay, CloverWeek = data.CloverWeek, DrySpell = data.DrySpell, HoodPieces = data.HoodPieces,
            MapGiven = data.MapGiven,
            VaultOpened = data.VaultOpened, WeekEvent = data.WeekEvent, EventDay = data.EventDay, BuyerKind = data.BuyerKind,
            PeddlerDay = data.PeddlerDay,
        };
        // Curios from an older version that are gone now are simply gone; the rest keep what you paid (or half today's price).
        for (int i = 0; i < data.Curios.Count; i++)
        {
            string key = data.Curios[i];
            if (!Curios.Exists(key) || state._curios.Contains(key)) continue;
            state._curios.Add(key);
            if (i < data.CurioPaid.Count && data.CurioPaid[i] > 0) state._curioPaid[key] = data.CurioPaid[i];
        }
        state._eventsSeen.AddRange(data.EventsSeen);
        state._snubbed.UnionWith(data.Snubbed);
        state.Commissions.AddRange(data.Commissions.Select(c => new Commission
        {
            Patron = c.Patron, Kind = c.Kind, Category = c.Category, Quality = c.Quality, Needed = c.Needed, Delivered = c.Delivered,
            Reward = c.Reward, Story = c.Story,
        }));
        state._owned.UnionWith(data.Owned);
        state.HintsSeen.UnionWith(data.HintsSeen);
        state.Met.UnionWith(data.Met);
        state.Found.UnionWith(data.Found);
        state.FoundEnchanted.UnionWith(data.FoundEnchanted);
        state.Tutored.UnionWith(data.Tutored);
        state.ActiveSynergies.UnionWith(data.ActiveSynergies.Where(key => Synergies.All.Any(s => s.Key == key)));
        state.Display.AddRange(data.Display.Select(i => new Item(i.Category, i.Quality, i.Magic)));
        int kinds = state._acquired.GetLength(0), qualities = state._acquired.GetLength(1);
        for (int i = 0; i < Math.Min(data.Acquired.Count, kinds * qualities); i++)
            state._acquired[i / qualities, i % qualities] = data.Acquired[i];
        return state;
    }

    /// <summary>For debugging and screenshots: jump ahead with the tools a player would have by then.</summary>
    public void SkipTo(int tier, int gold)
    {
        foreach (var upgrade in Upgrades.All.Where(u => u.IsRenovation ? u.SetsTier <= tier : u.MinTier < tier))
            _owned.Add(upgrade.Key);
        Tier = tier;
        Gold = gold;
        UpgradesOpen = true;
    }

    public void StartDay()
    {
        Day++;
        Rng = new Random(unchecked(Seed * 7919 + Day));
        Queue.Clear();
        Current = null;
        Today = new DayStats();
        PeddlerRerolled = false;
        DipIntoStash();
        if (DayOfWeek == 1) StartWeek();
        PeddlerStock.Clear();
        if (PeddlerToday) PeddlerStock.AddRange(Curios.Stock(new Random(unchecked(Seed * 6151 + Day)), this, PeddlerCount));

        if (Day == 1 && GivesLessons && Tier == 1)
        {
            Queue.AddRange(FirstDay(Rng));
            return;
        }

        // Each renovation brings its own crowd; the old regulars still drop by now and then. Nobody comes twice in a
        // day unless everyone else already has. A smithing name draws smiths and soldiers. With tips on, no more than
        // a few newcomers (each a lesson) a day: the rest of a new shop's crowd comes in over the next days. Anyone who
        // walked out on Rosa this week stays away, while there's anyone else.
        var sellers = Sellers.All.Where(s => s.Tier <= Tier).ToArray();
        if (sellers.Count(s => !_snubbed.Contains(s.Key)) >= 2) sellers = sellers.Where(s => !_snubbed.Contains(s.Key)).ToArray();
        float smiths = Synergies.SmithVisits(Active("smith"));
        while (Queue.Count < CustomersPerDay)
        {
            var fresh = sellers.Where(s => Queue.All(v => v.Seller != s)).ToArray();
            var pool = fresh.Length > 0 ? fresh : sellers.Where(s => Queue[^1].Seller != s).ToArray();
            if (Queue.Count(v => v.Lesson != null) >= LessonsPerDay)
            {
                var known = pool.Where(s => !Teaches(s)).ToArray();
                if (known.Length > 0) pool = known;
            }
            var seller = Rng.PickWeighted(pool, s => (s.Tier == Tier ? 3f : s.Tier == Tier - 1 ? 0.6f : 0.15f) * (s.Metalworker ? smiths : 1f));
            Queue.Add(NewVisit(seller));
        }
        StageEvent();
    }

    /// <summary>With tips on, at most this many sellers' lessons in one day.</summary>
    public const int LessonsPerDay = 3;

    /// <summary>Their next visit would be a lesson.</summary>
    bool Teaches(SellerDef seller) => GivesLessons && Lessons.For(seller.Key) != null && !Tutored.Contains(seller.Key);

    /// <summary>A seller's visit: the first time (with tips on), the lesson that introduces their habit.</summary>
    Visit NewVisit(SellerDef seller)
    {
        var lesson = Lessons.For(seller.Key);
        if (GivesLessons && lesson != null && Tutored.Add(seller.Key))
        {
            var visit = lesson.Make(Rng, Tier);
            visit.Lesson = lesson;
            return visit;
        }
        var fresh = Market.CreateVisit(Rng, seller, Tier, Boosts);
        // A merchant's signet (or Rosa's charm): hagglers hear out an offer that's too low, instead of storming off.
        fresh.HearsOut = HasCurio("signet") || HagglesAll;
        return fresh;
    }

    /// <summary>
    /// The first day teaches one thing per customer: a gentle shake and the slip (Tam never shook his chest), a hard
    /// shake (Penn shook hers too gently to hear the coat), a gentle shake under a clank (Hob shook hard and the
    /// knife drowned out the coins), and passing (Mags heard everything there was).
    /// </summary>
    static IEnumerable<Visit> FirstDay(Random rng)
    {
        Visit Scripted(string seller, int ask, params Category[] kinds) =>
            Market.Price(rng, Sellers.Get(seller), new Chest(kinds.Select(k => new Item(k, Rarity.Common))), 1, ask, Trade.Fixed);

        // Tam names a price today, so the first lesson is only about listening; from tomorrow he asks you to name one.
        yield return Market.Price(rng, Sellers.Get("tam"), new Chest(new[] { new Item(Category.Coins, Rarity.Common) }), 1, 4, Trade.Fixed,
            "Found it by the river. I didn't shake it, it might break! Is {price} okay?");
        yield return Scripted("penn", 3, Category.Cloth);
        yield return Scripted("hob", 7, Category.Coins, Category.Metal);
        yield return Scripted("mags", 7, Category.Metal);
    }

    /// <summary>Calls in the next customer, if anyone is waiting.</summary>
    public bool NextCustomer()
    {
        if (Current != null) NoteWalkout(Current);
        Current = null;
        if (Queue.Count == 0) return false;
        Current = Queue[0];
        Queue.RemoveAt(0);
        return true;
    }

    /// <summary>Records meeting a seller. True the first time.</summary>
    public bool Meet(SellerDef seller) => Met.Add(seller.Key);

    public Purchase Buy(Visit visit, Appraisal slip)
    {
        Gold -= visit.PricePaid;
        Today.Bought++;
        Today.Spent += visit.PricePaid;
        var (luck, changed) = Luck(visit.Chest);
        Pity(visit.Chest);
        Steady(visit.Chest);
        return new Purchase(visit, slip, luck, changed);
    }

    /// <summary>Notes a find in the collection and the journal. True the first time this thing turns up.</summary>
    public bool Collect(Item item)
    {
        Acquire(item);
        if (item.Magic && !item.Broken && !item.Fake) FoundEnchanted.Add(item.Entry);
        if (item.Broken || !Found.Add(item.Entry)) return false;
        Today.NewFinds++;
        Catalogue();
        return true;
    }

    /// <summary>Puts something on display instead of selling it: you give up its price.</summary>
    public bool Keep(Item item, int value)
    {
        if (!DisplayOpen || !item.Displayable || DisplayFull || Gold < value) return false;
        Display.Add(item);
        Gold -= value;
        Today.Earned -= value;
        return true;
    }

    /// <summary>Takes something off display and sells it.</summary>
    public void SellFromDisplay(Item item)
    {
        if (!Display.Remove(item)) return;
        Gold += SaleValue(item);
    }

    public void Pass() => Today.Passed++;

    public void Earn(int amount)
    {
        Gold += amount;
        Today.Earned += amount;
    }

    /// <summary>
    /// Closes the day: adds what its chests brought in to the total (renovations open once it's enough) and what curios
    /// bring in overnight.
    /// </summary>
    public void EndDay()
    {
        TotalEarned += Today.Earned;
        if (TotalEarned >= UpgradesAt) UpgradesOpen = true;
        Overnight();
    }
}
