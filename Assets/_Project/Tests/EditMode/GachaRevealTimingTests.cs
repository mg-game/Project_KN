using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 소환 공개 박자 (CR-04, SPEC4 §7.5 — PlayMode GachaRevealTests 대신 EditMode, RM-2 plan §2.5): GachaRevealTiming만 본다 (장면 · Game 없음).
    ///   · 10회에 ★4 이상이 없으면 4초 안 · 공개 소리 1초에 12번 이하 · ★4 이상 한 장마다 1.6초 더.
    ///   · 장마다 차례(FlipStart) → 뒤집기 → 다음 차례가 MultiDuration으로 이어진다 (GachaResultPopup.MultiReveal이 이 모형으로 기다린다).
    ///   · ★5 무지개 별가루는 여섯 빛깔 몫의 합이 별가루 수 그대로 (움직임 줄이기 9도).
    ///   · 건너뛰기는 0.5초부터 (0.4초에는 없음, 0.6초에는 있음).
    ///   · 예고(흔들림 빛)는 실제 등급보다 높지 않다 · 등급 빛 표 · 별가루 수 (움직임 줄이기 절반).
    ///   · 1회 박자: 떨어짐 → 흔들림 → 열림 → 그림자 → 끝, 그림자 → 흰색.
    /// </summary>
    public class GachaRevealTimingTests
    {
        static readonly ItemGrade[] AllGrades = { ItemGrade.Common, ItemGrade.Rare, ItemGrade.Epic, ItemGrade.Legendary, ItemGrade.Mythic };

        [Test]
        public void TenPull_WithoutHigh_FinishesWithinFourSeconds()
        {
            float total = GachaRevealTiming.MultiDuration(10, 0);
            Assert.LessOrEqual(total, GachaRevealTiming.MaxTenWithoutHigh, $"10회 (★4 없음) {total:0.00}초");
            Assert.Greater(total, GachaRevealTiming.AppearEnd(10) + 10 * GachaRevealTiming.TenFlip, "나타남 + 뒤집기 열 번보다 길다");
        }

        [Test]
        public void TenPull_EachHighCard_AddsFullReveal()
        {
            float none = GachaRevealTiming.MultiDuration(10, 0);
            Assert.AreEqual(none + GachaRevealTiming.HighReveal, GachaRevealTiming.MultiDuration(10, 1), 1e-4f);
            Assert.AreEqual(none + 2 * GachaRevealTiming.HighReveal, GachaRevealTiming.MultiDuration(10, 2), 1e-4f);
            Assert.AreEqual(1.6f, GachaRevealTiming.HighReveal, 1e-6f);
            Assert.AreEqual(GachaRevealTiming.MultiDuration(10, 10), GachaRevealTiming.MultiDuration(10, 99), 1e-4f, "장 수보다 많이 세지 않는다");
            Assert.AreEqual(0f, GachaRevealTiming.MultiDuration(0, 0), "결과가 없으면 0");
        }

        [Test]
        public void TenPull_RevealSounds_AtMostTwelvePerSecond()
        {
            Assert.LessOrEqual(GachaRevealTiming.MaxRevealSoundsPerSecond, 12f);
            Assert.GreaterOrEqual(GachaRevealTiming.TenFlipStep, GachaRevealTiming.TenFlip, "다음 뒤집기는 앞 뒤집기가 끝난 뒤");
            // 뒤집기 시작이 겹치지 않고 간격이 1/12초 이상
            for (int i = 1; i < 10; i++)
            {
                float gap = GachaRevealTiming.FlipStart(10, i, 0) - GachaRevealTiming.FlipStart(10, i - 1, 0);
                Assert.GreaterOrEqual(gap, 1f / 12f - 1e-5f, $"{i}번째 뒤집기 간격");
            }
        }

        [Test]
        public void TenPull_FlipStart_WaitsForEarlierHighReveals()
        {
            float plain = GachaRevealTiming.FlipStart(10, 5, 0);
            Assert.AreEqual(plain + GachaRevealTiming.HighReveal, GachaRevealTiming.FlipStart(10, 5, 1), 1e-4f);
            Assert.GreaterOrEqual(GachaRevealTiming.FlipStart(10, 0, 0), GachaRevealTiming.AppearEnd(10), "모두 나타난 뒤 뒤집는다");
            Assert.AreEqual(9 * GachaRevealTiming.TenAppearStagger + GachaRevealTiming.TenAppearPop, GachaRevealTiming.AppearEnd(10), 1e-5f);
            Assert.AreEqual(0.04f, GachaRevealTiming.TenAppearStagger, 1e-6f);
            Assert.AreEqual(0.08f, GachaRevealTiming.TenFlip, 1e-6f);
        }

        [Test]
        public void TenPull_Turns_ChainIntoMultiDuration()
        {
            // 장 i 차례 = FlipStart(count, i, 앞의 ★4 이상 수) — ★4 이상이면 그 자리에서 1.6초 크게 보여 준 뒤 뒤집는다.
            // 다음 차례 = 이번 뒤집기 + TenFlipStep, 끝 = 마지막 뒤집기 + TenFlipStep + Settle = MultiDuration
            bool[] high = { false, true, false, false, true, false, false, false, false, true };
            int count = high.Length;
            Assert.AreEqual(GachaRevealTiming.AppearStart(count - 1) + GachaRevealTiming.TenAppearPop, GachaRevealTiming.FlipStart(count, 0, 0), 1e-5f,
                "마지막 장이 다 나타난 때가 첫 차례");
            int before = 0;
            float flip = 0f;
            for (int i = 0; i < count; i++)
            {
                float turn = GachaRevealTiming.FlipStart(count, i, before);
                if (i > 0) Assert.AreEqual(flip + GachaRevealTiming.TenFlipStep, turn, 1e-4f, $"{i}번째 차례 = 앞 뒤집기 + 간격");
                flip = turn + (high[i] ? GachaRevealTiming.HighReveal : 0f);
                if (high[i]) before++;
            }
            Assert.AreEqual(flip + GachaRevealTiming.TenFlipStep + GachaRevealTiming.Settle, GachaRevealTiming.MultiDuration(count, before), 1e-4f, "끝");
        }

        [Test]
        public void Rainbow_SharesAddUpToStardustCount()
        {
            foreach (bool reduced in new[] { false, true })
            {
                int count = GachaRevealTiming.StardustCount(ItemGrade.Mythic, reduced);
                int total = 0, max = 0, min = int.MaxValue;
                for (int i = 0; i < GachaRevealTiming.RainbowHues; i++)
                {
                    int share = GachaRevealTiming.RainbowShare(count, i);
                    total += share;
                    max = Mathf.Max(max, share);
                    min = Mathf.Min(min, share);
                }
                Assert.AreEqual(count, total, $"무지개 별가루 합 (reduced={reduced})");
                Assert.LessOrEqual(max - min, 1, "고르게 나눈다");
            }
            Assert.AreEqual(2, GachaRevealTiming.RainbowShare(9, 0));
            Assert.AreEqual(1, GachaRevealTiming.RainbowShare(9, 5));
            Assert.AreEqual(0, GachaRevealTiming.RainbowShare(9, 6), "빛깔 밖");
            Assert.AreEqual(0, GachaRevealTiming.RainbowShare(3, 4), "적으면 앞 빛깔만");
            Assert.AreEqual(0, GachaRevealTiming.RainbowShare(0, 0));
        }

        [Test]
        public void Skip_HiddenAtPoint4_VisibleAtPoint6()
        {
            Assert.IsFalse(GachaRevealTiming.SkipVisible(0f));
            Assert.IsFalse(GachaRevealTiming.SkipVisible(0.4f));
            Assert.IsTrue(GachaRevealTiming.SkipVisible(0.5f));
            Assert.IsTrue(GachaRevealTiming.SkipVisible(0.6f));
        }

        [Test]
        public void Anticipation_NeverAboveActualGrade()
        {
            foreach (var grade in AllGrades)
            {
                Assert.LessOrEqual(GachaRevealTiming.AnticipationGrade(grade), grade, $"{grade} 예고");
                Assert.AreEqual(grade >= ItemGrade.Legendary, GachaRevealTiming.AnticipationGlow(grade), $"{grade} 예고 빛");
                // 흔들림 폭도 실제 등급으로만 커진다
                float amplitude = GachaRevealTiming.ShakeAmplitude(grade, false);
                Assert.AreEqual(grade >= ItemGrade.Legendary ? 6f * 1.6f : 6f, amplitude, 1e-4f, $"{grade} 흔들림");
                Assert.AreEqual(0f, GachaRevealTiming.ShakeAmplitude(grade, true), $"{grade} 움직임 줄이기");
            }
        }

        [Test]
        public void GradeLight_Mapping()
        {
            AssertColor("FFF4DC", GachaRevealTiming.GradeLight(ItemGrade.Common));
            AssertColor("FFF4DC", GachaRevealTiming.GradeLight(ItemGrade.Rare));
            AssertColor("E9A6C6", GachaRevealTiming.GradeLight(ItemGrade.Epic));
            AssertColor("F2C46B", GachaRevealTiming.GradeLight(ItemGrade.Legendary));
            AssertColor("C4B2F2", GachaRevealTiming.GradeLight(ItemGrade.Mythic));
        }

        [Test]
        public void Stardust_CountsAndReducedHalf()
        {
            Assert.AreEqual(0, GachaRevealTiming.StardustCount(ItemGrade.Epic, false));
            Assert.AreEqual(12, GachaRevealTiming.StardustCount(ItemGrade.Legendary, false));
            Assert.AreEqual(18, GachaRevealTiming.StardustCount(ItemGrade.Mythic, false));
            Assert.AreEqual(9, GachaRevealTiming.StardustCount(ItemGrade.Mythic, true));
            Assert.AreEqual(6, GachaRevealTiming.StardustCount(ItemGrade.Legendary, true));
            Assert.IsTrue(GachaRevealTiming.RainbowStardust(ItemGrade.Mythic));
            Assert.IsFalse(GachaRevealTiming.RainbowStardust(ItemGrade.Legendary));
            Assert.AreEqual(GachaRevealTiming.RainbowHue(0), GachaRevealTiming.RainbowHue(6), "여섯 빛깔을 돈다");
            Assert.AreNotEqual(GachaRevealTiming.RainbowHue(0), GachaRevealTiming.RainbowHue(1));
        }

        [Test]
        public void SingleBeats_InOrder()
        {
            Assert.AreEqual(GachaRevealTiming.Beat.Drop, GachaRevealTiming.SingleBeat(0.1f));
            Assert.AreEqual(GachaRevealTiming.Beat.Shake, GachaRevealTiming.SingleBeat(0.5f));
            Assert.AreEqual(GachaRevealTiming.Beat.Open, GachaRevealTiming.SingleBeat(0.8f));
            Assert.AreEqual(GachaRevealTiming.Beat.Silhouette, GachaRevealTiming.SingleBeat(1.2f));
            Assert.AreEqual(GachaRevealTiming.Beat.Done, GachaRevealTiming.SingleBeat(1.35f));
            Assert.AreEqual(1.35f, GachaRevealTiming.SingleDuration, 1e-6f);
        }

        [Test]
        public void Drop_StartsHigh_LandsAtZero()
        {
            Assert.AreEqual(320f, GachaRevealTiming.DropOffset(0f), 1e-3f);
            Assert.AreEqual(0f, GachaRevealTiming.DropOffset(GachaRevealTiming.DropEnd), 1e-3f);
            Assert.AreEqual(0f, GachaRevealTiming.DropOffset(1f), 1e-3f);
            Assert.Less(GachaRevealTiming.DropOffset(0.2f), GachaRevealTiming.DropOffset(0.05f), "내려온다");
        }

        [Test]
        public void Shake_OnlyBetweenDropAndOpen()
        {
            Assert.AreEqual(0f, GachaRevealTiming.ShakeAngle(0.1f, ItemGrade.Legendary, false));
            Assert.AreEqual(0f, GachaRevealTiming.ShakeAngle(0.9f, ItemGrade.Legendary, false));
            float peak = 0f;
            for (float t = GachaRevealTiming.DropEnd; t < GachaRevealTiming.ShakeEnd; t += 0.005f)
                peak = Mathf.Max(peak, Mathf.Abs(GachaRevealTiming.ShakeAngle(t, ItemGrade.Rare, false)));
            Assert.LessOrEqual(peak, 6f + 1e-3f);
            Assert.Greater(peak, 5f, "±6° 가까이 흔들린다");
            Assert.AreEqual(0f, GachaRevealTiming.ShakeAngle(0.5f, ItemGrade.Mythic, true), "움직임 줄이기면 흔들지 않는다");
        }

        [Test]
        public void Portrait_SilhouetteToWhite()
        {
            AssertNear(GachaRevealTiming.SilhouetteTint, GachaRevealTiming.PortraitTint(0.5f), "열리기 전 그림자");
            AssertNear(GachaRevealTiming.SilhouetteTint, GachaRevealTiming.PortraitTint(GachaRevealTiming.OpenEnd), "1.05초 그림자");
            AssertNear(Color.white, GachaRevealTiming.PortraitTint(GachaRevealTiming.SilhouetteEnd), "1.35초 흰색");
            Assert.AreEqual(0f, GachaRevealTiming.OpenProgress(0.7f), 1e-5f);
            Assert.AreEqual(1f, GachaRevealTiming.OpenProgress(1.05f), 1e-5f);
            Assert.AreEqual(GachaRevealTiming.ShakeEnd, GachaRevealTiming.HighRevealBeatTime(0f), 1e-5f);
            Assert.AreEqual(GachaRevealTiming.SilhouetteEnd, GachaRevealTiming.HighRevealBeatTime(GachaRevealTiming.HighRevealBeats), 1e-5f);
            Assert.AreEqual(GachaRevealTiming.SilhouetteEnd, GachaRevealTiming.HighRevealBeatTime(GachaRevealTiming.HighReveal), 1e-5f, "뒤쪽은 머무름");
        }

        static void AssertNear(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-4f, what + " r");
            Assert.AreEqual(expected.g, actual.g, 1e-4f, what + " g");
            Assert.AreEqual(expected.b, actual.b, 1e-4f, what + " b");
            Assert.AreEqual(expected.a, actual.a, 1e-4f, what + " a");
        }

        static void AssertColor(string hex, Color actual)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString("#" + hex, out var expected));
            Assert.AreEqual(expected.r, actual.r, 0.003f, hex + " r");
            Assert.AreEqual(expected.g, actual.g, 0.003f, hex + " g");
            Assert.AreEqual(expected.b, actual.b, 0.003f, hex + " b");
        }
    }
}
