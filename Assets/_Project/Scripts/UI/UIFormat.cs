using System;
using System.Text;

namespace IdleGame.UI
{
    /// <summary>
    /// 글자 붙이기·시간 표기 토큰 (M28 · M30 · M32, UX-SPEC §1.10).
    ///   · 숫자와 그 이름은 줄바꿈 없는 빈칸(Nbsp, U+00A0)으로 잇는다 ("단추 3.60A"가 두 줄로 갈라지지 않게).
    ///   · 스테이지 표기처럼 '-'가 든 말은 Keep으로 &lt;nobr&gt;에 넣는다 (TMP는 "2-10"의 '-' 뒤에서 줄을 바꿀 수 있다).
    ///     리치 텍스트가 켜진 TMP 글자(UIKit.Txt 기본)에서만 쓴다.
    ///   · 매초 바뀌는 시계 글자는 ClockMono (숫자 묶음만 고정폭). 토스트·로그는 MetaUi.Clock/ResetIn/HourMinute 그대로.
    ///   · 바구니처럼 지난 시간을 짧게 보일 때는 ShortElapsed ('방금' / 'N분' / 'N시간' — '0:00'을 쓰지 않는다).
    /// 이 함수들은 값이 바뀔 때만 부른다 (부르는 쪽이 바뀐 것을 확인한다). ShortElapsed는 미리 만든 문자열을 돌려줘 할당이 없다.
    /// </summary>
    public static class UIFormat
    {
        /// <summary>줄바꿈 없는 빈칸 (U+00A0). 숫자와 이름 사이에 쓴다</summary>
        public const string Nbsp = "\u00A0";
        public const char NbspChar = '\u00A0';

        /// <summary>
        /// 개수 표시 곱하기 '×' (U+00D7, "×5"). Noto Sans KR의 ×는 전각(폭 1em, 글자 높이 0.64em)이라 숫자보다 크고 옆 빈틈이 넓다 —
        /// 좁은 칩에서는 TimesSmall을 쓴다.
        /// </summary>
        public const string Times = "\u00D7";
        /// <summary>작은 곱하기 (전각 ×를 80% 크기로, 리치 텍스트 TMP 전용)</summary>
        public const string TimesSmall = "<size=80%>\u00D7</size>";

        /// <summary>
        /// ClockMono가 숫자 묶음에 주는 고정폭 (em). Noto Sans KR 숫자 폭을 잰 값: Bold 0.590em, Regular 0.555em
        /// (원래도 모든 숫자 폭이 같은 tabular 숫자라 흔들림은 없다 — 다른 글꼴로 바뀌어도 흔들리지 않게 막아 둔다)
        /// </summary>
        public const float DigitEm = 0.59f;

        const string MonoOpen = "<mspace=0.59em>";
        const string MonoClose = "</mspace>";

        static readonly StringBuilder Builder = new(96);

        /// <summary>"&lt;nobr&gt;s&lt;/nobr&gt;" — 줄이 바뀌지 않는 묶음 (예: Keep("스테이지 2-10")). 비었으면 빈 문자열</summary>
        public static string Keep(string s) => string.IsNullOrEmpty(s) ? string.Empty : "<nobr>" + s + "</nobr>";

        /// <summary>이름 + 줄바꿈 없는 빈칸 + 숫자 ("단추 3.60A")</summary>
        public static string Join(string label, string number)
        {
            if (string.IsNullOrEmpty(label)) return number ?? string.Empty;
            if (string.IsNullOrEmpty(number)) return label;
            return label + Nbsp + number;
        }

        /// <summary>
        /// 매초 바뀌는 시계 (TMP 리치 텍스트 전용): "7:38:48" / "38:48" 모양 그대로, 숫자 묶음만 &lt;mspace&gt;로 고정폭 —
        /// 쌍점은 원래 폭. 예) &lt;mspace=0.59em&gt;7&lt;/mspace&gt;:&lt;mspace=0.59em&gt;38&lt;/mspace&gt;:&lt;mspace=0.59em&gt;48&lt;/mspace&gt;.
        /// 음수는 0. 표기 규칙은 MetaUi.Clock과 같다 (시간이 0이면 분:초).
        /// </summary>
        public static string ClockMono(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            long hours = (long)span.TotalHours;
            var sb = Builder;
            sb.Clear();
            if (hours > 0)
            {
                AppendMono(sb, hours, 1);
                sb.Append(':');
            }
            AppendMono(sb, span.Minutes, 2);
            sb.Append(':');
            AppendMono(sb, span.Seconds, 2);
            return sb.ToString();
        }

        static void AppendMono(StringBuilder sb, long value, int minDigits)
        {
            sb.Append(MonoOpen);
            if (minDigits == 2 && value < 10) sb.Append('0');
            sb.Append(value);
            sb.Append(MonoClose);
        }

        // ───────────── 지난 시간 (바구니 위젯 M32) ─────────────

        /// <summary>1분 미만</summary>
        public const string JustNow = "방금";

        static readonly string[] MinuteTexts = new string[60];
        static readonly string[] HourTexts = new string[100];

        /// <summary>
        /// 지난 시간을 짧게: 1분 미만 '방금', 1시간 미만 'N분', 그 위 'N시간'(내림). '0:00'처럼 쓰지 않는다.
        /// 문자열은 한 번 만든 것을 다시 쓴다 (매초 불러도 할당 없음, 100시간 이상만 새로 만든다). 음수는 '방금'.
        /// </summary>
        public static string ShortElapsed(TimeSpan span) => ShortElapsed(span.TotalSeconds);

        /// <summary>ShortElapsed (초 단위, NaN·음수 → '방금')</summary>
        public static string ShortElapsed(double seconds)
        {
            if (!(seconds >= 60d)) return JustNow;
            if (seconds < 3600d)
            {
                int minutes = Math.Min(59, (int)(seconds / 60d));
                return MinuteTexts[minutes] ??= minutes + "분";
            }
            double hours = Math.Floor(seconds / 3600d);
            if (hours < HourTexts.Length)
            {
                int index = (int)hours;
                return HourTexts[index] ??= index + "시간";
            }
            return hours.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "시간";
        }
    }
}
