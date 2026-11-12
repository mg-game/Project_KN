# KN_DocMap — 기획서 지도 · 진행 상태

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | DD |
| 최종 수정 | 2026-11-11 (PR #14~#19 반영) |
| 관련 테이블 | - |
| 관련 문서 | [`decisions.md`](../decisions.md) D-25 · D-26 · [`KN_Glossary`](./KN_Glossary.md) · [`_Template_System`](../_Template_System.md) |

> Project_KN 기획서 전체 목록과 진행 상태. **DD가 관리**하며, 문서 PR이 머지될 때마다 이 표를 갱신한다.
> 시스템 문서 하나 = 문서 하나 (D-26): ① 현재 구현(As-built) ② 기획 방향(To-be) ③ 차이 → 결정 필요.

## 범례

| 표기 | 뜻 |
|---|---|
| 🔵 있음 · 🟡 보강 · 🔴 신규 | 문서 상태 |
| P0 | M1(12/1~12/26 플레이어블 프로토) 필수 |
| ① 구현 | 현재 구현 절 — **기개발** 추출 (근거: PR #2 코드) |
| ② 방향 · ③ 차이 | 기획 방향 · 차이 절 — **DD** 작성, ③은 **대표** 결정 |
| 진행 | `대기` → `① 작성` → `②③ 작성` → `Review` → `Approved` |

---

## 00_Overview

| 문서 | 상태 | 우선 | 진행 | 메모 |
|---|---|---|---|---|
| `KN_ConceptNarrative` | 🟡 | P1 | Approved (v0.2) | 시스템 v1.0이 예고한 v0.3 개정 항목 반영 필요 |
| `KN_Glossary` | 🔵 | **P0** | Approved | 용어 정본. 콘셉트 부록 A · 시스템 §15 통합 |
| `KN_DocMap` | 🔵 | **P0** | Approved | 이 문서 |
| `KN_Overview` | 🔵 | **P0** | Approved — D-35 | 1페이지 개요: 핵심 루프 · 타겟 · BM 방향 · 차별점. Notion 게임 개요 원본 |
| `KN_Roadmap` | 🟡 | **P0** | Review — 기획 · 아트 작업 계획 (부족한 것 · 구조 문제 4건 · 카드 21장 · 일정) |
| `KN_Preview` | 🔵 | **P0** | Approved — D-42 | M1 화면 미리보기(아티팩트) 링크 · 운영 규칙 6개 · 갱신 기록 |
| `Principles` · `Notion_Hub` | 🔵 | - | Approved | |

## 01_System

| 문서 | 상태 | 우선 | 진행 | 근거 코드 (PR #2) |
|---|---|---|---|---|
| `SYS_InGameBattle` | 🟡 | **P0** | ① §17 · ③ 13건 (12건 확정, #4 I-7 대기) · §18 M1 상세(성향 · 수선등 · 웨이브) Review | `Systems/Battle/*` (BattleSimulator · FormationSystem) |
| `SYS_Growth` | 🔵 | **P0** | ①②③ 완료 · D-29 확정 | GrowthSystem · PowerSystem · StrengthenAdvisor — 레벨업 와이어프레임(PR #8) |
| `SYS_Economy` | 🔵 | **P0** | ①②③ 완료 · D-30 확정 | CurrencySystem · InventorySystem |
| `SYS_IdleReward` | 🔵 | **P0** | ①②③ 완료 · D-31 확정 | OfflineRewardSystem · IdleChestSystem · ChapterChestSystem (+ 시간 조작 방어) |
| `SYS_FTUE` | 🔵 | **P0** | ①②③ 완료 · D-32 확정 | TutorialSystem · FeatureGateSystem |
| `SYS_Evolution` | 🟡 | **P0** | Review — 3단(유실 · 기억 각성 · 개화 A/B) · 돌파 20 연동 · 짝짝이 예외 · 새 아트 8장 · ③ 7건 (K-02) | EvolutionSystem |
| `SYS_Costume` | 🔴 | P1 | 대기 | CostumeSystem (기획 "외형변경") |
| `SYS_Gacha` | 🔴 | P1 | 대기 | GachaSystem · GachaMilestoneSystem |
| `SYS_Atelier` | 🔴 | P1 | 대기 | (코어 성장 — 구현 확인 필요) |
| `SYS_Dispatch` | 🔴 | P1 | 대기 | ExpeditionSystem |
| `SYS_Dungeon` | 🔴 | P1 | 대기 | DungeonSystem |
| `SYS_Synergy` | 🔴 | P1 | 대기 | SynergySystem (계보 공명 포함) |
| `SYS_Relationship` | 🔴 | P1 | 대기 | AffinitySystem · CheerSystem |
| `SYS_Codex` | 🔴 | P1 | 대기 | CodexSystem |
| `SYS_Skill` | 🔵 | **P0** | ①②③ 완료 · ③ 5건 D-39 · D-40 확정 | `SkillDefs` · `BattleSimulator.Skills` |
| `SYS_Save` | 🔴 | P1 | 대기 | SaveData · Account · BackupReminder (시계 · 세이브 위험, I-2 H-1 · M-1 · M-2) |
| `SYS_Shop` | 🔴 | P2 | 대기 | ShopSystem |
| `SYS_LiveOps` | 🔴 | P2 | 대기 | Mission · Achievement · Attendance · DailyGift · StarterWeek · SeasonPass · EventCalendar · Comeback · Coupon · Mail · Notice · GuideQuest |

## 02_Content

| 문서 | 상태 | 우선 | 진행 | 근거 |
|---|---|---|---|---|
| `CNT_Character_Main` | 🟡 | P1 | Review | 대표 8종. I-7 대조표 반영 필요 |
| `CNT_Character_List` | 🔴 | P1 | 대기 | `Editor/Content/CharacterContent*` (계보 · 형태 전체) |
| `CNT_Stage_Chapter1` | 🔵 | **P0** | ①②③ 완료 · D-34 확정 | `Editor/Content` 스테이지 · 웨이브 · 적 구성 |
| `CNT_Enemy` | 🔵 | **P0** | ①②③ 완료 · ③ 4건 D-40 확정 | `BattleConfig.enemies` |
| `CNT_Skill` | 🔵 | **P0** | ①②③ 완료 · ③ 7건 D-43 확정 (K-04) | `CharacterDatabase.asset` 형태 스킬 |
| `CNT_Boss` | 🔵 | **P0** | ①②③ 완료 · ③ 5건 D-41 확정 (K-06) | `BattleConfig.enemies` (isBoss) · `BattleSimulator` 보스 · 기믹 |
| `CNT_Item` | 🔴 | P2 | 대기 | ItemDatabase |
| `CNT_Story` | 🔴 | P1 | 대기 | StorySystem · 프롤로그 · 1부 |

## 03_UI_UX

| 문서 | 상태 | 우선 | 진행 | 메모 |
|---|---|---|---|---|
| `UI_InGame` | 🟡 | P1 | Approved (v0.1) | 전체 IA로 유지, 화면 상세는 아래로 분리 |
| `UI_Battle` | 🟡 | **P0** | Review — M1 상세 (하단 2열 · 수선등 · 성향 아이콘) | HUD 터치 규격, 하단 포화(시스템 §12.1 ⬜) |
| `UI_Formation` | 🟡 | **P0** | Review — 8방위 링 · 빈 방위 빗금 · 파견 배지 | 8방위 링 · 잔향이 덱 |
| `UI_Growth` | 🔵 | **P0** | ①②③ 완료 · ③ 7건 D-43 확정 (미리보기 레벨업 탭) | `GrowthPanel` · `GrowthLineageCardView` · `CharacterDetailPopup` |
| `UI_Home` · `UI_Characters` · `UI_Bag` · `UI_Shop` · `UI_Expedition` · `UI_Meta` | 🔴 | P1~P2 | 대기 | `Scripts/UI/*` 화면별 |

## 04_Balance · 05_DataTable

| 문서 | 상태 | 우선 | 진행 | 메모 |
|---|---|---|---|---|
| `BAL_BattleKnobs` | 🟡 | **P0** | Draft — 노브 정본 · M1 초기값 [가정] | 시스템 §13.3 노브 초기값 (잔향 수급 · 소환 상한 · 제한시간 등) |
| `BAL_ProgressionSim` | 🟡 | P1 | 대기 (I-4 이관) | 성장 곡선 |
| `TBL_*` | 🔴 | P1 | 대기 | 시스템 §13 골격. D-20: 원본 = 코드 콘텐츠 → 스키마 문서로 |

## 07_Art · 08_Audio · 09_Research

| 문서 | 상태 | 진행 | 메모 |
|---|---|---|---|
| `ART_StyleGuide` | 🔵 | Review | |
| `ART_M1_Backlog` | 🟡 | Review | M1 신규 · 변경 아트 14건 (P0 8) — D-38 A1 · D-41 |
| `ART_Character_Brief` | 🟡 | Review | 대표 캐릭터 8종 유실형 시안 브리프 — D-38 A2 |
| `ART_BattleSpriteSpec` | 🔵 | Approved | 전투 스프라이트 규격 — L&L 58장 실측 · ③ 5건 D-41 확정 (A-03) |
| `ART_FxJuice` · `ART_UpgradePlan` · `AUD_Spec` · `RES_IdleGames` | 🟡 | 대기 (I-4 이관) | L&L 영문 문서 이관 |

---

## P0 진행 순서 (D-26)

```
① KN_Glossary · KN_DocMap · SYS_InGameBattle ③차이   ← 이 PR
② SYS_InGameBattle ①구현        (기개발, I-9)
③ SYS_Growth → SYS_Economy → SYS_IdleReward → SYS_FTUE → CNT_Stage_Chapter1   (각각 ①기개발 → ②③DD)
④ UI_Battle · UI_Formation · UI_Growth · BAL_BattleKnobs · KN_Overview   (DD)
```
