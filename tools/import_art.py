#!/usr/bin/env python3
"""Builds assets/ from the open-licensed source packs. Re-run after changing a mapping below.

Sources (all CC0, see CREDITS.md):
  - Superpowers Asset Packs by Pixel-boy / Sparklin Labs  https://github.com/sparklinlabs/superpowers-asset-packs
  - Kenney Font Package, Impact Sounds, RPG Audio, Casino Audio, Interface Sounds
    https://kenney.nl (mirror: github.com/ETdoFresh/kenney.nl)

Usage: python3 tools/import_art.py [--cache DIR]
Requires Pillow. Sources are cloned (partially) into the cache on first run.
"""
import argparse
import os
import shutil
import subprocess
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")

SUPERPOWERS = ("https://github.com/sparklinlabs/superpowers-asset-packs", [
    "rpg-battle-system/item", "rpg-battle-system/backgrounds", "rpg-battle-system/fx",
    "ninja-adventure/items", "ninja-adventure/characters/faceset", "LICENSE.txt",
])
KENNEY = ("https://github.com/ETdoFresh/kenney.nl", [
    "kenney_fontpackage", "kenney_impactsounds", "kenney_rpgaudio", "kenney_casinoaudio", "kenney_interfacesounds",
])

# Item key -> rpg-battle-system/item sprite number. Keys match ItemDef.Key in scripts/Core/Items.cs.
ITEMS = {
    "copper_purse": 21, "silver_crowns": 20, "signet_ring": 38, "doubloons": 19, "moonstone_pendant": 39,
    "sunfire_diamond": 57,
    "hand_mirror": 29, "old_brandy": 61, "perfume_vial": 36, "liquid_sunlight": 13, "phoenix_tear": 47,
    "rusty_dagger": 31, "soldiers_helm": 1, "engraved_rapier": 53, "commander_gauntlet": 59, "drowned_king_helm": 4,
    "splintered_buckler": 28, "rusted_lantern": 17, "carved_idol": 60, "spiral_fossil": 75, "dragon_scale": 58,
    "love_letters": 25, "moth_eaten_cloak": 81, "merchants_ledger": 62, "golden_mantle": 76, "treasure_map": 22,
    "royal_deed": 23,
}

# Items drawn as one sprite with another tucked in its corner: key -> (base, overlay).
# A purse of gold is the copper purse with a coin showing; through a keyhole it still looks like the plain purse.
COMPOSITE_ITEMS = {"gold_purse": (21, 19)}

BACKGROUNDS = {"courtyard_night": "24", "cellar_night": "10"}

# Seller key -> Ninja Adventure faceset number. Keys match SellerDef.Key in scripts/Core/Sellers.cs, plus Uncle Bram.
PORTRAITS = {"hob": 22, "marta": 4, "vex": 9, "pip": 17, "gull": 14, "silas": 13, "penn": 19, "wren": 6, "bram": 16}

# The sounds of things rattling in a chest. Every item has its own group (see Signature in scripts/Core/Items.cs),
# except deliberate sound-alikes, which share one: purse (copper/gold), vial (perfume/phoenix tear), paper
# (letters/map) and cloth (cloak/mantle). Within a material, bigger things get lower recordings. A royal deed
# rides in a scroll case, so it knocks rather than rustles.
# Group -> Kenney source files (pack folder, file stem).
def _variants(pack, stem, indices, digits=3, sep="_"):
    return [(pack, f"{stem}{sep}{i:0{digits}d}") for i in indices]

IMPACT, RPG, CASINO, UI = "kenney_impactsounds", "kenney_rpgaudio", "kenney_casinoaudio", "kenney_interfacesounds"

SOUNDS = {
    # Coins and jewelry
    "purse": [(RPG, "handleCoins")],
    "crowns": [(CASINO, "chipsHandle1"), (CASINO, "chipsHandle3")],
    "doubloons": [(CASINO, "chipsStack2"), (CASINO, "chipsStack4")],
    "ring": _variants(IMPACT, "impactMetal_light", [0, 1, 4]),
    "pendant": [(RPG, "beltHandle1"), (RPG, "beltHandle2")],
    "diamond": _variants(UI, "glass", [1, 2, 3]),
    # Glass
    "mirror": _variants(IMPACT, "impactGlass_light", range(5)),
    "brandy": _variants(IMPACT, "impactGlass_heavy", range(5)),
    "vial": _variants(IMPACT, "impactGlass_medium", range(5)),
    "sunlight": _variants(UI, "glass", [4]),
    # Arms and armor
    "dagger": _variants(IMPACT, "impactMetal_medium", range(5)),
    "helm": _variants(IMPACT, "impactTin_medium", range(5)),
    "rapier": [(RPG, "knifeSlice2")],
    "gauntlet": _variants(IMPACT, "impactPlate_medium", [0, 1, 2]),
    "kinghelm": _variants(IMPACT, "impactBell_heavy", [0, 1, 2]),
    # Wood and stone
    "buckler": _variants(IMPACT, "impactWood_medium", range(5)),
    "lantern": [(RPG, "metalPot2"), (RPG, "metalPot3")],
    "idol": [(CASINO, "dieThrow1"), (CASINO, "dieThrow3")],
    "fossil": _variants(IMPACT, "impactMining", range(5)),
    "dragonscale": _variants(IMPACT, "impactPlate_heavy", [1, 4]),
    # Paper and cloth
    "paper": [(CASINO, "cardShove1"), (CASINO, "cardShove2"), (CASINO, "cardShove4")],
    "cloth": [(RPG, "cloth1")],
    "ledger": [(RPG, "bookPlace1"), (RPG, "bookPlace2"), (RPG, "bookPlace3")],
    "deed": _variants(UI, "drop", [2, 3]),

    # Handling the chest
    "land": _variants(IMPACT, "impactPlank_medium", range(5)),
    "knock": _variants(IMPACT, "impactWood_light", range(5)),
    "scale_pan": _variants(IMPACT, "impactPlate_light", range(5)),
    "pay": [(CASINO, f"chipsStack{i}") for i in range(1, 4)],
    "door_open": [(RPG, "doorOpen_1"), (RPG, "doorOpen_2")],
    "door_close": [(RPG, "doorClose_1"), (RPG, "doorClose_2")],
    "creak": [(RPG, f"creak{i}") for i in range(1, 4)],
    "latch": [(RPG, "metalLatch"), (RPG, "metalClick")],
    "page": [(RPG, "bookFlip1"), (RPG, "bookFlip3")],
    "book": [(RPG, "bookOpen")],
}

# The Ninja Adventure chest is 2 frames of 16x14: closed, then open. Its body palette (dark, main,
# highlight, shade) is swapped per lot provenance; the gold rim and orange fittings stay.
CHEST_BASE = ["#711335", "#dc2728", "#ff5733", "#c03a24"]
CHEST_SKINS = {
    "red": CHEST_BASE,
    "blue": ["#2b2a6e", "#457ad3", "#5cbad7", "#3b5bb0"],
    "purple": ["#3d1f5c", "#8a4fd1", "#b88cff", "#6a3aa8"],
    "wood": ["#4a2a14", "#8a5a2b", "#b07a40", "#6e4520"],
    "green": ["#23402a", "#4f8a3a", "#7fc25a", "#3b6e2e"],
}
LID_ROWS = (2, 9)        # rows 2..8 of the closed frame
BODY_ROWS = (9, 14)      # rows 9..13 of the closed frame (gold band, lock, body)
OPEN_BODY_ROWS = (4, 14)  # rows 4..13 of the open frame (rim, interior, band, body)

UI_SCALE = 3


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def fetch(url, paths, cache):
    dest = os.path.join(cache, url.rstrip("/").split("/")[-1])
    if not os.path.isdir(dest):
        subprocess.run(["git", "clone", "-q", "--filter=blob:none", "--no-checkout", "--depth", "1", url, dest], check=True)
    subprocess.run(["git", "-C", dest, "checkout", "HEAD", "--", *paths], check=True)
    return dest


def save(image, *parts):
    path = os.path.join(ASSETS, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    return path


def build_chests(src):
    sheet = Image.open(os.path.join(src, "ninja-adventure/items/big-treasure-chest.png")).convert("RGBA")
    closed, opened = sheet.crop((0, 0, 16, 14)), sheet.crop((16, 0, 32, 14))
    base = [hex_rgb(c) for c in CHEST_BASE]
    for skin, colors in CHEST_SKINS.items():
        swap = dict(zip(base, [hex_rgb(c) for c in colors]))

        def recolor(img):
            out = img.copy()
            px = out.load()
            for y in range(out.height):
                for x in range(out.width):
                    r, g, b, a = px[x, y]
                    if a and (r, g, b) in swap:
                        px[x, y] = (*swap[(r, g, b)], a)
            return out

        save(recolor(closed.crop((0, LID_ROWS[0], 16, LID_ROWS[1]))), "sprites", "chest", f"{skin}_lid.png")
        save(recolor(closed.crop((0, BODY_ROWS[0], 16, BODY_ROWS[1]))), "sprites", "chest", f"{skin}_body.png")
        save(recolor(opened.crop((0, OPEN_BODY_ROWS[0], 16, OPEN_BODY_ROWS[1]))), "sprites", "chest", f"{skin}_open.png")


def build_ui():
    """Pixel-art 9-slice frames, pre-scaled so one art pixel is UI_SCALE screen pixels."""
    k, hi, fill, lo = (2, 2, 2, 255), (59, 49, 80, 255), (29, 24, 40, 255), (18, 14, 26, 255)
    white, light, mid, dark = (255, 255, 255, 255), (235, 235, 235, 255), (190, 190, 190, 255), (130, 130, 130, 255)
    clear = (0, 0, 0, 0)

    def frame(fill_color, top, bottom, border=None, size=8):
        img = Image.new("RGBA", (size, size), fill_color)
        px = img.load()
        for i in range(size):
            px[i, 0] = px[i, size - 1] = px[0, i] = px[size - 1, i] = k
        for i in range(1, size - 1):
            px[i, 1], px[1, i] = top, top
            px[i, size - 2], px[size - 2, i] = bottom, bottom
        if border:
            for i in range(1, size - 1):
                px[i, 1] = px[i, size - 2] = px[1, i] = px[size - 2, i] = border
        for x, y in [(0, 0), (size - 1, 0), (0, size - 1), (size - 1, size - 1)]:
            px[x, y] = clear
        return img.resize((size * UI_SCALE, size * UI_SCALE), Image.NEAREST)

    save(frame(fill, hi, lo), "ui", "panel.png")
    save(frame(clear, white, white, border=white), "ui", "frame.png")
    save(frame(mid, light, dark), "ui", "button.png")
    save(frame(dark, dark, light), "ui", "button_pressed.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", default=os.path.expanduser("~/.cache/lootfall-art"))
    args = parser.parse_args()
    os.makedirs(args.cache, exist_ok=True)

    sp = fetch(*SUPERPOWERS, args.cache)
    kenney = fetch(*KENNEY, args.cache)

    def item_sprite(number):
        img = Image.open(os.path.join(sp, f"rpg-battle-system/item/{number}.png")).convert("RGBA")
        return img.crop(img.getbbox())

    # Rebuilt from scratch so renamed or dropped items and sounds don't linger.
    for folder in (os.path.join(ASSETS, "sprites", "items"), os.path.join(ASSETS, "audio")):
        shutil.rmtree(folder, ignore_errors=True)

    for key, number in ITEMS.items():
        save(item_sprite(number), "sprites", "items", f"{key}.png")
    for key, (base_number, overlay_number) in COMPOSITE_ITEMS.items():
        base, overlay = item_sprite(base_number), item_sprite(overlay_number)
        out = Image.new("RGBA", (base.width + overlay.width // 2, base.height + overlay.height // 3), (0, 0, 0, 0))
        out.alpha_composite(base, (0, 0))
        out.alpha_composite(overlay, (out.width - overlay.width, out.height - overlay.height))
        save(out, "sprites", "items", f"{key}.png")

    for name, number in BACKGROUNDS.items():
        save(Image.open(os.path.join(sp, f"rpg-battle-system/backgrounds/{number}.png")).convert("RGBA"), "backgrounds", f"{name}.png")

    save(Image.open(os.path.join(sp, "rpg-battle-system/fx/1.png")).convert("RGBA"), "sprites", "fx", "burst.png")

    for key, number in PORTRAITS.items():
        save(Image.open(os.path.join(sp, f"ninja-adventure/characters/faceset/{number}.png")).convert("RGBA"), "sprites", "sellers", f"{key}.png")

    for group, files in SOUNDS.items():
        out = os.path.join(ASSETS, "audio", group)
        os.makedirs(out, exist_ok=True)
        for i, (pack, stem) in enumerate(files):
            shutil.copy(os.path.join(kenney, pack, "Audio", f"{stem}.ogg"), os.path.join(out, f"{i}.ogg"))

    build_chests(sp)
    build_ui()

    fonts = os.path.join(ASSETS, "fonts")
    os.makedirs(fonts, exist_ok=True)
    shutil.copy(os.path.join(kenney, "kenney_fontpackage/Fonts/Kenney Mini.ttf"), os.path.join(fonts, "kenney_mini.ttf"))

    count = sum(len(files) for _, _, files in os.walk(ASSETS))
    print(f"Wrote {count} files to {ASSETS}")


if __name__ == "__main__":
    sys.exit(main())
