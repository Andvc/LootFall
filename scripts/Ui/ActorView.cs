using System;
using Godot;
using LootFall.Core;

namespace LootFall.Ui;

/// <summary>
/// The knight or a monster on the battlefield: its animated sprite standing on the node's origin (feet), facing the
/// other side, a health bar over its head, and the reactions the battle calls for (swing, flinch, fall).
/// </summary>
public partial class ActorView : Node2D
{
    public Fighter Fighter { get; private set; }
    public string Key { get; private set; }
    public int Px { get; private set; }

    readonly AnimatedSprite2D _sprite = new() { Centered = false };
    ShaderMaterial _material;
    ActorData _data;
    Bar _hp;
    Label _label;
    bool _still, _dying, _busy;
    float _flash, _bob, _hitKick;
    Vector2 _lunge;
    Tween _move;

    /// <summary>Where it's going to stand (it may still be walking there).</summary>
    public Vector2 Home { get; private set; }

    /// <summary>Where it stands right now, before lunges and knockback.</summary>
    Vector2 _base;

    public float Height => (_data?.bodyH ?? 60) * Px;
    public float Width => (_data?.bodyW ?? 60) * Px;

    /// <summary>The middle of its body, in the battlefield's coordinates.</summary>
    public Vector2 Middle => Position + new Vector2(0, -Height * 0.5f);

    /// <summary>Just over its head.</summary>
    public Vector2 Top => Position + new Vector2(0, -Height - 10);

    public static ActorView Make(Fighter fighter, string key, int scale, bool faceRight)
    {
        var view = new ActorView { Fighter = fighter, Key = key, Px = scale };
        view.Build(faceRight);
        return view;
    }

    void Build(bool faceRight)
    {
        _data = Anims.Actor(Key);
        _still = !_data.anims.ContainsKey("attack");
        _material = Shaders.Actor();
        _sprite.SpriteFrames = Anims.ForActor(Key);
        _sprite.Material = _material;
        bool flip = faceRight == _data.facesLeft;
        _sprite.FlipH = flip;
        _sprite.Offset = new Vector2(-(flip ? _data.w - _data.footX : _data.footX), -_data.footY);
        // Offset is in texture pixels, so it scales with the sprite.
        _sprite.Scale = Vector2.One * Px;
        AddChild(_sprite);
        _sprite.Play("idle");
        _sprite.AnimationFinished += () =>
        {
            if (_dying) return;
            _busy = false;
            _sprite.Play("idle");
        };

        if (_data.weaponColors != null)
        {
            _material.SetShaderParameter("swap", true);
            _material.SetShaderParameter("weapon_src", new Color(_data.weaponColors[0]));
            _material.SetShaderParameter("armor_src", new Color(_data.armorColors[0]));
            _material.SetShaderParameter("armor_dark_src", new Color(_data.armorColors[1]));
        }

        float barW = Mathf.Clamp(Width * 0.7f, 64, 220);
        _hp = UiKit.MakeBar(barW, Fighter?.Boss == true ? 8 : 6, Fighter?.IsHero == true ? Palette.Good : Palette.Bad, new Color("#2a1a22"));
        _hp.Position = new Vector2(-barW / 2, -Height - 16);
        _hp.ZIndex = 30;
        if (Fighter?.Boss == true) _hp.Visible = false; // bosses get the big bar at the top
        AddChild(_hp);
        if (Fighter == null) _hp.Visible = false;
    }

    /// <summary>Shows a name under the health bar (the wandering mimic).</summary>
    public void ShowName(string text, Color color)
    {
        _label = UiKit.MakeLabel(text, 16, color, HorizontalAlignment.Center, outline: 4).Place(-120, -Height - 44, 240, 20);
        _label.ZIndex = 30;
        AddChild(_label);
    }

    /// <summary>The knight's colours: his blade from the weapon's rarity, his cape from the armour's.</summary>
    public void SetGear(Rarity? weapon, Rarity? armor)
    {
        _material.SetShaderParameter("weapon_dst", Palette.Blade(weapon));
        var cape = Palette.Cape(armor);
        _material.SetShaderParameter("armor_dst", cape);
        _material.SetShaderParameter("armor_dark_dst", cape.Darkened(0.4f));
        _mythicBlade = weapon == Rarity.Mythic;
    }

    bool _mythicBlade;
    float _time;

    /// <summary>
    /// A swing or bite, timed so the blow lands <paramref name="windup"/> seconds from now: the animation's striking
    /// frame (about 55% through for the knight's slash, 40% for monsters) is stretched or squeezed to meet it.
    /// </summary>
    public void Attack(float windup, bool lunge)
    {
        if (_dying) return;
        _busy = true;
        if (_still)
        {
            Lunge(windup, lunge ? 46 : 20);
            return;
        }
        float length = Anims.Length(Key, "attack");
        float strike = Fighter?.IsHero == true ? 0.55f : 0.4f;
        _sprite.SpeedScale = Mathf.Clamp(length * strike / Mathf.Max(0.05f, windup), 0.6f, 6f);
        _sprite.Play("attack");
        if (lunge) Lunge(windup, Fighter?.IsHero == true ? 26 : 34);
    }

    void Lunge(float windup, float distance)
    {
        float dir = Fighter?.IsHero == true ? 1 : -1;
        _move?.Kill();
        _move = CreateTween();
        _move.TweenMethod(Callable.From<float>(v => _lunge = new Vector2(dir * distance * v, 0)), 0f, 1f, windup)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        _move.TweenMethod(Callable.From<float>(v => _lunge = new Vector2(dir * distance * v, 0)), 1f, 0f, 0.25f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Took a blow: a white flash, a kick back, and the flinch animation if it isn't mid-swing.</summary>
    public void Hurt(float strength = 1)
    {
        if (_dying) return;
        _flash = 1;
        _hitKick = 8 * strength;
        if (!_busy && !_still && _data.anims.ContainsKey("hit"))
        {
            _sprite.SpeedScale = 1.6f;
            _sprite.Play("hit");
            _busy = true;
        }
    }

    /// <summary>Raises the shield (the knight blocking or dodging).</summary>
    public void Guard()
    {
        if (_dying || _busy || !_data.anims.ContainsKey("guard")) return;
        _busy = true;
        _sprite.SpeedScale = 2.5f;
        _sprite.Play("guard");
    }

    /// <summary>Falls: a last flash, then it sinks and fades (monsters) or kneels grey (the knight). Frees monsters.</summary>
    public void Die(bool free = true)
    {
        if (_dying) return;
        _dying = true;
        _flash = 1;
        _hp.Visible = false;
        if (_label != null) _label.Visible = false;
        var t = CreateTween();
        if (free)
        {
            t.TweenProperty(this, "modulate:a", 0f, 0.35f).SetDelay(0.08f);
            t.Parallel().TweenProperty(_sprite, "scale", new Vector2(Px * 1.15f, Px * 0.7f), 0.35f).SetDelay(0.08f);
            t.TweenCallback(Callable.From(QueueFree));
        }
        else
        {
            _sprite.Pause();
            _material.SetShaderParameter("tint", new Color(0.45f, 0.45f, 0.5f));
            t.TweenProperty(_sprite, "rotation", -0.25f, 0.3f);
        }
    }

    /// <summary>The knight gets up again.</summary>
    public void Revive()
    {
        _dying = false;
        _busy = false;
        _sprite.Rotation = 0;
        _material.SetShaderParameter("tint", Colors.White);
        _hp.Visible = true;
        _sprite.Play("idle");
        _flash = 1;
    }

    /// <summary>Stands it at a spot at once.</summary>
    public void PlaceAt(Vector2 spot)
    {
        Home = _base = spot;
        Position = spot;
    }

    /// <summary>Walks (slides, with a little hop) from where it is to its spot over <paramref name="seconds"/>.</summary>
    public void WalkTo(Vector2 spot, float seconds)
    {
        Home = spot;
        var from = _base;
        _walk?.Kill();
        _walk = CreateTween();
        _walk.TweenMethod(Callable.From<float>(t =>
        {
            _base = from.Lerp(spot, t);
            // Small hops while walking, so the ones without a walk cycle still look like they're coming.
            if (!_dying && t < 1) _base.Y -= Mathf.Abs(Mathf.Sin(t * Mathf.Pi * 4)) * 8;
        }), 0f, 1f, seconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    Tween _walk;

    /// <summary>A tint over everything (a boss fleeing goes dark, a glow of power).</summary>
    public void Flash(Color color, float amount = 1)
    {
        _material.SetShaderParameter("flash_color", color);
        _flash = amount;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (_flash > 0)
        {
            _flash = Mathf.Max(0, _flash - dt * 7);
            _material.SetShaderParameter("flash", _flash);
            if (_flash <= 0) _material.SetShaderParameter("flash_color", Colors.White);
        }
        if (_mythicBlade)
        {
            var c = Color.FromHsv(_time * 0.6f % 1f, 0.55f, 1f);
            _material.SetShaderParameter("weapon_dst", c);
        }
        _hitKick = Mathf.MoveToward(_hitKick, 0, dt * 60);
        float kick = (Fighter?.IsHero == true ? -1 : 1) * _hitKick;
        if (_still && !_dying)
        {
            _bob += dt * 3.2f;
            _sprite.Scale = new Vector2(Px * (1 + 0.03f * Mathf.Sin(_bob)), Px * (1 - 0.03f * Mathf.Sin(_bob)));
        }
        if (!_dying) Position = _base + _lunge + new Vector2(kick, 0);
        if (Fighter != null && _hp.Visible)
        {
            _hp.Set((float)Fighter.HpShare);
            if (Fighter.IsHero) _hp.SetExtra((float)Math.Min(1, Fighter.Barrier / Math.Max(1, Fighter.MaxHp)));
        }
    }
}
