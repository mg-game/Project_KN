# kn-board — 팀 보드 갱신용 로컬 서버

웹 팀 보드(https://claude.ai/artifact/Apsp44mxNMroEmtNF8EksE)의 **"갱신" 버튼**과 **10~12시 자동 갱신**이 이 서버를 부른다.
대표 PC에 로그인된 GitHub 권한으로 PR · 브랜치 · 커밋 · 이슈 · CI 상태를 **읽기만** 한다. GitHub나 보드에 직접 쓰지 않는다.

```
보드 "갱신" / 자동(10~12시, 1시간마다)
  → Claude 데스크톱 앱 → kn-board(github_snapshot) → GitHub 읽기
  → 보드가 board/github에 저장 → 모두의 화면에 반영
```

| 조건 | 내용 |
|---|---|
| 어디서 | **대표 PC + Claude 데스크톱 앱**으로 보드를 열었을 때만. 브라우저 · 폰에서는 "갱신" 버튼이 숨고 "최신화 복사"만 보인다 |
| 누가 | 지금은 보드 소유자(대표)만 로컬 서버를 쓸 수 있다. 대표가 갱신한 결과는 모두에게 보인다 |
| 보조 | PC가 꺼져 있어도 매일 09:47 예약 작업("KN 팀 보드 아침 정리")이 결재함 · 상태를 정리한다 |

---

## 설치 (대표 PC · Windows 기준, 1회)

### 1. 준비물
- **Node.js 18 이상**: https://nodejs.org (LTS) → 설치 후 PowerShell에서 `node --version`
- **GitHub CLI**: https://cli.github.com → 설치 후
  ```powershell
  gh auth login
  ```
  (GitHub.com → HTTPS → 브라우저로 로그인. mg-game/Project_KN을 볼 수 있는 계정)

### 2. 점검
저장소를 받아 둔 폴더에서:
```powershell
node Tools\kn-board\server.mjs --check
```
`OK · mg-game/Project_KN · PR n(열림 n) · ...`가 나오면 성공.

### 3. Claude 데스크톱 앱에 등록
Claude 데스크톱 앱 → 설정 → 개발자 → **구성 편집**(`%APPDATA%\Claude\claude_desktop_config.json`)을 열고 `mcpServers`에 추가:

```json
{
  "mcpServers": {
    "kn-board": {
      "command": "node",
      "args": ["C:\\경로\\Project_KN\\Tools\\kn-board\\server.mjs"]
    }
  }
}
```
- 이름은 반드시 **`kn-board`** (보드가 `host:kn-board`로 부른다).
- 경로는 실제 저장소 위치로. 역슬래시는 `\\` 두 번.
- 이미 다른 서버가 있으면 `mcpServers` 안에 `"kn-board": {...}`만 추가.
- 저장 후 **Claude 앱을 완전히 종료했다가 다시 실행**.

### 4. 보드에서 첫 허용
1. Claude 데스크톱 앱 안에서 팀 보드를 연다.
2. 위쪽 **"갱신"** 버튼 → 허용 창이 뜨면 **허용**.
3. "GitHub에서 새로 읽었어요"가 뜨면 끝. 이후 보드를 열어 두면 10~12시에 1시간마다 알아서 갱신된다.

---

## 문제 해결

| 증상 | 해결 |
|---|---|
| 보드에 "갱신" 버튼이 안 보임 | 데스크톱 앱이 아님 / 3단계 이름이 `kn-board`가 아님 / 앱 재시작 안 함 |
| "kn-board 서버에 닿지 않아요" | 앱 설정 → 개발자에서 kn-board 상태 확인. 경로 · node 설치 확인 |
| "gh(GitHub CLI)를 찾지 못했어요" | gh 설치 후 앱 재시작. 또는 `"env": {"GH_PATH": "C:\\Program Files\\GitHub CLI\\gh.exe"}` 추가 |
| `gh api ... 실패: HTTP 401/404` | `gh auth login` 다시. 404면 그 계정이 저장소를 볼 권한이 없음 |

### 선택 설정 (`"env": {...}`에 넣음)
| 변수 | 기본값 | 용도 |
|---|---|---|
| `KN_OWNER` · `KN_REPO` | `mg-game` · `Project_KN` | 다른 저장소를 볼 때 |
| `GITHUB_TOKEN` | 없음 | gh 대신 토큰으로 읽을 때 (읽기 권한만 있는 토큰 권장) |
| `GH_PATH` | `gh` | gh 실행 파일 위치가 PATH에 없을 때 |

자동 시간대(10~12시 · 60분)는 보드 데이터 `board/now.auto`(`from` · `to` · `every_min`)에서 바꾼다. 서버는 고칠 필요 없다.
