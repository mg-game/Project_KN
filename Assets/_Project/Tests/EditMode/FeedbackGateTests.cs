using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 진동·효과음 간격 제한 (M18): HapticGate (80ms에 하나, 강한 진동 400ms에 하나), 진동 세기 표,
    /// UISfxGate (같은 소리 40ms에 하나, 한 소리 동시 3개, 음높이 ±5%, 단추 착지 +3%씩 최대 +18%). 정적 상태를 쓰지 않는다 (게이트를 새로 만듦).
    /// </summary>
    public class FeedbackGateTests
    {
        [Test]
        public void HapticGate_LimitsPulseRate()
        {
            var gate = new HapticGate();
            Assert.IsTrue(gate.TryPass(HapticKind.Light, 10f));
            Assert.IsFalse(gate.TryPass(HapticKind.Medium, 10.05f), "80ms 안");
            Assert.IsTrue(gate.TryPass(HapticKind.Medium, 10.09f));
        }

        [Test]
        public void HapticGate_HeavyOncePer400ms_BlockedDoesNotDelay()
        {
            var gate = new HapticGate();
            Assert.IsTrue(gate.TryPass(HapticKind.Heavy, 1f));
            Assert.IsFalse(gate.TryPass(HapticKind.Heavy, 1.2f), "강한 진동은 400ms에 하나");
            Assert.IsTrue(gate.TryPass(HapticKind.Light, 1.21f), "다른 세기는 80ms만 지키면 된다");
            Assert.IsTrue(gate.TryPass(HapticKind.Heavy, 1.41f), "막힌 시도는 시각을 늦추지 않는다");

            gate.Reset();
            Assert.IsTrue(gate.TryPass(HapticKind.Heavy, 1.42f));
        }

        [Test]
        public void Haptics_Table_IsShortAndSoft()
        {
            Assert.AreEqual(10, Haptics.DurationMs(HapticKind.Light));
            Assert.AreEqual(18, Haptics.DurationMs(HapticKind.Medium));
            Assert.AreEqual(30, Haptics.DurationMs(HapticKind.Heavy));
            Assert.AreEqual(50, Haptics.Amplitude(HapticKind.Light));
            Assert.AreEqual(110, Haptics.Amplitude(HapticKind.Medium));
            Assert.AreEqual(200, Haptics.Amplitude(HapticKind.Heavy));
            Assert.IsFalse(Haptics.PlaysWithoutAmplitude(HapticKind.Light), "SDK 26 미만은 가벼운 진동을 건너뛴다");
            Assert.IsTrue(Haptics.PlaysWithoutAmplitude(HapticKind.Heavy));
        }

        [Test]
        public void UISfxGate_SameKeyOncePer40ms_OtherKeysFree()
        {
            var gate = new UISfxGate();
            Assert.IsTrue(gate.TryPlay(UISfxKey.Tap, 1f, 0.1f, 0.5f, out float pitch));
            Assert.AreEqual(1f, pitch, 1e-6f, "흔들기 0.5 = 원래 음높이");
            Assert.IsFalse(gate.TryPlay(UISfxKey.Tap, 1.03f, 0.1f, 0.5f, out _), "같은 소리 40ms 안");
            Assert.IsTrue(gate.TryPlay(UISfxKey.Tab, 1.03f, 0.1f, 0.5f, out _), "다른 소리는 따로");
            Assert.IsTrue(gate.TryPlay(UISfxKey.Tap, 1.05f, 0.1f, 0.5f, out _));
        }

        [Test]
        public void UISfxGate_AtMostThreeVoicesPerKey()
        {
            var gate = new UISfxGate();
            Assert.IsTrue(gate.TryPlay(UISfxKey.Claim, 0f, 1f, 0.5f, out _));
            Assert.IsTrue(gate.TryPlay(UISfxKey.Claim, 0.05f, 1f, 0.5f, out _));
            Assert.IsTrue(gate.TryPlay(UISfxKey.Claim, 0.10f, 1f, 0.5f, out _));
            Assert.IsFalse(gate.TryPlay(UISfxKey.Claim, 0.15f, 1f, 0.5f, out _), "1초짜리 소리가 셋 다 울리는 중");
            Assert.IsTrue(gate.TryPlay(UISfxKey.Claim, 1.01f, 1f, 0.5f, out _), "첫 소리가 끝나면 다시");
        }

        [Test]
        public void UISfxGate_PitchJitterWithinFivePercent()
        {
            var gate = new UISfxGate();
            gate.TryPlay(UISfxKey.Tap, 0f, 0.1f, 0f, out float low);
            gate.TryPlay(UISfxKey.Tap, 1f, 0.1f, 1f, out float high);
            Assert.AreEqual(0.95f, low, 1e-5f);
            Assert.AreEqual(1.05f, high, 1e-5f);
        }

        [Test]
        public void UISfxGate_CoinLandRisesWithinWindow_CappedAt18Percent()
        {
            var gate = new UISfxGate();
            float now = 0f;
            Assert.IsTrue(gate.TryPlay(UISfxKey.CoinLand, now, 0.05f, 0.9f, out float pitch));
            Assert.AreEqual(1f, pitch, 1e-5f, "첫 착지는 원래 음 (흔들기 없음)");
            for (int i = 1; i <= 8; i++)
            {
                now += 0.05f;
                Assert.IsTrue(gate.TryPlay(UISfxKey.CoinLand, now, 0.05f, 0.9f, out pitch));
                Assert.AreEqual(1f + System.Math.Min(0.18f, 0.03f * i), pitch, 1e-5f, $"{i}번째 이어진 착지");
            }

            now += 0.5f; // 0.3초 넘게 쉬면 처음부터
            Assert.IsTrue(gate.TryPlay(UISfxKey.CoinLand, now, 0.05f, 0.9f, out pitch));
            Assert.AreEqual(1f, pitch, 1e-5f);
        }

        [Test]
        public void UISfxTable_EmptyTable_PicksNothing()
        {
            var table = UnityEngine.ScriptableObject.CreateInstance<UISfxTable>();
            try
            {
                Assert.IsNull(table.Get(UISfxKey.Tap));
                Assert.IsNull(table.Pick(UISfxKey.Tap, 0.5f), "클립이 없으면 조용히 건너뛴다");
                table.entries.Add(new UISfxTable.Entry { key = UISfxKey.Tap });
                table.ClearCache();
                Assert.IsNotNull(table.Get(UISfxKey.Tap));
                Assert.IsNull(table.Pick(UISfxKey.Tap, 0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }
    }
}
