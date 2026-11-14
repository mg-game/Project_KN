# SYS_Economy

| 항목 | 값 |
|---|---|
| 상태 | Approved (D-30) |
| 작성자 | ① 구현: 기개발 · ②③: DD |
| 최종 수정 | 2026-11-14 (§3 #8 수선점 강화 · 친구 레벨업 재화: E안, §1.4 이음매 설정 · 지출 기록, §6 판정 기준 미정) |
| 관련 테이블 | - |
| 관련 문서 | [`KN_Glossary`](../00_Overview/KN_Glossary.md) §3 · [`SYS_InGameBattle`](./SYS_InGameBattle.md) |
| 근거 | 코드 커밋 `4b73a5a` (`feature/lost-and-lovely-import`) — 경로 약칭 표 참고 |

> 구조(D-26): **① 현재 구현** = 지금 코드가 실제로 하는 일 / **② 기획 방향** = 기획서가 원하는 모습 / **③ 차이** = 둘이 다른 곳, 대표가 결정.
> 결정이 나면 ②에 반영하고 ③ 행에 `D-n`을 적는다. 코드 수정은 지시함(#3)으로. 용어는 [`KN_Glossary`](../00_Overview/KN_Glossary.md)를 따른다.

## 0. 개요
- 목적: 게임 안의 모든 재화 · 재료가 **어디서 생기고(수도꼭지) 어디서 사라지는지(싱크)** 를 한 곳에서 정의해, 성장 속도 · 과금 · 어뷰징을 통제한다.
- 핵심 루프 내 위치: 전투 · 방치 → 획득 → 성장 · 소환 · 진화로 소비 → 전투력 ↑
- 관련 필라: P3 희귀도 ≠ 서열 (과금 압박 억제) · P6 혼자 자기 속도로 (시간 강제 없는 획득)

## 1. 현재 구현 (As-built) — 기개발

> 작성: 기개발 (2026-11-11). **근거: `feature/lost-and-lovely-import` @ `4b73a5a`**.
> 코드가 실제로 하는 일만 적었다. 해석 · 제안 · 기획과의 비교 판단은 넣지 않았다 (② · ③은 DD).
> 범위: 재화 = `CurrencyType` 3종 — 단추(`CurrencyType.Gold`) · 별조각(`CurrencyType.Gem`) · 별빛 사탕(`CurrencyType.EventToken`). 재료 = `ItemDatabase.asset`의 아이템 9종 (모두 `ItemCategory.Material`, `Equipment` 아이템은 없음). 메타 지갑(빠른 수선권 · 두 배 수선권 · 선택 기억편 · 친구 선택권)과 계보 기억편은 범위 밖.
> 수치 출처: 런타임 에셋(`Data/*.asset`) 값. 에셋에 필드가 없으면 코드 기본값이 쓰이며 "에셋에 없음"으로 적었다. "메타 표" = `MetaDatabase.asset` 섹션, 합계는 기개발이 그 섹션 보상 행을 더한 값.

**파일 약칭** (모두 `Assets/_Project/` 아래)

| 약칭 | 경로 |
|---|---|
| `CUR` · `INV` · `RS` | `Scripts/Systems/CurrencySystem.cs` · `InventorySystem.cs` · `RewardService.cs` |
| `PD` · `GE` · `STORE` · `BOOT` · `GAME` | `Scripts/Core/PlayerData.cs` · `GameEnums.cs` · `SaveStorage.cs` · `GameBootstrap.cs` · `Game.cs` |
| `GC` · `GC.asset` · `BC.asset` · `GT.asset` | `Scripts/Data/GameConfig.cs` · `Data/GameConfig.asset` · `Data/BattleConfig.asset` · `Data/GachaTable_Normal.asset` |
| `GROW` · `OFF` · `GACHA` · `MAIL` · `RMAIL` | `Scripts/Systems/GrowthSystem.cs` · `OfflineRewardSystem.cs` · `GachaSystem.cs` · `MailSystem.cs` · `RewardMailSystem.cs` |
| `SHOP` · `SDB.asset` | `Scripts/Systems/Characters/ShopSystem.cs` · `Data/ShopDatabase.asset` |
| `CS` · `EVO` · `CDB.asset` | `Scripts/Systems/Characters/CharacterSystem.cs` · `EvolutionSystem.cs` · `Data/CharacterDatabase.asset` |
| `BS` · `DUN` · `EXP` | `Scripts/Systems/Battle/BattleSystem.cs` · `DungeonSystem.cs` · `ExpeditionSystem.cs` |
| `IDB` · `IDB.asset` · `EXPDB.asset` · `META` | `Scripts/Data/ItemDatabase.cs` · `Data/ItemDatabase.asset` · `Data/ExpeditionDatabase.asset` · `Data/MetaDatabase.asset` |
| `MRS` · `IDLE` · `EVT` · `CC` | `Scripts/Systems/Meta/MetaRewardService.cs` · `IdleChestSystem.cs` · `EventCalendarSystem.cs` · `ChapterChestSystem.cs` |
| `GQ` · `MIS` · `ATT` · `ACH` | `Scripts/Systems/Meta/GuideQuestSystem.cs` · `MissionSystem.cs` · `AttendanceSystem.cs` · `AchievementSystem.cs` |
| `CODEX` · `AFF` · `GM` · `SW` | `Scripts/Systems/Meta/CodexSystem.cs` · `AffinitySystem.cs` · `GachaMilestoneSystem.cs` · `StarterWeekSystem.cs` |
| `CB` · `STORY` · `TUT` · `FG` | `Scripts/Systems/Meta/ComebackSystem.cs` · `StorySystem.cs` · `TutorialSystem.cs` · `FeatureGateSystem.cs` |
| `DG` · `FF` · `CPN` · `SET` | `Scripts/Systems/Meta/DailyGiftSystem.cs` · `FireflyRules.cs` · `CouponSystem.cs` · `Scripts/UI/Popups/SettingsPopup.cs` |

### 1.1 규칙
```
[지급 — 재화] CurrencySystem.Add(type, amount)                         (CUR:21-25, 35-39)
IF amount ≤ 0 또는 NaN → 아무것도 하지 않음 (+∞는 거르지 않음)
ELSE → currencies[type] = 보유 + amount, Changed(type, 새 값)

[지급 — 재료] InventorySystem.Add(id, count) / AddRange(ids: 각 1개)     (INV:43-54, 101-112)
IF count ≤ 0 → 무시
ELSE IF ItemDatabase에 없는 id → 경고 로그 "[Inventory] 존재하지 않는 아이템", 무시
ELSE → items[id] = 보유 + count, ItemAdded(id, count) → Changed

[공통 창구] RewardService.Grant(RewardData)                            (RS:40-51)
IF type == Currency → Currency.Add  /  ELSE IF type == Item → Inventory.Add(itemId, (int)amount)

[메타 창구] MetaRewardService.Grant(rewards, multiplier, excludeGem)     (MRS:56-155)
IF multiplier ≤ 0 · NaN · ∞ → 1                                         (MRS:60)
Currency    → ⌊amount × 배율 + 1e-9⌋ (excludeGem이면 별조각 ×1), 0 이하 건너뜀 → RS
Item        → ⌊amount × 배율⌋, int 최대값으로 자름, 0 이하 · 빈 id 건너뜀 → RS
GoldMinutes → ⌊받는 순간 초당 단추 × 60 × 분 × 배율⌋ → 단추               (MRS:86-92, 346-351)

[소비 — 재화] CurrencySystem.TrySpend(type, amount)                      (CUR:27-33)
IF amount < 0 또는 NaN → ArgumentOutOfRangeException
ELSE IF 보유 < amount → false, 변화 없음
ELSE → 보유 − amount, Changed (amount 0도 true)

[소비 — 재료] InventorySystem.TryConsume(id, count)                      (INV:57-65)
IF count ≤ 0 또는 보유 < count → false
ELSE IF 보유 == count → 키 삭제  /  ELSE → 보유 − count

[부족 시] 호출부가 처리, 아무것도 빼지 않고 끝남
- 소환 false (GACHA:117) · 수선점 강화 / 계보 레벨업 false (GROW:92, CS:238)
- 상점 NotEnoughCurrency "○○이 부족해요" (SHOP:132-134, 145)
- 빠른 수선 "별조각이 부족해요" (IDLE:528-531) · 진화 제작 CanCraft false (EVO:107-109)

[음수 · 0 · NaN]
- 상점 가격 NaN · 음수 → 판매 불가(NotFound), 0은 허용 (SHOP:152)
- 저장된 보유량의 음수 · NaN은 고치지 않음 (Sanitize는 null만, PD:71-72)
- 단추는 소수가 될 수 있음: 방치 수익(BOOT:252) · 옛 오프라인 보상(OFF:39, 45) · 수선점 강화 비용(GROW:41)에 내림 없음

[상한]
- 재화 상한 없음 (double). 재료 상한 없음, int 덧셈에 넘침 검사 없음 (INV:109)
- int 최대값으로 자르는 곳: 메타 보상 재료(MRS:80) · 원정 재료(EXP:464)
- 상점: 상품별 dailyLimit · totalLimit, 코스튬 최대 1 (SHOP:97-108). 하루 = 현지 05:00 날 키 (SHOP:8-10)

[저장]
- currencies: Dictionary<CurrencyType, double>, 키 = 열거형 이름 "Gold" (PD:10-11, 29)
- items: Dictionary<string, int>, 0개가 되면 키 삭제 (PD:31, INV:61)
- 읽기: TolerantEnumKeyDictionaryConverter(모르는 열거형 키 건너뜀) → Sanitize (STORE:168-171, 842-848)
- 새 게임: 단추 · 별조각 키만 넣음. 별빛 사탕은 키 없음 → Get 0 (PD:59-65, CUR:17)

[그 밖]
- 별빛 사탕을 빼는 곳은 상점 구매뿐. 이벤트 종료 때 지우는 코드 없음 (EventToken 검색: BS:721 · 상점 데이터 · 이름 표시)
- 별조각 결제 지급 코드 없음 (UnityEngine.Purchasing 등 검색 결과 없음)
- 재료 단추 보너스 = 보유 종류마다 goldBonusPercent 1회 합 (INV:83-99), 9종 모두 0
```

### 1.2 상태 전이
상태 없음 — 재화 · 재료는 보유 수치만 있다. 지급 · 소비 = 수치 변경 + `Changed` 알림.

### 1.3 수치 (코드 상수 · 설정값)
| 이름 | 값 | 위치 (파일:줄) | 의미 |
|---|---|---|---|
| `startGold` · `startGem` | 0 · 300 | `GC.asset:15-16` | 새 게임 단추 · 별조각 (`PD:62-63`) |
| 환영 우편 | 별조각 1,000 · 단추 500 | `BOOT:112-116` | `SendOnce("welcome")`, 보관 기한 없음 |
| `maxLevel` · `levelUpBaseCost` · `levelUpCostGrowth` | 1000 · 10 · 1.12 | `GC.asset:17-19` | 수선점 최대 레벨 · 강화 L→L+1 = 10 × 1.12^(L−1) (`GROW:40-41`) |
| `baseGoldPerSecond` · `goldPerSecondGrowth` · `StageGoldBonusPerStage` | 1 · 1.06 · 0.1 | `GC.asset:20-21` · `GROW:16` | 초당 단추 = 1 × 1.06^(L−1) × 재료 보너스 × (1 + 0.1 × (최고 스테이지 − 1)) (`GROW:71-74`) |
| `IdleGoldMinInterval` · `IdleGoldMaxInterval` | 0.2초 · 1초 | `BOOT:42,44` | 방치 단추 지급 주기 (`BOOT:254`) |
| `levelRewardInterval` · `levelRewardGem` · `accountLinkRewardGem` | 10 · 50 · 300 | `GC.asset:22-23` · `GC:42` (에셋에 없음) | 수선점 10레벨마다 별조각 50 우편 · 첫 계정 연동 별조각 우편 |
| `offlineMaxHours` · `offlineRewardRate` · `offlineMinSeconds` | 8 · 0.5 · 60 | `GC.asset:24-26` | 옛 오프라인 보상 (`OFF`) |
| `characterLevelUpBaseCost` · `characterLevelUpCostGrowth` · `characterMaxLevel` | 50 · 1.09 · 100 | `GC:50-53` (에셋에 없음) | 계보 레벨업 비용 · 최대 레벨 |
| `substituteShardCostByGrade` | {0, 5, 10, 20, 40} | `GC:71` (에셋에 없음) | 진화 재료 1개를 기억편으로 대체하는 비용 (등급순, Rare 이상만, `EVO:148-153`) |
| `costCurrency` · `singleCost` · `tenCost` | 1(Gem) · 100 · 900 | `GT.asset:17-19` | 소환 비용 (`GACHA:85-89`) |
| `stageGoldBase` · `stageGoldGrowth` · `festivalGoldPercent` | 60 · 1.13 · 50 | `BC.asset:387-388` · `BC.asset:377` | 스테이지 클리어 단추 · 축제 기믹 스테이지 단추 +% |
| `stageCandy` · `stageCandyMilestone` · `stageCandyMilestoneInterval` | 3 · 10 · 10 | `BC.asset:389-391` | 클리어 별빛 사탕 |
| `stageClothChance` · `stageRibbonChance` · `failGoldRatio` | 0.35 · 0.25 · 0.3 | `BC.asset:392-394` | 클리어 재료 확률 · 실패 단추 비율 |
| `dungeonGoldBase` · `dungeonGoldGrowth` · `dungeonThreadFloor` · `dungeonSpringFloor` · `dungeonMoonlightFloor` · `dungeonMoonlightChance` | 150 · 1.15 · 3 · 5 · 10 · 0.12 | `BC.asset:397-402` | 던전 층 단추 · 재료 시작 층 · 달빛 조각 기본 확률 |
| `ExtraMemberGoldBonus` · `ExtraMemberDropChanceBonus` · `TierRewardScale` | 0.25 · 0.15 · {1.0, 2.8, 5.4} | `EXP:36,38,72` | 원정 인원 1명 추가당 단추 · 재료 확률 배율, 시간 단계 배율 |
| `quickIdleHours` · `quickIdleGemCosts` | 2 · {0, 50, 100} | `META:1673-1674` | 빠른 수선 1회 = 2시간치, 하루 n번째 별조각 비용 (int 배열 hex `000000003200000064000000`) |
| `baseCapHours`(+`capSteps`) · `stageGrowth` · `offlineGoldRate` | 8 (12 · 16) · 1.0025 · 0.5 | `META:1584-1591` · `META:1665-1666` | 바구니 쌓임 상한(시간) · 줄 스테이지 배율 · 단추 줄 비율 |
| `maxQuickIdleTickets` · `freeDoublePerDay` · `doubleExcludesGem` | 20 · 1 · 1 | `META:1675-1678` | 빠른 수선권 보관 상한 · 하루 무료 두 배 · 두 배에서 별조각 제외 |
| `gemPerLevel` · 도감 별조각 | 10 · 형태 20 · 30 · 50, 코스튬 30, 망각체 10, 시너지 20 + 30, 재료 5 | `META:3878` · `META:3333-3340` | 호감도 단계 별조각 = 10 × 단계 · 도감 칸 보상 (`CODEX:722-759`) |
| `lostChest.dropChance` · `firefly.perDay` | 0 · 0 | `META:4117` · `META:5464` | 잃어버린 상자 · 반딧불 꺼짐 |

### 1.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `PlayerData.currencies` | `Dictionary<CurrencyType, double>` | 재화 보유량 (`PD:29`) | `{"Gold": 1234.5, "Gem": 300}` |
| `PlayerData.items` | `Dictionary<string, int>` | 재료 보유 개수 (`PD:31`) | `{"mat_cloth": 12}` |
| `GameConfig.shopUpgradeCurrency` · `lineageLevelUpCurrency` | `CurrencyType` | 수선점 강화 · 친구 레벨업에 쓰는 재화. 기본 `Gold` (§3 #8 E안 이음매). 시스템(`GrowthSystem.LevelUpCurrency` · `CharacterSystem.LevelUpCurrency`)만 이 값을 읽는다. 화면은 아직 단추를 직접 본다 (§6) | `Gold` · `Gold` |
| 플레이 기록 `spend` 줄 | `sink` · `cur` · `amt` · (`from` · `to` 또는 `id` · `lv`) | 레벨업에 쓴 재화를 소비처별로 남긴다 (`PlayLogRecorder`). §3 #8 판정 근거 | `{"ev":"spend","sink":"shop","cur":"gold","amt":21,"from":1,"to":3}` |
| `ItemDef.id` · `name` | string · string | 아이템 ID · 표시명. ID 중복이면 경고 후 앞 것만 (`IDB:66-69`) | `mat_cloth` · 수선 천 |
| `ItemDef.grade` · `category` | `ItemGrade` (Common 0 ~ Mythic 4) · `ItemCategory` (Material 0 · Equipment 1) | 등급 = 기억편 대체 비용 · 보유 목록 정렬(등급 높은 순 → ID, `INV:76-78`). 기억편 대체 · 도감 재료 칸은 Material만 | 0 · 0 |
| `ItemDef.goldBonusPercent` | float | 보유 시 초당 단추 +% (종류당 1회) | 0 (9종 모두) |
| `ItemDef.sources[]` | `ItemSource` (`ItemSourceType` type · targetId · label) | 획득처 표시 · "재료 수집하러 가기" 이동. 지급에는 쓰이지 않음 | `{3, shop_normal, "수선점 상점"}` |
| `RewardData` | type(`RewardType`) · currency · itemId · amount(double) | 전투 · 원정 · 상점 · 우편 보상 한 줄 (`RS:7-19`) | `{1, 0, mat_cloth, 5}` |
| `MetaReward` | type(`MetaRewardType` 0~8) · currency · itemId · amount | 메타 표 보상 한 줄. 2 = 단추 N분치 (`GE:531-551`) | `{2, 0, "", 30}` |
| `ShopProductDef` | id · rewards · costumeId · currency · price · dailyLimit · totalLimit | 상점 상품 (`Scripts/Data/ShopDatabase.cs:8-23`) | `normal_cloth` |

아이템 정의 (`IDB.asset`) — `sources` 열은 에셋의 표시용 label 그대로

| id | 표시명 | 등급 | `sources` label | 위치 |
|---|---|---|---|---|
| `mat_cloth` | 수선 천 | Common | 수선점 상점 · 원정 1-4 · 원정 1-1 · 원정 2-4 · 스테이지 보상 | `IDB.asset:16-43` |
| `mat_ribbon` | 리본 조각 | Common | 수선점 상점 · 원정 1-1 · 원정 1-4 · 원정 1-2 · 스테이지 보상 | `IDB.asset:44-70` |
| `mat_wax` | 촛농 방울 | Common | 수선점 상점 · 원정 1-3 | `IDB.asset:71-88` |
| `mat_thread` | 기억의 실 | Rare | 잔향이 던전 3층 · 원정 2-3 · 수선점 상점 | `IDB.asset:89-109` |
| `mat_lace` | 레이스 조각 | Rare | 원정 1-2 · 원정 2-2 · 원정 1-3 · 수선점 상점 | `IDB.asset:110-132` |
| `mat_rib` | 우산살 | Rare | 원정 2-1 · 수선점 상점 | `IDB.asset:133-149` |
| `mat_suitcase` | 여행 가방 조각 | Epic | 이벤트 상점 | `IDB.asset:150-165` |
| `mat_spring` | 오르골 태엽 | Epic | 잔향이 던전 5층 · 원정 2-2 | `IDB.asset:166-182` |
| `mat_moonlight` | 달빛 조각 | Legendary | 잔향이 던전 10층 · 이벤트 상점 | `IDB.asset:183-200` |

### 1.5 재화 · 재료 획득처 / 사용처
위치는 지급 · 소비를 부르는 줄과 그 양이 적힌 데이터 줄이다. 지급은 모두 `RS:40-51`(메타 표는 `MRS:56-155` → `RS`)을 거친다.

**단추 (`CurrencyType.Gold`)**

| 재화/재료 | 구분(획득/사용) | 어디서 | 양 · 공식 | 위치 (파일:줄) |
|---|---|---|---|---|
| 단추 | 획득 | 새 게임 시작값 | 0 | `PD:62` · `GC.asset:15` |
| 단추 | 획득 | 방치 수익 (앱 켜 둔 동안) | 초당 단추 × 프레임 시간 적립, 1초마다 또는 0.2초 이상 + 1 이상이면 지급. 내림 없음 | `BOOT:250-263` |
| 단추 | 획득 | 옛 오프라인 보상 | 초당 단추 × min(비운 시간, 8시간) × 0.5, 60초 미만 없음. 부팅 계산은 `!Game.IsMetaActive`일 때만, `Game.SetupMeta`가 `IsMetaActive = true` | `OFF:30-48` · `BOOT:119` · `GAME:174` |
| 단추 | 획득 | 수선 바구니 받기 — 단추 줄 | ⌊초당 단추 × 3600 × 0.5 × 앱을 닫아 둔 시간(h) + 소수 이월⌋. 두 배 받기 ×2 | `IDLE:718` · `IDLE:783-786` · `IDLE:436-437` |
| 단추 | 획득 | 빠른 수선 | 바구니 2시간치 (단추 줄 포함) | `IDLE:574-579` |
| 단추 | 획득 | 스테이지 클리어 | ⌊⌊60 × 1.13^(s−1)⌋ × (1 + 보너스%/100)⌋. 보너스% = 시너지 `GoldGainPercent` 합 + 축제 기믹 50, 하한 −90 | `BS:712` · `BS:743-744` · `BS:612-617` · `BS:644` |
| 단추 | 획득 | 스테이지 실패 | ⌊클리어 단추식 × 0.3 × clamp01(처치 / 보스 등장 처치)⌋, 1 이상일 때만 | `BS:769-776` · `BS:659` |
| 단추 | 획득 | 잔향이 던전 돌파 · 소탕 · 한 번에 소탕 | ⌊150 × 1.15^(층−1) × (1 + 덱 시너지 보너스%/100)⌋. 한 번에 소탕은 × 횟수 | `DUN:156-158` · `DUN:209` · `DUN:390` · `DUN:277,288` |
| 단추 | 획득 | 원정 받기 | ⌊구역 `goldReward` × (1 + 0.25 × (인원−1)) × 단계 배율⌋. 구역 300 · 600 · 1,000 · 3,000 · 2,500 · 4,000 · 6,000 · 9,000 | `EXP:440-441` · `EXP:394` · `EXPDB.asset:25,44,63,82,101,120,139,158` |
| 단추 | 획득 | 환영 우편 (1회) | 500 | `BOOT:115` → `MAIL:96,111` |
| 단추 | 획득 | 메타 표 고정 단추 | 가이드 퀘스트 6행 합 5,000 · 튜토리얼 단계 250 | `GQ:173` · `TUT:384` · `META:502-952` · `META:1900` |
| 단추 | 획득 | 메타 표 단추 N분치 (`GoldMinutes`) | 가이드 퀘스트 5분 · 미션 상자 30분 · 출석 8행 합 1,200분 · 반딧불 10분(`perDay` 0이라 나오지 않음) | `GQ:173` · `MIS:339` · `ATT:182` · `FF:115` · `META:781,1180,5467` |
| 단추 | 획득 | 개발자 메뉴 (에디터 · 개발 빌드 전용) | 초당 단추 × 600 · 테스트 우편 초당 단추 × 60 | `SET:729` · `SET:774-777` · `SET:788-794` |
| 단추 | 사용 | 수선점 강화 (1 · 10 · 최대) | 합계 Σ 10 × 1.12^(L−1), 내림 없음 | `GROW:40-49` · `GROW:92` |
| 단추 | 사용 | 계보 레벨업 (+1) | ⌈50 × 1.09^(L−1) − 1e-6⌉ | `CS:199-206` · `CS:238` |
| 단추 | 사용 | 수선점 상점 `shop_normal` | 수선 천 ×5 = 400 · 리본 조각 ×5 = 300 · 촛농 방울 ×5 = 300, 각 하루 3회 | `SHOP:117` · `SDB.asset:31,43,55` |

**별조각 (`CurrencyType.Gem`)**

| 재화/재료 | 구분(획득/사용) | 어디서 | 양 · 공식 | 위치 (파일:줄) |
|---|---|---|---|---|
| 별조각 | 획득 | 새 게임 시작값 | 300 | `PD:63` · `GC.asset:16` |
| 별조각 | 획득 | 환영 우편 (1회) | 1,000 | `BOOT:115` → `MAIL:96,111` |
| 별조각 | 획득 | 수선점 레벨 보상 우편 | 10레벨마다 50 (키 `level_reward_N` 1회) | `RMAIL:21-33` · `GC.asset:22-23` |
| 별조각 | 획득 | 첫 계정 연동 우편 | 300 | `RMAIL:36-44` · `GC:42` |
| 별조각 | 획득 | 수선 바구니 별조각 줄 | 시간당 3 × 1.0025^(최고 스테이지−1), 배율 상한 1.5. 두 배 받기 제외 | `IDLE:724-731` · `IDLE:437` · `META:1593-1600` |
| 별조각 | 획득 | 호감도 단계 받기 | 단계마다 10 × 단계 번호 | `AFF:128-132` · `META:3878` |
| 별조각 | 획득 | 도감 칸 받기 | 1.3 도감 별조각 값 | `CODEX:722-759` · `CODEX:812-818` |
| 별조각 | 획득 | 업적 | 62행 합 3,400 | `ACH:451` · `META:2088-3331` |
| 별조각 | 획득 | 가이드 퀘스트 | 14행 합 640 | `GQ:173` · `META:502-952` |
| 별조각 | 획득 | 출석부 | 13행 합 2,100 | `ATT:182` · `META:1259-1582` |
| 별조각 | 획득 | 미션 상자 (미션 19개 자체 `rewards`는 모두 빈 목록) | 5행 합 400 | `MIS:339` · `META:1176-1258` |
| 별조각 | 획득 | 챕터 상자 (상자 45줄: 1~5장 · 10~200장 5장 간격 · chapter 0 = 줄 없는 장 기본) | 44행 합 4,490 | `CC:9-11` · `CC:123` · `META:4192-4998` |
| 별조각 | 획득 | 소환 마일스톤 · 반복 | 100 · 반복 300 | `GM:266` · `META:3595,3626` |
| 별조각 | 획득 | 이벤트 '별이 머문 밤 산책' 미션 · 도장 | 미션 100 · 도장 150 | `EVT:332,354` → `EVT:547` · `META:3753,3807` |
| 별조각 | 획득 | 첫 주 | 3행 합 300 | `SW:311` · `META:5017-5416` |
| 별조각 | 획득 | 복귀 도장 | 300 | `CB:112` · `META:3852` |
| 별조각 | 획득 | 동화책 첫 읽기 | 5행 합 250 | `STORY:77` · `META:4023-4115` |
| 별조각 | 획득 | 튜토리얼 마지막 단계 (건너뛰기 졸업 선물도 같은 행) | 300 | `TUT:384` · `TUT:291` · `META:2069` |
| 별조각 | 획득 | 기능 개방 | 30 | `FG:419` · `META:88` |
| 별조각 | 획득 | 작은 선물 | 20 | `DG:78` · `META:5444` |
| 별조각 | 획득 | 쿠폰 → 우편 | 50 | `CPN:100-101` → `MAIL:96` · `META:5004` |
| 별조각 | 획득 | 반딧불 (`perDay` 0이라 나오지 않음) | 10 | `FF:48` · `FF:115` · `META:5480-5486` |
| 별조각 | 획득 | 개발자 메뉴 (에디터 · 개발 빌드 전용) | 1,000 · 테스트 우편 100 | `SET:781-786` · `SET:792` |
| 별조각 | 사용 | 소환 (표 `friends`) | 1회 100 · 10회 900. 10회만 `tenCost`, 그 밖은 100 × 횟수. UI 버튼은 1회 · 10회 | `GACHA:85-89` · `GACHA:117` · `GT.asset:17-19` |
| 별조각 | 사용 | 빠른 수선 | 하루 1번째 0 · 2번째 50 · 3번째 100, 4번째부터 불가. 빠른 수선권을 쓰면 별조각 · 하루 횟수 안 씀 | `IDLE:485-494` · `IDLE:562-571` · `META:1674` |
| 별조각 | 사용 | 수선점 상점 `shop_normal` | 기억의 실 · 레이스 조각 · 우산살 각 ×1 = 30, 하루 3회 | `SHOP:117` · `SDB.asset:67,79,91` |

**별빛 사탕 (`CurrencyType.EventToken`)**

| 재화/재료 | 구분(획득/사용) | 어디서 | 양 · 공식 | 위치 (파일:줄) |
|---|---|---|---|---|
| 별빛 사탕 | 획득 | 스테이지 클리어 | 3, 10의 배수 스테이지는 10. 축제 이벤트 배율이 1 초과면 ⌊× 배율⌋ (`evt_starlit_walk` 배율 2) | `BS:714-721` · `BC.asset:389-391` · `EVT:177,464` · `META:3684` |
| 별빛 사탕 | 획득 | 수선 바구니 이벤트 전용 줄 | 시간당 2, 이벤트와 겹친 시간만 | `IDLE:724-731` · `META:1657-1664` |
| 별빛 사탕 | 획득 | 이벤트 '별이 머문 밤 산책' 미션 | 7행 합 370 (배율 없음) | `EVT:332` → `EVT:547` · `META:3686-3760` |
| 별빛 사탕 | 획득 | 같은 이벤트 도장 | 4행 합 250 | `EVT:354` → `EVT:547` · `META:3761-3814` |
| 별빛 사탕 | 사용 | 이벤트 상점 `shop_event` | 여행 가방 조각 20 (하루 5) · 달빛 조각 60 (전체 10) · 코스튬 `jjak_sleepcap` 150 · `jjak_festival` 300 (각 1회) | `SHOP:117` · `SDB.asset:110,122,130,138` |

**재료** — 진화 레시피는 `CDB.asset` 형태 29개 중 `materials`가 있는 22개. 바구니 줄은 `minStage` 이상일 때만, 시간당 양 × 1.0025^(최고 스테이지−1) (상한 없음), 두 배 받기 ×2.

| 재화/재료 | 구분(획득/사용) | 어디서 | 양 · 공식 | 위치 (파일:줄) |
|---|---|---|---|---|
| 수선 천 | 획득 | 스테이지 클리어 | 35% 확률 1~2개 | `BS:764` · `BC.asset:392` |
| 수선 천 | 획득 | 원정 | 1-1 60% 1~2 · 1-4 100% 2~4 · 2-1 50% 1~2 · 2-3 100% 2~4 · 2-4 100% 6~10 | `EXP:449-465` · `EXPDB.asset:31-34,84-87,107-110,141-144,160-163` |
| 수선 천 | 획득 | 수선점 상점 | ×5, 단추 400, 하루 3 | `SHOP:121` · `SDB.asset:22-33` |
| 수선 천 | 획득 | 수선 바구니 | 시간당 1.5 | `IDLE:437` · `META:1609-1616` |
| 수선 천 | 획득 | 메타 표 | 가이드 퀘스트 2행 15 · 미션 상자 5 · 출석 3행 45 · 챕터 상자 10 · 복귀 10 · 쿠폰 10 · 첫 주 15 · 작은 선물 5 | `GQ:173` · `MIS:339` · `ATT:182` · `CC:123` · `CB:112` · `CPN:100` · `SW:311` · `DG:78` |
| 수선 천 | 사용 | 진화 제작 | 레시피 10개 합 170 | `EVO:133-136` · `CDB.asset:135-1839` |
| 리본 조각 | 획득 | 스테이지 클리어 | 25% 확률 1개 | `BS:765` · `BC.asset:393` |
| 리본 조각 | 획득 | 원정 | 1-1 100% 2~4 · 1-2 40% 1~2 · 1-4 100% 2~3 · 2-4 50% 2~4 | `EXP:449-465` · `EXPDB.asset:27-30,50-53,88-91,164-167` |
| 리본 조각 | 획득 | 수선점 상점 | ×5, 단추 300, 하루 3 | `SHOP:121` · `SDB.asset:34-45` |
| 리본 조각 | 획득 | 수선 바구니 | 시간당 2 | `IDLE:437` · `META:1601-1608` |
| 리본 조각 | 획득 | 메타 표 | 가이드 퀘스트 5 · 출석 2행 25 · 챕터 상자 10 · 이벤트 도장 15 · 첫 주 15 · 작은 선물 5 | `GQ:173` · `ATT:182` · `CC:123` · `EVT:354` · `SW:311` · `DG:78` |
| 리본 조각 | 사용 | 진화 제작 | 레시피 8개 합 112 | `EVO:133-136` · `CDB.asset:201-2035` |
| 촛농 방울 | 획득 | 원정 | 1-3 100% 2~4 | `EXP:449-465` · `EXPDB.asset:65-68` |
| 촛농 방울 | 획득 | 수선점 상점 | ×5, 단추 300, 하루 3 | `SHOP:121` · `SDB.asset:46-57` |
| 촛농 방울 | 획득 | 수선 바구니 | 시간당 1 (`minStage` 5) | `IDLE:437` · `META:1625-1632` |
| 촛농 방울 | 획득 | 메타 표 | 가이드 퀘스트 3 · 출석 10 · 작은 선물 3 | `GQ:173` · `ATT:182` · `DG:78` |
| 촛농 방울 | 사용 | 진화 제작 | 레시피 6개 합 80 | `EVO:133-136` · `CDB.asset:386-1897` |
| 기억의 실 | 획득 | 잔향이 던전 돌파 · 소탕 | 3층부터 1 + ⌊(층−3)/3⌋. 한 번에 소탕은 × 횟수 | `DUN:159-160` · `DUN:209,277,390` · `BC.asset:399` |
| 기억의 실 | 획득 | 원정 | 2-3 25% 1~2 | `EXP:449-465` · `EXPDB.asset:145-148` |
| 기억의 실 | 획득 | 수선점 상점 | ×1, 별조각 30, 하루 3 | `SHOP:121` · `SDB.asset:58-69` |
| 기억의 실 | 획득 | 수선 바구니 | 시간당 0.15 (`minStage` 10) | `IDLE:437` · `META:1633-1640` |
| 기억의 실 | 획득 | 메타 표 | 가이드 퀘스트 2행 5 · 미션 상자 1 · 출석 3행 13 · 챕터 상자 45행 260 · 첫 주 3 | `GQ:173` · `MIS:339` · `ATT:182` · `CC:123` · `SW:311` |
| 기억의 실 | 사용 | 진화 제작 | 레시피 10개 합 113 | `EVO:133-136` · `CDB.asset:137-2096` |
| 레이스 조각 | 획득 | 원정 | 1-2 100% 1~3 · 1-3 30% 1 · 2-2 100% 2~4 | `EXP:449-465` · `EXPDB.asset:46-49,69-72,122-125` |
| 레이스 조각 | 획득 | 수선점 상점 | ×1, 별조각 30, 하루 3 | `SHOP:121` · `SDB.asset:70-81` |
| 레이스 조각 | 획득 | 수선 바구니 | 시간당 1 (`minStage` 3) | `IDLE:437` · `META:1617-1624` |
| 레이스 조각 | 획득 | 메타 표 | 가이드 퀘스트 3 · 출석 5 · 챕터 상자 2행 16 · 작은 선물 3 | `GQ:173` · `ATT:182` · `CC:123` · `DG:78` |
| 레이스 조각 | 사용 | 진화 제작 | 레시피 9개 합 90 | `EVO:133-136` · `CDB.asset:260-2033` |
| 우산살 | 획득 | 원정 | 2-1 100% 1~3 | `EXP:449-465` · `EXPDB.asset:103-106` |
| 우산살 | 획득 | 수선점 상점 | ×1, 별조각 30, 하루 3 | `SHOP:121` · `SDB.asset:82-93` |
| 우산살 | 획득 | 수선 바구니 | 시간당 0.5 (`minStage` 11) | `IDLE:437` · `META:1641-1648` |
| 우산살 | 획득 | 메타 표 | 출석 5 · 챕터 상자 6 | `ATT:182` · `CC:123` |
| 우산살 | 사용 | 진화 제작 | 레시피 6개 합 61 | `EVO:133-136` · `CDB.asset:569-1712` |
| 여행 가방 조각 | 획득 | 이벤트 상점 | ×1, 별빛 사탕 20, 하루 5 | `SHOP:121` · `SDB.asset:101-112` |
| 여행 가방 조각 | 사용 | 진화 제작 | 레시피 3개 합 21 | `EVO:133-136` · `CDB.asset:139-446` |
| 오르골 태엽 | 획득 | 잔향이 던전 돌파 · 소탕 | 5층부터 1 + ⌊(층−5)/5⌋. 한 번에 소탕은 × 횟수 | `DUN:161-162` · `DUN:209,277,390` · `BC.asset:400` |
| 오르골 태엽 | 획득 | 원정 | 2-2 15% 1 | `EXP:449-465` · `EXPDB.asset:126-129` |
| 오르골 태엽 | 획득 | 수선 바구니 | 시간당 0.05 (`minStage` 25) | `IDLE:437` · `META:1649-1656` |
| 오르골 태엽 | 획득 | 메타 표 | 출석 2 · 챕터 상자 42행 85 | `ATT:182` · `CC:123` |
| 오르골 태엽 | 사용 | 진화 제작 | 레시피 6개 합 33 | `EVO:133-136` · `CDB.asset:511-2094` |
| 달빛 조각 | 획득 | 잔향이 던전 돌파 · 소탕 · 한 번에 소탕 | 10층부터 확률 clamp01(0.12 + 0.01 × (층−10))로 1개. 한 번에 소탕은 횟수만큼 굴림 | `DUN:167-169` · `DUN:468-472` · `DUN:208,389` · `DUN:280-287` |
| 달빛 조각 | 획득 | 이벤트 상점 | ×1, 별빛 사탕 60, 전체 10 | `SHOP:121` · `SDB.asset:113-124` |
| 달빛 조각 | 획득 | 메타 표 | 미션 상자 1 · 출석 2 · 소환 마일스톤 2 · 챕터 상자 43행 43 · 첫 주 2 | `MIS:339` · `ATT:182` · `GM:266` · `CC:123` · `SW:311` |
| 달빛 조각 | 사용 | 진화 제작 | 레시피 4개 합 11 | `EVO:133-136` · `CDB.asset:448-2098` |

- 진화 제작은 (필요량 − 기억편 대체분)만 가방에서 뺀다 (`EVO:135-136`). 기억편 대체는 Rare 이상 재료만, 1개당 1.3 `substituteShardCostByGrade` (`EVO:148-153, 182-199`).
- 원정 재료는 성공한 굴림마다 min~max, 확률 = min(1, chance × (1 + 0.15 × 추가 인원)), 굴림 수 = ⌊단계 배율⌋ + 소수 확률 1회, 개수 × `DropMultiplier`(주말 원정 축제 2, `EVT:157,458` · `META:3644`) 내림 (`EXP:444-465`).
- 잃어버린 상자 표(`META:4118-4191`: 수선 천 · 리본 조각 · 촛농 방울 · 오르골 태엽)는 지급되지 않는다: `dropChance` 0(`META:4117`, `CC:83-84`), `BattleSystem.ClearDropHook`에 대입하는 코드는 `BS:757`의 null뿐.
- 반딧불의 재료 칸은 잃어버린 상자 표에서 고른다(`fromLostChestTable` 1, `META:5473-5478`), `perDay` 0이라 나오지 않는다. 별빛 산책 수첩(`SeasonPassSystem`)은 `season.tiers`가 빈 목록(`META:3842`)이라 지급이 없다.

### 1.6 추출 못 한 것
- 시너지 `GoldGainPercent` 실제 값(`SynergyDatabase.asset`, 스테이지 · 던전 단추 보너스) · 이벤트 일정(언제 배율 2가 켜지는지) — 값을 뽑지 않았다.
- 도감 칸 수(도감 별조각 총량) · 업적 · 가이드 퀘스트 · 출석 · 챕터 상자의 행별 조건 · 순서 — 합계만 적었다. 챕터 상자 기본 줄(chapter 0)이 실제로 몇 번 지급되는지(도달 가능한 챕터 수)는 확인하지 않았다.
- 옛 오프라인 보상(`OFF`)에 값이 쌓이는 경로 — 부팅 계산 조건(`BOOT:119`) 밖의 경로(설정 화면 테스트 등)는 끝까지 추적하지 않았다.
- `items` int 덧셈 넘침 시 동작 — 코드에 검사가 없고, 컴파일 옵션(checked)은 확인하지 않았다.
- `Scripts/UI` 쪽 직접 지급 · 소비 — `Currency.Add` · `MetaRewards.Grant` · `TrySpend` 검색만 했다 (개발자 메뉴 외에는 Systems 호출).

## 2. 기획 방향 (To-be) — DD

> 기준 문서: [`SYS_InGameBattle`](./SYS_InGameBattle.md) §7(잔향 = 전투 전용) · §14-6(재화 미결) · [`UI_InGame`](../03_UI_UX/UI_InGame.md) §14.1(재화 표시 원칙) · [`SYS_Growth`](./SYS_Growth.md) D-29.
> 기획서에는 재화 체계가 정의된 적이 없다(§14-6 미결). 그래서 ②는 **현재 구현을 뼈대로 삼고, 비어 있는 규칙(검증 · 상한 · 유료 구분 · 이벤트 소멸)을 채운 안**이다.

### 2.0 재화 체계

| 재화 | 층 | 표시명 (정본) | 코드 | 주 획득 | 주 사용 | 상단 HUD | 상한 |
|---|---|---|---|---|---|---|---|
| 소프트 | 아웃게임 | **단추** | `Gold` | 방치 · 스테이지 · 원정 · 던전 | 수선점 강화 · 계보 레벨업 · 상점 재료 | 고정 | 없음 (표시 K/M 축약) |
| 하드 | 아웃게임 | **별조각** (유료 · 무료 2풀) | `Gem` | 메타 보상 · (결제) | 소환 · 빠른 수선 · 상점 | 고정 | 없음 |
| 이벤트 | 아웃게임 · 기간 한정 | **별빛 사탕** | `EventToken` | 이벤트 기간 스테이지 · 바구니 · 미션 | 이벤트 상점 | 이벤트 화면에서만 | 이벤트별 |
| 전투 | 인게임 세션 | **잔향** | (전투 세션 값) | 초당 수급 · 처치 · 풀림 환급 | 잔향이 소환 · 수선등 밝히기 | 전투 HUD | 99 (D-28 #12) |
| 성장 재료 | 아웃게임 | **수선 재료 9종** | `Item` (Material) | 스테이지 · 원정 · 던전 · 바구니 · 상점 | 진화 제작 · 한계 돌파(P1) | 관련 화면만 | 없음 |
| 계보 재료 | 아웃게임 · 계보별 | **기억 조각** (D-30, 구 "기억편") | `LineageSave.shards` | 중복 획득 · (구조 · 스토리) | 계보 공명 · 진화 재료 대체 · 한계 돌파 | 캐릭터 · 진화 화면 | 없음 |

### 2.1 규칙
```
[지급]
IF amount ≤ 0 OR NaN OR ∞                → 거부 + 오류 로그 (현재: +∞는 통과)
ELSE IF 재화 = 별조각                      → 출처가 결제면 paid 풀, 그 외는 free 풀에 더함
ELSE                                       → 보유 + amount
[소비]
IF amount < 0 OR NaN                       → 거부 (현재 구현과 같음)
ELSE IF 보유 < amount                      → 실패, 부족분을 빨간 숫자 + 획득처 바로가기 (UI §14.1)
ELSE IF 재화 = 별조각                      → free 풀 먼저 소비, 모자라면 paid 풀
ELSE                                       → 보유 − amount
[로드 · 무결성]
IF 저장값 < 0 OR NaN OR ∞                  → 0으로 고치고 무결성 경고 기록 (`SYS_Save`)
IF 재료 개수 > int 상한 근처                → 상한에서 멈춤 (넘침 방지)
[이벤트 재화 소멸]
IF 이벤트 종료                             → 유예 7일 동안 이벤트 상점만 열어 둠
IF 유예 종료                               → 남은 별빛 사탕을 단추로 환산(비율은 BAL) 후 0, 우편으로 안내
[표시]
단추: 내부 double 그대로, 화면은 내림 정수 · K/M 축약 / 별조각: 정수 (paid · free 합계만 표시)
```

### 2.2 상태 전이
```
별빛 사탕
[없음] ─(이벤트 시작)→ [획득 · 사용 가능] ─(이벤트 종료)→ [유예 7일: 사용만] ─(유예 끝)→ [단추로 환산 · 0]
별조각 (풀)
[free] ←(메타 보상)  [paid] ←(결제)   소비 순서: free → paid   (환불 시 paid만 회수)
```

### 2.3 UI 플로우 / 디스크립션 (UI §14.1 따름)
1. **상단 재화 바** : 단추 · 별조각 2종 고정 (홈 · 캐릭터 · 가방 · 상점). 탭 → 획득처 팝업 (별조각은 상점 바로가기)
2. **부족 팝업** : "단추가 ○○ 모자라요" + 획득처 목록(최대 3) → 탭 → 해당 화면 이동
3. **재료 아이콘** (성장 · 진화 화면) : 보유 / 필요, 부족하면 빨강 · 탭 → 획득처 (현재 `ItemDef.sources` 그대로)
4. **이벤트 재화** : 이벤트 화면 상단에만 표시. 유예 기간에는 "○일 뒤 단추로 바뀌어요" 배지
5. **가방** (IV-001) : 탭 `재료 · 기억 조각 · 이벤트`. 기억 조각은 계보별 목록

### 2.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `Currency_Master.currency_id` | enum (PK) | Gold · Gem · EventToken | `Gem` |
| `Currency_Master.display_name` | string | 표시명 (KN_Glossary 정본) | 별조각 |
| `Currency_Master.hud_pinned` | bool | 상단 고정 여부 | true |
| `Currency_Master.cap` | double | 보유 상한 (0 = 없음) | 0 |
| `Wallet.currencies` | Dictionary<enum, double> | 현재 `PlayerData.currencies` 그대로 | `{"Gold": 1234.5}` |
| `Wallet.gem_paid` · `gem_free` | double · double | 별조각 2풀 — **신규**, `currencies[Gem]` = 합 (호환용) | 0 · 1300 |
| `Item_Master.item_id` | string (PK) | 현재 `ItemDef.id` | `mat_rib` |
| `Item_Master.grade` · `category` · `sources[]` | enum · enum · struct[] | 현재 `ItemDef` 그대로 | Rare · Material · … |
| `Item_Master.lineage_hint` | string (FK → Character_Base, nullable) | 원물 테마 계보 (우산살 → 레이니 등) — **신규**, 획득처 안내 · 아트 | `rainy` |
| `Event_Currency.event_id` · `grace_days` · `convert_rate` | string (FK → Event) · int · double | 이벤트 재화 유예 · 환산 — **신규** | `evt_starlit_walk` · 7 · 10 |
| `Currency_Log` (런타임 · 선택) | struct | 지급 · 소비 기록 (출처 · 양 · 시각) — 어뷰징 추적용 | `{Gem, +100, "achievement", t}` |

## 3. 차이 → 결정 필요 — DD 작성, 대표 결정

| # | 항목 | ① 현재 구현 | ② 기획 방향 | 영향 (코드 · 데이터 · 세이브) | DD 추천 | 결정 |
|---|---|---|---|---|---|---|
| 1 | 재화 이름 · 체계 | 단추 · 별조각 · 별빛 사탕 + 재료 9종 + 기억편 | 기획서 미정의 (§14-6 ⬜) | 문서 | **현재 구현을 정본으로 채택**, `KN_Glossary` §3 "기획 확정 전" 표기 해제. 세계관(수선점 · 별)과 맞음 | **D-30** DD 추천안 |
| 2 | **기억 조각 ↔ 기억편** (같은 이름 다른 물건) | 기억편 = 중복 획득 계보 재료 (공명 · 진화 대체) | 기억 조각 = 기억 베틀 서사 재료 · 진화 조건 · 한계 돌파 조건 (UI EV-001 · 와이어프레임 ④), 캐릭터별 · 공용 구분 (UI §14.1) | 용어 · UI · 데이터 · 세이브 | **하나로 통합: 표시명 "기억 조각"(세계관 용어), 계보별 보관(코드 `shards` 그대로).** 획득처 = 중복 + 구조 · 스토리. "공용 기억 조각"은 만들지 않음 (재화 수 늘리지 않기) | **D-30** DD 추천안 |
| 3 | 별조각 유료 · 무료 구분 | 한 덩어리 (`Gem` 하나), 결제 코드 없음 | paid · free 2풀, free 먼저 소비, 환불 시 paid만 회수 | 세이브 구조 (호환 필드 추가) | **채택 — 결제를 붙이기 전에 지금 분리.** 결제 환불 어뷰징 대응 · 유료 재화 환불 처리에 필요하고, 나중에 바꾸면 기존 세이브의 출처를 알 수 없음 | **D-30** DD 추천안 |
| 4 | 이벤트 재화 소멸 | 이벤트 끝나도 별빛 사탕 남음, 지우는 코드 없음 | 유예 7일 → 단추 환산 후 0 | 코드 · 이벤트 데이터 | **채택.** 다음 이벤트에 이월되면 이벤트 상점 균형이 깨짐 | **D-30** DD 추천안 |
| 5 | 입력 검증 · 상한 | +∞ 통과, 저장된 음수 · NaN 그대로, 재료 int 넘침 검사 없음 | 지급 · 로드 때 검증, 재료 상한 | 코드 (`CurrencySystem` · `InventorySystem` · 로드) | **채택, P0** — 세이브 조작 · 버그 한 번에 경제가 무너지는 지점 | **D-30** DD 추천안 |
| 6 | 개발자 메뉴 지급 | 에디터 · 개발 빌드에서 단추 · 별조각 지급 | 출시 빌드에서 코드째 빠져야 함 | 빌드 설정 | **확인만** — 기개발이 출시 빌드 제외(`#if`) 여부 확인 | **D-30** DD 추천안 |
| 7 | 별조각 유입량 | 시작 300 + 환영 1,000 + 업적 3,400 · 출석 2,100 · 챕터 상자 4,490 · 가이드 640 · … (소환 1회 100) | 기획 없음 (BM 미정) | 밸런스 · BM | **지금은 유지, `BAL` · BM 문서에서 판단.** 프로토 단계는 넉넉한 쪽이 테스트에 유리. 출시 전 "1주차 소환 횟수" 기준으로 재조정 | **D-30** DD 추천안 |
| 8 | **수선점 강화 · 친구 레벨업 재화 공유** | 둘 다 단추 (`GrowthSystem` · `CharacterSystem`). 친구 쪽은 D-29 ② "M1은 단추, 전환은 여기서 재검토", 수선점 쪽은 정한 기록 없음 (원본 코드 그대로) | 기획자 제기 (2026-11-14): 두 곳은 독립 재화여야 어색하지 않다. 처음 와이어프레임은 친구 레벨업 = 재료 투입형 (`SYS_Growth` §3 #2) | 코드 · 데이터 · 밸런스 (새 재화 키는 옛 세이브와 호환, `CurrencySystem` `Get` 기본 0) | **E안: M1은 단추 공유 유지 + 이음매(`GameConfig` 재화 설정 2칸, 기본 단추) + 소비처별 지출 기록(`spend`).** 친구 전용 재화(B안)는 M1 뒤 데이터로 판정. B는 최소 형태도 4일 넘게 들고, 획득처를 줄이면 그 재화만 방치 중에 안 쌓여 곡선이 어긋남. AI 3관점(경제 · 플레이어 경험 · 개발) 2라운드 토론 합의 | **백승천 결정 (2026-11-14), D 번호는 DD 기록 대기** |

## 4. 예외 처리 / 엣지 케이스
| 케이스 | 처리 |
|---|---|
| 네트워크 단절 | 로컬 경제 (영향 없음). 결제 도입 시 영수증 검증 실패 → 지급 보류 + 재시도 큐 |
| 앱 강제 종료 | 지급 · 소비는 즉시 세이브 대상. 소비 후 보상 지급 전 종료 → 같은 저장 단위로 묶어 원자적으로 처리 (기개발 확인) |
| 기기 시간 조작 | 재화 자체는 무관. 시간 기반 획득(방치 · 바구니 · 일일 상점 리셋)은 `SYS_IdleReward` · `SYS_Save` |
| 단추 소수점 | 내부 double 유지, 표시는 내림 |
| 이벤트 종료 순간 구매 | 서버 시각 없음 → 기기 시각 기준, 유예 7일로 흡수 |

## 5. 어뷰징 검토
| 시나리오 | 대응 |
|---|---|
| 세이브 수정으로 재화 무한 | §3 #5 로드 검증 + `SYS_Save` 무결성(해시) |
| 결제 후 환불 (결제 도입 시) | §3 #3 paid 풀 회수, 회수 불가(이미 사용)면 음수 대신 "빚" 상태로 다음 획득에서 차감 |
| 시간 조작으로 일일 상점 · 빠른 수선 리셋 | `DailyClock` 롤백 감지 (I-5와 같은 방어) |
| 개발자 메뉴 노출 | §3 #6 |
| 이벤트 재화 이월 | §3 #4 |

## 6. 확장성 / 오픈 이슈
- [ ] 별조각 2풀 → 결제(BM) 붙일 때 영수증 · 환불 흐름 `SYS_Shop`
- [ ] 별빛 사탕 환산 비율 · 유예 기간 → `BAL`
- [ ] 기억 조각 통합(§3 #2) 후 `KN_Glossary` · UI 문구 일괄 교체
- [ ] 재화 로그(`Currency_Log`) 저장 여부 — 용량 vs 추적성
- [ ] **§3 #8 B안 전환 판정 기준 · 시점 (미확정).** 후보: ① 경제 = 11/20 시뮬레이션 1차 · 출시 후 2주 2차, 10분 구간 지출 90% 이상 한쪽 쏠림이 전체의 20% 이상 ② 플레이어 경험 = M1 첫 플레이테스트 뒤, 1-1 후 10분 지출 80% 이상 쏠림 또는 테스터 10명 중 3명 이상 "헷갈린다 · 어색하다" ③ 개발 = 11/20 시뮬레이션, 첫 4시간 안에 70% 이상 쏠리는 구간이면 11/24에 B 확정
- [ ] §3 #8 B안으로 갈 때 함께 고칠 곳: 화면의 단추 직접 참조 (`GrowthPanel` 5곳 · `CharacterDetailPopup` 3곳 · `MainTabController` 2곳), 강해지기 시간 계산(초당 단추 기준, `StrengthenAdvisor`), 새 재화 획득처 · 상단 바 · 아이콘, 빌더 재생성
- 💡 §3 #8 "왜 가게 돈으로 친구를 키우지"라는 어색함은 강해지기 안내로는 안 풀린다 (플레이어 경험 의견). M1에서는 친구 레벨업 화면 문구 · 연출로 먼저 덜어 본다
- 확장성: 새 재화는 `Currency_Master` 한 줄 + 표시 규칙만 추가. 재료는 `Item_Master` 데이터만 추가 (코드 수정 없음)
