# LootFall: notes for Claude

Solo-developed PC game for Steam (Godot 4.7.2 .NET, C#, 2D, GL Compatibility renderer). The owner knows Unity/C#
and works through cloud sessions rather than locally, so changes must be verified here, not left for them to test.

## Setup in a fresh container

Run `tools/setup-cloud.sh` (add `--export-templates` before exporting). It installs the .NET 8 SDK and Godot to
`/opt/godot` and symlinks `godot`. GitHub release downloads work through the proxy; Unity's servers do not.

## Verify every change

1. `dotnet build` must report 0 warnings and 0 errors.
2. Gameplay/balance changes: `godot --headless --path . -- --sim` and compare the report before and after. Its last
   section is the lesson test: every seller's lesson must complete when played the careful way; the one before it,
   "Bram's kin", plays each character with no talents and with all of them at challenge levels 0 to 8.
   `-- --sim=quick` (three minutes) prints the main table, the careful collector week by week, the career targets
   (careful with no talents and with all, learners, both without Heartsongs) and, for the careful player and a first
   run, the dues week by week and profit per customer at each shop, for tuning the economy.
   `-- --sim=songs` runs only the Heartsongs section (how often, each one on display from day 1 against curios, and the
   careful player with and without Heartsongs) for tuning them quickly; `-- --sim=sellers` only the per-seller table and
   the lesson test (ten seconds), for a seller's rule.
3. Visual changes: run the autoplay bot under Xvfb with `--shots=<dir>` and look at the PNGs.
   Bot flags: `--seed=N --quit-at-end --days=N --no-tutorial --start-tier=N --gold=N --start-week=N --character=KEY
   --challenge=N --talents=all --heartsongs=P --songs=all` (`--quit-at-end` quits after the first opening: use `--days`
   for longer runs; `--heartsongs=0.3` puts a Heartsong in that share of chests, `--songs=all` plays as if all nine were
   known, so `--vault-demo --songs=all` shows the other ending). `--start-tier`
   jumps to a shop tier with the tools a player would own by then (use it to check later tiers' screens);
   `--start-week` fast-forwards a run with the sim's careful collector (curios, commissions, exhibition and all; `--gold`
   then sets the purse, e.g. 0 to see a short night and foreclosure). The bot plays scenes, Nim, the Guild's night and
   deliveries, and quits when a run ends. With tips
   on, every seller's first visit is a lesson, so a guided run at a tier shows those lessons; the bot logs any lesson
   it couldn't finish. `--opening-demo` opens a crafted chest with every quality and two surprises (record it);
   `--vault-demo` opens the Crown Chest, the finale's nine-thing opening; `--engine` starts with the Bookkeeping Engine.
   `--shell-demo` walks the title screen, the character screen and talents (with a made-up profile), a new game (Hesk),
   the pause menu and the settings.
   Keep screenshot folders named `shots*` (git- and export-ignored).
4. Shaking or physics changes: `godot --headless --path . -- --shake-test` should hear everything the rules say is
   audible at each strength (and with the magnet, the lodestone in game), with no leaks.
5. For feel/timing, record with `--write-movie movies/lot.avi --fixed-fps 60` and convert with ffmpeg.

## Conventions

- The world comes first (`docs/world.md`): the Locking, the Key-mark, the Gilded Guild, the Maw, the four places. Any
  new seller, thing, event or line belongs to a place and has a reason you could tell a stranger in a tavern; check it
  against the world doc and add to it there.
- `scripts/Core` stays engine-free (no `using Godot`) so it can be simulated headlessly.
- Design rules (see `docs/design.md`): the puzzle is three rules (a thing only sounds once the shake stirs it;
  clanks drown out delicate things; sellers price only what they think is there). Never show the player what a chest
  is worth before purchase: the slip only adds up what the player ticked, and its odds bar only says how likely that
  is to beat the price (`Appraisal.ChanceAbove`); each item's real quality is rolled around the chest's colour
  (`Categories.Odds`) so openings still surprise. Clues are
  perceptual (which sounds come at which strength, what glows, what the lodestone silences, a hollow drop, what straw
  muffles) or stated, and always true (what the seller says they heard, where it came from, and what some tell you
  besides: `SellerDef.States`, Pell's ticket counting every article, Wenna's scale, Bramble's dream of the dearest thing).
  No mechanic is about whether a seller is lying: none of them do. Any new way to judge a chest must follow a rule
  people know from real life, and should be a deduction (subtract what you heard from a count or a weight, read a sign).
  The slip values things as good as the chest's colour promises (`Categories.Nominal`); sellers price by the average
  (`Categories.Expected`); `Strategy` buys when the odds bar is on its side (`Appraisal.LikelyWorth`), like a player
  would. Layers of deduction (false bottoms, counting, sound-alikes like sand and demon gold) arrive one shop at a
  time, each taught by the seller it belongs to. Rules apply every time or not at all: a hidden roll that makes a rule
  work only sometimes reads as a bug, and a rule people can't reason about from real life (v7's cloth padding) goes. Enchantment (`Item.Magic`) is something any
  thing can be: x2 value and exhibition, same quality, colour and sprite.
- Six kinds of thing a sword-and-sorcery realm would lock away (`Category`: Coins, Arms, Garb, Potions, Scrolls, Gems,
  shown as Coin, Weapons, Cloaks, Potions, Spells, Gems; the fakes are demon gold, paste gems and scroll cases of sand).
  Every thing is a find worth having (no letters, no rusty daggers); rename one in `Categories.Renamed` too, so saves'
  journals carry over. One sound per kind of thing, the one you'd expect in real life (`CategoryDef` in `Items.cs`; alternatives for each
  kind, chosen in Settings > Item sounds, are in `Audio.SoundSets`: play `SoundSets.Group(kind)`); kinds differ in how
  hard you must shake to hear them (`StirsAt`) and whether they drown out or get drowned out (`Loud`, `Delicate`).
  Introduce new things one at a time: by shop tier (`CategoryDef.Tier`, `SellerDef.Tier`) and with a Bram hint
  (`Hints.cs`).
- A seller's first visit (with tips on) is a lesson (`Lessons.cs`): a chest that guarantees what it teaches, and
  steps whose `Done` checks run every frame on the hearing, the slip and the visit. Keep checks to things the player
  does, so they can't miss; the sim's lesson test plays each one. A lesson waits for tips already up
  (`MentorView.ShowLesson`), `CounterPhase.Teach` holds other tips back during one, and `Lesson.Covers` lists the tips
  it makes redundant. Read a guided `--autoplay` run's `[Bram ...]` lines to check the tutorial reads in order.
- Heartsongs (`Heartsongs.cs`, `Category.Heartsong`, `Rarity.Mythic`, red): nine collectibles, about one run in two, in any
  seller's chest (`Heartsongs.Chance`, own dice, rolled in `GameState.Sing` after the seller's priced it: no seller ever
  hears one, `SellerDef.Notices`). Each sings its own synth song (`Sfx` "song_{key}"), at the gentlest shake, never
  drowned or stilled; priced as a trinket as fine as its chest (`Quality` holds the chest colour); displayed without a
  slot at any shop ("Highly recommended"), never pawned; each does one big thing on display (`GameState.Shows`): they're
  the rarest things in Aldmark and each should be worth a run built around it (`--sim=songs` shows what each is worth
  from day 1; none shows what a chest is worth). Luck (`Chest.Improve`) never touches one.
  The profile's songbook (`Profile.Songs`, merged into `GameState.Songbook`) changes the Crown Chest's ending with all
  nine (`Story.VaultEpilogue`). Iterate `Categories.Kinds` (not `All`) where the six ordinary kinds are meant.
- Synergies (`Synergies.cs`) come from what's been acquired (ten of a kind at a quality per level) or achieved; only
  active ones (one slot, more with upgrades) do anything, chosen at the end of a day. Each is a real power: its effect
  and 10% a level on what its kind sells for (`Synergies.SaleBonus`).
- The economy grows fast and the Guild's dues grow faster (v18: the old tight constraints made upgrades feel optional).
  Qualities are worth 1/3/10/30/100 times a common (`Categories.QualityMultiplier`); each move inward multiplies what a
  customer is worth several times over (the sim's "profit per customer at each shop"); buyers' trades (`Upgrade.Sales`:
  scales, auction block, royal warrant) multiply what everything sells for, and sellers ask that much more of a shop known
  to sell high (`GameState.Buyers` in `Boosts.Dearer`), so a trade multiplies your edge and never makes buying blind pay;
  flat sums (curios' fees, door money) grow with `ShopTiers.Scale`. The careful player saves for the next move before
  anything else (`Strategy.Saving`). The Bookkeeping Engine (`Upgrades.Engine`) is the Guild's: offered once
  `CanPayOff`, for exactly `DebtLeft` (`GameState.Cost`), it marks every chest's real contents on the slip as it comes
  in (`Appraisal.Recorded`, `CounterPhase.Record`), so a won run can be finished fast; the sim's careful player pays off
  instead.
- The run (`Run.cs`, a partial of `GameState`): five-day weeks; the Gilded Guild's tithe every fifth evening (`Guild.Due`,
  eight weeks then endless x1.5); pawning, Bram's word once a run, foreclosure. Curios (`Curios.cs`, from Nim weekly)
  each bend one rule and never reveal what's in a chest, and are priced as a share of the week's due (commons 1g on
  Nim's first visit); commissions (`Commissions.cs`) pay for delivering finds;
  one event a week (`Events.cs`); the story's scenes and the shards of the Aldric Key that open the Crown Chest
  (`Story.cs`; in code still `MapPieces` and the vault, so saves carry over). Balance is set for a career, not
  a run (the sim's "Learners" and "Careers" sections; learners are the careful player with mistakes that shrink as
  `Style.Skill` goes from 0 to 1): a first-timer gets through the first weeks and loses in the middle ones; better
  judgment and talents each push that later; only the two together clear the debt more often than not (v18: the careful
  player with no talents a little under half the time, with every talent three in four; a first run one in ten, nearly
  always on a Heartsong's luck); careers first clear around the sixth run, open level 4 around the seventeenth, and
  level 8 stays a wall. `--sim=kin [--who=KEY]` runs just the characters' table, for tuning one. Careless styles (buying blind, one shake)
  are foreclosed within a few weeks, and no early week sends careful players to Bram's word. Anything added to the run goes in `SaveData` too; the profile across runs is
  `Profile` (user://profile.json). Bram's stash (`GameState.Stash`, three times a run) tops up a day that starts
  nearly broke. Bram's working purse (`ShopTiers.Working`) is both the day-end advice (orange prices) and what
  `Strategy.Reserve` keeps, so the advice is what the sim's careful player does: tune them together. The whole debt
  can be paid off early (`GameState.DebtLeft`, `PayOff`) from the day end or the Guild's night; the sim's careful
  player does, and reports the week each run cleared (a strong run takes off and clears in weeks 6-7; week 8 is the wall
  for the rest). Surprises (`Categories.Odds`, about one thing in eight a step better) are worth a quarter of what the
  chests hold, with a pity counter (`GameState.DrySpell`); the sim reports their share of income.
- Across runs (`Meta.cs` for the definitions and the score, `Kin.cs` for what they do to a run): four of Bram's kin
  (`Characters`), each changing one part of how chests are judged or the shop is run and paying for it (Hesk: no hard
  shake or drop, a scale; Rosa: everybody haggles, walkouts stay away a week; Isolde: a shelf and door money from day
  one, no pawning her collection); eight cumulative challenge levels (`Challenges`, opened per character by clearing
  at the level below, a quarter more renown each; order them by what each term does alone, the sim's `Trial`); and
  13 talents (`Talents`) bought with renown (`Score` times the level's multiplier, banked in `Main.EndRun` or when a
  saved run is abandoned). Talents and characters add no share to sale prices and take none off dues (the shop's growth
  is upgrades', synergies' and Heartsongs'); check the "Bram's kin" table that a full tree doesn't move the median clear
  day much. Bram's lessons are
  Kit's (`GameState.GivesLessons`); a new character gets a `kin_*` tip. Route seller traits through
  `GameState.MindsDrop`/`MindsMagnet`/`MindsHardShake`/`Impatient` and upgrade prices through `GameState.Cost`, so the
  terms and talents apply everywhere. The profile (`Profile`) keeps renown, talents and each character's open levels.
- The shop opens up a step at a time, moving inward (`ShopTiers.Name`: Bram's Stall, Thornwick Market, Crownhold, the
  Royal Emporium; `GameState.UpgradesOpen` once it has earned `UpgradesAt`, `DisplayOpen` with Thornwick,
  `SynergiesOpen` with Crownhold), each introduced by a Bram tip when it happens. Gate new
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
  places their chests come from (`From`, `Origins.cs`: the place decides what a chest tends to hold) with a story for
  each (`Stories`, always true), and maybe one rule of their own that changes what you can do, what you're told or what
  the deal is (`Patience`, `HatesDrop`, `HatesMagnet`, `HatesHardShake`, `ShakeLimit`, `ShakeFee`, `Packs`, `Packhorse`,
  `States`, `Wager`, `Heirloom`, `TakesDearest`, `SlipPrice`, `Gaze`), worded under the chest by `GameState.Terms` (and
  `Visit.Clue` for what they state). `Strategy.Reckon` is the careful player's read of a stated clue.
  `Visits` sets how often they come (the Maw rarely). `Market` rolls and prices their chests; `Visit` trades, and its
  `Explain` says on the results and pass screens why a deal went the way it did;
  `Shop.Buy` takes a seller's claim and `Shop.Settle` a wager or the King's game. A new rule needs the sim, the bot
  (`Strategy.Levels`, `Worth`, `WorthBuying`) and a lesson. The sim's per-seller table must show a careful player
  profits from each, and buying blind shouldn't (Tam, who takes a few coins, and the Maw, whose coin costs the rest of
  the day, are the exceptions). Stories name what was heard (`{heard}`, `{count}`, `{heft}`, `{colour}`, `{magic}`,
  `{articles}`, `{pounds}`, `{dream}`, `{price}`, `{fee}`) and must fit the speech bubble (about 170 characters with the
  price filled in).
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

- The art is a placeholder until dedicated art replaces it (the owner found it underwhelming), drawn in code: things,
  Heartsongs (`HEARTSONGS`), portraits, chest, curios, backdrops and effects in
  `tools/custom_art.py` (materials with a shadow/mid/light ramp, lit from the top left, a 1px dark outline; things
  24px, portraits 38px over their place's backdrop, curios 24px), frames, icons and shop architecture in
  `tools/import_art.py`, which builds `assets/` from both (needs Pillow). Draw or edit there and re-run rather than
  hand-editing `assets/`; a new seller, thing or curio needs its drawing (the importer writes what the dicts name).
  Sounds and the font are Kenney's CC0 packs (only GitHub is reachable from cloud sessions, so new sources must be
  GitHub-hosted); credit them in `CREDITS.md`.
- Keep pixels square: integer scales only (chest 12-14x, items 1-2x, backgrounds 2x, UI frames 3x), nearest
  filtering (project default), and Kenney Mini at multiples of 8px (`UiKit.MakeLabel` snaps sizes; min 16).
- Kenney Mini has no fallback font: stick to ASCII in UI strings.
- Panels and buttons use the 9-slice frames from `UiKit` (`MakePanel`, `MakeFrame`, `MakeButton`), not StyleBoxFlat.
- Item sprites are at most 24px (trimmed to what's drawn). To show a particular thing in a slot (collection, shelf,
  exhibition, chooser) use `UiKit.MakeItemIcon`, its own sprite from `assets/sprites/items/icons/`, so it looks the
  same everywhere. Kind icons (`UiKit.MakeKindIcon`, 16x16 art like the tool icons)
  stand for a kind, not a thing. Tool icons are 16px art at 3x; draw them at 2/3 scale for exact 2x.

## Known gaps

- The Windows .exe has the default Godot icon (rcedit needs Wine; do it in CI instead).
- Wine 9 in this container can't run the exported .exe. `.github/workflows/windows-build.yml` exports and smoke-tests on a real Windows runner on every push; download builds from the run's artifacts.
- The shop saves at the start of each day only (leaving mid-day replays that day), and sellers don't remember you
  (see `docs/design.md`).
