#!/usr/bin/env python3
"""Builds assets/ from the art drawn in tools/custom_art.py and the Kenney font and sounds. Re-run after changing a
drawing there or a mapping below.

Sources:
  - The pictures (things, portraits, chests, curios, backdrops, effects): drawn in code by tools/custom_art.py for
    LootFall; the UI frames and icons below are drawn here.
  - Kenney Font Package, Impact Sounds, RPG Audio, Casino Audio, Interface Sounds (CC0, see CREDITS.md)
    https://kenney.nl (mirror: github.com/ETdoFresh/kenney.nl)

Usage: python3 tools/import_art.py [--cache DIR]
Requires Pillow. Kenney's packs are cloned (partially) into the cache on first run.
"""
import argparse
import os
import shutil
import subprocess
import sys

from PIL import Image

import custom_art

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")

KENNEY = ("https://github.com/ETdoFresh/kenney.nl", [
    "kenney_fontpackage", "kenney_impactsounds", "kenney_rpgaudio", "kenney_casinoaudio", "kenney_interfacesounds",
])

# Curios (Curios.All in scripts/Core/Curios.cs) are drawn 24px and shown at 2x in 56px slots.
CURIO_SLOT = 56

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

    # Alternatives for each kind (scripts/Audio/SoundSets.cs), to audition and choose in Settings > Item sounds.
    "jingle_chips": [(CASINO, f"chipsCollide{i}") for i in range(1, 5)],
    "jingle_handle": [(CASINO, f"chipsHandle{i}") for i in range(1, 7)],
    "clank_light": _variants(IMPACT, "impactMetal_light", range(5)),
    "clank_pot": [(RPG, f"metalPot{i}") for i in range(1, 4)],
    "clank_plate": _variants(IMPACT, "impactPlate_medium", range(5)),
    "flump_soft": _variants(IMPACT, "impactSoft_medium", range(5)),
    "flump_heavy": _variants(IMPACT, "impactSoft_heavy", range(5)),
    "flump_leather": [(RPG, "dropLeather"), (RPG, "handleSmallLeather"), (RPG, "handleSmallLeather2"), (RPG, "clothBelt"),
                      (RPG, "clothBelt2")],
    "slosh_clink": _variants(IMPACT, "impactGlass_light", range(5)),
    "rustle_book": [(RPG, f"bookFlip{i}") for i in range(1, 4)],
    "rustle_slide": [(CASINO, f"cardSlide{i}") for i in range(1, 9)],
    "rustle_shuffle": [(CASINO, "cardShuffle"), (CASINO, "cardFan1"), (CASINO, "cardFan2")],
    "tinkle_glass": _variants(UI, "glass", [4, 5, 6]),
    "tinkle_plate": _variants(IMPACT, "impactPlate_light", range(5)),

    # Handling the chest and the shop
    "land": _variants(IMPACT, "impactPlank_medium", range(5)),
    "clack": _variants(IMPACT, "impactMetal_heavy", range(3)),
    "pay": [(CASINO, f"chipsStack{i}") for i in range(1, 4)],
    "door_open": [(RPG, "doorOpen_1"), (RPG, "doorOpen_2")],
    "door_close": [(RPG, "doorClose_1"), (RPG, "doorClose_2")],
    "latch": [(RPG, "metalLatch"), (RPG, "metalClick")],
    "book": [(RPG, "bookOpen")],
}

# The chest (custom_art.chest) is two 16x14 frames, closed and open, drawn in the red skin. Its body palette (dark,
# main, highlight, shade) is swapped per lot provenance; the gold rim and orange fittings stay.
CHEST_BASE = ["#711335", "#dc2728", "#ff5733", "#c03a24"]
CHEST_SKINS = {
    "red": CHEST_BASE,
    "blue": ["#2b2a6e", "#457ad3", "#5cbad7", "#3b5bb0"],
    "purple": ["#3d1f5c", "#8a4fd1", "#b88cff", "#6a3aa8"],
    "wood": ["#4a2a14", "#8a5a2b", "#b07a40", "#6e4520"],
    "green": ["#23402a", "#4f8a3a", "#7fc25a", "#3b6e2e"],
    "gold": ["#6b4a12", "#e0a82e", "#ffe27a", "#b7841f"],    # the old royal chests (and the vault)
    "black": ["#0e0b12", "#2a2433", "#4a4058", "#1c1724"],   # the Stranger's, packed in straw
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


def walk(folder):
    return [os.path.join(here, f) for here, _, files in os.walk(folder) for f in files]


def save(image, *parts):
    path = os.path.join(ASSETS, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    return path


def build_chests():
    closed, opened = custom_art.chest()
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

        if skin == "purple":
            # The window icon: the purple chest at 4x, centred on a 64px square.
            icon = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
            icon.alpha_composite(recolor(closed).resize((64, 56), Image.NEAREST), (0, 4))
            save(icon, "ui", "icon.png")
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
    "p": (160, 110, 230), "R": (255, 70, 90), "m": (150, 24, 44),
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
    "charter": [
        "................",
        "..kkkkkkkkkkkk..",
        ".kwwwwwwwwwwwwk.",
        "..kwsssssssswk..",
        "..kwwwwwwwwwwk..",
        "..kwssssssswwk..",
        "..kwwwwwwwwwwk..",
        "..kwsssssswwwk..",
        "..kwwwwwwwrrwk..",
        "..kwwwwwwrrrrk..",
        "..kwwwwwwwrrwk..",
        ".kwwwwwwwwrwwwk.",
        "..kkkkkkkkkkkk..",
        "................",
        "................",
        "................",
    ],
    "seal": [
        "................",
        "................",
        ".....kkkkkk.....",
        "....krrrrrrk....",
        "...krrgrrgrrk...",
        "..krrrgggggrrk..",
        "..krrrgGGGgrrk..",
        "..krrrrrrrrrrk..",
        "..krrrrrrrrrrk..",
        "...krrrrrrrrk...",
        "....krrrrrrk....",
        ".....kkkkkk.....",
        "....kr....rk....",
        "...kr......rk...",
        "................",
        "................",
    ],
    "case": [
        "................",
        "..kkkkkkkkkkkk..",
        "..kBBBBBBBBBBk..",
        "..kBkkkkkkkkBk..",
        "..kBkcccccckBk..",
        "..kBkcpccgckBk..",
        "..kBkcccccckBk..",
        "..kBkkkkkkkkBk..",
        "..kBkcccccckBk..",
        "..kBkcgccrckBk..",
        "..kBkcccccckBk..",
        "..kBkkkkkkkkBk..",
        "..kBBBBBgBBBBk..",
        "..kkkkkkkkkkkk..",
        "................",
        "................",
    ],
    "night": [
        "................",
        "......kkkk......",
        "....kkggggk.....",
        "...kgggkkk......",
        "..kgggk.........",
        "..kggk......k...",
        ".kgggk.....kwk..",
        ".kgggk......k...",
        ".kgggk..........",
        ".kGggk..........",
        "..kGggk.......k.",
        "..kGGggk.....kgk",
        "...kGGGgkkkkkGk.",
        "....kkGGGGGGkk..",
        "......kkkkkk....",
        "................",
    ],
    "patrons": [
        "................",
        "..kkkkkkkkkkkk..",
        ".kBBBBBBBBBBBBk.",
        ".kBkwwwwwwwwkBk.",
        ".kBkwsssssswkBk.",
        ".kBkwwwwwwwwkBk.",
        ".kBkwssssswwkBk.",
        ".kBkwwwwwwwwkBk.",
        ".kBkwsssswwwkBk.",
        ".kBkwwwwwwwwkBk.",
        ".kBkwwwwwrrwkBk.",
        ".kBkwwwwwrrwkBk.",
        ".kBkkkkkkkkkkBk.",
        ".kBBBBBBBBBBBBk.",
        "..kkkkkkkkkkkk..",
        "................",
    ],
    # A buyers' trade: brass scales, a crier's handbell, an auction gavel, a royal warrant with its seal.
    "scales": [
        "................",
        ".......kk.......",
        "......kggk......",
        "..kkkkkggkkkkk..",
        "..kgggggggggggk.",
        "..kkkkkggkkkkk..",
        "..k....gg....k..",
        ".k.k...gg...k.k.",
        "k...k..gg..k...k",
        "kkkkk..gg..kkkkk",
        "kGGGk..gg..kGGGk",
        ".kkk...gg...kkk.",
        ".......gg.......",
        ".....kkggkk.....",
        "....kGGGGGGk....",
        "....kkkkkkkk....",
    ],
    "crier": [
        "................",
        ".......kk.......",
        "......kbbk......",
        "......kbbk......",
        ".......kk.......",
        "......kggk......",
        ".....kggggk.....",
        "....kggwgggk....",
        "....kgwggggk....",
        "...kgggggggGk...",
        "...kggggggGGk...",
        "..kGGGGGGGGGGk..",
        "..kkkkkkkkkkkk..",
        ".......kk.......",
        ".......kk.......",
        "................",
    ],
    "block": [
        "................",
        ".kkkkkkk........",
        "kbbbbbbbk.......",
        "kbBBBBBbk.......",
        "kbbbbbbbk.......",
        ".kkkkkkkk.......",
        "....kbk.........",
        ".....kbk........",
        "......kbk.......",
        ".......kbk......",
        "........kbk.....",
        ".........kk.....",
        "..kkkkkkkkkkkk..",
        ".kBBBBBBBBBBBBk.",
        ".kkkkkkkkkkkkkk.",
        "................",
    ],
    "warrant": [
        "................",
        "..kkkkkkkkkkk...",
        ".kwwwwwwwwwwwk..",
        ".kwkkkkkkkkwwk..",
        ".kwwwwwwwwwwwk..",
        ".kwkkkkkkwwwwk..",
        ".kwwwwwwwwwwwk..",
        ".kwkkkkkkkwwwk..",
        ".kwwwwwwwwwwwk..",
        ".kwwwwwwkkkwwk..",
        ".kwwwwwkrrrkwk..",
        ".kwwwwwkrrrkwk..",
        ".kkkkkkkkrkkkk..",
        "........krk.....",
        ".......kr.rk....",
        "................",
    ],
    # The Bookkeeping Engine: a brass clockwork with a gear on top, a slip coming out of it.
    "engine": [
        "................",
        "....kkk.........",
        "...kgggk..kkkk..",
        "..kgkkkgk.kwwk..",
        "..kgk.kgk.kwsk..",
        "..kgkkkgk.kwwk..",
        "...kgggk..kwsk..",
        ".kkkkkkkkkkwwkk.",
        ".kGGGGGGGGGGGGk.",
        ".kGgggggggggGGk.",
        ".kGgkkgkkgkkgGk.",
        ".kGgggggggggGGk.",
        ".kGGGGGGGGGGGGk.",
        ".kkkkkkkkkkkkkk.",
        "..kk........kk..",
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
    # A Heartsong: a red heart with a note in it.
    "heartsong": [
        "................",
        "................",
        "..kkkk....kkkk..",
        ".kRRRRk..kRRRRk.",
        "kRRwRRRkkRRRRRRk",
        "kRwRRRRRRRRkkRRk",
        "kRRRRRRRRRRkwkRk",
        "kRRRRRRRRRRkRkRk",
        ".kRRRRRRkkkkRkk.",
        "..kRRRRkwwkRRk..",
        "...kRRRkwwkRk...",
        "....kmRRkkRmk...",
        ".....kmRRRmk....",
        "......kmmmk.....",
        ".......kkk......",
        "................",
    ],
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
    "arms": [
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
    "garb": [
        "................",
        "......kkkk......",
        ".....kgggGk.....",
        "....krrrrrrk....",
        "...krrrrrrrrk...",
        "...krrkrrkrrk...",
        "..krrrkrrkrrrk..",
        "..krrrkrrkrrrk..",
        "..krrrkrrkrrrk..",
        ".krrrrkrrkrrrrk.",
        ".krrrrkrrkrrrrk.",
        ".krrrrkrrkrrrrk.",
        ".kkkkkkkkkkkkkk.",
        "................",
        "................",
        "................",
    ],
    "potions": [
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
    "scrolls": [
        "................",
        "...kkkkkkkkkk...",
        "..kbbbbbbbbbbk..",
        "...kkkkkkkkkk...",
        "....kwwwwwwk....",
        "....kwkkkkwk....",
        "....kwwwwwwk....",
        "....kwkkkwwk....",
        "....kwwwwwwk....",
        "....kwkkkkwk....",
        "....kwwwwwwk....",
        "...kkkkkkkkkk...",
        "..kbbbbbbbbbbk..",
        "...kkkkkkkkkk...",
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
    "gems": [
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


# The shop at each tier, as one 640x360 picture shown at 2x behind the counter (and, dimmed, behind the other screens):
# a backdrop from custom_art (the place outside or the wall behind), the architecture drawn here, a few props, and the
# counter. The
# counter's top edge is at y 238 (476 on screen); the HUD covers the top 28 rows; on the counter screen the appraisal
# panel covers x 410 and up. The display shelf (from the village shop) is at y 98, where the game stands what you keep.
SHOP_W, SHOP_H, COUNTER_Y, SHELF_Y = 640, 360, 238, 98


def build_shops():
    from PIL import ImageDraw
    props = custom_art.props()

    def backdrop(img, shade=1.0):
        if shade != 1.0:
            img = Image.eval(img.convert("RGB"), lambda v: int(v * shade)).convert("RGBA")
        return img

    def prop(img, name, x, bottom):
        p = props[name]
        img.alpha_composite(p, (x, bottom - p.height))

    def shadow(img, box, alpha):
        layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).rectangle(box, fill=(0, 0, 0, alpha))
        img.alpha_composite(layer)

    def glow(img, cx, cy, radius, color):
        # Hard-edged rings, brighter toward the middle: pixel-art light, not a smooth gradient.
        layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        for i in range(4, 0, -1):
            r = radius * i // 4
            d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(*color, 22))
        img.alpha_composite(layer)

    def lamp(img, d, x, top, length):
        d.line((x, top, x, top + length), fill=(40, 30, 24, 255))
        d.rectangle((x - 4, top + length, x + 4, top + length + 10), fill=(58, 40, 26, 255), outline=(30, 20, 14, 255))
        d.rectangle((x - 2, top + length + 2, x + 2, top + length + 8), fill=(255, 214, 120, 255))
        glow(img, x, top + length + 5, 34, (255, 190, 90))

    def shelf(d, top, bottom_color, top_color, edge_color, bracket):
        d.rectangle((150, top, 402, top + 5), fill=top_color)
        d.line((150, top, 402, top), fill=edge_color)
        d.line((150, top + 5, 402, top + 5), fill=bottom_color)
        for x in (158, 388):
            d.polygon([(x, top + 6), (x + 8, top + 6), (x, top + 14)], fill=bracket)

    def planks(d, y0, y1, color, seam, every, x0=0, x1=SHOP_W):
        d.rectangle((x0, y0, x1, y1), fill=color)
        for x in range(x0 + every // 2, x1, every):
            d.line((x, y0, x, y1), fill=seam)

    shops = {}

    # The roadside stall: a trestle table under a striped awning at the Brindle crossroads.
    img = backdrop(custom_art.crossroads())
    d = ImageDraw.Draw(img)
    shadow(img, (0, 58, SHOP_W, 74), 70)
    d = ImageDraw.Draw(img)
    for i, x in enumerate(range(0, SHOP_W, 16)):
        colour = (184, 74, 60, 255) if i % 2 == 0 else (239, 225, 193, 255)
        d.rectangle((x, 26, x + 15, 58), fill=colour)
        d.ellipse((x, 52, x + 15, 66), fill=colour, outline=(59, 36, 24, 255))
        d.line((x, 26, x + 15, 26), fill=(59, 36, 24, 255))
    d.rectangle((0, 26, SHOP_W, 30), fill=(122, 46, 38, 255))
    for x in (6, SHOP_W - 14):
        d.rectangle((x, 30, x + 7, COUNTER_Y), fill=(110, 72, 38, 255), outline=(52, 32, 18, 255))
    lamp(img, d, 120, 66, 10)
    d = ImageDraw.Draw(img)
    prop(img, "crate", 560, COUNTER_Y)
    prop(img, "barrel", 520, COUNTER_Y)
    d.rectangle((0, COUNTER_Y, SHOP_W, COUNTER_Y + 11), fill=(160, 112, 60, 255))
    d.line((0, COUNTER_Y, SHOP_W, COUNTER_Y), fill=(196, 140, 84, 255))
    d.line((0, COUNTER_Y + 11, SHOP_W, COUNTER_Y + 11), fill=(70, 44, 22, 255))
    for x in range(70, SHOP_W, 150):
        d.line((x, COUNTER_Y + 1, x, COUNTER_Y + 10), fill=(110, 72, 38, 255))
    # A sacking cloth hangs down the front, ragged at the hem, with the trestle legs showing below.
    for x in (40, 300, 560):
        d.line((x, COUNTER_Y + 12, x + 22, SHOP_H), fill=(70, 44, 22, 255), width=4)
        d.line((x + 22, COUNTER_Y + 12, x, SHOP_H), fill=(70, 44, 22, 255), width=4)
    d.rectangle((0, COUNTER_Y + 12, SHOP_W, 326), fill=(176, 148, 104, 255))
    for y in range(COUNTER_Y + 14, 326, 2):
        for x in range((y // 2) % 2, SHOP_W, 2):
            d.point((x, y), fill=(160, 132, 92, 255))
    for x in range(0, SHOP_W, 6):
        d.line((x, COUNTER_Y + 16, x + 3, COUNTER_Y + 16), fill=(120, 94, 60, 255))
    for x in range(0, SHOP_W, 8):
        d.polygon([(x, 326), (x + 8, 326), (x + 4, 331 + (x // 8) % 3)], fill=(176, 148, 104, 255))
    shops[1] = img

    # The village shop: an old stone wall with a window onto the Old Wood, timber beams, oil lamps, and a shelf for what
    # you keep.
    img = backdrop(custom_art.thornwick_wall(), shade=0.92)
    d = ImageDraw.Draw(img)
    shadow(img, (0, 40, SHOP_W, 48), 60)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 26, SHOP_W, 40), fill=(78, 44, 18, 255))
    d.line((0, 27, SHOP_W, 27), fill=(110, 69, 32, 255))
    d.line((0, 40, SHOP_W, 40), fill=(40, 22, 10, 255))
    for x in (0, SHOP_W - 10):
        d.rectangle((x, 26, x + 9, COUNTER_Y), fill=(78, 44, 18, 255), outline=(40, 22, 10, 255))
    d.polygon([(10, 41), (34, 41), (10, 65)], fill=(78, 44, 18, 255), outline=(40, 22, 10, 255))
    d.polygon([(SHOP_W - 11, 41), (SHOP_W - 35, 41), (SHOP_W - 11, 65)], fill=(78, 44, 18, 255), outline=(40, 22, 10, 255))
    # Wainscoting along the bottom of the wall, behind the counter.
    d.rectangle((10, 168, SHOP_W - 11, COUNTER_Y), fill=(120, 78, 40, 255))
    d.rectangle((10, 168, SHOP_W - 11, 171), fill=(92, 56, 26, 255))
    d.line((10, 168, SHOP_W - 11, 168), fill=(150, 100, 56, 255))
    for x in range(30, SHOP_W - 11, 28):
        d.line((x, 172, x, COUNTER_Y), fill=(98, 62, 30, 255))
    lamp(img, d, 132, 41, 18)
    lamp(img, d, 520, 41, 18)
    d = ImageDraw.Draw(img)
    shelf(d, SHELF_Y, (78, 44, 18, 255), (147, 96, 47, 255), (176, 122, 64, 255), (78, 44, 18, 255))
    # Counter props stay in the corner by the appraisal panel, clear of the hints under the chest.
    prop(img, "jar", 394, COUNTER_Y)
    prop(img, "barrel", 470, COUNTER_Y)
    d = ImageDraw.Draw(img)
    planks(d, COUNTER_Y + 12, SHOP_H, (138, 90, 44, 255), (104, 66, 32, 255), 40)
    d.rectangle((0, COUNTER_Y, SHOP_W, COUNTER_Y + 11), fill=(176, 122, 64, 255))
    d.line((0, COUNTER_Y, SHOP_W, COUNTER_Y), fill=(208, 154, 90, 255))
    d.line((0, COUNTER_Y + 11, SHOP_W, COUNTER_Y + 11), fill=(78, 44, 18, 255))
    d.rectangle((0, 300, SHOP_W, 304), fill=(104, 66, 32, 255))
    shops[2] = img

    # The town shop: panelled walls and a big display window onto the square at sunset; what you keep stands on the
    # sill. Brass-edged walnut counter, lanterns.
    img = Image.new("RGBA", (SHOP_W, SHOP_H), (168, 142, 110, 255))
    d = ImageDraw.Draw(img)
    # Plaster above, with a faint mottle.
    for y in range(26, 116, 3):
        for x in range((y * 7) % 11, SHOP_W, 11):
            d.point((x, y), fill=(156, 130, 100, 255))
    img.alpha_composite(custom_art.crownhold_view(380, 72), (140, 26))
    d = ImageDraw.Draw(img)
    d.rectangle((134, 24, 526, 29), fill=(52, 32, 20, 255))
    for x in (134, 200, 266, 332, 398, 464, 522):
        d.rectangle((x, 26, x + 4, SHELF_Y), fill=(52, 32, 20, 255))
    d.rectangle((136, 60, 524, 62), fill=(52, 32, 20, 255))
    # The sill: what you keep stands against the window.
    d.rectangle((128, SHELF_Y, 532, SHELF_Y + 7), fill=(206, 194, 170, 255))
    d.line((128, SHELF_Y, 532, SHELF_Y), fill=(236, 228, 210, 255))
    d.line((128, SHELF_Y + 7, 532, SHELF_Y + 7), fill=(110, 96, 80, 255))
    # A dado rail, then panelled wood down to the counter.
    d.rectangle((0, 116, SHOP_W, COUNTER_Y), fill=(104, 70, 44, 255))
    d.rectangle((0, 112, SHOP_W, 117), fill=(70, 44, 26, 255))
    d.line((0, 112, SHOP_W, 112), fill=(130, 90, 58, 255))
    for x in range(0, SHOP_W, 80):
        d.rectangle((x + 6, 126, x + 74, 228), fill=(112, 76, 48, 255), outline=(60, 38, 22, 255))
        d.line((x + 7, 127, x + 73, 127), fill=(140, 98, 64, 255))
        d.line((x + 7, 127, x + 7, 227), fill=(140, 98, 64, 255))
    # Goods on the far wall: a shelf of jars and bottles.
    d.rectangle((548, 84, 628, 88), fill=(70, 44, 26, 255))
    d = ImageDraw.Draw(img)
    lamp(img, d, 110, 30, 30)
    lamp(img, d, 562, 30, 22)
    d = ImageDraw.Draw(img)
    prop(img, "jar", 556, 84)
    prop(img, "flask", 578, 84)
    prop(img, "small_jar", 598, 84)
    prop(img, "flask", 398, COUNTER_Y)
    d = ImageDraw.Draw(img)
    d.rectangle((0, COUNTER_Y + 12, SHOP_W, SHOP_H), fill=(74, 46, 28, 255))
    for x in range(0, SHOP_W, 80):
        d.rectangle((x + 8, COUNTER_Y + 22, x + 72, 312), fill=(82, 52, 32, 255), outline=(46, 28, 16, 255))
        d.line((x + 9, COUNTER_Y + 23, x + 71, COUNTER_Y + 23), fill=(106, 68, 44, 255))
    d.rectangle((0, COUNTER_Y, SHOP_W, COUNTER_Y + 11), fill=(90, 58, 36, 255))
    d.line((0, COUNTER_Y + 1, SHOP_W, COUNTER_Y + 1), fill=(120, 80, 52, 255))
    d.rectangle((0, COUNTER_Y + 10, SHOP_W, COUNTER_Y + 12), fill=(201, 160, 64, 255))
    d.line((0, COUNTER_Y + 10, SHOP_W, COUNTER_Y + 10), fill=(240, 208, 112, 255))
    shops[3] = img

    # The Emporium: a colonnade in daylight with the Gilded Gate down the avenue, crimson drapes with gold, a marble
    # ledge and a marble counter.
    img = backdrop(custom_art.emporium_hall())
    d = ImageDraw.Draw(img)
    crimson = [(106, 20, 26, 255), (138, 28, 36, 255), (168, 36, 46, 255), (138, 28, 36, 255)]
    for x0, x1 in ((0, 46), (SHOP_W - 46, SHOP_W)):
        for i, x in enumerate(range(x0, x1, 6)):
            d.rectangle((x, 26, x + 5, SHOP_H), fill=crimson[i % 4])
        d.rectangle((x0, 118, x1, 124), fill=(212, 168, 56, 255), outline=(140, 100, 30, 255))
    d.rectangle((0, 26, SHOP_W, 44), fill=(138, 28, 36, 255))
    for x in range(0, SHOP_W, 20):
        d.ellipse((x, 38, x + 19, 52), fill=(138, 28, 36, 255))
        d.line((x + 2, 52, x + 17, 52), fill=(212, 168, 56, 255))
        for fx in range(x + 2, x + 18, 3):
            d.line((fx, 52, fx, 55), fill=(240, 200, 90, 255))
    d.line((0, 27, SHOP_W, 27), fill=(212, 168, 56, 255))
    shelf(d, SHELF_Y, (150, 140, 130, 255), (232, 228, 220, 255), (255, 255, 255, 255), (212, 168, 56, 255))
    prop(img, "jar", 394, COUNTER_Y)
    d = ImageDraw.Draw(img)
    d.rectangle((0, COUNTER_Y + 16, SHOP_W, SHOP_H), fill=(52, 44, 60, 255))
    for x in range(0, SHOP_W, 96):
        d.rectangle((x + 10, COUNTER_Y + 26, x + 86, 318), outline=(212, 168, 56, 255))
        d.line((x + 20, COUNTER_Y + 40, x + 60, 300), fill=(70, 62, 80, 255))
    d.rectangle((0, COUNTER_Y, SHOP_W, COUNTER_Y + 12), fill=(236, 232, 224, 255))
    for x in range(10, SHOP_W, 37):
        d.line((x, COUNTER_Y + 2, x + 14, COUNTER_Y + 10), fill=(189, 182, 172, 255))
    d.rectangle((0, COUNTER_Y + 12, SHOP_W, COUNTER_Y + 16), fill=(212, 168, 56, 255))
    d.line((0, COUNTER_Y + 12, SHOP_W, COUNTER_Y + 12), fill=(244, 216, 120, 255))
    shops[4] = img

    for tier, img in shops.items():
        save(img, "backgrounds", f"shop{tier}.png")


def build_items():
    """Each thing's sprite, for the opening (shown 2x) and, the same picture, for the small slots (the collection, the
    exhibition, the keep chooser), so a thing looks the same everywhere."""
    for key, img in custom_art.items().items():
        img = img.crop(img.getbbox())
        save(img, "sprites", "items", f"{key}.png")
        save(img, "sprites", "items", "icons", f"{key}.png")


def build_curios():
    for key, img in custom_art.curios().items():
        img = img.crop(img.getbbox())
        img = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
        slot = Image.new("RGBA", (CURIO_SLOT, CURIO_SLOT), (0, 0, 0, 0))
        slot.alpha_composite(img, ((CURIO_SLOT - img.width) // 2, (CURIO_SLOT - img.height) // 2))
        save(slot, "ui", "curios", f"{key}.png")


def build_signs():
    """The Gilded Guild's sign and a shard of the Aldric Key (the story's four), at 2x."""
    for name, img in (("guild", custom_art.guild_sign()), ("key", custom_art.key_shard())):
        save(img.resize((img.width * 2, img.height * 2), Image.NEAREST), "ui", f"{name}.png")


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

    kenney = fetch(*KENNEY, args.cache)

    # Rebuilt from scratch so renamed or dropped pictures and sounds don't linger. Godot's .import files stay (they
    # keep each file's uid) unless what they import is gone.
    folders = [os.path.join(ASSETS, f) for f in ("sprites", "audio", "backgrounds", "ui")]
    for folder in folders:
        for path in walk(folder):
            if not path.endswith(".import"):
                os.remove(path)

    build_items()
    for key, img in custom_art.portraits().items():
        save(img, "sprites", "sellers", f"{key}.png")
    save(custom_art.burst(), "sprites", "fx", "burst.png")
    build_chests()
    build_curios()
    build_signs()
    build_shops()
    build_ui()
    build_icons()

    for group, files in SOUNDS.items():
        out = os.path.join(ASSETS, "audio", group)
        os.makedirs(out, exist_ok=True)
        for i, (pack, stem) in enumerate(files):
            shutil.copy(os.path.join(kenney, pack, "Audio", f"{stem}.ogg"), os.path.join(out, f"{i}.ogg"))

    fonts = os.path.join(ASSETS, "fonts")
    os.makedirs(fonts, exist_ok=True)
    shutil.copy(os.path.join(kenney, "kenney_fontpackage/Fonts/Kenney Mini.ttf"), os.path.join(fonts, "kenney_mini.ttf"))

    for folder in folders:
        for path in walk(folder):
            if path.endswith(".import") and not os.path.exists(path[:-len(".import")]):
                os.remove(path)
        for here, dirs, files in os.walk(folder, topdown=False):
            if not dirs and not files:
                os.rmdir(here)

    count = sum(len(files) for _, _, files in os.walk(ASSETS))
    print(f"Wrote {count} files to {ASSETS}")


if __name__ == "__main__":
    sys.exit(main())
