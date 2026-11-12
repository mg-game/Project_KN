# ART_M1_Backlog — M1 신규 · 변경 아트 목록

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | DD |
| 최종 수정 | 2026-11-12 |
| 관련 테이블 | - |
| 관련 문서 | [`ART_StyleGuide`](./ART_StyleGuide.md) · [`UI_Battle`](../03_UI_UX/UI_Battle.md) §4 · [`UI_Formation`](../03_UI_UX/UI_Formation.md) §5 · [`CNT_Stage_Chapter1`](../02_Content/CNT_Stage_Chapter1.md) · [`KN_Overview`](../00_Overview/KN_Overview.md) §2.2 |

> M1(D-35: **L&L 현재 아트로 진행**)에서 I-10 전투 · 편성 변경 때문에 **새로 필요하거나 바꿔야 하는 에셋만** 모았다. 대표 캐릭터 신규 아트는 이 목록이 아니라 `ART_Character_Brief`(시안, M1 중 병행).
> 제작: AI 아트 (DD 지시 → 지시함). 스타일 기준: `ART_StyleGuide` §2 마스터 프롬프트 · §9 UI 톤(딥 퍼플 · 인디고 패널, 얇은 골드 선, 둥근 형태, 파스텔 포인트).
> 규칙: 경로 `Assets/_Project/Art/<분류>/`, 파일명 `snake_case.png`, **LFS에는 최종본만 커밋**(D-24 — 시안은 Notion · 보드). "재사용" 칸에 적힌 기존 파일을 먼저 확인하고, 맞으면 새로 만들지 않는다.

## 1. 목록

| ID | 에셋 | 쓰는 곳 | 상태 · 변형 | 크기 (px) | 재사용 · 기준 파일 | 주 | 우선 |
|---|---|---|---|---|---|---|---|
| ART-01 | **행동 성향 아이콘 3종** — 앞장서기(화살) · 함께 걷기(원 화살표) · 곁 지키기(방패) | 덱 카드 · 편성 카드 · 필터 (UI_Battle ⑬ · UI_Formation ⑦ ⑪) | 켜짐 · 흐림 2상태 | 128 (화면 32~48) | 스타일 기준 `Icons/guardian.png` · `Icons/echo.png` | W2 | **P0** |
| ART-02 | **수선등 버튼** — 등불 아이콘 + 레벨 점 5개 | 잔향 줄 오른쪽 (D-44 #2 · UI_Battle ⑭) | Lv1~5 · MAX · 비활성 | 버튼 9-slice + 아이콘 128 | `Icons/lamp.png` · `UI/button_gold_9s.png` 바탕 | W2 | **P0** |
| ART-03 | **수선점 창문 불빛 단계** — 수선점 위 오버레이 | 전장 수선점 (투자 연출) | L1~L5 (창문 1~5개 켜짐) | `Battle/shop.png`와 같은 캔버스 | `Battle/shop.png` · `shop_crack_1~3.png` 위에 겹침 | W2 | **P0** |
| ART-04 | **8방위 링 세트** — 링 바탕(8 눈금) · 빈 방위 빗금(보통 · 빨강) · 위험 ▲ · 지속 압박 줄무늬 | 편성 링 · 전투 PREP (UI_Formation ④⑤⑥) | 4종 | 링 1024, 마커 128 | 슬롯은 `Battle/guardian_pad*.png` 재사용 | W1 | **P0** |
| ART-05 | **요격 부채꼴** | 편성 · 조준 프리뷰 | — | — | **아트 없음** — 코드(메시 · 셰이더)로 그림 권장. `Sprites/Battle/fx_range_disc.png` 색만 참고 | W1 | — |
| ART-06 | **웨이브 예고 마커** — 링 가장자리 화살표 + 지속 압박 고정 마커 | 전장 (UI_Battle ⑩) | 예고(깜빡임) · 고정 | 128 | `FX/ring_glow.png` 톤 | W1 | **P0** |
| ART-07 | **상단 HUD 소품** — 시계 아이콘 · 웨이브 칸(일반 · 보스 해골) · "남은 적" 배지 | 전투 상단 (UI_Battle ②③④) | 보통 · 경고(빨강) | 64~128 | `UI/panel_night_9s.png` 위에 | W1 | **P0** |
| ART-08 | **실패 리포트 아이콘** — 원인 2종(방어 문제 · 화력 부족) + CTA 3종(재도전 · 편성 · 성장) | RS-002 (D-28 #10) | — | 128 | `Icons/guardian.png` · `Icons/power.png` 재사용 가능 여부 먼저 확인 | W2 | P1 |
| ART-09 | **파견 중 배지** | 편성 목록 카드 (UI_Formation ⑪) | — | 64 | 원정 아이콘 톤 | W2 | P1 |
| ART-10 | **1-10 보스 변형 "기억등을 삼킨 좀나방 여왕"** — 몸 안 기억등 빛 + 8방향 부하 소환 연출 | 1-10 (D-34 #5) | 기본 · 소환 순간 | `Enemies/boss_mothqueen.png`와 같은 캔버스 | `Enemies/boss_mothqueen.png` 변형 | W3 | **P0** |
| ART-11 | **장소 6곳 배경** | 스테이지 배경 (D-34 #3) | — | `Battle/field_tall.png`와 같은 캔버스 | **M1은 기존 `field_tall.png` + 장소별 색 틴트만** (§2 판단) | W3 | P1 |
| ART-12 | **보름달 기믹 아이콘** — 효과는 D-50 #2의 "잔향 수급 +20%" | 기믹 안내 | — | 128 | `UI/moon.png` 재사용 → 아이콘에 잔향 표식만 추가 | W3 | P2 |
| ART-13 | **우두커니 받침 대비 보정** — 바닥(L≈83) 대비 +27로 묻힘 (`ART_BattleSpriteSpec` §1.4) | 전장 지킴이 | 받침 색만 밝게 (몸 그림 유지) | 256 | `Battle/guardian_pad*.png` 해당 색 1장 | W3 | P1 |
| ART-14 | **골무 껍데기 외곽 보정** — 바닥과 밝기 차이 −2 | 전장 망각체 | 밝은 외곽 1 px(전장 기준, 원본 256에서 ≈ 3 px) 추가 | 256 | `Enemies/enemy_thimble.png` | W3 | P1 |
| ART-15 | **프롤로그 그림 2장** — ① 문 닫은 분실물 보관소 · 이름 지워진 열쇠 · 벽의 문 ② 불 켜진 수선점에서 깨어나는 몽실 · 짝짝이 (D-53 #2, `CNT_Story` §2.2) | 인트로 | 기존 인트로 그림 크기 · 톤 | 인트로 화면 | `Story/intro_*` | W3 | P1 (없으면 기존 그림 + 새 문구로 M1) |

## 2. 판단 메모 (DD)
- **ART-05 부채꼴은 코드로**: 섹터 폭 · 사거리가 성향 · 지킴이마다 달라 스프라이트로는 변형이 너무 많다.
- **ART-11 장소 배경은 M1 틴트**: 신규 배경 6장은 M1 범위(현재 아트) 밖. 장소 이름 + 색 틴트 + 기믹 FX(비 · 바람 · 정전 …, 이미 있음)로 구분한다. 신규 배경은 M2 아트 교체 때.
- **수량**: 실제 §1 표 기준 **P0 7건 · P1 6건 · P2 1건**, 코드 표현 권장 1건(ART-05) = 총 15건. P0 중 UI·전장 소품은 ART-01~04·06~07의 6건이며 ART-10은 보스 변형이다.
- **UI 후보 제작·검수 기록**: [`ART_UIHandoff`](./ART_UIHandoff.md). 시안 제작과 정식 채택·Unity 적용 상태를 구분한다. D-44의 시트 위 전투 알림 칩은 기존 ART ID를 재번호화하지 않고 별도 D-44 항목으로 추적한다.

## 3. 공통 프롬프트 (UI 소품용)
```
Game-ready 2D mobile UI icon for a whimsical fairy-tale idle defense RPG. Soft storybook style, clean medium outline, soft cel shading, deep indigo and purple base with restrained thin gold accents and pastel highlight. Simple bold silhouette readable at 32px. Transparent background, centered, no text. Subject: <에셋 내용>.
```
```
게임용 2D 모바일 UI 아이콘. 말랑한 동화풍 방치 디펜스 RPG. 또렷한 중간 굵기 외곽선, 부드러운 셀 채색, 딥 인디고 · 보라 바탕에 얇은 금색 포인트와 파스텔 하이라이트. 32px에서도 읽히는 단순한 실루엣. 투명 배경, 가운데 정렬, 글자 없음. 대상: <에셋 내용>.
```
금지(ART_StyleGuide §14): 네온 SF 색감 · 복잡한 금속 장식 · 작은 패턴 남용 · 글자 넣기.

## 4. 납품 · 검수
1. AI 아트 → 시안은 Notion 아트 페이지 (Git에 올리지 않음)
2. DD 검수: 스타일 가이드 §15 체크 · 32px 축소 가독성 · 상태 변형 개수
3. 대표 채택 결재 → 최종본만 PR (LFS), 파일명 · 경로는 §1 표 기준
4. 기개발이 화면에 연결 (I-10 해당 PR)
