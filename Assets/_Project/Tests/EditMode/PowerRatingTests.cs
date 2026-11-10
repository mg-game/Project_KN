using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 전투력 평가 고치기 (SPEC5 §4.3 S5-24, 로드맵 delta 2 — R2P-C에서 옮겨 옴):
    /// 수선점 몫 상한 TeamPower = 공격 + min(수선점 내구도 × shopHpWeight, 공격 × shopShareCap),
    /// 권장 전투력 세 구간 (recommendedTaperStage 30 · recommendedTaperStage2 151에서 이어진다).
    /// </summary>
    public class PowerRatingTests : MetaSystemsTestBase
    {
        // TestContent 형태 기본값: 공격 10, 간격 1, 체력 100 → 200
        const long BaseUnit = 200;

        static long ShopPart(int level, float weight) => (long)Math.Round(GrowthSystem.CalcShopMaxHp(level) * (double)weight);

        void PlaceTwoGuardians()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");
        }

        [Test]
        public void ShopShareCap_DefaultsToOne()
        {
            Assert.AreEqual(1f, new PowerConfig().shopShareCap, "SPEC5 §2.3 기본 1 (콘텐츠가 비워도 상한이 켜진다)");
            Assert.AreEqual(1f, MetaDb.power.shopShareCap);
        }

        [Test]
        public void ShopShareCap_LimitsShopTermToOffense()
        {
            PlaceTwoGuardians();
            long offense = 2 * BaseUnit;
            Assert.AreEqual(offense + ShopPart(1, 0.3f), Power.TeamPower, "상한 아래면 옛 식 그대로 (400 + 90)");

            MetaDb.power.shopHpWeight = 10f; // 수선점 몫 3,000 > 공격 400
            Power.MarkDirty();
            Assert.AreEqual(2 * offense, Power.TeamPower, "수선점 몫은 공격 몫까지 = 전체의 절반까지");

            MetaDb.power.shopShareCap = 0.5f;
            Power.MarkDirty();
            Assert.AreEqual(offense + offense / 2, Power.TeamPower, "상한 배율을 따른다");
        }

        [Test]
        public void ShopShareCap_ZeroOrNegative_IsUncapped()
        {
            PlaceTwoGuardians();
            MetaDb.power.shopHpWeight = 10f;
            MetaDb.power.shopShareCap = 0f;
            Power.MarkDirty();
            Assert.AreEqual(2 * BaseUnit + ShopPart(1, 10f), Power.TeamPower, "0 = 상한 없음 (옛 식)");

            MetaDb.power.shopShareCap = -1f;
            Power.MarkDirty();
            Assert.AreEqual(2 * BaseUnit + ShopPart(1, 10f), Power.TeamPower, "음수도 상한 없음");
        }

        [Test]
        public void HighShopLevel_NoLongerDominatesTheRating()
        {
            PlaceTwoGuardians();
            Data.level = 401;
            Power.MarkDirty();
            long capped = Power.TeamPower;
            Assert.AreEqual(4 * BaseUnit, capped, "수선점 Lv 401: 내구도 몫이 공격 몫에서 멈춘다");

            MetaDb.power.shopShareCap = 0f;
            Power.MarkDirty();
            Assert.Greater(Power.TeamPower, 100 * capped, "상한이 없으면 수선점 내구도가 전투력을 삼킨다 (S5-24의 문제)");
        }

        [Test]
        public void ThreeSegments_ContinuousAt30And151()
        {
            var power = MetaDb.power;
            power.recommendedBase = 532;
            power.recommendedGrowth = 1.0741;
            power.recommendedTaperStage = 30;
            power.recommendedGrowthLate = 1.05;
            power.recommendedTaperStage2 = 151;
            power.recommendedGrowthLate2 = 1.015;

            double at30 = 532 * Math.Pow(1.0741, 29);
            double at151 = at30 * Math.Pow(1.05, 121);
            AssertRec(at30, 30, "30 = base × g^29");
            AssertRec(at30 * 1.05, 31, "30 → 31 첫 걸음 gL");
            AssertRec(at151, 151, "151까지는 두 구간 곡선 그대로");
            AssertRec(at151 * 1.015, 152, "151 → 152 첫 걸음 gL2");
            AssertRec(at151 * Math.Pow(1.015, 249), 400);

            // 이어지는지: 경계 양쪽 한 걸음의 비율
            Assert.AreEqual(1.0741, Ratio(29, 30), 1e-3);
            Assert.AreEqual(1.05, Ratio(30, 31), 1e-3);
            Assert.AreEqual(1.05, Ratio(150, 151), 1e-3);
            Assert.AreEqual(1.015, Ratio(151, 152), 1e-3);
        }

        [Test]
        public void ThirdSegment_Off_ReproducesTwoSegments()
        {
            var power = MetaDb.power;
            power.recommendedBase = 400;
            power.recommendedGrowth = 1.08;
            power.recommendedTaperStage = 30;
            power.recommendedGrowthLate = 1.04;
            long[] twoSegment = { Power.GetRecommendedPower(1), Power.GetRecommendedPower(30), Power.GetRecommendedPower(151), Power.GetRecommendedPower(300) };

            power.recommendedTaperStage2 = 0;
            power.recommendedGrowthLate2 = 1.01;
            Assert.AreEqual(twoSegment[3], Power.GetRecommendedPower(300), "T2 = 0 → 두 구간 그대로");

            power.recommendedTaperStage2 = 151;
            power.recommendedGrowthLate2 = 0;
            Assert.AreEqual(twoSegment[0], Power.GetRecommendedPower(1));
            Assert.AreEqual(twoSegment[1], Power.GetRecommendedPower(30));
            Assert.AreEqual(twoSegment[2], Power.GetRecommendedPower(151));
            Assert.AreEqual(twoSegment[3], Power.GetRecommendedPower(300), "gL2가 비면(0) gL 그대로");
        }

        [Test]
        public void SecondTaperBeforeFirst_UsesLate2FromTheFirst()
        {
            var power = MetaDb.power;
            power.recommendedBase = 100;
            power.recommendedGrowth = 1.2;
            power.recommendedTaperStage = 30;
            power.recommendedGrowthLate = 1.1;
            power.recommendedTaperStage2 = 10;
            power.recommendedGrowthLate2 = 1.01;

            AssertRec(100 * Math.Pow(1.2, 29), 30, "앞 구간은 T까지");
            AssertRec(100 * Math.Pow(1.2, 29) * Math.Pow(1.01, 10), 40, "T2 ≤ T면 T부터 바로 gL2 (BattleConfig.TaperedScale과 같다)");
        }

        [Test]
        public void SegmentsOff_IsTodaysSingleExponent()
        {
            var power = MetaDb.power;
            power.recommendedBase = 300;
            power.recommendedGrowth = 1.12;
            power.recommendedTaperStage2 = 151;
            power.recommendedGrowthLate2 = 1.01;
            for (int stage = 1; stage <= 200; stage += 13)
                AssertRec(300 * Math.Pow(1.12, stage - 1), stage, $"T = 0 (첫 구간 꺼짐) → T2도 무시, 스테이지 {stage}");
        }

        void AssertRec(double expected, int stage, string message = null)
        {
            long actual = Power.GetRecommendedPower(stage);
            Assert.AreEqual(expected, actual, Math.Max(1.0, expected * 1e-9), message);
        }

        double Ratio(int from, int to) => (double)Power.GetRecommendedPower(to) / Power.GetRecommendedPower(from);
    }
}
