using Godot;
using LootFall.Game;

namespace LootFall.View;

/// <summary>
/// A monster on the lane: one still illustration moved by code according to its body plan (docs/art-direction.md,
/// "Monsters"): walkers bob, hoppers squash and hop, flyers hover, heavies stomp, slitherers ripple. It flinches and
/// flashes when struck, lunges when it strikes, and dissolves when it falls. A boss is half as big again, with an aura.
/// </summary>
public partial class MonsterActor : Node2D
{
    const string ShaderCode = @"
shader_type canvas_item;
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0);
uniform vec4 tint : source_color = vec4(1.0);
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
// In fragment(), COLOR already has the texture in it; keep the vertex colour (modulate) apart instead.
varying vec4 vertex_color;
void vertex() { vertex_color = COLOR; }
float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    c.rgb *= tint.rgb;
    c.rgb = mix(c.rgb, flash_color.rgb, flash);
    if (dissolve > 0.0) {
        float n = hash(floor(UV * 48.0));
        if (n < dissolve) discard;
        if (n < dissolve + 0.1) c.rgb = mix(c.rgb, vec3(1.0, 0.86, 0.55), 0.85);
    }
    COLOR = c * vertex_color;
}";

    static Shader _shader;

    public static void Release() => _shader = null;

    public readonly Foe Foe;
    readonly Sprite2D _sprite;
    readonly ShaderMaterial _mat;
    readonly Painter _shadow;
    readonly Bar _hp;
    Sprite2D _aura;
    float _t, _flash, _knock, _lunge, _squash, _dead = -1, _baseScale;
    double _freeze;
    public float TargetX;
    public bool Leaving;

    public float Height { get; }
    public float HalfWidth { get; }
    public Body Body => Foe.Def.Body;

    public MonsterActor(Foe foe)
    {
        Foe = foe;
        var art = Assets.Monster(foe.Def.Key);
        float size = (float)foe.Def.Size * (foe.Role == FoeRole.Boss ? 1.5f : 1f);
        Height = Look.HeroHeight * size;
        _baseScale = art.Texture != null ? Height / art.Height : 1;
        HalfWidth = (art.Texture?.GetWidth() ?? 100) * _baseScale / 2;
        _shader ??= new Shader { Code = ShaderCode };
        _mat = new ShaderMaterial { Shader = _shader };
        _shadow = new Painter(p => p.DrawColoredPolygon(Ink.Ellipse(Vector2.Zero, HalfWidth * 0.8f, 12), new Color(0, 0, 0, 0.35f))) { ZIndex = -1 };
        AddChild(_shadow);
        if (foe.IsBoss)
        {
            _aura = Fx.Glow(this, new Vector2(0, -Height * 0.5f), foe.Role == FoeRole.Guardian ? new Color(1f, 0.55f, 0.2f, 0.55f) : new Color(1f, 0.25f, 0.2f, 0.45f), Height / 110f, -1);
        }
        _sprite = new Sprite2D
        {
            Texture = art.Texture, Centered = false, Material = _mat, Scale = Vector2.One * _baseScale,
            Offset = art.Texture != null ? -art.Foot : Vector2.Zero,
        };
        AddChild(_sprite);
        if (foe.Role == FoeRole.Boss) _mat.SetShaderParameter("tint", new Color(1f, 0.88f, 0.85f));
        if (!foe.IsBoss)
        {
            _hp = new Bar(Look.Threat) { Visible = false, Radius = 3 }.Place(-46, -Height - 26, 92, 9);
            AddChild(_hp);
        }
        _t = GD.Randf() * 10;
    }

    public override void _Process(double delta)
    {
        if (_freeze > 0)
        {
            _freeze -= delta;
            return;
        }
        float dt = (float)delta;
        _t += dt;
        _flash = Mathf.MoveToward(_flash, 0, dt * 5);
        _knock = Mathf.MoveToward(_knock, 0, dt * 90);
        _lunge = Mathf.MoveToward(_lunge, 0, dt * 160);
        _squash = Mathf.MoveToward(_squash, 0, dt * 4);
        _mat.SetShaderParameter("flash", _flash);
        if (_hp != null && Foe.Hp < 0.999 && !Foe.Dead)
        {
            _hp.Visible = true;
            _hp.Set((float)Foe.Hp);
        }
        if (_dead >= 0)
        {
            _dead += dt;
            _mat.SetShaderParameter("dissolve", Mathf.Clamp(_dead / 0.45f, 0, 1));
            if (_hp != null) _hp.Visible = false;
            if (_aura != null) _aura.Modulate = _aura.Modulate with { A = Mathf.Max(0, 0.5f - _dead) };
            if (_dead > 0.5f) QueueFree();
        }
        Animate();
    }

    /// <summary>The body plan's motion: an offset and a squash for the sprite, every frame.</summary>
    void Animate()
    {
        bool moving = Foe.Approach < 1 && !Foe.Dead;
        float y = 0, sx = 1, sy = 1, rot = 0;
        switch (Body)
        {
            case Body.Hopper:
            {
                float hop = moving ? Mathf.Abs(Mathf.Sin(_t * 7)) : Mathf.Abs(Mathf.Sin(_t * 2.5f)) * 0.25f;
                y = -hop * (moving ? 28 : 10);
                float land = 1 - hop;
                sy = 1 - land * 0.12f + hop * 0.06f;
                sx = 1 + land * 0.1f;
                break;
            }
            case Body.Flyer:
                y = -Height * 0.35f - 70 + Mathf.Sin(_t * 2.4f) * 12;
                rot = Mathf.Sin(_t * 1.7f) * 0.05f;
                break;
            case Body.Heavy:
            {
                float step = Mathf.Sin(_t * (moving ? 4 : 1.4f));
                y = -Mathf.Abs(step) * (moving ? 7 : 2);
                sy = 1 + step * 0.015f;
                rot = step * (moving ? 0.03f : 0.01f);
                break;
            }
            case Body.Slither:
            {
                float wave = Mathf.Sin(_t * (moving ? 8 : 2.5f));
                sx = 1 + wave * 0.05f;
                sy = 1 - wave * 0.04f;
                break;
            }
            default:
            {
                float step = Mathf.Sin(_t * (moving ? 9 : 2.2f));
                y = -Mathf.Abs(step) * (moving ? 9 : 2.5f);
                rot = step * (moving ? 0.035f : 0.012f);
                break;
            }
        }
        sy *= 1 - _squash * 0.12f;
        sx *= 1 + _squash * 0.1f;
        _sprite.Position = new Vector2(_knock - _lunge, y);
        _sprite.Scale = new Vector2(sx, sy) * _baseScale;
        _sprite.Rotation = rot;
        _shadow.Scale = Vector2.One * (Body == Body.Flyer ? 0.55f : 1f - Mathf.Clamp(-y / 200f, 0, 0.4f));
    }

    /// <summary>Struck: a flash, a shove back, a squash.</summary>
    public void Struck(bool crit)
    {
        _flash = crit ? 1f : 0.75f;
        _knock = crit ? 26 : 14;
        _squash = crit ? 1f : 0.6f;
        _mat.SetShaderParameter("flash_color", Colors.White);
    }

    /// <summary>Strikes at the knight: a quick lunge.</summary>
    public void Lunge() => _lunge = 34;

    public void Freeze(double seconds) => _freeze = System.Math.Max(_freeze, seconds);

    public void Fall()
    {
        if (_dead >= 0) return;
        _dead = 0;
        _flash = 1;
        _mat.SetShaderParameter("flash_color", new Color(1f, 0.95f, 0.85f));
    }

    public bool Falling => _dead >= 0;

    /// <summary>Where blows land on it (its chest, in the parent's space).</summary>
    public Vector2 HitPoint => Position + new Vector2(-HalfWidth * 0.35f, Body == Body.Flyer ? -Height * 0.85f - 70 : -Height * 0.5f);

    public Vector2 Top => Position + new Vector2(0, (Body == Body.Flyer ? -70 - Height * 0.35f : 0) - Height);
}
