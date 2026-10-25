# Placeholder art sources hosted on GitHub for LootFall (verified 2026-10-25)

All repositories below were checked from this cloud session on 2026-10-25. File counts, folder breakdowns and last-commit
dates come from depth-1 blobless clones. Download sizes were measured either with real depth-1 clones or with sparse
checkouts of the named folders. Image dimensions were read from file headers. A 24-image contact sheet of samples was
inspected to judge style; it is saved at
`/tmp/claude-0/-home-user/dfb589e2-ccf0-54ec-bb27-44c293176229/scratchpad/samples_montage.png`.
"Measured" below always means measured this way on that date.

## 1. Item icons: game-icons, other RPG icon sets (painted or pixel), Kenney mirrors, LPC, OpenGameArt mirrors

### Takeaway
GitHub has plenty of verified icon material for every candidate style except painted high-resolution art:
- Vector/flat: game-icons (4,239 single-colour SVGs, CC BY 3.0).
- Pixel art: the Dungeon Crawl Stone Soup (DCSS) tiles (1,133 item tiles plus equipment paperdoll layers at 32x32, mostly CC0) and the XNA RPG Starter Kit gear (432 crisp 64x64 icons, Ms-PL).
- 3D-rendered look: Fluent Emoji 3D (MIT, 256 px) and the new Noto 3D emoji set (Apache-2.0, up to 512 px).
- Flat emoji: Twemoji (CC BY 4.0) and OpenMoji (CC BY-SA 4.0).
- Kenney: a 2020 mirror of 129 packs (ETdoFresh/kenney.nl, CC0), which LootFall's importer already uses.

No painted, high-resolution (128 px or larger) RPG icon set was found on GitHub. The nearest are 60-72 px or rendered
icons from copyleft games (Wesnoth, Valyria Tear, Flare).

### Cited Findings
**game-icons (vector, single colour)**
- github.com/game-icons/icons exists (branch `master`, last commit 2026-04-23). It holds 4,239 SVG files in one folder
  per contributor: delapouite 2,022, lorc 1,429, skoll 172, caro-asercion 127, viscious-speed 121, sbed 95,
  aussiesim 52, darkzaitzev 32, cathelineau 29, and smaller folders. A full depth-1 clone is 3.8 MB and took about
  1 s — [repo](https://github.com/game-icons/icons).
- Licence: "Icons provided under the Creative Commons 3.0 BY or CC0 if mentioned below". Only Viscious Speed and
  Zeromancer are marked CC0 — [license.txt](https://github.com/game-icons/icons/blob/master/license.txt).
- Format: each icon has a 512x512 viewBox, a black square background path and one white (`fill="#fff"`) glyph path.
  Recolouring (for example per rarity) is a one-attribute change —
  [lorc/broadsword.svg](https://github.com/game-icons/icons/blob/master/lorc/broadsword.svg).
- Slot coverage by filename substring. These counts are approximate: "ring" also matches words like "string", and
  "bow" also matches "elbow".
  - Matches: helm 32, armor 21, boot 15, glove 6, gauntlet 1, ring 51, necklace 10, pendant 3, belt 4, cloak 4,
    cape 4, shield 52, sword 43, axe 14, bow 26, dagger 9, wand 4, staff 3, potion 6, chest 8, gem 8, crown 17,
    earring 5, mask 30, book 17.
  - No filename contains "amulet", "bracelet", "tattoo" or "trinket" —
    [repo](https://github.com/game-icons/icons).
- RPG-Awesome is an icon font built from game-icons. Its README says "Super shoutout to Game Icons ... used to create
  this project" and licenses the font under SIL OFL 1.1, the CSS/SASS under MIT and the documentation under CC BY 3.0 —
  [README](https://github.com/nagoshiashumari/Rpg-Awesome/blob/master/README.md).
  - Its LICENSE.md instead holds a BSD-style text, "Copyright (c) 2014, Daniela Howe" —
    [LICENSE.md](https://github.com/nagoshiashumari/Rpg-Awesome/blob/master/LICENSE.md).
  - Last commit 2018-06-14; 33 files, under 1 MB measured.

**Dungeon Crawl Stone Soup tiles (pixel art, 32x32, mostly CC0)**
- `crawl-ref/source/rltiles` in crawl/crawl (last commit 2026-10-24) holds 8,785 files. Almost all are PNG: dngn
  2,311, mon 1,600, player 1,220, item 1,133, gui 1,026, UNUSED 596, effect 514, misc 347. Sampled item and monster
  tiles are 32x32 — [rltiles](https://github.com/crawl/crawl/tree/master/crawl-ref/source/rltiles).
- Item subfolders: amulet, armour, book, food, gem, gizmo, gold, misc, parchment, potion, ring, rod, scroll, staff,
  talisman, wand, weapon. Player (paperdoll) subfolders: barding, base, beard, body, boots, cloak, drcwing, ench,
  felids, gloves, hair, halo, hand1, hand2, head, legs, mutations, transform —
  [rltiles](https://github.com/crawl/crawl/tree/master/crawl-ref/source/rltiles).
- The repo LICENSE lists "Public Domain|CC0: most of tiles". It adds that "The majority of Crawl's tiles and artwork
  are released under the CC0 license ... However, the licensing situation may be complex, especially for older pieces"
  and points to crawl/tiles — [LICENSE](https://github.com/crawl/crawl/blob/master/LICENSE).
- A sparse checkout of rltiles alone took 5 s: 25 MB of files, 12 MB of git objects —
  [crawl/crawl](https://github.com/crawl/crawl).
- crawl/tiles is a curated CC0 export. Its README says many of the artists "have signed off their copyright, returning
  these tiles to ... CC Zero", and it lists approved artists (ARTISTS.md) and ineligible tiles
  (TILES_UNDER_UNKNOWN_LICENSE.md) — [README](https://github.com/crawl/tiles/blob/master/README.md).
  - Contents: the Nov-2015 release has dngn 1,107, player 819, mon 772, item 673, UNUSED 282, gui 275, misc 231 and
    effect 198 PNG. There is also an `Ancient/Oct-5-2010` set of 3,039 PNG.
  - Last commit 2020-09-04; depth-1 clone 5 MB —
    [releases](https://github.com/crawl/tiles/tree/master/releases).

**XNA RPG Starter Kit gear (pixel-like, 64x64, Ms-PL)**
- SimonDarksideJ/XNAGameStudio (branch `archive`, last commit 2025-11-14) is MIT-licensed, "Copyright (c) 2016 Simon
  (Darkside) Jackson" — [LICENSE](https://github.com/SimonDarksideJ/XNAGameStudio/blob/archive/LICENSE).
- It contains the RolePlayingGame starter kit, whose folder ships "Microsoft Permissive License (Ms-PL) ... This license
  governs use of the accompanying software" —
  [Microsoft Permissive License.rtf](https://github.com/SimonDarksideJ/XNAGameStudio/blob/archive/Samples/RolePlayingGame_4_0_Win_Xbox/RolePlayingGame_4_0_Win_Xbox/Microsoft%20Permissive%20License.rtf).
- `Content/Textures/Gear` has 432 PNG at 64x64 —
  [sample folder](https://github.com/SimonDarksideJ/XNAGameStudio/tree/archive/Samples/RolePlayingGame_4_0_Win_Xbox).
  - Filename prefixes: torso 72, Sword 64, Shield 56, Helmet 56, Boots 56, Staff 40, Potion 24, Club 24, Hood 16,
    Scroll 8, Wand 7, Gems 5.
  - Variants are lettered a-g (for example Boots01a to Boots01g).
  - The sampled Sword02e and Helmet icons are crisp, shaded, pixel-art-like.
- Other texture folders: Portraits 76, Characters 64, Spells 47, GameScreens 26, Chests 20, Buttons 14, MainMenu 11,
  HUD 10, Combat 7. A sparse checkout of the whole sample is 1,191 files and 97 MB —
  [sample folder](https://github.com/SimonDarksideJ/XNAGameStudio/tree/archive/Samples/RolePlayingGame_4_0_Win_Xbox).

**Emoji sets (3D-rendered or flat vector)**
- **microsoft/fluentui-emoji** is MIT — [LICENSE](https://github.com/microsoft/fluentui-emoji/blob/main/LICENSE).
  Last commit 2026-08-24 — [assets](https://github.com/microsoft/fluentui-emoji/tree/main/assets).
  - Contents: `assets` holds 12,620 files (7,882 SVG in Color, Flat and High Contrast styles, 3,144 PNG, 1,594 JSON).
  - 3D renders: about 1,285 emoji have a top-level 3D render, a 256x256 PNG in a soft "clay" 3D style (checked:
    Crossed swords, Dragon). A sparse checkout of those 3D renders is 40 MB.
  - Slot- and fantasy-relevant names with a 3D render include Crossed swords, Dagger, Shield, Bow and arrow, Axe,
    Hammer, Ring, Gem stone, Crown, Magic wand, Crystal ball, Nazar amulet, Prayer beads, Military helmet, Gloves,
    Socks, Hiking boot, Womans boot, Coat, Scarf, Kimono, Dress, Backpack, Scroll, Test tube, Key, Old key, Money bag,
    Coin, Dragon, Goblin, Ogre, Troll, Person genie, Person zombie, Ghost, Skull, Spider, Bat, Wolf, Snake, Scorpion,
    Lizard, Mushroom and Unicorn.
- **googlefonts/noto-emoji**: its README says "Tools and most image resources are under the Apache license, version
  2.0", with fonts under OFL 1.1 — [README](https://github.com/googlefonts/noto-emoji/blob/main/README.md).
  - 2D set: 3,750 SVG (39 MB measured) plus PNG at 32, 72, 128 and 512 px.
  - 3D set: 4,007 PNG at each of 32, 72, 128 and 512 px. `3D/png/512` alone is 844 MB measured; the sample is a
    512x512 glossy 3D render — [3D/png](https://github.com/googlefonts/noto-emoji/tree/main/3D/png).
  - Last commit 2026-09-24.
- **jdecked/twemoji** (the maintained Twemoji fork): graphics are CC BY 4.0 —
  [LICENSE-GRAPHICS](https://github.com/jdecked/twemoji/blob/main/LICENSE-GRAPHICS). 4,009 SVG plus 4,009 72x72 PNG;
  depth-1 clone 6 MB; last commit 2026-06-01 — [assets](https://github.com/jdecked/twemoji/tree/main/assets).
- **hfg-gmuend/openmoji**: CC BY-SA 4.0 — [LICENSE.txt](https://github.com/hfg-gmuend/openmoji/blob/master/LICENSE.txt).
  - The color set has 4,565 emoji as SVG (13 MB measured), 72x72 PNG and 618x618 PNG (46 MB measured). There is also a
    black-line set.
  - The style is flat with a thick outline (dragon sample). Last commit 2026-08-12 —
    [color](https://github.com/hfg-gmuend/openmoji/tree/master/color).

**Kenney packs on GitHub**
- ETdoFresh/kenney.nl mirrors Kenney's packs: branch `master`, last commit 2020-07-17, 46,424 files in 129 pack
  folders — [repo](https://github.com/ETdoFresh/kenney.nl). LootFall's importer already fetches Kenney's font package
  and audio packs from it — [tools/import_art.py](/home/user/lootfall/tools/import_art.py).
- Item- and icon-relevant packs, with measured sparse sizes — [repo](https://github.com/ETdoFresh/kenney.nl):
  - `generic-items-160-assets`: 330 PNG, flat-vector items (sample 134x118), 2 MB.
  - `gameicons`: 425 PNG at 50x50, UI glyphs rather than fantasy items, 3 MB. `gameicons-expansion`: 790 PNG, 7 MB.
  - `kenneyrpgpack`: 233 PNG tiles at 64x64, 2 MB.
  - `kenneyDungeonPack_2.3`: 747 PNG, 13 MB.
  - `kenney_microroguelike_1.2`: 286 PNG. `1bitpack_kenney_1.1`: 14 PNG sheets.
  - `weaponpack_assets`: 65 PNG plus 38 OBJ. `kenney_emotespack`: 513 PNG.
- Pack licence files state CC0. For example, Particle Pack 1.1 says "License (Creative Commons Zero, CC0) ... You may use
  these assets in personal and commercial projects. Credit ... would be nice but is not mandatory" —
  [particlePack_1.1/License.txt](https://github.com/ETdoFresh/kenney.nl/blob/master/particlePack_1.1/License.txt).
- Kenney's own GitHub account (KenneyNL) hosts only Godot starter kits: 3D Platformer, City Builder, FPS and Racing,
  1-4 MB each (measured). The code is MIT and "Assets included in this package (2D sprites, 3D models and sound
  effects) are CC0 licensed" —
  [Starter-Kit-3D-Platformer README](https://github.com/KenneyNL/Starter-Kit-3D-Platformer/blob/main/README.md).

**Other icon sources in games and packs**
- **Superpowers medieval-fantasy pack** (CC0): item folders weapon 13, element 10, projectile 8, emblem 7,
  gold-&-gem 7 and food-&-potion 4. The pixel art is tiny: a weapon sprite is 7x11 px —
  [medieval-fantasy](https://github.com/sparklinlabs/superpowers-asset-packs/tree/master/medieval-fantasy),
  [LICENSE.txt](https://github.com/sparklinlabs/superpowers-asset-packs/blob/master/LICENSE.txt). LootFall already
  uses this repository's RPG Battle System item pictures — [CREDITS.md](/home/user/lootfall/CREDITS.md).
- **Wesnoth**: `data/core/images/items` has 128 PNG (sample 72x72) and `attacks` has 174 PNG weapon and attack icons
  (sample 60x60) — [images](https://github.com/wesnoth/wesnoth/tree/master/data/core/images). Licence is the GPL v2
  text in [data/COPYING.txt](https://github.com/wesnoth/wesnoth/blob/master/data/COPYING.txt).
- **Valyria Tear** inventory icons: weapons 64, items 60 and armor 29 PNG (sample 60x60) —
  [LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt).
  - Licences are per file: items GPLv2+ 17, CC0 15, CC-BY-SA-3.0 14, CC-BY-3.0 11; armor GPLv2+ 23, CC0 1.
- **Flare**: `mods/fantasycore/images/loot` has 39 PNG of rendered loot-drop animations (for example `battle_axe.png`,
  a 342x433 sheet of a spinning axe), and `powers` has 34 PNG icon sheets. Licence CC BY-SA 3.0 —
  [loot](https://github.com/flareteam/flare-game/tree/master/mods/fantasycore/images/loot),
  [LICENSE.txt](https://github.com/flareteam/flare-game/blob/master/LICENSE.txt).
- **Wyrmsun**: `graphics/icons` has 256 PNG (sample 46x38). Its `graphics/credits.txt` tags entries as follows: 1,633
  "licensed under the CC0", 356 GPL 2.0 and CC-BY-SA 3.0, 34 GPL 2.0, 15 CC-BY-SA 3.0, 12 CC-BY 4.0 —
  [credits.txt](https://github.com/Andrettin/Wyrmsun/blob/master/graphics/credits.txt).
- **Stendhal**: `data/sprites/items` has 635 PNG; the repo licence is GPL v2 —
  [data/sprites](https://github.com/arianne/stendhal/tree/master/data/sprites),
  [LICENSE.txt](https://github.com/arianne/stendhal/blob/master/LICENSE.txt).
- **Shattered Pixel Dungeon**: 78 sprite PNGs (for example `amulet.png` at 32x32) under GPL v3 —
  [assets](https://github.com/00-Evan/shattered-pixel-dungeon/tree/master/core/src/main/assets),
  [LICENSE.txt](https://github.com/00-Evan/shattered-pixel-dungeon/blob/master/LICENSE.txt).

**LPC and OpenGameArt**
- LPC equipment exists as paperdoll layers rather than icons: weapon 1,459, shield 1,282, hat 21,852, torso 11,415,
  legs 829, feet 496, neck 7,627 and cape 38 PNG —
  [spritesheets](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/tree/master/spritesheets).
  Details are under question 2.
- No standalone OpenGameArt mirror repository was found. OpenGameArt art sits inside other repos with attribution:
  - Valyria Tear's LICENSES.txt cites OpenGameArt URLs, such as the forest background by Tamara Ramsay —
    [LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt).
  - Flare credits OpenGameArt users — [CREDITS.txt](https://github.com/flareteam/flare-game/blob/master/CREDITS.txt).
  - LPC's CREDITS.csv links OpenGameArt pages —
    [CREDITS.csv](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/blob/master/CREDITS.csv).

### Inferences
- **Vector/flat:** game-icons is the strongest single source. About 4,200 silhouettes can be tinted per rarity with one
  fill change and imported as SVG. CC BY 3.0 needs a credit line, which fits LootFall's existing CREDITS.md habit.
- **Pixel art:** DCSS covers the most of LootFall's 149 slot kinds in one consistent 32 px style: amulets, rings,
  armour, talismans, staves and wands as items, plus boots, cloaks, gloves, head and legs as paperdoll layers. It is
  mostly CC0. The XNA gear adds 64 px helmets, boots, shields, torsos and weapons with lettered variants, which could
  stand in for rarity tiers.
- **3D look:** the Fluent 3D (MIT) and Noto 3D (Apache-2.0) renders read as glossy 3D cartoon. They cover ring, gem,
  crown, helmet, gloves, socks, boots, coat, scarf, amulet-like beads and nazar, and companions (dragon, wolf, unicorn,
  bat, spider). They would pair well with a KayKit-style 3D test.
- **Painted:** no verified hi-res painted icon set on GitHub. A painted test would have to use small copyleft icons
  (Wesnoth's 60-72 px icons, Valyria Tear's 60 px icons, Flare's rendered loot) or upscale them.
- **Unusual slots:** tattoos have no matching icon by name in any set checked. Rune or glyph icons from game-icons, or
  coloured body-part icons, are the likely stand-ins. Ten rings and ten toe rings can share ring art with tints.
- **Licences:** CC BY-SA and GPL sources (OpenMoji, Flare, Wesnoth, most of Valyria Tear, part of Wyrmsun, Shattered
  Pixel Dungeon, Stendhal) carry share-alike duties if a build containing them is distributed. LootFall's CI exports
  Windows builds as artifacts, so keep copyleft placeholders out of builds shared beyond the owner. CC0, MIT,
  Apache-2.0, CC BY and Ms-PL sources carry little friction.
- RPG-Awesome adds nothing over game-icons and has contradictory licence files; use game-icons directly.

### Gaps
- No painted, high-resolution (128 px or larger) fantasy icon set was found on GitHub. Well-known painted sets hosted
  on other sites could not be checked: those sites are blocked here, and web search was unavailable (its shared
  budget for the turn was used up).
- The Kenney mirror is a July 2020 snapshot. No folder for later packs (fantasy UI borders, "tiny" packs) matched, and
  no other Kenney mirror was verified.
- The game-icons keyword counts are filename substrings, not a curated mapping to LootFall's 149 slots.
- The README states Apache-2.0 for "most image resources"; whether that covers the Noto 3D set is not said separately.
- No OpenGameArt mirror repository could be located: GitHub's search API was blocked, and none was known by name.

## 2. Characters and monsters: high-res 2D (side view), Spine/DragonBones examples, LPC generator, CC0 3D with animation

### Takeaway
- **Painted, side view:** Valyria Tear has the only verified painted side-view enemy sheets (about 30 enemies; mostly
  GPL or CC BY-SA, a few CC0).
- **Painted, static:** Ancient Beast has 51 painted creature illustrations at 400x520 (CC BY-SA 4.0). Wesnoth adds
  286 painted 450-500 px portraits (GPL v2+).
- **Pixel art:** the CC0 Superpowers RPG Battle System (already in LootFall), Wesnoth's 7,175 unit images and the LPC
  generator. LPC has 88,114 paperdoll layers and 15 animations in 64 px frames, under mixed OGA-BY, CC and GPL
  licences.
- **Low-poly 3D:** KayKit's official repos (CC0, rigged and animated, packaged as Godot addons) are the clear source.
  Kenney 3D kits are in the 2020 mirror. No Quaternius repo was found.
- **Spine and DragonBones:** Spine's example art is non-commercial or demo-only. The DragonBones demos contain no
  fantasy characters.

### Cited Findings
**Superpowers RPG Battle System (pixel, side view, CC0; already used)**
- The `char` folders are warrior, warrior2, Wizard, mage, bowman and paladin —
  [rpg-battle-system](https://github.com/sparklinlabs/superpowers-asset-packs/tree/master/rpg-battle-system).
- The `monster` folders are mimic, bat, boar, dino, dragon, ghost, giant, mushroom, reptile, slime and snake, plus
  single sheets for octopus, yeti and chest.
- Frame sizes are large for pixel art: the dragon sheet is 1290x1045 in 258x209 frames, the chest 104x100, the yeti
  135x102.
- The whole repository clones (depth 1) at 78 MB; last commit 2019-09-21 —
  [repo](https://github.com/sparklinlabs/superpowers-asset-packs).
- The same repository has `3d-warriors` (29 OBJ, 65 PNG) and `3d-character` (82 files), also CC0 —
  [LICENSE.txt](https://github.com/sparklinlabs/superpowers-asset-packs/blob/master/LICENSE.txt).
- LootFall already uses its knight (`char/warrior2`), every monster, the backdrops, the effects, the items and the
  chest — [CREDITS.md](/home/user/lootfall/CREDITS.md).

**Valyria Tear (painted, side-view battle sheets)**
- `data/entities/battle` holds 113 PNG —
  [enemies](https://github.com/ValyriaTear/ValyriaTear/tree/master/data/entities/battle/enemies).
  - Enemy sheets: aerocephal, arcana_drake, aurum-drakueli, bat, beetle, big_slime, daemarbora, dark_soldier,
    deceleon, demonic_essence, dorver, dune_crawler, eyeball, fenrir, green_slime, green_snake, harlequin,
    lord_banesore, mushroom_zombie, nagaruda, rat, red_slime, scorpion, scorpion_goliath, shroom, skeleton, snake,
    spider, stygian_lizard, thing, yeti.
  - Sheet sizes vary: aerocephal is 768x192; fenrir is 2240x406 and shows four painted side-view frames of a wolf.
- Licences are per file in LICENSES.txt —
  [LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt).
  - `data/entities/battle`: GPLv2+ 82, CC-BY-SA-3.0 19, CC-BY-3.0 3, GPLv2 3, GPLv3 3.
  - `data/entities/portraits` (19 PNG): GPLv2+ 12, CC-BY-SA-3.0 3, CC0 3, GPLv3 1.
  - Allacrost-derived files "are assumed to be GPLv2 or later".
- Depth-1 clone 109 MB; last commit 2022-01-02 — [repo](https://github.com/ValyriaTear/ValyriaTear).

**Ancient Beast (painted illustrations)**
- The README's licence table says "CC-BY-SA 4.0: Artwork and audio can be remixed and shared under the same license,
  provided you credit and link the project, as well as the author(s)". The code is AGPL 3.0, which is what LICENSE.md
  contains — [README](https://github.com/FreezingMoon/AncientBeast/blob/master/README.md).
- `assets/units` contents — [assets/units](https://github.com/FreezingMoon/AncientBeast/tree/master/assets/units):
  - `artwork`: 51 JPG at 400x520, painted (sample "Golden Wyrm": a dragon-man warrior with an axe).
  - Also: avatars 59 JPG, cardboards 30 PNG, sprites 19 PNG (the sample is a 75x75 ability circle), abilities 204 SVG
    and shouts 51 OGG.
- Creatures (artwork filenames): Abolished, Aegis, Asher, Batmadillo, Blue Shrimp, Bounty Hunter, Chimera, Crystalis,
  Cyber Wolf, Cycloper, Dark Priest, Deep Beauty, Flayed, Gilded Maiden, Golden Wyrm, Greedy Knight, Gumble, Headless,
  Horn Head, Impaler, Infernal, Knightmare, Kraken, Lavamander, Living Armor, Mangler, Metalist, Miss Creeper,
  Moss Hound, Mr. Stitches, Nutcase, Papa Eggplant, Razorback, Royal Guard, Sarcophag, Satyr, Scavenger, Scorpius,
  Shadow Leech, Snow Bunny, Spikes, Stomper, Swampler, Swine Thug, Toxic Shroom, Troglodyte, Uncle Fungus, Vehemoth,
  Vertigo, Volpyr, Vulcan —
  [artwork](https://github.com/FreezingMoon/AncientBeast/tree/master/assets/units/artwork).
- Depth-1 clone 70 MB; last commit 2026-10-25 — [repo](https://github.com/FreezingMoon/AncientBeast).

**Battle for Wesnoth (painted portraits, pixel units)**
- `data/core/images/portraits` has 286 files (284 WebP). Samples are 500x500 and 450x450 painted busts. Groups:
  drakes, dunefolk, dwarves, elves, goblins, humans, merfolk, monsters, nagas, orcs, saurians, transport, trolls,
  undead, wolves, woses. The sparse download is 32 MB —
  [portraits](https://github.com/wesnoth/wesnoth/tree/master/data/core/images/portraits).
- `data/core/images/units` has 7,175 PNG totalling 8 MB; the sample is a 72x72 pixel sprite with painterly shading —
  [units](https://github.com/wesnoth/wesnoth/tree/master/data/core/images/units).
- Licence: the GPL v2 text in [data/COPYING.txt](https://github.com/wesnoth/wesnoth/blob/master/data/COPYING.txt).
  The per-file `copyrights.csv` covers only audio — [copyrights.csv](https://github.com/wesnoth/wesnoth/blob/master/copyrights.csv).
- Last commit 2026-10-24 — [repo](https://github.com/wesnoth/wesnoth).

**Flare (rendered, painterly, isometric)**
- `mods/fantasycore/images/enemies` has 50 PNG sheets —
  [enemies](https://github.com/flareteam/flare-game/tree/master/mods/fantasycore/images/enemies).
  - Enemies: antlion (and small), cursed_grave, fire_ant, goblin (and elite), hobgoblin (and archer), ice_ant,
    minotaur (block, cast, die, run, shoot, stance, swing), skeleton (archer, mage, weak), wyvern (air, fire and water,
    each with cast, dash_attack, die, hit, run, stance and swing), zombie.
  - `antlion.png` is 1919x3225.
- Other folders: avatar 147 PNG; portraits 36 PNG (sample 320x320, a realistic dog portrait); menus 48; powers 34.
- Licence CC BY-SA 3.0 — [LICENSE.txt](https://github.com/flareteam/flare-game/blob/master/LICENSE.txt).
- Depth-1 clone 818 MB; it includes `art_src` with 280 .blend files. Last commit 2026-08-07 —
  [repo](https://github.com/flareteam/flare-game).

**LPC character generator and LPC Revised (pixel paperdoll)**
- LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator (last commit 2026-10-14) has 88,114 PNG layers in
  `spritesheets` —
  [spritesheets](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/tree/master/spritesheets).
  - Layer counts: hat 21,852, facial 18,090, body 14,223, torso 11,415, neck 7,627, eyes 2,875, hair 2,289,
    head 2,056, dress 1,997, weapon 1,459, shield 1,282, backpack 948, legs 829, feet 496, shoulders 241, beards 196,
    arms 146, cape 38, tools 28, quiver 6.
- Animations on the male body: backslash, climb, combat_idle, emote, halfslash, hurt, idle, jump, run, shoot, sit,
  slash, spellcast, thrust, walk. Frames are 64x64 with one row per direction (4 rows); backslash is 832x256 and walk
  576x256 —
  [bodies/male](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/tree/master/spritesheets/body/bodies/male).
- CREDITS.csv licenses each sheet; it has 13,915 entries —
  [CREDITS.csv](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/blob/master/CREDITS.csv).
  - Most common: OGA-BY 3.0 alone 4,342; "OGA-BY 3.0+, CC-BY 3.0+, GPL 3.0" 1,642; "OGA-BY 3.0, CC-BY-SA 3.0, GPL 3.0"
    1,636; "CC-BY-SA 3.0, GPL 3.0" 1,588; CC0 1,011.
  - The repository LICENSE is GPL v3 —
    [LICENSE](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/blob/master/LICENSE).
  - Depth-1 clone 108 MB.
- ElizaWy/LPC ("LPC Revised"): "This pack is licensed OGA-BY 3.0 ... free of charge, even for commercial use, so long
  as the artists are credited" — [Credits.txt](https://github.com/ElizaWy/LPC/blob/main/Credits.txt).
  - 64,311 PNG. Characters folder: Clothing 36,421, Props 10,094, Hair 7,267, Head 3,964, Body 3,040, Head Accessories
    2,598, Children 457. Sheets are like `Climb.png` at 384x64.
  - Depth-1 clone 247 MB; last commit 2023-12-01 — [repo](https://github.com/ElizaWy/LPC).
- Older LPC repositories also resolve:
  - [sanderfrenken/Universal-LPC-Spritesheet-Character-Generator](https://github.com/sanderfrenken/Universal-LPC-Spritesheet-Character-Generator):
    24,221 PNG, last commit 2025-01-03.
  - [jrconway3/Universal-LPC-spritesheet](https://github.com/jrconway3/Universal-LPC-spritesheet): 2,755 PNG,
    25 MB, AUTHORS.txt.

**Spine examples (restrictive) and DragonBones demos**
- EsotericSoftware/spine-runtimes (default branch 4.3, last commit 2026-10-21, depth-1 clone 81 MB) has 29 example
  projects with their own license.txt —
  [examples](https://github.com/EsotericSoftware/spine-runtimes/tree/4.3/examples).
  - Examples: spineboy, raptor, goblins, owl, mix-and-match, hero, dragon, celestial-circus, chibi-stickers,
    cloud-pot, alien, speedy, windmill, diamond, tank, stretchyman, coin, powerup, spinosaurus, vine, snowglobe,
    food-app, and an animation-principles series.
  - The repo also has a `spine-godot` runtime folder (577 files).
- Spineboy's licence: "The images in this project may be redistributed as long as they are accompanied by this
  license file. The images may not be used for commercial use of any kind. The project file is released into the
  public domain." Raptor, goblins, owl and mix-and-match open with the same Esoteric Software wording —
  [spineboy/license.txt](https://github.com/EsotericSoftware/spine-runtimes/blob/4.3/examples/spineboy/license.txt).
- Dragon (Thiago Brayner) is "provided for demonstration purposes only and may not be redistributed for any reason nor
  used as the basis for derivative work" —
  [dragon/license.txt](https://github.com/EsotericSoftware/spine-runtimes/blob/4.3/examples/dragon/license.txt).
  Hero (XDTech) opens with the same "provided for" wording —
  [hero/license.txt](https://github.com/EsotericSoftware/spine-runtimes/blob/4.3/examples/hero/license.txt).
- The goblins atlas is a 512x512 PNG of painted body parts —
  [goblins](https://github.com/EsotericSoftware/spine-runtimes/tree/4.3/examples/goblins).
- DragonBones/DragonBonesJS is MIT, "Copyright (c) 2012-2025 The DragonBones team" —
  [LICENSE](https://github.com/DragonBones/DragonBonesJS/blob/master/LICENSE).
  - Its demo resources are you_xin (90 files), shizuku, several mecha_* and weapon_*, bullet_01, effect and
    skin_1502b: no fantasy characters —
    [Pixi demo resources](https://github.com/DragonBones/DragonBonesJS/tree/master/Pixi/Demos/resource).
  - Runtimes exist for Cocos, Pixi, Phaser, Egret, Hilo and ThreeJS; there is no Godot folder.
  - Depth-1 clone 24 MB; last commit 2025-05-24.

**KayKit (low-poly 3D, CC0, official GitHub)**
- **KayKit-Character-Pack-Adventures-1.0** (last commit 2023-09-16): "License: (Creative Commons Zero, CC0) ... free to
  use in personal, educational and commercial projects" —
  [LICENSE.txt](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/blob/main/LICENSE.txt).
  - The README describes "4 (+2 in the EXTRA tier) stylised low-poly character assets", "fully rigged and animated",
    "75 animations", "25+ weapons/accessories", one 1024x1024 gradient atlas, and FBX and GLTF files —
    [README](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/blob/main/README.md).
  - Files: Barbarian, Knight, Mage, Rogue and Rogue_Hooded GLB, plus separate weapon and prop glTFs (swords 1h/2h,
    axes 1h/2h, crossbows, dagger, staff, wand, spellbook, shields in badge, round, spikes and square shapes, quiver,
    arrows, smokebomb, mugs).
  - Packed under `addons/kaykit_character_pack_adventures`; depth-1 clone 22 MB.
- **KayKit-Character-Pack-Skeletons-1.0**: Skeleton_Mage, Skeleton_Minion, Skeleton_Rogue and Skeleton_Warrior GLB;
  19 MB; last commit 2024-02-02 —
  [repo](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0).
- **KayKit-Dungeon-Remastered-1.0**: 203 GLB, 203 FBX, 208 OBJ; 25 MB —
  [repo](https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0).
- **KayKit-Medieval-Hexagon-Pack-1.0**: 221 glTF, 442 FBX, 263 OBJ; 28 MB; last commit 2024-04-26 —
  [repo](https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0).
- Each pack has a LICENSE.txt. Three guessed names do not resolve with git: KayKit-Character-Pack-Adventurers-1.0,
  KayKit-Skeletons-1.0 and KayKit-Character-Animations-1.0 —
  [KayKit-Game-Assets](https://github.com/KayKit-Game-Assets).

**Kenney characters and 3D kits (CC0, 2020 mirror)**
- Measured sizes — [ETdoFresh/kenney.nl](https://github.com/ETdoFresh/kenney.nl):
  - `castle-kit-1.0`: 60 FBX, 60 OBJ, 341 PNG; 9 MB.
  - `fantasy-town-kit-1.0`: 153 each of GLB, FBX, OBJ and DAE; 17 MB.
  - `animated-characters-1`: 4 FBX; 2 MB.
  - `weaponpack_assets`: 38 OBJ.
  - `modularcharacters`: 437 PNG of 2D parts.
  - `kenney_tooncharacters1`: 677 PNG of flat cartoon characters; 6 MB.
  - `kenney_medievalrtspack`: 259 PNG.

**Other character sources checked**
- **0 A.D.**: 783 portrait files (58 MB) in `art/textures/ui/session/portraits`. Art and audio are "Creative Commons
  Attribution-Share Alike 3.0". It is a historical, not fantasy, setting. Mirror last commit 2024-08-17 —
  [portraits](https://github.com/0ad/0ad/tree/master/binaries/data/mods/public/art/textures/ui/session/portraits),
  [LICENSE.txt](https://github.com/0ad/0ad/blob/master/LICENSE.txt).
- **MegaGlest data**: factions norsemen, magic, persian, indian, romans, tech and egypt; 1,313 .g3d model files; CC
  BY-SA 3.0; depth-1 clone 272 MB —
  [LICENSE.data.txt](https://github.com/MegaGlest/megaglest-data/blob/develop/docs/LICENSE.data.txt).
- **Wyrmsun**: race sprite folders dwarf 240, teuton 195, germanic 117, goblin 113, gnome 96, human 43, norse 37 and
  others; depth-1 clone 390 MB — [graphics](https://github.com/Andrettin/Wyrmsun/tree/master/graphics).
- **Stendhal**: monsters 416 PNG as top-down sheets (for example 144x256, 3x4 frames of 48x64), npc 272, outfit 335;
  GPL v2 — [data/sprites](https://github.com/arianne/stendhal/tree/master/data/sprites).
- **DCSS**: `mon` holds 1,600 static 32x32 tiles, mostly CC0 —
  [rltiles](https://github.com/crawl/crawl/tree/master/crawl-ref/source/rltiles).
- **SuperTux**: 1,940 PNG cartoon creatures; the repo is GPL v3 —
  [data/images](https://github.com/SuperTux/supertux/tree/master/data/images),
  [LICENSE.txt](https://github.com/SuperTux/supertux/blob/master/LICENSE.txt).
- **awesome-cc0** list — [awesome-cc0](https://github.com/madjin/awesome-cc0):
  - It lists "300 CC0 VRM + FBX avatars". [madjin/100avatars](https://github.com/madjin/100avatars) holds 46,357 files
    (300 previews, VoxEdit .vxm/.vxa) and is not fantasy-specific.
  - It lists "PSX style low poly models". [M3-org/retro3d-assets](https://github.com/M3-org/retro3d-assets) has
    Characters (100 files) and Weapons (49), mostly modern and urban props.
  - [PolygonalMind/initiative-opensource-release](https://github.com/PolygonalMind/initiative-opensource-release) has
    75,151 files of Unity and voxel projects.
  - [M3-org/base-meshes](https://github.com/M3-org/base-meshes) also exists.
- No Quaternius repository was found: none is known by name, search was unavailable, and no Quaternius file appeared
  in any tree checked.

### Inferences
- **Painted, side view:** LootFall's battle is side-view (knight at x 300 facing the monsters), and Valyria Tear's
  enemy sheets are the only verified painted, animated side-view battlers. Ancient Beast's 51 creatures are static
  full-body paintings that could be idle sprites, animated with code-driven bob, squash and hit flashes, which LootFall
  already does for events. Wesnoth's portraits suit boss intros, companions or the chest-opening card better than the
  lane.
- **Pixel:** the Superpowers pack already in use stays the most coherent pixel set. LPC is the richest way to show gear
  on the hero (weapon, shield, hat, torso, legs, feet and cape layers, with slash, thrust, spellcast and hurt), but its
  perspective is 4-direction RPG, so only the right-facing row fits a side-view lane.
- **Low-poly 3D:** KayKit is the best fit: CC0, five heroes and four skeletons rigged and animated, weapons and shields
  as separate glTFs (good for "gear shows up in the fight"), and Godot addon packaging. A test could render it to 2D
  sprite sheets or use Godot 3D with an orthographic camera.
- **Spine:** the examples suit only a private look test; neither the non-commercial nor the demo-only terms allow
  shipping them.
- **DragonBones:** offers nothing fantasy-themed and has no Godot runtime in that repository.
- **Copyleft sources** (Flare, Wesnoth, Valyria Tear, Ancient Beast, much of LPC) are fine for private placeholder
  experiments but should not reach shared builds without accepting share-alike terms.

### Gaps
- No Quaternius, LuizMelo, 0x72, CraftPix or GameArt2D character packs were verified on GitHub. Web search was
  unavailable and these were not known by repository name. The exceptions are a GameArt2D tileset in phaserjs/examples
  and Ansimuz's Sunny Land tileset in GDevelop-examples.
- Only the Adventurers README states animation counts; whether the KayKit Skeletons pack includes animations was not
  verified.
- Not checked: whether Ancient Beast's `sprites` and `cardboards` folders hold in-game creature sprites (one sample
  was a 75x75 ability circle), and the frame layout of Valyria Tear's sheets.
- Whether the XNA RPG `Characters` folder includes side-view battle sprites was not verified; the sample was a
  1728x2016 sheet of small walking characters.
- Only the first line of the Spine hero licence was read.

## 3. Backgrounds and parallax, VFX, fantasy UI frames, fonts, sound and music

### Takeaway
- **Painted biome backgrounds:** found in copyleft games.
  - Valyria Tear: 1024x768 forest, desert, mountain, plains and snow scenes; two of them CC0.
  - Ancient Beast: five 1920x1080 painted locations (CC BY-SA 4.0).
  - Wesnoth: 1024x576 story art (GPL).
  - Wyrmsun: CC0 1080p sketches.
- **Parallax:** dedicated multi-layer sets are scarce. Only Superpowers' tiny 137x89 layers (already used) and Flare's
  five parallax PNGs were found.
- **VFX:** Kenney's Particle Pack (193 CC0 textures at 512x512) is the best general source; Wesnoth's 320 halo frames
  are ready-made animated spell effects (GPL).
- **UI:** fantasy 9-slice UI is thin: Kenney's generic UI packs, plus RPG UI from the XNA kit (Ms-PL), Flare and
  Valyria Tear.
- **Fonts:** google/fonts has every fantasy and numeral family checked (OFL, one Apache).
- **Audio:** CC0 audio on GitHub is mainly Kenney and Superpowers. The larger orchestral fantasy music sets are copyleft
  (Wesnoth, 0 A.D., Flare).

### Cited Findings
**Backgrounds and parallax**
- **Superpowers backgrounds pack** (CC0): 105 layer PNGs and 41 background PNGs, each 137x89, with a 732x509 preview.
  LootFall's zone and title backdrops already come from this repository —
  [backgrounds](https://github.com/sparklinlabs/superpowers-asset-packs/tree/master/backgrounds),
  [CREDITS.md](/home/user/lootfall/CREDITS.md).
- **Valyria Tear** `battle_scenes`: 18 PNG —
  [LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt).
  - Scenes: desert, desert_cave (with creatures, eyes, water and waterdrop overlays), forest, forest evening, mountain,
    mountain shrine, mountain village (and single house), plains, snow, rock, ripples. Sample desert background is
    1024x768.
  - Licence tally for the folder: GPLv2+ 10, CC-BY-SA-3.0 3, CC0 2.
  - The forest, forest-evening and mountain backgrounds are CC-BY-SA-3.0 by Tamara Ramsay ("Ramtam", OpenGameArt).
  - The desert and plains backgrounds are CC0 by JAP ("oil-painting-landscapes", OpenGameArt).
- **Ancient Beast** `assets/locations`: 5 painted JPG at 1920x1080 (Dark Forest, Desolated Canyon, Dragon Bones,
  Frozen Wall, Shadow Cave), under CC BY-SA 4.0 —
  [locations](https://github.com/FreezingMoon/AncientBeast/tree/master/assets/locations).
- **Wesnoth** `story`: 27 images (24 WebP, for example `blacksmith.webp` at 1024x576, and 3 JPG); 16 MB measured; GPL —
  [story](https://github.com/wesnoth/wesnoth/tree/master/data/core/images/story).
- **Wyrmsun** `graphics/backgrounds`: 25 PNG at about 1920x1080, for example `dwarven_victory.png` (1916x1059, by Jinn,
  CC0). Another is `gryphon.png`, credited as a drawing by Tanja Nov (CC0); it is a sepia pencil sketch —
  [credits.txt](https://github.com/Andrettin/Wyrmsun/blob/master/graphics/credits.txt).
- **Flare** (empyrean campaign) `parallax`: 5 PNG, for example `fog.png` at 1024x1024; CC BY-SA 3.0 —
  [parallax](https://github.com/flareteam/flare-game/tree/master/mods/empyrean_campaign/images/parallax).
- **Kenney mirror** (CC0): `background-elements-redux` (162 PNG, flat vector, 1 MB), `kenney-backgroundelements`
  (135 files) and `platformer-pack-medieval` (274 PNG) — [ETdoFresh/kenney.nl](https://github.com/ETdoFresh/kenney.nl).
- **SuperTux**: 143 PNG and 12 JPG cartoon backgrounds; GPL v3 repo —
  [data/images](https://github.com/SuperTux/supertux/tree/master/data/images).
- **phaserjs/examples**: `public/assets/skies` has 76 files. Licences are largely undocumented; one folder note says its
  tiles come "from the Free Platformer Game Tileset published by Game Art 2D" —
  [sets/license.txt](https://github.com/phaserjs/examples/blob/master/public/assets/sets/license.txt).

**VFX and particle textures**
- **Kenney Particle Pack 1.1** (CC0, 14 MB): 193 PNG at 512x512, white or grey textures meant for tinting. Counts by
  filename stem: smoke 20, star 18, trace 14, spark 14, flame 12, muzzle 10, magic 10, circle 10, window 8, slash 8,
  twirl 6, scorch 6, light 6, dirt 6, symbol 4, fire 4 —
  [particlePack_1.1](https://github.com/ETdoFresh/kenney.nl/tree/master/particlePack_1.1),
  [License.txt](https://github.com/ETdoFresh/kenney.nl/blob/master/particlePack_1.1/License.txt).
- **Kenney Smoke Particles**: 79 PNG at 362x336; 12 MB —
  [smokeparticleassets](https://github.com/ETdoFresh/kenney.nl/tree/master/smokeparticleassets).
- **Wesnoth halo**: 320 PNG, 2 MB, GPL —
  [halo](https://github.com/wesnoth/wesnoth/tree/master/data/core/images/halo).
- **DCSS** `effect`: 514 PNG at 32x32, mostly CC0 —
  [rltiles](https://github.com/crawl/crawl/tree/master/crawl-ref/source/rltiles).
- **Valyria Tear** `visuals/particle_effects`: 20 PNG and 37 Lua definitions. Licences: GPLv2+ 23, CC0 6, GPLv3 2,
  CC-BY-3.0 1, CC-BY-SA-3.0 1 — [LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt).
- **GDQuest/godot-visual-effects** (Godot VFX demos): "The demo's source code ... is available under the MIT license"
  and "Art assets (image textures and 3D models) are CC-BY-NC-SA 4.0"; 8 MB; last commit 2026-05-16 —
  [LICENSE](https://github.com/GDQuest/godot-visual-effects/blob/master/LICENSE).
- **Superpowers RPG Battle System** `fx`: battle effects, already used by LootFall —
  [CREDITS.md](/home/user/lootfall/CREDITS.md).
- **godotengine/godot-demo-projects** (MIT, 217 MB): only incidental Kenney files, one smoke texture
  (`3d/particles/kenney/smoke_05.png`) and the Kenney Mini Square font —
  [repo](https://github.com/godotengine/godot-demo-projects).

**UI frames and panels**
- **Kenney mirror** (CC0): `uipack_fixed` (153 PNG, 2 TTF, 6 OGG), `pixeluipack` (36 PNG) and `uipack-space`. No RPG
  expansion or fantasy-border folder is present — [ETdoFresh/kenney.nl](https://github.com/ETdoFresh/kenney.nl).
- **XNA RPG Starter Kit**: textures GameScreens 26, Buttons 14, MainMenu 11, HUD 10, Combat 7, under Ms-PL —
  [sample folder](https://github.com/SimonDarksideJ/XNAGameStudio/tree/archive/Samples/RolePlayingGame_4_0_Win_Xbox).
- **Flare** `menus`: 48 files, CC BY-SA 3.0 —
  [fantasycore images](https://github.com/flareteam/flare-game/tree/master/mods/fantasycore/images).
- **Valyria Tear** `gui`: battle 21 PNG, menus 15 PNG plus 4 Lua, themes 12 —
  [LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt).
  - `gui/menus` licences: CC-BY-3.0 8, GPLv2+ 5, CC-BY-SA-3.0 1.
  - `gui/themes` licences: GPLv2+ 6, CC-BY-3.0 3, CC-BY-SA-3.0 3.
- **Wesnoth** `themes`: 75 PNG, GPL — [images](https://github.com/wesnoth/wesnoth/tree/master/data/core/images).
- **Ancient Beast** `interface`: 63 files, CC BY-SA 4.0 —
  [assets](https://github.com/FreezingMoon/AncientBeast/tree/master/assets).
- **GDQuest/game-sprites**: "Everything here is under the CC0 - public domain license"; 11 SVG plus a Godot theme; last
  commit 2018-06-17 — [README](https://github.com/GDQuest/game-sprites/blob/master/README.md).

**Fonts**
- google/fonts (last commit 2026-10-23) stores families in licence folders (`ofl/`, `apache/`, `ufl/`) —
  [google/fonts](https://github.com/google/fonts). Every family checked is present:
  - Fantasy and serif display:
    [ofl/cinzel](https://github.com/google/fonts/tree/main/ofl/cinzel), cinzeldecorative, medievalsharp, almendra,
    almendradisplay, pirataone, metamorphous, uncialantiqua, unifrakturmaguntia, unifrakturcook, imfellenglish,
    macondo, grenzegotisch, newrocker, eaglelake, jimnightshade, fondamento, marcellus, cormorant, cormorantgaramond,
    alegreya, ebgaramond, germaniaone.
  - Big numbers and display: ofl/bebasneue, oswald, anton, lilitaone, bangers, russoone, titanone, bungee, fredoka,
    and [apache/luckiestguy](https://github.com/google/fonts/tree/main/apache/luckiestguy).
  - Pixel: ofl/pressstart2p, silkscreen, pixelifysans, vt323, jersey10, tiny5, sixtyfour.
- A sparse checkout of 15 of these families downloaded about 1 MB of git objects in 3 s —
  [google/fonts](https://github.com/google/fonts).
- The Kenney mirror's `kenney_fontpackage` (12 files) is where LootFall's Kenney Mini font comes from —
  [tools/import_art.py](/home/user/lootfall/tools/import_art.py).

**Sound effects and music**
- **Wesnoth** — [copyrights.csv](https://github.com/wesnoth/wesnoth/blob/master/copyrights.csv),
  [music](https://github.com/wesnoth/wesnoth/tree/master/data/core/music):
  - `data/core/music`: 44 OGG, 171 MB measured. copyrights.csv lists 40 as "GNU GPL v2+" and 4 as "CC BY-SA 4.0".
  - `data/core/sounds`: 272 files (232 OGG, 40 WAV), 7 MB. 271 are GPL v2+ and 1 is CC BY-SA 4.0.
  - Campaign sounds add 4 CC0 entries.
- **0 A.D.**: `audio/music` has 62 files (61 OGG), 211 MB measured. Other audio folders: attack 302 OGG, voice 183,
  actor 223, resource 193, interface 94, ambient 42. Licence CC BY-SA 3.0 —
  [music](https://github.com/0ad/0ad/tree/master/binaries/data/mods/public/audio/music),
  [LICENSE.txt](https://github.com/0ad/0ad/blob/master/LICENSE.txt).
- **Flare**: 136 OGG, 124 of them in fantasycore; CC BY-SA 3.0 — [repo](https://github.com/flareteam/flare-game).
- **Valyria Tear**: 60 OGG and 61 WAV —
  [LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt).
  - Music licences: CC-BY-SA-3.0 6, CC-BY-3.0 6, CC0 2, GPLv2+ 2, GPLv2/3 1.
  - Sound licences: GPLv2+ 61, CC-BY-3.0 15, CC0 12, CC-BY-SA-3.0 9.
- **Shattered Pixel Dungeon**: 31 OGG music tracks (sewers, prison, caves, city and halls, each with 1-3, boss and
  tense variants, plus theme_1, theme_2 and theme_finale) and 67 MP3 sounds; GPL v3 —
  [assets](https://github.com/00-Evan/shattered-pixel-dungeon/tree/master/core/src/main/assets).
- **Stendhal**: `data/music` 53 files (39 OGG), 53 MB; `data/sounds` 517 files (284 OGG, 232 FLAC), 54 MB; GPL v2 repo —
  [repo](https://github.com/arianne/stendhal).
- **Ancient Beast**: music 17 OGG, sounds 11, unit shouts 51 OGG; CC BY-SA 4.0 —
  [assets](https://github.com/FreezingMoon/AncientBeast/tree/master/assets).
- **Superpowers** (CC0): rpg-battle-system 56 OGG, ninja-adventure 62 OGG, medieval-fantasy 20 WAV —
  [repo](https://github.com/sparklinlabs/superpowers-asset-packs). LootFall already uses its battle, boss and title
  music — [CREDITS.md](/home/user/lootfall/CREDITS.md).
- **Kenney mirror** (CC0): `kenney_rpgaudio` 52 OGG, `kenney_impactsounds` 130 OGG, `kenney_musicjingles` 86 OGG,
  `kenney_uiaudio` 52 OGG, `kenney_voiceoverfighter` 47 OGG, plus interface, casino and digital audio packs —
  [ETdoFresh/kenney.nl](https://github.com/ETdoFresh/kenney.nl). Several are already in LootFall —
  [CREDITS.md](/home/user/lootfall/CREDITS.md).
- **Widelands**: "Unless otherwise stated in this file or in the meta tags, all music is licensed under the GPL v2 or
  later"; some tracks are CC-BY-SA 3.0 or CC-BY 4.0 —
  [data/music/LICENSE.txt](https://github.com/widelands/widelands/blob/master/data/music/LICENSE.txt).
- **MegaGlest data**: 1,385 WAV and 64 OGG, CC BY-SA 3.0 —
  [LICENSE.data.txt](https://github.com/MegaGlest/megaglest-data/blob/develop/docs/LICENSE.data.txt).
- **GDQuest/godot-open-rpg**: credits CC0 music by Zane Little and Kenney's sound packs —
  [CREDITS.md](https://github.com/GDQuest/godot-open-rpg/blob/main/CREDITS.md).
- **Kavex/GameSounds**: 22 WAV (blips, coins, explosions, jumps, sci-fi). Its LICENSE is GPL v3 —
  [LICENSE](https://github.com/Kavex/GameSounds/blob/master/LICENSE). That contradicts Kavex/GameDev-Resources, which
  lists it as "Free sounds for any use" —
  [README](https://github.com/Kavex/GameDev-Resources/blob/master/README.md).

### Inferences
- **Painted zones:** Valyria Tear, Ancient Beast, Wesnoth story art and Wyrmsun together cover forest, dark forest,
  desert, canyon, mountain, snow, cave, plains and village. That is enough biome variety to stage a painted look test
  across several zones. The pictures are 4:3 or 16:9, so each would be cropped to a band for LootFall's 1280x420 lane
  (for example a 1920x630 strip). Valyria Tear's two CC0 oil-painting landscapes are the low-friction pair.
- **Parallax:** a painted parallax test would mean splitting painted backgrounds by hand. The cheap alternatives are
  Kenney's flat background-elements packs (vector style) or Superpowers' tiny layers scaled up (pixel style).
- **VFX:** Kenney's particle textures are tintable per rarity in Godot's particle nodes, which suits a 12-rarity ladder
  in any style. Wesnoth's halo frames are a ready animated spell library for private tests only.
- **UI:** there is no good fantasy 9-slice set on GitHub. LootFall already draws its frames in code
  (`tools/import_art.py`), which stays the practical route. XNA's Ms-PL screens could show a classic RPG UI look.
- **Fonts, by style:**
  - Fantasy headers: Cinzel or Cinzel Decorative, MedievalSharp, Uncial Antiqua.
  - Big numbers: condensed Bebas Neue, Oswald or Anton, or chunky Lilita One, Titan One or Luckiest Guy for cartoon.
  - Pixel: Press Start 2P, Pixelify Sans or Silkscreen. Kenney Mini stays for the current pixel look.
- **Audio:** an all-CC0 placeholder soundtrack is possible from Superpowers plus Kenney. Larger orchestral fantasy sets
  (Wesnoth's 44 tracks, 0 A.D.'s 62) are copyleft and better kept for private tests. None were auditioned here.

### Gaps
- None of these were verified on GitHub: Kenney packs released after mid-2020 (fantasy UI borders and newer VFX
  packs), Kenney's RPG UI expansion, or a dedicated CC0 multi-layer fantasy parallax pack such as Ansimuz's. The only
  Ansimuz art found was the non-fantasy Sunny Land tileset inside
  [GDevelop-examples](https://github.com/GDevelopApp/GDevelop-examples).
- The GDevelop asset-store repository could not be found under the guessed names (GDevelopApp/GDevelop-assets and
  4ian/GDevelop-assets); both fail ls-remote.
- No music or sound was listened to, so audio quality is unassessed.
- Licences for most phaserjs/examples assets are undocumented.

## 4. Verification: existence, download route, licence files, sizes, and how well each source matches each style

### Takeaway
- **Verified:** 53 repositories, confirmed on 2026-10-25 with git (ls-remote plus depth-1 or blobless sparse clones)
  and raw.githubusercontent.com. Five guessed names do not exist.
- **Blocked:** for repositories not attached to the session, GitHub's REST API, search API, github.com pages and
  codeload archives all refuse access.
- **Working route:** `git clone --depth=1 --filter=blob:none --sparse` followed by `git sparse-checkout set`. It is
  fast: 29 full depth-1 clones and 9 sparse checkouts ran together in 43 s.

### Cited Findings
**Access checks run from this session**
- `api.github.com/repos/{owner}/{repo}` returned 403, "GitHub access to this repository is not enabled for this
  session", for every candidate.
- The search API returned 403, "sessions are bound to their configured repositories" —
  [search endpoint](https://api.github.com/search/repositories).
- codeload tarballs and github.com pages returned 403 —
  [codeload](https://codeload.github.com/game-icons/icons/tar.gz/refs/heads/master).
- raw.githubusercontent.com returned 200 —
  [raw license.txt](https://raw.githubusercontent.com/game-icons/icons/master/license.txt).
- git over HTTPS works (`git ls-remote`, shallow, blobless and sparse clones) —
  [game-icons](https://github.com/game-icons/icons).
- GitHub's git server advertises protocol v2 with `ls-refs`, `fetch=shallow wait-for-done filter`, `server-option` and
  `object-format`, but no `object-info`. Repository sizes therefore had to be measured by downloading, not queried —
  [game-icons](https://github.com/game-icons/icons).
- Names that do not resolve (git ls-remote asks for credentials, so they do not exist publicly):
  `KayKit-Game-Assets/KayKit-Character-Pack-Adventurers-1.0`, `KayKit-Game-Assets/KayKit-Skeletons-1.0`,
  `KayKit-Game-Assets/KayKit-Character-Animations-1.0`, `GDevelopApp/GDevelop-assets`, `4ian/GDevelop-assets` —
  [KayKit-Game-Assets](https://github.com/KayKit-Game-Assets).

**Master list.** Licence = the file read. Size = measured depth-1 clone unless marked "sparse", which means only the
named folders. Date = last commit on the default branch.

| Repository | What it holds (verified) | Licence (file) | Size | Last commit |
|---|---|---|---|---|
| [game-icons/icons](https://github.com/game-icons/icons) | 4,239 SVG icons, 512 viewBox, single colour | CC BY 3.0, some CC0 ([license.txt](https://github.com/game-icons/icons/blob/master/license.txt)) | 3.8 MB | 2026-04-23 |
| [nagoshiashumari/Rpg-Awesome](https://github.com/nagoshiashumari/Rpg-Awesome) | Icon font from game-icons | OFL/MIT/CC BY 3.0 per README; BSD text in LICENSE.md | <1 MB | 2018-06-14 |
| [microsoft/fluentui-emoji](https://github.com/microsoft/fluentui-emoji) | ~1,285 3D renders at 256 px; 7,882 SVG (Color, Flat, High Contrast) | MIT ([LICENSE](https://github.com/microsoft/fluentui-emoji/blob/main/LICENSE)) | 40 MB (sparse, 3D only) | 2026-08-24 |
| [googlefonts/noto-emoji](https://github.com/googlefonts/noto-emoji) | 2D: 3,750 SVG and PNG; 3D: 4,007 PNG per size up to 512 px | Apache-2.0 images, OFL fonts ([README](https://github.com/googlefonts/noto-emoji/blob/main/README.md)) | 39 MB 2D SVG; 844 MB 3D 512 px (sparse) | 2026-09-24 |
| [jdecked/twemoji](https://github.com/jdecked/twemoji) | 4,009 SVG and 72 px PNG | CC BY 4.0 ([LICENSE-GRAPHICS](https://github.com/jdecked/twemoji/blob/main/LICENSE-GRAPHICS)) | 6 MB | 2026-06-01 |
| [hfg-gmuend/openmoji](https://github.com/hfg-gmuend/openmoji) | 4,565 SVG, 72 px and 618 px PNG (colour and black) | CC BY-SA 4.0 ([LICENSE.txt](https://github.com/hfg-gmuend/openmoji/blob/master/LICENSE.txt)) | 13 MB SVG; 46 MB 618 px (sparse) | 2026-08-12 |
| [crawl/crawl](https://github.com/crawl/crawl) | rltiles: 8,785 tiles at 32 px (items, monsters, paperdoll, effects, GUI) | Mostly CC0 ([LICENSE](https://github.com/crawl/crawl/blob/master/LICENSE)) | 25 MB (sparse rltiles) | 2026-10-24 |
| [crawl/tiles](https://github.com/crawl/tiles) | Curated CC0 export (Nov-2015 and 2010 sets) | CC0 ([README](https://github.com/crawl/tiles/blob/master/README.md)) | 5 MB | 2020-09-04 |
| [SimonDarksideJ/XNAGameStudio](https://github.com/SimonDarksideJ/XNAGameStudio) | XNA RPG Starter Kit: 432 gear icons at 64 px, portraits, characters, UI | MIT repo; Ms-PL in kit folder | 97 MB (sparse RPG kit) | 2025-11-14 |
| [sparklinlabs/superpowers-asset-packs](https://github.com/sparklinlabs/superpowers-asset-packs) | RPG battle (side-view heroes and monsters), medieval-fantasy, backgrounds, ninja-adventure, 3D warriors; OGG and WAV | CC0 ([LICENSE.txt](https://github.com/sparklinlabs/superpowers-asset-packs/blob/master/LICENSE.txt)) | 78 MB | 2019-09-21 |
| [ETdoFresh/kenney.nl](https://github.com/ETdoFresh/kenney.nl) | Kenney mirror, 129 packs (particles, items, RPG, dungeon, UI, castle and town 3D kits, audio) | CC0 per pack ([example](https://github.com/ETdoFresh/kenney.nl/blob/master/particlePack_1.1/License.txt)) | 67 MB for 21 relevant packs (sparse) | 2020-07-17 |
| [KenneyNL/Starter-Kit-3D-Platformer](https://github.com/KenneyNL/Starter-Kit-3D-Platformer) (also City-Builder, FPS, Racing) | Godot starter kits with a few CC0 models and sounds | MIT code, CC0 assets ([README](https://github.com/KenneyNL/Starter-Kit-3D-Platformer/blob/main/README.md)) | 1-4 MB each | 2026-03-12 (Racing 2026-08-21) |
| [KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0) | 5 rigged heroes, 75 animations, 25+ weapons; GLB, glTF, FBX; Godot addon | CC0 ([LICENSE.txt](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/blob/main/LICENSE.txt)) | 22 MB | 2023-09-16 |
| [KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0) | 4 skeleton characters (GLB) | CC0 (LICENSE.txt) | 19 MB | 2024-02-02 |
| [KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0](https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0) | 203 dungeon models | CC0 (LICENSE.txt) | 25 MB | 2023-09-29 |
| [KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0](https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0) | 221 hex-terrain and building models | CC0 (LICENSE.txt) | 28 MB | 2024-04-26 |
| [ValyriaTear/ValyriaTear](https://github.com/ValyriaTear/ValyriaTear) | Painted side-view enemy sheets, 1024x768 scenes, 60 px icons, GUI, music | Per file: GPL, CC BY-SA, CC BY, CC0 ([LICENSES.txt](https://github.com/ValyriaTear/ValyriaTear/blob/master/LICENSES.txt)) | 109 MB | 2022-01-02 |
| [FreezingMoon/AncientBeast](https://github.com/FreezingMoon/AncientBeast) | 51 painted creatures at 400x520, 5 locations at 1080p, SVG icons, music | Art CC BY-SA 4.0, code AGPL ([README](https://github.com/FreezingMoon/AncientBeast/blob/master/README.md)) | 70 MB | 2026-10-25 |
| [wesnoth/wesnoth](https://github.com/wesnoth/wesnoth) | 286 painted portraits at 450-500 px, 7,175 unit images, items, attacks, halo FX, story art, 44 music tracks | GPL v2+ ([data/COPYING.txt](https://github.com/wesnoth/wesnoth/blob/master/data/COPYING.txt); audio in [copyrights.csv](https://github.com/wesnoth/wesnoth/blob/master/copyrights.csv)) | 238 MB for 9 folders (sparse) | 2026-10-24 |
| [flareteam/flare-game](https://github.com/flareteam/flare-game) | Rendered isometric enemies, loot animations, portraits, menus, parallax, 136 OGG | CC BY-SA 3.0 ([LICENSE.txt](https://github.com/flareteam/flare-game/blob/master/LICENSE.txt)) | 818 MB | 2026-08-07 |
| [LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator) | 88,114 paperdoll layers, 15 animations, 64 px frames | Per sheet: OGA-BY, CC BY(-SA), GPL, CC0 ([CREDITS.csv](https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/blob/master/CREDITS.csv)); code GPL v3 | 108 MB | 2026-10-14 |
| [ElizaWy/LPC](https://github.com/ElizaWy/LPC) | LPC Revised, 64,311 PNG | OGA-BY 3.0 ([Credits.txt](https://github.com/ElizaWy/LPC/blob/main/Credits.txt)) | 247 MB | 2023-12-01 |
| [jrconway3/Universal-LPC-spritesheet](https://github.com/jrconway3/Universal-LPC-spritesheet) | Older LPC layers, 2,755 PNG | AUTHORS.txt (licence text not read) | 25 MB | 2026-05-01 |
| [EsotericSoftware/spine-runtimes](https://github.com/EsotericSoftware/spine-runtimes) | 29 Spine example projects; spine-godot runtime | Examples non-commercial or demo-only ([spineboy](https://github.com/EsotericSoftware/spine-runtimes/blob/4.3/examples/spineboy/license.txt), [dragon](https://github.com/EsotericSoftware/spine-runtimes/blob/4.3/examples/dragon/license.txt)) | 81 MB | 2026-10-21 |
| [DragonBones/DragonBonesJS](https://github.com/DragonBones/DragonBonesJS) | Demo armatures (mecha, girls); no fantasy | MIT ([LICENSE](https://github.com/DragonBones/DragonBonesJS/blob/master/LICENSE)) | 24 MB | 2025-05-24 |
| [00-Evan/shattered-pixel-dungeon](https://github.com/00-Evan/shattered-pixel-dungeon) | Small pixel sprites, 31 music tracks, 67 sounds | GPL v3 ([LICENSE.txt](https://github.com/00-Evan/shattered-pixel-dungeon/blob/master/LICENSE.txt)) | 62 MB | 2026-10-06 |
| [watabou/pixel-dungeon](https://github.com/watabou/pixel-dungeon) | Original Pixel Dungeon (87 PNG, 49 MP3) | LICENSE.txt present (not read) | 2 MB | 2015-10-01 |
| [Andrettin/Wyrmsun](https://github.com/Andrettin/Wyrmsun) | RTS pixel units by race, 256 icons, 25 painted or sketch backgrounds, music | GPL v2 repo; per-file credits, 1,633 CC0 entries ([credits.txt](https://github.com/Andrettin/Wyrmsun/blob/master/graphics/credits.txt)) | 390 MB | 2025-04-17 |
| [arianne/stendhal](https://github.com/arianne/stendhal) | Top-down MMORPG sprites (635 items, 416 monsters), music, sounds | GPL v2 ([LICENSE.txt](https://github.com/arianne/stendhal/blob/master/LICENSE.txt)) | 167 MB for sprites, music and sounds (sparse) | 2026-07-12 |
| [0ad/0ad](https://github.com/0ad/0ad) | 783 portraits, 62 music files, .dae models (historical setting) | Art and audio CC BY-SA 3.0 ([LICENSE.txt](https://github.com/0ad/0ad/blob/master/LICENSE.txt)) | 268 MB for music and portraits (sparse) | 2024-08-17 |
| [MegaGlest/megaglest-data](https://github.com/MegaGlest/megaglest-data) | 3D RTS factions in .g3d format, WAV and OGG | CC BY-SA 3.0 ([LICENSE.data.txt](https://github.com/MegaGlest/megaglest-data/blob/develop/docs/LICENSE.data.txt)) | 272 MB | 2025-03-10 |
| [widelands/widelands](https://github.com/widelands/widelands) | Settlers-style game; music | GPL v2+ music, some CC ([music LICENSE](https://github.com/widelands/widelands/blob/master/data/music/LICENSE.txt)) | not measured | 2026-10-22 |
| [SuperTux/supertux](https://github.com/SuperTux/supertux) | Cartoon creatures, backgrounds, particles, music | GPL v3 ([LICENSE.txt](https://github.com/SuperTux/supertux/blob/master/LICENSE.txt)) | not measured | 2026-10-24 |
| [GDQuest/godot-visual-effects](https://github.com/GDQuest/godot-visual-effects) | Godot VFX demos | MIT code, CC BY-NC-SA 4.0 art ([LICENSE](https://github.com/GDQuest/godot-visual-effects/blob/master/LICENSE)) | 8 MB | 2026-05-16 |
| [GDQuest/game-sprites](https://github.com/GDQuest/game-sprites) | 11 prototype SVG and a Godot theme | CC0 ([README](https://github.com/GDQuest/game-sprites/blob/master/README.md)) | <1 MB | 2018-06-17 |
| [GDQuest/godot-open-rpg](https://github.com/GDQuest/godot-open-rpg) | Godot JRPG demo; few assets | MIT; credits CC0 Kenney sounds and Zane Little music ([CREDITS.md](https://github.com/GDQuest/godot-open-rpg/blob/main/CREDITS.md)) | 17 MB | 2026-10-13 |
| [godotengine/godot-demo-projects](https://github.com/godotengine/godot-demo-projects) | Godot demos; incidental Kenney files | MIT ([LICENSE.md](https://github.com/godotengine/godot-demo-projects/blob/master/LICENSE.md)) | 217 MB | 2026-10-08 |
| [google/fonts](https://github.com/google/fonts) | All checked fantasy, numeral and pixel families | OFL, Apache (per-family folders) | about 1 MB for 15 families (sparse) | 2026-10-23 |
| [phaserjs/examples](https://github.com/phaserjs/examples) | 3,609 mixed demo assets | Mostly undocumented ([sets/license.txt](https://github.com/phaserjs/examples/blob/master/public/assets/sets/license.txt)) | not measured | 2026-08-24 |
| [GDevelopApp/GDevelop-examples](https://github.com/GDevelopApp/GDevelop-examples) | Example games, per-example license.txt | Mixed | not measured | 2026-10-24 |
| [Kavex/GameSounds](https://github.com/Kavex/GameSounds) | 22 WAV | GPL v3 ([LICENSE](https://github.com/Kavex/GameSounds/blob/master/LICENSE)) | 1 MB | 2016-12-14 |
| [madjin/100avatars](https://github.com/madjin/100avatars), [M3-org/retro3d-assets](https://github.com/M3-org/retro3d-assets), [PolygonalMind/initiative-opensource-release](https://github.com/PolygonalMind/initiative-opensource-release), [M3-org/base-meshes](https://github.com/M3-org/base-meshes) | Voxel avatars, PSX-style props, Unity and voxel projects, base meshes | CC0 per [awesome-cc0](https://github.com/madjin/awesome-cc0) (own licence files not read) | not measured | 2022-2024 |

Also checked: [sanderfrenken/Universal-LPC-Spritesheet-Character-Generator](https://github.com/sanderfrenken/Universal-LPC-Spritesheet-Character-Generator)
(older generator home, 24,221 PNG) and [MonoGame/MonoGame.Samples](https://github.com/MonoGame/MonoGame.Samples), which
has no fantasy content. The lists [ellisonleao/magictools](https://github.com/ellisonleao/magictools) and
[Kavex/GameDev-Resources](https://github.com/Kavex/GameDev-Resources) were used for discovery but link mostly to tools.

### Inferences
**Style fit.** These ratings are my assessment from the verified contents and the sampled images:
strong / partial / weak / none.

| Source | Painted or illustrated hi-res 2D | Pixel art | Vector or flat cartoon | Low-poly 3D | Licence friction for a shared build |
|---|---|---|---|---|---|
| game-icons | weak (silhouettes) | weak | **strong** (icons, rarity tint) | weak | low (credit) |
| Fluent Emoji 3D / Noto 3D | none | none | partial (Fluent Flat/Color, Noto 2D) | **strong** for icons (3D-render look) | none (MIT) / low (Apache) |
| Twemoji / OpenMoji | none | none | **strong** | none | low (CC BY) / high (BY-SA) |
| DCSS tiles | none | **strong** (32 px, broad slot coverage) | none | none | low (mostly CC0) |
| XNA RPG Starter Kit | none | **strong** (64 px gear, UI) | none | none | low (Ms-PL) |
| Superpowers packs (in use) | none | **strong** (side-view battle set) | none | weak (OBJ warriors) | none (CC0) |
| Kenney mirror | none | partial (dungeon, roguelike, 1-bit) | **strong** (items, toon characters, backgrounds) | **strong** (castle and town kits) | none (CC0) |
| KayKit | none | none | none | **strong** (rigged, animated heroes and skeletons, weapons) | none (CC0) |
| Valyria Tear | **strong** (side-view battlers, 1024x768 scenes) | partial (map sprites) | none | none | high (mostly GPL or BY-SA; a few CC0) |
| Ancient Beast | **strong** (51 creatures, 1080p locations; static) | none | partial (SVG ability icons) | none | high (BY-SA 4.0) |
| Wesnoth | **strong** (portraits, story art) | **strong** (72 px units) | none | none | high (GPL) |
| Flare | partial (rendered, painterly, isometric) | none | none | partial (rendered 3D look, .blend sources) | high (BY-SA 3.0) |
| LPC generator / LPC Revised | none | **strong** (paperdoll, 15 animations) | none | none | medium to high (mixed BY, BY-SA, GPL) |
| Wyrmsun | partial (CC0 sketch backgrounds) | partial (RTS units, icons) | none | none | mixed (many CC0) |
| Stendhal, Shattered/Pixel Dungeon | none | partial (top-down or tiny) | none | none | high (GPL) |
| 0 A.D., MegaGlest | partial (historical portraits) | none | none | partial (realistic models; .g3d needs conversion) | high (BY-SA) |
| Spine examples | partial (painted goblins, dragon) | none | partial (cartoon) | none | blocking (non-commercial or demo-only) |
| google/fonts | all styles | all styles | all styles | all styles | none (OFL) |

**A placeholder kit per candidate style, all verified on GitHub:**
- **Pixel art:** Superpowers (already in use), DCSS items and paperdoll, XNA gear, LPC for hero gear layers, Kenney
  particles, Press Start 2P or Pixelify Sans, Superpowers and Kenney audio. Everything except LPC is CC0 or Ms-PL.
- **Vector or flat cartoon:** game-icons tinted by rarity, Twemoji (or Fluent Flat) for companions and odd slots,
  Kenney generic items, toon characters and background elements, Kenney particles, Lilita One or Luckiest Guy, Kenney
  audio.
- **Low-poly 3D:** KayKit heroes, skeletons, weapons, dungeon and hex biomes, Kenney castle and fantasy-town kits,
  Fluent or Noto 3D emoji as item icons, Bebas Neue or Titan One, Kenney audio. All CC0, MIT or Apache.
- **Painted or illustrated:** Valyria Tear battlers and backgrounds, Ancient Beast creatures and locations, Wesnoth
  portraits and story art, Wyrmsun CC0 sketches, Wesnoth halo FX, Cinzel, Wesnoth or 0 A.D. music. Almost all of it is
  copyleft, so keep it to private tests. Icons are the weak spot.

**Fetching:** `tools/import_art.py` already fetches GitHub packs by name. The cheapest way to add any of these is a
blobless sparse checkout of just the needed folders, for example Wesnoth portraits at 32 MB or DCSS rltiles at 25 MB,
rather than full clones such as Flare at 818 MB.

### Gaps
- GitHub release-asset downloads were not tested; github.com archive URLs returned 403.
- Full depth-1 sizes were not measured for the very large repositories (Wesnoth, 0 A.D., google/fonts, noto-emoji,
  fluentui-emoji, openmoji, widelands, phaserjs/examples, XNAGameStudio, SuperTux, the Kenney mirror, the M3-org and
  PolygonalMind repos); only folder-level sparse sizes are given.
- Not read: the Spine runtimes' own LICENSE (terms for using the runtime in a product), and the licence files of
  watabou/pixel-dungeon, jrconway3's LPC, madjin/100avatars, M3-org/retro3d-assets and PolygonalMind.
- Style was judged from one 24-image contact sheet, not by reviewing each set in full. Ratings are an assessment.
- Discovery depended on known repository names and three awesome lists, because web search was unavailable and the
  GitHub search API is blocked here. Unknown GitHub-hosted packs (for example mirrors of popular itch.io packs) may
  exist.
