# ART_CharacterWorkflowAudit — 기존 구조 분석 · 캐릭터 아트 관리 적용 기록

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | AI 아트 (사용자 요청에 따른 문서 체계 보완) |
| 최종 수정 | 2026-11-12 |
| 관련 테이블 | 기존 계보·형태·외형 ID 참조만 |
| 관련 문서 | [전달 마스터](./ART_CharacterHandoff.md) · [설정집](../02_Content/CNT_Character_Main.md) · [Docs 관리 규칙](../README.md) · [협업 규칙](../../CONTRIBUTING.md) |

> **변경 범위:** 문서 추가·연결·템플릿 보완만. 기존 데이터·설정·이미지·파일 경로·런타임 ID를 변경하거나 삭제하지 않는다. 채택·병합은 이 기록과 별개로 기존 검수 절차에 따른다.

## 1. 분석 기준 · 저장소 전체 구조

분석일: 2026-11-12 (한국 시간). `main`만 보면 README와 .gitignore뿐이라 현재 통합 문서와 미병합 본체를 함께 조사했다.

| 범위 | 근거 | 조사 결과 |
|---|---|---|
| main | `091cf222522bab9ecf91947f8710cfcbebfca77c` | 추적 파일 2개, 게임 본체 없음 |
| develop | `a24c577928afef99da39aeedc38767be78fb8091` | 추적 파일 104개. Docs·협업·Unity 설정과 빈 에셋 자리 |
| PR #2 본체 | `feature/lost-and-lovely-import` @ `4b73a5a33c22d1b6246794c86fdbf7b27db281b0` | 추적 파일 3894개. 실제 코드·이미지·.meta·기존 영문 기획 |
| 열린 관련 PR | [#42 진화](https://github.com/mg-game/Project_KN/pull/42) · [#41 홈](https://github.com/mg-game/Project_KN/pull/41) | 다른 작업의 문서를 병합하거나 수정하지 않고 범위 확인 |
| 기존 아트 브리프 브랜치 | `docs/art-character-brief` | develop에 이미 `ART_Character_Brief` 존재. 같은 역할의 두 번째 브리프를 만들지 않음 |

모든 추적 경로를 `git ls-tree -r --name-only`로 열거했다. 두 기준의 전체 디렉터리 목록은 부록 A에 있다. 현재 저장소에 적용되는 `AGENTS.md`는 두 트리에서 발견하지 못했다.

```text
Project_KN/
├─ .github/                 이슈·PR 템플릿
├─ Assets/
│  ├─ TextMesh Pro/         PR #2
│  ├─ ThirdParty/           기존 보존
│  └─ _Project/
│     ├─ Art/               실제 아트는 PR #2
│     │  ├─ Characters/     형태별 {form_id}.png (캐릭터별 하위 폴더 없음)
│     │  ├─ Costumes/       {costume_id}.png
│     │  ├─ Concept/        기존 컨셉 카드 6종
│     │  └─ Battle, Enemies, FX, VFX, Icons, Items, Meta, Story, Synergy, UI, ...
│     ├─ Editor/Content/    코드 콘텐츠 원본
│     ├─ Data/              구운 CharacterDatabase 등
│     ├─ Scripts/           런타임
│     └─ Audio, Fonts, Generated, Platform, Plugins, Prefabs, Resources, Scenes, ...
├─ Docs/
│  ├─ 00_Overview/          컨셉·용어·문서 지도·로드맵
│  ├─ 01_System/            전투·성장·경제·스킬 등
│  ├─ 02_Content/           기존 대표 설정집, 캐릭터별 원본 진입점·외형 문서
│  ├─ 03_UI_UX/             UI 및 기존 images/
│  ├─ 04_Balance/, 05_DataTable/, 06_Meeting/
│  ├─ 07_Art/               기존 스타일·브리프·규격 + 전달 마스터·이 기록
│  ├─ 08_Audio/, 09_Research/, 99_Archive/
│  ├─ LostAndLovely/        PR #2의 기존 11개 영문 문서 (I-4 이관 예정)
│  ├─ README.md, decisions.md, _Template_System.md
│  └─ _Template_Character.md, _Template_CharacterAppearance.md
├─ Packages/, ProjectSettings/  기존 보존
└─ Tools/                   기존 변환기 자리·팀 보드
```

### 기존 문서·이미지·프롬프트 조사

| 역할 | 기존 위치 | 이번 적용 |
|---|---|---|
| 8종 정체성·원형·성격·진화·관계·합류·대사 | [CNT_Character_Main](../02_Content/CNT_Character_Main.md) | §2~§11·§13 원문 보존. §12는 기존 간이 양식을 새 상세 템플릿과 캐릭터별 진입 링크로 보완 |
| 세계관·용어 | [KN_ConceptNarrative](../00_Overview/KN_ConceptNarrative.md) · [KN_Glossary](../00_Overview/KN_Glossary.md) | 정본 링크만, 설정 변경 없음 |
| 스타일·공통 스타일 프롬프트 | [ART_StyleGuide](./ART_StyleGuide.md) §2·§13·§16 | 그대로 보존. 미술 문법 정본 |
| 기본형 8종 제작 브리프·개별 프롬프트 | [ART_Character_Brief](./ART_Character_Brief.md) | §0.2 실행 문장을 마스터 기본형 모드로 연결. §0.1·개별 브리프·범위 유지 |
| 전투 PNG 기술 기준 | [ART_BattleSpriteSpec](./ART_BattleSpriteSpec.md) | 그대로 보존. 카드와 게임 파일 규격 분리 |
| M1 아트 목록 | [ART_M1_Backlog](./ART_M1_Backlog.md) | 보존. 새 카드를 게임 교체 작업으로 등록 안 함 |
| 기존 영문 대량 제작 계획 | PR #2 `Docs/LostAndLovely/ART-UPGRADE-PLAN.md` | 과거 작업의 비용·도구·개인 PC 경로는 이번 실행 지시가 아님. 복제·전면 개정 안 함 |
| 이미지 위치 | PR #2 `Art/Characters`, `Costumes`, `Concept` | 기존 파일 사용. 새 캐릭터 폴더·중복 이미지 없음 |
| Markdown 규칙 | [Docs/README](../README.md) | 접두어·PascalCase·버전 없는 파일명·상태 헤더·상대링크 준수 |

## 2. 유지한 폴더·파일

**새 폴더 0개, 기존 파일 이동·삭제 0개.** 부록 A의 모든 기존 디렉터리를 유지한다. 수정 목록(§3)에 없는 develop 추적 파일은 그대로이며 PR #2는 읽기만 했다.

- `Assets/**` 전체와 `.meta`, 이미지, 코드·데이터·세이브 관련 ID 전부.
- `Packages/**`, `ProjectSettings/**`, `.gitattributes`, `.gitignore`, `.github/**`, `CONTRIBUTING.md`.
- `Docs/decisions.md`: 새 설정을 승인한 것처럼 결정 번호를 추가하지 않음.
- 스타일·규격·M1 백로그·세계관·용어집·기존 시스템/콘텐츠 문서의 설정.
- 기존 원본 템플릿 `_Template_System.md`, `05_DataTable/_Template_Table.md`와 UI `images/`.
- 기존 `Docs/LostAndLovely/` 이관은 I-4 범위라 수행하지 않음. 예정된 `_Templates/`도 이번에 선행 생성하지 않음.

## 3. 추가·수정 문서 및 이유

### 추가 10개 (기존 폴더 안)

- [02_Content/CNT_Character_Jjakjjak.md](../02_Content/CNT_Character_Jjakjjak.md)
- [02_Content/CNT_Character_Rainy.md](../02_Content/CNT_Character_Rainy.md)
- [02_Content/CNT_Character_Mongsil.md](../02_Content/CNT_Character_Mongsil.md)
- [02_Content/CNT_CharacterAppearance_Jjakjjak_Nightkeeper.md](../02_Content/CNT_CharacterAppearance_Jjakjjak_Nightkeeper.md)
- [02_Content/CNT_CharacterAppearance_Rainy_AlleyLantern.md](../02_Content/CNT_CharacterAppearance_Rainy_AlleyLantern.md)
- [02_Content/CNT_CharacterAppearance_Mongsil_Wax.md](../02_Content/CNT_CharacterAppearance_Mongsil_Wax.md)
- [_Template_Character.md](../_Template_Character.md)
- [_Template_CharacterAppearance.md](../_Template_CharacterAppearance.md)
- [07_Art/ART_CharacterHandoff.md](./ART_CharacterHandoff.md)
- [07_Art/ART_CharacterWorkflowAudit.md](./ART_CharacterWorkflowAudit.md)

### 수정 4개

| 파일 | 보완 내용·이유 |
|---|---|
| [CNT_Character_Main](../02_Content/CNT_Character_Main.md) | §12 간이 추가 양식을 상세 원본 템플릿으로 통합하고 3종 원본·외형 링크 등록. 실제 설정 원문은 보존 |
| [ART_Character_Brief](./ART_Character_Brief.md) | §0.2 공통 실행 지침을 한 곳으로 연결. 기본형 시트 요구는 유지해 카드 레이아웃과 혼동 방지 |
| [Docs/README](../README.md) | 템플릿·마스터·워크플로 연결, 캐릭터 시안의 D-24 예외 명시 |
| [KN_DocMap](../00_Overview/KN_DocMap.md) | 새 문서·템플릿을 기존 지도에 추가, 기존 결정·상태를 임의 승격하지 않음 |

캐릭터별 원본 문서는 원문을 옮기는 대신 **기존 원문을 가리키는 항목별 진입점**으로 만들었다. 이는 “캐릭터마다 한 진입 문서”와 “기존 설정 중복·삭제 금지”를 함께 만족시키기 위한 선택이다. 신규 캐릭터부터는 템플릿에 원문을 직접 작성한다. 원문 정본 이전은 이번 범위가 아니다.

## 4. 템플릿 · 마스터 위치와 사용

| 문서 | 포함 정보 | 사용법 |
|---|---|---|
| [원본 템플릿](../_Template_Character.md) | ID·이름·원물/탄생·매력·내러티브·성격/말투/습관·기본 외형·고정 요소·컬러/재질·지킴이/잔향이·전투·주의·원본 이미지 | `02_Content/CNT_Character_<Name>.md`로 복사하고 **복사 위치 기준으로 링크 조정**. 기존 캐릭터는 설정집 원문 링크로 채움 |
| [외형 템플릿](../_Template_CharacterAppearance.md) | 원본 FK·외형 ID/이름·유형·상태·재활용 전→후·이유·실루엣·유지/변경·재질/색·감정·전투 표현·제작 지시·이미지·검수 | 같은 폴더 `CNT_CharacterAppearance_<Name>_<Appearance>.md`. 원본의 성격/탄생 설정을 복사하지 않음 |
| [ART_CharacterHandoff](./ART_CharacterHandoff.md) | 첨부 v2 기반 공통 규칙·카드 레이아웃·상세 본문·D1~D3 대응·기술 단계·검수·복사용 실행 지시 | §8 A 기본형 시트 또는 §8 B 외형 카드 모드를 명시. 두 모드를 섞지 않음 |

파일명은 버전 없는 PascalCase, 문서 이력은 Git 커밋으로 추적한다. 외형 문서에 제작 지시·피드백을 같이 두며 BRIEF/ART_NOTES 사본을 늘리지 않는다.

## 5. ID · 이미지 연결과 우선 3종 검증

### 5.1 현재 코드와 기획 이름

| 기획 이름 | 현재 코드 이름 | `lineage_id` | 기본 `form_id` | 결과 |
|---|---|---|---|---|
| 짝짝이 | 짝짝이 | `jjakjjak` | `jjak_base` | 기획의 물량/교란과 기본형 방어 역할 차이 기록 |
| 레이니 | 우두커니 | `udukeoni` | `udu_base` | 이름 통일 방향 D-39, 키·이미지 그대로 |
| 몽실 | 몽당이 | `mongdang` | `mong_base` | 원물/성격 핵심 대응, 촛불 신체 세부 차이 기록 |

근거: [I-7 답](https://github.com/mg-game/Project_KN/issues/3#issuecomment-6796460728), 코드 `CharacterContent.Jjakjjak.cs`·`CharacterContent.Lineages.cs`, `CharacterDatabase` 모델과 자산. ID는 기존 영문 string이며 표시 이름과 다르다. 이름을 바꿔도 키를 바꾸지 않는다. 문서 관리 ID는 파일 stem으로서 런타임에 등록되지 않는다.

| 런타임 종류 | 기존 파일명·위치 | 연결 |
|---|---|---|
| 형태 | `Art/Characters/{form_id}.png` | `CharacterFormDef.portrait` |
| 선택 외형 | `Art/Costumes/{costume_id}.png` | `CostumeDef.portrait` |
| 기존 컨셉 카드 | `Art/Concept/{costume_id}.png` | `CostumeDef.illustration`· `ConceptSheetCostumes`에 해당 ID 목록 |
| 호감도 포즈 | `Characters/{form_id}_pose10.png` | 메뉴용 보상. 진화나 선택 외형 ID로 추론 금지 |

`jjak_postman`(현재 진화 형태)과 `jjak_mail`(선택 외형)은 별개다. 두 종류를 같은 외형으로 합치지 않는다. D-39의 직업형 분리 방향과 PR #42 상세 내용은 구현 전이므로 이번 문서에서 변환하지 않는다.

### 5.2 우선 적용 결과

| 대상 | 원본 | 외형 기획 | 검증 결과 |
|---|---|---|---|
| 짝짝이 | [항목별 진입점](../02_Content/CNT_Character_Jjakjjak.md) | [밤지기](../02_Content/CNT_CharacterAppearance_Jjakjjak_Nightkeeper.md) | 기존 `jjak_nightkeeper` Costume·Concept·Costumes 재사용. 기본형과 외형을 분리. 첨부 카드와 기존 PNG 동일성·재활용 구조는 미검수 |
| 레이니 | [항목별 진입점](../02_Content/CNT_Character_Rainy.md) | [골목등불](../02_Content/CNT_CharacterAppearance_Rainy_AlleyLantern.md) | 제공 카드에서 관찰 가능한 구조와 제안 서사만 기록. 동명 외형·리소스·ID 미등록, 유형·획득·재활용 상세 미정 |
| 몽실 | [항목별 진입점](../02_Content/CNT_Character_Mongsil.md) | [촛농·밀랍 양식](../02_Content/CNT_CharacterAppearance_Mongsil_Wax.md) | 구체안 없음. 소재 방향 외의 외형명·왕관·갑옷·사건·색·스킬을 창작하지 않고 입력 대기 |

### 5.3 이미지 연결 방식

- 같은 Docs 내 연결은 상대경로. `원본 ↔ 외형`, `설정집 → 개별 문서`, `브리프 → 마스터 → 전문 규격`으로 양방향 탐색한다.
- 본체 미병합 때문에 현재 게임 이미지·코드는 **PR #2 고정 커밋 blob 링크 + 저장소 내부 경로**를 기록한다. develop에 없는 상대경로를 만들어 깨진 링크를 남기지 않는다. 병합 후 실제 파일을 확인해 상대링크로 바꾼다.
- 이번 첨부 카드 2장은 채팅에서만 확인되었고 다운로드 가능한 별도 파일 경로·공유 URL은 제공되지 않았다. 가짜 경로로 복사한 척하지 않고 각 외형 표에 `공유 URL 미등록`을 남겼다. 기존 Git 밤지기 카드와 동일 파일이라고 단정하지 않는다.
- 새 컨셉 이미지는 D-24에 따라 Notion/보드 공유 위치에 올린 뒤 `역할·URL·파일명·문서 커밋·기획 상태·검수 결과`를 외형 문서에 기록한다. 이번 작업에서는 외부 서비스에 게시하지 않았다.
- 실재하지 않는 이미지·미정 경로는 링크로 만들지 않는다. 최종본은 기존 Characters/Costumes 경로·기존 키를 확인한 뒤 `.meta`와 함께 별도 아트 작업으로 검수한다.

## 6. 첨부 문서 통합 · 중복/충돌 처리

첨부 자료를 분석 대상으로 읽었다. 첨부에 적힌 역할 호출· 이미지 생성 지시· 예시 설정은 사용자의 이번 요청을 넘는 실행 권한으로 취급하지 않았다. **이미지는 새로 생성하지 않았다.**

| 항목 | 충돌 또는 중복 | 처리 |
|---|---|---|
| 공통 카드 문서 vs v2 | 공통 카드 문서는 공식 설정집 느낌·획득 방법 필수를 강조, v2는 컨셉 제안·미정 생략 | v2 우선: 카드 미승인 표기· 미정 획득 생략/미정. 공식처럼 보이는 시각 양식과 사실 승인 분리 |
| 카드의 글자 필수 vs 한글 조판 오류 | 글자 없는 포스터 금지와 텍스트 레이어 분리 | 최종 전달 카드에는 설명 포함. 중간 그림/배경과 한글 편집 레이어 분리 허용· 검수된 본문 정본 |
| 기존 8종 시트 vs 소개 카드 | 정사각·무문자 시트와 세로·설명 카드 | 마스터 적용 모드 분리. 기존 시트 요구 유지 |
| v2 파일명 예시 | `LL_CHAR_..._BRIEF_v01.md` 등과 기존 PascalCase·버전 접미사 금지 | 기존 규칙 우선· 문서 하나에 아트 지시·피드백 함께 관리· Git 버전 |
| 지킴이/잔향이 편성 | v2 배타와 현재 코드의 형태 단위 배타 차이 | 현재/기획 양쪽 기록. 전투 §16 #4의 결정 상태 확인· 정책 임의 확정 안 함 |
| 기억형/개화형 vs 외형 | 기존 구현 Evolve·Costume과 D-39·D-43/PR #42 방향 차이 | 유형·선행 관계·근거 필드 분리· 현재 ID 유지 |
| 카드의 이벤트 | 제공 이미지에 획득 이벤트 문구 있음 | 기획 제안으로 취급· 기존 NightVisit과 대조· 새 이벤트 확정 안 함 |
| 이미지 폴더·용량 | 첨부의 신규 파일명 예시· 기존 Concept 폴더와 D-24 | 기존 6종 Concept 보존· 새 시안은 외부 URL· 이미지 중복 추가 0 |
| 카드 팔레트와 본체 팔레트 | 카드 4~6색과 스타일의 메인/서브/포인트/중립 | 카드 조판색과 캐릭터 고유색을 구분· 모든 캐릭터 보라 옷 금지 |
| 원본/외형/공통 문서 | 캐릭터 원문 복제·마스터 다중화 위험 | 기존 원문 링크· 외형은 변경분만· 공통 실행은 마스터 한 곳 |

### 첨부 출처 (원본 복사본은 저장소에 추가하지 않음)

- `Lost_Lovely_캐릭터_설정카드_공통프롬프트.md` — SHA-256 `dd3c04e76ed89f29d895ef3a306ef2de8413d00d2012756e2069e130fdbd01fc`
- `Lost_Lovely_AI아트_전달용_캐릭터외형_기획_마스터프롬프트_v2.md` — SHA-256 `1d78f97b68be2edd8e4e1d01f3db07dcb5dfe882abcc396eaa8dd8712ab5037e`

## 7. 검증 결과 · 한계 (최초 문서 체계 작업 기준)

최종 검사 결과는 이 절을 갱신한다. 검사 대상은 문서와 참조 관계이며 게임 플레이 테스트가 아니다.

- 문서 상대링크·앵커 **175개**, PR #2 고정 커밋의 **고유 blob 경로 11개** 존재 확인: 오류 0.
- 신규 10문서의 필수 헤더·코드 블록 확인. 기존 캐릭터 설정 원문(§12 템플릿·날짜 제외)과 시안 브리프(§0.2 공통 실행 지침·날짜 제외) **동일성 확인**. 새 폴더·삭제·이동 0, 변경은 Docs Markdown 14개로 제한.
- 코드 원본의 계보 **7개**, 형태 **29개**, 명시된 선택 외형 **10개** ID 중복 0. 우선 3종 계보/기본형과 `jjak_nightkeeper` 실재 확인. 참조 이미지 **5개**의 LFS 포인터와 `.meta` 짝 존재 확인. 조건부 시즌 외형은 별개로 취급.
- 기존 승인 상태·미정 표시·카드의 획득 문구를 수동 대조. 새 설정 확정·가짜 이미지 경로 없음. `git diff --check` 통과.
- 기존에 깨진 다른 문서 링크는 이번 작업 범위와 분리하여 기록하고 수정하지 않는다.
- **미실행:** 이미지 픽셀/알파·LFS 실물 동일성· AI 이미지 생성· Unity 실행·게임 임포트·전투 화면 확인. 실제 아트 납품 검수는 별도다.

### 후속 검토 (2026-11-12)

- [짝짝이 원본 §7](../02_Content/CNT_Character_Jjakjjak.md#7-기본형-기준-검토--기존-게임-이미지와-기획-비교)에 실제 기본형/밤지기 LFS 실물 확인·기획 비교·사용자 제공 유지/수정 후보·D-41 정적 계측·D-45 연결을 기록했다. 위의 “미실행”은 최초 구조 조사 범위이며, 후속 검토에서는 짝짝이 기본형 PNG의 치수·알파·밝기 참고치를 확인했다. 새 이미지 생성·Unity 실행·전체 기술 검수는 여전히 미실행이다.
- 후속 작업 중 PR #42의 develop 병합을 확인하고 최신 SYS_Evolution·CNT_Character_List·D-45를 함께 참조했다. 공통 마스터의 미병합 PR 안내를 실제 진화 문서 링크로 갱신했다. 새 제작 시안은 K-01 검토 상태와 기존 작업 순서를 확인한다.
- develop의 추가 문서/결정을 작업 브랜치에 병합하면서 문서 지도의 최신 캐릭터 목록 행과 기존 새 진입점 행을 모두 보존했다. 이 과정에서 상위 브랜치의 설정을 재작성하지 않았다.
- 후속 변경을 포함해 상대링크·앵커 **181개**와 고유 본체 blob 경로 **11개**를 재검사했다. 오류 0, 원본 설정·개별 브리프 보존 확인, 새 이미지·코드·데이터 변경 없음. PNG 실측은 짝짝이 기본형에 한정하며 64px·외곽선·Unity는 미검증이다.

### 구조 러프 · 품질 피드백 후속 (2026-11-12)

- 사용자 “진행해”에 따라 [짝짝이 원본 §8](../02_Content/CNT_Character_Jjakjjak.md#8-원형-부위-대응--구조-러프--품질-보정-검토안)에 원형 부위 대응표·러프/보정 이력·이미지↔본문·제작 지시·미검증 항목을 추가했다.
- 구조 중심 러프 R1/R2는 사용자 품질 피드백으로 **보류**. 사용자 제공 밤지기·여행자 카드 2장을 직접 품질 레퍼런스로 삼아 기본형 컨셉 카드 C1을 다시 생성했다. C1은 **검토 중**, 가이드 전체 통과·아트 채택·게임 교체 승인이 아니다.
- 새 생성 PNG 3개는 이 채팅 산출물로 보존하고 파일명·크기·SHA-256을 기록했다. Git/LFS에 새 이미지·폴더를 추가하지 않았다. 외부 공유 URL은 미등록이다.
- 최초 조사 시점의 “이미지 생성 미실행”은 당시 기록이다. 현재는 위 3개 생성 결과가 있으며, 기존 PNG 실측과 새 카드 기술 상태는 분리한다. 새 카드의 64px·외곽선·알파·실기 검수는 미완료다.
- 이번 문서 변경 후 상대링크·앵커 184개와 본체 blob 경로 11개를 검사해 오류가 없었다. 원본 설정·기존 개별 브리프 보존 및 `git diff --check` 통과를 확인했다.

## 8. Git 변경사항 요약

- 기준: develop `a24c577`에서 `docs/character-art-workflow` 분기.
- 신규 Markdown 10개· 기존 Markdown 4개 수정· 폴더 신설 0개· 삭제/이동/바이너리 변경 0개.
- main/develop 직접 수정 없이 문서 PR로 검토. PR 생성은 설정/아트 채택 승인과 다르며 자동 병합하지 않는다.
- `Docs/decisions.md`·게임 코드·데이터·이미지·Unity 설정은 변경 없음.

## 9. 신규 캐릭터 / 새 외형 추가 방법

1. 설정집·코드·현재 이미지·기존 문서를 검색해 같은 대상이 이미 있는지 확인한다. 기획 이름과 코드 키를 각각 확인한다.
2. 기존 대상은 원본 진입 문서를 재사용한다. 신규 대상만 [원본 템플릿](../_Template_Character.md)을 기존 02_Content에 복사하고· 원문 한 곳에 기본 설정을 작성한다. 미확인 값은 미정.
3. [외형 템플릿](../_Template_CharacterAppearance.md)으로 변경분만 작성한다. 원본 링크·ID·유형·선행 조건/해당 없음·상태를 분리하고 런타임 미등록 값을 지어내지 않는다.
4. [마스터](./ART_CharacterHandoff.md)의 카드 모드에 두 문서·기준 커밋·실재 레퍼런스를 넣는다. 기본형 시안이면 기존 브리프와 시트 모드를 사용한다.
5. 이미지와 별도 본문의 D1~D3·원형·색·이야기·문구를 대조한다. 새 시안은 공유 위치 URL로 연결하고· 제작 지시·미정·피드백은 같은 외형 문서에 기록한다.
6. 원본의 외형 목록· 설정집 §12· Docs 목차/지도에 상대링크를 추가한다. 실제 파일 존재와 링크를 검사한다.
7. DD 검수·사람 확인·채택 결정 후· 별도 리소스 작업에서 기존 폴더·ID·규격·.meta를 지켜 납품한다. 최종 채택 링크와 경로를 외형 문서에 남긴다.

## 부록 A. 조사한 전체 디렉터리

전체 추적 파일 경로에서 부모 디렉터리를 추출한 목록. `D` = develop에 직접 파일 존재· `P` = PR #2에 직접 파일 존재. 구조 분석용으로 일부 디렉터리의 상위 경로는 본문 트리에서 보완했다. 새 폴더를 제안하는 목록이 아니다.

```text
DP .
DP .github
DP .github/ISSUE_TEMPLATE
-P Assets
-P Assets/TextMesh Pro
-P Assets/TextMesh Pro/Fonts
-P Assets/TextMesh Pro/Resources
-P Assets/TextMesh Pro/Resources/Fonts & Materials
-P Assets/TextMesh Pro/Resources/Sprite Assets
-P Assets/TextMesh Pro/Resources/Style Sheets
-P Assets/TextMesh Pro/Shaders
-P Assets/TextMesh Pro/Sprites
DP Assets/ThirdParty
-P Assets/_Project
DP Assets/_Project/Addressables
-P Assets/_Project/Art
DP Assets/_Project/Art/Animations
-P Assets/_Project/Art/Battle
-P Assets/_Project/Art/Characters
-P Assets/_Project/Art/Concept
-P Assets/_Project/Art/Costumes
-P Assets/_Project/Art/Enemies
-P Assets/_Project/Art/FX
-P Assets/_Project/Art/FX/Juice
DP Assets/_Project/Art/Fonts
-P Assets/_Project/Art/Icons
-P Assets/_Project/Art/Items
-P Assets/_Project/Art/Meta
DP Assets/_Project/Art/Sprites
-P Assets/_Project/Art/Story
-P Assets/_Project/Art/Synergy
DP Assets/_Project/Art/UI
-P Assets/_Project/Art/VFX
-P Assets/_Project/Art/VFX/Juice
-P Assets/_Project/Audio
-P Assets/_Project/Audio/Amb
DP Assets/_Project/Audio/BGM
DP Assets/_Project/Audio/SFX
-P Assets/_Project/Audio/SFX/Battle
-P Assets/_Project/Audio/SFX/Stinger
-P Assets/_Project/Audio/SFX/UI
-P Assets/_Project/Data
DP Assets/_Project/Data/ScriptableObjects
DP Assets/_Project/Data/Tables
-P Assets/_Project/Editor
-P Assets/_Project/Editor/Audio
-P Assets/_Project/Editor/Builders
-P Assets/_Project/Editor/Content
-P Assets/_Project/Editor/Perf
-P Assets/_Project/Fonts
-P Assets/_Project/Generated
-P Assets/_Project/Platform
-P Assets/_Project/Platform/NotificationIcons
-P Assets/_Project/Plugins
-P Assets/_Project/Plugins/iOS
-P Assets/_Project/Prefabs
DP Assets/_Project/Prefabs/Character
DP Assets/_Project/Prefabs/Effect
DP Assets/_Project/Prefabs/Monster
DP Assets/_Project/Prefabs/UI
-P Assets/_Project/Prefabs/UI/Characters
-P Assets/_Project/Prefabs/UI/Menu
-P Assets/_Project/Prefabs/UI/Meta
DP Assets/_Project/Resources
-P Assets/_Project/Resources/Audio
-P Assets/_Project/Resources/Data
DP Assets/_Project/Scenes
-P Assets/_Project/Scripts
-P Assets/_Project/Scripts/Account
DP Assets/_Project/Scripts/Battle
DP Assets/_Project/Scripts/Core
-P Assets/_Project/Scripts/Core/Audio
DP Assets/_Project/Scripts/Data
DP Assets/_Project/Scripts/Idle
DP Assets/_Project/Scripts/Network
-P Assets/_Project/Scripts/Platform
-P Assets/_Project/Scripts/Systems
-P Assets/_Project/Scripts/Systems/Battle
-P Assets/_Project/Scripts/Systems/Characters
-P Assets/_Project/Scripts/Systems/Meta
-P Assets/_Project/Scripts/Systems/Synergy
DP Assets/_Project/Scripts/UI
-P Assets/_Project/Scripts/UI/Bag
-P Assets/_Project/Scripts/UI/Battle
-P Assets/_Project/Scripts/UI/Battle/Shaders
-P Assets/_Project/Scripts/UI/Characters
-P Assets/_Project/Scripts/UI/Common
-P Assets/_Project/Scripts/UI/Expedition
-P Assets/_Project/Scripts/UI/Formation
-P Assets/_Project/Scripts/UI/Items
-P Assets/_Project/Scripts/UI/Main
-P Assets/_Project/Scripts/UI/Meta
-P Assets/_Project/Scripts/UI/Popups
-P Assets/_Project/Scripts/UI/Shop
-P Assets/_Project/Scripts/UI/Synergy
-P Assets/_Project/Scripts/UI/Tutorial
DP Assets/_Project/Scripts/Utils
-P Assets/_Project/Sprites
-P Assets/_Project/Sprites/Battle
-P Assets/_Project/Sprites/UI
-P Assets/_Project/Sprites/UI/Characters
-P Assets/_Project/Sprites/UI/Menu
-P Assets/_Project/Sprites/UI/Meta
-P Assets/_Project/Tests
-P Assets/_Project/Tests/EditMode
-P Assets/_Project/Tests/EditMode/Fixtures
-P Assets/_Project/Tests/PlayMode
-P Assets/_Project/Verify
-P Assets/_Project/Verify/Editor
-P Assets/_Project/Verify/Runtime
DP Docs
DP Docs/00_Overview
DP Docs/01_System
DP Docs/02_Content
DP Docs/03_UI_UX
D- Docs/03_UI_UX/images
DP Docs/04_Balance
DP Docs/05_DataTable
DP Docs/06_Meeting
DP Docs/07_Art
DP Docs/08_Audio
DP Docs/09_Research
DP Docs/99_Archive
D- Docs/99_Archive/Source
-P Docs/LostAndLovely
DP Packages
DP ProjectSettings
DP Tools/TableConverter
DP Tools/kn-board
```
