// The equipment screen's figure (docs/equipment.md, "The figure"): the knight's art with what's worn painted over the
// body sites it covers, the region shown in full and the rest dimmed, and what the player points at lit. The site map
// gives every pixel its body site; the table (lut, a column per site) gives each site:
//   row 0  the fill of the piece worn over it: its material's middle tone (the era's metal, the cloth's colour), and
//          the material in alpha (0 none, 1 ink, 2 cloth, 3 mail, 4 plate, 5 over: heavy cloth)
//   row 1  its highlight: colour, and its strength in alpha
//   row 2  r: the fill's shine (its rarity), a: how much to dim it (outside the region shown, or while one piece is
//          chosen)
//   row 3  the piece's rarity colour (the ink of a tattoo)
//   row 4  b: 1 where the art is busy (hair, a beard): its strands don't show through what's worn over it, which is
//          shaded as a smooth dome lit from the top left instead of by the art's light
//   row 5  the site's box (x0, y0, x1, y1, as shares of the picture), for that dome
// Sites meet smoothly at any zoom: each pixel takes the two sites that cover most of it (of the four map pixels round
// it) and blends them over one screen pixel. The ink lines round what's worn are drawn over this, in vector
// (PaperDoll's outlines).
shader_type canvas_item;

uniform sampler2D sites : filter_nearest, repeat_disable;
uniform sampler2D lut : filter_nearest, repeat_disable;
uniform vec2 sites_size = vec2(1.0);
uniform float time = 0.0;
uniform float fade = 1.0;

int site_px(ivec2 p) {
	p = clamp(p, ivec2(0), ivec2(sites_size) - ivec2(1));
	return int(texelFetch(sites, p, 0).r * 255.0 + 0.5);
}

vec4 row(int site, int r) {
	return texelFetch(lut, ivec2(site, r), 0);
}

float luma(vec3 c) {
	return dot(c, vec3(0.299, 0.587, 0.114));
}

// Light in three flat steps, as the style draws every material (shadow, mid, light).
float steps3(float l) {
	float w = max(fwidth(l), 0.002) * 1.5;
	return 0.5 * smoothstep(0.3 - w, 0.3 + w, l) + 0.5 * smoothstep(0.78 - w, 0.78 + w, l);
}

vec3 ramp(vec3 shadow, vec3 mid, vec3 light, float t) {
	return t < 0.5 ? mix(shadow, mid, t * 2.0) : mix(mid, light, (t - 0.5) * 2.0);
}

// One site's colour at this pixel: the art, with what's worn over it, dimmed or lit.
vec3 shade(int id, vec3 art, float lum, float soft, vec2 texel) {
	if (id == 0) {
		return art;
	}
	vec4 fill = row(id, 0);
	vec4 hl = row(id, 1);
	vec4 extra = row(id, 2);
	vec3 col = art;
	int mat = int(fill.a * 255.0 + 0.5);
	if (mat > 0) {
		vec4 range = row(id, 4);
		// The art's light in three steps (its skin's shadow, middle and light tones): what's worn keeps the knight's
		// shading and folds.
		float light = clamp((soft - 0.4) / 0.4, 0.0, 1.0);
		if (range.b > 0.5) {
			// Over hair: a smooth dome, lit from the top left, with a little of the art's light for its shape.
			vec4 box = row(id, 5);
			vec2 c = (box.xy + box.zw) * 0.5 * sites_size;
			vec2 hs = max((box.zw - box.xy) * 0.5 * sites_size, vec2(1.0));
			vec2 d = (texel - c) / hs;
			float dome = 0.6 - 0.3 * (d.x * 0.55 + d.y * 0.85) - 0.2 * dot(d, d);
			light = clamp(mix(dome, light, 0.2), 0.0, 1.0);
		}
		float t = steps3(light);
		vec3 base = fill.rgb;
		vec3 m = col;
		if (mat == 1) {
			// Ink on the skin: bands of a tattoo in dark ink, touched with the piece's colour.
			vec2 g = texel / 7.0;
			float v = sin(g.x * 1.3 + sin(g.y * 0.9) * 2.2) * cos(g.y * 1.1 + sin(g.x * 0.7) * 1.8);
			float line = 1.0 - smoothstep(0.10, 0.10 + fwidth(v) * 1.5 + 0.02, abs(v));
			line *= clamp(1.4 - fwidth(g.x) * 2.5, 0.0, 1.0);
			m = mix(col, mix(base, row(id, 3).rgb, 0.35), line * 0.82);
		} else if (mat == 2 || mat == 5) {
			// Cloth: its colour in three steps of the art's light, with a faint weave.
			m = ramp(base * 0.55, base, mix(base, vec3(1.0), 0.16), t);
			vec2 g = texel / 1.6;
			float weave = (sin(g.x * 3.14159) * sin(g.y * 3.14159)) * clamp(1.2 - fwidth(g.x) * 2.0, 0.0, 1.0);
			m *= 1.0 + weave * 0.04;
		} else if (mat == 3) {
			// Mail: rows of small rings in the era's metal.
			vec2 g = texel / 2.6;
			g.x += mod(floor(g.y), 2.0) * 0.5;
			float d = length(fract(g) - 0.5);
			float w = fwidth(d) * 1.2 + 0.02;
			float ring = smoothstep(0.22 - w, 0.22, d) * (1.0 - smoothstep(0.46, 0.46 + w, d));
			float detail = clamp(1.6 - fwidth(g.x) * 3.0, 0.0, 1.0);
			vec3 steel = ramp(base * 0.42, base * 0.88, mix(base, vec3(1.0), 0.25), t);
			m = mix(steel * 0.9, mix(steel * 0.42, steel * 1.12, ring), detail);
		} else {
			// Plate: the era's metal on the art's shading, a glint on the lit step, a slow sheen for the rare ones.
			m = ramp(base * 0.42, base * 0.9, mix(base, vec3(1.0), 0.32), t);
			m += vec3(0.05) * step(0.99, t);
			// Now and then a glint sweeps across the rarer pieces.
			float sweep = fract(time * 0.12 - (texel.x - texel.y) / (sites_size.x + sites_size.y));
			m += extra.r * 0.22 * smoothstep(0.0, 0.025, sweep) * (1.0 - smoothstep(0.025, 0.07, sweep));
		}
		// The art's own lines (folds, muscles) show through, except the strands of hair under it.
		float ink = (1.0 - smoothstep(0.10, 0.22, lum)) * (1.0 - range.b);
		col = mix(m, art * 0.3, ink * 0.85);
	}
	col = mix(col, col * 0.3 + vec3(0.02, 0.015, 0.04), extra.a);
	if (hl.a > 0.0) {
		col = mix(col, hl.rgb, hl.a * 0.34) + hl.rgb * hl.a * 0.12;
	}
	return col;
}

void fragment() {
	vec4 art = texture(TEXTURE, UV);
	float lum = luma(art.rgb);
	// The four map pixels round this one, and how much of it each covers.
	vec2 p = UV * sites_size - 0.5;
	ivec2 i0 = ivec2(floor(p));
	vec2 f = fract(p);
	int a = site_px(i0), b = site_px(i0 + ivec2(1, 0)), c = site_px(i0 + ivec2(0, 1)), d = site_px(i0 + ivec2(1, 1));
	float wa = (1.0 - f.x) * (1.0 - f.y), wb = f.x * (1.0 - f.y), wc = (1.0 - f.x) * f.y, wd = f.x * f.y;
	float ca = wa + (b == a ? wb : 0.0) + (c == a ? wc : 0.0) + (d == a ? wd : 0.0);
	float cb = b == a ? 0.0 : wb + (c == b ? wc : 0.0) + (d == b ? wd : 0.0);
	float cc = (c == a || c == b) ? 0.0 : wc + (d == c ? wd : 0.0);
	float cd = (d == a || d == b || d == c) ? 0.0 : wd;
	int dom = a;
	float cdom = ca;
	if (cb > cdom) { dom = b; cdom = cb; }
	if (cc > cdom) { dom = c; cdom = cc; }
	if (cd > cdom) { dom = d; cdom = cd; }
	int sec = dom;
	float csec = 0.0;
	if (a != dom && ca > csec) { sec = a; csec = ca; }
	if (b != dom && cb > csec) { sec = b; csec = cb; }
	if (c != dom && cc > csec) { sec = c; csec = cc; }
	if (d != dom && cd > csec) { sec = d; csec = cd; }
	// The light, blurred a little (more over hair), so strands and the beard's spikes don't speckle what's worn.
	float r = mix(1.2, 3.2, row(dom, 4).b);
	vec2 o = TEXTURE_PIXEL_SIZE * r;
	float soft = lum * 0.2;
	soft += luma(texture(TEXTURE, UV + vec2(o.x, 0.0)).rgb) * 0.1;
	soft += luma(texture(TEXTURE, UV - vec2(o.x, 0.0)).rgb) * 0.1;
	soft += luma(texture(TEXTURE, UV + vec2(0.0, o.y)).rgb) * 0.1;
	soft += luma(texture(TEXTURE, UV - vec2(0.0, o.y)).rgb) * 0.1;
	soft += luma(texture(TEXTURE, UV + o * 0.7071).rgb) * 0.1;
	soft += luma(texture(TEXTURE, UV - o * 0.7071).rgb) * 0.1;
	soft += luma(texture(TEXTURE, UV + vec2(o.x, -o.y) * 0.7071).rgb) * 0.1;
	soft += luma(texture(TEXTURE, UV + vec2(-o.x, o.y) * 0.7071).rgb) * 0.1;
	vec2 texel = UV * sites_size;
	vec3 col = shade(dom, art.rgb, lum, soft, texel);
	// The edge between two sites, a screen pixel wide at any zoom (a plain blend of the two when zoomed out).
	float k = clamp(max(fwidth(p.x), fwidth(p.y)), 0.02, 1.0);
	float m = clamp((csec - 0.5) / k + 0.5, 0.0, 1.0);
	if (sec != dom && m > 0.002) {
		col = mix(col, shade(sec, art.rgb, lum, soft, texel), m);
	}
	COLOR = vec4(col, art.a * fade);
}
