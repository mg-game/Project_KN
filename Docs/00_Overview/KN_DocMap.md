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
| `KN_Overview` | 🔵 | **P0** | Approved — D-35 · 2부 M1 범위서 재정리(작업 목록 한 장 🅰/🅱) D-57 | 1페이지 개요: 핵심 루프 · 타겟 · BM 방향 · 차별점. Notion 게임 개요 원본 |
| `KN_Roadmap` | 🟡 | **P0** | Review — 기획 · 아트 작업 계획 (부족한 것 · 구조 문제 4건 · 카드 21장 · 일정) |
| `KN_Preview` | 🔵 | **P0** | Approved — D-42 | M1 화면 미리보기(아티팩트) 링크 · 운영 규칙 6개 · 갱신 기록 |
| `KN_M1_DevBrief` | 🔵 | **P0** | 정본 — 이슈 #3 지시 · 보충을 PR 묶음별 합본 · 어긋남 5건 정리 (보름달 +30% D-58) | M1 개발 지시 합본 (기개발이 11/24부터 읽는 한 장) |
| `KN_M1_Playtest` | 🔵 | **P0** | Approved — D-58 (기록 보내기 계정 · 데이터 탭 · 기록 줄 2개 · 내부 → 외부) | M1 플레이테스트 질문지 · 진행 방법 |
| `Principles` · `Notion_Hub` | 🔵 | - | Approved | |

## 01_System

| 문서 | 상태 | 우선 | 진행 | 근거 코드 (PR #2) |
|---|---|---|---|---|
| `SYS_InGameBattle` | 🟡 | **P0** | ① §17 · ③ 13건 (12건 확정, #4 I-7 대기) · §18 M1 상세(성향 · 수선등 · 웨이브) Review | `Systems/Battle/*` (BattleSimulator · FormationSystem) |
| `SYS_Growth` | 🔵 | **P0** | ①②③ 완료 · D-29 확정 | GrowthSystem · PowerSystem · StrengthenAdvisor — 레벨업 와이어프레임(PR #8) |
| `SYS_Economy` | 🔵 | **P0** | ①②③ 완료 · D-30 확정 | CurrencySystem · InventorySystem |
| `SYS_IdleReward` | 🔵 | **P0** | ①②③ 완료 · D-31 확정 | OfflineRewardSystem · IdleChestSystem · ChapterChestSystem (+ 시간 조작 방어) |
| `SYS_FTUE` | 🔵 | **P0** | ①②③ 완료 · D-32 확정 | TutorialSystem · FeatureGateSystem |
| `SYS_Evolution` | 🔵 | **P0** | ①②③ 완료 · ③ 7건 D-45 확정 (K-02) | EvolutionSystem |
| `SYS_Costume` | 🔵 | P1 | ①②③ 완료 · ③ 7건 D-55 확정 (계보당 +10% 상한 · 값 1~3% M1) | CostumeSystem (기획 "외형변경") |
| `SYS_Gacha` | 🔵 | P1 | ①②③ 완료 · ③ 7건 D-51 확정 (K-09) · 결제 전 P0 = 확률 표시 · 서버 뽑기 | GachaSystem · GachaMilestoneSystem |
| `SYS_Atelier` | 🔵 | P1 | 설계 초안 · ③ 8건 D-55 확정 — 수선점 레벨 → 수선 작업대 · 기억 베틀 · 기억등 (소환 상한 12→20) · 코드 없음 (P1) | (없음 — 지금은 GrowthSystem 레벨 1축) |
| `SYS_Dispatch` | 🔵 | P1 | ①②③ 완료 · ③ 6건 D-54 확정 (안전장치 M1) | ExpeditionSystem · ExpeditionDatabase |
| `SYS_Dungeon` | 🔵 | P1 | ①②③ 완료 · ③ 6건 D-55 확정 (달빛 상한 40% M1 · 축복 · 모아 두기 P1) | DungeonSystem |
| `SYS_Synergy` | 🔵 | P1 | ①②③ 완료 · ③ 6건 D-50 확정 (K-07 · K-11 흡수) | SynergySystem · SynergyDatabase (계보 공명 포함) |
| `SYS_Rescue` | 🔵 | P1 | ①②③ 완료 · ③ 7건 D-49 확정 (K-08) | GachaSystem · AffinitySystem · StorySystem |
| `SYS_Relationship` | 🔴 | P1 | 대기 — 합류 단계는 `SYS_Rescue` §2.3에 흡수, 관계 · 연계 스킬은 K-07과 | AffinitySystem · CheerSystem |
| `SYS_Codex` | 🔵 | P1 | ①②③ 완료 · ③ 6건 D-55 확정 (시너지 보너스 → 잔향 수급 % M1) | CodexSystem |
| `SYS_Skill` | 🔵 | **P0** | ①②③ 완료 · ③ 5건 D-39 · D-40 확정 | `SkillDefs` · `BattleSimulator.Skills` |
| `SYS_Save` | 🔵 | P1 | ①②③ 완료 · ③ 7건 D-59 확정 — 로컬 JSON · 백업 · 세이브 코드(CRC만) · DailyClock 되돌림 감지(원정만 빠짐 → I-5) · 결제 때 "서버 묶음"(계정 · 시각 · 지갑 · 뽑기 · 클라우드) · iOS 4.8 · 계정 삭제 | SaveStorage · PlayerData · DailyClock · SaveCode · Account |
| `SYS_Shop` | 🔴 | P2 | 대기 | ShopSystem |
| `SYS_LiveOps` | 🔴 | P2 | 대기 | Mission · Achievement · Attendance · DailyGift · StarterWeek · SeasonPass · EventCalendar · Comeback · Coupon · Mail · Notice · GuideQuest |

## 02_Content

| 문서 | 상태 | 우선 | 진행 | 근거 |
|---|---|---|---|---|
| `CNT_Character_Main` | 🟡 | P1 | Review | 대표 8종. I-7 대조표 반영 필요 |
| `CNT_Character_List` | 🔵 | **P0** | ①②③ 완료 · ③ 7건 D-46 확정 (K-01) | `CharacterDatabase.asset` · `Art/Characters` |
| `CNT_Stage_Chapter1` | 🔵 | **P0** | ①②③ 완료 · D-34 확정 | `Editor/Content` 스테이지 · 웨이브 · 적 구성 |
| `CNT_Enemy` | 🔵 | **P0** | ①②③ 완료 · ③ 4건 D-40 확정 | `BattleConfig.enemies` |
| `CNT_Skill` | 🔵 | **P0** | ①②③ 완료 · ③ 7건 D-43 확정 (K-04) | `CharacterDatabase.asset` 형태 스킬 |
| `CNT_Boss` | 🔵 | **P0** | ①②③ 완료 · ③ 5건 D-41 확정 (K-06) | `BattleConfig.enemies` (isBoss) · `BattleSimulator` 보스 · 기믹 |
| `CNT_Item` | 🔴 | P2 | 대기 | ItemDatabase |
| `CNT_Story` | 🔵 | P1 | ①②③ 완료 · ③ 7건 D-53 확정 (프롤로그 · 1장 M1) | `StoryIntroView` · `MetaContent.Story` · `MetaContent.Ftue` |

## 03_UI_UX

| 문서 | 상태 | 우선 | 진행 | 메모 |
|---|---|---|---|---|
| `UI_InGame` | 🟡 | P1 | Approved (v0.1) | 전체 IA로 유지, 화면 상세는 아래로 분리 |
| `UI_Battle` | 🔵 | **P0** | Approved (D-52) | HUD 터치 규격, 하단 포화(시스템 §12.1 ⬜) |
| `UI_Formation` | 🔵 | **P0** | Approved (D-52) | 8방위 링 · 잔향이 덱 |
| `UI_Growth` | 🔵 | **P0** | ①②③ 완료 · ③ 7건 D-43 확정 (미리보기 레벨업 탭) | `GrowthPanel` · `GrowthLineageCardView` · `CharacterDetailPopup` |
| `UI_Home` | 🔵 | **P0** | ①②③ 완료 · ③ 6건 D-44 확정 (미리보기 홈 탭) | `MainTabController` · `TopBarView` · `BattleHud` · `Meta/*` |
| `UI_Characters` | 🔵 | P1 | ①②③ 완료 · ③ 6건 D-56 확정 — 유실자 탭 · 상세 4탭 · 외형 탭(D-55 상한 표시) · 미보유 '찾으러 가기' | `CharacterPanel` · `CharacterDetailPopup` · `CostumeListView` |
| `UI_Bag` | 🔵 | P1 | ①②③ 완료 · ③ 4건 D-56 확정 — 재료 도감 · 얻는 곳 · 바로 가기 이름 의뢰 · 던전(D-54) · P1 쓰는 곳 | `BagPopup` · `ItemInfoPopup` |
| `UI_Shop` | 🔵 | P1 | ①②③ 완료 · ③ 6건 D-56 확정 — 수선점 · 이벤트 · 오늘의 선물 · 결제 코드 없음 → 결제 쪽 자리(P1) | `ShopPanel` · `ShopProductCardView` · `DailyGiftCardView` |
| `UI_Expedition` | 🔵 | P1 | ①②③ 완료 · ③ 5건 D-56 확정 — D-54 · D-55를 화면으로 · 자동 선택 순서 · 모두 다시 보내기 × 출발 가드 | `ExpeditionPanel` · `ExpeditionSetupPopup` · `DungeonCardView` |
| `UI_Meta` | 🔵 | P1 | ①②③ 완료 · ③ 7건 D-60 확정 — 메타 화면 목록 · **문자열 전수 검사**(기억편 35 · 의상 31 · 불빛 52 · 미보유 5) · ⚠ 기능 22 개방 카드 · 실패 리포트 조언 · 용어 정본 | `Scripts/UI/Meta/*` · `SettingsPopup` · `MetaDatabase.asset` |

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
| `ART_EchoSpec` | 🔵 | Approved | 잔향이 외형 규격 — ③ 5건 D-48 확정 (A-04) · 전용 그림은 추후 |
| `ART_BattleSpriteSpec` | 🔵 | Approved | 전투 스프라이트 규격 — L&L 58장 실측 · ③ 5건 D-41 확정 (A-03) |
| `ART_FxJuice` · `ART_UpgradePlan` · `AUD_Spec` · `RES_IdleGames` | 🟡 | 대기 (I-4 이관) | L&L 영문 문서 이관 |
| `ART_ReviewSheet` | 🟡 | **P0** | Ready — 시안 대기 · 짝짝이 기준(유지 / 바꿔도 됨) · 양말 카라반 "부분 → 구조 → 쓰임" · 컨셉 대조 · 기술 검사 11 · 가이드 §15 · 판정 | 캐릭터 시안 검수 대조표 (D-46 #5 대표 진행안) |

---

## P0 진행 순서 (D-26)

```
① KN_Glossary · KN_DocMap · SYS_InGameBattle ③차이   ← 이 PR
② SYS_InGameBattle ①구현        (기개발, I-9)
③ SYS_Growth → SYS_Economy → SYS_IdleReward → SYS_FTUE → CNT_Stage_Chapter1   (각각 ①기개발 → ②③DD)
④ UI_Battle · UI_Formation · UI_Growth · BAL_BattleKnobs · KN_Overview   (DD)
```
