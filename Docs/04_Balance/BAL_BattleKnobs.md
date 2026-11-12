# BAL_BattleKnobs — 전투 밸런싱 노브

| 항목 | 값 |
|---|---|
| 상태 | Draft |
| 작성자 | DD |
| 최종 수정 | 2026-11-13 (날짜 맞춤 — 검수 KN_DocReview) |
| 관련 테이블 | `Stage_Battle_Config` · `Wave_Config` · `Summon_Mode` · `BattleConfig.asset` |
| 관련 문서 | [`SYS_InGameBattle`](../01_System/SYS_InGameBattle.md) §13.3 · §17(현재 값) · §18(M1 상세) · [`CNT_Stage_Chapter1`](../02_Content/CNT_Stage_Chapter1.md) |

> 전투 수치의 **정본**. 기획 문서에는 노브 이름만 쓰고 값은 여기서 관리한다.
> "현재" = PR #2 코드 · 에셋 값(§17) / "M1 초기값" = I-10 구현 때 넣을 값 / "테스트 범위" = M1 플레이테스트에서 바꿔 볼 폭.
> 🆕 = 현재 구현에 없는 노브. [가정] = DD 초안, 플레이테스트로 확정.

## 1. 잔향 (전투 재화)
| 노브 | 타입 | 현재 | M1 초기값 | 테스트 범위 | 근거 |
|---|---|---|---|---|---|
| `resonance_start` | int | 10 (던전 30) | 10 | 8 ~ 15 | D-28 #12 |
| `resonance_cap` | int | 99 | 99 | — | D-28 #12 |
| `income_base` | float (/초) | 1.0 | 1.0 | 0.8 ~ 1.2 | D-28 #12 · §18.2 |
| `kill_drop` · `boss_drop` | int | 1 · 10 | 1 · 10 | — | 처치 수입 ≈ 총 수입의 20% 목표 (§7.1) |
| `dissolve_refund_ratio` | float | 0.3 | 0.3 | 0 ~ 0.3 | D-28 #12 "유지 여부 플레이테스트" |

## 2. 수선등 밝히기 🆕 [가정]
| 레벨 L | `light_mult[L]` (초당 잔향 배율) | `light_cost[L]` (L → L+1 비용) | 추가 수입 | 본전 시간 |
|---|---|---|---|---|
| 1 | ×1.0 | 10 | — | — |
| 2 | ×1.4 | 20 | +0.4/초 | 25초 |
| 3 | ×1.8 | 35 | +0.4/초 | 50초 |
| 4 | ×2.3 | 55 | +0.5/초 | 70초 |
| 5 (`light_max_level`) | ×3.0 | — | +0.7/초 | 79초 |

| 노브 | 타입 | M1 초기값 | 근거 |
|---|---|---|---|
| `auto_invest_threat` | float | 0.5 (최대 위협 / 기준) | AUTO는 급한 불 없을 때만 투자 |
| `lamp_legacy_enabled` | bool | false | 옛 전체 공격 보관 (D-27 #3) |
| `fullmoon_income_bonus` | float | 0.3 | 보름달 교체 (D-34 #4) |

> 의도: 1장 스테이지 길이(약 1.5 ~ 3분)에서 L2 · L3까지는 확실히 이득, L4 · L5는 긴 스테이지 · 보스전에서만 이득 → "지금 부를까 vs 투자할까" 판단이 매 스테이지 생김.

## 3. 소환 · 성향
| 노브 | 타입 | 현재 | M1 초기값 | 테스트 범위 | 근거 |
|---|---|---|---|---|---|
| `summon_cap` | int | 12 (`maxEchoes`) | 12 | 12 · 16 · 20 | D-28 #8 · P1 기억등 레벨로 12 → 20 성장 (D-55, `SYS_Atelier` §2.4) |
| `token_cap` | int | 6 | 6 | — | 현재 |
| `enemy_cap` | int | 40 | 40 | — | 현재 |
| `hold_threshold_sec` | float | 0.2 | 0.2 | 0.15 ~ 0.3 | §5.2 |
| `charge_duration_mult` · `charge_cost_mult` 🆕 | float | — | 0.6 · 0.8 | — | §18.1 |
| `hold_duration_mult` · `hold_cost_mult` 🆕 | float | — | 2.0 · 1.3 | — | §18.1 |
| `charge_radius` · `patrol_radius` · `hold_radius` 🆕 | float (링 반지름 1 기준) | (순찰 1.45) | 2.2 · 1.45 · 1.2 | — | §18.1 |
| `patrol_speed` 🆕 | float (°/초) | — | 40 | 25 ~ 60 | §18.1 |
| `hold_arc` 🆕 | float (±°) | — | 30 | 22.5 ~ 45 | §18.1 (8섹터 = 45° 폭) |

## 4. 웨이브 · 쇄도 · 제한시간
| 노브 | 타입 | 현재 | M1 초기값 | 근거 |
|---|---|---|---|---|
| `wave_count` (스테이지별) 🆕 | int | (처치 15 ~ 20 → 보스) | 1-1~1-3 = 3 · 1-4~1-9 = 4 · 1-10 = 5 | D-34 #1 |
| 웨이브 크기 · 생성 간격 · 휴식 | — | 5 + 0.25(s−1) (≤14) · 1.1 − 0.02(s−1) (≥0.45) · 3.5 − 0.05(s−1) (≥2) | 현재 공식 유지 | §17.3 |
| `wave_overlap_ratio` 🆕 | float | — | 0.3 | §9.2 |
| `wave_grace_sec` 🆕 | float | — | 8 | §18.3 |
| `boss_escort_ratio` 🆕 | float | — | 0.5 | §18.3 |
| `rush_speed_mult` 🆕 | float | — | 1.5 | §9.2 |
| `rush_slowmo_sec` 🆕 | float | — | 0.8 | §9.2 |
| `time_limit_coeff` 🆕 | float | — | 2.5 | §10.1 (2.5 ~ 3.0) |
| `time_limit_rush_sec` 🆕 | float | — | 15 | §10.1 (+10 ~ 20) |
| `boss_fight_est_sec` 🆕 | float | — | 30 | §18.3 |
| `tutorial_time_limit_off` 🆕 | bool | — | true (1-1~1-3) | D-34 §4 |

## 4.5 보스 (K-06 — [`CNT_Boss`](../02_Content/CNT_Boss.md)) 🆕 [가정]
| 노브 | 타입 | 현재 | M1 초기값 | 테스트 범위 | 근거 |
|---|---|---|---|---|---|
| `drain_on_hit` (공성 1타당 잔향 −) | int | 불빛 −0.15 | 8 | 5 ~ 12 | D-40 #1 · `CNT_Boss` §2.1 (≈ 기본 수입 8초분) |
| `drain_per_tick` · `drain_interval` | int · float | 불빛 −0.04 · 3초 | 2 · 3초 | 1 ~ 3 | 반경 안 ≈ −0.67/초 → L1(1.0/초)에서도 순수입 플러스 유지 |
| `drain_radius` | float | 1.3 | 1.3 | — | 현재 |
| `drain_tutorial_off` | bool | — | true (1-1~1-3) | — | `CNT_Boss` §3 #4 |
| `enrage_threshold` (보스별 덮어쓰기) | float | 0.5 (공통) | 일반 0.5 · 1-10 변형 0.3 | — | `CNT_Boss` §2.3 |
| `ring_telegraph_sec` | float | — | 1.5 | 1.0 ~ 2.0 | 예고 없는 360° = 가독성 실패 (P2) |
| `ring_repeat_sec` (P2 4섹터 교대) | float | — | 20 | 15 ~ 30 | `CNT_Boss` §2.3 |
| `phase_gap_sec` | float | — | 2 | — | 임계 동시 통과 시 행동 간격 |
| `boss_stun_mult` · `boss_stun_immune_sec` 🆕 | float · float | 기절 그대로 | 0.5 · 3 | 0.3 ~ 0.7 · 2 ~ 5 | `CNT_Skill` §5 — 찰칵이 잔향이 연속 소환 보스 기절 고정 방지 |
| `mong_resonance_gain` 🆕 | int | 불빛 +3% | 1 / 회 | 0.5 ~ 2 | `CNT_Skill` §2.1 · D-40 (`LampGain` → `ResonanceGain`) |
| 1-10 변형 체력 | float | 931.1 (여왕 × 배율) | 여왕과 같음 | ×1.0 ~ ×1.3 | 페이즈 연출 시간만큼 길어지므로 먼저 같은 값으로 측정 |
| `boss_fight_est_sec` (1-10) | float | — | 45 | 35 ~ 60 | §5 Time_Limit |

> 링 소환 마릿수: RING_1 8 + 반복 4 × (P2 길이 / 20초) + RING_2 8 → 1-10 보스전 좀나방 약 20 ~ 28마리. 적 상한 40 · 잔향이 상한 12와 같이 볼 것.

## 5. 1장 예상 값 (M1 초기값 대입) [가정]
| 스테이지 | 웨이브 N | 웨이브 크기 | 예상 클리어(초) | Time_Limit(초) |
|---|---|---|---|---|
| 1-1 | 3 | 5 | ≈ 54 | 끔 (튜토리얼) |
| 1-4 | 4 | 5 | ≈ 66 | ≈ 180 |
| 1-7 | 4 | 6 | ≈ 70 | ≈ 190 |
| 1-10 | 5 | 7 | ≈ 102 (보스 45초, §4.5) | ≈ 270 |

- 예상 클리어 = Σ(웨이브 크기 × 생성 간격 + 휴식) + `boss_fight_est_sec`(30, 1-10 = 45). 실측은 M1 `BattleStats`로 교체.
- 기믹 배율(먼지 폭풍 웨이브 ×1.3 등)은 반영 전 값.

## 6. 측정 계획 (M1 W4)
| 지표 | 목표 [가정] | 조정 노브 |
|---|---|---|
| 1장 스테이지별 첫 시도 클리어율 | 1-1~1-3 100% · 1-4~1-9 70 ~ 85% · 1-10 50 ~ 70% | 웨이브 크기 · 적 체력 배율 |
| 제한시간 실패 비율 (전체 실패 중) | ≤ 15% | `time_limit_coeff` |
| 수선등 평균 도달 레벨 (1-4 이후) | 2.5 ~ 3.5 | `light_cost` |
| 동시 잔향이 수 상한 도달 빈도 | 보스전에서만 | `summon_cap` |
