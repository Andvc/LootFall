# LootFall design notes

## The game in one paragraph

You inherit a roadside stall from Uncle Bram. People bring you chests and tell you what they heard when they shook
them. Shake each one yourself (gently, firmly, hard), drop it to listen for a false bottom, and work out what they
missed, made up or misjudged. Tick what you think is inside on your appraisal slip and trade the way each customer
trades: pay their price, haggle, or name your own. Pick the lock, wrench the lid, and watch everything fly out: each
thing's true quality, a tick or a cross against your slip, and the profit stamped across the screen. Keep the grand
things for your exhibition, which draws more and better customers. The goal is the Royal Warrant.

## The puzzle: three rules

1. **Every kind of thing has one sound, and it only sounds when the shake is strong enough to stir it.** Coins
   jingle, bottles slosh and jewelry tinkles at the gentlest shake; metal only clanks at a firm one; cloth flumps and
   paper rustles only at a hard one.
2. **While metal clanks, you can't hear anything delicate.** Shake gently and the metal stays put.
3. **Sellers shake too, as hard as they like, and price only what they think is there.** Their pitch says how hard
   they shook and what they heard, and the slip starts ticked with it.

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
| Shake gently, firmly, hard | start | which kinds are inside | none |
| Drop it (lift it high, let go) | start; matters from the village | a hollow BOOM means a false bottom: one more thing, padded so it never rattles, that nobody has priced | glass may break (half the time each); Marta hates it |
| Witchlight (150g) | town | magic glows when it makes a sound | none |
| Magnet (300g) | Emporium | iron can't move: no clanks to drown things out, and gilded fakes go quiet | Lady Ermine hates it |

**Left out: the scale.** A weight only means something if you add up what each thing should weigh in your head, which
isn't intuitive, and the drop already gives the one weight-like fact that matters (hollow or solid).

**Lockpicking** (from the village shop) comes after you buy a locked chest: set each pin when the pick passes the
green. A masterful pick (no slips, mostly perfect) makes everything worth 25% more; the crowbar is quick but scratches
everything (-10%) and breaks glass. Fine Lockpicks (50g) widen the sweet spot.

## Sellers: one habit each, and a reward for putting up with it

Three ways to trade:
- **Fixed price**: buy or pass.
- **Haggle**: they ask high (about 1.9 times what they think it's worth). You get one offer: they take it if it's at
  least what they'd really accept, name a final price if not, or storm off if it's under 35% of their ask.
- **Name a price**: they wait for your offer. Two tries.

| Seller | Shop | Shakes | Trades | Habit | Why put up with it |
|---|---|---|---|---|---|
| Little Tam | stall | not at all | name a price | no idea what anything's worth | a few coins buys anything |
| Widow Penn | stall | gently | fixed | metal and soft things go unheard | |
| Hob the Farmer | stall | hard | haggle | asks double | comes down to a fair price |
| Mags the Fishwife | stall | firmly | fixed | soft things and masked delicate ones go unheard | |
| Sergeant Brask | village | hard | fixed | army lockboxes, nearly always locked | lockpicking practice, masterful bonus |
| Tobias the Tinker | village | firmly | haggle | often claims a sound that isn't there | his misses, if you catch the lie |
| Old Marta | village | gently | fixed | **drop her chest and it's +50%** | her old chests often have false bottoms (40%) |
| Wren the Apprentice | town | gently | name a price | no idea of prices | brings magic (15%); takes about what she heard |
| Magister Orrin | town | hard | fixed | proud prices | brings magic (15%) he never checks for |
| Grit the Miner | town | hard | haggle | **thinks every tinkle is a diamond** | prices everything but metal and jewelry at half |
| Baron Vex | Emporium | firmly | fixed | **12 seconds to decide** | the best chests, often better than their colour (40% one step up) |
| Captain Gull | Emporium | hard | haggle | | sea chests, often with a false bottom (25%) |
| Pip the Sharp | Emporium | gently, then hard | fixed | hears everything | only magic and false bottoms get past him |
| Lady Ermine | Emporium | gently | fixed | **use a magnet and it's +50%**; some jewels are paste she was sold | a modest price (1.1 to 1.3 times) |
| Silas | Emporium | (packed it himself) | fixed | gilded iron sold as gold and jewels | sells for half: real goods come cheap |

Fixed-price sellers ask 1.4 to 1.8 times what they think it's worth (2 to 4g a tier if they heard nothing). Each
seller's chest holds something their shaking can't hear about 55% of the time (45% for the Emporium's hard and firm
shakers), which keeps buying blind from paying while listening does.

## Stages

| Shop | Cost | Customers/day | Chests | New |
|---|---|---|---|---|
| Roadside Stall | start | 4 | mostly wood | jingle, clank, flump; three strengths; clanks drown jingles; from day 2, haggling and naming a price |
| Village Shop | 60g | 5 | mostly green | slosh and rustle; locks; false bottoms and the drop; Tobias's tall tales; Marta |
| Town Shop | 150g | 6 | mostly blue | jewelry; magic and the Witchlight; Grit |
| The Emporium | 900g | 7 | blue and purple | Vex's clock, Silas's fakes, Ermine's paste, the Magnet |
| Royal Warrant | 4000g and an exhibition of 40 | | | the goal |

The Painted Sign (40g) adds a customer a day. Day one is scripted: four customers, one lesson each (a gentle shake
and the slip; a hard shake for cloth; listening past a clank; passing when they heard everything).

## Collection and exhibition

Every thing you open goes in the ledger's collection the first time you find it (38 entries: five qualities of six
kinds, six magic things, two fakes). After an opening, anything not fake or broken can be **kept for display** instead
of sold: you give up its price. Display space: a shelf of 2, then cabinets of 3, 3 and 4 (60g, 200g, 500g). Kept
things show on the shelf behind the counter; you can sell them back at the end of a day.

The **exhibition** score is the display's prestige (1, 2, 4, 8 or 16 by quality; double for magic). Every 10 points
brings a customer a day (up to two), and each point makes a chest 1% likelier to be a colour better (up to 40%). The
Crown wants an exhibition of 40 before it grants the warrant. Grand-looking things that sell cheap (a fine coat shows
as well as a diamond) are the efficient ones to keep.

## Evidence (`godot --headless --path . -- --sim`, 300 campaigns of 30 days)

| Style | Village | Town | Emporium | Royal Warrant | Exhibition on day 20 | Good buys | Perfect reads |
|---|---|---|---|---|---|---|---|
| Pays their price, every chest | day 3 | day 15 | day 25 | never | 0 | 51% | 25% |
| One hard shake, keeps the seller's ticks | day 3 | day 11 | day 21 | never | 0 | 75% | 56% |
| Every strength, drops when safe | day 2 | day 6 | day 10 | never (no exhibition) | 0 | 75% | 68% |
| + Witchlight and Magnet | day 2 | day 6 | day 11 | never (no exhibition) | 0 | 80% | 91% |
| + keeps grand things from the town shop on | day 2 | day 7 | day 14 | day 21 | 102 | 82% | 92% |

Per seller (careful player with every tool, at the seller's own shop), every quirk pays for putting up with it:
Marta +20g a visit, Grit +123g, Vex +198g, Gull +193g, Ermine +48g, against what a blind buyer makes from them
(+3g, -331g, +80g, -32g, -94g).

## What was cut, and why

1. **Auction (v1).** The game did the judging, and bidding against one bot had a dominant strategy.
2. **One unique sound per item (v2).** 28 sounds to memorize, with no logic connecting a sound to a thing.
3. **Sound families with variants, many tools, haggling, lockpicking (v3).** Too much at once, and profit came from
   habits the player couldn't see.
4. **v4** rebuilt the puzzle on shake strength. It was clear, but every profit was predictable and every seller
   alike. **v5** keeps v4's three rules and adds variety that follows its own believable rules: quality rolls around
   the chest's colour, three ways to trade, sellers with one habit and one reward each, the drop, lockpicking, and a
   collection worth keeping.

## Open questions

- Is drag speed a good stand-in for shake strength with a real mouse? The thresholds need playtesting.
- Vex's twelve seconds: enough to shake gently and hard and decide, but is it fun or just stressful?
- Should displayed things ever sell for more than their price (a famous piece)? Today they only draw customers.
- No save game yet.
