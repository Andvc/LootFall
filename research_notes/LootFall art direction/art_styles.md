# Art styles for an idle/incremental loot game (LootFall): comparison

Method note (25 Oct 2026): page fetches failed (gamedeveloper.com and 80.lv returned DNS errors, `getaddrinfo ENOTFOUND`), as the brief warned. Every claim below rests on a search engine's summary of the cited page, not a full read. Quotes are as the summary gave them. Raijin, VG Insights, Gamalytic, SteamSpy, SteamPulse, Vaporlens, PlayTracker, Sensor Tower and AppMagic figures are third-party estimates; developer or publisher announcements are marked as such.

## 1. Each style: notable games, readability, juice, loot/rarity conveyance, solo-developer pipeline cost

### Takeaway
No style wins on every axis.

- **Most readable in busy scenes:** styles that split a crisp, high-contrast character layer from a softer background. Examples are ink/graphic-novel (Hades, Darkest Dungeon), Torchlight's painterly backgrounds under crisp comic-book characters, Diablo III's layering, cel-shading and flat cartoon.
- **Cheapest to scale to many animated, equipment-bearing characters:** pipelines where a rig drives the animation rather than drawn frames. These are 3D rendered to 2D sprites (Dead Cells; Hades' characters; Diablo I/II) and 2D cut-out/skeletal (Spine skins). This matters doubly with AI-generated assets, because AI image generators drift from frame to frame.
- **Weakest fit:** silhouette and watercolor, for a game whose payoff is reading colour-coded loot.

### Cited Findings

#### Low-res pixel art (Vampire Survivors, Loop Hero, Legends of Idleon)
- Vampire Survivors' sprites largely came from a purchased asset pack that was itself based on Castlevania. Developer Luca Galante said so to PC Gamer. Poncle later hired artists to emulate the style and redo the offending sprites, but kept some. — [Inverse](https://www.inverse.com/gaming/vampire-survivors-castlevania-nintendo-switch-stealing-claims-dlc); [Digital Trends](https://digitaltrends.com/?p=3743839)
- Loop Hero is described in Devolver's sales coverage as a "pixel-art deck-building roguelike". — [RPG Site](https://www.rpgsite.net/news/12088-loop-hero-now-available-for-nintendo-switch-pc-version-hits-1-million-sold)
- IdleOn is described as "colorful pixel art" (a podcast description, so a weak source). — [Ausha podcast](https://podcast.ausha.co/entertainmerchworld/idleon-the-addictive-idle-mmo-you-didn-t-know-you-needed-and-yes-there-s-merchandise-too)
- Busy-scene readability is the weak point of the survivors-like pixel games.
  - A November 2024 Steam thread says late-game Vampire Survivors weapon effects hide the screen, with no way to lower them, and asks for an effects-visibility slider. — [Steam discussion](https://steamcommunity.com/app/1794680/discussions/0/4631482569784862180)
  - A fan mod, "No Particles", makes most particles transparent. — [Attack of the Fanboy](https://attackofthefanboy.com/guides/best-vampire-survivors-mods/)
  - Family Gaming Database lists 26 accessibility features for the game; its notes mention no effects-opacity option. — [FGDB](https://www.familygamingdatabase.com/accessibility/Vampire+Survivors)
- A 2023 Halls of Torment thread asks for an opacity slider and names Bio Prototype and Soulstone Survivors as games that already have one. — [Steam discussion](https://steamcommunity.com/app/2218750/discussions/0/3801652929323026410)
- Dark Light: Survivor ships a "Pixel Mode" that puts readability and performance stability ahead of visual complexity. — [PixelNitro](https://pixelnitro.com/?p=5529)
- **Cost.** A 2017 GameDev.net thread ("what is cheaper, pixel art or low poly") concluded that 2D sprites are easier to learn but more time-consuming to make, so they can end up costing more. — [GameDev.net](https://gamedev.net/forums/topic/694163-what-is-cheaper-pixel-art-or-low-poly). Blogs frame pixel art as the safe pick for a solo developer with 2D skills (vendor and agency blogs). — [hitem3d blog](https://blog.hitem3d.ai/blog/Game-Art-Styles-A-Complete-Guide-to-Choosing-the-Right-Visual-Style); [Pixune](https://pixune.com/blog/art-styles-in-indie-games-vs-aaa-games/)
- **AI generation.**
  - Each AI-generated frame is "basically a new roll of the dice": tiny shifts in outline, shading bands or anatomy "flicker like crazy once you animate it", and models don't understand motion or spacing. — [Chronocrash forum](https://chronocrash.com/forum/goto/post?id=111189)
  - A sprite-generator vendor concedes that complex poses and animation come out inconsistent, simple idle poses work far better, style matching is "possible but not guaranteed", and individual pixel control doesn't exist during generation. — [Sprite-AI guide](https://www.sprite-ai.art/guides/ai-sprite-generator-guide)
  - Recommended mitigations: fixed per-frame canvas, a shared foot baseline, sketched motion arcs, and checking animations at the target frame rate. — [SpriteCook](https://www.spritecook.ai/blog/animating-sprite-sheets-tips-for-consistency)
  - The open-source PerfectPixel Studio adds deterministic post-processing to AI output to enforce consistency. — [SourcePulse](https://www.sourcepulse.org/projects/31645438)
  - Vendor marketing claims a locked style can be held across frames. — [PCTechMag, Jul 2026](https://pctechmag.com/2026/07/cutting-sprite-production-time-from-weeks-to-minutes-without-losing-consistency/)

#### Hi-bit / high-res pixel art (Sea of Stars; Dead Cells' rendered pixels)
- Sea of Stars (Sabotage, 2023) pairs 16-bit-inspired pixel art with dynamic lighting from a custom render pipeline.
  - The 2020 feature list cited pixel-perfect rendering, fully dynamic lighting of normal-mapped sprites, volumetric lighting and screen-space indirect lighting.
  - One person (Jeakim Hamel) worked on the lighting from 2018.
  - "all of the assets that we make have to answer to the technical needs of the dynamic lighting".
  - Reviewers praised "the exquisite pixel art and dynamic lighting system".
  - Sources: [MobileSyrup interview, Sep 2023](https://mobilesyrup.com/2023/09/11/sea-of-stars-level-design-sabotage-studio-thierry-boulanger-interview/); [Niche Gamer, Mar 2020](https://nichegamer.com/2020/03/19/the-messenger-prequel-rpg-sea-of-stars-enters-kickstarter-launches-2020-for-pc/); [Gfinity review](https://gfinityesports.com/reviews/sea-of-stars-review)

#### HD-2D (Octopath Traveler and Square Enix's later titles)
- HD-2D games put pixel sprites in backgrounds rendered entirely in 3D, often with complex camerawork. The aim was to reproduce pixel graphics "in a rich way with current technology" (director Keisuke Miyauchi). Lighting and shadow on pixel environments were emphasised. — [Wikipedia: HD-2D](https://en.wikipedia.org/wiki/HD-2D); [Unreal Engine spotlight, Oct 2019](https://www.unrealengine.com/en-US/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true)
- Development was trial and error, per a Famitsu interview. — [Siliconera](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)
  - Early builds looked flat.
  - Another attempt pushed resolution and saturation so far that it lost the pixel-art appeal.
  - Sprites looked sparse at large screen sizes, so the team raised tile and colour variety.
- Producer Tomoya Asano (2022) said HD-2D is more expensive than people realize, and gave that as the reason few others have adopted it. — [Wikipedia: HD-2D](https://en.wikipedia.org/wiki/HD-2D)

#### Hand-painted / painterly (AFK Arena; Torchlight's and Hades' environments; Diablo III)
- **AFK Arena (Lilith)** is credited with a "unique 2D art style". The art director said the team spent a long time looking for a style that would appeal to all players, drawing on Western mural painting and cathedral design (a French source, summarized in translation). A later trailer used a "Mucha" style. — [ActuGaming](https://www.actugaming.net/?p=294547); [Toy-People](https://www.toy-people.com/en/?p=95285)
- **Torchlight (Runic, 2009).** Art director Jason Beck took "'classic animation' techniques of a painterly background with crisp, comic-book-ish style characters atop it". The look was pitched as "Dragon's Lair meets The Incredibles", with a lighter, more inviting fantasy tone and art kept "pretty low-tech" so it would run on virtually any system. — [Engadget, Sep 2009](https://www.engadget.com/2009-09-10-an-insight-into-the-art-of-torchlight.html); [Gaming Nexus](https://www.gamingnexus.com/Article/2452/Torchlight-Interview); [Wikipedia](https://Www.wikipedia.org/wiki/Torchlight)
- **Diablo III**, from Christian Lichtner's GDC 2012 talk. — [Game Developer write-up](https://gamedeveloper.com/design/gdc-2012-diablo-iii-s-art-director-shows-off-design-process); [80.lv](https://80.lv/articles/blizzard-art-philosophy-in-diablo-iii/); [GDC](https://gdconf.com/news/gdc_2012_debuts_diablo_iii_jet)
  - A painterly stylized world "that takes full advantage of its fixed camera, while finding ways to enhance and support our core gameplay goals".
  - Principles: strong silhouettes, bold colour, dynamic animation.
  - Art decisions were subordinated to gameplay through "a layering system for any given moment in the game".
- **Hades' environment art** is mostly hand-painted 2D; its characters are 3D renders (see the pre-rendered entry below). — [Steam developer posts](https://steamcommunity.com/app/1145360/discussions/0/1864993755183591660)
- **Bastion**: a hand-painted isometric world with a "storybook vibe" (a roundup, so a weak source). — [ComicBasics](https://www.comicbasics.com/?p=81518)

#### Ink / graphic-novel (Hades, Darkest Dungeon)
- **Hades.**
  - A Supergiant developer credits its proprietary engine and tools for the game's "crisp, responsive feel and graphic novel look". — [Steam developer posts](https://steamcommunity.com/app/1145360/discussions/0/1738883810796606862)
  - Art director Jen Zee won the 2021 BAFTA for Artistic Achievement for Hades. — [Wikipedia: Jen Zee](https://en.wikipedia.org/wiki/Jen_Zee)
  - Unverified: claims that Hades keeps enemies distinguishable through particle explosions, relies on pose, timing and silhouette, and codes warm for the player and cool for enemies come only from low-quality blogs. No primary source was found. — [GeekChamp](https://geekchamp.com/the-art-of-hades-showcases-the-talent-that-went-into-this-beautifully-vibrant-game/); [TechYorker](https://techyorker.com/?p=747620)
- **Darkest Dungeon.** — [GameSpot](https://www.gamespot.com:443/articles/the-gothic-sensibilities-of-darkest-dungeon/1100-6424880/); [80.lv](https://80.lv/articles/red-hook-studios-talks-about-the-creation-of-darkest-dungeon); [Game Developer, Road to the IGF](https://www.gamedeveloper.com/audio/road-to-the-igf-red-hook-studios-i-darkest-dungeon-i-)
  - Chris Bourassa was the lone artist for much of development.
  - "I made a point of not using 'dead gray' or black anywhere but in the inks."
  - The palette is warm, with even cool colours yellowed toward worn parchment.
  - A touch of cuteness raises the characters' iconic quality.
- Modders can import Darkest Dungeon's hero animations into Spine and use the vanilla skeletons as a base. That suggests cut-out skeletal animation, from modder evidence rather than a developer statement. — [Steam DD workshop group](https://steamcommunity.com/groups/dd-workshop/discussions/0/135511757698227150)

#### Vector / flat cartoon (Brotato, Kingdom Rush)
- **Brotato** comes from a one-man studio, Blobfish (Thomas Gervraud). He describes survivors-like and autochess mechanics as "glued together by a hand drawn artstyle and a potato theme". — [Wikipedia](https://en.wikipedia.org/wiki/Brotato); [Brotato 1.0 press release, Jun 2023](https://www.gamedeveloper.com/press-release/brotato-10-release)
- **Kingdom Rush (Ironhide).** — [WN Hub](https://wnhub.io/news/other/item-14983); [WN Hub interview](https://wnhub.io/news/marketing/item-2156); [Game World Observer](https://gameworldobserver.com/?p=888); [GamingOnLinux, Oct 2018](https://gamingonlinux.com/2018/10/kingdom-rush-origins-is-a-polished-and-fun-tower-defense-game-worth-your-time); [Game Informer, Dec 2014](https://gameinformer.com/games/kingdom_rush_origins/b/ios/archive/2014/12/09/kingdom-rush-origins-game-informer-review.aspx)
  - Ironhide says it keeps "the classic cartoonish style fans love but with a lot more details and animations".
  - Vengeance was built with in-house tools plus Adobe Animate and Cocos2d-x.
  - Reviewers say the maps read clearly at a glance; Origins is "vibrant... seriously crisp", and "everything is clear and easy to understand".
  - No source confirms that the art is vector.

#### Chibi / anime mobile (AFK Arena, Legend of Mushroom, Capybara Go, Archero)
- **Archero (Habby).** Reviewers place its style "somewhere between cartoon and pixel-adjacent". Enemies are "chunky and readable at a glance", "flashy effects stay readable rather than turning the screen into noise", and the look is "simple but colourful". — [games.gg](https://games.gg/archero/); [Gamepressure](https://www.gamepressure.com/games/archero/zc5f01)
- **Legend of Mushroom.** The mushrooms are "cute but fierce", and customising your mushroom is a core activity. Appearance and new equipment both come through a gacha system. — [PocketGamer guide](https://www.pocketgamer.com/legend-of-mushroom/guide/); [Logrus IT portfolio](https://games.logrusit.com/en/portfolio/legend-of-mushroom/)
- **Capybara Go**: no art-direction source was found (see Gaps).

#### Low-poly 3D (Megabonk)
- **Megabonk** (Vedinad, solo developer, Unity) went from starting development in August 2024 to release on 18 September 2025. — [Mezha review](https://mezha.ua/en/reviews/megabonk-review-305493/); [Wikipedia](https://en.wikipedia.org/wiki/Megabonk); [Techprincess](https://techprincess.it/?p=749545)
  - The look: low-poly models, pixelated textures and sprite special effects.
  - One reviewer called the style partly cost-saving and partly a 1990s aesthetic trend. They said the graphics were one of the main things that put them off buying it, but stopped mattering once they played.
- Game Dev Report: Megabonk looks technically simple; the real difficulty is the design. — [Game Dev Report](https://gamedevreport.beehiiv.com/p/can-you-make-megabonk-8mil-or-do-players-like-pixel-art)
- A Polycount thread found untextured low poly "not very efficient either" for a solo developer. — [Polycount](https://polycount.com/discussion/comment/2670927)
- A vendor blog says 3D has a steeper learning curve (modelling, UV unwrapping, texturing, rigging) and pays off when the camera, physics or lighting is dynamic. — [hitem3d blog](https://blog.hitem3d.ai/blog/Game-Art-Styles-A-Complete-Guide-to-Choosing-the-Right-Visual-Style)
- AI 3D generators: Meshy outputs textured, rigged meshes, but the geometry is "often triangulated rather than clean quad topology, and dense in places it doesn't need to be". — [Techsy](https://techsy.io/blog/meshy-vs-higgsfield-vs-pixellab)

#### Stylized 3D (Torchlight, Diablo III/IV, Torchlight Infinite, Last Epoch)
- See the Torchlight and Diablo III entries under painterly.
- Diablo IV's art director said Diablo III "had this really beautiful painterly art style, it was handcrafted", and that D4 would combine that with Diablo II's darkness. — [PCGamesN](https://www.pcgamesn.com/diablo-4/diablo-2-art-style)

#### 3D pre-rendered to 2D sprites (Dead Cells, Hades' characters, Diablo I/II, Halls of Torment)
- **Dead Cells (Motion Twin).** — [WN Hub summary of the Game Developer article](https://wnhub.io/news/art/item-13775); [80.lv interview](https://80.lv/articles/interview-with-the-developers-of-dead-cells)
  - Thomas Vasseur was the only artist in the first year and had little hand-drawing experience.
  - He built character models and skeletons in 3DS Max and exported them as FBX; a homebrew tool renders them as pixel-art sprites.
  - "To make up for the lack of bandwidth and still deliver on quality, we had to find a pipeline that could give us great looking pixel art, without having to hand draw each and every retake."
  - Models and animation were "all done in 3DS Max by one guy", and animations can be changed without redrawing whole sequences.
  - The original article (first published on Gamasutra, before its 2021 rename to Game Developer) could not be fetched.
- Indie developers copy this approach to get normal maps and lighting, calling it "the same pipeline than the game dead cells". — [itch.io posts](https://itch.io/post/1413411); [itch.io](https://itch.io/post/1532795). Vasseur's article inspired a free 3D-to-2D tool. — [itch.io: 3D to 2D animations](https://midgelf.itch.io/3d-to-2d-animations)
- **Hades.** Supergiant 3D-renders and animates many characters, Zagreus included, and ships "thousands of frames of rendered animation from many angles". There is no real-time 3D: every frame of Zagreus is a unique image. One example is a 104-frame bow attack rendered at 32 angles. — [Steam developer post, Dec 2019](https://steamcommunity.com/app/1145360/discussions/0/1864993755183591660); [Steam developer post, Mar 2020](https://steamcommunity.com/app/1145360/discussions/0/1738883810796606862)
- **Hades II**: a Supergiant graphics programmer "rethought and optimized nearly every part of the pipeline" for the sequel's larger content load. — [DigiPen](https://www.digipen.edu/showcase/news/hades-2-graphic-programmer)
- **Halls of Torment** (Chasing Carrots, five people, Godot engine). — [FullCleared interview](https://fullcleared.com/features/inside-halls-of-torment-an-interview-with-chasing-carrots/); [Wikipedia](https://en.wikipedia.org/wiki/Halls_of_Torment); [1.0 press release](https://www.gamedeveloper.com/press-release/halls-of-torment-will-release-its-1-0-version-on-steam-this-september-24)
  - Its biggest visual inspirations were Diablo I and II, "isometric 2d games with gorgeous pre-rendered sprites", alongside Arcanum, Nox, Fallout and Baldur's Gate.
  - The team saw the low-fi pre-rendered look as a differentiator ("haven't seen many other games trying to emulate that look") and as its "biggest creative risk".
- **Windblown** (Motion Twin, 2026) reportedly combines Photoshop, Spine rigging and internal pixel-rendering on simplified 3D meshes. This comes from a translated repost and is unverified. — [foro3d](https://foro3d.com/de/2026/mayo/windblown-la-evolucion-tecnica-del-motor-de-dead-cells-al-caos-isometr.html)

#### Cel-shaded (Ravenswatch, Wind Waker)
- Cel shading is non-photorealistic rendering that makes 3D look flat, using fewer shade steps instead of gradients. Bold outlines and flat fills make colours pop. — [Wikipedia: Cel shading](https://wikipedia.com/wiki/Cel_shaded); [Wayline guide](https://wayline.io/blog/cel-shading-a-comprehensive-expert-guide)
- Wind Waker's developer said the style let the team present puzzle mechanisms and objects "in a more easy-to-understand way". — [Wikipedia: Cel shading](https://wikipedia.com/wiki/Cel_shaded)
- **Ravenswatch (Passtech).** The art director: "it just felt obvious to embrace an illustrative art style". A review calls the result a consistent cel-shaded dark-fairy-tale aesthetic. — [Passtech](https://www.passtechgames.com/ravenswatch-news/ravenswatch-artstation-blast/)

#### Watercolor / storybook (Child of Light, Okami)
- **Child of Light** (Ubisoft Montreal) draws on 19th-century fairy-tale illustration. A GDC talk by Patrick Plourde covers the successes and failures of reaching its watercolor look, which runs through characters, environments, menus, props and effects. — [GDC Vault](https://gdcvault.com/play/1021146/The-Art-of-Child-of); [O'Reilly](https://oreilly.com/library/view/hands-on-game-development/9781789538335/ff1a45a3-e790-41bc-bfd1-acb61199a5c3.xhtml)
- **Okami** is "heavily inspired by Japanese watercolour painting and wood carving art of the Ukiyo-e style". — [Parka Blogs](https://www.parkablogs.com/comment/4000)

#### Paper cut-out / diorama (Tearaway, Paper Mario)
- Tearaway is papercraft-inspired; players cut out a paper crown for a character (a weak source). — [TrendHunter](https://www.trendhunter.com/trends/video-game-dioramas)
- Cut-out skeletal animation attaches images to bones and animates the bones; its listed benefits include small files and swappable attachments. — [Udemy Spine course](https://www.udemy.com/course/the-ultimate-spine-game-rigging-animation-course/)

#### Silhouette (Limbo, Badland)
- In silhouetted 2D, the foreground is drawn as silhouettes over a lighter background, so outlines "need to be carefully designed so that they immediately convey what an object is to a player just from its shape". — [Giant Bomb: Silhouetted 2D](https://giantbomb.com/wiki/Concepts/Silhouetted_2D)
- Limbo's boy is identifiable only by his glowing white eyes. — [Hexus review](https://hexus.net/qa6sf)
- Restricting colour "necessitates clarity of design", and Limbo and Badland are recognisable from a single image. — [Pixune](https://pixune.com/blog/monochrome-art-style); [Polycount](https://polycount.com/discussion/comment/2370285)
- The risk: on a Limbo-inspired indie game, a commenter found it hard to tell the game from the background image, and the developer had cut a contrast option for time. — [itch.io](https://itch.io/post/5299178); [itch.io](https://itch.io/post/1364004)

#### How loot value and rarity are conveyed (all styles)
- Colour-coded rarity was popularized by Diablo (1996) and Diablo II (2000); designer David Brevik took the idea from Angband. — [Wikipedia: Loot](https://en.wikipedia.org/wiki/Loot_(video_games)); [Game Informer, May 2019](https://gameinformer.com/2019/05/18/the-surprising-origins-of-loot-rarity-colors)
  - Diablo II used grey, white, blue, yellow, orange and green for its item classes.
  - World of Warcraft (2004) set grey/white/green/blue/purple/orange, a ladder most loot games have followed since.
- Games still disagree on the tiers, and one columnist wants a single standard. — [GG Recon](https://www.ggrecon.com/articles/i-want-games-to-decide-on-a-rarity-system/)
- Path of Exile's item filters escalate signals beyond colour. — [PoE Wiki: item filter guide](https://www.poewiki.net/wiki/Item_filter_guide); [Bamboo Gaming PoE2 guide](https://www.bamboogaming.net/poe2/loot-filters); [Highwind's filter](https://www.pathofexile.com/forum/view-thread/1490867)
  - Light beams and minimap icons for chosen item categories (since patch 3.4.0).
  - Custom sounds, with louder alerts for valuable items.
  - Bigger fonts for wanted items, and hidden junk.
  - Normal, Semi-Strict and Strict filter levels; players are told to "start loose... then increase strictness".

### Inferences
- **Comparison matrix.** This is my assessment from the findings above plus general practice, not a sourced score. 1 = poor, 5 = strong. For cost, 5 = cheapest for a solo developer using AI generation.

| Style | Clarity small / busy | Juice | Loot & rarity fit | Longevity / low fatigue | Solo + AI cost | Visible gear | Main risk |
|---|---|---|---|---|---|---|---|
| Low-res pixel | 4 / 3 | 4 | 3 | 4 | 3 | 3 | AI pixel animation drifts; few pixels for 12 tiers and many slots |
| Hi-bit pixel (+lighting) | 4 / 3 | 5 | 4 | 5 | 1-2 | 2-3 | Hand frames x items; lit sprites need extra maps (Sea of Stars) |
| HD-2D | 3 / 3 | 5 | 4 | 4 | 1-2 | 3 | "More expensive than people realize"; blur and bloom cost clarity |
| Painterly 2D | 3 / 3 | 4 | 5 | 4 | 3-4 | 4 (cut-out rig) | Muddy values unless the foreground is crisp (the Torchlight fix) |
| Ink / graphic novel | 5 / 4 | 5 | 4 | 5 | 2-3 | 2-3 | Consistent inking; Hades-style per-frame renders are heavy |
| Flat cartoon / vector | 5 / 4 | 4 | 4 | 3-4 | 4-5 | 4-5 | Can look generic or like a mobile ad |
| Chibi / anime mobile | 4 / 4 | 5 | 5 | 4 | 3 | 3 (costumes) | Gacha connotations on Steam; high polish bar |
| Low-poly 3D | 3-4 / 4 | 4 | 4 | 3 | 3-4 | 5 | Can read as cheap (Megabonk review) |
| Stylized 3D | 4 / 4 | 5 | 5 | 5 | 1-2 | 5 | Largest skill burden: rigging, animation, VFX, shaders |
| 3D pre-rendered to 2D | 4 / 4 | 4 | 4 | 4-5 | 3-4 | 4 (layer renders) | Frame storage; layer-sorting artefacts |
| Cel-shaded 3D | 5 / 4 | 4 | 4 | 5 | 2-3 | 5 | Outline and shader tuning; 3D skill burden |
| Watercolor | 2 / 2 | 2-3 | 2-3 | 4 | 3 | 3 | Soft edges and washes fight clarity and rarity colour |
| Paper cut-out | 4 / 3 | 3-4 | 3 | 3 | 4 | 4 | Novelty fades; narrow material range across many zones |
| Silhouette | 4 / 2 | 3 | 1 | 2 | 5 | 1 | Suppresses colour, the main loot channel |

- **Two-layer readability.** Torchlight (painterly backgrounds, crisp characters), Diablo III (its layering system) and Hades (3D-rendered characters over hand-painted environments) separately reached the same solution. The playfield is a crisp, saturated, outlined foreground of actors, loot and effects over a lower-contrast background. This is the strongest transferable principle for LootFall's lane (battle y 0-420, ground at 386, per [CLAUDE.md](/home/user/lootfall/CLAUDE.md)), whatever the rendering technique.
- **Twelve rarities exceed what colour can carry.** The convention covers about six tiers (WoW grey to orange). LootFall's twelve will need extra channels that escalate together: colour, frame shape, glow or beam intensity, particles, sound, and chest-opening choreography (PoE's beams, sounds and font sizes are the model). Styles with real lighting (HD-2D, lit hi-bit pixel, 3D, pre-rendered 3D with normal maps) have the most headroom for the top tiers' light effects. Silhouette and watercolor have the least.
- **Icons must be built from parts.** 114,432 first finds is exactly 149 slots x 12 rarities x 64, so whatever the 64 is, the codex implies tens of thousands of distinct entries. No style can hand-author that many icons; a base shape per slot plus rarity frame and glow plus a variant tint or emblem is the only scalable route. Hard-edged styles (flat/vector, pixel with palette swaps, cel-shaded or 3D batch renders with material swaps) take those layers cleanly. Painterly icons look richer but take tints less cleanly unless they are designed with masks.
- **AI sets the cost order.**
  - Weakest: frame-by-frame animation (pixel or painted), because of frame-to-frame drift.
  - Strongest: single stills, such as painted backdrops, icons and concept art.
  - Rig-driven pipelines dodge the drift. One is AI-generated 3D meshes, auto-rigged and rendered to sprites (the Dead Cells/Hades/Diablo II route). The other is AI-generated painted or flat parts rigged with Spine or Godot cut-out bones (the Darkest Dungeon/AFK Arena route).
  - Low-poly and cel-shaded looks are also forgiving of the messy topology and textures AI meshes come with.
- **LootFall's prototype today.** It already uses CC0 pixel packs at 2x, small bosses at 3x, and a palette-swap shader that recolours the knight's blade and cape with the gear's rarity ([CLAUDE.md](/home/user/lootfall/CLAUDE.md)). That swap is a zero-cost visible-gear technique that carries into any style with flat colour regions (pixel, flat, cel-shaded, pre-rendered toon).
- **Candidate shortlist for clarity, satisfaction and a solo developer with AI.**
  - (a) Toon or cel-lit 3D characters and gear (AI-generated, auto-rigged), pre-rendered to single-facing 2D layers and composited per slot, over AI-painted parallax backdrops kept low in contrast.
  - (b) 2D cut-out characters (Spine skins) with flat-cartoon or ink-outlined painted parts.
  - (c) Staying with pixel art but moving to hi-bit with lighting is the costliest option per animated item.

### Gaps
- No primary statement from Jen Zee or Supergiant on readability in Hades was found; the readability claims rest on low-quality blogs.
- No art-direction source was found for Capybara Go or Legend of Mushroom (Deconstructor of Fun's [LoM analysis](https://deconstructoroffun.com/blog/2024/4/15/the-magic-of-legend-of-mushroom) could not be read), nor for Habby's or Lilith's pipelines (for example whether AFK Arena uses Spine).
- No source confirms that Kingdom Rush is vector art.
- No sourced examples were found of paper cut-out or watercolor in idle, loot or ARPG games.
- No controlled study of readability by art style was found; all readability evidence is developer intent, reviews or player threads.
- To verify: which HD-2D and 3D lighting effects (depth of field, volumetrics, screen-space effects) Godot 4's GL Compatibility renderer, which LootFall uses, supports. Not researched here.

## 2. Commercial success in idle/incremental and loot games, 2020-2026

### Takeaway
No single style wins commercially.

- **Biggest earners by far:** mobile idle RPGs in cute 2D or chibi styles. AFK Arena has more than $1.5B lifetime, Legend of Mushroom roughly $100M-270M, Capybara Go more than $109M within three months.
- **Steam's loot-roguelite breakouts span every style:** asset-pack pixel (Vampire Survivors, about 6M+), hand-drawn flat (Brotato, 10M+, platforms unspecified), pixel (Loop Hero, 1M+), pre-rendered retro (Halls of Torment, about 0.8M), low-poly PS1 (Megabonk, 1M in two weeks) and stylized 3D ARPG (Last Epoch, 2.1M+).
- **Pure idle/incremental games on Steam** are an order of magnitude smaller: Melvor 0.4-1.1M, IdleOn about 0.8M units. The exceptions are free desktop companions such as Bongo Cat, which peaked near 190K concurrent players.

### Cited Findings

#### Steam survivors-likes and loot roguelites
- **Vampire Survivors**
  - About 6.6M Steam copies and about $27.8M gross (Raijin, 22 Jul 2026, medium confidence). — [Raijin](https://raijin.gg/app/1794680/Vampire_Survivors)
  - About 6M and $20M per VG Insights and GameSensor. — [Levvvel](https://levvvel.com/vampire-survivors-statistics/)
  - An outlier estimate of up to 11M on Steam. — [TweakTown](https://www.tweaktown.com/news/108112/gamers-used-cpu-blocked-from-valorant-after-previous-owner-cheated/index.html)
  - Its 8th DLC, Legacy of the Bloodmoon, came out in September 2026. — [iPhoneSoft](https://iphonesoft.fr/2026/09/01/vampire-survivors-legacy-of-the-bloodmoon-8e-dlc)
- **Brotato**
  - Over 2M Steam copies at 1.0 (developer press release, June 2023). — [Game Developer press release](https://www.gamedeveloper.com/press-release/brotato-10-release)
  - Over 10M copies by 2025, platforms unspecified. — [Wikipedia](https://en.wikipedia.org/wiki/Brotato)
  - About 3M on Steam and $12.6M (Raijin, Jul 2026). — [Raijin](https://raijin.gg/app/1942280/Brotato)
- **Halls of Torment**
  - Over 70K copies in its first week (May 2023, developer). — [GamingOnLinux](https://www.gamingonlinux.com/2023/06/halls-of-torment-hits-over-70k-sales-in-the-first-week/page=1/)
  - About 825.6K on Steam and $4.7M (Raijin, 22 Jul 2026). — [Raijin](https://raijin.gg/app/2218750/Halls_of_Torment)
- **Megabonk**
  - 1M copies in about two weeks (developer, 2 Oct 2025); VG Insights estimates about 1.3M; peak of 117,336 concurrent players on 5 Oct 2025. — [Game World Observer](https://gameworldobserver.com/2025/10/03/megabonk-is-a-new-indie-hit-on-steam-the-games-sales-have-surpassed-one-million-copies); [Notebookcheck](https://www.notebookcheck.net/Vampire-Survivors-esque-indie-Reverse-Bullet-Hell-Game-Megabonk-Sells-1-Million-Copies-in-Just-Two-Weeks.1132354.0.html)
  - That peak beat Hades II's all-time record. — [Notebookcheck](https://www.notebookcheck.net/Megabonk-beats-Hades-2-s-all-time-record-as-it-morphs-into-Steam-s-new-indie-sensation.1136297.0.html)
  - A newsletter headline put its revenue at about $8M. — [Game Dev Report](https://gamedevreport.beehiiv.com/p/can-you-make-megabonk-8mil-or-do-players-like-pixel-art)
- **Ball x Pit**
  - 100K copies in 24 hours, 400K in the first week, 1M by early December 2025, and 2M within ten months (developer and publisher, Aug 2026). — [Push Square](https://www.pushsquare.com/news/2026/08/devolvers-brilliant-roguelite-dopamine-factory-ball-x-pit-is-a-smash-hit-at-2-million-sales); [Final Weapon](https://finalweapon.net/2025/12/02/ball-x-pit-1-million-copies-sold-new-updates-2026/)
  - Devolver had expected 300K in its first year. — [GoNintendo](https://gonintendo.com/contents/56013-ball-x-pit-more-than-tripled-devolver-digital-s-full-year-sales-expectations-in-just)
  - Gamalytic estimated about $12M from Steam before the 2M milestone. The search summary didn't say which of these articles carried the estimate. — [Push Square](https://www.pushsquare.com/news/2026/08/devolvers-brilliant-roguelite-dopamine-factory-ball-x-pit-is-a-smash-hit-at-2-million-sales); [WN Hub](https://wnhub.io/news/investment/item-51689)
- **Soulstone Survivors**
  - Over 1M copies since Early Access (press release, Apr 2025). — [Gamespress](https://gamespress.com/Million-selling-Soulstone-Survivors-coming-to-Consoles-in-2025-Demo-Ou)
  - Raijin estimates about 691K on Steam and $6.2M. — [Raijin](https://raijin.gg/app/2066020/Soulstone_Survivors)
- **Death Must Die**: about 563K on Steam and $3.3M; 90% positive across about 21,653 reviews (Raijin, 28 Jul 2026). — [Raijin](https://raijin.gg/app/2334730/Death_Must_Die)
- **Loop Hero** (pixel art)
  - Over 500K copies in its first week (March 2021). — [RPG Site](https://www.rpgsite.net/news/10867-loop-hero-sells-500-000-copies-in-first-week-of-sale)
  - Over 1M on PC by December 2021. — [RPG Site](https://www.rpgsite.net/news/12088-loop-hero-now-available-for-nintendo-switch-pc-version-hits-1-million-sold)
  - The second 500K took nine months. — [WN Hub](https://wnhub.io/news/stores-and-publishing/item-19587)
- **Backpack Battles**: 640K copies by 8 Apr 2024, one month into Early Access; China 48%, Japan 11%, US 10%. — [GameDiscoverCo](https://newsletter.gamediscover.co/p/how-backpack-battles-sold-650k-copies); [Game World Observer](https://gameworldobserver.com/2024/04/25/backpack-battles-sales-640k-copies-china-top-country)

#### ARPGs
- **Last Epoch**
  - 2,135,257 copies and a 264,708 concurrent-player peak as of 22 Mar 2024. — [Wikipedia](https://en.wikipedia.org/wiki/Last_Epoch)
  - About 3.2M and $66.3M (Raijin, Jul 2026). — [Raijin](https://raijin.gg/app/899770/Last_Epoch)
  - 1M copies a month before its February 2024 1.0 launch. — [PCGamesN](https://www.pcgamesn.com/last-epoch/sales)
- **Torchlight Infinite** (free-to-play, PC and mobile)
  - About 638K units estimated. — [SteamPulse](https://edge-test.steampulse.org/game/1974050)
  - A Steam peak of 23,715 around 17 Jan 2026. — [Automaton](https://automaton-media.com/articles/newsjp/torchlight-20260119-408441/)
  - About 2,448 players in August 2026. — [SteamPulse](https://edge-test.steampulse.org/game/1974050)
  - Over 60% of revenue comes from outside China. — [iTiger](https://www.itiger.com/news/1148023161)
  - Maxroll dropped its support over monetization disputes. — [Maxroll](https://maxroll.gg/torchlight-infinite)

#### Pure idle/incremental on Steam
- **Melvor Idle**
  - About 424K Steam copies and $3.6M (Raijin, Jul 2026), against about 1.11M and $11.1M (Vaporlens, Dec 2025). — [Raijin](https://raijin.gg/app/1267910/Melvor_Idle); [Vaporlens](https://vaporlens.app/app/1267910/melvor_idle/stats/details)
  - Over 1M players across Steam, the App Store and Google Play at 1.0 (Jagex, 2021). — [Jagex](https://www.jagex.com/news/melvor-idle-version-1-0-launches-on-pc-and-mobile)
- **IdleOn** (free-to-play)
  - About 843K units estimated. — [SteamPulse](https://steampulse.org/game/1476970)
  - SteamSpy's developer total is 1.939M owners. — [SteamSpy](https://steamspy.com/dev/Lavaflame2)
  - About 8-14K concurrent players, with an all-time peak around 21K. — [Raijin](https://raijin.gg/app/1476970/IdleOn_The_Incremental_MMO)
- **Idle Champions** (free-to-play)
  - SteamSpy lists 1,000,000 owners, a lower bound; PlayTracker estimates about 2.9M players (low reliability). — [SteamSpy](https://www.steamspy.com/app/627690); [PlayTracker](https://playtracker.net/insight/game/6863)
  - About 1.8-3K concurrent players in mid-2026, down from about 2.7K to 2.1K on monthly average over the year. — [Steambase](https://steambase.io/apps/627690); [live-player-count](https://live-player-count.com/game/idle-champions-of-the-forgotten-realms)
- **Rusty's Retirement** (solo developer; an idle farm that sits as a bar at the bottom of the desktop; $7)
  - 100K copies in five days (April-May 2024). — [GamenGuide](https://www.gamenguide.com/articles/105189/20240502/rustys-retirement-idle-farming-simulator-sells-100k-copies-in-5-days.htm)
  - About 330K on Steam by late 2024; about $5 net per copy; a $4 DLC with an 11% attach rate earned $111K; China 26%. — [GameDiscoverCo](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed); [Automaton](https://automaton-media.com/articles/newsjp/rustys-retirement-20240828-308175/)
  - Wikipedia says over 500K. — [Wikipedia](https://en.wikipedia.org/wiki/Rusty%27s_Retirement)
- **Bongo Cat** (a free idle clicker that drums on the taskbar)
  - About 130K concurrent players soon after its Steam release. — [Insider Gaming](https://insider-gaming.com/what-is-bongo-cat-on-steam/)
  - All-time peak of 194,508 per tracker.gg, or 187,909 on 15 Jul 2026 per SteamDB. — [tracker.gg](https://tracker.gg/population/steam/3419430); [SteamDB](https://steamdb.com/de/tools/steam-charts/3419430-bongo-cat)
  - It counts clicks and keystrokes and drums on the taskbar. — [GamePro](https://www.gamepro.de/artikel/160000-spieler-steam-katze-glueck-geld-verdienen,3431840.html)
  - "On its way to 6 million players". — [GamesMarket](https://www.gamesmarket.global/bongo-cat-is-on-its-way-to-6-million-players)

#### Mobile idle
- **AFK Arena** (2D; see Q1 for its style)
  - Over $1.5B in lifetime player spending (AppMagic, April 2026, seventh anniversary). — [PocketGamer.biz](https://www.pocketgamer.biz/afk-arena-celebrates-seventh-anniversary-and-over-15bn-in-lifetime-earnings)
  - $12.1M in its first month and $469.8M in its first year; China 32%, US 24%. — [Udonis](https://blog.udonis.co/mobile-marketing/mobile-games/afk-arena-analysis); [PocketGamer.biz](https://www.pocketgamer.biz/afk-arena-celebrates-seventh-anniversary-and-over-15bn-in-lifetime-earnings)
- **AFK Journey**: over $110M (date unclear). — [MAF](https://maf.ad/en/blog/afk-journey-analysis/)
- **Legend of Mushroom**
  - Over $8.5M in its first week in Japan, and over $100M worldwide by March 2024 (Sensor Tower). — [Automaton](https://automaton-media.com/en/news/legend-of-mushrooms-massive-japanese-release-makes-8-5-million-in-one-week/)
  - $100M in Japan within four months, Japan being about 45% of revenue. — [Sensor Tower JP](https://sensortower.com/ja/blog/legend-of-mushroom-achieved-100m-dollars-in-japan)
  - About $270M since November 2023 (cutoff date unclear). — [WN Hub](https://wnhub.io/news/finance/item-43484)
  - Over 30M downloads. — [PocketGamer](https://www.pocketgamer.com/legend-of-mushroom/guide/)
- **Capybara Go** (soft launch August 2024; global November 2024)
  - $40M from outside China by 11 Dec 2024 (Sensor Tower). — [TechNode](https://technode.com/2024/12/19/capybara-go-takes-40-million-in-overseas-revenue-within-two-months-of-launch/)
  - $109M lifetime by 10 Feb 2025 (AppMagic); South Korea 28% ($30.1M), US $16.1M. — [PocketGamer.biz](https://www.pocketgamer.biz/habbys-capybara-go-surpasses-100m-in-gross-player-spending)
  - $33.3M in December 2024 and $32.1M in January 2025 (a vendor case study). — [ThinkingData](https://thinkingdata.io/?p=3488)

#### Long-tail references (launched before 2020, still selling)
- **Dead Cells** (3D rendered to pixel art): over 10M copies by 5 Jun 2023, six years after Early Access, with 34 free updates and 4 paid DLCs. — [Gematsu](https://www.gematsu.com/2023/06/dead-cells-sales-top-10-million-further-updates-planned-through-2025); [Gamespress](https://gamespress.com/Dead-Cells-Has-Sold-More-Than-10-Million-Copies)
- **Darkest Dungeon** (ink)
  - 1M copies in 2016, 94% on Steam. — [DualShockers](https://www.dualshockers.com/darkest-dungeon-surpasses-one-million-copies-sold-94-percent-came-steam/)
  - 6M units by December 2022. — [Game World Observer](https://gameworldobserver.com/2022/12/26/darkest-dungeon-sales-6-million-copies-red-hook)
  - Darkest Dungeon II: over 600K. — [Game World Observer](https://gameworldobserver.com/2023/06/08/darkest-dungeon-ii-sales-600k-copies-steam-launch)
- **Hades**: 1M copies by September 2020, about 700K of them in Early Access. — [GameSpot](https://gamespot.com/articles/hades-has-sold-1-million-copies-nearly-one-third-sold-in-last-few-days/1100-6482388/)

### Inferences
- **Art polish and sales don't line up.** Vampire Survivors began on asset-pack sprites, and Megabonk's look nearly put a reviewer off buying, yet both are top sellers. Halls of Torment's deliberately low-fi look sold about 0.8M. On Steam, art works as a hook and a clarity tool more than a fidelity contest; the Game Dev Report's "the hard part is design" fits this.
- **The mobile idle revenue leaders are cute, character-centric 2D or chibi titles with heavy live-ops presentation.** That polish bar (gacha-style rarity reveals, costume skins) is the benchmark for "satisfying loot". On Steam, though, a full gacha look may signal pay-to-win to PC players. This is inferred from the monetization backlash around IdleOn and Torchlight Infinite, not from any study of art style.
- **East Asia is a large share of these hits:** China 48% for Backpack Battles and 26% for Rusty's, Japan 45% for Legend of Mushroom, South Korea 28% for Capybara Go. Cute and anime-adjacent presentation may matter more there. This is weak inference.
- **The idle niche specifically rewards ambient, low-distraction visuals** that can live on the desktop (Rusty's bottom-of-screen bar, Bongo Cat on the taskbar). LootFall could consider a compact "idle view" alongside the full battle lane.

### Gaps
- The art styles of several games were not confirmed by the retrieved sources. From general knowledge, unverified here:
  - Ball x Pit: pixel art.
  - Backpack Battles: hand-drawn cartoon 2D.
  - Soulstone Survivors, Last Epoch, Torchlight Infinite: stylized 3D.
  - Idle Champions: 2D cartoon portraits.
  - Melvor Idle: icon and UI-driven, with no animated battle.
  - Capybara Go and Bongo Cat: flat cute cartoon.
  - Rusty's Retirement: pixel art.
  - AFK Journey: 3D stylized.
  - Death Must Die: 2D dark fantasy (least certain).
- Not found: revenue for Archero, Idle Heroes, Tap Titans 2, The Tower ([App Store listing](https://apps.apple.com/app/1575590830) only) and Cookie Clicker on Steam.
- No source systematically links art style to commercial outcome; the inferences above rest on a hand-picked list of hits, which is survivorship bias.

## 3. Ageing well and avoiding visual fatigue over thousands of hours

### Takeaway
The claim that stylization outlasts realism is mostly opinion (Ken Levine; WoW and Diablo III as examples). It is backed by stylized games' long tails: Dead Cells reached 10M+ over six years of updates, Darkest Dungeon went from 1M to 6M, and Diablo II's pre-rendered look is still being imitated more than 20 years later. Over thousands of hours, the evidence points to two bigger risks than dated technology: late-game clutter and sameness. The defences seen are effects-opacity options, filter-like control over loot noise, restrained background palettes, and pipelines that make new zones and monsters cheap.

### Cited Findings
- Ken Levine argues that chasing realism raises costs and dates a game faster, that strong art direction lasts longer, and that realism has "diminishing returns". — [Digital Citizen](https://www.digitalcitizen.life/ken-levine-says-stylized-games-age-better-than-realism/); [Gfinity](https://www.gfinityesports.com/article/bioshock-creator-says-the-gaming-industry-is-too-obsessed-with-ultra-realistic-graphics-that-dont-age-well)
- Analysts argue stylized art doesn't compete with improving technology and so avoids its ageing curve; World of Warcraft is the usual example. — [N-iX](https://gamestudio.n-ix.com/?p=12287); [Digital Citizen](https://www.digitalcitizen.life/ken-levine-says-stylized-games-age-better-than-realism/)
- Counterpoint: a forum commenter says many early-2000s "realistic" games still look good and the "age poorly" claim is overstated (anecdotal). — [RPGHQ](https://rpghq.org/forums/viewtopic.php?p=393574)
- Lichtner expected Diablo III's painterly textures to help future-proof the game. — [Game Developer](https://gamedeveloper.com/design/gdc-2012-diablo-iii-s-art-director-shows-off-design-process). Torchlight kept its art "pretty low-tech" to run on virtually any system. — [Gaming Nexus](https://www.gamingnexus.com/Article/2452/Torchlight-Interview)
- HD-2D's makers worried pixel art might look dated and added 3D, lighting and depth to make it appeal to modern players. At large screen sizes the sprites looked sparse until tile and colour variety went up. — [Wikipedia: HD-2D](https://en.wikipedia.org/wiki/HD-2D); [Siliconera](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)
- Darkest Dungeon's palette rules (warm tones, no "dead gray" or black outside the inks) give a consistent parchment feel across the game. — [GameSpot](https://www.gamespot.com:443/articles/the-gothic-sensibilities-of-darkest-dungeon/1100-6424880/)
- Long tails in stylized games:
  - Dead Cells: over 10M copies six years after Early Access, with 34 free updates and 4 DLCs. — [Gematsu](https://www.gematsu.com/2023/06/dead-cells-sales-top-10-million-further-updates-planned-through-2025)
  - Darkest Dungeon: 1M in 2016 to 6M in 2022. — [DualShockers](https://www.dualshockers.com/darkest-dungeon-surpasses-one-million-copies-sold-94-percent-came-steam/); [Game World Observer](https://gameworldobserver.com/2022/12/26/darkest-dungeon-sales-6-million-copies-red-hook)
  - Vampire Survivors: its 8th DLC in 2026. — [iPhoneSoft](https://iphonesoft.fr/2026/09/01/vampire-survivors-legacy-of-the-bloodmoon-8e-dlc)
  - Halls of Torment's developers deliberately emulated Diablo I and II's late-90s pre-rendered look. — [FullCleared](https://fullcleared.com/features/inside-halls-of-torment-an-interview-with-chasing-carrots/)
- Late-game clutter is the documented fatigue problem.
  - Vampire Survivors players say weapon effects hide the screen and ask for an effects slider; a mod removes particles. — [Steam discussion](https://steamcommunity.com/app/1794680/discussions/0/4631482569784862180); [Attack of the Fanboy](https://attackofthefanboy.com/guides/best-vampire-survivors-mods/)
  - Halls of Torment players ask for opacity sliders like those in Soulstone Survivors and Bio Prototype. — [Steam discussion](https://steamcommunity.com/app/2218750/discussions/0/3801652929323026410)
- Loot noise grows with play time. Path of Exile players are told to "start loose... then increase strictness once the game starts dropping more items than you want to see", using Normal, Semi-Strict and Strict filters. — [Bamboo Gaming](https://www.bamboogaming.net/poe2/loot-filters)
- Idle hits designed for long, ambient sessions keep their visuals small and out of the way: Rusty's Retirement runs in a bar along the bottom of the desktop, and Bongo Cat drums on the taskbar. — [GameDiscoverCo](https://newsletter.gamediscover.co/p/how-rustys-retirement-idle-farmed); [Automaton](https://automaton-media.com/articles/newsjp/rustys-retirement-20240828-308175/); [GamePro](https://www.gamepro.de/artikel/160000-spieler-steam-katze-glueck-geld-verdienen,3431840.html)
- Megabonk's reviewer said its graphics nearly put them off buying but stopped mattering once playing; the first impression and the long-run experience differ. — [Mezha](https://mezha.ua/en/reviews/megabonk-review-305493/)

### Inferences
- **For thousands of hours, the loot language must not wear out.** Save the loudest signals (beam, unique sound, screen flash, slow-motion opening) for the top few of the twelve tiers. Let low tiers fade, auto-collect or be filtered. Make effect intensity a player setting; LootFall already scales Shake, Flash and HitStop through a Screen Shake setting ([CLAUDE.md](/home/user/lootfall/CLAUDE.md)).
- **Restrained backgrounds reduce fatigue and keep loot colours meaningful.** Following the two-layer principle, desaturated, lower-contrast backdrops sit under saturated actors and loot. Darkest Dungeon's palette rules show a written style rule holding a whole game together.
- **Zone variety is cheapest where mood comes from lighting and palette:** HD-2D, 3D, lit hi-bit pixel art, palette-shifted pixel art and pre-rendered 3D re-lit per biome. Hand-painted per-zone backdrops cost more per zone. Painted stills are also what AI image generation does best, so with AI the painted-backdrop route becomes cheap. LootFall's current 640x210 backdrop bands fit that.
- **Monster variety:** palette swaps and scale variants (LootFall's 3x stage bosses) are the classic cheap multipliers. Re-texturing a rigged 3D base is the 3D equivalent. Frame-by-frame AI animation is the costliest per new monster.
- **Retro styles don't date further.** Pixel, PS1 low-poly and 90s pre-rendered looks are already period styles, but they are trend-sensitive in marketing; Halls of Torment called its look a risk.

### Gaps
- No study or postmortem was found that measures visual fatigue by art style over long or idle sessions.
- Whether Vampire Survivors added an effects-opacity option after November 2024 was not confirmed.
- No idle-game developer interview on keeping zones and monsters fresh over hundreds of biomes was found.

## 4. Showing a hero whose look reflects many pieces of equipment

### Takeaway
Visible equipment is always built by compositing parts, but the cost per new item depends on the pipeline:

- **3D and 2D skeletal (Spine skins):** each item is one asset, and the rig animates it for free.
- **Frame-based 2D (hand-drawn or pre-rendered):** each item needs a layer for every animation frame and every facing. Diablo II used component layers; Hades needs 104 frames x 32 angles for a single attack.

No game shows anything like 149 slots on the body. The practical pattern is a few silhouette-defining slots drawn on the hero, with auras, companions and effects standing in for the rest. Mobile idle games often swap gear visuals for gacha costumes.

### Cited Findings
- **Diablo II** builds characters from component layers. — [Daum cafe (Korean modding forum)](https://m.cafe.daum.net/rpgguild/ISk/30); [OpenDiablo2 (GitHub)](https://WWW.GITHUB.COM/OpenDiablo2); [GameDev.net](https://gamedev.net/forums/topic/556056-how-to-design-diablo-style-sprites/)
  - Head (HD), torso (TR), legs (LG), right and left arms (RA, LA), right and left hands (RH, LH), shield (SH), and special layers S1-S8 such as shoulders.
  - Each armour component comes in light, medium or heavy versions, picked per item.
  - A COF file is "a composition of files that form an animation".
  - Each armour configuration changed the player's look, with "no armor" treated as a form of armour.
- **Drawing every combination doesn't scale.** In one example, 10 weapons and 30 armour pieces give 341 combinations; the fix is to draw each piece separately and let the engine assemble them. — [HimeWorks](https://himeworks.com/?p=4051)
  - A common method: a base sprite with each piece layered on top at a set position. — [Solarus forum](https://forum.solarus-games.org/en/index.php/topic,1324.0.html)
  - A typical layer stack: helmet, hair, head, shoulders, sleeves, pants/boots, breastplate, torso/legs. — [Unity Discussions](https://discussions.unity.com/t/architectural-help-for-2d-sprite-layered-animations-re-customizable-characters/180183)
- **Spine (3.8+)** lets developers set up appearance and equipment items as separate skins and combine them at runtime. — [Spine Runtime Skins](https://en.esotericsoftware.com/spine-runtime-skins); [spine-unity mix-and-match](https://en.esotericsoftware.com/spine-unity-mix-and-match)
  - Its mix-and-match example lets you combine individual equipment and appearance pieces.
  - One item can span several attachments, such as a shirt made of a torso piece and two sleeves.
  - Only one skin can be active, so the outfit is built as a new skin from the pieces.
- A Godot user swapping Spine skins had to call `set_slots_to_setup_pose()` after `set_skin` before the change showed. — [Spine forum: Godot and skin swapping](https://esotericsoftware.com/forum/d/18329). Memory use with skins in separate atlases was raised without an answer. — [Defold forum](https://forum.defold.com/t/spine-skins-multiple-atlases/57888)
- **Darkest Dungeon (cut-out):** modders cannot expand a body part's dimensions in the idle, fight or walk sprite sheets, which makes additions like helmet horns hard. — [Steam discussion](https://steamcommunity.com/app/262060/discussions/4/1727575977545379683)
- **Hades (pre-rendered):** every frame of Zagreus is a unique image (for example a 104-frame bow attack at 32 angles). The developer said the per-frame approach is beyond what the small team can handle without big sacrifices elsewhere. The summary doesn't show what change the thread was asking about. — [Steam developer posts](https://steamcommunity.com/app/1145360/discussions/0/1738883810796606862)
- **Dead Cells:** because its art comes from 3D, animations can be changed without redrawing sequences. — [80.lv](https://80.lv/articles/interview-with-the-developers-of-dead-cells)
- **Sea of Stars:** every asset must meet the dynamic lighting's technical needs, which adds per-asset work for every visible item. — [MobileSyrup](https://mobilesyrup.com/2023/09/11/sea-of-stars-level-design-sabotage-studio-thierry-boulanger-interview/)
- **Legend of Mushroom:** appearance and equipment both come through gacha, and players customise their mushroom. — [Logrus IT](https://games.logrusit.com/en/portfolio/legend-of-mushroom/); [PocketGamer](https://www.pocketgamer.com/legend-of-mushroom/guide/)
- **IdleOn:** players complained that cosmetics were "not visible on character". The thread is informal and may be outdated, but it shows players expect bought or earned items to show. — [Steam discussion](https://steamcommunity.com/app/1476970/discussions/0/3073118388428222324)

### Inferences
- **Approximate art cost per visible item, by pipeline (my estimate):**
  - Real-time 3D: one mesh and texture; animation and lighting are free; a toon shader can hide AI-mesh flaws.
  - 2D cut-out (Spine skins or Godot bones): one to three images per item; animation is free. The limit is that items must fit the rig's parts (Darkest Dungeon's horn problem), so bulky silhouette changes need their own attachments or bones.
  - 3D pre-rendered to 2D layers: one mesh per item, rendered automatically into frames x facings. Storage grows with layers x frames, and layer-sorting artefacts appear at joints.
  - Hand-drawn frame-based (pixel or painted): frames x facings drawn by hand for every item. This is the worst case, and AI frame drift makes it worse.
  - Palette or tint swap: close to zero per rarity. LootFall's blade and cape swap already does this.
- **LootFall's fixed side view is a large saving.** The knight stands at x 300 and faces one way, so the 32-angle multiplier in Hades' pipeline drops to one. That makes the pre-rendered-layer and cut-out approaches much cheaper than in the isometric games they come from.
- **Visible gear for 149 slots is a priority list, not a body map.** For example:
  - Draw about 8-12 silhouette slots on the hero: main hand, off-hand, helmet, chest, shoulders, cape, gloves, boots, perhaps a belt.
  - Show rings, toe rings and tattoos as aggregate effects, such as glow intensity, small trails or a ring of orbiting glints, with rarity colour from the best piece.
  - Make companions separate actors.
  - Show celestial slots as halo or background effects.
  - Carry the rest on the paper-doll screen, where icons do the work.
- **Mobile-style costume skins are cheaper but less faithful to the loot.** They break the "my gear shows up in the fight" promise that LootFall's design statement makes.

### Gaps
- No source was retrieved on how Last Epoch, Torchlight Infinite or Diablo IV build armour visuals; the swap-mesh approach is assumed standard.
- That Brotato's weapons show around the potato was not sourced.
- Darkest Dungeon II's reported move to 3D characters was mentioned in a summary without a URL.
- Not researched: Godot's native cut-out tools (Skeleton2D) against the Spine runtime for skins, and how many Spine skeletons Godot can run at once.
- The Diablo II component mapping comes from one Korean modding post; cross-check it with D2 modding documentation.

## 5. What art directors and developers say about choosing a style for readability and scope

### Takeaway
Two pieces of advice recur.

- **Readability:** subordinate art to gameplay. Blizzard, Runic and Supergiant each separate a crisp, strongly silhouetted foreground from a painterly background, with bold colour.
- **Scope:** pick a pipeline the team can sustain. Bourassa chose a camera that cut his workload and wrote rules so he could art-direct himself. Vasseur turned to 3D-rendered pixel art rather than redraw every retake. Supergiant calls its per-frame pipeline beyond the team for extra variants. Square Enix says HD-2D is dearer than it looks. Sabotage made every asset answer to its lighting system.

### Cited Findings
- **Blizzard, Diablo III (Christian Lichtner, GDC 2012).** — [Game Developer](https://gamedeveloper.com/design/gdc-2012-diablo-iii-s-art-director-shows-off-design-process); [GDC](https://gdconf.com/news/gdc_2012_debuts_diablo_iii_jet); [80.lv](https://80.lv/articles/blizzard-art-philosophy-in-diablo-iii/); [CG Channel](https://www.cgchannel.com/?p=35120)
  - Art decisions were subordinated to gameplay, aiming for "a clear and well-composed gameplay gestalt" through a layering system.
  - Principles: strong silhouettes, bold colour, dynamic animation; a painterly world that "takes full advantage of its fixed camera".
  - Fans called the early screenshots "saccharine".
  - The talk is a one-hour video on GDC Vault.
- **Runic, Torchlight (Jason Beck):** painterly backgrounds with crisp, comic-book characters on top, from classic animation; low-tech by design so it runs on virtually any system; a lighter tone to be inviting. — [Engadget, 2009](https://www.engadget.com/2009-09-10-an-insight-into-the-art-of-torchlight.html); [Gaming Nexus](https://www.gamingnexus.com/Article/2452/Torchlight-Interview)
- **Red Hook, Darkest Dungeon (Chris Bourassa).** — [80.lv](https://80.lv/articles/red-hook-studios-talks-about-the-creation-of-darkest-dungeon); [GameSpot](https://www.gamespot.com:443/articles/the-gothic-sensibilities-of-darkest-dungeon/1100-6424880/); [Game Developer, Road to the IGF](https://www.gamedeveloper.com/audio/road-to-the-igf-red-hook-studios-i-darkest-dungeon-i-); [Void Signal podcast](https://voidsignal.buzzsprout.com/1744704/episodes/17754731-chris-bourassa-of-red-hook-studios)
  - As the lone artist, he needed something he could make alone.
  - The side-on view won partly because a top-down view switching to side-on for combat "would significantly increase his workload".
  - A style with "externalized rules" let him art-direct himself without constant self-doubt.
  - Cuteness was added to raise the characters' iconic quality.
- **Motion Twin, Dead Cells (Thomas Vasseur):** "To make up for the lack of bandwidth and still deliver on quality, we had to find a pipeline that could give us great looking pixel art, without having to hand draw each and every retake." Models and animation were done "by one guy". — [WN Hub](https://wnhub.io/news/art/item-13775); [80.lv](https://80.lv/articles/interview-with-the-developers-of-dead-cells)
- **Supergiant, Hades.** — [Steam developer posts](https://steamcommunity.com/app/1145360/discussions/0/1864993755183591660); [Steam developer posts](https://steamcommunity.com/app/1145360/discussions/0/1738883810796606862)
  - A proprietary engine and tools give "its crisp, responsive feel and graphic novel look".
  - Characters are 3D-rendered into thousands of frames; environments are hand-painted.
  - The per-frame approach is beyond the small team "without big sacrifices elsewhere".
- **Square Enix, HD-2D.** — [Wikipedia: HD-2D](https://en.wikipedia.org/wiki/HD-2D); [Siliconera](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)
  - The team had to invent game concepts to fit the new look instead of choosing an existing style (assistant producer Masaaki Hayasaka).
  - Asano says the style is "more expensive than people realize".
  - Prototypes swung between flat and over-saturated.
- **Sabotage, Sea of Stars:** "all of the assets that we make have to answer to the technical needs of the dynamic lighting". — [MobileSyrup](https://mobilesyrup.com/2023/09/11/sea-of-stars-level-design-sabotage-studio-thierry-boulanger-interview/)
- **Chasing Carrots, Halls of Torment:** emulating Diablo I/II's pre-rendered look set the game apart, and was "the biggest creative risk" because younger players might not know those games. — [FullCleared](https://fullcleared.com/features/inside-halls-of-torment-an-interview-with-chasing-carrots/)
- **Passtech, Ravenswatch:** "with the world of Reverie where tales and legends coexist, it just felt obvious to embrace an illustrative art style". — [Passtech](https://www.passtechgames.com/ravenswatch-news/ravenswatch-artstation-blast/)
- **Nintendo, Wind Waker:** cel shading made puzzle mechanisms "more easy-to-understand". — [Wikipedia: Cel shading](https://wikipedia.com/wiki/Cel_shaded)
- **Ironhide, Kingdom Rush:** keeps "the classic cartoonish style fans love but with a lot more details and animations"; uses Adobe Animate, Cocos2d-x and in-house tools. — [WN Hub](https://wnhub.io/news/other/item-14983); [Game World Observer](https://gameworldobserver.com/?p=888)
- **Lilith, AFK Arena:** spent a long time looking for a style that would appeal to all players, drawing on Western mural painting and cathedral design (translated summary). — [ActuGaming](https://www.actugaming.net/?p=294547)
- **Blobfish, Brotato:** "glued together by a hand drawn artstyle and a potato theme". — [Brotato 1.0 press release](https://www.gamedeveloper.com/press-release/brotato-10-release)
- **Poncle, Vampire Survivors:** started with asset-pack sprites, then hired artists to redo the ones that drew criticism. — [Inverse](https://www.inverse.com/gaming/vampire-survivors-castlevania-nintendo-switch-stealing-claims-dlc)
- **Megabonk**, per press: its style is "partly a cost-saving choice", and the difficulty lies in design, not visuals. — [Mezha](https://mezha.ua/en/reviews/megabonk-review-305493/); [Game Dev Report](https://gamedevreport.beehiiv.com/p/can-you-make-megabonk-8mil-or-do-players-like-pixel-art)
- **Solo-developer forums:** 2D sprites are easier to learn but slower to produce. — [GameDev.net, 2017](https://gamedev.net/forums/topic/694163-what-is-cheaper-pixel-art-or-low-poly). Low poly is not much more efficient. — [Polycount](https://polycount.com/discussion/comment/2670927). Match the style to what you can realistically produce and avoid realism. — [Pixune](https://pixune.com/blog/art-styles-in-indie-games-vs-aaa-games/)
- **Incremental-game players (anecdotal, itch.io and Reddit).**
  - "Love incrementals but they rarely look this good"; a game's ASCII look was praised. — [itch.io comments](https://jorbits.itch.io/exponential-locomotion/comments)
  - One critic would "never click a steampage with this art" if they didn't already know the developer. — [itch.io post](https://itch.io/post/10725747)
  - A developer noted that incremental players enjoy watching numbers grow. — [itch.io post](https://itch.io/post/16017562)
  - An r/incremental_games Feedback Friday thread shows developers aiming for a distinctive style. — [r/incremental_games mirror](https://redlib.hackliberty.org/r/incremental_games/comments/1peme3p/feedback_friday)

### Inferences
- **The two-layer principle is the most consistent readability advice across studios:** Blizzard, Runic and Supergiant each arrived at a crisp foreground over painterly backgrounds. It is independent of rendering technique and should be a rule in any style LootFall picks.
- **A written, rules-based style guide matters more with AI generation.** Bourassa's "externalized rules" are what let a lone artist stay consistent. With AI, rules on palette, outline weight, light direction, proportions and value ranges for foreground and background become the prompt and post-processing spec that keeps thousands of generated assets coherent.
- **Choosing a camera to save work has precedents.** LootFall's fixed side-view lane is the same kind of decision as Darkest Dungeon's side-on view. It favours pipelines whose cost scales with the number of facings (pre-rendered layers, cut-out), because LootFall needs only one.
- **Fidelity matters less than clarity and a hook.** Developers who scoped low-fi on purpose (Halls of Torment, Megabonk, Vampire Survivors) still sold hundreds of thousands to millions. For an idle loot game, clarity of what happened and the satisfaction of the reveal count for more than rendering fidelity.

### Gaps
- No GDC talk or postmortem on art direction for an idle or incremental game was found. The Game Developer interview on The Gnorp Apologue ([link](https://gamedeveloper.com/design/interview-the-gnorp-apologue)) and a thesis on pixel art in incremental games ([VDU](https://www.vdu.lt/cris/entities/etd/1429e864-7123-42e0-995f-d16245e8ec01)) were seen but could not be read.
- No Habby (Archero, Capybara Go) or Lilith pipeline talk was found.
- No Jen Zee primary statement on readability was found.
- Vasseur's original article and the 80.lv Red Hook interview could not be fetched; their quotes come from search summaries.
