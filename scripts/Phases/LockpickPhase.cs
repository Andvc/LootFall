using System;
using Godot;
using LootFall.Core;
using LootFall.Ui;

namespace LootFall.Phases;

/// <summary>
/// The lock between you and your chest. Timing mini-game: the pick sweeps around the lock; press when it's inside the
/// green zone to set a pin. The bright centre is a perfect set. Three slips and the pick snaps, which means forcing the
/// lid. A masterful pick leaves everything pristine (+25%); the crowbar scratches everything (-10%) and breaks glass.
/// The first time, Bram explains, and the pick waits until you've read him.
/// </summary>
public partial class LockpickPhase : Phase
{
    public const int MaxSlips = 3;
    const float Radius = 120f;
    static readonly Vector2 Center = new(640, 348);

    readonly Random _rng = new();
    Purchase _purchase;
    LockTier _lock;
    Painter _dial;
    Label _status;
    Button _crowbar;
    float _angle = -90f, _speed, _dir = 1f;
    float _zoneCenter, _zoneWidth, _perfectWidth;
    float _zoneFlash, _slipFlash;
    int _set, _perfects, _slips;
    MentorView _mentor;

    public bool Done { get; private set; }
    public MentorView Mentor => _mentor;

    /// <summary>Bram is explaining: the pick holds still and presses don't count.</summary>
    bool Waiting => _mentor.Showing;
    public bool InPerfectZone => !Done && MathF.Abs(AngleDiff(_angle, _zoneCenter)) <= _perfectWidth / 2f - 0.5f;

    public override void _Ready()
    {
        _purchase = State.Opening;
        _lock = State.Oiled(_purchase.Chest.Lock);
        _speed = _lock.NeedleSpeed;
        // Fine lockpicks give a wider sweet spot.
        _zoneWidth = _lock.ZoneDegrees * (State.Has("picks") ? 1.35f : 1f);
        _perfectWidth = _zoneWidth * 0.4f;
        PlaceZone();

        var metal = Palette.Lock(_lock) ?? Palette.MetalLight;
        Add(UiKit.MakeShop(State.Tier, 0.38f));
        Add(UiKit.MakeLabel("PICK THE LOCK", 32, Palette.Text, HorizontalAlignment.Center, outline: 4).Place(0, 64, 1280, 40));
        Add(UiKit.MakeLabel($"{_lock.Name}: {_lock.Pins} pins", 16, metal, HorizontalAlignment.Center, outline: 4).Place(0, 102, 1280, 24));

        _dial = Add(new Painter(DrawDial) { Position = Center });
        _status = Add(UiKit.MakeLabel("[Space] or [Left Click] when the pick is in the green.", 16, Palette.Text, HorizontalAlignment.Center, outline: 4)
            .Place(0, 566, 1280, 24));
        Add(UiKit.MakeLabel("Clean pick: full value.   No slips, mostly perfect: Masterful, +25%.", 16, Palette.Muted, HorizontalAlignment.Center, outline: 4)
            .Place(0, 592, 1280, 20));

        // Your own slip says whether there's glass in there.
        bool glass = _purchase.Slip.Count(Category.Potions) > 0;
        string warning = glass ? "Your slip says there's glass in there. Forcing it will break it." : "Forcing it is quick, but it scratches everything and breaks glass.";
        Add(UiKit.MakeLabel(warning, 16, glass ? Palette.Warn : Palette.Muted, HorizontalAlignment.Center, outline: 4).Place(0, 688, 1280, 20));
        _crowbar = Add(UiKit.MakeButton("Force it with a crowbar  [F]", Palette.Bad, 16, Force).Place(480, 624, 320, 52));
        _mentor = Add(new MentorView { Position = new Vector2(24, 300), ZIndex = 20 });
        if (State.FirstTime("lockpick")) _mentor.Say(Hints.Get("lockpick"));
        Game.Moment("06_lockpick", 0.6);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!Done && !Waiting) _angle = Mathf.Wrap(_angle + _dir * _speed * dt, 0f, 360f);
        _zoneFlash = Mathf.Max(0, _zoneFlash - dt * 3f);
        _slipFlash = Mathf.Max(0, _slipFlash - dt * 2.5f);
        _dial.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Space) TryPin();
            else if (key.Keycode == Key.F) Force();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            TryPin();
        }
    }

    public void TryPin()
    {
        if (Done || Waiting) return;
        float d = MathF.Abs(AngleDiff(_angle, _zoneCenter));
        if (d <= _zoneWidth / 2f)
        {
            bool perfect = d <= _perfectWidth / 2f;
            _set++;
            if (perfect) _perfects++;
            _zoneFlash = 1f;
            Sfx.Play(perfect ? "pin_perfect" : "pin", 1f + _set * 0.08f);
            Game.Shake(perfect ? 0.14f : 0.08f);
            FloatText(perfect ? "PERFECT!" : "SET", Center + new Vector2(0, 70), perfect ? Palette.Gold : Palette.Good, perfect ? 32 : 24);

            if (_set >= _lock.Pins)
            {
                bool masterful = _slips == 0 && _perfects * 2 >= _lock.Pins;
                Finish(masterful ? PickGrade.Masterful : PickGrade.Clean);
                return;
            }
            _speed *= 1.1f;
            _dir = -_dir;
            PlaceZone();
        }
        else
        {
            _slips++;
            _slipFlash = 1f;
            Sfx.Play("miss");
            Game.Shake(0.2f);
            FloatText("SLIP!", Center + new Vector2(0, 70), Palette.Bad, 24);
            if (_slips >= MaxSlips) Snap();
        }
    }

    public void Force()
    {
        if (Done || Waiting) return;
        Finish(PickGrade.Forced);
    }

    async void Snap()
    {
        Done = true;
        _crowbar.Disabled = true;
        Sfx.Play("snap");
        Game.Shake(0.5f);
        Game.Flash(Palette.Bad, 0.3f);
        _status.Text = "The pick snapped! Out comes the crowbar...";
        _status.AddThemeColorOverride("font_color", Palette.Bad);
        await Wait(1.1);
        if (!Alive) return;
        Finish(PickGrade.Forced);
    }

    async void Finish(PickGrade grade)
    {
        Done = true;
        _crowbar.Disabled = true;
        _purchase.Grade = grade;
        if (grade == PickGrade.Forced && _purchase.Chest.Force(State.Rng, State.ForceSturdier).Count > 0)
        {
            Sfx.Play("shatter");
            FloatText("CRASH", Center + new Vector2(0, -60), Palette.Bad, 32);
        }

        var (title, note, color, sound) = grade switch
        {
            PickGrade.Masterful => ("MASTERFUL!", "Not a scratch. Everything inside is pristine: +25%.", Palette.Gold, "win"),
            PickGrade.Clean => ("CLICK.", "A clean pick. Everything inside is safe.", Palette.Good, "sting"),
            _ => ("CRACK.", "Forced open: everything's scratched, and any glass is broken.", Palette.Bad, "crowbar"),
        };
        Sfx.Play(sound);
        if (grade == PickGrade.Forced) Sfx.Play("strain", 0.8f);
        Game.Shake(grade == PickGrade.Forced ? 0.45f : 0.25f);
        _status.Text = note;
        _status.AddThemeColorOverride("font_color", color);
        FloatText(title, Center, color, 56);

        await Wait(1.5);
        if (!Alive) return;
        Game.SwitchTo(new OpeningPhase());
    }

    void PlaceZone()
    {
        // Always ahead of the pick in its direction of travel, so there's time to react.
        _zoneCenter = Mathf.Wrap(_angle + _dir * (120f + (float)_rng.NextDouble() * 120f), 0f, 360f);
    }

    static float AngleDiff(float a, float b) => Mathf.Wrap(a - b + 180f, 0f, 360f) - 180f;

    Vector2 PinOffset(int i) => new((i - (_lock.Pins - 1) / 2f) * 46f, -Radius - 58f);

    void DrawDial(Painter p)
    {
        var metal = Palette.Lock(_lock) ?? Palette.MetalLight;
        p.DrawCircle(Vector2.Zero, Radius + 36, metal.Darkened(0.6f));
        p.DrawCircle(Vector2.Zero, Radius + 26, metal.Darkened(0.35f));
        p.DrawCircle(Vector2.Zero, Radius - 13, new Color("#16121e"));
        p.DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 96, new Color("#2b2338"), 18, false);

        if (!Done)
        {
            var zone = new Color("#3fbf6a").Lerp(Colors.White, _zoneFlash);
            p.DrawArc(Vector2.Zero, Radius, Rad(_zoneCenter - _zoneWidth / 2), Rad(_zoneCenter + _zoneWidth / 2), 24, zone, 18, false);
            p.DrawArc(Vector2.Zero, Radius, Rad(_zoneCenter - _perfectWidth / 2), Rad(_zoneCenter + _perfectWidth / 2), 12,
                new Color("#c8ffd8"), 18, false);
        }

        p.DrawCircle(new Vector2(0, -16), 18, Colors.Black);
        p.DrawColoredPolygon(new[] { new Vector2(-9, -6), new Vector2(9, -6), new Vector2(15, 40), new Vector2(-15, 40) }, Colors.Black);

        var dir = Vector2.FromAngle(Rad(_angle));
        var pickColor = new Color("#e8e2d6");
        p.DrawLine(dir * 26, dir * (Radius + 22), pickColor, 5, false);
        p.DrawLine(dir * (Radius + 22), dir * (Radius + 22) + dir.Orthogonal() * 10, pickColor, 5, false);
        p.DrawCircle(Vector2.Zero, 8, pickColor);

        if (_slipFlash > 0)
            p.DrawArc(Vector2.Zero, Radius + 30, 0, Mathf.Tau, 96, Palette.Bad with { A = _slipFlash }, 7, false);

        for (int i = 0; i < _lock.Pins; i++)
        {
            var pos = PinOffset(i);
            bool isSet = i < _set;
            p.DrawCircle(pos, 15, Palette.Panel);
            p.DrawArc(pos, 15, 0, Mathf.Tau, 32, isSet ? Palette.Good : Palette.PanelBorder, 3, false);
            if (isSet) p.DrawCircle(pos, 9, Palette.Good);
        }

        for (int i = 0; i < MaxSlips; i++)
        {
            var pos = new Vector2((i - 1) * 40f, Radius + 54f);
            var color = i < _slips ? Palette.Bad : Palette.Muted;
            p.DrawLine(pos + new Vector2(-12, 8), pos + new Vector2(10, -10), color, 4, false);
            p.DrawCircle(pos + new Vector2(-12, 8), 5, color);
            if (i < _slips) p.DrawLine(pos + new Vector2(-4, -8), pos + new Vector2(6, 4), Palette.Bg, 3);
        }
        p.DrawString(UiKit.Font, new Vector2(-60, Radius + 90), "slips left", HorizontalAlignment.Center, 120, 16, Palette.Muted);
    }

    static float Rad(float degrees) => Mathf.DegToRad(degrees);
}
