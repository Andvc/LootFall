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

The 25 October 2026 delivery includes ten primary PNGs and eight optional alternatives.
Read [the delivery handoff](../../docs/equipment-art-delivery-20261025.md) for previews,
selection notes, native-resolution limits, prompts, file hashes and validation results.
The primary set passes all ten checks; each alternative passes its corresponding check.
