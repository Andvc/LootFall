# LootFall: notes for Claude

Solo-developed PC game (Godot 4.7.2 .NET, C#, 2D, GL Compatibility renderer): an incremental loot game built for
thousands of hours of play.
- A knight fights ever deeper on his own, and each depth's drops fill a chest.
- Chests open through twelve rarities into gear for 586 kinds of piece on 545 slots, from the crown of his head to the
  tips of his toes, with pieces that clash and pieces with slots of their own. Auto-equip wears the strongest whole set.
- Forge, ranks, mastery, a 3,259-node talent tree, a codex and two prestige layers (ascension, transcendence) make him
  stronger.

The owner knows Unity/C# and works through cloud sessions rather than locally, so changes must be verified here, not
left for them to test. `main` is this game. The shake-and-deduce shop that once shared this repository is now its own
game, Keywright, in Andvc/keywright. The two share nothing but the toolchain, and each keeps its files in its own
`app_userdata` folder.

Read `docs/design.md` (the game), `docs/equipment.md` (the body, pieces, boards, auto-equip and the equipment screen),
`docs/progression.md` (the numbers) and `docs/art-direction.md` (the look, the screen and the asset pipeline) before
changing any of them.

## Setup in a fresh container

Run `tools/setup-cloud.sh` (add `--export-templates` before exporting). It installs the .NET 8 SDK and Godot to
`/opt/godot` and symlinks `godot`. After changing `assets/` run `godot --headless --path . --import` once.

The Python art tools need Pillow and numpy. `tools/placeholders.py` also needs rembg and cairosvg (`pip install`).

## Verify every change

1. `dotnet build` must report 0 warnings and 0 errors.
2. Numbers or rules: `dotnet build tools/progsim -c Release`, then:
   - **Balance is on hold.** The owner has suspended numerical balance validation and regression testing while the
     systems are redesigned. `progsim --check`, `--sweep` and `--content-sweep` aren't expected to pass, and CI
     (`.github/workflows/progression.yml`) runs them only when started by hand. Don't tune numbers to make them pass
     until the owner says balance is back.
   - Equipment (`Anatomy`, `Loadout`, wearing): `... progsim.dll --anatomy` must end "300 of 300 random problems match
     the brute force" with nothing fallen back. Add `--play=2` to see a sensible player's set after two hours, or use
     `--debug-wear` for why the first chest's pieces do or don't go on.
   - `... progsim.dll --game --hours=1` should print "All game checks pass" (about 10 s).

   The game check confirms that:
   - the session keeps up with the simulated player and every chest goes somewhere;
   - the battle's bosses and leaps add up to the depths cleared, every monster that comes on falls, and the hero never
     does;
   - saves round-trip and time away pays.

   Other progsim flags:
   - `--profile=active|idle|daily|no-prestige --hours=N` prints one player's report;
   - `--set=Name=value` tries a parameter;
   - `--tune` refits the open-ended systems' knobs;
   - `--sweep` and `--content-sweep` rerun the check with each knob changed and with more or less content.
3. Visual changes: run the bot under Xvfb with `--shots=<dir>` and look at the PNGs
   (`xvfb-run -a godot --path . --rendering-driver opengl3 -- --autoplay --minutes=2 --shots=$PWD/shots`).
   - **Flags:** `--seed=N`, `--hours=N` (start where a sensible player would be after N hours, via `Main.FastForward`
     and `SensiblePlayer`), `--speed=N`, `--minutes=N`.
   - **Demos** (`--demo=NAME`, `scripts/View/Demos.cs`):
     - `opening`: a chest of each of the twelve tiers (screens `reveal_*`);
     - `title`: the title, settings, a new game and the pause menu;
     - `welcome`: a save round trip and two hours away (prints "Save round trip ok");
     - `screens`: the lane, the equipment screen (the body, each region, a chosen slot, a piece and its board), talents,
       codex, prestige and settings (add `--hours=2` or more).
   - **Moments** are named in code (`Main.Moment`) and captured the frame after the call; the first of each is kept. A
     demo must wait before closing what it captured.
   - Keep screenshot folders named `shots*` (git- and export-ignored).
4. For feel and timing, record with `--write-movie movies/x.avi --fixed-fps 30` and pull frames with ffmpeg.
5. CI (`.github/workflows/windows-build.yml`) exports Windows and runs the bot headless for four minutes; it must print
   "Autoplay finished". Wine here can't run the .exe; use the run's artifact.
6. New content (a realm, a monster, a piece or item family): run `python3 tools/art.py manifest` so
   `art/manifest.json` lists it. Give it a stand-in in `tools/placeholders.py`, or the game falls back (an icon to
   "unknown"). Pieces are added in `Anatomy.cs` (docs/equipment.md, "Adding a piece").

## Conventions

**Layers**
- Three layers, and the first two stay engine-free (no `using Godot`), so `tools/progsim` runs them:
  - **`scripts/Progression`, the numbers.** Every number is in `Params.cs`, and power is measured in LE (log base R of a
    multiplier). Keep the loop gain and the prestige feedback (`Growth.cs`) well under 1. Anything per piece that grows
    without bound goes into the pool (ranks, mastery), never onto a piece's flat stats. New content goes through the
    budgets (`ReferenceSlots`, `TalentBudgetLE`). Update the simulated player (`Sim.cs`, `Strategy`) with any new system
    or rule.
  - **The equipment** (`Anatomy.cs`, `Loadout.cs`; docs/equipment.md). A cell (a "slot" on screen) is one layer of one
    site, or one of a piece's own slots. A piece (`SlotDef`, the framework's old "slot") fills cells (`Takes`) and keeps
    others free (`Keeps`); two pieces clash when one fills what the other fills or keeps free. A piece with slots of its
    own brings a board and opens some of its cells. Pieces that never go together may share a board, and validation
    checks this. `Hero.Worn` is what's put on; `Hero.Active` is what counts (worn, open, with an item, its host chain
    counting). Only Active pieces add stats or mastery. Change what's worn through `Session.Wear`, `TakeOff` and
    `WearBest`, or `Strategy.Wear`, which runs the exact loadout search.
  - **`scripts/Game`, the game around the numbers.** `Session` holds the hero's world, the chest pile, the automation,
    the totals and every action the player can take. The screen, the bot and the tests all call the same actions.
    `Stage` turns the closed-form fight into what's shown. `SensiblePlayer` plays for the game test and the
    fast-forward; update it with any new system.
  - **`scripts/View`, the presentation.** It animates what the session reports (`Session.Events`, `Stage.Cues`) and
    never decides an outcome.
- Anything added to `Session` or `Hero` that should survive closing goes in `SaveData` (`scripts/Game/SaveGame.cs`).
  Bump `CurrentVersion` if old saves can't be read.
- If the battle's choreography changes, keep `GameTest.StageCheck` passing. Bosses down plus leaps must equal the depths
  cleared, every foe that comes on falls, and the hero's health stays above its floor.

**Content and names**
- Content keys tie the code to the art:
  - monster keys (`Bestiary.cs`) to `assets/monsters/<key>.png` and `monsters.json` (foot points);
  - realm keys to `assets/backdrops/<realm>.jpg` and `realms.json`;
  - item families (`Describe.Family`) to `assets/icons/<family>.png`, with era tiers as `<family>_t2` to `_t5`.
- Every number on screen goes through `Format` (the player's notation). Rarity names, numerals and colours come from
  `Look` (`Tier`, `TierText`, `TierFace`, `TierName`, `Numeral`); never hard-code a rarity colour.

**UI and rendering**
- UI is built in code (`Ui`, `Painter`, `Canvas`, absolute positions on a 1920x1080 canvas), not in .tscn files. Layout
  constants and design tokens (colours, fonts, sizes) are in `Look.cs`. The lane is y 0 to 640 (ground at 575, the
  knight at x 480, monsters engaging at x 760 and x 940); the panels are below it.
- `GameScreen` puts the lane on canvas layer 0, the UI on 1, flying effects on 2 and overlays on 3. A full-screen
  overlay hides the lane. Overlays derive from `Overlay`.
- Buttons never take focus: Space and Enter belong to the chests. Esc calls the top overlay's `Back()` (which closes it,
  or steps out a level in the equipment screen) or opens the pause menu.
- GL Compatibility drops draw commands past `rendering/gl_compatibility/item_buffer_size` (65536) without an error:
  - batch lines with `DrawMultiline` and many shapes into a mesh;
  - render heavy canvases in a SubViewport (the talent map does);
  - hide what's covered.
- `GetTree()` is null in a constructor: start work in `_Ready`, and create an overlay's fade tween in `_EnterTree`.
- On-screen budget: at most 12 monsters and 10 floating numbers, whatever the power level.
- Screen effects go through `Main` (`Shake`, `Flash`, `HitStop`), scaled by the settings. Hit-stop freezes only the
  attacker and the target, and flashes stay under 35%. Quit through `Main.QuitGame` (it saves and releases sounds
  first), never `GetTree().Quit()`.

## Art direction

- **The style.** `docs/art-direction.md` is the spec: inked cel 2D over muted painted backdrops.
  - One ink outline (`#15101f`), flat three-step ramps, light from the top left, chunky silhouettes.
  - Backdrops stay desaturated and low in contrast, so the foreground reads.
  - Code draws everything that means something: rarity frames and colours, beams, glows, motion, numbers. Art is
    neutral and static.
- **The knight** is a code-drawn rig (`HeroRig.cs`) whose look follows his gear (`GearLook`). The equipment screen's
  paper doll (`PaperDoll`, `DollPieces`, geometry in `Figure`) draws every piece that counts in code, at any zoom. A
  new family gets the generic drawing for its layer until it has its own.
- **Monsters** are single pictures moved by body plan (`MonsterActor.cs`).
- **Final art** will be AI-generated and comes in through the pipeline:
  - `tools/art.py manifest` lists every asset with its sizes and prompt;
  - `tools/art.py import` mattes, flattens, outlines, checks and places new art from `art/incoming/`.
- **Stand-ins** come from GitHub-hosted packs via `tools/placeholders.py`. Only GitHub is reachable from cloud
  sessions. Edit its mappings and re-run rather than hand-editing `assets/`, and credit sources in `CREDITS.md`.
  Share-alike and copyleft stand-ins are for development builds only.
- **Textures** use linear filtering with mipmaps (the project default). Art is authored at twice its size on screen.
- **Fonts**: Cinzel for titles, Barlow Semi Condensed for the interface (tabular figures for numbers that change), and
  Lilita One for floating numbers and banners.

## Known gaps

- The art is stand-in. The knight is drawn from shapes in code, not from a parts sheet.
- Sounds and music were chosen by name and by analysis, not by ear. Music doesn't change by realm.
- The Windows .exe has the default Godot icon.
- There is one hero, no challenges and no third prestige layer (see "Not yet" in `docs/design.md`).
