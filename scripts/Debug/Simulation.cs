using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Debug;

/// <summary>
/// Headless balance report: run with `godot --headless --path . -- --sim [--n=300]`.
/// Plays whole campaigns at the counter with simple player styles. The design only works if judgment
/// (inspecting, reading sellers) clearly beats every mindless strategy, and if no strategy is dominant.
/// </summary>
public static class Simulation
{
    const int Days = 15;

    public static void Run(int campaigns)
    {
        GD.Print($"=== LootFall balance report ({campaigns} campaigns x {Days} days) ===");
        PrintDistribution();

        GD.Print("\nStyle                 survive  final gold  profit/day  buys/day  bought cons  walkouts/day  unseen/day");
        foreach (var (name, policy) in Policies)
        {
            var results = Enumerable.Range(0, campaigns).Select(i => Campaign(policy, 1000 + i)).ToList();
            GD.Print($"{name,-21} {100f * results.Count(r => r.Survived) / campaigns,6:0}%  {results.Average(r => r.FinalGold),10:0}  " +
                     $"{results.Average(r => r.ProfitPerDay),10:0}  {results.Average(r => r.BuysPerDay),8:0.0}  " +
                     $"{results.Average(r => r.ConsBought),11:0.0}  {results.Average(r => r.WalkoutsPerDay),12:0.0}  " +
                     $"{results.Average(r => r.UnseenPerDay),10:0.0}");
        }
    }

    static readonly (string, Action<GameState, Visit, SimLog>)[] Policies =
    {
        ("pay the ask", PayAsk),
        ("lowballer", Lowball),
        ("blind haggler", (s, v, log) => Haggle(s, v, log, inspect: false)),
        ("inspector", (s, v, log) => Haggle(s, v, log, inspect: true)),
        ("inspector, no haggle", InspectThenPay),
        ("triage inspector", Triage),
    };

    sealed class SimLog
    {
        public int Walkouts;
        public int ConsBought;
        public int Unseen;
    }

    sealed record Result(bool Survived, int FinalGold, float ProfitPerDay, float BuysPerDay, int ConsBought, float WalkoutsPerDay,
        float UnseenPerDay);

    static Result Campaign(Action<GameState, Visit, SimLog> policy, int seed)
    {
        var state = new GameState(seed);
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

            // Night: open everything with a typical skill spread at the lock.
            foreach (var p in state.Purchases)
            {
                float r = rng.NextFloat();
                var grade = r < 0.15f ? PickGrade.Masterful : r < 0.9f ? PickGrade.Clean : PickGrade.Forced;
                state.Gold += Loot.Resolve(p.Chest, grade).Sum(i => i.Value);
            }
            if (!state.PayRentIfDue(out _)) break;
        }
        int lived = Math.Min(day, Days);
        return new Result(day > Days, state.Gold, (state.Gold - GameState.StartingGold) / (float)lived,
            buys / (float)lived, log.ConsBought, log.Walkouts / (float)lived, log.Unseen / (float)lived);
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

    /// <summary>
    /// What a skilled player can work out. Shaking reveals the mix of materials and their quality, the weight is
    /// felt while dragging, the lock plate is read, and knocking finds hollow bottoms. Stones grind and fake locks
    /// show, so tampering is usually caught.
    /// </summary>
    static float? Judge(GameState s, Visit v, bool inspect)
    {
        if (!inspect) return s.AppraiserFor(v.Claimed).Prior.Median;

        int cost = ShopClock.ExamineLock + ShopClock.Knock;
        if (!s.CanAfford(cost + ShopClock.Offer)) return s.AppraiserFor(v.Claimed).Prior.Median;
        s.Spend(cost);
        if (v.Chest.Tampered && s.Rng.NextFloat() < 0.9f) return null;

        float belief = s.AppraiserFor(v.Claimed).Estimate(v.Chest, new Clues(true, true, true)).Median;
        if (v.Chest.FalseBottom != null) belief *= 1.35f;
        return belief;
    }

    static void Haggle(GameState s, Visit v, SimLog log, bool inspect)
    {
        var judged = Judge(s, v, inspect);
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

    /// <summary>A player who has learned the regulars: skips the usual con and the braggart, only checks Pip for a hollow bottom.</summary>
    static void Triage(GameState s, Visit v, SimLog log)
    {
        switch (v.Seller.Type)
        {
            case Archetype.Con or Archetype.Braggart:
                v.Pass(s.Rng);
                return;
            case Archetype.Collector:
                if (!s.CanAfford(ShopClock.Knock + ShopClock.Offer)) break;
                s.Spend(ShopClock.Knock);
                if (v.Chest.FalseBottom != null && v.Ask <= s.Gold) v.PayAsk(s.Rng);
                else v.Pass(s.Rng);
                return;
            case Archetype.Salvager:
                InspectThenPay(s, v, log);
                return;
        }
        Haggle(s, v, log, inspect: true);
        if (v.Outcome == VisitOutcome.Haggling) v.Pass(s.Rng);
    }

    static void InspectThenPay(GameState s, Visit v, SimLog log)
    {
        var judged = Judge(s, v, inspect: true);
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
            if (c.Tampered || c.Items.Max(i => i.Def.Rarity) < Rarity.Epic || c.Items.Count < 4) continue;
            if (v.OpeningAsk > GameState.StartingGold) continue;
            found++;
            GD.Print($"seed={seed}  {v.Seller.Name} asks {v.OpeningAsk}g, {c.Provenance.Name}, value={c.Value}g, fb={(c.FalseBottom != null)}  items: " +
                     string.Join(", ", c.Items.Select(i => $"{i.Def.Name} ({i.Def.Rarity})")));
        }
    }
}
