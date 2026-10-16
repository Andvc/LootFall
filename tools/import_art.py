#!/usr/bin/env python3
"""Builds assets/ from the open-licensed source packs. Re-run after changing a mapping below.

Sources (all CC0, see CREDITS.md):
  - Superpowers Asset Packs by Pixel-boy / Sparklin Labs  https://github.com/sparklinlabs/superpowers-asset-packs
  - Kenney Font Package, Impact Sounds, RPG Audio, Casino Audio  https://kenney.nl (mirror: github.com/ETdoFresh/kenney.nl)

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
    "kenney_fontpackage", "kenney_impactsounds", "kenney_rpgaudio", "kenney_casinoaudio",
])

# Item key -> rpg-battle-system/item sprite number. Keys match ItemDef.Key in scripts/Core/Items.cs.
ITEMS = {
    "copper_pouch": 21, "silver_crowns": 20, "signet_ring": 38, "doubloons": 19,
    "moonstone_pendant": 39, "gilded_chalice": 26, "amethyst_signet": 37, "sunfire_diamond": 57,
    "hand_mirror": 29, "old_brandy": 61, "perfume_vial": 36, "levitation_potion": 12,
    "liquid_sunlight": 13, "phoenix_tear": 47,
    "rusty_dagger": 31, "soldiers_helm": 1, "engraved_rapier": 53, "orcish_scimitar": 67,
    "sun_shield": 30, "commander_gauntlet": 59, "drowned_king_helm": 4,
    "splintered_buckler": 28, "rusted_lantern": 17, "carved_idol": 60, "spiral_fossil": 75,
    "dragon_scale": 58,
    "love_letters": 25, "moth_eaten_cloak": 81, "merchants_ledger": 62, "golden_mantle": 76,
    "treasure_map": 22, "royal_deed": 23,
}

BACKGROUNDS = {"courtyard_night": "24", "cellar_night": "10"}

# Seller key -> Ninja Adventure faceset number. Keys match SellerDef.Key in scripts/Core/Sellers.cs.
PORTRAITS = {"hob": 22, "marta": 4, "vex": 9, "pip": 17, "gull": 14, "silas": 13, "penn": 19, "wren": 6}

# The sound vocabulary for shaking a chest: each material has a cheap and a fine variant, so a player can learn
# to hear quality. Group -> Kenney source files (pack folder, file stem, variants).
def _variants(pack, stem, count, start=0, digits=3, sep="_"):
    return [(pack, f"{stem}{sep}{i:0{digits}d}") for i in range(start, start + count)]

SOUNDS = {
    "jingle_cheap": [("kenney_casinoaudio", f"chipsCollide{i}") for i in range(1, 5)],
    "jingle_fine": [("kenney_rpgaudio", "handleCoins"), ("kenney_rpgaudio", "handleCoins2")],
    "ring": _variants("kenney_impactsounds", "impactBell_heavy", 5),
    "clink_cheap": _variants("kenney_impactsounds", "impactGlass_light", 5),
    "clink_fine": _variants("kenney_impactsounds", "impactGlass_heavy", 5),
    "clank_cheap": _variants("kenney_impactsounds", "impactTin_medium", 5),
    "clank_fine": _variants("kenney_impactsounds", "impactMetal_heavy", 5),
    "thud_cheap": _variants("kenney_impactsounds", "impactWood_medium", 5),
    "thud_fine": _variants("kenney_impactsounds", "impactWood_heavy", 5),
    "rustle_cheap": [("kenney_rpgaudio", f"cloth{i}") for i in range(1, 5)],
    "rustle_fine": [("kenney_rpgaudio", f"bookFlip{i}") for i in range(1, 4)],
    "rock": _variants("kenney_impactsounds", "impactMining", 5),
    "land": _variants("kenney_impactsounds", "impactPlank_medium", 5),
    "knock_solid": _variants("kenney_impactsounds", "impactWood_light", 5),
    "knock_hollow": _variants("kenney_impactsounds", "impactPlank_medium", 3),
    "pay": [("kenney_casinoaudio", f"chipsStack{i}") for i in range(1, 4)],
    "door_open": [("kenney_rpgaudio", "doorOpen_1"), ("kenney_rpgaudio", "doorOpen_2")],
    "door_close": [("kenney_rpgaudio", "doorClose_1"), ("kenney_rpgaudio", "doorClose_2")],
    "creak": [("kenney_rpgaudio", f"creak{i}") for i in range(1, 4)],
    "latch": [("kenney_rpgaudio", "metalLatch"), ("kenney_rpgaudio", "metalClick")],
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

    for key, number in ITEMS.items():
        img = Image.open(os.path.join(sp, f"rpg-battle-system/item/{number}.png")).convert("RGBA")
        save(img.crop(img.getbbox()), "sprites", "items", f"{key}.png")

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
