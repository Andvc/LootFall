# Equipment pieces delivery - 26 October 2026

This checkpoint supplies **516 of 869 requested files**, based on source commit `e0ede57368aa1bde6e94a6baf67898631053183c`. The full checklist is authorized for incremental upload in upstream order. Family masters precede dependent views; whole-figure masters precede seeded region close-ups. No extra design variants are included. 353 files remain undelivered.

Delivery branch: `art/equipment-pieces-20261026`.

Updated on 2 November 2026. The art branch is synchronized with upstream integration commit `a3c93600a19d1c4ee78126b7dcdd22ee08bd2c9e`. The requested 869-file list, order, prompts, dimensions and acceptance thresholds are unchanged. Geometry corrections are described below. Upstream has already integrated the first 181 delivered files.

## Delivered scope

| Batch | Requested | Delivered | Remaining |
|---|---:|---:|---:|
| 1 | 277 | 277 | 0 |
| 2 | 299 | 223 | 76 |
| 3 | 293 | 16 | 277 |
| Total | 869 | 516 | 353 |

| Asset type | Delivered files |
|---|---:|
| Bare region plates | 13 |
| Worn equipment views | 462 |
| Held pieces and companions | 7 |
| Pieces painted on hosts | 16 |
| Separate attachments | 13 |
| Sheets of small parts | 5 |

Delivered families: amulet, ankle guard, ankle tattoo, ankle wrap, anklet, arm cuff, arm harness, arming cap, arming sleeve, armlet, back tattoo, backpack, backplate, baldric, bare plates, beard rings, bell, belly chain, belt, besagew, bevor, boot, bracelet, bracer, braid beads, breast tattoo, breastbone tattoo, breastplate, breeches, bridge stud, brigandine, brooch, brow guard, brow tattoo, buckle, calf band, calf guard, calf tattoo, charm, chausses, cheek fitting, cheek guard, chest harness, chest tattoo, chin guard, chin tattoo, choker, circlet, clasp, cloak, coif, collarbone stud, collarbone tattoo, couter, crest, crossguard, cuirass, cuisse, culet, diadem, ear chain, ear cuff, earring, elbow tattoo, eyebrow ring, eyepatch, face plate, falcon hood, fauld, faulds and tassets, fingerless glove, flank tattoo, focus orb, foot chain, foot tattoo, forearm sheath, forearm tattoo, forehead jewel, gambeson, garter, gauntlet, gem, glove, gold teeth, gorget, great helm, great weapon, greave, grip wrap, hair pin, hand chain, hand jewel, hand tattoo, heart tattoo, helix ring, heraldry, hip chain, hip tattoo, hood, jesses, kidney dagger, kidney guard, kidney tattoo, knee pit tattoo, knee wrap, knuckle ring, knuckleduster, kohl, laces, lance rest, lantern, lat guard, lat tattoo, leg harness, leg wraps, lip ring, loincloth, mail shirt, mail sleeve, main hand, mantle, medal, monocle, nail lacquer, nape tattoo, nasal helm, navel tattoo, neck cloth, necklace, nose guard, nose stud, off hand, outer thigh tattoo, palm sigil, pauldron, pectoral plate, plackart, plexus disc, plexus gem, plume, poleyn, pommel, potion, pouch, quiver, rerebrace, rib chain, rib guard, ring, ring tattoo, robe, rune, sallet, sandal, sash, scabbard, scale vest, scapula plate, scarf, septum ring, shield, shield boss, shield rim, shin tattoo, shirt, shoulder blade tattoo, shoulder cape, shoulder familiar, shoulder lantern, shoulder tattoo, side plate, side sheath, sock, spaulder, spectacles, spike, spine guard, spur, surcoat, tabard, tasset, thigh sheath, thigh strap, thigh tattoo, thimble, throat jewel, throat tattoo, thumb tattoo, toe ring, toenail paint, torc, torc terminal, triceps tattoo, trousers, upper arm tattoo, vambrace, waist tattoo, war paint, wizard hat, wrist tattoo.

## Files and review

- [Delivery manifest](../art/equipment/pieces-delivery-20261026.json): exact paths, dimensions, selected attempts and SHA-256 hashes.
- [Generation prompts](../art/equipment/pieces-prompts-20261026.json): full English prompts and generation method for all attempts associated with delivered assets.
- [Validation output](../art/equipment/pieces-validation-20261026.txt): complete phase-2 check results.
- [Full checklist](art-handoff-equipment-pieces-list.md): delivered files checked; remaining files unchecked.
- [Family review sheets](../art/equipment/review/pieces/): overlays on registered bases. These are inspection aids, not runtime textures.

Production PNGs occupy the exact requested paths under `art/equipment/`. Keep their green canvases and coordinates intact. Original generations and intermediate attempts are archived locally; only selected final PNGs are uploaded.

## Production and validation

The images were made with the built-in `image_gen` tool using upstream bases, seeds, placement guides and family references. Its exact model identifier is not exposed; no separate API or CLI model was selected. Paint, isolation and correction stages are recorded in the prompt set. Some seeded images already wear the whole-figure piece; restoration and isolation are recorded separately or as `restore_isolate`.

Final exports are 8-bit RGB PNGs with an sRGB profile. Generated canvases were resized to the requested dimensions without cropping, recentering or moving individual parts. Square attachment generations may be downsampled from a larger native square canvas. Pixels already classified as key green by the importer were normalized to `#00ff00`. Scripts did not repaint foregrounds or quantize colours. Generated shading has minor tonal variation and is not a strict three-colour indexed palette.

The phase-2 checker reports:

```text
516 of 516 delivered files pass; 353 of 869 not delivered yet
```

The unchanged phase-1 assets pass their original checker: `10 of 10 pass`. Review sheets were also inspected; numeric checks do not replace visual review. Exported paths, text files and PNG metadata are checked for Chinese characters before each upload.

## Registration and integration notes

Keep the plate coordinates unchanged. Regenerate inputs after delivering a whole-figure master used to seed region views:

```sh
python3 tools/equipment_pieces.py inputs <asset-id-or-prefix>
```

Occluded or off-canvas portions are intentionally absent. Paired items retain any visible fragment of the other limb at a cropped canvas edge. Cloth and back armor leave the braid in front; minor variation remains along its fine edge. Glove and armor cuffs stay inside their assigned regions. Rings, chains and open pendants have keyed openings so the base shows through. The great helm has six breathing holes on its right cheek only; close-up eye slits are keyed, while small whole-figure eye slits are dark. Left anklet links have a heavier outline than the right-side master.

Eyepatches cover the selected eye fully, with short visible temple straps. Monocle centers are keyed so the original eye remains visible. Tattoo overlays contain only ink; preserve clothing and body occlusion when combining layers.

Foot-chain sole views contain only the visible back of the second-toe ring, as defined by their placement guides. The dorsal chain and ankle connection belong to the top views.

The hip-crescent specification places both tattoos entirely beneath the base's shorts. As explicitly approved, the delivered layer keeps both complete tattoos in those requested positions. Apply underwear occlusion during integration; compositing this ink directly over opaque shorts would incorrectly display it on the fabric. The review sheet shows unoccluded registration for inspection.

The outer-thigh rune tattoo also extends beneath the base's shorts. Its full ink layer preserves the specified anatomical placement. Keep the original shorts above the tattoo during runtime compositing; no altered underwear is included in the delivery.

## Geometry corrections

The phase-2 `areas()` function includes a geometry correction. Cropped region bases lose off-canvas endpoints of reach primitives, so the original calf-length cloak mask stopped at the shoulder or waist despite seeded artwork continuing downward. The correction projects the full-figure specification mask using the artwork's coordinate transform, then unions it with the original region mask. It uses the specification mask, not the asset silhouette. Acceptance thresholds and the 869-file specification are unchanged.

Before correction, only 32.0-70.3% of the affected cloak seeds lay inside their region masks; corrected masks contain 99.7-100%. Two passing regression tests cover five affected crop edges and ensure the front drape still excludes the visible arm.

The gambeson reach now includes both mapped shoulder sites with the normal 0.35-body-unit Under-layer allowance. Its requested short sleeves must connect to the torso across these shoulder caps, but the occupied slot list omitted the connecting surfaces. This created false failures on close-ups, even for the registered whole-figure design. The correction uses the anatomy map, not generated artwork. It does not extend coverage into forearms or background and does not relax any acceptance threshold. Three additional passing regression tests cover shoulder continuity, exclusion of forearms and background, and preservation of all 869 requested files, their order, prompts and dimensions.

The plexus disc reach includes two narrow 1.4-body-unit straps from the central plexus to the mapped armpits. The description requires a strap around the chest, but the original slot-only mask allowed only the central disc. The correction uses anatomical endpoints and the existing strap primitive; it leaves the disc's required coverage and all acceptance thresholds unchanged. Two additional passing tests verify both chest connections and exclusion of the upper chest, belly, upper arms and background.

The nose guard reach includes the mapped brow for its explicitly requested brow band. The coif reach includes the ear-slit edges, nape, a narrow rim around the mapped chin, and room immediately below the chin for the cord knot. The rim excludes the mouth and its surrounding margin, so the fastening mask does not cover the central face. These additions follow anatomical sites, not generated silhouettes. Four additional passing tests cover fastening placement and exclusion of eyes, central face, lower neck and background.

The thumb tattoo uses the mapped thumb as its art region. Its gameplay slot reserves the shared fingers skin slot, but the requested design is three dots on the thumb only; the original checker incorrectly measured coverage against all five digits. A regression test verifies all four thumb views and excludes the other fingers, palm and background.

The chin tattoo uses a narrow anatomical region immediately below the lower lip, derived from the mouth bounds and body scale. The shared chin gameplay slot includes the entire beard and lower cheeks, which are not the requested short vertical column of dots. Its minimum ink coverage now measures that small region, including the lower-lip margin omitted by the chin slot. The visible dots are retained and the beard occludes the rest. A regression test checks placement below the lip and exclusion of the lip centre, broad beard area and background.

Backpack front straps now start at the shoulder top between the shoulder and clavicle, and end at the mapped armpit. The old strap began at the shoulder site's centre, halfway down the upper arm, so it omitted the upper connection explicitly required by the description. The new narrow strap primitive derives its endpoints entirely from the anatomy map. A regression test checks the full connection on the figure and torso while excluding the central chest and belly.

All fourteen geometry regression tests pass. The requested file count, order, prompts and dimensions remain unchanged, as do all acceptance thresholds.

Reproduce validation from the repository root:

```sh
python3 tools/equipment_pieces.py check
python3 tools/equipment_art.py check
python3 tools/equipment_pieces.py sheet
python3 -m unittest discover -s tools -p test_equipment_pieces.py
```

Upstream has implemented import, plate mapping and runtime rendering for the first 181 files. Newly delivered artwork still needs the upstream import and integration pass; companions and host attachments also need the runtime support noted in the upstream handoff. The source-art files preserve the requested green canvases and coordinates. Undelivered files are not represented as complete.
