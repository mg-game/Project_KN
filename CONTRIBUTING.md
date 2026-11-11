# Project_KN 협업 규칙

> 대상: 팀원 3명 전원. 이 문서가 협업 규칙의 단일 원본(SSOT)이다.
> 규칙의 방향은 [mg-game 10계명](./Docs/00_Overview/Principles.md). 계명이 방향, 아래 규칙이 방법이다.

---

## 0. 역할 = 모자 (이 프로젝트의 기본 방향)

**누구든지 기획 · 개발 · 아트를 한다.** 사람에게 직군을 고정하지 않고, 일을 집을 때 그 일에 맞는 모자를 쓴다.
**관문은 DD 하나.** 사람이 3명이라도 검수 · 머지 · 결정 기록 · AI 지시 배분은 DD 한곳을 거친다.

```
[사람 3명 · 모자를 바꿔 씀] ──요청──→ [DD · 관문] ──개발 지시함──→ [기개발 · 로컬 개발 · AI 아트]
                                        │ 검수 · 머지 · decisions.md
                                        ↓
                                     [CI 자동 확인]

할 일 ──"내가 할게"──→ 진행 ──"검수 요청"──→ 검수(DD) ──"확인 · 완료"(만든 사람 ≠ 누르는 사람)──→ 완료
  ↑                     │  └─"막힘"(이유 + 풀어 줄 사람) → 꼬임 · 막힘 패널     │
  └──── 내려놓기 ────────┘                                     반려 ──→ 진행 ←┘
```

| 규칙 | 내용 | 막으려는 것 |
|---|---|---|
| R1. 집은 사람이 주인 | 팀 보드 작업판에서 "내가 할게"를 누른 사람이 끝까지 책임. 못 하면 "내려놓기". 완료된 작업은 누구든 고친다(계명 5) — 진행 중 카드는 주인에게 한마디 또는 "넘겨받기" 후 | 아무도 안 하는 일, 둘이 같은 일 |
| R2. 모자 표시 | 지금 쓰는 모자(기획/개발/아트)를 팀 보드 내 자리에 표시 | 누가 무슨 관점으로 일하는지 모름 |
| R2-1. 주 모자 = 먼저 보는 분야일 뿐 | 대표 · 김용기 · 백승천은 **같은 위치**이고 같은 일을 한다. 누구든 다른 사람의 일을 **대신 · 이어서 처리**할 수 있다. 주 모자(대표 = 게임디자이너(기획, 아트) · 김용기 = 개발 · 서버 · 백승천 = 기획)는 그 분야를 **먼저 보는** 사람일 뿐, 그 사람을 기다릴 이유는 아니다. R3 확인 · 테스트 · 머지 준비는 **작성자가 아닌 누구든 먼저 잡는 사람**이 한다. 잡을 땐 보드 카드 담당을 자기로 바꾸고 한 줄 남긴다 | 한 사람에게 일이 몰려 다 같이 기다림 |
| R3. DD 검수 + 사람 1명 확인 | PR · 작업은 DD가 검수 · 머지하고, 만든 사람이 아닌 1명이 보드에서 "확인 · 완료" | 혼자 만들고 혼자 통과, 3명이 각자 다른 기준 |
| R4. 결정은 기록 | 기획·수치·아트 채택 결정은 DD가 `Docs/decisions.md`에 1줄 | 3명이 서로 다른 기억으로 작업 |
| R5. 최종 결정권 | 의견이 갈리면 대표가 정한다(결재함) | 끝나지 않는 논쟁 |
| R6. 진행은 1인 2건까지 | 작업판 '진행'에 내 이름 카드 최대 2장 | 벌여 놓고 안 끝나는 일 |
| R7. AI 지시는 DD를 거친다 | 누구나 DD에게 요청 → DD가 지시함으로 기개발 · AI 아트에 배분. 로컬 개발만 각자 PC에서 직접(결과는 PR로 DD 검수) | 3명의 지시가 기개발에서 충돌 |
| R7-1. 코드는 기개발, 실행은 Unity PC | **기개발**(클라우드) = Unity C# 게임 코드 · 에디터 스크립트 · 데이터 · 테스트 코드 · 문서 작성. Unity를 실행하지 못하므로 답에 "Unity 미실행 — 미검증"을 적는다. **로컬 개발 / 사람**(Unity가 깔린 PC) = PR 브랜치를 받아 컴파일 · EditMode/PlayMode 테스트 · 씬 생성 · 폰트 굽기 · 빌드 → 결과를 PR에 첨부(D-21). **김용기 PC가 주로 돌리지만 대표 · 백승천 PC도 같은 환경을 갖춰, 먼저 잡는 사람이 테스트한다** | 아무도 실행해 보지 않은 코드가 머지됨 · 한 PC에 몰림 |
| R8. 만지는 영역 적기 | 카드에 씬 · 프리팹 · 문서 경로를 적는다. 같은 영역의 진행 카드가 2장이면 보드가 "영역 겹침"을 띄움 → 먼저 끝낼 사람을 정한다 | 같은 씬 · 문서 동시 수정 |
| R9. 막히면 바로 "막힘" | 이유 + 풀어 줄 사람을 적는다. 그 사람이 지금 공을 받는다. 풀리면 "풀렸어요" | 혼자 붙잡고 며칠 멈춤 |
| R10. 3일 무변화 = 신호 | 진행 카드가 3일 바뀌지 않으면 "멈춘 진행", 검수 1일 대기면 "검수 대기"로 뜸 | 조용히 썩는 작업 |

- 팀 보드(작업판 · 메모 · 내 상태): https://claude.ai/artifact/Apsp44mxNMroEmtNF8EksE
- 결정 기록: [`Docs/decisions.md`](./Docs/decisions.md)

---

## 1. 최초 세팅 (1회)

```
1. Git LFS 설치            → git lfs install
2. 저장소 클론              → git clone <repo-url>
3. LFS 파일 수신 확인       → git lfs pull
4. Unity Smart Merge 등록   → 아래 1-1 참고
5. Unity Hub → Add project from disk → 저장소 루트 선택
   (최초 1회 Unity가 ProjectSettings/Packages 생성 → 담당자 1인만 커밋)
```

### 1-1. UnityYAMLMerge 등록 (`.git/config` 또는 `~/.gitconfig`)

```ini
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '<UnityEditorPath>/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A
    recursive = binary
```
- Windows 예: `C:/Program Files/Unity/Hub/Editor/<버전>/Editor/Data/Tools/UnityYAMLMerge.exe`
- macOS 예: `/Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents/Tools/UnityYAMLMerge`

### 1-2. Unity Editor 필수 설정 (Project Settings)

| 항목 | 값 | 이유 |
|---|---|---|
| Editor → Version Control → Mode | `Visible Meta Files` | .meta 누락 시 참조(GUID) 깨짐 |
| Editor → Asset Serialization → Mode | `Force Text` | Diff/Merge 가능 |
| Unity 버전 | `ProjectSettings/ProjectVersion.txt` 고정 | 버전 혼용 시 대량 재직렬화 diff 발생 |

---

## 2. 브랜치 전략

```
main ───────────●──────────────●────────── (릴리즈/마일스톤 빌드만)
                 ↑              ↑
develop ──●──●──●──●──●──●──●──●────────── (통합 브랜치, 항상 빌드 가능 상태)
           ↑     ↑        ↑
feature/*  ┘     │        │   (기능 단위, develop에서 분기 → PR → develop)
docs/*  ─────────┘        │   (기획 문서 전용)
fix/*   ──────────────────┘   (버그 수정)
```

| 브랜치 | 네이밍 | 분기 원본 | 병합 대상 | 비고 |
|---|---|---|---|---|
| main | `main` | - | - | 직접 push 금지 |
| develop | `develop` | main | main | 직접 push 금지, PR만 |
| 기능 | `feature/<시스템>-<요약>` | develop | develop | 예: `feature/idle-offline-reward` |
| 기획 | `docs/<분류>-<요약>` | develop | develop | 예: `docs/system-gacha` |
| 수정 | `fix/<이슈번호>-<요약>` | develop | develop | 예: `fix/42-stage-clear-crash` |
| 데이터 | `data/<테이블>-<요약>` | develop | develop | 수치 밸런스 단독 변경 시 |

---

## 3. 커밋 메시지 규칙

```
<type>(<scope>): <요약 50자 이내>

<본문: 무엇을/왜 (선택)>
```

| type | 용도 |
|---|---|
| feat | 기능 추가 |
| fix | 버그 수정 |
| docs | 기획 문서 |
| data | 데이터 테이블/수치 변경 |
| art | 아트 리소스 추가/교체 |
| refactor | 동작 변화 없는 구조 개선 |
| chore | 설정, 빌드, 패키지 |

예: `data(stage): 1~30 스테이지 몬스터 HP 곡선 1.12 → 1.10 완화`

---

## 4. 충돌 방지 규칙 (Unity 특화)

1. **씬(.unity) 동시 수정 금지** → 작업 전 이슈/채널에 "점유" 선언, 작업 후 해제.
2. **씬 대신 프리팹으로 작업** → 씬에는 프리팹 인스턴스만 배치, 수정은 프리팹 단위.
3. **.meta 파일 반드시 함께 커밋** → 에셋만 있고 .meta 없는 PR은 리젝.
4. **에셋 이동/이름 변경은 Unity 에디터 안에서만** → 탐색기에서 옮기면 GUID 분리.
5. **ProjectSettings / Packages 변경은 별도 PR** → 리뷰어 1인 필수.
6. **LFS 바이너리(PSD, 사운드)는 머지 불가** → 동시 작업 금지, 최종본만 커밋. LFS는 무료 한도(저장 10GB · 다운로드 월 10GB)만 쓴다 — **시안 · 중간본은 올리지 말고 Notion/보드에**, 최종본만 (D-24)
7. **빌더 생성물(`Main.unity` · UI 프리팹)은 손으로 고치지 않는다** → 수정은 `Editor/Builders/` 코드에서. 다시 만들 때(`IdleAutoSetup.SceneVersion` 올림)만 **1명이 점유** 선언 → 다시 생성 → 생성물 + 자동 생성 표식을 한 커밋으로. 생성물 충돌은 병합하지 말고 다시 생성 (D-17)
8. **한글 TMP 폰트는 Static 아틀라스** → 플레이 중 폰트 에셋이 바뀌면 커밋하지 말고 되돌린다 (D-18)

---

## 5. PR / 리뷰 규칙

> 직군 리뷰어는 없다. **DD가 검수 · 머지**하고, **만든 사람이 아닌 1명**이 확인한다(R3). 모자는 확인 관점을 정할 뿐이다.

| 변경 유형 | 검수 · 머지 | 사람 확인 (작성자 외 1명) | 병합 조건 |
|---|---|---|---|
| 코드 | DD | 개발 모자: 동작 · 구조 · 성능 | CI 통과(당분간 로컬 EditMode 결과 첨부, D-21) + DD 승인 + 확인. 기개발 PR은 **Unity PC에서 받아 테스트한 결과**가 붙어야 확인 가능(R7-1) |
| 기획 문서 | DD | 기획 모자: 논리 · 예외 · 어뷰징 | DD 승인 + 확인 + `Docs/decisions.md` 기록 |
| 데이터 테이블 | DD | 기획 + 개발 모자: 수치 근거 · 스키마 | 변환 툴 통과 + DD 승인 + 확인 |
| 아트 리소스 | DD | 아트 모자: 규격 · 톤 | DD 승인 + 확인 (채택 결정은 결재함) |
| ProjectSettings/Packages | DD | 대표 | 대표 승인 |

- **기능 PR에는 기획서 갱신을 같이 넣는다** (D-25). 해당 `Docs/01_System/SYS_*` 등의 "① 현재 구현"을 고치거나, 없으면 DD가 기개발에 작성을 지시한 뒤 머지
- 머지 방식: **Squash merge** (feature → develop), **Merge commit** (develop → main)
- 머지 후 브랜치 삭제

---

## 6. 폴더 구조

```
Project_KN/
├─ Assets/
│  ├─ _Project/            ← 우리 프로젝트 에셋 전부 (최상단 정렬용 '_')
│  │  ├─ Scripts/          Core, Data, Battle, Idle, UI, Network, Utils
│  │  ├─ Scenes/
│  │  ├─ Prefabs/          Character, Monster, UI, Effect
│  │  ├─ Art/              Sprites, Animations, UI, Fonts
│  │  ├─ Audio/            BGM, SFX
│  │  ├─ Data/             Tables(변환된 JSON/CSV), ScriptableObjects
│  │  ├─ Resources/        (최소 사용, 원칙적으로 Addressables)
│  │  └─ Addressables/
│  └─ ThirdParty/          ← 외부 에셋/SDK (수정 금지, 업데이트 시 통째 교체)
├─ Docs/                   ← 기획 문서 (Docs/README.md 참고)
├─ Tools/                  ← 테이블 변환 등 파이프라인 툴
├─ .gitattributes          ← LFS / SmartMerge 설정
└─ CONTRIBUTING.md
```
