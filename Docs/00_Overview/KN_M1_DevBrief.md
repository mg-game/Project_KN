# KN_M1_DevBrief — M1 개발 지시 합본 (I-10a~h · I-13 · I-14 · I-15 · I-5 · I-6)

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | DD |
| 최종 수정 | 2026-11-12 |
| 관련 문서 | [`KN_Overview`](./KN_Overview.md) §2.2 (M1 작업 목록 · 🅰/🅱) · §2.7 (일정) · [`decisions.md`](../decisions.md) D-27 ~ D-57 · 지시 이슈 [#3](https://github.com/mg-game/Project_KN/issues/3) |
| 쓰는 사람 | 기개발 — **11/24 PR #2 R3 → 머지 뒤 착수** (D-57 #1) |

> **이 문서 하나만 읽고 M1 개발을 시작할 수 있게** 이슈 #3에 흩어진 지시 · 보충 메모(댓글 17개)를 PR 묶음별로 합쳤다. 내용은 지시와 같고, 합치다가 찾은 **어긋남 5건은 §0.3에서 정리**했다.
> 원문 링크는 각 묶음 맨 아래. 이 문서와 원문이 다르면 **이 문서가 우선** (뒤 결정까지 반영됨).

## 0. 공통

### 0.1 원칙 (I-10 상세 지시 그대로)
- PR은 아래 묶음별로 나눔. 각 PR = 기능 + **EditMode 테스트** + 해당 기획서 "① 현재 구현" 갱신 (D-25) + 테스트 결과 첨부 (D-21).
- 수치는 코드에 박지 않고 `BattleConfig`(또는 새 데이터)의 노브로 — 이름은 [`BAL_BattleKnobs`](../04_Balance/BAL_BattleKnobs.md) 그대로.
- 옛 기능은 지우지 않고 플래그로 끔 (`lamp_legacy_enabled` 등).
- 세이브 구조가 바뀌면 옛 세이브 이전(Migration) + 테스트 필수.
- **코드 이름 · 세이브 키는 그대로**, 화면 문구만 바꿈 (이름 통일은 I-7 뒤 — D-57 #3).
- Unity를 못 돌렸으면 PR에 "미검증" 표시.

### 0.2 순서 · 의존
```
11/24  PR #2 R3 → 머지
준비   I-14 경제 안전 · I-5 원정 시계 · I-6 폰트(🅱)          ← 서로 독립
W1     I-10a 8섹터 편성 → I-10b 웨이브
W2     I-10c 수선등 투자 · 숫자 교체 → I-10e 파견 잠금 → I-10h 1-10 보스 (I-10b 웨이브 · I-10c 잔향 흡수 필요)
W3     I-10f 전투 · 편성 UI (a~e 뒤) → I-10g 튜토리얼 · 문구 (I-10c 수선등 단계 필요)
       🅱 I-10d 성향 · I-13 레벨업 화면 · I-15 알림 칩 — 12/19까지 못 넣으면 M1.5 (D-57 #4)
```

### 0.3 합치다 찾은 어긋남 — 정리
| # | 어긋남 | 정리 |
|---|---|---|
| 1 | **보름달 수치**: I-10c 원문 · D-34 #4 · `CNT_Stage_Chapter1` · ART-12 = 잔향 수급 **+30%** / D-50 #2 메모 · `SYS_Synergy` §1.4 = **+20%** | ⚠ **결정 필요** — DD 추천 **+30%** (D-34가 먼저 정한 값, D-50의 20은 DD가 옮기다 틀림). 결정 전엔 노브 기본값 30 |
| 2 | I-13 #6 화면 문구 "의상 → **외형변경**" / D-55 외형 #7 "**외형**으로 통일" | **외형** (뒤 결정 D-55가 우선) |
| 3 | 편성 프리셋 3칸이 I-10a 보충과 I-10f 범위에 둘 다 | **데이터 · 저장 · 불러오기 = I-10a**, 화면 칸 3개 = I-10f. 프리셋의 "파견 중 칸 비우기"는 I-10e에서 연결 |
| 4 | I-13이 이슈 표에 **P0**, D-57 #4에서는 🅱 여유 | **🅱** (12/19까지 못 넣으면 M1.5) |
| 5 | 좀나방 · 녹얼룩 "대응 특성"이 기획서에서 코드와 반대 (`CNT_Enemy` §2.4 정정) | 코드가 맞음 — 코드는 **안 바꿈**, 힌트 문구만 §8 대로 |

### 0.4 코드 이름 ↔ 기획 이름 (I-7 전 참고용, D-39 S1)
| 코드 (그대로) | 기획 이름 (I-7 뒤 화면) |
|---|---|
| 짝짝이 · 똑단이 | 짝짝이 · 똑단이 |
| 찰칵이 | 키키 |
| 몽당이 | 몽실 |
| 우두커니 | 레이니 |
| 루즈리 | 루루팝 |
| 라라벨 | 엘리제 |

---

## 1. I-10a 8섹터 편성 — `feat/battle-8-sectors` · 🅰 (프리셋 🅱) · W1
**범위**
1. 전장 8섹터 · 지킴이 **8방위 슬롯**(배치 최대 5) · 적 스폰 8섹터(남 정면 금지 → SE · SW) · 보스 북 진입.
2. 세이브 이전: 기존 5칸 각도 → 가장 가까운 방위 (90°→N · 162°→W · 234°→SW · 306°→SE · 18°→E).
3. **자동 편성** (D-52): 지금(전투력 순 · 칸 순서) + **다음 스테이지 위험 방위(웨이브 압박 · 지속 압박 섹터, `CNT_Stage_Chapter1` 데이터) 먼저** → 남은 지킴이 전투력 순.
4. **편성 프리셋 3칸** (D-52, 🅱): 지킴이 5 방위 + 잔향이 덱 6을 한 묶음 저장 · 불러오기 (데이터 · 저장만, 화면은 I-10f).

**완료**: 옛 세이브 로드 후 편성 유지 · 8방위 각도 · 자동 편성 위험 방위 우선 테스트.
원문: [I-10 상세](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796448448) · [D-52 보충](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6820265095)

## 2. I-10b 웨이브 — `feat/battle-waves` · 🅰 · W1
**범위**
1. `killsToBoss` → **웨이브 N** · 웨이브 전환(잔존 비율 · 시간) · 보스 웨이브 호위 · **최후의 쇄도** · **스테이지 제한시간**(실패 원인 = 화력 부족).
2. 1-1~1-3 튜토리얼 보호 중 제한시간 끔.
3. 1장 = `CNT_Stage_Chapter1` §2.3 표 (웨이브 N · 스폰 패턴 · 위험/지속 압박 섹터 · 장소 이름) · 1-10 제한시간 ≈ 270초 (`BAL_BattleKnobs` §4.5) · 2장 이후 기본 N = 4 (x-10 = 5).

**완료**: 1장 10스테이지 데이터 로드 · 쇄도 진입/종료 · 제한시간 실패 · CLEAR > FAIL 우선순위 테스트.
원문: [I-10 상세](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796448448)

## 3. I-10c 수선등 투자 · 숫자 교체 — `feat/battle-light-invest` · 🅰 (공명 효과는 I-13) · W2
**3.1 수선등 밝히기** (D-27 #3)
1. 불빛 게이지 → **수선등 밝히기(잔향 수급 레벨 투자)** L1~5 · AUTO 투자 규칙 · `lamp_legacy_enabled` = false로 옛 전체 공격 보관 · `LampPolicy` 기능 숨김.
2. 보름달 `FullMoon` = **잔향 수급 +30%** (§0.3 #1 결정 전 기본값) · 설명 문구 "불빛 게이지 충전" 교체.

**3.2 불빛 게이지에 묶였던 것 교체** (D-40 #4 · 망각체 #1)
3. `OnLampLit` → `OnLightInvest`(수선등 투자 직후) · `LampGain` → `ResonanceGain` · `LampDrain` → **잔향 흡수**(닿으면 잔향 −N · 근처 주기적 −n). 값은 노브.
4. `drain_tutorial_off` — 1-1~1-3 튜토리얼 보호 중 흡수 0 (D-41 #4).

**3.3 스킬 · 전투 규칙** (D-43 · `CNT_Skill` §2.1 · §4 · §5)
5. **잔향이 고유 스킬 약한 판**: 잔향이 소환 시 고유 스킬 1회, 값 × `echo_skill_ratio` 0.5 · 범위 × 0.7. 발동 = **몽당이만 풀릴 때(`OnDissolve`, 수명 끝 · 처치 둘 다)**, 나머지는 소환 시. 반복 없음.
6. 몽당이 `mong_base`: `LampGain 0.03` → `ResonanceGain` **+1/회** (`mong_resonance_gain`).
7. **보스 기절**: 기절 시간 × `boss_stun_mult` 0.5 · 끝나고 `boss_stun_immune_sec` 3초 면역.
8. 짝짝이 작은 친구: 잔향이 상한(12)에 세지 않음 · 별도 상한 6 · 넘으면 가장 오래된 것부터 흩어짐.
9. 겹침 규칙 확인만: 기절 = 남은 시간 긴 쪽 · 수선점 보호막 = 큰 값 덮어쓰기.

**3.4 효과 0 능력치 교체** (D-50 #2 · D-55)
10. `StatType.LampGainPercent` 쓰는 곳 (시너지: 지원 성향 · 빗속의 등불 · 마지막 노래 · 일곱 개의 불빛 / **도감 시너지 종류 보너스** `CodexSystem.AddCollectionBonus`) → `ResonanceRegenPercent` **같은 값** · `MetaDatabase.cs` 툴팁 "불빛 충전 %" → "잔향 수급 %".
11. `StatType.LampDamagePercent` (빗속의 등불 25 · 마지막 노래 35) → 신규 `LightInvestCostReductionPercent` 값 × 0.5 → **12 · 17**.
12. `SynergyText` 문구 갱신.

**3.5 상한** (D-55)
13. 외형 공격력: `CharacterSystem.GetCostumeAttackBonusPercent` = `min(costumeAtkCapPercent, 합)` · `costumeAtkCapPercent` = 10.
14. 짝짝이 외형 값 재배정 (`CharacterDatabase.asset`): nightkeeper 2 · cheer 2 · mail 1 · traveler 2 · festival 3 · caterpillar 2 · plush 2 · puppet 2 · sleepcap 3 · threadweb 2.
15. 던전 달빛: `DungeonSystem.GetMoonlightChance` = `min(dungeonMoonlightCap, 12% + 1%/층)` · `dungeonMoonlightCap` = 0.4.

**완료**: 레벨별 수급 · 비용 · AUTO 조건 · 옛 플래그 · 몽당이 풀릴 때 1회 · 보스 기절 0.5배 + 면역 3초 · 작은 친구 상한 6 · **시너지 상세 · 도감 · 전투력에 효과 0 능력치 없음** · 외형 상한 · 달빛 상한 테스트. 기획서 갱신: `SYS_InGameBattle` · `CNT_Skill` · `SYS_Synergy` · `SYS_Codex` · `SYS_Costume` · `SYS_Dungeon` ①.
원문: [I-10 상세](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796448448) · [추가 범위](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796977951) · [②](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6813262305) · [③](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6819302870) · [D-55](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6821040096)

## 4. I-10d 잔향이 성향 — `feat/echo-behavior` · 🅱 (줄이면 "기본 성향 1종") · W3
**범위**
1. 잔향이 **행동 성향 3종** (`Charge` 앞장서기 · `Patrol` 함께 걷기 · `Hold` 곁 지키기) · 성향별 이동 · 표적 · 유지 시간 · 비용 배율.
2. 계보 기본 성향 (`Summon_Mode.base_behavior`, D-46 #6): 짝짝이 Charge · 똑단이 Hold · 찰칵이 Charge · 몽당이 Hold · 우두커니 Hold · 루즈리 Charge · 라라벨 Patrol.
3. 탭 소환 방향: 곁 지키기 → 지속 압박 섹터 우선.

**완료**: 성향별 이동 · 표적 · 유지 시간 · 비용 배율 테스트.
원문: [I-10 상세](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796448448) · [D-46 보충](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6815273160)

## 5. I-10e 파견 잠금 — `feat/dispatch-lock` · 🅰 · W2
**범위** (D-27 #5 · D-54 · D-56)
1. **파견 중 계보 편성 불가** — 지킴이 · 잔향이 덱 · **던전 덱** 모두 · `PlaceResult.Dispatched` 실제 반환.
2. **출발 가드**: 보낸 뒤 편성 가능한 계보 < `minFormableAfterDispatch`(3) → 출발 막음 · 버튼 회색 + "{k}명만 보내면 출발할 수 있어요". **가드 판단 함수 하나**를 준비 팝업 · 다시 보내기 · 모두 다시 보내기가 같이 부름.
3. **출발 확인**: 편성 중 계보를 보내면 "편성에서 빠져요 (지킴이 짝짝이 · 잔향이 우두커니)" 1회 → [빈 칸 자동 채우고 출발] / [그냥 출발] / [취소].
4. **자동 복귀**: 원래 칸이 비어 있을 때만 원래 자리로 (`expedition.prevSlots`) · 잠금은 **완료 시각에** 풀림 (받기 전이라도).
5. 프리셋 불러올 때 파견 중 계보 칸만 비움 (I-10a 프리셋과 연결).
6. **원정 준비 자동 선택** (`ExpeditionSetupPopup.AutoPickKey`): 원정 안 나감 → **편성에 없음** → '원정 N회' 많이 남음 → 전투력 → ID.
7. **모두 다시 보내기** (`ExpeditionSystem.ResendAll`): 구역마다 가드 확인 → 걸리면 건너뜀 → "{n}곳을 다시 보냈어요 · {m}곳은 싸울 친구가 모자라 남겨 뒀어요".
8. 진행 중 구역 카드: 멤버 초상 위 '편성 잠김' 자물쇠 16 (완료 시각에 사라짐).
9. 1장 구역 4개 이름 · 설명 문구만: `exp_1_1`~`exp_1_4` → 양말 세탁소 뒷골목 · 오래된 문구점 창고 · 촛불 골목 끝 · 분실물 시장 좌판 (수치 그대로).

**완료**: 파견 중 배치 거부 · 출발 시 해제 · 가드(준비 · 다시 · 모두 다시 셋 다) · 복귀 · 완료 시각 잠금 해제 · 자동 선택 순서 테스트.
원문: [I-10 상세](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796448448) · [D-54](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6820811509) · [D-56](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6821319209)

## 6. I-10f 전투 · 편성 UI — `feat/battle-ui-m1` · 🅰 · W3
**범위**
1. `UI_Battle` §4 8건 · `UI_Formation` §5 7건: 하단 2열 · 수선등 버튼(잔향 줄 오른쪽, D-44 #2) · 성향 아이콘 · 8방위 링 · 빈 방위 빗금 · 웨이브 예고 · HUD · **실패 원인 리포트 + CTA 3버튼** (D-28 #10) · 파견 배지 · **프리셋 3칸 화면**.
2. 잔향이 오라 대비 (D-48 #5): `EchoView` 오라 = `EchoGlow`와 계보 대표색 섞는 비율 기본 0.25 → 그림 평균 밝기 L < 130 계보(지금 우두커니)는 **0.4** (`echo_aura_boost`) · 오라 최소 밝기 0.4.
3. 실패 리포트 "방어 문제"일 때 그 스테이지 대표 적의 `counter_hint` 1줄 (§8).
4. 아트는 `ART_M1_Backlog` P0 — 납품 전엔 자리표시 스프라이트.

**완료**: 화면 빌더로 생성 · 플레이 스크린샷(가능하면) · 미실행이면 "미검증".
원문: [I-10 상세](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796448448) · [D-48 보충](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6815543540)

## 7. I-10g 튜토리얼 · 스토리 — `feat/ftue-m1` · 🅰 · W3
**범위**
1. 튜토리얼 단계 교체 3건 (`ftue_first_light_invest` · `ftue_empty_direction` · `ftue_behavior_hint`) · 2번 단계 `blockInput` = 1 · `summon_cap` 노브(기본 12).
2. `ftue_first_lamp` 문구: "잔향이 모이면 수선등을 밝혀요 · 불이 커질수록 잔향이 더 빨리 모여요!" · 완료 조건 `LampLit` → 수선등 투자 (I-10c).
3. **인트로 4장 교체** (D-53 #2, `CNT_Story` §2.2) — ART-15 전엔 기존 3장 재사용 (4번째 = 2번 그림) · 건너뛰기 · 시간 규칙 그대로.
4. **1장 이야기 박자 11개** (`Story_Beat` 데이터): 스테이지 시작 · 보스 등장 · 클리어 말풍선 1~2줄, once · 건너뛰기 · AUTO면 3초 뒤 닫힘 · 잠자기 모드면 생략. 컷신 없음.
5. 1-3 클리어: 유실물 종 연출 + 레이니 유실표 문구 한 줄 → 첫 만남 카드 (D-32 흐름 유지).

## 8. 문구 · 데이터 묶음 (I-10g와 함께) · 🅰
코드 이름 · 세이브 키 그대로, **화면 문구만**.
| 어디 | 지금 | 바꿈 | 근거 |
|---|---|---|---|
| 하단 탭 · 화면 | 친구들 · 친구 | **유실자** (대사는 "친구" 허용) | D-44 #3 · D-53 #5 |
| 재화 | 기억편 | **기억 조각** | D-30 #2 · D-43 #7 |
| 외형 | 의상 · 외형변경 | **외형** | D-55 #7 (§0.3 #2) |
| 외형 탭 머리 | 보유 효과 공격력 +N% | **+N% / 최대 +10%** (최대면 금빛) | D-55 · D-56 유실자 #4 |
| 도감 분류 탭 | 친구 · 의상 · 적 · 시너지 | **유실자 · 외형 · 그림자 · 조합** | D-55 도감 #5 |
| 유실자 미보유 | '미보유' · '소환하러 가기' | **'아직 못 찾음'** · **'찾으러 가기'** (이동 = 소환 시트) | D-56 유실자 #1 |
| 원정 탭 쪽 | 원정 · 잔향이 던전 | **의뢰 · 던전** (탭 이름 = 원정) | D-54 #5 |
| 던전 화면 | — | 제목 · 입장 대사 **"기억의 안쪽"** | D-55 던전 #2 |
| 가방 바로 가기 | [원정 ›][잔향이 던전 ›] | '재료는 의뢰 · 던전 · 상점에서 모을 수 있어요' · **[의뢰 ›][던전 ›][상점 ›]** · 획득처 줄도 같은 이름 | D-56 가방 #1 |
| 상점 | 이벤트 쪽 '별이 머문 밤 산책' | **M1 숨김** (데이터로 끄기) | D-56 상점 #2 |
| 튜토리얼 · 시너지 · 스테이지 기믹 | "불빛 게이지" 들어간 문구 전부 | 수선등 · 잔향 수급 | D-53 #4 · D-50 |
| 망각체 | — | `origin_text` · `counter_hint` 10줄 (`CNT_Enemy` §2.4) — `EnemyDef` 신규 칸 | D-40 망각체 #2 |

**완료(7 · 8)**: 튜토리얼 단계 진행 테스트 갱신 · **화면에 "불빛 게이지" · "의상" · "기억편" 0개** (문자열 검색 결과 PR 첨부).
원문: [I-10 상세](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796448448) · [D-53](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6820592711) · [D-55](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6821040096) · [D-56](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6821319209)

## 9. I-10h 1-10 보스 — 🅰 · W2
1. `EnemyDef.enrageThreshold` (비면 `BattleConfig.enrageThreshold`) → 보스별 분노 임계.
2. `boss_mothqueen_lamp` 추가 (여왕 복사 · 체력 같음 · `enrageThreshold` 0.3) · ART-10 전엔 `boss_mothqueen.png`.
3. 스테이지 `boss_id` (비면 지금 순환 공식) → 1-10만 `boss_mothqueen_lamp`.
4. `Boss_Phase` 데이터 + 실행기: 체력 비율로만 발동 · 여러 임계 동시 통과 = 순서대로 큐 (`phase_gap_sec` 2초) · 행동 `SummonRing` · `SummonRingRepeat` · `MinionIntervalScale` · `Leak`.
5. `SummonRing`: 예고 `ring_telegraph_sec` 1.5 → 8섹터(또는 정방위/대각 교대). 적 상한이면 **빈 방향(지킴이 없음) 먼저** 남은 칸만큼, 1초 뒤 1회 재시도 후 취소. 예고 중 보스 처치 → 취소.
6. `Leak`: 숨은 적 전부 드러남 + 보스 생존 동안 숨기 무효 + 분노.
7. 페이즈 · 행동 큐 · 링 타이머를 전투 스냅샷에 포함.

**완료**: 70% · 30% 동시 통과 순서 · 상한 시 빈 방향 우선 · 예고 중 처치 취소 · 튜토리얼 흡수 0 · 일반 보스 2종 회귀. `CNT_Boss` ① 갱신.
원문: [I-10h](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6799935728)

## 10. I-13 레벨업 화면 — 🅱 (§0.3 #4) · W3
1. **공명 잔향이 효과** (`Resonance_Effect`): 소환 비용 −1 (공명 3 · 6 · 9, 최솟값 3) · 지속 +0.5초/레벨 · 공용 +5%/레벨 그대로 · 동시 출현 X (P1).
2. 상세 성장 칸: 능력치 전→후 · 공명 효과 전→후 · x10 (살 수 있는 만큼 "x10 (7)").
3. 피드백 칩: "팀 전투력 +N" (연속 합산) · "다음 스테이지: 도전 → 여유".
4. 강화 탭 추천 줄 (`StrengthenAdvisor` 1순위, 탭 = 스크롤 + 금빛 테).
5. 계보 카드: 편성 위치 배지 · 공명 가능 금 점 · 수선점 카드 다음 별조각까지 남은 레벨.
6. 문구는 §8 (외형 = "외형").

**완료**: 공명 3 → 비용 −1 · 최솟값 3 테스트 · 길게 누르기 중 버튼 유지. `UI_Growth` · `SYS_Growth` ① 갱신.
원문: [I-13](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6813262533)

## 11. I-14 경제 안전 — 🅰 · 준비 기간
지급 검증(+∞ · NaN · 0 · 음수 거부) · 소비 검증 · **로드 검증**(음수 · NaN · ∞ → 0 + 경고) · 재료 상한(임시 999,999) · **별조각 유료/무료 2풀** (`gem_paid` · `gem_free`, 합 = `currencies[Gem]`, 무료 먼저 소비, 옛 세이브 = 전부 무료). 결제 코드 X.
원문: [I-14](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6826248643)

## 12. I-15 홈 알림 칩 — 🅱 · W3
시트 열린 동안 보스 등장 · 수선점 위기(30% 이하) · 클리어 · 실패 칩 · 탭 = 시트 닫고 홈 · 3초 · 실패 "1-5 실패 — 1-4에서 다시".
원문: [I-15](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6826249134)

## 13. I-5 · I-6
- **I-5** 원정 시계 어뷰징 H-1 (D-23) — 🅰 · 준비 기간 · [지시](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6786384604)
- **I-6** 한글 폰트 Static 아틀라스 (D-18) — 🅱 · 준비 기간 · Unity PC 필요 · [지시](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6786384828)

## 14. M1에 넣지 않는 것 (요청 와도 보류)
§16 #4 배타 · 재집결 · 성격 모듈 · 지킴이 피격 (D-37) · 수선점 시설 · 한계 돌파 · 진화 3단 · 구조 전투 · 결제 — 전체는 `KN_Overview` §2.5.
