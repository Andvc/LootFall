# LootFall design notes

## The game in one paragraph

A knight fights his way through six places (Greenwood, the Sunscar Dunes, the Sapphire Coast, the Haunted Ruins,
Frostpeak and the Dragon's Roost), ten stages each, a boss at the end of every stage and a guardian at the end of
every place. He fights on his own; what you do is open the chests the monsters drop. Every chest holds one piece of
gear for one of his eight slots, in one of six rarities, and the chest makes a show of telling you which: it hops,
glows, and climbs through the rarity colours one step at a time until it bursts. A card shows the item against what
he wears; equip it and it flies onto him, his blade or cape changes colour, the power number jumps, and the next
monster dies faster. Sell it and it's gold, which buys a better chest level (stronger, rarer loot), training, and
later a helper that opens chests for you. Stronger gear clears stages faster, later stages drop more gold, and every
kill can drop another chest: open, equip, see it land, earn more.

## Why this loop (and what the old one taught)

The previous prototype (`prototype/chest-loop`) was about deducing what's inside a chest from its sounds before buying
it. The deduction was the work and the opening the reward, and the reward came only after the work went well. This one
keeps the opening at the centre and cuts the distance to its payoff to nothing: every chest is yours, every opening is
a roll you watch happen, and the result has an immediate, visible consequence in a fight that's always running. The
decisions are small and frequent (equip or sell, what to buy next, when to fight the boss); the satisfaction is in the
reveal and in watching numbers climb.

Three rules hold it together:

1. **Everything you get shows up in the fight.** Attack, health, crits and speed change the numbers and the pace of
   kills at once; legendary powers have their own effects (lightning, burning, meteors, barriers); the knight's blade,
   slash and shield face take his weapon's rarity colour and his cape and plume his armour's (`Shaders.cs`).
2. **Every chest pays out.** An item that isn't an upgrade sells for gold, and gold always has somewhere to go, so
   there is no inventory to manage and no dead opening.
3. **The fight feeds the chests.** Kills drop chests (and gold), bosses drop better chests, and gear can carry Chest
   Find, Gold Find and Luck, so loot can make loot.

## Chests

| Kind | From | Floor | Item level | Look |
|---|---|---|---|---|
| Wooden | 42% of kills (about 93% of drops) | Common | chest level's | brown with iron bands |
| Iron | 6% of drops; every stage boss | Uncommon | +1 | steel blue |
| Golden | 0.8% of drops; every zone guardian (and two Iron) | Rare | +2 | gold |
| Royal | the wandering mimic (1% of waves from stage 3, with a Golden) | Epic | +3 | purple |

The pile is opened best kind first. A new game starts with two wooden chests and its first three kills each drop one,
so the first thing a player does is open a chest.

**Rarity** (`Chests.Odds`): each rarity has a base weight (1000, 300, 60, 10, 1.2, 0.1) and each step up is multiplied
by e^(0.055 per chest level above 1, plus the kind's shift: 0, 0.3, 0.5, 0.7); nothing comes out below the kind's
floor. Then **Luck** (a gear stat) gives that chance of one step more, and the opening shows it ("LUCKY!").

| Chest | Common | Uncommon | Rare | Epic | Legendary | Mythic |
|---|---|---|---|---|---|---|
| Wooden lv 1 | 72.9% | 21.9% | 4.4% | 0.73% | 0.09% | 0.01% |
| Wooden lv 10 | 58.6% | 28.8% | 9.5% | 2.6% | 0.51% | 0.07% |
| Wooden lv 20 | 37.5% | 32.0% | 18.2% | 8.6% | 2.9% | 0.70% |
| Wooden lv 30 | 16.3% | 24.1% | 23.8% | 19.5% | 11.5% | 4.7% |
| Iron lv 1 | | 74.5% | 20.1% | 4.5% | 0.73% | 0.08% |
| Golden lv 1 | | | 74.8% | 20.6% | 4.1% | 0.56% |
| Royal lv 1 | | | | 78.0% | 18.8% | 3.2% |

**Item level** comes from the **chest level**, not the stage: 1 + 2 per chest level above 1, +1 per zone reached, plus
the kind's bonus, and a drop rolls +0/+1/+2 levels (60/30/10%). Each level is ×1.13 on main stats, so each chest level
makes new loot about 28% stronger. This was the most important balance decision (see Pacing): when items followed the
furthest stage, every stage cleared made the next chests better, gold followed the stage too, and a sensible player
reached stage 60 in 25 minutes and maxed everything within the hour. Tying item level to an upgrade bought with gold
turns progress into a staircase: farm, level the chest, a burst of upgrades, push a few stages, farm again.

## Gear

Eight slots, one item each, no inventory: equipping sells what it replaces.

| Slot | Main stats (common, item level 1) | Affix pool leans to |
|---|---|---|
| Weapon | ATK 6 | ATK%, crit, crit damage, speed, lifesteal, double strike, stun |
| Helmet | HP 18, DEF 1.2 | HP%, regen, crit, dodge, luck, chest find |
| Armor | HP 30, DEF 2 | HP%, regen, dodge, lifesteal, gold find |
| Shield | DEF 3, HP 10 | HP%, dodge, stun, regen, lifesteal |
| Gloves | ATK 2.5 | ATK%, crit, crit damage, speed, double strike, gold find |
| Boots | HP 14, DEF 0.8 | speed, dodge, HP%, chest find, luck |
| Ring | ATK 3 | crit, crit damage, ATK%, lifesteal, luck, gold find |
| Amulet | ATK 1.5, HP 12 | HP%, ATK%, regen, luck, chest find, gold find |

| Rarity | Main stats | Affixes | Affix size | Sells for | Also |
|---|---|---|---|---|---|
| Common | ×1 | 0 | | ×1 | |
| Uncommon | ×1.25 | 1 | ×1 | ×1.5 | |
| Rare | ×1.55 | 2 | ×1.34 | ×2.5 | |
| Epic | ×1.95 | 3 | ×1.68 | ×4 | |
| Legendary | ×2.45 | 3 | ×2.02 | ×7 | a legendary power |
| Mythic | ×3.1 | 4 | ×2.36 | ×12 | its power at 1.5×, a shifting colour |

The multipliers are flatter than they first were (up to ×4.3): with steep rarity, the gear a player wore was the best of
hundreds of drops and almost nothing new could beat it (5% upgrades an hour in). With these, two or three chest levels
let a fresh epic beat an old legendary, and upgrades keep coming (see Pacing).

**Legendary powers** (`Effects`), one per legendary or mythic item, each with its own effect in the fight:
Thunder (18% on hit: lightning for 150% ATK), Ember (hits burn for 40% more over 2s), Cleave (blows hit every other foe
for 60%), Meteor (every 6th attack: 250% ATK to all), Holy Light (every 8s: heal 20%), Thorns (attackers take 40% of
what they deal), Bulwark (every 10s: a barrier of 25% max HP), Vampire (+8% lifesteal), Midas (+60% gold), Prospector
(+25% chest find), Fortune (+5% luck), Swiftness (+20% attack speed, first blow of each wave instant), Executioner
(foes below 20% die at once), Second Wind (once a fight below 30%: heal 50%).

**Power** (`HeroStats.Power`) is one number for comparing: the geometric mean of damage per second (attack, speed,
crits, double strikes, offensive powers) and effective health against the furthest stage's monsters (health,
defence, dodge, lifesteal, regeneration, defensive powers). The card recommends equipping whatever raises it; loot
stats (gold, chests, luck) are shown in gold and left to the player's judgement.

**Names** come from the slot and rarity ("Steel Dagger", "Golden Cuirass"), the strongest affix ("Keen", "Twin",
"Prospector's") or the legendary power ("Moon Ring of Storms").

## The battle

The knight stands on the left; monsters walk in from the right in waves of one to three (smaller waves early; the big
ones take more room), one after another. Each has its own attack speed; the knight hits the one in front. The battle
runs in Core (`Battle.cs`) in steps of 1/30 s and reports events (`WaveSpawned`, `AttackStarted`, `HitLanded`...) that
the screen animates and the game state pays out on; the simulation runs the same code without a screen.

- **Stages**: ten kills, then the stage's boss: a big one of the zone's monsters (3× size for small ones), or on the
  tenth stage the zone's guardian (Old Tusk, the Sand Warlord, the Deep One, the Mimic King, the Frost Giant,
  Vermithrax). A boss has 16× (guardian 22×) a monster's health and 1.6× (1.8×) its attack, and the knight has 30
  seconds, so bosses are mostly a damage check that survival also matters for.
- **Auto-Boss** (on by default) fights the boss as soon as it's ready; after a loss it waits until the knight is 4%
  stronger (at least 8 s) or 90 s have passed. The boss button does it by hand (B).
- **Falling**: the knight gets up after two seconds and the wave starts over. Falling twice at a stage without clearing
  a wave steps him back a stage; he climbs again after ten kills once he's stronger.
- **Monsters** hit for much less than their health would suggest (attack 2.5 against health 10 at stage 1, both ×1.18 a
  stage), so farming a stage you can't beat the boss of stays safe and quick: early versions had the knight dying
  once a minute at every wall.
- **The mimic**: one wave in a hundred from stage 3 is a lone mimic; it drops a Royal and a Golden chest.
- Past the sixth zone the places come round again, darker and harder ("Greenwood II").

## Gold and upgrades

Gold comes from kills (1.5 × 1.089^(stage−1), bosses 8×, guardians 20×, the mimic 15×) and from selling (0.6 × 1.13^level
× the rarity's multiple). It grows more slowly per stage than monsters do (1.089 against 1.18), so pushing ahead earns
more but can't snowball.

| Upgrade | Does | Costs |
|---|---|---|
| Chest Level | +2 item levels, rarer odds (up to 45) | 60 for the first, then 145 × 1.35^(level−1) |
| Might | +5% attack per level | 10 × 1.16^level |
| Vigor | +5% health per level | 10 × 1.16^level |
| Haste | +2% attack speed per level (up to 30) | 40 × 1.35^level |
| Auto-Open | from stage 10: opens a chest every 0.8 s, equips upgrades, sells the rest, and stops for anything Epic or better | 2,500 |

Auto-Open is there for when chests pile up (and for coming back to a pile), but it hands the big moments back to the
player: anything epic or better waits on the counter and gets the full show.

**Time away** (`ApplyOffline`): on Continue, a 90-second trial fight at the current stage measures the kill rate, and
the time away (up to 8 hours) pays out at three quarters of it, in gold and chests, which wait to be opened.

## Pacing

Targets for a sensible player (`Policy`: opens every chest at once, wears whatever adds power, buys the next chest
level whenever it can and training while it costs under 30% of that), and what the simulation gives (12 runs of 5
hours, `dotnet tools/simcli/bin/Release/net8.0/simcli.dll --n=12 --hours=5`):

| Stage | Target | Median | 10th–90th |
|---|---|---|---|
| 10 (Greenwood's guardian) | 5 min | 3.6 min | 3.5–4.0 |
| 20 | 13 min | 10.0 min | 8.9–12.8 |
| 30 | 28 min | 28.4 min | 20.3–32.0 |
| 40 | 50 min | 50.9 min | 36.8–60.0 |
| 50 | 80 min | 70.0 min | 56.9–88.2 |
| 60 (Vermithrax) | 120 min | 103.6 min | 88.0–113.7 |
| 80 | 220 min | 178.8 min | 174.0–189.8 |

Real players read cards and miss things, so they'll be slower than this; the first zone is meant to go quickly.
The tuner (`--tune`) searches chest level cost, its growth, gold growth and training growth for these targets.

| | 0–2 min | 2–5 | 5–10 | 10–20 | 20–40 | 40–60 | 60–90 | 90–120 | 2–3 h | 3–4 h |
|---|---|---|---|---|---|---|---|---|---|---|
| Chests a minute | 17 | 16 | 11 | 9.4 | 9.2 | 9.9 | 10.6 | 10.3 | 12.3 | 11.0 |
| Upgrades | 52% | 23% | 20% | 14% | 11% | 7% | 6% | 5% | 4.5% | 4% |

First finds (median): rare at half a minute, epic 2.5 min, legendary 11 min (2 to 21), mythic 36 min (15 to 64).
The longest stay on one stage is about half an hour, past the sixth zone; in the first six zones walls last minutes.

## Feel

- **The opening**: a hop and a creak, a white glow, then one step per rarity: the glow takes the next colour, a ting
  a little higher each time, the chest shakes harder, sparks; rumbles from rare, lightning from epic. Luck adds a step
  with "LUCKY!". Then the latch, the burst, a ring and a flash in the rarity's colour, a pillar of light from rare,
  confetti and a fanfare from legendary, hit-stop for the biggest. Commons take half a second; clicking (or Space)
  hurries any opening.
- **The card**: rarity band, level, name, the picture in its rarity's frame, each main stat with its change in green
  or red, affixes, the legendary power, and POWER +N in the colour that says whether to take it. The recommended
  button pulses and Space takes it; E and S choose.
- **Equip**: the picture flies to its slot, the slot flashes, the knight flashes and "+N POWER" rises over him,
  coins from the old item fly to the gold counter. **Sell**: coins burst from the card and fly up.
- **The fight**: swings timed so the slash lands with the blow, numbers that pop and drift (yellow crits with "!",
  hit-stop and shake; blue thunder; orange burns; red when the knight is hit), flinches and knockback, a puff and a
  scatter of coins on a kill, chests that pop out of monsters and fall into the pile below ("lootfall").
- **Moments**: banners for bosses, stage and zone clears, the mimic; a fanfare and a ring when the chest levels up.

## Not yet

- More than one hero; a prestige layer (a reset for a permanent bonus) once the endless cycles are the norm.
- Sets, item enhancement or a forge for what's sold; collections ("found every legendary").
- Achievements and a stats page; Steam integration.
- Sound has been chosen by name and checked by analysis only, not by ear.
