using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 획득처를 지금 갈 수 있는지 (SourceAvailability, M17): 원정(기능 + 구역 잠금, UnlockSentence), 던전(기능), 상점(기능 + 오늘 재고),
    /// 스테이지(늘 열림), 첫 번째 갈 수 있는 곳, 표시 순서(열린 곳 → 스테이지 → 잠긴 곳을 열리는 스테이지 순).
    /// </summary>
    public class SourceAvailabilityTests : MetaSystemsTestBase
    {
        const string ShopId = "s";

        static ItemSource Src(ItemSourceType type, string target) => new ItemSource { type = type, targetId = target, label = target };

        /// <summary>원정 1-4 / 상점 1-8 / 던전 1-10 클리어로 열림. 상점 "s"는 m_cloth x5를 하루 1번 판다</summary>
        void SetupGates(int cleared)
        {
            AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            AddFeature(FeatureId.Dungeon, MetaCondition.StageCleared(1, 10));
            ShopDb.shops = new List<ShopDef>
            {
                new ShopDef
                {
                    id = ShopId,
                    name = "테스트 상점",
                    products = new List<ShopProductDef>
                    {
                        new ShopProductDef { id = "cloth", name = "수선 천", rewards = { RewardData.Item("m_cloth", 5) }, currency = CurrencyType.Gold, price = 10, dailyLimit = 1 },
                    },
                },
            };
            Rebuild();
            SetCleared(cleared);
            Features.Evaluate();
        }

        SourceCheck Check(ItemSource source, string itemId = "m_cloth") =>
            SourceAvailability.Check(source, itemId, Features, Expedition, Shop);

        [Test]
        public void Stage_IsAlwaysAvailable()
        {
            SetupGates(cleared: 1);
            var check = Check(Src(ItemSourceType.Stage, ""));
            Assert.IsTrue(check.Available);
            Assert.AreEqual(string.Empty, check.Reason);
            StringAssert.Contains("스테이지", SourceAvailability.StageHint);
        }

        [Test]
        public void Expedition_NeedsFeature_ThenZone()
        {
            SetupGates(cleared: 2);
            var (available, reason) = Check(Src(ItemSourceType.Expedition, "exp_1_1"));
            Assert.IsFalse(available);
            Assert.AreEqual("스테이지 1-4를 클리어하면 열려요", reason, "기능 잠금은 개방 안내와 같은 문장");

            SetCleared(4);
            Features.Evaluate();
            Assert.IsTrue(Check(Src(ItemSourceType.Expedition, "exp_1_1")).Available);

            var far = Check(Src(ItemSourceType.Expedition, "exp_2_4")); // requiredStage 20 (도달)
            Assert.IsFalse(far.Available);
            Assert.AreEqual("스테이지 2-9를 클리어하면 열려요", far.Reason, "구역 잠금 = MetaText.UnlockSentence(requiredStage)");
            Assert.AreEqual(20, far.RequiredStage);

            var missing = Check(Src(ItemSourceType.Expedition, "nope"));
            Assert.IsFalse(missing.Available);
            Assert.AreEqual(SourceAvailability.ZoneMissingReason, missing.Reason);
        }

        [Test]
        public void Dungeon_NeedsFeature()
        {
            SetupGates(cleared: 9);
            var locked = Check(Src(ItemSourceType.Dungeon, "3"));
            Assert.IsFalse(locked.Available);
            Assert.AreEqual("스테이지 1-10을 클리어하면 열려요", locked.Reason);
            Assert.AreEqual(11, locked.RequiredStage, "기능이 열리는 도달 스테이지 (1-10 클리어 = 11)");

            SetCleared(10);
            Features.Evaluate();
            Assert.IsTrue(Check(Src(ItemSourceType.Dungeon, "3")).Available);
        }

        [Test]
        public void Shop_NeedsFeature_AndStockToday()
        {
            SetupGates(cleared: 7);
            Assert.IsFalse(Check(Src(ItemSourceType.Shop, ShopId)).Available, "상점 잠김");

            SetCleared(8);
            Features.Evaluate();
            Assert.IsTrue(Check(Src(ItemSourceType.Shop, ShopId)).Available);

            Currency.Add(CurrencyType.Gold, 100);
            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "cloth"));
            var soldOut = Check(Src(ItemSourceType.Shop, ShopId));
            Assert.IsFalse(soldOut.Available);
            Assert.AreEqual("오늘 상점 수량을 모두 샀어요", soldOut.Reason);

            var notSold = Check(Src(ItemSourceType.Shop, ShopId), "m_thread");
            Assert.IsFalse(notSold.Available);
            Assert.AreEqual(SourceAvailability.ShopMissingReason, notSold.Reason);
            Assert.IsTrue(Check(Src(ItemSourceType.Shop, ShopId), null).Available, "재료를 모르면 기능만 본다");
        }

        [Test]
        public void FirstAvailable_FollowsContentOrder_SkipsLocked()
        {
            SetupGates(cleared: 4); // 원정만 열림
            var item = new ItemDef
            {
                id = "m_cloth",
                sources = new List<ItemSource>
                {
                    Src(ItemSourceType.Shop, ShopId),
                    Src(ItemSourceType.Expedition, "exp_2_4"),
                    Src(ItemSourceType.Expedition, "exp_1_1"),
                    Src(ItemSourceType.Stage, ""),
                },
            };

            var first = SourceAvailability.FirstAvailable(item, out var reason, Features, Expedition, Shop);

            Assert.AreEqual("exp_1_1", first.targetId);
            Assert.AreEqual("스테이지 1-8을 클리어하면 열려요", reason, "처음 막힌 곳의 이유");

            item.sources.RemoveAt(2);
            item.sources.RemoveAt(2);
            Assert.IsNull(SourceAvailability.FirstAvailable(item, out reason, Features, Expedition, Shop), "모두 잠김");
            Assert.AreEqual("스테이지 1-8을 클리어하면 열려요", reason);
        }

        [Test]
        public void SortForDisplay_AvailableFirst_StageLast_ThenLockedByStage()
        {
            SetupGates(cleared: 4);
            var item = new ItemDef
            {
                id = "m_cloth",
                sources = new List<ItemSource>
                {
                    Src(ItemSourceType.Stage, ""),
                    Src(ItemSourceType.Expedition, "exp_2_4"), // 잠김 (도달 20)
                    Src(ItemSourceType.Shop, ShopId), // 잠김 (도달 9)
                    Src(ItemSourceType.Expedition, "exp_1_1"), // 열림
                    Src(ItemSourceType.Expedition, "exp_1_3"), // 열림 (도달 5)
                },
            };
            var sorted = new List<ItemSource>();
            var checks = new List<SourceCheck>();

            SourceAvailability.SortForDisplay(item, sorted, checks, Features, Expedition, Shop);

            CollectionAssert.AreEqual(new[] { "exp_1_1", "exp_1_3", "", ShopId, "exp_2_4" }, sorted.ConvertAll(s => s.targetId));
            Assert.AreEqual(sorted.Count, checks.Count);
            Assert.IsTrue(checks[0].Available && checks[1].Available && checks[2].Available);
            Assert.IsFalse(checks[3].Available);
            Assert.AreEqual(9, checks[3].RequiredStage);
            Assert.AreEqual(20, checks[4].RequiredStage);
        }
    }
}
