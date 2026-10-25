# Art handoff: the equipment screen (phase 1)

These are the pictures the full-body equipment screen needs before its diagram can be built properly. They are to be
made with GPT-Image-2 (through Codex) and uploaded to the paths below. Once they're in, the figure's regions and every
slot are mapped onto them in one pass, and then the screen's flow is polished. The same list, with the full prompt for
each file, is in [`art/equipment/handoff.json`](../art/equipment/handoff.json).

The style rules are in [art-direction.md](art-direction.md). What the slots are and how they work is in
[equipment.md](equipment.md).

## The checklist

Upload each file to exactly this path, as a PNG.

| Done | # | Upload to | Size (px) | What it is | What it's for |
|---|---|---|---|---|---|
| [ ] | 1 | `art/equipment/figure_front.png` | 2160 × 3840 | The knight, bare, from the front | The whole-body view, every zoomed region except the close-ups, the main screen's small figure. Every region and slot is mapped on it. |
| [ ] | 2 | `art/equipment/figure_back.png` | 2160 × 3840 | The same knight from behind | "Turn round": the back, the back of the head, the backs of the limbs. |
| [ ] | 3 | `art/equipment/head_page.png` | 3520 × 2352 | The head: right profile, front, left profile | The head's close-up: face, ears and hair slots. |
| [ ] | 4 | `art/equipment/hand_right_page.png` | 3520 × 2352 | The right hand: back and palm | The hand's close-up: rings, knuckle rings, fingertips, glove, palm. |
| [ ] | 5 | `art/equipment/hand_left_page.png` | 3520 × 2352 | The left hand: back and palm | As above, for the left hand. |
| [ ] | 6 | `art/equipment/foot_right_page.png` | 3520 × 2352 | The right foot: top and sole | The foot's close-up: toe rings, toenails, sole. |
| [ ] | 7 | `art/equipment/foot_left_page.png` | 3520 × 2352 | The left foot: top and sole | As above, for the left foot. |
| [ ] | 8 | `art/equipment/stage_backdrop.png` | 2560 × 1920 | A dim armourer's fitting room | Behind the figure on the equipment screen. |
| [ ] | 9 | `art/equipment/board_backdrop.png` | 2560 × 1920 | A workbench seen from above | Behind a piece and its own slots (a helm's crest, visor and sockets). |
| [ ] | 10 | `art/equipment/slot_glyphs.png` | 2560 × 1024 | Ten white glyphs on a 5 × 2 grid | Inside the slot markers on the figure, one glyph for each kind of slot. |

Before uploading, run `python3 tools/equipment_art.py check` in the repository. It checks every file against this
spec and must say "10 of 10 pass". `python3 tools/equipment_art.py sheet` draws `art/equipment/review.png` to look at.

## How these pictures will be used

This shows why the constraints below matter.

- **One figure, mapped once.** The front and back figures are the base of the whole screen. Each part of the body gets
  a region drawn on the figure, and each of the 328 body slots gets a point. With every point showing, the body should
  look densely covered from head to toe. Everything is placed by pixel position on these images, so their framing must
  be exact and must never change between versions.
- **Zooming.** Clicking a part of the body zooms the camera into the same image. The figure has to stay sharp up to
  about 2.5× (torso, arms, legs), which the 3840 px height allows. The head, hands and feet zoom much further. For
  those, the camera ends on a close-up page (3 to 7), which fades in over the zoomed figure.
- **Equipping.** A worn piece is drawn on the figure, and hovering over it lights up the area it covers. The slots
  under it disappear and its own slots (a helm's crest, visor and gem sockets) appear on it. The piece pictures come
  in phase 2, painted onto these same figures. So the figure must be bare, neutral and clean, with every body part
  readable on its own.
- **Code draws everything that means something:** rarity colours, frames, highlights, glows, numbers, slot markers and
  motion. The art stays neutral and still.

## Rules for every file

- **Format:** PNG, 8-bit, sRGB, at exactly the size listed. No JPEG and no resizing after generation, except to reach
  the exact size.
- **Background (1 to 7 and 10):** either real transparency, or perfectly flat pure green `#00ff00` from edge to edge.
  The importer keys the green out. No floor, no ground shadow, no vignette, no texture and no gradient behind the
  subject. The backdrops (8 and 9) are opaque paintings.
- **Never:** text, letters, watermarks, signatures, frames, borders, UI, rarity colours (no green, blue, purple, orange
  or gold glows), gear, jewellery, tattoos, scars or blood on the knight.
- **Style:** inked cel shading as in [art-direction.md](art-direction.md). There is one clean outline in ink `#15101f`,
  with thinner inner lines in the same ink. Fills are flat, with exactly three tone steps per material, and light
  comes from the top left at 45°. No gradients, texture noise, cast shadows or glow.
- **Line weight:** about 12 px for the outer outline on the full figures and 10 px on the close-up pages. Inner lines
  are about 5 px. The line weight should be the same everywhere on an image.
- **Same knight everywhere:** files 2 to 7 are generated with `figure_front.png` as a reference image, so the face,
  skin, proportions and line style match exactly.

## The knight

He is a human knight-at-arms in his early thirties, shown bare so that every piece of equipment can later be painted
onto him.

- **Build:** athletic and strong but not bulky. He is about **seven heads tall**, with realistic heroic proportions.
  This is a deliberate exception to the four-heads-tall knight on the battle lane: the diagram needs every part of the
  body large enough to hold its slots.
- **Skin:** warm medium tone. Shadow `#9c5f48`, mid `#d08b6a`, light `#f0b693`.
- **Hair and face:** dark brown hair, pulled back into one braid that hangs behind the head, so both ears are fully
  visible. A short full beard. A calm, neutral expression, with the eyes open and looking straight at the viewer.
- **Clothing:** only plain undyed linen braies (short drawers ending at mid-thigh). Shadow `#8f8270`, mid `#b9ad9b`,
  light `#ddd3c4`.
- **Nothing else:** no armour, shirt, shoes, belt, weapon, jewellery, piercings, tattoos or scars.

## 1. `figure_front.png`: the knight from the front

- **Canvas:** 2160 × 3840 px.
- **Framing:**
  - The figure is centred on x = 1080, within ±48 px.
  - The top of the hair is at y = 380 and the soles at y = 3600, each within ±48 px. That makes him about 3220 px
    tall.
  - Nothing is cut off. The figure stays inside x 130 to 2030.
  - The empty band above the head is space for the halo and the stars.
- **View:** orthographic, straight on, with no perspective and no tilt.
- **Pose:** perfectly symmetric, so the left half mirrors the right.
  - Standing straight, feet shoulder-width apart and turned very slightly outward, with all ten toes visible.
  - Arms hanging straight, about 25° away from the body, so no part of an arm touches the torso or the hips.
  - Hands open and flat, with all five fingers spread apart and separated. The **backs of the hands face the
    viewer** (palms turned backward), with every fingernail visible.
- **Must read clearly as flat shapes, because slots will sit on each of them:**
  - the hair and braid, brow, eyes, ears, cheeks, nose, mouth and beard;
  - the neck and throat, and the base of the neck;
  - collarbones, pectorals, the breastbone line, the ribs at both sides, the solar plexus, the stomach muscles, the
    navel and the flanks;
  - the waist, hip bones and groin (in the braies);
  - shoulders, armpits, upper arms, forearms and wrists;
  - the backs of the hands, the fingers with their knuckles and nails;
  - thighs, the outsides of the thighs, kneecaps, shins and ankle bones;
  - the tops of the feet and the toes with their nails.
- **Checked automatically:** size, background, framing, symmetry (the mirror overlaps ≥ 86%), a dark outline round the
  silhouette, and no green spill.
- **Check by eye:**
  - The face is neutral, with the eyes looking at the viewer.
  - All 10 fingers and all 10 toes are separate and countable.
  - The ears are fully visible.
  - Nothing is worn but the braies.

## 2. `figure_back.png`: the same knight from behind

- **Canvas and framing:** the same as the front: the same canvas, centre line, height and feet positions. Mirrored
  left to right, his silhouette must line up with the front's. The checker requires an overlap of 85% or more.
- **Generate it from the front:** pass `figure_front.png` as the reference image. Ask for the same man, the same pose
  and the same framing, seen from directly behind.
- **Must read clearly:**
  - the back of the head, the braid and the nape;
  - both shoulder blades, the spine, the sides of the back, the small of the back and the kidney area;
  - the seat (in the braies);
  - the backs of the upper arms and the elbows;
  - the palms, which face the viewer from behind;
  - the backs of the thighs, the backs of the knees, the calves and the heels.

## 3. `head_page.png`: the head close-up

- **Canvas:** 3520 × 2352 px.
- **Views:** three views at the same scale, each centred in its own third, with empty space between them (the checker
  wants each gap clear). From left to right: his **right profile**, the **front**, his **left profile**.
- **Extent:** each view runs from the top of the hair to the base of the neck, ending in a clean straight cut. Each
  head is about 1800 px tall.
- **Must read clearly:**
  - the ears in full in both profiles, with lobe, rim and upper tip clearly separate;
  - the braid;
  - the eyebrows, the eyes, the nose with both nostrils, the closed lips and the beard.

## 4–5. `hand_right_page.png`, `hand_left_page.png`: the hand close-ups

- **Canvas:** 3520 × 2352 px.
- **Views:** two views at the same scale, each centred in its half: on the left, the **back of the hand**; on the
  right, the **palm**.
- **Pose:**
  - The hand is open and flat, with the fingers spread apart and pointing straight up.
  - On the right hand, the thumb is on the left in the back view and on the right in the palm view. The left hand is
    the mirror image.
  - The wrist and the lower part of the forearm are included, ending in a clean straight cut near the bottom edge.
  - Each hand is about 1900 px tall, from the middle fingertip to the cut.
- **Must read clearly:**
  - every knuckle and finger joint, where rings and knuckle rings sit;
  - every fingernail;
  - the back of the hand, where gloves, plates and jewels sit;
  - the palm, for sigils and crystals.
- **Generate the left from the right:** use `hand_right_page.png` as a second reference, so the two pages match.

## 6–7. `foot_right_page.png`, `foot_left_page.png`: the foot close-ups

- **Canvas:** 3520 × 2352 px.
- **Views:** two views at the same scale, each centred in its half: on the left, the **top of the foot** seen from
  directly above; on the right, the **sole** seen from directly below.
- **Pose:**
  - The toes point straight up the canvas and are spread slightly apart.
  - On the right foot, the big toe is on the left in the top view and on the right in the sole view. The left foot is
    the mirror image.
  - The ankle is included, ending in a clean straight cut.
- **Must read clearly:** every toe and toenail (toe rings and toenail paint go there), the top of the foot and the
  sole.

## 8. `stage_backdrop.png`: behind the figure

- **Canvas:** 2560 × 1920 px (the stage is 4:3), opaque.
- **Scene:** the inside of a dim armourer's fitting room at night, seen straight on.
  - Stone walls, and a tall arched window with cold moonlight on the far left.
  - Armour stands, weapon racks and banners only as soft shapes, far back at the sides.
  - Warm candlelight from the top left.
- **Treatment:** painterly and soft-edged, as for the lane backdrops.
- **Limits** (the checker measures them):
  - saturation of 40% or less;
  - dark overall, with a mean value of 36% or less and the brightest 5% no brighter than 75%;
  - a calm, almost empty middle, where the figure stands, with low contrast;
  - a soft vignette.
- **Must not contain:** a person, a mannequin or a dais in the middle. The figure is drawn on top and zooms while the
  room stays still, so nothing in the room may look like it belongs under his feet.

## 9. `board_backdrop.png`: behind a piece's own slots

- **Canvas:** 2560 × 1920 px, opaque.
- **Scene:** an armourer's workbench seen from directly above.
  - A dark, worn leather work mat fills the middle.
  - A few tools (pliers, a small hammer, a jeweller's loupe, rivets, a whetstone) and scraps of leather lie only at
    the edges.
  - Warm lamplight comes from the top left.
- **Limits:** the same as the stage backdrop. The middle stays empty, because a piece and its slots are drawn on top
  of it.

## 10. `slot_glyphs.png`: one glyph for each kind of slot

- **Canvas:** 2560 × 1024 px, laid out as an invisible grid of 5 columns and 2 rows of 512 px cells.
- **Each glyph:** a solid white silhouette with no outline, shading or colour. It is centred in its cell, fills about
  60% of it, and stays clear of the cell's edges. It must still read when shrunk to 16 px. Code tints it.
- **Glyphs, in reading order:**

| Cell | Kind of slot | Glyph |
|---|---|---|
| 1 | Ink on the skin (tattoos, sigils, kohl) | a drop of ink with a small swirl inside |
| 2 | Worn under (shirts, gloves, socks) | a quilted, padded square with stitched diamond lines |
| 3 | Mail | four interlinked rings |
| 4 | Plate | a curved plate with two rivets |
| 5 | Worn over (cloaks, tabards, hoods) | a hanging cloak with a clasp at the top |
| 6 | Jewellery | a ring with a cut gemstone |
| 7 | Held (weapons, shields, an orb) | a sword, point up, slightly diagonal |
| 8 | Grown (horns, wings, a tail, gills) | a curved horn |
| 9 | Around the body (aura, orbiting stones, the heavens) | a four-pointed star inside a thin orbit ring |
| 10 | A piece's own slot (gems, runes, fittings) | a round socket holding a small gem, with three prongs |

- **Generate the sheet in one image,** so the ten glyphs share one style.

## Generating with GPT-Image-2 (for Codex)

1. **Settings:**
   - Use custom sizes. Each edge must be a multiple of 16, at most 3840 px, and no more than 8,294,400 px in total.
     Every size here fits.
   - Use the highest quality.
   - Ask for a transparent background only where the API accepts it for this model; it is in preview. Otherwise, use
     the green background the prompts ask for.
2. **Prompts:** the full prompt for each file is in `art/equipment/handoff.json`, under `assets[].prompt`. Use it as
   it is. Add corrections at the end ("the left hand has six fingers; draw five") rather than rewriting it.
3. **References:**
   - Generate `figure_front.png` first.
   - Generate every other figure file as an edit or with image inputs, passing the files listed in its `refs`.
     GPT-Image-2 always reads image inputs at high fidelity; don't send `input_fidelity`.
   - Masks are not needed in this phase. With this model a mask is only a hint, not a hard edge.
4. **Pick the best:**
   - Make about four candidates of each file and keep the one that passes `python3 tools/equipment_art.py check`
     with the fewest problems by eye.
   - The front figure is worth the most attempts, because everything else follows it.
   - If the framing is a little off, move or scale the figure on the canvas to hit the framing numbers. Don't
     regenerate just for that.
5. **Upload:**
   - Commit the ten files to `art/equipment/` on the branch `prototype/progression`, or on a branch of your own and
     tell me its name.
   - Optionally, add `art/equipment/prompts.md` with the final prompt and any corrections for each file, so a file
     can be remade later.

## What happens after delivery

1. I run the checker and look at every file. Anything that won't map cleanly comes back with a note.
2. I import the files into `assets/equipment/`, keying out the green.
3. In one pass, I map regions and slots onto the delivered art:
   - each body site's region and each slot's point, on the front and back figures and the close-up pages;
   - the areas that pieces cover.
4. I rebuild the screen on the art:
   - hovering a worn piece lights up its area;
   - the slots under it disappear and its own slots appear on it;
   - the zoom moves from the figure into the close-up pages;
   - the screen turns round, and every view connects smoothly.
5. Phase 2 follows: the pictures of the pieces themselves on the figure.

## Phase 2, for planning: the worn pieces on the figure

This comes after the mapping. I will hand over a second list with a mask for each piece, cut from the mapped regions.

- **What:** a picture of each kind of piece as it's worn, painted onto the figure or a close-up page. For example, the
  great helm on the head, or rings on each finger.
- **Method:**
  - Each overlay is made by editing the matching figure or page inside the mask I provide.
  - The importer keeps only what changed inside the mask, so every overlay lines up with the figure exactly.
- **Size:** there are 314 families of piece. About 150 of them are big enough to need a picture on the figure (helms,
  breastplates, cloaks, pauldrons, gauntlets, boots, weapons, wings and so on), mostly from the front, and some from
  behind or on a close-up page.
- **Stays in code:** rarity colours, enchantment glows, gem sparkle, paint tints, and the smallest pieces (studs, the
  thinnest rings).

## Not in this handoff

- The item icons. The full list, with prompts, is in `art/manifest.json`, built by `python3 tools/art.py manifest`.
- The battle lane, the monsters and the chests.

## The body's sites, by region

These are the places on the body the figures must show clearly, from `art/content.json`. Each site holds one or more
slots: 328 in all.

| Region | Slots | Seen from the front | Seen from behind |
|---|---|---|---|
| Head | 45 | Scalp, hair, brow, eyes, ears, cheeks, face, nose, mouth, chin, horns, above the head | Nape |
| Neck | 9 | Neck, throat, base of the neck | |
| Torso | 45 | Collarbones, breasts, breastbone, heart, ribs, solar plexus, stomach, navel, flanks, lower belly, the whole torso | |
| Back | 25 | | Shoulder blades, upper spine, middle of the back, sides of the back, small of the back, kidneys, the whole back, tail |
| Waist | 16 | Waist, hips, groin | Seat |
| Each arm | 24 | Shoulder, armpit, upper arm, forearm, wrist | Back of the upper arm, elbow |
| Each hand | 23 | Back of the hand, palm, grip, fingers (ink), and each finger's band, knuckle and tip | |
| Each leg | 24 | Thigh, outside of the thigh, knee, shin, ankle | Back of the thigh, back of the knee, calf |
| Each foot | 13 | Top of the foot, sole, toenails, and each toe | Heel |
| Beyond | 20 | Aura, the six orbits, shadow, at your side (familiar, steed), the heavens | |
