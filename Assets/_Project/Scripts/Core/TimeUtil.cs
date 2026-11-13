using System;

namespace IdleGame
{
    /// <summary>
    /// 시간 공급자. 기기 시간 조작을 막으려면 서버 시간 기반 구현으로 교체한다.
    /// </summary>
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }

    public sealed class LocalTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public static class TimeUtil
    {
        public static long ToUnix(DateTime time)
        {
            var utc = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);
            return new DateTimeOffset(utc).ToUnixTimeSeconds();
        }

        public static DateTime FromUnix(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

        /// <summary>예) 2일 3시간, 5시간 12분, 3분 20초</summary>
        public static string FormatDuration(TimeSpan span)
        {
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}일 {span.Hours}시간";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}시간 {span.Minutes}분";
            if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}분 {span.Seconds}초";
            return $"{Math.Max(0, span.Seconds)}초";
        }

        /// <summary>예) 29일 남음, 5시간 남음, 12분 남음</summary>
        public static string FormatRemain(TimeSpan span)
        {
            if (span == TimeSpan.MaxValue) return "무기한";
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}일 남음";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}시간 남음";
            return $"{Math.Max(1, (int)span.TotalMinutes)}분 남음";
        }
    }
}
