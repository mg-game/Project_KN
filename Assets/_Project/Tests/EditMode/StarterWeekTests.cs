using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 첫 주 (NEW-01, SPEC4 §13.6): 설치 7일째 이하일 때만 시작 (옛 세이브는 거절), 날 열림은 05:00 경계, 오늘의 일지 점수 과제는 굳어서
    /// 날이 바뀌어도 남음, 점수 · 상자 문턱, 창(14일) 끝에 남은 보상 자동 받기, 되돌림 막음, 잠긴 기능 과제는 열린 뒤 받기. 일일 시계 UTC.
    /// </summary>
    public class StarterWeekTests : MetaSystemsTestBase
    {
        static StarterTaskDef Task(string id, int day, MetaCondition condition, int points = 5, FeatureId required = FeatureId.Battle) =>
            new StarterTaskDef { id = id, day = day, text = id, condition = condition, points = points, requiredFeature = required, navFeature = required };

        static StarterChestDef Chest(int points, params MetaReward[] rewards) => new StarterChestDef { points = points, rewards = new List<MetaReward>(rewards) };

        readonly List<StarterTaskStatus> _tasks = new();
        int _started;
        List<MetaReward> _autoGranted;

        [SetUp]
        public void ResetRecords()
        {
            _tasks.Clear();
            _started = 0;
            _autoGranted = null;
        }

        void SetupWeek()
        {
            AddFeature(FeatureId.Missions, MetaCondition.StageCleared(1, 7));
            AddFeature(FeatureId.Dungeon, MetaCondition.StageCleared(1, 10));
            MetaDb.starterWeek = new StarterWeekConfig
            {
                windowDays = 14,
                maxInstallDays = 7,
                tasks = new List<StarterTaskDef>
                {
                    Task("t1_stage", 1, MetaCondition.StageCleared(1, 8)),
                    Task("t1_journal", 1, MetaCondition.Of(MetaConditionType.JournalPoints, 40)),
                    Task("t2_gacha", 2, MetaCondition.Count(CounterKey.GachaPull, 10)),
                    Task("t3_dungeon", 3, MetaCondition.Of(MetaConditionType.DungeonFloor, 3), required: FeatureId.Dungeon),
                    Task("t7_stage", 7, MetaCondition.StageCleared(1, 9), points: 15),
                },
                chests = new List<StarterChestDef>
                {
                    Chest(10, MetaReward.Gem(50)),
                    Chest(20, MetaReward.QuickIdleTicket(1)),
                    Chest(100, MetaReward.Gem(150)),
                },
            };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            DailyClock.Refresh(); // 설치한 날 = 11-04
            StarterWeek.Started += () => _started++;
            StarterWeek.AutoGranted += granted => _autoGranted = granted;
            MetaSession.Start();
        }

        void NextDay(int days = 1)
        {
            Advance(24 * days);
            DailyClock.Refresh();
        }

        void OpenJournal()
        {
            SetCleared(7);
            Features.Evaluate();
        }

        [Test]
        public void Starts_WhenJournalOpens_IfInstallDayAtMostSeven()
        {
            SetupWeek();
            Assert.IsFalse(StarterWeek.IsStarted, "수선 일지가 아직 잠김");
            NextDay(3);
            OpenJournal();
            Assert.IsTrue(StarterWeek.IsActive);
            Assert.AreEqual(1, _started);
            Assert.AreEqual(DailyClock.DayKey, Data.meta.starterWeek.startDayKey);
            Assert.AreEqual(1, StarterWeek.DayIndex);
            Assert.AreEqual(13, StarterWeek.DaysLeft, "오늘 뒤로 남은 날 키 13개");
        }

        [Test]
        public void OldSave_PastDaySeven_IsDeclined()
        {
            SetupWeek();
            NextDay(7); // 설치 8일째
            OpenJournal();
            Assert.IsFalse(StarterWeek.IsActive);
            Assert.IsTrue(Data.meta.starterWeek.declined);
            StarterWeek.AfterStartup(true);
            Assert.IsFalse(StarterWeek.IsStarted, "거절은 영영");
            Assert.AreEqual(0, _started);
        }

        [Test]
        public void V3SaveAlreadyPastDaySeven_DeclinedOnStartup()
        {
            AddFeature(FeatureId.Missions, MetaCondition.StageCleared(1, 7));
            MetaDb.starterWeek.tasks = new List<StarterTaskDef> { Task("t1_stage", 1, MetaCondition.StageCleared(1, 8)) };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            Data.meta.clock.installDayKey = "2026-09-01";
            SetCleared(30);
            Data.meta.features.unlocked.Add(FeatureId.Missions);
            MetaSession.Start();
            Assert.IsTrue(Data.meta.starterWeek.declined);
            Assert.IsFalse(StarterWeek.IsActive);
        }

        [Test]
        public void DayGating_AtFiveAm()
        {
            SetupWeek();
            OpenJournal();
            Progress.Increment(CounterKey.GachaPull, 10);
            Progress.Flush();
            Assert.IsFalse(StarterWeek.IsDayOpen(2));
            Assert.IsNull(StarterWeek.TryClaimTask("t2_gacha"), "2일째 과제는 아직 닫힘");
            Assert.IsFalse(Data.meta.starterWeek.done.Contains("t2_gacha"), "닫힌 날은 굳히지 않는다");

            SetUtc(2026, 11, 5, 4, 59);
            DailyClock.Refresh();
            Assert.AreEqual(1, StarterWeek.DayIndex, "04:59 = 아직 첫날");
            SetUtc(2026, 11, 5, 5, 0);
            DailyClock.Refresh();
            Assert.AreEqual(2, StarterWeek.DayIndex);
            Assert.IsTrue(Data.meta.starterWeek.done.Contains("t2_gacha"), "열리는 날 굳힌다 (평생 카운터)");
            Assert.IsNotNull(StarterWeek.TryClaimTask("t2_gacha"));
        }

        [Test]
        public void LatchedDailyJournalTask_SurvivesDailyReset()
        {
            SetupWeek();
            OpenJournal();
            Data.meta.missions.daily.periodKey = DailyClock.DayKey;
            Data.meta.missions.daily.points = 40;
            Progress.MarkDirty();
            Progress.Flush();
            Assert.IsTrue(Data.meta.starterWeek.done.Contains("t1_journal"));

            NextDay();
            Assert.AreEqual(0, Progress.GetValue(MetaCondition.Of(MetaConditionType.JournalPoints, 40)), "오늘의 일지 점수는 0");
            StarterWeek.GetTasks(1, _tasks);
            var journal = _tasks.Find(t => t.Def.id == "t1_journal");
            Assert.IsTrue(journal.Done, "굳은 과제는 남는다");
            Assert.AreEqual(journal.Target, journal.Value);
            Assert.IsTrue(journal.CanClaim);
            Assert.IsNotNull(StarterWeek.TryClaimTask("t1_journal"));
        }

        [Test]
        public void Points_AndChestThresholds()
        {
            SetupWeek();
            OpenJournal();
            SetCleared(8);
            Progress.MarkDirty();
            Progress.Flush();
            Assert.IsTrue(StarterWeek.HasClaimable);
            Assert.IsNull(StarterWeek.TryClaimChest(0), "점수 0");

            Assert.IsNotNull(StarterWeek.TryClaimTask("t1_stage"));
            Assert.IsNull(StarterWeek.TryClaimTask("t1_stage"), "한 번만");
            Assert.AreEqual(5, StarterWeek.Points);
            Assert.IsNull(StarterWeek.TryClaimChest(0), "10점 상자");

            Data.meta.missions.daily.periodKey = DailyClock.DayKey;
            Data.meta.missions.daily.points = 40;
            Progress.MarkDirty();
            Progress.Flush();
            var all = StarterWeek.ClaimAll();
            Assert.AreEqual(10, StarterWeek.Points);
            Assert.AreEqual(50, AmountOf(all, MetaRewardType.Currency, CurrencyType.Gem), "10점 상자까지");
            Assert.IsTrue(StarterWeek.IsChestClaimed(0));
            Assert.IsFalse(StarterWeek.IsChestClaimed(1));
            Assert.IsFalse(StarterWeek.HasClaimable);
            Assert.IsEmpty(StarterWeek.ClaimAll());
        }

        [Test]
        public void LockedFeatureTask_ClaimableLater_InWindow()
        {
            SetupWeek();
            OpenJournal();
            NextDay(2); // 3일째
            Data.dungeon.highestFloor = 3;
            Progress.MarkDirty();
            Progress.Flush();
            StarterWeek.GetTasks(3, _tasks);
            var dungeon = _tasks.Find(t => t.Def.id == "t3_dungeon");
            Assert.IsTrue(dungeon.Done, "잠겨 있어도 굳는다");
            Assert.IsTrue(dungeon.Locked);
            Assert.IsFalse(dungeon.CanClaim);
            Assert.IsNull(StarterWeek.TryClaimTask("t3_dungeon"));

            SetCleared(10);
            Features.Evaluate();
            Assert.IsNotNull(StarterWeek.TryClaimTask("t3_dungeon"), "던전이 열린 뒤 (창 안)");
        }

        [Test]
        public void WindowEnd_AutoGrantsReachedRewards()
        {
            SetupWeek();
            OpenJournal();
            SetCleared(9);
            Progress.MarkDirty();
            Progress.Flush();
            Assert.IsTrue(Data.meta.starterWeek.done.Contains("t1_stage"));
            NextDay(6); // 7일째
            Assert.IsTrue(Data.meta.starterWeek.done.Contains("t7_stage"));
            Assert.IsNotNull(StarterWeek.TryClaimTask("t1_stage"));
            double gemBefore = Gem;

            NextDay(7); // 14일째 = 창의 마지막 날 (시작 날 키 + 13)
            Assert.IsTrue(StarterWeek.IsActive);
            Assert.AreEqual(0, StarterWeek.DaysLeft);
            NextDay(); // 창 끝
            Assert.IsFalse(StarterWeek.IsActive);
            Assert.IsTrue(Data.meta.starterWeek.finished);
            Assert.IsNotNull(_autoGranted, "남은 선물 알림");
            Assert.IsTrue(Data.meta.starterWeek.claimed.Contains("t7_stage"), "굳은 과제는 자동으로 받는다");
            Assert.AreEqual(20, StarterWeek.Points);
            Assert.IsTrue(StarterWeek.IsChestClaimed(0));
            Assert.IsTrue(StarterWeek.IsChestClaimed(1));
            Assert.IsFalse(StarterWeek.IsChestClaimed(2), "닿지 않은 상자는 없다");
            Assert.AreEqual(gemBefore + 50, Gem, 1e-6);
            Assert.AreEqual(1, MetaRewards.QuickIdleTickets);
            Assert.IsNull(StarterWeek.TryClaimTask("t1_journal"), "끝난 뒤에는 받기 없음");
        }

        [Test]
        public void Tampered_BlocksClaims()
        {
            SetupWeek();
            OpenJournal();
            SetCleared(8);
            Progress.MarkDirty();
            Progress.Flush();
            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(StarterWeek.HasClaimable);
            Assert.IsNull(StarterWeek.TryClaimTask("t1_stage"));
            Assert.IsEmpty(StarterWeek.ClaimAll());
        }

        [Test]
        public void EmptyContent_NeitherStartsNorDeclines()
        {
            AddFeature(FeatureId.Missions, MetaCondition.StageCleared(1, 7));
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start();
            OpenJournal();
            Assert.IsFalse(StarterWeek.IsStarted);
            Assert.IsFalse(Data.meta.starterWeek.declined, "콘텐츠가 나중에 들어와도 시작할 수 있게");
        }
    }
}
