#!/usr/bin/env python3
"""Builds assets/ from the open-licensed source packs. Re-run after changing a mapping below.

Sources (all CC0, see CREDITS.md):
  - Superpowers Asset Packs by Pixel-boy / Sparklin Labs  https://github.com/sparklinlabs/superpowers-asset-packs
    (the RPG Battle System pack: the knight, the monsters, battle backgrounds, effects, items, the chest, music)
  - Kenney Font Package, RPG Audio, Impact Sounds, Interface Sounds, Casino Audio, Digital Audio, Music Jingles
    https://kenney.nl (mirror: github.com/ETdoFresh/kenney.nl)

Usage: python3 tools/import_art.py [--cache DIR]
Requires Pillow. Sources are cloned (partially) into the cache on first run.
"""
import argparse
import colorsys
import json
import os
import shutil
import subprocess

from PIL import Image, ImageSequence

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")

SUPERPOWERS = ("https://github.com/sparklinlabs/superpowers-asset-packs", [
    "rpg-battle-system/char/warrior2", "rpg-battle-system/monster", "rpg-battle-system/backgrounds",
    "rpg-battle-system/fx", "rpg-battle-system/item", "rpg-battle-system/music", "ninja-adventure/musics",
    "ninja-adventure/sounds", "LICENSE.txt",
])
KENNEY = ("https://github.com/ETdoFresh/kenney.nl", [
    "kenney_fontpackage", "kenney_rpgaudio", "kenney_impactsounds", "kenney_interfacesounds", "kenney_casinoaudio",
    "kenney_digitalaudio", "kenney_musicjingles",
])

BATTLE = "rpg-battle-system"

# ---------------------------------------------------------------------------------------------------------------------
# Actors: the hero and the monsters, from the pack's animated GIFs. Every animation of an actor shares one canvas, so the
# frames are cropped to the union of everything drawn in any of them and stay aligned. Each animation is saved as a
# horizontal strip; actors.json says the frame size, where the feet are, which way it faces and each frame's duration.
# ---------------------------------------------------------------------------------------------------------------------

# key -> (source folder, {animation: gif file}, faces left in the source art)
ACTORS = {
    "knight": ("char/warrior2", {"idle": "idle", "attack": "attack", "hit": "hit", "guard": "guard"}, True),
    "slime": ("monster/slime", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "mushroom": ("monster/mushroom", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "snake": ("monster/snake", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "bat": ("monster/bat", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "ghost": ("monster/ghost", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "dino": ("monster/dino", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "boar": ("monster/boar", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "reptile": ("monster/reptile", {"idle": "idle", "attack": "attack", "hit": "Hit"}, False),
    "mimic": ("monster/mimic", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "giant": ("monster/giant", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
    "dragon": ("monster/dragon", {"idle": "idle", "attack": "attack", "hit": "hit"}, False),
}

# Monsters drawn once, without animation (the game animates them by squash and lunge): key -> (file, faces left).
STILL_ACTORS = {"octopus": ("monster/octopus.png", False), "yeti": ("monster/yeti.png", False)}

# The pack draws each monster in a second palette ("sprite-sheet-2"). The GIFs use the first, so the colour swap is read
# off the two sheets pixel by pixel and applied to the GIF frames: key -> (variant key, first sheet, second sheet).
VARIANTS = {
    "slime": ("slime_moss", "sprite-sheet-141x107.png", "sprite-sheet-2-141x107.png"),
    "mushroom": ("mushroom_frost", "sprite-sheet-192x163.png", "sprite-sheet-2-192x163.png"),
    "snake": ("snake_viper", "sprite-sheet-147x94.png", "sprite-sheet-2-147x94.png"),
    "bat": ("bat_ember", "sprite-sheet-121x89.png", "sprite-sheet-2-121x89.png"),
    "ghost": ("ghost_shade", "sprite-sheet-112x93.png", "sprite-sheet-2-112x93.png"),
    "dino": ("dino_sun", "sprite-sheet-154x118.png", "sprite-sheet-2-154x118.png"),
    "boar": ("boar_tusk", "sprite-sheet-239x178.png", "sprite-sheet-2-239x178.png"),
    "reptile": ("reptile_blood", "sprite-sheet- 248x151.png", "sprite-sheet-2-248x151.png"),
    "mimic": ("mimic_gilded", "sprite-sheet-231x172.png", "sprite-sheet-2-231x172.png"),
    "giant": ("giant_ember", "sprite-sheet-385x318.png", "sprite-sheet-2-385x318.png"),
    "dragon": ("dragon_ash", "sprite-sheet-258x209 - Copie.png", "sprite-sheet-2-258x209.png"),
}

# The knight's colours the game swaps for what he wears (see the gear shader in scripts/Ui/Shaders.cs): the blade, the
# slash and the shield's face are his weapon's; the cape and plume are his armour's.
KNIGHT_WEAPON = ["#8bffc7"]
KNIGHT_ARMOR = ["#d46d4f", "#794044"]

# ---------------------------------------------------------------------------------------------------------------------
# Effects: the pack's GIFs as strips, played at 2x in the battle. name -> gif number.
# ---------------------------------------------------------------------------------------------------------------------
FX = {
    "burst": 1, "lightning": 2, "claws": 3, "crescent": 4, "slash": 5, "cross": 6, "heal": 7, "rocks": 8,
    "fireball": 9, "scratch": 10, "flame": 11, "holy": 12, "poof": 13, "wind": 14,
}

# ---------------------------------------------------------------------------------------------------------------------
# Battle backgrounds, one per zone: a 640x210 band of a 640x480 picture (shown at 2x), from `top`.
# ---------------------------------------------------------------------------------------------------------------------
BACKGROUND_H = 210
BACKGROUNDS = {
    "greenwood": ("11", 70),
    "dunes": ("3", 96),
    "coast": ("5", 112),
    "ruins": ("15", 104),
    "frostpeak": ("7", 40),
    "roost": ("1-night", 84),
}

# The title screen's backdrop: a 640x360 window of a picture (shown at 2x, the whole screen).
TITLE = ("19", 60)

# ---------------------------------------------------------------------------------------------------------------------
# Items: one picture per slot and rarity (Common, Uncommon, Rare, Epic, Legendary, Mythic). An entry is a sprite
# number, or (number, hue turn in degrees, saturation, brightness) for a recoloured one.
# ---------------------------------------------------------------------------------------------------------------------
ITEMS = {
    "weapon": [31, 32, 67, 53, 78, (78, 150, 1.3, 1.0)],
    "helmet": [72, 1, 71, 45, 4, (4, -55, 1.2, 0.95)],
    "armor": [48, 2, 70, 69, 5, (5, -55, 1.2, 0.95)],
    "gloves": [18, 73, 59, (52, 230, 1.0, 1.0), (59, 25, 1.1, 1.1), (52, 0, 1.0, 1.0)],
    "boots": [54, 3, (3, 25, 1.2, 1.05), (3, 80, 1.1, 1.0), 6, (6, -55, 1.2, 0.95)],
    "ring": [38, 37, 68, 75, 49, (49, -60, 1.2, 1.0)],
    "amulet": [36, 39, 57, (57, 90, 1.3, 1.0), (39, 0, 1.0, 1.0), (57, -170, 1.4, 1.0)],
    "shield": [28, 29, 30, 74, 66, (66, -170, 1.2, 1.0)],
}

# ---------------------------------------------------------------------------------------------------------------------
# The chest (RPG Battle System, 127x88 frames: closed is frame 0, open frame 2), recoloured per kind of chest. Its
# palette: main, shade, planks, trim; the black outline and dark inside stay.
# ---------------------------------------------------------------------------------------------------------------------
CHEST_BASE = ["#ff4446", "#862627", "#e08441", "#ffe848"]
CHEST_SKINS = {
    "wooden": ["#a5683a", "#5c3418", "#cf9455", "#9aa3b4"],
    "iron": ["#6f8db0", "#384a6a", "#a8bdd6", "#e9eef5"],
    "golden": ["#f0b030", "#9a5a14", "#ffe08a", "#fff8d8"],
    "royal": ["#9a52e6", "#4a2186", "#d4a0ff", "#ffe848"],
}

UI_SCALE = 3

# ---------------------------------------------------------------------------------------------------------------------
# Sounds: group -> [(pack, path)], saved as assets/audio/<group>/<i>.ogg. Sfx.PlayGroup picks one at random.
# ---------------------------------------------------------------------------------------------------------------------
RPG, IMPACT, UI, CASINO, DIGITAL, JINGLE = ("kenney_rpgaudio", "kenney_impactsounds", "kenney_interfacesounds",
                                           "kenney_casinoaudio", "kenney_digitalaudio", "kenney_musicjingles")
NINJA = "ninja"


def _k(pack, *names):
    folder = {JINGLE: "Audio"}.get(pack, "Audio")
    return [(pack, f"{folder}/{n}.ogg") for n in names]


SOUNDS = {
    # The knight's blows: a swish, then flesh or armour
    "swing": _k(RPG, "drawKnife1", "drawKnife2", "drawKnife3"),
    "hit": _k(IMPACT, *[f"impactPunch_medium_00{i}" for i in range(5)]),
    "crit": _k(IMPACT, *[f"impactPunch_heavy_00{i}" for i in range(5)]),
    "slice": _k(RPG, "knifeSlice", "knifeSlice2", "chop"),
    # Monsters hitting the knight: his armour takes it
    "hurt": _k(IMPACT, *[f"impactPlate_light_00{i}" for i in range(5)]),
    "block": _k(IMPACT, *[f"impactMetal_light_00{i}" for i in range(5)]),
    "kill": [(NINJA, "kill.ogg")],
    # Coins and chests
    "coin": _k(RPG, "handleCoins", "handleCoins2"),
    "coins": [(NINJA, "gold-1.ogg"), (NINJA, "gold-2.ogg"), (NINJA, "gold-3.ogg")],
    "sell": _k(CASINO, "chipsStack1", "chipsStack2", "chipsStack3"),
    "chest_land": _k(IMPACT, *[f"impactPlank_medium_00{i}" for i in range(5)]),
    "creak": _k(RPG, "creak1", "creak2", "creak3"),
    "latch": _k(RPG, "metalLatch", "metalClick"),
    "lid": _k(RPG, "doorOpen_1", "doorOpen_2"),
    # Putting gear on
    "equip_metal": _k(RPG, "metalPot1", "metalPot2", "metalPot3"),
    "equip_cloth": _k(RPG, "cloth1", "cloth2", "cloth3", "cloth4", "beltHandle1"),
    # Moments
    "powerup": [(NINJA, "power-up.ogg")],
    "boss": _k(IMPACT, *[f"impactBell_heavy_00{i}" for i in range(3)]),
    "victory": _k(JINGLE, "8-Bit jingles/jingles_NES00"),
    "fanfare": _k(JINGLE, "Steel jingles/jingles_STEEL01"),
    "defeat": [(NINJA, "game-over.ogg")],
    "unlock": [(NINJA, "succes.ogg")],
    "click": _k(UI, "click_001", "click_002"),
    "toggle": _k(UI, "toggle_001", "toggle_002"),
    "error": _k(UI, "error_004"),
}

# Music: name -> (pack folder, file). Looped by the game.
MUSIC = {
    "battle": (f"{BATTLE}/music", "theme-10.ogg"),
    "battle2": (f"{BATTLE}/music", "theme-2.ogg"),
    "boss": (f"{BATTLE}/music", "theme-16.ogg"),
    "title": ("ninja-adventure/musics", "theme-11.ogg"),
}


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


def gif_frames(path):
    """Every frame of a GIF, composited (Pillow applies the disposal rules), with its duration in ms."""
    gif = Image.open(path)
    frames = []
    for frame in ImageSequence.Iterator(gif):
        frames.append((frame.convert("RGBA"), int(frame.info.get("duration", 100)) or 100))
    return frames


def union_box(images):
    boxes = [im.getbbox() for im in images if im.getbbox()]
    return (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))


def strip(frames):
    w, h = frames[0].size
    out = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        out.alpha_composite(f, (i * w, 0))
    return out


def palette_map(sheet_a, sheet_b):
    """The colour each colour of one sprite sheet becomes in its second palette (the majority, pixel by pixel)."""
    a, b = Image.open(sheet_a).convert("RGBA"), Image.open(sheet_b).convert("RGBA")
    pa, pb = a.load(), b.load()
    votes = {}
    for y in range(min(a.height, b.height)):
        for x in range(min(a.width, b.width)):
            ca, cb = pa[x, y], pb[x, y]
            if ca[3] and cb[3]:
                votes.setdefault(ca[:3], {}).setdefault(cb[:3], 0)
                votes[ca[:3]][cb[:3]] += 1
    return {k: max(v, key=v.get) for k, v in votes.items()}


def apply_map(img, mapping):
    img = img.copy()
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in mapping:
                px[x, y] = (*mapping[(r, g, b)], a)
    return img


def tint(img, turn, sat, val):
    """Turns the hue of every coloured pixel (greys stay grey) and scales saturation and brightness."""
    img = img.copy()
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s > 0.12:
                h = (h * 360 + turn) % 360 / 360
                s = min(1, s * sat)
                v = min(1, v * val)
            r, g, b = colorsys.hsv_to_rgb(h, s, v)
            px[x, y] = (round(r * 255), round(g * 255), round(b * 255), a)
    return img


def build_actors(src):
    meta = {}

    def write(key, anims, faces_left, extra=None):
        box = union_box([f for frames in anims.values() for f, _ in frames])
        box = (box[0] - 1, box[1] - 1, box[2] + 1, box[3] + 1)
        idle = anims["idle"][0][0].crop(box)
        ib = idle.getbbox()
        entry = {
            "w": box[2] - box[0], "h": box[3] - box[1],
            # Where the actor stands (bottom middle of what's drawn in its first idle frame), in frame pixels.
            "footX": (ib[0] + ib[2]) // 2, "footY": ib[3],
            # How wide and tall the body is, for placing health bars and numbers.
            "bodyW": ib[2] - ib[0], "bodyH": ib[3] - ib[1],
            "facesLeft": faces_left,
            "anims": {},
        }
        for name, frames in anims.items():
            cropped = [f.crop(box) for f, _ in frames]
            save(strip(cropped), "sprites", "actors", key, f"{name}.png")
            entry["anims"][name] = [d for _, d in frames]
        if extra:
            entry.update(extra)
        meta[key] = entry

    for key, (folder, files, faces_left) in ACTORS.items():
        base = os.path.join(src, BATTLE, folder)
        anims = {name: gif_frames(os.path.join(base, f"{gif}.gif")) for name, gif in files.items()}
        extra = None
        if key == "knight":
            extra = {"weaponColors": KNIGHT_WEAPON, "armorColors": KNIGHT_ARMOR}
        write(key, anims, faces_left, extra)
        if key in VARIANTS:
            variant, sheet_a, sheet_b = VARIANTS[key]
            mapping = palette_map(os.path.join(base, sheet_a), os.path.join(base, sheet_b))
            mapping = {k: v for k, v in mapping.items() if k != v}
            recoloured = {name: [(apply_map(f, mapping), d) for f, d in frames] for name, frames in anims.items()}
            write(variant, recoloured, faces_left)

    for key, (file, faces_left) in STILL_ACTORS.items():
        img = Image.open(os.path.join(src, BATTLE, file)).convert("RGBA")
        write(key, {"idle": [(img, 1000)]}, faces_left)

    path = os.path.join(ASSETS, "sprites", "actors", "actors.json")
    with open(path, "w") as f:
        json.dump(meta, f, indent=1, sort_keys=True)


def key_out(img, colour):
    """Makes every pixel of exactly this colour transparent (the effects' GIFs are drawn on an opaque grey)."""
    img = img.copy()
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            if px[x, y][:3] == colour:
                px[x, y] = (0, 0, 0, 0)
    return img


FX_BACKGROUND = (151, 151, 151)


def build_fx(src):
    meta = {}
    for name, number in FX.items():
        frames = [(key_out(f, FX_BACKGROUND), d) for f, d in gif_frames(os.path.join(src, BATTLE, "fx", f"{number}.gif"))]
        box = union_box([f for f, _ in frames])
        cropped = [f.crop(box) for f, _ in frames]
        save(strip(cropped), "sprites", "fx", f"{name}.png")
        meta[name] = {"w": box[2] - box[0], "h": box[3] - box[1], "durations": [d for _, d in frames]}
    with open(os.path.join(ASSETS, "sprites", "fx", "fx.json"), "w") as f:
        json.dump(meta, f, indent=1, sort_keys=True)


def build_backgrounds(src):
    for name, (number, top) in BACKGROUNDS.items():
        img = Image.open(os.path.join(src, BATTLE, "backgrounds", f"{number}.png")).convert("RGBA")
        save(img.crop((0, top, 640, top + BACKGROUND_H)), "backgrounds", f"{name}.png")
    number, top = TITLE
    img = Image.open(os.path.join(src, BATTLE, "backgrounds", f"{number}.png")).convert("RGBA")
    save(img.crop((0, top, 640, top + 360)), "backgrounds", "title.png")


def build_items(src):
    # The coin that flies to the gold counter.
    coin = Image.open(os.path.join(src, BATTLE, "item", "19.png")).convert("RGBA")
    save(coin.crop(coin.getbbox()), "sprites", "coin.png")
    for slot, entries in ITEMS.items():
        for rarity, entry in enumerate(entries):
            number, turn, sat, val = (entry, 0, 1, 1) if isinstance(entry, int) else entry
            img = Image.open(os.path.join(src, BATTLE, "item", f"{number}.png")).convert("RGBA")
            if turn or sat != 1 or val != 1:
                img = tint(img, turn, sat, val)
            img = img.crop(img.getbbox())
            save(img, "sprites", "items", f"{slot}_{rarity}.png")


def build_chests(src):
    sheet = Image.open(os.path.join(src, BATTLE, "item", "chest", "sprite-sheet-127x88.png")).convert("RGBA")
    closed, opened = sheet.crop((0, 0, 127, 88)), sheet.crop((254, 0, 381, 88))
    box = union_box([closed, opened])
    closed, opened = closed.crop(box), opened.crop(box)
    base = [hex_rgb(c) for c in CHEST_BASE]
    for skin, colours in CHEST_SKINS.items():
        mapping = dict(zip(base, [hex_rgb(c) for c in colours]))
        save(apply_map(closed, mapping), "sprites", "chests", f"{skin}_closed.png")
        save(apply_map(opened, mapping), "sprites", "chests", f"{skin}_open.png")
        save(chest_icon(colours), "ui", "chests", f"{skin}.png")


def chest_icon(colours):
    """A 16x14 chest for the pile and the buttons, in the same colours as the big one, at UI scale."""
    main, shade, planks, trim = [hex_rgb(c) + (255,) for c in colours]
    k, dark = (0, 0, 0, 255), (50, 37, 45, 255)
    rows = [
        "...kkkkkkkkkk...",
        "..kMMMMMMMMMMk..",
        ".kMMTTMMMMTTMMk.",
        ".kMMTTMMMMTTMMk.",
        ".kSSTTSSSSTTSSk.",
        ".kkkkkkkkkkkkkk.",
        ".kPPTTPkkPTTPPk.",
        ".kPPTTkTTkTTPPk.",
        ".kSSTTkTTkTTSSk.",
        ".kPPTTPkkPTTPPk.",
        ".kSSTTSSSSTTSSk.",
        ".kkkkkkkkkkkkkk.",
        "..kk........kk..",
        "................",
    ]
    colour = {"k": k, "M": main, "S": shade, "P": planks, "T": trim, "d": dark}
    img = Image.new("RGBA", (16, 14), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c != ".":
                px[x, y] = colour[c]
    return img.resize((16 * 2, 14 * 2), Image.NEAREST)


def build_ui():
    """Pixel-art 9-slice frames, pre-scaled so one art pixel is UI_SCALE screen pixels."""
    k, hi, fill, lo = (2, 2, 2, 255), (64, 54, 86, 255), (31, 26, 43, 255), (18, 14, 26, 255)
    white, light, mid, dark = (255, 255, 255, 255), (235, 235, 235, 255), (190, 190, 190, 255), (130, 130, 130, 255)
    clear = (0, 0, 0, 0)

    def frame(fill_colour, top, bottom, border=None, size=8):
        img = Image.new("RGBA", (size, size), fill_colour)
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
    # A gear slot: a sunken square that the rarity frame sits on.
    save(frame((22, 18, 32, 255), lo, hi), "ui", "slot.png")
    for name, rows in ICONS.items():
        save(pixel_icon(rows), "ui", "icons", f"{name}.png")
    icon = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    icon.alpha_composite(chest_icon(CHEST_SKINS["golden"]), (0, 2))
    save(icon.resize((64, 64), Image.NEAREST), "ui", "icon.png")


# 16x16 pixel icons for stats, slots and the HUD, drawn here (no CC0 pack has these), saved at 2x.
ICON_PALETTE = {
    "k": (20, 16, 26), "g": (242, 193, 78), "G": (176, 128, 44), "w": (240, 232, 214), "s": (190, 196, 210),
    "S": (110, 116, 134), "r": (226, 72, 72), "R": (150, 40, 52), "b": (140, 90, 46), "B": (92, 56, 28),
    "c": (110, 200, 230), "C": (60, 120, 180), "p": (170, 120, 240), "P": (110, 70, 170), "e": (110, 210, 120),
    "E": (50, 140, 70), "o": (255, 150, 60), "y": (255, 232, 90),
}
ICONS = {
    "atk": [
        "................", ".............kk.", "............kwsk", "...........kwsk.", "..........kwsk..",
        ".........kwsk...", "........kwsk....", ".......kwsk.....", "..kk..kwsk......", "..kgkkwsk.......",
        "...kgksk........", "....kgk.........", "...kgkgk........", "..kBk.kgk.......", ".kBk...kk.......",
        "..k.............",
    ],
    "hp": [
        "................", "................", "..kkk....kkk....", ".krrrk..krrrk...", "krwrrrkkrrrrrk..",
        "krwrrrrrrrrrrk..", "krrrrrrrrrrrrk..", "krrrrrrrrrrrrk..", ".krrrrrrrrrrk...", "..krrrrrrrrk....",
        "...krrrrrrk.....", "....krrrrk......", ".....krrk.......", "......kk........", "................",
        "................",
    ],
    "def": [
        "................", "..kkkkkkkkkkk...", "..kssswsssssk...", "..kswwwssssSk...", "..ksswsssssSk...",
        "..ksssssssSSk...", "..ksssssssSSk...", "..ksssssssSSk...", "...kssssssSk....", "...kssssssSk....",
        "....ksssSSk.....", ".....ksSSk......", "......kSk.......", ".......k........", "................",
        "................",
    ],
    "spd": [
        "................", "................", "....kkkkk.......", "...kyyyyyk......", "..kyyyyyk.......",
        ".kyyyyyk........", "kyyyyyyykkkk....", "kkkkkyyyyyyk....", "....kyyyyyk.....", "...kyyyyyk......",
        "..kyyyyk........", "..kyyyk.........", ".kyyk...........", ".kyk............", ".kk.............",
        "................",
    ],
    "crit": [
        "................", ".......kk.......", ".......kyk......", "......kyyk......", "......kyyyk.....",
        "kkkkkkyyyykkkkk.", ".kyyyyyyyyyyyk..", "..kyyyyyyyyyk...", "...kyyyyyyyk....", "...kyyyyyyyk....",
        "..kyyyykyyyyk...", "..kyyk..kkyyk...", ".kyk......kyyk..", ".kk........kk...", "................",
        "................",
    ],
    "critdmg": [
        "................", ".......k........", "......kok.......", "..k..kook..k....", ".kok.koook.kok..",
        ".koookoyookook..", "..kooooyyoook...", "..kooyyyyyook...", ".koooyyyyyoook..", ".kooyyyyyyyook..",
        "..koyyyyyyyok...", "...kooyyyyok....", "....kkoooook....", "......kkkk......", "................",
        "................",
    ],
    "lifesteal": [
        "................", "......kk........", ".....krrk.......", ".....krrk.......", "....krrrrk......",
        "....krrrrk......", "...krrrrrrk.....", "...krrwrrrk.....", "..krrwrrrrrk....", "..krwrrrrrrk....",
        "..krrrrrrrRk....", "...krrrrrRk.....", "....kRRRRk......", ".....kkkk.......", "................",
        "................",
    ],
    "dodge": [
        "................", "................", "...kkkk.........", "..kccccck.......", ".kcck..kcck.....",
        ".kck.....kck....", "..k.......kck...", "...........kck..", "..........kck...", ".kkkk....kck....",
        "kcccck..kck.....", ".kcccckkck......", "..kccccck.......", "...kkkkk........", "................",
        "................",
    ],
    "regen": [
        "................", "......kkkk......", "......keek......", "......keek......", "......keek......",
        "..kkkkkeekkkkk..", "..keeeeeeeeeek..", "..keeeeeeeeeek..", "..kkkkkeekkkkk..", "......keek......",
        "......keek......", "......keek......", "......kkkk......", "................", "................",
        "................",
    ],
    "double": [
        "................", "..k......k......", ".kwk....kwk.....", "..kwk....kwk....", "...kwk....kwk...",
        "....kwk....kwk..", ".....kwk....kwk.", "......kwk....kk.", ".......kk.......", "................",
        "..k......k......", ".kwk....kwk.....", "..kwk....kwk....", "...kk.....kk....", "................",
        "................",
    ],
    "stun": [
        "................", "....k...........", "...kyk......k...", "....k......kyk..", "...........k....",
        "......kkkk......", ".....kyyyyk.....", "....kyykkyyk....", "....kykyykyk....", "....kykkyyyk....",
        ".....kyyyyk.....", "......kkkk......", ".k..............", "kyk.......k.....", ".k.......kyk....",
        ".........k......",
    ],
    "gold": [
        "................", "................", ".....kkkkkk.....", "...kkggggggkk...", "..kggggwwgggGk..",
        "..kgggwggggGGk..", ".kggggGGGGgGGGk.", ".kgggGgggggGGGk.", ".kgggGgggggGGGk.", ".kggggGGGGgGGGk.",
        "..kggggggggGGk..", "..kGgggggggGGk..", "...kkGGGGGGkk...", ".....kkkkkk.....", "................",
        "................",
    ],
    "chest": [
        "................", "................", "...kkkkkkkkkk...", "..kbbbbbbbbbbk..", ".kbbggbbbbggbbk.",
        ".kBBggBBBBggBBk.", ".kkkkkkkkkkkkkk.", ".kbbggbkkbggbbk.", ".kbbggkggkggbbk.", ".kBBggbkkbggBBk.",
        ".kbbggbbbbggbbk.", ".kBBggBBBBggBBk.", ".kkkkkkkkkkkkkk.", "................", "................",
        "................",
    ],
    "luck": [
        "................", "....kkk..kkk....", "...keeek.keeek...", "...keEeekeeEek...", "....keeekeeek....",
        ".kkk.keekeek.kkk", "keeekkeeeeekkeeek", "keEeeeeeeeeeeEek", ".keeeekeekeeeek.", "..kkkkeekkkkkk..",
        "......kek.......", "......keek......", ".......kek......", "........kk......", "................",
        "................",
    ],
    "power": [
        "................", "........kk......", ".......kyk......", "......kyyk......", ".....kyyk.......",
        "....kyyyk.......", "...kyyyyykkk....", "..kkkkyyyyyk....", "......kyyyk.....", ".....kyyyk......",
        ".....kyyk.......", "....kyyk........", "....kyk.........", "....kk..........", "................",
        "................",
    ],
    "boss": [
        "................", "..k..........k..", ".krk........krk.", ".krrk.kkkk.krrk.", "..krkkwwwwkkrk..",
        "...kwwwwwwwwk...", "..kwwkkwwkkwwk..", "..kwwkrwwrkwwk..", "..kwwwwwwwwwwk..", "...kwwwkkwwwk...",
        "....kwkwkwkk....", "....kwwwwwwk....", ".....kkkkkk.....", "................", "................",
        "................",
    ],
    "skull": [
        "................", "....kkkkkkk.....", "...kwwwwwwwk....", "..kwwwwwwwwwk...", "..kwkkwwwkkwk...",
        "..kwkkwwwkkwk...", "..kwwwwkwwwwk...", "...kwwwwwwwk....", "....kwkwkwk.....", "....kkkkkkk.....",
        "................", "................", "................", "................", "................",
        "................",
    ],
    "auto": [
        "................", ".....kkkkkk.....", "....kccccccck...", "...kcck...kcck..", "..kck.......kk..",
        "..kck.....kkkkk.", "..kck......kcck.", "..kck.......kk..", "..kcck...kcck...", "...kccccccck....",
        "....kkkkkkk.....", "................", "................", "................", "................",
        "................",
    ],
}


def pixel_icon(rows):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows[:16]):
        for x, c in enumerate(row[:16]):
            if c in ICON_PALETTE:
                px[x, y] = ICON_PALETTE[c] + (255,)
    return img.resize((32, 32), Image.NEAREST)


def build_sounds(superpowers, kenney):
    out = os.path.join(ASSETS, "audio")
    if os.path.isdir(out):
        shutil.rmtree(out)
    for group, sources in SOUNDS.items():
        for i, (pack, path) in enumerate(sources):
            src = os.path.join(superpowers, "ninja-adventure", "sounds", path) if pack == NINJA else os.path.join(kenney, pack, path)
            dest = os.path.join(out, group, f"{i}.ogg")
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            shutil.copyfile(src, dest)
    for name, (folder, file) in MUSIC.items():
        dest = os.path.join(ASSETS, "music", f"{name}.ogg")
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(os.path.join(superpowers, folder, file), dest)


def build_fonts(kenney):
    dest = os.path.join(ASSETS, "fonts")
    os.makedirs(dest, exist_ok=True)
    shutil.copyfile(os.path.join(kenney, "kenney_fontpackage", "Fonts", "Kenney Mini.ttf"), os.path.join(dest, "kenney_mini.ttf"))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", default=os.path.join(os.path.expanduser("~"), ".cache", "lootfall-art"))
    args = parser.parse_args()
    os.makedirs(args.cache, exist_ok=True)
    superpowers = fetch(*SUPERPOWERS, args.cache)
    kenney = fetch(*KENNEY, args.cache)
    for folder in ("sprites", "backgrounds", "ui"):
        path = os.path.join(ASSETS, folder)
        if os.path.isdir(path):
            shutil.rmtree(path)
    build_actors(superpowers)
    build_fx(superpowers)
    build_backgrounds(superpowers)
    build_items(superpowers)
    build_chests(superpowers)
    build_ui()
    build_sounds(superpowers, kenney)
    build_fonts(kenney)
    print("assets rebuilt in", ASSETS)


if __name__ == "__main__":
    main()
