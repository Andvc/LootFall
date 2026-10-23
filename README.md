# LootFall

A small incremental game about the thrill of opening chests. Your knight fights on his own, wave after wave, through
six places and their bosses; every monster he fells may drop a chest, and every chest holds one piece of gear. Open
one and it hops, glows and climbs through the rarity colours (common, uncommon, rare, epic, legendary, mythic) before
it bursts and shows you what you got, side by side with what he wears. Equip it and watch it land: his blade and cape
change colour, the power number jumps, the next monster dies faster, and a legendary's power (lightning, embers,
meteors, a barrier) goes off in the fight. Sell it and it's gold for a better chest level, training, and a helper that
opens chests for you and stops for the big ones. Stronger gear, further stages, more gold, more chests.

**Status:** Prototype 0.1, a fresh start on the `prototype/loot-battle` branch (the shake-and-deduce shop is on
`prototype/chest-loop`). Playable from the title to the endless stages past the sixth zone: 24 kinds of monster in six
zones, stage bosses and six guardians, eight gear slots, six rarities, 14 legendary powers, four kinds of chest, chest
levels, training, Auto-Boss and Auto-Open, saves with time-away earnings, music per zone. Built with Godot 4.7 (.NET /
C#), targeting PC. Pixel art and music from Pixel-boy's CC0 Superpowers packs, sounds from Kenney's CC0 packs (see
[CREDITS.md](CREDITS.md)). Design and balance: [docs/design.md](docs/design.md).

## The loop

1. **The knight fights.** Monsters walk in from the right; he swings at the one in front. Ten kills and the stage's
   boss comes (30 seconds to beat it); beat it and the next stage begins. Every tenth stage a guardian, then a new
   place.
2. **Chests fall.** Most kills drop a wooden chest, which pops out of the monster and falls into the pile under the
   battle. Bosses drop iron chests, guardians golden ones, and now and then a wandering mimic drops a royal chest.
3. **Open them.** Click the chest or press Space. The rarer it is, the longer it builds; Luck can bump it one step
   more. The card shows the item's stats against what's worn and the change in power.
4. **Equip or sell.** Equip (E) puts it on and sells the old one; Sell (S) turns it into gold. Space takes whichever
   the card recommends, once the card has been up a moment (longer for a legendary), so mashing Space to hurry an
   opening never sells what it reveals.
5. **Spend the gold.** Chest Level makes new loot stronger and rarer; Might, Vigor and Haste train the knight; from the
   second zone, Auto-Open handles the small stuff and stops for anything as rare as you choose.
6. **Bounties.** Three goals at a time (slay, open, beat bosses, find rares...), each paying a chest when done.

Close the game and the knight keeps fighting: on Continue, the time away (up to 8 hours) is paid in gold and chests.

## Controls

| Action | Input |
|---|---|
| Open a chest / hurry an opening / take the recommended choice | Click the chest, or Space / Enter (hold to keep opening) |
| Equip / Sell the item on the card | E / S, or the card's buttons |
| Fight the boss now | B, or the boss button (when it's ready; after the mimic, if one's in front) |
| Auto-Boss, Auto-Open | Their switches (under the boss button; on the chest, beside the rarity it stops for) |
| Buy an upgrade | Its button, on the right |
| See an item's stats | Hover its slot |
| Pause, settings, title, quit | Esc, or Menu |

## Development

The rules run without the engine, so they can be simulated and tested headlessly:

| Folder | What's in it |
|---|---|
| `scripts/Core` | Pure C# rules: gear, rarities and legendary powers (`Gear.cs`), chests and their odds, monsters and zones (`World.cs`), the real-time battle (`Battle.cs`), the game state and its events, upgrades, saves, a sensible player (`Policy.cs`), and every pacing number (`Balance.cs`). No Godot types. |
| `scripts/Ui` | The screens and visuals: the battle lane, the chest and its opening, the reveal card, gear and upgrade panels, HUD, title, pause, settings, effects, the actors' palette-swap shader. |
| `scripts/Audio` | Recorded sounds by group, synthesized tings and stings, music. |
| `scripts/Debug` | The balance simulation and the autoplay bot. |
| `tools/` | `setup-cloud.sh` (toolchain), `import_art.py` (builds `assets/` from the CC0 packs), `simcli/` (the simulation without the engine, and the pacing tuner). |

```bash
tools/setup-cloud.sh                                        # .NET 8 + Godot 4.7.2 (.NET) in a fresh container
dotnet build                                                # compile
dotnet build tools/simcli -c Release && \
  dotnet tools/simcli/bin/Release/net8.0/simcli.dll --n=12 --hours=5    # balance report (or: godot --headless --path . -- --sim)
dotnet tools/simcli/bin/Release/net8.0/simcli.dll --tune    # fit Balance.cs to the pacing targets
xvfb-run -a godot --path . --rendering-driver opengl3 -- \
  --autoplay --minutes=3 --shots=$PWD/shots                 # the bot plays; screenshots of each moment
# add --stage=30 to start where a sensible player would be by then, --speed=3 to play faster, --auto-open to use it
xvfb-run -a godot --path . -- --opening-demo --shots=$PWD/shots_open   # one chest of each rarity, pause and Space checks
xvfb-run -a godot --path . -- --title-demo --shots=$PWD/shots_title    # title, settings, a new game, pause
godot --headless --path . --export-release "Windows Desktop" build/windows/LootFall.exe
```

Add `--write-movie movies/play.avi --fixed-fps 30` to record. Every push to `prototype/**` exports a Windows build
and smoke-tests it on GitHub Actions (download it from the run's artifacts).
