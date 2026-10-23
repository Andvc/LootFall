using System;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>One gear slot: its item in a frame of its rarity (or the slot's name when empty), with a tooltip.</summary>
public partial class SlotView : Control
{
    public const float S = 70;
    public Slot Slot { get; }
    Item _item;
    Node2D _icon;
    NinePatchRect _frame;
    Label _level, _empty;

    public SlotView(Slot slot)
    {
        Slot = slot;
        Size = new Vector2(S, S);
        MouseFilter = MouseFilterEnum.Pass;
        AddChild(UiKit.MakeSlot());
        _empty = UiKit.MakeLabel(Slots.Get(slot).Name.ToUpperInvariant(), 16, Palette.Dim, HorizontalAlignment.Center).Place(0, S / 2 - 10, S, 20);
        AddChild(_empty);
        _level = UiKit.MakeLabel("", 16, Palette.Text, HorizontalAlignment.Right, outline: 4).Place(0, S - 22, S - 6, 20);
        _level.ZIndex = 2;
        AddChild(_level);
    }

    public void Set(Item item)
    {
        _item = item;
        _icon?.QueueFree();
        _frame?.QueueFree();
        _icon = null;
        _frame = null;
        _empty.Visible = item == null;
        _level.Text = item == null ? "" : item.Level.ToString();
        TooltipText = item == null ? "" : item.Name;
        if (item == null) return;
        _frame = UiKit.MakeFrame(Palette.Of(item.Rarity));
        AddChild(_frame);
        _icon = UiKit.MakeItemIcon(item, new Vector2(S / 2, S / 2), 64);
        _icon.ZIndex = 1;
        AddChild(_icon);
        MoveChild(_level, -1);
    }

    /// <summary>A flash of colour over the slot (something just went in).</summary>
    public void Flash(Color color)
    {
        var flash = new ColorRect { Color = color with { A = 0.7f }, MouseFilter = MouseFilterEnum.Ignore }.Place(4, 4, S - 8, S - 8);
        flash.ZIndex = 3;
        AddChild(flash);
        var t = flash.CreateTween();
        t.TweenProperty(flash, "color:a", 0f, 0.5f);
        t.TweenCallback(Callable.From(flash.QueueFree));
        Fx.Bump(this, 1.15f, 0.25f);
    }

    public override GodotObject _MakeCustomTooltip(string forText) => _item == null ? null : ItemTooltip.Make(_item);
}

/// <summary>A panel describing an item: rarity, level, name, every stat and its legendary power.</summary>
public static class ItemTooltip
{
    public static Control Make(Item item)
    {
        var color = Palette.Of(item.Rarity);
        var lines = item.Main.Select(Stats.Describe).Concat(item.Affixes.Select(Stats.Describe)).ToList();
        string effect = item.Effect == Effect.None ? null
            : $"{Effects.Get(item.Effect).Name}: {Effects.Get(item.Effect).Describe(Effects.Magnitude(item.Rarity))}";
        float h = 64 + lines.Count * 20 + (effect != null ? 44 : 0) + 22;
        var root = new Control { CustomMinimumSize = new Vector2(300, h), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(UiKit.MakePanel(color).Place(0, 0, 300, h));
        root.AddChild(UiKit.MakeLabel($"{Rarities.Get(item.Rarity).Name} {Slots.Get(item.Slot).Name}  -  Lv {item.Level}", 16, color).Place(12, 8, 280, 20));
        root.AddChild(UiKit.MakeLabel(item.Name, 16, Palette.Text, outline: 4).Place(12, 30, 280, 20));
        float y = 56;
        foreach (var line in lines)
        {
            root.AddChild(UiKit.MakeLabel(line, 16, Palette.Muted).Place(12, y, 280, 20));
            y += 20;
        }
        if (effect != null)
        {
            root.AddChild(UiKit.MakeLabel(effect, 16, Palette.Of(Rarity.Legendary)).Wrapped().Place(12, y + 2, 276, 40));
            y += 44;
        }
        root.AddChild(UiKit.MakeLabel($"Sells for {Num.Short(item.SellValue)}g", 16, Palette.Gold.Darkened(0.2f)).Place(12, y + 2, 280, 20));
        return root;
    }
}

/// <summary>
/// The knight's gear at the bottom left: eight slots (weapon, helmet, armour, shield; gloves, boots, ring, amulet),
/// his power, and what the gear adds up to.
/// </summary>
public partial class GearPanel : Node2D
{
    public const float W = 440, H = 280;
    static readonly Slot[] Order = { Slot.Weapon, Slot.Helmet, Slot.Armor, Slot.Shield, Slot.Gloves, Slot.Boots, Slot.Ring, Slot.Amulet };

    readonly GameState _g;
    readonly SlotView[] _slots = new SlotView[Slots.Count];
    Label _power;
    readonly Label[] _stats = new Label[9];
    Label _effects;
    Control _highlight;

    public GearPanel(GameState g)
    {
        _g = g;
    }

    public override void _Ready()
    {
        AddChild(UiKit.MakePanel(Palette.PanelBorder).Place(0, 0, W, H));
        AddChild(UiKit.MakeLabel("GEAR", 16, Palette.Muted).Place(14, 10, 100, 20));
        AddChild(UiKit.MakeIconRect("power", W - 186, 4, 32));
        _power = UiKit.MakeLabel("", 24, Palette.Gold, HorizontalAlignment.Right, outline: 4).Place(W - 154, 6, 140, 30);
        AddChild(_power);

        var grid = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(14, 40, 4 * SlotView.S + 24, 2 * SlotView.S + 8);
        AddChild(grid);
        for (int i = 0; i < Order.Length; i++)
        {
            var view = new SlotView(Order[i]).Place(i % 4 * (SlotView.S + 8), i / 4 * (SlotView.S + 8), SlotView.S, SlotView.S);
            _slots[(int)Order[i]] = view;
            grid.AddChild(view);
        }

        // Effects in play, listed on the right of the slots.
        _effects = UiKit.MakeLabel("", 16, Palette.Of(Rarity.Legendary)).Place(14 + 4 * SlotView.S + 36, 42, 100, 150);
        _effects.MouseFilter = Control.MouseFilterEnum.Pass;
        _effects.AddThemeConstantOverride("line_spacing", -2);
        AddChild(_effects);

        string[] icons = { "atk", "hp", "def", "spd", "crit", "critdmg", "gold", "chest", "luck" };
        for (int i = 0; i < _stats.Length; i++)
        {
            float x = 14 + i % 3 * 142, y = 200 + i / 3 * 24;
            AddChild(UiKit.MakeIconRect(icons[i], x, y - 4, 24));
            _stats[i] = UiKit.MakeLabel("", 16, i >= 6 ? Palette.Gold.Darkened(0.1f) : Palette.Text).Place(x + 28, y, 112, 20);
            AddChild(_stats[i]);
        }
        Refresh();
    }

    /// <summary>Centre of a slot, in screen coordinates.</summary>
    public Vector2 SlotCenter(Slot slot) => _slots[(int)slot].GetGlobalRect().GetCenter();

    public void Flash(Slot slot, Color color) => _slots[(int)slot].Flash(color);

    /// <summary>Marks the slot a revealed item would go in (null clears it).</summary>
    public void Highlight(Slot? slot)
    {
        _highlight?.QueueFree();
        _highlight = null;
        if (slot is not Slot s) return;
        var view = _slots[(int)s];
        _highlight = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }.Place(-4, -4, SlotView.S + 8, SlotView.S + 8);
        _highlight.AddChild(UiKit.MakeFrame(Colors.White));
        view.AddChild(_highlight);
        var t = _highlight.CreateTween().SetLoops();
        t.TweenProperty(_highlight, "modulate:a", 0.2f, 0.35f);
        t.TweenProperty(_highlight, "modulate:a", 1f, 0.35f);
    }

    public void Refresh()
    {
        foreach (var slot in Order) _slots[(int)slot].Set(_g.Worn(slot));
        var s = _g.Stats;
        _power.Text = Num.Short(_g.Power);
        _stats[0].Text = Num.Short(s.Atk);
        _stats[1].Text = Num.Short(s.Hp);
        _stats[2].Text = Num.Short(s.Def);
        _stats[3].Text = $"{s.Speed:0.00}/s";
        _stats[4].Text = Num.Pct(s.Crit);
        _stats[5].Text = "x" + s.CritDmg.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        _stats[6].Text = "+" + Num.Pct(s.GoldFind);
        _stats[7].Text = "+" + Num.Pct(s.ChestFind);
        _stats[8].Text = "+" + Num.Pct(s.Luck);
        // Six lines fit beside the slots: past that, the rest are counted (and listed in the tooltip).
        var active = Effects.All.Where(e => e.Effect != Effect.None && s.Has(e.Effect)).Select(e => e.Name).ToList();
        _effects.Text = string.Join("\n", active.Count <= 6 ? active : active.Take(5).Append($"+{active.Count - 5} more"));
        _effects.TooltipText = string.Join("\n", Effects.All.Where(e => e.Effect != Effect.None && s.Has(e.Effect))
            .Select(e => $"{e.Name}: {e.Describe(s.Mag(e.Effect))}"));
    }

    /// <summary>Flashes the power number (it just went up).</summary>
    public void BumpPower() => Fx.Bump(_power, 1.3f, 0.3f);
}
