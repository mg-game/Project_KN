# SYS_Growth

| 항목 | 값 |
|---|---|
| 상태 | Approved (D-29) |
| 작성자 | ① 구현: 기개발 · ②③: DD |
| 최종 수정 | 2026-11-14 (§3 #2 재검토: B 적용, 유실자 레벨업 재화 "실타래", 수선점 기본 비용 12) · 2026-11-14 (§3 #2 재검토: `SYS_Economy` §3 #8 E안) |
| 관련 테이블 | - |
| 관련 문서 | [`KN_Glossary`](../00_Overview/KN_Glossary.md) · [`SYS_InGameBattle`](./SYS_InGameBattle.md) (전투 쪽 수선점 내구 · 계보 공명 배율) |
| 근거 | 코드 커밋 `4b73a5a` (`feature/lost-and-lovely-import`) — 경로 약칭 표 참고 |

> 구조(D-26): **① 현재 구현** = 지금 코드가 실제로 하는 일 / **② 기획 방향** = 기획서가 원하는 모습 / **③ 차이** = 둘이 다른 곳, 대표가 결정.
> 결정이 나면 ②에 반영하고 ③ 행에 `D-n`을 적는다. 코드 수정은 지시함(#3)으로. 용어는 [`KN_Glossary`](../00_Overview/KN_Glossary.md)를 따른다.

## 0. 개요
- 목적: 모은 재화(단추 · 실타래 · 기억편 · 재료)를 **캐릭터(계보)와 수선점의 힘**으로 바꿔, 막힌 스테이지를 넘게 한다. 유저 동기 = "조금만 더 키우면 다음 스테이지".
- 핵심 루프 내 위치: 전투(자동 진행 · 파밍) → 단추 · 실타래 · 기억편 획득 → **성장** → 전투력 ↑ → 다음 스테이지 → (막힘) → 강해지기 안내 → 성장
- 관련 필라: P3 희귀도 ≠ 서열(저희귀도 최애도 끝까지) · P5 최애 축(개별 성장) · P6 혼자 자기 속도로

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
> 2026-11-14 안 D: 아래 식은 **기준 수입 `IncomePerSecond`**(나누기 전)다. `GoldPerSecond` = 기준 × `goldIncomeShare`(0.5), `LineageXpPerSecond` = 기준 × `lineageXpIncomeShare`(0.5). 오프라인 · N분치 · 바구니 · 빠른 수선은 기준 수입으로 B를 만든 뒤 한 번만 나눈다.
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
계보 레벨 비용(L → L+1) = ceil(50 × 1.09^(L−1) − 1e-6) 실타래(`LineageXp`), 미보유 OR 최대 레벨이면 0   # CS:199-206 · 209-221
계보 TryLevelUp: 계보 레벨업 기능(FeatureId.CharacterLevelUp) 잠김 OR 미보유 OR Lv.100 OR 실타래 부족 → false, 성공 시 +1   # CS:34 · 233-243
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
비용 초 = max(0, 비용 − 보유 재화) ÷ max(수입 속도, 하한)   # 수선점 = 단추 ÷ 초당 단추(하한 1), 유실자 = 실타래 ÷ 초당 실타래(= 기준 수입 × 0.5, 하한 0.01)   # SA
```

**K. 강해지기 후보 9종** (`StrengthenKind`, GE:752-763)

| 종류 (번호) | 조건 · 고르는 법 | 증가 · 비용 초 | [가기] `FeatureId` · 위치 |
|---|---|---|---|
| `ShopUpgrade` (0) | 수선점 최대 레벨 아님. n = 1에서 시작, 보유 단추 + 120초 × GPS 예산 안의 최대 칸 수 (최대 10, 남은 레벨까지). n = 1은 예산과 무관 | `PreviewShopLevels(n)` · 비용(L, n)의 비용 초 | `ShopUpgrade`, x1 버튼 손가락 · SA:277-293 |
| `LineageLevel` (1) | 계보 레벨업 기능 열림, 편성에 놓인 보유 계보, 최대 레벨 아님. 레벨 수 = 지금 실타래로 살 수 있는 만큼 (최대 5, 못 사면 1) | `PreviewLevelUp(계보, n)` · 계보 비용(n)의 비용 초 | `ShopUpgrade` · SA:295-322 |
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
[시작 Lv. = CatchUpLevel] ──(TryLevelUp, 실타래)──→ … ──→ [Lv.100]   ·   [공명 0] ──(TryResonance, 기억편)──→ … ──→ [공명 10]

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
| `levelUpBaseCost` · `levelUpCostGrowth` | 12 · 1.12 (자산 같음, 기본 비용 10 → 12는 결정 2a) | GC:15-16 · GCA:18-19 | 수선점 Lv.1→2 비용 (단추) · 레벨마다 배율 |
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
| 수선점 비용 Lv.1 · 10 · 100 | 12 · 33.28 · 894,881.4 | GROW:40-41 | 다음 1레벨 비용 (단추, 소수 유지) |
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
| `GameConfig` 수선점 필드 | int · double | `maxLevel` · `levelRewardInterval`(int), `levelUpBaseCost` · `levelUpCostGrowth` · `baseGoldPerSecond` · `goldPerSecondGrowth` · `levelRewardGem`(double) | 1000 · 10 · 12 · 1.12 · 1 · 1.06 · 50 |
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

> 기준 문서: [`SYS_InGameBattle`](./SYS_InGameBattle.md) §4.1(성장 공유 · 육성 이원화 금지) · §8(코어 성장 3축) · [`UI_InGame`](../03_UI_UX/UI_InGame.md) GR-001 · EV-001 · HM-002 · [레벨업 와이어프레임](../03_UI_UX/images/UI_InGame_GrowthLevelup_Wireframe.png)(5화면).
> 기준 문서끼리 어긋나던 곳(§3 #1)은 **D-29 A안**으로 확정: 레벨 공유 1축 + 공명 = 잔향이 전용 효과 축.
> [가정] "캐릭터" = 현재 구현의 **계보**로 둔다. I-7 대조표(캐릭터 ↔ 계보 · 형태)로 확정되면 표기를 맞춘다.

### 2.0 성장 축 한눈에

| 축 | 단위 | 올리는 재화 | 효과 | 상한 · 게이트 | 단계 |
|---|---|---|---|---|---|
| **계보 레벨** (공유) | 계보 | 실타래 | 공격력 · 체력 (지킴이 · 잔향이 양쪽) | Lv.100, **한계 돌파** 20 · 40 · 60 · 80에서 막힘 | M1 (돌파 P1) |
| **계보 공명** | 계보 | 기억편(중복) | 잔향이 전용 효과 + 약한 공용 능력치 | 공명 10, 공명 돌파는 P1 | M1 (현재 구현 유지) |
| **수선점 레벨** | 계정 | 단추 | 수선점 내구 · 초당 단추 | Lv.1000 | M1 (현재 구현) |
| **수선점 시설 3축** | 계정 | 단추 + 수선 재료 | 등불 내구 / 잔향 친화 / 빛의 반경 (§8) | 시설별 | **P1** — `SYS_Atelier`로 분리 |
| 진화 · 개화 | 형태 | 진화 재료 · 기억 조각 | 형태 해금 (외형 · 스킬) | 3단계 | `SYS_Evolution` |

### 2.1 규칙
```
[계보 레벨업]
IF 계보 미보유 OR 계보 레벨업 기능 잠김                       → 버튼 비활성 (사유 표시)
ELSE IF 레벨 = 현재 상한 (한계 돌파 필요)                     → [한계 돌파] 버튼으로 교체
ELSE IF 실타래 < 비용(n)                                        → x1 은 부족분 빨간 표시, xN 은 살 수 있는 만큼으로 자동 축소 (0이면 비활성)
ELSE                                                            → 실타래 −비용, 레벨 +n, 전투력 변화 연출 (§2.3 ⑤)
  효과: 그 계보의 모든 형태 · 양쪽 모드(지킴이 · 잔향이)에 같은 레벨 적용   ← §4.1 "육성 이원화 금지"
  전투 중 레벨업: 지킴이 수치 즉시 반영, 이미 나온 잔향이는 소환 당시 수치 유지 (현재 구현과 같음)

[한계 돌파] (P1)
IF 레벨 = 상한(20 · 40 · 60 · 80) AND 돌파 재료 · 기억 조각 · 단추 충족 → 상한 +20, 돌파 보상(스킬 · 패시브 단계) 해금
ELSE → 부족한 조건마다 획득처 바로가기

[계보 공명]
IF 기억편 ≥ 공명 비용 AND 공명 < 상한 → 기억편 −비용, 공명 +1
  효과(잔향이 전용): 소환 비용 −, 유지 시간 +, (공명 단계 보상) 동시 출현 +1
  효과(공용, 작게): 공격력 · 체력 +% (현재 구현 5%/레벨 유지)
  편성 효과: 같은 계보가 지킴이 · 잔향이 양쪽에 있으면 "계보 공명" 시너지 (§3 #4 — 전투 차이 #4 결정 따름)

[수선점 레벨업] 현재 구현 그대로 (단추, x1 · x10 · MAX, 10레벨마다 별조각 우편)
```

### 2.2 상태 전이
```
계보 레벨
[Lv.1] ─(레벨업)→ … → [Lv.20 상한] ─(한계 돌파)→ [상한 40] → … → [상한 100]
계보 공명
[0] ─(기억편)→ … → [10]  (공명 돌파로 상한 확장은 P1)
성장 화면
[성장 허브] ─(지킴이 탭)→ [계보 레벨업] ─(상한)→ [한계 돌파] ─(완료)→ [전투 반영]
            └(잔향이 탭)→ [공명 성장] ─────────────────────────→ [전투 반영]
```

### 2.3 UI 플로우 / 디스크립션 (와이어프레임 5화면 기준)
1. **성장 허브** (캐릭터 상세 → 성장 탭)
   - 상단 탭 `[지킴이] [잔향이]` : 같은 계보의 두 모드 성장을 전환
   - 공유 요약 카드 : 계보 레벨 · 공명 레벨 · "공유: 레벨 · 호감도 · 공명" 한 줄
   - 성장 선택 카드 2개 : `지킴이 레벨업` / `잔향이 공명` — 각 카드에 다음 단계 전→후 수치 3줄 → 탭 → ② 또는 ③
2. **계보 레벨업**
   - 레벨 표시 `Lv.40 → Lv.41` · EXP 바(재료형 채택 시만, §3 #2)
   - 능력치 미리보기 표 (HP · 공격 · 방어 전→후, 증가분 초록) — 투자 전 판단용
   - `[x1] [x10] [MAX]` — 길게 누르면 x1 반복 / 부족하면 축소 · 비활성 규칙(§2.1)
   - 하단 안내 : "레벨은 지킴이 · 잔향이에 함께 적용돼요"
3. **잔향이 공명**
   - 공명 레벨 `Lv.18 → Lv.19` · 기억편 보유 / 필요
   - 적용 능력 미리보기 : 소환 비용 · 지속 시간 · 공격력 · 동시 출현 수 (잠금 아이콘 = 공명 단계 보상)
   - (P1) `개별 / 일괄` 탭 : 같은 계보의 여러 형태를 한 번에 — 현재 구현은 공명이 계보 단위라 일괄이 기본
4. **한계 돌파** (P1)
   - 조건 체크리스트 (레벨 도달 · 돌파 재료 · 기억 조각 · 단추) 각 줄 ✓ / 획득처 →
   - 결과 카드 `최대 레벨 40 → 60` · 해금 보상(스킬 2 등)
   - `[재료 획득처] [돌파 실행]`
5. **전투 반영**
   - 전투 준비 화면에 성장한 계보 하이라이트 · 전투력 전→후 (`+1,240`) · 추천 콘텐츠(평가: 여유 · 도전 · 어려움)
   - "성장 적용 완료!" 토스트 → `[전투 시작]` / `[성장 리포트]`
- 진입 경로 공통 : **강해지기**(현재 구현 `StrengthenAdvisor`) 줄의 [가기] → 해당 화면 + 버튼 손가락 안내 (현재 구현 유지)

### 2.4 데이터
| Column | Type | 설명 | 예시 |
|---|---|---|---|
| `Lineage_Growth.lineage_id` | string (FK → `Character_Base.char_id`, I-7 후 확정) | 계보 | `jjak` |
| `Lineage_Growth.level` | int | 계보 공유 레벨 (현재 `LineageSave.level`) | 41 |
| `Lineage_Growth.level_cap` | int | 현재 상한 (한계 돌파로 +20) — **신규** | 60 |
| `Lineage_Growth.resonance` | int | 공명 레벨 (현재 `LineageSave.resonance`) | 3 |
| `Lineage_Growth.shards` | int | 기억편 (현재 `LineageSave.shards`) | 40 |
| `Breakthrough_Config.cap` | int (PK) | 돌파 단계 (20 · 40 · 60 · 80) — **신규** | 40 |
| `Breakthrough_Config.cost_gold` · `cost_item_id` · `cost_item_count` · `cost_memory` | double · string (FK → Item) · int · int | 돌파 비용 | 3000 · `mat_core` · 3 · 20 |
| `Breakthrough_Config.reward_type` · `reward_value` | enum · string | 돌파 보상 (스킬 단계 · 패시브) | `SkillTier` · `2` |
| `Resonance_Effect.level` | int (PK) | 공명 레벨 | 5 |
| `Resonance_Effect.summon_cost_delta` · `duration_delta` · `max_concurrent_delta` | int · float · int | 잔향이 전용 효과 (누적) — **신규** | −1 · +0.5 · 0 |

## 3. 차이 → 결정 필요 — DD 작성, 대표 결정

| # | 항목 | ① 현재 구현 | ② 기획 방향 | 영향 (코드 · 데이터 · 세이브) | DD 추천 | 결정 |
|---|---|---|---|---|---|---|
| 1 | **성장 축 구조** ⚠ 기획서끼리 모순 | 계보 레벨 **하나**(양쪽 공유) + 계보 공명(기억편) | 시스템 §4.1 = "레벨 · 진화 **단일 축**, 육성 이원화 금지" ↔ 와이어프레임 = "지킴이 · 잔향이 **성장 규칙 분리**"(지킴이 레벨 / 잔향이 공명 레벨 따로) | 데이터 · 세이브 · UI 전체 | **A안: 레벨은 공유 1축 유지(§4.1 · 현재 구현) + 공명을 "잔향이 전용 효과" 축으로 재정의(와이어프레임).** 두 문서를 모두 살리고, 현재 세이브 구조(`LineageSave.level` · `resonance`)를 그대로 쓸 수 있음. B안(지킴이 · 잔향이 레벨 분리)은 육성 부담 2배 · 세이브 이전 필요 | **D-29** DD 추천안 |
| 2 | 계보 레벨업 재화 | **실타래만** (Lv.1→2 = 50, ×1.09. 2026-11-14 전까지 단추) | 수선 재료(경험치칩 · 기억 실타래 · 수선 키트) 투입형 (와이어프레임 ② · UI GR-001) | 경제 · UI · 세이브 | **M1은 단추 유지.** 방치형은 "모은 재화로 바로 올리기"가 기본 손맛이고 재료형은 획득처 · 재고 · 자동 투입 UI까지 필요. 재료는 한계 돌파(#3) · 진화에만 쓰고, 전환 여부는 `SYS_Economy`에서 재검토 | **D-29** DD 추천안 |
| 3 | 한계 돌파 | 없음 (Lv.1~100 연속) | 레벨 상한 → 돌파 조건(레벨 · 재료 · 기억 조각 · 단추) → 상한 + 스킬 해금 (와이어프레임 ④) | 코드 · 데이터(신규 테이블) · 세이브(`level_cap`) | **채택, P1.** 장기 목표 · 재료 싱크 · 스킬 해금 지점으로 필요. 상한 20 · 40 · 60 · 80 | **D-29** DD 추천안 |
| 4 | 공명 효과 | 공격력 · 체력 +5%/레벨 (공용) + 편성 계보 공명 시너지 | 잔향이 전용: 소환 비용 · 지속 시간 · 동시 출현 수 (와이어프레임 ③) | 코드(전투 소환) · 데이터 | **둘 다**: 공용 +5%는 유지(현재 밸런스 보존), 잔향이 전용 효과를 공명 단계 보상으로 **추가**. 동시 출현 +1은 전투 소환 상한(D-28 #8) 검증 후 | **D-29** DD 추천안 |
| 5 | 수선점 성장 | 수선점 **레벨 1축**(단추) → 내구 · 초당 단추 | 시설 강화 **3축**: 등불 내구 / 잔향 친화(수급) / 빛의 반경(결계 · 소환 상한) (§8, UI HM-002) | 코드 · 세이브 이전 · 경제 | **M1은 레벨 1축 유지, 3축은 P1 `SYS_Atelier`.** 3축 전환 때 기존 레벨을 "등불 내구" 레벨로 환산하는 이전 규칙 필요 | **D-29** DD 추천안 |
| 6 | 외형변경 능력치 | 보유한 외형마다 계보 공격력 +3~8% (장착 여부 무관, 누적) | 외형은 서사형 · 기본 밸런스 동일 권장, 효과 여부 미확정 (UI §12 · §18) | 밸런스 · BM | **유지하되 계보당 합계 상한 +10%.** 외형이 유료 상품이 되면 무제한 누적은 과금 압박(P3 "희귀도 ≠ 서열" 위반) | **D-29** DD 추천안 |
| 7 | 전투력 · 권장 · 강해지기 | 있음 (팀 전투력 가중 · 권장 3구간 곡선 · 강해지기 9종) | 기획서에 없음 (와이어프레임 ⑤에 전투력 전→후만) | 문서 | **현재 구현을 기획서로 채택** — 이 문서 ①을 정본으로. 막힘 해소 동선(강해지기)은 방치형 이탈 방지 핵심 | **D-29** DD 추천안 |

> **§3 #2 재검토 (2026-11-14)**: D-29 ②가 남긴 "전환 여부는 `SYS_Economy`에서 재검토"의 결과는 `SYS_Economy` §3 #8 **B 적용**이다. 디렉터 지시(결정 1 · 2 · 2a)로 M1 안에서 유실자 레벨업 재화를 단추에서 새 재화 "실타래"(`CurrencyType.LineageXp`)로 분리했다 (D-73 · D-29 ② · D-30 ① 변경, 대표 결재 필요, 결정 기록은 DD 대기). 비용 50 × 1.09 · Lv.100은 그대로, 단위만 실타래. 수선점 강화 재화는 단추 그대로이고 기본 비용 10 → 12. 이음매 `GameConfig.lineageLevelUpCurrency`는 `LineageXp`.

## 4. 예외 처리 / 엣지 케이스
| 케이스 | 처리 |
|---|---|
| 네트워크 단절 | 성장은 로컬 처리 (오프라인 게임). 영향 없음 |
| 앱 강제 종료 | 레벨업은 즉시 세이브 반영 대상. **⚠ 전투력 미리보기는 세이브를 잠깐 바꿨다 되돌림(①-I) → 그 사이 예외 · 종료 시 바뀐 값이 저장될 위험** → 기개발 확인 필요 (§6) |
| 기기 시간 조작 | 성장 자체는 시간과 무관. 단추 수입(방치 · 오프라인)은 `SYS_IdleReward`에서 방어 |
| xN 버튼 단추 부족 | 현재: 실패 (일부만 올리지 않음) → ②: 살 수 있는 만큼으로 축소 |
| 계보 레벨 > 상한 (돌파 도입 시 기존 세이브) | 이전 규칙: 기존 레벨을 포함하는 상한까지 돌파 완료로 간주 (보상 소급 지급) |
| 전투 중 성장 | 지킴이 즉시 반영, 이미 나온 잔향이는 유지 (현재 구현) |

## 5. 어뷰징 검토
| 시나리오 | 대응 |
|---|---|
| 세이브 파일 수정으로 레벨 · 단추 조작 | `SYS_Save` 범위 (I-2 M-2 세이브 무결성) |
| 시간 조작으로 단추 대량 획득 → 성장 | `SYS_IdleReward` 범위 (I-5 원정 시계와 같은 `DailyClock` 방어 적용) |
| 미리보기 중 강제 종료로 값 고정 | §4 — 미리보기를 세이브 복사본으로 계산하도록 (기개발 확인) |
| 외형 대량 보유로 공격력 무한 누적 | §3 #6 상한 |

## 6. 확장성 / 오픈 이슈
- [x] §3 #1 결정 → D-29 A안
- [ ] I-7 대조표로 "캐릭터 = 계보?" 확정 → `Lineage_Growth.lineage_id` FK 확정
- [ ] 기개발 확인: `PowerSystem` 미리보기가 세이브 원본을 바꾸는지 · 예외 시 복구 보장 (§4)
- [ ] 한계 돌파 · 공명 전용 효과 수치 → 성장 곡선 `BAL_` 문서 (P1, 아직 없음) / `BAL_BattleKnobs`
- [x] 수선점 3축 전환 이전 규칙 → `SYS_Atelier` §2.2 (수선점 레벨 = 수선 작업대 레벨, D-55)
- 확장성: 성장 축을 `Lineage_Growth` 한 테이블에 모으고 효과는 `Resonance_Effect` · `Breakthrough_Config` 데이터로 분리 → 새 계보 추가 시 코드 수정 없이 데이터만 추가
