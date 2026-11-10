# Project_KN 협업 규칙

> 대상: 팀원 3명 전원. 이 문서가 협업 규칙의 단일 원본(SSOT)이다.

---

## 0. 역할 = 모자 (이 프로젝트의 기본 방향)

**누구든지 기획 · 개발 · 아트를 한다.** 사람에게 직군을 고정하지 않고, 일을 집을 때 그 일에 맞는 모자를 쓴다.

```
할 일(누구나 집을 수 있음) ──"내가 할게"──→ 진행(집은 사람 = 주인, 모자 표시)
      ↑                                        │
      └──── 내려놓기 ────────────────────────────┤
                                               ↓ PR 생성
                                    리뷰(만든 사람 ≠ 리뷰어) ──승인──→ 완료
```

| 규칙 | 내용 | 막으려는 것 |
|---|---|---|
| R1. 집은 사람이 주인 | 팀 보드 작업판에서 "내가 할게"를 누른 사람이 끝까지 책임. 못 하면 "내려놓기" | 아무도 안 하는 일, 둘이 같은 일 |
| R2. 모자 표시 | 지금 쓰는 모자(기획/개발/아트)를 팀 보드 내 자리에 표시 | 누가 무슨 관점으로 일하는지 모름 |
| R3. 만든 사람 ≠ 머지하는 사람 | 자기 PR은 다른 1명이 리뷰·머지. 모자와 무관 | 혼자 만들고 혼자 통과 |
| R4. 결정은 기록 | 기획·수치·아트 채택 결정은 `Docs/decisions.md`에 1줄 | 3명이 서로 다른 기억으로 작업 |
| R5. 최종 결정권 | 의견이 갈리면 대표가 정한다(결재함) | 끝나지 않는 논쟁 |
| R6. 진행은 1인 2건까지 | 작업판 '진행'에 내 이름 카드 최대 2장 | 벌여 놓고 안 끝나는 일 |
| R7. AI 팀원은 누구나 지시 | DD · 기개발 · 로컬 개발 · AI 아트에게는 누구든 지시할 수 있고, 결과물은 지시한 사람이 주인 | AI 결과물을 아무도 검수하지 않음 |

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
6. **LFS 바이너리(PSD, 사운드)는 머지 불가** → 동시 작업 금지, 최종본만 커밋.

---

## 5. PR / 리뷰 규칙

> 직군 리뷰어가 따로 없다. 리뷰어는 **만든 사람이 아닌 1명**(R3). 모자는 리뷰 관점을 정할 뿐이다.

| 변경 유형 | 리뷰어 | 리뷰 관점 | 병합 조건 |
|---|---|---|---|
| 코드 | 작성자 외 1명 | 개발 모자: 동작 · 구조 · 성능 | 빌드 성공 + 승인 |
| 기획 문서 | 작성자 외 1명 | 기획 모자: 논리 · 예외 · 어뷰징 | 승인 + `Docs/decisions.md` 기록 |
| 데이터 테이블 | 작성자 외 1명 | 기획 + 개발 모자: 수치 근거 · 스키마 | 변환 툴 통과 + 승인 |
| 아트 리소스 | 작성자 외 1명 | 아트 모자: 규격 · 톤 | 승인 (채택 결정은 결재함) |
| ProjectSettings/Packages | 대표 | 전체 영향 | 대표 승인 |

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
