using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 실제 BattleSystem 이벤트 → 실제 상태 어댑터(BattleAudioState) → BattleAudioLogic (§5): 90초 전투를 돌려도 예외가 없고,
    /// 쏘기 · 명중 · 처치 소리가 나오며, 모든 예약 시각이 0~2초 안이다 (투사체 비행 · 처치 보류 범위).
    /// </summary>
    public class BattleAudioIntegrationTests : SystemsTestBase
    {
        sealed class RecordingSink : IBattleAudioSink
        {
            public readonly List<(string key, double delay, PendingSound sound)> Scheduled = new();
            public double FxNow { get; set; }
            public double UiNow { get; set; }
            public bool Has(AudioId id) => id.IsValid;
            public void Schedule(PendingSound sound, double delay) => Scheduled.Add((AudioCatalog.Keys[sound.Key].Key, delay, sound));
            public void SetLoop(int slot, AudioId id, float volume, float pitch, float fade) { }
            public void StopLoop(int slot, float fade) { }
            public void Duck(AudioBus bus, float gainDb, float hold, float attack, float release, int reason, int exempt, double delay) { }
            public void SetBossMusic(bool chapterBossAlive) { }
            public void SetBossAlive(bool alive) { }
            public void SetModifier(StageModifierType modifier, bool stageChanged) { }
            public void FlushBattle() { }
            public void SuppressEmitters(double seconds) { }
            public bool IsAsleep => false;
            public bool IsJinglePlaying => false;
            public double JingleStartedAt => 0;
            public AudioId CurrentJingle => AudioId.None;
            public int Transpose => 0;

            public int Count(string prefix)
            {
                int n = 0;
                foreach (var s in Scheduled)
                {
                    if (s.key.StartsWith(prefix)) n++;
                }
                return n;
            }
        }

        [Test]
        public void RealBattleEvents_ScheduleSoundsAtSaneTimes()
        {
            foreach (var enemy in BattleCfg.enemies)
            {
                enemy.hp = 4f;
                enemy.damage = 0.2f;
            }
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");
            Formation.TryPlace(CharacterRole.Guardian, 3, "button_base");

            var state = new BattleAudioState();
            state.Bind(Battle);
            var sink = new RecordingSink();
            var logic = new BattleAudioLogic(state, sink, () => 0.4f);
            int frame = 1;
            Battle.Event += e => logic.OnEvent(in e, frame);
            Battle.StageChanged += () => logic.OnStageChanged(frame);
            Battle.StageEnded += (outcome, _) => logic.OnStageEnded(outcome);
            Battle.StepBack += (a, b) => logic.OnStepBack();

            Assert.DoesNotThrow(() =>
            {
                for (float t = 0f; t < 90f; t += 0.1f)
                {
                    Battle.Tick(0.1f);
                    logic.Poll(0.1f, frame);
                    frame++;
                    sink.FxNow += 0.1;
                    sink.UiNow += 0.1;
                }
            });

            Assert.IsTrue(state.SimReady, "이벤트를 받은 뒤에만 시뮬레이터를 읽는다");
            Assert.Greater(sink.Count("fire_"), 0, "쏘는 소리");
            Assert.Greater(sink.Count("atk_"), 0, "명중 소리");
            Assert.Greater(sink.Count("death_") + sink.Count("boss_defeat"), 0, "처치 소리");
            Assert.Greater(sink.Count("enemy_spawn"), 0, "등장 소리");
            foreach (var s in sink.Scheduled)
            {
                Assert.That(s.delay, Is.InRange(0.0, 2.0), s.key + " 예약 시각");
                Assert.IsTrue(s.sound.Has(AudioPlayFlags.Battle), s.key + " 전투 표시");
                Assert.AreEqual(AudioCatalog.Get(new AudioId(s.sound.Key)).Key, s.key);
            }
            bool flight = false;
            foreach (var s in sink.Scheduled)
            {
                if (s.key.StartsWith("atk_") && s.delay > 0.04) flight = true;
            }
            Assert.IsTrue(flight, "명중은 투사체가 닿는 순간 (지연 > 0)");
        }
    }
}
