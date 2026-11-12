# CNT_Character_Rainy — 레이니 원본 기획 진입점

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | AI 아트 (기존 자료 연결 · 사용자 제공안 정리) |
| 최종 수정 | 2026-11-12 |
| 관련 테이블 | `LineageDef.id=udukeoni` · `CharacterFormDef.id=udu_base` (PR #2) |
| 관련 문서 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) · [전달 마스터](../07_Art/ART_CharacterHandoff.md) · [스타일](../07_Art/ART_StyleGuide.md) |

> 이 캐릭터의 **정체성·기본 설정 원문은 기존 설정집 해당 절**에 유지한다. 이 문서는 그 원문의 항목별 진입점과 현재 코드·이미지 연결을 담당한다. 원문을 복사하거나 새 설정을 확정하지 않는다. 원문 상태는 `Review`이다.

## 1. 식별 · 근거

| 필드 | 값 |
|---|---|
| 문서 관리 ID | `CNT_Character_Rainy` (런타임 키 아님) |
| 기획 표시 이름 / 코드 표시 이름 | 레이니 / 우두커니 |
| 현재 `lineage_id` / 기본 `form_id` | `udukeoni` / `udu_base` |
| 설정 기획 상태 | 검토 중 — 기존 원문의 Review 유지 |
| 설정 원문 기준 | `develop` @ `a24c577928afef99da39aeedc38767be78fb8091` |
| 코드·자산 기준 | PR #2 @ `4b73a5a33c22d1b6246794c86fdbf7b27db281b0` (develop 미병합) |
| 이름 대응 근거 | [I-7 대조표](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796460728) · [결정 기록](../decisions.md) D-39 |

## 2. 원본 기획 항목 (기존 원문으로 연결)

| 항목 | 기준 위치 | 읽는 범위·추가 확인 |
|---|---|---|
| 캐릭터 이름·원형 물건·탄생 배경 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) | 상단 기본 정보 + 수선점 합류 |
| 핵심 컨셉·매력 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) | 한 줄 소개 + 교훈 + IP 확장 |
| 세계관·내러티브 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) · [공통 세계관](../00_Overview/KN_ConceptNarrative.md) | 관계 + 수선점 합류 |
| 성격·말투·행동 습관 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) | 성격 + 대표 대사. 새 말투 규칙은 미정 |
| 기본 외형·실루엣 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) | 외형 절. 시안과 게임 구현의 차이를 §4에 기록 |
| 디자인 고정 요소 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) · [기존 브리프](../07_Art/ART_Character_Brief.md) §1 | 원물 구조·식별 요소. 채택 여부는 별도 |
| 핵심 컬러·재질 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) · [스타일](../07_Art/ART_StyleGuide.md) | 대표 색상·외형. 새 HEX 확정 안 함 |
| 지킴이·잔향이 기본 설정 | [용어집](../00_Overview/KN_Glossary.md) · [전투](../01_System/SYS_InGameBattle.md) §4.1·§16 #4 | 현재 구현과 기획 방향의 편성 정책 차이 유지 |
| 전투 역할·특징 | [설정집 레이니 원문](./CNT_Character_Main.md#7-레이니) · [스킬](./CNT_Skill.md) | 원문 전투는 콘셉트, 현재 코드 기본형은 `Defense / Guardian` |
| 아트 제작 주의사항 | [스타일](../07_Art/ART_StyleGuide.md) · [전달 마스터](../07_Art/ART_CharacterHandoff.md) | 원물 구조 보존·과잉 장식 지양·카드/실제 PNG 구분 |
| 원본 이미지 | 아래 §3 | 현재 기본형을 참고용으로 연결. 새 스타일 채택 원본으로 간주 안 함 |

## 3. 이미지 · 데이터 연결

| 역할 | 실제 경로·링크 | 확인 상태 |
|---|---|---|
| 현재 게임 기본형 | [Assets/_Project/Art/Characters/udu_base.png](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Characters/udu_base.png) | Git 트리·LFS 포인터 존재 확인, 픽셀·Unity 임포트 미검증 |
| 기본형 `.meta` | [.meta](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Art/Characters/udu_base.png.meta) | 기존 참조 보존 |
| 콘텐츠 원본 | [Assets/_Project/Editor/Content/CharacterContent.Lineages.cs](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Editor/Content/CharacterContent.Lineages.cs) | ID·기본형 이름 대조 |
| 구운 데이터 | [CharacterDatabase.asset](https://github.com/mg-game/Project_KN/blob/4b73a5a33c22d1b6246794c86fdbf7b27db281b0/Assets/_Project/Data/CharacterDatabase.asset) | 현재 구현 참조 |

> 링크는 본체가 아직 `develop`에 없어 근거 커밋으로 고정했다. 병합 후 실제 파일을 확인하고 `../../Assets/_Project/Art/...` 상대경로로 갱신한다. 폴더·파일은 이동하지 않는다.

## 4. 기획 / 현재 구현 차이 · 미정

- 설정집은 투명 우산·빗물 얼굴·해파리 촉수, 현재 코드는 우산천 망토와 손잡이 꼬리다. 첨부 카드를 기존 게임 기본형 이미지와 동일한 파일로 취급하지 않는다.
- D-39는 이름 통일 방향을 정했지만 이번 작업에서 표시 이름·ID·세이브·에셋 경로를 변경하지 않는다.
- 새로운 재활용 외형의 구조·스킬·획득 방식은 외형 기획서의 확인 상태를 따른다.

## 5. 외형변경 목록

| 문서 | 외형명 | 유형 | 기획 상태 |
|---|---|---|---|
| [CNT_CharacterAppearance_Rainy_AlleyLantern](./CNT_CharacterAppearance_Rainy_AlleyLantern.md) | 골목등불 레이니 | 외형 문서 §1 참조 | 검토 중 |

## 6. 검수 · 변경 기록

- 2026-11-12: 기존 원문을 보존한 항목별 진입점 및 외형·자산 링크 추가. 공식 설정의 승인·게임 반영 없음.
- 원본 이미지의 픽셀·최종 납품 기술 검수: 미실행.
