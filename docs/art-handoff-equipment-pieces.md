# Art handoff: the equipment screen, phase 2 (the pieces, worn and bare)

Every region of the knight's body should show clearly both bare and with what it wears. Phase 1 gave the bare knight;
this phase gives a picture of the bare body for every region, and a picture of every piece as it's worn. With them the
equipment screen can draw each piece where it goes, in its own shape, in place of the colour fills it uses now. They
are to be made with GPT-Image-2 (through Codex) and uploaded to the paths below.

- **This page:** what each kind of file is, how the game uses it, its specification, how to make it and where it goes.
- **Every file, one by one:** [art-handoff-equipment-pieces-list.md](art-handoff-equipment-pieces-list.md), a
  checklist of all 869 files in the order to make them.
- **The same list for tools:** [`art/equipment/handoff-pieces.json`](../art/equipment/handoff-pieces.json). It gives
  each file's full prompts, the picture it's painted on, its mask, its references, its checks and its use.
- **The tool:** `tools/equipment_pieces.py`. It makes the pictures to paint on and the masks (`inputs`), checks what's
  delivered (`check`), draws review sheets (`sheet`) and ticks off the list (`list`).

Phase 1 (the bare knight, the close-ups, the backdrops and the glyphs) is in
[art-handoff-equipment.md](art-handoff-equipment.md), and the style rules are in [art-direction.md](art-direction.md).
What the slots and pieces are, and how the screen works, is in [equipment.md](equipment.md).

**Status: requested on 25 October 2026.** Nothing is delivered yet.

## At a glance

| What | Files | Made by | Goes to |
|---|---|---|---|
| A. Plates: his bare body, region by region | 13 | An edit of a seed cut from the figure | `art/equipment/plates/<plate>.png` |
| B. Pieces as worn, painted on the body | 762 | Two edits of a base picture, with a mask | `art/equipment/pieces/<base>/<family>.png` |
| C. Held pieces, companions and the sky | 13 | A prompt | `art/equipment/sprites/<name>.png` |
| D1. Pieces set in pieces, painted on their host | 48 | Two edits of the host's picture, with a mask | `art/equipment/pieces/<base>/<host>/<family>.png`, `art/equipment/sprites/<host>/<family>.png` |
| D2. Pieces set in pieces, drawn on their own | 28 | A prompt | `art/equipment/attachments/<name>.png` |
| D3. Sheets of small parts | 5 | A prompt | `art/equipment/sheets/<family>.png` |
| E. Drawn by the game | none (21 families) | Code | |

They come in three batches, in the order a player meets the pieces:

| Batch | Files | What's in it |
|---|---|---|
| 1 | 277 | The plates, then every piece that opens by depth 300 (the first hours), starting with the four a new game begins with: the sallet, the main hand, the breastplate and the boot |
| 2 | 299 | The pieces that open by depth 3,000 |
| 3 | 293 | The rest, and the prestige pieces |

All 720 pieces are covered: 617 by these files, and 103 drawn by the game (E).

## How the game will use them

**Each region has two states.**
- **Bare:** the region's own picture with nothing on it.
  - The neck, torso, back, waist, arms and legs each get a plate (A).
  - The head, hands and feet use the phase-1 close-ups.
  - The body view uses the whole figure.
- **Equipped:** each piece that counts is drawn over the bare picture, in its own picture (B), in layer order. The
  order is ink, then cloth, mail, plate and what's worn over everything, then jewellery, then what's held or grown.
  Taking a piece off shows what's under it, down to the bare body.

**The screens.**
- **The whole body:** the equipment screen's body view (front and back side by side) and the main screen's small
  figure.
  - The figure is drawn with every counting piece's whole-figure picture (`figure_front`, `figure_back`).
  - A piece with no whole-figure picture of its own is drawn from its plate pictures, scaled down. Plates line up
    with the figure exactly, so this needs no extra file.
- **A region's page,** when the camera zooms in on the neck, torso, back, waist, an arm or a leg:
  - the plate fades in over the zoomed figure, as the close-ups do now;
  - the region's pieces are drawn on it, sharp;
  - pieces of the neighbouring regions at the page's edges come from their own pictures, dimmed like the rest of the
    body outside the region.
- **A close-up page:** the head from three sides, a hand's back and palm, or a foot's top and sole, each with its
  pieces' close-up pictures.
- **Pointing at a worn piece** lights its own shape (its picture's outline) in its rarity, hides the slots under it,
  and shows its own slots on it, each at its place on the picture.
- **Choosing a piece that isn't worn** shows its picture faint, where it would go.
- **Held pieces stand beside him.** His hands are open in the delivered figure, so that rings, gloves and every other
  hand piece fit one shape of hand.
  - The main hand's weapon stands upright at his right side, and the off hand's piece at his left, like arms on
    display.
  - On a hand's close-up, its held piece stands beside the hand.
- **Companions:** they stand at his side, and the falcon perches on his right shoulder. The comet, the eclipse and the
  orbiting stones circle in the sky round him.
- **Pieces set in pieces:** a helm's crest and visor, a sword's pommel, a ring's stone or a belt's pouches are drawn
  on their host's picture.
  - Those whose shape follows the host are painted on it (D1).
  - Rigid ones are placed at their slot (D2, D3).

**What code still draws.** Everything that means something:
- rarity colours and glows, enchantments and paint;
- the colour of runes, gems and heraldry;
- the era of the metal: the art's plain steel is recoloured for each era (iron, steel, mithril, starsteel, void);
- sparkle and motion.

The art stays neutral and still.

**Until a file arrives,** the game keeps its current drawing of that piece (the colour fills). Batches can therefore
arrive one at a time, and each one makes the screen better.

## The pictures everything is painted on (bases)

Pieces are painted on a **base**: a rectangle of one of the delivered views, at a fixed size.
- **Alignment:** a base maps onto its view by one scale and one offset, so a piece painted on it lines up with the
  figure exactly.
- **Size:** every base fits what the image tool really returns, about 1.57 million pixels at the shape asked for.
  Phase 1's figures came back at 941 × 1672, for example. Nothing is generated small and scaled up, and every side
  is a multiple of 16.
- **The picture to paint on:** `python3 tools/equipment_pieces.py inputs` writes each base as a picture,
  `art/equipment/inputs/bases/<base>.png`: the delivered art on flat green.

| Base | View | Size (px) | Scale | What it shows |
|---|---|---|---|---|
| `figure_front` | front | 944 × 1664 | 0.83 | The whole knight from the front |
| `figure_back` | back | 944 × 1664 | 0.83 | The whole knight from behind |
| `neck` | front | 1328 × 1168 | 4.85 | His neck and collarbones from the front, up close (the plate) |
| `torso` | front | 1104 × 1408 | 2.28 | His chest and belly from the front, up close (the plate) |
| `back` | back | 1088 × 1440 | 2.06 | His back from behind, up close (the plate) |
| `waist_front` | front | 1504 × 1024 | 3.02 | His waist, hips and the top of the thighs from the front (the plate) |
| `waist_back` | back | 1488 × 1040 | 2.82 | His waist, hips and the top of the thighs from behind (the plate) |
| `right_arm_front` | front | 960 × 1616 | 2.23 | His right arm from the front (the plate) |
| `right_arm_back` | back | 960 × 1616 | 2.19 | His right arm from behind (the plate) |
| `left_arm_front` | front | 960 × 1616 | 2.23 | His left arm from the front (the plate) |
| `left_arm_back` | back | 960 × 1616 | 2.19 | His left arm from behind (the plate) |
| `right_leg_front` | front | 864 × 1792 | 1.86 | His right leg from the front (the plate) |
| `right_leg_back` | back | 880 × 1760 | 2.01 | His right leg from behind (the plate) |
| `left_leg_front` | front | 864 × 1792 | 1.86 | His left leg from the front (the plate) |
| `left_leg_back` | back | 896 × 1744 | 2.02 | His left leg from behind (the plate) |
| `head_right` | head_right | 1024 × 1504 | 1.62 | His head and neck in profile, his right side to us |
| `head_front` | head_front | 976 × 1600 | 1.70 | His head and neck from the front |
| `head_left` | head_left | 1024 × 1504 | 1.62 | His head and neck in profile, his left side to us |
| `hand_R_back` | hand_R_back | 992 × 1568 | 1.46 | The back of his right hand, fingers up |
| `hand_R_palm` | hand_R_palm | 992 × 1568 | 1.46 | The palm of his right hand, fingers up |
| `hand_L_back` | hand_L_back | 1008 × 1552 | 1.45 | The back of his left hand, fingers up |
| `hand_L_palm` | hand_L_palm | 1008 × 1536 | 1.44 | The palm of his left hand, fingers up |
| `foot_R_top` | foot_R_top | 816 × 1904 | 1.80 | The top of his right foot, toes up |
| `foot_R_sole` | foot_R_sole | 816 × 1920 | 1.80 | The sole of his right foot, toes up |
| `foot_L_top` | foot_L_top | 816 × 1904 | 1.80 | The top of his left foot, toes up |
| `foot_L_sole` | foot_L_sole | 800 × 1920 | 1.77 | The sole of his left foot, toes up |
| `head_right_tall` | head_right | 960 × 1616 | 1.16 | As `head_right`, with room above for tall headgear |
| `head_front_tall` | head_front | 912 × 1712 | 1.22 | As `head_front`, with room above for tall headgear |
| `head_left_tall` | head_left | 960 × 1616 | 1.16 | As `head_left`, with room above for tall headgear |

The scale is the base's pixels per pixel of the imported view (`assets/equipment/<view>.png`).
- The whole figure's bases have room above his head (hats, horns, a banner) and at his sides (wings, cloaks). That is
  why they're a little smaller than the imported figure.
- A plate's base is the delivered plate (A) once it's in. Until then it's the plate's seed, which is only for making
  the plate.
- The `_tall` head bases have room above the head for the tall headgear: the wizard's hat, the horns, the horned helm
  and the crown.

## A. Plates: his bare body, region by region (13 files)

| File | Size (px) | Scale | What |
|---|---|---|---|
| `plates/neck.png` | 1328 × 1168 | 4.9× | His bare neck and collarbones, from the front |
| `plates/torso.png` | 1104 × 1408 | 2.3× | His bare chest and belly, from the front |
| `plates/back.png` | 1088 × 1440 | 2.1× | His bare back, from behind |
| `plates/waist_front.png` | 1504 × 1024 | 3.0× | His bare waist, hips and the top of the thighs, from the front |
| `plates/waist_back.png` | 1488 × 1040 | 2.8× | His bare waist, hips and the top of the thighs, from behind |
| `plates/right_arm_front.png` | 960 × 1616 | 2.2× | His bare right arm, from the front |
| `plates/right_arm_back.png` | 960 × 1616 | 2.2× | His bare right arm, from behind |
| `plates/left_arm_front.png` | 960 × 1616 | 2.2× | His bare left arm, from the front |
| `plates/left_arm_back.png` | 960 × 1616 | 2.2× | His bare left arm, from behind |
| `plates/right_leg_front.png` | 864 × 1792 | 1.9× | His bare right leg, from the front |
| `plates/right_leg_back.png` | 880 × 1760 | 2.0× | His bare right leg, from behind |
| `plates/left_leg_front.png` | 864 × 1792 | 1.9× | His bare left leg, from the front |
| `plates/left_leg_back.png` | 896 × 1744 | 2.0× | His bare left leg, from behind |

- **What:** a region's page rectangle of the whole figure, with a margin, redrawn sharp at two to five times the
  imported figure's size.
- **Why:**
  - It is the region's bare state at its page's zoom. The zoomed figure is soft there, because it was generated at
    941 × 1672 for the whole body.
  - It is the base every piece of the region is painted on.
- **Seed:** `art/equipment/inputs/seeds/<plate>.png`, the rectangle cut from the figure, scaled up and soft, on flat
  green.
- **How:** one edit of the seed with the plate's prompt (in `handoff-pieces.json`): redraw it crisply, line for line,
  adding nothing. No mask.
- **Must:**
  - line up with the seed (his outline overlaps the seed's by 93% or more);
  - keep the same colours, with nothing added (no gear, marks or new muscles);
  - have the ink outline;
  - run his body on to the canvas's edge where the rectangle cuts it, with flat green everywhere else.
- **First:** make all 13 before the pieces painted on them. If a plate is replaced later, the pieces painted on it
  must be redone.

## B. Pieces as worn, painted on the body (762 files)

**What a file holds.** One family of pieces on one base: every piece of the family that shows there, in its place,
and nothing else.
- For example, both pauldrons on the whole figure, as a matching pair; only the right one on the right arm's plates;
  and the five rings of one hand on its close-ups.
- Where a family has more than one of a thing in one place, all of them are drawn: two bracelets on each wrist, two
  anklets on each ankle, two necklaces, three medals, two hair pins.
- The game cuts each file into its pieces by the body's map, so each piece must stay on its own sites.

**Size and background.**
- The file is exactly the base's size, with the piece where it's worn on the base.
- Everything else is perfectly flat green `#00ff00`, or real transparency.
- Don't crop or move it: the file lines up with its base pixel for pixel.

**Which bases.** The list gives every family's files. As a rule:
- **Its region's pages:** the plates or close-ups of the region it fills, on both sides where both show it.
- **The whole figure,** from the front, behind or both, unless its plates hold all of it there. Then the game scales
  those down.
- **Not the whole figure for the 43 smallest families** (studs, small rings, piercings, nail lacquer, small
  tattoos), which would be a few pixels there. The game marks them with a glint.
- **Not the pages where it's only at the dimmed edge** (a shirt's armpits on the arm's page). The game draws it there
  from its other pictures.

**The mask.** `art/equipment/inputs/masks/pieces/<base>/<family>.png`, at the base's size.
- It is fully transparent where to paint and opaque elsewhere (the image tool's convention).
- `art/equipment/inputs/guides/pieces/<base>/<family>.jpg` shows the area on the base, for checking by eye.
- It covers the sites the pieces fill, grown by what the layer needs: ink hugs the skin, while plate stands off it.
- It also covers where the piece reaches beyond its sites: a helm's dome above the scalp, a hauberk's skirt to the
  knees, a cloak's drape, wings and straps.
- It is a guide, not a hard edge: the model treats a mask as a hint, and the check allows a margin round it.

**Pieces that hang behind him:** the cloak, cape, wings, tail, quiver, backpack, back scabbard and war banner.
- On the front pictures the mask covers only the space round his body, plus what's in front (the cloak over his
  shoulders, the straps across his chest).
- Paint only the parts that show past his body; his body stays in front of the rest.
- The game draws those parts behind him and behind his other pieces.

**How: two edits.** The prompts are in `handoff-pieces.json`, in `prompt` and `isolate`.
1. **Paint:** edit the base picture with the mask and the `prompt`. He is now wearing the piece, fitted to him as he's
   drawn, and the rest is unchanged.
2. **Isolate:** edit that result with the `isolate` prompt. Only the piece is kept, with its pixels where they are, and
   everything else becomes flat green. Nothing his body hid is drawn.

Then check the file (below).
- If the piece moved or changed in the second edit, run that edit again.
- If the first edit is wrong, start again.

**One design per family.** The family's first file (★ in the list, `master` in the JSON) sets its design.
- Every other file of the family passes the first file as a second input picture (`refs`), and its prompt asks for the
  same piece.
- **Pieces spanning regions** (a hauberk, a cloak, a robe, a tabard) take the whole figure as their first file.
  - Their plates (↻) are painted on a **seeded base**: the plate with the whole figure's picture of the piece already
    on it, soft. The prompt asks to redraw it crisply, in the same place.
  - `inputs` makes the seeded base once the whole-figure file is in:
    `art/equipment/inputs/bases/pieces/<plate>/<family>.png`.

**The look of every piece.**
- **One design per piece,** in neutral materials. The game recolours plain steel for each era, so steel and iron stay
  neutral grey (shadow `#4a4a4f`, mid `#8a8a90`, light `#cfcfd4`). No blueing, gilding or blackening, unless the
  piece is asked for in another metal.
- **Other materials:**
  - leather `#4a2f22` `#7a5038` `#a77552`;
  - gold and brass `#7a4f12` `#c48a22` `#f2c45a`;
  - wood `#4b3220` `#7b5534` `#a77a4e`;
  - bone, horn and ivory `#8f7f63` `#c9b994` `#ece2c8`.
- **Gems:** clear colourless crystal, white with pale grey facets. The game tints them.
- **Cloth** in its family's dye, which the prompt names. The dyes are madder red `#7a3b3b`, woad blue `#4f5d75`,
  walnut brown `#6d4c3d`, undyed dun `#8a7a5a`, slate `#3c4a5c`, linen `#cfc5b4` and ochre `#a4844a`. All are muted,
  never bright.
- **Tattoos and war paint** (54 families):
  - flat dark blue-black ink `#1b1f2e` on the skin;
  - no shading, and no outline round the design;
  - following the body's curve.

  The game tints the ink. The isolated file is the ink alone.
- **Outline and light:** the same ink outline as the knight's (`#15101f`), as thick as his on that base (the prompt
  gives the width). Three flat tones per material, with light from the top left.
- **Never:**
  - rarity colours (bright green, blue, purple, orange, red, pink or cyan);
  - glows or sparkles;
  - text or letters (the knuckle tattoos are runes);
  - anything green.

**Checks.** Run `python3 tools/equipment_pieces.py check pieces/<base>/<family>`. It checks that:
- the file is the right size and a PNG;
- at least 95% of the drawn pixels are where the piece is worn, meaning in the mask with a margin (90% for the pieces
  that hang behind him);
- armour, cloth and mail cover at least 75% of the sites they fill (jewellery, straps and ink only need to be there);
- there is a dark ink outline round it (except for ink);
- no key green is left in it.

Also look at it with `sheet`. The checker can't see a wrong design, a sixth finger in a glove, or a piece that slid
along the arm.

## C. Held pieces, companions and the sky (13 files)

Each is drawn alone on its own canvas from a prompt, with flat green round it, and placed by the game.

| File | Size (px) | Batch | What | Where the game puts it |
|---|---|---|---|---|
| `sprites/main_hand.png` | 768 × 2048 | 1 | An arming sword | Standing at his right side; beside the right hand's close-up |
| `sprites/great_weapon.png` | 768 × 2048 | 1 | A two-handed greatsword | Standing at his right side (it takes both hands) |
| `sprites/off_hand.png` | 768 × 2048 | 1 | A parrying dagger | Standing at his left side; beside the left hand's close-up |
| `sprites/shield.png` | 1024 × 1536 | 1 | A heater shield, plain, no emblem | Standing at his left side; beside the left hand's close-up |
| `sprites/shoulder_familiar.png` | 1024 × 1536 | 1 | A falcon, seen from the front | Perched on his right shoulder, from the front |
| `sprites/shoulder_familiar_back.png` | 1024 × 1536 | 1 | The same falcon, seen from behind | Perched on his right shoulder, from behind |
| `sprites/focus_orb.png` | 1248 × 1248 | 2 | A crystal orb in a three-clawed cradle | Floating at his left side |
| `sprites/torch.png` | 768 × 2048 | 3 | An everburning torch (the game adds the glow) | Standing at his left side |
| `sprites/familiar.png` | 1248 × 1248 | 3 | A small black cat, sitting | At his feet, on the Beyond page and the whole figure |
| `sprites/steed.png` | 1536 × 1024 | 3 | A destrier in profile, bare but for a halter | Beside him, smaller, on the Beyond page |
| `sprites/comet.png` | 1536 × 1024 | 3 | A small comet with a tail of pale light | In the sky round him |
| `sprites/eclipse.png` | 1248 × 1248 | 3 | A black disc with a pale corona | In the sky above him |
| `sprites/ioun_stone.png` | 1536 × 1024 | 3 | Six orbiting stones on a 3 × 2 grid, one per cell | Orbiting him, one per stone worn |

- **Held pieces** stand upright and point down, with the flat of the blade toward the viewer. They are centred and
  fill most of the canvas's height: 92% for the swords, 70% for the dagger, 90% for the shield. The game stands them
  on the floor beside him, at a height true to his.
- **The falcon's feet** are at the bottom middle of its canvas, and the game sets them on the point of his shoulder.
- **The game sets each one's size relative to him.**

## D. Pieces set in pieces

**D1. Painted on their host (48 files).** These are pieces whose shape follows the piece they're set in:
- a sallet's visor and cheek fittings, and a helm's aventail;
- a boot's toe cap and laces, and a wizard's hat's band;
- a sword's pommel, crossguard and grip wrap, and a shield's boss and rim;
- a familiar's collar and barding;
- a steed's saddle, barding, reins and saddlebags;
- a falcon's jesses and hood.

They're painted with the same two edits as B, on the host's picture.
- **The base** is the host's base with the host's file on it (`inputs` makes
  `art/equipment/inputs/bases/pieces/<base>/<host>/<family>.png`), or the host's sprite.
- **The mask** is where the piece goes on the host.
- **The host's file must be delivered first.** The list orders them after their hosts.
- **The file** is the size of the host's base, with only the set piece in it.

**D2. Drawn on their own (28 files).** These are rigid parts the game places on any host, at the slot's place: crests,
plumes, feathers, spikes, spurs, buckles, clasps, pouches, potions, knives and so on.
- 1024 × 1024, with one part centred and filling about 70%.
- Its fixing point (where it's attached) is at the bottom middle of the drawn shape.
- The crest, the dragon crest and the plume come twice: from the side (for the helm's profiles) and from the front.

**D3. Sheets (5 files).** Small parts the game tints and cuts into cells. Each part sits in one cell of the grid
given, centred, filling about 60% of the cell and clear of its edges.

| File | Size (px) | Grid | Batch | What |
|---|---|---|---|---|
| `sheets/gem.png` | 1536 × 1024 | 3 × 2 | 1 | Six gem cuts in clear crystal: round, oval, square, pear, marquise, cabochon. Every stone set in a piece |
| `sheets/rune.png` | 1536 × 768 | 4 × 2 | 1 | Eight angular runes, solid white. The runes on blades, vambraces, greaves and the rest |
| `sheets/charm.png` | 1248 × 1248 | 2 × 2 | 2 | A crescent moon, a key, a wolf's tooth and a coin. Charms on necklaces, bangles, the belt and the familiar |
| `sheets/heraldry.png` | 1248 × 1248 | 2 × 2 | 2 | A lion, an eagle, a tower and crossed swords, solid white. Painted by the game on shields, tabards, banners and breastplates |
| `sheets/torc_terminal.png` | 1536 × 1024 | 2 × 1 | 2 | A pair of wolf-head torc terminals |

## E. Drawn by the game (no file)

| Family | Pieces | How |
|---|---|---|
| Aura | 1 | A glow round the whole figure in the rarity's colour |
| Constellations | 8 | Stars joined by fine lines in the sky above his head |
| Shadow cloak | 1 | His living shadow: his own outline, dark, offset and wavering |
| Halo, starcrown | 2 | A ring of light, a crown of stars, above his head |
| Scalp tattoo | 1 | A faint pattern in his hair (the ink is under it) |
| Tongue stud | 1 | Behind closed lips: not seen |
| Enchantment | 36 | The host glows in its rarity |
| Paint | 16 | The host's metal is tinted |
| Engraving, embroidery | 9 | Fine lines on the host; a stitched border along its edge |
| Lens tint, blade oil, halo light, aura motes | 7 | A tint, a sheen, a light, motes of light |
| Lining, keepsakes, arrowheads, shield straps | 16 | Inside or behind their host: not seen |
| Horseshoes | 4 | Under the hooves: a glint |
| Lens chain | 1 | A fine chain line from the lens to the ear |

## Rules for every file

- **Format:** PNG, 8-bit sRGB, at exactly the size listed, as RGB on flat green or RGBA with real transparency. No
  JPEG.
  - If the image tool returns another size at the same shape, resize it to the exact size (it's within a few
    percent).
  - Never crop, stretch or move what's drawn.
- **Background:** perfectly flat pure green `#00ff00` from edge to edge (or transparency). No floor, shadow, gradient
  or texture. The plates are the exception: they keep his body running to the canvas's edge where it's cut.
- **Style:** inked cel, as in phase 1 and [art-direction.md](art-direction.md).
  - One clean ink outline (`#15101f`), with thinner inner lines.
  - Three flat tones per material, with light from the top left at 45°.
  - No gradients, texture noise, cast shadows or glow.
- **Never:** text, letters, numbers, watermarks, signatures, frames, rarity colours, glows, magic light or anything
  green.
- **The same knight:** pieces fit him as he's drawn on the base. Nothing about him changes: not his face, hair, braid,
  skin, braies or pose.

## How to make them (for Codex)

1. **Set up.** Use Python 3 with Pillow and numpy, in the repository. The tool reads the mapped art in
   `assets/equipment/` and the content in `art/content.json`; both are committed.
2. **Work batch by batch, in the list's order.**
   - Make the plates first, all 13.
   - Then make the batch's files by number. In each family the ★ file comes first, and a host comes before what's set
     in it.
3. **Make the inputs** for what you're about to paint: `python3 tools/equipment_pieces.py inputs <ids>`.
   - An id is a file's path under `art/equipment/` without `.png`. A trailing `/` takes a whole folder: for example,
     `pieces/torso/`.
   - Run it again after the plates and the whole-figure pictures arrive, because they become bases and seeds.
   - `art/equipment/inputs/` is not committed.
4. **Generate.**
   - Plates, B and D1: use GPT-Image-2's edit, with the base picture, the mask and the references listed.
   - C, D2 and D3: use the prompt alone.
   - Use the highest quality and the file's size, or the tool's size at that shape, then resize.
   - Use the prompts as they are. Add corrections at the end ("the left glove has six fingers; draw five") rather than
     rewriting them.
5. **Make three or four candidates of each ★ file** and keep the best, since it sets the family's design. One or two
   candidates of the others is usually enough.
6. **Check.** `python3 tools/equipment_pieces.py check <ids>` must pass. Then look: `sheet <ids>` writes review sheets
   to `art/equipment/review/pieces/`.
7. **Tick off.** `python3 tools/equipment_pieces.py list` rewrites the full list, showing what's in.
8. **Upload.** Commit the files to their paths on the branch `prototype/progression`, or on a branch of your own (tell
   me its name), one batch at a time or more often.
   - Optionally, add `art/equipment/prompts-pieces.json` with each file's final prompt and corrections, as phase 1 did,
     so that a file can be remade later.

## After delivery

This is what will be done with the files:
1. **Import them** into `assets/equipment/`:
   - key out the green;
   - crop each file to what's drawn, keeping its place on its base;
   - cut family files into their pieces by the body's map;
   - find the plain steel, to give it the era's colour;
   - split the pieces that hang behind him into their behind and front parts.
2. **Map the plates' sites** as the figure's are mapped, so that pointing, lighting and the slot markers work on them.
3. **Draw them on the screen:**
   - each region bare and equipped, in layer order;
   - lit in its own shape when pointed at, and faint as a preview;
   - the held pieces at his sides, the companions with him, and the set pieces on their hosts;
   - the whole figure drawn from the plates where it has no picture of its own.

## Not in this handoff

- The item icons (`art/manifest.json`, from `python3 tools/art.py manifest`).
- The battle lane's knight, which is drawn in code (`HeroRig.cs`).
- A design per era. Each piece has one design, and its era shows in the metal's colour. More ornate designs for the
  late eras could come later.
- Changes to his pose. He stays as phase 1 drew him, with his hands open.
