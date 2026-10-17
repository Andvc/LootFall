# LootFall design notes

## The game in one paragraph

You inherit a roadside stall from Uncle Bram. People bring you chests and tell you what they heard when they shook
them. Shake each one yourself, gently, firmly and hard, and listen for what they missed. Tick what you think is
inside on your appraisal slip, buy if it's worth more than they ask, and open it on the spot: every thing comes flying
out with a tick or a cross against your slip. Earnings go into the shop: each renovation brings richer customers and
one new thing to listen for. The goal is the Royal Warrant.

## The puzzle: three rules

1. **Every kind of thing has one sound, and it only sounds when the shake is strong enough to stir it.** Light, hard
   things (coins jingle, bottles slosh, jewelry tinkles) rattle at the gentlest shake. Metal is heavy: it only clanks
   at a firm shake. Soft things (cloth flumps, paper rustles) only make a sound when you shake hard.
2. **While metal clanks, you can't hear anything delicate.** Shake gently and the metal stays put, so the quiet
   things come through.
3. **Sellers shake too, as hard as they like, and price only what they heard.** Their pitch says how hard they shook
   and what they heard ("Gave it a right hard shake. Heard a clank. 7g?"), and the slip starts ticked with it.

So every chest asks one question: what did this seller miss (or make up)? How hard you drag sets the shake strength;
a meter under the chest shows it and records which sounds you heard at each strength. Everything is in plain sight:
there's nothing to memorize but six everyday sounds and which shake wakes them.

Shake strength comes from how fast the chest moves (gentle from 40 px/s, firm from 520, hard from 1150). The
`ShakeBox` gates its physics impacts by `Chest.Audible`, and the shake test checks the two agree.

## Feedback

- **The slip** adds up what you ticked: "If you're right, it's worth 17g. Hob asks 7g. +10g if you're right." The
  value of a kind of thing is common knowledge (it's in the ledger); what's in this chest is your judgment.
- **Opening** checks each thing against the slip: a tick or a cross, "you missed it", "magic!", "FAKE". Then the
  verdict stamp (profit or loss), coins flying into your purse, and PERFECT READ when every tick was right.
- **Every result says why**, in terms of the rules: "The clank drowned out the jingle for Hob: Pouch of Copper,
  unpriced." "Tobias made up the slosh: there were no bottles."
- **Passing still teaches**: the seller opens the lid to show you, checked against your slip.

## Stages: one new thing per renovation

| Shop | Cost | Customers/day | Things are worth | New |
|---|---|---|---|---|
| Roadside Stall | start | 4 | x1 | jingle, clank, flump; three shake strengths; clanks drown out jingles |
| Village Shop | 60g | 5 | x4 | two more sounds in the same scheme (slosh: gentle and delicate; rustle: hard); Tobias, who claims a sound that isn't there |
| Town Shop | 100g | 6 | x8 | jewelry (tinkle); mages, whose things are often magic (x4); the Witchlight (150g) |
| The Emporium | 600g | 7 | x16 | Silas, who sells gilded iron as gold and jewels, cheap; the Magnet (300g) |
| Royal Warrant | 3500g | | | the goal |

The Painted Sign (40g, from the village) adds a customer a day.

The two tools each answer one question the ear can't, by the same rules:

- **Witchlight**: the room goes dark and magic glows when it makes a sound. No seller ever prices magic.
- **Magnet**: iron can't move, so it makes no sound. Metal goes quiet (nothing drowns out the delicate things), and so
  does a crook's gilded fake, while real gold still jingles.

## The first day (scripted, one lesson per customer)

| Customer | Shook it | Chest | Lesson |
|---|---|---|---|
| Little Tam | not at all | coins | a gentle shake, the slip, buying, opening |
| Widow Penn | gently | a cloak | soft things only sound at a hard shake |
| Hob the Farmer | hard | a knife and coins | clanks drown out jingles: shake gently to hear past them |
| Mags the Fishwife | firmly | a knife | she heard everything: pass, and see what was inside |

## Regulars

| Seller | Shop | Shakes | Blind spot |
|---|---|---|---|
| Little Tam | stall | not at all | everything (asks a few coins) |
| Widow Penn | stall | gently | metal, cloth, paper |
| Hob the Farmer | stall | hard | delicate things under a clank |
| Mags the Fishwife | stall | firmly | cloth and paper; delicate things under a clank |
| Sergeant Brask | village | hard | delicate things under a clank |
| Tobias the Tinker | village | firmly | as Mags, and he often claims a sound that isn't there |
| Old Marta | village | gently | metal, cloth, paper |
| Wren the Apprentice | town | gently | as Penn; brings magic |
| Magister Orrin | town | hard | as Hob; brings magic |
| Baron Vex | emporium | firmly | as Mags |
| Captain Gull | emporium | hard | as Hob |
| Pip the Sharp | emporium | gently, then hard | only magic |
| Silas | emporium | (knows) | sells gilded iron as the real thing, at 50-70% of its worth |

Sellers ask 1.4 to 1.8 times the value of what they heard (2 to 4g a tier if they heard nothing). Each seller's chest
holds something their shaking can't hear about 55% of the time (40% for the richest), which keeps buying blind from
paying while listening does.

## Kinds of things

| Kind | Sound | Stirs at | Note | Stall | Village | Town | Emporium |
|---|---|---|---|---|---|---|---|
| Coins | jingle | gentle | delicate | 12g | 48g | 96g | 192g |
| Metal | clank | firm | loud, iron | 5g | 20g | 40g | 80g |
| Cloth | flump | hard | soft | 6g | 24g | 48g | 96g |
| Bottles | slosh | gentle | delicate | 8g | 32g | 64g | 128g |
| Paper | rustle | hard | soft | 5g | 20g | 40g | 80g |
| Jewelry | tinkle | gentle | delicate | 20g | 80g | 160g | 320g |

A chest holds at most one of each kind, so the slip is one tick per kind. Magic is worth four times as much; a fake
is worth 1g.

## Evidence (`godot --headless --path . -- --sim`, 200 campaigns of 30 days)

| Style | Village | Town | Emporium | Royal Warrant | Good buys | Perfect reads |
|---|---|---|---|---|---|---|
| Buys every chest | day 4 | day 10 | day 18 | never | 57% | 31% |
| Trusts the seller's slip | never | never | never | never | - | - |
| One hard shake, keeps the seller's ticks | day 4 | day 11 | day 25 | never | 97% | 72% |
| Every strength, no tools | day 3 | day 7 | day 12 | day 21 | 91% | 75% |
| Every strength and both tools | day 3 | day 7 | day 12 | day 19 | 100% | 99% |

Profit per customer for the careful player: 5g at the stall, 8g in the village, 25g in town (21g without the
witchlight), 77g at the Emporium (53g without the tools). Only players who shake at every strength reach the Warrant.

## What was cut, and why

1. **Auction (v1).** The game did the judging, and bidding against one bot had a dominant strategy.
2. **One unique sound per item (v2).** 28 sounds to memorize, with no logic connecting a sound to a thing.
3. **Sound families with variants, many tools, haggling, lockpicking (v3).** Too much at once: pitch variants,
   scale, keyhole, compass, lodestone, hammer and twelve pricing habits. Profit came from habits the player couldn't
   see. v4 keeps the families' everyday sounds, drops the variants, and builds the whole game on one physical idea:
   how hard you shake decides what you hear, for you and for the seller.

## Open questions

- Is drag speed a good stand-in for shake strength with a real mouse? The thresholds need playtesting; the meter
  shows the strength live.
- The slip adds up the values for you. Does that make the decision too easy, or is the listening enough of a puzzle?
- Selling is automatic at the item's value. Keeping items for collections, or selling to particular buyers, is a
  natural next layer.
