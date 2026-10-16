# LootFall design notes

## Clues: what you can learn about a sealed chest (replaces knock / peek / examine buttons)

### What was wrong

Playtest feedback on the first Counter build:

1. **Sounds didn't identify anything.** Each material had a cheap and a fine sound group with several random
   recordings, so a dozen items shared each sound and the same item sounded different from hit to hit. Item values
   were also rolled +-15%, so even a recognized item had no fixed worth.
2. **The three buttons felt useless.** The knock's two outcomes were near-identical wood samples; the peek showed an
   item without any way to know its worth; the lock text ("4 pins") read like a count of something.
3. **Weight implied the wrong thing.** A chest that dragged heavily didn't mean more items or more value, so the
   feel of it wasn't a clue anyone could reason with.

### The rules now

- **One item, one sound, one price.** Every item has a fixed value, a fixed weight in pounds and one sound
  signature: a recording at a fixed pitch plus its own word ("tiktik", "BONK"). Bigger things within a material get
  lower sounds. A chest never holds two items with the same sound, so each distinct sound is exactly one item.
- **Sound-alikes are deliberate and few.** Four pairs share a sound, always a cheap thing and a valuable one that
  differ in some other obvious way:

  | Sound | Cheap | Valuable | Tell |
  |---|---|---|---|
  | 'chink' | Purse of Copper (12g, 1 lb) | Purse of Gold (320g, 2.5 lb) | Weight: gold is heavy |
  | 'clink' | Perfume Vial (55g) | Phoenix Tear (950g) | Glow: obvious in the dark |
  | 'shff' | Love Letters (5g) | Treasure Map (400g) | Look |
  | 'flmp' | Moth-Eaten Cloak (9g) | Golden Silk Mantle (190g) | Look: gold thread |

- **The ledger is your knowledge, not an appraisal.** It starts with Uncle Bram's 11 everyday items and fills in as
  you open chests. Each entry shows the word, price and weight, and plays the sound when clicked. Unknown items stay
  blank, so an unfamiliar sound is a real gamble. It never totals a chest. Knowledge survives starting over.

### Interactions (no tool buttons)

| Action | How | Cost | What it tells you | Why it makes sense |
|---|---|---|---|---|
| Shake | Drag the chest | Free | Which items are loose inside (via the ledger) | Things rattle |
| Knock | Click the chest | Free | Hollow boom = false bottom; dry tok = solid | Hollow things resonate |
| Weigh | Drop it on the scale | Free | Pounds of contents (the scale is set so an empty chest reads 0) | Compare with what you heard |
| Look | Click the keyhole, aim the lantern | Clock runs, 5 min/s | What things look like, glows, rag bundles | You see it |
| Ledger | L or the button | Free | What you've learned so far | Your own notes |

The side panel lists only facts and perceptions: the lock maker, the words you've heard, the scale reading, the
knock. Every inference is the player's.

**Cons follow the same logic.** Silas pads chests with stones wrapped in rags, so they don't rattle. The crude version
is a chest that weighs far more than its sounds explain and knocks solid. The clever version weights a copper purse
with exactly 1.5 lb of stone so the scale says "gold". Only a look inside (or knowing Silas, or noticing the
farmhouse-green chest he calls "noble") catches it. A single clue can be faked; two rarely can.

### Teaching it (the guided first day)

Day one is scripted. Uncle Bram, who is handing over the shop, stands at the counter, and each customer brings a
chest built to teach one idea, in order:

1. **Hob:** two known sounds. Shake, find them in the ledger, add up, offer less.
2. **Wren:** one sound that isn't in the ledger. Unknown means a gamble; you learn sounds by opening.
3. **Penn:** a 'chink' that weighs too much for copper. The scale, and the first sound-alike.
4. **Marta:** 'shff' could be letters or a map. The lantern.
5. **Silas:** says the purse is gold, and the scale agrees. The lantern finds a rag-wrapped stone; the chest is
   farmhouse green. Clues can be faked.
6. **Pip:** knows his chest but never knocks. A hollow boom: false bottom.

Then the back room: pick, don't force (glass shatters and false bottoms stay hidden). Each reveal plays the item's
sound again with "NEW" on the card. From day two on, customers are random and Bram is gone.

## How you get chests: the Counter (replaces the auction)

### Why the auction was cut

Playtesting found the auction dull, and the cause was structural, not tuning:

1. **The game did the judging.** The appraisal bar showed a range and a best guess. With the answer on screen,
   the clues (text) and sounds (decoration) had nothing left to do.
2. **A rising auction against one bot has a dominant strategy:** keep bidding until your number. From the 35%
   opening price to the median value is about 11 raises of 10%, roughly half of them the player's, and none of
   those clicks is a decision.
3. **Risk never arrived.** The simulation showed profit at every bid limit from 0.5x to 1.3x the estimate.

### Principles for the replacement

1. **Evidence, never answers.** Nothing states a chest's value before you buy it. You infer it from what you hear,
   feel and see, and from the person selling it.
2. **The core clues are free; the decisive ones cost.** Shaking, knocking and weighing cost nothing, so players do
   them every time. Looking inside and haggling cost shop time, and haggling also costs the seller's patience.
3. **Mastery lives in the player's head.** Each item always sounds, weighs and sells the same; sellers keep their
   personalities between days. Players get better by learning, not by unlocking numbers.
4. **Variety from people and situations, not menus.** What you check depends on who is selling and what you suspect.
5. **Rhythm:** judgment by day at the counter, payoff by night in the back room.

### The loop

- **Day (9:00 to 17:00).** Six or seven sellers queue at the counter, each with one sealed chest, a story and an
  asking price. You learn about the chest by handling it (see "Clues" below), then haggle. Offers cost 10 minutes.
  Sellers have hidden walk-away prices and patience; mood is the only visible hint. Some push back once even on
  acceptable offers, so a counter doesn't simply mean "too low".
- **Night (back room).** Open the day's purchases (lockpick, then the reveal), see what each one held against what
  you paid, and pay rent every third day (150g, then +100g each time). Every item comes out with the sound it made
  in the chest, and anything new goes into the ledger.

### The regulars

| Seller | Type | What to learn |
|---|---|---|
| Hob the Farmer | Clueless | Prices on a hunch. Bargains and duds alike. |
| Old Marta | Cautious | Prices by the lock. Fair and firm. |
| Baron Vex | Braggart | Asks far too much. Sometimes secretly broke. |
| Pip the Sharp | Collector | Knows the contents, never knocks: the false bottom is your edge. |
| Captain Gull | Salvager | One price. Any lower offer and he leaves. |
| Silas | Con | Usually junk padded with rag-wrapped stones. Folds suspiciously fast. Sweats when you weigh or look. |
| Widow Penn | Desperate | Sells cheap. Very patient. |
| Wren the Apprentice | Novice | Underprices fragile tower stock. Force the lock and it shatters. |

Chest colors follow their real origin, so a "Noble Estate" chest in farmhouse green is a tell.

### Evidence (300 simulated 15-day campaigns, `godot --headless --path . -- --sim`)

The simulated players hear every loose item but only know what's in their ledger, which grows as they open chests
(unknown sounds are guessed at 120g).

| Style | Survives | Profit/day | Ledger at the end |
|---|---|---|---|
| Pays the asking price | 1% | -35g | 14/28 |
| Lowballs every seller | 19% | -4g | 15/28 |
| Haggles without inspecting | 4% | -24g | 13/28 |
| Listens only | 82% | +486g | 23/28 |
| Listens and weighs | 78% | +503g | 23/28 |
| **Listens, weighs and looks** | **89%** | **+635g** | 25/28 |
| Inspects but never haggles | 30% | +158g | 16/28 |
| Expert who knows every sound | 96% | +764g | 28/28 |

Each clue adds something, and knowledge pays: the expert out-earns the same habits with a partial ledger by a
fifth. "Listens and weighs" survives less often than "listens only" because Silas's weighted purse fools the scale
(2.1 cons bought per campaign); the lantern fixes that. That's the intended lesson: cross-check clues.

### Alternatives considered

- **Falling-price (Dutch) auction:** fixes the filler clicks with one tense timing decision, but keeps faceless
  bidders and doesn't fix the evidence problem. A good candidate for an occasional "estate sale" event.
- **Sealed-bid silent auction:** good portfolio thinking, little tactile play.
- **Salvage dives (push-your-luck):** fun, but a different and much bigger game.

### Open questions

- Do 24 distinct recordings stay recognizable by ear once players stop reading the words? Several are noisy
  rattles (coins, paper, cloth); a pass with purpose-made sounds would help.
- More sound-alike pairs as the catalog grows, each with one clear tell.
- Selling is automatic at the item's value. Keeping items for sets, or selling to specific buyers, is the next
  place for judgment.
- Upgrades (better lantern, scale, lockpicks) and returning-seller memory (they remember lowballs).
