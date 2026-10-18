using System;
using System.Collections.Generic;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// Uncle Bram at your elbow: a face and one short tip at a time about what's in front of you. Tips that come up
/// together wait their turn; click the box for the next one. A seller's lesson waits until the tips already up have
/// been read; then the box shows the step to do and stays up until it's done (or skipped).
/// </summary>
public partial class MentorView : Control
{
    public const float W = 456, H = 128;
    readonly Queue<string> _queue = new();
    Label _text, _more;
    Button _skip;
    Panel _panel;

    /// <summary>Where the box's bottom edge sits: a long tip grows it upward from here.</summary>
    float _bottom;
    bool _lesson, _waiting, _showingTip;

    /// <summary>A lesson step waiting for the tips in front of it to be read.</summary>
    (string Text, bool Waiting)? _held;

    public bool Showing => Visible;

    /// <summary>A lesson step is up that's done by doing something, not by clicking.</summary>
    public bool WaitingForAction => Visible && _lesson && _waiting;

    /// <summary>The player clicked on past a lesson step that's just for reading.</summary>
    public event Action LessonRead;

    /// <summary>The player skipped the lesson.</summary>
    public event Action LessonSkipped;

    /// <summary>Set by the autoplay bot: every message Bram shows, for reading the tutorial through.</summary>
    public static Action<string> Trace;

    public override void _Ready()
    {
        Size = new Vector2(W, H);
        _bottom = Position.Y + H;
        MouseFilter = MouseFilterEnum.Stop;
        PivotOffset = new Vector2(0, H);
        _panel = UiKit.MakePanel(Palette.Gold.Darkened(0.25f)).Place(0, 0, W, H);
        _panel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_panel);
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

    /// <summary>
    /// Shows a lesson step: one to do (it stays up) or one to read (click on). If a tip is still up, the step waits
    /// until it (and any after it) has been read.
    /// </summary>
    public void ShowLesson(string text, bool waiting)
    {
        if (!_lesson && Visible)
        {
            _held = (text, waiting);
            UpdateMore();
            return;
        }
        Begin(text, waiting);
    }

    void Begin(string text, bool waiting)
    {
        _held = null;
        _lesson = true;
        _waiting = waiting;
        _skip.Visible = true;
        Show(text);
    }

    /// <summary>The lesson's over: back to tips.</summary>
    public void EndLesson()
    {
        // Over before it began: the tips in front of it carry on as they were.
        if (_held != null)
        {
            _held = null;
            UpdateMore();
        }
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
        if (_queue.Count > 0) Show(_queue.Dequeue(), afterClick: true);
        else if (_held is var (text, waiting))
        {
            _showingTip = false;
            Begin(text, waiting);
        }
        else Visible = false;
    }

    void Show(string text, bool afterClick = false)
    {
        // A tip still up and unread when something else replaces it would never be seen: say so in the trace.
        if (Visible && _showingTip && !afterClick) Trace?.Invoke($"(replaced unread: {_text.Text})");
        Trace?.Invoke((_lesson ? (_waiting ? "[do] " : "[read] ") : "") + text);
        _showingTip = !_lesson;
        Visible = true;
        _text.Text = text;
        Fit(text);
        UpdateMore();
        Scale = new Vector2(0.96f, 0.9f);
        CreateTween().TweenProperty(this, "scale", Vector2.One, 0.14f).SetTrans(Tween.TransitionType.Back);
        Main.Instance.Sfx.Play("blip", 0.8f, -10);
    }

    /// <summary>Grows the box upward (its bottom edge stays put) when the text needs more than four lines.</summary>
    void Fit(string text)
    {
        float textHeight = UiKit.Font.GetMultilineStringSize(text, HorizontalAlignment.Left, W - 116, 16).Y;
        float h = Mathf.Max(H, Mathf.Ceil(textHeight) + 40);
        Size = new Vector2(W, h);
        Position = new Vector2(Position.X, _bottom - h);
        PivotOffset = new Vector2(0, h);
        _panel.Size = new Vector2(W, h);
        _text.Size = new Vector2(W - 116, h - 30);
        _more.Position = new Vector2(104, h - 26);
    }

    void UpdateMore()
    {
        int more = _queue.Count + (_held != null ? 1 : 0);
        _more.Text = _lesson ? (_waiting ? "do it to go on" : "click to go on") : more > 0 ? $"click for more ({more})" : "click to close";
    }
}
