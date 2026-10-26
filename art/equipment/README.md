# Equipment screen art: upload folder

The equipment screen's pictures go here, named exactly as in the checklist in
[docs/art-handoff-equipment.md](../../docs/art-handoff-equipment.md); `handoff.json` has the same list with each file's
full prompt and references. Godot doesn't import this folder (`art/.gdignore`) and exports leave it out: the game reads
the imported copies in `assets/equipment/`.

Before uploading, check the files:

    python3 tools/equipment_art.py check     # must end "10 of 10 pass"
    python3 tools/equipment_art.py sheet     # review.png, the files side by side

Then bring them into the game and map them:

    python3 tools/equipment_art.py import    # keyed and cut into assets/equipment/
    python3 tools/equipment_map.py           # the sites and slots on them; must report 0 problems
    godot --headless --path . --import

## Phase 2: the pieces

The bare body region by region and every piece as it's worn go in folders here, named as in
[docs/art-handoff-equipment-pieces.md](../../docs/art-handoff-equipment-pieces.md) (every file is listed in
[its full list](../../docs/art-handoff-equipment-pieces-list.md); `handoff-pieces.json` has each file's prompts,
masks and references):

    plates/<plate>.png                 the bare body, region by region
    pieces/<base>/<family>.png         a family of pieces as worn, on one of the pictures it's painted on
    pieces/<base>/<host>/<family>.png  a piece set in another, painted on its host
    sprites/<name>.png                 held pieces, companions and the sky; sprites/<host>/<family>.png on them
    attachments/<name>.png             parts placed on their hosts
    sheets/<family>.png                small parts on a grid

Make what each file is painted on, then check what's made:

    python3 tools/equipment_pieces.py inputs <ids>   # bases, masks and guides into inputs/ (not committed)
    python3 tools/equipment_pieces.py check <ids>    # every delivered file must pass
    python3 tools/equipment_pieces.py sheet <ids>    # review sheets in review/pieces/
    python3 tools/equipment_pieces.py list           # ticks off the full list

Then bring what's delivered into the game:

    python3 tools/equipment_pieces.py import         # into assets/equipment/ (plates/, pieces/, sprites/, pieces.json)
    godot --headless --path . --import

## The phase-1 delivery

The 25 October 2026 delivery includes ten primary PNGs and eight optional alternatives.
Read [the delivery handoff](../../docs/equipment-art-delivery-20261025.md) for previews,
selection notes, native-resolution limits, prompts, file hashes and validation results.
The primary set passes all ten checks; each alternative passes its corresponding check.
