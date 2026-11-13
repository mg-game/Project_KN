using System;
using System.Globalization;

namespace IdleGame
{
    /// <summary>
    /// 일일 시계 (F-01). 모든 "오늘 / 이번 주"의 기준이다. 초기화 시각은 현지 05:00.
    ///   · 날 키 = (현지 시각 − 5시간)의 날짜 "yyyy-MM-dd", 주 키 = 그 날이 속한 주 월요일의 날 키 (월요일 05:00 시작).
    ///   · 시간 되돌림 방지: 본 적 있는 가장 늦은 시각(meta.clock.maxSeenUnix)을 저장한다. 지금 시각이 그보다
    ///     RollbackToleranceSeconds 넘게 이르면 IsTampered — 출석·미션 받기·무료 빠른 수선·바구니 받기를 멈춘다 (각 시스템이 확인).
    ///   · 날 키는 "유효 시각" = max(지금, maxSeen)으로 만든다. 시계를 되돌려도 날이 거꾸로 가지 않아 어제 보상을 다시 받을 수 없다.
    ///     시계를 앞으로 돌리는 것은 오프라인에서 막을 수 없으므로 바구니 상한과 하루 1회 제한으로 피해를 묶는다.
    ///   · 날·주 키는 거꾸로 가지 않는다: 시간대를 서쪽으로 바꾸면(유닉스 시각은 그대로라 되돌림이 아니다) 계산한 날 키가
    ///     저장된 마지막 날(meta.clock.lastDayKey)보다 이를 수 있다. 그때는 저장된 날에 머물러 이벤트 없이 그 날 키를 돌려주고,
    ///     새 시간대에서 그다음 날 05:00이 되어야 날이 바뀐다 (출석·일지·무료 빠른 수선·무료 두 배가 시간대 왕복으로 다시 열리지 않게).
    ///     주 키는 날 키가 속한 주 월요일이고, 저장된 마지막 주(lastWeekKey)보다도 이르지 않다.
    /// Refresh(부팅/복귀/Tick)가 날·주 변경을 감지해 DayChanged / WeekChanged를 한 번씩 알린다.
    /// 다른 시스템은 자기 "오늘" 상태에 날 키를 함께 저장하고, 이벤트를 받거나 읽을 때 키가 다르면 비운다 (구독 순서와 무관하게 안전).
    /// 테스트는 timeZone에 TimeZoneInfo.Utc를 넘겨 기기 시간대와 무관하게 만든다.
    /// 날 키 문자열은 그 날 구간 [시작, 끝)과 함께 캐시해서, 같은 날 안에서는 다시 만들지 않는다 (매 프레임 읽어도 할당 없음).
    /// 저장된 날에 머무는 동안의 구간은 [지금, 새 시간대에서 그 날 다음 날 05:00)이다. 주 키는 날 키가 바뀔 때만 다시 본다.
    /// </summary>
    public sealed class DailyClock
    {
        /// <summary>일일 초기화 시각 (현지 시)</summary>
        public const int ResetHour = 5;
        /// <summary>이만큼(초)까지는 시계가 뒤로 가도 되돌림으로 보지 않는다 (시계 동기화 오차)</summary>
        public const long RollbackToleranceSeconds = 300;
        /// <summary>Tick에서 다시 확인하는 간격 (초)</summary>
        public const float RefreshInterval = 1f;
        /// <summary>되돌림을 감지했을 때 받기 버튼에서 띄우는 토스트</summary>
        public const string TamperedMessage = "기기 시간이 이전으로 바뀌었어요. 시간이 다시 맞으면 받을 수 있어요.";

        const string DayFormat = "yyyy-MM-dd";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>날이 바뀜 (새 날 키). 부팅 때 저장된 날과 다르면 Refresh에서 한 번 알린다</summary>
        public event Action<string> DayChanged;
        /// <summary>주가 바뀜 (새 주 키). 같은 Refresh에서 DayChanged 다음에 알린다</summary>
        public event Action<string> WeekChanged;
        /// <summary>되돌림 상태가 바뀜 (true = 되돌림 감지)</summary>
        public event Action<bool> TamperChanged;

        readonly PlayerData _data;
        readonly ITimeProvider _clock;
        readonly TimeZoneInfo _timeZone;

        float _tickTimer;
        bool _tamperKnown;
        bool _lastTampered;

        // 날 구간 캐시 (유닉스 초, [시작, 끝))
        string _dayKey;
        DateTime _dayDate;
        long _dayStart = long.MaxValue;
        long _dayEnd = long.MinValue;
        // 주 캐시 (만든 기준 날 키 문자열이 바뀔 때만 다시 계산)
        string _weekKey;
        DateTime _weekDate;
        string _weekSourceDay;
        long _weekEnd = long.MinValue;

        // 설치 일수 캐시 (설치 날 키와 오늘 날 키가 같은 문자열이면 다시 계산하지 않음)
        string _installCacheDay;
        string _installCacheInstall;
        int _installCacheValue = 1;

        /// <param name="timeZone">null이면 기기 현지 시간대</param>
        public DailyClock(PlayerData data, ITimeProvider clock, TimeZoneInfo timeZone = null)
        {
            _data = data;
            _clock = clock;
            _timeZone = timeZone ?? TimeZoneInfo.Local;
        }

        /// <summary>원래 시간 공급자 (UTC)</summary>
        public ITimeProvider Time => _clock;

        public TimeZoneInfo TimeZone => _timeZone;

        MetaClockSave Save => (_data.meta ??= new MetaSave()).clock ??= new MetaClockSave();

        /// <summary>기기 시각 (유닉스 초, 되돌림 보정 없음)</summary>
        public long NowUnix => _clock != null ? TimeUtil.ToUnix(_clock.UtcNow) : 0;

        /// <summary>유효 시각 = max(기기 시각, 본 적 있는 가장 늦은 시각). 날 키와 초기화 남은 시간의 기준</summary>
        public long EffectiveUnix => Math.Max(NowUnix, Save.maxSeenUnix);

        /// <summary>기기 시각이 본 적 있는 가장 늦은 시각보다 RollbackToleranceSeconds 넘게 이르다</summary>
        public bool IsTampered
        {
            get
            {
                long maxSeen = Save.maxSeenUnix;
                return maxSeen > 0 && NowUnix < maxSeen - RollbackToleranceSeconds;
            }
        }

        /// <summary>오늘 날 키 "yyyy-MM-dd" (유효 시각 기준)</summary>
        public string DayKey
        {
            get
            {
                EnsureDay(EffectiveUnix);
                return _dayKey;
            }
        }

        /// <summary>이번 주 키 (월요일 날 키)</summary>
        public string WeekKey
        {
            get
            {
                EnsureWeek(EffectiveUnix);
                return _weekKey;
            }
        }

        /// <summary>설치 후 며칠째인지 (설치한 날 = 1)</summary>
        public int InstallDay
        {
            get
            {
                string install = Save.installDayKey;
                if (string.IsNullOrEmpty(install)) return 1;
                string today = DayKey;
                if (!ReferenceEquals(today, _installCacheDay) || !ReferenceEquals(install, _installCacheInstall))
                {
                    _installCacheDay = today;
                    _installCacheInstall = install;
                    _installCacheValue = Math.Max(1, DaysBetween(install, today) + 1);
                }
                return _installCacheValue;
            }
        }

        /// <summary>다음 일일 초기화 시각 (UTC)</summary>
        public DateTime NextDailyResetUtc
        {
            get
            {
                EnsureDay(EffectiveUnix);
                return TimeUtil.FromUnix(_dayEnd);
            }
        }

        /// <summary>다음 주간 초기화 시각 (다음 월요일 05:00, UTC)</summary>
        public DateTime NextWeeklyResetUtc
        {
            get
            {
                EnsureWeek(EffectiveUnix);
                return TimeUtil.FromUnix(_weekEnd);
            }
        }

        /// <summary>일일 초기화까지 남은 시간 (수선 일지 "초기화까지 7:12:03")</summary>
        public TimeSpan TimeUntilDailyReset
        {
            get
            {
                long now = EffectiveUnix;
                EnsureDay(now);
                return TimeSpan.FromSeconds(Math.Max(0, _dayEnd - now));
            }
        }

        public TimeSpan TimeUntilWeeklyReset
        {
            get
            {
                long now = EffectiveUnix;
                EnsureWeek(now);
                return TimeSpan.FromSeconds(Math.Max(0, _weekEnd - now));
            }
        }

        /// <summary>
        /// 지금 시각을 읽어 maxSeen을 올리고(되돌림이 아닐 때), 설치 날을 채우고, 되돌림 상태와 날·주 변경을 확인해 이벤트를 알린다.
        /// 부팅·복귀 때 직접 부르고, 평소에는 Tick이 RefreshInterval마다 부른다. 날이 바뀌었으면 true.
        /// </summary>
        public bool Refresh()
        {
            var save = Save;
            long now = NowUnix;
            bool tampered = save.maxSeenUnix > 0 && now < save.maxSeenUnix - RollbackToleranceSeconds;
            // 허용 오차 안에서 조금 뒤로 간 것은 maxSeen을 낮추지 않는다 (올리기만)
            if (now > save.maxSeenUnix) save.maxSeenUnix = now;

            if (string.IsNullOrEmpty(save.installDayKey))
            {
                save.installDayKey = DayKey;
                save.installUnix = EffectiveUnix;
            }

            if (!_tamperKnown || tampered != _lastTampered)
            {
                bool notify = _tamperKnown || tampered;
                _tamperKnown = true;
                _lastTampered = tampered;
                if (notify) TamperChanged?.Invoke(tampered);
            }

            bool dayChanged = false;
            string day = DayKey;
            if (save.lastDayKey != day)
            {
                save.lastDayKey = day;
                dayChanged = true;
                DayChanged?.Invoke(day);
            }
            else if (!ReferenceEquals(save.lastDayKey, day))
            {
                save.lastDayKey = day; // 다음 비교가 참조 비교로 끝나게
            }

            string week = WeekKey;
            if (save.lastWeekKey != week)
            {
                save.lastWeekKey = week;
                WeekChanged?.Invoke(week);
            }
            else if (!ReferenceEquals(save.lastWeekKey, week))
            {
                save.lastWeekKey = week;
            }
            return dayChanged;
        }

        /// <summary>매 프레임 (실제 시간 dt). RefreshInterval마다 Refresh. 할당 없음</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!(unscaledDeltaTime > 0f) || float.IsInfinity(unscaledDeltaTime)) return;
            _tickTimer += unscaledDeltaTime;
            if (_tickTimer < RefreshInterval) return;
            _tickTimer = 0f;
            Refresh();
        }

        /// <summary>유닉스 초 → 현지 시각 (이 시계의 시간대)</summary>
        public DateTime ToLocal(long unixSeconds) => TimeZoneInfo.ConvertTimeFromUtc(TimeUtil.FromUnix(unixSeconds), _timeZone);

        /// <summary>유닉스 초가 속한 날 키</summary>
        public string DayKeyOfUnix(long unixSeconds)
        {
            if (unixSeconds >= _dayStart && unixSeconds < _dayEnd) return _dayKey;
            return DayKeyOf(ToLocal(unixSeconds));
        }

        /// <summary>현지 시각 → 날 키 (현지 − 5시간의 날짜). 04:59는 전날, 05:00은 그날</summary>
        public static string DayKeyOf(DateTime localTime) => ResetDate(localTime).ToString(DayFormat, Invariant);

        /// <summary>현지 시각 → 주 키 (현지 − 5시간이 속한 주 월요일의 날 키)</summary>
        public static string WeekKeyOf(DateTime localTime) => MondayOf(ResetDate(localTime)).ToString(DayFormat, Invariant);

        /// <summary>두 날 키 사이 일수 (b − a). 형식이 틀리면 0</summary>
        public static int DaysBetween(string dayKeyA, string dayKeyB)
        {
            if (!TryParseDay(dayKeyA, out var a) || !TryParseDay(dayKeyB, out var b)) return 0;
            return (int)Math.Round((b - a).TotalDays);
        }

        // ── 4차 일정 도우미 (EVT-01 이벤트 달력 · 첫 주 · 작은 선물 — 날 키 = "게임 날짜", DateTime.Date 값) ──

        /// <summary>오늘 날 키의 날짜 (게임 날짜 = 현지 − 5시간의 날짜, 시간대 변경으로 저장된 날에 머물면 그 날). 할당 없음</summary>
        public DateTime DayDate
        {
            get
            {
                EnsureDay(EffectiveUnix);
                return _dayDate;
            }
        }

        /// <summary>날 키 "yyyy-MM-dd" → 날짜. 형식이 틀리면 false</summary>
        public static bool TryParseDayKey(string dayKey, out DateTime date) => TryParseDay(dayKey, out date);

        /// <summary>날짜 → 날 키 "yyyy-MM-dd"</summary>
        public static string DayKeyOfDate(DateTime gameDate) => gameDate.Date.ToString(DayFormat, Invariant);

        /// <summary>날 키에 days일을 더한 날 키. 형식이 틀리면 null</summary>
        public static string AddDays(string dayKey, int days) =>
            TryParseDay(dayKey, out var date) ? date.AddDays(days).ToString(DayFormat, Invariant) : null;

        /// <summary>날짜의 요일 (월요일 = 0 … 일요일 = 6)</summary>
        public static int WeekdayOf(DateTime gameDate) => ((int)gameDate.DayOfWeek + 6) % 7;

        /// <summary>날 키의 요일 (월요일 = 0 … 일요일 = 6). 형식이 틀리면 -1</summary>
        public static int WeekdayOfKey(string dayKey) => TryParseDay(dayKey, out var date) ? WeekdayOf(date) : -1;

        /// <summary>유닉스 초가 속한 게임 날짜 (현지 − 5시간의 날짜, 저장된 마지막 날에 머무는 규칙은 적용하지 않는 순수 계산). 할당 없음</summary>
        public DateTime GameDateOf(long unixSeconds) => ResetDate(ToLocal(unixSeconds));

        /// <summary>게임 날짜가 시작되는 유닉스 초 (그 날짜 현지 05:00, 서머타임으로 없는 시각이면 1시간 뒤). 할당 없음</summary>
        public long DayStartUnix(DateTime gameDate) => LocalToUnix(gameDate.Date.AddHours(ResetHour));

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>초기화 시각을 뺀 "게임 날짜" (현지 − 5시간의 날짜)</summary>
        static DateTime ResetDate(DateTime localTime) => localTime.AddHours(-ResetHour).Date;

        static DateTime MondayOf(DateTime date)
        {
            int offset = ((int)date.DayOfWeek + 6) % 7; // 월 0 … 일 6
            return date.AddDays(-offset);
        }

        static bool TryParseDay(string key, out DateTime date) =>
            DateTime.TryParseExact(key, DayFormat, Invariant, DateTimeStyles.None, out date);

        /// <summary>
        /// unix가 속한 날 구간을 캐시한다. 계산한 날이 저장된 마지막 날(lastDayKey)보다 이르면 그 날에 머문다
        /// (시간대 변경으로 날 키가 거꾸로 가지 않게). 구간이 바뀔 때만 계산하고, 같은 날이면 문자열을 다시 만들지 않는다.
        /// </summary>
        void EnsureDay(long unix)
        {
            if (_dayKey != null && unix >= _dayStart && unix < _dayEnd) return;
            var date = ResetDate(ToLocal(unix));
            string key = null;
            string floorKey = Save.lastDayKey;
            if (!string.IsNullOrEmpty(floorKey) && TryParseDay(floorKey, out var floor) && date < floor)
            {
                date = floor;
                key = floorKey; // 저장된 문자열을 그대로 써서 Refresh 비교가 참조 비교로 끝나고 이벤트가 울리지 않는다
            }
            if (key == null) key = _dayKey != null && date == _dayDate ? _dayKey : date.ToString(DayFormat, Invariant);
            _dayKey = key;
            _dayDate = date;
            _dayStart = LocalToUnix(date.AddHours(ResetHour));
            _dayEnd = LocalToUnix(date.AddDays(1).AddHours(ResetHour));
            FixRange(unix, ref _dayStart, ref _dayEnd);
        }

        /// <summary>
        /// 이번 주 = 날 키가 속한 주 월요일 (저장된 마지막 주보다 이르면 그 주에 머문다). 날 키 문자열이 바뀔 때만 다시 계산한다.
        /// </summary>
        void EnsureWeek(long unix)
        {
            EnsureDay(unix);
            if (_weekKey != null && ReferenceEquals(_weekSourceDay, _dayKey)) return;
            _weekSourceDay = _dayKey;

            var monday = MondayOf(_dayDate);
            string key = null;
            string floorKey = Save.lastWeekKey;
            if (!string.IsNullOrEmpty(floorKey) && TryParseDay(floorKey, out var floor) && monday < floor)
            {
                monday = floor;
                key = floorKey;
            }
            if (key == null) key = _weekKey != null && monday == _weekDate ? _weekKey : monday.ToString(DayFormat, Invariant);
            _weekKey = key;
            _weekDate = monday;
            long start = LocalToUnix(monday.AddHours(ResetHour));
            _weekEnd = LocalToUnix(monday.AddDays(7).AddHours(ResetHour));
            FixRange(unix, ref start, ref _weekEnd);
        }

        /// <summary>서머타임 경계 같은 드문 경우에 구간이 지금을 품지 못하면 넓힌다 (다음 확인 때 다시 계산)</summary>
        static void FixRange(long unix, ref long start, ref long end)
        {
            if (start > unix) start = unix;
            if (end <= unix) end = unix + 3600;
        }

        /// <summary>이 시계 시간대의 현지 시각 → 유닉스 초. 서머타임으로 없는 시각이면 1시간 뒤로 민다 (겹치는 시각은 표준시 쪽)</summary>
        public long LocalToUnix(DateTime local)
        {
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    return TimeUtil.ToUnix(TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZone));
                }
                catch (ArgumentException)
                {
                    unspecified = unspecified.AddHours(1);
                }
            }
            return TimeUtil.ToUnix(DateTime.SpecifyKind(local, DateTimeKind.Utc));
        }
    }
}
