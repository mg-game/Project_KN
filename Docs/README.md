# Docs — 기획 문서 관리 규칙

## 1. 폴더 분류

| 폴더 | 내용 | 예시 |
|---|---|---|
| 00_Overview | 게임 컨셉, 핵심 루프, 로드맵, 용어집 | `KN_Overview.md`, `Glossary.md` |
| 01_System | 시스템 기획서 | `SYS_IdleReward.md`, `SYS_Gacha.md` |
| 02_Content | 스테이지, 캐릭터, 이벤트 등 콘텐츠 | `CNT_Stage_Chapter1.md` |
| 03_UI_UX | 화면 명세, UI 플로우, 상태 전이 | `UI_Lobby.md` |
| 04_Balance | 수치 설계 근거, 성장 곡선, 재화 흐름 | `BAL_GrowthCurve.md` |
| 05_DataTable | 테이블 스키마 정의 (컬럼/타입/관계) | `TBL_Character.md` |
| 06_Meeting | 회의록 | `2026-11-10_Kickoff.md` |
| 99_Archive | 폐기/구버전 문서 (삭제 대신 이동) | - |

## 2. 파일 네이밍

```
<접두어>_<대상>[_<세부>].md
  SYS_  시스템 | CNT_ 콘텐츠 | UI_ 화면 | BAL_ 밸런스 | TBL_ 테이블
```
- 영문 PascalCase, 공백 금지 (URL/CLI 호환)
- 버전 번호를 파일명에 넣지 않는다 → 이력은 Git이 관리 (`_v2_최종_진짜최종` 금지)

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

- 시스템 기획서: [`_Template_System.md`](./_Template_System.md)
- 테이블 스키마: [`05_DataTable/_Template_Table.md`](./05_DataTable/_Template_Table.md)
