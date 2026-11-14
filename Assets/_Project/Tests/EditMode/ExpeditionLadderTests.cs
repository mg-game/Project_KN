using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    /// <summary>
    /// 다녀올 시간 사다리 · 모두 다시 보내기 (IDLE-04, SPEC4 §5.3): 단계 시간 ×1/×3/×6, 12시간 상한, 튜토리얼 뒤에만, 보상 배율 굴림
    /// (평균 3% 안 · 정해진 난수면 같은 결과), 마지막 단계 저장, 호감도 2/4/6, 주말 재료 배율, ResendAll의 동시 원정 상한 · 못 보내는 구역 건너뛰기 ·
    /// ExpeditionBatch 잠김이면 시스템이 거절 (F-03).
    /// </summary>
    public class ExpeditionLadderTests : SystemsTestBase
    {
        ExpeditionDatabase _customDb;

        [TearDown]
        public void DestroyCustomDb()
        {
            if (_customDb != null) Object.DestroyImmediate(_customDb);
            _customDb = null;
        }

        void FinishTutorial() => Data.meta.ftue.done = true;

        /// <summary>재료 하나(확률 1, 1개)만 주는 10분 구역 하나로 따로 만든 원정 시스템 (정해진 난수)</summary>
        ExpeditionSystem CreateSingleDropSystem(int seed, int min = 1, int max = 1, float chance = 1f)
        {
            _customDb = ScriptableObject.CreateInstance<ExpeditionDatabase>();
            _customDb.zones = new List<ExpeditionZoneDef>
            {
                new ExpeditionZoneDef
                {
                    id = "exp_test", name = "시험", requiredStage = 1, durationMinutes = 10, maxMembers = 3, goldReward = 100,
                    drops = new List<ExpeditionDrop> { new ExpeditionDrop { itemId = "m_cloth", min = min, max = max, chance = chance } },
                },
            };
            return new ExpeditionSystem(Data, _customDb, Characters, Formation, Rewards, Clock, new System.Random(seed));
        }

        static int ItemsOf(ExpeditionResult result, string itemId)
        {
            int count = 0;
            foreach (var reward in result.Rewards)
            {
                if (reward.type == RewardType.Item && reward.itemId == itemId) count += (int)reward.amount;
            }
            return count;
        }

        [Test]
        public void Durations_Scale_Affinity_PerTier()
        {
            var zone = ExpeditionDb.Get("exp_1_1");
            Assert.AreEqual(10, Expedition.GetDurationMinutes(zone, 0));
            Assert.AreEqual(30, Expedition.GetDurationMinutes(zone, 1));
            Assert.AreEqual(60, Expedition.GetDurationMinutes(zone, 2));
            Assert.AreEqual(1.0, ExpeditionSystem.RewardScale(0));
            Assert.AreEqual(2.8, ExpeditionSystem.RewardScale(1));
            Assert.AreEqual(5.4, ExpeditionSystem.RewardScale(2));
            Assert.AreEqual(2, ExpeditionSystem.GetAffinityPerMember(0));
            Assert.AreEqual(4, ExpeditionSystem.GetAffinityPerMember(1));
            Assert.AreEqual(6, ExpeditionSystem.GetAffinityPerMember(2));
            Assert.AreEqual(0, Expedition.GetDurationMinutes(null, 1));
        }

        [Test]
        public void TierAllowed_OnlyAfterTutorial_AndUpToTwelveHours()
        {
            var shortZone = ExpeditionDb.Get("exp_1_1");   // 10분
            var longZone = ExpeditionDb.Get("exp_2_4");    // 180분
            var twoHours = ExpeditionDb.Get("exp_2_3");    // 120분 → 밤새 720분
            Assert.IsTrue(Expedition.IsTierAllowed(shortZone, 0));
            Assert.IsFalse(Expedition.IsTierAllowed(shortZone, 1), "튜토리얼 중에는 0단계만");
            Assert.IsFalse(Expedition.IsLadderOpen);

            FinishTutorial();
            Assert.IsTrue(Expedition.IsLadderOpen);
            Assert.IsTrue(Expedition.IsTierAllowed(shortZone, 1));
            Assert.IsTrue(Expedition.IsTierAllowed(shortZone, 2));
            Assert.IsTrue(Expedition.IsTierAllowed(longZone, 1), "540분");
            Assert.IsFalse(Expedition.IsTierAllowed(longZone, 2), "1080분 > 720분 → exp_2_4는 0 · 1단계만");
            Assert.IsTrue(Expedition.IsTierAllowed(twoHours, 2), "720분은 된다");
            Assert.IsFalse(Expedition.IsTierAllowed(shortZone, 3));
            Assert.IsFalse(Expedition.IsTierAllowed(shortZone, -1));
            Assert.IsFalse(Expedition.IsTierAllowed(null, 0));

            Characters.Grant("sock");
            Data.battle.highestStage = 20;
            Assert.IsFalse(Expedition.TryStart("exp_2_4", new[] { "sock" }, 2));
            Assert.IsNull(Expedition.GetActive("exp_2_4"));
        }

        [Test]
        public void TryStart_Tier_SetsDuration_AndRemembersTier()
        {
            FinishTutorial();
            Characters.Grant("sock");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }, 2));
            var active = Expedition.GetActive("exp_1_1");
            Assert.AreEqual(3600, active.endUnix - active.startUnix, "밤새 = 10분 × 6");
            Assert.AreEqual(2, active.durationTier);
            Assert.AreEqual(2, Expedition.GetLastTier("exp_1_1"));
            Assert.AreEqual(2, Data.expeditionLastMembers.Find(m => m.zoneId == "exp_1_1").lastTier);

            Expedition.TryCancel("exp_1_1");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }), "튜토리얼 · 예전 호출은 0단계");
            active = Expedition.GetActive("exp_1_1");
            Assert.AreEqual(600, active.endUnix - active.startUnix);
            Assert.AreEqual(0, active.durationTier);
            Assert.AreEqual(0, Expedition.GetLastTier("exp_1_1"));
        }

        [Test]
        public void Claim_Tier_ScalesGold_AffinityPerTier_EvolutionCountOnce()
        {
            FinishTutorial();
            Characters.Grant("sock");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }, 1));
            Clock.Advance(TimeSpan.FromMinutes(29));
            Assert.IsNull(Expedition.Claim("exp_1_1"), "길게 = 30분");
            Clock.Advance(TimeSpan.FromMinutes(1));
            var result = Expedition.Claim("exp_1_1");

            Assert.IsNotNull(result);
            Assert.AreEqual(420, Currency.Get(CurrencyType.Gold), 1e-6, "기준 300 × 2.8 = 840 → 단추 몫 절반 (안 D)");
            Assert.AreEqual(420, Currency.Get(CurrencyType.LineageXp), 1e-6, "실타래 몫 절반");
            Assert.AreEqual(4, Characters.GetAffinity("sock"), "길게 = 호감도 4");
            Assert.AreEqual(1, Characters.GetProgress("sock", ProgressType.ExpeditionCount), "진화 기록은 단계와 무관하게 +1");
        }

        [Test]
        public void ScaleRolls_MeanWithinThreePercent_AndDeterministic()
        {
            FinishTutorial();
            Characters.Grant("sock");
            foreach (int tier in new[] { 1, 2 })
            {
                double scale = ExpeditionSystem.RewardScale(tier);
                var first = CreateSingleDropSystem(seed: 77);
                long total = 0;
                var counts = new List<int>();
                const int runs = 2000;
                for (int i = 0; i < runs; i++)
                {
                    Assert.IsTrue(first.TryStart("exp_test", new[] { "sock" }, tier));
                    Clock.Advance(TimeSpan.FromMinutes(first.GetDurationMinutes(_customDb.Get("exp_test"), tier)));
                    var result = first.Claim("exp_test");
                    int count = ItemsOf(result, "m_cloth");
                    Assert.That(count, Is.EqualTo((int)Math.Floor(scale)).Or.EqualTo((int)Math.Floor(scale) + 1));
                    counts.Add(count);
                    total += count;
                }
                double mean = (double)total / runs;
                Assert.AreEqual(scale, mean, scale * 0.03, $"단계 {tier}: 평균 {mean:0.###} (배율 {scale})");

                Object.DestroyImmediate(_customDb);
                var second = CreateSingleDropSystem(seed: 77);
                for (int i = 0; i < 50; i++)
                {
                    second.TryStart("exp_test", new[] { "sock" }, tier);
                    Clock.Advance(TimeSpan.FromMinutes(second.GetDurationMinutes(_customDb.Get("exp_test"), tier)));
                    Assert.AreEqual(counts[i], ItemsOf(second.Claim("exp_test"), "m_cloth"), "같은 시드 = 같은 굴림");
                }
                Object.DestroyImmediate(_customDb);
                _customDb = null;
            }
        }

        [Test]
        public void TierZero_RollsOncePerDrop_LikeBeforeTheLadder()
        {
            Characters.Grant("sock");
            var system = CreateSingleDropSystem(seed: 5, min: 2, max: 2);
            Assert.IsTrue(system.TryStart("exp_test", new[] { "sock" }));
            Clock.Advance(TimeSpan.FromMinutes(10));
            var result = system.Claim("exp_test");
            Assert.AreEqual(2, ItemsOf(result, "m_cloth"));
            Assert.AreEqual(50, Currency.Get(CurrencyType.Gold), 1e-6, "기준 100의 단추 몫 (안 D)");
            Assert.AreEqual(50, Currency.Get(CurrencyType.LineageXp), 1e-6, "기준 100의 실타래 몫");
        }

        [Test]
        public void DropMultiplier_DoublesMaterials_NotGold()
        {
            Characters.Grant("sock");
            var system = CreateSingleDropSystem(seed: 5, min: 2, max: 2);
            system.DropMultiplier = () => 2.0;
            Assert.IsTrue(system.TryStart("exp_test", new[] { "sock" }));
            Clock.Advance(TimeSpan.FromMinutes(10));
            var result = system.Claim("exp_test");
            Assert.AreEqual(4, ItemsOf(result, "m_cloth"), "주말 원정 축제 = 재료 ×2 (받을 때)");
            Assert.AreEqual(50, Currency.Get(CurrencyType.Gold), 1e-6, "단추는 그대로 (기준 100의 단추 몫)");

            system.DropMultiplier = () => 0.5;
            Assert.IsTrue(system.TryStart("exp_test", new[] { "sock" }));
            Clock.Advance(TimeSpan.FromMinutes(10));
            Assert.AreEqual(2, ItemsOf(system.Claim("exp_test"), "m_cloth"), "1보다 작은 배율은 무시");
        }

        [Test]
        public void ResendAll_UsesLastTier_RespectsMaxConcurrent_AndSkipsIneligible()
        {
            FinishTutorial();
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Data.battle.highestStage = 20;

            Assert.AreEqual(0, Expedition.CountResendable(), "기억한 친구가 없으면 0");
            Assert.AreEqual(0, Expedition.ResendAll());

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }, 1));
            Assert.IsTrue(Expedition.TryStart("exp_1_2", new[] { "candle" }, 0));
            Assert.IsTrue(Expedition.TryStart("exp_1_3", new[] { "button" }, 2));
            Clock.Advance(TimeSpan.FromHours(3));
            Assert.AreEqual(3, Expedition.ClaimAll().Count);
            Assert.IsTrue(Expedition.TryStart("exp_1_4", new[] { "sock" }));
            Clock.Advance(TimeSpan.FromMinutes(45));
            Assert.IsNotNull(Expedition.Claim("exp_1_4"));

            Assert.AreEqual(3, Expedition.CountResendable(), "exp_1_4는 exp_1_1과 같은 친구(양말)라 앞 구역만 센다");
            int changed = 0;
            Expedition.Changed += () => changed++;
            Assert.AreEqual(3, Expedition.ResendAll());
            Assert.AreEqual(3, changed);
            Assert.AreEqual(3, Expedition.Active.Count, "동시 원정 상한 3");
            var a = Expedition.GetActive("exp_1_1");
            var c = Expedition.GetActive("exp_1_3");
            Assert.AreEqual(1, a.durationTier, "마지막 단계 그대로");
            Assert.AreEqual(30 * 60, a.endUnix - a.startUnix);
            Assert.AreEqual(2, c.durationTier);
            Assert.AreEqual(180 * 60, c.endUnix - c.startUnix);
            Assert.AreEqual(0, Expedition.GetActive("exp_1_2").durationTier);
            Assert.IsNull(Expedition.GetActive("exp_1_4"));
            Assert.AreEqual(0, Expedition.CountResendable(), "자리가 없음");

            foreach (var zone in new[] { "exp_1_1", "exp_1_2", "exp_1_3" }) Expedition.TryCancel(zone);
            Assert.IsTrue(Expedition.TryStart("exp_2_1", new[] { "candle" }));
            Assert.AreEqual(2, Expedition.CountResendable());
            Assert.AreEqual(2, Expedition.ResendAll(), "촛불이 다른 원정에 나가 있는 구역은 건너뛴다");
            Assert.IsNotNull(Expedition.GetActive("exp_1_1"));
            Assert.IsNull(Expedition.GetActive("exp_1_2"));
            Assert.IsNotNull(Expedition.GetActive("exp_1_3"));
        }

        [Test]
        public void Resend_LastTierNoLongerAllowed_FallsBackToTierZero()
        {
            FinishTutorial();
            Characters.Grant("sock");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }, 2));
            Clock.Advance(TimeSpan.FromHours(1));
            Expedition.Claim("exp_1_1");

            Data.meta.ftue.done = false; // 사다리가 닫힌 상태 (시험용)
            Assert.IsTrue(Expedition.TryResend("exp_1_1"));
            var active = Expedition.GetActive("exp_1_1");
            Assert.AreEqual(0, active.durationTier);
            Assert.AreEqual(600, active.endUnix - active.startUnix);
        }

        [Test]
        public void ResendAll_RefusedWhileExpeditionBatchLocked_SingleResendStaysOpen()
        {
            FinishTutorial();
            Characters.Grant("sock");
            Characters.Grant("candle");
            Data.battle.highestStage = 5;
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }, 1));
            Assert.IsTrue(Expedition.TryStart("exp_1_2", new[] { "candle" }, 0));
            Clock.Advance(TimeSpan.FromHours(1));
            Assert.AreEqual(2, Expedition.ClaimAll().Count);

            Assert.IsNull(Expedition.FeatureResolver, "MetaSession이 넣기 전 = 열림");
            Assert.IsTrue(Expedition.IsBatchOpen);
            Assert.AreEqual(2, Expedition.CountResendable());

            var asked = new List<FeatureId>();
            Expedition.FeatureResolver = f =>
            {
                asked.Add(f);
                return f != FeatureId.ExpeditionBatch;
            };
            Assert.IsFalse(Expedition.IsBatchOpen);
            Assert.AreEqual(0, Expedition.CountResendable(), "2-1 전 (ExpeditionBatch 잠김) = 버튼 없음");
            int changed = 0;
            Expedition.Changed += () => changed++;
            Assert.AreEqual(0, Expedition.ResendAll(), "화면 길이 새어도 시스템이 거절 (F-03)");
            Assert.AreEqual(0, changed);
            Assert.IsEmpty(Expedition.Active);
            CollectionAssert.Contains(asked, FeatureId.ExpeditionBatch);

            Assert.IsTrue(Expedition.TryResend("exp_1_1"), "구역 하나 다시 보내기는 잠금과 무관");
            Assert.AreEqual(1, Expedition.GetActive("exp_1_1").durationTier);

            Expedition.FeatureResolver = f => true;
            Assert.AreEqual(1, Expedition.CountResendable());
            Assert.AreEqual(1, Expedition.ResendAll());
            Assert.IsNotNull(Expedition.GetActive("exp_1_2"));
        }
    }
}
