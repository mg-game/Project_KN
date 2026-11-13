using System;
using TMPro;

namespace IdleGame.UI
{
    /// <summary>
    /// 할당 없는 숫자 표기 (팡파르 카운트업처럼 매 프레임 바뀌는 숫자용). char 버퍼에 써서 TMP_Text.SetCharArray로 넘긴다.
    ///   · 100만 미만: 콤마 정수 "+1,234"
    ///   · 100만 이상: NumberFormatter.Format과 같은 단위 표기 "+1.23B" (내림, 유효숫자 3자리)
    /// </summary>
    public static class FanfareFormat
    {
        /// <summary>버퍼 최소 길이 (부호 + 숫자 + 콤마 + 단위)</summary>
        public const int BufferSize = 32;

        /// <summary>value를 buffer에 쓰고 길이를 돌려준다. sign이 '\0'이면 부호를 붙이지 않는다</summary>
        public static int Write(char[] buffer, double value, char sign = '+')
        {
            int length = 0;
            if (sign != '\0') buffer[length++] = sign;
            if (double.IsNaN(value) || value < 0) value = 0;
            if (double.IsInfinity(value)) value = 1e300;

            if (value < 1_000_000) return WriteGrouped(buffer, length, (long)Math.Floor(value));

            int group = (int)Math.Floor(Math.Log10(value) / 3);
            double scaled = value / Math.Pow(1000, group);
            const double epsilon = 1e-9;
            if (scaled >= 1000 - epsilon) { scaled /= 1000; group++; }
            else if (scaled < 1 - epsilon) { scaled *= 1000; group--; }

            int decimals = scaled < 10 ? 2 : scaled < 100 ? 1 : 0;
            long factor = decimals == 2 ? 100 : decimals == 1 ? 10 : 1;
            long fixedPoint = (long)Math.Floor(scaled * factor + 1e-9);
            long whole = fixedPoint / factor;
            long fraction = fixedPoint % factor;

            length = WriteDigits(buffer, length, whole);
            if (decimals > 0)
            {
                buffer[length++] = '.';
                if (decimals == 2 && fraction < 10) buffer[length++] = '0';
                length = WriteDigits(buffer, length, fraction);
            }
            return WriteUnit(buffer, length, group);
        }

        /// <summary>TMP 글자에 바로 쓴다 (할당 없음)</summary>
        public static void Set(TMP_Text text, char[] buffer, double value, char sign = '+')
        {
            if (text == null) return;
            int length = Write(buffer, value, sign);
            text.SetCharArray(buffer, 0, length);
        }

        static int WriteGrouped(char[] buffer, int start, long value)
        {
            // 뒤에서부터 쓰고 뒤집는다
            int end = start;
            int digits = 0;
            do
            {
                if (digits > 0 && digits % 3 == 0) buffer[end++] = ',';
                buffer[end++] = (char)('0' + (int)(value % 10));
                value /= 10;
                digits++;
            } while (value > 0);
            Array.Reverse(buffer, start, end - start);
            return end;
        }

        static int WriteDigits(char[] buffer, int start, long value)
        {
            int end = start;
            do
            {
                buffer[end++] = (char)('0' + (int)(value % 10));
                value /= 10;
            } while (value > 0);
            Array.Reverse(buffer, start, end - start);
            return end;
        }

        /// <summary>group 1 = A, 26 = Z, 27 = AA … (NumberFormatter.GetUnit과 같다)</summary>
        static int WriteUnit(char[] buffer, int start, int group)
        {
            if (group <= 0) return start;
            int end = start;
            int n = group - 1;
            while (n >= 0 && end < buffer.Length)
            {
                buffer[end++] = (char)('A' + n % 26);
                n = n / 26 - 1;
            }
            Array.Reverse(buffer, start, end - start);
            return end;
        }
    }
}
