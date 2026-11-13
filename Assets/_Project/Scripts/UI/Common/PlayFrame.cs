using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 세로 놀이 틀 (aspect AF-2, "배경이 해상도 안 맞을 때"): 화면이 9:16보다 넓으면(태블릿 · 폴더블 안쪽 · 정사각 분할 창 · 가로 PC 창)
    /// 게임 화면 전체를 가운데 세로 기둥(높이 = 화면, 폭 = 높이 × MaxAspect)에 그대로 두고 — 캔버스 단위로 1080×1920, 폰과 똑같은 배치 —
    /// 양옆은 PlayFrameSides(밤 마을 그림, 어둡게 · 그림자 · 금실)가 덮는다. 9:16 이하(폰, 길쭉한 폰)는 화면 전체 그대로 — 아무것도 바뀌지 않는다.
    /// <para>
    /// 여기는 순수 계산과 "지금 적용된 기둥"만 들고 있다 (PlayFrameTests). 적용은 CanvasScalerAdapter(캔버스 직속 층 · 양옆, 실행 순서 −1000)가,
    /// 안전 영역 층은 SafeArea가 스스로(Version을 보고) 한다. 기둥은 화면 비율 기준 (0~1, 가로만): Column = (왼쪽, 오른쪽).
    /// 기둥 폭은 화면 픽셀 단위로 반올림하고 왼쪽 여백은 내림 — 금실 가장자리가 픽셀에 딱 맞는다.
    /// </para>
    /// </summary>
    public static class PlayFrame
    {
        /// <summary>틀 최대 비율 (가로/세로) = 기준 해상도 1080×1920. 모든 빌더가 폭 1080에 맞춰 배치한다</summary>
        public const float DefaultMaxAspect = 9f / 16f;
        /// <summary>이만큼까지는 넓어도 틀을 씌우지 않는다 (1080×1919 같은 반올림 · 창 테두리 오차)</summary>
        public const float SnapTolerance = 0.002f;

        /// <summary>전체 화면 (틀 없음)</summary>
        public static readonly Vector2 FullColumn = new(0f, 1f);

        static float _maxAspect = DefaultMaxAspect;

        /// <summary>틀 최대 비율. 0 이하면 틀을 끈다 (실험 · 검증용). 바꾸면 CanvasScalerAdapter가 다음 Update에 다시 적용한다</summary>
        public static float MaxAspect
        {
            get => _maxAspect;
            set => _maxAspect = value;
        }

        /// <summary>지금 적용된 기둥 (화면 비율, x만): (왼쪽, 오른쪽). 적용 전 · 폰 = (0, 1)</summary>
        public static Vector2 Column { get; private set; } = FullColumn;

        /// <summary>적용될 때마다 증가 — SafeArea가 이 값이 바뀌면 다시 맞춘다</summary>
        public static int Version { get; private set; }

        /// <summary>지금 틀이 씌워져 있는지 (양옆이 보이는지)</summary>
        public static bool IsFramed => Column.x > 0f || Column.y < 1f;

        /// <summary>기둥 폭 비율 (0~1, 1 = 화면 전체)</summary>
        public static float Fraction => Column.y - Column.x;

        /// <summary>지금 기둥의 화면 사각형 (픽셀, 왼쪽 아래 = 0)</summary>
        public static Rect ColumnScreenRect => ColumnPixels(Screen.width, Screen.height, Column);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 도메인 다시 읽기 없이 Play를 다시 눌러도 지난 판의 틀이 남지 않게
            _maxAspect = DefaultMaxAspect;
            Column = FullColumn;
            Version++;
        }

        /// <summary>CanvasScalerAdapter가 층을 옮긴 뒤 부른다</summary>
        public static void SetApplied(Vector2 column)
        {
            Column = column;
            Version++;
        }

        // ───────────── 순수 계산 ─────────────

        /// <summary>
        /// 화면 w×h(픽셀)에서의 기둥 (화면 비율, x만). w/h ≤ maxAspect + SnapTolerance면 (0, 1).
        /// 기둥 폭 = round(h × maxAspect) 픽셀, 왼쪽 여백 = floor((w − 폭) / 2) 픽셀.
        /// </summary>
        public static Vector2 ColumnFor(float w, float h, float maxAspect)
        {
            if (w <= 0f || h <= 0f || maxAspect <= 0f || w / h <= maxAspect + SnapTolerance) return FullColumn;
            float width = Mathf.Round(h * maxAspect);
            if (width >= w || width <= 0f) return FullColumn;
            float left = Mathf.Floor((w - width) * 0.5f);
            return new Vector2(left / w, (left + width) / w);
        }

        /// <summary>기둥 (화면 비율) → 화면 픽셀 사각형</summary>
        public static Rect ColumnPixels(float w, float h, Vector2 column) =>
            Rect.MinMaxRect(column.x * w, 0f, column.y * w, h);

        /// <summary>두 사각형의 겹친 곳 (안 겹치면 폭 · 높이 0)</summary>
        public static Rect Intersect(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : new Rect(xMin, yMin, 0f, 0f);
        }

        /// <summary>
        /// 자식 안의 정규화 사각형 inner(자식 기준 0~1)를 부모 기준 정규화 사각형으로 (자식 오프셋이 0일 때 — 앵커만으로 정해진다)
        /// </summary>
        public static Rect Nest(Vector2 anchorMin, Vector2 anchorMax, Rect inner)
        {
            var span = anchorMax - anchorMin;
            return Rect.MinMaxRect(
                anchorMin.x + inner.xMin * span.x, anchorMin.y + inner.yMin * span.y,
                anchorMin.x + inner.xMax * span.x, anchorMin.y + inner.yMax * span.y);
        }

        /// <summary>
        /// 안전 영역 층의 앵커: 대상 = Screen.safeArea ∩ 기둥 (화면 픽셀), 부모의 화면 정규화 사각형 parentNorm(루트 캔버스 = (0,0,1,1)) 기준으로.
        /// 틀이 없고 부모가 루트면 예전과 똑같이 safeArea / 화면. 겹치는 곳이 없으면 기둥 전체. 결과는 0~1로 자른다.
        /// </summary>
        public static void SafeAnchors(Rect safePixels, float w, float h, Vector2 column, Rect parentNorm, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.one;
            if (w <= 0f || h <= 0f) return;
            var columnPixels = ColumnPixels(w, h, column);
            var target = Intersect(safePixels, columnPixels);
            if (target.width <= 0f || target.height <= 0f) target = columnPixels;
            var min = new Vector2(target.xMin / w, target.yMin / h);
            var max = new Vector2(target.xMax / w, target.yMax / h);
            var span = parentNorm.size;
            if (span.x <= 0f || span.y <= 0f) return;
            anchorMin = new Vector2(Mathf.Clamp01((min.x - parentNorm.xMin) / span.x), Mathf.Clamp01((min.y - parentNorm.yMin) / span.y));
            anchorMax = new Vector2(Mathf.Clamp01((max.x - parentNorm.xMin) / span.x), Mathf.Clamp01((max.y - parentNorm.yMin) / span.y));
        }
    }
}
