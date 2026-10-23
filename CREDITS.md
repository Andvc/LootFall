# Credits

## Art

The pictures are LootFall's own, drawn for this game as pixel art in code, so anyone can rebuild them with
`python3 tools/import_art.py`:

- `tools/custom_art.py`: the 33 things in chests (six kinds, five qualities, three fakes), the portraits of every
  seller, Bram, his kin, Mister Vane, Nim, the Hooded Woman and the Stranger, each against the place they come from;
  the chest (recoloured per lot origin), the shards of the Aldric Key, the Gilded Guild's sign, the 25 curios, the
  four places behind the counter (the Brindle crossroads at dusk, Thornwick's stone wall onto the Old Wood,
  Crownhold's roofs and palace, the Royal Emporium's colonnade and the Gilded Gate), the props on the counters, and
  the burst a chest opens with.
- `tools/import_art.py`: the 9-slice frames, the tool, upgrade and kind icons, and the shops' architecture (awning,
  beams, panelling, drapes, shelves and counters).

## Font and sound

By [Kenney](https://kenney.nl), released under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/):

- **Kenney Mini** from the Kenney Font Package.
- Impact Sounds, RPG Audio, Casino Audio and Interface Sounds: the sound each kind of thing makes when you shake a
  chest (coins, tin, cloth, cards, glass), the chest landing, coins, doors and latches.

The license doesn't require attribution; it's given here because Kenney made the game sound good.
`tools/import_art.py` records exactly which source file each sound in `assets/audio/` came from.

Earlier versions (up to 0.15) used pixel art from Pixel-boy's CC0
[Superpowers Asset Packs](https://github.com/sparklinlabs/superpowers-asset-packs); thanks to them for the start.
