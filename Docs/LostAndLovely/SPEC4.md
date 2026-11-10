# LOST & LOVELY: Phase 4 implementation contract (SPEC4)

Version 1.1 · 2026-11-04 (v1 2026-11-03 + completeness-critic revisions, §13/§14) · author: phase-4 architect · target `D:/UnityProject/unity_idle` (Unity 6000.2.6f2, uGUI, 1080×1920 portrait)

**Relayed user question:** "다른 방치형게임시스템들을 학습한건 다적용했나?" (Have you applied all the idle-game systems you learned from other games?)
**Answer this spec gives:** not yet. The research backlog (`IDLE_RESEARCH.md` §3 + Critic additions) still has **34 open items: 15 partial and 19 missing**. UX-SPEC covers only slices of them (M13, M18, M41, M51/M57, M59 …; see §1). SPEC4 is the contract for everything else. It starts **after UX wave 2 has merged** (UX-SPEC §4.0). The standing order "learn from other idle games and apply proven systems" is satisfied once SPEC4 ships.
**Critic correction (v1.1, §14):** the 34 were undercounted. The critic pass found **10 more backlog items with open work that no package covered** (PRG-02, PRG-03, FX-01, FTUE-01, IDLE-01, F-04, FX-05, Q-01, CR-11, ATK-01/02/05 polish). Several of them were marked "done" but are not done end-to-end. It also found **4 proven genre systems that are in neither the backlog nor v1** (§13.6–§13.9). The honest count is **48 open items**, all assigned in §1 and §13. The standing order is open-ended, so §13.10 lists the next researched candidates (phase 5) and the ones rejected on purpose.

**Inputs (read before your package):** `IDLE_RESEARCH.md` (numbers and copy source; section ids F-xx/Q-xx/…), `SPEC.md` (content ids, ownership style), `SPEC3.md` (meta architecture, subscription rules), `UX-SPEC.md` (tokens §1, cross-cutting behaviour §3, wave plan §4). The code paths cited here were read on 2026-11-03 at about 23:30.

**How to read:** §0 rules → §2 contracts (everyone) → your package in §9 → the item sections it points to (§3–§8) → assets §10 → proof §11.

---

## 0. Ground rules

### 0.1 Inherited (unchanged)
- Everything in UX-SPEC §0.2 still applies: frozen coach/FTUE paths, names that code and tests look up, binder-field rule, focus rule (M01: no pause, mute or dim on `OnApplicationFocus(false)`), 해요체 copy, text floor 18 (autosize min 16), unscaled-time UI motion, no per-frame allocation, idle components disable themselves.
- SPEC/SPEC3 rules: never launch Unity in the user's open project. Compile-check every change with `python -I scratchpad/compile_check.py` and keep your own files at 0 errors. Scene rebuilds and batch tests run on a project copy (verify_proj / fx_proj). C# 9 only. Korean XML summaries on new public members. Pure C# systems plus MonoBehaviour views. Scenes come from builder code and `Binder`. Art comes through `ArtLib`/`UiArt`, which fall back to placeholders when a file is missing. Content lives in code under `Editor/Content`.
- One owner per file (§9.2). A file passes from one package to the next only at a merge point.

### 0.2 Phase-4 specific
1. **Contract first.** P4-0 lands the shared contracts (§2) and **inert stubs** before any other package starts: new systems compile, return empty or false, and **never throw**. GameBootstrap's `NotImplementedException` kill switch turns off *all* meta (`Game.IsMetaActive = false`), so a stub must never throw.
2. **Append-only enums.** `CounterKey`, `FeatureId`, `MetaPopupKind`, `MetaRewardType`, `MetaConditionType` and `DamageNumberKind` get new members only at the end, with explicit values. Saves store several of them by name or number.
3. **Save schema v4.** Every new field has a safe default and a `Sanitize` null guard. A v3 save must load, play and re-save (§2.1). Test with `scratchpad/ux_shots/autoshot_player.json` (copy it first).
4. **No new modal during FTUE.** Every new popup kind (Comeback, EventStart, StoryPage, ChapterChest, NotificationAsk, DefeatAnalysis ring) is suppressed while `Game.Tutorial.IsActive`.
5. **Locked means locked.** Systems guard feature gates themselves, not only the UI (F-03, §5.1).
6. **Art budget.** Higgsfield balance is **30.38 credits** (checked on 2026-11-03). `gpt_image_2_5` costs high 1.5 / xhigh 2.5 / max 5 per 1k image, and outpaint costs 2. Reuse `Art/**` first. Batch small icons into 2×2 sheets and slice them with PIL. Images marked "top-up" in §10.1 must not be generated until the user approves a top-up.
7. **Audio.** The Higgsfield catalog has no standalone SFX or music model (§10.2). Do not call `mirelo_text_to_audio` or `sonilo_music`: they are game-pipeline-only. `suno_music` is not in the catalog. Clips come from the §10.2 route the user picks.

---

## 1. Coverage matrix (what is left and where it goes)

Status comes from the orchestrator's audit. "UX covers" means it is already in a UX-SPEC package (wave 1–3). "SPEC4 adds" is this contract. Packages are in §9.

| ID | Pri | Status | UX covers (not repeated here) | SPEC4 adds | §/Package |
|---|---|---|---|---|---|
| ATK-06 | P0 | partial | FX quality toast guard (M01 WP-0) | perf harness + targets, T3 lineage-colour skill numbers, tier→sound map | §3.1 · P4-3b, P4-9, P4-3a |
| ATK-07 | P0 | partial | – | item 7 visual proof. Capture and crops are already done (180, critic C-14); still to do: the silhouette verdict, the in-build accessibility lines and the empty-VFX build | §3.2 · P4-9 (fallback owner) |
| ATK-08 | P1 | partial | – | yarn death, 3 shop crack states, rust/moth stickers, 2 sprite atlases | §3.3 · P4-3b, P4-9, P4-10 |
| FX-06 | P1 | partial | M41 bubble dedupe 2.5 s, ≤3 alive, separation (WP-5) | settings wiring, 1-per-1.2 s gate, priority, 10 s per skill, first-seen | §3.4 · P4-3b |
| AUD-01 + Critic-A3 | P0 | partial | M18 Phase A haptics + UI hooks (Phase B hook calls) | AudioSystem (buses, 12-voice pool, priority, ducking), battle SFX, BGM, cue table, clips route | §3.5 · P4-3a, P4-3b, P4-10 |
| IDLE-06 | P1 | partial | M01, M15, M68 | FX cap + BGM/SFX duck while asleep | §3.6 · P4-6, P4-3a/b |
| SET-01 | P0 | partial | M18 '준비 중' pills (WP-4), copy M74/M80/M53/M64/M22, notice move M10 | skill-name wiring, intro replay, import, backup restore, coupon row, live notification tab, sliders back on | §4.1 · P4-4 |
| SAVE-01 | P1 | partial | – | import/apply, share sheet, daily snapshots, restore, tests | §4.2 · P4-7, P4-4 |
| NOTI-01 | P1 | missing | – | package 2.4.3, planner, permission flow, copy | §4.3 · P4-7, P4-6 |
| CR-09 | P1 | missing | – | PlayLog jsonl + dev summary + export | §4.4 · P4-7 |
| EVT-02 | P2 | missing | – | hashed coupons → mail | §4.5 · P4-2, P4-4, P4-8 |
| F-03 | P0·M | partial | M38/M39/M40 restyle locks | enforcement inside open tabs + system guards; per-feature card readiness | §5.1 · all UI pkgs, P4-1/2, P4-8 |
| IDLE-05 | P1·S | partial | M13 auto re-challenge (WP-2) | cards for Speed2x/AutoChallenge/ExpeditionBatch/LampPolicy, dungeon batch sweep at floor 5 | §5.2 · P4-2, P4-4, P4-8 |
| IDLE-04 | P1·S | partial | M57 per-card resend (WP-4) | ×1/×3/×6 ladder, 모두 다시 보내기 | §5.3 · P4-2, P4-4 |
| Q-04 | P1·M | missing | – | 62 records + hidden 5, journal 3rd tab | §6.1 · P4-1, P4-6, P4-8 |
| Q-05 | P1·M | missing | – | codex with discovery rewards and permanent bonuses | §6.2 · P4-1, P4-6, P4-5, P4-8 |
| CR-10 | P1 | missing | M05 affinity sort only | levels 2–10, story lines, rewards, frame, tap-to-pet | §6.3 · P4-1, P4-5, P4-3b, P4-8 |
| PRG-04 | P1 | missing | M63 pity restyle | cumulative pull ladder + friend ticket | §6.4 · P4-1, P4-5, P4-8 |
| PRG-05 | P1 | missing | – | catch-up level 80% | §6.5 · P4-1 |
| PRG-01 | P0 | partial | M10, M12, M13, M34, M45, M46 | dungeon rec power, gacha new-friend power, grid power, tapered growth | §7.1 · P4-1, P4-4, P4-5, P4-8 |
| CR-01 | P1 | missing | M13 phase-2 강해지기 sheet | defeat ring + advice + slot pulse + sheet row | §7.2 · P4-3b, P4-4, P4-6 |
| CR-02 | P1 | missing | M09 removes LampPolicy HUD dot | 3 lamp policies + long-press picker | §7.3 · P4-3b, P4-0 |
| CR-03 | P1 | missing | – | battle stats recorder + popup + HUD button | §7.4 · P4-3b |
| CR-04 | P1 | partial | M16/M49/M55 CTAs + Heavy haptic | bundle reveal sequence, grade lights, 10-pull pacing, dup melt | §7.5 · P4-5, P4-10 |
| CR-07 | P0 | missing | – | 60-min headless economy test + tuning knobs | §7.6 · P4-9 |
| CR-05 | P1 | missing | (conflict) M13 phase-2 stage list | storybook 1–5 + chapter map (replaces the stage list) | §8.1 · P4-1, P4-6, P4-8, P4-10 |
| CR-06 | P1 | missing | – | comeback trigger, 3-day stamp, one-shot 24 h basket | §8.2 · P4-2, P4-6, P4-8 |
| EVT-01 | P1 | missing | ShortfallPopup routes EventToken to '이벤트' (WP-3a) | deterministic calendar, 4 events, hub, missions, stamps | §8.3 · P4-2, P4-6, P4-4, P4-8 |
| Q-06 | P2·M | missing | – | 28-day 30-tier free track | §8.4 · P4-2, P4-6, P4-8 |
| PRG-06 | P2 (lost chest) / **P1 (chapter chest, part of PRG-02)** | missing | – | lost chest roll (stretch) + chapter chest popup (core, critic C-04) | §8.5 · P4-1, P4-3b, P4-6, P4-8 |
| PRG-07 | P2 | missing | – | dungeon blessings 1-of-3 every 3 floors | §8.6 · P4-2, P4-4, P4-8 |
| PRG-08 | P2 | missing | – | **decision: do not build** (rename note) | §8.7 |
| Critic-A7 | meta | partial | – | all remaining contract changes | §2 · P4-0 |
| **Critic rows (v1.1)** | | | | | |
| PRG-02 | P0 | in-progress-ux | M13 phase 1 + HUD + fail banner (WP-2/5), M11 boss-phase label | chapter-boss gimmick hint line, 50% enrage tick; **chapter chest promoted to P1 core** | §13.1, §8.5 · P4-3b, P4-1, P4-6, P4-8 |
| PRG-03 | P1 | in-progress-ux | M13 phase-2 sheet (3 fixed rows) | StrengthenAdvisor ranking by gain/cost + 6 more row kinds, the ≥5% auto-formation rule, 10-minute farming nudge, 친구들의 응원 buff | §13.2 · P4-1, P4-6, P4-3b, P4-8 |
| FX-01 | P0 | done\* | – | the simulation follows the hit-stop/slow-motion (`PresentationScale`), so enemies stop sliding during the freeze | §13.3 · P4-3b |
| FTUE-01 | P0 | in-progress-ux | M06, M05, M79, M42, M15, M74 (WP-4/8) | battle protection + `FillLamp` API (removes the reflection hack), first-lamp slow beat, step-1 hit emphasis | §13.3 · P4-3b, P4-6 |
| IDLE-01 | P0 (P2 sub-item) | done\* | M32 widget polish (WP-7) | 16 h cap at shop Lv.200; **별조각 line growth capped at ×1.5** (economy fix) | §13.4 · P4-0, P4-2, P4-6, P4-8 |
| F-04 | P0 | in-progress-ux | M09, M10 | the `tab.shop.free` leaf gets a source (NEW-02); `tab.gacha.free` stays reserved (decision) | §13.7 · P4-2, P4-4 |
| FX-05 | P1 | in-progress-ux | M18 Phase A (Android hooks) | iOS `UIImpactFeedbackGenerator` backend (stretch) | §13.5 · P4-7 |
| Q-01 | P0 | done\* | M04, M62 (WP-7) | danger arrows avoid the guide-bar rect (`GuideQuestBarView.TryGetRect` has no caller today) | §13.5 · P4-3b |
| CR-11 | – | deferred | – | record the deferral; remove '장비' from the 3 player-facing evolution strings | §13.5 · P4-5 |
| ATK-01/02/05 polish | P0 | done\* | – | button shot spins with real art, echo streak only for snipe, radial lamp shake, save-based lamp banner flag | §13.5 · P4-3b |
| NEW-01 첫 주 | P1 | new | – | 7-day mission sheet (28 tasks, 5 chests) as a journal tab | §13.6 · P4-2, P4-6, P4-8 |
| NEW-02 오늘의 작은 선물 | P1 | new | – | free daily shop gift (7-day rotation) | §13.7 · P4-2, P4-4, P4-8 |
| NEW-03 길 잃은 반딧불 | P2 | new | – | tappable random visitor, ≤6 per day | §13.8 · P4-2, P4-3b, P4-8 |
| NEW-04 입장권 모아 두기 | P2 | new | – | dungeon ticket bank up to 9 | §13.9 · P4-0, P4-2, P4-4, P4-7 |

\* "done\*" = the audit marked it done, but it is not done end-to-end (evidence in §14).

Priority waves inside phase 4 (§9.1): **P0/P1 ship in 4.1. P2 items (Q-06, the PRG-06 lost chest, PRG-07, EVT-02, NEW-03, NEW-04, the FX-05 iOS backend) are "stretch": same packages, last in their order, and droppable without breaking contracts. The PRG-06 *chapter* chest is not stretch (§8.5, critic C-04).**

---

## 2. Shared contracts (P4-0 owns all of §2; everyone else compiles against it)

### 2.1 Save schema v4 (`Core/PlayerData.cs`, `Core/SaveData.*.cs`)
`PlayerData.CurrentVersion = 4`. In `Sanitize`, `bool v4Migration = version < 4;` and after `meta.Sanitize()`: `if (v4Migration) meta.MigrateToV4();`. `MigrateToV4` only sets `codex.backfillPending = true`. Everything else defaults to empty.

| Class (file) | New fields (default) | Sanitize |
|---|---|---|
| `MetaSave` (SaveData.Meta.cs) | `AchievementSave achievements`, `CodexSave codex`, `GachaMilestoneSave gachaMilestones`, `EventSave events`, `SeasonSave season`, `StorySave story`, `AffinitySave affinity`, `CouponSave coupons`, `ChapterChestSave chapterChests` (all `= new()`) | `??= new` + child Sanitize |
| `AchievementSave` | `HashSet<string> claimed` (tier ids `ach_stage_3`) | null→new |
| `CodexSave` | `HashSet<string> found` (keys §6.2), `HashSet<string> claimed`, `HashSet<string> seenSkills`, `bool backfillPending` | null→new |
| `GachaMilestoneSave` | `int pulls` (paid pulls only), `int claimedSteps` | clamp ≥0 |
| `EventSave` | `Dictionary<string, EventInstanceSave> instances` (key `{eventId}@{startDayKey}`) | drop entries whose startDayKey is >60 days old (DailyClock.DaysBetween) |
| `EventInstanceSave` | `string eventId, startDayKey`, `Dictionary<CounterKey,long> baseline`, `HashSet<string> claimed`, `int stamps`, `string lastStampDayKey`, `bool seen`, `bool startCardShown` | null→new |
| `SeasonSave` | `string startDayKey`, `int index`, `long xp`, `HashSet<int> claimedTiers` | clamp |
| `StorySave` | `HashSet<int> seenChapters`, `HashSet<int> rewardedChapters` | null→new |
| `AffinitySave` | `Dictionary<string,int> claimedLevel`, `Dictionary<string, PetDaySave> pets` | null→new |
| `PetDaySave` | `string dayKey`, `int count` | clamp |
| `CouponSave` | `HashSet<string> redeemed` (hash hex), `int failStreak`, `long lockUntilUnix` | null→new, clamp |
| `ChapterChestSave` | `HashSet<int> opened` (chapter numbers whose first-clear chest was granted) | null→new |
| `ComebackSave` (exists) | + `string startDayKey`, `string lastStampDayKey` | – |
| `MetaWalletSave` (exists) | + `int friendSelectTickets` | clamp |
| `IdleChestSave` (exists) | + `long bonusCapSeconds` (CR-06 one-shot cap, 0 = none) | clamp ≥0 |
| `BattleSave` (SaveData.Battle.cs) | + `int lampPolicy` (0 Auto) | clamp 0..2 |
| `ExpeditionSave` | + `int durationTier` (0) | clamp 0..2 |
| `ExpeditionMembersSave` | + `int lastTier` (0) | clamp 0..2 |
| `DungeonSave` | + `List<string> blessings`, `int blessingPicksPending`, `List<string> blessingOffer`, `string respecWeekKey` | null→new, clamp |
| **Critic additions (§13)** | | |
| `MetaSave` | + `StarterWeekSave starterWeek`, `DailyGiftSave dailyGift`, `FireflyDaySave firefly`, `CheerSave cheer` (all `= new()`) | `??= new` + child Sanitize |
| `StarterWeekSave` (NEW-01) | `string startDayKey` (null = not started), `HashSet<string> done` (latched task ids), `HashSet<string> claimed`, `HashSet<int> chests`, `bool finished`, `bool declined` (never starts on this save) | null→new |
| `DailyGiftSave` (NEW-02) | `string lastDayKey` | – |
| `FireflyDaySave` (NEW-03) | `string dayKey`, `int count` | clamp 0..cap |
| `CheerSave` (PRG-03) | `int lastChapter` (0 = never), `long endUnix` | clamp ≥0 |
| `DungeonSave` (NEW-04, stretch) | + `int ticketBank` (-1 = not migrated), `string bankDayKey` | clamp -1..bankCap+event bonus |
| `PlayerData.flags` (no schema change) | `fx.lamp_intro_seen` (moves from PlayerPrefs, §13.5), `hint.boss.{bossId}` (first encounter, §13.1) | – |

### 2.2 Enum appends (`Core/GameEnums.cs`)
```csharp
// CounterKey (append, values fixed)
JournalPoints = 31,      // 일일 미션 점수를 받은 만큼 (수첩 경험치, Q-06) — MissionSystem.TryClaim이 Increment(points)
AchievementClaim = 32,   // 수선점 기록 받기 (단계 1개당 1)
CodexClaim = 33,         // 도감 보상 받기
AffinityPet = 34,        // 쓰다듬기 (호감도가 오른 것만)
EventMissionClaim = 35,  // 이벤트 미션 받기
SeasonTierClaim = 36,    // 수첩 칸 받기
LostChestOpen = 37,      // 잃어버린 상자 (PRG-06)
// FeatureId (append)
DungeonBatchSweep = 25,  // 던전 입장권 한 번에 쓰기 (던전 5층 돌파, IDLE-05)
BattleStats = 26,        // 전투 기록 (1-7 클리어, CR-03)
// MetaPopupKind (append; both are session kinds, between stages only)
StoryPage = 8,           // 동화책 한 쪽 (CR-05, 챕터 상자 다음)
NotificationAsk = 9,     // 알림 권한 미리 묻기 (NOTI-01)
// MetaRewardType (append)
FriendSelectTicket = 8,  // 친구 선택권 (itemId = 최고 등급 이름 "Legendary", amount = 장 수)
// MetaConditionType (append)
AffinityLevelCount = 22, // targetId = 호감도 단계("10"), target = 그 단계 이상 계보 수
SynergyKindsSeen = 23,   // 한 번이라도 켜 본 시너지 종류 수 (CodexSave.found "synergy:*")
CostumesOwned = 24,      // 보유 의상 수
// DamageNumberKind (UI/Battle/BattleFx.Numbers.cs, append) — the only enum appended by P4-3b, not P4-0 (UI-only, single consumer)
Skill,                   // 스킬 피해: 1.2, 시전자 계보 색 (ATK-06 T3)
// new enums (GameEnums.cs, end of file)
public enum LampPolicy { Auto = 0, SaveForBoss = 1, Manual = 2 }
public enum AchievementCategory { Stage, Kill, Lamp, Echo, Friend, Form, Expedition, Dungeon, Gacha, Synergy, ShopLevel, LevelSum, Journal, Attendance, Costume, Affinity, Hidden }
public enum CodexCategory { Friend = 0, Costume = 1, Enemy = 2, Synergy = 3, Material = 4, Story = 5 }
public enum EventKind { DropBoost = 0, TicketBonus = 1, Festival = 2, NightBonus = 3 }
public enum EventScheduleType { Weekdays = 0, MonthDays = 1, DailyHours = 2 }
public enum AudioBus { Bgm = 0, Sfx = 1, Ui = 2 }
public enum NotificationPermission { Unknown = 0, Granted = 1, Denied = 2, SoftDeclined = 3 }
// ── critic additions (v1.1, §13) ──
// CostumeUnlockType (append; exists, last value FormUnlocked = 6)
Season = 7,              // 별빛 산책 수첩 30칸 (Q-06 fix, jjak_starwalk)
// EventScheduleType (append, reserved now so seasonal events can land later without a contract change)
Dates = 3,               // fromMonth/fromDay ~ toMonth/toDay by DayKey (wraps the year), §13.5(i)
public enum StrengthenKind { ShopUpgrade = 0, LineageLevel = 1, FillSlot = 2, SynergyOneShort = 3, EvolutionReady = 4,
                             ResonanceReady = 5, MissingMaterial = 6, AutoFormation = 7, WeakDirection = 8 }   // PRG-03
```
No new `CounterKey`, `FeatureId` or `MetaPopupKind` members are needed for §13: every NEW-01 task uses an existing counter or condition.
`MetaSession.IsHudToggle` gains `BattleStats`. `RedDotPaths.ParentOf` gains `DungeonBatchSweep → TabExpedition`. `BattleStats` returns null, so it gets no dot (M09).

### 2.3 MetaDatabase additions (`Data/MetaDatabase.cs`)
New `[Header]` blocks with empty defaults. Each block gets a lookup cache, cleared by `ClearCache()`:
```csharp
public List<AchievementDef> achievements = new();            // Q-04
public CodexConfig codex = new();                             // Q-05
public List<GachaMilestoneDef> gachaMilestones = new();       // PRG-04 (pulls asc)
public GachaRepeatMilestone gachaRepeat = new();              // every N after the last
public List<EventDef> events = new();                         // EVT-01
public SeasonConfig season = new();                           // Q-06
public ComebackConfig comeback = new();                       // CR-06
public AffinityConfig affinity = new();                       // CR-10
public List<StoryChapterDef> storyChapters = new();           // CR-05
public LostChestConfig lostChest = new();                     // PRG-06
public List<CouponDef> coupons = new(); public string couponSalt = "";   // EVT-02
public List<BlessingDef> blessings = new();                   // PRG-07
// PowerConfig (exists) — PRG-01 tapered growth + dungeon:
public int recommendedTaperStage;          // 0 = off (one exponent, today's behaviour)
public double recommendedGrowthLate;       // growth after the taper stage
public double dungeonRecommendedBase;      // 0 = no dungeon rec line
public double dungeonRecommendedGrowth = 1.1;
```
Definitions (all `[Serializable]`, fields public, Korean tooltips):
```csharp
class AchievementDef { string id; string lineId; int tier; AchievementCategory category; string title; MetaCondition condition;
                       List<MetaReward> rewards; bool hidden; string hint; FeatureId navFeature; string focusTarget; Sprite icon; }
class CodexConfig { int formBaseGem = 20, formTier1Gem = 30, formTier2Gem = 50, costumeGem = 30, enemyGem = 10,
                    synergyFirstGem = 20, synergyMaxGem = 30, materialGem = 5;
                    List<CodexBonusTier> formTiers; float allEnemiesShopHpPercent = 5; List<CodexBonusTier> synergyTiers;
                    List<CodexHintDef> hints; List<CodexEnemyText> enemyTexts; }
class CodexBonusTier { int count; float percent; }              // value = total at that tier (not additive)
class CodexHintDef { string key; string hint; }                  // key = CodexSystem.Key(...)
class CodexEnemyText { string enemyId; string text; }
class GachaMilestoneDef { int pulls; List<MetaReward> rewards; }
class GachaRepeatMilestone { int interval = 100; List<MetaReward> rewards; }
class EventDef { string id; string name; string shortDesc; EventKind kind; EventScheduleType scheduleType; int weekdayMask; // bit0 = Monday
                 int fromDay, toDay; int fromHour, toHour; double multiplier = 1; int bonus; List<EventMissionDef> missions;
                 List<EventStampDef> stamps; bool startCard; Sprite banner; Sprite icon; }
class EventMissionDef { string id; string text; CounterKey counter; long target; List<MetaReward> rewards; bool isBonus; }
class EventStampDef { int day; List<MetaReward> rewards; bool highlight; }
class SeasonConfig { string name = "별빛 산책 수첩"; int days = 28; int xpPerTier = 90; List<SeasonTierDef> tiers; Sprite banner; }
class SeasonTierDef { int tier; List<MetaReward> rewards; List<MetaReward> fallback; bool highlight; } // fallback = when the costume is owned
class ComebackConfig { long minAwaySeconds = 259200; int cooldownDays = 14; int lifetimeMax = 6; float bonusCapHours = 24;
                       List<ComebackDayDef> days; }
class ComebackDayDef { int day; List<MetaReward> rewards; }
class AffinityConfig { long[] thresholds = { 0, 50, 150, 350, 700, 1200, 2000, 3200, 4800, 7000 }; int gemPerLevel = 10;
                       int petAmount = 5; int petsPerDay = 3; int frameLevel = 5; int poseLevel = 10; List<AffinityLineDef> lineages; }
class AffinityLineDef { string lineageId; string[] story = new string[5]; string[] pet = new string[7]; }
class StoryChapterDef { int chapter; string title; string place; List<StoryPanelDef> panels; List<MetaReward> firstReadRewards; }
class StoryPanelDef { Sprite art; string text; }
class LostChestConfig { float dropChance = 0.6f; List<LostChestTable> tables; List<ChapterChestDef> chapterChests; }
class LostChestTable { int fromStage, toStage; List<LostChestEntry> entries; }
class LostChestEntry { string itemId; int min = 1, max = 1; int weight = 1; }
class ChapterChestDef { int chapter; List<MetaReward> rewards; }   // chapter 0 = default for chapters with no row
class CouponDef { string hash; string validFromDayKey; string validToDayKey; List<RewardData> rewards; string mailTitle; string mailBody; }
class BlessingDef { string id; string name; string desc; StatType stat; float value; int maxStacks = 2; string iconKey; }
```
**Critic additions (v1.1, §13; P4-0 adds the shapes, P4-8 the content):**
```csharp
public StarterWeekConfig starterWeek = new();   // NEW-01
public List<DailyGiftDef> dailyGifts = new();    // NEW-02 (7 rows, index = DayKey weekday, Monday = 0)
public FireflyConfig firefly = new();            // NEW-03
public CheerConfig cheer = new();                // PRG-03
public List<BossHintDef> bossHints = new();      // PRG-02
// existing classes gain:
IdleChestLine.maxStageMultiplier;   // double, 0 = uncapped (today). 별조각 line = 1.5 (§13.4)
IdleCapStep.shopLevel;              // int, 0 = unused. A step applies when every non-zero condition holds (§13.4)
EventDef.fromMonth, toMonth;        // int 1..12, only for EventScheduleType.Dates (reserved)
MetaDatabase.dungeonTicketBankCap;  // int, 0 = bank off (today's daily reset). NEW-04 content sets 9. DungeonSystem reads it via
                                    // the hook `public Func<int> TicketBankCap;` (P4-0 adds it next to BonusTickets; MetaSession wires it)

class StarterWeekConfig { int windowDays = 14; int maxInstallDays = 7; List<StarterTaskDef> tasks; List<StarterChestDef> chests; }
class StarterTaskDef { string id; int day; string text; MetaCondition condition; int points; FeatureId requiredFeature; FeatureId navFeature; string focusTarget; }
class StarterChestDef { int points; List<MetaReward> rewards; bool highlight; }
class DailyGiftDef { int weekday; List<MetaReward> rewards; }
class FireflyConfig { float minInterval = 360, maxInterval = 720; float lifeSeconds = 8; int perDay = 6; List<FireflyRewardDef> rewards; }
class FireflyRewardDef { MetaReward reward; bool fromLostChestTable; int weight = 1; }   // fromLostChestTable: 1 item from the B.3 row of the highest stage
class CheerConfig { float attackPercent = 20; int durationSeconds = 600; }
class BossHintDef { string enemyId; string hint; }
```

### 2.4 New systems, wiring and the module interface (`Systems/Meta/`, `Core/Game.cs`, `MetaSession.cs`)
`MetaSession` gains modules. **Its constructor signature does not change** (MetaSessionTests construct it):
```csharp
/// <summary>4차 메타 모듈 (도감·기록·이벤트 …). MetaSession이 정해진 순서로 부른다. 구현은 예외를 던지지 않는다.</summary>
public interface IMetaModule
{
    void Connect();                                  // Start에서 한 번 (다른 시스템 이벤트 구독은 여기)
    void BeforeAwayApplied(long awaySeconds, bool boot); // 비운 시간을 바구니에 넣기 직전 (다시 만난 날 24시간 상한)
    void AfterStartup(bool boot);                    // 접속 요청을 큐에 넣은 직후 (Comeback / EventStart / StoryPage …)
    void OnDayChanged(string dayKey);                // DailyClock.DayChanged (MetaSession이 한 곳에서 나눠 준다)
    void Tick(float unscaledDeltaTime);              // 매 프레임, 할당 없이. 1초 묶음은 모듈이 스스로
    void RegisterRedDots(RedDotSystem dots);         // MetaSession.RegisterRedDots 안에서
}
public void MetaSession.AddModule(IMetaModule module); // Game.SetupMeta가 만든 순서대로 넣는다
```
Order inside `MetaSession.Start`: existing `Connect()` → `modules.Connect` → `Features.Evaluate` → `CountLogin` → away → **`modules.BeforeAwayApplied(away, true)`** → `IdleChest.AddOfflineTime` → `ConsumeAwayGap` → `BeginStartupBatch` + `RequestStartupPopups` → **`modules.AfterStartup(true)`** → tutorial → `RegisterRedDots` (+ modules). `OnResume` follows the same pattern with `boot = false`. `Tick` calls the modules after `Progress.Flush` and before `RedDots.Refresh`.

`Game.SetupMeta` creates these after the existing systems, in this order, and adds each to the session:

| Property | Class | Ctor deps (exact order) | Package |
|---|---|---|---|
| `Game.Codex` | `CodexSystem` | data, db, Progress, MetaRewards, Features, Characters, Costumes, Evolution, Synergy, Battle, Inventory | P4-1 |
| `Game.Achievements` | `AchievementSystem` | data, db, Progress, MetaRewards, Features, Codex, Battle, DailyClock | P4-1 |
| `Game.GachaMilestones` | `GachaMilestoneSystem` | data, db, Gacha, MetaRewards, Characters | P4-1 |
| `Game.Affinity` | `AffinitySystem` | data, db, Characters, MetaRewards, DailyClock, Progress | P4-1 |
| `Game.ChapterChests` | `ChapterChestSystem` | data, db, Battle, MetaRewards, Popups, Items, Tutorial | P4-1 |
| `Game.Story` | `StorySystem` | data, db, Battle, MetaRewards, Popups, Tutorial | P4-1 |
| `Game.Events` | `EventCalendarSystem` | data, db, DailyClock, Progress, MetaRewards, Features, Popups, Battle, Expedition, Dungeon, IdleChest | P4-2 |
| `Game.Season` | `SeasonPassSystem` | data, db, DailyClock, Progress, MetaRewards, Features | P4-2 |
| `Game.Comeback` | `ComebackSystem` | data, db, DailyClock, MetaRewards, IdleChest, Popups, Tutorial | P4-2 |
| `Game.Coupons` | `CouponSystem` (not a module) | data, db, DailyClock, Mail | P4-2 |
| `Game.StarterWeek` (§13.6) | `StarterWeekSystem` | data, db, DailyClock, Progress, MetaRewards, Features | P4-2 |
| `Game.DailyGift` (§13.7) | `DailyGiftSystem` | data, db, DailyClock, MetaRewards, Features | P4-2 |
| `Game.Cheer` (§13.2) | `CheerSystem` | data, db, DailyClock, Battle, Tutorial | P4-1 |
| `Game.Fireflies` (§13.8) | `FireflyRules` (not a module) | data, db, DailyClock, MetaRewards, Battle, Tutorial | P4-2 |
| `Game.Strengthen` (§13.2) | `StrengthenAdvisor` (not a module; pure queries) | Power, Growth, Characters, Formation, Synergy, Evolution, Battle, Inventory, Cheer | P4-1 |

P4-0 writes each class with its full public surface from §6–§8 and §13 (critic) and inert bodies. `Tests/EditMode/SystemsTestBase.CreateMetaSystems` builds the same set (P4-0).

### 2.5 Hooks added to existing systems (P4-0 adds members only; the named package implements them)
| File | Member | Default | Implemented / consumed by |
|---|---|---|---|
| `Systems/Synergy/SynergySystem.cs` | `public Action<StatBlock> ExtraTeamModifiers;`, invoked at the end of `BuildModifiers` on `result.Team` (also when `echoesOnly`) | null | Codex sets it (P4-1) |
| `Systems/Battle/BattleTypes.cs` | `BattleSetup.LampPolicy LampPolicy;` | Auto | P4-3b |
| `Systems/Battle/BattleSystem.cs` | `public LampPolicy LampPolicy {get;set;}` (save-backed, applies live); `public Func<int, List<RewardData>, bool> ClearDropHook;` (true = it rolled drops, so skip `RollClearDrops`); `public Func<double> CandyMultiplier;` | null / 1 | P4-3b; hooks set by ChapterChests and Events |
| `Systems/Battle/BattleSimulator.cs` | `public string GetEnemyDefId(int enemyId)` (no alloc; null if unknown) | – | P4-3b; used by Codex |
| `Systems/Battle/ExpeditionSystem.cs` | `public Func<double> DropMultiplier;` `TryStart(zoneId, ids, int tier)`; `int GetDurationMinutes(ExpeditionZoneDef, int tier)`; `static double RewardScale(int tier)`; `bool IsTierAllowed(zone, tier)`; `int ResendAll()` | 1 | P4-2 |
| `Systems/Battle/DungeonSystem.cs` | `public Func<int> BonusTickets;` `public Func<FeatureId,bool> FeatureResolver;` `DungeonResult SweepMany(int floor, int count)`; blessing API (§8.6) | 0 / null | P4-2 |
| `Systems/Meta/IdleChestSystem.cs` | `public Func<long,long,double> NightBonusHours;` `public Func<long,long,double> EventLineHours;` `public Func<double> NightBonusMultiplier;` (null or ≤ 1 = no night bonus; `EventCalendarSystem.NightBonusMultiplier` sets it, 1.2 → +20 %) `void GrantBonusCap(double hours)` | null | P4-2 |
| `Systems/GachaSystem.cs` | `GachaResult.IsGuaranteed` (ctor param, default false; `PullGuaranteed` sets it true) | false | P4-1 |
| `Systems/Characters/{CharacterSystem, EvolutionSystem, CostumeSystem}.cs` | `public Func<FeatureId,bool> FeatureResolver;` (null = open) | null | P4-1 (guards §5.1) |
| `Systems/InventorySystem.cs` | `public event Action<string,int> ItemAdded;` (raised in `Add` when count > 0) | – | Codex |
| `Systems/Meta/MissionSystem.cs` | on daily claim: `_progress.Increment(CounterKey.JournalPoints, def.points)` | – | P4-2 |
| **Critic additions (§13)** | | | |
| `Systems/Battle/BattleSystem.cs` | `public float PresentationScale {get;set;}` (clamp 0.05..1; multiplies only `_sim.Tick`, never the end-pause timer) | 1 | P4-3b (FX-01, §13.3) |
| `Systems/Battle/BattleSystem.cs` | `public void ExtendEndPause(float totalSeconds)` (only while ended; ≤ 6 s) and `public void SkipEndPause()` | – | P4-3b (CR-01 fix, §7.2) |
| `Systems/Battle/BattleSystem.cs` | `public Func<float> TempAttackPercent;` read in `BuildSetup` as an extra factor on `AttackMultiplier`; `public void RequestUnitRefresh()` (sets the existing `_refreshPending`) | null | P4-3b; set by Cheer (P4-1) |
| `Systems/Battle/BattleSystem.cs`, `BattleTypes.cs`, `BattleSimulator.cs` | `public Func<int, BattleProtection> ProtectionResolver;` → `BattleSetup.Protection {bool Enabled; float EnemyHpScale; float ShopFloorHp;}`; `BattleSimulator.FillLamp()`; `BattleSystem.ForceLampFull()` | null / off | P4-3b; `MetaSession.Connect` sets the resolver from `Tutorial.WantsBattleProtection` (stage ≤ 3 → 0.6 / 1); FtueDirector calls `ForceLampFull` (P4-6) |
| `Systems/Meta/IdleChestSystem.cs` | honour `IdleChestLine.maxStageMultiplier` and `IdleCapStep.shopLevel` | – | P4-2 (§13.4) |
| `Systems/Battle/DungeonSystem.cs` | ticket bank (NEW-04, stretch) behind `ticketBankCap > 0` | off | P4-2 |
| `Core/Haptics.cs` | `public static Action<HapticKind> PlatformOverride;` (called instead of the Android path when set; HapticGate still applies) | null | P4-7 (FX-05 iOS) |
`MetaSession.Connect` (P4-0) sets the four `FeatureResolver`s to `f => _features.IsUnlocked(f)`.

### 2.6 Navigation and signals (`UI/UINav.cs`, new `Core/AppSignals.cs`)
```csharp
// UINav (append; P4-0) — receivers in brackets
public static event Action<CodexCategory?> CodexRequested;  public static void OpenCodex(CodexCategory? tab = null);   // [P4-6 MetaNavigator]
public static event Action EventsRequested;                 public static void OpenEvents();                          // [P4-6]
public static event Action SeasonRequested;                 public static void OpenSeason();                          // [P4-6]
public static event Action<int> ChapterMapRequested;        public static void OpenChapterMap(int chapter = 0);       // [P4-6] 0 = current
public static event Action BattleStatsRequested;            public static void OpenBattleStats();                     // [P4-3b]
public static event Action IntroReplayRequested;            public static void ReplayIntro();                         // [P4-6 → StoryIntroView]
public static event Action<int, float> FormationSlotPulseRequested; public static void PulseFormationSlot(int slot, float s = 2f); // [P4-4]
// AppSignals (new, Core) — cross-package runtime signals with no UI dependency
public static class AppSignals
{
    public static event Action<bool> SleepChanged;    public static void RaiseSleep(bool asleep);     // SleepModeView → BattleView, AudioSystem
    public static event Action<int> GachaRevealHigh;  public static void RaiseGachaRevealHigh(int grade); // GachaResultPopup → AudioSystem duck
    // critic (§13.3): FTUE presentation flags, set by FtueDirector (P4-6), read by BattleView (P4-3b), so neither package compiles against the other
    public static bool FtueFirstLampBeat;   // step-4 coach on the lamp → 0.2× for 0.6 s + big banner
    public static bool FtueFirstImpression; // step 1 → guardian hits ×1.3 + 2 sparkles
}
```

### 2.7 Red-dot paths (`RedDotPaths`, append)
| Constant | Path | Leaf type | Rule |
|---|---|---|---|
| `JournalRecords` | `top.journal.records` | bool | ≥1 achievement tier claimable. The journal button keeps its mission count; when the count is 0 this leaf shows as a dot |
| `Codex` | `tab.characters.codex` | bool | Codex unlocked and ≥1 claimable entry |
| `GachaMilestone` | `tab.gacha.milestone` | bool | next milestone reached and unclaimed, **or** `friendSelectTickets > 0` |
| `LineageAffinity(id)` | `tab.characters.lineage.{id}.affinity` | bool | that lineage has an unclaimed affinity level |
| `EventClaim` | `top.event.claim` | bool | any active instance: claimable mission, or today's stamp not taken |
| `EventNew` | `top.event.new` | bool | an active instance with `seen == false` (cleared when the hub opens) |
| `Season` | `top.event.season` | bool | ≥1 reached, unclaimed season tier |
| `DungeonBlessing` | `tab.expedition.dungeon.blessing` | bool | `blessingPicksPending > 0` |
| `JournalStarter` (§13.6) | `top.journal.starter` | bool | first week active and ≥1 claimable task or chest (shown as a dot when the mission count is 0, like `JournalRecords`) |
| `ShopFree` (exists, unregistered today) | `tab.shop.free` | bool | today's 오늘의 작은 선물 not yet taken (§13.7). Registered by `DailyGiftSystem` |
| `GachaFree` (exists) | `tab.gacha.free` | – | **stays unregistered (decision):** there is no free pull, because it would add ≈700 gem-equivalent a week (§11.2) |
No new HUD dots (M09). Comeback, coupon, notification ask, fireflies and the cheer never use dots.

### 2.8 Feature unlocks and cards (content P4-8, `MetaContent.Features.cs`)
Replace the global `ReservedFeatureCards` flag with per-feature readiness: `static readonly HashSet<FeatureId> CardsReady`. A card turns on only when that feature's screen has merged; until then the feature unlocks quietly with only its dot. At most **two cards per moment** (existing rule).

| Feature | Condition | Card | Card text (cardText) | Gift |
|---|---|---|---|---|
| Speed2x | 1-6 | **on** | '전투를 두 배 빠르게 볼 수 있어요' | – |
| LampPolicy | 1-6 | **on** (when §7.3 merges) | '불 밝히기 버튼을 길게 누르면 쓰는 방식을 고를 수 있어요' | – |
| BattleStats (new) | 1-7 | off (MetaPulse on the HUD button) | – | – |
| Codex | 1-10 | off (Dungeon + Evolution already fill the two cards). The 친구들 tab gets the **NEW pill** on `CodexButton` | – | – |
| AutoChallenge | 2-1 | **on** | '보스에 다시 도전하는 것도 친구들이 때를 봐서 해요' | – |
| ExpeditionBatch | 2-1 | **on** | '이제 원정을 한 번에 다시 보낼 수 있어요' | 빠른 수선권 1 |
| Achievements | 2-3 | **on** | '수선점에 쌓인 이야기를 기록하고 선물을 받아요' | – |
| DungeonBatchSweep (new) | `DungeonFloor 5` | **on** | '이제 던전 입장권을 한 번에 모두 쓸 수 있어요' | – |
| Events | 3-1 or InstallDays 3 | **on** | '별이 머문 밤 산책 같은 작은 축제가 열려요' | 별빛 사탕 50 |
`lockedHint`/`lockedToast` use `MetaText.UnlockShort/UnlockSentence` (M39). DungeonBatchSweep's toast is '잔향이 던전 5층을 돌파하면 열려요'.

### 2.9 PopupQueue (`Systems/Meta/PopupQueue.cs`)
- `RequiresBetweenStages` adds `StoryPage` and `NotificationAsk`. `ChapterChest` is already listed.
- `DropStartupKinds` **keeps `EventStart`**. When the startup cap (3) is reached, the card waits for the next between-stages slot instead of being dropped.
- Session order by enum value: FeatureUnlock(5) → FtueGraduation(6) → ChapterChest(7) → StoryPage(8) → NotificationAsk(9). A chapter boss's chest therefore always comes before its story page.
- New request factories: `MetaPopupRequest.Comeback()`, `EventStart(string instanceKey)`, `ChapterChest(int chapter)`, `StoryPage(int chapter)`, `NotificationAsk()`.

### 2.10 GameSettings additions (`Core/GameSettings.cs`, PlayerPrefs, device-level)
`NotificationPermission NotifyPermission` (key `settings.notify.permission`), `int NotifySoftAsks`, `long NotifyLastAskUnix`, `bool NotifyMaster` (default true; the 4 per-kind switches stay). No other new keys. `SkillNames` already exists (§3.4 wires it).

### 2.11 Conflict resolutions decided here
| # | Conflict | Decision |
|---|---|---|
| 1 | CR-05 chapter map vs UX M13 phase-2 "last 10 cleared stages" list on the StagePanel tap | The chapter map **replaces** the list. Cleared nodes call `GoToStage` (the same behaviour), and the map is disabled during FTUE (M13 rule). P4-6 deletes the M13 list popup if WP-7 built one and routes the tap to `UINav.OpenChapterMap()` |
| 2 | EVT-01 "TopBar [이벤트] icon" vs the M10 top bar (5 buttons, left block ≤406 px; no room for a 6th) | The existing **EventChip** (별빛 사탕 currency chip) becomes the event entry. Before Events unlocks it routes to the event shop (today's behaviour). After, it opens the hub and carries the `top.event` dot and a 'D-n' pill |
| 3 | Critic-A3 pitch ±6% vs `UISfxGate.PitchJitter` 0.05 pinned by FeedbackGateTests | Unify on **±6%** for every cue. P4-3a updates `FeedbackGateTests` in the same commit |
| 4 | PRG-04 "친구 선택권 (영웅 이하)" vs the roster (all ≤Epic lineages are starters or the attendance day-7 friend, so the ticket would always be a duplicate) | Ticket cap is **Legendary (전설 이하)**. For most players it means "pick whose 기억편 you get" (Legendary duplicate = 40). Documented deviation |
| 5 | Q-05 synergy max-tier reward 50 (research) vs gem inflation | **30** (§6.2 budget) |
| 6 | CR-10 reward '별조각 20×단계' vs inflation (7 lineages × 1,080 = 7,560 gems) | **10 × level** (540 per lineage, 3,780 lifetime) |
| 7 | Q-01 quest 22 already merges ChapterChest(1) contents vs PRG-06 chapter chest | Quest 22 keeps only 별조각 100. The chest content moves to ChapterChestSystem, so nothing is granted twice (P4-8). **The quest-22 content change merges in the same commit as the chapter chest, never alone.** If the chest were cut, the chapter-1 materials would silently disappear. That is why the chapter chest is P1 core (§8.5) |
| 10 (critic) | PRG-02 "field-top boss HP bar 900×28" vs UX M11 (boss HP lives in the StagePanel: '보스 HP' + %) and the crowded top HUD map (UX §2.1) | Keep M11. Add only what M11 lacks: a 50% enrage tick on the StagePanel bar and the boss's own bar, plus a one-line gimmick hint through `BattleNoticeView` (§13.1). No new 900×28 bar |
| 11 (critic) | CR-01 ring "shows 6 s, hides when the stage restarts" vs `BattleConfig.stageEndPause` = 1.5 s (the ring would be visible for ≈0.3 s) | The ring extends the end pause (`ExtendEndPause`), at most once per stage per 10 min (§7.2) |
| 12 (critic) | FX-04 "synergy chip line at the field top for 1.2 s" vs today's SynergyBannerView band at the field bottom (2.6 s, shown only when the set of active synergies changes) and the M43 chip punch | Accepted deviation, no work: it already shows only on change, and the top HUD has no free row |
| 13 (critic) | SET-01 "튜토리얼 다시 보기" vs a coach-mark replay that would re-grant FTUE rewards and move save state | `IntroRow` replays only the story intro (§4.1). Replaying the coach marks is out of scope |
| 8 | Achievement and codex progress before their features unlock | Progress is **recorded from the start**. Claims are allowed only after unlock, so the first visit shows an intentional burst of claimables |
| 9 | CR-03 stats button vs HUD map | StageArea TopRight(-24,-302), 64 visual / 88 hit, under IdleChestWidget (screen ≈ y 492–556). It must not overlap any guardian pad rect at ≥16 px clearance (capture check §11) |

### 2.12 Gate and red-dot summary (every item)
| Item | Opens at | Red dot / badge |
|---|---|---|
| ATK-06/07/08, FX-06, AUD-01, CR-07 | always (FX-06 also follows the setting) | – |
| IDLE-06 | sleep setting (default 2 min) | – |
| SET-01 new rows | always; IntroRow disabled during FTUE | – |
| SAVE-01, EVT-02 | always (Settings › 계정·데이터) | – |
| NOTI-01 | soft ask after `ftue.done` on the first full basket | – |
| CR-09 | dev menu only (editor / Development Build) | – |
| F-03 guards | per §5.1 | existing FeatureDot leaves |
| IDLE-04 ladder / resend-all | ladder after `ftue.done`; resend-all at ExpeditionBatch 2-1 | existing expedition-done dots; NEW pill on ResendAll until first use |
| IDLE-05 batch sweep | DungeonBatchSweep (floor 5) | FeatureDot `tab.expedition.feature.DungeonBatchSweep` |
| Q-04 | Achievements 2-3 | `top.journal.records` |
| Q-05 | Codex 1-10 | `tab.characters.codex` + NEW pill on CodexButton |
| CR-10 | levels always; pet after `ftue.done` | `tab.characters.lineage.{id}.affinity` |
| PRG-04 | with Gacha 1-3 | `tab.gacha.milestone` |
| PRG-05, PRG-01 extras, CR-04 | with their screens | – |
| CR-01 | after `ftue.done` | – |
| CR-02 | LampPolicy 1-6 | none (card + one MetaPulse, M09) |
| CR-03 | BattleStats 1-7 | none (one MetaPulse) |
| CR-05 | story: first clear of each x-10 after `ftue.done`; map: StagePanel tap after `ftue.done` | – (session popup) |
| CR-06 | `ftue.done` + ≥72 h away | – (startup popup) |
| EVT-01 | Events (3-1 or install day 3) | `top.event.claim`, `top.event.new` (on EventChip) |
| Q-06 | Events | `top.event.season` |
| PRG-06 | lost chest after `ftue.done`; chapter chest on the first clear of each x-10 | – (session popup) |
| PRG-07 | Dungeon 1-10 + floor 3 | `tab.expedition.dungeon.blessing` |
| PRG-02 boss hint (§13.1) | always; first encounter of each boss, and every retry after a fail | – |
| PRG-03 advisor + cheer (§13.2) | with the M13 sheet; cheer after `ftue.done`, once per chapter | none (sheet entry points only) |
| NEW-01 첫 주 (§13.6) | Missions (1-7) unlock, if install day ≤ 7; lasts 14 DayKeys | `top.journal.starter` |
| NEW-02 오늘의 작은 선물 (§13.7) | Shop (1-8) | `tab.shop.free` |
| NEW-03 반딧불 (§13.8) | after `ftue.done` | none |
| NEW-04 ticket bank (§13.9) | with Dungeon (1-10) | existing `dungeon.tickets` leaf, rule changed to "bank ≥ 3" |

---

## 3. Battle presentation, performance and audio

### 3.1 ATK-06 remainder: numbers, budget proof, tier→sound (P4-3b, P4-9, P4-3a)
**T3 skill numbers.** Today `BattleView.Attack.NumberKind` returns `Normal` for `HitTier.Skill`.
- Add `DamageNumberKind.Skill` (append, §2.2).
- `NumberColor(Skill)` = `Color.Lerp(look.Accent, UITheme.Cream, 0.25f)` of the caster's lineage. Use `AttackFxCatalog.Get(lineageId).Accent`; fall back to `UITheme.Lavender`.
- `NumberScale(Skill)` = 1.2. Merge window 0.12 s, the same as Normal.
- `BattleFx.Number(...)` gains an optional `Color? tint` parameter. Only Skill uses it.
- Precedence: Crit > Mark > Skill > Boss > Heavy > Normal. A boss hit by a skill shows the Skill colour at 1.2.
- `DamageNumbers = CritOnly` hides Skill numbers, like Normal.
- Tests (extend `AttackFxCatalogTests`): `NumberKind_Skill_UsesLineageAccent`, `NumberKind_CritBeatsSkill`, `CritOnly_HidesSkill`.

**Budget proof (performance gap 1).** Unmeasured today. P4-9 builds the harness (§11.3):
- `Verify/Runtime/PerfProbe.cs`, development players only, started by AutoShot `-perfBench`.
- `BattleSystem.DebugStress(int enemies = 20, float hpScale = 40f)` (P4-3b): `#if UNITY_EDITOR || DEVELOPMENT_BUILD` only. It runs a dev formation with 5 guardians (one per lineage and style mix) and a 5-echo deck, pins Speed 2, and keeps 20 enemies alive through HP scale. Release builds never compile it.
- Sampling: 10 s warm-up, then 30 s of `ProfilerRecorder` on `Main Thread` frame time, marker `Canvas.BuildBatch` (+ `Canvas.SendWillRenderCanvases`), `GC Allocated In Frame` and `Batches Count`. A recorder with `Valid == false` is logged as "marker n/a" and is not a failure.
- Writes `perf_{scenario}.csv` plus `perf_summary.json` to `-verifyOut`.
- Desktop acceptance (dev Windows player, 1080×1920 window, the proxy gate): frame p95 ≤ 8 ms; BuildBatch avg ≤ 0.6 ms; GC per frame avg ≤ 64 B; particle budget never exceeded; 0 exceptions.
- Device acceptance (manual, user): mid-range Android (Snapdragon 7-series class) at 2×: ≥55 fps avg, BuildBatch ≤ 2 ms.
- Run each scenario twice: atlases off and atlases on (§3.3). The summary shows the batch-count delta.

**Tier→sound map.** P4-3a implements it through `BattleSfx` (§3.5):

| Tier | Sound | Volume (cue × tier) | Pitch | Extra |
|---|---|---|---|---|
| T0 Normal | lineage hit cue | ×1.0 (cue base 0.45) | ±6% | lineage min interval 0.06 s (0.09 s at 2×) |
| T1 Heavy (snipe/assault/shield break) | lineage hit cue | ×1.41 (+3 dB) | ±6% | snipe also plays `fire_*` (full set) |
| T2 Crit | lineage hit + `hit_crit` ('팅') | crit 0.7 | ±4% | `hit_crit` at most once per 0.12 s |
| T3 Skill | `skill_cast` on cast (big skills only) | 0.5 | ±6% | per-skill hits reuse the lineage hit at ×0.8 |
| T4 Kill | `death_{dust,moth,metal,shadow}` at KillHold end | 0.55 | ±6% | ≤4 kill sounds per second |
| T5 Big | `lamp_lit`, `boss_appear`, `stage_clear`/`jingle_chapter_clear`, `stage_fail` | 0.9–1.0 | none | BGM duck 0.6 for 0.8 s |

### 3.2 ATK-07 item 7: visual verification (fallback owner P4-9)
The attack-FX workflow may close this before phase 4 (its `fx_capture.ps1` baseline failed on 2026-11-03 with "FxGalleryView / BattleFx not found — rebuild scene"). If it is still open when 4.1 starts, P4-9 owns it.
**Status correction (critic C-14):** a later run of the same baseline succeeded. `fx_out/baseline/capture.log` logs `[Verify] FxGallery: 180장`, and `fx_out/baseline/fx/` holds 180 crops plus `index.txt`: 84 guardian (7×6×2), 14 echo, 18 strike, 9 death, 7 crit, 33 skill, 4 spawn, 3 blackout, 2 shield, 2 pulse, 2 siege, 1 minion, 1 lamp. `fxdir/sheet_fx.png` is a first contact sheet. **Steps 1 and 2 below are done.** Keep them only as a regression check (re-run `fx_capture.ps1`, expect 180). What is still open: step 3 (silhouette sheet + blind verdict), step 4 (in-build accessibility lines) and step 5 (empty-VFX proof).
1. **Capture fix.** `Editor/FxGallery.Capture` must not depend on a scene that happened to be built with the gallery. Either `BatchBuildAll` always builds an inactive `FxGalleryView` under `Battle/Dev/FxGallery` (P4-3b adds it in BattleSceneBuilder, inactive, raycast off), or `Capture` instantiates the view itself after loading Main.unity. Pick the first: the builder owns the scene.
2. **Crops.** Per lineage × style (0 and 2 tier variants), the frame at arrival +0.05 s cropped to 200×200 → `fx_out/<run>/fx/{lineage}_{style}_{tier}.png` (7 × 6 × 2 = 84), plus enemy strikes (9), deaths (4), shield/crit (2), pulses (2) and lamp (1).
3. **Review sheet.** `scratchpad/phase4/fx_review/sheet.png` (PIL, 7 columns = lineages). Plus `silhouette.png`: the same crops grayscale + 4 px blur. Blind check: a reviewer names all 7 lineages from `silhouette.png`; the result goes to `fx_review/verdict.json` `{lineage: guessed}`. Pass = 7/7.
4. **In-build accessibility proof.** The AutoShot dev player logs `[Verify] shake_off_max_field_offset=0.0` (ShakePercent 0, LampLit forced) and `[Verify] reduce_flash_max_alpha<=0.30` (ReduceFlash on). Both lines are asserted by `verify.ps1`.
5. **Empty-VFX proof.** In a copy (`fx_proj`), delete `Art/VFX/*.png`, run `BatchBuildAll`, build the dev player, battle for 30 s. Pass = 0 exceptions in Player.log and ≥1 `[SkillVfxLibrary] fallback` log line.

### 3.3 ATK-08: extra art and atlases (P4-10 art, P4-3b wiring, P4-9 atlases)
| Asset | Use | Wiring | Fallback |
|---|---|---|---|
| `Art/VFX/enemy_death_yarn.png` 256² | `enemy_tangle` death (family stays Dust) | `EnemyFxCatalog`: per-enemy `DeathKeyOverride = "enemy_death_yarn"`; `EnemyFxBuilder` bakes it if present | today's dust + wine tint |
| `Art/Battle/shop_crack_{1,2,3}.png` 768² (aligned to `Battle/shop.png`) | shop damage states | `ShopView._crackOverlays[3]` (children of the shop image, same rect, raycast off). Thresholds HP ≤66% / ≤33% / ≤10%; fade in 0.3 s; fade out only when HP rises 5% above the threshold (hysteresis); reset on stage start | today's red tint and embers only |
| `Art/VFX/fx_rust_stain.png`, `fx_moth_hole.png` 64 px (from a 512 sheet cell) | Rusting sticker on the guardian pad; FabricEater gnaw on a fabric echo | replace the procedural `RustStain` / `GnawMark` sprites when present | procedural |

**Sprite atlases (P4-9, `Editor/Builders/AtlasBuilder.cs`, run from `IdleProjectSetup.BuildAll` after art import):**
- Packing is off today: `EditorSettings.spritePackerMode = 0` (Disabled). Set it to Sprite Atlas V2 in `IdleProjectSetup` (prefer V2 enabled-for-builds, if this editor exposes it, so editor import stays fast).
- Create V2 atlases with `UnityEditor.U2D.SpriteAtlasAsset` + `SpriteAtlasImporter`. Verify the exact V2 API on verify_proj first; if it differs, fall back to the V1 `SpriteAtlas` + `SpriteAtlasExtensions.Add` API.
- `Generated/Atlases/Atlas_BattleFx`: `Art/VFX/atk_*`, `enemy_*`, `motif_*` (≈54 sprites, ≈2.8 M px). 2048², padding 4, no rotation, **no tight packing** (FullRect sprites are rotated and aligned), mipmaps on, trilinear, CompressedHQ (Android ASTC 6×6).
- `Generated/Atlases/Atlas_SkillVfx`: `Art/VFX/vfx_*` (35 × 512²). 4096² Standalone; Android override 2048 (auto downscale ⇒ effective 256²; visual check).
- Excluded: `overlay_*` (1080×810), `Art/FX/*` (uncompressed glows; an atlas would compress them and add banding), 9-slices, characters and enemies.
- Acceptance: PerfProbe `Batches Count` falls ≥25% on the stress scenario; no visible change in the FX gallery crops (pixel diff ≤ 2% per crop vs atlases-off).

### 3.4 FX-06: skill-name restraint (P4-3b)
**Behaviour.**
1. `GameSettings.SkillNames == false` → no skill name notices (the `BattleNoticeView.Show` skill path in `BattleView.Skills.cs` ~:280) **and** no cast-line bubbles from `SkillCalloutView`. Gimmick tips ('좀나방은 천을 갉아요!'), enrage notices and CR-10 pet lines are not skill names and stay.
   - Re-read on `GameSettings.Changed`.
   - Settings row desc becomes '스킬 이름표와 대사 말풍선을 보여 줘요' (P4-4).
2. **Global admission gate** before M41's own rules. New pure class `UI/Battle/CalloutGate.cs`:
   - At most **1 admitted callout per 1.2 s**.
   - Same `skillId` at most once per **10 s**.
   - Priority: P0 = caster lineage grade ≥ Legendary, or `IsBigSkill`; P1 = first time this skill is seen (`Codex.HasSeenSkill`, persisted in `CodexSave.seenSkills`; mark it on admission); P2 = the rest.
   - One pending slot: a request inside the 1.2 s window waits ≤0.6 s and is replaced by a higher priority. P2 requests are never queued.
   - The big-skill notice keeps its own 30 s `AllowBig` rule. A notice also consumes the gate window.
3. M41 (dedupe 2.5 s, ≤3 alive, separation, guide-zone lift) runs after admission, unchanged.

**API.** `bool CalloutGate.TryAdmit(string skillId, int priority, float now, out bool queued)`; `bool CalloutGate.TryTakePending(float now, out PendingCallout c)`; `const float Interval = 1.2f, SameSkill = 10f, PendingLife = 0.6f`.
**Tests (`CalloutGateTests`):** 1.2 s spacing; same skill blocked for 10 s; a P0 replaces a pending P2/P1; pending expires after 0.6 s; first-seen marks once; the OFF setting blocks all; gimmick notice unaffected.

### 3.5 AUD-01 + Critic-A3: sound system (P4-3a runtime, P4-3b battle hooks, P4-10 clips)
**Architecture.** One `[Audio]` DontDestroyOnLoad object. `UISfx` stays as the static facade the UX hooks already call (`UISfx.Play(UISfxKey, float)`, `UISfx.Boot()`, `UISfx.ApplySettings()`) and forwards everything to `AudioSystem`.

| Part | Spec |
|---|---|
| Files | `Scripts/Core/Audio/{AudioSystem, AudioCueTable, AudioKeys, SfxGate}.cs` (new), `Core/UISfx.cs` (facade), `Core/UISfxTable.cs` (kept for compatibility, unused at runtime), `Editor/Audio/{AudioTableBuilder, AudioContent, AudioImportPostprocessor}.cs` (new) |
| Buses | Code buses, **no AudioMixer asset** (a builder cannot create one through public API). Bus gain = volume × SoundOn × duck(bus). Bgm = BgmVolume; Sfx = SfxVolume; Ui = SfxVolume × 0.9 |
| Voices | 12 pooled 2D AudioSources for Sfx+Ui (Ui may hold at most 4); BGM A/B pair (crossfade 1.2 s); 1 sting source (jingles, which duck BGM) |
| Gate (`SfxGate`, cue-indexed arrays, no alloc) | same cue ≥40 ms apart; ≤3 voices per cue; pitch jitter ±6% (cue override); CoinLand rise +3%/landing within 0.3 s, cap +18% (kept); battle token bucket ≤10 new battle starts per second |
| Priority | cue priority 0–255 (lower = more important). Pool full → steal the playing voice with the highest priority number (oldest on ties). Never steal priority ≤20 (stings, lamp, boss, Deny, Claim) |
| Ducking | `Duck(bus, gain, holdSeconds, fade)` with reason stacking (min gain wins). Sleep: Bgm 0.35, Sfx 0.4 until wake. 4★+ gacha reveal: Bgm 0.4 for 1.8 s. Lamp / stage-clear jingle: Bgm 0.6 for 0.8 s. **Never on focus loss** (M01); BGM fades 0.2 s only on `OnApplicationPause(true)` |
| BGM | `bgm_battle_night` default loop. `bgm_boss` only on chapter-boss stages (x-10, BossSpawned): crossfade in, then out 2 s after clear or fail. Jingles via the sting source |
| Table | `Resources/Audio/AudioCueTable.asset`, built by `AudioTableBuilder` during `BuildAll` from `Assets/_Project/Audio/{Sfx,Bgm}/{key}.wav` (variants `{key}_2.wav` …) plus per-key params from `AudioContent` (bus, volume, priority, maxVoices, minInterval, pitchJitter, loop). A missing file means a silent cue and one builder warning |
| Import | `AudioImportPostprocessor` (Assets/_Project/Audio only). SFX: mono, Vorbis q0.7, DecompressOnLoad for <1 s clips, CompressedInMemory otherwise. BGM: stereo, Vorbis q0.55, Streaming, loadInBackground. Sample rate preserved (44.1 kHz) |
| API | `AudioSystem.Boot()`; `int Resolve(string key)` (-1 = missing; callers cache it); `Play(int cue, float volume = 1, float pitch = 1)`; `Play(string key, float volume = 1)` (pre-built dictionary, no alloc); `PlayBgm(string key, float fade = 1.2f)`; `StopBgm(float fade = 0.8f)`; `Duck(AudioBus, float gain, float hold, float fade = 0.25f)`; `Unduck(AudioBus, float fade = 0.4f)`; `ApplySettings()`; `bool HasClips` |
| Battle scheduler | `UI/Battle/BattleSfx.cs` (P4-3b): fixed 32-slot delayed queue (`Schedule(cue, delay, vol, pitch)`) ticked by BattleView in battle time, so impact sounds land exactly when the hit pops (ATK principle 3). Resolves each catalog `Sfx` string once at Start |
| Lalabell | `atk_musicbox` pitch steps cycle through semitones {0,2,4,5,7,9}; the 7th degree (11) is never played (the lore of the missing last note) |
| Settings | Once `AudioSystem.HasClips`: re-enable the BGM/SFX sliders, remove the M18 '준비 중' pills (P4-4), and preview `ui_tap` at most once per 150 ms while dragging |

**Hook points (P4-3b in `BattleView*`, P4-5/P4-6 in their popups):**

| Event / place | Cue | Notes |
|---|---|---|
| GuardianAttack impact (Arrival) | `LineageAttackLook.Sfx` (`atk_needle` … `atk_musicbox`; `atk_generic`) | via `BattleSfx.Schedule(cue, flightDelay)`; tier volume §3.1 |
| Snipe/assault launch | `fire_{needle,button,key,candle,rain,kiss,musicbox}` (full set only) | volume 0.35 |
| Echo basic impact | lineage hit cue ×0.65 | echoes are small and translucent |
| CriticalHit | `hit_crit` | layered on the hit |
| EnemyStrike | `EnemyAttackLook.Sfx` (`enemy_puff` … `boss_vortex`) | ×0.4 normal, ×0.9 boss |
| ShopDamaged | `shop_hit` | ≤1 per 0.15 s; boss siege ×1.3 |
| EnemyKilled | `death_{family}` | at KillHold end; ≤4/s |
| ShieldGained/Broken (enemy) | `shield_block` / `shield_break` | |
| EchoSummoned / EchoDissolved | `echo_summon` / `echo_dissolve` | min interval 0.12 / 0.15 s |
| GuardianPulse | `guardian_pulse` | 0.25 |
| LampLit | `lamp_lit` + duck | |
| BossSpawned | `boss_appear` (+ `bgm_boss` on x-10) | |
| StageCleared | `stage_clear` (x-10 first clear: `jingle_chapter_clear`) | |
| StageFailed | `stage_fail` | |
| Boss kill moment (`BossKillMoment`) (critic) | `boss_defeat` ☆ (T5; BGM duck 0.6 for 0.8 s) | the research's "보스 처치" sound; v1 had no cue for it |
| `enemy_windup` death (critic) | `death_spring` ('보잉', replaces `death_metal` for this enemy only) | ATK-04 gap |
| Firefly appear / catch (§13.8) | `firefly_appear` (0.25) / `firefly_catch` | ≤1 appear per spawn |
| Cheer start (§13.2) | `cheer_start` | |
| Gacha bundle drop/open, 4★+ reveal | `gacha_bundle_drop`, `gacha_bundle_open`, `jingle_gacha_high` + `AppSignals.RaiseGachaRevealHigh` | P4-5 |
| Chest / basket / quick idle / unlock card / achievement / pet | `chest_open`, `basket_claim`, `quick_idle`, `unlock_card`, `achievement_claim`, `pet_squeak` | P4-6 / P4-3b |

**Tests:** `FeedbackGateTests` (updated to ±6%); `SfxGateTests` (interval, voices, token bucket, priority steal, never steal ≤20); `AudioDuckTests` (stacking, min wins, unduck); `BattleSfxTests` (delay ordering, 32-slot overflow drops the lowest priority). Missing table = silent no-op (existing behaviour kept).

### 3.6 IDLE-06 remainder: sleep lowers FX and sound (P4-6, consumers P4-3a/b)
- `SleepModeView` calls `AppSignals.RaiseSleep(true)` when the overlay finishes fading in and `RaiseSleep(false)` on wake. It keeps the M01 handlers.
- `BattleView` subscribes and calls `BattleFx.SetSleepMode(bool)` (P4-3b): particle budget becomes `min(budget, 60)`, damage numbers are suppressed, sparkle/extra tiers are dropped, overlays stay. Restore on wake without a toast.
- `AudioSystem` subscribes: `Duck(Bgm, 0.35, -1)`, `Duck(Sfx, 0.4, -1)`; `Unduck` on wake (0.4 s).
- `FxQualityMonitor` keeps ignoring sleep frames (it already treats the 30 fps target as not slow).
- Tests: `SleepSignalTests` (raise/unraise pairs; wake restores budget).

---

## 4. Settings, data safety and platform

### 4.1 SET-01 remainder (P4-4: `SettingsSceneBuilder.cs`, `SettingsPopup.cs`)
UX WP-4 delivers M74/M80/M53/M64/M22 copy and styling plus the M18 '준비 중' pills. SPEC4 adds or changes only these rows (RowHeight 96, RowStep 108, desc Hint 20; buttons per UX §1.6):

| Tab | Row (`name`) | Label / desc | Control | Behaviour |
|---|---|---|---|---|
| 일반 | `BgmRow`, `SfxRow` | unchanged | slider | live when `AudioSystem.HasClips`; pills removed (§3.5) |
| 일반 | `IntroRow` (new, after TutorialRow) | '처음 이야기 다시 보기' / '수선점의 첫 이야기를 다시 볼 수 있어요' | Secondary 180×80 '보기' | close Settings → `UINav.ReplayIntro()`; the StoryIntroView replay gives no rewards or flags. Disabled during FTUE (the intro is part of it) |
| 전투 연출 | `SkillNameRow` | desc → '스킬 이름표와 대사 말풍선을 보여 줘요' | switch | wired (§3.4) |
| 알림 | `NotifyStatus` (replaces the 'Desc' text) | Granted: '알림이 켜져 있어요' · Denied: '기기 설정에서 알림이 꺼져 있어요' + Primary '설정 열기 ›' · Unknown/SoftDeclined: '아직 알림을 허용하지 않았어요' + Primary '허용하기' | text + button | §4.3 |
| 알림 | `NotifyRow0..3` | unchanged copy | switch | live (planner reads them) |
| 알림 | `QuietNote` (new, bottom) | '밤 10시부터 아침 8시까지는 알림을 보내지 않아요.' (Hint 20 SubText) | – | – |
| 계정·데이터 | `SaveButtons` | Export '내보내기 (복사)'; Android adds '공유' (Secondary); Import '가져오기' | 3 buttons on Android (row of 3 × 280×96), else 2 | §4.2 |
| 계정·데이터 | `BackupRow` (new, under SaveButtons) | '백업에서 복구' / '최근 백업: {MM월 dd일 HH:mm} · 스테이지 {s}' (or '아직 백업이 없어요') | Danger-tone Secondary 180×80 '복구' | §4.2 |
| 계정·데이터 | `CouponRow` (new) | '쿠폰 입력' / '받은 쿠폰 번호를 넣으면 우편으로 선물이 와요' | Primary 180×80 '입력' | opens `CouponPopup` (§4.5) |
Account page vertical budget: existing content ends at y≈918. BackupRow at y 712 (shift Divider2/Reset down 120) and CouponRow at y 832. Reset moves to y −1068 (90 tall). This fits the 940×1516 window (RM-2 P4-4; body ≈1326). Smoke-test names `Tab0..Tab3` stay unchanged.

### 4.2 SAVE-01: save code import, share and backup restore (P4-7 core, P4-4 UI)
**Core (P4-7):**
- `SaveCode` (keeps the `LL1.{crc}.{b64}` format) gains `bool TryParse(string code, out PlayerData data, out SaveCodeError error)`: TryDecode → `JsonConvert.DeserializeObject<PlayerData>(json, ReadSettings with TolerantEnumKeyDictionaryConverter)` → `data.Sanitize()`.
  - Errors: `Empty`, `BadFormat`, `Checksum`, `TooLarge` (> 4 MB decoded), `NewerVersion` (`data.version > CurrentVersion`, checked before Sanitize rewrites it), `Corrupt`.
  - It also returns a `SaveSummary { int highestStage; int shopLevel; int lineages; long savedUnix; string accountIdShort }`.
- `ISaveStorage` / `LocalSaveStorage` (P4-7) gain:
  - `void Replace(PlayerData data, string reason)`: writes the current main file to `player.json.{reason}-{unix}.bak` first ('import'), then saves `data`.
  - `void SnapshotDaily(string dayKey)`: called by GameBootstrap on the first save of each DayKey. Writes `player.json.day-{dayKey}.bak` and keeps the newest 3.
  - `IReadOnlyList<SaveBackupInfo> ListBackups()`: daily snapshots + import backups + the rolling `.bak`, newest first, each with a `SaveSummary`. Files that fail to parse are skipped.
  - `bool RestoreBackup(string path)`: makes an `import` backup of the current state, then copies.
- `GameBootstrap.ReloadAfterReplace()` (P4-7): sets `_resetting = true` (skips the autosave), reloads the active scene, and queues the toast '진행을 옮겨 왔어요' on the next boot (PlayerPrefs one-shot flag).
- `Core/ShareSheet.cs` (P4-7): `static bool CanShare` (Android only); `Share(string title, string text)` via `AndroidJavaObject("android.content.Intent")` ACTION_SEND text/plain + `createChooser`. Other platforms fall back to the clipboard.

**UI (P4-4):**
- `SaveImportPopup` (new, UI/Popups, `BuildPopup 820×900`, title '세이브 코드 가져오기'):
  - Body text '다른 기기에서 복사한 세이브 코드를 붙여 넣어 주세요.' (Body 26).
  - [클립보드에서 붙여 넣기] Primary 520×112 reads `GUIUtility.systemCopyBuffer`. No giant input field on mobile; the code can be 100 KB.
  - Preview card (`UiArt.PanelInset9s` 740×260): '스테이지 3-4 · 수선점 Lv.87 · 친구 6명' (Title 28) / '저장 2026-11-02 21:14 · ID 1a2b3c4d' (Caption 22).
  - Error lines (Negative 24): Checksum/BadFormat '코드가 잘렸거나 바뀌었어요. 다시 복사해 주세요' · NewerVersion '더 새 버전에서 만든 코드예요. 앱을 업데이트해 주세요' · TooLarge/Corrupt '이 코드는 읽을 수 없어요'.
  - [이 진행으로 바꾸기] DangerButton → `ShowDangerConfirm('진행을 바꿀까요?', '지금 진행은 백업으로 남겨 두고 코드의 진행으로 바꿔요.\n바꾼 뒤에는 게임이 다시 시작돼요.', '바꾸기')` → `Replace` → `ReloadAfterReplace`.
- `BackupRestorePopup` (new, 820×1000, '백업에서 복구'):
  - List rows 740×120: label ('어제' / '그저께' / '가져오기 전' / '마지막 저장 직전') + summary + Secondary '복구'.
  - Danger confirm '이 백업으로 되돌릴까요? 지금 진행은 따로 남겨 둬요.' → `RestoreBackup` → reload.
- Export: also shows the share button on Android. Copy '세이브 코드를 복사했어요' stays (M76 tone).

**Tests (`SaveCodeImportTests`, `SaveBackupTests`):**
- Round trip Export → TryParse equals the original (deep JSON compare after Sanitize).
- One flipped base64 char → Checksum.
- Garbage / empty / wrong prefix → BadFormat.
- version 99 → NewerVersion.
- 5 MB → TooLarge.
- Replace writes a backup that `ListBackups` lists.
- SnapshotDaily keeps 3.
- RestoreBackup round trip.

### 4.3 NOTI-01: local push notifications (P4-7 core and platform, P4-6 soft ask)
**Package.** `Packages/manifest.json` + `"com.unity.mobile.notifications": "2.4.3"`, the version released for Unity 6000.2 (2.4.2 is also listed). It uses the unified API in namespace `Unity.Notifications`:
- `NotificationCenter.Initialize(args)` with `NotificationCenterArgs.Default` + `AndroidChannelId`/`Name`/`Description`.
- `RequestPermission()` returns a yieldable request with `.Status` (`NotificationsPermissionStatus`).
- `ScheduleNotification(Notification, NotificationDateTimeSchedule)`.
- `CancelScheduledNotification(id)`, `CancelAllScheduledNotifications()`, `CancelAllDeliveredNotifications()`, `ClearBadge()`, `OnNotificationReceived`, `QueryLastRespondedNotification()`.

Project Settings › Mobile Notifications:
- Android: small icon `ll_notify_small` (white silhouette; §10.3), large icon `ll_notify_large`; "Reschedule on device restart" on. No exact alarms, so no SCHEDULE_EXACT_ALARM.
- iOS: "Request Authorization on App Launch" **off** (we ask in-game).

**Isolation, so the dotnet compile check keeps working.**
- New asmdef `Scripts/Platform/IdleGame.Platform.asmdef`: references IdleGame + the package's unified runtime assembly (verify its exact name in `Packages/com.unity.mobile.notifications/Runtime/Unified/*.asmdef`); `includePlatforms` Android + iOS; `versionDefines` `com.unity.mobile.notifications` → `LL_NOTIFICATIONS`.
- It contains only `MobileNotificationBackend : INotificationBackend`, registered with `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] → LocalNotifications.Backend = new MobileNotificationBackend()`.
- The IdleGame assembly never references the package. `compile_check.py` builds only the listed assemblies, so the Platform asmdef compiles only in the Unity batch on verify_proj (§11).

**Core (IdleGame):**
```csharp
public interface INotificationBackend { bool Initialize(); NotificationPermission Permission { get; }
    IEnumerator RequestPermission(Action<NotificationPermission> done); void Schedule(in PlannedNotification n);
    void CancelAll(); void ClearDelivered(); void OpenSystemSettings(); }
public static class LocalNotifications   // Core/LocalNotifications.cs — null backend on editor/PC (dev log only)
{ public static INotificationBackend Backend; public static void Reschedule(IReadOnlyList<PlannedNotification> list);
  public static void CancelAll(); public static NotificationPermission Permission { get; } }
public struct PlannedNotification { public NotificationKind Kind; public int Id; public DateTime FireLocal; public string Title; public string Body; }
public static class NotificationPlanner   // Systems/Meta/NotificationPlanner.cs — pure, EditMode-tested
{ public static void Plan(in NotificationContext ctx, List<PlannedNotification> into); }
```
`NotificationContext` (filled by GameBootstrap on pause/quit) holds:
- nowLocal; ftueDone; per-kind enabled (`NotifyMaster && GetNotify(kind)`)
- `basketFullLocal` (lastClaim + cap; null if locked or already full)
- `lastExpeditionDoneLocal` (max endUnix of running expeditions, null if none)
- `attendancePending` (`Attendance.CanCheckIn`)
- `dungeonUnusedToday` (`Dungeon.HighestFloor ≥ 1 && TicketsLeft == DailyTickets`)
- `nextResetLocal` (DailyClock.NextDailyResetUtc → local)

**Rules (in priority order):**

| Kind (Id) | Fire time | Condition | Title / body |
|---|---|---|---|
| IdleChestFull (1001) | basket full time | IdleChest unlocked, not already full, ≥15 min ahead | '수선 바구니가 가득 찼어요' / '친구들이 모은 선물이 기다려요. 받으러 와 주세요!' |
| ExpeditionDone (1002) | last running expedition end | ≥15 min ahead | '원정 간 친구들이 돌아왔어요' / '재료를 한 아름 안고 왔어요. 받아 볼까요?' |
| Attendance (1004) | today 20:00, or tomorrow 20:00 if now ≥ 20:00 | attendance pending at pause time | '오늘 출석 도장을 아직 안 찍었어요' / '수선점에 들러 도장을 꾹 찍어 주세요.' |
| DungeonTickets (1003) | next 05:00 reset → shifted to 08:00 by quiet hours | `dungeonUnusedToday` (no tickets used today). If NEW-04's ticket bank ships (§13.9): `bank + 3 ≥ cap` instead | '잔향이 던전 입장권이 채워졌어요' / '오늘의 입장권으로 귀한 재료를 찾아요.' (bank: '잔향이 던전 입장권이 가득 찼어요' / same body) |

Common rules:
- **Quiet hours** 22:00–08:00 local: a fire time inside moves to 08:00 the next morning.
- **Max 3 pending:** keep the 3 earliest after priority. Space them ≥30 min apart (push the later ones).
- No notifications before `ftue.done`.
- On resume: `ClearDelivered` + `ClearBadge`. On pause/quit: `CancelAll` then schedule the plan.
- Android channel: id `ll_reminders`, name '수선점 소식', description '바구니·원정·출석 알림', importance Default.

**Permission flow.**
1. Never ask during FTUE.
2. The first time after `ftue.done` that `IdleChest.IsFull` turns true while the app is running (or right after the first Return popup that had a full basket), enqueue the session request `MetaPopupRequest.NotificationAsk()` (between stages only, §2.9). Conditions: Permission is Unknown, `NotifySoftAsks < 2`, ≥7 days since `NotifyLastAskUnix`. P4-6 does the detection in `MetaPopupPresenter` (a 1 s poll of `Game.IdleChest.IsFull`, no allocation).
3. `NotificationAskPopup` (P4-6, 760×600): art `Art/UI/idle_basket_3`, title '알림을 보내 드릴까요?', body '바구니가 가득 차거나 원정이 돌아오면 살짝 알려 드릴게요.\n밤 10시부터 아침 8시까지는 보내지 않아요.' Buttons [괜찮아요] Secondary (left) / [알려 주세요] Primary (right).
4. '알려 주세요' → `Backend.RequestPermission` → Granted saves Granted; Denied saves Denied. '괜찮아요' → SoftDeclined, `NotifySoftAsks++`.
5. The Settings button (§4.1) re-asks; when Denied it opens the system settings intent (`android.settings.APP_NOTIFICATION_SETTINGS` with `EXTRA_APP_PACKAGE`).

**Tests (`NotificationPlannerTests`):** quiet-hours shift; ≤3 and 30-min spacing; priority order; "now + <15 min" skipped; attendance after 20:00 goes to tomorrow; dungeon only when unused; FTUE → none; disabled kind → none; basket already full → none.

### 4.4 CR-09: local play log (P4-7)
- `Core/PlayLog.cs`: appends JSON lines to `persistentDataPath/playlog.jsonl` and rotates to `playlog.1.jsonl` at 1 MB (2 files). It uses one reused StringBuilder plus a buffered writer, flushes every 10 s and on pause/quit, and never writes per frame.
- Events and fields:
  - `session_start {v, build, stage, power}` / `session_end {sec}`
  - `ftue_step {n, t}`
  - `stage_end {stage, res, pow, rec, sec, hp, dir[5]}` (dir = shop damage per slot from §7.2's analyzer)
  - `gacha {n, grades[]}`, `quest_claim {id}`, `mission_box {period, pts}`, `basket_claim {h, double}`, `quick_idle {paid}`, `feature_unlock {id}`, `event_claim {id}`
- Subscribers live in `PlayLogRecorder` (Core, connected in `GameBootstrap` after `Game.Setup`).
- Dev menu (Settings 개발 tab, P4-4): '플레이 기록' → MessagePopup summary: '최근 24시간 · 스테이지 클리어 42 · 실패 3 · 시간당 6.1스테이지', plus the first-10-minutes table of ftue_step deltas. [내보내기] shares or copies the last 500 lines. No automatic upload, no personal data.
- Tests (`PlayLogTests`): rotation at the size limit; line format is valid JSON; flush on pause; disabled when `persistentDataPath` is unwritable (log a warning, never throw).

### 4.5 EVT-02: offline coupon codes (P2 stretch; P4-2 system, P4-4 popup, P4-8 content)
- Normalize: trim, uppercase, remove spaces and '-', require `^[A-Z0-9]{6,16}$`. Hash = lowercase hex `SHA256(UTF8(couponSalt + ":" + code))` (System.Security.Cryptography; no alloc concerns, it runs once per tap).
- `CouponSystem.TryRedeem(string input) → CouponResult { Ok, Invalid, AlreadyUsed, OutOfPeriod, Locked, Tampered }`:
  - Not found → `failStreak++`; 5 misses → lock 60 s.
  - Tampered clock → Tampered.
  - Ok → `redeemed.Add(hash)`, `Mail.Send(def.mailTitle ?? '쿠폰 선물이 도착했어요', def.mailBody, def.rewards, expireDays 30)`. Rewards are RewardData only (currency/items), because mail cannot carry meta wallet items.
- `CouponPopup` (P4-4, 820×560, '쿠폰 입력'):
  - TMP_InputField 600×96 (uppercase, 16 chars, `contentType Alphanumeric`), placeholder '쿠폰 번호'. [받기] Gold 520×112.
  - Feedback (Caption 22): Ok '쿠폰 선물을 우편함에 넣어 두었어요' (Success toast + close) · Invalid '쿠폰 번호를 다시 확인해 주세요' · AlreadyUsed '이미 받은 쿠폰이에요' · OutOfPeriod '사용 기간이 아닌 쿠폰이에요' · Locked '잠시 뒤에 다시 입력해 주세요' · Tampered `DailyClock.TamperedMessage`.
- Content: `MetaContent.Coupons.cs` stores **hashes only**. Editor menu 'IdleGame/쿠폰 해시 만들기' (P4-8, `MetaDataBuilder`) prints a hash for typed plaintext and never saves the plaintext. Ship with **one QA code** whose plaintext stays outside the repo. Real marketing codes are the user's decision (outward-facing).
- Tests (`CouponTests`): normalize; hash stable; one-time; date window via DayKey; lockout after 5; tamper.

---

## 5. Gates and automation

### 5.1 F-03 enforcement inside open tabs (UI P4-4/P4-5; guards P4-1/P4-2)
The rule: a locked action shows its lock in place, explains it with the M39 sentence, and **the system refuses it** even if a UI path slips through.

| Feature (unlock) | UI where it leaks today | Locked UI (owner) | System guard (owner) |
|---|---|---|---|
| CharacterLevelUp (1-1) | `GrowthLineageCardView` 레벨업 (live at FTUE step 2), `CharacterDetailPopup.LevelUp` | Button stays visible: `CostButtonView` Unavailable with label '1-1' pill (Micro 18 Gold on PillNight) and HoldRepeat disabled. Tap → Error toast `UnlockSentence(2)` + Light haptic (P4-4 card, P4-5 detail) | `CharacterSystem.TryLevelUp` returns false when `FeatureResolver(CharacterLevelUp) == false` (P4-1) |
| Dungeon (1-10) | `Seg_Dungeon` / DungeonPage (reachable from 1-4) | `Seg_Dungeon` α0.45 + 28 px lock + '1-10' pill; tap → toast, no page switch; `FocusDungeon` refuses while locked (P4-4) | `DungeonSystem.Challenge/Sweep/SweepMany` return null when locked (P4-2) |
| Evolution (1-10) | EvolutionRecipePopup '진화 제작 시작', EvolutionCraftPopup, CheckButton | The tree stays viewable (quest 23 needs it). CheckButton label 'UnlockShort(11) 열려요' (Cream 26), craft buttons Unavailable '1-10' (P4-5) | `EvolutionSystem.TryCraft` (and accelerate/substitute) return false when locked (P4-1) |
| Costumes (2-5) | `CostumeListView` equip/unlock | The list stays viewable; equip buttons Unavailable '2-5'; tab label gets a 24 px lock glyph (P4-5) | `CostumeSystem.TryEquip(lineageId, costumeId)` and `TryUnlock(costumeId)` refuse when locked. `CheckNightVisit` still records the visit flag, but the grant waits for unlock and fires on the first evaluation after 2-5 (P4-1) |
| Codex (1-10), Achievements (2-3), AutoChallenge (2-1), ExpeditionBatch (2-1), LampPolicy (1-6), Events (3-1/day 3) | no screens yet | Screens arrive in phase 4 and are gated at their entry (§6–§8). `CardsReady` set (§2.8) | their systems check `IsUnlocked` |
Tests: `FeatureGuardTests`: each guarded API refuses while locked and works when unlocked (resolver null = open, so existing tests stay valid). AutoShot capture 04 (FTUE step 2) shows the '1-1' pills (§11).

### 5.2 IDLE-05 remainder: automation unlock rewards (P4-8 cards, P4-2/P4-4 dungeon batch)
- Cards per §2.8: Speed2x, LampPolicy, AutoChallenge, ExpeditionBatch (+빠른 수선권 1), DungeonBatchSweep, Events.
- **Dungeon batch sweep** (DungeonFloor ≥5, `FeatureId.DungeonBatchSweep`):
  - `DungeonSweepPopup` gets a count stepper row under the floor row: `[-] ×{n}장 [+]` (n = 1..TicketsLeft, default all). The sweep button reads '{n}장 소탕' (Gold 520×112).
  - Locked: the stepper is hidden and the button reads '소탕' (n = 1) as today.
  - `DungeonSystem.SweepMany(floor, n)` uses n tickets, merges rewards, raises one `Changed` and counts n `DungeonSweep`.
  - Result: RewardPopup '{floor}층 {n}번 소탕 완료' / '잔향이들이 익숙한 길을 {n}번 다녀왔어요.'
  - Tests: `SweepMany_UsesNTickets_MergesRewards`, `SweepMany_Locked_ReturnsNull`, `SweepMany_ClampsToTicketsLeft`.

### 5.3 IDLE-04: timer ladder and resend-all (P4-2 system, P4-4 UI)
**Ladder.**

| Tier | Label | Duration | Reward scale | Affinity per member |
|---|---|---|---|---|
| 0 | '기본' | zone.durationMinutes | ×1 | 2 (today) |
| 1 | '길게' | ×3 | ×2.8 | 4 |
| 2 | '밤새' | ×6 | ×5.4 | 6 |
- A tier is allowed only if its duration ≤ 720 min (12 h). `exp_2_4` (180) therefore offers tiers 0–1 only.
- Ladder available after `ftue.done`. The FTUE first expedition stays tier 0 and short.
- Rewards: gold × scale. Each drop is rolled `floor(scale)` times plus one extra roll with probability `frac(scale)`, using the per-member chance bonus as today. Evolution `ExpeditionCount` progress is +1 per expedition regardless of tier (a choice: short runs advance evolution faster). Counters: ExpeditionStart/Claim +1.
- Save: `ExpeditionSave.durationTier`; `ExpeditionMembersSave.lastTier` (resend reuses it).
- Weekend event multiplier (§8.3) applies to drops at claim time.

**UI (P4-4).**
- `ExpeditionSetupPopup` gets a '다녀올 시간' row with an M53 Option segmented control of 3 segments (each 240×88): line 1 the duration via `MetaUi.Duration` ('30분' / '1시간 30분' / '3시간'), line 2 '보상 ×1' / '×2.8' / '×5.4' (Caption 22).
- A disallowed tier is hidden. Default = the zone's lastTier.
- The zone card's running state shows the tier chip '밤새' (PillNight 96×32) next to the timer.

**Resend all (ExpeditionBatch 2-1).**
- `ResendAllButton` (Primary 240×64, label '모두 다시 보내기', 28) sits in the **same rect as `ClaimAllButton`**. It is shown only when the claim-all count is 0 and `CountResendable() ≥ 1`. `ClaimAllButton` keeps its name and parent (FTUE step 14 coach path).
- Tap → `ExpeditionSystem.ResendAll()` resends each zone with `CanResend`, in zone order, with its lastTier, up to MaxConcurrent. Toast '원정 {n}곳을 다시 보냈어요' (Success).
- Locked → the button is hidden. After unlock and before first use it carries the NEW pill.

**Tests (`ExpeditionLadderTests`):** durations per tier; ≤720 cap; scale rolls are deterministic with a seeded Random (mean within 3% over 2,000 rolls); lastTier saved; ResendAll respects MaxConcurrent and skips ineligible members; affinity per tier.

---

## 6. Long-term collection

### 6.1 Q-04: 수선점 기록 (achievements) (P4-1 system, P4-6 UI, P4-8 content)
**Model.** Every tier is one `AchievementDef` (`id` = `{lineId}_{tier}`). The UI shows one row per line: its lowest unclaimed tier, or the last tier once everything is claimed. There are **17 lines with 62 tiers** (table in Appendix A.1), worth **3,400 별조각 over the game's lifetime**.
- **Progress** comes from `ProgressTracker.GetValue(condition)` (lifetime counters and state conditions) and is recorded from install. A tier can be claimed only when `Features.IsUnlocked(Achievements)` (2-3) is true and `!DailyClock.IsTampered`. The first visit therefore offers about 400 별조각 of early tiers at once (research intent: "1~2단계는 바로 받을 수 있게").
- **Hidden records** (5) are `MetaConditionType.Flag` conditions. AchievementSystem sets the flags from battle and meta events:
  - h1 `ach.hidden.rain_udu`: a Rain stage is cleared while udukeoni is a guardian (`StageEnded(Cleared)` + `Battle.CurrentStageModifier == Rain` + the formation contains an udukeoni form as a guardian).
  - h2 `ach.hidden.clean_boss`: an x-10 stage is cleared with 0 `ShopDamaged` events during it.
  - h3 `ach.hidden.three_lamps`: 3 `LampLit` in one stage.
  - h4 `ach.hidden.all_socks`: all 11 jjakjjak forms are unlocked.
  - h5 `ach.hidden.midnight`: an attendance check-in between 00:00 and 05:00 local.

**API (`AchievementSystem : IMetaModule`):**
- `event Action Changed`
- `bool IsUnlocked`
- `void GetLines(List<AchievementLineStatus> into)`. Sorted claimable → in progress (ratio desc) → done → hidden-unrevealed last.
- `int ClaimableCount`
- `List<MetaReward> TryClaim(string achievementId)`
- `List<MetaReward> ClaimAll()`
- `AchievementLineStatus { AchievementDef Current; int TierIndex; int TierCount; long Value; long Target; bool Claimable; bool AllDone; bool HiddenUnrevealed; }`
- A claim calls `MetaRewards.Grant` and `Progress.Increment(AchievementClaim)`. `RegisterRedDots` registers `JournalRecords`.

**UI (P4-6).** `JournalPopup` tab 3 '수선점 기록' replaces the `RecordsPanel` placeholder:
- **Summary card** Top(0,150), `FramedPanel` PanelLight@0.75. Medal icon 96 (Appendix A asset `medal_gold`, else `Icons/journal`) at MiddleLeft(24,0). '모은 기록 {claimed}/{62}' (H2 32 Cream bold). `UiArt.Bar` 520×20 Lavender fill at (140,-96). '받을 수 있는 기록 {n}개' (Caption 22 SubText) at the right.
- **Segments** [진행 중][완료] (M53 Option, 2×200×64) at Top(166,64).
- **List** Stretch(0,0,246,128). Rows reuse `MissionRowView`, bound through a new `Bind(in JournalRowModel model, Action<MissionRowView> onButton)`. `MissionStatus` binding stays as it is.
  - Row content: icon (category icon, table A.1) · title ('스테이지 3-10 클리어', Title 28 Text) · tier pips (`UiArt.PipButton` 14 px, step 18; Gold for reached, SubText@0.5 otherwise) · progress bar + '12,340/100,000' (Label 24 bold, `FormatCompact`) · reward chip `MetaRewardChip` · the points pill is hidden.
  - Buttons: [받기] Gold, [가기 ›] Primary when `navFeature != Battle`, [완료] Disabled.
  - Hidden-unrevealed rows show the title '???' and the hint in Hint 20 Lavender; the icon is `medal_hidden`.
- **Footer**: the existing `ClaimAllButton` (same object and coach path) is shown on this tab when `ClaimableCount > 0` and claims all records. The claim flies the rewards (`MetaUi.FlyRewards`) and plays `achievement_claim` + `PlayStamp` on the row.
- **Tab badge**: `_tabBadges[2]` (new third badge) shows a dot when claimable.

**Tests (`AchievementTests`):**
- Every line's tiers are strictly increasing.
- Locked → TryClaim returns null.
- A tier claimable before unlock becomes claimable at unlock (burst).
- ClaimAll claims in tier order and grants the summed rewards.
- Each hidden flag trigger has a scripted case.
- A tampered clock blocks claims.
- Total gem rewards == 3,400 (`EconomyBudgetTests`, §11.2).

### 6.2 Q-05: 도감 (codex rewards and permanent bonuses) (P4-1 system, P4-6 popup, P4-5 entry, P4-8 content)
**Entry keys.** `CodexSystem.Key(category, id)` returns:
- `form:{formId}` (29 entries)
- `costume:{costumeId}` (11 with `jjak_starwalk`)
- `enemy:{enemyId}` (9)
- `synergy:{id}` (25) and `synergymax:{id}` (25, reward only, not an entry)
- `material:{itemId}` (9)
- `story:{chapter}` (5)

**Discovery** is recorded from install. Rewards can be claimed only after Codex (1-10) unlocks.

| Category | Found when | First-find reward (별조각) |
|---|---|---|
| 친구 (forms) | `Characters.Granted` (base form) or `Evolution.FormUnlocked` | base 20 · tier-1 30 · tier-2 50 (7×20 + 17×30 + 5×50 = **900**) |
| 의상 | `Costumes.CostumeUnlocked` | 30 each (**330**: 11 costumes incl. `jjak_starwalk`, critic C-06) |
| 적 | first `EnemyKilled` of that def (`Battle.Sim.GetEnemyDefId`) | 10 each (**90**) |
| 시너지 | first `SynergyActivated` (first time on) / reaching its last tier (`synergymax`) | 20 / **30** (25×50 = **1,250**) |
| 재료 | `Inventory.ItemAdded` (first time) | 5 each (**45**) |
| 이야기 | `Story.MarkSeen` | none here (the story page pays 별조각 50) |
Lifetime total is **2,615 별조각** (v1: 2,585; +30 for the season costume, critic C-06).

**Backfill.** When `codex.backfillPending` is set (v3 saves, and at the first Codex Connect), these are marked found but not auto-claimed:
- every owned form, costume and material;
- every enemy whose `minStage < highestStage`;
- every synergy active right now.

Then the flag is cleared.

**Permanent collection bonuses.** They apply automatically, without a claim, once Codex is unlocked. They are added through `SynergySystem.ExtraTeamModifiers`:
- forms found 5/10/15/20/25/29 → `AttackPercent` and `HpPercent` total +1/+3/+5/+7/+8/+10;
- all 9 enemies → `ShopMaxHpPercent` +5;
- synergy kinds 10/20/25 → `LampGainPercent` +5/+10/+15.

`PowerSystem` picks the bonuses up through its existing Synergy.Changed dirty flag. Codex raises `Synergy` dirtiness (`SynergySystem.MarkDirty()`; add it if it is missing) when a tier is crossed.

**API (`CodexSystem : IMetaModule`):**
- `event Action Changed`, `bool IsUnlocked`, `float CompletionPercent`
- `int FoundCount(CodexCategory c)`, `int TotalCount(CodexCategory c)`
- `void GetEntries(CodexCategory c, List<CodexEntryStatus> into)`
- `int ClaimableCount(CodexCategory? c = null)`
- `List<MetaReward> TryClaim(string key)`, `List<MetaReward> ClaimAll(CodexCategory? c = null)`
- `void AddCollectionBonus(StatBlock team)`
- `bool HasSeenSkill(string skillId)`, `void MarkSkillSeen(string skillId)`
- `static string Key(CodexCategory c, string id)`
- `CodexEntryStatus { string Key; CodexCategory Category; string Id; bool Found; bool Claimable; bool Claimed; List<MetaReward> Reward; string Hint; }`

**UI.**
- **Entry (P4-5).** On the 친구들 tab, the `Collection` pill (or its M51 `PanelHeader` equivalent) becomes the Button `CodexButton`. It shows '도감 {pct}%' (Label 24 bold Cream) on `UiArt.PillNight9s`, gets the `Codex` dot, and shows the NEW pill on its first unlock. Tap → `UINav.OpenCodex()`. Before 1-10 it stays a plain counter (as today) and a tap toasts `UnlockSentence(11)`.
- **`CodexPopup` (P4-6, new, `BuildPopup 980×1500`, title '친구 도감').**
  - Header Top(0,150): '도감 {pct}%' (H1 40 Gold bold); `UiArt.Bar` 640×22; bonus line for the selected tab (Caption 22 SubText): '모은 형태 20종 · 친구 공격력·체력 +7%' / '적 9종 모두 만나면 수선점 내구도 +5%' / '켜 본 시너지 12종 · 불빛 충전 +5%'.
  - Bonus pips (friends tab): 6 `PipButton` at 5/10/15/20/25/29 along the bar, with labels '+1%'…'+10%' (Micro 18).
  - Tabs Top(160,64): [친구][의상][적][시너지][재료][이야기], M53 Page style, 6×144, gap 8, with count badges for claimables.
  - Grid Stretch(0,0,240,128): 5 columns of 164 cells (UX Grid5Popup), cell 164×212.
    - Portrait 140 on `UiArt.SlotFrame9s` tinted by grade, name (Label 24, Ink on Parchment card).
    - Unfound: the same sprite tinted (0.12,0.09,0.2,0.9) as a silhouette; '???'; tap → `InfoTooltipView` hint.
    - Claimable: `RibbonGold9s` 120×36 '받기' (Ink 20 bold) + `MetaRewardChip` 52.
    - Claimed: `Icons/check` 28 at the top right.
  - Enemy cells show the codex text from `CodexConfig.enemyTexts` once found (tooltip).
  - Story cells open `StoryPagePopup` (re-read, §8.1).
  - Footer: `UIKit.ClaimAllButton` 380×96 at BottomRight (claims the current tab).
- **Hints (content):**
  - unowned lineage: '잃어버린 친구 찾기에서 만날 수 있어요'
  - unfound form: '진화 기억도에서 단서를 찾아요'
  - costume: its unlock condition text
  - enemy: '스테이지 {minStage 표기}부터 나타나요'
  - synergy: '{구성원} 함께 세우면 켜져요'
  - material: first source label

**Red dot.** `Codex` (claimable). Tab badges show counts.

**Tests (`CodexTests`):**
- Each found trigger.
- Backfill is idempotent.
- Rewards by tier.
- Claim only after unlock.
- Bonus tiers produce the exact StatBlock values (+7% at 20 forms), and the bonus is absent before unlock.
- `seenSkills` persists.
- Total = 2,615 (`EconomyBudgetTests`; was 2,585 before critic C-06).

### 6.3 CR-10: making affinity useful (P4-1 system, P4-5 detail UI, P4-3b pet tap, P4-8 lines)
**Levels.** Cumulative affinity thresholds Lv1 0, Lv2 50, 3 150, 4 350, 5 700, 6 1,200, 7 2,000, 8 3,200, 9 4,800, **10 7,000** (≈140 h of being placed, per the research estimate).
- **Rewards per level reached (claimed):** 별조각 **10 × level** (Lv2 20 … Lv10 100; 540 per lineage). Every even level (2/4/6/8/10) also unlocks one **기억 조각** story line (5 per lineage, Appendix A.3).
- **Cosmetics:** Lv5 unlocks the heart-stitch card frame (`Art/UI/affinity_frame_9s`) on that lineage's 친구들 card and detail portrait. Lv10 unlocks an idle-pose variant: code-driven (sway ±3°, period 2.4 s, plus a heart sparkle every 6 s on the guardian pad). Real pose art is a top-up item (§10.1).
- **Tap-to-pet in battle (FtueDone only):**
  - Tapping a placed guardian's body does three things: squash 0.2 s (scale 1→0.9,1.08→1); a 40 px heart (`Icons/affinity`) rises 60 px over 0.8 s; a bubble with one of that lineage's 7 personality lines (Appendix A.4) shows for 1.5 s. The bubble goes through `SkillCalloutView.SayPet`, bypasses CalloutGate and obeys the M41 dedupe.
  - **+5 affinity up to 3 times per lineage per DayKey.** After that the animation still plays but gives no heart and no affinity.
  - Plays `pet_squeak`.
  - Disabled while an FTUE step targets the battle, while aiming a summon, and while asleep.
  - Input: GuardianSlotView gets a transparent `PetHit` Image (the guardian's sprite rect × 0.8, raycast on only when filled) above the pad, so the existing `_emptyButton` empty-pad behaviour is unchanged.

**API (`AffinitySystem : IMetaModule`):**
- `int GetLevel(string lineageId)`, `int GetClaimedLevel(string id)`
- `long GetLevelStart(int level)`, `long GetNextThreshold(string id)`
- `bool HasClaimable(string id)`, `List<MetaReward> TryClaim(string id)` (all reached levels)
- `int PetsLeftToday(string id)`, `bool TryPet(string id, out string line, out bool gained)`
- `string GetStoryLine(string id, int index)` (null if locked)
- `int CountAtLevel(int level)`

Red dot `LineageAffinity(id)`.

**UI (P4-5, `CharacterDetailPopup` Info tab, `TabInfo = 3`).** The affinity number (`_affinityText`, today N0 only) becomes an 'AffinitySection' card (PanelInset9s 860×300):
- 'Lv.{n} 호감도' (Title 28 bold Cream) + heart icon 40.
- `UiArt.Bar` 560×20 DustyPink with '{cur}/{next}' (Label 24).
- Claim chip: Gold `MetaRewardChip` + [받기] Gold 180×80 when claimable.
- '기억 조각 {k}/5' header plus unlocked lines (Body 26 Text, italic), locked lines '…' with 'Lv.{n}에 들을 수 있어요' (Hint 20).
- '오늘 쓰다듬기 {k}/3' (Caption 22 SubText).

**Hooks.** Achievements line `ach_affinity` (Lv10 count 1/3/7). The codex 이야기 tab lists lineages with Lv10 stories.

**Tests (`AffinityTests`):** thresholds; level from affinity; claim grants 10×level per level; pets 3/day reset at 05:00; locked story null; `CountAtLevel`.

### 6.4 PRG-04: 소환 마일스톤 (cumulative pulls) (P4-1 system, P4-5 UI, P4-8 content)
**Counting.** `GachaMilestoneSave.pulls` increases on `Gacha.Pulled` by the number of results with `!IsGuaranteed`. The FTUE "첫 만남" pull never counts and is labelled '첫 만남 확정' as today. Rates and pity are untouched (fairness).

**Ladder (content):**

| Pulls | Reward |
|---|---|
| 10 | 별조각 100 |
| 30 | 선택 기억편 20 |
| 50 | 달빛 조각 2 |
| 100 | **친구 선택권 1** (전설 이하; §2.11 #4) |
| 200 | 선택 기억편 40 |
| every +100 after 200 | 별조각 300 |
- Claims are in order, one step at a time. ClaimAll claims every reached step.
- Friend ticket → `wallet.friendSelectTickets++`. `FriendTicketPopup` (P4-5, `BuildPopup 900×1100`, '친구 선택권'):
  - grid of the 6 lineages ≤ Legendary (portrait 180, name, stars);
  - owned ones show '기억편 +{GetDuplicateShards}' (Caption 22) and new ones show the NEW ribbon;
  - [이 친구로 할게요] Gold → `GachaMilestones.TryUseFriendTicket(id)`. This grants through `MetaRewards.Grant(Lineage(id))`, which already converts duplicates into shards, then shows the GachaResult-style highlight.
  - The ticket is never consumed without a pick; closing the popup keeps it.

**API (`GachaMilestoneSystem : IMetaModule`):**
- `event Action Changed`, `int Pulls`
- `bool TryGetNext(out GachaMilestoneStep step)`
- `void GetSteps(List<GachaMilestoneStep> into, int repeatsAhead = 2)`
- `bool HasClaimable`
- `List<MetaReward> TryClaimNext()`, `List<MetaReward> ClaimAll()`
- `int FriendTickets`, `GrantResult TryUseFriendTicket(string lineageId)` (null when grade > Legendary or no ticket)

**UI (`GachaPanel`, P4-5).**
- `MilestoneRow` Top(718,56,30,30), below the M63 pity row, buttons and the M08 gem link.
- The panel Hint line (Top 688) is **removed**. Its copy moves into the milestone popup footer, so one instructional line remains per panel (M80).
- Contents: '누적 소환 {n}' (Label 24 bold Cream, at x 0); `UiArt.Bar` 520×18 GoldAccent showing progress to the next step, with 5 `PipButton` at relative step positions; the next reward icon `MetaRewardChip` 44 at the right; Gold pill '받기' 96×40 when claimable.
- Tap → `GachaMilestonePopup` (820×1000): rows 740×110, '{10}회 · 별조각 100' + state [받기]/[완료]/'{k}회 남음'. Footer hint '소환 확률과 천장은 그대로예요. 누적 소환은 첫 만남 소환을 세지 않아요.' (Hint 20).
- Red dot `GachaMilestone` on `tab.gacha`.

**Tests (`GachaMilestoneTests`):** guaranteed pull not counted; 10-pull counts 10; step order; repeat every 100 after 200; ticket grade cap; ticket duplicate → shards; the popup closing keeps the ticket.

### 6.5 PRG-05: new-friend catch-up level (P4-1, `CharacterSystem.cs`)
- In `Grant`, on the `IsNew` branch only: `save.level = CatchUpLevel()` = `max(1, floor(0.8 × average(top-3 owned lineage levels)))`. If fewer than 3 lineages are owned, average over the owned ones; if none, use 1. Clamp to `MaxLevel`.
- Applies to every source: gacha, attendance day 7, first meeting, friend ticket, MetaReward.Lineage. Starters stay at Lv1 because there is no owned lineage yet.
- `Granted` already fires after the change, and `LevelUp` is not raised (no fake level-up fanfare).
- `GrantResult` gains `int StartLevel`. GachaResultPopup (P4-5) shows '친구들 평균의 80%인 Lv.{n}부터 함께해요' (Caption 22 Lavender) under a new friend when `StartLevel > 1`.
- Tests (`CatchUpLevelTests`): levels 20/18/16 → 14; two owned (10, 6) → 6; none → 1; duplicate unchanged; MaxLevel clamp.

---

## 7. Progression feedback

### 7.1 PRG-01 remainder (P4-1 PowerSystem, P4-8 calibration, P4-4/P4-5 UI)
1. **Tapered recommended growth.**
   - `PowerSystem.GetRecommendedPower(s)`: when `recommendedTaperStage = T > 0` and `s > T`, the result is `base × g^(T−1) × gL^(s−T)`; otherwise it is today's single-exponent formula.
   - `PowerCalibration` (P4-8) fits two segments: `g` on stages 11..T and `gL` on stages T+1..100 (5 seeds, 4 wins). It writes `CalibratedRecommendedTaperStage` (= `BattleConfig.stageGrowthTaperStage`, 30) and `CalibratedRecommendedGrowthLate` into `MetaContent.Power.cs`. The envelope scale stays.
   - Acceptance: need/rec for 6-1…10-10 falls within 0.80–1.05 (today 0.52–0.83).
   - Tests: `PowerTests.TaperedRecommended_ContinuousAtTaperStage`, `…_UsesLateGrowthAfterTaper`.
2. **Dungeon floor recommendation.**
   - `PowerSystem.GetDungeonRecommended(int floor) = dBase × dGrowth^(floor−1)`, or 0 when `dungeonRecommendedBase == 0`.
   - Rating uses `EchoPower` (deck only, since guardians don't fight in the dungeon) with the same 1.0/0.8 bands.
   - Calibration: PowerCalibration's new 'IdleGame/던전 전투력 보정' scales the starter deck on floors 1..30 (5 seeds, 4 wins) and fits.
   - UI (P4-4, DungeonCardView): a line under the floor text: '권장 {rec:N0} · 잔향이 {echo:N0}' (Label 24) plus the rating word chip (UX M12 style; '여유'/'도전'/'어려움' Micro 18 on the rating colour).
3. **Gacha new-friend power (P4-5).** In GachaResultPopup's highlight:
   - new lineage: '전투력 {GetLineagePower:N0}' (Label 24 bold Cream + 24 px power icon) under the stars;
   - duplicate: '기억편 +{n}' (existing).
4. **친구들 grid power (P4-5).** `CharacterCardView` (owned only): `PowerText` '⚔ {FormatCompact(GetLineagePower)}' at BottomLeft(14,10), 20 bold. The 20 px sword is the same sprite as the M45 card. Ink on the parchment area, Cream on the night card. Refresh on `Power.Changed`.

### 7.2 CR-01: defeat analysis ring (P4-3b view, P4-4 slot pulse, P4-6 sheet row)
**Data.** `UI/Battle/DefeatAnalyzer.cs` is pure C# and fixed-size.
- On `EnemyStrike` with `TargetId == -1` (the shop), it adds `Value` to `slotDamage[EnemyFxCatalog.ShopStrikeSlot(Position)]` and to `slotEnemy[slot][enemyKey]` (top enemy per slot; small fixed map of 9 enemy ids).
- Reset on `StageChanged`. Also feeds CR-09's `dir[5]`.

**When.** On `StageEnded(Failed)`, after the fail banner (M13: 1.2 s), with `ftue.done`, if total ≥ 1.
- ~~`DefeatAnalysisView` shows for 6 s (or until a popup opens, the stage restarts, or it is tapped).~~ **Critic fix C-05:** the next attempt starts `BattleConfig.stageEndPause` = 1.5 s after the fail, so "hide on restart" left the ring on screen for ≈0.3 s. New rule:
  - At most **once per stage per 10 minutes** (farm loops don't repeat it; tracked in BattleView memory, not saved).
  - At the fail, BattleView calls `Game.Battle.ExtendEndPause(4.8f)`. The ring fades in (0.2 s) when the fail banner finishes (1.2 s for the M13 retreat, 1.6 s otherwise), so it is readable for ≈3.2 s. The field is frozen in its fail state during the pause, so covering the upper-left pad is harmless.
  - It hides when the pause ends (restart), a popup opens, or it is tapped.
  - The caption gains a second link, '바로 다시 도전 ›' (Ghost 26) → `Game.Battle.SkipEndPause()` (restart in 0.3 s).
  - Offline, dungeon and headless paths never call `ExtendEndPause`, so CR-07 EconomyFlowTests are unaffected.
- Position: StageArea TopLeft(24,-310). Ring 200×200, plus a caption plate 280×200 at its right (`UiArt.PanelNight9s` @0.92).
- In screen space that is x 24–504, y 500–700. It clears the M13 boss button (416–484) and the guide bar (712–796).

**Look.**
- 5 `Image.Type.Filled` radial segments using `UiArt.RingStitch` (a tint-able piece). Each segment is 72°, rotated to its slot (slot 0 at the top, counter-clockwise), with a 4° gap. Colour: SubText@0.55; the largest segment Danger@0.95 with a 1.06 pulse once.
- Centre: '{pct}%' of the largest (H2 32 bold Cream) + a 40 px "!" (`motif_exclaim`). This follows the CVD rule from ATK-17: colour never carries meaning alone.
- Caption:
  - L1 '{방향}이/가 많이 뚫렸어요' (the particle follows the last syllable's final consonant: '위쪽이', '왼쪽 위가', '오른쪽 아래가') (Title 28 bold Cream). Directions: slot0 '위쪽', 1 '왼쪽 위', 2 '왼쪽 아래', 3 '오른쪽 아래', 4 '오른쪽 위'.
  - L2 '{지킴이 이름} 자리' or '빈 자리' (Label 24 SubText).
  - L3: advice for the top enemy there (Appendix A.6; Hint 20 Lavender, 2 lines).
  - Link '편성 바꾸기 ›' (Ghost, 26).

**Tap.** Ring or link → `UINav.GoToTab(Formation)` + `UINav.PulseFormationSlot(slot, 2f)`. FormationPanel (P4-4) pulses that ring slot: gold outline, scale 1→1.06, 2 s.

**Sheet row.** The M13 강해지기 sheet gets the row '약한 방향 보강 · {방향}' with [가기 ›] doing the same navigation (P4-6). Shown when the last fail had analysis data.

**Tests (`DefeatAnalyzerTests`):** slot mapping by angle; top-enemy pick; reset on stage change; percent rounding; no data → no show; FTUE → no show; (critic) a second fail on the same stage within 10 min → no show; `BattleSystemTests.ExtendEndPause_DelaysRestart_SkipEndPause_RestartsNow`.

### 7.3 CR-02: auto-lamp policy (P4-3b sim and UI; P4-0 save and enum)
**Policies (sim, `BattleSimulator.ShouldAutoLamp`):**
- `Auto` (default): today's rule (≥5 alive, or boss alive, or shop < 35%).
- `SaveForBoss`: boss alive, or shop HP < 25%.
- `Manual`: never auto-fire. AUTO still auto-summons.

`BattleSystem.LampPolicy` writes `BattleSave.lampPolicy`, sets `_sim.LampPolicy` live and copies into `BattleSetup` on restart. Locked (LampPolicy 1-6) → always Auto.

**UI (`LampButtonView` + new `LampPolicyPicker`):**
- **Long-press** 0.5 s (unscaled; HoldRepeat-style cancel on drag or focus loss) opens the picker above the button: `UiArt.PanelNight9s` 360×(3×88+16).
- Rows 344×88: icon 48 (`Icons/auto` / `Enemies/boss_shadow` / `UI/tutorial_finger`) + title (Title 28 bold Cream) + desc (Hint 20 SubText):
  - '알아서' — '적이 많거나 위험할 때 바로 써요'
  - '보스까지 아끼기' — '보스가 나오거나 수선점이 위험할 때만 써요'
  - '직접' — 'AUTO가 켜져 있어도 직접 눌러야 써요'
- The selected row gets a 2 px Gold outline + `Icons/check` 28. A tap outside closes it (transparent full-field catcher). A tap on a row applies it, toasts '불 밝히기: {이름}' (Info) and closes.
- **Corner icon** 32×32 at the button's TopRight(-6,-6), on a 40 px `PlateRound`: shows the current policy; hidden for Auto.
- A short tap still lights the lamp (unchanged). Locked → long-press shows `UnlockSentence(7)` (Error toast + Light haptic).
- Disabled while an FTUE step targets `LampButton`.

**Tests (`LampPolicyTests`):** each policy's decision table (alive counts, boss, HP 24/26/34/36%); locked → Auto; save round trip; the picker does not consume a short tap (PlayMode, P4-9).

### 7.4 CR-03: battle record (damage share) (P4-3b)
**Recorder.** `UI/Battle/BattleStatsRecorder.cs` is fed from BattleView's resolved attribution:
- In `OnEnemyHit`: the Basic context → guardian slot; EchoBasic → echo; Cast → caster.
- Also `ShopHealed`/`EchoHealed` (SourceId slot or echo), `ShieldGained` (cast source) and `EnemyKilled` (SourceId).
- Units are keyed by **formId**; echoes merge by form.
- Storage: a 60-bucket ring (1 s buckets of per-unit {damage, heal, shield, kills}, max 16 units), plus stage totals reset on `StageChanged`. Arrays are fixed and nothing allocates on the event path.

**HUD button.** `StatsButton` at StageArea TopRight(-24,-302): 64 visual, `UIKit.HitPad` to 88. `UiArt.PlateRound` with the `Icons/quest` icon. Gate `FeatureId.BattleStats` (1-7): the button is hidden until then and plays one MetaPulse on unlock (no dot). It is hidden during FTUE and coach marks.

**Popup.** `BattleStatsPopup` (UI/Battle, built by BattleSceneBuilder): `BuildPopup 900×900`, title '전투 기록'.
- Segments [지난 60초][이번 스테이지] (M53 Option).
- Rows 820×92, sorted by damage desc, max 11:
  - portrait 56 in `SlotFrame9s` tinted by lineage main;
  - name (Label 24 bold) + role chip ('지'/'잔');
  - damage bar `UiArt.Bar` 360×18 with the lineage **main** colour and '{FormatCompact}' (Label 24);
  - small heal (Positive) and shield (SoftBlue) mini-bars 140×10 under it;
  - '처치 {n}' (Caption 22).
- The top unit gets `Icons/skill_buff_crown` 32.
- Live refresh at 1 Hz while open; text is written only when it changes.
- Empty state: '아직 기록이 없어요. 전투를 조금 지켜봐 주세요' (Body 26 SubText).

**Tests (`BattleStatsRecorderTests`):** attribution per context; 60 s window expiry; echoes merged by form; stage reset; overflow beyond 16 units drops the smallest.

### 7.5 CR-04: summon reveal (P4-5; art P4-10; audio P4-3a)
GachaResultPopup keeps its API (`Show(...)`, `Skip()`, `Closed`) and the FTUE first-meeting flow. A new reveal stage (`_revealStage`, the first child of Body) plays before the existing grid.

**1 pull:**

| t (s) | Beat |
|---|---|
| 0.00–0.30 | 분실물 꾸러미 (`Art/UI/gacha_bundle_closed`, 300×300) drops from y+320 (OutBack s=1.4), `gacha_bundle_drop` |
| 0.30–0.70 | shake ±6°, 3 cycles. If the actual best grade ≥ Legendary: amplitude ×1.6 plus a glow in the **actual** grade light. Anticipation never shows a higher grade (existing fairness test) |
| 0.70–1.05 | opens (`gacha_bundle_open` sprite swap + burst in the grade light), `gacha_bundle_open` sfx |
| 1.05–1.35 | portrait silhouette (tint 0.1,0.08,0.18) → white |
| 1.35– | existing highlight card (name, stars, NEW, power §7.1, catch-up line §6.5); Heavy haptic for ≥Legendary (M18); `jingle_gacha_high` + `AppSignals.RaiseGachaRevealHigh(grade)` |

**Grade lights:** 1–2★ cream FFF4DC · 3★ pink E9A6C6 · 4★ gold F2C46B + stardust (`fx_sparkle` ×12) · 5★ lavender C4B2F2 + rainbow stardust (`fx_sparkle` tinted through 6 hues, 18 particles, reduced-motion 9).

**10 pull:**
- A 2×5 grid of small bundles (150×150) appears with a 0.04 s stagger. Each flips in **0.08 s** (card back → result card).
- On a ≥4★ result, flipping pauses and that friend gets a **1.6 s** full reveal (the 1-pull beats from 0.70 s, scaled to fit) before flipping continues.
- New lineage: Gold ribbon '새 친구!' (RibbonGold9s 150×40, Ink 20 bold).
- Duplicate: after its flip the card shrinks to the shard icon and moves to the summary line in 0.4 s, then '+{n}' appears on the summary.

**Pacing and controls:**
- A 10-pull with no ≥4★ finishes in ≤ 4 s (≈ 0.3 + 10×0.08 + settle).
- Skip: [건너뛰기 ›] at TopRight, appearing after **0.5 s** → `Skip()` → `RevealInstant` + grid (existing).
- M49 dim/back during the reveal = Skip.
- Reduced motion (M64): no shake, alpha-only flips, half the particles.

**Tests:** PlayMode `GachaRevealTests` (P4-9) — 10-pull ≤4 s without ≥4★; skip visible at 0.5 s and not before; anticipation grade ≤ actual. EditMode: grade-light mapping.

### 7.6 CR-07: gold-flow targets and verification (P4-9)
- **Test `EconomyFlowTests.SixtyMinutes_AutoProgress_UpgradeIntervalsInBand`** (EditMode, `[Timeout(180000)]`):
  - Setup: new PlayerData from GameConfig; Game-like system set from SystemsTestBase, with the real BattleConfig/CharacterDatabase assets loaded via `AssetDatabase` from `Assets/_Project/Data`.
  - Run `BattleSystem.Tick(0.1)` for 36,000 steps (simulated 60 min) at Speed 1, with idle gold accrual as in GameBootstrap.
  - Policy: buy a shop level whenever affordable, otherwise the cheapest lineage level among placed lineages.
  - Record the gap between consecutive purchases, bucketed by chapter of the current stage.
  - Assert the median gap: chapter 1 in **15–30 s**, chapter 3 in **45–90 s** if reached, chapter 5+ **≤180 s**.
  - **Return check:** from that state, add 8 h offline → `IdleChest.Claim` gold ≥ cost of 3 consecutive shop levels.
  - Writes `Logs/EconomyFlow.csv` (minute, stage, shop level, gold, gap).
- **Tuning knobs** (if it fails): `GameConfig.goldPerSecondGrowth` and `levelUpCostGrowth` (note: the **asset** `Data/GameConfig.asset` holds explicit values, and IdleDataBuilder keeps inspector values, so a tuning change must edit the asset YAML; changing the C# default alone does nothing), and `BattleConfig.stageGoldGrowth` (P4-3b applies it, because `ApplyDefaults` rewrites BattleConfig).
- Analytic prior: time to the next shop level from idle gold alone is `10 × 1.0182^(L−1)` s, plus clear gold `60 × 1.13^(s−1)` per ~60–90 s stage. Expect chapter 1 to come out **faster** than 15 s (≈7–12 s), so a likely fix is `levelUpCostGrowth` 1.12 → 1.13, re-measured. Report the final values in the package handoff.

---

## 8. Story, return and the live calendar

### 8.1 CR-05: chapter storybook and chapter map (P4-1 StorySystem, P4-6 popups, P4-8 text, P4-10 art)
**Storybook trigger.** The first clear of a chapter boss stage (x-10) for chapters 1–5 with content.
- `StorySystem` subscribes to `Battle.StageEnded(Cleared)` where `stage % 10 == 0` and the chapter is not in `story.seenChapters`. It enqueues `StoryPage(chapter)`, which comes after `ChapterChest(chapter)` (§2.9). Tutorial active → it waits for `ftue.done`.
- Opening the page marks the chapter seen. Rewards '처음 읽은 선물 별조각 50' are granted when the page is closed, once (`rewardedChapters`).
- Codex `story:{n}` is found at that moment. Chapters without content (6+) skip the page.

**`StoryPagePopup`** (P4-6, `BuildPopup 980×1400`, title '동화책 {n}장 · {제목}'):
- Three panels stacked: each 940×380 `Image` (`Art/Story/ch{n}_{1..3}.png`; fallback = a `UiArt.PanelInset9s` card + chapter colour tint, text only), on `UiArt.CardFrame9s`.
- Under each panel one line ≤40 chars (Body 26 Text, centred).
- Panels reveal one by one with a 0.25 s alpha fade (tap = next, like StoryIntroView). Reduced motion → instant.
- Footer: reward chip (first read only) + [닫기] Gold (first read) / Secondary (re-read).
- Re-read from the codex 이야기 tab gives no reward. Back/dim → completes the reveal first, then closes (M49 policy).

**Chapter map.** `ChapterMapPopup` (P4-6, `BuildPopup 940×1300`) opens from the **StagePanel tap** (§2.11 #1) after `ftue.done`:
- Header '{n}장 · {장소}' (H1 40) with ‹ › chapter arrows (chapters ≤ current; 88 hit).
- Map art `Art/Story/map_bg.png` (1024×1536, cover-fit into 880×1000).
- 10 nodes at fixed normalized positions (content `StoryChapterDef` has none; use a shared code table `MapNodeLayout[10]`). Node art from `Art/UI/map_nodes` (2×2 sheet): lamp off / lamp on / boss ring / chest. Each node is 96×96.
  - cleared = lamp on + '{1-7}' (Label 24 bold Cream, outlined);
  - current = lamp on + 1.6 s breathing glow (one of the 2 allowed loops);
  - locked = lamp off α0.55.
- Boss node: the boss portrait (`Enemies/boss_*`) inside the ring. A chest marker by the boss node shows ChapterChest contents (tap → InfoTooltip '챕터 상자: 별조각 100 · …').
- Feature icons: for each FeatureId whose condition is `StageCleared(chapter, k)`, a 40 px icon plus a '여기서 열려요: {기능}' tooltip at node k.
- Tap a cleared node → `Battle.GoToStage(stage)` + toast '{1-7}을 다시 지켜요' (particle by `ObjectParticle`), then close. Tap the current node → close.
- Footer: '{n}장 진행 {k}/10' (Caption 22).

**Text.** Appendix A.5 (15 panel lines, 5 titles and places).

**Tests (`StoryTests`):** trigger only on the first x-10 clear; queue order chest → story; reward once; FTUE defers; chapters without content skip.

### 8.2 CR-06: 다시 만난 날 (returning-player welcome) (P4-2 system, P4-6 card, P4-8 content)
**Trigger.** In `BeforeAwayApplied(away, boot)`. All of these must hold:
- `away ≥ 259,200 s` (72 h);
- `ftue.done`, `!IsTampered`;
- `comeback.count < 6` (lifetime);
- `comeback.lastDayKey` is null or ≥14 days ago.

Then:
1. `count++`, `lastDayKey = startDayKey = today`, `day = 1`, `lastStampDayKey = null`.
2. **`IdleChest.GrantBonusCap(24)`** before AddOfflineTime. This sets `bonusCapSeconds = 86,400` until the next Claim, and `CapSeconds = max(step cap, bonus)`. The 72 h+ away therefore fills a 24 h basket once ('오랜만이라 바구니를 더 채워 뒀어요').
3. `AfterStartup` enqueues `Comeback` (startup kind 1, right after Return).

On later boots or resumes with a new DayKey while `day ≤ 3`, enqueue Comeback again for the next stamp.

**Card `ComebackCard`** (P4-6, `BuildPopup 900×1100`):
- Header art `Art/UI/comeback_header` (1536×560 cover into 840×300; optional asset, fallback `return_header`).
- Title '다시 만나서 반가워요!' (Display 56 → fit 44).
- Body '{d}일 만이에요. 친구들이 수선점을 지키며 기다렸어요.' (Body 26).
- Three stamp cells 220×260 (`UiArt.StampRing` + day label + rewards via MetaRewardChip); today's cell glows Gold.
- [도장 찍기] Gold 520×112 → `TryClaimToday` → stamp animation (reuse AttendanceCellView stamp, `ui_stamp`) → fly rewards. After day 3 the card closes with '이제 다시 매일 만나요!'.

**Rewards** (content): day 1 별조각 300 + 빠른 수선권 2 · day 2 두 배 수선권 2 + 수선 천 10 · day 3 선택 기억편 20.

**API (`ComebackSystem : IMetaModule`):** `bool IsActive`, `int Day`, `bool CanClaimToday`, `List<MetaReward> TryClaimToday()`, `long AwaySeconds`.

**Tests (`ComebackTests`):** 71 h → none, 72 h → trigger; 14-day cooldown; lifetime 6; tamper blocks; the bonus cap applies before AddOfflineTime and clears on claim; one stamp per DayKey; FTUE → none.

### 8.3 EVT-01: local event calendar (P4-2 system, P4-6 hub, P4-4 chip route, P4-8 content)
**Determinism.**
- Everything keys off `DailyClock` (05:00 day boundary, effective time = max(now, maxSeen)), except the nightly window, which uses the local clock hour.
- The same date gives the same schedule on every device. Moving the clock forward can trigger an event early; damage is bounded by per-instance claims. Moving it back is blocked by tamper checks.
- Unlock: `FeatureId.Events` (3-1 or install day 3).

**Events (content):**

| id | Name | Schedule | Effect | Missions / stamps |
|---|---|---|---|---|
| `evt_weekend_expedition` | '주말 원정 축제' | Weekdays Sat+Sun (DayKey weekday) | expedition **material drops ×2** at claim (`Expedition.DropMultiplier`) | – |
| `evt_wednesday_dungeon` | '수요 던전 데이' | Weekdays Wed | **+2 dungeon tickets** that day (`Dungeon.BonusTickets`) | – |
| `evt_starlit_walk` | '별이 머문 밤 산책' | MonthDays 1–14 | **별빛 사탕 ×2** on stage clears (`Battle.CandyMultiplier`); the basket's eventOnly 사탕 line accrues for the overlap (`IdleChest.EventLineHours`) | 6 missions + 1 bonus, 7-day stamp (Appendix A.7) |
| `evt_night_shop` | '밤의 수선점' | DailyHours 21–2 (wraps midnight) | basket **+20%** for overlapped time (materials + 별조각 lines; not gold) via `IdleChest.NightBonusHours` | – |
The event shop (`shop_event`) stays permanent (today's behaviour). The hub links to it, and the shop tab gets a DustyPink '축제 중' pill while the starlit walk runs.

**Instances.**
- Key `{id}@{startDayKey}` (weekend → Saturday's DayKey; monthly → `yyyy-MM-01`; Wednesday → that day). Nightly has no instance.
- On first activation while running (Connect / OnDayChanged / Tick at 1 s) the instance is created with `baseline[counter] = Progress.GetLifetime(counter)` for each mission counter. Mission progress = lifetime − baseline (target capped).
- Stamps: one per DayKey inside the window, claimed from the hub (`TryStamp`), up to 7.
- Claims are blocked when tampered.
- `startCardShown` → enqueue `EventStart(instanceKey)` once (startup kind; kept if the cap is hit, §2.9).

**API (`EventCalendarSystem : IMetaModule`):**
- `event Action Changed`, `bool IsUnlocked`
- `void GetActive(List<ActiveEventStatus> into)` (name, kind, effect label, `TimeLeft`, instanceKey or null)
- `double ExpeditionDropMultiplier`, `int BonusDungeonTickets`, `double StageCandyMultiplier`
- `double NightBonusHours(long fromUnix, long toUnix)`, `double EventLineHours(long fromUnix, long toUnix)` (pure overlap math, tested)
- `void GetMissions(string instanceKey, List<EventMissionStatus> into)`, `List<MetaReward> TryClaimMission(string key, string missionId)`
- `bool CanStamp(string key)`, `List<MetaReward> TryStamp(string key)`
- `bool HasClaimable`, `void MarkSeen(string key)`

**UI.**
- Entry (P4-4, `TopBarView`): when Events is unlocked, the EventChip tap → `UINav.OpenEvents()`; otherwise today's shop route.
  - Dot `top.event` (RedDotView attached by P4-6 in MetaSceneBuilder via `AttachRedDot`).
  - 'D-{n}' pill: PillNight 64×26, Micro 18 Gold, under the chip; shown while the starlit walk runs.
- `EventHubPopup` (P4-6, `BuildPopup 980×1500`, title '이벤트'):
  - **Banner** Top(0,354): `shop_event_banner` cover-fit 908×354 + M84-style scrim + event name (H1 40) + time left (Label 24, `ClockMono` when <24 h).
  - **Active effects strip** Top(370,72): chips (PillNight 9s, icon 32 + Label 24): '원정 재료 ×2' / '던전 입장권 +2' / '별빛 사탕 ×2' / '밤 9시~새벽 2시 바구니 +20%'. Inactive ones show their next start ('토요일 새벽 5시부터', Caption 22 SubText).
  - **Stamp row** Top(458,190): 7 cells 120×180 (AttendanceCellView template reused), today glows, [도장 찍기] in-cell when `CanStamp`.
  - **Missions** list Stretch(0,0,664,240): `MissionRowView` via `JournalRowModel`. Rows: icon, text, progress '12/50', reward chip (사탕), [받기]/[완료]/[가기 ›].
  - **Footer**: [이벤트 상점 가기 ›] Primary 420×96 (left); `ClaimAllButton` 380×96 Gold (right, when claimable). If Q-06 is unlocked, a **수첩 tile** sits above the footer (860×110, '별빛 산책 수첩 Lv.{n} ›', opens the season popup).
- `EventStartCard` (P4-6): reuses the FeatureUnlockCard layout 840×620 with the banner illustration. Title '{이벤트 이름}가 시작됐어요!' (particle helper). Line: shortDesc. Buttons [나중에] Secondary / [가 보기 ›] Primary → hub.

**Tests (`EventCalendarTests`, all UTC FakeClock):**
- Weekday boundaries at 05:00 (Fri 04:59 = not weekend; Sat 05:00 = weekend).
- Month days 1–14 inclusive by DayKey.
- Night window wrap (20:59 no, 21:00 yes, 01:59 yes, 02:00 no).
- Overlap math across days.
- Baseline captured once per instance.
- Mission claims capped; stamps 1/day, 7 max.
- Tamper blocks claims; the start card is queued once; locked → no effects.

### 8.4 Q-06: 별빛 산책 수첩 (free season track; P2 stretch) (P4-2 system, P4-6 UI, P4-8 content)
**Rules.**
- Unlocked with Events. A season is **28 DayKeys** starting the first DayKey after unlock; the next one starts automatically.
- XP = daily journal points claimed during the season (`CounterKey.JournalPoints` increments, daily missions only, 1 point = 1 xp). The daily cap is 170 (all daily missions), so a typical day gives ≈100.
- **90 xp per tier, 30 tiers (2,700)**: about 27 days at 100/day.
- At rollover, unclaimed reached tiers are **granted automatically** (MetaRewards.Grant; toast '지난 수첩의 남은 선물을 받았어요'). Unreached tiers are lost; that is the season.
- ~~Tier 30 = costume `jjak_sleepcap` (수면모자 짝짝이, existing art). If it is owned: `fallback` 별조각 300 + 선택 기억편 30.~~ **Critic fix C-06:** `jjak_sleepcap` is sold in the event shop for 150 사탕 (`ShopDatabase` `event_jjak_sleepcap`, `CostumeUnlockType.EventShop`). Most players would already own it, so season 1 would usually pay only the fallback, and every later season would pay 390 + 300 = 690, which breaks the ≤450 budget. New rule:
  - Tier 30 = season-exclusive costume **`jjak_starwalk` '별빛 산책 짝짝이'** (`CostumeUnlockType.Season`, granted through `MetaReward.Costume`; art A14 §10.1). This matches the research ("마지막 칸은 새 의상").
  - Until A14 exists, tier 30 pays the fallback.
  - Fallback (costume owned, i.e. season 2 onwards): **별조각 50 + 선택 기억편 30**, so every season pays ≤ 440.
  - The costume adds one codex entry (§6.2: 11 costumes, +30) and counts for `ach_costume`.

**Rewards** (Appendix A.8): ≈390 별조각 per season plus tickets, shards and materials. Inflation is small (≈100/week).

**API (`SeasonPassSystem : IMetaModule`):** `bool IsUnlocked`, `int SeasonIndex`, `long Xp`, `int Level`, `TimeSpan TimeLeft`, `void GetTiers(List<SeasonTierStatus> into)`, `List<MetaReward> TryClaim(int tier)`, `List<MetaReward> ClaimAll()`.

**UI.** `SeasonPassPopup` (P4-6, `BuildPopup 980×1500`, title '별빛 산책 수첩'):
- Banner Top(0,300) (`Art/UI/season_banner` optional; fallback `shop_event_banner`).
- 'Lv.{n} · {xp%90}/90' (H2 32) + bar; '남은 기간 {d}일' (Label 24); source line '수선 일지 점수 1점 = 수첩 경험치 1' (Hint 20).
- Vertical track (ScrollList) of 30 rows 860×110: `UiArt.MedallionDark` 72 with the tier number (Title 28 Cream), rewards (≤2 MetaRewardChip 72), state [받기] Gold / [완료] / locked α0.55. Highlighted tiers (5,10,…,30) use `RibbonLavender9s` behind the medallion.
- Footer `ClaimAllButton`.
- Red dot `Season`.

**Tests (`SeasonPassTests`):** xp only from daily points; tier math; rollover auto-grant and new season; costume fallback when owned; locked → no xp.

### 8.5 PRG-06: 잃어버린 상자 (lost chest = P2 stretch; chapter chest = **P1 core**, critic C-04) (P4-1 system, P4-3b hook, P4-6 popup, P4-8 tables)
**Lost chest (every normal stage clear, after `ftue.done`).**
- `BattleSystem.ClearDropHook` → `ChapterChests.TryRollLostChest(stage, rewards)` replaces `RollClearDrops`.
- With probability **0.6** it rolls one weighted entry from the stage's table (Appendix B.3) and adds it to the clear rewards. The expected common materials per clear (≈0.83) stay close to today (0.78). Rares appear from chapter 2.
- The M67 ribbon reward flyer shows a 40 px `journal_chest_closed` icon for that item (P4-3b, StageClearRewardFlyer). Counter `LostChestOpen`.
- Before `ftue.done` the hook returns false (today's drops).

**Chapter chest (first clear of each x-10). P1 core, not stretch (critic C-04):** the research puts '클리어하면 챕터 상자' in PRG-02 (P0). §2.11 #7 moves quest 22's chest content here, and CR-05's story page is ordered after it, so it ships in 4.1 with the quest-22 change.
- `ChapterChestSystem` enqueues `ChapterChest(chapter)` (between stages).
- `ChapterChestPopup` (P4-6, 840×780): chest art (`journal_chest_closed` → `_open` swap, 300×300, shake 0.4 s, open burst, `chest_open`) + title '{n}장 상자' + line '{보스 이름}를 물리치고 찾은 상자예요' + contents grid (MetaRewardChip 96) + [받기] Gold → fly.
- Rewards: Appendix B.2. Quest 22 drops its merged chest content (§2.11 #7).
- `chapterChests.opened` prevents repeats.

**Tests (`ChapterChestTests`):** drop rate statistics (seeded, 5,000 rolls ±2%); table by stage range; FTUE → false; chapter chest once; queue order.

### 8.6 PRG-07: 잔향 축복 (dungeon blessings; P2 stretch) (P4-2 system, P4-4 UI, P4-8 content)
**Rules.**
- Each time `HighestFloor` crosses a multiple of 3 (3, 6, … 30), `blessingPicksPending++` (max 10 blessings).
- A pick offers 3 distinct blessings from the pool (Appendix B.4), excluding ones already at `maxStacks` (2). The offer is a seeded roll saved in `blessingOffer`, so re-opening the popup shows the same 3.
- Picked blessings are permanent dungeon-only modifiers: `DungeonSystem.CreateSetup` adds `Σ value` to the deck StatBlock for each `stat`.
- **Free respec weekly:** if `respecWeekKey != WeekKey`, the player may clear all blessings and get the picks back (= floors/3).
- Gate: Dungeon (1-10) and floor ≥3.

**UI (P4-4).**
- DungeonCardView row '잔향 축복 {k}/10' (Label 24) + 5 mini icons (28 px). When `blessingPicksPending > 0`, the Gold pill '축복 고르기' 160×48.
- `BlessingPickPopup` (`BuildPopup 980×920`, title '잔향 축복을 하나 골라요'): 3 cards 280×560 (`UiArt.CardFrame9s` + skill icon from `Icons/skill_*` 128 + name Title 28 + desc Body 26 + '이미 {n}/2' when stacked); tap → [이 축복으로 할게요] Gold.
- Respec link 'Ghost: 축복 다시 고르기 (이번 주 무료)' on the card when available.
- Red dot `DungeonBlessing`.

**Tests (`DungeonBlessingTests`):** picks at 3/6/9; offer stable across reopen; stacks ≤2; the deck StatBlock includes blessings; weekly respec once per WeekKey.

### 8.7 PRG-08: 수선점 공명 (level sync): decision, do not build
- Shared lineage levels already span all forms. With 7 lineages, PRG-05 catch-up (§6.5) removes the "new friend is useless" gap that level sync targets.
- The name collides with the existing 계보 공명 (shard-paid resonance).
- **Decision:** not built in phase 4. Record it in research §17 (deferred list) as superseded by PRG-05.
- Revisit trigger: the CR-09 play log shows the median spread between the 5th-highest and the lowest placed lineage level above 40% at chapter 4+. If it is ever built, call it '레벨 맞추기 (수선점 맞춤)', never '공명'.

---

## 9. Work packages and file ownership

### 9.1 Waves
```
(UX wave 2 merged; graphics-polish + attack-FX merged)
Wave 4.0 ── P4-0 Contracts (sequential, alone in code)      ║  P4-10 Assets may start in parallel (Art/**, Audio/** only)
              │ merge + compile check + BatchBuildAll on verify_proj (stubs inert, game unchanged)
Wave 4.1 ─┬─ P4-1 Collection & progression systems
          ├─ P4-2 Return, events & idle systems
          ├─ P4-3a Audio runtime + table builder
          ├─ P4-3b Battle (sim hooks, FX, HUD views, battle SFX hooks)
          ├─ P4-4 Menu UI (settings, expedition, dungeon, formation, top bar)
          ├─ P4-5 Character & gacha UI
          ├─ P4-6 Meta UI (journal records, codex, events, season, story, map, comeback, sleep)
          ├─ P4-7 Platform & data (notifications, save import/backup, share, play log)
          ├─ P4-8 Content (tables, copy, calibration)
          └─ P4-10 Assets (continues)
              │ merge in this order to minimise rebase: P4-8 → P4-1/P4-2 → P4-3a → P4-3b → P4-7 → P4-4/P4-5/P4-6
Wave 4.2 ── P4-9 Tools, perf & verify (starts after UX WP-9 has merged and 4.1 is merged)
```
UI packages start on P4-0's inert stubs (the SPEC3 pattern). Behaviour is checked in 4.2. Stretch items (Q-06, PRG-06, PRG-07, EVT-02) are the last step in each package and may be cut without touching any contract.

### 9.2 File ownership (one owner per file in phase 4)
| Package | Files |
|---|---|
| **P4-0** (then handed on as noted) | `Scripts/Core/{GameEnums, SaveData.Meta, SaveData.Battle, PlayerData, Game, GameSettings}.cs`, new `Scripts/Core/AppSignals.cs`, `Scripts/Data/MetaDatabase.cs`, `Scripts/Systems/Meta/{MetaSession, PopupQueue, RedDotSystem, MetaTypes, ProgressTracker}.cs`, new `Scripts/Systems/Meta/IMetaModule.cs`, `Scripts/UI/UINav.cs`, `Tests/EditMode/{SystemsTestBase, MetaSessionTests}.cs`. **Stubs created, then handed off:** `Systems/Meta/{Achievement,Codex,GachaMilestone,Affinity,ChapterChest,Story}System.cs` → P4-1; `Systems/Meta/{EventCalendar,SeasonPass,Comeback,Coupon}System.cs` → P4-2; `Core/Audio/{AudioSystem,AudioKeys}.cs` → P4-3a; `Core/LocalNotifications.cs`, `Systems/Meta/NotificationPlanner.cs` → P4-7. **Hook members only, then handed back:** `Systems/Synergy/SynergySystem.cs`, `Systems/GachaSystem.cs`, `Systems/InventorySystem.cs`, `Systems/Characters/{CharacterSystem,EvolutionSystem,CostumeSystem}.cs` → P4-1; `Systems/Battle/{ExpeditionSystem,DungeonSystem}.cs`, `Systems/Meta/{IdleChestSystem,MissionSystem}.cs` → P4-2; `Systems/Battle/{BattleTypes,BattleSystem,BattleSimulator}.cs` → P4-3b. `ProgressTracker` gets `public Func<MetaCondition, long?> ExtraConditionValue` so modules can answer the 3 new condition types (it stays P4-0's; nobody else edits it in 4.1) |
| **P4-1** | `Systems/Meta/{AchievementSystem, CodexSystem, GachaMilestoneSystem, AffinitySystem, ChapterChestSystem, StorySystem, MetaRewardService, PowerSystem}.cs`, `Systems/Characters/{CharacterSystem, EvolutionSystem, CostumeSystem}.cs`, `Systems/GachaSystem.cs`, `Systems/Synergy/SynergySystem.cs`, `Systems/InventorySystem.cs`; tests: new `{Achievement, Codex, GachaMilestone, Affinity, ChapterChest, Story, CatchUpLevel, FeatureGuard, EconomyBudget}Tests.cs`, existing `{CharacterSystem, EvolutionSystem, CostumeSystem, GachaSystem, Power, MetaReward, SynergySystem}Tests.cs` |
| **P4-2** | `Systems/Meta/{EventCalendarSystem, SeasonPassSystem, ComebackSystem, CouponSystem, IdleChestSystem, MissionSystem, DailyClock}.cs`, `Systems/Battle/{ExpeditionSystem, DungeonSystem}.cs`, `Data/ExpeditionDatabase.cs`; tests: new `{EventCalendar, SeasonPass, Comeback, Coupon, ExpeditionLadder, DungeonBatch, DungeonBlessing}Tests.cs`, existing `{IdleChest, Mission, DailyClock, ExpeditionSystem, DungeonSystem}Tests.cs` |
| **P4-3a** | `Core/Audio/**`, `Core/UISfx.cs`, `Core/UISfxTable.cs`, new `Editor/Audio/{AudioTableBuilder, AudioContent, AudioImportPostprocessor}.cs`, generated `Resources/Audio/AudioCueTable.asset`; tests: new `{SfxGate, AudioDuck}Tests.cs`, existing `FeedbackGateTests.cs` |
| **P4-3b** | `Editor/Builders/{BattleSceneBuilder, AttackFxBuilder, EnemyFxBuilder}.cs`, `Scripts/UI/Battle/**` (incl. `BattleFx*`, `FxGalleryView`, `SkillCalloutView`, new `{BattleSfx, CalloutGate, BattleStatsRecorder, BattleStatsPopup, DefeatAnalyzer, DefeatAnalysisView, LampPolicyPicker, GuardianPetView}.cs`), `Scripts/Systems/Battle/{BattleSimulator*, BattleSystem, BattleTypes}.cs`, `Scripts/Data/BattleConfig.cs`, `Scripts/UI/Common/StageClearRewardFlyer.cs`; tests: new `{CalloutGate, LampPolicy, BattleStatsRecorder, DefeatAnalyzer, BattleSfx, SleepSignal}Tests.cs`, existing `{BattleSimulator, BattleSystem, AttackFxCatalog, EnemyFxCatalog}Tests.cs` |
| **P4-4** | `Editor/Builders/{MenuSceneBuilder, SettingsSceneBuilder}.cs`, `Scripts/UI/Main/{GrowthPanel, TopBarView}.cs`, `Scripts/UI/Popups/SettingsPopup.cs` + new `Scripts/UI/Popups/{SaveImportPopup, BackupRestorePopup, CouponPopup}.cs`, `Scripts/UI/{Shop, Expedition, Formation, Synergy, Bag}/**` (incl. `GrowthLineageCardView`, new `Expedition/BlessingPickPopup.cs`) |
| **P4-5** | `Editor/Builders/CharacterSceneBuilder.cs`, `Scripts/UI/Characters/**` (+ new `{FriendTicketPopup, GachaMilestonePopup}.cs`), `Scripts/UI/Main/GachaPanel.cs`, `Scripts/UI/Popups/GachaResultPopup.cs` |
| **P4-6** | `Editor/Builders/{MetaSceneBuilder, TutorialSceneBuilder, TutorialSceneBuilder.Art}.cs`, `Scripts/UI/Meta/**` **except** `SaveCode.cs` (+ new `{CodexPopup, CodexCellView, EventHubPopup, EventStartCard, SeasonPassPopup, ComebackCard, StoryPagePopup, ChapterMapPopup, ChapterChestPopup, NotificationAskPopup, JournalRowModel}.cs`), `Scripts/UI/Tutorial/**` |
| **P4-7** | `Packages/manifest.json`, package-created `ProjectSettings/*Notifications*` asset, new `Scripts/Platform/**` (asmdef + `MobileNotificationBackend.cs`), `Scripts/Core/{SaveStorage, GameBootstrap, LocalNotifications}.cs` + new `Scripts/Core/{ShareSheet, PlayLog, PlayLogRecorder}.cs`, `Scripts/Systems/Meta/NotificationPlanner.cs`, `Scripts/UI/Meta/SaveCode.cs`; tests: new `{NotificationPlanner, SaveCodeImport, SaveBackup, PlayLog}Tests.cs`, existing `SaveStorageTests.cs` |
| **P4-8** | `Editor/Content/**` (incl. `MetaContent.Ftue.cs`; new `MetaContent.{Achievements, Codex, GachaMilestones, Affinity, Story, ChapterChests, Events, Season, Comeback, Coupons, Blessings}.cs`), `Editor/MetaDataBuilder.cs`, `Editor/PowerCalibration.cs`; test: new `ContentMetaTests.cs` |
| **P4-9** | `Editor/IdleProjectSetup.cs`, new `Editor/Builders/AtlasBuilder.cs`, `Editor/FxGallery.cs`, `Verify/**` (new `Runtime/PerfProbe.cs`), `Tests/PlayMode/**` (incl. new `GachaRevealTests.cs`, `LampPickerTests.cs`), new `Tests/EditMode/EconomyFlowTests.cs`, `Scripts/Data/GameConfig.cs` + `Data/GameConfig.asset` (CR-07 tuning), scratchpad scripts (`verify.ps1`, `fx_capture.ps1`, new `phase4/perf_capture.ps1`) |
| **P4-10** | `Assets/_Project/Art/**` (new files only; existing art untouched), new `Assets/_Project/Audio/**`, `Editor/ArtImportPostprocessor.cs`, `Editor/UiArt.cs` (new accessors only), `scratchpad/phase4/{art,audio}/**` |
Untouched in phase 4: `UIManager`, `UIPopup`, `MainTabController`, `IdleSceneBuilder`, `SceneParts`, `UIKit`, `UITheme`, `Scripts/UI/Common/**` except `StageClearRewardFlyer`, `FeatureGateSystem`, `TutorialSystem`, `AttendanceSystem`, `ShopSystem`. If a package needs one of these, it reports the need; it does not edit the file.

**Critic additions to ownership (v1.1, §13).** New files and the existing files that had no phase-4 owner:

| Package | Adds |
|---|---|
| P4-0 | the §2 critic members: save sections, `CostumeUnlockType.Season`, `EventScheduleType.Dates`, `StrengthenKind`, the MetaDatabase classes and fields, the BattleSystem/DungeonSystem/Haptics hook members, the RedDotPaths `JournalStarter` constant, the stubs below |
| P4-1 | new `Systems/Meta/{StrengthenAdvisor, CheerSystem}.cs`; `PowerSystem.PreviewShopLevels(int)` and `PreviewAutoFormation()`; tests `{StrengthenAdvisor, Cheer}Tests.cs` |
| P4-2 | new `Systems/Meta/{StarterWeekSystem, DailyGiftSystem, FireflyRules}.cs`; tests `{StarterWeek, DailyGift, FireflyRules, DungeonTicketBank}Tests.cs` |
| P4-3b | new `UI/Battle/{CheerChipView, StrayFireflyView}.cs`; `DangerArrowView`, `BattleHud`, `EnemyView` (boss tick), `BattleView.Enemy.cs` (lamp banner flag) already sit in its `UI/Battle/**` |
| P4-4 | new `UI/Shop/DailyGiftCardView.cs` |
| P4-5 | the CR-11 copy lines in `CharacterSceneBuilder.cs` and `UI/Characters/EvolutionCraftPopup.cs` (already its files) |
| P4-6 | `UI/Meta/StrengthenPopup.cs` (built by UX WP-7), new `UI/Meta/StarterWeekPanelView.cs`; `UI/Tutorial/{FtueBattleGuard, FtueDirector}.cs` (already its files) |
| P4-7 | `Scripts/Core/Haptics.cs` (**no phase-4 owner in v1**), new `Plugins/iOS/LLHaptics.mm`, new `Scripts/Platform/IosHapticsBackend.cs` (Platform asmdef, `#if UNITY_IOS && !UNITY_EDITOR`) |
| P4-8 | new `Editor/Content/MetaContent.{StarterWeek, DailyGifts, Firefly, Cheer, BossHints}.cs`; the `jjak_starwalk` costume row in `CharacterContent.Jjakjjak.cs` |
| P4-10 | A14 `Art/Costumes/jjak_starwalk.png`; the 5 new cues (§10.2) |

Every 4.1 UI package also runs UX WP-9's `UiSceneLintTests` (Filled-sprite guard, text floor) on verify_proj before handoff, so new popups meet the same lint as the UX pass.

### 9.3 Cross-package contracts (provider → consumers)
| Provider | API | Consumers |
|---|---|---|
| P4-0 | everything in §2 (save v4, enums, MetaDatabase defs, IMetaModule, Game props, hooks, UINav, AppSignals, RedDotPaths, PopupQueue kinds, GameSettings keys) | all |
| P4-1 | `Codex.{IsUnlocked, CompletionPercent, GetEntries, ClaimableCount, TryClaim, ClaimAll, HasSeenSkill, MarkSkillSeen}` | P4-6 popup, P4-5 entry, P4-3b FX-06 |
| P4-1 | `Achievements.{GetLines, ClaimableCount, TryClaim, ClaimAll}` | P4-6 |
| P4-1 | `GachaMilestones.{Pulls, GetSteps, TryClaimNext, ClaimAll, FriendTickets, TryUseFriendTicket}`; `GrantResult.StartLevel` | P4-5 |
| P4-1 | `Affinity.{GetLevel, GetClaimedLevel, GetNextThreshold, TryClaim, TryPet, PetsLeftToday, GetStoryLine}` | P4-5 detail, P4-3b pet |
| P4-1 | `ChapterChests.{TryRollLostChest (via BattleSystem.ClearDropHook), OpenChapterChest}`, `Story.{Get, MarkSeen, IsSeen}`; `Power.GetDungeonRecommended(int)` | P4-3b, P4-6, P4-4 |
| P4-2 | `Events.{GetActive, GetMissions, TryClaimMission, CanStamp, TryStamp, MarkSeen, HasClaimable}`, `Season.{…}`, `Comeback.{…}`, `Coupons.TryRedeem` | P4-6, P4-4 |
| P4-2 | `Expedition.{TryStart(zone, ids, tier), GetDurationMinutes, RewardScale, IsTierAllowed, ResendAll, CountResendable}`, `Dungeon.{SweepMany, Blessings, BlessingPicksPending, GetBlessingOffer, TryPickBlessing, CanRespec, Respec}` | P4-4 |
| P4-3a | `AudioSystem.{Boot, Resolve, Play, PlayBgm, StopBgm, Duck, Unduck, ApplySettings, HasClips}`, `AudioKeys.*`, `AudioTableBuilder.Build()` | P4-3b, P4-4, P4-5, P4-6, P4-9 (BuildAll call) |
| P4-3b | `BattleSystem.{LampPolicy, DebugStress}`; scene object `Battle/Dev/FxGallery` (inactive); `UINav.BattleStatsRequested` receiver; `SkillCalloutView.SayPet` | P4-9, P4-6 |
| P4-4 | `UINav.FormationSlotPulseRequested` receiver (FormationPanel), EventChip route, settings rows calling P4-7 APIs | P4-3b (CR-01) |
| P4-5 | `AppSignals.RaiseGachaRevealHigh` call site | P4-3a |
| P4-6 | `AppSignals.RaiseSleep` call site; MetaNavigator receivers for `CodexRequested`/`EventsRequested`/`SeasonRequested`/`ChapterMapRequested`/`IntroReplayRequested`; `MetaPopupPresenter` cases for Comeback/EventStart/ChapterChest/StoryPage/NotificationAsk | P4-3a/b, all |
| P4-7 | `SaveCode.TryParse`, `ISaveStorage.{Replace, SnapshotDaily, ListBackups, RestoreBackup}`, `GameBootstrap.ReloadAfterReplace`, `ShareSheet.{CanShare, Share}`, `LocalNotifications.{Permission, RequestPermission, OpenSystemSettings}`, `PlayLog.Summary(…)` | P4-4, P4-6 |
| **Critic (§13)** | | |
| P4-1 | `Strengthen.GetRows`; `Cheer.{CanUse, IsActive, Remaining, TryUse, Changed}`; `Power.{PreviewShopLevels, PreviewAutoFormation}` | P4-6 sheet, P4-3b CheerChip |
| P4-2 | `StarterWeek.{IsActive, DayIndex, DaysLeft, Points, GetTasks, TryClaimTask, TryClaimChest, ClaimAll, HasClaimable}`; `DailyGift.{CanClaim, Today, TimeUntilNext, TryClaim}`; `Fireflies.{CanSpawn, NextDelay, TryCatch}`; the dungeon ticket bank (`TicketsLeft` semantics) | P4-6, P4-4, P4-3b, P4-7 |
| P4-3b | `BattleSystem.{PresentationScale, ExtendEndPause, SkipEndPause, ForceLampFull, RequestUnitRefresh}`; `BattleSetup.Protection` in the simulator | P4-6 (FTUE), P4-0 (resolver wiring) |
| P4-6 | sets `AppSignals.{FtueFirstLampBeat, FtueFirstImpression}`; `GuideQuestBarView.TryGetRect` (exists) | P4-3b |
| P4-8 | MetaDatabase content; `MetaDataBuilder` validation (unique ids, tier order, reward totals within §11.2 budgets, every hint key resolves, every story art path exists or warns) | all tests through `MetaContent.Apply` |
| P4-10 | art files (§10.1) + `UiArt.{ShopCrack(int), StoryPanel(ch, i), MapBg, MapNode(i), GachaBundle(state), Medal(i), AffinityFrame9s, ComebackHeader, SeasonBanner}`; clips (§10.2) | P4-3b, P4-4, P4-5, P4-6 builders; P4-3a table |

### 9.4 Package steps, risks, done-when
**P4-0 Contracts (M, ≈1–1.5 agent-days, sequential).**
- Steps: §2.1–§2.10 in this order: enums → save classes + migration → MetaDatabase defs → IMetaModule + stubs → MetaSession wiring + `AddModule` → Game.SetupMeta → hooks → UINav/AppSignals → RedDotPaths/PopupQueue → GameSettings keys → SystemsTestBase. Then compile check and a v3-save load test (`SaveMigrationV4Tests`: v3 JSON → loads, `codex.backfillPending == true`, every new section non-null, re-save → v4).
- Risks: the MetaSession order change; existing `MetaSessionTests` must stay green. Stubs must never throw (kill switch, §0.2).
- Done when: the full EditMode suite is green on verify_proj and the game is visibly unchanged. **Precondition (critic C-03):** `Hud/BossChallengeButton` exists in Main.unity (UX WP-5 merged). Otherwise stop: the PRG-02 soft-lock is still live.

**P4-1 Collection & progression (L, ≈4–5 d).**
- Order: PRG-05 → F-03 guards → Codex (incl. backfill and bonuses) → Achievements (incl. hidden flags) → Affinity → GachaMilestones (+ friend ticket) → Power tapered/dungeon → ChapterChests (chapter chest = core; lost chest roll = stretch, last) → Story → (critic §13) StrengthenAdvisor + Cheer → CostumeUnlockType.Season handling.
- Risks: collection bonuses change balance (+10% at full forms), so PowerCalibration must be re-run after P4-8 content lands. The codex enemy-kill lookup must not allocate (≈5 kills/s).
- Done when: the §11.1 tests are green and EconomyBudget is in band.

**P4-2 Return, events & idle (L, ≈4–5 d).**
- Order: IDLE-04 ladder + ResendAll → dungeon batch → IdleChest bonus API **+ 별조각 growth cap + 16 h step (critic §13.4, first: it changes every later budget)** → Comeback → EventCalendar → **StarterWeek (§13.6) → DailyGift (§13.7)** → Coupons (stretch) → Season (stretch) → Blessings (stretch) → FireflyRules (stretch, §13.8) → ticket bank (stretch, §13.9).
- Risks: DailyClock local-hour windows across DST and timezone changes (use `DailyClock.ToLocal`); event effects must read effective time.
- Done when: the deterministic schedule tests pass in UTC and in a +09:00 FakeClock zone.

**P4-3a Audio (M, ≈2 d).**
- Order: AudioSystem buses/pool/gate/priority/duck → UISfx facade → AudioCueTable + builder + import postprocessor → settings HasClips.
- Risks: keep `UISfx` public API byte-compatible (UX hooks). No AudioMixer asset.
- Done when: with zero clips the game is silent and has no errors; with the minimal clip set, the UI keys and the 10 battle cues play through the right buses.

**P4-3b Battle (L, ≈4–5 d).**
- Order: lamp policy (sim + save) → **(critic §13.3) PresentationScale + ExtendEndPause/SkipEndPause + BattleProtection/FillLamp/ForceLampFull + TempAttackPercent/RequestUnitRefresh** → T3 numbers → FX-06 gate → sleep mode → BattleSfx hooks → CR-01 analyzer/view (with the C-05 pause) → CR-03 recorder/popup/button → CR-10 pet → **(critic) boss hint + enrage tick (§13.1) → CheerChip (§13.2) → danger-arrow avoidance + ATK polish (§13.5)** → ATK-08 wiring (crack, yarn, stickers) → FxGallery in scene → DebugStress → lost-chest hook use (stretch) → StrayFireflyView (stretch, §13.8).
- Risks: BattleView partials are hot; keep the ATK timing contracts (impact at arrival). New HUD objects must not reparent coach paths (`Hud/*`, `Strip/*`).
- Done when: the §11 battle tests are green and a 20-min battle in the dev player shows 0 exceptions.

**P4-4 Menu UI (L, ≈4 d).**
- Order: settings rows → save import/backup/coupon popups → expedition ladder + resend-all → dungeon gate/rec/batch/blessings → lineage-card gate → formation slot pulse → EventChip route + shop '축제 중' pill → dev 'play log'.
- Risks: `ClaimAllButton`/`ZoneList` coach paths; `Tab0..3` names.
- Done when: captures 28/28e/16 (§11.4) pass.

**P4-5 Character & gacha UI (M, ≈3 d).**
- Order: codex entry + grid power → affinity section → evolution/costume gates → milestone row + popups (+ friend ticket) → gacha reveal (CR-04) + power/catch-up lines.
- Risks: the FTUE first-meeting flow relies on `GachaResultPopup.Closed`; `Pull(int)` must stay confirm-free (smoke test).
- Done when: GachaRevealTests pass and captures 20/20c pass.

**P4-6 Meta UI (L, ≈5 d).**
- Order: journal records tab → codex popup → presenter cases + notification ask → comeback card → story page + chapter map (+ M13 list replacement) → chapter chest popup (core; at runtime it opens before the story page) → event hub + start card → season popup (stretch) → sleep signal → intro replay → (critic §13) journal 첫 주 tab → StrengthenPopup advisor rows + cheer button → FtueBattleGuard/FtueDirector switch to the battle API → 16 h / capped-growth copy.
- Risks: popup sizes vs the M48 stacked dim; FTUE suppression for every new kind.
- Done when: captures 23b/30–36 pass.

**P4-7 Platform & data (M, ≈2.5 d).**
- Order: SaveCode.TryParse + storage API + tests → reload path → share sheet → PlayLog → notification core (planner + facade) → platform asmdef + package → icons wiring (with P4-10).
- Risks: the package isn't visible to the dotnet check until Unity regenerates csproj (keep it isolated, §4.3); Android 13 permission; iOS needs a provisioning-signed build to verify.
- Done when: the planner/import/backup tests pass and a dev APK schedules 3 notifications (manual).

**P4-8 Content (M, ≈3 d).**
- Order: features/cards → achievements → codex config/hints/enemy texts → milestones → affinity lines → story text → events → comeback → chapter chests + lost chest tables → season → blessings → coupons (QA hash) → PowerCalibration two-segment fit + dungeon fit.
- Risks: copy volume (≈200 lines); 해요체 lint (no '습니다'/'개방').
- Done when: `MetaDataBuilder.Validate` reports 0 errors and ContentMetaTests pass.

**P4-9 Tools, perf & verify (M, ≈2.5 d, wave 4.2).**
- Order: BuildAll call order (content → meta → AudioTableBuilder → atlases → scenes) → AtlasBuilder → FxGallery capture fix check → PerfProbe + perf_capture.ps1 → EconomyFlowTests + tuning → AutoShot captures §11.4 → PlayMode tests.
- Risks: license quirk (batch must use `-quit`, PlayMode cannot run in batch, so use the dev player + AutoShot).
- Done when: §11 acceptance is all green and the numbers are logged.

**P4-10 Assets (M, ≈2–3 d).**
- Order: Higgsfield images (§10.1, budget plan) → PIL slicing → importer rules (Story strips no-mip, Meta default) → UiArt accessors → audio synth (§10.2) → loudness check.
- Risks: credit overrun; style drift (use existing art as `image_references`).
- Done when: every file listed in §10 exists or is marked "top-up (deferred)".

---

## 10. Assets

### 10.1 Art (P4-10; follow the art-style memory and use existing art as `image_references`)
**Reuse first (0 credits):**

| New screen / feature | Existing art reused |
|---|---|
| Achievements (Q-04) | category icons: `Icons/quest` (수선), `Enemies/enemy_dust` (전투), `Icons/lamp`, `Icons/echo`, `Icons/guardian` (친구), `Icons/shards` (진화), `UI/tab_expedition`, `Icons/echo` (던전), `UI/tab_gacha`, `Icons/resonance` (시너지), `UI/tab_growth` (수선점), `Icons/star` (친구 레벨), `Icons/journal`, `Icons/attendance`, `Costumes/jjak_cheer` (의상), `Icons/affinity`; panels `PanelLight` FramedPanel, `MissionRowView` template |
| Codex (Q-05) | `Characters/*`, `Costumes/*`, `Enemies/*`, `Synergy/*`, `Icons/trait_*`/`style_*`, `Items/*` (silhouettes are tinted copies of these, not placeholders), `UiArt.SlotFrame9s`, `RibbonGold9s`, `Icons/check`, `Icons/journal` as the book icon |
| Affinity (CR-10) | `Icons/affinity` (heart), `UiArt.Bar` |
| Gacha milestones / friend ticket (PRG-04) | `UiArt.BarFrame9s/BarFill9s`, `PipButton`, `Icons/ticket_quick` style chips, `Characters/*` |
| Defeat ring (CR-01) | `UiArt.RingStitch` (Filled radial, tint-able), `VFX/motif_exclaim` |
| Lamp picker (CR-02) | `Icons/auto`, `Enemies/boss_shadow`, `UI/tutorial_finger`, `Icons/check`, `UiArt.PanelNight9s` |
| Battle stats (CR-03) | `Icons/quest`, `Icons/skill_buff_crown`, `UiArt.PlateRound`, `SlotFrame9s` |
| Event hub / start card (EVT-01) | `UI/shop_event_banner`, `UI/dungeon_banner`, `UI/zone_exp_*`, `UI/idle_basket_*`, `Icons/currency_event` |
| Comeback card (CR-06) | `UI/return_header` (fallback), `UiArt.StampRing`, `UI/attendance_stamp` |
| Chapter chest + lost chest (PRG-06) | `UI/journal_chest_closed/_open` |
| Blessings (PRG-07) | `Icons/skill_*` (33), `UiArt.CardFrame9s` |
| Season (Q-06) | `UiArt.MedallionDark`, `RibbonLavender9s`, `shop_event_banner` (fallback banner); tier-30 costume = new A14 `jjak_starwalk` (critic C-06; until it exists, tier 30 pays the fallback) |
| Critic additions (§13) | first week: `Icons/journal`, `MissionRowView`, `MissionChestView`; daily gift: `UI/journal_chest_closed`, `MetaRewardChip`; firefly: `StageAmbienceView` firefly sprite + `FX/glow_soft`; cheer chip: `Costumes/jjak_cheer` (the research's '응원대장 짝짝이'), `UiArt.PillNight9s`; boss hint: `BattleNoticeView` + `Enemies/boss_*` icons |
| Notification ask (NOTI-01) | `UI/idle_basket_3` |
| Settings, import, backup, coupon popups | `PanelInset9s`, existing buttons |

**New Higgsfield images** (`gpt_image_2_5`; balance 30.38; costs: high 1.5 / xhigh 2.5 / max 5):

| # | File(s) after slicing | Generation | Quality | Credits | Plan |
|---|---|---|---|---|---|
| A1 | `Art/Story/ch{1..5}_{1..3}.png` (15 panels, cover-cropped to 940×380 display; keep 1024×~480 source) | **5 portrait sheets 1024×1536**, each = 3 stacked panels with cream gutters (one chapter per sheet; refs: `Story/intro_1..3`, character art); PIL slices | high | 7.5 | budget ✔ |
| A2 | `Art/Story/map_bg.png` 1024×1536 | night-town path with 10 streetlamp spots, top-down storybook map | high | 1.5 | budget ✔ |
| A3 | `Art/UI/map_node_{off,on,boss,chest}.png` 512² each | 2×2 sheet 1024², transparent | high | 1.5 | budget ✔ |
| A4 | `Art/UI/gacha_bundle_{closed,glow,open}.png` (+1 spare) 512² | 2×2 sheet: lost-and-found cloth bundle tied with ribbon, 3 states | high | 1.5 | budget ✔ |
| A5 | `Art/Battle/shop_crack_{1,2,3}.png` 768² | **1 image edit** of `Battle/shop.png` (damaged: cracked boards, torn awning, loose shingles), then PIL difference mask → 3 progressive overlays (33/66/100% of the diff area by distance from the door) | xhigh | 2.5 | budget ✔ |
| A6 | `Art/VFX/enemy_death_yarn.png` 256, `fx_rust_stain.png`, `fx_moth_hole.png` 64 (+ spare) | 2×2 sheet 1024², transparent | high | 1.5 | budget ✔ |
| A7 | `Art/UI/medal_{bronze,silver,gold,hidden}.png` 256 | 2×2 sheet: stitched felt medals, '?' medal | high | 1.5 | budget ✔ |
| A8 | `Art/UI/affinity_frame_9s.png` 192 (border 48), `affinity_badge.png` 128, `fx_heart_burst.png` 256, `affinity_halo.png` 256 | 2×2 sheet 1024² (frame cell must have straight sides for 9-slicing) | high | 1.5 | budget ✔ |
| A9 | `Art/UI/comeback_header.png` 1536×560 | 1536×1024, crop | high | 1.5 | optional ✔ |
| A10 | `Art/UI/season_banner.png` 1024×384 | 1536×1024, crop | high | 1.5 | optional, **dropped from the budget plan by critic C-06** (fallback `shop_event_banner`) |
| A14 (critic) | `Art/Costumes/jjak_starwalk.png` (same size and pivot as the other `Costumes/jjak_*`) | 1 image edit of `jjak_base` with `jjak_sleepcap` + `jjak_nightkeeper` as `image_references` (night-walk scarf, star lantern, soft cream/lavender, gold accent only) | high | 1.5 | budget ✔ (takes A10's slot) |
| A11 | `Art/Characters/{base}_pose10.png` ×7 (CR-10 Lv10 idle variant) | 7 image edits of the base forms | xhigh | 17.5 | **needs top-up** (code-driven sway until then) |
| A12 | A1 upgrade: 15 separate story panels at full resolution | 15 × xhigh | xhigh | 37.5 (replaces A1's 7.5) | **needs top-up** |
| A13 | notification icons | derived with PIL from `Icons/currency_gold` / `Battle/shop` (§10.3) | – | 0 | ✔ |

- **Budget plan (A1–A10): 22.0 credits** + ~25% reroll reserve (5.5) = **27.5 ≤ 30.38**. It fits with ≈2.9 spare. Generate A1–A8 first and A9/A10 only if credits remain.
- **Critic C-06:** A14 (1.5) replaces A10 (1.5), so the plan is still 22.0 + 5.5 = 27.5. Order: A1–A8 → A14 → A9.
- **Quality plan:** A1 at xhigh (+5.0) → 27 + reserve ≈ 33.8, so a top-up of ≈5–10 is needed. **Full plan** (A11 + A12) ≈ 77 credits, a top-up of ≈50.
- Rules: transparent background except A1/A2/A9/A10; no white backgrounds; mid-weight outline; gold only as accent (`lost-and-lovely-art-style` memory). Importer: `Story/*` and `*_header`/`*banner` → no-mip (existing `IsLargeBackdrop` covers `Story/`); `UI/*_9s` → 9-slice 48.

### 10.2 Audio (AUD-01; P4-10 produces, P4-3a tables)
**Higgsfield finding (checked 2026-11-03 with read-only `models_explore` (list, get, search, recommend) and the `balance` and `transactions` reads).**
- The audio catalog lists `seed_audio`, `qwen_audio_tts`, `elevenlabs_v4`, `elevenlabs_v4_turbo`, `text2speech_v2` and `inworld_text_to_speech` (all **speech/TTS**), plus `mirelo_text_to_audio` (SFX) and `sonilo_music` (music).
- `generate_audio` states that mirelo, sonilo and inworld **exist only for the game-generation pipeline and must not be used for standalone audio**. `suno_music` is not in the catalog.
- `models_explore` does not expose per-generation audio prices (only `generate_audio get_cost:true` would, and it was not called).
- **So no Higgsfield model can make this game's SFX or BGM: Higgsfield credits needed = 0 for both the minimal and the full set, and the UX-SPEC M18 Phase B plan ("Higgsfield generate_audio") cannot run as written.** TTS could only produce voiced barks, which the design does not use.

**Routes (user decision; recommended = A now, B or C later):**

| Route | Cost | Quality | Notes |
|---|---|---|---|
| **A. Procedural synthesis** (`scratchpad/phase4/audio/synth.py`, numpy) | 0 credits, ≈1 agent-day | good for bells, chimes, plucks, music box, whooshes; modest for foley; BGM = simple music-box arrangement | deterministic (seed per key), 2 variants for frequent cues |
| B. CC0 packs (e.g. Kenney audio packs, CC0 1.0) | 0 credits; needs download approval | high for SFX/jingles | license notice file required; mix and loudness pass |
| C. External generator or composer (outside Higgsfield) | user's account/budget | best | outward-facing: ask first |

**Clip spec:** 44.1 kHz, 16-bit WAV. SFX mono, BGM stereo. Target −14 LUFS for music and jingles, about −16 dBFS RMS for SFX; peak ≤ −1 dBFS; 2 ms fade-in and 10 ms fade-out; seamless loops (0.5 s crossfade at the loop point). Files go to `Assets/_Project/Audio/{Sfx,Bgm}/{key}.wav` (variants `{key}_2.wav`).

**Cue list.** **Minimal set = 23** (☆). **Full set = 61 SFX + 4 music.** **Critic C-10: minimal 24, full 66 SFX + 4 music** (rows marked 'critic' below).

| Group | Keys (len, synthesis recipe) |
|---|---|
| UI (12, all ☆; = UISfxKey) | `ui_tap` 0.06 wood tick · `ui_tab` 0.12 paper flip · `ui_popup_open` 0.18 felt swish + bell · `ui_popup_close` 0.14 falling swish · `ui_deny` 0.18 muted double thud · `ui_coin_land` 0.12 button click + tiny bell (rise +3%) · `ui_gem_land` 0.25 glass sparkle · `ui_level_up` 0.9 xylophone arpeggio · `ui_stamp` 0.3 thump + ink squish · `ui_claim` 0.35 chime · `ui_gacha_flip` 0.15 card flip · `ui_gacha_rare` 1.0 music-box flourish |
| Lineage hit (8) | `atk_needle` 0.2 ping 2.6 kHz + pop '칭·퐁' · `atk_button` 0.15 double wood click '딸깍' · `atk_key` 0.2 latch click + ring '찰칵' · `atk_candle` 0.3 noise flare LP sweep '화륵' · `atk_rain` 0.25 droplet + splash '톡·찰방' · `atk_kiss` 0.15 soft pop + blip '쪽' · `atk_musicbox` 0.4 tine (pitched by code) '띵' · `atk_generic` ☆ 0.15 soft pop |
| Lineage fire (7, full) | `fire_{needle,button,key,candle,rain,kiss,musicbox}` 0.12–0.2 swish/whirr/zip/whoosh/drip/blow/wind-up |
| Enemy strike (9) | `enemy_puff` ☆ '푸슉' · `enemy_flutter` '파닥' · `enemy_splat` '철퍽' · `enemy_whip` '휙' · `enemy_scratch` '스윽' · `enemy_clang` '깡' · `enemy_boing` '끼릭' · `boss_wave` '웅—쿵' 1.2 · `boss_vortex` 1.0 |
| Death (4) | `death_dust` ☆ poof + sparkle · `death_moth` powder flutter · `death_metal` jingle scatter · `death_shadow` rising unwind chime |
| Battle moments (13) | `hit_crit` ☆ '팅' 0.2 · `shield_block` 0.15 · `shield_break` '쨍' 0.3 · `shop_hit` ☆ '쿵' + creak 0.4 · `boss_windup` 0.35 low swell (siege telegraph) · `lamp_lit` ☆ bell + warm chord 1.2 · `boss_appear` ☆ low cello 1.5 · `stage_clear` ☆ xylophone 0.9 · `stage_fail` ☆ soft descent 1.0 · `echo_summon` ☆ sparkle rise 0.3 · `echo_dissolve` shimmer 0.4 · `guardian_pulse` soft chime 0.3 · `skill_cast` whoosh + sparkle 0.4 |
| Meta (8) | `chest_open` 0.8 · `basket_claim` 0.6 rustle + coins · `quick_idle` 0.8 swirl · `unlock_card` 1.0 mini fanfare · `gacha_bundle_drop` 0.3 · `gacha_bundle_open` 0.5 · `achievement_claim` 0.5 medal clink · `pet_squeak` 0.2 |
| Critic (5) | `boss_defeat` ☆ 1.2 music-box flourish + low chime (T5, the research's '보스 처치') · `death_spring` 0.35 '보잉' spring bounce (windup kill) · `firefly_appear` 0.4 soft glass shimmer · `firefly_catch` 0.3 tiny bell + pop · `cheer_start` 0.8 three rising plucks + soft clap |
| Music (4) | `bgm_battle_night` ☆ 90 s loop, 76 BPM, F major, music box + celesta + pizzicato, the lullaby motif whose last note never sounds · `bgm_boss` 40 s loop, D minor, cello ostinato + soft drum (chapter bosses only) · `jingle_gacha_high` 4 s · `jingle_chapter_clear` 5 s · (optional `bgm_story` 60 s for the storybook/intro) |
**Credit estimate:** minimal 23 clips = **0 Higgsfield credits**; full 65 clips = **0 Higgsfield credits** (no usable model). After critic C-10: minimal 24, full 70 clips, still 0 credits. Routes A and B are free; route C is priced by the user's chosen service.

### 10.3 Notifications package and icons (P4-7 wiring, P4-10 icons)
- Package `com.unity.mobile.notifications` **2.4.3** (Unity 6000.2's released version; 2.4.2 is also listed).
- Android 13+ shows the POST_NOTIFICATIONS runtime prompt through `RequestPermission`. Target SDK is "automatic" today (`AndroidTargetSdkVersion: 0`), so the prompt works on API 33+ devices. Min SDK 23 is unchanged.
- No exact alarms: inexact scheduling is fine for these reminders.
- Icons: `Assets/_Project/Platform/NotificationIcons/ll_notify_small.png` (96×96, pure white silhouette on transparent, derived with PIL from `Icons/currency_gold` button outline) and `ll_notify_large.png` (192×192, from `Battle/shop.png`). Register both in Project Settings › Mobile Notifications › Android icons with ids `ll_notify_small` / `ll_notify_large`.
- iOS: authorization at launch off; alert + sound + badge requested at the in-game ask. Badge cleared on resume.

---

## 11. Verification

### 11.1 EditMode tests (new files unless noted; all run in the verify_proj batch)
`SaveMigrationV4Tests`, `AchievementTests`, `CodexTests`, `AffinityTests`, `GachaMilestoneTests`, `CatchUpLevelTests`, `FeatureGuardTests`, `ChapterChestTests`, `StoryTests`, `EventCalendarTests`, `SeasonPassTests`, `ComebackTests`, `CouponTests`, `ExpeditionLadderTests`, `DungeonBatchTests`, `DungeonBlessingTests`, `SfxGateTests`, `AudioDuckTests`, `BattleSfxTests`, `CalloutGateTests`, `LampPolicyTests`, `BattleStatsRecorderTests`, `DefeatAnalyzerTests`, `SleepSignalTests`, `NotificationPlannerTests`, `SaveCodeImportTests`, `SaveBackupTests`, `PlayLogTests`, `ContentMetaTests`, `EconomyBudgetTests`, `EconomyFlowTests`.
Updated: `FeedbackGateTests` (±6%), `PowerTests` (tapered, dungeon), `MetaSessionTests` (module order), `IdleChestTests` (bonus cap, night/event lines), `MissionTests` (JournalPoints), `ExpeditionSystemTests`, `DungeonSystemTests`, `CharacterSystemTests`, `GachaSystemTests` (IsGuaranteed), `AttackFxCatalogTests` (Skill numbers).
**Critic additions:** new `StarterWeekTests`, `DailyGiftTests`, `FireflyRulesTests`, `StrengthenAdvisorTests`, `CheerTests`, `BattleProtectionTests`, `DungeonTicketBankTests` (stretch). Updated: `IdleChestTests` (gem cap ×1.5, 16 h step), `BattleSystemTests` (PresentationScale, ExtendEndPause/SkipEndPause, TempAttackPercent), `AttackFxCatalogTests` (ttokdan art spin, echo streak only for snipe), `CostumeSystemTests` (Season unlock), `EconomyBudgetTests` (critic rows), the FtueBattleGuard tests (now driving the battle API instead of reflection).
**Gate:** the full suite (659 today + new) passes. The v3 player save (`ux_shots/autoshot_player.json` copy) loads, plays 60 s headless and re-saves as v4.

### 11.2 Economy budget (`EconomyBudgetTests`, P4-1; numbers from content via `MetaContent.Apply`)
| Source | Budget (별조각) | Spec value |
|---|---|---|
| Achievements, lifetime | ≤ 3,500 | 3,400 |
| Codex first-finds, lifetime | ≤ 2,700 | 2,615 (v1 2,585; +1 costume, C-06) |
| Affinity, lifetime (7 lineages) | ≤ 4,000 | 3,780 |
| Story (ch1–5) + chapter chests (ch1–5) | ≤ 900 | 250 + 590 = 840 |
| Season, per 28 days | ≤ 450 | 390 with the costume; 440 with the fallback (v1 fallback 300 gave 690, C-06) |
| Starlit walk, per month | ≤ 300 | 250 |
| Comeback, per trigger | ≤ 300 | 300 |
| Gacha milestones, first 200 pulls | ≤ 150 | 100 (+ items) |
| **Critic rows** | | |
| Basket 별조각 line, per day, **at any highest stage** (C-01) | ≤ 140 incl. quick idle | 108 (4.5/h × 24 h at the ×1.5 cap) + 9 per quick idle |
| First week (NEW-01), one-time | ≤ 350 | 300 |
| Daily gift (NEW-02), per week | ≤ 30 | 20 |
| Fireflies (NEW-03), per week, every one caught | ≤ 60 | 42 (6/day × 10% × 10 × 7) |
| Night shop +20% on the 별조각 line, per week | ≤ 40 | ≈ 24 at the cap (5 of 24 h × 20%) |

**Critic C-01: the basket's 별조각 line has no ceiling today.** `IdleChestSystem.PerHour` = amount × 1.02^(highest stage − 1) for every line (`MetaContent.IdleChest.cs`, `stageGrowth = 1.02`), and main stages are endless (tapered HP growth, no last stage). The gem line is 3/h at 1-1, 7.9/h at 5-10 (≈1,330 a week at two full 12 h claims a day), 21/h at 10-10 (≈3,580 a week) and ≈156/h at stage 200. v1's "basket ≈500" only holds up to about chapter 2. Fix (§13.4): `maxStageMultiplier` 1.5 on the 별조각 line only (cap reached at about 3-2). Material and gold lines keep growing. `EconomyBudgetTests.BasketGemLine_IsBoundedAtStage1_20_50_100_200` asserts the row above.

Projected steady-state income (week 4+, typical player; **revised by the critic**): journal 1,000 + basket ≈560 (≈18 h of accrual credited per day at the cap) + night bonus ≈20 + attendance ≈325 + affinity ≈265 + season ≈110 + events ≈60 + daily gift 20 + fireflies ≈28 + one-time trickle ≈250 ≈ **2,640 별조각/week** (≈1,825 before phase 4, **+45%**; v1 said 2,500 / +37% because it held the basket at 500 and omitted §13). That is a 60-pull pity about every 2.0 weeks. Without C-01, the same player at 10-10 would earn ≈4,900 a week (basket ≈2,700 + night ≈110), and that keeps rising with every stage, so the cap is the most important economy line in this spec.
Knobs: `AffinityConfig.gemPerLevel`, `CodexConfig.*Gem`, the achievement reward table, `IdleChestLine.maxStageMultiplier`, `FireflyConfig.rewards` weights, the Friday row of `dailyGifts`. The test asserts the budgets so that drift is caught.

### 11.3 Performance (P4-9, §3.1)
- `phase4/perf_capture.ps1`: robocopy → reimport → BatchBuildAll → dev Windows player → `-perfBench` (atlas off, then atlas on).
- Assertions read from `perf_summary.json`: frame p95 ≤ 8 ms, BuildBatch avg ≤ 0.6 ms, GC/frame ≤ 64 B, Batches −25% with atlases. Logged, never hidden.
- Device run (manual, §11.5) for the 55 fps / 2 ms targets.

### 11.4 AutoShot captures (P4-9; numbering continues UX-SPEC §5.2)
| # | Name | Setup | Assert |
|---|---|---|---|
| 04b | ftue_levelup_locked | fresh save, FTUE step 2 | lineage cards show '1-1' pill; tap → Error toast; level unchanged |
| 16b | dungeon_locked | 1-5 cleared | `Seg_Dungeon` α≈0.45 with lock; page does not switch |
| 16c | dungeon_rec | 1-10 cleared | '권장 … · 잔향이 …' line + rating word |
| 17b | expedition_ladder | 2-1 cleared, open setup | 3 tier segments (2 on exp_2_4); default = lastTier |
| 17c | resend_all | after claim-all | `ResendAllButton` visible in the ClaimAll rect, `ClaimAllButton` hidden |
| 20b / 20c | gacha_reveal_1 / _10 | Pull(1) at 0.5 s; Pull(10) at 1.0 s | bundle visible; skip button absent at 0.4 s, present at 0.6 s; ≤4 s total for 10 with no ≥4★ |
| 23b | journal_records | 2-3 cleared | rows sorted claimable first; ClaimAll visible; tab badge dot |
| 28e / 28f | settings_account / settings_notify | – | BackupRow + CouponRow inside the window; notify status line matches the permission |
| 30 | codex_friends | 1-10 cleared | % header, 5-col grid, silhouettes for unfound, ribbons for claimables |
| 31 | event_hub | clock = the 3rd of a month, Events unlocked | banner, effects strip with '별빛 사탕 ×2', stamps, missions |
| 32 | season | Events unlocked | Lv line, 30 rows, footer |
| 33 | comeback_card | lastSessionEnd −80 h | card after Return; 3 stamp cells; basket cap shows 24h |
| 34 / 35 | story_page / chapter_map | first 1-10 clear / tap StagePanel | 3 panels + text; 10 nodes, boss node, chest marker |
| 36 | chapter_chest | first 1-10 clear | chest popup before story page |
| 37 | battle_defeat_ring | forced fail at 1-4 | ring + caption, largest segment Danger, not overlapping x 16–484 × 712–796 |
| 38 / 39 | battle_stats / lamp_picker | 1-7 cleared | popup rows sorted; picker 3 rows above the lamp button |
| 40 | sleep_mode_fx | idle 2 min | log `[Verify] sleep_budget=60 numbers=off`; BGM duck logged |
| 41 | affinity_section | detail Info tab | Lv, bar, story lines, claim button when due |
| **Critic** | | | |
| 04c | ftue_first_lamp | fresh save, FTUE step 4 | log `[Verify] first_lamp_beat scale=0.2 time=0.6`; big banner shown; no reflection warning in the log |
| 05b | danger_arrow_vs_guide | 2-1, 8+ enemies on slots 2/3 | no DangerArrow rect intersects the guide bar rect (16–484 × 712–796) |
| 07b | boss_hint | first 1-10 boss | notice line with the boss hint; enrage tick on the StagePanel bar at 50% |
| 22b / 22c | strengthen_sheet / cheer_chip | forced 2 fails at 1-10 | ≤4 advisor rows sorted by score; cheer button; after tap, CheerChip '응원 9:59' inside 836–948 × 440–488 with ≥8 px clearance |
| 23c | journal_first_week | 1-7 cleared, fake clock day 2 | '첫 주' tab, 7 day chips (days 3–7 locked), 5 chests, dot on the journal button |
| 27b | shop_daily_gift | 1-8 cleared | DailyGiftCard above the product grid; after claim the countdown line; `tab.shop.free` dot gone |
| 37 (updated) | battle_defeat_ring | forced fail at 1-4 | ring visible ≥3.0 s; the next attempt starts only after the ring hides; '바로 다시 도전 ›' restarts within 0.4 s |
| 42 | firefly | dev hook `-forceFirefly` | firefly in the field band; tap → reward fly; count 1/6 logged |
Static checks (log only): no new TMP < 18 outside the allowlist; no '개방'/'습니다'; every new coach or popup path resolves; HUD rect overlap check for StatsButton vs pads (≥16 px). Critic: no '장비' in player-facing strings except the `SkillContent` lore line ('모두의 장비를 꼼꼼히 손봐요'); CheerChip vs IdleChestWidget, StatsButton and pads (≥8 px).

### 11.5 Manual checks (user or device owner)
1. Android device (API 33+ and one API ≤32): the soft ask appears only after FTUE when the basket fills; system prompt; 4 notification kinds fire; quiet hours respected; reboot reschedules; tapping opens the app; badge cleared.
2. Android share sheet for the save code and the play log.
3. Mid-range Android, 2× battle stress: ≥55 fps, BuildBatch ≤ 2 ms (Profiler over USB).
4. Ear check of every cue at default volumes, plus the sleep duck and gacha duck.
5. Grayscale pass of the defeat ring and codex states (colour is never the only cue).
6. (critic) iOS device: Light/Medium/Heavy haptics fire through `LLHaptics_Impact` with 진동 on, and never with it off (FX-05 stretch, §13.5 g).

---

## 12. Risks (top first)
1. **Audio has no generation path in Higgsfield.** SFX/music models are game-pipeline-only and suno is absent. The game stays silent until the user picks route A, B or C (§10.2). *Mitigation:* the AudioSystem ships with or without clips; route A (procedural, 0 credits) unblocks immediately; settings keep '준비 중' until `HasClips`.
2. **Art credits are tight.** The budget plan needs 22–27.5 of 30.38. The quality plan needs a ≈5–10 top-up; Lv10 poses and full-size story panels need ≈50. *Mitigation:* 2×2 sheets, story strips, every asset has a fallback, "top-up" items are not generated without approval.
3. **Gem inflation (+45% steady after the critic revision, early bursts from codex, records and the first week).** It could make gacha trivial and saturate shards. *Mitigation:* §11.2 budget test (now including the stage-independent basket row), the C-01 cap, knobs in content, early tiers deliberately small.
4. **Save schema v4 plus save replacement (import/restore).** A bad import could wipe progress. *Mitigation:* `Replace` always writes an `import-*.bak` first; daily snapshots ×3; strict parse plus `NewerVersion` refusal; migration test on a real v3 save.
5. **Hot shared files and the meta kill switch.** One throwing stub turns off all meta. *Mitigation:* P4-0 contract-first, inert stubs, `IMetaModule` (each package owns its system file), MetaSession order test.
6. **Unmeasured mobile performance plus atlas switch-on.** The sprite packer is disabled today, and atlases change batching, mipmaps and compression. *Mitigation:* atlases limited to VFX folders, A/B perf runs, crop pixel-diff gate, Android override 2048 for the skill atlas.
7. **Notifications can't be verified in the agent environment.** The package is invisible to the dotnet check until csproj regenerates; there is no device. *Mitigation:* isolated `IdleGame.Platform` asmdef with `versionDefines`, pure planner tests, manual device checklist.
8. **HUD crowding and coach-path breakage.** New StatsButton, defeat ring, lamp corner icon, pet hit areas and EventChip route. *Mitigation:* fixed rects from §7, capture overlap checks, nothing reparented under frozen paths, all hidden during FTUE.
9. **Calendar and clock abuse.** Events, season and comeback depend on the device clock. *Mitigation:* DailyClock effective time, tamper blocks on every claim, lifetime cap 6 on comeback, per-instance claims.
10. **Scope (≈48 items after the critic pass, 11 packages, ~49 agent-days: v1 ~40 + §13 ≈9).** *Mitigation:* P2 items are marked stretch and cut-safe; P0/P1 order inside each package; wave 4.2 verifies before any stretch work merges. The critic additions that are core (C-01 cap, C-04 chapter chest, C-05 ring pause, §13.1–§13.4, NEW-01, NEW-02) cost ≈6 agent-days; the rest are stretch.
11. **ATK-07.7 capture is still failing in the attack-FX workflow.** *Mitigation:* §3.2 fallback ownership and the builder-owned inactive gallery object.
12. **Economy test duration and assets.** `EconomyFlowTests` simulates 36k ticks, and `GameConfig.asset` keeps inspector values, so changing C# defaults does nothing. *Mitigation:* `[Timeout(180000)]`, tuning by editing the asset YAML (§7.6).
13. **(critic) Existing saves see a lower basket gem rate after C-01** (a 5-10 save drops from 7.9/h to 4.5/h). The game is pre-release, so this is acceptable. The basket popup's '+2%' line must stop promising gem growth once the cap is reached (§13.4).
14. **(critic) A parallel information-architecture study** (`scratchpad/ia/`: 'tall canvas', 'sheet variants', keep-out maps, 2026-11-04) may move StageArea and the HUD. Every StageArea-anchored rect here (§7.2 ring, §7.4 StatsButton, §13.2 CheerChip, §13.8 firefly band) moves with its anchor, but the screen-space clearances in §11.4 must be re-measured if that study becomes a spec. It is not a spec today, so SPEC4 keeps the UX-SPEC §2.1 HUD map as the reference.
15. **(critic) CR-01's end-pause extension** adds up to 3.3 s per analysed fail. The once-per-stage-per-10-minutes rule keeps the farm loop from slowing down.

---

## 13. Critic additions (v1.1): uncovered remainders and new genre systems

Evidence for each item is in §14. Owners follow §9.2 and its critic ownership table. Every new view or popup obeys §0.2: no modal during FTUE, locked means locked, append-only enums, and inert P4-0 stubs.

### 13.1 PRG-02 remainder: chapter-boss hint and enrage tick (P0; P4-3b view, P4-8 content)
UX already covers the farm state, the boss button and the retreat banner (M13). M11 keeps boss HP inside the StagePanel. Three things from the research are in no package: nothing explains a boss's gimmick, the enrage threshold is invisible, and the chapter chest has no owner (now §8.5 core, C-04).
- **Precondition (added to P4-0 done-when):** `Hud/BossChallengeButton` must exist in Main.unity, which proves UX WP-5 has merged. Until it does, the PRG-02 soft-lock is live: once 2-1 is cleared, a step-back leaves no UI that can call `ChallengeBoss`. Phase 4 must not start without it.
- **Hint line.** On `BossSpawned` in a Stage battle, if the boss has a `BossHintDef`, call `BattleNoticeView.Show(boss name, hint, Enemies/{id} icon, Danger tint, 2.4 s)` 1.0 s after the '보스 등장!' banner. Show it in two cases:
  - first encounter: `hint.boss.{id}` is not yet in `PlayerData.flags` (set the flag when it shows);
  - retry: the stage has at least 1 consecutive fail.
  - Not while an FTUE step targets the battle.
  - Copy: Appendix C.1.
- **Enrage tick.** During the boss phase, draw a tick at `BattleConfig.enrageThreshold` (0.5) on two bars:
  - the StagePanel `ProgressBar`: 2×22 px, Cream@0.85;
  - the boss's own `EnemyView` HP bar: 2×10 px.
  - On `EnemyEnraged` the tick turns Danger with one scale pop. The existing enrage notice says it in words, so colour is never the only cue.
- **Tests:** `BossHint_FirstEncounterAndRetryOnly` (the rule lives in a static helper), plus capture 07b.

### 13.2 PRG-03 remainder: how to get stronger (P1; P4-1 advisor and cheer, P4-6 sheet, P4-3b chip and hook, P4-8 copy)
UX M13 phase 2 builds `StrengthenPopup` (860×760) with 3 fixed rows. The research also asks for five things that no package has:
- the cheapest power gains, ranked;
- more kinds of row;
- the auto-formation rule;
- triggers;
- the cheer buff.

**`StrengthenAdvisor`** (pure, `Systems/Meta`). It allocates nothing apart from its output list and is called when the sheet opens and at 1 Hz while it is open.
- `void GetRows(List<StrengthenRow> into, int max = 4)`; `StrengthenRow { StrengthenKind Kind; string TargetId; long Gain; double CostSeconds; FeatureId Nav; string Focus; }`.

| Kind | Candidate | Gain | CostSeconds |
|---|---|---|---|
| ShopUpgrade | n = shop levels affordable within 120 s of gold income (1..10) | `Power.PreviewShopLevels(n)` (new) | `max(0, cost − gold) / max(1, GoldPerSecond)` |
| LineageLevel | each placed lineage, +5 levels or as many as affordable | `Power.PreviewLevelUp` | same formula |
| FillSlot | an empty guardian or deck slot, plus an owned unplaced form that fits it | `Power.PreviewPlace` | 0 |
| SynergyOneShort | a synergy whose next tier needs exactly one more member, provided by an owned unplaced form | PreviewPlace, swapping out the weakest placed unit that is outside that synergy | 0 |
| ResonanceReady | `CharacterSystem` can raise resonance | PowerSystem preview | 0 |
| AutoFormation | only when `Power.PreviewAutoFormation()` (new) ≥ 1.05 × current team power (research rule) | the delta | 0 |
| EvolutionReady | a form can be crafted now | `GetFormPower(new) − GetFormPower(current)` if that lineage is placed, else 0 | 0 |
| MissingMaterial | the nearest recipe (fewest missing materials) lacks one material | 0 | navigates with `UINav.GoToSource(itemId)` |
| WeakDirection | the last fail produced CR-01 data (§7.2) | 0 | – |

- **Ranking:** rows with Gain > 0 come first, sorted by `Gain / (1 + CostSeconds / 60)` descending. Gain-0 rows follow in the order EvolutionReady → WeakDirection → MissingMaterial. A row whose gain is below 0.5% of team power is dropped. At most 4 rows.
- If the M13 sheet computes its auto-formation delta inline, P4-1 moves that code into the advisor unchanged.

**Sheet (P4-6, same popup and size).**
- The header keeps '권장 / 내 / -gap'.
- Up to 4 rows (800×110, existing row style) replace the 3 fixed rows. Each row: kind icon 56 + Title 28 + Sub (Caption 22; '전투력 +{gain:N0}' when Gain > 0) + [가기 ›] Primary 160×64.
- Empty state: Appendix C.5.

**Nudge (never an automatic modal).**
- The research trigger "2연패 → 발동" is already covered by the M13 retreat-banner subtitle, which stays tappable for 3 s.
- The research trigger "반복 모드 10분 넘김" gets this: when `IsFarming` has held the same `challengeTarget` for at least 10 minutes of foreground battle time, BossChallengeButton line 2 reads '강해지는 방법 보기 ›' for 6 s with one MetaPulse. It fires once per challengeTarget. A tap opens the sheet, which the M13 '권장' line already does, so no new rect is needed.

**친구들의 응원 (cheer).**
- **Button:** in the sheet header, Gold 300×72 '친구들의 응원 받기', with the sub line '10분 동안 공격력 +20% · 이 장에서 한 번' (Hint 20). Once used it is Disabled and reads '다음 장에서 다시 받을 수 있어요'.
- **Availability:** after `ftue.done`, once per chapter (the chapter of `challengeTarget` while farming, otherwise the current stage's chapter), and not while the clock is tampered.
- **Effect:** `BattleSystem.TempAttackPercent = () => Cheer.IsActive ? cheer.attackPercent : 0`, plus `Battle.RequestUnitRefresh()` at start and end.
  - It lasts 600 s of real time (`EffectiveUnix`) and keeps running while the app is closed; the button says 10분.
  - It is left out of `PowerSystem`, so the power pill does not jump up and then fall back.
- **CheerChip (P4-3b):** 112×48 at StageArea TopRight(−132,−310), screen ≈ x 836–948, y 440–488 (moved left in RM-2 because IA §3.3 put the StatsButton in rail R1; it is a Wave-3 HUD keep-out, `BattleFx.HudKeepOutPaths`). That is left of the StatsButton and above the right-edge battle keep-out (y ≥ 586).
  - Contents: `PillNight9s` + `Costumes/jjak_cheer` at 40 (the research's '응원대장 짝짝이') + 'm:ss' (Label 22 bold Cream, rewritten once per second).
  - Hidden during FTUE and while asleep; the buff keeps running either way.
  - Tap → toast '친구들이 응원하고 있어요! 공격력 +20%'.
- **Feedback:** `cheer_start` sfx + Light haptic.
- **API (`CheerSystem : IMetaModule`, P4-1):** `bool CanUse`, `bool IsActive`, `float Remaining`, `bool TryUse()`, `event Action Changed`.

**Tests:**
- `StrengthenAdvisorTests`: ranking by score; the ≥5% auto-formation gate; one-short synergy detection; no row with negative gain; at most 4 rows; the empty case.
- `CheerTests`: once per chapter; expiry at 600 s; tamper and FTUE blocks; the TempAttackPercent value; RequestUnitRefresh at start and end.

### 13.3 FX-01 and FTUE-01 battle hooks (P0; P4-3b provides, P4-0 wires, P4-6 consumes)
**Presentation scale (FX-01: marked done, but not done end-to-end).**
- **Today:** `GlobalHitStop` scales only FX and actor animation. `BattleFx.cs:591` states this, and `BattleView.cs:79` uses `actorDt = dt * _fx.TimeScale`. But `GameBootstrap.Update` ticks `Game.Battle.Tick(Time.deltaTime)` unscaled (`:130`), and `EnemyView` follows `State.Position`. So on every boss kill and every lamp, enemies keep sliding while their walk cycle is frozen.
- **Research spec:** a `BattleTimeScale` applied to `BattleSystem.Tick`.
- **Fix:**
  - `BattleSystem.PresentationScale` (§2.5): `_sim.Tick(dt * Speed * PresentationScale)`. The `_ended` end-pause timer and the restart/refresh flags keep using unscaled `dt`.
  - BattleView writes `Game.Battle.PresentationScale = _fx.TimeScale` every frame and sets it to 1 in `OnDisable`.
  - Dungeon sims, offline accrual and EconomyFlowTests never touch it.
  - Cost: at most 0.25 s of sim time per boss kill or lamp.
- **Unchanged:** "multi-hit skills stop only on the first and last hit" is still handled by the 0.12 s impact merge (accepted as is).
- **Tests:** `BattleSystemTests.PresentationScale_ScalesSimOnly`, `…_DoesNotScaleEndPause`.

**Battle protection and lamp fill (FTUE-01).**
- **Today:** `FtueBattleGuard` fills the private `BattleSimulator._lamp` by reflection (`GetField("_lamp", NonPublic)`). It also fakes the shop HP floor by bouncing `SetShopMaxHp` inside `ShopDamaged`. Its own summary says to switch to a battle hook once one exists, and SPEC3 §9 is still open.
- **P4-3b:** `BattleSetup.Protection`, applied inside the simulator:
  - enemy max HP × `EnemyHpScale` at spawn, boss included (split children inherit the scaled parent);
  - shop HP never drops below `ShopFloorHp` (checked before the fail test);
  - `BattleSimulator.FillLamp()` and `BattleSystem.ForceLampFull()` (Stage mode, battle running, gauge < 1).
- **P4-0:** `MetaSession.Connect` sets `BattleSystem.ProtectionResolver` = stage ≤ 3 && (`WantsBattleProtection` || (`IsActive` && `StepIndex == 0`)) → `{true, 0.6, 1}`. This is exactly today's `FtueBattleGuard.IsProtecting`.
- **P4-6:** `FtueDirector` calls `Game.Battle.ForceLampFull()`. `FtueBattleGuard` keeps its public API but forwards to the hooks and drops `System.Reflection`. Static check: no `GetField(` under `Scripts/UI`.
- **First-lamp beat** (research FTUE: "불 밝히기를 처음 보여줄 때만 0.2배 느린 화면"):
  - `AppSignals.FtueFirstLampBeat` (§2.6; P4-0 declares it, `FtueDirector` (P4-6) sets it) is true while the step-4 coach targets the lamp.
  - While it is true, BattleView.OnLampLit (P4-3b) uses `HitFeel.FirstLampIgniteScale` 0.2 for `FirstLampIgniteTime` 0.6 s instead of 0.12 s, and always shows the big banner.
  - Because of PresentationScale, the simulation slows down with it.
- **Step-1 first impression** (research step 1: "처치마다 명중 연출을 크게"): `AppSignals.FtueFirstImpression` (set by FtueDirector during step 1 only) makes guardian hits ×1.3 in size with 2 extra sparkles. Numbers and trauma are unchanged.
- **Tests:**
  - `BattleProtectionTests`: HP scale at spawn, including split children; the shop floor holds at 1; a null resolver means off; dungeons ignore it.
  - `FillLamp_OnlyWhileRunning`.
  - Capture 04c.

### 13.4 IDLE-01 remainder: 16 h cap and the 별조각 ceiling (P0; P4-0 fields, P4-2 logic, P4-6 copy, P4-8 content)
- **16 h step.** Research IDLE-01 (P2) specifies 16 h at shop Lv.200, and `MetaContent.IdleChest.cs:10` still says '예정'.
  - New field `IdleCapStep.shopLevel`. Content adds `{ stageCleared = 0, shopLevel = 200, capHours = 16 }`.
  - A step applies when every non-zero condition holds, and the cap is the largest `capHours` among the steps that apply: 8 h → 12 h at 2-1 → 16 h at Lv.200.
- **별조각 ceiling (C-01).** `IdleChestLine.maxStageMultiplier` = 1.5 on the 별조각 line: when a cap is set, `PerHour = amount × min(StageMultiplier, cap)`.
  - Material and gold lines are unchanged.
  - Quick idle, return doubling (gems were already excluded from it) and the comeback 24 h cap all read `PerHour`, so they inherit the ceiling.
- **Copy (P4-6).**
  - ReturnPopup and IdleChestPopup cap note: Appendix C.6. It shows when the 12 h cap is full and the shop is below Lv.200.
  - Once the 별조각 line is capped, '다음 스테이지 클리어 시 +2%' becomes '다음 스테이지 클리어 시 재료 +2%'.
- **Tests:** `IdleChestTests.GemLine_CappedAtOnePointFive`, `CapStep_ShopLevel200_Gives16h`, `CapStep_AllConditionsMustHold`.

### 13.5 Small remainders (P4-3b unless noted)
- (a) **ATK-01 button spin.** `BattleFx.Attack.cs:209` sets `AlignToPath = true` on every art shot, so the real-art 똑단이 button never spins; only the fallback does.
  - Add `LineageAttackLook.ArtSpin` in deg/s (ttokdan 720, all others 0).
  - When ArtSpin ≠ 0, the art shot uses pivot (0.5, 0.5), `AlignToPath = false` and `Spin = ArtSpin`.
  - Test: `TtokdanArtShot_Spins`.
- (b) **ATK-02 echo streak.** Ranged echo shots draw the key streak only when the style is Snipe, because the research gives a trail to snipe only.
- (c) **ATK-05 / FX-02 radial lamp shake.** Add `TraumaShake.AddRadial(0.55)`: the offset direction follows the noise sample instead of leaning toward `Vector2.up`.
- (d) **ATK-05 banner flag.** Move `fx.lamp_intro_seen` from PlayerPrefs (`BattleView.Enemy.cs:25`) to `PlayerData.flags`, so each new save shows the big banner once.
- (e) **Q-01: danger arrows avoid the guide bar.** The research says "DangerArrowView 화살표는 이 사각형을 피해 놓는다".
  - `GuideQuestBarView.TryGetRect` already exists (static) but nothing calls it.
  - DangerArrowView reads it once per layout change. An arrow that would come within 8 px of the bar slides along the field edge to the nearest position above `bar.yMin − 8`.
  - Check: capture 05b.
- (f) **CR-11: equipment deferral and copy (P4-5).** Equipment stays out of scope (research CR-11); add it to the research §17 list the next time that file is edited. Three player-facing strings still promise equipment:
  - `CharacterSceneBuilder.cs:825`: '레벨 / 장비 / 호감도 공유' → '레벨 / 호감도 공유'.
  - `CharacterSceneBuilder.cs:835`: first line → '진화형은 계보의 레벨 · 호감도를 그대로 나눠 써요.' (second line unchanged).
  - `EvolutionCraftPopup.cs:217` → '레벨 · 호감도는 {name} 계보와 함께 나눠요.' Also fix the XML summary at `:10`.
- (g) **FX-05 on iOS (P4-7, stretch).** UX-SPEC §3.15 made iOS a no-op, but the research asks for `UIImpactFeedbackGenerator`.
  - `Plugins/iOS/LLHaptics.mm`: `void LLHaptics_Impact(int style)`, with one cached generator per style (Light / Medium / Heavy) and `prepare()` on creation.
  - `IosHapticsBackend` (Platform asmdef) registers `Haptics.PlatformOverride` at `BeforeSceneLoad`. HapticGate and the 진동 setting still apply.
  - Checked by hand on a device (§11.5 #6).
- (h) **Audio cues.** `boss_defeat`, `death_spring`, `firefly_appear`, `firefly_catch` and `cheer_start` (§3.5, §10.2). P4-3a adds them to the cue table, P4-3b hooks them up, and P4-10 makes the clips.
- (i) **EVT-01 seasonal hooks (P4-0 reserves now, content later).**
  - Reserve `EventScheduleType.Dates` and `EventDef.fromMonth/toMonth`, so '첫눈 오는 밤' (12/1–14, research EVT-01 P2) can ship later as content only.
  - Those dates overlap the monthly 1–14 starlit walk, so seasonal events are **cosmetic plus stamps only**, never a second candy multiplier.
  - While `jjak_nightkeeper` (NightVisit) is not owned, the 밤의 수선점 effect chip in the hub adds '밤 8시가 지나 들르면 밤지기 짝짝이가 찾아와요' (P4-6). This is the link between the night event and the night visit that the research describes.
- (j) **ATK-09 runtime proof (P4-3b counter, P4-9 assert).** Add a dev-only BattleView counter: a kill counts when its death FX starts and no projectile has arrived at that enemy in the previous 0.30 s. A 60 s AutoShot battle at 2× logs `[Verify] kill_without_arrival=0`, and verify.ps1 asserts it (the research's ATK-09 test).
- (k) **ATK-12 field-clip proof (P4-9).** In the same run, at a lamp's flash peak, pixels outside the StageArea rect must differ by at most 1% from the frame 0.05 s earlier. Log `[Verify] flash_outside_field<=1%`.

### 13.6 NEW-01 첫 주 (first-week mission sheet) (P1, M; P4-2 system, P4-6 tab, P4-8 content)
**Why.** Top idle RPGs pair the 7-day login with a newcomer task sheet that unlocks day by day:
- Capybara Go's 7-Day Carnival: 4 tasks a day for 7 days, 5 points each and 15 on day 7, 180 in total. Every milestone chest needs 150, all inside a 14-day window.
- Legend of Slime's 7-day "Slime Diary", which ends in a guaranteed legendary box.
- MapleStory Idle's 14-day special mission.

Our starter attendance (Q-03) rewards logging in, and the guide quest is a single line. Nothing turns the first week into daily goals. The item is in neither the backlog nor v1. It is called '첫 주' so it does not clash with '새싹 출석'.

**Rules.**
- **Start:** when Missions (1-7) unlocks, and only if `InstallDays ≤ 7` at that moment. Otherwise set `declined = true`, so v3 saves past day 7 never see it. `startDayKey` = that DayKey.
- **Schedule:** day d's 4 tasks (Appendix C.2) appear at DayKey index d−1 after the start. Everything stays claimable through index 13, which is 14 DayKeys.
- **End:** after that window the sheet is `finished`. Reached tasks and chests that were not claimed are granted automatically through MetaRewards, because mail cannot carry wallet items. Toast: '첫 주의 남은 선물을 받았어요'.
- **Completion:** a task completes the first time its condition is seen true, and stays completed (latched in `done`).
  - It is checked on `Progress.Changed`, batched once per frame.
  - Conditions are lifetime counters, state conditions or the daily `JournalPoints`, so earlier play counts (research A5).
- **Locked features:** a task whose feature is locked shows the M39 lock pill and stays claimable later in the window. This differs from Critic-A4, which hides locked daily missions, because this window lasts 14 days.
- **Points:** 5 per task on days 1–6 and 15 per task on day 7, 180 in total.
- **Chests:** at 30 / 60 / 90 / 120 / 150 points. That leaves 30 points (17%) of slack, the Capybara ratio.
- **Tamper:** claims are blocked while the clock is tampered.

**UI (P4-6, JournalPopup).** While the sheet is active, a fourth tab '첫 주' is inserted first (4 × 216). The journal opens on it when it has a claimable item. Its badge is `_tabBadges[3]`.
- **Header:** '새내기 수선공의 첫 주' (H2 32) + a 'D-{n}' pill (PillNight 64×26, Micro 18 Gold).
- **Bar:** `UiArt.Bar` 640×20 Lavender with 5 `MissionChestView` at 30 / 60 / 90 / 120 / 150 (the existing journal chest component).
- **Day chips:** 7 × 112×64 in M53 Option style, '1일'…'7일'. A locked day shows a 24 px lock and a tooltip on tap: '{d}일째에 열려요'. A day with something claimable gets a dot.
- **List:** `MissionRowView` via `JournalRowModel` (§6.1): icon, text, progress, '+5' points pill, and [받기] / [가기 ›] / [완료].
- **Footer:** the existing `ClaimAllButton` claims tasks and chests across all open days.

**API (`StarterWeekSystem : IMetaModule`).** `bool IsActive`, `int DayIndex`, `int DaysLeft`, `int Points`, `void GetTasks(int day, List<StarterTaskStatus> into)`, `List<MetaReward> TryClaimTask(string id)`, `List<MetaReward> TryClaimChest(int index)`, `List<MetaReward> ClaimAll()`, `bool HasClaimable`, `event Action Changed`. Red dot: `JournalStarter`.

**Economy.** 300 별조각 one-time, plus tickets, shards and materials (Appendix C.2 and the §11.2 row).

**Tests (`StarterWeekTests`):**
- it starts only when the install day is ≤ 7;
- day gating at 05:00;
- a latched daily-condition task survives the daily reset;
- points and chest thresholds;
- auto-grant at the end of the window;
- tamper;
- a locked-feature task becomes claimable later;
- a v3 save past day 7 → declined.

### 13.7 NEW-02 오늘의 작은 선물 (free daily shop gift) (P1, S; P4-2 system, P4-4 card, P4-8 content)
**Why.** A free daily shop item is standard in the genre; Legend of Slime, for example, gives 200 gems a day free in its shop. Our F-04 red-dot tree reserved `tab.shop.free` for exactly this ("무료 일일 상품"), but nothing fills it: `RedDotSystem.cs:52` declares the path and nothing registers it. The gift gives the 상점 tab a daily reason to open, without ads or IAP.

**Rules.**
- Needs Shop (1-8).
- Once per DayKey. The reward is `dailyGifts[weekday]` (Appendix C.3; weekday from the DayKey, Monday = 0).
- Blocked while the clock is tampered.
- PlayLog event: `daily_gift`.

**UI (P4-4).** `DailyGiftCardView`, 860×120, sits at the top of the 상점 tab's normal page; the product grid moves down 132.
- **Contents:** `UI/journal_chest_closed` 88 + '오늘의 작은 선물' (Title 28 Cream) + a reward chip (`MetaRewardChip` 64) + [받기] Gold 180×80.
- **After the claim:** the chest shows its open sprite and the line '내일 새벽 5시에 새 선물이 와요' with a countdown (`UIFormat.ClockMono` under 1 h). The button hides.
- The claim flies the reward (`MetaUi.FlyRewards`). Red dot: `tab.shop.free`.

**API (`DailyGiftSystem : IMetaModule`).** `bool IsUnlocked`, `bool CanClaim`, `IReadOnlyList<MetaReward> Today`, `TimeSpan TimeUntilNext`, `List<MetaReward> TryClaim()`, `event Action Changed`.

**Tests (`DailyGiftTests`).** Once per DayKey; the 04:59/05:00 boundary; weekday rotation; locked before 1-8; tamper.

### 13.8 NEW-03 길 잃은 반딧불 (stray firefly) (P2 stretch, S; P4-2 rules, P4-3b view, P4-8 content)
**Why.** A tappable random visitor rewards players who are watching without punishing idle ones. Proven examples: Legend of Slime's flying reward boxes, The Tower's floating gems, Cookie Clicker's golden cookie. The field already has firefly art (`StageAmbienceView`), and a lost firefly fits the lost-things story. There are no ads, so the reward stays small and capped.

**Rules (`FireflyRules`, pure).**
- **When:** after `ftue.done`, during a Stage battle in the foreground. Not while asleep, not with a popup open, not while a boss is alive, not while aiming a summon.
- **Spawn timing:** after 360–720 s of such time, drawn uniformly and seeded by DayKey + count.
- **Cap:** at most 6 catches per DayKey.
- **Lifetime:** a firefly lives 8 s, then flies off. There is no penalty and no re-queue.
- **Reward roll** (deterministic, Appendix C.4): 단추 10분치 w55 · 1 material from the highest stage's lost-chest row (B.3) w35 · 별조각 10 w10.

**View (`StrayFireflyView`, P4-3b).**
- **Look:** the 56 px firefly sprite tinted Gold, with a 120 px `glow_soft` halo pulsing every 1.2 s. On Blackout stages it adds a 2 px Cream ring so it stands apart from the ambience fireflies.
- **Path:** it enters from a random side inside the band at StageArea local y −250 to −400 (screen ≈ 440–590) and drifts on a lazy sine (amplitude 40 px, 140 px/s). The IA keep-out map (`ia/keepout_out.txt`) shows battle content only below y ≈ 586 at the edges, but from y ≈ 444 in the middle columns, so the firefly may pass over enemies there. That is harmless: it never covers a guardian pad hit area, and it fades out (raycast off) while a pet tap or a summon aim is in progress.
- **Input:** a 120×120 hit pad. Raycast is on for that image only, so it never blocks a drag.
- **Tap:** 8 cream sparkles + `firefly_catch` + Light haptic + the reward flies. The very first catch also shows the toast '길 잃은 반딧불이 선물을 두고 갔어요'.
- **Reduced motion:** a straight drift.

**Tests (`FireflyRulesTests`).** Cap of 6 per day with reset at 05:00; deterministic roll; blocked in FTUE, while asleep, during a boss and with a popup open; tamper → it still animates but gives no reward.

### 13.9 NEW-04 입장권 모아 두기 (dungeon ticket bank) (P2 stretch, S; P4-0 save and hook, P4-2 logic, P4-4 text, P4-7 planner)
**Why.** MapleStory Idle stores unused growth-dungeon keys up to 10, and Capybara Go keeps overflow energy for days. Missing one day should not throw away that day's tickets; this is the "쉬어도 괜찮아" principle from the phase-4 research sweep. Today `DungeonSystem` resets tickets every DayKey. A failed run already keeps its ticket, because `Challenge` spends one only on a clear.

**Rules.**
- Active when `TicketBankCap` > 0 (content: 9).
- On the first access each DayKey: `bank = min(cap, bank + 3 × DaysBetween(bankDayKey, today))`.
- Migration: `ticketBank = -1` means "not yet migrated"; it becomes today's `TicketsLeft`.
- The Wednesday event adds +2 once; on that day only the cap is 11.
- `TicketsLeft` = bank. `SweepMany` can spend all of it.

**Knock-on changes.**
- Red dot `dungeon.tickets`: on when bank ≥ 3 (was: first visit of the day).
- NOTI-01 DungeonTickets (P4-7): fires at the next reset only if `bank + 3 ≥ cap`, with '잔향이 던전 입장권이 가득 찼어요'.
- DungeonCardView (P4-4): '입장권 {n}/9' and '새벽 5시에 +3장'.

**Tests (`DungeonTicketBankTests`).** Refill per skipped day; the cap; the Wednesday bonus once; migration; a failed run keeps its ticket.

### 13.10 Researched systems not built in phase 4 (phase-5 candidates) and the ones rejected
Sources:
- the phase-4 sweeps `research2/kr_kiugi_notes.md` and `research2/global-idle-notes.md`. They were written after v1's parts and never merged into v1.
- the critic's own lookups (Lucky Defense meta).

Each candidate is proven in at least two titles and fits the storybook tone. Each is too large for 4.1 or overlaps a 4.1 system.

| Candidate | Proven in | Our version | Size | Why not now · revisit when |
|---|---|---|---|---|
| 주간 큰 잠꾸러기 (weekly damage-race boss) | AFK Journey Dream Realm, MapleStory Idle world boss, Legend of Slime Dwarf King, Legend of Mushroom Abyssal Portal | a sleepy giant the friends try to wake each week; reward tiers by best damage (local, no ranking) | M–L (new battle mode) | needs a boss-only sim mode and new art · revisit when CR-09 shows the median player at chapter 5+ |
| 밤의 작업대 (timed research tree) | Legend of Slime research, Legend of Mushroom tech park, The Tower labs, Cookie Run: Crumble gnome research | permanent account bonuses researched over real hours with materials | L | a new long-term sink and yet another timer next to the expedition ladder · revisit when materials pile up (CR-09 inventory snapshots) |
| 밤 산책 (roguelite side run, 1-of-3 picks) | Capybara Go core loop, AFK Journey Arcane Labyrinth, Idle Heroes Aspen Dungeon | a weekly night-walk run with 1-of-3 blessings | XL | PRG-07 blessings test the pick feel first · revisit after PRG-07 ships |
| 달빛 화분 (plant/harvest timer) | Capybara Go Immortal Peach Garden, NGU Idle Yggdrasil, Cookie Clicker sugar lumps | a moonlight pot that ripens while you are away | M | the expedition ladder already adds a come-back-later timer, and Stone Age Idle reviews warn against splitting growth across too many currencies · revisit if D7 data calls for a second timer |
| 편성 프리셋 (formation presets) | Seven Knights Idle, Legend of Mushroom skill presets, MapleStory Idle | 2–3 saved formations (farm / boss / dungeon) | S–M | 7 lineages × 1 guardian + 1 echo form leave few combinations · revisit when the roster passes 10 lineages |
| 유품 (relics with passives) | MapleStory Idle relics, Legend of Slime relics, Lucky Defense artifacts | small keepsakes from chapter chests, one passive each | M | overlaps the codex collection bonuses (Q-05) and adds a currency · revisit after Q-05 is tuned |

Rejected on purpose:
- **Summon level** (rates rise with pull count; MapleStory Idle, Legend of Slime): research §17 rejects it, because a rate table that changes costs trust. PRG-04 milestones replace it.
- **Equipment and gear gacha** (Legend of Mushroom's lamp, Lucky Defense treasures): deferred under CR-11.
- **Prestige reset:** rejected in research §17.
- **Guilds, rankings, PvP, co-op, server-wide bosses:** there is no server.
- **Ad boosts** (Legend of Slime's 20-minute ad blessings and ad pulls): there are no ads. Their free equivalents here are the cheer (§13.2), the daily gift (§13.7) and the fireflies (§13.8).
- **A free daily summon:** about +700 gem-equivalent a week (+27% on §11.2), so `tab.gacha.free` stays reserved.
- **Lucky Defense's random-merge loop** (random summons, three of a unit merge into a higher grade, mythic recipes): an in-match loop built for a different core game. Its transferable meta parts already exist here: the recipe guide maps to 진화 기억도 plus the PRG-03 "one member short" row, and duplicate shards map to 기억편.

---

## Appendix A: content copy (P4-8; 해요체, reviewed for '습니다'/'개방')

### A.1 수선점 기록 (17 lines · 62 tiers · 3,400 별조각)
| lineId · category · icon | Condition | Tiers: target → 별조각 | Title |
|---|---|---|---|
| `ach_stage` · Stage · `Icons/quest` | `StageCleared` | 1-10 → 20 · 2-10 → 40 · 3-10 → 60 · 5-10 → 80 · 10-10 → 100 | '스테이지 {x-10} 클리어' |
| `ach_kill` · Kill · `Enemies/enemy_dust` | `Counter EnemyKill` | 100 → 10 · 1,000 → 30 · 10,000 → 60 · 100,000 → 100 | '적 {n}마리 물리치기' |
| `ach_lamp` · Lamp · `Icons/lamp` | `Counter LampLit` | 10 → 20 · 100 → 50 · 1,000 → 100 | '불 밝히기 {n}번' |
| `ach_echo` · Echo · `Icons/echo` | `Counter EchoSummon` | 100 → 20 · 1,000 → 50 · 10,000 → 100 | '잔향이 {n}번 부르기' |
| `ach_friend` · Friend · `Icons/guardian` | `OwnedLineages` | 4 → 10 · 5 → 30 · 6 → 60 · 7 → 100 | '친구 {n}명 만나기' |
| `ach_form` · Form · `Icons/shards` | `UnlockedForms` | 5 → 20 · 10 → 40 · 20 → 70 · 29 → 100 | '형태 {n}종 해금' |
| `ach_expedition` · Expedition · `UI/tab_expedition` | `Counter ExpeditionClaim` | 10 → 20 · 50 → 50 · 200 → 100 | '원정 {n}번 다녀오기' |
| `ach_dungeon` · Dungeon · `Icons/echo` | `DungeonFloor` | 5 → 20 · 10 → 40 · 20 → 70 · 30 → 100 | '잔향이 던전 {n}층 돌파' |
| `ach_gacha` · Gacha · `UI/tab_gacha` | `Counter GachaPull` | 10 → 10 · 50 → 30 · 100 → 60 · 300 → 100 | '잃어버린 친구 {n}번 찾기' |
| `ach_synergy` · Synergy · `Icons/resonance` | `SynergyKindsSeen` | 5 → 10 · 10 → 30 · 20 → 70 · 25 → 100 | '시너지 {n}종 켜 보기' |
| `ach_shop` · ShopLevel · `UI/tab_growth` | `ShopLevel` | 10 → 10 · 50 → 30 · 100 → 60 · 200 → 100 | '수선점 Lv.{n}' |
| `ach_levelsum` · LevelSum · `Icons/star` | `LineageLevelSum` | 50 → 20 · 200 → 50 · 500 → 100 | '친구 레벨 합계 {n}' |
| `ach_journal` · Journal · `Icons/journal` | `Counter JournalComplete` | 7 → 20 · 30 → 50 · 100 → 100 | '수선 일지 100점 {n}일' |
| `ach_attend` · Attendance · `Icons/attendance` | `Counter AttendanceCheckIn` | 7 → 20 · 28 → 50 · 100 → 100 | '출석 도장 {n}일' |
| `ach_costume` · Costume · `Costumes/jjak_cheer` | `CostumesOwned` | 3 → 20 · 6 → 50 · 10 → 100 | '의상 {n}벌 모으기' |
| `ach_affinity` · Affinity · `Icons/affinity` | `AffinityLevelCount "10"` | 1 → 30 · 3 → 60 · 7 → 100 | '호감도 Lv.10 친구 {n}명' |
| `ach_hidden` · Hidden · `UI/medal_hidden` | `Flag` (§6.1) | 5 × 50 | see below |
Hidden (title before → after · hint):
- h1 '???' → '비 오는 날의 쓸모' · '비 오는 날, 누군가 쓸모를 찾을지도 몰라요'
- h2 → '무사한 밤' · '보스가 문 앞까지 오지 못하게 지켜 봐요'
- h3 → '불빛 세 번' · '한 판에 불을 여러 번 밝혀 볼까요?'
- h4 → '한 짝을 찾아서' · '짝짝이의 모든 모습을 만나 봐요'
- h5 → '늦은 밤의 손님' · '모두가 잠든 시간에 들러 볼까요?'
Done descriptions (shown after reveal):
- h1 '비 오는 스테이지에서 우두커니가 지킨 채 클리어했어요'
- h2 '수선점이 한 번도 다치지 않고 챕터 보스를 물리쳤어요'
- h3 '한 스테이지에서 불을 세 번 밝혔어요'
- h4 '짝짝이의 형태 11종을 모두 해금했어요'
- h5 '자정이 넘은 밤에 출석 도장을 찍었어요'

### A.2 도감 texts
- **Hints:**
  - unowned lineage '잃어버린 친구 찾기에서 만날 수 있어요'
  - unfound form '진화 기억도에서 단서를 찾아요'
  - costume = its `CostumeDef` unlock text
  - enemy '스테이지 {표기}부터 나타나요'
  - synergy '{구성원1}·{구성원2} 함께 세우면 켜져요' (traits/roles: '{특성} 친구 {n}명이 함께 서면 켜져요')
  - material = first source label ('원정 1-4에서 모아요')
  - story '{n}장 보스를 물리치면 열려요'
- **Enemy texts:**
  - 먼지뭉치 '잊힌 방구석에서 뭉친 먼지예요. 수가 많지만 약해요.'
  - 좀나방 '천을 갉아 먹어요. 천으로 된 친구가 맞으면 아파요.'
  - 엉킨 실뭉치 '쓰러지면 작은 실뭉치 둘로 갈라져요.'
  - 녹얼룩 '느리지만 단단해요. 맞으면 녹이 묻어 약해져요.'
  - 골무 껍데기 '보호막을 두르고 와요. 저격으로 먼저 깨 주세요.'
  - 망각의 그림자 '보이지 않다가 빛에 드러나요.'
  - 태엽쥐 '뒤에서 다른 적의 태엽을 감아 빠르게 만들어요.'
  - 망각의 큰그림자 '화가 나면 빨라지고 불빛을 빼앗아요.'
  - 좀나방 여왕 '좀나방을 불러내요. 화가 나면 더 사나워져요.'
- **Bonus tiers:** forms 5/10/15/20/25/29 → +1/+3/+5/+7/+8/+10% (ATK and HP); 9/9 enemies → 수선점 내구도 +5%; synergy kinds 10/20/25 → 불빛 충전 +5/+10/+15%.

### A.3 호감도 기억 조각 (Lv 2/4/6/8/10)
| Lineage | Lv2 | Lv4 | Lv6 | Lv8 | Lv10 |
|---|---|---|---|---|---|
| 짝짝이 | 세탁기 속에서 짝을 놓친 날을 아직 기억해요. | 그 뒤로 만나는 양말마다 '가족'이라고 불렀대요. | 수선점 바구니에서 처음으로 푹 잠들었어요. | 잃어버린 짝도 누군가의 가족이 되었길 바라요. | 이제 짝짝이에게 한 짝은 외로운 숫자가 아니에요. |
| 똑단이 | 외투에서 떨어지던 날, 끝까지 실 한 올을 붙잡았어요. | 그래서 지금도 무엇이든 꼭 붙잡고 놓지 않아요. | 수선점 문고리를 붙잡고 밤새 문을 지킨 적도 있어요. | 붙잡는 건 놓치기 싫은 마음이라는 걸 알게 됐어요. | 이제는 친구들 손을 꼭 붙잡는 단추가 되었어요. |
| 찰칵이 | 맞는 문을 찾아 골목의 문을 전부 두드려 봤어요. | 어느 문도 열리지 않았지만 포기하지 않았어요. | 수선점 문은 두드리기도 전에 먼저 열렸어요. | 맞는 자물쇠보다 반겨 주는 문이 더 좋다는 걸 알았어요. | 찰칵이는 이제 친구들의 마음을 여는 열쇠예요. |
| 몽당이 | 생일 케이크 위에서 마지막까지 타오른 초였어요. | 녹을수록 작아졌지만 빛은 줄지 않았어요. | 어두운 정류장에서 길 잃은 친구를 밝혀 준 적이 있어요. | 작아지는 게 무섭지 않다고, 조용히 웃어요. | 몽당이의 불씨는 이제 수선점 모두의 등불이에요. |
| 우두커니 | 주인이 '금방 올게' 하고 두고 간 우산이었어요. | 맑은 날이면 자기가 쓸모없다고 시무룩해했어요. | 비 오는 밤, 친구들을 지키며 처음 웃었어요. | 기다림도 누군가를 지키는 일이라는 걸 배웠어요. | 이제 우두커니는 맑은 날에도 수선점을 지켜요. |
| 루즈리 | 서랍 속에 잊힌 채 오래 뚜껑을 닫고 있었어요. | 누군가 돌려 올려 주자 처음으로 당당해졌어요. | 친구들 볼에 하트를 그려 주는 게 취미가 됐어요. | 수줍은 날도, 당당한 날도 모두 자기 모습이래요. | 루즈리의 색은 이제 수선점의 웃음이에요. |
| 라라벨 | 마지막 음을 치면 노래가 끝날까 봐 무서웠어요. | 그래서 늘 끝 음 앞에서 멈췄대요. | 친구들이 다음 노래를 이어 주겠다고 약속했어요. | 처음으로 마지막 음을 아주 작게 쳐 봤어요. | 끝은 또 다른 노래의 시작이라는 걸 알았어요. |

### A.4 쓰다듬기 말풍선 (7 per lineage)
- **짝짝이**: 오늘도 가족이 늘었어! / 내 짝은 어디 있을까? / 간지러워, 히히! / 수선점은 내가 지킬게! / 한 짝이어도 괜찮아. / 실밥이 풀렸나? 고마워! / 다 같이 있으니 좋다~
- **똑단이**: 꼭 붙잡고 있을게! / 놓치지 않아! / 단단히 매달렸지? / 여기가 내 자리야! / 떨어지면 안 돼… / 딸깍! 고마워. / 꽉 잡아 줄게!
- **찰칵이**: 이 문은 맞을까? / 찰칵! 아니네… / 언젠가 맞는 문이 있겠지? / 똑똑, 누구세요? / 어디든 열어 줄게! / 딱 맞는 짝을 찾는 중! / 열쇠 구멍 같은 눈이네?
- **몽당이**: 조금 작아졌지만 괜찮아. / 끝까지 밝혀 줄게. / 따뜻하지? / 불씨 하나 나눠 줄까? / 녹아도 무섭지 않아. / 어둠은 내가 맡을게! / 호~ 불지는 마!
- **우두커니**: 비가 오면 좋겠다… / 오늘은 맑네. 시무룩… / 기다리는 건 자신 있어. / 우산 아래로 와! / 쓸모 있는 날이 올 거야. / 빗소리가 그리워. / 주인은 언제 올까?
- **루즈리**: 꺄, 부끄러워! / 돌려 올리면 당당해져! / 오늘 색 어때? / 쪽! 고마워. / 뚜껑은 닫지 마~ / 반짝반짝 빛나 볼까? / 내가 제일 예쁘지?
- **라라벨**: 마지막 음은… 아직이야. / 도, 레, 미… 쉿. / 자장가 들려줄까? / 태엽을 감아 줄래? / 노래가 끝나는 게 무서워. / 함께라면 끝까지 칠 수 있을까? / 띵~ 고마워.

### A.5 동화책 (5 chapters × 3 panels; each line ≤40 chars)
| Ch | Title · place | Panel 1 | Panel 2 | Panel 3 |
|---|---|---|---|---|
| 1 | 불이 켜진 골목 · 잃어버린 골목 | 잊힌 물건들이 굴러드는 골목 끝, 작은 수선점에 불이 켜졌어요. | 짝을 잃은 양말이 문을 두드렸어요. "여기, 가족이 있나요?" | 큰그림자가 물러간 밤, 골목의 가로등이 하나둘 다시 켜졌어요. |
| 2 | 비 오는 광장의 우산 · 비 오는 광장 | 비 오는 광장 한가운데, 펼쳐진 우산 하나가 주인을 기다렸어요. | "비가 그치면 나는 쓸모없어질까?" 우산이 작게 중얼거렸어요. | 수선점 식구들이 우산 아래 모여 앉자, 빗소리가 노래처럼 들렸어요. |
| 3 | 마지막 음 · 먼지 쌓인 다락방 | 다락방 상자 속에서 마지막 음이 고장 난 오르골이 깨어났어요. | 태엽을 감을 때마다 오르골은 끝 음 앞에서 멈칫했어요. | "끝나도 괜찮아. 우리가 다음 노래를 이어 줄게." |
| 4 | 서랍 속 색 · 분실물 보관소 | 선반마다 이름표 없는 물건들이 빼곡한 분실물 보관소예요. | 서랍 속 립스틱이 뚜껑을 살짝 열고 바깥을 엿보았어요. | 돌려 올린 립스틱이 반짝 웃자, 보관소에 다정한 색이 번졌어요. |
| 5 | 별빛 세탁소 · 별빛 세탁소 | 밤에만 문을 여는 세탁소에서 별빛에 마른 천 냄새가 났어요. | 몽당초는 녹아 작아져도 세탁소 구석을 끝까지 밝혔어요. | 수선점에 돌아온 친구들이 말했어요. "잊혀도, 우리는 여기 있어." |
Art prompts follow the panel text. Each chapter sheet uses that chapter's featured friend art as a reference (ch1 jjak_base, ch2 udu_base, ch3 lala_base, ch4 rouge_base, ch5 mong_base).

### A.6 패배 분석 조언 (by top leaking enemy)
- enemy_moth '빠른 적이에요. 그 방향에 감속(방어) 지킴이를 세워요'
- enemy_thimble '보호막 적이에요. 저격이나 표식으로 먼저 깨요'
- enemy_tangle '갈라지는 적이에요. 돌격(범위) 친구가 좋아요'
- enemy_wisp '숨는 적이에요. 빛 특성(몽당이·라라벨)이 드러내요'
- enemy_windup '뒤쪽 태엽쥐를 저격으로 먼저 노려요'
- enemy_rust '녹을 묻혀요. 힐이나 정화 친구가 좋아요'
- enemy_dust '수가 많아요. 범위 공격과 불 밝히기로 막아요'
- boss_* '보스가 문을 두드려요. 불 밝히기를 보스까지 아껴 보세요'

### A.7 이벤트 (names, effect chips, starlit-walk missions and stamps)
- **Effect chips:**
  - 주말 원정 축제 → '원정 재료 ×2'
  - 수요 던전 데이 → '던전 입장권 +2'
  - 별이 머문 밤 산책 → '별빛 사탕 ×2'
  - 밤의 수선점 → '밤 9시~새벽 2시 바구니 +20%'
- **Short descriptions:**
  - '토·일요일에는 원정에서 재료를 두 배로 가져와요'
  - '수요일에는 잔향이 던전 입장권이 2장 더 생겨요'
  - '매달 1일부터 14일까지, 별빛 사탕이 두 배로 쏟아져요'
  - '밤에는 바구니에 선물이 조금 더 쌓여요'
- **Starlit-walk missions:**
  - `sw_stage` '스테이지 50번 클리어' StageClear 50 → 별빛 사탕 60
  - `sw_lamp` '불 밝히기 30번' LampLit 30 → 사탕 40
  - `sw_expedition` '원정 6번 보내기' ExpeditionStart 6 → 사탕 40
  - `sw_dungeon` '잔향이 던전 3번' DungeonRun 3 → 사탕 40
  - `sw_gacha` '잃어버린 친구 찾기 5번' GachaPull 5 → 사탕 50
  - `sw_basket` '수선 바구니 5번 받기' ChestClaim 5 → 사탕 40
  - `sw_bonus` (isBonus) '미션 6개 모두 받기' → 별조각 100 + 사탕 100
- **Stamps:** 1 사탕 30 · 2 빠른 수선권 1 · 3 사탕 50 · 4 리본 조각 15 · 5 사탕 70 · 6 두 배 수선권 1 · 7★ 별조각 150 + 사탕 100.

### A.8 별빛 산책 수첩 (30 tiers, 90 xp each; ★ = highlight)
1 별조각 30 · 2 수선 천 10 · 3 빠른 수선권 1 · 4 별조각 30 · 5★ 선택 기억편 10 · 6 리본 조각 10 · 7 별조각 30 · 8 촛농 방울 10 · 9 두 배 수선권 1 · 10★ 별조각 80 · 11 레이스 조각 5 · 12 별조각 30 · 13 빠른 수선권 1 · 14 기억의 실 3 · 15★ 선택 기억편 15 · 16 별조각 30 · 17 우산살 5 · 18 두 배 수선권 1 · 19 별조각 30 · 20★ 달빛 조각 1 · 21 수선 천 20 · 22 별조각 40 · 23 빠른 수선권 2 · 24 기억의 실 5 · 25★ 선택 기억편 20 · 26 별조각 40 · 27 오르골 태엽 2 · 28 별조각 50 · 29 별빛 사탕 100 · 30★ 별빛 산책 짝짝이 `jjak_starwalk` (season-exclusive; owned or art missing → 별조각 50 + 선택 기억편 30; critic C-06).

---

## Appendix B: numbers

### B.1 Gacha milestones: §6.4 table (10/30/50/100/200, then +100 → 별조각 300).
### B.2 Chapter chests (first clear of x-10)
- ch1: 별조각 100 · 수선 천 10 · 리본 조각 10 · 기억의 실 2
- ch2: 별조각 100 · 우산살 6 · 레이스 조각 6 · 기억의 실 3
- ch3: 별조각 120 · 오르골 태엽 2 · 기억의 실 4 · 달빛 조각 1
- ch4: 별조각 120 · 기억의 실 5 · 레이스 조각 10 · 달빛 조각 1
- ch5: 별조각 150 · 오르골 태엽 3 · 기억의 실 6 · 달빛 조각 1
- default (ch6+): 별조각 150 · 기억의 실 6 · 오르골 태엽 2 · 달빛 조각 1

### B.3 Lost chest tables (drop chance 0.6 per normal clear after FTUE)
| Stages | Entries (count, weight) | E[common items]/clear |
|---|---|---|
| 1–10 | 수선 천 1–2 w40 · 리본 조각 1–2 w35 · 촛농 방울 1 w15 · 레이스 조각 1 w10 | 0.83 (today 0.78) |
| 11–20 | 수선 천 1–2 w30 · 리본 조각 1–2 w20 · 촛농 방울 1–2 w15 · 레이스 조각 1 w15 · 우산살 1 w15 · 기억의 실 1 w5 | ≈0.80 + rares |
| 21+ | 수선 천 2–3 w25 · 레이스 조각 1–2 w20 · 우산살 1–2 w20 · 촛농 방울 1–2 w15 · 기억의 실 1 w15 · 오르골 태엽 1 w5 | ≈0.95 + rares |

### B.4 잔향 축복 pool (max 2 stacks each; icon = `Icons/{key}`)
| id | Name | Stat · value | Desc | Icon |
|---|---|---|---|---|
| b_shield | 보호막 실 | ShieldPowerPercent +20 | 모든 잔향이 보호막 +20% | skill_shield_bubble |
| b_attack | 반짝이는 바늘 | AttackPercent +15 | 잔향이 공격력 +15% | skill_thread_stitch |
| b_regen | 잔향 샘 | ResonanceRegenPercent +20 | 잔향 회복 +20% | skill_moon_glow |
| b_cost | 가벼운 발걸음 | EchoCostReductionFlat +1 | 잔향이 소환 비용 −1 | skill_summon_circle |
| b_life | 오래가는 빛 | EchoLifetimePercent +25 | 잔향이 머무는 시간 +25% | skill_reveal_light |
| b_crit | 별빛 눈 | CritChancePercent +8 | 치명타 확률 +8% | skill_star_burst |
| b_boss | 큰그림자 사냥 | BossDamagePercent +20 | 보스 피해 +20% | skill_shadow_burst |
| b_start | 아침 잔향 | StartResonanceFlat +15 | 시작 잔향 +15 | skill_sound_ring |
| b_speed | 바람 발 | EchoMoveSpeedPercent +15 | 잔향이 이동 속도 +15% | skill_storm_swirl |
| b_heal | 포근한 손길 | HealPowerPercent +25 | 회복량 +25% | skill_heart_heal |
| b_stun | 꼭 붙잡기 | StunDurationPercent +25 | 기절 시간 +25% | skill_stun_stars |
| b_slow | 엉킨 실 | SlowPowerPercent +20 | 감속 효과 +20% | skill_slow_yarn |

### B.5 Comeback stamps: day 1 별조각 300 + 빠른 수선권 2 · day 2 두 배 수선권 2 + 수선 천 10 · day 3 선택 기억편 20 (§8.2).

---

## Appendix C: critic content (P4-8; 해요체, checked for '습니다' and '개방')

### C.1 Boss hints (§13.1, `BossHintDef`)
- `boss_shadow` 망각의 큰그림자 (Enrage + LampDrain): '화가 나면 빨라지고 불빛을 빼앗아요. 불 밝히기는 분노하기 전에 써 봐요'
- `boss_mothqueen` 좀나방 여왕 (Enrage + FabricEater + summons): '좀나방을 불러내요. 범위 공격 친구와 방어 지킴이가 막아 줘요'

### C.2 첫 주 tasks and chests (§13.6)
| Day | Tasks: text (`condition`) |
|---|---|
| 1 (5 pts each) | 스테이지 1-10 클리어 (`StageCleared 10`) · 잔향이 50번 부르기 (`Counter EchoSummon 50`) · 수선점 Lv.20 (`ShopLevel 20`) · 오늘 수선 일지 40점 (`JournalPoints 40`, today, latched) |
| 2 | 잃어버린 친구 찾기 10번 (`Counter GachaPull 10`) · 원정 3번 보내기 (`Counter ExpeditionStart 3`) · 친구 레벨 합계 60 (`LineageLevelSum 60`) · 수선 바구니 3번 받기 (`Counter ChestClaim 3`) |
| 3 | 스테이지 2-5 클리어 (`StageCleared 15`) · 잔향이 던전 3층 돌파 (`DungeonFloor 3`) · 불 밝히기 30번 (`Counter LampLit 30`) · 빠른 수선 3번 (`Counter QuickIdle 3`) |
| 4 | 진화 1번 만들기 (`Counter EvolutionCraft 1`) · 시너지 3개 켜기 (`ActiveSynergies 3`) · 상점에서 2번 사기 (`Counter ShopBuy 2`) · 수선점 Lv.40 (`ShopLevel 40`) |
| 5 | 스테이지 2-10 클리어 (`StageCleared 20`) · 원정 10번 받기 (`Counter ExpeditionClaim 10`) · 적 3,000마리 물리치기 (`Counter EnemyKill 3000`) · 출석 도장 5일 (`Counter AttendanceCheckIn 5`) |
| 6 | 계보 공명 1번 (`Counter ResonanceUp 1`) · 의상 1번 입히기 (`Counter CostumeEquip 1`) · 친구 레벨 합계 120 (`LineageLevelSum 120`) · 잔향이 던전 5층 돌파 (`DungeonFloor 5`) |
| 7 (15 pts each) | 스테이지 3-5 클리어 (`StageCleared 25`) · 수선 일지 100점 3일 (`Counter JournalComplete 3`) · 잃어버린 친구 찾기 30번 (`Counter GachaPull 30`) · 형태 10종 해금 (`UnlockedForms 10`) |

Chests: 30 → 별조각 50 + 빠른 수선권 1 · 60 → 리본 조각 15 + 수선 천 15 · 90 → 별조각 100 + 기억의 실 3 · 120 → 선택 기억편 20 + 두 배 수선권 1 · 150★ → 별조각 150 + 달빛 조각 2 + 선택 기억편 20. Total: **별조각 300**.

Copy:
- Tab: '첫 주'. Header: '새내기 수선공의 첫 주'. Sub line: '일주일 동안 매일 새 할 일이 열려요'.
- Locked-day tooltip: '{d}일째에 열려요'.
- Start toast (between stages, once): '수선 일지에 「첫 주」가 열렸어요'.
- Window-end toast: '첫 주의 남은 선물을 받았어요'.

### C.3 오늘의 작은 선물 rotation (§13.7; weekday from the DayKey, Monday = 0)
월 수선 천 5 · 화 리본 조각 5 · 수 빠른 수선권 1 · 목 촛농 방울 3 · 금 별조각 20 · 토 레이스 조각 3 · 일 두 배 수선권 1.
Copy: '오늘의 작은 선물' / after the claim: '내일 새벽 5시에 새 선물이 와요'.

### C.4 길 잃은 반딧불 (§13.8)
Weights: `GoldMinutes 10` w55 · lost-chest material ×1 (B.3 row of the highest stage) w35 · 별조각 10 w10. First-catch toast: '길 잃은 반딧불이 선물을 두고 갔어요'.

### C.5 강해지기 rows and cheer (§13.2; particles via the existing particle helper)
- ShopUpgrade: '수선점 {n}번 강화' / '전투력 +{gain:N0}'
- LineageLevel: '{이름} 레벨 +{n}' / '전투력 +{gain:N0}'
- FillSlot: '빈 {지킴이|잔향이} 자리 채우기' / '{이름}{을/를} 세우면 전투력 +{gain:N0}'
- SynergyOneShort: '{시너지} 한 명만 더' / '{이름}{을/를} 세우면 켜져요 · 전투력 +{gain:N0}'
- ResonanceReady: '{이름} 공명을 올릴 수 있어요' / '전투력 +{gain:N0}'
- AutoFormation: '자동 편성으로 바꾸기' / '지금보다 {pct}% 강해요 · 전투력 +{gain:N0}'
- EvolutionReady: '{형태} 진화를 만들 수 있어요' / '재료가 모두 모였어요'
- MissingMaterial: '{재료}{이/가} 조금 모자라요' / '{출처}에서 모아요'
- WeakDirection: '약한 방향 보강 · {방향}' (§7.2)
- Empty state: '지금은 더 할 일이 없어요. 바구니와 원정으로 재료를 모아 봐요'
- Farm nudge (BossChallengeButton line 2): '강해지는 방법 보기 ›'
- Cheer: button '친구들의 응원 받기' / sub '10분 동안 공격력 +20% · 이 장에서 한 번' / used '다음 장에서 다시 받을 수 있어요' / chip tap '친구들이 응원하고 있어요! 공격력 +20%'

### C.6 Basket copy (§13.4)
- Cap note: '수선점 Lv.200이 되면 바구니가 16시간까지 늘어나요'
- Capped growth line: '다음 스테이지 클리어 시 재료 +2%'

---

## 14. Critic revisions (v1.1, 2026-11-04, completeness critic)

**Scope of the review.** The critic read SPEC4 v1, the orchestrator audit, `IDLE_RESEARCH.md` (§3, §17, §18, Critic additions A–E), UX-SPEC (§2.1 HUD map, §3.15, §4 waves and ownership, M11/M13/M43), `research2/{kr_kiugi,global-idle}-notes.md` and `ia/keepout_out.txt`, and checked the code read-only (paths under `Assets/_Project`). The audit JSON passed to the critic stopped after ATK-16, so the CR-xx statuses come from the v1 matrix and the code checks below. Web lookups (Lucky Defense meta) are listed at the end.

**Every change made to this file:**

| # | Change | Where | Evidence |
|---|---|---|---|
| C-01 | **Economy: the basket 별조각 line had no ceiling** → `maxStageMultiplier` 1.5 on that line only. A new stage-independent budget row, a revised projection (2,640/week, +45%), and risks #3 and #13 | §2.3, §11.2, §12, §13.4, C.6 | `MetaContent.IdleChest.cs` `stageGrowth = 1.02` for every line; `IdleChestSystem.PerHour`/`StageMultiplier` apply no cap; main stages are endless (`BattleConfig.TaperedScale`, no last stage). Rates: 3/h at 1-1, 7.9/h at 5-10, 21/h at 10-10 |
| C-02 | Open-item count corrected (34 → 48); 14 matrix rows added; the stretch list updated | header, §1 | the v1 text contains no PRG-02 or PRG-03 at all; the rest is listed in this table |
| C-03 | PRG-02 remainder: boss hint line, 50% enrage tick, the "WP-5 merged" precondition; the 900×28 bar is not built (conflict with UX M11) | §13.1, §2.11 #10, §2.12, C.1 | research PRG-02; UX §2.1.3/§2.1.5 (boss HP lives in the StagePanel); `BattleConfig` `enrageThreshold 0.5` and boss gimmicks; audit PRG-02 "LIVE RISK" |
| C-04 | **Chapter chest promoted from P2 stretch to P1 core**; the quest-22 content move is tied to it | §1, §2.11 #7, §8.5, §9.4 (P4-1, P4-6) | research PRG-02 (P0) "클리어하면 챕터 상자". In v1, cutting the stretch item would silently drop quest 22's chest content |
| C-05 | **CR-01 ring timing fixed** (it was visible for ≈0.3 s): `ExtendEndPause`/`SkipEndPause`, once per stage per 10 min, a '바로 다시 도전 ›' link | §7.2, §2.5, §2.11 #11, §11.4 #37, §12 #15 | `BattleConfig.stageEndPause = 1.5` (`Data/BattleConfig.asset:393`); `BattleSystem` `_endTimer` → `BeginPendingStage` |
| C-06 | **Q-06 tier 30 → the season-exclusive costume `jjak_starwalk`**; fallback cut from 300 to 50 + 선택 기억편 30; codex 11 costumes (2,615); art A14 takes A10's credit slot | §8.4, A.8, §6.2, §10.1, §11.2, §2.2 `CostumeUnlockType.Season` | `ShopDatabase.cs:84` `event_jjak_sleepcap` 150; `CharacterContent.Jjakjjak.cs:155` `Unlock.EventShop(150)`. With the v1 fallback, every later season would pay 690 against a budget of ≤450 |
| C-07 | PRG-03 remainder: `StrengthenAdvisor` (9 row kinds, scored by gain/cost), the ≥5% auto-formation gate, the 10-minute farm nudge, 친구들의 응원 + CheerChip | §13.2, §2.2–§2.5, C.5 | research PRG-03; UX M13 phase 2 has only 3 fixed rows; nothing in v1 |
| C-08 | FX-01 remainder: `BattleSystem.PresentationScale`, so the simulation follows the hit-stop | §13.3, §2.5 | `BattleFx.cs:591` (sim "그대로 흐른다"), `GameBootstrap.cs:130`, `BattleView.cs:79`; research FX-01 asks for a `BattleTimeScale` on `BattleSystem.Tick` |
| C-09 | FTUE-01 remainder: `BattleSetup.Protection`, `FillLamp`/`ForceLampFull`, the resolver wired by P4-0; FtueBattleGuard drops reflection; first-lamp beat (0.2× for 0.6 s); step-1 hit emphasis | §13.3, §2.5 | `FtueBattleGuard.cs:12-28` uses `GetField("_lamp", NonPublic)`; the `FtueAction` doc comment; research FTUE step 1 and step 4 |
| C-10 | Audio: added `boss_defeat` ☆, `death_spring`, `firefly_appear`, `firefly_catch`, `cheer_start` (minimal set 24, full set 70 clips) | §3.5 hook table, §10.2 | research AUD-01 "보스 처치" had no cue in v1; the audit's ATK-04 gap lists the windup '보잉' |
| C-11 | IDLE-01 16 h step at shop Lv.200 (`IdleCapStep.shopLevel`) plus copy | §13.4, §2.3, C.6 | `MetaContent.IdleChest.cs:10` "(P2: 수선점 Lv.200에 16시간 예정)"; `IdleCapStep` has only `stageCleared` |
| C-12 | F-04 leaves: NEW-02 registers `tab.shop.free`; `tab.gacha.free` stays reserved by decision | §2.7, §13.7, §13.10 | `RedDotSystem.cs:51-52` declares both and nothing registers them; research F-04 tree |
| C-13 | Small remainders (a)–(k): button art spin, echo streak, radial lamp shake, save-based lamp banner flag, danger arrows vs guide bar, CR-11 copy, FX-05 iOS backend, EVT-01 `Dates` reserve + night-visit line, ATK-09 runtime proof, ATK-12 clip proof | §13.5, §2.2, §2.5, §9.2 critic table | `BattleFx.Attack.cs:209`; `BattleView.Enemy.cs:25`; `GuideQuestBarView.cs:111` (no callers); `CharacterSceneBuilder.cs:825/835`, `EvolutionCraftPopup.cs:10/217`; UX §3.15 "iOS and editor: no-op"; research EVT-01 rows 4–5 |
| C-14 | §3.2 status corrected: the FX-gallery capture already works (180 crops), so steps 1–2 are done | §3.2 | `fx_out/baseline/capture.log` `[Verify] FxGallery: 180장`; `fx_out/baseline/fx/` (180 png + index.txt); `fxdir/sheet_fx.png` |
| C-15 | **NEW-01 첫 주** (first-week mission sheet) | §13.6, §2.1, §2.3, §2.4, §2.7, §2.12, C.2 | Capybara Go 7-Day Carnival, Legend of Slime "Slime Diary", MapleStory Idle 14-day mission (`research2` notes); in neither the backlog nor v1 |
| C-16 | **NEW-02 오늘의 작은 선물** (free daily shop gift) | §13.7, §2.1, §2.3, §2.4, §2.7, C.3 | Legend of Slime's free daily gems in the shop; F-04 `tab.shop.free` |
| C-17 | **NEW-03 길 잃은 반딧불** (tappable random visitor, P2) | §13.8, §2.1, §2.3, §2.4, C.4 | Legend of Slime flying boxes, The Tower floating gems, Cookie Clicker golden cookie (`research2/global-idle-notes.md`) |
| C-18 | **NEW-04 입장권 모아 두기** (dungeon ticket bank, P2) | §13.9, §2.1, §2.3 | MapleStory Idle keys stored up to 10; Capybara Go energy overflow; `DungeonSystem.UseTicket` resets per DayKey and `Challenge` spends a ticket only on a clear |
| C-19 | Phase-5 candidate table and rejected list (relics, research tree, roguelite, garden, presets, weekly boss; summon level, gear, prestige, server features, ads, free daily pull, Lucky Defense merge loop) | §13.10 | `research2` candidate order (20 items) was never merged into v1; research §17 |
| C-20 | Ownership: the critic table (new files per package); `Core/Haptics.cs` gets an owner (P4-7); UI packages must run UX WP-9's `UiSceneLintTests` before handoff | §9.2 | v1 left `Haptics.cs` unowned; UX §4.1 WP-9 owns `UiSceneLintTests` |
| C-21 | Package order steps for P4-1, P4-2, P4-3b and P4-6 | §9.4 | follows from C-01 … C-18 |
| C-22 | Tests and captures (7 new test files, 6 updated; captures 04c, 05b, 07b, 22b/22c, 23c, 27b, 42; capture 37 updated); static checks for '장비' and CheerChip clearance | §11.1, §11.4 | – |
| C-23 | Risks: inflation +45%, scope ≈48 items / ≈49 agent-days, current saves after C-01, the parallel IA layout study, CR-01 pause cost | §12 #3, #10, #13–#15 | `scratchpad/ia/` (tall canvas, sheet variants, keep-out maps; no spec yet) |
| C-24 | Conflict resolutions #10–#13: PRG-02 bar vs M11, CR-01 vs stageEndPause, FX-04 synergy line accepted as is, SET-01 "다시 보기" scoped to the intro | §2.11 | `SynergyBannerView` shows only when the set changes (`BattleView.Skills.cs:155-163`) |
| C-25 | Contract plumbing: the §2.4 stubs cover §13; `AppSignals.FtueFirstLampBeat/FtueFirstImpression` let P4-3b and P4-6 compile apart; critic provider rows in §9.3; the NOTI-01 DungeonTickets rule under NEW-04; manual check #6; the P4-0 precondition; version 1.1 | header, §2.4, §2.6, §4.3, §9.3, §9.4, §11.5 | – |

**Checked and found consistent (no change):** the achievements total of 3,400 over 62 tiers (re-added line by line); the affinity total of 3,780; the story + chest total of 840; the lost-chest expectation of 0.83 per clear; the blessing `StatType`s (every one exists in `GameEnums.StatType`); `Mail.Send(title, body, rewards, expireDays)` for coupons; the `CounterKey`, `FeatureId` and `MetaPopupKind` append values (the last are 30, 24 and 7); away time is uncapped (`MetaSession.ComputeAwaySeconds`), so the 72 h comeback trigger can fire; a failed dungeon run already keeps its ticket.

**Web references used by the critic** (other genre facts come from the `research2` notes, which list their own sources): [grindnstrat: Lucky Defense artifacts guide](https://grindnstrat.com/?p=1001), [grindnstrat: Lucky Defense mythic stones](https://grindnstrat.com/lucky-defense-mythic-stone-farm-guide), [ghidbase Lucky Defense guides](https://ghidbase.github.io/lucky-defense-guides), [Lucky Defense on the App Store](https://apps.apple.com/us/app/lucky-defense/id6482291732), [BlueStacks Lucky Defense beginner guide](https://www.bluestacks.com/th/blog/game-guides/lucky-defense/luckd-beginners-guide-th.html).

---

## Appendix D: Amendments after RM-2 (2026-11-06, orchestrator)

Applied in the RM-3 docs pass (T-5) from `scratchpad/roadmap/rm3/triage.md` §3. They record what RM-2 built and G2 verified; where they differ from older text above, they take precedence.
- **A-03 (R10)** §2.5 hook table, `IdleChestSystem.cs` row: adds `public Func<double> NightBonusMultiplier;`.
- **A-04 (R14)** §13.2 and §11.4 (22b/22c): the CheerChip sits at StageArea TopRight(−132,−310), screen ≈ x 836–948, y 440–488. §7.2: the L1 line uses '{방향}이/가'.
- **A-05 (iii) (R15, R70)** §4.1: the Settings window is 940×1516 (body ≈1326).
