using System.Threading.Tasks;
using Godot;
using LootFall.Phases;

namespace LootFall.Debug;

/// <summary>
/// Plays lots hands-free so the game can be screenshotted or recorded headlessly:
/// `godot --path . -- --autoplay --seed=1113 [--shots=DIR] [--quit-at-end] [--lots=N] [--bot-crowbar] [--bot-pass-odd]`.
/// </summary>
public partial class Autoplay : Node
{
    /// <summary>Quit after the first opened chest's results.</summary>
    public bool QuitAtEnd;

    /// <summary>Quit when this many lots have been played (0 = no limit).</summary>
    public int MaxLots;

    /// <summary>Force every lock with the crowbar instead of picking it.</summary>
    public bool UseCrowbar;

    /// <summary>Pass on odd-numbered lots, to exercise the "missed lot" screen.</summary>
    public bool PassOddLots;

    public async void OnPhase(Phase phase)
    {
        await Wait(0.6);
        if (!IsInstanceValid(phase)) return;
        switch (phase)
        {
            case AuctionPhase auction:
                await DriveAuction(auction);
                break;
            case LockpickPhase lockpick:
                await DriveLockpick(lockpick);
                break;
            case OpeningPhase opening:
                await DriveOpening(opening);
                break;
        }
    }

    async Task DriveAuction(AuctionPhase auction)
    {
        if (MaxLots > 0 && State.LotNumber > MaxLots)
        {
            GD.Print($"Autoplay finished {MaxLots} lots with {State.Gold}g");
            GetTree().Quit();
            return;
        }
        if (PassOddLots && State.LotNumber % 2 == 1)
        {
            await Wait(0.8);
            auction.Pass();
            await Wait(2.5);
            if (IsInstanceValid(auction)) auction.NextLot();
            return;
        }
        auction.Inspect(AuctionPhase.Inspection.Shake);
        await Wait(1.4);
        auction.Inspect(AuctionPhase.Inspection.Weigh);
        await Wait(1.4);
        Main.Instance.Moment("01_auction");
        await Wait(0.3);

        while (IsInstanceValid(auction) && !auction.Over)
        {
            if (auction.CanBid) auction.Bid();
            else if (!auction.Busy && !auction.PlayerLeads) auction.Pass();
            await Wait(1.0);
        }
        if (IsInstanceValid(auction) && auction.IsInsideTree() && State.PricePaid == 0)
        {
            await Wait(2.5);
            if (IsInstanceValid(auction)) auction.NextLot();
        }
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
        else if (IsInstanceValid(opening)) opening.NextLot();
    }

    static Core.GameState State => Main.Instance.State;

    SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
