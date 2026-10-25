# Equipment screen art: upload folder

The equipment screen's pictures go here, named exactly as in the checklist in
[docs/art-handoff-equipment.md](../../docs/art-handoff-equipment.md); `handoff.json` has the same list with each file's
full prompt and references. Godot doesn't import this folder (`art/.gdignore`) and exports leave it out: the game reads
the imported copies in `assets/equipment/`.

Before uploading, check the files:

    python3 tools/equipment_art.py check     # must end "10 of 10 pass"
    python3 tools/equipment_art.py sheet     # review.png, the files side by side
