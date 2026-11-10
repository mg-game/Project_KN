using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 수선 일지 (Q-02): 카운터 오늘/이번 주 값으로 진행, 받기 → 점수 + MissionClaim, 잠긴 기능 미션 숨김·총점 제외(A4),
    /// 점수 상자, 모두 받기, 100점 JournalComplete 1회, 날·주 초기화, 되돌림·미개방 받기 막음, 목록 객체 재사용.
    /// </summary>
    public class MissionTests : MetaSystemsTestBase
    {
        static MissionDef Daily(string id, CounterKey counter, long target, int points, FeatureId required = FeatureId.Battle) => new MissionDef
        {
            id = id, period = MissionPeriod.Daily, text = id, counter = counter, target = target, points = points, requiredFeature = required,
        };

        static MissionDef Weekly(string id, CounterKey counter, long target, int points) => new MissionDef
        {
            id = id, period = MissionPeriod.Weekly, text = id, counter = counter, target = target, points = points,
        };

        static MissionChestDef Chest(MissionPeriod period, int points, params MetaReward[] rewards) =>
            new MissionChestDef { period = period, points = points, rewards = new List<MetaReward>(rewards) };

        /// <summary>리서치 Q-02 일일 11개(170점) + 주간 2개 + 상자. 기능 개방은 F-03 스테이지대로</summary>
        void SetupJournal()
        {
            AddFeature(FeatureId.IdleChest, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            AddFeature(FeatureId.QuickIdle, MetaCondition.StageCleared(1, 5));
            AddFeature(FeatureId.Missions, MetaCondition.StageCleared(1, 7));
            AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            AddFeature(FeatureId.Dungeon, MetaCondition.StageCleared(1, 10));
            MetaDb.missions = new List<MissionDef>
            {
                Daily("md_login", CounterKey.Login, 1, 10),
                Daily("md_chest", CounterKey.ChestClaim, 2, 10, FeatureId.IdleChest),
                Daily("md_echo", CounterKey.EchoSummon, 30, 20),
                Daily("md_lamp", CounterKey.LampLit, 5, 20),
                Daily("md_kill", CounterKey.EnemyKill, 300, 20),
                Daily("md_exp", CounterKey.ExpeditionStart, 2, 20, FeatureId.Expedition),
                Daily("md_dungeon", CounterKey.DungeonRun, 1, 20, FeatureId.Dungeon),
                Daily("md_upgrade", CounterKey.AnyUpgrade, 5, 10),
                Daily("md_gacha", CounterKey.GachaPull, 1, 10, FeatureId.Gacha),
                Daily("md_shop", CounterKey.ShopBuy, 1, 10, FeatureId.Shop),
                Daily("md_quick", CounterKey.QuickIdle, 1, 20, FeatureId.QuickIdle),
                Weekly("mw_journal", CounterKey.JournalComplete, 5, 30),
                Weekly("mw_boss", CounterKey.BossKill, 15, 20),
            };
            MetaDb.missionChests = new List<MissionChestDef>
            {
                Chest(MissionPeriod.Daily, 40, MetaReward.Gem(30)),
                Chest(MissionPeriod.Daily, 20, MetaReward.GoldMinutes(30)),
                Chest(MissionPeriod.Daily, 60, MetaReward.QuickIdleTicket(1)),
                Chest(MissionPeriod.Daily, 80, MetaReward.Item("m_cloth", 5), MetaReward.Item("m_thread", 1)),
                Chest(MissionPeriod.Daily, 100, MetaReward.Gem(70)),
                Chest(MissionPeriod.Weekly, 20, MetaReward.Gem(50)),
            };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0); // 수요일
            DailyClock.Refresh();
            SetCleared(7);
            Features.Evaluate();
        }

        void Do(CounterKey key, long amount = 1) => Progress.Increment(key, amount);

        [Test]
        public void LockedFeatureMissions_AreHidden_AndExcludedFromMaxPoints()
        {
            SetupJournal();

            var list = new List<MissionStatus>();
            Missions.GetMissions(MissionPeriod.Daily, list);

            Assert.AreEqual(9, list.Count, "던전·상점 미션 숨김");
            Assert.IsFalse(list.Exists(m => m.Def.id == "md_dungeon"));
            Assert.IsFalse(list.Exists(m => m.Def.id == "md_shop"));
            Assert.AreEqual(140, Missions.GetMaxPoints(MissionPeriod.Daily), "170 → 140 (A4)");
            Assert.AreEqual(100, Missions.GetBarMax(MissionPeriod.Daily));

            SetCleared(10);
            Features.Evaluate();
            Missions.GetMissions(MissionPeriod.Daily, list);
            Assert.AreEqual(11, list.Count);
            Assert.AreEqual(170, Missions.GetMaxPoints(MissionPeriod.Daily));
        }

        [Test]
        public void Claim_GivesPoints_AndCountsMissionClaim()
        {
            SetupJournal();
            Assert.IsFalse(Missions.TryClaim("md_login", out _), "아직 안 함");

            Do(CounterKey.Login);
            var status = Missions.GetStatus("md_login");
            Assert.IsTrue(status.CanClaim);
            Assert.AreEqual(1f, status.Ratio);

            Assert.IsTrue(Missions.TryClaim("md_login", out var granted));
            Assert.IsEmpty(granted, "미션 자체 보상은 없음 (점수만)");
            Assert.AreEqual(10, Missions.GetPoints(MissionPeriod.Daily));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.MissionClaim));
            Assert.IsTrue(Missions.GetStatus("md_login").IsClaimed);
            Assert.IsFalse(Missions.TryClaim("md_login", out _), "두 번은 못 받음");
            Assert.IsFalse(Missions.TryClaim("md_dungeon", out _), "숨긴 미션은 못 받음");
            Assert.IsFalse(Missions.TryClaim("nope", out _));
        }

        [Test]
        public void Progress_IsClampedToTarget()
        {
            SetupJournal();
            Do(CounterKey.EchoSummon, 12);

            var status = Missions.GetStatus("md_echo");
            Assert.AreEqual(12, status.Progress);
            Assert.AreEqual(0.4f, status.Ratio, 1e-4f);
            Assert.IsFalse(status.IsComplete);

            Do(CounterKey.EchoSummon, 100);
            status = Missions.GetStatus("md_echo");
            Assert.AreEqual(30, status.Progress, "목표에서 자름");
            Assert.IsTrue(status.IsComplete);
        }

        [Test]
        public void LockedJournal_BlocksClaims_ButKeepsProgress()
        {
            SetupJournal();
            SetCleared(5);
            Data.meta.features.unlocked.Remove(FeatureId.Missions);
            Do(CounterKey.Login);

            Assert.IsFalse(Missions.IsUnlocked);
            Assert.IsFalse(Missions.TryClaim("md_login", out _));
            Assert.AreEqual(0, Missions.TotalClaimableCount, "잠겨 있으면 배지도 없다");
            Assert.AreEqual("스테이지 1-7을 클리어하면 열려요", Missions.BlockedReason);
            Assert.IsTrue(Missions.GetStatus("md_login").IsComplete, "진행도는 쌓인다");

            SetCleared(7);
            Features.Evaluate();
            Assert.IsTrue(Missions.TryClaim("md_login", out _));
        }

        [Test]
        public void Chests_UnlockByPoints_InAscendingOrder()
        {
            SetupJournal();
            var chests = new List<MissionChestStatus>();
            Missions.GetChests(MissionPeriod.Daily, chests);
            CollectionAssert.AreEqual(new[] { 20, 40, 60, 80, 100 }, chests.ConvertAll(c => c.Def.points), "점수 오름차순");

            Do(CounterKey.Login);
            Do(CounterKey.AnyUpgrade, 5);
            Missions.TryClaim("md_login", out _);
            Assert.IsFalse(Missions.TryClaimChest(MissionPeriod.Daily, 0, out _), "10점은 아직");
            Missions.TryClaim("md_upgrade", out _);

            double expectedGold = Math.Floor(Growth.GoldPerSecond * 60 * 30);
            Assert.IsTrue(Missions.TryClaimChest(MissionPeriod.Daily, 0, out var granted));
            Assert.AreEqual(expectedGold, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gold), "단추 30분치");
            Assert.IsFalse(Missions.TryClaimChest(MissionPeriod.Daily, 0, out _), "상자는 한 번");
            Assert.IsFalse(Missions.TryClaimChest(MissionPeriod.Daily, 1, out _), "40점 상자는 아직");
            Assert.IsFalse(Missions.TryClaimChest(MissionPeriod.Daily, 99, out _));

            Missions.GetChests(MissionPeriod.Daily, chests);
            Assert.IsTrue(chests[0].IsClaimed);
            Assert.IsFalse(chests[1].IsReached);
        }

        [Test]
        public void ClaimAll_ClaimsMissionsThenChests_AndMergesRewards()
        {
            SetupJournal();
            Do(CounterKey.Login);
            Do(CounterKey.EchoSummon, 30);
            Do(CounterKey.LampLit, 5);
            Do(CounterKey.EnemyKill, 300);
            Do(CounterKey.AnyUpgrade, 5);
            Do(CounterKey.QuickIdle);
            Assert.AreEqual(6, Missions.GetClaimableCount(MissionPeriod.Daily), "미션 6개 (상자는 점수를 받은 뒤)");
            Assert.AreEqual(6, Missions.TotalClaimableCount);

            var rewards = Missions.ClaimAll(MissionPeriod.Daily);

            Assert.AreEqual(100, Missions.GetPoints(MissionPeriod.Daily));
            Assert.AreEqual(100, AmountOf(rewards, MetaRewardType.Currency, CurrencyType.Gem), "40점 30 + 100점 70 합침");
            Assert.AreEqual(1, AmountOf(rewards, MetaRewardType.QuickIdleTicket));
            Assert.AreEqual(5, AmountOf(rewards, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.AreEqual(1, MetaRewards.QuickIdleTickets);
            Assert.AreEqual(0, Missions.GetClaimableCount(MissionPeriod.Daily));
            Assert.IsEmpty(Missions.ClaimAll(MissionPeriod.Daily), "다시 누르면 아무것도 없음");
        }

        [Test]
        public void ReachingHundred_CountsJournalCompleteOnce()
        {
            SetupJournal();
            Do(CounterKey.Login);
            Do(CounterKey.EchoSummon, 30);
            Do(CounterKey.LampLit, 5);
            Do(CounterKey.EnemyKill, 300);
            Do(CounterKey.QuickIdle);
            Do(CounterKey.GachaPull);
            Do(CounterKey.AnyUpgrade, 5);

            foreach (var id in new[] { "md_login", "md_echo", "md_lamp", "md_kill" }) Missions.TryClaim(id, out _);
            Assert.AreEqual(70, Missions.GetPoints(MissionPeriod.Daily));
            Assert.AreEqual(0, Progress.GetWeekly(CounterKey.JournalComplete));

            Missions.TryClaim("md_quick", out _);
            Missions.TryClaim("md_gacha", out _);
            Assert.AreEqual(100, Missions.GetPoints(MissionPeriod.Daily));
            Assert.AreEqual(1, Progress.GetWeekly(CounterKey.JournalComplete), "처음 100점");

            Missions.TryClaim("md_upgrade", out _);
            Assert.AreEqual(1, Progress.GetWeekly(CounterKey.JournalComplete), "그날은 한 번만");
            Assert.AreEqual(1, Missions.GetStatus("mw_journal").Progress, "주간 미션 진행");
        }

        [Test]
        public void NewDay_ResetsDaily_WeeklyPersistsUntilMonday()
        {
            SetupJournal();
            Do(CounterKey.Login);
            Do(CounterKey.BossKill, 15);
            Missions.TryClaim("md_login", out _);
            Missions.TryClaim("mw_boss", out _);
            int changed = 0;
            Missions.Changed += () => changed++;

            SetUtc(2026, 11, 5, 5, 0); // 목요일 05:00
            DailyClock.Refresh();

            Assert.Greater(changed, 0, "날이 바뀌면 알린다");
            Assert.AreEqual(0, Missions.GetPoints(MissionPeriod.Daily));
            Assert.IsFalse(Missions.GetStatus("md_login").IsClaimed);
            Assert.IsFalse(Missions.GetStatus("md_login").IsComplete, "오늘 카운터도 0");
            Assert.AreEqual(20, Missions.GetPoints(MissionPeriod.Weekly));
            Assert.IsTrue(Missions.GetStatus("mw_boss").IsClaimed);

            SetUtc(2026, 11, 9, 5, 0); // 월요일 05:00
            Assert.AreEqual(0, Missions.GetPoints(MissionPeriod.Weekly), "읽을 때도 주가 바뀌면 비운다");
            Assert.IsFalse(Missions.GetStatus("mw_boss").IsClaimed);
        }

        [Test]
        public void Tampered_BlocksClaims()
        {
            SetupJournal();
            Do(CounterKey.Login);
            SetUtc(2026, 11, 4, 7, 0); // 2시간 되돌림
            DailyClock.Refresh();

            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(Missions.TryClaim("md_login", out _));
            Assert.IsEmpty(Missions.ClaimAll(MissionPeriod.Daily));
            Assert.AreEqual(DailyClock.TamperedMessage, Missions.BlockedReason);
            Assert.AreEqual(0, Missions.TotalClaimableCount);

            SetUtc(2026, 11, 4, 9, 1);
            DailyClock.Refresh();
            Assert.IsTrue(Missions.TryClaim("md_login", out _), "시간이 맞으면 받을 수 있다");
        }

        [Test]
        public void GetMissions_ReusesStatusObjects()
        {
            SetupJournal();
            var list = new List<MissionStatus>();
            Missions.GetMissions(MissionPeriod.Daily, list);
            var first = list[0];
            int count = list.Count;

            Do(CounterKey.Login);
            Missions.GetMissions(MissionPeriod.Daily, list);

            Assert.AreSame(first, list[0], "같은 객체를 덮어쓴다");
            Assert.AreEqual(count, list.Count);
            Assert.IsTrue(list[0].IsComplete);

            list.Add(new MissionStatus());
            Missions.GetMissions(MissionPeriod.Daily, list);
            Assert.AreEqual(count, list.Count, "남는 것은 잘라 낸다");
        }

        [Test]
        public void Changed_OnlyWhenVisibleProgressMoves()
        {
            SetupJournal();
            int changed = 0;
            Missions.Changed += () => changed++;
            Progress.MarkDirty();
            Progress.Flush();
            int baseline = changed;

            Do(CounterKey.EnemyKill, 10);
            Progress.Flush();
            Assert.AreEqual(baseline + 1, changed);

            Do(CounterKey.EnemyKill, 500);
            Progress.Flush();
            Assert.AreEqual(baseline + 2, changed, "목표 도달");

            Do(CounterKey.EnemyKill, 500);
            Progress.Flush();
            Assert.AreEqual(baseline + 2, changed, "목표를 넘은 처치는 화면이 그대로라 알리지 않는다");
        }

        [Test]
        public void TimeUntilReset_DailyAndWeekly()
        {
            SetupJournal(); // 수요일 09:00
            Assert.AreEqual(TimeSpan.FromHours(20), Missions.GetTimeUntilReset(MissionPeriod.Daily));
            Assert.AreEqual(TimeSpan.FromHours(20) + TimeSpan.FromDays(4), Missions.GetTimeUntilReset(MissionPeriod.Weekly));
        }

        [Test]
        public void DailyClaim_AddsJournalPoints_WeeklyDoesNot()
        {
            SetupJournal();
            Do(CounterKey.Login);
            Assert.IsTrue(Missions.TryClaim("md_login", out _));
            Assert.AreEqual(10, Progress.GetLifetime(CounterKey.JournalPoints), "일일 미션 점수 = 수첩 경험치 (Q-06, SPEC4 §2.5)");
            Assert.AreEqual(10, Progress.GetDaily(CounterKey.JournalPoints));

            Do(CounterKey.JournalComplete, 5);
            Assert.IsTrue(Missions.TryClaim("mw_journal", out _));
            Assert.AreEqual(10, Progress.GetLifetime(CounterKey.JournalPoints), "주간 미션 점수는 더하지 않는다");
        }

        [Test]
        public void ChestClaim_WritesMissionBoxPlayLogLine()
        {
            SetupJournal();
            Do(CounterKey.Login);
            Do(CounterKey.AnyUpgrade, 5);
            Do(CounterKey.JournalComplete, 5);
            var lines = PlayLogLines("mission_box", () =>
            {
                Assert.IsTrue(Missions.TryClaim("md_login", out _));
                Assert.IsTrue(Missions.TryClaim("md_upgrade", out _));
                Assert.IsTrue(Missions.TryClaimChest(MissionPeriod.Daily, 0, out _));
                Assert.IsFalse(Missions.TryClaimChest(MissionPeriod.Daily, 0, out _), "다시 받지 못함 → 줄 없음");
                Assert.IsTrue(Missions.TryClaim("mw_journal", out _));
                Missions.ClaimAll(MissionPeriod.Weekly);
            });
            Assert.AreEqual(2, lines.Count, "상자 하나에 한 줄 (미션 받기는 줄 없음)");
            Assert.AreEqual("daily", lines[0].Value<string>("period"));
            Assert.AreEqual(20, lines[0].Value<int>("pts"));
            Assert.AreEqual("weekly", lines[1].Value<string>("period"));
            Assert.AreEqual(20, lines[1].Value<int>("pts"));
        }

        /// <summary>플레이 기록을 임시 폴더에 열고 body를 돌린 뒤 ev가 같은 줄들을 읽는다 (기록은 늘 닫고 폴더를 지운다)</summary>
        List<JObject> PlayLogLines(string ev, Action body)
        {
            string dir = Path.Combine(Path.GetTempPath(), "idle_p42_log_" + Guid.NewGuid().ToString("N"));
            var lines = new List<JObject>();
            PlayLog.Close();
            try
            {
                Assert.IsTrue(PlayLog.Open(dir, Clock));
                body();
                PlayLog.Flush();
                string path = Path.Combine(dir, PlayLog.FileName);
                if (File.Exists(path))
                {
                    foreach (var text in File.ReadAllLines(path))
                    {
                        if (text.Length == 0) continue;
                        var line = JObject.Parse(text);
                        if (line.Value<string>("ev") == ev) lines.Add(line);
                    }
                }
            }
            finally
            {
                PlayLog.Close();
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            return lines;
        }
    }
}
