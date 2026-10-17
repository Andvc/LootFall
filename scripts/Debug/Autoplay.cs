using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Phases;

namespace LootFall.Debug;

/// <summary>
/// Plays the shop hands-free so it can be screenshotted, recorded or smoke-tested headlessly:
/// `godot --path . -- --autoplay [--seed=N] [--shots=DIR] [--quit-at-end] [--days=N] [--start-tier=N]`.
/// It reads Bram's tips, shakes every chest gently, firmly and hard (with the witchlight on, and again gently with the
/// magnet once it has them), drops it when that's safe, fills in the slip from what it heard the way a careful player
/// would, and trades the way <see cref="Strategy"/> says: buys, haggles or names a price. It picks locks, keeps grand
/// things for display, and between days spends on upgrades in a sensible order.
/// </summary>
public partial class Autoplay : Node
{
    /// <summary>Quit after the first opened chest's results.</summary>
    public bool QuitAtEnd;

    /// <summary>Quit when this many days have been played (0 = no limit).</summary>
    public int MaxDays;

    const double ShakeSeconds = 1.6;

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

    async Task<bool> Shake(CounterPhase counter, Shake level, double seconds)
    {
        counter.Shake.AutoShake(seconds, level);
        await Wait(seconds + 0.5);
        if (!IsInstanceValid(counter)) return false;
        await ReadTips(counter.Mentor);
        return IsInstanceValid(counter);
    }

    async Task DriveCounter(CounterPhase counter)
    {
        var visit = counter.Visit;
        await ReadTips(counter.Mentor);
        if (!IsInstanceValid(counter)) return;

        if (State.Has("lamp")) counter.ToggleLamp();
        // An impatient seller only gets a gentle and a hard shake: that's enough to hear every kind.
        bool hurried = visit.Seller.Patience > 0;
        var levels = hurried ? new[] { Core.Shake.Gentle, Core.Shake.Hard } : new[] { Core.Shake.Gentle, Core.Shake.Firm, Core.Shake.Hard };
        foreach (var level in levels)
            if (!await Shake(counter, level, hurried ? 1.2 : ShakeSeconds)) return;
        // A jingle or tinkle that goes quiet under the magnet is gilded iron.
        if (Strategy.ShouldUseMagnet(visit, counter.Hearing, State))
        {
            counter.ToggleMagnet();
            await Wait(0.6);
            if (!await Shake(counter, Core.Shake.Gentle, ShakeSeconds)) return;
        }
        // Drop it to listen for a false bottom, unless there's glass in there or the seller would mind.
        if (Strategy.ShouldDrop(visit, counter.Hearing, State.Tier))
        {
            while (IsInstanceValid(counter) && counter.Shake.Busy) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInstanceValid(counter)) return;
            counter.DropChest();
            await Wait(1.8);
            if (!IsInstanceValid(counter)) return;
            await ReadTips(counter.Mentor);
            if (!IsInstanceValid(counter)) return;
        }

        if (!_ledgerShown && !hurried)
        {
            _ledgerShown = true;
            counter.OpenLedger();
            await Wait(2.0);
            for (int page = 1; page <= 2 && IsInstanceValid(counter); page++)
            {
                counter.LedgerPage(page);
                Main.Instance.Moment($"02_ledger_{page}", 0.3);
                await Wait(1.5);
            }
            if (!IsInstanceValid(counter)) return;
            counter.CloseLedger();
            await Wait(0.5);
        }

        // The slip, corrected from what it heard, one row at a time.
        var read = counter.Hearing.Read(visit.Chest.Quality, State.Tier);
        foreach (var kind in Categories.UpTo(State.Tier))
        {
            if (counter.Slip[kind.Category] == read[kind.Category]) continue;
            counter.SetMark(kind.Category, read[kind.Category]);
            await Wait(hurried ? 0.1 : 0.35);
            if (!IsInstanceValid(counter)) return;
        }
        if (read.Hidden != counter.Slip.Hidden) counter.SetHidden(read.Hidden);
        await ReadTips(counter.Mentor);
        if (!IsInstanceValid(counter) || counter.Concluded) return;
        await Wait(hurried ? 0.2 : 0.6);
        if (!IsInstanceValid(counter) || counter.Concluded) return;

        Main.Instance.Moment($"06_decide_d{State.Day}_{visit.Seller.Key}");
        int worth = counter.Slip.Worth;
        switch (visit.Trade)
        {
            case Trade.Fixed:
                if (worth > visit.Ask && State.Gold >= visit.Ask)
                {
                    counter.Buy();
                    return;
                }
                break;
            case Trade.Haggle:
            {
                int offer = Math.Min(State.Gold, Strategy.FirstOffer(visit, worth));
                if (offer <= 0 || worth <= offer) break;
                counter.MakeOffer(offer);
                await Wait(1.4);
                if (!IsInstanceValid(counter) || !visit.Deciding) return;
                if (worth > visit.Ask && State.Gold >= visit.Ask)
                {
                    counter.Buy();
                    return;
                }
                break;
            }
            default:
            {
                if (worth <= 1) break;
                counter.MakeOffer(Math.Min(State.Gold, Strategy.FirstOffer(visit, worth)));
                await Wait(1.4);
                if (!IsInstanceValid(counter) || !visit.Deciding) return;
                await ReadTips(counter.Mentor);
                if (!IsInstanceValid(counter) || !visit.Deciding) return;
                counter.MakeOffer(Math.Min(State.Gold, Strategy.SecondOffer(worth)));
                return;
            }
        }
        if (!visit.Deciding) return;
        counter.Pass();
        await Wait(2.6);
        if (!IsInstanceValid(counter)) return;
        await ReadTips(counter.Mentor);
        if (IsInstanceValid(counter)) counter.Continue();
    }

    async Task DriveDayEnd(DayEndPhase dayEnd)
    {
        await Wait(1.0);
        if (!IsInstanceValid(dayEnd)) return;
        dayEnd.BuyInOrder(Simulation.BuyOrder, 10 + 15 * State.Tier);
        await Wait(2.0);
        if (!IsInstanceValid(dayEnd)) return;
        dayEnd.NextDay();
    }

    /// <summary>Sets each pin when the pick is in the bright centre.</summary>
    async Task DriveLockpick(LockpickPhase lockpick)
    {
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

        await Wait(3.0);
        if (!IsInstanceValid(opening)) return;
        await ReadTips(opening.Mentor);
        foreach (var (item, worth) in opening.Revealed.ToList())
        {
            var (keep, replace) = Strategy.ShouldKeep(State, item, worth);
            if (!keep || !IsInstanceValid(opening)) continue;
            if (replace != null) State.SellFromDisplay(replace);
            opening.Keep(item);
            await Wait(0.6);
        }
        if (!IsInstanceValid(opening)) return;
        await ReadTips(opening.Mentor);
        if (QuitAtEnd) GetTree().Quit();
        else if (IsInstanceValid(opening)) opening.Next();
    }

    SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
