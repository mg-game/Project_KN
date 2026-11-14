# SYS_IdleReward

| 항목 | 값 |
|---|---|
| 상태 | Approved (D-31) |
| 작성자 | ① 구현: 기개발 · ②③: DD |
| 최종 수정 | 2026-11-14 (단추 · 실타래 반반 지급 주석) · 2026-11-13 (상태 헤더 맞춤 — 검수 KN_DocReview) |
| 관련 테이블 | - |
| 관련 문서 | [`KN_Glossary`](../00_Overview/KN_Glossary.md) · [`SYS_InGameBattle`](./SYS_InGameBattle.md) |
| 근거 | 코드 커밋 `4b73a5a` (`feature/lost-and-lovely-import`) — 경로 약칭 표 참고 |

> 구조(D-26): **① 현재 구현** = 지금 코드가 실제로 하는 일 / **② 기획 방향** = 기획서가 원하는 모습 / **③ 차이** = 둘이 다른 곳, 대표가 결정.
> 결정이 나면 ②에 반영하고 ③ 행에 `D-n`을 적는다. 코드 수정은 지시함(#3)으로. 용어는 [`KN_Glossary`](../00_Overview/KN_Glossary.md)를 따른다.

## 0. 개요
- 목적: 플레이어가 게임을 떠나 있는 동안에도 **수선점이 일했다**는 감각을 주고(방치형 핵심 보상), 돌아올 이유를 만든다.
- 핵심 루프 내 위치: 자동 진행(켜 둔 동안) → 앱을 닫음 → 수선 바구니가 쌓임 → 복귀 · 받기 → 성장 → 자동 진행
- 관련 필라: P6 혼자 자기 속도로 (시간 강제 없음) · P1 짧지만 판단은 계속 (빠른 수선 · 두 배 사용 판단)

## 1. 현재 구현 (As-built) — 기개발

> 작성: 기개발 (2026-11-11). **근거: `feature/lost-and-lovely-import` @ `4b73a5a`.** 코드가 실제로 하는 일만 적었다. 해석 · 제안 · 기획과의 비교는 넣지 않았다 (② · ③은 DD).
> 범위: 옛 오프라인 보상 `OfflineRewardSystem` · 수선 바구니(= 방치 바구니, `IdleChestSystem` — 빠른 수선 · 두 배 받기 포함) · 챕터 상자 / 잃어버린 상자 `ChapterChestSystem` · 시간 기준 `DailyClock` · 비운 시간 계산 `MetaSession`.
> 수치 출처: 빌드 장면은 `Scenes/Main.unity` 하나이고, 그 `GameBootstrap`이 `GameConfig.asset` · `MetaDatabase.asset`을 참조한다 (`SCN:361299` · `SCN:361308`). 그래서 에셋 값이 쓰인다. 에셋과 코드 기본값이 다른 칸은 1.3에 둘 다 적었다.
> **2026-11-14 실타래 분리(결정 2 안 D)**: 이 문서의 단추 양(오프라인 보상 · 바구니 단추 줄 · 빠른 수선 · 켜 둔 동안 초당 · 단추 N분치)은 지금 식으로 계산한 양 B이고, 지급은 반반이다. 단추 = ⌊B × 0.5⌋, 실타래 = ⌊B × 0.5⌋ (초당 적립은 실수 그대로). 바구니 실타래 줄은 코드가 따로 만들며 단추 줄 다음 순서, 이월 · 상한 · 두 배 받기 · 빠른 수선 포함이 단추 줄과 같다. 아래 "단추" 식의 초당 단추 · 0.5는 나누기 전 기준 수입으로 읽는다. 정본은 `SYS_Economy` §1.5.
> 재화: 단추 = `CurrencyType.Gold`, 실타래 = `CurrencyType.LineageXp`, 별조각 = `CurrencyType.Gem`, 별빛 사탕 = `CurrencyType.EventToken`. 재료 표시명은 `IDB`의 `name` (예: `mat_cloth` = 수선 천).
> 시각: `Scripts/` 안의 시간 공급자는 `LocalTimeProvider`(`DateTime.UtcNow` = 기기 시계, `TU:15`) 하나다. `GameBootstrap.ClockOverride`(`GB:89`)는 검증 도구(`Verify/Runtime/AutoShot.Spec4.cs:150`)만 넣는다. 서버 시각을 읽는 코드는 찾지 못했다.

**파일 약칭** (모두 `Assets/_Project/` 아래)

| 약칭 | 경로 |
|---|---|
| `ORS` | `Scripts/Systems/OfflineRewardSystem.cs` |
| `IC` | `Scripts/Systems/Meta/IdleChestSystem.cs` |
| `CC` | `Scripts/Systems/Meta/ChapterChestSystem.cs` |
| `DC` · `MS` | `Scripts/Systems/Meta/DailyClock.cs` · `Scripts/Systems/Meta/MetaSession.cs` |
| `CB` · `EVT` | `Scripts/Systems/Meta/ComebackSystem.cs` · `Scripts/Systems/Meta/EventCalendarSystem.cs` |
| `MRS` · `PQ` | `Scripts/Systems/Meta/MetaRewardService.cs` · `Scripts/Systems/Meta/PopupQueue.cs` |
| `BS` | `Scripts/Systems/Battle/BattleSystem.cs` |
| `GAME` · `GB` | `Scripts/Core/Game.cs` · `Scripts/Core/GameBootstrap.cs` |
| `SAVE` · `PD` · `TU` | `Scripts/Core/SaveData.Meta.cs` · `Scripts/Core/PlayerData.cs` · `Scripts/Core/TimeUtil.cs` |
| `MDB` · `GC` | `Scripts/Data/MetaDatabase.cs` · `Scripts/Data/GameConfig.cs` |
| `CCP` · `CMP` | `Scripts/UI/Meta/ChapterChestPopup.cs` · `Scripts/UI/Meta/ChapterMapPopup.cs` |
| `UIM` · `SET` · `FTUE` | `Scripts/UI/UIManager.cs` · `Scripts/UI/Popups/SettingsPopup.cs` · `Scripts/UI/Tutorial/FtueDirector.cs` |
| `META` · `GCA` · `BCA` · `IDB` | `Data/MetaDatabase.asset` · `Data/GameConfig.asset` · `Data/BattleConfig.asset` · `Data/ItemDatabase.asset` |
| `SCN` | `Scenes/Main.unity` |

### 1.1 규칙

**A. 경로 선택 — `OfflineRewardSystem`은 메타가 꺼졌을 때만 쓴다**
```
`Game.IsMetaActive` = Setup에서 늘 true (GAME:174). false가 되는 곳은 RunMeta가 NotImplementedException을 잡을 때뿐 (GB:387~390) — Assets 안 C#에 `throw new NotImplementedException` 없음 (grep). 객체는 늘 만든다 (GAME:141)
IF IsMetaActive (지금 빌드의 정상 흐름)
  → 비운 시간 보상 = 수선 바구니 (MS → IC.AddOfflineTime). 부팅·복귀에서 OfflineReward.Calculate를 부르지 않음 (GB:119 · GB:294)
  → 접속 팝업도 메타 쪽 (UIM:607~611). 일시정지·종료의 "HasPending이면 Claim" 줄은 남아 있으나 PendingGold = 0이라 지급 없음 (GB:275 · GB:311)
  → 개발 탭의 오프라인 테스트 버튼(`_offlineTestButton`, 에디터 · 개발 빌드만 SET:193~198)도 바구니에 1시간을 넣고 복귀 화면을 요청 (SET:803~815)
ELSE (메타 꺼짐)
  → 부팅·복귀 때 Calculate(lastSaveUnix) (GB:119 · GB:295), 복귀 때 OfflineRewardPopup (GB:297~303), 접속 때 UIM:613~623
  → Calculate (ORS:30~40):
     IF lastSaveUnix ≤ 0 → 없음
     ELSE IF 경과 = 기기 지금 − lastSaveUnix < offlineMinSeconds(60) → 없음 (시계를 되돌려 음수여도 여기서 끝)
     ELSE → PendingDuration = min(쌓인 것 + 경과, offlineMaxHours 8시간), PendingGold = 초당 단추 × PendingDuration(초) × offlineRewardRate 0.5
  → Claim(배율 1) = 단추 지급 후 0으로 (ORS:43~50). 되돌림 판정 · DailyClock은 쓰지 않는다
```

**B. 누적 (수선 바구니)**
```
IF 수선 바구니 잠김 (`FeatureId.IdleChest`, 1-2 클리어 — META:92~100)
  → 쌓이지 않음: 쌓인 시간 0, AddOfflineTime 무시 (IC:706 · IC:330)
ELSE IF lastClaimUnix ≤ 0 (첫 개방)
  → lastClaimUnix = 유효 시각, 이때부터 쌓임 (EnsureStarted IC:610~615, 개방 이벤트 IC:789~796)
재료·별조각·별빛 사탕 줄 (`IdleChestConfig.lines`) — 켜 둔 동안도 꺼 둔 동안도 쌓인다
  → 쌓인 초 = min(상한, 유효 시각 − lastClaimUnix) (IC:702~712)
  → 줄 시간당 양 = amount × stageGrowth^(최고 스테이지 − 1), maxStageMultiplier > 0이면 그 값에서 자름 (IC:716 · IC:724~732)
     · 최고 스테이지 = battle.highestStage (도달한 스테이지 = 클리어 + 1, BS:647 · IC:150). minStage 미만 줄 = 0
  → 줄 시간: 재료·별조각 = 쌓인 시간 + 밤의 수선점 덤, 이벤트 전용 줄 = 이벤트와 겹친 시간만 (IC:657~700, 훅은 EVT:465~470)
  → 받는 수 = floor(시간당 × 줄 시간 + carry). 남은 소수는 받을 때 carry에 저장 (IC:759~787)
단추 줄 — 앱을 닫아 둔 시간만
  → MetaSession이 비운 초로 AddOfflineTime: offlineSeconds = min(상한, offlineSeconds + 비운 초), 넘친 양은 버림 (IC:328~337)
  → 단추 = floor(초당 단추 × 3600 × offlineGoldRate 0.5 × offlineSeconds / 3600 + carry), 받는 순간의 초당 단추 (IC:718 · IC:734)
켜 둔 동안의 단추는 바구니가 아니라 GameBootstrap이 매 프레임 초당 단추 × dt를 지급 (GB:199 · GB:250~255)
```

**C. 상한**
```
단계 상한 = max(baseCapHours 8, 0이 아닌 조건이 모두 맞는 capSteps의 capHours) (IC:621~638) — cleared = 최고 스테이지 − 1, 수선점 레벨 = PlayerData.level (IC:627 · IC:651)
  · 2-1(11) 클리어 → 12시간, 2-1 클리어 + 수선점 Lv.200 → 16시간 (META:1584~1591)
IF 다시 만난 날 한 번짜리 상한 bonusCapSeconds > 단계 상한 → 상한 = bonusCapSeconds (IC:641~649)
  · ComebackSystem.BeforeAwayApplied가 GrantBonusCap(24) — 비운 시간 ≥ 259200초 · 튜토리얼 끝 · 되돌림 아님 · 평생 6번 · 14일 쉼 (CB:124~134 · CB:155)
  · MetaSession이 모듈 BeforeAwayApplied를 AddOfflineTime보다 먼저 부름 → 그 비운 시간이 24시간 상한으로 들어간다 (MS:213~214)
  · GrantBonusCap: min(시간, 72) × 3600초, 기존 값보다 클 때만 (IC:312~322). 받기 때 0 (IC:433)
튜토리얼 한 번짜리 채움: 바구니 개방 단계(action PrefillIdleChest, actionArg 2) → Prefill(2) (FTUE:305 · META:1951~1952)
  → lastClaimUnix를 min(상한, 2시간)만큼 앞당기고 offlineSeconds ≥ 그 초, MetaFlags.IdleChestPrefilled로 한 번만 (IC:592~605)
```

**D. 수령 (받기 · 두 배)**
```
IF 잠김 또는 되돌림(IsTampered) → CanClaim false (IC:391)
ELSE IF 지금 받을 단추 줄 ≥ 1 → 받기 가능 (IC:392)
ELSE IF 쌓인 초 ≥ minClaimSeconds(60) 이고 1개 이상이 되는 줄이 있음 → 가능 (IC:393~395)
ELSE → 불가 (빈 받기로 "바구니 받기" 카운터를 올릴 수 없음)
Claim(doubled) (IC:403~445)
  IF doubled 이고 오늘 무료 두 배 남음 (freeDoublePerDay > 0 · 되돌림 아님 · doubleClaimedDayKey ≠ 오늘) → ×2, 오늘 날 키 기록 (IC:416~420 · IC:450~458)
  ELSE IF doubled 이고 두 배 수선권 ≥ 1 → 1장 쓰고 ×2 (IC:421~425)
  ELSE → ×1 (Doubled = false)
  → MetaRewardService.Grant(목록, 배율, doubleExcludesGem) — 별조각은 ×1, 재료·단추·별빛 사탕은 ×배율 (IC:437 · MRS:56~93)
  → lastClaimUnix = 유효 시각, offlineSeconds = 0, bonusCapSeconds = 0 (IC:431~433). 상한을 넘은 시간은 이어지지 않는다
  → CounterKey.ChestClaim +1 (IC:440). 받은 직후 따로 저장하지 않음 — 자동 저장 · 일시정지 · 종료 저장에 실린다
복귀 화면: 비운 시간 > 0 · ≥ returnPopupMinSeconds(600) · CanClaim → PopupQueue에 Return(비운 초) (MS:641 · MS:651~658)
```

**E. 빠른 수선**
```
IF `FeatureId.QuickIdle` 잠김 (1-5 클리어 — META:190~198) → 불가 (IC:506~510)
ELSE IF 빠른 수선권 사용 → 수선권 ≥ 1이면 가능. 하루 횟수 · 되돌림을 보지 않음 (IC:511~516 · IC:555~559)
ELSE IF 오늘 쓴 횟수 ≥ quickIdleGemCosts 길이(3) → "오늘 빠른 수선을 모두 썼어요" (IC:517~522)
ELSE IF 이번 비용 0(무료) 이고 되돌림 → DailyClock.TamperedMessage (IC:523~527)
ELSE IF 비용 > 0 이고 별조각 부족 → "별조각이 부족해요" (IC:528~532)
ELSE → 별조각 차감 (1번째 0 · 2번째 50 · 3번째 100), quickDayKey = 오늘, quickUsedToday + 1 (IC:562~571)
처리 (IC:574~586)
  → 모든 줄 quickIdleHours(2)시간치 + 단추 2시간 × 초당 단추 × 0.5. 밤 덤 · 이벤트 시간 = 0 (이벤트 전용 줄은 0개)
  → 배율 1, carry는 바구니와 같이 씀, lastClaimUnix · offlineSeconds는 그대로 (바구니 타이머 안 바뀜). CounterKey.QuickIdle +1
"오늘" = DailyClock 날 키. quickDayKey ≠ 오늘이면 0회로 본다 (IC:469~477)
```

**F. 챕터 상자 · 잃어버린 상자**
```
[챕터 상자] BattleSystem.StageEnded 구독 (CC:140~145 · CC:168~178)
IF 결과 ≠ Cleared 또는 스테이지 % 10 ≠ 0 → 없음
ELSE (chapter = 스테이지 / 10)
  IF chapterChests.opened에 있음 또는 내용 없음 → 없음
  ELSE → PopupQueue.Enqueue(ChapterChest(chapter)). 첫 클리어가 아닌 다시 클리어도 같음. 큐는 같은 요청을 합친다 (PQ:91~103)
내용 = GetChapterChest(chapter): 그 장 줄 → 없으면 chapter 0 줄 → 없으면 상자 없음 (CC:199~204 · MDB:1022~1027)
OpenChapterChest (CC:114~126): 안 열었고 내용 있으면 opened에 넣고 ×1 지급 (장마다 한 번)
  · 시스템에는 되돌림 확인이 없다. 팝업은 되돌림이면 열지 않고 닫으며 상자는 남는다 (CCP:133~137 · CCP:146 · CCP:170). 그 밖에는 [받기] · X/뒤로/어둠 · 다른 길로 닫힘 모두 연다 (CCP:126~157 · CCP:167~185)
  · 지도: 보스 칸 클리어 + 미개봉이면 상자 버튼이 상자 창을 연다 (CMP:168~169 · CMP:396~404)
옛 세이브 (S-3): `chapter_chest.quest22_checked` 표시가 없고 1장 상자 줄이 있으면 표시를 세우고, 가이드 퀘스트 index ≥ 22이면 1장을 연 것으로 적음 (CC:185~196). 시작(AfterStartup) · 1-10 클리어 · 열기 앞에서 부른다
[잃어버린 상자] TryRollLostChest(stage, rewards) (CC:78~111) — BattleSystem.ClearDropHook 모양
IF 튜토리얼 미완료 · dropChance ≤ 0 · 그 스테이지 표 없음 · 가중치 합 0 → false (기본 재료 굴림)
ELSE IF 난수 ≥ dropChance → true (재료 없음, 기본 굴림도 건너뜀) (CC:94)
ELSE → 가중치로 한 칸, 개수 = min~max 균등 → true (CC:95~110)
지금: ClearDropHook에 넣는 코드 없음 (선언 BS:287, null 대입만 BS:757) + 에셋 dropChance 0 (META:4117)
  → 클리어 재료는 기본 굴림: 수선 천 1~2개 35% · 리본 조각 1개 25% (BS:643 · BS:762~766)
```

**G. 시간 처리 · 되돌림 (`DailyClock` · `MetaSession`)**
```
DailyClock.Refresh — 부팅·복귀에서 직접, 실행 중 1초마다 Tick. 일시정지 중에는 Tick 건너뜀 (DC:182~238 · MS:237)
  → 되돌림(IsTampered) = maxSeenUnix > 0 이고 기기 시각 < maxSeenUnix − 300초 (DC:90~97 · DC:186)
  → 기기 시각 > maxSeenUnix 이면 maxSeenUnix = 기기 시각. 내려가지 않는다 (DC:188)
  → 유효 시각 EffectiveUnix = max(기기 시각, maxSeenUnix) — 바구니 · 날 키 · 세션 끝 기록이 이 값을 쓴다 (DC:87 · IC:146)
  → 날 키 = (현지 유효 시각 − 5시간)의 날짜 "yyyy-MM-dd". 저장된 lastDayKey보다 이르면 그 날에 머문다 (DC:300 · DC:315~332)
  → 주 키 = 날 키가 속한 주 월요일의 날 키 (월요일 05:00 시작), lastWeekKey보다 이르지 않다 (DC:302~306 · DC:337~357)
  → 되돌림 상태 변화 → TamperChanged, 날·주 변화 → DayChanged · WeekChanged (DC:196~226). 시간대 = 기기 현지 (DC:73)
비운 시간 (MS:621~627) — Start · OnResume에서 Refresh 전에 maxSeenUnix를 읽어 둔다 (MS:203 · MS:282)
IF 되돌림 → 0
ELSE IF last = max(lastSessionEndUnix, PlayerData.lastSaveUnix, 읽어 둔 maxSeenUnix) ≤ 0 → 0
ELSE → max(0, 기기 시각 − last)
  → 모듈 BeforeAwayApplied → (> 0이면) AddOfflineTime → ConsumeAwayGap: lastSessionEndUnix = 유효 시각 (MS:211~215 · MS:284~288 · MS:633~637)
세션 끝: OnPause · OnQuit → lastSessionEndUnix = 유효 시각 (MS:265~269 · MS:312 · MS:682~686). 저장 → lastSaveUnix = 기기 시각 (GB:245 · GB:411)
```

**H. 상황별 동작** (위 규칙을 상황에 대입한 것)

| 상황 | 코드 동작 |
|---|---|
| 첫 설치 | 새 세이브는 `lastSaveUnix` 0 · `meta.clock` 0 (`PD:59~65`). Refresh가 maxSeen = 지금, 설치 날 기록 (`DC:188~194`). last = 0 → 비운 시간 0, 복귀 화면 없음 (`MS:625`). 바구니는 1-2 클리어 개방 때 시작하고 튜토리얼이 2시간을 미리 채움 (1.1-B · C). 세이브는 기기 로컬 `player.json` (`GB:90`) — 지우고 다시 깔면 같은 처리 |
| 시계 되돌림 (> 300초) | 되돌림 상태. 유효 시각 = maxSeen에 고정 → 바구니 쌓인 시간 멈춤. 비운 시간 0. 받기 · 무료 두 배 · 무료 빠른 수선 · 빨간 점 꺼짐 (`IC:203` · `IC:391` · `IC:454` · `IC:523`). 빠른 수선권 · 별조각 빠른 수선은 막지 않음 (`IC:511~533`). 날 키는 lastDayKey에 머묾. 기기 시각이 maxSeen − 300초를 넘으면 풀림 |
| 시계 되돌림 (≤ 300초) | 되돌림 아님. 유효 시각 = maxSeen이라 바구니 시간 줄지 않음. 비운 시간 = max(0, 기기 − last) → 0 |
| 되돌린 채 종료 → 시계 복구 후 접속 | 세션 끝 = 유효 시각(maxSeen), last에 maxSeen 포함 → 비운 시간 = 지금 − maxSeen (`MS:621~627` · `MS:682~686`). 되돌린 폭은 비운 시간에 들어가지 않음 |
| 시계 앞당김 | 감지 없음. maxSeen이 앞당긴 시각으로 올라감. 비운 시간 · 바구니 쌓인 시간은 상한(8/12/16시간, 다시 만난 날 24시간)까지 (`IC:335` · `IC:710`). 날 키가 넘어가면 빠른 수선 횟수 · 무료 두 배가 새 날로 (`IC:475` · `IC:456`) |
| 앞당긴 뒤 원래대로 | 기기 < maxSeen − 300초 → 되돌림 상태. 실제 시각이 앞당겼던 시각에 닿을 때까지 위 "되돌림" 동작 |
| 시간대 변경 | 바구니 · 비운 시간은 유닉스 초 기준이라 영향 없음. 날 키는 새 현지 시각으로 계산하되 저장된 날보다 이르면 머묾 (`DC:315~332`) |
| 앱 강제 종료 (일시정지 콜백 뒤) | `OnApplicationPause(true)`가 세션 끝 기록 후 저장 (`GB:273~276`). 다음 부팅 비운 시간 = 지금 − 그 시각 |
| 앱 강제 종료 (콜백 없이) | 디스크 세이브 = 마지막 자동 저장(30초, `GCA:28`) 또는 일시정지 저장. 그 뒤 메모리 변화(받기 · 빠른 수선 · 지급 · 상자 열기)는 함께 사라짐. 그 세이브의 lastSaveUnix · maxSeen 이후 시간이 비운 시간이 됨. 팝업 큐는 저장하지 않는 메모리 목록 (`PQ:78`) — 못 연 챕터 상자는 미개봉으로 남음 |
| 메타 꺼짐 경로 | `ORS`는 기기 시각만 씀. 되돌림 → 경과 < 60초 → 없음. 앞당김 → 8시간까지 지급. 일시정지 때 남은 보상을 먼저 지급하고 저장 (`GB:275`) |

### 1.2 상태 전이
```
[바구니] 수선 바구니 (IdleChestSave)
[잠김 lastClaimUnix=0] ──(1-2 클리어 → IdleChest 개방)──→ [쌓는 중] ──(쌓인 초 ≥ 상한)──→ [가득 IsFull, 넘친 시간 버림]
[쌓는 중 / 가득] ──(Claim)──→ [쌓는 중: lastClaimUnix=유효 시각 · offlineSeconds=0 · bonusCapSeconds=0]
[쌓는 중 / 가득] ──(빠른 수선)──→ [같은 상태: 보상만 지급]
[쌓는 중 / 가득] ──(되돌림 감지)──→ [멈춤: 유효 시각 고정 · 받기 불가] ──(기기 시각 ≥ maxSeen − 300)──→ [쌓는 중 / 가득]
[상한 = 단계 상한] ──(다시 만난 날 GrantBonusCap 24)──→ [상한 = 24시간] ──(Claim)──→ [상한 = 단계 상한]
[챕터 상자] (장마다)
[미개봉] ──(x-10 클리어)──→ [팝업 대기 (메모리 큐, 스테이지 사이)] ──(받기 · 닫기, 되돌림 아님)──→ [열림 (opened, 저장)]
[팝업 대기] ──(되돌림 중 닫음 / 앱 종료)──→ [미개봉] ──(지도 상자 버튼 · x-10 다시 클리어)──→ [팝업 대기]
[시계] DailyClock
[정상] ──(Refresh: 기기 < maxSeen − 300)──→ [되돌림 IsTampered] ──(Refresh: 기기 ≥ maxSeen − 300)──→ [정상]
[날 D] ──(유효 시각이 D+1일 05:00 도달 → Refresh)──→ [날 D+1]   (거꾸로 가지 않음)
```

### 1.3 수치 (코드 상수 · 설정값)

에셋 값 = 런타임 값. 괄호 안 "코드"는 코드 기본값이 다를 때만 적었다.

| 이름 | 값 | 위치 (파일:줄) | 의미 |
|---|---|---|---|
| `IdleChestConfig.baseCapHours` | 8 | `META:1584` · `MDB:263` | 기본 상한 (시간) |
| `capSteps[0]` | stageCleared 11 · shopLevel 0 → 12 (코드: 빈 목록) | `META:1586~1588` | 2-1 클리어 상한 |
| `capSteps[1]` | stageCleared 11 · shopLevel 200 → 16 | `META:1589~1591` | 2-1 클리어 + 수선점 Lv.200 상한 |
| `stageGrowth` | 1.0025 | `META:1665` · `MDB:270` | 최고 스테이지 1당 시간당 양 배율 (복리) |
| `offlineGoldRate` | 0.5 | `META:1666` · `MDB:272` | 단추 줄 = 초당 단추 × 비운 초 × 이 값 |
| `minClaimSeconds` | 60 | `META:1667` · `MDB:274` | 이보다 적게 쌓이면 재료 줄로는 못 받음 |
| `artStageThresholds` | 0.25 / 0.6 / 1 | `META:1668~1671` · `MDB:278` | 채움 비율 → 바구니 그림 0~3단계 (`IC:179~193`) |
| `redDotHours` | 1 | `META:1672` · `MDB:280` | 1시간 이상 쌓이거나 가득이면 빨간 점 (`IC:199~206`) |
| `quickIdleHours` | 2 | `META:1673` · `MDB:284` | 빠른 수선 1번 = 2시간치 |
| `quickIdleGemCosts` | 0 / 50 / 100 | `META:1674` · `MDB:286` | n번째 별조각 비용. 길이 3 = 하루 최대 3번 |
| `maxQuickIdleTickets` | 20 | `META:1675` · `MDB:288` · `MRS:312` | 빠른 수선권 보관 상한 |
| `returnPopupMinSeconds` | 600 | `META:1676` · `MDB:292` · `MS:639` | 복귀 화면 최소 비운 초 (설정 없으면 600) |
| `freeDoublePerDay` | 1 | `META:1677` · `MDB:294` | 1 이상이면 하루 1번 (저장 칸이 날 키 하나, `IC:454~456`) |
| `doubleExcludesGem` | true | `META:1678` · `MDB:296` | 두 배에서 별조각 제외 |
| 줄: 별조각 | 3/시간 · minStage 1 · maxStageMultiplier 1.5 | `META:1593~1600` | 최대 4.5/시간 (최고 스테이지 164부터 — 1.0025^163 > 1.5, 계산) |
| 줄: 리본 조각 `mat_ribbon` | 2 · minStage 1 | `META:1601~1608` | 시간당 양 = 2 × 스테이지 배율 |
| 줄: 수선 천 `mat_cloth` | 1.5 · minStage 1 | `META:1609~1616` | |
| 줄: 레이스 조각 `mat_lace` | 1 · minStage 3 | `META:1617~1624` | |
| 줄: 촛농 방울 `mat_wax` | 1 · minStage 5 | `META:1625~1632` | |
| 줄: 기억의 실 `mat_thread` | 0.15 · minStage 10 | `META:1633~1640` | carry로 2시간 1개 미만도 이어 쌓음 |
| 줄: 우산살 `mat_rib` | 0.5 · minStage 11 | `META:1641~1648` | |
| 줄: 오르골 태엽 `mat_spring` | 0.05 · minStage 25 | `META:1649~1656` | |
| 줄: 별빛 사탕 | 2 · minStage 1 · eventOnly | `META:1657~1664` | 이벤트와 겹친 시간만 쌓임 |
| `MaxBonusCapHours` · `GoldCarryKey` | 72 · `"offline:Gold"` | `IC:77` · `IC:75` | 한 번짜리 상한의 최대 (시간) · 단추 줄 carry 키 |
| 다시 만난 날 | minAwaySeconds 259200 · bonusCapHours 24 · lifetimeMax 6 · cooldownDays 14 | `META:3845~3848` · `MDB:548~554` | 한 번짜리 바구니 상한 조건 · 값 |
| 튜토리얼 미리 채움 | 2시간 (actionArg, 없으면 2) | `META:1951~1952` · `FTUE:305` | Prefill 시간 |
| 기능 개방 IdleChest / QuickIdle | StageCleared 2 / 5 | `META:92~100` · `META:190~198` | 1-2 / 1-5 클리어 |
| `ResetHour` · `RollbackToleranceSeconds` · `RefreshInterval` | 5 · 300 · 1초 | `DC:26` · `DC:28` · `DC:30` | 하루 초기화 현지 05:00 · 이만큼(초) 뒤로 간 것은 되돌림 아님 · Tick → Refresh 간격 |
| `TamperedMessage` | "기기 시간이 이전으로 바뀌었어요. 시간이 다시 맞으면 받을 수 있어요." | `DC:32` | 되돌림 토스트 |
| `QuickIdleMessage` | "친구들이 밤새 할 일을 지금 바로 끝냈어요!" | `IC:73` | 빠른 수선 문구 |
| `offlineMaxHours` · `offlineRewardRate` · `offlineMinSeconds` | 8 · 0.5 · 60 | `GCA:24~26` · `GC:30~33` | `ORS` 값. `IC`는 MetaDatabase가 없을 때만 대체 값으로 읽음 (`IC:131~139`) |
| `autoSaveInterval` | 30초 | `GCA:28` · `GC:80` | 자동 저장 간격 |
| `lostChest.dropChance` | 0 (코드 0.6) | `META:4117` · `MDB:617` | 잃어버린 상자 확률 |
| 잃어버린 상자 표 1~10 | 수선 천 1~2 (40) · 리본 조각 1~2 (35) · 촛농 방울 1 (15) · 레이스 조각 1 (10) | `META:4119~4137` | 개수 (가중치). 칸 확률 = dropChance × 가중치 / 합 |
| 잃어버린 상자 표 11~20 | 수선 천 1~2 (30) · 리본 조각 1~2 (20) · 촛농 방울 1~2 (15) · 레이스 조각 1 (15) · 우산살 1 (15) · 기억의 실 1 (5) | `META:4138~4164` | |
| 잃어버린 상자 표 21~ | 수선 천 2~3 (25) · 레이스 조각 1~2 (20) · 우산살 1~2 (20) · 촛농 방울 1~2 (15) · 기억의 실 1 (15) · 오르골 태엽 1 (5) | `META:4165~4191` | toStage 2147483647 |
| 기본 클리어 재료 굴림 | 수선 천 1~2개 0.35 · 리본 조각 1개 0.25 | `BCA:392~393` · `BS:764~765` | 지금 쓰이는 클리어 재료 |
| 챕터 상자 1장 | 별조각 100 · 수선 천 10 · 리본 조각 10 · 기억의 실 2 | `META:4193~4210` | |
| 챕터 상자 2장 | 별조각 100 · 우산살 6 · 레이스 조각 6 · 기억의 실 3 | `META:4211~4228` | |
| 챕터 상자 3장 | 별조각 120 · 오르골 태엽 2 · 기억의 실 4 · 달빛 조각 1 | `META:4229~4246` | |
| 챕터 상자 4장 | 별조각 120 · 기억의 실 5 · 레이스 조각 10 · 달빛 조각 1 | `META:4247~4264` | |
| 챕터 상자 5장 | 별조각 150 · 오르골 태엽 3 · 기억의 실 6 · 달빛 조각 1 | `META:4265~4282` | |
| 챕터 상자 10 · 15 · … · 200장 (39개) | 별조각 100 · 기억의 실 6 · 오르골 태엽 2 · 달빛 조각 1 | `META:4283~4984` | 5의 배수 장 |
| 챕터 상자 chapter 0 | 기억의 실 6 · 오르골 태엽 2 · 달빛 조각 1 (별조각 없음) | `META:4985~4998` | 줄 없는 장 (6~9장, 11~14장 …, 201장 이상) |

### 1.4 데이터

세이브 (`PlayerData` · `MetaSave`). 바구니 줄 · 상자 내용 같은 설정 데이터는 1.3 표.

| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `meta.idleChest.lastClaimUnix` | long | 마지막 받기 유효 시각. 0 = 아직 시작 안 함 (`SAVE:349`) | 1794366000 |
| `meta.idleChest.offlineSeconds` | double | 단추 줄용 앱을 닫아 둔 초 합계, 상한까지 (`SAVE:351`) | 28800 |
| `meta.idleChest.carry` | `Dictionary<string, double>` | 줄 키 → 받고 남은 소수 0~1 (`SAVE:353`, 키 = `MDB:242~244`) | `"item:mat_thread": 0.2`, `"offline:Gold": 0.4` |
| `meta.idleChest.quickUsedToday` · `quickDayKey` | int · string | 그 날 쓴 빠른 수선 횟수와 날 키 (`SAVE:355~356`) | 2 · "2026-11-11" |
| `meta.idleChest.bonusCapSeconds` | long | 다시 만난 날 한 번짜리 상한 (초), 0 = 없음 (`SAVE:358`) | 86400 |
| `meta.returnScreen.doubleClaimedDayKey` | string | 무료 두 배를 쓴 날 키 (`SAVE:417`) | "2026-11-11" |
| `meta.returnScreen.lastAwaySeconds` | long | 마지막 부팅 · 복귀의 비운 초 (`SAVE:419`, `MS:212`) | 3600 |
| `meta.returnScreen.lastShownUnix` | long | 복귀 화면을 넣은 기기 시각 (`SAVE:421`, `MS:657`) | 1794366000 |
| `meta.clock.maxSeenUnix` | long | 본 적 있는 가장 늦은 기기 시각 (`SAVE:202`) | 1794366000 |
| `meta.clock.lastSessionEndUnix` | long | 세션 끝 · 공백 소비 시각 = 유효 시각 (`SAVE:204`) | 1794365400 |
| `meta.clock.lastDayKey` · `lastWeekKey` | string · string | 마지막 날 키 · 주 키 (날 · 주가 거꾸로 가지 않는 바닥) (`SAVE:198~200`) | "2026-11-11" · "2026-11-09" |
| `meta.clock.installDayKey` · `installUnix` | string · long | 첫 실행 날 키 · 시각 (`SAVE:206~208`) | "2026-11-01" |
| `PlayerData.lastSaveUnix` | long | 저장 때 기기 시각 (`PD:40`, `GB:245` · `GB:411`) | 1794365980 |
| `meta.chapterChests.opened` | `HashSet<int>` | 연 챕터 상자 장 번호 (`SAVE:686`) | [1, 2] |
| `PlayerData.flags` 원소 | string | S-3 확인 표시 `chapter_chest.quest22_checked` (`CC:30`) | |
| `meta.wallet.quickIdleTickets` · `doubleTickets` | int · int | 빠른 수선권 · 두 배 수선권 (`MRS:307~308`) | 3 · 1 |

### 1.5 추출 못 한 것
- 초당 단추(`GrowthSystem.GoldPerSecond`)의 `GoldBonusMultiplier` · `StageGoldMultiplier` 세부는 읽지 않았다 → 단추 줄의 실제 시간당 양 숫자 없음.
- 밤의 수선점 · '별이 머문 밤 산책' 일정과 배율 (`EVT`가 넣는 `NightBonusHours` · `NightBonusMultiplier` · `EventLineHours` 훅의 내용) → 밤 덤 · 별빛 사탕 줄이 언제 얼마나 붙는지 미기재.
- 복귀 화면(`ReturnPopup`) · 바구니 창(`IdleChestPopup`)의 UI 흐름, 두 배 버튼 노출, 짧은 복귀 토스트("단추 1,800 모였어요")는 확인하지 않았다.
- `PopupQueue` 접속 묶음 최대 3개(`PQ:73`)에서 복귀 화면 · 챕터 상자가 밀리거나 버려지는 경우의 순서 규칙 상세.
- 앱을 강제로 닫을 때 OS · Unity가 `OnApplicationPause(true)` / `OnApplicationQuit`를 먼저 부르는지는 플랫폼 동작이라 코드로 확인할 수 없다.
- 옛 세이브 마이그레이션이 `idleChest` · `clock` 칸을 어떻게 채우는지, 알림(`NotificationPlanner`)의 바구니 관련 예약은 읽지 않았다.
- `RunMeta`가 잡는 `NotImplementedException`을 Unity · 외부 라이브러리가 던지는 경우가 있는지 (Assets 안 C#에는 던지는 곳 없음).
- 테스트(`Tests/EditMode/NoticeAndOfflineTests.cs` 등)는 실행하지 않았다. 위 동작은 코드 읽기로만 확인했다.

## 2. 기획 방향 (To-be) — DD

> 기준 문서: [`SYS_InGameBattle`](./SYS_InGameBattle.md) §2.3(방치 파밍 = 오프라인 자동 · 저효율 · 패배 없음) · §14-5(오프라인 판정 = **시뮬 아닌 기대값 산식 + 서버 타임스탬프 검증** 권장) · D-28 #9(메인 = 자동 진행 루프, 오프라인 = 이 문서가 정본) · [`UI_InGame`](../03_UI_UX/UI_InGame.md) HM-001(오프라인 보상 = 홈 진입 직후 1회 팝업) · [`SYS_Economy`](./SYS_Economy.md) D-30.
> 결론부터: **현재 구현(수선 바구니)이 기획 권장안(기대값 산식)을 이미 따르고 있다.** ②는 현재 구현을 정본으로 채택하고, 빠진 것(서버 시각 검증 · 앞당김 대응 · 죽은 코드 정리)만 더한다.

### 2.0 방치 보상 구성

| 구성 | 언제 쌓이나 | 내용 | 상한 | 정본 |
|---|---|---|---|---|
| **켜 둔 동안 단추** | 앱 실행 중 (전투 자동 진행과 함께) | 초당 단추 × 시간 (100%) | 없음 | 현재 구현 (`SYS_Growth` ①-C) |
| **수선 바구니** | 1-2 클리어부터, 켜 둔 동안 · 꺼 둔 동안 모두 (단추 줄만 꺼 둔 동안) | 단추(초당 단추 × 50%) · 재료 7종 · 별조각 · (이벤트) 별빛 사탕 — 최고 스테이지 배율 | 8 → 12 → 16시간 (다시 만난 날 24시간 1회) | 현재 구현 |
| **빠른 수선** | 1-5 클리어부터, 하루 3번 (0 · 50 · 100 별조각) 또는 수선권 | 바구니 2시간치 즉시 | 수선권 20장 | 현재 구현 |
| **두 배 받기** | 하루 1번 무료 또는 두 배 수선권 | 바구니 ×2 (별조각 제외) | — | 현재 구현 |
| **챕터 상자** | x-10 클리어, 장마다 1번 | 별조각 · 재료 | — | 현재 구현 (`CNT_Stage_Chapter1`과 연결) |
| ~~옛 오프라인 보상~~ · ~~잃어버린 상자~~ | 꺼져 있음 | — | — | §3 #3 · #4 |

### 2.1 규칙
```
[비운 시간 계산] — 현재 구현 + 서버 시각 검증(P1)
IF 온라인 AND 서버 시각 응답 (부팅 · 복귀 때 1회, 타임아웃 3초)
  → 기준 시각 = 서버 시각. 기기 시각과 차이 > 10분이면 "시계 불일치" 기록, 비운 시간 · 날 키는 서버 기준
ELSE (오프라인 · 응답 없음)
  → 기준 시각 = 현재 구현의 유효 시각 max(기기, maxSeen) + 되돌림 감지(300초)
  → 직전 서버 확인 이후 앞당긴 폭이 "바구니 상한"을 넘으면, 넘은 부분은 다음 서버 확인 때까지 보류 (지급 안 함, 버리지도 않음)
IF 되돌림 → 비운 시간 0, 받기 · 무료 두 배 · 무료 빠른 수선 막힘 (현재 구현과 같음)

[바구니 받기] 현재 구현 그대로 (②에서 바꾸지 않음)
IF 잠김 OR 되돌림 → 불가
ELSE IF 단추 줄 ≥ 1 OR (60초 이상 AND 1개 이상인 줄) → 받기 → 지급 후 **즉시 저장** (현재: 자동 저장 대기)

[복귀 화면] 비운 시간 ≥ 10분 AND 받기 가능 → 홈 진입 직후 1회 (UI HM-001과 일치)
```

### 2.2 상태 전이
```
[바구니 잠김] ─(1-2 클리어)→ [쌓는 중] ─(상한 도달)→ [가득] ─(받기)→ [쌓는 중]
                                 └────(되돌림)→ [멈춤] ─(시계 정상)→ [쌓는 중]
비운 시간 검증 (P1)
[기기 기준] ─(서버 응답)→ [서버 기준] ─(보류분 있음)→ [보류분 지급 or 폐기 판정] → [서버 기준]
```

### 2.3 UI 플로우 / 디스크립션
1. **홈 수선 바구니** (HM-001 시설 핫스팟) : 채움 비율 4단계 그림, 1시간 이상 · 가득이면 빨간 점 → 탭 → ②
2. **바구니 팝업** : 쌓인 시간 / 상한 바, 줄별 예상 수량, `[받기] [두 배 받기 (오늘 무료 1)]`, `[빠른 수선 (0 / 50 / 100)]` → 받기 → 보상 연출 → 닫기
3. **복귀 화면** : "○시간 ○분 동안 친구들이 수선점을 지켰어요" + 바구니 요약 → `[받기] [두 배]`
4. **되돌림 상태** : 받기 버튼 비활성 + `DailyClock.TamperedMessage` 토스트 (현재 문구 유지)
5. (P1) **시계 불일치 안내** : 서버 기준으로 계산했다는 1줄 안내 (보류분 있으면 수량 표시)

### 2.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `IdleChest_Config.base_cap_hours` · `cap_steps[]` | int · struct[] | 현재 `IdleChestConfig` 그대로 (상한 단계) | 8 · `{11, 0, 12}` |
| `IdleChest_Line.line_id` · `reward` · `per_hour` · `min_stage` · `max_stage_mult` · `event_only` | string · struct · double · int · double · bool | 현재 바구니 줄 그대로 | `mat_rib` · 0.5 · 11 · 0 · false |
| `IdleChest_Line.lineage_bonus_id` | string (FK → Character_Base, nullable) | **신규 · 💡 제안** — 편성에 이 계보가 있으면 이 줄 +% | `rainy` |
| `meta.idleChest.*` (세이브) | — | 현재 구현 그대로 (`lastClaimUnix` · `offlineSeconds` · `carry` · …) | — |
| `meta.clock.lastServerUnix` · `serverOffsetSec` | long · long | **신규 (P1)** — 마지막 서버 확인 시각 · 기기와의 차이 | 1794366000 · −12 |
| `meta.clock.heldSeconds` | double | **신규 (P1)** — 앞당김 보류분 | 0 |

## 3. 차이 → 결정 필요 — DD 작성, 대표 결정

| # | 항목 | ① 현재 구현 | ② 기획 방향 | 영향 (코드 · 데이터 · 세이브) | DD 추천 | 결정 |
|---|---|---|---|---|---|---|
| 1 | 오프라인 보상 방식 | 수선 바구니: 시간 × 줄별 기대값(최고 스테이지 배율), 단추 50% | "시뮬 아닌 기대값 산식"(§14-5), "최고 스테이지 순회 · 저효율 · 패배 없음"(§2.3) | 문서 | **현재 구현을 정본으로 채택** — 기획 권장안과 같은 방식. §2.3 "방치 파밍" = 수선 바구니로 표기 통일 | **D-31** DD 추천안 |
| 2 | 시계 검증 | 기기 시계만. 되돌림(300초 초과)은 감지 · 차단, **앞당김은 감지 없음** → 시계를 계속 앞으로 돌리면 바구니(상한까지) · 날 바뀜 보상(무료 빠른 수선 · 무료 두 배 · 출석 등)을 반복 획득 | 서버 타임스탬프 검증 (§14-5) | 코드(`DailyClock` · `MetaSession`) · 세이브 필드 추가 · 네트워크 1회 호출 | **채택, 출시 전 P1.** M1(프로토)은 현재 방어로 충분. 랭킹 없는 싱글(P6)이라 피해는 본인 경제지만, 별조각 · 출석이 걸려 있어 **BM을 붙이기 전엔 필수**. 오프라인일 때는 막지 않고 "보류"만 (P6) | **D-31** DD 추천안 |
| 3 | 옛 오프라인 보상 (`OfflineRewardSystem`) | 메타가 꺼졌을 때만 쓰는 예비 경로, 정상 빌드에선 안 쓰임 | 없음 | 코드 정리 | **제거 (P2).** 경로가 둘이면 버그 · 어뷰징 표면만 늘어남. `IdleChest`가 `GameConfig` 값으로 대체하는 부분만 확인 후 정리 | **D-31** DD 추천안 |
| 4 | 잃어버린 상자 | 확률 0 · 훅 미연결로 꺼짐 (코드 · 표는 있음) | 없음 | 콘텐츠 | **꺼진 채 보관.** 스테이지 클리어 재료 굴림을 대체하는 장치라, 켜려면 `CNT_Stage_Chapter1` 재료 설계와 같이 결정 | **D-31** DD 추천안 |
| 5 | 받기 후 저장 | 받은 직후 저장하지 않음 (자동 저장 30초 · 일시정지 때) | 별조각을 쓰는 행동(빠른 수선) · 받기 직후 즉시 저장 | 코드 | **채택 (P2).** 지금도 중복 지급은 없지만(받기 · 타이머가 함께 되돌아감), 별조각 소비가 사라지는 경우를 막음 | **D-31** DD 추천안 |
| 6 | 상한 · 빠른 수선 · 두 배 · 다시 만난 날 | 8/12/16h · 하루 3번(0/50/100) · 하루 무료 1번 · 3일 이상 비우면 24h 1회 | 기획서에 없음 | 문서 | **현재 구현을 정본으로 채택.** 상한 확장은 P1 `SYS_Atelier` 시설 축(빛의 반경 등)과 연결 검토 | **D-31** DD 추천안 |

> 💡 **조합 아이디어 (참고)** — 수선 바구니 재료 줄에 **"편성 계보 보너스"** 를 붙이면: 우산살이 필요할 때 레이니를 편성해 두고 자러 가는 식으로, **방치 효율이 편성 판단과 이어진다.** `Item_Master.lineage_hint`(D-30 데이터)와 같은 키라 데이터 추가만으로 가능. 단, 전투 최적 편성과 충돌하면 피로가 생기니 보너스는 작게(+20%) · 지킴이 · 잔향이 어느 쪽이든 인정.

## 4. 예외 처리 / 엣지 케이스
| 케이스 | 처리 |
|---|---|
| 네트워크 단절 | 기기 시각 + 되돌림 감지 (현재). P1: 앞당김 초과분은 보류 |
| 앱 강제 종료 | 마지막 저장 이후 받기 · 빠른 수선은 함께 사라짐 (중복 없음). §3 #5로 별조각 소비 유실 방지 |
| 기기 시간 조작 | 되돌림: 막힘 (현재). 앞당김: §3 #2 |
| 시간대 변경 · 서머타임 | 바구니는 유닉스 초라 영향 없음, 날 키는 저장된 날보다 이르면 머묾 (현재) |
| 재설치 (로컬 세이브 삭제) | 새 게임 (클라우드 저장은 `SYS_Save`) |
| 상한 초과 방치 | 넘친 시간 버림 (현재). 빨간 점으로 "가득" 안내 |

## 5. 어뷰징 검토
| 시나리오 | 대응 |
|---|---|
| 시계 앞당김 반복 → 바구니 · 일일 보상 무한 | §3 #2 서버 시각 (P1), 그 전엔 상한이 1회 이득을 제한 |
| 시계 되돌림 → 날 키 되돌려 일일 보상 재획득 | 현재 차단 (날 키는 뒤로 가지 않음 · 되돌림 시 무료 행동 막힘) |
| 받기 직후 강제 종료로 보상만 남기기 | 불가 — 보상 · 타이머가 같은 세이브 단위 (§4) |
| 빠른 수선권 대량 보유 | 보관 상한 20 (현재) |

## 6. 확장성 / 오픈 이슈
- [ ] 서버 시각 소스 선정 (자체 API vs 공용 시간 API) — 비용 0 원칙(D-24 · 계명 10)
- [ ] 상한 확장을 `SYS_Atelier` 시설 축과 연결할지
- [ ] 💡 편성 계보 보너스 채택 여부
- 확장성: 바구니 줄 · 상한 단계가 이미 데이터(`MetaDatabase`)라 새 재료 · 이벤트 줄은 코드 수정 없이 추가
