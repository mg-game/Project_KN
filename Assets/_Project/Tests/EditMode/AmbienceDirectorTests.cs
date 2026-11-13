using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 환경음 감독 (AUDIO-SPEC §3.12): 밤 바닥 + 스테이지 기믹 층 (A/B 2초 교차), 보스 층, 소환 · 이야기 화면 −6 dB, 정전 '쉿' (바닥 −6 dB),
    /// 던전 바닥, 12~30초마다 지금 층 묶음의 한 방 (징글 동안 쉼), 두 스테이지 안 쓴 기믹 클립은 내린다.
    /// </summary>
    public class AmbienceDirectorTests
    {
        sealed class FakeAmb : IAmbienceOutput
        {
            public readonly HashSet<string> Available = new()
            {
                "amb_night", "amb_rain", "amb_wind", "amb_blackout", "amb_dungeon", "amb_boss", "amb_emit_night", "amb_emit_rain",
            };
            public readonly string[] Layers = new string[4];
            public readonly float[] Levels = new float[4];
            public readonly List<(string key, float db, float pan)> Emits = new();
            public readonly List<(string key, bool loaded)> Loads = new();

            static string Name(AudioId id) => id.IsValid ? AudioCatalog.Keys[id.Index].Key : null;
            public int TakeCount(AudioId key) => Available.Contains(Name(key) ?? string.Empty) ? 1 : 0;
            public void Play(int layer, AudioId key, int take, float startShare) => Layers[layer] = Name(key);
            public void Stop(int layer) => Layers[layer] = null;
            public void SetLevel(int layer, float level) => Levels[layer] = level;
            public bool IsPlaying(int layer) => Layers[layer] != null;
            public void Emit(AudioId key, float gainDb, float pan) => Emits.Add((Name(key), gainDb, pan));
            public void SetLoaded(AudioId key, bool loaded) => Loads.Add((Name(key), loaded));
        }

        FakeAmb _out;
        AmbienceDirector _amb;
        double _now;

        [SetUp]
        public void SetUp()
        {
            _out = new FakeAmb();
            _amb = new AmbienceDirector(_out, () => 0.5f);
            _now = 0;
            _amb.SetActive(true, 0);
        }

        void Step(double seconds, float dt = 0.1f)
        {
            double end = _now + seconds;
            while (_now < end - 1e-9)
            {
                _now += dt;
                _amb.Update(_now, dt);
            }
        }

        int LayerOf(string key)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_out.Layers[i] == key) return i;
            }
            return -1;
        }

        [Test]
        public void NightBed_PlaysAndFadesIn()
        {
            Step(1.5);
            Assert.AreEqual("amb_night", _out.Layers[AmbienceDirector.LayerBase]);
            Assert.AreEqual(1f, _out.Levels[AmbienceDirector.LayerBase], 1e-3f);
        }

        [Test]
        public void Modifier_CrossfadesOnTheOtherLayer()
        {
            Step(1.5);
            _amb.SetModifier(StageModifierType.Rain, true);
            int rain = LayerOf("amb_rain");
            Assert.IsTrue(rain == AmbienceDirector.LayerModA || rain == AmbienceDirector.LayerModB);
            Step(1.0);
            Assert.That(_out.Levels[rain], Is.InRange(0.3f, 0.7f), "2초 교차의 가운데");
            Step(1.5);
            Assert.AreEqual(1f, _out.Levels[rain], 1e-3f);
            _amb.SetModifier(StageModifierType.Wind, true);
            int wind = LayerOf("amb_wind");
            Assert.AreNotEqual(rain, wind, "다른 층에서 들어온다");
            Step(2.5);
            Assert.AreEqual(1f, _out.Levels[wind], 1e-3f);
            Assert.AreEqual(-1, LayerOf("amb_rain"), "빠진 층은 멈춘다");
            _amb.SetModifier(StageModifierType.None, true);
            Step(2.5);
            Assert.AreEqual(-1, LayerOf("amb_wind"), "기믹 없음 = 조용");
        }

        [Test]
        public void Blackout_HushesTheBed_AndMenusDuckIt()
        {
            Step(1.5);
            _amb.SetModifier(StageModifierType.Blackout, true);
            Assert.IsTrue(_amb.Hush);
            Step(1.5);
            Assert.AreEqual(AudioGate.DbToGain(-6f), _out.Levels[AmbienceDirector.LayerBase], 0.02f, "정전: 바닥 −6 dB");
            _amb.SetModifier(StageModifierType.None, true);
            _amb.SetMusicState(MusicState.Gacha);
            Step(1.5);
            Assert.AreEqual(AudioGate.DbToGain(-6f), _out.Levels[AmbienceDirector.LayerBase], 0.02f, "소환 화면 아래 −6 dB");
        }

        [Test]
        public void Dungeon_SwapsTheBed_AndBossAddsALayer()
        {
            Step(1.5);
            _amb.SetMusicState(MusicState.Dungeon);
            Step(2.5);
            Assert.AreEqual("amb_dungeon", _out.Layers[AmbienceDirector.LayerBase]);
            _amb.SetMusicState(MusicState.Main);
            _amb.SetBossAlive(true);
            Step(2.5);
            Assert.AreEqual("amb_night", _out.Layers[AmbienceDirector.LayerBase]);
            Assert.AreEqual("amb_boss", _out.Layers[AmbienceDirector.LayerBoss]);
            _amb.SetBossAlive(false);
            Step(2.0);
            Assert.IsNull(_out.Layers[AmbienceDirector.LayerBoss], "보스가 없으면 1.5초에 빠지고 멈춘다");
        }

        [Test]
        public void Emitters_UseTheCurrentLayersSet_AndRestDuringJingles()
        {
            _amb.SetModifier(StageModifierType.Rain, true);
            Step(AmbienceDirector.EmitMax + 1f);
            Assert.GreaterOrEqual(_out.Emits.Count, 1);
            var emit = _out.Emits[0];
            Assert.AreEqual("amb_emit_rain", emit.key);
            Assert.That(emit.db, Is.InRange(AmbienceDirector.EmitGainMin, AmbienceDirector.EmitGainMax));
            Assert.That(emit.pan, Is.InRange(-AmbienceDirector.EmitPan, AmbienceDirector.EmitPan));
            int before = _out.Emits.Count;
            _amb.SuppressEmitters(_now + 100);
            Step(60);
            Assert.AreEqual(before, _out.Emits.Count, "징글 · 보스 처치 순간에는 쉰다");
            _amb.SetModifier(StageModifierType.Festival, true);
            _amb.SuppressEmitters(0);
            Step(120);
            Assert.AreEqual("amb_emit_night", _out.Emits[_out.Emits.Count - 1].key, "축제 한 방이 없으면 밤 묶음으로");
        }

        [Test]
        public void UnusedModifierClips_AreUnloadedAfterTwoStages()
        {
            _amb.SetModifier(StageModifierType.Rain, true);
            Assert.Contains(("amb_rain", true), _out.Loads);
            _amb.SetModifier(StageModifierType.None, true);
            Step(3.0);
            _amb.SetModifier(StageModifierType.None, true);
            _amb.SetModifier(StageModifierType.None, true);
            Assert.Contains(("amb_rain", false), _out.Loads, "두 스테이지 안 쓰면 내린다");
        }
    }
}
