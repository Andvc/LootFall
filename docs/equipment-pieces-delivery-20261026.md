# Equipment pieces delivery - 26 October 2026

This checkpoint supplies the first 26 requested files: the 13 bare-region plates and all 13 views for the four starting equipment families. It is a partial delivery against the 869-file phase-2 specification, based on commit `e0ede57368aa1bde6e94a6baf67898631053183c`.

| Group | Files | Contents |
|---|---:|---|
| Bare plates | 13 | Neck, torso, back, both waist views, and front/back views of each arm and leg |
| Sallet | 5 | Head front, right and left; whole figure front and back |
| Breastplate | 1 | Torso |
| Main hand | 1 | Arming sword |
| Boot | 6 | Right-foot top and sole; both legs from front and back |
| Total | 26 | One design per family, with no extra variants |

## Files and review

- [Machine-readable delivery manifest](../art/equipment/pieces-delivery-20261026.json) includes dimensions, selected attempts and SHA-256 hashes.
- [Full generation prompt set](../art/equipment/pieces-prompts-20261026.json) records the built-in image generation method and all attempts for this checkpoint.
- [Validation output](../art/equipment/pieces-validation-20261026.txt) contains the unmodified upstream checker's results.
- [Updated full checklist](art-handoff-equipment-pieces-list.md) leaves undelivered files unchecked.
- Review sheets: [plates](../art/equipment/review/pieces/plates.png), [sallet](../art/equipment/review/pieces/sallet.png), [breastplate](../art/equipment/review/pieces/breastplate.png), [boots](../art/equipment/review/pieces/boot.png), [sword](../art/equipment/review/pieces/sprites.png).

Production PNGs are at their exact requested paths under `art/equipment/plates/`, `art/equipment/pieces/` and `art/equipment/sprites/`. The review sheets are inspection aids, not runtime textures.

## Production and verification

The images were made with the built-in `image_gen` tool using the upstream seeds, bases, placement guides and family references. Its exact model identifier is not exposed; no separate API or CLI model was selected. Paint and isolation stages are recorded in the prompt set. The top and sole boot views already contained only the finished boot, so a second removal edit was unnecessary.

Each selected result was exported as an 8-bit RGB PNG with an sRGB profile. The small difference between generated and specified dimensions was normalized across the whole canvas, without cropping, recentering or moving individual parts. Pixels already classified as key green by the upstream importer were normalized to `#00ff00`. Foreground colours were not quantized or otherwise repainted by scripts. Generated shading has minor tonal variation and is not a strict three-colour indexed palette.

The original phase-2 checker reports:

```text
26 of 26 delivered files pass; 843 of 869 not delivered yet
```

The original phase-1 checker also reports `10 of 10 pass`. Neither checker was changed. The review sheets were inspected over the registered bases, including correction of the left rear boot so the bare heel no longer protrudes.

Reproduce the checks from the repository root:

```sh
python3 tools/equipment_pieces.py check
python3 tools/equipment_art.py check
python3 tools/equipment_pieces.py sheet
```

## Remaining work and integration

Batch 1 has 251 files remaining; batches 2 and 3 have 299 and 293 respectively. The next requested family is the travelling cloak. No undelivered file is represented as finished.

Keep the plate coordinates unchanged. Regenerate inputs after adding the plates and after delivering a whole-figure master used to seed region views:

```sh
python3 tools/equipment_pieces.py inputs <asset-id-or-prefix>
```

Import, material recolouring, slot mapping and runtime rendering remain the integration work described by the upstream handoff. This checkpoint contains art and delivery records only. Use the original full-body and close-up coordinates; do not trim the green canvas around overlays.
