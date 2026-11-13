using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    public class ShopSystemTests : SystemsTestBase
    {
        const string ShopId = "s";

        /// <summary>
        /// cloth: 단추 100 → m_cloth x5 (하루 2회) / thread: 별조각 10 → m_thread x1 (전체 1회)
        /// gold: 별조각 0 → 단추 50 (무제한) / costume: 별빛 사탕 50 → sock_event 의상
        /// </summary>
        [SetUp]
        public void SetUpShop()
        {
            ShopDb.shops = new List<ShopDef>
            {
                new ShopDef
                {
                    id = ShopId,
                    name = "테스트 상점",
                    products = new List<ShopProductDef>
                    {
                        new ShopProductDef { id = "cloth", rewards = { RewardData.Item("m_cloth", 5) }, currency = CurrencyType.Gold, price = 100, dailyLimit = 2 },
                        new ShopProductDef { id = "thread", rewards = { RewardData.Item("m_thread") }, currency = CurrencyType.Gem, price = 10, totalLimit = 1 },
                        new ShopProductDef { id = "gold", rewards = { RewardData.Currency(CurrencyType.Gold, 50) }, currency = CurrencyType.Gem, price = 0 },
                        new ShopProductDef { id = "costume", costumeId = "sock_event", currency = CurrencyType.EventToken, price = 50 },
                        new ShopProductDef { id = "empty", currency = CurrencyType.Gold, price = 1 },
                    },
                },
            };
        }

        ShopProductDef Product(string id) => Shop.GetProduct(ShopId, id);

        [Test]
        public void Buy_SpendsCurrencyAndGrantsRewards()
        {
            Currency.Add(CurrencyType.Gold, 300);
            int changed = 0;
            Shop.Changed += () => changed++;

            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "cloth"));

            Assert.AreEqual(200, Currency.Get(CurrencyType.Gold), 1e-9);
            Assert.AreEqual(5, Inventory.GetCount("m_cloth"));
            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, Shop.GetRemaining(Product("cloth")));
        }

        [Test]
        public void DailyLimit_ResetsOnNextLocalDay()
        {
            Currency.Add(CurrencyType.Gold, 1000);

            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual(ShopBuyResult.SoldOut, Shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual(0, Shop.GetRemaining(Product("cloth")));
            Assert.IsFalse(Shop.CanBuy(Product("cloth")));
            Assert.AreEqual(800, Currency.Get(CurrencyType.Gold), 1e-9, "품절이면 재화를 쓰지 않는다");

            Clock.Advance(TimeSpan.FromDays(1));

            Assert.AreEqual(2, Shop.GetRemaining(Product("cloth")));
            Assert.IsTrue(Shop.CanBuy(Product("cloth")));
            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual(1, Shop.GetRemaining(Product("cloth")));
            Assert.AreEqual(3, Data.shop.totalPurchases["cloth"]);
        }

        [Test]
        public void DayRollover_RaisesChangedAtLocalMidnight()
        {
            // 현지 23:50에 하루 제한을 다 쓰고 상점을 켜 둔 채 자정을 넘긴다
            Clock.UtcNow = DateTime.Today.AddHours(23).AddMinutes(50).ToUniversalTime();
            Currency.Add(CurrencyType.Gold, 1000);
            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "cloth"));
            int changed = 0;
            Shop.Changed += () => changed++;

            Clock.Advance(TimeSpan.FromMinutes(5));
            Currency.Add(CurrencyType.Gold, 1);
            Assert.AreEqual(0, changed, "자정 전에는 알리지 않는다");
            Assert.AreEqual(0, Shop.GetRemaining(Product("cloth")));

            // 방치 수익처럼 재화가 바뀌기만 해도 자정 초기화를 알린다
            Clock.Advance(TimeSpan.FromMinutes(10));
            Currency.Add(CurrencyType.Gold, 1);

            Assert.AreEqual(1, changed, "열려 있는 상점이 품절 표시를 다시 그리도록");
            Assert.AreEqual(2, Shop.GetRemaining(Product("cloth")));
            Assert.AreEqual(0, Data.shop.dailyPurchases.Count);
            Assert.AreEqual(2, Data.shop.totalPurchases["cloth"], "누적 기록은 그대로");
            Assert.IsFalse(Shop.CheckDayRollover(), "같은 날 두 번 초기화하지 않는다");
            Assert.AreEqual(1, changed);
        }

        /// <summary>
        /// 일일 시계(05:00 초기화, UTC 시간대)를 쓰는 상점. 재화는 따로 만든다 — 기본 상점(Shop, 일일 시계 없음)이 공유 재화의 Changed를
        /// 박동 삼아 현지 자정 규칙으로 기록을 비우지 않게 (같은 PlayerData를 쓰므로).
        /// </summary>
        (ShopSystem shop, CurrencySystem currency, DailyClock clock) CreateDailyShop(DateTime utcNow)
        {
            Clock.UtcNow = utcNow;
            var dailyClock = new DailyClock(Data, Clock, TimeZoneInfo.Utc);
            dailyClock.Refresh();
            var currency = new CurrencySystem(Data);
            var shop = new ShopSystem(Data, ShopDb, currency, Rewards, Costumes, Clock, dailyClock);
            return (shop, currency, dailyClock);
        }

        [Test]
        public void WithDailyClock_DailyLimit_ResetsAt0500_OnDayChanged()
        {
            var (shop, currency, dailyClock) = CreateDailyShop(new DateTime(2026, 11, 4, 4, 0, 0, DateTimeKind.Utc));
            currency.Add(CurrencyType.Gold, 1000);
            Assert.AreEqual(ShopBuyResult.Ok, shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual(ShopBuyResult.Ok, shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual(ShopBuyResult.SoldOut, shop.TryBuy(ShopId, "cloth"));
            Assert.AreEqual("2026-11-03", Data.shop.dailyDate, "05:00 전은 전날 (DailyClock 날 키)");
            int changed = 0;
            shop.Changed += () => changed++;

            Clock.UtcNow = new DateTime(2026, 11, 4, 4, 59, 59, DateTimeKind.Utc);
            dailyClock.Refresh();
            Assert.AreEqual(0, changed, "새벽 5시 전에는 그대로");
            Assert.AreEqual(0, shop.GetRemaining(shop.GetProduct(ShopId, "cloth")));

            Clock.UtcNow = new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(dailyClock.Refresh(), "날이 바뀜");

            Assert.AreEqual(1, changed, "DayChanged로 바로 비우고 알린다 (열린 상점이 품절 표시를 다시 그림)");
            Assert.AreEqual(2, shop.GetRemaining(shop.GetProduct(ShopId, "cloth")));
            Assert.AreEqual("2026-11-04", Data.shop.dailyDate);
            Assert.AreEqual(2, Data.shop.totalPurchases["cloth"], "누적 기록은 그대로");
            Assert.IsFalse(shop.CheckDayRollover(), "같은 날 두 번 비우지 않는다");
        }

        [Test]
        public void WithDailyClock_LazyCount_FollowsDayKey_WithoutRefresh()
        {
            var (shop, currency, _) = CreateDailyShop(new DateTime(2026, 11, 4, 4, 30, 0, DateTimeKind.Utc));
            currency.Add(CurrencyType.Gold, 1000);
            shop.TryBuy(ShopId, "cloth");
            shop.TryBuy(ShopId, "cloth");

            // 시계가 Refresh되지 않아도(메타가 꺼진 경우) 읽을 때 날 키를 비교한다
            Clock.UtcNow = new DateTime(2026, 11, 4, 6, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(2, shop.GetRemaining(shop.GetProduct(ShopId, "cloth")));
        }

        [Test]
        public void WithDailyClock_RollbackDoesNotRefill()
        {
            var (shop, currency, dailyClock) = CreateDailyShop(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc));
            currency.Add(CurrencyType.Gold, 1000);
            shop.TryBuy(ShopId, "cloth");
            shop.TryBuy(ShopId, "cloth");

            Clock.UtcNow = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc); // 기기 시계를 하루 전으로
            dailyClock.Refresh();

            Assert.AreEqual(0, shop.GetRemaining(shop.GetProduct(ShopId, "cloth")), "날 키는 거꾸로 가지 않아 어제 한도를 다시 쓸 수 없다");
        }

        [Test]
        public void TimeUntilReset_IsTimeToNext0500()
        {
            var (shop, _, _) = CreateDailyShop(new DateTime(2026, 11, 4, 4, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(TimeSpan.FromHours(1), shop.TimeUntilReset);

            Clock.UtcNow = new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(TimeSpan.FromHours(24), shop.TimeUntilReset);

            var legacy = Shop.TimeUntilReset; // 일일 시계 없음 = 다음 현지 자정까지
            Assert.Greater(legacy, TimeSpan.Zero);
            Assert.LessOrEqual(legacy, TimeSpan.FromHours(25));
        }

        [Test]
        public void DayRollover_WithoutDailyPurchases_IsSilent()
        {
            int changed = 0;
            Shop.Changed += () => changed++;

            Assert.IsFalse(Shop.CheckDayRollover());
            Clock.Advance(TimeSpan.FromDays(1));
            Assert.IsFalse(Shop.CheckDayRollover());
            Currency.Add(CurrencyType.Gold, 1);

            Assert.AreEqual(0, changed);
        }

        [Test]
        public void TotalLimit_NeverResets()
        {
            Currency.Add(CurrencyType.Gem, 100);

            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "thread"));
            Assert.AreEqual(ShopBuyResult.SoldOut, Shop.TryBuy(ShopId, "thread"));

            Clock.Advance(TimeSpan.FromDays(1));

            Assert.AreEqual(0, Shop.GetRemaining(Product("thread")));
            Assert.AreEqual(ShopBuyResult.SoldOut, Shop.TryBuy(ShopId, "thread"));
            Assert.AreEqual(1, Inventory.GetCount("m_thread"));
        }

        [Test]
        public void Unlimited_RemainingIsMinusOne()
        {
            Assert.AreEqual(-1, Shop.GetRemaining(Product("gold")));
            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "gold"), "무료 상품");
            Assert.AreEqual(50, Currency.Get(CurrencyType.Gold), 1e-9);
        }

        [Test]
        public void NotEnoughCurrency_ChangesNothing()
        {
            Currency.Add(CurrencyType.Gold, 50);

            Assert.AreEqual(ShopBuyResult.NotEnoughCurrency, Shop.TryBuy(ShopId, "cloth"));
            Assert.IsFalse(Shop.CanBuy(Product("cloth")));

            Assert.AreEqual(50, Currency.Get(CurrencyType.Gold), 1e-9);
            Assert.AreEqual(0, Inventory.GetCount("m_cloth"));
            Assert.AreEqual(2, Shop.GetRemaining(Product("cloth")));
        }

        [Test]
        public void UnknownOrBrokenProduct_NotFound()
        {
            Currency.Add(CurrencyType.Gold, 100);

            Assert.AreEqual(ShopBuyResult.NotFound, Shop.TryBuy("nope", "cloth"));
            Assert.AreEqual(ShopBuyResult.NotFound, Shop.TryBuy(ShopId, "nope"));
            Assert.AreEqual(ShopBuyResult.NotFound, Shop.TryBuy(null, null));
            Assert.AreEqual(ShopBuyResult.NotFound, Shop.TryBuy(ShopId, "empty"), "지급 내용이 없는 상품");
            Assert.AreEqual(100, Currency.Get(CurrencyType.Gold), 1e-9);
            Assert.AreEqual(0, Shop.GetRemaining(null));
            Assert.IsFalse(Shop.CanBuy(null));
        }

        [Test]
        public void CostumeProduct_UnlocksOnce()
        {
            Characters.Grant("sock");
            Currency.Add(CurrencyType.EventToken, 120);
            Assert.AreEqual(1, Shop.GetRemaining(Product("costume")));

            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(ShopId, "costume"));

            Assert.IsTrue(Costumes.IsUnlocked("sock_event"));
            Assert.AreEqual(70, Currency.Get(CurrencyType.EventToken), 1e-9);
            Assert.AreEqual(0, Shop.GetRemaining(Product("costume")));
            Assert.AreEqual(ShopBuyResult.AlreadyOwned, Shop.TryBuy(ShopId, "costume"));
            Assert.AreEqual(70, Currency.Get(CurrencyType.EventToken), 1e-9);
        }

        [Test]
        public void CostumeProduct_AlreadyOwnedFromOtherSource()
        {
            Currency.Add(CurrencyType.EventToken, 100);
            Costumes.Unlock("sock_event");

            Assert.AreEqual(ShopBuyResult.AlreadyOwned, Shop.TryBuy(ShopId, "costume"));
            Assert.AreEqual(100, Currency.Get(CurrencyType.EventToken), 1e-9);
        }

        [Test]
        public void Describe_NamesMissingCurrency()
        {
            Assert.AreEqual("별빛 사탕이 부족해요", ShopSystem.Describe(ShopBuyResult.NotEnoughCurrency, Product("costume")));
            Assert.AreEqual("단추가 부족해요", ShopSystem.Describe(ShopBuyResult.NotEnoughCurrency, Product("cloth")));
            Assert.AreEqual("재화가 부족해요", ShopSystem.Describe(ShopBuyResult.NotEnoughCurrency));
        }

        [Test]
        public void Defaults_ContainNormalAndEventShops()
        {
            var db = ScriptableObject.CreateInstance<ShopDatabase>();
            try
            {
                db.ApplyDefaults();

                var normal = db.Get(ShopDatabase.NormalShopId);
                var evt = db.Get(ShopDatabase.EventShopId);
                Assert.AreEqual("수선점 상점", normal.name);
                Assert.AreEqual("별이 머문 밤 산책", evt.name);
                Assert.IsTrue(normal.products.All(p => p.dailyLimit > 0 && p.rewards.Count > 0));
                Assert.IsTrue(normal.products.All(p => p.currency == CurrencyType.Gold || p.currency == CurrencyType.Gem));
                Assert.IsTrue(evt.products.All(p => p.currency == CurrencyType.EventToken));

                var sleepcap = evt.products.Single(p => p.costumeId == "jjak_sleepcap");
                var festival = evt.products.Single(p => p.costumeId == "jjak_festival");
                Assert.AreEqual(150, sleepcap.price, 1e-9);
                Assert.AreEqual(300, festival.price, 1e-9);

                var suitcase = evt.products.Single(p => p.rewards.Any(r => r.itemId == "mat_suitcase"));
                var moonlight = evt.products.Single(p => p.rewards.Any(r => r.itemId == "mat_moonlight"));
                Assert.AreEqual(5, suitcase.dailyLimit);
                Assert.AreEqual(10, moonlight.totalLimit);

                var ids = db.shops.SelectMany(s => s.products).Select(p => p.id).ToList();
                Assert.AreEqual(ids.Count, ids.Distinct().Count(), "상품 ID는 상점 전체에서 고유");

                // M30: 상품 이름에 개수를 넣지 않는다 (배지 "×5"가 맡음) — 그래도 한 상점 안에서 이름은 고유
                foreach (var shop in db.shops)
                {
                    foreach (var product in shop.products)
                        StringAssert.DoesNotMatch(@"\s[x×]\d", product.name, $"{product.id}: 이름에 개수가 없다");
                    var names = shop.products.Select(p => p.name).ToList();
                    Assert.AreEqual(names.Count, names.Distinct().Count(), $"{shop.id}: 상품 이름 고유");
                }
                Assert.AreEqual("수선 천", normal.products.Single(p => p.id == "normal_cloth").name);
                StringAssert.Contains("새벽 5시", normal.description, "구매 횟수는 새벽 5시에 다시 채워진다 (M77)");
            }
            finally
            {
                Object.DestroyImmediate(db);
            }
        }
    }
}
