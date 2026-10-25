# Credits

Most of the pictures in `assets/` are stand-ins. They were picked to stage the art direction
([docs/art-direction.md](docs/art-direction.md)) until the game's own art replaces them through `tools/art.py`.
`tools/placeholders.py` builds them and names the source file of each, and `art/manifest.json` marks every asset as a
placeholder or final.

Some stand-ins are under share-alike or copyleft licences. They are for development builds only, and every one must be
replaced before anything is published.

## Stand-in art (to be replaced)

- **Monsters**: creature artwork from [Ancient Beast](https://github.com/FreezingMoon/AncientBeast) by FreezingMoon
  and its artists, [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). Matted, cropped, outlined in ink
  and turned to face left.
- **Backdrops**: cut to the lane's band, desaturated and softened.
  - **Greenwood and Drowned Mire**: "Forest Background Art" by Tamara Ramsay (vectorgurl.com),
    [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/).
  - **Dunes**: "Oil painting landscapes" by JAP, [CC0](https://creativecommons.org/publicdomain/zero/1.0/).
  - **Ruins**: the mountain shrine by Guido Bos, adapted by Bertram, CC BY-SA 3.0 / GPLv3.
  - These four come by way of [Valyria Tear](https://github.com/ValyriaTear/ValyriaTear), whose `LICENSES.txt` lists
    each file.
  - **Frostpeak, Ashlands, Caverns and Starfall**: the Frozen Wall, Desolated Canyon, Shadow Cave and Dragon Bones
    locations from Ancient Beast, CC BY-SA 4.0.
- **Item icons**: silhouettes from [game-icons.net](https://github.com/game-icons/icons) by Lorc, Delapouite, Skoll,
  Lucas, Willdabeast and Seregacthtuf, [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). Filled with the
  material ramps, lit from the top left and outlined in ink.
- **Effects**: Kenney's Particle Pack ([kenney.nl](https://kenney.nl), via github.com/ETdoFresh/kenney.nl), CC0.

The game's icon, the knight, the chests, the frames and every rarity effect are drawn in code (`tools/placeholders.py`
and `scripts/View`).

## Fonts

All three are under the [SIL Open Font License 1.1](https://openfontlicense.org), from
[google/fonts](https://github.com/google/fonts). Their licence texts are in `assets/fonts`.

- **Cinzel**, by the Cinzel Project Authors (Natanael Gama): titles.
- **Barlow Semi Condensed**, by the Barlow Project Authors (Jeremy Tribby): the interface.
- **Lilita One**, by Juan Montoreano: numbers and banners.

## Sound and music

These are released under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) and built by
`tools/import_audio.py`.

- **Music** from the Superpowers Asset Packs by [Pixel-boy](https://twitter.com/2pblog1) / Sparklin Labs
  ([source](https://github.com/sparklinlabs/superpowers-asset-packs)):
  - the battle and boss themes from the RPG Battle System pack;
  - the title theme and a few sounds from the Ninja Adventure pack (kill, gold, power-up, success, game over).
- **Sound effects** by [Kenney](https://kenney.nl): RPG Audio, Impact Sounds, Interface Sounds, Casino Audio and
  Music Jingles. They cover blows, armour taking hits, coins, the chest's creak and latch, landing thuds, equipping,
  clicks and jingles.

The chest's build-up tones, sparkles and stings are synthesized in `scripts/Audio/Sfx.cs`.
