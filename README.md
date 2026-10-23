# LootFall

A small game about the satisfaction of opening treasure chests. Three hundred years ago the Locking shut every treasure
in the realm into a warded chest, to starve the demon of greed chained behind the Gilded Gate. The chests are still
everywhere, and only a Keywright can open one, once it's rightfully theirs; everyone else sells them unopened, and the
ward lets nothing through but sound. You're one of Bram's kin, with the Key-mark on your hand, and he hands you his
stall. People bring you chests and price them their own way (by what they heard, by weight, by the chest's colour, by
a dream) and tell you where they found them, not always truthfully. Shake each one yourself, gently, firmly and hard,
find what they missed or misjudged, trade for it, and open it on the spot. Move the shop inward, from the Brindle
crossroads to Thornwick Market, the capital and the Royal Emporium by the Gate, because the stall comes with twenty
years of Bram's unpaid tithe: every fifth evening the Gilded Guild comes for a due that climbs from 40g to 12,000g in
eight weeks. Pay them all (or the whole debt at once, the moment you have it) and the stall is free; find the four
shards of the Aldric Key on the way and the Crown Chest opens on your counter. The world is in
[docs/world.md](docs/world.md).

**Status:** Early Access (0.18): a rebuilt world (four places, 24 sellers with their own stories and rules, six kinds
of things a sword-and-sorcery realm would lock away, every one a find worth having), runs of eight weeks against the
Guild's dues (then endless), with curios, commissions, weekly events and a story; nine **Heartsongs**, the rarest
things in the realm, each with its own song and its own use on display, and an ending that needs all nine; four of
Bram's kin to play, eight challenge levels each, and a talent tree bought with the renown runs earn; saves, settings
and a guided start. The pixel art is drawn in code (`tools/custom_art.py`) as a placeholder until dedicated art
replaces it; recorded sounds and the font are from Kenney's CC0 packs (see [CREDITS.md](CREDITS.md)). Built with Godot
4.7 (.NET / C#), targeting PC on Steam. Design rationale: [docs/design.md](docs/design.md).

## The loop

1. **A customer brings a chest**, tells you where it came from (a millpond, the Barrow, a goblin warren, the Maw's
   hoard; the place says what it tends to hold) and prices it their way: some say how hard they
   shook it and what they heard, some heft it, some go by its colour, some feel for magic, some have no idea. The
   chest's colour (wood, green, blue, purple) says how good things inside tend to be. Each customer has one habit worth
   learning, and many have a rule of their own: a rotten chest that stands two seconds of shaking, a goblin who charges
   for every shake, a smuggler who packs his chests in straw (everything stirs a strength later), a pawn ticket that
   counts every article (false bottom and all), a huntress's game scale, a druid who dreams of the dearest thing inside,
   an imp with an hourglass, an archmage's wager, a knight's claim on the family sword, a devil's due, an old man whose
   price is whatever your slip says. What they tell you is always true: working out what it means is the game.
2. **Shake it yourself.** How fast you drag is how hard you shake (the words under the chest show gentle, firm or
   hard as you go, and which you've tried). Light things (coin jingles, potions slosh, gems
   tinkle) rattle at a gentle shake; weapons only clank at a firm one, and their clanking drowns out anything delicate;
   soft things (cloaks flump, spells rustle) only sound at a hard shake. Lift it high and let go to drop it: a hollow
   boom means a false bottom (but glass may break). As the shop grows, more to work out: two or three of a kind (count
   the rattles), river sand that rustles like a scroll, enchanted things that only a witchlight shows, demon gold.
3. **Correct the slip and trade.** Mark how many of each kind are in there; the slip adds them up at what they'd be
   worth if they're as good as the chest's colour promises, and an odds bar (red to green) says how likely it is to be
   worth more than the price, or your offer. Then pay their price, haggle, or name your own, depending on who it is.
   Pass, and they take it away unopened: your gut tells you, on a red-to-green scale, whether that was wise.
4. **Pick the lock and watch it burst open:** everything flies out with its true quality and a tick or a cross
   against your slip; anything better than its chest climbs, quality by quality, with more fuss for every step, and
   anything worse sags as its colour drains. The profit is stamped across the screen with your gut's verdict and why
   the seller missed it. Keep grand things for your exhibition (enchanted ones count double).
   Once in a long while, about one run in two, a chest sings: a sound like nothing else, that only you can hear. That's a
   **Heartsong**, one of nine things loved more than gold that the Locking was sung into. Nobody's priced it; it comes
   out last, in mythic red, and it's worth far more singing in your shop than any merchant will pay: each one changes
   the trade (two more customers a day, coin and gems at double, a bell that counts what's in every chest...). Find all
   nine, over your runs, and the Crown Chest ends differently.
5. **End of day:** once the stall has earned 120g, spend your earnings on moving inward (each place brings richer
   customers, a new look and something new), tools (the Witchlight shows what's enchanted, the Lodestone silences iron
   and demon gold, Fine Lockpicks), cabinets, a crier and a night market for more customers, and a buyers' trade that
   puts up everything you sell (brass scales, an auction block, a royal warrant). From Crownhold, choose which of the
   shop's synergies to put to work (ten of a kind at a quality earns one; each puts up what its kind sells for). Once
   your purse could pay off everything you owe, the Guild offers its **Bookkeeping Engine** for exactly that sum: it
   reads every chest for you, so a won run can be finished fast.
6. **The week:** every fifth evening Mister Vane collects the Guild's tithe. Short, pawn what's on display or sell your
   curios back; once a run Bram will vouch for you; otherwise the Guild seals the shop. Start a day nearly broke and
   Bram dips into his secret stash for you (three times a run). From the second week, Nim the
   peddler sells **curios** that bend one rule each (a lucky coin, kid gloves no seller minds, a gambler's token...;
   priced to keep pace with the weeks), regulars post **commissions** (deliver what they want from a chest instead of selling it), and once a
   week something happens in town: a fair, Locking Day, the Crown's buyer, a royal chest, a storm, a barge aground,
   or the Stranger with a chained chest that nothing rattles in. The Hooded Woman trades shards of the Aldric Key for
   the finds she asks for. Whenever you have all that's left of the debt, you can pay it off and finish early.

**Between runs:** a run ends with a score (dues paid, the debt cleared and how early, the Crown Chest, the shop, the
collection) that becomes **renown**, multiplied by the run's challenge level. Renown buys **talents** (a tree of 13:
a softer bad night, more choice from Nim, the shop's name sooner; none adds a share to what things sell for), kept for
every run. New game asks who minds the stall:

| Who | Plays |
|---|---|
| Kit, the Ear | the game as it was, with Bram's lessons |
| Hesk, the Scale | can't shake hard or drop a chest, but weighs every one: the weight left over is what he couldn't hear (unlocked by paying the third due) |
| Rosa, the Tongue | everybody haggles with her (though they open higher for her), and a lowball gets a second try; whoever walks out stays away that week (unlocked by clearing the debt) |
| Isolde, the Collection | displays from the first day and visitors pay a coin a point every night, but her collection can't be pawned (unlocked by opening the Crown Chest) |

The first weeks are gentle and a first run usually falls in the middle ones, with a purse that grew, just not fast
enough; it takes a few runs of sharper judgment and a few talents to clear the debt. Clearing it with someone opens
their next challenge level: each adds one of the Guild's harsher terms (Bram's
stretched thin, fussy sellers, demon gold about, a lean start, thin crowds, the Guild's interest, sharp sellers, a hurried town)
and a quarter more renown.

Day one is guided: Uncle Bram, who's handing you the stall, brings in four customers with one lesson each. After
that, each seller's first visit is a lesson in their habit and their rule, and each new part of the shop (moves, lockpicking,
the display shelf, synergies, the Guild, curios, commissions) comes with a word from Bram when it opens up. The shop
saves at the start of every day; your best runs are kept on the title screen.

## Controls

| Action | Input |
|---|---|
| Shake a chest | Drag it with the left mouse button: slowly for gentle, fast for hard |
| Drop it | Lift it high and let go |
| Count a kind on the slip | Click its row: one more each click (up to three from Crownhold), then back to none |
| Mark one of a kind enchanted | Click the star on its row (once you have the Witchlight, which stays lit) |
| Make an offer | Set the slider, then Offer |
| Lodestone | Button on the right, once you own it |
| Ledger (sounds, regulars and how far to trust them, places, collection, synergies) | L, or the Ledger button |
| Skip a seller's lesson | Skip lesson, over Bram's box |
| Pick a lock | Space or left click when the pick is in the green; F for the crowbar |
| Open the chest | It opens by itself (click or Space to hurry it) |
| Keep something for display | Its Keep button after an opening (Swap when the display is full); sell it back from the exhibition at day end |
| Deliver to a commission | Its Deliver button after an opening |
| Buy or sell back a curio | Nim's screen, the morning he visits |
| Pay the Guild, pawn, ask Bram | The Guild's screen, every fifth evening |
| Pay off the whole debt early | "Pay it all off" on the day-end Guild panel (or "Pay it all" on the Guild's night), once you have it |
| Choose synergies | Synergies, at the end of a day |
| Next tip from Bram | Click his box |
| Reread today's event | Click it in the top bar |
| Choose each kind's sound | Settings, Item sounds: click a kind's sound to hear the next |
| Pause, settings, quit to title | Esc, or Menu in the top bar |
| Choose who minds the stall, and the challenge level | New game: click a character, then - and + |
| Learn talents | Talents, on the title or the character screen: click a gold one |
| Ask Nim for something else | Show me something else, on Nim's screen (with Second Look) |

## Development

The code is split so the game rules can be tested without the engine:

| Folder | What's in it |
|---|---|
| `scripts/Core` | Pure C# rules: kinds, qualities and the shaking rules, chests, where chests come from (`Origins.cs`), sellers and how they judge, trade and tell their stories, the appraisal slip, a careful player's read and strategy, sellers' lessons, synergies, upgrades, the shop's progress and exhibition, the run (the Guild's dues, curios, commissions, events, the story), Bram's kin, challenge levels, talents and the score (`Meta.cs`, `Kin.cs`), Bram's hints. No Godot types. |
| `scripts/Phases` | The screens: title, the character screen, counter, lockpicking, opening, end of day, the Guild's night, Nim's curios, story scenes, the end of a run. |
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
# add --start-tier=3 --gold=600 to jump ahead with the tools you'd have by then,
# or --start-week=6 for a shop as a careful player would have it by then (--gold=N sets its purse);
# --character=hesk|rosa|isolde, --challenge=N and --talents=all play one of Bram's kin on those terms
xvfb-run -a godot --path . -- --shell-demo --shots=$PWD/shots_shell  # title, character screen, talents, pause, settings
xvfb-run -a godot --path . -- --vault-demo --quit-at-end --shots=$PWD/shots_vault  # the finale: the Crown Chest
godot --headless --path . --export-release "Windows Desktop" build/windows/LootFall.exe
```

Add `--write-movie movies/day.avi --fixed-fps 60` to the autoplay command to record video with sound, and
`--no-tutorial` to turn off Bram's tips and the scripted first day.
