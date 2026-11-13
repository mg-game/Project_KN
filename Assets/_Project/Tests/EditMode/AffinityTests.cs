using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 호감도 쓸모 (CR-10, SPEC4 §6.3): 단계 경계 0/50/150/…/7,000 · 단계 보상 10 × 단계 · 쓰다듬기 하루 3번 (05:00 초기화) ·
    /// 기억 조각 짝수 단계 · CountAtLevel · AffinityLevelCount 답하기 · 빨간 점.
    /// </summary>
    public class AffinityTests : MetaSystemsTestBase
    {
        void Own(string lineageId, int affinity)
        {
            Characters.Grant(lineageId);
            Data.lineages[lineageId].affinity = affinity;
        }

        [Test]
        public void Thresholds_AndLevelFromAffinity()
        {
            CollectionAssert.AreEqual(new long[] { 0, 50, 150, 350, 700, 1200, 2000, 3200, 4800, 7000 }, new AffinityConfig().thresholds);
            Assert.AreEqual(10, Affinity.MaxLevel);
            Assert.AreEqual(1, Affinity.LevelOf(0));
            Assert.AreEqual(1, Affinity.LevelOf(49));
            Assert.AreEqual(2, Affinity.LevelOf(50));
            Assert.AreEqual(5, Affinity.LevelOf(700));
            Assert.AreEqual(9, Affinity.LevelOf(6999));
            Assert.AreEqual(10, Affinity.LevelOf(7000));
            Assert.AreEqual(10, Affinity.LevelOf(99999));

            Assert.AreEqual(1, Affinity.GetLevel("sock"), "미보유 1");
            Own("sock", 160);
            Assert.AreEqual(3, Affinity.GetLevel("sock"));
            Assert.AreEqual(150, Affinity.GetLevelStart(3));
            Assert.AreEqual(350, Affinity.GetNextThreshold("sock"));
            Data.lineages["sock"].affinity = 9000;
            Assert.AreEqual(7000, Affinity.GetNextThreshold("sock"), "최고 단계면 그 단계 시작 값");
            Assert.AreEqual(0, Affinity.GetLevelStart(0));
        }

        [Test]
        public void Claim_Grants10TimesEachReachedLevel()
        {
            Own("sock", 400); // Lv.4
            Assert.IsTrue(Affinity.HasClaimable("sock"));
            Assert.AreEqual(1, Affinity.GetClaimedLevel("sock"));
            double before = Gem;

            var granted = Affinity.TryClaim("sock");

            Assert.AreEqual(20 + 30 + 40, Gem - before, "Lv.2 20 + Lv.3 30 + Lv.4 40");
            Assert.AreEqual(90, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.AreEqual(4, Affinity.GetClaimedLevel("sock"));
            Assert.IsFalse(Affinity.HasClaimable("sock"));
            Assert.IsNull(Affinity.TryClaim("sock"), "받을 것이 없으면 null");

            Data.lineages["sock"].affinity = 7000;
            before = Gem;
            Affinity.TryClaim("sock");
            Assert.AreEqual(50 + 60 + 70 + 80 + 90 + 100, Gem - before, "Lv.5~10");
            Assert.IsNull(Affinity.TryClaim("candle"), "미보유");
        }

        [Test]
        public void Pets_ThreePerDay_ResetAt0500()
        {
            Own("sock", 0);
            Data.meta.ftue.done = true;
            SetUtc(2026, 11, 5, 12, 0);
            Assert.AreEqual(3, Affinity.PetsLeftToday("sock"));

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(Affinity.TryPet("sock", out _, out bool gained));
                Assert.IsTrue(gained);
            }
            Assert.AreEqual(15, Characters.GetAffinity("sock"), "+5 × 3");
            Assert.AreEqual(0, Affinity.PetsLeftToday("sock"));
            Assert.IsTrue(Affinity.TryPet("sock", out _, out bool more), "몸짓은 그대로");
            Assert.IsFalse(more, "넷째부터 호감도 없음");
            Assert.AreEqual(15, Characters.GetAffinity("sock"));
            Assert.AreEqual(3, Progress.GetLifetime(CounterKey.AffinityPet), "호감도가 오른 것만 센다");

            SetUtc(2026, 11, 6, 4, 59);
            Assert.AreEqual(0, Affinity.PetsLeftToday("sock"), "05:00 전에는 같은 날");
            SetUtc(2026, 11, 6, 5, 0);
            Assert.AreEqual(3, Affinity.PetsLeftToday("sock"), "05:00에 새 날");
            Assert.IsTrue(Affinity.TryPet("sock", out _, out bool again));
            Assert.IsTrue(again);
        }

        [Test]
        public void Pet_RefusedBeforeFtueOrUnowned_AndUsesContentLines()
        {
            Own("sock", 0);
            Assert.IsFalse(Affinity.TryPet("sock", out _, out _), "튜토리얼 전에는 안 됨");
            Data.meta.ftue.done = true;
            Assert.IsFalse(Affinity.TryPet("candle", out _, out _), "미보유");

            MetaDb.affinity.lineages.Add(new AffinityLineDef { lineageId = "sock", pet = new[] { "간지러워요!", "", "또 해 줘요" } });
            Rebuild();
            Assert.IsTrue(Affinity.TryPet("sock", out string first, out _));
            Assert.IsTrue(Affinity.TryPet("sock", out string second, out _));
            Assert.AreEqual("간지러워요!", first);
            Assert.AreEqual("또 해 줘요", second, "빈 줄은 건너뛴다");
        }

        [Test]
        public void StoryLines_UnlockAtEvenLevels()
        {
            MetaDb.affinity.lineages.Add(new AffinityLineDef { lineageId = "sock", story = new[] { "하나", "둘", "셋", "넷", "다섯" } });
            Rebuild();
            Own("sock", 0);
            Assert.IsNull(Affinity.GetStoryLine("sock", 0), "Lv.1 잠김");

            Data.lineages["sock"].affinity = 50; // Lv.2
            Assert.AreEqual("하나", Affinity.GetStoryLine("sock", 0));
            Assert.IsNull(Affinity.GetStoryLine("sock", 1), "Lv.4에 열림");
            Data.lineages["sock"].affinity = 7000;
            Assert.AreEqual("다섯", Affinity.GetStoryLine("sock", 4));
            Assert.IsNull(Affinity.GetStoryLine("sock", 5));
            Assert.IsNull(Affinity.GetStoryLine("candle", 0), "미보유");
            Assert.AreEqual(10, AffinitySystem.StoryUnlockLevel(4));
        }

        [Test]
        public void CountAtLevel_AndAffinityLevelCountCondition()
        {
            Own("sock", 7000);
            Own("candle", 7500);
            Own("button", 700);
            Affinity.Connect();

            Assert.AreEqual(2, Affinity.CountAtLevel(10));
            Assert.AreEqual(3, Affinity.CountAtLevel(5));
            Assert.AreEqual(2, Progress.GetValue(MetaCondition.Of(MetaConditionType.AffinityLevelCount, 1, "10")), "ach_affinity: Lv.10 친구 수");
            Assert.AreEqual(3, Progress.GetValue(MetaCondition.Of(MetaConditionType.AffinityLevelCount, 1, "5")));
            Assert.IsTrue(Progress.IsMet(MetaCondition.Of(MetaConditionType.AffinityLevelCount, 2, "10")));
        }

        [Test]
        public void RedDot_PerLineage()
        {
            Own("sock", 60);
            Affinity.RegisterRedDots(RedDots);
            RedDots.Refresh();
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.LineageAffinity("sock")));
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.LineageAffinity("candle")));

            Affinity.TryClaim("sock");
            RedDots.Refresh();
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.LineageAffinity("sock")), "받으면 꺼진다");
        }
    }
}
