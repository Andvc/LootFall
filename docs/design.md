# LootFall design notes

## The game in one paragraph

You inherit a roadside stall from Uncle Bram. People bring you chests and price them their own way: some shake them
and price what they heard, some go by the weight or the chest's colour, some feel for magic, and some just dream up a
number. Shake each one yourself (gently, firmly, hard), drop it to listen for a false bottom, count the rattles, and
work out what they missed, made up or misjudged. Mark what you think is inside on your appraisal slip and trade the
way each customer trades: pay their price, haggle, or name your own. Pass, and your gut tells you whether you were
right; buy, and the chest bursts open on the spot: each thing's true quality, a tick or a cross against your slip,
and now and then something far better than the chest promised. What you acquire builds the shop's name (synergies)
and its exhibition, which draw more and better customers. The goal is the Royal Warrant.

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
| Packing | village | cloth packed around metal, coins and jewelry muffles them: they sound one shake harder | a jingle that only comes at a firm shake means there's packing, so there's cloth; the **Ear Trumpet** hears through it |
| Two or three of a kind | town | each thing sounds from its own spot, so more of them rattle more often | count the words over the chest; most sellers never count |
| Sand | town | sand rustles just like paper, but pours at the gentlest shake; paper never does | a rustle at a gentle shake is sand |
| Magic | town | magic sounds like anything else | under the **Witchlight** it glows |
| Gilded fakes | Emporium | gilded iron jingles and tinkles like gold and jewels | under the **Magnet** iron can't move: a fake goes quiet |

Sound-alikes (sand, gilded iron) are the deductive heart of the later shops: the sound tells you the kind of thing,
but only the rules tell you whether it's real. Packing changes *when* things sound, which hides them from anyone who
only shakes at one strength (most sellers) or can't shake hard (Granny Wick's chests).

How fast you drag sets the shake strength (gentle from 40 px/s, firm from 520, hard from 1150). There's no meter:
the sounds tell you. `ShakeBox` gates physics impacts by `Chest.Sounds`/`Chest.Audible`, and the shake test checks the
two agree, thing by thing, with and without the magnet and the trumpet.

## What a chest holds, and how good it is

A chest's **colour** says what to expect: wood (common), green (uncommon), blue (rare), purple (epic). Each thing
inside rolls its own quality around that: 70% as promised, 15% one worse, 13% one better, 1.6% two better, 0.4%
three better (what would fall below common or above legendary stays at the end). Value is the kind's base times
1, 3, 8, 20 or 50 by quality.

The slip shows what each thing would be worth **if it's as good as the chest promises**, so it's easy to read and on
the cautious side: things come out better than their colour about as often as worse, and better is worth far more.
Sellers price by the **average** (`Categories.Expected`), which keeps the economy easy to balance.

| Kind | Sound | Stirs at | Note | Common | Legendary |
|---|---|---|---|---|---|
| Coins | jingle | gentle | delicate, muffled by packing | Pouch of Copper 12g | Dragon's Hoard 600g |
| Metal | clank | firm | loud, iron, muffled by packing | Rusty Knife 5g | Royal Rapier 250g |
| Cloth | flump | hard | soft; packing | Moth-Eaten Cloak 6g | Royal Robe 300g |
| Bottles | slosh | gentle | delicate, glass | Bottle of Ale 8g | Ambrosia 400g |
| Paper | rustle | hard | soft; sand sounds alike | Bundle of Letters 5g | Royal Charter 250g |
| Jewelry | tinkle | gentle | delicate, muffled by packing | Brass Ring 20g | Jeweled Chalice 1000g |

Magic is worth four times as much and sounds like anything else; a fake (gilded slugs, paste, a bag of sand) is worth
1g; broken glass is worth nothing.

## Ways to judge a chest

Each answers one question by a rule you'd expect from real life. All of them are perceptions; nothing states a value.

| How | From | Tells you | Cost or risk |
|---|---|---|---|
| Shake gently, firmly, hard | start | which kinds are inside, and how many of each | Granny Wick hates a hard shake |
| Drop it (lift it high, let go) | village | a hollow BOOM means a false bottom | glass may break (half the time each); Madame Zara hates it |
| Ear Trumpet (100g) | village | always on once bought: packing can't muffle anything | none |
| Witchlight (200g) | town | always lit once bought: magic glows when it makes a sound | none |
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
cart" through "Could have gone either way" to "Even the heavens pity you", each with a few variants. A seller who
storms off leaves the same feeling. After an opening, the verdict gets a gut line too, from what it held against what
you paid ("You feel like you just got fleeced", "Highway robbery. The legal kind.").

## Opening a chest

It opens by itself (no holding): a short rumble in the chest's own colour, then the burst. Things no better than the
chest promised land quickly, worst first. A thing **better** than its chest rises out still glowing in the chest's
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
| Listen at one strength | Penn (gently), Hob (hard), Tobias (firmly, and makes up a sound), Wick (firmly), Gull (hard), Pell (firmly), Shelly (hard) | what their strength can't stir, a clank drowns or packing muffles; they never count | shake at the strengths they didn't; count |
| Listen gently, then hard | Pip | nothing she can hear; magic and false bottoms | witchlight and drop: she has plenty of both |
| Count the rattles | Mags | a rattle's a rattle: a knife costs what a purse does | buy when the rattles are good kinds |
| Weight | Brask | heavy means valuable: metal is heavy and cheap, bottles and paper light | buy light chests of good things |
| The chest's colour | Vex (12 seconds), Zara (feels magic) | never look inside | buy when it holds more than a chest that colour usually does |
| A dream | Orrin | the price has nothing to do with the chest | add it up, buy when the dream's cheap |
| Knows only metal | Grit | prices other things at half, and jewelry as diamonds | haggle; mind the tinkles |
| Knows only her jewelry | Ermine | prices her jewels at what she paid; the rest comes free, but some jewels are paste | weigh the free things against the paste |
| Feels magic | Wren | can't tell a coin from a coat; wants more only for magic | name a low price; a refusal means magic |
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
| Granny Wick | village | a firm shake | fixed, steep (1.8 to 2.1 times) | **shake it hard: +50%** | packs everything in cloth (90%): padded things she never hears |
| Wren the Apprentice | town | feeling for magic | name a price | | takes a little for anything not magic; brings magic (25%) |
| Magister Orrin | town | a dream (0.5 to 2.8 times a usual chest) | fixed | | brings magic (20%); sometimes a steal |
| Grit the Miner | town | knowing metal (hard) | haggle, comes down further | | everything but metal and jewelry at half |
| Madame Zara | town | the colour, plus magic | fixed | **drop it: +50%** | trick boxes with false bottoms (50%) she doesn't price |
| Pell the Pawnbroker | town | a firm shake, never counting | fixed, steep (2 to 2.5 times) | | lots of two or three of a kind (60%), priced as one |
| Shelly the Beachcomber | town | a hard shake | fixed | | half her "paper" is sand; bottles a clank drowns for her |
| Baron Vex | Emporium | the colour | fixed | **12 seconds to decide** | the best chests, often better than their colour (40% one step up) |
| Captain Gull | Emporium | a hard shake | haggle | | always prices in a false bottom: real 55% of the time |
| Pip the Sharp | Emporium | a gentle shake, then a hard one | fixed | | magic (20%) and false bottoms (30%) |
| Lady Ermine | Emporium | her jewelry, at what she paid | fixed | **use a magnet: +50%** | everything else free; 25% of her jewels are paste |
| Silas | Emporium | packed it himself | fixed, at half | | real goods for half; fakes (60%) worth nothing |

For listeners, whether the chest holds something they can't price (unheard, or a second of a kind they didn't count)
is rolled first, at their miss rate (55%, 45% for Wick and Pell, 50% for Gull; Pip and Silas miss nothing), which
keeps buying blind from paying while listening does. Everyone packs a chest with cloth now and then (30%, from the
village shop); everyone brings two of a kind now and then (8%, from the town shop).

## A seller's first visit is a lesson

The first time each seller comes in (with tips on), their visit is a lesson in their habit, built so it can't fail
to show it: Marta's chest has a false bottom, Pell's has three purses, Shelly's has sand, Gull's has *no* false bottom
for him to swear to, Silas's has gilded coins. Bram gives one step at a time; each step has a check
(`LessonStep.Done`, on what's been heard, the slip, the visit) that runs every frame, so a step done early counts.
Until the steps are done, the trade buttons wait (unless the step is about trading, like naming Tam a price); the
lesson can always be skipped. Steps adapt to the tools you own (Wick's lesson without a trumpet teaches you to
infer cloth from a late jingle; with one, it says what the trumpet hides from her). Day one is still the scripted
stall day; Tam, Hob and Mags teach naming a price, haggling and counting rattles on their first visit after it.

`--sim` ends with a lesson test: every lesson played the careful way (the same strategy the bot and the sim use),
with and without the shop's tools, must complete every step in order.

## Collection, synergies and exhibition

Every thing you open goes in the ledger's collection the first time you find it (39 entries: five qualities of six
kinds, six magic things, three fakes) and in the journal's tally of what you've acquired, kind by quality.

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
| Arcanist | magic things found (3, 8, 15, 25, 40) | each thing 2% likelier to be magic |

Only **one** synergy is active at a time, chosen at the end of a day (the day-end screen's Synergies button opens the
journal with switches). A Guild Charter (300g, town) and a Royal Seal (900g, Emporium) add a slot each. That makes a
specific kind of chest worth wanting: a player chasing Jewelry Tycoon level 3 wants blue chests with coins and
jewelry, and will pay up for them.

After an opening, anything not fake or broken can be **kept for display** instead of sold: you give up its price. The
button shows the points it adds (a star for magic, which counts double). With the display full, it becomes **Swap**:
pick something on display to sell, and the new thing takes its place. Display space: a shelf of 2, then cabinets of
3, 3 and 4 (60g, 250g, 600g). The **exhibition** score is the display's prestige (1, 2, 4, 8 or 16 by quality; double
for magic). Every 10 points brings a customer a day (up to two), and each point makes a chest 1% likelier to be a
colour better (up to 40%). The Crown wants an exhibition of 40 before it grants the warrant, and past 40 an
exhibition brings nothing more.

## Stages

| Shop | Cost | Customers/day | Chests | New |
|---|---|---|---|---|
| Roadside Stall | start | 4 | mostly wood | jingle, clank, flump; three strengths; clanks drown jingles; from day 2, haggling, naming a price, counting rattles |
| Village Shop | 60g | 5 | mostly green | slosh and rustle; locks; false bottoms and the drop; packing and the Ear Trumpet; Brask's weight, Tobias's tall tales, Marta, Wick |
| Town Shop | 200g | 6 | mostly blue | jewelry; two or three of a kind (Pell); sand (Shelly); magic and the Witchlight; Wren, Orrin's dreams, Grit, Zara; the Guild Charter |
| The Emporium | 1200g | 7 | blue and purple | Vex's clock, Gull's false bottoms, Pip, Silas's fakes, Ermine's paste, the Magnet, the Royal Seal |
| Royal Warrant | 5000g and an exhibition of 40 | | | the goal |

The Painted Sign (40g) adds a customer a day. Day one is scripted: four customers, one lesson each (a gentle shake
and the slip; a hard shake for cloth; listening past a clank; passing when they heard everything).

## Evidence (`godot --headless --path . -- --sim`, 200 campaigns of 30 days)

| Style | Village | Town | Emporium | Royal Warrant | First synergy | Exhibition on day 20 | Good buys | Perfect reads |
|---|---|---|---|---|---|---|---|---|
| Pays their price, every chest | day 4 | day 17 | day 30 | never | (none chosen) | 0 | 52% | 21% |
| One hard shake, keeps the seller's marks | day 3 | day 12 | day 20 | never | (none chosen) | 0 | 75% | 56% |
| Every strength, drops when safe | day 2 | day 8 | day 12 | never (no exhibition) | day 2 | 0 | 78% | 65% |
| + Ear Trumpet, Witchlight and Magnet | day 2 | day 8 | day 13 | never (no exhibition) | day 2 | 0 | 84% | 85% |
| + keeps grand things from the town shop on, up to 40 | day 2 | day 9 | day 14 | day 21 | day 2 | 42 | 85% | 85% |

Profit per customer for the careful collector: 8g at the stall, 15g at the village, 68g in town, 179g at the
Emporium (where synergies at levels 3 and 4 add most of the last). By day 30, most careful collectors have every
synergy at level 3 or 4; level 5 (ten legendary of a kind, or 25 perfect reads in a row) is reached by 1% to 29%.

Per seller, at their own shop, profit per visit for a careful player with every tool, against paying their price
blind. Every habit pays for putting up with it; only Tam (a few coins for anything), Wick (her padded things) and
Wren pay a little blind:

| | Tam | Penn | Hob | Mags | Brask | Tobias | Marta | Wick | Wren | Orrin | Grit | Zara | Pell | Shelly | Vex | Gull | Pip | Ermine | Silas |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Careful | +12 | +5 | +11 | +6 | +12 | +18 | +23 | +27 | +39 | +115 | +159 | +26 | +157 | +41 | +164 | +152 | +186 | +96 | +109 |
| Blind | +15 | -1 | -2 | +3 | 0 | -47 | +2 | +11 | +8 | -5 | -282 | -53 | -1 | -12 | -156 | -375 | -17 | -11 | +8 |

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
   puzzle. **v7** adds layers of deduction one at a time (packing and the ear trumpet, counting, sand), a lesson for
   each seller's first visit, a gut feeling instead of opening a chest you passed on, openings that build to their
   surprises, kinder quality odds with longer upward tails, a slip that reads at the chest's colour, and synergies
   that give the collection goals.
5. **Opening a passed chest (v5 to v6).** Immediate feedback, but it made no sense: they're taking it home. The gut
   feeling keeps the feedback and keeps the chest closed.

## Open questions

- Is drag speed a good stand-in for shake strength with a real mouse? The thresholds need playtesting.
- Vex's twelve seconds: enough to shake gently and hard and decide, but is it fun or just stressful?
- Should displayed things ever sell for more than their price (a famous piece)? Today they only draw customers.
- Is counting by ear (and by the words over the chest) readable with four or five things rattling at once?
- Granny Wick pays a little even blind (her padded things are the reward for not shaking hard). A fair kindly seller,
  or a hole?
- Synergies reach level 4 within 30 days for most careful players; level 5 is a long goal. Is the pace right?
- No save game yet, and sellers don't remember you.
