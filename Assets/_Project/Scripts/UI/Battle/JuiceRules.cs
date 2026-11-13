using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 손맛 곡선 (FX-JUICE §3.3 / §3.6). 입력은 0~1로 자른다 (SpringDisplacement·ImpactSize는 초 단위). 순수 함수, 할당 없음.
    /// </summary>
    public static class JuiceEase
    {
        public static float OutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        public static float InQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        public static float OutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        public static float OutQuart(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t * t;
        }

        public static float OutExpo(float t)
        {
            t = Mathf.Clamp01(t);
            return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        }

        /// <summary>뒤로 살짝 물러났다가 들어간다 (먼지 처치 몸이 쏙 줄어들 때)</summary>
        public static float InBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t);
            return t * t * ((overshoot + 1f) * t - overshoot);
        }

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.5f) return 4f * t * t * t;
            float u = -2f * t + 2f;
            return 1f - u * u * u * 0.5f;
        }

        /// <summary>
        /// 감쇠 용수철 변위 e^(−ζωt)·cos(ω_d·t), ω = 2π·hz, ω_d = ω·√(1 − ζ²). t = 0에서 1, 한 번 반대로 되튄 뒤 잦아든다
        /// (hz 5, ζ 0.45 → 112ms에 약 −0.2, 250ms쯤 멈춤). 눌림 X = 1 + aX·f, Y = 1 − aY·f
        /// </summary>
        public static float SpringDisplacement(float t, float hz, float zeta)
        {
            if (t <= 0f) return 1f;
            float omega = 2f * Mathf.PI * Mathf.Max(0.01f, hz);
            float z = Mathf.Clamp(zeta, 0f, 0.99f);
            float damped = omega * Mathf.Sqrt(1f - z * z);
            return Mathf.Exp(-z * omega * t) * Mathf.Cos(damped * t);
        }

        /// <summary>
        /// 명중 그림 크기 곡선 (SizeCurve.Impact): age &lt; hold → size0 (= 자리 크기 × FirstFrame, 가장 크다) ·
        /// hold ~ hold+settle → size0에서 size1로 OutCubic · 그 뒤 끝까지 size1 → size1 × endScale (endScale &lt; 1이면 InQuad로 줄고, 1 이상이면 OutQuart로 부푼다)
        /// </summary>
        public static float ImpactSize(float age, float life, float hold, float settle, float size0, float size1, float endScale)
        {
            if (age < hold) return size0;
            if (age < hold + settle) return Mathf.LerpUnclamped(size0, size1, OutCubic((age - hold) / Mathf.Max(0.0001f, settle)));
            float u = Mathf.Clamp01((age - hold - settle) / Mathf.Max(0.001f, life - hold - settle));
            float end = size1 * endScale;
            return endScale < 1f ? Mathf.LerpUnclamped(size1, end, InQuad(u)) : Mathf.LerpUnclamped(size1, end, OutQuart(u));
        }
    }

    /// <summary>손맛 색 어휘 (§1.0 / §2.3). 금색은 치명타·불 밝히기·보스 보상에만, 빨강은 수선점 피해와 위험에만</summary>
    public static class JuiceColors
    {
        /// <summary>코어 별 크림 #FFF8E8 (명중 순간의 하얀 불빛)</summary>
        public static readonly Color CoreCream = Hex(0xFFF8E8);
        /// <summary>잉크 받침 #2D2458 (파스텔 그림이 라벤더 광장 위에서 읽히게 하는 어두운 주머니)</summary>
        public static readonly Color Ink = Hex(0x2D2458);
        /// <summary>전장 어둡게 #1E1640 (검정이 아닌 남보라)</summary>
        public static readonly Color Dim = Hex(0x1E1640);
        public static readonly Color Cream = UITheme.Cream;
        public static readonly Color Lamp = UITheme.Lamp;
        public static readonly Color Gold = UITheme.Gold;
        public static readonly Color Lavender = UITheme.Lavender;
        public static readonly Color DustyPink = UITheme.DustyPink;
        public static readonly Color SoftBlue = UITheme.SoftBlue;
        public static readonly Color Danger = UITheme.Danger;
        public static readonly Color Mint = Hex(0x8FE3B0);
        public static readonly Color EchoGlow = UITheme.EchoGlow;
        /// <summary>인쇄 어긋남(동화책 색판 밀림) 두 장: 분홍은 −x, 하늘은 +x</summary>
        public static readonly Color MisprintPink = UITheme.DustyPink;
        public static readonly Color MisprintBlue = UITheme.SoftBlue;

        /// <summary>밝기 (감마 값 그대로 Rec.709 가중치) — 0.6을 넘으면 빛 번쩍임(스크린), 아니면 틴트</summary>
        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>강조색을 크림 쪽으로 (코어·빛줄기 색: lerp(accent, CoreCream, toCream))</summary>
        public static Color TowardCream(Color accent, float toCream) => Color.Lerp(accent, CoreCream, toCream);

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    /// <summary>화면 배치 (전장 로컬 px)</summary>
    public static class JuiceLayout
    {
        /// <summary>
        /// 보이는 띠 위 끝에서 전투 HUD 윗줄이 덮는 높이 — SafeTop = FieldTop − TopHud (숫자 천장 · 큰 터짐 · 연속 처치 알림이 이 아래).
        /// 새 화면 틀(UX-SPEC-IA §1.1 · §3.2, 1080x1920)에서 다시 잼:
        ///   · 상단 바 0~130, 전투(StageArea) 130~1750, Field = Stretch(0, 0, 0, 156) → 전장 창 위 끝 = 화면 y 130.
        ///     FieldTop = mapper.VisibleTop은 MAIN(올림 0)이든 SHEET(올림 360)든 늘 이 창 위 끝 → SafeTop = 화면 y 130 + TopHud (두 상태 같은 값).
        ///   · HUD 윗줄 (StageArea 로컬 = 위 끝에서 잰 거리, 빌더 그대로): 배속 · AUTO (−156/−14, −14) 66 높이 → 14~80 (잠금 덮개 포함 화면 144~220),
        ///     추천 전투력 알약 (352, −18) → 화면 155~207 (뿌리 142~222, SHEET에서는 사라짐), 스테이지 카드 (14, −12) 134 높이 → 12~146 (화면 142~276, 왼쪽 x ≤ 344).
        ///   · 전투가 이 높이까지 오는 곳은 SHEET의 위쪽 레인 하나 (가운데 열 x 324~755 — 알약 말고는 HUD가 없다). MAIN 전투 내용은 화면 y ≥ 362 (§3.5),
        ///     왼쪽 열(스테이지 카드 아래 끝 276)은 어느 상태든 전투가 닿지 않는다 (SHEET 왼쪽 위 레인 ≥ 450).
        ///   → 가운데·오른쪽 윗줄 아래 끝 222 + 틈 28 = 화면 250 → TopHud = 250 − 130 = 120 (IA-11 'SafeTop 화면 y ≥ 250'의 바닥값; 예전 틀과 같은 StageArea 로컬 값).
        ///     더 키우면 SHEET에서 막 나타난 위쪽 레인 적(발 화면 y 194)의 숫자가 몸 아래로 더 밀린다.
        /// </summary>
        public const float TopHud = 120f;
        public const float SideMargin = 90f;
        /// <summary>
        /// 연속 처치 · 한꺼번에 처치 알림 자리: (−FieldHalfWidth + 740, SafeTop − 50) = 화면 (740, 300) — SafeTop이 MAIN이든 SHEET든 화면 250이라 두 상태 같은 자리.
        /// 예전 (+220, −70) = 화면 (220, 320)은 84px 판(y 278~362 · x 69~371)이 시너지 줄(x 16~540 · y 284~348)을 덮고 농장 보스 도전 버튼(y 356~424)에 6px 걸쳤다 (§3.2 새 틀).
        /// 8글자 이하 판('5연속 수선!' · '10연속 반짝!' · '한꺼번에 N!', 폭 120 + 26 × 글자 ≤ 328)은 x 576~904 · y 258~342 —
        /// 시너지 줄(x ≤ 540) · 방치 바구니(x ≥ 906) · 스테이지 클리어 띠(y ≥ 372) · 끌어 겨누기 안내 · 배속/AUTO(y ≤ 220)를 모두 비킨다
        /// (9~10글자 '20연속 대수선!' · '40연속 눈부셔요!'는 바구니에 11~24px 닿는다 — Wave 3부터 BattleFx.CalloutAnchor가 실제 HUD 칸을 읽어 왼쪽으로 비킨다.
        /// 떠 있는 알림이 그 자리에서 더 큰 단계로 바뀌어 판이 넓어질 때도 UpgradeCallout이 자리를 다시 재 0.12초에 미끄러져 옮긴다).
        /// 숫자는 떠 있는 판 자리를 그대로 읽어 비킨다 (CalloutFloor)
        /// </summary>
        public static readonly Vector2 CalloutOffset = new(740f, 50f);
    }

    /// <summary>
    /// 전장 위 겹침 비키기 (Wave 3 WP-5B — 순수 함수, 할당 없음). 상자 = 가운데 + 반크기 (전장 로컬 px, 가운데 원점, 위로 +).
    /// 적 이름표·HP 바 묶음, 지킴이·잔향이 말풍선, 피해 숫자가 HUD 칸(스테이지 카드 · 배속/AUTO · 추천 알약 · 바구니 · 빠른 줄)이나 배너 · 보스 이름표
    /// (말풍선은 수선점 이름표 · 보스 몸도)에
    /// 가리지 않게, 허락한 방향(아래 · 위 · 옆) 중 가장 조금 옮긴다. 자리는 BattleFx.KeepOut이 한 프레임에 한 번 잰다.
    /// </summary>
    public static class BattleKeepOut
    {
        /// <summary>허락하는 방향 (moves 비트)</summary>
        public const int Down = 1;
        public const int Up = 2;
        public const int Side = 4;
        public const int Any = Down | Up | Side;

        /// <summary>상자(가운데 c, 반크기 h)가 사각형 r을 사방 gap만큼 넓힌 자리와 겹치는지</summary>
        public static bool Overlaps(Vector2 c, Vector2 h, Rect r, float gap) =>
            c.x + h.x > r.xMin - gap && c.x - h.x < r.xMax + gap && c.y + h.y > r.yMin - gap && c.y - h.y < r.yMax + gap;

        /// <summary>
        /// 겹치면 허락한 방향(moves) 중 가장 조금 옮긴 가운데를 돌려준다 (겹치지 않으면 그대로).
        /// 옆은 옮긴 가운데 x가 ±xLimit 안일 때만, 위·아래는 옮긴 가운데 y가 [yMin, yMax] 안일 때만. 갈 곳이 없으면 그대로 — 부르는 쪽이 Overlaps로 다시 잰다
        /// </summary>
        public static Vector2 PushOut(Vector2 c, Vector2 h, Rect r, float gap, int moves, float xLimit, float yMin, float yMax)
        {
            if (!Overlaps(c, h, r, gap)) return c;
            float best = float.MaxValue;
            var moved = c;
            if ((moves & Down) != 0)
            {
                float y = r.yMin - gap - h.y;
                if (y >= yMin && c.y - y < best)
                {
                    best = c.y - y;
                    moved = new Vector2(c.x, y);
                }
            }
            if ((moves & Up) != 0)
            {
                float y = r.yMax + gap + h.y;
                if (y <= yMax && y - c.y < best)
                {
                    best = y - c.y;
                    moved = new Vector2(c.x, y);
                }
            }
            if ((moves & Side) != 0)
            {
                float right = r.xMax + gap + h.x;
                if (right <= xLimit && right - c.x < best)
                {
                    best = right - c.x;
                    moved = new Vector2(right, c.y);
                }
                float left = r.xMin - gap - h.x;
                if (left >= -xLimit && c.x - left < best) moved = new Vector2(left, c.y);
            }
            return moved;
        }

        /// <summary>
        /// 말풍선 하나를 수선점 이름표('수선점' 알약) 밖으로 (Wave 3 r1 — SHEET에서 줄어든 전장의 앞줄 친구 말풍선이 이름표를 덮었다).
        /// 상자(가운데 c, 반크기 h)가 이름표 plate를 gap만큼 넓힌 자리와 겹치면 위나 옆 중 가장 조금 옮긴다 — 옮김은 maxMove(말풍선 띄움 한도 2 × 56px)까지만
        /// (아래로 밀면 꼬리가 말하는 친구 얼굴을 덮고, 더 멀리 옮기면 누가 말하는지 읽히지 않는다). 옆은 가운데 ±xLimit, 위는 가운데 ≤ yMax 안.
        /// 갈 곳이 없으면 그대로 두고, 이름표를 정말 덮으면(틈 없이 겹침) blocked — 부르는 쪽이 새 말풍선은 띄우지 않고 떠 있던 것은 일찍 사라지게 한다.
        /// 순수 함수, 할당 없음
        /// </summary>
        public static Vector2 BubblePlateClear(Vector2 c, Vector2 h, Rect plate, float gap, float maxMove, float xLimit, float yMax, out bool blocked)
        {
            blocked = false;
            if (!Overlaps(c, h, plate, gap)) return c;
            var moved = PushOut(c, h, plate, gap, Up | Side, xLimit, float.MinValue, yMax);
            if (moved != c && (moved - c).sqrMagnitude <= maxMove * maxMove) return moved;
            blocked = Overlaps(c, h, plate, 0f);
            return c;
        }

        /// <summary>
        /// 말풍선 하나를 보스 자리(keep = 이름표 묶음 + 몸 — BattleFx.PlateBodyKeepOut) 밖으로 (Wave 3 r2 — SHEET에서 스테이지 카드 바로 밑의 이름표에서
        /// 아래로 밀린 말풍선이 보스 머리와 몸을 덮었다). 상자(가운데 c, 반크기 h)가 keep을 gap만큼 넓힌 자리와 겹치면:
        ///   1. 위나 옆 중 가장 조금 (위는 가운데 ≤ yMax, 옆은 가운데 ±xLimit 안),
        ///   2. 그래도 안 되면 아래 — 몸 아래 끝 밑 (가운데 ≥ yMin).
        /// 옮김은 maxMove(말풍선 띄움 한도 2 × 56px)까지만 (더 멀리 옮기면 누가 말하는지 읽히지 않는다), 옮긴 자리가 avoid[0..avoidCount)
        /// (HUD 칸 — 말풍선보다 위에 그려진다)를 gap만큼 넓힌 자리에 들어가면 그 자리는 버린다.
        /// 갈 곳이 없으면 그대로 두고, 보스 자리를 정말 덮으면(틈 없이 겹침) blocked — 부르는 쪽이 새 말풍선은 띄우지 않고 떠 있던 것은 일찍 사라지게 한다.
        /// 순수 함수, 할당 없음
        /// </summary>
        public static Vector2 BubbleBossClear(Vector2 c, Vector2 h, Rect keep, float gap, float maxMove, float xLimit, float yMin, float yMax,
            Rect[] avoid, int avoidCount, out bool blocked)
        {
            blocked = false;
            if (!Overlaps(c, h, keep, gap)) return c;
            avoidCount = avoid != null ? Mathf.Min(avoidCount, avoid.Length) : 0;
            var best = c;
            float bestMove = float.MaxValue;
            float up = keep.yMax + gap + h.y;
            float right = keep.xMax + gap + h.x;
            float left = keep.xMin - gap - h.x;
            ConsiderMove(c, new Vector2(c.x, up), up <= yMax, h, gap, maxMove, avoid, avoidCount, ref best, ref bestMove);
            ConsiderMove(c, new Vector2(right, c.y), right <= xLimit, h, gap, maxMove, avoid, avoidCount, ref best, ref bestMove);
            ConsiderMove(c, new Vector2(left, c.y), left >= -xLimit, h, gap, maxMove, avoid, avoidCount, ref best, ref bestMove);
            if (bestMove < float.MaxValue) return best;
            float down = keep.yMin - gap - h.y;
            ConsiderMove(c, new Vector2(c.x, down), down >= yMin, h, gap, maxMove, avoid, avoidCount, ref best, ref bestMove);
            if (bestMove < float.MaxValue) return best;
            blocked = Overlaps(c, h, keep, 0f);
            return c;
        }

        /// <summary>적 이름표 · HP 바 묶음과 화면 양옆 사이 최소 여백 (px — HUD 여백과 같다, StatusClear)</summary>
        public const float StatusSideMargin = 12f;

        /// <summary>BubbleBossClear의 후보 하나: 범위 안(inside) · maxMove 안 · 지금까지보다 가깝고 · avoid 칸(gap 포함)에 들지 않으면 고른다</summary>
        static void ConsiderMove(Vector2 c, Vector2 to, bool inside, Vector2 h, float gap, float maxMove, Rect[] avoid, int avoidCount, ref Vector2 best,
            ref float bestMove)
        {
            if (!inside) return;
            float move = (to - c).magnitude;
            if (move > maxMove || move >= bestMove) return;
            for (int i = 0; i < avoidCount; i++)
            {
                if (Overlaps(to, h, avoid[i], gap)) return;
            }
            best = to;
            bestMove = move;
        }

        /// <summary>두 사각형을 덮는 가장 작은 사각형 (b는 사방 pad만큼 넓혀서). 순수 함수</summary>
        public static Rect Union(Rect a, Rect b, float pad) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin - pad), Mathf.Min(a.yMin, b.yMin - pad), Mathf.Max(a.xMax, b.xMax + pad), Mathf.Max(a.yMax, b.yMax + pad));

        /// <summary>
        /// 적 이름표 · HP 바 묶음 자리 (BattleFx.KeepStatusClear — Wave 2 r2 battle 항목): 가운데 center · 반크기 half를 보이는 띠 위 끝(top) − gap 아래로 자르고,
        /// HUD 칸 rects[0..count)를 두 번 훑어 아래나 옆(전장 ±halfWidth − 6 안)으로 가장 조금 옮긴다. 위로는 옮기지 않는다 (HUD는 모두 위쪽).
        /// 바닥(bottom = 보이는 띠 아래 끝) 밑으로는 가지 않는다. 위를 자른 뒤 양옆으로도 화면 끝에서 StatusSideMargin(12px) 안쪽에 둔다
        /// (Wave 3 r3 → RM-2: 전장 끝을 걷는 보스의 이름표 묶음이 화면 테두리에 붙었다 — HUD 여백과 같은 12px)
        /// </summary>
        public static Vector2 StatusClear(Vector2 center, Vector2 half, Rect[] rects, int count, float top, float bottom, float halfWidth, float gap)
        {
            float maxY = top - gap - half.y;
            float minY = bottom + half.y;
            if (center.y > maxY) center.y = maxY;
            float xLimit = Mathf.Max(0f, halfWidth - StatusSideMargin - half.x);
            center.x = Mathf.Clamp(center.x, -xLimit, xLimit);
            if (rects == null) return center;
            count = Mathf.Min(count, rects.Length);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++) center = PushOut(center, half, rects[i], gap, Down | Side, xLimit, minY, maxY);
            }
            return center;
        }

        /// <summary>
        /// 피해 숫자 하나를 보스 이름표 묶음(plate — 이름표 + HP 바) 밖으로 (Wave 3 — 숫자 레이어가 위에 그려져 이름표 · HP 바를 덮지 않게).
        /// 숫자 상자 = 뜬 자리 position부터 떠오른 끝(+ rise)까지 · 흐름(driftX) 포함, 반크기 half. 겹치지 않으면 그대로.
        ///   · 묶음 밑에서 뜬 숫자: 떠오름을 묶음 아래 끝 − gap까지 줄인다 (최소 minRise).
        ///   · 묶음 높이에서 뜬 숫자 — 그 보스가 맞은 숫자(own, 치명타 머리 위 쾅 등): 옆으로 밀면 맞은 몸에서 떨어지므로 묶음 위로
        ///     (위 끝 + gap + 반높이 — 뜬 자리가 ceiling 아래이고 minRise만큼은 roof 아래로 떠오를 수 있을 때, 떠오름은 roof까지), 자리가 없으면 묶음 바로 밑
        ///     (minRise만 떠오름 — 몸 위에 겹쳐도 그 보스 위에 남는다).
        ///   · 다른 적의 숫자: 묶음 옆 (가까운 쪽, 가운데 ±limit 안).
        /// rise는 바꾼 떠오름. 돌려주는 값 = 새 뜬 자리. 순수 함수, 할당 없음
        /// </summary>
        public static Vector2 NumberPlateClear(Vector2 position, ref float rise, float driftX, Vector2 half, Rect plate, bool own, float gap, float minRise,
            float ceiling, float roof, float limit)
        {
            float x0 = Mathf.Min(position.x, position.x + driftX) - half.x;
            float x1 = Mathf.Max(position.x, position.x + driftX) + half.x;
            if (x1 <= plate.xMin - gap || x0 >= plate.xMax + gap) return position;
            if (position.y + rise + half.y <= plate.yMin - gap || position.y - half.y >= plate.yMax + gap) return position;
            float under = plate.yMin - gap - half.y;
            if (position.y + minRise <= under)
            {
                rise = Mathf.Max(minRise, Mathf.Min(rise, under - position.y));
                return position;
            }
            if (own)
            {
                float above = plate.yMax + gap + half.y;
                if (above <= ceiling && above + minRise <= roof)
                {
                    rise = Mathf.Max(minRise, Mathf.Min(rise, roof - above));
                    return new Vector2(position.x, above);
                }
                rise = minRise;
                return new Vector2(position.x, Mathf.Min(position.y, under - minRise));
            }
            float right = plate.xMax + gap + half.x - Mathf.Min(0f, driftX);
            float left = plate.xMin - gap - half.x - Mathf.Max(0f, driftX);
            bool canRight = right <= limit;
            bool canLeft = left >= -limit;
            if (canRight && (!canLeft || right - position.x <= position.x - left)) position.x = right;
            else if (canLeft) position.x = left;
            return position;
        }

        /// <summary>
        /// 말풍선 겹침 띄움 (UX-SPEC §2.1.8 M41 — 오래된 것부터): 발밑 foot · 폭 width · 높이 height인 말풍선이 앞서 놓인 말풍선(others[i] = 발밑 x, 발밑 y, 폭)과
        /// 겹치면(가로 두 반폭 + gap · 세로 높이 + gap 안) 가장 높은 겹친 말풍선의 위 끝 + gap까지 올린다 — 최대 steps번, 합 maxLift(2 × 56px)까지.
        /// 그래도 겹치거나 발밑이 천장(ceiling)을 넘으면 blocked (부르는 쪽이 일찍 사라지게 한다). 돌려주는 값 = 띄울 높이 (0 이상)
        /// </summary>
        public static float BubbleLift(Vector2 foot, float width, float height, Vector3[] others, int count, float gap, int steps, float maxLift,
            float ceiling, out bool blocked)
        {
            blocked = false;
            float lift = 0f;
            for (int step = 0; ; step++)
            {
                float y = foot.y + lift;
                float top = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    var o = others[i];
                    if (Mathf.Abs(foot.x - o.x) >= (width + o.z) * 0.5f + gap) continue;
                    if (Mathf.Abs(y - o.y) >= height + gap) continue;
                    top = Mathf.Max(top, o.y + height);
                }
                if (top == float.MinValue) break;
                if (step >= steps)
                {
                    blocked = true;
                    break;
                }
                lift = top + gap - foot.y;
                if (lift > maxLift)
                {
                    lift = maxLift;
                    blocked = true;
                    break;
                }
            }
            if (foot.y + lift > ceiling)
            {
                lift = Mathf.Max(0f, ceiling - foot.y);
                blocked = true;
            }
            return lift;
        }
    }

    /// <summary>
    /// 품질·설정별 층 문 (§2.5 설정 표). 모두 순수 함수 — 부르는 쪽이 BattleFx.Quality를 넘긴다.
    /// </summary>
    public static class JuiceGates
    {
        /// <summary>잉크 받침: 높음 모두 · 보통 치명타·처치·보스만(important) · 낮음 없음</summary>
        public static bool InkBacking(FxQuality quality, bool important) => quality == FxQuality.High || (quality == FxQuality.Medium && important);

        /// <summary>일반 명중 고리: 높음(밀도 8 이하) · 보통은 리듬 강조만 · 낮음 없음</summary>
        public static bool NormalRing(FxQuality quality, int density, bool rhythmAccent) =>
            quality == FxQuality.High ? density <= HitFeel.DensityMini || rhythmAccent : quality == FxQuality.Medium && rhythmAccent;

        /// <summary>일반 명중 빛줄기 수: 4 / 3 / 2</summary>
        public static int StreaksPerNormalHit(FxQuality quality) => quality switch
        {
            FxQuality.Low => 2,
            FxQuality.Medium => 3,
            _ => 4,
        };

        /// <summary>투사체 잔상 / 흘리는 빛 부스러기: 2 / 1 / 0</summary>
        public static int Afterimages(FxQuality quality) => quality switch
        {
            FxQuality.Low => 0,
            FxQuality.Medium => 1,
            _ => 2,
        };

        public static int ShedMotes(FxQuality quality) => Afterimages(quality);

        /// <summary>속도선: 높음 모두 · 보통은 보스·불 밝히기(bigMoment)만 · 낮음 없음</summary>
        public static bool Speedlines(FxQuality quality, bool bigMoment) => quality == FxQuality.High || (quality == FxQuality.Medium && bigMoment);

        /// <summary>일반 처치 색종이: 10 / 6 / 4</summary>
        public static int KillConfetti(FxQuality quality) => quality switch
        {
            FxQuality.Low => 4,
            FxQuality.Medium => 6,
            _ => 10,
        };

        /// <summary>보스 처치 색종이 / 리본 / 별빛: (24, 6, 18) / (16, 4, 12) / (10, 2, 6)</summary>
        public static int BossConfetti(FxQuality quality) => quality switch
        {
            FxQuality.Low => 10,
            FxQuality.Medium => 16,
            _ => 24,
        };

        public static int BossRibbons(FxQuality quality) => quality switch
        {
            FxQuality.Low => 2,
            FxQuality.Medium => 4,
            _ => 6,
        };

        public static int BossSparkles(FxQuality quality) => quality switch
        {
            FxQuality.Low => 6,
            FxQuality.Medium => 12,
            _ => 18,
        };

        /// <summary>스테이지 클리어 색종이: 30 / 20 / 10</summary>
        public static int CelebrationConfetti(FxQuality quality) => quality switch
        {
            FxQuality.Low => 10,
            FxQuality.Medium => 20,
            _ => 30,
        };

        /// <summary>불 밝히기 별비: 높음 40(여유 50% 넘을 때, 아니면 18) · 보통 24 · 낮음 10</summary>
        public static int LampRain(FxQuality quality, float freeRatio) => quality switch
        {
            FxQuality.Low => 10,
            FxQuality.Medium => 24,
            _ => freeRatio > 0.5f ? 40 : 18,
        };

        /// <summary>인쇄 어긋남: 번쩍임 줄이기·낮음 품질에서 끈다</summary>
        public static bool Misprint(FxQuality quality, bool reduceFlash) => !reduceFlash && quality != FxQuality.Low;

        /// <summary>플립북 코어(P2): 높음만</summary>
        public static bool Flipbook(FxQuality quality) => quality == FxQuality.High;

        /// <summary>코어 별 세기: 번쩍임 줄이기면 ×0.6</summary>
        public static float CoreIntensity(bool reduceFlash) => reduceFlash ? 0.6f : 1f;

        /// <summary>후광 세기: 번쩍임 줄이기면 ×0.7</summary>
        public static float HaloIntensity(bool reduceFlash) => reduceFlash ? 0.7f : 1f;

        /// <summary>
        /// 충격 고리 세기: 번쩍임 줄이기면 가산 비율만큼 ×1 → ×0.6 (코어 별과 같은 값, 보통 알파 고리는 그대로).
        /// 밝은 광장 위 가산 크림 고리는 치명타·처치 +17~83ms의 가장 큰 흰 면이다 — 줄이기 설정에서 그대로 남지 않게 (§9 '줄이기가 모두 줄인다')
        /// </summary>
        public static float RingLight(bool reduceFlash, float add) => Mathf.Lerp(1f, CoreIntensity(reduceFlash), Mathf.Clamp01(add));

        /// <summary>
        /// 불 밝히기 빛 층(점화 빛 · 빛살 · 등불 꽃 · 빛기둥 · 바닥 빛 물결 · 안쪽 고리 · 속도선) 세기: 번쩍임 줄이기면 ×0.5 —
        /// 순수 가산 층이 겹쳐 수선점 둘레가 하얗게 날아가지 않게 (Appendix B: ReduceFlash가 모두 줄인다)
        /// </summary>
        public static float LampLight(bool reduceFlash) => reduceFlash ? 0.5f : 1f;
    }

    /// <summary>
    /// 연출 시험장이 설정을 PlayerPrefs에 쓰지 않고 잠깐 바꿔 보는 값 (BattleFx.DevOverride). Active가 아니면 기기 설정을 쓴다
    /// </summary>
    public struct FxDevSettings
    {
        public bool Active;
        public FxQuality Quality;
        public bool ReduceFlash;
        /// <summary>흔들림 배율 0~1</summary>
        public float ShakeScale;
        public DamageNumberMode Numbers;
        /// <summary>
        /// 성능 대조용 (시험장 할당·성능 확인만): 입자 크기·색을 예전처럼 매 프레임 sizeDelta · Image.color로 쓴다
        /// (그림 메시를 매 프레임 다시 만든다). 꺼져 있으면 크기는 배율, 투명도는 CanvasRenderer 색으로 (메시 다시 만들기 없음)
        /// </summary>
        public bool LegacyParticleWrites;

        public static FxDevSettings Defaults => new() { Active = true, Quality = FxQuality.High, ReduceFlash = false, ShakeScale = 1f, Numbers = DamageNumberMode.All };
    }

    /// <summary>이번 프레임 연출 상태 (시험장 캡처 metrics.tsv — 흔들림·확대·어둡게·빛·입자 수·숫자 수·예산 탈락)</summary>
    public struct FxFrameStats
    {
        /// <summary>전장 흔들림 크기 (px) = trauma 잡음 + 킥만 (줌 펀치의 중심 보정 이동은 ZoomShift로 따로)</summary>
        public float ShakeOffset;
        /// <summary>줌 펀치가 확대 중심을 제자리에 두려고 옮긴 양 (px, (1 − z) × 중심) — 흔들림이 아니다</summary>
        public float ZoomShift;
        /// <summary>전장 확대 (1 = 보통)</summary>
        public float Zoom;
        /// <summary>전장 어둡게 알파</summary>
        public float Dim;
        /// <summary>빛 번쩍임 세기 (전장 + 앱 전체 중 큰 쪽)</summary>
        public float Light;
        /// <summary>틴트 번쩍임 알파</summary>
        public float Tint;
        public int LiveUpper;
        public int LiveGround;
        public int VisibleNumbers;
        /// <summary>실루엣(임팩트 프레임·인쇄 어긋남) 켜진 수</summary>
        public int Silhouettes;
        /// <summary>지난 표본 뒤 위쪽 예산이 차서 못 만든 입자 수 (별빛 층 + 영웅 층)</summary>
        public int RejectedUpper;
        /// <summary>그중 영웅 층(별빛 층이 아닌 것: 투사체 머리 · 명중 그림 · 처치 꽃 · 코어 별 …) — 높음 품질에서는 0이어야 한다</summary>
        public int RejectedHero;
        /// <summary>지난 표본 뒤 바닥 예산이 차서 못 만든 바닥 입자 수 (위쪽 층으로 넘기지 않는다)</summary>
        public int RejectedGround;
        /// <summary>지난 표본 뒤 마지막으로 막힌 영웅 층 그림 (진단 — 어떤 층이 예산에 막히는지 시험장 metrics가 이름으로 남긴다)</summary>
        public Sprite LastRejectedHero;
        /// <summary>지난 표본 뒤 화면 효과 큐가 가득 차 버린 수</summary>
        public int CuesDropped;
    }

    /// <summary>
    /// 늦은 화면 효과의 종류. 값 순서 = 우선순위 (큐가 가득 차면 낮은 것부터 비운다):
    /// 진동 &lt; 킥 &lt; 인쇄 어긋남 &lt; 어둡게 &lt; 줌 &lt; 흔들림 &lt; 빛 번쩍임 &lt; 임팩트 프레임 &lt; 보스 처치 순간
    /// </summary>
    public enum FxCueKind : byte
    {
        None,
        Haptic,
        Kick,
        Misprint,
        Dim,
        Zoom,
        Trauma,
        Light,
        ImpactFrame,
        BossMoment,
    }

    /// <summary>늦은 화면 효과 하나 (BattleFx가 값을 채우고, 그 시각이 되면 FxCueQueue가 돌려준다)</summary>
    public struct FxCue
    {
        public FxCueKind Kind;
        /// <summary>At이 실제 시간인지 (아니면 연출 시계)</summary>
        public bool Real;
        public float At;
        public Vector2 V;
        public float A;
        public float B;
        public float C;
        public float D;
        public Color Color;
        public bool Force;
        public Graphic Body;
        /// <summary>킥 채널 / 진동 종류</summary>
        public byte Code;
    }

    /// <summary>
    /// 늦은 화면 효과 큐 (FX-JUICE §3.3, 32칸 — 순수 C#, 할당 없음, 테스트 가능). 어떤 큐도 제 시각보다 일찍 터뜨리지 않는다.
    ///   · 합치기 — 막힐 것이 뻔한 같은 종류는 하나만 남긴다: 킥은 채널마다(간격 안이면 이른 쪽) · 인쇄 어긋남 하나(1초 안이면 이른 쪽) ·
    ///     진동은 종류마다(0.4초 안이면 이른 쪽) · 줌 하나(0.8초 안이면 센 쪽, 강제는 어느 쪽이든 남긴다).
    ///   · 가득 차면 우선순위(FxCueKind 값)가 가장 낮은 큐를 밀어내되(같으면 가장 늦은 것), 들어오는 큐가 그보다 높을 때만 — 아니면 들어오는 큐를 버린다.
    ///     (범위 스킬로 20마리를 한꺼번에 처치해도 킥·인쇄 어긋남이 보스 처치 순간을 밀어내거나 앞당기지 못한다)
    /// </summary>
    public sealed class FxCueQueue
    {
        public const int Capacity = 32;
        /// <summary>진동 게이트(강한 진동 0.4초)보다 가까운 같은 종류는 하나로</summary>
        public const float HapticMergeWindow = 0.4f;
        /// <summary>인쇄 어긋남은 1초에 한 번 (FxGovernor.TryMisprint)</summary>
        public const float MisprintMergeWindow = 1f;

        readonly FxCue[] _cues = new FxCue[Capacity];

        /// <summary>지금 기다리는 큐 수</summary>
        public int Count { get; private set; }
        /// <summary>지금까지 합친 수 / 밀어낸 수 / 버린 수 (진단)</summary>
        public int Merged { get; private set; }
        public int Evicted { get; private set; }
        public int Dropped { get; private set; }

        /// <summary>우선순위 (클수록 남는다)</summary>
        public static int Rank(FxCueKind kind) => (int)kind;

        /// <summary>큐 하나를 넣는다 (합치거나 빈칸에, 가득 차면 더 낮은 것을 밀어낸다). 버렸으면 false</summary>
        public bool Enqueue(in FxCue cue)
        {
            if (cue.Kind == FxCueKind.None) return false;
            if (TryMerge(in cue))
            {
                Merged++;
                return true;
            }
            for (int i = 0; i < Capacity; i++)
            {
                if (_cues[i].Kind != FxCueKind.None) continue;
                _cues[i] = cue;
                Count++;
                return true;
            }
            int victim = 0;
            for (int i = 1; i < Capacity; i++)
            {
                int rank = Rank(_cues[i].Kind);
                int worst = Rank(_cues[victim].Kind);
                if (rank < worst || (rank == worst && _cues[i].At > _cues[victim].At)) victim = i;
            }
            if (Rank(_cues[victim].Kind) < Rank(cue.Kind))
            {
                _cues[victim] = cue;
                Evicted++;
                return true;
            }
            Dropped++;
            return false;
        }

        /// <summary>막힐 것이 뻔한 같은 종류와 합친다 (합쳤으면 true)</summary>
        bool TryMerge(in FxCue cue)
        {
            for (int i = 0; i < Capacity; i++)
            {
                ref var pending = ref _cues[i];
                if (pending.Kind != cue.Kind || pending.Real != cue.Real) continue;
                float gap = Mathf.Abs(pending.At - cue.At);
                switch (cue.Kind)
                {
                    case FxCueKind.Kick:
                        if (pending.Code != cue.Code || gap >= HitFeel.KickInterval((KickChannel)cue.Code)) continue;
                        if (cue.At < pending.At) pending = cue;
                        return true;
                    case FxCueKind.Misprint:
                        if (gap >= MisprintMergeWindow) continue;
                        if (cue.At < pending.At) pending = cue;
                        return true;
                    case FxCueKind.Haptic:
                        if (pending.Code != cue.Code || gap >= HapticMergeWindow) continue;
                        if (cue.At < pending.At) pending = cue;
                        return true;
                    case FxCueKind.Zoom:
                        if (gap >= HitFeel.ZoomInterval) continue;
                        bool force = pending.Force || cue.Force;
                        if (cue.A > pending.A || (cue.A == pending.A && cue.At < pending.At)) pending = cue;
                        pending.Force = force;
                        return true;
                    default:
                        // 어둡게·흔들림·빛·임팩트 프레임·보스 순간은 각자 터진다 (각자의 규칙이 거른다)
                        return false;
                }
            }
            return false;
        }

        /// <summary>시각이 된 큐 중 가장 이른 것을 꺼낸다 (실제 시간 큐는 realNow, 나머지는 fxNow로 잰다). 없으면 false</summary>
        public bool TakeDue(float fxNow, float realNow, out FxCue cue)
        {
            int best = -1;
            for (int i = 0; i < Capacity; i++)
            {
                ref var c = ref _cues[i];
                if (c.Kind == FxCueKind.None) continue;
                if ((c.Real ? realNow : fxNow) < c.At) continue;
                if (best < 0 || c.At < _cues[best].At) best = i;
            }
            if (best < 0)
            {
                cue = default;
                return false;
            }
            cue = _cues[best];
            _cues[best] = default;
            Count--;
            return true;
        }

        /// <summary>모두 비운다 (진단 수도)</summary>
        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) _cues[i] = default;
            Count = 0;
            Merged = 0;
            Evicted = 0;
            Dropped = 0;
        }
    }

    /// <summary>
    /// 밀도·간격 관리 (FX-JUICE §2.4) — 순수 C# (할당 없음, 테스트 가능). BattleFx가 시계를 넘겨 부른다:
    /// 명중·처치·밀도는 연출 시계(도착 시각 = clock + delay), 화면 번쩍임·전체 정지·줌·킥은 실제 시간.
    /// </summary>
    public sealed class FxGovernor
    {
        const float Never = -9999f;
        const int TargetSlots = 64;
        const int ArrivalSlots = 128;
        const int BurstSlots = 16;
        const int KillSlots = 32;

        readonly int[] _targetIds = new int[TargetSlots];
        readonly float[] _lastFull = new float[TargetSlots];
        readonly float[] _lastHit = new float[TargetSlots];
        readonly int[] _chain = new int[TargetSlots];
        readonly float[] _arrivals = new float[ArrivalSlots];
        int _arrivalHead;
        readonly float[] _bigBursts = new float[BurstSlots];
        int _bigHead;
        readonly float[] _killBursts = new float[BurstSlots];
        int _killBurstHead;
        readonly float[] _kills = new float[KillSlots];
        readonly Vector2[] _killPositions = new Vector2[KillSlots];
        int _killHead;
        float _lastKill;
        int _streak;

        float _lastFlash;
        float _flashBlockedUntil;
        float _lastGlobalStop;
        float _lastZoom;
        float _lastBossMoment;
        float _lastMisprint;
        float _lastSpotlight;
        readonly float[] _lastKick = new float[8];

        public FxGovernor() => Clear();

        /// <summary>지금 이어지는 연속 처치 수 (마지막 처치 뒤 1.2초가 지나면 다음 처치가 1부터)</summary>
        public int Streak => _streak;

        public void Clear()
        {
            for (int i = 0; i < TargetSlots; i++)
            {
                _targetIds[i] = int.MinValue;
                _lastFull[i] = Never;
                _lastHit[i] = Never;
                _chain[i] = 0;
            }
            for (int i = 0; i < ArrivalSlots; i++) _arrivals[i] = Never;
            for (int i = 0; i < BurstSlots; i++)
            {
                _bigBursts[i] = Never;
                _killBursts[i] = Never;
            }
            for (int i = 0; i < KillSlots; i++)
            {
                _kills[i] = Never;
                _killPositions[i] = Vector2.zero;
            }
            for (int i = 0; i < _lastKick.Length; i++) _lastKick[i] = Never;
            _arrivalHead = _bigHead = _killBurstHead = _killHead = 0;
            _lastKill = Never;
            _streak = 0;
            _lastFlash = Never;
            _flashBlockedUntil = Never;
            _lastGlobalStop = Never;
            _lastZoom = Never;
            _lastBossMoment = Never;
            _lastMisprint = Never;
            _lastSpotlight = Never;
        }

        // ───────────── 명중 ─────────────

        /// <summary>
        /// 이 대상에 at(연출 시계)에 닿는 명중이 풀 명중인지 (같은 대상 90ms에 하나, 나머지는 미니). 0.6초 안에 이어지는 사슬의 3번째마다 리듬 강조
        /// (늘 풀 + 코어 ×1.15 + 고리). chain = 사슬 안에서 몇 번째 명중인지. 대상 ID가 음수면 기록하지 않고 늘 풀
        /// </summary>
        public bool TryFullImpact(int targetId, float at, out bool rhythmAccent, out int chain)
        {
            if (targetId < 0)
            {
                rhythmAccent = false;
                chain = 1;
                return true;
            }
            int slot = TargetSlot(targetId);
            float gap = Mathf.Abs(at - _lastHit[slot]);
            _chain[slot] = gap <= HitFeel.RhythmChainGap ? _chain[slot] + 1 : 1;
            chain = _chain[slot];
            rhythmAccent = chain >= HitFeel.RhythmEvery && chain % HitFeel.RhythmEvery == 0;
            bool full = rhythmAccent || Mathf.Abs(at - _lastFull[slot]) >= HitFeel.FullImpactInterval;
            if (full) _lastFull[slot] = at;
            _lastHit[slot] = Mathf.Max(_lastHit[slot], at);
            return full;
        }

        int TargetSlot(int targetId)
        {
            int oldest = 0;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < TargetSlots; i++)
            {
                if (_targetIds[i] == targetId) return i;
                if (_lastHit[i] < oldestTime)
                {
                    oldestTime = _lastHit[i];
                    oldest = i;
                }
            }
            _targetIds[oldest] = targetId;
            _lastFull[oldest] = Never;
            _lastHit[oldest] = Never;
            _chain[oldest] = 0;
            return oldest;
        }

        /// <summary>예약된 도착 하나를 센다 (밀도용)</summary>
        public void NoteArrival(float at)
        {
            _arrivals[_arrivalHead] = at;
            _arrivalHead = (_arrivalHead + 1) % ArrivalSlots;
        }

        /// <summary>at 전 0.25초 안(at 포함)에 이미 센 도착 수 — 이 명중을 세기(NoteArrival) 전에 묻는다</summary>
        public int Density(float at)
        {
            int count = 0;
            float from = at - HitFeel.DensityWindow;
            for (int i = 0; i < ArrivalSlots; i++)
            {
                float t = _arrivals[i];
                if (t >= from && t <= at + 0.0001f) count++;
            }
            return count;
        }

        // ───────────── 큰 터짐 / 처치 ─────────────

        /// <summary>
        /// 큰 터짐(치명타·처치·스킬 폭발) 시작을 0.1초에 2개로: 그대로면 0, 세 번째는 +0.033, 네 번째는 +0.05초 미룬 값, 그 뒤로는 −1 (작은 연출로)
        /// </summary>
        public float BigBurstDelay(float at)
        {
            int near = 0;
            for (int i = 0; i < BurstSlots; i++)
            {
                if (Mathf.Abs(_bigBursts[i] - at) < HitFeel.BigBurstWindow) near++;
            }
            float extra = near < HitFeel.BigBurstsPerWindow ? 0f : near == HitFeel.BigBurstsPerWindow ? 0.033f : near == HitFeel.BigBurstsPerWindow + 1 ? 0.05f : -1f;
            if (extra < 0f) return -1f;
            _bigBursts[_bigHead] = at + extra;
            _bigHead = (_bigHead + 1) % BurstSlots;
            return extra;
        }

        /// <summary>풀 처치 꽃을 0.2초에 3개까지 (넘치면 false = 미니 처치)</summary>
        public bool TryKillBurst(float at)
        {
            int near = 0;
            for (int i = 0; i < BurstSlots; i++)
            {
                if (Mathf.Abs(_killBursts[i] - at) < HitFeel.KillBurstWindow) near++;
            }
            if (near >= HitFeel.KillBurstsPerWindow) return false;
            _killBursts[_killBurstHead] = at;
            _killBurstHead = (_killBurstHead + 1) % BurstSlots;
            return true;
        }

        /// <summary>
        /// 처치 하나 (at = 처치 꽃이 터지는 연출 시각). 연속 처치 수를 돌려주고, 0.25초 안에 함께 터진 처치 수와 그 가운데를 알려 준다
        /// </summary>
        public int NoteKill(float at, Vector2 position, out int multi, out Vector2 centroid)
        {
            _streak = _lastKill > Never + 1f && Mathf.Abs(at - _lastKill) <= HitFeel.StreakGap ? _streak + 1 : 1;
            _lastKill = Mathf.Max(_lastKill, at);
            _kills[_killHead] = at;
            _killPositions[_killHead] = position;
            _killHead = (_killHead + 1) % KillSlots;
            multi = 0;
            var sum = Vector2.zero;
            for (int i = 0; i < KillSlots; i++)
            {
                if (Mathf.Abs(_kills[i] - at) > HitFeel.MultiKillWindow) continue;
                multi++;
                sum += _killPositions[i];
            }
            centroid = multi > 0 ? sum / multi : position;
            return _streak;
        }

        // ───────────── 화면 (실제 시간) ─────────────

        /// <summary>
        /// 전장 전체 번쩍임(빛·틴트·임팩트 프레임)은 1초에 한 번 (WCAG 2.3.1). force(불 밝히기·보스 처치)는 자리를 차지하고 1초 동안 다른 번쩍임을 막는다.
        /// 막힌 번쩍임은 미루지 않고 버린다
        /// </summary>
        public bool TryFieldFlash(float now, bool force)
        {
            if (force)
            {
                _lastFlash = now;
                _flashBlockedUntil = now + HitFeel.FieldFlashInterval;
                return true;
            }
            if (now < _flashBlockedUntil || now - _lastFlash < HitFeel.FieldFlashInterval) return false;
            _lastFlash = now;
            return true;
        }

        /// <summary>전체 정지는 1.5초에 한 번</summary>
        public bool TryGlobalStop(float now)
        {
            if (now - _lastGlobalStop < HitFeel.GlobalStopInterval) return false;
            _lastGlobalStop = now;
            return true;
        }

        /// <summary>줌 펀치는 0.8초에 한 번 (force = 보스 처치·불 밝히기)</summary>
        public bool TryZoom(float now, bool force)
        {
            if (!force && now - _lastZoom < HitFeel.ZoomInterval) return false;
            _lastZoom = now;
            return true;
        }

        /// <summary>채널별 킥 간격</summary>
        public bool TryKick(KickChannel channel, float now)
        {
            int index = Mathf.Clamp((int)channel, 0, _lastKick.Length - 1);
            if (now - _lastKick[index] < HitFeel.KickInterval(channel)) return false;
            _lastKick[index] = now;
            return true;
        }

        /// <summary>보스 처치 순간은 1.5초에 한 번</summary>
        public bool TryBossMoment(float now)
        {
            if (now - _lastBossMoment < HitFeel.GlobalStopInterval) return false;
            _lastBossMoment = now;
            return true;
        }

        /// <summary>치명타 처치 인쇄 어긋남은 1초에 한 번</summary>
        public bool TryMisprint(float now)
        {
            if (now - _lastMisprint < 1f) return false;
            _lastMisprint = now;
            return true;
        }

        /// <summary>스킬 조명(전장 어둡게)은 2초에 한 번</summary>
        public bool TrySpotlight(float now)
        {
            if (now - _lastSpotlight < 2f) return false;
            _lastSpotlight = now;
            return true;
        }

        /// <summary>지난 보스 처치 순간 뒤 실제 시간 (알림을 잠시 쉰다)</summary>
        public float SinceBossMoment(float now) => now - _lastBossMoment;
    }
}
