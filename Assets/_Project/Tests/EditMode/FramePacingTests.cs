using IdleGame.UI;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>perf PF-1: 프레임 페이싱 계산 (모바일 주사율 / n, PC 제한기) · 자동 품질 판정이 실제 목표를 써야 하는 이유 (S9)</summary>
    public class FramePacingTests
    {
        [TestCase(60.0, 60, 60)]
        [TestCase(59.94, 60, 60)]
        [TestCase(120.0, 60, 60)]
        [TestCase(90.0, 60, 90)]
        [TestCase(144.0, 60, 72)]
        [TestCase(60.0, 30, 30)]
        [TestCase(90.0, 30, 30)]
        [TestCase(120.0, 30, 30)]
        [TestCase(144.0, 30, 36)]
        public void Mobile_TargetIsRefreshOverDivisor(double hz, int want, int expected)
        {
            Assert.AreEqual(expected, FramePacing.EffectiveFps(hz, want, true));
        }

        [TestCase(165.0, 60)]
        [TestCase(59.951, 60)]
        [TestCase(144.0, 30)]
        public void Desktop_UsesLimiterTarget_WhateverTheMonitor(double hz, int want)
        {
            Assert.AreEqual(want, FramePacing.EffectiveFps(hz, want, false));
        }

        [Test]
        public void UnknownRefresh_FallsBackToWant()
        {
            Assert.AreEqual(60, FramePacing.EffectiveFps(0.0, 60, true));
            Assert.AreEqual(60, FramePacing.EffectiveFps(double.NaN, 60, true));
            Assert.AreEqual(30, FramePacing.EffectiveFps(5000.0, 30, true));
            Assert.AreEqual(FramePacing.DefaultFps, FramePacing.EffectiveFps(60.0, 0, true));
        }

        [Test]
        public void Divisor_StaysInRange()
        {
            Assert.AreEqual(4, FramePacing.Divisor(1000.0, 30));
            Assert.AreEqual(1, FramePacing.Divisor(30.0, 60));
            Assert.AreEqual(1, FramePacing.Divisor(double.NaN, 60));
            Assert.AreEqual(1, FramePacing.Divisor(60.0, 0));
        }

        /// <summary>
        /// S9: 90 Hz 화면에 targetFrameRate 60을 주면 45 fps(22.2 ms)로 돈다. 옛 기준(목표 60 → 22 ms)으로는 건강한 기기가 10초 만에 품질을 잃는다.
        /// FramePacing은 90을 맞추고(11 ms), FrameTarget이 그 값을 넘기면 느림으로 세지 않는다
        /// </summary>
        [Test]
        public void FxQuality_At90HzPanel_UsesEffectiveTarget()
        {
            var old = new FxQualityMonitor();
            bool loweredOld = false;
            for (int i = 0; i < 45 * 12; i++) loweredOld |= old.Sample(1f / 45f, 60);
            Assert.IsTrue(loweredOld, "목표 60으로 45 fps를 재면 느림 두 번 → 품질 낮춤 (지금의 버그)");

            int target = FramePacing.EffectiveFps(90.0, 60, true);
            Assert.AreEqual(90, target);
            var paced = new FxQualityMonitor();
            bool loweredPaced = false;
            for (int i = 0; i < target * 12; i++) loweredPaced |= paced.Sample(1f / target, target);
            Assert.IsFalse(loweredPaced, "실제 목표(90)로 맞춰 돌면 느림이 아니다");

            var halved = new FxQualityMonitor();
            bool loweredHalved = false;
            for (int i = 0; i < 45 * 12; i++) loweredHalved |= halved.Sample(1f / 45f, 45);
            Assert.IsFalse(loweredHalved, "목표가 45로 알려지면 45 fps는 느림이 아니다");
        }
    }
}
