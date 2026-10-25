# Equipment

The knight gears up from the crown of his head to the tips of his toes, and past them: what floats around him, what
walks beside him, the stars over him. Every piece takes room on his body. Most also keep other room free, and some carry
slots of their own. Which pieces go together is the choice the equipment system is about. Auto-equip makes that choice
by weighing every whole set the rules allow.

The content is in `scripts/Progression/Anatomy.cs` and the search in `scripts/Progression/Loadout.cs`. The screen is
`scripts/View/EquipmentScreen.cs`, with the knight in `PaperDoll` (see "The figure" below). Balance is on hold while the
systems are redesigned (see "Balance" below).

## Words

| Word | Meaning | In code |
|---|---|---|
| Slot | One place a piece can fill: one layer of one site on the body ("the right index finger's band"), or one of a piece's own slots ("the helm's crest"). | `Cell` |
| Site | A part of the body with its layers (the right forearm: ink, wraps, plate, a hidden blade, a sheath). It has an area on each picture of the knight that shows it, and each of its slots has its own point. | `Cell.Site` |
| Piece | A kind of thing that can be worn (a Sallet, a Left Knuckle Ring, a Helm Crest). It holds one item, the best found. | `SlotDef` (the framework's old word "slot") |
| Board | A set of slots: the body is one, and so is each piece's own (the helm's board, a ring's setting). | `Board`, `BoardDef` |
| Host | The worn piece whose board it is. | `Hero.HostOf` |
| Level | The body is level 1, pieces worn on it level 2, pieces set in their boards level 3, and a few go to level 4. | `SlotDef.Level` |

On screen and in these docs, "slot" means a cell. In code, a `SlotDef` is a kind of piece, because the progression
framework was written when every slot held one piece.

## Slots and options

- **A slot is one place, and holds one piece at a time.** Each slot is its own point on the figure.
- **The pieces that can go in a slot are its options.** The head's "Headwear" slot takes a great helm, a sallet, a
  nasal or horned helm, a hood, a wizard hat or a crown, one at a time.
- **Two slots at the same site** are there because they can be filled together: a cap under a helm, ink under a
  breastplate, a ring and a knuckle ring on one finger.
- **Two places that never take pieces together are one slot.** Helm and hood became "Headwear"; brow guard and circlet
  became "Brow band"; mask and face plate became "Mask"; choker and torc became "Neck ring"; nose guard and bridge stud
  became "Bridge"; helix ring and ear cuff became "Helix". The fingers' ink became one slot.
- **A piece that covers another slot** keeps it free: a bracer over the bracelets, a gauntlet over the rings, a great
  helm over the ears and face. That is coverage, not a choice inside one slot. `progsim --anatomy` lists the same-site
  pairs that never go together, so a split slot shows up.
- **Every slot is a choice of its own.** Some piece fills it alone. The anatomy's validation refuses a slot that only a
  bigger piece can fill.

## The body (level 1)

The body has 328 slots on 121 sites in 14 regions, spread so the whole body is covered. With every slot showing, the
figure is dotted from head to toe:

| Region | Slots | Sites |
|---|---|---|
| Head | 45 | scalp, hair, brow, eyes, ears, cheeks, face, nose, mouth, chin, nape, horns, above the head |
| Neck | 9 | neck, throat, the base of the neck |
| Torso | 45 | collarbones, breasts, breastbone, heart, ribs, solar plexus, stomach, navel, flanks, lower belly, the whole torso |
| Back | 25 | shoulder blades, upper spine, middle of the back, sides of the back, small of the back, kidneys, the whole back, tail |
| Waist | 16 | waist, hips, groin, seat |
| Each arm | 24 | shoulder, armpit, upper arm, back of the upper arm, elbow, forearm, wrist |
| Each hand | 23 | back of the hand, palm, grip, the fingers' ink, and each finger's band, knuckle and tip |
| Each leg | 24 | thigh, outside of the thigh, back of the thigh, knee, back of the knee, shin, calf, ankle |
| Each foot | 13 | top of the foot, sole, heel, toenails, five toes |
| Beyond | 20 | aura, six orbits, shadow, companions (a familiar, a steed), the heavens (eight stars, a comet, an eclipse) |

Each site has layers, and a layer is a slot:

| Layer | Worth | Examples |
|---|---|---|
| Skin | 0.35 | tattoos, kohl, nail lacquer, palm sigils |
| Under | 0.5 | shirt, glove, sock, coif, arming cap |
| Mail | 0.7 | mail shirt, mail sleeves, aventail |
| Armour | 1.0 | sallet, cuirass, vambrace, sabaton |
| Over | 0.6 | hood, tabard, cloak, robe |
| Jewellery | 0.45 | rings, earrings, bracelets, anklets, studs, chains |
| Held | 2.5 | the main hand, the off hand, a great weapon, a shield, an orb, a torch |
| Grown | 1.0 | grafted horns, wings, a tail, talons, elven ear tips |
| Beyond | 1.2 | the aura, orbiting stones, the shadow, the familiar, the steed, constellations |
| On a board | 0.35 | a helm's crest, a blade's rune, a ring's stone |

A slot's worth is what filling it is worth next to one plate of armour. A site can change a slot's worth from the
layer's (a finger's band is worth less than a forearm's plate).

## Pieces (level 2)

A piece names the slots it **fills** and the slots it **keeps free**. Two pieces can't be worn together when one fills
a slot the other fills or keeps free. Every rule about what goes with what is one of these shared slots. 720 kinds of
piece exist: 424 on the body, 284 set in boards, 12 at level 4. They come in 314 families, each with its own picture.

The arm and hand, for example:

| Piece | Fills | Keeps free |
|---|---|---|
| Glove | the back of the hand, under | the fingertips (no tip rings, no lacquer) |
| Fingerless Glove | the back of the hand, under | nothing |
| Hand Plate | the back of the hand, armour | every finger's band and knuckle (no rings) |
| Gauntlet | the hand under and armour, and the wrist's plate | rings, knuckle rings, tips, bangles, the palm |
| Knuckleduster | the bands of four fingers (no rings on them) | nothing |
| Ring (per finger) | that finger's band | nothing; it brings a setting with a stone |
| Vambrace / Rerebrace / Couter | the forearm's / upper arm's / elbow's plate | nothing |
| Arm Harness | the upper arm, elbow and forearm plates | nothing (one piece instead of three) |
| Full Arm Harness | shoulder, armpit, upper arm, elbow and forearm plates | nothing |
| Bracelet, bangle stack | the wrist's bangle slots | nothing |

The head:

| Piece | Fills | Keeps free |
|---|---|---|
| Great Helm | headwear, brow band, mask, both cheek guards and the nape guard | hair, ears, the forehead jewel, the eyes, eyebrow rings, the face's rings, studs and gold teeth, cheek gems, horns |
| Sallet | headwear, brow band, both cheek guards and the nape guard | hair, ears, the forehead jewel, horns, but not the face |
| Nasal Helm | headwear, brow band and the nose's bridge | hair, the forehead jewel, horns, a septum ring |
| Horned Helm | as the great helm, and the horns | as the great helm; it brings the horns' board as well as the helm's |
| Hood, Wizard Hat, Crown | headwear | some of the hair; the hood also each ear's helix and tip (the lobes stay free) |
| Brow Guard, Circlet, Diadem | the brow band | nothing (the diadem one hair pin) |
| Cheek Guard, Nape Guard, Nose Guard, Chin Guard | their own guard's slot, as a helm would | a cheek gem, nothing, nothing, the beard rings |
| Spectacles / Monocle | both eyes' / the right eye's lens | nothing; they bring lenses (a tint, a chain, an enchantment) |
| Grafted Horns | the horns | the headwear (no helm, hood or hat) |

Without a helm the head opens up: a hood, a brow guard or a circlet, cheek and nape guards, a monocle, kohl, eyebrow
rings, earrings on each ear's lobe, helix and tip, nose and lip rings, a tongue stud, gold teeth, beard rings, braid
beads. With a great helm, all of that goes, and the helm's own board comes instead.

The torso works the same way, part by part:

| Piece | Fills | Keeps free |
|---|---|---|
| Pectoral Plate, Placard, Rib Guard, Plexus Disc | one breast, the breastbone, one side's ribs, the solar plexus | the rib chain (rib guard), the plexus gem (disc) |
| Breastplate | both pectoral plates, the placard, both rib guards and the plexus disc | the rib chains, gills and the plexus gem |
| Cuirass | the breastplate's slots, the plackart's and the backplate's | as the breastplate and the backplate |
| Brigandine | the cuirass's slots and both side plates | as the cuirass |
| Shirt, Gambeson | the whole torso's under layer (the gambeson also the sleeves) | nothing |
| Mail Shirt, Scale Vest, Hauberk | the whole torso's mail (the hauberk also the arms' and thighs') | nothing |
| Tabard, Surcoat, Robe | the whole torso's over layer (surcoat and robe also the groin's) | nothing |
| Bandolier, Baldric, Chest Harness | the strap across the torso | nothing; bandolier and harness bring loops and pockets |
| Medals, Badge, Brooch, tattoos, studs, chains | their own slot on a breast, the breastbone, a collarbone, the ribs, the navel or the lower belly | nothing |

Feet work the same way. A boot keeps the toes and anklets free, a sandal leaves the toes open for toe rings, and a
sabaton goes over a boot. The fantastical pieces follow the same rules: wings on the back, a tail with rings and a
spike, a third eye, talons and toe talons, fang caps, orbiting stones, a familiar on the shoulder, a steed with its
barding, saddlebags and horseshoes, a halo, and constellations in the heavens.

## Boards (levels 3 and 4)

A piece with slots of its own **brings a board** and says which of its slots it **opens**. There are 66 boards with
294 slots. Some examples:

- **The helm's board:** crest, plume holder, visor, both cheek fittings, aventail, three gem sockets, a crown gem,
  lining, paint, enchantment and engraving. A sallet opens the visor and the cheek fittings. A great helm opens the
  aventail, three brow sockets, a crown socket and engraving.
- **The blade's board:** pommel, crossguard, grip, five runes, two sockets, enchantment, oil and tassel. The main hand
  opens 11 of these 13 slots (three runes), and a great weapon opens all 13.
- **Rings:** a setting with a stone. **Gauntlets:** four knuckle spikes, a cuff socket, a palm rune, paint and an
  enchantment. **Belts:** a buckle, four pouches, four potion loops, three charms and an enchantment.
- **Straps across the chest:** a bandolier's two knife loops and two vial loops, and a chest harness's two pockets and
  buckle.

A few slots go a level deeper: the pommel's stone, the helm crest's jewel, and the keepsakes in each pouch and in the
saddlebags.

**Sharing.** Pieces that can never be worn together may bring the same board. All helms bring the helm's board, and the
main hand and the great weapon bring the blade's. What's set in a board stays there when its host changes. It counts
again as soon as a host that opens its slot is worn, so swapping a sallet for a great helm doesn't throw away the
crest. The anatomy's validation refuses a board shared by two pieces that could be worn together.

**What counts.** A piece counts (`Hero.Active`) when:
- it's worn (`Hero.Worn`);
- it's open (reached by depth, or opened by the prestige perk);
- it holds an item;
- on a board, its host counts and opens every slot the piece fills.

This is settled level by level, so a board's pieces count only through a chain of counting hosts. A piece set but not
counting is shown as "set, waiting" on the screen.

## Auto-equip

When anything changes (a chest brings an upgrade, an ascension resets the run) and auto-equip is on, the knight puts
on the strongest whole set the rules allow. This is `Strategy.Wear`, using `Loadout.Solve`:

1. **Value each piece.** Every piece's item is valued by what it adds to the knight's power, using the stat weights
   (`Strategy.UpdateWeights`, the derivative of the objective for each stat). A piece that's worn is valued by what
   taking it off would lose, and a piece that isn't by what putting it on would add.
2. **Solve from the bottom up.** A host's value is its own plus the best set its boards hold under the slots it opens.
   So a weak helm with a rich board can beat a strong helm with an empty one.
3. **On each board, find the best set that fits.** Pieces that clash are joined in a conflict graph. The best set is
   its maximum-weight independent set, found exactly by branch and bound:
   - the graph is split into connected parts;
   - it branches on the piece with the most conflicts;
   - it bounds each branch by the sum of what's left;
   - each part is memoised by bitset (up to 256 pieces).
   A budget of 400,000 nodes falls back to a greedy set; it has never been reached.
4. **Apply it only if it helps.** The set goes on only if the knight's true power doesn't drop. The weights are then
   updated and a second round runs.

Pieces that are kept but not worn stay kept: a better item for a blocked piece still replaces the old one, ready for
when its set wins.

`progsim --anatomy` checks the search. It matches a brute force on 300 of 300 random problems (dense conflicts, hosts
and their boards), and it takes 0.23 ms over all 720 pieces, at most about 420 nodes.

Wearing or taking something off by hand turns auto-equip off, so the player's choice stays. The body panel's switch
turns it back on, and "Wear the best set" runs the search once.

## The figure

The knight on the screen is the delivered art ([art-handoff-equipment.md](art-handoff-equipment.md)), mapped once and
drawn with what he wears on it.

- **Thirteen pictures.** The figure from the front and from behind, and the close-ups: the head from the right, the
  front and the left, each hand's back and palm, each foot's top and sole. `tools/equipment_art.py import` keys out the
  green and cuts each into `assets/equipment/<view>.png` (at half the delivered size, which holds all the detail the
  art has).
- **The map.** `tools/equipment_map.py` cuts each picture into the body's sites, along lines placed by hand on the art
  and the edges the art draws between hair and skin, and names each part. It writes:
  - `<view>_sites.png`, the site under every pixel, which the screen reads for the mouse, the highlights and the fills;
  - `map.json`: each view's sites (box, outline, whether it's hair), every slot's point on the whole figure and on its
    close-up, each region's page, and what each site covers.

  It checks that every slot has one point on the whole figure and one on its close-up, on its own site, and draws
  review sheets in `shots_map/`. Run it again after a site or slot is added, moved or renamed.
- **Which side.** Each slot shows on one side of the whole figure: the back's own slots, the backs of the limbs (the
  triceps, elbows, calves, heels), the palms, the soles and the hair's slots from behind, the rest from the front.
- **What covers what.** A slot covers its site, and some sites cover others: the mask the eyes, cheeks, nose and mouth;
  the whole torso every part of the chest, belly and back; the left breast its heart; the stomach its navel; the
  fingers' ink each finger; the grip the hand. Cloth, mail, plate and what's worn over them also wrap round a limb (the upper arm's plate covers the
  triceps; the thigh's the hamstring; the foot's the toes, sole and heel); ink and jewellery don't.
- **What's worn shows** (`Coverage`): where pieces overlap, the outermost layer shows (ink, cloth, mail, plate, then
  what's over everything). The figure shader paints each piece's material over its area in three steps of the art's
  own light, so the knight's muscles and folds show through: ink as a tattoo's bands, cloth in its family's colour,
  mail as rings, plate in its era's metal with a glint for the rarer ones, heavy cloth with a hem. Over hair it's
  shaded as a smooth dome instead. Each piece gets an ink line round its area, and plate and heavy cloth a trim inside
  it, drawn as smooth lines at any zoom (`Outlines`).
- **What's under it doesn't show.** A slot doesn't show while a piece keeps it free, or while a piece on a higher
  layer covers its site: the helm hides the cap, the scalp's ink and the hair's pins. A worn piece is one marker over
  what it covers, with a count of the slots under it, and its own slots (a helm's crest, visor and sockets) show on it,
  spread over its area.
- **Close-ups.** The head, the hands and the feet have their own pages: the camera zooms into the figure until the
  close-up takes over. Every other region's page shows the part of the figure it's on, from both sides where both have
  slots (an arm's front and back), with the rest of the body dimmed.

## The screen

`EquipmentScreen` (G, or the dock's Equipment button) has the knight on the left and the slots on the right.

- **The body.** Front and back side by side, wearing everything that counts, every slot a dot (in its rarity when
  something is worn), so the body reads as covered from head to toe. "Hide slots" clears the dots.
  - Pointing at a part of him lights his region and names it, with its slots and pieces worn. Pointing at a dot names
    the slot, why it's empty or what's in it.
  - Clicking a part of him, or turning the wheel over it, zooms into its region's page. Clicking a dot goes straight
    to that slot.
  - The panel lists the regions with what's worn in each and its rarity spread (pointing at one lights it on the
    figure), the auto-equip switch and "Wear the best set".
- **A region's page.** The page grows out of its place on the whole figure (the close-ups out of the head, hand or
  foot, their other views unfolding beside them), and shrinks back into it.
  - Every slot that shows has a marker with its kind's glyph: in a ring for a free slot, dim for one not open yet.
    A worn piece shows its item in its rarity.
  - Markers are pushed apart so they don't overlap, with a line back to their point, and a legend explains them.
  - Pointing at a worn piece, on the figure or at its marker, lights the area it covers in its rarity and what it
    keeps free in red, and names it with its own slots and the slots under it. Pointing at a slot or a bare site
    lights it in gold.
  - The panel lists every slot, site by site, with what fills it or why nothing does (free, under the sallet, kept
    free by the gauntlet, opens at depth 1,200). A worn piece's own slots are listed under it. Pointing at a row lights
    its place on the figure.
  - The wheel zooms closer and dragging moves round; turning it out past the page steps back.
- **A slot.** Choosing a slot, on the figure or in the list, lights it and lists every piece that can fill it. Each has
  its item, whether it's worn or what's in its way, and a Wear or Take-off button that shows the change in power. Its
  tooltip names what the piece would take off. Pointing at one lights on the figure what it would cover and keep free.
- **A piece.** Its item and stats, what it fills and keeps free, its own slots (listed, and shown on it on the
  figure), the slots under it, and its forge, rank, mastery and focus. On the figure its area is lit and the rest
  dimmed; one that isn't worn shows where it would go.

Putting something on flies its icon onto the figure with a burst and the area's flash. Taking something off flies it
away. Esc or a right click steps out one level at a time: a piece set in another, its host; a piece, its region; a
region, the whole body. While the screen is open, a change auto-equip makes after a chest shows at once.

On the main screen, the middle panel shows the small figure from the front, wearing what counts (pointing at it lights
a region; clicking it opens the equipment screen there), the 14 regions with what's worn in each, and the latest
finds. The reveal marks each card WORN, KEPT (for another set) or salvaged.

## Unlocks

Four pieces are open from the start: the main hand, a breastplate, a sallet and a right boot. The rest open by depth,
from depth 3 to depth 12,000 on a quadratic curve (`ContentScale.SlotFirstDepth`, `SlotLastDepth`). They come in an
order mixed by tier:
- the classic pieces first, then the detailed ones, then the exotic;
- a board's pieces only after a host that brings it.

By depth 100, 66 pieces are open; by 1,000, 203; by 12,000, 697. The 23 prestige pieces (the halo, constellations and
other celestial pieces) open with the transcendence tree's Celestial Slots perk.

## Saves

Everything kept per piece (its item, forge, rank, mastery, focus, whether it's worn), the codex and the chests' contents
is saved with the pieces' names, and matched up by name when loaded. The equipment can therefore change between
versions without losing a game: a piece added starts empty, and one taken away is dropped.

## Balance

Each piece's share of the power budget is its **size**: the worth of the slots it fills, plus 0.4 times the worth of
the slots it keeps free on its own board. Content scales every size so the largest set that can be worn at once (420
pieces) adds up to `Params.ReferenceSlots` (149), the budget the progression was tuned for. A piece's stat weight is
its kind's weight times its size times the slow growth along the unlock order. Percentage affixes scale with size too.

The owner has put numerical balance validation and the regression checks on hold while the systems are redesigned:
- `progsim --check`, `--sweep` and `--content-sweep` are not expected to pass;
- CI runs them only when asked.

The game check (`progsim --game`) still checks that the session works: chests, the stage, saves and time away.

## Tools

| Command | What it shows |
|---|---|
| `progsim --anatomy` | Counts by level, kind and region; slots by region; same-site slots that never go together; when pieces open; sizes; the largest set; the search checked against brute force and timed |
| `progsim --anatomy --play=2` | A sensible player's knight after two hours: what's worn by region, and what's kept with what blocks it |
| `progsim --debug-wear` | Why the first chest's pieces do or don't go on (weights, values, the objective) |
| `--demo=screens --hours=2` | Screenshots of the body, each region, a chosen slot, a worn piece pointed at, a piece and one of its slots, and the mouse on the figure: a slot pointed at, the wheel into the head, a piece pointed at, a right click back (`equipment*`) |
| `tools/equipment_art.py check` | The equipment screen's delivered art against its spec ([art-handoff-equipment.md](art-handoff-equipment.md)) |
| `tools/equipment_art.py import` | The checked art into `assets/equipment/`: keyed, each view cut out, the backdrops and the glyph atlas |
| `tools/equipment_map.py` | The map of the art (sites, slot points, pages, what covers what) into `assets/equipment/`, checked, with review sheets in `shots_map/` |
| `tools/equipment_pieces.py spec`, `list` | Phase 2's art: every file to make for the bare regions and the pieces as worn, written to `art/equipment/handoff-pieces.json` and [the full list](art-handoff-equipment-pieces-list.md) ([art-handoff-equipment-pieces.md](art-handoff-equipment-pieces.md)) |
| `tools/equipment_pieces.py inputs`, `check`, `sheet` | What each file is painted on (bases, masks, guides in `art/equipment/inputs/`), the delivered files against the spec, and review sheets |

## Adding a piece

1. In `Anatomy.cs`, add its site's slot if it needs a new one (`Site(...)`, with a layer and maybe a worth). A new
   slot needs a piece that fills it alone.
2. Add the piece with `Piece(...)` or `Sided(...)`: its slots, the slots it keeps free, and the boards it brings
   (`Opens(board, slots...)`). For a piece set in a board, use `Mod(...)` or `SidedMod(...)`.
3. The family comes from its name (`Anatomy.FamilyOf`). For a new family, add its picture to `tools/placeholders.py`
   (`ICONS`), run `python3 tools/placeholders.py --only icons`, `python3 tools/art.py manifest` and
   `godot --headless --path . --import`.
4. A new slot needs a point on the art, and a new site an area: run `python3 tools/art.py manifest`, then add the
   site's cut lines and seed in `tools/equipment_map.py` (and a point for the slot if spreading it over its site
   won't do), run it until it reports no problems, check `shots_map/`, and run `godot --headless --path . --import`.
   On the figure a piece is painted by its layer until its own pictures arrive: give it a design in
   `tools/equipment_pieces.py` (`FAMILIES`, or `BOARD_FAMILIES` for a piece set in another), then run its `spec` and
   `list` to add its files to [the phase-2 list](art-handoff-equipment-pieces-list.md).
5. Run `progsim --anatomy` (validation, exactness, speed) and the screens demo, and look at the region.
