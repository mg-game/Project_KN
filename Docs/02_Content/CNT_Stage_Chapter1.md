# CNT_Stage_Chapter1

| 항목 | 값 |
|---|---|
| 상태 | Draft |
| 작성자 | ① 구현: 기개발 · ②③: DD |
| 최종 수정 | 2026-11-11 |
| 관련 테이블 | - |
| 관련 문서 | [`SYS_InGameBattle`](../01_System/SYS_InGameBattle.md) §17 · [`KN_Glossary`](../00_Overview/KN_Glossary.md) |
| 근거 | 코드 커밋 `4b73a5a` (`feature/lost-and-lovely-import`) — 경로 약칭 표 참고 |

> 구조(D-26): **① 현재 구현** = 지금 코드가 실제로 하는 일 / **② 기획 방향** = 기획서가 원하는 모습 / **③ 차이** = 둘이 다른 곳, 대표가 결정.
> 결정이 나면 ②에 반영하고 ③ 행에 `D-n`을 적는다. 코드 수정은 지시함(#3)으로. 용어는 [`KN_Glossary`](../00_Overview/KN_Glossary.md)를 따른다.

## 0. 개요
- 목적 (이 시스템이 해결하는 문제 / 유저 동기):
- 핵심 루프 내 위치:
- 관련 필라 (SYS_InGameBattle §1 P1~P6):

## 1. 현재 구현 (As-built) — 기개발

> 작성: 기개발 (2026-11-11). **근거: `feature/lost-and-lovely-import` @ `4b73a5a`**.
> 코드 · 데이터가 실제로 하는 일만 적었다. 해석 · 제안은 넣지 않았다 (② · ③은 DD).
> 1장 = 메인 스테이지 1~10 (`1-1` = 1 … `1-10` = 10). 전투 규칙 전체는 SYS_InGameBattle §17이 다룬다. 이 문서는 스테이지마다 달라지는 내용만 적는다.
> 수치 출처: 런타임 에셋 `BCA`(`Scenes/Main.unity`가 참조)와 코드 기본값 `BC`가 같다 (스칼라 필드 전부 · 망각체 9종 · `firstChapterModifiers` — 스크립트로 대조). 그래서 위치는 코드 줄로 적는다.

**파일 약칭** (모두 `Assets/_Project/` 아래)

| 약칭 | 경로 |
|---|---|
| `BC` · `BCA` | `Scripts/Data/BattleConfig.cs` · `Data/BattleConfig.asset` |
| `SIM` · `GM` | `Scripts/Systems/Battle/BattleSimulator.cs` · `BattleSimulator.Gimmicks.cs` |
| `BS` · `BT` | `Scripts/Systems/Battle/BattleSystem.cs` · `BattleTypes.cs` |
| `GE` | `Scripts/Core/GameEnums.cs` |
| `MS` · `TUT` | `Scripts/Systems/Meta/MetaSession.cs` · `TutorialSystem.cs` |
| `FG` · `PT` | `Scripts/Systems/Meta/FeatureGateSystem.cs` · `ProgressTracker.cs` |
| `CC` · `STORY` | `Scripts/Systems/Meta/ChapterChestSystem.cs` · `StorySystem.cs` |
| `EVT` · `GQ` | `Scripts/Systems/Meta/EventCalendarSystem.cs` · `GuideQuestSystem.cs` |
| `BHR` | `Scripts/UI/Battle/BossHintRule.cs` |
| `META` · `ITEM` | `Data/MetaDatabase.asset` · `Data/ItemDatabase.asset` |

### 1.1 규칙
스테이지 내용을 정하는 규칙만 적는다. 이동 · 공격 · 불 밝히기 · 실패 후 단계 내림 등은 SYS_InGameBattle §17.
```
스테이지 s (1장 = s 1~10, 라벨 "1-s")                                      BS:127-133
전투 구성: Level = s · 기믹 = GetStageModifier(s) · 보호 = ProtectionResolver(s)   BS:555-570
  기믹: 1장이면 firstChapterModifiers[s−1]                                   BC:460-466
  망각체 풀: enemies를 목록 순서로 훑어 minStage ≤ s인 것만                       SIM:680-708
    IF isBoss → 보스 풀
    ELSE IF spawnWeight > 0 → 일반 풀, 가중치 = spawnWeight × 기믹 가중치 배율      SIM:699-706 · GM:230-235
웨이브: 첫 대기 → 웨이브 크기만큼 생성 간격마다 1마리 → 휴식 → 반복                    SIM:908-940
  1마리 = 일반 풀에서 가중치 비례 추첨 (분열체 · 보스 부하는 추첨 밖에서 생긴다)          SIM:950-959 · GM:197-212 · SIM:1096-1114
처치마다 처치 수 +1 (일반 · 분열체 · 부하 모두)                                    SIM:1566
  IF 보스 처치 → 클리어                                                       SIM:1576-1579
  ELSE IF 보스 미등장 AND 처치 수 ≥ 보스 처치 수 → 보스 등장 (스테이지당 1번)          SIM:1580-1583
     보스 = 보스 풀[(s−1) % 보스 풀 수], 이후 일반 생성 진행 속도 ×0.5                 SIM:971 · SIM:911
클리어 → 단추 + 별빛 사탕 + 재료 굴림 + 호감도, highestStage = max(highestStage, s+1)    BS:639-647
  → 기능 개방: StageCleared t 조건은 highestStage > t일 때 충족 (= 1-t 클리어)          PT:543 · FG:262-264
  → s % 10 = 0 → 챕터 상자 · 동화책 팝업 요청 (아직 안 열었 / 안 봤을 때만)             CC:168-178 · STORY:123-138
실패 → 진행도 비례 단추                                                       BS:656-659 · BS:769-775
```

### 1.2 상태 전이
전투 진행 · 클리어 · 실패 · 결과 대기 · 다음 전투의 상태 전이는 SYS_InGameBattle §17.2와 같다 (이 문서에서 다시 적지 않음).

### 1.3 수치 (코드 상수 · 설정값)
| 이름 | 값 | 위치 (파일:줄) | 의미 |
|---|---|---|---|
| `firstChapterModifiers` | None, None, None, Rain, Wind, None, Blackout, DustStorm, FullMoon, Festival | BC:319-323 · BCA:359 · GE:244-259 | 1-1~1-10 스테이지 기믹 |
| 기믹 효과 수치 | 아래 1.5 표 기믹 칸 | BC:331-366 · 평소 드러남 반경 BC:285 · 적용 GM:46 · GM:216-264 | 비 · 바람 · 정전 · 먼지 폭풍 · 보름달 · 축제의 밤 |
| `killsToBossBase` · `killsToBossPerStage` · `killsToBossMax` | 15 · 0.6 · 40 | BC:74·76·78 | 보스 처치 수 = clamp(15 + floor((s−1) × 0.6), 1, 40) (BC:422-423) |
| `stageHpGrowth` · `stageDamageGrowth` | 1.11 · 1.08 | BC:80·82 | 체력 배율 = 1.11^(s−1), 피해 배율 = 1.08^(s−1). s ≤ 30은 첫 구간만 (`stageGrowthTaperStage` 30, BC:84 · BC:438-454) |
| `stageSpeedGrowth` · `maxSpeedMultiplier` | 0.01 · 1.35 | BC:96·98 | 이동 배율 = clamp(1 + 0.01 × (s−1), 0.1, 1.35) (SIM:560) |
| `rainEnemySpeedScale` · `windEnemySpeedScale` | 0.85 · 1.2 | BC:332·350 · GM:219-224 | 생성 때 이동 속도에 더 곱함 (SIM:987) |
| `waveSizeBase` · `waveSizePerStage` · `waveSizeMax` | 5 · 0.25 · 14 | BC:104·106·108 | 웨이브 크기 = RoundToInt(clamp(5 + floor(0.25 × (s−1)), 1, 14) × 기믹 배율) (SIM:562-565) |
| `dustStormWaveSizeScale` | 1.3 | BC:358 · GM:227 | 먼지 폭풍 웨이브 크기 배율 (상한 14 적용 뒤에 곱함) |
| `spawnIntervalBase` · `spawnIntervalPerStage` · `spawnIntervalMin` | 1.1 · 0.02 · 0.45 | BC:110·112·114 | 생성 간격 = max(0.45, 1.1 − 0.02 × (s−1)) 초 (SIM:566) |
| `waveRestBase` · `waveRestMin` · 스테이지당 감소 | 3.5 · 2 · 0.05 (코드 상수) | BC:116·118 · SIM:567 | 웨이브 휴식 = max(2, 3.5 − 0.05 × (s−1)) 초 |
| `firstWaveDelay` | 1.5 | BC:102 · SIM:528 | 첫 웨이브 전 대기(초) |
| `bossPhaseSpawnScale` | 0.5 | BC:128 · SIM:911 | 보스 등장 뒤 일반 생성 진행 속도 배율 |
| `dustStormEnemyId` · `dustStormWeightScale` | `enemy_dust` · 3 | BC:354·356 · GM:232 | 먼지 폭풍: 먼지뭉치 가중치 ×3 |
| `blackoutPhasingWeightScale` | 2.5 | BC:340 · GM:233 | 정전: Phasing 망각체(망각의 그림자) 가중치 ×2.5 |
| 보스 선택 | 보스 풀[(s−1) % 보스 풀 수] | SIM:971 · 풀 SIM:694-698 | 보스 풀 = isBoss이고 minStage ≤ s, 목록 순서 (1~2: 큰그림자 1종, 3~: 큰그림자 · 여왕) |
| 보스 체력 | def.hp × 체력 배율 × 보호 배율 | SIM:980 | 보스 피해 = def.damage × 피해 배율 (SIM:988) |
| 튜토리얼 보호 | 적 체력 ×0.6 · 수선점 최소 1 · s ≤ 3 | BT:88 · MS:70·474-478 · SIM:405-411 | 튜토리얼 진행 중(FTUE 0단계, 또는 `BattleProtection` 단계 `ftue_first_battle` 이후 졸업 전)일 때만 (TUT:110-136 · META:1804·1817). 보스 · 분열체 · 부하 포함 |
| `stageGoldBase` · `stageGoldGrowth` | 60 · 1.13 | BC:388·390 · BS:743-744 | 기본 단추 = floor(60 × 1.13^(s−1)) |
| 클리어 단추 | floor(기본 단추 × (1 + 보너스% / 100)) | BS:712 · BS:612-617 | 보너스% = 편성 시너지 `GoldGainPercent` + 축제의 밤 50 (`festivalGoldPercent`, BC:364), 하한 −90 |
| `stageCandy` · `stageCandyMilestone` · `stageCandyMilestoneInterval` | 3 · 10 · 10 | BC:392·394·395 · BS:714-715 | 별빛 사탕: s % 10 = 0이면 10, 아니면 3 |
| 사탕 이벤트 배율 | ×2 (배율 > 1일 때만) | BS:719-720 · EVT:177·690 · META:3671-3684 | floor(사탕 × 배율). '별이 머문 밤 산책'(데이터 문구 "매달 1일부터 14일까지") 기간이고 `Events` 기능(3-1 클리어 또는 설치 3일째, META:462)이 열렸을 때만 |
| `stageClothChance` · `stageRibbonChance` | 0.35 · 0.25 | BC:397·399 · BS:762-766 | 클리어마다 수선 천 35% (1~2개), 리본 조각 25% (1개), 따로 굴림 (ITEM:16·44) |
| `ClearDropHook` | 연결 안 됨 | BS:287·643 · CC:17-19 | 테스트 밖 코드에 넣는 곳 없음 → 위 재료 굴림이 늘 쓰인다 (`lostChest.dropChance` 0, META:4117) |
| `failGoldRatio` | 0.3 | BC:401 · BS:769-775 | 실패 단추 = floor(기본 단추 × (1 + 보너스%/100) × 0.3 × clamp01(처치 수 / 보스 처치 수)), 1 미만이면 없음 |
| `clearAffinity` | 1 | BC:403 · BS:778-788 | 클리어 때 편성(지킴이 · 덱)된 보유 계보 호감도 +1 |

### 1.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `EnemyDef.id` · `name` | string | 망각체 ID · 표시명 (BC:10-11) | `enemy_dust` · 먼지뭉치 |
| `EnemyDef.hp` | float | 1스테이지 기준 체력. × 체력 배율 (BC:15 · SIM:980) | 34 |
| `EnemyDef.speed` | float | 이동 속도. × 이동 배율 × 기믹 이동 배율 (BC:17 · SIM:987) | 0.3 |
| `EnemyDef.damage` | float | 수선점에 주는 피해. × 피해 배율 (BC:19 · SIM:988) | 4.4 |
| `EnemyDef.isBoss` | bool | true면 보스 풀, 아니면 일반 풀 (BC:20 · SIM:695-706) | `boss_shadow` = true |
| `EnemyDef.minStage` | int | 이 스테이지부터 풀에 들어감 (BC:24 · SIM:694) | 태엽쥐 8 |
| `EnemyDef.spawnWeight` | float | 일반 풀 추첨 가중치, 보스는 무시 (BC:26 · SIM:699-706) | 먼지뭉치 5 |
| `EnemyDef.gimmicks` | `EnemyGimmick` (flags) | 망각체 기믹 (BC:37 · GE:263-285) | 태엽쥐 = Mender + Hastener (96) |
| `EnemyDef.shieldAmount` | float | Shielded 보호막, 체력 배율과 같이 곱함 (BC:39 · GM:33-34) | 골무 껍데기 30 |
| `EnemyDef.splitInto` · `splitCount` | string · int | Splitter 처치 때 나뉘는 적 · 수 (BC:41-42 · GM:197-212) | `enemy_tangle` · 2 |
| `EnemyDef.minionId` · `minionInterval` · `minionCount` | string · float · int | 보스 부하 (BC:52-56 · SIM:1096-1114) | `enemy_moth` · 6 · 2 |
| `BattleConfig.firstChapterModifiers` | `StageModifierType[]` | 1장 스테이지별 기믹 (BC:319-323) | 1-4 = `Rain` |
| `MetaDatabase.features[].conditions[]` | `MetaCondition` | type 4 = `StageCleared`, target = 스테이지 번호. 조건 중 하나만 맞아도 열림 (GE:487 · FG:262-264) | 원정: target 4 |
| `MetaDatabase.lostChest.chapterChests[]` | chapter · rewards | 챕터 보스(x-10) 클리어 후 상자 내용 (META:4192-) | chapter 1 (META:4193-4210) |
| `MetaDatabase.storyChapters[]` | chapter · firstReadRewards | 동화책 장, 처음 읽은 선물 (META:4023-) | 1장 "불이 켜진 골목" |

### 1.5 스테이지별 표

**망각체 기본값** (BC:486-551 = BCA:15-245)

| 망각체 | id | 체력 | 이동 | 피해 | 첫 등장 | 가중치 | 기믹 |
|---|---|---|---|---|---|---|---|
| 먼지뭉치 | `enemy_dust` | 34 | 0.30 | 4.4 | 1-1 | 5 | 없음 |
| 좀나방 | `enemy_moth` | 21 | 0.48 | 3.3 | 1-2 | 3 | FabricEater · 잔향이 피해 ×2 |
| 엉킨 실뭉치 | `enemy_tangle` | 47 | 0.30 | 5.5 | 1-3 | 3 | Splitter (같은 `enemy_tangle` 2마리로 분열, 체력 ×0.4) |
| 녹얼룩 | `enemy_rust` | 98 | 0.19 | 8.8 | 1-4 | 2 | Rusting (반경 0.55 · 30%) |
| 골무 껍데기 | `enemy_thimble` | 44 | 0.22 | 6.6 | 1-5 | 2 | Shielded (보호막 30) |
| 망각의 그림자 | `enemy_wisp` | 39 | 0.38 | 6.6 | 1-6 | 2 | Phasing |
| 태엽쥐 | `enemy_windup` | 39 | 0.24 | 4.4 | 1-8 | 1.5 | Hastener + Mender (반경 0.6 · 이동 +25% · 초당 3%) |
| 망각의 큰그림자 (보스) | `boss_shadow` | 416 | 0.16 | 11 | 1-1 | - | Enrage + LampDrain |
| 좀나방 여왕 (보스) | `boss_mothqueen` | 364 | 0.20 | 9.9 | 보스 풀 1-3 · 첫 등장 1-4 | - | Enrage + FabricEater · 부하 좀나방 2 / 6초 |

**스테이지별** — 1.3의 공식을 Python 스크립트로 계산 (`BCA` 값 사용, float 연산은 float32로 맞춤). 확률 = 기믹 반영 가중치 ÷ 그 스테이지 일반 풀 가중치 합 (소수 첫째 자리 반올림). 단추는 편성 시너지 `GoldGainPercent` 0 기준. "공통" = 수선 천 35% (1~2) · 리본 조각 25% (1) · 편성 계보 호감도 +1.

| 스테이지 | 기믹 | 등장 망각체 (확률 %) | 보스 처치 수 | 보스 | 적 체력 · 피해 배율 | 보스 체력 | 웨이브 크기 · 생성 간격 · 휴식 | 클리어 단추 | 기타 보상 | 이 스테이지 클리어로 열리는 기능 |
|---|---|---|---|---|---|---|---|---|---|---|
| 1-1 | 없음 | 먼지뭉치 100 | 15 | 망각의 큰그림자 | 체력 ×1.000 · 피해 ×1.000 · 이동 ×1.00 | 416.0 (보호 시 249.6) | 5 · 1.10초 · 3.50초 | 60 | 별빛 사탕 3 · 공통 | 친구 강화 (META:52) |
| 1-2 | 없음 | 먼지뭉치 62.5 · 좀나방 37.5 | 15 | 망각의 큰그림자 | ×1.110 · ×1.080 · 이동 ×1.01 | 461.8 (보호 시 277.1) | 5 · 1.08초 · 3.45초 | 67 | 별빛 사탕 3 · 공통 | 자동 전투 (개방 보상 별조각 30, META:70·87-91) · 수선 바구니 (META:92) |
| 1-3 | 없음 | 먼지뭉치 45.5 · 좀나방 27.3 · 엉킨 실뭉치 27.3 | 16 | 망각의 큰그림자 | ×1.232 · ×1.166 · 이동 ×1.02 | 512.6 (보호 시 307.5) | 5 · 1.06초 · 3.40초 | 76 | 별빛 사탕 3 · 공통 | 잃어버린 친구 찾기 (META:110) · 편성 (META:128) · 시너지 (META:150) — 편성 · 시너지는 FTUE 11단계로도 열림 |
| 1-4 | 비 오는 밤 (`Rain`): 적 이동 ×0.85 · 비 특성 +25% · 빛 특성 −10% | 먼지뭉치 38.5 · 좀나방 23.1 · 엉킨 실뭉치 23.1 · 녹얼룩 15.4 | 16 | 좀나방 여왕 | ×1.368 · ×1.260 · 이동 ×1.03 (비 ×0.85 추가) | 497.8 | 5 · 1.04초 · 3.35초 | 86 | 별빛 사탕 3 · 공통 | 원정 (META:172) |
| 1-5 | 바람 (`Wind`): 적 이동 ×1.2 · 잔향이 이동 +30% | 먼지뭉치 33.3 · 좀나방 20.0 · 엉킨 실뭉치 20.0 · 녹얼룩 13.3 · 골무 껍데기 13.3 | 17 | 망각의 큰그림자 | ×1.518 · ×1.360 · 이동 ×1.04 (바람 ×1.2 추가) | 631.5 | 6 · 1.02초 · 3.30초 | 97 | 별빛 사탕 3 · 공통 | 빠른 수선 (META:190) · 수선점 출석부 (META:208, FTUE 졸업으로도 열림) |
| 1-6 | 없음 | 먼지뭉치 29.4 · 좀나방 17.6 · 엉킨 실뭉치 17.6 · 녹얼룩 11.8 · 골무 껍데기 11.8 · 망각의 그림자 11.8 | 18 | 좀나방 여왕 | ×1.685 · ×1.469 · 이동 ×1.05 | 613.4 | 6 · 1.00초 · 3.25초 | 110 | 별빛 사탕 3 · 공통 | 2배속 (META:230) · 불 밝히기 방식 (META:248) |
| 1-7 | 정전 (`Blackout`): 그림자 가중치 ×2.5 · 저절로 드러나는 반경 0.85 → 0.6 · 빛 특성 +30% | 먼지뭉치 25.0 · 좀나방 15.0 · 엉킨 실뭉치 15.0 · 녹얼룩 10.0 · 골무 껍데기 10.0 · 망각의 그림자 25.0 | 18 | 망각의 큰그림자 | ×1.870 · ×1.587 · 이동 ×1.06 | 778.1 | 6 · 0.98초 · 3.20초 | 124 | 별빛 사탕 3 · 공통 | 수선 일지 (META:266) · 전투 기록 (META:284) |
| 1-8 | 먼지 폭풍 (`DustStorm`): 먼지뭉치 가중치 ×3 · 웨이브 ×1.3 · 모든 유닛 사거리 −15% · 천 특성 +20% | 먼지뭉치 52.6 · 좀나방 10.5 · 엉킨 실뭉치 10.5 · 녹얼룩 7.0 · 골무 껍데기 7.0 · 망각의 그림자 7.0 · 태엽쥐 5.3 | 19 | 좀나방 여왕 | ×2.076 · ×1.714 · 이동 ×1.07 | 755.7 | 8 (6 × 1.3 = 7.8 → 8) · 0.96초 · 3.15초 | 141 | 별빛 사탕 3 · 공통 | 수선점 상점 (META:301) |
| 1-9 | 보름달 (`FullMoon`): 불 밝히기 게이지 충전 +30% · 잔향이 유지 +25% · 밤 특성 +20% | 먼지뭉치 27.0 · 좀나방 16.2 · 엉킨 실뭉치 16.2 · 녹얼룩 10.8 · 골무 껍데기 10.8 · 망각의 그림자 10.8 · 태엽쥐 8.1 | 19 | 망각의 큰그림자 | ×2.305 · ×1.851 · 이동 ×1.08 | 958.7 | 7 · 0.94초 · 3.10초 | 159 | 별빛 사탕 3 · 공통 | 없음 |
| 1-10 | 축제의 밤 (`Festival`): 클리어 단추 +50% · 치장/소리 특성 +15% | 먼지뭉치 27.0 · 좀나방 16.2 · 엉킨 실뭉치 16.2 · 녹얼룩 10.8 · 골무 껍데기 10.8 · 망각의 그림자 10.8 · 태엽쥐 8.1 | 20 | 좀나방 여왕 | ×2.558 · ×1.999 · 이동 ×1.09 | 931.1 | 7 · 0.92초 · 3.05초 | 270 (180 × 1.5) | 별빛 사탕 10 · 공통 · 챕터 1 상자 · 동화책 1장 | 잔향이 던전 (META:319) · 진화 제작 (META:337) · 도감 보상 (META:355) |

- 보스 체력 "보호 시" = 튜토리얼 보호가 걸린 1-1~1-3에서 × 0.6 (1.3 튜토리얼 보호 행). 보호가 없으면 앞의 값.
- 이벤트 '별이 머문 밤 산책'이 켜져 있으면 별빛 사탕은 1-1~1-9 = 6, 1-10 = 20 (1.3 사탕 이벤트 배율 행).
- 실패 단추 최대 (진행도 1, 시너지 0): 18 · 20 · 22 · 25 · 29 · 33 · 37 · 42 · 47 · 81 (1-1 → 1-10). 1-10은 축제 +50%가 실패에도 붙는다 (BS:773).
- 클리어 단추 · 사탕 · 재료 · 호감도는 클리어할 때마다 받는다 (BS:641-645). 기능 개방 · 챕터 상자 · 동화책은 한 번만.
- 기능 이름은 `META` `features[].name` 그대로. 1장 기능 조건에는 `alsoRequire`가 없다 (META:52-372).
- **챕터 1 상자** (1-10 클리어, 아직 안 열었을 때 팝업 → [받기]로 지급, CC:114-126 · 168-178): 별조각 100 · 수선 천 10 · 리본 조각 10 · 기억의 실 2 (META:4193-4210, ITEM:16·44·89). 옛 세이브가 가이드 퀘스트 22번째를 이미 받았으면 연 것으로 처리한다 (CC:185-196).
- **동화책 1장** "불이 켜진 골목" (1-10 클리어, 아직 안 봤을 때 팝업, 튜토리얼 중이면 졸업까지 미룸, STORY:123-138): 처음 읽은 선물 별조각 50 (META:4024-4041).
- **가이드 퀘스트** (순서대로 하나씩 받음, GQ:8-10) 중 1장 스테이지 조건: 3번째 1-1 단추 300 · 6번째 1-2 단추 500 · 8번째 1-3 단추 800 · 12번째 1-4 단추 1,200 · 17번째 1-7 별조각 30 · 22번째 1-10 별조각 100 (META:533·578·608·668·743·818).
- **보스 안내 문구** (META:5490-5497): 보스 등장 1초 뒤 2.4초 표시. 튜토리얼 중이 아니고 (그 보스를 처음 만남 또는 연속 실패 ≥ 1)일 때만 (BHR:11·13·23-27).

### 1.6 추출 못 한 것
- 스테이지별 실제 소요 시간 · 웨이브 수 · 클리어율: 전투 시뮬레이션을 돌리지 않아 없음. 확률은 생성 추첨 확률이며, 분열체 · 부하 · 동시 망각체 상한(SIM:928-929)으로 실제 등장 비율은 다를 수 있다.
- 편성 시너지 `GoldGainPercent`가 있을 때의 단추 값: 편성에 따라 달라 표에 넣지 않음 (BS:614).
- 튜토리얼 보호가 실제 플레이에서 몇 번째 스테이지까지 걸리는지: 튜토리얼 졸업 시점에 달려 있어 코드만으로 정해지지 않음.
- 가이드 퀘스트 · FTUE 단계(`ftueSteps`)가 기능 개방 뒤에 이어지는 흐름: 이 문서 범위 밖 (각 시스템 문서).
- 소수점: 배율 · 보스 체력은 float32로 맞춰 계산했으나 Unity 런타임 값과 소수 넷째 자리 이하가 다를 수 있다.

## 2. 기획 방향 (To-be) — DD

### 2.1 규칙
```
IF <조건>
  → <처리>
```

### 2.2 상태 전이
```
[State A] ──(트리거)──→ [State B]
```

### 2.3 UI 플로우 / 디스크립션
1. <UI 요소> : 기능 / 인터랙션 규칙
   - 탭 → <연결 화면>
   - 비활성 조건 →

### 2.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|

## 3. 차이 → 결정 필요 — DD 작성, 대표 결정

| # | 항목 | ① 현재 구현 | ② 기획 방향 | 영향 (코드 · 데이터 · 세이브) | DD 추천 | 결정 |
|---|---|---|---|---|---|---|

## 4. 예외 처리 / 엣지 케이스
| 케이스 | 처리 |
|---|---|
| 네트워크 단절 | |
| 앱 강제 종료 | |
| 기기 시간 조작 | |

## 5. 어뷰징 검토
| 시나리오 | 대응 |
|---|---|

## 6. 확장성 / 오픈 이슈
- [ ]
