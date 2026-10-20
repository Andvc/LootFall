using System.Collections.Generic;
using System.Linq;
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
        int early = State.ClearedWeek > 0 ? Guild.Weeks - State.ClearedWeek : 0;
        string paid = early > 0 ? $"Bram's debt is paid, {early} week{(early == 1 ? "" : "s")} early" : "Bram's debt is paid";
        string sub = _cleared
            ? State.VaultOpened ? $"{paid}, and King Aldric's hoard sits on your counter."
            : State.MapGiven ? $"{paid}. The Guild has the map; let them dig."
            : State.Week < Guild.Weeks && State.MapPieces > 0 ? $"{paid}. Keep trading, and the map may still lead to the vault."
            : $"{paid}. The vault will have to wait."
            : State.DebtCleared ? $"Bram's debt was paid, but the Guild's interest caught up with you in week {State.Week}."
            : $"The Guild took the shop in week {State.Week}.";
        Add(UiKit.MakeLabel(sub, 24, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 168, 1280, 30));

        var panel = Add(UiKit.MakePanel(color.Darkened(0.3f)).Place(90, 220, 540, 300));
        var rows = new List<(string, string)>
        {
            ("Debt cleared", State.ClearedDay > 0 ? $"day {State.ClearedDay}, week {State.ClearedWeek}" : "no"),
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
            panel.AddChild(UiKit.MakeLabel(label, 16, Palette.Muted).Place(32, 24 + i * 32, 260, 20));
            panel.AddChild(UiKit.MakeLabel(value, 16, Palette.Text, HorizontalAlignment.Right).Place(260, 24 + i * 32, 248, 20));
        }
        BuildScore(color);
        Add(UiKit.MakeLabel(News(), 16, Palette.Good, HorizontalAlignment.Center, outline: 4).Place(0, 532, 1280, 20));

        if (_cleared)
        {
            Add(UiKit.MakeButton("Keep trading", Palette.Gold, 24, KeepTrading).Place(340, 580, 290, 64)).TooltipText =
                State.Week < Guild.Weeks
                    ? $"Nobody's coming for anything until week {Guild.Weeks + 1}: then the Guild wants interest, half as much again each week."
                    : "The Guild keeps coming, for half as much again each week. How long can you last?";
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

    /// <summary>
    /// The run's score, line by line, and the renown it earns at this challenge level: banked when the run ends (a
    /// cleared run that keeps trading adds to it every week it keeps the Guild paid).
    /// </summary>
    void BuildScore(Color color)
    {
        var panel = Add(UiKit.MakePanel(Palette.Magic.Darkened(0.3f)).Place(650, 220, 540, 300));
        panel.AddChild(UiKit.MakeLabel($"{State.Kin.Name.ToUpperInvariant()}'S SCORE{(State.Challenge > 0 ? $", LEVEL {State.Challenge}" : "")}", 16,
            Palette.Magic).Place(32, 20, 480, 20));
        var lines = Score.Lines(State);
        for (int i = 0; i < lines.Count; i++)
        {
            var (label, points) = lines[i];
            panel.AddChild(UiKit.MakeLabel(label, 16, Palette.Muted).Place(32, 50 + i * 26, 380, 20));
            panel.AddChild(UiKit.MakeLabel($"{points}", 16, Palette.Text, HorizontalAlignment.Right).Place(400, 50 + i * 26, 108, 20));
        }
        int total = Score.Total(State), renown = Score.Renown(State);
        string times = State.Challenge > 0 ? $"{total} x{Challenges.Multiplier(State.Challenge):0.##} = " : "";
        panel.AddChild(UiKit.MakeLabel("Renown", 24, Palette.Gold, outline: 4).Place(32, 254, 200, 30));
        panel.AddChild(UiKit.MakeLabel($"{times}{renown}", 24, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(200, 254, 308, 30));
    }

    /// <summary>What this run opened up: another of Bram's kin, the next challenge level; or the record across runs.</summary>
    string News()
    {
        var profile = Game.Profile;
        var news = new List<string>();
        foreach (var kin in Characters.All.Where(c => profile.Unlocked(c) && !Game.KinAtStart.Contains(c.Key)))
            news.Add($"{kin.Name} can mind the stall now!");
        int levels = profile.MaxChallenge(State.Character);
        if (levels > Game.LevelsAtStart) news.Add($"Challenge level {levels} is open for {State.Kin.Name}.");
        if (_cleared && State.Week <= Guild.Weeks) news.Add("Keep trading and every week you keep the Guild paid adds to the score.");
        if (news.Count > 0) return string.Join("  ", news);
        return profile is { Runs: > 0 }
            ? $"All runs: {profile.Runs} started, {profile.Cleared} debts cleared, {profile.Vaults} vaults opened, best week {profile.BestWeek}."
            : "";
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
