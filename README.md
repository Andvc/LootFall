# LootFall

An incremental loot game about abundance. A knight fights ever deeper on his own. Every depth's monsters and boss fill a
chest, and opening one climbs through twelve rarities, from Common to Transcendent, before the cards turn over.
Whatever beats what he wears goes on by itself, across 149 slots from helm to toe rings, tattoos, companions and
celestial slots. Gold forges the slots, shards rank them, and levels pay into a 3,259-node talent tree. First finds
fill the codex, and ascension and transcendence trade a run for power that lasts. It is built to keep a player
progressing for thousands of hours.

**Status:** Prototype 0.2, playable from the title to the second prestige layer:
- the battle, with waves, bosses, guardians, the gate and overkill leaps through eight realms;
- the chest tray and the reveal for all twelve rarities;
- the armoury (forge, ranks, mastery focus), the talent tree, the codex, ascension and transcendence;
- auto-open and the other automations;
- saves with time away, the title, pause and settings.

Built with Godot 4.7 (.NET / C#) for PC.

The direction is inked cel 2D over muted painted backdrops ([docs/art-direction.md](docs/art-direction.md)). The
pictures are still stand-ins, to be replaced by the game's own art (see [CREDITS.md](CREDITS.md)). The design is in
[docs/design.md](docs/design.md), and the numbers behind it are in [docs/progression.md](docs/progression.md).

## Controls

| Action | Input |
|---|---|
| Open the next chest; hurry an opening | Space or Enter, or click the chest |
| Armoury, Talents, Codex, Ascension | G, T, C, A, or the dock's buttons |
| Close a screen; pause, settings, title, quit | Esc |

## Development

| Folder | What's in it |
|---|---|
| `scripts/Progression` | The numbers: content (slots, rarities, affixes, the talent tree, prestige trees), the closed-form combat, the world's step, the simulated player and the acceptance checks. Engine-free; every number is in `Params.cs`. |
| `scripts/Game` | The game around them: the session (chests, automation, time away), the battle's choreography (`Stage`), the bestiary, saves, names and number formats. Engine-free. |
| `scripts/View` | The screens: the lane (backdrop, the knight's rig, monsters, effects), HUD, chest tray, reveal, armoury, talents, codex, prestige, title, menus, the bot and the demos. |
| `scripts/Audio` | Recorded sounds by group, synthesized tones and stings, music. |
| `tools/progsim` | Runs the numbers and the game without the engine: the 3,000-hour check, player reports, tuning, the game-layer test, the content list for the art manifest. |
| `tools/` | `setup-cloud.sh` (toolchain), `placeholders.py` (the stand-in art), `art.py` (the art manifest and importer), `import_audio.py` (sounds and music). |

```bash
tools/setup-cloud.sh                         # .NET 8 + Godot 4.7.2 (.NET) in a fresh container
dotnet build                                 # compile
dotnet build tools/progsim -c Release
dotnet tools/progsim/bin/Release/net8.0/progsim.dll --check            # the numbers over 3,000 hours (CI)
dotnet tools/progsim/bin/Release/net8.0/progsim.dll --game --hours=1   # the game layer (CI)
xvfb-run -a godot --path . --rendering-driver opengl3 -- --autoplay --minutes=3 --shots=$PWD/shots
#   the bot plays and screenshots each moment; --hours=N starts where a sensible player would be after N hours,
#   --speed=N plays faster, --seed=N
xvfb-run -a godot --path . --rendering-driver opengl3 -- --demo=opening --shots=$PWD/shots_open
#   demos: opening (a chest of every rarity), title, welcome (a save round trip and two hours away),
#   screens (every screen; add --hours=6 for a game in progress)
python3 tools/art.py manifest                # every asset with its size and prompt -> art/manifest.json
python3 tools/art.py import                  # new art from art/incoming/<id>.png into assets/
godot --headless --path . --export-release "Windows Desktop" build/windows/LootFall.exe
```

Add `--write-movie movies/play.avi --fixed-fps 30` to record. Every push to `main` or a `prototype/**` branch exports
a Windows build and smoke-tests it on GitHub Actions (download it from the run's artifacts).
