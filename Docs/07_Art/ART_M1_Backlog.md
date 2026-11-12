# ART_M1_Backlog — M1 신규 · 변경 아트 목록

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | DD |
| 최종 수정 | 2026-11-13 (#43 ART-01~04 · 06~07 후보 기록 · #59 UI 후보 기록 병합 · D-58 보름달 +30%) |
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
| ART-12 | **보름달 기믹 아이콘** — 효과는 "잔향 수급 +30%" (D-34 · D-58 — D-50 메모의 +20%는 정정됨) | 기믹 안내 | — | 128 | `UI/moon.png` 재사용 → 아이콘에 잔향 표식만 추가 | W3 | P2 |
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

## 6. ART-02 수선등 버튼 — 기존 리소스 재사용 검토 (2026-11-12)

사용자 “아트 작업 진행”에 따라 [전투 UI §2⑭](../03_UI_UX/UI_Battle.md)·[수선등 규칙 §18.2](../01_System/SYS_InGameBattle.md#182-수선등-밝히기-잔향-수급-투자--71-상세)·[밸런스 가정값 §2](../04_Balance/BAL_BattleKnobs.md#2-수선등-밝히기--가정)를 확인했다. 스타일과 원본 이미지를 유지하기 위해 **새 원화를 생성하지 않고 기존3개 원화를 재사용**했다. 구성·기술 파생본·상태 검토 단계이며 채택·Unity 연결은 별도다.

### 6.1 실물·분할·UI 색상 근거

| 원본 | 실물·역할 | SHA-256 |
|---|---|---|
| [Icons/lamp.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Icons/lamp.png) |256 RGBA, 등불 원화, 51,578bytes|`c73a558eeaaa75ff1020fc6f07399148c0768bd4531ac8e190ed91f2f7944b63`|
| [UI/button_gold_9s.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/button_gold_9s.png) |192 RGBA, 금색 버튼 원화, 22,883bytes|`6ace1af174be5598e903b5b25c840cf1fc17fac65feec44c5021dea1dc2c9e0f`|
| [UI/ui_pip_button.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/ui_pip_button.png) |128 RGBA, 단추형 점 원화, 22,200bytes|`8aff68892c636ff221b860898d7e0f3c53bb050038a58c9c18658bf2dcf7b647`|

LFS OID·크기·이미지 육안을 대조했다. 기존 파일과 `.meta`·GUID는 그대로다. [기존 버튼 .meta](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/button_gold_9s.png.meta)의 **Sprite Border 좌/하/우/상48px**를 사용한다. 기존192 버튼의 전달용 복사본은 바이트까지 동일하다.

[UITheme.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/UITheme.cs)의 금색 칠 위 글자 `Ink #3D2B5E`, 점 바탕 `Panel #261D4C`, 비용 배경 `CostPillBack rgba(23,18,46,.72)`, 부족 비용 `CostShortText #FFC2CC`를 참고했다. 이 검토에서 새로운 확정 컬러 토큰이나 게임 데이터를 추가하지 않는다.

### 6.2 전달 구성과 동적 필드 분리

| 전달 파일 | 크기·역할 | 처리 |
|---|---|---|
| `repair_light_button_9s.png` |192×192 RGBA, 버튼 바탕|기존 파일의 정확한 복사,48px 분할 유지|
| `repair_light_icon.png` |128×128 RGBA, 등불|기존256 원화의 동일 비율 크기/배치 파생본|
| `repair_light_icon_dim.png` |128×128 RGBA, 흐림 등불|같은 원화 알파0.40 제안|
| `repair_light_pip_on.png` |32×32 RGBA, 켜진 점|기존 단추형 pip의 동일 비율 파생본|
| `repair_light_pip_off.png` |32×32 RGBA, 빈 점|같은 pip 알파0.25 제안|

파생본은 사각 클립·동일 비율 스케일·중심 배치/알파만 적용했고 새 그림·색칠·비균등 변형을 하지 않았다. **5개 구성 PNG에는 레벨·비용·수급·MAX 글자를 굽지 않았다.** 실제 Unity는 기존 원본을 그대로 참조해 표시 크기만 조절할 수도 있으며, 전달 파생본이 중복 에셋 등록을 요구하지 않는다. 최종 경로·그림/코드 알파 중 선택은 채택 후 정한다.

레벨/비용/수급 문구와 켜진 점 개수는 별도 동적 레이어다. `review_state_*.png`와 HTML/비교 화면에는 설명용 문구가 보이지만 **실제 게임용 버튼 텍스처가 아니다.** 비용·수급은 BAL §2의 **가정값을 사용한 검토 예시**이며 새 수치 확정·게임 로직 연결이 아니다.

### 6.3 상태 검토

| 검토 상태 | 표시 | 점 개수·입력 의미 |
|---|---|---|
| L1 |현재L1·다음비용10·예시+0.4/초|1/5, 투자 가능 예시|
| L2 |현재L2·다음비용20·예시+0.4/초|2/5, 투자 가능 예시|
| L3 |현재L3·다음비용35·예시+0.5/초|3/5, 투자 가능 예시|
| L4 |현재L4·다음비용55·예시+0.7/초|4/5, 투자 가능 예시|
| L5 / MAX |현재L5·MAX·최대 레벨|5/5, 투자 불가|
| 잔향 부족 |L2 예시·비용20을 어두운 배경 위 분홍색·흐림 등불|현재2/5 유지, 투자 불가|

**L5와MAX는 같은 최대 레벨**이며 추가6단계로 만들지 않는다. 리셋/자동 투자 규칙은 시스템 정본을 따르고 아트 검토에서 바꾸지 않는다. 부족 상태에서 점을 줄이지 않고 현재 레벨 정보를 유지했다. 흐림 강도는 기존 엔진 계약이 아닌 제안이다.

### 6.4 기술·육안 검토 결과와 남은 것

- 등불2개·pip2개는 지정128/32 RGBA·중심 오차≤1px·투명 테두리·연결 성분1개를 확인했다. 최대 알파는 등불255/102, pip253/64다. 바탕은 원본 바이트/해시 동일성을 확인했다.
- 384×152의6상태와 **256×128·384×152·512×160** 크기를 기기 배율1에서 표시했다. CSS9-slice의48px 분할과 글자 캔버스 넘침/제목-레벨 겹침 없음, 점5개·켜진 수와L 대응, MAX/부족의 입력 불가 표시를 확인했다. 이는 검토 DOM과 렌더러의 확인이며 Unity 구현 통과가 아니다.
- 실제 등불 PNG의128/48/32px도 육안 확인했다. 장식·작은 별은 줄어들지만 등불 형태와 내부 불빛이 읽힌다. 버튼 모서리·등불은 크기 변경 때 비균등으로 늘리지 않았다.
- PNG·기존 원화·변환/검사JSON·상태 예시PNG6개·HTML·비교 화면·스크립트·실기 확인표·해시를 채팅 묶음으로 전달한다. 외부 공유 URL은 미등록이다.
- **남음:** 실제 Unity Sliced Image/Canvas/폰트/입력 상태·게임 데이터 연결·기기별 글자/잘림·실제 화면 크기·DD/사람 확인·최종 채택. 새PNG·임의.meta·코드·설정·데이터·기존 에셋 변경은 Git에 반영하지 않았다.

| 구성 파일 | SHA-256 |
|---|---|
| `repair_light_button_9s.png` | `6ace1af174be5598e903b5b25c840cf1fc17fac65feec44c5021dea1dc2c9e0f` |
| `repair_light_icon.png` | `924cd3215d728735ee4339fa2ae208f134f419e7689fcd572c538e014b8a2b12` |
| `repair_light_icon_dim.png` | `005a774ff54d99293572bccf23954daf4cccd61e89a6b99535b5cd87028ab592` |
| `repair_light_pip_on.png` | `4224258fdea14a90c6082b47f6278eb34986865ecc371eddbb16c6c9bd248a31` |
| `repair_light_pip_off.png` | `2524bfbb8237fa1ce2558e28a27a8826ea0f589c3d9ba7b004ce622ac0a15a3a` |

## 7. ART-03 수선점 창문 불빛 — 원본 정렬 후보 (2026-11-12)

[스타일 가이드](./ART_StyleGuide.md)·[전투 규격](./ART_BattleSpriteSpec.md)·기존 목록의 L1~L5 요구를 따랐다. **건물 원화를 다시 그리지 않고** 기존5개 외벽 창문의 유리만 단계별로 다룬다. 원본에는 이미5창 모두 불이 켜져 있어, 꺼진 창의 유리만 어두운 인디고로 가리는 **Normal Alpha 오버레이 방식**을 제안한다. 발광 Additive 텍스처가 아니다.

### 7.1 원본·범위·점등 순서

직접 확인한 기준: [shop.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/shop.png) · [shop_crack_1.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/shop_crack_1.png) · [shop_crack_2.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/shop_crack_2.png) · [shop_crack_3.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/shop_crack_3.png). 기존4개 파일은 해시까지 보존했다. [ShopView.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/Battle/ShopView.cs)의 건물/손상 겹침 구조도 확인했다. 기존 전체 건물 밝기 효과를 창문 단계 연결 완료로 간주하지 않는다.

| 순서 제안 | 위치 | 측정한 유리 픽셀 수 | 켜진 유리 평균 Y |
|---|---|---|---|
| W1 | 좌하 | 378 | 220.24 |
| W2 | 우하 | 208 | 210.50 |
| W3 | 좌상 | 720 | 225.05 |
| W4 | 우상 | 165 | 214.98 |
| W5 | 상단 | 1164 | 222.30 |

외벽5창을 대상으로 **좌하→우하→좌상→우상→상단** 순서를 제안한다. 문 유리와 기존 등불은 환경광으로 유지한다. 외벽/문 구분과 순서는 검토 제안이며 기획 정본의 확정 순서가 아니다. Y는 선정 유리의 sRGB Rec.709 가중값(0~255), 선형 광도·전장 전체 밝기 측정이 아니다.

### 7.2 생성·정렬·전달

- 기존 shop 이미지를 직접 참조해 **어두운 유리 재질 한 장만** AI 생성했다. 건물·창틀·창살·단추·꽃·그림자·실루엣은 새로 생성하지 않았다.
- 원본에서 수동 지정한5개 유리 내부 ROI와 따뜻한 유리색 조건을 교차하고, 각4칸 창의 큰 연결 성분4개를 남겨 픽셀 좌표를 측정했다. 일반 의미 분할이나 모든 유리 영역의 완벽한 인식으로 주장하지 않는다. 선정 좌표에만 새 재질을 SVG 클립하고 브라우저로 PNG를 내보냈다.
- 후보 `shop_light_l1.png`~`shop_light_l5.png`는 **768×768 RGBA 전체 캔버스**다. 건물과 같은 캔버스/중앙 피벗·배치를 유지하며, 작은 유리 내용 기준으로 자르거나 재중앙화하지 않는다. 건물 발 기준 y722·중심384는 원본을 따른다.
- L1은W1, L2는W1~2, L3는W1~3, L4는W1~4, L5는W1~5 점등이다. 각 단계는 겹쳐 누적하지 않고 **해당 레벨 레이어1장**만 선택한다.
- **L5는 알파가 전부0인 의도된 빈 오버레이**다. 원본5창 점등을 그대로 드러내므로 누락/불량 파일이 아니다. 실제 연결에서는 L5에 오버레이를 숨기는 동등한 처리도 가능하다.
- 검토 합성 순서는 **원본 건물 → 불빛 단계 오버레이 → 기존 금 간 레이어**다. 실제 Unity에서는 동일 부모/변환·정렬·필터·스케일을 확인해야 하며, 임의 `.meta`/GUID를 만들지 않았다.

### 7.3 검사·제한·전달 묶음

| 검사 | 결과 |
|---|---|
| 후보5개 규격·정렬·투명 테두리 | 768 RGBA, 원본 좌표 그대로 |
| 각 단계의 비투명 픽셀 수 | L1 2257 · L2 2049 · L3 1329 · L4 1164 · L5 0 |
| 원본과 같은 브라우저 경로로 합성 후 비교 | 해당 단계의 선정 유리 밖 RGBA 완전 동일 |
| 인접 단계 변화 | 새로 켜지는 창의 선정 유리에만 변화 |
| L5 합성 | 원본 브라우저 합성과 전체 픽셀 동일 |
| 꺼진 유리 평균 Y | 53.43~56.59, 켜진 유리210.50~225.05 |
| 실제 PNG 검토 | 205px 캔버스5단계, 손상 없음/1/2/3 ×5단계20칸, 유리 확대 육안 확인 |
| Unity·투자 연동·애니메이션·최종 채택 | 미실시 |

205px는 전장 크기를 참고한 **브라우저 정적 합성**이며 실제 게임 캡처가 아니다. 작은 상부 우측 창은 단계 차이가 작아 실제 기기·전장/손상/흔들림 위에서 추가 검토한다. 창 영역 밖의 기존 따뜻한 반사광은 보존했으며 완전한 소등 재조명 작업은 하지 않았다. 실제 단계 전환·리셋·기존 건물 효과와의 상호작용·필터링 경계·스프라이트 피벗/패킹·색 공간은 Unity 연결 시 확인한다.

5개 후보·원본4개·생성 재질/프롬프트·창 좌표 JSON·검사 JSON·전체/확대/손상 비교·내보내기 스크립트·실기 확인표·해시를 채팅 묶음으로 전달한다. 외부 공유 URL은 미등록이다. 기존 문서만 보완했고 새 PNG/폴더/코드/데이터/설정/기존 에셋을 Git에 추가·교체하지 않았다. DD/사람 확인·최종 경로/파일명·채택은 별도이며 ART-03 게임 적용 완료로 표시하지 않는다.

| 후보 파일 | SHA-256 |
|---|---|
| `shop_light_l1.png` | `57df5fd5e541ac40cb0e7b8ad7341f691ba3102bd3fa70a5db8ee4ff126dfd0e` |
| `shop_light_l2.png` | `fafaf5fcfa89609ef7f31d8a291c4830f94ea1327be00f275420ddfb45410ee4` |
| `shop_light_l3.png` | `d1d30f6087ff48b0e9f5a99cef3c21fc1739cd0ce35d3ef4a04c58198c0cd5bd` |
| `shop_light_l4.png` | `f63884464947b898549575b875ea0bcc4af22912096da568e7dc12a90d6a73d6` |
| `shop_light_l5.png` | `60efeded078b9499b82367a9c5cc1a43b3bfec251cb3d8c5e49a429cc335277d` |

## 8. ART-04 8방위 링 세트 — 재사용·마커 후보 (2026-11-12)

[편성 화면](../03_UI_UX/UI_Formation.md) §2④~⑥·[스타일 가이드](./ART_StyleGuide.md) §9·기존 목록을 확인했다. 링/빈 방위/위험/압박 **4개 계열**이며 빈 방위의 보통/빨강2상태를 포함해 전달 PNG는5개다. 기존 링에 이미8개 별 눈금이 있어 **링과 받침은 재사용**하고 정보 마커4개만 생성했다. 게임 적용/최종 채택은 별도다.

### 8.1 실물·재사용 근거

| 기존 리소스 | 확인·용도 | SHA-256 |
|---|---|---|
| [Battle/field_tall.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/field_tall.png) | 기존 바닥 정본·해시 보존, 새로 그리지 않음 | `9b00bf54464a05f46bc1e3d1d4431ea987725613d51d70c3f6fee92baf214f28` |
| [UI/ui_ring_stitch.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/ui_ring_stitch.png) | 512 RGBA, 봉제 원형 링·별8개. 링 후보의 직접 재사용 원본 | `8f7c1ca77550a1d7c6af7be2feff6922daedaf283c844ceb6e8f1f33cb3cd8f9` |
| [FX/ring_glow.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/FX/ring_glow.png) | 512 RGBA, 기존 흰 발광/알파 리소스. 검토하고 새로 만들거나 대체하지 않음 | `88e6dd58dc5c17427a2a620811b4b3d04a20c2a9f34a4b8716c2a32b7fe4febf` |
| [Battle/guardian_pad.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/guardian_pad.png) | 256 RGBA, 찬 슬롯 예시용 원본 그대로 | `38f8c16967a53f3fad74bfb5fd125eb1a09d34d257f653ee5b10ce44e811d59b` |
| [Battle/guardian_pad_empty.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/guardian_pad_empty.png) | 256 RGBA, 빈 슬롯 예시용 원본 그대로 | `72fe665faf7e3ddaaa9a24de5572b91a59a638d8ee976cc3b3739cc82bb4c73a` |

LFS 실물/해시·알파·육안을 대조했다. 기존 파일/`.meta`/GUID는 보존한다. [DirectionRingView.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/Battle/DirectionRingView.cs)·[FormationSlotView.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/Formation/FormationSlotView.cs)의 현재5방향/72°·위협 표시·부채꼴/원근 레이어와, 승인된 UI의8방위 요구를 구분했다. 이 아트는 기존 코드를8방향으로 바꾸지 않는다.

### 8.2 PNG와 생성 범위

| 전달 파일 | 규격 | 의미·처리 |
|---|---|---|
| `formation_ring_8.png` |1024×1024 RGBA|기존512 봉제 링의 동일 비율2배 파생. 별/봉제/색/재질 그대로|
| `formation_uncovered.png` |128×128 RGBA|미커버 방위: 크림/라벤더 사선3개|
| `formation_uncovered_danger.png` |128×128 RGBA|미커버이면서 위험: 로즈 레드 사선3개|
| `formation_danger.png` |128×128 RGBA|위험 방위: 위를 향한 삼각형▲1개|
| `formation_pressure.png` |128×128 RGBA|지속 압박: 수평 줄무늬3개|

신규 마커는 기존 봉제 링·빈 받침을 직접 이미지 참조로 사용했다. 둥근 형태·딥 플럼 외곽·크림/라벤더·절제된 금색 하이라이트·부드러운 셀 채색을 유지한다. 보통/빨강 사선은 같은3줄 구조의 생성 상태 쌍이며 **픽셀까지 동일한 모양의 단순 색 변경은 아니다.** 2172×724 RGBA 마스터를 사각 클립·동일 비율 축소·중앙 배치만 하여 분리했다. 새 문양·배지 테·문자·부채꼴 원화를 더하지 않았다.

링은 원본512를 그대로 참조해 표시해도 된다. 1024 파생은 세부를 새로 추가한 고해상도 원화가 아니며 중복 에셋 등록을 요구하지 않는다. 원본8별은 장식이고 일부 간격이 수학적45°와 다르다. **정확한8방위 스냅/위험 좌표는 별 위치에서 추출하지 않고 N부터 시계방향0/45/90/135/180/225/270/315° 앵커를 사용**한다. 앵커/방위 글자/슬롯은 텍스처에 굽지 않는다.

### 8.3 표시 의미·조합 제안

| 입력 | 표시 |
|---|---|
| 미커버이고 위험 아님 | 보통 사선 |
| 미커버이면서 위험 | 빨간 사선 + 별도▲ |
| 커버되어 있지만 위험 | ▲ 유지, 사선 없음 |
| 지속 압박 방위 | 수평3줄을 별도 유지 |
| 슬롯이 비었지만 다른 지킴이 요격으로 커버 | 빈 받침 유지, 사선 없음 |

**빈 슬롯과 미커버 방위를 동일시하지 않는다.** 링 슬롯은8개/배치 최대5명이고, 빗금은 요격 커버 계산 결과를 입력받는다. 위험▲와 압박은 커버 여부와 독립이다. 보통/빨강 사선만 서로 교체하며▲/압박은 같은 방위에서도 유지한다. 빨강만으로 정보를 알리지 않고 삼각형/사선/수평줄 형태를 함께 사용한다.

이 후보의 사선은 **방위별 기호형 빗금**이다. 미커버 영역 전체를 덮는 가변 부채꼴 텍스처가 아니다. ART-05 요격 범위/가변 커버 영역은 기존 지침대로 코드/메시/셰이더로 만들며, 실제 영역을 채우는 패턴이 필요하면 그 커버 마스크에 별도 클립/타일 처리를 연결한다. 이 기호 후보만으로 가변 범위 구현 완료를 주장하지 않는다.

검토 화면은 링/받침 뒤, 사선은 반지름123, ▲210, 압박88, 방위 글자234에 두는 **512px 캔버스의 배치 제안**이다. 마커를 방향마다 돌리지 않아 ▲는 위를 향하고 사선/압박은 각각 대각/수평으로 읽힌다. 실제 위치·크기·회전/알파·가림 순서·동시 표시는 구현 검토 후 정한다. 원형 기획과 기존 원근 투영 중 선택은 아트 후보에서 확정하지 않았다.

### 8.4 검토·제한·전달

- PNG5개 지정 크기/RGBA·투명 테두리·가로 중심을 확인했다. 마커4개 중심64/64, 주요 연결 성분은3/3/1/3개다. 최대 알파253/253/254/254이며255로 표시하지 않는다. 링의 알파>16 내용 중심은512/510, 중앙은 투명하다.
- 실제 마커 PNG를 **128/48/32px**로 딥 퍼플/크림 위에 표시해 사선·삼각형·수평3줄의 육안 구분을 확인했다. 모든 실제 배경/기기에서의 보장을 뜻하지 않는다.
- **0명/미커버8방위, 5명/빈3칸/미커버2방위, 5명/빈3칸/미커버0방위, NW 미커버+위험+압박 동시**4상태를512px 브라우저에서 검토했다.8슬롯·배치≤5·각 방위/마커 입력 대응과 미커버/빈칸 독립을 검사했다. 배치/커버/스테이지 값은 읽힘용 가상 예시이며 실제 스킬 계산 결과가 아니다.
- 384px 축소 예시에서는32px 마커가24px로 줄어든다. 실제 UI는 마커 크기를 별도 유지하거나 목표 기기에서 다시 검토해야 한다. 이 예시를32px 게임 검증 통과로 주장하지 않는다.
- **남음:** 실제 Unity8방향 전환·스냅/드래그/입력·게임 커버/위험/압박 데이터 연결·실제 미커버 영역 표시·크기/색 공간/투영·레이캐스트/마스크/가림·동시표시/애니메이션·DD/사람 확인·최종 채택.

PNG·기존 원본/해시·마스터/프롬프트·분리/검사JSON·4상태/축소/배경 비교·스크립트·실기 확인표를 채팅 전달 묶음으로 보존한다. 외부 공유 URL은 미등록이다. Git에는 기존 문서 기록만 보완하고 PNG/새 폴더/임의.meta/코드/데이터/설정/기존 에셋 변경은 반영하지 않았다. 채택 후 기존 폴더/명명 규칙으로 최종본만 LFS 등록한다.

| 전달 파일 | SHA-256 |
|---|---|
| `formation_uncovered.png` | `93e15b80e5158706c7dfafb3cdb22e63dc82e72e02f5b125cd25b4d0363ce512` |
| `formation_uncovered_danger.png` | `f10f69d81dfe66919d61c44149bb2203407b2ceeaf9fbb4adb3321ff249c23e0` |
| `formation_danger.png` | `15f61d014fc70f79a07e9140681f6ebd716b2b3db27a1be4d1abacf5e9a3c841` |
| `formation_pressure.png` | `0ce9b89e233d408ff6c9bb16401c9840d91ed3fcf3daff4f73b8f6b6c52fee9b` |
| `formation_ring_8.png` | `dddf31161d95fab7a25af9fade26cf66fcab750607457a87ff2b6483f756271f` |

## 9. ART-06 웨이브 예고 마커 — 기존 원화 재사용 후보 (2026-11-12)

[전투 UI](../03_UI_UX/UI_Battle.md) §2⑩의 **다음 웨이브 스폰 섹터·링 가장자리·3초 전 표시**, [기존 목록](#1-목록)의 예고 깜빡임/지속 압박 고정 상태와 [스타일 가이드](./ART_StyleGuide.md) §9를 확인했다. 기존 화살촉이 목적에 맞아 **새 AI 원화는 생성하지 않고** 봉제 화살촉·링 효과를 재사용했다. 압박은 [ART-04 후보](#8-art-04-8방위-링-세트--재사용마커-후보-2026-11-12) 수평3줄과 같은 PNG를 공유한다. 두 작업 모두 최종 채택/게임 적용은 미실시다.

### 9.1 원본·선택 근거

| 기존 리소스 | 확인·역할 | SHA-256 |
|---|---|---|
| [UI/ui_chevron.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/ui_chevron.png) | 128×96 RGBA, 봉제 단일 화살촉. 예고 화살표에 재사용 | `bec73bd648d07d360f0ad42bc792bd8e46334d9c88b52e2ea7ea9cdd9917a1b3` |
| [VFX/motif_downarrow.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/VFX/motif_downarrow.png) | 128 RGBA, 두꺼운 판형 아래화살표. 검토 후 사용하지 않음 | `38d75558632099e292d07308db9d46029d2c98e27965fb298078b4f4410de5b8` |
| [FX/ring_glow.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/FX/ring_glow.png) | 512 RGBA, 흰색/알파 링. 따뜻한 Lamp 틴트 검토 기준/실제 합성 | `88e6dd58dc5c17427a2a620811b4b3d04a20c2a9f34a4b8716c2a32b7fe4febf` |
| [UI/ui_ring_stitch.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/ui_ring_stitch.png) | 512 RGBA, 기존 방위 링 실물/해시 보존. 이번 예고 화면에서는 ring_glow 사용 | `8f7c1ca77550a1d7c6af7be2feff6922daedaf283c844ceb6e8f1f33cb3cd8f9` |
| [Battle/field_tall.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/field_tall.png) | 1080×2048 RGB, 기존 바닥. 정적 크롭 위32/48px 비교 | `9b00bf54464a05f46bc1e3d1d4431ea987725613d51d70c3f6fee92baf214f28` |
| [Battle/shop.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Battle/shop.png) | 768 RGBA, 원본 그대로 합성해 안쪽 방향 확인 | `f6972deb9707550402ca159f32fc3ff793c837d06e247f5402e7246d44bd2078` |

기존6개 실물/해시를 보존했다. [DangerArrowView.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/Battle/DangerArrowView.cs)는 현재 **실시간 위협도**로 크기/행진/색/가림을 조절하는 코드이며 다음 웨이브 예고 타이머가 아니다. 이번 검토는 그 코드에 새 예고를 연결하거나 기존 위협도를 다음 웨이브 데이터로 대체하지 않았다. [DirectionRingView.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/Battle/DirectionRingView.cs)의 Lamp/GoldAccent 링 톤을 참고했고 새 확정 색 토큰을 추가하지 않는다.

### 9.2 전달 PNG·방향·색

| 전달 파일 | 규격·처리 |
|---|---|
| `wave_preview_arrow.png` |128×128 RGBA, ui_chevron 사각 클립·−90° 강체 회전·동일 비율 확대·중앙 배치. 기본 방향 위쪽/로컬+y|
| `wave_pressure_marker.png` |128×128 RGBA, ART-04 `formation_pressure.png`의 **바이트 동일 복사본**. 새 원화/새 디자인 아님|

화살표의 중성 흰색/봉제/회색 외곽/작은 원본 별을 유지했다. 검토 합성에서만 `UITheme.Lamp #FFD98A`를 곱하고, PNG에 틴트·점멸·문자·카운트다운을 굽지 않았다. 압박 PNG도 전체 알파 상태를 보존하고 검토에서만0.45 알파로 옅게 표시한다. 채택 시 ART-04와 같은 리소스를 참조해도 되며 전달 파일명 때문에 중복 에셋을 등록하지 않는다.

방위θ는 N부터 시계방향0/45/90/135/180/225/270/315°다. 브라우저의 위쪽 기준 화살표에 `θ+180°`를 적용해 **수선점 안쪽**을 향하게 했다.8개 방향 벡터와 중심 벡터의 내적1을 확인했다. Unity 좌표/회전 부호와는 별도로 실제 연결 시 확인한다. 안쪽 방향은 기존 DangerArrowView와 일관된 **검토 제안**이고, 승인 UI는 화살표의 구체적 방향/알파/주기를 별도로 고정하지 않았다.

### 9.3 시간·상태 제안

| 상태 | 예고 화살표 | 지속 압박 |
|---|---|---|
| 다음 웨이브 예고 중 | 해당 스폰 방향에 표시, 검토 알파0.35~1.0 | 지정1방위 고정0.45 |
| 예고 종료(3초 도달) | 숨김 | 유지 |
| 깜빡임 줄이기 | 예고 중 알파1 고정, 종료 때 숨김 | 고정 유지 |
| 예고/압박이 같은 방위 | 링 가장자리 화살촉 | 안쪽 반지름으로 분리해 수평3줄 유지 |

**3초는 승인 UI 근거**다. **주기1초·사인형 알파0.35~1·압박0.45·Lamp 틴트·안쪽 방향·반지름 분리**는 확정 게임 계약이 아닌 검토 제안이다. 검토512px 캔버스에서 화살표48px/반지름189, 압박40px/반지름139로 두었다. 실제 전장/HUD/안내줄·다른 VFX·원근/원형 결정에 따라 조정한다. 예고는 스폰 예정 섹터, ART-04 ▲는 스테이지 위험 방위, 현재 DangerArrowView는 실시간 위협도로 의미가 다르다. 하나의 불리언으로 합치지 않는다.

이 후보는 **각1장의 PNG와 코드 알파/회전으로 표시**한다. 점멸 프레임 시트를 만들지 않는다. 검토 HTML에는3초 재생·시간 슬라이더·깜빡임 줄이기 예시를 포함했다. 게임 일시정지/배속/스킵/앱 복귀/연속 웨이브에 대한 타이머 구현은 하지 않았다.

### 9.4 검토 결과·제한

- PNG2개 모두128 RGBA·테두리 알파0·내용 중심64/64. 화살표 alpha>16 bbox `(10,24,118,104)`, 주요 연결 성분1개·최대 알파255. 압박 bbox `(8,21,120,107)`, 주요 성분3개·최대 알파254. 원본/압박 공유 해시를 확인했다.
- 512px **시작/점멸 저점/종료/깜빡임 줄이기/8방위/같은 방위 중첩**6상태를 육안/DOM으로 검토했다.8방위는 가상 스폰 예시이며 실제 스테이지 값을 만들지 않았다.
- 32/48/128px를 딥 퍼플/크림 위에 표시하고, 원본 바닥의 정적 크롭 위에서32/48px Lamp 화살표와0.45 압박 줄무늬를 구분했다. 크림 등 밝은 배경에서는 화살표 대비가 약하므로 모든 배경/실제 기기 가독성 통과로 주장하지 않는다.
- 시간18샘플과 실제 브라우저3초 재생2회(일반/줄이기)를 검사했다. 예고 중 일반 모드는 알파 저점<0.4·고점>0.95, 줄이기는1 고정이며, 둘 다3초 끝에서 화살표 숨김/압박0.45 유지. 이는 **검토 HTML의 동작 검증**이며 Unity/게임 시간/실제 스폰 연결의 검증이 아니다.
- **남음:** 실제 다음 웨이브/지속 압박 데이터·8방위 기하·3초 예고/정지/배속/스킵/앱 복귀·기존 실시간 위협/가이드 회피와 공존·실제 밝은 영역/다중 VFX/마커 크기·접근성/기기·Unity 임포트/알파/색 공간·DD/사람 확인·최종 채택.

새 PNG/.meta/폴더/코드/데이터/설정/기존 이미지를 Git에 추가·교체하지 않았다. 원본6개·후보2개·재사용 근거·변환/검사JSON·6상태/바닥 비교·재생HTML/스크립트·실기 확인표·해시를 채팅 묶음으로 전달한다. 새 이미지 생성 프롬프트는 **미사용**이며 외부 공유 URL은 미등록이다. 기존 문서 기록만 보완하고 최종 채택 시 기존 폴더/명명/LFS 규칙을 따른다.

| 후보 파일 | SHA-256 |
|---|---|
| `wave_preview_arrow.png` | `e20602a259719ccdb707b26a2aa12ff07e1e580a801412ead86037a9ea65cba4` |
| `wave_pressure_marker.png` | `0ce9b89e233d408ff6c9bb16401c9840d91ed3fcf3daff4f73b8f6b6c52fee9b` |

## 10. ART-07 상단 HUD 소품 — 시계·웨이브·남은 적 후보 (2026-11-12)

[전투 UI](../03_UI_UX/UI_Battle.md) §2②③④·[전투 시스템](../01_System/SYS_InGameBattle.md) §18.3·[스타일 가이드](./ART_StyleGuide.md) §9를 확인했다. **시계와 보스 해골2개만 생성**하고 일반 웨이브 단추·HUD 패널·남은 적 배지 바탕은 기존 원화를 재사용한다. 시간/웨이브/HP/적 수는 별도 동적 텍스트/표시이며 기획 수치·게임 데이터를 확정하지 않는다.

### 10.1 기존 실물과 생성 참조

| 기존 리소스 | 확인·용도 | SHA-256 |
|---|---|---|
| [UI/ui_pip_button.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/ui_pip_button.png) | 128 RGBA, 일반 웨이브 칸에 그대로 재사용 | `8aff68892c636ff221b860898d7e0f3c53bb050038a58c9c18658bf2dcf7b647` |
| [Icons/guardian.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Icons/guardian.png) | 256 RGBA, 생성 시 보라/골드·외곽·셀 채색 직접 참조 | `ec15d7ca2ff862c47c76e83132d851795eca365d79c5af86229a8994993a5311` |
| [Icons/echo.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Icons/echo.png) | 256 RGBA, 생성 시 동화풍 재질/크림 하이라이트 직접 참조 | `c4ae714ffa2cae08d3d96277f75980f1fee2af567a4f9f24772563c35ae72444` |
| [UI/panel_night_9s.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/panel_night_9s.png) | 192×192 RGBA, HUD 바탕 원본 그대로 | `bb873455d9e47b6530301ad1439cdeae7378ce1250839ddc88083dd6d603d6a1` |
| [UI/map_node_boss.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/map_node_boss.png) | 512 RGBA, 해골 없는 테두리임을 육안 확인. 이번 보스 칸에는 미사용 | `7db22ed39d75456cc5a1eaf09b03c0f49f1f86c50782fb16263e2dbb4434805d` |
| [UI/ui_pill_night_9s.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/ui_pill_night_9s.png) | 192×96 RGBA, 남은 적 숫자 배지 바탕 원본 그대로 | `5d8cc0ddcc9191849f939394a534f64a6730cfcf97a0382df6f3d1728eb49919` |

원본6개 실물/해시를 보존했다. 이름이 clock/skull인 기존 이미지 검색도 확인했다. [BattleHud.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/Battle/BattleHud.cs)의 현재 처치/보스 진행과 신규 UI의 제한시간/Wave/최후의 쇄도 표시를 구분하며 기존 HUD 코드를 바꾸지 않았다. [UITheme.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Scripts/UI/UITheme.cs)의 어두운 바탕 글자 `Text #FBF6EE`, 경고 글자 `Negative #FF8A9E`, 현재칸 선 `GoldAccent #E8BE6A`를 검토에 사용했다.

### 10.2 전달 PNG와 동적 정보 분리

| 후보 파일 | 크기·처리 |
|---|---|
| `hud_clock.png` |128×128 RGBA, 보라 케이스/크림 시계판/굵은 바늘2개의 정적 시간 기호|
| `hud_wave_boss.png` |128×128 RGBA, 크림색 둥근 해골·보라 눈구멍. 일반 단추와 형태로 구별|
| `hud_wave_normal.png` |128×128 RGBA, 기존 단추 원본의 바이트 동일 복사|
| `hud_panel_9s.png` |192×192 RGBA, 기존 패널 원본의 바이트 동일 복사|
| `hud_remaining_badge.png` |192×96 RGBA, 기존 pill 원본의 바이트 동일 복사.2:1 전체 그림 표시|

시계/해골은 guardian/echo 실물을 직접 참조해 같은2172×724 RGBA 마스터에 생성했다. 사각 클립·동일 비율 축소·중앙 배치만 적용했다. 작은 금속 장식/입체 렌더를 늘리지 않고 큰 시계 바늘·눈구멍·두개골 실루엣을 우선한다. 새 글자/숫자/카운트다운/적 수를 PNG에 굽지 않았다. 시계 바늘은 정적 상징이며 실시간 잔여 시간이나 진행률을 가리키지 않는다.

웨이브 `n/N`·N개 칸·마지막 칸의 해골·현재칸 밑줄·미도달 알파·보스 HP바/수치·남은 적 숫자는 별도 UI 레이어다. HP바는 검토용 코드 도형이며 새 바 텍스처를 납품하지 않았다. 일반/보스 칸은 같은32px로 표시하되 PNG 실루엣으로 구분한다. 미도달 알파0.32·현재칸 밑줄·시간을 초 단위 올림 표시하는 방식은 검토 제안이다.

### 10.3 기존 배지 분할 문제와 보존 방식

[패널 .meta](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/panel_night_9s.png.meta)·[배지 .meta](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/UI/ui_pill_night_9s.png.meta)의 Sprite Border는 모두 좌/하/우/상48px다. 패널192×192은 중앙96×96을 유지해 검토에서도48px 소스 분할을 썼다(CSS 표시 테두리24px, Unity 최종 크기 계약 아님).

배지는 **높이96 = 위48+아래48**로 중앙 높이가0이다. 브라우저에서 이 값으로 높이를 늘렸을 때 중앙이 비어 보이는 문제를 확인했다. 기존 그림/.meta를 수정하지 않고, 이번 후보에서는 **Simple 전체 그림을112×56, 원래2:1 비율로 표시하고 숫자만 겹친다.** ‘남은 적’ 문구는 그 옆 별도 텍스트다. 임의9-slice 재설정/늘어난 배지 PNG/.meta를 만들지 않았다. 기존 `_9s` 원본의 최종 엔진 사용 방식을 이 문서에서 바꾸지 않으며, 이번 검토 표시 방법만 제안한다.

### 10.4 상태·검토 결과

| 상태 | 검토 표시 |
|---|---|
| 보통 | 시계/시간·Wave n/N·일반 단추/마지막 해골 |
| 잔여20% 이하 | 시간 글자 빨강/밑줄,1초 주기 점멸. 시계/나머지HUD 유지 |
| 보호 튜토리얼 | 시계/시간 숨김,웨이브 유지 |
| 보스 등장 | 보스 HP 영역 표시 |
| 보스 처치 후 잔존 적 있음 | 보스 HP 대신 ‘남은 적 N’ 배지 |
| 깜빡임 줄이기 | 경고색/밑줄 유지,알파1 고정 제안 |

**20% 이하·1초 점멸·보호 튜토리얼(1-1~1-3)·HP→쇄도 카운터 전환은 승인 UI/시스템 근거**다. 경고 최저 알파0.55·사인형 변화·밑줄·배지 위치·미도달0.32·현재칸 표시·줄바꿈은 검토 제안이다. 숫자/문구 예시는 실제 스테이지/밸런스 값이 아니다.

- 생성128 PNG2개는 RGBA·투명 테두리·중심64/64·주요 연결 성분1개·최대 알파254를 확인했다. 기존 복사3개는 원본과 바이트/해시까지 같다. 배경 패널/배지의 원래 크기/알파는 보존하며 아이콘용 투명 테두리 규칙을 잘못 적용하지 않는다.
- 실제 시계/일반단추/해골 PNG를 **128/64/48/32px,딥 퍼플/크림** 위에서 육안 구분했다. 실제 모든 기기/배경에서의 보장은 아니다.
- 720px6상태(보통/20%/점멸저점/쇄도/튜토리얼/줄이기),320px3배치(4칸/12칸 줄바꿈/8칸+9999)를 검사했다. 텍스트·슬롯 넘침/문구 간 겹침 없음,마지막 해골1칸/현재칸1개·시간 숨김·보스HP/남은적 배타 표시를 확인했다.12칸/9999는 스트레스 예시이며 실제 상한이 아니다.
- 20.1%→경고아님,20%/19.9%→경고,튜토리얼 낮은 시간→시간 경고숨김의4경계 입력을 검사했다. 초 단위 올림 때문에20.1%/20% 표시 숫자가 다르게 보이는 것은 검토 포맷 제안이다.
- 위상10샘플과 실제 브라우저2초 재생2회에서 일반 경고 알파0.55~1/줄이기1 고정·시간00:48 유지 상태를 확인했다. **알파 연출만 검사하며 실제 타이머를48초에서 진행/정지시킨 게임 테스트가 아니다.**

**남음:** 실제 BattleHud/타이머·Wave/HP/적 수/튜토리얼 플래그·정지/배속/실패/클리어 연결,Sprite Simple/Sliced·폰트/안전영역·화면 비율·가림/VFX·기기/색 공간·DD/사람 확인·최종 채택. Unity 실행/게임 적용은 미실시다. 생성 프롬프트/마스터·후보5개·원본6개·9배치/크기비교·재생HTML·변환/검사/해시·실기 확인표는 채팅 묶음으로 전달한다. 외부 공유 URL은 미등록이며 Git에는 기존 문서만 보완한다. 새 PNG/폴더/.meta/코드/데이터/설정/기존 원화 변경은 없다.

| 후보 파일 | SHA-256 |
|---|---|
| `hud_clock.png` | `e667758fe447171c33c28594887c122742963804b5d9abe06c77dceed46dd9d7` |
| `hud_wave_boss.png` | `46e79f39cfafaaad0688226465d8bf6b6bea15af04cb044506d06c2ac3c8b860` |
| `hud_wave_normal.png` | `8aff68892c636ff221b860898d7e0f3c53bb050038a58c9c18658bf2dcf7b647` |
| `hud_panel_9s.png` | `bb873455d9e47b6530301ad1439cdeae7378ce1250839ddc88083dd6d603d6a1` |
| `hud_remaining_badge.png` | `5d8cc0ddcc9191849f939394a534f64a6730cfcf97a0382df6f3d1728eb49919` |
