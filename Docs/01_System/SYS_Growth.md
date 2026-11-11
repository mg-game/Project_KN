# SYS_Growth

| 항목 | 값 |
|---|---|
| 상태 | Draft |
| 작성자 | ① 구현: 기개발 · ②③: DD |
| 최종 수정 | 2026-11-11 |
| 관련 테이블 | - |
| 관련 문서 | [`KN_Glossary`](../00_Overview/KN_Glossary.md) · [`SYS_InGameBattle`](./SYS_InGameBattle.md) (전투 쪽 수선점 내구 · 계보 공명 배율) |
| 근거 | 코드 커밋 `4b73a5a` (`feature/lost-and-lovely-import`) — 경로 약칭 표 참고 |

> 구조(D-26): **① 현재 구현** = 지금 코드가 실제로 하는 일 / **② 기획 방향** = 기획서가 원하는 모습 / **③ 차이** = 둘이 다른 곳, 대표가 결정.
> 결정이 나면 ②에 반영하고 ③ 행에 `D-n`을 적는다. 코드 수정은 지시함(#3)으로. 용어는 [`KN_Glossary`](../00_Overview/KN_Glossary.md)를 따른다.

## 0. 개요
- 목적 (이 시스템이 해결하는 문제 / 유저 동기):
- 핵심 루프 내 위치:
- 관련 필라 (SYS_InGameBattle §1 P1~P6):

## 1. 현재 구현 (As-built) — 기개발

> 근거: `feature/lost-and-lovely-import` @ `4b73a5a`. 코드가 실제로 하는 일만 적었다 (해석 · 제안 · 기획 비교 없음 — ② · ③은 DD). 범위: 수선점 강화 `GrowthSystem` · 전투력 `PowerSystem` · 강해지기 `StrengthenAdvisor`, 그리고 이 셋이 읽는 계보 성장 식. 코드 주석의 "친구" = 유실자, "의상" = 외형변경(코드 `Costume`).
> 수치 출처: `GameConfig.asset`(`GCA`)에 있는 수선점 필드는 코드 기본값과 모두 같다. 계보 성장 필드(`characterMaxLevel` ~ `resonanceBonusPercent`)는 자산에 **없어 코드 기본값**이 쓰인다. `PowerConfig`는 `MetaDatabase.asset`(`META`) 값이 코드와 **다른 필드가 있어 자산 값**이 쓰인다 (1.3에 둘 다 적음). 세 자산 모두 `Scenes/Main.unity`가 참조한다.

**파일 약칭** (모두 `Assets/_Project/` 아래)

| 약칭 | 경로 |
|---|---|
| `GROW` | `Scripts/Systems/GrowthSystem.cs` |
| `PS` · `SA` | `Scripts/Systems/Meta/PowerSystem.cs` · `Scripts/Systems/Meta/StrengthenAdvisor.cs` |
| `GC` · `GCA` | `Scripts/Data/GameConfig.cs` · `Data/GameConfig.asset` |
| `MDB` · `META` | `Scripts/Data/MetaDatabase.cs` (`PowerConfig`) · `Data/MetaDatabase.asset` |
| `CS` · `CDB` · `CDA` | `Scripts/Systems/Characters/CharacterSystem.cs` · `Scripts/Data/CharacterDatabase.cs` · `Data/CharacterDatabase.asset` |
| `IDB` · `IDA` | `Scripts/Data/ItemDatabase.cs` · `Data/ItemDatabase.asset` |
| `INV` · `CUR` | `Scripts/Systems/InventorySystem.cs` · `Scripts/Systems/CurrencySystem.cs` |
| `RMS` · `OFF` | `Scripts/Systems/RewardMailSystem.cs` · `Scripts/Systems/OfflineRewardSystem.cs` |
| `FORM` · `BS` · `DUN` | `Scripts/Systems/Battle/FormationSystem.cs` · `BattleSystem.cs` · `DungeonSystem.cs` (같은 폴더) |
| `SIM` · `ST` | `Scripts/Systems/Battle/BattleSimulator.cs` · `Scripts/Systems/Battle/BattleSimulator.Status.cs` |
| `BOOT` · `PD` | `Scripts/Core/GameBootstrap.cs` · `Scripts/Core/PlayerData.cs` |
| `SAVE` · `SAVEC` · `GE` | `Scripts/Core/SaveData.Battle.cs` · `SaveData.Characters.cs` · `GameEnums.cs` (같은 폴더) |
| `PT` · `MS` | `Scripts/Systems/Meta/ProgressTracker.cs` · `Scripts/Systems/Meta/MetaSession.cs` |
| `GP` · `SP` | `Scripts/UI/Main/GrowthPanel.cs` · `Scripts/UI/Meta/StrengthenPopup.cs` |

### 1.1 규칙

**A. 수선점 레벨업** — 재화는 단추(`CurrencyType.Gold`)
```
비용(L → L+1) = levelUpBaseCost × levelUpCostGrowth^(L−1)       # GROW:40-41 (올림 없음, 소수 그대로)
비용(L, n칸)  = Σ i=0..n−1 비용(L+i)                              # GROW:44-49
TryLevelUp(n)                                                     # GROW:88-98
  n = min(n, maxLevel − Level)
  IF n ≤ 0                    → false
  ELSE IF 단추 < 비용(Level, n) → false, 아무것도 안 함 (일부만 올리지 않음)   # CUR:30
  ELSE                        → 단추 −비용, level += n, LevelChanged(이전, 새)
LevelUpMax()                                                      # GROW:101-105 · 52-66
  n = Level부터 한 칸씩 비용을 더해 보유 단추를 넘지 않는 최대 칸 수 → n > 0이면 TryLevelUp(n), 아니면 0
UI: x1 = TryLevelUp(1) (길게 누르면 반복) · x10 = TryLevelUp(10) · MAX = LevelUpMax()   # GP:289 · 305 · 320 · 331
```

**B. 수선점 레벨이 바뀐 뒤** (`LevelChanged` 구독)
```
LevelChanged(이전, 새)                                            # GROW:19 · 96
  → 전투력 더러움 표시                                             # PS:86 · 449
  → 레벨 달성 우편: (이전/10 + 1) × 10부터 새 레벨까지 10레벨마다 별조각 50, 키 level_reward_{레벨} 1회 (간격 ≤ 0이면 없음)   # RMS:21-33
  → 진행 기록 ShopLevelUp += 새 − 이전                              # PT:806-810
  → 스테이지 전투 중이면 다음 Tick에 수선점 내구 최대치 교체           # BS:460-464
     늘어난 만큼 현재 내구도도 채우고, 최대치를 넘으면 자른다          # SIM:782-796
```

**C. 초당 단추 · 방치 지급**
```
GoldPerSecond(L) = baseGoldPerSecond × goldPerSecondGrowth^(L−1)   # GROW:73-76
                 × (1 + 보유 재료 goldBonusPercent 합 / 100)        # GROW:68 · INV:83-99 (개수 > 0인 종류마다 1번)
                 × (1 + 0.1 × (max(1, 최고 스테이지) − 1))           # GROW:71 (단리)
방치 지급 (앱이 켜져 있는 동안 매 Update)                            # BOOT:199 · 250-264
  누적 += GoldPerSecond × Time.deltaTime
  IF 누적 시간 ≥ 1초 OR (누적 시간 ≥ 0.2초 AND 누적 ≥ 1) → 누적분 지급, 0으로 (자동 저장 직전에도 지급, BOOT:244)
GPS를 곱하는 다른 곳: 오프라인 보상 = GPS × 비운 시간(최대 8시간) × 0.5, 60초 미만이면 없음 (OFF:34-39, GC:30-33) ·
  GoldMinutes 보상 = GPS × 60 × 분 (MetaRewardService.cs:346-350) · 수선 바구니 시간당 단추 (IdleChestSystem.cs:718)
```

**D. 수선점 내구** (용어집: 수선점 내구 = `ShopHp`. 여기서는 최대치 `ShopMaxHp`)
```
CalcShopMaxHp(L) = min(float.MaxValue / 4, 300 × 1.04^(max(1, L) − 1))   # GROW:79-80
스테이지 전투 · 잔향이 던전 시작: BattleSetup.ShopMaxHp = CalcShopMaxHp(수선점 레벨)   # BS:563 · DUN:570
전투 안 최대치 = max(1, 위 값 × max(0.1, 1 + 시너지 ShopMaxHpPercent 합 / 100))   # SIM:510-512 · ST:103
```

**E. 계보 성장 → 능력치** (전투력이 읽는 값)
```
계보 레벨 비용(L → L+1) = ceil(50 × 1.09^(L−1) − 1e-6) 단추, 미보유 OR 최대 레벨이면 0   # CS:199-206 · 209-221
계보 TryLevelUp: 계보 레벨업 기능(FeatureId.CharacterLevelUp) 잠김 OR 미보유 OR Lv.100 OR 단추 부족 → false, 성공 시 +1   # CS:34 · 233-243
새 계보 시작 레벨 = max(1, floor(0.8 × 보유 계보 레벨 상위 3개 평균)), 중복 획득 = 기억편(shards) 지급   # CS:106-135 · 144-179
계보 공명 비용(r → r+1) = max(1, 20 × (r + 1)) 기억편, r < 10일 때만   # CS:274-297
GetStats(형태)                                                     # CS:368-389
  공격력 = 형태 공격 × (1 + 8 × (레벨 − 1) / 100) × (1 + (5 × 공명 레벨 + 외형변경 %) / 100)
  체력   = 형태 체력 × (1 + 8 × (레벨 − 1) / 100) × (1 + 5 × 공명 레벨 / 100)   (나머지 능력치는 형태 값 그대로, 미보유 계보는 Lv.1)
외형변경 % = 그 계보의 "보유" 외형 attackBonusPercent 합 (장착 여부와 무관)   # CS:392-402
진화(EvolutionSystem): 제작 완료 시 UnlockForm으로 형태 해금 (CS:181-187). 여기서는 IsFormUnlocked · 강해지기 진화 줄만 읽는다
```

**F. 유닛 전투력** (`ComputeUnitPower`)
```
공격   = max(0, 공격력) × max(0, 공명 배율) × max(0.1, 1 + 공%)      # PS:429-444
간격   = max(0.1, 공격 간격),  속도 = max(0.2, 1 + 공속%)
공격 몫 = 공격 / 간격 × 속도 × 10 × (1 + 치확 × 치피)   (치확은 0~1로 자름, 치피 ≥ 0, 모두 % ÷ 100)
방어 몫 = max(0, 체력) × max(0.1, 1 + 체%)
유닛   = 공격 몫 + 방어 몫   (NaN OR ∞ → 0)
%값 = 그 계보 · 형태 몫 시너지 보정 SynergySystem.BuildModifiers(StageModifierType.None) (스테이지 기믹 제외, 기본 치명타 제외)   # PS:22 · 493-504 · 546-555
공명 배율 = 1 + max(0, Σ(4 + 2 × 계보 공명 레벨)) / 100, Σ = 지킴이 쪽 계보 중 잔향이 쪽에도 있는 계보   # PS:507 · FORM:24-25 · 368-386
GetFormPower(형태) = 보정 없음 · 배율 1 (편성 카드 · 목록 · 가챠 결과)                     # PS:133-139
```

**G. 팀 전투력** (`TeamPower`)
```
대상 칸 = 지킴이 5칸 + 잔향이 덱 6칸 중 "보유 계보의 해금 형태"인 칸, 잠긴 덱 칸은 빈칸   # PS:510-514 · 558-566 · FORM:78
g = Σ 지킴이 유닛,  e = Σ 잔향이 유닛  (GuardianPower = Round(g), EchoPower = Round(e), 가중치 전)   # PS:520-539
공격 합   = g × guardianWeight + e × echoWeight                              # PS:540
수선점 몫 = 수선점 내구 최대치 × max(0.1, 1 + 시너지 ShopMaxHpPercent / 100) × shopHpWeight   # PS:535-541
IF shopShareCap > 0 → 수선점 몫 = min(수선점 몫, max(0, 공격 합) × shopShareCap)   # PS:542
팀 = Round(공격 합 + 수선점 몫)   (Round: ≤ 0 OR NaN → 0, .5는 0에서 먼 쪽)       # PS:446-447 · 543
→ 지금 값: 팀 = g + 0.6 × e + min(0.3 × 보정 내구, g + 0.6 × e) — 빈 편성 = 0
전투의 AttackMultiplier에는 응원 임시 공격력이 더 곱해지지만 전투력에는 넣지 않는다   # BS:564-565
```

**H. 권장 전투력 · 평가**
```
rec(s) = recommendedBase × g^(앞 걸음) × gL^(중간 걸음) × gL2^(뒤 걸음), 걸음 합 = s − 1   # PS:252-281
  T = recommendedTaperStage, T2 = recommendedTaperStage2. T ≤ 0이면 지수 하나 (T2 무시). gL · gL2 ≤ 0이면 앞 구간 값, 모든 성장률 최소 1
→ 지금 값: rec(s) = 277.184 × 1.1043^min(s−1, 29) × 1.0553^(min(s−1, 150) − min(s−1, 29)) × 1.0228^max(0, s−151)
던전 권장(층 f): IF dungeonRecommendedBase ≤ 0 → 0 (줄 없음) ELSE base × max(1, growth)^(f−1)   # PS:288-293
  → 지금 값: 83.7 × 1.177^(f−1). 던전 평가는 팀 전투력이 아니라 EchoPower로 한다           # PS:296
평가(전투력 p, 권장 r)                                                        # PS:368-378 · GE:617-625
  IF r ≤ 0 OR p / r ≥ comfortableRatio − 1e-6 → 여유 (Comfortable)
  ELSE IF p / r ≥ challengeRatio − 1e-6   → 도전 (Challenge)
  ELSE                                  → 어려움 (Hard)
표기: 절댓값 p < 1,000,000 → 콤마 정수, 이상 → NumberFormatter.Format   # PS:423-424
```

**I. 다시 계산 · 이벤트 · 미리보기**
```
더러움 표시: Formation · Characters · Synergy · Costumes의 Changed, Growth.LevelChanged   # PS:83-87
TeamPower 읽기 OR Refresh() → 더러우면 그때 한 번 계산 (EnsureComputed)      # PS:93-100 · 451-460
Refresh()는 MetaSession.Tick이 매 프레임 부른다                              # MS:239
  첫 계산 → 기준값만 저장, 이벤트 없음. 그 뒤 값이 바뀌면 Changed(이전, 새)     # PS:389-403
미리보기 (세이브를 잠깐 바꿔 계산 후 되돌림, 이벤트 없음)
  PreviewPlace(쪽, 칸, 형태): 놓은 뒤 팀 전투력, 마지막 1건 캐시               # PS:164-188
  PreviewLevelUp(계보, n): 레벨 +n(최대에서 멈춤) 증가분 · PreviewResonance(계보): 공명 +1 증가분 (시너지 · 공명 배율 다시 셈)   # PS:200-243
  PreviewShopLevels(n): 수선점 내구만 CalcShopMaxHp(L+n)으로 바꾼 증가분 (상한에 걸리면 0)   # PS:303-315
  PreviewAutoFormation(): BuildAutoFormation 결과의 팀 전투력, 더러워질 때까지 캐시   # PS:323-356
```

**J. 강해지기 줄 고르기** (`GetRows`)
```
호출: 강해지기 시트가 열릴 때 + 열린 동안 1초마다, max = min(4, 줄 칸 수)     # SP:30-32 · 208
  IF max ≤ 0 OR PowerSystem · CharacterSystem 없음 → 빈 목록               # SA:139
최소 증가 = ceil(max(0, 팀 전투력) × 0.005)                                  # SA:141
후보 모으는 순서: 수선점 → 계보 레벨 → 빈 칸 → 시너지 1명 부족 → 계보 공명 → 자동 편성 → 진화 → 약한 방향 → 모자란 재료   # SA:144-152
전투력 줄: 증가 ≤ 0 OR 증가 < 최소 증가 → 버림                                 # SA:244-248
  정렬 = 점수 큰 순, 점수 = 증가 ÷ (1 + 비용 초 / 60), 같으면 StrengthenKind 번호 작은 순   # SA:178-184
안내 줄(증가 0): 진화 가능 → 약한 방향 → 모자란 재료, 같은 종류는 TargetId 순    # SA:186-198
결과 = 전투력 줄 먼저, 그다음 안내 줄, 최대 max줄. 예외 → 경고 로그 + 빈 목록     # SA:154-163
비용 초 = max(0, 비용 − 보유 단추) ÷ max(1, GoldPerSecond)                      # SA:269-273
```

**K. 강해지기 후보 9종** (`StrengthenKind`, GE:752-763)

| 종류 (번호) | 조건 · 고르는 법 | 증가 · 비용 초 | [가기] `FeatureId` · 위치 |
|---|---|---|---|
| `ShopUpgrade` (0) | 수선점 최대 레벨 아님. n = 1에서 시작, 보유 단추 + 120초 × GPS 예산 안의 최대 칸 수 (최대 10, 남은 레벨까지). n = 1은 예산과 무관 | `PreviewShopLevels(n)` · 비용(L, n)의 비용 초 | `ShopUpgrade`, x1 버튼 손가락 · SA:277-293 |
| `LineageLevel` (1) | 계보 레벨업 기능 열림, 편성에 놓인 보유 계보, 최대 레벨 아님. 레벨 수 = 지금 단추로 살 수 있는 만큼 (최대 5, 못 사면 1) | `PreviewLevelUp(계보, n)` · 계보 비용(n)의 비용 초 | `ShopUpgrade` · SA:295-322 |
| `FillSlot` (2) | 첫 빈 지킴이 칸 · 첫 열린 빈 덱 칸 각각. 안 쓴 해금 형태 중 그 칸에 놓을 수 있고 `GetFormPower` 최대 (추천 역할 일치 +1) | `PreviewPlace` − 팀 · 0 | `Formation` · SA:324-360 |
| `SynergyOneShort` (3) | 다음 단계까지 1명 남은 시너지. 들어갈 안 쓴 형태를 추천 역할 쪽 빈 칸 (없으면 구성원 아닌 가장 약한 칸, 그래도 없으면 반대쪽) | 모든 조합 중 증가 최대 1줄 · 0 | `Formation` · SA:363-428 |
| `ResonanceReady` (5) | 편성에 놓인 계보, 기억편 ≥ 공명 비용 | `PreviewResonance` · 0 | `Characters` · SA:430-441 |
| `AutoFormation` (7) | 팀 > 0이면 미리보기 ≥ 1.05 × 팀, 팀 ≤ 0이면 미리보기 > 0 | 미리보기 − 팀 · 0 | `Formation`, 자동 편성 버튼 손가락 · SA:443-450 |
| `EvolutionReady` (4) | 진화 제작 열림, 단서 발견(`ClueFound`) 형태 중 지금 제작 가능 | 계보가 편성에 있으면 max(0, 그 형태 − 편성된 같은 계보 최고 형태 전투력). 0이면 안내 줄 | `Characters` · SA:452-489 |
| `WeakDirection` (8) | `LastDefeat`에 자료 있음 (마지막 실패 기록, 클리어하면 비움, 저장 안 함), 칸 0~4 | 안내 줄, Amount = 그 방향 몫 % (0~100) | `Formation` + 그 칸 반짝 · SA:491-503 |
| `MissingMaterial` (6) | `ClueFound`이면서 제작 불가인 형태 중 모자란 재료 종류 수 최소가 정확히 1 | 안내 줄, TargetId = 그 재료 | `FeatureId.Battle` · SA:505-536 |

### 1.2 상태 전이
```
수선점 레벨 (PlayerData.level)
[Lv.1] ──(TryLevelUp(n) 성공, 단추 −비용)──→ [Lv.1+n] ──…──→ [Lv.1000 = maxLevel]   (내리는 경로 없음, 로드 시 < 1 → 1, PD:79)

계보 레벨 · 계보 공명 (LineageSave)
[시작 Lv. = CatchUpLevel] ──(TryLevelUp, 단추)──→ … ──→ [Lv.100]   ·   [공명 0] ──(TryResonance, 기억편)──→ … ──→ [공명 10]

전투력 캐시 (PowerSystem)
[더러움] ──(TeamPower 읽기 OR Refresh)──→ [계산됨] ──(Changed 5종 중 하나)──→ [더러움]
[보고 전] ──(첫 Refresh)──→ [기준값 저장, 이벤트 없음] ──(값이 바뀐 Refresh)──→ Changed(이전, 새)
```
- `StrengthenAdvisor`: 상태 없음 (호출마다 처음부터 계산, 작업 목록은 호출 끝에 비움 — SA:164-172).

### 1.3 수치 (코드 상수 · 설정값)
| 이름 | 값 | 위치 (파일:줄) | 의미 |
|---|---|---|---|
| `ShopBaseHp` · `ShopHpGrowth` | 300 · 1.04 | GROW:12 · 14 | 수선점 Lv.1 내구 최대치 · 레벨마다 배율 (복리) |
| `StageGoldBonusPerStage` | 0.1 | GROW:16 | 최고 스테이지 1단계마다 초당 단추 +10% (단리) |
| `maxLevel` | 1000 (자산 같음) | GC:14 · GCA:17 | 수선점 최대 레벨 |
| `levelUpBaseCost` · `levelUpCostGrowth` | 10 · 1.12 (자산 같음) | GC:15-16 · GCA:18-19 | 수선점 Lv.1→2 비용 (단추) · 레벨마다 배율 |
| `baseGoldPerSecond` · `goldPerSecondGrowth` | 1 · 1.06 (자산 같음) | GC:19 · 22 · GCA:20-21 | 수선점 Lv.1 초당 단추 · 레벨마다 배율 (복리) |
| `levelRewardInterval` · `levelRewardGem` | 10 · 50 (자산 같음) | GC:26-27 · GCA:22-23 | 10레벨마다 별조각 50 우편 |
| `characterMaxLevel` · `characterLevelUpBaseCost` · `characterLevelUpCostGrowth` | 100 · 50 · 1.09 (자산에 없음 → 코드) | GC:50 · 52-53 | 계보 최대 레벨 · Lv.1→2 비용 · 배율 |
| `attackPerLevelPercent` · `hpPerLevelPercent` | 8 · 8 (자산에 없음 → 코드) | GC:55 · 57 | 레벨당 공격력 · 체력 % (선형) |
| `duplicateShardsByGrade` | 5, 10, 20, 40, 80 (자산에 없음 → 코드) | GC:61 | 중복 획득 기억편 (계보 등급 순) |
| `maxResonanceLevel` · `resonanceCostPerLevel` · `resonanceBonusPercent` | 10 · 20 · 5 (자산에 없음 → 코드) | GC:63 · 65 · 67 | 공명 최대 · 비용 계수 · 레벨당 공격력 · 체력 % |
| `CostumeDef.attackBonusPercent` | 기본 3, 자산 10개 = 3 · 3 · 4 · 4 · 5 · 5 · 5 · 6 · 6 · 8 | CDB:83 · CDA:736-936 | 보유 외형 1개당 계보 공격력 % |
| `ItemDef.goldBonusPercent` | 기본 0, 자산 9개 모두 0 | IDB:27 · IDA:27-193 | 지금 초당 단추 보유 배율 = 1 |
| `SynergyBaseBonusPercent` · `SynergyBonusPerResonance` | 4 · 2 | FORM:24-25 | 편성 계보 공명 공격력 % |
| 지킴이 칸 · 덱 칸 | 5 · 6 (덱 기본 5, 최고 스테이지 ≥ 20이면 6) | SAVE:10-11 · FORM:20-22 · 50 | 전투력 합산 칸 |
| `IdleGoldMinInterval` · `IdleGoldMaxInterval` | 0.2초 · 1초 | BOOT:42 · 44 | 방치 단추 지급 간격 |
| 유닛 공식 상수 | 공격 계수 10, 간격 최소 0.1, 속도 최소 0.2, 공% · 체% 최소 0.1 | PS:437-441 | F 공식 하한 |
| `guardianWeight` · `echoWeight` · `shopHpWeight` | 1 · 0.6 · 0.3 (자산 같음) | MDB:343-345 · META:2074-2076 | 팀 전투력 가중치 |
| `recommendedBase` · `recommendedGrowth` | 코드 300 · 1.12 → 자산 **277.184 · 1.1043** 적용 | MDB:347-348 · META:2077-2078 | rec(1) · 1~30 구간 배율 |
| `recommendedTaperStage` · `recommendedGrowthLate` · `recommendedTaperStage2` · `recommendedGrowthLate2` | 코드 0 · 0 · 0 · 0 → 자산 **30 · 1.0553 · 151 · 1.0228** 적용 | MDB:356-362 · META:2081-2084 | 30~151 구간 · 151 뒤 구간 |
| `comfortableRatio` · `challengeRatio` | 1 · 0.8 (자산 같음), 비교 때 −1e-6 | MDB:350 · 352 · META:2079-2080 · PS:373 | 여유 · 도전 경계 |
| `shopShareCap` | 1 (자산 같음) | MDB:364 · META:2085 | 수선점 몫 ≤ 공격 합. 툴팁은 "읽는 곳이 없다"지만 PS:542가 읽는다 |
| `dungeonRecommendedBase` · `dungeonRecommendedGrowth` | 코드 0 · 1.1 → 자산 **83.7 · 1.177** 적용 | MDB:366-367 · META:2086-2087 | 던전 층 권장 |
| `ShopIncomeWindowSeconds` · `MaxShopLevels` · `LineageLevels` | 120초 · 10 · 5 | SA:52-55 | 수선점 줄 예산 · 최대 칸, 계보 레벨 줄 최대 레벨 수 |
| `AutoFormationRatio` · `MinGainRatio` · 점수 분모 | 1.05 · 0.005 · 60 | SA:57 · 59 · 178 | 자동 편성 줄 조건 · 최소 증가 비율 · 증가 ÷ (1 + 비용 초 / 60) |
| `MaxRows` · `AdviceInterval` | 4 · 1초 | SP:30 · 32 | 강해지기 줄 수 · 갱신 간격 |

**계산 예** (위 식에 현재 값 대입)

| 이름 | 값 | 위치 (파일:줄) | 의미 |
|---|---|---|---|
| 수선점 비용 Lv.1 · 10 · 100 | 10 · 27.73 · 745,734.5 | GROW:40-41 | 다음 1레벨 비용 (단추, 소수 유지) |
| 수선점 내구 Lv.1 · 10 · 100 · 1000 | 300 · 427.0 · 14,568.7 · 약 3.11e19 | GROW:79-80 | float.MaxValue/4 상한 안 닿음 |
| 초당 단추 Lv.1 · 10 · 100 (최고 1스테이지) | 1 · 1.69 · 320.1 | GROW:73-74 | 최고 11스테이지면 × 2 |
| 계보 비용 Lv.1 · 10 · 99 | 50 · 109 · 232,685 | CS:199-206 | 다음 1레벨 비용 (올림 정수) |
| 권장 전투력 s = 1 · 30 · 151 · 200 / 던전 층 1 · 10 | 277 · 4,924 · 3,317,277 · 10,012,142 / 84 · 363 | PS:252-293 | 자산 값 기준 |

### 1.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `PlayerData.level` | int | 수선점 레벨 (PD:28), 로드 시 < 1이면 1 | 1 |
| `PlayerData.currencies[Gold]` | double | 단추 보유량 (PD:29), 레벨업 비용 차감 | 1234.5 |
| `PlayerData.items` | Dictionary string→int | 재료 보유 개수 (PD:31), 개수 > 0이면 `goldBonusPercent` 합산 | `{"mat_cloth": 3}` |
| `BattleSave.highestStage` | int | 도달한 최고 스테이지 (SAVE:43), 초당 단추 배율 | 12 |
| `FormationSave.guardians` · `echoes` | string[5] · string[6] | 편성 형태 ID, 빈칸 null (SAVE:13-14) — 팀 전투력 대상 | `["jjak_base", null, …]` |
| `LineageSave.level` | int | 계보 공유 레벨 (SAVEC:10) | 1 |
| `LineageSave.shards` · `resonance` | int · int | 기억편 (SAVEC:12, 공명 비용) · 계보 공명 레벨 0~10 (SAVEC:14) | 40 · 2 |
| `LineageSave.equippedCostume` | string | 장착 외형 (SAVEC:23) — 능력치 계산에는 쓰이지 않음 | null |
| `PlayerData.unlockedCostumes` | HashSet string | 보유 외형 ID (PD:45) — 외형변경 % 합산 대상 | `["jjak_nightkeeper"]` |
| `GameConfig` 수선점 필드 | int · double | `maxLevel` · `levelRewardInterval`(int), `levelUpBaseCost` · `levelUpCostGrowth` · `baseGoldPerSecond` · `goldPerSecondGrowth` · `levelRewardGem`(double) | 1000 · 10 · 10 · 1.12 · 1 · 1.06 · 50 |
| `GameConfig` 계보 성장 · 공명 | int · double · float · int[] | `characterMaxLevel` · `maxResonanceLevel` · `resonanceCostPerLevel`(int), `characterLevelUpBaseCost` · `characterLevelUpCostGrowth`(double), `attackPerLevelPercent` · `hpPerLevelPercent` · `resonanceBonusPercent`(float), `duplicateShardsByGrade`(int[]) | 100 · 10 · 20 · 50 · 1.09 · 8 · 8 · 5 · [5,10,20,40,80] |
| `PowerConfig` 가중치 · 경계 | float | `guardianWeight` · `echoWeight` · `shopHpWeight` · `comfortableRatio` · `challengeRatio` · `shopShareCap` | 1 · 0.6 · 0.3 · 1 · 0.8 · 1 |
| `PowerConfig` 권장 | double · int | `recommendedBase` · `recommendedGrowth` · `recommendedGrowthLate` · `recommendedGrowthLate2` · `dungeonRecommendedBase` · `dungeonRecommendedGrowth`(double) · `recommendedTaperStage` · `recommendedTaperStage2`(int) | 277.184 · 1.1043 · 1.0553 · 1.0228 · 83.7 · 1.177 · 30 · 151 |
| `StrengthenRow` (런타임, 저장 안 함) | class | Kind · TargetId · Gain(long) · CostSeconds(double) · Nav · Focus · Amount · Slot · Role · CandidateId (SA:10-31) | ShopUpgrade, Gain 312, Amount 3 |

### 1.5 추출 못 한 것
- 수선점 강화 탭 · 버튼의 기능 개방 조건: `GrowthSystem` · `GrowthPanel`에는 확인이 없다. `FeatureId.ShopUpgrade`의 개방 데이터는 추적하지 않았다.
- 형태별 기본 능력치(`CharacterFormDef.attack` · `hp` · `attackInterval`, CDA 값)는 대조하지 않았다 → 실제 유닛 · 팀 전투력 숫자 예시 없음.
- 범위 밖이라 읽지 않은 것: 시너지 보정 `SynergySystem.BuildModifiers` 내부 (도감 영구 보너스 경로 포함), 외형변경 획득 조건(`CostumeSystem`), 진화 제작 조건(`EvolutionSystem`).
- `LastDefeat`를 채우는 규칙(`UI/Battle/DefeatAnalyzer.cs`)과 방향 몫 % 계산은 읽지 않았다.
- 자산의 권장 전투력 값을 만든 "에디터 보정 메뉴"(MDB:346 툴팁)의 계산 방식 — 에디터 코드 미확인.
- 화면 숫자 표시(`MenuText.RateText`, `NumberFormatter.FormatCurrency`)의 반올림 방식 미확인. 수선점 비용은 코드상 소수 그대로다.
- 방치 단추는 `Time.deltaTime`을 쓴다. `Scripts/` 안에 `Time.timeScale`을 바꾸는 코드는 없음(grep). 앱이 백그라운드일 때의 실제 동작은 런타임 확인 안 함.
- 수선점 레벨을 읽는 다른 곳(수선 바구니 보관 상한 `IdleChestSystem.cs:651`, 가이드 퀘스트 `GuideQuestSystem.cs:219`, 진행 기록 `PT:224`, 세이브 요약 `SaveStorage.cs:68`)의 규칙은 각 시스템 문서 범위라 적지 않았다.

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
