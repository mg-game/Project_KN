using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 별빛 산책 수첩 (Q-06, SPEC4 §8.4 — 확장 항목, 칸이 비면 꺼짐): 경험치는 일일 미션 점수(JournalPoints)만, 90마다 한 칸,
    /// 시즌 넘김 때 남은 칸 자동 받기 + 새 시즌 (오래 비우면 지나간 시즌 수만큼), 의상을 가졌거나 표에 없으면 fallback, 잠기면 경험치 없음, 되돌림 막음.
    /// </summary>
    public class SeasonPassTests : MetaSystemsTestBase
    {
        List<MetaReward> _rolled;

        [SetUp]
        public void ResetRolled() => _rolled = null;

        static SeasonTierDef Tier(int tier, List<MetaReward> rewards, List<MetaReward> fallback = null) =>
            new SeasonTierDef { tier = tier, rewards = rewards, fallback = fallback ?? new List<MetaReward>() };

        void Setup(string costumeId = "sock_event", bool lockEvents = false)
        {
            if (lockEvents) AddFeature(FeatureId.Events, MetaCondition.StageCleared(3, 1));
            MetaDb.season = new SeasonConfig
            {
                days = 28,
                xpPerTier = 90,
                tiers = new List<SeasonTierDef>
                {
                    Tier(1, new List<MetaReward> { MetaReward.Gem(30) }),
                    Tier(2, new List<MetaReward> { MetaReward.Item("m_cloth", 10) }),
                    Tier(3, new List<MetaReward> { MetaReward.QuickIdleTicket(1) }),
                    Tier(30, new List<MetaReward> { MetaReward.Costume(costumeId) },
                        new List<MetaReward> { MetaReward.Gem(50), MetaReward.SelectShards(30) }),
                },
            };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            Season.RolledOver += granted => _rolled = granted;
            MetaSession.Start();
        }

        void AddXp(long points) => Progress.Increment(CounterKey.JournalPoints, points);

        void Days(int days)
        {
            Advance(24 * days);
            DailyClock.Refresh();
        }

        [Test]
        public void Xp_OnlyFromJournalPoints_TierMath()
        {
            Setup();
            Assert.IsTrue(Season.IsUnlocked);
            Assert.AreEqual(DailyClock.DayKey, Data.meta.season.startDayKey, "열린 것을 본 날에 시작");
            Assert.AreEqual(30, Season.TierCount);
            Progress.Increment(CounterKey.MissionClaim, 5);
            Progress.Increment(CounterKey.StageClear, 50);
            Assert.AreEqual(0, Season.Xp, "다른 카운터는 경험치가 아니다");

            AddXp(100);
            Assert.AreEqual(100, Season.Xp);
            Assert.AreEqual(1, Season.Level);
            Assert.AreEqual(10, Season.XpIntoTier);
            AddXp(170);
            Assert.AreEqual(3, Season.Level, "270 / 90");
            AddXp(100000);
            Assert.AreEqual(30, Season.Level, "최대 30칸");
            Assert.AreEqual(TimeSpan.FromHours(27 * 24 + 20), Season.TimeLeft, "28개 날 키 뒤 05:00까지");
        }

        [Test]
        public void Claims_OnceEach_OnlyReached()
        {
            Setup();
            AddXp(270);
            Assert.IsTrue(Season.HasClaimable);
            var first = Season.TryClaim(1);
            Assert.AreEqual(30, AmountOf(first, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.IsNull(Season.TryClaim(1), "한 번만");
            Assert.IsNull(Season.TryClaim(4), "닿지 않음");
            Assert.IsNull(Season.TryClaim(0));
            var rest = Season.ClaimAll();
            Assert.AreEqual(10, AmountOf(rest, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.AreEqual(1, MetaRewards.QuickIdleTickets);
            Assert.IsFalse(Season.HasClaimable);
            Assert.AreEqual(3, Progress.GetLifetime(CounterKey.SeasonTierClaim));

            var tiers = new List<SeasonTierStatus>();
            Season.GetTiers(tiers);
            Assert.AreEqual(4, tiers.Count);
            Assert.IsTrue(tiers[0].Claimed);
            Assert.IsFalse(tiers[3].Reached);
        }

        [Test]
        public void Rollover_AutoGrantsRest_AndStartsNewSeason()
        {
            Setup();
            string start = Data.meta.season.startDayKey;
            AddXp(180);
            Season.TryClaim(1);
            Days(27);
            Assert.AreEqual(0, Season.SeasonIndex, "27일째 = 아직 같은 시즌");
            Days(1);
            Assert.AreEqual(1, Season.SeasonIndex);
            Assert.AreEqual(0, Season.Xp);
            Assert.AreEqual(DailyClock.AddDays(start, 28), Data.meta.season.startDayKey);
            Assert.IsNotNull(_rolled, "남은 선물 알림");
            Assert.AreEqual(10, AmountOf(_rolled, MetaRewardType.Item, itemId: "m_cloth"), "닿았지만 받지 않은 2칸");
            Assert.AreEqual(10, ItemCount("m_cloth"));
            Assert.IsEmpty(Data.meta.season.claimedTiers);

            _rolled = null;
            Days(60);
            Assert.AreEqual(3, Season.SeasonIndex, "지나간 시즌 수만큼");
            Assert.AreEqual(DailyClock.AddDays(start, 28 * 3), Data.meta.season.startDayKey, "28일 간격에 맞춘다");
            Assert.IsNull(_rolled, "받을 칸이 없으면 알리지 않는다");
        }

        [Test]
        public void TierThirty_Costume_FallbackWhenOwned()
        {
            Setup();
            AddXp(2700);
            Data.unlockedCostumes.Add("sock_event");
            var tiers = new List<SeasonTierStatus>();
            Season.GetTiers(tiers);
            var last = tiers.Find(t => t.Tier == 30);
            Assert.AreEqual(MetaRewardType.Currency, last.Rewards[0].type, "가진 의상이면 fallback을 보여 준다");
            double gem = Gem;
            var granted = Season.TryClaim(30);
            Assert.AreEqual(50, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.AreEqual(30, MetaRewards.SelectShards);
            Assert.AreEqual(gem + 50, Gem, 1e-6);
        }

        [Test]
        public void TierThirty_Costume_GrantedWhenNotOwned()
        {
            Setup();
            AddXp(2700);
            var granted = Season.TryClaim(30);
            Assert.IsNotNull(Find(granted, MetaRewardType.Costume));
            Assert.IsTrue(Data.unlockedCostumes.Contains("sock_event"));
            Assert.AreEqual(0, MetaRewards.SelectShards, "의상을 받으면 fallback 없음");
        }

        [Test]
        public void TierThirty_CostumeMissingFromTable_PaysFallback()
        {
            Setup(costumeId: "jjak_starwalk"); // 그림(A14)이 아직 없어 의상 표에 없음
            AddXp(2700);
            var granted = Season.TryClaim(30);
            Assert.AreEqual(50, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.AreEqual(30, MetaRewards.SelectShards);
        }

        [Test]
        public void Locked_NoXp_NoSeason()
        {
            Setup(lockEvents: true);
            Assert.IsFalse(Season.IsUnlocked);
            AddXp(500);
            Assert.AreEqual(0, Season.Xp);
            Assert.IsNull(Data.meta.season.startDayKey);
            Assert.IsNull(Season.TryClaim(1));
        }

        [Test]
        public void EmptyTiers_IsOff()
        {
            MetaSession.Start();
            Assert.IsFalse(Season.IsUnlocked);
            AddXp(500);
            Assert.AreEqual(0, Season.Xp);
            Assert.IsEmpty(Season.ClaimAll());
        }

        [Test]
        public void Tampered_BlocksClaims()
        {
            Setup();
            AddXp(100);
            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsNull(Season.TryClaim(1));
            Assert.IsFalse(Season.HasClaimable);
        }
    }
}
