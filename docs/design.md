# LootFall design notes

## The game in one paragraph

You inherit a roadside stall from Uncle Bram. People bring you chests and price them their own way: some shake them
and price what they heard, some go by the weight or the chest's colour, some feel for magic, and some just dream up a
number. Shake each one yourself (gently, firmly, hard), drop it to listen for a false bottom, and work out what they
missed, made up or misjudged. Tick what you think is inside on your appraisal slip and trade the way each customer
trades: pay their price, haggle, or name your own. Pick the lock, wrench the lid, and watch everything fly out: each
thing's true quality, a tick or a cross against your slip, and the profit stamped across the screen. Keep the grand
things for your exhibition, which draws more and better customers. The goal is the Royal Warrant.

## The puzzle: three rules

1. **Every kind of thing has one sound, and it only sounds when the shake is strong enough to stir it.** Coins
   jingle, bottles slosh and jewelry tinkles at the gentlest shake; metal only clanks at a firm one; cloth flumps and
   paper rustles only at a hard one.
2. **While metal clanks, you can't hear anything delicate.** Shake gently and the metal stays put.
3. **Sellers price only what they think is there.** Each has their own way of judging (below). Those who listen say
   how hard they shook and what they heard, and the slip starts ticked with it.

How fast you drag sets the shake strength (gentle from 40 px/s, firm from 520, hard from 1150). There's no meter:
the sounds tell you. `ShakeBox` gates physics impacts by `Chest.Audible`, and the shake test checks the two agree.

## What a chest holds, and how good it is

A chest's **colour** says what to expect: wood (common), green (uncommon), blue (rare), purple (epic). Each thing
inside rolls its own quality around that: half the time as expected, often one worse (40%), now and then better
(8% one step, 2% two). A wood chest can't go lower, so it leans upward a little. Value is the kind's base times
1, 3, 8, 20 or 50 by quality. The slip shows the **average** for that colour, so it's right on average and the
opening is still a surprise both ways ("BETTER THAN EXPECTED!").

| Kind | Sound | Stirs at | Note | Common | Legendary |
|---|---|---|---|---|---|
| Coins | jingle | gentle | delicate | Pouch of Copper 12g | Dragon's Hoard 600g |
| Metal | clank | firm | loud, iron | Rusty Knife 5g | Royal Rapier 250g |
| Cloth | flump | hard | soft | Moth-Eaten Cloak 6g | Royal Robe 300g |
| Bottles | slosh | gentle | delicate, glass | Bottle of Ale 8g | Ambrosia 400g |
| Paper | rustle | hard | soft | Bundle of Letters 5g | Royal Charter 250g |
| Jewelry | tinkle | gentle | delicate | Brass Ring 20g | Jeweled Chalice 1000g |

Magic is worth four times as much and sounds like anything else; a fake is worth 1g; broken glass is worth nothing.

## Ways to judge a chest

Each answers one question by a rule you'd expect from real life. All of them are perceptions; nothing states a value.

| How | From | Tells you | Cost or risk |
|---|---|---|---|
| Shake gently, firmly, hard | start | which kinds are inside | Granny Wick and Madame Zara hate a hard shake |
| Drop it (lift it high, let go) | start; matters from the village | a hollow BOOM means a false bottom: one more thing, padded so it never rattles, that nobody has priced | glass may break (half the time each); Marta hates it |
| Witchlight (150g) | town | always lit once bought: magic glows when it makes a sound | none |
| Magnet (300g) | Emporium | iron can't move: no clanks to drown things out, and gilded fakes go quiet | Lady Ermine hates it |

**Left out: the scale.** A weight only means something if you add up what each thing should weigh in your head, which
isn't intuitive, and the drop already gives the one weight-like fact that matters (hollow or solid). Weight comes back
as a seller's habit instead: Sergeant Brask goes by it, and the ledger says which kinds are heavy.

**Lockpicking** (from the village shop) comes after you buy a locked chest: set each pin when the pick passes the
green. A masterful pick (no slips, mostly perfect) makes everything worth 25% more; the crowbar is quick but scratches
everything (-10%) and breaks glass. Fine Lockpicks (50g) widen the sweet spot.

## Sellers: one way of judging, one habit, and a reward for putting up with it

The same three rules make a different puzzle with each seller, because each judges a chest a different way, with a
blind spot a careful player can see through. Not every visit is a deal: often the right answer is to pass.

| How they judge | Who | Their blind spot | How to beat it |
|---|---|---|---|
| Listen at one strength | Penn (gently), Hob (hard), Tobias (firmly, and makes up a sound), Wick (firmly), Gull (hard) | what their strength can't stir, or a clank drowns | shake at the strengths they didn't |
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
| Old Marta | village | a gentle shake | fixed | **drop her chest: +50%** | false bottoms (50%) |
| Granny Wick | village | a firm shake | fixed, steep (1.8 to 2.1 times) | **shake it hard: +50%** | full of cloth she never hears |
| Wren the Apprentice | town | feeling for magic | name a price | | takes a little for anything not magic; brings magic (25%) |
| Magister Orrin | town | a dream (0.5 to 2.8 times a usual chest) | fixed | | brings magic (20%); sometimes a steal |
| Grit the Miner | town | knowing metal (hard) | haggle, comes down further | | everything but metal and jewelry at half |
| Madame Zara | town | the colour, plus magic | fixed | **shake it hard: +50%** | trick boxes with false bottoms (50%) she doesn't price |
| Baron Vex | Emporium | the colour | fixed | **12 seconds to decide** | the best chests, often better than their colour (40% one step up) |
| Captain Gull | Emporium | a hard shake | haggle | | always prices in a false bottom: real 55% of the time |
| Pip the Sharp | Emporium | a gentle shake, then a hard one | fixed | | magic (20%) and false bottoms (30%) |
| Lady Ermine | Emporium | her jewelry, at what she paid | fixed | **use a magnet: +50%** | everything else free; 25% of her jewels are paste |
| Silas | Emporium | packed it himself | fixed, at half | | real goods for half; fakes (60%) worth nothing |

For listeners, whether the chest holds something they can't hear is rolled first, at their miss rate (55%, 45% for
Wick and 50% for Gull; Pip and Silas miss nothing), which keeps buying blind from paying while listening does. If
someone who prices by what they found finds nothing, they ask 2 to 4g a tier.

## Stages

| Shop | Cost | Customers/day | Chests | New |
|---|---|---|---|---|
| Roadside Stall | start | 4 | mostly wood | jingle, clank, flump; three strengths; clanks drown jingles; from day 2, haggling and naming a price |
| Village Shop | 60g | 5 | mostly green | slosh and rustle; locks; false bottoms and the drop; Brask's weight, Tobias's tall tales, Marta, Wick's "never hard" |
| Town Shop | 150g | 6 | mostly blue | jewelry; magic and the Witchlight; Wren, Orrin's dreams, Grit, Zara |
| The Emporium | 900g | 7 | blue and purple | Vex's clock, Gull's false bottoms, Pip, Silas's fakes, Ermine's paste, the Magnet |
| Royal Warrant | 3000g and an exhibition of 40 | | | the goal |

The Painted Sign (40g) adds a customer a day. Day one is scripted: four customers, one lesson each (a gentle shake
and the slip; a hard shake for cloth; listening past a clank; passing when they heard everything).

## Collection and exhibition

Every thing you open goes in the ledger's collection the first time you find it (38 entries: five qualities of six
kinds, six magic things, two fakes). After an opening, anything not fake or broken can be **kept for display** instead
of sold: you give up its price. The button shows the points it adds (a star for magic, which counts double). With the
display full, it becomes **Swap**: pick something on display to sell, and the new thing takes its place. Display space: a shelf of 2, then cabinets of 3, 3 and 4 (60g, 200g, 500g). Kept
things show on the shelf behind the counter; you can sell them back at the end of a day.

The **exhibition** score is the display's prestige (1, 2, 4, 8 or 16 by quality; double for magic). Every 10 points
brings a customer a day (up to two), and each point makes a chest 1% likelier to be a colour better (up to 40%). The
Crown wants an exhibition of 40 before it grants the warrant, and past 40 an exhibition brings nothing more. Grand-looking things that sell cheap (a fine coat shows
as well as a diamond) are the efficient ones to keep.

## Evidence (`godot --headless --path . -- --sim`, 200 campaigns of 30 days)

| Style | Village | Town | Emporium | Royal Warrant | Exhibition on day 20 | Good buys | Perfect reads |
|---|---|---|---|---|---|---|---|
| Pays their price, every chest | day 4 | day 17 | day 30 | never | 0 | 50% | 21% |
| One hard shake, keeps the seller's ticks | day 3 | day 13 | day 22 | never | 0 | 71% | 63% |
| Every strength, drops when safe | day 2 | day 8 | day 12 | never (no exhibition) | 0 | 70% | 63% |
| + Witchlight and Magnet | day 2 | day 8 | day 13 | never (no exhibition) | 0 | 74% | 83% |
| + keeps grand things from the town shop on, up to 40 | day 2 | day 8 | day 15 | day 24 | 40 | 76% | 83% |

Per seller, at their own shop, profit per visit for a careful player with every tool, against paying their price
blind. Every habit pays for putting up with it, and only Tam (a few coins for anything) pays much blind:

| | Tam | Penn | Hob | Mags | Brask | Tobias | Marta | Wick | Wren | Orrin | Grit | Zara | Vex | Gull | Pip | Ermine | Silas |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Careful | +10 | +4 | +10 | +6 | +10 | +14 | +18 | +17 | +31 | +75 | +129 | +23 | +93 | +110 | +129 | +49 | +90 |
| Blind | +11 | -2 | -3 | +3 | 0 | -38 | +3 | +6 | +8 | -6 | -310 | -33 | -88 | -283 | -19 | -40 | +1 |

## What was cut, and why

1. **Auction (v1).** The game did the judging, and bidding against one bot had a dominant strategy.
2. **One unique sound per item (v2).** 28 sounds to memorize, with no logic connecting a sound to a thing.
3. **Sound families with variants, many tools, haggling, lockpicking (v3).** Too much at once, and profit came from
   habits the player couldn't see.
4. **v4** rebuilt the puzzle on shake strength. It was clear, but every profit was predictable and every seller
   alike. **v5** keeps v4's three rules and adds variety that follows its own believable rules: quality rolls around
   the chest's colour, three ways to trade, sellers with one habit and one reward each, the drop, lockpicking, and a
   collection worth keeping. **v6** gives each seller their own way of judging a chest (listening, counting, weight,
   colour, a dream, a trade they know, a sense for magic, or no idea), so each visit is a different puzzle; adds
   Granny Wick and Madame Zara, who won't have their chests shaken hard; keeps the witchlight lit; and lets you swap
   an exhibit for something grander straight after an opening.

## Open questions

- Is drag speed a good stand-in for shake strength with a real mouse? The thresholds need playtesting.
- Vex's twelve seconds: enough to shake gently and hard and decide, but is it fun or just stressful?
- Should displayed things ever sell for more than their price (a famous piece)? Today they only draw customers.
- Do players read a seller's way of judging from their pitch and Bram's first tip, or do they need the ledger open?
- Granny Wick pays a little even blind (her cloth is the reward for not shaking hard). Is that a fair kindly seller,
  or a hole?
- No save game yet, and sellers don't remember you.
