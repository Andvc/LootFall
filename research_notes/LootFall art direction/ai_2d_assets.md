# AI image and sprite generation for 2D game assets: capabilities, limits, failure modes and workflows (as of October 2026)

> **How to read the citations.** From this environment only github.com pages could be fetched; every other site failed
> DNS lookup. A citation marked **(fetched)** is a page I read myself. Every other claim rests on a web-search result
> summary of the linked page, not on the page itself. **(vendor)** marks a source that sells the tool or model it
> describes. Dates are the page's publication date where the summary or URL showed one; "undated" means none was visible.
> Anything dated before 2025 is flagged as possibly outdated. The web-search budget shared by all researchers ran out
> near the end, so a few late leads could not be checked (listed under Gaps in section 1).
>
> **LootFall context checked in the repo (for the inferences):** item icons are pixel sprites about 20 to 55 px in source
> (`assets/sprites/items/*.png`), scaled by whole numbers into a 64 px box in the gear panel and an 80 px box on the
> reveal card (`UiKit.MakeItemIcon`, `GearPanel.cs`, `RevealCard.cs`). Rarity is a glow drawn in code for Rare and up.
> Stat icons are 24 to 32 px. Monsters already come in reskinned variants (`bat_ember`, `boar_tusk`, `dragon_ash`,
> `ghost_shade`, `giant_ember`, `mimic_gilded`). The knight's palette swap shows the gear's rarity colours.

## 1. Leading models and platforms, and how good they are for each 2D asset type

### Takeaway
By October 2026 the frontier general models (OpenAI gpt-image-2, Google's Nano Banana Pro and Nano Banana 2, FLUX.2,
Midjourney V8, Seedream 5.0) produce near-professional single illustrations and can keep a character or object
recognisable across several reference images. Game-focused platforms (Scenario, Layer, Ludo, PixelLab, Retro Diffusion)
wrap these models with custom styles, sheets and engine exports. Static single images (portraits, monster
illustrations, painted backgrounds, single icons) are AI's strong ground. Anything with geometric or temporal
constraints (9-slice frames, flipbook VFX, animation) still needs substantial manual or procedural work. Transparency
(alpha) support differs between models and changed during 2026, which matters a great deal for icons and sprites.

### Cited Findings

#### Models and platforms current in 2026
- **OpenAI gpt-image-2** ("ChatGPT Images 2.0") was released on April 21, 2026. It outputs up to 2K, with aspect ratios
  from 3:1 to 1:3 and up to eight variants per prompt. It has a "thinking" mode that plans the composition first (low,
  medium and high in the API), much better text inside images, and claimed consistency of characters and objects across
  a set of images — [NYU Shanghai RITS, Apr 2026](https://rits.shanghai.nyu.edu/ai/openai-launches-chatgpt-images-2-0-with-2k-output-and-reasoning-mode/); [Neurohive, Apr 2026](https://neurohive.io/en/news/chatgpt-images-2-0-openai-launches-image-generation-model-with-reasoning-2k-resolution-and-multilingual-text/); [BuildFastWithAI, 2026](https://www.buildfastwithai.com/blogs/chatgpt-images-2-0-gpt-image-2-2026).
  A headline claim of "99% text accuracy" gives no method — [DataNorth, Apr 2026](https://datanorth.ai/news/openai-launches-gpt-image-2-with-99-text-accuracy).
  All of these are secondary sources; I found no OpenAI page.
- **Google Nano Banana Pro** (described as built on Gemini 3 Pro Image) is marketed for keeping a subject consistent
  through several reference images. Guides recommend building a reference sheet first (one suggests 8 to 14 angles), and
  one says it accepts up to 8 reference images. The "consistency" figures quoted by different guides (93%, 95%, 97%)
  point to no identifiable benchmark — [Selfielab guide, Feb 12 2026](https://selfielabstudio.com/blog/nano-banana-pro-perfect-character-consistency-guide-20260212); [Selfielab reference-sheet tutorial, Feb 26 2026](https://selfielabstudio.com/blog/nano-banana-pro-reference-sheet-tutorials-for-consistent-characters-20260226); [Coverr, 2026](https://coverr.co/blog/nano-banana-pro-ai-image-character-consistency-2026); base model per [APIYI help, 2026](https://help.apiyi.com/en/gpt-image-2-transparent-background-not-supported-en.html).
- **Nano Banana 2** (Gemini 3.1 Flash Image) launched on February 26, 2026, billed as Pro-level quality at Flash speed. It
  became the default image model across Gemini, Search, Lens, Flow and the developer API. A Japanese report's headline
  says it took first place on the leaderboard at under half the price of competitors. An update, Nano Banana 2.1, was
  reportedly made generally available on October 6, 2026 (one source only) — [NYU Shanghai RITS, Feb 2026](https://rits.shanghai.nyu.edu/ai/google-launches-nano-banana-2-pro-quality-image-generation-at-flash-speed/); [gori.me, Feb 26 2026](https://gori.me/google/google-news/163972); [LosClouds model page](https://losclouds.com/ai-models/google-gemini-3-1-flash-image-preview); [Let's Data Science, Oct 2026](https://letsdatascience.com/news/google-rolls-out-nano-banana-21-image-model-c6ac3b89).
- **FLUX.2** (Black Forest Labs) was released on November 25, 2025: a 32-billion-parameter [pro] flagship and an
  open-weight [dev] checkpoint for generation and multi-reference editing. It takes up to 10 reference images at once,
  which BFL calls "the best character, product, and style consistency available today" (vendor claim). Third parties
  give 8 to 10 references, and Cloudflare's Workers AI version allows 4 — [BFL blog, Nov 25 2025 (vendor)](https://bfl.ai/blog/flux-2); [ComfyUI blog, Nov 2025](https://blog.comfy.org/p/flux2-state-of-the-art-visual-intelligence); [MindStudio model page, undated](https://www.mindstudio.ai/models/flux-pro-2).
  A 2026 roundup says the small FLUX.2 klein 4B model is Apache 2.0, allowing commercial use with no revenue cap — [2026 roundups: Ludo (vendor)](https://ludo.ai/compare/best-ai-image-generators-for-game-art), [Cinevva](https://app.cinevva.com/guides/ai-asset-generators-games), [Techsy](https://techsy.io/en/blog/best-ai-game-asset-generators), [Blyth](https://blyth.ai/blog/best-ai-game-asset-generators-2026) (the summary didn't show which roundup made the claim).
- **Midjourney V8** opened as an alpha on March 17, 2026. It is about 5x faster than V7, adds `--hd` (native 2K) and a
  `--q 4` coherence mode, carries over V7 personalization profiles, moodboards and style references, and charges about
  4x more for its best features. Midjourney's docs say V8.1 launched on midjourney.com on April 30, 2026 (Wikipedia says
  April 14), and they also mention a V8.2 — [The Decoder, Mar 2026](https://the-decoder.com/midjourney-v8-rolls-out-with-5x-faster-generation-but-charges-4x-more-for-its-best-features/); [Midjourney docs: models, 2026](https://docs.midjourney.com/docs/models); [Wikipedia](https://en.wikipedia.org/wiki/Midjourney); [Heather Cooper, Substack, Mar 2026](https://heatherbcooper.substack.com/p/midjourney-v8-is-live-on-alpha-heres).
- Midjourney's consistency tools are **Omni Reference** (V7 and later) and **Style Reference** (V6 and later). Omni
  Reference puts a character, object, vehicle or creature from a reference image into new images; its weight `--ow`
  runs from 0 to 1000 (default 100) and it costs twice the GPU time — [Midjourney docs: Omni Reference](https://docs.midjourney.com/hc/en-us/articles/36285124473997); [Midjourney docs: Style Reference](https://docs.midjourney.com/docs/style-reference); [The Decoder, 2025](https://the-decoder.com/midjourney-expands-image-control-with-omni-reference-tool/).
  I found no official Midjourney API, and a 2026 roundup names the missing API as its main drawback — [Ludo roundup (vendor), 2026](https://ludo.ai/compare/best-ai-image-generators-for-game-art).
- **Seedream 5.0** (ByteDance): according to one reference page, Lite was announced on February 13, 2026 and Pro on
  June 23, 2026. Vendor and aggregator pages claim up to 14 reference images in one edit and prices from $0.035 per
  image. A review dating it to late 2024 conflicts with every other source — [HowAIWorks, 2026](https://howaiworks.ai/models/seedream); [Artlist, 2026](https://artlist.io/blog/new-seedream-5-0-announcement/); [Dzine review](https://www.dzine.ai/blog/seedream-5-0-review/).
- **Recraft V4** (V4.1 dated May 13, 2026) generates native SVG with editable paths and layers rather than traced
  bitmaps. It can match a style from 1 to 10 attached images and has palette controls. Aggregator prices run from $0.08
  (V4 Vector) to $0.30 (V4.1 Pro Vector) per image — [Segmind, 2026](https://www.segmind.com/models/recraft-v4-pro-svg); [Prodia docs](https://docs.prodia.com/models/recraft-v4/); [MindStudio, 2026](https://www.mindstudio.ai/blog/what-is-recraft-studio-2); [Shopot listing, 2026](https://api3.shopot.ai/recraft/recraft-v4.1-pro-vector).
- **Ideogram** has a dedicated "Generate with Ideogram 3.0 (Transparent Background)" API endpoint. Ideogram 4.0 lists
  transparent-background generation, bounding-box layout control and 2K output — [Ideogram API reference, undated](https://developer.ideogram.ai/api-reference/api-reference/generate-transparent-v3); [Runware Ideogram 4.0 guide, 2026](https://runware.ai/docs/models/ideogram-4-0/guides/text-and-design).
- **Game-focused platforms.**
  - Scenario trains custom styles and hosts third-party models (Retro Diffusion, Nano Banana Pro, Seedream 4.5, Ideogram) — [Scenario help, Apr 9 2026 (vendor)](https://help.scenario.com/en/articles/designing-icons-for-a-match-3-game); [Scenario: Retro Diffusion essentials (vendor)](https://help.scenario.com/en/articles/retro-diffusion-models-the-essentials/); [Scenario: Ideogram V3 essentials (vendor)](https://help.scenario.com/articles/2867418947-ideogram-v3-the-essentials).
  - Layer.ai gives access to FLUX, Imagen 4 and other models. Its 2025 roadmap planned "Animation v1" (lip-sync and
    simple 3D animation), with 2D Spine and sprite exports as next steps; I couldn't confirm which of these shipped — [Layer 2D page (vendor)](https://www.layer.ai/features/2d-ai-art-generation); [Layer roadmap, 2025 (vendor)](https://help.layer.ai/en/articles/8001331-layer-product-roadmap).
  - The 2026 roundups recommend:
    - Leonardo for concept art and textures (free tier of 150 tokens a day, paid plans from $10 a month);
    - Adobe Firefly for legally indemnified assets;
    - Recraft for vectors and saved styles;
    - PixelLab for pixel art and sprite sheets;
    - Scenario for consistent on-brand 2D (custom LoRA from 5 to 100 images).

    Their rankings conflict, and several are written by vendors — [Ludo (vendor), 2026](https://ludo.ai/compare/best-ai-image-generators-for-game-art); [Ludo (vendor), 2026](https://ludo.ai/compare/best-ai-game-asset-generators); [Cinevva, 2026](https://app.cinevva.com/guides/ai-asset-generators-games); [Techsy, 2026](https://techsy.io/en/blog/best-ai-game-asset-generators); [Blyth, 2026](https://blyth.ai/blog/best-ai-game-asset-generators-2026).
- **Newer names I could not verify.** Banner text on an API reseller's pages says "FLUX 3 and Gemini 3.7 Flash are now
  live" and "GPT Image 2.5 Sunburst and Flare are now live". APIYI's October 2026 notice also recommends
  "gpt-image-2.5-flare" and "gpt-image-2.5-sunburst". I found no first-party source for any of them — [CometAPI page banner](https://www.cometapi.com/how-to-use-omni-reference-in-midjourney-v7); [CometAPI (nl) banner](https://www.cometapi.com/nl/how-to-use-omni-reference-in-midjourney-v7/); [APIYI notice, Oct 15 2026](https://docs.apiyi.com/en/live/2026-10/gpt-image-2-transparent-shutdown).

#### Transparency (alpha) support, which decides much of the cleanup for icons and sprites
- **gpt-image-2 no longer accepts transparent backgrounds.** It now rejects `background: "transparent"` with
  "Transparent background is not supported for this model", according to a thread on OpenAI's developer forum. The
  reseller APIYI says OpenAI shut the option off on September 30, 2026 and all 21 of its retest calls (14 generations,
  7 edits) were refused. APIYI's own August 2026 guide had documented the parameter working, so either a preview ended or
  the behaviour changed. Neither source is an OpenAI statement — [OpenAI Developer Community, 2026](https://community.openai.com/t/is-transparent-background-support-broken-in-gpt-image-2/1402707); [APIYI notice, Oct 15 2026](https://docs.apiyi.com/en/live/2026-10/gpt-image-2-transparent-shutdown); [APIYI guide, Aug 2026](https://docs.apiyi.com/en/live/2026-08/gpt-image-2-transparent-background).
- gpt-image-1.5 still lists `auto`, `transparent` and `opaque` backgrounds, and a comparison names it OpenAI's model for
  transparent PNGs — [fal.ai comparison, 2026](https://fal.ai/learn/tools/gpt-image-2-vs-gpt-image-1-5).
- Asking for a "transparent backdrop" in the prompt can make a model paint a fake checkerboard. Vendors advise
  describing only the object and doing pixel-exact extraction yourself (rembg, PIL, sharp) — [Atlas Cloud (vendor), 2026](https://www.atlascloud.ai/blog/guides/gpt-image-2-api-transparent-background).
- **Nano Banana Pro and Nano Banana 2 don't output alpha.** A transparency prompt gives a painted grey-and-white
  checkerboard. There are two workarounds:
  - generate on a solid contrasting background and remove it;
  - render the subject once on white and once on black, then compute alpha = 1 − (white − black)/255 (a Replicate
    model does this).

  — [APIYI help, 2026](https://help.apiyi.com/en/gpt-image-2-transparent-background-not-supported-en.html); [Google AI developer forum](https://discuss.ai.google.dev/t/how-can-i-make-the-background-transparent/111698); [Replicate: nano-banana-2-transparent](https://replicate.com/jide/nano-banana-2-transparent).
- Ideogram 3.0's transparent endpoint produces the alpha channel automatically. Most Retro Diffusion models support
  background removal and seamless tiling — [Ideogram API reference](https://developer.ideogram.ai/api-reference/api-reference/generate-transparent-v3); [Replicate: Retro Diffusion readme](https://replicate.com/retro-diffusion/rd-fast/readme).

#### Item icons
- **Scenario's guidance for a consistent icon pack** (guide updated April 9, 2026):
  - train a custom model on roughly 10 to 30 images in the art style;
  - fix each icon's shape with a sketch in image-to-image at an influence of 10 to 25;
  - or use its ready-made icon models ("Colorful Digital Icons", "Juicy Icons").

  Scenario also lists a Flux LoRA for 2D game icons with "bold outlines and clean silhouettes". The guide adds that
  newer models such as Nano Banana Pro and Seedream 4.5 reach similar or better results in fewer steps — [Scenario help (vendor), Apr 9 2026](https://help.scenario.com/en/articles/designing-icons-for-a-match-3-game); [Scenario docs (vendor)](https://docs.scenario.com/get-started/documentation/integrating-with-applications-real-world-scenarios); [Scenario model page (vendor)](https://www.scenario.com/models/stylized-game-icons-and-props); [Scenario gem icon pack tutorial (vendor)](https://scenario.com/post/crafting-the-perfect-gem-icon-pack).
- Layer's icon-set page describes the drift its product is meant to prevent. Without a locked frame, padding, light
  direction and palette, "by icon thirty the light has drifted from top-left to overhead, the outlines have thickened,
  and poison is harder to pick out of the hotbar than fire" — [Layer (vendor), 2026](https://layer.ai/use-cases/game-icon-sets).
- **Icon workflows in use:**
  - Build the set from one anchor image used as the style reference for every new item (Ludo; it warns that its "item
    icon" type resets to "Art" when a reference is attached).
  - Generate a whole set as one sheet and slice it. SpriteCook says "thirty icons cost one generation instead of
    thirty".
  - Strip backgrounds in a separate pass.
  - Use a packaged SDXL ComfyUI workflow with batch and post-processing scripts ("Dark Fantasy RPG Icon Factory").

  — [Ludo docs (vendor)](https://ludo.ai/docs/image-generator/icons-and-ui); [SpriteCook (vendor)](https://www.spritecook.ai/game-icon-generator); [AgentBrush (vendor)](https://agentbrush.dev/blog/ai-pixel-art-generator); [Stavroo, itch.io](https://stavroo.itch.io/dark-fantasy-rpg-icon-factory).

#### Monster and character illustrations, portraits
- BFL's advice for consistent characters: supply one or more reference images, then describe the new scene, pose or
  context. Identity and clothing are said to hold across a series of edits — [BFL docs (vendor)](https://docs.bfl.ai/guides/usecases_editing_character_consistency).
- **A shipped game where it went wrong.** Vapor World: Over The Mind replaced its in-engine cutscenes with AI-generated
  ones. Reviewers focused on inconsistent character models, the launch drew 64 mostly negative reviews, and the studio
  pulled the AI cutscenes (2026) — [PC Gamer, 2026](https://www.pcgamer.com/gaming-industry/steam-week-in-review-a-touch-of-ai-is-all-it-takes-to-trigger-backlash-as-a-promising-new-indie-falls-afoul-of-slop-skeptics/); [IndieForGames, 2026](https://indieforgames.com/steam-ai-art-backlash/); [Everything Edinburgh, 2026](https://everythingedinburgh.com/games/news/vapor-world-steam-ai-cutscene-backlash/).
- Codex Mortis (demo December 2025, art from ChatGPT) was described as "riddled with AI artifacts" and showing "obvious
  AI traits" — [Creative Bloq, Dec 2025](https://creativebloq.com/3d/video-game-design/is-the-first-100-percent-ai-video-game-the-ultimate-rage-bait); [Gfinity, Dec 2025](https://www.gfinityesports.com/article/steam-now-has-a-playable-demo-for-the-worlds-first-game-created-100-through-ai); [Gigazine, Dec 15 2025](https://gigazine.net/gsc_news/en/20251215-codex-mortis).

#### Backgrounds and parallax layers
- Sorceress's parallax tool turns a scene idea into prompts for foreground, midground and background, generates each
  layer separately, keys out a green screen on the front layers, and exports PNGs with a config file — [Sorceress wiki (vendor)](https://sorceress.games/wiki/sorceress/parallax).
- **AI-assisted commercial packs already ship.**
  - Cobalt Game Assets sells 20 environments as separate 2K PNG layers, described as "AI-assisted and carefully
    post-processed, separated and organized".
  - An itch.io pack has four seamlessly repeating landscapes made with SDXL in ComfyUI.

  — [Unreal Engine forums (Fab listing), 2026](https://forums.unrealengine.com/t/cobalt-game-assets-marcel-iulian-serban-parallax-game-backgrounds-pack-vol-1-fantasy-sci-fi-cozy-scenes/2765309); [coderedcat, itch.io](https://coderedcat.itch.io/4k-parallax-landscape).
- **Qwen-Image-Layered** (paper December 18, model December 19, 2025; Apache 2.0) splits an existing image into a
  variable number of RGBA layers, and can split a layer again. Its examples show 3 and 8 layers, and it recommends
  640 px (a 1024 setting also exists). Generating layers straight from text works poorly, and the prompt cannot control
  what goes into each layer — [GitHub (fetched)](https://github.com/QwenLM/Qwen-Image-Layered); [arXiv 2512.15603, Dec 2025](https://arxiv.org/html/2512.15603v1).
- Retro Diffusion has a tileset model (RD Tile), and most of its models support seamless tiling — [Scenario essentials (vendor)](https://help.scenario.com/en/articles/retro-diffusion-models-the-essentials/); [Replicate readme](https://replicate.com/retro-diffusion/rd-fast/readme).

#### UI frames and 9-slice panels
- **A vendor's August 2026 guide on AI-generated game UI:**
  - Buttons and panels generated one at a time come out with mismatched lighting, border thickness and corner radius.
  - Generate the whole screen as concept art and cut the pieces from it instead.
  - AI output isn't made for stretching, so 9-slice borders have to be marked by hand.
  - Test each panel tall, wide and near its minimum size: corners should keep their shape, edge motifs shouldn't go
    rubbery, and the centre shouldn't show seams.
  - Prompt for flat corners and a thick, even border.

  — [SpriteCook blog (vendor), Aug 11 2026](https://www.spritecook.ai/blog/generate-game-ui-with-ai); [FrameSprite, dev.to](https://dev.to/feed/framesprite).
- One vendor's "nine-slice aware" export is still only planned, and its preview is a single concept image — [LlamaGen PixelBox concept preview (vendor)](https://llamagen.ai/pixel-game-tools/pixel-art-ui-generator).

#### VFX textures and flipbooks
- The only dedicated VFX tool I found is an Unreal Engine 5 plugin, "AI Flipbook Generator". It prompts OpenAI's image
  models for Niagara sprite sheets in 4x4, 6x6 or 8x8 grids, using a grid template and a gutter-locking mask so the cells
  don't bleed into each other. It costs about $0.04 per 1024x1024 image. These are the seller's claims — [Fab listing (vendor), undated](https://www.fab.com/listings/695f3e5d-3aae-4ed8-9720-a4f65f9fe4ac).
- A developer who turned AI video into sprites said this solved continuity but "still required a lot of manual work,
  frame by frame, removing backgrounds, and handling semi-transparent effects" — [MP4 to Sprite, itch.io](https://yewlark.itch.io/mp4tosprite).

### Inferences
- **Single static images are largely solved; sets and motion are not.**
  - The marketing of every frontier model in 2026 centres on multi-reference consistency, not on raw image quality.
    That suggests single-image quality is no longer the bottleneck.
  - The failures seen in shipped games are about consistency (Vapor World's character models) and visible artefacts
    (Codex Mortis).
- **Transparency is a pipeline decision, and it must not depend on one provider.** These models give alpha:
  gpt-image-1.5, Ideogram 3.0 and 4.0, Retro Diffusion's background removal, and Recraft's SVG output. gpt-image-2 and
  the Nano Banana models don't. OpenAI's switch-off happened within a month (September 30, 2026), which shows how
  fragile a one-provider pipeline is. A white/black two-render matte or a solid-colour key, done by a script in the
  repo, works with any model.
- **Per asset type (detail in section 7):**
  - Portraits and monster or character illustrations: strong.
  - Painted backgrounds: strong; separating them into parallax layers is moderate.
  - Single icons: strong; large consistent icon sets are moderate.
  - UI frames and 9-slice panels: weak to moderate. Even borders, matched corners and tileable edges are exactly the
    constraints generators don't guarantee.
  - VFX flipbooks: weak. They need both frame-to-frame coherence and soft semi-transparency.
- **LootFall item icons.**
  - At the 64 to 80 px display boxes (and 24 to 32 px stat icons), only the silhouette, two or three value groups and
    the colour survive. Fine AI detail doesn't pay at those sizes, and it is where drift (light direction, outline
    weight) is most visible.
  - The game already draws rarity in code (a glow for Rare and up). Keeping all 12 rarity treatments in code (frame,
    glow, colour ramp, particles) means the generator never has to encode rarity, which is the information players
    most need to read correctly.
- **LootFall monsters.** The game already uses reskinned monster variants (ember, ash, shade, gilded). Multi-reference
  editing models ("same creature, new biome") fit that "base plus variant" pattern better than unrelated generations do.
- **LootFall backdrops.** The battle backdrops are 640x210 bands shown at 2x, well within any model's output size. For
  parallax, generating each layer separately and keying it is simpler than decomposing a finished picture.
- **UI and VFX.** The game already uses hand-made 9-slice frames (Kenney via `UiKit`) and keys out pack effect GIFs. The
  lowest-risk plan is to keep frames hand-made or procedural and use AI only for ornament or texture fills. For VFX,
  generate single-frame elements (sparks, smoke puffs, glows) on black for additive blending and keep the motion in
  Godot particles. This is an inference; I found no source that tested it.

### Gaps
- No independent benchmark compares these models for game icons or sprites; the rankings come from vendor roundups.
- I couldn't verify "FLUX 3", "Gemini 3.7 Flash" or "GPT Image 2.5 Sunburst/Flare": the shared search budget ran out,
  and the only mentions are reseller banners and a reseller notice.
- I didn't establish whether Midjourney or FLUX.2 output native alpha in 2026.
- I found little 2026-specific on Leonardo (only roundup mentions) or on Layer's current animation features.
- No source tested AI-made 9-slice frames or VFX flipbooks independently. The VFX finding rests on one seller's listing.

## 2. Consistency at scale: references, custom models, editing for variants, and cleanup

### Takeaway
Consistency tools have matured: models take 8 to 14 reference images, offer style references, and custom LoRAs can be
trained cheaply from 10 to 30 images. But no source measures how many assets stay consistent, and vendor material
itself shows an unanchored set drifting by about the 30th icon. In practice a set of hundreds stays consistent by
locking everything the generator could vary (frame, padding, light direction, palette, outline) and then enforcing it
with deterministic cleanup after generation. The cleanup steps themselves (background removal, matting, grid snapping,
palette quantization, pivot alignment) are well tooled and fast. Human review is the expensive part.

### Cited Findings
- **How many reference images each model takes:**
  - FLUX.2: up to 10 (4 through Cloudflare) — [BFL blog, Nov 25 2025 (vendor)](https://bfl.ai/blog/flux-2).
  - Nano Banana Pro: up to 8, per one guide — [Selfielab, Feb 2026](https://selfielabstudio.com/blog/nano-banana-pro-perfect-character-consistency-guide-20260212).
  - Seedream 5.0: up to 14 — [HowAIWorks, 2026](https://howaiworks.ai/models/seedream).
  - Recraft V4: a style from 1 to 10 images — [Segmind, 2026](https://www.segmind.com/models/recraft-v4-pro-svg).
  - Midjourney: Omni Reference plus Style Reference — [Midjourney docs](https://docs.midjourney.com/hc/en-us/articles/36285124473997).
  - gpt-image-2: claimed consistency across a set, up to eight variants per prompt — [Neurohive, Apr 2026](https://neurohive.io/en/news/chatgpt-images-2-0-openai-launches-image-generation-model-with-reasoning-2k-resolution-and-multilingual-text/).
- **Custom models (LoRAs).**
  - Scenario's docs suggest 10 to 30 reference images in your art style to keep an item-icon pack on style — [Scenario docs (vendor)](https://docs.scenario.com/get-started/documentation/integrating-with-applications-real-world-scenarios).
  - A roundup gives 5 to 100 images for Scenario LoRA training — [Cinevva, 2026](https://app.cinevva.com/guides/ai-asset-generators-games) (or another roundup in the same result set).
  - General LoRA guides: 5 images is a minimum for simple, consistent styles; 10 to 30 is recommended; returns
    diminish past 50 unless the style varies a lot. Another guide gives 15 to 50. Style LoRAs need more images than
    character LoRAs, training images should be at least 1024 px with no watermarks, and mixed styles give unpredictable
    results. None of these guides is about icons — [Layer help: train a LoRA (vendor)](https://help.layer.ai/en/articles/14094114-how-to-train-a-custom-model-lora); [Diffusion Doodles](https://diffusiondoodles.substack.com/p/how-to-train-a-lora); [Krea docs (vendor)](https://docs.krea.ai/developers/tutorials/train-image-style); [RunComfy (vendor)](https://www.runcomfy.com/trainer/ai-toolkit/z-image-character-lora-dataset-guide).
- **Editing models are catching up with custom training.** Scenario's April 2026 icon guide says Nano Banana Pro and
  Seedream 4.5 reach similar or better results than its trained-model workflow, in fewer steps — [Scenario help (vendor), Apr 9 2026](https://help.scenario.com/en/articles/designing-icons-for-a-match-3-game).
- **Evidence of drift.**
  - Layer: by the 30th icon the light has drifted from top-left to overhead and the outlines have thickened — [Layer (vendor), 2026](https://layer.ai/use-cases/game-icon-sets).
  - Typical drift in sprite frames: proportions, colour shifts, detail lost or gained, inconsistent outlines; compare
    frames side by side — [PCTechMag, Jul 2026](https://pctechmag.com/2026/07/cutting-sprite-production-time-from-weeks-to-minutes-without-losing-consistency/); [Abratabia guide](https://abratabia.com/ai-game-art/ai-sprite-sheets.php).
- **Anchoring techniques:**
  - Lock frame, padding, light direction and palette before generating — [Layer (vendor)](https://layer.ai/use-cases/game-icon-sets).
  - Use one anchor image as the style reference for every item — [Ludo docs (vendor)](https://ludo.ai/docs/image-generator/icons-and-ui).
  - Generate the whole set on one sheet — [SpriteCook (vendor)](https://www.spritecook.ai/game-icon-generator).
  - Make a reference sheet first, start prompts with exact descriptions of the reference images, and avoid stacking
    "highly detailed", which reportedly weakens consistency — [Selfielab, Feb 2026](https://selfielabstudio.com/blog/nano-banana-pro-reference-sheet-tutorials-for-consistent-characters-20260226); [Selfielab, Feb 2026](https://selfielabstudio.com/blog/nano-banana-pro-master-character-consistency-prompts-20260225).
  - Fix each shape with a sketch in image-to-image (influence 10 to 25) — [Scenario help (vendor)](https://help.scenario.com/en/articles/designing-icons-for-a-match-3-game).
  - Skill registries list an OpenAI "sprite-pipeline" agent skill that starts from an approved in-game seed frame and
    generates the whole animation strip in one pass, on the grounds that frame-by-frame generation drifts. I couldn't
    find it in the openai/plugins README when I fetched it, so treat it as unverified — [Tessl registry](https://tessl.io/registry/skills/github/openai/plugins/sprite-pipeline); [mcpservers.org](https://mcpservers.org/agent-skills/openai/sprite-pipeline); [openai/plugins (fetched)](https://github.com/openai/plugins).
- **Cleanup tools:**
  - **Background removal** as a separate pass — [Ludo docs (vendor)](https://ludo.ai/docs/image-generator/icons-and-ui); [AgentBrush](https://agentbrush.dev/blog/ai-pixel-art-generator).
    Or a white/black two-render matte — [Replicate](https://replicate.com/jide/nano-banana-2-transparent).
  - **unfake.py** (MIT) detects the pixel scale, reduces colours (Wu's algorithm), snaps to the grid, offers several
    downscaling methods, removes noise and stray diagonal pixels, binarizes alpha and removes backgrounds by flood fill.
    It takes about 700 ms per 1024x1024 image with its Rust part, against about 71 s in pure Python (the author's
    desktop) — [GitHub (fetched)](https://github.com/painebenjamin/unfake.py).
  - **Sprite Fusion Pixel Snapper** (MIT) snaps to a grid and quantizes to a palette (16 colours by default, or a
    custom palette) — [GitHub (fetched)](https://github.com/Hugo-Dz/spritefusion-pixel-snapper).
  - Video-to-sheet tools remove the background, align frames to a shared pivot, suggest a loop point, normalize padding,
    align to a grid and export TexturePacker or Godot formats with a JSON manifest — [Rengi Forge listing (vendor)](https://opensea.io/tools/x402-bazaar/base/4370968317791781978); [Sorceress Auto-Sprite (vendor)](https://sorceress.games/pages/auto-sprite).

### Inferences
- **How many assets stay consistent.** With every visual variable locked and enforced after generation, a coherent set
  of a few hundred icons or monster portraits looks achievable for one person. "Thousands" are better made as families
  (a base design per slot or archetype, plus material, tier or biome variants made by editing models and palette
  swaps) than as thousands of independent generations. Nothing measured supports a specific number.
- **Generation is cheap; review is not.** Illustrative arithmetic, based on assumptions rather than measurements:
  - 3,000 icons at 4 attempts each is 12,000 images.
  - At the $0.035 to $0.08 list prices above, that costs about $420 to $960.
  - Accepting or rejecting each image in 10 seconds takes about 33 hours of review.

  Human curation and fixes, not API cost, set the budget.
- **Automate the checks.** A script can enforce much of the consistency: bounding-box fill, centring, outline
  thickness, distance from the palette, where the brightest quadrant is (light direction), and a test at display size.
  This also matches the project's convention of rebuilding `assets/` from a script (`tools/import_art.py`) rather than
  hand-editing it.
- **Where generation can run.** Cloud sessions here can only reach GitHub, so calling the generation APIs has to happen
  outside them: on the owner's machine or apps, or in a GitHub Actions workflow with API keys kept as secrets. The
  deterministic cleanup (matting, snapping, palette, outline, slicing) can live in the repo and be checked in cloud
  sessions. unfake.py and the Pixel Snapper are MIT-licensed and hosted on GitHub.
- **Keep one anchor set.** Approve 20 to 30 hand-picked anchors (a "style bible") and use them both as LoRA training
  data and as references for editing models. This follows from the recommended dataset sizes above.

### Gaps
- No study measures consistency against set size, or the hours of cleanup per asset.
- No independent comparison of multi-reference editing against a trained LoRA for icon sets was found.
- No public write-up was found of a developer generating hundreds to thousands of game icons end to end.

## 3. Sprite animation: frame-by-frame sheets and video models turned into frames

### Takeaway
Dedicated tools (PixelLab, Retro Diffusion's RD Animation, Ludo, God Mode AI, Scenario) now turn one character image
into small animated sprite sheets with walk, run, idle and attack presets, mostly at 64 to 256 px. Video models (Veo 3.1,
Kling, Seedance) can be mined for frames, and first/last-frame inputs help loops close. But frame-to-frame consistency,
flicker, loop closure, backgrounds and pivot alignment all still need cleanup. There is no independent evidence that
these outputs reach hand-animated quality, and even a competitor's guide says final frames need human cleanup.
Frame-by-frame AI animation for dozens of monster types is the most cleanup-heavy route for LootFall.

### Cited Findings
- **PixelLab:**
  - Skeleton-based animation accepts canvases of 16 to 256 px. An older version of the docs page and the API list a
    maximum of 128.
  - The Rotate tool produces 8 directional views at up to 128 px.
  - "Create animations (automatic)" requires a canvas of 64x64 or smaller and at least Tier 1.
  - The product page advertises 4- or 8-direction views.

  — [PixelLab docs: skeleton animation](https://www.pixellab.ai/docs/tools/skeleton-animation); [PixelLab docs: rotate](https://pixellab.ai/docs/tools/rotate); [PixelLab docs: create animations (automatic)](https://www.pixellab.ai/docs/tools/create-animations-automatic); [PixelLab API](https://pixellab.ai/pixellab-api); [PixelLab](https://pixellab.ai/).
- **Retro Diffusion RD Animation** makes animated sprites and sheets. Reference images aren't used as starting frames,
  each style supports only certain sizes, and a palette input enforces strict colours — [Replicate: rd-animation](https://replicate.com/retro-diffusion/rd-animation); [Replicate: rd-animation readme](https://replicate.com/retro-diffusion/rd-animation/readme).
- **Ludo:**
  - Starts from a static sprite, then applies walk, run, idle or attack presets or a text description of the motion.
    It can also transfer motion from an uploaded reference video.
  - Exports a sprite-sheet PNG, GIF, JSON, frame sequences and MP4, with 64, 128 or 256 px cells.
  - A keyframe tool takes up to three keyframes and returns 4 to 64 frames.

  — [Ludo sprite generator (vendor)](https://ludo.ai/features/sprite-generator); [Ludo keyframe animation (vendor)](https://ludo.ai/tools/keyframe-sprite-animation); [Ludo docs (vendor)](https://ludo.ai/docs/sprite-generator); [Ludo for Godot (vendor)](https://ludo.ai/tools/sprite-generator-for-godot).
- **God Mode AI:**
  - Upload a character, pick from about 100 actions and views (side-scrolling or 8-direction isometric), and download
    PNG sheets. It also claims Spine export.
  - Pricing: credit packs from $12, or $19 a month for up to 200 generations if you share your results publicly.
  - One comparison calls it less polished than the larger platforms. Its Spine page was about a year old when found.

  — [DolphinVoice directory](https://dolphinvoice.ai/en/ai-apps/god-mode-ai-1052529); [Ludo comparison (vendor)](https://ludo.ai/compare/best-ai-sprite-generators); [God Mode AI Spine page (vendor)](https://www.godmodeai.co/ai-spine-animation).
- **Scenario:** "Convert to Video" loads your image as the first frame (Seedance 1 Lite or Pro), and the clip is then
  turned into a sprite sheet — [Scenario help (vendor)](https://help.scenario.com/articles/9088582240-create-spritesheets-with-scenario); [Scenario blog (vendor)](https://www.scenario.com/blog/ai-sprite-generator).
- **Video models.**
  - Veo 3.1 and Kling accept a first and last frame, which helps a loop close. The trade-off is that a keyframed clip
    loops cleanly largely because little happens in it — [Mervin Praison, Sep 2026](https://mer.vin/2026/09/which-ai-video-models-actually-take-a-first-and-last-frame/).
  - Seedance 2.0's input modes can't be mixed: text-to-video; first (and optionally last) frame; or "omni-reference"
    with up to nine images — [Pixo, 2026](https://pixo.video/ja/blog/seedance-2-first-frame-reference-image); [APIYI FAQ](https://docs.apiyi.com/en/faq/seedance-first-frame-mixed-with-reference.md).
- **Comparisons of video models conflict and are unreproducible.**
  - One vendor blog reports visible artefacts in about 30% of Kling's first-pass clips against about 15% for Veo 3.1,
    over 20 physics-heavy prompts.
  - Another says Kling's walk cycles, hands and faces are more anatomically accurate.
  - A 20-prompt test had Kling winning three of four motion prompts.

  None gives a reproducible method — [Oakgen, 2026](https://oakgen.ai/blog/kling-3-vs-veo-3-1-tested); [Pixmax, 2026](https://www.pixmax.ai/blog/kling-vs-veo-3.html); [Wan27 blog, 2026](https://wan27.org/blog/kling-3-0-vs-veo-3-1).
- **Turning a clip into a sprite loop.**
  - AI clips run at 24 to 30 fps, but a sprite loop usually needs 8 to 24 frames. If the last frame duplicates the
    first, end the range one frame earlier — [LoopSprite](https://loopsprite.com/extract/).
  - A community write-up describes generating the first pose with an image model, animating it in Kling, then
    extracting, cleaning and packing with local scripts. Its loop advice: the last frames often look too much like the
    first, which makes the animation seem stuck, so keep the one that leads best into the first frame — [LayrKits on X, 2026](https://x.com/LayrKits/article/2050277473116619240) (attribution within the result set uncertain).
  - Tools exist that resample frames to 64, 128 or 256 px — [DoSprite, itch.io](https://agemar.itch.io/dosprite).
- **Flicker comes with the method.** Each frame of a diffusion video differs slightly, which causes flicker. A
  deflicker node helps, and upscaling unstable footage magnifies the problem — [Topaz Labs community](https://community.topazlabs.com/t/deflicker-for-ai-videos/42459); [Magic Hour (vendor)](https://magichour.ai/blog/how-to-fix-flicker-in-ai-video).
- **The cleanup is mostly manual.** A developer reported that the video route still meant "a lot of manual work, frame
  by frame" — [MP4 to Sprite, itch.io](https://yewlark.itch.io/mp4tosprite).
- One guide recommends generating only the base character with AI and animating by hand (for example in Aseprite),
  because AI can't yet guarantee frame-to-frame precision — [Wayline](https://www.wayline.io/blog/animate-2d-game-sprites-workflow-royalty-free-assets) or [Abratabia](https://abratabia.com/ai-game-art/ai-sprite-sheets.php) (one of these two).
- A guide from the maker of a competing tool says AI suits prototypes, jams and placeholder art, and that polished hero
  characters and final animation frames still need human cleanup — [LlamaGen (vendor of a competing tool), 2026](https://llamagen.ai/pt-br/articles/best-ai-pixel-art-generators-2026).

### Inferences
- **For an auto-battler with many monster types across many biomes, frame-by-frame AI animation multiplies the
  cleanup by every monster and every variant.** The most reliable cheap route is one high-quality static illustration
  per monster plus procedural motion: an idle bob, squash and stretch on hit, a lunge to attack, a flash, and a death
  dissolve or particles. LootFall's screen effects (`Main.Shake`, `Flash`, `HitStop`) and its tween-based UI already
  point that way. Hand-cleaned AI frames can be kept for a few high-value actors, such as the knight and the bosses.
- **Resolution changes the picture.** The tools work best at small pixel sizes: 64 px or less for PixelLab's automatic
  animation, 128 to 256 px for its skeleton tool. At those sizes there are fewer pixels to keep consistent, but each
  wrong pixel shows. Video-derived frames at high resolution keep more drift and flicker, and downscaling them hides
  some of it. I couldn't find a test of whether flicker survives downscaling to sprite size.
- **Budget for cleanup on video-derived frames:** a background key, pivot alignment, loop trimming, a locked palette to
  stop colour flicker, and a downscale. Rengi Forge and Sorceress automate parts of this, but no independent tests of
  them were found.

### Gaps
- No independent, hands-on quality reviews of PixelLab, RD Animation, Ludo or God Mode AI animation were found; all
  descriptions are vendors' own.
- No measured cleanup time per animation.
- No sprite-specific evidence for Wan, Sora 2 or Runway Gen-4.
- No data on how well 8-direction rotations stay consistent across many characters.

## 4. 2D skeletal and cut-out animation from AI art (Spine, Godot Skeleton2D, Live2D)

### Takeaway
Since late 2025, open models can split a single painting into inpainted layers: See-through (up to 23 body-part layers)
and Qwen-Image-Layered. An open-source toolchain (rig-parts) turns those layers into a checked Spine 4.3 rig with an
idle loop, and commercial tools (Riggy, anysplit, segly) do layer splitting and occlusion fill for Spine and Live2D. The
output is a first draft ("an existence proof, not a rate"). The tooling targets front-facing anime-style humanoids,
19 to 27% of the visible pixels are invented by the model, and bones, meshes and motion still need manual correction.
Nothing found targets Godot's Skeleton2D directly.

### Cited Findings
- **See-through** (ACM SIGGRAPH 2026; changelog entries April 2 and 14, 2026; Apache 2.0) splits one anime illustration
  into up to 23 body-part layers with inferred drawing order, fully inpainted, and outputs a layered PSD with depth maps
  and masks. It needs about 12 to 16 GB of VRAM at 1280 px in bf16, or about 8 GB with quantization (a small quality
  loss: PSNR about 30 dB, SSIM about 0.96). It says it is "not yet an Image-to-Live2D pipeline": rigging, deformation
  meshes and physics aren't covered. Its splits favour semantic correctness over the layer choices an animator would
  make — [GitHub (fetched)](https://github.com/shitagaki-lab/see-through).
- **rig-parts** (MIT, 2026, version 1.37.0 mentioned) runs See-through twice: once on the full figure and once on a head
  crop, because the eyes need more pixels. It then assembles the parts, reports uncovered holes, drafts bones, meshes,
  regions and an idle motion for the author to correct, and compiles a checked Spine 4.3 skeleton, atlas and loop
  preview.
  - **Input it wants:** a front view with arms clear of the body and hands open. Its examples were painted with SDXL
    anime checkpoints (Hassaku XL, Pony Diffusion V6 XL).
  - **Limits it states:** one depth value per layer, so overlaps can render wrongly once parts move; no expressions,
    lip-sync or jiggle by default; hidden pixels are synthesized rather than drawn by an artist (about 19 to 27% of the
    opaque pixels in its examples); raised-arm or reclining poses need keypoints.
  - **Success rate:** ten characters have passed its gates, "an existence proof, not a rate".

  — [GitHub (fetched)](https://github.com/firejune/rig-parts).
- **Qwen-Image-Layered** (December 2025, Apache 2.0) splits an image into RGBA layers, with no rig. A tester using it
  with a LoRA to split a face for Live2D still had to cut individual facial features and separate front and back hair
  by hand — [GitHub (fetched)](https://github.com/QwenLM/Qwen-Image-Layered); [lilting.ch test](https://lilting.ch/en/articles/qwen-image-layered-live2d-face-parts).
- A tester judged See-through an "automatic PSD draft generator" rather than a one-click Live2D model maker — [lilting.ch test](https://lilting.ch/en/articles/see-through-anime-layer-decomposition).
- **Bunraku** (arXiv 2607.27348, submitted July 29, 2026; Tsinghua, NTU and SparcAI) takes one illustration and outputs
  layered RGBA parts, a deformation mesh per layer and keypose animation data. It reports a per-vertex direction cosine
  of 0.768 on 50 held-out characters and introduces a Live2D-Bench benchmark. The authors note that a Live2D model
  normally takes weeks of manual layer separation, occlusion fill, mesh placement and keyframing. It is a preprint and
  hasn't been independently validated — [alphaXiv](https://www.alphaxiv.org/abs/2607.27348); [papers.cool](https://papers.cool/arxiv/2607.27348).
- **Commercial tools:**
  - Riggy takes a layered PSD or a flat PNG or JPG, splits it into parts, paints in hidden areas and outputs a Spine 4.2
    skeleton. It does not create the character from a text prompt. A Phaser tutorial (October 2026) walks through it — [Riggy (vendor)](https://riggyapp.com/); [Phaser, Oct 2026](https://phaser.io/news/2026/10/riggy-spine-animation-phaser-tutorial).
  - anysplit cuts parts and fills what's behind them, exporting a PSD or PNGs for Spine or Live2D. It does no rigging — [anysplit FAQ (vendor)](https://www.anysplit.net/faq.html).
  - segly exports PSDs prepared for Spine's PSD import, with slot tags, bone groups and pivots — [segly (vendor)](https://segly.io/).
  - imagetolayers gives visual layers only; bones, meshes, weights and animation stay manual — [imagetolayers (vendor)](https://www.imagetolayers.com/character-to-layers/spine).
  - Layer offers a "2D Animation Rigging Sheet" template that turns a concept into a parts sheet (limbs, torso, head) — [Layer (vendor)](https://layer.ai/tools/layer--create-spine-components).
  - God Mode AI claims it infers how the parts connect and exports to Spine 3.5 to 4.2 (page about a year old) — [God Mode AI (vendor)](https://www.godmodeai.co/ai-spine-animation).
- Spine's own automatic weights date from Spine 1.9.15 and are algorithmic, not AI. A common failure is that only the
  first bone gets weight when the bones don't overlap the mesh (forum posts, mostly from before 2025, possibly
  outdated) — [Esoteric forum, pre-2025](https://esotericsoftware.com/forum/d/3108); [Esoteric forum](https://ru.esotericsoftware.com/forum/d/28557-mesh-auto-weighting-only-assigns-to-first-bone).
- Live2D Inc. has a Japanese patent application, published June 2026, on generating hidden content in layered
  illustrations from the surrounding image — [IPqwery](https://ipqwery.com/ipowner/en/owner/ip/802074-live2d-inc.html).

### Inferences
- **Cut-out rigs are the AI-compatible route to animating a few key characters.** One consistent painting yields every
  frame, so AI's weakness at frame-to-frame consistency doesn't apply. For LootFall that could mean the knight and
  perhaps the bosses: paint once in a front or side pose with limbs clear, split, then rig by hand or semi-automatically.
- **Monsters are a poor fit for today's splitters.** The tools are trained or tested on anime-style humanoids.
  Quadrupeds, dragons, slimes and ghosts are untested, and the invented hidden areas (about a fifth to a quarter of the
  pixels) need review.
- **Godot.** No AI tool targets Skeleton2D or Polygon2D. The realistic paths are to import the split PNG parts and
  assemble a Skeleton2D rig by hand, or to use Spine with a Spine runtime for Godot. rig-parts notes that using a Spine
  runtime falls under Esoteric's separate licence; I didn't research the Godot runtime itself.

### Gaps
- No production case study of an AI-split, rigged character in a shipped 2D game was found.
- No evidence on non-humanoid creatures or on side-view poses (LootFall's knight faces right in a side-on lane).
- No Godot-specific tooling found; the Godot Spine runtime and its licence weren't researched.

## 5. Pixel art specifically: palette, grid alignment and animation

### Takeaway
General models do not understand the pixel grid. Their "pixel art" has pixels of inconsistent size and position, a grid
that drifts and colours outside any palette, so it needs snapping and quantizing. MIT-licensed tools on GitHub now fix
much of this automatically. Pixel-specific models (Retro Diffusion, PixelLab) generate grid-aligned, palette-limited art
natively at small sizes (up to about 256 px) and accept a fixed palette. That makes static pixel icons and sprites one
of the most AI-compatible styles, with palette and grid enforced twice (at generation and in cleanup). Consistent pixel
animation is still limited to small canvases and needs human touch-up for hero-quality frames.

### Cited Findings
- The Sprite Fusion Pixel Snapper README states that AI image models "can't understand grid-based pixel art". The
  results have pixels of inconsistent size and position, a grid resolution that drifts across the image, and colours
  not tied to a strict palette. The tool snaps pixels to a grid, quantizes to 16 colours by default (or a custom palette
  via `--palette`), auto-detects the pixel size and keeps dithering intact. It is MIT-licensed — [GitHub (fetched)](https://github.com/Hugo-Dz/spritefusion-pixel-snapper).
- unfake.py (MIT) "improves AI-generated pixel art through scale detection, color quantization, and smart
  downscaling":
  - scale detection from run lengths, falling back to edge detection;
  - Wu colour quantization, with a cap, automatic count or fixed palette;
  - grid snapping;
  - downscaling by dominant colour, median, mode, mean or a content-adaptive method (Kopf and Lischinski, 2011);
  - optional morphological noise removal, stray-diagonal removal, alpha binarization and flood-fill background removal.

  It credits unfake.js (Eugeniy Smirnov) — [GitHub (fetched)](https://github.com/painebenjamin/unfake.py); [PyPI](https://pypi.org/project/unfake/).
- Other cleanup tools:
  - DeAI PixelKit, an Aseprite extension that removes messy backgrounds and snaps fake pixel grids back to true
    resolution, locally — [itch.io](https://onlyjasoncando.itch.io/deai-pixelkit).
  - Tidy Pixel Art, a web tool — [Phaser](https://phaser.io/tools/tidy-pixel-art).
  - Scenario's Pixel Snapper (MIT) — [Scenario help (vendor)](https://help.scenario.com/articles/1753861053-pixel-snapper-the-essentials).
  - RepairAI Pixel, an offline Eagle plugin port with an 8 to 64 colour palette — [Eagle community](https://community-en.eagle.cool/plugin/a02aa42e-9d55-41a2-9e49-3f4df500b6c5).
- **Retro Diffusion** models are described as designed for grid-aligned, style-consistent, limited-colour output and
  developed with experienced pixel artists. RD Plus was trained at native resolutions of 256x256 or below "without
  aliasing" and covers more than a dozen styles, including UI icons and top-down maps. RD Fast has 15 styles; there are
  also RD Tile and RD Animation. All accept a palette image for strict colours and explicit width and height, though
  some styles allow only certain sizes — [Replicate blog](https://replicate.com/blog/retro-diffusions-pixel-art-models-are-now-on-replicate); [Replicate: rd-plus](https://replicate.com/retro-diffusion/rd-plus); [Replicate: rd-fast readme](https://replicate.com/retro-diffusion/rd-fast/readme); [Scenario essentials (vendor)](https://help.scenario.com/en/articles/retro-diffusion-models-the-essentials/).
- **PixelLab** works at 16 to 256 px for skeleton animation, up to 128 px for 8-direction rotation and up to 64 px for
  automatic animation (see section 3) — [PixelLab docs](https://www.pixellab.ai/docs/tools/skeleton-animation).
- A competitor's guide (LlamaGen) calls PixelLab one of the strongest pixel tools for game developers. It also lists
  checks for production work, including consistent animation frames, transparent exports and a licence check — [LlamaGen (vendor of a competing tool), 2026](https://llamagen.ai/pt-br/articles/best-ai-pixel-art-generators-2026).
- **The idle genre is crowded with lookalikes.** PC Gamer counted 54 idle games released on Steam in one week. The vast
  majority had an AI disclosure, and many had "strikingly similar" interfaces — [PC Gamer, 2026 (undated)](https://pcgamer.com/gaming-industry/steam-week-in-review-54-idle-games-released-on-steam-last-week-and-many-look-eerily-similar).

### Inferences
- **Pixel art fits LootFall's current pipeline.** The game already uses pixel icons of about 20 to 55 px, scaled by whole
  numbers into 64 to 80 px boxes, and pixel actors at 2x. That is inside the native range of the pixel-specific models
  (256 px or less) and of the snapping tools.
- **A fixed project palette can be enforced twice:** as Retro Diffusion's palette input at generation, and again by
  unfake.py or the Pixel Snapper in a repo script. With a fixed outline colour and light direction, that addresses most
  of the drift listed in section 2.
- **Pixel art hides some AI faults and exposes others.** It hides fine-detail errors because there are fewer pixels to
  get wrong, but grid and palette errors show at once. Automated snapping is the safety net, and a quick human pass on
  every accepted icon is still needed.
- **The medium won't make the game stand out.** Pixel-art idle games with AI disclosures are common on Steam, so
  distinctiveness has to come from art direction: palette, silhouette language and the rarity presentation.
- **Pixel animation stays the weak link.** Keep frame counts low, use the skeleton tools at the actor's native size, and
  plan manual touch-ups. Alternatively, animate static pixel sprites procedurally with whole-pixel offsets and squash so
  the pixels stay square.

### Gaps
- No independent quality comparison of Retro Diffusion and PixelLab, and no pixel artist's assessment, was found.
- No test of whether Retro Diffusion holds quality at non-square backdrop sizes like LootFall's 640x210 bands (its
  native training was 256 px or less).
- The dev.to article "How to tame your AI pixel art" (by the unfake.js author) turned up but couldn't be read.

## 6. Real-world evidence: shipped games, statistics, developer write-ups and player reception

### Takeaway
AI disclosure on Steam has risen fast. About 7% of new releases carried one when the field launched in early 2024, about
20% in 2025, and roughly 31 to 33% in 2026. In-game art is the most common disclosed use (49%). But AI-flagged games
rate lower, earn little, and those that failed used AI for visuals far more often (72%) than for voice or localization.
Backlash concentrates on AI that players can see: calling cards (Black Ops 7), cutscenes (Vapor World), placeholder
textures (Clair Obscur's revoked Indie Game Awards), an idle game's switch to an AI art style (Galaxy Idle Clicker), and
store capsules. Developer opinion has turned sharply negative: 52% say generative AI harms the industry, rising to 64%
of visual artists. Since January 2026 Steam requires disclosing AI used for in-game content and store assets, but not
for development tools.

### Cited Findings
- **Disclosure counts and shares (Steam is self-reported, so these are floors):**
  - Ichiro Lambe counted about 1,000 titles in April 2024 (before 2025, possibly outdated) — [Game Developer, Apr 2024](https://GamaSutra.com/business/ex-steam-labs-dev-claims-1-000-steam-games-use-generative-ai).
  - In July 2025 he counted 7,818, about 7% of the catalogue, with about 20% of 2025 releases expected to disclose — [MP1st, Jul 2025](https://mp1st.com/news/games-released-on-steam-reportedly-used-ai-in-2025); [Gigazine, Jul 17 2025](https://gigazine.net/gsc_news/en/20250717-steam-games-that-use-generative-ai).
  - Totally Human Media's later count is 10,258 (8% of all Steam games), with an estimated $660M combined gross from
    review counts. In-game art is 49% of disclosures and voiceover or dialogue 27%, and most disclosures describe light,
    peripheral use. The count probably dates from late 2025 — [Totally Human Media](https://www.totallyhuman.io/blog/games-with-ai-disclosures-have-grossed-an-estimated-660m-on-steam); [WN Hub](https://wnhub.io/news/analytics/item-49306).
  - PC Gamer's coverage gives 10.9% of 2024 releases, 19.9% of 2025 and 30.8% of 2026 releases to July, and projects
    more than half of new releases by 2027 to 2028 — [PC Gamer, Jul 2026](https://www.pcgamer.com/gaming-industry/steam-week-in-review-take-cover-because-it-looks-like-more-than-half-of-steam-games-will-have-an-ai-disclosure-by-2027-2028/).
  - Another PC Gamer column: 120 of more than 300 releases in one week had disclosures — [PC Gamer, 2026](https://www.pcgamer.com/gaming-industry/steam-week-in-review-more-than-300-games-released-on-steam-last-week-and-120-of-them-had-ai-disclosures/).
  - A widely shared "36%" figure for Steam Next Fest comes from a third-party tally; Valve has published none — [Digital Citizen, 2026](https://www.digitalcitizen.life/steam-next-fest-ai-disclosure-36-percent-third-party-tally/); [Llama & Griffin, Jun 2026](https://www.llamagriffin.com/Data/SteamNextFestJune2026/part-2.html).
- **Sulka Haro's census** (July 2026; 53,597 Steam releases from mid-2023 to mid-2026):
  - About one in three new releases carries the disclosure, up from about 7% when the field launched in early 2024.
  - AI-flagged games account for 60 to 90% of the growth in monthly releases, but only about 10 to 27% of estimated
    sales in the latest stretch. The top 1% of titles take about 94% of estimated revenue.
  - AI-flagged games rate about 4 to 6 points lower.
  - Among AI-flagged games that failed to find an audience, 72% used AI for visuals. Successful ones leaned more on
    voice (24% against 8%) and localization (18% against 6%).
  - About 22% of successful AI games added the flag after launch.
  - The flag is binary: one upscaled texture and an entirely generated art set look the same.
  - Haro: "Almost none of them make money – because almost no games do."

  — [Sulka Haro, "Three years of AI on Steam", Jul 2026](https://fragwyz.substack.com/p/three-years-of-ai-on-steam); [Sulka Haro, Part 2](https://fragwyz.substack.com/p/ai-on-steam-part-2); [GamesRadar, Jul 2026](https://www.gamesradar.com/games/steam-study-of-over-53-000-games-finds-60-90-percent-of-the-growth-in-monthly-releases-on-valves-store-is-from-games-using-ai-and-almost-none-of-them-make-money/); [Cinevva, Jul 20 2026](https://app.cinevva.com/news/2026-07-20-steam-ai-disclosure-study); [OtakuKart, 2026](https://otakukart.com/steam-study-of-53000-games-finds-ai-titles-drive-up-to-90-of-new-releases-but-few-become-commercial-hits/).
  One summary of the 72% figure was cut off mid-sentence; the voice and localization numbers come from an earlier,
  fuller summary.
- A Game Oracle study of nearly 10,000 Steam releases, reported second-hand, found AI-assisted games have a median of 4
  reviews against 14, average ratings of 74% against 80%, and are 16% less likely to turn a profit. The method wasn't
  visible — [FeedBagel](https://feedbagel.com/post/ai-is-ruining-game-sales-numbers-show).
- **Valve's rules.** Around January 16 to 19, 2026, Valve rewrote the disclosure form. Developers no longer disclose AI
  tools used in development (efficiency tools, code), but must still disclose AI used to make in-game content, store
  page assets or marketing, and AI that generates content during play. Players can report content through the Steam
  overlay. In June 2026 Epic's Tim Sweeney called the tags "irresponsible" — [Gigazine, Jan 20 2026](https://gigazine.net/gsc_news/en/20260120-steam-updates-ai-disclosure-guidelines); [Slashdot, Jan 19 2026](https://games.slashdot.org/story/26/01/19/1735231/valve-has-significantly-rewritten-steams-rules-for-how-developers-must-disclose-ai-use); [Notebookcheck, Jan 2026](https://www.notebookcheck.net/Steam-updates-AI-disclosure-form-requiring-developers-to-report-visible-and-in-game-AI-but-not-background-tools.1206103.0.html); [Let's Data Science, Jun 2026](https://letsdatascience.com/news/epic-ceo-criticizes-valve-steam-ai-disclosures-06e02306).
- **Developer sentiment.** GDC's 2026 State of the Game Industry survey (about 2,300 respondents, published January 29,
  2026):
  - 36% use generative AI at work: 30% at studios, 58% at publishers, support and marketing firms.
  - 52% believe it is hurting the industry, up from 30% the year before and 18% the year before that. Only about 7%
    see a positive effect.
  - Visual and technical artists are the most negative (64%).

  — [BusinessWire, Jan 29 2026](https://www.businesswire.com/news/home/20260129438528/en/2026-State-of-the-Game-Industry-Report-Reveals-Widening-Effect-of-Layoffs-Broader-Perspectives-on-Generative-AI-Unionization-Tariffs-and-More); [GamesBeat, Jan 2026](https://gamesbeat.com/state-of-game-industry-survey-sheds-light-on-layoffs-genai-views-unionization-and-tariffs-gdc-survey/).
- **Notable cases:**
  - **Clair Obscur: Expedition 33.** The Indie Game Awards revoked its Game of the Year and Debut Game awards around
    December 20 to 22, 2025, because generative AI had been used. The studio said AI was briefly used for temporary
    textures that weren't meant to ship (reportedly patched out around launch); the awards' rules ban AI at any stage — [Malay Mail, Dec 21 2025](https://www.malaymail.com/news/tech-gadgets/2025/12/21/indie-game-awards-rescinds-honours-from-clair-obscur-expedition-33-for-genai-use/202718); [Push Square, Dec 2025](https://www.pushsquare.com/news/2025/12/ps5-fave-expedition-33-stripped-of-indie-game-awards-but-not-for-the-reasons-you-may-think).
  - **Call of Duty: Black Ops 7** (launched November 14, 2025). Players found calling cards in a Ghibli-like style they
    took for AI. Activision said it uses "a variety of digital tools, including AI tools" without naming the assets, the
    Steam page carried an AI notice, and some players got refunds — [MP1st, Nov 2025](https://mp1st.com/news/activision-responds-to-ai-claims-in-black-ops-7); [Gfinity, Nov 2025](https://www.gfinityesports.com/article/activision-confirms-ai-usage-in-black-ops-7-after-calling-cards-backlash); [Hitmarker, Nov 2025](https://hitmarker.net/news/call-of-duty-black-ops-7-faces-backlash-over-ai-generated-calling-card-artwork-1576825).
  - **Vapor World: Over The Mind** (2026): its AI cutscenes were pulled after a mostly negative launch (see section 1) — [PC Gamer, 2026](https://www.pcgamer.com/gaming-industry/steam-week-in-review-a-touch-of-ai-is-all-it-takes-to-trigger-backlash-as-a-promising-new-indie-falls-afoul-of-slop-skeptics/).
  - **Codex Mortis** (demo December 2025) billed itself as 100% AI-made, with art from ChatGPT. Coverage describes a
    largely negative reception ("AI garbage", "slop") — [Creative Bloq, Dec 2025](https://creativebloq.com/3d/video-game-design/is-the-first-100-percent-ai-video-game-the-ultimate-rage-bait); [Xataka, Dec 2025](https://www.xataka.com/videojuegos/primer-videojuego-hecho-integramente-ia-llega-a-steam-rechazo-jugadores-ha-sido-casi-unanime).
  - **Galaxy Idle Clicker** (idle genre; undated). After an art change reportedly made with AI, recent reviews flipped:
    68% of the last 70 were negative, against 70% positive of 762 overall. Long-time players who had bought cosmetics
    called it a downgrade — [Notebookcheck](https://www.notebookcheck.net/Steam-launch-New-free-to-play-idle-clicker-game-hits-Steam-faces-immediate-backlash-over-artwork.1081692.0.html).
  - **Revolution Idle** (idle genre) is well liked for its design, but drew criticism for AI art, especially without
    clear labelling. This rests on a summary; the article was apparently a Notebookcheck piece on a free idle game that
    reached first place on Steam — [Notebookcheck](https://www.notebookcheck.net/Free-to-play-this-free-idle-game-is-purely-addictive-and-climbs-to-1-on-the-Steam-charts.1009139.0.html).
  - **Beyond Words:** its art drew negative feedback for AI use, and reviews were mixed — [Wikipedia](https://en.wikipedia.org/wiki/Beyond_Words_(video_game)).
  - **"Hardest":** a developer pulled this partly AI-made free game from Steam in January 2026 — [Futurism, Jan 2026](https://futurism.com/artificial-intelligence/indie-developer-deleting-entire-game-ai).
  - **Store capsules:** a PC Gamer column calls AI-generated capsule art spam that makes browsing Steam less fun — [PC Gamer, 2026](https://www.pcgamer.com/gaming-industry/steam-week-in-review-spammy-ai-generated-capsule-art-is-a-pox-and-it-makes-browsing-steam-less-fun/).
- **Where players draw the line.** One analysis argues players object to AI substituting for the parts of a game they
  look at (cutscenes, character design, key art) rather than to AI-assisted development in general — [IndieForGames, 2026](https://indieforgames.com/steam-ai-art-backlash/).
- **Developer write-ups (anecdotal).**
  - A solo developer on Steam (Catvivors) found AI decent for basic 2D sprites, but complex, beautiful art still
    required human artists or bought asset packs — [Medium](https://pchojecki.medium.com/ai-helped-me-solo-dev-a-game-on-steam-f84a2425be10).
  - itch.io developers say AI art "is not as effortless as some may think" and that many elements need adapting and
    refining. Commenters criticise games that look AI-made — [itch.io post](https://itch.io/post/13013818) (one of several itch.io posts in the result set).
  - A solo developer used AI character sheets to test silhouettes and palettes before commissioning art. The illustrator
    said the sheets saved at least two rounds of revisions — [Nerdbot, Aug 19 2026](https://nerdbot.com/2026/08/19/five-ways-an-ai-picture-maker-actually-earns-its-keep-in-solo-game-dev-not-just-hype/) (attribution likely, not certain).

### Inferences
- **Visible AI art carries the reception risk; voice and localization don't.** AI used for visuals is associated with
  failure in Haro's census, and the controversies cluster on visible art. This is a correlation: the binary flag can't
  separate "AI art" from "low-effort game". Still, an idle game made of mostly AI-generated visuals starts in the
  statistically weakest group, and it has to earn trust through coherence and polish.
- **Idle-genre players will notice.** AI-disclosed lookalike idle games are common, and an existing idle game's switch
  to AI art drew a review backlash from invested players. A consistent, deliberate style and honest disclosure matter
  more than in less crowded genres. Changing the art style after launch is risky.
- **Disclose up front.** LootFall's AI-generated in-game art would need a disclosure under the January 2026 rules. The
  cases where AI was found later (Black Ops 7, Clair Obscur's placeholder textures) caused more damage than a disclosure
  made from the start.
- **Hide the tells.** "AI artefacts" and inconsistent character models are what reviewers name. The art direction
  should make those hard to produce or easy to remove: few details, locked palettes, uniform outlines, procedural
  motion.

### Gaps
- No verified list of commercially successful games whose in-game 2D art is mostly AI-generated was found. Totally
  Human's post apparently lists unit estimates, but the titles weren't visible.
- No player survey specific to idle or incremental players' tolerance of AI art.
- No detailed developer postmortem of a full AI 2D art pipeline for a shipped Steam game was found. The write-ups are
  anecdotal.
- Primary pages (Haro's Substack, PC Gamer, Totally Human) couldn't be fetched, so the exact figures rest on search
  summaries.

## 7. Overall: AI-strong and AI-weak 2D asset types, and what that implies for the art style

### Takeaway
AI is strong where each asset is a single static image and small differences between assets don't matter: portraits,
monster and character illustrations, painted backgrounds, individual icons, concept art. It is moderate where many
assets must match: icon sets in the thousands, a bestiary with biome variants, parallax layers. That works with locked
references and deterministic cleanup, but costs review time. It is weak wherever exact geometry or coherence over time
is needed: 9-slice UI, VFX flipbooks, frame-by-frame animation, rigs for non-humanoid creatures. For LootFall this
points to a style defined by things a script can enforce (palette, outline, light direction, size), static art with
procedural motion, and rarity, UI frames and VFX motion kept in code. Visible AI art also carries the reception risk,
which the style has to manage.

### Cited Findings
- AI suits prototypes, jams and placeholder art; polished hero characters and final animation frames still need human
  cleanup, by a competitor's own account — [LlamaGen (vendor of a competing tool), 2026](https://llamagen.ai/pt-br/articles/best-ai-pixel-art-generators-2026).
- A solo Steam developer found AI decent for basic 2D sprites, while complex, beautiful art still required human artists
  or bought packs — [Medium](https://pchojecki.medium.com/ai-helped-me-solo-dev-a-game-on-steam-f84a2425be10).
- Players' objections centre on AI in the parts of a game they look at (cutscenes, character design, key art) — [IndieForGames, 2026](https://indieforgames.com/steam-ai-art-backlash/).
- Among AI-flagged Steam games that failed, 72% used AI for visuals — [Sulka Haro via GamesRadar, Jul 2026](https://www.gamesradar.com/games/steam-study-of-over-53-000-games-finds-60-90-percent-of-the-growth-in-monthly-releases-on-valves-store-is-from-games-using-ai-and-almost-none-of-them-make-money/).
- Icon sets drift without locked framing, light, outline and palette (by about the 30th icon in a vendor's example) — [Layer (vendor), 2026](https://layer.ai/use-cases/game-icon-sets).
- AI UI pieces come out with mismatched borders and corners, and 9-slice preparation is manual — [SpriteCook (vendor), Aug 2026](https://www.spritecook.ai/blog/generate-game-ui-with-ai).
- Diffusion video flickers frame to frame, and upscaling magnifies it — [Topaz Labs community](https://community.topazlabs.com/t/deflicker-for-ai-videos/42459).
- General models can't produce grid-true pixel art without snapping — [Sprite Fusion Pixel Snapper (fetched)](https://github.com/Hugo-Dz/spritefusion-pixel-snapper).
- Layer-splitting rigs are drafts, "an existence proof, not a rate" — [rig-parts (fetched)](https://github.com/firejune/rig-parts).
- **Copyright** (relevant to a commercial release). On March 2, 2026 the US Supreme Court declined to hear Thaler v.
  Perlmutter, leaving in place the D.C. Circuit's requirement of human authorship. The Copyright Office's January 2025
  report says prompts alone are not enough for protection, and the Office has rejected registrations of Midjourney
  images. How much human input is enough remains open — [Mayer Brown, Mar 11 2026](https://www.mayerbrown.com/en/insights/publications/2026/03/supreme-court-denies-review-in-ai-authorship-case); [IPWatchdog, Mar 2 2026](https://ipwatchdog.com/2026/03/02/supreme-court-denies-thalers-latest-attempt-register-copyright-ai-generated-image/).

### Inferences
**Classification (my synthesis of sections 1 to 6; the cleanup estimates are qualitative):**

| Asset type | AI quality ceiling | Main failure modes | Cleanup | Best-fit approach |
|---|---|---|---|---|
| Portraits (hero, NPCs) | Near-professional per image | "AI look", hands and detail artefacts, inconsistency across a cast | Low per image | Frontier model + 8 to 14 references or a style LoRA; human pass |
| Monster illustrations (static) | Near-professional per image | Design drift between variants, artefacts | Low to moderate | One base per monster, biome variants by editing or palette swap |
| Single item icons | High | Perspective, light and outline drift; generic silhouettes; no alpha in some models | Low per icon | Locked frame, light and palette; transparent-capable model or matte step |
| Icon sets in the thousands | Moderate | Drift across batches, sameness across 149 slot types, curation load | Moderate (mostly review) | Families (base × variants), scripted checks, rarity in code |
| Painted backgrounds | High | Tiling seams, perspective oddities | Low | Generate the band; check that it tiles |
| Parallax layers | Moderate | Layer separation, invented hidden areas | Moderate | Generate per layer on a key colour, or Qwen-Image-Layered |
| UI frames, 9-slice | Low to moderate | Uneven borders and corners, edges that don't stretch | High | Hand-made or procedural frames; AI only for textures |
| VFX flipbooks | Low | Frame coherence, semi-transparency, no alpha | High | Procedural particles and shaders; AI single-frame elements on black (additive) |
| Frame-by-frame animation | Low to moderate (best at 64 to 256 px pixel art) | Drift, flicker, loop closure, pivot jitter | High per animation | Few key actors only; otherwise static art + tweens |
| Cut-out rigs | Emerging (humanoid anime) | Invented hidden parts, depth errors, non-humanoids unsupported | Moderate to high | Hero and bosses only, if anything |
| Pixel art (static) | High with pixel-specific models + snapping | Fake pixels, palette creep (general models) | Low with scripts | Retro Diffusion or PixelLab at native size + unfake / Pixel Snapper |

**What this implies for LootFall's art direction:**
- **Clarity first.** Make rarity, slot and power readable from shapes and colours drawn in code (frames, glows, colour
  ramps, particle tiers), not from generated detail. At 64 to 80 px, players read silhouette and colour. A generator
  should never be trusted to encode 12 rarity tiers consistently.
- **Pick a style a script can enforce.** Bold, uniform outlines; a fixed light direction; a limited project palette (or
  per-rarity ramps); a consistent framing box. Every one of these can be checked and corrected automatically, which is
  how drift across thousands of icons gets contained. Both pixel art and bold-outline "sticker" or flat-shaded styles
  qualify. Soft painterly rendering and photorealism are harder to normalise.
- **Keep motion procedural.** Static monster and hero art moved by tweens and effects scales to many monster types and
  biomes without per-frame cleanup. Hand-cleaned AI frames or a cut-out rig are worth it only for the knight and the
  bosses.
- **Volume through families.** Generate a curated base per slot archetype and per monster, then make variants by
  editing models and palette swaps. The game already palette-swaps the knight's blade and cape and ships ember, ash,
  shade and gilded reskins.
- **Keep the pipeline in the repo.** Off-session generation, then in-repo deterministic cleanup and checks: matting,
  pixel snapping and quantizing, outline pass, auto-crop and centring, and a contact sheet for review. This matches the
  project's "rebuild assets/ from a script" rule and its cloud-session constraint (only GitHub is reachable).
- **Manage reception.** Disclose AI art on Steam. Avoid generic AI capsule and key art, the most-criticised use. Keep a
  coherent, deliberate look from launch rather than switching styles later. Human edits and curation also strengthen
  any copyright claim. That last point is my inference from the Copyright Office's "prompts alone are insufficient";
  this research didn't cover what counts as enough.

### Gaps
- No controlled study compares how players perceive a polished, consistent AI-assisted 2D style against hand-made art.
  The classification above is inferred from tool documentation, vendor material, anecdotes and reception cases, not
  measured.
- No quantitative cleanup-hour data per asset type was found anywhere, so the cleanup column is qualitative.
- Newer model generations (unverified "FLUX 3", "GPT Image 2.5", "Gemini 3.7 Flash") may shift the ceiling for icons and
  transparency; they couldn't be checked before the search budget ran out.
