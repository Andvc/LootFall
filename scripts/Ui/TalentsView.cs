using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The talent tree, over the title or the character screen: what Bram's kin have learned across runs, bought with the
/// renown runs earn. One root and three branches (safety, curios, the shop), each talent building on the one above
/// it. Learned talents come along on every run.
/// </summary>
public partial class TalentsView : Control
{
    const float X = 30, Y = 16, W = 1220, H = 688, NodeW = 370, NodeH = 100, NodeGap = 8, RootY = 92, BranchY = 222;
    static readonly float[] Columns = { 45, 455, 865 };

    public event Action Closed;

    Control _board;

    static Profile Profile => Main.Instance.Profile;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920));
        AddChild(UiKit.MakePanel(Palette.Magic.Darkened(0.3f)).Place(X, Y, W, H));
        AddChild(UiKit.MakeLabel("TALENTS", 32, Palette.Magic, HorizontalAlignment.Center, outline: 4).Place(X, Y + 14, W, 40));
        AddChild(UiKit.MakeLabel("Learned once, kept for every run. Runs earn renown: their score, times the challenge level's multiplier.",
            16, Palette.Muted, HorizontalAlignment.Center).Place(X, Y + 56, W, 20));
        _board = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_board.Place(0, 0, 1280, 720));
        AddChild(UiKit.MakeButton("Close", Palette.Gold, 16, Close).Place(X + W - 180, Y + H - 52, 160, 40));
        Build();
    }

    void Build()
    {
        foreach (var child in _board.GetChildren()) child.QueueFree();
        _board.AddChild(UiKit.MakeLabel($"Renown: {Profile.Renown}", 24, Palette.Gold, outline: 4).Place(X + 24, Y + 18, 400, 30));

        var root = Talents.All.First(t => t.Branch == null);
        float rootX = Columns[1];
        // The lines first, under the nodes: root to the head of each branch, and down each branch.
        _board.AddChild(new Painter(p =>
        {
            var line = Palette.PanelBorder.Lightened(0.3f);
            for (int b = 0; b < Talents.Branches.Length; b++)
            {
                float cx = Columns[b] + NodeW / 2;
                p.DrawLine(new Vector2(rootX + NodeW / 2, RootY + NodeH - 20), new Vector2(cx, BranchY - 24), line, 4);
                for (int i = 1; i < 4; i++)
                    p.DrawLine(new Vector2(cx, BranchY + i * (NodeH + NodeGap) - NodeGap - 2), new Vector2(cx, BranchY + i * (NodeH + NodeGap) + 2), line, 4);
            }
        }));
        _board.AddChild(MakeNode(root, rootX, RootY, NodeH - 20));
        for (int b = 0; b < Talents.Branches.Length; b++)
        {
            string branch = Talents.Branches[b];
            _board.AddChild(UiKit.MakeLabel(branch.ToUpperInvariant(), 16, Palette.Gold.Darkened(0.15f), HorizontalAlignment.Center, outline: 4)
                .Place(Columns[b], BranchY - 24, NodeW, 20));
            var talents = Talents.On(branch).ToList();
            for (int i = 0; i < talents.Count; i++)
                _board.AddChild(MakeNode(talents[i], Columns[b], BranchY + i * (NodeH + NodeGap), NodeH));
        }
    }

    /// <summary>A talent: learned (green), ready to learn (gold), the next on its branch but not affordable yet, or further down.</summary>
    Control MakeNode(TalentDef talent, float x, float y, float h)
    {
        bool learned = Profile.Knows(talent.Key), next = !learned && (talent.Requires == null || Profile.Knows(talent.Requires));
        bool ready = next && Profile.CanLearn(talent);
        var border = learned ? Palette.Good : ready ? Palette.Gold : next ? Palette.Text.Darkened(0.3f) : Palette.PanelBorder;
        var node = UiKit.MakePanel(border).Place(x, y, NodeW, h);
        node.Modulate = learned || next ? Colors.White : new Color(1, 1, 1, 0.55f);
        node.AddChild(UiKit.MakeLabel(talent.Name, 16, learned ? Palette.Good : Palette.Text, outline: 4).Place(14, 8, NodeW - 120, 20));
        node.AddChild(UiKit.MakeLabel(learned ? "Learned" : $"{talent.Cost}", 16, learned ? Palette.Good : ready ? Palette.Gold : Palette.Muted,
            HorizontalAlignment.Right).Place(NodeW - 120, 8, 106, 20));
        node.AddChild(UiKit.MakeLabel(talent.Blurb, 16, Palette.Muted).Wrapped().Place(14, 32, NodeW - 28, h - 36));
        if (ready)
        {
            var learn = new Button { Flat = true, FocusMode = FocusModeEnum.None, TooltipText = $"Learn {talent.Name} for {talent.Cost} renown." };
            learn.Pressed += () => Learn(talent);
            node.AddChild(learn.Place(0, 0, NodeW, h));
        }
        else if (next) node.TooltipText = $"{talent.Cost - Profile.Renown} more renown to learn it.";
        return node;
    }

    public bool Learn(TalentDef talent)
    {
        if (!Main.Instance.LearnTalent(talent)) return false;
        Main.Instance.Sfx.Play("fanfare", 1.4f, -10);
        Build();
        return true;
    }

    public void Close()
    {
        Closed?.Invoke();
        QueueFree();
    }
}
