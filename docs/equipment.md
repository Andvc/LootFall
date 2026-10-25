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
| Site | A spot on the body with its layers (the right forearm: ink, sleeve, mail, plate, bracer). Its slots share a marker on the figure. | `Cell.Site` |
| Piece | A kind of thing that can be worn (a Sallet, a Left Knuckle Ring, a Helm Crest). It holds one item, the best found. | `SlotDef` (the framework's old word "slot") |
| Board | A set of slots: the body is one, and so is each piece's own (the helm's board, a ring's setting). | `Board`, `BoardDef` |
| Host | The worn piece whose board it is. | `Hero.HostOf` |
| Level | The body is level 1, pieces worn on it level 2, pieces set in their boards level 3, and a few go to level 4. | `SlotDef.Level` |

On screen and in these docs, "slot" means a cell. In code, a `SlotDef` is a kind of piece, because the progression
framework was written when every slot held one piece.

## The body (level 1)

The body has 258 slots on 14 regions:

| Region | Sites |
|---|---|
| Head | scalp, hair, brow, eyes, ears, cheeks, face, nose, mouth, chin, nape, horns, halo |
| Neck | neck |
| Torso | chest, heart, belly |
| Back | back, tail |
| Waist | waist, hips, groin |
| Arms (each side) | shoulder, armpit, upper arm, elbow, forearm, wrist |
| Hands (each side) | back of the hand, palm, grip, and five fingers each with ink, band, knuckle and tip |
| Legs (each side) | thigh, knee, shin, ankle |
| Feet (each side) | top of the foot, sole, toenails, five toes |
| Beyond | aura, six orbits, shadow, companions (a familiar, a steed), the heavens (eight stars, a comet, an eclipse) |

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
a slot the other fills or keeps free. Every rule about what goes with what is one of these shared slots. 586 kinds of
piece exist: 297 on the body, 277 set in boards, 12 at level 4. They come in 238 families, each with its own picture.

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
| Great Helm | scalp, brow, face, both cheeks and nape (armour) | hair, ears, the circlet, the eyes, the face's rings, studs and gold teeth, a mask, horns, a hood or hat |
| Sallet | scalp, brow, cheeks and nape (armour) | hair, ears, the circlet, horns, a hood or hat, but not the face |
| Nasal Helm | scalp, brow and nose (armour) | hair, the circlet, horns, a septum ring, a hood or hat |
| Horned Helm | as the great helm, and the horns | as the great helm; it brings the horns' board as well as the helm's |
| Hood | the scalp's over layer | hair, and each ear's helix, cuff and tip (the lobes stay free) |
| Circlet / Diadem | the brow's jewellery | nothing / one hair pin |
| Spectacles / Monocle | both eyes' / the right eye's jewellery | nothing; they bring lenses (a tint, a chain, an enchantment) |
| Grafted Horns | the horns | the scalp's armour and over (no helm, no hat) |

Without a helm the head opens up: a hood, a circlet, a monocle, kohl, earrings on each ear's lobe, helix, cuff and tip,
nose and lip rings, a tongue stud, gold teeth, beard rings, braid beads. With a great helm, all of that goes, and the
helm's own board comes instead.

Feet work the same way. A boot keeps the toes and anklets free, a sandal leaves the toes open for toe rings, and a
sabaton goes over a boot. The fantastical pieces follow the same rules: wings on the back, a tail with rings and a
spike, a third eye, talons and toe talons, fang caps, orbiting stones, a familiar on the shoulder, a steed with its
barding, saddlebags and horseshoes, a halo, and constellations in the heavens.

## Boards (levels 3 and 4)

A piece with slots of its own **brings a board** and says which of its slots it **opens**. There are 64 boards with
287 slots. Some examples:

- **The helm's board:** crest, plume holder, visor, both cheek fittings, aventail, three gem sockets, a crown gem,
  lining, paint, enchantment and engraving. A sallet opens the visor and the cheek fittings. A great helm opens the
  aventail, three brow sockets, a crown socket and engraving.
- **The blade's board:** pommel, crossguard, grip, five runes, two sockets, enchantment, oil and tassel. The main hand
  opens 11 of these 13 slots (three runes), and a great weapon opens all 13.
- **Rings:** a setting with a stone. **Gauntlets:** four knuckle spikes, a cuff socket, a palm rune, paint and an
  enchantment. **Belts:** a buckle, four pouches, four potion loops, three charms and an enchantment.

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
and their boards), and it takes 0.17 ms over all 586 pieces, at most about 300 nodes.

Wearing or taking something off by hand turns auto-equip off, so the player's choice stays. The body panel's switch
turns it back on, and "Wear the best set" runs the search once.

## The screen

`EquipmentScreen` (G, or the dock's Equipment button) has the paper doll on the left and the slots on the right.

- **The body.** The knight faces you, with every piece that counts drawn in its place, layer over layer. Pieces set in
  boards are drawn on their hosts: the crest on the helm, runes along the blade, stones in the rings. "Turn round"
  shows his back, and dragging and the wheel move the camera. The panel lists the regions with what's worn in each and
  its rarity spread. It also has the auto-equip switch and "Wear the best set".
- **A region.** Clicking a region zooms in. Every site gets a marker:
  - a tick for a worn slot;
  - + for a free slot;
  - x for a slot kept free by a piece;
  - \- for a slot not open yet;
  - "2/5" for a site with five slots, two of them filled.

  Markers are pushed apart so they don't overlap, with a line back to their spot, and a legend explains them. The
  panel lists every slot, layer by layer, with what fills it or why nothing does.
- **A slot.** Choosing a slot, on the figure or in the list, fans its site's slots out round the marker as tiles.
  Each tile shows what's worn there or why it's empty, so every slot can be picked on the figure. The panel then lists
  every piece that can fill the slot. Each has its item, whether it's worn or what's in its way, and a Wear or
  Take-off button that shows the change in power. Its tooltip names what the piece would take off.
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

By depth 100, 54 pieces are open; by 1,000, 165; by 12,000, 563. The 23 prestige pieces (the halo, constellations and
other celestial pieces) open with the transcendence tree's Celestial Slots perk.

## Balance

Each piece's share of the power budget is its **size**: the worth of the slots it fills, plus 0.4 times the worth of
the slots it keeps free on its own board. Content scales every size so the largest set that can be worn at once (330
pieces) adds up to `Params.ReferenceSlots` (149), the budget the progression was tuned for. A piece's stat weight is
its kind's weight times its size times the slow growth along the unlock order. Percentage affixes scale with size too.

The owner has put numerical balance validation and the regression checks on hold while the systems are redesigned:
- `progsim --check`, `--sweep` and `--content-sweep` are not expected to pass;
- CI runs them only when asked.

The game check (`progsim --game`) still checks that the session works: chests, the stage, saves and time away.

## Tools

| Command | What it shows |
|---|---|
| `progsim --anatomy` | Counts by level, kind and region; when pieces open; sizes; the largest set; the search checked against brute force and timed |
| `progsim --anatomy --play=2` | A sensible player's knight after two hours: what's worn by region, and what's kept with what blocks it |
| `progsim --debug-wear` | Why the first chest's pieces do or don't go on (weights, values, the objective) |
| `--demo=screens --hours=2` | Screenshots of the body, each region, a chosen slot, a piece and a board (`equipment*`) |

## Adding a piece

1. In `Anatomy.cs`, add its site's slot if it needs a new one (`Site(...)`, with a layer and maybe a worth).
2. Add the piece with `Piece(...)` or `Sided(...)`: its slots, the slots it keeps free, and the boards it brings
   (`Opens(board, slots...)`). For a piece set in a board, use `Mod(...)` or `SidedMod(...)`.
3. The family comes from its name (`Anatomy.FamilyOf`). For a new family, add its picture to `tools/placeholders.py`
   (`ICONS`), run `python3 tools/placeholders.py --only icons`, `python3 tools/art.py manifest` and
   `godot --headless --path . --import`.
4. Draw it on the doll in `DollPieces.cs` if the generic drawing for its layer isn't enough. Give its site a shape or
   anchor in `Figure.cs` if it's new.
5. Run `progsim --anatomy` (validation, exactness, speed) and the screens demo, and look at the region.
