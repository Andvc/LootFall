using System;
using System.Collections.Generic;

namespace LootFall.Progression;

/// <summary>How a simulated player plays: how often they check in (equipping is automatic, like the game's auto-equip),
/// and whether they use the prestige layers.</summary>
public sealed class Profile
{
    public string Name = "active";
    /// <summary>At most this many seconds between decisions (talents, forge, ranks, focus, prestige); 0 means every step.
    /// Check-ins are a tenth of the time played apart until they reach it: everyone plays the first hour.</summary>
    public double CheckIn;
    public bool Ascends = true, Transcends = true;
    /// <summary>A run of this length (seconds) ascends for 3% more depth; one n times as long for 3% over n: the
    /// longer the player has waited, the smaller the gain they'll take.</summary>
    public double Patience = 1800;

    public static Profile Active() => new() { Name = "active", CheckIn = 0 };
    public static Profile Idle() => new() { Name = "idle", CheckIn = 4 * 3600 };
    public static Profile Daily() => new() { Name = "daily", CheckIn = 24 * 3600 };
    public static Profile NoPrestige() => new() { Name = "no-prestige", CheckIn = 0, Ascends = false, Transcends = false };
}

/// <summary>
/// The long-horizon simulator: one hero's <see cref="World"/>, played by a <see cref="Strategy"/> at the profile's
/// check-ins, for thousands of hours, in steps that grow from a second to ten minutes. Fights are the closed form in
/// <see cref="Combat"/>, drops are sampled in aggregate (a Poisson count per slot and rarity, and the best quality of n
/// drops in one draw), so a 5,000-hour run takes seconds.
/// </summary>
public sealed class Sim
{
    public readonly World W;
    public readonly Strategy S;
    public readonly Hero H;
    public readonly Profile Pr;
    public readonly Metrics M;
    double _nextCheck;

    public Sim(Params p, Content c, TalentTree tree, Profile profile, int seed)
    {
        Pr = profile;
        W = new World(p, c, tree, seed);
        H = W.H;
        S = new Strategy(W);
        M = new Metrics(this);
        W.Events = M;
        S.UpdateWeights();
    }

    public double Time => W.Time;
    public double Hours => W.Hours;
    public double[] Weights => S.Weights;

    /// <summary>When set, transcendence decisions are written here (progsim --debug-prestige).</summary>
    public List<string> Log
    {
        get => S.Log;
        set => S.Log = value;
    }

    public void Run(double hours, double maxStep = 600)
    {
        double end = hours * 3600;
        M.Snapshot(force: true);
        while (W.Time < end)
        {
            double dt = Math.Clamp(W.Time / 400, 1, maxStep);
            // Steps land on the end and on each snapshot, so every mark is measured where it is.
            dt = Math.Min(dt, (M.NextSnap > W.Time ? Math.Min(end, M.NextSnap) : end) - W.Time);
            Step(dt);
            W.Time += dt;
            M.AfterStep(dt);
        }
        if (M.Snaps[^1].Hours < Hours - 1e-9) M.Snapshot(force: true);
        else M.Snaps[^1].Knockout ??= M.Knockouts(M.Snaps[^1].Reach);
    }

    void Step(double dt)
    {
        var o = W.Step(dt, S);
        M.Kills += o.PushKills + o.FarmKills;
        M.FarmDepth = o.FarmDepth;

        // Decisions, at check-ins. From the first transcendence on, the autopilot works in between: it buys forge levels
        // and ranks as auto-buyers would and ascends by the player's own rule (the automation milestone that lets idle
        // play keep up).
        if (W.Time >= _nextCheck)
        {
            _nextCheck = W.Time + Math.Min(Pr.CheckIn, Math.Max(60, W.Time / 10));
            Decide();
        }
        else if (H.Transcendence.Resets > 0)
        {
            if (S.SpendForge() | S.SpendRanks()) H.Rebuild();
            if (Pr.Ascends && S.TryPrestige(transcend: false, Pr.Patience) && (S.SpendForge() | S.SpendRanks())) H.Rebuild();
        }
    }

    void Decide()
    {
        S.UpdateWeights();
        S.AllocateTalents();
        M.Talents(H.Talents.AllocatedCount, H.Talents.Paragon);
        bool changed = S.SpendForge() | S.SpendRanks();
        S.ChooseFocus();
        if (changed) H.Rebuild();
        else H.Rebuild(gear: false);
        if (Pr.Ascends) S.TryPrestige(transcend: Pr.Transcends, Pr.Patience);
    }
}
