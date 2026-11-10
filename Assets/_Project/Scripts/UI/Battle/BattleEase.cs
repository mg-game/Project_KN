using System;
using System.Globalization;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 연출용 이징/보간 함수와 숫자 문자열 캐시 (매 프레임 할당 없이 숫자를 찍기 위함).
    /// 숫자는 실수 그대로 받는다 — 후반 피해는 int 범위(2.1B)를 넘으므로 int로 바꾸지 않는다.
    /// 이징 4개(OutCubic · InCubic · InOutSine · OutBack)는 UIMotion을 부르는 한 줄이다 (M24 — 이징은 UIMotion 하나, 식이 같아 움직임은 그대로).
    /// </summary>
    public static class BattleEase
    {
        public static float OutCubic(float t) => UIMotion.OutCubic(t);

        public static float InCubic(float t) => UIMotion.InCubic(t);

        public static float InOutSine(float t) => UIMotion.InOutSine(t);

        /// <summary>목표를 살짝 지나쳤다가 돌아오는 곡선 (UIMotion.OutBack과 같은 식)</summary>
        public static float OutBack(float t, float overshoot = 1.70158f) => UIMotion.OutBack(t, overshoot);

        /// <summary>0에서 시작해 1까지 튀어 오른 뒤 0으로 잦아드는 펀치 (t = 0~1)</summary>
        public static float Punch(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI) * (1f - t);
        }

        /// <summary>프레임 속도와 무관한 지수 감쇠 보간</summary>
        public static float Damp(float current, float target, float sharpness, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));

        public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>색의 알파만 바꾼 값</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // ───────────── 막대 ─────────────

        /// <summary>
        /// 가로 막대 채움 (0~1). 빌더의 막대는 틀(ui_bar_frame_9s) 안에 9-slice 채움(ui_bar_fill_9s)이 왼쪽에 붙어 있고,
        /// 오른쪽 앵커(anchorMax.x)를 비율로 옮겨 폭을 바꾼다 — 둥근 9-slice에 fillAmount를 쓰면 끝이 렌즈 모양으로 깎이므로 쓰지 않는다.
        /// 0이거나 테두리 안쪽 여백보다 짧아지면 채움을 끈다. 예전 Filled 이미지면 fillAmount를 그대로 쓴다.
        /// </summary>
        public static void SetBar(UnityEngine.UI.Image fill, float ratio)
        {
            if (fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            if (fill.type == UnityEngine.UI.Image.Type.Filled)
            {
                fill.fillAmount = ratio;
                return;
            }
            var rect = fill.rectTransform;
            var max = rect.anchorMax;
            if (!Mathf.Approximately(max.x, ratio)) rect.anchorMax = new Vector2(ratio, max.y);
            bool visible = ratio > 0.001f;
            // 부모 크기가 정해졌으면(레이아웃 뒤) 실제 폭이 0 이하인 9-slice가 뒤집혀 그려지지 않게 끈다
            if (visible && rect.parent is RectTransform track && track.rect.width > 0f)
                visible = track.rect.width * (ratio - rect.anchorMin.x) + rect.offsetMax.x - rect.offsetMin.x > 0.5f;
            if (fill.enabled != visible) fill.enabled = visible;
        }

        /// <summary>SetBar로 정한 지금 비율 (꺼져 있어도 마지막 비율 — 보간용)</summary>
        public static float BarRatio(UnityEngine.UI.Image fill)
        {
            if (fill == null) return 0f;
            return fill.type == UnityEngine.UI.Image.Type.Filled ? fill.fillAmount : fill.rectTransform.anchorMax.x;
        }

        /// <summary>
        /// 캡슐 9-slice(위아래 테두리 합 = 그림 높이인 알약 · 막대 그림)를 지금 높이에 맞춰 끝이 동그랗게 유지한다: 배율 = 그림 높이 / 높이.
        /// 막대 높이를 런타임에 바꿀 때(보스 HP 바 10 → 16px) 빌더가 정한 배율이 맞지 않아 끝이 각진 둥근 사각형이 되는 것을 막는다.
        /// 캡슐이 아닌 그림(자리표시 둥근 사각형)이나 Sliced가 아닌 이미지는 그대로 둔다
        /// </summary>
        public static void FitCapsule(UnityEngine.UI.Image image, float height)
        {
            if (image == null || image.type != UnityEngine.UI.Image.Type.Sliced || height <= 0f) return;
            var sprite = image.sprite;
            if (sprite == null) return;
            float vertical = sprite.border.y + sprite.border.w;
            if (vertical <= 0f || Mathf.Abs(vertical - sprite.rect.height) > 0.5f) return;
            image.pixelsPerUnitMultiplier = vertical / height;
        }

        // ───────────── 숫자 문자열 캐시 ─────────────

        /// <summary>숫자 문자열 모양 (앞에 붙는 말이 다르다)</summary>
        enum NumberStyle : byte
        {
            Int,
            Plus,
            Minus,
            Blocked,
            Crit,
            Mark,
        }

        const int CacheSize = 10000;
        /// <summary>
        /// 단위 표기 문자열 캐시 칸 수 (모양별, 2의 거듭제곱 — 그 모양을 처음 쓸 때 만든다). 칸 = 표시값 차례(묶음 × 2,700 + 소수 자리·앞 3자리)를
        /// 칸 수로 나눈 나머지라, 이어진 표시값 4,096개(약 1.5묶음 — 크기 차 3만 배 안쪽)는 서로 다른 칸이다: 한 전투의 숫자는 데워진 뒤 할당 없이
        /// 늘 같은 글자. 그보다 멀리 떨어진 값끼리만 같은 칸을 덮어쓴다 (예전 1,024개에서 통째로 비우던 사전은 한 묶음의 표시값 2,700개도 못 담아 계속 새로 만들었다)
        /// </summary>
        const int LargeSlots = 4096;
        /// <summary>한 묶음(1,000배)의 표시값 수: 소수 2자리(1.00~9.99) · 1자리(10.0~99.9) · 0자리(100~999) 각 900개</summary>
        const int LargePerGroup = 2700;
        const int LargePerDecimals = 900;
        /// <summary>치명타 숫자 앞의 작은 별 (색만으로 뜻을 전하지 않게, ATK-17)</summary>
        const string CritPrefix = "<size=70%>★</size>";
        /// <summary>표식으로 커진 피해 앞의 작은 하트 (FX-03 / ATK-17 — 더스티 핑크 색만으로 구분하지 않게)</summary>
        const string MarkPrefix = "<size=70%>♥</size>";

        static readonly string[] Prefixes = { string.Empty, "+", "-", "막음 ", CritPrefix, MarkPrefix };
        static readonly string[][] SmallCaches =
        {
            new string[CacheSize], new string[CacheSize], new string[CacheSize], new string[CacheSize], new string[CacheSize], new string[CacheSize],
        };
        /// <summary>단위 표기 캐시 (모양별 칸 배열, 처음 쓸 때 만든다): 칸마다 표시값 열쇠와 그 글자</summary>
        static readonly int[][] LargeKeys = new int[6][];
        static readonly string[][] LargeTexts = new string[6][];
        static readonly string[] InfinityCache = new string[6];

        /// <summary>정수 문자열: 10,000 미만은 콤마 정수(캐시), 그 이상은 단위 표기 (FX-03)</summary>
        public static string Int(int value) => value < 0 ? Minus((double)value) : Make(NumberStyle.Int, value);

        /// <summary>
        /// 실수 그대로 받는 정수 문자열 (후반 피해는 int 범위 2.1B를 넘는다 — int로 바꾸지 않는다).
        /// 10,000 미만은 콤마 정수, 그 이상은 단위 표기 3자리 (NumberFormatter.Format과 같은 표기, 같은 표시값은 캐시)
        /// </summary>
        public static string Int(double value) => value < 0 ? Minus(value) : Make(NumberStyle.Int, value);

        /// <summary>"+N" 문자열</summary>
        public static string Plus(int value) => Plus((double)value);

        /// <summary>"+N" 문자열 (실수 그대로)</summary>
        public static string Plus(double value) => value < 0 ? Minus(value) : Make(NumberStyle.Plus, value);

        /// <summary>"-N" 문자열 (int.MinValue도 안전하게 — 실수로 절댓값을 잰다)</summary>
        public static string Minus(int value) => Minus((double)value);

        /// <summary>"-N" 문자열 (실수 그대로, 부호는 무시)</summary>
        public static string Minus(double value) => Make(NumberStyle.Minus, Math.Abs(value));

        /// <summary>"막음 N" 문자열 (적 보호막이 막은 피해)</summary>
        public static string Blocked(int value) => Blocked((double)value);

        /// <summary>"막음 N" 문자열 (실수 그대로)</summary>
        public static string Blocked(double value) => Make(NumberStyle.Blocked, Math.Abs(value));

        /// <summary>치명타 숫자: 작은 별 + 정수 ("★1,234", 별은 70% 크기)</summary>
        public static string Crit(int value) => Crit((double)value);

        /// <summary>치명타 숫자 (실수 그대로)</summary>
        public static string Crit(double value) => Make(NumberStyle.Crit, Math.Abs(value));

        /// <summary>표식 증폭 숫자: 작은 하트 + 정수 ("♥1,234", 하트는 70% 크기)</summary>
        public static string Mark(int value) => Mark((double)value);

        /// <summary>표식 증폭 숫자 (실수 그대로)</summary>
        public static string Mark(double value) => Make(NumberStyle.Mark, Math.Abs(value));

        /// <summary>
        /// 모양별 숫자 문자열. 반올림해 10,000 미만이면 콤마 정수 캐시, 그 이상은 표시값(단위 · 소수 자리 · 3자리 숫자) 칸 캐시 (LargeSlots) —
        /// 같은 표시값이면 할당이 없다 (칸이 다른 표시값에 덮였을 때만 다시 만든다). NaN·음수(이미 절댓값을 받는다)는 0
        /// </summary>
        static string Make(NumberStyle style, double value)
        {
            int index = (int)style;
            if (double.IsInfinity(value)) return InfinityCache[index] ??= Prefixes[index] + "∞";
            if (!(value > 0d)) value = 0d;
            double rounded = Math.Round(value);
            if (rounded < CacheSize)
            {
                int small = (int)rounded;
                return SmallCaches[index][small] ??= Prefixes[index].Length == 0 ? Comma(small) : Prefixes[index] + Comma(small);
            }
            LargeParts(rounded, out int group, out int decimals, out int digits);
            int key = (group << 12) | (decimals << 10) | digits;
            int slot = LargeSlot(group, decimals, digits);
            var keys = LargeKeys[index] ??= new int[LargeSlots];
            var texts = LargeTexts[index] ??= new string[LargeSlots];
            var text = texts[slot];
            if (text != null && keys[slot] == key) return text;
            text = Prefixes[index] + LargeText(group, decimals, digits);
            keys[slot] = key;
            texts[slot] = text;
            return text;
        }

        /// <summary>
        /// 표시값의 캐시 칸: 표시값 차례 = 묶음 × 2,700 + (2 − 소수 자리) × 900 + (앞 3자리 − 100) — 값이 커질수록 하나씩 늘어나는 번호를 칸 수로 나눈 나머지
        /// (열쇠는 따로 맞춰 보므로 칸이 겹쳐도 틀린 글자는 나오지 않는다)
        /// </summary>
        static int LargeSlot(int group, int decimals, int digits) =>
            (group * LargePerGroup + (2 - decimals) * LargePerDecimals + digits - 100) & (LargeSlots - 1);

        /// <summary>
        /// 단위 표기의 표시값 (NumberFormatter.Format과 같은 계산): 1,000 단위 묶음 수, 소수 자리(0~2), 내림한 앞 3자리 숫자(100~999).
        /// 예) 12,345 → (1, 1, 123) = "12.3A"
        /// </summary>
        static void LargeParts(double value, out int group, out int decimals, out int digits)
        {
            group = (int)Math.Floor(Math.Log10(value) / 3);
            double scaled = value / Math.Pow(1000, group);
            // Log10/Pow 부동소수 오차 보정 (NumberFormatter와 같다)
            const double epsilon = 1e-9;
            if (scaled >= 1000 - epsilon)
            {
                scaled /= 1000;
                group++;
            }
            else if (scaled < 1 - epsilon)
            {
                scaled *= 1000;
                group--;
            }
            decimals = scaled < 10 ? 2 : scaled < 100 ? 1 : 0;
            double factor = decimals == 2 ? 100d : decimals == 1 ? 10d : 1d;
            digits = (int)Math.Min(999d, Math.Max(0d, Math.Floor(scaled * factor + 1e-9)));
        }

        /// <summary>표시값 → 글자 ("1.23B", "12.3A", "123C"). 캐시가 비어 있을 때만 만든다</summary>
        static string LargeText(int group, int decimals, int digits)
        {
            string unit = NumberFormatter.GetUnit(group);
            if (decimals == 0) return digits.ToString(CultureInfo.InvariantCulture) + unit;
            int scale = decimals == 2 ? 100 : 10;
            string fraction = (digits % scale).ToString(decimals == 2 ? "00" : "0", CultureInfo.InvariantCulture);
            return (digits / scale).ToString(CultureInfo.InvariantCulture) + "." + fraction + unit;
        }

        static string Comma(int value) => value.ToString("#,0", CultureInfo.InvariantCulture);
    }
}
