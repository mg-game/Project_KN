using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 엔진 스모크 (실제 AudioSource · 메모리 클립): 소스 31개를 만들고, UI · 전투 예약 · 군중 묶음 · 징글 교체 · 반복 · 잠 · 집중 · 백그라운드 ·
    /// 음악 · 환경음을 여러 프레임 돌려도 예외가 없다. 실제 소리는 듣지 않는다 (배치 모드는 출력이 없을 수 있다).
    /// </summary>
    public class AudioEngineSmokeTests
    {
        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        AudioClip Clip(string name, float seconds, int channels = 1)
        {
            int samples = Mathf.Max(64, (int)(seconds * 44100));
            var clip = AudioClip.Create(name, samples, channels, 44100, false);
            var data = new float[samples * channels];
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(i * 0.05f) * 0.1f;
            clip.SetData(data, 0);
            _created.Add(clip);
            return clip;
        }

        AudioLibrary Library()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            _created.Add(library);
            library.catalogVersion = AudioCatalog.Version;
            var entries = new AudioLibrary.Entry[AudioCatalog.Count];
            for (int i = 0; i < entries.Length; i++) entries[i] = new AudioLibrary.Entry { key = AudioCatalog.Keys[i].Key };
            void Put(string key, float seconds, int channels = 1, float root = 0f, int takes = 2)
            {
                var id = AudioCatalog.Find(key);
                var list = new AudioLibrary.Take[takes];
                for (int t = 0; t < takes; t++) list[t] = new AudioLibrary.Take { clip = Clip(key + t, seconds, channels), number = (byte)(t + 1), rootHz = root };
                entries[id.Index].takes = list;
            }
            Put("ui_tap", 0.06f);
            Put("ui_count_tick", 0.06f, 1, 1396.9f);
            Put("atk_needle", 0.22f, 1, 0f, 4);
            Put("hit_multi", 0.3f);
            Put("hit_lamp", 0.35f, 1, 698.24f);
            Put("shop_heartbeat", 1f, 1, 0f, 1);
            Put("bgm_battle_night", 3f, 2, 0f, 3);
            Put("amb_night", 2f, 2, 0f, 1);
            Put("amb_emit_night", 0.5f);
            Put("jingle_stage_clear", 0.5f, 2, 0f, 1);
            Put("jingle_stage_fail", 0.5f, 2, 0f, 1);
            entries[AudioCatalog.Find("bgm_battle_night").Index].takeMusic = new[]
            {
                new AudioLibrary.MusicInfo { bpm = 92f, beatsPerBar = 4 }, new AudioLibrary.MusicInfo { bpm = 132f, beatsPerBar = 3 },
                new AudioLibrary.MusicInfo { bpm = 100f, beatsPerBar = 4 },
            };
            library.entries = entries;
            library.Invalidate();
            return library;
        }

        static PendingSound Battle(string key, bool hit = false, float importance = 0f) => new()
        {
            Key = AudioCatalog.Find(key).Index,
            Volume = 1f,
            Pitch = 1f,
            Class = AudioCatalog.Get(AudioCatalog.Find(key)).Class,
            EnemyId = -1,
            Importance = importance,
            Category = hit ? AudioCategory.Hit : AudioCategory.None,
            Flags = AudioPlayFlags.Battle | AudioPlayFlags.Predicted | (hit ? AudioPlayFlags.Hit : AudioPlayFlags.None),
        };

        [Test]
        public void Engine_RunsEveryPath_WithoutExceptions()
        {
            // ApplySettings는 이 기기의 실제 설정(PlayerPrefs — 에디터와 같이 씀)을 읽는다: 소리를 꺼 두었으면 UI 소리가 안 나는 게 맞다
            if (!GameSettings.SoundOn || GameSettings.SfxVolume <= 0f) Assert.Ignore("이 기기의 게임 설정에서 효과음이 꺼져 있어요");
            var go = new GameObject("[AudioSmoke]");
            _created.Add(go);
            var engine = new AudioEngine(go, Library(), null);
            Assert.AreEqual(AudioEngine.VoiceCount, go.GetComponentsInChildren<AudioSource>(true).Length, "소스 31개 (실제 목소리 32개 안)");
            engine.ApplySettings();

            Assert.IsTrue(engine.PlayUi(UISfxKey.Tap, 0.5f));
            Assert.IsFalse(engine.PlayUi(UISfxKey.Tap, 0.5f), "같은 소리 40ms 안");
            Assert.IsTrue(engine.PlayUi(UISfxKey.CountTick, 1f), "조율 UI 키");
            Assert.IsFalse(engine.PlayUi(UISfxKey.GachaRare, 1f), "클립이 없으면 조용");

            // 같은 60ms 창의 명중 다섯 → 군중 묶음
            for (int i = 0; i < 5; i++) Assert.IsTrue(engine.Schedule(Battle("atk_needle", true, i), 0.05 + i * 0.01));
            var lamp = Battle("hit_lamp");
            lamp.TargetHz = 523.25f;
            engine.Schedule(lamp, 0.02);
            engine.SetLoop(0, AudioCatalog.Find("shop_heartbeat"), 0.4f, 1.1f, 0.3f);
            engine.PlayJingle(AudioCatalog.Find("jingle_stage_clear"));
            engine.PlayJingle(AudioCatalog.Find("jingle_stage_fail"));
            engine.Duck(AudioBus.Bgm, -8f, 0.5f, 0.05f, 0.4f, DuckReason.LampLit);
            engine.SetModifier(StageModifierType.Blackout, true);
            engine.SetBossAlive(true);
            engine.SetBattleSpeed(2);

            for (int frame = 0; frame < 120; frame++)
            {
                engine.SetFxTimeScale(frame < 10 ? 0f : 1f);
                engine.Tick(1f / 60f, 1f / 60f);
                if (frame == 30) engine.SetSleep(true);
                if (frame == 40) engine.SetBattleFocus(BattleAudioFocus.Dimmed, 1);
                if (frame == 50) engine.SetBattleFocus(BattleAudioFocus.Hidden, 2);
                if (frame == 60)
                {
                    engine.ClearBattleFocus(2);
                    engine.ClearBattleFocus(1);
                    engine.SetSleep(false);
                    engine.PushMusic(MusicState.Gacha, 5);
                }
                if (frame == 70) engine.OnPause(true);
                if (frame == 80) engine.OnPause(false);
                if (frame == 90) engine.PopMusic(5);
            }
            Assert.Greater(engine.Stats.Committed, 0, "예약이 소리로 넘어갔다");
            Assert.Greater(engine.Stats.Folded, 0, "60ms 창에 다섯 → 묶음");
            Assert.IsFalse(engine.IsAsleep);
            Assert.AreEqual(BattleAudioFocus.Full, engine.Focus);

            engine.ResetForScene();
            engine.StopAll();
            Assert.AreEqual(0, engine.Scheduler.Count);
        }

        [Test]
        public void DspClock_StaysMonotonic_WhenTheAudioDeviceRestartsIt()
        {
            double dspBase = 0;
            bool reset = false;
            Assert.AreEqual(100.0, AudioEngine.MonotonicDsp(100.0, 99.98, ref dspBase, ref reset), 1e-9);
            Assert.IsFalse(reset, "보통은 그대로");
            Assert.AreEqual(99.97, AudioEngine.MonotonicDsp(99.97, 100.0, ref dspBase, ref reset), 1e-9);
            Assert.IsFalse(reset, "50ms 안의 흔들림은 장치 바뀜이 아니다");

            // 이어폰 · 블루투스: Unity가 DSP 시계를 0부터 다시 센다 → 엔진 시계는 이어 간다
            double now = AudioEngine.MonotonicDsp(0.02, 100.0, ref dspBase, ref reset);
            Assert.IsTrue(reset);
            Assert.AreEqual(100.0, now, 1e-9);
            reset = false;
            Assert.AreEqual(100.5, AudioEngine.MonotonicDsp(0.52, now, ref dspBase, ref reset), 1e-9, "그 뒤로는 옮긴 만큼 더해 앞으로");
            Assert.IsFalse(reset);
        }

        [Test]
        public void Engine_AfterAnAudioConfigurationChange_KeepsLoopsMusicAndRules()
        {
            var go = new GameObject("[AudioSmokeReset]");
            _created.Add(go);
            // ApplySettings를 부르지 않는다 (기기 설정과 상관없이 기본 볼륨으로)
            var engine = new AudioEngine(go, Library(), null);
            engine.SetLoop(0, AudioCatalog.Find("shop_heartbeat"), 0.4f, 1.1f, 0.3f);
            for (int frame = 0; frame < 60; frame++) engine.Tick(1f / 60f, 1f / 60f);
            var music = engine.Music.State;

            engine.NotifyAudioConfigurationChanged();
            engine.Tick(1f / 60f, 1f / 60f);
            Assert.IsTrue(engine.IsLoopPlaying(0), "반복 소리는 이어 튼다");
            Assert.AreEqual(music, engine.Music.State, "음악 상태는 그대로");
            for (int frame = 0; frame < 30; frame++) engine.Tick(1f / 60f, 1f / 60f);
            Assert.IsTrue(engine.Schedule(Battle("atk_needle", true), 0.02), "예약도 그대로");

            engine.StopAll();
        }

        [Test]
        public void Engine_WithAnEmptyLibrary_StaysQuiet()
        {
            var go = new GameObject("[AudioSmokeEmpty]");
            _created.Add(go);
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            _created.Add(library);
            var engine = new AudioEngine(go, library, null);
            engine.ApplySettings();
            Assert.IsFalse(engine.PlayUi(UISfxKey.Claim, 1f));
            engine.Play(AudioCatalog.Find("atk_needle"), 1f, 0f, 1f);
            engine.PlayJingle(AudioCatalog.Find("jingle_stage_clear"));
            for (int frame = 0; frame < 60; frame++) engine.Tick(1f / 60f, 1f / 60f);
            Assert.AreEqual(0, engine.Stats.Committed);
            Assert.AreEqual(MusicState.Silent, engine.Music.State, "곡이 없으면 조용");
        }
    }
}
