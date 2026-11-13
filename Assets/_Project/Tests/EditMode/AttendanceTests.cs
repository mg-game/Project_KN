using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 출석부 (Q-03): 하루 1도장(05:00), 새싹 7 + 정규 28 동시, 누적 일수, 7일째 친구 확정(중복이면 기억편),
    /// 28일 회차(다 채운 다음 날은 빈 판), 미개방·되돌림 막음, 시간대를 바꿔도 같은 날 다시 찍을 수 없음
    /// </summary>
    public class AttendanceTests : MetaSystemsTestBase
    {
        static AttendanceDayDef Day(AttendanceTrack track, int day, params MetaReward[] rewards) =>
            new AttendanceDayDef { track = track, day = day, rewards = new List<MetaReward>(rewards) };

        void SetupAttendance(bool unlocked = true)
        {
            AddFeature(FeatureId.Attendance, MetaCondition.Of(MetaConditionType.FtueDone, 1), MetaCondition.StageCleared(1, 5));
            var days = new List<AttendanceDayDef>
            {
                Day(AttendanceTrack.Starter, 1, MetaReward.Gem(300)),
                Day(AttendanceTrack.Starter, 2, MetaReward.QuickIdleTicket(3)),
                Day(AttendanceTrack.Starter, 3, MetaReward.Item("m_cloth", 15)),
                Day(AttendanceTrack.Starter, 4, MetaReward.Gem(300)),
                Day(AttendanceTrack.Starter, 5, MetaReward.Item("m_thread", 5)),
                Day(AttendanceTrack.Starter, 6, MetaReward.Gem(400)),
                Day(AttendanceTrack.Starter, 7, MetaReward.Lineage("candle")),
            };
            for (int d = 1; d <= AttendanceSystem.MonthlyLength; d++)
            {
                var reward = d switch
                {
                    7 => MetaReward.Gem(200),
                    14 => MetaReward.SelectShards(30),
                    21 => MetaReward.Item("m_thread", 2),
                    28 => MetaReward.Gem(500),
                    _ => MetaReward.Gem(50),
                };
                days.Add(Day(AttendanceTrack.Monthly, d, reward));
            }
            MetaDb.attendance = days;
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            DailyClock.Refresh();
            if (unlocked)
            {
                Data.meta.ftue.done = true;
                Features.Evaluate();
            }
        }

        /// <summary>다음 날 같은 시각으로 가서 시계를 확인한다</summary>
        void NextDay(int days = 1)
        {
            Advance(24 * days);
            DailyClock.Refresh();
        }

        [Test]
        public void Locked_CannotCheckIn()
        {
            SetupAttendance(unlocked: false);

            Assert.IsFalse(Attendance.IsUnlocked);
            Assert.IsFalse(Attendance.CanCheckIn);
            Assert.IsNull(Attendance.CheckIn());

            SetCleared(5);
            Features.Evaluate();
            Assert.IsTrue(Attendance.CanCheckIn, "1-5 클리어로도 열린다");
        }

        [Test]
        public void CheckIn_StampsBothTracks_GrantsRewards_OncePerDay()
        {
            SetupAttendance();
            AttendanceCheckInResult stamped = null;
            Attendance.CheckedIn += r => stamped = r;

            var result = Attendance.CheckIn();

            Assert.IsNotNull(result);
            Assert.AreSame(result, stamped);
            Assert.AreEqual(1, result.StarterDay);
            Assert.AreEqual(1, result.MonthlyDay);
            Assert.AreEqual(350, AmountOf(result.Rewards, MetaRewardType.Currency, CurrencyType.Gem), "새싹 300 + 정규 50 합침");
            Assert.AreEqual(1, result.Rewards.Count);
            Assert.AreEqual(350, Gem);
            Assert.AreEqual(1, Progress.GetWeekly(CounterKey.AttendanceCheckIn));
            Assert.IsTrue(Attendance.HasCheckedInToday);
            Assert.IsNull(Attendance.CheckIn(), "하루 한 번");

            SetUtc(2026, 11, 5, 4, 59);
            DailyClock.Refresh();
            Assert.IsFalse(Attendance.CanCheckIn, "05:00 전은 같은 날");
            SetUtc(2026, 11, 5, 5, 0);
            DailyClock.Refresh();
            Assert.IsTrue(Attendance.CanCheckIn);
            Assert.AreEqual(3, Attendance.CheckIn().Rewards.Find(r => r.type == MetaRewardType.QuickIdleTicket).amount);
            Assert.AreEqual(3, MetaRewards.QuickIdleTickets);
        }

        [Test]
        public void MissedDays_DoNotReset()
        {
            SetupAttendance();
            Attendance.CheckIn();
            NextDay(5);

            var result = Attendance.CheckIn();

            Assert.AreEqual(2, result.StarterDay, "누적 일수");
            Assert.AreEqual(2, result.MonthlyDay);
            Assert.AreEqual(2, Attendance.TotalDays);
        }

        [Test]
        public void DaySeven_GrantsFriend_OrShardsWhenOwned()
        {
            SetupAttendance();
            for (int i = 0; i < 6; i++)
            {
                Attendance.CheckIn();
                NextDay();
            }
            Assert.IsFalse(Characters.IsOwned("candle"));

            var result = Attendance.CheckIn();
            Assert.AreEqual(7, result.StarterDay);
            Assert.IsTrue(Characters.IsOwned("candle"), "7일째 친구 확정");
            Assert.IsNotNull(Find(result.Rewards, MetaRewardType.Lineage));
            Assert.IsTrue(Attendance.IsStarterComplete);
            Assert.AreEqual(0, Attendance.GetNextDay(AttendanceTrack.Starter));
        }

        [Test]
        public void DaySeven_DuplicateFriend_BecomesShards()
        {
            SetupAttendance();
            Characters.Grant("candle");
            Data.meta.attendance.starterDays = 6;
            Data.meta.attendance.monthlyDays = 6;

            Attendance.CheckIn();

            Assert.AreEqual(Config.GetDuplicateShards(ItemGrade.Rare), Characters.GetShards("candle"), "이미 있으면 기억편");
        }

        [Test]
        public void Monthly_RollsOverAfter28_StarterStopsAfter7()
        {
            SetupAttendance();
            for (int i = 0; i < AttendanceSystem.MonthlyLength; i++)
            {
                if (i > 0) NextDay();
                Assert.IsNotNull(Attendance.CheckIn(), $"{i + 1}일째");
            }
            Assert.AreEqual(7, Attendance.StarterDays);
            Assert.AreEqual(28, Attendance.MonthlyDays, "28일째를 찍은 날은 판이 가득");
            Assert.AreEqual(0, Attendance.MonthlyCycle);
            Assert.IsTrue(Attendance.IsStamped(AttendanceTrack.Monthly, 28));
            Assert.IsTrue(Attendance.IsToday(AttendanceTrack.Monthly, 28));
            Assert.AreEqual(30, MetaRewards.SelectShards, "14일째 선택 기억편");

            NextDay();
            Assert.AreEqual(1, Attendance.GetNextDay(AttendanceTrack.Monthly), "28일을 채우면 다음은 새 회차 1일째");
            Assert.AreEqual(0, Attendance.MonthlyDays, "찍기 전에도 새 회차의 빈 판으로 보인다");
            Assert.AreEqual(1, Attendance.MonthlyCycle);
            Assert.IsTrue(Attendance.IsToday(AttendanceTrack.Monthly, 1));
            for (int d = 1; d <= AttendanceSystem.MonthlyLength; d++)
                Assert.IsFalse(Attendance.IsStamped(AttendanceTrack.Monthly, d), $"{d}일째 칸은 비어 있다 (오늘 칸이 '받았어요'로 보이지 않게)");
            Assert.AreEqual(28, Data.meta.attendance.monthlyDays, "저장값은 다음 도장에서 넘어간다");

            var result = Attendance.CheckIn();
            Assert.AreEqual(0, result.StarterDay, "새싹은 끝남");
            Assert.AreEqual(1, result.MonthlyDay);
            Assert.AreEqual(1, Attendance.MonthlyCycle);
            Assert.AreEqual(1, Attendance.MonthlyDays);
            Assert.IsTrue(Attendance.IsStamped(AttendanceTrack.Monthly, 1));
            Assert.IsFalse(Attendance.IsStamped(AttendanceTrack.Monthly, 2));
            Assert.AreEqual(29, Attendance.TotalDays);
        }

        [Test]
        public void TimeZoneMovedWest_DoesNotReopenTodaysStamp()
        {
            SetupAttendance();
            SetUtc(2026, 11, 4, 10, 0); // UTC 날 키 11-04
            DailyClock.Refresh();
            Assert.IsNotNull(Attendance.CheckIn());

            // 시간대를 UTC−12로 바꾸고 다시 켬: 그 시간대로는 아직 11-03이지만 날 키는 거꾸로 가지 않는다
            var west = TimeZoneInfo.CreateCustomTimeZone("UTC-12", TimeSpan.FromHours(-12), "UTC-12", "UTC-12");
            var westClock = new DailyClock(Data, Clock, west);
            int dayChanged = 0;
            westClock.DayChanged += _ => dayChanged++;
            westClock.Refresh();
            var westAttendance = new AttendanceSystem(Data, MetaDb, westClock, Progress, MetaRewards, Features);

            Assert.AreEqual(0, dayChanged);
            Assert.AreEqual("2026-11-04", westClock.DayKey);
            Assert.IsTrue(westAttendance.HasCheckedInToday);
            Assert.IsFalse(westAttendance.CanCheckIn, "시간대 왕복으로 도장을 또 찍을 수 없다");
            Assert.IsNull(westAttendance.CheckIn());
        }

        [Test]
        public void Tampered_BlocksCheckIn()
        {
            SetupAttendance();
            Attendance.CheckIn();
            NextDay();
            SetUtc(2026, 11, 4, 9, 0); // 어제로 되돌림
            DailyClock.Refresh();

            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(Attendance.CanCheckIn);
            Assert.IsNull(Attendance.CheckIn());
        }

        [Test]
        public void TodayAndStampedCells()
        {
            SetupAttendance();
            Assert.IsTrue(Attendance.IsToday(AttendanceTrack.Starter, 1));
            Assert.IsTrue(Attendance.IsToday(AttendanceTrack.Monthly, 1));
            Assert.IsFalse(Attendance.IsStamped(AttendanceTrack.Starter, 1));

            Attendance.CheckIn();
            Assert.IsTrue(Attendance.IsStamped(AttendanceTrack.Starter, 1));
            Assert.IsTrue(Attendance.IsToday(AttendanceTrack.Starter, 1), "찍은 뒤에는 오늘 찍힌 칸");
            Assert.IsFalse(Attendance.IsToday(AttendanceTrack.Starter, 2));

            NextDay();
            Assert.IsTrue(Attendance.IsToday(AttendanceTrack.Starter, 2));
            Assert.AreEqual(AttendanceSystem.StarterLength, Attendance.GetLength(AttendanceTrack.Starter));
            Assert.AreEqual(AttendanceSystem.MonthlyLength, Attendance.GetLength(AttendanceTrack.Monthly));
        }

        [Test]
        public void DayChange_RaisesChanged()
        {
            SetupAttendance();
            Attendance.CheckIn();
            int changed = 0;
            Attendance.Changed += () => changed++;

            NextDay();

            Assert.AreEqual(1, changed);
            Assert.IsTrue(Attendance.CanCheckIn);
        }

        [Test]
        public void NoContent_CannotCheckIn()
        {
            Data.meta.ftue.done = true;
            Assert.IsTrue(Attendance.IsUnlocked, "정의 없음 = 열림");
            Assert.IsFalse(Attendance.CanCheckIn, "칸 데이터가 없으면 찍을 수 없다");
        }
    }
}
