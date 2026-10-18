using System;
using System.Collections.Generic;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// Uncle Bram at your elbow: a face and one short tip at a time about what's in front of you. Tips that come up
/// together wait their turn; click the box for the next one. During a seller's lesson the box shows the step to do and
/// stays up until it's done (or skipped); tips that come up meanwhile wait until the lesson's over.
/// </summary>
public partial class MentorView : Control
{
    public const float W = 456, H = 128;
    readonly Queue<string> _queue = new();
    Label _text, _more;
    Button _skip;
    bool _lesson, _waiting;

    public bool Showing => Visible;

    /// <summary>A lesson step is up that's done by doing something, not by clicking.</summary>
    public bool WaitingForAction => Visible && _lesson && _waiting;

    /// <summary>The player clicked on past a lesson step that's just for reading.</summary>
    public event Action LessonRead;

    /// <summary>The player skipped the lesson.</summary>
    public event Action LessonSkipped;

    public override void _Ready()
    {
        Size = new Vector2(W, H);
        MouseFilter = MouseFilterEnum.Stop;
        PivotOffset = new Vector2(0, H);
        var panel = UiKit.MakePanel(Palette.Gold.Darkened(0.25f)).Place(0, 0, W, H);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(panel);
        AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>("res://assets/sprites/sellers/bram.png"),
            Position = new Vector2(52, 58),
            Scale = Vector2.One * 2,
        });
        AddChild(UiKit.MakeLabel(Hints.Mentor.ToUpperInvariant(), 16, Palette.Gold, HorizontalAlignment.Center).Place(4, 98, 96, 20));
        _text = UiKit.MakeLabel("", 16, Palette.Text).Wrapped().Place(104, 8, W - 116, H - 30);
        _text.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_text);
        _more = UiKit.MakeLabel("", 16, Palette.Gold, HorizontalAlignment.Right).Place(104, H - 26, W - 116, 20);
        AddChild(_more);
        _skip = UiKit.MakeButton("Skip lesson", Palette.Muted, 16, () => LessonSkipped?.Invoke()).Place(W - 132, -30, 132, 30);
        _skip.Visible = false;
        AddChild(_skip);
        GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Next();
        };
        Visible = false;
    }

    /// <summary>Shows a tip now, or after the ones already waiting (and after any lesson).</summary>
    public void Say(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!Visible && !_lesson)
        {
            Show(text);
            return;
        }
        _queue.Enqueue(text);
        UpdateMore();
    }

    /// <summary>Shows a lesson step: one to do (it stays up) or one to read (click on).</summary>
    public void ShowLesson(string text, bool waiting)
    {
        _lesson = true;
        _waiting = waiting;
        _skip.Visible = true;
        Show(text);
    }

    /// <summary>The lesson's over: back to tips.</summary>
    public void EndLesson()
    {
        if (!_lesson) return;
        _lesson = false;
        _skip.Visible = false;
        if (_queue.Count > 0) Show(_queue.Dequeue());
        else Visible = false;
    }

    /// <summary>Moves on to the next waiting tip, or puts the box away. A lesson step to do stays up.</summary>
    public void Next()
    {
        if (_lesson)
        {
            if (!_waiting) LessonRead?.Invoke();
            return;
        }
        if (_queue.Count > 0) Show(_queue.Dequeue());
        else Visible = false;
    }

    void Show(string text)
    {
        Visible = true;
        _text.Text = text;
        UpdateMore();
        Scale = new Vector2(0.96f, 0.9f);
        CreateTween().TweenProperty(this, "scale", Vector2.One, 0.14f).SetTrans(Tween.TransitionType.Back);
        Main.Instance.Sfx.Play("blip", 0.8f, -10);
    }

    void UpdateMore() => _more.Text = _lesson ? (_waiting ? "do it to go on" : "click to go on")
        : _queue.Count > 0 ? $"click for more ({_queue.Count})" : "click to close";
}
