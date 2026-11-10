using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    /// <summary>
    /// 픽스처 계보: sock(영웅) / candle(희귀) / button(일반). 전설·신화 계보는 없다.
    /// 기본 테이블: 일반 70 / 희귀 29 / 전설 1, 1회 100 / 10회 900, 천장 없음.
    /// </summary>
    public class GachaSystemTests : SystemsTestBase
    {
        static List<GachaGradeRate> Only(ItemGrade grade) => new() { new GachaGradeRate { grade = grade, weight = 1 } };

        [SetUp]
        public void SetUpShards()
        {
            Config.duplicateShardsByGrade = new[] { 5, 10, 20, 40, 80 };
        }

        [Test]
        public void Pull_NotEnoughGem_Fails()
        {
            Assert.IsFalse(Gacha.TryPull(Table, 1, out var results));
            Assert.IsNull(results);
            Assert.AreEqual(0, Characters.GetOwnedLineages().Count);
            Assert.AreEqual(0, Gacha.GetPityCount(Table));
        }

        [Test]
        public void Pull_SpendsSingleAndTenCost()
        {
            Currency.Add(CurrencyType.Gem, 1000);

            Assert.IsTrue(Gacha.TryPull(Table, 1, out var single));
            Assert.AreEqual(1, single.Count);
            Assert.AreEqual(900, Currency.Get(CurrencyType.Gem), 1e-9);

            Assert.IsTrue(Gacha.TryPull(Table, 10, out var ten));
            Assert.AreEqual(10, ten.Count);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gem), 1e-9);
            Assert.IsTrue(ten.All(r => Characters.IsOwned(r.Lineage.id)));
        }

        [Test]
        public void Cost_TenPullUsesDiscountPrice()
        {
            Assert.AreEqual(100, Gacha.GetCost(Table, 1), 1e-9);
            Assert.AreEqual(300, Gacha.GetCost(Table, 3), 1e-9);
            Assert.AreEqual(900, Gacha.GetCost(Table, 10), 1e-9);
            Assert.AreEqual(0, Gacha.GetCost(null, 1), 1e-9);
            Assert.IsFalse(Gacha.CanAfford(Table, 1));

            Currency.Add(CurrencyType.Gem, 900);

            Assert.IsTrue(Gacha.CanAfford(Table, 10));
            Assert.IsFalse(Gacha.CanAfford(Table, 11), "11회는 할인 없이 1100");
            Assert.IsFalse(Gacha.CanAfford(null, 1));
        }

        [Test]
        public void FirstPull_IsNew_DuplicatesConvertToShards()
        {
            Table.rates = Only(ItemGrade.Common);
            Currency.Add(CurrencyType.Gem, 1e6);

            Gacha.TryPull(Table, 10, out var results);

            Assert.IsTrue(results.All(r => r.Lineage.id == "button"));
            Assert.IsTrue(results[0].IsNew);
            Assert.AreEqual(0, results[0].ShardsGained);
            Assert.IsTrue(results.Skip(1).All(r => !r.IsNew && r.ShardsGained == 5), "일반(1성) 중복 = 기억편 5");
            Assert.AreEqual(45, Characters.GetShards("button"));
            Assert.IsTrue(Characters.IsFormUnlocked("button_base"));
        }

        [Test]
        public void Duplicate_ShardsDependOnGrade()
        {
            Characters.Grant("candle");
            Table.rates = Only(ItemGrade.Rare);
            Currency.Add(CurrencyType.Gem, 100);

            Gacha.TryPull(Table, 1, out var results);

            Assert.IsFalse(results[0].IsNew);
            Assert.AreEqual(10, results[0].ShardsGained, "희귀(2성) 중복 = 기억편 10");
            Assert.AreEqual(10, Characters.GetShards("candle"));
        }

        [Test]
        public void Pity_GuaranteesGradeOnNthPull()
        {
            Table.pityCount = 10;
            Table.pityGrade = ItemGrade.Epic;
            Table.rates = Only(ItemGrade.Common);
            Currency.Add(CurrencyType.Gem, 1e6);

            for (int i = 0; i < 9; i++)
            {
                Gacha.TryPull(Table, 1, out var results);
                Assert.AreEqual(ItemGrade.Common, results[0].Lineage.grade);
                Assert.IsFalse(results[0].IsPity);
            }
            Assert.AreEqual(1, Gacha.GetRemainToPity(Table));

            Gacha.TryPull(Table, 1, out var pity);

            Assert.AreEqual("sock", pity[0].Lineage.id);
            Assert.IsTrue(pity[0].IsPity);
            Assert.IsTrue(pity[0].IsNew);
            Assert.AreEqual(0, Gacha.GetPityCount(Table));
            Assert.AreEqual(10, Gacha.GetRemainToPity(Table));
        }

        [Test]
        public void Pity_ResetsWhenGradeRolledNaturally()
        {
            Table.pityCount = 10;
            Table.pityGrade = ItemGrade.Epic;
            Table.rates = Only(ItemGrade.Epic);
            Currency.Add(CurrencyType.Gem, 1e6);

            Gacha.TryPull(Table, 3, out var results);

            Assert.IsTrue(results.All(r => !r.IsPity));
            Assert.AreEqual(0, Gacha.GetPityCount(Table));
        }

        [Test]
        public void Pity_InactiveWithoutLineageOfPityGrade()
        {
            Table.pityCount = 5;
            Table.pityGrade = ItemGrade.Legendary;
            Currency.Add(CurrencyType.Gem, 1e6);

            Assert.AreEqual(-1, Gacha.GetRemainToPity(Table));
            Gacha.TryPull(Table, 10, out var results);

            Assert.IsTrue(results.All(r => !r.IsPity && r.Lineage.grade != ItemGrade.Legendary));
        }

        [Test]
        public void Rates_ExcludeGradesWithoutLineages()
        {
            Table.rates.Add(new GachaGradeRate { grade = ItemGrade.Mythic, weight = 50 });

            var rates = Gacha.GetRates(Table);

            Assert.IsFalse(rates.Any(r => r.Grade == ItemGrade.Legendary || r.Grade == ItemGrade.Mythic));
            Assert.AreEqual(100, rates.Sum(r => r.Percent), 1e-9);
            Assert.AreEqual(70.0 / 99 * 100, rates.First(r => r.Grade == ItemGrade.Common).Percent, 1e-9);
            Assert.AreEqual(0, Gacha.GetRates(null).Count);
        }

        [Test]
        public void Distribution_MatchesWeights()
        {
            Table.singleCost = 0;
            const int pulls = 20000;

            Assert.IsTrue(Gacha.TryPull(Table, pulls, out var results));

            double Ratio(ItemGrade grade) => results.Count(r => r.Lineage.grade == grade) / (double)pulls * 100;
            Assert.AreEqual(70.0 / 99 * 100, Ratio(ItemGrade.Common), 1.5);
            Assert.AreEqual(29.0 / 99 * 100, Ratio(ItemGrade.Rare), 1.5);
            Assert.AreEqual(0, Ratio(ItemGrade.Legendary), 1e-9);
        }

        [Test]
        public void Pull_NoRollableLineage_FailsWithoutSpending()
        {
            Table.rates = Only(ItemGrade.Mythic);
            Currency.Add(CurrencyType.Gem, 1000);
            LogAssert.Expect(LogType.Error, new Regex("뽑을 수 있는 계보가 없습니다"));

            Assert.IsFalse(Gacha.TryPull(Table, 1, out var results));
            Assert.IsNull(results);
            Assert.AreEqual(1000, Currency.Get(CurrencyType.Gem), 1e-9);
        }

        /// <summary>확정 소환은 IsGuaranteed (소환 마일스톤에 세지 않음), 뽑기는 false. 새 친구 StartLevel = 따라잡기 레벨 (RM-2 P4-1)</summary>
        [Test]
        public void Guaranteed_IsFlagged_AndStartLevelFollowsCatchUp()
        {
            var pulled = new List<GachaResult>();
            Gacha.Pulled += (table, results) => pulled.AddRange(results);

            var guaranteed = Gacha.PullGuaranteed(Table, "sock");
            Assert.IsTrue(guaranteed.IsGuaranteed);
            Assert.AreEqual(1, guaranteed.StartLevel, "첫 친구는 Lv.1");
            Assert.AreEqual(1, pulled.Count, "확정 소환도 Pulled를 울린다 (세는 쪽이 IsGuaranteed로 거른다)");
            Data.lineages["sock"].level = 20;

            Table.rates = Only(ItemGrade.Rare);
            Currency.Add(CurrencyType.Gem, 1000);
            Assert.IsTrue(Gacha.TryPull(Table, 1, out var results));
            Assert.IsFalse(results[0].IsGuaranteed);
            Assert.IsTrue(results[0].IsNew);
            Assert.AreEqual(16, results[0].StartLevel, "친구들 평균의 80% (20 × 0.8)");
            Assert.AreEqual(16, Characters.GetLevel("candle"));

            Assert.IsTrue(Gacha.TryPull(Table, 1, out var again));
            Assert.IsFalse(again[0].IsNew);
            Assert.AreEqual(1, again[0].StartLevel, "중복은 1");
            Assert.AreEqual(1, new GachaResult(null, false, false, 0, startLevel: 0).StartLevel, "1 아래는 1");
        }

        [Test]
        public void GetTable_FindsById()
        {
            Assert.AreSame(Table, Gacha.GetTable("test"));
            Assert.IsNull(Gacha.GetTable("nope"));
            Assert.IsNull(Gacha.GetTable(null));
        }
    }
}
