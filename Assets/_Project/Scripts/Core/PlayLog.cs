using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 로컬 플레이 기록 (CR-09, SPEC4 §4.4): persistentDataPath/playlog.jsonl에 JSON 한 줄씩 쓴다. 1 MB가 넘으면 playlog.1.jsonl로 돌린다 (파일 2개).
    /// 줄은 재사용하는 StringBuilder 하나로 만들고 메모리에 모아 두었다가 10초마다(Tick) · 멈춤 · 종료 때(Flush) 한 번에 쓴다 — 매 프레임 쓰지 않는다.
    /// 자동 업로드 없음 · 개인 정보 없음. 기록 경로를 쓸 수 없으면 경고 한 줄 뒤 꺼진다 (예외를 내지 않는다). 메인 스레드에서만 부른다.
    /// <para>
    /// 줄 쓰기: <c>PlayLog.Begin("stage_end").Int("stage", 12).Str("res", "clear").End();</c> — 줄마다 "ts"(유닉스 초) · "ev"가 먼저 붙는다 (이벤트 칸 이름 "ts" · "ev"는 쓰지 않는다).
    /// 숫자 · 글자 쓰기는 할당하지 않는다 (Num의 소수도 정수 계산). 열리지 않았으면 아무것도 하지 않는다.
    /// 구독(세션 · 스테이지 · 소환 …)은 PlayLogRecorder가 한다. 다른 패키지도 같은 방법으로 줄을 더할 수 있다 (SPEC5 side_start 등).
    /// </para>
    /// </summary>
    public static class PlayLog
    {
        public const string FileName = "playlog.jsonl";
        public const string RotatedFileName = "playlog.1.jsonl";
        /// <summary>이 크기(바이트)를 넘기 전에 playlog.1.jsonl로 돌린다</summary>
        public const long DefaultMaxBytes = 1024 * 1024;
        /// <summary>모아 둔 줄을 쓰는 간격 (초)</summary>
        public const float FlushIntervalSeconds = 10f;
        /// <summary>모아 둔 줄이 이만큼(바이트) 넘으면 10초를 기다리지 않고 쓴다</summary>
        const int PendingFlushBytes = 64 * 1024;
        /// <summary>내보내기(Tail)가 돌려주는 기본 줄 수</summary>
        public const int ExportLineCount = 500;

        static readonly UTF8Encoding Utf8NoBom = new(false);
        static readonly StringBuilder Line = new(256);
        static readonly StringBuilder Pending = new(8 * 1024);

        static string _path;
        static string _rotatedPath;
        static long _maxBytes = DefaultMaxBytes;
        static long _fileBytes;
        static long _pendingBytes;
        static float _sinceFlush;
        static bool _open;
        static bool _inLine;
        static ITimeProvider _clock;

        /// <summary>기록 중인지 (Open 성공 ~ Close · 쓰기 실패 전)</summary>
        public static bool IsOpen => _open;

        /// <summary>지금 기록 파일 경로 (열리지 않았으면 null)</summary>
        public static string FilePath => _path;

        /// <summary>아직 파일에 쓰지 않은 줄의 바이트 수 (시험 · 개발용)</summary>
        public static long PendingBytes => _pendingBytes;

        /// <summary>
        /// directory에 기록을 연다 (이미 같은 곳에 열려 있으면 그대로 true). 폴더를 만들거나 파일을 열 수 없으면 경고 한 줄 뒤 false (기록 꺼짐).
        /// clock = 줄의 "ts" 시각 (null = 기기 시계). maxBytes = 돌리는 크기 (시험용)
        /// </summary>
        public static bool Open(string directory, ITimeProvider clock = null, long maxBytes = DefaultMaxBytes)
        {
            if (string.IsNullOrEmpty(directory)) return false;
            string path;
            try
            {
                path = Path.Combine(directory, FileName);
            }
            catch (Exception e)
            {
                Warn("경로", e);
                return false;
            }
            if (_open && string.Equals(path, _path, StringComparison.Ordinal))
            {
                _clock = clock ?? _clock;
                return true;
            }
            Close();
            try
            {
                Directory.CreateDirectory(directory);
                // 쓸 수 있는지 미리 본다 (0바이트 덧붙이기)
                using (new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { }
                _fileBytes = new FileInfo(path).Length;
            }
            catch (Exception e)
            {
                Warn("열기", e);
                return false;
            }
            _path = path;
            _rotatedPath = Path.Combine(directory, RotatedFileName);
            _clock = clock;
            _maxBytes = Math.Max(256, maxBytes);
            _pendingBytes = 0;
            _sinceFlush = 0f;
            Pending.Clear();
            _inLine = false;
            _open = true;
            return true;
        }

        /// <summary>모아 둔 줄을 쓰고 기록을 닫는다 (장면이 내려갈 때)</summary>
        public static void Close()
        {
            if (_open) Flush();
            _open = false;
            _inLine = false;
            _path = null;
            _rotatedPath = null;
            _clock = null;
            _pendingBytes = 0;
            Pending.Clear();
            Line.Clear();
        }

        /// <summary>매 프레임 불러도 된다: 10초가 지나고 모아 둔 줄이 있을 때만 쓴다 (그 밖에는 더하기 · 비교뿐, 할당 없음)</summary>
        public static void Tick(float unscaledDeltaTime)
        {
            if (!_open) return;
            _sinceFlush += unscaledDeltaTime;
            if (_sinceFlush < FlushIntervalSeconds) return;
            _sinceFlush = 0f;
            if (_pendingBytes > 0) Flush();
        }

        /// <summary>모아 둔 줄을 지금 쓴다 (멈춤 · 종료 · 요약 · 내보내기 전). 쓰지 못하면 경고 한 줄 뒤 기록을 끈다</summary>
        public static void Flush()
        {
            _sinceFlush = 0f;
            if (!_open || _pendingBytes == 0) return;
            try
            {
                using (var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, Utf8NoBom, 8 * 1024))
                {
                    writer.Write(Pending);
                }
                _fileBytes += _pendingBytes;
            }
            catch (Exception e)
            {
                Warn("쓰기", e);
                _open = false;
            }
            Pending.Clear();
            _pendingBytes = 0;
        }

        // ───────── 줄 만들기 ─────────

        /// <summary>
        /// 새 줄을 시작한다 ("ts" · "ev"). 앞 줄을 End하지 않았으면 버린다. 열리지 않았으면 아무것도 하지 않는 줄을 돌려준다
        /// </summary>
        public static PlayLogLine Begin(string ev)
        {
            if (!_open || string.IsNullOrEmpty(ev))
            {
                _inLine = false;
                return default;
            }
            Line.Clear();
            Line.Append("{\"ts\":");
            AppendLong(Line, NowUnix());
            Line.Append(",\"ev\":");
            AppendString(Line, ev);
            _inLine = true;
            return default;
        }

        /// <summary>줄 하나 (Begin이 돌려준다). 값 쓰기는 이어서 부를 수 있고 End로 끝낸다. 할당 없는 구조체</summary>
        public readonly struct PlayLogLine
        {
            public PlayLogLine Int(string key, long value)
            {
                if (Key(key)) AppendLong(Line, value);
                return this;
            }

            /// <summary>소수 decimals자리까지 (0~4, 반올림, 할당 없음). NaN · 무한은 0</summary>
            public PlayLogLine Num(string key, double value, int decimals = 2)
            {
                if (Key(key)) AppendFixed(Line, value, decimals);
                return this;
            }

            public PlayLogLine Str(string key, string value)
            {
                if (Key(key)) AppendString(Line, value);
                return this;
            }

            public PlayLogLine Bool(string key, bool value)
            {
                if (Key(key)) Line.Append(value ? "true" : "false");
                return this;
            }

            /// <summary>정수 배열 (null이면 [])</summary>
            public PlayLogLine Ints(string key, IReadOnlyList<int> values)
            {
                if (!Key(key)) return this;
                Line.Append('[');
                if (values != null)
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (i > 0) Line.Append(',');
                        AppendLong(Line, values[i]);
                    }
                }
                Line.Append(']');
                return this;
            }

            /// <summary>줄을 끝내고 모아 둔다 (크기가 넘으면 쓰고 돌린다)</summary>
            public void End() => EndLine();
        }

        static bool Key(string key)
        {
            if (!_inLine || string.IsNullOrEmpty(key)) return false;
            Line.Append(',');
            AppendString(Line, key);
            Line.Append(':');
            return true;
        }

        static void EndLine()
        {
            if (!_inLine) return;
            _inLine = false;
            if (!_open) return;
            Line.Append('}');
            long bytes = Utf8Length(Line) + 1;
            // 이 줄을 더하면 1 MB를 넘는다: 지금까지를 쓰고 돌린 뒤 새 파일에 넣는다
            if (_fileBytes + _pendingBytes + bytes > _maxBytes && _fileBytes + _pendingBytes > 0)
            {
                Flush();
                if (!_open) return;
                Rotate();
                if (!_open) return;
            }
            Pending.Append(Line).Append('\n');
            _pendingBytes += bytes;
            if (_pendingBytes >= PendingFlushBytes) Flush();
        }

        static void Rotate()
        {
            try
            {
                if (File.Exists(_rotatedPath)) File.Delete(_rotatedPath);
                if (File.Exists(_path)) File.Move(_path, _rotatedPath);
                _fileBytes = 0;
            }
            catch (Exception e)
            {
                Warn("돌리기", e);
                _open = false;
            }
        }

        static long NowUnix()
        {
            try
            {
                return _clock != null ? TimeUtil.ToUnix(_clock.UtcNow) : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ───────── 할당 없는 JSON 글자 쓰기 ─────────

        static void AppendLong(StringBuilder sb, long value)
        {
            if (value == 0)
            {
                sb.Append('0');
                return;
            }
            if (value == long.MinValue)
            {
                sb.Append("-9223372036854775808");
                return;
            }
            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }
            long div = 1;
            while (value / div >= 10) div *= 10;
            while (div > 0)
            {
                sb.Append((char)('0' + (int)(value / div % 10)));
                div /= 10;
            }
        }

        static readonly long[] Pow10 = { 1, 10, 100, 1000, 10000 };

        static void AppendFixed(StringBuilder sb, double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                sb.Append('0');
                return;
            }
            decimals = Math.Max(0, Math.Min(4, decimals));
            long scale = Pow10[decimals];
            double scaled = Math.Round(Math.Abs(value) * scale, MidpointRounding.AwayFromZero);
            if (scaled >= long.MaxValue / 2)
            {
                // 너무 큰 값은 정수 부분만 (전투력 같은 값은 Int로 쓴다)
                AppendLong(sb, value < 0 ? long.MinValue + 1 : long.MaxValue);
                return;
            }
            long units = (long)scaled;
            if (value < 0 && units > 0) sb.Append('-');
            AppendLong(sb, units / scale);
            if (decimals == 0) return;
            long frac = units % scale;
            sb.Append('.');
            for (long d = scale / 10; d > 0; d /= 10)
            {
                sb.Append((char)('0' + (int)(frac / d % 10)));
            }
        }

        static void AppendString(StringBuilder sb, string value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u00");
                            sb.Append(Hex(c >> 4));
                            sb.Append(Hex(c & 0xF));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        static char Hex(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);

        /// <summary>UTF-8로 쓸 때의 바이트 수 (대리 쌍은 4바이트)</summary>
        static long Utf8Length(StringBuilder sb)
        {
            long bytes = 0;
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (c < 0x80) bytes += 1;
                else if (c < 0x800) bytes += 2;
                else if (char.IsHighSurrogate(c) && i + 1 < sb.Length && char.IsLowSurrogate(sb[i + 1]))
                {
                    bytes += 4;
                    i++;
                }
                else bytes += 3;
            }
            return bytes;
        }

        static void Warn(string what, Exception e) => Debug.LogWarning($"[PlayLog] 플레이 기록 {what} 실패 — 기록을 꺼요: {e.Message}");

        // ───────── 읽기 (개발 메뉴 · 내보내기 — 누를 때만, 할당 신경 쓰지 않음) ─────────

        /// <summary>
        /// 최근 hours시간 요약 (설정 개발 탭 '플레이 기록' 팝업 본문, SPEC4 §4.4):
        /// '최근 24시간 · 스테이지 클리어 42 · 실패 3 · 시간당 6.1스테이지' + 튜토리얼 단계 표 (첫 10분, 단계 사이 걸린 초).
        /// 기록이 없으면(열리지 않음 · 그 시간 안에 줄이 없음) 빈 문자열
        /// </summary>
        public static string Summary(int hours = 24)
        {
            try
            {
                var lines = ReadAllLines();
                if (lines.Count == 0) return string.Empty;
                hours = Math.Max(1, hours);
                long now = NowUnix();
                long from = now - hours * 3600L;
                int clears = 0, fails = 0;
                double battleSeconds = 0;
                int any = 0;
                var steps = new List<(int n, double t)>();
                foreach (var line in lines)
                {
                    JObject row;
                    try
                    {
                        row = JObject.Parse(line);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    long t = row.Value<long?>("ts") ?? 0;
                    string ev = row.Value<string>("ev");
                    if (ev == "ftue_step")
                    {
                        // 튜토리얼 표는 시간 창과 상관없이 마지막 진행분 (단계 번호가 줄면 새 진행)
                        int n = row.Value<int?>("n") ?? 0;
                        double at = row.Value<double?>("t") ?? 0;
                        if (steps.Count > 0 && n <= steps[steps.Count - 1].n) steps.Clear();
                        steps.Add((n, at));
                    }
                    if (t < from || t > now + 60) continue;
                    any++;
                    if (ev != "stage_end") continue;
                    string res = row.Value<string>("res");
                    if (res == "clear") clears++;
                    else if (res == "fail") fails++;
                    battleSeconds += Math.Max(0, row.Value<double?>("sec") ?? 0);
                }
                if (any == 0 && steps.Count == 0) return string.Empty;

                var text = new StringBuilder();
                text.Append("최근 ").Append(hours.ToString(CultureInfo.InvariantCulture)).Append("시간 · 스테이지 클리어 ")
                    .Append(clears.ToString(CultureInfo.InvariantCulture)).Append(" · 실패 ").Append(fails.ToString(CultureInfo.InvariantCulture));
                if (battleSeconds >= 60)
                {
                    double perHour = clears / (battleSeconds / 3600.0);
                    text.Append(" · 시간당 ").Append(perHour.ToString("0.0", CultureInfo.InvariantCulture)).Append("스테이지");
                }
                if (steps.Count > 0)
                {
                    text.Append("\n\n튜토리얼 단계 (처음부터 · 앞 단계부터)");
                    double previous = 0;
                    foreach (var (n, at) in steps)
                    {
                        text.Append('\n').Append(n.ToString(CultureInfo.InvariantCulture)).Append("단계  ")
                            .Append(Clock(at)).Append("  (+").Append(Clock(at - previous)).Append(')');
                        previous = at;
                    }
                }
                return text.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayLog] 요약을 만들지 못했어요: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>마지막 maxLines줄 (내보내기: 공유 시트 · 복사, SPEC4 §4.4 — 기본 500줄). 기록이 없으면 빈 문자열</summary>
        public static string Tail(int maxLines = ExportLineCount)
        {
            try
            {
                var lines = ReadAllLines();
                if (lines.Count == 0 || maxLines <= 0) return string.Empty;
                int start = Math.Max(0, lines.Count - maxLines);
                return string.Join("\n", lines.GetRange(start, lines.Count - start));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayLog] 기록을 읽지 못했어요: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>모아 둔 줄을 쓴 뒤 돌린 파일 → 지금 파일 순으로 모든 줄 (빈 줄 제외)</summary>
        static List<string> ReadAllLines()
        {
            var lines = new List<string>();
            if (!_open || string.IsNullOrEmpty(_path)) return lines;
            Flush();
            foreach (var file in new[] { _rotatedPath, _path })
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) continue;
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Utf8NoBom);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length > 0) lines.Add(line);
                }
            }
            return lines;
        }

        static string Clock(double seconds)
        {
            int s = (int)Math.Max(0, Math.Round(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
