# LootFall design notes

## The game in one paragraph

A knight fights ever deeper, depth after depth, through eight realms that come round again harder each time. Every
depth's monsters and its boss fill a chest. Open it and the haul climbs through twelve rarities, from Common to
Transcendent, before the cards turn over. Whatever beats what the knight wears goes on by itself, across 149 slots from
helm to toe rings, tattoos, companions and celestial slots. Gold forges the slots, shards from everything else rank them,
and levels pay talent points into a tree of 3,259 nodes. First finds fill the codex, and its power with them. When a run
slows, ascension trades it for aether, which buys power for good; later, transcendence trades the ascension tree for
stardust. The game is built to keep a player progressing for thousands of hours, and it shows that growth as speed and
reach rather than as clutter.

## Where things live

| Layer | Folder | What it decides |
|---|---|---|
| The numbers | `scripts/Progression` ([progression.md](progression.md)) | Everything that's balanced: what a depth needs, kill times, drops and rarities, costs, levels, talents, the codex, both prestige layers. Closed-form and engine-free; `tools/progsim --check` simulates 3,000 hours for three kinds of player and checks the result. |
| The game | `scripts/Game` | The session around the numbers: chests and the pile, the battle's choreography (`Stage`), the bestiary, automation, saving, time away, names and number formats. Engine-free; `tools/progsim --game` plays it. |
| The screen | `scripts/View` | What the player sees and hears ([art-direction.md](art-direction.md)). It animates what the game reports and calls the session's actions. It never decides an outcome. |

`Main` (`scripts/Main.cs`) owns the session, saving, sound and the screen-wide effects. `Settings` keeps the player's
choices in user://settings.cfg.

## The loop

1. **The knight fights.** Two monsters at a time walk in from the right, wave after wave, then the depth's boss.
   Beat it in 30 seconds and the next depth starts. Every tenth depth's boss is the realm's guardian.
2. **Chests fill.** A depth's drops (its waves and its boss) go into one chest, sealed when the boss falls. The chest
   drops into the tray; from Epic up, a beam in the colour of the rarest thing inside marks it.
3. **Open them.** Click a chest or press Space. The opening climbs through the rarities inside, then the cards turn
   over, rarest last. Upgrades fly to their kind's tile; the rest is salvaged into shards.
4. **Gear goes on by itself.** An item goes on when it's at least as good in every stat, or when trading one stat for
   another leaves the knight ahead. The player never compares numbers. They see the knight change and the push get
   faster.
5. **Spend.** Gold forges slots, shards rank them, mastery grows on worn slots (faster on focused ones), and talent
   points go into the tree. Forge, ranks, talents and the prestige trees each have a button that spends the best way,
   and later their own automation.
6. **Reset.** Past depth 150, kills earn aether. Ascending ends the run for its aether, which buys permanent power
   (a head start among it), and starts a new one. Once enough aether has been earned, transcending gives up the
   ascension tree for stardust, which lasts through every cycle.

Close the game and the knight keeps going. Continue plays the time away, up to a week, with the automation the player
has, and a welcome-back screen shows what happened.

## Systems open one at a time

Each system's button appears in the dock when it opens (`Session.Unlocked`); the automations announce themselves with
a toast. After the first ascension, everything that opened by depth stays open.

| System | Opens at | What it does |
|---|---|---|
| Forge | depth 3 | Gold into forge levels per slot. |
| Talents | level 2 | The tree: a wheel of sectors (damage, health, speed, crit, loot, gold and more), notables and keystones; paragon points after it's full. |
| Codex | 8 first finds | Every slot, rarity, quality band and era is an entry; first finds add to its power. |
| Auto-open | depth 10 | Chests open as they drop. Chests holding the rarest tier found so far wait for the player. |
| Ranks | depth 25 | Shards into ranks per slot. |
| Mastery | depth 40 | Worn slots grow mastery; focused slots grow faster. |
| Ascension | depth 150 | Reset the run for aether and a permanent tree. |
| Auto-forge, auto-ranks | the first ascension | The "all" buttons run by themselves. |
| Transcendence | a tenth of the aether it needs | Reset the ascension tree for stardust and a stronger next cycle. |
| Autopilot | the first transcendence | Ascends by the sensible rule. |

## Chests

- **What one holds.** The items that could matter: at most four per slot, the best kept, plus how many of each rarity
  dropped in all (salvaged on opening), their shards and the first finds they made.
- **Kinds.** Boss chests come from a depth cleared, guardian chests from every tenth depth, and monster chests from
  farming a depth (sealed after a depth's worth of kills). A new hero starts with one chest that holds something for
  each open slot.
- **The pile.** It holds 100 chests; past that, new ones pour into the last, so nothing is lost. The next one opened is
  the best kind first, oldest first among those.
- **Auto-open.** It opens chests as they arrive, and the reveal is skipped. Notable finds still pop: a new tier, a
  first find, an upgrade. A chest holding the rarest tier found so far waits to be opened by hand, so the moments
  worth seeing are kept (`Automation.HoldAt`). While the player is away, everything opens.

## The reveal

Rarities come in four bands, and the opening is longer and louder for each (see "The rarity ladder" in
[art-direction.md](art-direction.md)).

1. The chest shakes and climbs through every tier inside.
2. It bursts in its best tier's colour.
3. Up to eight cards turn over, rarest last.
4. A Divine or rarer card glows face down before it turns.
5. The summary counts what went on, what was found for the first time and what was salvaged.
6. Leaving sends upgrades to their tiles and the rest to the shard counter.

Space or a click hurries an opening; once it's done, the next press puts it away.

## The battle

The numbers settle each depth in closed form. `Stage` turns that into what's shown, so the screen always matches the
rules:

- **Push.** The bar's progress is the depth's progress (`DepthProgress + Accum / Need`). Waves of two walk in and fall
  exactly when their kill time is up; the boss's health drains with the rest of the bar.
- **Farm** (the gate). The next boss can't be beaten yet, so the knight farms the deepest depth he's comfortable at.
  The boss waits behind a gate on the right, labelled with what it needs ("needs x1.8 damage").
- **Leap.** When overkill clears depths by the dozen, the knight dashes and the depth counter rolls. A banner reads
  "Depth 1,240 to 1,480". Then the last depth's boss is fought in full.

How a blow is shown follows the kill time, not the depth:
- **Duel** (0.8 s or more a monster): every swing, its wind-up and impact, a number per hit.
- **Wave** (faster kills): one arc fells the front monster, and a tally counts the kills.

Bosses always play in full, with a name plate, a health bar and the 30-second clock.

Monsters swipe back. The knight dodges, blocks or takes it; his health recovers between waves and never runs out on
the lane, because the rules have already decided he wins. Deaths belong to the gate: a boss he can't beat is the one
he isn't sent to fight.

**Realms.** The realm follows the era of the depth (`Hero.Era`). Eras double in length, and after the eighth realm the
order comes round again with a numeral ("Greenwood II").

| Depths | Realm | Monsters | Guardian |
|---|---|---|---|
| 1 to 99 | Greenwood | Bloblet, Toadstool, Moss Hound, Thicket Glutton | Old Tusk |
| 100 to 199 | Dunes | Dune Scorpion, Sand Raptor, Carrion Harpy, Stitched Dead | Sand Warlord |
| 200 to 399 | Frostpeak | Snow Biter, Rime Wolf, Frost Siren, Rime Stalker | Frost Warden |
| 400 to 799 | Drowned Mire | Tide Shrimp, Drowned Maiden, Bog Gawker, Pale Grasper | Deep One |
| 800 to 1,599 | Ashlands | Cinder Phoenix, Lavamander, Magma Worm, Spined Hellbeast | Cinder Colossus |
| 1,600 to 3,199 | Ruins | Risen Knight, Restless Shade, Living Armor, Dark Priest | Gilded Maiden |
| 3,200 to 6,399 | Caverns | Batmadillo, Pale Ravager, Swine Thug, Chained Horror | Vermithrax |
| 6,400 to 12,799 | Starfall | Cycloper, Rift Wolf, Crystalis, Mangler | Hollow Star |

Each monster has a body plan (walker, hopper, flyer, heavy, slither), which sets how its single picture moves.

## Screens and controls

The screen layout is set out in [art-direction.md](art-direction.md) ("The screen"). The main screen has three parts:
- **The lane** across the top, with a bar over it showing level and XP, gold, shards and aether, the depth and realm,
  and the depth's progress.
- **The chest tray** at the bottom left.
- **The armoury's eight kind tiles and the latest finds** in the middle, with **the dock** (each system's button and
  badge) on the right.

| Action | Input |
|---|---|
| Open the next chest; hurry an opening | Space or Enter, or click the chest |
| Armoury (every slot by body region, forge, ranks, mastery focus) | G, or its dock button |
| Talents (pan with drag, zoom with the wheel) | T |
| Codex | C |
| Ascension and transcendence | A |
| Close a screen; pause, settings, title, quit | Esc |

Buttons never take focus: Space and Enter belong to the chests. Settings has master, music and effects volume,
fullscreen, screen shake, flashes, damage numbers, and number notation (mixed, letters, scientific, engineering).

## Saving

The game saves to user://save.json every 30 seconds, at pause and on quitting. It writes to a side file and swaps it
in, keeping the last save as save.bak. A save this version can't read is never overwritten without asking. Everything
that should survive closing goes in `SaveData` (`scripts/Game/SaveGame.cs`). Bump `CurrentVersion` if old saves can't
be read.

## Checking it

| What | How |
|---|---|
| The numbers hold for 3,000 hours | `progsim --check` (CI) |
| The game keeps up with the simulated player; chests, the stage, saves and time away add up | `progsim --game --hours=1` (CI) |
| It plays from a new game, exported, on Windows | The bot for four minutes (CI, "Autoplay finished") |
| It looks right | The bot and the demos under Xvfb with `--shots` (see the README) |

## Not yet

- The art is a stand-in, built by `tools/placeholders.py` to show the direction. It will be replaced by the game's own
  art through `tools/art.py` (see "The asset pipeline" in [art-direction.md](art-direction.md)).
- The knight is drawn in code from shapes, not from a parts sheet.
- Sounds and music were chosen by name, not by ear. Music doesn't change by realm.
- There is one hero, no challenges and no third prestige layer.
- There are no achievements, no Steam integration and no controller support. The Windows .exe has the default icon.
