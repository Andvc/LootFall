using System.Collections.Generic;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The end of a run: Bram's debt paid (keep trading against the Guild's endless interest, or retire), or the shop
/// foreclosed. Either way, how the run went, and your best across every run.
/// </summary>
public partial class RunEndPhase : Phase
{
    readonly bool _cleared;

    public RunEndPhase(bool cleared)
    {
        _cleared = cleared;
    }

    public RunEndPhase() { }

    public bool Cleared => _cleared;

    public override void _Ready()
    {
        Add(UiKit.MakeShop(State.Tier, _cleared ? 0.55f : 0.25f));
        var color = _cleared ? Palette.Gold : Palette.Bad;
        string title = _cleared ? (State.VaultOpened ? "THE VAULT IS YOURS" : "DEBT CLEARED") : "FORECLOSED";
        var logo = Add(UiKit.MakeLabel(title, 64, color, HorizontalAlignment.Center, outline: 8).Place(0, 80, 1280, 80));
        logo.PivotOffset = new Vector2(640, 40);
        logo.Scale = Vector2.One * 1.3f;
        logo.CreateTween().TweenProperty(logo, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        string sub = _cleared
            ? State.VaultOpened ? "Bram's debt is paid, and King Aldric's hoard sits on your counter."
            : State.MapGiven ? "Bram's debt is paid. The Guild has the map; let them dig."
            : "Bram's debt is paid. The vault will have to wait."
            : $"The Guild took the shop in week {State.Week}.";
        Add(UiKit.MakeLabel(sub, 24, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 168, 1280, 30));

        var panel = Add(UiKit.MakePanel(color.Darkened(0.3f)).Place(340, 220, 600, 300));
        var rows = new List<(string, string)>
        {
            ("Weeks the Guild was paid", $"{State.WeeksPaid}"),
            ("Days of trading", $"{State.Day}"),
            ("The shop", ShopTiers.Name(State.Tier)),
            ("The chests brought in", $"{State.TotalEarned}g"),
            ("Gold in hand", $"{State.Gold}g"),
            ("Collection", $"{State.Found.Count} of {Collection.Size}"),
            ("Map pieces", State.MapGiven ? "given to the Guild" : $"{State.MapPieces} of {Story.Pieces}"),
            ("Curios", $"{State.CurioKeys.Count}"),
        };
        for (int i = 0; i < rows.Count; i++)
        {
            var (label, value) = rows[i];
            panel.AddChild(UiKit.MakeLabel(label, 16, Palette.Muted).Place(32, 24 + i * 32, 300, 20));
            panel.AddChild(UiKit.MakeLabel(value, 16, Palette.Text, HorizontalAlignment.Right).Place(300, 24 + i * 32, 268, 20));
        }
        var profile = Game.Profile;
        if (profile != null)
            Add(UiKit.MakeLabel($"All runs: {profile.Runs} started, {profile.Cleared} debts cleared, {profile.Vaults} vaults opened, " +
                                $"best week {profile.BestWeek}.", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(0, 532, 1280, 20));

        if (_cleared)
        {
            Add(UiKit.MakeButton("Keep trading", Palette.Gold, 24, KeepTrading).Place(340, 580, 290, 64)).TooltipText =
                "The Guild keeps coming, for half as much again each week. How long can you last?";
            Add(UiKit.MakeButton("Retire", Palette.Text, 24, Retire).Place(650, 580, 290, 64)).TooltipText = "Hang up your apron: back to the title.";
            Sfx.Play("fanfare", 1f, -2);
            Fx.Confetti(this, new Vector2(640, 120), 160);
        }
        else
        {
            Add(UiKit.MakeButton("Back to the title", Palette.Gold, 24, Retire).Place(495, 580, 290, 64));
            Sfx.Play("lose", 0.8f, -2);
        }
        Game.Moment(_cleared ? "23_cleared" : "23_foreclosed", 0.8);
    }

    public void KeepTrading()
    {
        if (!_cleared) return;
        Sfx.Play("bid");
        Game.StartDay();
    }

    public void Retire()
    {
        Sfx.Play("bid");
        Game.EndRun();
    }
}
