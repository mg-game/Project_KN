# SYS_FTUE

| 항목 | 값 |
|---|---|
| 상태 | Approved (D-32) |
| 작성자 | ① 구현: 기개발 · ②③: DD |
| 최종 수정 | 2026-11-13 (상태 헤더 맞춤 — 검수 KN_DocReview) |
| 관련 테이블 | - |
| 관련 문서 | [`KN_Glossary`](../00_Overview/KN_Glossary.md) · [`SYS_InGameBattle`](./SYS_InGameBattle.md) |
| 근거 | 코드 커밋 `4b73a5a` (`feature/lost-and-lovely-import`) — 경로 약칭 표 참고 |

> 구조(D-26): **① 현재 구현** = 지금 코드가 실제로 하는 일 / **② 기획 방향** = 기획서가 원하는 모습 / **③ 차이** = 둘이 다른 곳, 대표가 결정.
> 결정이 나면 ②에 반영하고 ③ 행에 `D-n`을 적는다. 코드 수정은 지시함(#3)으로. 용어는 [`KN_Glossary`](../00_Overview/KN_Glossary.md)를 따른다.

## 0. 개요
- 목적: 설치 후 첫 5분 안에 **"지키고 · 부르고 · 키우고 · 맡겨 둔다"** 는 핵심 루프를 손으로 한 번씩 겪게 해 이탈을 막는다 (계명 3 첫 5분 후킹).
- 핵심 루프 내 위치: 첫 실행 → 1-1~1-5 (튜토리얼 17단계 + 기능 개방 27개 중 앞부분) → 졸업 → 자동 진행 루프
- 관련 필라: P1 짧지만 판단은 계속 · P2 전황 가독성 (한 번에 하나씩 가르침) · P6 혼자 자기 속도로 (입력을 거의 막지 않음 · 언제든 건너뛰기)

## 1. 현재 구현 (As-built) — 기개발

> 작성: 기개발 (2026-11-11). **근거: `feature/lost-and-lovely-import` @ `4b73a5a`**.
> 코드 · 데이터가 실제로 하는 일만 적었다. 해석 · 제안 · 기획과의 비교 판단은 넣지 않았다 (② · ③은 DD).
> 범위: 튜토리얼 `TutorialSystem`(FTUE-01) · 기능 개방 `FeatureGateSystem`(F-03) · `FeatureId` · `MetaDatabase.asset`의 `features` / `ftueSteps` · 첫 부팅 지급.
> 데이터 출처: 런타임은 `Scenes/Main.unity:361308`이 가리키는 `Data/MetaDatabase.asset`을 읽는다. 이 asset은 `Editor/Content/MetaContent*.cs`를 `MetaDataBuilder`가 구워 덮어쓴 결과다 (`MDB:771`). 아래 값은 asset 기준이고, 원본 코드와 다른 곳은 1.7에 적었다.
> 단계 번호는 코드 인덱스(0부터)다. 세이브 `meta.ftue.step` = 끝낸 단계 수 = 지금 단계 인덱스. 재화 · 전투 용어는 용어집 표기(단추 `CurrencyType.Gold` · 별조각 `CurrencyType.Gem` · 지킴이 `GuardianUnit` · 잔향이 `EchoUnit` · 망각체 · 수선점 내구 `ShopHp` · 계보 `Lineage` · 불 밝히기 게이지 `LampGauge` ⚠ 기획의 수선등 밝히기와 다른 기능 · 원정 `ExpeditionSystem` = 기획 "수선 의뢰(파견)" · 의상 `CostumeSystem` = 기획 "외형변경")를 쓴다. 데이터 문자열(기능 이름 · 말풍선)은 따옴표로 원문 그대로 둔다.

**파일 약칭** (모두 `Assets/_Project/` 아래. 약칭이 없는 파일은 `Scripts/` 아래 경로로 적는다)

| 약칭 | 경로 |
|---|---|
| `TUT` · `FG` | `Scripts/Systems/Meta/TutorialSystem.cs` · `FeatureGateSystem.cs` |
| `PT` · `MS` · `PQ` | `Scripts/Systems/Meta/ProgressTracker.cs` · `MetaSession.cs` · `PopupQueue.cs` |
| `GE` · `SVM` · `BOOT` | `Scripts/Core/GameEnums.cs` · `SaveData.Meta.cs` · `GameBootstrap.cs` |
| `MDB` · `GC` | `Scripts/Data/MetaDatabase.cs` · `GameConfig.cs` |
| `BS` · `SIM` · `BT` · `FORM` | `Scripts/Systems/Battle/BattleSystem.cs` · `BattleSimulator.cs` · `BattleTypes.cs` · `FormationSystem.cs` |
| `FD` · `FBG` | `Scripts/UI/Tutorial/FtueDirector.cs` · `FtueBattleGuard.cs` |
| `MPP` | `Scripts/UI/Meta/MetaPopupPresenter.cs` |
| `SET` | `Scripts/UI/Popups/SettingsPopup.cs` |
| `META` · `CDB` | `Data/MetaDatabase.asset` · `CharacterDatabase.asset` |
| `MC` | `Editor/Content/MetaContent.Features.cs` |

### 1.1 규칙

**A. 기능 개방 판정** — `FeatureGateSystem.IsUnlocked` / `Evaluate`
```
IF features에 그 기능 정의가 없음 또는 conditions가 비어 있음 → 열림 (FG:82-85)
ELSE IF 세이브 meta.features.unlocked에 있음 → 열림. 한 번 열린 기능을 닫는 코드는 없다 (FG:86)
ELSE → 잠김

Evaluate — Progress.Changed마다(FG:75 · FG:361), MetaSession.Start에서 1번(MS:208). features 순서대로, 새로 연 것이 없을 때까지 최대 8바퀴 (FG:245)
  IF conditions가 비어 있음 → unlocked + seen에 넣음. 카드 · 점 · 보상 없음 (FG:254-260)
  ELSE IF conditions 중 하나라도 충족 (OR, FG:262)
       AND alsoRequire 전부 충족 (AND. 옛 세이브 조용한 동기화 때는 안 봄, FG:264)
    → unlocked에 넣고 Announce (FG:266-278, FG:294)
Announce: showCard면 개방 카드를 PopupQueue에 1번만 → rewards 지급 → Unlocked 이벤트 (FG:413-421)
ForceUnlock(건너뛰기 · 개발자 메뉴): 조건 · alsoRequire를 보지 않고 연다 (FG:305-321, SET:853-861은 개발 빌드 전용)
```

**B. 스테이지 조건(`StageCleared`) 판정과 표기**
```
값 = ClearedStage = max(0, max(1, battle.highestStage) − 1)   (PT:543, PT:221-222)
IF 값 ≥ target → 충족   (= highestStage > target, GE:486)
target t → "챕터-번호": 챕터 = (t−1)/10 + 1, 번호 = (t−1)%10 + 1   (BS:127-133, 역함수 MDB:48)
예: target 4 → "1-4" 클리어 · target 11 → "2-1" 클리어 · target 19 → "2-9" 클리어
```

**C. 개방 카드 띄우기**
```
개방 카드(FeatureUnlock)는 스테이지 사이에만 큐에서 꺼낸다 (PQ:176, PQ:183-185)
IF 그 기능 화면에 이미 들어가 봄 (IsNew = false) → 카드 버림 (MPP:163, FG:205)
ELSE IF 마지막 카드를 닫은 뒤 앞에서 본 시간 < 180초 AND 마지막 카드와 다른 Evaluate 묶음
  → 대기 줄 (MPP:164-167, FG:196-202)
ELSE → 띄움. 닫히면 NotifyCardClosed → 시계 0 (MPP:422, FG:211-216)
시계 = 앱을 켜 둔 시간(일시정지 제외, 프레임당 최대 10초)을 더함. 상한 360초. 앱을 새로 켜면 180초에서 시작 (MS:586-588, FG:56, FG:184-189)
```

**D. 잠긴 기능 안내 문구**
```
IF 콘텐츠 lockedHint / lockedToast가 있음 → 그대로 씀 (FG:98, FG:123)
ELSE → 조건 중 StageCleared를 먼저, 없으면 첫 조건으로 만든다 (FG:424-436, FG:101-114, FG:126-146)
  StageCleared → "1-4" / "스테이지 1-4를 클리어하면 열려요" · FtueStep · FtueDone → "튜토리얼" / "튜토리얼을 마치면 열려요" 등
IF conditions는 충족했고 alsoRequire만 남음 → 작은 글자 없음, 문구 "조금만 더 수선점을 돌보면 열려요" (FG:97, FG:122, FG:41)
```

**E. 튜토리얼 단계 진행** — `TutorialSystem.Refresh(betweenStages)`
```
불리는 곳: Progress.Changed(MS:785) · Features.Changed(MS:811) · FtueDirector 0.25초마다(FD:529-534) · 스테이지 끝(FD:400) · Tick 0.5초 안전망(TUT:218-223)
IF done 또는 단계 데이터 없음 → 아무것도 안 함 (TUT:166, TUT:93)
반복 (한 번에 최대 64단계, TUT:170):
  IF 지금 단계가 아직 시작 전
    IF trigger 미충족 → 멈춤 (TUT:183). Immediate → 늘 충족 / BetweenStages → 스테이지 사이(BS:161)일 때 /
      FeatureUnlocked → triggerArg 기능이 열림(이름을 못 읽으면 바로 충족) / AfterDelay → 앞 단계 끝 + triggerArg초, 실제 시간 (TUT:353-367, TUT:200-216)
    ELSE → 시작: RunningFlag "ftue.running" 저장 → StepStarted (FtueDirector가 action 수행) (TUT:369-377)
  IF complete 조건 미충족 → 멈춤 (TUT:189)
  ELSE → 단계 보상 지급 → step + 1 → Progress.MarkDirty(기능 개방 재평가) (TUT:379-392)
    IF 마지막 단계였음 → done = true, 졸업 카드 FtueGraduation을 PopupQueue에 (TUT:391, TUT:395-407)
앱을 다시 켜면: RunningFlag가 남은 단계는 trigger를 다시 기다리지 않고 바로 다시 시작 (TUT:155-157)
complete가 평생 카운터(Counter)인 단계는 시작 전에 이미 채웠으면 같은 Refresh에서 시작과 함께 끝난다 (TUT:184-190, PT:215-216)
```

**F. 전투 보호** — `WantsBattleProtection`
```
WantsBattleProtection = 진행 중 AND step ≥ (action = BattleProtection인 첫 단계 = 1번) (TUT:111-138)
전투에 넣는 규칙 (MS:474-478, MS:467에서 연결. FBG:47-49도 같은 규칙):
IF 지금 스테이지 번호 ≤ 3 (1-1~1-3) AND (WantsBattleProtection OR (진행 중 AND step = 0))
  → BattleProtection.Tutorial: 새로 나오는 망각체 최대 체력 × 0.6 (보스 · 분열체 포함), 수선점 내구는 1 아래로 안 내려감 (BT:88, SIM:386-411)
ELSE → Off (BT:85)
메인 스테이지에만 걸린다 (SIM:408). BattleSystem이 매 Tick 다시 확인해 단계가 바뀌면 바로 따른다 (BS:379-384)
```

**G. 자동 전투(AUTO) 끄기 · 켜기 / H. 첫 불 밝히기** (`ForceLampFull` → `FillLamp`)
```
IF MetaSession.Start 때 새 튜토리얼 (단계 데이터 있음 AND done 아님 AND step = 0) → Battle.AutoMode = false (MS:222, MS:663-668)
8번 단계 시작 (EnableAutoBattle) → AutoMode = true (FD:301-303)
IF AutoBattle 기능이 새로 열림 AND 튜토리얼 진행 중 아님 → AutoMode = true (MS:819-823)
IF 건너뛰기로 끝남 AND AutoBattle 이미 열림 AND AutoMode 꺼짐 → AutoMode = true (FD:364)

H. 4번 단계(ForceLampFull) 진행 중이고 게이지가 아직 안 참:
IF Sim.Kills ≥ 8 OR 단계 시작 후 4초 → BattleSystem.ForceLampFull (FD:686-692)
  → 메인 스테이지 · 전투 진행 중일 때만 BattleSimulator.FillLamp: 불 밝히기 게이지(LampGauge) = 1 (BS:334-338, SIM:369-373)
게이지가 차기 전: 어둠 · 손가락 없이 테두리와 안내만, 찬 뒤 손가락 (FD:667-673)
```

**I. 건너뛰기** — `TutorialSystem.Skip` / **J. 옛 세이브**
```
설정 → "튜토리얼 건너뛰기" 확인 창 ('건너뛰기' / '계속 배우기') → Skip (SET:439-456)
IF 이미 done → 아무것도 안 함 (TUT:267)
ELSE
  → conditions에 FtueStep이 있는 잠긴 기능을 카드 없이 ForceUnlock. 지금 데이터 = Formation · Synergy (TUT:275-281)
  → 그중 "새로 열림" 점은 하나만 남김: 가이드 퀘스트의 이동 기능, 없거나 Battle이면 Gacha (TUT:283-287, TUT:312-316)
  → 마지막 단계 보상(별조각 300) 지급, 토스트 "튜토리얼을 건너뛰었어요 · 별조각 300을 받았어요" (TUT:81-82, TUT:290-292)
  → skipped = true, done = true. 졸업 카드 없음 (TUT:294, TUT:300, TUT:403)
건너뛴 뒤에도 첫 만남 확정 소환 카드는 소환이 열려 있으면 소환 시트 위에 뜬다 (FD:419, FD:521-526)
done이 되면 FtueStep · FtueDone 조건은 늘 충족 (PT:330-336) → Attendance(FtueDone 또는 1-5)도 이때 열림 조건을 채운다

J. 옛 세이브: IF 메타 기능 이전 세이브 → features.silentSyncPending = true, 진행이 있으면 ftue.done = skipped = true (SVM:182-189)
  첫 Evaluate: 이미 조건을 넘은 기능을 카드 · 점 · 보상 없이 열고 seen · cardShown 처리, alsoRequire 무시 → 플래그 끔 (FG:226-286)
```

**K. 첫 부팅 지급** — `GameBootstrap`
```
세이브가 없으면(또는 못 읽으면) PlayerData.CreateNew (BOOT:95)
IF 보유 계보 0개 → GameConfig.starterLineages 4종 지급 → 자동 편성 → 전투 다시 시작 (BOOT:183-189)
  = jjakjjak "짝짝이"(Epic) · ttokdan "똑단이"(Common) · chalkak "찰칵이"(Rare) · mongdang "몽당이"(Rare)
  (GC:46 — GameConfig.asset에 이 필드가 없어 코드 기본값. 이름 · 등급 CDB:16-30 · 939-952 · 1121-1133 · 1319-1332)
매 부팅 우편 "welcome"을 SendOnce: 별조각 1000 · 단추 500, 만료 없음 (BOOT:112-116)
```

**L. 튜토리얼이 끝나기 전에 꺼져 있는 것**
```
IF 튜토리얼 진행 중 (IsActive)
  → 메뉴 출석부 · 쿠폰 타일 숨김 (UI/Main/MenuPopup.cs:148, :157) · 시작 공지 안 띄움 (MPP:353)
  → 4차 팝업(다시 만난 날 · 이벤트 시작 · 챕터 상자 · 동화책 · 알림 묻기)은 대기 줄 (MPP:171-174, MPP:230-240)
  → 잠자기 모드 안 됨 (UI/Meta/SleepModeView.cs:195)
IF ftue.done = false
  → 원정 1 · 2단계 고르기 불가 (Systems/Battle/ExpeditionSystem.cs:92) · 쓰다듬기 불가 (Systems/Meta/AffinitySystem.cs:157-158)
  → 잃어버린 상자 굴림 없음 (Systems/Meta/ChapterChestSystem.cs:81-82) · 응원 불가 (Systems/Meta/CheerSystem.cs:74-75)
  → 다시 만난 날 시작 안 함 (Systems/Meta/ComebackSystem.cs:129) · 반딧불 안 뜸 (Systems/Meta/FireflyRules.cs:79)
  → 챕터 동화책은 미뤘다가 done 뒤 1초 단위로 확인해 요청 (Systems/Meta/StorySystem.cs:103-114, :131-135)
```

### 1.2 상태 전이

튜토리얼 (`FtueSave`, SVM:393-410)
```
[step=i · 대기] ──(trigger 충족)──→ [step=i · 진행, RunningFlag] ──(complete 충족 · 보상)──→ [step=i+1 · 대기]
[step=16 · 진행] ──(complete 충족 · 별조각 300)──→ [done=true] ──→ 졸업 카드 (스테이지 사이)
[아무 단계] ──(설정 · 건너뛰기)──→ [done=true · skipped=true] (졸업 카드 없음)
[옛 세이브 · 진행 있음] ──(MigrateFromLegacy)──→ [done=true · skipped=true]
[step ≥ 단계 수] ──(Begin · Refresh)──→ [done=true] (카드 없음, TUT:148-152 · TUT:173-177)
```

기능 (`FeatureSave`, SVM:372-389)
```
[잠김] ──(conditions 하나 + alsoRequire 전부)──→ [열림 · 새로 열림(점) · 카드 큐] ──(MarkSeen: 그 화면 들어감)──→ [열림 · 봤음]
[정의 없음 또는 conditions 빔] ──(첫 Evaluate)──→ [열림 · 봤음] (카드 · 점 없음)
[잠김] ──(ForceUnlock: 건너뛰기 · 개발자 메뉴)──→ [열림] (건너뛰기는 카드 없음) / 옛 세이브는 1.1-J
```

### 1.3 수치 (코드 상수 · 설정값)
| 이름 | 값 | 위치 (파일:줄) | 의미 |
|---|---|---|---|
| `RecheckInterval` | 0.5초 | TUT:38 | 튜토리얼 안전망 Refresh 간격 |
| `MaxStepsPerRefresh` | 64 | TUT:40 | Refresh 1번에 끝낼 수 있는 단계 수 상한 |
| `_pollInterval` | 0.25초 | FD:59 | FtueDirector가 Refresh(스테이지 사이 여부)를 부르는 간격 |
| `_lampKills` · `_lampSeconds` | 8 · 4초 | FD:63 · FD:65 | 첫 불 밝히기: 처치 수 또는 단계 시작 후 시간이 되면 게이지 가득 |
| `TutorialProtectionMaxStage` | 3 | MS:70 | 보호를 거는 마지막 메인 스테이지 (1-3). `FBG:20`도 같은 값 |
| `BattleProtection.Tutorial` | 체력 × 0.6 · 내구 바닥 1 | BT:88 | 튜토리얼 전투 보호 값. `FBG:18` · `FBG:22`도 같은 값 |
| `CardSpacingSeconds` | 180초 (시계 상한 360초) | FG:39 · FG:188 | 개방 카드 사이 최소 간격 (앞에서 본 시간) |
| `MaxPlayDeltaSeconds` | 10초 | MS:68 | 카드 시계에 한 프레임이 더할 수 있는 최대 |
| AfterDelay 인자 | 2초 · 5초 · 1.5초 | META:1838 · 1854 · 1886 | 3번 · 4번 · 6번 단계 대기 시간 |
| PrefillIdleChest 인자 | 2시간 | META:1952 | 9번 단계 수선 바구니 미리 채움 (바구니 상한 안) |
| ShortFirstExpedition 인자 | 1분 | META:2018 | 13번 단계 첫 원정 길이 |
| 6번 단계 보상 | 단추 250 | META:1899-1903 | `ftue_shop_upgrade` 끝 보상 |
| 졸업 선물 (16번 단계 보상) | 별조각 300 | META:2068-2072 | 졸업 · 건너뛰기 모두 지급 |
| AutoBattle 개방 보상 | 별조각 30 | META:87-91 | 27개 기능 중 보상이 있는 유일한 행 |
| `guideVisible` | `FtueStep` 6 | META:961-965 · MDB:784 | 가이드 퀘스트 줄이 보이는 조건 (0~5번 단계 완료) |
| `BaseEchoSlots` · `EchoSlotUnlockStage` | 5 · 20 | FORM:20 · FORM:22 | 잔향이 덱 칸 수, 6번째 칸이 열리는 최고 스테이지 |
| `starterLineages` · welcome 우편 | 계보 4종 · 별조각 1000 + 단추 500 | GC:46 · BOOT:115 | 첫 부팅 지급 (1.1-K) |

### 1.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `features[].feature` | `FeatureId` (int) | 기능 (GE:397-453, 0~26 · 27개 모두 정의됨) | 7 (`Gacha`) |
| `features[].name` | string | 기능 이름 (카드 · 안내) | "잃어버린 친구 찾기" |
| `features[].conditions` | List\<MetaCondition\> | 하나라도 충족하면 열림 (OR). 비면 처음부터 열림 | `StageCleared` target 3 |
| `features[].alsoRequire` | List\<MetaCondition\> | 전부 충족해야 열림 (AND). **asset 27개 모두 비어 있음** | [] |
| `features[].lockedHint` · `lockedToast` | string | 잠긴 탭 아래 작은 글자 · 눌렀을 때 토스트 | "1-3" · "스테이지 1-3을 클리어하면 열려요" |
| `features[].showCard` · `cardTitle` · `cardText` | bool · string · string | 개방 카드 여부 (0이면 점만) · 카드 제목 · 한 줄 | 1 · "새 기능이 열렸어요!" |
| `features[].rewards` | List\<MetaReward\> | 열릴 때 보상 | 별조각 30 (`AutoBattle`) |
| `MetaCondition` | type · counter · targetId · target | 조건 종류와 인자 (MDB:14-51, 종류 GE:476-528) | type 4, target 3 |
| `ftueSteps[].id` | string | 단계 ID | `ftue_intro` |
| `ftueSteps[].trigger` · `triggerArg` | `FtueTrigger` · string | 시작 조건 · 인자 (기능 이름 / 초) (GE:554-564) | 3 · "5" |
| `ftueSteps[].coachTarget` · `text` | string · string | 손가락 대상 경로 (비면 말풍선만) · 말풍선 문구 | `SafeArea/StageArea/Battle/Strip/LampButton` |
| `ftueSteps[].blockInput` | bool | 대상 밖 입력 막기. **17단계 모두 0** | 0 |
| `ftueSteps[].hintOnly` | bool | 손가락 없이 토스트 한 줄만 (3 · 5 · 8 · 14번) | 1 |
| `ftueSteps[].complete` | MetaCondition | 끝 조건 | `Counter` `EnemyKill` 3 |
| `ftueSteps[].action` · `actionArg` | `FtueAction` · string | 시작 때 할 일 · 인자 (GE:570-589) | 5 · "udukeoni" |
| `ftueSteps[].rewards` | List\<MetaReward\> | 끝낼 때 보상 | 단추 250 |
| 세이브 `meta.features` | unlocked · seen · cardShown (HashSet\<FeatureId\>) · silentSyncPending | 열림 · 봤음(점 꺼짐) · 카드 넣음 · 옛 세이브 동기화 (SVM:372-389) | unlocked = {0, 1, 2, 4} |
| 세이브 `meta.ftue` | step (int) · done · skipped (bool) · flags (HashSet\<string\>) · startedUnix | 튜토리얼 진행 (SVM:393-410) | step 5, flags = {"ftue.running"} |

### 1.5 해금 순서 표

`StageCleared` target → 챕터-번호 변환은 1.1-B. asset의 `lockedHint`는 모든 스테이지 행에서 이 변환 결과와 같다. 같은 순서 번호 = 같은 스테이지 클리어에서 함께 열림. 순서는 스테이지 조건 기준이며, 설치 일수 · 던전 층 조건은 스테이지와 앞뒤가 정해지지 않는다. 표의 행 순서는 asset `features` 목록 순서와 같다 (`TryGetNextLocked` "다음 개방" 안내가 이 순서를 쓴다, FG:338-350).

| 순서 | 기능 (FeatureId 이름 · 번호) | 조건 | 표기 (예: 1-3 클리어) | 보상 | 위치 (MetaDatabase.asset:줄) |
|---|---|---|---|---|---|
| 1 | `Battle` · 0 — "전투" | 없음 | 처음부터 열림 | 없음 | META:16 |
| 1 | `ShopUpgrade` · 1 — "수선점 강화" | 없음 | 처음부터 열림 | 없음 | META:28 |
| 1 | `Characters` · 2 — "친구들" | 없음 | 처음부터 열림 | 없음 | META:40 |
| 2 | `CharacterLevelUp` · 4 — "친구 강화" | `StageCleared` 1 | 1-1 클리어 | 없음 | META:52 |
| 3 | `AutoBattle` · 5 — "자동 전투" | `StageCleared` 2 | 1-2 클리어 | 별조각 30 | META:70 |
| 3 | `IdleChest` · 6 — "수선 바구니" | `StageCleared` 2 | 1-2 클리어 | 없음 | META:92 |
| 4 | `Gacha` · 7 — "잃어버린 친구 찾기" | `StageCleared` 3 | 1-3 클리어 | 없음 | META:110 |
| 4 | `Formation` · 3 — "편성" | `FtueStep` 11 또는 `StageCleared` 3 | 1-3 클리어 (또는 튜토리얼 0~10번 단계 완료) | 없음 | META:128 |
| 4 | `Synergy` · 17 — "시너지" | `FtueStep` 11 또는 `StageCleared` 3 | 1-3 클리어 (또는 튜토리얼 0~10번 단계 완료) | 없음 | META:150 |
| 5 | `Expedition` · 8 — "원정" | `StageCleared` 4 | 1-4 클리어 | 없음 | META:172 |
| 6 | `QuickIdle` · 9 — "빠른 수선" | `StageCleared` 5 | 1-5 클리어 | 없음 | META:190 |
| 6 | `Attendance` · 10 — "수선점 출석부" | `FtueDone` 또는 `StageCleared` 5 | 1-5 클리어 (또는 튜토리얼 끝) | 없음 | META:208 |
| 7 | `Speed2x` · 11 — "2배속" | `StageCleared` 6 | 1-6 클리어 | 없음 | META:230 |
| 7 | `LampPolicy` · 22 — "불 밝히기 방식" | `StageCleared` 6 | 1-6 클리어 | 없음 | META:248 |
| 8 | `Missions` · 12 — "수선 일지" | `StageCleared` 7 | 1-7 클리어 | 없음 | META:266 |
| 8 | `BattleStats` · 26 — "전투 기록" | `StageCleared` 7 | 1-7 클리어 | 없음 | META:284 |
| 9 | `Shop` · 13 — "수선점 상점" | `StageCleared` 8 | 1-8 클리어 | 없음 | META:301 |
| 10 | `Dungeon` · 14 — "잔향이 던전" | `StageCleared` 10 | 1-10 클리어 | 없음 | META:319 |
| 10 | `Evolution` · 15 — "진화 제작" | `StageCleared` 10 | 1-10 클리어 | 없음 | META:337 |
| 10 | `Codex` · 16 — "도감 보상" | `StageCleared` 10 | 1-10 클리어 | 없음 | META:355 |
| 11 | `AutoChallenge` · 23 — "자동 보스 도전" | `StageCleared` 11 | 2-1 클리어 | 없음 | META:373 |
| 11 | `ExpeditionBatch` · 24 — "원정 한 번에 보내기" | `StageCleared` 11 | 2-1 클리어 | 없음 | META:391 |
| 12 | `Achievements` · 18 — "수선점 기록" | `StageCleared` 13 | 2-3 클리어 | 없음 | META:409 |
| 13 | `Costumes` · 19 — "의상" | `StageCleared` 15 | 2-5 클리어 | 없음 | META:427 |
| 14 | `EchoSlot6` · 20 — "잔향이 여섯째 칸" | `StageCleared` 19 | 2-9 클리어 (= 2-10 도달) | 없음 | META:445 |
| 15 | `Events` · 21 — "이벤트" | `StageCleared` 21 또는 `InstallDays` 3 | 3-1 클리어 또는 설치 3일째 | 없음 | META:462 |
| 16 | `DungeonBatchSweep` · 25 — "던전 입장권 한 번에 쓰기" | `DungeonFloor` 5 | 잔향이 던전 5층 돌파 | 없음 | META:484 |

- 개방 카드 `showCard = 1` (13개): `CharacterLevelUp` · `AutoBattle` · `IdleChest` · `Gacha` · `Formation` · `Expedition` · `QuickIdle` · `Speed2x` · `Missions` · `Shop` · `Dungeon` · `Evolution` · `Costumes`. 나머지 14개는 0 (점만, 처음부터 열린 3개 포함).
- `Formation` · `Synergy`의 `FtueStep` 11은 10번 단계 `ftue_first_meeting`이 끝나야 충족된다. 그 단계의 시작 조건이 `Gacha` 열림(1-3 클리어)이라, 건너뛰기가 없으면 `FtueStep` 11보다 `StageCleared` 3이 먼저 충족된다. 건너뛰기를 하면 두 기능은 그 자리에서 `ForceUnlock`된다 (1.1-I).
- `EchoSlot6`의 실제 칸 개방은 `FeatureGateSystem`을 거치지 않는다: `FormationSystem.UnlockedEchoSlots`가 `highestStage ≥ 20`을 직접 본다 (FORM:50). asset 조건(target 19 → highestStage > 19)과 같은 시점이다.

**잠금을 확인하는 곳 (요약)** — `IsUnlocked` · `TryUse` · `FeatureResolver` 검색 기준 (`FeatureResolver`는 MS:454-468 · `Core/Game.cs:164`에서 `FeatureGateSystem.IsUnlocked`로 연결)

| 기능 | 잠금 확인 위치 | 잠겼을 때 |
|---|---|---|
| `Formation` · `Expedition` · `Gacha` · `Shop` (하단 탭) | `UI/Meta/TabGateView.cs:45-52`, `:130-136`, `:197-205` | 탭 반투명 + 자물쇠 + `lockedHint`, 누르면 토스트 |
| `AutoBattle` · `Speed2x` (전투 HUD 버튼) | `UI/Meta/FeatureLockCover.cs:131-144`, `:218-226` (붙이는 곳 `Editor/Builders/MetaSceneBuilder.cs:304-305`) | 버튼 위 덮개 + 자물쇠, 누르면 토스트 |
| `Missions` · `Attendance` · `Events` · `Codex` · `Achievements` (메뉴 타일) | `UI/Main/MenuPopup.cs:146-156` | 타일 숨김 |
| `CharacterLevelUp` · `Evolution` · `Costumes` | UI `UI/Characters/CharacterUI.cs:435-489` 경유 · 시스템 `Systems/Characters/CharacterSystem.cs:34`, `EvolutionSystem.cs:26-40`, `CostumeSystem.cs:37-48` | 버튼 잠금 + 토스트, 시스템 쪽 거절 |
| `Dungeon` · `DungeonBatchSweep` · `ExpeditionBatch` | `UI/Expedition/ExpeditionPanel.cs:103-106` · `Systems/Battle/DungeonSystem.cs:174`, `:266`, `:384` · `Systems/Battle/ExpeditionSystem.cs:82-89` | 진입 · 소탕 · 모두 다시 보내기 거절 |
| `AutoChallenge` · `LampPolicy` | BS:178-186 · BS:260-263 | 자동 보스 도전 꺼짐 · 방식은 `Auto`로 고정 |
| `IdleChest` · `QuickIdle` · `Missions` · `Shop`(일일 선물) · `Events`(시즌 · 캘린더) · `Codex` · `Achievements` · `BattleStats` | `Systems/Meta/IdleChestSystem.cs:153`, `:466` · `MissionSystem.cs:83` · `DailyGiftSystem.cs:43` · `SeasonPassSystem.cs:93` · `EventCalendarSystem.cs:152` · `CodexSystem.cs:147` · `AchievementSystem.cs:107` · `UI/Meta/QuickIconView.cs:78` | 시스템 `IsUnlocked` false · 아이콘 숨김 |
| 가이드 퀘스트 · 일지 · 강화 추천의 [가기] | `UI/Meta/GuideQuestBarView.cs:306` · `UI/Meta/JournalPopup.cs:487`, `:730` · `UI/Meta/StrengthenPopup.cs:521` | 이동 대신 잠김 토스트 |

### 1.6 튜토리얼 단계 표

17단계 (`META:1785` `ftueSteps`). 끝 조건의 `Counter`는 평생 값이다 (PT:215-216). 손가락 대상은 `coachTarget` 경로의 끝 부분만 적었다. 시작 때 동작은 FD:295-329.

| 단계 | 이름/내용 | 시작 조건 | 끝 조건 | 막는 것 · 강제하는 것 | 위치 |
|---|---|---|---|---|---|
| 0 | `ftue_intro` — 동화 인트로 3장 (`StoryIntro`) | 즉시 (`Immediate`) | 플래그 `meta.intro_seen` (인트로 끝 → `Signal`, FD:393-396) | 새 게임이면 AUTO 끔 (MS:222). 1-1~1-3 전투 보호 켜짐 (step = 0 규칙). 코치마크 숨김 | META:1786 |
| 1 | `ftue_first_battle` — "지킴이로 방향을 지키고, 잔향이로 위기를 막아 / 수선점을 밝혀 주세요!" (`BattleProtection`) | 즉시 | 망각체 처치 `EnemyKill` ≥ 3 | 이 단계부터 졸업 전까지 `WantsBattleProtection` = true. 말풍선만 (대상 없음) | META:1804 |
| 2 | `ftue_first_summon` — 덱 첫 칸을 눌러 잔향이 부르기 | 즉시 | 잔향이 소환 `EchoSummon` ≥ 1 | 손가락: `DeckPanel/Card0` | META:1820 |
| 3 | `ftue_drag_summon` — 끌어서 방향 보내기 힌트 (hintOnly) | 앞 단계 끝 + 2초 (`AfterDelay`) | 없음 (`None`, 시작과 함께 끝) | 토스트 한 줄만 | META:1836 |
| 4 | `ftue_first_lamp` — 첫 불 밝히기 (`ForceLampFull`) | 앞 단계 끝 + 5초 | 불 밝히기 `LampLit` ≥ 1 | `Sim.Kills` ≥ 8 또는 4초 뒤 게이지 강제 가득 (1.1-H). 손가락: `Strip/LampButton` | META:1852 |
| 5 | `ftue_first_boss` — "보스를 물리치면 다음 스테이지로 가요!" (hintOnly) | 즉시 | 1-1 클리어 (`StageCleared` 1) | 토스트 한 줄만 | META:1868 |
| 6 | `ftue_shop_upgrade` — 강화 탭에서 수선점 강화 x1을 3번 | 앞 단계 끝 + 1.5초 | 수선점 강화 `ShopLevelUp` ≥ 3 | 손가락: `GrowthPanel/Buttons/LevelUp1Button`. 끝 보상 단추 250 | META:1884 |
| 7 | `ftue_friend_level` — 짝짝이를 레벨 5까지 | 즉시 | 계보 `jjakjjak` 레벨 ≥ 5 (`LineageLevel`) | 손가락: `GrowthPanel/LineageList` | META:1904 |
| 8 | `ftue_auto_battle` — "AUTO가 켜졌어요!" (hintOnly, `EnableAutoBattle`) | `AutoBattle` 열림 (1-2 클리어) | 없음 (`None`) | `Battle.AutoMode = true` | META:1921 |
| 9 | `ftue_idle_chest` — 바구니 받기 (`PrefillIdleChest` "2") | `IdleChest` 열림 (1-2 클리어) | 바구니 받기 `ChestClaim` ≥ 1 | 바구니에 2시간치를 1번 미리 채움 (`Systems/Meta/IdleChestSystem.cs:592-605`). 손가락: `MetaHud/IdleChestWidget` | META:1937 |
| 10 | `ftue_first_meeting` — 첫 만남 무료 소환 (`FirstMeetingSummon` "udukeoni") | `Gacha` 열림 (1-3 클리어) | 플래그 `meta.first_meeting_summoned` | 소환 시트 위 첫 만남 카드 → "우두커니"(Legendary, CDB:1520-1535) 확정 소환 (FD:421-462, 무료 · 천장 · 확률 제외 GE:581). 손가락: `TabBar/Tab_GachaPanel` | META:1954 |
| 11 | `ftue_formation` — 우두커니를 지킴이로 세우기 | 즉시 | 편성된 지킴이 ≥ 4 (`GuardiansPlaced`) | 손가락: `TabBar/Tab_FormationPanel` | META:1970 |
| 12 | `ftue_synergy` — 시너지 자세히 보기 | 즉시 | 플래그 `meta.synergy_detail_viewed` (시너지 상세 팝업이 열리면 `Signal`, FD:775) | 손가락: `FormationPanel/TeamSynergy` | META:1987 |
| 13 | `ftue_expedition` — 첫 원정 보내기 (`ShortFirstExpedition` "1") | `Expedition` 열림 (1-4 클리어) | 플래그 `meta.short_first_expedition_used` | 처음 보낸 원정의 끝 시각을 시작 + 1분으로 당김, 1번 (FD:464-484). 손가락: `TabBar/Tab_ExpeditionPanel` | META:2004 |
| 14 | `ftue_expedition_claim` — 원정 받기 (hintOnly) | 즉시 | 원정 받기 `ExpeditionClaim` ≥ 1 | 토스트 한 줄만 | META:2020 |
| 15 | `ftue_quick_idle` — 빠른 수선 (오늘 무료) | `QuickIdle` 열림 (1-5 클리어) | 빠른 수선 `QuickIdle` ≥ 1 | 손가락: `MetaHud/IdleChestWidget` | META:2036 |
| 16 | `ftue_graduation` — 졸업 (`Graduation`) | 스테이지 사이 (`BetweenStages`) | 없음 (`None`) | 시작 때 MAIN 화면으로 (FD:323-326). 끝 → done, 졸업 카드 큐, 별조각 300 | META:2053 |

- 입력 막기(`blockInput`)가 켜진 단계는 없다 (FtueDirector는 이 값을 코치마크에 넘기기만 한다, FD:664 · FD:679). 진행 중 전체에 걸리는 것: 1-1~1-3 전투 보호 (1.1-F), 꺼져 있는 기능 · 팝업 (1.1-L).

### 1.7 추출 못 한 것

- **asset ↔ 원본 콘텐츠 코드 차이**: `MC:27-30` `CardsReady` = {`Speed2x`, `LampPolicy`, `AutoChallenge`, `Achievements`, `Events`}이고 `MC:115` · `MC:160-161` · `MC:175` · `MC:202-203`이 이 목록으로 `showCard`와 카드 보상(`AutoChallenge` 빠른 수선권 1 · `Events` 별빛 사탕 50)을 켠다. asset은 `Speed2x`만 `showCard = 1`이고 나머지 4개는 0, 보상 없음 (META:248 · 373 · 409 · 462). `MetaDataBuilder`를 다시 돌리면 값이 바뀐다. 어느 쪽이 의도인지는 코드로 알 수 없다.
- `GE:405` enum 주석은 `Formation`을 "FTUE 3단계"로 적었으나 asset 조건은 `FtueStep` 11 또는 1-3 클리어다 (META:128-138).
- `FD:21` · `FD:263-264` 주석은 첫 만남 단계의 끝 조건을 "GachaPull 1"로 적었으나 asset은 플래그 `meta.first_meeting_summoned`다 (META:1962-1966).
- `GE:566-569` `FtueAction` 주석은 "전투 훅이 생길 때까지 UI에서 흉내"라고 적었으나, 지금은 `BattleSystem.ForceLampFull`(BS:334) · `ProtectionResolver`(MS:467) 훅이 있다.
- `Gacha` · `Formation` · `Synergy` · `AutoBattle` · `Speed2x`는 시스템 코드에서 `FeatureId` 잠금 확인을 찾지 못했다 (UI 탭 · HUD 덮개만). `Synergy`는 UI 잠금 표시 위치도 찾지 못했다. 검색 기준이라 빠진 곳이 있을 수 있다.
- 각 기능이 실제 플레이 몇 분째에 열리는지는 스테이지 진행 속도에 달려 코드만으로 알 수 없다. `alsoRequire`(`PlayMinutes`) 속도 조절 구조는 있으나 asset 값이 비어 있어 지금은 동작하지 않는다.
- 코치마크 · 인트로 · 졸업 카드 · 첫 만남 카드의 화면 표현과 문구 배치는 이 문서 범위 밖이다 (UI 문서).

## 2. 기획 방향 (To-be) — DD

> 기준 문서: [`UI_InGame`](../03_UI_UX/UI_InGame.md) §16.1(초반 5분 온보딩 7단계) · §4.3(튜토리얼 팝업 = 지정 영역 외 입력 차단) · [`SYS_InGameBattle`](./SYS_InGameBattle.md) §5.2(탭만 교육, 조준은 팁) · §14-5(튜토리얼 지급 6~8캐릭) · §14-8(잔향이 해금 = 합류 '손님'부터) · [`KN_ConceptNarrative`](../00_Overview/KN_ConceptNarrative.md) 프롤로그(열쇠 → 수선점 → 짝짝이 · 몽실 첫 만남 → 망각 방어) · D-27 · D-28.
> 결론: **현재 구현 17단계가 UI §16.1의 7박자를 거의 다 덮는다.** ②는 현재 흐름을 정본으로 두고, ① D-27 전투 변경(8방위 · 수선등 밝히기 · 성향)에 맞춘 단계 교체, ② 빠진 박자(구조 · 합류 · 모험 지도) 보강만 한다.

### 2.0 초반 5분 — 기획 7박자 ↔ 현재 17단계

| UI §16.1 박자 | 현재 단계 (①-1.6) | 판정 | To-be |
|---|---|---|---|
| 1. 수선점에 불을 밝힌다 | 0 동화 인트로 3장 | △ 홈 소개 없음 | 인트로 마지막 장 = "불 켜기" 탭 1번 → 바로 전투 (홈 소개는 6단계 강화 때) |
| 2. 첫 방어전 | 1 첫 전투 (1-1~1-3 보호) | ✅ | 유지 + **D-27 #1 이후: 지킴이 1명 자동 배치 → "빈 방향" 경고 1회** |
| 3. 첫 잔향이 소환 (탭 → 두 번째에 드래그) | 2 탭 소환 · 3 드래그 힌트 | ✅ | 유지 (§5.2 학습 계단과 같음) |
| (전투 동사) | 4 첫 불 밝히기 (게이지 강제 가득) | ⚠ D-27 #3로 기능이 바뀜 | **"수선등 밝히기 = 잔향 투자" 첫 1회로 교체** (I-10 이후) |
| 4. 긴급 귀환이 아닌 첫 승리 | 5 첫 보스 · 1-1~1-3 보호(지지 않음) | ✅ | 유지 |
| 6. 수선 1회 (성장 기대) | 6 수선점 강화 x1 ×3 · 7 짝짝이 Lv.5 | ✅ | 유지 |
| (방치) | 8 AUTO · 9 바구니(2시간 미리 채움) · 15 빠른 수선 | ✅ 기획엔 없음 | 유지 — 방치형 핵심 손맛 (D-28 #11) |
| 5. 새 유실자 합류 | 10 첫 만남 무료 소환(우두커니) | △ 획득 = 가챠, 합류 서사 없음 | §3 #2 |
| (편성) | 11 지킴이로 세우기 · 12 시너지 | ✅ | 유지 + **D-27 #6 이후: 성향 1줄 힌트** |
| (파견) | 13 첫 원정(1분) · 14 받기 | ✅ | 유지 (D-27 #5 "파견 중 편성 불가" 안내 1줄 추가) |
| 7. 다음 유실표 → 모험 지도 | 16 졸업 → 메인 | ✗ 지도 · 유실표 안내 없음 | 졸업 카드에 "다음 유실표" 1줄 + 지도 버튼 손가락 (P1, 지도 · 유실표 구현과 함께) |

### 2.1 규칙
```
[진행] 현재 구현 그대로 (trigger → 시작 → complete → 보상 → 다음, 앱 재시작 시 진행 중 단계 다시 시작)
[입력 막기]
IF 단계 = 2 (첫 잔향이 소환)            → blockInput = 1 (손가락 대상만 누를 수 있음)   ← UI §4.3, 핵심 동사 1개만 강제
ELSE                                     → blockInput = 0 (현재와 같음, P6 "자기 속도")
[전투 보호] 1-1~1-3 + 튜토리얼 진행 중 → 망각체 체력 ×0.6 · 수선점 내구 바닥 1 (현재 구현 유지)
[건너뛰기] 설정에서 언제든. 핵심 기능(편성 · 시너지) 강제 개방 + 별조각 300 (현재 구현 유지)
[D-27 반영 단계 교체] (I-10 전투 수정과 같은 PR 묶음에서)
  4번 ftue_first_lamp      → ftue_first_light_invest: 잔향 ≥ 첫 투자 비용이면 손가락, 투자 1회 = 끝
  1번 뒤 추가 ftue_empty_direction: 지킴이가 없는 방향에서 적이 오면 1회 경고 연출 (hintOnly)
  11번 뒤 추가 ftue_behavior_hint: 성향 아이콘 1줄 설명 (hintOnly)
```

### 2.2 상태 전이
```
[인트로] → [첫 전투(보호)] → [소환 탭 · 드래그] → [수선등 투자*] → [첫 보스 · 1-1 클리어]
→ [강화 ×3 · 짝짝이 Lv.5] → [1-2: AUTO · 바구니] → [1-3: 첫 만남 · 편성 · 시너지] → [1-4: 원정] → [1-5: 빠른 수선] → [졸업]
  (* D-27 이후 교체 단계)
어느 단계든 ─(설정 · 건너뛰기)→ [졸업(건너뜀)]
```

### 2.3 UI 플로우 / 디스크립션
1. **코치마크** : 손가락 + 말풍선. 대상 밖은 어둡게만, 입력은 막지 않음 (2번 단계만 막음)
2. **힌트 토스트** (hintOnly) : 화면 위 한 줄, 3초
3. **개방 카드** : 스테이지 사이에만, 카드 간 최소 180초 (현재 구현)
4. **잠긴 기능** : 탭 반투명 + 자물쇠 + "1-3" 작은 글자 → 탭 → "스테이지 1-3을 클리어하면 열려요" 토스트 (현재 구현)
5. **졸업 카드** : 별조각 300 + (P1) 다음 유실표 1줄 + 모험 지도 버튼

### 2.4 데이터
현재 `features[]` · `ftueSteps[]` 구조를 그대로 정본으로 쓴다 (①-1.4). 변경분만:

| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `ftueSteps[2].blockInput` | bool | 첫 잔향이 소환만 1 | 1 |
| `ftueSteps[].id` 신규 3행 | string | `ftue_first_light_invest` · `ftue_empty_direction` · `ftue_behavior_hint` (D-27 이후) | `ftue_empty_direction` |
| `FtueAction` 신규 값 | enum | `ForceInvestReady` (첫 투자 비용만큼 잔향 보장) — `ForceLampFull` 대체 | 9 |
| `starterLineages` | string[] | 시작 계보 — I-7 이름 확정 후 `GameConfig.asset`에 명시 (지금은 코드 기본값) | `["jjakjjak", …]` |

## 3. 차이 → 결정 필요 — DD 작성, 대표 결정

| # | 항목 | ① 현재 구현 | ② 기획 방향 | 영향 (코드 · 데이터 · 세이브) | DD 추천 | 결정 |
|---|---|---|---|---|---|---|
| 1 | 온보딩 흐름 | 전투 먼저 (인트로 → 전투 → 소환 → 불 밝히기 → 보스 → 강화 …), 17단계 | 홈(수선점 불) 먼저 → 방어전 → 소환 → 승리 → 합류 → 수선 → 지도 (UI §16.1) | 데이터 | **현재 흐름을 정본으로 채택** — 첫 화면부터 전투가 도는 쪽이 첫 5분 후킹(계명 3)에 유리. 홈 소개는 강화 단계에서 자연스럽게. §2.0 표의 보강만 | **D-32** DD 추천안 |
| 2 | 첫 캐릭터 획득 방식 | 1-3 클리어 → **첫 만남 무료 소환**(가챠, 우두커니 Legendary 확정) | 컨셉: 유실자는 **구조**로 합류 (유실표 → 구조 전투 → 피난자), UI RC-001 · §16.1-5 | 기획(획득 구조 전체) · 코드 | **M1: 현재 유지, 연출만 "구조"로.** 첫 만남 카드 문구 · 연출을 "수선점 앞에서 발견" 구조 서사로 바꾸고(데이터 텍스트), 구조 전투(시스템 §2.3)가 구현되는 P1에 실제 구조로 교체. 가챠 · 구조 병행 구조는 `SYS_Gacha`에서 확정 | **D-32** DD 추천안 |
| 3 | 시작 캐릭터 | 계보 4종 (짝짝이 · 똑단이 · 찰칵이 · 몽당이) + 첫 만남 1 = 5 | 짝짝이 · 몽실 첫 만남(컨셉 프롤로그), 튜토리얼 지급 6~8 (시스템 §14-5) | 데이터 | **수는 현재(4 + 1) 유지, 이름은 I-7 대조표 후 확정.** 지킴이 5칸을 1-3에 채우는 지금 구조가 편성 학습에 맞음. 6~8은 전투 #4(배타) 결정 뒤 재검토 | **D-32** DD 추천안 |
| 4 | 입력 막기 | 17단계 모두 막지 않음 | 튜토리얼 팝업 = 지정 영역 외 입력 차단 (UI §4.3) | 데이터 1칸 | **첫 잔향이 소환(2번) 1단계만 막기.** 핵심 동사를 확실히 배우게 하고, 나머지는 자기 속도(P6) | **D-32** DD 추천안 |
| 5 | D-27 전투 변경 반영 | 4번 = 불 밝히기 전체 공격 교육, 방향 · 성향 교육 없음 | 수선등 밝히기 = 잔향 투자 · 8방위 빈 방향 · 성향 | 데이터 · 연출 | **I-10(전투 코드 수정)과 같은 묶음으로 단계 교체 3건** (§2.1) | **D-32** DD 추천안 |
| 6 | 합류 단계 · 잔향이 해금 | 없음 (얻으면 바로 지킴이 · 잔향이 모두 가능) | 피난자는 잔향이 덱 불가, '손님'부터 (시스템 §14-8 · UI FM-001) | 코드 · 세이브 | **P1, `SYS_Relationship`에서.** M1 튜토리얼은 영향 없음 | **D-32** DD 추천안 |
| 7 | 건너뛰기 보상 | 건너뛰어도 졸업 선물(별조각 300) 동일 | 기획 없음 | 경제 | **현재 유지** — 랭킹 없는 싱글이라 건너뛰기를 벌할 이유 없음(P6). 리세마라는 §5 | **D-32** DD 추천안 |

## 4. 예외 처리 / 엣지 케이스
| 케이스 | 처리 |
|---|---|
| 네트워크 단절 | 영향 없음 (로컬) |
| 앱 강제 종료 | 진행 중 단계는 다음 실행에 바로 다시 시작 (현재 `RunningFlag`) |
| 기기 시간 조작 | 13번 첫 원정 1분 · 9번 바구니 미리 채움은 1회 플래그라 반복 불가. 3번 · 4번 대기(AfterDelay)는 실제 시간 |
| 튜토리얼 중 실패 | 1-1~1-3 보호로 실패하지 않음 (내구 바닥 1) |
| 건너뛴 뒤 다시 보기 | UI §16.3 "튜토리얼 다시 보기" 요구 — 현재 없음 → P2 (코치마크만 다시 보기) |
| 옛 세이브 | 진행이 있으면 done · skipped 처리 (현재 구현) |

## 5. 어뷰징 검토
| 시나리오 | 대응 |
|---|---|
| 리세마라 (새 계정 → 건너뛰기 → 별조각 1,600 + 첫 만남 Legendary로 반복) | 싱글 · 랭킹 없음이라 피해는 작음. BM 붙일 때 `SYS_Gacha`에서 "첫 10연 확정 + 시작 지급 축소" 등으로 검토 |
| 첫 원정 1분 단축 반복 | 1회 플래그 (현재) |
| 바구니 미리 채움 반복 | 1회 플래그 (현재) |

## 6. 확장성 / 오픈 이슈
- [ ] §3 #2 구조 서사 문구 — `CNT_Story` 프롤로그와 맞춤
- [ ] §3 #5 단계 교체 3건 → I-10 상세 지시에 포함
- [ ] 튜토리얼 다시 보기 (UI §16.3) — P2
- 확장성: 단계 · 기능 개방이 모두 데이터(`ftueSteps` · `features`)라 순서 조정은 코드 수정 없이 가능. 단, 새 `FtueAction`은 코드 추가 필요
