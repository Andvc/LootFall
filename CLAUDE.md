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
   Bot flags: `--seed=N --quit-at-end --days=N --bot-crowbar --no-tutorial --start-tier=N --gold=N`. `--start-tier`
   jumps to a shop tier with the tools a player would own by then (use it to check later tiers' screens).
   Keep screenshot folders named `shots*` (git- and export-ignored).
4. Shaking or physics changes: `godot --headless --path . -- --shake-test` should hear every item in every chest.
5. For feel/timing, record with `--write-movie movies/lot.avi --fixed-fps 60` and convert with ffmpeg.

## Conventions

- `scripts/Core` stays engine-free (no `using Godot`) so it can be simulated headlessly.
- Design rules (see `docs/design.md`): never show the player a computed value before purchase. Clues are
  perceptual (sounds, words, readings, glimpses) or factual (lock maker); judging them is the player's job. The
  ledger only holds what the player has learned (items, family rules, sellers' habits), never a chest's total.
- Sounds come in families that match real life (coins jingle, bottles slosh...). Within a family, differences are a
  pitch or an added layer, always the same for a variant (`Signature` in `Items.cs`). Introduce new things one at a
  time: by shop tier (`ItemDef.Tier`, `SellerDef.Tier`, `Families.Tier`) and with a Bram hint (`Hints.cs`).
- Every seller prices by a habit with a blind spot that some clue exposes (`PriceRule`); the results screen says why
  a deal went the way it did. Keep that link when adding sellers.
- UI is built in code with `UiKit` and `Painter` (absolute positions on a 1280x720 canvas), not in .tscn files.
- Buttons use `FocusMode.None`: Space is reserved for lockpicking and holding the lid. L opens the ledger.
- `Estimator` (in `Appraiser.cs`) models a reasonable player for the sim and the bot. Update it when a clue changes.
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

## Known gaps

- The Windows .exe has the default Godot icon (rcedit needs Wine; do it in CI instead).
- Wine 9 in this container can't run the exported .exe. `.github/workflows/windows-build.yml` exports and smoke-tests on a real Windows runner on every push; download builds from the run's artifacts.
- Selling is automatic at item value; there's no save game yet, and sellers don't remember you (see `docs/design.md`).
