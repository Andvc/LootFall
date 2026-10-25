# LootFall art direction

The rules every picture, effect and screen in LootFall follows, and the pipeline that will make the final art. The
research behind them is in `reports/LootFall art direction.md` (styles compared, what AI generation can do in late
2026, how other loot games present fights, rarity and huge inventories); its notes are in `research_notes/`.

## The decision

**2D, side view, inked cel.** The knight, the monsters, chests, loot and effects are drawn in an inked cel style: one
bold dark outline, flat fills with two or three shade steps, light from the top left. They stand in front of painted
backdrops that are deliberately softer, less saturated and lower in contrast than anything that moves. The knight
fights alone in one lane, facing right; monsters come in from the right.

Why this and not the alternatives, in short:

- **Clarity.** The crisp-foreground, soft-background split (Torchlight, Diablo III, Hades) keeps every actor readable
  against any place. A dark outline separates anything from anything.
- **What AI can make.** Generators make near-professional single stills but drift across sets and flicker across
  frames. Inked cel is the style whose rules a script can check and enforce (outline width, palette, light direction,
  framing), and its flat fills give a model little detail to get wrong. AI makes only neutral, static stills. Everything
  that must read the same way every time is code: rarity, frames, motion, effects, numbers.
- **Cost per monster.** 3D would make every gear combination free on the hero, but each monster body plan would need
  its own rig and clips, which is exactly where AI rigging and motion fail. In 2D a new monster is one illustration.
- **Gear that shows.** High resolution leaves room for ten visible gear pieces and twelve tiers. Pixel art at lane size
  doesn't, and it breaks when static parts are rotated and squashed, which is how everything here is animated.

The knight is the one asset that could later move to real-time 3D, after a timed pilot. Nothing else would change,
because backdrops, monsters, icons, UI and effects are 2D either way.

## Style rules

These are the style bible's rules. Prompts state them, the importer checks them, and code-drawn placeholders follow
them.

| Rule | Value |
|---|---|
| Outline | One uniform outline in ink `#15101f` (near-black, violet bias); 1.5% of an actor's height, about 6 px on a 2x master, 3 px on screen. Inner lines thinner, same ink. |
| Fills | Flat. Each material has a shadow, a mid and a light step; metal may add one highlight. No gradients across a whole form, no texture noise, no painted cast shadows (the game draws a soft ground shadow). |
| Light | From the top left, 45 degrees, for every actor, item and part. |
| Proportions | Chunky and heroic. The knight is about four heads tall, with an oversized helm and weapon (the weapon is about 0.9 of his height) so gear reads at lane size. Silhouettes recognisable in solid black. |
| Colour | One saturated focus colour per actor; everything else in muted materials from the ramp library. Gear art is neutral: steel, iron, leather, cloth and wood, never a rarity colour. |
| Backdrops | Painterly. Saturation at most 45%, value between 15% and 75%, lower contrast than any actor. Far layers hazier and lighter, the ground band darker. Nothing bright or busy at actor height. |
| Hue families | The knight's blows are cool white and pale gold. Monster threats are red and orange-red. Rarity colours are reserved for loot. |

### The ramp library (materials)

Each ramp is shadow, mid and light, then an optional highlight. The importer snaps generated art toward these, and
placeholders are drawn from them.

| Material | Shadow | Mid | Light | Highlight |
|---|---|---|---|---|
| Iron | `#3b3f4a` | `#646b78` | `#959dab` | `#d4dae4` |
| Steel | `#47506a` | `#7a87a3` | `#b4c0d6` | `#eef3fb` |
| Leather | `#4a2f22` | `#7a5038` | `#a77552` | |
| Cloth | `#3a2b45` | `#5f4a72` | `#8a76a0` | |
| Gold | `#7a4f12` | `#c48a22` | `#f2c45a` | `#fff2bf` |
| Wood | `#4b3220` | `#7b5534` | `#a77a4e` | |
| Bone | `#7d7462` | `#b8ad94` | `#e6dcc4` | |
| Skin | `#9c5f48` | `#d08b6a` | `#f0b693` | |

## The screen

The canvas is **1920x1080**, stretched in `canvas_items` mode with the aspect kept. Textures use linear filtering with
mipmaps, and art is authored at twice its on-screen size, so it stays crisp up to 4K.

| Region | Where | What |
|---|---|---|
| The lane | y 0 to 640 | Parallax backdrop. The ground line is at y 575. The knight's feet are at x 480; monsters engage at x 760 and x 940 and walk in from x 2080. |
| Top bar | y 0 to 96, over the lane | Level and XP, gold, shards, aether (once open). Depth, realm and the depth's progress (waves, then the boss) in the middle. Settings and menu on the right. |
| Bottom panel | y 640 to 1080 | Left, x 0 to 520: the chest tray (the pile, Open, Auto). Middle, x 520 to 1400: a small figure of the knight wearing what counts, his 14 regions with what's worn in each, and the latest finds. Right, x 1400 to 1920: the dock (Talents, Forge, Ranks, Codex, Ascend), each with its badge. |
| Overlays | full screen | The equipment screen (a paper doll and the slots, [equipment.md](equipment.md)), the talent tree, the codex, the prestige trees, settings and the pause menu. A shade under each; Esc steps back or closes it. |

The on-screen budget holds whatever the power level: at most **12 monsters and 10 floating numbers** at once. Growth
shows as speed and reach, not as density.

## Combat presentation

Combat is automatic. The rules settle each depth in closed form, and `scripts/Game/Stage.cs` turns that into what's
shown: waves of two walking in, blows spread so each monster falls exactly when its kill time is up, the boss's health
draining with the depth's progress, and leaps when overkill clears depths by the hundred. The screen never decides an
outcome. How the fight is shown follows the kill time, not the depth:

| Mode | When | What the lane shows |
|---|---|---|
| Duel | A monster takes 0.8 s or more | Every swing with anticipation and impact; flinch and knockback; a puff and coins on a kill; one number per hit. |
| Wave | Faster kills | One arc fells the front monster; numbers merge per swing; a kill tally ("x12") instead of a number per kill. |
| Leap | Overkill clears several depths at once (`StageMode.Leap`) | The knight dashes, the parallax races, the depth counter rolls, and a banner reads "Depth 1,240 to 1,480". Chests fall as counts; only notable finds pop. |
| Gate | The next boss can't be beaten yet | The knight farms the deepest comfortable depth. The boss waits behind a gate at the right, labelled with what it needs ("needs x1.8 damage"). |

A boss always plays in full, whatever the mode: it steps in with a name plate, a health bar and the 30-second timer.
A boss killed in one blow gets a stinger: 120 ms of hit-stop, a flash and its own sound.

**Feel.** Hit-stop freezes only the attacker and the target, never the whole game: 40 ms on a crit (at most every half
second), 120 ms on a boss kill, and never more than 150 ms. Shake uses trauma squared, capped. Flashes are capped at
35% alpha. The Settings screen has separate switches for shake, flashes and damage numbers.

**Numbers.** Every number goes through `Format` in the player's notation (mixed, letters, scientific, engineering).
Floating numbers use Lilita One with an ink outline: white for the knight's blows, gold and 1.4 times the size for
crits, red-orange for damage taken. They stay above the target, merge per swing, and drop the oldest past the budget.

**The lootfall.** A chest is seen leaving the monster that dropped it, with a beam in the colour of the rarest thing
inside, and falls into the tray. Late in the game, when chests come by the dozen, they fall as counts, and only the
reserved signals (a new tier, an upgrade, a first find) still pop.

## The rarity ladder

Hue is fixed per tier, and it is drawn by code, never painted into the art. Twelve tiers are too many for colour alone,
so tiers come in four bands, and each band has its own frame and its own way of opening. Every tier also carries its
numeral.

| # | Tier | Colour | Band | Frame | Opening |
|---|---|---|---|---|---|
| I | Common | `#c9c9c9` light grey | A | Plain | A quick pop, half a second |
| II | Uncommon | `#1eff00` green | A | Plain | Card colour and a soft sound |
| III | Rare | `#0070dd` blue | A | Plain | As above, with a ring |
| IV | Epic | `#a335ee` purple | B | Ornate | A beam and its own sound |
| V | Legendary | `#ff8000` orange | B | Ornate | A beam, sparks, a fanfare |
| VI | Mythic | `#e8283c` red | C | Crowned | Flash and shake, an animated edge |
| VII | Ascended | `#ff4fd8` pink | C | Crowned | As above |
| VIII | Celestial | `#00ccff` cyan | C | Crowned | As above |
| IX | Divine | `#e6cc80` pale gold | D | Radiant | A held reveal: the card glows face down before it turns |
| X | Primordial | Dark face `#140b1f`, violet glow `#8a5cff` | D | Radiant | As above, with its own sound |
| XI | Eternal | Silver face `#e9edf5`, cyan edge `#5fe0ff` | D | Radiant | As above |
| XII | Transcendent | Animated prismatic | D | Radiant | As above |

These colours are for frames, beams and backgrounds. Text in a tier's colour is lightened until it reads on the dark
panels (`Look.TierText`).

**Emphasis follows relevance, not tier.** The hue says what an item is. How loudly it's shown (size, beam, sound,
pulse) says how much it matters now. An upgrade or a first find shouts; a Divine the knight can't use is just listed.
The top signals are reserved: band D's reveal never plays for anything else.

## Items and icons

Every icon is built from three layers:

1. **Base art.** One neutral picture per item family and material tier.
2. **Rarity frame and background**, drawn by code in twelve variants by band.
3. **State overlays**, drawn by code: the upgrade arrow and its verdict ("+3.2% reach"), the NEW badge for a first
   find, affix dots, the forge level, rank stars, mastery pips and the tier numeral.

The 720 kinds of piece reduce to **314 families** (`Anatomy.FamilyOf`): left and right mirror one design, and
numbered pieces share one (the ten finger rings are one family, the gems set in every board another). Each family comes
in **five material tiers** following the codex eras:

| Art tier | Codex eras | Materials |
|---|---|---|
| T1 | 0 to 1 (Iron, Steel) | Plain forged metal, leather, rough cloth |
| T2 | 2 to 3 (Silvered, Runed) | Fine steel with silver inlay, etched runes |
| T3 | 4 to 5 (Mithril, Adamant) | Pale bright metal, gem settings, gilt trim |
| T4 | 6 to 7 (Dragonbone, Starsteel) | Bone, scale and dark star-metal, glowing seams |
| T5 | 8 and beyond (Voidforged onward) | Otherworldly: void glass, sunfire, astral filigree |

Code tints within a tier by era, so all sixteen eras look different.

## The knight

He is a cut-out rig built in code (`HeroRig`): about sixteen body parts on bones, plus attachments for ten visible
gear slots. All of his animation is code: idle breathing, walk, wind-up, strike, flinch, dash, victory.

| Shown as | Slots |
|---|---|
| Attachments, in neutral art with a trim region tinted by code in the piece's rarity colour | Main hand (the weapon), off hand (shield), helm, breastplate, tabard or tunic, cloak, near pauldron, near gauntlet, belt, boots |
| Glints on the sword hand, in the best ring's colour | The finger rings |
| Sparkles at the neck and ears | Amulet, necklace, earrings and the other jewellery |
| Sigils on the arms that flare on a crit | Tattoos |
| Motes that orbit him | Trinkets |
| A small actor beside him | The familiar (companion slots) |
| Shader and particle layers | Celestial slots: halo, wings, aura, starcrown, comet, nebula, eclipse, zenith, the eight constellations |

A gear piece changes shape only within its slot's envelope, so the knight always reads as the knight.

## Monsters

Each is one illustration: a side view facing left, the whole body in frame, standing on the ground line. Code moves it
by body plan (`Body` in `scripts/Game/Bestiary.cs`):

| Body | Motion |
|---|---|
| Walker | Bob and stride lean |
| Hopper | Squash and hop |
| Flyer | Hover on a slow wave, never touching the ground |
| Heavy | Slow stomp that shakes the ground |
| Slither | Ripple along the ground |

A boss is one of its realm's monsters, half as big again, with an aura and a name plate. A guardian (every tenth depth)
is unique. Realms follow the codex eras, eight of them with four monsters and a guardian each, then round again darker
("Greenwood II").

## Backdrops

Each realm has four layers, scrolling at different speeds:

| Layer | Parallax | Size on screen (master is 2x) |
|---|---|---|
| Sky | Static | 1920x640 |
| Far | 0.15 | 1920x576, tileable |
| Ground band | 1.0 | 1920x160 strip under the actors, tileable |
| Foreground | 1.3 | Sparse dark silhouettes along the bottom edge |

## Effects

Effects are code-driven: particles, shaders, polygon arcs and rings. They are built from a library of 30 to 60
greyscale textures on transparent or black (spark, star, soft circle, ring, slash arc, smoke puff, rays, rune, flare),
tinted and blended additively.

## UI

- **Fonts.** All are vector fonts from google/fonts (OFL) and final, not placeholders:
  - Cinzel for titles and headers;
  - Barlow Semi Condensed for UI text and figures, with tabular digits so counters don't jitter;
  - Lilita One for floating numbers and banners.
- **Type scale (1080p).** 18 for small print, 22 for body, 28 for subheads, 36 for headers, 48 for titles, 72 and up
  for banners.
- **Tokens** (`scripts/View/Look.cs`):

  | Token | Colour |
  |---|---|
  | Panel | `#17131f` at 92% |
  | Edge | `#3a3150` |
  | Text | `#efe9f7` |
  | Muted | `#a59cb8` |
  | Gold | `#ffd166` |
  | Good | `#6be38b` |
  | Bad | `#ff6b6b` |
  | Threat | `#ff5a3c` |

- **Frames.** Panels, buttons, slots and rarity frames are drawn in code. AI may add ornament fills at most, never the
  frame geometry.
- **Focus and input.** Buttons never take focus: Space and Enter belong to the chest. Esc closes the top overlay, or
  opens the pause menu.
- **Unlocks.** Each system's button and tab appears only when it unlocks (`Session.Unlocked`), with a one-time
  highlight.

## The asset pipeline

The pipeline follows four rules:

1. **Neutral and static.** A model is never asked for a rarity colour, a glow, a background, text or motion.
2. **Families.** Each archetype has a base design, and its variants are made by multi-reference editing and palette
   ramps, not by independent generations.
3. **Keyed alpha.** Art is generated on a flat key colour (`#00ff00`), or once on white and once on black. The repo
   mattes it, so no provider's transparency is trusted. For art with a painted background, `rembg` is the fallback.
4. **Scripted acceptance.** `tools/art.py import` mattes, crops and centres each picture, gives it the ink outline and
   flattens its fills to a few flat steps (48 colours, no dithering). It then checks the bounding-box fill, the light direction (the brightest quadrant) and
   whether it reads at display size, and writes a contact sheet. The bot's screenshots then judge it in context.

Generation runs wherever the owner runs the models: their own machine, or a GitHub Actions workflow with API keys as
secrets. Cloud sessions reach only GitHub. Everything after generation runs in the repo.

A **style bible** of 20 to 30 approved images anchors the set and serves as every prompt's reference: the knight, three
monsters, ten icons, two backdrops and one UI concept.

`tools/art.py manifest` writes `art/manifest.json`, generated from the game's content so it never falls behind. It
lists every asset with its id, its kind, its target size, its prompt and its status (placeholder or final). New art
dropped into `art/incoming/<id>.png` goes through `tools/art.py import` into `assets/`.

### Specifications

| Asset | Count | Master | On screen |
|---|---|---|---|
| Item icons | About 85 families x 5 tiers | 512 px square, object inside the centre 80% | 48 to 96 px |
| Knight parts | About 16, plus about 10 gear types x 3 to 5 shapes x 5 tiers | One parts sheet, side view facing right, limbs clear, pivots marked | Knight 280 px tall |
| Monsters | 32 plus 8 guardians, each realm with variants | Side view facing left, 2x the largest size shown (up to 1000 px tall for guardians) | 150 to 560 px tall |
| Backdrop layers | 4 per realm | 3840 px wide, horizontally tileable, each on a key colour | The 1920x640 lane |
| Effect textures | 30 to 60 | 512 px greyscale on black | Lane overlays |
| Guardian portraits, store art | 1 per guardian, plus the store page | Painted over by hand | Boss intros, Steam |

### Prompt templates

The style preamble goes first in every prompt:

> Inked cel illustration for a 2D fantasy game. One bold uniform dark outline (#15101f), flat colour fills with three
> shade steps, light from the top left, no gradients, no texture noise, no cast shadow, no text, no glow, no frame.
> Flat pure green background (#00ff00) filling the canvas edge to edge.

Then the template for the kind of asset:

| Kind | Template |
|---|---|
| Monster | `{style} Full-body side view of {name}: {description}. Facing left, standing on a flat ground line, whole body in frame with an 8% margin. Chunky silhouette that reads in solid black. One saturated focus colour ({focus}); everything else in muted {materials}.` |
| Icon | `{style} Game item icon: {tier materials} {family} ({description}). Three-quarter view, tilted 30 degrees, centred, filling 80% of a square canvas. Neutral metal, leather and cloth colours only.` |
| Knight parts | `{style} Character parts sheet: a stout knight about four heads tall, side view facing right, neutral pose. Head, torso, pelvis, both upper arms, forearms and hands, both thighs, shins and feet drawn separately on a grid, each whole, with joints overlapping by 15%.` |
| Gear piece | `{style} {tier materials} {piece} for the knight, drawn as worn in side view facing right, on the {part} template. Neutral colours, with the trim to be tinted painted in pure magenta (#ff00ff).` |
| Backdrop layer | `Painterly fantasy landscape, {realm description}, the {layer} layer of a side-scrolling parallax scene, horizontally tileable, soft edges, desaturated, low contrast, no characters, no text. Above the silhouette, flat pure green (#00ff00).` |
| Effect texture | `Single {effect} shape for a game effect, white to grey on pure black, soft edges, centred, no colour.` |

### Steam

Disclose generative AI for in-game content and store art from the first build anyone else sees. The knight's parts, the
guardians and the store capsule get human paint-over, which matters both for the store page and for authorship. Keep
the prompts and the generation logs with the art.

## Placeholders

Every placeholder will be replaced. They exist to stage the look and exercise the code paths, and
`tools/placeholders.py` builds them all, recording the source of each in `CREDITS.md`.

| Layer | Stand-in | How it's processed |
|---|---|---|
| Monsters | Ancient Beast's 51 painted creatures (CC BY-SA 4.0) | Matted with `rembg`, cropped, haze dropped, given the ink outline, flipped to face left |
| Backdrops | Valyria Tear battle scenes (CC0, CC BY-SA 3.0, GPL), Ancient Beast locations (CC BY-SA 4.0) | Banded, desaturated, value-compressed, softened. Sky gradient, ground band and foreground silhouettes drawn in code. |
| Item icons | game-icons (CC BY 3.0) silhouettes | Filled with the family's material ramp, lit from the top left, outlined in ink |
| The knight | Drawn in code: inked shapes on the real rig | The rig, its pivots and its clips are final; only the parts are stand-ins |
| Effects | Kenney Particle Pack (CC0) | Greyscale, tinted in code |
| Sounds and music | Kenney (CC0) and Superpowers (CC0), as before | |
| Fonts | Cinzel, Barlow Semi Condensed, Lilita One (OFL) | Final |

Copyleft stand-ins (CC BY-SA, GPL) are credited in `CREDITS.md`. They should stay out of any build shared beyond the
owner, until they're replaced.
