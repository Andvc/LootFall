#!/usr/bin/env python3
"""Builds LootFall's placeholder art from GitHub-hosted packs: stand-ins for the AI-made final art, processed the way
docs/art-direction.md describes ("Placeholders"), so they stage the inked cel look and exercise the same code paths.

  monsters   Ancient Beast's painted creatures, matted (rembg), cropped, outlined in ink, turned to face left
             -> assets/monsters/<key>.png and assets/monsters/monsters.json (size and foot point of each)
  backdrops  painted scenes from Valyria Tear and Ancient Beast, cut to the lane's band, desaturated, value-compressed
             and softened -> assets/backdrops/<realm>.jpg and assets/backdrops/realms.json (colours sampled for the
             sky, ground and foreground the game draws itself)
  icons      game-icons silhouettes filled with the family's material ramp, lit from the top left, outlined in ink
             -> assets/icons/<family>.png
  vfx        Kenney's Particle Pack (white on transparent) -> assets/vfx/<name>.png
  fonts      Cinzel, Barlow Semi Condensed and Lilita One from google/fonts (final, not placeholders)
  appicon    the game's icon, drawn here: an open chest pouring light -> assets/icon.png

Usage: python3 tools/placeholders.py [--cache DIR] [--only appicon,monsters,backdrops,icons,vfx,fonts]
Needs Pillow; rembg (monsters) and cairosvg (icons). Sources are fetched by blobless sparse checkouts into the cache.
The monster and realm keys must match scripts/Game/Bestiary.cs, the icon families Describe.Family in
scripts/Game/Describe.cs.
"""
import argparse
import json
import math
import os
import re
import subprocess
import urllib.request

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")

INK = (0x15, 0x10, 0x1F)

SOURCES = {
    "ancientbeast": ("https://github.com/FreezingMoon/AncientBeast.git", ["assets/units/artwork", "assets/locations"]),
    "valyriatear": ("https://github.com/ValyriaTear/ValyriaTear.git", ["data/battles/battle_scenes"]),
    "game-icons": ("https://github.com/game-icons/icons.git", None),
    "kenney": ("https://github.com/ETdoFresh/kenney.nl.git", ["particlePack_1.1"]),
}

FONTS = {
    "Cinzel.ttf": "ofl/cinzel/Cinzel%5Bwght%5D.ttf",
    "BarlowSemiCondensed-Medium.ttf": "ofl/barlowsemicondensed/BarlowSemiCondensed-Medium.ttf",
    "BarlowSemiCondensed-SemiBold.ttf": "ofl/barlowsemicondensed/BarlowSemiCondensed-SemiBold.ttf",
    "BarlowSemiCondensed-Bold.ttf": "ofl/barlowsemicondensed/BarlowSemiCondensed-Bold.ttf",
    "LilitaOne-Regular.ttf": "ofl/lilitaone/LilitaOne-Regular.ttf",
}
FONT_LICENCES = {"Cinzel-OFL.txt": "ofl/cinzel/OFL.txt", "Barlow-OFL.txt": "ofl/barlowsemicondensed/OFL.txt",
                 "LilitaOne-OFL.txt": "ofl/lilitaone/OFL.txt"}

# ---------------------------------------------------------------------------------------------------------------------
# Monsters: bestiary key -> (Ancient Beast artwork, faces right in the source and must be flipped)
# ---------------------------------------------------------------------------------------------------------------------
MONSTERS = {
    # Greenwood
    "bloblet": ("Gumble", False), "toadstool": ("Toxic Shroom", False), "moss_hound": ("Moss Hound", True),
    "thicket_glutton": ("Papa Eggplant", True), "old_tusk": ("Stomper", False),
    # Sunscorch Dunes
    "dune_scorpion": ("Scorpius", False), "sand_raptor": ("Uncle Fungus", False), "carrion_harpy": ("Scavenger", False),
    "stitched_dead": ("Mr. Stitches", False), "sand_warlord": ("Golden Wyrm", False),
    # Frostpeak
    "snow_biter": ("Snow Bunny", False), "rime_wolf": ("Vehemoth", False), "frost_siren": ("Deep Beauty", True),
    "rime_stalker": ("Knightmare", True), "frost_warden": ("Royal Guard", False),
    # Drowned Mire
    "tide_shrimp": ("Blue Shrimp", False), "drowned_maiden": ("Miss Creeper", False), "bog_gawker": ("Swampler", False),
    "pale_grasper": ("Headless", True), "deep_one": ("Flayed", False),
    # Ashlands
    "cinder_phoenix": ("Volpyr", False), "lavamander": ("Lavamander", False), "magma_worm": ("Infernal", False),
    "spined_hellbeast": ("Spikes", True), "cinder_colossus": ("Vulcan", False),
    # Fallen Ruins
    "risen_knight": ("Greedy Knight", False), "restless_shade": ("Shadow Leech", False),
    "living_armor": ("Living Armor", False), "dark_priest": ("Dark Priest", False), "gilded_maiden": ("Gilded Maiden", False),
    # Deep Caverns
    "batmadillo": ("Batmadillo", True), "pale_ravager": ("Abolished", False), "swine_thug": ("Swine Thug", True),
    "chained_horror": ("Horn Head", False), "vermithrax": ("Chimera", True),
    # Starfall Rift
    "cycloper": ("Cycloper", False), "rift_wolf": ("Cyber Wolf", True), "crystalis": ("Crystalis", False),
    "mangler": ("Mangler", False), "hollow_star": ("Metalist", False),
}

# ---------------------------------------------------------------------------------------------------------------------
# Backdrops: realm -> (source, file, crop box in the source, tint, how far toward the tint)
# ---------------------------------------------------------------------------------------------------------------------
BACKDROPS = {
    "greenwood": ("valyriatear", "data/battles/battle_scenes/forest_background.png", (0, 0, 1024, 307), (70, 110, 80), 0.15),
    "dunes": ("valyriatear", "data/battles/battle_scenes/desert_background.png", (0, 60, 1024, 367), (190, 140, 90), 0.12),
    "frostpeak": ("ancientbeast", "assets/locations/Frozen Wall.jpg", (0, 0, 1920, 576), (150, 175, 210), 0.2),
    "mire": ("valyriatear", "data/battles/battle_scenes/forest_background_evening.png", (0, 0, 1026, 308), (70, 90, 120), 0.25),
    "ashlands": ("ancientbeast", "assets/locations/Desolated Canyon.jpg", (0, 0, 1920, 576), (140, 60, 50), 0.15),
    "ruins": ("valyriatear", "data/battles/battle_scenes/mountain_shrine.png", (0, 0, 1024, 307), (110, 100, 120), 0.2),
    "caverns": ("ancientbeast", "assets/locations/Shadow Cave.jpg", (0, 0, 1920, 576), (70, 60, 80), 0.3),
    "starfall": ("ancientbeast", "assets/locations/Dragon Bones.jpg", (0, 0, 1920, 576), (80, 60, 140), 0.55),
}

# ---------------------------------------------------------------------------------------------------------------------
# Icons: item family -> (game-icons file, material). Families as Describe.Family names them.
# ---------------------------------------------------------------------------------------------------------------------
TATTOO = "ink"
ICONS = {
    # The families the first content had, still in use.
    "main_hand": ("lorc/broadsword", "steel"), "off_hand": ("willdabeast/round-shield", "iron"),
    "quiver": ("delapouite/quiver", "leather"), "breastplate": ("lorc/breastplate", "steel"),
    "cloak": ("lucasms/cloak", "cloth"), "mantle": ("delapouite/cape", "cloth"), "tabard": ("lorc/robe", "cloth"),
    "belt": ("lucasms/belt", "leather"), "gorget": ("delapouite/heavy-collar", "iron"),
    "visor": ("delapouite/overlord-helm", "steel"), "hood": ("lorc/hood", "cloth"),
    "pauldron": ("delapouite/shoulder-armor", "steel"), "gauntlet": ("delapouite/gauntlet", "steel"),
    "vambrace": ("delapouite/bracer", "leather"), "cuisse": ("delapouite/leg-armor", "steel"),
    "boot": ("lorc/leather-boot", "leather"), "amulet": ("lorc/gem-pendant", "gold"),
    "necklace": ("delapouite/pearl-necklace", "gold"), "choker": ("delapouite/heart-necklace", "gold"),
    "circlet": ("delapouite/tiara", "gold"), "diadem": ("delapouite/jewel-crown", "gold"),
    "hair_pin": ("lorc/comb", "gold"), "earring": ("delapouite/drop-earrings", "gold"),
    "bracelet": ("delapouite/double-necklace", "gold"), "armlet": ("delapouite/arm-bandage", "gold"),
    "anklet": ("delapouite/primitive-necklace", "gold"), "ring": ("delapouite/diamond-ring", "gold"),
    "toe_ring": ("skoll/big-diamond-ring", "gold"), "brooch": ("delapouite/ribbon-medal", "gold"),
    "sash": ("delapouite/bow-tie-ribbon", "cloth"), "scabbard": ("delapouite/scabbard", "leather"),
    "monocle": ("lorc/spectacles", "gold"), "pouch": ("seregacthtuf/pouch-with-beads", "leather"),
    "garter": ("delapouite/headband-knot", "cloth"), "charm": ("lorc/charm", "gold"),
    "brow_tattoo": ("lorc/tribal-mask", TATTOO), "neck_tattoo": ("lorc/spiral-bloom", TATTOO),
    "chest_tattoo": ("lorc/star-swirl", TATTOO), "back_tattoo": ("delapouite/snake-spiral", TATTOO),
    "shoulder_tattoo": ("lorc/scarab-beetle", TATTOO), "hand_tattoo": ("lorc/crystal-shine", TATTOO),
    "thigh_tattoo": ("lorc/feather-wound", TATTOO), "calf_tattoo": ("lorc/spiral-thrust", TATTOO),
    "familiar": ("lorc/raven", "cloth"), "saddle": ("delapouite/saddle", "leather"),
    "barding": ("willdabeast/chain-mail", "iron"), "reins": ("delapouite/bowen-knot", "leather"),
    "lantern": ("lorc/lantern", "iron"), "halo": ("lorc/spiked-halo", "astral"),
    "wings": ("lorc/angel-wings", "astral"), "aura": ("lorc/aura", "astral"),
    "starcrown": ("delapouite/imperial-crown", "astral"), "comet": ("lorc/comet-spark", "astral"),
    "eclipse": ("lorc/eclipse", "astral"), "constellation": ("delapouite/star-formation", "astral"),
    # Head
    "sallet": ("delapouite/light-helm", "steel"), "great_helm": ("delapouite/black-knight-helm", "steel"),
    "nasal_helm": ("delapouite/viking-helmet", "iron"), "horned_helm": ("lorc/horned-helm", "iron"),
    "coif": ("lorc/cowled", "iron"), "arming_cap": ("delapouite/winter-hat", "cloth"),
    "wizard_hat": ("lorc/pointy-hat", "cloth"), "crown": ("lorc/crown", "gold"), "horns": ("lorc/bull-horns", "bone"),
    "horn_ring": ("lorc/linked-rings", "gold"), "horn_tip": ("delapouite/rhinoceros-horn", "steel"),
    "dragon_crest": ("delapouite/spiked-dragon-head", "gold"), "crest": ("lorc/crested-helmet", "steel"),
    "plume": ("lorc/feather", "cloth"), "hatband": ("lorc/ribbon", "cloth"), "feather": ("lorc/two-feathers", "cloth"),
    "bevor": ("delapouite/closed-barbute", "steel"), "cheek_fitting": ("delapouite/spartan-helmet", "steel"),
    "mouth_guard": ("delapouite/mecha-mask", "steel"), "mask": ("lorc/iron-mask", "iron"),
    "eyepatch": ("delapouite/eyepatch", "leather"), "spectacles": ("lorc/spectacles", "gold"),
    "goggles": ("delapouite/steampunk-goggles", "leather"), "lens_tint": ("delapouite/protection-glasses", "astral"),
    "chain": ("lorc/perpendicular-rings", "gold"), "third_eye": ("lorc/third-eye", "astral"),
    "forehead_jewel": ("delapouite/fire-gem", "gold"), "ear_tip": ("delapouite/elf-ear", "gold"),
    "ear_cuff": ("delapouite/crystal-earrings", "gold"), "helix_ring": ("delapouite/earrings", "gold"),
    "ear_chain": ("lorc/andromeda-chain", "gold"), "septum_ring": ("delapouite/nose-front", "gold"),
    "nose_stud": ("delapouite/nose-side", "gold"), "nose_chain": ("skoll/breaking-chain", "gold"),
    "lip_ring": ("lorc/lips", "gold"), "tongue_stud": ("delapouite/tongue", "gold"),
    "gold_teeth": ("lorc/front-teeth", "gold"), "fang_caps": ("lorc/pretty-fangs", "gold"),
    "beard_rings": ("lorc/beard", "gold"), "braid_beads": ("delapouite/prayer-beads", "wood"),
    "hair_ribbon": ("lorc/broken-ribbon", "cloth"), "kohl": ("delapouite/eyelashes", TATTOO),
    "war_paint": ("delapouite/ceremonial-mask", TATTOO),
    # Neck and torso
    "scarf": ("delapouite/tie", "cloth"), "neck_cloth": ("delapouite/bow-tie", "cloth"),
    "aventail": ("lorc/wavy-chains", "iron"), "torc": ("cathelineau/torc", "gold"),
    "torc_terminal": ("lorc/coiling-curl", "gold"), "clasp": ("delapouite/safety-pin", "gold"),
    "chain_of_office": ("delapouite/intricate-necklace", "gold"), "shirt": ("lucasms/shirt", "cloth"),
    "gambeson": ("lorc/armor-vest", "cloth"), "mail_shirt": ("lorc/mail-shirt", "iron"),
    "hauberk": ("lorc/scale-mail", "steel"), "cuirass": ("delapouite/chest-armor", "steel"),
    "brigandine": ("delapouite/leather-armor", "leather"), "full_plate_harness": ("lorc/layered-armor", "steel"),
    "plackart": ("delapouite/abdominal-armor", "steel"), "backplate": ("lorc/armoured-shell", "steel"),
    "surcoat": ("delapouite/clothes", "cloth"), "robe": ("lorc/robe", "cloth"),
    "cape": ("delapouite/vampire-cape", "cloth"), "medal": ("lorc/medal", "gold"),
    "navel_ring": ("delapouite/pearl-earring", "gold"), "heartstone": ("lorc/mineral-heart", "astral"),
    "backpack": ("delapouite/backpack", "leather"), "bedroll": ("delapouite/sleeping-bag", "cloth"),
    "back_scabbard": ("lorc/crossed-swords", "leather"), "lining": ("delapouite/rolled-cloth", "cloth"),
    "embroidery": ("lorc/sewing-needle", "cloth"), "heraldry": ("lorc/eagle-emblem", "gold"),
    "engraving": ("delapouite/chisel", "iron"), "paint": ("delapouite/paint-brush", "wood"),
    # Waist and legs
    "girdle": ("delapouite/black-belt", "leather"), "fauld": ("delapouite/belt-armor", "steel"),
    "faulds_and_tassets": ("delapouite/metal-skirt", "steel"), "tasset": ("delapouite/armor-cuisses", "steel"),
    "codpiece": ("delapouite/underwear-shorts", "steel"), "loincloth": ("delapouite/loincloth", "cloth"),
    "thigh_sheath": ("lorc/plain-dagger", "leather"), "trousers": ("lorc/trousers", "cloth"),
    "breeches": ("lucasms/trousers", "leather"), "mail_leggings": ("irongamer/armored-pants", "iron"),
    "chausses": ("delapouite/leg", "iron"), "leg_harness": ("delapouite/robot-leg", "steel"),
    "poleyn": ("delapouite/knee-cap", "steel"), "greave": ("delapouite/greaves", "steel"),
    "leg_wraps": ("lorc/bandage-roll", "cloth"), "knee_wrap": ("delapouite/knee-bandage", "cloth"),
    "ankle_guard": ("delapouite/foot-plaster", "steel"),
    # Feet
    "sock": ("delapouite/socks", "cloth"), "sandal": ("delapouite/sandal", "leather"),
    "sabaton": ("delapouite/metal-boot", "steel"), "armoured_boot": ("lorc/boots", "steel"),
    "toe_cap": ("lorc/boot-stomp", "steel"), "laces": ("delapouite/sewing-string", "leather"),
    "buckle": ("lorc/belt-buckles", "gold"), "foot_chain": ("lorc/gem-chain", "gold"),
    "spur": ("lorc/circular-sawblade", "iron"), "toenail_paint": ("lorc/barefoot", TATTOO),
    "toe_talons": ("lorc/bird-claw", "bone"),
    # Arms and hands
    "spaulder": ("lorc/shoulder-scales", "steel"), "besagew": ("lorc/rosa-shield", "steel"),
    "rerebrace": ("delapouite/armor-punch", "steel"), "couter": ("delapouite/elbow-pad", "steel"),
    "bracer": ("skoll/bracers", "leather"), "mail_sleeve": ("lorc/mailed-fist", "iron"),
    "arm_harness": ("lorc/mechanical-arm", "steel"), "full_arm_harness": ("delapouite/arm", "steel"),
    "arm_wraps": ("lorc/bandaged", "cloth"), "bangle_stack": ("delapouite/globe-ring", "gold"),
    "glove": ("delapouite/gloves", "leather"), "fingerless_glove": ("delapouite/winter-gloves", "cloth"),
    "hand_plate": ("lorc/metal-hand", "steel"), "knuckleduster": ("delapouite/brass-knuckles", "iron"),
    "knuckle_ring": ("delapouite/power-ring", "gold"), "hand_chain": ("lorc/crossed-chains", "gold"),
    "thimble": ("delapouite/bottle-cap", "iron"), "nail_lacquer": ("delapouite/fingernail", TATTOO),
    "talons": ("lorc/steel-claws", "steel"), "claw_gauntlet": ("delapouite/wolverine-claws", "steel"),
    "wrist_blade": ("lorc/knife-thrust", "steel"), "palm_crystal": ("lorc/floating-crystal", "astral"),
    # Held, and what's set in it
    "great_weapon": ("delapouite/two-handed-sword", "steel"), "shield": ("lorc/edged-shield", "iron"),
    "shield_rim": ("lorc/bordered-shield", "iron"), "shield_boss": ("delapouite/spiked-shield", "iron"),
    "shield_straps": ("delapouite/knot", "leather"), "crossguard": ("lorc/sword-hilt", "steel"),
    "pommel": ("delapouite/diamond-hilt", "gold"), "grip_wrap": ("delapouite/hand-bandage", "leather"),
    "tassel": ("lorc/spinning-ribbons", "cloth"), "blade_oil": ("lorc/droplets", "gold"),
    "focus_orb": ("lorc/concentration-orb", "astral"), "torch": ("delapouite/torch", "wood"),
    "lance_rest": ("lorc/spears", "iron"), "arrowheads": ("lorc/arrowhead", "iron"),
    "fletching": ("lorc/arrow-flights", "cloth"), "gem": ("lorc/gems", "gold"), "rune": ("lorc/rune-stone", TATTOO),
    "enchantment": ("delapouite/sparkles", "astral"), "spike": ("sbed/spikes", "iron"),
    "potion": ("caro-asercion/round-potion", "astral"), "star_charm": ("delapouite/falling-star", "gold"),
    "keepsake": ("delapouite/heart-key", "gold"),
    # Grown
    "tail": ("delapouite/reptile-tail", "leather"), "tail_ring": ("lorc/swirl-ring", "gold"),
    "tail_tuft": ("delapouite/hair-strands", "leather"), "tail_spike": ("lorc/spiked-tail", "bone"),
    "wing_blade": ("lorc/winged-sword", "steel"),
    # At your side
    "shoulder_familiar": ("lorc/sparrow", "leather"), "shoulder_lantern": ("lorc/lantern-flame", "iron"),
    "falcon_hood": ("delapouite/egyptian-bird", "leather"), "jesses": ("lorc/bird-limb", "leather"),
    "bell": ("lorc/ringing-bell", "gold"), "collar": ("lorc/spiked-collar", "gold"),
    "steed": ("lorc/horse-head", "leather"), "horseshoe": ("delapouite/horseshoe", "iron"),
    "saddlebags": ("delapouite/duffel-bag", "leather"), "war_banner": ("delapouite/vertical-banner", "cloth"),
    "shadow_cloak": ("lorc/shadow-follower", "cloth"), "ioun_stone": ("lorc/double-ringed-orb", "astral"),
    "halo_light": ("lorc/sunbeams", "astral"), "aura_mote": ("lorc/ball-glow", "astral"),
    # Ink
    "scalp_tattoo": ("lorc/oily-spiral", TATTOO), "nape_tattoo": ("delapouite/zigzag-hieroglyph", TATTOO),
    "cheek_tattoo": ("lorc/moebius-trefoil", TATTOO), "chin_tattoo": ("lorc/thorn-helix", TATTOO),
    "torso_tattoo": ("caro-asercion/heraldic-sun", TATTOO), "belly_tattoo": ("lorc/sun", TATTOO),
    "upper_arm_tattoo": ("lorc/barbed-sun", TATTOO), "elbow_tattoo": ("lorc/light-thorny-triskelion", TATTOO),
    "forearm_tattoo": ("lorc/thorny-vine", TATTOO), "sleeve_tattoo": ("lorc/thorny-tentacle", TATTOO),
    "wrist_tattoo": ("lorc/fluffy-trefoil", TATTOO), "palm_sigil": ("lorc/magic-palm", TATTOO),
    "thumb_tattoo": ("delapouite/finger-print", TATTOO), "ring_tattoo": ("lorc/swirl-string", TATTOO),
    "knuckle_letters": ("delapouite/hieroglyph-y", TATTOO), "knee_tattoo": ("lorc/heavy-thorny-triskelion", TATTOO),
    "leg_sleeve_tattoo": ("delapouite/hieroglyph-legs", TATTOO), "ankle_tattoo": ("lorc/footprint", TATTOO),
    "foot_tattoo": ("lorc/wingfoot", TATTOO),
    # Not a slot: the fallback for any family without its own picture, and the chest.
    "unknown": ("lorc/crystal-ball", "iron"), "chest": ("delapouite/chest", "wood"),
}

# Shadow, mid, light, highlight (None: no highlight). Shared with docs/art-direction.md's ramp library.
RAMPS = {
    "iron": ("#3b3f4a", "#646b78", "#959dab", "#d4dae4"),
    "steel": ("#47506a", "#7a87a3", "#b4c0d6", "#eef3fb"),
    "leather": ("#4a2f22", "#7a5038", "#a77552", None),
    "cloth": ("#3a2b45", "#5f4a72", "#8a76a0", None),
    "gold": ("#7a4f12", "#c48a22", "#f2c45a", "#fff2bf"),
    "wood": ("#4b3220", "#7b5534", "#a77a4e", None),
    "bone": ("#7d7462", "#b8ad94", "#e6dcc4", None),
    "ink": ("#123a44", "#1f6b75", "#3fa3a8", "#a8f0ea"),
    "astral": ("#4a5a8a", "#8aa2d8", "#cfe0ff", "#ffffff"),
}

VFX = {
    "spark1": "spark_01", "spark2": "spark_02", "spark3": "spark_03", "star1": "star_01", "star2": "star_02",
    "glow": "circle_05", "slash1": "slash_01", "slash2": "slash_02", "smoke1": "smoke_01",
}


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# ---------------------------------------------------------------------------------------------------------------------
# Fetching
# ---------------------------------------------------------------------------------------------------------------------

def fetch(cache, name):
    url, folders = SOURCES[name]
    path = os.path.join(cache, name)
    if not os.path.isdir(path):
        if folders is None:
            subprocess.run(["git", "clone", "-q", "--depth=1", url, path], check=True)
        else:
            subprocess.run(["git", "clone", "-q", "--depth=1", "--filter=blob:none", "--sparse", url, path], check=True)
    if folders:
        subprocess.run(["git", "-C", path, "sparse-checkout", "set", *folders], check=True)
    return path


def download(url, dest):
    if os.path.exists(dest):
        return
    with urllib.request.urlopen(url, timeout=60) as r, open(dest, "wb") as f:
        f.write(r.read())


# ---------------------------------------------------------------------------------------------------------------------
# Shared processing (the same steps the AI pipeline's import runs)
# ---------------------------------------------------------------------------------------------------------------------

def crop_to_alpha(im, threshold=40, pad=0):
    a = im.split()[3].point(lambda v: 255 if v > threshold else 0)
    box = a.getbbox()
    if box is None:
        return im
    x0, y0, x1, y1 = box
    return im.crop((max(0, x0 - pad), max(0, y0 - pad), min(im.width, x1 + pad), min(im.height, y1 + pad)))


def ink_outline(im, width):
    """Puts one uniform ink outline round everything opaque (rounded, antialiased), the inked cel look's first rule."""
    pad = width + 2
    canvas = Image.new("RGBA", (im.width + 2 * pad, im.height + 2 * pad), (0, 0, 0, 0))
    canvas.paste(im, (pad, pad))
    solid = canvas.split()[3].point(lambda v: 255 if v > 110 else 0)
    grown = solid.filter(ImageFilter.GaussianBlur(width * 0.55)).point(lambda v: 255 if v > 6 else 0)
    grown = grown.filter(ImageFilter.GaussianBlur(0.7))
    ink = Image.new("RGBA", canvas.size, INK + (0,))
    ink.putalpha(grown)
    return Image.alpha_composite(ink, canvas)


def foot_point(im):
    """Where it stands: the middle of what's drawn in its lowest rows."""
    a = im.split()[3]
    w, h = im.size
    px = a.load()
    bottom = None
    for y in range(h - 1, -1, -1):
        if any(px[x, y] > 128 for x in range(0, w, 2)):
            bottom = y
            break
    if bottom is None:
        return w // 2, h - 1
    band = max(1, h // 25)
    xs = [x for y in range(max(0, bottom - band), bottom + 1) for x in range(w) if px[x, y] > 128]
    return (min(xs) + max(xs)) // 2, bottom


# ---------------------------------------------------------------------------------------------------------------------
# Monsters
# ---------------------------------------------------------------------------------------------------------------------

def build_monsters(cache):
    from rembg import new_session, remove
    src = os.path.join(fetch(cache, "ancientbeast"), "assets/units/artwork")
    out = os.path.join(ASSETS, "monsters")
    os.makedirs(out, exist_ok=True)
    session = new_session("isnet-general-use")
    info = {}
    for key, (name, flip) in MONSTERS.items():
        im = Image.open(os.path.join(src, name + ".jpg")).convert("RGB")
        cut = remove(im, session=session)
        # Smoke and haze round painted creatures go: keep what's clearly there, a little punchier than the source.
        r, g, b, a = cut.split()
        a = a.point(lambda v: 0 if v < 60 else min(255, int((v - 60) * 255 / 120)))
        rgb = Image.merge("RGB", (r, g, b))
        rgb = ImageOps.autocontrast(rgb, cutoff=1)
        cut = Image.merge("RGBA", (*rgb.split(), a))
        cut = crop_to_alpha(cut, threshold=60)
        if flip:
            cut = ImageOps.mirror(cut)
        cut = ink_outline(cut, max(4, round(cut.height * 0.014)))
        cut.save(os.path.join(out, key + ".png"), optimize=True)
        fx, fy = foot_point(cut)
        info[key] = {"w": cut.width, "h": cut.height, "footX": fx, "footY": fy, "source": name}
        print(f"  monster {key:18s} {name:14s} {cut.width}x{cut.height}")
    with open(os.path.join(out, "monsters.json"), "w") as f:
        json.dump(info, f, indent=1, sort_keys=True)


# ---------------------------------------------------------------------------------------------------------------------
# Backdrops
# ---------------------------------------------------------------------------------------------------------------------

def grade(im, tint, toward):
    """The two-layer rule: backdrops desaturated, values compressed into 15-75%, softened, nudged toward the realm's
    colour, so anything outlined in front of them reads first."""
    import numpy as np
    hsv = np.asarray(im.convert("HSV"), dtype=np.float32) / 255.0
    hsv[..., 1] *= 0.5
    v = 0.15 + hsv[..., 2] * 0.6
    hsv[..., 2] = v
    rgb = np.asarray(Image.fromarray((hsv * 255).astype(np.uint8), "HSV").convert("RGB"), dtype=np.float32) / 255.0
    t = np.array(tint, dtype=np.float32) / 255.0
    rgb = rgb * (1 - toward) + t[None, None, :] * toward * (0.4 + v)[..., None]
    out = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB")
    return out.filter(ImageFilter.GaussianBlur(1.2))


def average(im, box):
    region = im.crop(box).resize((1, 1), Image.Resampling.BOX)
    return "#%02x%02x%02x" % region.getpixel((0, 0))[:3]


def build_backdrops(cache):
    out = os.path.join(ASSETS, "backdrops")
    os.makedirs(out, exist_ok=True)
    realms = {}
    for realm, (source, path, box, tint, toward) in BACKDROPS.items():
        root = fetch(cache, source)
        im = Image.open(os.path.join(root, path)).convert("RGB").crop(box)
        im = im.resize((1920, 576), Image.Resampling.LANCZOS)
        im = grade(im, tint, toward)
        im.save(os.path.join(out, realm + ".jpg"), quality=88)
        realms[realm] = {
            "skyTop": average(im, (0, 0, 1920, 40)),
            "skyHorizon": average(im, (0, 250, 1920, 300)),
            "ground": average(im, (0, 520, 1920, 576)),
            "source": f"{source}: {path}",
        }
        print(f"  backdrop {realm:10s} {path}")
    with open(os.path.join(out, "realms.json"), "w") as f:
        json.dump(realms, f, indent=1, sort_keys=True)


# ---------------------------------------------------------------------------------------------------------------------
# Icons
# ---------------------------------------------------------------------------------------------------------------------

def silhouette(svg_path, size):
    import cairosvg
    with open(svg_path) as f:
        svg = f.read()
    # Each game-icons file draws a black square, then the white shape: keep only the shape.
    svg = re.sub(r'<path d="M0 0h512v512H0z"\s*/>', "", svg, count=1)
    svg = svg.replace('fill="#fff"', 'fill="#ffffff"')
    png = cairosvg.svg2png(bytestring=svg.encode(), output_width=size, output_height=size)
    import io
    im = Image.open(io.BytesIO(png)).convert("RGBA")
    return im.split()[3]


def shift(mask, dx, dy):
    return ImageChops.offset(mask, dx, dy)


def inked_icon(mask, material, size=256):
    """A cel-shaded icon from a silhouette: the material's mid tone, its light along the top-left edges, its shadow along
    the bottom-right, a thin highlight for metals, and the ink outline."""
    shadow, mid, light, hi = RAMPS[material]
    m = mask.point(lambda v: 255 if v > 127 else 0)
    k = max(2, size // 40)
    lit = ImageChops.subtract(m, shift(m, k, k))          # top-left edges: nothing just up-left of them
    dark = ImageChops.subtract(m, shift(m, -k, -k))       # bottom-right edges
    img = Image.new("RGBA", m.size, hex_rgb(mid) + (0,))
    img.putalpha(m)
    for colour, region in ((shadow, dark), (light, lit)):
        layer = Image.new("RGBA", m.size, hex_rgb(colour) + (255,))
        img.paste(layer, (0, 0), region)
    if hi:
        edge = ImageChops.subtract(m, shift(m, k // 2, k // 2))
        thin = ImageChops.multiply(edge, shift(lit, k // 2, k // 2).point(lambda v: 255 if v > 0 else 0))
        img.paste(Image.new("RGBA", m.size, hex_rgb(hi) + (255,)), (0, 0), thin)
    img.putalpha(m.filter(ImageFilter.GaussianBlur(0.6)))
    return img


def build_icons(cache):
    root = fetch(cache, "game-icons")
    out = os.path.join(ASSETS, "icons")
    os.makedirs(out, exist_ok=True)
    size = 256
    for family, (svg, material) in ICONS.items():
        mask = silhouette(os.path.join(root, svg + ".svg"), 1024).resize((size - 40, size - 40), Image.Resampling.LANCZOS)
        icon = inked_icon(mask, material, size - 40)
        icon = ink_outline(icon, 9)
        canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        canvas.alpha_composite(icon, ((size - icon.width) // 2, (size - icon.height) // 2))
        canvas.save(os.path.join(out, family + ".png"), optimize=True)
    print(f"  icons: {len(ICONS)} families")


# ---------------------------------------------------------------------------------------------------------------------
# Effects and fonts
# ---------------------------------------------------------------------------------------------------------------------

def build_vfx(cache):
    root = os.path.join(fetch(cache, "kenney"), "particlePack_1.1", "PNG (Transparent)")
    out = os.path.join(ASSETS, "vfx")
    os.makedirs(out, exist_ok=True)
    for name, src in VFX.items():
        im = Image.open(os.path.join(root, src + ".png")).convert("RGBA")
        im = im.resize((256, 256), Image.Resampling.LANCZOS)
        im.save(os.path.join(out, name + ".png"), optimize=True)
    print(f"  vfx: {len(VFX)} textures")


def build_fonts(cache):
    out = os.path.join(ASSETS, "fonts")
    os.makedirs(out, exist_ok=True)
    for name, path in {**FONTS, **FONT_LICENCES}.items():
        download("https://raw.githubusercontent.com/google/fonts/main/" + path, os.path.join(out, name))
    print(f"  fonts: {', '.join(FONTS)}")


def build_appicon(_cache):
    """The game's icon (project.godot's config/icon): an open chest pouring light, in the inked cel style, drawn at 4x
    and shrunk so its edges are smooth."""
    k = 4
    S = 256 * k
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    def box(x0, y0, x1, y1):
        return [v * k for v in (x0, y0, x1, y1)]

    def poly(points):
        return [(x * k, y * k) for x, y in points]

    ink = INK + (255,)
    d.rounded_rectangle(box(6, 6, 250, 250), radius=46 * k, fill=hex_rgb("#231a38") + (255,), outline=ink, width=6 * k)
    # The light from inside: a soft glow and rays.
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for i in range(14, 0, -1):
        r = i * 9 * k
        g.ellipse((128 * k - r, 112 * k - r, 128 * k + r, 112 * k + r), fill=hex_rgb("#ffb347") + (int(14 + (14 - i) * 6),))
    for a, b in [(-62, -48), (-30, -18), (-6, 6), (18, 30), (48, 62)]:
        p0 = (128, 128)
        pa = (128 + 160 * math.sin(math.radians(a)), 128 - 160 * math.cos(math.radians(a)))
        pb = (128 + 160 * math.sin(math.radians(b)), 128 - 160 * math.cos(math.radians(b)))
        g.polygon(poly([p0, pa, pb]), fill=hex_rgb("#ffc861") + (80,))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box(12, 12, 244, 244), radius=40 * k, fill=255)
    glow.putalpha(Image.composite(glow.split()[3], Image.new("L", (S, S), 0), mask))
    im.alpha_composite(glow)
    d = ImageDraw.Draw(im)
    # The open lid's inside, the glowing mouth, then the body with its bands and lock.
    d.polygon(poly([(52, 132), (66, 84), (190, 84), (204, 132)]), fill=hex_rgb("#4a2e1c") + (255,), outline=ink, width=6 * k)
    d.polygon(poly([(62, 128), (72, 94), (184, 94), (194, 128)]), fill=hex_rgb("#5e3b22") + (255,))
    d.ellipse(box(60, 118, 196, 146), fill=hex_rgb("#fff4c8") + (255,))
    d.rounded_rectangle(box(48, 132, 208, 214), radius=10 * k, fill=hex_rgb("#8a5a34") + (255,), outline=ink, width=6 * k)
    d.rectangle(box(54, 136, 202, 150), fill=hex_rgb("#b07a48") + (255,))
    d.rectangle(box(54, 192, 202, 208), fill=hex_rgb("#5e3b22") + (255,))
    for x in (68, 170):
        d.rectangle(box(x, 132, x + 18, 214), fill=hex_rgb("#e2b04a") + (255,), outline=ink, width=4 * k)
        d.rectangle(box(x + 3, 136, x + 8, 210), fill=hex_rgb("#ffe08a") + (255,))
    d.rounded_rectangle(box(112, 150, 144, 188), radius=6 * k, fill=hex_rgb("#e2b04a") + (255,), outline=ink, width=4 * k)
    d.ellipse(box(123, 160, 133, 170), fill=ink)
    d.polygon(poly([(126, 166), (130, 166), (132, 180), (124, 180)]), fill=ink)
    # Glints.
    for x, y, r in [(54, 62, 12), (202, 54, 9), (214, 104, 6)]:
        d.polygon(poly([(x, y - r), (x + r * 0.28, y - r * 0.28), (x + r, y), (x + r * 0.28, y + r * 0.28), (x, y + r),
                        (x - r * 0.28, y + r * 0.28), (x - r, y), (x - r * 0.28, y - r * 0.28)]), fill=(255, 255, 255, 235))
    im = im.resize((256, 256), Image.Resampling.LANCZOS)
    im.save(os.path.join(ASSETS, "icon.png"))


BUILDERS = {"appicon": build_appicon, "monsters": build_monsters, "backdrops": build_backdrops, "icons": build_icons,
            "vfx": build_vfx, "fonts": build_fonts}


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--cache", default=os.path.join(os.path.expanduser("~"), ".cache", "lootfall-placeholders"))
    p.add_argument("--only", default=",".join(BUILDERS))
    args = p.parse_args()
    os.makedirs(args.cache, exist_ok=True)
    for name in args.only.split(","):
        print(name)
        BUILDERS[name](args.cache)


if __name__ == "__main__":
    main()
