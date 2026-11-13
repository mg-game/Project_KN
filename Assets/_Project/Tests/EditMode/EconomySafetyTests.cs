using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    /// <summary>
    /// 경제 안전 (I-14 · SYS_Economy §2.1): 지급 · 소비 · 로드 검증, 재료 상한, 별조각 유료/무료 2풀.
    /// 잘못된 지급의 오류 로그는 LogAssert.Expect로 기대한다 (기대하지 않은 오류 로그는 테스트를 실패시킨다).
    /// </summary>
    public class EconomySafetyTests : SystemsTestBase
    {
        static readonly Regex CurrencyRejected = new(@"\[Currency\] 잘못된 지급");
        static readonly Regex InventoryRejected = new(@"\[Inventory\] 잘못된 지급");

        // ───────────── 지급 · 소비 검증 ─────────────

        [TestCase(-5.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void CurrencyAdd_RejectsInvalidAmount_WithError(double amount)
        {
            Currency.Add(CurrencyType.Gold, 100);
            LogAssert.Expect(LogType.Error, CurrencyRejected);
            Currency.Add(CurrencyType.Gold, amount);
            Assert.AreEqual(100, Currency.Get(CurrencyType.Gold), 1e-9);
        }

        [Test]
        public void CurrencyAdd_Zero_IsSilentNoChange()
        {
            int changed = 0;
            Currency.Changed += (_, _) => changed++;
            Currency.Add(CurrencyType.Gold, 0);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gold), 1e-9);
            Assert.AreEqual(0, changed, "0은 지급 없음 — 이벤트도 로그도 없다");
        }

        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        public void CurrencySpend_NegativeOrNaN_Throws(double amount)
        {
            Currency.Add(CurrencyType.Gold, 100);
            Assert.Throws<ArgumentOutOfRangeException>(() => Currency.TrySpend(CurrencyType.Gold, amount));
            Assert.AreEqual(100, Currency.Get(CurrencyType.Gold), 1e-9);
        }

        [Test]
        public void CurrencyAdd_NearMaxValue_StopsAtMaxValue()
        {
            Currency.Add(CurrencyType.Gold, double.MaxValue);
            Currency.Add(CurrencyType.Gold, double.MaxValue);
            Assert.IsFalse(double.IsInfinity(Currency.Get(CurrencyType.Gold)), "∞를 저장하지 않는다");
            Assert.AreEqual(double.MaxValue, Currency.Get(CurrencyType.Gold));
        }

        // ───────────── 로드 검증 ─────────────

        [Test]
        public void Load_InvalidAmounts_AreZeroedOrRemoved()
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>(
                "{\"currencies\":{\"Gold\":-5,\"Gem\":\"NaN\",\"EventToken\":\"Infinity\"},\"items\":{\"m_cloth\":-3,\"m_thread\":4},\"gemPaid\":-1,\"gemFree\":\"NaN\"}");
            data.Sanitize();

            Assert.AreEqual(0, data.currencies[CurrencyType.Gold]);
            Assert.AreEqual(0, data.currencies[CurrencyType.Gem]);
            Assert.AreEqual(0, data.currencies[CurrencyType.EventToken]);
            Assert.IsFalse(data.items.ContainsKey("m_cloth"), "음수 재료는 지운다");
            Assert.AreEqual(4, data.items["m_thread"], "정상 값은 그대로");
            Assert.AreEqual(0, data.gemPaid);
            Assert.AreEqual(0, data.gemFree);
        }

        // ───────────── 재료 상한 ─────────────

        [Test]
        public void InventoryAdd_StopsAtCap_AndReportsActualAdded()
        {
            Items.countCap = 10;
            int reported = 0;
            Inventory.ItemAdded += (_, n) => reported += n;
            Inventory.Add("m_cloth", 8);
            Inventory.Add("m_cloth", 5);
            Assert.AreEqual(10, Inventory.GetCount("m_cloth"));
            Assert.AreEqual(10, reported, "8 + 상한까지 2");

            Inventory.Add("m_cloth", 1);
            Assert.AreEqual(10, Inventory.GetCount("m_cloth"), "가득 차면 더 늘지 않는다");
            Assert.AreEqual(10, reported, "하나도 못 넣으면 ItemAdded가 울리지 않는다");
        }

        [Test]
        public void InventoryAdd_HugeCount_DoesNotOverflow()
        {
            Inventory.Add("m_cloth", ItemDatabase.DefaultCountCap - 1);
            Inventory.Add("m_cloth", int.MaxValue);
            Assert.AreEqual(ItemDatabase.DefaultCountCap, Inventory.GetCount("m_cloth"));
        }

        [Test]
        public void InventoryAdd_Negative_RejectedWithError_ZeroIgnored()
        {
            Inventory.Add("m_cloth", 3);
            LogAssert.Expect(LogType.Error, InventoryRejected);
            Inventory.Add("m_cloth", -2);
            Inventory.Add("m_cloth", 0);
            Assert.AreEqual(3, Inventory.GetCount("m_cloth"));
        }

        [Test]
        public void Inventory_SavedCountOverCap_IsClampedOnCreate()
        {
            Data.items["m_cloth"] = int.MaxValue;
            var inventory = new InventorySystem(Data, Items);
            Assert.AreEqual(ItemDatabase.DefaultCountCap, inventory.GetCount("m_cloth"));
        }

        // ───────────── 별조각 2풀 ─────────────

        [Test]
        public void Gem_GrantGoesToFree_PurchaseGoesToPaid_TotalIsSum()
        {
            Currency.Add(CurrencyType.Gem, 300);
            Currency.Add(CurrencyType.Gem, 50, CurrencySource.Purchase);
            Assert.AreEqual(300, Currency.GemFree, 1e-9);
            Assert.AreEqual(50, Currency.GemPaid, 1e-9);
            Assert.AreEqual(350, Currency.Get(CurrencyType.Gem), 1e-9, "currencies[Gem] = 합");
        }

        [Test]
        public void Gem_Spend_UsesFreeFirst_ThenPaid()
        {
            Currency.Add(CurrencyType.Gem, 100);
            Currency.Add(CurrencyType.Gem, 50, CurrencySource.Purchase);

            Assert.IsTrue(Currency.TrySpend(CurrencyType.Gem, 80));
            Assert.AreEqual(20, Currency.GemFree, 1e-9);
            Assert.AreEqual(50, Currency.GemPaid, 1e-9, "무료가 남아 있으면 유료는 그대로");

            Assert.IsTrue(Currency.TrySpend(CurrencyType.Gem, 40));
            Assert.AreEqual(0, Currency.GemFree, 1e-9);
            Assert.AreEqual(30, Currency.GemPaid, 1e-9, "무료를 다 쓰고 모자란 20만 유료에서");
            Assert.AreEqual(30, Currency.Get(CurrencyType.Gem), 1e-9);

            Assert.IsFalse(Currency.TrySpend(CurrencyType.Gem, 31), "합보다 많으면 실패, 풀은 그대로");
            Assert.AreEqual(30, Currency.GemPaid, 1e-9);
        }

        [Test]
        public void Gem_OldSave_MigratesAllToFree()
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>("{\"version\":4,\"currencies\":{\"Gem\":1300}}");
            data.Sanitize();
            Assert.AreEqual(0, data.gemPaid);
            Assert.AreEqual(1300, data.gemFree, "옛 세이브 = 전부 무료");
            Assert.AreEqual(1300, data.currencies[CurrencyType.Gem]);
        }

        [Test]
        public void Gem_NewGame_StartGemIsFree()
        {
            Config.startGem = 1000;
            var data = PlayerData.CreateNew(Config);
            Assert.AreEqual(1000, data.gemFree);
            Assert.AreEqual(0, data.gemPaid);
        }

        [Test]
        public void Gem_TotalChangedElsewhere_PoolsFollowTotal()
        {
            Currency.Add(CurrencyType.Gem, 50, CurrencySource.Purchase);
            Data.currencies[CurrencyType.Gem] = 500; // 풀을 거치지 않은 변경 (옛 경로 · 세이브 편집)
            Assert.AreEqual(50, Currency.GemPaid, 1e-9);
            Assert.AreEqual(450, Currency.GemFree, 1e-9, "차이는 무료 풀에서 맞춘다");

            Data.currencies[CurrencyType.Gem] = 20; // 유료보다 적어지면 유료도 합까지
            Assert.AreEqual(20, Currency.GemPaid, 1e-9);
            Assert.AreEqual(0, Currency.GemFree, 1e-9);
        }

        [Test]
        public void Gem_Pools_SurviveSaveRoundTrip()
        {
            Currency.Add(CurrencyType.Gem, 120);
            Currency.Add(CurrencyType.Gem, 30, CurrencySource.Purchase);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(Data);
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>(json);
            loaded.Sanitize();
            Assert.AreEqual(30, loaded.gemPaid, 1e-9);
            Assert.AreEqual(120, loaded.gemFree, 1e-9);
            Assert.AreEqual(150, loaded.currencies[CurrencyType.Gem], 1e-9);
        }
    }
}
