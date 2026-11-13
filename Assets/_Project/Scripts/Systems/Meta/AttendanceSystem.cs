using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>도장 한 번의 결과</summary>
    public sealed class AttendanceCheckInResult
    {
        /// <summary>이번에 찍힌 새싹 출석 일차 (0 = 새싹 출석은 이미 끝남)</summary>
        public int StarterDay;
        public AttendanceDayDef StarterDef;
        /// <summary>이번에 찍힌 정규 출석 일차 (1~28)</summary>
        public int MonthlyDay;
        public AttendanceDayDef MonthlyDef;
        /// <summary>실제로 받은 보상 (두 출석부 합계, 같은 종류 합침)</summary>
        public List<MetaReward> Rewards = new();
    }

    /// <summary>
    /// 출석부 (Q-03). 하루 1번(05:00 기준) 도장을 찍으면 새싹 출석(7일, 한 번만)과 정규 출석(28일 반복)에 함께 찍히고 그 칸 보상을 바로 받는다.
    ///   · 누적 일수로 센다 (빠진 날이 있어도 초기화되지 않음). 정규 28일을 채우면 다음 도장은 새 회차 1일째.
    ///   · 새싹 7일째 = 루즈리 확정 (이미 있으면 기억편 — MetaRewardType.Lineage가 처리).
    ///   · 출석부(FeatureId.Attendance)가 열려야 하고, 되돌림(IsTampered) 중에는 찍을 수 없다.
    ///   · 도장 = AttendanceCheckIn 카운터 (주간 미션 "출석 5일").
    ///   · 접속할 때 오늘 안 찍었으면 MetaSession이 PopupQueue에 출석부를 넣는다 (F-05 둘째).
    /// <para>
    /// 새싹 출석 칸 데이터가 없으면 새싹 쪽은 건너뛰고 정규만 찍는다. 두 출석부 모두 칸 데이터가 없으면 찍을 수 없다.
    /// "오늘 칸"(IsToday)은 찍기 전에는 다음에 찍힐 칸, 찍은 뒤에는 오늘 찍힌 칸이다.
    /// 정규 28칸을 다 채운 다음 날(찍기 전)에는 화면 값(MonthlyDays/MonthlyCycle/IsStamped)이 이미 새 회차(빈 판)를 가리킨다.
    /// </para>
    /// </summary>
    public class AttendanceSystem
    {
        public const int StarterLength = 7;
        public const int MonthlyLength = 28;

        /// <summary>도장/회차 상태가 바뀜 (날이 바뀌어 다시 찍을 수 있게 된 때 포함)</summary>
        public event Action Changed;
        /// <summary>도장을 찍음 (도장 '꾹' 연출 + 보상 날아가기)</summary>
        public event Action<AttendanceCheckInResult> CheckedIn;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;

        readonly List<MetaReward> _pending = new();

        public AttendanceSystem(PlayerData data, MetaDatabase db, DailyClock clock, ProgressTracker progress, MetaRewardService rewards,
            FeatureGateSystem features)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _progress = progress;
            _rewards = rewards;
            _features = features;

            if (_clock != null)
            {
                _clock.DayChanged += OnDayChanged;
                _clock.TamperChanged += OnTamperChanged;
            }
            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
        }

        AttendanceSave Save => (_data.meta ??= new MetaSave()).attendance ??= new AttendanceSave();

        /// <summary>출석부가 열렸는지 (FeatureId.Attendance)</summary>
        public bool IsUnlocked => _features == null || _features.IsUnlocked(FeatureId.Attendance);

        /// <summary>오늘 이미 도장을 찍음</summary>
        public bool HasCheckedInToday
        {
            get
            {
                string last = Save.lastCheckInDayKey;
                return !string.IsNullOrEmpty(last) && _clock != null && last == _clock.DayKey;
            }
        }

        /// <summary>지금 찍을 수 있음 (열림 + 오늘 안 찍음 + 되돌림 아님 + 칸 데이터가 있음)</summary>
        public bool CanCheckIn =>
            IsUnlocked && !HasCheckedInToday && (_clock == null || !_clock.IsTampered) && HasDays;

        public int StarterDays => Save.starterDays;

        public bool IsStarterComplete => Save.starterDays >= GetLength(AttendanceTrack.Starter);

        /// <summary>
        /// 정규 출석 이번 회차 도장 수 (0~28, 화면 기준). 지난 회차를 다 채우고 오늘 아직 안 찍었으면 새 회차라 0
        /// (저장값은 28 그대로이고 다음 도장이 새 회차 1일째가 된다)
        /// </summary>
        public int MonthlyDays => IsMonthlyRolloverPending ? 0 : Save.monthlyDays;

        /// <summary>정규 출석 회차 (0부터, 화면 기준). 지난 회차를 다 채우고 오늘 아직 안 찍었으면 이미 새 회차로 센다</summary>
        public int MonthlyCycle => IsMonthlyRolloverPending ? Save.monthlyCycle + 1 : Save.monthlyCycle;

        /// <summary>
        /// 정규 출석 한 회차를 다 채웠고 오늘은 아직 안 찍음 — 판을 새 회차(빈 판, 1일째 칸이 오늘)로 보여 준다.
        /// 그러지 않으면 지난 회차 28칸이 모두 찍힌 채 오늘 칸(1일째)까지 "받았어요"로 보여 다 받은 줄 알고 닫게 된다.
        /// </summary>
        bool IsMonthlyRolloverPending => Save.monthlyDays >= GetLength(AttendanceTrack.Monthly) && !HasCheckedInToday;

        /// <summary>평생 도장 수</summary>
        public int TotalDays => Save.totalDays;

        /// <summary>출석부 칸 수 (콘텐츠 길이, 없으면 StarterLength / MonthlyLength)</summary>
        public int GetLength(AttendanceTrack track)
        {
            var days = _db != null ? _db.GetAttendance(track) : null;
            if (days != null && days.Count > 0) return Math.Max(1, days[days.Count - 1].day);
            return track == AttendanceTrack.Starter ? StarterLength : MonthlyLength;
        }

        /// <summary>다음 도장이 찍힐 일차 (1부터). 새싹이 끝났으면 0</summary>
        public int GetNextDay(AttendanceTrack track)
        {
            var save = Save;
            if (track == AttendanceTrack.Starter) return IsStarterComplete ? 0 : save.starterDays + 1;
            return save.monthlyDays >= GetLength(AttendanceTrack.Monthly) ? 1 : save.monthlyDays + 1;
        }

        /// <summary>day일째 칸에 도장이 찍혔는지 (이번 회차 기준 — 다 채운 회차 다음 날 찍기 전에는 빈 판)</summary>
        public bool IsStamped(AttendanceTrack track, int day)
        {
            if (day < 1) return false;
            return track == AttendanceTrack.Starter ? day <= Save.starterDays : day <= MonthlyDays;
        }

        /// <summary>오늘 찍을 칸인지 (빛나는 칸)</summary>
        public bool IsToday(AttendanceTrack track, int day)
        {
            if (day < 1 || !IsUnlocked) return false;
            var save = Save;
            if (HasCheckedInToday)
            {
                if (track == AttendanceTrack.Monthly) return day == save.monthlyDays;
                // 새싹은 처음 도장들과 함께 찍히므로, 오늘 도장이 새싹 칸이었는지는 평생 도장 수로 안다
                return HasTrack(AttendanceTrack.Starter) && save.totalDays == save.starterDays && day == save.starterDays;
            }
            if (track == AttendanceTrack.Starter && (IsStarterComplete || !HasTrack(AttendanceTrack.Starter))) return false;
            return day == GetNextDay(track);
        }

        public AttendanceDayDef GetDay(AttendanceTrack track, int day) => _db != null ? _db.GetAttendanceDay(track, day) : null;

        /// <summary>도장을 찍고 보상을 준다. 찍을 수 없으면 null</summary>
        public AttendanceCheckInResult CheckIn()
        {
            if (!CanCheckIn) return null;
            var save = Save;
            var result = new AttendanceCheckInResult();
            _pending.Clear();

            if (HasTrack(AttendanceTrack.Starter) && !IsStarterComplete)
            {
                int day = save.starterDays + 1;
                save.starterDays = day;
                result.StarterDay = day;
                result.StarterDef = GetDay(AttendanceTrack.Starter, day);
                if (result.StarterDef?.rewards != null) _pending.AddRange(result.StarterDef.rewards);
            }

            int monthlyLength = GetLength(AttendanceTrack.Monthly);
            if (save.monthlyDays >= monthlyLength)
            {
                save.monthlyDays = 0;
                save.monthlyCycle++;
            }
            int monthlyDay = save.monthlyDays + 1;
            save.monthlyDays = monthlyDay;
            result.MonthlyDay = monthlyDay;
            result.MonthlyDef = GetDay(AttendanceTrack.Monthly, monthlyDay);
            if (result.MonthlyDef?.rewards != null) _pending.AddRange(result.MonthlyDef.rewards);

            save.lastCheckInDayKey = _clock != null ? _clock.DayKey : save.lastCheckInDayKey;
            save.totalDays++;

            result.Rewards = _rewards != null ? _rewards.Grant(_pending) : MetaRewardService.Merge(_pending);
            _pending.Clear();
            _progress?.Increment(CounterKey.AttendanceCheckIn);
            _progress?.MarkDirty();

            CheckedIn?.Invoke(result);
            Changed?.Invoke();
            return result;
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        bool HasTrack(AttendanceTrack track)
        {
            var days = _db != null ? _db.GetAttendance(track) : null;
            return days != null && days.Count > 0;
        }

        /// <summary>찍을 칸 데이터가 있음 (새싹이 남았거나 정규 칸이 있음)</summary>
        bool HasDays => (HasTrack(AttendanceTrack.Starter) && !IsStarterComplete) || HasTrack(AttendanceTrack.Monthly);

        void OnDayChanged(string dayKey) => Changed?.Invoke();

        void OnTamperChanged(bool tampered) => Changed?.Invoke();

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature == FeatureId.Attendance) Changed?.Invoke();
        }
    }
}
