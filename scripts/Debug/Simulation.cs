using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report: run with `godot --headless --path . -- --sim [--n=300]`.
/// Plays whole campaigns at the counter with simple player styles. The design only works if judgment
/// (listening, weighing, looking, reading sellers) clearly beats every mindless strategy, each clue adds something,
/// and knowing more of the ledger pays.
/// </summary>
public static class Simulation
{
    const int Days = 15;

    public static void Run(int campaigns)
    {
        GD.Print($"=== LootFall balance report ({campaigns} campaigns x {Days} days) ===");
        PrintDistribution();

        GD.Print($"\nStyle                       survive  final gold  profit/day  buys/day  bought cons  walkouts/day  unseen/day  ledger/{ItemCatalog.All.Length}");
        foreach (var (name, make) in Policies)
        {
            bool knowsAll = name.StartsWith("expert");
            var results = Enumerable.Range(0, campaigns).Select(i => Campaign(make(), 1000 + i, knowsAll)).ToList();
            GD.Print($"{name,-27} {100f * results.Count(r => r.Survived) / campaigns,6:0}%  {results.Average(r => r.FinalGold),10:0}  " +
                     $"{results.Average(r => r.ProfitPerDay),10:0}  {results.Average(r => r.BuysPerDay),8:0.0}  " +
                     $"{results.Average(r => r.ConsBought),11:0.0}  {results.Average(r => r.WalkoutsPerDay),12:0.0}  " +
                     $"{results.Average(r => r.UnseenPerDay),10:0.0}  {results.Average(r => r.Known),9:0}");
        }
    }

    static readonly (string, Func<Action<GameState, Visit, SimLog>>)[] Policies =
    {
        ("pay the ask", () => PayAsk),
        ("lowballer", () => Lowball),
        ("blind haggler", () => (s, v, log) => Haggle(s, v, log, null)),
        ("ears only", () => Haggler(new Player { Ears = true })),
        ("ears and scale", () => Haggler(new Player { Ears = true, Scale = true })),
        ("full inspector", () => Haggler(new Player { Ears = true, Scale = true, Lantern = true })),
        ("full inspector, no haggle", () => Payer(new Player { Ears = true, Scale = true, Lantern = true })),
        ("expert (whole ledger)", () => Haggler(new Player { Ears = true, Scale = true, Lantern = true, KnowsAll = true })),
    };

    /// <summary>What a simulated player does at the counter. Shaking and knocking are free; the lantern costs time.</summary>
    sealed class Player
    {
        public bool Ears, Scale, Lantern, KnowsAll;
    }

    sealed class SimLog
    {
        public int Walkouts;
        public int ConsBought;
        public int Unseen;
    }

    sealed record Result(bool Survived, int FinalGold, float ProfitPerDay, float BuysPerDay, int ConsBought, float WalkoutsPerDay,
        float UnseenPerDay, int Known);

    static Action<GameState, Visit, SimLog> Haggler(Player p) => (s, v, log) => Haggle(s, v, log, p);
    static Action<GameState, Visit, SimLog> Payer(Player p) => (s, v, log) => InspectThenPay(s, v, p);

    static Result Campaign(Action<GameState, Visit, SimLog> policy, int seed, bool knowsAll)
    {
        var state = new GameState(seed);
        if (knowsAll) foreach (var item in ItemCatalog.All) state.Ledger.Learn(item);
        var log = new SimLog();
        var rng = new Random(seed);
        int buys = 0, day = 0;
        for (day = 1; day <= Days; day++)
        {
            state.StartDay();
            while (state.NextCustomer())
            {
                policy(state, state.Current, log);
                if (state.Current.Outcome == VisitOutcome.Bought)
                {
                    state.Buy(state.Current);
                    buys++;
                    if (state.Current.Chest.Tampered) log.ConsBought++;
                }
                if (state.Current.Outcome == VisitOutcome.Walked) log.Walkouts++;
            }
            log.Unseen += state.Queue.Count;

            // Night: open everything with a typical skill spread at the lock, and write down what came out.
            foreach (var p in state.Purchases)
            {
                float r = rng.NextFloat();
                var grade = r < 0.15f ? PickGrade.Masterful : r < 0.9f ? PickGrade.Clean : PickGrade.Forced;
                var haul = Loot.Resolve(p.Chest, grade);
                state.Gold += haul.Sum(i => i.Value);
                foreach (var item in haul) state.Ledger.Learn(item.Def);
            }
            if (!state.PayRentIfDue(out _)) break;
        }
        int lived = Math.Min(day, Days);
        return new Result(day > Days, state.Gold, (state.Gold - GameState.StartingGold) / (float)lived,
            buys / (float)lived, log.ConsBought, log.Walkouts / (float)lived, log.Unseen / (float)lived, state.Ledger.Count);
    }

    static void PayAsk(GameState s, Visit v, SimLog log)
    {
        if (s.Gold >= v.Ask) v.PayAsk(s.Rng);
        else v.Pass(s.Rng);
    }

    static void Lowball(GameState s, Visit v, SimLog log)
    {
        foreach (float fraction in new[] { 0.35f, 0.45f })
        {
            if (!s.CanAfford(ShopClock.Offer)) break;
            s.Spend(ShopClock.Offer);
            var response = v.Offer(Math.Min(s.Gold, (int)(v.OpeningAsk * fraction)), s.Rng);
            if (v.Outcome != VisitOutcome.Haggling) return;
            if (response.Kind == ResponseKind.Counter && v.Ask <= v.OpeningAsk * 0.55f && v.Ask <= s.Gold)
            {
                v.PayAsk(s.Rng);
                return;
            }
        }
        if (v.Outcome == VisitOutcome.Haggling) v.Pass(s.Rng);
    }

    const float UnknownGuess = 120f;
    const float LanternLook = 4f; // seconds at the keyhole

    /// <summary>
    /// What a player can work out with the clues they use and the ledger they've built. Every loose item is heard,
    /// but a sound only means something if it's in the ledger; sound-alikes need the scale (weight) or a look
    /// (glow, appearance); unexplained weight plus a solid knock means stones; a hollow knock means a bonus.
    /// Returns null when the player catches a con.
    /// </summary>
    static float? Judge(GameState s, Visit v, Player p)
    {
        if (p is not { Ears: true }) return s.AppraiserFor(v.Claimed).Prior.Median;
        var chest = v.Chest;
        var ledger = s.Ledger;

        // A seasoned player looks inside when the story doesn't match the chest, or when they hear a sound-alike
        // that only a look can settle.
        bool colorLies = v.Claimed.Skin != chest.Provenance.Skin;
        bool needsLook = chest.Items.Any(i => ItemCatalog.TellFor(i.Def) is Tell.Look or Tell.Glow);
        bool looked = p.Lantern && (colorLies || needsLook) && s.CanAfford((int)(LanternLook * ShopClock.LanternMinutesPerSecond) + ShopClock.Offer);
        if (looked)
        {
            s.Spend((int)(LanternLook * ShopClock.LanternMinutesPerSecond));
            if (chest.Tampered) return null; // rag-wrapped stones are plain to see
        }

        float belief = 0, explained = 0;
        bool allWeightsKnown = true;
        var weightTwins = new List<ItemDef>();
        foreach (var heard in chest.Items.Select(i => i.Def))
        {
            var alikes = ItemCatalog.All.Where(d => d.Sound == heard.Sound).ToList();
            var known = alikes.Where(ledger.Knows).ToList();
            if (known.Count == 0)
            {
                belief += UnknownGuess;
                allWeightsKnown = false;
                continue;
            }
            if (alikes.Count == 1)
            {
                belief += heard.Value;
                explained += heard.Weight;
                continue;
            }

            var cheap = alikes.MinBy(d => d.Value);
            var rich = alikes.MaxBy(d => d.Value);
            var tell = ItemCatalog.TellFor(heard);
            if (tell == Tell.Weight && p.Scale)
            {
                weightTwins.Add(heard);
                continue;
            }
            // Without the right check, assume it's the common one.
            var guess = (tell is Tell.Look or Tell.Glow) && looked ? heard : cheap;
            belief += ledger.Knows(guess) ? guess.Value : UnknownGuess * 1.5f;
            explained += guess.Weight;
        }

        bool hollow = p.Scale && chest.FalseBottom != null; // knocking is free, so anyone weighing also knocks
        foreach (var twin in weightTwins)
        {
            // The scale settles it only when everything else on it is accounted for.
            var alikes = ItemCatalog.All.Where(d => d.Sound == twin.Sound).ToList();
            var cheap = alikes.MinBy(d => d.Value);
            var rich = alikes.MaxBy(d => d.Value);
            float rest = chest.ContentsWeight - explained - weightTwins.Where(t => t != twin).Sum(t => t.Weight);
            var guess = allWeightsKnown && !hollow && MathF.Abs(rest - rich.Weight) < 0.3f ? rich : cheap;
            belief += ledger.Knows(guess) ? guess.Value : UnknownGuess * 1.5f;
            explained += guess.Weight;
        }

        if (p.Scale && allWeightsKnown)
        {
            float silent = chest.ContentsWeight - explained;
            if (silent > 0.4f && !hollow) return null; // dead weight that knocks solid: stones
        }
        if (hollow) belief += 150f * 0.85f; // a false bottom, if the lock is picked cleanly
        return belief;
    }

    static void Haggle(GameState s, Visit v, SimLog log, Player p)
    {
        var judged = Judge(s, v, p);
        if (judged is not float belief || v.OpeningAsk > belief * 1.8f)
        {
            v.Pass(s.Rng);
            return;
        }

        foreach (float fraction in new[] { 0.55f, 0.7f, 0.85f })
        {
            if (!s.CanAfford(ShopClock.Offer)) break;
            if (v.Ask <= belief * fraction && v.Ask <= s.Gold)
            {
                v.PayAsk(s.Rng);
                return;
            }
            s.Spend(ShopClock.Offer);
            v.Offer(Math.Min(s.Gold, (int)(belief * fraction)), s.Rng);
            if (v.Outcome != VisitOutcome.Haggling) return;
        }
        if (v.Ask <= belief * 0.9f && v.Ask <= s.Gold) v.PayAsk(s.Rng);
        else v.Pass(s.Rng);
    }

    static void InspectThenPay(GameState s, Visit v, Player p)
    {
        var judged = Judge(s, v, p);
        if (judged is float belief && v.Ask <= belief * 0.9f && v.Ask <= s.Gold) v.PayAsk(s.Rng);
        else v.Pass(s.Rng);
    }

    static void PrintDistribution()
    {
        var rng = new Random(42);
        var state = new GameState(42);
        var visits = Enumerable.Range(0, 2000)
            .Select(_ => Market.CreateVisit(rng, rng.PickOne(Sellers.All), state.AppraiserFor)).ToList();
        GD.Print("\nSeller        asks/value  walk-away/value  tampered  value p50");
        foreach (var group in visits.GroupBy(v => v.Seller.Name))
        {
            var list = group.ToList();
            float Ratio(Func<Visit, float> f) => Median(list.Select(f).ToList());
            GD.Print($"{group.Key,-20} {Ratio(v => v.OpeningAsk / (float)Math.Max(1, v.Chest.Value)),6:0.00}x  " +
                     $"{Ratio(v => v.Reservation / (float)Math.Max(1, v.Chest.Value)),13:0.00}x  " +
                     $"{100f * list.Count(v => v.Chest.Tampered) / list.Count,7:0}%  {Median(list.Select(v => (float)v.Chest.Value).ToList()),9:0}g");
        }
    }

    static float Median(List<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    /// <summary>Prints seeds whose first customer makes a good demo: a rich, honest chest.</summary>
    public static void FindDemoSeeds()
    {
        int found = 0;
        for (int seed = 1; seed < 5000 && found < 12; seed++)
        {
            var state = new GameState(seed);
            state.StartDay();
            var v = state.Queue[0];
            var c = v.Chest;
            if (c.Tampered || c.Items.Max(i => i.Def.Rarity) < Rarity.Epic || c.Items.Count < 3) continue;
            if (v.OpeningAsk > GameState.StartingGold) continue;
            found++;
            GD.Print($"seed={seed}  {v.Seller.Name} asks {v.OpeningAsk}g, {c.Provenance.Name}, value={c.Value}g, fb={(c.FalseBottom != null)}  items: " +
                     string.Join(", ", c.Items.Select(i => $"{i.Def.Name} ({i.Def.Rarity})")));
        }
    }
}
