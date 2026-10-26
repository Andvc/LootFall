#!/usr/bin/env python3
"""The equipment screen's art, phase 2: the bare body region by region, and every piece as it's worn
(docs/art-handoff-equipment-pieces.md).

  spec     writes art/equipment/handoff-pieces.json: every file to make, where it goes, its size, the picture it's
           painted on, its mask, the prompts, the references, the checks it must pass and what the game does with it.
  list     writes docs/art-handoff-equipment-pieces-list.md: the same list as a checklist, with what's delivered.
  inputs   writes what the files are painted on into art/equipment/inputs/ (not committed): each base picture (the
           delivered art on flat green at the size to paint at), each file's mask (transparent where to paint) and a
           guide (the base with the mask outlined). Run it again after delivering plates and a family's whole-figure
           pictures: plates become the bases of the pieces painted on them, and the whole-figure pictures seed the
           plates of a piece that spans regions.
  check    checks the delivered files against the spec (size, background, where the piece is, how much of its area
           it covers, the ink outline, no key green, plates against their seeds). Exit code 1 if any fails.
  sheet    review sheets in art/equipment/review/pieces/: each delivered file over its base, marked with its result.
  import   brings what's delivered and passes into the game (assets/equipment/plates/, pieces/, sprites/ and
           pieces.json): each plate keyed with its site map and slot points; each family file keyed, cut into its
           pieces, cropped, split into what's behind him and in front where it hangs behind, with its outline; the held
           pieces placed beside him. Then run `godot --headless --path . --import`. Needs scipy and scikit-image too.

Usage:
  python3 tools/equipment_pieces.py spec
  python3 tools/equipment_pieces.py list
  python3 tools/equipment_pieces.py inputs [ids or prefixes...]
  python3 tools/equipment_pieces.py check [ids or prefixes...]
  python3 tools/equipment_pieces.py sheet [ids or prefixes...]
  python3 tools/equipment_pieces.py import

An id is a delivered file's path under art/equipment/ without ".png" (plates/torso, pieces/figure_front/cloak,
sprites/main_hand); a prefix picks every id starting with it (pieces/head_front/). The body, its sites and slots come
from assets/equipment/ (tools/equipment_map.py) and art/content.json (`python3 tools/art.py manifest`). Needs Pillow
and numpy.
"""
import argparse
import json
import math
import os
import sys
from collections import OrderedDict

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import equipment_art as ea  # noqa: E402

ROOT = ea.ROOT
DROP = os.path.join(ROOT, "art", "equipment")
INPUTS = os.path.join(DROP, "inputs")
REVIEW = os.path.join(DROP, "review", "pieces")
EQUIP = os.path.join(ROOT, "assets", "equipment")
SPEC = os.path.join(DROP, "handoff-pieces.json")
LIST = os.path.join(ROOT, "docs", "art-handoff-equipment-pieces-list.md")
CONTENT = json.load(open(os.path.join(ROOT, "art", "content.json")))
MAP = json.load(open(os.path.join(EQUIP, "map.json")))
GREEN = np.array(ea.KEY, np.float32)
# What the image tool really returns: about 1.57 million pixels at the asked shape. Every canvas fits in it, so nothing
# is generated small and scaled up.
BUDGET = 1536 * 1024
INK = "#15101f"

# ---------------------------------------------------------------------------------------------------------------- body

SITES = {s["key"]: s for s in CONTENT["sites"]}
CELLS = {c["key"]: dict(c, site=s["key"], region=s["region"]) for s in CONTENT["sites"] for c in s["slots"]}
PIECES = CONTENT["pieces"]
BY_NAME = {p["name"]: p for p in PIECES}
WRAPPING = ("Under", "Mail", "Armor", "Over")
LAYERS = ["Skin", "Under", "Mail", "Armor", "Over", "Adorn", "Hold", "Graft", "Beyond", "Mod"]
# The regions whose pages show the figure: each gets plates. The head, hands and feet have their close-ups.
PLATE_REGIONS = ("Neck", "Torso", "Back", "Waist", "Right arm", "Left arm", "Right leg", "Left leg")


def leaves(site, layer):
    """The mapped sites a cell's site covers: a mask the face's parts, a shirt the torso front and back; what's
    wrapped round a limb (cloth, mail, plate, over) reaches round to its back too."""
    return [leaf for leaf, _, wrap in MAP["covers"].get(site, []) if not wrap or layer in WRAPPING]


def region_of(site):
    return SITES[site]["region"] if site in SITES else None


# ---------------------------------------------------------------------------------------------------------------- views

class View:
    """One mapped picture (assets/equipment/<name>.png and its site map)."""

    def __init__(self, name):
        j = MAP["views"][name]
        self.name = name
        self.w, self.h = j["size"]
        self.keys = [s["key"] for s in j["sites"]]
        self.index = {k: i + 1 for i, k in enumerate(self.keys)}
        self.ids = np.asarray(Image.open(os.path.join(EQUIP, f"{name}_sites.png")))
        self._rgba = None

    def rgba(self):
        if self._rgba is None:
            self._rgba = np.asarray(Image.open(os.path.join(EQUIP, self.name + ".png")).convert("RGBA")).astype(np.float32)
        return self._rgba


VIEWS = {n: View(n) for n in MAP["views"]}
# One body unit: a hundredth of the figure's height on the whole-figure views (from the top of his hair to his soles).
_rows = np.where(VIEWS["front"].ids.any(1))[0]
U_FIG = (_rows[-1] - _rows[0]) / 100.0
# How much bigger the body is on each close-up than on the whole figure (measured on the nose, the thumb, the big toe).
MAG = {"head": 2.45, "hand": 3.1, "foot": 3.5}


def fit(w, h):
    """The biggest canvas with this shape in the budget, each side a multiple of 16."""
    k = math.sqrt(BUDGET / (w * h))
    return int(w * k // 16 * 16), int(h * k // 16 * 16)


def shaped(rect, size):
    """The rectangle grown round its middle to the canvas's shape."""
    x0, y0, x1, y1 = rect
    W, H = size
    k = min(W / (x1 - x0), H / (y1 - y0))
    cx, cy, hw, hh = (x0 + x1) / 2, (y0 + y1) / 2, W / k / 2, H / k / 2
    return (round(cx - hw, 2), round(cy - hh, 2), round(cx + hw, 2), round(cy + hh, 2))


class Base:
    """A picture to paint on: a rectangle of one view, at a canvas size. The view's pixels map onto the canvas by one
    scale and offset, so what's painted lines up with the figure."""

    def __init__(self, name, view, rect, kind, group, title, region=None, size=None):
        self.name, self.view, self.kind, self.group, self.title, self.region = name, view, kind, group, title, region
        x0, y0, x1, y1 = rect
        self.size = size or fit(x1 - x0, y1 - y0)
        self.rect = shaped(rect, self.size)
        self.k = self.size[0] / (self.rect[2] - self.rect[0])
        self.mag = MAG.get(group, 1.0)
        self.U = U_FIG * self.mag * self.k
        self._ids = None

    @property
    def V(self):
        return VIEWS[self.view]

    def ids(self):
        """Which site each canvas pixel is (0 for none)."""
        if self._ids is None:
            W, H = self.size
            v = self.V
            xs = np.floor(self.rect[0] + (np.arange(W) + 0.5) / self.k).astype(int)
            ys = np.floor(self.rect[1] + (np.arange(H) + 0.5) / self.k).astype(int)
            okx, oky = (xs >= 0) & (xs < v.w), (ys >= 0) & (ys < v.h)
            ids = np.zeros((H, W), np.uint8)
            sub = v.ids[np.clip(ys, 0, v.h - 1)][:, np.clip(xs, 0, v.w - 1)]
            ids[np.ix_(oky, okx)] = sub[np.ix_(oky, okx)]
            self._ids = ids
        return self._ids

    def has(self, site):
        return site in self.V.index

    def mask(self, sites):
        idx = [self.V.index[s] for s in sites if s in self.V.index]
        if not idx:
            return np.zeros(self.size[::-1], bool)
        return np.isin(self.ids(), idx)

    def body(self):
        return self.ids() > 0

    def box(self, sites):
        """The sites' bounding box on the canvas (x0, y0, x1, y1), or None if none of them is on this view."""
        m = self.mask(sites)
        if not m.any():
            return None
        ys, xs = np.where(m)
        return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1

    def centre(self, sites):
        m = self.mask(sites)
        if not m.any():
            return None
        ys, xs = np.where(m)
        return xs.mean(), ys.mean()

    def from_view(self, rgba):
        """A view-sized RGBA picture moved onto the canvas (premultiplied, so its edges don't darken)."""
        W, H = self.size
        a = rgba[..., 3:4] / 255.0
        data = (1 / self.k, 0, self.rect[0], 0, 1 / self.k, self.rect[1])
        out = []
        for ch in [rgba[..., i] * a[..., 0] for i in range(3)] + [a[..., 0] * 255]:
            im = Image.fromarray(ch.astype(np.float32), "F")
            out.append(np.asarray(im.transform((W, H), Image.AFFINE, data, Image.BICUBIC)))
        pre, alpha = np.dstack(out[:3]), np.clip(out[3], 0, 255)
        an = np.maximum(alpha / 255.0, 1e-6)[..., None]
        colour = np.clip(pre / an, 0, 255)
        return np.dstack([colour, alpha])

    def seed(self):
        """The view on flat green at the canvas size: what's painted on until a plate of its own is delivered."""
        rgba = self.from_view(self.V.rgba())
        return on_green(rgba)


def on_green(rgba):
    a = rgba[..., 3:4] / 255.0
    return np.clip(rgba[..., :3] * a + GREEN * (1 - a), 0, 255).astype(np.uint8)


def figure_base(side):
    # Room above his head for hats, horns and banners, and at his sides for wings and cloaks.
    return Base(f"figure_{side}", side, (-100, -305, 1040, 1705), "figure", side,
                f"the whole knight seen from {'the front' if side == 'front' else 'behind'}")


CLOSE_TITLES = {
    "head_right": "a close-up of his head and neck in profile, his right side to us",
    "head_front": "a close-up of his head and neck from the front",
    "head_left": "a close-up of his head and neck in profile, his left side to us",
    "hand_R_back": "a close-up of the back of his right hand, fingers up, with the wrist and forearm",
    "hand_R_palm": "a close-up of the palm of his right hand, fingers up, with the wrist and forearm",
    "hand_L_back": "a close-up of the back of his left hand, fingers up, with the wrist and forearm",
    "hand_L_palm": "a close-up of the palm of his left hand, fingers up, with the wrist and forearm",
    "foot_R_top": "a close-up of the top of his right foot, toes up, with the ankle",
    "foot_R_sole": "a close-up of the sole of his right foot, toes up, with the ankle",
    "foot_L_top": "a close-up of the top of his left foot, toes up, with the ankle",
    "foot_L_sole": "a close-up of the sole of his left foot, toes up, with the ankle",
}
CLOSE_REGION = {"head": "Head", "hand_R": "Right hand", "hand_L": "Left hand", "foot_R": "Right foot", "foot_L": "Left foot"}


def close_base(view, tall=False):
    v = VIEWS[view]
    group = view.split("_")[0]
    if tall:
        # Room for tall headgear: a pointed hat, horns, a crown's points, a crest.
        rect = (-0.22 * v.w, -0.62 * v.h, 1.22 * v.w, 1.0 * v.h)
    else:
        rect = (-0.05 * v.w, -0.06 * v.h, 1.05 * v.w, 1.02 * v.h)
    region = CLOSE_REGION["head" if group == "head" else view[:6]]
    return Base(view + ("_tall" if tall else ""), view, rect, "closeup", group,
                CLOSE_TITLES[view] + (", with room above for tall headgear" if tall else ""), region)


PLATE_NAMES = {("Neck", "front"): "neck", ("Torso", "front"): "torso", ("Back", "back"): "back",
               ("Waist", "front"): "waist_front", ("Waist", "back"): "waist_back",
               ("Right arm", "front"): "right_arm_front", ("Right arm", "back"): "right_arm_back",
               ("Left arm", "front"): "left_arm_front", ("Left arm", "back"): "left_arm_back",
               ("Right leg", "front"): "right_leg_front", ("Right leg", "back"): "right_leg_back",
               ("Left leg", "front"): "left_leg_front", ("Left leg", "back"): "left_leg_back"}
PLATE_WORDS = {"Neck": "neck and collarbones", "Torso": "chest and belly", "Back": "back",
               "Waist": "waist, hips and the top of the thighs", "Right arm": "right arm", "Left arm": "left arm",
               "Right leg": "right leg", "Left leg": "left leg"}


def plate_bases():
    out = []
    for region in PLATE_REGIONS:
        for panel in MAP["pages"][region]:
            side = panel["view"]
            x0, y0, x1, y1 = panel["rect"]
            m = 0.06 * max(x1 - x0, y1 - y0)
            name = PLATE_NAMES[(region, side)]
            title = f"the knight's {PLATE_WORDS[region]} {'from the front' if side == 'front' else 'from behind'}, up close"
            out.append(Base(name, side, (x0 - m, y0 - m, x1 + m, y1 + m), "plate", side, title, region))
    return out


PLATES = plate_bases()
BASES = OrderedDict()
for b in [figure_base("front"), figure_base("back")] + PLATES:
    BASES[b.name] = b
for vn in CLOSE_TITLES:
    BASES[vn] = close_base(vn)
for vn in ("head_right", "head_front", "head_left"):
    BASES[vn + "_tall"] = close_base(vn, tall=True)


def plate_file(name):
    return os.path.join(DROP, "plates", name + ".png")


_PICTURES = {}


def base_picture(base):
    """What a piece is painted on: a delivered plate, or the view on green (a plate's seed until it's delivered), and
    whether it's final (a plate's seed isn't)."""
    if base.name not in _PICTURES:
        got = None
        if base.kind == "plate" and os.path.exists(plate_file(base.name)):
            im = Image.open(plate_file(base.name)).convert("RGB")
            if im.size == tuple(base.size):
                got = (np.asarray(im), True)
        _PICTURES[base.name] = got or (base.seed(), base.kind != "plate")
    return _PICTURES[base.name]


# ---------------------------------------------------------------------------------------------------------------- masks

def grow(mask, r):
    """Grown by about r pixels (a rounded square, alternating straight and diagonal steps)."""
    r = int(round(r))
    if r <= 0:
        return mask.copy()
    m = mask.copy()
    for i in range(r):
        n = m.copy()
        n[1:] |= m[:-1]
        n[:-1] |= m[1:]
        n[:, 1:] |= m[:, :-1]
        n[:, :-1] |= m[:, 1:]
        if i % 2:
            n[1:, 1:] |= m[:-1, :-1]
            n[:-1, :-1] |= m[1:, 1:]
            n[1:, :-1] |= m[:-1, 1:]
            n[:-1, 1:] |= m[1:, :-1]
        m = n
    return m


def shrink_mask(mask, r):
    return ~grow(~mask, r)


def coarse(mask, r, f):
    """grow() for big radii: on a smaller copy, then scaled back up."""
    if r < 24:
        return f(mask, r)
    s = max(2, int(r // 12))
    H, W = mask.shape
    small = np.asarray(Image.fromarray(mask.astype(np.uint8) * 255).resize((max(1, W // s), max(1, H // s)), Image.BOX)) > 0
    small = f(small, r / s)
    return np.asarray(Image.fromarray(small.astype(np.uint8) * 255).resize((W, H), Image.BILINEAR)) > 127


def rect_mask(size, x0, y0, x1, y1):
    W, H = size
    m = np.zeros((H, W), bool)
    x0, y0, x1, y1 = (int(max(0, min(W, v))) if i % 2 == 0 else int(max(0, min(H, v))) for i, v in enumerate((x0, y0, x1, y1)))
    m[y0:y1, x0:x1] = True
    return m


def strap_mask(size, a, b, w):
    im = Image.new("L", size, 0)
    ImageDraw.Draw(im).line([a, b], fill=255, width=max(1, int(w)))
    r = w / 2
    d = ImageDraw.Draw(im)
    for p in (a, b):
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=255)
    return np.asarray(im) > 0


def ellipse_mask(size, box):
    im = Image.new("L", size, 0)
    ImageDraw.Draw(im).ellipse(box, fill=255)
    return np.asarray(im) > 0


def extent(base, prim):
    """One shape of a piece's reach beyond the sites it fills, on a base (empty if its sites aren't on it).
    Distances are in body units (a hundredth of his height, as big as the body is drawn on this base)."""
    U = base.U
    kind = prim[0]
    if kind == "box":
        _, sites, l, t, r, b = prim
        bx = base.box(sites)
        return None if bx is None else rect_mask(base.size, bx[0] - l * U, bx[1] - t * U, bx[2] + r * U, bx[3] + b * U)
    if kind in ("above", "below"):
        _, sites, h, w = prim
        bx = base.box(sites)
        if bx is None:
            return None
        x0, y0, x1, y1 = bx
        if kind == "above":
            return rect_mask(base.size, x0 - w * U, y0 - h * U, x1 + w * U, y0 + 0.35 * (y1 - y0))
        return rect_mask(base.size, x0 - w * U, y1 - 0.35 * (y1 - y0), x1 + w * U, y1 + h * U)
    if kind == "strap":
        _, a, b, w = prim
        ca, cb = base.centre([a] if isinstance(a, str) else a), base.centre([b] if isinstance(b, str) else b)
        return None if ca is None or cb is None else strap_mask(base.size, ca, cb, w * U)
    if kind == "sites":
        _, sites, r = prim
        m = base.mask(sites)
        return coarse(m, r * U, grow) if m.any() else None
    raise ValueError(kind)


# ---------------------------------------------------------------------------------------------------------------- style

STYLE = ea.STYLE
PALETTE = ("Materials, each in exactly three flat tones: steel and iron in neutral grey with no tint at all (shadow "
           "#4a4a4f, mid #8a8a90, light #cfcfd4; the game recolours plain metal for each era, so never tint, blue, gild "
           "or blacken it unless asked); leather in warm brown (#4a2f22, #7a5038, #a77552); gold and brass (#7a4f12, "
           "#c48a22, #f2c45a); wood (#4b3220, #7b5534, #a77a4e); bone, horn and ivory (#8f7f63, #c9b994, #ece2c8); "
           "gemstones as clear colourless crystal, white with pale grey facets (the game tints them); cloth in the "
           "dye named, muted, never bright. No colours of the rarity ladder (no bright green, blue, purple, orange, red, "
           "pink or cyan), no glow, no magic light, no sparkles, and nothing green at all: the background green is keyed "
           "out.")
INK_STYLE = ("Tattoo ink: flat dark blue-black ink (#1b1f2e) on the skin, crisp lines and solid shapes with no shading, no "
             "outline round the design and no colour; it follows the curve of the body and the skin's own light shows "
             "between its lines (the game tints the ink).")
DYES = {"madder": ("madder red", "#7a3b3b"), "woad": ("woad blue", "#4f5d75"), "walnut": ("walnut brown", "#6d4c3d"),
        "dun": ("undyed dun wool", "#8a7a5a"), "slate": ("slate blue-grey", "#3c4a5c"), "russet": ("russet tan", "#8c6d46"),
        "linen": ("undyed linen", "#cfc5b4"), "black": ("black wool", "#2b2630"), "ochre": ("ochre yellow", "#a4844a")}


def dye_words(d):
    name, hexv = DYES[d]
    return f"{name} ({hexv} for the middle tone)"


# ---------------------------------------------------------------------------------------------------------------- the pieces

def P(look, **kw):
    """A piece painted on the body, on every picture where it shows."""
    return dict(cls="overlay", look=look, **kw)


def S(look, **kw):
    """A piece drawn on its own canvas and placed beside or around him by the game."""
    return dict(cls="sprite", look=look, **kw)


def C(why):
    """A piece the game draws itself (or that can't be seen)."""
    return dict(cls="code", why=why)


HEAD_ALL = ["scalp", "brow", "L.ear", "R.ear", "nape", "L.cheek", "R.cheek", "chin"]

# Every body piece family: how it's drawn. Looks describe the piece as worn; the prompts add where, the materials and
# which side shows. reach: where it goes beyond the sites it fills, by picture ("front" and "back" for the whole figure
# and the plates, "head", "hand", "foot", "*" for all), as shapes (extent()); cover: how much of its sites it should
# hide ("full", "part"); ink: a tattoo; dye: its cloth's colour (DYES); tall: on the head close-ups with room above;
# behind: from the front it hangs behind him; pages: regions whose plates it's painted on besides its own (a robe's
# skirt over the legs).
FAMILIES = {
    # ---------------------------------------------------------------------------------------------------- back
    "back_scabbard": P("a longsword in a brown leather scabbard strapped diagonally across his back, the hilt rising "
                       "behind his right shoulder and the tip at his left hip, held by a leather strap that crosses his "
                       "chest from his right shoulder to his left hip",
                       reach={"back": [("strap", "R.blade", "L.kidney", 2.8), ("above", ["R.shoulder"], 5, 0.5)],
                              "front": [("above", ["R.shoulder"], 5, 0.2), ("strap", "R.shoulder", "L.hip", 1.2)]},
                       cover="part", behind=True),
    "back_tattoo": P("a large tattoo over the whole back, from the shoulder blades down to the small of the back: a "
                     "great tree of knotwork, its roots spreading over the small of the back and its branches over the "
                     "shoulder blades", ink=True),
    "backpack": P("a sturdy brown leather rucksack with a rolled-down flap and two brass buckles, worn high on the back "
                  "on two shoulder straps that come over the tops of his shoulders and run down the front of his chest to "
                  "his armpits",
                  reach={"back": [("box", ["L.blade", "R.blade", "upspine", "midspine", "lumbar"], 0.5, 0.5, 0.5, 0)],
                         "front": [("strap", "L.shoulder", "L.armpit", 1.4), ("strap", "R.shoulder", "R.armpit", 1.4)]},
                  cover="part", behind=True),
    "backplate": P("a steel backplate shaped to his back, with a raised ridge down the spine, a rolled top edge below "
                   "the neck and leather straps with buckles at the sides"),
    "cape": P("a short riding cape hanging from his shoulders to mid-thigh at the back, fastened at the collarbones with "
              "a cord", dye="woad",
              reach={"back": [("box", ["L.shoulder", "R.shoulder", "L.hamstring", "R.hamstring"], 0.6, 0.3, 0.6, -6)],
                     "front": [("box", ["L.shoulder", "R.shoulder"], 0.3, 0.3, 0.3, 0),
                               ("box", ["L.shoulder", "R.shoulder", "L.thigh", "R.thigh"], -1.0, 0, -1.0, -6),
                               ("strap", "L.clavicle", "R.clavicle", 0.8)]},
              behind=True),
    "cloak": P("a long travelling cloak of heavy wool over both shoulders, falling to his calves at the back, fastened at "
               "the base of his throat with a round brass clasp", dye="walnut",
               reach={"back": [("box", ["L.shoulder", "R.shoulder", "L.calf", "R.calf"], 0.8, 0.3, 0.8, -3)],
                      "front": [("box", ["L.shoulder", "R.shoulder"], 0.3, 0.4, 0.3, 0),
                                ("box", ["L.shoulder", "R.shoulder", "L.shin", "R.shin"], 0.4, 0, 0.4, -4),
                                ("sites", ["collar"], 0.4)]},
               behind=True),
    "dorsal_spines": P("a row of five curved spines of bone grown from his spine between the shoulder blades, ivory with "
                       "darker tips, each pointing up and back",
                       reach={"back": [("box", ["upspine", "midspine"], 1.6, 1.0, 1.6, 0)]}, cover="part"),
    "kidney_dagger": P("a rondel dagger in a leather sheath worn crosswise at the small of the back on each side, its "
                       "grip toward the hip", reach={"back": [("sites", ["L.kidney", "R.kidney"], 0.8)]}, cover="part"),
    "kidney_guard": P("a small curved steel plate over each kidney, held by a strap round the waist"),
    "kidney_tattoo": P("a small tattoo over each kidney: a ring of knotwork round a dot", ink=True),
    "lat_guard": P("a curved steel plate guarding each side of the back below the shoulder blades"),
    "lat_tattoo": P("a tattoo along each side of the back: a long feather, its quill toward the armpit", ink=True),
    "lower_back_tattoo": P("a tattoo across the small of the back: a band of interlaced knotwork with a round sigil in "
                           "the middle", ink=True),
    "lumbar_plate": P("a broad steel plate across the small of the back, made of three overlapping horizontal lames"),
    "mid_back_tattoo": P("a tattoo down the middle of the spine: a column of small knots, one above another", ink=True),
    "quiver": P("a tall brown leather quiver full of arrows slung across his back, its mouth behind his right shoulder "
                "with the grey-feathered fletchings rising above the shoulder, its strap crossing his chest",
                reach={"back": [("strap", "R.blade", "L.kidney", 3.0), ("above", ["R.shoulder"], 6, 0.5)],
                       "front": [("above", ["R.shoulder"], 6, 0.2), ("strap", "L.shoulder", "R.flank", 1.1)]},
                cover="part", behind=True),
    "scapula_plate": P("a steel plate shaped over each shoulder blade, strapped under the arm"),
    "shoulder_blade_tattoo": P("a tattoo on each shoulder blade: a spread raven's wing drawn in knotwork", ink=True),
    "spine_guard": P("a narrow guard of small overlapping steel plates running down the upper spine, from the base of "
                     "the neck to between the shoulder blades"),
    "spine_piercings": P("a vertical row of six small steel ring piercings down the upper spine", cover="part"),
    "tail": P("a long thick tail grown from the base of his spine, covered in small overlapping slate-grey scales with a "
              "paler underside, hanging down behind him and curling at its end just above the floor; from the front only "
              "its end shows, beside his left calf",
              reach={"back": [("box", ["seat"], -1.5, -1.0, -1.5, 30), ("box", ["L.calf", "R.calf"], 1.5, 0, 1.5, 3)],
                     "front": [("box", ["L.shin"], -1.0, -4, 4, 4)]}, cover="part", behind=True),
    "upper_spine_tattoo": P("a tattoo down the upper spine: a sword pointing down, its hilt at the base of the neck",
                            ink=True),
    "war_banner": P("a tall war banner carried on his back: a wooden pole strapped along his spine, rising far above his "
                    "head, with a crossbar from which hangs a long narrow cloth banner, plain with no emblem", dye="madder",
                    reach={"back": [("box", ["upspine", "midspine", "lumbar"], 0.4, 0, 0.4, 0),
                                    ("above", ["scalp", "L.shoulder", "R.shoulder"], 18, -2)],
                           "front": [("above", ["scalp"], 18, 3)]}, cover="part", behind=True),
    "wings": P("a great pair of feathered wings grown from his shoulder blades, half spread, the long flight feathers "
               "reaching below his knees; pale grey-white feathers with darker grey tips, in clean overlapping rows",
               reach={"back": [("box", ["L.blade", "R.blade", "L.calf", "R.calf"], 9, 6, 9, -2)],
                      "front": [("box", ["L.shoulder", "R.shoulder", "L.knee", "R.knee"], 8, 5, 8, 0)]},
               cover="part", behind=True),
    # ---------------------------------------------------------------------------------------------------- beyond
    "aura": C("a glow round the whole figure in the rarity's colour, drawn by the game"),
    "comet": S("a small comet: a rough rocky head with a long streaming tail of pale light, flying from the bottom left "
               "to the top right"),
    "constellation": C("stars joined by fine lines in the sky above his head, drawn by the game from each "
                       "constellation's stars"),
    "eclipse": S("an eclipse: a perfectly black disc with a ragged pale corona of light round it and a thin bright rim "
                 "on its lower left edge"),
    "familiar": S("his familiar: a small black cat with amber eyes, sitting upright and alert, seen from the front and "
                  "a little to the side, its tail curled round its paws"),
    "ioun_stone": S("six small orbiting gemstones (ioun stones)"),
    "shadow_cloak": C("his living shadow: the figure's own outline, dark, offset and wavering, drawn by the game"),
    "steed": S("his warhorse: a strong destrier, standing in profile facing the viewer's left, head up, bare with only a "
               "simple rope halter, a dark bay coat with black mane and tail"),
    # ---------------------------------------------------------------------------------------------------- head
    "arming_cap": P("a close-fitting quilted linen arming cap over the top and back of his head, its edge just above the "
                    "brow and passing above the ears, the braid hanging free below it", dye="linen", tall=False),
    "beard_rings": P("three small silver rings braided into the beard at the chin", cover="part"),
    "bevor": P("a steel bevor: a curved plate guarding his chin and throat, rising from the collarbones to just under the "
               "lower lip, strapped behind the neck"),
    "braid_beads": P("four carved metal beads threaded onto his braid, spaced along it", cover="part"),
    "bridge_stud": P("a small steel bar piercing through the bridge of the nose, a bead on each side", cover="part"),
    "brow_guard": P("a curved steel band across the brow, like a reinforced headband, riveted at the temples"),
    "brow_tattoo": P("a small tattoo across the brow: a row of dots with a little rune in the middle", ink=True),
    "cheek_gem": P("a small cut gem set into the skin on each cheekbone", cover="part"),
    "cheek_guard": P("a steel cheek plate over each cheek, hinged at the temple on a strap round the head, leaving the "
                     "eyes, nose and mouth free"),
    "cheek_tattoo": P("a tattoo on each cheek: three short curved parallel lines with a dot", ink=True),
    "chin_guard": P("a small steel plate cupping the chin, on a strap round the back of the head"),
    "chin_tattoo": P("a tattoo on the chin below the lip: a short vertical line of dots, half hidden by the beard",
                     ink=True),
    "circlet": P("a thin plain metal circlet round his head across the brow, passing above the ears", cover="part"),
    "coif": P("a close-fitting padded linen coif covering the top and back of the head and the nape, tied under the "
              "chin with a cord, the ears free through slits", dye="linen"),
    "crown": P("a gold crown on his head: a band set round the head above the brow with five tall points, each point "
               "ending in a small knob", tall=True, reach={"*": [("above", ["scalp"], 4, 0.4)]}, cover="part"),
    "diadem": P("a delicate gold diadem across the brow, rising to a peak in the middle", cover="part"),
    "ear_chain": P("a fine chain on each ear running from the lobe up to the top of the rim", cover="part"),
    "ear_cuff": P("a metal cuff clasped round the middle of each ear's rim", cover="part"),
    "ear_tip": P("elven ear tips: a pointed metal sheath fitted over the top of each ear, making it long and pointed",
                 reach={"*": [("above", ["L.ear", "R.ear"], 2.0, 0.6)]}, cover="part"),
    "earring": P("a small gold hoop in each earlobe", cover="part"),
    "eyebrow_ring": P("a small ring through the outer end of each eyebrow", cover="part"),
    "eyepatch": P("a black leather eyepatch over his left eye, on a thin cord round his head", cover="part",
                  reach={"*": [("strap", "L.eye", "L.ear", 0.5)]}),
    "face_plate": P("a steel face plate covering the whole face below the brow, with two eye slits and rows of small "
                    "breathing holes, strapped behind the head"),
    "fang_caps": P("pointed steel caps on his canine teeth: his lips are parted a little to bare them", cover="part"),
    "forehead_jewel": P("a teardrop jewel hanging in the middle of the forehead from a fine chain round the brow",
                        cover="part"),
    "goggles": P("brass goggles with round clear lenses over both eyes, on a leather strap round the head",
                 reach={"*": [("box", ["L.eye", "R.eye", "L.ear", "R.ear"], 0.3, 0.3, 0.3, 0.3)]}),
    "gold_teeth": P("gold teeth: his lips are parted a little in a slight grin, showing gold upper teeth", cover="part"),
    "great_helm": P("a great helm: a flat-topped steel cylinder enclosing the whole head down to the neck, with a "
                    "narrow horizontal eye slit, rows of small breathing holes on the right of the face, a vertical "
                    "riveted ridge down the middle of the face",
                    reach={"*": [("box", HEAD_ALL + ["face"], 0.6, 0.8, 0.6, 0.3)]}),
    "hair_pin": P("two long ornamental pins pushed through the hair at the back of the head, crossing, their round heads "
                  "showing", cover="part"),
    "hair_ribbon": P("a ribbon tied in a bow round the end of his braid", dye="madder", cover="part"),
    "halo": C("a ring of light above his head, drawn by the game"),
    "helix_ring": P("a small ring through the upper rim of each ear", cover="part"),
    "hood": P("a hood of heavy wool pulled up over the head, its edge framing the face, its short cape covering the neck "
              "and the tops of the shoulders", dye="slate",
              reach={"*": [("box", HEAD_ALL + ["hair", "neck"], 0.9, 0.9, 0.9, 0.6)],
                     "front": [("box", ["neck", "L.clavicle", "R.clavicle"], 0.5, 0, 0.5, 0)]}),
    "horned_helm": P("a closed steel helm with a visored face, from whose sides two great ridged horns curve outward and "
                     "up like a ram's, bone-grey with dark tips", tall=True,
                     reach={"*": [("box", HEAD_ALL + ["face"], 0.6, 0.8, 0.6, 0.3), ("above", ["scalp"], 5, 4)]}),
    "horns": P("a pair of horns grown from his temples, ridged, sweeping back and then up, bone-grey darkening to the "
               "tips", tall=True, reach={"*": [("above", ["scalp"], 5, 3)]}, cover="part"),
    "kohl": P("dark kohl lines round each eye, winged at the outer corner", ink=True),
    "lip_ring": P("a small ring through the lower lip, to one side", cover="part"),
    "mask": P("a carved leather half-mask over the upper face, from the brow to below the cheekbones, with almond eye "
              "holes, tied behind the head"),
    "monocle": P("a round monocle in a thin metal rim over his right eye", cover="part"),
    "mouth_guard": P("a curved steel guard over the mouth with three breathing slits, on a strap round the head"),
    "nape_guard": P("a curved steel plate guarding the nape, on a strap round the neck"),
    "nape_tattoo": P("a tattoo on the nape: a small knotwork triangle", ink=True),
    "nasal_helm": P("a conical steel helm with a rim round the head and a long nose guard down over the nose",
                    reach={"*": [("above", ["scalp"], 1.5, 0.4)]}),
    "neck_cloth": P("a linen neck cloth hanging from under the hair over the back of the neck, against the sun",
                    dye="linen"),
    "nose_chain": P("a fine chain from a stud in the right nostril to a ring in the right ear", cover="part"),
    "nose_guard": P("a narrow steel nose guard hanging from a band across the brow"),
    "nose_stud": P("a small stud in each nostril", cover="part"),
    "sallet": P("a sallet: a rounded steel helmet sweeping back into a long tail over the nape, open at the face (eyes, "
                "nose and mouth free), its edge low over the cheeks",
                reach={"*": [("above", ["scalp"], 1.2, 0.5), ("sites", ["nape", "L.ear", "R.ear"], 0.8)]}),
    "scalp_tattoo": C("ink under his hair: the game draws a faint pattern in the hair where it's worn"),
    "septum_ring": P("a small ring through the septum", cover="part"),
    "spectacles": P("round wire-rimmed spectacles over both eyes, their arms going back over the ears", cover="part",
                    reach={"head": [("box", ["L.eye", "R.eye", "L.ear", "R.ear"], 0.2, 0.2, 0.2, 0)],
                           "front": [("box", ["L.eye", "R.eye", "L.ear", "R.ear"], 0.2, 0.2, 0.2, 0)]}),
    "starcrown": C("a crown of stars floating above his head, drawn by the game"),
    "third_eye": P("a third eye in the middle of the forehead: an almond-shaped eye of carved ivory with a grey stone "
                   "iris, its lids half open", cover="part"),
    "tongue_stud": C("behind closed lips: not seen"),
    "war_paint": P("bold war paint in dark ink: a broad band across the brow, three stripes down each cheek and one down "
                   "the chin", ink=True),
    "wizard_hat": P("a tall pointed wizard's hat with a wide, slightly floppy brim, its tip bent back", dye="slate",
                    tall=True, reach={"*": [("above", ["scalp"], 10, 2.5)]}),
    # ---------------------------------------------------------------------------------------------------- arms
    "arm_harness": P("plate armour for the arm below the shoulder: a tube of plate on the upper arm, a cop with a "
                     "round fan-shaped side wing at the elbow, and a vambrace on the forearm, joined by leather straps"),
    "arm_wraps": P("linen strips wrapped round each forearm from the wrist to below the elbow", dye="linen"),
    "bangle_stack": P("a stack of four thin metal bangles on each wrist", cover="part"),
    "besagew": P("a round steel disc (a besagew) hanging at the front of each armpit"),
    "bracelet": P("two bracelets on each wrist: a plain gold band near the hand and, just above it, a bracelet of "
                  "small carved beads", cover="part"),
    "bracer": P("a short steel bracer round each wrist and the base of the forearm"),
    "couter": P("a steel couter cupping each elbow, with a round fan-shaped wing on the outside"),
    "elbow_tattoo": P("a tattoo on each elbow: a spider's web of fine lines", ink=True),
    "forearm_sheath": P("a slim knife in a leather sheath strapped along the outside of each forearm", cover="part"),
    "full_arm_harness": P("full plate armour for the whole arm: a pauldron of overlapping lames over the shoulder, a "
                          "round besagew at the armpit, a tube on the upper arm, a winged couter at the elbow and a "
                          "vambrace on the forearm, all articulated",
                          reach={"front": [("sites", ["L.shoulder", "R.shoulder"], 1.2)]}),
    "mail_sleeve": P("a short mail sleeve covering each upper arm, from the shoulder to just above the elbow"),
    "mantle": P("a short wool mantle over both shoulders, covering the shoulders and the top of the chest and back, its "
                "edge cut in scallops", dye="madder",
                reach={"front": [("box", ["L.shoulder", "R.shoulder", "L.clavicle", "R.clavicle"], 0.4, 0.3, 0.4, 1)],
                       "back": [("box", ["L.shoulder", "R.shoulder", "L.blade", "R.blade"], 0.4, 0.3, 0.4, -4)]}),
    "pauldron": P("a large steel pauldron over each shoulder: three overlapping lames curving down over the top of the "
                  "arm, the top lame with a raised ridge on the neck side", reach={"*": [("sites", ["L.shoulder", "R.shoulder"], 1.0)]}),
    "rerebrace": P("a tube of steel plate round each upper arm, from the shoulder to the elbow"),
    "shoulder_cape": P("a short half-cape hanging from each shoulder over the upper arm, fastened with a small brooch at "
                       "the collarbone", dye="woad", reach={"*": [("sites", ["L.shoulder", "R.shoulder"], 1.0)]}),
    "shoulder_lantern": P("a small brass lantern with clear glass panes, unlit, hanging from a hook on a strap over his "
                          "left shoulder", reach={"*": [("below", ["L.shoulder"], 4, 0.5)]}, cover="part"),
    "shoulder_tattoo": P("a tattoo on each shoulder: a round knotwork medallion", ink=True),
    "wrist_tattoo": P("a tattoo round each wrist: a band of knotwork like a bracelet of ink", ink=True),
    "arm_cuff": P("a wide metal cuff clasped round the back of each upper arm", cover="part"),
    "arming_sleeve": P("a padded linen sleeve covering each upper arm, quilted in vertical lines", dye="linen"),
    "armlet": P("a coiled gold armlet, like a snake, round each upper arm", cover="part"),
    "elbow_spike": P("a sharp steel spike on a small plate strapped to each elbow, pointing back and out",
                     reach={"*": [("sites", ["L.elbow", "R.elbow"], 1.6)]}, cover="part"),
    "forearm_tattoo": P("a tattoo along each forearm: a long serpent winding round the arm", ink=True),
    "shoulder_familiar": S("a falcon perched on his right shoulder"),
    "sleeve_tattoo": P("a full sleeve tattoo covering each arm from the shoulder to the wrist: flowing knotwork with "
                       "waves", ink=True),
    "spaulder": P("a smaller steel spaulder: three narrow plates over the point of each shoulder, the armpit left open",
                  reach={"*": [("sites", ["L.shoulder", "R.shoulder"], 0.7)]}),
    "triceps_tattoo": P("a tattoo on the back of each upper arm: an arrow pointing down", ink=True),
    "upper_arm_tattoo": P("a tattoo round each upper arm: a band of angular runes", ink=True),
    "vambrace": P("a steel vambrace enclosing each forearm from the wrist to the elbow, in two hinged halves"),
    "wrist_blade": P("a hidden blade: a slim spring blade under a leather bracer on each forearm, its tip showing past "
                     "the wrist", cover="part"),
    # ---------------------------------------------------------------------------------------------------- feet
    "armoured_boot": P("an armoured boot on each foot: a brown leather boot to above the ankle with articulated steel "
                       "plates over the top of the foot and the ankle"),
    "foot_charm": P("a small charm on a fine chain across the top of each foot", cover="part"),
    "foot_tattoo": P("a tattoo on the top of each foot: a small compass rose", ink=True),
    "heel_spike": P("a short steel spike strapped to each heel, pointing back",
                    reach={"*": [("sites", ["L.heel", "R.heel"], 1.2)]}, cover="part"),
    "sabaton": P("a steel sabaton over each foot: overlapping lames over the top of the foot ending in a rounded toe cap"),
    "sandal": P("a leather sandal on each foot: a sole held by straps across the foot and round the ankle",
                reach={"*": [("sites", ["L.foot", "R.foot", "L.ankle", "R.ankle"], 0.2)]}, cover="part"),
    "sock": P("a knitted wool sock on each foot, up to the ankle", dye="dun",
              reach={"*": [("sites", ["L.ankle", "R.ankle"], 0.1)]}),
    "toe_talons": P("sharp curved metal talons capping each toe", cover="part"),
    "toenail_paint": P("each toenail painted in dark lacquer", ink=True),
    "boot": P("a sturdy brown leather boot on each foot, reaching just above the ankle, laced at the front"),
    "foot_chain": P("a fine chain from a ring on the second toe across the top of the foot to the ankle", cover="part"),
    "heel_tattoo": P("a tattoo on each heel: a small winged mark", ink=True),
    "toe_ring": P("a thin metal ring on each of the ten toes", cover="part"),
    # ---------------------------------------------------------------------------------------------------- hands
    "fingerless_glove": P("a fingerless brown leather glove on each hand, the fingers bare from the first knuckle",
                          cover="part"),
    "focus_orb": S("a focus orb: a clear crystal sphere held in a cradle of three curved metal claws"),
    "glove": P("a soft brown leather glove on each hand, with a short cuff"),
    "hand_tattoo": P("a tattoo on the back of each hand: a knotwork compass", ink=True),
    "knuckleduster": P("a brass knuckleduster across the four knuckles of each hand", cover="part"),
    "nail_lacquer": P("each fingernail lacquered dark", ink=True),
    "off_hand": S("an off-hand blade: a parrying dagger with a straight double-edged blade and a long crossguard"),
    "palm_crystal": P("a small faceted crystal set into the middle of each palm", cover="part"),
    "palm_sigil": P("a sigil tattooed in the middle of each palm: a circle with a rune inside", ink=True),
    "ring_tattoo": P("a thin band tattooed round the base of each finger, like a ring of ink", ink=True),
    "shield": S("a heater shield: a flat-topped shield with a pointed base, wooden planks faced with leather, a plain "
                "steel rim, no emblem"),
    "talons": P("sharp curved metal claws capping each fingertip", cover="part"),
    "thumb_tattoo": P("a small tattoo on each thumb: a row of three dots", ink=True),
    "torch": S("an everburning torch: a carved wooden handle bound with brass bands, a wrapped head with a steady flame"),
    "claw_gauntlet": P("a steel gauntlet on each hand, its fingers ending in long curved claws"),
    "claw_ring": P("a claw ring on each finger: a ring at the middle joint carrying a pointed metal claw over the "
                   "fingertip", cover="part"),
    "gauntlet": P("a steel gauntlet on each hand: articulated plates over the back of the hand and down each finger, "
                  "a flared cuff over the wrist"),
    "great_weapon": S("a great weapon: a two-handed greatsword with a long straight blade, a wide crossguard and a long "
                      "leather-wrapped grip"),
    "hand_chain": P("a hand chain on each hand: a bracelet linked by fine chains across the back of the hand to a ring "
                    "on the middle finger", cover="part"),
    "hand_jewel": P("a jewel in a small metal mount on the back of each hand, held by thin straps", cover="part"),
    "hand_plate": P("a steel plate over the back of each hand, strapped round the palm"),
    "knuckle_letters": P("an angular rune tattooed on each finger between the knuckles (runes only, no letters)",
                         ink=True),
    "knuckle_ring": P("a small ring at the middle knuckle of each finger", cover="part"),
    "main_hand": S("a main-hand weapon: an arming sword with a straight double-edged blade, a simple crossguard, a "
                   "leather-wrapped grip and a round pommel"),
    "ring": P("a gold ring on each finger at the base segment, each with an empty round setting on top (its stone is "
              "added by the game)", cover="part"),
    "thimble": P("a metal thimble on each middle fingertip", cover="part"),
    # ---------------------------------------------------------------------------------------------------- legs
    "ankle_tattoo": P("a tattoo round each ankle: a band of thorny vines", ink=True),
    "anklet": P("two anklets on each ankle: a fine gold chain and, just above it, a string of small carved beads",
                cover="part"),
    "calf_band": P("a metal band round each calf", cover="part"),
    "calf_guard": P("a curved steel plate on the back of each calf"),
    "greave": P("a steel greave over each shin from below the knee to the ankle"),
    "knee_pit_tattoo": P("a tattoo in the hollow behind each knee: a small diamond of knotwork", ink=True),
    "leg_harness": P("plate armour for the whole leg: a cuisse on the thigh, a poleyn with a round side wing at the knee "
                     "and a greave on the shin, joined by straps"),
    "leg_sleeve_tattoo": P("a full leg-sleeve tattoo from the thigh to the ankle: flowing knotwork with waves",
                           ink=True),
    "leg_wraps": P("linen strips wrapped round each shin from the ankle to below the knee", dye="linen"),
    "mail_leggings": P("short mail breeches covering both thighs from the hips to the knees", cover="full"),
    "poleyn": P("a steel poleyn over each knee, with a round fan-shaped wing on the outside"),
    "shin_tattoo": P("a tattoo down each shin: a long dagger pointing down", ink=True),
    "trousers": P("plain woollen trousers covering both legs from the waist to just above the ankles", dye="walnut",
                  pages=("Waist",),
                  reach={"front": [("sites", ["groin", "L.hip", "R.hip"], 0.3)], "back": [("sites", ["seat"], 0.3)]}),
    "ankle_guard": P("a ring of steel guarding each ankle"),
    "ankle_wrap": P("a cloth wrap bound round each ankle", dye="linen"),
    "breeches": P("padded linen breeches covering each thigh to the knee", dye="dun"),
    "calf_tattoo": P("a tattoo on each calf: a coiled serpent", ink=True),
    "chausses": P("mail chausses covering each thigh, laced at the back"),
    "cuisse": P("a steel cuisse over the front and sides of each thigh"),
    "garter": P("a band of small metal links round each thigh just above the knee", cover="part"),
    "hamstring_tattoo": P("a tattoo on the back of each thigh: a long feather", ink=True),
    "knee_tattoo": P("a tattoo on each kneecap: a compass rose", ink=True),
    "knee_wrap": P("a cloth wrap bound round each knee", dye="linen"),
    "outer_thigh_tattoo": P("a tattoo on the outside of each thigh: a column of runes", ink=True),
    "thigh_sheath": P("a dagger in a leather sheath strapped to the outside of each thigh", cover="part"),
    "thigh_strap": P("a leather strap with a small buckle round each thigh, seen at the back", cover="part"),
    "thigh_tattoo": P("a tattoo on the front of each thigh: a stag's head in knotwork", ink=True),
    # ---------------------------------------------------------------------------------------------------- neck
    "amulet": P("an amulet on a leather cord: a round metal pendant with an empty setting in the middle, resting on the "
                "breastbone", reach={"front": [("strap", "collar", "sternum", 1.6)]}, cover="part", pages=("Torso",)),
    "chain_of_office": P("a heavy chain of office: broad gold links lying over the collarbones and the top of the "
                         "chest, with a round medallion hanging in the middle",
                         reach={"front": [("box", ["L.clavicle", "R.clavicle", "sternum"], 0.3, 0, 0.3, -2)]},
                         cover="part", pages=("Torso",)),
    "choker": P("a close-fitting dark band round the neck with a small metal pendant", cover="part"),
    "gorget": P("a steel gorget: overlapping plates round the neck and over the base of the neck",
                reach={"front": [("sites", ["collar"], 0.6)]}),
    "neck_tattoo": P("a tattoo on each side of the neck: a sprig of thorns", ink=True),
    "necklace": P("two necklaces: a fine gold chain close round the neck with a small pendant, and a longer string of "
                  "beads lying lower on the chest", reach={"front": [("strap", "collar", "sternum", 2.2)]}, cover="part",
                  pages=("Torso",)),
    "scarf": P("a wool scarf wrapped once round the neck, its two ends hanging down the chest", dye="ochre",
               reach={"front": [("below", ["neck"], 9, 0.5)]}, pages=("Torso",)),
    "throat_jewel": P("a small jewel on a fine chain at the hollow of the throat", cover="part"),
    "throat_tattoo": P("a tattoo on the throat: a small crescent", ink=True),
    "torc": P("a thick twisted metal torc round the neck, open at the front, its two ends shaped as animal heads",
              cover="part"),
    # ---------------------------------------------------------------------------------------------------- torso
    "badge": P("a badge of honour pinned on the right breast: a small round metal badge with a raised edge",
               cover="part"),
    "baldric": P("a broad brown leather baldric over the right shoulder, across the chest to the left hip",
                 reach={"front": [("strap", "R.shoulder", "L.hip", 2.2)], "back": [("strap", "R.shoulder", "L.kidney", 2.2)]},
                 cover="part"),
    "bandolier": P("a leather bandolier across the chest from the left shoulder to the right hip, with small loops and "
                   "pockets along it", reach={"front": [("strap", "L.shoulder", "R.hip", 2.6)],
                                              "back": [("strap", "L.shoulder", "R.kidney", 2.6)]}, cover="part"),
    "belly_chain": P("a fine chain hanging across the lower belly in a loop", cover="part"),
    "belly_tattoo": P("a tattoo on the stomach: a sun with wavy rays", ink=True),
    "breast_tattoo": P("a tattoo on each breast: a swallow in flight", ink=True),
    "breastbone_tattoo": P("a tattoo down the breastbone: a sword pointing down", ink=True),
    "breastplate": P("a steel breastplate covering his chest and ribs, with a central ridge, a rolled top edge and "
                     "leather straps at the sides"),
    "brigandine": P("a brigandine: a sleeveless close-fitting jacket of cloth over small steel plates, covering the "
                    "chest, the flanks and the back, the rivet heads showing in neat rows", dye="madder"),
    "brooch": P("a round brooch pinned at the top of the breastbone", cover="part"),
    "chest_harness": P("a leather chest harness: straps over both shoulders crossing in an X on the chest and on the "
                       "back, with a metal ring where they cross",
                       reach={"front": [("strap", "L.shoulder", "R.flank", 1.3), ("strap", "R.shoulder", "L.flank", 1.3)],
                              "back": [("strap", "L.shoulder", "R.kidney", 1.3), ("strap", "R.shoulder", "L.kidney", 1.3)]},
                       cover="part"),
    "chest_tattoo": P("a large tattoo across the chest: a spread eagle in knotwork", ink=True),
    "collarbone_chain": P("a fine chain draped along both collarbones", cover="part"),
    "collarbone_stud": P("a small stud piercing at each collarbone", cover="part"),
    "collarbone_tattoo": P("a tattoo along each collarbone: a line of small runes", ink=True),
    "cuirass": P("a full steel cuirass: a breastplate and a backplate joined at the sides by straps, covering the "
                 "chest, the stomach, the ribs and the back"),
    "flank_tattoo": P("a tattoo on each flank: a long wave", ink=True),
    "full_plate_harness": P("a full harness of plate: cuirass, pauldrons, plates on the upper arms and tassets over the "
                            "hips, all articulated, covering the torso, the shoulders, the upper arms and the hips",
                            reach={"*": [("sites", ["L.shoulder", "R.shoulder"], 1.1)]}),
    "gambeson": P("a thick quilted gambeson (a padded jacket) with short sleeves to above the elbows, covering the "
                  "torso down to the hips, quilted in vertical lines", dye="dun",
                  reach={"front": [("below", ["waist"], 3.5, 0.6)], "back": [("below", ["waist"], 3.5, 0.6)]}),
    "gills": P("gills: three parallel slits with fleshy grey edges along each side of the ribs", cover="part"),
    "hauberk": P("a mail hauberk: a shirt of riveted mail with sleeves to above the elbows, reaching down to the knees, "
                 "split at the front and the back",
                 reach={"front": [("below", ["waist"], 22, 1.2)], "back": [("below", ["waist"], 22, 1.2)]}),
    "heart_tattoo": P("a small tattoo over the heart: a heart drawn in knotwork", ink=True),
    "heartstone": P("a heartstone: a round polished grey stone embedded in the chest over the heart, rimmed with metal",
                    cover="part"),
    "lower_belly_tattoo": P("a tattoo across the lower belly: a band of interlaced knotwork", ink=True),
    "mail_shirt": P("a sleeveless mail shirt covering the torso down to the hips",
                    reach={"front": [("below", ["waist"], 3, 0.3)], "back": [("below", ["waist"], 3, 0.3)]}),
    "medal": P("three medals pinned in a row on the left breast, each a small round metal disc hanging from a short "
               "ribbon", dye="madder", cover="part"),
    "navel_ring": P("a small ring in the navel", cover="part"),
    "navel_tattoo": P("a small tattoo round the navel: a sun", ink=True),
    "pectoral_plate": P("a round steel plate over each pectoral, strapped round the chest"),
    "placard": P("a placard: a narrow reinforcing steel plate down the breastbone, riveted"),
    "plackart": P("a plackart: a steel plate covering the stomach, its top edge rising to a point"),
    "plexus_disc": P("a round steel disc over the solar plexus, on a strap round the chest"),
    "plexus_gem": P("a gem in a small metal setting fixed over the solar plexus", cover="part"),
    "plexus_tattoo": P("a tattoo on the solar plexus: a rayed sun", ink=True),
    "rib_chain": P("a fine chain draped along the ribs on each side", cover="part"),
    "rib_guard": P("a curved steel plate over the ribs on each side"),
    "rib_tattoo": P("a tattoo along the ribs on each side: flowing waves", ink=True),
    "robe": P("a long sleeveless robe from the shoulders to the ankles, belted with a cord at the waist", dye="slate",
              pages=("Right leg", "Left leg"),
              reach={"front": [("box", ["groin", "L.thigh", "R.thigh", "L.shin", "R.shin"], 0.8, 0, 0.8, 0.5)],
                     "back": [("box", ["seat", "L.hamstring", "R.hamstring", "L.calf", "R.calf"], 0.8, 0, 0.8, 0.5)]}),
    "scale_vest": P("a vest of overlapping steel scales covering the torso, like a fish's",
                    reach={"front": [("below", ["waist"], 2, 0.2)], "back": [("below", ["waist"], 2, 0.2)]}),
    "shirt": P("a plain sleeveless linen undershirt tucked into the braies", dye="linen"),
    "side_plate": P("a steel plate on each flank"),
    "side_sheath": P("a knife in a leather sheath strapped along each flank under the arm", cover="part"),
    "soul_gem": P("a soul gem: a faceted clear crystal embedded in the breastbone, rimmed with metal", cover="part"),
    "surcoat": P("a long sleeveless surcoat over the chest, falling to the knees, split at the front, belted at the "
                 "waist", dye="woad", pages=("Right leg", "Left leg"),
                 reach={"front": [("box", ["groin", "L.thigh", "R.thigh", "L.knee", "R.knee"], 0.6, 0, 0.6, 0)],
                        "back": [("box", ["seat", "L.hamstring", "R.hamstring", "L.kneepit", "R.kneepit"], 0.6, 0, 0.6, 0)]}),
    "tabard": P("a tabard: front and back panels joined at the shoulders, open at the sides, falling to mid-thigh",
                dye="madder", reach={"front": [("below", ["waist"], 13, -1.5)], "back": [("below", ["waist"], 13, -1.5)]}),
    "torso_tattoo": P("a tattoo covering the whole torso, front and back: bands of interlocking knotwork", ink=True),
    # ---------------------------------------------------------------------------------------------------- waist
    "belt": P("a brown leather belt with a plain brass buckle round the waist", cover="part"),
    "codpiece": P("a steel codpiece over the groin, strapped to the waist"),
    "culet": P("a culet: overlapping steel lames over the seat"),
    "fauld": P("a fauld: three overlapping steel hoops round the waist and the top of the hips",
               reach={"*": [("below", ["waist"], 1.5, 0.3)]}),
    "faulds_and_tassets": P("a fauld of steel hoops round the waist with a tasset plate hanging over each hip and the top "
                            "of the thigh", reach={"front": [("below", ["L.hip", "R.hip"], 4, 0.4)]}),
    "girdle": P("a girdle: a broad belt of linked metal plates round the waist", cover="part"),
    "hip_chain": P("a decorative chain draped over each hip", cover="part"),
    "hip_tattoo": P("a tattoo on each hip bone: a crescent moon", ink=True),
    "loincloth": P("a long cloth panel hanging from the waist over the groin to the knees, at the front and at the "
                   "back", dye="madder",
                   reach={"front": [("below", ["groin"], 9, 0.3)], "back": [("below", ["seat"], 9, -0.5)]}),
    "pouch": P("a leather pouch on a belt at the back, over the seat", reach={"back": [("sites", ["waist", "seat"], 0.2)]},
               cover="part"),
    "sash": P("a cloth sash wound round the waist, its end hanging at the left hip", dye="madder",
              reach={"front": [("below", ["L.hip"], 5, 0.3)]}, cover="part"),
    "scabbard": P("a sword in a leather scabbard hanging from a belt at each hip, the hilt at the hip and the scabbard "
                  "running down along the outside of the thigh", pages=("Right leg", "Left leg"),
                  reach={"front": [("below", ["L.hip", "R.hip"], 22, 0.6)], "back": [("box", ["waist"], 1.5, 0, 1.5, 20)]},
                  cover="part"),
    "tasset": P("a steel tasset hanging over each hip and the top of the thigh",
                reach={"front": [("below", ["L.hip", "R.hip"], 4, 0.4)]}),
    "waist_tattoo": P("a tattoo round the waist: a belt of knotwork", ink=True),
}
# The default reach beyond a piece's sites, by layer (body units), and how much of its sites it should hide.
GROW = {"Skin": 0.12, "Under": 0.35, "Mail": 0.45, "Armor": 0.9, "Over": 1.0, "Adorn": 0.45, "Hold": 0.7, "Graft": 0.8}
FULL_LAYERS = ("Under", "Mail", "Armor", "Over")
# Too small to paint on the whole figure (a few pixels there): the game marks them with a glint at their slot until
# their close-up pictures are scaled onto the figure. They're painted on every close-up and plate where they show.
SMALL = {"beard_rings", "braid_beads", "bridge_stud", "cheek_gem", "ear_chain", "ear_cuff", "earring", "eyebrow_ring",
         "helix_ring", "lip_ring", "nose_chain", "nose_stud", "septum_ring", "spine_piercings", "collarbone_stud",
         "navel_ring", "toe_ring", "ring", "knuckle_ring", "claw_ring", "thimble", "nail_lacquer", "toenail_paint",
         "talons", "toe_talons", "fang_caps", "gold_teeth", "forehead_jewel", "throat_jewel", "hair_pin", "palm_crystal",
         "foot_charm", "foot_chain", "thumb_tattoo", "ring_tattoo", "knuckle_letters", "kohl", "brow_tattoo", "chin_tattoo", "nape_tattoo", "heel_tattoo", "palm_sigil", "monocle"}

# ---------------------------------------------------------------------------------------------------------------- pieces set in pieces

def A(look, **kw):
    """A piece set in another, drawn on its own canvas and placed on its host by the game."""
    return dict(cls="attachment", look=look, **kw)


def H(look, **kw):
    """A piece set in another whose shape follows its host: painted on the host's own picture."""
    return dict(cls="host", look=look, **kw)


def SH(look, cells, **kw):
    """Several small things on one sheet, one to a cell, white so the game can tint them, or in their materials."""
    return dict(cls="sheet", look=look, cells=cells, **kw)


BOARD_FAMILIES = {
    "crest": A("a crest for the top of a helm or a pauldron: a tall fan-shaped fin of thin steel with a scalloped top "
               "edge and a raised rib along it", views=("side", "front")),
    "dragon_crest": A("a dragon crest for the top of a helm: a small steel dragon crouching with its wings half spread "
                      "and its tail curled", views=("side", "front")),
    "plume": A("a tall plume of long soft white and pale grey feathers rising from a small steel holder and arching back",
               views=("side", "front")),
    "feather": A("a single long feather, white with pale grey barring, its quill at the bottom"),
    "spike": A("a conical steel spike on a small round riveted base plate, pointing straight up"),
    "spur": A("a rowel spur seen from the side: a U-shaped steel heel band with a short neck ending in a spiked wheel "
              "behind the heel"),
    "buckle": A("a square brass buckle with its prong, seen from the front"),
    "clasp": A("a round brass clasp with a knotwork face, seen from the front"),
    "star_charm": A("a small five-pointed gold star charm hanging from a little ring at its top point"),
    "hair_pin": A("a long hat pin with a round metal head, slanting from the top right to the bottom left",
                  name="hat_pin"),
    "horn_ring": A("a broad ridged metal ring band seen from the side, as it would sit round a horn"),
    "horn_tip": A("a pointed metal cap for the tip of a horn, slightly curved, seen from the side"),
    "tail_ring": A("a broad metal ring band seen from the side, as it would sit round a tail"),
    "tail_spike": A("three short steel spikes on a band, for the tip of a tail, seen from the side"),
    "tail_tuft": A("a tuft of coarse dark grey hair, like a lion's tail tip, hanging down"),
    "wing_blade": A("a long curved steel blade like a sickle's, its sharp edge outward, to be fixed along a wing's edge"),
    "tassel": A("a cord tassel hanging from a knot and a loop at its top", dye="madder"),
    "pouch": A("a small brown leather belt pouch with a flap and a brass stud, seen from the front, its belt loop at the "
               "top"),
    "potion": A("a small corked glass vial, upright, clear glass with a pale grey liquid (the game tints it)"),
    "throwing_knife": A("a small leaf-shaped throwing knife with a ring pommel, point down"),
    "bedroll": A("a rolled grey wool blanket tied with two leather straps, lying horizontal"),
    "lantern": A("a small hanging lantern of brass with clear glass panes, unlit, with a hook at the top"),
    "fletching": A("a bundle of six arrows' fletched ends rising together, grey-feathered, the shafts cut off at the "
                   "bottom"),
    "lance_rest": A("a steel lance rest: a hook-shaped bracket on a small riveted plate, seen from the front"),
    "bell": A("a small round brass bell with a loop at its top"),
    "torc_terminal": SH("the two ends of a torc: a pair of wolf-head terminals in twisted gold facing each other, the "
                        "left one in the left half and the right one in the right half", (2, 1), size=(1536, 1024)),
    "gem": SH("six cut gemstones, one in each cell, seen from the front, in clear colourless crystal (white with pale "
              "grey facets, the game tints them): a round brilliant, an oval, a square step cut, a pear, a marquise and "
              "a smooth cabochon", (3, 2), size=(1536, 1024)),
    "rune": SH("eight angular runes, one in each cell, as solid white strokes with no outline (the game tints them and "
               "makes them glow)", (4, 2), size=(1536, 768), white=True),
    "heraldry": SH("four heraldic charges, one in each cell, as solid white silhouettes with no outline (the game paints "
                   "them in a colour onto shields, tabards, banners and breastplates): a rampant lion, an eagle with "
                   "spread wings, a tower and two crossed swords", (2, 2), size=(1248, 1248), white=True),
    "charm": SH("four small charms, one in each cell, each hanging from a little ring at its top: a crescent moon of "
                "silver, an iron key, a wolf's tooth bound with gold wire and an old gold coin", (2, 2), size=(1248, 1248)),
    "visor": H("a visor fitted to the sallet: a curved steel plate covering the upper face with a narrow eye slit, "
               "hinged at the temples", bases=("head_front", "head_right", "head_left", "figure_front"),
               reach=[("sites", ["L.eye", "R.eye", "brow", "nose", "L.cheek", "R.cheek"], 0.5)]),
    "cheek_fitting": H("steel cheek pieces hinged to the lower edge of the sallet over each cheek",
                       bases=("head_front", "head_right", "head_left", "figure_front"),
                       reach=[("sites", ["L.cheek", "R.cheek"], 0.6)]),
    "aventail": H("an aventail: a curtain of mail hanging from the helm's lower edge round the neck and over the tops "
                  "of the shoulders", bases=("head_front", "head_right", "head_left", "figure_front", "figure_back"),
                  reach=[("sites", ["neck", "nape", "throat", "collar", "chin"], 0.8)]),
    "toe_cap": H("a steel cap over the boot's toe, riveted", bases=("foot_R_top", "foot_L_top"),
                 reach=[("sites", ["R.bigtoe", "R.toe2", "R.toe3", "R.toe4", "R.littletoe", "L.bigtoe", "L.toe2",
                                   "L.toe3", "L.toe4", "L.littletoe"], 0.6)]),
    "laces": H("pale leather laces criss-crossing up the front of the boot", bases=("foot_R_top", "foot_L_top"),
               reach=[("sites", ["R.foot", "L.foot", "R.ankle", "L.ankle"], 0.2)]),
    "hatband": H("a broad cloth band round the hat just above the brim", dye="ochre",
                 bases=("head_front_tall", "head_right_tall", "head_left_tall", "figure_front"),
                 reach=[("above", ["scalp"], 2.5, 0.8)]),
    "pommel": H("a heavier, ornate pommel in place of the plain one: a faceted steel wheel with an empty round setting",
                sprite=True),
    "crossguard": H("a broader, ornate crossguard in place of the plain one: its arms curving toward the blade and "
                    "ending in scrolls", sprite=True),
    "grip_wrap": H("the grip wrapped in criss-crossed dark leather strips", sprite=True),
    "shield_boss": H("a round domed steel boss in the middle of the shield, riveted", sprite=True),
    "shield_rim": H("a heavier steel rim all round the shield's edge, riveted", sprite=True),
    "collar": H("a brown leather collar round the cat's neck with a small ring at the front", sprite=True),
    "barding": H("barding: light armour fitted to the animal, steel plates over its head, neck and back on a quilted "
                 "cloth caparison", dye="madder", sprite=True),
    "saddle": H("a brown leather war saddle with a high back and a girth strap", sprite=True),
    "saddlebags": H("a pair of brown leather saddlebags hanging behind the saddle", sprite=True),
    "reins": H("a bridle and reins of brown leather with brass fittings", sprite=True),
    "jesses": H("short leather jesses on the falcon's legs", sprite=True),
    "falcon_hood": H("a falconer's leather hood over the falcon's head, with a small tuft on top", dye="madder",
                     sprite=True),
}
BOARD_CODE = {
    "lining": "inside the piece: not seen", "paint": "the game tints the host's metal",
    "enchantment": "the game makes the host glow in its rarity", "engraving": "the game draws fine lines on the host",
    "embroidery": "the game stitches a border along the host's edge", "lens_tint": "the game tints the lenses",
    "blade_oil": "the game adds a sheen to the blade", "halo_light": "the game's light in the halo",
    "aura_mote": "the game's motes of light round him", "keepsake": "inside a pouch: not seen",
    "arrowheads": "inside the quiver: not seen", "horseshoe": "under the hooves: the game adds a glint",
    "shield_straps": "behind the shield: not seen from the front", "chain": "the game draws a fine chain line",
}

# Held pieces, companions and what's round him: drawn on their own canvases. The held ones stand beside him on the
# whole figure (his open hands can't hold them: the main hand's on his right, the off hand's on his left) and beside
# the hand on its close-up; the companions stand at his side; the rest is in the sky round him.
UPRIGHT = ("standing upright, point down, seen from the front with the flat of the blade toward the viewer, centred, "
           "filling 92% of the canvas's height, nothing cut off")
SPRITES = {
    "main_hand": dict(size=(768, 2048), pose=UPRIGHT, held=True),
    "great_weapon": dict(size=(768, 2048), pose=UPRIGHT, held=True),
    "off_hand": dict(size=(768, 2048), pose=UPRIGHT.replace("92%", "70%"), held=True),
    "shield": dict(size=(1024, 1536), pose="upright, its face toward the viewer, centred, filling 90% of the canvas's "
                                           "height", held=True),
    "focus_orb": dict(size=(1248, 1248), pose="floating upright, seen from the front, centred, filling 70% of the canvas",
                      held=True),
    "torch": dict(size=(768, 2048), pose="upright, the flame at the top, centred, filling 80% of the canvas's height; the "
                                        "flame in flat pale yellow and cream tones inside an ink outline (the game adds "
                                        "its glow)", held=True),
    "familiar": dict(size=(1248, 1248), pose="centred, its paws on the bottom quarter line, filling 70% of the canvas"),
    "steed": dict(size=(1536, 1024), pose="the whole horse in profile, its hooves on a line 6% above the bottom edge, "
                                          "filling 90% of the canvas's width, nothing cut off"),
    "shoulder_familiar": dict(size=(1024, 1536), pose="a falcon perched, seen from the front, wings folded, head turned "
                                                      "to the viewer's left, its feet together at the bottom middle of "
                                                      "the canvas as if gripping a shoulder (draw no perch), filling 80% "
                                                      "of the canvas's height", view="front"),
    "shoulder_familiar_back": dict(family="shoulder_familiar", size=(1024, 1536), what="The same falcon seen from behind",
                                   pose="the same falcon perched, seen from behind: its folded wings and tail toward "
                                        "the viewer, its head turned to the viewer's right, its feet together at the "
                                        "bottom middle, filling 80% of the canvas's height", view="back"),
    "comet": dict(size=(1536, 1024), pose="the comet's head in the upper right third, its tail streaming to the lower "
                                          "left, filling 85% of the canvas's width"),
    "eclipse": dict(size=(1248, 1248), pose="centred, the disc filling 55% of the canvas, its corona round it"),
    "ioun_stone": dict(size=(1536, 1024), cells=(3, 2),
                       pose="six small orbiting stones, one centred in each cell of an invisible 3 by 2 grid, each "
                            "filling about half its cell: a rhomboid, an ellipsoid, a prism, a sphere, a spindle and a "
                            "pyramid, each cut in clear colourless crystal (white with pale grey facets: the game tints "
                            "them)"),
}

# Where a held piece's own slots, or a companion's, go on its picture (shares of the picture's box), for the masks of
# the pieces painted on it.
SPRITE_PARTS = {
    "pommel": {"main_hand": (0.2, 0.0, 0.8, 0.07), "great_weapon": (0.2, 0.0, 0.8, 0.06), "off_hand": (0.2, 0.0, 0.8, 0.09)},
    "crossguard": {"main_hand": (0.0, 0.14, 1.0, 0.24), "great_weapon": (0.0, 0.16, 1.0, 0.26), "off_hand": (0.0, 0.16, 1.0, 0.3)},
    "grip_wrap": {"main_hand": (0.3, 0.05, 0.7, 0.17), "great_weapon": (0.3, 0.05, 0.7, 0.2)},
    "shield_boss": {"shield": (0.3, 0.25, 0.7, 0.6)},
    "shield_rim": {"shield": (0.0, 0.0, 1.0, 1.0)},
    "collar": {"familiar": (0.25, 0.2, 0.75, 0.45)},
    "barding": {"familiar": (0.1, 0.1, 0.9, 0.85), "steed": (0.05, 0.0, 0.95, 0.7)},
    "saddle": {"steed": (0.35, 0.1, 0.7, 0.55)},
    "saddlebags": {"steed": (0.5, 0.25, 0.85, 0.65)},
    "reins": {"steed": (0.0, 0.0, 0.6, 0.5)},
    "jesses": {"shoulder_familiar": (0.25, 0.7, 0.75, 1.0), "shoulder_familiar_back": (0.25, 0.7, 0.75, 1.0)},
    "falcon_hood": {"shoulder_familiar": (0.2, 0.0, 0.8, 0.35), "shoulder_familiar_back": (0.2, 0.0, 0.8, 0.35)},
}

# ---------------------------------------------------------------------------------------------------------------- the list

def body_pieces(fam):
    return [p for p in PIECES if p["board"] == "body" and p["family"] == fam]


def piece_leaves(p):
    out = []
    for ck in p["takes"]:
        c = CELLS[ck]
        out += leaves(c["site"], c["layer"])
    return out


def layers_of(ps):
    return sorted({CELLS[ck]["layer"] for p in ps for ck in p["takes"]}, key=LAYERS.index)


def priority(ps):
    """1: opens in the first hours (by depth 300); 2: by depth 3,000; 3: later, or with the prestige perks."""
    d = min(p["depth"] for p in ps)
    if all(p["prestige"] for p in ps):
        return 3
    return 1 if d <= 300 else 2 if d <= 3000 else 3


def plural(words, n):
    if n <= 1:
        return words
    for end, rep in (("ss", "sses"), ("sh", "shes"), ("ch", "ches"), ("x", "xes")):
        if words.endswith(end):
            return words[: -len(end)] + rep
    if words.endswith("s"):
        return words
    if words.endswith("y") and words[-2] not in "aeiou":
        return words[:-1] + "ies"
    return words + "s"


NOUNS = {"kohl": "the kohl lines", "war_paint": "the war paint", "mail_leggings": "the mail breeches",
         "faulds_and_tassets": "the fauld and tassets", "nail_lacquer": "the lacquer on the nails",
         "toenail_paint": "the lacquer on the toenails", "gold_teeth": "the gold teeth and the parted lips",
         "fang_caps": "the fang caps and the parted lips", "wings": "the wings", "horns": "the horns",
         "tail": "the tail", "trousers": "the trousers", "breeches": "the breeches", "chausses": "the chausses",
         "anklet": "the anklets", "bracelet": "the bracelets", "necklace": "the necklaces", "medal": "the medals",
         "hair_pin": "the hair pins", "arm_wraps": "the arm wraps", "leg_wraps": "the leg wraps",
         "bangle_stack": "the bangles", "spectacles": "the spectacles", "goggles": "the goggles",
         "gills": "the gills", "dorsal_spines": "the spines", "beard_rings": "the beard rings",
         "braid_beads": "the beads", "spine_piercings": "the piercings", "full_plate_harness": "the plate harness"}


def noun(fam, n):
    if fam in NOUNS:
        return NOUNS[fam]
    return "the " + plural(fam.replace("_", " "), n)


class Family:
    """One body family on the bases: which pieces show on each, the area to paint, the core it should cover."""

    def __init__(self, fam, spec):
        self.fam, self.spec = fam, spec
        self.pieces = body_pieces(fam)
        self.layers = layers_of(self.pieces)
        self.grow = max(GROW.get(l, 0.5) for l in self.layers)
        self.leaves = {p["name"]: piece_leaves(p) for p in self.pieces}
        regs = {region_of(l) for ls in self.leaves.values() for l in ls}
        regs |= {p["region"] for p in self.pieces}
        self.regions = {r for r in regs if r} | set(spec.get("pages", ()))
        self.ink = spec.get("ink", False)
        self.cover = "ink" if self.ink else spec.get("cover") or ("full" if set(self.layers) <= set(FULL_LAYERS) else "part")
        self.priority = priority(self.pieces)
        self._area = {}

    def prims(self, base):
        reach = self.spec.get("reach", {})
        return reach.get("*", []) + reach.get(base.group, [])

    def area(self, base):
        """(paint mask, leaf mask, pieces that show) on a base."""
        if base.name in self._area:
            return self._area[base.name]
        leaf = np.zeros(base.size[::-1], bool)
        shows = []
        for p in self.pieces:
            ls = [l for l in self.leaves[p["name"]] if base.has(l)]
            m = base.mask(ls) if ls else None
            if m is not None and m.sum() >= 50:
                shows.append(p["name"])
                leaf |= m
        paint = coarse(leaf, self.grow * base.U, grow) if leaf.any() else leaf.copy()
        reach = np.zeros_like(paint)
        front = np.zeros_like(paint)
        for prim in self.prims(base):
            m = extent(base, prim)
            if m is not None:
                # Straps and what lies on his sites are in front of him; the rest of a piece that hangs behind him
                # shows only round his body.
                (front if prim[0] in ("strap", "sites") else reach)[...] |= m
        if self.spec.get("behind") and base.group == "front":
            reach &= ~shrink_mask(base.body(), 0.3 * base.U)
        paint |= reach | front
        if paint.any() and not shows:
            shows = [p["name"] for p in self.pieces]
        self._area[base.name] = (paint, leaf, shows)
        return self._area[base.name]

    def units(self, base):
        return self.area(base)[0].sum() / base.U ** 2

    def focus(self, base):
        """How much of what it fills on this picture is in the picture's own region (the rest is dimmed there, and
        drawn by the game's fills): a shirt's armpits don't put it on the arm's pages, nor a vambrace's wrist on the
        hand's. Pieces with no sites of their own (horns, a tail) count as all in focus where they reach."""
        paint, leaf, shows = self.area(base)
        if base.region in self.spec.get("pages", ()):
            return 1.0 if paint.any() else 0.0
        if not leaf.any():
            return 1.0 if paint.any() and base.region in self.regions else 0.0
        own = [l for ls in self.leaves.values() for l in ls if base.has(l) and region_of(l) == base.region]
        return (base.mask(own) & leaf).sum() / leaf.sum()


_FAMS = {}


def family(fam):
    if fam not in _FAMS:
        _FAMS[fam] = Family(fam, FAMILIES[fam])
    return _FAMS[fam]


def fam_bases(F):
    """The bases a family is painted on, master first."""
    tall = F.spec.get("tall", False)
    chosen = []
    plates_by_side = {"front": [], "back": []}
    for b in PLATES:
        if b.region in F.regions and F.units(b) >= 1.0 and F.focus(b) >= 0.12:
            chosen.append(b)
            plates_by_side[b.group].append(b)
    close = [BASES[vn + "_tall"] if tall and vn.startswith("head") else BASES[vn] for vn in CLOSE_TITLES]
    groups = {b.region for b in close if b.region in F.regions and F.units(b) >= 0.5 and F.focus(b) > 0}
    # Every view of a close-up page it shows on, so the page's views (the head from three sides, a hand's back and
    # palm) all show the same picture of it.
    chosen += [b for b in close if b.region in groups and F.units(b) >= 0.5]
    figures = []
    if F.fam not in SMALL:
        for side in ("front", "back"):
            b = BASES[f"figure_{side}"]
            if F.units(b) < 2.0:
                continue
            paint = F.area(b)[0]
            # Painted on the whole figure unless its plates hold all of it there (then the figure's is theirs, scaled
            # down): a head, hand or foot piece, one reaching past its plates, or one spanning regions.
            inside = np.zeros_like(paint)
            for p in plates_by_side[side]:
                x0, y0, x1, y1 = p.rect
                fx0, fy0 = (x0 - b.rect[0]) * b.k, (y0 - b.rect[1]) * b.k
                fx1, fy1 = (x1 - b.rect[0]) * b.k, (y1 - b.rect[1]) * b.k
                inside |= rect_mask(b.size, fx0, fy0, fx1, fy1)
            held = (paint & inside).sum() / max(1, paint.sum())
            if held < 0.97 or F.spans():
                figures.append(b)
    chosen = figures + chosen
    if not chosen:
        return []
    # The first one painted sets its design: the whole figure for a piece spanning regions, otherwise the picture
    # showing it biggest, preferring the front, his right and the head from the front.
    pool = figures if figures and F.spans() else chosen
    master = max(pool, key=lambda b: F.area(b)[0].sum() * PREFER.get(b.view, 1.0) * PREFER.get(b.group, 1.0)
                 * (0.9 if b.region and b.region.startswith("Left") else 1.0))
    chosen.remove(master)
    return [master] + chosen


PREFER = {"back": 0.75, "head_right": 0.7, "head_left": 0.65, "hand_R_palm": 0.75, "hand_L_palm": 0.75,
          "foot_R_sole": 0.6, "foot_L_sole": 0.6}


def _spans(self):
    """Whether one of its pieces reaches over two or more of the plates' regions (a hauberk, a cloak, a robe): then
    its whole-figure picture is its design, and its plates are drawn from it."""
    for p in self.pieces:
        regs = {region_of(l) for l in self.leaves[p["name"]]} & set(PLATE_REGIONS)
        if len(regs) >= 2:
            return True
    return bool(self.spec.get("behind"))


Family.spans = _spans


def asset_id(path):
    return os.path.relpath(path, DROP)[:-4].replace(os.sep, "/")


def sides_note(F, shows, base):
    sided = {BY_NAME[n]["side"] for n in shows} - {"None"}
    if not sided:
        return ""
    if sided == {"Left", "Right"}:
        return "Paint both, on his left and his right, as a matching pair. "
    side = sided.pop().lower()
    if base.kind == "closeup":
        return ""
    where = {"front": {"right": "the viewer's left", "left": "the viewer's right"},
             "back": {"right": "the viewer's right", "left": "the viewer's left"}}[base.group][side]
    return f"Only his {side} one is in this picture (on {where}): paint that one. "


def material_note(spec):
    out = []
    if spec.get("ink"):
        out.append(INK_STYLE)
    if spec.get("dye"):
        out.append(f"The cloth is {dye_words(spec['dye'])}.")
    return " ".join(out)


def line_px(base):
    """The ink outline's weight on a base: about 6 px on the imported figure, 5 on a close-up's view, scaled."""
    return max(3, round((5 if base.kind == "closeup" else 6) * base.k))


def overlay_prompts(F, base, shows, seeded, ref):
    look = F.spec["look"]
    n = noun(F.fam, len(shows))
    behind = ""
    if F.spec.get("behind") and base.group == "front":
        behind = ("Seen from the front it is mostly behind him: paint only the parts that show past his body and over "
                  "his shoulders; his body stays in front of the rest. ")
    keep = ("Keep everything else exactly as it is: his pose, body, face, hair, braies, the framing and the flat green "
            "background. ")
    if seeded:
        lead = (f"This is an edit of the attached picture, {base.title}, in which he already wears {n}, soft and "
                f"blurred because it was scaled up: redraw {n} crisply at this size, with exactly the same shape, design "
                f"and colours, in exactly the same place ({look}). ")
    elif F.ink:
        lead = (f"This is an edit of the attached picture, {base.title}. Paint on his skin {look}, inside the masked "
                f"area only, following the shape of his body exactly as he is drawn. ")
    else:
        lead = (f"This is an edit of the attached picture, {base.title}. Paint him wearing {look}, inside the masked area "
                f"only, fitted to him exactly as he is drawn. ")
    refs = ("It is the same piece as in the second attached picture: match its shape, design, materials and colours "
            "exactly. " if ref else "")
    lines = ("" if F.ink else f"Its outline is the same dark ink ({INK}) and the same weight as his (about "
             f"{line_px(base)} px), with thinner lines inside it, and its light comes from the top left. ")
    paint = (f"{STYLE} {lead}{sides_note(F, shows, base)}{behind}{refs}{keep}{lines}{material_note(F.spec)} "
             f"{PALETTE}").replace("  ", " ")
    if F.ink:
        isolate = (f"This is an edit of the attached picture. Keep only the ink of {n}: replace everything else, his "
                   f"skin between and round the ink's lines included, and his hair, beard, braies and body, with "
                   f"perfectly flat pure green #00ff00, edge to edge. Keep every line and shape of the ink exactly where "
                   f"and as it is.")
    else:
        isolate = (f"This is an edit of the attached picture. Keep only {n}: replace everything else (his skin, hair, "
                   f"beard, braies and body, and anything else in the picture) with perfectly flat pure green #00ff00, "
                   f"edge to edge. Keep every pixel of {n} exactly where and as it is, with its ink outline. Where his "
                   f"body is in front of it, leave green: don't draw what his body hid.")
    return paint, isolate


PLATE_PROMPT = (
    "{style} This is an edit of the attached picture: {title}, cropped from the whole figure and scaled up, so it is "
    "soft. Redraw it crisply at this size, line for line: the same man, pose, proportions and framing; his outline and "
    "every line inside it stay exactly where they are, so the picture still lines up with the whole figure. The same "
    "colours: skin shadow #9c5f48, mid #d08b6a, light #f0b693; hair and beard dark brown; the braies undyed linen "
    "(shadow #8f8270, mid #b9ad9b, light #ddd3c4). One clean ink outline ({ink}) about {line} px wide round him and "
    "thinner inner lines about {inner} px wide for the muscles and folds, in three flat tones per material, light from "
    "the top left. Add nothing: no gear, jewellery, tattoos, scars, hair or muscles that aren't in the picture, and cut "
    "nothing off: where the picture's edge crosses his body, the body runs on to the edge. Background: perfectly flat "
    "pure green #00ff00 wherever there's no body.")

USES = {
    "plate": "The {region} page's picture: his bare {words}, sharp at the page's zoom. The page fades it in over the "
             "zoomed whole figure, as the head, hand and foot close-ups do now; his slots' markers sit on it, and every "
             "piece of the region is painted over it (its own pictures on this plate).",
    "figure": "Worn on the whole figure: the equipment screen's body view (front and back side by side) and the main "
              "screen's small figure. Drawn over the knight in layer order whenever the piece counts, lit in its rarity "
              "when pointed at, shown faint where it would go when it's chosen but not worn.",
    "plate_piece": "Worn on the {region} page ({base}), up close: drawn over the plate whenever the piece counts, lit "
                   "when pointed at, faint when previewed. Scaled down, it is also the piece on the whole figure where "
                   "the figure has no picture of its own.",
    "closeup": "Worn on the {region} close-up ({base}): drawn over the close-up whenever the piece counts, lit when "
               "pointed at, faint when previewed.",
    "held": "Held: shown standing beside him on the whole figure (the main hand's piece on his right, the off hand's "
            "on his left; his hands stay open) and beside the hand on its close-up, as the piece's own picture its slots "
            "sit on (pommel, guard, runes, gems), and faint as a preview.",
    "companion": "At his side on the Beyond page and the whole figure, with what's set in it painted on it.",
    "perched": "Perched on his right shoulder on the whole figure and the arm's pages ({view}), placed by the game at "
               "the shoulder's point.",
    "sky": "Round him on the Beyond page and the whole figure: placed and moved by the game (orbiting, drifting), with "
           "its glow in the rarity's colour.",
    "attachment": "Set in its host: placed by the game on the host's picture at the slot's place, scaled to it "
                  "({hosts}).",
    "sheet": "Cut into cells by the game and placed on the host pieces at their slots ({hosts}), tinted where white.",
    "host": "Painted on {host}'s picture ({base}): drawn over it whenever both are worn.",
}


def hosts_of(fam):
    """The families of the pieces a set piece's family can be set in."""
    out = []
    for p in PIECES:
        if p["family"] != fam or p["board"] == "body":
            continue
        for ck in p["takes"]:
            for h in PIECES:
                for b in h["brings"]:
                    if b["board"] == p["board"] and ck in b["opens"] and h["family"] not in out:
                        out.append(h["family"])
    return out


def board_pieces(fam):
    return [p for p in PIECES if p["family"] == fam and p["board"] != "body"]


def sprite_box(path):
    """The drawn part of a delivered sprite (x0, y0, x1, y1), or None."""
    if not os.path.exists(path):
        return None
    rgba = ea.load(path)[1]
    m = ea.subject(rgba)[0]
    return ea.bbox(m)


def build():
    """Every file to make, in the order to make them: plates first, then each family's files with its master first,
    by priority."""
    assets = []

    def add(a):
        a["id"] = asset_id(os.path.join(ROOT, a["file"]))
        assets.append(a)

    for b in PLATES:
        add({"class": "plate", "file": f"art/equipment/plates/{b.name}.png", "size": list(b.size), "priority": 1,
             "base": b.name, "view": b.view, "rect": list(b.rect), "seed": f"art/equipment/inputs/seeds/{b.name}.png",
             "what": f"His bare {PLATE_WORDS[b.region]}, {'from the front' if b.group == 'front' else 'from behind'}",
             "use": USES["plate"].format(region=b.region, words=PLATE_WORDS[b.region]),
             "prompt": PLATE_PROMPT.format(style=STYLE, title=b.title, ink=INK, line=round(6 * b.k), inner=round(2.5 * b.k)),
             "refs": []})
    families = {}
    for fam, spec in FAMILIES.items():
        if spec["cls"] != "overlay":
            continue
        F = family(fam)
        families[fam] = F
        bases = fam_bases(F)
        names = [b.name for b in bases]
        master = None
        for i, b in enumerate(bases):
            paint, leaf, shows = F.area(b)
            path = f"art/equipment/pieces/{b.name}/{fam}.png"
            seeded = b.kind == "plate" and f"figure_{b.group}" in names[:i]
            p1, p2 = overlay_prompts(F, b, shows, seeded, master is not None and not seeded)
            kind = "figure" if b.kind == "figure" else "plate_piece" if b.kind == "plate" else "closeup"
            add({"class": "piece", "family": fam, "file": path, "size": list(b.size), "priority": F.priority,
                 "base": b.name, "view": b.view, "rect": list(b.rect), "pieces": shows, "layers": F.layers,
                 "master": master is None, "refs": [master] if master else [],
                 "seeded_from": f"figure_{b.group}" if seeded else None, "cover": F.cover,
                 "behind": bool(F.spec.get("behind")) and b.group == "front",
                 "what": F.spec["look"][0].upper() + F.spec["look"][1:],
                 "use": USES[kind].format(region=b.region or "", base=b.name),
                 "mask": f"art/equipment/inputs/masks/pieces/{b.name}/{fam}.png",
                 "prompt": p1, "isolate": p2, "units": round(F.units(b), 1)})
            if master is None:
                master = path
    for name, sp in SPRITES.items():
        fam = sp.get("family", name)
        spec = FAMILIES[fam]
        ps = body_pieces(fam)
        what = sp.get("what") or spec["look"][0].upper() + spec["look"][1:]
        kind = "held" if sp.get("held") else "perched" if fam == "shoulder_familiar" else \
            "companion" if fam in ("familiar", "steed") else "sky"
        prompt = (f"{STYLE} A single game sprite on its own: {spec['look']}. {sp['pose'][0].upper()}{sp['pose'][1:]}. "
                  f"Its outline is the ink "
                  f"({INK}), about 8 px wide, with thinner lines inside. {PALETTE} {ea.BACKGROUND}")
        add({"class": "sprite", "family": fam, "file": f"art/equipment/sprites/{name}.png", "size": list(sp["size"]),
             "priority": priority(ps), "pieces": [p["name"] for p in ps], "what": what, "cells": sp.get("cells"),
             "use": USES[kind].format(view=sp.get("view", "")), "prompt": prompt,
             "refs": []})
    for fam, spec in BOARD_FAMILIES.items():
        ps = board_pieces(fam)
        if not ps:
            continue
        hosts = hosts_of(fam)
        pri = priority(ps)
        what = spec["look"][0].upper() + spec["look"][1:]
        if spec["cls"] == "attachment":
            for v in spec.get("views", ("front",)):
                stem = spec.get("name", fam)
                name = stem if len(spec.get("views", ())) < 2 else f"{stem}_{v}"
                seen = "seen from the side" if v == "side" else "seen from the front"
                prompt = (f"{STYLE} A single small game part on its own, {seen}: {spec['look']}. Centred, filling about "
                          f"70% of the canvas, its fixing point (where it's attached) at the bottom middle of the drawn "
                          f"shape. Its outline is the ink ({INK}), about 8 px wide. {material_note(spec)} {PALETTE} "
                          f"{ea.BACKGROUND}")
                add({"class": "attachment", "family": fam, "file": f"art/equipment/attachments/{name}.png",
                     "size": [1024, 1024], "priority": pri, "pieces": [p["name"] for p in ps], "hosts": hosts,
                     "what": what + (f" ({seen})" if len(spec.get("views", ())) > 1 else ""),
                     "use": USES["attachment"].format(hosts=", ".join(h.replace("_", " ") for h in hosts)),
                     "prompt": prompt.replace("  ", " "), "refs": []})
        elif spec["cls"] == "sheet":
            cols, rows = spec["cells"]
            W, H = spec["size"]
            look = spec["look"]
            prompt = (f"{STYLE} A sheet of small game parts on one canvas, laid out on an invisible grid of {cols} "
                      f"columns and {rows} rows of equal cells ({W // cols} by {H // rows} px), each part centred in its "
                      f"cell, filling about 60% of it and never touching its edges: {look}. "
                      + ("" if spec.get("white") else f"Outlines in the ink ({INK}). {PALETTE} ") + ea.BACKGROUND)
            add({"class": "sheet", "family": fam, "file": f"art/equipment/sheets/{fam}.png", "size": [W, H],
                 "priority": pri, "pieces": [p["name"] for p in ps], "hosts": hosts, "cells": [cols, rows],
                 "white": bool(spec.get("white")), "what": what,
                 "use": USES["sheet"].format(hosts=", ".join(h.replace("_", " ") for h in hosts[:8])
                                             + (" and more" if len(hosts) > 8 else "")),
                 "prompt": prompt, "refs": []})
        else:
            if spec.get("sprite"):
                targets = []
                for h in hosts:
                    for name, sp in SPRITES.items():
                        if sp.get("family", name) == h and name in SPRITE_PARTS.get(fam, {}):
                            targets.append((name, h))
                for name, h in targets:
                    sp = SPRITES[name]
                    prompt = (f"{STYLE} This is an edit of the attached picture, the game's picture of the "
                              f"{h.replace('_', ' ')} on its own. Paint on it {spec['look']}, inside the masked area, "
                              f"fitted to it exactly as it is drawn. Keep everything else exactly as it is. The same ink "
                              f"outline and light from the top left. {material_note(spec)} {PALETTE}")
                    isolate = (f"This is an edit of the attached picture. Keep only the {fam.replace('_', ' ')}: replace "
                               f"everything else with perfectly flat pure green #00ff00. Keep every pixel of it exactly "
                               f"where and as it is.")
                    add({"class": "host", "family": fam, "host": h, "file": f"art/equipment/sprites/{name}/{fam}.png",
                         "size": list(sp["size"]), "priority": max(pri, priority(body_pieces(h))),
                         "base": f"sprites/{name}", "pieces": [p["name"] for p in ps], "what": what,
                         "use": USES["host"].format(host="the " + h.replace("_", " "), base=f"sprites/{name}"),
                         "mask": f"art/equipment/inputs/masks/sprites/{name}/{fam}.png", "part": SPRITE_PARTS[fam][name],
                         "prompt": prompt.replace("  ", " "), "isolate": isolate,
                         "refs": [f"art/equipment/sprites/{name}.png"]})
            else:
                for h in hosts:
                    HF = families.get(h)
                    if HF is None:
                        continue
                    hb = [b.name for b in fam_bases(HF)]
                    first = None
                    for bn in spec["bases"]:
                        if bn not in hb:
                            continue
                        b = BASES[bn]
                        host_file = f"art/equipment/pieces/{bn}/{h}.png"
                        prompt = (f"{STYLE} This is an edit of the attached picture, {b.title}, wearing "
                                  f"{noun(h, 1)}. Add to it {spec['look']}, inside the masked area, fitted to the "
                                  f"{h.replace('_', ' ')} exactly as it is drawn. Keep everything else exactly as it is. "
                                  f"The same ink outline (about {line_px(b)} px) and light from the top left. "
                                  + ("It is the same as in the second attached picture. " if first else "")
                                  + f"{material_note(spec)} {PALETTE}")
                        isolate = (f"This is an edit of the attached picture. Keep only the {fam.replace('_', ' ')}: "
                                   f"replace everything else, the {h.replace('_', ' ')} and the man included, with "
                                   f"perfectly flat pure green #00ff00. Keep every pixel of it exactly where and as it is.")
                        path = f"art/equipment/pieces/{bn}/{h}/{fam}.png"
                        add({"class": "host", "family": fam, "host": h, "file": path, "size": list(b.size),
                             "priority": max(pri, HF.priority), "base": bn, "view": b.view, "rect": list(b.rect),
                             "pieces": [p["name"] for p in ps], "what": what,
                             "use": USES["host"].format(host=noun(h, 1), base=bn),
                             "mask": f"art/equipment/inputs/masks/pieces/{bn}/{h}/{fam}.png", "host_file": host_file,
                             "prompt": prompt.replace("  ", " "), "isolate": isolate,
                             "refs": [host_file] + ([first] if first else [])})
                        first = first or path
    order = {"plate": 0, "piece": 1, "sprite": 1, "host": 2, "attachment": 3, "sheet": 3}
    regions = ["Head", "Neck", "Torso", "Back", "Waist", "Right arm", "Left arm", "Right hand", "Left hand", "Right leg",
               "Left leg", "Right foot", "Left foot", "Beyond"]
    place = {}
    for fam in FAMILIES:
        ps = body_pieces(fam) or board_pieces(fam)
        place[fam] = min((regions.index(p["region"]) for p in ps if p["region"] in regions), default=len(regions))
    # In the order the player meets them (the unlock order, by depth), batch by batch.
    for a in assets:
        names = a.get("pieces") or []
        ds = [BY_NAME[n]["depth"] if not BY_NAME[n]["prestige"] else 10 ** 6 for n in names if n in BY_NAME]
        if a["class"] == "host":
            ds += [min((BY_NAME[n]["depth"] for n in BY_NAME if BY_NAME[n]["family"] == a["host"]), default=0)]
            a["depth"] = max(min(ds[:-1], default=0), ds[-1])
        else:
            a["depth"] = min(ds, default=0)
    assets.sort(key=lambda a: (a["class"] != "plate", a["priority"], a["depth"], order[a["class"]],
                               place.get(a.get("family"), 99)))
    for i, a in enumerate(assets):
        a["n"] = i + 1
    return assets


# ---------------------------------------------------------------------------------------------------------------- areas

def path(rel):
    return os.path.join(ROOT, rel)


def load_rgba(p):
    """A delivered picture as straight RGBA floats, the green keyed out (or its own transparency kept)."""
    im = Image.open(p)
    if im.mode == "RGBA" and np.asarray(im)[..., 3].min() < 250:
        return np.asarray(im).astype(np.float32)
    return ea.keyed(p)


def delivered(rel, size=None):
    p = path(rel)
    if not os.path.exists(p):
        return None
    if size and Image.open(p).size != tuple(size):
        return None
    return load_rgba(p)


def to_base(rgba, src, dst):
    """A picture painted on one base moved onto another base of the same view (a figure's piece onto a plate)."""
    k = dst.k / src.k
    W, H = dst.size
    a = rgba[..., 3:4] / 255.0
    # Canvas of dst -> view -> canvas of src.
    data = (1 / k, 0, (dst.rect[0] - src.rect[0]) * src.k, 0, 1 / k, (dst.rect[1] - src.rect[1]) * src.k)
    out = []
    for ch in [rgba[..., i] * a[..., 0] for i in range(3)] + [a[..., 0] * 255]:
        out.append(np.asarray(Image.fromarray(ch.astype(np.float32), "F").transform((W, H), Image.AFFINE, data, Image.BICUBIC)))
    pre, alpha = np.dstack(out[:3]), np.clip(out[3], 0, 255)
    colour = np.clip(pre / np.maximum(alpha / 255.0, 1e-6)[..., None], 0, 255)
    return np.dstack([colour, alpha])


def over(rgb, rgba):
    a = rgba[..., 3:4] / 255.0
    return np.clip(rgb.astype(np.float32) * (1 - a) + rgba[..., :3] * a, 0, 255).astype(np.uint8)


def areas(a, full=True):
    """An asset's canvas, what it's painted on (RGB) and its masks: where to paint and, with full, the sites it should
    cover and where its pixels may be (for the checks). None for the ones painted from a prompt alone."""
    cls = a["class"]
    if cls == "plate":
        b = BASES[a["base"]]
        seed = b.seed()
        body = b.from_view(b.V.rgba())[..., 3] > 127
        return {"base": seed, "paint": body, "core": body, "allowed": body, "unit": b.U}
    if cls == "piece":
        b = BASES[a["base"]]
        F = family(a["family"])
        paint, leaf, _ = F.area(b)
        picture, _ = base_picture(b)
        if a.get("seeded_from"):
            fig = BASES[a["seeded_from"]]
            # A cropped base can lose the endpoints of a reach primitive (for
            # example, a cloak's calves on an arm page). Project the figure's
            # mask by the same transform as its artwork so the seeded drape
            # remains paintable, even when those endpoints are off canvas.
            figure_mask = np.zeros((*fig.size[::-1], 4), np.float32)
            figure_mask[..., 3] = F.area(fig)[0] * 255.0
            paint = paint | (to_base(figure_mask, fig, b)[..., 3] > 127)
            got = delivered(f"art/equipment/pieces/{fig.name}/{a['family']}.png", fig.size)
            if got is not None:
                picture = over(picture, to_base(got, fig, b))
        if not full:
            return {"base": picture, "paint": paint, "unit": b.U}
        core = coarse(leaf, 0.5 * b.U, shrink_mask) if F.cover == "full" else leaf
        return {"base": picture, "paint": paint, "core": core, "allowed": coarse(paint, 1.2 * b.U, grow), "unit": b.U}
    if cls == "host" and a["base"] in BASES:
        b = BASES[a["base"]]
        spec = BOARD_FAMILIES[a["family"]]
        picture, _ = base_picture(b)
        host = delivered(a["host_file"], b.size)
        HF = family(a["host"])
        harea = HF.area(b)[0] if host is None else host[..., 3] > 127
        if host is not None:
            picture = over(picture, host)
        paint = np.zeros(b.size[::-1], bool)
        for prim in spec.get("reach", []):
            m = extent(b, prim)
            if m is not None:
                paint |= m
        if a["family"] != "aventail":
            # It lies on its host (an aventail hangs below the helm).
            paint &= coarse(harea, 1.5 * b.U, grow)
        allowed = coarse(paint, 1.2 * b.U, grow) if full else None
        return {"base": picture, "paint": paint, "core": paint, "allowed": allowed, "unit": b.U}
    if cls == "host":
        name = a["base"].split("/")[1]
        sp = SPRITES[name]
        W, H = sp["size"]
        host = delivered(f"art/equipment/sprites/{name}.png", sp["size"])
        if host is None:
            return {"base": np.zeros((H, W, 3), np.uint8) + GREEN.astype(np.uint8), "paint": np.ones((H, W), bool),
                    "core": np.ones((H, W), bool), "allowed": np.ones((H, W), bool), "unit": W / 60, "missing_host": True}
        body = host[..., 3] > 127
        x0, y0, x1, y1 = ea.bbox(body)
        px0, py0, px1, py1 = a["part"]
        bw, bh = x1 - x0, y1 - y0
        paint = rect_mask((W, H), x0 + px0 * bw, y0 + py0 * bh, x0 + px1 * bw, y0 + py1 * bh) & coarse(body, W / 40, grow)
        if a["family"] == "shield_rim":
            paint = body & ~shrink_mask(body, W * 0.06)
        if a["family"] == "shield_boss":
            cx, cy = x0 + 0.5 * bw, y0 + 0.42 * bh
            paint = ellipse_mask((W, H), [cx - 0.2 * bw, cy - 0.2 * bw, cx + 0.2 * bw, cy + 0.2 * bw])
        return {"base": over(np.zeros((H, W, 3), np.uint8) + GREEN.astype(np.uint8), host), "paint": paint,
                "core": paint, "allowed": coarse(paint, W / 25, grow), "unit": W / 60}
    return None


# ---------------------------------------------------------------------------------------------------------------- inputs

def save_rgb(arr, p):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    if p.endswith(".jpg"):
        Image.fromarray(arr.astype(np.uint8), "RGB").save(p, quality=85)
    else:
        Image.fromarray(arr.astype(np.uint8), "RGB").save(p)


def edit_mask(paint):
    """The image tool's mask: fully transparent where it may paint, opaque black elsewhere."""
    H, W = paint.shape
    m = np.zeros((H, W, 4), np.uint8)
    m[..., 3] = np.where(paint, 0, 255)
    return Image.fromarray(m, "RGBA")


def guide(picture, paint):
    """The picture with the area to paint tinted and outlined, to look at."""
    out = picture.astype(np.float32)
    tint = np.array([255, 60, 200], np.float32)
    out[paint] = out[paint] * 0.6 + tint * 0.4
    edge = paint & ~shrink_mask(paint, 2)
    out[edge] = (255, 255, 255)
    return out.astype(np.uint8)


def select(assets, ids):
    """The assets named: an id exactly, or every id under a prefix ending in "/" or "*" (or matching no id exactly)."""
    if not ids:
        return assets
    known = {a["id"] for a in assets}
    out = []
    for a in assets:
        for i in ids:
            prefix = i.endswith(("/", "*")) or i not in known
            if a["id"] == i or prefix and a["id"].startswith(i.rstrip("*")):
                out.append(a)
                break
    return out


def inputs(args):
    assets = select(build(), args.ids)
    done = set()
    waiting = []
    n = 0
    for a in assets:
        cls = a["class"]
        if cls in ("sprite", "attachment", "sheet"):
            continue
        if cls == "host":
            host = a["host_file"] if a["base"] in BASES else a["refs"][0]
            if not os.path.exists(path(host)):
                waiting.append(f"{a['id']} (after {host[len('art/equipment/'):-4]})")
                continue
        ar = areas(a, full=False)
        if ar is None:
            continue
        if cls == "plate":
            save_rgb(ar["base"], path(a["seed"]))
            n += 1
            continue
        rel = a["file"][len("art/equipment/"):-4]
        if a.get("base") in BASES and a["base"] not in done and not a.get("seeded_from") and cls == "piece":
            done.add(a["base"])
            save_rgb(base_picture(BASES[a["base"]])[0], os.path.join(INPUTS, "bases", a["base"] + ".png"))
        if a.get("seeded_from") and not os.path.exists(path(f"art/equipment/pieces/{a['seeded_from']}/{a['family']}.png")):
            waiting.append(f"{a['id']} (after pieces/{a['seeded_from']}/{a['family']})")
        elif cls == "host" or a.get("seeded_from"):
            # What this one is painted on: its base with its host, or a plate with the whole figure's picture of it.
            save_rgb(ar["base"], os.path.join(INPUTS, "bases", rel + ".png"))
        mpath = os.path.join(INPUTS, "masks", rel + ".png")
        os.makedirs(os.path.dirname(mpath), exist_ok=True)
        edit_mask(ar["paint"]).save(mpath)
        save_rgb(guide(ar["base"], ar["paint"]), os.path.join(INPUTS, "guides", rel + ".jpg"))
        n += 1
    print(f"Wrote inputs for {n} files into art/equipment/inputs/ (seeds/, bases/, masks/, guides/)")
    if waiting:
        print(f"{len(waiting)} wait for the picture they're painted on (a host, or the whole figure's picture that seeds a "
              f"plate): run inputs again once it's delivered. " + "; ".join(waiting[:8]) + (" ..." if len(waiting) > 8 else ""))


def input_base(a):
    """The picture an asset is painted on, as the inputs command writes it."""
    rel = a["file"][len("art/equipment/"):-4]
    if a["class"] == "plate":
        return a["seed"]
    if a["class"] == "host" or a.get("seeded_from"):
        return f"art/equipment/inputs/bases/{rel}.png"
    if a["class"] == "piece":
        return f"art/equipment/inputs/bases/{a['base']}.png"
    return None


# ---------------------------------------------------------------------------------------------------------------- checks

def outline(rgba, mask, margin=12):
    """How much of the drawn shape's edge is dark ink, leaving out where the canvas's edge cuts it (a plate cuts his
    arms and legs; a cloak runs off a plate)."""
    edge = mask & ~ea.erode(mask, 3)
    edge[:margin] = edge[-margin:] = False
    edge[:, :margin] = edge[:, -margin:] = False
    if edge.sum() == 0:
        return 1.0
    return float((ea.luminance(rgba)[edge] < 0.28).mean())


LIMITS = {"inside": 0.95, "inside_behind": 0.9, "cover_full": 0.75, "ink_min": 0.03, "part_min": 0.02,
          "outline": 0.6, "spill": 0.004, "plate_fit": 0.93, "plate_colour": 16, "plate_new": 0.02,
          "border": 0.97, "sprite_outline": 0.7}


def check_one(a):
    r = ea.Result(a["id"])
    p = path(a["file"])
    if not os.path.exists(p):
        r.add("delivered", False, f"no {a['file']}")
        return r
    im = Image.open(p)
    W, H = a["size"]
    r.add("size", im.size == (W, H), f"{im.size[0]}x{im.size[1]} (want {W}x{H})")
    r.add("format", im.format == "PNG" and im.mode in ("RGB", "RGBA"), f"{im.format} {im.mode}")
    if im.size != (W, H):
        return r
    rgba = np.asarray(im.convert("RGBA")).astype(np.int16)
    mask, how = ea.subject(rgba)
    sp = ea.spill(rgba, mask)
    r.add("no key green", sp <= LIMITS["spill"], f"{sp:.2%} of it is key green")
    cls = a["class"]
    if cls == "plate":
        ar = areas(a)
        fit = ea.iou(mask, ar["paint"])
        r.add("lines up", fit >= LIMITS["plate_fit"], f"its outline overlaps the seed's {fit:.0%} (at least "
              f"{LIMITS['plate_fit']:.0%})")
        both = mask & ar["paint"]
        rad = max(2, int(ar["unit"]))
        soft = lambda x: np.asarray(Image.fromarray(x.astype(np.uint8)).filter(ImageFilter.BoxBlur(rad))).astype(np.float32)
        got, want = soft(rgba[..., :3]), soft(ar["base"])
        diff = np.abs(got - want).mean(-1)[both]
        r.add("same colours", diff.mean() <= LIMITS["plate_colour"], f"differs from the seed by {diff.mean():.1f} of 255 "
              f"on average")
        new = (diff > 70).mean()
        r.add("nothing added", new <= LIMITS["plate_new"], f"{new:.1%} of the body changed a lot (gear, marks?)")
        ink = outline(rgba, mask)
        r.add("ink outline", ink >= LIMITS["outline"], f"{ink:.0%} of his edge is dark ink")
        return r
    if cls in ("piece", "host"):
        ar = areas(a)
        if ar.get("missing_host"):
            r.add("host", False, f"its host's picture isn't delivered yet ({a['refs'][0]})")
            return r
        total = mask.sum()
        r.add("drawn", total > 0.05 * ar["unit"] ** 2, f"{total} pixels drawn ({how})")
        if total == 0:
            return r
        inside = (mask & ar["allowed"]).sum() / total
        want = LIMITS["inside_behind"] if a.get("behind") else LIMITS["inside"]
        r.add("in its place", inside >= want, f"{inside:.0%} of it is where it's worn (at least {want:.0%})")
        cover = a.get("cover", "part")
        core = ar["core"]
        if cover == "full" and core.any():
            share = (mask & core).sum() / core.sum()
            r.add("covers its sites", share >= LIMITS["cover_full"], f"covers {share:.0%} of the sites it fills "
                  f"(at least {LIMITS['cover_full']:.0%})")
        elif cover == "ink" and core.any():
            share = (mask & core).sum() / core.sum()
            r.add("ink on its sites", share >= LIMITS["ink_min"], f"ink over {share:.0%} of its sites")
        else:
            share = total / max(1, ar["paint"].sum())
            r.add("big enough", share >= LIMITS["part_min"], f"fills {share:.1%} of its area")
        if cover != "ink":
            ink = outline(rgba, mask)
            r.add("ink outline", ink >= LIMITS["outline"], f"{ink:.0%} of its edge is dark ink")
        return r
    # Sprites, set pieces and sheets: one thing (or a grid of them) on its own.
    r.add("background", ea.border(~mask).mean() >= LIMITS["border"], f"{ea.border(~mask).mean():.1%} of the border "
          f"is background ({how})")
    if cls == "sheet" or a.get("cells"):
        cols, rows = a["cells"]
        cw, ch = W // cols, H // rows
        lum = ea.luminance(rgba)
        for i in range(cols * rows):
            cx, cy = i % cols, i // cols
            cell = mask[cy * ch:(cy + 1) * ch, cx * cw:(cx + 1) * cw]
            fill = cell.mean()
            clear = not (cell[:6].any() or cell[-6:].any() or cell[:, :6].any() or cell[:, -6:].any())
            ok = 0.04 <= fill <= 0.7 and clear
            detail = f"fills {fill:.0%} of its cell, {'clear of' if clear else 'touching'} its edges"
            if a.get("white"):
                light = lum[cy * ch:(cy + 1) * ch, cx * cw:(cx + 1) * cw][cell].mean() if cell.any() else 0
                ok &= light >= 0.75
                detail += f", brightness {light:.0%}"
            r.add(f"cell {i + 1}", ok, detail)
        return r
    box = ea.bbox(mask)
    if box is None:
        r.add("drawn", False, "nothing on the canvas")
        return r
    x0, y0, x1, y1 = box
    big = max((x1 - x0) / W, (y1 - y0) / H)
    r.add("size on the canvas", 0.3 <= big <= 0.97, f"its longer side is {big:.0%} of the canvas")
    cx, cy = (x0 + x1) / 2 / W, (y0 + y1) / 2 / H
    r.add("centred", abs(cx - 0.5) <= 0.12 and (abs(cy - 0.5) <= 0.15 or cls == "sprite"),
          f"its middle is at {cx:.0%}, {cy:.0%} of the canvas")
    ink = ea.outline_share(rgba, mask)
    r.add("ink outline", ink >= LIMITS["sprite_outline"], f"{ink:.0%} of its edge is dark ink")
    return r


def check(args, quiet=False):
    assets = select(build(), args.ids)
    if not getattr(args, "all", False):
        present = [a for a in assets if os.path.exists(path(a["file"]))]
        missing = len(assets) - len(present)
    else:
        present, missing = assets, 0
    results = [check_one(a) for a in present]
    for r in results:
        if quiet and r.ok:
            continue
        print(f"{'PASS' if r.ok else 'FAIL'}  {r.id}")
        for name, passed, detail in r.lines:
            print(f"   {'ok ' if passed else 'NO '} {name}: {detail}")
    bad = [r.id for r in results if not r.ok]
    print(f"\n{len(results) - len(bad)} of {len(results)} delivered files pass; {missing} of {len(assets)} not delivered yet"
          + (f"; fix {', '.join(bad[:20])}" + (" ..." if len(bad) > 20 else "") if bad else ""))
    return results, 1 if bad else 0


# ---------------------------------------------------------------------------------------------------------------- review

def label(img, text, colour):
    d = ImageDraw.Draw(img)
    d.rectangle([0, img.height - 22, img.width, img.height], fill=(14, 11, 19))
    d.text((6, img.height - 18), text, fill=colour)


def tile(a, result, size=360):
    """A delivered file over what it's painted on, cropped to where it is."""
    p = path(a["file"])
    got = load_rgba(p) if os.path.exists(p) else None
    ar = areas(a) if a["class"] in ("plate", "piece", "host") else None
    if ar is not None and got is not None and got.shape[:2] == ar["base"].shape[:2]:
        pic = over(ar["base"], got) if a["class"] != "plate" else on_green(got)
        m = (got[..., 3] > 127) | ar["paint"]
    elif got is not None:
        pic = on_green(got)
        m = got[..., 3] > 127
    else:
        pic = ar["base"] if ar is not None else np.zeros((64, 64, 3), np.uint8)
        m = ar["paint"] if ar is not None else np.ones((64, 64), bool)
    box = ea.bbox(m, 1) or (0, 0, pic.shape[1], pic.shape[0])
    pad = 20
    x0, y0 = max(0, box[0] - pad), max(0, box[1] - pad)
    x1, y1 = min(pic.shape[1], box[2] + pad), min(pic.shape[0], box[3] + pad)
    im = Image.fromarray(pic[y0:y1, x0:x1])
    im.thumbnail((size, size - 24))
    out = Image.new("RGB", (size, size), (24, 20, 31))
    out.paste(im, ((size - im.width) // 2, (size - 24 - im.height) // 2))
    ok = result.ok if result else None
    name = a["base"] if a["class"] in ("piece", "host") else a["id"].split("/")[-1]
    label(out, f"{name}  {'PASS' if ok else 'FAIL' if ok is False else 'missing'}",
          (120, 230, 120) if ok else (240, 110, 110) if ok is False else (150, 140, 160))
    return out


def sheet(args):
    assets = select(build(), args.ids)
    groups = OrderedDict()
    for a in assets:
        if not os.path.exists(path(a["file"])):
            continue
        key = a["family"] if a["class"] in ("piece", "host") else a["class"] + "s"
        groups.setdefault(key, []).append(a)
    os.makedirs(REVIEW, exist_ok=True)
    for key, group in groups.items():
        results = {a["id"]: check_one(a) for a in group}
        tiles = [tile(a, results[a["id"]]) for a in group]
        cols = min(6, len(tiles))
        rows = (len(tiles) + cols - 1) // cols
        out = Image.new("RGB", (360 * cols, 360 * rows + 30), (14, 11, 19))
        ImageDraw.Draw(out).text((8, 8), key, fill=(230, 220, 240))
        for i, t in enumerate(tiles):
            out.paste(t, (360 * (i % cols), 30 + 360 * (i // cols)))
        out.save(os.path.join(REVIEW, f"{key}.png"))
    print(f"Wrote {len(groups)} review sheets into art/equipment/review/pieces/")


# ---------------------------------------------------------------------------------------------------------------- spec, list

SECTIONS = [("plate", "Plates: the bare body, region by region"), ("piece", "Pieces as worn, painted on the body"),
            ("sprite", "Held pieces, companions and the sky"), ("host", "Pieces set in pieces, painted on their host"),
            ("attachment", "Pieces set in pieces, drawn on their own"), ("sheet", "Sheets of small parts")]


def code_only():
    out = []
    for fam, spec in FAMILIES.items():
        if spec["cls"] == "code":
            out.append({"family": fam, "pieces": [p["name"] for p in body_pieces(fam)], "why": spec["why"]})
    for fam, why in BOARD_CODE.items():
        ps = board_pieces(fam)
        if ps:
            out.append({"family": fam, "pieces": [p["name"] for p in ps], "why": why})
    return out


def summary(assets):
    s = OrderedDict()
    for cls, _ in SECTIONS:
        row = [sum(1 for a in assets if a["class"] == cls and a["priority"] == p) for p in (1, 2, 3)]
        s[cls] = {"priority 1": row[0], "priority 2": row[1], "priority 3": row[2], "all": sum(row)}
    return s


def spec_cmd(_args):
    assets = build()
    for a in assets:
        a["paint_on"] = input_base(a)
        if a.get("mask"):
            a["guide"] = a["mask"].replace("/inputs/masks/", "/inputs/guides/")[:-4] + ".jpg"
    out = OrderedDict()
    out["about"] = ("Phase 2 of the equipment screen's art (docs/art-handoff-equipment-pieces.md): the bare body region "
                    "by region (plates) and every piece as it's worn. Upload each file to its 'file' path. Make what a "
                    "file is painted on with `python3 tools/equipment_pieces.py inputs`, and check deliveries with "
                    "`python3 tools/equipment_pieces.py check`.")
    out["style"], out["palette"], out["tattoo_ink"] = STYLE, PALETTE, INK_STYLE
    out["dyes"] = {k: {"name": v[0], "mid": v[1]} for k, v in DYES.items()}
    out["budget_pixels"] = BUDGET
    out["bases"] = OrderedDict((b.name, {"view": b.view, "kind": b.kind, "region": b.region, "size": list(b.size),
                                         "rect_in_view": list(b.rect), "scale": round(b.k, 4),
                                         "body_unit_px": round(b.U, 2), "what": b.title})
                               for b in BASES.values())
    out["summary"] = summary(assets)
    out["assets"] = assets
    out["code_only"] = code_only()
    with open(SPEC, "w") as f:
        json.dump(out, f, indent=1)
    n = len(assets)
    print(f"Wrote art/equipment/handoff-pieces.json: {n} files "
          f"({', '.join(f'{v['all']} {k}s' for k, v in out['summary'].items())}), {len(out['code_only'])} families "
          f"drawn by the game")


def list_cmd(_args):
    assets = build()
    lines = ["# Phase 2 art: the full list", "",
             "Generated by `python3 tools/equipment_pieces.py list` from the same data as "
             "[`art/equipment/handoff-pieces.json`](../art/equipment/handoff-pieces.json), which has each file's full "
             "prompts, references and masks. How to make them is in [art-handoff-equipment-pieces.md]"
             "(art-handoff-equipment-pieces.md). Run `list` again after delivering to tick off what's in "
             "(a file is ticked when it's there and passes `check`).", "",
             "**Pri** is the batch (1 first). A piece's file is `pieces/<base>/<family>.png`: the family's pieces that "
             "show on that base (its picture to paint on is `art/equipment/inputs/bases/<base>.png`), and **Pieces** "
             "counts them. ★ marks a family's first file, which sets its design: make it first, then pass it as a "
             "reference picture for the family's other files (\"as #n\"). ↻ marks the plates painted on a seeded base "
             "(the plate with the family's whole-figure picture on it; `inputs` makes it).", ""]
    s = summary(assets)
    lines += ["| What | Priority 1 | Priority 2 | Priority 3 | All |", "|---|---|---|---|---|"]
    for cls, title in SECTIONS:
        v = s[cls]
        lines.append(f"| {title} | {v['priority 1']} | {v['priority 2']} | {v['priority 3']} | {v['all']} |")
    tot = [sum(v[f"priority {p}"] for v in s.values()) for p in (1, 2, 3)]
    lines.append(f"| **All** | **{tot[0]}** | **{tot[1]}** | **{tot[2]}** | **{sum(tot)}** |")
    lines.append("")
    for cls, title in SECTIONS:
        group = [a for a in assets if a["class"] == cls]
        if not group:
            continue
        lines += [f"## {title}", ""]
        if cls == "piece":
            lines += ["| Done | # | File (`art/equipment/pieces/…`) | Size | Pri | Pieces | What |", "|---|---|---|---|---|---|---|"]
        else:
            lines += ["| Done | # | File (`art/equipment/…`) | Size | Pri | What |", "|---|---|---|---|---|---|"]
        first = {}
        for a in group:
            ok = os.path.exists(path(a["file"])) and check_one(a).ok
            mark = " ★" if a.get("master") else ""
            mark += " ↻" if a.get("seeded_from") else ""
            what = a["what"].rstrip(".")
            if len(what) > 150:
                what = what[:147].rsplit(" ", 1)[0] + "…"
            if cls == "piece":
                if a.get("master"):
                    first[a["family"]] = a["n"]
                else:
                    what = f"as #{first.get(a['family'], '?')}"
            size = f"{a['size'][0]}×{a['size'][1]}"
            if cls == "piece":
                rel = a["file"][len("art/equipment/pieces/"):]
                n = len(a["pieces"])
                lines.append(f"| {'[x]' if ok else '[ ]'} | {a['n']} | `{rel}`{mark} | {size} | {a['priority']} | {n} | {what} |")
            else:
                rel = a["file"][len("art/equipment/"):]
                lines.append(f"| {'[x]' if ok else '[ ]'} | {a['n']} | `{rel}`{mark} | {size} | {a['priority']} | {what} |")
        lines.append("")
    lines += ["## Drawn by the game, no file", "", "| Family | Pieces | How |", "|---|---|---|"]
    for c in code_only():
        names = ", ".join(c["pieces"][:4]) + (f" and {len(c['pieces']) - 4} more" if len(c["pieces"]) > 4 else "")
        lines.append(f"| {c['family'].replace('_', ' ')} | {names} | {c['why']} |")
    lines.append("")
    with open(LIST, "w") as f:
        f.write("\n".join(lines))
    print(f"Wrote docs/art-handoff-equipment-pieces-list.md: {len(assets)} files")


# ---------------------------------------------------------------------------------------------------------------- import

PIECES_OUT = os.path.join(EQUIP, "pieces")
PLATES_OUT = os.path.join(EQUIP, "plates")
SPRITES_OUT = os.path.join(EQUIP, "sprites")
PIECES_JSON = os.path.join(EQUIP, "pieces.json")
# Kinds whose plain steel the game recolours for each era (cloth, ink and the rest keep their colours).
METAL_KINDS = ("Armor", "Weapon", "Jewelry")
# Where a held piece stands beside him on the whole figure, from the front: his side (his right is the viewer's left),
# its height as a share of his, and whether it floats at the hand rather than standing on the floor.
HELD = {"main_hand": ("R", 0.52, False), "great_weapon": ("R", 0.7, False), "off_hand": ("L", 0.27, False),
        "shield": ("L", 0.42, False), "torch": ("L", 0.36, False), "focus_orb": ("L", 0.13, True)}


def keep_exact(p):
    """A site map is read pixel for pixel: imported lossless and without mipmaps."""
    if os.path.exists(p):
        text = open(p).read()
        text = text.replace("mipmaps/generate=true", "mipmaps/generate=false")
        for mode in ("1", "2", "3", "4"):
            text = text.replace(f"compress/mode={mode}", "compress/mode=0")
    else:
        text = '[remap]\n\nimporter="texture"\ntype="CompressedTexture2D"\n\n[params]\n\ncompress/mode=0\nmipmaps/generate=false\n'
    with open(p, "w") as f:
        f.write(text)


def res(p):
    return "res://" + os.path.relpath(p, ROOT).replace(os.sep, "/")


def save_png(arr, p, mode="RGBA"):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    Image.fromarray(np.round(arr).astype(np.uint8), mode).save(p, optimize=True)


def loops_of(mask, origin, scale, tolerance=1.2):
    """The outline of a picture's shape as loops in a view's pixels (for lighting it in its own shape)."""
    from skimage import measure
    out = []
    padded = np.pad(mask.astype(np.float32), 1)
    for c in measure.find_contours(padded, 0.5):
        if len(c) < 12:
            continue
        c = measure.approximate_polygon(c, tolerance)
        pts = [[round(origin[0] + (x - 1) / scale, 1), round(origin[1] + (y - 1) / scale, 1)] for y, x in c]
        if len(pts) >= 4:
            out.append(pts)
    out.sort(key=len, reverse=True)
    return out[:12]


def plate_ids(b, body):
    """A plate's site map: the figure's, moved onto the plate (each site's area scaled smoothly, the likeliest site at
    each pixel), kept to the plate's own body and filled out to its edge."""
    from scipy import ndimage
    v = b.V
    W, H = b.size
    x0, y0 = max(0, int(b.rect[0]) - 2), max(0, int(b.rect[1]) - 2)
    x1, y1 = min(v.w, int(b.rect[2]) + 3), min(v.h, int(b.rect[3]) + 3)
    present = [int(i) for i in np.unique(v.ids[y0:y1, x0:x1]) if i]
    best = np.zeros((H, W), np.float32)
    ids = np.zeros((H, W), np.uint8)
    data = (1 / b.k, 0, b.rect[0], 0, 1 / b.k, b.rect[1])
    for sid in present:
        m = Image.fromarray(((v.ids == sid) * 255).astype(np.float32), "F")
        m = np.asarray(m.transform((W, H), Image.AFFINE, data, Image.BILINEAR))
        better = m > best
        best[better] = m[better]
        ids[better] = sid
    ids[(best < 1) | ~body] = 0
    hole = body & (ids == 0)
    if hole.any() and (ids > 0).any():
        _, (iy, ix) = ndimage.distance_transform_edt(ids == 0, return_indices=True)
        ids[hole] = ids[iy[hole], ix[hole]]
    return ids, present


def import_plate(b):
    """A delivered plate into the game: its picture, its site map and its slots' points, in its own pixels."""
    rgba = ea.keyed(plate_file(b.name))
    body = rgba[..., 3] > 127
    name = "plate_" + b.name
    ids, present = plate_ids(b, body)
    # Number the sites on the plate 1.. in the figure's order.
    remap = np.zeros(256, np.uint8)
    keys, sites = [], []
    figure_sites = {s["key"]: s for s in MAP["views"][b.view]["sites"]}
    for sid in sorted(present):
        if not (ids == sid).any():
            continue
        remap[sid] = len(keys) + 1
        key = b.V.keys[sid - 1]
        keys.append(key)
        ys, xs = np.where(ids == sid)
        sites.append({"key": key, "box": [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1],
                      "busy": bool(figure_sites.get(key, {}).get("busy", False))})
    ids = remap[ids]
    save_png(rgba, os.path.join(PLATES_OUT, name + ".png"))
    sites_png = os.path.join(PLATES_OUT, name + "_sites.png")
    save_png(ids, sites_png, "L")
    keep_exact(sites_png + ".import")
    W, H = b.size
    points = {}
    for cell, (x, y) in MAP["overview"].get(b.view, {}).items():
        px, py = (x - b.rect[0]) * b.k, (y - b.rect[1]) * b.k
        if 0 <= px < W and 0 <= py < H:
            points[cell] = [round(px, 1), round(py, 1)]
    return name, {"view": b.view, "region": b.region, "rect": list(b.rect), "scale": round(b.k, 5), "size": [W, H],
                  "texture": res(os.path.join(PLATES_OUT, name + ".png")), "sites_texture": res(sites_png),
                  "sites": sites, "points": points}


_HAIR = {}
# The views where his hair hangs over what he wears: from behind. From the front it's behind his neck, and in profile
# the sides of what's round his neck are nearer than the braid down the middle of his back.
HAIR_OVER = ("back",)


def hair_of(b):
    """Where his hair lies over what he wears on a base (his braid down his back and the back of his neck), and the
    base's own picture: the hair site less the skin round it. None where there's none."""
    if b.name not in _HAIR:
        got = None
        if b.view in HAIR_OVER and b.has("hair"):
            if b.kind == "plate":
                pic = ea.keyed(plate_file(b.name))
                ids, _ = plate_ids(b, pic[..., 3] > 127)
                hair = ids == b.V.index["hair"]
            else:
                pic = b.from_view(b.V.rgba())
                hair = b.mask(["hair"])
            # The site reaches a little past the braid: its skin (bright, warm) isn't hair.
            hair &= (pic[..., 0] < 135) & (pic[..., 3] > 127)
            if hair.sum() > 30:
                got = hair, pic
        _HAIR[b.name] = got
    return _HAIR[b.name]


def under_hair(b, rgba):
    """A piece's picture with his hair over it as the base has it. Each picture left a hole where its painter drew his
    hair, never quite the base's hair: the hole is closed with the piece's own colours where the piece runs on both
    sides of it, and the base's hair is laid over the piece, so every picture shows the same hair, over it."""
    from scipy import ndimage
    got = hair_of(b)
    if got is None:
        return rgba
    hair, pic = got
    drawn = rgba[..., 3] > 127
    r = b.U
    band = grow(hair, r)
    if not (band & drawn).any():
        return rgba
    # Rows where the piece runs on both sides of the hair, near enough to be the same piece behind it: close to the
    # hair's edges where the row has hair (not a collar rising away from it), and anywhere near below its tip.
    ys, xs = np.where(hair)
    reach = int(xs.max() - xs.min() + 2 * r)
    H, W = drawn.shape
    idx = np.arange(W)[None, :].repeat(H, 0)
    left = np.maximum.accumulate(np.where(drawn, idx, -10 ** 6), axis=1)
    right = np.minimum.accumulate(np.where(drawn, idx, 10 ** 6)[:, ::-1], axis=1)[:, ::-1]
    ry = np.arange(H)
    has = hair.any(axis=1)
    hl = hair.argmax(axis=1)
    hr = W - 1 - hair[:, ::-1].argmax(axis=1)
    close = ~has | ((hl - left[ry, hl] <= r) & (right[ry, hr] - hr <= r))
    # A row or two that misses (where the braid's tie is lighter, say) between rows that pass, passes.
    close = ndimage.binary_closing(close, structure=np.ones(max(3, int(r / 2)), bool))
    gap = band & ~drawn & (idx - left <= reach) & (right - idx <= reach) & close[:, None]
    inside = drawn | gap
    out = rgba.copy()
    if gap.any():
        # Each pixel takes the nearer side's colour along its row, past the line drawn round the hole (a line across,
        # like a collar's edge, carries on behind the hair), and so does that line.
        e = line_px(b) + 2
        clean = drawn & (rgba[..., 3] > 250) & (rgba[..., 1] <= np.maximum(rgba[..., 0], rgba[..., 2]) + 6)
        src = ndimage.binary_erosion(clean, structure=np.ones((1, 2 * e + 1), bool))
        gap = gap | (grow(gap, e) & drawn & band & ~src)
        sl = np.maximum.accumulate(np.where(src, idx, -10 ** 6), axis=1)
        sr = np.minimum.accumulate(np.where(src, idx, 10 ** 6)[:, ::-1], axis=1)[:, ::-1]
        pick = np.where(idx - sl <= sr - idx, sl, sr)
        ok = (pick >= 0) & (pick < W)
        _, (iy, ix) = ndimage.distance_transform_edt(~(src if src.any() else drawn), return_indices=True)
        rows = np.arange(H)[:, None].repeat(W, 1)
        iy, ix = np.where(ok, rows, iy), np.where(ok, pick, ix)
        out[gap, :3] = rgba[iy[gap], ix[gap], :3]
        out[gap, 3] = 255
    over = hair & inside
    out[over, :3] = pic[over, :3]
    out[over, 3] = 255
    return out


def target(b):
    """The view a picture painted on a base draws on, and how the base's pixels map to it (origin, scale)."""
    if b.kind == "plate":
        return "plate_" + b.name, (0.0, 0.0), 1.0
    return b.view, (b.rect[0], b.rect[1]), b.k


def import_piece(a, plates_in):
    """A delivered family file into the game: cut into its pieces (those on the same sites share a picture), each
    cropped to what's drawn, split into what's behind him and in front where it hangs behind, with its outline."""
    from scipy import ndimage
    b = BASES[a["base"]]
    if b.kind == "plate" and b.name not in plates_in:
        return []
    F = family(a["family"])
    rgba = load_rgba(path(a["file"]))
    if a["pieces"] and BY_NAME[a["pieces"][0]]["region"] != "Head":
        rgba = under_hair(b, rgba)
    alpha = rgba[..., 3]
    drawn = alpha > 4
    if not drawn.any():
        return []
    groups = OrderedDict()
    for name in a["pieces"]:
        ls = tuple(sorted(set(l for l in F.leaves[name] if b.has(l))))
        groups.setdefault(ls, []).append(name)
    owner = np.zeros(drawn.shape, np.int32)
    if len(groups) > 1:
        dists = []
        for ls in groups:
            m = b.mask(list(ls))
            dists.append(ndimage.distance_transform_edt(~m) if m.any() else np.full(drawn.shape, 1e9, np.float32))
        owner = np.argmin(np.stack(dists), 0)
    behind_split = bool(a.get("behind"))
    if behind_split:
        if b.kind == "plate":
            body = ea.keyed(plate_file(b.name))[..., 3] > 127
        else:
            body = b.from_view(b.V.rgba())[..., 3] > 127
        body = grow(body, 2)
    view, origin, scale = target(b)
    kind = BY_NAME[a["pieces"][0]]["kind"] if a["pieces"] else ""
    layer = max(F.layers, key=LAYERS.index) if F.layers else "Adorn"
    out = []
    for gi, names in enumerate(groups.values()):
        mine = drawn & (owner == gi)
        if mine.sum() < 30:
            continue
        ys, xs = np.where(mine)
        x0, y0 = max(0, xs.min() - 2), max(0, ys.min() - 2)
        x1, y1 = min(drawn.shape[1], xs.max() + 3), min(drawn.shape[0], ys.max() + 3)
        part = rgba[y0:y1, x0:x1].copy()
        part[..., 3] *= mine[y0:y1, x0:x1]
        stem = a["family"] + (f".{gi}" if len(groups) > 1 else "")
        folder = os.path.join(PIECES_OUT, b.name)
        # For the order within a layer: what hangs behind him goes over the rest on his back, and smaller pieces over
        # bigger ones (its area is in the whole figure's pixels, the same on every view of it).
        entry = {"pieces": names, "family": a["family"], "layer": layer, "metal": kind in METAL_KINDS,
                 "drape": bool(F.spec.get("behind")), "area": int(round(mine.sum() / b.k ** 2)),
                 "view": view, "rect": [round(origin[0] + x0 / scale, 2), round(origin[1] + y0 / scale, 2),
                                        round((x1 - x0) / scale, 2), round((y1 - y0) / scale, 2)],
                 "base": b.name, "texture": None, "behind": None,
                 "loops": loops_of(mine[y0:y1, x0:x1], (origin[0] + x0 / scale, origin[1] + y0 / scale), scale)}
        if behind_split:
            inside = body[y0:y1, x0:x1]
            front, back = part.copy(), part.copy()
            front[..., 3] *= inside
            back[..., 3] *= ~inside
            if back[..., 3].max() > 4:
                save_png(back, os.path.join(folder, stem + ".behind.png"))
                entry["behind"] = res(os.path.join(folder, stem + ".behind.png"))
            part = front
        if part[..., 3].max() > 4:
            save_png(part, os.path.join(folder, stem + ".png"))
            entry["texture"] = res(os.path.join(folder, stem + ".png"))
        out.append(entry)
    return out


def held_rect(name, aspect):
    """Where a held piece stands on the whole figure from the front, in its pixels: on the floor in the gap between
    his hand and his thigh, on his side for it (or floating at the hand), as tall as it really is beside him."""
    side, share, floats = HELD[name]
    v = BASES["figure_front"]
    hand, thigh = v.box([f"{side}.hand", f"{side}.thumb", f"{side}.index", f"{side}.little"]), v.box([f"{side}.thigh", f"{side}.outthigh"])
    feet = v.box(["R.foot", "L.foot", "R.bigtoe", "L.bigtoe"])
    to_view = lambda x: v.rect[0] + x / v.k
    gap = (to_view(hand[2]), to_view(thigh[0])) if side == "R" else (to_view(thigh[2]), to_view(hand[0]))
    cx = (gap[0] + gap[1]) / 2
    h = share * U_FIG * 100
    w = h * aspect
    if floats:
        cy = v.rect[1] + (hand[1] + hand[3]) / 2 / v.k
        return [round(cx - w / 2, 2), round(cy - h / 2, 2), round(w, 2), round(h, 2)]
    floor = v.rect[1] + feet[3] / v.k
    return [round(cx - w / 2, 2), round(floor - h, 2), round(w, 2), round(h, 2)]


def import_sprite(a):
    """A held piece into the game: it stands just behind him (his body in front of it, the rest beside him), so its
    outline, for the mouse, is the part that shows."""
    from scipy import ndimage
    name = a["file"].split("/")[-1][:-4]
    if name not in HELD:
        return []
    rgba = load_rgba(path(a["file"]))
    drawn = rgba[..., 3] > 4
    if not drawn.any():
        return []
    ys, xs = np.where(drawn)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    part = rgba[y0:y1, x0:x1]
    out_png = os.path.join(SPRITES_OUT, name + ".png")
    save_png(part, out_png)
    rect = held_rect(name, (x1 - x0) / (y1 - y0))
    scale = (y1 - y0) / rect[3]
    v = VIEWS["front"]
    body = ndimage.binary_erosion(v.rgba()[..., 3] > 127, iterations=2)
    H, W = part.shape[:2]
    vx = np.floor(rect[0] + (np.arange(W) + 0.5) / scale).astype(int)
    vy = np.floor(rect[1] + (np.arange(H) + 0.5) / scale).astype(int)
    hidden = body[np.clip(vy, 0, v.h - 1)][:, np.clip(vx, 0, v.w - 1)]
    hidden &= ((vy >= 0) & (vy < v.h))[:, None] & ((vx >= 0) & (vx < v.w))[None, :]
    shows = drawn[y0:y1, x0:x1] & ~hidden
    ps = body_pieces(a["family"])
    return [{"pieces": [p["name"] for p in ps], "family": a["family"], "layer": "Hold", "metal": True, "drape": False,
             "area": int(round(drawn.sum() / scale ** 2)), "view": "front",
             "rect": rect, "base": "sprites/" + name, "texture": None, "behind": res(out_png), "held": True,
             "loops": loops_of(shows, (rect[0], rect[1]), scale, 2.0)}]


def import_cmd(_args):
    """Brings what's delivered and passes its checks into assets/equipment/ (plates/, pieces/, sprites/,
    pieces.json); run `godot --headless --path . --import` after it."""
    assets = [a for a in build() if os.path.exists(path(a["file"]))]
    ok = {a["id"] for a in assets if check_one(a).ok}
    bad = [a["id"] for a in assets if a["id"] not in ok]
    if bad:
        print(f"Left out, failing their checks: {', '.join(bad)}")
    for folder in (PIECES_OUT, PLATES_OUT, SPRITES_OUT):
        if os.path.isdir(folder):
            for dirpath, _, files in os.walk(folder):
                for f in files:
                    if f.endswith(".png") or f.endswith(".png.import"):
                        os.remove(os.path.join(dirpath, f))
    data = OrderedDict(about="Written by `python3 tools/equipment_pieces.py import` from the delivered art in "
                             "art/equipment/ (docs/art-handoff-equipment-pieces.md); don't edit by hand.",
                       plates=OrderedDict(), pages=OrderedDict(), art=[])
    plates_in = set()
    for a in assets:
        if a["class"] == "plate" and a["id"] in ok:
            b = BASES[a["base"]]
            name, entry = import_plate(b)
            data["plates"][name] = entry
            plates_in.add(b.name)
            print(f"  plate {name}: {entry['size'][0]}x{entry['size'][1]}, {len(entry['sites'])} sites, "
                  f"{len(entry['points'])} slot points")
    for region, panels in MAP["pages"].items():
        names = []
        for panel in panels:
            key = PLATE_NAMES.get((region, panel["view"]))
            if key and key in plates_in:
                names.append("plate_" + key)
        if names:
            data["pages"][region] = names
    n = 0
    for a in assets:
        if a["id"] not in ok:
            continue
        if a["class"] == "piece":
            got = import_piece(a, plates_in)
        elif a["class"] == "sprite":
            got = import_sprite(a)
        else:
            got = []
        data["art"].extend(got)
        n += len(got)
    with open(PIECES_JSON, "w") as f:
        json.dump(data, f, separators=(",", ":"))
    size = sum(os.path.getsize(os.path.join(d, f)) for folder in (PIECES_OUT, PLATES_OUT, SPRITES_OUT) if os.path.isdir(folder)
               for d, _, fs in os.walk(folder) for f in fs) // (1024 * 1024)
    print(f"Wrote assets/equipment/pieces.json: {len(data['plates'])} plates, {n} pictures of pieces "
          f"({size} MB of textures). Next: godot --headless --path . --import")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("spec")
    sub.add_parser("list")
    sub.add_parser("import")
    for name in ("inputs", "check", "sheet"):
        c = sub.add_parser(name)
        c.add_argument("ids", nargs="*")
        if name == "check":
            c.add_argument("--all", action="store_true", help="also list the files not delivered yet")
    args = p.parse_args()
    if args.cmd == "spec":
        spec_cmd(args)
    elif args.cmd == "list":
        list_cmd(args)
    elif args.cmd == "import":
        import_cmd(args)
    elif args.cmd == "inputs":
        inputs(args)
    elif args.cmd == "check":
        sys.exit(check(args)[1])
    else:
        sheet(args)


if __name__ == "__main__":
    main()
