using System;
using System.Collections.Generic;

namespace IdleGame.Tests
{
    /// <summary>
    /// 메타 시스템 테스트 공용 도우미 (M1). SystemsTestBase가 만든 시스템 위에서
    /// MetaDb 내용을 채우고(Rebuild로 메타 시스템만 다시 만듦) 시계를 UTC로 맞춰 쓴다.
    /// 일일 시계는 UTC 시간대라 "현지 05:00" = UTC 05:00이다.
    /// 주의: NUnit(Unity Test Runner)은 픽스처 인스턴스 하나로 그 클래스의 모든 테스트를 돌린다. 이벤트를 받아 적는
    /// 필드(readonly List 등)는 필드 초기화로는 한 번만 만들어지므로 파생 클래스의 [SetUp]에서 비워야 한다.
    /// </summary>
    public abstract class MetaSystemsTestBase : SystemsTestBase
    {
        /// <summary>MetaDb 조회 캐시를 비우고 메타 시스템을 다시 만든다 (Data는 그대로)</summary>
        protected void Rebuild()
        {
            MetaDb.ClearCache();
            CreateMetaSystems();
        }

        /// <summary>시계를 UTC 시각으로 맞춘다</summary>
        protected void SetUtc(int year, int month, int day, int hour, int minute = 0, int second = 0) =>
            Clock.UtcNow = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

        protected void Advance(double hours) => Clock.Advance(TimeSpan.FromHours(hours));

        protected void AdvanceSeconds(double seconds) => Clock.Advance(TimeSpan.FromSeconds(seconds));

        /// <summary>기능 정의를 넣는다 (조건 중 하나라도 충족하면 열림)</summary>
        protected FeatureUnlockDef AddFeature(FeatureId feature, params MetaCondition[] conditions)
        {
            var def = new FeatureUnlockDef
            {
                feature = feature,
                name = feature.ToString(),
                conditions = new List<MetaCondition>(conditions),
                cardText = feature + " 열림",
            };
            MetaDb.features.Add(def);
            MetaDb.ClearCache();
            return def;
        }

        /// <summary>최고 스테이지(도달)를 정한다. 스테이지 n 클리어 = highestStage n + 1</summary>
        protected void SetCleared(int clearedStage) => Data.battle.highestStage = Math.Max(1, clearedStage + 1);

        protected double Gold => Currency.Get(CurrencyType.Gold);

        protected double Gem => Currency.Get(CurrencyType.Gem);

        protected int ItemCount(string itemId) => Inventory.GetCount(itemId);

        protected static MetaReward Find(List<MetaReward> rewards, MetaRewardType type, CurrencyType currency = CurrencyType.Gold, string itemId = null) =>
            rewards.Find(r => r.type == type && (type != MetaRewardType.Currency || r.currency == currency) && (itemId == null || r.itemId == itemId));

        protected static double AmountOf(List<MetaReward> rewards, MetaRewardType type, CurrencyType currency = CurrencyType.Gold, string itemId = null) =>
            Find(rewards, type, currency, itemId)?.amount ?? 0;
    }
}
