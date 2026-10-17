using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Phases;

namespace LootFall.Debug;

/// <summary>
/// Plays the shop hands-free so it can be screenshotted, recorded or smoke-tested headlessly:
/// `godot --path . -- --autoplay [--seed=N] [--shots=DIR] [--quit-at-end] [--days=N] [--bot-crowbar] [--start-tier=N]`.
/// It reads Bram's tips, shakes every chest, uses every tool it owns, judges the chest the way the balance simulation
/// does, makes one offer, and spends its earnings on upgrades in a sensible order.
/// </summary>
public partial class Autoplay : Node
{
    /// <summary>Quit after the first opened chest's results.</summary>
    public bool QuitAtEnd;

    /// <summary>Quit when this many days have been played (0 = no limit).</summary>
    public int MaxDays;

    /// <summary>Force every lock with the crowbar instead of picking it.</summary>
    public bool UseCrowbar;

    bool _ledgerShown;

    static GameState State => Main.Instance.State;

    /// <summary>Returns false (and quits) once the day limit is reached.</summary>
    public bool OnDayStarted(int day)
    {
        if (MaxDays <= 0 || day <= MaxDays) return true;
        GD.Print($"Autoplay finished {MaxDays} days with {State.Gold}g ({ShopTiers.Name(State.Tier)})");
        GetTree().Quit();
        return false;
    }

    public async void OnPhase(Phase phase)
    {
        await Wait(0.8);
        if (!IsInstanceValid(phase)) return;
        switch (phase)
        {
            case CounterPhase counter:
                await DriveCounter(counter);
                break;
            case DayEndPhase dayEnd:
                await DriveDayEnd(dayEnd);
                break;
            case LockpickPhase lockpick:
                await DriveLockpick(lockpick);
                break;
            case OpeningPhase opening:
                await DriveOpening(opening);
                break;
        }
    }

    /// <summary>Gives each of Bram's tips a moment on screen, then clicks on.</summary>
    async Task ReadTips(Ui.MentorView mentor)
    {
        while (IsInstanceValid(mentor) && mentor.Showing)
        {
            await Wait(1.8);
            if (!IsInstanceValid(mentor)) return;
            mentor.Next();
        }
    }

    async Task DriveCounter(CounterPhase counter)
    {
        var visit = counter.Visit;
        var chest = visit.Chest;
        await ReadTips(counter.Mentor);
        if (!IsInstanceValid(counter)) return;

        counter.Shake.AutoShake(2.4);
        await Wait(1.0);
        Main.Instance.Moment("01_shake");
        await Wait(2.2);
        if (!IsInstanceValid(counter)) return;
        await ReadTips(counter.Mentor);
        if (!IsInstanceValid(counter)) return;

        if (!_ledgerShown)
        {
            _ledgerShown = true;
            counter.OpenLedger();
            await Wait(2.0);
            if (!IsInstanceValid(counter)) return;
            counter.CloseLedger();
            await Wait(0.5);
        }

        // Handling with every tool it owns, cheapest first.
        if (State.Has("scale"))
        {
            counter.Weigh();
            await Wait(2.0);
        }
        if (State.Has("compass") && IsInstanceValid(counter))
        {
            counter.UseCompass();
            await Wait(1.4);
        }
        if (State.Has("lodestone") && IsInstanceValid(counter))
        {
            counter.UseLodestone();
            await Wait(1.4);
        }
        if (State.Has("hammer") && IsInstanceValid(counter))
        {
            counter.Knock();
            await Wait(1.4);
        }
        bool worthAPeek = chest.Items.Any(i => i.Def.Family is Family.Cloth or Family.Paper) || visit.Seller.Rule == PriceRule.Con;
        if (worthAPeek && State.PeeksLeft > 0 && IsInstanceValid(counter))
        {
            counter.Peek();
            await Wait(2.2);
            if (!IsInstanceValid(counter)) return;
            counter.ClosePeephole();
            await Wait(0.6);
        }
        if (!IsInstanceValid(counter)) return;
        await ReadTips(counter.Mentor);
        if (!IsInstanceValid(counter)) return;

        var estimate = Estimator.Estimate(chest, visit.Seller, counter.Inspection, State.Ledger);
        if (estimate is not float worth)
        {
            counter.Pass();
            return;
        }
        if (visit.CanOffer && State.CanHaggle)
        {
            int offer = Math.Min(State.Gold, Money.RoundTo5(Math.Min(worth * 0.7f, visit.Ask * 0.85f)));
            if (offer < visit.Ask)
            {
                counter.MakeOffer(offer);
                await Wait(1.4);
                if (!IsInstanceValid(counter) || counter.Concluded) return;
            }
        }
        if (visit.Ask <= worth * 0.95f && State.Gold >= visit.Ask) counter.Buy();
        else counter.Pass();
    }

    async Task DriveDayEnd(DayEndPhase dayEnd)
    {
        await Wait(1.0);
        if (!IsInstanceValid(dayEnd)) return;
        dayEnd.BuyInOrder(Simulation.BuyOrder, 20 + 15 * State.Tier);
        await Wait(2.0);
        if (!IsInstanceValid(dayEnd)) return;
        dayEnd.NextDay();
    }

    async Task DriveLockpick(LockpickPhase lockpick)
    {
        if (UseCrowbar)
        {
            await Wait(0.5);
            lockpick.Force();
            return;
        }
        double cooldown = 0;
        while (IsInstanceValid(lockpick) && !lockpick.Done)
        {
            cooldown -= GetProcessDeltaTime();
            if (cooldown <= 0 && lockpick.InPerfectZone)
            {
                lockpick.TryPin();
                cooldown = 0.3;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    async Task DriveOpening(OpeningPhase opening)
    {
        await Wait(0.4);
        Main.Instance.AutoHold = true;
        while (IsInstanceValid(opening) && !opening.Finished)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Main.Instance.AutoHold = false;

        await Wait(2.4);
        if (!IsInstanceValid(opening)) return;
        await ReadTips(opening.Mentor);
        if (QuitAtEnd) GetTree().Quit();
        else if (IsInstanceValid(opening)) opening.Next();
    }

    SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
