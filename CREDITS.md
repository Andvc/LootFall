# Credits

## Art and music

**Superpowers Asset Packs** by [Pixel-boy](https://twitter.com/2pblog1) / Sparklin Labs, released under
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).
Source: https://github.com/sparklinlabs/superpowers-asset-packs

- RPG Battle System pack: the knight (`char/warrior2`), every monster and its second palette (`monster/*`), the zone
  and title backdrops (`backgrounds`), the battle effects (`fx`), the gear pictures (`item`; a few recoloured for the
  rarer tiers) and the chest (`item/chest`, recoloured per kind), and the battle and boss music (`music`).
- Ninja Adventure pack: the title music (`musics/theme-11`) and a few sounds (`sounds`: kill, gold, power-up, success,
  game over).

## Font and sound

By [Kenney](https://kenney.nl), released under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/):

- **Kenney Mini** from the Kenney Font Package.
- RPG Audio, Impact Sounds, Interface Sounds, Casino Audio and Music Jingles: blows and swishes, armour taking hits,
  coins, the chest's creak, latch and lid, landing thuds, equipping, clicks, and the victory and fanfare jingles.

The UI frames and the 16x16 stat icons are drawn in `tools/import_art.py`, which also records exactly which source file
each asset in `assets/` came from. The chest's build-up tones, sparkles and stings are synthesized in
`scripts/Audio/Sfx.cs`.

None of these licenses require attribution; it's given here because these artists made the game look and sound good.
