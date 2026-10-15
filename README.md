# LootFall

A small game about the satisfaction of opening treasure chests. You're an appraiser at a fantasy auction house:
inspect sealed chests for clues, outbid your rivals, pick the lock, and crack them open.

**Status:** first playable prototype (placeholder art and synthesized sound). Built with Godot 4.7 (.NET / C#), targeting PC on Steam.

## The loop

1. **Auction.** Each lot comes with a public provenance (shipwreck, noble estate, wizard's tower...).
   Spend two of three inspections:
   - **Shake it**: what it sounds like, whether anything fragile is inside, and roughly how full it is.
   - **Weigh it**: total weight; compare against an empty chest.
   - **Examine lock**: who made the lock, which hints at value and sets how hard it is to pick.

   Every clue narrows the appraisal bar. Then bid against one rival:
   Old Marta judges by the lock, Baron Vex bids on gut feel, and Pip the Sharp somehow knows what's inside.
2. **Lockpick.** Press when the pick is in the green zone to set each pin.
   No slips = **Masterful** (+25% value, and you'll find hidden compartments). Three slips snaps the pick.
   You can always force it with the crowbar, but anything fragile will shatter.
3. **Open.** Hold to open. The glow climbs through each rarity up to the best item inside,
   then the lid bursts and items come out one by one, best last.
4. **Results.** Profit, and how your appraisal compared to the real value.

## Controls

| Action | Input |
|---|---|
| Set a pin | Space or left click |
| Force the lock | F or the crowbar button |
| Open the chest | Hold Space or left mouse |
| Read an item's flavor text | Hover its card |

## Development

The code is split so the game rules can be tested without the engine:

| Folder | What's in it |
|---|---|
| `scripts/Core` | Pure C# rules: items, chest generation, the appraiser, rivals, loot resolution. No Godot types. |
| `scripts/Phases` | The three screens of the loop (auction, lockpick, opening). |
| `scripts/Ui` | Code-drawn visuals (chest, item cards, HUD), particles and styling. |
| `scripts/Audio` | Placeholder sound effects, synthesized at startup. |
| `scripts/Debug` | Balance simulation and an autoplay bot for screenshots and recordings. |

Useful commands (Linux; run `tools/setup-cloud.sh` first in a fresh container):

```bash
dotnet build                                               # compile
godot --headless --path . -- --sim                         # balance report
godot --headless --path . -- --find-demo                   # seeds with a showy first lot
xvfb-run -a godot --path . --rendering-driver opengl3 -- \
  --autoplay --seed=1113 --shots=$PWD/shots --quit-at-end  # play one lot, save screenshots
godot --headless --path . --export-release "Windows Desktop" build/windows/LootFall.exe
```

Add `--write-movie movies/lot.avi --fixed-fps 60` to the autoplay command to record video with sound.
