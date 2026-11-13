using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 덕킹 (AUDIO-SPEC §3.10): 들어감 · 유지 · 풀림 곡선, 이유가 겹치면 가장 깊은 것 (곱하지 않음), 같은 이유를 다시 걸면 지금 깊이에서 이어짐,
    /// 지속 덕 (잠 · 시트 집중), 빠지는 소리 묶음 (불 밝히기 · 보스 · 치명타), 저역 통과 요청은 가장 낮은 값.
    /// </summary>
    public class AudioDuckTests
    {
        [Test]
        public void Envelope_AttackHoldRelease()
        {
            Assert.AreEqual(0f, DuckStack.Envelope(-10f, 0.1f, 1f, 0.5f, false, false, 0f, -0.01, 0), 1e-5f, "시작 전");
            Assert.AreEqual(-5f, DuckStack.Envelope(-10f, 0.1f, 1f, 0.5f, false, false, 0f, 0.05, 0), 1e-4f, "들어가는 중");
            Assert.AreEqual(-10f, DuckStack.Envelope(-10f, 0.1f, 1f, 0.5f, false, false, 0f, 0.6, 0), 1e-5f, "유지");
            Assert.AreEqual(-5f, DuckStack.Envelope(-10f, 0.1f, 1f, 0.5f, false, false, 0f, 1.35, 0), 1e-4f, "풀리는 중");
            Assert.AreEqual(0f, DuckStack.Envelope(-10f, 0.1f, 1f, 0.5f, false, false, 0f, 1.7, 0), 1e-5f, "끝");
            Assert.AreEqual(-8f, DuckStack.Envelope(-8f, 0.6f, 0f, 0f, true, false, 0f, 100.0, 0), 1e-5f, "지속 덕은 풀어 줄 때까지");
            Assert.AreEqual(-4f, DuckStack.Envelope(-8f, 0.6f, 0f, 0.4f, true, true, -8f, 100.0, 0.2), 1e-4f, "지속 덕 풀기");
        }

        [Test]
        public void DeepestDuckWins_NotTheProduct()
        {
            var ducks = new DuckStack();
            ducks.Trigger(DuckReason.LampLit, -8f, 0.05f, 1f, 0.9f, 0.0);
            ducks.Trigger(DuckReason.JingleClear, -10f, 0.06f, 0.3f, 0.2f, 0.0);
            Assert.AreEqual(-10f, ducks.DepthDb(0.2), 1e-4f, "−18(곱)이 아니라 가장 깊은 −10");
            Assert.AreEqual(-8f, ducks.DepthDb(0.7), 1e-4f, "징글 덕이 끝나면 불 밝히기 덕이 남는다");
            Assert.AreEqual(-4f, ducks.DepthDb(1.5), 1e-3f, "불 밝히기 덕이 풀리는 중 (0.45 / 0.9)");
            Assert.AreEqual(0f, ducks.DepthDb(3.0), 1e-4f);
            Assert.IsFalse(ducks.Tick(3.0), "끝난 덕은 지워진다");
        }

        [Test]
        public void Retrigger_ContinuesFromTheCurrentDepth()
        {
            var ducks = new DuckStack();
            ducks.Trigger(DuckReason.CritMicro, -4f, 0.1f, 0.06f, 0.12f, 0.0);
            Assert.AreEqual(-4f, ducks.DepthDb(0.12), 1e-4f);
            ducks.Trigger(DuckReason.CritMicro, -4f, 0.1f, 0.06f, 0.12f, 0.12);
            Assert.AreEqual(-4f, ducks.DepthDb(0.13), 1e-3f, "다시 걸어도 0에서 다시 들어가지 않는다 (덜컥임 없음)");
        }

        [Test]
        public void SustainedDuck_HoldsUntilReleased()
        {
            var ducks = new DuckStack();
            ducks.Sustain(DuckReason.Sleep, -9f, 0.6f, 0.0);
            Assert.AreEqual(-4.5f, ducks.DepthDb(0.3), 1e-3f);
            Assert.AreEqual(-9f, ducks.DepthDb(500.0), 1e-4f);
            Assert.IsTrue(ducks.Has(DuckReason.Sleep));
            ducks.Release(DuckReason.Sleep, 0.4f, 500.0);
            Assert.AreEqual(-4.5f, ducks.DepthDb(500.2), 1e-3f);
            Assert.AreEqual(0f, ducks.DepthDb(500.5), 1e-4f);
            ducks.Tick(500.5);
            Assert.IsFalse(ducks.Has(DuckReason.Sleep));
        }

        [Test]
        public void ExemptVoices_SkipThatDuck()
        {
            var ducks = new DuckStack();
            ducks.Trigger(DuckReason.LampLit, -3f, 0f, 1f, 0.9f, 0.0, DuckExempt.Lamp);
            ducks.Trigger(DuckReason.CritMicro, -4f, 0f, 0.06f, 0.12f, 0.0, DuckExempt.Crit);
            Assert.AreEqual(-4f, ducks.DepthDb(0.01), 1e-4f, "보통 소리는 둘 다 받고 깊은 것");
            Assert.AreEqual(-4f, ducks.DepthDb(0.01, DuckExempt.Lamp), 1e-4f, "불빛 소리는 불 밝히기 덕을 안 받는다");
            Assert.AreEqual(-3f, ducks.DepthDb(0.01, DuckExempt.Crit), 1e-4f, "치명타 층은 자기 작은 덕을 안 받는다");
            Assert.AreEqual(0f, ducks.DepthDb(0.01, DuckExempt.Lamp | DuckExempt.Crit), 1e-4f);
        }

        [Test]
        public void FutureTrigger_StartsLater()
        {
            var ducks = new DuckStack();
            ducks.Trigger(DuckReason.BossKill, -10f, 0.03f, 1.2f, 1f, 0.3);
            Assert.AreEqual(0f, ducks.DepthDb(0.2), 1e-5f, "처치 꽃이 터지는 순간부터 (예약)");
            Assert.AreEqual(-10f, ducks.DepthDb(0.5), 1e-4f);
        }

        [Test]
        public void LowPass_LowestRequestWins_AndOpensWhenCleared()
        {
            var lpf = new LowPassStack();
            Assert.IsFalse(lpf.Enabled);
            lpf.Set(DuckReason.FocusDimmed, 6000f);
            lpf.Set(DuckReason.Sleep, 1200f);
            Assert.AreEqual(1200f, lpf.Target, 1e-3f);
            lpf.Snap();
            Assert.IsTrue(lpf.Enabled);
            lpf.Clear(DuckReason.Sleep);
            Assert.AreEqual(6000f, lpf.Target, 1e-3f);
            for (int i = 0; i < 200; i++) lpf.Tick(1f / 60f);
            Assert.AreEqual(6000f, lpf.Current, 1f, "로그 주파수로 따라간다");
            lpf.Clear(DuckReason.FocusDimmed);
            for (int i = 0; i < 200; i++) lpf.Tick(1f / 60f);
            Assert.IsFalse(lpf.Enabled, "요청이 없으면 필터를 끈다");
        }
    }
}
