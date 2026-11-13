using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 길 잃은 반딧불 규칙 (NEW-03, SPEC4 §13.8 — 확장 항목, perDay 0이면 꺼짐): 하루 6번 (05:00에 다시), 정해진 굴림 (같은 날 같은 순서 = 같은 결과),
    /// 다음까지 360~720초, 튜토리얼 · 잠자기 · 팝업 · 조준 · 보스 중에는 없음, 되돌림이면 보상 없음, 첫 잡기 표시.
    /// </summary>
    public class FireflyRulesTests : MetaSystemsTestBase
    {
        void Setup(int perDay = 6, bool tutorialRunning = false)
        {
            MetaDb.firefly = new FireflyConfig
            {
                minInterval = 360,
                maxInterval = 720,
                lifeSeconds = 8,
                perDay = perDay,
                rewards = new List<FireflyRewardDef>
                {
                    new FireflyRewardDef { reward = MetaReward.GoldMinutes(10), weight = 55 },
                    new FireflyRewardDef { fromLostChestTable = true, weight = 35 },
                    new FireflyRewardDef { reward = MetaReward.Gem(10), weight = 10 },
                },
            };
            MetaDb.lostChest = new LostChestConfig
            {
                tables = new List<LostChestTable>
                {
                    new LostChestTable
                    {
                        fromStage = 1, toStage = 10,
                        entries = new List<LostChestEntry>
                        {
                            new LostChestEntry { itemId = "m_cloth", weight = 40 },
                            new LostChestEntry { itemId = "m_thread", weight = 10 },
                        },
                    },
                },
            };
            MetaDb.ftueSteps = tutorialRunning ? new List<FtueStepDef> { new FtueStepDef { id = "ftue_test" } } : new List<FtueStepDef>();
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            DailyClock.Refresh();
        }

        [Test]
        public void CapSixPerDay_ResetsAtFiveAm()
        {
            Setup();
            Assert.IsTrue(Fireflies.IsEnabled);
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(Fireflies.CanSpawn(false, false, false), "잡기 " + i);
                Assert.IsNotNull(Fireflies.TryCatch());
            }
            Assert.AreEqual(6, Fireflies.CaughtToday);
            Assert.IsFalse(Fireflies.CanSpawn(false, false, false), "하루 6번");
            Assert.IsNull(Fireflies.TryCatch());

            SetUtc(2026, 11, 5, 4, 59);
            DailyClock.Refresh();
            Assert.IsFalse(Fireflies.CanSpawn(false, false, false), "04:59 = 같은 날");
            SetUtc(2026, 11, 5, 5, 0);
            DailyClock.Refresh();
            Assert.AreEqual(0, Fireflies.CaughtToday);
            Assert.IsTrue(Fireflies.CanSpawn(false, false, false));
        }

        [Test]
        public void Rolls_AreDeterministic_ByDayKeyAndCount()
        {
            Setup();
            string day = DailyClock.DayKey;
            var first = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                var reward = Fireflies.RollReward(day, i);
                Assert.IsNotNull(reward);
                first.Add(reward.type + ":" + reward.currency + ":" + reward.itemId + ":" + reward.amount);
            }
            for (int i = 0; i < 6; i++)
            {
                var reward = Fireflies.RollReward(day, i);
                Assert.AreEqual(first[i], reward.type + ":" + reward.currency + ":" + reward.itemId + ":" + reward.amount, "같은 날 · 같은 순서");
            }
            float delay = Fireflies.NextDelay();
            Assert.That(delay, Is.InRange(360f, 720f));
            Assert.AreEqual(delay, Fireflies.NextDelay(), "같은 날 · 같은 횟수 = 같은 간격");
            Fireflies.TryCatch();
            float next = Fireflies.NextDelay();
            Assert.That(next, Is.InRange(360f, 720f));
        }

        [Test]
        public void RewardMix_FollowsWeights_AndLostChestRow()
        {
            Setup();
            int gold = 0, material = 0, gem = 0;
            for (int d = 0; d < 400; d++)
            {
                string day = DailyClock.AddDays("2026-01-01", d);
                for (int i = 0; i < 6; i++)
                {
                    var reward = Fireflies.RollReward(day, i);
                    if (reward.type == MetaRewardType.GoldMinutes) gold++;
                    else if (reward.type == MetaRewardType.Item)
                    {
                        material++;
                        Assert.That(reward.itemId, Is.EqualTo("m_cloth").Or.EqualTo("m_thread"), "최고 스테이지의 잃어버린 상자 표");
                        Assert.AreEqual(1, reward.amount);
                    }
                    else if (reward.type == MetaRewardType.Currency && reward.currency == CurrencyType.Gem) gem++;
                }
            }
            double total = gold + material + gem;
            Assert.AreEqual(2400, total);
            Assert.AreEqual(0.55, gold / total, 0.04);
            Assert.AreEqual(0.35, material / total, 0.04);
            Assert.AreEqual(0.10, gem / total, 0.03);
        }

        [Test]
        public void Blocked_InTutorial_Asleep_PopupOpen_Aiming()
        {
            Setup(tutorialRunning: true);
            Assert.IsFalse(Tutorial.IsDone);
            Assert.IsFalse(Fireflies.CanSpawn(false, false, false), "튜토리얼 중");

            Setup();
            Assert.IsTrue(Fireflies.CanSpawn(false, false, false));
            Assert.IsFalse(Fireflies.CanSpawn(true, false, false), "잠자기");
            Assert.IsFalse(Fireflies.CanSpawn(false, true, false), "팝업");
            Assert.IsFalse(Fireflies.CanSpawn(false, false, true), "소환 조준");
        }

        [Test]
        public void Blocked_WhileBossAlive()
        {
            Setup();
            // 보스만 있는 판: 일반 적이 없으면 첫 웨이브에 바로 보스 (BattleSimulator.UpdateSpawner). 배치가 비면 적이 나오지 않으므로 지킴이 하나를 둔다
            var boss = SimKit.Enemy("boss_test", hp: 1e6f);
            boss.isBoss = true;
            boss.spawnWeight = 0f;
            SimKit.OnlyEnemies(BattleCfg, 1, boss);
            Characters.Grant("sock");
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base"));
            Battle.Restart();
            Assert.IsFalse(Battle.IsBetweenStages);
            Assert.IsNull(Battle.Sim.Boss, "아직 보스 없음");
            Assert.IsTrue(Fireflies.CanSpawn(false, false, false), "보스 전에는 뜰 수 있다");

            for (int i = 0; i < 50 && Battle.Sim.Boss == null; i++) Battle.Tick(0.1f);
            Assert.IsNotNull(Battle.Sim.Boss, "보스가 나왔다");
            Assert.IsFalse(Battle.IsBetweenStages);
            Assert.IsFalse(Fireflies.CanSpawn(false, false, false), "보스 중");
        }

        [Test]
        public void Tampered_NoReward()
        {
            Setup();
            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsNull(Fireflies.TryCatch(), "날아다녀도 보상 없음");
            Assert.AreEqual(0, Fireflies.CaughtToday);
        }

        [Test]
        public void FirstCatch_IsFlaggedOnce_AndRewardGranted()
        {
            Setup();
            double gemBefore = Gem;
            var granted = Fireflies.TryCatch();
            Assert.IsTrue(Fireflies.LastCatchWasFirst);
            Assert.IsTrue(Data.flags.Contains(FireflyRules.FirstCatchFlag));
            Assert.IsNotNull(granted, "단추 N분치는 초당 단추가 0이면 0개라 목록이 빌 수 있다");
            Fireflies.TryCatch();
            Assert.IsFalse(Fireflies.LastCatchWasFirst);
            Assert.GreaterOrEqual(Gem, gemBefore);
        }

        [Test]
        public void PerDayZero_IsOff()
        {
            Setup(perDay: 0);
            Assert.IsFalse(Fireflies.IsEnabled);
            Assert.IsFalse(Fireflies.CanSpawn(false, false, false));
            Assert.IsNull(Fireflies.TryCatch());
        }
    }
}
