using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// Before a new run: which of Bram's kin minds the stall (each reads chests or runs the shop their own way; the others
/// unlock as you go), and at what challenge level (every level adds one of the Guild's harsh terms, and a quarter more
/// renown). The talents learned with renown come along on every run.
/// </summary>
public partial class SetupPhase : Phase
{
    const float CardW = 270, CardH = 300, CardGap = 20, CardsX = 70, CardsY = 84;

    Node2D _board;
    string _character;
    int _level;
    TalentsView _talents;

    Profile Profile => Game.Profile;

    public string Character => _character;
    public TalentsView TalentsOpen => _talents;
    public int Level => _level;

    public override void _Ready()
    {
        Add(UiKit.MakeShop(1, 0.35f));
        Add(UiKit.MakeLabel("WHO MINDS THE STALL?", 32, Palette.Gold, HorizontalAlignment.Center, outline: 6).Place(0, 24, 1280, 40));
        var last = Characters.Get(Profile.LastCharacter);
        _character = Profile.Unlocked(last) ? last.Key : Characters.Kit;
        _level = System.Math.Clamp(Profile.LastChallenge, 0, Profile.MaxChallenge(_character));
        _board = Add(new Node2D());
        Build();
        Game.Moment("00_setup", 0.8);
    }

    void Build()
    {
        foreach (var child in _board.GetChildren()) child.QueueFree();
        _board.AddChild(UiKit.MakeLabel($"Renown: {Profile.Renown}", 16, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(900, 34, 310, 20));
        for (int i = 0; i < Characters.All.Length; i++)
            _board.AddChild(MakeCard(Characters.All[i], CardsX + i * (CardW + CardGap), CardsY));
        BuildDetails();
        _board.AddChild(UiKit.MakeButton("Back", Palette.Text, 16, Back).Place(CardsX, 652, 200, 52));
        _board.AddChild(UiKit.MakeButton($"Talents ({Profile.Talents.Count}/{Talents.All.Length})", Palette.Magic, 16, OpenTalents)
            .Place(CardsX + 220, 652, 260, 52));
        _board.AddChild(UiKit.MakeButton("Open the stall", Palette.Gold, 24, Start).Place(1210 - 300, 652, 300, 52));
    }

    Control MakeCard(CharacterDef kin, float x, float y)
    {
        bool open = Profile.Unlocked(kin), chosen = kin.Key == _character;
        var card = UiKit.MakePanel(chosen ? Palette.Gold : open ? Palette.PanelBorder.Lightened(0.2f) : Palette.PanelBorder).Place(x, y, CardW, CardH);
        var face = new Sprite2D
        {
            Texture = GD.Load<Texture2D>($"res://assets/sprites/sellers/{kin.Key}.png"),
            Position = new Vector2(CardW / 2, 64),
            Scale = Vector2.One * 3,
            Modulate = open ? Colors.White : new Color(0.15f, 0.12f, 0.2f),
        };
        card.AddChild(face);
        card.AddChild(UiKit.MakeLabel(kin.Name, 24, open ? Palette.Text : Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(0, 122, CardW, 30));
        card.AddChild(UiKit.MakeLabel(kin.Title, 16, open ? Palette.Gold : Palette.Muted, HorizontalAlignment.Center).Place(0, 152, CardW, 20));
        card.AddChild(UiKit.MakeLabel(open ? kin.Blurb : kin.Unlock, 16, open ? Palette.Muted : Palette.Warn, HorizontalAlignment.Center).Wrapped()
            .Place(14, 178, CardW - 28, 88));
        if (open)
        {
            int best = Profile.BestScore.GetValueOrDefault(kin.Key), max = Profile.MaxChallenge(kin.Key);
            string record = best > 0 ? $"Best {best}, level {max} open" : max > 0 ? $"Level {max} open" : "";
            card.AddChild(UiKit.MakeLabel(record, 16, Palette.Gold.Darkened(0.2f), HorizontalAlignment.Center).Place(0, CardH - 28, CardW, 20));
            var pick = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, TooltipText = $"{kin.Name}, {kin.Title}" };
            pick.Pressed += () => Choose(kin.Key);
            card.AddChild(pick.Place(0, 0, CardW, CardH));
        }
        if (chosen)
        {
            card.Position -= new Vector2(0, 6);
            card.AddChild(UiKit.MakeFrame(Palette.Gold));
        }
        return card;
    }

    /// <summary>The chosen character's rules, and the challenge level with the terms it brings.</summary>
    void BuildDetails()
    {
        var kin = Characters.Get(_character);
        var panel = UiKit.MakePanel().Place(CardsX, 400, 1140, 240);
        _board.AddChild(panel);
        panel.AddChild(Header($"HOW {kin.Name.ToUpperInvariant()} PLAYS", 24, 16));
        float y = 44;
        foreach (string rule in kin.Rules)
        {
            var line = UiKit.MakeLabel($"- {rule}", 16, Palette.Text).Wrapped().Place(24, y, 600, 60);
            panel.AddChild(line);
            y += 20 * Lines(rule, 64) + 8;
        }

        const float cx = 660;
        panel.AddChild(Header("CHALLENGE LEVEL", cx, 16));
        int max = Profile.MaxChallenge(_character);
        var down = UiKit.MakeButton("-", Palette.Text, 24, () => SetLevel(_level - 1)).Place(cx, 42, 52, 52);
        down.Disabled = _level <= 0;
        panel.AddChild(down);
        panel.AddChild(UiKit.MakeLabel($"{_level}", 48, _level > 0 ? Palette.Warn : Palette.Text, HorizontalAlignment.Center, outline: 6)
            .Place(cx + 56, 40, 80, 56));
        var up = UiKit.MakeButton("+", Palette.Text, 24, () => SetLevel(_level + 1)).Place(cx + 140, 42, 52, 52);
        up.Disabled = _level >= max;
        up.TooltipText = max >= Challenges.Max ? "The top level." : $"Clear Bram's debt at level {max} with {kin.Name} to open level {max + 1}.";
        panel.AddChild(up);
        panel.AddChild(UiKit.MakeLabel($"Renown x{Challenges.Multiplier(_level):0.##}", 24, Palette.Gold, outline: 4).Place(cx + 210, 48, 260, 30));
        panel.AddChild(UiKit.MakeLabel(max >= Challenges.Max ? "Every level open." : $"Levels open: 0 to {max}.", 16, Palette.Muted)
            .Place(cx + 210, 80, 260, 20));
        if (_level == 0)
        {
            panel.AddChild(UiKit.MakeLabel("The Guild's usual terms. Clear Bram's debt to open the next level: each adds one harsher term, " +
                                           "and a quarter more renown.", 16, Palette.Muted).Wrapped().Place(cx, 112, 456, 100));
            return;
        }
        var newest = Challenges.Get(_level);
        panel.AddChild(UiKit.MakeLabel($"{newest.Name}: {newest.Rule}", 16, Palette.Warn).Wrapped().Place(cx, 108, 456, 44));
        var earlier = Challenges.Upto(_level - 1).Select(c => c.Name).ToList();
        if (earlier.Count > 0)
        {
            var also = UiKit.MakeLabel($"And: {string.Join(", ", earlier)}.", 16, Palette.Muted).Wrapped().Place(cx, 156, 456, 72);
            also.MouseFilter = Control.MouseFilterEnum.Pass;
            also.TooltipText = string.Join("\n", Challenges.Upto(_level - 1).Select(c => $"{c.Level}. {c.Name}: {c.Rule}"));
            panel.AddChild(also);
        }
    }

    /// <summary>About how many lines a sentence wraps to, at so many characters a line.</summary>
    static int Lines(string text, int perLine) => (text.Length + perLine - 1) / perLine;

    public void Choose(string character)
    {
        if (!Profile.Unlocked(Characters.Get(character))) return;
        _character = character;
        _level = System.Math.Min(_level, Profile.MaxChallenge(character));
        Sfx.Play("tick", 1.2f, -8);
        Build();
    }

    public void SetLevel(int level)
    {
        level = System.Math.Clamp(level, 0, Profile.MaxChallenge(_character));
        if (level == _level) return;
        _level = level;
        Sfx.Play("tick", 1f + level * 0.05f, -8);
        Build();
    }

    public void OpenTalents()
    {
        if (_talents != null) return;
        _talents = Add(new TalentsView());
        _talents.ZIndex = 30;
        _talents.Closed += () =>
        {
            _talents = null;
            Build();
        };
    }

    public void Start()
    {
        if (_talents != null) return;
        Sfx.Play("bid");
        Game.StartRun(_character, _level);
    }

    void Back()
    {
        if (_talents != null) return;
        Game.ToTitle();
    }
}
