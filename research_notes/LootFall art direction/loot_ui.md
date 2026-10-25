# Loot UI at scale: gear screens, item icons, rarity, tooltips, giant trees, codexes and idle numbers (survey for LootFall)

Sourcing note (researched 2026-10-25). Only GitHub resolves from this environment, so pages on other sites could not be
opened. Claims from them rest on search-engine result summaries and are marked "(summary)". Treat them as weaker
evidence; where the summaries disagreed, the conflict is noted. GitHub-hosted primary sources (game data exports, UI
source code, filter files, libraries) were read directly and are marked "(read)", with their commit dates. The web-search
budget, shared with other agents, ran out after about two dozen queries, so several games named in the brief could not be
checked. Each section's Gaps lists them. LootFall facts come from the repository (`CLAUDE.md`,
`scripts/Progression/Content.cs`) and the assignment brief.

## 1. Equipment screens with many slots: layouts, slot types, surfacing upgrades and comparisons

### Takeaway
None of the games checked puts more than about 20 equipment slots on one paper doll. WoW's 18 slots in two columns
plus a weapon row is the canonical form. The only interfaces found that hold hundreds to thousands of "slots" are
collection UIs (category tabs plus grids, hiding what isn't reachable yet). Upgrades are surfaced the same way
everywhere: a small marker on the icon (WoW's green arrow), a pulse for new items, and a gained/lost comparison on demand.
For 149 slots, LootFall will need a grouped, progressively revealed screen, not a single doll.

### Cited Findings
- WoW's character pane (live UI source, build 12.1.5 (70575), committed 2026-10-23) is a paper doll of 18 slot buttons:
  a left column of 8 (Head, Neck, Shoulder, Back, Chest, Shirt, Tabard, Wrist), a right column of 8 (Hands, Waist, Legs,
  Feet, Finger0, Finger1, Trinket0, Trinket1), and 2 along the bottom (MainHand, SecondaryHand). The column templates are
  `PaperDollItemSlotButtonLeft/Right/BottomTemplate`
  (`Interface/AddOns/Blizzard_UIPanels_Game/Mainline/PaperDollFrame.xml`) — [Gethe/wow-ui-source](https://github.com/Gethe/wow-ui-source) (read)
- WoW item buttons are 37x37 px; a 64x64 size also appears in the template. Quality is shown by tinting one shared white
  frame texture (`Interface\Common\WhiteIconFrame`) with the quality colour through `IconBorder:SetVertexColor`, so the
  icon art itself is never recoloured. Artifact relics get a different frame shape (`Interface\Artifacts\RelicIconFrame`)
  (`Blizzard_ItemButton/Shared/ItemButtonTemplate.xml`, `Mainline/ItemButtonTemplate.lua`) — [Gethe/wow-ui-source](https://github.com/Gethe/wow-ui-source) (read)
- WoW bag slots mark upgrades with a green-arrow overlay (`UpgradeIcon`, atlas `bags-greenarrow`). They mark new items
  with a pulsing `NewItemTexture` (alpha 1 to 0.2 to 1, in 0.5 s steps) and a 0.6 s fade-out flash (`bags-glow-flash`).
  Empty slots use a slot-background atlas named `bags-item-slot64`
  (`Blizzard_UIPanels_Game/Mainline/ContainerFrame.xml`) — [Gethe/wow-ui-source](https://github.com/Gethe/wow-ui-source) (read)
- Diablo IV's "Advanced Tooltip Compare" (Gameplay options) shows every effect you would gain (green text) and lose (red
  text) against the equipped item — [Den of Geek](https://www.denofgeek.com/games/diablo-4-best-hidden-setting-compare-gear) (summary).
  Holding Shift with an unequipped item selected shows the comparison — [Windows Central](https://windowscentral.com/gaming/the-one-diablo-4-tip-i-wish-i-knew-before-i-started-playing) (summary)
- Diablo IV restricts aspect categories to slot types: Offensive on amulets, weapons, gloves and rings; Defensive on
  shields, helms, chests, pants and amulets; Mobility on amulets and boots; Resource on rings. Other tables disagree
  (e.g. helmets Defensive or Utility, amulets any type), probably because of patch changes — [Icy Veins](https://www.icy-veins.com/d4/guides/legendary-aspects-codex-of-power-guide/), [Mobalytics](https://mobalytics.gg/blog/diablo-4/aspects-codex-of-power-explained/) (summary)
- Path of Building, the community planner for Path of Exile, makes "how much of an upgrade" a first-class number. Its
  item planner is "fully integrated with the offence/defence calculations; see exactly how much of an upgrade a given
  item is!" and it can "search the trade site for the most impactful items" — [PathOfBuilding README](https://github.com/PathOfBuildingCommunity/PathOfBuilding) (read)
- Legends of Idleon shows per-entry progression as star ranks. Each card ranks from 0 to 6 stars by collecting copies
  of itself. One card's bonus goes from +18 at 0 stars to +126 at 6 stars, and the copies needed go from 1 to 73,225 — [DigitalTQ Idleon wiki](https://www.digitaltq.com/wiki/idleon/items/cards/sacrilegious-kattlekruk-card-1782) (summary)
- The large "slot" interfaces found are collections. The Old School RuneScape collection log has 1,925 slots (1,717
  unique entries) — [OSRS Wiki](https://oldschool.runescape.wiki/w/Collection_log) (summary). Path of Exile's Unique
  Collection Tab holds one of each unique (capacity 1,483 in 3.25) and gives uniques that aren't regularly obtainable no
  visible slot until one is acquired — [GGG forum list](https://web.poecdn.com/forum/view-thread/1835899), [Bex_GGG announcement](https://devtrackers.gg/pathofexile/p/3de9e1f6-introducing-the-unique-collection-tab) (summary)
- LootFall's content opens its 149 slots in stages: "the eight classics first [Main Hand, Breastplate, Helm, Left
  Gauntlet, Left Boot, Left Index Ring, Amulet, Off Hand], then the kinds interleaved so every part of the body fills
  gradually; celestial slots last (prestige nodes open those)". Slot kinds are Weapon, Armor, Jewelry, Accessory, Trinket,
  Tattoo, Companion and Celestial — [scripts/Progression/Content.cs](/home/user/lootfall/scripts/Progression/Content.cs) (read)
- LootFall's UI constraints: absolute positions on a 1280x720 canvas; the battle lane takes y 0 to 420 and the gear,
  chest and upgrade panels sit below; Kenney Mini font at multiples of 8 px (min 16), ASCII only; 9-slice frames — [CLAUDE.md](/home/user/lootfall/CLAUDE.md) (read)

### Inferences
- The paper doll stops scaling at about 20 slots: WoW's two columns of 8 already fill the height of its pane. 149 slots
  need two levels. (1) An overview of the 8 slot kinds, each tile showing slots open/total, best rarity, and a count of
  available upgrades (an aggregated WoW green arrow). (2) A grid page per kind, using body-region sub-groups where the
  count is large: a hand cluster for the 10 rings, a foot cluster for the 10 toe rings, ear and neck clusters for the
  35 jewellery slots.
- Space arithmetic: the panel strip below the lane is 1280x300. At 48 px icons on a 52 px pitch, that holds about 24x5 =
  120 cells, which is fewer than 149 even before labels. A dedicated full-screen armoury (1280x720 at a 56 px pitch is about
  22x12 = 264 cells) can show every open slot grouped by kind. The battle should shrink or pause behind it.
- Progressive reveal is the strongest clarity tool, and LootFall already has it in the data (8 classics first, celestial
  last). Draw only open slots, plus silhouettes of the next few to open as goals. Following the Unique Collection Tab, don't
  draw 141 locked boxes on day one.
- An auto-battler should make "equip all upgrades" one press, and should state each slot's upgrade as one power number
  (Path of Building's "how much of an upgrade"), with gained/lost stat lines on demand (Diablo IV's compare).
- Per-slot forge, ranks and mastery can live on the slot frame as pips or stars (Idleon's 0 to 6 stars), so the grid
  shows depth without opening a tooltip.

### Gaps
- Equipment screens of Path of Exile 1/2, Diablo IV, Last Epoch, Grim Dawn, Titan Quest, Lost Ark, Torchlight Infinite,
  Melvor Idle, NGU Idle and Legend of Mushroom could not be checked: their sites don't load here, and the search budget
  ran out before those queries. A Legend of Mushroom search returned only WoW add-ons.
- Unverified leads from background knowledge that are worth checking: Legend of Mushroom's draw, compare, then
  equip-or-sell loop with auto-draw rules (closest to LootFall's chest loop); Melvor Idle's switchable equipment sets;
  NGU Idle's loadouts and merges; Lost Ark's separate panels for accessories, gems, cards and engravings.
- No source was found on how any game lays out more than about 30 equipped slots. The Game UI Database could not be
  reached.

## 2. Item icon conventions: sizes, framing, rarity borders and backgrounds, markers, lighting and perspective

### Takeaway
The verified pattern keeps rarity out of the icon art: one piece of art per item, a rarity-tinted frame or background
layer, and small overlays for state (new, upgrade, tier stars, affix dots). Commercial packs ship 64 to 512 px masters and
sell rarity borders as a separate layer. That suits AI generation: generate one consistent base icon per item type and
apply all 12 rarities programmatically.

### Cited Findings
- WoW draws item icons in 37x37 px buttons. Quality is a tinted overlay frame (`WhiteIconFrame` plus a vertex colour);
  frame shape changes only for a different item class (relics). The new-item pulse and the upgrade arrow are separate
  overlay textures — [Gethe/wow-ui-source](https://github.com/Gethe/wow-ui-source) (read)
- Asset-store icon packs, which are observed practice rather than standards:
  - Packs ship the same icons at 64, 128, 256 and 512 px, some from 32 px up.
  - One pack signals rarity with a subtle inner glow on each tile (grey, green, blue, purple, gold).
  - One sells "57 item borders" at 250x250 as a separate layer for its icons.
  - One carries tiers through material, form and stone size "so quality reads before the item does".
  - One keeps the same factions in every rarity tier so a loadout looks like one set.
  - Sellers stress silhouettes that still read at 64 px, and transparent PNGs are the norm.
  - [Fab listing](https://www.fab.com/listings/cc12e99f-03e7-48db-95af-279bcd39bdb9), [UIUX Forge pack](https://uiuxforge.itch.io/bold-icons-pack), [ZephireDev RPG icons](https://zephiredev.itch.io/101-rpg-icons-assets), [OpenGameArt Radakan icons](https://opengameart.org/content/radakan-icons-inventory) (summary; the summary didn't tie each claim to a specific listing)
- Path of Exile's passive tree sizes its icons by importance. In the 0.5-zoom sprite sheets, small passive icons are
  34 px, notables 49 px, keystones 68x69 px and masteries 113 px. The frames around them are 51, 76 and 109x111 px
  (details in section 5) — [grindinggear/skilltree-export data.json, 3.29.1](https://github.com/grindinggear/skilltree-export) (read)
- Diablo IV marks items with a Greater Affix with a star. One source says nothing shows when an item has two or more,
  and players have asked for extra stars — [Icy Veins](https://wp-prod.icy-veins.com/diablo-4-still-missing-one-simple-gearing-feature-and-its-driving-players-nuts/) (summary).
  Another says the ground label shows "I, II or III" next to the name — [PureDiablo](https://www.purediablo.com/diablo4/Items) (summary).
  This is a conflict, possibly a change between patches.
- Last Epoch shows affix-count dots on dropped items, but players say items in the inventory are harder to tell apart
  by background colour alone — [Last Epoch forum: Colorblind difficulties](https://forum.lastepoch.com/t/colorblind-difficulties/38732) (summary)
- LootFall's art pipeline: CC0 pixel-art packs imported by `tools/import_art.py`; the battle at 2x; Kenney Mini text at
  multiples of 8 px; panels and buttons from 9-slice frames — [CLAUDE.md](/home/user/lootfall/CLAUDE.md) (read).
  Keywright, the owner's sister project, already fixed an icon house style that LootFall's AI icons could copy:
  "materials with a shadow/mid/light ramp, lit from the top left, a 1px dark outline; things 24px", integer scales only — [Keywright CLAUDE.md](/home/user/keywright/CLAUDE.md) (read)

### Inferences
- Layer the icon. (a) Base art, one per base item: 149 slots, with variants per depth era if wanted. (b) Rarity frame
  plus background tint, 12 variants drawn by code or 9-slice. (c) Overlays: an upgrade arrow, a "new" pulse, a first-find
  badge, affix-count dots, rank stars, an item-level or tier number. WoW and the asset stores both do this, and it means AI
  generation never has to produce 12 rarity versions of each item. That avoids the hardest consistency problem.
- Size for LootFall's pixel grid: 24x24 or 32x32 art shown at 2x gives 48 or 64 px on the 1280x720 canvas. That sits in
  the 64 px "still reads" band the packs aim for. Generate masters large (256 to 512 px) with a fixed light direction (top
  left), a fixed three-quarter view and a 1 px dark outline, then reduce to the grid with palette quantisation. Check every
  icon at 1x, where silhouette is all that survives.
- If the art escalates with rarity at all, it should escalate in material and ornament (iron to gilt to gem-set, as one
  pack does), never in hue. Hue belongs to the frame so the code stays unambiguous.
- Markers should be sprites, not glyphs: the font is ASCII-only, so stars and arrows must be drawn. Tiers can be written
  as digits or roman numerals.

### Gaps
- Exact on-screen icon sizes for Path of Exile, Diablo II/III/IV, Grim Dawn and Titan Quest inventories, and the studios'
  icon style rules (lighting, perspective, palette) were not found. The search budget ran out, and GGG's and Blizzard's
  item art isn't on GitHub.
- No research was found on the minimum pixel size for recognising an icon or on how thick a border must be to read as a
  rarity cue.

## 3. Rarity colour language: standard colours, keeping 7+ tiers apart, per-tier effects, colour-blindness

### Takeaway
The shared vocabulary is five hues in a fixed order: white, green, blue, purple, orange (WoW, Fortnite and the Diablo
lineage, with exceptions). Games that go past seven tiers add red, pink or magenta, cyan, light gold and rainbow, but
neighbouring hues start being confused at around 10 (Terraria). The best-documented ways to separate more tiers are
extra channels on top of hue:
- background inversion and contrast
- beams and sound IDs
- icon size and shape (NeverSink's PoE filter)
- stars and pips
- the tier name in text
- user colour overrides (WoW)

### Cited Findings
- Origins: David Brevik traced Diablo's rarity colours partly to the roguelike Angband — [Game Informer, 2019-05-18](https://gameinformer.com/2019/05/18/the-surprising-origins-of-loot-rarity-colors) (summary).
  The code appeared earlier in BBS games and text adventures but reached a wide audience with Diablo II (June 2000).
  Dark Age of Camelot instead coloured items relative to the player's level (grey far below, yellow optimal) — [Mein-MMO](https://mein-mmo.de/en/why-is-the-loot-always-white-green-blue-purple-and-gold,305450) (summary).
  MMOs had no common pattern until WoW, which was clearly modelled on Diablo. Guild Wars 2 used pink for Ascended — [MassivelyOP](https://massivelyop.com/?p=407107) (summary)
- WoW's quality colours: Poor #9d9d9d, Common #ffffff, Uncommon #1eff00, Rare #0070dd, Epic #a335ee, Legendary #ff8000,
  Artifact #e6cc80, Heirloom #00ccff. Heirloom moved from light gold to Blizzard blue in Legion, and the WoW Token
  shares that blue — [Warcraft Wiki: Quality](https://warcraft.wiki.gg/wiki/Quality), [API GetItemQualityColor](https://warcraft.wiki.gg/wiki/API_GetItemQualityColor) (summary).
  Quality also tracks stat count: poor and common have none, uncommon one or two, rare and up as many as four — [Warcraft Wiki: Item Quality](https://warcraft.wiki.gg/wiki/Item_Quality) (summary)
- WoW's live settings (12.1.5) offer three accessibility tools:
  - a "Use Colorblind Mode" checkbox (CVar `colorblindMode`)
  - a colour-blind filter (None, Protanopia, Deuteranopia, Tritanopia) with a strength slider (`colorblindWeaknessFactor`)
  - per-quality colour overrides for Poor through Heirloom, searchable under both "Item Quality" and "Rarity". Blizzard's
    comment in the code: "since the terms are a bit interchangeable players could search for either"
  - Sources: `Blizzard_SettingsDefinitions_Frame/Colorblind.lua`, `Mainline/ColorblindOverrides.lua`; `ITEM_QUALITY_COLORS` is
    rebuilt from `C_ColorOverrides` in `Blizzard_Colors/Mainline/ColorManager.lua` — [Gethe/wow-ui-source](https://github.com/Gethe/wow-ui-source) (read)
- Last Epoch: Common white, Magic blue, Rare yellow, Unique orange, Set green, Exalted purple, Legendary red — [Upcomer](https://upcomer.com/last-epoch-loot-rarity-colors-explained) (summary).
  Exalted, added in patch 0.7.9, shows purple on the ground and in the inventory. It needs at least one tier 6 or 7 affix
  (T6 drops from area level 55, T7 from 90), comes only from drops, and can't be crafted to T6/T7 — [Last Epoch forum: Introducing tier 6 and 7 item affixes](https://forum.lastepoch.com/t/introducing-tier-6-and-7-item-affixes/22279) (summary).
  Legendaries combine a Unique and an Exalted in the Eternity Cache — [Last Epoch community guide](https://forum.lastepoch.com/t/community-game-guide/26057/6) (summary; another guide describes them differently)
- Grim Dawn breaks the Diablo order: white common, yellow magical, green rare, blue epic, purple legendary, from its
  Titan Quest roots (in Diablo II yellow is rare and blue is magic) — [Crate forum: Items color and rarity](https://forums.crateentertainment.com/t/items-color-and-rarity-full-explanation/35245), [Crate forum: Why did Grim Dawn change the usual ARPG item colors?](https://forums.crateentertainment.com/t/why-did-grim-dawn-change-the-usual-arpg-item-colors/157478) (summary).
  The wiki calls magic gold/orange and lists light blue for relics and lavender for quest items and crafting materials — [Grim Dawn wiki archive](https://grimdawn-archive.fandom.com/wiki/Items) (summary)
- Borderlands goes past legendary orange:
  - E-tech is magenta — [Steam BL2 discussion](https://steamcommunity.com/app/49520/discussions/0/1319962514590785968) (summary)
  - Seraph is pink with red flavour text — [GamerGuides](https://gamerguides.com/borderlands-2/guide/extras/equipment/seraph) (summary), though another result calls it cyan
  - Pearlescent was recoloured cyan "to avoid confusion with the most common white weapons" — [Destructoid](https://destructoid.com/?p=78526) (summary; the summary was muddled about which game introduced it)
  - Randy Pitchford calls pearlescent rarer than legendary and says not every player will ever see one; Effervescent is
    the top tier — [Gameranx](https://gameranx.com/updates/id/556679/article/randy-pitchford-on-borderlands-4-rarity-not-all-players-will-ever-see-a-pearlescent/) (summary)
- Terraria, with many tiers, warns that "certain Rarity colors appear very similar to each other in-game and are easily
  confused, e.g. Green/Lime, Orange/Light Red" — [Terraria Wiki: Rarity](https://terraria.wiki.gg/wiki/Rarity) (summary)
- Colour-blind handling found:
  - Fortnite has three colour-blind modes and a strength slider, which matter because its loot runs grey, green, blue,
    purple, orange — [Accessible Games: Distinguish This From That](https://accessible.games/accessible-player-experiences/access-patterns/distinguish-this-from-that/) (summary)
  - Last Epoch players asked for the rarity name in the tooltip, since background colour alone is hard to read — [Last Epoch forum](https://forum.lastepoch.com/t/colorblind-difficulties/38732) (summary)
  - The card game Into the Rift marks rarity with a corner gem, and a colour-blind player said shape and position cues
    helped — [r/ColorBlind thread, 2025-01-04 (mirror)](https://pol1.lr.ggtyler.dev/r/ColorBlind/comments/1ht4b1c/how_do_these_look) (summary)
  - Factorio players proposed shape markers that can be switched off — [Factorio forum](https://forums.factorio.com/viewtopic.php?p=600798) (summary)
- NeverSink's Path of Exile filter (v8.20.2, 2026-10-15) separates value tiers inside one rarity hue. The unique
  colour, 175,96,37, stays constant while the treatment changes:
  - T1: unique-brown text and border on a white background, alert sound 6 at volume 300, red beam, largest red star on
    the minimap
  - T2: white text on unique-brown, sound 1, red beam, yellow star
  - T3 (boss uniques): brown text on dark 53,13,13, sound 3, blue beam, smallest blue star
  - Currency steps down the same way: T1 red text on white (red beam, sound 6, red star). T2 is white on orange-red
    240,90,35 (red beam). Then black text on orange-red, orange 249,150,25, ochre 213,159,0 and tan 210,178,135, with beams
    yellow, white, white, grey and icon sizes 1, 2, 2, 2
  - Hidden junk gets font size 18, a transparent border, a near-transparent background and no drop sound
  - Source: `NeverSink's filter - 1-REGULAR.filter` — [NeverSinkDev/NeverSink-Filter](https://github.com/NeverSinkDev/NeverSink-Filter) (read)
- Path of Exile's filter effects:
  - `PlayEffect <colour> [Temp]` draws a light beam, and Temp shows it only briefly
  - `MinimapIcon <size 0-2> <colour> <shape>` uses 0 for largest, with shapes such as Circle, Diamond, Triangle, Star
  - [poedb Item Filter](https://poedb.tw/tw/Item_Filter) (summary)
  - Counting only active (uncommented) lines in NeverSink's REGULAR file, the beam colours used are Blue (114 blocks, +20
    temporary), Red 91, Purple 79 (+3 temporary), Yellow 72, White 71, Grey 37 (+24 temporary), Pink 21, Orange 8 and
    Green 6. Red beams sit on the top tiers and grey or temporary beams on the lowest shown ones — [NeverSink-Filter](https://github.com/NeverSinkDev/NeverSink-Filter) (read)
- RuneLite, the OSRS client, colours ground items by value with a hue ladder and configurable thresholds: low at 20,000
  coins (#66B2FF), medium at 100,000 (#99FF99), high at 1,000,000 (#FF9600), insane at 10,000,000 (#FF66B2). Highlighted
  items are #AA00FF, hidden items grey and the default white (`GroundItemsConfig.java`, master 2026-10-24) — [runelite/runelite](https://github.com/runelite/runelite/blob/master/runelite-client/src/main/java/net/runelite/client/plugins/grounditems/GroundItemsConfig.java) (read)

### Inferences
- LootFall's first five rarity names match WoW's Common to Legendary, so their colours should too (white or light grey,
  #1eff00 green, #0070dd blue, #a335ee purple, #ff8000 orange). That is the vocabulary players arrive with.
  Grim Dawn shows that departing from it costs a forum thread of confusion.
- Precedented hues for tiers 6 to 12:
  - Mythic: red (Last Epoch's Legendary, NeverSink's top tier)
  - Ascended: pink or magenta (Guild Wars 2's Ascended, Borderlands' E-tech and Seraph)
  - Celestial: cyan or sky blue (Borderlands' pearlescent, WoW's Heirloom #00ccff)
  - Divine: light gold (WoW's Artifact #e6cc80)
- The last three cannot get new hues without breaking Terraria's rule, so they should differ in treatment, as
  NeverSink does:
  - Primordial: dark background with a coloured glow (inversion, like NeverSink's T3 uniques)
  - Eternal: white or silver background with coloured edges (NeverSink's T1 inversion)
  - Transcendent: animated prismatic cycle (Borderlands' Effervescent rainbow)
  - This mapping is a proposal and needs checking with a colour-vision-deficiency simulation.
- Every tier also needs a channel other than colour:
  - the name and a tier number (1 to 12, or I to XII) in the tooltip (the Last Epoch request)
  - pips or frame ornaments that grow with tier (Into the Rift, Factorio)
  - from tier 6, animation and particles
  - per-rarity colour overrides in Settings (WoW). LootFall's Settings already hold the Screen Shake scale for
    `Main` effects, so an accessibility page fits there.
- Separate "what it is" from "how much it matters now", as NeverSink and RuneLite do. Hue is fixed per rarity. Emphasis
  (beam, sound, size, pulse) goes to what matters to this player: an upgrade, a first find for the codex, a new tier. A
  Rare upgrade can then shout louder than a Divine the knight can't use.
- Effects in a chest opening should step up with tier, following NeverSink's sound ladder (distinct alert sounds per
  tier) and Borderlands' "not everyone will see one". A sketch:
  - tiers 1 to 3: card colour plus a soft sound
  - tiers 4 to 5: beam plus a distinct sound
  - tiers 6 to 8: flash and shake through `Main.Flash` and `Main.Shake`
  - tiers 9 to 12: a held reveal with its own sound, rare enough that seeing one is an event

### Gaps
- Destiny, Diablo III and Diablo IV rarity palettes and per-tier beams and sounds, gacha reveal conventions (e.g.
  Genshin's wish colour foreshadowing), and Lost Ark's and Torchlight Infinite's tiers were not verified (search budget
  exhausted). Unverified leads: Diablo III's grey, white, blue, yellow, orange and green (set), with Ancient/Primal
  borders; Destiny's white, green, blue, purple and gold (exotic); gacha "pre-reveal" colours that telegraph rarity
  before the card turns.
- No formal study of colour-blind discrimination between rarity palettes was found. The 12-colour proposal above has not
  been simulated.

## 4. Tooltips and comparisons: affixes, stat deltas, highlighting upgrades, loot filters and auto-salvage

### Takeaway
Comparisons that work have two layers: a one-glance verdict (a green arrow, or a single "how much of an upgrade" number)
and a detail layer on demand (Diablo IV's per-line gained/lost colours, Path of Exile's Alt view with prefix, suffix,
tier and range). Loot filters scale through presets and simple rules. NeverSink has 7 strictness levels over 500+ rules.
Players get confused when rule semantics are opaque (Last Epoch's "total tiers") and value tools that say which rule
matched. Auto-sell tools default to protecting valuable or bound items and confirming before selling.

### Cited Findings
- Path of Exile's "Advanced Mod Descriptions" (patch 3.1.2) "shows additional information about items when you press
  Alt. It breaks down the mods of the item into separate prefixes and suffixes, shows their possible ranges and what tiers
  they are" — [3.1.2 patch notes, official forum thread 2075735 (seen via a mirror)](https://vpn-erintek.duckdns.org/forum/view-thread/2075735/page/4) (summary).
  The tier numbers count every base type, so the best tier possible on a ring may read "Tier 5", which confused players
  used to their own numbering — [PoE forum: How do you interpret Mod Tiers?](https://www.pathofexile.com/forum/view-thread/2138559) (summary).
  The Alt view also shows mod tags and whether a value scales with quality — [PoE Wiki](https://pathofexile.fandom.com/wiki/List_of_mods_with_critical_mod_tag) (summary)
- Path of Exile 2 players asked for in-game tier display ("t4/t5") and use poe2db instead — [Steam PoE2 discussion](https://steamcommunity.com/app/2694490/discussions/0/598516657262956342/?l=swedish) (summary).
  Browser extensions add prefix/suffix and tier labels to the trade site — [Chrome Web Store: TierFill](https://chromewebstore.google.com/detail/gglcnglencknfbdejeepmfibimnlicjd) (summary)
- Diablo IV: Advanced Tooltip Compare colours gains green and losses red, and Shift compares — [Den of Geek](https://www.denofgeek.com/games/diablo-4-best-hidden-setting-compare-gear), [Windows Central](https://windowscentral.com/gaming/the-one-diablo-4-tip-i-wish-i-knew-before-i-started-playing) (summary).
  For the Greater Affix star, see section 2 (conflicting sources).
- WoW: a green-arrow overlay marks bag items that are upgrades — [Gethe/wow-ui-source](https://github.com/Gethe/wow-ui-source) (read)
- Path of Building: "see exactly how each node will affect your character" and "how much of an upgrade a given item is" — [PathOfBuilding README](https://github.com/PathOfBuildingCommunity/PathOfBuilding) (read)
- Path of Exile's filter language uses RGB colours (0-255) for SetTextColor, SetBorderColor and SetBackgroundColor, plus
  SetFontSize, PlayAlertSound, PlayEffect and MinimapIcon — [poedb Item Filter](https://poedb.tw/tw/Item_Filter), [PoE Wiki item filter guide](https://pathofexile.fandom.com/wiki/Guide:Item_filter_guide) (summary).
  A player proposed an "ActionGroup" block because the same highlight actions must be repeated in every block (a
  suggestion, not a feature) — [PoE forum 3663879](https://webcdn.pathofexile.com/forum/view-thread/3663879) (summary)
- NeverSink's filter:
  - "Over 500 different rules", economy-based tiering of uniques, currency and 20+ other lists
  - 7 strictness levels (0-SOFT to 6-UBER-PLUS-STRICT, SEMI-STRICT recommended for beginners) and 16 visual styles that
    keep the same filtering
  - a switch from levelling to endgame rules at item level 68
  - a blue minimap icon for "items that need manual checking"
  - highlights for identified items with several good mods — [NeverSink-Filter README](https://github.com/NeverSinkDev/NeverSink-Filter) (read)
  - Active font-size lines in the REGULAR file: 45 (543), 40 (131), 35 (28), 30 (1) and 18 (9, used for the hidden
    "hide layer" style; more size-18 blocks are commented out at this strictness). Almost everything shown is enlarged
    and junk is shrunk — [NeverSink-Filter](https://github.com/NeverSinkDev/NeverSink-Filter) (read)
- Last Epoch's in-game filter:
  - Rules can require "Individual Tiers - More or Equal: 6", "Total Affix Tiers - More or Equal: 12" and "With at least N" affixes — [Last Epoch forum 70038](https://forum.lastepoch.com/t/loot-filter/70038) (summary)
  - The total counts only the affixes listed in that rule, which players mistook for a bug — [forum 70656](https://forum.lastepoch.com/t/loot-filter-help-unsure-if-bug/70656) (summary)
  - Stacked tier rules misfired, with a T7 item shown un-emphasised — [forum 70684](https://forum.lastepoch.com/t/loot-filter-doesnt-apply-tier-level-and-emphasize/70684) (summary)
  - A common setup: a base rule for the wanted affixes, tiered variants, a catch-all, a rule for uniques and exalted
    items, then hide all — [Steam LE discussion](https://steamcommunity.com/app/899770/discussions/0/5950985868223268343) (summary)
  - Maxroll's guide (updated April 2025): the LE planner can generate a filter from a build's planned affixes — [Maxroll](https://maxroll.gg/last-epoch/resources/understanding-maxroll-loot-filters) (summary)
  - The "Terrible Tooltips" mod adds tier-coloured affix labels and shows which filter rule matched each item — [Nexus Mods](https://www.nexusmods.com/lastepoch/mods/30) (summary)
- Auto-sell conventions in WoW add-ons:
  - Legacy Vendor filters by rarity (Poor to Legendary), bind type, equipment slot and source, and by default shows a sell
    button at the vendor instead of selling at once — [CurseForge: Legacy Vendor](https://www.curseforge.com/wow/addons/legacy-vendor) (summary)
  - EasySell has a rarity threshold and protects soulbound items above grey by default — [CurseForge: EasySell](https://www.curseforge.com/wow/addons/easysell) (summary)
  - Another offers a quality filter plus an item-ID exclusion list — [CurseForge: Quality of Life](https://www.curseforge.com/wow/addons/quality-of-life-by-shmargus) (summary)
  - Another sells only at or below a maximum item level — [CurseForge: TransmogSeller](https://www.curseforge.com/wow/addons/transmogseller) (summary)
- RuneLite's ground-items plugin: value tiers with colours, "Hide under value" (default 0), lists of highlighted and
  hidden items, a "notify tier", and value counted at the highest of the item's prices by default — [GroundItemsConfig.java](https://github.com/runelite/runelite/blob/master/runelite-client/src/main/java/net/runelite/client/plugins/grounditems/GroundItemsConfig.java) (read)

### Inferences
- LootFall already measures power in LE (log base R of a multiplier, per `CLAUDE.md`). Each item can therefore carry one
  verdict: "+1.4 LE" with a green up-arrow or a red down-arrow, on the icon and the chest card. Per-stat gains and losses
  in green and red (Diablo IV) and the affix detail (tier, roll range, prefix/suffix, as in Path of Exile's Alt) open on
  hover, or on a held key that isn't Space or Enter, which belong to the chest.
- 616 affix variants: group affix lines by stat family, each with a small icon. Show the tier as a number plus a short
  roll bar. Give top-tier affixes a drawn star, with one star per such affix so Diablo IV's "two greater affixes look like
  one" complaint can't happen. A dot count of affixes on the card follows Last Epoch.
- The filter for Auto-Open and held finds should copy NeverSink's structure: about 5 strictness presets and a handful of
  readable rules (rarity at least X, is an upgrade, is a first find, has affix family Y at tier Z or better). Every kept or
  salvaged item should record the rule that decided it ("Terrible Tooltips"). Protect by default as EasySell and Legacy
  Vendor do: never auto-salvage an upgrade or a first find, and keep a short "recently salvaged" list for undo.
- Avoid Last Epoch's confusion with a preview: "this rule would have kept 3 of your last 100 finds".

### Gaps
- Filters and auto-salvage in Diablo IV, Grim Dawn, Torchlight Infinite and Lost Ark, and auto-sell or auto-equip in
  idle loot games (Legend of Mushroom and similar), were not verified (search budget exhausted).
- No data was found on how often players change filter presets, or on how satisfied they are with auto-salvage.

## 5. Giant trees: navigation, node shapes, path highlighting, planning, clear versus overwhelming

### Takeaway
Path of Exile proves trees at LootFall's scale (2,000 to 4,000 nodes) can work. It does so with a strict visual grammar:
- a size ladder (small, notable, keystone and mastery icons at 34, 49, 68 and 113 px)
- three frame states (unallocated, can allocate, allocated)
- orbit geometry
- zoom levels
- a search box that highlights matches

Players still call the tree overwhelming until they learn to look at notables first and lean on planners such as Path
of Building (path tracing, node-power heat maps). Diablo IV's paragon boards and Grim Dawn's constellations show two
other ways to cut a huge tree into readable chunks: square boards with gates, and named star groups with affinity
requirements.

### Cited Findings
- Path of Exile 1 tree data (official export, tag 3.29.1, committed 2026-07-31), counted from `data.json`:
  - 3,389 node records in 797 groups
  - excluding ascendancy and proxy records, the nodes connected into the tree are 1,476 small passives, 454 notables, 48
    keystones, 315 masteries, 57 jewel sockets and 7 class starts (2,357 in all)
  - another 301 notables and 8 keystones are records not connected to the tree, likely anoint-, jewel- or league-only
  - 558 records belong to ascendancies
  - a character gets `totalPoints` 123 plus 8 ascendancy points, so a full build takes about 5% of the connected tree
  - the coordinate span is about 26,600 x 21,600 units
  - [grindinggear/skilltree-export](https://github.com/grindinggear/skilltree-export) (read)
- Path of Exile's layout geometry (same export):
  - orbit radii [0, 82, 162, 335, 493, 662, 846] with [1, 6, 16, 16, 40, 72, 72] positions per orbit
  - orbits 2 and 3 went from 12 to 16 positions in 3.17 "to give us more angles to work with" (45-degree steps added)
  - sprite sheets at zoom levels 0.1246, 0.2109, 0.2972, 0.3835 and 0.5, a 4x range
  - group background discs of 180, 233 and 370x187 (half) px, and class-start art of 314x289 px
  - [skilltree-export README and data.json](https://github.com/grindinggear/skilltree-export) (read)
- Path of Exile's node grammar, at the 0.5 sheet:
  - icons: small 34 px, notable 49 px, keystone 68x69 px, mastery 113 px
  - frames: small `PSSkillFrame` 51 px (normal, highlighted, active); notable 76 px and keystone 109x111 px, each in
    `Unallocated`, `CanAllocate` and `Allocated` states; jewel frames 76 px, cluster sockets 100 px
  - connectors have separate "Active" and "Normal" line sprites (17 px tall) and arc sprites for each orbit
  - so size and frame give the node class, and the frame state gives reachability
  - [skilltree-export data.json](https://github.com/grindinggear/skilltree-export) (read)
- Node roles: small passives give minor bonuses and lead to notables, keystones and masteries — [Maxroll](https://maxroll.gg/poe/getting-started/passive-skill-tree-for-beginners) (summary).
  Notables "provide a significant boost". Keystones "significantly alter how the character functions". The tree has 21
  jewel sockets, 15 basic and 6 large ones for cluster jewels — [PoE Wiki: Passive skill](https://pathofexile.fandom.com/wiki/Passive_skill), [poewiki](https://www.poewiki.net/wiki/Passive) (summary)
- Path of Exile 2's tree data (Path of Building PoE2, `src/TreeData/0_5/tree.json`, committed 2026-10-01):
  - 4,914 node records
  - excluding ascendancy records, the connected ones are 2,898 small passives (293 of them "Attribute" nodes where you
    pick an attribute), 733 notables, 11 keystones, 8 jewel sockets and 6 class starts
  - 431 ascendancy records
  - the file also flags 368 image-only and 155 conditionally unlocked records, so the allocatable count is somewhat lower
  - [PathOfBuildingCommunity/PathOfBuilding-PoE2](https://github.com/PathOfBuildingCommunity/PathOfBuilding-PoE2) (read)
- Path of Exile 2's tree search:
  - a "Passive Skill Filter" search bar at the top right (reached by zooming out); typing e.g. "Bleeding" highlights
    every related node "in flashing colors" — [Sportskeeda](https://www.sportskeeda.com/mmo/path-exile-2-poe2-passive-skill-filter-search-skill) (summary)
  - guides advise Ctrl-F and looking at notables first ("5 or 10 Notables is easier than scanning dozens of small
    passives") — [Mobalytics](https://mobalytics.gg/poe-2/guides/passive-skill-tree) (summary)
  - the tree's "size and complexity ... can be overwhelming until you familiarize yourself with it" — [Vulkk, 2025-02-08](https://vulkk.com/2025/02/08/path-of-exile-2-passive-tree-guide/) (summary)
- Path of Building's planning tools:
  - path tracing: "mouse over a sequence of nodes while holding shift, then click to allocate them all" — [PathOfBuilding README](https://github.com/PathOfBuildingCommunity/PathOfBuilding) (read)
  - a "Show Node Power" checkbox draws a heat map over the tree for a chosen stat, with a maximum search depth (All, 5,
    10, 15 or Custom) "to limit calculation time of the heat map" — [PathOfBuilding src/Classes/TreeTab.lua](https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/dev/src/Classes/TreeTab.lua) (read)
- Diablo IV's paragon boards (sources disagree on several numbers):
  - normal nodes give +5 to one attribute; rare nodes give two attributes plus a bonus that unlocks at an attribute
    threshold; "always 5 Magic Nodes surrounding any Rare Node" — [Maxroll](https://maxroll.gg/d4/resources/paragon-boards) (summary)
  - gate nodes at the edges give +5 to all attributes and attach a new board, which can be rotated before attaching — [GameSpot](https://Gamespot.Com/articles/diablo-4-paragon-board-glyphs-guide/1100-6514819/), [Wowhead](https://www.wowhead.com/diablo-4/pt/news/diablo-4-paragon-board-tips-and-tricks-333435) (summary)
  - a glyph's bonus counts the purchased nodes inside its radius, drawn on the board as a red area — [KeenGamer](https://www.keengamer.com/articles/guides/diablo-4-complete-paragon-board-guide-boards-glyphs-and-more-explained/) (summary)
  - the starting radius is given as 2 — [Fextralife](https://diablo4.wiki.fextralife.com/Glyphs) (summary) — or 3 — [Wowhead](https://www.wowhead.com/diablo-4/pt/news/diablo-4-paragon-board-tips-and-tricks-333435) (summary), growing at ranks 15 and 46 — [DiabloBytes](https://www.diablobytes.com/diablo-iv/guides/paragon-guide/) (summary)
  - one build-defining legendary node per board — [Guildorder](https://guildorder.com/games/diablo4/wiki/paragon-board-system) (summary).
    The starter board has none, and later boards have it at the centre (the search summary didn't attribute this to one
    page; candidates: [Fextralife](https://diablo4.wiki.fextralife.com/Paragon), [PureDiablo](https://purediablo.com/diablo4/Paragon_Board))
  - a cap of 300 paragon points at level 100, with a planner recommended before spending — [Guildorder](https://guildorder.com/games/diablo4/wiki/paragon-board-system) (summary)
  - rare-node counts per board conflict (4, 6 or "10 or more") — [Wowhead S4 sorcerer](https://www.wowhead.com/diablo-4/cn/guide/classes/sorcerer/paragon-boards), [Mobalytics](https://mobalytics.gg/blog/diablo-4/paragon-board-system-how-it-works/) (summary)
- Grim Dawn's devotion:
  - each restored shrine gives one Devotion point, spent on a star map opened from the Skill window
  - you start with only the Crossroads constellation
  - higher constellations need affinity in five types (Chaos, Order, Eldritch, Primordial, Ascendant)
  - brighter stars grant Celestial Powers that you bind to skills
  - up to 50 points over three difficulties (55 with Ashes of Malmouth)
  - [grimdawn.com devotion guide](https://www.grimdawn.com/guide/character/devotion) (summary)
  - an older article counts 434 stars and 50 Celestial Powers — [PC Invasion](https://www.pcinvasion.com/?p=120847) (summary; predates the expansions)
  - players build planners themselves — [Crate forum: Devotion Picker](https://forums.crateentertainment.com/t/tool-grim-dawn-devotion-picker/155421?page=3) (summary)

### Inferences
- LootFall's 3,259-node tree is the size of Path of Exile's (2,357 connected main-tree nodes plus 558 ascendancy
  records) and below PoE2's export. PoE's grammar is the proven template, and its weak spot (overwhelm) is known and
  answered by search and planning.
- Reuse PoE's proportions on LootFall's grid. Frames 51 : 76 : 109 are about 1 : 1.5 : 2.1 for small : notable :
  keystone. In 2x pixel art that could be 16, 24 and 32 px node art (32, 48, 64 px on screen at default zoom), with paragon
  or mastery hubs larger again. Keep PoE's three frame states (locked, reachable, taken) and its two line states.
- Six sectors and fourteen rings map naturally onto a radial PoE-style layout: each sector a colour-coded wedge, rings
  as orbits by depth, notables where rings meet, keystones on the outer rim. Readability comes from repetition: every
  sector built with the same ring pattern.
- Must-haves, ordered by evidence:
  1. Search that highlights matches with pulsing rings (PoE2).
  2. A path preview with point cost, plus click-to-path to a far node (PoB's Shift trace).
  3. A node-power heat map coloured by LE gain, depth-limited for speed (PoB).
  4. Zoom levels with level of detail: far out show only sectors, keystones and notables; near in show small nodes.
     PoE's sheets span 4x.
  5. A "planned" overlay distinct from "allocated".
- Since a build takes only about 5% of a PoE-sized tree, the screen should lead with a summary: total bonuses by stat
  family, and the next suggested node from LootFall's `Policy`, shown as a highlighted target. Browsing the full map
  becomes optional for idle players.
- Endless paragon shouldn't extend the map forever. Diablo IV caps boards at 300 points. A repeating, compact "board"
  or counter (spend into a few stat tracks), kept apart from the authored tree, stays readable.

### Gaps
- Last Epoch's per-skill specialisation trees and class passive trees (points-gated masteries), Diablo III's endless
  paragon, and any in-game planning mode in PoE1 or PoE2 were not verified (search budget exhausted).
- No official GGG count of the PoE2 tree was found. The counts above come from data records whose visibility could not
  be fully separated.
- No player study (surveys, retention) on tree overwhelm was found. The evidence is guides and forum opinion.

## 6. Collection logs and codex UIs at large scale

### Takeaway
The largest collection UIs found, OSRS's collection log (1,925 slots) and PoE's Unique Collection Tab (about 1,500), work
through category tabs, grids that show obtained against missing, a notification per new entry (OSRS's chat line, which
players can switch off), and hiding what can't realistically be obtained. Diablo IV's Codex of Power adds filters
(class, category, source, favourites, newly obtained) and keyword search. LootFall's 114,432-entry codex is about 60x
OSRS's. It can't be a flat list. It has to be a hierarchy of aggregated progress with heat-map grids at the bottom.

### Cited Findings
- OSRS collection log facts:
  - a book from The Collector in the Varrock Museum
  - records "nearly every unique drop"
  - also reachable from the adventurer's log and the Character Summary
  - "a cap on the amount each item can be tracked"
  - an older revision lists 1,807 slots and 1,610 unique entries, a newer one 1,925 slots and 1,717 unique entries
  - [OSRS Wiki: Collection log](https://oldschool.runescape.wiki/w/Collection_log) (summary)
- OSRS announces each new entry in chat as "New item added to your collection log: <item>". An in-game option
  (`OPTION_COLLECTION_NEW_ITEM`) controls this, and RuneLite can take a screenshot of every new entry into a "Collection
  Log" folder (`ScreenshotPlugin.java`, master 2026-10-24) — [runelite/runelite](https://github.com/runelite/runelite/blob/master/runelite-client/src/main/java/net/runelite/client/plugins/screenshot/ScreenshotPlugin.java) (read)
- Path of Exile's Unique Collection Tab, introduced 2019-03-11:
  - stores one of each unique
  - uniques that aren't regularly obtainable "don't have visible slots until you acquire one", so the tab shows no gaps
    you can't realistically fill — [Bex_GGG announcement](https://devtrackers.gg/pathofexile/p/3de9e1f6-introducing-the-unique-collection-tab) (summary)
  - capacity 1,483 in 3.25; alternate-art and very limited uniques have hidden slots — [GGG forum unique list](https://web.poecdn.com/forum/view-thread/1835899) (summary)
- Diablo IV's Codex of Power:
  - filter by class or category, and hover an aspect to see where it comes from — [Icy Veins](https://www.icy-veins.com/d4/guides/legendary-aspects-codex-of-power-guide/) (summary)
  - another guide lists filters for own class only, unlockable through a dungeon, favourites and newly obtained, plus
    keyword search ("darkness, armor, or damage") — [Blizzard Watch](https://blizzardwatch.com/2023/04/24/diablo-4-codex-power-legendary-aspects/), [Mein-MMO](https://mein-mmo.de/en/diablo-4-simple-trick-makes-the-new-upgrade-system-even-easier,1129898) (summary; the summary didn't say which of these two articles)
- Melvor Idle:
  - the Completion Log sits under "Other" in the side menu and lists mastery per item
  - both local and global mastery count toward completion
  - Steam achievements reward 100% in categories (Item Master, Pet Master, Mastery Master, Monster Master, The Completionist)
  - [Steam discussion 3199241400250725473](https://steamcommunity.com/app/1267910/discussions/0/3199241400250725473), [Steam discussion 3192495424129812592](https://steamcommunity.com/app/1267910/discussions/0/3192495424129812592), [Melvor Idle achievements](https://steamcommunity.com/stats/1267910/achievements) (summary of 2021-22 posts; claims not mapped to individual threads)
- Legends of Idleon's cards show per-entry depth as 0 to 6 stars, with steeply growing copy requirements — [DigitalTQ](https://www.digitaltq.com/wiki/idleon/items/cards/sacrilegious-kattlekruk-card-1782) (summary)
- Antimatter Dimensions keeps achievements as one of its 11 top-level tabs — [AntimatterDimensionsSourceCode, src/core/secret-formula/tabs.js](https://github.com/IvarK/AntimatterDimensionsSourceCode) (read)

### Inferences
- The arithmetic of LootFall's codex: 149 slots x 12 rarities = 1,788 slot-rarity pairs. 114,432 / 1,788 = 64 quality-band
  x depth-era cells per pair (e.g. 8 x 8). A natural UI:
  1. Overview: 8 slot kinds with completion bars, plus a rarity x era heat map.
  2. Kind page: one row per slot with a 12-segment rarity bar.
  3. Slot page: a 12 x 64 grid of tiny cells, filled where found. At 8 px cells that is 96 x 512 px, which fits the 1280 canvas.
- Hide the unreachable, as the Unique Collection Tab does: show only eras the player has reached and rarities that can
  drop at their depth. Otherwise the opening screen reads "0.01% complete", which deflates rather than motivates.
- Notifications must be batched. OSRS players can switch the chat line off, and an idle game makes many first finds an
  hour early on. Collapse them into one line per chest ("3 new codex entries") with a pulse on the codex tab. Keep a full
  popup for milestones (first of a rarity, a slot complete, an era complete).
- Copy Diablo IV's filters: newly found, by slot kind, by rarity, by era, and search. Hovering a missing cell should say
  where it can drop (the codex aspect "source"). That turns the codex into a goal-finder.
- Milestones should pay at aggregate levels (per slot, per rarity, per era), much as Melvor rewards 100% per category.
  Entry-level rewards would mean 114,432 tiny events.

### Gaps
- Pokedex-style seen-versus-caught conventions, Destiny's collections and Torchlight Infinite's codex were not
  verified (search budget exhausted).
- The OSRS collection-log tab names and its milestone rewards could not be read (page didn't load, and the summary omitted
  them).
- No source was found on throttling collection notifications in idle games.

## 7. Big-number formatting, progress visualisation, and presenting many systems at once in idle games

### Takeaway
Idle games converge on these conventions:
- a choice of notation: Antimatter Dimensions defaults to "Mixed scientific" and offers about 20 more
- short suffixes or long names at the low end: Cookie Clicker spells out "million" upward and groups digits with commas
  below that
- scientific notation once the names run out (swarm-numberformat falls back at 1e249)
- log-scaled progress bars aimed at the next named goal (AD's bar fills log10(current)/log10(goal))
- systems gated so a tab appears only when it unlocks, with a one-time highlight, hidable tabs, and an in-game "How to
  Play" reference

### Cited Findings
- Antimatter Dimensions' notation library:
  - base notations: Scientific, Engineering, Letters, Standard, Emoji, Mixed Scientific, Mixed Engineering, Logarithm,
    Brackets, Infinity, Roman, Dots, Zalgo, Hex, Imperial, Clock, Prime, Bar, Shi, Blind, ALL, plus a community pack (Long
    scale, Infix engineering, and others)
  - `format(value, places, placesUnder1000)` sets mantissa precision separately above and below 1000
  - commas are put in exponents (e.g. "1e100,000") for exponents of at least 100,000, the setting's lower bound
  - values at or above MAX_VALUE print "Infinite"
  - [antimatter-dimensions/notations README](https://github.com/antimatter-dimensions/notations) (read)
- Antimatter Dimensions' default notation is "Mixed scientific" (`src/core/player.js`) — [AntimatterDimensionsSourceCode, commit 2026-07-17](https://github.com/IvarK/AntimatterDimensionsSourceCode) (read).
  The wiki shows 1e29 as Scientific "1e29", Engineering "100e27" and Letters "100i". "Painful" notations unlock an
  achievement and show numbers less precisely. The mobile version adds Custom and Random — [AD wiki: Notations](https://antimatterdimensions.wiki.gg/wiki/Notations) (summary)
- swarm-numberformat, "designed for incremental games like Swarm Simulator":
  - standard by default ("10.000 billion"), scientific ("1.0000e10"), engineering ("10.000E9"), longScale ("10.000 milliard")
  - "At 1e249, 'standard' and 'longScale' fall back to scientific notation"
  - a short flavour gives "10.0B"
  - sig figs are configurable, and formatted strings can be parsed back
  - [erosson/swarm-numberformat](https://github.com/erosson/swarm-numberformat) (read)
- Cookie Clicker's `Beautify` (`main.js`, GitHub mirror of the web version):
  - from 1,000,000 up, numbers get long English names (" million", " billion" ... " quadrillion" ... then
    "decillion"-style compounds), rounded to 3 decimals; below that, digits grouped with commas
  - an alternative raw mode prints digits and switches to 3-significant-figure exponent form when the number would
    print with "e+"
  - the "Short numbers" option toggles between them (default `prefs.format=0`, long names)
  - a short suffix table (k, M, B, T, Qa, Qi, Sx, Sp, Oc, No, Dc, ...) is also defined
  - [ozh/cookieclicker main.js](https://github.com/ozh/cookieclicker/blob/master/main.js) (read)
  - players report the option missing in some languages — [Steam discussion](https://steamcommunity.com/app/1454400/discussions/0/2961670087548848662) (summary)
  - one developer says full names "look fine", while abbreviations like "QD" take extra space — [itch.io post](https://itch.io/post/10387683) (summary)
  - another player mocks the "jibazillion fubarillion" naming style — [Revolution Idle Steam discussion](https://steamcommunity.com/app/2763740/discussions/0/597396334368006383) (summary)
- break_infinity.js handles numbers "bigger in magnitude than 1e308, up to as much as 1e9e15" and prioritises speed over
  accuracy. It has a C# port (BreakInfinity.cs, "for your C#/Unity incremental games"), and break_eternity.js goes to
  10^^3 and beyond — [Patashu/break_infinity.js README](https://github.com/Patashu/break_infinity.js) (read)
- Antimatter Dimensions' progress bar:
  - fills `log10(current) / log10(goal)`, capped at 100%
  - the tooltip names the goal ("Percentage to Infinity", "Percentage to next Tesseract", "Percentage to Challenge goal")
  - challenge goals take priority "because numbers will always be much smaller than normal and therefore default filling
    won't be meaningful"
  - a few goals use linear progress
  - source: `src/components/tabs/antimatter-dimensions/AntimatterDimensionProgressBar.vue` — [AntimatterDimensionsSourceCode](https://github.com/IvarK/AntimatterDimensionsSourceCode) (read)
- Antimatter Dimensions' tabs and onboarding:
  - 11 top-level tabs (Dimensions, Options, Statistics, Achievements, Automation, Challenges, Infinity, Eternity,
    Reality, Celestials, Shop) and 42 subtabs
  - each is gated by a `condition()` (e.g. Automation appears at 1e40 total antimatter, Infinity once Infinity is
    unlocked), and 49 of the 53 tabs and subtabs are `hidable: true`
  - a tab notification fires once per feature; it highlights the tab and subtab unless the player is already there, and
    force-unhides them
  - a How-to-Play modal has 48 entries, including "Common Abbreviations", "Effect Stacking" and "Offline Progress"
  - sources: `src/core/secret-formula/tabs.js`, `src/core/tab-notifications.js`, `src/core/secret-formula/h2p.js` — [AntimatterDimensionsSourceCode](https://github.com/IvarK/AntimatterDimensionsSourceCode) (read)
- LootFall measures power in LE, the log base R of a multiplier, and its progression framework is engine-free C# in
  `scripts/Progression` — [CLAUDE.md](/home/user/lootfall/CLAUDE.md) (read)
- LootFall's `Big` is a mantissa in [1, 10) with a 64-bit exponent ("like break_infinity.js"), because "monster health
  at depth 20,000 is about 1e830". Its `ToString` already prints short suffixes (K, M, B, T, Qa, Qi, Sx, Sp, Oc, No, Dc)
  with 3 significant figures and scientific notation ("0.00e" + exponent) past Dc — [scripts/Progression/Big.cs](/home/user/lootfall/scripts/Progression/Big.cs) (read)

### Inferences
- LootFall's LE is already the logarithm that Antimatter Dimensions computes for its bar, so the headline number can
  stay small and linear ("Power 37.2 LE"). Raw stats and gold go through one formatter, with notation chosen in Settings.
  A sensible default, following AD: digits with thin grouping below 1e6, short suffixes up to the end of a short table
  (around 1e33, "Dc"), then mixed scientific with 3 significant figures and a fixed width so labels don't jitter. It must
  be ASCII-only for Kenney Mini (no Emoji or Zalgo notations).
- Progress bars toward walls, bosses, prestige and slot unlocks should be log-scaled toward a named goal with a tooltip,
  as AD's are. Linear bars on exponential quantities sit near empty and then jump.
- To present many systems (gear, chest, forge, ranks, mastery, talents, codex, two prestige trees), follow AD's
  structure. Each system is a tab that appears only when it unlocks, gated in the engine-free Core so the sim, the
  fast-forward and the bot (`Policy`) see the same unlocks. A one-time highlight fires on unlock. Finished or irrelevant tabs can be hidden. A
  How-to-Play reference has one entry per system, including "abbreviations" and "how bonuses stack". LootFall's slots
  already open gradually, and the other systems should do the same.
- `Big.cs` already plays break_infinity's role, and its `ToString` already matches the default above (suffixes to Dc,
  then 3-figure scientific). Numbers reach about 1e830, far past any suffix table, so most of the late game will show
  scientific. What's missing is a Settings choice of notation (scientific, engineering, letters, as in AD's ASCII set) and
  fixed-width labels.

### Gaps
- NGU Idle's, Melvor Idle's and Legends of Idleon's number formatting and UI organisation were not verified (search
  budget exhausted). An "overwhelming idle UI" query to r/incremental_games was among those lost to the limit.
- No quantitative data was found on which notation players prefer or on how gating systems affects retention.
- The Kongregate "Math of Idle Games" series and the GDC idle talks could not be fetched or searched in time.
