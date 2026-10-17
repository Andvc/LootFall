# LootFall design notes

## The game in one paragraph

You inherit a roadside stall from Uncle Bram. People bring you chests. You shake them and listen, use whatever
tools you've bought, and decide whether the asking price is worth it. Whatever you buy, you open right away, and
see what you paid against what it held and why. Earnings go into the shop: a bigger shop brings new customers
with new things to learn; tools tell you more about each chest. The goal is the Royal Warrant.

## Pillars

1. **Sounds you already know.** Coins jingle, metal clanks, bottles slosh, cloth flumps, paper rustles, jewelry
   tinkles. A player can name the family of everything in a chest on the first shake, without being taught.
2. **Small, consistent rules within a family.** Copper jingles dull, silver bright, gold heavy. Iron clanks, steel
   rings on. A plain slosh is a drink; a fizzing one is magic. The same rule always holds, so learning it pays.
3. **One new thing at a time.** The stall only sees four families and seven kinds of things. Each renovation brings
   new customers, things and sounds; each tool arrives with a tip on how to use it.
4. **Every profit has a reason.** Each seller prices by a habit (a hunch, the weight, the lock, the kind of sound),
   and each habit has a blind spot one of your clues can see through. The results screen names it.
5. **Payoff within a minute.** Buy, open, see the result, next customer. No waiting until night.
6. **Evidence, never answers.** Nothing states what a chest is worth before you buy it. The ledger holds only what
   you've learned: what each thing sounds like, weighs and sells for.

## The loop

- **A customer arrives** with a chest and an asking price.
- **Inspect:** drag to shake. Each thing inside rattles on its own and the panel lists what you heard
  ("Heard 3 things: jingle, slosh, flump"). Then any tools you own.
- **Decide:** buy, pass, or (from day 2) make **one offer**. If the offer is at or above what they'll really take,
  it's yours at that price; if not, they name a final price and you buy at that or pass. One clear decision, no
  clicking up a ladder.
- **Open it now:** lockpick if it's locked (from the village shop on), hold to open, items come out one by one with
  the sound each made in the chest. Then the verdict: paid, worth, profit, and a sentence on why ("Sergeant Brask
  priced it by weight: 1.3 lb. A steal.").
- **End of day:** summary, then spend on renovations and tools. If you end a day broke, Bram tops you up to 30g.

## Sound families

| Family | Sound | Within the family |
|---|---|---|
| Coins | jingle | dull = copper, bright = silver, heavy = gold (and gilded iron fakes) |
| Metal | clank | dull = old iron, rings = steel, rings and hums = enchanted |
| Bottles | slosh (bubbles) | big = ale, thick = honey, small = potion, fizzy = magic |
| Cloth | flump | heavy = wool, light = rags or fine silk (look before you trust it) |
| Paper | rustle | loose = letters, scrolls, maps; thumpy = books |
| Jewelry | tinkle | dull = brass, bright = silver or gold, sparkly = a gem |

The base word is all you see at first. Variants are in the sound itself (pitch, an added fizz, hum or thump), so a
practiced ear learns them; the ear trumpet spells them out ("bright jingle").

## Progression

| Shop | Cost | Customers/day | Who comes in | New things to learn |
|---|---|---|---|---|
| Roadside Stall | start | 4 | Hob, Widow Penn, Mags, Little Tam | jingle, clank, slosh, flump |
| Village Shop | 120g | 5 | Sergeant Brask, Tobias the Tinker, Old Marta | silver, steel, paper; locked chests |
| Town Shop | 300g | 6 | Wren, Magister Orrin | potions, magic, jewelry |
| The Emporium | 900g | 7 | Baron Vex, Captain Gull, Silas, Pip | gold, gems, false bottoms, fakes |
| Royal Warrant | 2000g | | | the goal |

| Tool | Shop | Cost | What it tells you |
|---|---|---|---|
| Shop Scale | stall | 25g | Weight of the contents. A wool coat outweighs a rag; gold outweighs copper. |
| Keyhole Lens | stall | 40g | 2 peeks a day; you see the one thing nearest the keyhole, and the view says how many you can't see. |
| Ear Trumpet | village | 60g | Names the variant of each sound. |
| Lantern Lens | village | 120g | 3 peeks a day; you see the two nearest things. |
| Fine Lockpicks | village | 80g | A wider sweet spot, so more masterful picks (+25%). |
| Painted Sign | village | 100g | One more customer a day. |
| Mana Compass | town | 150g | The needle swings toward magic: still, a twitch, hard. |
| Brass Periscope | town | 300g | 4 peeks a day; you see everything. |
| Lodestone | emporium | 150g | Pulls at iron and steel. Gold doesn't stick; gilded iron slugs do. |
| Tapping Hammer | emporium | 120g | Click the chest to knock: a false bottom booms hollow. |

## Regulars and their blind spots

| Seller | Shop | Prices by | Your edge |
|---|---|---|---|
| Hob the Farmer | stall | a hunch | listen: he's often way off |
| Widow Penn | stall | need: asks low | a cheap offer usually works |
| Mags the Fishwife | stall | how many things rattle | many rags cost as much as many coats: listen and weigh |
| Little Tam | stall | a flat few coins | usually junk, sometimes not |
| Sergeant Brask | village | weight | the scale: light silver is cheap, heavy iron dear |
| Tobias the Tinker | village | the kind of sound, not which | the ear trumpet: silver at copper prices |
| Old Marta | village | the lock | a padlock on good things |
| Wren the Apprentice | town | magic high, the rest as junk | the compass: plain good things come cheap |
| Magister Orrin | town | the same, never haggles | the compass |
| Baron Vex | emporium | double, folds when pushed | a firm low offer |
| Captain Gull | emporium | one price | peek: gold and jewels, or barnacles |
| Silas | emporium | a con: stones and gilded iron | the scale, a peek and the lodestone |
| Pip the Sharp | emporium | fair | the hammer: he doesn't know about false bottoms |

## Evidence (`godot --headless --path . -- --sim`, 200 campaigns of 40 days)

Simulated players hear every family, know what's in their ledger (it grows as they open chests), and buy upgrades
in a sensible order.

| Style | Village | Town | Emporium | Royal Warrant | Good buys |
|---|---|---|---|---|---|
| Pays the asking price | day 9 | day 34 | never | never | 54% |
| Haggles blind | day 3 | day 12 | day 23 | never | 66% |
| Listens, no tools | day 3 | day 12 | day 23 | never | 76% |
| Listens and uses tools | day 4 | day 14 | day 24 | day 36 | 97% |
| Practiced ear and tools | day 4 | day 13 | day 23 | day 35 | 98% |

The stall is gentle: anyone gets through it. From the town shop on, profit per customer is about 20g with tools and
12g without; at the Emporium, blind play earns nothing (cons and fakes) while tool users make about 25g a customer.
Only players who use their tools reach the Royal Warrant.

## What was cut, and why

1. **Auction (v1).** The game did the judging (it showed an estimate), and bidding against one bot had a dominant
   strategy: raise until your number.
2. **One unique sound per item (v2).** 28 sounds to memorize from the start, with no logic connecting a sound to a
   thing; the knock, peek and lock buttons each gave little; payoff waited until night.
3. **This version (v3)** groups sounds by real-world family, introduces families and tools gradually, opens chests
   immediately, and ties every profit to a seller habit the player can learn.

## Open questions

- Do the synthesized slosh, fizz and hum read as liquid and magic to players? They're the only non-recorded family
  sounds.
- The middle of the game (village to town) takes about ten days; is that too long?
- Selling is automatic at the item's value. Keeping items for collections, or selling to particular buyers, is a
  natural next layer.
