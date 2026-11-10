using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>일일 시계 (F-01): 05:00 경계, 주 경계(월 05:00), 되돌림 감지·복구, 설치 일수, 남은 시간, 시간대, 시간대를 바꿔도 날·주가 거꾸로 가지 않음</summary>
    public class DailyClockTests : MetaSystemsTestBase
    {
        readonly List<string> _events = new();

        /// <summary>NUnit은 픽스처 인스턴스 하나로 모든 테스트를 돌린다 — 기록은 테스트마다 비운다 (기반 SetUp 다음에 불림)</summary>
        [SetUp]
        public void ClearEvents() => _events.Clear();

        void Listen()
        {
            DailyClock.DayChanged += key => _events.Add("day:" + key);
            DailyClock.WeekChanged += key => _events.Add("week:" + key);
            DailyClock.TamperChanged += tampered => _events.Add("tamper:" + tampered);
        }

        [Test]
        public void DayKey_ChangesAtFiveAm()
        {
            SetUtc(2026, 11, 3, 4, 59, 59);
            Assert.AreEqual("2026-11-02", DailyClock.DayKey, "04:59는 전날");

            AdvanceSeconds(1);
            Assert.AreEqual("2026-11-03", DailyClock.DayKey, "05:00부터 그날");

            SetUtc(2026, 11, 3, 23, 59, 59);
            Assert.AreEqual("2026-11-03", DailyClock.DayKey, "자정이 지나도 05:00 전까지는 같은 날");
            SetUtc(2026, 11, 4, 0, 30);
            Assert.AreEqual("2026-11-03", DailyClock.DayKey);
        }

        [Test]
        public void StaticKeys_MatchInstanceKeys()
        {
            Assert.AreEqual("2026-11-02", DailyClock.DayKeyOf(new DateTime(2026, 11, 3, 4, 59, 0)));
            Assert.AreEqual("2026-11-03", DailyClock.DayKeyOf(new DateTime(2026, 11, 3, 5, 0, 0)));
            Assert.AreEqual("2026-10-26", DailyClock.WeekKeyOf(new DateTime(2026, 11, 2, 4, 59, 0)), "월요일 05:00 전은 지난주");
            Assert.AreEqual("2026-11-02", DailyClock.WeekKeyOf(new DateTime(2026, 11, 2, 5, 0, 0)));
            Assert.AreEqual("2026-11-02", DailyClock.WeekKeyOf(new DateTime(2026, 11, 9, 4, 0, 0)), "다음 월요일 05:00 전까지 같은 주");
        }

        [Test]
        public void WeekKey_ChangesMondayAtFiveAm()
        {
            SetUtc(2026, 11, 2, 4, 59, 59); // 월요일
            Assert.AreEqual("2026-10-26", DailyClock.WeekKey);
            AdvanceSeconds(1);
            Assert.AreEqual("2026-11-02", DailyClock.WeekKey);
            SetUtc(2026, 11, 8, 23, 0); // 일요일 밤
            Assert.AreEqual("2026-11-02", DailyClock.WeekKey);
        }

        [Test]
        public void Refresh_RaisesDayThenWeekOnce()
        {
            SetUtc(2026, 11, 1, 12, 0); // 일요일
            DailyClock.Refresh();
            Listen();

            Assert.IsFalse(DailyClock.Refresh(), "같은 날 다시 확인하면 아무 일도 없다");
            CollectionAssert.IsEmpty(_events);

            SetUtc(2026, 11, 2, 5, 0); // 월요일 05:00
            Assert.IsTrue(DailyClock.Refresh());
            CollectionAssert.AreEqual(new[] { "day:2026-11-02", "week:2026-11-02" }, _events, "날 다음에 주");

            _events.Clear();
            Assert.IsFalse(DailyClock.Refresh());
            CollectionAssert.IsEmpty(_events);

            SetUtc(2026, 11, 3, 6, 0);
            Assert.IsTrue(DailyClock.Refresh());
            CollectionAssert.AreEqual(new[] { "day:2026-11-03" }, _events, "주는 그대로");
        }

        [Test]
        public void Refresh_SkippingSeveralDays_RaisesDayChangedOnce()
        {
            SetUtc(2026, 11, 3, 6, 0);
            DailyClock.Refresh();
            Listen();

            SetUtc(2026, 11, 7, 6, 0);
            DailyClock.Refresh();

            CollectionAssert.AreEqual(new[] { "day:2026-11-07" }, _events);
            Assert.AreEqual("2026-11-07", Data.meta.clock.lastDayKey);
        }

        [Test]
        public void Tick_RefreshesEverySecond()
        {
            SetUtc(2026, 11, 3, 4, 59, 59);
            DailyClock.Refresh();
            Listen();
            AdvanceSeconds(2);

            DailyClock.Tick(0.5f);
            CollectionAssert.IsEmpty(_events, "1초가 안 지나면 확인하지 않는다");
            DailyClock.Tick(0.6f);
            CollectionAssert.AreEqual(new[] { "day:2026-11-03" }, _events);
        }

        [Test]
        public void Rollback_BeyondTolerance_IsTampered_AndDayDoesNotGoBack()
        {
            SetUtc(2026, 11, 3, 6, 0);
            DailyClock.Refresh();
            Listen();
            long maxSeen = Data.meta.clock.maxSeenUnix;

            AdvanceSeconds(-DailyClock.RollbackToleranceSeconds);
            DailyClock.Refresh();
            Assert.IsFalse(DailyClock.IsTampered, "300초까지는 시계 오차로 본다");
            Assert.AreEqual(maxSeen, Data.meta.clock.maxSeenUnix, "maxSeen은 낮아지지 않는다");

            SetUtc(2026, 11, 2, 6, 0); // 하루 전으로 되돌림
            DailyClock.Refresh();
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.AreEqual("2026-11-03", DailyClock.DayKey, "유효 시각은 본 적 있는 가장 늦은 시각");
            Assert.AreEqual(maxSeen, DailyClock.EffectiveUnix);
            CollectionAssert.AreEqual(new[] { "tamper:True" }, _events, "되돌림이어도 날은 바뀌지 않는다");
        }

        [Test]
        public void Rollback_RecoversWhenClockCatchesUp()
        {
            SetUtc(2026, 11, 3, 6, 0);
            DailyClock.Refresh();
            Listen();

            SetUtc(2026, 11, 3, 5, 0);
            DailyClock.Refresh();
            Assert.IsTrue(DailyClock.IsTampered);

            SetUtc(2026, 11, 3, 6, 1);
            DailyClock.Refresh();
            Assert.IsFalse(DailyClock.IsTampered);
            CollectionAssert.AreEqual(new[] { "tamper:True", "tamper:False" }, _events);
            Assert.AreEqual(TimeUtil.ToUnix(Clock.UtcNow), Data.meta.clock.maxSeenUnix, "정상으로 돌아오면 다시 올라간다");
        }

        [Test]
        public void TamperedAtBoot_NotifiesOnFirstRefresh()
        {
            SetUtc(2026, 11, 3, 6, 0);
            Data.meta.clock.maxSeenUnix = TimeUtil.ToUnix(Clock.UtcNow) + 3600;
            Listen();

            DailyClock.Refresh();

            Assert.IsTrue(DailyClock.IsTampered);
            CollectionAssert.Contains(_events, "tamper:True");
        }

        [Test]
        public void ForwardClock_IsNotTampered()
        {
            SetUtc(2026, 11, 3, 6, 0);
            DailyClock.Refresh();
            Advance(24 * 30);
            DailyClock.Refresh();
            Assert.IsFalse(DailyClock.IsTampered, "앞으로 돌리는 것은 막을 수 없다 (상한·하루 1회로 묶는다)");
            Assert.AreEqual("2026-12-03", DailyClock.DayKey);
        }

        [Test]
        public void InstallDay_CountsResetDays()
        {
            SetUtc(2026, 11, 3, 4, 0); // 05:00 전이라 11-02에 설치
            Assert.AreEqual(1, DailyClock.InstallDay, "설치 전에도 1");
            DailyClock.Refresh();
            Assert.AreEqual("2026-11-02", Data.meta.clock.installDayKey);
            Assert.AreEqual(1, DailyClock.InstallDay);

            SetUtc(2026, 11, 3, 5, 0);
            Assert.AreEqual(2, DailyClock.InstallDay, "05:00이 지나면 2일째");

            SetUtc(2026, 11, 5, 12, 0);
            Assert.AreEqual(4, DailyClock.InstallDay);

            DailyClock.Refresh();
            Assert.AreEqual("2026-11-02", Data.meta.clock.installDayKey, "설치 날은 처음 한 번만 기록");
        }

        [Test]
        public void TimeUntilReset_CountsToNextFiveAm()
        {
            SetUtc(2026, 11, 3, 3, 0); // 화요일 03:00 (아직 월요일 게임 날)
            Assert.AreEqual(TimeSpan.FromHours(2), DailyClock.TimeUntilDailyReset);
            Assert.AreEqual(new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), DailyClock.NextDailyResetUtc);

            SetUtc(2026, 11, 3, 5, 0);
            Assert.AreEqual(TimeSpan.FromHours(24), DailyClock.TimeUntilDailyReset);
            Assert.AreEqual(new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc), DailyClock.NextWeeklyResetUtc);
            Assert.AreEqual(TimeSpan.FromDays(6), DailyClock.TimeUntilWeeklyReset);
        }

        [Test]
        public void DaysBetween_ParsesKeys()
        {
            Assert.AreEqual(0, DailyClock.DaysBetween("2026-11-03", "2026-11-03"));
            Assert.AreEqual(7, DailyClock.DaysBetween("2026-11-03", "2026-11-10"));
            Assert.AreEqual(-2, DailyClock.DaysBetween("2026-11-03", "2026-11-01"));
            Assert.AreEqual(1, DailyClock.DaysBetween("2026-12-31", "2027-01-01"));
            Assert.AreEqual(0, DailyClock.DaysBetween("bad", "2026-11-01"), "형식이 틀리면 0");
            Assert.AreEqual(0, DailyClock.DaysBetween(null, null));
        }

        [Test]
        public void CustomTimeZone_UsesLocalFiveAm()
        {
            var kst = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
            var clock = new DailyClock(Data, Clock, kst);

            SetUtc(2026, 11, 2, 19, 59); // 서울 11-03 04:59
            Assert.AreEqual("2026-11-02", clock.DayKey);
            SetUtc(2026, 11, 2, 20, 0); // 서울 11-03 05:00
            Assert.AreEqual("2026-11-03", clock.DayKey);
            Assert.AreEqual(new DateTime(2026, 11, 3, 20, 0, 0, DateTimeKind.Utc), clock.NextDailyResetUtc);
            Assert.AreEqual(new DateTime(2026, 11, 3, 5, 0, 0), clock.ToLocal(TimeUtil.ToUnix(Clock.UtcNow)));
        }

        [Test]
        public void DayKeyOfUnix_UsesCachedRangeAndOtherDays()
        {
            SetUtc(2026, 11, 3, 6, 0);
            long now = TimeUtil.ToUnix(Clock.UtcNow);
            Assert.AreEqual("2026-11-03", DailyClock.DayKeyOfUnix(now));
            Assert.AreEqual("2026-11-02", DailyClock.DayKeyOfUnix(now - 2 * 3600));
            Assert.AreEqual("2026-11-04", DailyClock.DayKeyOfUnix(now + 23 * 3600));
            Assert.AreSame(DailyClock.DayKey, DailyClock.DayKey, "같은 날 안에서는 같은 문자열 (할당 없음)");
        }

        // ───────────── 시간대 변경 (날·주 키는 거꾸로 가지 않는다) ─────────────

        static TimeZoneInfo Zone(int hours) =>
            TimeZoneInfo.CreateCustomTimeZone("UTC" + hours, TimeSpan.FromHours(hours), "UTC" + hours, "UTC" + hours);

        [Test]
        public void TimeZoneMovedWest_DayKeyDoesNotGoBack_UntilNextLocalReset()
        {
            SetUtc(2026, 11, 3, 12, 0);
            var east = new DailyClock(Data, Clock, Zone(14)); // 현지 11-04 02:00 → 11-03 게임 날
            east.Refresh();
            Assert.AreEqual("2026-11-03", Data.meta.clock.lastDayKey);

            // UTC−12로 바꾸고 다시 켬: 그 시간대로는 11-02(현지 11-03 00:00)지만 유닉스 시각은 그대로라 되돌림이 아니다
            var west = new DailyClock(Data, Clock, Zone(-12));
            west.DayChanged += key => _events.Add("day:" + key);
            west.WeekChanged += key => _events.Add("week:" + key);
            Assert.AreEqual("2026-11-03", west.DayKey, "Refresh 전에 읽어도 저장된 날에 머문다");
            Assert.IsFalse(west.Refresh());
            Assert.IsFalse(west.IsTampered);
            CollectionAssert.IsEmpty(_events, "날이 거꾸로 가며 바뀌지 않는다 (일일 초기화가 다시 울리지 않게)");
            Assert.AreEqual("2026-11-03", west.DayKey);
            Assert.AreEqual("2026-11-02", west.WeekKey);
            Assert.AreSame(Data.meta.clock.lastDayKey, west.DayKey, "머무는 동안은 저장된 문자열 그대로 (할당 없음)");

            // 새 시간대에서 그다음 날(11-04) 05:00이 되어야 바뀐다: UTC−12 11-04 05:00 = UTC 11-04 17:00
            Assert.AreEqual(new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc), west.NextDailyResetUtc);
            Assert.AreEqual(TimeSpan.FromHours(29), west.TimeUntilDailyReset);

            SetUtc(2026, 11, 4, 16, 59);
            Assert.IsFalse(west.Refresh());
            Assert.AreEqual("2026-11-03", west.DayKey);

            SetUtc(2026, 11, 4, 17, 0);
            Assert.IsTrue(west.Refresh());
            Assert.AreEqual("2026-11-04", west.DayKey);
            CollectionAssert.AreEqual(new[] { "day:2026-11-04" }, _events, "주는 그대로");
        }

        [Test]
        public void TimeZoneMovedWest_WeekKeyDoesNotGoBack()
        {
            SetUtc(2026, 11, 9, 12, 0);
            var east = new DailyClock(Data, Clock, Zone(14)); // 현지 11-10 02:00 → 월요일(11-09) 게임 날
            east.Refresh();
            Assert.AreEqual("2026-11-09", east.WeekKey);

            var west = new DailyClock(Data, Clock, Zone(-12)); // 현지로는 일요일(11-08) 게임 날 = 지난주
            west.DayChanged += key => _events.Add("day:" + key);
            west.WeekChanged += key => _events.Add("week:" + key);
            west.Refresh();

            CollectionAssert.IsEmpty(_events, "지난주로 돌아가지 않는다 (주간 미션이 다시 열리지 않게)");
            Assert.AreEqual("2026-11-09", west.DayKey);
            Assert.AreEqual("2026-11-09", west.WeekKey);
            Assert.AreEqual(new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc), west.NextWeeklyResetUtc, "새 시간대의 다음 월요일 05:00");
        }

        [Test]
        public void TimeZoneMovedEast_AdvancesDayNormally()
        {
            SetUtc(2026, 11, 3, 12, 0);
            var west = new DailyClock(Data, Clock, Zone(-12));
            west.Refresh();
            Assert.AreEqual("2026-11-02", west.DayKey);

            var east = new DailyClock(Data, Clock, Zone(14));
            east.DayChanged += key => _events.Add("day:" + key);
            Assert.IsTrue(east.Refresh(), "앞으로 가는 쪽은 막지 않는다 (하루 1번 제한과 상한으로 묶는다)");
            CollectionAssert.AreEqual(new[] { "day:2026-11-03" }, _events);
        }

        [Test]
        public void SavedDayInPast_StillRaisesDayChanged()
        {
            SetUtc(2026, 11, 3, 6, 0);
            DailyClock.Refresh();
            Listen();

            Data.meta.clock.lastDayKey = "2000-01-03"; // 개발자 메뉴 "하루 넘기기"
            Assert.IsTrue(DailyClock.Refresh());

            CollectionAssert.AreEqual(new[] { "day:2026-11-03" }, _events);
            Assert.AreEqual("2026-11-03", Data.meta.clock.lastDayKey);
        }

        // ───────────── 4차 일정 도우미 (EVT-01 · NEW-01 · NEW-02): UTC · +09:00 · 서머타임 경계 ─────────────

        [Test]
        public void ScheduleHelpers_ParseAddWeekday()
        {
            Assert.IsTrue(DailyClock.TryParseDayKey("2026-11-07", out var date));
            Assert.AreEqual(new DateTime(2026, 11, 7), date);
            Assert.IsFalse(DailyClock.TryParseDayKey("2026/11/07", out _));
            Assert.AreEqual("2026-11-07", DailyClock.DayKeyOfDate(new DateTime(2026, 11, 7, 13, 0, 0)));
            Assert.AreEqual("2026-12-01", DailyClock.AddDays("2026-11-30", 1));
            Assert.AreEqual("2026-10-31", DailyClock.AddDays("2026-11-01", -1));
            Assert.IsNull(DailyClock.AddDays("bad", 1));
            Assert.AreEqual(0, DailyClock.WeekdayOfKey("2026-11-02"), "월요일 = 0");
            Assert.AreEqual(5, DailyClock.WeekdayOfKey("2026-11-07"), "토요일 = 5");
            Assert.AreEqual(6, DailyClock.WeekdayOfKey("2026-11-08"), "일요일 = 6");
            Assert.AreEqual(-1, DailyClock.WeekdayOfKey(null));
        }

        [Test]
        public void GameDateAndDayStart_Utc()
        {
            SetUtc(2026, 11, 7, 4, 59, 59);
            DailyClock.Refresh();
            Assert.AreEqual(new DateTime(2026, 11, 6), DailyClock.DayDate);
            Assert.AreEqual(new DateTime(2026, 11, 6), DailyClock.GameDateOf(NowUnix));
            Assert.AreEqual(new DateTime(2026, 11, 7), DailyClock.GameDateOf(NowUnix + 1));
            Assert.AreEqual(TimeUtil.ToUnix(new DateTime(2026, 11, 7, 5, 0, 0, DateTimeKind.Utc)), DailyClock.DayStartUnix(new DateTime(2026, 11, 7)));
        }

        [Test]
        public void GameDateAndDayStart_PlusNine()
        {
            var kst = new DailyClock(Data, Clock, Zone(9));
            SetUtc(2026, 11, 6, 19, 59, 59); // KST 11-07 04:59:59
            Assert.AreEqual(new DateTime(2026, 11, 6), kst.GameDateOf(NowUnix), "KST 04:59는 전날");
            Assert.AreEqual(new DateTime(2026, 11, 7), kst.GameDateOf(NowUnix + 1), "KST 05:00부터 그날");
            Assert.AreEqual(new DateTime(2026, 11, 6), kst.DayDate);
            Assert.AreEqual("2026-11-06", kst.DayKey);
            Assert.AreEqual(TimeUtil.ToUnix(new DateTime(2026, 11, 6, 20, 0, 0, DateTimeKind.Utc)), kst.DayStartUnix(new DateTime(2026, 11, 7)),
                "KST 11-07 05:00 = UTC 11-06 20:00");
            Assert.AreEqual(new DateTime(2026, 11, 7, 4, 59, 59), kst.ToLocal(NowUnix));
            Assert.AreEqual(NowUnix, kst.LocalToUnix(new DateTime(2026, 11, 7, 4, 59, 59)));
        }

        /// <summary>서머타임 시간대: 3월 29일 현지 05:00에 한 시간 앞으로 (05:00~05:59가 없음), 10월 25일 05:00(서머타임)에 04:00으로</summary>
        static TimeZoneInfo DstZone()
        {
            var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 5, 0, 0), 3, 29);
            var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 5, 0, 0), 10, 25);
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31),
                TimeSpan.FromHours(1), start, end);
            return TimeZoneInfo.CreateCustomTimeZone("DST5", TimeSpan.Zero, "DST5", "STD5", "DST5", new[] { rule });
        }

        [Test]
        public void DstJumpAtFiveAm_DayStartsAtFirstValidTime()
        {
            var clock = new DailyClock(Data, Clock, DstZone());
            // 3월 29일 현지 05:00은 없다 → 그날의 시작은 현지 06:00(서머타임) = UTC 05:00
            long dayStart = clock.DayStartUnix(new DateTime(2026, 3, 29));
            Assert.AreEqual(TimeUtil.ToUnix(new DateTime(2026, 3, 29, 5, 0, 0, DateTimeKind.Utc)), dayStart);
            Assert.AreEqual(new DateTime(2026, 3, 28), clock.GameDateOf(dayStart - 1), "바로 전 초는 전날");
            Assert.AreEqual(new DateTime(2026, 3, 29), clock.GameDateOf(dayStart), "첫 유효 시각부터 그날");
            Assert.AreEqual(TimeUtil.ToUnix(new DateTime(2026, 3, 28, 5, 0, 0, DateTimeKind.Utc)), clock.DayStartUnix(new DateTime(2026, 3, 28)));
            // 여름 동안 현지 05:00 = UTC 04:00
            Assert.AreEqual(TimeUtil.ToUnix(new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc)), clock.DayStartUnix(new DateTime(2026, 7, 1)));

            SetUtc(2026, 3, 29, 4, 59, 59);
            Assert.AreEqual("2026-03-28", clock.DayKey);
            AdvanceSeconds(1);
            Assert.AreEqual("2026-03-29", clock.DayKey, "서머타임 경계에서도 날이 한 번만 바뀐다");
        }
    }
}
