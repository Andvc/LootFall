using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// A look through the keyhole. It shows exactly how much you can see: one card per thing inside, lit if it's near
/// enough to the keyhole to make out (how many depends on your lens), "out of sight" otherwise. Things you've seen
/// before are named from the ledger. Shaking the chest moves things around, so a second peek can show something else.
/// </summary>
public partial class PeepholeView : Node2D
{
    const float SlotW = 150, SlotH = 196, Gap = 12, SlotY = 196;

    readonly List<(int Index, ItemDef Item, Vector2 Position, float Radius)> _contents;
    readonly int _reach, _peeksLeft;
    readonly Ledger _ledger;
    bool _closing;

    /// <summary>Indexes (into the chest's items, then stones) of what this peek showed.</summary>
    public IReadOnlyList<int> Seen { get; }

    public event Action Closed;

    public PeepholeView() { }

    /// <param name="contents">What's inside and where, from the shake box.</param>
    /// <param name="reach">How many things the lens shows, nearest the keyhole first.</param>
    public PeepholeView(IEnumerable<(int Index, ItemDef Item, Vector2 Position, float Radius)> contents, int reach, int peeksLeft, Ledger ledger)
    {
        _contents = contents.ToList();
        _reach = reach;
        _peeksLeft = peeksLeft;
        _ledger = ledger;
        // The keyhole sits in the middle of the front, so whatever has settled nearest the middle is what you see.
        Seen = _contents.OrderBy(c => c.Position.Length()).Take(reach).Select(c => c.Index).ToList();
    }

    public override void _Ready()
    {
        var blocker = new ColorRect { Color = new Color(0.02f, 0.015f, 0.03f, 0.96f), MouseFilter = Control.MouseFilterEnum.Stop }
            .Place(-100, -100, 1480, 920);
        blocker.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) Close();
        };
        AddChild(blocker);

        int total = _contents.Count, seen = Seen.Count;
        AddChild(UiKit.MakeLabel("THROUGH THE KEYHOLE", 32, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 80, 1280, 40));
        string coverage = seen >= total ? $"You can see all {total} things inside." : $"You can make out {seen} of the {total} things inside.";
        AddChild(UiKit.MakeLabel(coverage, 24, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(0, 128, 1280, 32));

        float width = total * SlotW + (total - 1) * Gap;
        float x0 = 640 - width / 2;
        var ordered = _contents.OrderBy(c => Seen.Contains(c.Index) ? 0 : 1).ThenBy(c => c.Index).ToList();
        for (int i = 0; i < ordered.Count; i++)
            AddChild(MakeSlot(ordered[i].Item, Seen.Contains(ordered[i].Index), x0 + i * (SlotW + Gap)));

        bool hiddenGlow = _contents.Any(c => c.Item is { Glows: true } && !Seen.Contains(c.Index));
        if (hiddenGlow)
            AddChild(UiKit.MakeLabel("A warm glow comes from somewhere you can't see.", 16, new Color("#ffb347"), HorizontalAlignment.Center)
                .Place(0, SlotY + SlotH + 24, 1280, 20));

        string footer = seen >= total
            ? $"Peeks left today: {_peeksLeft}."
            : $"Shake the chest to move things around, then peek again to see something else. Peeks left today: {_peeksLeft}.";
        AddChild(UiKit.MakeLabel(footer, 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 560, 1280, 20));
        AddChild(UiKit.MakeLabel("Click anywhere to look away", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 600, 1280, 20));

        Main.Instance.Sfx.Play("peek", 1f, -6);
        Main.Instance.Sfx.Play("lantern", 1f, -8);
        Modulate = Colors.Transparent;
        CreateTween().TweenProperty(this, "modulate", Colors.White, 0.18f);
    }

    Control MakeSlot(ItemDef item, bool visible, float x)
    {
        var slot = UiKit.MakePanel(visible ? Palette.Gold.Darkened(0.35f) : Palette.PanelBorder).Place(x, SlotY, SlotW, SlotH);
        slot.MouseFilter = Control.MouseFilterEnum.Ignore;
        var center = new Vector2(SlotW / 2, 78);
        if (!visible)
        {
            slot.Modulate = new Color(1, 1, 1, 0.6f);
            slot.AddChild(UiKit.MakeLabel("?", 40, Palette.Muted, HorizontalAlignment.Center).Place(0, 50, SlotW, 48));
            slot.AddChild(UiKit.MakeLabel("out of sight", 16, Palette.Muted, HorizontalAlignment.Center).Place(0, 150, SlotW, 20));
            return slot;
        }

        // Lantern light behind whatever you can see.
        slot.AddChild(new Painter(p =>
        {
            for (int i = 5; i >= 1; i--) p.DrawCircle(center, 14 + i * 11, new Color("#ffb060") with { A = 0.07f });
        }) { Material = Fx.Additive() });

        string name;
        if (item == null)
        {
            slot.AddChild(new Painter(p => DrawStone(p, center, 30)));
            name = "A heavy bundle of rags";
        }
        else
        {
            var texture = GD.Load<Texture2D>($"res://assets/sprites/items/{item.LookKey}.png");
            int scale = Math.Clamp((int)(96f / Math.Max(texture.GetWidth(), texture.GetHeight())), 1, 3);
            slot.AddChild(new Sprite2D { Texture = texture, Position = center, Scale = Vector2.One * scale, Modulate = new Color(1f, 0.92f, 0.8f) });
            var looks = item.LooksLike != null ? ItemCatalog.Get(item.LooksLike) : item;
            name = _ledger.Knows(looks) ? $"{looks.Name}, {looks.Value}g" : "Something new";
        }
        var label = UiKit.MakeLabel(name, 16, Palette.Text, HorizontalAlignment.Center).Wrapped().Place(8, 140, SlotW - 16, 48);
        label.VerticalAlignment = VerticalAlignment.Center;
        slot.AddChild(label);
        return slot;
    }

    static void DrawStone(Painter p, Vector2 c, float r)
    {
        var sack = new[]
        {
            new Vector2(-r, r * 0.55f), new Vector2(-r * 1.05f, -r * 0.1f), new Vector2(-r * 0.6f, -r * 0.7f),
            new Vector2(-r * 0.12f, -r * 0.82f), new Vector2(r * 0.12f, -r * 1.05f), new Vector2(r * 0.3f, -r * 0.78f),
            new Vector2(r * 0.85f, -r * 0.5f), new Vector2(r * 1.05f, r * 0.2f), new Vector2(r * 0.7f, r * 0.75f),
        }.Select(v => v + c).ToArray();
        p.DrawColoredPolygon(sack, new Color("#7d6a4c"));
        p.DrawLine(c + new Vector2(-r * 0.25f, -r * 0.78f), c + new Vector2(r * 0.35f, -r * 0.82f), new Color("#4a3a26"), 4);
        for (int i = 0; i < 4; i++)
            p.DrawLine(c + new Vector2(-r * 0.8f + i * r * 0.45f, r * 0.5f), c + new Vector2(-r * 0.6f + i * r * 0.45f, r * 0.1f), new Color("#5e4e36"), 2);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false })
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
        tween.TweenProperty(this, "modulate", Colors.Transparent, 0.15f);
        tween.TweenCallback(Callable.From(() =>
        {
            Closed?.Invoke();
            QueueFree();
        }));
    }
}
