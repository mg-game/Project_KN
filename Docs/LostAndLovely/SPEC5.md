# LOST & LOVELY: Phase 5 implementation contract (SPEC5) — the R2 systems, priorities P0 and P1

Version 1.0.1 · 2026-11-04 · author: phase-5 architect · **v1.0.1: the PHASE4-ROADMAP critic's edits, tagged `(CR4-nn)`; the reasons and evidence are in PHASE4-ROADMAP "Critic revisions"** (v1.0 backup: `phase4b/critic/SPEC5.pre-critic.md`; the RM-3 critic pass against the real SPEC4 code still writes v1.1) · target `D:/UnityProject/unity_idle` (Unity 6000.2.6f2, uGUI, 1080×1920 portrait) · project read-only while writing (no project file was edited)

**Relayed user requests (standing orders):**
- "다른 유명한 방치형게임들의 레퍼런스를읽어서 부족한기능들 알아서 다진행해줘" (read the references of famous idle games and build every missing feature yourself).
- "크레딧충전했으니까마음껏써 품질최상으로 전부다" (use the credits freely; top quality; everything).

**What this is.** The build contract for every **P0 and P1** system of `IDLE_RESEARCH2.md` v1.2 (the second reference sweep, critic-revised, with the 51 unseen findings merged). It is written in the SPEC4 style: shared contracts first (save, enums, content definitions, module wiring, hooks, navigation, red dots, popups), then one section per system (rules, API, UI in the new screen frame, Korean copy, numbers, art, audio, tests), then packages, assets and proof. P2 items (waves C and D) are out of scope; their numbers are reserved so they can land later without a contract change.

**Answer this spec gives to "is everything from the references built?":** after SPEC4 ships, the second sweep still leaves **7 P0 systems** (R2-01 MVP, R2-03, R2-04, R2-05a, R2-07, R2-08, R2-36a), **16 P1 systems or halves** (R2-02, R2-05b, R2-09, R2-10, R2-11, R2-12, R2-13, R2-14, R2-15, R2-17, R2-18, R2-19, R2-20, R2-36b, R2-37, errand 3) and the shared **R2-00** infrastructure. SPEC5 assigns all of them. It also adds the one curve fix and two economy settings that the progression simulation proved are required before any R2 number is valid (§4), and fixes a pacing defect the simulation exposed (§2.8).

**Inputs (read before your package):**
- `IDLE_RESEARCH2.md` v1.2 (design source, IDs R2-xx, critic tags CR2-xx, v1.2 merge log) — this spec supersedes it wherever they differ; every difference is listed in §0.4.
- `SPEC4.md` v1.1 (the contracts SPEC5 builds on: `IMetaModule`, `EventCalendarSystem`, `CodexSystem`, `AchievementSystem`, `AffinitySystem`, `DailyGiftSystem` (NEW-02), `FireflyRules` (NEW-03), ticket bank (NEW-04), `StrengthenAdvisor`, `CheerSystem`, red-dot paths, popup kinds, PowerSystem taper, CR-07).
- `UX-SPEC.md` (tokens, cross-cutting rules) and **`UX-SPEC-IA.md` v1.1** (the new frame: MAIN = battle full height, SHEET = 836 px sheet, 3+1+3 nav with the centre 사냥 button, quick rails, ≡ menu). The frame is implemented in code (`UI/UIFrame.cs`, `UI/Main/{MainTabController,SheetController,SheetDragHandle,MenuPopup}.cs`, `UI/Meta/{QuickRailView,QuickIconView,RedDotAnyView}.cs` exist on 2026-11-04).
- `phase4b/PROGRESSION-SIM.md` and `phase4b/analysis/report_tables.md` (227 runs of the real game code: curve fix, economy retune, chapter timeline, unlock-timing finding).
- `ART-UPGRADE-PLAN.md` (prices, prompt assembly, QA gate, balance floor) and `AUDIO-SPEC.md` (audio engine `AudioService`, catalog, CC0 + synthesis production, reserved R2 keys §2.11).
- Code read on 2026-11-04 (paths under `Assets/_Project`): `Scripts/Core/{GameEnums,PlayerData,SaveData.*}.cs`, `Scripts/Data/{MetaDatabase,BattleConfig,GameConfig,CharacterDatabase,ExpeditionDatabase,ShopDatabase}.cs`, `Scripts/Systems/Battle/{BattleSystem,BattleSimulator*,BattleTypes,DungeonSystem,FormationSystem,ExpeditionSystem}.cs`, `Scripts/Systems/Meta/{MetaSession,FeatureGateSystem,PopupQueue,RedDotSystem,ProgressTracker,DailyClock,MissionSystem,IdleChestSystem,MetaRewardService,PowerSystem,MetaTypes}.cs`, `Scripts/Systems/Characters/CharacterSystem.cs`, `Scripts/UI/{UIFrame,UINav}.cs`, `Scripts/UI/Main/{MainTabController,GrowthPanel}.cs`, `Scripts/UI/Meta/{QuickRailView,QuickIconView}.cs`, `Scripts/UI/Battle/BattleView.cs`, `Editor/Content/**`.

**How to read:** §0 rules → §2 contracts (everyone) → your package in §8 → the system sections it points to (§3–§6) → the screen anatomy (§7) → assets §9 → proof §10. Korean copy is in the system sections and in Appendix A/B; numbers are in the system sections and Appendix C.

---

## 0. Ground rules

### 0.1 Inherited (unchanged)
- Everything in SPEC4 §0.1–§0.2 still applies: frozen coach/FTUE paths (now the UX-SPEC-IA §5.8 list), binder-field rule, focus rule M01, 해요체 copy (no '습니다', no '개방'), text floor 18 (autosize min 16), unscaled-time UI motion, no per-frame allocation, idle components disable themselves, pure C# systems plus MonoBehaviour views, scenes from builder code and `Binder`, art through `ArtLib`/`UiArt` with placeholder fallbacks, content in code under `Editor/Content`.
- Never launch Unity in the user's open project. Compile-check with `python -I scratchpad/compile_check.py` (keep your own files at 0 errors). Scene rebuilds and batch tests run on a project copy (`verify_proj`). C# 9 only. Korean XML summaries on new public members.
- One owner per file (§8.2). A file passes from one package to the next only at a merge point.
- UX-SPEC-IA frozen shell files (`UIFrame`, `UINav`, `UIManager`, `MainTabController`, `SheetController`, `SheetDragHandle`, `IdleSceneBuilder`, `SheetFade`, `RedDotSystem.IsClaimOn`) are edited only by R2P-0 under the shell exception in §2.6, and only with the listed members. Every other need is reported, not edited.

### 0.2 Phase-5 specific
1. **Contract first.** R2P-0 lands all of §2 (wave A **and** wave B shapes) plus inert stubs before any other R2 package starts. Stubs compile, return empty/false and **never throw** (GameBootstrap's kill switch turns off all meta on an exception).
2. **Append-only enums with explicit values** (§2.2). R2 numbering continues after SPEC4's appends. Values reserved for P2 are written in comments and never reused.
3. **Save schema v5** (§2.1). Every field has a safe default and a `Sanitize` guard. A v4 save loads, plays and re-saves. All wave-A and wave-B fields land in v5 at once, so wave B needs no v6.
4. **No new modal during FTUE.** Every R2 popup kind and every side battle refuses while `Game.Tutorial.IsActive`.
5. **Locked means locked.** Every R2 system guards its own gate (`FeatureResolver`), not only its UI.
6. **Side battles never touch main progress.** They never raise `StageChanged`/`StageEnded`, never change `highestStage`, `challengeTarget` or the stage counters, and the main stage restarts at its start after them (§3.1).
7. **Economy guards are tests** (§4.5, §10.2): R2 P0+P1 ≤ +250 별조각/week (typical engaged player, week 4+), ≤ +30 선택 기억편/week, ≤ +80 lineage 기억편/week; the basket's 별조각 line is identical with every R2 modifier at maximum; no new spendable currency; no fourth permanent growth axis (R2 adds exactly three: 수선 비법, 한 땀 돌파 with 추억 수놓기, 기념품 선반).
8. **Art:** Higgsfield `gpt_image_2_5`, quality **max**, one asset per image, existing art as `image_references`, the ART-UPGRADE-PLAN prompt assembly and QA gate, and its balance floor. Every R2 asset has a code fallback, so no system waits for art (§9.1).
9. **Audio:** CC0 packs + original synthesis through the AUDIO-SPEC pipeline (orchestrator decision). No Higgsfield SFX or music model (`mirelo_text_to_audio`, `sonilo_music`, `inworld_text_to_speech` are never called). R2 code calls `AudioService` with string keys resolved once (§9.2).
10. **Orchestrator decisions applied:** no NPC league and no NPC names on thresholds or ladders; no restyle of the udu/lala lineages (their R2 art references the current forms); prestige, gear gacha and summon level stay rejected.

### 0.3 Start conditions (all must hold before wave R2-A starts)
1. SPEC4 wave 4.1 has merged and 4.2 verification is green (R2 depends on `IMetaModule`, `EventCalendarSystem`, `CodexSystem`, `AchievementSystem`, `AffinitySystem`, `DailyGiftSystem`, `CheerSystem`, `StrengthenAdvisor`, `ChapterChestSystem`, `SweepMany`, `ResendAll`, `BattleSetup.Protection`, `PresentationScale`).
2. The **curve package** (§4.1–§4.3) is merged: second enemy taper at stage 151, `goldPerSecondGrowth` 1.06, basket `stageGrowth` 1.0025, SPEC4 C-01 (`maxStageMultiplier` 1.5 on the 별조각 line) on, and the power rating repair. If SPEC4's P4-9 did not land the first three, the R2-0 package R2P-C lands them (§8.1). (CR4-18) Under PHASE4-ROADMAP RD-1 the curve lands in RM-1 as P4-C and the rating repair in RM-2 (P4-0/P4-1/P4-8), so R2P-C only confirms. SPEC4's ch6+ chapter-chest row is changed per RD-13 (CR4-01).
3. SPEC4 stretch items have a status. If NEW-03 (fireflies) or NEW-04 (dungeon bank) were cut, R2-15 and R2-07 ship them exactly as SPEC4 §13.8/§13.9 specify, then extend them. If PRG-07 (blessings) was cut, R2-10 builds `BlessingPickPopup` itself (L+).
4. UX-SPEC-IA WP-IA acceptance is green (frame, rails, sheet, menu) and the UX wave 2 packages have merged.
5. The R2-0 shell exception (§2.6) is answered. "Yes" → hub sheets; "no" → the popup fallback in §2.6. Either way wave A can start.

### 0.4 Decisions this spec takes (differences from IDLE_RESEARCH2 v1.2)
IDs `S5-n` are SPEC5 decisions (not IDLE_RESEARCH2's deferred list D-01…D-21, which keeps its own IDs).

| # | Topic | IDLE_RESEARCH2 v1.2 | SPEC5 | Why |
|---|---|---|---|---|
| S5-1 | Late curve (CR2-08) | open question | second enemy taper at 151 (HP ×1.02, damage ×1.015) + the two CR-07 settings; R2-04/R2-05 constants unchanged (§4) | PROGRESSION-SIM §0, 227 runs |
| S5-2 | Acceptance for R2-04 | need/rec 0.80–1.05 to the day-60 stage | the projection checks of §4.4 (chapter timeline + stall + income bands); the rating colours become informative again through §4.3 | the sim shows TeamPower is dominated by shop HP, so need/rec cannot be checked with today's formula |
| S5-3 | Unlock pacing | stage gates 2-2 … 5-1, "≤4 cards per chapter" | stage gates **plus** a play-time floor (`CounterKey.PlayMinutes`) and a global card spacing of 180 s of play (§2.8) | the sim: an engaged player clears chapters 1–5 in 14 minutes and chapter 10 after 34 minutes, so every R2 unlock up to 5-1 would fire inside the first quarter hour |
| S5-4 | Chapter-2 cards (merge-log Q1) | R2 guest → NEW pill; suggest a shared 2-1 card to SPEC4 | both applied: WeeklyGuest has no card (NEW pill + first-entry explainer); SPEC4's AutoChallenge card text covers ExpeditionBatch, whose own card turns off (R2P-5 owns that content in phase 5) | merge-log consistency note 1 |
| S5-5 | Hub surface (merge-log Q2) | sheet preferred, popup fallback | hub sheets through a 25-line shell extension of `MainTabController` (hub panels 6 and 7, no tab button); popup fallback specified (§2.6) | the battle stays visible above the hubs, as in every other sheet |
| S5-6 | 맞춤 편성 storage | `FormationSave.parkedMain` snapshot, swap main formation, restore on exit or load | **no swap**: the biased formation is a per-attempt `FormationSnapshot` used only by the side setup; the main formation is never modified; `parkedMain` is not added | removes the killed-app risk and the "main hunt runs with the mode formation" window entirely; the formation tab never shows a temporary formation |
| S5-7 | 맞춤 편성 and expeditions | members away on an expedition are skipped | they are **included** | the code rule "원정 중에도 함께 싸워요" (`FormationSystem.AutoFormation`, UX M05) |
| S5-8 | Errand scaling | s(L) = requiredStage + 2(L−1), climb one level per clear | s(L) = requiredStage + **5**(L−1), and the stepper may start at `LevelForStage(highestStage) − 2` (skip-ahead); milestone chest **별조각 10** + bundle (was 30) | the sim's engaged player is at stage ≈100 on day 0 and ≈450 on day 30; ticket-limited climbing would keep errands trivially easy and still pay ≈84 별조각/week; this keeps errands a real power check and bounds gems by stage progress |
| S5-9 | Tower curve | s(f) = 6 + k·f, k ≈ 0.75–1.0 from CR-07 | s(f) = **30 + 3f**; boss floors pay materials, trial floors pay 별조각 20 + 선택 기억편 2; milestones at 20/40/60/80; a headless '자동으로 오르기' batch | the sim's day-60 stage is ≈547 (not ≈100); a 3-stage step gives ≈180 floors by day 60, so a floor still moves after most upgrades, and R2's 7 별조각/floor table would then pay ≈240/week in the first month — the gems move to trial floors only |
| S5-10 | Tower name | '분실물 보관소' | mode name **'보관소 오르기'** (the place is still the 분실물 보관소) | expedition zone `exp_2_3` and story chapter 4 are both named '분실물 보관소' |
| S5-11 | Guest reward grant | granted in the reveal; unclaimed → permanent mail | **granted at the week rollover** (MetaRewards); the reveal popup only presents it | mail cannot carry wallet items (선택 기억편), SPEC4 §4.5/§13.6 |
| S5-12 | Guest bank | +1 attempt per fully unused day (max 4) | same, and each banked attempt scores as a separate catch-up best (4 × tier stitches) | otherwise banked attempts return no value to a player who skipped a day |
| S5-13 | Notification kinds | append ResearchDone, LanternsFull, ErrandTicketsFull | **no new `NotificationKind`**: errand tickets and lanterns fold into `DungeonTickets` ('입장권'), research folds into `ExpeditionDone` (new planner ids 1005/1006) | every kind needs its own Settings switch; v1.2 itself says "adds no notification kinds" |
| S5-14 | `ExpeditionSave.zoneCompletions` | on `ExpeditionSave` | on a new account-level `RouteMarkSave` | `ExpeditionSave` is one *running* expedition (`PlayerData.expeditions`), not a zone record |
| S5-15 | Lantern overflow (등불 길) | overflow → mail, 6 days | overflow bank of up to 3 inside `WalkSave`, each expiring after 6 DayKeys | mail carries `RewardData` only |
| S5-16 | Borrowed friend slot | an empty direction or "echo slot 7" | empty guardian pad → empty deck slot → replaces the weakest deck echo for that walk | there is no 7th deck slot; adding one changes the 156 px strip and frozen coach paths (the critic's own reason for cutting 덱 7칸) |
| S5-17 | Workbench '귀환 잔향 +5%' | node | replaced by '잔향이 이동 속도 +3%' (`EchoMoveSpeedPercent`) | no `StatType` exists for returned resonance; adding one is sim work for a minor node |
| S5-18 | Workbench slots 2/3 | at stages 7-1 / 9-1 | after 8 / 24 completed research jobs | stage gates fire within the first hour (S5-3) |
| S5-19 | Extra costume sources in wave B (tower 40, depth 5, festival final) | planned | **optional** (B-opt, needs 3 more costumes and 15 credits); the season-costume cadence (one per Q-06 season) is core | art budget; avoids moving the R2-36a unlock table after players own them |
| S5-20 | `MetaConditionType 25` | `MaxLineageLevel` | `CapBreaks` (Σ breaks over lineages) | `LineageLevel` with an empty `targetId` already reads the highest lineage; records need the break total |
| S5-21 | Keepsakes | 31 | 32 (adds the attendance day-28 keepsake R2-12 mentions) | R2-12 text |
| S5-22 | Hub dot naming | `map.outings`, `map.home` | `top.outings`, `top.home` (rail icons use `top.*` like 이벤트/출석부/수선 일지) | `RedDotPaths` convention |
| S5-23 | `FeatureId.Home` | derived, not stored | stored, condition = `FeatureUnlocked Keepsakes` or `FeatureUnlocked Workbench`, no card | `QuickIconView.IsAvailable` needs one FeatureId |
| S5-24 | Power rating | PRG-01 as SPEC4 | shop term capped at the offense term, and a third recommended-power segment from stage 151 (§4.3) | the sim: power/rec runs from 30× to 900,000× on the shipped economy and collapses the other way once the economy is retuned |

---

## 1. Coverage matrix (what is left and where it goes)

"Wave" is the R2 wave (§8.1). Packages: R2P-0 contracts, R2P-C curve, R2P-1 battle, R2P-2 meta systems, R2P-3 growth, R2P-4 UI (4a wave A, 4b/4c wave B), R2P-5 content, R2P-6 art and audio, R2P-7 verify.

| ID | Name | P · size | Wave | SPEC5 adds | § · packages |
|---|---|---|---|---|---|
| R2-00 | Shared infrastructure | – · L | R2-0 | side-battle host, side scripts and 3 gimmicks, time-sliced runner, fast-forward, rule-table types, two hub sheets, explainer strip, result card, v5 save, enums, defs, red dots, pacing | §2, §3 · R2P-0, R2P-1, R2P-4a |
| curve | Second taper + CR-07 retune + rating repair | P0 · S | R2-0 | 3 BattleConfig fields, 2 economy constants, shop-share cap, 3-segment rec, projection checks | §4 · R2P-C, R2P-5, R2P-7 |
| R2-03 | 이번 주의 손님 | P0 · M | A | weekly guest, 5-layer shell fight at the door, stitch meter, 25 tiers, Monday reveal, 모두 함께, rumour, bank | §5.1 · R2P-1/2/4a/5/6 |
| R2-04 | 수선 비법 | P0 · M | A | 7 rows (+1 in B), 수선공 등급, [수선 비법] segment in 강화 | §5.2 · R2P-3/4a/5 |
| R2-05a | 한 땀 돌파 + 추억 수놓기 | P0 · S–M | A | cap 100 → 200 in 5 breaks, stitch marks, embroidery sink | §5.3 · R2P-3/4a/5/6 |
| R2-01 | 밤 골목 심부름 (errands 1–2) | P0 · M | A | 그림자 까치 쫓기, 빨랫줄 걷기, tickets 2/day store 6, levels with skip-ahead, sweeps, style favours | §5.4 · R2P-1/2/4a/5/6 |
| R2-07 | Banking + 오늘의 순회 | P0 · S | A | errand/guest/free-quick banks, kept daily gift, 놓친 선물 rollover claim, one-tap rounds | §5.5 · R2P-2/4a |
| R2-08 | 맞춤 편성 | P0 support · S | A | `FormationSystem.BuildBiased`, per-mode switch, preview | §5.6 · R2P-3/4a |
| R2-36a | 옷장 넓히기 + fitting room | P0 · S | A | 6 costumes, `CostumeUnlockType.Reward`, preview | §5.7 · R2P-3/4a/5/6 |
| R2-02 | 보관소 오르기 (tower) + rule effects | P1 · M | B1 | floors, blocks, trial rules (rule-table effects), auto-climb, auto-retry | §6.1 · R2P-1/2/4b/5/6 |
| R2-09 + R2-20 | 이름표 + 대표 친구·수선점 이름 | P1 · S + S | B1 | 37 titles, profile popup, partner effects, shop name | §6.2 · R2P-2/4c/5 |
| R2-10 | 등불 길 (MVP) | P1 · L | B2 | 15-step run, 40 charms, 21 crests, 10 depths, 8 talents, borrowed friend | §6.3 · R2P-1/2/4b/5/6 |
| R2-11 | 밤의 작업대 + 안채 | P1 · M–L | B2 | 3 branches, 19 nodes, tier gating, timers, 안채 hub | §6.4 · R2P-2/4c/5/6 |
| R2-12 | 기념품 선반 | P1 · L | B2 | 32 keepsakes, placed conditional effects, shine, shelf slots | §6.5 · R2P-1/2/4c/5/6 |
| R2-13 + R2-14 | 수선 축제 주간 + 보름달 밤 | P1 · M + S | B1 | monthly festival rounds, moon nights (real moon) | §6.6 · R2P-2/4c/5/6 |
| R2-15 | 반딧불 등롱 | P1 · S | B1 | kill-gauge lantern, 1-of-4 notes, moth, modifier variants | §6.7 · R2P-1/2/4c/5/6 |
| R2-17/18/19 | 단골 길 · 주말 골목 장터 · 재료 엮기 | P1 · S each | B1 | route marks, weekend market, merge (+ [모두 엮기]) | §6.8 · R2P-2/4c/5 |
| R2-36b step 1 | 진화 가지 잇기 | P1 · M + art | B2 | +1 tier-2 form for each of the six 3-form lineages | §6.9 · R2P-5/6 |
| R2-37 | 골목 지도 넓히기 (first new place) | P1 · M + art | B2 | `PlaceDef`, place 2 '야시장 골목' (ch6+), its boss, place rule | §6.9 · R2P-1/5/6 |
| R2-05b | 스킬 각성 | P1 · M | B2 | 21 riders at 공명 3/6/9, 3 sim additions | §6.10 · R2P-1/4c/5 |
| R2-01 errand 3 | 다시 뭉치는 먼지 왕 | P1 · S | B1 | Reforming gimmick, 10 tiers | §6.10 · R2P-1/2/5/6 |

**Out of scope (P2, waves C/D):** R2-16, R2-21, R2-22, R2-23, R2-26, R2-27, R2-29, R2-32, R2-33, R2-38/38b, R2-39, R2-40. **Deferred / cut:** D-01…D-21 and the cuts of IDLE_RESEARCH2 Appendix C. **Reserved numbers** (never reuse): `CounterKey` 53 BoxHuntOpen, 55 CallEarly, 56 RequestClear · `FeatureId` 37 DreamWalk · `MetaRewardType` 12 Track, 13 Tint · `BattleMode` 6 Endless, 7 Request · `EventScheduleType` 6 DateList · `CurrencyType` 3 (실밥, wave D only if approved).

---

## 2. Shared contracts (R2P-0 owns all of §2; everyone else compiles against it)

### 2.1 Save schema v5 (`Core/PlayerData.cs`, `Core/SaveData.*.cs`)
`PlayerData.CurrentVersion = 5`. In `Sanitize`: `bool v5Migration = version < 5;` and after `meta.Sanitize()` (and after SPEC4's `MigrateToV4`): `if (v5Migration) meta.MigrateToV5(ftueDone: meta.ftue.done);`.
`MigrateToV5` only sets `titles.backfillPending = true` (so '수선점 주인' is granted to saves past FTUE, §6.2) and puts `"guest"` into `rounds.disabledSteps` (the guest step is off by default, §5.5). Every other field defaults to empty. Errand tickets start at −1 ("first refill pending"), so the first access grants one day's tickets, not the cap.

| Class (file) | New fields (default) | Sanitize |
|---|---|---|
| `MetaSave` (SaveData.Meta.cs) — wave A | `ErrandSave errands`, `WeeklyGuestSave guest`, `RoundsSave rounds`, `TechniqueSave techniques` (all `= new()`) | `??= new` + child Sanitize |
| `MetaSave` — wave B (declared now, used later) | `TowerSave tower`, `TitleSave titles`, `ProfileSave profile`, `LanternSave lantern`, `WalkSave walk`, `WorkbenchSave workbench`, `KeepsakeSave keepsakes`, `MarketSave market`, `MergeSave merge`, `RouteMarkSave routes` (all `= new()`) | same |
| `ErrandSave` | `Dictionary<string, ErrandProgress> byId` | null→new; drop null values |
| `ErrandProgress` | `int highestLevel` (0 = none cleared), `int tickets = -1`, `string refillDayKey`, `int fullClears`, `HashSet<int> milestones` (claimed multiples of 10), `int lastLevel` (stepper memory), `bool autoForm = true`, `int bestTier` (errand 3) | clamp tickets −1..cap+bonus, levels ≥ 0 |
| `WeeklyGuestSave` | `string weekKey`, `int guestIndex = -1`, `int hpStage` (0 = not snapshotted), `long stitches`, `string stitchDayKey`, `int stageStitchesToday`, `int expStitchesToday`, `int bestTierToday`, `int attemptsUsedToday`, `string attemptDayKey`, `int bankedAttempts`, `int bestTierEver`, `int weeksLikeNew` (weeks finished at 새것처럼+), `int weeksFullBloom` (weeks finished at 추억 가득), `bool costumeGranted`, `bool autoForm = true`, `HashSet<string> rewardedWeeks`, `HashSet<string> seenGuests`, `GuestRevealSave reveal` (null = none) | clamp; drop `rewardedWeeks` older than 30 weeks |
| `GuestRevealSave` | `string weekKey`, `int guestIndex`, `int tier`, `bool shown` | – |
| `RoundsSave` | `HashSet<string> disabledSteps`, `string lastRunDayKey`, `List<RewardSummary> missedSummary`, `string missedDayKey`, `bool shownMissed` | null→new |
| `RewardSummary` (new, SaveData.Meta.cs) | `MetaRewardType type`, `CurrencyType currency`, `string id`, `double amount` | – |
| `TechniqueSave` | `Dictionary<string,int> levels`, `int buyMode` (0 ×1 · 1 ×10 · 2 MAX) | clamp 0..cap; drop unknown ids at Connect, not in Sanitize |
| `LineageSave` (SaveData.Characters.cs) | + `int capBreaks` (0..5), + `int embroidery` (0..20) | clamp |
| `IdleChestSave` | + `int bankedFreeQuick` (0..1), + `string bankedFreeQuickDayKey` | clamp |
| `DailyGiftSave` (SPEC4 NEW-02) | + `string keptDayKey` (null = none) | – |
| `EventInstanceSave` (SPEC4 EVT-01) | + `int roundsClaimed` (0..4), + `int milestonesClaimed` (0..20) | clamp |
| `TowerSave` (B) | `int highestFloor`, `bool autoRetry = true`, `long lastFailPower`, `long lastAutoUnix`, `int trialBonusCount` (0..10), `bool autoForm = true` | clamp |
| `TitleSave` (B) | `HashSet<string> owned`, `string equipped`, `bool backfillPending` | equipped must be owned, else null |
| `ProfileSave` (B) | `string shopName` (null = default), `int renames`, `string partnerLineageId`, `string partnerBonusDayKey`, `bool namingOffered` | name length ≤ 8 after trim, else null |
| `LanternSave` (B) | `float points`, `int stored` (0..3), `int[] noteTiers = new int[4]`, `string openDayKey`, `int opensToday`, `string shardNoteDayKey`, `float killsPerMinute` | resize array to 4; clamp |
| `WalkSave` (B) | `WalkRunSave current` (null = no run), `int lanterns = 3`, `long lastLanternUnix`, `List<long> overflowExpiry` (≤3), `int highestDepth`, `HashSet<string> talents`, `string weekKey`, `int rewardWalksThisWeek`, `bool weeklyChestClaimed`, `string dayKey`, `int walksToday`, `HashSet<string> borrowedSeen` | clamp; drop expired overflow |
| `WalkRunSave` (B) | `int seed`, `int depth`, `int step` (0..15), `float shopHp` (fraction), `List<string> charms` (`"{id}#{tier}"`), `string borrowedFormId`, `string lenderId`, `List<string> offer`, `string pendingNode`, `bool merchantUsed`, `int rerollsLeft`, `List<string> dozing` (form ids, '졸음 길'), `List<string> recentFighters`, `long startedUnix` | null lists → new |
| `WorkbenchSave` (B) | `Dictionary<string,int> levels`, `List<ResearchJob> jobs`, `int researchDone` | drop jobs with unknown nodes |
| `ResearchJob` (B) | `string nodeId`, `int toLevel`, `int slot`, `long startUnix`, `long endUnix`, `long pausedSeconds` | – |
| `KeepsakeSave` (B) | `Dictionary<string,int> shine` (owned = key present, 0..5), `string[] placed = new string[5]`, `Dictionary<string,int> zoneTrips`, `HashSet<string> pending` (earned before the shelf unlocked), `HashSet<string> seen` | resize to 5; drop placed ids not owned |
| `MarketSave` (B) | `string weekKey`, `Dictionary<string,int> bought` | – |
| `MergeSave` (B) | `string dayKey`, `Dictionary<string,int> used`, `bool autoWeave` | – |
| `RouteMarkSave` (B) | `Dictionary<string,int> completions` (zone id → claims) | clamp ≥ 0 |
| `PlayerData.flags` (no schema change) | `hint.mode.{errand,guest,tower,walk}` (explainer seen), `hint.rounds.first`, `hint.fitting`, `r2.guest.first_bloom` | – |

**Not added (SPEC4 already has them, or removed by S5-6/S5-14):** `FormationSave.parkedMain`, `MetaSave.firefly` (SPEC4 `FireflyDaySave`), `DungeonSave.bankedTickets` (SPEC4 `ticketBank`), `ExpeditionSave.zoneCompletions` (→ `RouteMarkSave`), `SproutSave`, `ShopExtraSave`, `CurrencyType.Footprint`.

### 2.2 Enum appends (`Core/GameEnums.cs` unless noted)
```csharp
// CounterKey (SPEC4 ends at LostChestOpen = 37) — explicit values; names are saved, never rename
ErrandRun = 38,          // 심부름 입장권을 쓴 횟수 (직접 다녀오기 + 소탕 1장당 1)
ErrandClear = 39,        // 심부름을 끝까지 해냄 (그 단계 완료 결과: 까치 100% · 둥지 5개 · 먼지 왕 7단계 이상)
TowerFloorClear = 40,    // (B) 보관소 오르기 새 층 (자동 오르기 포함)
GuestAttempt = 41,       // 이번 주의 손님 도전 (직접 + 순회 자동)
GuestStitches = 42,      // 손님 바늘땀 (얻은 양만큼)
WalkStart = 43,          // (B) 등불 길 출발
WalkClear = 44,          // (B) 등불 길 15칸 완주
FireflyTap = 45,         // (B) 길 잃은 반딧불(NEW-03) 잡기 (나방 포함)
LanternOpen = 46,        // (B) 반딧불 등롱 열기
TechniqueLevelUp = 47,   // 수선 비법 레벨 (오른 만큼)
CapBreak = 48,           // 한 땀 돌파
ResearchStart = 49,      // (B) 작업대 연구 시작
ResearchDone = 50,       // (B) 작업대 연구 완료
KeepsakeGet = 51,        // (B) 기념품 새로 얻음
MaterialMerge = 52,      // (B) 재료 엮기 (만든 개수)
// 53 BoxHuntOpen — reserved (R2-16, P2)
MaterialSpent = 54,      // (B) 제작·연구·돌파·닦기에 쓴 재료 개수
// 55 CallEarly, 56 RequestClear — reserved (R2-39, R2-38b, P2)
PlayMinutes = 57,        // 앱을 켜 둔 시간 (분, 잠자기 포함) — 기능이 열리는 속도 조절 (§2.8)

// FeatureId (SPEC4 ends at BattleStats = 26)
Outings = 27,            // 밤 나들이 (지도 아이콘 + 시트)
Errands = 28,            // 밤 골목 심부름
WeeklyGuest = 29,        // 이번 주의 손님
DailyRounds = 30,        // 오늘의 순회
Techniques = 31,         // 수선 비법
LevelBreak = 32,         // 한 땀 돌파
Tower = 33,              // (B) 보관소 오르기
NightWalk = 34,          // (B) 등불 길
Workbench = 35,          // (B) 밤의 작업대
Keepsakes = 36,          // (B) 기념품 선반
// 37 DreamWalk — reserved (R2-21, P2)
Home = 38,               // (B) 수선점 안채 (조건 = 작업대 또는 기념품 선반이 열림, 카드 없음)

// MetaRewardType (SPEC4 ends at FriendSelectTicket = 8)
Title = 9,               // (B) 칭호 (itemId = 칭호 ID)
Keepsake = 10,           // (B) 기념품 (itemId = 기념품 ID; 선반이 잠겨 있으면 pending)
WalkLantern = 11,        // (B) 등불 (amount개, 상한 넘으면 보관 칸)
// 12 Track, 13 Tint — reserved (P2)

// MetaConditionType (SPEC4 ends at CostumesOwned = 24)
CapBreaks = 25,          // 모든 계보의 한 땀 돌파 합 ≥ target
TowerFloor = 26,         // (B) 보관소 최고 층 ≥ target
GuestTier = 27,          // 손님 등급 (targetId "week" = 이번 주 단계 ≥ target, "weeks" = 새것처럼 이상으로 마친 주 수 ≥ target, "bloomWeeks" = 추억 가득으로 마친 주 수 ≥ target)
ErrandLevel = 28,        // targetId 심부름 최고 단계 ≥ target (비면 가장 높은 심부름)

// MetaPopupKind (SPEC4 ends at NotificationAsk = 9)
GuestReveal = 10,        // 지난주의 손님 (접속 종류, 복귀 다음 — §2.9)

// BattleMode (code: Stage 0, EchoDungeon 1)
Errand = 2, Tower = 3, GuestShadow = 4, NightWalk = 5,   // 6 Endless, 7 Request reserved

// EnemyGimmick (flags; code ends at LampDrain = 1 << 8)
Fleeing = 1 << 9,        // 공격하지 않고 레인을 옮겨 다님 (심부름 까치; approach면 다가오며 옮김 — 야시장 까치)
Spawner = 1 << 10,       // 제자리 둥지: 켜져 있는 동안 minionId를 minionInterval마다 부름
Reforming = 1 << 11,     // (B) 쓰러지면 더 단단하게 다시 뭉침 (먼지 왕)

// CostumeUnlockType (SPEC4 adds Season = 7)
Reward = 8,              // 보상(MetaReward.Costume)으로만 받는 의상 — 스스로 열리지 않음

// StatType (code ends at GoldGainPercent = 22)
DoubleHitChancePercent = 23,   // (B) 두 땀 뜨기: 같은 투사체가 한 번 더 맞힘 (숫자는 '×2' 하나로 합침)

// SPEC4 enums, appended
// EventKind (0..3): FestivalRounds = 4, MoonNight = 5            (B)
// EventScheduleType (0..3): MoonPhase = 4, MonthRotation = 5      (B; 6 DateList reserved)
// AchievementCategory (SPEC4: 17 members, Stage 0 … Hidden 16; CR4-19 explicit values): Errand = 17, Tower = 18, Guest = 19, Walk = 20, Technique = 21, Keepsake = 22, CapBreak = 23, Firefly = 24
// CodexCategory (0..5): Keepsake = 6 (B), Guest = 7
// StrengthenKind (0..8): Technique = 9, CapBreak = 10, TrialRule = 11 (B)

// SkillTrigger (code ends at OnBattleStart = 8) — wave B, R2-05b
OnWaveFirstReach = 9,    // 웨이브마다 수선점에 처음 닿는 적이 생길 때
EveryNthCast = 10,       // 이 스킬의 n번째 시전마다 (SkillEffectDef.count = n)

// BattleEventType (Systems/Battle/BattleTypes.cs, not saved — append at the end)
ObjectivePhase,          // 목표 적이 껍질을 벗음 / 다시 뭉침. SourceId = 적 ID, Value = 지난 단계 수
ObjectiveHop,            // 목표 적이 레인을 옮김. SourceId = 적 ID, Value = 새 레인
ShopRevived,             // (B) 문장 효과로 수선점이 되살아남. Value = 회복량

// new enums (end of GameEnums.cs)
public enum SideRuleEffect { EnemyHpScale = 0, EnemyDamageScale = 1, EnemySpeedScale = 2, WaveSizeScale = 3, EliteEvery = 4, ShopHpScale = 5,
                             ResonanceRegenScale = 6, EchoCostDelta = 7, ClosedPads = 8, LampOff = 9, EchoLifetimeScale = 10, ExtraModifier = 11, Roster = 12 }
public enum RosterRule { None = 0, NoEchoes = 1, MaxSameStyle = 2, MaxDeckSlots = 3, MaxGuardians = 4, TraitOnly = 5 }
public enum RoundsStep { Basket = 0, QuickIdle = 1, Expeditions = 2, Dungeon = 3, Errands = 4, Guest = 5, Tower = 6, Claims = 7 }
public enum ErrandKind { Magpie = 0, Laundry = 1, DustKing = 2 }
public enum GuestBand { Crack = 0, Shine = 1, LikeNew = 2, FullBloom = 3 }   // 실금 · 반짝 · 새것처럼 · 추억 가득
public enum OutingsPage { Hub = 0, Errands = 1, Errand = 2, Guest = 3, Tower = 4, Walk = 5 }
public enum HomeRoom { Hub = 0, Workbench = 1, Keepsakes = 2 }
public enum WalkNode { Fight = 0, Charm = 1, Chest = 2, Elite = 3, Rest = 4, Merchant = 5, Boss = 6 }
public enum CharmRarity { Common = 0, Rare = 1, Dark = 2 }
public enum WorkbenchBranch { Thread = 0, Thimble = 1, Basket = 2 }
public enum KeepsakeSource { Affinity = 0, Souvenir = 1, Tower = 2, Guest = 3, Attendance = 4 }
public enum KeepsakeCondition { Always = 0, Modifier = 1, NotModifier = 2, Mode = 3, ChapterBoss = 4, NightHours = 5, LineagePresent = 6, TraitCount = 7 }   // all evaluated at setup time
public enum LanternNote { Gold = 0, Materials = 1, Candy = 2, Shards = 3 }
```
`MetaSession.IsHudToggle` is unchanged. `RedDotPaths.ParentOf` gains the rows in §2.7.

### 2.3 MetaDatabase additions (`Data/MetaDatabase.cs`)
New `[Header]` blocks with empty defaults; each gets a lookup cache cleared by `ClearCache()`. R2P-0 adds the shapes, R2P-5 the content (`Editor/Content/MetaContent.R2*.cs`).
```csharp
public SideBattleConfig sideBattle = new();               // R2-00
public List<SideRuleDef> sideRules = new();               // R2-00 (effects ship in B)
public List<ErrandDef> errands = new();                   // R2-01
public GuestConfig guest = new();                         // R2-03
public List<TechniqueDef> techniques = new();             // R2-04
public TechniqueRankConfig techniqueRank = new();         // R2-04
public CapBreakConfig capBreak = new();                   // R2-05a
public RoundsConfig rounds = new();                       // R2-07
public TowerConfig tower = new();                         // (B) R2-02
public List<TitleDef> titles = new();                     // (B) R2-09
public ProfileConfig profile = new();                     // (B) R2-20
public LanternConfig lantern = new();                     // (B) R2-15
public WalkConfig walk = new();                           // (B) R2-10
public WorkbenchConfig workbench = new();                 // (B) R2-11
public List<KeepsakeDef> keepsakes = new(); public KeepsakeConfig keepsakeShelf = new();   // (B) R2-12
public List<FestivalThemeDef> festivals = new();          // (B) R2-13
public MoonConfig moon = new();                           // (B) R2-14
public RouteMarkConfig routeMarks = new();                // (B) R2-17
public MarketConfig market = new();                       // (B) R2-18
public List<MergeRecipeDef> merges = new();               // (B) R2-19
public List<PlaceDef> places = new();                     // (B) R2-37
public List<SkillRiderDef> riders = new();                // (B) R2-05b
public IncomeConfig income = new();                       // (CR4-05) 단추 N분치 follow real income (§4.6)
// existing classes gain
FeatureUnlockDef.alsoRequire;   // List<MetaCondition>, ALL must hold in addition to one of `conditions` (§2.8)
IdleChestConfig.offlineGoldMinutesPerHour;   // (CR4-05) double, 0 = today's offlineGoldRate × GoldPerSecond path; > 0 = the night basket's 단추 line pays this many 단추 N분치 per hour away (§4.6)
PowerConfig.recommendedTaperStage2; PowerConfig.recommendedGrowthLate2; PowerConfig.shopShareCap = 1f;   // §4.3
AffinityLineDef.keepsakeId;     // (B) granted at affinity Lv6 (SPEC4 CR-10 class)
```
Definitions (all `[Serializable]`, public fields, Korean tooltips):
```csharp
class SideBattleConfig { float finishNowAfter = 5f; float fastForwardMsPerFrame = 8f; float headlessMsPerFrame = 2f;
                         float resultAutoResume = 10f; List<ModeIntroDef> intros; }
class ModeIntroDef { BattleMode mode; string[] titles = new string[3]; string[] lines = new string[3]; Sprite[] art = new Sprite[3]; }
class SideRuleDef { string id; string name; string desc; string iconKey; int risk = 1; string group; SideRuleEffect effect; float value;
                    StageModifierType modifier; RosterRule roster; TraitType trait; }
class ErrandDef { string id; string name; string desc; ErrandKind kind; string bannerKey; int requiredStage; int stagePerLevel = 5;
                  float timeLimit = 60f; int ticketsPerDay = 2; int ticketCap = 6; CombatStyle favouredStyle; float favouredPercent;
                  float rumourPercent = 15f; string targetEnemyId; float targetHpBossMultiple = 3f; float orbitRadius = 1.75f; float hopInterval = 3f;
                  int nestCount = 5; float nestHpMultiple = 8f; float nestRadius = 1.85f; float nestInterval = 2f; int nestMaxAlive = 6;
                  string nestMinionId = "enemy_dust"; float reformStep = 0.10f; float tierParBossMultiple = 4f; int tiers = 10;
                  double rewardScale = 1; List<MetaReward> milestoneRewards; FeatureId feature = FeatureId.Errands; }
class GuestConfig { string anchorMonday = "2026-11-02"; int attemptsPerDay = 3; int bankMax = 4; int hpStageOffset = 5; float layerHpBossMultiple = 2f;
                    float newShellScale = 1.5f; int layers = 5; int maxTier = 10; float timeLimit = 90f; float favouredPercent = 30f;
                    float perOwnedFormPercent = 1f; float ownedFormCapPercent = 30f; int stitchPerTier = 4; int stageStitchCap = 60; int expStitch = 3;
                    int expStitchCap = 24; int basketStitchPerHour = 2; int basketStitchCap = 24; int[] tierThresholds = new int[25];
                    List<GuestBandReward> bandRewards; string firstBloomCostume = "udu_raincoat"; List<GuestDef> guests; int launchCount = 4; }
class GuestDef { string id; string name; TraitType[] traits = new TraitType[2]; string portraitKey; Color tint; string thanks; string mementoId;
                 bool launch; int order; Vector2[] patches = new Vector2[3]; }   // reveal stitch patches, portrait-local 0..1
class GuestBandReward { int tier; int gems; List<MetaReward> rewards; bool memento; }       // tier 1..25
class TechniqueDef { string id; string name; string iconKey; StatType stat; float perLevel; int cap; string requiresId; int requiresLevel;
                     double baseCost; double costGrowth = 1.10; FeatureId feature = FeatureId.Techniques; bool waveB; }
class TechniqueRankConfig { int levelsPerRank = 25; float multiplierPerRank = 1.02f; }
class CapBreakConfig { int levelsPerBreak = 20; List<CapBreakStep> steps;   // 5 rows (Unity cannot serialize List<T>[])
                       double goldNextLevelMultiple = 10; int embroideryShards = 30; float embroideryPercent = 0.5f; int embroideryMax = 20; }
class CapBreakStep { int shards; List<MaterialCost> materials; }
class MaterialCost { string itemId; int amount; }
class RoundsConfig { List<RoundsStep> order; List<RoundsStep> defaultOff = new() { RoundsStep.Guest }; }
class TowerConfig { int stageBase = 30; int stagePerFloor = 3; float normalLimit = 45f; float bossLimit = 60f; float trialLimit = 60f; int bossEvery = 5;
                    int trialEvery = 10; float waveSizeScale = 1.5f; StageModifierType[] blockCycle; List<string> trialRuleIds; List<MetaReward> floorReward;
                    List<MetaReward> bossReward; List<MetaReward> trialReward; float trialBonusPercent = 0.5f; int trialBonusMax = 10;
                    List<TowerMilestone> milestones; float autoRetryPowerGain = 1.03f; int autoRetryMinutes = 10; int autoClimbMax = 30; string[] shelfNames; }
class TowerMilestone { int floor; string titleId; string keepsakeId; }
class TitleDef { string id; string name; string source; FeatureId feature; }
class ProfileConfig { string defaultName = "작은 수선점"; int maxLength = 8; double renameGoldMinutes = 60; int partnerAffinity = 10; string[] blockedWords;
                      List<PartnerLineDef> lines; }
class PartnerLineDef { string lineageId; string returnLine; string notifyLine; }
class LanternConfig { int baseKills = 300; int killsPerChapter = 20; int maxKills = 500; float fireflyBonus = 0.10f; int store = 3; int opensPerDay = 6;
                      float offlineRate = 0.5f; float[] tierMultiplier = { 1f, 1.4f, 1.8f, 2.2f, 2.6f, 3f }; List<MetaReward> goldNote, materialNote, candyNote;
                      float mothChance = 0.10f; int mothExtraEnemies = 3; int nightFrom = 21, nightTo = 5; }
class WalkConfig { int steps = 15; WalkNode[] layout; int lanternCap = 3; int regenHours = 8; int overflowMax = 3; int overflowDays = 6; int rewardWalks = 10;
                   int stageBelowHighest = 15; int stagePerDepth = 4; int stagePerFiveSteps = 2; float eliteHp = 1.8f; float eliteDamage = 1.3f;
                   float bossHp = 2f; float fightLimit = 30f; float restHeal = 0.30f; float merchantCost = 0.20f;
                   List<CharmDef> charms; List<CrestDef> crests; List<WalkDepthDef> depths; List<WalkTalentDef> talents;
                   List<MetaReward> step5, step10, step15Extra, chestRewards, weeklyChest; int depthFirstClearGems = 50; List<LenderDef> lenders; }
class CharmDef { string id; string name; string desc; TraitType trait; CharmRarity rarity; StatType stat; float value; float value2;
                 StatType penaltyStat; float penalty; string iconKey; }
class CrestDef { TraitType trait; int count; StatType stat; float value; CrestSpecial special; float specialValue; string text; }
public enum CrestSpecial { None = 0, ShopReviveOnce = 1, LampResonance = 2, BossRage = 3, CritResonance = 4, EnemySlow = 5, ForceRain = 6 }
class WalkDepthDef { int depth; string ruleId; string name; }
class WalkTalentDef { string id; string name; string desc; }
class LenderDef { string id; string name; string line; }
class WorkbenchConfig { int[] slotUnlockAt = { 0, 8, 24 }; int tierGate = 10; float[] tierMinutes = { 20, 60, 180, 480 }; float levelTimeGrowth = 1.5f;
                        float capHours = 72f; double goldMinutesPerLevelTier = 20; float ticketSkipHours = 2f; List<WorkbenchNodeDef> nodes; }
class WorkbenchNodeDef { string id; string name; WorkbenchBranch branch; int tier; int maxLevel; StatType stat; float perLevel; int slot = -1;
                         WorkbenchEffect effect; List<MaterialCost> perLevelCost; }
public enum WorkbenchEffect { Stat = 0, BasketRate = 1, BasketCapHours = 2, ExpeditionLoot = 3, ErrandTicketCap = 4, ResearchSpeed = 5, PadRange = 6 }
class KeepsakeDef { string id; string name; string desc; KeepsakeSource source; string sourceId; int sourceValue; KeepsakeCondition condition;
                    string conditionId; int conditionValue; StatType stat; float value; KeepsakeEconomy economy; string hint; string iconKey; }
public enum KeepsakeEconomy { None = 0, ExpeditionReward = 1, ErrandTime = 2, StageGold = 3 }
class KeepsakeConfig { int[] slotShine = { 0, 0, 10, 25, 45 }; float shinePerStep = 0.20f; int maxShine = 5; float souvenirChance = 0.10f;
                       int souvenirGuarantee = 10; int souvenirRepeat = 20; List<MaterialCost> polishPerStep; double polishGoldMinutes = 30; }
class FestivalThemeDef { string id; string name; string desc; int index; List<FestivalPointSource> sources; long roundTarget; string titleId; Sprite banner; }
class FestivalPointSource { CounterKey counter; long per = 1; long points = 1; }
class MoonConfig { string epochUtc = "2000-01-06T18:14:00Z"; double synodicDays = 29.530588853; double fullAge = 14.77; int windowDays = 1;
                   float modifierShare = 0.5f; double candyMultiplier = 1.5; double candyMultiplierFull = 2.0; float basketBonus = 0.10f; }
class RouteMarkConfig { int[] thresholds = { 3, 10, 25, 60, 120 }; float lootPerMark = 0.08f; float souvenirBonus = 0.05f; float springChance = 0.05f; }
class MarketConfig { int slots = 6; List<MarketItemDef> pool; }
class MarketItemDef { string id; List<MetaReward> rewards; CurrencyType currency; double price; double goldMinutes; double listPrice; int limit; bool always; }
class MergeRecipeDef { string id; List<MaterialCost> inputs; double goldMinutes; string outputId; int outputAmount = 1; int dailyLimit = 5; }
class PlaceDef { string id; string name; int fromChapter; string fieldKey; List<PlaceEnemy> extraEnemies; List<string> bossIds; string ruleId; string bossHintId;
                 string ambKey; }   // (CR4-20) the place's ambience bed (AUDIO-SPEC key, e.g. "amb_market"); null = amb_night
class IncomeConfig { float referenceClearSeconds = 180f; bool enabled = true; }   // (CR4-05) §4.6; enabled = false restores today's GoldMinutes
class PlaceEnemy { string enemyId; float weight; }
class SkillRiderDef { string lineageId; int resonance; string text; SkillRiderKind kind; float value; SkillEffectDef extra; bool optionB; }
public enum SkillRiderKind { ValuePercent = 0, DurationAdd = 1, CountAdd = 2, ExtraEffect = 3 }
```

### 2.4 New systems, wiring and the module interface (`Systems/Meta/`, `Core/Game.cs`)
R2 systems implement SPEC4's `IMetaModule`. Two R2 needs are optional interfaces, so no SPEC4 module changes:
```csharp
/// <summary>05:00 전에 지난날을 마감해야 하는 모듈 (놓친 선물 · 어제 선물 · 보관). DailyClock.DayChanging/WeekChanging에서 MetaSession이 부른다</summary>
public interface IDayRolloverModule { void OnDayChanging(string oldDay, string newDay); void OnWeekChanging(string oldWeek, string newWeek); }
/// <summary>옆 전투(심부름 · 손님 · 보관소 · 등불 길) 결과를 받는 모듈. BattleSystem.SideBattleEnded를 MetaSession이 나눠 준다</summary>
public interface ISideBattleModule { BattleMode Mode { get; } void OnSideBattleEnded(SideBattleResult result); }
```
`MetaSession.AddModule` registers a module in these lists too when it implements them (an `is` check at add time). `DailyClock.DayChanging(old, new)` is raised **before** `DayChanged`, and `WeekChanging` before `WeekChanged` (§2.5), so a rollover module still reads the old period's counters through `ProgressTracker.GetPeriodValue`.

`Game.SetupMeta` creates these after SPEC4's systems, in this order, and adds each module to the session:

| Property | Class | Ctor deps (exact order) | Package | Wave |
|---|---|---|---|---|
| `Game.Errands` | `ErrandSystem` | data, db, DailyClock, Battle, Power, Formation, MetaRewards, Progress, Features, Tutorial | R2P-2 | A |
| `Game.Guest` | `WeeklyGuestSystem` | data, db, DailyClock, Battle, Characters, Formation, Expedition, IdleChest, MetaRewards, Progress, Features, Popups, Tutorial | R2P-2 | A |
| `Game.Techniques` | `TechniqueSystem` | data, db, Currency, Progress, Features, Synergy, Battle, Power | R2P-3 | A |
| `Game.Rounds` | `DailyRoundsSystem` | data, db, DailyClock, IdleChest, Expedition, Dungeon, Errands, Guest, Tower, Missions, Attendance, Mail, MetaRewards, Features | R2P-2 | A |
| `Game.Tower` | `TowerSystem` | data, db, Battle, Power, Formation, MetaRewards, Progress, Features, Tutorial, Keepsakes, Titles | R2P-2 | B1 |
| `Game.Titles` | `TitleSystem` | data, db, MetaRewards, Progress, Features, Tutorial | R2P-2 | B1 |
| `Game.Profile` | `ProfileSystem` (not a module) | data, db, DailyClock, Characters, Currency | R2P-2 | B1 |
| `Game.Lantern` | `LanternSystem` | data, db, DailyClock, Battle, MetaRewards, Features, Characters, Formation | R2P-2 | B1 |
| `Game.RouteMarks` | `RouteMarkSystem` | data, db, Expedition, Keepsakes | R2P-2 | B1 |
| `Game.Market` | `MarketSystem` | data, db, DailyClock, Currency, MetaRewards, Features | R2P-2 | B1 |
| `Game.Merge` | `MaterialMergeSystem` | data, db, DailyClock, Inventory, Evolution, Currency, Progress, Features, IdleChest | R2P-2 | B1 |
| `Game.Walk` | `NightWalkSystem` | data, db, DailyClock, Battle, Characters, Formation, Power, MetaRewards, Progress, Features, Tutorial, Guest | R2P-2 | B2 |
| `Game.Workbench` | `WorkbenchSystem` | data, db, DailyClock, Currency, Inventory, MetaRewards, Progress, Features, IdleChest, Expedition | R2P-2 | B2 |
| `Game.Keepsakes` | `KeepsakeSystem` | data, db, DailyClock, Expedition, Affinity, Inventory, Currency, MetaRewards, Progress, Features | R2P-2 | B2 |
| `Game.Places` | `PlaceRules` (pure, not a module) | db | R2P-1 | B2 |

Not new systems: cap breaks and embroidery live in `CharacterSystem` (R2P-3); 맞춤 편성 is `FormationSystem.BuildBiased` (R2P-3); festival rounds and moon nights are `EventCalendarSystem` kinds (R2P-2); the lantern's firefly hooks extend SPEC4 `FireflyRules` (R2P-2).

Modules that do not exist yet in a wave are created as inert stubs by R2P-0 (they report locked and do nothing). `Tests/EditMode/SystemsTestBase.CreateMetaSystems` builds the same set (R2P-0).

### 2.5 Hooks added to existing systems (R2P-0 adds members only; the named package implements them)

| File | Member | Default | Implemented / consumed by |
|---|---|---|---|
| `Systems/Battle/BattleSystem.cs` | side-battle API (§3.1): `bool CanBeginSideBattle(out string reason)`, `bool BeginSideBattle(SideBattleRequest r)`, `bool IsSideBattle`, `SideBattleRequest ActiveSide`, `float SideTimeLeft`, `void AbandonSideBattle()`, `bool CanFinishNow`, `void FinishSideBattleNow()`, `bool IsFastForwarding`, `void ResumeMainAfterSide()`, events `SideBattleStarted`, `SideBattleEnded`, `BattleRestarted` | inert | R2P-1; consumers R2P-2 modes, R2P-4 views |
| same | `Func<float> ExtraAttackMultiplier` (product with `AttackMultiplier` in every setup), `Func<float> ShopHpMultiplier` (× `ShopMaxHp`), `Func<int, StageModifierType?> ModifierOverride` (moon), `Action<BattleSetup, BattleMode> SetupDecorators` (multicast; keepsakes, places, rumour) | null | R2P-1; set by Techniques (rank), Events (moon), Keepsakes, Places |
| same | `void QueueBonusEnemies(string enemyId, int count)` (added to the next wave; moth) | – | R2P-1; Lantern/FireflyRules |
| `Systems/Battle/BattleTypes.cs` | `SideBattleRequest`, `SideBattleResult`, `SideScript`, `SideSpawn`, `SidePhase`, `FormationSnapshot`, `FormationBias`; `BattleSetup.Script`, `BattleSetup.Rules`, `BattleSetup.ExtraModifier`, `BattleSetup.ShopStartHpFraction = 1`, `BattleSetup.Crests`; `BattleModifiers.Slot` (`StatBlock[5]`, per guardian pad); the `BattleEventType` appends | – | R2P-1 |
| `Systems/Battle/BattleSimulator*.cs` | scripted spawns and objectives (§3.2), Fleeing/Spawner (A) and Reforming (B) gimmicks, phases, `ExtraModifier`, rule effects (B), slot modifiers (B), crest specials (B), `DoubleHitChancePercent` (B), the R2-05b triggers (B) | – | R2P-1 |
| `Data/BattleConfig.cs` | `stageGrowthTaperStage2 = 151`, `stageHpGrowthLate2 = 1.02f`, `stageDamageGrowthLate2 = 1.015f` + `TaperedScale` (§4.1) | on | R2P-C |
| `Systems/Battle/FormationSystem.cs` | `FormationSnapshot BuildBiased(FormationBias bias, Func<string,long> formPower)` (the caller passes `Game.Power.GetFormPower`, so FormationSystem keeps no PowerSystem dependency), `FormationSnapshot Snapshot()` (current main), `static bool Satisfies(FormationSnapshot s, IReadOnlyList<SideRuleDef> rules)` | – | R2P-3 |
| `Systems/Characters/CharacterSystem.cs` | `int GetMaxLevel(string lineageId)`, `const int AbsoluteMaxLevel = 200`, `int GetCapBreaks(id)`, `bool CanCapBreak(id, out CapBreakCost cost)`, `bool TryCapBreak(id)`, `bool CanEmbroider(id)`, `bool TryEmbroider(id)`, `event Action<string,int> CapBroken`, embroidery in the attack term (§5.3) | – | R2P-3 |
| `Systems/Meta/PowerSystem.cs` | include `Battle.ExtraAttackMultiplier` and `ShopHpMultiplier`; the shop-share cap and the third rec segment (§4.3); `long PreviewTechnique(string id, int levels)` | – | R2P-C (cap, rec), R2P-3 (preview) |
| `Systems/Meta/IdleChestSystem.cs` | banked free quick idle (§5.5); `Func<IdleChestLine, long, long, double> LineRateBonus` (applies to lines whose reward is **not** the 별조각 currency; the gem line is skipped by the system, not by callers); `Func<IdleChestLine, double> LineCapBonusHours` (same rule) | null | R2P-2; set by Workbench (B), Events/moon (B) |
| `Systems/Meta/DailyClock.cs` | `event Action<string,string> DayChanging`, `event Action<string,string> WeekChanging` (old, new) raised immediately before `DayChanged`/`WeekChanged` in the same `Refresh` | – | R2P-0 (it is a one-line raise) |
| `Systems/Meta/ProgressTracker.cs` | `long GetPeriodValue(CounterKey key, CounterScope scope, string periodKey)` (stored value when the stored key equals `periodKey`, else 0); answers `CapBreaks/TowerFloor/GuestTier/ErrandLevel` through SPEC4's `ExtraConditionValue`; counts `PlayMinutes` (§2.8) | – | R2P-0 |
| `Systems/Meta/MissionSystem.cs` | `List<MetaReward> AutoClaimPeriod(MissionPeriod p, string periodKey)` (claims every completed mission and reached chest of that period; increments `MissionClaim`, `JournalPoints`, `JournalComplete` as a manual claim would; refuses when tampered) | – | R2P-2 (called by DailyRounds at DayChanging/WeekChanging) |
| `Systems/Meta/DailyGiftSystem.cs` (SPEC4) | `bool CanClaimKept`, `IReadOnlyList<MetaReward> Kept`, `List<MetaReward> TryClaimKept()`, `List<MetaReward> ClaimAll()` | – | R2P-2 |
| `Systems/Meta/FeatureGateSystem.cs` | honour `FeatureUnlockDef.alsoRequire`; card spacing (§2.8): `bool CardReady(FeatureId f)` used by the presenter; drop a queued card when `IsNew(f) == false` | – | R2P-0 |
| `Systems/Meta/PopupQueue.cs` | order table instead of raw enum values; `GuestReveal` is a startup kind that is never dropped by the startup cap (it waits for the next between-stages slot, like SPEC4's EventStart); `MetaPopupRequest.GuestReveal(string weekKey)` | – | R2P-0 |
| `Systems/Meta/MetaRewardService.cs` | `Func<MetaReward, bool> ExtraGrant` (true = handled; multicast-safe chain: first non-false wins), `Func<MetaReward, string> ExtraDescribe`, `Func<MetaReward, string> ExtraName`; **default handling when no system claims a reward** (so nothing granted before its system exists is ever lost): `Keepsake` → `keepsakes.pending.Add(id)` + flag `r2.keepsake.first`; `Title` → `titles.owned.Add(id)`; `WalkLantern` → `walk.lanterns` up to the cap, the rest into `overflowExpiry` | null | R2P-0 adds (incl. the defaults), R2P-2 sets (titles, keepsakes, lanterns) |
| `Systems/Meta/StrengthenAdvisor.cs` (SPEC4) | rows `Technique` (cheapest gain per 단추 among open rows) and `CapBreak` (a placed lineage at its cap with the break affordable) | – | R2P-3 |
| `Systems/Meta/EventCalendarSystem.cs` (SPEC4) | `FestivalRounds` and `MoonNight` kinds, `MonthRotation`/`MoonPhase` schedules, rounds API (§6.6) | – | R2P-2 (B1) |
| `Systems/Meta/FireflyRules.cs` (SPEC4) | `FireflyKind Variant(StageModifierType m, bool night)`, `bool IsMoth`, catch → `Progress.Increment(FireflyTap)` and `Lantern.AddFireflyBonus()` | – | R2P-2 (B1) |
| `Systems/Meta/AffinitySystem.cs` (SPEC4) | Lv6 claim also grants `MetaReward.Keepsake(lineDef.keepsakeId)` when set | – | R2P-2 (B2) |
| `Systems/Battle/ExpeditionSystem.cs` | `event Action<string> ZoneClaimed` (zone id, once per claimed expedition; **wave A**, guest stitches); `Func<string, double> ZoneLootBonus` (× gold and drop rolls, after SPEC4's `DropMultiplier`) and `Func<string, bool> AutoResendZone` (B1) | null | R2P-2 |
| `Systems/Meta/NotificationPlanner.cs` (SPEC4) | context fields `outingTicketsFull` (A), `researchDoneLocal` (B), `lanternsFullLocal` (B); planner ids 1005/1006 under existing kinds (S5-13) | – | R2P-2 |
| `Systems/Meta/MetaSession.cs` | the two optional module lists (§2.4); `PlayMinutes` ticking (§2.8); forwards `Battle.SideBattleEnded` to `ISideBattleModule`s of that mode | – | R2P-0 |
| `Systems/Meta/MetaRewardService.cs` (CR4-05) | `public Func<double> IncomePerSecond;` used by `GoldForMinutes` (null = `Growth.GoldPerSecond`, today's behaviour) | null | R2P-0 adds; R2P-3 resolves through it; MetaSession wires it (§4.6) |
| `Systems/Battle/BattleSystem.cs` (CR4-05) | `public double ClearGoldAt(int stage)` (the existing private `StageGold` × the gold bonus; no allocation) | – | R2P-1; read by the income hook |
| `Systems/Meta/IdleChestSystem.cs` (CR4-05) | the offline 단추 line honours `IdleChestConfig.offlineGoldMinutesPerHour` (> 0: away hours × that many 단추 N분치 through `MetaRewards.GoldForMinutes`) | 0 = today | R2P-2 |
| `Systems/Battle/FormationSystem.cs` (CR4-06) | `public Func<string,long> AutoFormationPower;` — when set, `AutoFormation()` keeps its signature, role order and pad order but ranks candidates by it (ties: HP, then form id); null = today's `attack/interval×10 + hp` | null | R2P-0 adds; R2P-3 implements; MetaSession sets it to `Power.GetFormPower` (§5.6) |
| `Core/AppSignals.cs` (CR4-20) | `public static event Action<string, string> PlaceChanged; public static void RaisePlace(string placeId, string ambKey)` | – | R2P-0 adds; R2P-1 raises it when a main stage of another place starts; AUD's ambience director listens |
`MetaSession.Connect` (R2P-0) sets every new `FeatureResolver` to `f => _features.IsUnlocked(f)`, sets `MetaRewards.IncomePerSecond = () => Growth.GoldPerSecond + Battle.ClearGoldAt(highestStage) / income.referenceClearSeconds` when `income.enabled` (CR4-05), sets `Formation.AutoFormationPower = Power.GetFormPower` (CR4-06), wires `Techniques` into `Battle.ExtraAttackMultiplier`/`ShopHpMultiplier` and `Synergy.ExtraTeamModifiers` (multicast `+=`, next to SPEC4's Codex), sets `Errands.Rumour`, `Tower.Rumour` and `Walk.Rumour` (`Func<TraitType[]>`) to `() => Guest.RumourTraits` (so no mode system depends on `WeeklyGuestSystem` at construction), and wires `MetaRewards.ExtraGrant` to the wave-B systems when they exist.

### 2.6 Frame, navigation and signals (`UI/UINav.cs`, `UI/Main/MainTabController.cs`, `Systems/Meta/MetaTypes.cs`)
**Hub sheets (S5-5).** The two hubs are sheet panels without a tab button. Shell exception requested for R2P-0, these members only:
- `MainTabController`: `[SerializeField] GameObject[] _hubPanels` (index *h* is selected as `TabCount + h`: 6 = 밤 나들이, 7 = 수선점 안채); internal `_allPanels` = tab panels + hub panels used by `ShowPanel`/`HidePanels`; `TrySelect` accepts hub indices (no `SelectGuard`, no `Selected`; raises new `event Action<int> HubSelected`); `Apply` keeps every side plate off, hides the indicator (as in MAIN) and shows the 사냥 SHEET look; `public Func<bool> MainClickInterceptor` (called first in `OnMainClicked`, before its `IsMain` early return; true = handled); `public void SetMainLabelOverride(string text)` (null restores '사냥').
- `UIFrame`: doc comment only — `SheetTab` ≥ 6 means a hub panel.
- `UINav`: `public static event Action<int> HubRequested; public static void OpenHub(int hub)`; `public static event Action<OutingsPage, string> OutingsRequested; public static void OpenOutings(OutingsPage page = OutingsPage.Hub, string id = null)`; `public static event Action<HomeRoom> HomeRequested; public static void OpenHome(HomeRoom room = HomeRoom.Hub)` (B).
- Append-only additions to two other frozen files, exactly as SPEC4's P4-0 did: `RedDotPaths` in `RedDotSystem.cs` (the §2.7 constants and `ParentOf` rows; `IsClaimOn` untouched) and `CoachTargets` in `MetaTypes.cs` (the four constants below).
- No change to `SheetController`, `SheetDragHandle`, `UIManager` or `IdleSceneBuilder`. Hub panels are built by the new `Editor/Builders/OutingsSceneBuilder.cs` (and `HomeSceneBuilder.cs` in B), called from `MetaSceneBuilder.Build`; they append `ContentArea/OutingsPanel` and `ContentArea/HomePanel` and bind `_hubPanels` through `SerializedObject`.
- Behaviour that follows with no other change: 사냥 closes a hub, back closes it (SheetBack), the 60 s auto-return closes it, the return chip works, the battle pans up exactly as for a tab sheet, `UIFrame.IsUnderSheet` and the coach redirect are unchanged.
- **Fallback (exception refused):** each hub is `BuildPopup(980, 1500)` with the same page views (cards stretch to 900); the side-battle abandon control moves from the centre button to the side bar (§3.5). Nothing else changes.

**Rails (UX-SPEC-IA §3.3).** Two new `QuickIconView` cells, built by `MetaSceneBuilder`:
- `QuickRail_R/OutingsButton` ('밤 나들이'; bottom; listed after `JournalButton`, so it takes R2 while the journal keeps R3); feature `Outings`; dot `top.outings`; icon `Icons/rail_outings` (fallback `Icons/lamp`); tap → `UINav.OpenOutings()`; **long-press 0.5 s → `Game.Rounds.Run()`** when DailyRounds is unlocked (R2-07).
- `QuickRail_L/HomeButton` ('안채'; bottom; after `AttendanceButton`, so it takes L2); feature `Home` (B); dot `top.home`; icon `Icons/rail_home` (fallback `Icons/mission_visit`); tap → `UINav.OpenHome()`.
- Both rails are then full. No later R2 item adds a rail icon (UX-SPEC-IA v1.2 rule). `QuickIconView.Activate` gains the two cases.

**≡ 메뉴 (UX-SPEC-IA §3.6).** Two tiles: `OutingsTile` '밤 나들이' (gate Outings, dot `top.outings`) and `HomeTile` '수선점 안채' (gate Home, dot `top.home`). They duplicate the rails on purpose (rails hide in SHEET); the ≡ dot does not aggregate them. (CR4-09) UX-SPEC-IA §8.8 already puts SPEC4's EventHub, SeasonPass, Codex, Achievements and Coupon tiles in this menu (built by P4-4 in phase 4), so the grid is not 8 tiles:
- the R2 tiles go after the last activity tile (수선 일지 · 출석부 · 이벤트 and SPEC4's tiles) and before the system tiles 가방 · 공지 · 설정;
- rows = ⌈visible tiles / 3⌉, and the window is `BuildPopup(900, 300 + 220·rows)` (4 rows = 1180, which fits the 1920 frame);
- R2P-7 updates `AutoShot.MenuTileOrder`, and the frozen tile paths keep their names (`Popups/MenuPopup/Window/Body/Grid/*Tile`).

**Centre button during a side battle.** While `Battle.IsSideBattle`: label '사냥으로' (`SetMainLabelOverride`), and `MainClickInterceptor` → if a sheet is open, close it (normal); else `UIManager.ShowConfirm(title '나들이를 멈추고 돌아갈까요?', body '{모드별 한 줄}', onOk → Battle.AbandonSideBattle(), okLabel '돌아가기', cancel '계속하기')`. Mode lines: errand '지금 돌아가면 입장권은 쓰지 않아요.' · guest '지금 돌아가면 도전 횟수는 줄지 않아요.' · tower '이 층은 다음에 다시 올라요.' · walk '등불 길은 그 자리에 멈춰 기다려요.'.

**Side battles run in MAIN (CR4-10).** Every live start (errand [다녀오기], guest [그림자 떼어 내기], tower [도전], walk [걷기]/[이어 걷기] and every [다시 하기]) calls `UINav.GoToMain()` first and starts the fight once the frame reports MAIN. A live fight under an open sheet would show only the top band, and the deck strip, lamp and `SideBattleBar` would be covered (UX-SPEC-IA §4.5: deck interaction needs MAIN). Headless runs (auto-climb, rounds) stay in their sheet. The result card's [확인] reopens the hub page as specified.

**Android back during a side battle (CR4-11).** UX-SPEC-IA §4.7's order would end in the *exit* confirm, and `UIManager` is frozen. So `SideBattleBarView` calls `UIManager.PushBackHandler(h)` on `SideBattleStarted` and `RemoveBackHandler(h)` on resume. `h` returns false while a popup is open or `UIFrame.SheetOpen` (so step 2 closes the popup and step 4 closes the sheet), and otherwise opens the abandon confirm above and returns true.

**Navigation (`MetaNavigator`, R2P-4a).** `OnFeature` gains: Outings/Errands/WeeklyGuest/DailyRounds/Tower/NightWalk → `OpenOutings(page)`; Techniques → `GoToTab(Growth)` + select the [수선 비법] segment; LevelBreak → `OpenCharacter(highest-level lineage at its cap)`; Workbench/Keepsakes/Home → `OpenHome(room)`. `FeatureSlot` (M78 flight) gains Outings… → `…/QuickRail_R/OutingsButton`, Home… → `…/QuickRail_L/HomeButton`, Techniques → `Tab_GrowthPanel`, LevelBreak → `Tab_CharacterPanel`. During a side battle every `GoToTab`/hub request still works (the battle keeps running above the sheet); the Formation tab shows its normal content, and edits apply from the next attempt (§3.1).

**Coach targets (`CoachTargets`, append):** `OutingsButton = Battle + "/MetaHud/QuickRail_R/OutingsButton"`, `HomeButton = Battle + "/MetaHud/QuickRail_L/HomeButton"`, `OutingsPanel = "SafeArea/ContentArea/OutingsPanel"`, `HomePanel = "SafeArea/ContentArea/HomePanel"`. Frozen after R2-0 (do not rename or reparent): those four, `OutingsPanel/{PanelHeader, CardList, Pages}`, `Battle/SideHud`, `Battle/MetaHud/SideBattleBar`. `MetaDataBuilder.ValidateTargetsInScene` must report 0 missing.

**AppSignals (SPEC4 §2.6, append):** `public static event Action<BattleMode> SideBattleChanged; public static void RaiseSideBattle(BattleMode m)` (Stage = back to the hunt) so audio, sleep and the guide bar react without compiling against R2 systems. (CR4-20) Also `public static event Action<string, string> PlaceChanged; public static void RaisePlace(string placeId, string ambKey)`: R2P-1 raises it when a main stage of another place starts, and the AUDIO workflow's ambience director swaps its base bed with no R2 dependency.

### 2.7 Red-dot paths (`RedDotPaths`, append)

| Constant | Path | Leaf | Rule (F-04: only claimable, wasting or finished) |
|---|---|---|---|
| `TopOutings` | `top.outings` | parent | any child |
| `OutingErrand(id)` | `top.outings.errand.{id}` | bool | tickets at cap (more would be wasted at 05:00) |
| `OutingGuest` | `top.outings.guest` | bool | today's unused attempts would be lost at 05:00 because the bank is full (`bankedAttempts == bankMax` and attempts left today > 0) |
| `OutingTower` | – | – | none (M09 noise rule: floors are never a dot) |
| `OutingWalk` | `top.outings.walk` | bool | (B) lanterns at cap and the overflow bank full |
| `TopHome` | `top.home` | parent | (B) any child |
| `HomeWorkbench` | `top.home.workbench` | bool | (B) a research job has finished (never "a node is affordable") |
| `HomeKeepsakes` | `top.home.keepsakes` | bool | (B) a new keepsake not yet seen, or a free shelf slot while an owned keepsake is unplaced |
| `HudLantern` | `hud.lantern` | bool | (B) a full lantern is stored |
| `GrowthTechniques` | – | – | none (rows are "affordable", never a dot) |
| `LineageBreak(id)` | `tab.characters.lineage.{id}.break` | bool | that lineage is at its cap and the break is affordable (a finished-state, like SPEC4's resonance dot) |
`ParentOf` gains: Outings, Errands, WeeklyGuest, DailyRounds, Tower, NightWalk → `top.outings`; Workbench, Keepsakes, Home → `top.home`; Techniques → `tab.growth`; LevelBreak → `tab.characters`. Rails pick up `top.outings`/`top.home` through `QuickRailView.PlacedDotPaths`, so the 사냥 aggregate (UX-SPEC-IA §5.9) needs no change.

### 2.8 Feature unlocks, pacing and cards (content R2P-5 in `MetaContent.Features.cs`; logic R2P-0)
**The defect (PROGRESSION-SIM §6.3).** The engaged player clears chapters 1–5 in ≈14 minutes and chapter 10 at ≈34 minutes online; no economy knob changes this. SPEC4's cards already cluster in those minutes (1-6 ×2, 1-7, 1-8, 1-10 ×2, 2-1 ×2, 2-3, 2-5, 3-1), and R2's stage gates (2-2 … 5-1) would add six more.

**Fix (three parts):**
1. **`CounterKey.PlayMinutes`.** `MetaSession.Tick` adds unscaled foreground time (sleep included, paused excluded) and increments the counter once per whole minute (one `Progress.Increment` per minute, so `Progress.Changed` stays cheap). Gates use `MetaCondition.Count(CounterKey.PlayMinutes, n)`. This is not a day gate (§17): it measures play, so a binge player is never held back by the calendar.
2. **`FeatureUnlockDef.alsoRequire`** (AND list). Today `conditions` are OR (`FeatureGateSystem.AnyMet`). A feature opens when one of `conditions` holds **and** every `alsoRequire` holds. `ForceUnlock` and the silent legacy sync ignore `alsoRequire`.
3. **Card spacing.** A FeatureUnlock card is presented only when ≥ 180 s of foreground play (`FeatureGateSystem.CardSpacingSeconds = 180`) have passed since the last card closed; two cards that became ready in the same evaluation still show back to back (SPEC4's "two per moment"). A queued card is dropped if the player already entered the feature (`IsNew == false`). Applies to SPEC4's cards too. The presenter asks `Features.CardReady(f)` before it dequeues a FeatureUnlock request; the timer counts foreground seconds (paused time excluded), so a card never fires on the first frame after a resume.

| Feature | Conditions (any) | alsoRequire (all) | Card | Card text (cardText) | Engaged player (sim) |
|---|---|---|---|---|---|
| Outings | StageCleared 2-2 | PlayMinutes ≥ 20 | **on** | '밤 나들이 지도가 생겼어요. 골목 심부름으로 단추와 재료를 모아요' + line 2 '2-6을 지나면 이번 주의 손님이 찾아와요' | end of session 1 |
| Errands | StageCleared 2-2 | PlayMinutes ≥ 20 | off (rides on Outings) | – | same |
| LevelBreak | LineageLevel ≥ 100 (empty targetId = highest) | – | **on** | '한 땀 돌파로 레벨 한계를 넘을 수 있어요' | min 21–29 |
| Techniques | StageCleared 3-4 · LineageLevel ≥ 60 | PlayMinutes ≥ 30 | **on** | '모인 단추로 수선 솜씨를 갈고닦아요. 끝없이 강해질 수 있어요' | session 2 |
| WeeklyGuest | StageCleared 2-6 | PlayMinutes ≥ 45; FeatureUnlocked Outings | **off** — NEW pill on the guest card + explainer on first entry (S5-4) | – | day 1 |
| DailyRounds | StageCleared 3-2 | Counter ErrandClear ≥ 10; FeatureUnlocked WeeklyGuest | **on** | '이제 매일 할 일을 한 번에 끝낼 수 있어요' | day 2–3 |
| Tower (B1) | StageCleared 3-8 | PlayMinutes ≥ 90; FeatureUnlocked Outings | **on** | '분실물 보관소의 선반을 한 층씩 올라가요. 입장권이 필요 없어요' | day 1–2 |
| NightWalk (B2) | StageCleared 4-1 | PlayMinutes ≥ 150; FeatureUnlocked Outings | **on** | '등불을 하나씩 켜며 걷는 밤길이 열렸어요. 매번 다른 길이에요' | day 3–4 |
| Keepsakes (B2) | Flag `r2.keepsake.ready` · Flag `r2.keepsake.first` | – (see note) | **on** | '모은 기념품을 선반에 올려 두면 친구들이 힘을 내요' | day 4–5 |
| Workbench (B2) | StageCleared 5-1 | PlayMinutes ≥ 270 | **on** | '밤새 바느질해 두면 수선점이 조금씩 단단해져요' | ≈ch15, day 6–7 |
| Home (B2) | FeatureUnlocked Keepsakes · FeatureUnlocked Workbench | – | off (the Keepsakes/Workbench card announces it) | – | – |
Note (Keepsakes): `alsoRequire` applies to the whole def, but the "first keepsake earned" path must not wait for the play floor. So the def has two flag conditions and no `alsoRequire`: `KeepsakeSystem` sets `r2.keepsake.ready` when StageCleared 4-5 **and** PlayMinutes ≥ 200 hold, and `r2.keepsake.first` when any keepsake is granted (pending included).

SPEC4 card change (S5-4, content R2P-5): AutoChallenge cardText → '이제 보스 도전과 원정 다시 보내기를 친구들이 알아서 해요' (gift 빠른 수선권 1 moves here); ExpeditionBatch `showCard = false` (its NEW pill on ResendAll stays). `CardsReady` (SPEC4 §2.8) gains the R2 features as each screen merges.
`lockedHint`/`lockedToast` use `MetaText.UnlockShort/UnlockSentence` (M39) for the stage part; when only the play floor is missing the toast is '조금만 더 수선점을 돌보면 열려요'.

### 2.9 PopupQueue (`Systems/Meta/PopupQueue.cs`)
- An explicit order table replaces "order = enum value": Return, Comeback, **GuestReveal**, Attendance, Notice, EventStart, FeatureUnlock, FtueGraduation, ChapterChest, StoryPage, NotificationAsk.
- `IsStartupKind` adds `GuestReveal`. Like SPEC4's EventStart it is **never dropped** by `MaxStartupModals`; it waits for the next between-stages slot.
- `RequiresBetweenStages` is unchanged. **"Between stages" is false during a side battle and while its result card is open** (`BattleSystem.IsBetweenStages` returns false when `IsSideBattle`), so no queued modal ever covers a side battle.
- New factory `MetaPopupRequest.GuestReveal(string weekKey)`. The presenter (R2P-4a) shows `GuestRevealPopup` (§5.1). It is suppressed during FTUE like every kind.
- Not popup kinds (they open in place): the side-battle result card, the explainer strip, the rounds summary, the walk choice nodes, the charm pick.

### 2.10 GameSettings additions (`Core/GameSettings.cs`)
None. Rounds toggles, 맞춤 편성 switches, auto-retry and auto-weave are gameplay choices stored in the save (§2.1). (CR4-19) SPEC4's "no other keys" note no longer holds as written: `SheetAutoReturn` (WP-IA) and `VoiceBarks` (P4-0, PHASE4-ROADMAP delta 13) exist. SPEC5 adds no key to them.

### 2.11 Conflict resolutions decided here

| # | Conflict | Decision |
|---|---|---|
| 1 | R2-00 "parks the main stage and resumes it" vs SPEC4 trackers that listen to `StageChanged`/`StageEnded` (CR-01 ring, CR-03 stats, hidden records, chapter chest, story, missions) | Side battles raise neither event. They raise `SideBattleStarted/Ended`; `BattleRestarted` fires for every battle start (main or side) and is what `BattleView` rebinds on. The main stage restarts with a normal `StageChanged` after the side result closes |
| 2 | ProgressTracker counts `EnemyKill`/`EchoSummon`/`LampLit` from every `Battle.Event` | Live side battles count (the player is playing; missions like '잔향이 30번 부르기' may be done in errands). Headless runs never emit through `Battle.Event`, so they never count. `StageClear`/`BossKill`/`StageFail` never count for side battles. `ProgressTracker` doc comment changes from '메인 스테이지' to '라이브 전투' for the three keys |
| 3 | Upgrades and formation edits during a live side battle (`_refreshPending`, `RefreshUnits`) | A side battle's units are fixed at its start; `RequestUnitRefresh` and formation changes are deferred to the main restart. No mid-fight power changes, no exploit |
| 4 | SPEC4 NEW-03 fireflies, CR-01 ring, cheer button and the M13 boss button during side battles | Fireflies need `Mode == Stage` (SPEC4 rule), so none spawn. CR-01 never runs (no stage fail). The cheer buff applies (SPEC4 `TempAttackPercent` is in the shared setup), but the cheer button is in the 강해지기 sheet only. The boss challenge button hides while `IsSideBattle` |
| 5 | Sleep mode (2 min) and sheet auto-return (60 s) during a side battle | Sleep never starts while `IsSideBattle` (`SleepModeView` reads `AppSignals.SideBattleChanged`). Auto-return closes a hub sheet as for any sheet; a side battle always starts in MAIN, because every live start calls `GoToMain()` first (§2.6, CR4-10) |
| 6 | R2-08 skip-expedition rule vs "원정 중에도 함께 싸워요" | Expedition members fight (S5-7) |
| 7 | SPEC4 `ExpeditionSystem.DropMultiplier` (single `Func`, set by Events) vs route marks and the workbench | New per-zone `ZoneLootBonus` multiplies after `DropMultiplier`; Events keeps its hook |
| 8 | SPEC4 `CandyMultiplier` (Events) vs moon nights | Moon nights are an `EventCalendarSystem` kind, so the one function returns ×2 inside days 1–14 (starlit walk) and the moon value outside; never ×4 (SPEC4 §13.5(i)) |
| 9 | Q-05 "all 9 enemies → 수선점 내구도 +5%" vs new enemies (그림자 까치, 둥지, 먼지 왕, 흔들 종의 그림자) | The bonus threshold stays a count: '적 9종 이상'. New enemies add codex rows (10 별조각 each, one-time) and never take a bonus away |
| 10 | Q-05 codex tab row 6×144 vs 2 new tabs | 8 tabs × 110 px, gap 8 (936 ≤ 940 inner width); labels Label 24 ('기념품', '손님'); counts unchanged |
| 11 | R2-36a's six costumes vs SPEC4's `ach_costume` 3/6/10 | Thresholds unchanged (more costumes make them easier); the codex costume total becomes 17 (SPEC4 11 + 6) |
| 12 | UX M09 "no new HUD dots" vs the lantern | The lantern chip's dot is the only R2 HUD dot (B1), it is claimable-only, and the chip is MAIN-only |

### 2.12 Gate and red-dot summary (every item)

| Item | Opens at | Red dot / badge |
|---|---|---|
| R2-00 side battles | with their mode; never in FTUE | – |
| R2-01 errands | Outings + Errands (2-2 & 20 min); errand 2 at its `requiredStage` 2-4 (data) | `top.outings.errand.{id}` (tickets at cap); NEW pill on each tile until entered |
| R2-03 guest | WeeklyGuest (2-6 & 45 min & Outings), NEW pill | `top.outings.guest` (bank full and attempts left) |
| R2-04 techniques | Techniques ((3-4 or Lv60) & 30 min) | none (rows); NEW pill on the [수선 비법] segment until opened |
| R2-05a breaks | LevelBreak (any lineage Lv100) | `tab.characters.lineage.{id}.break` |
| R2-07 rounds | DailyRounds (3-2 & ErrandClear 10 & WeeklyGuest) | none (NEW pill on the CTA until first run) |
| R2-07 banks, kept gift, 놓친 선물 | with their source features | none (toast / Return line) |
| R2-08 맞춤 편성 | with each mode page | – |
| R2-36a costumes, fitting room | Costumes (2-5) | SPEC4's existing costume dots |
| R2-02 tower (B1) | Tower (3-8 & 90 min & Outings) | none |
| R2-09/R2-20 titles, profile (B1) | titles from their sources; profile with Tower or PlayMinutes ≥ 60 (naming bubble) | NEW pill on LevelText when a new title is owned |
| R2-15 lantern (B1) | with Outings, after `ftue.done` | `hud.lantern` |
| R2-17 marks (B1) | ExpeditionBatch (2-1) | – |
| R2-18 market (B1) | Shop (1-8), Sat–Sun DayKeys | NEW pill on the shop tab section on the first weekend |
| R2-19 merge (B1) | Evolution (1-10) & PlayMinutes ≥ 60 | NEW pill on the bag's [엮기] tab |
| R2-13/R2-14 festival, moon (B1) | Events | SPEC4 `top.event.claim` (rounds milestones claimable) |
| R2-10 등불 길 (B2) | NightWalk (4-1 & 150 min & Outings) | `top.outings.walk` |
| R2-11/R2-12 home, workbench, keepsakes (B2) | Workbench (5-1 & 270 min), Keepsakes (4-5 & 200 min or first keepsake), Home (either) | `top.home.workbench`, `top.home.keepsakes` |
| R2-36b forms, R2-37 place, R2-05b riders (B2) | content (forms in the 진화 기억도, place from chapter 6, riders at 공명 3/6/9) | SPEC4's evolution dots |

---

## 3. R2-00 battle infrastructure (R2P-1 implements; R2P-0 declares)

### 3.1 Side-battle host (`Systems/Battle/BattleSystem.cs`)
A side battle is a live fight of another mode in the same battle view. The main stage is parked, never progressed, and restarted afterwards.

**Instances.** `BattleSystem` owns a second `BattleSimulator _sideSim` (created lazily, `Event` forwarded through the same `BattleSystem.Event`). `Sim` returns `_sideSim` while a side battle is active. The parked main `_sim` is not ticked. `BattleView` already rebinds when `Game.Battle.Sim` changes (`BattleView.OnBattleEvent`: `if (Game.Battle.Sim != _sim) Rebind(false)`); R2P-1 also subscribes it to `BattleRestarted` → `Rebind(true)` so the switch is immediate.

**Lifecycle.**
1. `CanBeginSideBattle(out reason)` refuses: tutorial active ('튜토리얼이 끝나면 다녀올 수 있어요'), a side battle already running, no guardian and no deck ('편성을 먼저 해 주세요'), the app paused.
2. `BeginSideBattle(request)`: records the main stage number (no save change), builds the side setup through the **shared setup path** (§3.1.1), `_sideSim.Start(setup, request.Seed)`, `AutoMode` = the saved AUTO, raises `BattleRestarted` and `SideBattleStarted(request)`, and `AppSignals.RaiseSideBattle(mode)`. (CR4-10) The UI callers have already returned the frame to MAIN (§2.6); `BeginSideBattle` itself does not touch the frame.
3. `Tick(dt)`: ticks `_sideSim` with `dt × Speed × PresentationScale` (SPEC4 FX-01); ends when the sim outcome is not Running, or `ElapsedTime ≥ Script.TimeLimit` (time up), or `AbandonSideBattle()`.
4. End: builds `SideBattleResult`, raises `SideBattleEnded(result)` (MetaSession forwards it to the mode's `ISideBattleModule`, which spends tickets and grants rewards), and waits. The result card calls `ResumeMainAfterSide()`; if nothing calls it within `SideBattleConfig.resultAutoResume` (10 s real time — tests, headless tools), the system resumes itself.
5. `ResumeMainAfterSide()`: `Sim` returns to `_sim`, the main stage restarts at its start (`StartBattle()` → normal `StageChanged`), `AppSignals.RaiseSideBattle(Stage)`.
- **Abandon** (`AbandonSideBattle`): ends at once with `Abandoned = true`; modes spend nothing and grant nothing.
- **Pause/kill.** Nothing about a side battle is saved. Paused: it simply does not tick. Killed: the next boot starts the main stage; no ticket was spent (tickets are spent only in `OnSideBattleEnded`).
- **Units are fixed** for the whole side battle (conflict #3): `RequestUnitRefresh` and `Formation.Changed` set a flag consumed at the main restart.
- `IsBetweenStages` is false while `IsSideBattle` or while the result card is open (§2.9).

#### 3.1.1 One setup path
`BuildSetup` is split into `BuildCommonSetup(FormationSnapshot formation, int stageEquivalent, BattleMode mode)` (synergy modifiers incl. SPEC4 `ExtraTeamModifiers` (codex, techniques), `TempAttackPercent` (cheer), costumes through `CharacterSystem.GetStats`, `ExtraAttackMultiplier`, `ShopHpMultiplier`, `SetupDecorators` (keepsakes, place rules, rumour)) and the mode part. Main stages and side battles both go through it, so power numbers and rating colours mean the same thing everywhere (CR2 critic rule). `BuildSideSetup(request)` then adds:
- `Mode`, `Level = request.StageEquivalent` (enemy scaling uses `BattleConfig.GetStageHpScale/DamageScale(Level)` exactly like a stage; `EchoDungeon` keeps its own path);
- `StageModifier` = `request.Modifier` (default `None`; tower blocks, walk depth weather);
- `Script` (§3.2), `Rules` (§3.4), `ShopStartHpFraction` (walk durability carry-over);
- bias stat bonuses from `request.Bias` (§5.6): `Modifiers.Lineage[l].AttackPercent += traitPercent` for lineages with a favoured trait; `Modifiers.Form[f].AttackPercent += stylePercent` for forms with a favoured style; `Modifiers.Team.AttackPercent += min(cap, ownedForms × perForm)` for 모두 함께.

```csharp
public sealed class SideBattleRequest {
    public BattleMode Mode; public string SourceId; public int Level; public int StageEquivalent; public int Seed;
    public StageModifierType Modifier; public SideScript Script; public FormationSnapshot Formation;   // null = current main formation
    public FormationBias Bias; public List<SideRuleDef> Rules; public float ShopStartHpFraction = 1f; public string HudTitle;
}
public sealed class SideBattleResult {
    public BattleMode Mode; public string SourceId; public int Level; public BattleOutcome Outcome; public bool TimeUp, Abandoned, FastForwarded;
    public float ElapsedSeconds, ShopHpFraction; public double ObjectiveDamage, ObjectiveMaxHp; public int ObjectivesKilled, ObjectiveCount, Phases;
}
public sealed class FormationSnapshot { public string[] Guardians = new string[5]; public string[] Echoes = new string[6]; }
public sealed class FormationBias { public TraitType[] Traits; public float TraitPercent; public CombatStyle[] Styles; public float StylePercent;
                                    public bool OwnedFormsBonus; public IReadOnlyList<SideRuleDef> Rules; }
```

### 3.2 Side scripts, objectives and the gimmicks (`BattleTypes.cs`, `BattleSimulator*.cs`)
```csharp
public sealed class SideScript {
    public float TimeLimit;                 // battle seconds; 0 = none
    public bool NormalSpawns;               // the stage spawner (waves of the stage pool) runs
    public float WaveSizeScale = 1f; public int WaveCount;   // tower: one scripted wave; walk: one mini-wave (0 = unlimited)
    public bool ClearOnObjectives;          // all objectives dead → Cleared
    public bool ClearOnWaves;               // all scripted waves spawned and dead → Cleared
    public bool ClearOnBoss = true;         // boss dead → Cleared (stage rule)
    public bool NoBoss;                     // no KillsToBoss boss
    public List<SideSpawn> Spawns = new();
}
public sealed class SideSpawn {
    public string EnemyId; public float At; public int Lane = -1;            // −1 = pressure lane
    public float Radius = BattleGeometry.SpawnRadius; public float HpMultiple = 1f, DamageMultiple = 1f;
    public bool Objective, Boss, Stationary; public EnemyGimmick AddGimmicks; public int ChainIndex = -1;  // Spawner chain order
    public float OrbitRadius, HopInterval; public bool Approach;            // Fleeing
    public string MinionId; public float MinionInterval; public int MinionMaxAlive;
    public List<SidePhase> Phases;                                           // shell layers / re-forms
}
public sealed class SidePhase { public float HpMultiple = 1f; public EnemyGimmick AddGimmicks; public string MinionId; public int MinionBurst;
                                public string SplitInto; public int SplitCount; public bool KnockBack = true; public float DamageMultiple = 1f; }
```
- **Objectives.** Spawns with `Objective = true` are tracked: `ObjectiveDamage` (sum of `EnemyHit.Value` on them), `ObjectiveMaxHp` (sum of their phase HP so far), `ObjectivesKilled`, `Phases`. Computed in the sim (`BattleSimulator.SideStats`, no allocation) so live and headless agree.
- **Fleeing (A).** Never attacks and never strikes the shop. With `Approach = false` (errand magpie) it orbits at `OrbitRadius` on its lane and every `HopInterval` s hops to another lane (seeded, never the same lane twice in a row), raising `ObjectiveHop`. Guardians can hit it only while it is inside their fan and range (the normal targeting rule — that is the point: reach and lane coverage matter); echoes chase it. With `Approach = true` (place-2 chapter enemy, B2) it walks in like a normal enemy but hops once to an adjacent lane halfway.
- **Spawner (A).** `Stationary` at `Radius` on its lane; summons `MinionId` every `MinionInterval` while active, at most `MinionMaxAlive` alive. A chain (same `ChainIndex` group) has exactly one active nest: the others are dormant (untargetable, drawn closed); when the active nest dies the next one in the chain activates after 1.0 s.
- **Phases.** When an objective with remaining phases reaches 0 HP it does not die: HP resets to the next phase's HP, `AddGimmicks` are applied, optional `MinionBurst` / `SplitInto`, optional knock-back to the spawn radius, and `ObjectivePhase` is raised. The last phase dies normally.
- **Reforming (B1).** A shorthand for unlimited phases: each re-form is `+reformStep` HP and damage over the previous one, until the time limit.
- **Clear and fail.** Fail = shop HP 0 (as a stage). Clear per the `Clear*` flags. Time up = outcome stays Running and `TimeUp = true` (modes score partial results).
- `BattleSimulator.HasDefenders`, `LampDrain`, `Enrage`, `Splitter`, `FabricEater` and modifiers behave exactly as on a stage.

### 3.3 Headless runner and fast-forward
- **Sweeps never simulate.** Errand sweeps (and dungeon `SweepMany`) pay instantly, so `오늘의 순회` never runs a fight for them.
- **`SideBattleRunner`** (new, `Systems/Battle/`, pure): `static SideBattleRunner Start(BattleSetup setup, int seed, SideScript script)`, `bool Step(float budgetMs)` (advances whole fixed steps until the budget is used; returns true when done), `SideBattleResult Result`. It allocates only at `Start`. Budget: `SideBattleConfig.headlessMsPerFrame` (2 ms) for automatic runs (rounds' guest step, tower auto-retry and auto-climb), measured with a `Stopwatch` every 64 steps.
- **'결과 바로 보기'** (`FinishSideBattleNow`, available after `finishNowAfter` = 5 s): fast-forwards **the same `_sideSim` instance** with AUTO on, unrendered, `fastForwardMsPerFrame` (8 ms) per frame. It never re-simulates from the start (taps in the first 5 s would make a rerun differ; the sim is fixed-step `StepSeconds = 1/60`). `IsFastForwarding` is true meanwhile; `BattleView`, `BattleAudioDirector` and the damage numbers skip events while it is set; the side HUD shows '결과를 정리하고 있어요…' with a bar.
- **Determinism test:** a fixed request with AUTO inputs gives the identical `SideBattleResult` live (stepped by `Tick`), headless (runner) and fast-forwarded from t = 5 s.

### 3.4 Rule table (`SideRuleDef`; types in R2-0, effects in B1 with the tower)
`BattleSetup.Rules` are applied inside `BuildSideSetup`, so every mode and floor is data:

| Effect | Applied as | Notes |
|---|---|---|
| EnemyHpScale / EnemyDamageScale / EnemySpeedScale | multiplies the spawn HP (SPEC4 `Protection.EnemyHpScale` path, split children inherit), the damage scale, the speed scale | `value` = multiplier |
| WaveSizeScale | × wave size | – |
| EliteEvery | every Nth spawn × 3 HP (an '정예' tint) | `value` = N |
| ShopHpScale | × shop max HP | – |
| ResonanceRegenScale | × resonance regen | – |
| EchoCostDelta | + summon cost | – |
| ClosedPads | N pads closed by seed (their guardians removed from the setup) | `value` = N |
| LampOff | lamp button disabled, AUTO never lights | – |
| EchoLifetimeScale | × echo lifetime | – |
| ExtraModifier | a second stage modifier applied on top (`BattleSetup.ExtraModifier`) | `modifier` |
| Roster | checked by `FormationSystem.Satisfies` before the start (and by 맞춤 편성) | `roster` (+`value`, `trait`) |
- Rows in the same `group` exclude each other. `risk` (1–3) is display only. Trait-only roster rows are allowed only when the player owns ≥ 3 lineages with that trait (`TraitOnly` refuses otherwise and the content validator rejects a trial floor that would need it).
- Starter rows (content R2P-5, Appendix C.6): 단단한 먼지, 날카로운 녹, 재촉하는 바람, 몰려드는 밤, 정예 그림자, 낡은 벽, 잔향 가뭄, 비싼 부름, 빈 방위, 꺼진 등불, 짧은 꿈, the seven trial rules, the two support rows 튼튼한 문 / 든든한 바늘, the ten walk depth rows and the place-2 rule '야시장'.

### 3.5 Mode HUD, centre button, result card, explainer
- **`Battle/SideHud`** (`SideHudView`, R2P-1, built by `BattleSceneBuilder`): occupies the StagePanel rect (screen 14–344 × 142–276); `StagePanel` fades to α0 (raycast off) while a side battle runs. Lines: mode + level title (Title 28 bold Cream, e.g. '그림자 까치 쫓기 Lv.12'), the time bar (`UiArt.Bar` 300×18, Lavender, 'm:ss' ClockMono Label 24), the score line (Label 24: '떼어 낸 껍질 3' · '바구니 2/5' · '피해 46%' · '{f}층'). The RecommendedPower pill shows the mode's own rec power (errand level, tower floor; hidden for the guest).
- **`Battle/MetaHud/SideBattleBar`** (`SideBattleBarView`, R2P-4a, built by `MetaSceneBuilder`): the GuideQuestBar rect (16–484 × 1492–1576); the guide bar fades out while a side battle runs. Left: up to 3 chips 64×64 with a 20 px caption (favoured style, rumour, rules; tap → `InfoTooltipView`). Right: [결과 바로 보기] Secondary 200×64 (appears at 5 s). In the popup fallback (§2.6) a [그만하기] Ghost button 120×64 sits left of it.
- **Centre button:** §2.6.
- **`SideBattleResultPopup`** (R2P-4a, `BuildPopup 860×980`, not a queue kind): mode banner 780×220 (mode art, cover-fit, scrim), result line (H2 32), detail lines (Body 26), tier pips or a percentage ring where relevant, rewards grid (`MetaRewardChip` 96), `[확인]` Gold 520×112 (fly rewards → `ResumeMainAfterSide()` → reopen the hub at that mode page), `[다시 하기]` Secondary 300×96 (only when another attempt is possible; re-enters directly), `[사냥으로]` Ghost (resume, stay in MAIN). Back/dim = `[확인]`.
- **`ModeIntroPopup`** (R2P-4a): the first entry of each mode page (flag `hint.mode.{mode}`) shows a 3-card '이렇게 해요' strip reusing the FeatureUnlockCard layout 840×620 (dots, [다음] Primary, [시작하기] Gold on card 3, skip × at TopRight). Copy: Appendix A.2. The unlock card says only *that* a mode opened (SPEC4 rule).

### 3.6 What side battles touch
- Count: `EnemyKill`, `EchoSummon`, `LampLit` (live only), the mode's own counters (§2.2), Codex first-kill discovery of new enemies, CR-03 stats (the recorder resets on `BattleRestarted`).
- Never: `StageClear`, `BossKill`, `StageFail`, `highestStage`, `challengeTarget`, farm counters, chapter chests, story pages, CR-01, SPEC4 hidden records h2/h3 (they key on stages), fireflies.
- PlayLog (SPEC4 CR-09, append): `side_start {mode, src, lv}`, `side_end {mode, src, lv, res, score, sec, ff}`, `rounds {steps, sec}`.
- Audio: `AudioService.PushMusic(MusicState.X, owner)` for modes that have their own music (guest, tower, walk; §9.2) on `SideBattleStarted`, `PopMusic` on resume.

---

## 4. The curve and economy package (prerequisite; source: PROGRESSION-SIM)

### 4.1 Second enemy taper (`Data/BattleConfig.cs`, R2P-C)
Exactly the harness code (PROGRESSION-SIM §8.1): `stageGrowthTaperStage2 = 151`, `stageHpGrowthLate2 = 1.02f`, `stageDamageGrowthLate2 = 1.015f`, and `GetStageHpScale/GetStageDamageScale` through the three-segment `TaperedScale` (capped at 1e30). Stages 1–150 are unchanged. Re-run `IdleDataBuilder` so `BattleConfig.asset` serializes the fields. Dungeon floors keep their own growth. Tests: continuity at 150/151 (ratio = exactly ×1.02 / ×1.015), equality with the harness's patched curve for stages 1–300 (the `ProgSim selftest` table), and `stageGrowthTaperStage2 = 0` reproduces today's curve.

### 4.2 CR-07 retune (R2P-C)
| Setting | From → to | Where |
|---|---|---|
| 수선점 단추/s growth | `goldPerSecondGrowth` 1.10 → **1.06** | `Data/GameConfig.asset` YAML (IdleDataBuilder keeps inspector values) **and** the C# default |
| 수선 바구니 per-stage growth | `IdleChestConfig.stageGrowth` 1.02 → **1.0025** | `Editor/Content/MetaContent.IdleChest.cs` (+ doc comment) |
| 바구니 별조각 line ceiling | SPEC4 C-01 `maxStageMultiplier` = 1.5 on `Line(Gem(3))` | must be on (SPEC4 §13.4) |
- **Why C-01 is mandatory with this package.** The sim's R2 run earns ≈1,200 별조각/week more than the no-R2 run (3.6k vs 2.4k, `report_tables.md`) mostly because R2-04 lifts the stage and the basket's 별조각 line grows with it (×1.0025^449 ≈ 3.1 at stage 450). With the ×1.5 cap the line stops at 4.5/h from stage ≈163, so the R2 guard (§4.5) measures R2's own sources, not a basket leak.
- The basket's 단추 line becomes cosmetic late in the game (clear 단추 dominates from week 2, sim §6.1). ~~Accepted; a later "N minutes of current-stage clears" basket formula is out of scope.~~ (CR4-05) Not accepted: every `GoldMinutes` reward and price has the same defect, so §4.6 fixes both.

### 4.3 Power rating repair (R2P-C in `PowerSystem.cs`; calibration R2P-5)
1. **Shop-share cap.** `TeamPower = offense + min(shopHp × shopHpWeight, offense × shopShareCap)` with `offense = GuardianPower + EchoPower` and `shopShareCap = 1` (the shop term is at most half the total). Today shop HP (300 × 1.04^(Lv−1)) dominates once 수선점 Lv > ≈150, so power/rec runs from 30× to 900,000× and every page reads '여유'.
2. **Third recommended-power segment.** `PowerConfig.recommendedTaperStage2 = 151`, `recommendedGrowthLate2` fitted by `PowerCalibration` (R2P-5) on stages 151–400 with the curve package, mirroring the enemy taper. SPEC4's two-segment fit (1–30, 31–150) is re-run on the new TeamPower.
3. **Acceptance (advisory, measured by the sim harness, §10.3):** for the engaged player the rating chip on errand levels and tower floors predicts the live outcome (여유 → win, 어려움 → loss) on ≥ 80% of attempts; need/rec on main stages 100–500 stays within 0.6–1.6.
4. The rating stays advisory in every R2 rule: nothing is gated on it.

### 4.4 Projection checks (replace CR-07's chapter-1 band; R2P-7)
SPEC4's `EconomyFlowTests` stays as a smoke test without the 15–30 s chapter-1 band (unreachable while the journal chest pays `GoldMinutes(30)` at 1-7). The binding check is the sim harness (`phase4b/sim`), re-run by R2P-7 at the end of R2-0, A and B with the real implementation (the `R2Systems` model is removed once `TechniqueSystem` and the breaks exist; the agent calls their public API). Engaged player, 2 × 20 min/day, 6 seeds minimum (12 for the final wave-B report), medians:

| Check | Target (from the 12-seed package) | Fail if |
|---|---|---|
| chapter 10 cleared | ≤ day 2 | > day 2 |
| chapter 15 cleared | days 4–10 | outside |
| chapter 25 cleared | days 10–18 | outside |
| first ≥ 7-day stall | after chapter 45 (no stall before day 40) | before |
| stage on day 30 / 60 | 400–520 / 500–600 | outside |
| 별조각 earned in week 4 / week 8 | **2,800–3,800 / 2,600–3,600** (C-01 on, SPEC4 built, R2 wave A on), logged per source (CR4-02) | outside. Knobs, in order: the C-01 gem-line cap `maxStageMultiplier` 1.5 → 1.25 → 1.0, then `GameConfig.levelRewardGem` 50 → 30 |
| `auto` player (AutoFormation only, 3 seeds; CR4-06) | ch25 ≤ day 17; no ≥ 7-day stall before day 40 | outside |
| (if Q15) 단추 N분치 (CR4-05) | "60분치" is worth 30–90 minutes of the player's own measured 단추 per online minute on days 1–40; the night basket's 단추 is 25–50 % of a day's 단추 | outside. Tune `income.referenceClearSeconds`, then `offlineGoldMinutesPerHour`, then the R2 N values |
| R2 off (techniques and breaks disabled) | a ≥ 7-day stall before chapter 16 | no stall (then R2-04 is not doing the work and its numbers need review) |
| stage failures, 90 days | ≥ 30 (shop durability matters) | 0 |
- The harness also logs the new modes' payouts per week (errand, guest, tower, walk) for the §4.5 table.
- **(CR4-08) Every band binds at every run:** #2 at G5, the B1 run at G6, #3 at G7. Wave B adds power the sim never modelled (keepsakes, workbench, riders, new forms, codex bonuses). If a run breaks a chapter band, trim in this order: keepsake values, workbench `perLevel`, rider values, codex percentages.
- **(CR4-02) Why the 별조각 band moved.** The v1.0 band (2,600–3,400) was SPEC4's *modelled* 2,640 plus R2. The harness *measures* more:
  - 2,399/week from the pre-SPEC4 sources alone (R2 off), and 3,609 with R2-04/05 and no C-01 (`analysis/report_tables.md`).
  - Its basket credits ≈26–39 gem-hours a day (24 h plus the free and ticket quick idles), not 18.
  - The shipped shop-level mail (별조각 50 every 10 levels) adds 350–550 a week in weeks 2–4.

  With SPEC4's sources, the ch6+ chapter-chest fix (PHASE4-ROADMAP RD-13) and C-01 on, week 4 projects to ≈3.7–4.1k, or ≈3.3–3.8k with the gem-line cap at 1.0. The new band spans IDLE_RESEARCH2's cadence (≈2.9k) and the income the curve was validated with (≈3.6k). P4-9 logs the R2-off baseline per source at G3, and every later run is compared with it.

### 4.5 R2 economy budget (typical engaged player, week 4+, against SPEC4's 2,640/week baseline)

Week-4+ rates use the sim's engaged timeline (stage ≈ 150 / 270 / 450 / 550 on days 7 / 14 / 30 / 60). "Weeks 1–4" is the first-month burst while levels and floors catch up with the stage; it is reported, not guarded (like SPEC4's codex and record bursts).

(CR4-03) **Guard window.** "Week 4+" means the mean of weeks 5–8. Week 4 itself (days 21–28, the G5 measurement window) still climbs ≈85 stages, so the stage-tracking rows pay more there:
- errand milestones ≈1.7 per errand (≈60 별조각 with tickets for errands 1–2);
- tower trial floors ≈2.9 (≈57 별조각 and ≈6 선택 기억편).

R2's own week 4 is therefore ≈230 (wave A + tower) to ≈295 (with B). It is reported with a ≤ 350 limit; the ≤ 250 guard applies to weeks 5–8.

(CR4-01) **R2-induced flows through other systems are not R2 rows, but R2 causes them.** R2-04 lets the engaged player complete 6.5–10.5 chapters a week in weeks 2–4. SPEC4's chapter-chest default row (별조각 150 on every ch6+ chapter) would then pay ≈975–1,575 a week, so PHASE4-ROADMAP RD-13 cuts it to 별조각 100 on chapters divisible by 5. The shipped shop-level mail (≈350–550 a week in weeks 2–4) is already inside the measured baseline. Both show up in the per-source log (§4.4).

| Source | 별조각 /week, week 4+ (weeks 1–4) | One-time 별조각 | 선택 기억편 /week | Lineage 기억편 /week | Notes |
|---|---|---|---|---|---|
| R2-01 errands 1–3 | ≈ 14 + 빠른 수선권 ≈ 4 (≈ 36 + 16 for errands 1–2) | – | 0 | 0 | milestone chest every 10 levels: 별조각 10 + bundle; levels track the stage, ≈ 0.5 milestones/week/errand after day 30 (S5-8) |
| R2-03 weekly guest | ≈ 90 (same) | – | ≈ 11 | 0 | band table Appendix C.2; 추억 가득 1–2 typical, upper 110 |
| R2-02 tower (B1) | ≈ 25 (≈ 60–80, plus ≈ 60 at unlock) | – | ≈ 3 (≈ 6) | 0 | trial floors only (별조각 20 + 선택 기억편 2); floors track the stage (S5-9) |
| R2-10 등불 길 (B2) | 50 | 500 (depth first clears) | 10 | ≤ 70 | weekly chest only; 10 reward walks/WeekKey |
| R2-13 festival week (B1) | ≈ 15 | – | ≈ 3.5 | 0 | 60 per monthly festival |
| R2-15 lantern (B1) | 0 | – | 0 | ≤ 7 | shard note once per DayKey |
| Q-04 R2 lines | ≈ 20 trickle | ≤ 800 lifetime | – | – | 8 lines × 4 tiers (10/20/30/40) |
| Codex rows (R2-36a/36b, new enemies) | 0 | +180 (6 costumes) · +300 (6 forms) · +40 (4 enemies) | 0 | 0 | SPEC4 `CodexConfig` values |
| Banks, kept gift, 놓친 선물 | 0 | – | 0 | 0 | return missed value only |
| Weekend market (B1) | sink | – | (sells ≤ 5) | – | 선택 기억편 ×5 for 250, limit 1 |
| R2-36a season costumes (B, on Q-06) | −≈ 12 | +30 per costume | −≈ 7.5 | 0 | replaces Q-06's tier-30 fallback |
| **P0+P1 total** | **≈ 206 (guard ≤ 250); weeks 1–4 ≈ 300–340** | | **≈ 20 (guard ≤ 30)** | **≤ 77 (guard ≤ 80)** | |
- Result: ≈ 2,640 → ≈ 2,850/week from week 4, a 60-pull pity every ≈ 1.9 weeks (the cadence IDLE_RESEARCH2 aimed for); the first month is faster by design (catch-up bursts). (CR4-02) These are **modelled** totals. The binding totals are the measured bands of §4.4 (week 4: 2,800–3,800, week 8: 2,600–3,600); at their top the pity comes about every 1.4 weeks.
- Knobs (content): errand milestone gems, guest band gems, tower trial gems, reward walks/week, festival round gems, market prices.
- `EconomyBudgetTests` R2 block (§10.2) asserts each row from content, both totals, the two shard guards, "the basket 별조각 per-hour is identical with every R2 modifier at maximum", "R2 never grants a `CurrencyType` other than Gold/Gem/EventToken", and "the rollover grants at most what the period had earned".

### 4.6 단추 N분치 follow real income (CR4-05; PHASE4-ROADMAP Q15, default yes)
**The defect.** `MetaReward.GoldMinutes(N)` resolves as `Growth.GoldPerSecond × 60 × N`, that is, minutes of the shop's passive income (`MetaRewardService.cs:10`). After the CR-07 retune, stage clears are ≈100% of 단추 from week 2 (sim §6.1); on day 1 they already earn ≈37× the shop rate per online minute. In the sim's package runs (`R_REC_r2`, medians), "단추 60분치" is:

| Day | Share of that day's 단추 |
|---|---|
| 1 | 3.0 % |
| 6 | 1.5 % |
| 13 | 0.003 % |
| 20 onward | ≈0 (day 29: 6.9e17 against 1.1e27) |

So from week 2 every R2 단추 reward is decorative. That includes errand 1's whole payout, the guest bands' 60분치, tower floors, walk step 5 and the lantern's 단추 note. Every 단추-minute price is free: workbench research, keepsake polish, rename, market items, the merge recipe, and P2's decor, 끝없는 밤 and 의뢰 rewards. §4.2 accepted this for the basket only.

**The fix (contract R2P-0, RM-4; implementation RM-5).**
- `IncomePerSecond = Growth.GoldPerSecond + Battle.ClearGoldAt(highestStage) / income.referenceClearSeconds` (default 180 s, about one frontier clear per three minutes of play). `GoldForMinutes(N) = floor(60 × N × IncomePerSecond)` through `MetaRewardService.IncomePerSecond` (§2.5).
  - Below ≈stage 30 the shop rate dominates, so early rewards move only 5–15 % and the CR-07 early tuning stands.
  - Late, "N분치" means N minutes of frontier play, which is what the name promised.
- **The night basket:** with `IdleChestConfig.offlineGoldMinutesPerHour > 0` (default 1.5), the offline 단추 line pays that many 단추 N분치 per hour away. A 12-hour night ≈ 18분치, ≈ 25–50 % of a 2 × 20-min player's daily 단추. Online passive income (`GoldPerSecond` while open) is unchanged.
- Optional (R2P-4c, B1): the top bar's '초당 +{rate}' shows `IncomePerSecond`.
- **Harness gate (R2P-7, projection #2):** the §4.4 row "단추 N분치" plus every chapter band.
  - R2-04's rows cost ×1.10 per level, so doubling 단추 adds only ≈7 levels per row. The effect is real but logarithmic.
  - If a band breaks, R2P-5 lowers the N values (errand 1 first) or raises `referenceClearSeconds`.
- **Tests (`IncomeTests`):** a null hook or `income.enabled = false` reproduces today's values; at stage ≤ 30 the value is within 15 % of today's; at stage 150+ it tracks `ClearGoldAt`; the basket line honours `offlineGoldMinutesPerHour`; nothing touches the 별조각 line.
- **If Q15 is declined:** errand 1 pays materials instead of 단추 minutes, the 단추-minute prices are re-priced in 단추 per level, and this section's hooks stay unset.

---

## 5. Wave A — P0 systems

### 5.1 R2-03 이번 주의 손님 (weekly guest) (P0 · M; R2P-2 system, R2P-1 script and door view, R2P-4a pages and reveal, R2P-5 content, R2P-6 art)
**Story.** Every Monday 05:00 a big forgotten object checks into the shop for a week of mending, wrapped in a **망각의 껍질**. We never fight the guest; we peel the shadow off it. The next Monday's first session shows it mended.

**Week and guest.**
- `weekIndex = DaysBetween(anchorMonday "2026-11-02", WeekKey) / 7` (0 before the anchor).
- While only the launch portraits exist (`launch = true`, 4 rows): `guest = launchSet[weekIndex mod 4]`. Launch set, in order: 낡은 옷장 할아버지 [천·밤], 멈춘 괘종시계 [금속·소리], 금 간 거울 [빛·치장], 녹슨 자전거 종 [금속·비] — together they cover all 7 traits, and the 비 guest pairs 비 with 금속 (2 lineages), the critic's rule.
- When all 12 portraits exist (content flag `GuestConfig.launchCount = 12`), `guest = guests[weekIndex mod 12]` by `order`. Each guest returns every 12 weeks, so nothing is lost for good.
- **The rumour:** the week's two traits (`RumourTraits`) also give +15% damage to units with either trait in errands (wave A) and in tower normal floors and 등불 길 fights (wave B). Never on the main stage, trial floors or (P2) puzzles. Shown in the 밤 나들이 header (§7.2).

**Daily fight '그림자 떼어 내기'** (`BattleMode.GuestShadow`, live, 90 s battle time):
- **HP snapshot:** `hpStage = max(1, highestStage − 5)`, taken at the week's **first attempt** and kept for the week (CR2-11). Layer HP = `2 × bossHp(hpStage)` (`bossHp(s)` = `boss_shadow.hp × GetStageHpScale(s)`); the second shell's layers × 1.5.
- **Script:** one objective, the shell (`boss_shadow` tinted with `GuestDef.tint`), spawns at 1.5 s on the top lane, advances like a boss and sieges the shop. Ten phases (two shells × five layers). Layer behaviours, the same in both shells: 1 plain · 2 a burst of 좀나방 ×6 (`enemy_moth`, FabricEater) · 3 + `LampDrain` · 4 on peel splits into 망각의 그림자 ×2 (`enemy_wisp`) · 5 + `Enrage`. Each peel knocks the shell back to the spawn radius and raises `ObjectivePhase`. No normal spawns.
- **Tier = layers peeled** (0–10). Ten peeled ends the fight early as a full clear. The shop can fall: the attempt ends and the layers already peeled still count.
- **Bonuses:** favoured traits +30% damage (`Modifiers.Lineage`), **'모두 함께'** +1% per unlocked form, cap +30% (`Modifiers.Team`, chip '모두 함께 +{n}%'), plus everything the shared setup adds (codex, techniques, cheer, costumes).
- **Attempts:** 3 per DayKey. The day's best tier counts. A fully unused day banks +1 attempt (bank ≤ 4, R2-07). A banked attempt scores as its own **catch-up best** (S5-12). An abandoned attempt (centre button) uses nothing.
- **The guest at the door** (`GuestDoorView`, R2P-1, GuestShadow mode only): the guest portrait stands behind the shop at the door side (field-local, left of the shop, height 0.9 × the shop sprite), wrapped in five concentric shadow rings (`FX/glow_soft` tinted with `GuestDef.tint`, α 0.35) and the faint `boss_shadow` silhouette (α 0.25). Each `ObjectivePhase` fades one ring in 0.3 s with SPEC4's shield-break FX at the shell. For the second shell the rings return thinner (α 0.2). 0 credits beyond the portraits.

**Stitches (바늘땀) into the weekly meter:**

| Source | Stitches | Cap |
|---|---|---|
| best attempt of the day | 4 × tier (only the improvement is added when the best rises) | 40/day |
| each banked (catch-up) attempt | 4 × its tier | bank ≤ 4 |
| main stage clear with ≥ 1 favoured-trait guardian / ≥ 3 | +1 / +2 | 60/day |
| expedition claimed (`ExpeditionSystem.ZoneClaimed`, hook landed in R2-0) | +3 per expedition | 24/day |
| basket claim (`IdleChest.Claimed`) | +2 per full hour away | 24 per claim |
- Engaged ≈ 130/day (≈ 920/week → 추억 가득 1–2); idle-only ≈ 80/day (반짝 4 – 새것처럼 2).
- **25 weekly tiers** (cumulative stitches): 실금 1–5 = 15/35/60/90/125 · 반짝 1–5 = 165/210/260/315/375 · 새것처럼 1–5 = 440/510/585/665/750 · 추억 가득 1–10 = 840/930/1,020/1,110/1,200/1,290/1,380/1,470/1,540/1,600.

**Rewards (final tier only, S5-11).** At `WeekChanging(old, new)` the system computes the old week's final tier, grants that tier's band reward through `MetaRewards.Grant` (Appendix C.2: 별조각 10–110, 선택 기억편 3–15, materials; 새것처럼+ adds the guest's memento keepsake from wave B2, held as pending until then), stores `reveal = {old week, guest, tier, shown = false}`, and adds the week to `rewardedWeeks`. The **first 추억 가득 ever** also grants the costume `udu_raincoat` (R2-36a) once (`costumeGranted`). Typical ≈ 90 별조각 and ≈ 11 선택 기억편 per week (§4.5).

**Reveal (`MetaPopupKind.GuestReveal`).** `AfterStartup` enqueues it when `reveal != null && !shown` (startup kind after Return, §2.9; if the week changed mid-session it shows at the next between-stages moment). `GuestRevealPopup` (R2P-4a, `BuildPopup 900×1300`, title '지난주의 손님'):
- a 760×620 card: the guest portrait (PanelInset9s) with the gold stitch mark overlay (`UI/stitch_mark`, 3 patches placed by content offsets) and a 0.4 s "shell falls away" fade of the tinted ring;
- '{이름}{이/가} 다 나았어요' (H2 32); the thank-you line (Body 26, quoted, Appendix A.3); band and step '새것처럼 2' with 25 tier pips;
- rewards grid (`MetaRewardChip` 96); [고마워요] Gold 520×112 → fly the rewards (already granted, S5-11) → `MarkRevealShown()`;
- footer teaser (Caption 22): '이번 주에는 {다음 손님}{이/가} 찾아왔어요 ›' → `OpenOutings(Guest)`.
- `jingle_guest_reveal` (§9.2). Suppressed during FTUE.

**Codex [손님] tab** (`CodexCategory.Guest`, 12 entries, 0 별조각): found on the first attempt of that guest's week; the cell shows the portrait and, once that guest finished at 새것처럼+, its thank-you line; unfound = silhouette + '{n}주 뒤에 찾아와요' (or '언젠가 찾아와요' while the guest is outside the launch cycle). `weeksLikeNew` feeds `GuestTier "weeks"` (Q-04 `ach_guest`); `weeksFullBloom` feeds `GuestTier "bloomWeeks"` (the title '손님맞이 장인', 4 weeks at 추억 가득). Both count once per WeekKey, at the rollover grant.

**API (`WeeklyGuestSystem : IMetaModule, IDayRolloverModule, ISideBattleModule`):**
- `event Action Changed`, `bool IsUnlocked`, `GuestDef Current`, `TimeSpan TimeLeft`, `TraitType[] RumourTraits` (null while locked)
- `long Stitches`, `int Tier` (0–25), `GuestBand Band`, `int BandStep`, `int NextThreshold`, `void GetStitchBreakdown(out int attempts, out int stage, out int exp, out int basket)`
- `int AttemptsLeftToday`, `int Banked`, `int BestTierToday`, `int BestTierEver`, `float OwnedFormsPercent`, `bool AutoForm {get;set;}`
- `bool CanAttempt(out string reason)`, `bool TryBeginAttempt()` (uses today's attempt first, then a banked one), `bool TryStartHeadless(out SideBattleRunner runner)` + `void CommitHeadless(SideBattleResult r)` (rounds)
- `IReadOnlyList<MetaReward> RewardFor(int tier)`, `GuestRevealSave PendingReveal`, `void MarkRevealShown()`, `FormationSnapshot PreviewFormation()`
- `OnSideBattleEnded`: tier = `result.Phases + result.ObjectivesKilled` (the tenth layer dies instead of phasing); best-of-day and stitches; `GuestAttempt` +1; `GuestStitches` +gain; personal best. Tampered clock: the attempt plays but records nothing ('기기 시간이 이전으로 바뀌었어요…', SPEC4 `TamperedMessage`).
- Red dot `top.outings.guest` (§2.7).

**UI (§7.2 for the frame):** the hub card '이번 주의 손님 · {이름} · {등급 이름} {n} · 오늘 {k}/3' (+ '보관 {b}'); the guest page (portrait, traits, 모두 함께, best line, meter with 25 pips and the breakdown, next-reward preview, 맞춤 편성 row, [그림자 떼어 내기] Gold); the side HUD '그림자 떼어 내기 · {이름}' / '떼어 낸 껍질 {n}'; the result card '껍질 {n}겹을 떼어 냈어요' + '오늘 최고 {best}단계 · 바늘땀 +{s}'.
**Explainer (first entry):** Appendix A.2 (3 cards).
**Art:** 4 launch portraits (`Art/Guests/guest_{wardrobe,clock,mirror,bell}.png`, 1024², transparent, max; §9.1); the other 8 are optional B content. Shell = tinted `boss_shadow` (0 credits). Fallback portrait: `Enemies/boss_shadow` tinted + the guest's name plate.
**Audio:** `guest_layer_peel` (each peel), `guest_shell_crack` (shell 1 → 2), `bgm_worldboss` while the fight runs (AUDIO-SPEC §2.11 reserved key, promoted to P0), `jingle_guest_reveal`.
**Tests (`WeeklyGuestTests`):** week index and the 4-guest launch cycle (anchor Monday, a Sunday 04:59 / Monday 05:00 boundary); hp snapshot at the first attempt and unchanged after later clears; layer HP ×2 and the second shell ×1.5; tier = phases; best-of-day only adds the improvement; caps (60 / 24 / 24 per claim / 40); bank +1 per fully unused day, ≤ 4, catch-up scoring; final-tier grant exactly once per week (re-entering WeekChanging twice grants once); reveal queued once and never during FTUE; first 추억 가득 costume once; 모두 함께 cap 30; tampered → no stitches and no grant; rumour null while locked; headless == live for a fixed seed with AUTO inputs.

### 5.2 R2-04 수선 비법 (mending techniques) (P0 · M; R2P-3 system, R2P-4a segment, R2P-5 rows)
The first uncapped offense axis: 단추 → permanent team stats.

| Row (id) | Effect per level (`StatType`) | Cap | Opens | Base cost (단추) |
|---|---|---|---|---|
| 바늘 끝 (`tq_needle`) | 지킴이·잔향이 공격 +1% (`AttackPercent`) | – | with the feature | 2,000 |
| 촘촘한 바느질 (`tq_stitch`) | 수선점 내구도 +1% (`ShopMaxHpPercent`) | – | with the feature | 2,000 |
| 손놀림 (`tq_hands`) | 공격 속도 +0.5% (`AttackSpeedPercent`) | Lv100 | with the feature | 5,000 |
| 매듭 확률 (`tq_knot`) | 치명 확률 +0.1% (`CritChancePercent`) | Lv300 | with the feature | 8,000 |
| 심지 손질 (`tq_wick`) | 불빛 충전 +0.5% (`LampGainPercent`) | Lv100 | with the feature | 8,000 |
| 단단한 매듭 (`tq_hardknot`) | 치명 피해 +1% (`CritDamagePercent`) | – | 매듭 확률 Lv50 | 10,000 |
| 그림자 다림질 (`tq_iron`) | 보스 피해 +1% (`BossDamagePercent`) | – | 바늘 끝 Lv100 | 20,000 |
| (B2) 두 땀 뜨기 (`tq_double`) | 두 번 꿰매기 확률 +0.1% (`DoubleHitChancePercent`) | Lv250 | 손놀림 Lv100 | 50,000 |
- Cost of level n = `base × 1.10^(n−1)`; ×10 = the sum of the next 10 (needs all 10 affordable); MAX = as many as affordable (≥ 1).
- **수선공 등급:** every 25 total levels = 1 rank; each rank multiplies attack and shop HP by **×1.02, compounding** (`Battle.ExtraAttackMultiplier`, `Battle.ShopHpMultiplier`, mirrored in `PowerSystem`). The sim's engaged player reaches rank ≈ 30 on day 1, ≈ 42 on day 7, ≈ 110–123 on day 30, ≈ 130 on day 59 — it is the axis that dissolves the chapter 12–16 soft walls (PROGRESSION-SIM §7.2).
- Constants are exactly the ones the harness ran (S5-1); changing a row, cap or the rank multiplier requires a harness re-run (sim §8.5).
- **Integration:** `TechniqueSystem.AddTeamModifiers(StatBlock team)` on `Synergy.ExtraTeamModifiers` (multicast with Codex), so the main stage, every side battle and `PowerSystem` read the same block. Buying raises `TechniqueLevelUp` (amount) — which also feeds SPEC4's `AnyUpgrade` sum — and marks power dirty. A new rank fires `RankUp(rank)`.
- **Performance (critic):** 손놀림 +50% and 두 땀 뜨기 multiply hit events by up to ≈ 1.9×. 두 땀 뜨기 is a damage proc on the same projectile, merged into one '×2' number, never a second projectile. R2P-7 re-runs SPEC4's perf capture with all rows maxed (§10.4).
- **Gate:** Techniques ((3-4 or any lineage Lv60) & PlayMinutes ≥ 30). The system refuses buys while locked.

**API (`TechniqueSystem : IMetaModule`):** `event Action Changed`, `event Action<int> RankUp`, `bool IsUnlocked`, `int TotalLevels`, `int Rank`, `float RankMultiplier`, `int LevelsToNextRank`, `void GetRows(List<TechniqueRowStatus> into)`, `int GetLevel(string id)`, `bool IsOpen(string id, out string lockText)`, `double CostOf(string id, int levels)`, `int AffordableLevels(string id)`, `bool TryBuy(string id, int levels)`, `int BuyMax(string id)`, `void AddTeamModifiers(StatBlock team)`, `int BuyMode {get;set;}`. `TechniqueRowStatus { TechniqueDef Def; int Level; int Cap; bool Open; string LockText; double Cost; float Now; float Next; long PowerGain; }` (`PowerGain` from `Power.PreviewTechnique`, computed when the page opens and after each buy, never per gold tick).
**StrengthenAdvisor row `Technique`:** the open row with the best `PowerGain / cost`, '수선 비법 {이름} +{n}' / '전투력 +{gain}' → `GoToTab(Growth)` + segment.

**UI (강화 sheet, R2P-4a, `GrowthPanel`):**
- The `PanelHeader` row (0–88) gets `SegTabs` [수선점][수선 비법] (M53 Option, 2 × 200×64) at TopRight(−24,−12) once Techniques is unlocked; the NEW pill sits on [수선 비법] until it is first opened. The title '수선점 강화' stays at x 24.
- [수선 비법] shows **`GrowthPanel/TechniquePage`**, a sibling overlay Stretch(0,0,88,0) with the opaque `content_bg`, so the frozen paths `GrowthPanel/Buttons/LevelUp1Button` and `GrowthPanel/LineageList` are never reparented. [수선점] hides it.
- `RankCard` 1032×100 at Top(24,−8): rank medallion 80 (`UiArt.MedallionDark` + `UI/stitch_mark` + the rank number, H2 32 Cream), '수선공 등급 {r} · 공격력·내구도 ×{m:0.00}' (Title 28), `UiArt.Bar` 560×18 + '다음 등급까지 {n}레벨' (Caption 22), buy-mode chips [×1][×10][최대] (3 × 96×56, M53 Option) at the right.
- `Rows` ScrollRect Stretch(0,0,120,0): rows 1032×120, spacing 10: icon 88 (`Icons/skill_*`, Appendix C.3), name (Title 28) + 'Lv.{n}' (Label 24 Gold), effect '지킴이·잔향이 공격 +37% → +38%' (Caption 22, next value PositiveInk bold), 'Lv.{cap} 최대' when capped; right: `CostButtonView` 260×96 with `HoldRepeatButton` (delay 0.45 s). Locked rows α 0.55 with the opening line ('매듭 확률 Lv.50이 되면 열려요').
- Rank-up: `PowerUpFloatView` style float on the RankCard + toast '수선공 등급 {r}! 공격력·내구도 ×1.02' + `jingle_rank_up`.
- The M35d growth glow (UX-SPEC) is unchanged: it reflects the shop x10 only.
**Art:** reuse `Icons/skill_*` (0 credits; optional 7 icons ≈ 35, §9.1).
**Tests (`TechniqueTests`):** cost formula; ×10 needs all ten; MAX ≥ 1; caps; the two chains; rank every 25 and ×1.02^rank on the setup's attack multiplier and shop HP; the team StatBlock values; `PowerSystem.TeamPower` includes both; locked → refuses; `TechniqueLevelUp` and `AnyUpgrade` increments.

### 5.3 R2-05a 한 땀 돌파 + 추억 수놓기 (cap breaks) (P0 · S–M; R2P-3 `CharacterSystem`, R2P-4a detail popup, R2P-5 costs, R2P-6 mark)
- **Five breaks:** lineage cap 100 → 120 → 140 → 160 → 180 → 200 (`GetMaxLevel(id) = characterMaxLevel + 20 × capBreaks`, `AbsoluteMaxLevel = 200`). A break is allowed only at the current cap.

| Break | 기억편 (own lineage) | Material | 단추 |
|---|---|---|---|
| 1 | 40 | 오르골 태엽 (`mat_spring`) 2 | 10 × the next level's cost |
| 2 | 60 | 오르골 태엽 3 | same |
| 3 | 80 | 오르골 태엽 4 | same |
| 4 | 100 | 달빛 조각 (`mat_moonlight`) 1 | same |
| 5 | 120 | 달빛 조각 2 | same |
- Levels 101–200 keep `50 × 1.09^(lv−1)` 단추 and +8%/level. Every place that used `MaxLevel` for a lineage (`IsMaxLevel`, `GetLevelUpCost`, `TryLevelUp`, `CanLevelUp`, PRG-05 catch-up clamp, the growth cards' 'MAX') switches to `GetMaxLevel(id)`; `MaxLevel` stays as the base 100.
- **Marks, not stars:** each break stitches a gold cross-stitch mark (`UI/stitch_mark`); stars mean grade (`GameEnums.cs:13`).
- **추억 수놓기** (after the 5th break): 30 기억편 → +0.5% attack for that lineage, up to 20 times (+10%, 600 shards). It adds to the capped attack term: `attack = form × (1 + 8% × (lv−1)) × (1 + (resonance% + costume% + 0.5 × embroidery)/100)`.
- Shard sinks: resonance 1,100 + breaks 400 + embroidery 600 = 2,100 per lineage (14,700 for 7).
- Counters: `CapBreak` +1; `MaterialSpent` (B) by the materials used. Event `CapBroken(id, breaks)`.
- **Gate:** LevelBreak (any lineage Lv100). Locked → refuses.
- **StrengthenAdvisor row `CapBreak`:** a placed lineage at its cap with the break affordable: '{이름} 한 땀 돌파 가능' / '레벨 한계 +20' → `OpenCharacter(id)`.

**API (`CharacterSystem`, appended):** `int GetMaxLevel(string id)`, `int GetCapBreaks(string id)`, `int GetEmbroidery(string id)`, `bool CanCapBreak(string id, out CapBreakCost cost)` (`CapBreakCost { int Shards; string MaterialId; int MaterialAmount; double Gold; bool AtCap; bool Affordable; string MissingText; }`), `bool TryCapBreak(string id)`, `bool CanEmbroider(string id)`, `bool TryEmbroider(string id)`, `event Action<string,int> CapBroken`.

**UI (R2P-4a):**
- `CharacterDetailPopup`: at the cap, `_levelUpButton` (same rect) becomes **[한 땀 돌파]** Gold with cost chips (기억편 · 재료 · 단추; `MetaRewardChip` 48 in Short tint when missing). Tap → `UIManager.ShowConfirm(title '레벨 한계를 {cap}에서 {cap+20}으로 올릴까요?', body '{재료 목록}을 써요.', onOk → TryCapBreak, okLabel '돌파하기')` → stitch FX (reuse `SparkleBurst` + a 0.5 s gold thread line drawn across the portrait) + `jingle_cap_break` + toast '한 땀 돌파! 이제 Lv.{cap+20}까지 올릴 수 있어요'.
- Under the level line: 5 mark slots 28 px (filled `stitch_mark` / empty outline α 0.3); tooltip '한 땀 돌파 {n}/5 · 레벨 한계 {max}'.
- After 5 breaks the button becomes **[추억 수놓기 {k}/20]** Secondary with '기억편 30 · 공격력 +0.5%' and the toast '추억을 한 땀 수놓았어요. 공격력 +{k×0.5}%'; at 20 it reads '추억을 모두 수놓았어요' (Disabled).
- `EvolutionTreeView` lineage header and `CharacterCardView` show the marks (16 px, up to 5) next to the stars.
- Missing resources: Error toast `MetaText` shortfall line ('{재료}{이/가} 모자라요') + `UINav.GoToSource(itemId)` link.
**Art:** `UI/stitch_mark.png` (512², gold cross-stitch, transparent; 5 credits; fallback `Icons/star` tinted Gold at 0.8 scale — acceptable only as a placeholder because stars mean grade).
**Tests (`CapBreakTests`):** max level per break; refuse below the cap and at 5; costs per break; levels 101–200 cost formula; catch-up clamp uses the lineage's own cap; embroidery ≤ 20 and the attack term; counters; locked refuses; a v4 save loads with 0 breaks.

### 5.4 R2-01 밤 골목 심부름 (errands 1–2) (P0 · M; R2P-2 system, R2P-1 scripts and gimmicks, R2P-4a pages, R2P-5 rows, R2P-6 art)
**Loop.** Pick an errand → pick a level → a **live** 60 s fixed-seed fight with the formation (or 맞춤 편성) → reward by result. A ticket is spent only on a paying result; failure is free. Cleared levels can be swept instantly (no fight).

| # | Name (id) | requiredStage | Script | Pays (level L) | Full result (opens L+1, `ErrandClear`) | Favour |
|---|---|---|---|---|---|---|
| 1 | 그림자 까치 쫓기 (`errand_magpie`) | 12 (2-2) | one objective `enemy_magpie` (Fleeing, orbit r 1.75, hop every 3 s, never attacks); HP pool `3 × bossHp(s(L))`; no normal spawns | 단추 **(20 + 2L)분치 × p**; p = HP fraction removed, paid as max(0.3, p) once ≥ 10% is dealt | p = 1.0 | 저격 (Snipe) +100% damage ('저격 친구 피해 ×2') |
| 2 | 빨랫줄 걷기 (`errand_laundry`) | 14 (2-4) | a chain of 5 `enemy_nest` (Spawner, r 1.85, clockwise from the top: slots 0, 4, 3, 2, 1, one active at a time); each active nest summons `enemy_dust` every 2 s (≤ 6 alive); nest HP `8 × dustHp(s(L))` | per full clear: 수선 천 6+L · 리본 조각 6+L · 촛농 방울 4+⌊L/2⌋ · 레이스 조각 1+⌊L/5⌋ (L ≥ 5) · 우산살 1+⌊L/8⌋ (L ≥ 10); partial = 20% per nest down (≥ 1 nest pays) | all 5 nests | 돌격 (Assault) +50% ('돌격 친구 피해 ×1.5') |
| 3 | 다시 뭉치는 먼지 왕 (B1, §6.10) | 17 (2-7) | Reforming boss | tiers | tier ≥ 7 | 지원·힐 |
- **Stage equivalent** `s(L) = requiredStage + 5(L−1)` (S5-8). The page shows `rec(s(L))` with SPEC4's rating chip (advisory, §4.3).
- **Levels and skip-ahead:** `LevelForStage(S) = ⌊(S − requiredStage)/5⌋ + 1`; the stepper offers 1 … `MaxSelectable = max(highestLevel + 1, LevelForStage(highestStage) − 2)` and defaults to `lastLevel` (or `MaxSelectable` the first time). A full result at L sets `highestLevel = max(highestLevel, L)`.
- **Milestone chests** every 10 levels of `highestLevel` (granted once each, also when a skip-ahead crosses several): 별조각 10 + errand 1 빠른 수선권 1 · errand 2 레이스 조각 10 · errand 3 기억의 실 3. Toast '심부름 {n}단계 선물을 받았어요'.
- **Tickets:** 2 per DayKey per errand, stored up to 6 (+2 with the B2 workbench node). On the first access of a DayKey: `tickets = tickets < 0 ? 2 : min(cap, tickets + 2 × DaysBetween(refillDayKey, today))`. Tampered → no refill.
- **Rumour:** the week's guest traits +15% (`FormationBias.TraitPercent`), once WeeklyGuest is unlocked.
- **Sweeps** (`[소탕]`): any level ≤ `highestLevel`, instant, 1 ticket each, pays the full result of that level (errand 1: p = 1; errand 2: all nests; errand 3: its `bestTier`). `SweepMany(id, level, n)` merges the rewards; counts `ErrandRun` n (not `ErrandClear`).
- **Counters:** a paying live run or a sweep ticket = `ErrandRun` +1; a full result = `ErrandClear` +1. Q-02 daily '밤 골목 심부름 1번' (ErrandRun 1, 20 pts).
- **Economy:** ≈ 14 별조각 + ≈ 4 in tickets per week from week 4 (§4.5); 단추 and materials scale with L and are bounded by tickets.

**API (`ErrandSystem : IMetaModule, ISideBattleModule`):** `event Action Changed`, `bool IsUnlocked`, `void GetErrands(List<ErrandStatus> into)` (`ErrandStatus { ErrandDef Def; bool Unlocked; string LockText; int Tickets; int Cap; int HighestLevel; int MaxSelectable; bool NewPill; bool TicketsFull; }`), `int GetTickets(id)`, `int MaxSelectable(id)`, `long RecommendedPower(id, level)`, `void GetRewardPreview(id, level, List<MetaReward> into)`, `bool CanRun(id, level, out string reason)`, `bool TryBegin(id, level)`, `List<MetaReward> SweepMany(id, level, int count)`, `bool AutoForm(id)` / `SetAutoForm(id, bool)`, `FormationSnapshot PreviewFormation(id)`; `OnSideBattleEnded` pays and spends.
**Red dot** `top.outings.errand.{id}` (tickets at cap).

**UI (§7.2):** hub card '밤 골목 심부름 · 입장권 {합계} · 최고 Lv.{n}'; the errand list page (tiles 510×300); the errand page (banner, stepper, rec line, chips, reward preview, 맞춤 편성 row, [다녀오기] Gold, [소탕] Secondary → `ErrandSweepPopup`); the side HUD ('그림자 까치 쫓기 Lv.{L}', score '떼어 낸 그림자 {p}%' / '바구니 {k}/5'); the result card (Appendix A.4 lines).
**Explainer:** Appendix A.2.
**Art:** `Enemies/enemy_magpie.png` (shadow magpie of forgetting carrying a cream button sack; indigo/lavender shadow body, gold-button eyes), `Enemies/enemy_nest.png` + `enemy_nest_broken.png` (laundry-basket nest); banners reuse `UI/zone_exp_*`; §9.1. Fallbacks: `enemy_moth` tinted indigo ×1.3; `UI/idle_basket_2`. All three double as place-2 enemies (B2).
**Audio:** `errand_start`, `magpie_flutter` (hop), `magpie_sack_drop` (full result), `nest_spawn`, `nest_break`, `jingle_errand_clear`.
**Tests (`ErrandTests`):** ticket refill per skipped day, cap 6, first access 2, tamper; paying threshold (9.9% pays nothing, 10% pays 0.3); a failed or abandoned run spends nothing; full result opens L+1; MaxSelectable and skip-ahead; milestones once each across a skip; sweep instant, ≤ highestLevel, merges, counts; favour/rumour modifiers in the setup; Fleeing hop timing and lane change; nest chain order and one active nest; reward formulas at L = 1/5/10/50.

### 5.5 R2-07 banking + 오늘의 순회 (P0 · S; R2P-2 systems, R2P-4a UI)
**Banking** (returns value to people who skip a day; adds none for daily players, §4.5 row 0):

| Thing | Rule | Owner |
|---|---|---|
| 잔향이 던전 | SPEC4 NEW-04 unchanged (bank 9, Wednesday 11). If NEW-04 was cut, R2P-2 ships it as SPEC4 §13.9 specifies | DungeonSystem |
| 심부름 | 2/day, store 6 (§5.4) | ErrandSystem |
| 손님 attempts | a fully unused day banks +1, ≤ 4, catch-up scoring (§5.1) | WeeklyGuestSystem |
| 빠른 수선 (free use) | if yesterday's free use was not taken, it is kept for today (≤ 2 free uses today): `bankedFreeQuick = 1`, valid on that DayKey only; the banked one is used first and does not advance the day's cost ladder; the widget's free tag reads '무료 2' | IdleChestSystem |
| 오늘의 작은 선물 (NEW-02) | on the first access of a DayKey, `keptDayKey = yesterday` if yesterday's gift was not taken; it can be claimed today next to today's (`DailyGiftCardView` shows both, '어제 선물도 남아 있어요', [모두 받기]); expires at the next DayKey | DailyGiftSystem |
| 수선 일지 rewards | **놓친 선물:** at `DayChanging(old, new)` the rollover calls `Missions.AutoClaimPeriod(Daily, old)`, at `WeekChanging` `AutoClaimPeriod(Weekly, oldWeek)` — every completed mission and every reached chest of the old period, before `Reset`, unless the clock is tampered. The merged rewards go to `rounds.missedSummary` | DailyRoundsSystem (active whenever Missions is unlocked) |
- **Surfacing.** The Return popup (R2P-4a) adds one line '어제 못 받은 수선 일지 선물을 챙겨 뒀어요' with the reward chips when `missedSummary` is not empty, then clears it. If no Return popup shows (in-session rollover), a toast at the next between-stages moment with [보기 ›] → `RewardPopup`. Banked tickets/attempts/quick/gift → one toast at the first open of the DayKey: '어제 못 쓴 입장권을 모아 뒀어요' (or '어제 선물을 챙겨 뒀어요' when only the gift was kept).
- `JournalPoints` and `JournalComplete` increment as for a manual claim, so SPEC4 Q-04, NEW-01 and Q-06 stay consistent.

**오늘의 순회** (one tap; DailyRounds unlock §2.8):

| # | Step (id) | Action | Default |
|---|---|---|---|
| 1 | 바구니 받기 (`basket`) | `IdleChest.Claim(doubled: IsDoubleFree)` (never spends a 두 배 수선권) | on |
| 2 | 빠른 수선 (`quick`) | the free use(s) only (`NextQuickIdleCost == 0`), including the banked one; never 별조각, never 빠른 수선권 | on |
| 3 | 원정 받고 다시 보내기 (`expedition`) | `ClaimAll()` then SPEC4 `ResendAll()` (ExpeditionBatch) | on |
| 4 | 잔향이 던전 소탕 (`dungeon`) | `SweepMany(HighestFloor, TicketsLeft)` (DungeonBatchSweep), else single sweeps | on |
| 5 | 심부름 소탕 (`errands`) | each unlocked errand with `highestLevel ≥ 1`: `SweepMany(id, highestLevel, tickets)` | on |
| 6 | 손님 그림자 (자동) (`guest`) | one headless attempt with 맞춤 편성 if attempts are left (time-sliced runner) | **off** (CR2 critic: the live fight is the day's moment of play) |
| 7 | 보관소 다시 오르기 (`tower`, B1) | `Tower.TryAutoRetry(force: true)` once; never trial floors | on |
| 8 | 일지·출석·우편 받기 (`claims`) | `Missions.ClaimAll(Daily)` + `ClaimAll(Weekly)`; `Attendance.CheckIn()` if `CanCheckIn` (silent: its reward joins the summary, the attendance popup does not open); `Mail.ClaimAll()` | on |
- **Never** spends 별조각 or 빠른 수선권, pulls gacha, crafts or buys.
- Each step has its own toggle (`RoundsSettingsPopup`). Locked or empty steps are skipped with a reason.
- Headless steps run on `SideBattleRunner` at 2 ms/frame behind the summary's progress bar; nothing blocks a frame.
- One summary lists every step and the merged rewards; `RewardFlyer` flies once.
- Entry: [오늘의 순회 ▶] in the 밤 나들이 header, and a long-press (0.5 s) on the rail icon `OutingsButton`.
- Refused during FTUE and during a side battle ('나들이가 끝나면 할 수 있어요').

**API (`DailyRoundsSystem : IMetaModule, IDayRolloverModule`):** `bool IsUnlocked`, `bool IsRunning`, `float Progress`, `bool IsStepEnabled(RoundsStep s)`, `void SetStepEnabled(RoundsStep s, bool on)`, `bool Run()`, `event Action<RoundsResult> Finished`, `RoundsResult Last`, `IReadOnlyList<RewardSummary> MissedSummary`, `void MarkMissedShown()`. `RoundsResult { List<RoundsStepResult> Steps; List<MetaReward> Rewards; float Seconds; }`, `RoundsStepResult { RoundsStep Step; RoundsStepState State; string Line; List<MetaReward> Rewards; }`, `enum RoundsStepState { Done, Nothing, Locked, Off, NoTickets }`.
**UI (§7.6):** `RoundsPopup` (`BuildPopup 900×1300`, title '오늘의 순회'): 8 step rows 820×88 (icon 56, name Title 28, result Caption 22; a spinner while running), the progress bar 780×18 while headless steps run ('순회하는 중… {k}/{n}'), the merged rewards grid, [확인] Gold. `RoundsSettingsPopup` (`820×1000`, title '순회 설정'): 8 `MetaSwitch` rows; the guest row's desc '직접 하면 더 높은 기록이 나와요. 켜면 자동 전투로 한 번 해요'.
**Audio:** `rounds_step` (each step done, ladder +1 semitone), `rounds_done`.
**Tests (`RoundsTests`, `BankingTests`, `MissedRewardsTests`):** order and toggles; never spends gems or tickets (wallet before/after); guest off by default and on after toggling; headless step never exceeds 2 ms per frame (fake stopwatch); summary merges; refusal in FTUE and during a side battle; free-quick bank only for yesterday, used first, not on the ladder; kept gift claimable only today and expiring; a completed unclaimed mission is granted exactly once at rollover (and not again when the day re-enters); a tampered clock grants nothing; the weekly rollover after the daily one includes that day's points; a 3-day jump refills errand tickets to the cap and banks 3 guest attempts (≤ 4); a clock rollback refuses refills.

### 5.6 R2-08 맞춤 편성 (mode-aware auto-formation) (P0 support · S; R2P-3, R2P-4a)
- `FormationSnapshot FormationSystem.BuildBiased(FormationBias bias, Func<string,long> formPower)`: candidates are the unlocked forms of owned lineages, **expedition members included** (S5-7). Score = `formPower(form) × (1 + TraitPercent/100 × [lineage has a favoured trait]) × (1 + StylePercent/100 × [form style is favoured])`. Assignment = SPEC4/`AutoFormation`'s order (recommended role first, then the other role, guardians fill pads in `AutoGuardianOrder`), LineageLimit (1 guardian form + 1 echo form per lineage), the roster rules in `bias.Rules` (`MaxSameStyle`, `MaxDeckSlots`, `NoEchoes`, `MaxGuardians`, `ClosedPads`). Ties: power desc, then form id. **It never writes the save** (S5-6).
- Each mode keeps a switch `autoForm` (default on). On: the side request uses `BuildBiased`. Off: the current main formation. Either way the request carries the mode's `FormationBias`, so the trait/style damage bonuses always apply (§3.1.1); the switch only chooses who stands where.
- Biases: guest → its 2 traits, `TraitPercent 30`; errands → the rumour traits (15) + the errand's favoured style (`StylePercent` 100 / 50); tower trial floors (B1) → the floor's rule; 등불 길 (B2) → the rumour.
- **UI (each mode page, R2P-4a):** a row 1032×64: `MetaSwitch` '맞춤 편성' + a mini preview (5 pad icons 48 + 6 deck icons 40, `CharacterIconLibrary`; favoured units get a 2 px Gold ring) + '편성 바꾸기 ›' (Ghost 24: turns the switch off and opens the Formation sheet). Switching on toasts '이번 손님에게 맞춰 세웠어요' / '이번 심부름에 맞춰 세웠어요'.
- Manual memos stay deferred (D-11) until the roster passes 10 lineages.
- **(CR4-06) 자동 편성 v2, the main formation.** PROGRESSION-SIM §7.3: today's `AutoFormation` ranks by `attack/interval×10 + hp`, ignores the style damage scales (snipe ×1.7/1.5, assault ×1.6) and 공명, and fields tanky Defense forms. An AutoFormation-only player reaches ch25 on day 20.3 instead of 13.3 and stalls ≈150 stages lower. SPEC4's 강해지기 `AutoFormation` row then recommends that formation whenever its total power preview wins by 5 %.
  - With `FormationSystem.AutoFormationPower` set (§2.5), `AutoFormation()` keeps its signature, role order, pad order and LineageLimit, but ranks by `Power.GetFormPower` (the S5-24 offence term with style scale, 공명, costume and embroidery); HP only breaks ties.
  - The advisor row compares offence power.
  - The FTUE steps keep working: they only need *a* valid formation.
  - Tests: snipe/assault forms beat a Defense form of equal stats; the offence order is deterministic; a null hook reproduces today's order.
  - Projection #2 runs the `auto` player (§4.4).
- **Tests (`ModeFormationTests`):** favoured traits and styles win ties against slightly stronger units as the formula says; LineageLimit; roster rules; expedition members included; the save's formation is byte-identical before and after; deterministic for a fixed roster.

### 5.7 R2-36a 옷장 넓히기 + fitting room (P0 · S; R2P-5 content, R2P-3 unlock type, R2P-4a preview, R2P-6 art)
**Six costumes, one per lineage that has none** (all 10 existing costumes are 짝짝이's). Content uses the existing `Costume(...)` helper (`CharacterContent.cs`); attack bonus by rarity (3–8%, existing rule):

| Lineage | Costume (id) | Arc | Rarity | Unlock |
|---|---|---|---|---|
| 똑단이 | 잠옷 단추 똑단이 (`ttok_pajama`) | 떨어짐 → 다시 채움 | Rare | `Unlock.Resonance(3)` |
| 찰칵이 | 열쇠고리 여행자 찰칵이 (`chal_keyring`) | 잠김 → 열림 | Rare | `Unlock.Expeditions(10)` |
| 몽당이 | 생일 초 몽당이 (`mong_birthday`) | 사라짐 → 축하 | Epic | `Unlock.EventShop(150)` 별빛 사탕 (`ShopDatabase` row `event_mong_birthday`) |
| 우두커니 | 비 갠 날의 우비 우두커니 (`udu_raincoat`) | 기다림 → 마중 | Legendary | `CostumeUnlockType.Reward` (8) — granted by the first 추억 가득 guest week (§5.1) |
| 루즈리 | 무대 화장 루즈리 (`rouge_stage`) | 감춤 → 드러냄 | Epic | `Unlock.Stage(40)` (스테이지 4-10 도달) |
| 라라벨 | 자장가 라라벨 (`lala_lullaby`) | 두려움 → 위로 (never the 7th note) | Legendary | `Unlock.EventShop(300)` 별빛 사탕 (`event_lala_lullaby`) |
- `CostumeUnlockType.Reward`: `CostumeSystem` never auto-unlocks it; only `MetaReward.Costume` grants it; its acquisition text comes from `CostumeDef.acquisition`.
- Storybook texts (4 lines each, the `Costume(...)` description pattern): Appendix A.6.
- Economy: codex +6 × 30 = +180 별조각 lifetime; the two event-shop rows give 별빛 사탕 a sink again. SPEC4's `ach_costume` 3/6/10 unchanged.
- **Season costumes (wave B, R2-36a v1.2):** from Q-06 season 2, tier 30 grants the next unowned season costume in the order 우두커니 → 몽당이 → 루즈리 → 찰칵이 → 똑단이 → 라라벨 (`CostumeUnlockType.Season`, via `MetaReward.Costume`); an unfinished season carries that costume to the next tier 30; the fallback (별조각 50 + 선택 기억편 30) pays only when all six are owned. One costume per season ≈ 5 credits (§9.1).
**Fitting room (v1.2, R2P-4a in `CostumeListView`/`CostumeItemView`):** a tap on any costume row previews it: the detail portrait swaps (0.2 s cross-fade) and a 160×160 pad mock (`Battle/guardian_pad` + the costume sprite at pad scale) shows "in battle". Owned: [입히기] Primary (equip, `CostumeSystem.TryEquip`) / [되돌리기] Secondary (restore the worn look). Locked: the unlock line '{의상 이름} · {acquisition}' (Caption 22; `acquisition` is a full 해요체 sentence, Appendix A.6) and no button. The preview is transient: closing the popup or switching tabs restores the worn costume; it never equips by itself. First open toast (flag `hint.fitting`): '의상을 누르면 미리 입어 볼 수 있어요'.
**Art:** 6 costume battle sprites (`Art/Costumes/{id}.png`, same size and pivot as `jjak_*`; max; the lineage's base form as reference; udu/lala referenced as they are, no restyle); §9.1.
**Tests (`CostumeR2Tests`):** the validator accepts one costume per lineage; codex costume total 17; `Reward` never auto-unlocks and unlocks through `MetaReward.Costume`; event-shop rows priced 150/300 and buyable once; the fitting-room preview never changes `equippedCostume`; the season order and carry-over (B).

---

## 6. Wave B — P1 systems
Wave B ships in two sub-waves (§8.1): **B1** (tower + rule effects, titles + profile, errand 3, lantern, route marks, market, merge, festival + moon) and **B2** (등불 길, 안채 + workbench + keepsakes, new forms, the first new place, skill riders). B2 also waits for the art top-up answer (§9.1).

### 6.1 R2-02 보관소 오르기 (the lost-and-found climb) + rule effects (P1 · M; B1; R2P-2 system, R2P-1 rule effects and floor scripts, R2P-4b page, R2P-5 content, R2P-6 bg)
- **Floors:** `s(f) = 30 + 3f` (S5-9; floor 1 ≈ stage 33). The sim's engaged player can clear ≈ floor 40 on day 7, ≈ 80 on day 14, ≈ 140 on day 30 and ≈ 170 on day 60.
- **Normal floor** (45 s): one scripted wave of `1.5 × waveSize(s)` enemies from that stage's pool (`SideScript.WaveCount 1`, `ClearOnWaves`, `NoBoss`); cleared when the wave is dead and the shop stands; time up or a fallen shop = not cleared (free retry).
- **Boss floor** (every 5th that is not a 10th; 60 s): the wave plus the stage boss (`ClearOnBoss`).
- **시험 칸** (every 10th; 60 s): a normal floor plus **one rule** from the pool, cycled by `f/10`: '지킴이 3명만' (ClosedPads 2) · '잔향이 없이' (Roster NoEchoes) · '불 밝히기 금지' (LampOff) · '같은 역할 2명까지' (Roster MaxSameStyle 2) · '내구도 50%로 시작' (ShopHpScale 0.5) · '잔향이 덱 3칸만' (Roster MaxDeckSlots 3) · '비 오는 정전' (ExtraModifier: Rain + Blackout). Trial floors are never auto-run.
- **Blocks:** floors 1–5, 6–10, … carry one `StageModifierType` each, cycling Rain → Blackout → Wind → DustStorm → FullMoon → Festival. Shelf names cycle '{f}층 · 우산 칸 / 장갑 칸 / 열쇠 칸 / 단추 칸 / 양말 칸'.
- **First-clear rewards:** every floor 단추 10분치 · boss floors + 수선 천 10 + 리본 조각 10 · trial floors a chest: 별조각 20 + 선택 기억편 2 + 기억의 실 2 + 우산살 3 + **보관소 감사장** (+0.5% team attack, permanent, at most 10 = +5% at floor 100) · floors 20/40/60: titles '보관소 견습생' / '보관소 정리꾼' / '보관소 지배인' · floors 20/40/60/80: keepsakes (pending until the shelf exists, §6.5).
- **'자동으로 오르기'** (Secondary): headless batch from the next floor up to (not including) the next trial floor or the first failure, ≤ 30 floors per tap, on `SideBattleRunner` at 2 ms/frame behind a progress line '보관소를 정리하는 중… {f}층'. Rewards merge into one summary.
- **'자동 재도전'** (switch, default on): after a main-stage clear, if `TeamPower ≥ 1.03 × lastFailPower` and ≥ 10 minutes since the last automatic run, the current floor (never a trial floor) is run headless once. A win → toast '보관소 {f}층을 정리했어요!'.
- Rumour +15% on normal and boss floors; 맞춤 편성 on trial floors uses the rule (§5.6).
- Counters `TowerFloorClear`; Q-02 weekly '보관소 3층 오르기' (TowerFloorClear 3, 20 pts).
- **Rule effects (R2P-1):** every `SideRuleEffect` of §3.4 is implemented here, with `RuleTableTests` per effect. The B1 package also adds `BattleSetup.ExtraModifier` support in the sim (two modifiers' effects applied together).
**API (`TowerSystem : IMetaModule, ISideBattleModule`):** `bool IsUnlocked`, `int HighestFloor`, `int NextFloor`, `void GetFloors(int from, int to, List<TowerFloorStatus> into)` (`{ int Floor; int Stage; StageModifierType Modifier; SideRuleDef Rule; bool Boss; bool Trial; bool Cleared; long Rec; List<MetaReward> Reward; }`), `bool TryChallenge()`, `bool TryAutoClimb(out SideBattleRunner runner)`, `bool TryAutoRetry(bool force)`, `bool AutoRetry {get;set;}`, `int TrialBonusCount`, `float TrialBonusPercent`, `bool AutoForm {get;set;}`.
**UI (§7.2):** the hub card '보관소 오르기 · {n}층 · 다음 상자 {m}층'; the tower page: `UI/tower_bg` (shelf office, cover-fit) behind rows 1032×130 for the current floor ±3 (floor label Title 28 + shelf name Caption 22, modifier icon 48, rule chip, rec + rating chip, reward chips 56), the current floor highlighted with a Gold outline; [도전] Gold 520×112, [자동으로 오르기] Secondary 300×112, '자동 재도전' `MetaSwitch`, '다음 상자까지 {n}층 · 감사장 +{p}%' (Caption 22), 맞춤 편성 row on trial floors.
**Audio:** `jingle_tower_floor` (reserved key, promoted), `bgm_tower` (reserved, promoted) while a live floor runs.
**Tests (`TowerTests`, `RuleTableTests`):** stage per floor; boss/trial cadence; block modifiers; first-clear once; the 감사장 cap; auto-climb stops before trial floors and at the first fail and never exceeds 30; auto-retry gating (3%, 10 min, never trial); headless == live for a fixed floor; every rule effect.

### 6.2 R2-09 수선공 이름표 (titles) + R2-20 대표 친구 · 수선점 이름 (profile) (P1 · S + S; B1; R2P-2, R2P-4c, R2P-5)
**Titles.** 37 titles (Appendix B.1): '수선점 주인' (title #1, granted at `ftue.done`; backfilled on the v5 load, so the FTUE graduation pill '칭호 · 수선점 주인' — `TutorialSceneBuilder.cs:420` — becomes true), the final tier of every Q-04 line (17 SPEC4 + 8 R2 lines; content adds `MetaReward.Title` to those tiers, no 별조각 change), 보관소 20/40/60, 손님 추억 가득 ×4 '손님맞이 장인', first clear of 5-10 '밤 골목 지킴이', the five festival themes (first full run), optional '새싹 수선공' (SPEC4 NEW-01 150 chest).
- Equip one. **Cosmetic only** (critic): no basket or stat bonus.
- Shown on the shop's sign plate in battle (`ShopView` child `TitlePlate`: `UI/title_plate_9s` 220×44 under the shop sprite, Label 20 bold Cream, hidden when no title is equipped), in `ProfilePopup`, and later on the share card (P2).
- `TitleSystem : IMetaModule`: `void GetTitles(List<TitleStatus> into)`, `bool IsOwned(id)`, `string Equipped`, `bool TryEquip(id)`, `event Action Changed`; grants through `MetaRewards.ExtraGrant` (`MetaRewardType.Title`); toast on a new title '새 칭호를 얻었어요: {이름}' + a NEW pill on `LevelText`.

**Profile.** `ProfileSystem` (not a module):
- **Shop name** ≤ 8 characters after trim; default shows '수선점' (LevelText keeps '수선점 Lv.{n}' until a name is set, then '{이름} Lv.{n}', Fit 22); one free rename, then 단추 60분치 each. A local filter rejects blocked words (content list), URLs, '@', and digit runs ≥ 6: '이 이름은 쓸 수 없어요'. The name ends up on share cards (SAVE-01), so the filter is required (critic).
- **대표 친구** (one owned lineage): opens the Return popup with its own line (`PartnerLineDef.returnLine`, Appendix B.2); dozes on the sleep card ('zzz', replaces the moon icon in UX-SPEC-IA §4.9's status card; tap wakes as today); is the profile portrait; gets +10 affinity on the first login of each DayKey (`partnerBonusDayKey`); signs local notifications ('{이름}: {한 줄}', planner body prefix). No stats.
- **Naming bubble:** after `PlayMinutes ≥ 60` and `ftue.done`, once, at a between-stages moment, a bubble under `LevelText` '수선점 이름을 지어 줄까요? ›' (MetaPulse, 6 s) → `ProfilePopup`.
**UI (R2P-4c):** `TopBar/LevelText` becomes a Button (hit 292×40; it is not a frozen coach path) → `ProfilePopup` (`BuildPopup 820×1100`, title '수선점 명함'): partner portrait 280 + [바꾸기 ›], the shop name (H1 40) + [이름 바꾸기] Secondary ('무료' / '단추 60분'), the equipped title on `title_plate_9s` + [칭호 고르기 ›] → `TitlePickerPopup` (`820×1300`, rows 740×96: name Title 28, source Caption 22, [달기] / '단 칭호' Gold pill / locked α 0.55 with the source line), stats line (Caption 22: '수선점 Lv.{n} · 최고 {stage} · 친구 {k}명'). Text input: `TMP_InputField` 600×96, 8 characters.
**Tests (`TitleTests`, `ProfileTests`):** grant once; equip only owned; backfill on v5 load for saves past FTUE (and not before); filter cases; rename pricing; partner +10 once per DayKey; the Return line source.

### 6.3 R2-10 등불 길 (lantern-road run, MVP) (P1 · L; B2; R2P-2 system, R2P-1 fight scripts and crest specials, R2P-4b pages, R2P-5 content, R2P-6 art)
A decision-dense 8–12-minute run: walk from lamp to lamp, pick charms, keep the shop standing.
- **Layout (15 steps):** 1 전투 · 2 부적 · 3 전투 · 4 길가 상자 · 5 엘리트 · 6 부적 · 7 전투 · 8 가로등 쉼터 · 9 전투 · 10 엘리트 · 11 부적 · 12 그림자 상인 · 13 전투 · 14 쉼터 · 15 보스.
- **Difficulty:** `walkStage(depth) = max(10, highestStage − 15) + 4 × (depth − 1)`; a fight at step i uses `walkStage + 2 × ⌊i/5⌋`; elites ×1.8 HP and ×1.3 damage (`EliteEvery 3` too); the boss is a chapter boss at `walkStage + 6` with ×2 HP. Fights are live 30 s mini-waves (`WaveCount 1`, `ClearOnWaves`; the boss `ClearOnBoss`) through the side host; **the shop's durability carries over** (`ShopStartHpFraction`). A fallen shop ends the walk; rewards already reached are kept. AUTO is allowed; choice nodes pause.
- **Nodes:** 부적 고르기 = 1 of 3 run-only charms (4 with the talent) · 엘리트 = after the fight, 1 of 3 **rare** charms · 가로등 쉼터 = +30% durability · 그림자 상인 = pay 20% durability for a dark charm, at most once per walk ('그림자 상인이 손을 내밀어요. 조금 아플지도 몰라요.'; skipping is free) · 길가 상자 = materials (Appendix C.7) · 보스.
- **Charms:** 40 (Appendix B.3): SPEC4 B.4's 12 blessings + 28 new; each has one trait tag and a rarity (common / rare / dark). A duplicate upgrades it to tier 2 (`value2`). Charms are `StatBlock` entries on `Modifiers.Team` for the walk's fights only.
- **Crests (문장):** for each trait, count Σ tiers of charms with that tag; at 2/4/6 the trait's crest turns on (Appendix B.3: 21 crests). Four crest-6 specials need sim hooks (R2P-1): `ShopReviveOnce` (the shop comes back once at 30%, `ShopRevived`), `LampResonance` (+20 잔향 on each lamp), `BossRage` (+30% attack for 20 s when a boss appears), `CritResonance` (+1 잔향 per crit, ≤ 1 per 0.5 s); two are rules (`EnemySpeedScale 0.9`, `ExtraModifier Rain`).
- **밤의 깊이 (depth 1–10):** one rule each (Appendix B.3 rows). The player picks any depth ≤ `highestDepth + 1`. **First clear of a depth:** 별조각 50 (500 lifetime) **+ one 등불 나무 talent** of the player's choice (depths 1–8 → 8 talents: '다시 고르기 +1', '부적 네 개 중에서', '첫 부적은 귀한 것으로', '처음 내구도 +10%', '등불 하나 더', '길가 상자 +20%', '빌릴 친구 둘 중에서', '문장 하나 더 쉽게' (one crest threshold −1)). Depth 7 '졸음 길': a friend who fought two fights in a row dozes through the next one (left out of that setup), so deep walks ask for the whole roster.
- **Team:** the formation (or 맞춤 편성 with the rumour) + **1 빌린 친구**: lineage drawn uniformly over 7, then a form inside it (critic: 짝짝이 has 11 of 29 forms), seeded; level = SPEC4 PRG-05 catch-up; it takes an empty guardian pad, else an empty deck slot, else replaces the weakest deck echo for this walk (S5-16; shown with a '빌린 친구' ribbon). It arrives with a note from a neighbour lender (Appendix B.3: 우산가게 할머니, 시계방 쌍둥이, 빵집 고양이 꼬미 — story flavour only, no scores). An unowned form records the codex clue '??? — 등불 길에서 만나 봤어요' (no form is granted), and the result card offers '[{이름} 찾으러 가기 ›]' → the 소환 tab (no purchase prompt).
- **Energy (등불):** 1 per walk; +1 per 8 h (`EffectiveUnix`); cap 3 (+1 talent); overflow goes to a bank of ≤ 3 more, each expiring after 6 DayKeys (S5-15). `MetaRewardType.WalkLantern` grants lanterns (cap → bank).
- **Rewards (only the first 10 walks of each WeekKey pay; later walks still count for depths and practice):** step 5 단추 30분치 + 수선 천 10 · step 10 리본 조각 10 + 촛농 방울 6 · step 15 기억편 1 for each owned lineage in the walk team (≤ 7) + 기억의 실 2 + 우산살 3 · chests per Appendix C.7 · **weekly chest at 10 walks:** 별조각 50 + 선택 기억편 10 (once per WeekKey). Steady ≈ 50 별조각 + 10 선택 기억편 + ≤ 70 lineage 기억편 per week (§4.5).
- **Seed** `hash(DayKey, walkIndex)`: same day, same walk index, same offers and fights. The run is saved between nodes (`WalkRunSave`); a fight in progress is not (killed mid-fight → that fight restarts from the node).
- Counters `WalkStart`, `WalkClear`; Q-02 weekly '등불 길 3번' (WalkStart 3, 20 pts).
- If SPEC4 PRG-07 shipped, the charm pick reuses `BlessingPickPopup` (980×920, 3 cards 280×560); otherwise R2P-4b builds it to the SPEC4 §8.6 anatomy.
**API (`NightWalkSystem : IMetaModule, ISideBattleModule`):** `bool IsUnlocked`, `int Lanterns`, `int Banked`, `TimeSpan NextLantern`, `int HighestDepth`, `IReadOnlyCollection<string> Talents`, `bool CanStart(int depth, out string reason)`, `bool TryStart(int depth)`, `WalkRunSave Current`, `WalkNode NodeAt(int step)`, `IReadOnlyList<CharmDef> Offer`, `bool TryPickCharm(string id)`, `bool TryReroll()`, `bool TryMerchant(bool accept)`, `bool TryContinue()` (fights through the host), `void GetCrests(List<CrestStatus> into)`, `bool TryPickTalent(string id)`, `void Abandon()`, `event Action Changed`.
**UI (§7.2):** the hub card '등불 길 · 등불 {n}/3 · 깊이 {d}'; the walk page: the street strip (`UI/walk_strip`, 1032×300) with 15 lamp nodes (SPEC4 A3 node art: lamp off/on, boss ring, chest), the current step glowing; the next two nodes' names; the charm bar (icons 56 + tier pips); crest pips per trait (7 × trait icon 40 + 3 pips); depth picker ‹ 깊이 {d} › with its rule chip; lanterns '{n}/3 · 다음 등불 {h:mm}' (+ '보관 {b}'); [걷기] Gold / [이어 걷기] Gold; the talent tree popup (`820×1100`, 8 nodes). During fights the side HUD reads '등불 길 {i}/15 · 깊이 {d}' with a durability bar.
**Art:** `UI/walk_strip.png`, `Enemies/npc_shadow_merchant.png` (§9.1); node icons and charms reuse SPEC4 A3 and `Icons/skill_*` + `trait_*`; optional 4 dark-charm icons.
**Audio:** `walk_step`, `charm_pick` (= alias of `ui_blessing_pick`), `crest_light`, `merchant_whisper`, `walk_rest`, `jingle_walk_clear`, `bgm_lanternroad` (new MUS piece, §9.2).
**Tests (`NightWalkTests`):** a run is deterministic (seed → offers and fight results with AUTO); reward walks stop at 10 per WeekKey and the weekly chest pays once; durability carries and rests heal 30%; merchant once; crest counts by tier and the talent −1; borrowed-friend slot order; the codex clue for unowned forms; lantern regen across sleep/resume, cap, overflow expiry, tamper; depth first-clear gems once + one talent; '졸음 길'.

### 6.4 R2-11 밤의 작업대 + 수선점 안채 (P1 · M–L; B2; R2P-2, R2P-4c, R2P-5, R2P-6)
- **Slots:** 1 at unlock, 2nd after 8 completed jobs, 3rd after 24 (S5-18).
- **3 branches, 19 nodes** (Appendix B.4: 실 6 · 골무 8 · 바구니 5). Node tiers T1–T4; a tier opens after 10 levels bought in the tier below, across the branches ('{n}레벨 더 배우면 다음 칸이 열려요').
- **Timers:** T1 20 min · T2 1 h · T3 3 h · T4 8 h, × 1.5 per level of that node, cap 72 h; the '작업대 속도' node −2% per level (−20%). Timers run on `EffectiveUnix`; a tampered clock pauses them (`pausedSeconds`).
- **Costs:** 단추 (20 × level × tier)분치 + materials per level n: T1 수선 천 15n · T2 리본 15n + 레이스 3n · T3 촛농 15n + 우산살 3n + 기억의 실 n · T4 오르골 태엽 n + 기억의 실 2n (the main sink for surplus commons). A 빠른 수선권 skips 2 h of a running job.
- **Effects:** stat nodes → `ExtraTeamModifiers` (team `StatBlock`); the five '방위 받침' nodes → `BattleModifiers.Slot[pad].RangePercent` (+2% per level, for whoever stands on that pad); basket nodes → `IdleChest.LineRateBonus` / `LineCapBonusHours` (**단추 and material lines only**, rule 7; the system skips the 별조각 line); '원정 재료 +5%' → `Expedition.ZoneLootBonus`; '심부름 입장권 보관 +2' → `ErrandDef.ticketCap + 2`.
- Notifications: research done → planner id 1005 under `ExpeditionDone` (S5-13). Counters `ResearchStart`, `ResearchDone`, `MaterialSpent`.
- **안채 hub** (`ContentArea/HomePanel`, hub 7): a cut-away storybook view (`UI/home_bg`), two room cards (재봉틀 방, 기념품 선반) each with a one-line "next useful thing" hint; future rooms stay hidden. The header's right side reserves a 64 px slot (P2 gramophone).
**API (`WorkbenchSystem : IMetaModule`):** `int Slots`, `void GetNodes(List<WorkbenchNodeStatus> into)`, `bool CanStart(id, out string reason)`, `bool TryStart(id)`, `IReadOnlyList<ResearchJob> Jobs`, `TimeSpan Remaining(job)`, `bool TrySkipWithTicket(job)`, `int Level(id)`, `void AddTeamModifiers(StatBlock t)`, `void AddSlotModifiers(StatBlock[] slots)`, `double BasketRateBonus`, `double BasketCapBonusHours`, `double ExpeditionLootBonus`, `int ErrandTicketBonus`, `event Action Changed`.
**UI (§7.3):** the workbench page: the slot bar on top (3 slots 330×120: '바느질 중: {노드} Lv.{n} · 2:14:05' + [빨리 끝내기] (빠른 수선권), or '빈 자리'), three branch columns 336 wide of node cards 336×150 (icon 64, name Title 26, 'Lv.{n}/{max}', effect Caption 22, cost chips, time), the selected node's [시작] Gold 520×112.
**Art:** `UI/home_bg.png`, `UI/workbench_bg.png` (§9.1); branch icons reuse art.
**Tests (`WorkbenchTests`):** slot unlocks; tier gating; timer formula and cap; speed node; costs; tamper pause; sleep/resume correctness; the basket bonus never changes the 별조각 line (asserted with every node maxed); pad range applies by pad.

### 6.5 R2-12 기념품 선반 (keepsakes) (P1 · L; B2; R2P-2, R2P-1 setup decorator, R2P-4c, R2P-5, R2P-6)
- **32 keepsakes** (Appendix B.5): 7 lineage keepsakes (granted at affinity **Lv6**, SPEC4 CR-10 claim), 8 expedition souvenirs (one per zone: 10% per claimed trip, **guaranteed on the 10th trip** to that zone; route marks add +5% at 3 marks), 4 보관소 keepsakes (floors 20/40/60/80), 12 guest mementos (the guest's week finished at 새것처럼+), 1 attendance keepsake (monthly day 28, S5-21). Every source is deterministic or guaranteed by count.
- **Placed effects only** (critic: no owned effects; Q-05 stays the only always-on collection layer). A placed keepsake applies when its **setup-time** condition holds: stage modifier is / is not X, mode is X, chapter-boss stage (x-10), local night hours 21–05 at setup, a lineage is present, ≥ n units with a trait. Effects are `StatType` entries added by `Battle.SetupDecorators`, or one of three economy effects (expedition reward +%, errand time +5 s, stage-clear 단추 +%).
- **광택 (shine 0–5, shown as shine dots):** each step +20% to the effect (step 5 = ×2). A step comes from a duplicate (a souvenir every further 20 trips; a guest memento when that guest's week ends at 새것처럼+ again) or a deterministic polish: 수선 천 20n + 레이스 조각 5n + 단추 (30n)분치 for step n.
- **Shelf slots:** 2 → 3 at 10 total shine → 4 at 25 → 5 at 45. No rerolls.
- **Pending:** keepsakes earned before the shelf unlocks wait in `pending` and arrive in one burst on unlock ('그동안 모인 기념품 {n}개가 선반에 도착했어요').
- Codex tab [기념품] (`CodexCategory.Keepsake`, 0 별조각 per entry). Counter `KeepsakeGet`.
**API (`KeepsakeSystem : IMetaModule`):** `void GetAll(List<KeepsakeStatus> into)`, `IReadOnlyList<string> Placed`, `int SlotCount`, `bool TryPlace(id, slot)`, `bool TryRemove(slot)`, `bool CanPolish(id, out cost)`, `bool TryPolish(id)`, `void Decorate(BattleSetup setup, BattleMode mode)`, `double ExpeditionRewardBonus`, `float ErrandTimeBonus`, `double StageGoldBonus`, `event Action Changed`.
**UI (§7.3):** the shelf page: 5 shelf slots on top (`UI/shelf_bg`, slots 180×180; tap-to-place or drag; locked slots show '광택 {n}에 열려요'); the collection grid below (5 columns, cells 180×212: icon 140, name Label 22, shine dots; unfound = silhouette + hint, e.g. '별빛 세탁소에 열 번 다녀오면 꼭 찾아요'); a detail card 980 wide: effect text with its condition, '광택 ●●●○○', [올려 두기] / [내리기], [닦기] with cost chips.
**Art:** 16 launch keepsake icons (7 lineage + 8 souvenirs + attendance; 512², transparent, max), `UI/shelf_bg.png`; the other 16 use fallbacks (the source's art cropped: guest portrait, tower bg, zone banner) until a top-up (§9.1).
**Audio:** `keepsake_get`, `keepsake_place`, `keepsake_polish`.
**Tests (`KeepsakeTests`):** each source grants once; the 10th-trip guarantee; shine steps and ×2 at 5; slot thresholds; pending burst; every condition type at setup; economy effects; a placed keepsake never touches the 별조각 basket line.

### 6.6 R2-13 수선 축제 주간 + R2-14 보름달 밤 (P1 · M + S; B1; R2P-2 `EventCalendarSystem`, R2P-4c hub strip, R2P-5, R2P-6)
**Festival week (`EventKind.FestivalRounds`, `EventScheduleType.MonthRotation`):**
- Days 15–21 of each month (DayKey date); days 1–14 stay SPEC4's 별이 머문 밤 산책; days 22–end stay quiet (critic: one festival a month).
- Theme = `(year × 12 + month) mod 5`: A '실 엮는 주' · B '친구 찾는 주' · C '불빛 주간' · D '원정 주간' · E '나들이 주간' (Appendix B.6 point sources and round targets). Points = Σ over the theme's counters of (lifetime − baseline at instance start) × points / per (SPEC4's baseline model).
- **Rounds:** 4 rounds × 5 milestones at 20/40/60/80/100% of the round target (one round ≈ 25% of an engaged normal week for that theme, so all four finish by about day 5). Milestones: 별빛 사탕 10/12/14/16/20 (instant, RewardFlyer). Each round: 별빛 사탕 60 + 별조각 15. All four rounds: 선택 기억편 15 + the theme title (first time per theme). ≈ 15 별조각 and ≈ 3.5 선택 기억편 per week averaged over the month.
- UI: in SPEC4 `EventHubPopup`, a **rounds strip** above the missions list: 4 medallions 120×120 (`UiArt.MedallionDark`, round number, done = Gold stitch), the current round bar 600×20 with 5 pips, '{k}/{target}', [받기] Gold pill when a milestone is claimable. The start card is SPEC4's `EventStartCard` with the theme banner. Dot: SPEC4 `top.event.claim`.
- Banners: 5 theme banners (`UI/festival_{a..e}.png`, 21:9 → 1024×384; fallback `shop_event_banner`).

**Moon nights (`EventKind.MoonNight`, `EventScheduleType.MoonPhase`):**
- Moon age = `(days since 2000-01-06 18:14 UTC) mod 29.530588853`; the full moon ≈ age 14.77. **보름달 밤** = the DayKey containing the full-moon instant ±1 (3 DayKeys, 05:00 boundaries, tamper-checked).
- Effects: the FullMoon modifier replaces ≈ 50% of the rotating modifier slots (`Battle.ModifierOverride`: stages whose rotation modifier is not None/Festival and `hash(stage) % 2 == 0`); 별빛 사탕 ×1.5 on stage clears (×2 on the exact full-moon DayKey) **only on moon DayKeys outside days 1–14** (inside, SPEC4's ×2 stays; never ×4); basket +10% on the **단추 and material lines** during moon DayKeys (`LineRateBonus`).
- UI: a moon chip in the hub's effects strip: '보름달까지 D-{n}' / '오늘은 보름달 밤이에요 · 별빛 사탕 ×1.5' (icon `Icons/moon_phase`, rotated/clipped by phase in code: one image).
**Tests (`FestivalRoundsTests`, `MoonPhaseTests`):** the festival starts and ends on DayKeys 15/22 at 05:00 in UTC and +09:00; theme rotation; baseline once per instance; milestones and rounds claim once; the candy multiplier never stacks with the starlit walk; moon dates match a published table for 2026-11 … 2028-12 within ±1 day; tamper blocks claims.

### 6.7 R2-15 반딧불 등롱 (lantern + night moth, on SPEC4 NEW-03) (P1 · S; B1; R2P-2, R2P-1 moth wave hook and variants, R2P-4c chip, R2P-5, R2P-6)
- SPEC4 NEW-03's firefly rules, cap (6/DayKey) and reward table are unchanged; R2-15 adds the lantern, the moth and the variants.
- **Lantern (kill gauge):** main-stage kills fill it; full = 300 kills + 20 per chapter of `highestStage`, cap 500 (≈ 8–10 min of active play). A caught firefly adds 10%. Offline: credited basket hours × the last-played average kills/minute × 50%. Stores up to 3 full lanterns; at most **6 opens per DayKey**.
- **Open → pick 1 of 4 반딧불 쪽지:** 단추 쪽지 (단추 30분치) · 재료 쪽지 (수선 천 6 + 리본 조각 6 + 촛농 방울 3) · 사탕 쪽지 (별빛 사탕 15) · 기억 쪽지 (기억편 1 of an owned lineage in the formation; **offered at most once per DayKey**, otherwise the slot is a second 재료 쪽지). **The 3 unchosen notes gain 1 반짝 tier** (≤ 5; multipliers ×1, ×1.4, ×1.8, ×2.2, ×2.6, ×3; the chosen one resets to 0). The 기억 쪽지 never scales. No 빠른 수선권 note (critic).
- **Night moth:** between 21:00 and 05:00 local, 10% of NEW-03 spawns are a 그림자 나방 (`enemy_moth` tint); a catch doubles the firefly reward (still inside NEW-03's 6/day and ≤ 60 별조각/week rows) and queues 3 extra `enemy_moth` into the next wave (`Battle.QueueBonusEnemies`). AUTO never taps.
- **Variants by modifier (v1.2, cosmetic):** 떠다니는 종이배 (Rain), 바람에 날린 손수건 (Wind), 별똥별 (FullMoon), 비눗방울 (Festival), the firefly otherwise. The first catch of each kind sets `r2.firefly.{kind}` (for P2's hidden record).
- **Chip:** `Battle/MetaHud/LanternChip` 64×64 at screen x 500–564, y 1500–1564 (right of the guide bar, 12 px above the DeckTab; UX-SPEC-IA rail L3 forced the move), MAIN only (`SheetFade 0`): `Icons/lantern_chip` (fallback `Icons/lamp`) + a fill ring (`Image.Filled` radial) + '×{n}' stored (Micro 18); dot `hud.lantern` when ≥ 1 full. Hidden during side battles and FTUE. Tap → `LanternNotesPopup` (`BuildPopup 860×700`: 4 cards 196×420 with icon, reward chip, 반짝 tier dots, [고르기]).
- Notifications: lanterns full → planner id 1006 under `DungeonTickets` ('입장권·등롱') (S5-13). Counters `LanternOpen`, `FireflyTap`.
**Tests (`LanternTests`):** fill per kill and per chapter; cap 3 stored and 6 opens/DayKey; shard note once per DayKey; tier growth and reset; offline credit; moth only at night, doubled reward within NEW-03's cap, +3 moths next wave; variants per modifier; the gem budget row (0).

### 6.8 R2-17 단골 길, R2-18 주말 골목 장터, R2-19 재료 엮기 (P1 · S each; B1; R2P-2, R2P-4c, R2P-5)
**Route marks (R2-17, `RouteMarkSystem`).** Each zone earns 가로등 marks 0–5 at 3/10/25/60/120 claimed trips (`RouteMarkSave.completions`, from `Expedition.ZoneClaimed`). Each mark: +8% loot (gold and drop rolls, `ZoneLootBonus`). 3 marks: the zone's souvenir chance +5% (or a 오르골 태엽 5% roll before keepsakes exist). 5 marks: **단골 길** — auto-resend on claim when the members are free (`AutoResendZone`, toggle on the zone card, default on). UI (expedition zone card, R2P-4c): 5 lamp icons 20 px under the zone name; tooltip '{n}번 더 다녀오면 가로등이 하나 더 켜져요'. The +40% at 5 marks multiplies SPEC4's weekend ×2 and the ×3/×6 tiers; CR-07's material projection includes it (R2P-7).

**Weekend market (R2-18, `MarketSystem`).** On Saturday and Sunday DayKeys, 6 slots seeded by WeekKey from the pool (Appendix C.8), 20–40% under the list price; a **별조각 and 단추 sink**. 선택 기억편 ×5 for **250**, limit 1 per weekend (critic). UI (상점 tab, R2P-4c): a section '주말 골목 장터' 1032×340 above the product grid on weekends (stall banner optional, fallback `UI/shop_event_banner`): 6 product cards 160×300 with the '−30%' `ui_tag_sale` tag, price chip, limit 'n/1', [사기] Gold; a countdown '월요일 새벽 5시까지'.

**Material merge (R2-19, `MaterialMergeSystem`).** A [엮기] tab in `BagPopup`: 수선 천 20 → 레이스 조각 1 · 리본 조각 20 → 우산살 1 · 촛농 방울 15 → 기억의 실 1 (5/day each) · 기억의 실 5 + 단추 60분치 → 오르골 태엽 1 (**1/day**, critic). No 달빛 조각 recipe. **[모두 엮기]** weaves only the surplus above what the currently craftable and next-tier evolution recipes still need (reads `EvolutionSystem` recipes of every form whose parent is unlocked and which is still locked), within the daily limits. Toggle '바구니를 받을 때 자동으로 엮기' (off by default, `MergeSave.autoWeave`). Counter `MaterialMerge` (outputs), `MaterialSpent` (inputs). UI: recipe rows 740×120 (inputs → output chips, '오늘 {k}/5', [엮기] Primary, a ×n stepper), the [모두 엮기] Gold button with a preview line '천 40 → 레이스 2 · 리본 20 → 우산살 1'.
**Tests:** route mark thresholds and bonuses, 단골 길 resend only with free members; market seeding stable per WeekKey, weekend only, limits, price; merge limits, the surplus rule never eats a needed material, auto-weave on claim.

### 6.9 R2-36b 진화 가지 잇기 (step 1) + R2-37 골목 지도 넓히기 (first new place) (P1 · M + art each; B2; R2P-5 content, R2P-1 place rules, R2P-6 art)
**Six new tier-2 forms** (×1.35² through `FormStats.TierGrowth`; deterministic 진화 기억도 recipes; skills from `SkillKit`; Appendix B.7 for recipes and skill concepts):

| Lineage | New form (id) | Parent | Style · role | Skill concept (existing effect types) |
|---|---|---|---|---|
| 똑단이 | 요요 단추 (`ttok_yoyo`) | `ttok_spinner` | Capture · Echo | '요요 끌어오기' — Pull the nearest enemy + Stun 0.4 s |
| 찰칵이 | 마음 열쇠 찰칵이 (`chal_heartkey`) | `chal_master` | Heal · Echo | '마음 열기' — Heal shop 4% + Cleanse allies |
| 몽당이 | 등대 몽당이 (`mong_lighthouse`) | `mong_lantern` | Snipe · Guardian | '등대 불빛' — Lane damage + Reveal |
| 우두커니 | 천둥 우산 우두커니 (`udu_thunder`) | `udu_storm` | Assault · Echo | '우르릉 내려치기' — AoE Damage + Slow (Rain: +30%) |
| 루즈리 | 콤팩트 거울 루즈리 (`rouge_compact`) | `rouge_gloss` | Defense · Guardian | '거울 반사' — Shield self 20% + Mark the attacker |
| 라라벨 | 회전목마 라라벨 (`lala_carousel`) | `lala_waltz` | Capture · Echo | '빙글빙글 회전목마' — Stun all in range 0.6 s + AttackRamp |
- Recipes need a broken cap (`LineageLevel 110`) and sink 오르골 태엽 / 달빛 조각 (Appendix B.7). Codex +6 forms (tier-2 50 each = +300 one-time). Q-05's form tiers stay 5/10/15/20/25/29 (more forms make them easier).
- ~~The forms raise the ceiling the sim found at ≈ chapter 55 (weeks 7–10).~~ (CR4-07) They are collection content, not the ceiling raise.
  - Each form is ×1.35 over its tier-1 parent in one of its lineage's two slots, so a full team gains at most ≈ +19 % attack, worth ≈ 5–15 stages at the ×1.02 late slope.
  - A launched player has them from day 0.
  - R2P-7 reports the measured move. The ≈ch55 ceiling is PHASE4-ROADMAP Q16 (post-launch cadence).
- (CR4-21) The six skills use existing `vfxId`s, so the AUDIO-SPEC per-vfx cues and the FX-JUICE style rules apply; a new vfx needs its own audio key. Their icons default to the closest `Icons/skill_*` (six new icons are optional, +30 credits).
- Art: 6 forms × 5 credits (base form + the tier-1 parent as references; udu/lala referenced as they are). Fallback: the parent's sprite with a +8% scale and a lineage-colour outline (dev only — shipping a fallback form needs the art).
- Step 2 (one new lineage every 4–6 weeks) is post-launch content cadence and not in this spec.

**First new place (R2-37).** `PlaceDef` rows; `PlaceRules.For(chapter)` returns the place of a chapter (a place lasts until the next place's `fromChapter`; the last defined place continues). Place 1 = chapters 1–5 (today's 골목, unchanged). **Place 2 '야시장 골목'** from chapter 6:
- field `Art/Battle/field_market_tall.png` (an image edit of `field_tall.png`, 1080×2048, max@2k; the plaza ring must match the explicit ring constants of UX-SPEC-IA §1.4 within 2 px — QA overlays the ellipse from the constants; if it drifts, R2P-6 composites the original plaza disc (r ≤ 1.75 ring units) onto the new art);
- two enemy families from R2-01's art: **그림자 까치** (`enemy_magpie_alley`: fast, low HP, Fleeing with `Approach` — one hop to an adjacent lane halfway) and **빨래 바구니 둥지** (`enemy_nest_alley`: Spawner at r 2.1 in a lane mouth, summons `enemy_dust` every 4 s, ≤ 4 alive, at most one per wave);
- a boss **'흔들 종의 그림자'** (`boss_bell`; shadows cast by a forgotten market hand bell — IP-safe: never one of our friends, never the weekly guest): every 15 s it rings and hastes all enemies for 3 s; a placed 소리-trait guardian halves the haste. `BossHintDef`: '종이 울리면 적들이 빨라져요. 소리 친구가 종소리를 덮어 줘요'. Place-2 boss pool = [`boss_bell`, `boss_mothqueen`].
- place rule **'야시장'** (WaveSizeScale 1.2). ~~shown as one chip on the StagePanel~~ (CR4-12) The StagePanel (330×134, M11) has no free slot, and the SideHud covers it in side battles. So the place sign is a second 40 px icon inside the SynergyRow's **modifier chip**, with the rule in its tooltip ('야시장 골목 · 적이 조금 더 몰려와요'); with no modifier the chip shows the place sign alone. The SynergyRow ≤ 540 px check stays (capture 87);
- its ambience (CR4-20): `ambKey = "amb_market"`. R2P-1 raises `AppSignals.RaisePlace(id, ambKey)` when a main stage of another place starts, and the AUDIO workflow swaps the base bed (modifier layers stay on top);
- the chapter map (SPEC4 CR-05) shows '{n}장 · 야시장 골목' for chapters without a story row.
- `BattleSystem.SetupDecorators` applies the place rule; `BattleSimulator.BuildSpawnPools` adds `extraEnemies` and the boss pool for stages in the place. `BattleArt.Load` picks the place field (same 1080×2048 constants; any other size falls back to `field_tall`). (CR4-22) Each place also ships a 1080×810 crop of its field (rows 619–1428, where `field.png` sits inside `field_tall`; PIL, 0 credits). The formation mini-field (4:3) and the `field.png` fallback use the crop, so they match the place.
- Codex +4 enemies (+40 one-time).
**Tests (`NewFormsContentTests`, `PlaceTests`):** the six forms validate (recipe, skill, codex), a recipe needs a broken cap; `PlaceRules.For` per chapter; the place rule applies only in its chapters; the boss chime haste and the 소리 halving; the art-size guard.

### 6.10 R2-05b 스킬 각성 (resonance riders) and errand 3 (P1 · M + S; B2 / B1; R2P-1 sim, R2P-4c lines, R2P-5 rows)
**Riders at 공명 3/6/9** (21; no RNG; derived from resonance, no save):

| Lineage | 공명 3 | 공명 6 | 공명 9 |
|---|---|---|---|
| 짝짝이 | 가족 부르기 양말 토큰 +1 | 토큰 지속 +2초 | 토큰이 풀릴 때 털실 폭발 (공격 40%, 범위) |
| 똑단이 | 보호막 +20% | 기절 +0.2초 | 웨이브마다 수선점에 처음 닿는 적을 끌어당김 |
| 찰칵이 | 표식 +1 중첩 | 표식 대상 치명 +5% | 표식된 보스가 받는 피해 +15% |
| 몽당이 | 촛농 웅덩이 +25% | 드러냄 반경 +20% | 잔향이로 사라질 때 수선점 3% 회복 |
| 우두커니 | 비 보너스 +10%p | 비 아닌 날 '기다림': 10초마다 공격 +2% (최대 10중첩) | 옆 지킴이에게 보호막 10% |
| 루즈리 | 버프 +5%p | 버프 지속 +1.5초 | 세 번째 시전마다 표식 |
| 라라벨 | 마지막 음 범위 +30% | 회복 +15% | '여섯째 음의 메아리': 전투마다 한 번, 수선점 30% 아래에서 회복 20% + 모든 적 기절 1초 |
- Data: `SkillRiderDef { lineageId; resonance; kind (ValuePercent, DurationAdd, CountAdd, ExtraEffect); value; extra }` applied to the form skills of that lineage at setup (`BattleSimulator` reads the rider list from `UnitSetup`). The content package verifies every rider against the real `SkillDefs` and the synergy vocabulary (R2P-5) and adjusts a rider's wording only, never its kind, if a skill lacks the targeted number.
- **Sim additions (R2P-1):** `SkillTrigger.OnWaveFirstReach` (똑단이 9), `SkillTrigger.EveryNthCast` (루즈리 9), a stacking timed buff with a modifier condition (우두커니 6: `AttackRamp` with "not Rain"), the token-dissolve burst (짝짝이 9: `OnDissolve` for tokens). 몽당이 9 (`OnDissolve` + shop heal) and 라라벨 9 (`OnShopLowHp` + Heal + Stun all) use existing triggers.
- **Tone (critic):** the last note is never spent as a stat; 라라벨's rider is '여섯째 음의 메아리'. `atk_musicbox` never plays the 7th degree (AUDIO-SPEC rule).
- **Option (v1.2, B-opt):** at 공명 9 only, a free switch between the listed rider (A) and a flaw-rooted habit (B) — 7 B riders (Appendix B.8); switching is free outside battle.
- UI (`FormInfoPopup`, R2P-4c): three rider lines with lock lines at 공명 3/6/9 ('공명 {n}에 깨어나요'), the active ones Gold; with the option, a two-chip toggle on the 공명 9 line.
- Perf: re-run the SPEC4 perf capture with all riders active.
**Errand 3 '다시 뭉치는 먼지 왕'** (B1; `errand_dustking`, requiredStage 17): a crowned 먼지 king (`boss_dustking`, Reforming +10% HP/damage per re-form) for 60 s; score = total damage; 10 tiers at `t/10 × 4 × bossHp(s(L))`; tier t pays 우산살 ⌈t/2⌉ · 레이스 조각 ⌈t/2⌉ · 기억의 실 ⌊t/4⌋, and t = 10 with L ≥ 10 adds 오르골 태엽 1; tier ≥ 7 opens L+1 (`ErrandClear`); favour 지원·힐 (heal and shield +50%, support attack +25%); milestone 별조각 10 + 기억의 실 3. Sweeps pay the level's `bestTier`. Audio (CR4-21): `dustking_reform` on each re-form (`ObjectivePhase`), `jingle_errand_clear` on a tier ≥ 7 result.
**Tests (`RiderTests`, `ErrandDustKingTests`):** each rider's effect in a scripted battle; riders inactive below their resonance; the option switch; Reforming growth per re-form; tier thresholds; the sweep tier.

---

## 7. UI per screen in the new frame (consolidated anatomy)
Coordinates are 1080×1920 reference px, screen y from the top (UX-SPEC-IA §1.1). Sheet-panel coordinates are panel-local from the top of the 780 ContentArea. Tokens are UX-SPEC §1 (`UIType`: Display 56, H1 40, H2 32, Title 28, Body 26, Label 24, Caption 22, Hint 20, Micro 18; buttons Gold 520×112, Primary, Secondary, Ghost; `MetaRewardChip`; `MetaSwitch`; M53 Option segments).

### 7.1 Frame
- **MAIN:** rails L1 이벤트 · **L2 안채 (B2)** · L3 출석부 · R1 SPEC4 StatsButton · **R2 밤 나들이** · R3 수선 일지 (UX-SPEC-IA §3.3 slots; both rails full). Lantern chip (B1) at 500–564 × 1500–1564. Everything else as UX-SPEC-IA §3.
- **SHEET (hub):** `ContentArea` 970–1750 holds `OutingsPanel` (hub 6) or `HomePanel` (hub 7); the header (914–970) with the handle and the return chip is the shared `SheetHeader`; the battle pans up 360 px (16:9) like any sheet; no side tab is highlighted; 사냥 shows the SHEET look and closes the hub.
- **Side battle (MAIN):** every live start returns the frame to MAIN first (CR4-10, §2.6). `SideHud` in the StagePanel rect 14–344 × 142–276 (StagePanel α 0); RecommendedPower shows the mode rec (hidden for the guest); `SideBattleBar` in the guide-bar rect 16–484 × 1492–1576 (GuideQuestBar α 0); rails, basket widget, x1/AUTO, deck and lamp unchanged; boss challenge button, lantern chip and fireflies hidden; centre label '사냥으로'; Android back → the abandon confirm once no popup or sheet is open (CR4-11). Guest fight only: `GuestDoorView` at the shop.
- **≡ 메뉴 (CR4-09):** 수선 일지 · 출석부 · 이벤트 · SPEC4's tiles (UX-SPEC-IA §8.8) · 밤 나들이 · 수선점 안채 (B2) · 가방 · 공지 · 설정. Gated tiles hide and the grid reflows; rows = ⌈tiles/3⌉, `BuildPopup(900, 300 + 220·rows)`.

### 7.2 `OutingsPanel` (밤 나들이 sheet, R2P-4a; pages for B by R2P-4b)
| y | Element |
|---|---|
| 0–88 | `PanelHeader`: title '밤 나들이' 34 bold Cream at x 24 (x 96 on sub-pages, with `PageBack` ‹ 88×88 at TopLeft(4,0) and the page title); hub only: `RoundsButton` Primary 300×72 '오늘의 순회 ▶' at TopRight(−24,−8) and `RoundsGear` 64×64 at TopRight(−336,−12) (DailyRounds unlocked; NEW pill until the first run) |
| 88–136 | `RumourLine` (WeeklyGuest unlocked): '이번 주 소문 · ' + 2 trait chips (icon 40 + name Label 22) + ' 친구들이 힘을 내요' (Caption 22 SubText); tap → tooltip '심부름 · 보관소 · 등불 길에서 이 특성 친구는 피해 +15%예요' |
| 136–780 | `CardList` ScrollRect (from 96 without the rumour line): cards 1032×148, spacing 12, padding 12 |
**`ModeCardView`** (1032×148, `UiArt.PanelLight9s`@0.75): banner 280×124 at MiddleLeft(12,0) (rounded mask; errands `UI/zone_exp_1_3`, guest = this week's portrait, dungeon `UI/dungeon_banner`, tower `UI/tower_bg`, walk `UI/walk_strip`), title Title 28 bold Cream at (308,−20), status Label 24 SubText at (308,−60), detail Caption 22 at (308,−98), pill `PillNight9s` 150×40 at TopRight(−20,−20), chevron 28 at MiddleRight(−24,0), NEW pill at the banner's top-left, dot at TopRight(−8,−8). Locked: α 0.55, 28 px lock, the M39 sentence as detail. Card order: 이번 주의 손님 → 밤 골목 심부름 → 잔향이 던전 (→ `GoToTab(Expedition)` + `FocusDungeon`) → 보관소 오르기 (B1) → 등불 길 (B2). Only modes present in the build appear.
**Pages** (`OutingsPanel/Pages/*`, Stretch(0,0,88,0), 0–692; 0.12 s cross-fade):

| Page | Layout (page-local y) |
|---|---|
| `ErrandListPage` | 2-column grid of tiles 510×300 (padding 24, spacing 12): banner 510×170, name Title 28, '입장권 {n}/6' pill on the banner, '최고 Lv.{n}' Label 24, favour chip (style icon 32 + Caption 22), NEW pill, dot; locked tile α 0.55 + '스테이지 2-4를 클리어하면 열려요' |
| `ErrandPage` | 0–200 banner 1032×200 (name H2 32, desc Caption 22 ×2, tickets pill '입장권 {n}/6 · 새벽 5시에 +2장' at BottomRight) · 212–300 level stepper ‹ 'Lv.{L}' › (88-hit arrows) + '권장 {rec} · 내 {power}' Label 24 + rating chip · 308–364 chips (favour 260×48, rumour 260×48) · 372–468 'Lv.{L} 선물' + reward chips 72 · 476–540 맞춤 편성 row · 568–680 [다녀오기] Gold 500×112 + [소탕] Secondary 300×112 (Unavailable '입장권이 없어요' at 0 tickets) |
| `GuestPage` | 0–320 portrait card 300×300 (PanelInset9s + portrait + shell rings faded by this week's band) and the right column: name H2 32 · '월요일 새벽 5시까지 {d}일 {h}시간' Caption 22 · trait chips 2 × 200×48 '{특성} · 피해 +30%' · '모두 함께 +{n}%' Gold pill 220×48 · '지난번 최고 {n}단계' Label 24 · '오늘 {k}/3 · 보관 {b}' Label 24 · 330–440 meter: '{등급} {n} · 다음 단계까지 바늘땀 {k}' Title 28, bar 984×24 with 25 pips (band separators), breakdown Caption 22 · 448–500 '이번 주 지금 받을 선물' + chips 56 · 508–556 맞춤 편성 row · 568–680 [그림자 떼어 내기] Gold 520×112 (sub-label '오늘 {k}/3') + '손님 도감 ›' Ghost (Codex Guest tab) |
| `TowerPage` (B1) | `UI/tower_bg` cover · 0–56 '보관소 {h}층 · 감사장 +{p}% · 다음 상자까지 {n}층' · 64–484 five rows 1032×84 (current ±2; label, shelf name, modifier icon 48, rule chip, rec + rating, reward chips 48; current row Gold outline) · 492–548 '자동 재도전' switch (+ 맞춤 편성 on trial floors) · 568–680 [도전] Gold 500×112 + [자동으로 오르기] Secondary 300×112 |
| `WalkPage` (B2) | 0–300 street strip with 15 lamp nodes · 308–364 next two nodes + depth picker ‹ 깊이 {d} › + rule chip · 372–436 charm bar (icons 56 + tier pips) · 444–500 crest pips (7 traits × 3) · 508–556 lanterns '{n}/3 · 다음 등불 {h:mm} · 보관 {b}' + 맞춤 편성 switch · 568–680 [걷기]/[이어 걷기] Gold 520×112 + [등불 나무] Secondary 300×112 |

### 7.3 `HomePanel` (수선점 안채 sheet, B2, R2P-4c)
- 0–88 `PanelHeader` '수선점 안채' (+ a reserved 64 px slot at TopRight for P2's gramophone).
- Hub page 88–780: `UI/home_bg` cut-away 1032×420 at (24,−96) with two glowing room hotspots; room cards 2 × 510×200 at y 528: 재봉틀 방 ('바느질 중 {n}/{slots}' / '빈 자리 {k}') and 기념품 선반 ('올려 둔 기념품 {n}/{slots}'), each with its hint line (Caption 22) and dot.
- `WorkbenchPage`: slot bar 0–120 (3 × 330×120), branch columns 128–560 (3 × 336, node cards 336×150 in a ScrollRect), 568–680 [시작] Gold 520×112 + cost chips.
- `KeepsakePage`: shelf 0–220 (`UI/shelf_bg`, 5 slots 180×180), collection grid 228–600 (5 columns, cells 180×212), detail card 608–692 (effect line, shine dots, [올려 두기]/[내리기], [닦기]).

### 7.4 Existing screens changed
| Screen (owner) | Change |
|---|---|
| 강화 sheet `GrowthPanel` (R2P-4a) | [수선점][수선 비법] segment in the header; `TechniquePage` overlay (§5.2) |
| `CharacterDetailPopup` (R2P-4a) | [한 땀 돌파] / [추억 수놓기] in the level-up button rect; 5 mark slots under the level line; costume fitting room (§5.3, §5.7) |
| `CharacterCardView`, `EvolutionTreeView` header (R2P-4a) | stitch marks 16 px |
| `CostumeListView`/`CostumeItemView` (R2P-4a) | tap = preview; [입히기]/[되돌리기]; the locked line |
| `DailyGiftCardView` (R2P-4a) | the kept gift (second chip, '어제 선물도 남아 있어요', [모두 받기]) |
| `ReturnPopup` (R2P-4a; B2 partner line by R2P-4c) | 놓친 선물 line + chips; the 대표 친구's opening line |
| `IdleChestWidgetView` (R2P-4a) | free tag '무료 2' when a free quick idle is banked |
| `MenuPopup` (R2P-4a) | two tiles after SPEC4's tiles, before 가방 · 공지 · 설정; rows = ⌈tiles/3⌉ (CR4-09) |
| Q-02 journal rows, Q-04 records (content R2P-5) | new mission and record rows render through SPEC4's `MissionRowView`/`JournalRowModel` |
| `StrengthenPopup` (SPEC4 sheet; rows by R2P-3) | Technique and CapBreak rows (B1 TrialRule) |
| `TopBarView` LevelText (B1, R2P-4c) | button → `ProfilePopup`; '{이름} Lv.{n}'; naming bubble; NEW pill for a new title |
| 상점 tab (B1, R2P-4c) | weekend market section on Sat–Sun |
| `BagPopup` (B1, R2P-4c) | [엮기] tab |
| 원정 zone card (B1, R2P-4c) | 5 lamp-post marks + 단골 길 toggle |
| `EventHubPopup` (B1, R2P-4c) | festival rounds strip; moon chip in the effects strip |
| `CodexPopup` (A guest tab; B2 keepsake tab; R2P-4a/4c) | 8 tabs × 110 |
| `FormInfoPopup` (B2, R2P-4c) | rider lines at 공명 3/6/9 |
| `SleepModeView` status card (B1, R2P-4c) | the 대표 친구 dozing ('zzz') in place of the moon icon; no sleep during side battles (A, R2P-4a) |
| `GuideQuestBarView` (A, R2P-4a) | fades out during side battles |

### 7.5 Battle field (R2P-1 unless noted)
| Element | Rect / anchor | Rule |
|---|---|---|
| `Battle/SideHud` | StagePanel rect | side battles only |
| `MetaHud/SideBattleBar` (R2P-4a) | guide-bar rect | side battles only; [결과 바로 보기] after 5 s |
| `GuestDoorView` | field-local, behind the shop at the door side | GuestShadow only |
| `ShopView/TitlePlate` (B1) | 220×44 under the shop sprite | equipped title; MAIN and SHEET |
| `MetaHud/LanternChip` (B1, R2P-4c) | 500–564 × 1500–1564 | MAIN only, `SheetFade 0` |
| place field and enemies (B2) | `BattleArt.Load` per chapter | same ring constants |

### 7.6 Popups
| Popup | Size | Owner · wave |
|---|---|---|
| `GuestRevealPopup` (queue kind) | 900×1300 | R2P-4a · A |
| `SideBattleResultPopup` | 860×980 | R2P-4a · A |
| `ModeIntroPopup` | 840×620 | R2P-4a · A |
| `ErrandSweepPopup` | 820×700 | R2P-4a · A |
| `RoundsPopup` / `RoundsSettingsPopup` | 900×1300 / 820×1000 | R2P-4a · A |
| `ProfilePopup` / `TitlePickerPopup` | 820×1100 / 820×1300 | R2P-4c · B1 |
| `LanternNotesPopup` | 860×700 | R2P-4c · B1 |
| charm pick (SPEC4 `BlessingPickPopup` or own) / `MerchantPopup` / `WalkTalentPopup` | 980×920 / 820×900 / 820×1100 | R2P-4b · B2 |
All use `UIKit.BuildPopup`, the M48 dim, M49 back, and FTUE suppression; text ≥ 18 (UX-SPEC lint `UiSceneLintTests` runs before every UI handoff).

---

## 8. Work packages and file ownership

### 8.1 Waves
```
(SPEC4 4.1 merged + 4.2 green; UX wave 2 and WP-IA accepted)
Wave R2-0 ─┬─ R2P-0 Contracts (sequential, alone in its files; ≈ 2 agent-days)
           ├─ R2P-C Curve package (file-disjoint; ≈ 1 d) — skip the parts SPEC4's P4-9 already landed
           └─ R2P-6 art may start (Art/** only) when §9.1's balance rule allows; audio briefs go to the AUDIO workflow
           │ merge → compile check → verify_proj BatchBuildAll → R2P-7 projection run #1 (curve package, R2 off: a stall before ch16)
Wave R2-A ─┬─ R2P-1 Battle      (side host, scripts, Fleeing/Spawner, phases, SideHud, GuestDoorView, BattleView rebind) ≈ 5 d
           ├─ R2P-2 Meta        (errands 1–2, guest, rounds, banks, 놓친 선물, kept gift, planner fold) ≈ 6 d
           ├─ R2P-3 Growth      (techniques, cap breaks + embroidery, BuildBiased, advisor rows, Reward unlock type) ≈ 3 d
           ├─ R2P-4a UI         (hub sheet + pages, popups, rails/menu, 강화 segment, detail popup, fitting room, lines) ≈ 6 d
           ├─ R2P-5 Content     (rows, 6 costumes, missions/records, pacing table, copy) ≈ 3 d
           └─ R2P-6 Art/audio   (wave-A list) ≈ 2 d
           │ merge: R2P-5 → R2P-3 → R2P-2 → R2P-1 → R2P-4a, then R2P-7 verify A (tests, captures, perf, projection #2) ≈ 2 d
Wave R2-B1 ─ tower + rule effects, titles + profile, errand 3, lantern, route marks, market, merge, festival + moon ≈ 18 d
           │ R2P-7 verify B1
Wave R2-B2 ─ 등불 길, 안채 + workbench + keepsakes, 6 new forms, first new place, skill riders ≈ 24 d (needs the art top-up answer)
           │ R2P-7 verify B2 (projection #3 with 12 seeds)
```
Wave A ≈ 26 agent-days (IDLE_RESEARCH2 estimated 20–26), B1 ≈ 18, B2 ≈ 24. Wave B starts only after wave A's done-when list (§8.4) is green. UI packages start on R2P-0's inert stubs (the SPEC3/SPEC4 pattern).

### 8.2 File ownership (one owner per file per wave)
| Package | Files |
|---|---|
| **R2P-0** (then handed on) | `Scripts/Core/{GameEnums, SaveData.Meta, SaveData.Characters, PlayerData, Game, AppSignals}.cs`, `Scripts/Data/MetaDatabase.cs`, `Scripts/Systems/Meta/{MetaSession, PopupQueue, MetaTypes, ProgressTracker, FeatureGateSystem, DailyClock, IMetaModule}.cs` (+ new `IDayRolloverModule`, `ISideBattleModule` in IMetaModule.cs), `Scripts/Systems/Meta/RedDotSystem.cs` (`RedDotPaths` constants only), `Scripts/UI/UINav.cs`, `Scripts/UI/UIFrame.cs` (doc), `Scripts/UI/Main/MainTabController.cs` (§2.6 shell exception), `Systems/Battle/BattleTypes.cs` (declarations) → R2P-1; stubs of every new system → R2P-2/R2P-3; `Tests/EditMode/{SystemsTestBase, MetaSessionTests}.cs` + new `{R2SaveMigrationTests, FeaturePacingTests, PopupOrderTests}.cs`. **Hook members only, then handed back:** `BattleSystem` (→ R2P-1), `CharacterSystem`, `FormationSystem`, `MetaRewardService`, `StrengthenAdvisor` (→ R2P-3), `IdleChestSystem`, `MissionSystem`, `DailyGiftSystem`, `ExpeditionSystem`, `EventCalendarSystem`, `FireflyRules`, `AffinitySystem`, `NotificationPlanner` (→ R2P-2) |
| **R2P-C** | `Scripts/Data/BattleConfig.cs` (taper only → R2P-1), `Scripts/Data/GameConfig.cs` + `Data/GameConfig.asset`, `Editor/Content/MetaContent.IdleChest.cs` (→ R2P-5), `Scripts/Systems/Meta/PowerSystem.cs` (cap + rec → R2P-3), `Editor/PowerCalibration.cs` (→ R2P-5); new `Tests/EditMode/{CurvePackageTests, PowerRatingTests}.cs` |
| **R2P-1** | `Scripts/Systems/Battle/{BattleSystem, BattleSimulator*, BattleTypes}.cs` + new `SideBattleRunner.cs`, `Scripts/Data/BattleConfig.cs`, `Editor/Builders/{BattleSceneBuilder, EnemyFxBuilder}.cs`, `Scripts/UI/Battle/**` (incl. new `{SideHudView, GuestDoorView}.cs`; B1 `ShopView` title plate and `StrayFireflyView` variants; B2 bell FX), new `Scripts/Systems/Meta/PlaceRules.cs` (B2); tests new `{SideBattleHost, SideScript, RuleTable, Place, Rider}Tests.cs`, existing `{BattleSimulator, BattleSystem}Tests.cs` |
| **R2P-2** | new `Scripts/Systems/Meta/{ErrandSystem, WeeklyGuestSystem, DailyRoundsSystem}.cs` (A), `{TowerSystem, TitleSystem, ProfileSystem, LanternSystem, RouteMarkSystem, MarketSystem, MaterialMergeSystem}.cs` (B1), `{NightWalkSystem, WorkbenchSystem, KeepsakeSystem}.cs` (B2); existing `Scripts/Systems/Meta/{IdleChestSystem, MissionSystem, DailyGiftSystem, EventCalendarSystem, FireflyRules, AffinitySystem, NotificationPlanner}.cs`, `Scripts/Systems/Battle/{ExpeditionSystem, DungeonSystem}.cs` (NEW-04 fallback only); tests new `{Errand, WeeklyGuest, Rounds, Banking, MissedRewards, Tower, Title, Profile, Lantern, RouteMark, Market, Merge, FestivalRounds, MoonPhase, NightWalk, Workbench, Keepsake, ErrandDustKing}Tests.cs`, existing `{IdleChest, Mission, DailyGift, EventCalendar, NotificationPlanner}Tests.cs` |
| **R2P-3** | new `Scripts/Systems/Meta/TechniqueSystem.cs`; `Scripts/Systems/Characters/{CharacterSystem, CostumeSystem, EvolutionSystem}.cs`, `Scripts/Systems/Battle/FormationSystem.cs`, `Scripts/Systems/Meta/{PowerSystem, StrengthenAdvisor, MetaRewardService}.cs`; tests new `{Technique, CapBreak, ModeFormation, CostumeR2}Tests.cs`, existing `{CharacterSystem, CostumeSystem, Power, StrengthenAdvisor, MetaReward}Tests.cs` |
| **R2P-4a** | new `Editor/Builders/OutingsSceneBuilder.cs`; `Editor/Builders/{MetaSceneBuilder, CharacterSceneBuilder, FrameSceneBuilder}.cs`; new `Scripts/UI/Meta/Outings/{OutingsPanelView, ModeCardView, ErrandListPage, ErrandPageView, ErrandSweepPopup, GuestPageView, GuestRevealPopup, RoundsPopup, RoundsSettingsPopup, ModeIntroPopup, SideBattleResultPopup, SideBattleBarView, ModeFormationRow}.cs`, new `Scripts/UI/Main/TechniquePageView.cs`; existing `Scripts/UI/Meta/{MetaNavigator, MetaPopupPresenter, QuickIconView, ReturnPopup, IdleChestWidgetView, GuideQuestBarView, SleepModeView, CodexPopup}.cs`, `Scripts/UI/Main/{GrowthPanel, MenuPopup}.cs`, `Scripts/UI/Characters/{CharacterDetailPopup, CostumeListView, CostumeItemView, CharacterCardView, EvolutionTreeView}.cs`, `Scripts/UI/Shop/DailyGiftCardView.cs` |
| **R2P-4b** (B) | `Editor/Builders/OutingsSceneBuilder.cs` (from 4a); new `Scripts/UI/Meta/Outings/{TowerPageView, WalkPageView, WalkTalentPopup, MerchantPopup}.cs` (+ `CharmPickPopup.cs` only if SPEC4 PRG-07 was cut) |
| **R2P-4c** (B) | new `Editor/Builders/HomeSceneBuilder.cs`; `Editor/Builders/{MetaSceneBuilder, CharacterSceneBuilder, FrameSceneBuilder}.cs` (from 4a: lantern chip, HomeButton, rider lines, LevelText button); new `Scripts/UI/Meta/Home/{HomePanelView, WorkbenchPageView, KeepsakePageView}.cs`, new `Scripts/UI/Meta/{ProfilePopup, TitlePickerPopup, LanternChipView, LanternNotesPopup}.cs`; existing `Scripts/UI/Main/TopBarView.cs`, `Scripts/UI/{Shop, Bag, Expedition}/**`, `Scripts/UI/Meta/{EventHubPopup, CodexPopup (from 4a), ReturnPopup (from 4a), SleepModeView (from 4a)}.cs`, `Scripts/UI/Characters/FormInfoPopup.cs` |
| **R2P-5** | `Editor/Content/**` (new `MetaContent.{R2Features, Errands, Guest, Techniques, CapBreak, Rounds, R2Missions, R2Achievements, SideRules, Tower, Titles, Profile, Lantern, Walk, Workbench, Keepsakes, Festivals, Moon, RouteMarks, Market, Merge, Places, Riders}.cs`; costume and new-form rows in `CharacterContent.*.cs`; skills in `SkillContent.*.cs`; `MetaContent.{Features, Missions, IdleChest, Attendance, Affinity, Achievements}.cs` row edits), `Scripts/Data/ShopDatabase.cs` (event rows), `Editor/{MetaDataBuilder, PowerCalibration}.cs`; new `Tests/EditMode/ContentR2Tests.cs` |
| **R2P-6** | `Assets/_Project/Art/**` (new files only; existing art untouched), `Editor/UiArt.cs` + `ArtLib` (new accessors only), `Editor/ArtImportPostprocessor.cs` (rules for `Art/Guests/`), `scratchpad/phase5/art/**`; the audio key briefs handed to the AUDIO workflow (§9.2) |
| **R2P-7** | `Tests/EditMode/EconomyBudgetTests.cs` (R2 block), `Tests/PlayMode/**` (new R2 tests), `Verify/**` (AutoShot R2 shots, PerfProbe scenarios), `phase4b/sim/**` (harness update), `scratchpad/verify.ps1`, `scratchpad/phase5/**` reports |
Untouched in phase 5 (report needs, do not edit): `UIManager`, `SheetController`, `SheetDragHandle`, `IdleSceneBuilder`, `SheetFade`, `UIKit`, `UITheme`, `UITokens`, `TutorialSystem`, `AttendanceSystem`, `ShopSystem`, `GachaSystem`, `Core/Audio/**` and `UISfx*` (AUDIO workflow), `Editor/Audio/**`. (CR4-11) The side-battle back handler goes through the existing public `UIManager.PushBackHandler`; `UIManager` itself is not edited.

**(CR4-17, CR4-18) Ownership notes from PHASE4-ROADMAP v1.1:**
- **R2P-C is confirm-only.** The curve files (`BattleConfig`, `GameConfig`, `MetaContent.IdleChest`) changed in RM-1 (P4-C), and the rating repair (`PowerSystem`, `PowerCalibration`, `PowerConfig` fields) in RM-2 (P4-0/P4-1/P4-8). R2P-C edits none of them.
- **The pacing core** (`CounterKey.PlayMinutes`, `alsoRequire`, card spacing, `FeaturePacingTests`) is R2P-0's only if Q2 was declined; otherwise P4-0 landed it in RM-1 and R2P-5 adds only the R2 rows.
- **`UI/Expedition/BlessingPickPopup.cs`** belongs to R2P-4b in B2 (the charm pick reuses it); R2P-4c keeps the rest of `UI/Expedition/**`.
- **R2P-7** also updates `AutoShot.MenuTileOrder` when R2P-4a adds the ≡ tiles, and re-bases SPEC4's lifetime rows in `EconomyBudgetTests` (§10.2).
- **R2P-6** inherits P4-10's unfinished SPEC4/ART-UPGRADE rungs in ladder order (PHASE4-ROADMAP delta 38).

### 8.3 Cross-package contracts (provider → consumers)
| Provider | API | Consumers |
|---|---|---|
| R2P-0 | all of §2 (v5 save, enums, defs, module interfaces, Game props, hooks, nav, frame extension, red dots, popup order, pacing) | all |
| R2P-C | `BattleConfig` taper2; economy constants; `PowerSystem` cap + rec segment; calibration | R2P-1, R2P-3, R2P-5, R2P-7 |
| R2P-1 | `BattleSystem.{CanBeginSideBattle, BeginSideBattle, IsSideBattle, ActiveSide, SideTimeLeft, AbandonSideBattle, CanFinishNow, FinishSideBattleNow, IsFastForwarding, ResumeMainAfterSide, SideBattleStarted, SideBattleEnded, BattleRestarted, QueueBonusEnemies}`; `SideBattleRunner`; `SideHudView`, `GuestDoorView`; rule effects (B1); `PlaceRules` (B2) | R2P-2, R2P-4a/b, R2P-7 |
| R2P-2 | `Errands.*`, `Guest.*`, `Rounds.*`, bank behaviour of `IdleChest`/`DailyGift`, `Missions.AutoClaimPeriod`; B: `Tower.*`, `Titles.*`, `Profile.*`, `Lantern.*`, `RouteMarks.*`, `Market.*`, `Merge.*`, `Walk.*`, `Workbench.*`, `Keepsakes.*`, festival/moon on `Events` | R2P-4a/b/c |
| R2P-3 | `Techniques.*`, `Characters.{GetMaxLevel, CanCapBreak, TryCapBreak, CanEmbroider, TryEmbroider, CapBroken}`, `Formation.BuildBiased`, `Power.PreviewTechnique`, advisor rows, `CostumeUnlockType.Reward` handling | R2P-2, R2P-4a |
| R2P-4a | hub sheet views, `SideBattleBarView`, result/intro/reveal/rounds popups, navigator receivers | R2P-4b/c (they add pages to the same panel), R2P-7 |
| R2P-5 | content for every def; `MetaDataBuilder.Validate` R2 rules (unique ids, tiers increasing, rewards within §4.5, every keepsake/title/costume referenced exists, trial rules satisfiable, Korean copy lint) | all tests via `MetaContent.Apply` |
| R2P-6 | art files + `UiArt.{GuestPortrait(id), StitchMark, RailOutings, RailHome, TowerBg, WalkStrip, HomeBg, WorkbenchBg, ShelfBg, FestivalBanner(i), MoonPhase, LanternChip, TitlePlate9s, Keepsake(id)}`; audio keys | R2P-1, R2P-4*, AUDIO workflow |

### 8.4 Package steps, risks, done-when
**R2P-0 Contracts (≈ 2 d, sequential).**
- Steps: enums → save classes + `MigrateToV5` (seeding `PlayMinutes` for saves past FTUE if it lands here, CR4-24) → MetaDatabase defs (incl. `income`, `PlaceDef.ambKey`, `offlineGoldMinutesPerHour`) → module interfaces + stubs → `MetaSession` (lists, PlayMinutes, side forwarding, the income and AutoFormation wiring) → `Game.SetupMeta` → hooks (members only; incl. `IncomePerSecond`, `ClearGoldAt`, `AutoFormationPower`, `AppSignals.PlaceChanged`) → `DailyClock` events → `ProgressTracker.GetPeriodValue` → `FeatureGateSystem` (`alsoRequire`, spacing; only if Q2 was declined) → `PopupQueue` order → `RedDotPaths` → `UINav` + `MainTabController` hub extension (if approved) → `CoachTargets` → `SystemsTestBase`.
- Risks: the shell exception (fallback ready); SPEC4 module order (`MetaSessionTests` stay green); stubs never throw.
- Done when: the full EditMode suite is green on verify_proj, a v4 save (`ux_shots/autoshot_player.json` copy migrated through SPEC4's v4) loads as v5, plays 60 s and re-saves; the game is visibly unchanged except the card spacing.

**R2P-C Curve (≈ 1 d).**
- Steps: BattleConfig fields + `TaperedScale` → asset re-serialize → GameConfig asset YAML + C# default → basket stageGrowth → confirm C-01 is on → PowerSystem cap → rec segment fit → tests.
- Risks: `GameConfig.asset` keeps inspector values (edit the YAML, SPEC4 §7.6); calibration time.
- Done when: `CurvePackageTests`/`PowerRatingTests` green; projection run #1 shows the R2-off stall before chapter 16 and chapters 1–11 within day 1.

**R2P-1 Battle (A ≈ 5 d; B1 ≈ 4 d; B2 ≈ 5 d).**
- A order: `_sideSim` + lifecycle → shared setup split → scripts/objectives/phases → Fleeing → Spawner chain → `SideBattleRunner` + fast-forward → `BattleRestarted` rebind in BattleView → `SideHudView` → `GuestDoorView` → ProgressTracker doc + counters check → `ClearGoldAt(int)` (CR4-05) → tests.
- B1: rule effects + `ExtraModifier` → tower floor scripts → Reforming → `QueueBonusEnemies` → title plate → firefly variants. B2: crest specials → walk fight scripts → slot modifiers → `DoubleHitChancePercent` → rider triggers → place spawns + bell boss → `BattleArt` place field (+ the 4:3 crop) → `AppSignals.RaisePlace` and the place sign in the modifier chip (CR4-12, CR4-20).
- Risks: `BattleView` partials are hot (keep ATK timing contracts); `BattleAudioDirector` must not break on the sim swap (it re-reads `Game.Battle.Sim` after `BattleRestarted`; report to the AUDIO owner); allocation in the scripted spawner.
- Done when: §10 battle tests green; a 20-min dev-player session with 10 side battles logs 0 exceptions; perf scenarios in budget (§10.4).

**R2P-2 Meta (A ≈ 6 d; B1 ≈ 6 d; B2 ≈ 8 d).**
- A order: ErrandSystem (tickets, levels, sweeps, rewards) → WeeklyGuestSystem (week, snapshot, stitches, tiers, rollover grant, reveal, bank) → IdleChest free-quick bank → the night basket's 단추 line in 단추 minutes (`offlineGoldMinutesPerHour`, CR4-05) → DailyGift kept gift → Missions `AutoClaimPeriod` + DailyRounds rollover + summary → DailyRoundsSystem steps → planner fold → tests.
- Risks: day-boundary ordering (`DayChanging` before counters reset); never double-granting at rollover; tamper paths.
- Done when: the deterministic schedule tests pass in UTC and +09:00 FakeClocks; a 3-day jump and a rollback behave per §5.5.

**R2P-3 Growth (A ≈ 3 d; B2 ≈ 1 d).**
- A order: `GetMaxLevel` sweep through every `MaxLevel` use → breaks + embroidery → TechniqueSystem → modifiers + rank multipliers → `PreviewTechnique` → `BuildBiased` → 자동 편성 v2 (`AutoFormationPower`, CR4-06) → advisor rows (the `AutoFormation` row compares offence) → `GoldMinutes` through `IncomePerSecond` (CR4-05) → `CostumeUnlockType.Reward`.
- Risks: a missed `MaxLevel` use (grep `MaxLevel`/`IsMaxLevel`; the PRG-05 clamp); PowerCalibration must be re-run after techniques land (R2P-5).
- Done when: tests green; projection run #2 matches §4.4.

**R2P-4a UI (≈ 6 d).**
- Order: OutingsSceneBuilder (panel, header, card list, pages) → hub registration + rails + menu tiles (CR4-09) → navigator/presenter cases → errand pages + sweep popup → guest page + reveal → side bar + result card + intro strip (`GoToMain()` before live starts, the back handler; CR4-10, CR4-11) → rounds popups → 강화 segment + TechniquePage → detail popup break/embroidery + marks → fitting room → gift/return/widget lines → guide bar/sleep side-battle rules.
- Risks: frozen coach paths (`GrowthPanel/Buttons/LevelUp1Button`, `GrowthPanel/LineageList`, rails); text floor; the 780-high layouts; `ValidateTargetsInScene`.
- Done when: captures 50–63 pass (§10.5) and `UiSceneLintTests` are green.

**R2P-4b / R2P-4c (B1 ≈ 5 d; B2 ≈ 7 d).** Same rules; done when their captures pass.

**R2P-5 Content (A ≈ 3 d; B1 ≈ 3 d; B2 ≈ 5 d).**
- A order: features/pacing table (incl. the SPEC4 AutoChallenge/ExpeditionBatch card change) → errand rows → guest rows + bands → technique rows → break costs → costumes (+ event-shop rows) → missions + records rows → copy (Appendix A) → PowerCalibration re-run.
- Risks: copy volume (≈ 400 lines in A+B); 해요체 lint; reward totals.
- Done when: `MetaDataBuilder.Validate` reports 0 errors and `ContentR2Tests` pass.

**R2P-6 Art and audio (A ≈ 2 d; B ≈ 4 d).** Order and rules in §9. Done when every file in §9.1 exists or is marked "top-up (deferred)" with its fallback verified in a capture, and the audio keys are delivered to the AUDIO workflow with recipes.

**R2P-7 Verify (≈ 2 d per wave).** Order: EconomyBudget R2 block → AutoShot shots → PerfProbe scenarios → harness update + projection run → PlayMode tests → report (`scratchpad/phase5/verify_{wave}.md`). Done when §10 is green and the numbers are logged.

**Wave A done-when (gate for wave B):**
- A v4 save migrates; `R2SaveMigrationTests` green.
- Headless == live == fast-forwarded for a fixed seed in each wave-A mode with AUTO inputs.
- Ticket and attempt banking survive a 3-day jump and refuse a clock rollback; a completed unclaimed mission is granted exactly once at rollover; the kept gift expires after one DayKey.
- 오늘의 순회 completes in one tap with every toggle on; no frame exceeds the time-slice budget.
- The R2 economy block passes (§4.5, §10.2); the basket 별조각 line is unchanged with every R2 modifier maxed.
- Projection run #2 meets §4.4.
- The main formation is byte-identical before and after any 맞춤 편성 battle; the fitting-room preview never equips.
- AutoShot captures 50–63 pass.

---

## 9. Assets

### 9.1 Art (R2P-6; Higgsfield `gpt_image_2_5`, quality max, one asset per image; ART-UPGRADE-PLAN §2 rules)
**Rules (inherited, binding).**
- Prompt = ART-UPGRADE-PLAN §2.2 `[MASTER]` + the batch's style block (`[S-SPRITE]` for battle sprites and costumes, `[S-ICON]` for icons and keepsakes, `[S-SCENE]` (+ `[S-WIDE]`) for backgrounds and banners, `[S-UI9]` for the title plate) + `SUBJECT:` + `REFERENCES:` roles. English names only, no Hangul in prompts.
- References by job id (ART-UPGRADE-PLAN §2.3); upload first when no id exists. The udu and lala lineages are referenced exactly as they are today (no restyle).
- 1k = 5 credits, 2k = 9 credits; `background: transparent` for sprites/icons/UI, `opaque` for scenes and banners; `generate_image_batch` (≤ 12) → `jobs_wait` → one `show_generation_by_ids`.
- QA gate ("excellent or regenerate", ≤ 2 re-rolls from a 20% reserve), post-processing with `art2/process.py` (`clean_alpha`, `remove_halo`, `fit` feet/icon), staging in `scratchpad/phase5/art/<batch>/{raw,out,preview}` with `jobs.tsv`; nothing reaches `Art/**` before QA.
- **Balance floor:** before every submission `balance ≥ cost + floor`, where the floor protects the FX-JUICE, frame and SPEC4/ART-UPGRADE-PLAN work still pending (ART-UPGRADE-PLAN §1.3). Today's plan leaves ≈ 15 credits after those, so **R2 art waits for the top-up the user has been asked about**; R2 systems ship with the fallbacks below until then.
- Tell the user the R2 totals (core ≈ 284 + reserve ≈ 57 = **≈ 341**, optional ≈ 225) when the top-up is answered, not mid-wave.

| Wave | Asset (file) | Size · bg | Subject (prompt core) | References | Credits | Fallback |
|---|---|---|---|---|---|---|
| R2-0 | `Icons/rail_outings.png` | 256 · T | a wooden alley signpost with a small hanging paper lantern | lamp b1dc188b, quest (upload) | 5 | `Icons/lamp` |
| R2-0 | `Icons/rail_home.png` | 256 · T | a warm arched back-room door with lamplight spilling out | shop 8a5942a2, journal (upload) | 5 | `Icons/mission_visit` |
| A | `Enemies/enemy_magpie.png` | 1k · T | a magpie-shaped shadow made of forgetting, indigo/lavender translucent body, gold-button eyes, carrying a cream button sack in its beak | boss_shadow beb64e6f, enemy_moth f127e516, enemy_dust 0cd79557 | 5 | `enemy_moth` tinted indigo ×1.3 |
| A | `Enemies/enemy_nest.png` / `enemy_nest_broken.png` | 1k · T | a wicker laundry basket nest with dust puffs peeking out / the same basket tipped over and empty (image edit of the first) | enemy_dust 0cd79557, idle_basket_2 (upload) | 5 + 5 | `UI/idle_basket_2` |
| A | `Guests/guest_wardrobe.png` | 1k · T | an old wooden wardrobe grandpa, a living forgotten object with drawer-mouth and brass-knob eyes, one door hanging loose | jjak_base f983e23c, chal_base 4ad012d6, shop 8a5942a2 | 5 | tinted `boss_shadow` + name plate |
| A | `Guests/guest_clock.png` | 1k · T | a stopped grandfather clock, pendulum still, face with soft eyes, one hand bent | same | 5 | same |
| A | `Guests/guest_mirror.png` | 1k · T | a cracked hand mirror on a little stand, a gentle face in the reflection, a thin crack across | same | 5 | same |
| A | `Guests/guest_bell.png` | 1k · T | a rusty bicycle bell with a dented dome, tiny handlebar arms, rain drops on it | same | 5 | same |
| A | `UI/stitch_mark.png` | 512 · T | one gold cross-stitch mark (two crossed gold thread stitches on a tiny felt patch) | star f7b4bb2d, currency_gold 7b52dc09 | 5 | `Icons/star` tinted Gold 0.8× (placeholder only) |
| A | `Costumes/ttok_pajama.png` | 1k · T | 똑단이 in a tiny striped pajama with a button-hole bed-cap | ttok_base 00dab067, nightkeeper 3e492504 | 5 | the base form |
| A | `Costumes/chal_keyring.png` | 1k · T | 찰칵이 wearing a traveller's belt jingling with souvenir keyrings (station, lighthouse, laundry) | chal_base 4ad012d6, traveler (upload) | 5 | base form |
| A | `Costumes/mong_birthday.png` | 1k · T | 몽당이 with a cake-shaped party hat of star candy, a never-out tiny flame | mong_base f6d15773, festival (upload) | 5 | base form |
| A | `Costumes/udu_raincoat.png` | 1k · T | 우두커니 in a yellow after-rain raincoat, canopy hood folded back, a small rainbow ribbon | udu_base d67e1fbe, udu_parasol 7d9ff3a8 | 5 | base form |
| A | `Costumes/rouge_stage.png` | 1k · T | 루즈리 in shining stage make-up with a tiny spotlight sparkle, gold case polished | rouge_base d4b9a2b6 | 5 | base form |
| A | `Costumes/lala_lullaby.png` | 1k · T | 라라벨 in a starry pajama ribbon and nightcap, music-box drum with a moon motif | lala_base a832626f, lala_waltz a057706d, sleepcap (upload) | 5 | base form |
| A opt | `UI/errand_{magpie,laundry}_banner.png` | 21:9 · O | the alley chase at night / laundry lines between lamp posts | bg_night b937156d, field (media 73d203b2) | 10 | `UI/zone_exp_*` |
| A opt | 7 technique icons `Icons/tq_*.png` | 512 · T | needle tip, stitch line, quick hands, knot, wick, hard knot, shadow iron | `Icons/skill_*` set | 35 | `Icons/skill_*` |
| B1 | `UI/tower_bg.png` | 1k · O | the night lost-and-found office: tall wooden shelves of forgotten things with name tags, a ladder | dungeon_banner abf8ffad, bg_night b937156d | 5 | `UI/dungeon_banner` |
| B1 | `Enemies/boss_dustking.png` | 1k · T | a crowned dust king made of rolled dust and thread, a paper crown | enemy_dust 0cd79557, boss_shadow beb64e6f | 5 | `enemy_dust` ×1.6 + tint |
| B1 | `UI/title_plate_9s.png` | 1k · T (`[S-UI9]`) | a small brass name plate with stitched felt backing | button_gold_raw 9448cfa9 | 5 | `UiArt.PillNight9s` |
| B1 | `UI/festival_{a..e}.png` | 21:9 · O | five theme banners: thread weaving, friend searching, lantern lights, expedition road, night outing | shop_event_banner (upload), bg_night | 25 | `UI/shop_event_banner` |
| B1 | `Icons/moon_phase.png` | 512 · T | a soft full moon icon (phases clipped in code) | stage_fullmoon 0b3a33a6 | 5 | `Icons/stage_fullmoon` |
| B1 | `Icons/lantern_chip.png` | 512 · T | a small paper lantern filled with firefly light | lamp b1dc188b | 5 | `Icons/lamp` |
| B2 | `UI/walk_strip.png` | 21:9 · O | a long night street of lamp posts receding, each lamp a node | bg_night, field (media 73d203b2) | 5 | `UI/bg_night` crop |
| B2 | `Enemies/npc_shadow_merchant.png` | 1k · T | a hooded shadow merchant with a lantern of dark charms, polite and eerie | boss_shadow beb64e6f | 5 | `boss_shadow` tinted |
| B2 | `UI/home_bg.png` / `UI/workbench_bg.png` | 1k · O | a cut-away storybook view of the shop's back rooms / a sewing-machine room at night | shop 8a5942a2, bg_night | 5 + 5 | `UI/content_bg` |
| B2 | `UI/shelf_bg.png` | 1k · O | a wooden keepsake shelf with 5 lit niches | panel_parchment_raw 9448fe4e | 5 | `UiArt.PanelInset9s` |
| B2 | 16 keepsake icons `Keepsakes/{id}.png` (7 lineage, 8 souvenirs, attendance) | 512 · T | per Appendix B.5 names | items (cloth 2e488620, thread c7063fbd, spring fe2aebb7), star | 80 | the source's art cropped |
| B2 | 6 forms `Characters/{ttok_yoyo, chal_heartkey, mong_lighthouse, udu_thunder, rouge_compact, lala_carousel}.png` | 1k · T | per §6.9 | the base and tier-1 parent (ttok_spinner 8b1195c4, chal_master (upload), mong_lantern 2b3547b6, udu_storm 1978b1ff, rouge_gloss (upload), lala_waltz a057706d) | 30 | none for shipping (dev: parent + outline) |
| B2 | `Battle/field_market_tall.png` | 2k · O (image edit) | the same plaza at a lantern-lit night market: stalls, string lights, the plaza ring untouched | field_tall (upload), field 73d203b2 | 9 | `Battle/field_tall` |
| B2 (CR4-22) | `Battle/field_market.png` | 1080×810 · O | PIL crop of rows 619–1428 of `field_market_tall` (the `field.png` window), for the formation mini-field and the 4:3 fallback | – | 0 | `Battle/field` |
| A (CR4-22) | mode intro cards (`ModeIntroDef.art`, 3 per mode × 4) | 840×620 cards | reuse, cover-fit: errand → `enemy_magpie` / `enemy_nest` / `UI/zone_exp_*`; guest → the week's portrait / `boss_shadow` tinted / `UI/stitch_mark`; tower → `UI/tower_bg` / `boss_shadow` / a trial-rule icon; walk → `UI/walk_strip` / charm icons / `npc_shadow_merchant` | – | 0 | `UI/bg_night` crop |
| B2 opt (CR4-22) | 6 new-form skill icons `Icons/skill_{yoyo_pull, heart_open, lighthouse_beam, thunder_strike, mirror_reflect, carousel_spin}.png` | 512 · T | per §6.9 skill concepts | the `Icons/skill_*` set | 30 | the closest existing `Icons/skill_*` |
| B2 | `Enemies/boss_bell.png` | 1k · T | the shadow cast by a forgotten market hand bell, a bell-shaped shadow with a ringing ripple | boss_shadow beb64e6f, boss_mothqueen 9a1d6cc3 | 5 | `boss_shadow` tinted |
| B season | 1 season costume per Q-06 season (order §5.7) | 1k · T | per lineage | its base form | 5 each | the fallback reward |
| B opt | 8 more guest portraits, 16 more keepsakes, 4 dark-charm icons, 4 firefly variants, 1 stall banner, 3 mode-source costumes (S5-19) | – | – | – | 40 + 80 + 20 + 20 + 5 + 15 | fallbacks above |
**Totals:** R2-0 10 · A core 70 (+ 45 optional) · B1 core 50 · B2 core 149 · the first season costume 5 (then 5/month) → 284, + a 20% re-roll reserve ≈ 57 → **≈ 341 core**; ≈ 225 optional (CR4-22: + 30 for the optional new-form skill icons; the crop and the intro cards are 0).
**Importer (R2P-6):** `Art/Guests/*` sprite import like `Characters/*`; `Art/Keepsakes/*` like `Icons/*`; `Art/UI/*_bg` and banners no-mip (`IsLargeBackdrop`); `title_plate_9s` 9-slice 48.

### 9.2 Audio (R2P-6 briefs → the AUDIO-SPEC workflow; 0 credits)
Every key follows AUDIO-SPEC: synthesis (SYN), hybrid with an optional Kenney CC0 layer (HYB), or an original score (MUS); takes `{key}_{NN}.wav`; QC; the user picks takes in '사운드 시험장'. R2 code resolves keys once with `AudioService.Resolve` and plays them; music through `PushMusic/PopMusic`. **Request to the AUDIO owner:** append `MusicState.Guest = 7, Tower = 8, Walk = 9` (`AudioTypes.cs`, priority between Dungeon 30 and Event 40: Guest 35, Tower 33, Walk 34) and promote the reserved keys `bgm_worldboss` (→ Guest), `bgm_tower`, `jingle_tower_floor` from P3.

| Key | Wave · P | Bus · class | Len s | Route | Sound | Trigger |
|---|---|---|---|---|---|---|
| `errand_start` | A · P1 | Sfx · High | 0.8 | SYN | small door bell + quick footsteps on cobbles | side battle start (errand) |
| `magpie_flutter` | A · P1 | Sfx · Normal | 0.4 | HYB | papery shadow-wing flap, hollow | `ObjectiveHop` |
| `magpie_sack_drop` | A · P1 | Sfx · High | 0.6 | SYN | a cloth sack plops, buttons scatter (wood clicks) | errand 1 full result |
| `nest_spawn` | A · P2 | Sfx · Low | 0.3 | SYN | wicker creak + dust puff | Spawner summon (≤ 2/s) |
| `nest_break` | A · P1 | Sfx · High | 0.7 | HYB | wicker crackle + soft dust burst | nest killed |
| `jingle_errand_clear` | A · P1 | Stinger · Critical | 2.0 | MUS | pizzicato + glockenspiel errand-done motif `inkey` | full result |
| `guest_layer_peel` | A · P0 | Sfx · High | 0.7 | SYN | a soft cloth tearing away + shimmer rising, pitch +1 step per layer | `ObjectivePhase` (guest) |
| `guest_shell_crack` | A · P1 | Sfx · High | 1.0 | SYN | glassy shell crack + low exhale | shell 1 → 2 |
| `bgm_worldboss` (promoted) | A · P0 | Bgm | 60.95 | MUS | AUDIO-SPEC §2.11 score, re-voiced tender-urgent (we mend, we don't destroy) | guest fight |
| `jingle_guest_reveal` | A · P0 | Stinger · Critical | 3.0 | MUS | music-box + strings "mended" cadence (never the 7th degree) `inkey` | GuestReveal |
| `jingle_cap_break` | A · P0 | Stinger · Critical | 2.2 | MUS | a thread pulled taut → bright bell + harp rise `inkey` | 한 땀 돌파 |
| `embroidery_stitch` | A · P1 | Ui · Normal | 0.5 | SYN | needle-through-cloth tick ×2 + tiny chime | 추억 수놓기 |
| `jingle_rank_up` | A · P1 | Stinger · Critical | 1.6 | MUS | three rising stitched notes + warm chord `inkey` | 수선공 등급 up |
| `rounds_step` | A · P1 | Ui · Normal | 0.25 | SYN | soft tick, ladder +1 semitone per step (tuned) | rounds step done |
| `rounds_done` | A · P1 | Ui · High | 0.9 | SYN | basket rustle + chime | rounds finished |
| `fitting_preview` | A · P2 | Ui · Normal | 0.3 | alias `ui_costume_equip` (−6 dB) | – | fitting preview |
| `jingle_tower_floor` (promoted) | B1 · P1 | Stinger · Critical | 2.0 | MUS | §2.11 | floor first clear |
| `bgm_tower` (promoted) | B1 · P1 | Bgm | 71.11 | MUS | §2.11 | live tower floor |
| `rule_chip` | B1 · P2 | Ui · Normal | 0.3 | SYN | a paper tag flick | trial-floor rule shown |
| `title_equip` | B1 · P2 | Ui · Normal | 0.4 | SYN | small brass plate click + sparkle | title equipped |
| `lantern_fill` | B1 · P2 | Sfx · Low | 0.4 | SYN | glass-jar firefly hum swell | lantern full |
| `lantern_open` / `note_pick` | B1 · P1 | Ui · High / Normal | 0.8 / 0.4 | SYN | paper lantern opening + glow / note unfold | lantern open / pick |
| `moth_hiss` | B1 · P1 | Sfx · Normal | 0.5 | SYN | a soft eerie powdery hiss | moth caught |
| `market_open` | B1 · P2 | Ui · Normal | 0.8 | HYB | stall awning flap + small bell | market section first open |
| `weave` | B1 · P2 | Ui · Normal | 0.5 | SYN | thread twist + pop | merge |
| `moon_rise` | B1 · P2 | Ui · Normal | 1.2 | SYN | glassy moonlight swell | moon chip on a moon night |
| `festival_round` | B1 · P1 | Ui · High | 1.0 | SYN | paper popper + three chimes | festival round done |
| `walk_step` | B2 · P1 | Sfx · Normal | 0.6 | HYB | footsteps + a lamp clicking on | next node |
| `charm_pick` | B2 · P1 | alias `ui_blessing_pick` | – | – | – | charm chosen |
| `crest_light` | B2 · P1 | Ui · High | 0.9 | SYN | stitched emblem glow + rising fifth | crest on |
| `merchant_whisper` | B2 · P1 | Sfx · Normal | 1.2 | SYN | a breathy reversed whisper texture (no words) | merchant node |
| `walk_rest` | B2 · P2 | Sfx · Normal | 1.0 | SYN | lamp hum + soft sigh chord | rest node |
| `jingle_walk_clear` | B2 · P1 | Stinger · Critical | 2.6 | MUS | lantern-walk theme cadence `inkey` | walk finished |
| `bgm_lanternroad` | B2 · P1 | Bgm | ≈ 70 | MUS | new score: 3/4 lamp-to-lamp walk, celesta + bowed glass, D Dorian | walk fights and page |
| `research_start` / `research_done` | B2 · P1 | Ui · Normal / High | 0.5 / 1.0 | SYN | sewing machine whirr / finished-stitch chime | workbench |
| `keepsake_get` / `keepsake_place` / `keepsake_polish` | B2 · P1 | Ui · High / Normal / Normal | 1.0 / 0.4 / 0.6 | SYN | little treasure chime / wood niche click / cloth polish squeak | keepsakes |
| `bell_boss_ring` | B2 · P1 | Sfx · High | 1.2 | SYN | a distant shadowy hand bell, detuned | place-2 boss chime |
| `dustking_reform` (CR4-21) | B1 · P1 | Sfx · Normal | 0.8 | SYN | dust gathering in a soft whoosh + a muffled thump, +1 semitone per re-form | errand 3 `ObjectivePhase` |
| `amb_market` (CR4-20) | B2 · P1 | Amb · Critical | 40 `loop` | AMB (SYN/HYB) | 야시장 골목 bed: string-light hum, awning flaps, far stall bells, no voices | `AppSignals.PlaceChanged` → base bed for place 2 (modifier layers stay on top) |
No voice, no Higgsfield audio model. Battle cues go through `BattleAudioDirector` mapping (the AUDIO owner adds the event→key rows for `ObjectivePhase`, `ObjectiveHop` and the place boss). (CR4-20) The AUDIO owner also adds the place layer to the ambience director (AUDIO-SPEC §3.12): `PlaceChanged` swaps the base bed from `amb_night` to the place's `ambKey`, crossfaded 2.0 s, with the same load/unload rule as the modifier layers.

---

## 10. Verification

### 10.1 EditMode tests (new files unless noted; all run in the verify_proj batch)
- **R2-0:** `R2SaveMigrationTests` (v4 → v5: every new section non-null, the two migration flags, re-save as v5; a v5 save round-trips), `FeaturePacingTests` (`alsoRequire` AND; `PlayMinutes` once per minute, sleep counted, pause not; card spacing 180 s; two cards of one moment back to back; a seen feature drops its card), `PopupOrderTests` (order table; GuestReveal never dropped by the cap; no modal during a side battle), `CurvePackageTests` (§4.1–§4.2), `PowerRatingTests` (cap; three-segment continuity at 30 and 151), `SideBattleHostTests` (park/resume keeps `highestStage`, `challengeTarget`, farm state; no `StageChanged`/`StageEnded` during a side battle; counters per §3.6; units fixed; abandon spends nothing; FTUE refuses; auto-resume after 10 s; `IsBetweenStages` false), `SideScriptTests` (objectives score, phases with knock-back and behaviours, Fleeing hop, Spawner chain, clear flags, time up).
- **A:** `WeeklyGuestTests`, `TechniqueTests`, `CapBreakTests`, `ErrandTests`, `RoundsTests`, `BankingTests`, `MissedRewardsTests`, `ModeFormationTests`, `CostumeR2Tests`, `ContentR2Tests` (validator rules), updated `{IdleChest, Mission, DailyGift, CharacterSystem, CostumeSystem, Power, StrengthenAdvisor, MetaSession, NotificationPlanner}Tests`.
- **B1:** `TowerTests`, `RuleTableTests`, `TitleTests`, `ProfileTests`, `LanternTests`, `RouteMarkTests`, `MarketTests`, `MergeTests`, `FestivalRoundsTests`, `MoonPhaseTests`, `ErrandDustKingTests`.
- **B2:** `NightWalkTests`, `WorkbenchTests`, `KeepsakeTests`, `NewFormsContentTests`, `PlaceTests`, `RiderTests`.
- **Gate:** the full suite (SPEC4's total + new) passes; the v4 player save loads, plays 60 s headless with one side battle of each shipped mode, and re-saves as v5.

### 10.2 Economy budget (`EconomyBudgetTests` R2 block, R2P-7; numbers from content via `MetaContent.Apply`)
| Row | Budget | Spec value |
|---|---|---|
| Errand milestones, 별조각 per 10 levels | ≤ 10 + one 빠른 수선권 | 10 (+1 ticket on errand 1) |
| Guest band gems at 추억 가득 1–2 / max | ≤ 95 / ≤ 110 | 90–92 / 110 |
| Guest 선택 기억편 at 추억 가득 1–2 | ≤ 12 | 11 |
| Tower trial chest | ≤ 20 별조각 + 2 선택 | 20 + 2 |
| Tower 별조각 per 10 floors | ≤ 20 | 20 |
| 등불 길 weekly chest / depth first clears lifetime | ≤ 50 + 10 선택 / ≤ 500 | 50 + 10 / 500 |
| 등불 길 lineage 기억편 per week (10 walks × ≤ 7) | ≤ 70 | 70 |
| Festival per month | ≤ 60 별조각 + 15 선택 | 60 + 15 |
| Lantern | 0 별조각, ≤ 7 lineage 기억편/week | 0 / 7 |
| Q-04 R2 lines, lifetime | ≤ 1,000 (CR4-04: was 850; RM-9's 15 hidden records, +150, must fit) | 800 (950 with R2-26) |
| Codex R2 rows, lifetime | ≤ 560 | 520 |
| (CR4-04) SPEC4's lifetime rows: records ≤ 3,500, codex ≤ 2,700 | computed over SPEC4 ids only; `MetaContent` tags the R2 rows, which the two rows above own | 3,400 / 2,615 |
| (CR4-01) SPEC4 chapter chests, ch6+, 별조각 per 10 chapters (PHASE4-ROADMAP RD-13) | ≤ 200 | 200 (100 on chapters ending in 5 or 0) |
| **Weekly model total (§4.5), week 4+** | **≤ 250 별조각, ≤ 30 선택, ≤ 80 lineage** (the weeks 5–8 mean; week 4 ≤ 350, CR4-03) | ≈ 206 / ≈ 20 / ≤ 77 |
| (CR4-05) `GoldMinutes` with `income.enabled = false`; at stage ≤ 30 | identical to today; within ±15 % of today | – |
| Basket 별조각 per hour with every R2 modifier at max (workbench, moon, keepsakes, place) | identical to the no-R2 value | identical |
| R2 grants of `CurrencyType` outside Gold/Gem/EventToken | 0 | 0 |
| Rollover grant | ≤ what the period earned | equal |
| Banks (errand, guest, quick, gift) for a daily player | +0 | 0 |

### 10.3 Projection (sim harness, R2P-7)
Runs #1 (after R2-0), #2 (after A), #3 (after B2, 12 seeds) per §4.4, plus: weekly payouts per R2 source vs §4.5 (±25%; the week-4 column for stage-tracking rows, CR4-03); the rating-chip prediction rate (§4.3); the material projection including route marks, the workbench sink and festival candy (R2-11 trigger: commons owned at day 14 > 3× the next tier's need); the ceiling with the six new forms (report the measured move, ≈5–15 stages expected; CR4-07). (CR4-02, CR4-06) Every run also logs 별조각 per source (journal, attendance, basket, quick idle, mail incl. the shop-level mail, chests, codex, records, affinity, events, season, each R2 mode) and runs the `auto` player variant. Report: `scratchpad/phase5/projection_{run}.md` with the same tables as PROGRESSION-SIM §7, plus the measured ceiling stage and day (input for PHASE4-ROADMAP Q16).

### 10.4 Performance (R2P-7, SPEC4 §11.3 harness)
New `PerfProbe` scenarios (dev player, 1080×1920, 2×): `errand_magpie` (echoes chasing, hops), `errand_laundry` (5-nest chain at max minions), `guest_10_layers`, `tower_autoclimb` (30 floors headless behind the UI), `fastforward` (guest from t = 5 s), `techniques_max` (손놀림 100 + 두 땀 뜨기 250 + all riders, SPEC4 stress), `walk_crests` (crest specials active). Acceptance: frame p95 ≤ 8 ms; headless steps ≤ 2 ms/frame; fast-forward ≤ 8 ms/frame; GC/frame ≤ 64 B; particle budget never exceeded; 0 exceptions.

### 10.5 AutoShot captures (R2P-7; names, numbering after SPEC4's 42)
| # | Name | Setup | Assert |
|---|---|---|---|
| 50 | rails_r2 | Outings unlocked (dev) | `OutingsButton` at R2 (964–1068 × 1222–1338), journal at R3; with Home (B2) at L2 |
| 51 | outings_hub | open hub | header, rumour line, 3 cards; sheet top y 914; pan 360 |
| 52 | errand_page | errand 1 | stepper, rec line, chips, reward preview, 맞춤 편성 row, buttons inside 0–692 |
| 53 | errand_battle | start errand 1 **from the open hub sheet**, 3 s | frame MAIN (sheet closed, pan 0; CR4-10); SideHud in the StagePanel rect; SideBattleBar in the guide-bar rect; centre label '사냥으로'; no StageChanged logged |
| 54 | errand_result | finish now | result card; [확인] reopens the errand page |
| 55 | guest_page | WeeklyGuest unlocked | portrait, traits, 모두 함께, meter with 25 pips |
| 56 | guest_door | guest fight 4 s | the guest at the door with 5 rings; after one peel 4 rings |
| 57 | guest_reveal | fake clock next Monday 05:01 | popup after Return; rewards already in the wallet |
| 58 | technique_segment | Techniques unlocked | segment, RankCard, rows; `LevelUp1Button` path still resolves |
| 59 | capbreak_button | a lineage at Lv100 | [한 땀 돌파] with cost chips; marks row |
| 60 | fitting_room | detail → costume tab, tap a locked costume | preview + unlock line; `equippedCostume` unchanged in the log |
| 61 | rounds_summary | DailyRounds unlocked, all on | 8 rows, merged rewards, log `[Verify] rounds_max_frame_ms<=2` |
| 62 | gift_kept | fake clock +1 day without claiming | two chips + '어제 선물도 남아 있어요' |
| 63 | menu_r2 | ≡ menu with SPEC4's tiles unlocked | 밤 나들이 tile after SPEC4's activity tiles and before 가방; rows = ⌈tiles/3⌉; the window fits the frame; `MenuTileOrder` matches (CR4-09) |
| 70–79 (B1) | tower_page, tower_trial, tower_autoclimb, profile, title_picker, lantern_chip, lantern_notes, market, weave, festival_strip | per feature | layouts in their rects; the lantern chip at 500–564 × 1500–1564, ≥ 8 px from the guide bar |
| 80–89 (B2) | walk_page, walk_charm, walk_merchant, home_hub, workbench, keepsake_shelf, new_form_tree, place2_field, place2_boss_hint, rider_lines | per feature | ring overlay check on `place2_field` (ratio 1 ± 0.02) |
Static checks (log only): no TMP < 18 outside the allowlist; no '개방'/'습니다'; every new coach path resolves; the lantern chip vs the guide bar and DeckTab (≥ 8 px); no NPC rank or score strings.

### 10.6 Manual checks (user or device owner)
1. One full day of play on a device: R2 cards arrive no faster than one per 3 minutes; the first R2 card appears after ≈ 20 minutes.
2. The guest fight reads as "peeling the shadow off the guest", not fighting the guest (tone).
3. Ear check of the R2 cues and the three music states.
4. Grayscale pass of the meter, rating chips and crest pips (colour is never the only cue).
5. Android back in a hub sheet: closes the hub; during a side battle: closes the top popup, then the sheet, then the abandon confirm (through `SideBattleBarView`'s `PushBackHandler`, CR4-11; never the exit confirm).

---

## 11. Risks (top first)
1. **Pacing.** The chapter sprint (14 minutes to 5-10) is a property of today's game; the play floors and card spacing only spread the cards. *Mitigation:* FeaturePacingTests, capture 61, manual check 1; the floors are content knobs.
2. **Shell files are frozen.** The hub-sheet extension touches `MainTabController`. *Mitigation:* a 25-line, member-listed exception (§2.6) and a complete popup fallback; no other shell file changes.
3. **Economy drift through stage gains.** R2-04 lifts the stage, and anything stage-scaled (basket, errands, tower) follows. *Mitigation:* C-01 mandatory, levels and floors tied to the stage with small gem rows, §10.2 asserts, projection runs per wave.
4. **Art credits.** R2 core ≈ 341 credits against an exhausted plan. *Mitigation:* fallbacks for every asset, the balance floor, B2 waits for the top-up answer.
5. **Side-battle state bugs** (a killed app mid-battle, popups over a side battle, double grants). *Mitigation:* nothing about a side battle is saved, tickets are spent only on the result, `IsBetweenStages` false during side battles, `SideBattleHostTests`.
6. **Day-boundary ordering.** 놓친 선물, kept gift and banks must read the old period. *Mitigation:* `DayChanging`/`WeekChanging` raised before the counters reset, `GetPeriodValue`, MissedRewardsTests in UTC and +09:00.
7. **Performance with techniques, riders and chasing echoes.** *Mitigation:* the double hit is a proc, not a projectile; §10.4 scenarios.
8. **Scope** (≈ 26 + 18 + 24 agent-days). *Mitigation:* B1/B2 split, wave gates, every B item cut-safe behind its FeatureId.
9. **Audio dependency.** R2 cues need the AUDIO workflow. *Mitigation:* silence is the no-clip behaviour; briefs are delivered in R2-0.
10. **The rating repair changes SPEC4's numbers** (power pill, advisor, rec line). *Mitigation:* it lands first (R2-0) with its own tests and a calibration re-run; it makes the pill informative again rather than always '여유'.
11. **Name collisions.** '분실물' (zone 2-3, story ch4), '선반' (keepsakes), '한 땀' (breaks, embroidery). *Mitigation:* '보관소 오르기' (S5-10), '추억 수놓기', the critic's renames kept; the content lint lists duplicate display names across systems.
12. **Guest art arrives late.** *Mitigation:* the launch set is 4 portraits; until art exists the guest uses the tinted shadow + name plate and the cycle still runs.

---

## Appendix A: Korean copy, wave A (R2P-5; 해요체, checked for '습니다' and '개방'; particles through `KoreanText`)

### A.1 Frame, hub and side battles
- Rail labels: '밤 나들이' · '안채' (B2). Menu tiles: '밤 나들이' · '수선점 안채' (B2).
- Hub header: '밤 나들이'; CTA '오늘의 순회 ▶'; gear tooltip '순회 설정'.
- Rumour line: '이번 주 소문 · {특성1}·{특성2} 친구들이 힘을 내요'; tooltip '심부름 · 보관소 · 등불 길에서 이 특성 친구는 피해 +15%예요'.
- Cards:
  - '이번 주의 손님' / '{이름} · {등급} {n}' / '오늘 {k}/3 · 보관 {b}' (보관 part only when b > 0)
  - '밤 골목 심부름' / '입장권 {합계} · 최고 Lv.{n}' / '오늘 다녀올 수 있는 심부름 {k}개'
  - '잔향이 던전' / '입장권 {n}/{cap}' / '{f}층까지 돌파했어요'
  - '보관소 오르기' (B1) / '분실물 보관소 {n}층' / '다음 상자까지 {m}층'
  - '등불 길' (B2) / '등불 {n}/3' / '가장 깊이 걸은 밤 {d}'
  - locked detail: M39 `UnlockSentence` ('스테이지 2-6을 클리어하면 열려요'); play-floor only: '조금만 더 수선점을 돌보면 열려요'
- Page back: '‹ 밤 나들이'.
- Side HUD titles: '그림자 까치 쫓기 Lv.{L}' · '빨랫줄 걷기 Lv.{L}' · '그림자 떼어 내기 · {이름}' · '보관소 {f}층' (B1) · '등불 길 {i}/15 · 깊이 {d}' (B2).
- Side HUD scores: '떼어 낸 그림자 {p}%' · '바구니 {k}/5' · '떼어 낸 껍질 {n}' · '남은 적 {n}' (B1).
- Side bar: '결과 바로 보기' · fast-forward '결과를 정리하고 있어요…' · fallback '그만하기'.
- Centre button label: '사냥으로'. Confirm title '나들이를 멈추고 돌아갈까요?'; bodies: errand '지금 돌아가면 입장권은 쓰지 않아요.' · guest '지금 돌아가면 도전 횟수는 줄지 않아요.' · tower '이 층은 다음에 다시 올라요.' · walk '등불 길은 그 자리에 멈춰 기다려요.'; buttons '돌아가기' / '계속하기'.
- Refusals: FTUE '튜토리얼이 끝나면 다녀올 수 있어요' · busy '나들이가 끝나면 할 수 있어요' · no team '편성을 먼저 해 주세요' · tampered `DailyClock.TamperedMessage`.
- Result card buttons: '확인' · '다시 하기' · '사냥으로'.

### A.2 First-run explainers ('이렇게 해요', 3 cards each)
- **심부름:** ① '입장권으로 다녀와요' / '심부름마다 하루 2장, 6장까지 모여요. 선물을 받을 때만 입장권을 써요.' ② '단계를 올려요' / '한 번 끝까지 해내면 다음 단계가 열려요. 강해지면 높은 단계부터 시작할 수 있어요.' ③ '끝낸 단계는 소탕으로' / '이미 해낸 단계는 싸우지 않고 바로 선물을 받아요.'
- **이번 주의 손님:** ① '손님은 고치는 거예요' / '망각의 껍질만 떼어 내요. 손님은 다치지 않아요.' ② '하루 세 번, 가장 좋은 기록' / '그날 가장 많이 떼어 낸 기록이 바늘땀이 돼요. 실패해도 떼어 낸 만큼은 남아요.' ③ '일주일 동안 바늘땀을 모아요' / '스테이지 · 원정 · 바구니로도 바늘땀이 쌓여요. 월요일에 선물을 받아요.'
- **보관소 오르기 (B1):** ① '입장권이 필요 없어요' / '몇 번이든 다시 올라도 돼요. 층마다 처음 한 번 선물이 있어요.' ② '열 층마다 시험 칸' / '시험 칸에는 규칙이 하나 붙어요. 맞춤 편성이 규칙에 맞춰 세워 줘요.' ③ '자동으로 올라요' / '쉬운 층은 한 번에 정리하고, 강해지면 친구들이 스스로 다시 올라 봐요.'
- **등불 길 (B2):** ① '등불 하나로 한 번' / '등불은 8시간마다 하나씩 생겨요. 걸을 때마다 길이 달라져요.' ② '부적을 골라요' / '세 개 중 하나를 고르고, 같은 특성을 모으면 문장이 켜져요.' ③ '내구도는 이어져요' / '쉼터에서 쉬고, 그림자 상인은 조심하세요. 쓰러져도 얻은 선물은 남아요.'

### A.3 이번 주의 손님 (12 guests)
| order | id | Name | Traits | Thank-you line | Memento (B2) |
|---|---|---|---|---|---|
| 1 (launch) | `guest_wardrobe` | 낡은 옷장 할아버지 | 천 · 밤 | '고맙구나. 이제 다시 따뜻한 옷을 품을 수 있겠어.' | 옷장 손잡이 |
| 2 (launch) | `guest_clock` | 멈춘 괘종시계 | 금속 · 소리 | '고마워요. 이제 다시 시간을 셀 수 있어요.' | 괘종시계 추 |
| 3 (launch) | `guest_mirror` | 금 간 거울 | 빛 · 치장 | '고마워요. 이제 비친 얼굴이 웃고 있어요.' | 거울 조각 |
| 4 (launch) | `guest_bell` | 녹슨 자전거 종 | 금속 · 비 | '따릉! 고마워요. 이제 비 오는 골목도 무섭지 않아요.' | 자전거 종 |
| 5 | `guest_birdcage` | 빈 새장 | 밤 · 소리 | '고마워요. 이제 노래가 머물 자리가 생겼어요.' | 새장 문고리 |
| 6 | `guest_bear` | 구멍 난 곰인형 | 천 · 치장 | '고마워요. 이제 다시 꼭 안아 줄 수 있어요.' | 곰인형 단추 눈 |
| 7 | `guest_map` | 바랜 지도 | 밤 · 비 | '고마워요. 이제 집으로 가는 길이 다시 보여요.' | 접힌 지도 조각 |
| 8 | `guest_lampshade` | 깨진 램프갓 | 빛 · 천 | '고마워요. 이제 불빛을 부드럽게 감쌀 수 있어요.' | 램프갓 술 |
| 9 | `guest_kite` | 찢어진 연 | 비 · 치장 | '고마워요. 이제 바람을 타고 다시 날아 볼게요.' | 연 꼬리 리본 |
| 10 | `guest_watch` | 고장 난 회중시계 | 금속 · 밤 | '째깍, 째깍. 고마워요. 잊었던 약속이 생각났어요.' | 회중시계 태엽 |
| 11 | `guest_piano` | 낡은 피아노 의자 | 소리 · 천 | '고마워요. 이제 누군가 앉아 노래할 수 있어요.' | 피아노 건반 하나 |
| 12 | `guest_ornament` | 잃어버린 트리 장식 | 빛 · 치장 | '고마워요. 올겨울엔 다시 반짝일 수 있어요.' | 별 모양 장식 |
- Band names: '실금' · '반짝' · '새것처럼' · '추억 가득' (shown with the step: '반짝 3').
- Page: '월요일 새벽 5시까지 {d}일 {h}시간' · trait chip '{특성} · 피해 +30%' · '모두 함께 +{n}%' (tooltip '가진 형태 하나마다 피해 +1%예요 (최대 +30%)') · '지난번 최고 {n}단계' · '오늘 {k}/3' · '보관 {b}' · meter '{등급} {n} · 다음 단계까지 바늘땀 {k}' · breakdown '오늘 · 도전 {a} · 스테이지 {b}/60 · 원정 {c}/24 · 바구니 {d}' · preview '이번 주 지금 받을 선물' · button '그림자 떼어 내기' · none left '오늘 도전을 모두 했어요. 새벽 5시에 다시 와요' · link '손님 도감 ›'.
- Result: '껍질 {n}겹을 떼어 냈어요' · '오늘 최고 {best}단계 · 바늘땀 +{s}' · full '껍질을 모두 떼어 냈어요! 손님이 한결 가벼워졌어요'.
- Reveal: title '지난주의 손님' · '{이름}{이/가} 다 나았어요' · button '고마워요' · teaser '이번 주에는 {다음 손님}{이/가} 찾아왔어요 ›'.
- First 추억 가득 costume toast: '손님이 노란 우비를 남기고 떠났어요. 우두커니에게 입혀 볼까요?'.

### A.4 밤 골목 심부름
- 그림자 까치 쫓기: desc '그림자 까치가 단추 자루를 물고 골목을 뛰어다녀요. 놓치기 전에 떨어뜨려 주세요!' · reward line '단추 {m}분치 × 떼어 낸 만큼' · result '그림자를 {p}% 떼어 냈어요' / full '단추 자루를 되찾았어요!'.
- 빨랫줄 걷기: desc '빨래 바구니마다 먼지가 뭉쳐 튀어나와요. 바구니를 하나씩 비워 주세요.' · result '바구니 {k}/5개를 비웠어요' / full '빨랫줄이 깨끗해졌어요!'.
- 다시 뭉치는 먼지 왕 (B1): desc '쓰러져도 더 단단하게 다시 뭉치는 먼지 왕이에요. 얼마나 오래 버티게 할 수 있을까요?' · result '먼지 왕을 {t}단계까지 흩어 놓았어요'.
- Level: 'Lv.{n}' · rec '권장 {rec} · 내 {power}' · next locked '조금 더 강해지면 열려요' · skip-ahead hint (first time) '강해져서 Lv.{n}부터 다녀올 수 있어요'.
- Chips: '저격 친구 피해 ×2' · '돌격 친구 피해 ×1.5' · '지원·힐 친구 효과 ×1.5' · '소문 · {특성}·{특성} +15%'.
- Tickets: '입장권 {n}/6 · 새벽 5시에 +2장' · full '입장권이 가득 찼어요. 오늘 쓰지 않으면 더 쌓이지 않아요' · none '입장권이 없어요. 새벽 5시에 2장이 생겨요'.
- Buttons: '다녀오기' · '소탕'. Sweep popup: title '{이름} 소탕' · '[-] ×{n}장 [+]' · '{n}장 소탕' · result '{이름} Lv.{L} {n}번 다녀왔어요'.
- Not paying: '조금 더 힘을 내 봐요. 입장권은 그대로예요' · level up 'Lv.{n}이 열렸어요' · milestone '심부름 {n}단계 선물을 받았어요'.

### A.5 수선 비법 · 한 땀 돌파 · 순회 · 보관 · 맞춤 편성
- Techniques: segment '수선점' / '수선 비법' · rank '수선공 등급 {r} · 공격력·내구도 ×{m}' · '다음 등급까지 {n}레벨' · tooltip '수선 비법 레벨 25마다 등급이 하나 올라요. 등급마다 공격력과 내구도가 ×1.02씩 커져요.' · buy modes '×1' '×10' '최대' · max '최대 레벨' · rank-up toast '수선공 등급 {r}! 공격력·내구도 ×1.02'.
- Rows: 바늘 끝 '지킴이·잔향이 공격 +{a}% → +{b}%' · 촘촘한 바느질 '수선점 내구도 +{a}% → +{b}%' · 손놀림 '공격 속도 +{a}% → +{b}%' · 매듭 확률 '치명 확률 +{a}% → +{b}%' · 심지 손질 '불빛 충전 +{a}% → +{b}%' · 단단한 매듭 '치명 피해 +{a}% → +{b}%' (locked '매듭 확률 Lv.50이 되면 열려요') · 그림자 다림질 '보스 피해 +{a}% → +{b}%' (locked '바늘 끝 Lv.100이 되면 열려요') · 두 땀 뜨기 (B2) '두 번 꿰매기 확률 +{a}% → +{b}%' (locked '손놀림 Lv.100이 되면 열려요').
- Cap breaks: button '한 땀 돌파' · confirm '레벨 한계를 {cap}에서 {cap+20}으로 올릴까요?' / '{재료 목록}을 써요.' / '돌파하기' · toast '한 땀 돌파! 이제 Lv.{max}까지 올릴 수 있어요' · marks tooltip '한 땀 돌파 {n}/5 · 레벨 한계 {max}' · locked '레벨 {cap}을 채우면 돌파할 수 있어요' · embroidery button '추억 수놓기 {k}/20' / '기억편 30 · 공격력 +0.5%' · toast '추억을 한 땀 수놓았어요. 공격력 +{p}%' · done '추억을 모두 수놓았어요'.
- Advisor rows: '수선 비법 {이름} +{n}' / '전투력 +{gain}' · '{이름} 한 땀 돌파 가능' / '레벨 한계 +20'.
- Rounds: title '오늘의 순회' · steps '바구니 받기' · '빠른 수선' · '원정 받고 다시 보내기' · '잔향이 던전 소탕' · '심부름 소탕' · '손님 그림자 (자동)' · '보관소 다시 오르기' (B1) · '일지·출석·우편 받기' · states '받았어요' · '할 일이 없어요' · '입장권이 없어요' · '아직 잠겨 있어요' · '꺼 둠' · running '순회하는 중… {k}/{n}' · done '오늘의 순회를 마쳤어요' · settings title '순회 설정' · guest row desc '직접 하면 더 높은 기록이 나와요. 켜면 자동 전투로 한 번 해요'.
- Banking: toast '어제 못 쓴 입장권을 모아 뒀어요' · gift-only '어제 선물을 챙겨 뒀어요' · free quick '어제 쓰지 않은 무료 빠른 수선을 하나 챙겨 뒀어요' (widget tag '무료 2') · kept gift card '어제 선물도 남아 있어요' / '모두 받기' · Return line '어제 못 받은 수선 일지 선물을 챙겨 뒀어요' · in-session toast link '보기 ›'.
- 맞춤 편성: switch '맞춤 편성' · subs guest '이번 손님에게 맞춰 세워요' · errand '{역할} 친구를 먼저 세워요' · trial (B1) '시험 칸 규칙에 맞춰 세워요' · toasts '이번 손님에게 맞춰 세웠어요' / '이번 심부름에 맞춰 세웠어요' / '시험 칸에 맞춰 세웠어요' · link '편성 바꾸기 ›'.

### A.6 옷장 넓히기 (six costume texts; `Costume(...)` descriptions, 4 lines)
- **잠옷 단추 똑단이** (`ttok_pajama`, 떨어짐 → 다시 채움; acquisition '똑단이 공명 Lv.3이 되면 받아요'): "외투에서 떨어진 날부터 똑단이는 밤이 무서웠어요. 단추가 없으면 옷 틈으로 바람이 들어오니까요.\n수선점 친구들이 작은 잠옷을 지어 주며 말했어요. \"오늘 밤은 네가 채워질 차례야.\"\n폭신한 잠옷 단춧구멍에 쏙 끼워지자, 똑단이는 처음으로 붙잡지 않고도 안심할 수 있었어요.\n\"이번엔 내가 붙잡지 않아도, 꼭 맞는 자리가 나를 안아 줬어!\""
- **열쇠고리 여행자 찰칵이** (`chal_keyring`, 잠김 → 열림; '찰칵이와 원정을 10번 다녀오면 받아요'): "찰칵이는 맞는 문을 찾느라 늘 같은 골목만 맴돌았어요.\n원정에서 돌아올 때마다 친구들이 작은 열쇠고리를 하나씩 달아 주었지요. 정류장, 장난감 가게, 별빛 세탁소의 열쇠고리였어요.\n어느새 찰칵이의 허리춤은 다녀온 길들로 짤랑거렸어요.\n\"꼭 맞는 문이 하나뿐일 필요는 없었네. 다녀온 길마다 문이 열려 있었어!\""
- **생일 초 몽당이** (`mong_birthday`, 사라짐 → 축하; '이벤트 상점에서 별빛 사탕 150개로 바꿔요'): "몽당이는 녹아 작아질 때마다 사라질까 봐 조용해졌어요.\n그런 몽당이에게 친구들이 별빛 사탕으로 만든 케이크 모자를 씌워 주었어요. \"오늘은 네가 작아진 만큼 축하하는 날이야.\"\n후 불어도 꺼지지 않는 작은 불꽃이 몽당이 머리 위에서 춤을 췄어요.\n\"작아진 만큼, 누군가의 소원이 이루어졌던 거구나.\""
- **비 갠 날의 우비 우두커니** (`udu_raincoat`, 기다림 → 마중; '이번 주의 손님을 처음 「추억 가득」으로 고쳐 주면 받아요'): "맑은 날이면 우두커니는 쓸모없다며 문가에만 서 있었어요.\n손님을 끝까지 고쳐 준 밤, 손님이 노란 우비 한 벌을 남기고 떠났어요. \"비가 그친 뒤에도 누군가는 마중을 나가야 하니까요.\"\n우비를 입은 우두커니는 이제 비가 그친 골목으로 먼저 걸어 나가요.\n\"기다리기만 하지 않을래. 오늘은 내가 먼저 데리러 갈게.\""
- **무대 화장 루즈리** (`rouge_stage`, 감춤 → 드러냄; '스테이지 4-10에 닿으면 받아요'): "루즈리는 서랍 속에서 오래 뚜껑을 닫고 있었어요. 드러나면 놀림받을까 봐서요.\n큰그림자가 물러간 밤, 골목 친구들이 작은 무대를 세우고 루즈리를 불렀어요.\n반짝이는 무대 화장을 한 루즈리가 조명 아래 서자, 그림자들이 오히려 한 걸음 물러섰어요.\n\"숨는 것보다 보여 주는 게 더 용기가 필요해. 그래서 더 빛나는 거야!\""
- **자장가 라라벨** (`lala_lullaby`, 두려움 → 위로; '이벤트 상점에서 별빛 사탕 300개로 바꿔요'): "라라벨은 마지막 음 앞에서 늘 멈췄어요. 노래가 끝나면 모두 잠들어 떠날까 봐서요.\n별이 머문 밤, 친구들이 라라벨에게 별무늬 잠옷 리본을 달아 주었어요. \"끝까지 치지 않아도 돼. 자장가는 잠들 때까지만 불러도 되는걸.\"\n라라벨은 처음으로 노래를 멈추는 게 무섭지 않았어요. 다음 밤에 다시 부르면 되니까요.\n\"쉿… 오늘 노래는 여기까지. 내일 밤에 이어서 불러 줄게.\""
- Fitting room: first-open toast '의상을 누르면 미리 입어 볼 수 있어요' · '미리 입어 보는 중' (Caption 22 on the portrait) · '입히기' · '되돌리기' · locked '{의상 이름} · {acquisition}'.

### A.7 Unlock cards and pacing toasts
- Outings card: title '새 기능이 열렸어요!' · '밤 나들이 지도가 생겼어요. 골목 심부름으로 단추와 재료를 모아요' · line 2 '2-6을 지나면 이번 주의 손님이 찾아와요'.
- LevelBreak: '한 땀 돌파로 레벨 한계를 넘을 수 있어요'. Techniques: '모인 단추로 수선 솜씨를 갈고닦아요. 끝없이 강해질 수 있어요'. DailyRounds: '이제 매일 할 일을 한 번에 끝낼 수 있어요'. AutoChallenge (SPEC4 row, changed): '이제 보스 도전과 원정 다시 보내기를 친구들이 알아서 해요'.
- WeeklyGuest (no card): NEW pill + the first-entry explainer; the hub card's detail on its first day '이번 주 손님이 처음 찾아왔어요'.
- Locked toast with only the play floor missing: '조금만 더 수선점을 돌보면 열려요'.

---

## Appendix B: Korean copy and content, wave B (R2P-5)

### B.1 Titles (37; `TitleDef`, cosmetic only)
| id | 칭호 | Source |
|---|---|---|
| `title_owner` | 수선점 주인 | `ftue.done` (backfilled on the v5 load) |
| `title_ach_stage` | 골목의 등불지기 | Q-04 `ach_stage` final tier |
| `title_ach_kill` | 먼지 털이 장인 | `ach_kill` |
| `title_ach_lamp` | 불빛을 지키는 손 | `ach_lamp` |
| `title_ach_echo` | 잔향을 부르는 목소리 | `ach_echo` |
| `title_ach_friend` | 모두의 수선공 | `ach_friend` |
| `title_ach_form` | 진화 기억도의 주인 | `ach_form` |
| `title_ach_expedition` | 먼 길 다녀온 수선공 | `ach_expedition` |
| `title_ach_dungeon` | 잔향이 던전 탐험가 | `ach_dungeon` |
| `title_ach_gacha` | 잃어버린 친구를 찾는 사람 | `ach_gacha` |
| `title_ach_synergy` | 어울림을 아는 수선공 | `ach_synergy` |
| `title_ach_shop` | 큰 수선점 주인 | `ach_shop` |
| `title_ach_levelsum` | 친구들의 버팀목 | `ach_levelsum` |
| `title_ach_journal` | 꼼꼼한 일지 작가 | `ach_journal` |
| `title_ach_attend` | 매일 오는 손님 | `ach_attend` |
| `title_ach_costume` | 옷장 지기 | `ach_costume` |
| `title_ach_affinity` | 마음을 꿰맨 수선공 | `ach_affinity` |
| `title_ach_hidden` | 숨은 이야기 수집가 | `ach_hidden` (all 5) |
| `title_ach_errand` | 밤 골목 심부름꾼 | `ach_errand` |
| `title_ach_tower` | 분실물 보관소지기 | `ach_tower` |
| `title_ach_guest` | 단골 수선점 | `ach_guest` |
| `title_ach_walk` | 밤길의 친구 | `ach_walk` (depth 10) |
| `title_ach_technique` | 수선 비법 달인 | `ach_technique` |
| `title_ach_capbreak` | 한 땀 한 땀 | `ach_capbreak` |
| `title_ach_keepsake` | 기념품 선반 주인 | `ach_keepsake` |
| `title_ach_firefly` | 반딧불 친구 | `ach_firefly` |
| `title_tower_20` / `_40` / `_60` | 보관소 견습생 / 보관소 정리꾼 / 보관소 지배인 | tower floors 20 / 40 / 60 |
| `title_guest_bloom4` | 손님맞이 장인 | 4 weeks finished at 추억 가득 (`GuestTier "bloomWeeks"` ≥ 4) |
| `title_chapter5` | 밤 골목 지킴이 | first clear of 5-10 |
| `title_fest_a` … `_e` | 실 엮는 손 · 친구를 찾는 눈 · 불빛 축제의 주인공 · 원정 축제의 주인공 · 나들이 축제의 주인공 | all 4 rounds of that theme, first time |
| `title_sprout` (opt) | 새싹 수선공 | SPEC4 NEW-01 150 chest |

### B.2 Profile (R2-20)
- Popup '수선점 명함'; rows '대표 친구' [바꾸기 ›] · '수선점 이름' [이름 바꾸기] ('무료' / '단추 60분') · '칭호' [칭호 고르기 ›]; input placeholder '수선점 이름 (8자까지)'; error '이 이름은 쓸 수 없어요'; saved toast '이름을 바꿨어요: 「{이름}」'; naming bubble '수선점 이름을 지어 줄까요? ›'; title picker '달기' · '단 칭호' · new title toast '새 칭호를 얻었어요: {이름}'.
- Partner lines (`PartnerLineDef`; return popup opener / notification signature):
  - 짝짝이: '다녀왔어? 오늘도 가족이 늘었어!' / '짝짝이: 바구니가 가득 찼어! 같이 열어 볼래?'
  - 똑단이: '꼭 붙잡고 기다렸어!' / '똑단이: 놓치기 전에 와 줄래?'
  - 찰칵이: '찰칵! 문 열어 두고 기다렸어' / '찰칵이: 원정 간 친구들이 돌아왔어!'
  - 몽당이: '불 켜 두고 기다렸어. 따뜻하지?' / '몽당이: 등불이 꺼지기 전에 들러 줘'
  - 우두커니: '기다리는 건 자신 있어. 어서 와' / '우두커니: 비가 와요! 잠깐 들러 주실래요?'
  - 루즈리: '오늘 색 어때? 보러 와 줘서 고마워!' / '루즈리: 오늘도 반짝이러 와 줄래?'
  - 라라벨: '자장가를 반쯤 부르고 기다렸어' / '라라벨: 노래가 기다리고 있어요'

### B.3 등불 길 (R2-10)
**Charms (40; value / tier-2 value; trait; rarity; icon `Icons/skill_*`):**
| id | 부적 | Effect | Trait | Rarity |
|---|---|---|---|---|
| `b_shield` | 보호막 실 | 보호막 +20% / +32% | 천 | common |
| `b_attack` | 반짝이는 바늘 | 공격 +15% / +24% | 금속 | common |
| `b_regen` | 잔향 샘 | 잔향 회복 +20% / +32% | 밤 | common |
| `b_cost` | 가벼운 발걸음 | 소환 비용 −1 / −2 | 소리 | rare |
| `b_life` | 오래가는 빛 | 잔향이 머무는 시간 +25% / +40% | 빛 | common |
| `b_crit` | 별빛 눈 | 치명 확률 +8% / +13% | 치장 | common |
| `b_boss` | 큰그림자 사냥 | 보스 피해 +20% / +32% | 밤 | common |
| `b_start` | 아침 잔향 | 시작 잔향 +15 / +24 | 소리 | common |
| `b_speed` | 바람 발 | 잔향이 이동 속도 +15% / +24% | 비 | common |
| `b_heal` | 포근한 손길 | 회복량 +25% / +40% | 천 | common |
| `b_stun` | 꼭 붙잡기 | 기절 시간 +25% / +40% | 금속 | common |
| `b_slow` | 엉킨 실 | 감속 효과 +20% / +32% | 비 | common |
| `w_shophp` | 덧댄 지붕 | 수선점 내구도 +15% / +24% | 천 | common |
| `w_reduce` | 두꺼운 커튼 | 수선점 피해 감소 +6% / +10% | 천 | common |
| `w_regenshop` | 꿰매는 손 | 수선점 초당 회복 +0.15% / +0.24% | 천 | common |
| `w_aspd` | 재빠른 손끝 | 공격 속도 +10% / +16% | 금속 | common |
| `w_range` | 긴 바늘 | 사거리 +10% / +16% | 금속 | common |
| `w_lamp` | 심지 다듬기 | 불빛 충전 +20% / +32% | 빛 | common |
| `w_lampdmg` | 환한 등불 | 불 밝히기 피해 +25% / +40% | 빛 | common |
| `w_shield2` | 이중 보호막 | 보호막 +15% / +24% | 빛 | common |
| `w_cd` | 태엽 감기 | 스킬 재사용 −8% / −13% | 소리 | common |
| `w_echohp` | 밤 망토 | 친구 체력 +12% / +19% | 밤 | common |
| `w_rain` | 젖은 땅 | 감속 효과 +15% / +24% | 비 | common |
| `w_wind` | 뒷바람 | 잔향이 이동 속도 +12% / +19% | 비 | common |
| `w_critdmg` | 반짝 실밥 | 치명 피해 +20% / +32% | 치장 | common |
| `w_charm` | 리본 매듭 | 치명 확률 +6% / +10% | 치장 | common |
| `r_attack2` | 은바늘 | 공격 +25% / +40% | 금속 | rare |
| `r_aspd2` | 태엽 심장 | 공격 속도 +18% / +29% | 금속 | rare |
| `r_heal2` | 약손 | 회복량 +40% / +64% | 천 | rare |
| `r_life2` | 별빛 실 | 잔향이 머무는 시간 +40% / +64% | 빛 | rare |
| `r_lamp2` | 보름달 심지 | 불빛 충전 +35% / +56% | 빛 | rare |
| `r_start2` | 새벽 종소리 | 시작 잔향 +25 / +40 | 소리 | rare |
| `r_stun2` | 꽉 묶은 매듭 | 기절 시간 +40% / +64% | 소리 | rare |
| `r_boss2` | 큰 다리미 | 보스 피해 +30% / +48% | 밤 | rare |
| `r_regen2` | 밤샘 샘물 | 잔향 회복 +35% / +56% | 밤 | rare |
| `r_crit2` | 작은 손거울 | 치명 확률 +12% / +19% | 치장 | rare |
| `d_rage` | 그림자 바늘 | 공격 +50% / +80%, 수선점 내구도 −20% | 밤 | dark |
| `d_greed` | 그림자 주머니 | 치명 피해 +60% / +96%, 회복량 −30% | 치장 | dark |
| `d_haste` | 그림자 바람 | 공격 속도 +30% / +48%, 잔향이 머무는 시간 −30% | 비 | dark |
| `d_bell` | 그림자 종 | 스킬 재사용 −20% / −32%, 불빛 충전 −40% | 소리 | dark |
**Crests (count = Σ tiers of that trait's charms; 2 / 4 / 6):**
- 천: 친구 체력 +10% · 수선점 내구도 +15% · 수선점이 한 번 30%로 되살아나요
- 금속: 공격 속도 +8% · 치명 피해 +25% · 보스 피해 +40%
- 빛: 불빛 충전 +15% · 불 밝히기 피해 +40% · 불을 밝히면 잔향 +20
- 소리: 스킬 재사용 −6% · 시작 잔향 +20 · 잔향이 소환 비용 −1
- 밤: 잔향 회복 +15% · 잔향이 머무는 시간 +25% · 보스가 나오면 20초 동안 공격 +30%
- 비: 감속 효과 +15% · 적 이동 속도 −10% · 비가 내려요 (비 오는 밤 기믹)
- 치장: 치명 확률 +5% · 치명 피해 +30% · 치명타마다 잔향 +1
**Depths (one rule each; rule ids in C.6):** 1 단단한 먼지 · 2 재촉하는 바람 · 3 날카로운 녹 · 4 몰려드는 밤 · 5 꺼진 등불 · 6 정예 그림자 · 7 졸음 길 · 8 잔향 가뭄 · 9 낡은 벽 · 10 깊은 밤 (적 체력 +50% · 적 피해 +30%).
**Talents (등불 나무):** 다시 고르기 +1 · 부적 네 개 중에서 · 첫 부적은 귀한 것으로 · 처음 내구도 +10% · 등불 하나 더 · 길가 상자 +20% · 빌릴 친구 둘 중에서 · 문장 하나 더 쉽게 ('걷기 전에 고른 특성의 문장이 하나 덜 모아도 켜져요').
**Lenders:** 우산가게 할머니 '비 오는 날은 내게 맡겨요' · 시계방 쌍둥이 '오늘 밤만 함께해요' · 빵집 고양이 꼬미 '꼬미가 빵 냄새를 맡고 따라왔대요'.
**Strings:** nodes '전투' · '부적 고르기' · '길가 상자' · '엘리트' · '가로등 쉼터' · '그림자 상인' · '보스'; rest '가로등 아래에서 쉬었어요. 내구도 +30%'; chest '길가 상자에서 재료를 찾았어요'; merchant '그림자 상인이 손을 내밀어요. 조금 아플지도 몰라요.' / '내구도 20%를 주고 부적 받기' / '그냥 지나가기'; borrowed '{이웃}{이/가} {이름}{을/를} 빌려줬어요: "{한 줄}"'; codex clue '??? — 등불 길에서 만나 봤어요'; result '{i}칸까지 걸었어요' / '등불 길을 끝까지 걸었어요!'; '이번 주 선물 걷기 {k}/10'; weekly chest '열 번 걸은 선물'; depth first clear '깊이 {d}{을/를} 처음 걸었어요! 등불 나무에 불이 하나 켜졌어요'; lanterns '등불 {n}/3 · 다음 등불 {h:mm}' · '보관 {b}'; card '등불을 하나씩 켜며 걷는 밤길이 열렸어요. 매번 다른 길이에요'; result link '{이름} 찾으러 가기 ›'.

### B.4 밤의 작업대 (19 nodes; `WorkbenchNodeDef`)
| Branch | Node (id) | Effect per level | Max | Tier |
|---|---|---|---|---|
| 실 | 잔향 지속 (`wb_echo_life`) | 잔향이 머무는 시간 +3% | 10 | T1 |
| 실 | 잔향 회복 (`wb_echo_regen`) | 잔향 회복 +2% | 10 | T1 |
| 실 | 친구 체력 (`wb_hp`) | 친구 체력 +2% | 10 | T2 |
| 실 | 시작 잔향 (`wb_start_res`) | 시작 잔향 +2 | 5 | T2 |
| 실 | 잔향이 이동 (`wb_echo_speed`) | 잔향이 이동 속도 +3% (S5-17) | 5 | T3 |
| 실 | 소환 비용 (`wb_echo_cost`) | 잔향이 소환 비용 −1 | 1 | T4 |
| 골무 | 내구도 (`wb_shop_hp`) | 수선점 내구도 +1% | 10 | T1 |
| 골무 | 피해 감소 (`wb_shop_reduce`) | 수선점 피해 감소 +0.5% | 10 | T2 |
| 골무 | 방위 받침 · 위쪽 / 왼쪽 위 / 왼쪽 아래 / 오른쪽 아래 / 오른쪽 위 (`wb_pad_0..4`) | 그 자리 친구 사거리 +2% | 5 each | T2 |
| 골무 | 수선 회복 (`wb_shop_regen`) | 수선점 초당 회복 +0.1% | 5 | T3 |
| 바구니 | 바구니 적립 (`wb_basket_rate`) | 바구니 단추·재료 +2% | 5 | T1 |
| 바구니 | 원정 재료 (`wb_exp_loot`) | 원정 재료 +5% | 4 | T2 |
| 바구니 | 바구니 상한 (`wb_basket_cap`) | 바구니 단추·재료 상한 +1시간 | 2 | T3 |
| 바구니 | 심부름 입장권 보관 (`wb_errand_cap`) | 심부름 입장권 보관 +2장 | 1 | T3 |
| 바구니 | 작업대 속도 (`wb_speed`) | 바느질 시간 −2% | 10 | T4 |
Strings: room '재봉틀 방'; slot '바느질 중: {노드} Lv.{n} · {시간}' / '빈 자리'; '빨리 끝내기' ('빠른 수선권 1 · 2시간'); gate '{n}레벨 더 배우면 다음 칸이 열려요'; '바느질 시작'; done '{노드} Lv.{n} 바느질을 마쳤어요'; slot unlock '작업대 자리가 하나 늘었어요'; card '밤새 바느질해 두면 수선점이 조금씩 단단해져요'; tampered '기기 시간이 맞춰질 때까지 바느질을 잠시 멈춰요'.

### B.5 기념품 (32; placed effects, setup-time conditions)
| id | 기념품 | Source | Placed effect | Hint (unfound) |
|---|---|---|---|---|
| `k_jjak` | 짝 잃은 단추 구멍 | 짝짝이 호감도 Lv.6 | 짝짝이가 있으면 친구 체력 +10% | '{이름}{와/과} 더 친해지면 받아요' |
| `k_ttok` | 끝까지 붙잡은 실 한 올 | 똑단이 Lv.6 | 똑단이가 있으면 기절 시간 +20% | (same pattern) |
| `k_chal` | 맞지 않던 자물쇠 | 찰칵이 Lv.6 | 찰칵이가 있으면 치명 확률 +5% | |
| `k_mong` | 마지막 촛농 한 방울 | 몽당이 Lv.6 | 몽당이가 있으면 회복량 +12% | |
| `k_udu` | 마른 날의 우산끈 | 우두커니 Lv.6 | 비가 오지 않는 스테이지에서 수선점 내구도 +8% | |
| `k_rouge` | 금빛 뚜껑 테 | 루즈리 Lv.6 | 루즈리가 있으면 공격 속도 +5% | |
| `k_lala` | 빠진 일곱째 음 | 라라벨 Lv.6 | 라라벨이 있으면 스킬 재사용 −6% | |
| `s_1_1` | 여행 엽서 | 원정 1-1 잃어버린 골목 | 원정 보상 +10% | '잃어버린 골목에 열 번 다녀오면 꼭 찾아요' |
| `s_1_2` | 빗방울 유리병 | 원정 1-2 젖은 버스 정류장 | 비 스테이지에서 공격 +12% | (same pattern, per zone) |
| `s_1_3` | 꺼진 성냥 | 원정 1-3 꺼진 가로등 아래 | 정전 스테이지에서 공격 +12% | |
| `s_1_4` | 태엽 열쇠 | 원정 1-4 닫힌 장난감 가게 | 바람 스테이지에서 공격 속도 +8% | |
| `s_2_1` | 반쪽 이어폰 | 원정 2-1 비 오는 광장 | 소리 특성 친구가 둘 이상이면 스킬 재사용 −6% | |
| `s_2_2` | 먼지 털이 깃털 | 원정 2-2 먼지 쌓인 다락방 | 먼지 폭풍 스테이지에서 수선점 피해 감소 +10% | |
| `s_2_3` | 이름표 없는 열쇠고리 | 원정 2-3 분실물 보관소 | 보관소 오르기에서 치명 피해 +20% | |
| `s_2_4` | 별빛 빨래집게 | 원정 2-4 별빛 세탁소 | 보름달 스테이지에서 잔향이 머무는 시간 +20% | |
| `t_20` | 할머니의 골무 | 보관소 20층 | 챕터 보스 스테이지에서 보스 피해 +10% | '보관소 20층에 있어요' |
| `t_40` | 빛바랜 영수증 | 보관소 40층 | 심부름에서 공격 +10% | |
| `t_60` | 멈춘 손목시계 | 보관소 60층 | 심부름 시간 +5초 | |
| `t_80` | 보관소 맨 위 열쇠 | 보관소 80층 | 보관소 오르기에서 공격 +12% | |
| `g_wardrobe` | 옷장 손잡이 | 낡은 옷장 할아버지 (새것처럼+) | 천 특성 친구가 셋 이상이면 수선점 내구도 +8% | '{손님}{을/를} 새것처럼 고쳐 주면 받아요' |
| `g_clock` | 괘종시계 추 | 멈춘 괘종시계 | 이번 주의 손님 전투에서 공격 속도 +8% | |
| `g_mirror` | 거울 조각 | 금 간 거울 | 치장 특성 친구가 둘 이상이면 치명 확률 +5% | |
| `g_bell` | 자전거 종 | 녹슨 자전거 종 | 비 스테이지에서 공격 속도 +8% | |
| `g_birdcage` | 새장 문고리 | 빈 새장 | 밤 9시부터 새벽 5시 사이에 잔향 회복 +15% | |
| `g_bear` | 곰인형 단추 눈 | 구멍 난 곰인형 | 천 특성 친구가 둘 이상이면 회복량 +15% | |
| `g_map` | 접힌 지도 조각 | 바랜 지도 | 등불 길에서 공격 +10% | |
| `g_lampshade` | 램프갓 술 | 깨진 램프갓 | 정전 스테이지에서 불 밝히기 피해 +25% | |
| `g_kite` | 연 꼬리 리본 | 찢어진 연 | 바람 스테이지에서 잔향이 이동 속도 +20% | |
| `g_watch` | 회중시계 태엽 | 고장 난 회중시계 | 이번 주의 손님 전투에서 공격 +10% | |
| `g_piano` | 피아노 건반 하나 | 낡은 피아노 의자 | 소리 특성 친구가 둘 이상이면 시작 잔향 +10 | |
| `g_ornament` | 별 모양 장식 | 잃어버린 트리 장식 | 축제 스테이지에서 치명 피해 +25% | |
| `a_28` | 닳은 출석 도장 | 정규 출석 28일째 | 스테이지 클리어 단추 +10% | '출석 도장 28일째에 받아요' |
Strings: room '기념품 선반'; locked slot '광택 {n}에 열려요'; '올려 두기' · '내리기' · '닦기'; shine '광택 {n}/5'; burst '그동안 모인 기념품 {n}개가 선반에 도착했어요'; new '새 기념품: {이름}'; card '모은 기념품을 선반에 올려 두면 친구들이 힘을 내요'.

### B.6 수선 축제 주간 · 보름달 밤
| Theme (id, index) | 이름 · desc | Points (counter × points per unit) | Round target |
|---|---|---|---|
| A (`fest_thread`, 0) | 실 엮는 주 · '진화하고 공명하고 재료를 쓸수록 실이 엮여요' | EvolutionCraft ×100 · ResonanceUp ×50 · MaterialSpent per 10 → 1 | 100 |
| B (`fest_friends`, 1) | 친구 찾는 주 · '잃어버린 친구를 찾을수록 점수가 쌓여요' | GachaPull ×1 | 10 |
| C (`fest_lights`, 2) | 불빛 주간 · '불을 밝히고 보스를 물리칠수록 축제가 밝아져요' | LampLit ×1 · BossKill ×3 | 350 |
| D (`fest_roads`, 3) | 원정 주간 · '원정을 보내고 받을수록 점수가 쌓여요' | ExpeditionClaim ×5 · ExpeditionStart ×2 | 90 |
| E (`fest_outings`, 4) | 나들이 주간 · '심부름 · 보관소 · 손님 도전으로 점수를 모아요' | ErrandRun ×5 · TowerFloorClear ×10 · GuestAttempt ×3 | 75 |
Strings: strip '{k}번째 마당 · {p}/{target}' · '받기'; round toast '{테마} {k}번째 마당을 마쳤어요!'; all '{테마}{을/를} 모두 마쳤어요! 칭호 「{칭호}」'; effects chip '수선 축제 주간 · {테마}'; start card '{테마}{이/가} 시작됐어요!' (SPEC4 EventStartCard).
Moon: chip '보름달까지 D-{n}' · '오늘은 보름달 밤이에요 · 별빛 사탕 ×1.5' · '보름달이 떴어요 · 별빛 사탕 ×2' (outside days 1–14) · inside days 1–14 '보름달 밤 · 별빛 산책과 함께 별빛 사탕 ×2'; desc '보름달이 뜨는 사흘 동안 보름달 스테이지가 늘고, 바구니에 단추와 재료가 조금 더 쌓여요'.

### B.7 New forms (R2-36b step 1; `Evolve(...)` rows, tier 2)
| Form | Recipe (materials) | Conditions | Skill numbers | Description (2 lines) |
|---|---|---|---|---|
| 요요 단추 `ttok_yoyo` (from `ttok_spinner`) | 수선 천 40 · 기억의 실 12 · 오르골 태엽 4 · 달빛 조각 2 | LineageLevel 110 · EchoDeployCount 80 | CD 6 s: Pull the nearest enemy within 2.2 + Stun 0.4 s + Damage 120% | "팽이처럼 돌던 단추가 실을 매달고 요요가 되었어요. 휙 던졌다가 끌어당기면 놓친 적도 다시 데려와요.\n잔향이로 나오면 가장 가까운 적을 실로 끌어와 꼭 붙잡아요." |
| 마음 열쇠 찰칵이 `chal_heartkey` (from `chal_master`) | 레이스 조각 30 · 기억의 실 12 · 오르골 태엽 4 · 달빛 조각 2 | LineageLevel 110 · ExpeditionCount 40 | CD 8 s: Heal shop 4% + Cleanse allies near self r 1.2 | "마스터키가 된 찰칵이는 문보다 마음을 여는 법을 배웠어요. 하트 모양 열쇠 머리가 따스하게 빛나요.\n잔향이로 나오면 수선점을 고치고 친구들의 나쁜 기운을 풀어 줘요." |
| 등대 몽당이 `mong_lighthouse` (from `mong_lantern`) | 촛농 방울 40 · 기억의 실 12 · 오르골 태엽 4 · 달빛 조각 2 | LineageLevel 110 · GuardianKillCount 3,000 | CD 7 s: Lane damage 220% + Reveal 3 s | "초롱이 된 몽당이가 바닷가 등대만큼 자랐어요. 멀리 비추는 불빛이 길 잃은 친구를 불러요.\n지킴이로 서면 한 방향의 적을 길게 꿰뚫고 숨은 그림자를 드러내요." |
| 천둥 우산 우두커니 `udu_thunder` (from `udu_storm`) | 우산살 30 · 기억의 실 12 · 오르골 태엽 4 · 달빛 조각 2 | LineageLevel 110 · StageReached 200 | CD 6 s: Damage 160% near target r 0.8 + Slow 30% 2 s | "폭풍 속에서 버티던 우산이 천둥을 품었어요. 우산을 쾅 내려치면 골목이 울려요.\n잔향이로 나오면 주변 적을 내려치고 느리게 해요." |
| 콤팩트 거울 루즈리 `rouge_compact` (from `rouge_gloss`) | 리본 조각 40 · 레이스 조각 12 · 오르골 태엽 4 · 달빛 조각 2 | LineageLevel 110 · DungeonFloor 30 | CD 9 s: Shield self 20% + Mark the last attacker 4 s | "글로스 루즈리가 작은 콤팩트 거울을 열었어요. 거울에 비친 그림자는 저도 모르게 움찔해요.\n지킴이로 서면 스스로 보호막을 두르고, 때린 적에게 표식을 남겨요." |
| 회전목마 라라벨 `lala_carousel` (from `lala_waltz`) | 여행 가방 조각 20 · 기억의 실 12 · 오르골 태엽 4 · 달빛 조각 2 | LineageLevel 110 · EchoDeployCount 80 | CD 8 s: Stun enemies in range 0.6 s + AttackRamp +4% per cast (max 5) | "왈츠를 추던 라라벨의 오르골 위로 작은 회전목마가 돌기 시작했어요. 마지막 음은 아직이지만 회전목마는 멈추지 않아요.\n잔향이로 나오면 주변 적을 빙글빙글 어지럽게 하고, 돌수록 힘이 붙어요." |
Hint for the 진화 기억도 clue: '한 땀 돌파로 레벨 110을 넘으면 단서가 보여요'.

### B.8 Rider option B (B-opt; 공명 9 only, free switch outside battle)
짝짝이 '함께라면 괜찮아': 양말 토큰이 하나라도 있으면 공격 +12% · 똑단이 '꽉 붙든 손': 기절한 적에게 피해 +25% · 찰칵이 '맞는 열쇠 찾기': 표식 대상에게 치명 피해 +20% · 몽당이 '아껴 쓰는 불': 잔향이 머무는 시간 +25% · 우두커니 '빗속의 영웅': 비 스테이지에서 공격 +15% 더 · 루즈리 '당당한 무대': 버프를 받은 친구 치명 확률 +6% · 라라벨 '다음 노래 약속': 수선점이 처음 30% 아래로 내려가면 잔향 +30. UI: '버릇 고르기' two chips on the 공명 9 line; toast '{이름}의 버릇을 바꿨어요'.

### B.9 Tower, lantern, market, merge, route marks, place 2
- Tower: shelf names '우산 칸' · '장갑 칸' · '열쇠 칸' · '단추 칸' · '양말 칸' ('{f}층 · {칸}'); trial chip '시험 칸 · {규칙}'; '도전' · '자동으로 오르기' · '자동 재도전'; progress '보관소를 정리하는 중… {f}층'; auto toast '보관소 {f}층을 정리했어요!'; header '보관소 {h}층 · 감사장 +{p}% · 다음 상자까지 {n}층'; 감사장 toast '보관소 감사장을 받았어요. 친구들 공격력 +0.5%'; card '분실물 보관소의 선반을 한 층씩 올라가요. 입장권이 필요 없어요'.
- Lantern: tooltip '반딧불 등롱 · 적을 물리치면 차올라요'; notes '단추 쪽지' · '재료 쪽지' · '사탕 쪽지' · '기억 쪽지'; tier '반짝 {n}'; popup '반딧불 쪽지를 하나 골라요' · hint '고르지 않은 쪽지는 다음에 더 커져요'; limit '오늘은 등롱을 모두 열었어요. 새벽 5시에 다시 열 수 있어요'; moth '그림자 나방이었어요! 선물이 두 배 · 다음 무리에 나방이 섞여 와요'; variants '떠다니는 종이배' · '바람에 날린 손수건' · '별똥별' · '비눗방울'.
- Market: '주말 골목 장터' · '월요일 새벽 5시까지' · '다 팔렸어요' · '사기' · first open '주말에만 열리는 골목 장터가 섰어요'.
- Merge: tab '엮기' · '오늘 {k}/{limit}' · '엮기' · '모두 엮기' · preview '{재료} {n} → {결과} {m}' · toggle '바구니를 받을 때 자동으로 엮기' · none '지금은 엮을 만큼 남는 재료가 없어요' · done '재료 {n}개를 엮었어요'.
- Route marks: tooltip '{n}번 더 다녀오면 가로등이 하나 더 켜져요' · 5 marks '단골 길 · 받으면 바로 다시 보내요' · toast '{구역}에 가로등이 하나 더 켜졌어요 · 보상 +8%'.
- Place 2: '야시장 골목' · rule chip '야시장 · 무리 +20%' · boss '흔들 종의 그림자' · hint '종이 울리면 적들이 빨라져요. 소리 친구가 종소리를 덮어 줘요' · arrival notice (first stage of chapter 6) '야시장 골목에 들어섰어요' · codex texts: 그림자 까치 '단추를 물고 레인을 건너뛰어요. 저격으로 길목을 지켜요.' · 빨래 바구니 둥지 '바구니에서 먼지가 끝없이 나와요. 바구니부터 비워요.' · 흔들 종의 그림자 '종소리로 적들을 재촉해요. 소리 친구가 있으면 덜 울려요.' · 먼지 왕 '쓰러져도 더 단단하게 다시 뭉쳐요.'

---

## Appendix C: number tables (R2P-5)

### C.1 Errands
- `s(L) = requiredStage + 5(L−1)` · `LevelForStage(S) = ⌊(S − requiredStage)/5⌋ + 1` · `MaxSelectable = max(highestLevel + 1, LevelForStage(highestStage) − 2)`.
- Errand 1 at L = 1 / 10 / 50 / 100: 단추 22 / 40 / 120 / 220분치 × p. Errand 2 per full clear at L = 10: 수선 천 16 · 리본 16 · 촛농 9 · 레이스 3 · 우산살 2; at L = 50: 56 · 56 · 29 · 11 · 7.
- Milestones (every 10 of `highestLevel`): 별조각 10 + 빠른 수선권 1 (errand 1) / 레이스 조각 10 (errand 2) / 기억의 실 3 (errand 3).

### C.2 Guest weekly tiers and band rewards (all bands also pay 단추 60분치)
| Tier | Band · step | Stitches ≥ | 별조각 | Other |
|---|---|---|---|---|
| 1–5 | 실금 1–5 | 15 / 35 / 60 / 90 / 125 | 10 / 12 / 15 / 17 / 20 | 수선 천 20 |
| 6–10 | 반짝 1–5 | 165 / 210 / 260 / 315 / 375 | 30 / 35 / 40 / 45 / 50 | 선택 기억편 3 / 3 / 4 / 4 / 5 · 레이스 조각 5 |
| 11–15 | 새것처럼 1–5 | 440 / 510 / 585 / 665 / 750 | 60 / 65 / 70 / 75 / 80 | 선택 기억편 8 / 8 / 9 / 9 / 10 · 기억의 실 3 · memento |
| 16–25 | 추억 가득 1–10 | 840 / 930 / 1,020 / 1,110 / 1,200 / 1,290 / 1,380 / 1,470 / 1,540 / 1,600 | 90 / 92 / 94 / 96 / 98 / 100 / 102 / 104 / 107 / 110 | 선택 기억편 11 / 11 / 12 / 12 / 12 / 13 / 13 / 14 / 14 / 15 · 달빛 조각 1 · memento (first 추억 가득 ever: + `udu_raincoat`) |
A repeat memento (the guest returns after 12 weeks and finishes at 새것처럼+ again) is +1 shine (B2).

### C.3 Technique icons
`tq_needle` skill_thread_stitch · `tq_stitch` skill_patch_heal · `tq_hands` skill_spin_button · `tq_knot` skill_slow_yarn · `tq_wick` skill_candle_burst · `tq_hardknot` skill_split_yarn · `tq_iron` skill_shadow_burst · `tq_double` skill_star_burst.

### C.4 Missions and records (R2 rows)
- Q-02: daily `md_errand` '밤 골목 심부름 1번' (ErrandRun 1, 20 pts, requires Errands, nav Outings) · weekly `mw_guest` '손님의 그림자 5번 떼어 내기' (GuestAttempt 5, 20, WeeklyGuest) · `mw_tower` '보관소 3층 오르기' (TowerFloorClear 3, 20, Tower; B1) · `mw_walk` '등불 길 3번' (WalkStart 3, 20, NightWalk; B2). Totals with everything unlocked: daily 190, weekly 200; chest thresholds unchanged (more slack). Rows stay hidden while locked (A4 rule).
- Q-04 (`AchievementDef`, 4 tiers each, 별조각 10 / 20 / 30 / 40, final tier + title): `ach_errand` (A; Counter ErrandRun 10 / 50 / 200 / 500) · `ach_guest` (A; GuestTier "weeks" 1 / 4 / 12 / 26) · `ach_technique` (A; Counter TechniqueLevelUp 300 / 1,000 / 2,000 / 3,000) · `ach_capbreak` (A; CapBreaks 1 / 7 / 21 / 35) · `ach_tower` (B1; TowerFloor 20 / 60 / 120 / 160) · `ach_firefly` (B1; Counter FireflyTap 30 / 100 / 300 / 600) · `ach_walk` (B2; Counter WalkStart 10 / 50 / 200, then Flag `r2.walk.depth10`) · `ach_keepsake` (B2; Counter KeepsakeGet 5 / 15 / 25 / 32). Total 800 별조각. Titles: Appendix B.1. Category icons: errand `Enemies/enemy_magpie` (fallback `Icons/lamp`), guest `UI/stitch_mark`, technique `Icons/skill_thread_stitch`, capbreak `UI/stitch_mark`, tower `UI/tower_bg` crop, firefly `Icons/skill_firefly`, walk `Icons/lamp`, keepsake `Icons/star`.

### C.5 Cap breaks
The §5.3 table; embroidery 30 shards → +0.5% (×20).

### C.6 Side rule rows (`SideRuleDef`; id · 이름 · effect · value · group · risk)
`r_dust_1/2/3` 단단한 먼지 · EnemyHpScale 1.25 / 1.5 / 2.0 · hp · 1/2/3 · `r_rust_1/2/3` 날카로운 녹 · EnemyDamageScale 1.3 / 1.6 / 2.0 · dmg · 1/2/3 · `r_wind_1/2/3` 재촉하는 바람 · EnemySpeedScale 1.1 / 1.2 / 1.3 · speed · 1/2/3 · `r_horde_1/2` 몰려드는 밤 · WaveSizeScale 1.25 / 1.5 · wave · 1/2 · `r_elite` 정예 그림자 · EliteEvery 5 · elite · 2 · `r_wall_1/2` 낡은 벽 · ShopHpScale 0.75 / 0.5 · wall · 1/2 · `r_drought_1/2` 잔향 가뭄 · ResonanceRegenScale 0.7 / 0.4 · regen · 1/2 · `r_cost_1/2` 비싼 부름 · EchoCostDelta +1 / +2 · cost · 1/2 · `r_pads_1/2` 빈 방위 · ClosedPads 1 / 2 · pads · 1/2 · `r_nolamp` 꺼진 등불 · LampOff · lamp · 2 · `r_short` 짧은 꿈 · EchoLifetimeScale 0.6 · life · 1 · `r_noecho` 잔향이 없이 · Roster NoEchoes · roster · 3 · `r_samestyle` 같은 역할 2명까지 · Roster MaxSameStyle 2 · roster · 2 · `r_deck3` 잔향이 덱 3칸만 · Roster MaxDeckSlots 3 · roster · 2 · `r_rainout` 비 오는 정전 · ExtraModifier Rain (+ the floor's Blackout) · weather · 2 · `r_sleepy` 졸음 길 · (walk rule, applied by NightWalkSystem) · walk · 2 · `r_deep` 깊은 밤 · EnemyHpScale 1.5 + EnemyDamageScale 1.3 (two rows) · deep · 3 · `r_market` 야시장 · WaveSizeScale 1.2 · place · 1 · support (practice only): `r_door` 튼튼한 문 · ShopHpScale 1.5 · `r_needle` 든든한 바늘 · (Team AttackPercent +30 via the bias) · support · 0.
Tower trial pool (cycled by f/10): `r_pads_2` (지킴이 3명만) → `r_noecho` → `r_nolamp` → `r_samestyle` → `r_wall_2` (내구도 50%로 시작) → `r_deck3` → `r_rainout`.

### C.7 등불 길 rewards
Step 4 chest (길가 상자): 수선 천 15 + 리본 조각 10 + 레이스 조각 3 (+20% with the talent) · step 5 단추 30분치 + 수선 천 10 · step 10 리본 조각 10 + 촛농 방울 6 · step 15 기억편 1 per owned lineage in the team (≤ 7) + 기억의 실 2 + 우산살 3 · weekly chest (10 reward walks) 별조각 50 + 선택 기억편 10 · depth first clear 별조각 50 + one talent (depths 1–8; depths 9–10 별조각 50 only).

### C.8 Weekend market pool (`MarketItemDef`; slot 1 always 선택 기억편)
| id | Item | Price (list) | Limit |
|---|---|---|---|
| `m_select` | 선택 기억편 ×5 | 별조각 250 | 1 (always) |
| `m_thread` | 기억의 실 ×3 | 별조각 75 (90) | 2 |
| `m_quick` | 빠른 수선권 ×1 | 별조각 40 (60) | 2 |
| `m_double` | 두 배 수선권 ×1 | 별조각 40 (60) | 1 |
| `m_spring` | 오르골 태엽 ×1 | 별조각 120 (180) | 1 |
| `m_suitcase` | 여행 가방 조각 ×3 | 별조각 90 (135) | 1 |
| `m_rib` | 우산살 ×5 | 별조각 105 (150) | 1 |
| `m_lace` | 레이스 조각 ×10 | 단추 120분치 (180) | 2 |
| `m_cloth` | 수선 천 ×30 | 단추 60분치 (90) | 3 |
| `m_ribbon` | 리본 조각 ×30 | 단추 60분치 (90) | 3 |
| `m_wax` | 촛농 방울 ×20 | 단추 60분치 (90) | 3 |
| `m_polish` (B2) | 광택 손질 도구 ×1 (+1 shine to a chosen keepsake) | 별조각 100 | 1 (only when Keepsakes is unlocked) |
Slots 2–6 are drawn by `hash(WeekKey)` without repeats from the rest of the pool.

---

## Appendix D: decisions requested from the orchestrator / user
1. **Shell exception (§2.6):** approve the 25-line `MainTabController` hub extension (+ `UINav` events, `UIFrame` doc), or choose the popup fallback. Wave A can start either way.
2. **Art top-up:** R2 core ≈ 341 credits including the 20% reserve (R2-0 + A ≈ 96, B1 ≈ 60, B2 ≈ 179, first season costume 6), optional ≈ 225 (8 guest portraits, 16 keepsakes, dark-charm and firefly-variant icons, 3 mode-source costumes, technique icons, banners). Every asset has a fallback; B2 is the wave that really needs the art (forms, place field, keepsakes).
3. **Pacing floors (§2.8):** the PlayMinutes values (20 / 30 / 45 / 90 / 150 / 200 / 270) and the 180 s card spacing are content knobs; confirm the approach (it also spaces SPEC4's cards).
4. **Power rating repair (§4.3):** it changes SPEC4 PRG-01's TeamPower formula (shop term capped at the offense term) and adds a third rec segment; confirm.
5. **Candy (merge-log Q5):** wave B adds ≈ 70 별빛 사탕/week (festival milestones, moon nights, lantern notes); recommend a weekly limit of 5 on the event shop's 여행 가방 조각 before B1 ships (content row) — confirm.
6. **Season costumes (merge-log Q3):** one costume per Q-06 season (≈ 5 credits/month for six seasons) is in the core plan; confirm the cadence.
7. **Optional B items:** rider option B (7 habits), 3 mode-source costumes (S5-19), the 8 remaining guests (cycle 12 instead of 4).
8. **For the SPEC4 owner (not R2 work):** the 14-DayKey backup reminder mail suggested in the R2 merge log; the AutoChallenge/ExpeditionBatch shared card (S5-4) is applied by R2P-5 unless SPEC4 objects.
9. **(CR4-05) 단추 N분치 follow real income** (§4.6; PHASE4-ROADMAP Q15, default yes).
10. **(CR4-07) The ceiling plan** (PHASE4-ROADMAP Q16): the ≈ch55 ceiling stays, and the six forms do not move it much. Default: post-launch drops scheduled from projection #3's measured ceiling, and SPEC6 evaluates a compounding milestone row.

---

## Critic revisions (v1.0.1, 2026-11-04, PHASE4-ROADMAP critic)
Inline edits tagged `(CR4-nn)`. The evidence and the full table are in PHASE4-ROADMAP.md, "Critic revisions". Summary of what changed here:

| Tag | Where | Change |
|---|---|---|
| CR4-01 | §0.3, §4.5, §10.2 | SPEC4's ch6+ chapter chest pays 별조각 only on chapters ending in 5 or 0 (PHASE4-ROADMAP RD-13); the R2-induced flows are named |
| CR4-02 | §4.4, §4.5 | 별조각 bands re-based on the harness measurement (week 4: 2,800–3,800, week 8: 2,600–3,600), logged per source; knobs named |
| CR4-03 | §4.5, §10.2, §10.3 | guard window = the weeks 5–8 mean; week 4 ≤ 350 |
| CR4-04 | §10.2 | SPEC4's lifetime rows count SPEC4 ids only; Q-04 R2 row ≤ 1,000 |
| CR4-05 | §2.3, §2.5, §4.2, §4.4, new §4.6, §10.2 | `GoldMinutes` and the night basket's 단추 follow `IncomePerSecond` (Q15) |
| CR4-06 | §2.5, §4.4, §5.6 | 자동 편성 v2 (`AutoFormationPower`) and the `auto` projection variant |
| CR4-07 | §6.9, §10.3, App. D | the six forms are not the ceiling raise; the measured ceiling feeds Q16 |
| CR4-08 | §4.4 | every band binds at G5, G6 and G7 |
| CR4-09 | §2.6, §7.1, §7.4, §10.5 | ≡ menu rows follow the tile count, including SPEC4's row-3 tiles |
| CR4-10, CR4-11 | §2.6, §2.11, §3.1, §7.1, §8.2, §10.5, §10.6 | side battles start in MAIN; the back handler shows the abandon confirm |
| CR4-12 | §6.9 | the place sign sits in the SynergyRow modifier chip, not on the StagePanel |
| CR4-17, CR4-18 | §0.3, §8.2 | R2P-C confirm-only; the pacing core only if Q2 was declined; `BlessingPickPopup` → R2P-4b |
| CR4-19 | §2.2, §2.10 | `AchievementCategory` explicit values 17–24; GameSettings note |
| CR4-20, CR4-21 | §2.3, §2.5, §2.6, §6.9, §6.10, §9.2 | `PlaceDef.ambKey`, `AppSignals.PlaceChanged`, `amb_market`, `dustking_reform`; new skills reuse existing vfx ids |
| CR4-22 | §6.9, §9.1 | the 4:3 place-field crop, mode intro art reuse, optional new-form skill icons |
| CR4-24 | §8.4 | `PlayMinutes` seeded on migration for saves past FTUE |
