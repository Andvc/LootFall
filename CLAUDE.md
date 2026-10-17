# LootFall: notes for Claude

Solo-developed PC game for Steam (Godot 4.7.2 .NET, C#, 2D, GL Compatibility renderer). The owner knows Unity/C#
and works through cloud sessions rather than locally, so changes must be verified here, not left for them to test.

## Setup in a fresh container

Run `tools/setup-cloud.sh` (add `--export-templates` before exporting). It installs the .NET 8 SDK and Godot to
`/opt/godot` and symlinks `godot`. GitHub release downloads work through the proxy; Unity's servers do not.

## Verify every change

1. `dotnet build` must report 0 warnings and 0 errors.
2. Gameplay/balance changes: `godot --headless --path . -- --sim` and compare the report before and after.
3. Visual changes: run the autoplay bot under Xvfb with `--shots=<dir>` and look at the PNGs.
   Bot flags: `--seed=N --quit-at-end --days=N --no-tutorial --start-tier=N --gold=N`. `--start-tier`
   jumps to a shop tier with the tools a player would own by then (use it to check later tiers' screens).
   Keep screenshot folders named `shots*` (git- and export-ignored).
4. Shaking or physics changes: `godot --headless --path . -- --shake-test` should hear everything the rules say is
   audible at each strength (and with the magnet), with no leaks.
5. For feel/timing, record with `--write-movie movies/lot.avi --fixed-fps 60` and convert with ffmpeg.

## Conventions

- `scripts/Core` stays engine-free (no `using Godot`) so it can be simulated headlessly.
- Design rules (see `docs/design.md`): the whole puzzle is three rules (a thing only sounds once the shake stirs it;
  clanks drown out delicate things; sellers price only what they heard at their own shake). Never show the player
  what a chest is worth before purchase: the slip only adds up what the player ticked. Clues are perceptual (which
  sounds come at which strength, what glows, what the magnet silences) or stated (what the seller says they heard).
- One sound per kind of thing, the one you'd expect in real life (`CategoryDef` in `Items.cs`); kinds differ in how
  hard you must shake to hear them (`StirsAt`) and whether they drown out or get drowned out (`Loud`, `Delicate`).
  Introduce new things one at a time: by shop tier (`CategoryDef.Tier`, `SellerDef.Tier`) and with a Bram hint
  (`Hints.cs`).
- Every seller shakes at one strength and prices what they heard (`SellerDef.Notices`); the pitch says how hard and
  what. Profit is what they missed, and the results and pass screens say why (`Visit.Explain`). Keep that link when
  adding sellers.
- `ShakeBox` gates sounds with the same rules as `Chest.Audible`: change both together (the shake test checks them).
- UI is built in code with `UiKit` and `Painter` (absolute positions on a 1280x720 canvas), not in .tscn files.
- Buttons use `FocusMode.None`: Space is reserved for holding the chest open. L opens the ledger.
- `Hearing.Read` models a careful player for the sim and the bot. Update it when a rule or tool changes.
- Phases are swapped by `Main.SwitchTo`; check `Alive` after every `await` in a phase.
- Screen effects go through `Main` (`Shake`, `Flash`, `HitStop`); capture points use `Main.Moment(name)`.
- Godot C# naming differs from GDScript in places (e.g. `CpuParticles2D`, `AudioStreamWav`).

## Art direction (pixel art)

- All art comes from CC0 packs via `tools/import_art.py` (needs Pillow); edit its mappings and re-run rather than
  hand-editing `assets/`. Only GitHub is reachable from cloud sessions, so new sources must be GitHub-hosted.
  Credit sources in `CREDITS.md`.
- Keep pixels square: integer scales only (chest 12-14x, items 1-2x, backgrounds 2x, UI frames 3x), nearest
  filtering (project default), and Kenney Mini at multiples of 8px (`UiKit.MakeLabel` snaps sizes; min 16).
- Kenney Mini has no fallback font: stick to ASCII in UI strings.
- Panels and buttons use the 9-slice frames from `UiKit` (`MakePanel`, `MakeFrame`, `MakeButton`), not StyleBoxFlat.
- Item sprites vary in size (up to 72px); in 32px slots use the kind icons (`UiKit.MakeKindIcon`), drawn as 16x16
  pixel art in `tools/import_art.py` like the tool icons.

## Known gaps

- The Windows .exe has the default Godot icon (rcedit needs Wine; do it in CI instead).
- Wine 9 in this container can't run the exported .exe. `.github/workflows/windows-build.yml` exports and smoke-tests on a real Windows runner on every push; download builds from the run's artifacts.
- Selling is automatic at item value; there's no save game yet, and sellers don't remember you (see `docs/design.md`).
