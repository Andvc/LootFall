using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// A few lines between the people behind the story: Bram on the right, whoever he's talking to on the left, the one
/// speaking lit and the other in shadow. Click (or Space) for the next line; Skip jumps to the end. Then it hands on.
/// </summary>
public partial class ScenePhase : Phase
{
    const int FaceScale = 6;
    static readonly Vector2 Left = new(250, 300), Right = new(1030, 300);

    readonly List<Line> _lines;
    readonly string _title;
    readonly Action _then;
    readonly Dictionary<string, Node2D> _faces = new();
    Label _name, _text, _more;
    int _index = -1;
    bool _done;

    /// <summary>The bot reads the lines through.</summary>
    public static Action<string> Trace;

    public ScenePhase(List<Line> lines, Action then, string title = null)
    {
        _lines = lines;
        _then = then;
        _title = title;
    }

    public ScenePhase() { }

    public bool Done => _done;

    public override void _Ready()
    {
        Add(UiKit.MakeShop(State.Tier, 0.3f));
        if (_title != null)
            Add(UiKit.MakeLabel(_title, 32, Palette.Gold, HorizontalAlignment.Center, outline: 6).Place(0, 72, 1280, 40));

        // Bram stands on the right; everyone else takes the left, in the order they first speak.
        var speakers = _lines.Select(l => l.Speaker).Distinct().ToList();
        var others = speakers.Where(s => s != Story.Bram).ToList();
        for (int i = 0; i < speakers.Count; i++)
        {
            string key = speakers[i];
            var at = key == Story.Bram ? Right : Left + new Vector2(others.IndexOf(key) * 260, 0);
            _faces[key] = Add(MakeFace(key, at));
        }

        var box = Add(UiKit.MakePanel(Palette.Gold.Darkened(0.3f)).Place(140, 500, 1000, 160));
        _name = UiKit.MakeLabel("", 24, Palette.Gold, outline: 4).Place(24, 14, 600, 30);
        box.AddChild(_name);
        _text = UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(24, 50, 952, 80);
        box.AddChild(_text);
        _more = UiKit.MakeLabel("", 16, Palette.Muted, HorizontalAlignment.Right).Place(600, 128, 380, 20);
        box.AddChild(_more);
        var click = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None };
        click.Pressed += Advance;
        box.AddChild(click.Place(0, 0, 1000, 160));
        Add(UiKit.MakeButton("Skip", Palette.PanelBorder.Lightened(0.15f), 16, Finish).Place(1160, 670, 100, 34));
        Advance();
    }

    static Node2D MakeFace(string key, Vector2 at)
    {
        var face = new Node2D { Position = at };
        face.AddChild(UiKit.MakePanel(Palette.PanelBorder).Place(-128, -128, 256, 256));
        face.AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>($"res://assets/sprites/sellers/{key}.png"),
            Scale = Vector2.One * FaceScale,
        });
        face.AddChild(UiKit.MakeLabel(Story.Name(key), 24, Palette.Gold, HorizontalAlignment.Center, outline: 4).Place(-200, 134, 400, 30));
        return face;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space or Key.Enter })
        {
            GetViewport().SetInputAsHandled();
            Advance();
        }
    }

    /// <summary>The next line, or on once they've all been said.</summary>
    public void Advance()
    {
        if (_done) return;
        if (++_index >= _lines.Count)
        {
            Finish();
            return;
        }
        var line = _lines[_index];
        Trace?.Invoke($"{Story.Name(line.Speaker)}: {line.Text}");
        _name.Text = Story.Name(line.Speaker);
        _text.Text = line.Text;
        _more.Text = _index < _lines.Count - 1 ? "click for more" : "click to go on";
        foreach (var (key, face) in _faces)
        {
            bool speaking = key == line.Speaker;
            face.Modulate = speaking ? Colors.White : new Color(0.45f, 0.42f, 0.5f);
            if (!speaking) continue;
            face.Scale = Vector2.One * 1.04f;
            face.CreateTween().TweenProperty(face, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Back);
        }
        Sfx.Play("tick", line.Speaker == Story.Vane ? 0.7f : line.Speaker == Story.Bram ? 1f : 1.25f, -8);
        Game.Moment($"20_scene_{line.Speaker}", 0.4);
    }

    void Finish()
    {
        if (_done) return;
        _done = true;
        _then?.Invoke();
    }
}
