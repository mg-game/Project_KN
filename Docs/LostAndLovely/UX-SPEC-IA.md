# LOST & LOVELY: Screen Frame Addendum (UX-SPEC-IA)

Version 1.1 · 2026-11-04 · addendum to `UX-SPEC.md` v1 · new package **WP-IA** (Wave 1c) · critic pass applied: changed text carries `(CR-nn)` tags, list at the end (**Critic revisions**)

**Relayed user request (verbatim)**
1. "그리고 우리 방치하는 맵을 크게보는기능도만들어줘. 너무 보이는게없어 좌우만보이고"
2. "평상시에 크게보여주고, 내가 특정기능을 클릭하면 잠깐그화면을가리는것처럼보여주자. 예를들어 가운데 아래메인버튼을누르면 메인사냥화면에 이벤트나 기타 작은아이콘들몇개만보여주는거야"

**Reading:** the battle map is the home screen and is shown big. The six feature tabs slide up as a sheet that temporarily covers the lower part of the map; the battle keeps running and stays visible above it. A centre-bottom main button (사냥) brings back the hunting screen, which carries only a few small icons (events and similar).

**Inputs**
- Geometry analysis: `scratchpad/ia/` (ia_01…ia_06, geom*.py, keepout_out.txt, rule_out.txt).
- HUD inventory and frame dependencies (relayed with the task; its file:line keys ISB/MSB/BSB/MetaSB/TSB/CT/SMOKE/SHOT are reused here).
- Reference research: `scratchpad/ia/research/**` (세븐나이츠 키우기, Tap Titans 2, 버섯커 키우기, 레전드 오브 슬라임, Capybara Go, AFK Journey, 메이플 키우기).
- This addendum's own validation: `scratchpad/ia/spec/ia_check.py` → `ia_check_out.txt` (Appendix B).
- Mock-ups (existing art + current shots, placeholder backdrop): `scratchpad/ia/spec/ia_08_main_mock.png`, `ia_09_sheet_mock.png`, annotated `ia_08b_main_annotated.png`, `ia_09b_sheet_annotated.png`, side by side `ia_10_mock_pair_half.png`.
- Outpaint post-process tool (tested on synthetic results): `scratchpad/ia/spec/field_tall_post.py`. Not used in the end (§9.0).
- Delivered art (WP-IA0 done, §9.0): `Art/Battle/field_tall.png` (1080×2048) and `Art/Icons/menu_all.png`; report `scratchpad/ia/field_tall_report.txt`; previews `scratchpad/ia/field_tall_preview.png`, `scratchpad/ia/icons_preview.png`.
- Critic pass: crops and the v1 backup in `scratchpad/ia/critic/`.

**How to read:** decisions (§0) → the frame (§1) → your area (§2–§6) → WP-IA (§7) → your Wave 2 delta (§8) → art (§9). Item ids `[IA-xx]` are cited by packages. All coordinates are 1080×1920 reference px; "screen y" is measured from the top. Tokens are UX-SPEC §1 (`UIType`, `UISpace`, `UISize`, `UIMotion`); new constants are listed in §7.3.

---

## 0. Decisions at a glance

| Id | Decision | Why |
|---|---|---|
| IA-01 | Two frame states: **MAIN** (battle at full height, a few icons) and **SHEET(tab)** (an 836 px sheet over the lower battle; the battle keeps running above it). Launch is always MAIN. | The request; 세븐나이츠 키우기 battle-first + half-sheet model, Tap Titans 2 |
| IA-02 | **One sheet height for all six tabs**: a 56 px header + the existing 780 px panel. When open, ContentArea has today's rect (970–1750), so **no tab panel layout changes**. | Panels are hard-laid-out for 780 (inventory F); coach rects unchanged; no camera moves on tab switch |
| IA-03 | The battle is never resized or zoomed. In SHEET it **pans up 360 px** (16:9: the rule's 359.5 rounded to a whole device pixel, CR-09) through a new `Field/Camera` node. Ppu, Squash and unit size are unchanged. Effects that place themselves by the field's extents follow the *visible* band (CR-01). | Validated: 100% of every lane visible in both states. Without the pan, lanes 2 and 3 are 100% hidden |
| IA-04 | Tall backdrop `Art/Battle/field_tall.png` (1080×2048, Higgsfield outpaint of field.png pasted at y 619; **delivered**, §9.0) plus a **cover cap** in `FitToBackdrop`, so the painted ring always matches the world ring. | No blur from 16:9 to 20:9; also fixes today's tall-phone bug (art ring 16.7–23.4% larger than the guardian ring) |
| IA-05 | Bottom nav 3+1+3: 강화 · 편성 · 친구들 · **사냥** · 원정 · 소환 · 상점. The centre is a medallion with the 수선점 art. Re-tapping the open tab closes the sheet; 사냥 always returns to MAIN. The `MainTab` enum is unchanged: MAIN means "no sheet" (index −1). | 7K and Habby pattern; zero enum, TabGate or AutoShot churn |
| IA-06 | Compact top bar, 130 px: level + power on the left; gold, gem, 우편 and ≡ 메뉴 on the right. 설정, 공지 and 가방 move into the ≡ menu; 일지, 출석 and 이벤트 become quick icons on the battle. | Frees 60 px for the battle; "a few small icons" |
| IA-07 | Quick rails: at most 3 icons per side, in the lane-free pockets (1 top + 2 bottom). v1 content: 이벤트 (top-left), 출석부 (bottom-left), 수선 일지 (bottom-right). | Validated: 0% overlap with the battle keep-out, bosses included |
| IA-08 | HUD per state: stage panel and x1/AUTO stay. The recommended-power pill, synergy row and rails fade out in SHEET; the idle chest and boss button dim to α0.5. The deck strip, lamp and guide bar sit under the sheet (AUTO keeps fighting). | After the pan, each faded item would hide 0.3–0.8 world units of a lane |
| IA-09 | Back order: overlay handlers → popup → return chip (only while it is shown; it restores the frame it was pushed from, CR-05) → sheet → exit confirm. Optional auto-return to MAIN after 60 s without input (setting, default on; off in tools and tests, CR-04). Sleep mode is restyled so the map stays visible (dim 0.5, CR-16). | "방치 중에도 크게" |
| IA-10 | WP-IA is Wave 1c, after Wave 1b and before Wave 2, in two tracks (A shell, B battle frame). Wave 2 packages build on the new frame with the deltas in §8. | Keeps Wave 2 file-disjoint |

### 0.1 What this addendum changes in UX-SPEC

| UX-SPEC section | Status | Now in |
|---|---|---|
| §0.1 Reference frame | superseded | §1.1 |
| §0.2.1 Frozen coach paths, §0.2.2 names | amended | §5.8 |
| §1.5 Sizes, §1.8 Motion | rows added | §1.5, §7.3 |
| §2.1 HUD map | screen y re-based to the 130 top bar | §3.2 |
| §2.1.5 boss button, §2.1.6 synergy chip, §2.1.10 basket | SHEET behaviour added | §1.6 |
| §2.1.7 ribbon, §2.1.8 bubbles | formulas re-based | §8.3 |
| §2.1.16 Return chip | moved into the sheet header | §4.2 |
| §3.14 Return chip lifetime | every path to MAIN clears the slot; a pop restores the frame (MAIN or the tab) it was pushed from (CR-05) | §5.2 |
| §3.13 Red dots | `RedDotSystem.IsClaimOn` (claimables only) added; the ≡ dot is `RedDotView(top.settings)` (CR-06, CR-17) | §5.9, §3.6 |
| §2.2 Top bar (M10) | layout superseded; M08, M09 and M21 behaviours kept | §3.1, §8.2 |
| §2.3 Tab bar (M52) | layout and re-tap superseded; indicator, icon motion and glow kept | §2 |
| §2.4–§2.9 Tab panels | unchanged (same rect) | — |
| §2.10 Settings | one row added | §8.2 |
| §2.11 Sleep mode | superseded (map stays visible) | §4.9 |
| §2.12 M42 coach bubble | MAIN fallback position | §8.6 |
| §3.1 Layer order | ContentArea becomes a nested Canvas | §1.1 |
| §3.2 Android back | superseded | §4.7 |
| §3.5 Toast position | unchanged (y 480 from the safe top works in both states) | — |
| §4.0 Waves, §4.1 Ownership | Wave 1c inserted; WP-IA rows; Wave 2 handover | §7.0, §7.1 |
| §5.2 Captures | IA shots added; checks for 03, 05, 11c and 29 re-based | §7.5, §8.7 |
| Appendix C (M66 rejected) | resolved here | Appendix A |

---

## 1. Frame and states [IA-01…IA-04]

### 1.1 Reference frame (1080×1920; replaces UX-SPEC §0.1)

| Region | Screen y | Notes |
|---|---|---|
| TopBar | 0–130 | compact; identical in MAIN and SHEET (§3.1) |
| StageArea / `Battle` | 130–1750 (1620) | full height in both states; never resized |
| · Field (mapper rect) | 130–1594 (1464) | `Stretch(0,0,0,156)` as today |
| · Strip | 1594–1750 (156) | DeckTab rises 18 px (1576–1594) |
| Sheet = `SheetHeader` + `ContentArea` | open: header 914–970, ContentArea 970–1750. Closed: below 1750 and inactive | ContentArea keeps today's rect when open |
| TabBar | 1750–1920 (170) | 7 slots (§2) |

- SafeArea sibling order is unchanged (StageArea, ContentArea, TopBar, TabBar, …), so the sheet slides out from behind the tab bar and stays under the top bar.
- ContentArea gains a nested `Canvas` + `GraphicRaycaster` + `CanvasGroup`; moving it never re-batches the root canvas. Panels stay its direct children.
- `SheetHeader` is a child of ContentArea that sits on its top edge: anchors (0,1)–(1,1), pivot (0.5,0), position (0,0), size (0,56).
- Taller phones: the frame lives in SafeArea and StageArea absorbs all extra height (≈1890 on 19.5:9, ≈1950 on 20:9). The sheet stays 836 tall.

### 1.2 States and triggers

| State | ContentArea | p | Battle pan | Nav selection |
|---|---|---|---|---|
| MAIN | inactive | 0 | 0 | 사냥 (`Tab_Main`) |
| OPENING | active | 0→1 | 0→PanOpen | target tab |
| SHEET(tab) | active, one panel on | 1 | PanOpen | tab |
| SWITCHING | active | 1 | PanOpen | new tab (panel α cross-fade 0.12) |
| DRAGGING | active | follows the finger | PanOpen·p | tab |
| CLOSING | active until p = 0, then inactive | →0 | →0 | 사냥 |

`p` is the eased shown amount. Every follower (sheet position, battle pan, band inset, HUD fades) reads the same `UIFrame.SheetProgress` (§5.1). An interruption re-targets from the current p.

Every path to MAIN (사냥, re-tap, handle, drag dismiss, back, auto-return, `GoToMain`) runs `MainTabController.ShowMain`, which clears the return slot (CR-05).

| Input | In MAIN | In SHEET(X) |
|---|---|---|
| Tap side tab Y (unlocked) | open on Y | Y ≠ X: switch (no slide, no pan). Y = X: raise `Reselected`, then close |
| Tap a locked tab | TabGateView toast + lock shake (unchanged) | same; the sheet is unchanged |
| Tap 사냥 | press feedback only | close |
| `UINav.GoToTab(Y)` (code, deep links) | open Y | switch to Y; **never closes** |
| `UINav.GoToMain()` | no-op | close |
| Tap the header handle | — | close |
| Drag the header down | — | interactive dismiss (§4.3) |
| Android back | §4.7 | §4.7 |
| Android back while dragging the header | — | ends the drag, then closes from the current p (CR-21) |
| 60 s without input (setting) | — | close (§4.8) |
| Tap the battle above the sheet | normal | normal: HUD buttons work, empty taps do nothing |

### 1.3 Battle remap per state [IA-03]

| | MAIN (16:9) | SHEET (16:9) | Today |
|---|---|---|---|
| Field / visible band | 130–1594 (1464) | 130–914 (784) | 190–814 (624) |
| Backdrop | field_tall 1080×2048, scale 1.000, art top y −84 | same art, moved up 360 (art top y −444) | field.png 1080×810, top y 175 |
| Shop (world origin) screen y | 925.5 | 565.5 | 565.5 |
| Ppu / Squash / ActorScale | 205 / 0.7256 / 1.00 | same | same |
| Visible world (feet) | x ±2.63, y −4.49…+5.35 | x ±2.63, y −2.34…+2.93 | x −2.63…+2.64, y −1.67…+2.52 |
| Lanes: path visible from spawn | 100% on all five | 100% on all five | 61–90%; worst lane hidden up to 4.0 s |
| Plaza ring r 1.6 / guardians / shop | 100% | 100% | 100% |
| Spawn circle r 2.5 inside the band | 100% | 88% (only the bottom arc between lanes) | 73% |

Spawn points (r 2.5) in MAIN: top (539.5, 553.6), upper-left (52.1, 810.6), lower-left (238.3, 1226.4), lower-right (840.7, 1226.4), upper-right (1026.9, 810.6). In SHEET: same x, y − 360.

**Pan rule** (runtime, any aspect ratio; StageArea-local px measured from the StageArea top):
- `H` = StageArea height, `k = Ppu / 205`, `sheetTop = H − 836`, `mainShop = (H − 156)/2 − Origin.y` (Origin is field-local, y up).
- `shop = sheetTop ≥ 760k ? sheetTop − 348k : 146k + 0.35·sheetTop`.
- `PanOpen = round_px(max(0, mainShop − shop))` with `round_px(v) = Mathf.Round(v·s)/s`, `s` = the root canvas scaleFactor (CR-09). The battle never moves down.
  - Why: `Origin.y` is −63.5, so the raw value ends in .5 on every aspect. That parks the backdrop and the field on a half texel for as long as the sheet is open: a permanent bilinear blur on 1080-wide phones (scale 1).
- 16:9: H 1620, mainShop 795.5, sheetTop 784, shop 436 → raw 359.5 → **PanOpen 360**. Other shapes (insets 100/50 assumed; always computed at runtime; raw → shipped at scale 1): 19.5:9 224.5 → 224, 20:9 194.5 → 194, 21:9 141.5 → 142. The 0.5 px shift changes no visibility number in Appendix B.
- The rule matches a brute-force search within about 12 px (geometry `rule_out.txt`).
- Zoom-to-fit is rejected: it only helps sheets of 60% or more, shrinks units to 0.78× or 0.67×, and needs art extended sideways (geometry §3).
- MAIN is not zoomed in either (CR-22). Ppu can rise at most to the 229.8 cap (1.12×). That upsamples the 1080-wide art and pushes the lane 1/4 spawns to x −7 and 1086. The user's complaint is coverage ('좌우만 보이고'), and MAIN fixes it at unchanged unit size.

### 1.4 Backdrop and cover cap [IA-04]
- `BattleArt.Load` uses `Battle/field_tall` when it exists, otherwise `Battle/field`. Keep `field.png`: the formation mini-field (MSB:295) assumes 4:3, and it is the fallback.
- For field_tall, set the ring values explicitly. **Never run `MeasurePlazaRing` on it**: its search windows would cross generated pixels.
  - 2048 variant: `_backdropSize (1080,2048)`, `_backdropRingCenter (0.49954, 0.50708)`, `_backdropRingRadius (0.30370, 0.11621)`.
  - 1920 variant: **not shipped** (the 2048 outpaint passed, §9.0). `BattleArt.Load` accepts field_tall only at 1080×2048. Any other size logs an error and falls back to `field.png` with its measured ring (CR-23).
- Cover cap in `FitToBackdrop`:
  - `ppuCap = min(size.x · 0.5 / _extentX, _maxPpu)` (229.8 at width 1080).
  - `cover = min(max(frame.x / art.x, frame.y / art.y), ppuCap · _backdropRingWorld / (_backdropRingRadius.x · art.x))` (≤ 1.121).
  - When the cap applies, the art letterboxes vertically (centred) and the painted ring still equals 1.6 world units.
  - This fixes today's tall-phone bug, and makes a build that ships the new frame with the old art degrade to letterboxing instead of a 2× blur with a 78% ring mismatch.
- Rows of the 2048 art on screen: MAIN 16:9 rows 214–1678 (the strip covers 1678–1834); SHEET rows 574–1358; 20:9 rows ≈49–1843. No upscaling on 16:9, 19.5:9 or 20:9; at most 1.035× on 21:9.

### 1.5 Motion (rows added to UX-SPEC §1.8)

| Interaction | Duration (s) | Curve | Detail |
|---|---|---|---|
| Sheet open | 0.30 `SheetOpen` | OutCubic | p 0→1. Sheet y, battle pan, band inset and HUD fades all follow p |
| Sheet close | 0.24 `SheetClose` | OutCubic | from the current p |
| Re-target or drag release | remaining fraction × open/close time, min 0.10 `SheetSettleMin` | OutCubic | dismiss if p < 0.75 (`SheetDismissFraction` 0.25) or the downward speed is ≥ 1200 px/s (`SheetDismissVelocity`); otherwise re-open |
| Tab switch with a sheet open | α 0.12 `PanelFade`, indicator 0.18 `TabIndicator` | linear / OutCubic | no slide, no pan (M52 values) |
| 사냥 glow on/off | 0.12 `MainGlowFade` | OutCubic | α only; the glow is static, not a loop |
| Rail icon appears on unlock | 0.22 `BadgeAppear` | OutBack | 0→1.2→1. Wave 1c: on `Features.Changed`. Once WP-7 adds the M78 hold: after the fly-to-slot lands (CR-24) |
| Auto-return | 60 `SheetAutoReturnSeconds` | – | §4.8 |
| Reduced motion (`UIMotion.Reduced`) | sheet α in 0.10 / out 0.08 (`SheetReducedIn/Out`) | linear | no slide. The pan snaps at the start of an open and at the end of a close. A drag still follows the finger and snaps on release |

All on unscaled time, no per-frame allocations. Sound: the existing `Tab` hook on open and switch, nothing on close (the medallion's PressScale tap sound still plays on 사냥). No haptics.

**First frames (CR-13).** Activating ContentArea runs the panel's OnEnable and a full build of the ContentArea canvas. On heavy panels (Characters grid, Shop grid) that can be one long frame. For the first 2 frames after activation the sheet clock advances by `min(unscaledDeltaTime, 1/30)`, so the hitch never skips the first third of the slide.

### 1.6 HUD per state [IA-08]

| Element | MAIN | SHEET | Mechanism |
|---|---|---|---|
| TopBar (all) | shown | shown | — |
| StagePanel, SpeedButton, AutoButton, their lock covers | shown | shown | — |
| RecommendedPower | shown | hidden (α 1→0 with p, raycast off) | `SheetFade 0` on the root |
| SynergyRow (M43 chip) | shown | hidden | `SheetFade 0` |
| IdleChestWidget | shown | α 0.5, still tappable. **α 1 while the tutorial is active** | `SheetFade 0.5`, `_ignoreDuringTutorial` |
| BossChallengeButton (farm only, M13) | shown | α 0.5, still tappable | `SheetFade 0.5` |
| QuickRail_L / QuickRail_R | shown | hidden | `SheetFade 0` |
| EmptyHint (no guardians) | shown | hidden | `SheetFade 0` |
| GuideQuestBar, DeckTab, Strip (deck + lamp) | shown | under the opaque sheet (no change) | — |
| DangerVignette, BannerArea, SynergyBannerArea, NoticeArea | the field | the visible band only | `BattleFrameDriver` sets their bottom inset to `max(156, 836·p)` |
| Return chip (M72) | never (MAIN clears the slot) | in the sheet header | §4.2 |
| Toast | top edge y 480 | top edge y 480 | unchanged |

Validation (Appendix B): with this table, all five lanes stay 100% visible from spawn in SHEET. Kept opaque, the idle chest would hide lane 4 for up to 0.43 world units (1.4 s), the boss button lane 1 for up to 0.78 (2.6 s), and the pill + synergy row lanes 0 and 1 for up to 0.32 and 0.50.

### 1.7 Unchanged
- Battle simulation, AUTO, auto-summon and auto-lamp: unchanged. The battle never pauses or slows for a sheet.
- Popups, tooltips, toasts, flyers and fanfares stay above SafeArea (UX-SPEC §3.1).

---

## 2. Bottom nav [IA-05]

### 2.1 Order and why
**강화 · 편성 · 친구들 · 사냥 · 원정 · 소환 · 상점**
1. Semantic halves: left = build your team (grow, place, collect), right = get more (send, summon, buy).
2. Same 3+1+3 as 세븐나이츠 키우기, and it keeps the `MainTab` enum order (0–5). TabGate arrays, `(MainTab)i` casts and AutoShot shot names stay valid.
3. The home button is the largest and most central target. 강화, the most frequent tab, stays on the bottom arc that the thumb reaches.

Rejected: home at one end (no symmetry; home not central); merging 소환 into 친구들 (gacha is a top-level loop and the 1-3 unlock card points at its tab); 5 tabs with 원정/상점 inside ≡ (hides daily loops).

### 2.2 Geometry
- TabBar 1080×170 (unchanged). The HorizontalLayoutGroup gets `childForceExpandWidth = false`; each button has a `LayoutElement` (side 148, centre 192; 6×148 + 192 = 1080).
- Slot x: 0–148, 148–296, 296–444, **444–636**, 636–784, 784–932, 932–1080.
- Side tab (today's anatomy, narrower): `SelectedPlate` `Stretch(6,6,2,0)` (136×168); `Icon` 92 at TopCenter(0,−14); `Label` 28 bold at `Bottom(18,44)` ('친구들' is ≈84 px wide); `TabLock` 92 at TopCenter(0,−14); `MetaDot` 28 at TopRight(−30,−10).
- Centre `Tab_Main` (192×170):

| Child | Rect | Content |
|---|---|---|
| `Hit` | Stretch | transparent Image, raycast target; `Button` target graphic (hit 192×170) |
| `Glow` | **200×200**, centred on the medallion (bar-local y 77), so it rises 23 px above the bar (CR-12) | existing glow sprite (`MetaIcons.Glow`), Lamp@0.45, static; MAIN only. `raycastTarget = false`: at 240 it rose 43 px over the deck strip and, as a raycast target, would eat taps on the lower edge of Card1–Card3 |
| `Medallion` | At(TopCenter,(0,−2),(150,150)) | `UiArt.MedallionDark`; `PressScale` on this node |
| `Icon` | At(TopCenter,(0,−21),(112,112)), preserveAspect | `ArtLib.Battle("shop")` (centre at bar-local y 77) |
| `LabelPlate` | At(BottomCenter,(0,10),(112,36)) (bar-local y 124–160) | `UiArt.PillNight9s` + 2 px GoldAccent outline |
| `Label` | Stretch in the plate | '사냥', Body 26 bold |
| `MetaDot` | At(TopCenter,(58,−14),(30,30)) | `RedDotAnyView` (§5.9) |

Every `Tab_Main` child except `Hit` has `raycastTarget = false`.

### 2.3 States

| | MAIN | SHEET(tab) | Locked side tab |
|---|---|---|---|
| Centre | Glow α1, Label Gold, Icon α1 | Glow α0, Label SubText, Icon α0.8, aggregate dot if any | never locked |
| Selected side tab | – | SelectedPlate on, icon 1.12 + 6 px lift (M52 timings), Label Gold | TabGateView: icon/label α0.45 + lock + '1-3' pill (unchanged) |
| Indicator (M52 Gold 104×6 at the bar top) | α0 (0.12) | centred on the selected side tab (x 74 / 222 / 370 / 710 / 858 / 1006); it touches the sheet's bottom edge | – |
| Side tab dots | all may show | the selected tab never shows a badge (M09) | NEW pill per M09 |

- **Tab_Main aggregate dot (CR-07):**
  - It ORs `RedDotSystem.IsClaimOn` (claimable leaves only, §5.9) over the dot paths of every rail icon currently placed (rails and tray, published by `QuickRailView.Changed`) plus `hud.guidequest`. v1 = `top.event`, `top.attendance`, `top.journal`, `hud.guidequest`.
  - It shows only while a sheet is open; in MAIN those dots are already visible on the rails and the guide bar.
  - NEW-only (FeatureDot) leaves never light 사냥, so a just-unlocked rail icon cannot leave a permanent dot on it. A rail icon added later (Phase 4) is included with no list to update.
- M35d growth glow: shown while the Growth sheet is not open (MAIN or another tab). Needs the `RefreshGrowthGlow` change in §5.3 (CR-08).
- BottomBleed (M68): unchanged.

### 2.4 Centre art
- 0 credits: `ui_medallion_dark` + `Battle/shop.png` + `ui_pill_night_9s` + the existing glow sprite. Mock: `ia_10_mock_pair_half.png`.
- Copy: '사냥' (the user's word). The team may switch to '수선점' if it prefers in-world wording; do not use '홈'.
- No X/close swap on the centre (7K style). The button keeps one identity, and the sheet has its own close controls (§4.3).

---

## 3. MAIN screen [IA-06, IA-07]

### 3.1 Top bar (130; replaces UX-SPEC §2.2 layout)

| Element | Rect (TopBar-local) | Screen | Style |
|---|---|---|---|
| `TopBleed` (M68) | first sibling, anchors (0,1)–(1,1), pivot (0.5,0), size (0,400) | above y 0 | Panel, raycast off |
| Velvet | Stretch | 0–130 | `UiArt.BarVelvet9s`, gold seam on the bottom edge (as today) |
| `LevelText` | At(TopLeft,(24,−10),(292,40)) | 24–316 × 10–50 | Title 28 bold Cream '수선점 Lv.{n}', Fit 22 |
| `PowerChip` | At(TopLeft,(16,−54),(300,62)) | 16–316 × 54–116 | PanelLight@0.6 rounded. Icon 40 at MiddleLeft(12,0). `PowerText` At(MiddleLeft,(60,0),(228,56)), 38 bold Gold, autosize 28–40. Tap → the RecommendedPower toast text (M10) |
| `GoldChip` | At(TopRight,(−446,−14),(240,60)) | 394–634 × 14–74 | `SceneParts.CurrencyChip` |
| `GoldPerSecText` (name kept) | At(TopRight,(−446,−80),(240,28)), right-aligned | 394–634 × 80–108 | Hint 20 SubText '초당 +{rate}' (§1.10 rate format), written on change |
| `GemChip` | At(TopRight,(−224,−14),(210,60)) | 646–856 × 14–74 | CurrencyChip; M08 '+' badge fits (amount right inset 52) |
| `MailButton` (name and path kept) | At(TopRight,(−116,−17),(96,96)) | 868–964 × 17–113 | icon `Icons/menu_mail` 64 at TopCenter(0,−2); label '우편' 20 bold Cream outlined, At(BottomCenter,(0,0),(96,26)); count badge (§1.5) |
| `MenuButton` (new) | At(TopRight,(−12,−17),(96,96)) | 972–1068 × 17–113 | icon `Icons/menu_all` (new, §9.3; fallback `Icons/menu_settings`), label '메뉴'; plain dot = unread notices |

- Removed from the top bar: Logo (M10), NoticeButton (M10), BagButton and SettingsButton (→ ≡ menu, §3.6), JournalButton and AttendanceButton (→ rails, §3.3), EventChip (→ event rail icon; the candy balance stays in the Shop event tab). This supersedes SPEC4 conflict #2 (§8.8).
- No overlaps: left block x ≤ 316, chips 394–856, icons 868–1068. The 8 px gap between 우편 and 메뉴 clamps their HitPads (UX-SPEC §1.5).
- `TopBarView`:
  - Remove `_eventText`, `_eventChipButton`, `_bagButton`, `_noticeButton`, `_settingsButton` and `_noticeBadge`.
  - Add `_menuButton` (opens `MenuPopup`). The ≡ dot is a `RedDotView` on `MenuButton`, dot mode, bound to `RedDotPaths.TopSettings` (`top.settings`), which is already the parent of `top.settings.notice` (CR-17). Phase 4 menu-only leaves register under `top.settings.*` and light it with no view change.
  - `GoldPerSecText` is written by `TopBarView` as '초당 +{rate}' (§1.10 rate format) and stays active.
- `TopBarMetaView` (CR-14):
  - It binds `_statsText`/`_statsPunch` to `PowerChip/PowerText` (M10).
  - `WriteStats` writes **the number only** (`PowerSystem.Format`). Today it writes '전투력 {p}  · 초당 단추 {r}', which would overflow the 228-px PowerText from Wave 1c until WP-7.
  - The builder no longer binds `_legacyGoldPerSec`. Today that binding hides `GoldPerSecText` whenever meta is on.
  - It no longer creates `PowerStatsText` or the journal and attendance buttons.
- `PowerUpFloatView` still finds a TopBar child whose name contains "Power" (`PowerChip`).

### 3.2 Battle top band (StageArea-local positions unchanged; screen y re-based)

| Element | Screen rect | Spec |
|---|---|---|
| StagePanel | 14–344 × 142–276 | M11 |
| RecommendedPower root / pill | 352–652 × 142–222 / 155–207 | M12 |
| SpeedButton (x1) / AutoButton | 794–914 / 926–1066 × 144–220 | M22 |
| SynergyRow | 16–≤540 × 284–348 | M43 |
| IdleChestWidget | 906–1056 × 250–420 | M32 |
| BossChallengeButton (farm only) | 16–246 × 356–424 | M13 |
| Stage-clear ribbon (transient) | 282–798 × 372–474 | §8.3 |
| Drag-aim hint (transient) | 330–750 × 148–200 | §8.3 |

The top lane's normal enemies reach up to y 444, and bosses with name plates up to y 362. The centre column (x 324–755) stays clear of every HUD element.

### 3.3 Quick rails
- Containers: `Battle/MetaHud/QuickRail_L` and `QuickRail_R` (Stretch). They hold `QuickRailView` and a `SheetFade 0`.
- Slots (cell 104×116; StageArea-local anchors; screen on 16:9):

| Slot | Anchor | Screen | v1 content |
|---|---|---|---|
| L1 | At(TopLeft,(12,−306),(104,116)) | 12–116 × 436–552 | `EventButton` 이벤트 |
| L2 | At(BottomLeft,(12,412),(104,116)) | 12–116 × 1222–1338 | (2nd bottom icon / 더보기) |
| L3 | At(BottomLeft,(12,288),(104,116)) | 12–116 × 1346–1462 | `AttendanceButton` 출석부 |
| R1 | At(TopRight,(−12,−306),(104,116)) | 964–1068 × 436–552 | reserved for SPEC4 StatsButton (§8.8) |
| R2 | At(BottomRight,(−12,412),(104,116)) | 964–1068 × 1222–1338 | (2nd bottom icon / 더보기) |
| R3 | At(BottomRight,(−12,288),(104,116)) | 964–1068 × 1346–1462 | `JournalButton` 수선 일지 |

- **Cell anatomy (`QuickIconView`):**
  - `Plate`: `UiArt.MedallionDark`, At(TopCenter,(0,0),(88,88)).
  - `Icon`: 62×62, centred in the plate.
  - `LabelPill`: RoundSprite, Background@0.78, At(BottomCenter,(0,0),(100,28)). Label 20 bold Cream, Stretch (outline not needed on the pill); '수선 일지' fits.
  - Badge per UX-SPEC §1.5 at the plate's top-right (−4,−4).
  - Optional `SubPill`: At(TopCenter,(0,+10),(64,26)), pivot (0.5,1), so it rises 10 px above the cell. PillNight9s, Micro 18 Gold, for a 'D-n' countdown (Phase 4).
  - `Button` + `PressScale` on the cell; the hit area is the whole 104×116 cell.
- **Gates and actions:**
  - 이벤트: `FeatureId.Events`. Tap → `UINav.GoToSource(ItemSourceType.Shop, ShopDatabase.EventShopId)` (the old EventChip route; Phase 4 swaps in the event hub). Dot `top.event`.
  - 출석부: Attendance unlocked **and** FTUE done (the old top-bar gate). Tap → `UINav.OpenAttendance()` (`Open`, not `OpenAuto`, per M69). Dot `top.attendance`.
  - 수선 일지: `FeatureId.Missions`. Tap → `UINav.OpenJournal()`. Count `top.journal`.
  - Rails are naturally empty during FTUE (gates open at 1-7, after FTUE, and 3-1/day 3).
  - **Seen on tap (CR-06):** `QuickIconView` calls `Game.Features.MarkSeen(feature)` when `IsNew(feature)`, before its action.
    - Today nothing ever marks `FeatureId.Events` seen. Its unlock card is reserved off (`ReservedFeatureCards = false`), and the old EventChip had no dot.
    - Once the rail binds `top.event`, the event icon would otherwise keep a NEW badge forever.
- **Placement rules:**
  - Visible icons sort by priority (event > attendance > journal > later additions).
  - A side's top-pocket icon takes slot 1. Bottom icons fill bottom-up (slot 3, then slot 2).
  - Max 3 per side (6 total), plus the basket widget.
  - Overflow: when more than 2 bottom icons qualify on a side, slot 2 becomes `더보기`. It uses `UiArt.Chevron` rotated up, label '더보기', and a badge counting hidden icons with a claimable dot. It opens a tray:
    - PanelNight9s @0.94, 128 wide; items stack upward from the slot top at a 124 px pitch; at most 4 visible, then it scrolls.
    - It closes on an outside tap, back (back-handler stack), an item tap, or when a sheet opens.
    - Tray rows are **proxy cells** (same icon, label, dot path and action). The frozen-path icons are never reparented into the tray; an overflowed icon stays under `QuickRail_L/R`, inactive (CR-11).
    - v1 never shows it.
- **Short screens:** if the StageArea is shorter than 1500 px (4:3 tablets), the top-pocket icons join the bottom group, overflowing into the tray.
- **Unlock:**
  - In Wave 1c the icon appears with `BadgeAppear` on `Features.Changed`.
  - WP-7 (M78) adds the hold: the icon is placed at α0, so the card can read its rect, and appears when the flight lands or the card is skipped (CR-24).
- `QuickRailView` is event-driven (Features.Changed, RedDot changes, Start). There is no Update.

### 3.4 Bottom band (MAIN)

| Element | Screen rect | Spec |
|---|---|---|
| EmptyHint (no guardians only) | 260–820 × 1390–1454: `Hud`-local At(BottomCenter,(0,296),(560,64)) | moved off the ring; tap → `GoToTab(Formation)` (opens the sheet) |
| GuideQuestBar | 16–484 × 1492–1576 | M04, unchanged local `BottomLeft(16,174)` 468×84 |
| DeckTab | 212–530 × 1576–1594 | M36 |
| Strip: Resonance / Deck / Lamp | 0–1080 × 1594–1750 | M36, M37 |

### 3.5 Keep-out validation (Appendix B)
- Battle content envelope in MAIN: y 444–1292 for normal enemies; y 362–1292 including bosses, name plates and +36 px for damage numbers. The edge columns x 0–120 and 960–1080 are blocked from y 558 to y 1208.
- All six rail slots, the boss button and every §3.2/§3.4 element show **0.0% overlap** with that envelope.
- With all of them shown, every lane is 100% visible from spawn.

### 3.6 ≡ Menu popup (new, `Popups/MenuPopup`)
- `BuildPopup(900, 300 + 220·rows)`: 740 for v1's 2 rows. Title '메뉴' (H1 40).
- `Window/Body/Grid`: GridLayoutGroup, 3 columns, cell 240×196, spacing 24×24, centred, top 136 below the window top.
- Tiles in order; gated tiles are hidden and the grid reflows:

| Tile | Gate | Icon | Badge | Tap |
|---|---|---|---|---|
| `JournalTile` 수선 일지 | Missions | `Icons/journal` | count `top.journal` | Close → `UINav.OpenJournal()` |
| `AttendanceTile` 출석부 | Attendance + FTUE done | `Icons/attendance` | dot `top.attendance` | Close → `UINav.OpenAttendance()` |
| `EventTile` 이벤트 | Events | `Icons/currency_event` | dot `top.event` | Close → event route (as the rail); while `EventHubTile` is shown: label '이벤트 상점', route = event shop (RM-2 P4-4) |
| `BagTile` 가방 | – | `Icons/menu_bag` | – | Close → `BagPopup.Open()` |
| `NoticeTile` 공지 | – | `Icons/menu_notice` | count = unread notices | Close → `NoticePopup.OpenList()` |
| `SettingsTile` 설정 | – | `Icons/menu_settings` | – | Close → `SettingsPopup.Open()` |

- Tile anatomy: `Plate` (`UiArt.PanelInset9s`, 240×196) + `PressScale`; Icon 104 (IconXL) at TopCenter(0,−18); Label 26 bold Cream at `Bottom(16,36)`; Badge at TopRight(−8,−8).
- The journal, attendance and event tiles duplicate the rails on purpose, so they stay reachable while a sheet hides the rails.
- The ≡ `MenuButton` dot aggregates **menu-only** items through `RedDotView(top.settings)` (CR-17): v1 = unread notices. Rail items are not re-aggregated there, to avoid triple badges (research pitfall 1).

---

## 4. SHEET [IA-02, IA-09]

### 4.1 Height
- One height: 836 = `SheetHeader` 56 + ContentArea 780. When open, ContentArea is today's 970–1750 rect, so panel layouts, coach target rects and the FTUE first-meeting cover are unchanged.
- No title row in the header: every panel already identifies itself (Growth/Characters PanelHeader titles, Expedition/Shop segments, the Gacha banner, the Formation mini-field).
- P2 backlog: a "tall" variant (sheet top y 440) for list tabs, opened with a header ⌃, only after WP-4/WP-6 make those panels height-adaptive. The pan rule already covers it.

### 4.2 Header anatomy (`ContentArea/SheetHeader`, 1080×56)

| Child | Rect | Content |
|---|---|---|
| `Shadow` | anchored above the header, 40 tall | generated vertical fade, Background α 0→0.45 toward the sheet, raycast off |
| `Velvet` | Stretch | `UiArt.BarVelvet9s` with the gold seam on the **top** edge (flipped like the tab bar) |
| `DragArea` | Stretch | transparent raycast Image + `SheetDragHandle` |
| `Handle` | At(TopCenter,(0,−8),(120,40)) | `UiArt.PillNight9s` + GoldAccent@0.8 2 px outline; `UiArt.Chevron` rotated −90° (pointing down) 28×28 Cream |
| `HandleHit` | At(TopCenter,(0,+24),(200,80)), pivot (0.5,1): from 24 px above the sheet top down to the header's bottom edge | transparent `Button` → close |
| `ReturnChip` (M72, moved) | At(MiddleLeft,(16,0),(300,48)) | PanelLight round, 2 px GoldAccent outline, '‹ {label}' 24 Cream autosize 20–24, a '×' glyph on the right whose hit is 48 wide × the full 56-px header (raycastPadding). Hidden during FTUE, coach marks and popups (M72 rules) |
| `RightSlot` | At(MiddleRight,(−16,0),(300,48)) | empty; reserved (P2 ⌃ tall) |

The header is opaque. Together with the content backdrop (`content_bg`), the sheet hides everything under it.

### 4.3 Gestures and controls

| Gesture | Result |
|---|---|
| Tap `HandleHit` | close |
| Drag down anywhere on `DragArea` or the handle | p = p0 − Δy/836 (canvas px). Release: close if p < 0.75 or the downward speed is ≥ 1200 px/s, otherwise re-open (§1.5) |
| Drag up | no effect (no tall variant in v1) |
| Re-tap the open tab | `Reselected`, then close |
| Tap 사냥 | close |
| Tap inside the panel | normal |
| Drag inside a panel ScrollRect | scrolls; never moves the sheet |

The scroll-to-top on re-tap (M52) is dropped: re-tap has exactly one meaning, close (Appendix A). Panels keep their scroll positions across tab switches within a session.

### 4.4 Scroll areas
Unchanged: each panel's own ScrollRects (Characters grid, Shop grid, Expedition zone list, Growth lineage row, Formation card list). Only the header moves the sheet.

### 4.5 Under the sheet
- The deck strip, lamp, DeckTab, guide quest bar and bottom rails are covered while the sheet is open. Their objects stay active; only raycasts are blocked by the sheet.
- After 1-2, AUTO keeps auto-summoning and lighting the lamp (LampPolicy). Before 1-2, the only sheet the FTUE opens is Growth (steps 6–7, under FTUE battle protection).
- Guide quest completion surfaces as the `Tab_Main` aggregate dot.
- Deck interaction (tap or drag summon) needs MAIN. `SummonAimView` cancels an aim in progress when p becomes > 0.
- Optional optimisation (P2): when p = 1 and settled, set the `Field` RectMask2D bottom padding to 680, which culls the ≈46% of the field under the sheet. Clear it when a close starts.
- Optional optimisation (P2, CR-18): give `Strip` its own Canvas and disable that Canvas while p = 1 and settled. Adding a component leaves the coach paths unchanged.
  - Why: the deck cooldown radials and the lamp gauge redraw every frame and dirty the whole Battle canvas even while fully covered.
  - Logic keeps running, and `IsUnderSheet` already keeps coach targets off the strip.
- Known trade-off (P2 backlog): before LampPolicy (1-6), a full lamp is not visible from SHEET. A later pass may light a small lamp glyph on the 사냥 medallion. It is not a red dot (M09).

### 4.6 Popups over sheets
Popups, their dims, tooltips, the tutorial overlay, fanfares, system popups and toasts all draw above SafeArea and cover both the battle band and the sheet. A popup can open sheets through navigation: `MetaNavigator.GoToTab` closes popups first (existing `CloseAllPopups`), then opens or switches the sheet.

### 4.7 Android back (replaces UX-SPEC §3.2)
`UIManager.OnBack` resolves in this order (CR-05, CR-21):
1. Back-handler stack, newest first: SleepModeView (wake), StoryIntroView, FirstMeetingCardView, the rail tray. Then WP-3a's InfoTooltip tier.
2. `CloseTop()`: the top popup's `RequestDismiss` (M49).
3. `UINav.TryPopReturn()`, **only while `ReturnChipView` is shown**. This consumes back.
   - The slot exists only while a sheet is open. The chip hides during FTUE, coach marks and popups, and back must not act on an invisible affordance.
   - It restores the frame the slot was pushed from, then reopens the source (§5.2).
4. Sheet: if `UIFrame.SheetOpen` → `UINav.GoToMain()`. If a header drag is in progress, `SheetDragHandle.Cancel()` ends it first and the sheet closes from the current p.
5. Exit confirm (M15 copy).

A second back while the sheet is still closing goes straight to step 5. There is no double-back-to-quit.

### 4.8 Auto-return to MAIN (setting)
- `GameSettings.SheetAutoReturn` (bool, key `settings.sheet_auto_return`, default **true**).
- `SheetController` counts unscaled seconds without input while a sheet is open and settled. At 60 s → `UINav.GoToMain()` (animated).
  - Input uses the `SleepModeView.HasInput` definition (touch held, mouse button held, any key down, scroll, mouse moved > 2 px), so the two idle clocks agree. A long-press on 강화 x1 counts as input.
- The count resets on `OnApplicationFocus` and `OnApplicationPause`, and each frame adds at most 0.1 s. A resume after minutes in the background never fires a close on its first frame (CR-25).
- Tools and tests (CR-04):
  - AutoShot and every PlayMode fixture set `GameSettings.SheetAutoReturn = false` in SetUp and restore it in TearDown.
  - Only `UiFrameTests (h)` turns it on, with the seconds shortened through `SheetController.AutoReturnSecondsForTests`.
  - Otherwise AutoShot's ~60 s of sheet-open, popup-free time before `battle_later` closes the sheet at a timing-dependent shot.
- The timer is held at 0 while: a popup is open, `Game.Tutorial.IsActive`, a coach mark is visible, a hold or drag is in progress, or the sheet is moving.
- With the default sleep time (2 min), the order is: 60 s → MAIN, 120 s → sleep.
- Settings row (WP-4, General tab, under 잠자기 모드): '전투 화면으로 돌아가기', desc '1분 동안 손대지 않으면 열린 창을 내려요', MetaSwitch.

### 4.9 Sleep mode, map visible (replaces UX-SPEC §2.11 sleep layout; WP-IA Track A)
- Before sleeping: if a sheet is open, close it first (animated), and start the sleep fade only after it settles.
- **Overlay** (`SafeArea/SleepMode/Overlay`, Stretch, plus 400 px bleed on both insets per M68):
  - `Dim`: Background α **0.5** (CR-16; was 0.35), raycast target (wakes on tap, as today). Sleep still exists to save power: the night art at half brightness, 30 fps and a veiled HUD keep most of the old 85%-dark saving, and the fight stays readable.
  - `TopVeil`: Top(0,460), generated fade, Background α 0.92 from the top to y 300, fading to 0 by y 460 (CR-16). It hides the top bar and the top HUD band (StagePanel, x1/AUTO and most of the chest). At 330 with a 140 px fade, the chest and the boss button stayed bright.
  - `BottomVeil`: Bottom(0,600), fade 0 → 0.92 over its upper 180 px. It hides the rails, guide bar, strip and tab bar.
- **Status card** At(BottomCenter,(0,236),(880,136)), screen y 1548–1684, `UiArt.PanelNight9s` α 0.92:
  - `Moon` 80 at MiddleLeft(20,0).
  - `Title` '쉿, 친구들이 수선점을 지키고 있어요' (Title 28 bold Cream) at (116,−22), 740×40.
  - A stats row (HLG at (116,−70)) of the existing `KillsText` '물리친 잔향 {n}', `GoldText` '모은 단추 {x}' (Gold) and `StageText`, Label 24, separated by ' · '.
- `Hint` '화면을 누르면 깨어나요' (Hint 20 SubText, MetaPulse) at `Bottom(196,30)` (screen y 1694–1724, under the card).
- The old centred shop picture is removed: the real shop is visible. `_shop` becomes optional and null-safe.
- Unchanged: 30 fps while asleep, wake on any tap (consumed), the M01 focus handlers, and the M15 back handler.
- New `public void SleepNow()` for tools and tests. It takes the same path as the timer, including the close-the-sheet-first rule. The AutoShot `sleep` capture needs it, and no hook exists today (CR-04).
- Settings copy for 잠자기 모드 (M80 row): '손대지 않으면 전투만 남기고 어둡게 해요'.

### 4.10 Feedback placement per state

| Feedback | MAIN | SHEET |
|---|---|---|
| Toast (M14) | top edge y 480 | y 480 (over the battle band, not the panel) |
| Gold/gem flights (M21) | GoldChip / GemChip | same (the top bar is always visible) |
| Event-token flights | `QuickRail_L/EventButton` | `Tab_ShopPanel` (rail faded): `RewardFlyer.ResolveTarget` treats `UIFrame.IsFeedbackHidden` targets as hidden (CR-03) |
| Item flights (was BagButton) | `TopBar/MenuButton` | same |
| Friend flights | `Tab_CharacterPanel` | same |
| Stage-clear coins | from the battle | from the visible band (§6.5) |
| Power float (M46) | `TopBar/PowerChip` | same |

---

## 5. Navigation APIs, tutorial and coach paths [IA-05, IA-09]

### 5.1 `UIFrame` (new, `Scripts/UI/UIFrame.cs`): the frame contract
```csharp
namespace IdleGame.UI
{
    /// <summary>화면 틀(IA) 상태. SheetController만 쓰고, 전투 카메라·HUD 흐림·튜토리얼·코치가 읽는다.</summary>
    public static class UIFrame
    {
        public const float TopBarHeight = 130f, TabBarHeight = 170f, StripHeight = 156f;
        public const float SheetHeaderHeight = 56f, SheetContentHeight = 780f;
        public const float SheetHeight = SheetHeaderHeight + SheetContentHeight;   // 836
        public static bool SheetOpen { get; private set; }        // target state (true from Open() on)
        public static int SheetTab { get; private set; } = -1;    // MainTab index or -1 (MAIN)
        public static float SheetProgress { get; private set; }   // eased shown amount 0..1
        public static bool SheetSettled { get; private set; } = true;
        public static RectTransform SheetRect { get; private set; } // ContentArea
        public static RectTransform StageRect { get; private set; } // StageArea
        public static event System.Action<float> SheetProgressChanged; // only when p changes
        public static event System.Action<bool> SheetStateChanged;     // target open/closed
        /// <summary>StageArea 안 대상이 열린(또는 열리는) 시트 밑에 가리는지: 목표 상태(SheetOpen) + 다 열린 시트 위 끝 기준. 탭 바·상단 바·팝업 대상은 false</summary>
        public static bool IsUnderSheet(RectTransform target);
        /// <summary>보상 날아가기 도착지로 못 쓰는지: IsUnderSheet 또는 SheetFade 알파 &lt; 0.05</summary>
        public static bool IsFeedbackHidden(RectTransform target);
        internal static void Bind(RectTransform sheetRect, RectTransform stageRect);
        internal static void Unbind();
        internal static void SetState(bool open, int tab);
        internal static void SetProgress(float p, bool settled);
    }
}
```
`IsUnderSheet` (CR-02) is true when all three hold:
- `SheetOpen` is true. This is the **target** state, so it flips in the same frame as `Select`/`ShowMain`.
- The target is under `StageRect` (not the sheet, the tab bar, the top bar or a popup).
- Its rect centre, in SafeArea space, is below the **fully open** sheet top: `SafeArea.yMin + TabBarHeight + SheetHeight`, a constant that does not move with p.

The v1 rule (`p > 0.5`, the current header edge, any target) had two failures:
- On a tab change the resolver re-ran while p was still 0, kept `DeckCard0`, and held it until its 1 s timer, so `ftue_coach_sheet` (captured at 0.6 s) failed.
- A tab-bar target counted as under the sheet (its centre is below the header), so FTUE step 10's `TabGacha` was redirected to 사냥 whenever another sheet was open.

No coach target lives under `Field/Camera`, so the pan never moves a target this test reads. It allocates nothing (cached corner array).

`IsFeedbackHidden(t)` = `IsUnderSheet(t)` or the nearest `SheetFade` ancestor's alpha < 0.05 (`GetComponentInParent`, called once per flight, no allocation).

State reset (CR-20):
- `Bind` resets every field (closed, tab −1, p 0, settled), and `Unbind` (from `SheetController.OnDestroy`) clears the rects.
- A `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset also clears the events.
- Why: statics outlive the scene in PlayMode tests, and each test reloads Main. A stale `SheetOpen` or a destroyed `SheetRect` would leak into the next test.

### 5.2 `UINav`
- `GoToTab(MainTab)`: unchanged signature. It now **opens or switches the sheet and never closes it**. All existing callers (BattleHud:79, EchoDeckView:71, GuardianSlotView:407, CharacterDetailPopup:358, EvolutionCraftPopup:250, FormInfoPopup:134, SynergyDetailPopup:322, SourceNavigator:24-37, MetaNavigator:216-284) need no change. A guardian-pad tap now opens Formation while the battle pans up and the pads stay in view.
- New: `public static event Action<bool> MainRequested; public static void GoToMain(bool immediate = false)`. Tools and tests use `immediate`.
- `ClearReturn()` runs in `MainTabController.ShowMain`, so every path to MAIN clears the slot (사냥, re-tap close, handle, drag dismiss, back, auto-return, `GoToMain`). This is in addition to the M72 different-tab rule (CR-05).
- `PushReturn(label, reopen)` also captures the frame at push time (`UIFrame.SheetTab`, −1 = MAIN). `TryPopReturn` restores it before calling `reopen` (CR-05):
  - captured −1 → `GoToMain(immediate: true)`;
  - a tab other than the current one → `GoToTab(tab)`;
  - the current tab → nothing.
  - `TryPopReturn` reads the delegate and the captured tab, then clears the slot, as today, before it restores, so the `ClearReturn` inside `ShowMain` is a no-op there.
  - Example: 수선 일지 opened from a MAIN rail → 가기 → the Growth sheet with '‹ 수선 일지로'. Without the restore, the pop reopens the journal over the Growth sheet, and closing the journal leaves open a sheet the player never opened.

### 5.3 `MainTabController` (rewrite of selection; WP-3a motion kept)
- New fields: `Button _mainButton`, `GameObject _mainGlow`, `TMP_Text _mainLabel`, `Graphic _mainIcon`, `SheetController _sheet`. `_defaultIndex = -1`.
- `SelectedIndex` is −1 in MAIN. `IsMain => SelectedIndex < 0`.
- `Select(int)` (programmatic): `SelectGuard` → switch panels (activate the new one, deactivate others; α fade 0.12 only when switching while open) → `_sheet.Open()`. It never toggles.
- `ShowMain(bool immediate = false)`:
  - Sets `SelectedIndex = -1` at once (target state), calls `UINav.ClearReturn()` (CR-05) and raises `MainShown`.
  - Calls `_sheet.Close(immediate)`; panels deactivate when the close settles.
  - A `Select` during the close re-targets open from the current p.
- User taps:
  - `OnTabClicked(i)`: if `i == SelectedIndex` → `Reselected?.Invoke(tab)`, then `ShowMain()`. Otherwise `UINav.ClearReturn()` + `Select(i)`.
  - `OnMainClicked()`: if not MAIN → `UINav.ClearReturn()` + `ShowMain()`.
- Events: `Selected(MainTab)` fires when a sheet tab becomes selected (open or switch; TabGateView MarkSeen relies on it). `Reselected(MainTab)` as M52 (fires before the close). New `MainShown`.
- Subscriptions: `UINav.TabRequested += Select`, `UINav.MainRequested += ShowMain`. Sets `UIManager.Instance.SheetBackHandler` (§5.5).
- Visuals: side plates, labels and icons (M52); indicator hidden in MAIN; centre states (§2.3); M35d glow rule (§2.3).
  - `RefreshGrowthGlow` drops its `SelectedIndex >= 0` term (WP-3a code: `SelectedIndex >= 0 && SelectedIndex != Growth && …`). Otherwise the glow never shows in MAIN, the state players see most (CR-08).
  - Until WP-4 publishes `GrowthPanel.Cost10`, `GrowthCost10` is null and the glow stays off, with no error.
  - Remove `ScrollPanelToStart` and the per-selection `FadeInPanel` (already implemented by WP-3a, §8.1).
- `Start`: `Apply(-1)`. No `Selected` is raised at boot.

### 5.4 `SheetController` and `SheetDragHandle` (new, `Scripts/UI/Main/`)
- `SheetController` lives on `SafeArea/TabBar`, which is always active, next to `MainTabController`.
- Fields: `_content` (ContentArea), `_header`, `_group` (ContentArea CanvasGroup), `_handle` (Button), `_drag` (SheetDragHandle).
- `Open()`: activate ContentArea, set the target to 1, animate from p (§1.5), `UIFrame.SetState(true, tab)`.
- `Close(bool immediate)`: set the target to 0 and animate. At p = 0: ContentArea `SetActive(false)`, then raise `Closed`.
- Position: `_content.anchoredPosition.y = 170 − 836·(1 − p)`. At p = 0 the header lies exactly behind the tab bar.
- Reduced motion: `_group.alpha` path (§1.5).
- Each changed p → `UIFrame.SetProgress(p, settled)`.
- The component `enabled`s itself only while animating, dragging, or open (for the §4.8 timer). Closed and settled → `enabled = false`.
- `SheetDragHandle` (IBeginDrag/IDrag/IEndDrag on `DragArea` and `HandleHit`):
  - Converts screen deltas to canvas units with `rootCanvas.scaleFactor` (ContentArea is a nested canvas) and tracks the speed over the last 0.1 s.
  - Implements the M01 rule: `OnApplicationFocus(false)` during a drag snaps back.
  - `Cancel()` ends a drag without a release decision (used by back, §4.7).
- First frames: §1.5 (CR-13). Idle count: §4.8 (CR-25).

### 5.5 `UIManager`
- New `public Func<bool> SheetBackHandler { get; set; }`.
- `OnBack()` follows §4.7: back stack → `CloseTop()` → `UINav.TryPopReturn()` → `SheetBackHandler?.Invoke() == true` → exit confirm.
- `CloseAllImmediate()` (WP-3a) does **not** touch the sheet. Tools call `UINav.GoToMain(true)` when they want MAIN.

### 5.6 Deep links (`MetaNavigator`)
- `GoAndFocus(nav, focusPath)`:
  - If `nav == FeatureId.Battle`, or the focus path is under `SafeArea/StageArea/` and `UIFrame.IsUnderSheet`, call `UINav.GoToMain()` first, then `FocusTarget`. The coach finger already waits until the target is shown.
  - Tab navigation is unchanged (opens the sheet).
- New `public static string FeatureSlot(FeatureId)` for the M78 flight:

| Features | Slot path |
|---|---|
| Missions, Achievements | `…/QuickRail_R/JournalButton` |
| Attendance | `…/QuickRail_L/AttendanceButton` |
| Events | `…/QuickRail_L/EventButton` |
| Formation, Synergy | `Tab_FormationPanel` |
| Expedition, Dungeon, ExpeditionBatch | `Tab_ExpeditionPanel` |
| Gacha | `Tab_GachaPanel` |
| Shop | `Tab_ShopPanel` |
| Evolution, Costumes, Codex, CharacterLevelUp | `Tab_CharacterPanel` |
| AutoBattle | `Hud/AutoButton` |
| Speed2x | `Hud/SpeedButton` |
| IdleChest, QuickIdle | `MetaHud/IdleChestWidget` |
| LampPolicy | `Strip/LampButton` |
| EchoSlot6 | `Strip/DeckPanel/Card5` |
| AutoChallenge | `Hud/StagePanel` |
| others | `Tab_Main` |

  While a sheet is open, any rail or strip target maps to `Tab_Main`: rails are faded and the strip is under the sheet.
- `OnFeature(FeatureId.Events)`: route to the event shop (the rail's action) instead of the '곧 찾아와요' toast.

### 5.7 Tutorial and coach

| FTUE step | Target | IA effect |
|---|---|---|
| 2–3 summon / drag | DeckCard0 | if a sheet is open when the step starts → `GoToMain()`; if the player reopens one, the finger moves to 사냥 |
| 4 lamp | LampButton | same |
| 5 first boss | StagePanel | visible in both states |
| 6 강화 x1 | Growth (tab → LevelUp1Button) | finger on the 강화 tab in MAIN → the sheet opens → finger on LevelUp1 (the coach mark follows the moving target every frame) |
| 7 짝짝이 Lv.5 | LineageList | inside the Growth sheet |
| 8 AUTO (hint) | AutoButton | visible in both states |
| 9 바구니 | IdleChestWidget | visible; SheetFade ignores the dim while the tutorial is active |
| 10 첫 만남 | TabGacha → card button | the card fits ContentArea while the Gacha sheet is open |
| 11–12 편성 / 시너지 | TabFormation → FormationPanel | sheet |
| 13–14 원정 | TabExpedition | sheet |
| 15 빠른 수선 | IdleChestWidget → popup | visible |
| 16 졸업 | – | MAIN |

Code changes (WP-IA Track A):
- `CoachTargetResolver.Resolve`: as a final step, if the resolved target `UIFrame.IsUnderSheet` → return `UIFind.Path(_canvas, CoachTargets.TabMain)` and set `RedirectedToMain = true` (cleared at the start of each Resolve).
- `FtueDirector`:
  - (a) On step start, after the first resolve: if `RedirectedToMain` → `UINav.GoToMain()` (once per step).
  - (c) Subscribe to `UIFrame.SheetStateChanged` and set `_contextDirty = true`, so the finger moves in the same frame as the sheet's target state (CR-02). Today it re-resolves only on a tab change, a popup change or its 1 s timer.
  - (d) Graduation (step 16, BetweenStages): `UINav.GoToMain()` before the card is presented, so the player graduates onto the big map. The table's 'MAIN' for step 16 had no mechanism (CR-27).
  - (b) First-meeting card: `want = pending && !popupOpen && tab == (int)MainTab.Gacha`. For a target-less first-meeting step, call `UINav.GoToTab(Gacha)` instead of covering an inactive ContentArea.
- `FirstMeetingCardView`: call `Fit()` in `Update` while shown, only when `_coverArea.position` changed, so the card tracks the sheet during its 0.30 s slide.
- `TutorialSceneBuilder` flyer targets:
  - `_bagTarget` → `TopBar/MenuButton`.
  - `_eventTarget` → `MetaHud/QuickRail_L/EventButton`; new `_eventFallbackTarget` → `Tab_ShopPanel` (CR-03).
  - `RewardFlyer.ResolveTarget` uses the fallback when the event target is inactive or `UIFrame.IsFeedbackHidden` (faded by SheetFade, or under the sheet).
  - Today's code only checks `activeInHierarchy`, so a faded rail icon would still receive the flight.

### 5.8 Coach paths and frozen names (amends UX-SPEC §0.2)
`CoachTargets` (MetaTypes.cs):
- Add `TabMain = "SafeArea/TabBar/Tab_Main"`.
- Add `MenuButton = TopBar + "/MenuButton"`.
- Add `EventButton = Battle + "/MetaHud/QuickRail_L/EventButton"`.
- Change `JournalButton = Battle + "/MetaHud/QuickRail_R/JournalButton"`.
- Change `AttendanceButton = Battle + "/MetaHud/QuickRail_L/AttendanceButton"`.
- Remove `BagButton` and `SettingsButton`. Nothing targets them, and `ValidateTargetsInScene` would fail on the missing paths.
- Keep `MailButton` and `TopBar`.

Frozen after WP-IA (do not rename or reparent):
- `SafeArea/StageArea/Battle/{Hud/StagePanel, Hud/AutoButton, Hud/SpeedButton, Strip/DeckPanel/Card{n}, Strip/LampButton, MetaHud/GuideQuestBar, MetaHud/IdleChestWidget, MetaHud/QuickRail_L/{EventButton, AttendanceButton}, MetaHud/QuickRail_R/JournalButton}`
- `SafeArea/TabBar/{Tab_GrowthPanel, Tab_FormationPanel, Tab_CharacterPanel, Tab_Main, Tab_ExpeditionPanel, Tab_GachaPanel, Tab_ShopPanel}`
- `SafeArea/ContentArea/*Panel`. ContentArea stays a direct child of SafeArea; `SheetHeader` is a sibling of the panels.
- `SafeArea/ContentArea/SheetHeader/{DragArea, HandleHit, ReturnChip}` (AutoShot drives `DragArea`, CR-04).
- `SafeArea/TopBar/{MailButton, MenuButton}` and a TopBar child whose name contains "Power" (`PowerChip`).
- `Battle/Field` (MetaSB:354) and `Battle/SynergyBannerArea/SynergyBanner` (MetaSB:357). Not reparented (§6.1).
- `Popups/MenuPopup/Window/Body/Grid/*Tile`, plus the UX-SPEC popup paths.

Names (amends §0.2.2):
- TSB looks up `MenuButton` instead of `BagButton`.
- `EventChip` no longer exists.
- `ClaimAllButton`, `LevelUp1Button`, SettingsPopup `Tab0..Tab3`, `CheckInButton` and `Popups/EvolutionCraftPopup` are unchanged.

### 5.9 `RedDotAnyView` and `SheetFade` (new)
- `RedDotAnyView` (`Scripts/UI/Meta/`): `[SerializeField] string[] _paths; QuickRailView[] _rails; Badge _badge; bool _onlyWhileSheetOpen`.
  - Paths = `_paths` (v1: `hud.guidequest`) ∪ the dot paths of the icons the rails currently place, rails and tray (`QuickRailView.Changed`, CR-07).
  - On RedDot changes, rail changes and `UIFrame.SheetStateChanged`, it calls `SetDot(anyClaim && (!_onlyWhileSheetOpen || UIFrame.SheetOpen))`, where `anyClaim` = `Game.RedDots.IsClaimOn(p)` for any path.
  - Event-driven, no per-frame work. It is used on `Tab_Main`.
- `RedDotSystem.IsClaimOn(path)` (new, A0, CR-06/07):
  - Like `IsOn`, but it skips FeatureDot leaves (`*.feature.*`, flagged once at leaf creation, no allocation).
  - The 더보기 count uses it too.
  - WP-7 can reuse it for the M09 NEW-vs-dot choice: UX-SPEC §3.13 item 3 needs exactly this and has no API today.
- `SheetFade` (`Scripts/UI/Common/`): `[SerializeField] CanvasGroup _group; float _sheetAlpha; bool _ignoreDuringTutorial`.
  - On `UIFrame.SheetProgressChanged`: `a = Lerp(1, _sheetAlpha, p)`, or 1 when ignored. The group's alpha, `blocksRaycasts` and `interactable` are written only on change; raycasts are off below α 0.05.
  - It re-applies `OnEnable`.
  - Its group is dedicated: never shared with a view's own CanvasGroup.

---

## 6. Battle frame [IA-03, IA-04]

### 6.1 Hierarchy (BattleSceneBuilder)
```
Battle (Canvas, BattleView, + BattleFrameDriver [new])
├ Background (RectMask2D, + Canvas [new]) / FieldArt     ← mapper._backdrop (pan applied)
├ EdgeShade
├ Field (Canvas, GraphicRaycaster, RectMask2D, BattleFieldMapper)  Stretch(0,0,0,156)
│  └ Camera [new] Stretch()                                ← mapper._view (anchoredPosition.y = pan)
│     └ ShakeRoot (unchanged; the screen shake writes its position/rotation, BattleFx.cs:965-970)
│        └ GroundPlane, Ambient, Pads, GroundFx, ShopBackLayer, Actors, Ambience, Fx, Edge, FieldFlash, Numbers, Callouts
├ DangerVignette, Hud, BannerArea, SynergyBannerArea, NoticeArea   (not reparented; bottom inset driven)
├ Strip, AimOverlay, FxGallery …                           (unchanged)
└ MetaHud (MetaSceneBuilder; + QuickRail_L/R)
```
- No code looks up `ShakeRoot` by path. Inserting `Camera` is safe: the only `Field` lookups are MetaSB:354 and the Formation mini-field (a different object).
- Banner, notice and vignette areas keep their names and draw order. `BattleFrameDriver` writes their `offsetMin.y`.

### 6.2 `BattleFieldMapper` additions
- `[SerializeField] RectTransform _view` (the Camera node).
- `public float Pan { get; }` and `public void SetView(float pan)`: writes `_view.anchoredPosition = (0, pan)` and `_backdrop.anchoredPosition = (0, pan)`. **No `Version` bump.**
- `FitToBackdrop` writes `(0, Pan)` instead of `Vector2.zero` for the backdrop, so a re-layout during SHEET keeps the art aligned. It also applies the cover cap (§1.4).
- `VisibleTop` and `VisibleBottom`: the visible band in camera-local y (centre origin, y up). With field height `h` and sheet progress `p`:
  - `VisibleTop = h/2 − Pan`;
  - `VisibleBottom = −h/2 + max(0, 836·p − 156) − Pan` (the strip top in MAIN, the sheet top in SHEET).
- `ViewVersion`: increments when the pan target changes and when a move settles.
- `ScreenToWorld` goes through `_view` (keeps drag-summon aiming exact).
- `ContainsScreenPoint` stays on the Field rect (the window) and also rejects points below `VisibleBottom` (CR-26). `_view` is the field shifted up by the pan, so in SHEET it reaches 360 px under the sheet top.
- `public Vector3 FieldToCanvasWorld(Vector2 fieldLocal)`: for overlays outside the camera. Take `v = _view != null ? _view : Rect`, then return `v.TransformPoint(v.rect.center + fieldLocal)`. Use an explicit Unity null check: `??` ignores Unity's destroyed-object null.
- `WorldToField`, `Origin`, `Ppu` and `Squash` are unchanged, so everything under the camera keeps field-local coordinates.

### 6.3 `BattleFrameDriver` (new, `Scripts/UI/Battle/`)
- Subscribes to `UIFrame.SheetProgressChanged`.
- On each p: `mapper.SetView(PanOpen · p)`; `_bandAreas[i].offsetMin = (0, max(156, 836·p))` for DangerVignette, BannerArea, SynergyBannerArea and NoticeArea.
- Recomputes `PanOpen` with the §1.3 rule, rounded with `round_px` (CR-09), on `mapper.Version` change and on `OnRectTransformDimensionsChange`. If a sheet is open, it then re-applies `SetView(PanOpen·p)` at once.
- Cost per animation frame: 2 mapper writes + 4 inset writes. No `Recalculate`, no `Version` bump, no allocations. It stays idle between transitions.

### 6.4 Out-of-camera consumers (one-line fixes, WP-IA Track B)
- `BattleNoticeView.cs:161`: row y through world space: `NoticeArea.InverseTransformPoint(mapper.FieldToCanvasWorld(WorldToField((0,_worldTop))))`. The area's centre now differs from the field centre.
- `GuideQuestBarView.cs:317-327` (`IsEnemyNear`): `mapper.FieldToCanvasWorld(local)` instead of `field.TransformPoint(center + local)`.
- `SummonAimView`: cancel the aim when `UIFrame.SheetProgress > 0`.
- Danger arrows (under the camera) stay within y 215–807 at this sheet height; no new clamping.

### 6.5 Effects placed by field extents (CR-01)
Everything under `Field/Camera` follows the pan. Several effects, though, place themselves by the **field's** extents (`_layer.rect`, half the 1464-px field), and in SHEET those extents reach 360 px above the top bar and 680 px under the sheet.

The current FX sources, by symbol (line numbers drift while the FX workflow is open):
- `BattleFx.FieldTop` (`_layer.rect.height·0.5`) feeds:
  - the lamp rain (`BattleFx.cs`, `float top = FieldTop`);
  - `SkillRain` (`BattleFx.Skills.cs`);
  - the two `FieldTop + 140` points in `BattleFx.Enemy.cs`;
  - `BattleView.cs` `RewardShower(0, FieldTop·2/3)`;
  - `BattleFx.Juice.SafeTop` (= FieldTop − `JuiceLayout.TopHud` 120). `SafeTop` in turn feeds `ClampToSafe` (the big-burst clamp) and `JuiceLayout.CalloutOffset` (the combo callout at `SafeTop − 70`).
- Effect in SHEET: SafeTop lands at screen y ≈ −110 and the combo callout at ≈ −40, both **invisible**. Big bursts may centre under the top HUD, and the reward shower starts under the top bar.
- `-FieldTop` is used as the floor in `ClampToSafe` (`minY`) and in `BattleFx.Skills.cs` (`start = (…, −FieldTop + 16)`). In SHEET both sit 680 px under the sheet.

**Fix (Track B step B8, after the FX merge and before the Wave 2 gate):**
- `BattleFx.FieldTop => _mapper != null ? _mapper.VisibleTop : …` (today's fallback stays).
- Add `BattleFx.FieldBottom => _mapper != null ? _mapper.VisibleBottom : −FieldTop`.
- Replace the two `−FieldTop` uses with `FieldBottom`.
- That is two properties and two expressions, and every site above follows, `BattleView.cs:142` included.
- In MAIN both properties equal today's values (VisibleTop = h/2, VisibleBottom = −h/2), so outside SHEET this changes nothing.
- These are the only `BattleFx*` lines WP-IA touches. Because B8 waits for the FX merge, it is Track B's last step.

**Still WP-5 (Wave 2):**
- `StageAmbienceView` (CR-15):
  - The overlays are **already** cover-fit (`Layout`: `max(size/overlay)·1.12`); v1's 'stretched' was wrong. The real costs on the 1464 field are a ×2.1 upsample of the 1024×768 overlay (×1.18 today) and the mote density.
  - Spawn and wrap motes inside [VisibleBottom, VisibleTop], and scale the mote count by visible area ÷ (1080·624), so rain is not 57% sparser in MAIN.
  - Put the moon at `VisibleTop − 0.2·band` instead of `size.y·0.3`, which puts it at screen y ≈ 64 in SHEET, behind the top bar.
- Fireflies (`BattleFx.UpdateFireflies`, `_layer.rect.size`): keep them, they are ambient. Optionally wrap them within the visible band.
- `SkillCalloutView` M41 floor (§8.3).
- **Enemy pop-in (CR-19):**
  - In MAIN all five spawn points are on screen; today lanes 0, 2 and 3 entered at or under an edge (61–90% path visibility). Every normal enemy now visibly pops in mid-street (`EnemyView`: 0.35 s OutBack scale, 0.2 s fade).
  - Add a small spawn puff at the feet: the boss shadow pool at 0.45 scale for 0.35 s, or `FX/puff`.
  - This lives in `EnemyView`/`BattleFx.Enemy`, which the FX workflow owns, so it is a named-lines item for WP-5 after the merge (P1).
- `FxGalleryView` (dev only) draws markers in field space outside the camera. Leave it: it is never shown in SHEET.

---

## 7. Work package WP-IA (Wave 1c) [IA-10]

### 7.0 Waves (amends UX-SPEC §4.0)
```
WP-0 → Wave 1 (WP-1, WP-2) → Wave 1b (WP-3a, WP-3b)
        │
WP-IA0 Art (DONE: field_tall.png, menu_all.png; §9.0)
        │
Wave 1c ┬─ WP-IA/A Shell: frame, sheet, nav, top bar, rails, menu, tutorial hooks   (needs Wave 1b)
        └─ WP-IA/B Battle frame: mapper, camera, driver, art hookup                 (needs A0 contracts + IA0 art;
                                                    may start before the FX merge with guarded patches)
        │
  ── gate: FX + graphics merged; WP-IA acceptance; scene rebuild OK; smoke + IA tests green ──
        │
Wave 2 (WP-4 … WP-8 with the §8 deltas) → Wave 3 (WP-9)
```
- Track A touches no FX-owned file.
- Track B touches `BattleSceneBuilder` in a few small regions:
  - the Camera node at lines 58–62;
  - the art hookup in `Build` around lines 93–103 and in `BattleArt.Load` around line 2022;
  - `BuildBackground`;
  - the driver binding at the end of `Build`;
  - `BuildHud` / `BuildSynergyHud` (SheetFade on SynergyRow, the EmptyHint rect and SheetFade).
- If the FX workflow is still open, Track B ships these edits as a re-runnable anchor-checked patch script (`scratchpad/ia/wpia_patch_battle.py`, the allyfx_patch_* pattern) and re-runs it after the FX merge. Line numbers in this addendum are indicative because the FX workflow keeps moving them; the script anchors on code text.
- Track B's last step, B8 (`BattleFx.FieldTop/FieldBottom`, §6.5), runs only after the FX merge. The Wave 2 gate already requires that merge, so B8 never delays Wave 2.

### 7.1 Files (one owner per file) and Wave 2 handover

| File | Track | Wave 2 owner |
|---|---|---|
| new `Scripts/UI/UIFrame.cs` | A0 | frozen (shell) |
| new `Scripts/UI/Common/SheetFade.cs` | A0 | frozen |
| `Scripts/UI/UINav.cs`, `Scripts/UI/UIManager.cs` | A | frozen |
| `Scripts/UI/UITokens.cs` (IA constants only, §7.3) | A0 | frozen |
| `Scripts/Core/GameSettings.cs` (`SheetAutoReturn`) | A0 | frozen (Phase 4 P4-0) |
| `Scripts/Systems/Meta/RedDotSystem.cs` (`IsClaimOn` only, CR-06) | A0 | frozen |
| `Scripts/UI/Common/RewardFlyer.cs` (`ResolveTarget` hidden-target rule + `_eventFallbackTarget` only, CR-03) | A | frozen |
| `Scripts/UI/Main/MainTabController.cs`, new `SheetController.cs`, new `SheetDragHandle.cs` | A | frozen |
| new `Scripts/UI/Main/MenuPopup.cs` | A | WP-4 |
| `Scripts/UI/Main/TopBarView.cs` | A | WP-4 |
| `Editor/IdleSceneBuilder.cs` (frame, ContentArea canvas, 7-slot tab bar, **`BuildSheetHeader`**, CR-10), `Editor/SceneBuildContext.cs` | A | frozen |
| new `Editor/Builders/FrameSceneBuilder.cs` (`BuildTopBar`, `BuildMenuPopup`; the sheet header is shell and stays in IdleSceneBuilder, CR-10) | A | WP-4 |
| `Editor/Builders/MenuSceneBuilder.cs` (delete `BuildTopBar`) | A | WP-4 |
| `Editor/Builders/MetaSceneBuilder.cs` (rails, Tab_Main dot, SheetFade on chest/pill, top-bar meta removal, sleep restyle) | A | WP-7 |
| new `Scripts/UI/Meta/{QuickRailView, QuickIconView, RedDotAnyView}.cs` | A | WP-7 |
| `Scripts/UI/Meta/{TopBarMetaView, MetaNavigator, SleepModeView}.cs` | A | WP-7 |
| `Scripts/Systems/Meta/MetaTypes.cs` (CoachTargets only) | A | frozen (Phase 4 P4-0) |
| `Editor/Builders/TutorialSceneBuilder.cs` (flyer targets) | A | WP-8 |
| `Scripts/UI/Tutorial/{CoachTargetResolver, FtueDirector, FirstMeetingCardView}.cs` | A | WP-8 |
| `Tests/PlayMode/MainSceneSmokeTests.cs` (one test), new `Tests/PlayMode/UiFrameTests.cs` | A | WP-9 |
| `Verify/Runtime/AutoShot.cs` (IA shots) | A | WP-9 |
| `Editor/Builders/BattleSceneBuilder.cs` (guarded patch) | B | WP-5 |
| `Scripts/UI/Battle/BattleFieldMapper.cs`, new `BattleFrameDriver.cs` | B | WP-5 |
| `Scripts/UI/Battle/{BattleNoticeView, SummonAimView}.cs` (one line each) | B | WP-5 |
| `Scripts/UI/Meta/GuideQuestBarView.cs` (one line) | B | WP-7 |
| `Scripts/UI/Battle/BattleFx.cs`, `BattleFx.Juice.cs`, `BattleFx.Skills.cs` (`FieldTop`/`FieldBottom` and the two `−FieldTop` uses only, after the FX merge, §6.5) | B8 | FX workflow; WP-5 named lines |
| new `Art/Battle/field_tall.png`, new `Art/Icons/menu_all.png` (both **delivered**, §9.0) | IA0 | graphics |

"Frozen" means no Wave 2 package edits it. Needs are reported to the orchestrator, as for SPEC4's untouched list.

### 7.2 Ordered steps
**Track A (shell)**
- **A0. Contracts first** (Track B starts when A0 compiles):
  - `UIFrame`, `SheetFade`, `UINav.GoToMain/MainRequested`.
  - The §7.3 constants in `UITokens`.
  - `GameSettings.SheetAutoReturn`.
  - `RedDotSystem.IsClaimOn` (§5.9).
- **A1. Frame:**
  - `SceneBuildContext`: `TopBarHeight = UIFrame.TopBarHeight` (130), plus `SheetHeaderHeight` and `SheetHeight`.
  - `IdleSceneBuilder`: StageArea `Stretch(0,0,130,170)`.
  - ContentArea `Bottom(170,780)` + Canvas + GraphicRaycaster + CanvasGroup; starts inactive.
  - Sibling order unchanged.
- **A2. Top bar:** `FrameSceneBuilder.BuildTopBar` replaces `MenuSceneBuilder.BuildTopBar` (§3.1, M68 bleed included); `TopBarView` field changes.
- **A3. Sheet:**
  - `SheetController` + `SheetDragHandle` + `IdleSceneBuilder.BuildSheetHeader` (§4.2).
  - Re-parent WP-3a's `ReturnChip` into the header left slot (300×48).
  - Add the first-frame clamp (§1.5) and `SheetDragHandle.Cancel` (§4.7). The header builder sits in IdleSceneBuilder, not FrameSceneBuilder (CR-10).
- **A4. Tab bar and controller:**
  - `BuildTabBar` → 7 slots + `Tab_Main` (§2.2). Keep WP-3a's Indicator, BottomBleed and growth glow; `ctx.TabMain`.
  - `MainTabController` (§5.3).
  - `UIManager.SheetBackHandler` + the §4.7 order.
- **A5. Menu:** `MenuPopup` + `FrameSceneBuilder.BuildMenuPopup` (§3.6) → `ctx.Popups`.
- **A6. Meta:**
  - `MetaSceneBuilder.BuildTopBar` stops building journal/attendance, binds `PowerChip/PowerText` and no longer binds `_legacyGoldPerSec`. `TopBarMetaView.WriteStats` writes the number only (CR-14).
  - New `BuildQuickRails` (§3.3).
  - `BuildTabGate` adds the `RedDotAnyView` on `Tab_Main`.
  - `SheetFade` on `IdleChestWidget` (0.5, ignore during the tutorial) and `RecommendedPower` (0).
  - Sleep restyle (§4.9; keep the M01 handlers, add M15 and M68) and `SleepModeView.SleepNow()`.
  - `QuickIconView` marks its feature seen on tap. `MetaNavigator.OnFeature(Events)` routes to the event shop and marks it seen (CR-06).
- **A7. Navigation and tutorial:**
  - `MetaNavigator` (§5.6), `CoachTargets` (§5.8).
  - `CoachTargetResolver`, `FtueDirector`, `FirstMeetingCardView` (§5.7).
  - `TutorialSceneBuilder` flyer targets (§5.7), including `_eventFallbackTarget`, and the `RewardFlyer.ResolveTarget` rule (CR-03).
  - `FtueDirector` (c) and (d), plus the resolver's `RedirectedToMain` (§5.7).
- **A8. Tests and tools:**
  - `MainSceneSmokeTests.GrowthPanel_LevelUpButton_RaisesLevel`: call `Select(MainTab.Growth)` and wait 0.35 s realtime first. Panels are inactive in MAIN, so `FindAnyObjectByType<GrowthPanel>()` would return null.
  - New `UiFrameTests` (§7.5).
  - AutoShot IA shots (§7.5).
  - AutoShot and the PlayMode fixtures turn auto-return off (§4.8).
  - The existing `battle_later` capture calls `UINav.GoToMain(true)` first. Today it runs with the Formation sheet open (CR-04).
- **A9. Verify:** compile check (dotnet) → verify_proj `BatchBuildAll` → `MetaDataBuilder.ValidateTargetsInScene` = 0 missing → PlayMode tests → AutoShot.

**Track B (battle frame)**
- **B1. Art hookup:**
  - `BattleArt.Load` prefers `Battle/field_tall`.
  - Explicit ring constants (§1.4). Accept 1080×2048 only; otherwise log an error and fall back to `field.png` (CR-23).
  - `BuildBackground` initial size from the 1620 StageArea; add a Canvas to `Background`.
- **B2. Mapper:** §6.2, including the cover cap.
- **B3. Camera node:** `Field/Camera` between `Field` and `ShakeRoot`; bind `_view`.
- **B4. Driver:** `BattleFrameDriver` on `Battle` (§6.3); bind `_bandAreas`. `PanOpen` is rounded with `round_px` (CR-09).
- **B5. Battle HUD:**
  - `SheetFade 0` on `Hud/SynergyRow` and `Hud/EmptyHint`.
  - EmptyHint to At(BottomCenter,(0,296),(560,64)).
- **B6. One-liners:** §6.4.
- **B7. Verify:** compile; with A merged, verify_proj build + AutoShot battle checks.
- **B8. FX extents (after the FX merge):** §6.5 `FieldTop`/`FieldBottom`, then re-run AutoShot `tab_*` with the IA-11 check (§7.5).

### 7.3 New tokens (`UITokens.cs`; frame heights live in `UIFrame`)
- `UISize`:
  - Top bar: `TopIconButton 96`, `TopIcon 64`.
  - Tab bar: `TabSideWidth 148`, `TabCentreWidth 192`, `MainMedallion 150`, `MainGlow 200`, `MainIcon 112`, `MainLabelPlateWidth 112`, `MainLabelPlateHeight 36`.
  - Rails: `RailCellWidth 104`, `RailCellHeight 116`, `RailPlate 88`, `RailIcon 62`, `RailLabelWidth 100`, `RailLabelHeight 28`, `RailEdge 12`, `RailTopOffset 306`, `RailBottomOffset 288`, `RailPitch 124`.
  - Sheet header: `SheetHandleWidth 120`, `SheetHandleHeight 40`, `SheetHandleHitWidth 200`, `SheetHandleHitHeight 80`, `SheetHandleHitRise 24`, `HeaderChipWidth 300`, `HeaderChipHeight 48`.
  - Menu popup: `MenuTileWidth 240`, `MenuTileHeight 196`, `MenuTileGap 24`.
- `UIMotion`: `SheetOpen 0.30`, `SheetClose 0.24`, `SheetSettleMin 0.10`, `SheetDismissFraction 0.25`, `SheetDismissVelocity 1200`, `SheetReducedIn 0.10`, `SheetReducedOut 0.08`, `MainGlowFade 0.12`, `SheetAutoReturnSeconds 60`, `SheetFirstFrameMaxStep 1/30` (2 frames), `SheetIdleMaxStep 0.1`.

### 7.4 Risks

| Risk | Mitigation |
|---|---|
| FX workflow edits `BattleSceneBuilder` concurrently | Track B as an anchor-checked patch script, re-run after the FX merge; FX call sites go to WP-5 after the merge (§6.5) |
| Panels are inactive at boot (MAIN). Anything that relied on GrowthPanel being the active default tab breaks: the smoke test (fixed in A8), the M35d glow's `GrowthPanel.Cost10` | `Cost10` becomes a static computed property from `Game.Growth` (WP-4 delta, §8.2). Grep `FindAnyObjectByType<` and `FindObjectOfType<` for panel types |
| Coach path moves (journal, attendance) and removed constants | `ValidateTargetsInScene` must report 0 missing; the frozen list is updated (§5.8) |
| ContentArea becomes a nested Canvas (sorting, raycasts) | No override sorting; TopBar and TabBar are later siblings and draw above; popups are in the separate `Popups` root |
| Outpaint puts rooftops in the top-lane strip (enemies and boss plates are drawn there) | **Closed** (§9.0): the strip is ~60% avenue (one lamp at x≈575) and ~40% garden and house front continuing the original curb. Top-lane enemies stand on that curb, in front of the houses. Judged a pass |
| Statics (`UIFrame`, `UINav`) survive scene reloads in PlayMode tests | `Bind`/`Unbind` and a SubsystemRegistration reset (CR-20); UiFrameTests (i) |
| FX placed by field extents vanish or misplace in SHEET | B8 property change (§6.5); IA-11 capture check |
| Enemies visibly pop in at the on-screen spawn points in MAIN | WP-5 spawn puff after the FX merge (§6.5, CR-19) |
| Tall phones / 4:3 tablets | Pan rule and cover cap at runtime; the rails' top pocket collapses below a 1500 px StageArea |
| WP-3a in flight implements M52 re-tap scroll-to-top and the StageArea ReturnChip | §8.1 tells WP-3a what to skip; WP-IA adapts the rest |
| Back-order regressions (return chip vs sheet vs exit) | UiFrameTests back-order case + manual device check |
| Event-token flights to a hidden rail icon | Fallback target `Tab_ShopPanel` (§4.10) |
| AutoShot numbering shifts (01–29 renumber when IA shots are inserted) | Shots are compared by name, not number; WP-9 re-baselines |
| Auto-return surprises a reader | 60 s, held during popups, tutorial, coach and drags; setting to turn it off |

### 7.5 Acceptance and captures
Done when:
1. Both tracks compile with 0 errors (dotnet check).
2. The verify_proj BatchBuildAll succeeds and `ValidateTargetsInScene` reports 0 missing.
3. Smoke tests and the new `UiFrameTests` are green.
4. Every `[Verify] IA-*` check passes.
5. The §5.4 manual checks pass (device back order, swipe feel, reduced motion, one 19.5:9 device, a full FTUE run with sheets opened at random points: the finger never points under the sheet).

`UiFrameTests` (PlayMode, new):
- (a) boot → `SelectedIndex == -1`, ContentArea inactive;
- (b) `Select(Growth)` → after 0.35 s, `UIFrame.SheetProgress == 1` and `mapper.Pan ≈ PanOpen`;
- (c) a simulated user re-tap closes and fires `Reselected` once;
- (d) `UINav.GoToTab(Growth)` twice never closes;
- (e) back order popup → sheet → exit confirm;
- (f) SheetFade alphas at p = 1 (pill 0, synergy 0, rails 0, chest 0.5);
- (g) resolver: with a sheet open and DeckCard0 as the target, the result is `Tab_Main`;
- (h) the auto-return timer closes after the configured time (set the seconds via a test hook);
- (i) after a scene reload, `UIFrame.SheetOpen == false`, `SheetTab == -1` and `SheetProgress == 0` (CR-20);
- (j) return restoration: MAIN → `UINav.OpenJournal()` → `PushReturn` + `GoToTab(Growth)` → `TryPopReturn()` → MAIN with the journal open (CR-05);
- (k) `IsUnderSheet(DeckCard0)` is true in the same frame as `Select(Growth)`, and `IsUnderSheet(Tab_GachaPanel)` stays false while a sheet is open (CR-02);
- (l) with the tutorial skipped, (f)'s chest alpha is 0.5 (the tutorial forces 1).

Every fixture sets `GameSettings.SheetAutoReturn = false` except (h) (CR-04).

AutoShot IA captures (inserted into the existing sequence; `[Verify] PASS|FAIL IA-x` lines):

| Shot | Setup | Checks |
|---|---|---|
| ftue_coach_sheet | right after shot 03 (FTUE step 2): `tabs.Select(Growth)`, wait 0.6 s; then `GoToMain(true)` | the coach target is `Tab_Main` and the finger is visible |
| main | after skip + unlock-all + `CloseAllImmediate` + `GoToMain(true)`, wait 4 s | IA-1 `SelectedIndex −1`, ContentArea inactive, Tab_Main glow on. IA-2 StageArea 130–1750 ±1, Field 130–1594. IA-3 Ppu 205 ±5, Squash 0.7256 ±0.01, shop y 925.5 ±8 (16:9), backdrop sprite `field_tall`, backdrop scale 1.000. IA-4 art ring radius / (1.6·Ppu) = 1 ±0.02. IA-5 no spawn point (r 2.5, 5 lanes) inside any TopBar, Hud, MetaHud or rail rect. IA-6 top-bar rects disjoint (§3.1). IA-7 rails at §3.3 rects, labels present |
| sheet_opening | `tabs.Select(Growth)`, capture at 0.12 s realtime | 0.2 < p < 0.9; pan = PanOpen·p ±2 |
| tab_{Tab} ×6 (existing loop, switching while open) | 1.2 s each | IA-8 ContentArea 970–1750 ±1, header 914–970, pan 360 ±1 and a whole number of device pixels (16:9, CR-09). IA-9 SheetFade alphas (§1.6). IA-10 the indicator is centred on the tab. IA-11 (after B8) `BattleFx.FieldTop` = `mapper.VisibleTop` ±1, and `SafeTop` maps to screen y ≥ 250 (CR-01) |
| sheet_closing | user-tap `Tab_ShopPanel` (onClick) while Shop is open, capture at 0.10 s | p decreasing; `Reselected` count 1 |
| main_after_close | +0.4 s | p 0, ContentArea inactive, pan 0 |
| back_order | Growth open → SettingsPopup → `UIManager.Instance.OnBack()` ×3 (public since WP-3a), a few frames apart | 1: popup closed, sheet open. 2: sheet closed. 3: exit confirm open (then `CloseAllImmediate`) |
| sheet_drag | Growth open; `ExecuteEvents` begin/drag/end on `SheetHeader/DragArea`, 300 px down in 0.2 s | closed |
| menu | `MenuPopup.Open()` | 6 tiles (everything unlocked), 3×2 grid, window 900×740 |
| sleep | Growth open, `SleepModeView.SleepNow()` | the sheet closes first; the card is in the bottom band; mid-screen luminance 40–65% of the `main` shot (map visible at dim 0.5, CR-16) |
| battle_later (existing, last) | `UINav.GoToMain(true)`, then the existing 20 s wait | MAIN: ContentArea inactive, pan 0 (CR-04) |
| tall (optional second pass at 540×1170) | main + Growth | cover ≤ 1.0, ring ratio 1 ±0.02, pan per rule ±4 |

---

## 8. Deltas for the other packages

### 8.1 WP-3a / WP-3b (Wave 1b, in flight)
Status at the critic pass: `MainTabController` already implements both of these (`ScrollPanelToStart`, and `FadeInPanel` on every change). WP-IA's A4 rewrite removes them, so WP-3a need not revert anything:
- M52 re-tap scroll-to-top (re-tap closes, §4.3);
- the panel α fade on every selection (it applies only to switches while open).

Build as specified; WP-IA adapts:
- the ReturnChip (re-parented into the sheet header at 300×48, label 24, min 20);
- `UIManager.OnBack` (WP-IA inserts the return and sheet tiers);
- `BuildTabBar` Indicator, BottomBleed and growth glow (carried into the 7-slot bar);
- `ClearReturn` on a different-tab tap (WP-IA adds it on 사냥 and `GoToMain`).

WP-3b `ReturnChipView` must lay out at 300×48 (not only 360×64): use Stretch children and autosize text. Toast position (`UISize.ToastTop` 480) and `TooltipLayer` are unchanged.

### 8.2 WP-4 (Menu)
- **Top bar (§2.2):** the layout is done by WP-IA (§3.1). WP-4 keeps:
  - M08: gold and gem chips tappable with an 88-tall hit clipped above y 96; the gem '+' badge; the gold sources sheet;
  - M21: counter sync, decrease tint, `GoldDisplayKey`, the '+N' pill;
  - M09: the notice dot, now on `MenuButton`;
  - the IconMenuButton style for 우편/메뉴 if wanted.
  - Delete nothing else. `FrameSceneBuilder` and `MenuPopup` are WP-4's from Wave 2.
- **Panels (§2.4–§2.9):** no layout change; the rect is identical.
  - `GrowthPanel.Cost10` must be a static computed property from `Game.Growth`, not cached in `OnEnable`: every panel is inactive in MAIN at boot.
  - The optional '강화 x1을 길게…' toast still applies.
- **Settings (§2.10):**
  - New row '전투 화면으로 돌아가기' (§4.8), bound to `GameSettings.SheetAutoReturn`.
  - 잠자기 모드 desc → '손대지 않으면 전투만 남기고 어둡게 해요'.
  - Rows stay one line at 20 px in the 380×40 box.
- Bag, Notice and Settings popups now open from the ≡ menu tiles. Nothing changes inside them.
- M68 TopBleed: done by WP-IA.

### 8.3 WP-5 (Battle HUD)
- **HUD map (§2.1):** local positions unchanged; use the §3.2/§3.4 screen numbers in checks.
- **M13 BossChallengeButton:** add `SheetFade 0.5` on its root.
- **M43:** keep the `SheetFade 0` on `SynergyRow` when rebuilding the chip.
- **M67 ribbon:** `RibbonHeight()` returns `area.rect.height*0.5f − 293f`. The ribbon centre sits 293 px below the band top: HUD bottom 218 + 24 gap + 51 half height. Screen 372–474 in both states, clear of the synergy row and the boss button (x-disjoint). Keep the drop/fade while a popup is open.
- **M41 bubbles:** `_floorY` = `mapper.VisibleBottom` (Callouts space) + 24, so in SHEET bubbles stay above the sheet. The guide zone rule applies in MAIN only.
- **EmptyHint:** the WP-IA position is kept (§3.4).
- **Aim hint (optional):** move to local y 230–282 (screen 360–412) so it clears the M12 pill during a drag.
- **FX extents:** done by WP-IA B8 (§6.5); WP-5 keeps them. The only other `BattleFx*`/`EnemyView` lines WP-5 may touch are the spawn puff (§6.5, CR-19).
- `StageAmbienceView`: moon anchor, mote band and density (§6.5, CR-15).
- Lock covers and coach targets: unchanged.

### 8.4 WP-6 (Character & gacha)
- No structural change: 친구들 and 소환 panels keep their rects.
- `CharacterDetailPopup.GoToGacha` → `GoToTab(Gacha)` opens the Gacha sheet; the pushed return slot appears in the sheet header.
- `GachaResult` `FormationButton` → `GoToTab(Formation)` opens the sheet.
- `OpenCharacter` keeps opening the detail popup over any state, MAIN included.

### 8.5 WP-7 (Meta)
- **M10:** done by WP-IA (no journal/attendance in the top bar; `PowerChip` binding; `WriteStats` already writes the number only, CR-14). Do not rebuild them there.
- **M12 RecommendedPower rebuild:** keep `SheetFade 0` on the root; the local rect is unchanged.
- **M04 guide bar:** unchanged local rect; the `IsEnemyNear` fix is done by WP-IA (Track B).
- **M32 basket:** keep `SheetFade 0.5` (ignored during the tutorial).
- **M09:** the ≤4-badge acceptance on shot 05 is re-baselined to **≤5 in MAIN** (rails add one cluster). `Tab_Main` aggregate (WP-IA) counts only in SHEET. The selected-tab rule is unchanged.
- **M78:** use `MetaNavigator.FeatureSlot` (§5.6). Add the rail hold: the icon is placed at α0 and appears on arrival (`BadgeAppear`) or on skip (§3.3, CR-24).
- **M09:** use `RedDotSystem.IsClaimOn` for the NEW-vs-dot choice (§5.9).
- **Sleep:** layout done by WP-IA (§4.9). WP-7 keeps the M15 handler and the M68 bleed as already included.
- `QuickRailView`, `QuickIconView` and `RedDotAnyView` are WP-7's from Wave 2. Style tweaks are allowed; slot rects are frozen (§3.3).

### 8.6 WP-8 (Tutorial)
- **M42 free bubble:**
  - SHEET: anchor to ContentArea (unchanged; contentTop − 28).
  - MAIN (ContentArea inactive): bubble bottom at screen y 1468 (StageArea bottom − 282), centred, width 720, above the guide bar.
  - Replace the `_topBubbleOffset` fallback.
- The resolver, director, first-meeting and flyer changes are done by WP-IA (§5.7). Keep them when touching these files.
- FTUE copy and step indices are unchanged. `FtueStepGuideVisible` = 6 and `FtueStepFormation` = 11.

### 8.7 WP-9 (Verify)
- Own the WP-IA shots and `UiFrameTests` (§7.5).
- Re-base the UX-SPEC §5.2 checks:
  - 03: the bubble is in the MAIN bottom band (§8.6).
  - 05 → `main`: guide bar 16–484 × 1492–1576; top-bar rects per §3.1; mapper as IA-3.
  - 11c: ribbon at 372–474.
  - 12–17: add IA-8…IA-10.
  - 29 (`battle_later`): MAIN, set up with `GoToMain(true)` (A8 adds the call; CR-04).
  - Auto-return stays off in every harness run except UiFrameTests (h).
- `UiSceneLintTests`: every `CoachTargets` path, including `TabMain`, `MenuButton` and `EventButton`, resolves in Main.unity.

### 8.8 SPEC4 (phase 4, after Wave 2)
- **CR-03 `StatsButton`:** the R1 rail slot (964–1068 × 436–552) with the rail cell anatomy, not StageArea TopRight(−24,−302). Hidden in SHEET (rail rule). The SPEC4 conflict #9 rect is superseded.
- **EVT-01:** the event entry is the `EventButton` rail icon; the EventChip no longer exists, so SPEC4 conflict #2 is superseded. The 'D-n' pill is the rail `SubPill`, and `top.event` sits on the rail icon.
- **EventHub, SeasonPass, Codex, Achievements, Coupon:** row 3 of the ≡ menu (and the hub). The ≡ `MenuButton` dot aggregates their menu-only leaves.
- **P4-4 `TopBarView` work:** targets the compact bar.
- **IDLE-06 sleep signals:** compatible with §4.9.
- **GameSettings:** `SheetAutoReturn` already exists; P4-0's "no other keys" note should list it.

---

## 9. Art (Higgsfield budget for this addendum: 7 credits)

### 9.0 Outcome (WP-IA0 delivered; replaces the plan below where they differ)
- **`Art/Battle/field_tall.png`:**
  - 1080×2048 RGB. `field.png` sits at (0,619), rows 619–1428, pixel-identical (re-checked in the critic pass).
  - The mapper constants are the 2048 values of §1.4.
  - No `.meta` was committed; on import the `Battle/field` prefix rule applies (no mips, max 2048, CompressedHQ).
- **`Art/Icons/menu_all.png`:**
  - 256×256 RGBA ≡ icon: a lavender quilted patch with three cream stitched bars, a gold needle and pink thread. Framed like the other icons, with interior alpha normalised.
  - It reads as ≡ at 64, 48 and 32 px next to `menu_mail` (`scratchpad/ia/icons_preview.png`).
  - No longer optional: §3.1 uses it.
- **Outpaint:** attempt 2 was used. Attempt 1 failed QA 2 and was only tall enough for the 1920 variant.
  - The service ignores width and height. It returned 1536×2752 with a non-uniform stretch (×1.3794 across, ×1.4237 down), so `field_tall_post.py`, which assumes a uniform scale, was not used. The source was matched at 66 points instead (mean error < 1 px).
  - One 9:16 outpaint adds only ~553–570 rows each way, so rows 0–49 are a sky continuation and rows 1982–2047 a blurred, darkened fill.
  - Neither fill shows on 16:9, 19.5:9 or 20:9 (about one sky row at the top on 20:9). On 21:9 the bottom fill sits under the deck strip.
- **Seams:** the spec metric is 3.58 at the top and 7.66 at the bottom (both < 8).
  - The bottom value tracks the scene's brightness change: the service's own image scores 7.54 at the same row.
  - One window at ≈(142, 619) has slightly dimmer upper panes, visible only at 4×.
- **QA 2 (top-lane strip): judged a pass.**
  - The right ~60% is the avenue, with one lamp post at x≈575. The left ~40% is a garden and house front continuing the original curb.
  - Top-lane enemies spawn on that curb (art row ≈638 in MAIN), so their sprites read as standing in front of the houses.
  - The §9.2 cobblestone patch was not applied: it would slice through the house.
- **QA 3–5:** pass.
- **The 1920 variant is not shipped.** §1.4 and B1 accept 1080×2048 only.
- **Credits:** 5.5 of the 7-credit cap.
- **Critic check of the delivered art** (`scratchpad/ia/critic/main_band_half.png`, `top_300_800.png`):
  - The MAIN band reads as intended: plaza centred, avenue to the north, a street with two lamps to the south.
  - Nothing new competes with the ring.
  - The rails sit over houses and flowerbeds, not over cobblestone the lanes use.

### 9.1 Budget (plan, kept for the record)

| Item | Tool | Credits | Priority |
|---|---|---|---|
| `field_tall` outpaint | `outpaint_image` 9:16, 1152×2048 | 2 | 1 |
| One outpaint retry, only if QA fails | same | 2 | 1 |
| `Icons/menu_all.png` (≡ icon) | `generate_image` gpt_image_2_5, quality **high**, transparent | 1.5 | 2 (fallback `menu_settings`) |
| Reserve | — | 1.5 | a third outpaint try takes precedence over the icon |

- Run `get_cost` before each submit; outpaint needs an uploaded `image_id` first.
- At the house standard (quality max, 5 credits per image) the icon alone would break this cap. If the orchestrator raises the cap, regenerate the icon at max.
- Everything else reuses `Art/**` (§9.3): 0 credits.

### 9.2 Outpaint brief: `Art/Battle/field_tall.png`
- **Source:** `D:/UnityProject/unity_idle/Assets/_Project/Art/Battle/field.png` (1080×810, RGB, night plaza; ring centre (539.5, 390.5), radius 328×238 px).
- **Steps:**
  1. `media_upload` → PUT → `media_confirm`.
  2. `outpaint_image {image_id, aspect_ratio: "9:16", width: 1152, height: 2048, get_cost: true}`, then the same without `get_cost`. 1152×2048 is a true 9:16 pair. If the source keeps its native 1080 width, cropping 36 px per side yields exactly 1080×2048 with the original at (0, 619).
  3. Download the result to a fresh scratch folder and run `python -I scratchpad/ia/spec/field_tall_post.py <result.png> <out_dir>`. The tool:
     - locates the original (scale + offset);
     - rescales so the original is 1080 wide;
     - picks 1080×2048 (original at y 619) or, if the service scaled the source to the full width, 1080×1920 (original at y 555);
     - re-pastes the original pixel-identically and feathers 16 generated rows at each seam;
     - writes `field_tall.png`, `field_tall_report.txt` (seam metric + the exact mapper constants) and `field_tall_qa.png`.
     It was tested on both synthetic cases.
- **Content wanted** (`outpaint_image` takes no prompt; use this to judge the result and to patch):
  - **North (top) extension:** the cobblestone avenue continues straight up from the plaza's north gap between two rows of storybook houses with lavender roofs, warm lanterns, hanging flower baskets and string lights, receding in the same 3/4 top-down perspective. A moonlit indigo sky only in roughly the top 150 rows.
  - **South (bottom) extension:** the plaza edge continues toward the viewer as a short wide street or steps with pink and lavender flowerbeds, two lamp posts and a low ivy stone wall. It darkens toward the bottom edge (soft vignette) so the deck strip and tab bar sit on a calm area.
  - **Style:** the LOST & LOVELY master style (soft storybook 2D, clean cel shading, pastel purple/pink/blue/cream, restrained gold), the same lighting (warm lamplight pools) and palette, the same rendering scale. No characters, creatures, text, UI, signatures or watermarks.
- **QA (accept only if all pass):**
  1. Seams invisible at 100%; the tool's seam metric < 8 on both seams.
  2. **Top-lane strip x 324–755, rows 430–619 (2048 canvas) reads as walkable street**, not rooftops, walls or trees. Top-lane enemies and boss name plates are drawn there.
  3. Perspective and lighting continuous; no horizon tilt.
  4. Clean edge to edge: 20:9 shows rows ≈49–1843 and 21:9 nearly all.
  5. No new focal objects that compete with the plaza ring.
- **If QA 2 fails:**
  - Patch it at 0 credits: tile the source's own top-centre cobblestone rows (x 324–755, rows 619–720) upward over rows 430–619 with a 24 px feather.
  - Or retry the outpaint once (2 credits).
- **Import:** PNG RGB, 1080×2048 (multiples of 4, at the 2048 cap). `ArtImportPostprocessor` applies the `Battle/field` no-mip rule and CompressedHQ by prefix. Keep `field.png`.
- **Builder constants:** §1.4. Never run `MeasurePlazaRing` on field_tall.

### 9.3 Icon brief and reuse map
**New (optional, 1.5 credits): `Art/Icons/menu_all.png` (the ≡ 메뉴 button, 64 px on screen)**
- `generate_image`, model `gpt_image_2_5`, quality high, background transparent, 1024×1024, downscaled to 256.
- `image_references`: `Icons/menu_settings.png`, `Icons/menu_bag.png`, `Icons/journal.png` (style match).
- Prompt (EN): "A single game UI icon for a main-menu button in a whimsical 2D storybook mobile game style: a soft lavender quilted fabric tag (rounded square patch) with three thick cream stitched horizontal lines across it like a ≡ menu symbol, a tiny gold sewing needle with pink thread tucked through the top-right corner, small gold stitch dots along the border, clean medium-weight dark purple outline, soft cel shading, gentle gradient, warm dreamy highlight, pastel purple, cream and restrained gold palette, sticker-like, centered, readable at 64 px, no text, no letters, no background."
- Avoid: plain flat hamburger lines, letters or numbers, drop-shadow boxes, any background, a basket (it would read as the 수선 바구니).
- Accept: reads as ≡ at 64 px next to `menu_mail`; outline weight and palette match the `menu_*` icons; clean alpha edges.
- Fallback: `Icons/menu_settings.png` with the label '메뉴'.

**Reuse (0 credits)**

| Element | Art |
|---|---|
| 사냥 centre button | `UI/ui_medallion_dark.png` 150 + `Battle/shop.png` 112 + `UI/ui_pill_night_9s.png` plate + the existing glow sprite |
| Rail plates | `UI/ui_medallion_dark.png` 88 |
| Rail icons | `Icons/currency_event.png` (이벤트), `Icons/attendance.png` (출석부), `Icons/journal.png` (수선 일지); 더보기 = `UI/ui_chevron.png` rotated up |
| Top bar | `Icons/menu_mail.png` (우편), `ui_bar_velvet_9s` |
| Sheet header | `UI/ui_bar_velvet_9s.png` (seam on top), `UI/ui_pill_night_9s.png` handle, `UI/ui_chevron.png` rotated down |
| Menu tiles | `Icons/{journal, attendance, currency_event, menu_bag, menu_notice, menu_settings}.png` on `ui_panel_inset_9s` |
| Sleep card | `UI/moon.png`, `ui_panel_night_9s` |
| Gradients (sheet shadow, sleep veils) | generated fades in the M84 `Generated(...)` style (allowed as UI primitives) |

A later P2 upgrade, if the budget allows: a dedicated "night 수선점 medallion" icon for the centre button. The composite reads correctly in `ia_08_main_mock.png`.

---

## Appendix A: Why the rejected M66 is now safe
UX-SPEC Appendix C rejected a "watch mode" for six reasons. This addendum answers each:

| M66 objection | Answer here |
|---|---|
| A taller StageArea does not enlarge the fight (Ppu is width-clamped) | Correct, and not the goal. The gain is *coverage*: all five lanes visible from spawn (today 61–90%, worst lane hidden up to 4.0 s), the whole spawn circle, and the plaza surroundings. Units keep their size by design |
| Cover-fit blurs and crops the backdrop | field_tall 1080×2048 at scale 1.000 on 16:9; no upscale through 20:9 (§1.4) |
| The painted ring misaligns | Explicit ring uv for field_tall + the cover cap: the painted ring always equals 1.6 world units (§1.4); verified by IA-4 |
| Re-tap gets a hidden second meaning (vs M52 scroll-to-top) | In the sheet model, re-tap = close is the only meaning; scroll-to-top is dropped (§4.3) |
| A confusing persisted collapsed state | MAIN is the home state, not a mode. Launch is always MAIN; sheets are explicit and temporary (§1.2) |
| FTUE coach targets in ContentArea break | ContentArea keeps its name and rect when open. The resolver already points at the tab while a panel is inactive. Occluded targets redirect to 사냥 (§5.7) |

## Appendix B: Validation (`scratchpad/ia/spec/ia_check_out.txt`)
- **MAIN** (top bar 130, field 130–1594, shop 925.5), all HUD + all 6 rail slots + boss button: hidden lead 0.00 and path visible 100% on all five lanes.
- **SHEET** (sheet top 914, shop 566.0 at the raw pan 359.5; the shipped pan 360 puts the shop at 565.5, a 0.5 px shift that changes no number below), StagePanel + x1 + AUTO, chest dimmed: 0.00 / 100% on all lanes. Elements: ring 1.0, guardians 1.0 ×5, shop 1.0, spawn circle 0.88.
- **SHEET, chest opaque:** lane 4 hidden lead max 0.43 (1.4 s at 0.30). With the boss button too: lane 1 max 0.78 (2.6 s).
- **SHEET, pill + synergy kept:** lane 0 max 0.32, lane 1 max 0.50.
- **SHEET without the pan:** lanes 2 and 3 hidden 2.18/2.18 (100%), so the pan is mandatory.
- **Rails, boss button and every HUD rect** vs the battle keep-out (bosses + 36 px numbers): 0.0% overlap.
- **Pan by aspect** (insets 100/50, raw): 16:9 359.5 · 19.5:9 224.5 · 20:9 194.5 · 21:9 141.5. Shipped, rounded to device pixels at scale 1: 360 · 224 · 194 · 142 (CR-09).

## Appendix C: New copy (해요체)

| Where | Text |
|---|---|
| Tab_Main label | 사냥 |
| Top bar buttons | 우편 · 메뉴 |
| Gold rate | 초당 +{rate} |
| Rail labels | 이벤트 · 출석부 · 수선 일지 · 더보기 |
| Menu popup | title 메뉴; tiles 수선 일지 · 출석부 · 이벤트 · 가방 · 공지 · 설정 |
| Settings row | 전투 화면으로 돌아가기 / 1분 동안 손대지 않으면 열린 창을 내려요 |
| 잠자기 모드 desc | 손대지 않으면 전투만 남기고 어둡게 해요 |
| Sleep card | 쉿, 친구들이 수선점을 지키고 있어요 · 물리친 잔향 {n} · 모은 단추 {x} · {stage} · 화면을 누르면 깨어나요 |

## Appendix D: Files produced for this addendum (scratch)
- `scratchpad/ia/spec/ia_check.py`, `ia_check_out.txt`: validation of every number above.
- `scratchpad/ia/spec/ia_mock.py`, `ia_annotate.py` → `ia_08_main_mock.png`, `ia_08b_main_annotated.png`, `ia_09_sheet_mock.png`, `ia_09b_sheet_annotated.png`, `ia_10_mock_pair_half.png`. The backdrop in the mocks is a mirrored placeholder, not the outpaint.
- `scratchpad/ia/spec/field_tall_post.py`: outpaint post-process (tested on `_test_a` native-size and `_test_b` scaled cases).
- From the geometry pass: `scratchpad/ia/ia_01…ia_07*.png`, `geom*.py`, `keepout_out.txt`, `rule_out.txt`.
- From the critic pass: `scratchpad/ia/critic/UX-SPEC-IA.v1.md` (pre-critic backup), `apply_critic.py` (this edit), and the art crops `main_band_half.png`, `top_300_800.png`, `bottom_1400_2048_half.png`.

---

## Critic revisions (v1.1)

**Scope.** This pass reviewed the following against the code as it stood at the time of the pass:

- **Shell and navigation:** `MainTabController`, `UINav`, `UIManager` (WP-3a in flight), `TabGateView`.
- **Top bar and meta:** `TopBarView`, `TopBarMetaView`, `RewardFlyer`, `RedDotSystem`/`MetaSession`, `SleepModeView`.
- **Battle:** `BattleFieldMapper`, `BattleFx*`, `StageAmbienceView`, `DangerArrowView`, `EnemyView`.
- **Tutorial:** `CoachTargetResolver`, `CoachMarkView`, `FtueDirector`.
- **Harness:** `MainSceneSmokeTests`, `AutoShot`.
- **Builders and art:** every panel builder, and the delivered art.

Each item gives the problem, the evidence, and where the spec now says what to do. Items are ordered by severity.

### Correctness and breakage

1. **CR-01: effects placed by field extents disappear in SHEET.**
   - Evidence: `BattleFx.FieldTop` (half the 1464-px field) feeds:
     - lamp rain, `SkillRain`, the `BattleFx.Enemy` points and `RewardShower`;
     - `Juice.SafeTop`, and through it `ClampToSafe` and the combo callout at `SafeTop − 70`.
   - In SHEET the combo callout lands at screen y ≈ −40, so it is invisible, and bursts can centre under the top HUD.
   - v1 §6.5 listed 5 sites by stale line numbers and missed `SafeTop`, `ClampToSafe` and the lamp rain.
   - Fix:
     - Redefine `FieldTop` as `mapper.VisibleTop`, add `FieldBottom`, and swap the two `−FieldTop` floors. Nothing changes in MAIN.
     - This is Track B step B8, after the FX merge and before the Wave 2 gate. Acceptance check IA-11 (§6.5, §7.2, §7.5).
2. **CR-02: `IsUnderSheet` misfired.**
   - Evidence, stale target:
     - With `p > 0.5`, the resolver re-ran on the tab change while p was still 0, so it kept `DeckCard0`.
     - `FtueDirector` re-resolves only on a tab change, a popup change or a 1 s timer, so it held that stale target.
     - Result: `ftue_coach_sheet`, captured at 0.6 s, would fail.
   - Evidence, tab targets: the rule matched any target whose centre was below the header, so tab-bar targets counted too. FTUE step 10's `TabGacha` pointed at 사냥 whenever another sheet was open.
   - Fix: use the target state and the fully open sheet top, test StageArea targets only, and have `FtueDirector` re-resolve on `SheetStateChanged` (§5.1, §5.7).
3. **CR-03: event-token flights would land on an invisible rail icon.**
   - Evidence: `RewardFlyer.ResolveTarget` falls back only when the target is `!activeInHierarchy`, but `SheetFade` keeps the icon active at α0. `RewardFlyer.cs` was also missing from the IA file table.
   - Fix: add `UIFrame.IsFeedbackHidden` and `_eventFallbackTarget` = `Tab_ShopPanel`. `RewardFlyer.cs` becomes an A-owned file, for that rule only (§4.10, §5.7, §7.1).
4. **CR-04: the harness was nondeterministic and missing hooks.**
   - Evidence:
     - AutoShot leaves the Formation sheet open from the tab loop through `battle_later`, about 60 s with no popup open. The 60 s auto-return would close it at an unpredictable shot.
     - `battle_later` is re-based to MAIN, but nothing put the screen in MAIN.
     - The `sleep` capture needs a hook that `SleepModeView` lacks.
   - Fix:
     - Auto-return is off in AutoShot and in every PlayMode fixture except UiFrameTests (h).
     - `battle_later` calls `GoToMain(true)` first.
     - New `SleepModeView.SleepNow()`.
     - Freeze the `SheetHeader/{DragArea, HandleHit, ReturnChip}` paths.
     - See §4.8, §4.9, §5.8, §7.2, §7.5 and §8.7.
5. **CR-05: the return slot ignored the new frame.**
   - Evidence:
     - Journal opened from a MAIN rail → 가기 → Growth sheet → pop: the journal reopened over the Growth sheet.
     - Re-tap close and the handle went to MAIN without clearing the slot, which contradicts §1.6.
     - Back could pop a slot whose chip was hidden during FTUE or popups.
   - Fix:
     - `PushReturn` captures the frame and `TryPopReturn` restores it.
     - `ShowMain` clears the slot on every path to MAIN.
     - Back pops only while the chip is shown.
     - See §1.2, §4.7, §5.2, §5.3 and UiFrameTests (j).
6. **CR-06: the Events NEW badge never clears.**
   - Evidence:
     - `FeatureId.Events` is marked seen only by an unlock card, and that card is reserved off (`ReservedFeatureCards = false`).
     - Today no view binds `top.event`, so the problem is hidden.
     - Binding it on the rail would show a permanent NEW badge, and v1 would also show a permanent dot on 사냥 in SHEET.
   - Fix:
     - `QuickIconView` marks its feature seen on tap.
     - `MetaNavigator.OnFeature(Events)` routes to the feature and marks it seen.
     - New `RedDotSystem.IsClaimOn`.
     - See §3.3, §5.9, §7.1 and §7.2.
7. **CR-07: the 사냥 aggregate list was static.**
   - Evidence: Phase 4 rail icons (StatsButton, event hub) and overflow tray items would silently fall outside the aggregate.
   - Fix: the aggregate is the union of the rail and tray icons the rails currently place, plus `hud.guidequest`, counting claimables only (§2.3, §5.9).
8. **CR-08: the growth glow could never show in MAIN.**
   - Evidence: WP-3a's `RefreshGrowthGlow` requires `SelectedIndex >= 0`, and MAIN is −1.
   - Fix: drop that term in the A4 rewrite. A null `GrowthCost10` keeps the glow off until WP-4 publishes it (§5.3).
9. **CR-09: the SHEET backdrop sat on a half texel.**
   - Evidence: `Origin.y` is −63.5, so `PanOpen` ends in .5 on every aspect (359.5, 224.5, 194.5, 141.5). On 1080-wide phones the art and the field then rest at half-pixel offsets.
   - Fix: round to device pixels with `round_px`, giving 360 at 16:9. Visibility numbers are unchanged (§1.3, §6.3, Appendix B).
10. **CR-10: shell code was in a Wave 2-owned file.**
    - Evidence: `BuildSheetHeader` is shell code (`SheetController` binds its children), but it sat in `FrameSceneBuilder`, which WP-4 owns.
    - Fix: move it to `IdleSceneBuilder`, which is frozen (§7.1, §7.2).
11. **CR-11: the overflow tray could break frozen coach paths.**
    - Evidence: moving rail icons into a tray reparents `QuickRail_L/EventButton` and its neighbours.
    - Fix: tray rows are proxy cells. The frozen icons stay where they are, inactive (§3.3).
12. **CR-12: the 사냥 glow covered the deck.**
    - Evidence: at 240 px, centred at bar-local y 77, the glow rose 43 px over Card1–Card3, with no raycast rule.
    - Fix: make it 200 px, and turn raycast off on every child except `Hit` (§2.2, §7.3).
13. **CR-13: a first-frame hitch skipped part of the slide.**
    - Evidence: activating ContentArea runs the panel's OnEnable and a full canvas build. Heavy panels can take one long frame, so the OutCubic slide would skip about a third of its travel.
    - Fix: cap the clock step at 1/30 s for the first 2 frames (§1.5, §7.3).
14. **CR-14: the top-bar power text would overflow from Wave 1c.**
    - Evidence:
      - `TopBarMetaView.WriteStats` writes '전투력 {p} · 초당 단추 {r}' into whatever `_statsText` is bound to.
      - v1 binds `_statsText` to the 228-px PowerText, which that string overflows.
      - `_legacyGoldPerSec` also hides `GoldPerSecText` whenever meta is on.
    - Fix: write the number only, and drop the `_legacyGoldPerSec` binding in A6 (§3.1, §7.2, §8.5).

### Quality and performance

15. **CR-15: the `StageAmbienceView` claim was wrong.**
    - Evidence:
      - The overlays are already cover-fit (×1.12).
      - The real issues on the 1464 field are a ×2.1 overlay upsample and motes 57% sparser in MAIN.
      - The moon, placed at `size.y·0.3`, sits behind the top bar in SHEET.
    - Fix: confine motes to the visible band, scale their count by area, and anchor the moon to `VisibleTop` (§6.5, §8.3).
16. **CR-16: the sleep dim was too light for a power-saving mode.**
    - Evidence:
      - At α 0.35 the screen stays at about 65% brightness. The genre's 절전 mode is near-black, and the old overlay here was 85% dark.
      - The 330-px top veil also left the chest and the boss button bright.
    - Fix:
      - Dim to 0.5.
      - Make the TopVeil opaque to y 300, fading out by y 460.
      - Set the luminance check to 40–65%.
      - See §4.9 and §7.5.
17. **CR-17: the ≡ dot bypassed the red-dot tree.**
    - Fix: use `RedDotView(top.settings)`. It already parents `top.settings.notice`, so Phase 4 menu-only leaves join without a view change (§3.1, §3.6).
18. **CR-18: the covered strip redraws every frame.**
    - Evidence: the deck cooldown radials and the lamp gauge keep dirtying the Battle canvas while fully hidden.
    - Fix (P2): give Strip its own Canvas, and disable it while the sheet is settled open (§4.5).
19. **CR-19: enemies pop in mid-street in MAIN.**
    - Evidence: all five spawn points are now on screen, whereas today three lanes enter under an edge. Each normal enemy scales in from 0 (`EnemyView`, 0.35 s OutBack).
    - Fix: add a spawn puff. It goes to WP-5 as named lines after the FX merge (§6.5, §7.4).
20. **CR-20: static state survives scene reloads.**
    - Evidence: `UIFrame` statics, like `UINav`'s, persist across the per-test scene reloads. A stale `SheetOpen` or a destroyed `SheetRect` leaks into the next test.
    - Fix: reset in `Bind`/`Unbind`, plus a SubsystemRegistration reset; covered by UiFrameTests (i) (§5.1, §7.4).
21. **CR-21: back during a header drag was undefined.**
    - Fix: call `SheetDragHandle.Cancel()`, then close from the current p (§1.2, §4.7, §5.4).
22. **CR-22: unit size stays at 1.0 in MAIN.**
    - Rationale: a MAIN zoom tops out at 1.12× (the Ppu cap), upsamples the art and pushes the lane 1/4 spawns off screen. The user asked for coverage, not zoom.
    - Kept at 1.0, with the rationale recorded in §1.3.
23. **CR-23: the art branch was ambiguous.**
    - Evidence: only the 2048 variant exists.
    - Fix: B1 accepts 1080×2048 only; any other size logs an error and falls back to `field.png`. The 1920 constants are retired (§1.4, §7.2, §9.0).
24. **CR-24: the rail reveal on unlock depended on WP-7's M78.**
    - Fix: in Wave 1c, rails reveal immediately on `Features.Changed`. WP-7 later adds the hold-until-landing (§3.3, §8.5).
25. **CR-25: the auto-return clock could fire on resume.**
    - Evidence: unscaled time can jump after backgrounding, and the clock's input rule differed from the sleep clock's.
    - Fix: reset the clock on focus and pause, cap each frame's step at 0.1 s, and reuse the `SleepModeView.HasInput` rule (§4.8).
26. **CR-26: `ContainsScreenPoint` through `_view` reached under the sheet.**
    - Fix: keep it on the Field window, clipped at `VisibleBottom`. Also use an explicit Unity null check in `FieldToCanvasWorld` (§6.2).
27. **CR-27: the graduation step had no mechanism for returning to MAIN.**
    - Fix: `FtueDirector` calls `GoToMain()` before presenting the graduation card (§5.7).

### Checked, no change needed

- **Sheet height vs tab layouts:**
  - ContentArea keeps 970–1750, and every panel's content ends inside 780:
    - Growth: LineageList 568–774.
    - Formation: mini-field from y 8, CardList 572–780.
    - Characters: Grid 100–780.
    - Expedition: ZoneList 100–780.
    - Gacha: footer 676–766.
    - Shop: grid down to 780.
  - The Shop event banner keeps the candy balance, so removing the EventChip loses nothing.
  - The Formation drag ghost uses the panel as its layer.
- **FeatureGate on the centre button:**
  - `Tab_Main` is outside `_tabButtons` and `ctx.TabButtons` (6 entries), so these are unaffected:
    - the TabGate arrays;
    - `(MainTab)i`;
    - `RetireLegacyTabBadges`.
  - `CoachTargetResolver.IsTabButton`'s prefix test treats `Tab_Main` like any other tab.
  - 사냥 is never guarded.
  - A locked `GoToTab` shows a toast and leaves the frame unchanged.
- **Smoke tests:**
  - Only `GrowthPanel_LevelUpButton_RaisesLevel` needs the A8 fix.
  - The Gacha test works, because `Select` activates the panel and ContentArea synchronously.
  - The meta test's top-bar asserts follow the updated `CoachTargets` constants (`Transform.Find` sees the inactive rails).
  - The locked-Shop assert still holds with `SelectedIndex` −1.
- **Coins, arrows and navigation:**
  - `StageClearRewardFlyer` starts coins from the `Shop` actor, which is under the camera, so in SHEET coins leave from the visible shop.
  - Danger arrows and ring alerts are camera-local and stay inside the band (the lower arrows sit at screen y ≤ 807).
  - `SourceNavigator.FocusZone` and `SelectShop` run after `GoToTab`, on a panel that is already active.
- **Re-layout:** only `DangerArrowView`, `DirectionRingView`, `GuardianSlotView` and `ShopView` re-layout, and only on `mapper.Version`, which the pan never bumps. No `OnRectTransformDimensionsChange` listener sits on the inset-driven band areas.
- **Keep-out and layout balance:**
  - Rails, the boss button and the HUD keep 0.0% overlap with the battle keep-out (`ia_check_out.txt`).
  - In MAIN the left column (StagePanel, synergy row, boss button, event icon) is denser than the right, but only while farming.
- **Unchanged behaviour:**
  - The transient ribbon, toast and synergy banner behave as before.
  - UI code reads `rootCanvas`, so the nested ContentArea canvas does not change scale math.

---

## Amendments after RM-2 (2026-11-06, orchestrator)

Applied in the RM-3 docs pass (T-5) from `scratchpad/roadmap/rm3/triage.md` §3. They record what RM-2 built and G2 verified; where they differ from older text above, they take precedence.
- **A-05 (ii) (R15, R70)** §3.6 `EventTile` row: while `EventHubTile` is shown, the tile is labelled '이벤트 상점' and opens the event shop (an RM-2 P4-4 deviation, accepted at RM-3; UX-SPEC Appendix D).
