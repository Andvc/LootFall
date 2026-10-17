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

# Item sprite -> rpg-battle-system/item sprite number. Names match the sprites in scripts/Core/Items.cs: one per kind of
# thing per seller tier, a magic one and (for coins and jewelry) a crook's fake.
ITEMS = {
    # Coins
    "copper_pouch": 21, "silver_crowns": 20, "gold_doubloons": 19,
    # Metal
    "rusty_knife": 31, "soldiers_helm": 1, "steel_sword": 78, "knights_gauntlet": 59, "royal_rapier": 53, "rune_blade": 67,
    # Cloth
    "wool_coat": 43, "golden_mantle": 76, "royal_robe": 50, "wizard_hat": 44,
    # Bottles
    "ale_bottle": 61, "honey_jar": 63, "ambrosia": 47, "mana_potion": 12,
    # Paper
    "love_letters": 25, "merchants_ledger": 62, "treasure_map": 22, "illuminated_codex": 42, "spell_scroll": 23,
    # Jewelry
    "brass_ring": 38, "silver_locket": 36, "gold_ring": 68, "sunfire_diamond": 57, "jeweled_chalice": 26,
    "moonstone_pendant": 39,
}

# Sprites greyed toward iron or dust: key -> (sprite number, how much grey, 0..1).
# The crook's fakes are the real thing with the gilt worn thin; the moth-eaten cloak is a faded cape.
WORN_ITEMS = {"gilded_slugs": (19, 0.55), "paste_diamond": (57, 0.6), "moth_eaten_cloak": (81, 0.6)}

# Sprites with their hue turned: key -> (sprite number, hue shift in degrees, saturation multiplier).
TINTED_ITEMS = {
    "silk_cloak": (81, 180, 0.8), "vintage_brandy": (61, -25, 1.3), "fairy_gold": (19, 110, 1.1),
    "elven_wine": (63, 200, 1.1), "royal_charter": (24, 15, 1.4),
}

# Sprites made of a few copies of another, for "a lot of it": key -> (sprite number, [(x, y) offsets]).
PILED_ITEMS = {
    "royal_gold": (19, [(0, 6), (9, 6), (4, 0)]),
    "dragon_hoard": (19, [(0, 12), (9, 12), (18, 12), (4, 6), (13, 6), (9, 0)]),
}

BACKGROUNDS = {"courtyard_night": "24", "cellar_night": "10"}

# Seller key -> Ninja Adventure faceset number. Keys match SellerDef.Key in scripts/Core/Sellers.cs, plus Uncle Bram.
PORTRAITS = {
    "hob": 22, "penn": 19, "mags": 5, "tam": 25,           # tier 1: the village
    "brask": 18, "tobias": 10, "marta": 4,                 # tier 2: soldiers and merchants
    "wren": 6, "orrin": 9, "grit": 8,                      # tier 3: mages and a miner
    "vex": 3, "gull": 14, "silas": 13, "pip": 17, "ermine": 21,  # tier 4: nobles, adventurers, crooks
    "bram": 16,                                            # Uncle Bram, who hands you the shop
}

# Group -> Kenney source files (pack folder, file stem).
def _variants(pack, stem, indices, digits=3, sep="_"):
    return [(pack, f"{stem}{sep}{i:0{digits}d}") for i in indices]

IMPACT, RPG, CASINO, UI = "kenney_impactsounds", "kenney_rpgaudio", "kenney_casinoaudio", "kenney_interfacesounds"

SOUNDS = {
    # Kinds of things: the sound you'd expect from each in a shaken chest. Bottles' slosh is synthesized in Sfx.cs.
    "jingle": [(RPG, "handleCoins"), (RPG, "handleCoins2")],
    "clank": _variants(IMPACT, "impactTin_medium", range(5)),
    "flump": [(RPG, f"cloth{i}") for i in range(1, 5)],
    "rustle": [(CASINO, "cardShove1"), (CASINO, "cardShove2"), (CASINO, "cardShove4")],
    "tinkle": _variants(UI, "glass", [1, 2, 3]),

    # Handling the chest and the shop
    "land": _variants(IMPACT, "impactPlank_medium", range(5)),
    "clack": _variants(IMPACT, "impactMetal_heavy", range(3)),
    "pay": [(CASINO, f"chipsStack{i}") for i in range(1, 4)],
    "door_open": [(RPG, "doorOpen_1"), (RPG, "doorOpen_2")],
    "door_close": [(RPG, "doorClose_1"), (RPG, "doorClose_2")],
    "latch": [(RPG, "metalLatch"), (RPG, "metalClick")],
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


# Tool and upgrade icons, 16x16 pixel art drawn here (no CC0 pack has these), pre-scaled like the UI frames.
ICON_PALETTE = {
    "k": (20, 16, 26), "g": (242, 193, 78), "G": (176, 128, 44), "w": (240, 232, 214), "s": (190, 196, 210),
    "S": (110, 116, 134), "r": (200, 60, 60), "b": (140, 90, 46), "B": (92, 56, 28), "c": (110, 200, 230),
    "p": (160, 110, 230),
}
ICONS = {
    "sign": [
        "................",
        ".kkkkkkkkkkkkkk.",
        ".kbbbbbbbbbbbbk.",
        ".kkkkkkkkkkkkkk.",
        "...k........k...",
        "...k........k...",
        ".kkkkkkkkkkkkkk.",
        ".kggggggggggggk.",
        ".kgkkggkkgkkggk.",
        ".kgkggkgkgkgkgk.",
        ".kgkkggkkgkkggk.",
        ".kggggggggggggk.",
        ".kkkkkkkkkkkkkk.",
        "................",
        "................",
        "................",
    ],
    "shop": [
        "................",
        ".......kk.......",
        "......krrk......",
        ".....krrrrk.....",
        "....krrrrrrk....",
        "...krrrrrrrrk...",
        "..krrrrrrrrrrk..",
        ".kkkkkkkkkkkkkk.",
        "..kbbbbbbbbbbk..",
        "..kbkkbbbbkkbk..",
        "..kbkcbbbbkcbk..",
        "..kbkkbkkbkkbk..",
        "..kbbbbkBkbbbk..",
        "..kbbbbkBkbbbk..",
        "..kkkkkkkkkkkk..",
        "................",
    ],
    "lamp": [
        "................",
        ".......kk.......",
        "......kSSk......",
        ".....kkkkkk.....",
        "....kSkppkSk....",
        "....kkpwwpkk....",
        "....kSpwwpSk....",
        "....kkpppkkk....",
        "....kSkppkSk....",
        ".....kkkkkk.....",
        "......kSSk......",
        "......kkkk......",
        "...p........p...",
        ".....p....p.....",
        "................",
        "................",
    ],
    "cabinet": [
        "................",
        "..kkkkkkkkkkkk..",
        "..kBbbbbbbbbBk..",
        "..kBkkkkkkkkBk..",
        "..kBkcwccgckBk..",
        "..kBkccccgckBk..",
        "..kBkkkkkkkkBk..",
        "..kBkcrccpckBk..",
        "..kBkcrccpckBk..",
        "..kBkkkkkkkkBk..",
        "..kBkcccgcckBk..",
        "..kBkccgggckBk..",
        "..kBkkkkkkkkBk..",
        "..kBbbbbbbbbBk..",
        "..kkkkkkkkkkkk..",
        "..kk........kk..",
    ],
    "picks": [
        "................",
        "...........kk...",
        "..........ksk...",
        ".........ksk....",
        "........ksk.....",
        ".......ksk......",
        "......ksk..kk...",
        ".....ksk..ksk...",
        "...kkkk..ksk....",
        "..kgggk.ksk.....",
        "..kgGgkksk......",
        "..kgggkkk.......",
        "...kkkk.........",
        "................",
        "................",
        "................",
    ],
    "magnet": [
        "................",
        "...kkkkkkkkkk...",
        "..krrrrrrrrrrk..",
        ".krrrrrrrrrrrrk.",
        ".krrrkkkkkkrrrk.",
        ".krrrk....krrrk.",
        ".krrrk....krrrk.",
        ".krrrk....krrrk.",
        ".krrrk....krrrk.",
        ".kSSSk....kSSSk.",
        ".ksssk....ksssk.",
        ".ksssk....ksssk.",
        ".kkkkk....kkkkk.",
        "................",
        "................",
        "................",
    ],
    "crown": [
        "................",
        "................",
        "..k....kk....k..",
        ".kgk..kggk..kgk.",
        ".kggk.kggk.kggk.",
        ".kgggkggggkgggk.",
        ".kggggggggggggk.",
        ".kgrggggggggrgk.",
        ".kggggrggrggggk.",
        ".kGGGGGGGGGGGGk.",
        ".kkkkkkkkkkkkkk.",
        "................",
        "................",
        "................",
        "................",
        "................",
    ],
}


# One small icon per kind of thing (matching Category in scripts/Core/Items.cs), for the slip and the ledger rows,
# where item sprites are too big. Saved at 2x under assets/ui/kinds/.
KIND_ICONS = {
    "coins": [
        "................",
        "................",
        "................",
        ".....kkkkkk.....",
        "....kggggggk....",
        "....kGGGGGGk....",
        "...kkkkkkkkkk...",
        "..kggggggggggk..",
        "..kGGGGGGGGGGk..",
        "..kkkkkkkkkkkk..",
        ".kggggggggggggk.",
        ".kGGGGGGGGGGGGk.",
        ".kggggggggggggk.",
        ".kGGGGGGGGGGGGk.",
        "..kkkkkkkkkkkk..",
        "................",
    ],
    "metal": [
        "................",
        ".............kk.",
        "............kwk.",
        "...........kssk.",
        "..........kssk..",
        ".........kssk...",
        "........kssk....",
        "...kk..kssk.....",
        "...kGkkssk......",
        "....kGssk.......",
        "....kkGk........",
        "...kbkkGk.......",
        "..kbk..kk.......",
        ".kbk............",
        ".kk.............",
        "................",
    ],
    "cloth": [
        "................",
        "................",
        "....kkkkkkkk....",
        "...krrrrrrrrk...",
        "..krrrrrrrrrrk..",
        "..krrkrrrrkrrk..",
        "..krrkrrrrkrrk..",
        "..krrkrrrrkrrk..",
        "..krrkrrrrkrrk..",
        "..krkrrrrrrkrk..",
        "..krkrrrrrrkrk..",
        "..kkrrrrrrrrkk..",
        "..krrrrrrrrrrk..",
        "..kkkkkkkkkkkk..",
        "................",
        "................",
    ],
    "bottles": [
        "................",
        "......kkkk......",
        "......kbbk......",
        ".......kk.......",
        "......kwck......",
        "......kwck......",
        ".....kccccck....",
        "....kccccccck...",
        "....kwcccccck...",
        "....kwcccccck...",
        "....kccccccck...",
        "....kccccccck...",
        "....kccccccck...",
        ".....kkkkkkk....",
        "................",
        "................",
    ],
    "paper": [
        "................",
        "................",
        "..kkkkkkkkkkk...",
        ".kwwwwwwwwwwwk..",
        ".kwkkkkkkkwwwk..",
        "..kwwwwwwwwwwk..",
        "..kwkkkkkkkwwk..",
        "..kwwwwwwwwwwk..",
        "..kwkkkkkkwwwk..",
        "..kwwwwwwwwwwk..",
        "..kwkkkkkkkwwk..",
        "..kwwwwwwwwwwwk.",
        "...kkkkkkkkkkkk.",
        "................",
        "................",
        "................",
    ],
    "hidden": [
        "................",
        "................",
        ".kkkkkkkkkkkkkk.",
        ".kbbbbbbbbbbbbk.",
        ".kb..........bk.",
        ".kb..........bk.",
        ".kb..........bk.",
        ".kbBBBBBBBBBBbk.",
        ".kb..gggg....bk.",
        ".kb.gGGGGg...bk.",
        ".kbBBBBBBBBBBbk.",
        ".kkkkkkkkkkkkkk.",
        "................",
        "................",
        "................",
        "................",
    ],
    "jewels": [
        "................",
        "................",
        ".....kkkkkk.....",
        "....kcwcccck....",
        "...kcwcccccck...",
        "..kkkkkkkkkkkk..",
        "..kcwcccccccck..",
        "...kcwcccccck...",
        "....kcwcccck....",
        ".....kcccck.....",
        "......kcck......",
        ".......kk.......",
        "................",
        "................",
        "................",
        "................",
    ],
}


def build_icons():
    for name, rows in ICONS.items():
        save(pixel_icon(rows, name).resize((16 * UI_SCALE, 16 * UI_SCALE), Image.NEAREST), "ui", "icons", f"{name}.png")
    for name, rows in KIND_ICONS.items():
        save(pixel_icon(rows, name).resize((32, 32), Image.NEAREST), "ui", "kinds", f"{name}.png")


def pixel_icon(rows, name):
    assert len(rows) == 16 and all(len(r) == 16 for r in rows), f"icon {name} must be 16x16"
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                px[x, y] = (*ICON_PALETTE[ch], 255)
    return img


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
    for folder in (os.path.join(ASSETS, "sprites", "items"), os.path.join(ASSETS, "audio"), os.path.join(ASSETS, "ui", "icons"),
                   os.path.join(ASSETS, "ui", "kinds")):
        shutil.rmtree(folder, ignore_errors=True)

    for key, number in ITEMS.items():
        save(item_sprite(number), "sprites", "items", f"{key}.png")
    for key, (number, grey) in WORN_ITEMS.items():
        img = item_sprite(number)
        px = img.load()
        for y in range(img.height):
            for x in range(img.width):
                r, g, b, a = px[x, y]
                lum = (r * 3 + g * 6 + b) // 10
                px[x, y] = (round(r + (lum - r) * grey), round(g + (lum - g) * grey), round(b + (lum - b) * grey), a)
        save(img, "sprites", "items", f"{key}.png")

    for key, (number, hue, sat) in TINTED_ITEMS.items():
        img = item_sprite(number)
        h, sv, v = img.convert("RGB").convert("HSV").split()
        h = h.point(lambda x: (x + round(hue * 255 / 360)) % 256)
        sv = sv.point(lambda x: min(255, round(x * sat)))
        tinted = Image.merge("HSV", (h, sv, v)).convert("RGBA")
        tinted.putalpha(img.getchannel("A"))
        save(tinted, "sprites", "items", f"{key}.png")
    for key, (number, offsets) in PILED_ITEMS.items():
        img = item_sprite(number)
        w = max(x for x, _ in offsets) + img.width
        h = max(y for _, y in offsets) + img.height
        pile = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        for x, y in offsets:
            pile.alpha_composite(img, (x, y))
        save(pile, "sprites", "items", f"{key}.png")

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
    build_icons()

    fonts = os.path.join(ASSETS, "fonts")
    os.makedirs(fonts, exist_ok=True)
    shutil.copy(os.path.join(kenney, "kenney_fontpackage/Fonts/Kenney Mini.ttf"), os.path.join(fonts, "kenney_mini.ttf"))

    count = sum(len(files) for _, _, files in os.walk(ASSETS))
    print(f"Wrote {count} files to {ASSETS}")


if __name__ == "__main__":
    sys.exit(main())
