# LootFall: notes for Claude

Solo-developed PC game (Godot 4.7.2 .NET, C#, 2D, GL Compatibility renderer), an incremental loot game: a knight
auto-battles through zones, monsters drop chests, chests open into gear, gear shows up in the fight. The owner knows
Unity/C# and works through cloud sessions rather than locally, so changes must be verified here, not left for them to
test. `main` is this game. The shake-and-deduce shop that used to share this repository (on a branch) is now its own
game, Keywright, in Andvc/keywright; the two share nothing but the toolchain, and each keeps its files in its own
`app_userdata` folder.

## Setup in a fresh container

Run `tools/setup-cloud.sh` (add `--export-templates` before exporting). It installs the .NET 8 SDK and Godot to
`/opt/godot` and symlinks `godot`. After changing `assets/` run `godot --headless --path . --import` once.

## Verify every change

1. `dotnet build` must report 0 warnings and 0 errors.
2. Balance or rules changes: run the report before and after and compare
   (`dotnet build tools/simcli -c Release && dotnet tools/simcli/bin/Release/net8.0/simcli.dll --n=12 --hours=5`; the
   same report is `godot --headless --path . -- --sim`). Pacing targets are in `tools/simcli/Program.cs` and
   `docs/design.md`; `--tune` searches four knobs for them. Watch the upgrade rate, first finds, walls, and the
   deaths outside boss fights (they should stay rare). `SIM_DEATHS=1` logs each farming death with the knight's and
   the monsters' numbers.
3. Visual changes: run the bot under Xvfb with `--shots=<dir>` and look at the PNGs
   (`xvfb-run -a godot --path . --rendering-driver opengl3 -- --autoplay --minutes=2 --shots=$PWD/shots`). Flags:
   `--seed=N --stage=N` (start where a sensible player would be, via `Simulation.FastForward`) `--speed=N`
   `--minutes=N`, and `--auto-open` (buys Auto-Open as soon as it can, with a head start from stage 10, and plays
   with it on, so held finds and the stop switch get exercised). `--opening-demo` opens one chest of each rarity
   (screens `demo_*`) and checks that the pause menu holds an opening and that a stray Space doesn't decide a fresh
   card (it prints "Pause check ok" and "Space check ok"); `--title-demo` walks the title, settings, a new game and the
   pause menu. Moments are named in code (`Main.Moment`); the first of each is kept.
   Keep screenshot folders named `shots*` (git- and export-ignored).
4. For feel and timing, record with `--write-movie movies/x.avi --fixed-fps 30` and pull frames with ffmpeg.
5. CI (`.github/workflows/windows-build.yml`) exports Windows and runs the bot headless for four minutes; it must
   print "Autoplay finished". Wine here can't run the .exe; use the run's artifact.

## Conventions

- `scripts/Core` stays engine-free (no `using Godot`) so it runs in the simulation and in `tools/simcli`. Every pacing
  number lives in `Balance.cs` (static fields, so the tuner can turn them); don't scatter constants.
- The battle is Core (`Battle.cs`), stepped by `GameState.Update` in slices of `TickSize`; it reports `BattleEvent`s
  and `GameState` adds its own `GameEvent`s (gold, chests, bosses, stages). The screen animates events; it never
  decides outcomes. Process `Battle.Events` by index: handling one can start a wave and add more.
- Item level comes from the chest level (plus zone and kind), not the stage; gold grows more slowly per stage than
  monsters. Both stop progress from snowballing (see "Pacing" in `docs/design.md`); re-run the sim if either changes.
- `Policy` is the sensible player for the sim, the fast-forward and the bot: update it with any rule or upgrade.
- Anything added to `GameState` that should survive closing goes in `SaveData` (bump `CurrentVersion` if old saves
  can't be read).
- UI is built in code (`UiKit`, `Painter`, absolute positions on 1280x720), not .tscn files. Layout: the battle lane is
  y 0 to 420 (ground at 386, knight at x 300); gear, chest and upgrade panels below. Buttons never take focus: Space and
  Enter belong to the chest.
- Screen effects go through `Main` (`Shake`, `Flash`, `HitStop`), scaled by the Screen Shake setting. Quit through
  `Main.QuitGame` (it saves and releases sounds first), never `GetTree().Quit()`.
- Actors use `Shaders.Actor()`. In a Godot 4 canvas shader `COLOR` in `fragment()` already includes the texture: the
  shader keeps the vertex colour in a varying instead (multiplying twice darkened every sprite). The knight's palette
  swap replaces his blade colour (#8bffc7) with the weapon's rarity colour and his cape (#d46d4f, #794044) with the
  armour's (`Palette.Blade`, `Palette.Cape`).

## Art direction (pixel art)

- All art comes from CC0 packs via `tools/import_art.py` (needs Pillow); edit its mappings and re-run rather than
  hand-editing `assets/`. Only GitHub is reachable from cloud sessions, so sources must be GitHub-hosted. Credit them in
  `CREDITS.md`.
- The battle is at 2x: backdrops (640x210 bands of the pack's 640x480 pictures), actors (small stage bosses at 3x),
  effects. Actor frames come from the pack's GIFs, cropped to the union of all of an actor's animations so they stay
  aligned; `actors.json` gives each actor's foot point and facing. The effect GIFs have an opaque grey background
  (151,151,151) that the import keys out.
- Kenney Mini at multiples of 8px (`UiKit.MakeLabel` snaps sizes; min 16), ASCII only (no fallback font).
- Panels and buttons use the 9-slice frames from `UiKit`, not StyleBoxFlat.

## Known gaps

- Sounds and music were chosen by name and by analysis, not by ear.
- The Windows .exe has the default Godot icon.
- One hero, no prestige layer yet (see "Not yet" in `docs/design.md`).
