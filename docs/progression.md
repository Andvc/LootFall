# LootFall progression framework

The numerical foundation for growing LootFall from eight slots and a few hours of play into a game of abundance: 149
equipment slots from head to toe, twelve rarities, a 3,259-node talent tree with endless paragon, per-slot forge,
ranks and mastery, a codex of first finds, and two prestige layers, all of it able to keep a player progressing for
thousands of hours without breaking. It is engine-free C# in `scripts/Progression` (nothing in the game uses it yet),
and `tools/progsim` simulates thousands of hours of play from it in seconds.

```
dotnet build tools/progsim -c Release
dotnet tools/progsim/bin/Release/net8.0/progsim.dll --check                    # the acceptance test (CI runs it)
dotnet tools/progsim/bin/Release/net8.0/progsim.dll --hours=3000 --profile=idle # one player's report
dotnet tools/progsim/bin/Release/net8.0/progsim.dll --tune                      # refit the open-ended systems' knobs
dotnet tools/progsim/bin/Release/net8.0/progsim.dll --sweep                     # robustness: each knob -20% / +25%
dotnet tools/progsim/bin/Release/net8.0/progsim.dll --content-sweep             # robustness: less and more content
```

Profiles: `active` (decides at every step), `idle` (checks in every four hours), `daily` (once a day), `no-prestige`
(never resets). `--debug-talents` and `--debug-prestige` print the talent allocation and every transcendence decision.

## What the research says

Notes from a survey of how long-running incremental and loot games keep their economies working, and how they break.
(The web pages themselves couldn't be fetched from this environment, so this draws on search results' summaries and
on the published designs below; the numbers quoted are those designs' own.)

- **Walls and runaways come from mismatched growth rates.** Anthony Pecorella's analysis of idle games (Kongregate)
  frames everything as costs growing exponentially (×1.07 to ×1.15 a purchase) against production growing
  polynomially; where cost outgrows production you hit a wall, where production outgrows cost you run away. A stable
  economy needs the two rates in a fixed relation, and a reset (prestige) to move the relation on.
- **Prestige currency grows as a root or a log of what was earned.** AdVenture Capitalist's angels go with the
  square root of lifetime earnings (doubling them takes about four times the earnings); Clicker Heroes moved its
  second layer to Ancient Souls = 5 × log10(Hero Souls) because the first formula inflated. The Prestige Tree and
  similar games needed soft caps for the same reason. The lesson: a prestige layer's power must grow sublinearly in
  its currency, and the currency's feedback on itself must stay below one.
- **Difficulty is a premium per level.** Diablo III's Greater Rifts add +17% monster health a level, with a practical
  ceiling where gear stops keeping up (around 150); every system the player has must together pay that premium.
- **Additive buckets, few multipliers.** Diablo IV separates additive ("increased") and multiplicative ("more")
  damage buckets, and keeps the multiplicative ones few, because many independent multipliers compound without
  bound. World of Warcraft's armour formula (reduction = A / (A + K × attacker level)) and capped chances turn ratings
  into percentages that neither vanish nor pass their caps as numbers grow. Item budgets are quality × item level ×
  a slot modifier.
- **Item level gates power, tiers keep ranges apart.** Path of Exile's item level gates affix tiers; Last Epoch's
  affix tiers (T1 to T7) don't overlap. Path of Exile's passive tree (1,325 nodes) is mostly small nodes on the way to
  notables and keystones, with cluster jewels adding more: breadth with a few strong anchors.
- **Big numbers need their own type.** Doubles overflow at 1.8e308; break_infinity-style numbers (a mantissa and an
  exponent) reach 1e(9e15) at a fraction of BigInteger's cost.
- **Engagement**: balance faucets against sinks; always show a next goal; open new layers over time; and expect
  hedonic adaptation, so progress has to keep arriving in new forms, not only as bigger numbers.

What the framework takes from this: measure everything against the difficulty premium in one unit; make every
system's long-run growth a power of time with a known exponent; keep every feedback loop's gain below one, with
margin; let percent stats add within buckets and keep "more" multipliers to a few budgeted systems; turn ratings into
chances against the depth's scale; use big numbers; and make prestige trees polynomial in cost so their power grows
as a root of their currency.

## The unit: level-equivalents

Everything absolute grows by R = 1.10 a depth: monster health and attack, item stats, gold, XP. A multiplier m is
worth log(m) / log(R) **LE** (level-equivalents): the number of depths of monster growth it pays for. An item found at
depth d has item level d, so gear keeps up with the absolute scale by itself (the treadmill); what it can't keep up
with is the **premium**: monsters at depth d are also R^(p (d - 1)) stronger than gear from there, p = 0.12 for health
and 0.11 for attack (`Params.PremiumHpRate`, `PremiumAtkRate`). Every other system exists to pay that premium. The
hero clears depth d when what everything else gives, C (in LE), covers it:

    C >= p (d - 1) + (a boss margin)        so the deepest depth reached is about  d* = C / p.

Power in this document is the mean of damage and health LE; a "share" of power is a system's knockout (the LE lost
without it at the current reach) over the sum of all of them.

## Stats: channels, buckets and the pool

Every source lands in a few channels (`Stats.cs`), and the channels combine one fixed way:

- **Flat** stats (damage, health, armour, ratings) are kept as LE and add up as absolute amounts across slots
  (log-sum-exp). Gear and the forge give them.
- **Percent** stats add up within their bucket, and each bucket multiplies once (increased damage, increased health,
  crit damage, attack speed, the rating buckets...). Affixes and minor talents give them; stacking a bucket gives
  diminishing returns by construction.
- **More** stats are LE and add as LE (multiply). Only a few budgeted systems feed them: notables and keystones, the
  pool (ranks and mastery), the codex, paragon and the prestige trees.
- **Ratings** become chances against the depth's scale: cap / (1 + K R^(d-1) / rating), so a rating keeps its worth as
  monsters grow and never passes its cap (crit 75%, multi-hit 50%, armour 85%, dodge 60%, block 50%).

**Why ranks and mastery are a pool.** Flat stats add across slots, so if one slot's bonus exceeds the others' by Δ LE
it holds R^Δ / (R^Δ + N − 1) of the total: with 149 slots, 44% at Δ = 50 and 99.99% at Δ = 100. Any bonus that grows
without bound on one slot's flat stats therefore makes that slot all that matters (an early version ended with one slot
at rank 2,000 while the average was 70). As a pool, every rank and mastery level in any slot multiplies the hero's
damage or health by the same amount (by the slot's offense share), polynomial costs make spreading the levels the best
buy, and a newly opened slot is the cheapest to raise. The same rule applies to anything added later: per-slot growth
goes into the pool, never onto a slot's flat stats. The forge stays on flat stats because its exponential cost keeps
every slot within a few levels of the others.

## Systems

| System | Paid with | Cost curve | Gives | Reset by | Grows like |
|---|---|---|---|---|---|
| Gear (149 slots) | drops | | flat stats at the item level; rarity +4 LE a tier, quality up to +2 LE; affixes into percent buckets | ascension | tracks depth (treadmill) |
| Rarity (12 tiers) | | depth, then an ascension, then one more transcendence per tier | more affixes, +4 LE, more materials, rarer codex entries | | steps |
| Forge (per slot) | gold | 5 × 1.8^level | +1.2% to the slot's flat stats a level | ascension | depth (0.018 LE a depth) |
| Ranks (pool) | materials (salvage) | 20 × (rank+1)^2.2 | more damage or health per rank | never | time^0.31 × gains |
| Mastery (pool) | time worn (focus ×5 on 4+ slots) | XP level^2 | more damage or health per level | never | time^0.33 × gains |
| Talents (3,259 nodes) | a point a level | levels: 12 × 1.25^level XP | percents, notables' more, keystones' trade-offs | never | depth, until full |
| Paragon | points after the tree | | more damage or health per point | never | depth |
| Codex (114,432 entries) | first finds | | more for each slot × rarity × quality band × era found, rarer worth more | never | log(depth), rarity |
| Ascension (40 + 6 nodes) | aether (kills past depth 150) | (rank+1)^3 | more, gold, XP, item find, quantity, mastery and material gains, head start | transcendence | aether^0.25 |
| Transcendence (27 nodes) | stardust (from a cycle's aether) | stats (rank+1)^1.4, gains (rank+1)^3 | big more, aether, mastery and codex gains, echo, head start | | stardust^0.42 |

## Stability

Two feedback loops could run away; the framework keeps both well below one (`Growth.cs` computes them from the
parameters, and the check fails above 0.6 and 0.7).

**Depth feeds back on itself.** Some systems grow with depth by themselves: gold per kill grows R^(0.9 (d−1)) and a
forge level costs ×1.8, so forge levels follow depth at 0.9 ln R / ln 1.8 a depth, each worth ln(1.012) / ln R LE:
σ_forge = 0.9 × ln 1.012 / ln 1.8 = 0.018 LE a depth. Levels follow depth too (XP R^(0.8 (d−1)) against ×1.25 a level:
0.34 levels a depth), and each is a talent point or a paragon rank (σ ≈ 0.012). With C = C_time + σ d,

    d* = C_time / (p − σ),        loop gain G = σ / p.

At G ≥ 1 every depth pays for more than the next and depth runs away (an early version, with p = 0.10 under the sum of
slopes, reached depth 900 in two hours); below 1 the time-driven systems set depth, amplified by 1 / (1 − G). Now
G = (0.018 + 0.012) / 0.115 = 0.27.

**Prestige currency feeds back on itself.** Aether per kill grows as (d / 150)^φ, φ = 1.25; the ascension tree's
ranks cost (k + 1)^3, so spending T aether buys about T^(1/4) ranks: power ∝ T^β, β = 0.25. Depth follows power, so
dT/dt ∝ d^φ ∝ T^(φβ). Stardust multiplies aether too (Aether Flow), by stardust^(1/4) with stardust ∝ T^0.6: another
T^μ, μ = 0.15. So

    dT/dt ∝ T^F,   F = φβ + μ = 0.46        and  T ∝ t^(1/(1−F)).

At F ≥ 1 aether grows exponentially (an early version, with aether ∝ depth^2.5, grew depth faster than time); below 1
it grows as a power of time.

## Growth exponents

If depth grows like t^α, aether grows like t^τ with τ = (1 + φα) / (1 − μ), and each open-ended system's power grows
as a power of time:

| System | Exponent | Why |
|---|---|---|
| Ascension | β τ | ranks ∝ aether^β |
| Transcendence | 0.6 τ / (1.4 + 1) | stardust ∝ cycle aether^0.6, stat ranks ∝ stardust^(1/2.4) |
| Mastery | (1 + max(β, μ) τ) / (2 + 1) | XP is time × a gain the trees multiply; levels ∝ XP^(1/3) |
| Ranks | (1 + β τ) / (2.2 + 1) | materials are counted per item × a gain the ascension tree multiplies |

Depth grows like the fastest of them, so α is the fixed point α = max(...)(α): **α ≈ 0.49**. The exponents are
deliberately close together (0.46 to 0.49), so no system's share runs away from the others over thousands of hours.
The check compares the prediction with the simulation; the measured exponent from 300 to 3,000 hours is within 0.15.

## Prestige

**Ascension** resets the run (depth, gear, gold, forge) for aether: every kill at or past depth 150 earns
0.01 × (depth / 150)^1.25 aether, banked until the next ascension. The tree has five tiers of eight families (Might,
Vigor, Golden Path, Scholar, Seeker, Bounty, Discipline, Salvager), cheap Awakened Edge and Heart nodes that make the
first ascension pay at once, Head Start (begin a run at up to half the deepest depth), Forgemaster, Second Focus and
Forge Memory.

**Transcendence** wipes the ascension tree for stardust = 5 × (cycle aether / 50,000)^0.6. The new cycle starts with
40% of the last cycle's aether (more with Aether Echo, up to 70%), which rebuilds about 80% of the tree at once: a
setback, not a wipe, so droughts stay short. Its tree: Starforged Might and Vigor (the strongest multipliers in the
game), Aether Flow, Eternal Discipline, Resonance, Stellar Fortune, Celestial Slots (the sixteen celestial slots),
Aether Echo and Starlit Path. The late rarity tiers (Celestial to Transcendent) are milestones: a depth and one more
transcendence each. The first transcendence brings the **autopilot**: ascensions by the player's own rule while they're
away, so idle play keeps up.

## The simulated player

The policy (`Sim.cs`) stands in for a sensible player:

- **Weights**: what a unit of each stat is worth now, by finite differences of its objective (half how deep it can
  push, half how deep it farms comfortably). Utility stats (gold, items, XP, materials, quantity) are worth 30% of an
  equal multiplier on power.
- **Gear**: equips strict upgrades, salvages the rest, notes first finds. Drops are sampled in aggregate (a Poisson
  count per slot and rarity, the best quality of n drops in one draw), so a 3,000-hour run takes seconds.
- **Forge**: water-filling. Costs grow ×1.8 a level and value only ×1.012, so the best buys fill every slot up to a
  common level; a bisection finds it for the gold on hand.
- **Ranks**: best value per material (in practice the cheapest, newest slots first). **Focus**: the least-mastered
  worn slots. **Talents**: the best value per point among paths up to eight nodes long, so notables and keystones get
  walked to; paragon once the tree is full.
- **Ascension**: not while the run is still climbing (less than 5% deeper over its last quarter), then when what the
  aether buys takes the hero deeper by enough for how long they've waited: 5% for a half-hour run, 5%/n for one n
  times as long.
- **Transcendence**: when it would add a quarter to all the stardust ever earned and what the stardust buys is worth
  at least what wiping the ascension tree costs, less what the echo buys back at once: a transcendence never leaves
  the hero weaker.
- **Prestige trees**: greedy by value per cost, saving for a one-off that's the best buy but out of reach.

## The acceptance test

`progsim --check` runs the four players for 3,000 hours and checks (`Checks.cs`):

- **Stability**: loop gain ≤ 0.6, prestige feedback ≤ 0.7, predicted exponent 0.3 to 0.7.
- **Growth**: the measured exponent from 300 to 3,000 hours is 0.3 to 0.75 and within 0.15 of the prediction; the last
  third doesn't run faster than the prediction + 0.15 (no runaway); a new record in every window (no wall); depth
  within ranges at 1, 10, 100 and 1,000 hours.
- **Prestige pays**: twice the depth of never prestiging by the end, and never behind it from 20 hours on.
- **Idle keeps up**: the four-hourly player at 70% of the active one from 100 hours, the daily one at 50%.
- **Every system matters**: gear 1%, codex 2%, and forge, ranks, mastery, talents and paragon, ascension and
  transcendence each 3% to 40% of power at the end.
- **Progress keeps showing**: a noticeable gain (reach up 0.1 depth, about 1% more power) at least every two hours; no
  record drought longer than a quarter of the time played.
- **Rhythm**: median ascension runs between 15 minutes and the longer of a day and 2% of the time played; the first
  transcendence within 300 hours; transcendence cycles under half the time played.
- **Content lasts**: the talent tree full, but not before 100 hours; every depth slot open and rarity at Divine or
  better by 3,000 hours.
- **Sanity**: numbers finite, records never go back.

## Results

(Filled in from the final runs.)

## Extending it

Add content through the budgets, then run `--check` (and `--tune` if a system's share moves):

- **Slots**: add to `Content.BuildSlotList` with a kind (offense share, weight, affix pool come from `KindRule`) and
  place it in the unlock order. Ranks, mastery and the codex are budgeted per `Params.ReferenceSlots`, so more slots
  spread their power rather than multiply it. Classic slots weigh `ClassicWeight` times more.
- **Affixes**: add a named variant to an `AffixDef`, or a new stat to a slot kind's pool. New percent stats need a
  bucket in `Combat.Prepare` or `At`; never add a new "more" source outside the budgeted systems.
- **Rarities**: a tier needs a name, a depth and a milestone in `Content`; it adds +4 LE and an affix, which the
  bounded gear share can absorb.
- **Talents**: more rings or sectors spread `TalentBudgetLE` over more nodes; keystones are trade-offs (a big more
  on one side, a cost on the other) and aren't budgeted, so keep their net small.
- **A prestige layer**: give it a currency that grows as a root of the layer below's total, a tree with polynomial
  costs, and check that its feedback on the currency below keeps `PrestigeFeedback` under 0.7.
- **Anything that grows with depth by itself** (gold sinks, level rewards) adds to the loop gain: keep the total under
  0.6 of the premium.

## Not yet

- The game (`scripts/Core`) doesn't use the framework yet; the battle there is tick-by-tick, this one closed-form.
- The simulated player is a heuristic, not an optimum; a min-maxer would go somewhat faster.
- One hero; no challenges or third layer (the rhythm checks say where one would be wanted: late transcendence cycles
  run to hundreds of hours).
