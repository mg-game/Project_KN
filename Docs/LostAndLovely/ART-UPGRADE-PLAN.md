# ART-UPGRADE-PLAN — LOST & LOVELY, max-quality art pass

- **Date:** 2026-11-04. **Role:** art producer. This is a plan only. Nothing has been generated and no project file was edited.
- **Sources:**
  - the audit JSON (200 assets, embedded in the task);
  - `polish_plan.json` (its `dropped` list and conventions);
  - `polish_art_reports.json`;
  - the audit folders `artmax/{chars,eis,icons,uisb}`;
  - `FX-JUICE-SPEC.md` §5 (FX workflow budget);
  - `SPEC4.md` §2.8 and §10.1;
  - the memories `lost-and-lovely-art-style`, `graphics-via-higgsfield` and `main-screen-ia`.
- **User directive:** "크레딧충전했으니까마음껏써 품질최상으로 전부다" (use credits freely, top quality, everything). Memory as of 2026-11-04: quality **max**, **one asset per image** (no grid sheets), regenerate anything that is not excellent, keep a balance floor for each workflow, and tell the user when "everything at max" needs another top-up.
- **Out of scope:**
  - `Art/VFX/**` and `Art/FX/**` belong to the FX-JUICE workflow.
  - `Art/Battle/field*.png` belongs to the screen-frame workflow.
  - This plan only *reads* `art2/raw/vfx_*`, as motif references for the skill icons.

Path shorthand used below:
- `SCR` = `C:/Users/PC/AppData/Local/Temp/claude/d--UnityProject-unity-idle/5af81621-3ac1-471f-b619-0795eeedc9ed/scratchpad`
- `ART` = `D:/UnityProject/unity_idle/Assets/_Project/Art`

---

## 1. Summary and totals

### 1.1 Prices used
| Item | Credits | Evidence |
|---|---|---|
| gpt_image_2_5 **max @1k** | 5 | Task brief and ledger (`transactions`): story jobs at 5 |
| gpt_image_2_5 **max @2k** | **9** | Ledger: −9 on 2026-11-03 06:53 UTC, the only 2k job (bg_night 1520×2688). The ui-primitives report gives xhigh@2k as 4.5 = 1.8 × 2.5, and 1.8 × 5 = 9. Run a `get_cost` preflight before the first 2k job. |
| upscale_image (bytedance, the only provider of this tool) | 2 flat | `get_cost` preflight by the eis auditor; no job was submitted |
| outpaint | 2 | Ledger |
| 4k | not used | The importer caps textures at 2048, and every final asset is ≤ 2048 |

`models_explore` returns no prices. It confirms the gpt_image_2_5 parameters: `quality` low…max, `resolution` 1k/2k/4k, `background` auto/opaque/transparent, `variant` flare/sunburst, and the aspect ratios used here (1:1, 2:3, 3:4, 16:9, 21:9). Topaz (`topaz_image`, which has "Text Refine" and "High Fidelity V2" variants) is in the catalog as a model, without a price.

### 1.2 Batches, in priority order (credits = base + re-roll reserve)
| Wave | Batch | Content | Images | Base | Reserve | Total | Cumulative |
|---|---|---|---|---|---|---|---|
| W0 | **B0** | Free re-exports and clean-ups: 39 characters at 1024, threadweb haze, items, synergy and trait icons at 512, zones and banners, moon | 0 | 0 | 0 | **0** | 0 |
| W1 | **B1** | Story intro_1..3, max@2k | 3 | 27 | 18 | 45 | 45 |
| W1 | **B2** | Enemies: dust, moth, windup, thimble, mothqueen | 5 | 25 | 10 | 35 | 80 |
| W1 | **B3a** | Button core: primary, gold, secondary, disabled | 4 | 20 | 5 | 25 | 105 |
| W1 | **B4** | Ribbons: night, gold, lavender | 3 | 15 | 5 | 20 | 125 |
| W1 | **B6a** | Skill icons, the 8 high-priority ones | 8 | 40 | 5 | 45 | 170 |
| W1 | **B7a** | currency_event | 1 | 5 | 0 | 5 | 175 |
| W2 | **B8a** | Feature-unlock banners ×9 (cards shown today) + `feature_level_up` medal | 10 | 50 | 10 | 60 | **235** |
| W2 | **B5** | IA frame: topbar_9s and navbar_9s (2k), nav_main_button, sheet_frame_9s. **Waits for WP-IA geometry.** | 4 | 28 | 14 | 42 | 277 |
| W3 | **B9** | Synergy emblems ×5 | 5 | 25 | 5 | 30 | 307 |
| W3 | **B3b** | State 9-slices ×9: soft, tag, tab on/off, switch on/off, attendance ×2, nameplate | 9 | 45 | 10 | 55 | 362 |
| W3 | **B10** | Items: mat_spring, mat_rib | 2 | 10 | 5 | 15 | 377 |
| W3 | **B7b** | Icons: stage_blackout, power, quest_stage, quest_repeat, mission_kill, mission_boss | 6 | 30 | 5 | 35 | 412 |
| W3 | **B6b** | Skill icons, the other 25 | 25 | 125 | 20 | 145 | 557 |
| W4 | **B8b** | Banners for the 6 cards SPEC4 turns on, 4 medals, bag_header | 11 | 55 | 10 | 65 | **622** |
| W5 | **OPT** | Optional: tangle; seven_lights; codex banner + medal; udu/lala lineage restyle ×6 (+10 reserve); threadweb 2k regeneration; concept upscales ×6 (2 each) | 11 (+6 upscales) | 71 | 10 | 81 | **703** |

**This plan: 622 credits without the optional items (500 base + 122 reserve), 703 with everything.** That is 107 generations and 6 upscales.

### 1.3 What fits now and what needs a top-up
| Line | Credits |
|---|---|
| Balance (`balance`, 2026-11-04; nothing spent since the +500 grant at 2026-11-03 15:03 UTC) | 530.38 |
| − FX-JUICE workflow budget (FX-JUICE-SPEC §5.1) | −220 |
| − screen-frame outpaint for field_tall (estimate) | −20 |
| − SPEC4 P4-10 floor (its own §10.1 plan at high costs 27.5) | −40 |
| **= available to this plan now** | **≈ 250** |

- **Fits now (235):** W0 + W1 + B8a. That covers the first-run story, the battle field, the most-tapped buttons, the boss ribbon, the HUD skill badges and the first-session unlock cards. **Balance floor rule:** before every submission, check `balance ≥ cost + floor`, where `floor = (220 − credits FX-JUICE has already spent) + 60` (frame 20 + P4-10 40). That is 280 before FX-JUICE starts and 60 after it reports done.
- **Needs a top-up:** B5 onward. That is 387 credits (non-optional) or 468 (all).
- **Everything at max, all workflows:** FX 220 + frame 20 + this plan 703 + SPEC4 §10.1 re-costed at max with one asset per image (A1 15 panels 75, A2 9, A3 20, A4 15, A5 5, A6 15, A7 20, A8 20, A9 9, A10 5, A11 35 = 228, + 15 % = 262) = **≈ 1205**.
  - The shortfall is **≈ 675 credits** (≈ 594 without W5).
  - **Tell the user:** one 1,000-credit pack covers everything, with ≈ 325 left for the IDLE_RESEARCH2 feature art (pets, tower, world boss and so on), which is not yet specified.

---

## 2. Rules for every batch

### 2.1 Generation parameters
`model: gpt_image_2_5`, `variant: flare` (default), `quality: max`, one asset per image, `count: 1`.
- **1k** for every sprite, icon, UI frame, ribbon, banner and header. The final size is always ≤ 1344 px, so the raw is ≥ 2× most finals.
- **2k** only for the 3 story pages (final 1364×2048) and the 2 IA bars (cap art must be downsampled, not upscaled).
- `background: transparent` for sprites, icons and UI. `opaque` for story pages, banners and headers.
- Use `generate_image_batch` for independent prompts (≤ 12 per call), then `jobs_wait`, then one `show_generation_by_ids`.
- On a 429, re-submit once. On a transport timeout, never re-submit blindly. Call `get_preferences` before the first job to decide the destination folder.

### 2.2 Prompt assembly (exact)
`PROMPT = [MASTER] + "\n\n" + [STYLE block of the batch] + "\n\nSUBJECT: " + subject + "\n\nREFERENCES: " + reference roles`

- Prompts use English names only. No Hangul, which the model may render as text.
- Pass references in `medias[]` with `role: image_references`, in the order listed. Prefer **job ids** (stable). If a media id is rejected, re-upload with `media_upload` → PUT → `media_confirm`.

**[MASTER]** (verbatim from the art-style memory):
> A whimsical 2D mobile game art style with a soft fairy-tale tone, cute but slightly eerie charm, and strong collectible RPG appeal. Characters should feel like living objects born from forgotten items, with the object's original structure, material, and function clearly reflected in the body, silhouette, and animation. The art should look game-ready rather than overly painterly: clean readable shapes, clear outer lines, soft cel shading, gentle gradients, warm dreamy lighting, and strong silhouette readability on small mobile screens. Use a cozy magical night-world atmosphere with pastel purple, pink, blue, cream, and restrained gold accents. Characters must be memorable, expressive, chibi-friendly, and IP-friendly, suitable for battle sprites, character cards, home screens, stickers, plush toys, and merchandise. Overall tone: soft storybook fantasy, magical toy-like charm, adorable but strange, elegant but simple, polished 2D mobile RPG aesthetic.

**[S-SCENE]** (opaque story pages and banners):
> Full-bleed storybook game illustration, opaque, no border, no frame, no vignette box, no text, no letters, no numbers, no logo, no UI elements, no watermark. Clean readable shapes with clear medium-weight dark-plum (#2A1F4D) outlines on characters and props, soft cel shading with gentle gradients, warm lamp light against an indigo night (#1E1838 to #2F2552), lavender and dusty-pink rooftops, cream highlights, restrained gold. Every character is exactly on-model with its reference image (same object-body design, colours, eyes and accessories), chibi proportions about 2.5 heads tall.

**[S-WIDE]** (appended after S-SCENE for 21:9 banners):
> Wide 21:9 banner. The main subject and every character sit inside the central 60% of the width and the middle 70% of the height; the outer 20% on each side and the top and bottom 15% are calm background that will be cropped away.

**[S-SPRITE]** (enemies and items, transparent):
> One isolated 2D game sprite on a fully transparent background, centred, whole body visible, nothing cropped, filling about 82% of the canvas height. Clear medium-weight dark-plum (#2A1F4D) outer outline about 3% of the sprite width, thicker than the few inner lines; flat cel shading: about 70% flat colour blocks, 20% soft gradient, 10% highlight; light from the top-left. Big simple shapes that still read at 90 px on a phone. No ground plane, no cast shadow, no glow, haze or halo outside the outline, no fine fibre or speckle micro-texture, no text, no photorealism, no 3D-render look.

**[S-ICON]** (all icons and medals, transparent):
> One isolated game icon on a fully transparent background, centred, filling about 86% of the square, nothing cropped, no badge, plate, frame or background shape behind it. One bold hero shape plus at most two small secondary details; it must read at 24 px. Thick dark-indigo (#2A1F4D) outer outline about 4% of the icon width; bright, high-value core colours so it pops on a dark indigo medallion and on a cream panel; flat cel shading (70% flat colour, 20% soft gradient, 10% sparkle); light from the top-left. No outer glow, haze, smoke or soft halo outside the outline, no text, no letters, no numbers.

**[S-EMBLEM]** (synergy):
> One round synergy emblem medallion, front view, on a fully transparent background, centred, filling 90% of the square. Copy the frame of reference image 1 exactly: the same thick polished gold rim, rim thickness and bevel, exactly 4 small diamond studs at 12, 3, 6 and 9 o'clock, and the same deep-indigo night-sky interior with a few tiny stars. Inside it, one big hero subject fills about 70% of the interior. Thick dark-indigo outlines, flat cel shading, strong value contrast; it must read at 44 px. No text, no ribbon or banner, no glow outside the rim.

**[S-UI9]** (square 9-slices, 1:1):
> One flat front-on UI element for a 9-slice sprite, on a fully transparent background: a single rounded rectangle filling 94% of the square canvas, corner radius about 12% of its width, perfectly symmetric left-right and top-bottom, straight parallel edges. All ornament (rim, piping, stitching, studs) stays inside the outer 25% band of each side; the inner 50% is one plain uniform fill with no texture, no pattern, no sparkle and no horizontal variation (a gentle top-to-bottom value change is allowed). Clean anti-aliased outer edge, no drop shadow, no outer glow, no text, no icons.

**[S-CAPSULE]** (192×96 capsules; 16:9 canvas):
> One flat front-on horizontal capsule (stadium shape with fully round ends, length exactly 2 x its height) centred on a fully transparent 16:9 canvas, filling 90% of the width. Perfectly symmetric, straight top and bottom edges; ornament only in a narrow rim band; the middle is one plain uniform fill. No drop shadow, no outer glow, no text, no icons.

**[S-RIBBON]** (21:9):
> One horizontal satin storybook ribbon banner, front view, perfectly symmetric, on a fully transparent 21:9 canvas, filling 94% of the width. A long straight band with parallel top and bottom edges whose middle 60% is one plain uniform satin colour (no wrinkles, folds, highlights or stars there); stitched piping along the top and bottom edges; folded swallow-tail ends with a darker under-fold, kept inside the outer 17% of the width on each side. No text, no drop shadow, no outer glow.

**[S-BAR]** (21:9 at 2k):
> One long horizontal UI bar, front view, on a fully transparent 21:9 canvas, spanning the full width and about 22% of the canvas height. Perfectly symmetric left-right. Ornate end caps only inside the outer 20% of the width on each side; the middle 60% is a horizontally repeating seamless pattern with nothing unique in it, so any slice of it can be tiled. Straight top and bottom edges. No text, no icons, no buttons, no drop shadow.

**[S-CHAR-EDIT]** (optional restyle, OPT):
> Redraw the character in reference image 1 as the same character: identical object-body design, pose, proportions, silhouette, colours, face and accessories, full body, centred, feet at the bottom, fully transparent background. Change only the rendering to match the in-game house style of reference image 2: clear medium-weight dark-plum (#2A1F4D) outer outline thicker than the inner lines, minimal inner lines, flat cel shading (about 70% flat colour, 20% soft gradient, 10% highlight), no painterly texture, no semi-realistic anime rendering, no thin sketchy lines. No ground, no cast shadow, no text.

### 2.3 Reference library
Jobs are the 1024 raws; media are uploaded copies. Character look notes for the prompts:
- **jjak (sock doll):** cream knit body, purple ribbed cap, a big purple button eye on the left with X stitches, an X-stitched eye on the right, pink cheek patch, gold hoop earring, purple cuffs and feet, blue body patch.
- **ttok:** round blue button/pocket-watch body, cream ribbon bow, X thread across the face, gold rim, gold bead feet.
- **chal:** slim golden-key girl whose ornate key-bow is her head, lavender bow.
- **mong:** drippy cream wax candle with a flame, in a purple teacup.
- **udu:** tall dark-blue umbrella figure, canopy hood, curled-handle tail.
- **rouge:** pink lipstick in a gold case, gold skirt.
- **lala:** lavender-haired music-box ballerina on a music-box drum.

| Ref | Job id (raw) | Media id |
|---|---|---|
| jjak_base | f983e23c-f5f2-4835-a8ff-78dee64bd475 | 9db87e67-5774-4695-b66d-4efe4715d1f7 (also 4023f67e-3db3-4973-aa63-90529ba98d5c) |
| ttok_base | 00dab067-8abe-4232-993d-825120c855ae | 478b8378-9a21-4130-a13f-abd2dc21e792 |
| chal_base | 4ad012d6-70d6-41cb-ba30-fa930b919f5f | 85f37a4f-88b1-4c80-b9e3-d618e837eaee |
| mong_base | f6d15773-f231-4129-9941-5b03047166f8 | 9e15dccb-5480-48c4-a4b3-86a00f3c1826 |
| rouge_base | d4b9a2b6-b2d4-4cfa-a755-ba67c5285d4f | 342df510-f045-45c1-99a0-f3c1f06c122a |
| udu_base / storm / parasol | d67e1fbe-124c-42ba-a764-a733844bd772 / 1978b1ff-65dc-4df0-94e1-cacebdb45867 / 7d9ff3a8-ec07-4430-98aa-3295d0f46947 | – |
| lala_base / waltz / finale | a832626f-f58f-4a1e-9cb7-0d6717ede570 / a057706d-b69c-49a9-9ceb-e02bb6c73ca5 / c909bb21-1539-4eed-bdd4-5dc50d5715af | – |
| jjak_caravan, ttok_spinner, mong_lantern | 45d4419e-762d-48d0-bb08-7cfe970eda02, 8b1195c4-7c5f-47f4-877d-ac175f31e5f8, 2b3547b6-c123-4ce7-9a7a-bf9e88588e09 | – |
| costumes nightkeeper / cheer / mail / threadweb | 3e492504-1f53-4cf9-8870-76b7098ac792 / 606d50a1-7801-4896-859f-7b21aba826ea / b0487dd3-7548-46c3-a2fd-baac514ddf0a / 1dbd490c-1581-4399-bcd5-dd3feacce6b1 | – |
| User concept crops (`scripts/refs.txt`) | – | nightkeeper 63f11d7e-be50-4242-b5c0-ac2a6218be41, plush 48a1561a-3324-4a1f-a289-859d1ceb4c43, puppet a90632ec-add3-4460-a926-e743b3598b40, sleepcap 3b359e77-ebc3-4d8d-bf1a-022e132d2505, threadweb 2617facc-787c-4e46-b5db-15315c84b28a, caterpillar cacdc520-50f0-41bc-bd98-561f93423afa, costumes5 938dab2e-01ff-42ec-927f-cd5f858bdd59, **UI mock** fc057e95-7af7-41d7-9d47-7c486ced561a |
| Enemies | dust 0cd79557-615d-44a3-9d21-8b5a2cc448c0, moth f127e516-d042-475c-a9e1-eebbc9e9627d, rust 4b2a0a97-5223-4f26-9119-42a7b25e23b5, tangle d8438a67-8324-467a-8a8a-c69a4fba58e2, boss_shadow beb64e6f-71e8-4b66-a831-4e5447235ac5, boss_mothqueen 9a1d6cc3-439c-4afb-9983-656a7e0018ac, thimble 6401d75d-d2ac-494a-b97d-37f6dc1a52f5, windup 0bc97680-66e0-40ec-aa78-b2d79982b155 | rust 7b0bb1a7-4a69-4f9e-af6a-85843a9d4833 |
| Items | cloth 2e488620-4c18-4a75-930f-2323e4e4c94e, thread c7063fbd-4c3b-4bac-82f6-0b385295cc0d, rib 6ed2ff5b-3a50-451a-9eb5-317ca9e53599, spring fe2aebb7-1ef0-4cb4-a23d-3c5c03f1234a | thread 99500a67-32b5-47d1-a185-b9c666b806d8 |
| Icons | currency_gold 7b52dc09-2cb6-4414-b690-3f2af9656380, gem 42e9033a-99fd-421f-94e3-303af46a6723, event c9d7088d-4024-45f2-98ac-ae40db836ee3, lamp b1dc188b-d750-4c8a-8ae4-174e120cce96, star f7b4bb2d-3027-4eb8-a52a-70d184c4ff1f, guardian 4ec25d93-18d3-44d9-b533-f212e140f6bf, echo 8220b67e-f143-4a5a-b9fe-be02ce009c67, trait_fabric f41df481-d38c-4216-a87f-9543668ab8f7, stage_rain 7ec11f5c-0910-49f3-ab17-94d8cb278f20, stage_fullmoon 0b3a33a6-dd57-4df4-8f45-b5af3ab37745, stage_blackout c3579ceb-6439-48f1-9720-4cf2b43227e6 | lamp 70d60977-2cd2-4360-9f40-f99b3088cec0, guardian 865616e0-df7c-47dd-b263-13ab1ec03a18 |
| Synergy | key_and_lock **2a026a9c-dfce-4276-af5a-858e16f53281** (frame master), clear_sky ef3e7e51-6a84-4014-9bde-365bf8b8a400, rainy_lantern cea9bc8f-70d4-46ce-9319-692795c078d0, parade 3c6bb429-b715-4fea-91c4-91a210c5d6e5, shop_family f253ee38-aad6-40b9-836b-02a049d950d2, last_song 01939086-98d0-4d30-8982-1849059b0184, locked_musicbox ba6d6452-4950-4529-8056-8bc71b31611d, seven_lights 915f00e9-21b6-4817-8125-b407a108561d | – |
| Scenes and UI | shop 8a5942a2-65cd-4c05-a0ac-be0f241a2fae, bg_night b937156d-348f-4d49-bc2e-4865342ede36, button_primary_raw bfbc1cca-9265-4aed-babb-93ae110e04f0, button_gold_raw 9448cfa9-1bd2-4db7-8abc-a43cddd6d3a3, panel_night_raw 4ddba8e3-ddd3-4b5a-82d9-7d91e438dbad, panel_parchment_raw 9448fe4e-e0e8-4730-92c3-bf4886e49671, dungeon_banner abf8ffad-33f4-4e71-8b23-985f14af337e, tab_shop 7809fe11-8723-4c05-b1cb-c81be4171516, tab_expedition 78ad59b7-4724-4d0f-b36b-f050834de755 | shop 6c22c00a-50b9-44e2-b10a-43d2479240f1, bg_night 98d38ae5-48d2-4dc6-804d-b9fcb0e513ae, field 73d203b2-0c60-48d7-a559-79b4bb70beec, guardian_pad 581a7a48-041c-474d-b6e0-57a7df92cada, gacha_banner 5ed62193-987f-41f4-bcd5-f72b7cb0f885, button_gold_9s 70b498d6-6087-466d-9dde-c4688c752fb1 |
| Skill-icon motifs (read-only) | `art2/jobs.tsv` `vfx_<key>`, e.g. vfx_candle_burst 6b5820b4-08a9-410b-8700-7348bd246bf9 (all 33 are listed there) | – |

**Upload first; no id exists yet:**
- `ART/Icons/skill_*.png` (33);
- `ART/Icons/{quest,ticket_quick,journal,menu_bag}.png`;
- `ART/UI/{idle_basket_3,journal_chest_closed,ui_medallion_dark,ui_pill_night_9s,ui_bar_velvet_9s,content_bg}.png`;
- `ART/Battle/guardian_pad_{pink,mint,lavender,gold,sky}.png`;
- `ART/Story/intro_2.png`, `ART/Story/intro_3.png`, which hold the approved 1024×1536 compositions;
- `SCR/polish_art/ui-primitives/raw/ui-wide.png`, for the ribbon shapes.

### 2.4 Post-processing toolkit
Everything is staged in `SCR/artmax/gen/<batch>/{raw,out,preview}` with a `jobs.tsv` (id, job id, credits, verdict). Nothing is copied to `ART` until it passes QA. The old file is kept in `SCR/artmax/backup/<path>`.

| Need | Tool |
|---|---|
| Alpha clean-up and halo removal | `SCR/art2/process.py`: `clean_alpha(lo 6/255, hi 248/255)`; `remove_halo(solid, grow, feather)` (creatures 0.7/6/2.5, items and icons 0.92/2/2.5) |
| Fit to canvas | Same file, `fit(mode)`: `feet` = side 4 % / top 4 % / bottom 6 %, feet-aligned (this keeps the EnemyView pivot); `icon` = 6 % margin (5 % for skill icons, to match today's set). Resize with premultiplied Lanczos. |
| Square 9-slices | `SCR/scripts/build_9s.py`. Add FRAMES entries with corner `c ≈ 0.29 ×` trimmed raw width; `flatten=False` for buttons and tabs (keeps the vertical gradient), `True` for panels, cells and sheet. Output is exactly 192×192 with border 48. |
| Capsules and ribbons | `SCR/polish_art/ui-primitives/finish.py`: `capsule()` gives 192×96; `ribbon(arr, W, H, fill_h)` keeps tails inside the borders |
| Opaque cover-crop | `SCR/scripts/process_opaque.py` logic (crop in source space with yfrac, then one Lanczos downscale) |
| **New** IA bars | `SCR/artmax/gen/build_bar.py`: crop the bar, scale to the target height, left/right caps = 320 px, centre tile = 384 px made seamless (50 % offset + 48 px feathered cross-fade). Output 1024×H. |
| **New** attendance cells | `SCR/artmax/gen/build_cell.py`: 192×256 with custom borders L/R 44, T 88, B 44 |
| QA sheets | `artmax/eis/ingame.py` (enemies on a field crop), `artmax/icons/icons_display.py` (icons at display sizes on `ui_medallion_dark` and cream), `artmax/uisb/sheet_story.py` (story safe column), `artmax/chars/qhd_compare.py` |

### 2.5 QA gate ("excellent or regenerate")
Every asset must pass all of these:
1. On-model, checked side by side with its references.
2. Night-pastel palette with gold as an accent only.
3. Medium dark outline present.
4. No text or letters.
5. Real alpha: no baked checkerboard, alpha max 255, 0 detached haze px outside the 3 px dilated silhouette.
6. Nothing cropped.
7. The batch's own display test (listed per batch).

A failure means a re-roll from the batch reserve, at most 2 per asset. After that, stop and report. Never spend past the balance floor in §1.3.

### 2.6 Order dependencies, and parallel lanes (file-disjoint)
- **Lane A:** B1, then B8a, B8b, OPT-codex. B8 reuses B1's look and the new enemy_dust.
- **Lane B:** B2 (mothqueen first, then moth, dust, windup, thimble), then B7b (mission_kill uses the new dust).
- **Lane C:** B3a first (it is the family master), then B4, B3b and B5 (B5 only after WP-IA freezes TopBar and TabBar heights).
- **Lane D:** B6a, B7a, B6b.
- **Lane E:** B9, B10.

---

## 3. Batches and per-asset actions

### B0 — Free work (0 credits; do first)
| # | Asset(s) | Action | Detail |
|---|---|---|---|
| 0 | `SCR/art_raw`, `SCR/art2/raw`, `SCR/polish_art/*/raw` | **Back up** | Zip them to `SCR/artmax/backup/raw_masters.zip`. art_raw is the only local copy of the 1024 masters (also re-downloadable from the job rawUrls). |
| 1 | `Characters/*` ×29, `Costumes/*` ×9 (all but threadweb) | Re-export at a 1024 canvas | Copy `SCR/artmax/chars/reexport/{Characters,Costumes}/*.png`. Fit is 4/4/6 % feet-aligned; downscaled to 512 they match the current files (alpha MAD ≤ 3.46, worst ttok_spinner with its semi-alpha motion arcs; most ≤ 1.3; `reexport_report.json`). Gives 1.04–1.11× more detail where the 400 ref evolution card magnifies them today. Do **not** use the Higgsfield upscaler (no alpha, invents stitches). |
| 2 | `Costumes/jjak_threadweb.png` | Haze clean-up at 1024 | From `art_raw/cos_jjak_threadweb.png`: `a' = a if a ≥ 0.6 else 0.6·(a/0.6)^2.2` (use 1.6 to keep more aura), lift low-alpha RGB by 6 %. Prototype: `artmax/chars/threadweb_cleanup_proto.png`. QA: no lavender veil on white/cream plates; aura still reads on night. |
| 3 | `Items/mat_thread.png`, `Items/mat_suitcase.png` | Halo removal | From `art_raw`: clean_alpha → remove_halo(0.92, 2) → icon 256. Trials: `artmax/eis/trial/mat_thread_g2_s92.png`, `mat_suitcase_256.png`. mat_spring gets the same interim fix only if B10 is postponed. |
| 4 | `Synergy/combo_{key_and_lock,locked_musicbox,mending_duo,night_walk,vanity_secret,waiting_end,seven_lights}.png` | Re-export at 512 | From `art2/raw`, icon mode, canvas 512. Gives 2× headroom in the 192 px detail popup. |
| 5 | `Icons/trait_*.png` ×7 | Re-export at 512 + trait_metal halo fix | From `art2/raw`. For trait_metal: remove_halo(0.85, 3), then zero any pixel with α < 0.6 and luminance > 180 that lies outside the dark outline. |
| 6 | `UI/zone_exp_{1_1..2_4}.png` ×8 | Re-export at 1344×672 | From `art_raw` (1344×752), 2:1 cover-crop, yfrac 0.5. The popup draws them at 888 px, which is 1.73× magnified today. Needs the importer change in §4.3 #18. |
| 7 | `UI/dungeon_banner.png`, `UI/shop_event_banner.png` | Re-export at 1344×504 | yfrac 0.5 and 0.22. Same 2.667 aspect as today. |
| 8 | `UI/moon.png` | Strip the baked halo | Set α to 0 outside the crescent+sparkle mask dilated by 3 px, then re-feather with a 24 px Gaussian ramp. The engine already adds MoonGlow. |
| 9 | `UI/bg_night.png` | Optional re-export at 1152×2048 | From art_raw 1520×2688, yfrac 0.5. Only shows in notch strips. |
| 10 | `Concept/*.png` ×6 | Optional | Pad to dims divisible by 4 (1086→1088; 1122×1402→1124×1404, edge replicate) so PC gets BC7. Better: ask the user for the pre-KakaoTalk originals. |

### B1 — Story pages (P0, 3 × max@2k = 27 + reserve 18 = 45)
- Params: 2k, `aspect_ratio 2:3`, opaque. Style blocks: [MASTER] [S-SCENE].
- Final: `ART/Story/intro_{1,2,3}.png` at **1364×2048 RGB**. Lanczos from the ≈1660×2490 raw; no sharpening unless the edge test is soft. This is the same aspect as today, and Story/ has no mips.
- **QA:**
  - `artmax/uisb/sheet_story.py` safe-column overlay on 9:16 and 9:19.5 envelopes with the 1.07 Ken Burns zoom. Every character must be whole and above the bottom-23 % caption band on 19.5:9.
  - On-model side-by-side with the references.
  - A crop at device scale (1.34×) next to the old page.

| Asset | Action | References (order) |
|---|---|---|
| intro_1 | **New composition** (today two friends are cut off on every phone) | ttok_base job, chal_base job, mong_base job, rouge_base job, bg_night media |
| intro_2 | Same composition, regenerated crisp | upload `ART/Story/intro_2.png`, shop job |
| intro_3 | Same composition, regenerated crisp | upload `ART/Story/intro_3.png`, jjak_base job, shop job |

```
intro_1 SUBJECT: Opening page of the story. A narrow cobblestone night alley in a cozy storybook town under a big pale-cream crescent moon; purple-shingled rooftops, warm street lamps, string lights, a soft lavender mist at the far end. Four small lost living objects walk together down the middle of the alley toward the viewer, a little anxious but brave: (1) the blue round pocket-watch-button friend with a cream ribbon bow on top and X-shaped thread across its face (image 1); (2) the slim golden-key girl whose ornate key-bow is her head, with a lavender bow (image 2); (3) the drippy cream wax candle with a small flame, sitting in a purple teacup (image 3); (4) the pink lipstick friend in a gold case with a gold skirt (image 4). Each friend is about 12% of the image height; the four stand close together in the middle third of the height. Composition rules: all four friends and the moon sit inside the central 60% of the image width (the outer 20% on each side is only walls, lamp posts and sky, safe to crop); the bottom 25% is calm, dark, empty cobblestone with no characters or props (a caption box covers it); the top 10% is calm sky. Match the night palette of image 5.
REFERENCES: image 1-4 = the four characters (copy them exactly), image 5 = palette and town style only.

intro_2 SUBJECT: Recreate image 1 as the same illustration with the same composition, camera, layout, lighting and colours, redrawn crisply at full resolution with clean outlines: a narrow lamplit alley that ends at the little patchwork mending-shop house (purple shingle roof, thread-spool chimney, button windows glowing warm gold, exactly as image 2), paper bunting and fireflies; the shop stands on the street in the upper middle. Keep the shop and every important prop inside the central 60% of the width and keep the bottom 25% as calm dark cobblestones.
REFERENCES: image 1 = composition to keep, image 2 = the shop design.

intro_3 SUBJECT: Recreate image 1 as the same illustration with the same composition, redrawn crisply at full resolution: a close-up of the mending-shop door swinging open with warm light spilling out; the sock-doll hero (exactly as image 2: cream knit sock body, purple ribbed cap, a big purple button eye on the left, an X-stitched eye on the right, pink cheek patch, gold hoop earring, purple cuffs and feet, blue body patch) waves a welcome on the doorstep; fireflies and soft sparkles. Keep the hero and the door inside the central 60% of the width; the bottom 25% stays calm.
REFERENCES: image 1 = composition to keep, image 2 = the hero (copy exactly), image 3 = the shop design.
```

### B2 — Enemies (P0; 5 × max@1k = 25 + reserve 10 = 35; tangle is conditional, see OPT)
- Params: 1k, 1:1, transparent. Style blocks: [MASTER] [S-SPRITE].
- Post: `process.py` feet mode with remove_halo(0.7, 6). Enemies at 256 (same canvas, feet at bottom 6 %, so the EnemyView pivot is unchanged). The boss at 512.
- Order: **mothqueen → moth** (the moth uses the new queen as image 1) → dust → windup → thimble.
- **QA:**
  - `artmax/eis/ingame.py` composite on a `field.png` plaza crop at 87 and 100 px.
  - |luminance Δ vs the plaza floor| ≥ 25; rust's −28.9 is the bar (today's dust is +2, windup +0.2, moth +8.6, thimble +6.4).
  - Grayscale silhouette test.
  - Bbox aspect within ±15 % of the current sprite.
  - Moth and queen read as one species.

| Asset | References (order) | Final |
|---|---|---|
| boss_mothqueen | current queen job 9a1d6cc3…, boss_shadow job, rust media | `Enemies/boss_mothqueen.png` 512 |
| enemy_moth | **new queen job**, current moth job f127e516…, rust media | `Enemies/enemy_moth.png` 256 |
| enemy_dust | current dust job 0cd79557…, rust media, boss_shadow job | `Enemies/enemy_dust.png` 256 |
| enemy_windup | current windup job 0bc97680…, rust media, boss_shadow job | `Enemies/enemy_windup.png` 256 |
| enemy_thimble | current thimble job 6401d75d…, rust media, boss_shadow job | `Enemies/enemy_thimble.png` 256 |

```
boss_mothqueen SUBJECT: The moth-queen boss enemy, front three-quarter view, hovering: a regal clothes-moth queen with 2 big upper wings and 2 smaller lower wings; each upper wing carries one large round eye-spot (muted mustard ring around a dusty-teal centre); a few big simple moth-eaten holes instead of fine lace; a cream fur mane, a small gold crown, a big face with big glowing pale-yellow eyes; plum and dusty-mauve body. Far fewer lines than image 1, darker plum outline like image 2. Wingspan about 1.1 x its height.
REFERENCES: image 1 = design to keep (simplify it), image 2 = outline weight and finish, image 3 = enemy family style.

enemy_moth SUBJECT: A small clothes-moth enemy of the same species as image 1 (same wing shapes, same mustard-and-teal eye-spots, same plum body), drawn small and simple: darker plum fuzzy body, two wings each with one eye-spot and one or two big moth-eaten holes, short thick antennae, big glowing pale-yellow eyes, a chunky cream knit scrap held in its jaws. Wider than tall, about 1.25 : 1.
REFERENCES: image 1 = species design (copy), image 2 = old minion pose, image 3 = enemy family style.

enemy_dust SUBJECT: A dust-bunny enemy made of a ball of lint: chunky scalloped fuzz silhouette (8 to 10 big round tufts, no thin hairs), round body slightly taller than wide, two big glowing pale-yellow eyes (the enemy signature), a small grumpy mouth, short thick stubby legs. Charcoal-plum body (#3B2F4A to #4A3B5C) with a lavender rim light along the top-left edge so it separates from a mid grey-violet cobblestone floor; bold dark-plum outline.
REFERENCES: image 1 = old design (simplify), image 2 and 3 = enemy family style and outline weight.

enemy_windup SUBJECT: A clockwork tin-mouse enemy: warm tarnished-brass body with plum enamel ears and belly, a big bright brass wind-up key on its back as the signature silhouette, chunky wheel feet, a small wrench in one paw, only 3 or 4 small rust spots, big glowing pale-yellow eyes; about as wide as tall.
REFERENCES: image 1 = old design, image 2 and 3 = enemy family style.

enemy_thimble SUBJECT: A thimble-shell enemy: a polished tarnished-brass thimble dome worn like a shell, one broad highlight band and a dark rim, 10 to 14 big round dimples (not dozens), a very dark plum body peeking out underneath with big glowing pale-yellow eyes and tiny feet. Keep the design of image 1 but with much stronger value contrast.
REFERENCES: image 1 = design to keep, image 2 and 3 = enemy family style.
```

### B3 — Buttons and state 9-slices (B3a P0: 4 → 25 credits with reserve; B3b P2: 9 → 55)
- Params: 1k, transparent. Square frames are 1:1 [S-UI9]; capsules are 16:9 [S-CAPSULE]; cells are 3:4.
- All 13 are one style family. `button_primary` is generated first and passed as image 1 to the other 12.
- **Geometry is fixed:** 192×192 with border 48, or 192×96 capsules (border 48), or 192×256 cells with a custom border. The ButtonPpu and CapsulePpu values do not change.
- **QA (stretch sheet):**
  - Sliced at 330×96 ppu 2, 900×100 ppu 2, 200×72 ppu 2.8 and 120×44 ppu 3.5.
  - Capsules at 256×62 ppu 1.55 and 120×34 ppu 2.8.
  - Cells at 130×170 ppu 2.4.
  - No seams; the corner fits in 48 px; centre column σ ≤ 2/255.
  - All 13 side by side on #34284F and on cream.

| Asset (final) | B | References | SUBJECT (appended to the style block) |
|---|---|---|---|
| `UI/button_primary_9s.png` 192 | 3a | button_primary_raw job, panel_night_raw job, UI mock media | A game button: lavender-violet satin cushion (top #A585EC, bottom #6C4BC2), a thin polished gold piping rim, an inner dashed cream stitch line parallel to the edge inside the rim band, a soft top highlight kept inside the top rim band. |
| `UI/button_gold_9s.png` 192 | 3a | new primary, button_gold_raw job | The same button in warm honey-gold satin (top #F8D27A, bottom #E2A847, never orange), cream highlight inside the top band, darker gold rim with a dark-indigo outer outline, inner dashed cream stitch. |
| `UI/ui_button_secondary_9s.png` 192 | 3a | new primary | The same button in matte dusky indigo felt (top #4F4183, bottom #3A2E66), thin pale-gold rim, inner dashed lavender stitch; clearly darker and less saturated than image 1. |
| `UI/ui_button_disabled_9s.png` 192 | 3a | new primary | The same button in desaturated slate-lavender worn felt (top #8D89A6, bottom #76728E), dim silver rim, faint grey dashed stitch, no gloss, no sparkle. |
| **NEW** `UI/ui_button_soft_9s.png` 192 | 3b | new primary, panel_parchment_raw job | The same button in cream linen/parchment (top #FBF1DC, bottom #EAD8B8), thin gold rim, inner dusty-pink dashed stitch matching the cream panel of image 2. |
| **NEW** `UI/ui_tab_on_9s.png` 192 | 3b | panel_parchment_raw job, new soft | A selected page tab: flat cream parchment (#FBF1DC) with a gold rim and an inner dusty-pink dashed stitch, slightly brighter top edge, no gloss. |
| **NEW** `UI/ui_tab_off_9s.png` 192 | 3b | new tab_on, panel_night_raw job | An unselected page tab: matte dark indigo felt (#2F2552), thin muted-gold rim, dim lavender dashed stitch. |
| **NEW** `UI/ui_tag_gold_9s.png` 192×96 | 3b | new button_gold | A small honey-gold satin tag in the same material as image 1: cream dashed stitch inside a thin gold rim, dark-indigo outline. |
| **NEW** `UI/ui_switch_on_9s.png` 192×96 | 3b | new primary | A toggle-switch track, ON: glowing lavender-violet satin inside a gold rim, soft inner light, cream dashed stitch. |
| **NEW** `UI/ui_switch_off_9s.png` 192×96 | 3b | new switch_on | The same track, OFF: matte dark indigo felt inside a muted silver rim, dim lavender stitch. |
| **NEW** `UI/ui_attendance_cell_9s.png` 192×256 | 3b | panel_parchment_raw job, new primary | (3:4 canvas, replaces "square canvas" in S-UI9.) A daily attendance calendar card: cream parchment card with a thin gold rim and an inner dusty-pink dashed stitch; a lavender satin header band across the top 30% with a straight lower edge; plain uniform cream body below. |
| **NEW** `UI/ui_attendance_cell_gold_9s.png` 192×256 | 3b | new attendance_cell | The same card for a special reward day: a honey-gold header band and a double gold rim with tiny star studs in the four corners. |
| **NEW** `UI/ui_nameplate_9s.png` 192×96 | 3b | upload `ui_pill_night_9s`, new primary | A small night-velvet nameplate capsule (#2F2552) with a polished gold rim and one tiny gold nail head near each round end. |

### B4 — Ribbons (P0; 3 × 5 = 15 + reserve 5 = 20)
- Params: 1k, 21:9, transparent. Style blocks: [S-RIBBON]. References: upload `ui-wide.png` (shape), then the new button_primary (gold piping).
- Post: `finish.py ribbon()`.
  - **night:** W 768, H 192, fill_h 184. Tails and folds inside L/R 200, T/B 48 (BorderOverrides). Builder ppu 1.4 unchanged.
  - **gold and lavender:** 512×160, fill_h ≈ 150, inside L/R 140, T/B 40.
- **QA:** night at 860×138 ppu 1.4, crisp at 1:1 on QHD next to the 58 pt title; gold at 260×76; lavender at 400×84 and 420×62.

```
ui_ribbon_night_9s SUBJECT: night-indigo satin (#2B2257 to #3A2E6E) with gold piping and a dashed gold stitch line along both edges, tiny gold star studs where the tails fold.
ui_ribbon_gold_9s SUBJECT: warm honey-gold satin (#F8D27A to #E2A847) with cream piping and a dashed cream stitch line along both edges.
ui_ribbon_lavender_9s SUBJECT: soft lavender satin (#C9B5F2 to #A58BE0) with gold piping and a dashed gold stitch line along both edges.
```

### B5 — WP-IA frame (P1, gated on WP-IA; 2 × max@2k + 2 × max@1k = 28 + reserve 14 = 42)
These are the dropped "illustrated top and tab bars", resized to the new frame: compact TopBar (default 130) and bottom nav (default 170) with a centre MAIN button. **Generate only after WP-IA freezes the heights.** The texture height is 2 × the bar height.

| Asset (final) | Params | References | Post |
|---|---|---|---|
| **NEW** `UI/topbar_9s.png` 1024×260 | 2k, 21:9, transparent, [S-BAR] | upload `ui_bar_velvet_9s` and `content_bg`, UI mock media | `build_bar.py`: caps 320 px, seamless centre tile, BorderOverrides L/R 320, T/B 0. **Image.Type.Tiled** with ppu = 260 / TopBarHeight. |
| **NEW** `UI/navbar_9s.png` 1024×340 | 2k, 21:9, transparent, [S-BAR] | new topbar raw, UI mock media | Same as topbar; ppu = 340 / TabBarHeight; no vertical flip |
| **NEW** `UI/nav_main_button.png` 512 | 1k, 1:1, transparent, [S-ICON] | shop media, upload `ui_medallion_dark`, button_gold_9s media | icon fit at 512 with a 4 % margin. The spin ring is the existing `FX/fx_ring_thread`, so no new FX is needed. |
| **NEW** `UI/sheet_frame_9s.png` 192 | 1k, 1:1, transparent, [S-UI9] | upload `content_bg`, panel_night_raw job | `build_9s.py` with flatten=True |

```
topbar_9s SUBJECT: dark indigo quilted velvet (#261D4C) with a low-contrast diamond quilting; along the BOTTOM edge an embroidered gold seam with small scallops and tiny gold stitch dots; the top 35% of the bar is plain velvet of the same colour so it can continue upward behind the phone notch; end caps: a cream crescent-moon charm hanging from a gold thread on the left cap and a gold sewing-button star charm on the right cap.
navbar_9s SUBJECT: the same quilted velvet bar (match image 1) but with the embroidered gold scalloped seam along the TOP edge, the bottom 35% plain velvet that continues down to the screen edge, a small thread-spool charm on the left cap and a star charm on the right cap; the centre stays plain because a big round button covers it.
nav_main_button SUBJECT: the big round MAIN home/battle button of a bottom navigation bar, front view: a thick polished gold rim with four small gold thread-hole studs like a giant sewing button, a deep indigo starry enamel disc inside, and in the centre the little patchwork mending-shop house from image 1 (purple shingle roof, spool chimney, warm glowing windows) standing on a tiny cobblestone plaza; a soft lamp glow only inside the disc.
sheet_frame_9s SUBJECT: (replaces the rounded-rectangle sentence of S-UI9) a panel whose TOP-left and TOP-right corners are rounded and whose bottom edge is straight and open with no rim, filled with dark indigo velvet (#261D4C, matching image 1), an embroidered gold seam along the top edge and thin gold piping down both sides.
```

### B6 — Skill icons (B6a P0: 8 → 45 with reserve; B6b P2: 25 → 145; total 165 + 25)
- Params: 1k, 1:1, transparent. Style blocks: [MASTER] [S-ICON].
- References, in this order:
  1. upload of the current `Icons/skill_<key>.png` (the concept);
  2. job `vfx_<key>` from `art2/jobs.tsv` (motif identity; read-only);
  3. trait_fabric job (house icon finish).
- Final: `Icons/skill_<key>.png` at **256** (was 128). Same name, so it is drop-in (`BattleSceneBuilder` ~2149 loads by name). Post: icon mode with a 5 % margin, remove_halo(0.92, 2).
- **QA** (`icons_display.py` / `verdict_sheet.png` method):
  - on `ui_medallion_dark` at 19, 22, 26 and 42 px, and on cream at 128 px (SPEC4 blessing card; 171 px on QHD);
  - outline ring present; 0 detached haze px; α max 255;
  - no two icons read alike at 26 px.
- **B6b must land before SPEC4 PRG-07 blessing cards ship**, because their 128 px cards need 256 px files.

High priority (B6a): candle_burst, heart_heal, moon_glow, shadow_burst, slow_yarn, split_yarn, storm_swirl, summon_circle.
```
candle_burst: a small cream wax candle whose flame bursts into a bright golden-orange star-shaped flame with a crisp outline.
heart_heal: a bright pink heart with a cream plus sign in its centre.
moon_glow: a cream crescent moon with a soft gold rim and a small heart-shaped glow inside its curve, no haze outside the outline.
shadow_burst: a burst of dark plum shadow with three glowing pale-yellow eyes and a bright lavender rim light.
slow_yarn: a bright lavender yarn ball whose loose strands spread into a sticky spider-web pattern, cream highlights.
split_yarn: a lavender yarn ball splitting into two smaller yarn balls with a short crack of light between them.
storm_swirl: a dark-blue storm-cloud spiral with a bright yellow lightning bolt across it.
summon_circle: a magic sewing circle: a ring of gold thread with four sewing-button knots and a glowing star in the middle.
```
Medium priority (B6b):
```
thread_stitch: a silver sewing needle trailing a bright pink thread in a bold zigzag stitch.
patch_heal: a heart-shaped dusty-pink fabric patch with big cream cross-stitches and a small gold plus sparkle.
button_shield: a big round four-hole sewing button used as a shield, lavender with a gold rim.
spin_button: a spinning blue sewing button with two bold outlined curved motion arcs.
keyhole_lock: a chunky gold padlock with a glowing heart-shaped keyhole.
key_beam: an ornate golden key firing a short bright cream beam of light to the right.
wax_drip: three fat cream-lavender wax drops dripping from a candle edge into a small puddle.
firefly: a cute round firefly with a glowing warm-yellow tail and small lavender wings.
lantern_beam: a pink paper lantern casting a short cone of warm light.
umbrella_shield: an open soft-blue umbrella held like a shield, gold handle.
raindrops: three big glossy blue raindrops in a triangle.
lipstick_heart: a pink lipstick in a gold case drawing a bold pink heart.
kiss_burst: a glossy pink lips kiss-mark with three small heart sparks.
gloss_shine: a glossy pink droplet with a big white four-point sparkle.
music_notes: two bold lavender eighth notes joined by a beam, gold outline accents.
sound_ring: a small gold bell with two bold concentric sound arcs.
waltz_ribbon: a pink and blue satin ribbon twirling in one loop.
star_burst: a bright gold five-point star with four small star sparks bursting out.
buff_crown: a small gold crown above a bold upward arrow.
debuff_mark: a purple downward spiral mark with a small down arrow.
stun_stars: three small gold stars circling on one elliptical orbit line.
shield_bubble: a translucent soft-blue bubble dome with a strong white highlight and a gold rim at its base.
cleanse_sparkle: a bright mint-and-cream water drop with a four-point sparkle.
dust_poof: a puff of cream-and-lavender dust with three little stars popping out.
reveal_light: a tall arched doorway of warm golden light opening in the dark, with short rays.
```

### B7 — Other icons (B7a P0: currency_event 5; B7b P2: 6 → 35 with reserve)
- Params: 1k, 1:1, transparent. Style blocks: [MASTER] [S-ICON]. Final 256, icon mode, remove_halo(0.92, 2).
- **QA:** at each display size (top-bar chip 44, mission rows 76, guide bar 48, HUD chip 36) on night and cream.

| Asset (final) | B | References | SUBJECT |
|---|---|---|---|
| `Icons/currency_event.png` | 7a | currency_gold job, currency_gem job, current event job | A compact wrapped star candy: a saturated pink-and-blue swirl star core filling about 70% of the width, short opaque cellophane wrapper twists on both sides, dark-indigo outline all round; same size, weight and rendering as images 1 and 2. |
| `Icons/stage_blackout.png` | 7b | stage_rain job, stage_fullmoon job, current blackout job | A snuffed storybook lantern for the blackout stage: a plum-and-brass lantern whose flame has just gone out, a cream-lavender smoke curl rising from the top, cool moonlight rim light on its left edge, wide sturdy silhouette, no near-black masses. |
| **NEW** `Icons/power.png` | 7b | guardian job, lamp job | A battle-power emblem: a heart-shaped lavender pincushion shield with two crossed silver sewing needles behind it and a small gold star on its front. |
| **NEW** `Meta/quest_stage.png` | 7b | lamp job, upload `Icons/quest.png` | A stage-progress icon: a short cobblestone path leading to a small lamppost with a warm glowing lantern and a lavender pennant flag with a gold star. |
| **NEW** `Meta/quest_repeat.png` | 7b | upload `Icons/quest.png` | A repeat-task icon: a rolled cream scroll tied with a lavender thread whose loose end curls into a circular arrow around it, with a small gold star charm. |
| **NEW** `Meta/mission_kill.png` | 7b | **new enemy_dust job**, guardian job | A defeat-enemies icon: a silver sewing needle striking like a little sword into a charcoal-plum dust-bunny puff that bursts into gold stars. |
| **NEW** `Meta/mission_boss.png` | 7b | boss_shadow job, star job | A defeat-bosses icon: a small gold crown tilted on top of a dark plum shadow blob with three glowing pale-yellow eyes, one bold gold star spark. |

### B8 — Feature-unlock illustrations and bag header (B8a P1: 10 → 60; B8b P3: 11 → 65)
- **Banners:** 1k, 21:9, opaque, [MASTER] [S-SCENE] [S-WIDE]. Cover-crop to 8:3 (yfrac 0.5, 1344×504), then Lanczos to **`Meta/feature_<name>_banner.png` 1280×480** RGB. The FeatureUnlockCard draws them in its 640×240 band (853 px on QHD).
- **Medals:** 1k, 1:1, transparent, [S-ICON], giving `Meta/feature_<name>.png` at 256 (116 px medallion, 76 px mission rows, 48 px guide bar).
- **QA:**
  - Composite each banner into the card band (7 px inset, RectMask2D, EnvelopeParent) at 1080 and 1440 widths.
  - Characters on-model; focus inside the central 60 %; no text.
  - Banners side by side as a set, for consistent lighting and palette.

| Asset (final) | B | References | SUBJECT |
|---|---|---|---|
| **NEW** `Meta/feature_level_up_banner.png` | 8a | jjak job, ttok job, shop job | Inside the cozy mending-shop workroom at night: the sock-doll hero (image 1) stands proudly on the sewing table while a floating silver needle and pink thread stitch a glowing gold star patch onto his arm; little stars rise around him; the blue pocket-watch friend (image 2) cheers beside a pincushion. |
| **NEW** `Meta/feature_level_up.png` (medal) | 8a | jjak job | A cream plush heart tag with a sewing-button centre and a big gold upward-arrow ribbon, two sparkles. |
| **NEW** `Meta/feature_auto_battle_banner.png` | 8a | jjak job, ttok job, shop job, new enemy_dust job, guardian_pad media | On the moonlit cobblestone plaza in front of the mending shop (image 3), the sock-doll hero and the pocket-watch friend stand on round tin-lid pedestals (image 5) and defend the shop on their own, firing sparkling thread shots at a few grumpy dust bunnies (image 4), while a little brass wind-up key turns by itself in the air above them. |
| **NEW** `Meta/feature_idle_chest_banner.png` | 8a | jjak job, upload `idle_basket_3`, shop job | On the mending shop's front step at night a big wicker mending basket (image 2) overflows with gifts (gold buttons, thread spools, ribbons, a star candy); fireflies drift and the sock-doll hero (image 1) naps against it under the moon. |
| **NEW** `Meta/feature_formation_banner.png` | 8a | jjak, ttok, chal, mong jobs; field media; upload the 5 `guardian_pad_*` | A bird's-eye storybook view of the round night plaza: the little mending shop in the centre and five colourful tin-lid pedestals in a ring around it (pink, mint, lavender, gold, sky); the sock doll, the pocket-watch friend, the golden-key girl and the teacup candle stand on four of them, each with a soft fan-shaped glow pointing outward. |
| **NEW** `Meta/feature_quick_idle_banner.png` | 8a | jjak job, upload `Icons/ticket_quick`, upload `idle_basket_3` | A cream ticket with a blue hourglass (image 2) tears open in a whoosh of sparkles and the wicker basket (image 3) fills up instantly with gifts; the sock-doll hero (image 1) jumps with joy. |
| **NEW** `Meta/feature_missions_banner.png` | 8a | mong job, upload `journal_chest_closed`, upload `Icons/journal` | On the shop counter by candlelight: an open stitched journal with gold check marks on its pages (image 3), a quilted purple gift chest with a button lock beside it (image 2), and the teacup candle friend (image 1) lighting the page. |
| **NEW** `Meta/feature_shop_banner.png` | 8a | rouge job, mat_cloth job, tab_shop job | The little store inside the mending shop: wooden shelves with bolts of cloth, ribbon rolls, lace, wax drops and thread spools, a brass welcome bell on the counter, and the lipstick friend (image 1) behind the counter as the shopkeeper. |
| **NEW** `Meta/feature_evolution_banner.png` | 8a | jjak job, jjak_caravan job | A glowing sewing-pattern paper on the workroom table; the sock-doll hero (image 1) holds a big needle while a radiant silhouette of his new caravan form (image 2: the sock doll carrying a little caravan house on his back) rises from the pattern in threads of light. |
| **NEW** `Meta/feature_costumes_banner.png` | 8a | jjak job, nightkeeper costume job, cheer costume job | A small wardrobe corner of the shop with costume outfits on hangers (a starry nightcap with a little lantern, a cheer flag and cap, a postman bag, as in images 2 and 3); the sock-doll hero (image 1) tries on the nightcap in front of a round mirror. |
| **NEW** `Meta/feature_speed_banner.png` | 8b | ttok_spinner job, ttok job | On the plaza the pocket-watch friend (images 1 and 2) spins super fast with golden motion arcs while its watch hands race, and the friends behind it blur with soft speed lines; cute, not aggressive. |
| **NEW** `Meta/feature_lamp_policy_banner.png` | 8b | mong_lantern job, lamp job | The candle friend in its paper-lantern form (image 1) holds up the shop's big brass lamp (image 2) as it lights the whole plaza with a warm golden wave; three small glowing choice tokens float beside it. |
| **NEW** `Meta/feature_auto_challenge_banner.png` | 8b | chal job, jjak job, boss_shadow job | The friends march together toward the plaza gate where a huge many-eyed shadow boss (image 3) looms; the golden-key girl (image 1) raises her key like a banner and the sock-doll hero (image 2) follows, determined. |
| **NEW** `Meta/feature_auto_challenge.png` (medal) | 8b | star job | A small gold crown inside two bold circular lavender arrows. |
| **NEW** `Meta/feature_expedition_batch_banner.png` | 8b | jjak, ttok, mong jobs; tab_expedition job | A little caravan of friends (the sock doll, the pocket-watch friend and the teacup candle), each carrying a small patchwork satchel, sets off together down a moonlit country road, tied together by one long pink ribbon. |
| **NEW** `Meta/feature_expedition_batch.png` (medal) | 8b | tab_expedition job | Three tiny patchwork satchels tied together with one pink ribbon. |
| **NEW** `Meta/feature_achievements_banner.png` | 8b | chal job, shop job | A cozy wall inside the shop covered with framed stitched pictures, ribbon rosette medals and a big open ledger on a stand, lit by warm lamps; the golden-key girl (image 1) proudly pins up a gold medal. |
| **NEW** `Meta/feature_achievements.png` (medal) | 8b | star job | A gold ribbon rosette medal with a stitched star centre, pinned to a small leather ledger. |
| **NEW** `Meta/feature_dungeon_batch_sweep_banner.png` | 8b | dungeon_banner job, echo job | A spiral library stairwell (image 1) with glowing ghost wisps (image 2); a stack of cream dungeon tickets fans out and flies down the stairs in one sparkling stream. |
| **NEW** `Meta/feature_dungeon_batch_sweep.png` (medal) | 8b | echo job | A fan of three cream dungeon tickets with a small echo-wisp swirl and a double arrow. |
| **NEW** `UI/bag_header.png` 1200×400 | 8b | upload `Icons/menu_bag`, tab_shop job, mat_cloth job | An open patchwork satchel (lavender quilted bag with a sewing-button clasp and moon and star prints, as in images 1 and 2) lying on a wooden table at night, crafting materials spilling out of it: a bolt of cream cloth, a spool of thread, a pink ribbon, a lace roll, a wax drop and a glowing moonlight shard; warm lamp light. (Post: 3:1 cover-crop to 1344×448, then 1200×400. The `_header` suffix means no mips.) |

### B9 — Synergy emblems (P2; 5 × 5 = 25 + reserve 5 = 30)
- Params: 1k, 1:1, transparent. Style blocks: [MASTER] [S-EMBLEM].
- References: image 1 = **key_and_lock job** (frame master); image 2 = the current emblem job (subject idea); then the member characters.
- Final: `Synergy/<id>.png` at **512**. icon fit + remove_halo. An optional free rim transplant from key_and_lock gives identical rims.
- **QA:** the 12 emblems side by side at 44, 136 and 192 px. Confusion pairs must separate at 44 px: rainy_lantern vs waiting_end, last_song vs locked_musicbox.
```
combo_clear_sky (+udu_base, lala_base jobs): a big rainbow arching over a closed soft-blue umbrella with a gold handle, two tiny music notes, after-rain sparkles.
combo_rainy_lantern (+mong_base, udu_base jobs): a warm golden-orange candle lantern as the hero (gold-orange dominant) sheltered under a small soft-blue umbrella canopy, only three raindrops.
combo_parade (+lala_base job): a lavender music box wearing a tiny marching-band hat under one strand of bunting.
combo_shop_family (+shop job): the little patchwork mending-shop cottage alone, big and simple, with a warm glowing window and door.
combo_last_song (+lala_base, mong_base jobs, +locked_musicbox job as the "must look different" reference): one big glowing golden music note rising from a small open plum music box with a tiny candle stub beside it; cooler, darker, bittersweet palette (deep teal-indigo and gold).
```

### B10 — Items (P2; 2 × 5 = 10 + reserve 5 = 15)
- Params: 1k, 1:1, transparent. Style blocks: [MASTER] [S-SPRITE] (item variant: "crafting-material icon").
- Final: `Items/<id>.png` at 256, icon mode, remove_halo(0.92, 2).
- **QA:** at 56, 84 and 160 px; it must read as its object at 56 px.
```
mat_spring (refs: current spring job, mat_cloth job, nightkeeper concept media): a chunky gold wind-up key fixed to one coiled brass spring, a small silver music-box comb beside it, at most one small music note; no glow, no halo, clean dark-plum outline.
mat_rib (refs: current rib job, mat_cloth job, udu_base job): a small folded umbrella skeleton as a crafting material: 4 or 5 chunky silver-blue ribs fanning out from a gold hub, a scrap of soft-blue canopy fabric caught on one rib, a curved handle tip, a soft-blue ribbon bow and one raindrop; it must read as an umbrella at 56 px.
```

### OPT — Optional (W5; 81 credits; each needs its own go-ahead)
| Item | Credits | Note |
|---|---|---|
| enemy_tangle regeneration | 5 | Only if it looks out of family next to the new B2 set. S-SPRITE: "a tangled yarn-ball enemy with big glowing pale-yellow eyes, chunky strands, plum-and-rose yarn". Reference: current job d8438a67… |
| combo_seven_lights on the standard frame | 5 | "Seven small coloured flames in a ring around a bright star." Its special frame is acceptable as is. |
| `Meta/feature_codex_banner` + `Meta/feature_codex.png` medal | 10 | SPEC4 keeps the Codex card off. Banner: "a big scrapbook album open on a table with friend portraits pinned in it"; medal: "a stitched scrapbook album with a button clasp and little photos peeking out". |
| udu ×3 and lala ×3 lineage restyle | 30 + 10 | [S-CHAR-EDIT]. Image 1 = the form's raw job (§2.3), image 2 = jjak_base media. Post: `artmax/chars/reexport.py` fit at 1024. **Accept a lineage only if all 3 forms pass the identity check; otherwise keep all 3 old forms.** Today they are clean and on-model, with thin-line drift only. |
| threadweb 2k regeneration | 9 | Only if the B0 haze clean-up is rejected. Use job 1dbd490c…'s prompt + "no fog or haze between threads, crisp glowing threads", with today's sprite as the reference. |
| Concept sheets ×6 upscale | 12 | **Prefer the user's originals.** Otherwise `upscale_image` (bytedance, 2k, 2 credits each, `get_cost` first). Reject on any Korean glyph change. Save 1536×2048 (nightkeeper 1640×2048), RGB. |

---

## 4. Dropped items: new assets and the code that must use them

### 4.1 The 22 feature names (`Meta/feature_*`), and DungeonBatchSweep added by SPEC4
The card rule (`FeatureUnlockCard.cs:76-80`): an illustration with aspect > 1.3 goes in the 640×240 band; otherwise it goes in the medal. The medal falls back to `def.icon`, then to `MetaIcons.Feature()`.

| # | FeatureId | Card today / after SPEC4 §2.8 | Medal (`def.icon`) | Band (`illustration`) |
|---|---|---|---|---|
| 1 | CharacterLevelUp | on / on | **NEW** feature_level_up (B8a) | **NEW** banner (B8a) |
| 2 | AutoBattle | on / on | Icons/auto (keep) | **NEW** (B8a) |
| 3 | IdleChest | on / on | UI/idle_basket_2 | **NEW** (B8a) |
| 4 | Gacha | on / on | UI/tab_gacha | UI/gacha_banner_wide (keep) |
| 5 | Formation | on / on | UI/tab_formation | **NEW** (B8a) |
| 6 | Synergy | off / off | Icons/resonance | – (no card) |
| 7 | Expedition | on / on | UI/tab_expedition | UI/zone_exp_1_1 (keep; 1344×672 in B0) |
| 8 | QuickIdle | on / on | Icons/ticket_quick | **NEW** (B8a) |
| 9 | Attendance | off / off | Icons/attendance | – |
| 10 | Speed2x | off / **on** | Icons/speed | **NEW** (B8b) |
| 11 | LampPolicy | off / **on** (§7.3) | Icons/lamp | **NEW** (B8b) |
| 12 | Missions | on / on | Icons/journal | **NEW** (B8a) |
| 13 | Shop | on / on | UI/tab_shop | **NEW** (B8a) |
| 14 | Dungeon | on / on | Icons/echo | UI/dungeon_banner (keep; 1344×504 in B0) |
| 15 | Evolution | on / on | Icons/shards | **NEW** (B8a), replacing the jjak_caravan portrait in the medal |
| 16 | Codex | off / off | Icons/star | OPT |
| 17 | AutoChallenge | off / **on** | **NEW** medal (B8b) | **NEW** (B8b) |
| 18 | ExpeditionBatch | off / **on** | **NEW** medal (B8b) | **NEW** (B8b) |
| 19 | Achievements | off / **on** | **NEW** medal (B8b) | **NEW** (B8b) |
| 20 | Costumes | on / on | Costumes/jjak_cheer | **NEW** (B8a), replacing the nightkeeper portrait in the medal |
| 21 | EchoSlot6 | off / off | Icons/echo | – |
| 22 | Events | off / **on** | Icons/currency_event (regenerated in B7a) | UI/shop_event_banner (keep; 1344×504 in B0) |
| + | DungeonBatchSweep (new in SPEC4) | – / **on** | **NEW** medal (B8b) | **NEW** (B8b) |

Totals: 15 new banners, 5 new medals, 4 existing wide arts reused. The other medals reuse existing icons.

### 4.2 The other dropped items
- **Illustrated top and tab bars:** done as B5 (topbar_9s, navbar_9s), sized to WP-IA, plus nav_main_button and sheet_frame_9s.
- **Bag illustration:** `UI/bag_header.png` (B8b).
- **Mission and quest icons:**
  - quest_stage and quest_repeat (code already looks them up);
  - mission_kill and mission_boss (B7b);
  - mission_login stays `Icons/mission_visit` (fine at 76 px).
- **Per-state and dedicated 9-slices** (B3b): tab_on/off, switch_on/off, button_soft, tag_gold, attendance cell ×2, nameplate. These stay merged and are fine as shared primitives: card_back, frame_stitch, pill/chip, button_lavender (= secondary).
- **Small one-offs:**
  - `Icons/power` (B7b), for the M10 PowerChip.
  - Everything else stays merged; the shared primitives are crisp at their sizes.
- **No max tier for sheets** before: fixed by B3/B4/B6/B7 and the max singles. Gacha grade backs, card_portrait_bg and the other sheet cells stay, because they are drawn at ≤ 0.5×.

### 4.3 Code places to wire (code is owned by the UI packages; the art agents only drop files)
| # | Asset(s) | File (current line) | What |
|---|---|---|---|
| 1 | 15 `Meta/feature_*_banner` | `Editor/Content/MetaContent.Features.cs` FillFeatures (`AtStage` calls, lines 31–160) | Pass `illustration: Pick(MetaArt("feature_<name>_banner"), <current illustration>)` for LevelUp, AutoBattle, IdleChest, Formation, QuickIdle, Missions, Shop, Evolution and Costumes. After SPEC4 P4-8 `CardsReady`, also Speed2x, LampPolicy, AutoChallenge, ExpeditionBatch, Achievements and DungeonBatchSweep. FeatureUnlockCard needs no change. |
| 2 | 5 medals `Meta/feature_<name>.png` | `Editor/Builders/MetaSceneBuilder.cs` `FeatureArt()` (~142) | **Naming bug:** it loads `Meta/feature_{enum.ToLowerInvariant()}` (`feature_characterlevelup`), while content uses snake_case (`feature_level_up`). Use one shared FeatureId→snake_case table so `MissionRowView:94` and `GuideQuestBarView:190` fallbacks show the same medals as the card. |
| 3 | `Meta/mission_kill`, `Meta/mission_boss` | `Editor/Content/MetaContent.Missions.cs` md_kill (~29), mw_boss (~50); `MetaContent.Quests.cs` boss quest (~89) | `Pick(MetaArt("mission_kill"), ArtLib.Enemy("enemy_dust"), …)` / `Pick(MetaArt("mission_boss"), ArtLib.Enemy("boss_shadow"), StageIcon())`. quest_stage and quest_repeat are already wired (`Quests.cs:123,154`). |
| 4 | attendance cell ×2 | `Editor/ArtImportPostprocessor.cs` BorderOverrides (~45); `Editor/Builders/MetaSceneBuilder.cs` `BuildAttendanceCellTemplate` (~1128); `Scripts/UI/Meta/AttendanceCellView.cs` (fields ~17–23, `Bind` ~62–87) | Border (L44, B44, R44, T88) for both. Frame/Background use `UiArt.AttendanceCell9s` at ppu 2.4; bind a new `_specialSprite` (the gold variant). The highlight/day-7 state swaps the sprite instead of tinting Gold. The stamped state is a light lerp (≤ 0.35) + stamp. `_todayGlow` is unchanged. |
| 5 | switch on/off | `MetaSceneBuilder.Switch()` (~1759); `Scripts/UI/Meta/MetaSwitch.cs` (`_track` ~15, `Apply/Animate` ~57–82) | New `[SerializeField] Sprite _trackOn, _trackOff`; set `_track.sprite` per state with a white colour. `TrackColor` tint only when the sprites are null. The knob stays `ui_pip_button`. |
| 6 | `ui_tag_gold_9s` | `MetaSceneBuilder` ClaimTag (~345), FullTag (~416), PointsPill (~958) | `Sliced(…, "ui_tag_gold_9s", UiArt.CapsulePpu(h), Color.white)`, falling back to `button_gold_9s`. |
| 7 | all new UI names | `Editor/UiArt.cs` (accessor block ~61–106; new accessors only) | Add, each with today's sprite as fallback: ButtonSoft9s→parchment, TagGold9s→button_gold, TabOn9s/TabOff9s→ui_tab_9s, SwitchOn9s/SwitchOff9s→ui_bar_frame_9s, AttendanceCell9s/Gold9s→parchment, Nameplate9s→pill_night, TopBar9s/NavBar9s→ui_bar_velvet_9s, SheetFrame9s→panel_night, NavMainButton→null, BagHeader→null. |
| 8 | `ui_button_soft_9s` | `Editor/SceneParts.cs` ButtonStyle.Soft (~66); `MenuSceneBuilder.Skin` Tone.Soft (~2179); `CharacterSceneBuilder.ApplyArt` Tone.Soft (~1696) | Change `PanelParchment9s` to `UiArt.ButtonSoft9s` (ppu = ButtonPpu(h)). |
| 9 | `ui_tab_on_9s` / `ui_tab_off_9s` | **New** `Scripts/UI/Common/TabSkin.cs` (`[SerializeField] Sprite _on, _off`); `Scripts/UI/UITheme.cs` `TabStyle.Apply` (~327); builders `MetaSceneBuilder.TabButton` (~1728), `CharacterSceneBuilder.TabArt` (~1705), `SettingsSceneBuilder` tabs (~37), `MenuSceneBuilder` synergy-codex tabs (~1755); direct tinters `JournalPopup` (~62), `AttendancePopup` (~51), `SettingsPopup` (~73), `SynergyCodexPopup` (~197) | For TabVariant.Page with a TabSkin: swap the sprite and use a white colour. Option tabs keep the primary/secondary button art. The direct tinters route through `TabStyle.Apply`. The codex kind colour becomes `Lerp(white, kindColor, 0.35)` on the on-sprite. |
| 10 | `UI/bag_header` | `Editor/Builders/MenuSceneBuilder.cs` `BuildBagPopup` (~1095) | Add a ≈900×300 header band under the title (RectMask2D + AspectRatioFitter EnvelopeParent + card_frame_9s overlay); `windowHeight +=` band + gap; skip it when the art is missing. Keep the band at the top of the sheet once WP-IA turns the bag into a sheet. |
| 11 | `Icons/power` | `MenuSceneBuilder.BuildTopBar` PowerChip (UX-SPEC §2.2 M10, when built) | `ArtLib.Icon("power") ?? ArtLib.Icon("guardian")`, 40 px, white. |
| 12 | topbar_9s, navbar_9s | `Editor/ArtImportPostprocessor.cs` BorderOverrides | `topbar_9s` and `navbar_9s` = (320, 0, 320, 0). |
| 13 | topbar_9s | `MenuSceneBuilder.BuildTopBar` (~64–68), WP-IA | `velvetArt` becomes `UiArt.TopBar9s`, `Image.Type.Tiled`, `pixelsPerUnitMultiplier = 260 / TopBarHeight`. The TopBleed colour matches the art's top row (#261D4C). |
| 14 | navbar_9s | `Editor/IdleSceneBuilder.cs` `BuildTabBar` (~277–284), WP-IA | `UiArt.NavBar9s`, Tiled, ppu = 340 / TabBarHeight, **no** `localScale.y = −1` flip. |
| 15 | nav_main_button | WP-IA MAIN-button builder (new) | Image (preserveAspect, white) + `FX/fx_ring_thread` slow spin behind it + press scale; the label is TMP. |
| 16 | sheet_frame_9s | WP-IA sheet builder (new) | Top edge of every sheet: Sliced at ppu 2 over `content_bg`. |
| 17 | `ui_nameplate_9s` | `Editor/Builders/BattleSceneBuilder.cs` NamePlate '수선점' (~249) and BossPlate (~506) — **after FX-JUICE merges this file** | `Pill(…)` becomes `UiArt.Nameplate9s` (CapsulePpu(34/36)), white; no Panel/Background tint. |
| 18 | zone_exp 1344×672 | `ArtImportPostprocessor` NoMipPrefixes (~53) | Remove `"UI/zone_"`; the list cards draw these at 296×148 and need mips. Bump `GetVersion()` 5→6 once, together with #4 and #12. |
| 19 | Characters/Costumes at 1024, banners | `ArtImportPostprocessor` (optional) | Android/iOS maxTextureSize override of 768 for `Characters/` and `Costumes/` (all 39 are referenced by content, so about 52 MB ASTC resident drops to about 29 MB). Optional ASTC 6×6 for `Meta/*_banner` (15 × 1280×480 kept resident by MetaDatabase, about 12 MB at 4×4). |

**No code change** (same names, canvases, aspects and borders): story pages, enemies, items, synergy (512), skill icons (256), ribbons, buttons, currency_event, stage_blackout, traits (512), dungeon and shop_event banners (same 2.667 aspect), moon, concept sheets, quest_stage, quest_repeat, and the medals that use existing MetaArt names.

---

## 5. Priority order (what the player sees most first)
| # | Batch | Why | Credits | Cumulative |
|---|---|---|---|---|
| 1 | B0 | Free | 0 | 0 |
| 2 | B1 story | Every new player's first 30 s. intro_1 cuts off two friends on every phone; intro_2 and intro_3 are 2× soft. | 45 | 45 |
| 3 | B2 enemies | The most-spawned sprites on the main screen disappear into the plaza floor (Δ +0.2 to +8.6) | 35 | 80 |
| 4 | B3a buttons | Every screen and every tap; generic app-store gloss with a style that drifts from the set | 25 | 105 |
| 5 | B4 ribbons | Every boss and stage-clear banner; the night ribbon is soft at 1.5× | 20 | 125 |
| 6 | B6a skill icons, high | Battle badges all the time: no outline, haze, low contrast | 45 | 170 |
| 7 | B7a currency_event | Top-bar chip, always visible | 5 | 175 |
| 8 | B8a feature banners | The 9 milestone cards of the first two hours | 60 | **235 ← fits the current balance** |
| 9 | B5 IA frame | Always on screen, but generated only after WP-IA | 42 | 277 |
| 10 | B9 synergy | HUD row at 44 px and the codex | 30 | 307 |
| 11 | B3b state 9-slices | Tabs, switches, tags, attendance, nameplates | 55 | 362 |
| 12 | B10 items | Bag, rewards, recipes | 15 | 377 |
| 13 | B7b misc icons | Stage chip, PowerChip, journal rows | 35 | 412 |
| 14 | B6b skill icons, medium | Badges, and SPEC4 128 px blessing cards (must land first) | 145 | 557 |
| 15 | B8b SPEC4 cards + bag | Later unlocks | 65 | 622 |
| 16 | OPT | Optional | 81 | 703 |

---

## 6. Kept as is (no credits)
- **Battle:** shop, guardian_pad_* and empty.
- **Enemies:** rust, wisp, boss_shadow.
- **Items:** cloth, lace, moonlight, ribbon, wax.
- **Icons:** affinity, attendance, auto, check, gem, gold, echo, guardian, info, journal, lamp, lock, menu_*, mission_visit, quest, resonance, reward_generic, shards, speed, star, ticket_*, style_*, and stage_calm/duststorm/festival/fullmoon/rain/wind.
- **UI:** content_bg, portrait_bg, gacha_banner(_wide), return_header, attendance_header, attendance_stamp, card_bg_grade1–5, gacha_card_back, ui_card_empty and locked, idle_basket_0–3, journal chests, the panel/inset/slot/tab/bubble/pill/bar 9-slices, divider, tag_sale, stamp_soldout, badge_red, pip, slot_plus, close, chevron, tab_selected, plate_round, medallion_dark, ring_stitch, skill_orb_frame, stamp_ring, tab_* icons, title_logo, tutorial_finger, tutorial_highlight.
- **Reason:** each is crisp at its display size; the audit found no issue, or only sub-pixel softness below 0.5×.

## 7. Decisions needed
1. Approve the top-up (≈ 675; or a 1,000-credit pack). Until then, run only up to line 8 of §5.
2. WP-IA must publish TopBarHeight and TabBarHeight and the MAIN button diameter before B5.
3. Should SPEC4 P4-10 move its §10.1 art from high to max singles (+≈ 222 over its 40 floor)?
4. Ask the user for the original concept-sheet files (this avoids the OPT concept upscale and any text risk).
5. udu/lala restyle (OPT): go or no-go. The current forms are on-model, with only thin-line drift.

## Backlog after RM-2 (no credits now; Codex lane or a top-up)
- `UI/ui_tab_selected`: a deeper lilac, without the warm glow under the label. The stopgap is the `SelectedPlate` 0.75 tint + label outline in `IdleSceneBuilder` (RM-2 KIT).
- A white loop glyph for the farm-loop toggle (WP-5A).
- `VFX/atk_*_crit` with an open centre so the number reads (WP-5B).

## Amendments after RM-2 (2026-11-06, orchestrator)

Applied in the RM-3 docs pass (T-5) from `scratchpad/roadmap/rm3/triage.md` §3.
- **A-09 (R48 + the UX Wave 3 art requests):** the "Backlog after RM-2" section above.
