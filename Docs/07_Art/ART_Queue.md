# ART_Queue — 아트 할 일 대기열

| 항목 | 값 |
|---|---|
| 상태 | Approved (D-70) — 살아 있는 대기열 |
| 작성자 | DD (작업 추가 · 검수 · 완료 처리) · 아트 세션 (진행 · 제출 표시) |
| 최종 수정 | 2026-11-14 |
| 관련 문서 | [`ART_CharacterHandoff`](./ART_CharacterHandoff.md) · [`ART_Character_Brief`](./ART_Character_Brief.md) · [`ART_ReviewSheet`](./ART_ReviewSheet.md) · [`ART_M1_Backlog`](./ART_M1_Backlog.md) · [`ART_BattleSpriteSpec`](./ART_BattleSpriteSpec.md) |

> **대표가 아트 세션에 보내는 말은 `아트 큐` 한 마디.** 무엇을 · 어떤 기준으로 · 어디에 올릴지는 전부 이 표에 있다.

## 1. 아트 세션 규칙 ("아트 큐"를 받으면)

```
1. develop 최신을 받는다 → 이 문서 §2 표를 연다
2. 상태 "대기" 중 # 가장 작은 것부터 1건씩
   IF 대기 없음 → ART_M1_Backlog 미완료 항목을 위에서부터
3. 시작할 때 상태 → "진행" (작업 PR 안에서 이 표도 같이 고침)
4. 산출물 종류에 따라:
   시안 (시트 · 카드)  → 깃 `ArtSource/Sheets/<원물 키>_sheet_v<N>.png` PR (LFS · Unity 밖, D-71). 표 "제출" 칸에 PR 번호, 상태 → "검수 대기"
   게임 PNG (최종 규격) → 깃 PR (LFS, D-65). 표 "제출" 칸에 PR 번호, 상태 → "검수 대기"
5. 머지하지 않는다. 대표에게 "아트 큐 #N 검수 대기 — PR #번호 / 링크" 한 줄로 보고
6. 기준 문서와 다르게 그려야 할 이유가 있으면 그리지 말고 "질문" 상태 + 비고에 이유
```

**DD 규칙:** 새 작업은 표 맨 아래에 "대기"로 추가 · 검수 결과(`ART_ReviewSheet` 또는 해당 문서)를 남기고 상태 → "완료" / "수정 1회"(비고에 고칠 점 → 다시 "대기").

상태: `대기` → `진행` → `검수 대기` → `완료` · 갈래: `수정 1회` · `질문` · `보류`

## 2. 대기열

| # | 상태 | 작업 | 기준 문서 (프롬프트 · 규격) | 산출 | 올리는 곳 | 제출 | 요청 · 결정 |
|---|---|---|---|---|---|---|---|
| 1 | 대기 | **웰리** 기본형 시트 (장화 · 2장 구조 A) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 웰리 행 + 추가 프롬프트 · [`ART_CharacterHandoff`](./ART_CharacterHandoff.md) §8 A · 원본 [`CNT_Character_Boots`](../02_Content/CNT_Character_Boots.md) | 시트 1장 (전신 3/4 · 검은 실루엣 · 64 px · 표정 3, 글자 없음) | 채팅 / Notion (깃 X) | - | 2026-11-13 · D-68 #4 |
| 2 | 대기 | **보틀리** 기본형 시트 (유리병 편지 · 2장 구조 B) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 보틀리 행 + 추가 프롬프트 · §8 A · 원본 [`CNT_Character_BottleLetter`](../02_Content/CNT_Character_BottleLetter.md) | 시트 1장 (위와 같음) | `ArtSource/Sheets/bottleletter_sheet_v1.png` PR (D-71) | - | 2026-11-13 · D-68 #4 |
| 3 | 대기 | **푸딩 경** 기본형 시트 (4장 구조 A · 설정집 §8) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 푸딩 경 행 + 추가 프롬프트 · [`ART_CharacterHandoff`](./ART_CharacterHandoff.md) §8 A · 원문 [설정집](../02_Content/CNT_Character_Main.md#8-푸딩-경) | 시트 1장 (전신 3/4 · 검은 실루엣 · 64 px · 표정 3, 글자 없음) | `ArtSource/Sheets/pudding_sheet_v1.png` PR | - | 2026-11-14 · D-46 · D-53 · D-71 #2 |
| 4 | 대기 | **네임리스 00** 기본형 시트 (7장 · 설정집 §9) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 네임리스 00 행 + 추가 프롬프트 · [`ART_CharacterHandoff`](./ART_CharacterHandoff.md) §8 A · 원문 [설정집](../02_Content/CNT_Character_Main.md#9-네임리스-00) | 시트 1장 (전신 3/4 · 검은 실루엣 · 64 px · 표정 3, 글자 없음) | `ArtSource/Sheets/nameless_sheet_v1.png` PR | - | 2026-11-14 · D-46 · D-53 · D-71 #2 |
| 5 | 대기 | **피피** 기본형 시트 (현관문 외시경 (문에 달린 작은 렌즈) · 4장 구조 4-10) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 피피 행 + 추가 프롬프트 · §8 A · 원본 [`CNT_Character_Peephole`](../02_Content/CNT_Character_Peephole.md) | 시트 1장 (전신 3/4 · 검은 실루엣 · 64 px · 표정 3, 글자 없음) | `ArtSource/Sheets/peephole_sheet_v1.png` PR | - | 2026-11-14 · D-71 |
| 6 | 대기 | **칙폭이** 기본형 시트 (태엽 장난감 기관차 · 5장 구조 5-5) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 칙폭이 행 + 추가 프롬프트 · §8 A · 원본 [`CNT_Character_ToyTrain`](../02_Content/CNT_Character_ToyTrain.md) | 시트 1장 (전신 3/4 · 검은 실루엣 · 64 px · 표정 3, 글자 없음) | `ArtSource/Sheets/toytrain_sheet_v1.png` PR | - | 2026-11-14 · D-71 |
| 7 | 대기 | **아홉시** 기본형 시트 (9시에 멈춘 회중시계 · 5장 구조 5-10) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 아홉시 행 + 추가 프롬프트 · §8 A · 원본 [`CNT_Character_PocketWatch`](../02_Content/CNT_Character_PocketWatch.md) | 시트 1장 (전신 3/4 · 검은 실루엣 · 64 px · 표정 3, 글자 없음) | `ArtSource/Sheets/pocketwatch_sheet_v1.png` PR | - | 2026-11-14 · D-71 |
| 8 | 대기 | **글로비** 기본형 시트 (스노우볼 · 6장 구조 6-5) | [`ART_Character_Brief`](./ART_Character_Brief.md) §1 글로비 행 + 추가 프롬프트 · §8 A · 원본 [`CNT_Character_SnowGlobe`](../02_Content/CNT_Character_SnowGlobe.md) | 시트 1장 (전신 3/4 · 검은 실루엣 · 64 px · 표정 3, 글자 없음) | `ArtSource/Sheets/snowglobe_sheet_v1.png` PR | - | 2026-11-14 · D-71 |

## 3. 완료 기록 (최근 → 과거)

| # | 작업 | 결과 | 기록 |
|---|---|---|---|
| - | 2장 망각체 3종 (첨벙이 · 번진 사진 · 녹슨 우산살) | 채택 | PR #84 · [`ART_Enemy_Chapter2`](./ART_Enemy_Chapter2.md) §5 (큐 도입 전) |
| - | 카라반 · 레이니 2회차 · 몽실 MW2 | 채택 | PR #71 · #75 · [`ART_ReviewSheet`](./ART_ReviewSheet.md) §8 (큐 도입 전) |
