# Equipment

The knight gears up from the crown of his head to the tips of his toes, and past them: what floats around him, what
walks beside him, the stars over him. Every piece takes room on his body. Most also keep other room free, and some carry
slots of their own. Which pieces go together is the choice the equipment system is about. Auto-equip makes that choice
by weighing every whole set the rules allow.

The content is in `scripts/Progression/Anatomy.cs` and the search in `scripts/Progression/Loadout.cs`. The screen is
`scripts/View/EquipmentScreen.cs`, which uses `PaperDoll`, `DollPieces` and `Figure`. Balance is on hold while the
systems are redesigned (see "Balance" below).

## Words

| Word | Meaning | In code |
|---|---|---|
| Slot | One place a piece can fill: one layer of one site on the body ("the right index finger's band"), or one of a piece's own slots ("the helm's crest"). | `Cell` |
| Site | A part of the body with its layers (the right forearm: ink, wraps, plate, a hidden blade, a sheath). Each of its slots has its own point on the figure. | `Cell.Site` |
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

## The screen

`EquipmentScreen` (G, or the dock's Equipment button) has the paper doll on the left and the slots on the right.

The figure and its points are provisional, drawn in code. The final diagram is built in one pass on the art listed in
[art-handoff-equipment.md](art-handoff-equipment.md): regions, slot points, the areas pieces cover, and the transitions.

- **The body.** The knight faces you, with every piece that counts drawn in its place, layer over layer. Pieces set in
  boards are drawn on their hosts: the crest on the helm, runes along the blade, stones in the rings.
  - Every slot on that side shows as a small dot, in its rarity when something is worn, so the body reads as covered.
    "Hide slots" clears them.
  - Hovering a dot names it. Clicking a dot goes straight to that slot; clicking elsewhere on a part zooms into its
    region.
  - "Turn round" shows his back, and dragging and the wheel move the camera.
  - The panel lists the regions with what's worn in each and its rarity spread. It also has the auto-equip switch and
    "Wear the best set".
- **A region.** Clicking a region zooms in. Every slot gets its own marker:
  - a tick for a worn slot;
  - + for a free slot;
  - x for a slot covered by a piece;
  - \- for a slot not open yet.

  Markers are pushed apart so they don't overlap, with a line back to their spot, and a legend explains them. Hovering
  a marker names the slot and what's in it. The panel lists every slot, layer by layer, with what fills it or why
  nothing does.
- **A slot.** Choosing a slot, on the figure or in the list, lists every piece that can fill it. Each has its item,
  whether it's worn or what's in its way, and a Wear or Take-off button that shows the change in power. Its tooltip
  names what the piece would take off.
- **A piece.** Its item and stats, what it fills and keeps free, its forge, rank, mastery and focus, and "Open its
  slots" if it brings a board.
- **A board.** The piece large in the middle with its slots round it. Slots it doesn't open are greyed. Clicking a slot
  lists what can be set there.

Putting something on flies its icon onto the figure (or onto its tile or board slot) with a burst and the site's
flash. Taking something off flies it away. Esc steps out one level at a time.

On the main screen, the middle panel shows a small figure wearing what counts, the 14 regions with what's worn in each,
and the latest finds. The reveal marks each card WORN, KEPT (for another set) or salvaged.

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
| `--demo=screens --hours=2` | Screenshots of the body, each region, a chosen slot, a piece and a board (`equipment*`) |
| `tools/equipment_art.py check` | The equipment screen's delivered art against its spec ([art-handoff-equipment.md](art-handoff-equipment.md)) |

## Adding a piece

1. In `Anatomy.cs`, add its site's slot if it needs a new one (`Site(...)`, with a layer and maybe a worth). A new
   slot needs a piece that fills it alone.
2. Add the piece with `Piece(...)` or `Sided(...)`: its slots, the slots it keeps free, and the boards it brings
   (`Opens(board, slots...)`). For a piece set in a board, use `Mod(...)` or `SidedMod(...)`.
3. The family comes from its name (`Anatomy.FamilyOf`). For a new family, add its picture to `tools/placeholders.py`
   (`ICONS`), run `python3 tools/placeholders.py --only icons`, `python3 tools/art.py manifest` and
   `godot --headless --path . --import`.
4. Draw it on the doll in `DollPieces.cs` if the generic drawing for its layer isn't enough. Give its site a shape or
   anchor in `Figure.cs` if it's new.
5. Run `progsim --anatomy` (validation, exactness, speed) and the screens demo, and look at the region.
