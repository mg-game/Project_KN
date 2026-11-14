# AGENTS.md · Project_KN에서 일하는 AI가 먼저 읽을 것

이 파일은 **공통 규칙이 어디에 있는지만** 적는다. 규칙 내용은 아래 정본 문서에만 있고, 여기에 옮겨 적지 않는다.
정본과 이 파일이 다르면 정본이 맞다.

## 1. 일을 시작하기 전에 읽는 순서

1. [`Docs/decisions.md`](Docs/decisions.md) 맨 위 "★ 지금 상태": 무엇이 정해졌고 무엇이 진행 중인지
2. 맡은 일의 기획서: 시스템은 `Docs/01_System/SYS_*.md`, 화면은 `Docs/03_UI_UX/UI_*.md`. 어느 문서인지 모르면 [`Docs/00_Overview/KN_DocMap.md`](Docs/00_Overview/KN_DocMap.md)
3. [`CONTRIBUTING.md`](CONTRIBUTING.md): 브랜치 · 커밋 · PR · 충돌 방지

## 2. 공통 규칙 위치

| 무엇 | 정본 위치 | 한 줄 요지 |
|---|---|---|
| 팀 원칙 (mg-game 10계명) | [`Docs/00_Overview/Principles.md`](Docs/00_Overview/Principles.md) | 계명이 방향, 세부 규칙이 방법 |
| 역할 · 검수 · AI 지시 흐름 | [`CONTRIBUTING.md`](CONTRIBUTING.md) §0 (R1~R10) | 검수 · 머지 · 결정 기록은 DD 한곳 |
| 브랜치 · 커밋 메시지 · PR | [`CONTRIBUTING.md`](CONTRIBUTING.md) §2 · §3 · §5 | develop에 직접 push 금지, PR로만 |
| Unity 충돌 방지 | [`CONTRIBUTING.md`](CONTRIBUTING.md) §4 | .meta 함께 커밋, 빌더 생성물(`Main.unity` · UI 프리팹)은 손으로 고치지 않음 (D-17) |
| 폴더 구조 | [`CONTRIBUTING.md`](CONTRIBUTING.md) §6 | 우리 에셋은 `Assets/_Project/` |
| 기획 문서 관리 | [`Docs/README.md`](Docs/README.md) | 분류 · 파일 이름 · 상태 헤더, 템플릿은 `Docs/_Templates/` |
| 결정 기록 | [`Docs/decisions.md`](Docs/decisions.md) | 기록은 DD가 한다 (R4). 다른 AI는 PR 본문에 "결정 기록 요청"으로 남긴다 |
| 용어 정본 | [`Docs/00_Overview/KN_Glossary.md`](Docs/00_Overview/KN_Glossary.md) | 화면 글자는 이 용어로 |
| UI 디자인 원칙 · 공통 규격 | [`Docs/03_UI_UX/UI_InGame.md`](Docs/03_UI_UX/UI_InGame.md) §2 | 시각 방향 · 정보 계층 · 공통 상태 표현 |
| 공통 HUD · 팝업 규칙 | [`Docs/03_UI_UX/UI_InGame.md`](Docs/03_UI_UX/UI_InGame.md) §4 (§4.3 공통 팝업) | 바깥 영역 눌러 닫기, 탭 자동 이동 (`AutoTabRunner`) 등 |
| 화면별 규칙 | `Docs/03_UI_UX/UI_*.md` | 메타 화면(일지 · 도감 · 우편 · 설정 등)은 [`UI_Meta`](Docs/03_UI_UX/UI_Meta.md) |
| UI 크기 · 모션 · 색 상수 | `Assets/_Project/Scripts/UI/UITokens.cs` · `UITheme.cs` | 화면 코드가 쓰는 공용 값 모음 |
| 아트 스타일 | [`Docs/07_Art/ART_StyleGuide.md`](Docs/07_Art/ART_StyleGuide.md) | 계명 #8 "아트는 한 사람이 그린 것처럼" |
| 아트 할 일 · 시안 보관 | [`Docs/07_Art/ART_Queue.md`](Docs/07_Art/ART_Queue.md) (D-70) · [`ArtSource/`](ArtSource/README.md) (D-71) | 시안은 `ArtSource/Sheets/`, 게임에 쓰는 그림은 `Assets/_Project/Art/` |
| 참고용 원본 스펙 | `Docs/LostAndLovely/` | 정본이 아니다 (D-66). 정본은 위 문서들 |
| 코드 서식 | [`.editorconfig`](.editorconfig) | |

## 3. PR을 올리기 전에

- 기능 PR에는 해당 기획서의 "① 현재 구현" 갱신을 같이 넣는다 (D-25).
- 코드 PR에는 로컬 EditMode 테스트 결과를 붙인다 (D-21). Unity를 돌리지 못했으면 미검증이라고 적는다 (`CONTRIBUTING.md` R7-1).
- PR 양식은 [`.github/pull_request_template.md`](.github/pull_request_template.md)를 따른다.

## 4. 이 파일을 고치는 법

- 공통 규칙을 새로 만들거나 옮기면, 먼저 정본 문서에 쓰고 §2 표에 위치를 한 줄 더한다.
- 규칙 내용을 이 파일에 길게 쓰지 않는다. 내용이 두 곳에 있으면 언젠가 어긋난다.
