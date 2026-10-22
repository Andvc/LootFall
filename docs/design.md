# LootFall design notes

## The game in one paragraph

Three hundred years ago the Locking shut every treasure in the realm of Aldmark into a warded chest, to starve the
demon Avarax, the Gilded Maw. The chests are still everywhere, and only a Keywright can open one, once it's rightfully
theirs: everyone else sells them unopened, and the ward lets nothing through but sound (the whole world is in
`docs/world.md`). You're one of Bram's kin, with the Key-mark, and Bram hands you his stall at the Brindle crossroads.
People bring you chests and price them their own way: some shake them and price what they heard, some go by the
weight or the chest's colour, some feel for magic, some dream up a number, and each has a rule of their own (a rotten
chest, a fee for every shake, a wager, a clock, a claim on what's inside). They tell you where the chest came from, and
not all of them tell the truth. Shake each one yourself (gently, firmly, hard), drop it to listen for a false bottom,
count the rattles, and work out what they missed, made up or misjudged. Mark what you think is inside on your
appraisal slip and trade the way each customer trades: pay their price, haggle, or name your own. Pass, and your gut
tells you whether you were right; buy, and the chest bursts open on the spot: each thing's true quality, a tick or a
cross against your slip, and now and then something far better than the chest promised. What you acquire builds the
shop's name (synergies) and its exhibition, which draw more and better customers, and the shop moves inward, from the
crossroads to Thornwick Market, Crownhold and the Royal Emporium by the Gilded Gate. The stall comes with Bram's twenty
years of unpaid tithe to the Gilded Guild: every fifth evening Mister Vane collects a due that climbs from 40g to
12,000g over eight weeks. Curios from Nim the peddler bend the rules in your favour, regulars pay well for
commissions, something different happens in town once a week, and a hooded woman trades shards of the Aldric Key for
the finds only a good ear turns up. Pay all eight dues, or the whole of what's left at once whenever you have it, and
the stall is free (and with the Key whole, the Crown Chest opens on your counter and the Locking holds); fall short and
the Guild seals the shop.

Across runs, four of Bram's kin can mind the stall, each reading chests or running the shop their own way: Kit listens,
Hesk weighs, Rosa haggles, Isolde collects. Clearing the debt with one opens the next of eight challenge levels for
them, each adding one of the Guild's harsher terms. Every run ends with a score; times the challenge level's
multiplier, it's renown, which buys talents that come along on every run.

## The world

`docs/world.md` is the reference for anything that speaks, sells, falls out of a chest or happens in a scene: why the
world is full of chests (the Locking), why people sell them unopened (only a Keywright can open one, once it's theirs),
why you can (the Key-mark), and what opening them does (coin for the realm, treasure back in the light, and every chest
a little looser on the Maw's chains, which is why the Guild wants you opening them). Every rule has a reason you could
tell a stranger in a tavern: the ward lets only sound through, so everybody shakes and guesses; the old iron lock under
the ward still needs picking; people hid their best things under a false bottom when the Wardens came round; demon gold
is iron under the gilt, so the lodestone silences it.

The shop's four tiers are four places, a journey inward from the edge of the realm to the Gate at its heart, and each
brings people who belong there: villagers, an innkeeper and a young delver at the crossroads; a goblin, a huntress, a
druid, a monk and a smuggler at Thornwick; an archmage and his apprentice, a dwarf, a noblewoman, a knight, a
pawnbroker and a mudlark in Crownhold; demons, a court lady, a fence, the King in disguise and once in a while the Maw
himself at the Royal Emporium. Every chest has an **origin** (`Origins`: the millpond, the Barrow, a goblin warren, the
abbey crypt, the Maw's hoard...), and the origin decides what it tends to hold. Sellers say where theirs came from
(`SellerDef.Stories`), truthfully as often as their `Honesty` says, or they tell a tall tale (`Tales`). What really
came out of a chest shows when it's open (`Visit.Explain`: "Dorrin's story was a tall one: it came from the Barrow"),
the ledger keeps each seller's tally and a Places page of what each place tends to hold, and a careful player who knows
a seller always tells the truth reads their chest by its place (`Strategy.Likely`).

## The puzzle: rules you'd expect from real life

Three core rules, from the first day:

1. **Every kind of thing has one sound, and it only sounds when the shake is strong enough to stir it.** Coin
   jingles, potions slosh and gems tinkle at the gentlest shake; arms only clank at a firm one; garb flumps and
   scrolls rustle only at a hard one.
2. **While arms clank, you can't hear anything delicate.** Shake gently and the steel stays put.
3. **Sellers price only what they think is there.** Each has their own way of judging (below). Those who listen say
   what they heard, and the slip starts marked with it.

Then, one at a time as the shop grows, more layers of deduction, each taught by the seller it belongs to:

| Layer | From | The rule | How you tell |
|---|---|---|---|
| False bottoms | Thornwick | a sealed hollow under the floor never rattles | drop the chest: a hollow BOOM (glass may break) |
| Two or three of a kind | Crownhold | each thing sounds from its own spot, so more of them rattle more often | count the words over the chest; most sellers never count |
| Sand | Crownhold | a scroll case of river sand rustles just like a scroll, but pours at the gentlest shake; parchment never does | a rustle at a gentle shake is sand |
| Enchantment | Crownhold | anything can be enchanted (worth double); it sounds like anything else | under the **Witchlight** it glows |
| Demon gold | Royal Emporium | gilded iron jingles and tinkles like gold and gems | near the **Lodestone** iron can't move: a fake goes quiet |

Sound-alikes (sand, demon gold) are the deductive heart of the later shops: the sound tells you the kind of thing,
but only the rules tell you whether it's real. Soft things only sound at a hard shake, which hides them from anyone
who won't shake hard (most sellers) or isn't allowed to (Brother Ansel's crypt chests: take the vestments on faith,
or pay to be sure).

How fast you drag sets the shake strength (gentle from 40 px/s, firm from 520, hard from 1150). While you shake,
three words under the chest (gentle, firm, hard) show how hard you're shaking and, in green, the strengths you've
already given it a fair go at; at rest the line under the chest says the same ("Shaken gently and firmly. Not hard
yet."). That's your hands, not the chest: what's inside is still only in the sounds. (v5 to v10 had no readout at all,
and the beginner playtest showed new players couldn't tell a firm shake from a hard one, or which a lesson was still
waiting for.) `ShakeBox` gates physics impacts by `Chest.Sounds`/`Chest.Audible`, and the shake test checks the two
agree, thing by thing, with and without the lodestone (`Magnet` in code).

## What a chest holds, and how good it is

A chest's **colour** says what to expect: wood (common), green (uncommon), blue (rare), purple (epic). Each thing
inside rolls its own quality around that: 80% as promised, 15% one worse, 4.5% one better, 0.4% two better, 0.1%
three better (what would fall below common or above legendary stays at the end). Value is the kind's base times
1, 3, 8, 20 or 50 by quality.

Surprises are rare on purpose: in v10 one thing in five came out better (two steps in 2%), a third of a careful
player's income came from them, and one lucky chest could pay for a renovation. Now a careful player sees about one
thing in twelve come out better and 1 in 170 jump two steps or more, and surprises are about a seventh of the income.
So a long dry spell doesn't drag on, a pity counter (`GameState.DrySpell`) counts the things opened since the last
surprise: after 15 (7 with a Lucky Shell), each chest you buy has a 3% chance per thing more (up to 50%) that one thing
in it is a step better, and any surprise resets it. The Lucky Coin and the Four-Leaf Clover work on top of this.

The slip shows what each thing would be worth **if it's as good as the chest promises** (`Categories.Nominal`).
Sellers price by the **average** (`Categories.Expected`), which keeps the economy easy to balance.

Neither number answers the question a player actually has, so the panel shows an **odds bar** under the total: "Worth
more than 55g? 62%". It's exact (`Appraisal.Outcomes` combines every ticked thing's quality odds, what a false bottom
might hold, and the shop's sale bonuses), updates as you tick rows and as you move the offer slider, and runs from red
(a long shot) through gold to green (a safe bet). Why not show the average: 80% of things come out exactly as promised
and the average includes rare big upgrades, so most chests are worth a little *less* than it, and a player who paid it
would lose a little on most buys. Why not just the promised total: a price a little above it can still be worth a
gamble, and one a little below is nearly always a good buy, and the bare number says neither. With the bar there are
no sums to do, and it's only as good as your listening, so the read is still yours. The sim's careful player buys
when the bar is on their side, which wins more often than buying on the average (85 to 92% of buys profit) and still
profits from every seller.

| Kind | Sound | Stirs at | Note | Common to legendary |
|---|---|---|---|---|
| Coin | jingle | gentle | delicate | Copper Pennies 12g, Silver Marks, Gold Crowns, Old Sovereigns, Dragon Gold 600g |
| Arms | clank | firm | loud, iron | Rusty Dagger 5g, Soldier's Helm, Steel Longsword, Knight's Gauntlet, Runeblade 250g |
| Garb | flump | hard | soft | Moth-Eaten Cloak 6g, Hunter's Coat, Silk Robe, Archmage's Mantle, Royal Ermine 300g |
| Potions | slosh | gentle | delicate, glass | Swamp Tonic 8g, Healing Draught, Elixir of Vigour, Phoenix Tears, Ambrosia 400g |
| Scrolls | rustle | hard | soft; sand sounds alike | Tattered Map 5g, Spell Scroll, Bestiary, Grimoire, Locking Codex 250g |
| Gems | tinkle | gentle | delicate | Brass Ring 20g, Silver Amulet, Ruby Ring, Starstone, Dragon's Eye 1000g |

The kinds are what a sword-and-sorcery realm would lock away: currency, weapons and armour, clothes, potions, scrolls
and gems. Each keeps the sound you'd expect of it, so the three rules read the same in any shop. From Crownhold,
**anything can be enchanted** (5% of things, more from the Arcanum): an Enchanted Silk Robe is still a silk robe, the
same quality and colour, worth twice as much and twice the exhibition points. It sounds like anything else; under the
Witchlight it glows. A fake (demon gold, a paste gem, a scroll case of sand) is worth 1g; broken glass is worth
nothing.

## Ways to judge a chest

Each answers one question by a rule you'd expect from real life. All of them are perceptions; nothing states a value.

| How | From | Tells you | Cost or risk |
|---|---|---|---|
| Shake gently, firmly, hard | start | which kinds are inside, and how many of each | Brother Ansel hates a hard shake; Dorrin's chests stand two seconds of it; Snikkit charges for every strength after the first |
| Drop it (lift it high, let go) | Thornwick | a hollow BOOM means a false bottom | glass may break (half the time each); Lady Corisande hates it |
| Witchlight (200g) | Crownhold | always lit once bought: anything enchanted glows when it makes a sound | none |
| Lodestone (400g) | Royal Emporium | iron can't move: no clanks to drown things out, and demon gold goes quiet | Lady Ermine hates it |

**Left out: the scale.** A weight only means something if you add up what each thing should weigh in your head, which
isn't intuitive, and the drop already gives the one weight-like fact that matters (hollow or solid). Weight comes back
as a seller's habit instead (Wenna the Huntress goes by it, and the ledger says which kinds are heavy), and as
Hesk's way of reading a chest.

**Lockpicking** (from Thornwick) comes after you buy a locked chest: set each pin when the pick passes the
green. A masterful pick (no slips, mostly perfect) makes everything worth 25% more; the crowbar is quick but scratches
everything (-10%) and breaks glass. Fine Lockpicks (50g) widen the sweet spot.

## Passing: your gut

When you pass, the seller takes the chest away unopened: opening it after you turned it down wouldn't make sense. But
you get feedback straight away, the way you would in life: a gut feeling, from what it really held against their
price (or what they'd have taken, if they wanted you to name one). Six bands, from "You have a feeling you dodged a
cart" through "Could have gone either way" to "Even the heavens pity you", each with a few variants. Words alone are
hard to rank, so the feeling also has a colour and a meter: a strip from red ("missed out") to green ("right call"),
with a marker that slides to where this call landed (`Gut.PassScore`: even at one half, a third of the price or
less at the green end, four times it or more at the red). A seller who storms off leaves the same feeling. After an
opening, the verdict gets a gut line and meter too, from what it held against what you paid, from "fleeced" to "a
steal" (`Gut.BuyScore`).

## Opening a chest

It opens by itself (no holding): a short rumble in the chest's own colour, then the burst. A thing **worse** than its
chest comes first: it pops up in the chest's colour as if all's well, then sags with a deflating slide while the
colour drains to what it really is ("WORSE..."), and flops onto the table. Things as good as promised land quickly.
An enchanted thing lands in a purple glow ("ENCHANTED! x2"). A thing **better** than its chest rises out still glowing in the chest's
colour while the room dims, trembles, and climbs a quality at a time ("BETTER!", "EVEN BETTER!!", "INCREDIBLE!!!"),
each step with a flash, a ring and a rising arpeggio, before it slams onto the table. Two or more steps up also stop
time, flash the room white, and throw confetti and a fanfare. Each quality lands with its own effect, not just its
colour: common, a puff of dust; uncommon, rising sparkles and a chime; rare, a pillar of light and a ring; epic,
crackling lightning, a boom and a lingering aura; legendary, all that with turning rays, a fountain of coins and a
fanfare. `--opening-demo` opens a crafted chest with one of each to preview them.

## Sellers: one way of judging, one rule, and a story

The same rules make a different puzzle with each seller, because each judges a chest a different way, with a blind
spot a careful player can see through; and since v15 each comes from somewhere, tells you where the chest came from
(true or not), and many have **a rule of their own that changes what you can do or what the deal is**. Every rule is
something a real person might insist on. Not every visit is a deal: often the right answer is to pass.

| How they judge | Who | Their blind spot | How to beat it |
|---|---|---|---|
| Listen at one strength | Penn, Snikkit and Fennick (gently), Ansel and Pell (firmly), Hob and Shelly (hard) | what their strength can't stir or a clank drowns; they never count | shake at the strengths they didn't; count |
| Listen gently, then hard | Skrae | nothing it can hear; enchantment and false bottoms | witchlight and drop: it brings plenty of both |
| Count the rattles | Mags | a rattle's a rattle: a dagger costs what a purse does | buy when the rattles are good kinds |
| Weight | Wenna | heavy means valuable: arms are heavy and cheap, potions and scrolls light | buy light chests of good things |
| The chest's colour | Dorrin, Orrin, Corisande (feels magic), Aldous, Vexis, Malbranche, the Maw | never look inside | buy when it holds more than a chest that colour usually does |
| A dream | Mother Bramble | the price has nothing to do with the chest | add it up, buy when the dream's cheap |
| Knows only arms | Grit | prices other things at half, and gems as diamonds | haggle; mind the tinkles |
| Knows only her gems | Ermine | prices her gems at what she paid; the rest comes free, but some gems are paste | weigh the free things against the paste |
| Feels magic | Wren | can't tell a coin from a coat; wants more only for enchanted things | name a low price; a refusal means something's enchanted |
| No idea | Tam, Old Aldo | Tam takes a few coins (and his millpond chests are sometimes empty); Aldo lets your slip set the price | shake first; read Aldo's exactly |
| Packed it himself | Silas | sells demon gold as gold and gems, cheap | the lodestone silences fakes |

**The rules** (`SellerDef` fields; `Visit`, `Shop.Buy`, `Shop.Settle` and the counter carry them out, and
`GameState.Terms` puts each under the chest in words):

| Rule | Who | What it does | Why it's a puzzle |
|---|---|---|---|
| A rotten chest (`ShakeLimit`) | Dorrin | two seconds of shaking, all told, then it creaks and won't be shaken again | one strength: hard hears everything that isn't drowned out; gentle hears the coin a clank would hide |
| A fee for shaking (`ShakeFee`) | Snikkit | every strength you try after the first puts his price up a tenth | is the hard shake worth a tenth? Gentle and hard between them hear everything firm would |
| No hard shake (`HatesHardShake`) | Brother Ansel | half again if you do | the crypt's vestments only flump at a hard shake: tick them on faith or pay to be sure |
| No drop (`HatesDrop`) | Lady Corisande | half again if you do | half her grandmother's trick boxes hide a false bottom |
| No lodestone (`HatesMagnet`) | Lady Ermine | half again if you use it | a quarter of her gems are paste |
| A clock (`Patience`) | Vexis | twelve seconds | gently, then hard, and decide |
| A dream price | Mother Bramble | the price is a dream (0.4 to 1.8 times a usual chest) | add it up yourself |
| A wager (`Wager`) | Archmage Orrin | read his chest exactly and a third of the price comes back | a careful read makes his dear chests cheap |
| An heirloom (`Heirloom`) | Sir Aldous | the best arms inside are his, for a fee he names (three-fifths of what it's worth as promised) | value the rest; a chest with no blade is all yours |
| A devil's due (`TakesDearest`) | Malbranche | half price, but the dearest thing inside is his | many middling things suit his bargain; one grand thing doesn't |
| The King's game (`SlipPrice`) | Old Aldo | his price is your slip: exactly right and he takes a quarter off; wrong, and you pay what it truly held and a quarter more | the purest test of a read; a careless one costs dear |
| The Maw's gaze (`Gaze`) | Avarax, the Gilded Maw | one coin for the chest, but everyone after him today asks twice as much | count who's still waiting |

**Stories** (`SellerDef.Stories`, `Tales`, `Honesty`; `Market.Story`). Every seller's line is a story about where the
chest came from and what happened to them, with what they heard or how heavy it felt woven in ("Turned up under the
plough on the old battlefield. Shook it hard: heard a clank. 30g, and that's a bargain!"). A seller tells the truth
about the place as often as their honesty says; otherwise it's a tall tale (Dorrin's dragon, Snikkit's King's bedroom,
Silas's Royal Treasury). The place decides what the chest tends to hold, so a true story is a clue and a tall one is a
trap: the careful player reads an always-honest seller's chest by its place, and anyone else's by what they usually
bring. The ledger keeps how often each seller's story held up, and the results screen says when one didn't.

Three ways to trade:
- **Fixed price**: buy or pass.
- **Haggle**: they ask high (about 1.9 times what they think it's worth). You get one offer: they take it if it's at
  least what they'd really accept, name a final price if not, or storm off if it's under 35% of their ask.
- **Name a price**: they wait for your offer. Two tries.

| Seller | Place | Comes from | Judges by | Trades | Their rule | Trust |
|---|---|---|---|---|---|---|
| Little Tam | Brindle | the millpond, Gran's attic | no idea | name a price | (30% empty) | true |
| Widow Penn | Brindle | her Tobb's kitbag, the attic | a gentle shake | fixed | | true |
| Hob the Farmer | Brindle | the old battlefield | a hard shake | haggle | | true |
| Mags the Innkeeper | Brindle | unpaid tabs | counting rattles (firmly) | fixed | | true |
| Dorrin the Delver | Brindle | the Barrow | colour | fixed, cheap | a rotten chest | tall tales (40% true) |
| Snikkit the Goblin | Thornwick | a goblin warren | a gentle shake | fixed | a tenth more per strength | lies |
| Wenna the Huntress | Thornwick | outlaws' camps, wolves' dens | weight | fixed, fair | (80% locked) | true |
| Mother Bramble | Thornwick | the Old Wood | a dream | fixed | | true |
| Brother Ansel | Thornwick | the abbey crypt | a firm shake | fixed, steep | no hard shake | true |
| Fennick the Smuggler | Thornwick | the smugglers' road | a gentle shake | haggle | (55% false bottoms) | half true |
| Wren the Apprentice | Crownhold | the Arcanum stores | feeling for magic | name a price | (25% enchanted) | true |
| Archmage Orrin | Crownhold | the Arcanum stores | colour | fixed | a wager | true |
| Grit the Miner | Crownhold | a dwarf-hold | knowing arms (hard) | haggle | | true |
| Lady Corisande | Crownhold | a noble's strongroom | colour, plus magic | fixed | no drop (50% false bottoms) | true |
| Pell the Pawnbroker | Crownhold | forgotten pawn lots | a firm shake, never counting | fixed, steep | (60% pairs) | true |
| Shelly the Mudlark | Crownhold | the Aldwater mud | a hard shake | fixed | (half her scrolls are sand) | true |
| Sir Aldous | Crownhold | a knight's war chest | colour | fixed | an heirloom | true |
| Vexis the Imp | Royal Emporium | the Maw's hoard | colour | fixed | twelve seconds (things often better than their colour) | mostly lies |
| Malbranche | Royal Emporium | the Maw's hoard | colour | fixed, cheap | a devil's due | half true |
| Skrae | Royal Emporium | the Maw's hoard | a gentle shake, then a hard one | fixed | (enchantment 20%, false bottoms 30%) | true |
| Lady Ermine | Royal Emporium | a noble's strongroom | her gems, at what she paid | fixed | no lodestone (25% paste) | true |
| Silas Gilt | Royal Emporium | the Maw's hoard | packed it himself | fixed, at about half | (60% demon gold) | lies |
| Old Aldo | Royal Emporium | the Royal Treasury | none | your slip | the King's game | lies (about who he is) |
| Avarax, the Gilded Maw | Royal Emporium | the Maw's hoard | colour | one coin | the Maw's gaze | lies |

For listeners, whether the chest holds something they can't price (unheard, or a second of a kind they didn't count)
is rolled first, at their miss rate (55% by default; 45% for Pell, 50% for Ansel, 60% for Snikkit; Skrae and Silas
miss nothing), which keeps buying blind from paying while listening does. Everyone brings two of a kind now and
then (8%, from Crownhold). Old Aldo comes in less often than the others (`Visits`), and the Maw rarely.

## A seller's first visit is a lesson

The first time each seller comes in (with tips on), their visit is a lesson in their habit, built so it can't fail
to show it: Fennick's chest has a false bottom, Pell's has three purses, Shelly's has a scroll case of sand, Silas's has
demon gold, Dorrin's comes with a tall tale (a dragon) to set against the Barrow, Malbranche's holds four things for
him to take the best of. Bram gives one step at a time; each step has a check
(`LessonStep.Done`, on what's been heard, the slip, the visit) that runs every frame, so a step done early counts.
Until the steps are done, the trade buttons wait (unless the step is about trading, like naming Tam a price); the
lesson can always be skipped. Steps adapt to the tools you own (Skrae's asks you to mark what glows if you have the
witchlight; Silas's to use the lodestone if you have one). Ansel's lesson leaves the choice to you: tick the crypt's
vestments on faith, or shake hard and pay half again to hear them. The new rules each get theirs: one hard shake for
Dorrin's rotten chest, gentle-then-hard for Snikkit's fee, an exact read for Orrin's wager and the King's game, the
queue to count for the Maw. Day one is still the scripted stall day; Tam, Hob and Mags teach
naming a price, haggling and counting rattles on their first visit after it.

Lessons and Bram's tips never talk over each other. A lesson waits until any tip already up (a new shop, a lock, a
new way of trading) has been read. While a lesson runs, tips that would come up don't pile up behind it: they wait
for their moment to come round again. And a lesson's `Covers` marks the tips it already teaches as seen (Hob's lesson
is the haggling tip; Fennick's, the drop and the boom; Dorrin's, the story tip). A new shop's crowd doesn't all arrive
on its first day: with tips on, no more than three lessons a day (`GameState.LessonsPerDay`), so Crownhold's seven
newcomers take three days to meet. Bram's kin who come knowing the trade (no lessons) still get a word on each seller's
terms the first time they meet them (`seller_*` in `Hints.cs`), and the terms are always under the chest. `--autoplay` prints every message Bram shows, and flags any that was replaced before it could be read.

`--sim` ends with a lesson test: every lesson played the careful way (the same strategy the bot and the sim use),
with and without the shop's tools, must complete every step in order.

## How the shop opens up

Everything arrives when it's needed, each with a word from Bram, so the first days are only about listening:

| When | What opens up | Bram explains |
|---|---|---|
| Day 1 | shaking, the slip and the odds bar, buying and passing | one lesson per customer (scripted) |
| Day 2 | haggling, naming a price, counting rattles; sellers' stories and the ledger (L) | each seller's first visit is a lesson; the ledger and the story tip on a later customer |
| The stall has earned 120g, all told (`GameState.UpgradesAt`) | moves and tools at the end of the day; until then the day-end screen shows how far there is to go | what Thornwick brings |
| Move to Thornwick | locks and lockpicking; the display shelf, keeping things, the exhibition and cabinets | the lock (the first time, the pick waits while he talks); the shelf and why an exhibition matters |
| Move to Crownhold | synergies: the journal has tallied everything from day one, so some are ready to switch on | what a synergy is and where to choose one |
| The Royal Emporium | the biggest crowd, the lodestone, the Royal Seal | that the Gate is just down the street |
| The first evening | the Gilded Guild and Bram's twenty years of back-tithe: a due every fifth evening (until then nothing shows it) | a scene: Vane, then Bram |
| Week 2 | the Locking and the Maw; Bram's shard of the Aldric Key; commissions; Nim's curios (his first visit is that morning); one event a week | a scene; a tip with each |
| Week 3 | the Hooded Woman's commissions, which pay in shards of the Key | a scene |
| A short night | pawning, and (once a run) Bram's word | a tip |
| A day that starts nearly broke | Bram's secret stash (three times a run) | the day card, and a tip the first time |
| The day's event | a chip in the top bar that reopens it | a tip pointing at it |

The moves are renovations under the hood (`shop2` to `shop4`, `ShopTiers.Name`): Bram's Stall, Thornwick Market,
Crownhold, the Royal Emporium. `GameState.UpgradesOpen`, `DisplayOpen` and `SynergiesOpen` hold the gates, so the sim and the bot follow them too. "Earned" is what
the chests you've bought brought in (the day end's "the chests held", added up): a careful player gets there at the
end of day 3, after two days of practice at the stall. (Counting profit instead took five to eight days at the stall
with nothing new, too long.)

## The run: the Guild's dues

The days come in weeks of five. At the end of each week's fifth day Mister Vane, Collector for the Gilded Guild, comes
for the week's tithe (`Guild.Due`, `DuesPhase`). Bram's debt is eight weeks long; after that the Guild keeps coming for "interest",
half as much again each week, for as long as you last (endless).

| Week | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | then |
|---|---|---|---|---|---|---|---|---|---|
| Due | 40g | 100g | 300g | 800g | 1,400g | 2,400g | 3,800g | 7,000g | x1.5 a week |

(v13; v14's are below.) These follow what a careful shop earns on its own judgment, now that no curio or synergy adds
a share to sales (v10 and v11 climbed to 26,000g, which only the +50% curios made payable). The last week is the wall:
twice the week before, so the debt's last two weeks (10,800g) are rarely in hand before week 8.

**v14: dues for a career, not a run.** With characters, challenge levels and talents, the question changed from "does
a careful player clear the debt?" to "how does a player get from their first run to their first clear, and on up the
levels?" (see *Balance across a career*). The dues now start lighter and finish steeper:

| Week | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | then |
|---|---|---|---|---|---|---|---|---|---|
| Due | 40g | 100g | 200g | 500g | 1,300g | 2,800g | 6,000g | 12,000g | x1.5 a week |

The first four weeks ask for little: a first-timer's purse grows and they get through them (v13's 300g in week 3 sent a
third of them to Bram's word, whose interest then sank them in week 4). From week 5 the dues climb faster than a
learner's shop grows; week 8 is the wall that only good judgment with some talents behind it gets over.

**Paying it off early.** Whenever you have all that's left of the debt (`GameState.DebtLeft`: this week's due if it's
unpaid, and every week's after it to the eighth), you can pay it at once: from the day-end screen's Guild panel, which
shows what's left and turns into a "Pay it all off" button (with a confirmation), or on the Guild's night beside the
week's due. The debt is cleared and the run is won, however early (`ClearedDay`; the profile keeps your fastest).
Keep trading after, and nobody comes until week 9, when the Guild's interest starts. A careful player usually pays
the eighth due on the night; a lucky run has it all a week early, and nobody yet has managed two.

The first weeks are small while you learn the stall and the village; from week 4 the dues climb with what a growing
shop can earn, so every evening's spending is a decision: a renovation or cabinet now pays back over days, but the
Guild comes on a fixed night. Once Vane has introduced himself (the first evening; nothing mentions the due before
then), the HUD shows the next due and when, and the day-end screen shows it against your gold.

Spending is guided, not blocked. The day-end screen says how much to keep for tomorrow's chests
(`ShopTiers.Working`: 20g at the stall, 30g in the village, 100g in town, 250g at the Emporium, counting the shop a
renovation would make), and an upgrade that would leave less than that, or leave you short for the Guild tonight, has
an orange price and a tooltip saying why. The sim's careful player keeps the same purse (`Strategy.Reserve` adds it
to what the next due needs), so Bram's advice is what good play does: buying the village at 120g and opening it with
60g is fine, because a careful player passes on the dear chests and buys the bargains until the purse grows. (A
safer-sounding 70g made the sim renovate three days later and clear the debt a tenth less often.)

**Bram's secret stash** is the safety net for a beginner who spends too much or buys badly: start a day with less than
20g at the stall and he slips you 30g; 40g/60g in the village, 80g/120g in town, 150g/250g at the Emporium. Three
times a run (`GameState.StashLeft`); the day card says so, with how many are left, and Bram explains it the first
time. The careful player hardly needs it (about once in two runs).

Short on the night: pawn what's on display (at 60% of its price) or sell curios back (half what you paid). Once a run,
if even that won't do, Bram vouches for you: everything you have goes to the Guild and the rest, with half as much
again, is added to next week's due (not in week 8: the last of the debt is the last). Otherwise the Guild takes the
shop and the run is over. A short night is also the most dramatic moment the shop has: what do you give up first?

## Curios

Nim the peddler (horned, cheerful, half-something; nobody asks) drops by before opening once a week from week 2
(`PeddlerPhase`; Nim's first visit is the morning after the Guild's first) with three curios, oddments that came out of
Locking chests and bend a rule of the trade (`Curios.Stock`: rarer ones more often as the weeks go by). Each bends one rule of
and you have room for three (a Curio Case, 250g in Crownhold, and a Velvet Case at the Royal Emporium add one
each), so which ones you keep shapes how you play. None of them tells you what a chest holds before you buy it, and
**none adds a share to what things sell for, or takes one off what you pay**. The shop's long game is your purse
growing on good judgment, chest after chest; a few percent on every sale compounds into weeks (v10 and v11 had
+50% on whole kinds of things, +50% on everything with a cursed crescent, a fifth off every due: the sim's careful
player cleared the debt 69% of the time with curios and synergies and never without them). Now a curio changes what
you can do, what luck does, or what happens on a bad night.

**Prices follow the economy.** A curio costs a share of the week's due (common a tenth, uncommon a quarter, rare
three-fifths: 30, 75 and 180g in week 3, 700, 1,750 and 4,200g in week 8), and the dues follow what a shop that's
keeping up makes, so a curio costs about the same share of your means early and late. On Nim's first visit the commons
are 1g, and he always has one. He buys curios back for half what you paid (so one bought cheap doesn't sell dear later).

| Curio | | Does |
|---|---|---|
| Lucky Coin | common | the first chest you open each day: everything in it gets two tries at its quality |
| Lucky Shell | common | a dry spell ends twice as soon: the pity starts after 7 plain finds, not 15 |
| Shop Lantern | common | one more customer on the fifth day of each week |
| Collector's Catalogue | common | every new find in the journal pays 10g per shop tier |
| Padded Crate | common (Thornwick on) | glass never breaks when you drop a chest |
| Locksmith's Oil | common (Thornwick on) | every lock has one pin fewer |
| Rainy-Day Purse | common | Bram's stash has one more dip in it |
| Pawn Ticket | common | on a short night, what you pawn fetches four-fifths of its price, not three-fifths |
| Merchant's Signet | uncommon | hagglers hear you out: an offer too low gets a second try, not a slammed door |
| Savings Jar | uncommon | nightly interest: 5% of your gold, up to 25g per shop tier |
| Letter of Introduction | uncommon | regulars take what you deliver a quality lower than they asked for (not the Hooded Woman) |
| Burglar's Knife | uncommon (Thornwick on) | forcing a lock breaks and scratches nothing |
| Smuggler's Hook | uncommon (Thornwick on) | false bottoms turn up a little more often (+5%) |
| Witch's Draught | uncommon (Crownhold on) | enchanted things turn up a little more often (+3%) |
| Pocket Watch | uncommon (Royal Emporium) | impatient sellers give you twice as long |
| Gambler's Token | uncommon | luck swings both ways: things come out better or worse than their chest twice as often |
| Cursed Crescent | uncommon | no surprises: everything comes out exactly as its chest promised, never better or worse |
| Guildsman's Marker | uncommon | Bram's word is good twice a run |
| Smith's Glove | uncommon | arms never come out worse than their chest promised (you mend them) |
| Hatter's Cap | uncommon | garb never comes out worse than its chest promised |
| Jeweller's Loupe | uncommon (Royal Emporium) | paste gems and demon gold sell as costume pieces, for a quarter of the real thing |
| Four-Leaf Clover | rare | the first chest you open each week: everything in it is a step better |
| Visitors' Book | rare (Thornwick on) | every night, 2g per exhibition point |
| Kid Gloves | rare (Thornwick on) | nobody minds how you handle their chest: shake hard, drop it, use the lodestone (a rotten chest is still rotten) |
| Bell Jar | rare (Thornwick on) | room to display one more thing |

The token and the crescent are a gamble and a safe bet: the token's swings are slightly better than even, the
crescent's certainty slightly worse, and the sim's careful player buys neither. Effects live in `GameState` (`Run.cs`
and `Shop.cs`); what a curio's luck did at purchase, the opening announces ("Lucky Coin: 2 things came out better!",
"Smith's Glove: 1 thing mended.").

## Commissions

From week 2, three regulars you've met post a commission each week (`Patrons.Roll`), good until the Guild's next
visit: one thing of a kind at a quality (a step better than the shop's chests usually are), two or three of a kind
(a step lower), or (from Crownhold) anything enchanted. They pay two or three times what such a thing sells
for. A board under the seller lists them, the slip marks the kinds they want with a gold pennant, and anything that
comes out of a chest you open can be delivered instead of sold. A commission makes a sound worth more than its price:
a chest with a flump in it is worth chasing when Penn wants silk.

## Events

Once a week from week 2, on a day of its own, something happens in town (the day card announces it, and a chip in the
top bar keeps it there all day: click it to read it again; `Events`):

| Event | That day |
|---|---|
| Fair Day | three more customers |
| Locking Day | three hundred years since the Locking: everything sells for a quarter more |
| The Crown's Buyer | one kind of thing sells for double |
| A Royal Chest | someone brings one of King Aldric's own chests, gilded with the royal seal: a colour better than its seller thinks, with something enchanted inside |
| The Stranger | an extra customer with a black chest wrapped in iron chains: nothing rattles, but a drop tells you how heavy it is (heavy ones hold more: often junk, sometimes a fortune) |
| A Storm | two fewer customers, but every chest a colour better |
| A Barge Aground (Crownhold on) | a treasure barge on the Aldwater: lots come in twos and threes |

They all come round before any comes twice.

## The story

The world's story (`docs/world.md`) is told in scenes (`ScenePhase`, `Story`) on the first evening, at the start of
some weeks, on the Guild's nights and at the end. Each scene answers one question and leaves the next one open:

- The first evening: Mister Vane of the Gilded Guild. Every Keywright pays the Guild a tithe "to keep the Gate"; Bram
  opened chests unlicensed for twenty years, and the back-tithe comes with the stall, in eight weekly payments.
- Week 2: Bram tells you what the chests are (the Locking, King Aldric, the Maw behind the Gilded Gate) and gives you
  his one secret, a shard of the Aldric Key, broken in four when the Locking was made.
- Weeks 3, 5 and 7: the Hooded Woman asks for something only a Keywright's chests hold (scrolls, a gem, something
  enchanted; or coin or a blade while the shop is small, each with its reason) and pays a shard for it. Miss one and
  she asks again the next week, until the debt's last week, even if the debt's already paid off.
- Week 4: Bram knows her voice; the Guild has been buying every chest with a royal seal. Week 5: the whole Key opens
  the Crown Chest, King Aldric's own, which holds the heart of the Locking; the debt is a leash.
- Week 6: Vane offers to halve that night's tithe for your shards. Take it and there's no Crown Chest.
- Week 7: she is Mara, the last archmage of the Keyhold and Bram's old partner. Every tithe goes down through the Gate:
  the Maw is buying his way out, and every chest opened loosens the Locking. At the Royal Emporium, Bram spots the seal
  on Old Aldo's ring: the tired old man who's been playing his game with your slip is the King.
- Debt paid, with the Key whole: the King gives you the Crown Chest, so it opens for you (a gilded chest with the best of
  everything inside, opened like any other), and the Locking holds: the Gate goes quiet. Then you keep trading
  against the Guild's interest, or retire. Paid off early without the whole Key, you can keep trading: the Guild
  leaves you alone until week 9, and if Mara's last shard comes, the Crown Chest opens that night.
- Foreclosed: the Guild seals the shop, and your licence with it.

Internally the shards are still `MapPieces` and the Crown Chest is the vault (`VaultOpened`, `--vault-demo`), so
saves and profiles carry over. Every run is recorded in `user://profile.json` (runs, debts cleared, Crown Chests
opened, the furthest week), shown on the title screen and at the end of a run.

## Across runs: Bram's kin, challenge levels and talents

A run's own loop (a chest, a day, a week, eight weeks) needed a reason to play another run that isn't the same run
again. Three things carry over, all in `scripts/Core/Meta.cs` (definitions, the score) and `Kin.cs` (what they do to a
run, as a partial of `GameState`); the profile (`Profile` in `Save.cs`, user://profile.json) keeps renown, talents, each
character's open levels and best score.

**Bram's kin** (`Characters`). Each changes one part of how you judge a chest or run the shop, and pays for it; the rest
of the game is the same. They're all Bram's family, so the story's lines ("your uncle") hold for each.

| Who | Unlocked by | What's different |
|---|---|---|
| Kit, the Ear | from the start | the game as it was: every strength, the drop, and (with tips on) Bram's lessons as each seller arrives |
| Hesk, the Scale | paying the third week's due | a bad back: no hard shake and no drop. Every chest sits on his scale, and he knows what each kind weighs (coin 4 lb, arms 6, garb 3, potions 3, scrolls 1, gems 1); demon gold weighs half what gold does and a case of sand three times a scroll, so the weight left over once the heard things are counted is what he couldn't hear, what's under a false bottom, or a fake weighing short |
| Rosa, the Tongue | clearing the debt | everybody haggles with her (fixed-price sellers ask high and hear one offer instead, though never more than a bit over the price they'd have named, nor holding out for more than it; a crook still names his too-good price, and nobody haggles with the King or the Maw), but they know her: they open 3% higher and come down 3% less; an offer too low gets a second try; a seller who walks out on her doesn't come back that week |
| Isolde, the Collection | opening the Crown Chest | a two-place shelf from the stall on; visitors pay 1g per exhibition point every night (not more in a grander shop: that version cleared days early); her collection can't be pawned, so a short night can only take her curios |

Every new judgment follows a rule people know: things weigh what they weigh, and gold is heavier than iron (the old
test for a gilded crown). Hesk's scale shows the total and the slip shows what the ticked things would weigh; the sum
is the player's to do. The careful player's read is `Strategy.Weigh`: the likeliest mix of garb, scrolls, a false
bottom and short-weight fakes for the leftover, given the seller's habits. Bram's lessons are Kit's (the others come
knowing the trade, `GameState.GivesLessons`); each of the others gets a first-morning tip on their way (`kin_*` in
`Hints.cs`). Curios a character can't use aren't in Nim's stock (`GameState.NoUse`: no padded crate for Hesk, no
signet for Rosa, no visitors' book for Isolde).

**Challenge levels** (`Challenges`). Eight, each adding one term on top of the ones below, opened one at a time per
character by clearing the debt at the level below. Each adds a quarter to the renown a run earns (x3 at level 8).
Terms are rules that hold every time, not hidden rolls:

| Level | Term |
|---|---|
| 1 | Bram's stretched thin: his stash has one dip fewer, his word one time fewer |
| 2 | Fussy sellers: everyone minds a drop (price up by half) |
| 3 | Demon gold about: one coin in twenty is demon gold, whoever brings it |
| 4 | Lean start: half the starting gold |
| 5 | Thin crowds: one customer fewer every day at Bram's stall |
| 6 | The Guild's interest: every due a tenth higher |
| 7 | Sharp sellers: everyone asks, and takes, a twentieth more |
| 8 | Hurried: every seller gives you 20 seconds |

They're ordered mildest first by what each does on its own to the careful player with every talent (the sim's
`Trial`, one term at a time; v14's figures are in *Balance across a career*): the stash and the word matter to people
who get into trouble more than to the careful player, so they come first; a thinner margin on every chest is the
hardest, because the last weeks need every coin the shop's growth can compound.

**Score and renown** (`Score`). Points for the Guild dues paid (10 each), the debt cleared (100) and every day to
spare (5), the Crown Chest (100) or the Key's shards held (10 each), moves (25 each), the collection (2 per find) and
every week after the debt you kept the Guild paid (25). A foreclosed run still scores what it did, so every run moves
you on. Renown is the score times the level's multiplier, banked when the run ends (retiring, foreclosure, or starting
over on a saved shop); the run's end shows the lines, the renown and anything it opened.

**Talents** (`Talents`). A root and three branches of four, each talent building on the one above it (13 in all,
5,600 renown). Like curios and synergies, **no talent adds a share to what things sell for or takes one off the
dues**: they soften a bad night, widen your choice of curios, or open the shop's name sooner.

| Branch | Talents (cost) |
|---|---|
| (root) | Bram's Kin (50): start with 10g more |
| Bram's Lessons | Rainy Day (100): a fourth stash dip; Pawnbroker's Friend (250): pawning at 70%; Good Word (500): Bram vouches twice; Mara's Favour (1,000): once a run, Mara pays a night nothing you own can cover (not week 8) |
| Nim's Favour | Familiar Face (100): four curios on Nim's counter; Second Look (250): once a visit, another four; Keepsake (500): on Nim's first visit, one curio of your choice is free; Curio Cabinet (1,000): a fourth curio slot |
| The Trade | Bram's Ledger (100): every seller's habits in the ledger from the start; Early Name (250): synergies from Thornwick; Trade Secrets (500): the witchlight and the lodestone at half price; Royal Favour (1,000): one more synergy slot |

Two first drafts sped the run up rather than widening it, and went: Keepsake as a free common curio on the first
morning (alone, +8 points of clears; with Early Name and Old Picks, free fine lockpicks from the start, +22), because a
curio or 50g on day one compounds through the whole run. A curio of your choice on Nim's first visit, and the sellers'
habits in the ledger, widen choices without moving the timeline as much.

## The shop's look

Each move changes the shop around you: Bram's trestle table under a striped awning at the Brindle crossroads; a
plastered shop at Thornwick Market with timber beams and oil lamps; a panelled shop in Crownhold whose display window
looks over the square at sunset; the Royal Emporium's colonnade, crimson drapes and marble counter, in the palace's
shadow. Each is one 640x360 picture made by
`tools/import_art.py` (a backdrop from the pack, architecture drawn as pixel art, a few of the pack's props, and the
counter), shown at 2x behind the counter and dimmed behind the lockpicking, opening and day-end screens
(`UiKit.MakeShop`). What you keep stands on the shelf (the window sill in town).

## Title, saves and settings

The game opens on a title screen (Continue, New game, Talents once there's a run behind you, Settings, Quit) over
your shop as you left it. New game opens the character screen (`SetupPhase`): the four of Bram's kin (locked ones say
what unlocks them), the chosen one's rules, and the challenge level with its terms and multiplier. The shop is
saved to `user://save.json` at the start of each day (`GameState.Save` and `Load`, plain JSON from Core), so leaving
mid-day starts that day over; the pause menu (Esc, or Menu in the top bar) says so. Settings (`user://settings.cfg`):
volume, fullscreen, screen shake and flashes (full, reduced or off), Uncle Bram's tips, and **item sounds**: for each
kind of thing, which of three or four sounds it makes (`Audio.SoundSets`; recorded ones from Kenney's CC0 packs via
`tools/import_art.py`, a few synthesized in `Sfx`). Each kind's sound stays the one you'd expect in real life (a
purse, a pot, a thump of cloth, a bottle, pages, glass), so any set keeps the rules readable; the choice is taste.
Click a kind's option to hear the next one.

## Collection, synergies and exhibition

Every thing you open goes in the ledger's collection the first time you find it (33 entries: five qualities of six
kinds and three fakes; finding one enchanted stars its entry) and in the journal's tally of what you've acquired,
kind by quality. Each thing looks the same everywhere: the collection, the shelf, the exhibition and the keep chooser
all show its own sprite (`UiKit.MakeItemIcon`, from icons `tools/import_art.py` halves when a sprite is too big).

**Synergies** turn that tally into goals. Each has five levels; ten of a kind at common or better reaches level 1,
ten at uncommon or better level 2, and so on to ten legendary for level 5. Two are achievements instead. A synergy is
what the shop gets known for, and a name works the way it does in real life: it brings people and chances your way
and spares you some breakage. It never adds a share to what things sell for (v7 to v11 had +3% to +15% on every sale,
or a fee of 3% to 15% of what a perfect read's chest held; on margins of a few percent, that grew the shop far faster
than judgment did).

| Synergy | Needs | Does (per level) |
|---|---|---|
| Goldsmith | coin and gems | coin and gems turn up in chests 10% more often |
| Armourer | arms | soldiers, knights and dwarves (who bring arms) come 20% more often |
| Apothecary | potions | glass 20% less likely to break, dropped or forced (never, at level 5) |
| Archivist | scrolls | chests 2% likelier to have a false bottom |
| Clothier | garb | exhibition +10% |
| Keen Eye | perfect reads in a row (3, 6, 10, 15, 25) | a perfect read earns a valuer's fee of 1g per shop tier |
| Arcanist | enchanted things found (3, 8, 15, 25, 40) | each thing 1% likelier to be enchanted |

Only **one** synergy is active at a time, chosen at the end of a day (the day-end screen's Synergies button opens the
journal with switches). A Crownhold Charter (300g) and then a Royal Seal (900g, Royal Emporium, only once you have the
charter) add a slot each. The Synergies button is on the day-end screen from the moment you move to Crownhold, even
before any synergy is reached ("Synergies: none reached yet"), so Bram's tip always has something to point at. That makes a
specific kind of chest worth wanting: a player chasing Goldsmith level 3 wants blue chests with coin and gems (a
noble's strongroom, the Maw's hoard), and will pay up for them.

After an opening, anything not fake or broken can be **kept for display** instead of sold: you give up its price. The
button shows the points it adds (a star for enchanted, which counts double). With the display full, it becomes **Swap**:
pick something on display to sell, and the new thing takes its place. Display space: a shelf with room for one, then
six cabinets of one each, each on offer only once the one before it is bought: Oak Cabinet 60g and Oak Dresser 120g
(Thornwick), Glass Cabinet 250g and Glass Case 400g (Crownhold), Gilded Cabinet 700g and Gilded Case 1,100g (Royal
Emporium); a Bell
Jar (a curio) holds one more. The **exhibition** score is the display's prestige (1, 2, 4, 8 or 16 by quality; double
if enchanted). Every 10 points brings a customer a day (up to two), and each point makes a chest 1% likelier to be a
colour better (up to 40%). Past 40 an exhibition brings nothing more, unless visitors pay to see it (the Visitors'
Book curio).

## Stages

| Place | Cost | Customers/day | Chests | New |
|---|---|---|---|---|
| Bram's Stall, the Brindle crossroads | start | 4 | mostly wood | jingle, clank, flump; three strengths; clanks drown jingles; from day 2, haggling, naming a price, counting rattles, stories; Dorrin's rotten Barrow chests |
| Thornwick Market | 60g, once the stall has earned 120g | 5 | mostly green | slosh and rustle; locks; false bottoms and the drop; the display shelf and cabinets; Snikkit's fee, Wenna's weight, Bramble's dreams, Ansel's crypt, Fennick's false bottoms |
| Crownhold | 300g | 6 | mostly blue | gems; two or three of a kind (Pell); sand (Shelly); enchantment and the Witchlight; synergies and the Crownhold Charter; Wren, Orrin's wager, Grit, Corisande's trick boxes, Sir Aldous's heirloom |
| The Royal Emporium | 2,000g | 7 | blue and purple | Vexis's hourglass, Malbranche's due, Skrae, Silas's demon gold, Ermine's paste, the King's game, the Maw's gaze, the Lodestone, the Royal Seal |

The Painted Sign (40g) adds a customer a day; the Curio Case (250g, Crownhold) room for a fourth curio. The Royal
Emporium has three big sums to grow into while the dues climb: the Night Market (2,500g, two more customers a day), the
Patrons' Book (4,000g, a fourth commission a week) and the Velvet Case (6,000g, a fifth curio, after the Curio Case).
Day one is scripted: four customers, one lesson each (a gentle shake and the slip; a hard shake for a coat; listening
past a clank; passing when they heard everything).

## Evidence (`godot --headless --path . -- --sim`, 300 runs per style, up to 12 weeks)

Each style plays whole runs as Kit at level 0 with no talents: buying chests, curios (a careful player's taste,
keeping enough back for the Guild) and upgrades (keeping Bram's working purse), delivering commissions, paying the
Guild (pawning, or Bram's word, when short), taking Bram's stash when a day starts nearly broke, and paying off the
whole debt as soon as it can. These are the perfect listeners; how real players get there is in *Balance across a
career*, below. v15 rebuilt the cast and the kinds on top of v14's curve and tuned the new sellers until it held:

| Style | Still trading after each due, weeks 1-8 | Debt cleared | Crown Chest | Foreclosed (median week) | Thornwick | Crownhold | Royal Emporium |
|---|---|---|---|---|---|---|---|
| Pays their price, every chest | 100 97 81 40 18 8 3 0% | 0% | 0% | all (week 4) | day 6 | never | never |
| One hard shake, keeps the seller's marks | 100 100 99 95 82 60 51 0% | 0% | 0% | all (week 8) | day 5 | day 13 | never |
| Every strength, drops when safe | 100 100 100 100 97 91 87 1% | 1% | 1% | all (week 8) | day 3 | day 11 | day 17 |
| + Witchlight and Lodestone | 100 100 100 100 98 92 91 19% | 19% | 19% | all (week 8) | day 3 | day 11 | day 17 |
| + keeps grand things | 100 100 100 100 96 83 82 41% | 41% | 41% | all (week 8) | day 3 | day 12 | day 21 |

("Foreclosed" counts the Guild's endless interest after the debt, which catches every shop by week 10.)

With no talents, even a perfect ear clears the debt less than half the time, and only with an exhibition: what you
keep draws the customers and the better chests that the last two weeks need. Week 8 is the wall (12,000g, twice
week 7); a careful player who sells everything has a median 6,863g on that night, a collector 9,478g.

Week by week for the careful collector (gold on the Guild's night, before paying):

| Week | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Due | 40 | 100 | 200 | 500 | 1,300 | 2,800 | 6,000 | 12,000 | 18,000 | 27,000 |
| Gold, 10th percentile | 84 | 176 | 320 | 611 | 760 | 693 | 6,526 | 5,748 | 11,184 | 4,197 |
| Gold, median | 123 | 325 | 669 | 1,619 | 3,421 | 6,799 | 9,982 | 9,478 | 15,210 | 9,523 |
| Paid | 100% | 100% | 100% | 100% | 97% | 86% | 99% | 33% | 46% | 3% |
| At the Royal Emporium | 0% | 0% | 4% | 39% | 69% | 79% | 93% | 94% | 100% | 100% |

A careful collector fills about 21 commissions a run (some 7,600g), Bram vouches in a fifth of runs (usually week 5,
for collectors who spent on cabinets, or are still at Thornwick: about one in six is, in week 4), and his stash opens
less than once a run. The new sellers have to be wired into the day for that to hold: Dorrin and Snikkit come in half
the days, Old Aldo one in four, the Maw one in ten (`SellerDef.Visits`). Before that was wired up, the Maw's one-coin
chest (about 600g of profit) came as often as anyone, and the Royal Emporium paid a third more per customer than v14's.

What curios and synergies are worth, to the careful player with both tools who sells everything:

| | Debt cleared | Median day cleared | Median gold on the night, week 4 / 6 / 8 |
|---|---|---|---|
| With both | 19% | 40 | 2,096 / 6,447 / 6,863 |
| No curios | 3% | 40 | 1,767 / 5,416 / 4,926 |
| No synergies | 6% | 40 | 1,748 / 5,140 / 4,543 |
| Neither | 0% | - | 1,522 / 4,597 / 3,256 |

On a steep curve every edge matters; they still don't add a share to sales, and those who clear with them do so on
the eighth night, not weeks sooner.

Surprises: a careful player sees 8% of things come out a step better than their chest and 0.5% two or more, worth 14%
of what the chests held.

Per seller, at their own place, profit per visit for a careful player with every tool, against paying their price
blind. Every habit and every rule pays for putting up with it; only Tam (a few coins for anything), Wren (a modest sum
when nothing's enchanted), Mags, Wenna and Mother Bramble pay a little blind, and the Maw's coin pays anyone (its
price is the rest of the day, which the table doesn't count):

| | Tam | Penn | Hob | Mags | Dorrin | Snikkit | Wenna | Bramble | Ansel | Fennick | Wren | Orrin |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Careful | +10 | +5 | +9 | +6 | +2 | +7 | +15 | +13 | +15 | +23 | +30 | +72 |
| Blind | +10 | 0 | 0 | +3 | 0 | -3 | +3 | +2 | -1 | -23 | +8 | -24 |

| | Grit | Corisande | Pell | Shelly | Aldous | Vexis | Malbranche | Skrae | Ermine | Silas | Aldo | the Maw |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Careful | +125 | +33 | +104 | +24 | +31 | +44 | +31 | +77 | +34 | +84 | +112 | +599 |
| Blind | -404 | -7 | -12 | -26 | -9 | -258 | -68 | -17 | -69 | -19 | -239 | +608 |

The careful player plays the King's game only on a read it could check every way (every strength and the drop): a
wrong read costs a quarter more than the chest held, and Hesk, who can't shake hard or drop, never plays it. It buys
the Maw's coin when the chest's worth beats what the rest of the day would cost (what's still waiting, doubled, at the
share it would buy anyway).

Every seller's lesson completes, played the careful way, with and without the tools of their place.

## Balance across a career

v13 gave runs a meta-progression; v14 sets the balance for a career, not a run, and v15 keeps that curve through the
new world (every number below is v15's). The goal, in the owner's words: no
pressure before players have learned the puzzles, no easy win in the first few runs, a purse that keeps growing but not
steeply enough for the middle and late dues, and judgment and talents together pushing the curve up until a run
clears; then the challenge levels.

**Learners.** The sim's careful player is a perfect listener. A learner (`Style.Skill`, 0 to 1) plays the same way
with a learner's mistakes, each shrinking as skill grows: at skill 0 they skip each strength 40% of the time, count a
pair as one half the time, believe a seller's claim a third of the time, forget the drop half the time (and drop
glass a quarter of the time), forget the lodestone, miss a glow or a boom on the slip, judge a chest's worth with a 45%
spread either way, pick locks worse, and buy curios on a whim. Skill 0 is a first-timer who has had Bram's lessons:
they know the rules but don't yet apply them every time.

| Skill (Kit, level 0) | No talents: cleared, still trading after weeks 4 / 5 / 6 / 7 | Every talent |
|---|---|---|
| 0.15 (a first run) | 0%; 89 / 61 / 41 / 39%, lost in week 6 | 3% |
| 0.5 | 5%; 96 / 83 / 61 / 57% | 15% |
| 0.75 | 19%; 99 / 91 / 73 / 73% | 50% |
| 1 (the careful collector) | 43%; 100 / 95 / 83 / 82% | 81% |

**Careers.** Each simulated player starts at skill 0 to 0.3 and improves toward their own best (0.75 to 1) at their
own pace (a tenth to three tenths of the way each run); every run is Kit's at the highest level open, scored at the
end of week 8 or when the shop is lost, and its renown spent on the cheapest talent going. The pace of learning is a
guess; what the sim shows is the shape (`--sim`, 40 players over 30 runs):

| Run | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 12 | 16 | 20 | 24 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Skill | 0.15 | 0.29 | 0.41 | 0.50 | 0.57 | 0.63 | 0.68 | 0.71 | 0.74 | 0.77 | 0.80 | 0.84 | 0.86 | 0.87 |
| Cleared | 0% | 0% | 0% | 8% | 8% | 18% | 18% | 20% | 23% | 23% | 25% | 33% | 43% | 30% |
| Lost in week (median) | 6 | 7 | 7 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 |
| Challenge level | 0 | 0 | 0 | 0 | 0.1 | 0.2 | 0.3 | 0.5 | 0.7 | 0.9 | 1.4 | 2.6 | 4.0 | 5.4 |
| Talents (of 13) | 1.6 | 3.1 | 4.1 | 4.9 | 5.7 | 6.6 | 7.2 | 7.8 | 8.4 | 9.0 | 10.0 | 11.6 | 12.8 | 13 |
| Renown | 167 | 172 | 163 | 199 | 222 | 254 | 270 | 279 | 310 | 343 | 374 | 436 | 583 | 628 |

The first run gets through the first five weeks and loses in week 6 with a growing purse that wasn't growing fast
enough; the next few get to the last weeks and lose there; the debt is first cleared around the ninth run (a quarter
of players by run 6, half by 9, three quarters by 12), with about eight talents learned. Then the levels: level 1
around run 13, 2 around 16, 4 around 21 (95% of players by run 30), 6 around 28 (55%); level 8 is cleared by one in
twelve by run 30. The whole talent tree is learned around run 21, as the climb starts in earnest; renown keeps the
score of how far a run got. A new talent comes after nearly every run early on.

Getting there: v13's dues (40, 100, 300, 800, 1,400, 2,400, 3,800, 7,000) let the careful player with no talents
clear 61% of the time and half of all careers clear by their fifth run, with a third of first-timers already short in
week 3 (Bram's word then, his interest sinking them in week 4). Raising the late dues alone (to 10,000) made it harder
without making it gentler; making week 7 heavy did nothing (anyone short in week 7 leans on Bram's word, and its
interest lands on week 8). Lighter early dues with a heavier last week did both.

**The challenge levels**, ordered by what each term does alone to the careful player with every talent (81% on the
usual terms; v15's figures): Bram stretched thin (81%), demon gold about (73%), fussy sellers (72%), thin crowds at
the stall (69%), a lean start (67%), the Guild's interest (67%), sharp sellers (61%), a hurried town (59%). The new
world's hurried town is the harshest (twenty seconds for a goblin's fee, a rotten chest and the King's game), so it
moved from level 6 to 8; the rest kept their places (within a few points of each other, which is noise at 150 runs).
All together, level by level: 81, 81, 72, 67, 61, 43, 31, 13, 6%. The first levels are an on-ramp for a careful player
and bite a learner who still leans on Bram's stash and word and drops glass.

**Bram's kin** on the new curve (the careful collector, 150 runs each; cleared %):

| | No talents | All, level 0 | Level 2 | Level 4 | Level 6 | Level 8 |
|---|---|---|---|---|---|---|
| Kit | 43% | 81% | 72% | 61% | 31% | 6% |
| Hesk | 39% | 76% | 76% | 59% | 24% | 14% |
| Rosa | 43% | 79% | 69% | 54% | 28% | 5% |
| Isolde | 45% | 80% | 72% | 60% | 32% | 5% |

v14 set Rosa's wariness at a twentieth; v15's sellers who'd name a cheap price (Dorrin, Wenna, Malbranche) made
haggling dearer for her than their price, so now a seller who'd name a price never holds out for more than it with
Rosa, and her wariness is 3% (`Market.RosaWary`, `ConvertedAsk`, `ConvertedFloor`). Hesk loses the King's game (he can
never be sure enough of a read to play it) and wins at the top, where he shrugs off two of the terms (he never drops,
and his scale catches demon gold).

## What was cut, and why

1. **Auction (v1).** The game did the judging, and bidding against one bot had a dominant strategy.
2. **One unique sound per item (v2).** 28 sounds to memorize, with no logic connecting a sound to a thing.
3. **Sound families with variants, many tools, haggling, lockpicking (v3).** Too much at once, and profit came from
   habits the player couldn't see.
4. **v4** rebuilt the puzzle on shake strength. It was clear, but every profit was predictable and every seller
   alike. **v5** keeps v4's three rules and adds variety that follows its own believable rules: quality rolls around
   the chest's colour, three ways to trade, sellers with one habit and one reward each, the drop, lockpicking, and a
   collection worth keeping. **v6** gives each seller their own way of judging a chest, so each visit is a different
   puzzle. **v7** adds layers of deduction one at a time (padding and the ear trumpet, counting, sand), a lesson for
   each seller's first visit, a gut feeling instead of opening a chest you passed on, openings that build to their
   surprises, kinder quality odds with longer upward tails, a slip that reads at the chest's colour, and synergies
   that give the collection goals. **v8** makes padding certain (any cloth pads, so the rule never seems to misfire),
   makes enchantment something any thing can be, replaces "worth about" with an odds bar, colours the gut feeling,
   shows a worse-than-expected find as a letdown, and stops lessons and tips talking over each other. **v9** drops
   padding, opens the shop up a step at a time (renovations once the stall earns 120g, the display shelf with the
   village shop, synergies with the town shop, each introduced by Bram), gives every shop its own look, and adds the
   title screen, daily saves, pause menu and settings. **v10** turns the shop into a run: the Grey Guild's weekly dues
   (eight weeks, then endless), curios from Nim, commissions, an event each week, the Stranger's sealed chests and the
   gilded ones, and the story of King Aldric's vault. **v11** makes surprises rare (with a pity counter for long dry
   spells), adds Bram's secret stash as a beginner's safety net, guides early spending (a working purse, orange
   prices), makes commons 1g at Nim's, chains the cabinets and synergy slots one after another, keeps the day's
   event in the top bar, lets you choose each kind's sound, and, after a beginner's playtest, shows how hard you're
   shaking.
   **v15** rebuilds the world around the game (`docs/world.md`): the Locking explains the chests, the Key-mark why
   only you can open them, the Gilded Guild's tithe the debt, the Aldric Key and the Crown Chest the story; the shop
   moves inward through four places; the kinds become coin, arms, garb, potions, scrolls and gems; every chest has an
   origin and every seller a story about it, true or tall; and the cast is rebuilt by place, with rules of their own
   (a rotten chest, a fee for shaking, a wager, an heirloom, a devil's due, the King's game, the Maw's gaze).
5. **Opening a passed chest (v5 to v6).** Immediate feedback, but it made no sense: they're taking it home. The gut
   feeling keeps the feedback and keeps the chest closed.
6. **Cloth padding (v7 to v8).** Cloth packed around hard things made them sound one shake harder (with an Ear Trumpet
   to hear through it). First it was a hidden roll, so it seemed to work only sometimes; made certain in v8, it was
   still too unintuitive to reason about. Gone in v9, trumpet and all; Granny Wick's puzzle is now her sewing.
7. **Magic as its own kind of thing (v5 to v7).** A magic thing had its own name and sprite and showed one colour
   better than its quality, so a purple chest's "gold" find could arrive without the upgrade fanfare, and its picture
   didn't match anything in the journal. Enchantment is now something any thing can be.
8. **The Royal Warrant as the goal (v5 to v9).** A 5,000g upgrade and an exhibition of 40, with nothing pressing until
   then: the days felt the same from the town shop on, and nothing ever went wrong. v10 replaces it with the Guild's
   weekly dues (pressure that climbs), curios, commissions, events and a story with a vault at the end.
9. **Percentage bonuses from curios and synergies (v7 to v11).** +50% on coins and jewelry (or metal, or cloth and
   paper), +50% on everything with a cursed crescent, a fifth off every due, +3% a level on every sale, a fee of 3% a
   level of what a perfect read's chest held, commissions +50%, one chest in six a step better. A careful buy's margin
   is a few percent of what the chest holds, so a few percent more on every sale multiplied how fast the shop grew:
   the sim's careful player cleared the debt 69% of the time with curios and synergies and never without, and the
   dues had climbed to 26,000g to keep up. v12 makes curios rule-benders and synergies a name that brings chances
   your way, and sets the dues by what judgment earns: take every curio and synergy away and a careful player
   finishes about a day later.

10. **Made-up sounds and sworn false bottoms (v6 to v14).** Tobias the Tinker claimed a sound that wasn't there, and
    Captain Gull always priced in a false bottom. Both were the same puzzle as everyone else's (check it yourself) with
    a hidden roll on top. v15 moves unreliability to where people really lie: where a chest came from. The place is a
    clue (a barrow holds grave goods), the story is true as often as the seller is honest, and the truth comes out when
    the chest opens. The old cast went with the old world: Sergeant Brask's weight is Wenna's, Old Marta's forgotten
    false bottoms are Fennick's smuggling chests, Granny Wick's unheard sewing is Brother Ansel's vestments, Madame
    Zara's trick boxes are Lady Corisande's, Baron Vex's clock is Vexis's hourglass, Pip's sharp ears are Skrae's, and
    Magister Orrin's dreams went to Mother Bramble while Orrin took up a wager.

## Open questions

- v15's stories: do players read them, and use a place as a clue (a barrow's grave goods, a goblin warren's coin)? Is
  the ledger's tally of each seller's honesty enough to learn whom to believe, and is the "tall tale" line on the
  results screen a satisfying reveal?
- The new rules: is Dorrin's two seconds readable with the countdown over the chest? Does a goblin's tenth feel like a
  real choice? Does the King's game (a quarter off, or a quarter more) feel like a test or a trap for a learner? The
  Maw comes one day in ten: is "everyone after him asks twice as much" a thrill to weigh, or a coin nobody can refuse?
- Hesk never plays the King's game in the sim (no hard shake, no drop: he can't be sure of a read). Is that a fair
  cost of his back, or should the scale count as checking?

- With no percentage bonuses, are curios still exciting to find? Which do players pick, and does anyone take the token
  or the crescent?
- Paying off early: does a week-7 finish feel like the reward it should, or like the story ending before Mara's last
  piece? (Keep trading, and her last ask still comes.)
- Is drag speed a good stand-in for shake strength with a real mouse? The thresholds need playtesting; the words under
  the chest now show where you are.
- Vexis's twelve seconds: enough to shake gently and hard and decide, but is it fun or just stressful?
- Should displayed things ever sell for more than their price (a famous piece)? Today they only draw customers.
- Is counting by ear (and by the words over the chest) readable with four or five things rattling at once?
- Wenna, Mags and Mother Bramble pay a little even blind (a fair price for a heavy chest, a rattle's price for a purse,
  a cheap dream). Kindly sellers, or holes?
- Synergies reach level 4 within 30 days for most careful players; level 5 is a long goal. Is the pace right?
- The odds bar takes the arithmetic away but not the read: it only knows what you ticked. Does it still leave room
  for a hunch (paying a little over the odds for a chest you suspect holds more), or does everyone just follow it?
- Enchantment is now x2 instead of x4: still exciting enough to hunt with the witchlight?
- The shop saves at the start of each day only, and sellers don't remember you.
- Three days at the stall before the first renovation: long enough to learn the three rules, or too long?
- The sim's careful player is a perfect listener: real players will clear the debt less often. Is 12,000g in week 8
  a wall people want to climb again, or a cliff? Bram's word (once a run) and his stash (three times) are the safety
  nets; are they enough for a beginner, and too much for a good player?
- Curios are chosen by a fixed taste in the sim: which ones do players actually pick, and do any feel useless (the
  knife, the hook)?
- Bram's kin: is Hesk's arithmetic (the scale against the slip) a puzzle or a chore? Does Rosa's haggling feel like
  her own game, with sellers asking her more? Does Isolde's door money make keeping things a real choice from the
  stall on?
- Careers: the learner model's mistakes and its pace of learning are guesses. Playtest how many runs a first clear
  takes (the sim says about eight or nine) and whether losing in week 8 several runs running feels like "nearly" or
  like a wall. The talents lift the careful player from 41% to 79%; should the tree be a loadout instead?
- With the steeper late dues, the exhibition matters: a careful player who sells everything clears 21%, one who keeps
  grand things 44%. Is keeping things taught early enough (Bram's "keep" tip) for that to be fair?
- Challenge levels 1 to 3 barely trouble the careful player (they bite people who use the stash and the drop); is
  that a gentle on-ramp or free renown?
- The Crown Chest usually comes with clearing the debt for a careful player (the Hooded Woman's asks are met by volume at
  the Emporium). Should her asks be harder, or limited to their own week?
