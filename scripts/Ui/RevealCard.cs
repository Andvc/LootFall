using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// What just came out of the chest, against what the knight wears in that slot: its rarity, level and name, its stats
/// with the change from the worn item (green up, red down), its legendary power, the change in power, and the two
/// choices. The recommended one (equip an upgrade, sell the rest) pulses; Space takes it.
/// </summary>
public partial class RevealCard : Control
{
    public const float W = 360, H = 300;

    public Opening Opening { get; }
    public Button EquipButton { get; private set; }
    public Button SellButton { get; private set; }

    /// <summary>Where the item's picture sits on the card, in the card's coordinates.</summary>
    public static readonly Vector2 IconSpot = new(52, 84);

    public RevealCard(Opening opening, Action equip, Action sell)
    {
        Opening = opening;
        Size = new Vector2(W, H);
        MouseFilter = MouseFilterEnum.Stop;
        Build(equip, sell);
    }

    void Build(Action equip, Action sell)
    {
        var item = Opening.Item;
        var color = Palette.Of(item.Rarity);
        var current = Opening.Current;

        AddChild(UiKit.MakePanel(color).Place(0, 0, W, H));
        // Header: rarity and slot on a band of the rarity's colour, item level on the right.
        AddChild(new ColorRect { Color = color.Darkened(0.55f), MouseFilter = MouseFilterEnum.Ignore }.Place(6, 6, W - 12, 28));
        AddChild(UiKit.MakeLabel($"{Rarities.Get(item.Rarity).Name.ToUpperInvariant()} {Slots.Get(item.Slot).Name.ToUpperInvariant()}",
            16, color.Lightened(0.35f), outline: 4).Place(14, 9, 250, 22));
        AddChild(UiKit.MakeLabel($"Lv {item.Level}", 16, Palette.Text, HorizontalAlignment.Right, outline: 4).Place(W - 104, 9, 90, 22));

        // The picture, in a frame of its rarity.
        var frame = new Control { MouseFilter = MouseFilterEnum.Ignore }.Place(IconSpot.X - 42, IconSpot.Y - 42, 84, 84);
        frame.AddChild(UiKit.MakeSlot());
        frame.AddChild(UiKit.MakeFrame(color));
        AddChild(frame);
        AddChild(UiKit.MakeItemIcon(item, IconSpot, 80));
        if (Opening.Lucky)
        {
            var stamp = UiKit.MakeLabel("LUCKY!", 16, Palette.Good, HorizontalAlignment.Center, outline: 6).Place(IconSpot.X - 50, IconSpot.Y + 30, 100, 20);
            stamp.PivotOffset = new Vector2(50, 10);
            stamp.Rotation = -0.2f;
            AddChild(stamp);
        }

        // Name, then the main stats against what's worn.
        AddChild(UiKit.MakeLabel(item.Name, 16, color, outline: 4).Wrapped().Place(104, 40, W - 114, 40));
        float y = 82;
        foreach (var m in item.Main)
        {
            double was = current?.Get(m.Stat) ?? 0;
            MainRow(m, m.Value - was, y);
            y += 22;
        }

        // Affixes in two columns.
        for (int i = 0; i < item.Affixes.Length; i++)
        {
            var a = item.Affixes[i];
            bool loot = Stats.Get(a.Stat).Loot;
            AddChild(UiKit.MakeLabel(Stats.Describe(a), 16, loot ? Palette.Gold : new Color("#9fd2ff"))
                .Place(12 + i % 2 * 172, 134 + i / 2 * 20, 172, 20));
        }
        if (item.Effect != Effect.None)
        {
            var def = Effects.Get(item.Effect);
            AddChild(UiKit.MakeLabel($"{def.Name.ToUpperInvariant()}: {def.Describe(Effects.Magnitude(item.Rarity))}", 16, Palette.Of(Rarity.Legendary))
                .Wrapped().Place(12, 178, W - 24, 40));
        }

        // Power: the one number that says whether it's better.
        string power;
        Color powerColor;
        if (current == null)
        {
            power = "NEW SLOT!";
            powerColor = Palette.Good;
        }
        else if (Opening.PowerDelta >= 0)
        {
            power = $"POWER +{Num.Short(Math.Max(1, Opening.PowerDelta))}";
            powerColor = Palette.Good;
        }
        else
        {
            power = $"POWER -{Num.Short(-Opening.PowerDelta)}";
            powerColor = Palette.Bad;
        }
        AddChild(UiKit.MakeLabel(power, 24, powerColor, HorizontalAlignment.Right, outline: 6).Place(W - 214, 222, 200, 28));
        AddChild(UiKit.MakeLabel(current == null ? "nothing worn" : $"worn: {Rarities.Get(current.Rarity).Name} Lv {current.Level}", 16, Palette.Muted)
            .Place(14, 228, 170, 20));

        bool upgrade = Opening.Upgrade;
        EquipButton = UiKit.MakeButton("EQUIP [E]", upgrade ? Palette.Good.Darkened(0.2f) : new Color("#5a5470"), 16, equip).Place(12, 256, 162, 34);
        SellButton = UiKit.MakeButton($"SELL +{Num.Short(item.SellValue)} [S]", upgrade ? new Color("#5a5470") : Palette.Gold.Darkened(0.35f), 16, sell)
            .Place(W - 174, 256, 162, 34);
        AddChild(EquipButton);
        AddChild(SellButton);
        var pick = upgrade ? EquipButton : SellButton;
        pick.PivotOffset = pick.Size / 2;
        var pulse = pick.CreateTween().SetLoops();
        pulse.TweenProperty(pick, "scale", Vector2.One * 1.06f, 0.35f).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(pick, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>"HP 1.2K  +340": the stat, its value on this item, and how much more (or less) than what's worn.</summary>
    void MainRow(Affix stat, double delta, float y)
    {
        var def = Stats.Get(stat.Stat);
        AddChild(UiKit.MakeIconRect(def.Icon, 104, y - 3, 24));
        AddChild(UiKit.MakeLabel(def.Short, 16, Palette.Muted).Place(132, y, 44, 20));
        AddChild(UiKit.MakeLabel(Num.Short(Math.Round(stat.Value)), 16, Palette.Text, HorizontalAlignment.Right).Place(172, y, 80, 20));
        if (Math.Abs(delta) >= 0.5)
        {
            bool up = delta > 0;
            AddChild(UiKit.MakeLabel($"{(up ? "+" : "-")}{Num.Short(Math.Round(Math.Abs(delta)))}", 16, up ? Palette.Good : Palette.Bad, HorizontalAlignment.Right)
                .Place(256, y, 90, 20));
        }
    }

    /// <summary>Pops in from a little smaller, as if it rose out of the chest.</summary>
    public void Appear()
    {
        PivotOffset = new Vector2(W / 2, H);
        Scale = new Vector2(0.7f, 0.2f);
        Modulate = Colors.Transparent;
        var t = CreateTween().SetParallel();
        t.TweenProperty(this, "scale", Vector2.One, 0.22f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(this, "modulate", Colors.White, 0.12f);
    }
}
