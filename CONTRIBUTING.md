# Project_KN 협업 규칙

> 대상: 기획 / 클라이언트 / 아트 전원. 이 문서가 협업 규칙의 단일 원본(SSOT)이다.

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

| 변경 유형 | 필수 리뷰어 | 병합 조건 |
|---|---|---|
| 코드 | 클라이언트 1인 | 빌드 성공 + 리뷰 승인 |
| 기획 문서 | 리드 기획 1인 | 승인 |
| 데이터 테이블 | 기획 1인 + 클라 1인 | 변환 툴 통과 + 승인 |
| ProjectSettings/Packages | 클라 리드 | 승인 |

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
