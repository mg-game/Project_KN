# LOST & LOVELY: UI/UX Overhaul Spec (UX-SPEC)

Version 1 · 2026-11-03 · output of workflow `lost-lovely-ux-audit` (wf_9da663f3-d1e)

**Inputs**
- The 6 audit lenses produced 156 findings. They were merged into 85 items, then challenged. Result: **84 accepted** (M01–M84 except M66, plus Z99) and **1 rejected** (M66).
- Each item's `revisedProposal` from its challenge verdict is the authoritative version. It is used throughout this spec.
- Full verdict JSON: `scratchpad/ux/_findings_all.json`.
- Z99 recovery: truncation dropped the consistency-lens findings C-05…C-25 from the merge input. They were recovered from the workflow journal to `scratchpad/ux/consistency_findings.json`. **They were never challenged.** Appendix B lists where each one went: folded into an accepted item, or backlog.

**Relayed user request:** "그리고 포커스아닐때도작동하게해줘" (also make it keep working when the window is not focused). This is **M01**. It ships first as **WP-0**, before any other package (§2.0, §4).

**How to read this spec**
1. Ground rules (§0).
2. Tokens (§1).
3. Your work package (§4).
4. The screen and behaviour sections that your package's finding ids point to (§2, §3).
5. Proof (§5).

Finding ids appear as `[Mxx]` everywhere.

---

## 0. Ground rules

### 0.1 Reference frame (1080×1920, CanvasScaler, no safe-area inset)

| Region | Screen y (from top) | Built by |
|---|---|---|
| TopBar | 0–190 | MenuSceneBuilder.BuildTopBar + MetaSceneBuilder.BuildTopBar |
| StageArea (battle) | 190–970 (780). Field ≈190–814 (624), Strip ≈814–970 (156), DeckTab rises 18 px above the strip | BattleSceneBuilder, MetaSceneBuilder.BuildHud |
| ContentArea (tab panels) | 970–1750 (780) | Menu/Character builders |
| TabBar | 1750–1920 (170) | IdleSceneBuilder.BuildTabBar |

All coordinates in this spec are reference px. "Screen y" means distance from the top of the 1920 frame. `At(anchor,(x,y),(w,h))`, `Top(y,h,l,r)`, `Stretch(l,r,t,b)` and `Bottom(y,h)` follow the existing UIKit helpers.

### 0.2 Invariants for every package

1. **Coach and FTUE paths are frozen.** Do not rename or reparent:
   - `SafeArea/StageArea/Battle/{Hud/StagePanel, Hud/AutoButton, Hud/SpeedButton, Strip/DeckPanel/Card{n}, Strip/LampButton, MetaHud/GuideQuestBar, MetaHud/IdleChestWidget}`
   - `SafeArea/TabBar/Tab_{Growth,Formation,Character,Expedition,Gacha,Shop}Panel`
   - `SafeArea/ContentArea/*Panel`
   - `SafeArea/TopBar/{MailButton,SettingsButton,BagButton,JournalButton,AttendanceButton}`
   - `MetaContent.Helpers.Targets`: GrowthPanel/Buttons/LevelUp1Button, GrowthPanel/LineageList, FormationPanel/CardList, FormationPanel/TeamSynergy, GachaPanel/Buttons/Pull1Button, ExpeditionPanel/ZoneList, ShopPanel/ProductList, CharacterPanel/Grid
   - `Popups/{IdleChestPopup,JournalPopup,AttendancePopup,CharacterDetailPopup,SynergyCodexPopup}/Window/Body/...`, including `IdleChestPopup/Window/Body/ClaimButton`, `.../QuickSection/QuickButton` and `JournalPopup/Window/Body/ClaimAllButton`

   New objects may be added *next to* these. Never insert a new parent between a path and its object.
2. **Names looked up by code, tests and tools must stay:**
   - `ClaimAllButton` (MailPopup test, expedition FTUE step)
   - `LevelUp1Button` (smoke test)
   - SettingsPopup `Tab0..Tab3` (smoke test)
   - `BagButton` (TutorialSceneBuilder:512)
   - `Popups/EvolutionCraftPopup` (LevelUpFanfareView:130/312)
   - `CheckInButton` (MetaNavigator unlock target)
   - `Popups/{name}/Window/Body` (MetaDataBuilder.IsUnder)
3. **Binder fields.** A builder and the view it binds are always in the same package (§4.1), so renaming a serialized field is safe only when both sides change in one commit. Prefer adding fields over renaming them.
4. **Do not touch files owned by in-flight workflows.**
   - Attack-FX workflow: `BattleFx*.cs`, `AttackFxBuilder.cs`, `EnemyFxBuilder.cs`, `FxGalleryView`, the damage `NumberTemplate`.
   - Graphics-polish workflow: art under `Art/` and sprite swaps.
   - Wave 2 starts only after both workflows have merged (§4.0). WP-0 touches `BattleView.Enemy.cs` with a single guarded line; rebase it if the FX workflow is still open.
5. **Runtime rules.**
   - All UI motion runs on **unscaled time**.
   - No per-frame allocations: write a string only when its value changes.
   - An idle component disables itself instead of polling in Update.
6. **Focus rule (user request).** Nothing may pause, mute, dim or slow down on `OnApplicationFocus(false)` on desktop. Only `OnApplicationPause(true)` (real mobile backgrounding) may fade BGM. Focus loss may only *cancel transient input state* (holds, presses, aims) (§3.16).
7. **Copy** is 해요체 (§1.10). **Text floor** (§1.3).

### 0.3 Priority legend
- **P0**: bug, FTUE blocker or user request. Ships in the first pass.
- **P1**: core polish.
- **P2**: optional sub-step or follow-up.

---

## 1. Design tokens

### 1.1 Where tokens live (WP-1 owns all of these)

| File | Contents |
|---|---|
| `Scripts/UI/UITheme.cs` | Colours: existing ones plus §1.2. Usage rules as the class doc comment. `TabStyle.Apply` (M53). Grade, Trait and Style colours unchanged. |
| `Scripts/UI/UITokens.cs` (new) | `UIType`, `UISpace`, `UISize`, `UIMotion` (durations, easing, `EaseFill`, `Reduced`, `Deny` shake). Enums `ToastKind {Info, Success, Error}`, `CostSurface {Dark, Gold, Parchment}`, `TabVariant {Page, Option}`. |
| `Scripts/UI/UIFormat.cs` (new) | `Nbsp` (U+00A0), `Keep(s)` returning `<nobr>s</nobr>`, `ClockMono(TimeSpan)`, `ShortElapsed(TimeSpan)`. |
| `Editor/UIKit.cs` | `WhiteSprite`, `HitPad`, `OutlineMaterial` + `Outlined()`, Btn ColorBlock, `CostBtn`, `ClaimAllButton`, `FitFloor` helper (min 16 + warning). |
| `Editor/SceneParts.cs` | `ButtonStyle`, `SkinButton`, `Button(...)`. `BuildPopup` (Dim CanvasGroup, close-button pad). `PopupBalanceChip`, `PanelHeader`, `BuildBadge`, `MenuButton(size)`, `IconMenuButton`, `CurrencyChip` (tooltip hook). Grid constants. |
| `Editor/IdleProjectSetup.cs` | Real Bold weight, shared outline material, Hangul line breaking, runInBackground persistence (WP-0 lands that part first). |

### 1.2 Colour tokens

| Token | Value | Measured contrast | Use | Id |
|---|---|---|---|---|
| `PositiveInk` (new) | #23643F | 6.08:1 Parchment / 4.95:1 ParchmentDark | positive values on paper | M29 |
| `NegativeInk` (new) | #A8324B | 5.60 / 4.56 | negative or short values on paper | M29, M07 |
| `InkSub` (changed) | #7A6894 → **#5E4C7B** | 6.45 / 5.26 | captions on paper | M29 |
| `InkLine` (new) | #7A6894 (the old InkSub) | – | non-text strokes, rings, silhouettes, dividers on paper | M29 |
| `PrimaryButton` (new) | #7A58D6 | white 5.01:1 | fill of any flat Primary button that carries a text label. `Primary` stays for toggles, tracks and fills. | M82 |
| `DangerButton` (new) | #C2405C | Text 4.67:1 | destructive buttons only. Battle FX keep `Danger`. | M82 |
| `Disabled` / `DisabledText` (new) | #3B3460 / #9A90BE | – | Unavailable cost buttons | M07 |
| `CostPillBack` (new) | (0.09,0.07,0.18,0.72) | – | pill behind a short cost | M07 |
| `CostShort(CostSurface)` (new fn) | Dark/Gold: #FFC2CC (on the pill). Parchment: NegativeInk (no pill) | ≥5:1 | short cost text | M07 |
| `BadgeTextOutline` (new) | #8A1F3A | – | outline on white count/NEW text over RedDot | M82 |
| `TabSelected` (changed) | #3A2D6E → **#4A3A8A** | – | bottom nav selected tab | M52 |
| Lock cover | `TabNormal` at α 1, plus a 2 px Gold@0.35 inner outline | hides glyphs below it | HUD and tab locks | M38 |
| Toast background | `Panel`@0.95 + 2 px GoldAccent@0.5 border | – | toast | M14 |
| Optional (C-23, P2) | `Scrim`=Background@0.72, `ScrimStrong`@0.88, `Glass`@0.82, `Inset` #1F173D, `ControlOff` #2B2157 | – | replace raw dark literals when touched | C-23 |

Unchanged: `Primary`, `RedDot`, `Danger`, `GradeColor`, `TraitColor` and `StyleColor` (M83 keeps the palette).

**Usage rules.** Put these in the UITheme class doc comment.
- `Positive`, `Negative`, `Gold` and `Lavender` text go on **dark surfaces only**.
- On Parchment and ParchmentDark, text uses only `Ink`, `InkSub`, `PositiveInk` or `NegativeInk`. Numbers on paper use `Ink`.
- A Lavender fill takes Ink or InkSub text, never Text or Cream.
- A Gold fill takes an Ink label.
- RedDot with text uses a white bold label with a `BadgeTextOutline` outline of 0.2.
- Contrast targets: ≥4.5:1 for text ≤24 px. ≥3:1 for text ≥28 px bold.

### 1.3 Typography (`UIType`)

| Step | px | Role |
|---|---|---|
| Display | 56 | hero headings (banner title, celebrations) |
| H1 | 40 | popup and panel titles, top-bar power number |
| H2 | 32 | section headers, large card titles |
| Title | 28 | card and row titles, toast text |
| Body | 26 | body text |
| Label | 24 | chips, small buttons, secondary numbers |
| Caption | 22 | floor for SubText/InkSub body text and captions |
| Hint | 20 | the single instructional line per panel (min 18) [M80] |
| Micro | 18 | bold only, on a solid pill or badge (≥4.5:1) |

Rules:
- **Base fontSize ≥ 18 everywhere.**
- **Auto-size floor ≥ 16.** Every Fit or AutoSize helper computes `fontSizeMin = Mathf.Max(requested, 16)` and calls `Debug.LogWarning` with the hierarchy path when the request is below 16. The helpers are MenuSceneBuilder.Fit:2159, MetaSceneBuilder.Fit:1505, the CharacterSceneBuilder auto-size helper (~1724) and TutorialSceneBuilder.Art.cs:122.
- A floor raise must ship together with the container resize it needs (§2) [M23].
- Numbers that matter (currency, power, stage, cost, timer) are **bold**. Labels that tick every second use `UIFormat.ClockMono` [M30].
- **Real bold [M27].**
  - Import `NotoSansKR-Bold.otf` (OFL) and create `NotoSansKR-Bold SDF` with the same settings as Regular: 64 sampling, 6 padding, SDFAA, 2048, Dynamic.
  - Set `regular.fontWeightTable[7].regularTypeface = bold` and `boldSpacing = 0`.
  - Do this in a step that runs **every time** (not behind the EnsureFontAsset early return).
  - No Medium weight, no display face.
- **Shared outline material [M81].**
  - Asset: `Fonts/NotoSansKR-Regular SDF Outline.mat`. Settings: OUTLINE_ON, `_OutlineWidth` 0.18, `_OutlineColor` Background α1, softness 0, `_FaceDilate` 0.05. Call `ShaderUtilities.UpdateShaderRatios`.
  - Apply it with `UIKit.Outlined()`, which sets `fontSharedMaterial` and never instantiates.
  - Use it only on text drawn directly over battlefield art with no plate.
- **Hangul line breaking [M28].**
  - `TMP Settings.asset m_UseModernHangulLineBreakingRules = 1`. Also set it in `IdleProjectSetup.SetTmpDefaultFont`.
  - Join a number to its label with U+00A0.
  - Wrap stage labels in `<nobr>`, because TMP can break after the `-` in `2-10`.
- **Lint (warn only) [M23].** At the end of `IdleSceneBuilder.Build`, log one summary warning listing every TMP_Text with fontSize < 18, or autosize min < 16, in Main.unity and the UI prefabs. Allowlist damage-number pools, fallback glyphs and empty labels. The lint becomes failing only once the count reaches 0.

### 1.4 Spacing & grids (`UISpace`)

- Scale: 4 / 8 / 12 / 16 / 24 / 32 / 48.
- Named values: `HudMargin` 16, `ContentMargin` 24, `PanelPad` 24, `PopupPad` 36, `RowPad` 20, `ListGap` 12, `GridGap` 16, `SectionGap` 32.
- **Grid4** = cell 240, gap 16 on both axes, horizontal pad 36 (36+4·240+3·16+36 = 1080).
  - Characters: 240×330, vertical gap 18→16.
  - Shop: 244×290 pad 28 → 240×290 pad 36.
  - Acceptance: the first column's x is 36 on both screens [M24].
- **Grid5Popup** = 164, gap 17 (888 body).
  - Bag stays 164×204.
  - Reward 156×196 → 164×196.
- Leave unchanged: GachaResult 170×250, the Meta 7-column 128×150 grids, and the Growth lineage scroller (M34 sets its own sizes).

### 1.5 Sizes (`UISize`)

- **Tap targets.**
  - `MinHit` = **88** is the hard floor; the validator warns below it. `HitTarget` = 96 is preferred. HUD and top bar may use 88.
  - Extend hits with `Graphic.raycastPadding` through `UIKit.HitPad(Graphic g, float minW = 96, float minH = 96, maxL, maxR, maxB, maxT)`. No child objects. Each side is clamped to half the gap to the nearest interactive neighbour.
  - A lock cover copies its target's raycastPadding [M22].
- **Buttons.** `SceneParts.Button` never sets the size. Font presets: XL 36, L 30, M 28, S 24. Guidance for new UI: single popup CTA 520×112; a pair has equal widths with a 20–24 gap; an in-row action is 180×80 with a 28 label (C-17, backlog).
- **Icons** (guidance, C-22): IconS 32 inline cost, IconM 44 chips, IconL 64 hero amounts, IconXL 104 tiles.
- **Badges [M09].** All have `raycastTarget=false`.
  - Plain dot: 30 px with a 3 px Panel outline.
  - Count: height 40, min width 40, grows for 2 digits; '99+' at 22 bold.
  - NEW pill: 52×26, Ink 17 bold on Gold, radius 13. On the ~120 px top-bar buttons: 44×22, Ink 15 bold.
  - Anchor: top-right of the **hit rect**, pivot centre, position (-8,-8).
- **Close button**: 76×76 visual, `raycastPadding` -12 → 100×100 hit [M22].
- **Popups**: sizes unchanged in this pass [M47]. Spend popups get a `BalanceChip` 210×56 at TopLeft(30,-30). Size classes (C-09) are backlog.

### 1.6 Buttons

| `ButtonStyle` | Skin | Flat fallback | Label |
|---|---|---|---|
| Gold | `button_gold_9s`, Sliced, ppu ×2 (24 px corners), colour white | UITheme.Gold #F2C46B (drop the GoldAccent fallback) | Ink, bold |
| Primary | `button_primary_9s`, ppu ×2 | `PrimaryButton` | Text |
| Secondary | RoundSprite `Secondary` + 2 px inner outline Lavender@0.35 | same | Text / Cream |
| Soft | ParchmentDark | same | Ink |
| Danger | `DangerButton` | same | Cream |
| Ghost | no fill | – | Lavender + ' ›' |

- **Factory [M25] Phase 1.** `SceneParts.SkinButton(Button, ButtonStyle)` and `SceneParts.Button(parent, name, label, style, fontSize, out label, Sprite leadingIcon=null)`. The bodies of Menu `Skin`, Character `ApplyArt`, Meta `Skin` and Tutorial `GoldButton` become one-line calls. Call sites and names stay unchanged. Phase 2 (deleting the wrappers, CostButton routing, the legacy IdleSceneBuilder popups) is backlog.
- **Disabled**: `colors.disabledColor = (0.62,0.60,0.70,1)` plus `ButtonLabelDim`, which sets the label to α0.5 while `!interactable`. This applies to factory buttons only. UIKit.cs:82 stays as it is for other buttons.
- **Pressed** (UIKit.Btn): `pressedColor` (0.92,0.90,0.95), highlighted 0.95, `fadeDuration` 0.06 [M61].
- **Cost buttons** use Affordable / Short / Unavailable (§3.10) [M07].
- **Semantics [M26]**:
  - **Gold** = grant or commit: 받기, 모두 받기, 두 배로 받기, 도장 찍기, 출발, 도전, 진화 제작, 10회 소환, 최대 강화.
  - **Primary** = proceed, navigate, or a repeatable action: 가기 ›, …하러 가기, 보내기, 레벨업, 강화 x1/x10, 1회 소환, a lone 확인.
  - **Secondary** = dismiss or utility: 취소, 나중에, a 확인 paired with another button, 정렬, 도감, 자동 선택, 되돌리기, 그냥 받기.
  - **Danger** = destructive. **Ghost** = tertiary link.
  - At most **one Gold per view**. Exceptions: repeated list-row claim buttons count as one, and "ready-state" gold overlays (M54) may coexist.
  - In new rows, the Secondary button goes left and Gold/Primary right.

### 1.7 Radii (Phase 3, after the graphics-polish workflow locks the frames)
- `UIKit.SetRadius(Image, r)`: `mult = sprite.border.x * 100 / (sprite.pixelsPerUnit * r)`.
- Radii: Chip = h/2, S 12, M 24 (buttons, cards), L 32, Window 48.
- Migrate only plain rounded sprites. Ornate Higgsfield frames keep their hand-tuned multipliers [M24].

### 1.8 Motion (`UIMotion`)

Single easing source: `UIMotion.OutCubic / InCubic / OutQuad / InOutSine / OutBack(t, s = 1.70158)`. The math is identical to `BattleEase`. `MenuFx.EaseOutCubic/EaseOutBack`, `CharacterMotion.EaseOutCubic/EaseInCubic/EaseOutBack` and `BattleEase` become one-line delegates (zero behaviour change), then get retuned to the table below [M24].

| Interaction | Duration (s) | Curve | Detail | Id |
|---|---|---|---|---|
| Press down | 0.06 | OutCubic | scale → 0.94 | M61 |
| Release inside | 0.16 | OutBack s=1.2 | peak ≈1.025–1.03 | M61 |
| Release outside / exit | 0.10 / 0.08 | OutCubic | no click | M61 |
| Popup open | root α 0.12, dim 0.16, window 0.20 | OutQuad / OutCubic / OutBack s=1.2 | window scale 0.90→1; dim → 1 (single popup) or 0.4 (stacked) | M48 |
| Popup close | 0.12 | InCubic | window 1→0.94, α→0, dim→0, then SetActive(false) | M48 |
| Startup chain gap | 0.18 | – | `WaitForSecondsRealtime` after IsOpen goes false | M48 |
| Toast in / out | 0.15 / 0.20 | OutCubic | α + y −24→0 | M14 |
| Toast dedupe punch | 0.18 | OutBack | 1.0→1.06→1.0 | M14 |
| Tab indicator | 0.18 | OutCubic | x tween | M52 |
| Tab icon select / deselect | 0.20 / 0.12 | OutBack / OutCubic | 1.0→1.12 and 6 px lift / back | M52 |
| Panel switch, page segment | 0.12 | linear α | no slide, no stagger | M52, M58 |
| Re-tap scroll-to-top | 0.20 | OutCubic | verticalNormalizedPosition → 1 | M52 |
| Currency count | 0.35 | OutCubic | decrease: 0.20 tween + Negative→Text colour over 0.30 | M21 |
| Power count | 0.50 | existing | top bar | M10 |
| Progress fill | ≈0.3 | `UIMotion.EaseFill(cur, tgt, dt)`: snaps down; `MoveTowards(dt*max(1.2,(tgt-cur)*6))` | – | M62 |
| Pity fill / flash | 0.35 / 0.25 | OutCubic | flash: Cream α0.6→0 | M63 |
| Punch | amplitude S 0.08 / M 0.12 / L 0.25; duration 0.22 | OutBack | MenuFx.Punch default (0.14, 0.26) → (0.12, 0.22) | M24 |
| Badge appear | 0.22 | OutBack | 0→1.2→1, only on an inactive→active change | M09 |
| Count-badge pulse | 0.5 every 3 s | sine | 1.0→1.06, battle screen front-most only, one shared ticker | M09 |
| Deny shake | 0.26 | – | `x = sin(t·40)·8·(1−t)` on the label/content container, never the root. Replaces the 7/0.28, 10/0.25 and 8/0.25 variants | M07, M19, M56 |
| Hold-repeat | delay 0.35 (cards 0.45); interval `Lerp(0.17, 0.05, OutCubic(clamp01(hold/1.8)))` | – | combo pill punch 1.15 over 0.12; hide 0.4 s after release, fade 0.25 | M19 |
| Affordable shine | 0.45 sweep, α0→0.30→0; ambient every 4.0 s (x10 only) | OutCubic | suppressed within 1.5 s of a spend | M35 |
| Breathing glow | 1.6 s period | sine | α 0.30↔0.55 (tab glow), 0.35↔0.8 (deck '+') | M35, M36 |
| Feature fly-to-slot | 0.55 | InOutSine, Bezier | 96 px, scale 1→0.7, punch target 1.15 over 0.2 | M78 |
| Return chip | 0.2 fade + one 1.06 pulse | OutCubic | no loop | M72 |
| Shop buy | card punch 1.06 (0.08 up, 0.14 down); sold-out fade 0.2 | – | 0.15 s tap lock | M56 |

Rule (C-08, guidance): at most two looping elements per screen, and loop only for "actionable now" states.

### 1.9 Reduce motion

`UIMotion.Reduced => GameSettings.ReduceFlash`. This is one existing switch, '번쩍임 줄이기', with the new description '전투·뽑기·축하 연출의 번쩍임과 움직임을 줄여요 (눈 보호)' [M64]. When it is on:
- GachaCardView.Flash peak = `ClampFlashAlpha(0.75)` = 0.3. Burst counts halve: 18→9, 10→5, 8→4.
- CelebrationFx: rays at α 0.16 with no rotation and breathe = 1. Sparks 6/14 active, confetti 8/20. Pools keep their sizes.
- MenuFx.Flash: 1 pulse, period ≥0.6 s, α ≤0.3.
- Popups: alpha-only fade, 0.08 s in and 0.06 s out, scale fixed at 1 [M48].
- Screen-shake settings are unchanged (already separate).
- Backlog (C-08): a separate '움직임 줄이기' toggle, and a `UILoop` component that merges SoftPulse, MenuGlowPulse, MetaPulse and Spin and freezes them at the rest pose.

### 1.10 Copy and number tokens

- **Voice [M76].** 해요체 everywhere. Fixed verbs:
  - '받기' / '모두 받기'
  - '가기 ›' for small in-row navigation
  - '가 보기 ›' (unlock card only)
  - '…하러 가기' for large CTAs that name an action (unchanged)
  - '건너뛰기' / '확인'
- **Unlock wording [M39, M38]**, from `MetaText`. Both helpers take the *reached* index.
  - `UnlockShort(reached)` = `"스테이지 {FormatStage(reached-1)} 클리어 시"`, or "" when reached ≤ 1.
  - `UnlockSentence(reached)` = `"스테이지 {WithObject(FormatStage(reached-1))} 클리어하면 열려요"`.
  - Tiny locks (HUD chips, tab pills) keep the bare code '1-2'. Toasts use the sentence. Cards use `UnlockShort` + "\n열려요".
  - Remove every player-facing '개방'.
- **Numbers [M30]** (NumberFormatter):
  - `FormatCurrency(v)`: N0 below 1,000,000, otherwise `Format`.
  - `FormatCompact(v)`, for chips at most 5 glyphs wide: N0 below 10,000, otherwise 3 floored significant digits with trailing zeros trimmed ('12.3A', '1B'; never '1.00A').
  - `Format`/`FormatInt` are unchanged (tests pin them; damage numbers use them).
  - Rates: below 1,000 use 1–2 decimals; from 1,000 use FormatCurrency ('+1,800/시간').
  - Item counts: '×N' (U+00D7). Check that the font has the glyph; otherwise render it at 85% size.
  - Timers: `ClockMono` only in TMP labels that tick every second. `Clock`/`ResetIn`/`HourMinute` stay plain for toasts and logs.
- **Particles**: always go through `KoreanText`/`MenuText.Object` helpers.

---

## 2. Per-screen changes

### 2.0 App lifecycle: keep running when unfocused [M01] (P0, WP-0)

The simulation already runs in the background: `GameBootstrap.Awake:56` sets `Application.runInBackground = true`. Keep that line. Four gaps remain:

1. **Persist the setting.** In `IdleProjectSetup.BuildAll`, call `ApplyPlayerSettings()` right after the TMP check. The existing `AssetDatabase.SaveAssets()` at the end then writes `runInBackground: 1` into `ProjectSettings.asset:85`.
2. **No sleep dim on an unfocused desktop window** (`SleepModeView`):
   - The first line of `CanSleep()`: `if (!Application.isMobilePlatform && !Application.isFocused) return false;`
   - Add: `void OnApplicationFocus(bool focused) { _idle = 0f; _lastMouse = Input.mousePosition; if (!focused && _asleep && !Application.isMobilePlatform) Wake(); }`
   - Do not change the settings UI.
3. **Cancel stuck input on focus loss.** There is no new UIFocus.cs; each component handles it locally:
   - `HoldRepeatButton`: `void OnApplicationFocus(bool f) { if (!f) _holding = false; }`
   - `PressScale`: `void OnApplicationFocus(bool f) { if (!f) transform.localScale = _baseScale; }`
   - `EchoDeckView`: `void OnApplicationFocus(bool f) { if (!f) CancelAim(); }`
4. **No FX downgrade from throttled frames.** The first line of `BattleView.TickQuality`: `if (!Application.isFocused) { _quality.Reset(); return; }`
5. **Tell the user** that smooth editor play mode while unfocused also needs *Edit › Preferences › General › Interaction Mode = No Throttling*.

**Acceptance** (PC build and editor play mode):
- (a) Alt-tab mid-battle for 3 minutes or more. The battle keeps animating, with no dim overlay and no FX-quality toast.
- (b) Hold 강화 x1, then alt-tab. Gold stops dropping at once, and the button returns to scale 1.
- (c) Let sleep start while focused, then alt-tab. The overlay fades away.
- (d) Drag an echo card, then alt-tab. The aim preview clears.
- (e) `ProjectSettings.asset` reads `runInBackground: 1` after menu 2.
- Mobile behaviour does not change.
- Every later package that edits these 5 files must keep the handlers (check with `grep OnApplicationFocus`).

### 2.1 Battle field & HUD

**HUD map after this pass (screen px):**

| Element | x | y | Notes | Id |
|---|---|---|---|---|
| StagePanel | 14–344 | 202–336 | name and size unchanged | M11 |
| RecommendedPower root / pill | 352–652 | 202–282 / 215–267 | 80 tall hit area | M12 |
| SpeedButton (x1) | 794–914 | 204–280 | pad to 104 tall | M22 |
| AutoButton | 926–1066 | 204–280 | pad to 104 tall | M22 |
| SynergyRow (modifier + summary chip) | 16–≤540 | 344–408 | | M43 |
| BossChallengeButton (farm only) | 16–246 | 416–484 | | M13 |
| Stage-clear ribbon (transient) | 282–798 | 399–501 | | M67 |
| Toast (no popup) | 100–980 | top 480, bottom ≤620 | | M14 |
| GuideQuestBar | 16–484 | 712–796 | | M04 |
| ReturnChip | 696–1056 | 710–774 | | M72 |
| DeckTab / Strip | – | 796–814 / 814–970 | | M36 |
| IdleChestWidget | unchanged (coach target) | | | M32 |

#### 2.1.1 Battle-field validity gate [M03] (P0, WP-9)
The 1080×1920 `ux_shots` set lost the shop, pads and ring. The 540×960 runs before and after it were correct, so the likely cause is the window size or focus, not layout code. Treat findings based on ux_shots 05–11/29 as **unverified** until the §5 assertions pass. Do **not** add ForceUpdateCanvases/Recalculate to BattleView.Start. Gallery mode is not a factor.

#### 2.1.2 Fill bars that never deplete [M02] (P0, WP-1/5/7)
- `UIKit.WhiteSprite`:
  - 4×4 opaque white `Sprites/UI/UI_White.png`.
  - Import as Sprite (2D/UI), no border, FullRect, Bilinear, Clamp, no mips, alphaIsTransparency off.
  - Lazily created and loaded inside UIKit, so IdleSceneBuilder needs no change.
- The first line of `BattleSceneBuilder.Filled()` (:1613) and `MetaSceneBuilder.Filled()` (:1513): `if (image.sprite == null) image.sprite = UIKit.WhiteSprite;`.
- Fills fixed by this: Battle :461 (enemy HP), :463 (enemy trail), :476 (enemy shield), :553 (echo-unit HP), :727 (stage progress), :738/:740 (shop trail/HP), :910 (잔향), :957 (lamp), :1055 (echo cooldown radial).
- No RectMask2D and no inset changes.
- **Acceptance:**
  - The 보스 bar width tracks kills/15.
  - The enemy HP bar shrinks on a hit, with a lagging white trail.
  - The echo cooldown sweeps Radial360 from the top, counter-clockwise, at α0.72.
  - The lamp rises from the bottom.
  - A new EditMode test fails on any `Image{type==Filled, sprite==null}` in Main.unity and the enemy/echo prefabs (WP-9).
- Logged separately: the Meta/Menu bars stretch a non-sliced RoundSprite (cosmetic, backlog).

#### 2.1.3 Stage panel [M11] (P1, WP-5)
Keep `StagePanel` at 330×134 at TopLeft(14,-12), the accent bar, and StageText at 32 bold Cream.
- **Progress row:**
  - `BossLabel`: '보스까지', 20 bold, Cream@0.8, TopLeft(22,-52), 86×28, Fit(16).
  - `ProgressBar`: TopLeft(112,-59), 136×18, fill Stretch(3,3,3,3).
  - `ProgressText`: TopRight(-14,-51), 68×30, 22 bold, Cream, right-aligned, '{kills}/{total}'.
- **Boss phase** (BattleHud):
  - New `[SerializeField] TMP_Text _bossLabel`.
  - On boss start: label '보스 HP' in Negative, ProgressText = `SetText("{0}%", ceil(ratio*100))`, written only when the integer changes. Keep the Danger fill and the pulse.
  - On end: back to '보스까지'.
  - '보스 등장!' belongs to the banner, not to this slot.
- **Shop HP:**
  - ShopHpBar: TopLeft(62,-92), 254×32. The shield strip stays Bottom(3,7,3,3).
  - Text: 22 bold Cream, centred, Stretch().
  - In `BattleHud.Awake`, assign a runtime outline material: OUTLINE_ON, width 0.22, colour #21123D, dilate 0.1, the same pattern as `BattleFx.CreateOutlineMaterial`. Do not set it in the builder.
  - Never use Background-coloured text on the fill.
- ShopIcon stays 48×48 at TopLeft(10,-82).
- If the panel ever widens, move RecommendedPower to x = panelRight + 8.

#### 2.1.4 Power vs recommended pill [M12 + C-13] (P1, WP-7)
Rebuild `BuildRecommendedPower` as a single comparison pill. The top-bar power stays the global stat.
- Root: At(TopLeft,(352,-12),(300,80)), transparent, as the hit area.
- Pill: At(MiddleLeft,(0,0),(300,52)), Panel@0.88, RoundSprite, 2 px outline in the rating colour at 0.6.
- Contents:
  - rating dot 14×14 at x 14;
  - power icon sprite 28×28 at x 34 (no emoji);
  - a single TMP with rich text from x 68: `<size=26><b>{power:N0}</b></size> <size=22>/ 권장 {rec:N0}</size>`. Power in Cream bold, the rest in the rating colour, Fit 18.
- Rating word chip at the right, **mandatory** (CVD fold of C-13): '여유' / '도전' / '어려움', 20 bold, in the rating colour.
- Colours keep the 3 tiers from `PowerSystem.GetRating`: Positive / Gold / Negative.
- Text is rewritten only when power, recommended or rating changes.
- Tap behaviour:
  - Growth unlocked: `UINav.GoToTab(MainTab.Growth)`.
  - Rating 어려움 and the 강해지기 sheet exists (M13 phase 2): open the sheet.
  - Otherwise: the existing toast.
  - Add PressScale. Navigation None.
- The stage-clear ribbon does not overlap the pill; no change there.

#### 2.1.5 Stuck on a boss: farm state and boss challenge [M13] (P0 phase 1, P1 phase 2; WP-2/5/7)

**Phase 1, BattleSystem (WP-2):**
- `BattleSave.challengeTarget` (int, 0 = none) and `autoChallenge` (bool).
- On step-back after `FailsBeforeStepBack`, set `challengeTarget = stage`.
- `IsFarming => challengeTarget > 0 && Stage < challengeTarget`.
- While farming, `_pendingStage` after a clear = the current stage (hold), unless `autoChallenge` is on.
- With `autoChallenge`, re-challenge every 3 farm clears.
- **Before AutoChallenge unlocks (2-1), keep today's behaviour**: auto re-challenge with no hold, so new and offline players keep progressing. Show only the one-line banner.
- `ChallengeBoss()` calls `GoToStage(challengeTarget)` and clears the hold.
- Raise an event on step-back so the HUD can react.

**HUD (WP-5):**
- Farm state:
  - Stage title: '⟲ {FormatStage(stage)} 반복 중' (Lavender glyph, same title font).
  - BossLabel: '보스 도전까지 힘을 모으는 중' (Hint 20, SubText).
- **BossChallengeButton**: Gold 9-slice, 230×68, TopLeft(16,-226) in StageArea (screen 16–246 × 416–484, under the synergy row and left of the ribbon zone). Active only while `IsFarming`.
  - Line 1: '보스 도전 {FormatStage(challengeTarget)}', 24 bold Ink.
  - Line 2: '권장 {rec:N0}', 18 bold, NegativeInk if power < rec, otherwise PositiveInk (it sits on the gold fill).
  - Hit pad: top/bottom 10 (→ 88).
  - Pulse (MenuGlowPulse, once per state change) when power ≥ 0.9×rec or power ≥ 1.1× the power at the last fail.
  - A separate 44×44 loop toggle at the button's right edge flips `autoChallenge` (only once FeatureId.AutoChallenge is unlocked). When on, show a 24 px Lavender ⟲ badge at the top-right.
  - Hidden while an FTUE step is active.
  - *Resolution note:* the finding's (356,-252) collided with the M12 pill, the M43 row and the M67 ribbon, so the button moved to the left column.
- **Fail banner** (retreat case): '아쉬워요! {FormatStage(stage-1)}에서 힘을 모아요' (DustyPink, 1.2 s). Then slide the boss button in from x−40 over 0.25 s with OutCubic. Fails that don't step back keep the current text.

**Phase 2, 강해지기 sheet (WP-7, P1):**
- `BuildPopup 860×760`, "StrengthenPopup".
- Header: '권장 {rec:N0} / 내 {power:N0}', with the gap '-{gap:N0}' in Negative.
- Rows with a 가기 › button (Primary, 160×64):
  - 수선점 강화 → Growth tab + `FocusTarget(Targets.GrowthLevelUp1)`.
  - Lowest-level placed friend → their growth card.
  - 자동 편성 '+{delta:N0}' → Formation tab + auto-formation pulse. Only shown when delta > 0.
- Opened from: the fail-banner subtitle (tappable for 3 s), RecommendedPower when the rating is 어려움, the 권장 line of the boss button, and a no-nav stage guide quest (M04).
- **Stage list**: tapping StagePanel opens the last 10 cleared stages, newest first, and calls `GoToStage`. **Disabled while any FTUE step is active**, because StagePanel is a coach target.

#### 2.1.6 Synergy summary chip [M43] (P1, WP-5)
Replace the 8 icon slots in `BuildSynergyHud` with one **`SynergySummary`** chip inside `SynergyRow`.
- Row height 48→64. The modifier chip height becomes 60. The chip follows the modifier chip with the existing 10 px gap; when there is no modifier it starts at TopLeft(16,-154).
- Chip contents:
  - Panel@0.94 back with a 2 px GoldAccent bottom edge.
  - Up to 3 icons, each a 40×40 circle with a 3 px accent rim, centres at x 30/58/86, the first icon on top.
  - Label at x 114: '시너지 <b>N</b>', 24 bold Cream, N in Gold.
  - '›' 26 SubText, 12 px right padding.
- Width = 114 + label preferredWidth + 34, clamped 200–280. Hidden when N = 0.
- One Button + PressScale, with HitPad to 88 tall.
- Tap: N = 1 → `UINav.OpenSynergy(id)`. N > 1 → `UINav.OpenSynergy(null, kindHint: firstActiveKind)` (new optional parameter, WP-3a; SynergyNavigator selects that tab, WP-4).
- When a synergy turns on: punch to 1.12 and play the glint on icon 0.
- **Acceptance:** with 8 synergies and the longest modifier name, the row stays left of x 540.
- This makes the M22 slot padding and the M61 press work on the old slots moot.

#### 2.1.7 Stage-clear ribbon [M67] and modal suppression [M21] (P1, WP-5)
- `RibbonHeight()` returns `area.rect.height*0.5f - 260f` (fallback -50). At 0.6 scale the 516×102 ribbon sits at screen y ≈399–501, clear of the HUD (bottom ≈370 before M43; the M43 row ends at 408). Width, scale and timings are unchanged.
- Update the method comment: 'HUD 아래 빈 바닥 위, 영역 위에서 260px'.
- Re-measure after M11/M12/M43. Keep ≥24 px above the ribbon's top edge, raising 260 if needed. The ribbon may overlap the synergy row's lower 9 px only during its exit drift.
- While `UIManager.HasOpenPopup`:
  - Drop new Ribbon requests.
  - Fade a playing ribbon out over 0.15 s.
  - `StageClearRewardFlyer.OnStageEnded` skips `Fly`. The currency is already credited.

#### 2.1.8 Unit speech bubbles [M41] (P1, WP-5)
`SkillCalloutView` only, plus one number in the builder:
- **Dedupe**: an 8-entry ring (text hash + time). Drop a line if the same text is alive or started within 2.5 s.
- `_maxAlive` = 3. Drop new lines when full; never recycle the oldest. The pool stays at 5.
- **Floor**: `_floorY` = strip top + 18 + 6.
- **Guide zone**: `_guideZone` = the guide-bar rect + 6 px pad. A bubble whose x-span overlaps it lifts its bottom above it.
- **Separation pass**, oldest first. Bubble rect = w × 60.
  - Lift by `otherTop − y + 6`, at most 2 steps of 56 px.
  - If it still overlaps, or would go past half.y − 70, set `Age = 0.75·life` (fade out).
  - Ease the lift with `MoveTowards` at 600 px/s.
  - Store `Lift` in the struct. No allocations.
- Text `fontSizeMin` 15 → 18, max 22. Width clamp 96..340 → 96..400.
- **Acceptance** (re-shoot 09, 13, 15): no bubble pixel inside the guide bar or the deck-tab label, no two bubbles overlapping, no duplicate text shown at once.

#### 2.1.9 Guide quest bar [M04, M62] (P0, WP-7)
Keep the name and path `Battle/MetaHud/GuideQuestBar`.
- **Rect**: At(BottomLeft,(16, StripHeight+18),(468,84)), i.e. screen x 16–484, y 712–796. The right edge clears the south pad and guardian column (≈500–620).
- **Inside:**
  - `IconBack` 64×64 at MiddleLeft(10,0). `Icon` 52×52 shows the reward currency or item sprite, falling back to the quest icon.
  - Amount label 'x20' at IconBack BottomRight, 60×24, 20 px Cream, outlined.
  - `Text`: 27 px, Fit min 20, at most 2 lines, Stretch(84,108,6,12), Ellipsis.
  - `ProgressText`: 24 bold Cream (not SubText) at MiddleRight(-12,0), 92×40.
  - `ClaimTag`: 96×46 at MiddleRight(-10,0), '받기' 26 bold.
  - Use the shared outline material (M81) on Text, ProgressText and the amount label.
- **Fade rules** (GuideQuestBarView.Update):
  - `_complete` → target alpha **1 always**, ignoring proximity, arrows and banner. Keep the existing gold-edge breathing and the 2.4 s shine. `PanelCompleteColor` = Gold@0.18 over NightPanelColor.
  - In progress → a second CanvasGroup "Chrome" (Panel, IconBack, progress bar) fades to 0.35 near enemies and 0.3 under the banner.
  - The root stays at 1. Under the synergy banner the root goes to 0.6 (`BannerAlpha` 0.2 → 0.6).
  - Text and ProgressText are never multiplied below 0.9.
  - `NearDistance` 120 → 48.
- **Do not** reorder MetaHud above the units. Check in a capture that no guardian sprite overlaps x 16–484 at that height. The DeckTab pill (y StripHeight..+18) stays uncovered.
- **Tap on a stage-type quest with no nav**: keep `Nudge()` and add the toast '전투력 {power:N0} · 권장 {rec:N0}'. If the rating is 어려움 and the M13 sheet exists, open the sheet instead.
- **Fill**: ease toward a target (`UIMotion.EaseFill`). Snap when the quest id changes and on OnEnable [M62].
- Step 2 (a docked row) is dropped.

#### 2.1.10 수선 바구니 widget [M32] (P1, WP-7)
- Time text: `UIFormat.ShortElapsed`: '방금' (<1 min), 'N분' (<1 h), otherwise 'N시간' (whole hours). **Never '0:00'.** Write only on change.
- Put it on a pill under the basket: 104×32, RoundSprite, Background@0.65, 2 px Lavender@0.5 outline, 22 bold Cream. Keep the '가득!' FullTag swap. Outline material (M81).
- **Red dot** (WP-2, `IdleChestSystem.HasRedDot`): `IsUnlocked && !Tampered && (IsFull || ElapsedSeconds >= redDotHours*3600)`.
- **FreeTag**: Gold pill, '무료' Ink 18 bold, 64×28 at TopLeft(2,-2). Shown when `HasFreeQuickIdle && !HasRedDot`. One badge at a time.

#### 2.1.11 Echo deck strip [M36] (P1, WP-5)
- **Empty slot.** Remove the '비어 있음' label. A *candidate* exists when any unlocked form gives `CanPlace(Echo, index, form) == Ok`; evaluate on Formation.Changed.
  - Candidate: '+' bars 40×8 and 8×40 at α0.9 with MenuGlowPulse (1.6 s, α0.35↔0.8). Tap opens Formation with the toast '이 칸에 넣을 잔향이가 있어요'.
  - No candidate: bars at α0.3, no pulse. Tap shows the toast '새 친구를 만나면 잔향이를 더 넣을 수 있어요', with no navigation. If the gacha tab is unlocked, pulse `CoachTargets.TabGacha`.
  - While an FTUE step is active, suppress the toast and navigation.
- **Locked slot.** Lock 46 px at y +14. Pill DeepInk@0.9, 72×28 at Bottom(8), text `FormatStage(EchoSlotUnlockStage)` 20 bold Gold, autosize to 16. The existing toast now uses `UnlockSentence` (M39).
- **Cost badge.** Fill #4B2A8C, rim Resonance α1, 40→44 at TopLeft(-8,8), text 26 bold Cream.
- **Stars.** Build them after Cooldown/CooldownText so they draw above the dim. 18 px with a 0.2 DeepInk outline. Grade ≥4 shows '★4'.
- **Deck tab.**
  - Show '탭 = 자동 소환 · 끌기 = 방향 지정' (20 bold Cream on a 440×32 Panel pill) until `CounterKey.EchoSummon ≥ 10` **and** flag `hint.deck.drag` (set by SummonAimView on a successful drag-aim summon). Then fade it over 0.4 s.
  - After that the tab reads '잔향이 N/M', 18 SubText, with a 160×48 transparent hit area that opens Formation.
  - *(Resolution: M36's rule replaces M80 item 3.)*
- **Resonance panel.** Keep 180 wide; the deck position and `CardWidth` 98 are unchanged. Remove the '잔향 보유량' title. Icon 64 and count 52 Cream centred at y +6. The bar stays.

#### 2.1.12 Lamp button [M37] (P1, WP-5)
- **Label:**
  - At(MiddleRight,(-14,4),(110,104)), lineSpacing -10, autosize 22–28, Cream.
  - `LampButtonView.cs:74`: replace the lerp with `_label.color = UITheme.Cream`. Optionally lerp toward white at `_ready`.
- **Inner field**: Background α 0.55 → **0.68**. The frame and `_idleTint` are unchanged.
- **Percent:**
  - 24 bold Lamp, autosize 16–24, outline 0.2 Background (or the M81 material).
  - Rect At(BottomLeft,(10,2),(102,26)).
  - Move RingBack and Ring to MiddleLeft(12,12) and the icon to y +4.
- **Press feedback**: `background.gameObject.AddComponent<PressScale>()`.

#### 2.1.13 HUD lock covers [M38] (P1, WP-7)
- `BuildLockCover`: cover colour = `UITheme.TabNormal` α1 (≥0.95 acceptable), plus a 2 px Gold@0.35 inner outline.
- Hint: short code '1-2', 22 bold Gold, Fit min 14→18, Stretch(44,6,8,0).
- Do not touch the target's CanvasGroup.
- Copy the target's `raycastPadding` onto the cover (M22). Fallback sizes at MetaSceneBuilder:167-168 match the new AUTO/x1 rects.

#### 2.1.14 HUD tap targets [M22] (P1, WP-5/7)
- AUTO: 140×76 at TopRight(-14,-14). x1: 120×76 at (-166,-14). Both padded to 104 tall.
- RecommendedPower: see §2.1.4 (80-tall root).
- Synergy: the summary chip (§2.1.6).

#### 2.1.15 Outline text on the field [M81] (P1, WP-5/7)
`.Outlined()` goes on:
- the shop HP value (alternatively M11's runtime material; pick one);
- the chest TimeText;
- the lamp Percent;
- ProgressText;
- the ring hint caption;
- ring name plates that have no backing.

Never on cream cards, panels, buttons, Ink/InkSub text or damage numbers.

#### 2.1.16 Return chip [M72] (P1, WP-3a/3b)
- `ReturnChipView` under StageArea, built after `BattleSceneBuilder.Build`.
- Anchor and pivot (1,0) at (-24,196), 360×64 (screen 696–1056 × 710–774).
- Style: PanelLight round, 2 px GoldAccent outline, '‹' + label 26 Cream, autosize min 20, left padding 20. A 48×48 '×' hit area on the right (22 px glyph).
- Fade in 0.2 s with one 1.06 pulse.
- Hidden (not cleared) during FTUE, coach marks, or while any popup is open. See §3.14.

### 2.2 Top bar [M10, M08, M09, M21, M68] (P0/P1; WP-4 owns MenuSceneBuilder.BuildTopBar + TopBarView, WP-7 owns MetaSceneBuilder.BuildTopBar + TopBarMetaView)

| Element | Rect | Style |
|---|---|---|
| Logo | removed from the in-game TopBar (stays on title and intro) | – |
| `LevelText` | TopLeft(26,-20), **300**×46 | 32 bold Cream '수선점 Lv.N' |
| `PowerChip` (new) | TopLeft(20,-84), 380×58 | PanelLight@0.6 rounded. Icon 40 at Left(12,0) (ArtLib power/sword sprite, else MetaIcons.Star tinted Gold). `PowerText` Left(60,0) 300×54, 40 bold Gold, autosize min 28, `PowerSystem.Format`. Tap shows the RecommendedPower toast text. |
| `GoldPerSecText` (name kept) | TopLeft(28,-146), 380×30 | 22 regular SubText '초당 단추 1.1' |
| Currency chips | unchanged rect (y 18–80, x ≈337–1060) | see below |
| Buttons (5) | 120×80 each, TopRight y −98 (screen 98–178). x: Settings −20, Mail −150, Bag −280, Attendance −410, Journal −540 (left edge 420). | `SceneParts.MenuButton(size 120×80)`, labels 30 bold. `MetaSceneBuilder.TopButton` calls it as well. HitPad top/bottom 4 (→ 88). |

- *Resolution note:* the left block is narrowed to 300 wide (LevelText) and the PowerChip moved down to −84. M10's coordinates overlapped the currency chips (x ≥337, y ≤80).
- **Delete `NoticeButton`** and the `_noticeButton`/`_noticeBadge` fields in TopBarView. Unread notices show a **plain dot** on SettingsButton (M09 rule: FYI items get a dot, no count). Settings › 공지사항 keeps its own `NoticeButton`.
- **TopBarMetaView:**
  - Stop creating `PowerStatsText`.
  - Bind `_statsText`/`_statsPunch` to `PowerChip/PowerText`. The string is the number only, with the existing 0.5 s count-up and punch.
  - Keep `GoldPerSecText` active.
  - Point PowerUpFloatView's punch target at PowerText.
- **IconMenuButton** (P2): icon 44×44 at Top(0,-6) and a 20 px label at Bottom(0,4). Use it only when `ArtLib.UI("icon_settings|icon_mail|icon_bag|icon_attendance|icon_journal")` exists; otherwise fall back to text. Note for the graphics workflow: these icons are missing.
- **Currency chips [M08]:**
  - Gold and gem chips become Buttons via `MakeTappable`, each with a transparent raycast child giving an 88 tall hit area. Clip it so it stays above y 96.
  - Gem chip: a '+' badge, a 36×36 Primary circle with a Cream '+' at MiddleRight(-8,0); amount right inset 52. Tap opens ShortfallPopup in *sources mode* (titled '별조각 얻는 곳').
  - Gold chip: tap opens a small sources sheet (수선 바구니, 스테이지 진행, 원정).
  - Event chip: unchanged.
- **Counter sync [M21]** (TopBarView):
  - Target = `max(0, Currency.Get(t) − RewardFlyer.PendingOf(t))`.
  - If |Δ| > 1% (or >1 when below 100), or `PendingChanged` fired: tween over 0.35 s OutCubic, restarting from the current shown value. Otherwise set directly (passive gold).
  - Decreases: 0.2 s tween plus a Negative→Text colour lerp over 0.3 s. Skip both below 1%.
  - Write text only when the display key changes. Below 1M, `GoldDisplayKey` = `Math.Floor(amount)`.
  - Chips use `RewardService.FormatAmount` → FormatCurrency [M30].
- **'+N' reward label [M21]** (RewardFlyer.StepDest):
  - Pill centred on the chip's bottom edge, x = chip centre + 0.25·chip width.
  - Height 40, padding 14, min width 72, Panel@0.92 with a 1.5 px GoldAccent@0.5 border, text 28 bold in the currency tint.
  - Keep the 14 px rise, the 0.75 s hold and the 0.3 s fade.
- **Badges [M09, M82]**: count badges for journal and mail (white bold + outline); a dot for attendance; a dot for settings (notices).
- **Notch bleed [M68]**: `TopBleed` Img(Panel) is the first sibling of the bar. Anchors (0,1)–(1,1), pivot (0.5,0), size (0,400), raycast off.
- **Acceptance:**
  - At 1080×1920 there is no overlap between the left block (x ≤406, y ≤176), the chips (y 18–80) and the buttons (x ≥420, y 98–178).
  - Smoke test paths for JournalButton and AttendanceButton resolve.
  - `BagButton` is found by the tutorial builder.

### 2.3 Tab bar [M52, M09, M35d, M68] (P1, WP-3a; colour token WP-1)
- `Indicator`: Img(bar, Gold, RoundSprite), 104×6, anchored top-left, pivot (0.5,1), y 0, ignoreLayout, raycast off. Bound to `MainTabController._indicator`.
- `TabSelected` = #4A3A8A.
- MainTabController:
  - `Start`: `Canvas.ForceUpdateCanvases()`, then snap the indicator to the default tab.
  - `Apply`: indicator x tween 0.18 OutCubic. Selected icon scale 1.12 and y −14→−20 (0.20 OutBack); others return over 0.12. Store base positions once. The panel's CanvasGroup α 0→1 over 0.12, only on change. One coroutine handle per group via `MenuFx.Restart`.
  - **Re-tap** (user tap, same index): play the click SFX hook; scroll the panel's first active ScrollRect to the top (0.20 OutCubic); raise the new `Reselected(MainTab)` event. **Do not** raise `Selected`.
  - A user tap on a *different* tab calls `UINav.ClearReturn()` (§3.14).
- **Badges**: the selected tab never shows a badge (TabGateView, §3.13).
- **강화 glow [M35d]**: an `art.Glow` 120×120 Gold@0.45 under the Tab_GrowthPanel icon, breathing 0.30↔0.55 over 1.6 s. Shown only while another tab is active, the shop isn't maxed, and `gold ≥ cost10`. Evaluate in MainTabController on `Currency.Changed` against a cached cost10 that GrowthPanel publishes (static). It never uses the RedDot system and draws under unlock dots.
- **Bleed [M68]**: `BottomBleed` Img(TabNormal), first sibling, anchors (0,0)–(1,0), pivot (0.5,1), size (0,400), ignoreLayout.

### 2.4 강화 tab [M33, M34, M35, M19, M07, M51, M29] (P0/P1, WP-4)

Final layout (panel-local y from the top of the 780 ContentArea):

| y | Element |
|---|---|
| 0–88 | `PanelHeader` [M51]: title '수선점 강화', 34 bold Cream at x 24. The hold hint ('x1 버튼을 길게…') is **deleted**. |
| 96–306 | `ShopCard` (cream Card, Ink). Art 206→**180** at MiddleLeft(36,0), glow 230×210. `LevelText` 44→**38** at (250,-24). `StatText` 30 at (250,-78): '초당 단추 1 → <b><color=PositiveInk>1.1</color></b>'. `HpText` 26 at (250,-124): '수선점 내구도 300 → 312', next value in PositiveInk bold. **DescText deleted.** |
| 322–472 | `Buttons`: `LevelUp1Button` (Primary, HoldRepeat, `HoldPill` '길게' 76×32 at TopRight(-10,-10), PanelLight@0.85, Ink 19, raycast off), `LevelUp10Button` (Primary), `LevelUpMaxButton` (**Gold**). |
| 486 | `LineageHeader` '친구 레벨 <color=SubText><size=80%>{n}명</size></color>' |
| 480–532 | P2 '모두 레벨업' chip: Secondary 200×52 at TopRight(-30,-480) (top pivot), in line with the header. It replaces LineageHint. |
| 534–740 | `LineageList` (horizontal) |

- **MAX button** (RefreshMax, keeping the `_maxShrinkBelow`/`_maxGrowAt` boundaries):
  - count ≥2: '최대 +{count}' with the affordable cost.
  - count ≤1: '최대', cost hidden, a serialized CanvasGroup at α0.5, `interactable=false`.
  - At max level, all three buttons show '최대 레벨' (CostButtonView Unavailable).
- **No '전투력 +N' on the shop card.** The top-bar power FX covers it.
- **Optional one-time toast**: '강화 x1을 길게 누르면 연속으로 강화돼요' after 3 single taps within 8 s, gated by a save flag.
- **Lineage cards [M34]:**
  - Card 244×196 → **212×196**, spacing 12, padding 24/24. Result: 4 full cards plus a ≈150 px peek.
  - A 72 px right-edge fade (panel colour, α0→0.9), hidden when the content is not wider than the viewport.
  - Name and stars column 124→92 at x 112.
  - `Lv.N` moves to a portrait badge: 20 bold Cream on Ink@0.85, 88×26 at TopLeft(14,-80).
  - The freed slot (112,-72) 92×34 holds `GainText`, 20 px **PositiveInk** (the card is parchment, so the M29 rule replaces M34's 'Positive'): '전투력 +{Format(PreviewLevelUp(id))}'. Unplaced lineages show '미편성' (InkSub); at max, 'MAX'. Compute only in `Set()`, never per gold tick.
  - Sort (local): the active FTUE or guide-quest lineage first, then placed lineages in slot order, then unplaced by grade descending, then id. Compute only in OnEnable or when the owned count changes; never reorder on level-up. On enable with a coach target, `horizontalNormalizedPosition = 0`.
  - `HoldRepeatButton` on the card `LevelUpButton`, delay 0.45, with drag-cancel (§3.11).
  - P2 '모두 레벨업': round-robin, lowest level first, until gold runs out. Toast '친구 N명 레벨업 · 전투력 +X'. Disabled while an FTUE step targets a lineage.
- **Affordable feedback [M35]** (buttons only):
  - Each button gets a `ShineMask` child (RectMask2D, inset 6, last sibling) holding `Shine` (art.Glow 56×170, rotated 20°, Cream@0.30, inactive). Neither name may contain 'LevelUpButton'.
  - A false→true affordability flip plays one sweep plus `Punch(0.06, 0.20)`, unless within 1.5 s of `_lastSpendTime`.
  - Ambient sweep on x10 every 4.0 s while `gold ≥ _cost10` and not maxed.
  - All shines are suppressed while a coach mark targets LevelUp1Button.
  - LevelUpFanfareView chip text: '수선점 내구도 +N' → **'10레벨 동안 내구도 +N'**. Milestone pips are dropped. Placement (RM-2 KIT): the chip is sized to its text (240–600) and placed at the 내구도 line height, right of the furthest text end of `LevelText`/`StatText`/`HpText` (+24, card right −16), rising 60. If the gap is too narrow it shrinks to the gap (text autosizes down to 70 %, never below 16 px); if it is still too narrow it sits right-aligned at card right −16 on the 내구도 line without rising. It never leaves the ShopCard (the Growth Buttons row starts 16 px below) and never covers 'Lv.N'.
- **Cost states [M07]**: all three buttons and the card buttons use `CostButtonView`. A gold shortfall shows the toast `MetaText.GoldShortfall`, throttled to 1.5 s [M08].
- **Hold [M19]**: §3.11.
- **Acceptance (12_tab_Growth):**
  - At '+1/10' MAX reads '최대' dimmed.
  - The x10 cost in the Short state reads ≥4.5:1 on its pill.
  - The '1.1' next value is legible (≥5:1).
  - The lineage list shows a peek of the 5th card.
  - The FTUE ShopLevelUp x3 step and the smoke test `LevelUp1Button` still pass.

### 2.5 편성 tab [M44, M45, M46, M80, M16, M22, M05, M23] (P1, WP-4)
- **Team synergy panel** (MenuSceneBuilder ~401-421; panel name `TeamSynergy` unchanged):
  - Codex button: 150×50 at TopRight(-16,-10), label 19 (min 16). Its hit area extends to the divider; move the divider to y 66.
  - List: Top(72, …). **Keep 4 rows visible.** Rows are 56 (below); lower the list's bottom inset as far as the RuleText line (Bottom 80, h 22) allows.
  - RuleText: Bottom(80,22), 17→**18**, min 16, text '<gold>규칙</gold> · 같은 계보 최대 2종'. The full '(지킴이 1 + 잔향이 1)' explanation moves into the codex header.
  - AutoButton: 368×64 at BottomCenter(0,12), label 26.
- **SynergyRow** (~1380-1426):
  - Height **56**.
  - Icon ring 44×44 at x 5. The ring colour encodes the kind: Trait = Lavender, Combo = DustyPink, Style/Role = SoftBlue.
  - The visible KindChip is SetInactive, but keep the `_kindChip` binding; SynergyRowView sets the ring colour.
  - Name: 21 bold, min 17, at (56,-3), 190×28.
  - Progress: 20 bold, min 16, TopRight(-10,-3), 110×28.
  - Pips 12×12, step 16, from x 56 at y −32.
  - Favoured tag '×1.5': **64×26** at TopRight(-80,-28), 18 bold, min 16.
  - Delta badge '발동'/'완성': **64×26** at TopRight(-10,-28), 18 bold, min 16, Ink on Gold.
  - *(Resolution: M44's layout uses M23's pill sizes and floor.)*
- **Formation card [M45]** (`BuildFormationCardPrefab` + FormationCardView):
  - Root 152×196, HorizontalScroll spacing 10.
  - PortraitBg Top(8,100,8,8). Portrait TopCenter(0,-6), 100×100.
  - Trait chips: 50×26 at TopLeft (6,-6) and (6,-34), icon 20, label 18 bold Ink, min 16. Keep LitRing.
  - `LevelPill`: TopRight(-6,-6), 58×26, Background@0.8, 'Lv.{n}' 18 bold Cream, min 16.
  - Role glyph 26×26 at TopRight(-6,-36), **only when not placed**: a Guardian or Echo colour dot with '지'/'잔' 16 bold. **RoleText deleted.**
  - StateBand: TopCenter(0,-86), 140×28, 18 bold, min 16, same colours.
  - NameText: Top(116,28,6,6), 22 bold Ink, min 16.
  - StarsText: Top(144,18), 18, coloured per M83.
  - StyleChip: BottomLeft(8,8), 56×26, 18 bold, min 16.
  - `PowerText`: BottomRight(-8,8), 76×26, `Format(GetFormPower)`, 18 bold Ink, MidlineRight, min 16, with an 18 px power icon if one exists.
  - Refresh in RefreshState, plus on Power.Changed. No long-press.
  - *Not doing:* the 168×212 card, Gold power text, drag-to-live-battle pads, shrinking the mini field.
- **Power [M46]:**
  - The top-right field pill reads '지킴이 3/5  ⚔ 1,226': a 26 Cream number plus a 24×24 sword *sprite*, pill widened. Refresh on Formation/Power.Changed.
  - **Drag delta**: only while hovering a placeable slot. Compute once per hover change: `PreviewPlace − TeamPower`. Pill 120×40, 8 px under `_ghostSynergyTag`: '전투력 +312' (Positive) / '전투력 −80' (Negative), 24 px, PopIn 0.2. Hidden when 0. Not shown on tap-select.
  - `PowerSystem.PreviewPlace` becomes synergy-aware (WP-2). If it can't be exact, prefix '약'.
  - After a drop, `PowerUpFloatView.AllowDecreaseOnce` (0.5 s window): decreases float in Negative at 0.8× with no punch, shimmer or sound.
- **Hints [M80]:**
  - `HintText` (min 16→18): the idle text '카드를 위로 끌어…' shows only while `FormationSave.placementsDone < 3` (a new counter, WP-2). The live drag-state texts are unchanged.
  - `DeckHint` (MenuSceneBuilder:447) deleted; EchoCount 22→24.
- **Tap targets [M22]**: codex button as above; Undo 300×52 padded **upward only** (0,0,0,-16), never into CardList (top at 572).
- **Echo slot lock hint (MenuSceneBuilder:497)**: autosize 18–22, two lines, text `UnlockShort(stage)` + '\n열려요' [M28, M39].
- **New-friend nudge [M16]**: `RedDotPaths.FormationEmpty` ('tab.formation.empty', WP-2). FormationPanel exposes `PulseRosterCard(lineageId, 3f)` (scale 1→1.06 plus a gold outline, scroll into view). P2: SynergyPanelView pulses 자동 편성 when AutoFormation would raise TeamPower ≥5%. The value is cached on Formation/Characters change; the pulse stops after one tap or 3 visits without action.
- **Expeditions no longer block placement [M05]**:
  - FormationCardView/FormationPanel stop dimming dispatched cards and stop blocking drags.
  - A dispatched card shows an informational pill '원정 중', 96×26, DustyPink, Ink 16→**18** bold, top-right. When the LevelPill is present, stack it under the level pill at (-6,-36) and hide the role glyph.

### 2.6 친구들 tab [M40B, M45, M22, M51, M24] (P1, WP-6)
- Header via `PanelHeader` [M51]. The sort button moves to the right slot at **180×64** [M22].
- Grid: Grid4 (240×330, gap 16, pad 36) [M24].
- Card line 'Lv.1 · 진화 1/11' splits into 'Lv.1' (24 bold Cream, MidlineLeft, inset 14) and '진화 1/11' (19→**20** px Ink on a Lavender pill 92×28, MidlineRight, inset 12) [M45].
- Unowned cards [M40B] (CharacterCardView only):
  - `_group.alpha` 0.88→1.0.
  - Trait chip alpha 0.6→0.85.
  - Stars stay at 0.45, the '미보유' chip is unchanged.

### 2.7 원정 tab [M58, M57, M05, M40A, M39, M51, M22, M77] (P0/P1, WP-4)
- **Header row** (single, 100 px; `PanelTitle` '원정' and the Hint removed) [M58]:
  - `Seg_Expedition` '원정': At(TopLeft,(24,-16),(200,68)), 28.
  - `Seg_Dungeon` '잔향이 던전': (232,-16), 260×68, 28.
  - Selected: Gold fill with Ink text. Unselected: PanelLight with Cream@0.7.
  - Badges at each segment's top-right (+6,+6): the expedition count shows completed runs; the dungeon dot shows tickets > 0.
  - `ActiveChip`: At(TopLeft,(508,-26),(200,48)) on the 원정 page.
  - `ClaimAllButton` (`UIKit.ClaimAllButton`): TopRight(-24,-18), **240×64**, 28, with a 88 hit area [M51].
  - The 던전 page hides both and shows the ticket pill '입장권 n/2' in the (508,-26) slot.
- **Pages:**
  - `ZoneList`: Stretch(0,0,100,0), unchanged.
  - Sibling `DungeonPage`: Stretch(0,0,100,0), containing the DungeonCard 1032×300 at (24,-4), plus a '층별 보상' list. If there is no floor data, show the line '매일 05:00 충전' instead.
  - Remove `SetAsLastSibling` (:86).
  - `SelectPage(int)`: session-persistent. Reset to 원정 on launch, except open 던전 when tickets > 0 **and** all expedition slots are busy.
  - `FocusDungeon` = `SelectPage(1)` + Highlight. `FocusZone` = `SelectPage(0)` + scroll. 0.12 s crossfade.
  - Cheaper fallback (if needed): `SetAsFirstSibling` and a 1032×200 card at 0 tickets.
- **Zone card, locked [M40A + M39]:**
  - Dim Ink 0.45 → **0.22**. Art tint (0.55,0.52,0.62) while locked, white otherwise.
  - LockIcon 50 with a 64 DeepInk@0.85 circle.
  - `LockPill`: (0.06,0.04,0.13,0.88), RoundSprite, Bottom(2,74), 4 px side insets.
  - `LockText`: 22 bold Cream, Fit 16, text `UnlockShort(requiredStage)` + '\n열려요' (e.g. '스테이지 2-9 클리어 시 / 열려요'). *(Resolution: M39's wording overrides M40's 'keep copy'.)*
- **Send loop [M57]:**
  - `ExpeditionSetupPopup.Show` pre-fills from `GetLastMembers(zone)` when every member is still selectable; otherwise it runs a silent `AutoFill()`. The `자동 선택` button stays and toasts only when the result is 0.
  - Empty-state text: '보낼 수 있는 친구가 모두 원정 중이에요' (after M05, placement no longer blocks).
  - Ready card with valid last members and a free slot: primary label '다시 보내기' (same ~190×80 rect) calls `TryStart` directly. The hint line under it becomes the link '친구 바꾸기 ›' (same position, 88×48 hit) that opens the pre-filled setup.
  - If any member is ineligible, show the normal 보내기.
  - Last members are saved (WP-2).
  - '모두 다시 보내기' is a follow-up.
- **Member cells [M05]**: placed friends stay selectable, with a bottom pill '지킴이'/'잔향이' (120×26, Ink 16→**18**, Cream bg). Friends on another expedition are greyed out with the reason. AutoSelect order: not dispatched → biggest affinity / recipe-count need → power.
- **Copy [M05]**: MenuSceneBuilder:638 hint → '친구를 보내 재료를 모아요 · 원정 중에도 함께 싸워요' (21→Hint 20, same width).
- **Running card cancel [M22]**: 140×44, font 20. Check the card height and move ProgressBg if needed. The cancel confirm uses Danger with the label '원정 취소' [M55].
- **Dungeon reset copy [M77]**: toasts at DungeonCardView:99/125 read '오늘 입장권을 모두 썼어요. 새벽 5시에 다시 충전돼요.' `TicketReset` text: At(TopRight,(-22,-74),(230,28)), 19→**20** SubText, '충전까지 {ClockMono}'. Only shown when tickets < daily, on a 1 s timer while visible.
- **Reward chips [M30, M83]**: `FormatCompact`, '×N', and a 3 px grade bar for Epic+ on mini chips.

### 2.8 소환 tab [M63, M84, M07, M08, M55, M74] (P1, WP-6)
- Banner (20–360) and rates (372–430) unchanged.
- **Banner scrim [M84]:**
  - `_fadeLeft = Generated("CH_FadeLeft",128,8,(u,v)=>pow(1-u,1.6))`.
  - `TextScrim` Img (0.05,0.03,0.12,0.62) with `_fadeLeft` at BottomLeft, 600×230, raycast off, placed after `Shade`.
  - TitleText: At(BottomLeft,(36,70),(540,72)), autosize 42–54.
  - Subtitle: (38,30), 560×38, autosize 20–24, Ellipsis.
- **Pity row [M63]**: `Pity` Top(440,72,30,30).
  - PityText on the top line (h 34): the existing rich text + '  <color=SubText><size=85%>{count}/{pityCount}</size></color>', 24 Cream.
  - PityBar full width, 18 tall at Bottom(6). Track PanelLight (visible at 0). WhiteSprite fill Stretch(3,3,3,3) in GradeColor. Optional 20% ticks at α0.25.
  - Tween the increase over 0.35 s plus a flash. Snap on wrap.
  - When remain ≤10, pulse the remain number Cream↔Gold over 1.2 s.
- **Buttons**: Top(526,150,30,30). Both pulls are `CostButtonView` with Surface Gold/Primary [M07]. The discount pill uses GoldAccent with Ink text [M09/M82].
- **Gem-source link [M08]**: `GemSourceLink`, a 22 px Lavender centred link '별조각 얻는 곳 ▸' at Top(682,32), active only when either pull is unaffordable. When unclaimed gem mail exists the text is '우편함에 별조각이 있어요 ▸'. Opens ShortfallPopup in sources mode. While it shows, Hint moves to Top(718,58) at 20 px. Otherwise Hint stays at Top(688,76), 21→**22**.
- **Pull flow [M55]**:
  - 1-pull: instant (keeps `FtueDirector Pull(1)`).
  - 10-pull: the **button handler** calls `ShowSpendConfirm("10회 소환", "<배너명>에서 10번 소환할까요?", Gem, cost, "소환", …, skipKey "confirm.gacha10")`.
  - `GachaPanel.Pull(int)` itself stays confirm-free, because the smoke test and AutoShot call it directly.
  - A short pull opens ShortfallPopup(Gem, need) [M08].
- **First-meeting retention [M74 3b]** (P2): the 소환 panel shows the FirstMeeting button while `!FirstMeetingSummoned`, independent of `tutorial.IsActive`.

### 2.9 상점 tab [M56, M77, M30, M07, M08, M55, M51] (P1, WP-4)
- No title row. The 300×72 segment bar sits at y 12 with 24 margins [M51]. Tabs use the M53 Page style; the event tab gets a 6 px DustyPink accent bar.
- **Reset pill [M77]**: `ResetPill` 330×40, right edge −24, **vertically centred on the tab row**. Text: 22 SubText, `'초기화까지 ' + ClockMono(Game.Shop.TimeUntilReset)`. Shown on the 수선점 상점 tab only. Refreshed on a 1 s timer while active; at 0, `CheckDayRollover` and rebuild the cards. Shop copy line 2: '구매 횟수는 매일 새벽 5시에 다시 채워져요.'
- **Product card:**
  - `LimitText` → Top(182,26), 19→**20** InkSub, min 16: '오늘 2/3 · 보유 15' / '한정 1/1 · 보유 3'. Costumes are unchanged.
  - The price row uses `CostButtonView`.
  - The product name drops the count (WP-2 ShopDatabase); the badge carries '×5'.
  - The `SoldOut` overlay gets a CanvasGroup and fades in over 0.2 s.
- **Buy flow [M56]** (`OnBuy`):
  1. Pre-check: `Game.Shop.CanBuy`. If short: gem or event currency → `UIManager.ShowShortfall`; gold → the `GoldShortfall` toast + Deny shake. Other failures → the `Describe` toast.
  2. Single-reward non-costume products:
     - No RewardPopup. Gem-priced ones keep a confirm via `ShowSpendConfirm` (no skip key); gold-priced buy on tap.
     - On success: `RewardFlyer.Fly(rewards, icon)`, card punch, the toast '{name} ×{n} 획득 (보유 {count})' (Success), immediate `RefreshLimit`, and a 0.15 s tap lock.
  3. Bundles: RewardPopup '구매 완료' plus Fly on 확인.
  4. Costumes: confirm → RewardPopup '의상 획득!'.
     - New optional RewardPopup secondary action, `Show(..., actionLabel, onAction)`: a Secondary button of equal width.
     - If the lineage is owned: '바로 입혀 보기' → `TryEquip`, Close, then `UINav.OpenCharacter(id, TabCostume)`.
     - Otherwise the subtitle reads '{친구}을(를) 만나면 입힐 수 있어요'.
     - Remove '친구들 탭에서 갈아입힐 수 있어요'.

### 2.10 Popups

#### 친구 상세 CharacterDetailPopup [M54, M47, M60B, M73, M19, M53, M07, M08, M29] (P1, WP-6)
- `LevelUpButton`: unchanged (Primary 220×112, HoldRepeat), switched to `TryStep` (§3.11).
- `ResonanceButton`:
  - 166×98, Tone.Secondary, Cream label and cost.
  - Child `ReadyArt` (button_gold_9s, Sliced, ppu 1.6, stretch, first sibling, raycast off), bound `_resonanceReadyArt`.
  - ready = `canRaise && shards ≥ cost`. When ready: ReadyArt on and the label/cost in Ink. When not affordable: the cost is `Negative` (dark surface).
- `CheckButton`:
  - Bottom(0,112), Secondary, label '진화 레시피 보기 ›' 32 Cream.
  - `ReadyArt` child bound `_checkReadyArt`, together with `_checkGlow`.
  - States (EvolutionTreeView.Refresh, aligned with the M75 labels):
    - not owned: '진화 확인', non-interactable;
    - craftable: '{clue} 진화 제작! <size=80%>준비 완료</size>' in Ink with the gold overlay;
    - clue not craftable: '다음 진화 · {clue} <size=80%>재료 {k}/{n}</size>' in Cream;
    - no clue: '진화 단서 찾는 중' in SubText, interactable (the toast explains).
  - Label autosize 26–34.
- `BalanceChip` (Gold) via `SceneParts.PopupBalanceChip`, direct child of Window. Title insets Top(6,104,250,250). The 레벨업 cost turns red when unaffordable and refreshes on Currency.Changed [M47].
- Resonance line [M60B]: `$"계보 능력치 <color=SubText>+{cur}%</color> → <b>+{next}%</b>"` (base colour Positive, dark tile). At max: '(MAX)'. Fit 16. `next` comes from `CharacterSystem.GetResonanceBonusPercentAt(level+1)`. Optional: the confirm at :339 says '능력치 +{cur}% → +{next}%'.
- Tabs: `public const int TabTree=0, TabSkill=1, TabCostume=2, TabInfo=3`. `Show(lineageId, tab = -1)`; if already open and tab ≥0, call `SelectTab` [M73]. The tab style is M53 Page.
- Gold shortfall → `GoldShortfall` toast (:309). Gem/shard spends → `ShowSpendConfirm`.
- Text on paper inside the popup uses the §1.2 rules [M29]. `CharacterUI.InkPositive`/`InkNegative` become aliases of PositiveInk/NegativeInk. Non-text InkSub uses move to `InkLine` (CharacterSceneBuilder :408 :513 :1351 :1909; EvolutionNodeView :68/:116; EvolutionTreeView :251; FriendPortrait :81; FormSkillRowView :72/:90 and CostumeItemView :112-113 chip backgrounds). NeedTag (:1296) uses NegativeInk with Cream text.
- `GoToGacha` pushes the return slot '‹ {name} 상세로' [M72].
- Captions at CharacterSceneBuilder :401-404, :449, :1160, :1341, :1349, :1389 and :1393 → 22 [M29]. Line :290 → Hint 20 (min 18). Line :449 '눌러서 자세히' is **deleted**; form cards get a 24×24 'i' or chevron corner cue instead [M80].

#### 진화 레시피 EvolutionRecipePopup [M71, M17, M75] (P1, WP-6)
- VList order: ① 필요 재료 → Materials → ② 진화 조건 → Conditions → **③ 획득처** → Sources → Skill (moved to the end, full size). Move builder lines 668-669 to after 683.
- An 8 px spacer before each section title.
- Reset to the top on Open/Refresh.
- Material cards with no 기억편 대체 button get a 20 px SubText link '획득처 ›' that scrolls to the matching source row (no navigation).
- Condition rows: the label area (~480 px) is a transparent Button with a 22 px '›' that navigates via UINav. '가속' stays the only filled button.
- **Collect button [M17]**: label '{재료} {사러 가기|모으러 가기} · {목적지}' ('수선 천 사러 가기 · 일반 상점'), autosize 30→22, single line. It stays interactable while anything is missing.
- **Source logic [M17]**:
  - `FindMissingSource` walks the materials and returns the first available source per `SourceAvailability.Check` (WP-2).
  - If none is available: the toast '{label}은(는) {reason}', and **no popup closes**.
  - Run the lock check **before** `CloseAllPopups` in CollectMaterials, `RecipeSourceView.OnGo` and `ItemSourceRowView`.
  - A Stage source gives the toast '전투(스테이지)에서 가끔 얻을 수 있어요' and does not navigate.
  - Push the return slot '‹ {form} 레시피로' before navigating [M72].
- **Source rows**:
  - Available rows first, then locked rows sorted by requiredStage.
  - Locked rows: α0.55, a 28 px lock glyph, and the SubText line '스테이지 2-10 도달 시' → **use `UnlockShort`** (M39).
  - The button is Secondary '잠김', interactable, and shows the reason toast.
  - Navigation verb '가기 ›' [M76].
- The recipe button label '진화 제작' → **'진화 제작 시작'**, and it calls `EvolutionCraftPopup.Show(formId, autoStart: true)` [M75].

#### 진화 제작 EvolutionCraftPopup [M75, M49, M62, M76] (P1, WP-6)
- `Show(formId, autoStart)`: auto-start sets the label '곧 제작해요…' and makes the button non-interactable. After 0.35 s realtime it calls `StartCraft()`. On failure it falls back to manual.
- `CloseButtonUsesDismiss = true`. Dismiss behaviour:
  - Ignore input during the progress bar.
  - During the celebration, snap to its end state.
  - After crafting, `Confirm()`.
  - Otherwise `Close()`.
- Progress uses `CharacterUI.EaseFill`.
- Copy: '{form} 해금됐어요!'.

#### 소환 결과 GachaResultPopup [M16, M49, M47, M55, M76, M26] (P0/P1, WP-6)
- Remove the full-width Gold `TreeButton` at Bottom(124,104).
- `SubActions` strip at Bottom(128,64):
  - Left: `TreeButton` as a Ghost link '진화 기억도 보기 ›', 26 Lavender, 300×64, shown whenever `_best != null` (binding kept).
  - Right: new `FormationButton` (Primary) '편성하러 가기 ›', 28, 300×64, inactive by default. After Finish(), show it only if an unlocked slot is empty **and** a result lineage is owned, unplaced and not dispatched.
- Bottom row Bottom(0,112): two **equal 452×112** buttons with a 20 gap.
  - Left: '확인' (Secondary).
  - Right: `RetryButton`, **Gold** with an Ink label and the gem CostLine. If it can't be afforded, the cost goes red but the button stays tappable and routes to the shortfall.
- `FormationButton.onClick`: Close → `GoToTab(Formation)` → `FormationPanel.PulseRosterCard(lineage)`. If AutoFormation would raise power, also `FocusTarget` the 자동 편성 button.
- **Dismiss**: during the reveal, dim tap or back calls `Skip()`. After the reveal, it closes. X stays an explicit close. Optional: stretch SkipArea over the body during the reveal [M49].
- `BalanceChip` (Gem) [M47].
- The retry goes through `ShowSpendConfirm` (skip key 'confirm.gacha10'), drawn above via SystemPopups, and the retry button is disabled until the confirm closes [M55].
- Copy: '새로운 계보의 {역할을} 만났어요!' and '…기억편으로 바꿨어요.' [M76].

#### 보상 RewardPopup [M20, M59, M83, M56] (P1, WP-4)
- 확인: if `_rewards.Count > 0 && success`, call `RewardFlyer.Fly(_rewards, _slotContent)` **before** `Close()`. Lock the button after the first click.
- A failed dungeon run with rewards also flies.
- After `Reveal()`, slots are `SetItemInfoClick()` (items only; currency and costumes are not clickable).
- `RewardSlot` prefab `GradePill`: TopLeft(8,-8), 52×24, GradeColor fill, `GradeName` 18 bold Ink, min 16. Hidden for Common and for currency [M83].
- Secondary action button (§2.9).

#### 가방 BagPopup [M70, M83] (P2, WP-4)
- Window 960×1480 → **960×1060**. The Items ScrollList keeps scrolling.
- `_emptyGuide` (owned == 0):
  - PanelLight@0.6, 888×132, top at body y 520.
  - Text '재료는 원정·던전·상점에서 모을 수 있어요' 26 Text at x 28.
  - Secondary button '원정 가기 ›' 240×72 at x −20, same route as ItemSourceRowView.
- A zero count renders in SubText. Owned counts stay Gold.
- `GradePill` as above.

#### 아이템 정보 / 획득처 rows [M17, M72, M76] (P1, WP-4)
`ItemSourceRowView`:
- Both branches read '가기 ›'.
- Lock check before closing.
- Locked rows follow the recipe-row style.
- Push the return slot from the source-row navigation.

#### 시너지 도감 SynergyCodexPopup [M44, M53, M70] (P2, WP-4)
- Tier tick labels 15→18 bold: SubText for inactive tiers, Gold for reached.
- The favoured chip reads '이번 스테이지 ×1.5', 17→**18** bold Gold, about 210 wide, under the state pill.
- Description width 520→470, ≥16 px before the chip.
- Tabs: M53 Page style. The selected count is InkSub. Each tab gets a 6 px kind-colour underline accent (an Image child per tab, inset 24, 6 px above the bottom).
- Stage slot with no gimmick: hide the ring (`_stageIcon.transform.parent`) and move `_stageText` x 554→474, width 320→400. Restore when a gimmick is active.

#### 우편 MailPopup [M20, M51, M26, M76] (P1, WP-3a)
- `MailItemView` callback → `Action<MailData, RectTransform>`.
- Claim:
  - Set `_deferRefresh = true`, then call `Mail.Claim`.
  - null result → the toast '만료된 우편이에요.' and Refresh.
  - Rewards → `RewardFlyer.Fly(rewards, btn)` (no toast). Play a 0.2 s '받음' stamp on the row label (Gold, 1.4→1 OutBack). After 0.25 s realtime, clear the flag and Refresh.
  - A notice mail with no rewards refreshes at once.
- ClaimAll: `Fly(Merge(rewards), claimAllRect)`. Empty: '받을 보상이 없어요.' If `RewardFlyer` is unavailable, fall back to the toast.
- Footer `ClaimAllButton` (`UIKit.ClaimAllButton`): BottomRight(0,0), **380×96**, 32, **Gold**, badge with the count. `SetActive(count > 0)`. Leave a 24 px gap to its left. `InfoText` width 480→440.
- The row claim button (:382) is Gold as well (repeated rows count as one Gold).
- Copy: '받은 우편이 없어요.' and '보관 기간이 지난 우편은 자동으로 사라져요.'

#### 공지 NoticePopup [M65, M50] (P2, WP-3a)
- Empty or loading state: `loading = !Game.Notice.IsLoaded`. Text '공지를 불러오는 중이에요…' while loading, '아직 등록된 공지가 없어요' when empty, 30 SubText centred.
- The builder default string matches.
- The `NoticeItem` bg gets PressScale and Btn colours [M61].

#### 확인 MessagePopup [M15, M55, M76] (P0/P1, WP-3a)
- Moved under a new **`SystemPopups`** root (§3.1). It stays in `ctx.Popups`.
- `ShowConfirm(title, body, onOk, okLabel = "확인", cancelLabel = "취소")`. Labels reset in OnClose.
- `ConfirmOpts { okLabel; ConfirmStyle {Normal, Spend, Danger}; CurrencyType? currency; double cost; string skipKey }`.
- `CostRow` (inactive): 72 tall, 112 from the bottom. Icon 48, cost 40 bold Cream, '보유 4,450 → 3,550' 26 SubText.
- `SkipToggle` (inactive): 52 tall, a 40×40 box, '오늘 다시 묻지 않기' 26 SubText. Stored in PlayerPrefs as the key = today's DayKey.
- The popup grows 560 → +84 (cost row) → +60 (toggle). The body's bottom inset grows to match.
- Buttons: 취소 (Secondary, left) / OK (right).
  - Normal and Spend use `PrimaryButton`.
  - Danger uses `DangerButton` with Cream text, disabled for 0.6 s after opening.
  - Button positions never swap.
  - The OK label is always a verb: 구매, 소환, 수선, 초기화, 건너뛰기, 해제, 원정 취소, 종료.
- **Exit dialog** [M15]: '수선점 문을 닫을까요?' / '자리를 비워도 친구들이 바구니에 선물을 모아 둬요.' Buttons '더 지킬래요' (Secondary) and '닫기' (Primary).

#### 부족 ShortfallPopup (new) [M08] (P1, WP-3a)
- `BuildPopup(820×700)`, title '{KoreanText.WithSubject(name)} 부족해요'.
- Contents: icon 72, '필요 {need:N0} · 보유 {have:N0}' (Cream 34 bold / SubText 26), '{need−have:N0} 더 필요해요' (Negative 28).
- Up to 4 source rows (96 tall, '가기 ›' Primary 160×72) from `CurrencySources.For(type)` (WP-1), routed via UINav.
  - Gem: 우편 first and highlighted **only** when unclaimed gem mail exists ('우편함에 별조각 {n:N0}개가 있어요 · 받기'), then 수선 일지, 출석부, 잔향이 던전, 상점.
  - EventToken: 이벤트, then 이벤트 상점.
- **Sources mode** (`need = 0`): hides the 필요/부족 lines; title '별조각 얻는 곳'.
- API: `UIManager.ShowShortfall(CurrencyType, double need)`.
- Callers: GachaPanel:60, the IdleChest quick-repair reason path (IdleChestSystem:399 → IdleChestPopup), ShopProductCardView, the gem chip, and the gacha link.

#### 설정 SettingsPopup [M74, M80, M18, M64, M53, M82, M22] (P1, WP-4)
- **Rows [M80]**: desc 19→**20**, min 18, box 340×40 → **380×40**, one line. RowHeight 96. Copy:
  - 잠자기 모드 '손대지 않으면 화면을 어둡게 해요'
  - 진동 '큰 순간에만 짧게'
  - 화면 흔들림 '맞거나 불 밝힐 때 흔들림'
  - 번쩍임 줄이기 '전투·뽑기·축하 연출의 번쩍임과 움직임을 줄여요 (눈 보호)' [M64]
- **Tutorial row [M74]**:
  - desc '남은 안내 말풍선을 꺼요'. Bind `TutorialRow/Desc`. When `!canSkip`, it reads '처음 안내를 모두 마쳤어요'. Button '건너뛰기' (Secondary 170×66 → 180×80 per C-17 if convenient).
  - Confirm (Danger, '건너뛰기' / '계속 배우기'), with a runtime body of at most 6 lines:
    - '남은 안내 말풍선이 더 나오지 않아요.'
    - '편성·시너지는 바로 열려요. 다른 기능은 스테이지를 깨면 열려요.'
    - Before first meeting (step ≤10 and `!FirstMeetingSummoned`): '<color=#FFB86B>첫 만남 소환(전설 우두커니 확정)을 받을 수 없어요.</color>', or with M74 3b: '첫 만남 소환은 소환이 열리면 그대로 받을 수 있어요'.
    - '졸업 선물(별조각 300)은 그대로 받아요'
    - '건너뛸까요?'
- **Audio [M18 Phase A]**: the BgmRow and SfxRow sliders become non-interactable at α0.5, with a '준비 중' pill (22 px) at the slider's right end. The 소리 and 진동 switches stay live. Rows are not hidden.
- **Danger**: '데이터 초기화' uses `DangerButton` + `ShowDangerConfirm(…, "초기화")`. Account unlink uses Danger '해제' [M55, M82].
- **Tabs**: M53 Page style (names `Tab0..3` unchanged). Segments use the M53 Option style: selected = PrimaryButton + a 2 px Cream@0.6 outline, unselected = Secondary + SubText.
- **Hit areas [M22]**: segments ≥96×64. Toggle and segment rows use the whole 100 px row as the hit area. Slider rows do not.

#### 오프라인 보상 OfflineRewardPopup (legacy) [M26, M76] (P2, WP-3a)
- The claim (:278) is Gold.
- Copy '보상은 최대 8시간까지 쌓여요.' and OfflineRewardPopup.cs:26 → 해요체.

### 2.11 Meta popups & overlays (WP-7)

#### 다녀오셨어요 ReturnPopup [M31, M49, M26] (P1)

**Buttons:**
- `DoubleButton`: BottomRight(-40,64), **500×120**, gold skin.
  - Label '두 배로 받기' 40 bold Ink, Top(10,60).
  - SubLabel 22 Ink@0.8, Top(70,34): '오늘 무료' or '두 배 수선권 1장 (보유 N)'.
  - RedDot pill 96×36 at TopRight(+10,+14): '무료' or 'x2'.
- `ClaimButton`: BottomLeft(40,64), 320×110, Secondary, '그냥 받기' 34 Cream.
- When `!canDouble`: hide DoubleButton, and ClaimButton becomes BottomCenter(0,64), 520×120, Gold, '받기' 40 Ink. NoteText is hidden. Swap at runtime with cached sprites.

**Clocks:**
- Keep the away line.
- CapText → '바구니 {elapsed} / {cap}', plus ' · 가득' when full (24 SubText).

**Grid:**
- `rows = max(1, ceil(n/6))`, `h = rows·170 + (rows−1)·16`. Set the grid height to h.
- lineRoot y = −614 − h − 40 − 41.
- Window height = 1240 − (356 − h), i.e. **1054** for 1 row.

**Speaker:** name colour `Lerp(mainColor, Ink, 0.55)`, checked against ≥4.5:1 (else Ink), 22 bold. Optional 10 px mainColor dot.

**Dismiss [M49]:**
- `CloseButtonUsesDismiss = true`.
- Dim tap, X or back: ignored while claiming. Otherwise Close, plus the toast '선물은 수선 바구니에 그대로 있어요' when the basket had rewards.
- `_flown` guard: a forced close mid-claim still calls `FlyRewards` in OnClose.

#### 수선 바구니 IdleChestPopup [M32, M28, M30, M08, M55] (P1)
- **Quick button sub-line**:
  - free: '무료 · 오늘 {left}회 남음'
  - paid: '[star] {cost} · 오늘 {left}회 남음'
  - none left: '오늘 모두 썼어요'
  - The button stays Primary. When free, add a `FreeRibbon` (Gold, '무료' Ink 20 bold, 76×30) at TopLeft(-8,8). 받기 stays the only Gold.
- **Dynamic rhythm**:
  - `rows = max(1, ceil(activeLines/6))`, `gridH = rows·170 + (rows−1)·16`.
  - footnote y = 364 + gridH + 14. QuickSection y = footnote + 44. QuickSection shrinks to 180 when the ticket button is hidden.
  - Window height keeps 받기 24 px below QuickSection: 1320 → about 1124 at 1 row.
  - Recompute only on Refresh(force) or a row-count change.
- **Auto-close**: after a successful 받기, if there is no free quick idle and no tickets, Close() 0.8 s (realtime) after Celebrate. Cancelled by any tap or OnClose. Never after a quick idle or ticket use.
- The paid quick repair uses `ShowSpendConfirm(Gem, cost, "수선")` (IdleChestPopup:343). A short pull opens ShortfallPopup.
- Reward-list text: name and amount joined with U+00A0 ('단추 3.60A'), 2-line height for the status and 빠른 수선 lines [M28]. Amounts use `FormatCompact` on chips and `FormatCurrency` in text. Rates use the §1.10 rule; the trailing '*' is at 70% size or dropped [M30].

#### 출석부 AttendancePopup [M69, M59, M53] (P1)
- Window stays 980×1300.
- `BuildAttendanceCellTemplate(grid, cellH)`:
  - Starter: grid Top(320,196), cells 128×**184**.
  - Monthly: 128×150.
- Cell:
  - DayLabel Top(6,28), 22 bold.
  - Icon tile At(TopCenter,(0,-36),(96,H−76)), icon 64 (starter) or 52 (monthly).
  - **Amount below the tile**: Bottom(6,30,4,4), 22 bold Ink, Fit 16, '×N' / '×15+'.
  - Stamp centred on the tile.
- Highlight days: the day-label row becomes a Gold pill Top(-8,32,-6,-6) reading '{day}일 확정!', 20 bold Ink, Fit 16. DayText is hidden. The reward name shows in the amount slot.
- Status text: hidden while check-in is possible. Done: '내일 또 만나요 · 다음 도장까지 {ClockMono}'. Tampered: the existing message.
- **Auto-stamp**: `OpenAuto()` waits for the open tween plus 0.5 s (realtime), then calls `CheckIn()` if still possible. A tap cancels the wait and stamps immediately. `MetaPopupPresenter.Show(Attendance)` uses OpenAuto; the top-bar button uses Open. After an auto-stamp, `CheckInButton` reads '확인' and closes (name kept).
- Cells get PressScale [M61]. Tapping a non-today cell opens a small 'N일차 보상' card listing all rewards (MetaRewardChip 96×96), replacing the toast. A 22 px Lavender 'i' badge marks cells with more than one reward [M59].
- Tabs: M53 Page.

#### 수선 일지 JournalPopup [M60A, M51, M53, M62, M72, M61] (P1)
- **Chest captions** (`BuildChestTemplate`): child `Reward` on the root (not on Visual), At(TopCenter,(0,24),(98,30)), HLG spacing 4. Contents:
  - `RewardIcon` 24.
  - `RewardAmount` 20 bold Cream, Fit 16.
  - `RewardMore` '+1' 16→**18** SubText.
  - Colours: Cream when not reached, Gold when reached and unclaimed, SubText@0.6 (icon @0.5) when claimed.
  - If it clips, use y 20.
- Chests get PressScale [M61].
- Footer `ClaimAllButton` (direct child of Body, coach path) via `UIKit.ClaimAllButton`: BottomRight, 380×96, 32 Gold, with a badge count instead of '(N)'.
- `MissionRowView` fill eases [M62]. Row '가기 ›' [M76].
- '가기' to a tab pushes the return slot '‹ 수선 일지로' [M72].
- Tabs: M53 Page. Delete the raw TabOff colour.

#### 새 기능 카드 FeatureUnlockCard [M78, M26, M76] (P2)
- The medallion already uses FeatureArt. Note for the graphics workflow: `Art/Icons/journal.png` and `attendance.png` are missing.
- On decline ('나중에', '좋아요', X) **only**: `RewardFlyer.FlyTo(sprite, illustration, MetaNavigator.FeatureSlot(id), onArrive)`. On arrival: punch 1.15, `MetaSparkleBurst.BurstAt(target, 8, Lamp, 0.8)`.
- Skipped during FTUE and for Graduation. When the card also grants a gift, start 0.25 s after the gift fly.
- The next queued popup is held until onArrive (≤0.8 s).
- '가 보기' → Primary, label '가 보기 ›' [M26, M76].

#### Startup chain [M50, M48] (P1)
- At most 1 notice per launch: `NoticeSystem.GetStartupNoticeToShow()` returns the first unread popup notice, or null when hidden today.
- The `MetaPopupPresenter:178` guard uses it.
- `WaitClosed` adds 0.18 s realtime.
- Result: at most 3 modals (복귀, 출석부, ≤1 공지). Later launches the same day show no notice once it is read.

#### 잠자기 모드 SleepModeView [M01, M15, M68] (P0)
- M01 handlers (§2.0).
- Back handler registered OnEnable: `() => { if (_asleep) { Wake(); return true; } return _overlay.activeSelf; }`.
- The overlay's offsets extend 400 px past both insets.

#### 강해지기 sheet (new) [M13 phase 2] (P1): see §2.1.5.

### 2.12 Tutorial / FTUE (WP-8 unless noted)

| Item | Spec | Id |
|---|---|---|
| Story intro skip | A `Safe` child (SafeArea). `Skip` Button at At(TopRight,(-36,-44),(196,64)), pivot (1,1), bg (0.06,0.04,0.14,0.6), '건너뛰기 ›' 26 Cream@0.9, with its own CanvasGroup `_skipGroup`. Alpha = `clamp01((total−1.0)/0.3)`, interactable only above α0.5. `Skip()` → `BeginClose()`. Back calls Skip (via the M15 stack; until then, Escape in Update). | M79 |
| Tap hint | Bottom(140,44), 28 Cream, on a 360×44 band (0.06,0.04,0.14,0.45) or outline 0.15. Text '화면을 눌러 주세요  ›'. Alpha `0.8+0.2·sin(t·4.49)`; it fades in over 0.3 s after the typewriter finishes. | M79 |
| Intro back | Back handler while `_page ≥ 0`: honour `_closing` and the 0.35 s debounce; show the full text first, then `Next()`. | M15 |
| First-meeting card back | Handler while shown: toast '먼저 새 친구를 만나 볼까요?' (throttled to 2 s), consumes the press. | M15 |
| Coach bubble (no target) | `[SerializeField] RectTransform _freeAnchor` = ctx.ContentArea. Bubble top = contentTop − 28, i.e. screen ≈996–1146. Fallback to `_topBubbleOffset` when the content area is hidden. Text autosize off, 30 px, lineSpacing 4. Height = clamp(preferred + 56, 150, 220), width 720. `_charsPerSecond` 48→60. Targeted placement unchanged. | M42 |
| FTUE step 6 reward | `shop.rewards.Add(Gold(250))` on the step after the first boss. Step 7 text: '선물 받은 단추로 짝짝이를 레벨 5까지 올려 보세요\n레벨은 짝짝이의 모든 형태가 함께 나눠요'. No index shifts. Update IDLE_RESEARCH §7 row 6. | M06 |
| Post-graduation mail hint | After step 16, if the mail has unclaimed rewards: a 3 s hint-only pulse on `CoachTargets.MailButton` + the toast '개업 선물이 우편함에 와 있어요' (FtueDirector). | M06 |
| FTUE expedition | Solved by M05 (WP-2/WP-4): no FTUE content change. ValidateFtue checks owned ≥1 at the Expedition step. | M05 |
| Tutorial dim | Confirm the TutorialSceneBuilder dim/spotlight (~:528) is canvas-wide; if not, extend it 400 px like the sleep overlay. | M68 |
| Art helpers | `TutorialSceneBuilder.Art.cs:122` min = `Max(16, fontSize·0.7)`. `GoldButton` → `SceneParts.SkinButton(Gold)`. | M23, M25 |

---

## 3. Cross-cutting behaviour

### 3.1 Layer order (canvas siblings, bottom → top)
`Background` → `SafeArea` (TopBar, StageArea incl. Battle/MetaHud/ReturnChip, ContentArea, TabBar) → `Popups` → **`TooltipLayer`** (new, SafeArea component) → `TutorialOverlay` → `FanfareLayer` (RewardFlyer, celebrations) → **`SystemPopups`** (MessagePopup) → **`ToastLayer`** (new, stretched, SafeArea component; hosts the toast).

*Resolution:* M14 said to parent the toast to SafeArea. SafeArea sits **below** Popups, so the toast gets its own safe-area-aware layer, built last.

### 3.2 Android back [M15, M49]
`UIManager.OnBack` resolves in this order:
1. Back-handler stack, newest first: `PushBackHandler(Func<bool>)` / `RemoveBackHandler`.
   - Registered handlers: SleepModeView, StoryIntroView, FirstMeetingCardView.
   - None for LevelUpFanfare or CelebrationFx.
2. `CloseTop()`, which now calls `RequestDismiss()` on the top popup.
3. The exit confirm (§2.10).

A popup that absorbs back still returns true, so the quit dialog never appears behind it. A second back while the exit confirm is open closes it. No double-back-to-quit.

### 3.3 Dim tap and dismiss policy [M49]
- `UIPopup`:
  - `protected virtual void OnDimOrBack() => Close();`
  - `public void RequestDismiss() => OnDimOrBack();`
  - `protected virtual bool CloseButtonUsesDismiss => false`.
- The Dim button calls RequestDismiss.
- Overrides:
  - GachaResult: skip, then close.
  - Return: ignore while claiming; toast on close.
  - EvolutionCraft: ignore, snap, confirm, close.
- **Tools must not loop on CloseTop.** Add `UIManager.CloseAllImmediate()`, which deactivates every popup without dismiss logic or animation. Use it in AutoShot and in tests that "close everything".

### 3.4 Popup transitions and stacked dim [M48]
- `SceneParts.BuildPopup` adds a CanvasGroup to `Dim` (raycast stays on). It binds to `UIPopup._dimGroup`, which may be null and falls back to the root group.
- **Open**: if a close animation is running, stop it and set blocksRaycasts true. Then play the §1.8 open timings. `dimTarget` = 1 for a single popup, **0.4** when stacked (decided in `UIManager.OnPopupOpened`, stack count > 1). Lower dims are never changed.
- **Close**: synchronous bookkeeping as today (IsOpen=false, OnPopupClosed, OnClose, Closed). Then blocksRaycasts false and the exit animation, then SetActive(false) with alpha and scale reset. Already inactive or Reduced → deactivate immediately.
- QA cases:
  - MessagePopup OK opens another MessagePopup in the same frame.
  - Back with 2 popups stacked.
  - Startup chain return → attendance → notice.
  - GachaResult + FTUE steps that open a popup right after another closes.

### 3.5 Toasts [M14]

**Build:**
- `Toast` in `ToastLayer`, last sibling, TopCenter pivot (0.5,1).
- 880 wide. ContentSizeFitter height 88 min to about 140 (2 lines). Padding 28 left/right, 18 top/bottom.
- Text 28 Text, wrapping, **no autosize**, line 2 Ellipsis.
- Background `Panel`@0.95 rounded, with a 2 px GoldAccent@0.5 outline.
- Kind marker: a 40×40 icon at left 24 (Info = Lavender dot, Success = Positive check, Error = Negative '!'), plus a 6 px left bar on Error only.

**Position** (resolved on every show):
- (a) No popup: y = −480 from the safe-area top.
- (a2) No popup and the MAIN quick rails visible (`UIFrame.SheetProgress` < 0.999 and `QuickRail_L` or `QuickRail_R` has an active `QuickIconView` cell): width clamp(areaWidth − 2×128, 440, 880), so x 128–952 on 1080. With the sheet fully open or under a popup it stays 880 (x 100–980).
- (b) A popup with `windowTop` ≥160 below the safe top: the toast's bottom edge sits 20 px above the window.
- (c) Otherwise (tall popups): inside the window. Top 12 px below the window top, width `min(720, windowWidth − 240)` so the X stays visible.

**API:** `UIManager.ShowToast(string msg, ToastKind kind = Info)`. The 1-argument overload is kept.
- Convert 부족해요 / 잠겨 / 아직 열리지 / 만료 to **Error**.
- Convert grants and claims to **Success**.

**Queue:**
- FIFO of 3. When full, drop the oldest non-error.
- The same text and kind within 1.5 s is not enqueued. Instead, extend the visible toast to ≥1.0 s remaining and punch it.
- Hold: Info/Success 1.4 s, Error 1.8 s, +0.04 s per character beyond 20, cap 3.0 s. When another toast is queued, cut the current one to max(remaining, 0.6 s).
- Error plays the Deny SFX hook plus a Light haptic, at most once per 0.3 s.

**Mail rewards:** until RewardFlyer covers them, show at most 3 items joined with ' · ' plus ' 외 N개' (Success).

### 3.6 Info tooltips (tap / long-press) [M59]
- **Items** reuse `ItemInfoPopup`:
  - RewardPopup slots after Reveal.
  - `MetaRewardChip` (Button + PressScale in the builder template): item → ItemInfoPopup; currency → InfoTooltip.
  - Attendance cells (§2.11).
- **`InfoTooltipView`** (new, WP-1 runtime; instance under `TooltipLayer`, WP-3a) for non-item info: currency chips, trait chips, synergy icons.
  - Layout: width 360–520, padding 20/16, Panel@0.96, 2 px GoldAccent border, 16 px arrow. Title Cream 26 bold, body SubText 22 (≤3 lines), optional source line Lavender 20.
  - Gestures: tap shows it. A 0.35 s long-press also shows it on elements that already have a Button. `IBeginDrag` cancels.
  - It closes after 3 s or on any tap, through a full-screen transparent catcher that exists only while it is open.
  - Position: above the target, flipping below when within 24 px of the safe top. Clamped to the safe area with 16 px margins.
- Wire it to `SceneParts.CurrencyChip` (text from `CurrencySources`) and to the CharacterSceneBuilder `Chip()` trait/synergy chips **on the detail and formation screens only**. List cards keep their card-level tap.

### 3.7 Confirm on spend [M55, M56]

| Action | Confirm | Notes |
|---|---|---|
| 1회 소환 | none | keeps the FTUE Pull(1) flow |
| 10회 소환, 10회 더 소환 | `ShowSpendConfirm` with skipKey `confirm.gacha10` (once per day) | button handler only |
| Shop: gem-priced single item | `ShowSpendConfirm`, no skip | pre-check first |
| Shop: gold-priced single item | none | 0.15 s tap lock |
| Shop: costume | `ShowSpendConfirm` → RewardPopup '의상 획득!' + '바로 입혀 보기' | |
| Paid quick repair | `ShowSpendConfirm(Gem, "수선")` | |
| Recipe / material / shard / gem spends | `ShowSpendConfirm` with their currency | |
| 데이터 초기화, 튜토리얼 건너뛰기, 계정 해제, 원정 취소 | `ShowDangerConfirm` (0.6 s OK lock) | labels 초기화 / 건너뛰기 / 해제 / 원정 취소 |
| Quit | Normal, '닫기' | §2.10 |

Never open a spend confirm the player cannot afford. Run the affordability check first (§3.8).

### 3.8 Shortfall routing [M08, M07]
- **Gold (단추)**: never a popup.
  - `MetaText.GoldShortfall(need, have)` → '단추 {need−have:N0}개가 더 필요해요 · 약 {eta}'. The ETA appears only when under 10 minutes.
  - If the idle chest holds at least the gap, append ' · 바구니를 받아 보세요' and punch IdleChestWidget once (0.08/0.22).
  - Throttled to 1.5 s, Error kind.
  - During a hold: stop the hold, Deny-shake, and toast once.
- **Gem / EventToken**: `UIManager.ShowShortfall(type, need)`.
- **Callers**:
  - GrowthPanel.LevelUp/LevelUpMax, GrowthLineageCardView:54, CharacterDetailPopup:309 (gold);
  - GachaPanel:60, ShopProductCardView, IdleChest quick (gem);
  - the gem chip and the gacha link (sources mode).

### 3.9 Claim-all [M51, M20, M57]
- `UIKit.ClaimAllButton(parent, w, h, fontSize, out label, out Badge badge)`:
  - The name is always `ClaimAllButton`. Gold skin, '모두 받기' Ink bold, Fit 22..fontSize.
  - Badge at the top-right (-6,-6) holds the count (no '(N)' in the label).
  - `disabledColor` white. The rule is `SetActive(count>0)`, never `interactable=false`.
  - Pop scale 0.85→1 over 0.18 s when the count goes 0→>0.
- **Placement:**
  - Tab panel header right slot: 240×64, 28, 88 hit (원정).
  - Popup footer: BottomRight, 380×96, 32 (수선 일지, 우편).
- Every claim-all flies its merged rewards (§3.12).
- Expedition claim-all keeps the RewardPopup, which now flies on 확인. After it, cards show '다시 보내기' (M57). '모두 다시 보내기' is a follow-up.

### 3.10 Button states and press [M07, M25, M61]

**`CostButtonView`** (WP-1) on each cost button root.
- Fields: `_button`, `_skin`, `_label`, `_costText`, `_costIcon?`, `_costPill?`, `_surface`. Caches the normal look in Awake.
- **Affordable**: normal skin. Cost text is Text on Dark/Primary, Ink on Gold/Parchment. Pill hidden, icon α1.
- **Short**:
  - Keep the skin, multiplied by (0.78,0.78,0.82,1). Label α0.9.
  - `CostPill` (RoundSprite, height 40, padding 12, `CostPillBack`) behind the cost.
  - Cost text = `CostShort(surface)`. Icon α0.5.
  - **Stays interactable.** A tap runs `Deny()` (the §1.8 shake on the label/cost container) plus the Deny SFX hook, then routes per §3.8.
  - No '부족' text pill.
- **Unavailable**: `interactable=false`, skin = RoundSprite `Disabled`, label `DisabledText` with the `unavailableLabel` text ('최대 레벨' / '품절' / '보유 중' / '받을 보상 없음'), cost row hidden. `disabledColor = white` on these buttons only (`UIKit.CostBtn`).
- State is re-evaluated only from the existing Refresh calls. No polling.
- **Call sites:**
  - GachaPanel:117, GrowthPanel:191, GrowthLineageCardView:47, ShopProductCardView:50, GachaResultPopup:70, CharacterDetailPopup:264/282.
  - Expedition '모두 받기' / '진화 제작' → Unavailable.
  - EchoCardView:140: colour only (pill + CostShort); keep its own dim and Deny.
- New `CostPill` children go only under existing cost rows; no renames.
- **Acceptance**: cost text ≥4.5:1 in every state on shots 12, 15, 16, 17, 18, 19 and 20.

**Disabled (non-cost factory buttons):** §1.6.

**Press (`PressScale` rewrite, WP-3b):**
- Implements down / up / exit / enter. Scales **its own transform** using the §1.8 timings. Tracks `_held`.
- Disables itself when idle and snaps on OnDisable. Keeps the M01 focus handler.
- Apply to: AttendanceCell root, MissionChest root, RecommendedPower pill, NoticeItem bg, the SynergySummary chip, and the Lamp background (M37).
- Coach highlights are unaffected (scale only).

### 3.11 Hold-to-repeat [M19, M34] (WP-3b rewrite)
- `RequireComponent(Button)`.
- **On OnPointerDown**: cache `_ev` (PointerEventData) and the press position.
- **On the first repeat**: set `_ev.eligibleForClick = false`, so releasing does not add an extra level.
- **Cancel when any of these happens:**
  - pointer up, exit or OnDisable;
  - `_ev.dragging`, or the pointer moves more than `EventSystem.pixelDragThreshold` (the ScrollRect keeps scrolling; never implement IBeginDragHandler);
  - `OnApplicationFocus(false)` or `OnApplicationPause(true)` (only the hold stops, not the game).
- **Timing:** §1.8 (delay 0.35; 0.45 on lineage cards).
- **`Func<bool> Step`:**
  - While holding, `Step()` is called instead of `onClick`; a tap still uses onClick.
  - When Step returns false, the hold ends and `event Action Denied` fires once.
  - `TryStep()` methods return false **without toasting** during a repeat: `GrowthPanel.LevelUp(1)`, `GrowthLineageCardView.LevelUp`, `CharacterDetailPopup.LevelUp`.
  - The Denied handler toasts once and plays the shake.
- **Combo pill:** created lazily at runtime.
  - `HoldCombo`: TopCenter(0,+26), 112×44, Gold rounded, Ink 26 bold '+N', raycast off, ignoreLayout. On the 244/212 lineage cards it sits at TopCenter(0,+18).
  - Shows from the 2nd successful repeat and punches 1.15 per tick.
  - After release: held 0.4 s, then fades over 0.25 s.
- **No per-tick haptics**, and never `Handheld.Vibrate`.
- **Where it applies:** LevelUp1Button, the lineage card LevelUpButton (add the component at MenuSceneBuilder ~187), and the detail LevelUpButton. x10 and MAX stay tap-only.

### 3.12 Reward flights, counters, count-ups, fills [M20, M21, M56, M62, M63, M78]
- **`RewardFlyer` (WP-3b):**
  - `static double PendingOf(CurrencyType)`. It increments when a request is accepted. When the request cap is hit, the amount merges into the last same-destination request (never dropped).
  - Each `Arrive` decrements. When nothing is in flight, pending snaps to 0.
  - Safety drain: if pending > 0 and nothing has landed for 1.2 s, set it to 0. Also clear it on OnDisable, OnDestroy and scene change.
  - `event Action<CurrencyType> PendingChanged`.
  - `FlyTo(Sprite, from, to, onArrive)` (M78).
- **Who flies:**
  - mail (§2.10), RewardPopup on 확인, shop single buys, unlock-card decline;
  - plus the existing ones: stage clear, journal, attendance, idle chest, return, FTUE.
- **Count-ups:** currency 0.35 s, power 0.5 s (existing), pity 0.35 s.
- **Fills:** `UIMotion.EaseFill` in GuideQuestBarView and MissionRowView; `CharacterUI.EaseFill` in EvolutionCraftPopup and RecipeConditionView. Snap on rebind or enable. The idle-chest bar is unchanged.
- **Power-decrease float:** only after a formation drop (M46).

### 3.13 Red dots and badges [M09, M82, M16, M32, C-10]
1. **Selected tab never shows a badge.**
   - `TabGateView.MarkSeenTab(tab)` is called from Start, from the end of Refresh (on Features.Changed, when SelectedIndex ≥ 0) and from `MainTabController.Reselected`.
   - Guard against re-entry with `_markingSeen`.
   - The tab RedDotView hides while its tab is selected.
2. **Skip:**
   - `TutorialSystem.Skip` keeps `ForceUnlock(f,false)`, then MarkSeen on every forced feature except the current guide-quest target (if there is none, keep TabGacha).
   - No stage-based expiry.
3. **NEW vs dot:**
   - `Badge.SetNew(bool)` shows the NEW pill (§1.5). RedDotView picks it for FeatureDot leaves that have no claimable child.
   - Red dot and count badges are for claimables only: mail, journal, attendance, basket, expedition done, evolution/resonance/costume, dungeon tickets, `tab.formation.empty`.
4. **HUD toggles** (HudSpeed, HudAuto, LampPolicy) lose their FeatureDot registration. The unlock card plus one 2 s MetaPulse is enough. The FeatureLockCover newDot clears on the first tap.
5. **Notice**: `SetDot(UnreadCount > 0)` on SettingsButton (M10 removes the top-bar button).
6. **Gacha discount pill**: GoldAccent with Ink. Never RedDot.
7. **Sizes, outline and motion**: §1.5 and §1.8. Count/NEW text is white bold with a `BadgeTextOutline` 0.2 outline [M82]. Badge circles under 40 px with text grow to 40.
8. **Basket**: see §2.1.10.
9. **Acceptance**: after a fresh save → tutorial → skip, shot 05 shows ≤4 badges. The selected tab has none in 12, 13, 17 and 29. A feature that unlocks while its tab is open clears in the same frame.
10. (P2, C-10) Route ad-hoc dots through `SceneParts.BuildBadge`: CharacterSceneBuilder.Dot, the MetaSceneBuilder lock-cover NewDot and chest Dot, the IdleSceneBuilder UnreadDot and NewBadge.

### 3.14 Navigation: deep links, return chip, source availability [M72, M73, M17, M43]

**`UINav` (WP-3a):**
- `PushReturn(label, Action reopen)` / `TryPopReturn()` / `ClearReturn()` / `event ReturnChanged`, holding one slot.
- `OpenCharacter(lineageId, int tab = -1)`, with `CharacterRequested : Action<string,int>`.
- `OpenSynergy(id, SynergyKind? kindHint = null)`.

**Return slot pushers:**
- EvolutionRecipePopup.CollectMaterials, RecipeSourceView, ItemInfoPopup source rows: '‹ {form} 레시피로', which reopens the detail and then the recipe.
- Journal 가기 to a tab: '‹ 수선 일지로', which reopens and reselects the tab.
- CharacterDetailPopup.GoToGacha: '‹ {name} 상세로'.
- MetaNavigator guide-quest and unlock-card jumps do **not** push.

**Return chip lifetime:** until it is tapped (clear first, then reopen), × is tapped, a new push replaces it, or the user taps a different tab-bar button. There is no timer. If reopen fails, toast and clear.

**MetaNavigator.OnFeature:**
- Evolution → `OpenCharacter(PickLineage(LineageEvolution), TabTree)`.
- Costumes → `OpenCharacter(PickLineage(LineageCostume), TabCostume)`.
- Codex → stay on the 친구들 tab.
- `PickLineage`: the first owned lineage with that dot on, otherwise GuideLineage if owned, otherwise the first owned lineage, otherwise null.
- `MetaDataBuilder.PopupOpenedBy` matches this (Evolution/Costumes → CharacterDetailPopup; Codex → null).

**`SourceAvailability.Check(ItemSource)` (WP-2):**
- Expedition: feature unlocked and zone unlocked. Reason `UnlockSentence`.
- Dungeon: feature unlocked.
- Shop/Event: unlocked and stock remaining. Reason '오늘 상점 수량을 모두 샀어요'.
- Stage: always available, ranked last.

**Content (WP-2, ItemContent):**
- 수선 천: [Shop("normal","일반 상점"), Expedition("exp_1_4"), Expedition("exp_1_1"), Expedition("exp_2_4"), Stage()].
- Audit the other 8 materials against the drop tables.
- Add an editor warning for a source zone that doesn't drop the item.

### 3.15 Haptics and sound hooks [M18] (WP-2 core; hooks in their owners)

**Phase A (ship now):** `Scripts/Core/Haptics.cs` with `Play(Light|Medium|Heavy)`.
- Android: `VibrationEffect.createOneShot` with Light 10 ms / amp 50, Medium 18 / 110, Heavy 30 / 200.
- SDK < 26: `vibrate(ms)` for Medium and Heavy only.
- Add the VIBRATE permission via `Plugins/Android/AndroidManifest.xml`.
- iOS and editor: no-op.
- Gated by `GameSettings.Haptics`. At most one pulse per 80 ms and one Heavy per 400 ms.
- **Never on plain taps.** Hooks:

| Event | Haptic | Owner |
|---|---|---|
| Lamp lit | Medium | WP-5 |
| Boss kill | Heavy | WP-5 |
| High-grade gacha reveal | Heavy | WP-6 |
| Level-up fanfare | Medium | WP-3b |
| Denied purchase / locked feature (FeatureLockCover, TabGateView, EchoCardView.Deny, CostButtonView.Deny, toast Error) | Light | WP-7 / WP-5 / WP-1 / WP-3b |

Settings honesty: §2.10.

**Phase B (same sprint, once clips exist):**
- Generate clips via Higgsfield `generate_audio`: soft bell, wood and paper timbres; ≤0.3 s (LevelUp/GachaRare ≤1.2 s); −14 LUFS. This is a content task.
- `Scripts/Core/UISfx.cs`:
  - A `[Audio]` DontDestroyOnLoad object with 1 BGM source and 8 SFX sources.
  - A `UISfxTable` ScriptableObject keyed {Tap, Tab, PopupOpen, PopupClose, Deny, CoinLand, GemLand, LevelUp, Stamp, Claim, GachaFlip, GachaRare}.
  - BGM gain = BgmVolume × SoundOn. SFX gain = SfxVolume × SoundOn. `GameSettings.Apply` pushes both.
  - Same key at most once per 40 ms, ≤3 voices per key, pitch ±5%. CoinLand rises +3% per landing within 0.3 s, capped at +18%. A null clip is a silent no-op.
  - **No mute on focus loss.** BGM fades 0.2 s only on `OnApplicationPause(true)`.
- Hooks:
  - PressScale: Tap at 0.5 volume, on up-inside within the drag threshold.
  - MainTabController: Tab.
  - UIPopup: Open/Close.
  - RewardFlyer.Arrive: Coin/Gem.
  - LevelUpFanfare: LevelUp.
  - Attendance stamp: Stamp.
  - GachaCardView.ShowFront: Flip / Rare.
  - Claim buttons: Claim.
  - Deny sites: Deny.
- Then re-enable the sliders, remove the pills, and preview at most one SFX per 150 ms while dragging.

### 3.16 Focus and background (user request)
§2.0 is normative.
- Focus loss cancels: holds (HoldRepeatButton), presses (PressScale), aims (EchoDeckView), and the FX-quality sampler.
- Focus loss **never** pauses, mutes, dims or throttles on desktop.
- Every new component that keeps transient pointer state must add the same local `OnApplicationFocus(false)` cancel.

### 3.17 Instructional hints [M80]
- At most **one** instructional line per panel. Hint style 20 (min 18), SubText.
- Gesture-teaching lines hide for good after the player has done the gesture (Formation <3 placements; deck caption per M36).
- State and feedback lines stay.

---

## 4. Implementation plan

### 4.0 Waves

```
WP-0 Focus hotfix (M01) ── ships first, alone, sequential
        │
Wave 1 ─┬─ WP-1 Kit & tokens & project setup
        └─ WP-2 Systems, data, content, EditMode tests
        │
Wave 1b ┬─ WP-3a Shell & popup infra   (needs WP-1 tokens/enums)
        └─ WP-3b Common components     (needs WP-1, WP-2 Haptics)
        │
  ── gate: graphics-polish + attack-FX workflows merged; scene rebuild OK; smoke tests green ──
        │
Wave 2 ─┬─ WP-4 Menu (tabs, settings, bag, shop, expedition, formation, synergy)
        ├─ WP-5 Battle HUD
        ├─ WP-6 Character & gacha
        ├─ WP-7 Meta
        └─ WP-8 Tutorial
        │
Wave 3 ── WP-9 Verify (AutoShot, PlayMode/EditMode UI tests, harness)
```

**Why the task's package list was adjusted.** The task partitioned the work as kit / menu / battle / character / meta / tutorial(+Common). That leaves no home for the many system-level changes (Systems/Core/Data/Content), and it puts **cross-package runtime APIs** inside Wave 2 packages: `UIManager`/`UIPopup`/`UINav`, `HoldRepeatButton.Step`, `RewardFlyer.PendingOf/FlyTo`, `Badge.SetNew`, `ToastKind`. Wave 2 agents would then fail compile checks while waiting on each other. So:
- systems became **WP-2**;
- the popup/nav shell and `Scripts/UI/Common` became **WP-3a/3b** in Wave 1b;
- tutorial became **WP-8**.

Ownership stays strictly **per file** (§4.1). Every package compile-checks with the dotnet check (memory note) before handing off. Scene rebuild and batch tests run on the `verify_proj` copy (verify.ps1), never in the open editor.

### 4.1 File ownership (one owner per file)

| Package | Files |
|---|---|
| WP-0 | `Editor/IdleProjectSetup.cs` (BuildAll line), `Scripts/UI/Meta/SleepModeView.cs`, `Scripts/UI/Common/HoldRepeatButton.cs`, `Scripts/UI/Common/PressScale.cs`, `Scripts/UI/Battle/EchoDeckView.cs`, `Scripts/UI/Battle/BattleView.Enemy.cs`. These are **handed back** to WP-1, WP-7, WP-3b, WP-3b, WP-5 and WP-5 after WP-0 merges. |
| WP-1 | `Scripts/UI/UITheme.cs`, `Editor/UIKit.cs`, `Editor/SceneParts.cs`, `Editor/SceneBuildContext.cs`, `Editor/IdleProjectSetup.cs`, `Assets/TextMesh Pro/Resources/TMP Settings.asset`, `Fonts/NotoSansKR-Bold.otf` (+SDF, +Outline.mat), new `Scripts/UI/UITokens.cs`, new `Scripts/UI/UIFormat.cs`, new `Scripts/UI/Common/{CostButtonView, ButtonLabelDim, CurrencyStripView, InfoTooltipView, CurrencySources}.cs` |
| WP-2 | `Scripts/Core/**` (GameSettings, NumberFormatter, SaveData*, Game.cs, new Haptics.cs, new UISfx.cs), `Scripts/Systems/**` (BattleSystem, ExpeditionSystem, FormationSystem, RedDotSystem, FeatureGateSystem, TutorialSystem, MetaSession, NoticeSystem, ShopSystem, DungeonSystem, DailyClock, PowerSystem, CharacterSystem, IdleChestSystem, RewardService, MetaRewardService, MetaText, new SourceAvailability), `Scripts/Data/ShopDatabase.cs`, `Editor/Content/**` **except** MetaContent.Ftue.cs, `Editor/MetaDataBuilder.cs`, `Tests/EditMode/**` (existing files), `Plugins/Android/AndroidManifest.xml` |
| WP-3a | `Editor/IdleSceneBuilder.cs`, `Scripts/UI/UIManager.cs`, `Scripts/UI/UIPopup.cs`, `Scripts/UI/UINav.cs`, `Scripts/UI/Main/MainTabController.cs`, `Scripts/UI/Popups/{MessagePopup, MailPopup, NoticePopup, OfflineRewardPopup}.cs`, new `Scripts/UI/Popups/ShortfallPopup.cs`, `Scripts/UI/Items/**` (MailItemView, AccountLinkRow, ItemSlotView, …) |
| WP-3b | `Scripts/UI/Common/**` existing files (HoldRepeatButton, PressScale, ToastView, RewardFlyer, StageClearRewardFlyer, Badge, LevelUpFanfareView, CelebrationFx, PowerUpFloatView, SafeArea, UIFind, UIListPool) + new `Scripts/UI/Common/ReturnChipView.cs` |
| WP-4 | `Editor/Builders/MenuSceneBuilder.cs`, `Editor/Builders/SettingsSceneBuilder.cs`, `Scripts/UI/Main/{GrowthPanel, TopBarView}.cs`, `Scripts/UI/Popups/SettingsPopup.cs`, `Scripts/UI/{Shop, Expedition, Formation, Synergy, Bag}/**` |
| WP-5 | `Editor/Builders/BattleSceneBuilder.cs`, `Scripts/UI/Battle/**` (incl. SynergyHudView, EchoCardView, EchoDeckView, BattleHud, BattleBannerView, LampButtonView, SkillCalloutView, BattleView*.cs (only the lines named here), FxQualityMonitor, BattleEase). **Excludes** `BattleFx*`, `FxGalleryView`, `AttackFxBuilder`, `EnemyFxBuilder`. |
| WP-6 | `Editor/Builders/CharacterSceneBuilder.cs`, `Scripts/UI/Characters/**`, `Scripts/UI/Main/GachaPanel.cs`, `Scripts/UI/Popups/GachaResultPopup.cs` (their builders live in CharacterSceneBuilder) |
| WP-7 | `Editor/Builders/MetaSceneBuilder.cs`, `Scripts/UI/Meta/**` |
| WP-8 | `Editor/Builders/TutorialSceneBuilder.cs`, `Editor/Builders/TutorialSceneBuilder.Art.cs`, `Scripts/UI/Tutorial/**`, `Editor/Content/MetaContent.Ftue.cs` |
| WP-9 | `Verify/Runtime/AutoShot.cs`, `Verify/Editor/**`, `Tests/PlayMode/MainSceneSmokeTests.cs`, new `Tests/EditMode/UiSceneLintTests.cs`, new `Tests/PlayMode/UiBehaviourTests.cs`, `scratchpad/verify.ps1` |

### 4.2 Cross-package contracts (provider → consumers)

| Provider | API (exact names) | Consumers |
|---|---|---|
| WP-1 | `UITheme.{PositiveInk, NegativeInk, InkSub, InkLine, PrimaryButton, DangerButton, Disabled, DisabledText, CostPillBack, BadgeTextOutline, TabSelected, CostShort(CostSurface)}`, `TabStyle.Apply(Graphic, TMP_Text, bool, TabVariant, Graphic accent=null, Color? accentColor=null)` | all UI packages |
| WP-1 | `UIType.*`, `UISpace.*`, `UISize.*`, `UIMotion.{OutCubic, InCubic, OutQuad, InOutSine, OutBack, EaseFill, Reduced, DenyOffset(t)}` + duration consts, `ToastKind`, `CostSurface`, `TabVariant` | all |
| WP-1 | `UIFormat.{Nbsp, Keep, ClockMono, ShortElapsed}` | WP-4, WP-7 |
| WP-1 | `CostButtonView.{State, SetState(State, string unavailableLabel=null), Deny(), event Action Denied}`, `ButtonLabelDim`, `CurrencyStripView`, `InfoTooltipView.Show(RectTransform anchor, string title, string body, string source=null)`, `CurrencySources.For(CurrencyType)` | WP-3a, WP-4, WP-5, WP-6, WP-7 |
| WP-1 | `UIKit.{WhiteSprite, HitPad, OutlineMaterial, Outlined(), CostBtn, ClaimAllButton, FitFloor}`, `SceneParts.{ButtonStyle, SkinButton, Button, PopupBalanceChip, PanelHeader, BuildBadge, MenuButton(…, Vector2 size), IconMenuButton, Grid4, Grid5Popup}`, `BuildPopup` binding `_dimGroup` | builder packages |
| WP-2 | `NumberFormatter.{FormatCurrency, FormatCompact}`, `RewardService.{FormatAmount (all via FormatCurrency), FormatAmountCompact}` | all UI |
| WP-2 | `MetaText.{UnlockShort, UnlockSentence, GoldShortfall}` | WP-4, WP-5, WP-6, WP-7 |
| WP-2 | `BattleSystem.{IsFarming, ChallengeTarget, AutoChallenge, ChallengeBoss(), StepBack event}`; `ExpeditionSystem.GetLastMembers(zoneId)` (+CanSelect change); `PowerSystem.{PreviewPlace (synergy-aware), PreviewLevelUp, GetFormPower}`; `RedDotPaths.FormationEmpty`; `NoticeSystem.GetStartupNoticeToShow()`; `ShopSystem.TimeUntilReset`; `CharacterSystem.GetResonanceBonusPercentAt(int)`; `IdleChestSystem.HasRedDot` (new rule); `SourceAvailability.Check`; `Haptics.Play(HapticKind)`; counters `FormationSave.placementsDone`, flag `hint.deck.drag` | WP-4 … WP-7 |
| WP-3a | `UIManager.{ShowToast(string, ToastKind), ShowConfirm(title, body, onOk, okLabel, cancelLabel), ShowSpendConfirm(...), ShowDangerConfirm(...), ShowShortfall(CurrencyType, double), PushBackHandler, RemoveBackHandler, CloseAllImmediate(), HasOpenPopup, TopPopupWindowRect}`; `UIPopup.{RequestDismiss, OnDimOrBack, CloseButtonUsesDismiss}`; `UINav.{PushReturn, TryPopReturn, ClearReturn, ReturnChanged, OpenCharacter(id, tab), OpenSynergy(id, kindHint)}`; `MainTabController.Reselected` | WP-4 … WP-8, WP-9 |
| WP-3b | `HoldRepeatButton.{Step, Denied}`; `RewardFlyer.{PendingOf, PendingChanged, FlyTo}`; `PowerUpFloatView.AllowDecreaseOnce()`; `Badge.{SetDot, SetCount, SetNew}`; `ReturnChipView` | WP-4, WP-6, WP-7 |
| WP-4 | `FormationPanel.PulseRosterCard(string lineageId, float seconds)`; `GrowthPanel.Cost10` (static cached, for the tab glow) | WP-6 (gacha result), WP-3a (glow) |
| WP-6 | `CharacterDetailPopup.Tab*` consts, `Show(lineageId, tab)`; `EvolutionCraftPopup.Show(formId, autoStart)` | WP-3a (router), WP-7 (navigator) |

The tab glow needs a Wave 2 API that WP-3a (Wave 1b) consumes. WP-3a reads `GrowthPanel.Cost10` through a static `Func<double>` hook on MainTabController that GrowthPanel assigns in OnEnable, so there is no compile dependency.

### 4.3 Work packages

#### WP-0: Focus hotfix (M01) · P0 · effort S
- **Files**: §4.1 row WP-0.
- **Steps:**
  1. IdleProjectSetup.BuildAll: `ApplyPlayerSettings()` after the TMP check.
  2. SleepModeView: CanSleep first line + OnApplicationFocus.
  3. HoldRepeatButton, PressScale and EchoDeckView focus handlers.
  4. BattleView.TickQuality guard.
  5. Run menu 2 in the editor (or set Player › Run In Background) so `ProjectSettings.asset:85` = 1.
  6. Compile check.
- **Risks:**
  - `BattleView.Enemy.cs` may be open in the attack-FX workflow. Apply it as a one-line patch and re-check after their merge.
  - `CanSleep` is static, which is fine because `Application.*` is static.
- **Done when**: the §2.0 acceptance passes. Report the editor Interaction Mode tip to the user.

#### WP-1: Kit, tokens, project setup · Wave 1 · effort M
- **Findings**: M02, M07, M08 (CurrencySources), M09 (BuildBadge), M10 (MenuButton size, IconMenuButton), M14 (ToastKind), M22, M23, M24, M25, M26 (doc), M27, M28, M29, M30 (UIFormat), M47 (PopupBalanceChip, CurrencyStripView), M48 (Dim CanvasGroup), M51 (ClaimAllButton, PanelHeader), M52 (TabSelected), M53 (TabStyle), M59 (InfoTooltipView), M61 (Btn colours), M62 (EaseFill), M64 (Reduced), M81, M82. Optional C-23 tokens.
- **Steps:**
  1. UITheme colours + usage doc (§1.2). Do not touch `GradeColor`.
  2. UITokens.cs and UIFormat.cs (§1.3–1.10). The easing math is copied from BattleEase.
  3. UIKit:
     - WhiteSprite (lazy asset).
     - HitPad via raycastPadding.
     - Btn ColorBlock (pressed 0.92/0.90/0.95, fade 0.06).
     - FitFloor.
     - CostBtn (sets `disabledColor` white and attaches CostButtonView + CostPill).
     - ClaimAllButton.
     - OutlineMaterial loading + `Outlined()`.
  4. SceneParts:
     - ButtonStyle, SkinButton, Button.
     - BuildPopup: Dim CanvasGroup bound to `_dimGroup`; close `raycastPadding` −12.
     - BuildBadge: §1.5 sizes, raycast off, white text + outline.
     - MenuButton(size) with HitPad, IconMenuButton.
     - PopupBalanceChip (`BalanceChip`, a direct child of Window).
     - PanelHeader (88 row, title 34, left/right slots, hint ≤1 line).
     - CurrencyChip tooltip hook. Grid consts.
  5. New runtime components: CostButtonView, ButtonLabelDim, CurrencyStripView, InfoTooltipView (component only; WP-3a instantiates it), CurrencySources (table of label + nav key; gem mail row conditional).
  6. IdleProjectSetup:
     - EnsureFontAsset(src, dst) for both weights.
     - EnsureBoldWeight (every run).
     - EnsureOutlineMaterial (re-apply properties every run).
     - SetTmpDefaultFont sets `m_UseModernHangulLineBreakingRules`.
     - Keep WP-0's BuildAll line.
  7. Compile check, then a batch BuildAll on verify_proj **without** builder changes. The scene must be visually identical except for bold glyphs, line breaking and button pressed tint.
- **Risks:**
  - The bold weight table must keep outlined text (BattleFx outline, MetaSceneBuilder:679). `TMP_Settings.matchMaterialPreset` must stay true. Verify bold damage numbers.
  - The font OTF adds about 4.7 MB to the build.
  - Changing the existing `InkSub` value restyles every caller at once. Non-text callers move to `InkLine` in their own packages (WP-4/6), and screens look heavier until then. Accept this, or land WP-1 with an `InkSub` alias kept at the old value and switch it in Wave 2's last commit (recommended).
  - `TabSelected` changes the tab bar immediately (intended).

#### WP-2: Systems, data, content · Wave 1 · effort M
- **Findings**: M05, M08 (GoldShortfall), M09, M13, M16, M17, M18, M30, M32, M34 (PreviewLevelUp check), M36 (flag), M39, M46, M50, M57, M60, M64 (doc), M73 (MetaDataBuilder), M74, M77, M80 (counter).
- **Steps:**
  1. **M05**:
     - `ExpeditionSystem.CanSelect`: delete the `IsLineagePlaced` branch; keep the not-owned and `IsDispatched` checks. Update the docstring (lines 7-8).
     - `FormationSystem`: remove the dispatched rejection in Place (:76) and the skip in AutoFormation (:199). Keep the enum value.
     - `MetaDataBuilder.ValidateFtue`: owned ≥1 at the Expedition step.
     - Note for balance: expedition rewards may need a pass.
  2. **M57**: per-zone last members, set in TryStart and saved as `lastMembers` (zoneId + ids). `GetLastMembers`.
  3. **M13**: BattleSave fields, IsFarming / hold / ChallengeBoss / autoChallenge every 3 clears, gated by AutoChallenge unlock, plus a step-back event.
  4. **M09**: Skip → MarkSeen except the guide target; unregister HUD-toggle FeatureDots in MetaSession.
  5. **M16**: `RedDotPaths.FormationEmpty` leaf + register. Evaluate on Formation/Characters.Changed only.
  6. **M46**: `PowerSystem.PreviewPlace` builds modifiers from `SynergySystem.PreviewPlace`. Add PowerTests: synergy gained/lost, preview == TeamPower after TryPlace.
  7. **M50**: `GetStartupNoticeToShow()`.
  8. **M77**:
     - DailyClock is created in Game.Initialize before Shop/Dungeon and injected.
     - `Today => DayKey`, `DayChanged` subscription, `TimeUntilReset`.
     - Dungeon DayKey. MetaSession DayKey compare.
     - Copy '새벽 5시'.
     - Migration note: a player updating between 00:00 and 05:00 gets one extra refill.
  9. **M30**:
     - `FormatCurrency`/`FormatCompact` + tests (999,999 / 1,000,000 / 9,999 / 10,000 / 12,345 / 1,550,000 / NaN / negative).
     - RewardService and MetaRewardService '×'.
     - `ShopDatabase.Material` name without the count.
     - ContentValidator: product names stay unique.
  10. **M39**:
      - `MetaText.UnlockShort/UnlockSentence`.
      - FormationSystem:50/:306, MetaContent.Features:148, CharacterContent:214.
      - FormationSystemTests:56 expects '스테이지 2-9'. New tests: `UnlockSentence(20)`, `UnlockShort(1)==""`.
      - Grep and remove the player-facing '개방'.
  11. **M74**: Skip grants the final step's rewards plus the toast '튜토리얼을 건너뛰었어요 · 별조각 300을 받았어요'. TutorialSystemTests:300 expects 300.
  12. **M17**: SourceAvailability; ItemContent sources (수선 천 first: Shop normal); the drop-table audit and its editor warning.
  13. **M32**: the IdleChest HasRedDot rule.
  14. **M60**: `GetResonanceBonusPercentAt`.
  15. **M18 Phase A**: Haptics.cs + manifest. Phase B: UISfx.cs + UISfxTable (clips are a content follow-up).
  16. **M08**: `MetaText.GoldShortfall(need, have)`.
  17. **M80**: `FormationSave.placementsDone`. **M36**: flag `hint.deck.drag`.
  18. **M73**: MetaDataBuilder.PopupOpenedBy mapping.
  19. **M64**: the GameSettings doc comment.
- **Risks:**
  - Save-schema additions must default safely (challengeTarget 0, lastMembers empty, placementsDone 0).
  - Day-key changes affect DungeonSystem/ShopSystem tests. Update EditMode tests in the same package.
  - `PreviewPlace` must not allocate per hover beyond the scratch formation.

#### WP-3a: Shell and popup infra · Wave 1b · effort M
- **Findings**: M08 (ShortfallPopup, ShowShortfall), M09 (Reselected), M14, M15, M20 (mail), M22 (validator), M23 (lint), M26 (:194/:278/:382 Gold), M35d (tab glow), M43 (OpenSynergy hint), M48, M49, M50 (ShowStartupNotices), M51 (mail footer), M52, M55, M59 (TooltipLayer), M61 (NoticeItem), M65, M68 (BottomBleed), M72 (UINav + chip build), M73 (UINav sig), M76 (copy: IdleSceneBuilder 189/192/214/276, UIManager 144, MailPopup 53/60, AccountLinkRow 40-60, OfflineRewardPopup 26).
- **Steps:**
  1. Layers (§3.1): `TooltipLayer` after Popups; `SystemPopups` after BuildOverlays; `ToastLayer` last. MessagePopup and ShortfallPopup go under SystemPopups and stay in `ctx.Popups`.
  2. UIPopup: transitions (§3.4), `_dimGroup`, RequestDismiss / OnDimOrBack / CloseButtonUsesDismiss, Reduced path.
  3. UIManager:
     - back stack, `CloseTop` → `RequestDismiss`, `CloseAllImmediate`;
     - OnPopupOpened dimTarget;
     - ShowToast(kind) + positioning (§3.5) + `TopPopupWindowRect`;
     - ShowConfirm labels, ShowSpendConfirm / ShowDangerConfirm, ShowShortfall;
     - ShowStartupNotices shows at most 1;
     - quit copy (M15).
  4. MessagePopup: ConfirmOpts, CostRow, SkipToggle, runtime height, Danger 0.6 s lock, label reset.
  5. ShortfallPopup + builder (820×700, rows built locally).
  6. Toast build (§3.5) in ToastLayer. `ToastView` lives in WP-3b; this package builds the objects and binds them.
  7. UINav: return slot, OpenCharacter(tab), OpenSynergy(kindHint). Update the CharacterNavRouter subscription signature together with WP-6: keep the old `Action<string>` event as an obsolete forwarder until WP-6 lands.
  8. MainTabController: indicator, icon motion, panel fade, Reselected + scroll-to-top, ClearReturn on user tap, growth-glow hook. IdleSceneBuilder.BuildTabBar: Indicator, BottomBleed, the glow image under Tab_GrowthPanel.
  9. Mail popup: §2.10 (callback, defer refresh, stamp, Fly, footer ClaimAll, Gold row claim, copy). Notice popup (M65). NoticeItem PressScale. Offline popup Gold + copy.
  10. Return chip: build `ReturnChip` under StageArea after BattleSceneBuilder.Build (view from WP-3b).
  11. End-of-build validators (warn only): §1.3 text lint; Button hit-rect <88 with whitelist (cells inside ScrollRects, battle synergy chip); overlapping padded hit rects within one parent.
- **Risks:**
  - `CloseTop` semantics change. AutoShot `CloseAll` and smoke-test `CloseStartupPopups` must use `CloseAllImmediate` or a bounded loop (WP-9). A `while(CloseTop())` with ReturnPopup claiming **hangs**.
  - Moving MessagePopup out of `Popups`: only EvolutionCraftPopup path lookups exist (safe).
  - `ClaimAllButton` keeps its name (smoke test).
  - Toast layering must remain above SystemPopups.

#### WP-3b: Common components · Wave 1b · effort M
- **Findings**: M09 (Badge SetNew/sizes/appear/pulse ticker), M14 (ToastView queue, kinds, dedupe, timings), M18 (hooks in PressScale, RewardFlyer, LevelUpFanfareView), M19, M20 (RewardFlyer usage), M21 (pending), M35 (fanfare chip text), M46 (AllowDecreaseOnce), M61, M64 (CelebrationFx), M72 (ReturnChipView), M78 (FlyTo).
- **Steps:**
  1. PressScale rewrite (§3.10). Keep the M01 handler.
  2. HoldRepeatButton rewrite (§3.11). Keep the M01 handler, and extend it to OnApplicationPause.
  3. ToastView (§3.5).
  4. RewardFlyer: pending, the label pill (§2.2), FlyTo, the safety drain. StageClearRewardFlyer skips while a popup is open.
  5. Badge: SetDot/SetCount/SetNew, the appear pop, one shared static pulse ticker.
  6. PowerUpFloatView.AllowDecreaseOnce. LevelUpFanfareView chip copy + Medium haptic.
  7. CelebrationFx reduced mode (§1.9).
  8. ReturnChipView (§2.1.16, §3.14).
- **Risks:**
  - LevelUpFanfareView looks up `Popups/EvolutionCraftPopup`; don't change it.
  - RewardFlyer is used by FTUE/graduation flows; regression-check the 14 FTUE reward flies.
  - The Badge API change must keep the old `SetCount` behaviour for existing callers.

#### WP-4: Menu · Wave 2 · effort L
- **Findings**: M05, M07, M08, M09 (TopBarView notice), M10, M16 (PulseRosterCard, auto-pulse), M17 (SourceNavigator, ItemSourceRowView), M18 (settings pills), M19, M20 (RewardPopup), M21 (TopBarView), M22, M23, M24 (grids, MenuFx delegate), M25, M28, M29, M30, M33, M34, M35, M39 (zone card), M40A, M44, M45, M46, M51, M53 (Shop, Settings, Codex), M55, M56, M57, M58, M59 (reward slots), M64 (MenuFx.Flash, desc), M68 (TopBleed), M70, M72 (ItemInfo push), M74, M76, M77, M80, M82, M83.
- **Order:**
  1. Top bar (M10, M08 chips, M21, M68, M09 notice dot).
  2. Growth (§2.4).
  3. Formation (§2.5) + Synergy panel and codex (§2.10).
  4. Expedition (§2.7).
  5. Shop (§2.9).
  6. Bag / RewardPopup / ItemInfo / RewardSlot (M70, M83, M20, M59, M17, M72).
  7. Settings (§2.10).
  8. Copy pass M76 (ItemSourceRowView:26, MenuSceneBuilder:1119).
  9. MenuFx: easing delegates to UIMotion; Punch defaults (0.12, 0.22); EaseFill wrapper; Flash reduced.
  10. Grids (Grid4 Shop pad 36; Grid5Popup Reward 164×196).
  11. M29 captions to 22 (MenuSceneBuilder :128 (min 15→18), :683, :703, :948), :1643 → InkLine, GrowthPanel PositiveInk, CostumeListView check (WP-6 owns it; skip).
  12. M83 stars at :182/:558: `Lerp(GradeColor, Ink, 0.35)` on paper, GradeColor on night.
  13. M82 Primary text sites (:134, :137, :857, Tone mapping :2104) → PrimaryButton.
  14. M22: codex 150×50, undo 300×52 up-pad, expedition cancel 140×44, settings segments.
- **Risks:**
  - Coach targets `GrowthPanel/Buttons/LevelUp1Button`, `LineageList` (+ card `LevelUpButton`), `FormationPanel/CardList`, `TeamSynergy`, `ExpeditionPanel/ZoneList`, `ShopPanel/ProductList`, `ClaimAllButton` (expedition FTUE step 14) must keep names and parents.
  - `CoachTargetResolver.FindCardByLineage` relies on the lineage card being on screen; the sort and scroll reset handle it.
  - The smoke test settings `Tab0..3` names are unchanged.
  - The M58 page split must keep `ZoneList` under `ExpeditionPanel`, not under a new page parent. **Leave ZoneList where it is and add DungeonPage as its sibling.**
  - The M05 FormationCardView behaviour change interacts with M57 eligibility; test dispatch + place.

#### WP-5: Battle HUD · Wave 2 (after the attack-FX merge) · effort M
- **Findings**: M02 (Filled guard), M07 (EchoCardView:140), M11, M13 (HUD), M18 (lamp/boss haptics), M21 (ribbon drop), M22 (AUTO/x1), M23 (battle floors, DeckTab, lamp, shop HP), M24 (BattleEase delegate), M36, M37, M41, M43, M67, M81.
- **Order:**
  1. M02 (+ rebuild prefabs).
  2. M11 + M81 outlines.
  3. M43 synergy chip.
  4. M13 HUD (farm title, boss button, banner).
  5. M67 + M21.
  6. M36, M37.
  7. M41.
  8. M22 sizes.
  9. M23 floors (subtitle 12→18 min 16; floors at :1168/:1311/:1375 → 16).
- **Risks:**
  - Coach targets: `Hud/StagePanel`, `Hud/AutoButton`, `Hud/SpeedButton`, `Strip/DeckPanel/Card{n}`, `Strip/LampButton`. MetaSceneBuilder lock-cover fallback sizes must match the AUTO/x1 resize (WP-7 coordinates the numbers in §2.1.14).
  - BattleView*/BattleFx are shared with the attack-FX workflow; touch only the named lines.
  - SynergyHudView is still referenced by BattleView.Skills.cs (FallbackMotif stays).
  - The guide-zone rect in SkillCalloutView must use the M04 rect (16,174,484,258 in StageArea-bottom space).

#### WP-6: Character & gacha · Wave 2 · effort L
- **Findings**: M07, M08, M09 (discount pill), M16, M17, M19, M22 (sort), M23 (autosize helper floor), M24 (grid, CharacterMotion delegate), M25 (ApplyArt), M26 (:304 :414 :443 :595 :819 :965 :1300), M29, M30 (:262), M40B, M45, M47, M49, M53, M54, M55, M59 (chips), M60B, M62, M63, M64 (GachaCardView), M71, M72, M73, M74 (3b, P2), M75, M76, M80 (:290, :449), M82 (:149, :1166, discount :155), M84.
- **Order:**
  1. Gacha tab (§2.8).
  2. GachaResult (§2.10).
  3. CharacterDetail (§2.10).
  4. Recipe + Craft.
  5. 친구들 grid/cards.
  6. M29 InkLine migration + captions.
  7. M26 tones.
  8. M53 tab style.
  9. Router signature (M73).
- **Risks:**
  - Coach targets `GachaPanel/Buttons/Pull1Button`, `CharacterPanel/Grid`, `Popups/CharacterDetailPopup`, `Popups/SynergyCodexPopup` (built in MenuSceneBuilder; WP-4) must keep names.
  - The FTUE first-meeting flow relies on GachaResultPopup `Closed` firing only on a real close (the M49 first tap = skip).
  - `GachaPanel.Pull(int)` must stay confirm-free (smoke test + AutoShot).
  - The `_treeButton` binding stays.

#### WP-7: Meta · Wave 2 · effort L
- **Findings**: M02 (Filled guard), M04, M08 (IdleChest shortfall), M09 (TabGateView, RedDotView, FeatureLockCover, AttachRedDot), M10 (BuildTopBar, TopBarMetaView), M12, M13 (phase 2 sheet), M15 (sleep handler), M18 (stamp, deny hooks), M22 (lock covers, RecommendedPower), M23 (Fit floor, attendance ribbon), M25 (Skin), M26 (:1341, :1228/:1232), M30, M31, M32, M38, M39 (cover hint format), M48 (WaitClosed), M49 (ReturnPopup), M50 (presenter guard), M51 (journal footer), M53 (Journal, Attendance, MetaSegmentedControl), M59 (MetaRewardChip, attendance), M60A, M61, M62, M68 (sleep overlay), M69, M72 (journal push), M73 (MetaNavigator), M76 (:814, :1341, MissionRowView:97, FeatureUnlockCard:72), M78, M81 (chest time, guide texts), M82 (:814, :1095, :1556).
- **Order:**
  1. HUD: M04, M32 widget, M38, M12.
  2. Top bar (M10, with WP-4 contract).
  3. Red-dot views (M09).
  4. Return / IdleChest / Attendance / Journal popups.
  5. Unlock card + presenter (M78, M50, M48).
  6. MetaNavigator (M73).
  7. Strengthen sheet (M13 P2).
  8. Copy and colour passes.
- **Risks:**
  - Coach targets `MetaHud/GuideQuestBar`, `MetaHud/IdleChestWidget`, `TopBar/JournalButton`, `TopBar/AttendanceButton` (smoke test), `IdleChestPopup/Window/Body/ClaimButton`, `.../QuickSection/QuickButton`, `JournalPopup/Window/Body/ClaimAllButton`, `CheckInButton`.
  - `MetaSceneBuilder.BuildTopBar` positions relative to `BagButton` and `GoldPerSecText`, which are moved by WP-4. Use the final §2.2 numbers, not relative offsets.
  - The SleepModeView M01 handlers must survive.

#### WP-8: Tutorial · Wave 2 · effort S
- **Findings**: M06, M15 (StoryIntro, FirstMeeting handlers), M23 (Art.cs:122), M25 (GoldButton), M42, M68 (dim check), M79.
- **Order**: M06 → M42 → M79 → M15 handlers → M25/M23.
- **Risks:**
  - FTUE step indices must not shift (M06 adds a reward only).
  - `FtueStepGuideVisible` = 6 and `FtueStepFormation` = 11 are unchanged.
  - `CoachMarkView._freeAnchor` is a direct reference bound by the builder; there is no path lookup.

#### WP-9: Verify · Wave 3 · effort M
- **Findings**: M02 (EditMode test), M03, M50 (test replacement), Z99 (done: consistency findings recovered; future merges must write `scratchpad/ux/merge_input.json` first), plus §5 for every finding.
- **Steps**: §5. Also update the smoke tests:
  - `CloseStartupPopups` → `UIManager.Instance.CloseAllImmediate()` with a bounded loop.
  - Replace `StartupNotices_ShowEveryPopupNotice_OneAfterAnother` with (a) the first launch opens exactly 1 (the first unread) and (b) the next run opens the next unread, then none.
  - New PlayMode tests in `UiBehaviourTests`:
    - a hold of ≥1 repeat followed by release does not add an extra level;
    - `OnApplicationFocus(false)` during a hold stops spending;
    - back during StoryIntro does not open MessagePopup;
    - the 10-pull *button* opens a spend confirm (while `Pull(10)` does not);
    - a toast dedupes within 1.5 s;
    - a stacked popup dim is 0.4.
  - New EditMode `UiSceneLintTests`: no Filled Image without a sprite; every CoachTargets / Targets path resolves in Main.unity; no TMP fontSize <16 outside the allowlist (warn → fail once at 0).

### 4.4 Conflict resolutions (decided here)

| # | Conflict | Decision |
|---|---|---|
| 1 | M39 vs M40 lock copy | M39 wording ('스테이지 2-9 클리어 시 / 열려요'); M40 styles the pill |
| 2 | M38 vs M39 tiny locks | HUD and tab locks keep the bare '1-2'; toasts use UnlockSentence; cards use UnlockShort |
| 3 | M36 vs M80 deck caption gating | M36 rule (EchoSummon ≥10 AND drag flag) |
| 4 | M24 vs M48 popup motion | M48 timings |
| 5 | M24 vs M61 press timing | M61 (0.06 down / 0.16 up) |
| 6 | M23 Caption 22 vs M80 hint 20 | Two tokens: Caption 22, Hint 20 |
| 7 | M23 floor vs M44/M45 micro labels (13–17) | Base 18 bold, autosize min 16; pills grown to 64×26 where needed |
| 8 | M26 one Gold vs M54 ready-states and mail row claims | Ready-state overlays and repeated row claims exempt |
| 9 | M10 notice count on Settings vs M09 FYI dot | Plain dot |
| 10 | M10 left-block rects vs currency chips | LevelText 300 wide; PowerChip y −84; GoldPerSec y −146 |
| 11 | M13 boss button (356,−252) vs M12/M43/M67 | Moved to TopLeft(16,−226) |
| 12 | M14 toast under SafeArea | Separate ToastLayer (safe-area aware, last sibling) |
| 13 | M24 lineage grid vs M34 scroller | M34 (212×196, spacing 12, pad 24) |
| 14 | M51 vs M58 expedition header | M58 segments (no title) + M51 ClaimAllButton 240×64 |
| 15 | M33 card Top(92) vs M51 content start 96 | Everything +4 (card 96–306, buttons 322–472, list 534–740) |
| 16 | M65 vs M76 notice empty copy | '아직 등록된 공지가 없어요' (M65) |
| 17 | M15 vs M76 quit copy | M15 copy |
| 18 | M57 empty-state copy vs M05 | '보낼 수 있는 친구가 모두 원정 중이에요' |
| 19 | M34 GainText Positive on a parchment card | PositiveInk (M29 rule) |
| 20 | C-05/C-10/C-12/C-19/C-09 vs accepted M53/M09/M29+M82/M10/M47 | Accepted items win; the C items' remainders are backlog (Appendix B) |
| 21 | M12 two- vs three-tier and CVD | Three tiers kept + mandatory rating word (C-13) |
| 22 | M43 removes slots vs M22/M61 slot fixes | Apply padding and press to the summary chip instead |

### 4.5 Global risk register
- **Concurrent workflows.** The graphics-polish workflow edits builders (sprite swaps) and the attack-FX workflow edits Battle files. Wave 2 starts after both merge. If not, each package rebases on their latest commit and reruns the scene build. All Wave 2 packages re-shoot their screens after the rebase.
- **Scene regeneration.** Builders write Main.unity and prefabs. Only one BuildAll per wave, on verify_proj (batch). Never in the open editor.
- **Save compatibility.** New save fields (WP-2) default to safe values, and old saves must load. Test by loading `scratchpad/ux_shots/autoshot_player.json` (a copy).
- **Performance.** No new per-frame work on idle screens: tickers, pulses and sweeps enable and disable themselves. Text writes only on change. PowerSystem previews only on hover change.
- **Localization hazards.** Particle helpers for every new string with a variable noun; '×' glyph availability; nobr tags only in TMP labels with rich text.

---

## 5. AutoShot verification (WP-9)

### 5.1 Harness changes (`Verify/Runtime/AutoShot.cs`, `scratchpad/verify.ps1`)
1. **Resolution and focus-independence [M03].**
   - Keep launching at 540×960 windowed.
   - Capture with `ScreenCapture.CaptureScreenshot(path, 2)` to get true 1080×1920 PNGs.
   - Log `Screen.width/height` after 3 frames.
   - Never wait on focus events. The harness relies on `runInBackground` (§2.0).
   - Optional second pass: `-popupwindow -screen-width 1080 -screen-height 1920`. Assert the screen still reads 1080×1920 after 3 frames; otherwise log `[Verify] SKIP native`.
2. **CloseAll** → `UIManager.Instance.CloseAllImmediate()`. A `while (CloseTop())` loop can hang after M49.
3. **Assertions** are written as `[Verify] PASS|FAIL <id> <detail>` lines to `player.log` **and** `shots/verify_report.txt`. verify.ps1's shots stage greps `\[Verify\] FAIL` and prints the report. Any FAIL makes the run red.
4. **Helpers**:
   - `RectOnScreen(RectTransform)` → a screen Rect in 1080 space.
   - `Overlaps(a, b, pad)`.
   - `Contrast(TMP_Text)`: sample the background luminance under the text bounds from the captured texture (`ReadPixels` after `WaitForEndOfFrame`) against the text colour; report ≥4.5.
   - `SimulateHold(Button, seconds)` with `ExecuteEvents.pointerDown/Up`.
   - `BroadcastFocus(bool)`: `SendMessage("OnApplicationFocus", f, DontRequireReceiver)` on all MonoBehaviours.

### 5.2 Capture plan

| # | Shot | Setup | Proves | Checks (`[Verify]`) |
|---|---|---|---|---|
| 01 | startup | 3 s | M79 | skip button α=0 before 1 s (log) |
| 02 | ftue_intro_2 | 2.5 s | M79 | skip visible + interactable; tap-hint contrast ≥4.5 |
| 02b | ftue_intro_back (new) | invoke `UIManager.OnBack` (reflection or a debug hook) during the intro | M15 | MessagePopup not open; page advanced or text completed |
| 03 | ftue_coach | as now | M42 | bubble rect inside ContentArea (screen y ≥970); StagePanel and AUTO not overlapped |
| 04 | ftue_battle_locked_tabs | as now | M38, M09 | lock covers opaque (sample: no 'AUTO' glyph contrast under the cover); no badge on the selected tab |
| 04b | ftue_step6_reward (new) | drive FTUE to complete step 6 (Tutorial debug complete) | M06 | gold ≥ cost(Lv1→5) right after step 6; RewardFlyer launched from GrowthLevelUp1 |
| 04c | ftue_expedition (new) | FTUE at the Expedition step; open the setup popup | M05 | ≥1 selectable cell; 출발 interactable after pre-fill |
| 05 | battle | skip + unlock all, 4 s | M02, M03, M04, M09, M10, M11, M12, M22, M32, M36, M37, M43, M52, M68 | mapper Ppu 205±5, Squash 0.726±0.02, shop width ≥120 and centre in field, 5 pads active spread ≥300 px; badge count ≤4; selected tab has no badge; top-bar regions don't overlap (§2.2); guide bar rect = 16–484 × 712–796; ProgressText/BossLabel/shop HP contrast ≥4.5; widget never shows '0:00'; SynergyRow right edge ≤540 |
| 06–11 | battle_fx_0..5 | as now | M02, M41 | at least one enemy HP fillAmount <1 and its trail > fill; no two bubbles overlap; no bubble inside the guide/deck-tab rects; no duplicate bubble text |
| 11b | battle_farm (new) | set challengeTarget = stage+1 and stage = stage (or force 2 boss fails via a debug kill-shop) | M13 | title contains '반복 중'; BossChallengeButton active at 16–246 × 416–484; no overlap with SynergyRow or the ribbon zone |
| 11c | battle_ribbon (new) | force a stage clear; shoot 0.3 s in | M67 | ribbon rect ∩ (StagePanel ∪ RecommendedPower ∪ SynergyRow) = ∅ |
| 11d | toast_stack (new) | 3× the same error toast within 1 s, then 1 info | M14 | one error toast visible; top edge y=480±4; 2nd toast queued; kind icon present |
| 11e | focus_hold (new) | `SimulateHold(LevelUp1Button)` 1 s, then `BroadcastFocus(false)`, wait 1 s | M01, M19 | gold unchanged during the 1 s after focus loss; button scale 1; no extra level on release |
| 12 | tab_Growth | as now | M33, M34, M35, M52, M29 | MAX label '최대' (dim) at Lv1 / '최대 +N'; 1.1 next-value contrast ≥5; lineage list content > viewport shows peek ≥100 px; indicator under the tab |
| 12b | tab_Growth_short (new) | gold = 0 | M07 | x10 cost pill visible, cost contrast ≥4.5; skin not flattened |
| 12c | tab_reselect (new) | scroll the Growth panel, re-tap the tab | M52 | scroll at top after 0.3 s; `Selected` not fired (log counter) |
| 13 | tab_Formation | as now | M44, M45, M46, M16, M80 | synergy row texts ≥16 rendered; LevelPill and PowerText present; field pill shows '⚔'; FormationEmpty dot when a slot is empty + unplaced owned |
| 14 | tab_Characters | as now | M40B, M45, M24, M51 | first column x = 36; unowned species contrast ≥4.5 |
| 15 | tab_Expedition | as now | M58, M40A, M39, M51, M57 | segments present; dungeon not under the zone list; lock text contains '클리어 시'; no '개방' anywhere (scan all TMP) |
| 15b | expedition_resend (new) | start + claim a zone (time-skip) | M57, M20 | card label '다시 보내기'; RewardPopup 확인 launches flyers |
| 16 | tab_Gacha | as now | M63, M84, M07 | pity bar track visible at 0; title box ≤540 wide |
| 16b | tab_Gacha_short (new) | gems = 0 | M07, M08 | GemSourceLink active; 10-pull cost contrast ≥4.5 |
| 16c | gacha_confirm (new) | invoke the 10-pull **button** onClick | M55 | MessagePopup open with CostRow active, label '소환'; `Pull(10)` direct path untouched |
| 17 | tab_Shop | as now | M56, M77, M30 | ResetPill text matches `초기화까지 \d+:\d\d:\d\d`; product names contain no ' x\d' |
| 18 | character_jjakjjak | as now | M54, M47, M60B, M53 | BalanceChip present; ≤1 non-ready Gold fill; resonance line contains '→' |
| 18b | character_costume_nav (new) | `UINav.OpenCharacter("jjakjjak", TabCostume)` | M73 | the costume page is active |
| 19 | recipe_caravan | as now | M71, M17 | section order ①②③ then skill; collect label contains '·'; locked source rows show the lock |
| 20 | gacha_result | as now (Pull(10)) | M16, M47, M76 | no full-width Gold; Retry is Gold; '확인' left; FormationButton active when applicable; no '했습니다' |
| 20b | gacha_result_dim (new) | open the result, immediately call RequestDismiss | M49 | popup still open (skipped); a second dismiss closes it |
| 21 | bag | as now | M70, M83 | window height 1060; zero counts in SubText; grade pill on ≥Rare |
| 22 | synergy_codex | as now | M44, M53, M70 | favoured chip reads '이번 스테이지 ×1.5' or the ring is hidden |
| 23 | meta_journal | as now | M60A, M51, M53 | each chest has a Reward caption; ClaimAllButton under Body |
| 24 | meta_attendance | as now (Open, not OpenAuto) | M69, M59 | amount labels are fully inside cells (no clipping: label rect ⊂ cell rect) |
| 25 | meta_idle_chest | as now | M32, M28, M30 | footnote directly under the grid (gap ≤20); no line ends mid-word (check TMP lineInfo: a line break never sits between two Hangul syllables of one word) |
| 26 | meta_return | as now | M31, M21 | DoubleButton Gold on the right; window 1054 when 1 row; '+N' labels don't overlap the top-bar buttons |
| 27 | meta_unlock_card | as now | M78, M26 | '가 보기 ›' is Primary |
| 27b | unlock_decline_fly (new) | Show(Missions), then 나중에, shoot at 0.3 s | M78 | the flyer is moving toward JournalButton |
| 28 | settings | as now | M74, M80, M18, M64 | desc lines single-line; BGM/SFX sliders non-interactable with '준비 중'; skip row text '남은 안내 말풍선을 꺼요' |
| 28b | shortfall (new) | `ShowShortfall(Gem, 900)` with gems 350 | M08 | '550 더 필요해요'; first row is the mail when gem mail is unclaimed |
| 28c | popup_stack (new) | Settings, then a MessagePopup | M48, M15 | the top dim α≈0.4; MessagePopup under SystemPopups and above Popups |
| 28d | mail (new) | open the mail, claim one | M20, M51, M76 | ClaimAllButton Gold 380×96; flyer started from the row; '없어요' copy when empty |
| 29 | battle_later | 20 s | M21, M09, M02 | chips equal Currency−Pending after the flights settle; badge count ≤4; boss bar matches kills |

### 5.3 Static checks run in the same pass (log only)
- Every CoachTargets and MetaContent.Helpers.Targets path resolves (`canvas.Find`). FAIL otherwise.
- No TMP_Text has fontSize < 16; count those < 18 (warn).
- No Image has `type == Filled && sprite == null`.
- Every Button hit rect is ≥88 on both axes, excluding the whitelist (warn).
- No player-facing text contains '개방', '습니다' or '하시겠습니까' (warn).
- `Application.runInBackground == true` (FAIL otherwise).

### 5.4 Manual checks (not automatable)
1. M01 alt-tab checks (a)–(e) on a PC build and in editor play mode.
2. Android back on device: intro, first-meeting card, sleep mode, stacked popups, gacha reveal, return claim.
3. Haptics on an Android device with API ≥26 and on one older device (M18).
4. Notched-device simulator profiles (iPhone 15, Pixel): no bg_night seams (M68).
5. Real hold-to-repeat feel: acceleration, scroll cancel on lineage cards, combo pill (M19).
6. Grayscale screenshot pass of 23, 24 and 28: the selected tab is identifiable (M53).

---

## Appendix A: Finding index

| Id | Title | Pri | Packages | Section |
|---|---|---|---|---|
| M01 | Keep running unfocused (user request) | P0 | WP-0 | 2.0, 3.16 |
| M02 | Battle fill bars never deplete | P0 | 1, 5, 7, 9 | 2.1.2 |
| M03 | Battle focal collapse: verification gate | P0 | 9 | 2.1.1, 5.1 |
| M04 | Guide quest bar legibility | P0 | 7 | 2.1.9 |
| M05 | FTUE expedition dead end → expeditions independent of formation | P0 | 2, 4 | 2.7, 2.12 |
| M06 | FTUE gold wall → step 6 reward 250 | P0 | 8 | 2.12 |
| M07 | Cost button states | P0 | 1, 4, 5, 6 | 3.10 |
| M08 | Shortfall routing & currency sources | P1 | 1, 2, 3a, 4, 6, 7 | 3.8, 2.10 |
| M09 | Red-dot hygiene | P0 | 1, 2, 3a, 3b, 4, 6, 7 | 3.13 |
| M10 | Top bar restructure | P1 | 1, 4, 7 | 2.2 |
| M11 | Stage panel readability | P1 | 5 | 2.1.3 |
| M12 | Power vs recommended pill | P1 | 7 | 2.1.4 |
| M13 | Boss stuck guidance (farm, boss button, 강해지기) | P0/P1 | 2, 5, 7 | 2.1.5 |
| M14 | Toast system | P0 | 1, 3a, 3b | 3.5 |
| M15 | Android back over overlays | P0 | 3a, 7, 8 | 3.2 |
| M16 | Gacha result CTAs & new-friend nudge | P1 | 2, 4, 6 | 2.10 |
| M17 | Recipe source targeting | P1 | 2, 4, 6 | 2.10, 3.14 |
| M18 | Audio & haptics hooks | P1 | 2, 3a, 3b, 4, 5, 6, 7 | 3.15 |
| M19 | Hold-to-repeat | P0 | 3b, 4, 6 | 3.11 |
| M20 | Reward-flyer coverage | P1 | 3a, 3b, 4 | 3.12 |
| M21 | Counter sync & flyers over modals | P1 | 3b, 4, 5 | 2.2, 2.1.7 |
| M22 | Tap targets | P1 | 1, 3a, 4, 5, 6, 7 | 1.5 |
| M23 | Type scale & floor | P1 | 1, 3a, 4, 5, 6, 7, 8 | 1.3 |
| M24 | Design-token layer | P1 | 1, 4, 5, 6 | 1.4, 1.7, 1.8 |
| M25 | One button factory | P1 | 1, 4, 6, 7, 8 | 1.6 |
| M26 | Button semantics rule | P1 | 1, 3a, 6, 7 | 1.6 |
| M27 | Real bold weight | P1 | 1 | 1.3 |
| M28 | Hangul line breaking | P0 | 1, 4, 7 | 1.3 |
| M29 | Parchment ink tokens | P1 | 1, 4, 6 | 1.2 |
| M30 | Number format | P1 | 1, 2, 4, 6, 7 | 1.10 |
| M31 | Return popup | P1 | 7 | 2.11 |
| M32 | Basket widget & popup | P1 | 2, 7 | 2.1.10, 2.11 |
| M33 | Growth panel hierarchy | P1 | 4 | 2.4 |
| M34 | Growth lineage cards | P1 | 2, 4 | 2.4 |
| M35 | Growth idle juice | P2 | 3a, 3b, 4 | 2.4, 2.3 |
| M36 | Deck strip | P1 | 2, 5 | 2.1.11 |
| M37 | Lamp button | P1 | 5 | 2.1.12 |
| M38 | Lock covers clean | P1 | 7 | 2.1.13 |
| M39 | Unlock-condition wording | P1 | 2, 4, 7 | 1.10 |
| M40 | Locked-card dim | P1 | 4, 6 | 2.7, 2.6 |
| M41 | Battle speech bubbles | P1 | 5 | 2.1.8 |
| M42 | Coach bubble placement | P1 | 8 | 2.12 |
| M43 | Battle synergy summary chip | P1 | 3a, 4, 5 | 2.1.6 |
| M44 | Synergy rows & panel | P1 | 4 | 2.5, 2.10 |
| M45 | Formation cards & density | P1 | 4, 6 | 2.5, 2.6 |
| M46 | Formation power preview | P1 | 2, 3b, 4 | 2.5 |
| M47 | Popup balance strip | P1 | 1, 6 | 2.10 |
| M48 | Popup transitions & stacked dim | P1 | 1, 3a, 7 | 3.4 |
| M49 | Dim/back tap policy | P0 | 3a, 6, 7 | 3.3 |
| M50 | Startup modal chain | P1 | 2, 3a, 7, 9 | 2.11 |
| M51 | Panel header & claim-all | P1 | 1, 3a, 4, 6, 7 | 3.9 |
| M52 | Bottom tab selected state | P1 | 1, 3a | 2.3 |
| M53 | Segmented control unify | P1 | 1, 4, 6, 7 | 2.10, 2.11 |
| M54 | Character detail CTA hierarchy | P1 | 6 | 2.10 |
| M55 | Spend confirm | P1 | 3a, 4, 6, 7 | 3.7 |
| M56 | Shop purchase flow | P1 | 4 | 2.9 |
| M57 | Expedition send loop | P1 | 2, 4 | 2.7 |
| M58 | Dungeon buried | P1 | 4 | 2.7 |
| M59 | Info tooltips | P1 | 1, 3a, 4, 6, 7 | 3.6 |
| M60 | Hidden reward previews | P2 | 2, 6, 7 | 2.11, 2.10 |
| M61 | Press states | P1 | 1, 3a, 3b, 7 | 3.10 |
| M62 | Progress fill animate | P2 | 1, 6, 7 | 3.12 |
| M63 | Gacha pity row | P1 | 6 | 2.8 |
| M64 | Reduce motion (menus) | P2 | 2, 3b, 4, 6 | 1.9 |
| M65 | Notice loading state | P2 | 3a | 2.10 |
| M67 | Ribbon/HUD collision | P1 | 5 | 2.1.7 |
| M68 | Safe-area bleed | P2 | 3a, 4, 7, 8 | 2.2, 2.3 |
| M69 | Attendance popup | P1 | 7 | 2.11 |
| M70 | Bag popup & codex empty | P2 | 4 | 2.10 |
| M71 | Recipe section order | P1 | 6 | 2.10 |
| M72 | Return chip after jumps | P1 | 3a, 3b, 4, 6, 7 | 3.14 |
| M73 | Nav hard-coded lineage | P1 | 2, 3a, 6, 7 | 3.14 |
| M74 | Tutorial skip copy & reward | P1 | 2, 4, 6 | 2.10 |
| M75 | Evolve double confirm | P2 | 6 | 2.10 |
| M76 | Tone & nav-verb consistency | P2 | 3a, 4, 6, 7 | 1.10 |
| M77 | Daily reset unified (05:00) | P1 | 2, 4 | 2.9, 2.7 |
| M78 | Feature unlock fly-to-slot | P2 | 3b, 7 | 2.11 |
| M79 | Story intro skip & tap hint | P1 | 8 | 2.12 |
| M80 | Hint-line sprawl | P2 | 2, 4, 5, 6 | 3.17 |
| M81 | Shared outline material | P1 | 1, 5, 7 | 1.3 |
| M82 | Flat-button & badge contrast | P1 | 1, 3a, 4, 6, 7 | 1.2 |
| M83 | Colour-only encodings (CVD) | P1 | 4 | 2.10 |
| M84 | Gacha banner scrim | P2 | 6 | 2.8 |
| Z99 | Merge-input truncation (process) | – | 9 | Appendix B |

## Appendix B: Consistency findings recovered after truncation (C-05…C-25, never challenged)

| Id | Topic | Disposition |
|---|---|---|
| C-05 | One tab component | **Superseded by M53** (Page = Parchment/Ink; Option = PrimaryButton/Cream). Its main-tab indicator idea is already M52. |
| C-06 | Rarity not colour-only | **Partly M83** (GradePill on RewardSlot). Backlog P2: GradeMark on FormationCard, ExpeditionMemberCell, SynergyMemberCell and RecipeMaterial. **Bug worth fixing:** ExpeditionMemberCellView:40 and SynergyMemberCellView:34 recolour the frame Gold for selected/placed, so an unselected Legendary looks selected. Use a 6 px Lamp outline + check badge for selection instead (needs a challenge pass). Changing Common to #8C86A3: deferred (M83 keeps the palette). |
| C-07 | Type scale & weights | **Folded** into M23 + M27. |
| C-08 | Reduce motion | **Partly M64** (`UIMotion.Reduced` = ReduceFlash). Backlog: a separate '움직임 줄이기' toggle; a UILoop component consolidating SoftPulse/MenuGlowPulse/MetaPulse/Spin; "only the craftable evolution node pulses". |
| C-09 | Popup size classes + footer | **Backlog** (M47 deliberately left sizes alone). Revisit after the art workflow: DialogS/M/L 840×560/760/880, Sheet S/M/L/XL 980×1120/1320/1560/1720, footer Bottom(36,112,36,36). |
| C-10 | Badge system | **Mostly M09**; ad-hoc dot consolidation is listed as §3.13 item 10 (P2). |
| C-11 | Surfaces & radii | **Backlog**, with M24 Phase 3. Notable rules to evaluate: Paper = actionable list entries, NightCard = status rows; Journal mission rows → Paper. |
| C-12 | Contrast tokens | **Superseded** by M29 (InkSub #5E4C7B) and M82 (PrimaryButton #7A58D6). |
| C-13 | Power rating colour-only | **Folded into M12** (rating word mandatory). |
| C-14 | CVD chips, legend, tree lines | **Backlog P2**: chips always show an icon (first-syllable fallback); evolution legend shapes; tree line styles (solid / dashed / dotted); direction glyphs on pads. |
| C-15 | Tab panel header | **Folded** into M51. |
| C-16 | Section header | **Backlog P2**: `SceneParts.SectionHeader(parent, title, count, hint, onPaper)` at 44 tall (H2 30). Easy to add to WP-1 if time allows. |
| C-17 | CTA & row sizes | **Backlog**, with C-09. In-row action 180×80 kept as guidance (§1.5). |
| C-18 | Friend & item tiles | **Backlog (L)**: FriendTile and ItemTile prefabs. |
| C-19 | Top bar | **Superseded by M10**. |
| C-20 | Tap targets & press | **Folded** into M22 + M61. |
| C-21 | Duplicated private helpers | **Backlog**, with M25 Phase 2: move Pill, ChipRow, VList, Fit, WithAlpha, Card, Portrait, LockIcon and CheckMark to UIKit/SceneParts, plus a guard script. |
| C-22 | Icon sizes & glyphs | **Tokens adopted** as guidance (§1.5). Chevron/check icon assets and leading/trailing icon params are backlog. |
| C-23 | Off-token colours | **Optional tokens** in WP-1 (§1.2). Toast and lock-cover colours are fixed by M14/M38. Delete the TabOff duplicates via M53. |
| C-24 | Copy voice | **Folded** into M76 (OfflineRewardPopup:26 and MailPopup:53 added). |
| C-25 | Run when unfocused | **Duplicate of M01**. |

Process fix (Z99): future merge steps must first write their input JSON to `scratchpad/ux/merge_input.json`, and must check the per-lens finding counts before merging.

## Appendix C: Rejected

**M66 content-collapse "watch mode"**: rejected. A taller StageArea does not enlarge the fight, because Ppu is clamped by width. It also blurs and crops the backdrop under cover-fit, misaligns the painted ring, gives re-tap a hidden second meaning (which conflicts with M52 scroll-to-top), persists a confusing collapsed state, and breaks FTUE coach targets in ContentArea. If a watch mode is wanted later, use an explicit eye-icon '감상 모드' HUD button: session-only, disabled during FTUE, hiding HUD overlays and dimming content without resizing the stage.

## Appendix D: Amendments after UX Wave 3 (2026-11-05, orchestrator)

These record what was built and verified in Wave 3 (verify r3 green). Where they differ from the body above, they take precedence.

- **M15 exit dialog:** the button roles are swapped. OK = '더 지킬래요' (stay), Cancel = '닫기' (quit). Only the Cancel *button* press quits; dim tap, Android back and X all mean stay. Landed in the RM-2 KIT lane: `ConfirmOpts.onCancelButton` runs only when the Cancel button itself is pressed, after `onCancel` and `onClosed`; `UIManager.ShowExitConfirm` passes okLabel '더 지킬래요', cancelLabel '닫기', `onCancelButton` = Quit.
- **M31 ReturnPopup:** '오늘 무료' → '하루 한 번'.
- **M32 IdleChestPopup:** '무료 · 오늘 N회 남음' → '오늘 N회 남음'.
- **M35 / §2.4 MAX button (count ≤ 1):** the plate alone dims to α 0.65 with the gold skin kept (the transition switches to None while dimmed). The label stays at α 1. This replaces "a serialized CanvasGroup at α0.5"; `MenuSceneBuilder` no longer adds that CanvasGroup.
- **Quick-rail badges (§8.5 M09 addendum):** the rail NEW mark rests while 2 or more rail claim marks are lit. Claim marks are never capped.
- **RM-2 P4-4 deviations (accepted at RM-3):** (a) while `EventHubTile` is shown, `EventTile` opens the event shop and is labelled '이벤트 상점' (`MenuPopup.EventTileGoesToShop`); without the hub tile it keeps the event route; (b) `CouponTile` appears only after the FTUE; (c) the '축제 중' pill sits on the in-panel ShopPanel `EventTab`, not on the bottom-nav shop tab; (d) the expedition zone-card tier chip sits on the art's top-left; (e) the Settings window is 940×1516; (f) `DungeonLock` stays a sibling of `Seg_Dungeon` (as a child with `ignoreParentGroups` it would break the panel and sheet fades), so checks look it up as the sibling.

## Appendix E: Amendments after RM-2 (2026-11-06, orchestrator)

Applied in the RM-3 docs pass (T-5) from `scratchpad/roadmap/rm3/triage.md` §3. They record what RM-2 built and G2 verified; where they differ from older text above, they take precedence.
- **A-05 (i) (R15, R70)** Appendix D: the accepted RM-2 P4-4 deviations (a)–(f).
- **A-06 (R44)** Appendix D, M15: `ConfirmOpts.onCancelButton` has landed (RM-2 KIT).
- **A-07 (R45)** §3.5 Position: rule (a2), the toast width inside the MAIN quick rails.
- **A-08 (R46)** §2.4 M35: the LevelUpFanfare chip placement.
