# LootFall

A small game about the satisfaction of opening treasure chests. You inherit a roadside stall; people bring you
chests and price them their own way: by what they heard, by weight, by the chest's colour, or by a dream. Shake each
one yourself, gently, firmly and hard, find what they missed or misjudged, trade for it, and open it on the spot.
Keep the best for your exhibition and grow the stall into the finest shop in the kingdom.

**Status:** Early Access (0.9): the full loop from roadside stall to Royal Warrant, with saves, settings and a guided
start. Pixel art from Pixel-boy's CC0 Superpowers asset packs and recorded sounds from
Kenney's CC0 packs (see [CREDITS.md](CREDITS.md)). Built with Godot 4.7 (.NET / C#), targeting PC on Steam.
Design rationale: [docs/design.md](docs/design.md).

## The loop

1. **A customer brings a chest** and prices it their way: some say how hard they shook it and what they heard, some
   heft it, some go by its colour, some feel for magic, some have no idea. The chest's colour (wood, green, blue,
   purple) says how good things inside tend to be. Each customer has one habit worth learning, and a few won't stand
   for something (a hard shake, a drop, a magnet).
2. **Shake it yourself.** How fast you drag is how hard you shake. Light things (coins jingle, bottles slosh,
   jewelry tinkles) rattle at a gentle shake; metal only clanks at a firm one, and its clanking drowns out anything
   delicate; soft things (cloth flumps, paper rustles) only sound at a hard shake. Lift it high and let go to drop it:
   a hollow boom means a false bottom (but glass may break). As the shop grows, more to work out: two or three of a
   kind (count the rattles), sand that rustles like paper, enchanted things that only a witchlight shows, gilded
   fakes.
3. **Correct the slip and trade.** Mark how many of each kind are in there; the slip adds them up at what they'd be
   worth if they're as good as the chest's colour promises, and an odds bar (red to green) says how likely it is to be
   worth more than the price, or your offer. Then pay their price, haggle, or name your own, depending on who it is.
   Pass, and they take it away unopened: your gut tells you, on a red-to-green scale, whether that was wise.
4. **Pick the lock and watch it burst open:** everything flies out with its true quality and a tick or a cross
   against your slip; anything better than its chest climbs, quality by quality, with more fuss for every step, and
   anything worse sags as its colour drains. The profit is stamped across the screen with your gut's verdict and why
   the seller missed it. Keep grand things for your exhibition (enchanted ones count double).
5. **End of day:** once the stall has earned 120g, spend your earnings on renovations (each brings richer customers,
   a new look and something new), tools (the Witchlight shows what's enchanted, the Magnet silences iron and fakes,
   Fine Lockpicks) and cabinets. From the town shop, choose which of the shop's synergies to put to work (ten of a
   kind at a quality earns one).

Day one is guided: Uncle Bram, who's handing you the stall, brings in four customers with one lesson each. After
that, each seller's first visit is a lesson in their habit, and each new part of the shop (renovations, lockpicking,
the display shelf, synergies, the warrant) comes with a word from Bram when it opens up. The shop saves at the start
of every day.

## Controls

| Action | Input |
|---|---|
| Shake a chest | Drag it with the left mouse button: slowly for gentle, fast for hard |
| Drop it | Lift it high and let go |
| Count a kind on the slip | Click its row: one more each click (up to three from the town shop), then back to none |
| Mark one of a kind enchanted | Click the star on its row (once you have the Witchlight, which stays lit) |
| Make an offer | Set the slider, then Offer |
| Magnet | Button on the right, once you own it |
| Ledger (sounds, regulars, collection, synergies) | L, or the Ledger button |
| Skip a seller's lesson | Skip lesson, over Bram's box |
| Pick a lock | Space or left click when the pick is in the green; F for the crowbar |
| Open the chest | It opens by itself (click or Space to hurry it) |
| Keep something for display | Its Keep button after an opening (Swap when the display is full); sell it back from the exhibition at day end |
| Choose synergies | Synergies, at the end of a day |
| Next tip from Bram | Click his box |
| Pause, settings, quit to title | Esc, or Menu in the top bar |

## Development

The code is split so the game rules can be tested without the engine:

| Folder | What's in it |
|---|---|
| `scripts/Core` | Pure C# rules: kinds, qualities and the shaking rules, chests, sellers and how they judge and trade, the appraisal slip, a careful player's read and strategy, sellers' lessons, synergies, upgrades, the shop's progress and exhibition, Bram's hints. No Godot types. |
| `scripts/Phases` | The screens of the loop (counter, lockpicking, opening, end of day). |
| `scripts/Ui` | Visuals: the shakeable chest, the ledger, Bram, the seller, HUD, particles and styling. |
| `scripts/Audio` | Recorded sounds, the synthesized slosh, and placeholder effects. |
| `scripts/Debug` | Balance simulation (with the lesson test), the shake test and an autoplay bot for screenshots and recordings. |
| `assets/` | Sprites, portraits, backgrounds, sounds, icons, UI frames and font, generated by `tools/import_art.py` from CC0 packs. |

Useful commands (Linux; run `tools/setup-cloud.sh` first in a fresh container):

```bash
dotnet build                                               # compile
godot --headless --path . -- --sim                         # balance report
godot --headless --path . -- --shake-test                  # check that shaking follows the rules
xvfb-run -a godot --path . --rendering-driver opengl3 -- \
  --autoplay --days=1 --shots=$PWD/shots                   # play the guided day, save screenshots
# add --start-tier=3 --gold=600 to jump ahead with the tools you'd have by then
xvfb-run -a godot --path . -- --shell-demo --shots=$PWD/shots_shell  # title, pause and settings screenshots
godot --headless --path . --export-release "Windows Desktop" build/windows/LootFall.exe
```

Add `--write-movie movies/day.avi --fixed-fps 60` to the autoplay command to record video with sound, and
`--no-tutorial` to turn off Bram's tips and the scripted first day.
