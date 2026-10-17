using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The shop's ledger. Sounds: the rules of shaking, and one row per kind of thing your shop sees (its sound, how hard
/// you must shake to hear it, and what one is worth in a chest of each colour); click a row to hear it. Regulars: how
/// each seller you've met shakes, trades and behaves. Collection: everything you've found so far.
/// </summary>
public partial class LedgerView : Node2D
{
    // Ends above Uncle Bram's box, so his tips stay readable with the ledger open.
    const float PanelX = 40, PanelY = 66, PanelW = 1200, PanelH = 518;
    const float TableX = 64, RowH = 44;

    readonly GameState _state;
    Node2D _page;
    bool _closing;

    public event Action Closed;

    public LedgerView() { }

    public LedgerView(GameState state)
    {
        _state = state;
    }

    static Audio.Sfx Sfx => Main.Instance.Sfx;

    public override void _Ready()
    {
        var blocker = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop }.Place(-100, -100, 1480, 920);
        blocker.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) Close();
        };
        AddChild(blocker);
        AddChild(UiKit.MakePanel(Palette.Gold.Darkened(0.3f)).Place(PanelX, PanelY, PanelW, PanelH));
        AddChild(UiKit.MakeLabel("LEDGER", 32, Palette.Gold, outline: 4).Place(PanelX + 24, PanelY + 10, 200, 40));
        AddChild(UiKit.MakeButton("Sounds", Palette.Text, 16, () => ShowPage(0)).Place(PanelX + 220, PanelY + 14, 120, 36));
        AddChild(UiKit.MakeButton("Regulars", Palette.Text, 16, () => ShowPage(1)).Place(PanelX + 348, PanelY + 14, 140, 36));
        AddChild(UiKit.MakeButton("Collection", Palette.Text, 16, () => ShowPage(2)).Place(PanelX + 496, PanelY + 14, 160, 36));
        AddChild(UiKit.MakeButton("Close  [L]", Palette.Text, 16, Close).Place(PanelX + PanelW - 168, PanelY + 12, 144, 40));
        _page = new Node2D();
        AddChild(_page);
        ShowPage(0);

        Sfx.PlayGroup("book", 1f, -4);
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.15f);
    }

    /// <summary>0: sounds, 1: regulars, 2: collection.</summary>
    public void ShowPage(int page)
    {
        foreach (var child in _page.GetChildren()) child.QueueFree();
        if (page == 1) BuildRegulars();
        else if (page == 2) BuildCollection();
        else BuildSounds();
    }

    /// <summary>A grid: one row per kind, one column per quality, then magic and fake. Found things show; the rest are "?".</summary>
    void BuildCollection()
    {
        const float cell = 64, gridX = TableX + 140, gridY = PanelY + 92;
        string[] columns = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Magic", "Fake" };
        for (int c = 0; c < columns.Length; c++)
            _page.AddChild(UiKit.MakeLabel(columns[c], 16, c < 5 ? Palette.Of((Rarity)c) : c == 5 ? Palette.Magic : Palette.Muted, HorizontalAlignment.Center)
                .Place(gridX + c * (cell + 60) - 30, gridY - 26, cell + 60, 20));
        var entries = Collection.Entries.ToList();
        int row = 0;
        foreach (var kind in Categories.All)
        {
            float y = gridY + row * cell;
            _page.AddChild(UiKit.MakeKindIcon(kind.Category, new Vector2(TableX + 16, y + cell / 2 - 6)));
            _page.AddChild(UiKit.MakeLabel(kind.Name, 16, Palette.Text).Place(TableX + 40, y + cell / 2 - 16, 100, 20));
            var mine = entries.Where(e => e.Category == kind.Category).ToList();
            for (int i = 0; i < mine.Count; i++)
            {
                var entry = mine[i];
                int column = entry.Fake ? 6 : entry.Magic ? 5 : (int)entry.Quality;
                float x = gridX + column * (cell + 60);
                bool found = _state.Found.Contains(entry.Entry);
                var slot = UiKit.MakePanel(found ? Palette.Of(entry.Rarity) : Palette.PanelBorder).Place(x, y, cell - 10, cell - 10);
                slot.TooltipText = found ? $"{entry.Entry}: {entry.Value}g" : "Not found yet";
                slot.MouseFilter = Control.MouseFilterEnum.Pass;
                _page.AddChild(slot);
                if (found)
                {
                    var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{entry.Sprite}.png");
                    if (texture.GetWidth() <= 50 && texture.GetHeight() <= 50)
                        slot.AddChild(new Sprite2D { Texture = texture, Position = new Vector2((cell - 10) / 2, (cell - 10) / 2) });
                    else
                        slot.AddChild(UiKit.MakeKindIcon(kind.Category, new Vector2((cell - 10) / 2, (cell - 10) / 2)));
                }
                else
                {
                    slot.AddChild(UiKit.MakeLabel("?", 24, Palette.Muted, HorizontalAlignment.Center).Place(0, 16, cell - 10, 30));
                }
            }
            row++;
        }
        _page.AddChild(UiKit.MakeLabel($"Found {_state.Found.Count} of {Collection.Size}. Hover one to see its name and price.", 16, Palette.Muted)
            .Place(TableX, PanelY + PanelH - 34, PanelW - 48, 20));
    }

    void BuildSounds()
    {
        int tier = _state.Tier;
        var rules = new[]
        {
            "Light things rattle at a gentle shake. Metal needs a firm one. Soft things only sound at a hard shake.",
            "While metal clanks, you can't hear anything delicate. Shake gently to hear past it.",
            tier >= 2 ? "Dropped, a chest with a false bottom booms hollow. Glass may break." : null,
            tier >= 3 ? "Magic sounds like anything else, and nobody prices it. Under a witchlight, it glows. Worth x" + Categories.MagicMultiplier + "." : null,
            tier >= 4 ? "Gilded fakes are iron: they sound just like the real thing, but go quiet under a magnet." : null,
        }.Where(r => r != null).ToArray();
        _page.AddChild(UiKit.MakeLabel(string.Join("\n", rules.Select(r => "- " + r)), 16, Palette.Text).Place(TableX, PanelY + 64, PanelW - 48, 90));

        float y = PanelY + 160;
        Header("Kind", TableX + 48, y);
        Header("Sound", TableX + 220, y);
        Header("Shake", TableX + 330, y);
        Header("Note", TableX + 440, y);
        for (int c = 0; c < 4; c++) Header(Categories.ChestSkin((Rarity)c), TableX + 760 + c * 104, y);

        var kinds = Categories.UpTo(tier).ToList();
        for (int i = 0; i < kinds.Count; i++) _page.AddChild(MakeRow(kinds[i], y + 26 + i * RowH, tier));
        _page.AddChild(UiKit.MakeLabel("Worth: what one is worth on average in a chest of each colour. Click a row to hear it.", 16, Palette.Muted)
            .Place(TableX, PanelY + PanelH - 34, PanelW - 48, 20));
    }

    void Header(string text, float x, float y) => _page.AddChild(UiKit.MakeLabel(text, 16, Palette.Gold.Darkened(0.2f)).Place(x, y, 200, 20));

    Control MakeRow(CategoryDef kind, float y, int tier)
    {
        var row = new Panel { MouseFilter = Control.MouseFilterEnum.Stop }.Place(TableX - 8, y, PanelW - 32, RowH - 4);
        row.AddThemeStyleboxOverride("panel", UiKit.PanelBox(4));
        row.AddChild(UiKit.MakeKindIcon(kind.Category, new Vector2(28, (RowH - 4) / 2)));
        row.AddChild(UiKit.MakeLabel(kind.Name, 16, Palette.Text).Place(56, 10, 160, 20));
        row.AddChild(UiKit.MakeLabel(kind.Word, 16, Palette.Word).Place(228, 10, 100, 20));
        row.AddChild(UiKit.MakeLabel(Categories.LevelName(kind.StirsAt), 16, Palette.Level(kind.StirsAt)).Place(338, 10, 100, 20));
        string note = kind.Loud ? "loud: drowns out delicate things" : kind.Delicate ? "delicate: drowned out by clanks" : "soft: needs a hard shake";
        if (kind.FakeName != null && tier >= 4) note += "; Silas fakes it";
        row.AddChild(UiKit.MakeLabel(note, 16, Palette.Muted).Place(448, 10, 310, 20));
        for (int c = 0; c < 4; c++)
            row.AddChild(UiKit.MakeLabel($"~{Categories.Expected(kind.Category, (Rarity)c)}g", 16, Palette.Of((Rarity)c)).Place(768 + c * 104, 10, 96, 20));
        row.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            Sfx.PlayGroup(kind.Sound, kind.Pitch, 2f);
            row.PivotOffset = row.Size / 2;
            var bump = row.CreateTween();
            bump.TweenProperty(row, "scale", Vector2.One * 1.02f, 0.05f);
            bump.TweenProperty(row, "scale", Vector2.One, 0.12f);
        };
        return row;
    }

    void BuildRegulars()
    {
        var met = Sellers.All.Where(s => s.Tier <= _state.Tier && _state.Met.Contains(s.Key)).ToList();
        if (met.Count == 0)
        {
            _page.AddChild(UiKit.MakeLabel("You haven't met anyone yet.", 24, Palette.Muted, HorizontalAlignment.Center).Place(PanelX, 300, PanelW, 30));
            return;
        }
        for (int i = 0; i < met.Count; i++)
        {
            float x = TableX + i % 2 * 580, y = PanelY + 62 + i / 2 * 56;
            _page.AddChild(UiKit.MakeSprite($"res://assets/sprites/sellers/{met[i].Key}.png", new Vector2(x + 20, y + 24), 1));
            string trade = met[i].Trade switch { Trade.Haggle => "haggles", Trade.Offer => "you name the price", _ => "fixed price" };
            _page.AddChild(UiKit.MakeLabel($"{met[i].Name}: shakes {met[i].ShakeWord}, {trade}", 16, Palette.Gold).Place(x + 52, y + 2, 520, 20));
            _page.AddChild(UiKit.MakeLabel(met[i].Habit, 16, Palette.Text).Wrapped().Place(x + 52, y + 22, 510, 40));
        }
        int total = Sellers.All.Count(s => s.Tier <= _state.Tier);
        _page.AddChild(UiKit.MakeLabel($"{met.Count} of {total} met", 16, Palette.Muted).Place(PanelX + 690, PanelY + 24, 300, 20));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.L or Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void Close()
    {
        if (_closing || !IsInsideTree()) return;
        _closing = true;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.Transparent, 0.12f);
        tween.TweenCallback(Callable.From(() =>
        {
            Closed?.Invoke();
            QueueFree();
        }));
    }
}
