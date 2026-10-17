using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The shop's ledger. Things: one page per family, with its sound, the rules for telling things apart within it
/// (as you learn them), and every thing you've opened, with its price and weight; click one to hear it. Regulars:
/// each seller's pricing habit, once you've met them. Only shows what you've learned or could meet at your shop's size.
/// </summary>
public partial class LedgerView : Node2D
{
    // Ends above Uncle Bram's box, so his tips stay readable with the ledger open.
    const float PanelX = 40, PanelY = 66, PanelW = 1200, PanelH = 518;
    const float ListX = 64, ListW = 260, DetailX = 352, DetailW = 864, CellW = 136, CellH = 150;

    readonly Ledger _ledger;
    readonly int _tier;
    Node2D _page;
    Label _detail;
    bool _closing;
    Family _family = Family.Coins;

    public event Action Closed;

    public LedgerView() { }

    public LedgerView(Ledger ledger, int tier)
    {
        _ledger = ledger;
        _tier = tier;
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
        AddChild(UiKit.MakeButton("Things", Palette.Text, 16, () => ShowPage(false)).Place(PanelX + 220, PanelY + 14, 120, 36));
        AddChild(UiKit.MakeButton("Regulars", Palette.Text, 16, () => ShowPage(true)).Place(PanelX + 348, PanelY + 14, 140, 36));
        AddChild(UiKit.MakeButton("Close  [L]", Palette.Text, 16, Close).Place(PanelX + PanelW - 168, PanelY + 12, 144, 40));
        _page = new Node2D();
        AddChild(_page);
        _detail = UiKit.MakeLabel("", 16, Palette.Muted).Wrapped().Place(DetailX, PanelY + PanelH - 52, DetailW, 40);
        AddChild(_detail);
        ShowPage(false);

        Sfx.PlayGroup("book", 1f, -4);
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.15f);
    }

    void ShowPage(bool regulars)
    {
        foreach (var child in _page.GetChildren()) child.QueueFree();
        _detail.Text = "";
        if (regulars) BuildRegulars();
        else BuildThings();
    }

    void BuildThings()
    {
        var families = Families.All.Where(f => Families.Tier(f) <= _tier).ToArray();
        if (!families.Contains(_family)) _family = families[0];
        for (int i = 0; i < families.Length; i++)
        {
            var family = families[i];
            bool selected = family == _family;
            var button = UiKit.MakeButton($"{Families.Name(family)}: {Families.Word(family)}", selected ? Palette.Gold : Palette.Text, 16,
                () => { _family = family; ShowPage(false); });
            _page.AddChild(button.Place(ListX, PanelY + 72 + i * 56, ListW, 48));
        }

        float y = PanelY + 72;
        _page.AddChild(UiKit.MakeLabel($"{Families.Name(_family).ToUpperInvariant()} {Families.Word(_family)}", 32, Palette.Gold, outline: 4)
            .Place(DetailX, y, DetailW, 40));
        _page.AddChild(UiKit.MakeLabel(Families.Rule(_family), 16, Palette.Text).Place(DetailX, y + 44, DetailW, 20));
        var rules = _ledger.KnownVariants(_family).Select(v => "- " + Families.VariantRule(_family, v)).ToList();
        if (rules.Count > 1)
            _page.AddChild(UiKit.MakeLabel(string.Join("\n", rules), 16, Palette.Warn).Place(DetailX, y + 68, DetailW, 88));

        var items = ItemCatalog.UpTo(_tier).Where(d => d.Family == _family).OrderBy(d => d.Tier).ThenBy(d => d.Value).ToArray();
        for (int i = 0; i < items.Length; i++)
            _page.AddChild(MakeCell(items[i], DetailX + i * (CellW + 8), y + 160));
        int known = items.Count(_ledger.Knows);
        _detail.Text = known < items.Length
            ? $"{known} of {items.Length} known. You learn the rest by buying them and opening them. Click one to hear it."
            : "Click one to hear how it sounds in a chest.";
    }

    Control MakeCell(ItemDef item, float x, float y)
    {
        bool known = _ledger.Knows(item);
        var cell = new Panel { MouseFilter = Control.MouseFilterEnum.Stop }.Place(x, y, CellW, CellH);
        cell.AddThemeStyleboxOverride("panel", UiKit.PanelBox(4));
        if (!known)
        {
            cell.Modulate = new Color(1, 1, 1, 0.45f);
            cell.AddChild(UiKit.MakeLabel("?", 40, Palette.Muted, HorizontalAlignment.Center).Place(0, 44, CellW, 48));
            return cell;
        }

        var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{item.Key}.png");
        int scale = Math.Max(1, (int)MathF.Min(64f / texture.GetHeight(), 96f / texture.GetWidth()));
        cell.AddChild(new TextureRect { Texture = texture, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore }
            .Place((CellW - texture.GetWidth() * scale) / 2, 8 + (64 - texture.GetHeight() * scale) / 2, texture.GetWidth() * scale, texture.GetHeight() * scale));
        var name = UiKit.MakeLabel(item.Name, 16, Palette.Text, HorizontalAlignment.Center).Wrapped().Place(4, 76, CellW - 8, 40);
        name.VerticalAlignment = VerticalAlignment.Center;
        cell.AddChild(name);
        cell.AddChild(UiKit.MakeLabel($"{item.Value}g  {Describe.Pounds(item.Weight)}", 16, Palette.Gold, HorizontalAlignment.Center)
            .Place(0, 120, CellW, 20));
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

    /// <summary>Shows an entry's sound and tells, as hovering it would.</summary>
    public void Show(ItemDef item)
    {
        var tells = new[]
        {
            Families.Describe(item.Family, item.Variant),
            item.Magic > 0 ? "magic" : null,
            item.Magnetic ? "iron" : null,
            item.Glows ? "glows" : null,
            item.Fragile ? "breaks if forced" : null,
        }.Where(t => t != null);
        _detail.Text = $"{item.Name}: {string.Join(", ", tells)}. {item.Flavor}";
    }

    void BuildRegulars()
    {
        var met = Sellers.All.Where(s => s.Tier <= _tier && _ledger.Knows(s)).ToList();
        if (met.Count == 0)
        {
            _page.AddChild(UiKit.MakeLabel("You haven't met anyone yet.", 24, Palette.Muted, HorizontalAlignment.Center).Place(PanelX, 300, PanelW, 30));
            return;
        }
        for (int i = 0; i < met.Count; i++)
        {
            float x = ListX + i % 2 * 580, y = PanelY + 76 + i / 2 * 64;
            _page.AddChild(UiKit.MakeSprite($"res://assets/sprites/sellers/{met[i].Key}.png", new Vector2(x + 20, y + 24), 1));
            _page.AddChild(UiKit.MakeLabel(met[i].Name, 16, Palette.Gold).Place(x + 52, y + 2, 500, 20));
            _page.AddChild(UiKit.MakeLabel(met[i].Quirk, 16, Palette.Text).Wrapped().Place(x + 52, y + 22, 510, 40));
        }
        int total = Sellers.All.Count(s => s.Tier <= _tier);
        _detail.Text = $"{met.Count} of {total} regulars met. Everyone has a habit when they price a chest. That's where the profit is.";
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
