using System;
using System.Globalization;

namespace IdleGame
{
    /// <summary>
    /// 방치형 큰 숫자 표기. 1,000 단위마다 A, B, C ... Z, AA, AB ... 단위를 붙인다.
    /// 표시값은 내림 처리해서 실제 보유량보다 크게 보이지 않게 한다.
    /// <para>
    /// 쓰임 (M30): 재화 표시는 FormatCurrency (100만 미만 콤마), 5글자 이하 칩은 FormatCompact (1만 미만 콤마, 그 위는 유효 숫자 3자리에서
    /// 끝의 0을 뺀다). Format / FormatInt는 그대로 둔다 (피해 숫자와 테스트가 고정해 쓴다). 세 함수는 같은 자릿수 계산(Scale)을 쓴다.
    /// </para>
    /// </summary>
    public static class NumberFormatter
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>FormatCurrency가 콤마 표기를 쓰는 상한 (미만)</summary>
        public const double CurrencyCommaLimit = 1_000_000;
        /// <summary>FormatCompact가 콤마 표기를 쓰는 상한 (미만)</summary>
        public const double CompactCommaLimit = 10_000;

        public static string Format(double value)
        {
            if (double.IsNaN(value)) return "0";
            if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";
            if (value < 0) return "-" + Format(-value);
            if (value < 1000) return Math.Floor(value).ToString("0", Invariant);

            double truncated = Scale(value, out int group, out int decimals);
            return truncated.ToString("F" + decimals, Invariant) + GetUnit(group);
        }

        /// <summary>젬처럼 정수로 보여줄 재화. 100만 미만은 콤마, 이상은 단위 표기.</summary>
        public static string FormatInt(double value)
        {
            if (Math.Abs(value) < 1_000_000) return Math.Floor(value).ToString("N0", Invariant);
            return Format(value);
        }

        /// <summary>
        /// 재화 표기 (상단 바·비용·보상 문구). 100만 미만은 내림 정수 콤마("999,999"), 그 위는 Format("1.00B").
        /// NaN은 "0", 음수는 "-" + 양수 표기 (Format과 같음).
        /// </summary>
        public static string FormatCurrency(double value)
        {
            if (double.IsNaN(value)) return "0";
            if (double.IsInfinity(value)) return Format(value);
            if (value < 0) return "-" + FormatCurrency(-value);
            if (value < CurrencyCommaLimit) return Math.Floor(value).ToString("N0", Invariant);
            return Format(value);
        }

        /// <summary>
        /// 좁은 칩용 표기 (최대 5글자). 1만 미만은 내림 정수 콤마("9,999"), 그 위는 유효 숫자 3자리 내림에서 끝의 0을 뺀다
        /// ("10A", "12.3A", "999A", "1B", "1.55B" — "1.00A"처럼 쓰지 않는다). NaN은 "0", 음수는 "-" + 양수 표기.
        /// </summary>
        public static string FormatCompact(double value)
        {
            if (double.IsNaN(value)) return "0";
            if (double.IsInfinity(value)) return Format(value);
            if (value < 0) return "-" + FormatCompact(-value);
            if (value < CompactCommaLimit) return Math.Floor(value).ToString("N0", Invariant);

            double truncated = Scale(value, out int group, out _);
            return truncated.ToString("0.##", Invariant) + GetUnit(group);
        }

        /// <summary>group 1 = A(10^3), 26 = Z, 27 = AA ...</summary>
        public static string GetUnit(int group)
        {
            if (group <= 0) return string.Empty;
            int n = group - 1;
            var unit = string.Empty;
            while (n >= 0)
            {
                unit = (char)('A' + n % 26) + unit;
                n = n / 26 - 1;
            }
            return unit;
        }

        /// <summary>
        /// 1000 이상 값을 단위 묶음(group)과 유효 숫자 3자리로 내린 값으로 나눈다 (예: 12,345 → 12.3, group 1, 소수 1자리).
        /// Log10/Pow 부동소수 오차를 보정한다 (예: 1e81이 999.99..Z로 표기되는 문제).
        /// </summary>
        static double Scale(double value, out int group, out int decimals)
        {
            group = (int)Math.Floor(Math.Log10(value) / 3);
            double scaled = value / Math.Pow(1000, group);
            const double epsilon = 1e-9;
            if (scaled >= 1000 - epsilon) { scaled /= 1000; group++; }
            else if (scaled < 1 - epsilon) { scaled *= 1000; group--; }

            decimals = scaled < 10 ? 2 : scaled < 100 ? 1 : 0;
            double factor = Math.Pow(10, decimals);
            return Math.Floor(scaled * factor + 1e-9) / factor;
        }
    }
}
