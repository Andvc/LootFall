# LootFall design notes

## The game in one paragraph

You inherit a roadside stall from Uncle Bram. People bring you chests and price them their own way: some shake them
and price what they heard, some go by the weight or the chest's colour, some feel for magic, and some just dream up a
number. Shake each one yourself (gently, firmly, hard), drop it to listen for a false bottom, count the rattles, and
work out what they missed, made up or misjudged. Mark what you think is inside on your appraisal slip and trade the
way each customer trades: pay their price, haggle, or name your own. Pass, and your gut tells you whether you were
right; buy, and the chest bursts open on the spot: each thing's true quality, a tick or a cross against your slip,
and now and then something far better than the chest promised. What you acquire builds the shop's name (synergies)
and its exhibition, which draw more and better customers. The stall comes with Bram's old debt to the Grey Guild:
every fifth evening Mister Vane collects a due that climbs from 40g to 22,000g over eight weeks. Curios from Nim the
peddler bend the rules in your favour, regulars pay well for commissions, something different happens in town once a
week, and a hooded woman trades pieces of a map to King Aldric's lost vault for the finds only a good ear turns up.
Pay all eight dues and the stall is free (and with the whole map, the vault opens on your counter); fall short and
the Guild takes the shop.

## The puzzle: rules you'd expect from real life

Three core rules, from the first day:

1. **Every kind of thing has one sound, and it only sounds when the shake is strong enough to stir it.** Coins
   jingle, bottles slosh and jewelry tinkles at the gentlest shake; metal only clanks at a firm one; cloth flumps and
   paper rustles only at a hard one.
2. **While metal clanks, you can't hear anything delicate.** Shake gently and the metal stays put.
3. **Sellers price only what they think is there.** Each has their own way of judging (below). Those who listen say
   what they heard, and the slip starts marked with it.

Then, one at a time as the shop grows, more layers of deduction, each taught by the seller it belongs to:

| Layer | From | The rule | How you tell |
|---|---|---|---|
| False bottoms | village | a sealed hollow under the floor never rattles | drop the chest: a hollow BOOM (glass may break) |
| Two or three of a kind | town | each thing sounds from its own spot, so more of them rattle more often | count the words over the chest; most sellers never count |
| Sand | town | sand rustles just like paper, but pours at the gentlest shake; paper never does | a rustle at a gentle shake is sand |
| Enchantment | town | anything can be enchanted (worth double); it sounds like anything else | under the **Witchlight** it glows |
| Gilded fakes | Emporium | gilded iron jingles and tinkles like gold and jewels | under the **Magnet** iron can't move: a fake goes quiet |

Sound-alikes (sand, gilded iron) are the deductive heart of the later shops: the sound tells you the kind of thing,
but only the rules tell you whether it's real. Soft things only sound at a hard shake, which hides them from anyone
who won't shake hard (most sellers) or isn't allowed to (Granny Wick's chests: take her sewing on faith, or pay to be
sure).

How fast you drag sets the shake strength (gentle from 40 px/s, firm from 520, hard from 1150). There's no meter:
the sounds tell you. `ShakeBox` gates physics impacts by `Chest.Sounds`/`Chest.Audible`, and the shake test checks the
two agree, thing by thing, with and without the magnet.

## What a chest holds, and how good it is

A chest's **colour** says what to expect: wood (common), green (uncommon), blue (rare), purple (epic). Each thing
inside rolls its own quality around that: 70% as promised, 15% one worse, 13% one better, 1.6% two better, 0.4%
three better (what would fall below common or above legendary stays at the end). Value is the kind's base times
1, 3, 8, 20 or 50 by quality.

The slip shows what each thing would be worth **if it's as good as the chest promises** (`Categories.Nominal`).
Sellers price by the **average** (`Categories.Expected`), which keeps the economy easy to balance.

Neither number answers the question a player actually has, so the panel shows an **odds bar** under the total: "Worth
more than 55g? 62%". It's exact (`Appraisal.Outcomes` combines every ticked thing's quality odds, what a false bottom
might hold, and the shop's sale bonuses), updates as you tick rows and as you move the offer slider, and runs from red
(a long shot) through gold to green (a safe bet). Why not show the average: 70% of things come out exactly as promised
and the average includes rare big upgrades, so most chests are worth a little *less* than it, and a player who paid it
would lose a little on most buys. Why not just the promised total: a price a little above it can still be worth a
gamble, and one a little below is nearly always a good buy, and the bare number says neither. With the bar there are
no sums to do, and it's only as good as your listening, so the read is still yours. The sim's careful player buys
when the bar is on their side, which wins more often than buying on the average (85 to 92% of buys profit) and still
profits from every seller.

| Kind | Sound | Stirs at | Note | Common | Legendary |
|---|---|---|---|---|---|
| Coins | jingle | gentle | delicate | Pouch of Copper 12g | Dragon's Hoard 600g |
| Metal | clank | firm | loud, iron | Rusty Knife 5g | Royal Rapier 250g |
| Cloth | flump | hard | soft | Moth-Eaten Cloak 6g | Royal Robe 300g |
| Bottles | slosh | gentle | delicate, glass | Bottle of Ale 8g | Ambrosia 400g |
| Paper | rustle | hard | soft; sand sounds alike | Bundle of Letters 5g | Royal Charter 250g |
| Jewelry | tinkle | gentle | delicate | Brass Ring 20g | Jeweled Chalice 1000g |

From the town shop, **anything can be enchanted** (5% of things, more from mages): an Enchanted Silk Cloak is still a
silk cloak, the same quality and colour, worth twice as much and twice the exhibition points. It sounds like anything
else; under the Witchlight it glows. A fake (gilded slugs, paste, a bag of sand) is worth 1g; broken glass is worth
nothing.

## Ways to judge a chest

Each answers one question by a rule you'd expect from real life. All of them are perceptions; nothing states a value.

| How | From | Tells you | Cost or risk |
|---|---|---|---|
| Shake gently, firmly, hard | start | which kinds are inside, and how many of each | Granny Wick hates a hard shake |
| Drop it (lift it high, let go) | village | a hollow BOOM means a false bottom | glass may break (half the time each); Madame Zara hates it |
| Witchlight (200g) | town | always lit once bought: anything enchanted glows when it makes a sound | none |
| Magnet (400g) | Emporium | iron can't move: no clanks to drown things out, and gilded fakes go quiet | Lady Ermine hates it |

**Left out: the scale.** A weight only means something if you add up what each thing should weigh in your head, which
isn't intuitive, and the drop already gives the one weight-like fact that matters (hollow or solid). Weight comes back
as a seller's habit instead: Sergeant Brask goes by it, and the ledger says which kinds are heavy.

**Lockpicking** (from the village shop) comes after you buy a locked chest: set each pin when the pick passes the
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

## Sellers: one way of judging, one habit, and a reward for putting up with it

The same rules make a different puzzle with each seller, because each judges a chest a different way, with a blind
spot a careful player can see through. Not every visit is a deal: often the right answer is to pass.

| How they judge | Who | Their blind spot | How to beat it |
|---|---|---|---|
| Listen at one strength | Penn (gently), Hob (hard), Tobias (firmly, and makes up a sound), Wick (firmly), Gull (hard), Pell (firmly), Shelly (hard) | what their strength can't stir or a clank drowns; they never count | shake at the strengths they didn't; count |
| Listen gently, then hard | Pip | nothing she can hear; enchantment and false bottoms | witchlight and drop: she has plenty of both |
| Count the rattles | Mags | a rattle's a rattle: a knife costs what a purse does | buy when the rattles are good kinds |
| Weight | Brask | heavy means valuable: metal is heavy and cheap, bottles and paper light | buy light chests of good things |
| The chest's colour | Vex (12 seconds), Zara (feels magic) | never look inside | buy when it holds more than a chest that colour usually does |
| A dream | Orrin | the price has nothing to do with the chest | add it up, buy when the dream's cheap |
| Knows only metal | Grit | prices other things at half, and jewelry as diamonds | haggle; mind the tinkles |
| Knows only her jewelry | Ermine | prices her jewels at what she paid; the rest comes free, but some jewels are paste | weigh the free things against the paste |
| Feels magic | Wren | can't tell a coin from a coat; wants more only for enchanted things | name a low price; a refusal means something's enchanted |
| No idea | Tam | takes a few coins for anything; sometimes it's empty | shake first: an empty chest makes no sound |
| Packed it himself | Silas | sells gilded iron as gold and jewels for half | a magnet silences fakes |

Three ways to trade:
- **Fixed price**: buy or pass.
- **Haggle**: they ask high (about 1.9 times what they think it's worth). You get one offer: they take it if it's at
  least what they'd really accept, name a final price if not, or storm off if it's under 35% of their ask.
- **Name a price**: they wait for your offer. Two tries.

| Seller | Shop | Judges by | Trades | Won't stand for | Why put up with it |
|---|---|---|---|---|---|
| Little Tam | stall | no idea | name a price | | a few coins buys anything (30% empty) |
| Widow Penn | stall | a gentle shake | fixed | | metal and soft things go unpriced |
| Hob the Farmer | stall | a hard shake | haggle | | asks double, comes down to a fair price |
| Mags the Fishwife | stall | counting rattles (firmly) | fixed | | good kinds at the price of rusty knives |
| Sergeant Brask | village | weight | fixed, at about what he thinks it's worth | | light chests of good things; lockboxes (80% locked) for a masterful pick |
| Tobias the Tinker | village | a firm shake | haggle | | his misses, if you catch the sound he made up |
| Old Marta | village | a gentle shake | fixed, steep | | false bottoms (45%) she's forgotten; drop and listen |
| Granny Wick | village | a firm shake | fixed, steep (1.8 to 2.1 times) | **shake it hard: +50%** | a seamstress: there's usually sewing in her chests, which only flumps at a hard shake, so she never prices it; tick it on faith or pay to be sure |
| Wren the Apprentice | town | feeling for magic | name a price | | takes a little for anything not enchanted; brings enchanted things (25%) |
| Magister Orrin | town | a dream (0.5 to 2.8 times a usual chest) | fixed | | brings enchanted things (20%); sometimes a steal |
| Grit the Miner | town | knowing metal (hard) | haggle, comes down further | | everything but metal and jewelry at half |
| Madame Zara | town | the colour, plus magic | fixed | **drop it: +50%** | trick boxes with false bottoms (50%) she doesn't price |
| Pell the Pawnbroker | town | a firm shake, never counting | fixed, steep (2 to 2.5 times) | | lots of two or three of a kind (60%), priced as one |
| Shelly the Beachcomber | town | a hard shake | fixed | | half her "paper" is sand; bottles a clank drowns for her |
| Baron Vex | Emporium | the colour | fixed | **12 seconds to decide** | the best chests, often better than their colour (40% one step up) |
| Captain Gull | Emporium | a hard shake | haggle | | always prices in a false bottom: real 55% of the time |
| Pip the Sharp | Emporium | a gentle shake, then a hard one | fixed (1.2 to 1.45 times) | | enchanted things (20%) and false bottoms (30%) |
| Lady Ermine | Emporium | her jewelry, at what she paid | fixed | **use a magnet: +50%** | everything else free; 25% of her jewels are paste |
| Silas | Emporium | packed it himself | fixed, at half | | real goods for half; fakes (60%) worth nothing |

For listeners, whether the chest holds something they can't price (unheard, or a second of a kind they didn't count)
is rolled first, at their miss rate (55%, 45% for Wick and Pell, 50% for Gull; Pip and Silas miss nothing), which
keeps buying blind from paying while listening does. Everyone brings two of a kind now and then (8%, from the town
shop).

## A seller's first visit is a lesson

The first time each seller comes in (with tips on), their visit is a lesson in their habit, built so it can't fail
to show it: Marta's chest has a false bottom, Pell's has three purses, Shelly's has sand, Gull's has *no* false bottom
for him to swear to, Silas's has gilded coins. Bram gives one step at a time; each step has a check
(`LessonStep.Done`, on what's been heard, the slip, the visit) that runs every frame, so a step done early counts.
Until the steps are done, the trade buttons wait (unless the step is about trading, like naming Tam a price); the
lesson can always be skipped. Steps adapt to the tools you own (Pip's asks you to mark what glows if you have the
witchlight; Silas's to use the magnet if you have one). Wick's lesson leaves the choice to you: tick her sewing on
faith, or shake hard and pay half again to hear it. Day one is still the scripted stall day; Tam, Hob and Mags teach
naming a price, haggling and counting rattles on their first visit after it.

Lessons and Bram's tips never talk over each other. A lesson waits until any tip already up (a new shop, a lock, a
new way of trading) has been read. While a lesson runs, tips that would come up don't pile up behind it: they wait
for their moment to come round again. And a lesson's `Covers` marks the tips it already teaches as seen (Hob's lesson
is the haggling tip; Marta's, the drop and the boom). A new shop's crowd doesn't all arrive on its first day: with
tips on, no more than three lessons a day (`GameState.LessonsPerDay`), so the town shop's six newcomers take two days
to meet. `--autoplay` prints every message Bram shows, and flags any that was replaced before it could be read.

`--sim` ends with a lesson test: every lesson played the careful way (the same strategy the bot and the sim use),
with and without the shop's tools, must complete every step in order.

## How the shop opens up

Everything arrives when it's needed, each with a word from Bram, so the first days are only about listening:

| When | What opens up | Bram explains |
|---|---|---|
| Day 1 | shaking, the slip and the odds bar, buying and passing | one lesson per customer (scripted) |
| Day 2 | haggling, naming a price, counting rattles; the ledger (L) | each seller's first visit is a lesson; the ledger on a later customer |
| The stall has earned 120g, all told (`GameState.UpgradesAt`) | renovations and tools at the end of the day; until then the day-end screen shows how far there is to go | what a renovation brings |
| The Village Shop | locks and lockpicking; the display shelf, keeping things, the exhibition and cabinets | the lock (the first time, the pick waits while he talks); the shelf and why an exhibition matters |
| The Town Shop | synergies: the journal has tallied everything from day one, so some are ready to switch on | what a synergy is and where to choose one |
| The Emporium | the biggest crowd, the magnet, the Royal Seal | that from now on it's about the Guild |
| The first evening | the Grey Guild and Bram's debt: a due every fifth evening | a scene: Vane, then Bram |
| Week 2 | Bram's piece of the map; commissions; Nim's curios (his first visit is that morning); one event a week | a scene; a tip with each |
| Week 3 | the Hooded Woman's commissions, which pay in map pieces | a scene |
| A short night | pawning, and (once a run) Bram's word | a tip |

`GameState.UpgradesOpen`, `DisplayOpen` and `SynergiesOpen` hold the gates, so the sim and the bot follow them too. "Earned" is what
the chests you've bought brought in (the day end's "the chests held", added up): a careful player gets there at the
end of day 3, after two days of practice at the stall. (Counting profit instead took five to eight days at the stall
with nothing new, too long.)

## The run: the Guild's dues

The days come in weeks of five. At the end of each week's fifth day Mister Vane of the Grey Guild comes for the
week's due (`Guild.Due`, `DuesPhase`). Bram's debt is eight weeks long; after that the Guild keeps coming for "interest",
half as much again each week, for as long as you last (endless).

| Week | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | then |
|---|---|---|---|---|---|---|---|---|---|
| Due | 40g | 100g | 250g | 700g | 2,000g | 6,000g | 13,000g | 22,000g | x1.5 a week |

The first weeks are small while you learn the stall and the village; from week 4 the dues climb with what a growing
shop can earn, so every evening's spending is a decision: a renovation or cabinet now pays back over days, but the
Guild comes on a fixed night. The HUD always shows the next due and when; the day-end screen shows it against your
gold, and an upgrade that would leave you short tonight has an orange price.

Short on the night: pawn what's on display (at 60% of its price) or sell curios back (half their price). Once a run,
if even that won't do, Bram vouches for you: everything you have goes to the Guild and the rest, with half as much
again, is added to next week's due (not in week 8: the last of the debt is the last). Otherwise the Guild takes the
shop and the run is over. A short night is also the most dramatic moment the shop has: what do you give up first?

## Curios

Nim the peddler drops by before opening once a week from week 2 (`PeddlerPhase`; his first visit is the morning after
the Guild's first) with three curios (`Curios.Stock`: rarer ones more often as the weeks go by). Each bends one rule of
the trade, and you have room for three (a Curio Case, 250g at the town shop, adds a fourth), so which ones you keep
shapes how you play: whom to haggle with, which sounds to chase, whether to save or spend. None of them tells you what
a chest holds before you buy it. Prices climb with the stakes (common 50g, uncommon 100g, rare 180g in week 2, half as
much again each week after); Nim buys them back for half.

| Curio | | Does |
|---|---|---|
| Lucky Coin | common | the first chest you open each day: everything in it gets two tries at its quality |
| Lucky Shell | common | one chest in six you open: everything in it is a step better |
| Jeweller's Loupe | common | coins and jewelry sell for +50% |
| Smith's Glove | common | metal sells for +50% |
| Hatter's Cap | common | cloth and paper sell for +50% |
| Shop Lantern | common | one more customer every day |
| Collector's Catalogue | common | every new find in the journal pays 10g per shop tier |
| Merchant's Signet | uncommon | hagglers and those who ask you to name a price settle for 15% less |
| Savings Jar | uncommon | nightly interest: 5% of your gold, up to 25g per shop tier |
| Letter of Introduction | uncommon | commissions pay +50% |
| Burglar's Knife | uncommon (village on) | forcing a lock breaks and scratches nothing |
| Smuggler's Hook | uncommon (village on) | false bottoms turn up far more often |
| Witch's Draught | uncommon (town on) | enchanted things turn up far more often |
| Four-Leaf Clover | rare | chests come in a colour better more often |
| Forged Receipt | rare | the Guild's dues are a fifth less |
| Visitors' Book | rare (village on) | every night, 3g per exhibition point |
| Cursed Crescent | rare | everything sells for +50%, but the Guild's dues are a quarter more |

Effects live in `GameState` (`Run.cs`): sale multipliers, the chests' boosts, the dues, the night's income and a
curio's luck at purchase, which the opening announces ("Lucky Coin: 2 things came out better!").

## Commissions

From week 2, three regulars you've met post a commission each week (`Patrons.Roll`), good until the Guild's next
visit: one thing of a kind at a quality (a step better than the shop's chests usually are), two or three of a kind
(a step lower), or (from the town shop) anything enchanted. They pay two or three times what such a thing sells
for. A board under the seller lists them, the slip marks the kinds they want with a gold pennant, and anything that
comes out of a chest you open can be delivered instead of sold. A commission makes a sound worth more than its price:
a chest with a flump in it is worth chasing when Penn wants silk.

## Events

Once a week from week 2, on a day of its own, something happens in town (the day card announces it; `Events`):

| Event | That day |
|---|---|
| Market Day | three more customers |
| Harvest Festival | everything sells for a quarter more |
| The Royal Buyer | one kind of thing sells for double |
| A Gilded Chest | someone brings one of the old royal chests: gold, a colour better than its seller thinks, with something enchanted inside |
| The Stranger | an extra customer with a sealed black chest packed in straw: nothing rattles, but a drop tells you how heavy it is (heavy ones hold more: often junk, sometimes a fortune) |
| A Storm | two fewer customers, but every chest a colour better |
| A Shipwreck (town on) | lots come in twos and threes |

They all come round before any comes twice.

## The story

Twenty years ago Bram borrowed from the Grey Guild to buy a quarter of a map to King Aldric's vault. Scenes
(`ScenePhase`, `Story`) play the first evening (Vane introduces the debt), at the start of some weeks, and at the
end:

- Week 2: Bram tells you about the map and gives you his piece.
- Week 3, 5 and 7: the Hooded Woman asks for something (old papers, a ring, something enchanted, each better than the
  regulars ask for) and pays a map piece for it. Miss one and she asks again the next week, until the debt's last week.
- Week 4: Bram recognises her voice; the Guild has been buying up old records. Week 5: she says the debt was a leash.
- Week 6: Vane offers to halve that night's due for the map. Take it and there's no vault.
- Week 7: she is Mara, Bram's old partner. Week 8: the last due.
- Debt paid: with all four pieces, Mara and Bram bring the vault to the counter: a gilded chest with a dwarven lock
  and the best of everything inside, opened like any other. Then you keep trading against the Guild's interest, or
  retire.

Every run is recorded in `user://profile.json` (runs, debts cleared, vaults opened, the furthest week), shown on the
title screen and at the end of a run.

## The shop's look

Each renovation changes the shop around you: a trestle table under a striped awning outside the town walls; a
plastered village shop with timber beams and oil lamps; a panelled town shop whose display window looks over the
square at sunset; the Emporium's colonnade, crimson drapes and marble counter. Each is one 640x360 picture made by
`tools/import_art.py` (a backdrop from the pack, architecture drawn as pixel art, a few of the pack's props, and the
counter), shown at 2x behind the counter and dimmed behind the lockpicking, opening and day-end screens
(`UiKit.MakeShop`). What you keep stands on the shelf (the window sill in town).

## Title, saves and settings

The game opens on a title screen (Continue, New game, Settings, Quit) over your shop as you left it. The shop is
saved to `user://save.json` at the start of each day (`GameState.Save` and `Load`, plain JSON from Core), so leaving
mid-day starts that day over; the pause menu (Esc, or Menu in the top bar) says so. Settings (`user://settings.cfg`):
volume, fullscreen, screen shake and flashes (full, reduced or off), and Uncle Bram's tips.

## Collection, synergies and exhibition

Every thing you open goes in the ledger's collection the first time you find it (33 entries: five qualities of six
kinds and three fakes; finding one enchanted stars its entry) and in the journal's tally of what you've acquired,
kind by quality. Each thing looks the same everywhere: the collection, the shelf, the exhibition and the keep chooser
all show its own sprite (`UiKit.MakeItemIcon`, from icons `tools/import_art.py` halves when a sprite is too big).

**Synergies** turn that tally into goals. Each has five levels; ten of a kind at common or better reaches level 1,
ten at uncommon or better level 2, and so on to ten legendary for level 5. Two are achievements instead.

| Synergy | Needs | Does (per level) |
|---|---|---|
| Jewelry Tycoon | coins and jewelry | +3% on everything you sell |
| Smithing Tycoon | metal | smiths and soldiers come 30% more often; metal 5% likelier to be one quality better |
| Vintner | bottles | bottles fetch +8%; glass 15% less likely to break |
| Archivist | paper | chests 3% likelier to have a false bottom |
| Tailor | cloth | exhibition +10% |
| Keen Eye | perfect reads in a row (3, 6, 10, 15, 25) | a perfect read earns a fee of 3% of what the chest held |
| Arcanist | enchanted things found (3, 8, 15, 25, 40) | each thing 2% likelier to be enchanted |

Only **one** synergy is active at a time, chosen at the end of a day (the day-end screen's Synergies button opens the
journal with switches). A Guild Charter (300g, town) and a Royal Seal (900g, Emporium) add a slot each. That makes a
specific kind of chest worth wanting: a player chasing Jewelry Tycoon level 3 wants blue chests with coins and
jewelry, and will pay up for them.

After an opening, anything not fake or broken can be **kept for display** instead of sold: you give up its price. The
button shows the points it adds (a star for enchanted, which counts double). With the display full, it becomes **Swap**:
pick something on display to sell, and the new thing takes its place. Display space: a shelf of 2, then cabinets of
3, 3 and 4 (60g, 250g, 600g). The **exhibition** score is the display's prestige (1, 2, 4, 8 or 16 by quality; double
if enchanted). Every 10 points brings a customer a day (up to two), and each point makes a chest 1% likelier to be a
colour better (up to 40%). Past 40 an exhibition brings nothing more, unless visitors pay to see it (the Visitors'
Book curio).

## Stages

| Shop | Cost | Customers/day | Chests | New |
|---|---|---|---|---|
| Roadside Stall | start | 4 | mostly wood | jingle, clank, flump; three strengths; clanks drown jingles; from day 2, haggling, naming a price, counting rattles |
| Village Shop | 60g, once the stall has earned 120g | 5 | mostly green | slosh and rustle; locks; false bottoms and the drop; the display shelf and cabinets; Brask's weight, Tobias's tall tales, Marta, Wick's sewing |
| Town Shop | 200g | 6 | mostly blue | jewelry; two or three of a kind (Pell); sand (Shelly); enchantment and the Witchlight; synergies and the Guild Charter; Wren, Orrin's dreams, Grit, Zara |
| The Emporium | 1200g | 7 | blue and purple | Vex's clock, Gull's false bottoms, Pip, Silas's fakes, Ermine's paste, the Magnet, the Royal Seal |

The Painted Sign (40g) adds a customer a day; the Curio Case (250g, town) room for a fourth curio. The Emporium has
three big sums to grow into while the dues climb: the Night Market (2,500g, two more customers a day), the Patrons'
Book (4,000g, a fourth commission a week) and the Velvet Case (6,000g, a fifth curio). Day one is scripted: four customers, one lesson each (a gentle shake
and the slip; a hard shake for cloth; listening past a clank; passing when they heard everything).

## Evidence (`godot --headless --path . -- --sim`, 200 runs per style, up to 12 weeks)

Each style plays whole runs: buying chests, curios (a careful player's taste, keeping enough back for the Guild) and
upgrades, delivering commissions, paying the Guild (pawning, or Bram's word, when short).

| Style | Still trading after each due, weeks 1-8 | Debt cleared | Vault | Foreclosed (median week) | Village | Town | Emporium |
|---|---|---|---|---|---|---|---|
| Pays their price, every chest | 100 83 60 23 10 6 5 0% | 0% | 0% | all (week 4) | day 5 | never | never |
| One hard shake, keeps the seller's marks | 100 100 91 57 29 15 11 1% | 1% | 0% | all (week 5) | day 6 | never | never |
| Every strength, drops when safe | 100 100 98 94 88 82 81 60% | 60% | 59% | 99% (week 9) | day 3 | day 10 | day 14 |
| + Witchlight and Magnet | 100 100 98 94 87 82 80 66% | 66% | 64% | 97% (week 10) | day 3 | day 10 | day 15 |
| + keeps grand things | 100 100 98 93 83 79 79 71% | 71% | 71% | 91% (week 11) | day 3 | day 10 | day 16 |

Week by week for the careful player with both tools (gold on the Guild's night, before paying):

| Week | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Due | 40 | 100 | 250 | 700 | 2,000 | 6,000 | 13,000 | 22,000 | 33,000 | 49,500 | 74,250 |
| Gold, 10th percentile | 78 | 161 | 368 | 563 | 498 | 6,324 | 11,594 | 4,144 | 28,394 | 23,880 | 25,959 |
| Gold, median | 134 | 421 | 1,491 | 4,998 | 12,872 | 25,363 | 40,217 | 48,684 | 69,494 | 65,251 | 54,863 |
| Paid | 100% | 100% | 98% | 96% | 92% | 94% | 99% | 82% | 90% | 75% | 44% |
| At the Emporium | 0% | 8% | 50% | 81% | 88% | 95% | 98% | 98% | 100% | | |

(Dues shown before curios: the Forged Receipt takes a fifth off, which is why the report's medians read 17,600 in week
8.) The early weeks are gentle and the renovations' payback is quick; the weeks that end runs are week 5 (shops still
in the village when the due reaches 2,000) and week 8, the 22,000 wall. A careful player fills about 26 commissions a
run (some 14,000g), Bram vouches in 30% of runs (usually week 4 or 5), and endless lasts two or three more weeks.
Careless play loses the shop in week 4 or 5.

Per seller, at their own shop, profit per visit for a careful player with every tool, against paying their price
blind (unchanged by the run). Every habit pays for putting up with it; only Tam (a few coins for anything), Wick (her unpriced sewing), Wren
and Mags pay a little blind:

| | Tam | Penn | Hob | Mags | Brask | Tobias | Marta | Wick | Wren | Orrin | Grit | Zara | Pell | Shelly | Vex | Gull | Pip | Ermine | Silas |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Careful | +14 | +4 | +10 | +5 | +13 | +19 | +18 | +19 | +45 | +43 | +150 | +27 | +122 | +29 | +62 | +110 | +62 | +38 | +82 |
| Blind | +15 | -2 | -2 | +3 | +1 | -49 | -3 | +5 | +11 | -87 | -331 | -25 | -29 | -25 | -257 | -422 | -15 | -83 | -39 |

Every seller's lesson completes, played the careful way, with and without the tools of their shop.

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
   gilded ones, and the story of King Aldric's vault.
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

## Open questions

- Is drag speed a good stand-in for shake strength with a real mouse? The thresholds need playtesting.
- Vex's twelve seconds: enough to shake gently and hard and decide, but is it fun or just stressful?
- Should displayed things ever sell for more than their price (a famous piece)? Today they only draw customers.
- Is counting by ear (and by the words over the chest) readable with four or five things rattling at once?
- Granny Wick pays a little even blind (her unpriced sewing is the reward for not shaking hard). A fair kindly seller,
  or a hole?
- Synergies reach level 4 within 30 days for most careful players; level 5 is a long goal. Is the pace right?
- The odds bar takes the arithmetic away but not the read: it only knows what you ticked. Does it still leave room
  for a hunch (paying a little over the odds for a chest you suspect holds more), or does everyone just follow it?
- Enchantment is now x2 instead of x4: still exciting enough to hunt with the witchlight?
- The shop saves at the start of each day only, and sellers don't remember you.
- Four days at the stall before the first renovation: long enough to learn the three rules, or too long?
- The sim's careful player is a perfect listener: real players will clear the debt less often. Is 22,000g in week 8
  a wall people want to climb again, or a cliff? Bram's word (once a run) is the safety net; is once enough?
- Curios are chosen by a fixed taste in the sim: which ones do players actually pick, and do any feel useless (the
  knife, the hook)?
- The vault usually comes with clearing the debt for a careful player (the Hooded Woman's asks are met by volume at
  the Emporium). Should her asks be harder, or limited to their own week?
