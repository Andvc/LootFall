using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The shop's ledger, one row per material. Known items show their word, price and weight; click one to hear it
/// rattle. Unknown ones are blank until you buy one and open it. Uncle Bram's margin notes explain sound-alikes.
/// </summary>
public partial class LedgerView : Node2D
{
    const float PanelX = 40, PanelY = 72, PanelW = 1200, PanelH = 600;
    const float CellW = 136, CellH = 82, CellGap = 6, RowH = 88, RowsY = 150, CellsX = 228;

    readonly Ledger _ledger;
    Label _name, _detail, _note;
    bool _closing;

    public event Action Closed;

    public LedgerView() { }

    public LedgerView(Ledger ledger)
    {
        _ledger = ledger;
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

        var panel = UiKit.MakePanel(Palette.Gold.Darkened(0.3f)).Place(PanelX, PanelY, PanelW, PanelH);
        AddChild(panel);
        AddChild(UiKit.MakeLabel("LEDGER", 32, Palette.Gold, outline: 4).Place(PanelX + 24, PanelY + 14, 300, 40));
        AddChild(UiKit.MakeLabel($"What each thing sounds like, weighs and fetches. {_ledger.Count} of {ItemCatalog.All.Length} known. Click one to hear it.",
            16, Palette.Muted).Place(PanelX + 24, PanelY + 56, 1000, 22));
        AddChild(UiKit.MakeButton("Close  [L]", Palette.Text, 16, Close).Place(PanelX + PanelW - 168, PanelY + 16, 144, 44));

        foreach (var family in Enum.GetValues<Family>())
        {
            int row = (int)family;
            float y = RowsY + row * RowH;
            AddChild(UiKit.MakeLabel(ItemCatalog.FamilyName(family), 16, Palette.Gold.Darkened(0.1f)).Wrapped().Place(PanelX + 24, y + 20, 150, 44));
            var items = ItemCatalog.All.Where(i => i.Family == family).ToArray();
            for (int i = 0; i < items.Length; i++)
                AddChild(MakeCell(items[i], CellsX + i * (CellW + CellGap), y));
        }

        float footY = RowsY + 5 * RowH + 2;
        AddChild(new ColorRect { Color = Palette.PanelBorder, MouseFilter = Control.MouseFilterEnum.Ignore }.Place(PanelX + 24, footY, PanelW - 48, 3));
        _name = AddChild2(UiKit.MakeLabel("", 24, Palette.Text).Place(PanelX + 24, footY + 10, 520, 30));
        _detail = AddChild2(UiKit.MakeLabel("", 16, Palette.Muted).Place(PanelX + 560, footY + 16, 600, 22));
        _note = AddChild2(UiKit.MakeLabel("Bram's notes and yours. Hover an entry to read it.", 16, Palette.Muted).Wrapped().Place(PanelX + 24, footY + 40, PanelW - 48, 22));

        Sfx.PlayGroup("book", 1f, -4);
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.15f);
    }

    T AddChild2<T>(T node) where T : Node
    {
        AddChild(node);
        return node;
    }

    Control MakeCell(ItemDef item, float x, float y)
    {
        bool known = _ledger.Knows(item);
        var cell = new Panel { MouseFilter = Control.MouseFilterEnum.Stop }.Place(x, y, CellW, CellH);
        cell.AddThemeStyleboxOverride("panel", UiKit.PanelBox(4));
        if (!known)
        {
            cell.Modulate = new Color(1, 1, 1, 0.45f);
            cell.AddChild(UiKit.MakeLabel("?", 32, Palette.Muted, HorizontalAlignment.Center).Place(0, 20, CellW, 40));
            return cell;
        }

        var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{item.Key}.png");
        int scale = Math.Max(1, (int)MathF.Min(40f / texture.GetHeight(), 60f / texture.GetWidth()));
        var icon = new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        icon.Place(8, (CellH - texture.GetHeight() * scale) / 2, texture.GetWidth() * scale, texture.GetHeight() * scale);
        cell.AddChild(icon);
        cell.AddChild(UiKit.MakeLabel($"'{item.Sound.Word}'", 16, Palette.Text, HorizontalAlignment.Right).Place(0, 8, CellW - 10, 20));
        cell.AddChild(UiKit.MakeLabel($"{item.Value}g", 16, Palette.Gold, HorizontalAlignment.Right).Place(0, 32, CellW - 10, 20));
        cell.AddChild(UiKit.MakeLabel(Describe.Pounds(item.Weight), 16, Palette.Muted, HorizontalAlignment.Right).Place(0, 56, CellW - 10, 20));
        cell.MouseEntered += () => Show(item);
        cell.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            Show(item);
            Sfx.Play(item.Sound, 2f);
            cell.PivotOffset = cell.Size / 2;
            var bump = cell.CreateTween();
            bump.TweenProperty(cell, "scale", Vector2.One * 1.08f, 0.05f);
            bump.TweenProperty(cell, "scale", Vector2.One, 0.12f);
        };
        return cell;
    }

    void Show(ItemDef item)
    {
        _name.Text = item.Name;
        _name.AddThemeColorOverride("font_color", Palette.Of(item.Rarity));
        _detail.Text = $"'{item.Sound.Word}'   {item.Value}g   {Describe.Pounds(item.Weight)}   {item.Rarity}" + (item.Fragile ? "   fragile" : "");
        var alikes = ItemCatalog.SoundAlikes(item).Where(_ledger.Knows).Select(i => i.Name).ToList();
        string note = Ledger.Note(item) ?? "";
        if (alikes.Count > 0) note = $"Sounds exactly like: {string.Join(", ", alikes)}. {note}";
        _note.Text = note.Length > 0 ? note : item.Flavor;
        _note.AddThemeColorOverride("font_color", note.Length > 0 ? Palette.Warn : Palette.Muted);
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
