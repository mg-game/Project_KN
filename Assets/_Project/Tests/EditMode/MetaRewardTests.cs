using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>메타 보상 (MetaRewardService): 단추 N분치, 수선권 상한, 선택 기억편, 친구 확정(중복 → 기억편), 의상, 배수(별조각 제외), 문구, 합치기</summary>
    public class MetaRewardTests : MetaSystemsTestBase
    {
        [Test]
        public void GoldMinutes_UseIncomeAtGrantTime_SplitOnce()
        {
            SetCleared(4); // 초당 수입이 최고 스테이지로도 오른다
            double income = Growth.IncomePerSecond;

            var granted = MetaRewards.Grant(new[] { MetaReward.GoldMinutes(5) });

            // 안 D: B = 기준 초당 수입(나누기 전) × 60 × 분, 단추 = ⌊B × 0.5⌋ · 실타래 = ⌊B × 0.5⌋ (이미 나뉜 초당 단추로 B를 만들어 다시 나누지 않는다)
            Assert.AreEqual(Math.Floor(income * 60 * 5 * 0.5), Gold);
            Assert.AreEqual(Math.Floor(income * 60 * 5 * 0.5), Currency.Get(CurrencyType.LineageXp));
            Assert.AreEqual(2, granted.Count);
            Assert.AreEqual(MetaRewardType.Currency, granted[0].type, "결과에는 GoldMinutes가 없다 (단추 · 실타래로 풀림)");
            Assert.AreEqual(CurrencyType.Gold, granted[0].currency, "순서 = 단추 → 실타래");
            Assert.AreEqual(CurrencyType.LineageXp, granted[1].currency);
            var resolved = new List<MetaReward>();
            MetaRewards.ResolveAll(new[] { MetaReward.GoldMinutes(5) }, resolved);
            Assert.AreEqual(Math.Floor(income * 300 * 0.5), resolved[0].amount, "ResolveAll 단추 줄 = 지급과 같은 값");
        }

        [Test]
        public void CurrencyAndItems_GoThroughRewardService()
        {
            var granted = MetaRewards.Grant(new List<MetaReward>
            {
                MetaReward.Gem(30), MetaReward.Item("m_cloth", 5), MetaReward.Gem(20), MetaReward.Currency(CurrencyType.EventToken, 7),
            });

            Assert.AreEqual(50, Gem);
            Assert.AreEqual(5, ItemCount("m_cloth"));
            Assert.AreEqual(7, Currency.Get(CurrencyType.EventToken));
            Assert.AreEqual(3, granted.Count, "같은 종류는 합친다");
            Assert.AreEqual(50, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem));
        }

        [Test]
        public void QuickIdleTickets_CapAtTwenty_OverflowDiscarded()
        {
            int walletChanged = 0;
            MetaRewards.WalletChanged += () => walletChanged++;

            MetaRewards.Grant(new[] { MetaReward.QuickIdleTicket(15) });
            var granted = MetaRewards.Grant(new[] { MetaReward.QuickIdleTicket(10) });

            Assert.AreEqual(20, MetaRewards.QuickIdleTickets);
            Assert.AreEqual(5, AmountOf(granted, MetaRewardType.QuickIdleTicket), "실제로 들어간 양만");
            Assert.AreEqual(0, Gem, "넘친 양은 별조각으로 바꾸지 않는다");
            Assert.IsEmpty(MetaRewards.Grant(new[] { MetaReward.QuickIdleTicket(1) }), "가득이면 아무것도 못 받음");
            Assert.AreEqual(2, walletChanged);
            Assert.AreEqual(MetaRewardService.DefaultMaxQuickIdleTickets, MetaRewards.MaxQuickIdleTickets);
        }

        [Test]
        public void Wallet_SpendAndAssignSelectShards()
        {
            MetaRewards.Grant(new[] { MetaReward.DoubleTicket(2), MetaReward.SelectShards(30), MetaReward.QuickIdleTicket(1) });

            Assert.IsTrue(MetaRewards.TrySpendDoubleTicket());
            Assert.AreEqual(1, MetaRewards.DoubleTickets);
            Assert.IsTrue(MetaRewards.TrySpendQuickIdleTicket());
            Assert.IsFalse(MetaRewards.TrySpendQuickIdleTicket());

            Assert.IsFalse(MetaRewards.TryAssignSelectShards("sock", 10), "미보유 계보");
            Characters.Grant("sock");
            Assert.IsFalse(MetaRewards.TryAssignSelectShards("sock", 31), "보유량 초과");
            Assert.IsFalse(MetaRewards.TryAssignSelectShards("sock", 0));
            Assert.IsTrue(MetaRewards.TryAssignSelectShards("sock", 20));
            Assert.AreEqual(20, Characters.GetShards("sock"));
            Assert.AreEqual(10, MetaRewards.SelectShards);
        }

        [Test]
        public void Lineage_GrantsOrConvertsToShards()
        {
            var first = MetaRewards.Grant(new[] { MetaReward.Lineage("candle") });
            Assert.IsTrue(Characters.IsOwned("candle"));
            Assert.AreEqual(MetaRewardType.Lineage, first[0].type);

            MetaRewards.Grant(new[] { MetaReward.Lineage("candle") });
            Assert.AreEqual(Config.GetDuplicateShards(ItemGrade.Rare), Characters.GetShards("candle"), "중복이면 등급별 기억편");
            Assert.AreEqual(0, MetaRewards.SelectShards, "선택 기억편이 아니라 계보 기억편");

            Assert.IsEmpty(MetaRewards.Grant(new[] { MetaReward.Lineage("nope") }), "없는 계보는 건너뛴다");
        }

        /// <summary>친구 선택권 (PRG-04): 지갑에 쌓이고 쓰기 · 되돌리기, 이름 '친구 선택권' (RM-2 P4-1)</summary>
        [Test]
        public void FriendSelectTicket_GoesToWallet()
        {
            int walletChanged = 0;
            MetaRewards.WalletChanged += () => walletChanged++;

            var granted = MetaRewards.Grant(new[] { MetaRewardService.FriendTicket(2) });

            Assert.AreEqual(2, MetaRewards.FriendSelectTickets);
            Assert.AreEqual(2, Data.meta.wallet.friendSelectTickets);
            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual(MetaRewardType.FriendSelectTicket, granted[0].type);
            Assert.AreEqual("Legendary", granted[0].itemId, "고를 수 있는 최고 등급");
            Assert.AreEqual("친구 선택권 ×2", MetaRewards.Describe(granted[0]));
            Assert.AreEqual(1, walletChanged);

            Assert.IsTrue(MetaRewards.TrySpendFriendTicket());
            Assert.AreEqual(1, MetaRewards.FriendSelectTickets);
            MetaRewards.RefundFriendTicket();
            Assert.AreEqual(2, MetaRewards.FriendSelectTickets);
            Assert.IsTrue(MetaRewards.TrySpendFriendTicket());
            Assert.IsTrue(MetaRewards.TrySpendFriendTicket());
            Assert.IsFalse(MetaRewards.TrySpendFriendTicket(), "없으면 false");
            Assert.IsEmpty(MetaRewards.Grant(new[] { MetaRewardService.FriendTicket(0) }));
        }

        [Test]
        public void Costume_Unlocks()
        {
            Characters.Grant("sock");
            var granted = MetaRewards.Grant(new[] { MetaReward.Costume("sock_event") });

            Assert.IsTrue(Costumes.IsUnlocked("sock_event"));
            Assert.AreEqual(MetaRewardType.Costume, granted[0].type);
        }

        [Test]
        public void Multiplier_AppliesToCurrencyItemsGoldMinutes_NotTicketsOrExcludedGem()
        {
            double income = Growth.IncomePerSecond;
            var rewards = new List<MetaReward>
            {
                MetaReward.Gold(100), MetaReward.Gem(30), MetaReward.Item("m_cloth", 3), MetaReward.GoldMinutes(1),
                MetaReward.QuickIdleTicket(1), MetaReward.SelectShards(5),
            };

            var granted = MetaRewards.Grant(rewards, multiplier: 2, excludeGemFromMultiplier: true);

            // 고정 단추 100 × 2 = B 200 → 단추 100 · 실타래 100, N분치도 B에 배수를 곱한 뒤 나눈다 (안 D)
            double minutesShare = Math.Floor(income * 60 * 2 * 0.5);
            Assert.AreEqual(100 + minutesShare, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gold));
            Assert.AreEqual(100 + minutesShare, AmountOf(granted, MetaRewardType.Currency, CurrencyType.LineageXp));
            Assert.AreEqual(30, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem), "별조각 제외");
            Assert.AreEqual(6, AmountOf(granted, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.AreEqual(1, MetaRewards.QuickIdleTickets, "수선권은 항상 1배");
            Assert.AreEqual(5, MetaRewards.SelectShards);

            var withGem = MetaRewards.Grant(new[] { MetaReward.Gem(10) }, multiplier: 2);
            Assert.AreEqual(20, AmountOf(withGem, MetaRewardType.Currency, CurrencyType.Gem), "제외 옵션이 없으면 별조각도 두 배");
        }

        [Test]
        public void EmptyOrNull_ReturnsEmpty_AndGrantedEventOnlyWhenSomething()
        {
            int events = 0;
            MetaRewards.Granted += _ => events++;

            Assert.IsEmpty(MetaRewards.Grant(null));
            Assert.IsEmpty(MetaRewards.Grant(new List<MetaReward>()));
            Assert.IsEmpty(MetaRewards.Grant(new MetaReward[] { null }));
            Assert.AreEqual(0, events);

            MetaRewards.Grant(new[] { MetaReward.Gem(1) });
            Assert.AreEqual(1, events);
        }

        [Test]
        public void Describe_AndNames()
        {
            Characters.Grant("sock");
            Assert.AreEqual("별조각 300", MetaRewards.Describe(MetaReward.Gem(300)));
            Assert.AreEqual("단추 1,200", MetaRewards.Describe(MetaReward.Gold(1200)), "재화는 100만 미만 콤마 (M30 FormatCurrency)");
            Assert.AreEqual("단추 1.50B", MetaRewards.Describe(MetaReward.Gold(1.5e6)));
            Assert.AreEqual("수선 천 ×5", MetaRewards.Describe(MetaReward.Item("m_cloth", 5)), "개수는 곱하기 기호 U+00D7");
            Assert.AreEqual("단추 · 실타래 5분치", MetaRewards.Describe(MetaReward.GoldMinutes(5)), "N분치는 둘로 나뉘어 들어온다 (안 D)");
            Assert.AreEqual("단추 · 실타래 2시간치", MetaRewards.Describe(MetaReward.GoldMinutes(120)));
            Assert.AreEqual("실타래 300", MetaRewards.Describe(MetaReward.Currency(CurrencyType.LineageXp, 300)));
            Assert.AreEqual("빠른 수선권 ×1", MetaRewards.Describe(MetaReward.QuickIdleTicket()));
            Assert.AreEqual("두 배 수선권 ×2", MetaRewards.Describe(MetaReward.DoubleTicket(2)));
            Assert.AreEqual("선택 기억편 ×20", MetaRewards.Describe(MetaReward.SelectShards(20)));
            Assert.AreEqual("촛불", MetaRewards.Describe(MetaReward.Lineage("candle")));
            Assert.AreEqual("축제 양말", MetaRewards.Describe(MetaReward.Costume("sock_event")));
            Assert.AreEqual("별조각 300 · 수선 천 ×5",
                MetaRewards.Describe(new[] { MetaReward.Gem(300), MetaReward.Item("m_cloth", 5) }, " · "));
            Assert.AreEqual("수선 천 ×5", Rewards.Describe(RewardData.Item("m_cloth", 5)), "RewardService도 같은 표기");
            Assert.AreEqual("단추 1,200", Rewards.Describe(RewardData.Currency(CurrencyType.Gold, 1200)));
            Assert.AreEqual("기억의 실", MetaRewards.NameOf(MetaReward.Item("m_thread", 1)));
            Assert.AreEqual("단추 · 실타래", MetaRewards.NameOf(MetaReward.GoldMinutes(5)), "N분치는 둘로 나뉘어 들어온다 (안 D)");
        }

        [Test]
        public void Merge_CombinesSameKind_KeepsFriendsSeparate()
        {
            var merged = MetaRewardService.Merge(new[]
            {
                MetaReward.Gem(10), MetaReward.Gold(5), MetaReward.Gem(15), MetaReward.Item("m_cloth", 1), MetaReward.Item("m_cloth", 2),
                MetaReward.Item("m_thread", 1), MetaReward.Lineage("candle"), MetaReward.Lineage("candle"), null,
            });

            Assert.AreEqual(6, merged.Count);
            Assert.AreEqual(25, AmountOf(merged, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.AreEqual(3, AmountOf(merged, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.AreEqual(2, merged.FindAll(r => r.type == MetaRewardType.Lineage).Count);

            var source = MetaReward.Gem(1);
            var copy = MetaRewardService.Merge(new[] { source });
            Assert.AreNotSame(source, copy[0], "새 목록 (원본을 바꾸지 않음)");
        }

        [Test]
        public void ResolveAll_FillsPreviewWithoutGranting()
        {
            var into = new List<MetaReward> { MetaReward.Gem(999) };
            MetaRewards.ResolveAll(new[] { MetaReward.GoldMinutes(10), MetaReward.Gem(5) }, into);

            Assert.AreEqual(3, into.Count, "N분치는 단추 · 실타래 두 줄로 풀린다");
            Assert.AreEqual(MetaRewardType.Currency, into[0].type);
            Assert.AreEqual(CurrencyType.Gold, into[0].currency);
            Assert.AreEqual(CurrencyType.LineageXp, into[1].currency);
            Assert.AreEqual(0, Gold, "지급하지 않음");
            Assert.AreEqual(0, Currency.Get(CurrencyType.LineageXp));
            Assert.AreEqual(0, Gem);
        }
    }
}
