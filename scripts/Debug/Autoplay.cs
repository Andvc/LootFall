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
/// magnet once it has them), fills in the slip from what it heard the way a careful player would, and buys when the
/// slip says it's worth more than the ask. Between days it spends on upgrades in a sensible order.
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

    async Task<bool> Shake(CounterPhase counter, Shake level)
    {
        counter.Shake.AutoShake(ShakeSeconds, level);
        await Wait(ShakeSeconds + 0.5);
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
        foreach (var level in new[] { Core.Shake.Gentle, Core.Shake.Firm, Core.Shake.Hard })
            if (!await Shake(counter, level)) return;
        // A jingle or tinkle that goes quiet under the magnet is gilded iron.
        if (State.Has("magnet") && Categories.All.Any(c => c.Delicate && counter.Hearing.HeardAt(c.Category, Core.Shake.Gentle, false)))
        {
            counter.ToggleMagnet();
            await Wait(0.6);
            if (!await Shake(counter, Core.Shake.Gentle)) return;
        }

        if (!_ledgerShown)
        {
            _ledgerShown = true;
            counter.OpenLedger();
            await Wait(2.0);
            if (!IsInstanceValid(counter)) return;
            counter.CloseLedger();
            await Wait(0.5);
        }

        // The slip, corrected from what it heard, one row at a time.
        var read = counter.Hearing.Read(visit.Grade);
        foreach (var kind in Categories.UpTo(State.Tier))
        {
            if (counter.Slip[kind.Category] == read[kind.Category]) continue;
            counter.SetMark(kind.Category, read[kind.Category]);
            await Wait(0.35);
            if (!IsInstanceValid(counter)) return;
        }
        await ReadTips(counter.Mentor);
        if (!IsInstanceValid(counter)) return;
        await Wait(0.6);
        if (!IsInstanceValid(counter)) return;

        Main.Instance.Moment($"06_decide_d{State.Day}_{visit.Seller.Key}");
        if (counter.Slip.Worth > visit.Ask && State.Gold >= visit.Ask)
        {
            counter.Buy();
            return;
        }
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
        if (QuitAtEnd) GetTree().Quit();
        else if (IsInstanceValid(opening)) opening.Next();
    }

    SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
