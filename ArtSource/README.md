# ArtSource — 게임 밖 아트 원본 (D-71)

Unity가 읽지 않는 폴더(`Assets` 밖)다. 게임 빌드에 들어가지 않는다.

| 폴더 | 무엇 | 이름 규칙 |
|---|---|---|
| `Sheets/` | 캐릭터 기본형 시트 · 외형 카드 같은 **시안** (D-24의 Notion 대신) | `<문서 원물 키>_sheet_v<N>.png` 예) `boots_sheet_v1.png` |

- PNG는 `.gitattributes`에 따라 LFS로 저장된다.
- 같은 파일 덮어쓰기 대신 `v2`, `v3`로 새 파일 (검수 기록과 해시가 맞도록). 채택본이 정해지면 오래된 버전은 DD가 정리 PR로 지운다.
- 게임에 쓰는 최종 PNG는 여기가 아니라 `Assets/_Project/Art/...` (D-65).
