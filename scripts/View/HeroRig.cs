using System;
using Godot;
using LootFall.Progression;

namespace LootFall.View;

/// <summary>What the knight wears, as the rig shows it: each visible piece's rarity (-1 when the slot is empty) and art
/// tier, and the aggregate signs of everything else (docs/art-direction.md, "The knight").</summary>
public sealed class GearLook
{
    public int Weapon = -1, Shield = -1, Helm = -1, Chest = -1, Tabard = -1, Cloak = -1, Pauldron = -1, Gauntlet = -1, Belt = -1, Boots = -1;
    public int WeaponTier, ShieldTier, HelmTier, ChestTier, PauldronTier, GauntletTier, BootsTier;
    public int Rings = -1, Neck = -1, Tattoo = -1, Trinket = -1, TrinketCount, Familiar = -1;
    public int Halo = -1, Wings = -1, Aura = -1, Starcrown = -1, Comet = -1, Nebula = -1, Eclipse = -1, Zenith = -1, Constellation = -1;

    public static GearLook Of(Hero h)
    {
        var g = new GearLook();
        var slots = h.C.Slots;
        int Best(Func<SlotDef, bool> which, out int tier)
        {
            int best = -1;
            tier = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                var it = h.Gear[s];
                if (it == null || !h.Open[s] || !which(slots[s])) continue;
                if (it.Rarity > best || (it.Rarity == best && Ink.ArtTier(Hero.Era(it.Ilvl)) > tier))
                {
                    best = it.Rarity;
                    tier = Ink.ArtTier(Hero.Era(it.Ilvl));
                }
            }
            return best;
        }
        int Named(params string[] names) => Best(d => Array.IndexOf(names, d.Name) >= 0, out _);
        g.Weapon = Best(d => d.Name == "Main Hand", out g.WeaponTier);
        g.Shield = Best(d => d.Name == "Off Hand", out g.ShieldTier);
        g.Helm = Best(d => d.Name == "Helm", out g.HelmTier);
        g.Chest = Best(d => d.Name == "Breastplate", out g.ChestTier);
        g.Tabard = Named("Tabard", "Tunic");
        g.Cloak = Named("Cloak", "Mantle");
        g.Pauldron = Best(d => d.Name.EndsWith("Pauldron"), out g.PauldronTier);
        g.Gauntlet = Best(d => d.Name.EndsWith("Gauntlet"), out g.GauntletTier);
        g.Belt = Named("Belt", "Sash");
        g.Boots = Best(d => d.Name.EndsWith(" Boot"), out g.BootsTier);
        g.Rings = Best(d => d.Kind == SlotKind.Jewelry && d.Name.EndsWith(" Ring") && d.Name != "Nose Ring", out _);
        g.Neck = Best(d => d.Kind == SlotKind.Jewelry && !d.Name.EndsWith(" Ring"), out _);
        g.Tattoo = Best(d => d.Kind == SlotKind.Tattoo, out _);
        g.Trinket = Best(d => d.Kind == SlotKind.Trinket, out _);
        for (int s = 0; s < slots.Length; s++)
            if (slots[s].Kind == SlotKind.Trinket && h.Gear[s] != null && h.Open[s]) g.TrinketCount++;
        g.Familiar = Named("Familiar");
        g.Halo = Named("Halo");
        g.Wings = Named("Wings");
        g.Aura = Named("Aura");
        g.Starcrown = Named("Starcrown");
        g.Comet = Named("Comet");
        g.Nebula = Named("Nebula");
        g.Eclipse = Named("Eclipse");
        g.Zenith = Named("Zenith");
        g.Constellation = Best(d => d.Name.StartsWith("Constellation"), out _);
        return g;
    }
}

/// <summary>
/// The knight: a cut-out rig posed in code every frame (docs/art-direction.md, "The knight"). Bones are computed by
/// forward kinematics from a few animation values (a swing from wind-up to strike, a walk cycle, lean, a lunge, a
/// flinch); each part is drawn on its bone, in inked shapes for now, as textures when the AI parts arrive. Origin at his
/// feet, facing right, <see cref="Look.HeroHeight"/> tall.
/// </summary>
public partial class HeroRig : Node2D
{
    public GearLook Gear = new();

    // Animation state.
    float _t, _walkPhase;
    /// <summary>0 standing, 1 walking (2 running): blended toward its target.</summary>
    float _walk, _walkTarget;
    /// <summary>The sword arm: -1 wound up, 0 at rest, 1 struck through.</summary>
    float _swing;
    float _swingFrom, _swingTo, _swingTime, _swingDur;
    float _lunge, _lean, _hurt, _cheer, _flash;
    Color _flashColor = Colors.White;
    double _freeze;

    // Bones this frame (rig space).
    Vector2 _pelvis, _neck, _shoulderN, _shoulderF, _elbowN, _elbowF, _handN, _handF, _hipN, _hipF, _kneeN, _kneeF, _ankleN, _ankleF;
    float _torsoA, _headA, _upperN, _foreN, _upperF, _foreF, _thighN, _shinN, _thighF, _shinF, _capeA;

    /// <summary>Where the blade's tip is (in the parent's space), for slashes and sparks.</summary>
    public Vector2 BladeTip => Position + Scale * _bladeTip;
    Vector2 _bladeTip;

    /// <summary>Where blows land: just in front of him at chest height.</summary>
    public Vector2 StrikePoint => Position + new Vector2(150, -150) * Scale;

    public float Height => Look.HeroHeight * Scale.Y;

    public override void _Process(double delta)
    {
        if (_freeze > 0)
        {
            _freeze -= delta;
            QueueRedraw();
            return;
        }
        float dt = (float)delta;
        _t += dt;
        _walk = Mathf.MoveToward(_walk, _walkTarget, dt * 3f);
        _walkPhase += dt * (6f + 5f * Mathf.Max(0, _walk - 1)) * Mathf.Min(1, _walk + 0.0001f);
        if (_swingDur > 0)
        {
            _swingTime += dt;
            float k = Mathf.Clamp(_swingTime / _swingDur, 0, 1);
            float e = _swingTo > _swingFrom ? 1 - (1 - k) * (1 - k) * (1 - k) : k * k * (3 - 2 * k);
            _swing = Mathf.Lerp(_swingFrom, _swingTo, e);
            if (k >= 1)
            {
                _swingDur = 0;
                // After a strike, ease back to rest.
                if (_swingTo > 0.5f) SwingTo(0, 0.22f);
            }
        }
        _lunge = Mathf.MoveToward(_lunge, _swing > 0.3f ? 22 * _swing : 0, dt * 260);
        _lean = Mathf.MoveToward(_lean, _swing > 0 ? 0.12f * _swing : _swing < 0 ? -0.06f * -_swing : Mathf.Min(0.2f, _walk * 0.1f), dt * 2.5f);
        _hurt = Mathf.MoveToward(_hurt, 0, dt * 4);
        _cheer = Mathf.MoveToward(_cheer, 0, dt * 0.8f);
        _flash = Mathf.MoveToward(_flash, 0, dt * 6);
        // A flash tints him (modulate above 1 brightens): red when struck, the colour of what lifted him otherwise.
        Modulate = _flash > 0.01f ? Colors.White.Lerp(new Color(0.6f + _flashColor.R, 0.6f + _flashColor.G, 0.6f + _flashColor.B), _flash) : Colors.White;
        QueueRedraw();
    }

    // ------------------------------------------------------------------------------------------------ actions

    void SwingTo(float target, float duration)
    {
        _swingFrom = _swing;
        _swingTo = target;
        _swingTime = 0;
        _swingDur = duration;
    }

    /// <summary>Raises the sword for a blow that lands in <paramref name="lead"/> seconds.</summary>
    public void Windup(float lead) => SwingTo(-1, Mathf.Max(0.06f, lead * 0.9f));

    /// <summary>The blow lands: the sword comes through fast.</summary>
    public void Strike()
    {
        if (_swing > -0.2f) _swing = -0.6f;
        SwingTo(1, 0.07f);
    }

    public void Hurt(Color flash)
    {
        _hurt = 1;
        _flash = 0.9f;
        _flashColor = flash;
    }

    public void Cheer() => _cheer = 1.2f;

    /// <summary>0 to stand, 1 to walk, 2 to run.</summary>
    public void SetWalk(float target) => _walkTarget = target;

    /// <summary>Hit-stop: holds the pose for a moment (only the knight and his target freeze, never the game).</summary>
    public void Freeze(double seconds) => _freeze = Math.Max(_freeze, seconds);

    public void Flash(Color c, float amount = 0.8f)
    {
        _flash = amount;
        _flashColor = c;
    }

    // ------------------------------------------------------------------------------------------------ the pose

    static Vector2 Dir(float a) => new(-Mathf.Sin(a), Mathf.Cos(a));

    void Pose()
    {
        float breath = Mathf.Sin(_t * 2.2f);
        float run = Mathf.Clamp(_walk, 0, 2);
        float stride = Mathf.Sin(_walkPhase);
        float bob = -Mathf.Abs(Mathf.Cos(_walkPhase)) * 5 * Mathf.Min(1, run) + breath * 1.5f * (1 - Mathf.Min(1, run));
        float crouch = _swing < 0 ? 6 * -_swing : 0;
        _pelvis = new Vector2(_lunge - _hurt * 10, -118 + bob + crouch);
        _torsoA = _lean - _hurt * 0.18f + (_cheer > 0 ? -0.05f : 0);

        Vector2 T(Vector2 local) => _pelvis + local.Rotated(_torsoA);
        _neck = T(new Vector2(3, -86));
        _shoulderN = T(new Vector2(8, -76));
        _shoulderF = T(new Vector2(-6, -74));
        _hipN = _pelvis + new Vector2(5, -2);
        _hipF = _pelvis + new Vector2(-7, -2);
        _headA = _torsoA * 0.6f + breath * 0.02f;

        // Legs: a stance when still, a stride when walking.
        float stance = 1 - Mathf.Min(1, run);
        _thighN = 0.18f * stance - 0.55f * stride * Mathf.Min(1, run) - _lean * 0.6f;
        _thighF = -0.22f * stance + 0.55f * stride * Mathf.Min(1, run) - _lean * 0.6f;
        _shinN = 0.12f + Mathf.Max(0, Mathf.Sin(_walkPhase + 1.2f)) * 0.7f * Mathf.Min(1, run) + crouch * 0.02f;
        _shinF = 0.12f + Mathf.Max(0, Mathf.Sin(_walkPhase + 1.2f + Mathf.Pi)) * 0.7f * Mathf.Min(1, run) + crouch * 0.02f;
        _kneeN = _hipN + Dir(_thighN) * 54;
        _kneeF = _hipF + Dir(_thighF) * 54;
        _ankleN = _kneeN + Dir(_thighN + _shinN) * 50;
        _ankleF = _kneeF + Dir(_thighF + _shinF) * 50;
        // Keep the planted foot on the ground.
        float lowest = Mathf.Max(_ankleN.Y, _ankleF.Y);
        float drop = -12 - lowest;
        _pelvis.Y += drop;
        _neck.Y += drop; _shoulderN.Y += drop; _shoulderF.Y += drop; _hipN.Y += drop; _hipF.Y += drop;
        _kneeN.Y += drop; _kneeF.Y += drop; _ankleN.Y += drop; _ankleF.Y += drop;

        // The sword arm: rest (sword up and forward), wound up (raised behind), struck (through and forward).
        float s = _swing;
        float restU = -0.35f, restF = -1.25f, upU = 2.55f, upF = -0.55f, hitU = -1.75f, hitF = -0.1f;
        if (_cheer > 0)
        {
            float c = Mathf.Clamp(_cheer, 0, 1);
            restU = Mathf.Lerp(restU, 2.9f, c);
            restF = Mathf.Lerp(restF, 0.2f, c);
        }
        if (s < 0)
        {
            _upperN = Mathf.Lerp(restU, upU, -s);
            _foreN = Mathf.Lerp(restF, upF, -s);
        }
        else
        {
            _upperN = Mathf.Lerp(restU, hitU, s);
            _foreN = Mathf.Lerp(restF, hitF, s);
        }
        if (run > 1.2f && s == 0)
        {
            _upperN = 1.1f;
            _foreN = -0.6f;
        }
        _upperN += _torsoA + Mathf.Sin(_t * 2.2f + 1) * 0.03f;
        _elbowN = _shoulderN + Dir(_upperN) * 44;
        _handN = _elbowN + Dir(_upperN + _foreN) * 40;

        // The shield arm: held forward, swinging a little with the stride.
        _upperF = 0.35f + _torsoA - 0.25f * stride * Mathf.Min(1, run) + (s < 0 ? 0.25f * -s : 0);
        _foreF = -1.1f;
        _elbowF = _shoulderF + Dir(_upperF) * 42;
        _handF = _elbowF + Dir(_upperF + _foreF) * 38;

        _capeA = 0.12f + run * 0.32f + Mathf.Sin(_t * 3.1f) * 0.04f + _hurt * 0.2f + (s > 0.5f ? 0.15f : 0);
    }

    // ------------------------------------------------------------------------------------------------ drawing

    public override void _Draw()
    {
        Pose();
        DrawCelestialBack();
        DrawCape();
        DrawShieldArm();
        DrawLeg(_hipF, _kneeF, _ankleF, _thighF, _shinF, far: true);
        DrawTorso();
        DrawHead();
        DrawLeg(_hipN, _kneeN, _ankleN, _thighN, _shinN, far: false);
        DrawSwordArm();
        DrawCelestialFront();
    }

    static Color Trim(int rarity) => rarity < 0 ? Look.Dim : Look.Tier(rarity);

    void DrawCape()
    {
        var root = _shoulderF + new Vector2(-6, -4);
        var r = Gear.Cloak >= 0 ? new Ink.Ramp(Look.Tier(Gear.Cloak).Darkened(0.42f)) : new Ink.Ramp("#3a2f3a", "#5a4752", "#7a6470");
        float a = _capeA;
        float flutter = Mathf.Sin(_t * 5.5f) * 6 * (0.3f + _walk);
        var pts = new[]
        {
            root + new Vector2(-4, 0), root + new Vector2(14, 2),
            root + new Vector2(10, 70).Rotated(a * 0.5f), root + new Vector2(2 + flutter * 0.5f, 140).Rotated(a),
            root + new Vector2(-30 + flutter, 150).Rotated(a), root + new Vector2(-34, 74).Rotated(a * 0.7f),
        };
        Ink.Shaded(this, pts, r);
        if (Gear.Cloak >= 0)
        {
            var hem = new[] { pts[3], pts[4], pts[4] + new Vector2(2, -9).Rotated(a), pts[3] + new Vector2(0, -9).Rotated(a) };
            DrawColoredPolygon(hem, Look.Tier(Gear.Cloak).Lightened(0.15f));
            Ink.Outline(this, hem, 2);
        }
    }

    void DrawLeg(Vector2 hip, Vector2 knee, Vector2 ankle, float thighA, float shinA, bool far)
    {
        var cloth = far ? new Ink.Ramp("#2c2533", "#463a52", "#5e4f6c") : new Ink.Ramp("#3a3045", "#5a4a6a", "#7a6890");
        Ink.Shaded(this, Ink.Capsule(hip, knee, 14, 12), cloth);
        Ink.Shaded(this, Ink.Capsule(knee, ankle, 12, 10), cloth);
        // Boot: a toe forward, a heel back.
        float a = thighA + shinA;
        var foot = new[]
        {
            ankle + new Vector2(-13, -14).Rotated(a * 0.2f), ankle + new Vector2(10, -14).Rotated(a * 0.2f),
            ankle + new Vector2(26, 2), ankle + new Vector2(26, 12), ankle + new Vector2(-14, 12),
        };
        var boot = Gear.Boots >= 0 ? Ink.Metal(Gear.BootsTier) : Ink.Leather;
        if (far) boot = new Ink.Ramp(boot.Shadow.Darkened(0.2f), boot.Mid.Darkened(0.25f), boot.Light.Darkened(0.25f));
        Ink.Shaded(this, foot, boot);
        if (Gear.Boots >= 0)
        {
            var cuff = new[] { ankle + new Vector2(-14, -16), ankle + new Vector2(12, -16), ankle + new Vector2(12, -9), ankle + new Vector2(-14, -9) };
            DrawColoredPolygon(cuff, Trim(Gear.Boots));
            Ink.Outline(this, cuff, 2);
        }
        // Knee plate shows any leg armour's metal.
        if (!far && Gear.Chest >= 0) Ink.ShadedCircle(this, knee + new Vector2(5, 0), 9, Ink.Metal(Gear.ChestTier), 2.5f);
    }

    void DrawTorso()
    {
        Vector2 T(float x, float y) => _pelvis + new Vector2(x, y).Rotated(_torsoA);
        var body = new[] { T(-22, 6), T(24, 6), T(28, -32), T(32, -66), T(20, -90), T(-14, -90), T(-26, -68), T(-28, -30) };
        Ink.Shaded(this, body, Gear.Chest >= 0 ? Ink.Metal(Gear.ChestTier) : Ink.Linen);
        if (Gear.Chest >= 0)
        {
            // Plate ridge and a rarity trim along the neckline and the waist.
            DrawPolyline(new[] { T(12, -84), T(22, -48), T(18, -14) }, Look.Ink, 2.5f, true);
            var neck = new[] { T(-12, -90), T(20, -90), T(16, -82), T(-10, -82) };
            DrawColoredPolygon(neck, Trim(Gear.Chest));
            Ink.Outline(this, neck, 2);
        }
        if (Gear.Tabard >= 0)
        {
            var cloth = new Ink.Ramp(Look.Tier(Gear.Tabard).Darkened(0.45f));
            var tab = new[] { T(2, -74), T(26, -72), T(30, 34), T(6, 40) };
            Ink.Shaded(this, tab, cloth, 3);
            DrawColoredPolygon(new[] { T(5, 28), T(29, 24), T(30, 34), T(6, 40) }, Look.Tier(Gear.Tabard).Lightened(0.1f));
        }
        // Belt.
        var belt = new[] { T(-27, -10), T(28, -10), T(28, 4), T(-27, 4) };
        Ink.Shaded(this, belt, Gear.Belt >= 0 ? Ink.Leather : new Ink.Ramp("#2e2229", "#4a363f", "#62484f"), 3);
        var buckle = new[] { T(14, -12), T(26, -12), T(26, 6), T(14, 6) };
        DrawColoredPolygon(buckle, Gear.Belt >= 0 ? Look.Tier(Gear.Belt) : Ink.Iron.Mid);
        Ink.Outline(this, buckle, 2.5f);
        // Neck jewellery: a gem at the throat.
        if (Gear.Neck >= 0)
        {
            var at = T(16, -84);
            Ink.Circle(this, at, 5.5f, Look.Tier(Gear.Neck), 2);
            Ink.Glint(this, at + new Vector2(-1, -1), 6 + 2 * Mathf.Sin(_t * 4), new Color(1, 1, 1, 0.8f));
        }
    }

    void DrawHead()
    {
        var c = _neck + new Vector2(6, -32).Rotated(_headA);
        Vector2 H(float x, float y) => c + new Vector2(x, y).Rotated(_headA);
        if (Gear.Helm < 0)
        {
            Ink.ShadedCircle(this, c, 28, Ink.Skin);
            // Hair and a short beard.
            var hair = new[] { H(-28, -4), H(-24, -24), H(-4, -32), H(18, -26), H(24, -14), H(6, -16), H(-6, -6), H(-14, 8) };
            Ink.Shaded(this, hair, Ink.Hair, 3);
            var beard = new[] { H(6, 10), H(24, 6), H(22, 22), H(8, 28), H(-2, 18) };
            Ink.Shaded(this, beard, Ink.Hair, 3);
            DrawCircle(H(16, -4), 3.2f, Look.Ink, true, -1, true);
            return;
        }
        var metal = Ink.Metal(Gear.HelmTier);
        // Plume behind the helm, in the helm's colour.
        var plumeC = Look.Tier(Gear.Helm);
        float wave = Mathf.Sin(_t * 4) * 4 + _walk * 6;
        var plume = new[] { H(-6, -30), H(4, -40), H(-18, -48 - wave * 0.3f), H(-44, -40 - wave), H(-56, -20 - wave), H(-30, -26) };
        Ink.Shaded(this, plume, new Ink.Ramp(plumeC.Darkened(0.2f)), 3);
        var dome = new[] { H(-28, 8), H(-30, -14), H(-18, -32), H(4, -38), H(24, -28), H(32, -8), H(30, 14), H(10, 24), H(-16, 22) };
        Ink.Shaded(this, dome, metal);
        // Visor slit and a rarity trim band.
        var slit = new[] { H(8, -6), H(32, -8), H(32, -1), H(8, 0) };
        DrawColoredPolygon(slit, Look.Ink);
        var band = new[] { H(-29, 2), H(30, 6), H(30, 12), H(-28, 9) };
        DrawColoredPolygon(band, Trim(Gear.Helm));
        Ink.Outline(this, band, 2);
        Ink.Glint(this, H(-12, -22), 6, new Color(1, 1, 1, 0.55f));
    }

    void DrawShieldArm()
    {
        var cloth = new Ink.Ramp("#2c2533", "#463a52", "#5e4f6c");
        Ink.Shaded(this, Ink.Capsule(_shoulderF, _elbowF, 11, 10), cloth);
        Ink.Shaded(this, Ink.Capsule(_elbowF, _handF, 10, 9), cloth);
        if (Gear.Shield < 0)
        {
            Ink.ShadedCircle(this, _handF, 9, Ink.Skin);
            return;
        }
        var centre = _handF + new Vector2(14, 4);
        var rim = Ink.Metal(Gear.ShieldTier);
        Ink.ShadedCircle(this, centre, 46, rim);
        var face = new Ink.Ramp(Look.Tier(Gear.Shield).Darkened(0.5f));
        Ink.ShadedCircle(this, centre, 36, face, 2.5f);
        // Emblem: a chevron in the rarity colour, and the boss.
        var chevron = new[] { centre + new Vector2(-20, -6), centre + new Vector2(0, -22), centre + new Vector2(20, -6), centre + new Vector2(20, 6), centre + new Vector2(0, -10), centre + new Vector2(-20, 6) };
        DrawColoredPolygon(chevron, Look.Tier(Gear.Shield).Lightened(0.1f));
        Ink.Outline(this, chevron, 2);
        Ink.ShadedCircle(this, centre + new Vector2(0, 12), 9, rim, 2.5f);
    }

    void DrawSwordArm()
    {
        var cloth = new Ink.Ramp("#3a3045", "#5a4a6a", "#7a6890");
        float foreA = _upperN + _foreN;
        // The sword, held in the fist: the blade points up out of it, square to the forearm.
        DrawSword(_handN, foreA + Mathf.Pi * 0.5f + 0.25f);
        Ink.Shaded(this, Ink.Capsule(_shoulderN, _elbowN, 12, 11), cloth);
        Ink.Shaded(this, Ink.Capsule(_elbowN, _handN, 11, 10), cloth);
        if (Gear.Tattoo >= 0)
        {
            var glow = Look.Tier(Gear.Tattoo);
            float pulse = 0.55f + 0.35f * Mathf.Sin(_t * 3);
            var mid = (_elbowN + _handN) / 2;
            DrawPolyline(new[] { _elbowN.Lerp(_handN, 0.2f), mid + Dir(foreA + 1.57f) * 4, _elbowN.Lerp(_handN, 0.8f) }, new Color(glow, pulse), 3.5f, true);
        }
        // Gauntlet or a bare fist.
        if (Gear.Gauntlet >= 0)
        {
            var m = Ink.Metal(Gear.GauntletTier);
            Ink.Shaded(this, Ink.Capsule(_elbowN.Lerp(_handN, 0.45f), _handN, 12, 12), m, 3);
            Ink.ShadedCircle(this, _handN, 12, m);
            DrawPolyline(new[] { _elbowN.Lerp(_handN, 0.45f) + Dir(foreA + 1.57f) * 12, _elbowN.Lerp(_handN, 0.45f) - Dir(foreA + 1.57f) * 12 }, Trim(Gear.Gauntlet), 4, true);
        }
        else Ink.ShadedCircle(this, _handN, 10, Ink.Skin);
        if (Gear.Rings >= 0)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(_t * 5);
            Ink.Glint(this, _handN + new Vector2(6, -8), 7 + 5 * p, new Color(Look.Tier(Gear.Rings), 0.9f));
            Ink.Glint(this, _handN + new Vector2(6, -8), 3 + 2 * p, Colors.White);
        }
        // Pauldron over the shoulder.
        if (Gear.Pauldron >= 0)
        {
            var m = Ink.Metal(Gear.PauldronTier);
            var a = _upperN;
            var pts = new[]
            {
                _shoulderN + new Vector2(-20, -10).Rotated(a * 0.3f), _shoulderN + new Vector2(6, -18).Rotated(a * 0.3f),
                _shoulderN + new Vector2(24, -4).Rotated(a * 0.3f), _shoulderN + new Vector2(20, 16).Rotated(a * 0.3f),
                _shoulderN + new Vector2(-18, 14).Rotated(a * 0.3f),
            };
            Ink.Shaded(this, pts, m);
            DrawPolyline(new[] { pts[4], pts[3] }, Trim(Gear.Pauldron), 4, true);
        }
    }

    void DrawSword(Vector2 fist, float a)
    {
        // Local: blade along -y from the fist.
        Vector2 S(float x, float y) => fist + new Vector2(x, y).Rotated(a);
        int tier = Gear.Weapon >= 0 ? Gear.WeaponTier : 0;
        var metal = Gear.Weapon >= 0 ? Ink.Metal(tier) : new Ink.Ramp("#4a4a52", "#6d6d78", "#8e8e9a");
        float len = Gear.Weapon >= 0 ? 196 + tier * 10 : 150;
        float w = Gear.Weapon >= 0 ? 8 + tier : 6;
        var blade = new[] { S(-w, -12), S(w, -12), S(w * 0.9f, -len + 18), S(0, -len), S(-w * 0.9f, -len + 18) };
        // A glow along the blade from Epic up (band B), stronger from Mythic (band C).
        if (Gear.Weapon >= 3)
        {
            var g = Look.Tier(Gear.Weapon);
            float pulse = 0.55f + 0.25f * Mathf.Sin(_t * 4);
            float k = Look.Band(Gear.Weapon) >= 2 ? 1.4f : 1f;
            DrawColoredPolygon(new[] { S(-w * 2.4f * k, -6), S(w * 2.4f * k, -6), S(w * 1.8f * k, -len + 8), S(0, -len - 16 * k), S(-w * 1.8f * k, -len + 8) }, new Color(g, 0.22f * pulse));
            DrawColoredPolygon(new[] { S(-w * 1.6f, -8), S(w * 1.6f, -8), S(w * 1.3f, -len + 12), S(0, -len - 6), S(-w * 1.3f, -len + 12) }, new Color(g, 0.32f * pulse));
        }
        DrawColoredPolygon(blade, metal.Mid);
        DrawColoredPolygon(new[] { S(-w * 0.15f, -14), S(w, -12), S(w * 0.9f, -len + 18), S(0, -len) }, metal.Light);
        DrawPolyline(new[] { S(0, -16), S(0, -len + 10) }, metal.Shadow, 2, true);
        Ink.Outline(this, blade);
        _bladeTip = S(0, -len);
        // Crossguard in the rarity colour, the grip and the pommel.
        var guard = new[] { S(-24, -6), S(24, -6), S(22, -16), S(-22, -16) };
        DrawColoredPolygon(guard, Gear.Weapon >= 0 ? Look.Tier(Gear.Weapon) : Ink.Iron.Mid);
        Ink.Outline(this, guard, 3);
        Ink.Poly(this, new[] { S(-5, -6), S(5, -6), S(5, 16), S(-5, 16) }, Ink.Leather.Mid, 3);
        Ink.Circle(this, S(0, 20), 6, Gear.Weapon >= 0 ? Look.Tier(Gear.Weapon) : Ink.Iron.Light, 2.5f);
    }

    // ------------------------------------------------------------------------------------------------ celestial and trinkets

    void DrawCelestialBack()
    {
        var head = _neck + new Vector2(6, -32);
        if (Gear.Aura >= 0)
        {
            var c = Look.Tier(Gear.Aura);
            for (int i = 4; i >= 1; i--) DrawCircle(_pelvis + new Vector2(0, -40), 110 * i / 4f + 4 * Mathf.Sin(_t * 2 + i), new Color(c, 0.05f), true, -1, true);
        }
        if (Gear.Eclipse >= 0)
        {
            var c = Look.Tier(Gear.Eclipse);
            DrawCircle(head + new Vector2(-10, -6), 58, new Color(c, 0.25f), true, -1, true);
            DrawCircle(head + new Vector2(-10, -6), 50, Look.Ink, true, -1, true);
        }
        if (Gear.Wings >= 0)
        {
            var c = new Ink.Ramp(Look.Tier(Gear.Wings).Lerp(Colors.White, 0.35f));
            float flap = Mathf.Sin(_t * 2.6f) * 0.12f;
            var root = _shoulderF + new Vector2(-8, 6);
            var wing = new[]
            {
                root, root + new Vector2(-60, -70).Rotated(flap), root + new Vector2(-130, -96).Rotated(flap),
                root + new Vector2(-150, -60).Rotated(flap), root + new Vector2(-120, -30).Rotated(flap),
                root + new Vector2(-136, 0).Rotated(flap), root + new Vector2(-96, 6).Rotated(flap), root + new Vector2(-104, 34).Rotated(flap),
                root + new Vector2(-50, 24),
            };
            Ink.Shaded(this, wing, c);
        }
        if (Gear.Constellation >= 0)
        {
            var c = Look.Tier(Gear.Constellation);
            var stars = new[] { new Vector2(-90, -250), new Vector2(-40, -290), new Vector2(30, -300), new Vector2(80, -262), new Vector2(110, -300) };
            for (int i = 0; i < stars.Length - 1; i++) DrawLine(stars[i], stars[i + 1], new Color(c, 0.35f), 1.5f, true);
            for (int i = 0; i < stars.Length; i++) Ink.Glint(this, stars[i], 7 + 2 * Mathf.Sin(_t * 3 + i), new Color(c.Lightened(0.4f), 0.9f));
        }
        if (Gear.Nebula >= 0)
        {
            var c = Look.Tier(Gear.Nebula);
            for (int i = 0; i < 5; i++)
            {
                var at = _pelvis + new Vector2(Mathf.Cos(_t * 0.4f + i * 1.3f) * 70, -120 + Mathf.Sin(_t * 0.3f + i) * 40);
                DrawCircle(at, 30 + 6 * i, new Color(c, 0.05f), true, -1, true);
            }
        }
        if (Gear.Familiar >= 0) DrawFamiliar();
    }

    void DrawCelestialFront()
    {
        var head = _neck + new Vector2(6, -32).Rotated(_headA);
        if (Gear.Halo >= 0)
        {
            var c = Look.Tier(Gear.Halo);
            var at = head + new Vector2(-4, -50 + 3 * Mathf.Sin(_t * 2));
            DrawArc(at, 26, 0, Mathf.Tau, 32, new Color(c, 0.35f), 12, true);
            DrawArc(at, 26, 0, Mathf.Tau, 32, c.Lightened(0.4f), 4, true);
        }
        if (Gear.Starcrown >= 0)
        {
            var c = Look.Tier(Gear.Starcrown);
            for (int i = 0; i < 5; i++)
            {
                float a = -Mathf.Pi / 2 + (i - 2) * 0.38f;
                Ink.Glint(this, head + Vector2.FromAngle(a) * 46, 6 + 2 * Mathf.Sin(_t * 4 + i), c.Lightened(0.3f));
            }
        }
        if (Gear.Comet >= 0)
        {
            var c = Look.Tier(Gear.Comet);
            float a = _t * 1.6f;
            var at = _pelvis + new Vector2(Mathf.Cos(a) * 90, -70 + Mathf.Sin(a) * 26);
            for (int i = 1; i <= 6; i++)
            {
                float b = a - i * 0.09f;
                DrawCircle(_pelvis + new Vector2(Mathf.Cos(b) * 90, -70 + Mathf.Sin(b) * 26), 6 - i * 0.8f, new Color(c, 0.5f - i * 0.07f), true, -1, true);
            }
            Ink.Glint(this, at, 9, c.Lightened(0.5f));
        }
        if (Gear.Zenith >= 0)
        {
            var c = Look.Tier(Gear.Zenith);
            DrawColoredPolygon(new[] { head + new Vector2(-14, -40), head + new Vector2(14, -40), head + new Vector2(30, -420), head + new Vector2(-30, -420) },
                new Color(c, 0.08f + 0.04f * Mathf.Sin(_t * 2)));
        }
        if (Gear.TrinketCount > 0 && Gear.Trinket >= 0)
        {
            var c = Look.Tier(Gear.Trinket);
            int n = Math.Clamp(Gear.TrinketCount / 4 + 1, 1, 6);
            for (int i = 0; i < n; i++)
            {
                float a = _t * 1.1f + i * Mathf.Tau / n;
                var at = _pelvis + new Vector2(Mathf.Cos(a) * 64, -40 + Mathf.Sin(a) * 14);
                if (Mathf.Sin(a) < 0) continue;
                DrawCircle(at, 4, new Color(c, 0.85f), true, -1, true);
                DrawCircle(at, 9, new Color(c, 0.18f), true, -1, true);
            }
        }
    }

    void DrawFamiliar()
    {
        var c = new Ink.Ramp(Look.Tier(Gear.Familiar).Darkened(0.55f));
        var at = new Vector2(-70 + 8 * Mathf.Sin(_t * 0.9f), -230 + 10 * Mathf.Sin(_t * 1.7f));
        float flap = Mathf.Sin(_t * 9) * 10;
        var body = new[] { at + new Vector2(-16, 0), at + new Vector2(4, -10), at + new Vector2(22, -6), at + new Vector2(30, 0), at + new Vector2(20, 6), at + new Vector2(-6, 10) };
        var wing = new[] { at + new Vector2(-4, -4), at + new Vector2(-18, -24 - flap), at + new Vector2(4, -10) };
        Ink.Shaded(this, wing, c, 2.5f);
        Ink.Shaded(this, body, c, 2.5f);
        DrawCircle(at + new Vector2(20, -3), 2.2f, Look.Tier(Gear.Familiar).Lightened(0.4f), true, -1, true);
    }

}
