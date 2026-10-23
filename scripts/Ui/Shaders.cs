using Godot;

namespace LootFall.Ui;

/// <summary>
/// The one shader actors use: a white flash when hit, a tint (a fallen knight goes grey), and for the knight a palette
/// swap, so his blade and slash take his weapon's rarity colour and his cape and plume his armour's.
/// </summary>
public static class Shaders
{
    const string ActorCode = @"
shader_type canvas_item;
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0);
uniform vec4 tint : source_color = vec4(1.0);
uniform bool swap = false;
uniform vec3 weapon_src : source_color = vec3(0.545, 1.0, 0.78);
uniform vec3 weapon_dst : source_color = vec3(0.545, 1.0, 0.78);
uniform vec3 armor_src : source_color = vec3(0.831, 0.427, 0.31);
uniform vec3 armor_dst : source_color = vec3(0.831, 0.427, 0.31);
uniform vec3 armor_dark_src : source_color = vec3(0.475, 0.251, 0.267);
uniform vec3 armor_dark_dst : source_color = vec3(0.475, 0.251, 0.267);

// In fragment(), COLOR already has the texture in it; keep the vertex colour (modulate) apart instead.
varying vec4 vertex_color;

void vertex() {
    vertex_color = COLOR;
}

bool near(vec3 a, vec3 b) { return distance(a, b) < 0.03; }

void fragment() {
    vec4 c = texture(TEXTURE, UV);
    if (swap) {
        if (near(c.rgb, weapon_src)) c.rgb = weapon_dst;
        else if (near(c.rgb, armor_src)) c.rgb = armor_dst;
        else if (near(c.rgb, armor_dark_src)) c.rgb = armor_dark_dst;
    }
    c *= tint;
    c.rgb = mix(c.rgb, flash_color.rgb, flash);
    COLOR = c * vertex_color;
}";

    static Shader _actor;

    public static ShaderMaterial Actor()
    {
        _actor ??= new Shader { Code = ActorCode };
        return new ShaderMaterial { Shader = _actor };
    }
}
