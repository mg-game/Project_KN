using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 던전 한 번에 소탕 (IDLE-05, SPEC4 §5.2) · 던전 잠금 (F-03, §5.1) · 수요 던전 데이 입장권 (EVT-01):
    /// n장 쓰고 보상 합침 · Changed 한 번, 잠기면 null, 남은 입장권에서 자름, 더하는 입장권.
    /// </summary>
    public class DungeonBatchTests : SystemsTestBase
    {
        static double Sum(List<RewardData> rewards, RewardType type, string itemId = null, CurrencyType currency = CurrencyType.Gold)
        {
            double total = 0;
            foreach (var reward in rewards)
            {
                if (reward.type != type) continue;
                if (type == RewardType.Item && reward.itemId != itemId) continue;
                if (type == RewardType.Currency && reward.currency != currency) continue;
                total += reward.amount;
            }
            return total;
        }

        [Test]
        public void SweepMany_UsesNTickets_MergesRewards()
        {
            Data.dungeon.highestFloor = 5;
            var single = Dungeon.GetRewards(5);
            double gold = Sum(single, RewardType.Currency);
            double thread = Sum(single, RewardType.Item, DungeonSystem.ThreadItemId);
            double spring = Sum(single, RewardType.Item, DungeonSystem.SpringItemId);
            Assert.Greater(gold, 0);
            Assert.Greater(thread, 0);
            Assert.AreEqual(0f, Dungeon.GetMoonlightChance(5), "5층은 달빛 조각 확률 없음 (결정적)");

            int changed = 0;
            Dungeon.Changed += () => changed++;
            var result = Dungeon.SweepMany(5, 3);

            Assert.IsNotNull(result);
            Assert.AreEqual(5, result.Floor);
            Assert.IsTrue(result.Cleared);
            Assert.AreEqual(1, changed, "Changed는 한 번");
            Assert.AreEqual(0, Dungeon.TicketsLeft, "입장권 3장");
            Assert.AreEqual(3 * gold, Sum(result.Rewards, RewardType.Currency), 1e-6);
            Assert.AreEqual(3 * thread, Sum(result.Rewards, RewardType.Item, DungeonSystem.ThreadItemId), 1e-6);
            Assert.AreEqual(3 * spring, Sum(result.Rewards, RewardType.Item, DungeonSystem.SpringItemId), 1e-6);
            Assert.AreEqual(2, result.Rewards.FindAll(r => r.type == RewardType.Currency).Count, "같은 보상은 한 줄로 합친다 (단추 · 실타래 한 줄씩, 안 D)");
            Assert.AreEqual(3 * gold, Currency.Get(CurrencyType.Gold), 1e-6);
            double lineageXp = Sum(single, RewardType.Currency, currency: CurrencyType.LineageXp);
            Assert.Greater(lineageXp, 0, "던전도 실타래를 준다");
            Assert.AreEqual(3 * lineageXp, Currency.Get(CurrencyType.LineageXp), 1e-6);
            Assert.AreEqual(3, Data.dungeon.ticketsUsed, "진행 기록 추론용 사용 수");
        }

        [Test]
        public void SweepMany_Locked_ReturnsNull()
        {
            Data.dungeon.highestFloor = 5;
            Dungeon.FeatureResolver = f => f != FeatureId.DungeonBatchSweep;
            Assert.IsNull(Dungeon.SweepMany(5, 2), "던전 5층 돌파 전(한 번에 소탕 잠김)");
            Assert.AreEqual(3, Dungeon.TicketsLeft);
            Assert.IsNotNull(Dungeon.Sweep(5), "한 장 소탕은 지금처럼");

            Dungeon.FeatureResolver = f => f != FeatureId.Dungeon;
            Assert.IsNull(Dungeon.SweepMany(5, 1), "던전 잠김");
            Assert.IsNull(Dungeon.Sweep(5), "던전 잠김 (F-03 시스템 거절)");
            Assert.IsNull(Dungeon.Challenge());
            Assert.AreEqual(2, Dungeon.TicketsLeft);

            Dungeon.FeatureResolver = null;
            Assert.IsNotNull(Dungeon.SweepMany(5, 1), "null = 열림");
        }

        [Test]
        public void SweepMany_ClampsToTicketsLeft()
        {
            Data.dungeon.highestFloor = 4;
            Assert.IsNotNull(Dungeon.Sweep(1));
            var single = Dungeon.GetRewards(4);
            var result = Dungeon.SweepMany(4, 10);
            Assert.IsNotNull(result);
            Assert.AreEqual(2 * Sum(single, RewardType.Currency), Sum(result.Rewards, RewardType.Currency), 1e-6, "남은 2장만");
            Assert.AreEqual(0, Dungeon.TicketsLeft);
            Assert.IsNull(Dungeon.SweepMany(4, 1), "입장권 없음");

            Clock.Advance(TimeSpan.FromDays(1));
            Assert.IsNull(Dungeon.SweepMany(4, 0), "0장");
            Assert.IsNull(Dungeon.SweepMany(5, 1), "돌파하지 않은 층");
            Assert.IsNull(Dungeon.SweepMany(0, 1));
            Assert.AreEqual(3, Dungeon.TicketsLeft);
        }

        [Test]
        public void BonusTickets_AddToTodaysTickets()
        {
            Data.dungeon.highestFloor = 2;
            int bonus = 2;
            Dungeon.BonusTickets = () => bonus;
            Assert.AreEqual(5, Dungeon.TicketsLeft, "수요 던전 데이 +2");
            Assert.IsNotNull(Dungeon.SweepMany(2, 5));
            Assert.AreEqual(0, Dungeon.TicketsLeft);

            bonus = 0;
            Assert.AreEqual(0, Dungeon.TicketsLeft, "쓴 수는 그대로");
            Clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(3, Dungeon.TicketsLeft, "다음 날 기본");
        }
    }
}
