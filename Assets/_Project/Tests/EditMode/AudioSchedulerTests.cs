using IdleGame.UI;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 연출 시계 대기열 (AUDIO-SPEC §3.6, §3.9, §5.6): BattleFx와 같은 정지 거울로 시계를 옮기면 불 밝히기 점화는 예약된 명중을 0.096초,
    /// 보스 처치 정지는 0.246초 늦춘다 (화면과 같이). 가장 이른 것부터, 넘치면 가장 낮은 등급의 가장 오래된 것을 버린다, 스테이지가 바뀌면 전투 예약만 비운다,
    /// 지연 보정 상한 60ms, 군중 묶음 (60ms 창 4개 이상 → 중요한 2개 + hit_multi).
    /// </summary>
    public class AudioSchedulerTests
    {
        static PendingSound Sound(short key, AudioVoiceClass cls = AudioVoiceClass.Normal, AudioPlayFlags flags = AudioPlayFlags.Battle, float importance = 0f,
            float pan = 0f) =>
            new() { Key = key, Class = cls, Flags = flags, Importance = importance, Pan = pan, Volume = 1f, Pitch = 1f, EnemyId = -1 };

        /// <summary>실제 시간 dt로 프레임을 돌리며 연출 시계가 due에 닿기까지 걸린 실제 시간</summary>
        static double RealTimeUntil(ref FxSlowMotion slow, AudioScheduler scheduler, double due)
        {
            const float dt = 1f / 240f;
            double real = 0;
            for (int i = 0; i < 100000 && scheduler.FxNow < due - 1e-9; i++)
            {
                float scale = slow.Tick(dt);
                scheduler.AdvanceFx(dt * scale);
                real += dt;
            }
            return real;
        }

        [Test]
        public void LampIgnite_DelaysPendingHitsBy0096()
        {
            var scheduler = new AudioScheduler();
            Assert.IsTrue(scheduler.Add(Sound(1), 0.3));
            var slow = new FxSlowMotion();
            slow.Clear();
            slow.Trigger(0f, LampWave.IgniteScale, LampWave.IgniteTime);
            double real = RealTimeUntil(ref slow, scheduler, 0.3);
            Assert.AreEqual(0.3 + 0.096, real, 0.006, "점화 0.12초 동안 0.2배 → 0.096초 늦게");
        }

        [Test]
        public void BossKillStop_DelaysPendingHitsBy0246()
        {
            var scheduler = new AudioScheduler();
            scheduler.Add(Sound(1), 0.5);
            var slow = new FxSlowMotion();
            slow.Clear();
            slow.Trigger(HitFeel.GlobalStop, HitFeel.SlowScale, HitFeel.SlowTime);
            double real = RealTimeUntil(ref slow, scheduler, 0.5);
            Assert.AreEqual(0.5 + 0.246, real, 0.006, "정지 0.12 + 0.3배 0.18초 → 0.246초 늦게");
        }

        [Test]
        public void EarliestDue_ComesFirst_AcrossClocks()
        {
            var scheduler = new AudioScheduler();
            scheduler.SetUiNow(100.0);
            scheduler.Add(Sound(1), 0.2);
            scheduler.Add(Sound(2), 0.05);
            scheduler.Add(Sound(3, AudioVoiceClass.Normal, AudioPlayFlags.UiClock), 0.1);
            Assert.AreEqual(-1, scheduler.EarliestDue(0.01, 100.01, 0));
            int first = scheduler.EarliestDue(1.0, 101.0, 0);
            Assert.AreEqual(2, scheduler.Items[first].Key, "남은 시간이 가장 짧은 것 (연출 0.05)");
            scheduler.RemoveAt(first);
            int second = scheduler.EarliestDue(1.0, 101.0, 0);
            Assert.AreEqual(3, scheduler.Items[second].Key, "UI 시계 0.1");
            scheduler.RemoveAt(second);
            Assert.AreEqual(-1, scheduler.EarliestDue(0.15, 101.0, 0));
            Assert.AreEqual(-1, scheduler.EarliestDue(0.15, 101.0, 0.06), "예측하지 않은 소리는 지연 보정을 쓰지 않는다");
            scheduler.Add(Sound(4, AudioVoiceClass.Normal, AudioPlayFlags.Battle | AudioPlayFlags.Predicted), 0.2);
            int predicted = scheduler.EarliestDue(0.15, 101.0, 0.06);
            Assert.AreNotEqual(-1, predicted, "예측한 소리(투사체 도착 등)는 지연 보정만큼 일찍 꺼낸다");
            Assert.AreEqual(4, scheduler.Items[predicted].Key);
        }

        [Test]
        public void Overflow_DropsTheOldestOfTheLowestClass()
        {
            var scheduler = new AudioScheduler();
            for (int i = 0; i < AudioScheduler.Capacity; i++)
                Assert.IsTrue(scheduler.Add(Sound((short)i, i == 5 ? AudioVoiceClass.Low : AudioVoiceClass.Normal), 1.0));
            Assert.IsTrue(scheduler.Add(Sound(500, AudioVoiceClass.High), 1.0), "넘치면 Low 하나를 버리고 넣는다");
            bool lowLeft = false;
            bool highIn = false;
            foreach (var item in scheduler.Items)
            {
                if (!item.Active) continue;
                lowLeft |= item.Key == 5;
                highIn |= item.Key == 500;
            }
            Assert.IsFalse(lowLeft);
            Assert.IsTrue(highIn);
            Assert.IsTrue(scheduler.Add(Sound(501, AudioVoiceClass.Normal), 1.0), "같은 등급이면 가장 오래된 Normal을 버린다");
            Assert.IsFalse(scheduler.Add(Sound(502, AudioVoiceClass.Low), 1.0), "새 소리가 가장 낮으면 새 소리를 버린다");
            Assert.AreEqual(AudioScheduler.Capacity, scheduler.Count);
        }

        [Test]
        public void FlushBattle_KeepsUiSounds()
        {
            var scheduler = new AudioScheduler();
            scheduler.Add(Sound(1), 0.5);
            scheduler.Add(Sound(2, AudioVoiceClass.Normal, AudioPlayFlags.UiClock), 0.5);
            Assert.AreEqual(1, scheduler.FlushBattle());
            Assert.AreEqual(1, scheduler.Count);
        }

        [Test]
        public void LatencyCompensation_IsCappedAndNeverNegative()
        {
            Assert.AreEqual(0.06, AudioScheduler.LatencyCompensation(1024, 4, 48000), 1e-9, "85ms 출력 지연 → 60ms까지만");
            Assert.AreEqual(0.0, AudioScheduler.LatencyCompensation(256, 2, 48000), 1e-9, "짧은 버퍼는 보정 없음");
            Assert.AreEqual(1024.0 * 2 / 44100 - 0.025, AudioScheduler.LatencyCompensation(1024, 2, 44100), 1e-9);
            Assert.AreEqual(0.0, AudioScheduler.LatencyCompensation(0, 0, 0));
        }

        [Test]
        public void CrowdFold_KeepsTheTwoMostImportant()
        {
            var scheduler = new AudioScheduler();
            var hit = AudioPlayFlags.Battle | AudioPlayFlags.Hit;
            scheduler.Add(Sound(10, AudioVoiceClass.Normal, hit, 0.5f, -0.2f), 0.10);
            scheduler.Add(Sound(11, AudioVoiceClass.Normal, hit, 4.5f, 0.2f), 0.12);
            scheduler.Add(Sound(12, AudioVoiceClass.Normal, hit, 0.1f, 0.0f), 0.13);
            scheduler.Add(Sound(13, AudioVoiceClass.Normal, hit, 2.2f, 0.1f), 0.15);
            scheduler.Add(Sound(14, AudioVoiceClass.Normal, hit, 9.0f, 0.0f), 0.40);
            int first = scheduler.EarliestDue(1.0, 0, 0);
            var item = scheduler.Items[first];
            scheduler.RemoveAt(first);
            var scratch = new int[32];
            Assert.IsTrue(scheduler.FoldCrowd(in item, 0.06, 4, scratch, out bool kept, out int total, out double meanDue, out float meanPan, out int folded));
            Assert.AreEqual(4, total, "0.40의 명중은 다른 창");
            Assert.IsFalse(kept, "중요도 0.5는 두 번째 안에 들지 못한다");
            Assert.AreEqual(1, folded, "11 · 13만 남고 12는 지운다");
            Assert.AreEqual(0.125, meanDue, 1e-6);
            Assert.AreEqual(0.025f, meanPan, 1e-6f);
            int keptCount = 0;
            foreach (var s in scheduler.Items)
            {
                if (s.Active && s.Has(AudioPlayFlags.Kept)) keptCount++;
            }
            Assert.AreEqual(2, keptCount);
            Assert.IsFalse(scheduler.FoldCrowd(in item, 0.06, 8, scratch, out _, out _, out _, out _, out _), "모자라면 묶지 않는다");
        }

        [Test]
        public void CrowdGain_FollowsTheFormula()
        {
            Assert.AreEqual(-9f, AudioScheduler.CrowdGainDb(2), 1e-4f);
            Assert.AreEqual(-6f, AudioScheduler.CrowdGainDb(4), 1e-4f);
            Assert.AreEqual(-3f, AudioScheduler.CrowdGainDb(8), 1e-4f);
            Assert.AreEqual(0f, AudioScheduler.CrowdGainDb(64), 1e-4f, "0 dB 상한");
        }
    }
}
