# LootFall: notes for Claude

Solo-developed PC game for Steam (Godot 4.7.2 .NET, C#, 2D, GL Compatibility renderer). The owner knows Unity/C#
and works through cloud sessions rather than locally, so changes must be verified here, not left for them to test.

## Setup in a fresh container

Run `tools/setup-cloud.sh` (add `--export-templates` before exporting). It installs the .NET 8 SDK and Godot to
`/opt/godot` and symlinks `godot`. GitHub release downloads work through the proxy; Unity's servers do not.

## Verify every change

1. `dotnet build` must report 0 warnings and 0 errors.
2. Gameplay/balance changes: `godot --headless --path . -- --sim` and compare the report before and after. Its last
   section is the lesson test: every seller's lesson must complete when played the careful way.
3. Visual changes: run the autoplay bot under Xvfb with `--shots=<dir>` and look at the PNGs.
   Bot flags: `--seed=N --quit-at-end --days=N --no-tutorial --start-tier=N --gold=N --start-week=N`. `--start-tier`
   jumps to a shop tier with the tools a player would own by then (use it to check later tiers' screens);
   `--start-week` fast-forwards a run with the sim's careful collector (curios, commissions, exhibition and all; `--gold`
   then sets the purse, e.g. 0 to see a short night and foreclosure). The bot plays scenes, Nim, the Guild's night and
   deliveries, and quits when a run ends. With tips
   on, every seller's first visit is a lesson, so a guided run at a tier shows those lessons; the bot logs any lesson
   it couldn't finish. `--opening-demo` opens a crafted chest with every quality and two surprises (record it);
   `--vault-demo` opens King Aldric's vault, the finale's nine-thing opening.
   `--shell-demo` walks the title screen, a new game, the pause menu and the settings.
   Keep screenshot folders named `shots*` (git- and export-ignored).
4. Shaking or physics changes: `godot --headless --path . -- --shake-test` should hear everything the rules say is
   audible at each strength (and with the magnet), with no leaks.
5. For feel/timing, record with `--write-movie movies/lot.avi --fixed-fps 60` and convert with ffmpeg.

## Conventions

- `scripts/Core` stays engine-free (no `using Godot`) so it can be simulated headlessly.
- Design rules (see `docs/design.md`): the puzzle is three rules (a thing only sounds once the shake stirs it;
  clanks drown out delicate things; sellers price only what they think is there). Never show the player what a chest
  is worth before purchase: the slip only adds up what the player ticked, and its odds bar only says how likely that
  is to beat the price (`Appraisal.ChanceAbove`); each item's real quality is rolled around the chest's colour
  (`Categories.Odds`) so openings still surprise. Clues are
  perceptual (which sounds come at which strength, what glows, what the magnet silences, a hollow drop) or stated
  (what the seller says they heard). Any new way to judge a chest must follow a rule people know from real life.
  The slip values things as good as the chest's colour promises (`Categories.Nominal`); sellers price by the average
  (`Categories.Expected`); `Strategy` buys when the odds bar is on its side (`Appraisal.LikelyWorth`), like a player
  would. Layers of deduction (false bottoms, counting, sound-alikes like sand and gilded iron) arrive one shop at a
  time, each taught by the seller it belongs to. Rules apply every time or not at all: a hidden roll that makes a rule
  work only sometimes reads as a bug, and a rule people can't reason about from real life (v7's cloth padding) goes. Enchantment (`Item.Magic`) is something any
  thing can be: x2 value and exhibition, same quality, colour and sprite.
- One sound per kind of thing, the one you'd expect in real life (`CategoryDef` in `Items.cs`; alternatives for each
  kind, chosen in Settings > Item sounds, are in `Audio.SoundSets`: play `SoundSets.Group(kind)`); kinds differ in how
  hard you must shake to hear them (`StirsAt`) and whether they drown out or get drowned out (`Loud`, `Delicate`).
  Introduce new things one at a time: by shop tier (`CategoryDef.Tier`, `SellerDef.Tier`) and with a Bram hint
  (`Hints.cs`).
- A seller's first visit (with tips on) is a lesson (`Lessons.cs`): a chest that guarantees what it teaches, and
  steps whose `Done` checks run every frame on the hearing, the slip and the visit. Keep checks to things the player
  does, so they can't miss; the sim's lesson test plays each one. A lesson waits for tips already up
  (`MentorView.ShowLesson`), `CounterPhase.Teach` holds other tips back during one, and `Lesson.Covers` lists the tips
  it makes redundant. Read a guided `--autoplay` run's `[Bram ...]` lines to check the tutorial reads in order.
- Synergies (`Synergies.cs`) come from what's been acquired (ten of a kind at a quality per level) or achieved; only
  active ones (one slot, more with upgrades) do anything, chosen at the end of a day.
- The run (`Run.cs`, a partial of `GameState`): five-day weeks; the Grey Guild's due every fifth evening (`Guild.Due`,
  eight weeks then endless x1.5); pawning, Bram's word once a run, foreclosure. Curios (`Curios.cs`, from Nim weekly)
  each bend one rule, never reveal what's in a chest and never add a share to sale prices (nor do synergies: a few
  percent on every sale compounds into weeks; the sim's "what curios and synergies are worth" lines show it), and are
  priced as a share of the week's due (commons 1g on Nim's first visit); commissions (`Commissions.cs`) pay for delivering finds;
  one event a week (`Events.cs`); the story's scenes and the map to the vault (`Story.cs`). The sim's report must show
  careful players usually clear the debt (week 8 the real wall), careless ones foreclosed within a few weeks, and early
  foreclosures rare for careful play. Anything added to the run goes in `SaveData` too; the profile across runs is
  `Profile` (user://profile.json). Bram's stash (`GameState.Stash`, three times a run) tops up a day that starts
  nearly broke. Bram's working purse (`ShopTiers.Working`) is both the day-end advice (orange prices) and what
  `Strategy.Reserve` keeps, so the advice is what the sim's careful player does: tune them together. The whole debt
  can be paid off early (`GameState.DebtLeft`, `PayOff`) from the day end or the Guild's night; the sim's careful
  player does, and reports the week each run cleared (week 8 should stay the usual one). Surprises
  (`Categories.Odds`) are rare, with a pity counter (`GameState.DrySpell`); the sim reports their share of income.
- The shop opens up a step at a time (`GameState.UpgradesOpen` once it has earned `UpgradesAt`, `DisplayOpen` with
  the village shop, `SynergiesOpen` with the town shop), each introduced by a Bram tip when it happens. Gate new
  systems the same way, in Core, so the sim and the bot follow them.
- Each shop tier has its own picture (`assets/backgrounds/shop{tier}.png`, drawn by `build_shops` in
  `tools/import_art.py`, shown with `UiKit.MakeShop`); keep the counter top at y 476 and the shelf at y 196.
- The game opens on `TitlePhase`; `Main` saves `GameState.Save()` to user://save.json at the start of each day (not
  for the bot or debugging flags) and Esc opens `PauseMenu`. A day: `Main.StartDay` (the week's scene, Nim), the
  counter, `DayEndPhase`, then `Main.CloseDay` (Vane's introduction the first evening, `DuesPhase` on the fifth). `Settings` (user://settings.cfg) scales screen shake and
  flashes: route new screen effects through `Main.Shake`/`Flash`. Anything added to `GameState` that should survive
  a day boundary goes in `SaveData` too.
- Every seller has one way of judging a chest (`Judgment`: listening, counting rattles, weight, colour, a dream, a
  trade they know, a sense for magic, no idea) with a blind spot a careful player can see through, one way to trade,
  and maybe one thing they won't stand for, with a reward for putting up with it (`SellerDef` traits like `Patience`,
  `HatesDrop`, `HatesMagnet`, `HatesHardShake`). `Market` rolls and prices their chests; `Visit` trades, and its
  `Explain` says on the results and pass screens why a deal went the way it did. The sim's per-seller table must show
  a careful player profits from each, and buying blind shouldn't (Tam, who takes a few coins, is the exception).
- `ShakeBox` gates sounds with the same rules as `Chest.Audible`: change both together (the shake test checks them).
- UI is built in code with `UiKit` and `Painter` (absolute positions on a 1280x720 canvas), not in .tscn files.
- Buttons use `FocusMode.None`: Space is reserved for lockpicking and holding the chest open. L opens the ledger.
- `Hearing.Read` and `Strategy` model a careful player (what they hear, when they drop or use the magnet, what they
  offer and keep) for the sim and the bot. Update them when a rule, tool or trade changes.
- Phases are swapped by `Main.SwitchTo`; check `Alive` after every `await` in a phase. Quit through `Main.QuitGame`
  (it frees the phase and bot and releases the sounds first), never `GetTree().Quit()`: long runs crashed on exit
  otherwise, and short ones leaked.
- Screen effects go through `Main` (`Shake`, `Flash`, `HitStop`, `Zoom`); capture points use `Main.Moment(name)`.
- Godot C# naming differs from GDScript in places (e.g. `CpuParticles2D`, `AudioStreamWav`).

## Art direction (pixel art)

- All art comes from CC0 packs via `tools/import_art.py` (needs Pillow); edit its mappings and re-run rather than
  hand-editing `assets/`. Only GitHub is reachable from cloud sessions, so new sources must be GitHub-hosted.
  Credit sources in `CREDITS.md`.
- Keep pixels square: integer scales only (chest 12-14x, items 1-2x, backgrounds 2x, UI frames 3x), nearest
  filtering (project default), and Kenney Mini at multiples of 8px (`UiKit.MakeLabel` snaps sizes; min 16).
- Kenney Mini has no fallback font: stick to ASCII in UI strings.
- Panels and buttons use the 9-slice frames from `UiKit` (`MakePanel`, `MakeFrame`, `MakeButton`), not StyleBoxFlat.
- Item sprites vary in size (up to 89px). To show a particular thing in a slot (collection, shelf, exhibition,
  chooser) use `UiKit.MakeItemIcon`, its own sprite from `assets/sprites/items/icons/` (halved by `tools/import_art.py`
  when over 48px), so it looks the same everywhere. Kind icons (`UiKit.MakeKindIcon`, 16x16 art like the tool icons)
  stand for a kind, not a thing. Tool icons are 16px art at 3x; draw them at 2/3 scale for exact 2x.

## Known gaps

- The Windows .exe has the default Godot icon (rcedit needs Wine; do it in CI instead).
- Wine 9 in this container can't run the exported .exe. `.github/workflows/windows-build.yml` exports and smoke-tests on a real Windows runner on every push; download builds from the run's artifacts.
- The shop saves at the start of each day only (leaving mid-day replays that day), and sellers don't remember you
  (see `docs/design.md`).
