# Equipment pieces delivery - 26 October 2026

This checkpoint supplies 150 requested files: the 13 bare-region plates, all 13 starting-equipment views, and the equipment families listed below. The full 869-file phase-2 checklist is authorized for production and incremental upload in upstream order, with family masters produced before their dependent views. This remains a partial delivery based on commit `e0ede57368aa1bde6e94a6baf67898631053183c`.

| Group | Files | Contents |
|---|---:|---|
| Bare plates | 13 | Neck, torso, back, both waist views, and front/back views of each arm and leg |
| Sallet | 5 | Head front, right and left; whole figure front and back |
| Breastplate | 1 | Torso |
| Main hand | 1 | Arming sword |
| Boot | 6 | Right-foot top and sole; both legs from front and back |
| Cloak | 7 | Whole figure front/back, back plate, and four arm views |
| Bracelet | 4 | Both arms from front and back; plain band and wooden beads |
| Gauntlet | 10 | Whole figure front/back, four arm views, and both hands' backs and palms |
| Cuisse | 4 | Both thighs from front and back |
| Hair pin | 3 | Head front, right and left |
| Pectoral plate | 1 | Paired steel chest plates |
| Greave | 4 | Both lower legs from front and back |
| Kidney dagger | 1 | Paired sheathed daggers on the lower back |
| Tasset | 1 | Paired hip plates on the front waist |
| Armlet | 1 | Coiled gold snake on the right upper arm |
| Plackart | 1 | Steel abdomen plate |
| Brooch | 1 | Plain round brass chest brooch |
| Glove | 6 | Whole figure front/back and both hands' backs and palms |
| Bracer | 4 | Both wrists from front and back |
| Great helm | 5 | Head front, right and left; whole figure front and back |
| Pauldron | 4 | Both shoulders from front and back |
| Trousers | 8 | Whole figure, waist and both legs from front and back |
| Ring | 4 | Five empty gold settings on each hand's back; plain band backs on the palms |
| Sandal | 6 | Whole figure front/back and both feet's tops and soles |
| Hood | 5 | Head front, right and left; whole figure front and back |
| Poleyn | 4 | Both knees from front and back |
| Anklet | 4 | Gold chain and carved wooden beads on both ankles, front and back |
| Couter | 4 | Both elbows from front and back |
| Vambrace | 4 | Both forearms from front and back |
| Amulet | 2 | Neck and torso views of the open steel pendant on a leather cord |
| Tabard | 4 | Whole figure and torso/back close-ups |
| Shirt | 4 | Whole figure and torso/back close-ups |
| Earring | 3 | Head front, right and left |
| Toe ring | 4 | Both feet from top and sole |
| Backplate | 1 | Steel back plate |
| Baldric | 4 | Whole figure and torso/back close-ups |
| Gorget | 5 | Neck, figure back and three head views |
| Off hand | 1 | Dagger sprite |
| Total | 150 | One design per family, with no extra variants |

## Files and review

- [Machine-readable delivery manifest](../art/equipment/pieces-delivery-20261026.json) includes dimensions, selected attempts and SHA-256 hashes.
- [Full generation prompt set](../art/equipment/pieces-prompts-20261026.json) records the built-in image generation method and all attempts for this checkpoint.
- [Validation output](../art/equipment/pieces-validation-20261026.txt) contains the phase-2 checker's results with the seeded-mask correction described below.
- [Updated full checklist](art-handoff-equipment-pieces-list.md) leaves undelivered files unchecked.
- Review sheets: [plates](../art/equipment/review/pieces/plates.png), [sallet](../art/equipment/review/pieces/sallet.png), [breastplate](../art/equipment/review/pieces/breastplate.png), [boots](../art/equipment/review/pieces/boot.png), [sword](../art/equipment/review/pieces/sprites.png), [cloak](../art/equipment/review/pieces/cloak.png), [bracelets](../art/equipment/review/pieces/bracelet.png), [gauntlets](../art/equipment/review/pieces/gauntlet.png).

Production PNGs are at their exact requested paths under `art/equipment/plates/`, `art/equipment/pieces/` and `art/equipment/sprites/`. The review sheets are inspection aids, not runtime textures.

New review sheets: [cuisses](../art/equipment/review/pieces/cuisse.png), [hair pins](../art/equipment/review/pieces/hair_pin.png), [pectoral plates](../art/equipment/review/pieces/pectoral_plate.png), [greaves](../art/equipment/review/pieces/greave.png), [kidney daggers](../art/equipment/review/pieces/kidney_dagger.png), [tassets](../art/equipment/review/pieces/tasset.png), [armlet](../art/equipment/review/pieces/armlet.png), [plackart](../art/equipment/review/pieces/plackart.png), and [brooch](../art/equipment/review/pieces/brooch.png).

This checkpoint also adds [gloves](../art/equipment/review/pieces/glove.png), [bracers](../art/equipment/review/pieces/bracer.png), [great helms](../art/equipment/review/pieces/great_helm.png), and [pauldrons](../art/equipment/review/pieces/pauldron.png).

The latest families are [trousers](../art/equipment/review/pieces/trousers.png), [rings](../art/equipment/review/pieces/ring.png), [sandals](../art/equipment/review/pieces/sandal.png), [hoods](../art/equipment/review/pieces/hood.png), [poleyns](../art/equipment/review/pieces/poleyn.png), [anklets](../art/equipment/review/pieces/anklet.png), [couters](../art/equipment/review/pieces/couter.png), [vambraces](../art/equipment/review/pieces/vambrace.png), and [amulets](../art/equipment/review/pieces/amulet.png).

## Production and verification

The latest review sheets are [tabards](../art/equipment/review/pieces/tabard.png), [shirts](../art/equipment/review/pieces/shirt.png), [earrings](../art/equipment/review/pieces/earring.png), [toe rings](../art/equipment/review/pieces/toe_ring.png), [backplate](../art/equipment/review/pieces/backplate.png), [baldrics](../art/equipment/review/pieces/baldric.png), [gorgets](../art/equipment/review/pieces/gorget.png), and the updated [held sprites](../art/equipment/review/pieces/sprites.png).

The images were made with the built-in `image_gen` tool using the upstream seeds, bases, placement guides and family references. Its exact model identifier is not exposed; no separate API or CLI model was selected. Paint, isolation and correction stages are recorded in the prompt set. The top and sole boot views already contained only the finished boot, so a second removal edit was unnecessary. Some seeded region images already wear the delivered whole-figure piece; those were restored and isolated in one edit, recorded as `restore_isolate`.

Each selected result was exported as an 8-bit RGB PNG with an sRGB profile. The small difference between generated and specified dimensions was normalized across the whole canvas, without cropping, recentering or moving individual parts. Pixels already classified as key green by the upstream importer were normalized to `#00ff00`. Foreground colours were not quantized or otherwise repainted by scripts. Generated shading has minor tonal variation and is not a strict three-colour indexed palette.

The phase-2 checker reports:

```text
150 of 150 delivered files pass; 719 of 869 not delivered yet
```

The unchanged phase-1 checker also reports `10 of 10 pass`. Review sheets were inspected over the registered bases. The front arm gauntlet cuffs were shortened to fit their permitted areas; finger placement and the intentional crop at the bottom edge were retained.

The rear thigh plates were extended over the shorts hems to cover their specified sites, then their upper edges were aligned between views. The sheathed back daggers were reduced to fit their permitted area. The armlet's green fringe was cleaned. Hair-pin occlusion and the cropped opposite leg armor are intentional; do not complete the hidden or off-canvas parts.

The glove and bracer cuffs were shortened to their wrist areas. Pauldron silhouettes were fitted to the shoulder masks while retaining three lames per view. The great helm covers the crown hair and ears; its close-up eye openings are keyed green so the original eyes remain visible. The small whole-figure eye slits are dark. Its right cheek has six breathing holes; the left cheek and rear are unperforated. Hair and braid visible below or behind the shell remain part of the base.

Trousers retain the whole-figure registration in all six seeded region views. The hood crown and lower opening were fitted to the head; the rear cowl stays within its narrower permitted area. Rings expose empty settings on the hand backs and plain bands on the palms. Sandal overlays retain open toes and keyed strap gaps; the sole views use separate forefoot and heel pads inside their specified areas. Vambrace upper rims were shortened to the forearm guides. The amulet has a keyed open center, ready for a separately supplied insert. Anklet links use a heavier outline in the left-side views to keep the small recesses clean; link detail varies slightly between views.

The phase-2 `areas()` function received a geometry correction. Cropped region bases lose off-canvas endpoints of reach primitives, so a calf-length cloak's mask stopped at the shoulder or waist despite the seeded artwork continuing downward. The correction projects the full-figure specification mask using the same coordinate transform as the artwork, then unions it with the original region mask. It uses the specification mask, not the generated asset silhouette. Acceptance thresholds and the 869-file specification are unchanged.

Before this correction, only 32.0-70.3% of the affected cloak seeds lay inside their own region masks. The corrected masks contain 99.7-100% of those same seeds. Two regression tests cover the five affected region crop edges and ensure the front drape still excludes the visible arm. Both pass.

Reproduce the checks from the repository root:

```sh
python3 tools/equipment_pieces.py check
python3 tools/equipment_art.py check
python3 tools/equipment_pieces.py sheet
python3 -m unittest discover -s tools -p test_equipment_pieces.py
```

Tabard, shirt and baldric close-ups retain the whole-figure placement. Their restoration and isolation were separated into two edits when a combined edit enlarged the garment. Rear cloth overlays include a keyed opening for the braid and its tail; minor variation remains along the fine hair edge. Toe rings are separate bands on all five toes of each foot. Gorgets preserve the visible beard and hair, and the backplate leaves the braid in front.

## Remaining work and integration

Batch 1 has 127 files remaining; batches 2 and 3 have 299 and 293 respectively. The next requested files begin with spaulders, a shield, circlets, rerebraces, monocles, knuckle rings, eyepatches, tattoos and ear cuffs. Matching views listed later in batch 1 are completed with their family dependencies. No undelivered file is represented as finished.

Keep the plate coordinates unchanged. Regenerate inputs after adding the plates and after delivering a whole-figure master used to seed region views:

```sh
python3 tools/equipment_pieces.py inputs <asset-id-or-prefix>
```

Import, material recolouring, slot mapping and runtime rendering remain the integration work described by the upstream handoff. This checkpoint contains art, delivery records, and the tested mask-generation correction. Use the original full-body and close-up coordinates; do not trim the green canvas around overlays.
