# LootFall design notes

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
2. **Every input costs something:** shop time, a seller's patience, or money.
3. **Mastery lives in the player's head.** Sounds map consistently to materials and quality; sellers keep their
   personalities between days. Players get better by learning, not by unlocking numbers.
4. **Variety from people and situations, not menus.** What you check depends on who is selling and what you suspect.
5. **Rhythm:** judgment by day at the counter, payoff by night in the back room.

### The loop

- **Day (9:00 to 17:00).** Six or seven sellers queue at the counter, each with one sealed chest, a story and an
  asking price.
  - **Shake (free):** drag the chest. Loose items are physics bodies; each hit plays its material's recording
    (cheap and fine variants) with a matching word. Heavy chests lag behind the mouse and land harder.
  - **Knock (20 min):** a false bottom sounds hollow.
  - **Peek through the keyhole (1 h):** a dim look at one random thing inside.
  - **Examine the lock (30 min):** the maker, which sets picking difficulty, and whether it's a painted fake.
  - **Haggle:** offers cost 10 minutes. Sellers have hidden walk-away prices and patience; mood is the only
    visible hint. Some push back once even on acceptable offers, so a counter doesn't simply mean "too low".
- **Night (back room).** Open the day's purchases (lockpick, then the reveal), see what each one held against what
  you paid, and pay rent every third day (150g, then +100g each time).

### The regulars

| Seller | Type | What to learn |
|---|---|---|
| Hob the Farmer | Clueless | Prices on a hunch. Bargains and duds alike. |
| Old Marta | Cautious | Prices by the lock. Fair and firm. |
| Baron Vex | Braggart | Asks far too much. Sometimes secretly broke. |
| Pip the Sharp | Collector | Knows the contents, never knocks: the false bottom is your edge. |
| Captain Gull | Salvager | One price. Any lower offer and he leaves. |
| Silas | Con | Usually stones and a painted lock. Folds suspiciously fast. Sweats when you look closely. |
| Widow Penn | Desperate | Sells cheap. Very patient. |
| Wren the Apprentice | Novice | Underprices fragile tower stock. Force the lock and it shatters. |

Chest colors follow their real origin, so a "Noble Estate" chest in farmhouse green is a tell.

### Evidence (300 simulated 15-day campaigns, `godot --headless --path . -- --sim`)

| Style | Survives | Profit/day |
|---|---|---|
| Pays the asking price | 0% | -34g |
| Lowballs every seller | 14% | -15g |
| Haggles without inspecting | 28% | +10g |
| Inspects without haggling | 15% | +77g |
| **Inspects and haggles** | **87%** | **+658g** |

No mindless strategy survives. Inspecting and haggling are each necessary and only work together. The sim's
"inspector" reads chests perfectly; real players won't, which is where knowing the regulars should pay off.

### Alternatives considered

- **Falling-price (Dutch) auction:** fixes the filler clicks with one tense timing decision, but keeps faceless
  bidders and doesn't fix the evidence problem. A good candidate for an occasional "estate sale" event.
- **Sealed-bid silent auction:** good portfolio thinking, little tactile play.
- **Salvage dives (push-your-luck):** fun, but a different and much bigger game.

### Open questions

- Do the recorded sounds read clearly enough on their own? (Words mirror them for accessibility.)
- Selling is automatic at the item's value. Keeping items for sets, or selling to specific buyers, is the next
  place for judgment.
- Upgrades (better lantern, scale, lockpicks) and returning-seller memory (they remember lowballs).
