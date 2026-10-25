#!/usr/bin/env python3
"""Builds assets/audio and assets/music from the CC0 source packs. Re-run after changing a mapping below.

Sources (all CC0, see CREDITS.md):
  - Superpowers Asset Packs by Pixel-boy / Sparklin Labs  https://github.com/sparklinlabs/superpowers-asset-packs
    (the RPG Battle System pack's music, the Ninja Adventure pack's music and sounds)
  - Kenney RPG Audio, Impact Sounds, Interface Sounds, Casino Audio, Music Jingles
    https://kenney.nl (mirror: github.com/ETdoFresh/kenney.nl)

The pictures are built by tools/placeholders.py (stand-ins) and tools/art.py (the game's own art).

Usage: python3 tools/import_audio.py [--cache DIR]
Sources are cloned (partially) into the cache on first run; ffmpeg trims trailing silence when it's installed.
"""
import argparse
import os
import shutil
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")

SUPERPOWERS = ("https://github.com/sparklinlabs/superpowers-asset-packs", [
    "rpg-battle-system/music", "ninja-adventure/musics", "ninja-adventure/sounds", "LICENSE.txt",
])
KENNEY = ("https://github.com/ETdoFresh/kenney.nl", [
    "kenney_rpgaudio", "kenney_impactsounds", "kenney_interfacesounds", "kenney_casinoaudio", "kenney_digitalaudio",
    "kenney_musicjingles",
])

BATTLE = "rpg-battle-system"

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


def fetch(url, paths, cache):
    dest = os.path.join(cache, url.rstrip("/").split("/")[-1])
    if not os.path.isdir(dest):
        subprocess.run(["git", "clone", "-q", "--filter=blob:none", "--no-checkout", "--depth", "1", url, dest], check=True)
    subprocess.run(["git", "-C", dest, "checkout", "HEAD", "--", *paths], check=True)
    return dest


def trimmed_copy(src, dest):
    """Copies a sound without its trailing silence (some are padded to seconds of nothing, which holds a voice), via
    ffmpeg; a plain copy if ffmpeg isn't there."""
    if shutil.which("ffmpeg") is None:
        shutil.copyfile(src, dest)
        return
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", src, "-af",
                    "areverse,silenceremove=start_periods=1:start_threshold=-60dB,areverse,apad=pad_dur=0.05",
                    "-c:a", "libvorbis", "-q:a", "6", dest], check=True)


def build_sounds(superpowers, kenney):
    out = os.path.join(ASSETS, "audio")
    if os.path.isdir(out):
        shutil.rmtree(out)
    for group, sources in SOUNDS.items():
        for i, (pack, path) in enumerate(sources):
            src = os.path.join(superpowers, "ninja-adventure", "sounds", path) if pack == NINJA else os.path.join(kenney, pack, path)
            dest = os.path.join(out, group, f"{i}.ogg")
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            trimmed_copy(src, dest)
    for name, (folder, file) in MUSIC.items():
        dest = os.path.join(ASSETS, "music", f"{name}.ogg")
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(os.path.join(superpowers, folder, file), dest)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", default=os.path.join(os.path.expanduser("~"), ".cache", "lootfall-audio"))
    args = parser.parse_args()
    os.makedirs(args.cache, exist_ok=True)
    superpowers = fetch(*SUPERPOWERS, args.cache)
    kenney = fetch(*KENNEY, args.cache)
    # Godot's .import files (with each asset's uid) are kept for whatever is rebuilt, so a re-run doesn't churn them.
    kept = {}
    for folder in ("audio", "music"):
        for path, _, files in os.walk(os.path.join(ASSETS, folder)):
            for name in files:
                if name.endswith(".import"):
                    with open(os.path.join(path, name)) as f:
                        kept[os.path.join(path, name)] = f.read()
    build_sounds(superpowers, kenney)
    for path, text in kept.items():
        if os.path.exists(path[:-len(".import")]):
            with open(path, "w") as f:
                f.write(text)
    print("audio rebuilt in", ASSETS)


if __name__ == "__main__":
    main()
