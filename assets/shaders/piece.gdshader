// A worn piece's own picture on the equipment screen's figure (docs/equipment.md, "The figure"): drawn over the
// panel's picture in layer order. Its plain steel takes the era's metal; the rest of it keeps its colours. Where it
// lies on the body it dims and lights with the site under it (the panel's table, as the figure shader does), elsewhere
// with the piece; one shown where it would go is faint. Seen from behind, his braid hangs over what he wears: there the
// panel's own picture of it shows, sharp even where the piece's picture is borrowed from another view.
shader_type canvas_item;

uniform sampler2D sites : filter_nearest, repeat_disable;
uniform sampler2D lut : filter_nearest, repeat_disable;
uniform vec2 sites_size = vec2(1.0);
// The panel's own picture, and its hair site where his hair lies over this piece (0 where it doesn't).
uniform sampler2D body : filter_linear_mipmap, repeat_disable;
uniform int hair = 0;
// The whole picture's rectangle in the panel's view pixels (x, y, w, h): where each of its pixels lies on the body.
uniform vec4 view_rect = vec4(0.0, 0.0, 1.0, 1.0);
// The era's metal (shadow, mid, light, highlight), and how much plain steel takes it (0 for cloth, ink and the rest).
uniform vec3 era_shadow = vec3(0.23, 0.25, 0.29);
uniform vec3 era_mid = vec3(0.39, 0.42, 0.47);
uniform vec3 era_light = vec3(0.58, 0.62, 0.67);
uniform vec3 era_hi = vec3(0.83, 0.85, 0.89);
uniform float metal = 0.0;
// Off the body (a cloak's drape, a sword beside him): the piece's own dimming and light.
uniform float dim = 0.0;
uniform vec4 glow = vec4(0.0);
uniform float fade = 1.0;

vec3 era(float l) {
	if (l < 0.29) {
		return mix(era_shadow * 0.45, era_shadow, l / 0.29);
	}
	if (l < 0.54) {
		return mix(era_shadow, era_mid, (l - 0.29) / 0.25);
	}
	if (l < 0.81) {
		return mix(era_mid, era_light, (l - 0.54) / 0.27);
	}
	return mix(era_light, era_hi, clamp((l - 0.81) / 0.19, 0.0, 1.0));
}

void fragment() {
	vec4 c = texture(TEXTURE, UV);
	vec3 col = c.rgb;
	// Plain steel: grey, and not the ink.
	float chroma = max(col.r, max(col.g, col.b)) - min(col.r, min(col.g, col.b));
	float l = dot(col, vec3(0.299, 0.587, 0.114));
	float steel = metal * (1.0 - smoothstep(0.045, 0.075, chroma)) * smoothstep(0.1, 0.18, l);
	col = mix(col, era(l), steel);
	// The site under this pixel: it dims and lights with it, as the picture under it does (what's lit there, a region
	// pointed at, a flash); off the body, with the piece.
	vec2 at = view_rect.xy + UV * view_rect.zw;
	ivec2 px = ivec2(floor(at));
	float d = dim;
	vec4 light = glow;
	if (px.x >= 0 && px.y >= 0 && px.x < int(sites_size.x) && px.y < int(sites_size.y)) {
		int id = int(texelFetch(sites, px, 0).r * 255.0 + 0.5);
		if (id > 0) {
			d = texelFetch(lut, ivec2(id, 2), 0).a;
			light = texelFetch(lut, ivec2(id, 1), 0);
		}
		if (hair > 0 && id == hair) {
			// His hair, not the skin round it (as the import tells them apart).
			vec4 b = texture(body, at / sites_size);
			if (b.a > 0.5 && b.r < 0.53) {
				col = b.rgb;
			}
		}
	}
	col = mix(col, col * 0.3 + vec3(0.02, 0.015, 0.04), d);
	if (light.a > 0.0) {
		col = mix(col, light.rgb, light.a * 0.3) + light.rgb * light.a * 0.1;
	}
	COLOR = vec4(col, c.a * fade);
}
