# Equipment pieces delivery - 26 October 2026

This checkpoint supplies **359 of 869 requested files**, based on source commit `e0ede57368aa1bde6e94a6baf67898631053183c`. The full checklist is authorized for incremental upload in upstream order. Family masters precede dependent views; whole-figure masters precede seeded region close-ups. No extra design variants are included. 510 files remain undelivered.

Delivery branch: `art/equipment-pieces-20261026`.

Updated on 1 November 2026. The art branch is synchronized with upstream integration commit `a3c93600a19d1c4ee78126b7dcdd22ee08bd2c9e`. The requested 869-file list, order, prompts, dimensions and acceptance thresholds are unchanged. Geometry corrections are described below. Upstream has already integrated the first 181 delivered files.

## Delivered scope

| Batch | Requested | Delivered | Remaining |
|---|---:|---:|---:|
| 1 | 277 | 277 | 0 |
| 2 | 299 | 82 | 217 |
| 3 | 293 | 0 | 293 |
| Total | 869 | 359 | 510 |

| Asset type | Delivered files |
|---|---:|
| Bare region plates | 13 |
| Worn equipment views | 323 |
| Held pieces and companions | 7 |
| Pieces painted on hosts | 1 |
| Separate attachments | 10 |
| Sheets of small parts | 5 |

Delivered families: amulet, ankle guard, ankle tattoo, anklet, arm cuff, arm harness, arming cap, arming sleeve, armlet, backplate, baldric, bare plates, bell, belt, bevor, boot, bracelet, bracer, braid beads, breast tattoo, breastplate, bridge stud, brooch, buckle, calf guard, calf tattoo, charm, chausses, cheek guard, choker, circlet, cloak, collarbone stud, collarbone tattoo, couter, crest, cuisse, ear chain, ear cuff, earring, eyebrow ring, eyepatch, falcon hood, fingerless glove, focus orb, foot chain, forearm sheath, gambeson, garter, gauntlet, gem, glove, gold teeth, gorget, great helm, great weapon, greave, hair pin, heart tattoo, heraldry, hip chain, hip tattoo, hood, kidney dagger, kidney guard, kidney tattoo, knee pit tattoo, knee wrap, knuckle ring, kohl, lance rest, lat guard, leg harness, lip ring, loincloth, main hand, mantle, medal, monocle, nail lacquer, nape tattoo, nasal helm, navel tattoo, necklace, nose stud, off hand, pauldron, pectoral plate, plackart, plexus disc, plexus gem, plume, poleyn, pouch, rerebrace, ring, ring tattoo, rune, sallet, sandal, scale vest, scapula plate, septum ring, shield, shin tattoo, shirt, shoulder familiar, shoulder lantern, shoulder tattoo, sock, spaulder, spike, spur, surcoat, tabard, tasset, thigh sheath, thigh strap, thigh tattoo, toe ring, torc, torc terminal, trousers, vambrace, waist tattoo.

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
359 of 359 delivered files pass; 510 of 869 not delivered yet
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

## Geometry corrections

The phase-2 `areas()` function includes a geometry correction. Cropped region bases lose off-canvas endpoints of reach primitives, so the original calf-length cloak mask stopped at the shoulder or waist despite seeded artwork continuing downward. The correction projects the full-figure specification mask using the artwork's coordinate transform, then unions it with the original region mask. It uses the specification mask, not the asset silhouette. Acceptance thresholds and the 869-file specification are unchanged.

Before correction, only 32.0-70.3% of the affected cloak seeds lay inside their region masks; corrected masks contain 99.7-100%. Two passing regression tests cover five affected crop edges and ensure the front drape still excludes the visible arm.

The gambeson reach now includes both mapped shoulder sites with the normal 0.35-body-unit Under-layer allowance. Its requested short sleeves must connect to the torso across these shoulder caps, but the occupied slot list omitted the connecting surfaces. This created false failures on close-ups, even for the registered whole-figure design. The correction uses the anatomy map, not generated artwork. It does not extend coverage into forearms or background and does not relax any acceptance threshold. Three additional passing regression tests cover shoulder continuity, exclusion of forearms and background, and preservation of all 869 requested files, their order, prompts and dimensions.

The plexus disc reach includes two narrow 1.4-body-unit straps from the central plexus to the mapped armpits. The description requires a strap around the chest, but the original slot-only mask allowed only the central disc. The correction uses anatomical endpoints and the existing strap primitive; it leaves the disc's required coverage and all acceptance thresholds unchanged. Two additional passing tests verify both chest connections and exclusion of the upper chest, belly, upper arms and background. All seven geometry regression tests pass.

Reproduce validation from the repository root:

```sh
python3 tools/equipment_pieces.py check
python3 tools/equipment_art.py check
python3 tools/equipment_pieces.py sheet
python3 -m unittest discover -s tools -p test_equipment_pieces.py
```

Upstream has implemented import, plate mapping and runtime rendering for the first 181 files. Newly delivered artwork still needs the upstream import and integration pass; companions and host attachments also need the runtime support noted in the upstream handoff. The source-art files preserve the requested green canvases and coordinates. Undelivered files are not represented as complete.
