using System.Collections.Generic;
using System.Linq;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The shake meter under the chest, which doubles as a record of what you heard: one column per shake strength
/// (gentle, firm, hard). The bar shows how hard you're shaking now; a column lights up while you're in it, gets a tick
/// once you've shaken that hard for a moment, and lists the sounds you heard at that strength.
/// </summary>
public partial class HearingChart : Node2D
{
    public const float W = 484, H = 92;
    const float ColumnW = 156, Gap = 8, BarY = 26, BarH = 12;

    static readonly Shake[] Levels = { Shake.Gentle, Shake.Firm, Shake.Hard };

    readonly RichTextLabel[] _words = new RichTextLabel[3];
    readonly Label[] _ticks = new Label[3];
    readonly bool[] _tried = new bool[3];
    Painter _painter;
    float _bar;
    Shake _level;

    public override void _Ready()
    {
        _painter = new Painter(DrawMeter);
        AddChild(_painter);
        for (int i = 0; i < 3; i++)
        {
            float x = i * (ColumnW + Gap);
            AddChild(UiKit.MakeLabel(Categories.LevelName(Levels[i]).ToUpperInvariant(), 16, Palette.Muted).Place(x + 10, 4, 100, 20));
            _ticks[i] = UiKit.MakeLabel("", 16, Palette.Good, HorizontalAlignment.Right).Place(x + ColumnW - 60, 4, 50, 20);
            AddChild(_ticks[i]);
            // Rich text, so a word that glowed under the witchlight can glow on its own.
            _words[i] = new RichTextLabel { BbcodeEnabled = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore }
                .Place(x + 10, BarY + BarH + 6, ColumnW - 20, 44);
            _words[i].AddThemeFontOverride("normal_font", UiKit.Font);
            _words[i].AddThemeFontSizeOverride("normal_font_size", 16);
            _words[i].AddThemeColorOverride("default_color", Palette.Word);
            AddChild(_words[i]);
        }
    }

    /// <summary>
    /// The live shake. <paramref name="speed"/> is the chest's speed in px/s; the bar fills each column across its
    /// own band of speeds, so a full gentle column means you're about to shake firmly.
    /// </summary>
    public void SetShake(float speed, Shake level)
    {
        _bar = speed < ShakeBox.GentleSpeed ? 0f
            : speed < ShakeBox.FirmSpeed ? (speed - ShakeBox.GentleSpeed) / (ShakeBox.FirmSpeed - ShakeBox.GentleSpeed)
            : speed < ShakeBox.HardSpeed ? 1f + (speed - ShakeBox.FirmSpeed) / (ShakeBox.HardSpeed - ShakeBox.FirmSpeed)
            : 2f + Mathf.Min(1f, (speed - ShakeBox.HardSpeed) / (ShakeBox.HardSpeed * 0.6f));
        _level = level;
        _painter.QueueRedraw();
    }

    /// <summary>What you heard at each strength, and which strengths you've tried, for the current magnet setting.</summary>
    public void Show(Hearing hearing, bool magnet)
    {
        for (int i = 0; i < 3; i++)
        {
            var level = Levels[i];
            _tried[i] = hearing.HasTried(level, magnet);
            _ticks[i].Text = _tried[i] ? "done" : "";
            var heard = hearing.At(level, magnet).ToList();
            _words[i].Text = heard.Count > 0
                ? string.Join(" ", heard.Select(c => hearing.Glowed(c) ? $"[color=#{Palette.Magic.ToHtml(false)}]{Categories.Get(c).Word}[/color]" : Categories.Get(c).Word))
                : _tried[i] ? $"[color=#{Palette.Muted.ToHtml(false)}]silence[/color]" : "";
        }
        _painter.QueueRedraw();
    }

    void DrawMeter(Painter p)
    {
        for (int i = 0; i < 3; i++)
        {
            float x = i * (ColumnW + Gap);
            bool active = _level == Levels[i];
            var frame = active ? Palette.Gold : _tried[i] ? Palette.Good.Darkened(0.4f) : Palette.PanelBorder;
            p.DrawRect(new Rect2(x, 0, ColumnW, H), new Color(0.06f, 0.05f, 0.09f, 0.88f));
            p.DrawRect(new Rect2(x, 0, ColumnW, H), frame, false, 3);
            // The bar: this column's share of the live shake.
            float fill = Mathf.Clamp(_bar - i, 0f, 1f);
            p.DrawRect(new Rect2(x + 9, BarY, ColumnW - 18, BarH), new Color(0, 0, 0, 0.6f));
            if (fill > 0)
                p.DrawRect(new Rect2(x + 9, BarY, (ColumnW - 18) * fill, BarH), LevelColor(Levels[i]));
        }
    }

    public static Color LevelColor(Shake level) => level switch
    {
        Shake.Gentle => new Color("#7fd3ff"),
        Shake.Firm => Palette.Gold,
        _ => Palette.Warn,
    };
}
