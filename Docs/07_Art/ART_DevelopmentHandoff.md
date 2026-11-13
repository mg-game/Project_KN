# ART_DevelopmentHandoff — 개발 연결용 아트 전달

| 항목 | 값 |
|---|---|
| 상태 | Development candidate / Review |
| 작성자 | AI 아트 · 사용자 업로드 요청 |
| 최종 수정 | 2026-11-13 |
| 관련 테이블 | 기존 캐릭터/전투 데이터 변경 없음 |
| 관련 문서 | [파일·GUID·해시 목록](./ART_DevelopmentAssets.json) · [스타일](./ART_StyleGuide.md) · [문서 체계/상세 검수 PR #43](https://github.com/mg-game/Project_KN/pull/43) |

사용자 **“깃 올려 개발할수 있게” / “진행해”** 지시에 따라 현재 검수한 출력물을 개발 브랜치에 등록했다. 기존 D-24의 중간 시안 외부 관리 원칙에 대한 이번 개발 전달 범위의 명시적 사용자 지시이며, 설정·최종 작화 채택·DD/사람 확인·게임 적용 완료를 의미하지 않는다. 기존 이미지/ID/GUID/코드/씬/데이터를 교체하지 않는다.

## 1. 개발 시작

게임 본체 [PR #2](https://github.com/mg-game/Project_KN/pull/2)의 `feature/lost-and-lovely-import` / `4b73a5a33c22d1b6246794c86fdbf7b27db281b0`를 기반으로 한 **`feature/art-dev-handoff`** 브랜치다. 본체가 아직 develop에 병합되지 않아 아트 PR도 본체 브랜치를 대상으로 한다. 브랜치를 받으면 게임 본체와 새 아트 파일을 함께 확인할 수 있다. main/develop/본체 PR을 자동 병합하지 않았다.

```bash
git fetch origin feature/art-dev-handoff
git switch --track origin/feature/art-dev-handoff
git lfs install
git lfs pull
```

이미 로컬 브랜치가 있으면 해당 브랜치로 전환해 갱신한다. [ProjectVersion.txt](../../ProjectSettings/ProjectVersion.txt)의 **Unity 6000.2.6f2**로 저장소 루트를 연다. LFS를 받기 전 PNG가 포인터 텍스트로 보이는 것은 이미지 실물이 없는 상태다.

### 전달 범위

- 신규 PNG30개: **Sprite 후보21개 + 참고 카드/비교9개**. 모든 PNG는 Git LFS.
- 신규 `.meta`30개: 프로젝트 TextureImporter 템플릿 기반으로 새 UUID/GUID·spriteID를 작성했다. 기존 GUID/리소스는 보존했다. 실제 Unity Editor가 생성/임포트 검증한 파일이라고 주장하지 않는다.
- Sprite: Single / Full Rect / PPU100 / 중앙 피벗 / Bilinear / mipmap off / 비압축.768 빈 레이어도 전체 사각형으로 보존한다.
- 참고 카드/비교: 기존 `Art/Concept/` 위치의 Default texture. 게임용 Sprite/데이터/런타임 키로 사용하지 않는다.
- 새 폴더 없음. 같은 그림은 기존 경로를 참조하며 전달 복사본을 중복 등록하지 않는다.

Inspector에서 새 Sprite를 `Image.Source Image` 등에 연결해 개발한다. 씬/프리팹/데이터/레지스트리는 아직 자동 변경하지 않았다. 특히 캐릭터 파일명은 **새 개발용 이름**이며 기존 형태/외형 ID가 아니다.

## 2. 연결 순서와 표시 계약

**레이니 골목등불 본체 수정 (2026-11-13):** 후속 브랜치는 `feature/art-rainy-two-lanterns`이며,PR #71 병합 후의 본체 브랜치를 기준으로 이번 변경3개파일만 분리했다. 아래 공통시작명 대신 이 후속브랜치를 받아도 된다. 사용자 요청으로 이미지편집1회만 수행했다. 중앙등불을 지우고 우산테두리 좌우2개만 유지,불꽃없이 둥근유리/천덮개안의 크림~연한앰버 갇힌빛으로 표현했다. 각등불에 짧은리본1개. 우산/물빛얼굴/넓은촉수/J꼬리/우산살끝받침·천덮개·짧은손목끈고리·기존그림체/색/시점 유지.1024 RGBA·높이922·발963·중심512와64px/128px 딥퍼플 브라우저 읽힘을 확인했다. 같은파일/GUID를 유지했으며 참고카드의3등불 구성보다 최신본체/지시를 우선한다. 불꽃은몽실전용이라는 사용자지시를 반영했다. 외형유형/런타임ID/최종채택·Unity연결은 별도다.

**짝짝이 카라반 본체 수정 (2026-11-13):** 사용자 요청으로 이미지편집1회만 수행했다. 같은 파일/기존 후보GUID에 외부등불을 제거하고 창문3곳에 작은 양말유령을 각1명 넣은 수정본을 반영했다. 얼굴+방2개·보라커프/크림니트/패치/양말팔/실뭉치바퀴·기존시점 유지,빛은 창문내부만/불꽃모양없음.1024 RGBA·높이922·발963·중심512 확인. 기존 참고카드는 수정 전 컨셉이므로 등불/창문 구성은 최신 본체Sprite와 이 지시를 따른다. 참고카드·다른 캐릭터·게임 ID/씬/데이터는 변경하지 않았다.

| 대상 | 개발 시 연결 | 남은 검토 |
|---|---|---|
| 캐릭터3종 | 새 `_art_candidate` Sprite를 테스트용 뷰에 연결.1024×1024,내용높이922,발기준y963,중심512 | 짝짝이 개화B의 기존 그림 교체 여부,레이니/몽실 유형·런타임ID·최종채택.기존 `jjak_caravan`/`udu_base`/`mong_base` 교체 금지 |
| ART-01 성향 | `Icons/behavior_charge/patrol/hold`와 `_dim` 상태 | 흐림알파.40 제안.코드알파를 쓰면 켜짐3장만 직접 참조 가능 |
| ART-02 수선등 | 기존 lamp/button_gold_9s/ui_pip_button 재사용.레벨점5개와비용/MAX는별도 UI | 버튼Border48,등불128표시/점32표시.비활성알파는 제안.게임수급/비용연결별도 |
| ART-03 수선점 | 기존건물→현재레벨overlay1장→기존crack.768캔버스/같은부모·위치·스케일·중앙피벗 유지 | **Normal Alpha**이며 Additive아님. L5는원본5창점등을드러내는완전투명정상파일.자르거나재중앙화금지.외벽창5개범위/점등순서제안 |
| ART-04 링 | 기존 ui_ring_stitch/받침 +새미커버사선/위험▲/압박3줄 | 8별은장식.정확한8방위는N부터시계방향45°앵커.빈슬롯과미커버입력독립.현재5방향코드를8방향으로전환해야함 |
| ART-05 범위 | 아트파일없음.코드/메시/셰이더로가변요격범위 | 기호형사선을영역전체텍스처로간주하지않음.실제커버마스크/클립처리별도 |
| ART-06 예고 | wave_preview_arrow + ART-04 formation_pressure 공유 | 다음스폰3초예고.위쪽PNG를수선점안쪽으로회전.기존DangerArrowView실시간위협도와별도입력.예고끝화살표숨김/압박유지.주기1초/.35~1·압박.45·줄이기고정은제안 |
| ART-07 HUD | 새시계/해골 +기존단추/패널/pill.시간/Wave/HP/적수는별도텍스트/표시 | 잔여20%이하/1초경고·보호튜토리얼시간숨김·HP→남은적전환.원본pill은192×96/위아래48이라센터높이0:이번검토는**Simple2:1(112×56)+숫자**.패널만정상9-slice48 |

Sprite 이미지의 회전은 실제 Unity 좌표/부모 변환에서 확인한다. 브라우저 CSS 각도를 그대로 복사하지 않는다. 수선점 흔들림/스쿼시와 조명 레이어 정렬,HUD 가이드/안전영역,반투명/가림/색 공간을 실제 게임에서 검사한다. 표시 예시의 수치·타이밍/알파/위치 제안은 게임 밸런스·확정 계약과 구분한다.

## 3. 신규 파일

경로·해시·GUID·규격·원본 재사용 대응은 [JSON 목록](./ART_DevelopmentAssets.json)을 정본으로 확인한다.

| 그룹 | 파일 | 용도 |
|---|---|---|
| Characters | [jjak_caravan_art_candidate.png](../../Assets/_Project/Art/Characters/jjak_caravan_art_candidate.png) | Sprite 개발 후보 |
| Characters | [rainy_recycle_art_candidate.png](../../Assets/_Project/Art/Characters/rainy_recycle_art_candidate.png) | Sprite 개발 후보 |
| Characters | [mongsil_wax_art_candidate.png](../../Assets/_Project/Art/Characters/mongsil_wax_art_candidate.png) | Sprite 개발 후보 |
| ART-01 | [behavior_charge.png](../../Assets/_Project/Art/Icons/behavior_charge.png) | Sprite 개발 후보 |
| ART-01 | [behavior_charge_dim.png](../../Assets/_Project/Art/Icons/behavior_charge_dim.png) | Sprite 개발 후보 |
| ART-01 | [behavior_patrol.png](../../Assets/_Project/Art/Icons/behavior_patrol.png) | Sprite 개발 후보 |
| ART-01 | [behavior_patrol_dim.png](../../Assets/_Project/Art/Icons/behavior_patrol_dim.png) | Sprite 개발 후보 |
| ART-01 | [behavior_hold.png](../../Assets/_Project/Art/Icons/behavior_hold.png) | Sprite 개발 후보 |
| ART-01 | [behavior_hold_dim.png](../../Assets/_Project/Art/Icons/behavior_hold_dim.png) | Sprite 개발 후보 |
| ART-03 | [shop_light_l1.png](../../Assets/_Project/Art/Battle/shop_light_l1.png) | Sprite 개발 후보 |
| ART-03 | [shop_light_l2.png](../../Assets/_Project/Art/Battle/shop_light_l2.png) | Sprite 개발 후보 |
| ART-03 | [shop_light_l3.png](../../Assets/_Project/Art/Battle/shop_light_l3.png) | Sprite 개발 후보 |
| ART-03 | [shop_light_l4.png](../../Assets/_Project/Art/Battle/shop_light_l4.png) | Sprite 개발 후보 |
| ART-03 | [shop_light_l5.png](../../Assets/_Project/Art/Battle/shop_light_l5.png) | Sprite 개발 후보 |
| ART-04 | [formation_uncovered.png](../../Assets/_Project/Art/UI/formation_uncovered.png) | Sprite 개발 후보 |
| ART-04 | [formation_uncovered_danger.png](../../Assets/_Project/Art/UI/formation_uncovered_danger.png) | Sprite 개발 후보 |
| ART-04 | [formation_danger.png](../../Assets/_Project/Art/UI/formation_danger.png) | Sprite 개발 후보 |
| ART-04 | [formation_pressure.png](../../Assets/_Project/Art/UI/formation_pressure.png) | Sprite 개발 후보 |
| ART-06 | [wave_preview_arrow.png](../../Assets/_Project/Art/UI/wave_preview_arrow.png) | Sprite 개발 후보 |
| ART-07 | [hud_clock.png](../../Assets/_Project/Art/Icons/hud_clock.png) | Sprite 개발 후보 |
| ART-07 | [hud_wave_boss.png](../../Assets/_Project/Art/Icons/hud_wave_boss.png) | Sprite 개발 후보 |
| Character reference | [jjak_caravan_development_card.png](../../Assets/_Project/Art/Concept/jjak_caravan_development_card.png) | 참고 전용 |
| Character reference | [rainy_recycle_development_card.png](../../Assets/_Project/Art/Concept/rainy_recycle_development_card.png) | 참고 전용 |
| Character reference | [mongsil_wax_development_card.png](../../Assets/_Project/Art/Concept/mongsil_wax_development_card.png) | 참고 전용 |
| UI reference | [art01_behavior_development_review.png](../../Assets/_Project/Art/Concept/art01_behavior_development_review.png) | 참고 전용 |
| UI reference | [art02_lamp_development_review.png](../../Assets/_Project/Art/Concept/art02_lamp_development_review.png) | 참고 전용 |
| UI reference | [art03_shop_development_review.png](../../Assets/_Project/Art/Concept/art03_shop_development_review.png) | 참고 전용 |
| UI reference | [art04_formation_development_review.png](../../Assets/_Project/Art/Concept/art04_formation_development_review.png) | 참고 전용 |
| UI reference | [art06_wave_development_review.png](../../Assets/_Project/Art/Concept/art06_wave_development_review.png) | 참고 전용 |
| UI reference | [art07_hud_development_review.png](../../Assets/_Project/Art/Concept/art07_hud_development_review.png) | 참고 전용 |

## 4. 재사용 경로

| 그룹 | 기존/공유 파일 | 적용 |
|---|---|---|
| ART-02 | [button_gold_9s.png](../../Assets/_Project/Art/UI/button_gold_9s.png) | 기존192 버튼,Border48. 별도 동적 레벨점/문구. |
| ART-02 | [lamp.png](../../Assets/_Project/Art/Icons/lamp.png) | 표시128px/흐림 알파0.40 제안. 원본256 재사용. |
| ART-02 / ART-07 | [ui_pip_button.png](../../Assets/_Project/Art/UI/ui_pip_button.png) | 수선등32px 점/꺼짐알파0.25 제안,HUD일반웨이브32px 단추. 새 PNG 복제 없음. |
| ART-04 | [ui_ring_stitch.png](../../Assets/_Project/Art/UI/ui_ring_stitch.png) | 기존512 봉제8별 링,표시크기 조절. 별은 장식이며45°앵커별도. |
| ART-04 | [guardian_pad.png](../../Assets/_Project/Art/Battle/guardian_pad.png) | 기존 찬 받침 재사용. |
| ART-04 | [guardian_pad_empty.png](../../Assets/_Project/Art/Battle/guardian_pad_empty.png) | 기존 빈 받침 재사용. 빈 슬롯과 미커버 계산 구분. |
| ART-06 | [formation_pressure.png](../../Assets/_Project/Art/UI/formation_pressure.png) | 이번 ART-04와 같은 압박3줄 공유. 알파0.45 제안. |
| ART-06 | [ring_glow.png](../../Assets/_Project/Art/FX/ring_glow.png) | 기존 흰색 알파 링,Lamp 틴트 참고. |
| ART-07 | [panel_night_9s.png](../../Assets/_Project/Art/UI/panel_night_9s.png) | 192×192 기존HUD패널,Border48. |
| ART-07 | [ui_pill_night_9s.png](../../Assets/_Project/Art/UI/ui_pill_night_9s.png) | 192×96,위48+아래48로중앙높이0. 이번 검토는Simple2:1(112×56)+숫자겹침. |
| ART-03 | [shop.png](../../Assets/_Project/Art/Battle/shop.png) | 기존5창점등 건물. 단계별 overlay 1장만겹침. |
| ART-03 | [shop_crack_1.png](../../Assets/_Project/Art/Battle/shop_crack_1.png) | 기존 손상 레이어,단계 조명 위에 겹침. |
| ART-03 | [shop_crack_2.png](../../Assets/_Project/Art/Battle/shop_crack_2.png) | 기존 손상 레이어,단계 조명 위에 겹침. |
| ART-03 | [shop_crack_3.png](../../Assets/_Project/Art/Battle/shop_crack_3.png) | 기존 손상 레이어,단계 조명 위에 겹침. |

## 5. 검수 근거와 개발 체크

원본3종·상세 기획·생성 프롬프트·검토 전후 기록은 [문서 PR #43](https://github.com/mg-game/Project_KN/pull/43)의 `12483c46eb9c676ab94a6a8c5b8eb52857148804` 문서를 참조한다. 이 브랜치는 해당 문서 PR을 자동 병합하거나 정본을 복제하지 않았다. `Concept/*development_review.png`는 브라우저 합성/재생 비교이며 실제 Unity 스크린샷이 아니다. `Concept/*development_card.png`는 설정 참고 카드이며 전투 그림이 아니다.

- [x] 신규 경로 충돌 없음·기존 파일/GUID 보존·새 GUID 중복 없음.
- [x] PNG 규격/해시·캐릭터3종의발기준/내용높이·빈L5 정상·Sprite설정YAML 검사.
- [x] LFS 포인터/바이너리 업로드·새 저장소로 원격 LFS 다운로드 후 해시 대조.
- [ ] Unity 6000.2.6f2 실제 임포트/플레이·Sprite 표시/알파/색 공간·기기 검수.
- [ ] 게임8방위/웨이브/타이머/수선등/HP·상태입력·씬 연결.
- [ ] 최종캐릭터ID/외형유형·DD/사람 확인·아트 최종 채택.

원격 다운로드 검사와 검수 결과는 PR 설명에 기록한다. 이 자료는 개발자가 바로 연결을 시작할 수 있는 전달이며, 게임 코드/씬 구현 완료나 머지 완료로 표시하지 않는다.
