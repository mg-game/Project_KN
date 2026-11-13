using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    /// <summary>
    /// AppSignals 구독 (AUDIO-SPEC §8.2 #4, PHASE4 delta 14): 엔진이 붙어 있을 때만 잠 신호를 따라가고, 같은 값이 두 번 와도 견디며,
    /// 높은 등급 소환 신호는 덕만 건다 (징글은 OnGachaReveal 몫). 엔진이 없거나 떨어진 뒤의 신호는 조용히 지나간다.
    /// </summary>
    public class AudioSignalsTests
    {
        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            AppSignals.ResetState();
            foreach (var o in _created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        AudioEngine NewEngine(AudioLibrary library = null)
        {
            var go = new GameObject("[AudioSignalsTest]");
            _created.Add(go);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                _created.Add(library);
            }
            var engine = new AudioEngine(go, library, null);
            engine.ApplySettings();
            return engine;
        }

        [Test]
        public void Signals_WithoutAnEngine_AreQuietNoOps()
        {
            Assert.IsNull(AudioService.Engine, "편집 모드 테스트에는 \"[Audio]\"가 없다");
            Assert.DoesNotThrow(() =>
            {
                AppSignals.RaiseSleep(true);
                AppSignals.RaiseSleep(false);
                AppSignals.RaiseGachaRevealHigh((int)ItemGrade.Mythic);
            });
            Assert.IsFalse(AudioService.IsAsleep);
        }

        [Test]
        public void SleepSignal_FollowsTheAttachedEngine_AndStopsAfterDetach()
        {
            var engine = NewEngine();
            AudioService.Attach(engine);
            try
            {
                AppSignals.RaiseSleep(true);
                Assert.IsTrue(AudioService.IsAsleep, "잠 신호 → SetSleep(true)");
                AppSignals.RaiseSleep(true);
                Assert.IsTrue(engine.IsAsleep, "같은 값이 두 번 와도 그대로");
                AudioService.SetSleep(true);
                Assert.IsTrue(engine.IsAsleep, "직접 호출과 신호가 겹쳐도 그대로");
                AppSignals.RaiseSleep(false);
                Assert.IsFalse(AudioService.IsAsleep, "깸 신호 → SetSleep(false)");

                for (int frame = 0; frame < 30; frame++) engine.Tick(1f / 60f, 1f / 60f);
            }
            finally
            {
                AudioService.Detach(engine);
            }

            Assert.IsNull(AudioService.Engine);
            AppSignals.RaiseSleep(true);
            Assert.IsFalse(engine.IsAsleep, "떨어진 엔진은 신호를 듣지 않는다");

            AudioService.Attach(engine);
            AudioService.Attach(engine);
            try
            {
                AppSignals.RaiseSleep(false);
                AppSignals.RaiseSleep(true);
                Assert.IsTrue(engine.IsAsleep, "다시 붙이면 다시 듣는다 (두 번 붙여도 구독은 하나)");
            }
            finally
            {
                AudioService.Detach(engine);
            }
        }

        /// <summary>
        /// 높은 등급 소환 신호 (PHASE4 delta 14): 전설 이상만 배경음을 −8 dB 줄인다 (들어감 0.05초 · 유지 1.8초 · 풀림 0.8초, 이유 GachaHigh).
        /// 편집 모드에서는 UI 시계가 흐르지 않으므로 덕 깊이는 건 시각 + 0.5초(유지 구간)에서 읽는다.
        /// </summary>
        [Test]
        public void GachaRevealHighSignal_DucksBgmOnlyFromLegendary_AndStopsAfterDetach()
        {
            var engine = NewEngine();
            AudioService.Attach(engine);
            try
            {
                double probe = engine.UiNow + 0.5;
                Assert.AreEqual(0f, engine.BusDuckDb(AudioBus.Bgm, probe), 1e-4f, "새 엔진의 배경음에는 덕이 없다");

                AppSignals.RaiseGachaRevealHigh((int)ItemGrade.Common);
                AppSignals.RaiseGachaRevealHigh((int)ItemGrade.Epic);
                AppSignals.RaiseGachaRevealHigh(-7);
                Assert.IsFalse(engine.BusHasDuck(AudioBus.Bgm, DuckReason.GachaHigh), "전설 아래 등급 · 모르는 숫자는 덕을 걸지 않는다");
                Assert.AreEqual(0f, engine.BusDuckDb(AudioBus.Bgm, probe), 1e-4f, "전설 아래 등급은 배경음을 그대로 둔다");

                AppSignals.RaiseGachaRevealHigh((int)ItemGrade.Legendary);
                Assert.IsTrue(engine.BusHasDuck(AudioBus.Bgm, DuckReason.GachaHigh), "전설 → 배경음 덕 (이유 GachaHigh)");
                Assert.AreEqual(-8f, engine.BusDuckDb(AudioBus.Bgm, probe), 0.01f, "유지 구간에서 −8 dB");
                Assert.Less(engine.BusDuckDb(AudioBus.Bgm, engine.UiNow + 0.04), 0f, "들어가는 중에도 이미 줄고 있다");
                Assert.AreEqual(0f, engine.BusDuckDb(AudioBus.Bgm, engine.UiNow + 0.05 + 1.8 + 0.8 + 0.1), 1e-4f, "유지 + 풀림이 끝나면 0");
                Assert.AreEqual(0f, engine.BusDuckDb(AudioBus.Sfx, probe), 1e-4f, "효과음 버스는 줄이지 않는다");
                Assert.AreEqual(0f, engine.BusDuckDb(AudioBus.Stinger, probe), 1e-4f, "징글 버스도 줄이지 않는다");

                AppSignals.RaiseGachaRevealHigh((int)ItemGrade.Mythic);
                Assert.AreEqual(-8f, engine.BusDuckDb(AudioBus.Bgm, probe), 0.01f, "신화도 같은 덕 (겹치면 갱신될 뿐 더 깊어지지 않는다)");

                for (int frame = 0; frame < 10; frame++) engine.Tick(1f / 60f, 1f / 60f);
                Assert.IsTrue(engine.BusHasDuck(AudioBus.Bgm, DuckReason.GachaHigh), "짧은 Tick 뒤에도 덕은 아직 걸려 있다");
                Assert.Less(engine.BusDuckDb(AudioBus.Bgm, engine.UiNow + 0.5), -7.9f, "Tick 뒤 0.5초 시점도 유지 구간");
            }
            finally
            {
                AudioService.Detach(engine);
            }

            var detached = NewEngine();
            AudioService.Attach(detached);
            AudioService.Detach(detached);
            Assert.IsNull(AudioService.Engine);
            AppSignals.RaiseGachaRevealHigh((int)ItemGrade.Legendary);
            Assert.IsFalse(detached.BusHasDuck(AudioBus.Bgm, DuckReason.GachaHigh), "떨어진 엔진에는 소환 덕이 걸리지 않는다");
            Assert.AreEqual(0f, detached.BusDuckDb(AudioBus.Bgm, detached.UiNow + 0.5), 1e-4f, "떨어진 엔진의 배경음은 그대로");
        }

        /// <summary>음악 키만 든 라이브러리 (테이크 하나씩, 음량: bgm_sleep −22 LUFS · 나머지 −18 — 사용자 라이브러리의 값)</summary>
        AudioLibrary MusicLibrary(params string[] keys)
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            _created.Add(library);
            library.catalogVersion = AudioCatalog.Version;
            var entries = new AudioLibrary.Entry[AudioCatalog.Count];
            for (int i = 0; i < entries.Length; i++) entries[i] = new AudioLibrary.Entry { key = AudioCatalog.Keys[i].Key };
            foreach (var key in keys)
            {
                var clip = AudioClip.Create(key, 4410, 2, 44100, false);
                _created.Add(clip);
                var entry = entries[AudioCatalog.Find(key).Index];
                entry.takes = new[] { new AudioLibrary.Take { clip = clip, number = 1 } };
                entry.music = new AudioLibrary.MusicInfo { bpm = 60f, beatsPerBar = 4, bars = 1, loudnessLufs = key == "bgm_sleep" ? -22f : -18f };
            }
            library.entries = entries;
            library.Invalidate();
            return library;
        }

        /// <summary>
        /// 잠들 때 검증 줄 (SPEC4 S4-40 'BGM duck logged', V2 요청): 배경음을 어떻게 · 얼마나 줄였는지 한 줄 (에디터 · 개발 빌드).
        ///   · 잠 곡이 없으면 버스 덕 −9 dB + 1.2 kHz 저역 통과 (bgm_sleep=duck)
        ///   · 잠 곡이 있으면 버스 덕 없이 그 곡으로 (bgm_sleep=track, 음량 차 −22 − (−18) = −4)
        ///   · 잠 곡이 있어도 보스 곡이 울리는 중이면 잠 곡 대신 버스 덕 −9 dB + 1.2 kHz (bgm_sleep=duck bgm_state=Boss — F1 · RM-3, 예전에는 kept · 0)
        /// 같은 값이 다시 오면 줄이 없다 (엔진이 무시). 깰 때도 줄이 없다
        /// </summary>
        [Test]
        public void SleepEntry_LogsHowMuchTheBgmWasLowered()
        {
            Assert.AreEqual(-9f, AudioEngine.SleepBgmDb, 1e-4f, "AUDIO-SPEC §3.10: 배경음 ×0.35");
            Assert.AreEqual(1200f, AudioEngine.SleepBgmLowPassHz, 1e-2f);
            Assert.AreEqual(-8f, AudioEngine.SleepSfxDb, 1e-4f, "효과음 ×0.4");

            var plain = NewEngine();
            LogAssert.Expect(LogType.Log, "[Verify] bgm_duck=-9 bgm_lpf=1200 bgm_sleep=duck bgm_state=Main");
            plain.SetSleep(true);
            Assert.IsTrue(plain.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "잠 곡이 없으면 배경음 버스 덕");
            Assert.AreEqual(-9f, plain.BusDuckDb(AudioBus.Bgm, plain.UiNow + 1.0), 0.01f, "들어간 뒤 −9 dB");
            plain.SetSleep(true);
            plain.SetSleep(false);

            var lullaby = NewEngine(MusicLibrary("bgm_battle_night", "bgm_boss", "bgm_sleep"));
            LogAssert.Expect(LogType.Log, "[Verify] bgm_duck=-4 bgm_sleep=track bgm_state=Main");
            lullaby.SetSleep(true);
            Assert.IsFalse(lullaby.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "잠 곡이 있으면 버스 덕 대신 곡을 바꾼다 (AUDIO-SPEC §3.12)");
            Assert.IsTrue(lullaby.BusHasDuck(AudioBus.Sfx, DuckReason.Sleep), "효과음 덕은 늘 건다");
            Assert.IsTrue(lullaby.Music.SleepTrackChosen);
            lullaby.SetSleep(false);
            Assert.IsFalse(lullaby.Music.SleepTrackChosen, "깨면 잠 곡을 고르지 않는다");

            lullaby.SetBossMusic(true);
            LogAssert.Expect(LogType.Log, "[Verify] bgm_duck=-9 bgm_lpf=1200 bgm_sleep=duck bgm_state=Boss");
            lullaby.SetSleep(true);
            Assert.IsFalse(lullaby.Music.SleepTrackChosen, "보스 곡이 울리는 동안은 잠 곡이 아니다");
            Assert.IsTrue(lullaby.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "F1: 그동안은 잠 곡 대신 배경음 버스 덕");
            Assert.AreEqual(-9f, lullaby.BusDuckDb(AudioBus.Bgm, lullaby.UiNow + 1.0), 0.01f, "들어간 뒤 −9 dB");
            lullaby.SetSleep(false);
            lullaby.SetBossMusic(false);
        }

        /// <summary>UI 시계를 seconds초 움직이고 한 프레임 돌린다 (편집 모드에서는 Time이 흐르지 않는다 — AudioEngine.UiClockOffset)</summary>
        static void Step(AudioEngine engine, float seconds)
        {
            engine.UiClockOffset += seconds;
            engine.Tick(1f / 60f, 1f / 60f);
        }

        /// <summary>
        /// F1 (RM-3 · triage R54): 잠 곡(bgm_sleep)이 있어도 잠든 동안 챕터 보스 곡이 울리면 배경음을 −9 dB + 1.2 kHz로 줄인다.
        /// 보스가 사라져 잠 곡이 넘겨받으면 덕과 저역 통과를 MusicDirector.SleepFade(2초)에 걸쳐 함께 풀고 (나가는 보스 곡이 갑자기 커지지 않게),
        /// 잠 곡은 덕 없이 남는다 (R55 기본값). 잠든 채 보스가 다시 오면 다시 줄이고, 깨면 0.4초에 푼다. 바뀔 때마다 검증 줄이 하나씩.
        /// 음악 감독은 바로 시작시킨다 (Boot 0초 — 편집 모드에서는 DSP 시계가 0.5초 흐르기를 기다릴 수 없다). 덕 · 저역 통과 시간은 UiClockOffset으로 움직인다
        /// </summary>
        [Test]
        public void SleepDuringTheBossTrack_DucksTheBgm_UntilTheLullabyTakesOver()
        {
            Assert.AreEqual(AudioEngine.SleepBgmLowPassHz, AudioEngine.SleepGlideHz(0f), 0.01f, "푸는 길의 시작 = 1.2 kHz");
            Assert.AreEqual(LowPassStack.Open, AudioEngine.SleepGlideHz(1f), 0.5f, "끝 = 열림");
            Assert.AreEqual(AudioEngine.SleepBgmLowPassHz, AudioEngine.SleepGlideHz(-1f), 0.01f, "k는 0~1로 자른다");
            Assert.AreEqual(LowPassStack.Open, AudioEngine.SleepGlideHz(2f), 0.5f);
            float half = AudioEngine.SleepGlideHz(0.5f);
            Assert.AreEqual(Mathf.Sqrt(AudioEngine.SleepBgmLowPassHz * LowPassStack.Open), half, 1f, "옥타브로 고르게 — 절반은 기하 평균");

            // 잠 곡이 없으면 예전 그대로: 잠든 동안 Tick이 덕을 풀지 않는다
            var plain = NewEngine();
            LogAssert.Expect(LogType.Log, "[Verify] bgm_duck=-9 bgm_lpf=1200 bgm_sleep=duck bgm_state=Main");
            plain.SetSleep(true);
            for (int i = 0; i < 12; i++) Step(plain, 0.25f);
            Assert.AreEqual(-9f, plain.BusDuckDb(AudioBus.Bgm), 0.01f, "잠 곡이 없으면 깰 때까지 −9 dB");
            Assert.AreEqual(AudioEngine.SleepBgmLowPassHz, plain.BgmLowPassTarget, 0.5f, "저역 통과도 그대로");
            plain.SetSleep(false);
            Step(plain, 0.5f);
            Assert.IsFalse(plain.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "깨면 0.4초에 풀린다");
            Assert.AreEqual(LowPassStack.Open, plain.BgmLowPassTarget, 0.5f);

            var engine = NewEngine(MusicLibrary("bgm_battle_night", "bgm_boss", "bgm_sleep"));
            var bossTrack = AudioCatalog.Find("bgm_boss");
            var sleepTrack = AudioCatalog.Find("bgm_sleep");
            engine.Music.Boot(0, 0f);
            engine.SetBossMusic(true);
            Step(engine, 0f);
            Assert.AreEqual(bossTrack, engine.Music.CurrentKey, "챕터 보스가 살아 있으면 보스 곡");

            LogAssert.Expect(LogType.Log, "[Verify] bgm_duck=-9 bgm_lpf=1200 bgm_sleep=duck bgm_state=Boss");
            engine.SetSleep(true);
            Assert.IsFalse(engine.Music.SleepTrackChosen, "보스 곡이 울리는 동안은 잠 곡이 아니다");
            Assert.IsTrue(engine.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "F1: 보스 곡이면 잠 곡이 있어도 배경음 버스 덕");
            Assert.AreEqual(AudioEngine.SleepBgmLowPassHz, engine.BgmLowPassTarget, 0.5f, "+ 1.2 kHz 저역 통과");
            for (int i = 0; i < 5; i++) Step(engine, 0.25f);
            Assert.AreEqual(bossTrack, engine.Music.CurrentKey, "잠들어도 보스 곡은 그대로 (상태 Boss)");
            Assert.AreEqual(-9f, engine.BusDuckDb(AudioBus.Bgm), 0.01f, "들어간 뒤 −9 dB가 이어진다");

            engine.SetBossMusic(false);
            LogAssert.Expect(LogType.Log, "[Verify] bgm_duck=-4 bgm_sleep=track bgm_state=Main");
            Step(engine, 0f);
            Assert.AreEqual(sleepTrack, engine.Music.CurrentKey, "보스가 사라지면 잠 곡으로 교차");
            Assert.IsTrue(engine.Music.SleepTrackChosen);
            Assert.IsTrue(engine.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "덕은 바로 끊지 않고 풀리는 중");
            Assert.AreEqual(-4.5f, engine.BusDuckDb(AudioBus.Bgm, engine.UiNow + MusicDirector.SleepFade * 0.5), 0.05f, "SleepFade 절반에서 반만큼");
            Assert.AreEqual(0f, engine.BusDuckDb(AudioBus.Bgm, engine.UiNow + MusicDirector.SleepFade + 0.05), 1e-4f, "SleepFade 뒤 0");
            Step(engine, MusicDirector.SleepFade * 0.5f);
            Assert.AreEqual(half, engine.BgmLowPassTarget, 1f, "저역 통과도 같은 2초에 걸쳐 열린다 (절반)");
            Assert.AreEqual(-4.5f, engine.BusDuckDb(AudioBus.Bgm), 0.05f);
            Step(engine, MusicDirector.SleepFade * 0.5f + 0.1f);
            Assert.IsFalse(engine.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "다 풀리면 덕이 없다 — 잠 곡은 덕 없이 (R55 기본값)");
            Assert.AreEqual(LowPassStack.Open, engine.BgmLowPassTarget, 0.5f, "저역 통과도 열림");
            Assert.IsTrue(engine.BusHasDuck(AudioBus.Sfx, DuckReason.Sleep), "효과음 잠 덕은 깰 때까지");
            Assert.AreEqual(sleepTrack, engine.Music.CurrentKey);
            for (int i = 0; i < 4; i++) Step(engine, 0.25f);
            Assert.IsFalse(engine.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "잠 곡이 이어지는 동안 다시 걸지 않는다");

            engine.SetBossMusic(true);
            LogAssert.Expect(LogType.Log, "[Verify] bgm_duck=-9 bgm_lpf=1200 bgm_sleep=duck bgm_state=Boss");
            Step(engine, 0f);
            Assert.IsFalse(engine.Music.SleepTrackChosen);
            Assert.IsTrue(engine.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "잠든 채 보스가 다시 오면 다시 덕");
            Assert.AreEqual(AudioEngine.SleepBgmLowPassHz, engine.BgmLowPassTarget, 0.5f);
            Step(engine, 1f);
            Assert.AreEqual(bossTrack, engine.Music.CurrentKey, "보스 곡으로 교차");
            Assert.AreEqual(-9f, engine.BusDuckDb(AudioBus.Bgm), 0.01f);

            engine.SetSleep(false);
            Step(engine, 0.5f);
            Assert.IsFalse(engine.BusHasDuck(AudioBus.Bgm, DuckReason.Sleep), "깨면 0.4초에 풀린다");
            Assert.IsFalse(engine.BusHasDuck(AudioBus.Sfx, DuckReason.Sleep));
            Assert.AreEqual(LowPassStack.Open, engine.BgmLowPassTarget, 0.5f);
            engine.SetBossMusic(false);
            engine.StopAll();
        }

        /// <summary>
        /// 불 밝히기 합계 판 (AudioService.OnLampSumShown, WP-5B 요청 · RM-2): 깨어 있고 전장이 보이면 lamp_sum을 한 번 울려 본다
        /// (빈 라이브러리라 '클립 없음'으로 세어진다). 잠자기 중 · 전장이 가려짐(Hidden) · 엔진 없음이면 아무것도 시도하지 않는다
        /// </summary>
        [Test]
        public void LampSumShown_ChimesOnlyWhileAwakeAndTheFieldIsVisible()
        {
            // ApplySettings는 이 기기의 실제 설정(PlayerPrefs)을 읽는다: 효과음을 꺼 두었으면 시도 자체가 없는 게 맞다
            if (!GameSettings.SoundOn || GameSettings.SfxVolume <= 0f) Assert.Ignore("이 기기의 게임 설정에서 효과음이 꺼져 있어요");
            var engine = NewEngine();
            AudioService.Attach(engine);
            try
            {
                int Tried() => engine.Stats.Committed + engine.Stats.Missing + engine.Stats.GateBlocks;

                int before = Tried();
                AudioService.OnLampSumShown();
                Assert.AreEqual(before + 1, Tried(), "깨어 있고 전장이 보이면 한 번 시도");

                AudioService.SetSleep(true);
                before = Tried();
                AudioService.OnLampSumShown();
                Assert.AreEqual(before, Tried(), "잠자기 중에는 조용");
                AudioService.SetSleep(false);

                AudioService.SetBattleFocus(BattleAudioFocus.Hidden, 77);
                before = Tried();
                AudioService.OnLampSumShown();
                Assert.AreEqual(before, Tried(), "전장이 가려지면 조용");
                AudioService.ClearBattleFocus(77);

                AudioService.SetBattleFocus(BattleAudioFocus.Dimmed, 78);
                before = Tried();
                AudioService.OnLampSumShown();
                Assert.AreEqual(before + 1, Tried(), "시트가 덮었을 뿐이면 (Dimmed) 울린다 — 전투 버스 덕이 줄여 준다");
                AudioService.ClearBattleFocus(78);
            }
            finally
            {
                AudioService.Detach(engine);
            }

            Assert.IsNull(AudioService.Engine);
            Assert.DoesNotThrow(AudioService.OnLampSumShown, "엔진이 없으면 조용히 지나간다");
        }
    }
}
