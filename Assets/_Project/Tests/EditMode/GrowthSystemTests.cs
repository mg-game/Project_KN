using NUnit.Framework;

namespace IdleGame.Tests
{
    public class GrowthSystemTests : SystemsTestBase
    {
        [Test]
        public void LevelUpCost_FollowsGrowthCurve()
        {
            // 안 D 기본 비용 12 (단추 수입이 절반, bal 04_bal_d_note.md D2)
            Assert.AreEqual(12, Growth.GetLevelUpCost(1), 1e-9);
            Assert.AreEqual(12 * 1.12, Growth.GetLevelUpCost(2), 1e-9);
            Assert.AreEqual(12 + 12 * 1.12, Growth.GetLevelUpCost(1, 2), 1e-9);
        }

        [Test]
        public void TryLevelUp_SpendsGold()
        {
            Config.levelUpBaseCost = 10;
            Currency.Add(CurrencyType.Gold, 10);

            Assert.IsTrue(Growth.TryLevelUp());
            Assert.AreEqual(2, Growth.Level);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gold), 1e-9);
        }

        [Test]
        public void TryLevelUp_NotEnoughGold_ChangesNothing()
        {
            Currency.Add(CurrencyType.Gold, 9);

            Assert.IsFalse(Growth.TryLevelUp());
            Assert.AreEqual(1, Growth.Level);
            Assert.AreEqual(9, Currency.Get(CurrencyType.Gold), 1e-9);
        }

        [Test]
        public void LevelUpCurrency_DefaultsToGold_AndFollowsConfig()
        {
            // SYS_Economy §3 #8 E안 이음매: 기본은 단추, 설정을 바꾸면 그 재화로 강화한다 (나중에 재화를 나눌 때 이 한 칸만 바꾼다)
            Assert.AreEqual(CurrencyType.Gold, Growth.LevelUpCurrency);

            Config.shopUpgradeCurrency = CurrencyType.EventToken;
            Config.levelUpBaseCost = 10;
            Currency.Add(CurrencyType.Gold, 1000);
            Currency.Add(CurrencyType.EventToken, 10);
            double gold = Currency.Get(CurrencyType.Gold);

            Assert.AreEqual(1, Growth.GetAffordableCount(5), "가진 강화 재화 10 = 1레벨치");
            Assert.IsTrue(Growth.TryLevelUp());
            Assert.AreEqual(gold, Currency.Get(CurrencyType.Gold), 1e-9, "단추는 쓰지 않는다");
            Assert.AreEqual(0, Currency.Get(CurrencyType.EventToken), 1e-9);
            Assert.IsFalse(Growth.TryLevelUp(), "강화 재화가 없으면 단추가 있어도 못 올린다");
        }

        [Test]
        public void TryLevelUp_Multiple_RequiresFullCost()
        {
            Currency.Add(CurrencyType.Gold, Growth.GetLevelUpCost(1, 3));

            Assert.IsFalse(Growth.TryLevelUp(10));
            Assert.AreEqual(1, Growth.Level);
        }

        [Test]
        public void LevelUpMax_UsesAllAffordableGold()
        {
            Currency.Add(CurrencyType.Gold, Growth.GetLevelUpCost(1, 3) + 5);

            Assert.AreEqual(3, Growth.LevelUpMax());
            Assert.AreEqual(4, Growth.Level);
            Assert.AreEqual(5, Currency.Get(CurrencyType.Gold), 1e-6);
        }

        [Test]
        public void MaxLevel_StopsLevelUp()
        {
            Config.maxLevel = 3;
            Currency.Add(CurrencyType.Gold, 1e9);

            Assert.AreEqual(2, Growth.LevelUpMax());
            Assert.IsTrue(Growth.IsMaxLevel);
            Assert.IsFalse(Growth.TryLevelUp());
        }

        [Test]
        public void GoldPerSecond_IncludesItemBonus()
        {
            double baseGps = Growth.GoldPerSecond;
            Inventory.Add("l1");

            Assert.AreEqual(baseGps * 1.2, Growth.GoldPerSecond, 1e-9);
        }

        [Test]
        public void GoldPerSecond_ScalesWithHighestStage()
        {
            double baseGps = Growth.GoldPerSecond;
            Data.battle.highestStage = 11;

            Assert.AreEqual(baseGps * (1 + GrowthSystem.StageGoldBonusPerStage * 10), Growth.GoldPerSecond, 1e-9);
            Assert.AreEqual(Growth.GoldPerSecond, Growth.GetGoldPerSecondAt(Growth.Level), 1e-9);
        }

        [Test]
        public void ShopMaxHp_GrowsWithLevel()
        {
            Assert.AreEqual(GrowthSystem.ShopBaseHp, Growth.ShopMaxHp, 1e-3);
            Currency.Add(CurrencyType.Gold, 1e6);

            Growth.TryLevelUp(5);

            Assert.Greater(Growth.ShopMaxHp, GrowthSystem.ShopBaseHp);
            Assert.AreEqual(GrowthSystem.CalcShopMaxHp(6), Growth.ShopMaxHp, 1e-3);
            Assert.Greater(Growth.GetShopMaxHpAt(7), Growth.ShopMaxHp, "다음 레벨 미리보기");
        }

        [Test]
        public void LevelReward_SentByMailOncePerMilestone()
        {
            Config.levelRewardInterval = 10;
            Config.levelRewardGem = 50;
            Currency.Add(CurrencyType.Gold, 1e12);

            Assert.IsTrue(Growth.TryLevelUp(24));
            Assert.AreEqual(25, Growth.Level);
            Assert.AreEqual(2, Mail.Count, "10, 20레벨 보상");

            Mail.ClaimAll();
            Assert.AreEqual(100, Currency.Get(CurrencyType.Gem), 1e-9);

            // 레벨이 초기화돼도 같은 마일스톤 보상은 다시 오지 않는다
            Data.level = 1;
            Growth.TryLevelUp(10);
            Assert.AreEqual(0, Mail.Count);
        }
    }
}
