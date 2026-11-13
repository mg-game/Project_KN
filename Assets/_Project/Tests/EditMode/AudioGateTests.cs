using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 소리 제한 (AUDIO-SPEC §3.5): 키 게이트 (간격 · 동시 수), 흔들기 범위, 섞어 뽑기 (반복 없음), 토큰 통, 사다리 (오름 · 유지 · 처음으로),
    /// 근음이 여럿인 조율 키의 가까운 테이크, UISfxGate의 키별 간격 (예전 동작은 FeedbackGateTests가 지킨다).
    /// </summary>
    public class AudioGateTests
    {
        [Test]
        public void Gate_MinInterval_AndMaxVoices()
        {
            var gate = new AudioGate(4);
            Assert.IsTrue(gate.TryPass(1, 0.03f, 2, 10.0, 1f));
            Assert.IsFalse(gate.TryPass(1, 0.03f, 2, 10.02, 1f), "30ms 안");
            Assert.IsTrue(gate.TryPass(1, 0.03f, 2, 10.031, 1f));
            Assert.IsFalse(gate.TryPass(1, 0.03f, 2, 10.07, 1f), "1초짜리가 둘 울리는 중 (동시 2)");
            Assert.IsTrue(gate.TryPass(1, 0.03f, 2, 11.01, 1f), "첫 소리가 끝나면 다시");
            Assert.IsTrue(gate.TryPass(2, 0.03f, 2, 10.02, 1f), "다른 키는 따로");
            Assert.IsFalse(gate.TryPass(9, 0f, 1, 0, 0f), "범위 밖 키");
        }

        [Test]
        public void Jitter_StaysInRange()
        {
            Assert.AreEqual(0.94f, AudioGate.PitchJitter(0.06f, 0f), 1e-5f);
            Assert.AreEqual(1.06f, AudioGate.PitchJitter(0.06f, 1f), 1e-5f);
            Assert.AreEqual(1f, AudioGate.PitchJitter(0.06f, 0.5f), 1e-5f);
            Assert.AreEqual(-1.5f, AudioGate.GainJitterDb(1.5f, 0f), 1e-5f);
            Assert.AreEqual(1.5f, AudioGate.GainJitterDb(1.5f, 1f), 1e-5f);
            Assert.AreEqual(1f, AudioGate.DbToGain(0f), 1e-6f);
            Assert.AreEqual(0.5012f, AudioGate.DbToGain(-6f), 1e-3f);
            Assert.AreEqual(0f, AudioGate.DbToGain(-90f));
        }

        [Test]
        public void ShuffleBag_NeverRepeatsLast_WithTwoOrMore()
        {
            var state = new ShuffleState();
            int last = -1;
            for (int i = 0; i < 200; i++)
            {
                int pick = ShuffleBag.Next(ref state, 2, (i * 0.37f) % 1f);
                Assert.AreNotEqual(last, pick, "같은 테이크 연속");
                last = pick;
            }
            state = new ShuffleState();
            Assert.AreEqual(0, ShuffleBag.Next(ref state, 1, 0.9f));
            Assert.AreEqual(0, ShuffleBag.Next(ref state, 1, 0.1f), "하나뿐이면 늘 그것");
            Assert.AreEqual(-1, ShuffleBag.Next(ref state, 0, 0.5f));
        }

        [Test]
        public void ShuffleBag_AvoidsLastTwo_WithFourOrMore()
        {
            var state = new ShuffleState();
            int a = -1;
            int b = -1;
            for (int i = 0; i < 400; i++)
            {
                int pick = ShuffleBag.Next(ref state, 5, ((i * 7919) % 1000) / 1000f);
                Assert.AreNotEqual(a, pick);
                Assert.AreNotEqual(b, pick);
                b = a;
                a = pick;
            }
        }

        [Test]
        public void TokenBucket_RefillsAtItsRate()
        {
            var bucket = new TokenBucket(14f, 6f);
            int passed = 0;
            for (int i = 0; i < 20; i++)
            {
                if (bucket.TryTake(0.0)) passed++;
            }
            Assert.AreEqual(6, passed, "처음엔 모인 만큼 (burst)");
            Assert.IsFalse(bucket.TryTake(0.0));
            Assert.IsTrue(bucket.TryTake(0.08), "1/14초 지나면 하나");
            Assert.IsFalse(bucket.TryTake(0.081));
            Assert.AreEqual(6f, bucket.Peek(10.0), 1e-4f, "최대 burst까지만 모인다");
            var fast = new TokenBucket(10f, 1f);
            Assert.IsTrue(fast.TryTake(0.0));
            Assert.IsFalse(fast.TryTake(0.09, 1f));
            Assert.IsTrue(fast.TryTake(0.18, 1f));
            Assert.IsFalse(fast.TryTake(0.25, 0.5f), "잠: 절반 속도로 찬다");
        }

        [Test]
        public void Ladder_StepsUp_HoldsTheTop_AndResets()
        {
            var def = AudioCatalog.Ladders[AudioCatalog.LadderKill];
            var state = new LadderState();
            Assert.AreEqual(0, PitchLadder.Advance(ref state, in def, 1.0));
            Assert.AreEqual(1, PitchLadder.Advance(ref state, in def, 1.5), "0.8초 안: 한 칸");
            Assert.AreEqual(2, PitchLadder.Advance(ref state, in def, 2.2));
            Assert.AreEqual(2, PitchLadder.Advance(ref state, in def, 3.2), "0.8~1.2초: 그대로");
            Assert.AreEqual(0, PitchLadder.Advance(ref state, in def, 4.5), "1.2초 넘게 조용하면 처음");
            double t = 5.0;
            int step = 0;
            for (int i = 0; i < 20; i++) step = PitchLadder.Advance(ref state, in def, t += 0.1);
            Assert.AreEqual(def.Count - 1, step, "맨 위 유지");
        }

        [Test]
        public void NearestTake_PicksTheClosestRoot_ForMultiRootKeys()
        {
            float[] lamp = { 698.24f, 698.24f, 1396.79f, 1396.79f };
            int low = PitchLadder.NearestTake(523.25f, lamp, 4, 0.9f);
            Assert.IsTrue(low == 0 || low == 1, "C5는 F5 근음 테이크에서 (−5반음)");
            int high = PitchLadder.NearestTake(2093f, lamp, 4, 0.1f);
            Assert.IsTrue(high == 2 || high == 3, "C7은 F6 근음 테이크에서 (+7반음)");
            Assert.AreEqual(2093f / 1396.79f, PitchLadder.PitchFor(2093f, lamp[high]), 1e-4f);
            float[] none = { 0f, 0f };
            Assert.AreEqual(-1, PitchLadder.NearestTake(880f, none, 2, 0.5f), "근음이 없으면 −1");
            float[] kill = { 697.99f, 697.96f, 1174.91f, 1174.91f };
            int a6 = PitchLadder.NearestTake(AudioCatalog.LadderHz(AudioCatalog.LadderKill, 7), kill, 4, 0.5f);
            Assert.IsTrue(a6 >= 2, "A6은 D6 근음 테이크에서 (+7반음)");
        }

        [Test]
        public void UISfxGate_PerKeyIntervals_AndTunedKeyHasNoJitter()
        {
            var gate = new UISfxGate();
            Assert.IsTrue(gate.TryPlay(UISfxKey.CountTick, 1f, 0.05f, 0f, out float pitch));
            Assert.AreEqual(1f, pitch, 1e-6f, "조율 키는 흔들지 않는다 (음높이는 사다리)");
            Assert.IsFalse(gate.TryPlay(UISfxKey.CountTick, 1.06f, 0.05f, 0.5f, out _), "70ms 안");
            Assert.IsTrue(gate.TryPlay(UISfxKey.CountTick, 1.071f, 0.05f, 0.5f, out _));
            Assert.IsTrue(gate.TryPlay(UISfxKey.SliderTick, 2f, 0.05f, 0.5f, out _));
            Assert.IsFalse(gate.TryPlay(UISfxKey.SliderTick, 2.14f, 0.05f, 0.5f, out _), "볼륨 막대 150ms");
            Assert.IsTrue(gate.TryPlay(UISfxKey.RedDotNew, 3f, 0.1f, 0.5f, out _));
            Assert.IsFalse(gate.TryPlay(UISfxKey.RedDotNew, 4.9f, 0.1f, 0.5f, out _), "빨간 점 2초");
            Assert.IsTrue(gate.TryPlay(UISfxKey.RedDotNew, 5.01f, 0.1f, 0.5f, out _));
            Assert.AreEqual(UISfxGate.SameKeyInterval, UISfxGate.MinInterval(UISfxKey.Tap), "예전 키는 40ms 그대로");
            Assert.AreEqual(UISfxGate.PitchJitter, UISfxGate.JitterFor(UISfxKey.Claim));
        }
    }
}
