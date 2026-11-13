using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 목소리 고르기 (AUDIO-SPEC §3.4): 빈 목소리 먼저, 빼앗기 순서 (꼬리 70% &gt; 낮은 등급 &gt; 오래된 것), Critical은 절대 안 빼앗음,
    /// 50ms 안 된 목소리는 등급 차이 2 이상일 때만, 더 중요한 목소리는 안 빼앗음, 후보가 없으면 −1 (버림).
    /// </summary>
    public class AudioVoicePoolTests
    {
        static VoiceSlot Busy(AudioVoiceClass cls, double start, double end) => new() { Busy = true, Class = cls, Start = start, End = end, Key = 1 };

        [Test]
        public void FreeVoice_ComesFirst()
        {
            var slots = new[] { Busy(AudioVoiceClass.Low, 0, 10), new VoiceSlot(), Busy(AudioVoiceClass.Low, 0, 10) };
            Assert.AreEqual(1, AudioVoicePool.Choose(slots, 0, 3, AudioVoiceClass.Normal, 1.0, out bool steal));
            Assert.IsFalse(steal);
            slots[1] = Busy(AudioVoiceClass.High, 0, 0.5);
            Assert.AreEqual(1, AudioVoicePool.Choose(slots, 0, 3, AudioVoiceClass.Low, 1.0, out steal), "끝난 목소리는 빈 것");
            Assert.IsFalse(steal);
        }

        [Test]
        public void Steal_PrefersTails_ThenLowerClass_ThenOldest()
        {
            double now = 10.0;
            var slots = new[]
            {
                Busy(AudioVoiceClass.Low, 9.0, 12.0),     // 33% — 낮은 등급, 오래됨
                Busy(AudioVoiceClass.Normal, 9.5, 10.2),  // 71% — 꼬리
                Busy(AudioVoiceClass.Low, 9.8, 13.0),     // 6%
            };
            Assert.AreEqual(1, AudioVoicePool.Choose(slots, 0, 3, AudioVoiceClass.Normal, now, out bool steal), "꼬리(70% 넘음)가 먼저 — 잘려도 안 들림");
            Assert.IsTrue(steal);
            slots[1] = Busy(AudioVoiceClass.Normal, 9.5, 14.0);
            Assert.AreEqual(0, AudioVoicePool.Choose(slots, 0, 3, AudioVoiceClass.Normal, now, out _), "꼬리가 없으면 낮은 등급 중 오래된 것");
        }

        [Test]
        public void NeverStealsCritical_OrHigherClass()
        {
            var slots = new[] { Busy(AudioVoiceClass.Critical, 0, 100), Busy(AudioVoiceClass.High, 0, 100) };
            Assert.AreEqual(-1, AudioVoicePool.Choose(slots, 0, 2, AudioVoiceClass.Normal, 50.0, out bool steal), "더 중요한 목소리만 있으면 버린다");
            Assert.IsFalse(steal);
            Assert.AreEqual(1, AudioVoicePool.Choose(slots, 0, 2, AudioVoiceClass.Critical, 50.0, out steal), "Critical 새 소리도 Critical은 못 빼앗는다");
            Assert.IsTrue(steal);
        }

        [Test]
        public void YoungVoices_AreProtected_UnlessTheClassGapIsTwo()
        {
            var slots = new[] { Busy(AudioVoiceClass.Low, 9.98, 11.0) };
            Assert.AreEqual(-1, AudioVoicePool.Choose(slots, 0, 1, AudioVoiceClass.Normal, 10.0, out _), "20ms 된 목소리 (등급 차이 1)");
            Assert.AreEqual(0, AudioVoicePool.Choose(slots, 0, 1, AudioVoiceClass.High, 10.0, out bool steal), "등급 차이 2면 빼앗는다");
            Assert.IsTrue(steal);
            var scheduled = new[] { Busy(AudioVoiceClass.Low, 10.2, 11.0) };
            Assert.AreEqual(-1, AudioVoicePool.Choose(scheduled, 0, 1, AudioVoiceClass.Low, 10.0, out _), "예약된(아직 시작 전) 목소리도 바쁨");
        }

        [Test]
        public void Range_IsRespected()
        {
            var slots = new[] { new VoiceSlot(), Busy(AudioVoiceClass.Low, 0, 100), Busy(AudioVoiceClass.Low, 0, 100) };
            Assert.AreEqual(1, AudioVoicePool.Choose(slots, 1, 2, AudioVoiceClass.Normal, 50.0, out bool steal), "버스 범위 밖 빈 칸은 쓰지 않는다");
            Assert.IsTrue(steal);
            Assert.AreEqual(-1, AudioVoicePool.Choose(slots, 0, 0, AudioVoiceClass.Normal, 0, out _));
        }
    }
}
