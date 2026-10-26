# Equipment pieces delivery - 26 October 2026

This checkpoint supplies **181 of 869 requested files**, based on source commit `e0ede57368aa1bde6e94a6baf67898631053183c`. The full checklist is authorized for incremental upload in upstream order. Family masters precede dependent views; whole-figure masters precede seeded region close-ups. No extra design variants are included. 688 files remain undelivered.

Delivery branch: `art/equipment-pieces-20261026`.

## Delivered scope

| Batch | Requested | Delivered | Remaining |
|---|---:|---:|---:|
| 1 | 277 | 181 | 96 |
| 2 | 299 | 0 | 299 |
| 3 | 293 | 0 | 293 |
| Total | 869 | 181 | 688 |

| Asset type | Delivered files |
|---|---:|
| Bare region plates | 13 |
| Worn equipment views | 165 |
| Held pieces and companions | 3 |
| Pieces painted on hosts | 0 |
| Separate attachments | 0 |
| Sheets of small parts | 0 |

Delivered families: amulet, anklet, armlet, backplate, baldric, bare plates, boot, bracelet, bracer, breastplate, brooch, circlet, cloak, couter, cuisse, ear cuff, earring, eyepatch, gauntlet, glove, gorget, great helm, greave, hair pin, hood, kidney dagger, knuckle ring, main hand, monocle, off hand, pauldron, pectoral plate, plackart, poleyn, rerebrace, ring, ring tattoo, sallet, sandal, shield, shirt, spaulder, tabard, tasset, thigh tattoo, toe ring, trousers, vambrace.

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
181 of 181 delivered files pass; 688 of 869 not delivered yet
```

The unchanged phase-1 assets pass their original checker: `10 of 10 pass`. Review sheets were also inspected; numeric checks do not replace visual review. Exported paths, text files and PNG metadata are checked for Chinese characters before each upload.

## Registration and integration notes

Keep the plate coordinates unchanged. Regenerate inputs after delivering a whole-figure master used to seed region views:

```sh
python3 tools/equipment_pieces.py inputs <asset-id-or-prefix>
```

Occluded or off-canvas portions are intentionally absent. Cloth and back armor leave the braid in front; minor variation remains along its fine edge. Glove and armor cuffs stay inside their assigned regions. Rings, chains and open pendants have keyed openings so the base shows through. The great helm has six breathing holes on its right cheek only; close-up eye slits are keyed, while small whole-figure eye slits are dark. Left anklet links have a heavier outline than the right-side master.

Eyepatches cover the selected eye fully, with short visible temple straps. Monocle centers are keyed so the original eye remains visible. Tattoo overlays contain only ink; preserve clothing and body occlusion when combining layers.

## Seeded-mask correction

The phase-2 `areas()` function includes a geometry correction. Cropped region bases lose off-canvas endpoints of reach primitives, so the original calf-length cloak mask stopped at the shoulder or waist despite seeded artwork continuing downward. The correction projects the full-figure specification mask using the artwork's coordinate transform, then unions it with the original region mask. It uses the specification mask, not the asset silhouette. Acceptance thresholds and the 869-file specification are unchanged.

Before correction, only 32.0-70.3% of the affected cloak seeds lay inside their region masks; corrected masks contain 99.7-100%. Two passing regression tests cover five affected crop edges and ensure the front drape still excludes the visible arm.

Reproduce validation from the repository root:

```sh
python3 tools/equipment_pieces.py check
python3 tools/equipment_art.py check
python3 tools/equipment_pieces.py sheet
python3 -m unittest discover -s tools -p test_equipment_pieces.py
```

Import, material recolouring, slot mapping and runtime rendering remain the integration work described in the upstream handoff. This checkpoint contains art, delivery records, review sheets and the tested mask-generation correction. Undelivered files are not represented as complete.
