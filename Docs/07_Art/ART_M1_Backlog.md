# ART_M1_Backlog — M1 신규 · 변경 아트 목록

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | DD |
| 최종 수정 | 2026-11-12 (ART-01 후보 제작 기록, 기존 목록 보존) |
| 관련 테이블 | - |
| 관련 문서 | [`ART_StyleGuide`](./ART_StyleGuide.md) · [`UI_Battle`](../03_UI_UX/UI_Battle.md) §4 · [`UI_Formation`](../03_UI_UX/UI_Formation.md) §5 · [`CNT_Stage_Chapter1`](../02_Content/CNT_Stage_Chapter1.md) · [`KN_Overview`](../00_Overview/KN_Overview.md) §2.2 |

> M1(D-35: **L&L 현재 아트로 진행**)에서 I-10 전투 · 편성 변경 때문에 **새로 필요하거나 바꿔야 하는 에셋만** 모았다. 대표 캐릭터 신규 아트는 이 목록이 아니라 `ART_Character_Brief`(시안, M1 중 병행).
> 제작: AI 아트 (DD 지시 → 지시함). 스타일 기준: `ART_StyleGuide` §2 마스터 프롬프트 · §9 UI 톤(딥 퍼플 · 인디고 패널, 얇은 골드 선, 둥근 형태, 파스텔 포인트).
> 규칙: 경로 `Assets/_Project/Art/<분류>/`, 파일명 `snake_case.png`, **LFS에는 최종본만 커밋**(D-24 — 시안은 Notion · 보드). "재사용" 칸에 적힌 기존 파일을 먼저 확인하고, 맞으면 새로 만들지 않는다.

## 1. 목록

| ID | 에셋 | 쓰는 곳 | 상태 · 변형 | 크기 (px) | 재사용 · 기준 파일 | 주 | 우선 |
|---|---|---|---|---|---|---|---|
| ART-01 | **행동 성향 아이콘 3종** — 앞장서기(화살) · 함께 걷기(원 화살표) · 곁 지키기(방패) | 덱 카드 · 편성 카드 · 필터 (UI_Battle ⑬ · UI_Formation ⑦ ⑪) | 켜짐 · 흐림 2상태 | 128 (화면 32~48) | 스타일 기준 `Icons/guardian.png` · `Icons/echo.png` | W2 | **P0** |
| ART-02 | **수선등 버튼** — 등불 아이콘 + 레벨 점 5개 | 전투 하단 2열 (UI_Battle ⑭) | Lv1~5 · MAX · 비활성 | 버튼 9-slice + 아이콘 128 | `Icons/lamp.png` · `UI/button_gold_9s.png` 바탕 | W2 | **P0** |
| ART-03 | **수선점 창문 불빛 단계** — 수선점 위 오버레이 | 전장 수선점 (투자 연출) | L1~L5 (창문 1~5개 켜짐) | `Battle/shop.png`와 같은 캔버스 | `Battle/shop.png` · `shop_crack_1~3.png` 위에 겹침 | W2 | **P0** |
| ART-04 | **8방위 링 세트** — 링 바탕(8 눈금) · 빈 방위 빗금(보통 · 빨강) · 위험 ▲ · 지속 압박 줄무늬 | 편성 링 · 전투 PREP (UI_Formation ④⑤⑥) | 4종 | 링 1024, 마커 128 | 슬롯은 `Battle/guardian_pad*.png` 재사용 | W1 | **P0** |
| ART-05 | **요격 부채꼴** | 편성 · 조준 프리뷰 | — | — | **아트 없음** — 코드(메시 · 셰이더)로 그림 권장. `Sprites/Battle/fx_range_disc.png` 색만 참고 | W1 | — |
| ART-06 | **웨이브 예고 마커** — 링 가장자리 화살표 + 지속 압박 고정 마커 | 전장 (UI_Battle ⑩) | 예고(깜빡임) · 고정 | 128 | `FX/ring_glow.png` 톤 | W1 | **P0** |
| ART-07 | **상단 HUD 소품** — 시계 아이콘 · 웨이브 칸(일반 · 보스 해골) · "남은 적" 배지 | 전투 상단 (UI_Battle ②③④) | 보통 · 경고(빨강) | 64~128 | `UI/panel_night_9s.png` 위에 | W1 | **P0** |
| ART-08 | **실패 리포트 아이콘** — 원인 2종(방어 문제 · 화력 부족) + CTA 3종(재도전 · 편성 · 성장) | RS-002 (D-28 #10) | — | 128 | `Icons/guardian.png` · `Icons/power.png` 재사용 가능 여부 먼저 확인 | W2 | P1 |
| ART-09 | **파견 중 배지** | 편성 목록 카드 (UI_Formation ⑪) | — | 64 | 원정 아이콘 톤 | W2 | P1 |
| ART-10 | **1-10 보스 변형 "기억등을 삼킨 좀나방 여왕"** — 몸 안 기억등 빛 + 8방향 부하 소환 연출 | 1-10 (D-34 #5) | 기본 · 소환 순간 | `Enemies/boss_mothqueen.png`와 같은 캔버스 | `Enemies/boss_mothqueen.png` 변형 | W3 | **P0** |
| ART-11 | **장소 6곳 배경** | 스테이지 배경 (D-34 #3) | — | `Battle/field_tall.png`와 같은 캔버스 | **M1은 기존 `field_tall.png` + 장소별 색 틴트만** (§2 판단) | W3 | P1 |
| ART-12 | **보름달 기믹 아이콘** — 효과가 "잔향 수급 +30%"로 바뀜 | 기믹 안내 | — | 128 | `UI/moon.png` 재사용 → 아이콘에 잔향 표식만 추가 | W3 | P2 |
| ART-13 | **우두커니 받침 대비 보정** — 바닥(L≈83) 대비 +27로 묻힘 (`ART_BattleSpriteSpec` §1.4) | 전장 지킴이 | 받침 색만 밝게 (몸 그림 유지) | 256 | `Battle/guardian_pad*.png` 해당 색 1장 | W3 | P1 |
| ART-14 | **골무 껍데기 외곽 보정** — 바닥과 밝기 차이 −2 | 전장 망각체 | 밝은 외곽 1 px(전장 기준, 원본 256에서 ≈ 3 px) 추가 | 256 | `Enemies/enemy_thimble.png` | W3 | P1 |
| ART-15 | **프롤로그 그림 2장** — ① 문 닫은 분실물 보관소 · 이름 지워진 열쇠 · 벽의 문 ② 불 켜진 수선점에서 깨어나는 몽실 · 짝짝이 (D-53 #2, `CNT_Story` §2.2) | 인트로 | 기존 인트로 그림 크기 · 톤 | 인트로 화면 | `Story/intro_*` | W3 | P1 (없으면 기존 그림 + 새 문구로 M1) |

## 2. 판단 메모 (DD)
- **ART-05 부채꼴은 코드로**: 섹터 폭 · 사거리가 성향 · 지킴이마다 달라 스프라이트로는 변형이 너무 많다.
- **ART-11 장소 배경은 M1 틴트**: 신규 배경 6장은 M1 범위(현재 아트) 밖. 장소 이름 + 색 틴트 + 기믹 FX(비 · 바람 · 정전 …, 이미 있음)로 구분한다. 신규 배경은 M2 아트 교체 때.
- **수량**: P0 8건 · P1 3건 · P2 1건. 대부분 아이콘 · UI 소품이라 AI 아트 1~2회 왕복 규모.

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

## 5. ART-01 행동 성향 아이콘 — 제작 후보 (2026-11-12)

사용자 “아트 작업 진행해”에 따라 우선 3종 캐릭터의 선정 핵심 형태/부속을 후속 점검하고, P0 ART-01 시안을 제작했다. [전투 UI](../03_UI_UX/UI_Battle.md)·[편성 UI](../03_UI_UX/UI_Formation.md)의 성향 표시와 [스타일](./ART_StyleGuide.md) §9를 기준으로 한다. 기존 목록의 범위·우선순위·필요 상태는 보존한다.

**상태: 시안·128 PNG·축소 검토 후보 준비. DD/사람 확인·최종 채택·Unity 연결은 미실시.** ART-01을 게임 적용 완료로 승격하지 않는다. 나머지 ART 항목의 제작 상태도 변경하지 않는다.

### 5.1 기존 리소스 재사용 검토·참조

- 기존 [guardian.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Icons/guardian.png)와 [echo.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Icons/echo.png)를 LFS 실물·해시와 대조하고 육안 확인했다. 두 파일과 `.meta`는 보존했다.
- guardian은 별·바늘이 있는 역할 방패이고 echo는 유령 원형 배지여서 성향의 화살/원 화살표/방패 3기호를 그대로 대체하지 않는다. 두 파일은 보라/골드·형태·재질·렌더링의 직접 이미지 참조로 사용했다.
- 공통 보라 원형 배지·절제된 골드 테두리를 두고 큰 단일 기호로 구분했다. 곁 지키기는 원형 배지 안의 단순 방패로, 기존 별/바늘 역할 방패와 세부를 구분한다. 신규 게임 규칙·성향 이름·효과를 만들지 않는다.
- guardian SHA-256 `ec15d7ca2ff862c47c76e83132d851795eca365d79c5af86229a8994993a5311`, 71,409 bytes. echo `c4ae714ffa2cae08d3d96277f75980f1fee2af567a4f9f24772563c35ae72444`, 89,363 bytes. 기준은 PR #2 `4b73a5a`다.

### 5.2 생성·상태·PNG

| 성향 | 기호 | 켜짐 후보 | 흐림 후보 |
|---|---|---|---|
| 앞장서기 | 북동쪽 단일 화살 | `behavior_charge.png` | `behavior_charge_dim.png` |
| 함께 걷기 | 단일 시계방향 원 화살표 | `behavior_patrol.png` | `behavior_patrol_dim.png` |
| 곁 지키기 | 단순 방패 | `behavior_hold.png` | `behavior_hold_dim.png` |

한 장의 2172×724 RGBA 마스터에 서로 분리된 3기호를 생성했다. 알파>16의 열 구간으로 3개를 식별하고 **사각 클립·동일 비율 스케일·중심 배치**만 적용해 개별 128×128로 내보냈다. 흐림은 같은 그림에 **알파0.40을 적용한 기술 제안**이며, 확정된 엔진/UI 비활성 상태 계약이 아니다. 새로 다른 모양을 그리거나 임의 회색 재질로 바꾸지 않았다.

6개 모두 128×128 RGBA, alpha>16 bbox `(9,8,119,120)`, 가로/세로 중심64, 테두리 알파0, 연결 성분1개를 확인했다. 켜짐의 최대 알파는254(거의 불투명), 흐림은102다. UI 가이드는 켜짐 최대 알파255를 별도로 요구하지 않으므로 값254를 오류/불투명255로 바꿔 표시하지 않는다.

### 5.3 검수·전달·미정

실제 저장된 6개 PNG를 기기 배율1에서 128/48/32px로 검토용 딥 퍼플 `#241B38` 위에 표시했다. **화살/원 화살표/방패를 육안 구분**했고, 흐림에서도 원기호는 유지된다. 검토 배경색은 실제 Unity UI 화면이나 신규 확정 컬러 토큰이 아니다.

| 검사 | 결과 |
|---|---|
| 128 RGBA·테두리 알파0·중심·여백·연결 성분·상태 알파 | 6개 정의한 검사 충족 |
| 32/48px 읽힘 | 이 검토 배경에서 육안 확인 |
| 기존 데이터·리소스·GUID·UI 코드 | 변경 없음 |
| 모든 UI 배경·실제 화면/입력 상태·Unity | 미실시 |
| 흐림 강도·상태 처리·실제 파일명/경로 채택 | 제안·미정 |
| DD·사람 확인·최종 채택 | 미실시 |

이미지·실제 프롬프트·변환값·검사 JSON·32/48px 비교·해시를 채팅 전달 묶음으로 보존한다. Git에는 PNG·임의 `.meta`·새 폴더를 추가하지 않았으며 외부 공유 URL은 미등록이다. 채택 시 기존 `Assets/_Project/Art/Icons/`의 파일명/경로 규칙을 확인하고 최종본만 LFS에 등록한다.

| 후보 파일 | SHA-256 |
|---|---|
| `behavior_charge.png` | `17fc000dbe3b6ac6aafb45674077eca1ee42af5092c3ac2a986ed844322ce8ca` |
| `behavior_charge_dim.png` | `0ef0089bf97d5aacfc09c624fd92534853c398942ee299d76275ebeb012e2cde` |
| `behavior_hold.png` | `8cfbced734e371200c07f324cfd0b1de44215170d82f9d8fefa93ef58df3a0d6` |
| `behavior_hold_dim.png` | `7783dd0e76c352db13fdd769a698abdf19e5cca98e8bbb097b7662ac6022a2ab` |
| `behavior_patrol.png` | `0cd54a7400e534ae3c1633456385cffd5354abbb28bad6c6976d2f791800c6b1` |
| `behavior_patrol_dim.png` | `6f7f9543565b31dfe09067355020988fd3358607382c11a16ce1b6cf9f75b0ba` |
