#!/usr/bin/env python3
"""LootFall's art pipeline, the repo half (docs/art-direction.md, "The asset pipeline").

  manifest   art/manifest.json: every asset the game uses or will use, generated from the game's own content (realms and
             monsters, item families, eras; via `progsim --manifest`), each with its target path, its sizes, its AI prompt
             (the style preamble and the template for its kind) and whether what's there now is a stand-in.
  import     takes new art from art/incoming/<kind>/<name>.png (generated on a flat #00ff00 key, or with a painted
             background and --rembg), mattes it, crops it, flattens its fills to a few flat steps, gives it the ink
             outline, fits it to its master size, checks it (bounding-box fill, light from the top left, legible at
             display size), writes it where the game reads it, marks it final in art/sources.json, and draws
             art/contact_sheet.png to review.
  sheet      a contact sheet of the assets as they are now.

Usage:
  python3 tools/art.py manifest
  python3 tools/art.py import [--rembg] [--flip] [--flatten=N] [ids...]
             (ids like monster/bloblet or icon/helm/t1; all of art/incoming/ by default)
  python3 tools/art.py sheet [monsters|icons]

Generation happens wherever the owner runs the models (their machine, or a GitHub Actions workflow with API keys as
secrets): drop the results into art/incoming/ named by id, then run import. Needs Pillow (and rembg for --rembg).
"""
import argparse
import json
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageStat

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from placeholders import INK, crop_to_alpha, foot_point, ink_outline  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "art")
ASSETS = os.path.join(ROOT, "assets")
KEY = (0, 255, 0)

STYLE = ("Inked cel illustration for a 2D fantasy game. One bold uniform dark outline (#15101f), flat colour fills with "
         "three shade steps, light from the top left, no gradients, no texture noise, no cast shadow, no text, no glow, no "
         "frame. Flat pure green background (#00ff00) filling the canvas edge to edge.")

BODY = {
    "Walker": "walks on its legs",
    "Hopper": "a squat, bouncing body",
    "Flyer": "flies, wings or a floating body clear of the ground",
    "Heavy": "a huge, heavy, slow body",
    "Slither": "a long body that slithers along the ground",
}

# The neutral materials each kind of item family is made of (no rarity colour, ever: code draws rarity).
MATERIALS = {
    "Weapon": "forged steel, leather grip", "Armor": "plate steel, leather straps", "Jewelry": "gold and a gemstone",
    "Accessory": "leather, brass and cloth", "Trinket": "carved bone, brass and stone", "Tattoo": "ink lines on parchment, a sigil",
    "Companion": "leather, cloth and fur", "Celestial": "pale starlight metal, clouds of light",
}

# The art tiers that follow the codex eras (docs/art-direction.md, "Items and icons").
TIERS = [
    ("t1", "Iron and Steel eras: plain forged metal, leather, rough cloth"),
    ("t2", "Silvered and Runed eras: fine steel with silver inlay, etched runes"),
    ("t3", "Mithril and Adamant eras: pale bright metal, gem settings, gilt trim"),
    ("t4", "Dragonbone and Starsteel eras: bone, scale and dark star-metal, glowing seams"),
    ("t5", "Voidforged eras and beyond: void glass, sunfire, astral filigree"),
]

KNIGHT_PARTS = ["head", "torso", "pelvis", "upper_arm_near", "forearm_near", "hand_near", "upper_arm_far", "forearm_far",
                "hand_far", "thigh_near", "shin_near", "foot_near", "thigh_far", "shin_far", "foot_far", "cape"]
GEAR_PIECES = ["sword", "shield", "helm", "breastplate", "tabard", "cloak", "pauldron", "gauntlet", "belt", "boot"]
BACKDROP_LAYERS = ["sky", "far", "ground", "front"]
# The effect textures the game draws with (scripts/View/Fx.cs, BattleView.cs, Reveal.cs, Panels.cs); rings, beams, rays
# and the boss gate are drawn in code. Greyscale: code tints them.
VFX = {
    "spark1": "a short teardrop spark, bright core", "spark2": "a thin streak spark for blows",
    "spark3": "a small blunt spark for monster swipes", "star1": "a four-pointed twinkle star",
    "star2": "a soft five-pointed star with a glow", "smoke1": "a soft round puff of smoke",
    "slash1": "a broad crescent sword slash", "slash2": "a thin crescent slash highlight",
    "glow": "a soft round glow, bright centre fading to nothing",
}

# Master sizes (px) and what they're shown at on the 1920x1080 canvas.
SPEC = {
    "monster": {"master": "2x the largest size shown, up to 1000 px tall", "display": "150-560 px tall", "height": 640},
    "guardian": {"master": "up to 1000 px tall", "display": "420-560 px tall", "height": 1000},
    "icon": {"master": "512 px square, object in the middle 80%", "display": "48-96 px", "size": 512},
    "knight": {"master": "parts sheet for a 560 px tall knight", "display": "280 px tall"},
    "gear": {"master": "on the part's pivot template, 2x", "display": "on the knight"},
    "backdrop": {"master": "3840 px wide, tileable", "display": "the 1920x640 lane"},
    "vfx": {"master": "512 px greyscale on black", "display": "lane overlays"},
}


def run_content():
    path = os.path.join(ART, "content.json")
    dll = os.path.join(ROOT, "tools/progsim/bin/Release/net8.0/progsim.dll")
    if not os.path.exists(dll):
        subprocess.run(["dotnet", "build", os.path.join(ROOT, "tools/progsim"), "-c", "Release"], check=True, stdout=subprocess.DEVNULL)
    subprocess.run(["dotnet", dll, f"--manifest={path}"], check=True)
    with open(path) as f:
        return json.load(f)


def load_sources():
    path = os.path.join(ART, "sources.json")
    if os.path.exists(path):
        with open(path) as f:
            return json.load(f)
    return {}


def save_sources(sources):
    with open(os.path.join(ART, "sources.json"), "w") as f:
        json.dump(sources, f, indent=1, sort_keys=True)


def manifest(_args):
    os.makedirs(ART, exist_ok=True)
    content = run_content()
    sources = load_sources()
    entries = []

    def add(id, kind, path, prompt, **extra):
        status = "final" if sources.get(id, {}).get("status") == "final" else ("placeholder" if os.path.exists(os.path.join(ROOT, path)) else "missing")
        entries.append({"id": id, "kind": kind, "path": path, "status": status, "master": SPEC[kind]["master"],
                        "display": SPEC[kind]["display"], "prompt": prompt, **extra})

    for realm in content["realms"]:
        where = f"{realm['name']} (realm {realm['era'] + 1}, the {content['eras'][min(realm['era'], len(content['eras']) - 1)]} era)"
        for m in realm["monsters"] + [realm["guardian"]]:
            guardian = m is realm["guardian"]
            kind = "guardian" if guardian else "monster"
            prompt = (f"{STYLE} Full-body side view of {m['name']}, a {'guardian boss' if guardian else 'monster'} of {where}: "
                      f"{BODY[m['body']]}. Facing left, standing on a flat ground line, whole body in frame with an 8% margin. "
                      "Chunky silhouette that reads in solid black. One saturated focus colour; everything else in muted, "
                      "natural materials.")
            add(f"monster/{m['key']}", kind, f"assets/monsters/{m['key']}.png", prompt, realm=realm["key"], body=m["body"], size=m["size"])
        for layer in BACKDROP_LAYERS:
            prompt = (f"Painterly fantasy landscape, {realm['name']}, the {layer} layer of a side-scrolling parallax scene, "
                      "horizontally tileable, soft edges, desaturated, low contrast, no characters, no text."
                      + (" Above the silhouette, flat pure green (#00ff00)." if layer != "sky" else ""))
            path = f"assets/backdrops/{realm['key']}.jpg" if layer == "far" else f"assets/backdrops/{realm['key']}_{layer}.png"
            add(f"backdrop/{realm['key']}/{layer}", "backdrop", path, prompt, realm=realm["key"])

    for fam in content["families"]:
        noun = fam["family"].replace("_", " ")
        for tier, materials in TIERS:
            prompt = (f"{STYLE} Game item icon: {noun} ({', '.join(fam['slots'][:3])}), made of {MATERIALS[fam['kind']]}; "
                      f"{materials}. Three-quarter view, tilted 30 degrees, centred, filling 80% of a square canvas. "
                      "Neutral colours only.")
            path = f"assets/icons/{fam['family']}.png" if tier == "t1" else f"assets/icons/{fam['family']}_{tier}.png"
            add(f"icon/{fam['family']}/{tier}", "icon", path, prompt, kind_of=fam["kind"], slots=fam["slots"])

    knight = (f"{STYLE} Character parts sheet: a stout knight about four heads tall, side view facing right, neutral pose. "
              "Head, torso, pelvis, both upper arms, forearms and hands, both thighs, shins and feet drawn separately on a "
              "grid, each whole, with joints overlapping by 15%.")
    for part in KNIGHT_PARTS:
        add(f"knight/{part}", "knight", f"assets/knight/{part}.png", knight)
    for piece in GEAR_PIECES:
        for tier, materials in TIERS:
            prompt = (f"{STYLE} {materials} {piece} for the knight, drawn as worn in side view facing right, on its part's "
                      "template. Neutral colours, with the trim to be tinted painted in pure magenta (#ff00ff).")
            add(f"gear/{piece}/{tier}", "gear", f"assets/gear/{piece}_{tier}.png", prompt)
    for v, shape in VFX.items():
        add(f"vfx/{v}", "vfx", f"assets/vfx/{v}.png", f"Game effect texture: {shape}. White to grey on pure black, centred, no colour, no background detail.")

    with open(os.path.join(ART, "manifest.json"), "w") as f:
        json.dump({"style": STYLE, "assets": entries}, f, indent=1)
    counts = {}
    for e in entries:
        counts.setdefault(e["kind"], {}).setdefault(e["status"], 0)
        counts[e["kind"]][e["status"]] += 1
    print(f"art/manifest.json: {len(entries)} assets")
    for kind, c in counts.items():
        print(f"  {kind:9s} " + ", ".join(f"{n} {s}" for s, n in sorted(c.items())))


# ---------------------------------------------------------------------------------------------------------------------
# Import
# ---------------------------------------------------------------------------------------------------------------------

def key_matte(im):
    """Alpha from the distance to the #00ff00 key, with the green spill taken off the edges."""
    import numpy as np
    a = np.asarray(im.convert("RGB"), dtype=np.float32)
    d = np.sqrt(((a - np.array(KEY, dtype=np.float32)) ** 2).sum(axis=2))
    alpha = np.clip((d - 60) / 70, 0, 1)
    spill = alpha < 0.98
    g = a[..., 1]
    a[..., 1] = np.where(spill, np.minimum(g, np.maximum(a[..., 0], a[..., 2])), g)
    out = np.dstack([a, alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def flatten(im, colours):
    """Cel fills: the colours quantized to a few flat steps (no dithering), the alpha kept as it was."""
    if colours <= 0:
        return im
    rgb = im.convert("RGB").quantize(colors=colours, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    rgb.putalpha(im.split()[3])
    return rgb


def checks(im, kind):
    """The scripted acceptance: fill, light from the top left, legible at display size. Returns a list of problems."""
    problems = []
    a = im.split()[3]
    box = a.getbbox()
    if box is None:
        return ["nothing opaque"]
    w, h = box[2] - box[0], box[3] - box[1]
    fill = sum(a.crop(box).histogram()[129:]) / max(1, w * h)
    if not 0.2 <= fill <= 0.97:
        problems.append(f"fills {fill:.0%} of its box")
    lum = im.convert("L")
    tl = ImageStat.Stat(lum.crop((box[0], box[1], box[0] + w // 2, box[1] + h // 2)), a.crop((box[0], box[1], box[0] + w // 2, box[1] + h // 2))).mean[0]
    br = ImageStat.Stat(lum.crop((box[0] + w // 2, box[1] + h // 2, box[2], box[3])), a.crop((box[0] + w // 2, box[1] + h // 2, box[2], box[3]))).mean[0]
    if tl + 6 < br:
        problems.append(f"lit from the bottom right? (top-left {tl:.0f}, bottom-right {br:.0f})")
    small = im.resize((48, max(1, int(48 * im.height / im.width))) if kind == "icon" else (int(150 * im.width / im.height), 150), Image.Resampling.LANCZOS)
    if ImageStat.Stat(small.convert("L"), small.split()[3]).stddev[0] < 18:
        problems.append("flat at display size")
    return problems


def import_art(args):
    incoming = os.path.join(ART, "incoming")
    if not os.path.isdir(incoming):
        print("Nothing in art/incoming/.")
        return
    with open(os.path.join(ART, "manifest.json")) as f:
        entries = {e["id"]: e for e in json.load(f)["assets"]}
    sources = load_sources()
    wanted = set(args.ids)
    done = []
    for root, _, files in os.walk(incoming):
        for name in sorted(files):
            if not name.lower().endswith((".png", ".jpg", ".webp")):
                continue
            id = os.path.relpath(os.path.join(root, os.path.splitext(name)[0]), incoming).replace(os.sep, "/")
            if wanted and id not in wanted:
                continue
            e = entries.get(id)
            if e is None:
                print(f"  {id}: not in the manifest, skipped")
                continue
            im = Image.open(os.path.join(root, name))
            if e["kind"] in ("backdrop",):
                out = os.path.join(ROOT, e["path"])
                im.convert("RGB").save(out, quality=90) if out.endswith(".jpg") else im.save(out)
                done.append((id, im.convert("RGBA"), []))
                continue
            if args.rembg:
                from rembg import new_session, remove
                im = remove(im.convert("RGB"), session=new_session("isnet-general-use"))
            elif im.mode != "RGBA" or im.getextrema()[3][0] == 255:
                im = key_matte(im)
            im = crop_to_alpha(im.convert("RGBA"), threshold=40)
            if args.flip:
                from PIL import ImageOps
                im = ImageOps.mirror(im)
            kind = e["kind"]
            if kind != "vfx":
                im = flatten(im, args.flatten)
            if kind in ("monster", "guardian"):
                h = SPEC[kind]["height"]
                if im.height > h:
                    im = im.resize((int(im.width * h / im.height), h), Image.Resampling.LANCZOS)
                im = ink_outline(im, max(4, round(im.height * 0.012)))
            elif kind == "icon":
                size = SPEC["icon"]["size"]
                inner = int(size * 0.8)
                k = inner / max(im.width, im.height)
                im = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.Resampling.LANCZOS)
                im = ink_outline(im, 16)
                canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
                canvas.alpha_composite(im, ((size - im.width) // 2, (size - im.height) // 2))
                im = canvas
            out = os.path.join(ROOT, e["path"])
            os.makedirs(os.path.dirname(out), exist_ok=True)
            im.save(out, optimize=True)
            if kind in ("monster", "guardian"):
                path = os.path.join(ASSETS, "monsters", "monsters.json")
                info = json.load(open(path)) if os.path.exists(path) else {}
                fx, fy = foot_point(im)
                key = id.split("/", 1)[1]
                info[key] = {"w": im.width, "h": im.height, "footX": fx, "footY": fy, "source": "ai"}
                json.dump(info, open(path, "w"), indent=1, sort_keys=True)
            problems = checks(im, kind)
            sources[id] = {"status": "final", "file": name, "problems": problems}
            done.append((id, im, problems))
            print(f"  {id}: {'ok' if not problems else '; '.join(problems)}")
    save_sources(sources)
    if done:
        contact(done, os.path.join(ART, "contact_sheet.png"))
        print(f"Imported {len(done)}; review art/contact_sheet.png, then `godot --headless --path . --import` and the bot's --shots.")


def contact(items, path):
    cols = 6
    cell = 220
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell, rows * (cell + 30)), (96, 102, 92, 255))
    d = ImageDraw.Draw(sheet)
    for i, (id, im, problems) in enumerate(items):
        t = im.copy()
        t.thumbnail((cell - 20, cell - 20))
        x, y = (i % cols) * cell, (i // cols) * (cell + 30)
        sheet.alpha_composite(t, (x + (cell - t.width) // 2, y + cell - 10 - t.height))
        d.text((x + 6, y + cell + 4), id[:34], fill=(255, 120, 120, 255) if problems else (255, 255, 255, 255))
    sheet.save(path)


def sheet(args):
    what = args.ids[0] if args.ids else "monsters"
    folder = os.path.join(ASSETS, what)
    items = [(f, Image.open(os.path.join(folder, f)).convert("RGBA"), []) for f in sorted(os.listdir(folder)) if f.endswith(".png")]
    contact(items, os.path.join(ART, f"sheet_{what}.png"))
    print(f"art/sheet_{what}.png: {len(items)}")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("command", choices=["manifest", "import", "sheet"])
    p.add_argument("ids", nargs="*")
    p.add_argument("--rembg", action="store_true", help="matte with rembg instead of the green key")
    p.add_argument("--flip", action="store_true", help="mirror (art drawn facing the wrong way)")
    p.add_argument("--flatten", type=int, default=48, help="quantize to this many flat colours (0 keeps them as drawn)")
    args = p.parse_args()
    os.makedirs(ART, exist_ok=True)
    {"manifest": manifest, "import": import_art, "sheet": sheet}[args.command](args)


if __name__ == "__main__":
    main()
