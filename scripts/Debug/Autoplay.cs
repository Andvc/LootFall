using System;
using System.Threading.Tasks;
using Godot;
using LootFall.Core;
using LootFall.Phases;

namespace LootFall.Debug;

/// <summary>
/// Plays the shop hands-free so it can be screenshotted, recorded or smoke-tested headlessly:
/// `godot --path . -- --autoplay --seed=4306 [--shots=DIR] [--quit-at-end] [--days=N] [--bot-crowbar] [--bot-pass-odd]`.
/// It shakes, weighs and knocks on every chest, looks inside when a lesson calls for it (and once a day otherwise),
/// passes on cons, and haggles from 60% of the ask.
/// </summary>
public partial class Autoplay : Node
{
    /// <summary>Quit after the first opened chest's results.</summary>
    public bool QuitAtEnd;

    /// <summary>Quit when this many days have been played (0 = no limit).</summary>
    public int MaxDays;

    /// <summary>Force every lock with the crowbar instead of picking it.</summary>
    public bool UseCrowbar;

    /// <summary>Pass on every other customer, to exercise walk-away and pass paths.</summary>
    public bool PassOdd;

    int _customersToday;
    bool _peekedToday, _ledgerShown;

    static GameState State => Main.Instance.State;

    /// <summary>Returns false (and quits) once the day limit is reached.</summary>
    public bool OnDayStarted(int day)
    {
        _customersToday = 0;
        _peekedToday = false;
        if (MaxDays <= 0 || day <= MaxDays) return true;
        GD.Print($"Autoplay finished {MaxDays} days with {State.Gold}g");
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
            case BackRoomPhase room:
                await DriveBackRoom(room);
                break;
            case LockpickPhase lockpick:
                await DriveLockpick(lockpick);
                break;
            case OpeningPhase opening:
                await DriveOpening(opening);
                break;
        }
    }

    async Task DriveCounter(CounterPhase counter)
    {
        _customersToday++;
        var visit = counter.Visit;
        var lesson = visit.Lesson;
        bool Expects(Cue cue) => lesson != null && new Tutor(lesson).Expects(cue);

        counter.Shake.AutoShake(2.4);
        await Wait(1.0);
        Main.Instance.Moment("01_shake");
        await Wait(2.2);
        if (!IsInstanceValid(counter)) return;

        if (!_ledgerShown || Expects(Cue.LedgerOpened))
        {
            _ledgerShown = true;
            counter.OpenLedger();
            await Wait(2.0);
            if (!IsInstanceValid(counter)) return;
            counter.CloseLedger();
            await Wait(0.6);
        }

        // Handling the chest is free, so do all of it.
        counter.Weigh();
        await Wait(2.2);
        if (!IsInstanceValid(counter)) return;
        counter.Knock();
        await Wait(1.6);
        if (!IsInstanceValid(counter)) return;

        bool look = lesson != null ? Expects(Cue.SawItem) : visit.Seller.Type == Archetype.Con || !_peekedToday;
        if (look)
        {
            _peekedToday = true;
            counter.Peek();
            await Wait(0.3);
            counter.Keyhole?.AutoSweep(3.2);
            await Wait(3.6);
            if (!IsInstanceValid(counter)) return;
            counter.CloseKeyhole();
            await Wait(0.8);
            if (!IsInstanceValid(counter)) return;
        }

        // The lantern shows the rag-wrapped stones.
        if (visit.Chest.Tampered || (PassOdd && _customersToday % 2 == 0))
        {
            counter.Pass();
            return;
        }

        // A lesson chest is a good buy by design: one offer, then pay.
        if (lesson != null)
        {
            counter.MakeOffer(Math.Min(State.Gold, Money.RoundTo5(visit.Ask * 0.8f)));
            await Wait(1.3);
            if (!IsInstanceValid(counter) || counter.Concluded) return;
            if (State.Gold >= visit.Ask) counter.PayAsk();
            else counter.Pass();
            return;
        }

        foreach (float fraction in new[] { 0.6f, 0.72f, 0.85f })
        {
            if (!IsInstanceValid(counter) || counter.Concluded) return;
            if (visit.Ask <= visit.OpeningAsk * 0.8f && State.Gold >= visit.Ask)
            {
                counter.PayAsk();
                return;
            }
            counter.MakeOffer(Money.RoundTo5(visit.Ask * fraction));
            await Wait(1.3);
        }
        if (IsInstanceValid(counter) && !counter.Concluded) counter.Pass();
    }

    async Task DriveBackRoom(BackRoomPhase room)
    {
        await Wait(0.6);
        if (!IsInstanceValid(room)) return;
        if (room.HasUnopened)
        {
            room.OpenNext();
            return;
        }
        await Wait(2.0);
        if (!IsInstanceValid(room)) return;
        if (room.Bankrupt) room.StartOver();
        else room.NextDay();
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
        await Wait(0.5);
        Main.Instance.AutoHold = true;
        while (IsInstanceValid(opening) && !opening.Finished)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Main.Instance.AutoHold = false;

        await Wait(3.0);
        if (QuitAtEnd) GetTree().Quit();
        else if (IsInstanceValid(opening)) opening.BackToRoom();
    }

    SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
