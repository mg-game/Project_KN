# ART_Character_Brief — 대표 캐릭터 8종 유실형 시안 브리프

| 항목 | 값 |
|---|---|
| 상태 | Review |
| 작성자 | DD |
| 최종 수정 | 2026-11-13 (#43 공통 실행 지침 연결 병합) |
| 관련 테이블 | - |
| 관련 문서 | [`CNT_Character_Main`](../02_Content/CNT_Character_Main.md) (설정 정본) · [`ART_StyleGuide`](./ART_StyleGuide.md) (스타일 정본) · [`ART_M1_Backlog`](./ART_M1_Backlog.md) |

> D-35 #3: M1은 L&L 현재 아트로 진행하고, **아트 가이드 기준 신규 아트는 M1 중 시안만** 만든다. 이 문서는 그 시안 의뢰서다.
> 범위: 대표 8종의 **1단계 유실형만** (기억형 · 개화형은 시안 채택 후). 산출물은 Notion 아트 페이지로 (Git에는 채택된 최종본만, D-24).
> ⚠ 이름은 설정집 표기. 코드 캐릭터 이름과의 대응은 **I-7 대조표 후 확정** — 아래 "현재 L&L 아트" 칸은 원물로 추정한 [가정]이다.


> **진행 순서 (D-46 #5, 대표 · AI 아트)**: ① 짝짝이 기본형 기준 정리 (유지할 특징 / 수정 가능한 부분) → ② 재활용 외형 1종 검증 — 양말 카라반(개화 B): "양말의 어느 부분 → 어떤 구조 → 새 쓰임" → ③ 컨셉 카드 · 텍스트 대조 → ④ 검증된 방식을 레이니 · 몽실에 적용. 고정 조건 = `ART_BattleSpriteSpec` §2.1 · §2.2 + `SYS_Evolution` §2.0 (기억형 = 장식 레이어 자리).
## 0. 공통 규칙

### 0.1 산출물 (캐릭터 1명당 1장, 캐릭터 시트)
1. 전신 3/4 정면 1개 (1024 × 1024, 단색 연보라 배경)
2. 같은 포즈 **검은 실루엣** 1개 — 실루엣만으로 원물이 읽히는지 확인용 (가이드 §3.2)
3. **64px 축소** 미리보기 1개 — 전투 화면 가독성 확인용 (가이드 §8)
4. 표정 3개 (기본 · 웃음 · 당황) 얼굴만

### 0.2 공통 프롬프트

공통 실행 지침은 [ART_CharacterHandoff §8 A](./ART_CharacterHandoff.md#8-복사용-실행-프롬프트)에서 관리한다. **스타일 가이드 §13 영문 문장 + 마스터의 기본형 시트 지시 + 아래 캐릭터별 추가 프롬프트** 순서로 조합한다. 이 의뢰는 §0.1의 **기본형 시트 모드**이며, 세로 외형 소개 카드 모드를 적용하지 않는다. 전신·검은 실루엣·64px·표정 3종·글자 없음 조건을 유지한다.

외형변경 기획의 카드·별도 텍스트·전달 흐름은 같은 마스터의 카드 모드를 사용한다. 기존 캐릭터별 브리프 및 M1 범위는 그대로다.

### 0.3 체형 분산 (가이드 §4 비중 35 / 45 / 20)
| 유형 | 캐릭터 |
|---|---|
| 완전 비인간 마스코트형 | 짝짝이 · 몽실 · 푸딩 경 · 웰리 · 피피 · 칙폭이 · 글로비 |
| 준인간 이형형 | 키키 · 레이니 · 엘리제 · 네임리스 00 · 보틀리 · 아홉시 |
| 미형 인간형 | 루루팝 |

### 0.4 검수 체크 (가이드 §15에서 시안 단계에 보는 것)
- [ ] 64px에서도 누군지 구분된다
- [ ] 검은 실루엣만으로 원물이 짐작된다
- [ ] 원물이 장식이 아니라 몸 구조 · 움직임이다
- [ ] 귀엽지만 이상한 점 하나가 있다
- [ ] 8명이 체형 · 색 · 실루엣으로 서로 다르다
- [ ] 금지 요소 없음 (가이드 §14: 미형 인간 + 소품 · 반실사 · 장식 과밀 · 공포 · 고어)

## 1. 캐릭터별 브리프

| 캐릭터 | 원물 | 핵심 실루엣 · 꼭 남길 것 | 이상한 점 (한 문장) | 색 (메인 / 서브 / 포인트) | 전투 역할 → 형태 언어 | 현재 L&L 아트 [가정] |
|---|---|---|---|---|---|---|
| **짝짝이** | 짝 잃은 양말 한 짝 | 양말 몸통 · 발꿈치 얼굴(단추눈 · 꿰맨 입) · 짝짝이 양말 팔다리 · 작은 양말 유령 | 한 발로 깡충거리며 만나는 양말을 다 가족이라 부른다 | 크림 / 파스텔 멀티 / 보라 | 물량 · 교란 → 몸 주변 작은 동료 | `Characters/jjak_*` 있음 — 비교용 |
| **몽실** | 다 녹은 생일 초 · 촛농 | 촛불 두 개 = 귀 · 흘러내리는 촛농 몸 · 금속 촛대 다리 | 자기 생일을 몰라 매일 남의 생일에 간다 | 아이보리 / 라벤더 / 촛불 골드 | 힐러 → 원형 · 대칭 · 빛 | `Characters/mong_*` |
| **푸딩 경** | 유통기한 하루 지난 푸딩 | 탱글한 푸딩 몸 · 캐러멜 투구 · 접시 방패 · 은수저 창 | 말랑하다는 말을 모욕으로 듣는 기사 | 커스터드 옐로 / 캐러멜 브라운 / 실버 | 저비용 탱커 → 낮고 넓은 덩어리 | 없음 |
| **키키** | 어느 문을 여는지 잊은 열쇠 | 머리~꼬리가 **하나의 열쇠** · 열쇠 홈 척추 · 자물쇠 관절 · **문짝 방패** | 아무 문에나 자기를 꽂아 보고, 안 열리면 적으로 본다 | 크림 골드 / 보라 / 청록 | 탱커 → 크고 낮은 중심 · 방패 구조 | 없음 |
| **레이니** | 주인을 기다린 투명 우산 | 우산 = 머리 · 상체, **우산살 = 갈비뼈**, 고인 빗물 = 얼굴, 손잡이 = 척추 꼬리, 해파리 촉수 다리 | 비가 안 오는 날엔 자기가 쓸모없다고 생각한다 | 투명 블루 / 네이비 / 라벤더 | 보호 · 둔화 → 부유형 · 원형 | `Characters/udu_*` (우산?) |
| **엘리제** | 태엽 망가진 발레리나 오르골 | 금 간 도자기 상체 · **회전판 하체** · 등의 태엽 · 악보 띠 치마 · 걷지 않고 미끄러짐 | 마지막 음을 연주하면 사라질까 봐 같은 곡만 반복한다 | 아이보리 / 먼지보라 / 브론즈 골드 | 광역 제어 → 큰 후방 장치 · 원형 | `Characters/lala_*` (오르골?) |
| **네임리스 00** | 이름이 지워진 명찰 | 몸 중심의 빈 명찰 · 별자리 얼굴 · 명찰 끈 팔다리 · 등에 빈 명찰 날개 | 만나는 사람의 이름과 말투를 빌려 쓴다 | 무채색 / 별빛 블루 / 홀로그램 | 복제 · 변칙 → 반투명 · 가변 | 없음 |
| **루루팝** | 유행 지나 버려진 립스틱 | **립스틱 케이스 하체에서 회전하며 올라오는 상체** · 녹은 립스틱 머리 · 큰 입술 · 검은 백조 날개 | 공격할수록 몸이 닳아 짧아진다 | 진홍 / 블랙 / 로즈 골드 | 광역 딜 → 앞으로 기운 실루엣 | `Characters/rouge_*` (립스틱?) |
| **웰리** [가정] · 2장 | 작아져서 버려진 노란 장화 | 장화 한 짝 = 몸 · 앞코 위 눈 · 입 · **입구에서 뒤집혀 나온 남색 안감 머리** · 옆 고리 팔 1개 · 밑창으로 통통 튐 | 웅덩이만 보면 일단 뛰어든다 | 레몬 옐로 / 남색 / 코랄 | 돌격 → 앞으로 기운 · 앞코가 진행 방향 | 없음 |
| **보틀리** [가정] · 2장 | 도착하지 못한 유리병 편지 | 둥근 간유리 병 몸 · 코르크 마개 모자 · 병 목의 얼굴 · 안의 말린 편지 + 갇힌 빛 · 편지 끈 팔 2개 + 봉랍 도장 · 오뚝이 흔들 이동 | 자기 편지를 한 번도 열어 본 적이 없다 | 바다유리 연초록 / 크림 / 봉랍 빨강 | 포획 → 둥글고 낮은 무게중심 · 끈 팔 | 없음 |
| **피피** [가정] · 4장 | 현관문 외시경 (문에 달린 작은 렌즈) | 둥근 황동 테두리 + 볼록 렌즈 몸 · **렌즈 = 큰 눈 하나** · 뚜껑 눈꺼풀 · 나사 꼬리 · 문짝 조각 받침 | 모든 문을 들여다봐야 잠이 온다 | 황동 / 짙은 남색 / 렌즈 하늘빛 | 저격 → 작은 몸 · 한 점(눈)이 크게 | 없음 |
| **칙폭이** [가정] · 5장 | 태엽 장난감 기관차 | 장난감 기관차 몸 · 보일러 얼굴(헤드라이트 눈) · **등의 큰 태엽 열쇠** · 굴뚝 김 · 빈 화물칸 | 태엽이 풀릴까 봐 멈추지 못한다 | 토마토 빨강 / 크림 / 놋쇠 | 돌격 → 앞으로 낮고 긴 · 진행 방향 뚜렷 | 없음 |
| **아홉시** [가정] · 5장 | 9시에 멈춘 회중시계 | 열린 회중시계 몸 · **시곗바늘 = 눈썹 표정 (9시)** · 시곗줄 팔 · 꼬리 고리 · 뚜껑 망토 | 누가 시각을 물으면 언제나 9시라고 답한다 | 은회색 / 바랜 금 / 자정 파랑 | 지원 → 원형 · 대칭 · 둥근 몸 | 없음 |
| **글로비** [가정] · 6장 | 스노우볼 | 유리 돔 몸 + 나무 받침 · 돔 안 눈 마을 · 돔 앞 얼굴 · 옆 태엽 손잡이 팔 | 놀라면 몸 안이 눈보라가 된다 | 눈 흰색 / 겨울 하늘 파랑 / 나무 받침 갈색 | 방어 → 넓고 둥근 · 낮은 무게중심 | 없음 |

### 캐릭터별 추가 프롬프트 (0.2 뒤에 붙임)
```
[짝짝이] A mismatched lone sock creature: the sock itself is the body, a face with button eyes and a stitched mouth on the sock heel, arms and legs made of different colored patterned socks, hopping on one foot, tiny sock ghosts floating around. Cream, pastel multicolor and purple. Cheerful, loud, clingy.
[몽실] A melted birthday candle bunny: two long lit candles form the ears, the whole body is dripping translucent candle wax, a small metal candlestick base as legs with tiny crystal ornaments. Ivory, lavender and warm candle gold. Warm, optimistic, glowing softly.
[푸딩 경] A one-day-expired custard pudding knight: wobbly jiggly pudding body, hardened caramel sauce shaped like a knight helmet crest, a dessert plate as a shield and a silver spoon as a lance. Custard yellow, caramel brown, silver. Proud, overly polite, squishy.
[키키] A key dragon: from head to tail the body is one single large key silhouette, the spine is the key shaft, the tail is the key bit, limbs made of small keys and padlock joints, tiny doors and keyholes on the body, holding an actual small wooden door as a shield. Cream gold, purple, teal. Earnest, principled. Not a dragon decorated with keys — the key itself became a dragon.
[레이니] A transparent umbrella jellyfish spirit: the umbrella canopy is the head and upper body, the umbrella ribs look like ribs and skeleton, rainwater pooled inside forms the face, the handle continues as spine and tail, flowing water and fabric tentacles instead of legs. Clear blue, navy, lavender. Quiet, gentle, waiting. Not a girl holding an umbrella.
[엘리제] A broken ballerina music-box doll: cracked porcelain ballerina upper body, the lower body is a rotating music-box turntable with small organ pipes, a big wind-up key on the back, sheet-music ribbons and metal pipes spreading like a skirt, gliding instead of walking. Ivory, dusty purple, bronze gold. Quiet, elegant, melancholic.
[네임리스 00] A faceless name-tag child: an old name tag with an erased name at the center of the body, the face is empty with a faint star constellation, arms and legs of lanyard straps and clear film, blank name tags hanging from the back like wings, slightly translucent. Achromatic grey, starlight blue, holographic accents. Shy, imitative.
[루루팝] An out-of-fashion lipstick opera diva: the lower body is a lipstick case and the upper body rises out of it with a twist, hair of melted lipstick texture, a simple face with big vivid lips, black swan wing arms and cape. Crimson, black, rose gold. Confident, theatrical. The lipstick's twist-up structure must read before any gothic dress.
[웰리] A small outgrown yellow rain boot creature: the single rubber boot itself is the whole body, round eyes and a grin on the upper toe cap, the navy boot lining flips out of the opening like a tuft of hair, one side pull-loop works as a little waving hand, no legs - it hops on its sole, a drop of water and mud in the tread. Lemon yellow, navy, coral. Impatient, loud, fearless, jumps into every puddle. Not a person wearing boots.
[보틀리] A message-in-a-bottle creature: a round frosted sea-glass bottle is the body (frosted and nearly opaque, thick outline, not clear glass), a cork stopper as a little hat, a shy small face on the bottle neck, a rolled letter visible inside with a soft contained glow under it (no flame shape), the letter's string comes out as two arms ending in a red wax seal, a rounded bottom that wobbles like a roly-poly toy, dried water-line stripes. Pale sea-glass green, cream paper, wax-seal red. Shy, slow, careful, never finishes a sentence. Not a person holding a bottle.
[피피] A door peephole lens creature: a round brass rim with a big convex lens is the whole body, and the lens itself is one huge cute eye whose pupil rolls inside it, the small brass peephole cover works as an eyelid that blinks, a short threaded screw tail behind, no legs - it floats and drags a small piece of wooden door as a shield and stand. Brass, deep navy, sky-blue lens shine. Curious, suspicious, cannot keep a secret. Not a person holding a magnifying glass, not scary.
[칙폭이] A wind-up toy steam locomotive creature: the tin toy train itself is the whole body, a face on the round boiler front with two headlight eyes and a grin, a big wind-up key on its back, a small white puff cloud from the chimney (no fire), four wheels, pulling one tiny empty freight car behind. Tomato red, cream, brass. Eager, brave, always shouting 'departure!'. Not a person in a conductor hat, no real railway logos.
[아홉시] A stopped pocket watch creature: an open silver pocket watch is the head and body, the watch face is the face and the two clock hands act like expressive eyebrows (stopped at nine o'clock, a gentle sad look), the watch chain becomes arms and a tail with a ring at the end, the open lid flips back like a little cape. Silver grey, faded gold, midnight blue. Calm, precise, quietly sad. Not a person wearing a watch, thick clock hands readable at small size.
[글로비] A snow globe creature: the glass dome is the head and body, frosted and nearly opaque with a thick outline (not clear glass), inside it a tiny snowy village scene, and a cute face drawn on the dome front, a short wooden base instead of legs, a small music-box wind-up handle on the side moves like one little arm, snowflakes swirl inside when it is surprised. Snow white, winter sky blue, warm wooden brown. Gentle, nostalgic, stubborn. Not a person holding a snow globe.
```

## 2. 진행
| 단계 | 누가 | 산출 |
|---|---|---|
| 1. 시안 1차 (8종) | AI 아트 (대표가 프롬프트 전달) | 캐릭터 시트 8장 → Notion |
| 2. 검수 | DD | §0.4 체크 · 현재 L&L 아트(있는 5종)와 나란히 비교표 |
| 3. 방향 결정 | 대표 | 캐릭터별 (채택 / 수정 1회 / 현재 L&L 아트 유지) |
| 4. 기억형 · 개화형 브리프 | DD | 채택된 캐릭터만, M1 이후 |

> ⚠ 현재 L&L 아트가 있는 5종(짝짝이 · 몽실 · 레이니 · 엘리제 · 루루팝으로 추정)은 **M1 게임 화면에는 계속 현재 아트**를 쓴다 (D-35). 시안은 M2 교체 판단 자료.
