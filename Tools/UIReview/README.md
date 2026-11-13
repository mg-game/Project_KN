# Project_KN UI 개발 전달본 — Review 08 / 0.8.1

홈·전투·편성·성장·결과의 브라우저 코드, 입력 흐름, 화면 규격과 필수 이미지를 함께 보관합니다. **채팅 ZIP 없이 이 브랜치만 받아 실행할 수 있습니다.** Unity 게임 코드와 분리된 개발 참조이며, 최종 아트 채택이나 실제 전투·저장 구현은 아닙니다.

## 받기 · 실행

Git LFS와 Node.js 20.19 이상이 필요합니다. 기존 저장소에서:

```sh
git fetch origin fix/72-ui-stage-context
git switch --track origin/fix/72-ui-stage-context
git lfs install --local
git lfs pull --include="Tools/UIReview/**"
cd Tools/UIReview
npm start
```

이미 로컬 브랜치가 있으면 `git switch fix/72-ui-stage-context`을 사용합니다. 미리보기 실행 자체는 Node 내장 서버만 사용하므로 `npm ci`가 필요 없습니다.

- 화면 조작: <http://127.0.0.1:8772/>
- 화면 규격·상태·리소스 대응표: <http://127.0.0.1:8772/handoff.html>
- 직접 진입: `/?view=formation`, `/?view=growth`, `/?view=results`
- 검토 보드: [UI_Review_Board.png](UI_Review_Board.png)

기본 서버는 로컬 컴퓨터에서만 접속할 수 있습니다. `PORT` 환경 변수로 포트를 바꿀 수 있습니다. 파일을 직접 여는 대신 HTTP 서버를 사용합니다. 외부 CDN·API·폰트 다운로드는 없습니다.

## 개발 정본과 검토본의 차이

개발 정본은 [KN_M1_DevBrief](../../Docs/00_Overview/KN_M1_DevBrief.md)입니다. 초기 전달 PR #72는 develop에 병합되었습니다. 이번 구역 연결 보완은 `develop@8f281cf` 위의 후속 수정 브랜치입니다. 문서 전달 PR #59도 통합된 상태입니다. 08의 화면·수치 예시는 `755a13a`, 기존 그림은 PR #2 `4b73a5a`를 기준으로 제작한 것이므로 최신 결정이 우선합니다.

- **보름달은 잔향 수급 +30% (D-58)**입니다. D-50의 +20% 메모와 과거 검토 기록은 개발 수치로 사용하지 않습니다. 현재 `asset-manifest.json`, `manifest.js`, 출력 스크립트와 ART 목록도 +30%로 맞췄습니다. 아이콘 자체에는 수치 글자가 없습니다.
- 강화 비용·능력치·편성 배지·전투 결과는 고정 예시입니다. JS의 지갑/레벨을 실제 게임 경제·세이브로 옮기지 말고 실제 시스템 데이터를 바인딩합니다.
- 프리셋은 실제 개발에서 지킴이 방위와 잔향이 덱을 함께 저장해야 합니다(I-10a). 검토본의 프리셋은 방위 배치만 비교하는 예시입니다.
- 이름·세이브 키와 개발 착수 순서는 DevBrief를 따릅니다. 본 전달은 PR #2 R3, DD 검수, I-10 의존 관계를 해제하지 않습니다.

## 어디부터 볼지

| 파일 | 개발에서 참고할 내용 |
|---|---|
| `review.js`, `review.css` | 홈·전투·편성·실패의 기본 화면 구성 |
| `interactions.js`, `interactions.css` | 탭/홀드 조준·포인터 취소·편성 이동·프리셋·일시정지 |
| `growth.js`, `growth.css` | 공유 레벨·부분 구매·홀드·공명 확인·입력 차단 |
| `results.js`, `results.css` | 결과 6상태·자동 정산 표시·1.5초/최대 6초 확인·중복 이동 차단 |
| `readability.css` | 주요 보조 문구·상태 안내·색상 받침 |
| `ScreenHandoff.json` | 8개 화면 × 2비율의 측정 좌표·고정/스크롤 영역 |
| `UnityImportPlan.json` | 후보 48종의 대상 경로·해시·피벗·9-slice **미적용 제안** |
| `UIInteractionSpec.json`, `GrowthUIReviewSpec.json`, `ResultsUIReviewSpec.json`, `FlowReviewSpec.json` | 상태·입력·예시 모델의 범위와 예외 |
| `reference/manifest.json` | 기존 리소스 44개의 원본 경로·커밋·SHA-256 |
| [ART_UIHandoff](../../Docs/07_Art/ART_UIHandoff.md) | 제작·검수 이력과 남은 검수 |

화면 좌표는 왼쪽 위 원점의 390×845 / 390×693 논리 참조입니다. Unity의 Safe Area·RectTransform 확정값이 아닙니다. 전장 방위는 N=0°, 시계 방향 45°씩입니다.

## 구역 연결 보완 (0.8.1)

결과에서 다음 전투로 이동한 뒤 AUTO·배속·비율 변경으로 화면을 다시 그려도 현재 구역을 유지합니다. 편성·일시정지·실패/재도전·성장 복귀는 같은 구역 표시를 사용합니다. 1장 장소명은 `CNT_Stage_Chapter1` §2.3을 참조하고, 정의되지 않은 2장 이후 장소는 `스테이지 N`으로만 표시합니다.

강화 시트 위 클리어/실패 알림은 현재 구역의 결과로 연결됩니다. 예를 들어 1-5에서 클리어하면 예시 단추 97과 다음 1-6을, 실패하면 진행도 60% 예시 단추 17과 같은 1-5 재도전을 표시합니다. 상세/비율 재렌더로 보상을 다시 정산하지 않습니다. 외부 결과 상태 선택은 1-4 또는 보스 1-10의 고정 예시를 새로 시작하며, '결과 다시 보기'는 그 결과의 구역을 유지합니다.

기대 화면: [홈 1-5](stage-home-5.png) · [편성 1-5](stage-formation-5.png) · [일시정지 1-5](stage-pause-5.png). 전달 시점 검사 기록은 [stage-verification.json](stage-verification.json)입니다.

이는 **화면 연결용 상태**입니다. 실제 스테이지 전투·위험 방향·웨이브·권장 전투력·인벤토리·장 해금/저장은 기존 검토용 예시이며 구현하지 않았습니다. 실제 게임 데이터 연결에서는 DevBrief와 기획 정본을 따릅니다. 검증 파일은 `verify-stage.cjs`, 최신 결과는 `.review-output/stage-verification.json`입니다.

## 이미지 · Unity 연결

이미지는 저장소의 기존 `.gitattributes`에 따라 **Git LFS**로 보관합니다. 사용자의 개발 전달 지시에 따라 현재 08 묶음만 올렸으며, 과거 ZIP/중간 버전은 올리지 않습니다. 전체 후보를 게임에 채택했다는 의미는 아닙니다.

- 런타임 후보: `candidates/` 47종 PNG/SVG + `lighting/shop_dark_base.png` 1장.
- 기존 참조: `reference/` 44장. PR #2 파일과 같은 내용이며 원본을 변경하지 않습니다.
- `concepts/`·`lighting/masks/`·`lighting/previews/`: 점등 재출력과 정합 검수에 필요한 입력/참조입니다.
- 채택한 것만 `UnityImportPlan.json`의 대상 경로로 가져오고 **Unity가 생성한 `.meta`**를 함께 관리합니다. 이 도구는 `.meta`나 GUID를 임의 생성하지 않습니다.
- 게임의 기존 `shop.png` 교체는 별도 채택 대상입니다. 보정된 수선점은 검토용입니다.
- 점등은 같은 768² 기본 위에 **현재 단계 레이어 하나**를 일반 알파로 합성합니다. Lv.3에서 L1+L2+L3을 중첩하지 않습니다.
- 전투/편성은 I-10f, 성장은 I-13, 알림은 I-15에서 실제 데이터·상태·입력 시스템과 연결합니다. 현재 C#·씬·프리팹은 수정하지 않았습니다.

## 검증

```sh
npm ci
npx playwright install chromium
npm run check:assets
npm test
```

Linux에서 브라우저 시스템 라이브러리가 없다면 Playwright의 플랫폼별 설치 안내를 따릅니다. 기존 Chromium을 쓰려면 `KN_UI_BROWSER_PATH`에 실행 파일 경로를 지정합니다. 테스트는 임시 포트의 서버를 자동으로 열고 종료합니다. 검사 화면·보고서는 무시되는 `.review-output/`에 기록하므로 추적된 그림을 덮어쓰지 않습니다.

8개 브라우저 검사 묶음은 68개 통과 항목(입력 취소 8항목·구역 연결 6항목 포함)을 재현합니다. 관리형 브라우저의 `file://` 정책 차단 기록 1건은 별도로 구분합니다. 선택 색상 6쌍의 대비 검수는 전체 접근성 인증이 아니며, `blur` 이벤트 검사는 실제 단말 앱 전환 검사가 아닙니다.

출력 재생성은 의도적으로 추적 파일을 바꾸는 저작 작업입니다:

```sh
npm run build:assets
npm run build:handoff
```

두 명령은 먼저 `npm start`로 기본 서버를 열고 실행합니다(`build:assets`는 서버 불필요). 다른 서버를 쓰는 도구 호출에는 `KN_UI_REVIEW_URL`을 지정할 수 있습니다. 그림/캡처 변경은 검수 후에만 커밋합니다.

Unity 6000.2.6f2 실행, TMP·압축·안전 영역·실기기 터치/성능, 실제 전투/정산/세이브 연결은 남아 있습니다. 기존 검토본의 자세한 기록은 [REVIEW_08_NOTES.md](REVIEW_08_NOTES.md)에 보존했습니다.
