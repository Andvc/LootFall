#!/usr/bin/env python3
"""The equipment screen's art: the handoff spec, and checks for what's delivered (docs/art-handoff-equipment.md).

  spec    writes art/equipment/handoff.json: every file the equipment screen needs, its size, framing, prompt, the
          references to generate it from and the checks it must pass, with the body's sites and slots (from
          art/content.json, which `python3 tools/art.py manifest` refreshes).
  check   checks the files in art/equipment/ against the spec: size, background, framing, symmetry, the ink outline,
          the back lining up with the front, the views on a page kept apart, a backdrop dark and quiet enough, the glyph
          grid. Exit code 1 if any file fails. Run it before uploading.
  sheet   art/equipment/review.png: the delivered files side by side, each marked with its result.

Usage:
  python3 tools/equipment_art.py spec
  python3 tools/equipment_art.py check [ids...]
  python3 tools/equipment_art.py sheet

Needs Pillow and numpy.
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DROP = os.path.join(ROOT, "art", "equipment")
KEY = (0, 255, 0)

STYLE = ("Inked cel illustration for a 2D fantasy game's equipment screen. One clean, uniform, dark outline (#15101f) "
         "around every shape, thinner inner lines in the same ink, flat colour fills with exactly three tone steps per "
         "material (shadow, mid, light), light from the top left at 45 degrees, no gradients, no texture noise, no cast "
         "shadows, no glow, no text, no watermark, no frame, no border.")
BACKGROUND = ("Background: perfectly flat pure green #00ff00 from edge to edge (or real transparency), with no floor, no "
              "ground shadow and nothing else on it.")
KNIGHT = ("The same man every time: a human knight-at-arms in his early thirties, athletic and strong but not bulky, about "
          "seven heads tall (realistic heroic proportions), warm medium skin (shadow #9c5f48, mid #d08b6a, light #f0b693), "
          "dark brown hair pulled back into one braid that hangs behind his head so both ears are fully visible, a short "
          "full beard, a calm neutral expression with the eyes open and looking straight at the viewer, no scars, no "
          "tattoos, no jewellery, no piercings. He wears only plain undyed linen braies (short drawers ending at "
          "mid-thigh; shadow #8f8270, mid #b9ad9b, light #ddd3c4): no armour, no shirt, no shoes, no belt, no weapon.")

FRONT = (f"{STYLE} Full-body reference figure for an equipment diagram, orthographic front view, perfectly symmetric pose. "
         f"{KNIGHT} He stands straight, facing the viewer, feet shoulder-width apart and turned very slightly outward, all ten "
         "toes visible. His arms hang straight and away from the body at about 25 degrees, so no part of an arm touches "
         "the torso or the hips; the hands are open and flat with all five fingers spread apart, and the backs of the "
         "hands face the viewer (palms turned backward), every fingernail visible. The anatomy reads clearly as flat "
         "shapes: collarbones, pectorals, the breastbone line, the ribs at the sides, the solar plexus, the stomach "
         "muscles, the navel, the flanks, hip bones, kneecaps, shins and ankle bones. The canvas is 2160 by 3840 pixels: "
         "the figure is centred on the vertical middle line, the top of his hair is 380 px from the top, the soles of "
         f"his feet are 3600 px from the top, and nothing is cut off. {BACKGROUND}")
BACK = (f"{STYLE} The same man as in the reference image, with identical proportions, pose, braid, braies, skin colours "
        "and line style, seen from directly behind (orthographic back view), on the same 2160 by 3840 canvas with the "
        "same framing, so that his silhouette mirrored left to right lines up with the reference. Clearly drawn as flat "
        "shapes: the braid hanging down the back of the head, the nape, both shoulder blades, the spine, the sides of the "
        "back, the small of the back, the backs of the arms and elbows, the palms (which face the viewer from behind), "
        "the backs of the knees, the calves and the heels. "
        f"{BACKGROUND}")
HEAD = (f"{STYLE} Close-up reference sheet of the head and neck of the same man as in the reference image, at large "
        "size, three views side by side on one canvas, each centred in its own third with clear empty space between "
        "them and at the same scale: on the left his right profile, in the middle the front view, on the right his left "
        "profile. Each view runs from the top of the hair to the base of the neck, where it ends in a clean straight "
        "cut. The ears are drawn in full in the profiles (lobe, rim and upper tip clearly separate), the braid hangs "
        "behind, the eyebrows, eyes, nose with both nostrils, lips (closed) and the short beard are crisp. Neutral "
        f"expression, eyes open. {BACKGROUND}")


def hand(side):
    thumb_back, thumb_palm = ("left", "right") if side == "right" else ("right", "left")
    return (f"{STYLE} Close-up reference sheet of the {side} hand of the same man as in the reference image, two views side "
            "by side on one canvas at the same large scale, each centred in its half with clear empty space between "
            f"them: on the left the back of the {side} hand, on the right its palm. In both the hand is open and flat, "
            f"fingers spread apart and pointing straight up, the thumb on the {thumb_back} in the back view and on the "
            f"{thumb_palm} in the palm view. Knuckles, every finger joint and every fingernail are crisp; the wrist and the "
            "lower part of the forearm are included and end in a clean straight cut at the bottom. "
            f"{BACKGROUND}")


def foot(side):
    big_top, big_sole = ("left", "right") if side == "right" else ("right", "left")
    return (f"{STYLE} Close-up reference sheet of the {side} foot of the same man as in the reference image, bare, two "
            "views side by side on one canvas at the same large scale, each centred in its half with clear empty space "
            f"between them: on the left the top of the {side} foot seen from directly above, on the right its sole seen "
            "from directly below. In both the toes point straight up the canvas and are spread slightly apart, with "
            f"the big toe on the {big_top} in the top view and on the {big_sole} in the sole view; every toe and toenail is "
            "crisp; the ankle is included and ends in a clean straight cut at the bottom. "
            f"{BACKGROUND}")


STAGE = ("Painted background for a 2D fantasy game's equipment screen: the inside of a dim armourer's fitting room at "
         "night, seen straight on. Stone walls, a tall arched window with cold moonlight on the far left, armour stands "
         "and racks of weapons and banners as soft shapes far back at the sides, warm candlelight from the top left. "
         "Painterly and soft-edged, desaturated (saturation at most 35%), dark (values between 5% and 40%), low "
         "contrast, with a calm, almost empty darker middle where a figure will stand in front of it, and a soft "
         "vignette. No people, no figure, no mannequin in the middle, no text, no frame. 2560 by 1920 pixels.")
BOARD = ("Painted background seen from directly above: an armourer's workbench, a dark worn leather work mat filling the "
         "middle, a few tools (pliers, a small hammer, a jeweller's loupe, rivets, a whetstone) and scraps of leather "
         "only at the edges, warm lamplight from the top left. Painterly and soft-edged, desaturated (saturation at most "
         "35%), dark (values between 5% and 40%), low contrast, the middle empty and calm because a piece and its slots "
         "are drawn on top of it. No text, no frame. 2560 by 1920 pixels.")
GLYPHS = [
    ("skin", "a drop of ink with a small swirl inside (ink on the skin: tattoos, sigils, kohl)"),
    ("under", "a quilted padded square with stitched diamond lines (cloth worn under: shirts, gloves, socks)"),
    ("mail", "four interlinked rings of chain mail"),
    ("armor", "a curved plate of armour with two rivets (plate armour)"),
    ("over", "a hanging cloak with a clasp at the top (worn over everything: cloaks, tabards, hoods)"),
    ("adorn", "a finger ring with a cut gemstone (jewellery)"),
    ("hold", "a sword, point up, slightly diagonal (held: weapons, shields, an orb)"),
    ("graft", "a curved horn (grown from the body: horns, wings, a tail, gills)"),
    ("beyond", "a four-pointed star inside a thin orbit ring (around the body: aura, orbiting stones, the heavens)"),
    ("mod", "a round socket holding a small gem, with three prongs (a piece's own slot: gems, runes, fittings)"),
]
GLYPH_SHEET = ("A sheet of ten simple, bold game interface glyphs, solid white silhouettes with no outline, no shading and "
               "no colour, each centred in its own cell of an invisible grid of five columns and two rows, each filling "
               "about 60% of its cell and never touching another, readable when shrunk to 16 pixels. In reading order: "
               + "; ".join(f"{i + 1}. {d}" for i, (_, d) in enumerate(GLYPHS)) + ". " + BACKGROUND + " 2560 by 1024 pixels.")

# Every file, as the handoff lists it (docs/art-handoff-equipment.md has the same in words).
ASSETS = [
    {"id": "figure_front", "kind": "figure", "size": [2160, 3840], "refs": [],
     "use": "The knight as the equipment screen shows him from the front: the whole-body view, the zoomed regions of the "
            "head, torso, arms, waist and legs, the main screen's small figure, and the base every region and slot is "
            "mapped on.", "prompt": FRONT},
    {"id": "figure_back", "kind": "figure", "size": [2160, 3840], "refs": ["figure_front.png"], "mirror_of": "figure_front",
     "use": "The same figure turned round: the back, the back of the head, the backs of the limbs and the palms.",
     "prompt": BACK},
    {"id": "head_page", "kind": "page", "size": [3520, 2352], "views": 3, "refs": ["figure_front.png"],
     "use": "The head's close-up, shown when the camera zooms into the head: the face, the ears from the side, the hair.",
     "prompt": HEAD},
    {"id": "hand_right_page", "kind": "page", "size": [3520, 2352], "views": 2, "refs": ["figure_front.png"],
     "use": "The right hand's close-up: rings, knuckle rings and fingertips, the glove, plate and jewel on the back of the "
            "hand, the palm.", "prompt": hand("right")},
    {"id": "hand_left_page", "kind": "page", "size": [3520, 2352], "views": 2, "refs": ["figure_front.png", "hand_right_page.png"],
     "use": "The left hand's close-up, as the right's.", "prompt": hand("left")},
    {"id": "foot_right_page", "kind": "page", "size": [3520, 2352], "views": 2, "refs": ["figure_front.png"],
     "use": "The right foot's close-up: the five toe rings, the toenails, the top of the foot and the sole.",
     "prompt": foot("right")},
    {"id": "foot_left_page", "kind": "page", "size": [3520, 2352], "views": 2, "refs": ["figure_front.png", "foot_right_page.png"],
     "use": "The left foot's close-up, as the right's.", "prompt": foot("left")},
    {"id": "stage_backdrop", "kind": "backdrop", "size": [2560, 1920], "refs": [],
     "use": "Behind the figure on the equipment screen; it stays still while the camera zooms into the figure.",
     "prompt": STAGE},
    {"id": "board_backdrop", "kind": "backdrop", "size": [2560, 1920], "refs": [],
     "use": "Behind a piece and its own slots (a helm with its crest, visor and gem sockets).", "prompt": BOARD},
    {"id": "slot_glyphs", "kind": "glyphs", "size": [2560, 1024], "grid": [5, 2], "refs": [],
     "glyphs": [g for g, _ in GLYPHS],
     "use": "One glyph for each kind of slot, drawn inside the slot markers on the figure (tinted by code).",
     "prompt": GLYPH_SHEET},
]

# Figure framing (px on the 2160x3840 canvas) and the checks' limits.
FIGURE = {"centre_x": 1080, "top": 380, "bottom": 3600, "tolerance": 48, "max_width": 1900}
LIMITS = {"border_key": 0.97, "symmetry": 0.86, "back_fit": 0.85, "outline": 0.75, "spill": 0.004,
          "backdrop_saturation": 0.40, "backdrop_mean": 0.36, "backdrop_p95": 0.75, "backdrop_centre_std": 0.13}


def path_of(a):
    return os.path.join(DROP, a["id"] + ".png")


def use_dir(path):
    """Checks the files in another folder (to try the checks on test images)."""
    global DROP
    if path:
        DROP = os.path.abspath(path)


# ---------------------------------------------------------------------------------------------------------------- spec

def spec(_args):
    content = os.path.join(ROOT, "art", "content.json")
    sites = []
    if os.path.exists(content):
        with open(content) as f:
            sites = json.load(f).get("sites", [])
    out = {
        "about": "The equipment screen's art (docs/art-handoff-equipment.md). Upload each file to art/equipment/<id>.png "
                 "and run `python3 tools/equipment_art.py check` before uploading.",
        "style": STYLE, "knight": KNIGHT, "figure_framing": FIGURE, "limits": LIMITS,
        "assets": [dict(a, file=f"art/equipment/{a['id']}.png") for a in ASSETS],
        "sites": sites,
    }
    os.makedirs(DROP, exist_ok=True)
    with open(os.path.join(DROP, "handoff.json"), "w") as f:
        json.dump(out, f, indent=2)
    print(f"Wrote art/equipment/handoff.json: {len(ASSETS)} files, {len(sites)} body sites, "
          f"{sum(len(s['slots']) for s in sites)} slots")


# ---------------------------------------------------------------------------------------------------------------- checks

def load(path):
    im = Image.open(path)
    rgba = np.asarray(im.convert("RGBA")).astype(np.int16)
    return im, rgba


def subject(rgba):
    """What isn't background: alpha where the file has transparency, otherwise everything off the green key."""
    a = rgba[..., 3]
    if a.min() < 250:
        return a > 128, "alpha"
    r, g, b = rgba[..., 0], rgba[..., 1], rgba[..., 2]
    key = (g > 190) & (r < 90) & (b < 90)
    return ~key, "green key"


def border(mask, k=6):
    return np.concatenate([mask[:k].ravel(), mask[-k:].ravel(), mask[:, :k].ravel(), mask[:, -k:].ravel()])


def erode(mask, n):
    m = mask.copy()
    for _ in range(n):
        m = m & np.roll(m, 1, 0) & np.roll(m, -1, 0) & np.roll(m, 1, 1) & np.roll(m, -1, 1)
    return m


def bbox(mask, min_run=3):
    rows = np.where(mask.sum(1) >= min_run)[0]
    cols = np.where(mask.sum(0) >= min_run)[0]
    if len(rows) == 0:
        return None
    return int(cols[0]), int(rows[0]), int(cols[-1]), int(rows[-1])


def iou(a, b):
    inter = np.logical_and(a, b).sum()
    union = np.logical_or(a, b).sum()
    return inter / max(1, union)


def luminance(rgba):
    return (0.2126 * rgba[..., 0] + 0.7152 * rgba[..., 1] + 0.0722 * rgba[..., 2]) / 255.0


def outline_share(rgba, mask):
    """How much of the subject's edge is dark ink: the pixels just inside the silhouette."""
    edge = mask & ~erode(mask, 3)
    if edge.sum() == 0:
        return 0.0
    return float((luminance(rgba)[edge] < 0.28).mean())


def spill(rgba, mask):
    r, g, b = rgba[..., 0], rgba[..., 1], rgba[..., 2]
    green = (g > r + 60) & (g > b + 60)
    return float((green & erode(mask, 2)).sum() / max(1, mask.sum()))


class Result:
    def __init__(self, aid):
        self.id = aid
        self.lines = []
        self.ok = True

    def add(self, name, passed, detail):
        self.lines.append((name, passed, detail))
        self.ok &= bool(passed)


def check_common(a, r, im, rgba):
    w, h = a["size"]
    r.add("size", im.size == (w, h), f"{im.size[0]}x{im.size[1]} (want {w}x{h})")
    r.add("format", im.format == "PNG" and im.mode in ("RGB", "RGBA"), f"{im.format} {im.mode}")


def check_figure(a, r, rgba, masks):
    mask, how = subject(rgba)
    masks[a["id"]] = mask
    r.add("background", border(~mask).mean() >= LIMITS["border_key"], f"{border(~mask).mean():.1%} of the border is background ({how})")
    box = bbox(mask)
    if box is None:
        r.add("figure", False, "nothing found on the canvas")
        return
    x0, y0, x1, y1 = box
    cx, tol = (x0 + x1) / 2, FIGURE["tolerance"]
    r.add("centred", abs(cx - FIGURE["centre_x"]) <= tol, f"middle at x {cx:.0f} (want {FIGURE['centre_x']} +-{tol})")
    r.add("top of the head", abs(y0 - FIGURE["top"]) <= tol, f"y {y0} (want {FIGURE['top']} +-{tol})")
    r.add("soles", abs(y1 - FIGURE["bottom"]) <= tol, f"y {y1} (want {FIGURE['bottom']} +-{tol})")
    r.add("width", x1 - x0 <= FIGURE["max_width"] and x0 > 20 and x1 < rgba.shape[1] - 20, f"x {x0} to {x1}")
    sym = iou(mask, mask[:, ::-1])
    r.add("symmetric pose", sym >= LIMITS["symmetry"], f"left and right halves overlap {sym:.0%} when mirrored")
    ink = outline_share(rgba, mask)
    r.add("ink outline", ink >= LIMITS["outline"], f"{ink:.0%} of the silhouette's edge is dark ink")
    sp = spill(rgba, mask)
    r.add("no key spill", sp <= LIMITS["spill"], f"{sp:.2%} of the figure is key green")
    if a.get("mirror_of") and a["mirror_of"] in masks:
        fit = iou(masks[a["mirror_of"]], mask[:, ::-1])
        r.add("lines up with the front", fit >= LIMITS["back_fit"], f"mirrored, overlaps the front's silhouette {fit:.0%}")


def check_page(a, r, rgba):
    mask, how = subject(rgba)
    n = a["views"]
    r.add("background", border(~mask).mean() >= LIMITS["border_key"], f"{border(~mask).mean():.1%} of the border is background ({how})")
    w = rgba.shape[1]
    # Each view in its own part, the gaps between them empty.
    for i in range(1, n):
        gx = int(w * i / n)
        gap = mask[:, gx - 30:gx + 30].mean()
        r.add(f"gap {i}", gap < 0.01, f"{gap:.1%} drawn in the gap at x {gx}")
    for i in range(n):
        part = mask[:, int(w * i / n):int(w * (i + 1) / n)]
        fill = part.mean()
        box = bbox(part)
        tall = (box[3] - box[1]) / rgba.shape[0] if box else 0
        r.add(f"view {i + 1}", 0.08 <= fill <= 0.75 and tall >= 0.6, f"fills {fill:.0%} of its part, {tall:.0%} of the height")
    ink = outline_share(rgba, mask)
    r.add("ink outline", ink >= LIMITS["outline"], f"{ink:.0%} of the edges are dark ink")
    sp = spill(rgba, mask)
    r.add("no key spill", sp <= LIMITS["spill"], f"{sp:.2%} key green")


def check_backdrop(a, r, rgba):
    rgb = rgba[..., :3].astype(np.float32) / 255
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    lum = luminance(rgba)
    h, w = lum.shape
    centre = lum[h // 4:3 * h // 4, w // 4:3 * w // 4]
    r.add("opaque", rgba[..., 3].min() >= 250, "no transparency")
    r.add("muted", sat.mean() <= LIMITS["backdrop_saturation"], f"mean saturation {sat.mean():.0%} (at most {LIMITS['backdrop_saturation']:.0%})")
    r.add("dark", lum.mean() <= LIMITS["backdrop_mean"] and np.percentile(lum, 95) <= LIMITS["backdrop_p95"],
          f"mean value {lum.mean():.0%}, brightest 5% from {np.percentile(lum, 95):.0%}")
    r.add("quiet middle", centre.std() <= LIMITS["backdrop_centre_std"], f"the middle's contrast (std) {centre.std():.2f}")


def check_glyphs(a, r, rgba):
    mask, how = subject(rgba)
    cols, rows = a["grid"]
    h, w = mask.shape
    cw, ch = w // cols, h // rows
    lum = luminance(rgba)
    for i, name in enumerate(a["glyphs"]):
        cx, cy = i % cols, i // cols
        cell = mask[cy * ch:(cy + 1) * ch, cx * cw:(cx + 1) * cw]
        fill = cell.mean()
        clear = not (cell[:8].any() or cell[-8:].any() or cell[:, :8].any() or cell[:, -8:].any())
        light = lum[cy * ch:(cy + 1) * ch, cx * cw:(cx + 1) * cw][cell].mean() if cell.any() else 0
        r.add(f"glyph {name}", 0.04 <= fill <= 0.6 and clear and light >= 0.75,
              f"fills {fill:.0%} of its cell, {'clear of' if clear else 'touching'} its edges, brightness {light:.0%}")


def check(args):
    wanted = set(args.ids)
    results, masks = [], {}
    for a in ASSETS:
        if wanted and a["id"] not in wanted:
            continue
        r = Result(a["id"])
        p = path_of(a)
        if not os.path.exists(p):
            r.add("delivered", False, f"no {os.path.relpath(p, ROOT)}")
            results.append(r)
            continue
        im, rgba = load(p)
        check_common(a, r, im, rgba)
        if im.size == tuple(a["size"]):
            {"figure": lambda: check_figure(a, r, rgba, masks), "page": lambda: check_page(a, r, rgba),
             "backdrop": lambda: check_backdrop(a, r, rgba), "glyphs": lambda: check_glyphs(a, r, rgba)}[a["kind"]]()
        results.append(r)
    for r in results:
        print(f"{'PASS' if r.ok else 'FAIL'}  {r.id}")
        for name, passed, detail in r.lines:
            print(f"   {'ok ' if passed else 'NO '} {name}: {detail}")
    bad = [r.id for r in results if not r.ok]
    print(f"\n{len(results) - len(bad)} of {len(results)} pass" + (f"; fix {', '.join(bad)}" if bad else ""))
    return results, 1 if bad else 0


def sheet(args):
    results, _ = check(argparse.Namespace(ids=[]))
    status = {r.id: r.ok for r in results}
    tiles = []
    for a in ASSETS:
        p = path_of(a)
        im = Image.open(p).convert("RGBA") if os.path.exists(p) else Image.new("RGBA", tuple(a["size"]), (40, 34, 50, 255))
        im.thumbnail((420, 420))
        tile = Image.new("RGBA", (440, 470), (24, 20, 31, 255))
        tile.alpha_composite(im, ((440 - im.width) // 2, 10))
        d = ImageDraw.Draw(tile)
        ok = status.get(a["id"])
        d.text((12, 440), f"{a['id']}  {'PASS' if ok else 'FAIL' if os.path.exists(p) else 'missing'}",
               fill=(120, 230, 120) if ok else (240, 110, 110))
        tiles.append(tile)
    out = Image.new("RGBA", (440 * 5, 470 * 2), (14, 11, 19, 255))
    for i, t in enumerate(tiles):
        out.alpha_composite(t, (440 * (i % 5), 470 * (i // 5)))
    out.save(os.path.join(DROP, "review.png"))
    print("Wrote art/equipment/review.png")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("spec")
    c = sub.add_parser("check")
    c.add_argument("ids", nargs="*")
    c.add_argument("--dir", help="check the files in this folder instead of art/equipment")
    sh = sub.add_parser("sheet")
    sh.add_argument("--dir", help="the files in this folder instead of art/equipment")
    args = p.parse_args()
    use_dir(getattr(args, "dir", None))
    if args.cmd == "spec":
        spec(args)
    elif args.cmd == "check":
        sys.exit(check(args)[1])
    else:
        sheet(args)


if __name__ == "__main__":
    main()
