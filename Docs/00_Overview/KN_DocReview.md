# KN_DocReview — 기획 문서 검수 기록

| 항목 | 값 |
|---|---|
| 상태 | 기록 (검수할 때마다 맨 위에 회차 추가) |
| 작성자 | DD |
| 최종 수정 | 2026-11-13 (1회차) |
| 관련 문서 | [`KN_DocMap`](./KN_DocMap.md) · [`KN_Glossary`](./KN_Glossary.md) · [`../README.md`](../README.md) §3 · §6 · [`decisions.md`](../decisions.md) |

---

## 1회차 — 2026-11-13 (D-1 ~ D-60 · 문서 53개)

> 범위: `Docs/` 전체 (머지 대기 PR #58 ~ #66 내용 포함). 자동 검사(링크 · 헤더 · 표 형식 · 목록 · 결정 번호) + 핵심 수치 · 용어 대조 + 폴더 정리.
> 결론: **내용 모순은 거의 없고(보름달은 D-58로 이미 정정), 형식 · 관리 쪽 구멍이 컸다.** 가장 큰 것은 **결정 칸이 표 밖으로 밀려 안 보이던 45행** — 6개 문서의 "결정" 열이 비어 보였다.

### 1. 찾은 것 → 처리
| # | 검사 | 찾은 것 | 처리 |
|---|---|---|---|
| 1 | 표 형식 | ⚠ **결정 칸 밀림 45행** — `SYS_Growth` 7 · `SYS_Economy` 7 · `SYS_IdleReward` 6 · `SYS_FTUE` 7 · `SYS_InGameBattle` 12 · `CNT_Stage_Chapter1` 6. "DD 추천" 뒤에 빈 칸이 하나 더 있어(`\|\|`) D-29 ~ D-34 결정 표기가 표 밖 열로 밀림 (예전 칸 채우기 실수) | **고침** — 빈 칸 제거, 모든 표 행의 칸 수 = 머리 칸 수 확인 |
| 2 | 상단 상태 ↔ `KN_DocMap` | 13개 불일치 — 결정이 났는데 상단은 Draft · Review (`SYS_Economy` · `SYS_FTUE` · `SYS_Growth` · `SYS_IdleReward` · `SYS_Skill` · `CNT_Stage_Chapter1` · `CNT_Enemy` · `KN_Overview` · `KN_Glossary` · `KN_DocMap` · `KN_M1_DevBrief` · `KN_ConceptNarrative` · `UI_InGame`) | **고침** — 상단을 `Approved (D-n)` 등으로 맞춤 · `SYS_InGameBattle`은 Review(③ 1건 I-7 대기) |
| 3 | 헤더 블록 | `Principles` · `Notion_Hub` 헤더 없음 | **추가** (내용 그대로) |
| 4 | 최종 수정 날짜 | 16개가 실제 마지막 수정보다 오래됨 | **맞춤** |
| 5 | 깨진 링크 | 0 (1건은 `[받기](금)` 글자 — 링크 아님) | — |
| 6 | 없는 문서 이름 참조 | 28곳 중 26곳 = 계획 문서(`SYS_Shop` · `SYS_LiveOps` · `CNT_Item` · `BAL_ProgressionSim` …) 정상 / **낡은 이름 2곳** (`BAL_Curve` · `CNT_Stage`) | **고침** |
| 7 | `KN_DocMap` | `ART_ReviewSheet` 행 칸 수 틀림 · `KN_Roadmap` 행 칸 하나 빠짐 · 템플릿 경로 | **고침** · `KN_DocReview` 행 추가 |
| 8 | 결정 번호 | D-1 ~ D-60 빠짐 · 중복 없음 · 없는 번호 참조 없음 | — |
| 9 | 핵심 수치 대조 | 보름달 +30% · 소환 상한 12(→ P1 20) · 1-10 제한시간 ≈ 270초 · 로스터 7 / 9 / 15 · 외형 상한 +10% · 입장권 3 · 소환 100 — **문서 간 일치** (보름달 +20%는 D-58로 이미 정정) | — |
| 10 | 용어집 | `KN_Glossary`가 11/11 그대로 — D-45 ~ D-60 용어 없음, "파견 중 편성 가능"(D-54와 반대) · "외형변경 = 코스튬" 낡음 | **갱신** — 외형 · 의뢰 · 기억 각성 / 개화 · 유실표 장부 · 먼저 만남 · 기억의 안쪽 · 시설 · 서버 묶음 · 도감 분류 + **§5 화면 라벨 규칙**(D-60) |
| 11 | 로드맵 | `KN_Roadmap` §5 일정이 11/11 계획 그대로 — 끝난 카드(K-02 · K-04 · K-06 · K-07 ~ K-09)가 앞으로 할 일로 남음 | **갱신** — 실제 진행 · 11/23 1차 마감 · 11/24 R3 · K-12 시설 반영 · A-02 P0 7건 |
| 12 | ⚠ **이름 충돌** | 수선점 시설 "**기억등**"(`SYS_Atelier`, D-55) ↔ 세계관 "**일곱 기억등**"(지역 등불 = 장, 최종 목표 — `KN_ConceptNarrative` · D-53) | **결정 필요** (§3 #1) |
| 13 | "① 현재 구현" 속 옛 용어 | 불빛 게이지 10 · 기억편 10 · 의상 12 · 미보유 11 문서 | 정상 — ①은 지금 코드 서술. I-10 머지 뒤 기개발이 ① 갱신 (D-25) |

### 2. 폴더 정리
| 한 것 | 내용 |
|---|---|
| `_Templates/` 신설 | `_Template_System.md`(Docs 바로 아래) · `_Template_Table.md`(05_DataTable) 이동 + **아트 · 회의록 양식 신규** — I-4 #1 중 PR #2와 무관한 부분 |
| `.gitkeep` 정리 | 파일이 있는 폴더 6곳의 `.gitkeep` 삭제 (빈 폴더 06 · 08 · 09 · 05는 유지) |
| `README.md` | 폴더 표 예시를 실제 문서로 · `_Templates` 행 · 템플릿 4종 링크 · **§6 문서 찾기 = `KN_DocMap`** (목차 사본을 두지 않음) · 상태 표기 규칙 |
| 남은 것 (I-4) | `Docs/LostAndLovely/` 11개 이관 · `CONTRIBUTING.md` 폴더 구조 — **PR #2 머지 뒤** (§3 #2) |

```
Docs/
├─ README.md · decisions.md
├─ 00_Overview   KN_* (개요 · 범위 · 로드맵 · 용어 · 목록 · 검수 · 지시 합본 · 플레이테스트) · Principles · Notion_Hub
├─ 01_System     SYS_* 16
├─ 02_Content    CNT_* 7
├─ 03_UI_UX      UI_* 10 · images/
├─ 04_Balance    BAL_BattleKnobs
├─ 05_DataTable  (TBL_* 예정)
├─ 06_Meeting    (비어 있음)
├─ 07_Art        ART_* 6
├─ 08_Audio · 09_Research   (I-4 이관 대기)
├─ 99_Archive    Source/ (변환 전 원본)
└─ _Templates    System · Table · Art · Meeting
```

### 3. 결정 필요 — DD 작성, 대표 결정
| # | 항목 | 지금 | 선택지 | DD 추천 | 결정 |
|---|---|---|---|---|---|
| 1 | 수선점 빛 시설 이름 (`SYS_Atelier`) | "기억등" — 세계관 일곱 기억등(지역 · 최종 목표)과 같은 이름 | 처마등 / 수선점 등불 / 기억등 그대로 | **처마등** — 수선점 처마에 거는 등, "빛이 닿는 만큼 잔향이가 머문다"(빛의 반경)와 맞고 기억등과 안 겹침 | |
| 2 | I-4 (문서 폴더 정리) 범위 | 기개발 지시 — 템플릿 · README · 이관 · CONTRIBUTING 전부 | 템플릿 · README는 DD가 끝냄 → I-4 = LostAndLovely 이관 + CONTRIBUTING만 · README "전체 목차" 대신 `KN_DocMap` 링크 | **채택** — 목차를 README에도 두면 DocMap과 어긋남 (README §4 원칙) | |
| 3 | 검수 주기 | 없음 | 결정 10개마다 1회 / 매주 월요일 / 요청 때만 | **결정 10개마다 1회** (다음 = D-70 무렵) — 오늘처럼 형식 구멍이 쌓이기 전에 | |
| 4 | 다음 개선 | — | `KN_ConceptNarrative` v0.3 개정 (D-45 · D-48 · D-53 반영 — DocMap 🟡) / 문구 테이블(`Text_Def`) 설계 / 대기 | **`KN_ConceptNarrative` v0.3** — 컨셉 원본이 결정들보다 뒤처져 있으면 아트 · 스토리 작업자가 옛 설정을 봄 | |
