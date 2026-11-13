# Docs — 기획 문서 관리 규칙

## 1. 폴더 분류

| 폴더 | 내용 | 예시 |
|---|---|---|
| 00_Overview | 게임 개요 · M1 범위, 로드맵, 용어집, **문서 목록(`KN_DocMap`)** | `KN_Overview.md`, `KN_Glossary.md`, `KN_DocMap.md` |
| 01_System | 시스템 기획서 | `SYS_IdleReward.md`, `SYS_Gacha.md` |
| 02_Content | 스테이지, 캐릭터, 이벤트 등 콘텐츠 | `CNT_Stage_Chapter1.md` |
| 03_UI_UX | 화면 명세, UI 플로우, 상태 전이 (이미지는 `images/`) | `UI_Home.md` |
| 04_Balance | 수치 설계 근거, 노브, 성장 곡선 | `BAL_BattleKnobs.md` |
| 05_DataTable | 테이블 스키마 정의 (컬럼/타입/관계) | `TBL_Character.md` |
| 06_Meeting | 회의록 | `2026-11-10_Kickoff.md` |
| 07_Art | 아트 방향, 스타일 가이드, FX, 아트 작업 계획 | `ART_StyleGuide.md` |
| 08_Audio | 사운드 · BGM 방향과 명세 | `AUD_Spec.md` |
| 09_Research | 경쟁작 · 레퍼런스 분석, 리서치 | `RES_IdleGames.md` |
| 99_Archive | 폐기/구버전 문서 (삭제 대신 이동) · 변환 전 원본 `Source/` | - |
| _Templates | 문서 양식 (시스템 · 테이블 · 아트 · 회의록 · 캐릭터 · 장) | `_Template_System.md` |

## 2. 파일 네이밍

```
<접두어>_<대상>[_<세부>].md
  SYS_  시스템 | CNT_ 콘텐츠 | UI_ 화면 | BAL_ 밸런스 | TBL_ 테이블
  ART_  아트   | AUD_ 오디오 | RES_ 리서치 | KN_ 프로젝트 전체(개요 · 로드맵)
```
- 영문 PascalCase, 공백 금지 (URL/CLI 호환)
- 버전 번호를 파일명에 넣지 않는다 → 이력은 Git이 관리 (`_v2_최종_진짜최종` 금지)
- **이미지 · 와이어프레임**: 문서와 같은 폴더의 `images/`에 `<문서명>_<내용>.png` (LFS). 문서 헤더 "첨부"에 링크. **캐릭터 아트 시안·중간본은 D-24에 따라 Notion/보드에 두고 URL만 기록**한다. 기존 Git 컨셉 파일은 보존한다.
- **변환 전 원본(docx · pptx · pdf)**: `99_Archive/Source/`에 참고용으로만 둔다. 정본은 md 하나

## 3. 문서 상태 관리

모든 문서 상단에 헤더 블록 필수:

```
| 항목 | 값 |
|---|---|
| 상태 | Draft → Review → Approved → Deprecated |
| 작성자 | |
| 최종 수정 | YYYY-MM-DD |
| 관련 테이블 | TBL_xxx |
| 관련 이슈 | #번호 |
```

```
Draft ──(PR 생성)──→ Review ──(리드 승인/머지)──→ Approved
  ↑                    │                           │
  └──(수정 요청)───────┘                (사양 폐기) → Deprecated → 99_Archive 이동
```

## 4. 원본 위치 원칙

| 자료 | 원본 위치 | 저장소에 두는 것 |
|---|---|---|
| 시스템/콘텐츠 기획서 | `Docs/*.md` | 원본 그대로 |
| 데이터 (수치 · 콘텐츠) | 당분간 코드 콘텐츠 `Assets/_Project/Editor/Content` (D-20). 밸런스 표만 추후 시트 | 코드 원본 + 빌더 생성 결과(`Assets/_Project/Data`) |
| UI 시안 | Figma | 링크 + 확정 캡처 |

> ⚠️ 원본이 두 곳에 있으면 반드시 어긋난다. 위 표 외의 위치에 사본을 만들지 않는다.

## 5. 템플릿

- 시스템 기획서: [`_Templates/_Template_System.md`](./_Templates/_Template_System.md)
- 테이블 스키마: [`_Templates/_Template_Table.md`](./_Templates/_Template_Table.md)
- 아트 문서: [`_Templates/_Template_Art.md`](./_Templates/_Template_Art.md)
- 회의록: [`_Templates/_Template_Meeting.md`](./_Templates/_Template_Meeting.md)
- 장(챕터) 묶음: [`_Templates/_Template_Chapter.md`](./_Templates/_Template_Chapter.md) — 첫 예시 `02_Content/CNT_Stage_Chapter2.md` (D-67)
- 캐릭터 원본: [`_Templates/_Template_Character.md`](./_Templates/_Template_Character.md) · 캐릭터 외형: [`_Templates/_Template_CharacterAppearance.md`](./_Templates/_Template_CharacterAppearance.md)

## 6. 문서 찾기 · 상태

- **전체 문서 목록 · 진행 상태 = [`00_Overview/KN_DocMap.md`](./00_Overview/KN_DocMap.md)** (DD 관리, 목차를 여기에 따로 두지 않음 — 사본은 어긋남, §4)
- 결정 기록 = [`decisions.md`](./decisions.md) · 용어 = [`00_Overview/KN_Glossary.md`](./00_Overview/KN_Glossary.md)
- 검수 기록 = [`00_Overview/KN_DocReview.md`](./00_Overview/KN_DocReview.md)
- 상태 표기: `Approved (D-n)` = 그 결정으로 확정 · `Review` = 결정 대기 · `Draft` = 쓰는 중. 문서 상단 상태와 `KN_DocMap` 상태는 같아야 한다

## 7. 캐릭터 기획 · AI 아트 관리

- [원본 기획 템플릿](./_Templates/_Template_Character.md) → `02_Content/CNT_Character_<Name>.md`
- [외형 템플릿](./_Templates/_Template_CharacterAppearance.md) → `02_Content/CNT_CharacterAppearance_<Name>_<Appearance>.md`
- [공통 전달 마스터](./07_Art/ART_CharacterHandoff.md): 원본 → 외형 → 카드 → 상세 텍스트 → 아트 전달 → 결과 검수. 결과 검수 표 = [`07_Art/ART_ReviewSheet.md`](./07_Art/ART_ReviewSheet.md) (D-46 #5)
- [기존 설정집과 우선 3종 연결](./02_Content/CNT_Character_Main.md#12-캐릭터-원본--외형-문서-연결): 기존 원문은 보존하고 필드별 링크로 참조. 신규 캐릭터는 개별 문서 한 곳에만 원문 작성.
- [구조 분석·적용 기록](./07_Art/ART_CharacterWorkflowAudit.md): 보존·변경 목록, ID/경로, 첨부 문서 충돌 처리, 검사 결과, 신규 추가 방법.

캐릭터 기본 설정을 외형 문서에 복사하지 않는다. 공통 실행 프롬프트는 마스터 한 곳에 두며, 스타일과 게임 리소스 규격은 기존 전문 문서로 연결한다. 문서 관리 ID와 코드 ID를 구분하고, 기획 상태(확정/검토 중/폐기)는 문서 상태와 별도로 기록한다.

> 2026-11-12 변경: 기존 폴더를 유지한 템플릿·전달 지침·문서 연결 추가. 기존 게임 설정·이미지·ID 변경 없음.
