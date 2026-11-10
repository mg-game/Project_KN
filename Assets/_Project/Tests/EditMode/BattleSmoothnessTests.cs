using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// perf PF-2: 고정 진행(1/60초) 사이 그리기 보간 (BattleSimulator.RenderAlpha · PrevPosition · PrevAttackAnim) ·
    /// 멈칫 · 느린 화면에서 뷰가 붙잡는 배율 (EnemyFxCatalog.HoldScale). 시뮬레이터 판정 · 결정성은 건드리지 않는다
    /// </summary>
    public class BattleSmoothnessTests : SystemsTestBase
    {
        const float Step = BattleSimulator.StepSeconds;

        static CharacterStats Walker() => new CharacterStats
        {
            style = CombatStyle.Assault,
            attack = 10f,
            hp = 100f,
            attackInterval = 1f,
            range = 1.6f,
            arcDegrees = 72f,
            moveSpeed = 0.8f,
            summonCost = 5,
            lifetime = 30f,
        };

        /// <summary>적 없이 잔향이 하나를 부를 수 있는 전투 (순찰 반경을 넓혀 0.5초 넘게 곧게 걷는다)</summary>
        BattleSimulator StartQuiet()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            BattleCfg.echoPatrolRadius = 3f;
            var setup = new BattleSetup { Mode = BattleMode.Stage, Level = 1, ShopMaxHp = 1e6f };
            setup.Deck.Add(new UnitSetup { FormId = "w", LineageId = "w", Stats = Walker() });
            var sim = new BattleSimulator(BattleCfg, 1);
            sim.Start(setup);
            return sim;
        }

        static Vector2 Drawn(BattleSimulator sim, EchoState echo) => Vector2.LerpUnclamped(echo.PrevPosition, echo.Position, sim.RenderAlpha);

        [Test]
        public void RenderAlpha_IsLeftoverTimeOverStep()
        {
            var sim = StartQuiet();
            Assert.AreEqual(0f, sim.RenderAlpha, 1e-5f, "시작 직후 누적 0");
            sim.Tick(Step * 1.5f);
            Assert.AreEqual(1, sim.StepCount);
            Assert.AreEqual(0.5f, sim.RenderAlpha, 1e-3f);
            sim.Tick(Step * 0.25f);
            Assert.AreEqual(1, sim.StepCount, "모자라면 진행하지 않는다");
            Assert.AreEqual(0.75f, sim.RenderAlpha, 1e-3f);
            sim.Tick(Step * 0.5f);
            Assert.AreEqual(2, sim.StepCount);
            Assert.AreEqual(0.25f, sim.RenderAlpha, 1e-3f);
        }

        [Test]
        public void RenderAlpha_IsOne_WhenNotRunning()
        {
            Assert.AreEqual(1f, new BattleSimulator(BattleCfg, 1).RenderAlpha, "시작 전 = 마지막 자리 그대로");

            var setup = new BattleSetup { Mode = BattleMode.Stage, Level = 1, ShopMaxHp = 1f };
            setup.Deck.Add(new UnitSetup { FormId = "idle", LineageId = "idle", Stats = Walker() });
            var sim = new BattleSimulator(BattleCfg, 1);
            sim.Start(setup);
            while (sim.Outcome == BattleOutcome.Running && sim.ElapsedTime < 120f) sim.Tick(0.25f);
            Assert.AreEqual(BattleOutcome.Failed, sim.Outcome);
            Assert.AreEqual(1f, sim.RenderAlpha, "결과 뒤에는 한 칸 뒤로 되돌아가 그리지 않는다");
        }

        [Test]
        public void PrevPosition_EqualsPosition_AtSummon()
        {
            var sim = StartQuiet();
            Assert.IsTrue(sim.TrySummon(0, BattleGeometry.SlotAngle(1)));
            var echo = sim.Echoes[0];
            Assert.AreEqual(echo.Position, echo.PrevPosition);
            Assert.AreEqual(echo.Position, Drawn(sim, echo), "부른 프레임에 제자리에서 나타난다");
        }

        [Test]
        public void PrevPosition_IsPositionBeforeLastStep()
        {
            var sim = StartQuiet();
            Assert.IsTrue(sim.TrySummon(0, BattleGeometry.SlotAngle(1)));
            var echo = sim.Echoes[0];
            for (int i = 0; i < 30; i++) sim.Tick(Step * 0.5f);

            int start = sim.StepCount;
            Vector2 before;
            do
            {
                before = echo.Position;
                sim.Tick(Step * 0.5f);
            } while (sim.StepCount == start);

            Assert.AreEqual(start + 1, sim.StepCount, "반 칸씩 넣으면 한 번에 한 진행");
            Assert.AreEqual(before, echo.PrevPosition);
            Assert.AreNotEqual(before, echo.Position, "걷는 중이어야 한다");
        }

        [Test]
        public void PrevPosition_EqualsPosition_WhenEnemySpawns()
        {
            BattleCfg.firstWaveDelay = 0f;
            var setup = new BattleSetup { Mode = BattleMode.Stage, Level = 1, ShopMaxHp = 1e6f };
            // 덱이나 지킴이가 하나도 없으면 적이 나오지 않는다 (BattleSimulator._spawningEnabled) — 부르지 않는 덱 하나
            setup.Deck.Add(new UnitSetup { FormId = "w", LineageId = "w", Stats = Walker() });
            var sim = new BattleSimulator(BattleCfg, 3);
            int seen = 0, mismatched = 0;
            sim.Event += e =>
            {
                if (e.Type != BattleEventType.EnemySpawned) return;
                var enemies = sim.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var s = enemies[i];
                    if (s.Id != e.SourceId) continue;
                    seen++;
                    if (s.PrevPosition != s.Position || s.PrevAttackAnim != s.AttackAnim) mismatched++;
                }
            };
            sim.Start(setup);
            for (int i = 0; i < 400 && seen < 3; i++) sim.Tick(0.05f);
            Assert.Greater(seen, 0, "적이 나와야 한다");
            Assert.AreEqual(0, mismatched, "등장한 진행에서는 이전 자리 = 자리 (뒤에서 미끄러져 들어오지 않는다)");
        }

        /// <summary>F1의 핵심: 144 Hz로 그리면 시뮬레이터 자리는 절반 넘는 프레임에서 그대로지만, 보간한 자리는 매 프레임 같은 거리만큼 움직인다</summary>
        [Test]
        public void Interpolation_MovesEveryFrame_At144Hz()
        {
            var sim = StartQuiet();
            Assert.IsTrue(sim.TrySummon(0, BattleGeometry.SlotAngle(1)));
            var echo = sim.Echoes[0];
            const float frame = 1f / 144f;
            var lastRaw = echo.Position;
            var lastDrawn = Drawn(sim, echo);
            int rawStill = 0, drawnStill = 0, counted = 0;
            float minMove = float.MaxValue, maxMove = 0f;
            for (int i = 0; i < 72; i++)
            {
                sim.Tick(frame);
                var raw = echo.Position;
                var drawn = Drawn(sim, echo);
                if (i >= 8)
                {
                    counted++;
                    if ((raw - lastRaw).sqrMagnitude < 1e-12f) rawStill++;
                    float move = (drawn - lastDrawn).magnitude;
                    if (move < 1e-6f) drawnStill++;
                    minMove = Mathf.Min(minMove, move);
                    maxMove = Mathf.Max(maxMove, move);
                }
                lastRaw = raw;
                lastDrawn = drawn;
            }
            Assert.Greater(rawStill, counted / 3, "보간 전: 60 Hz 진행을 144 Hz로 보면 많은 프레임이 그대로");
            Assert.AreEqual(0, drawnStill, "보간 뒤: 걷는 동안 멈춘 프레임이 없다");
            Assert.Less(maxMove / Mathf.Max(1e-6f, minMove), 1.5f, "프레임마다 움직인 거리가 고르다");
        }

        [TestCase(0.3f, 0.3f, 1f)]
        [TestCase(0.2f, 0.2f, 1f)]
        [TestCase(0f, 0.05f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(0.3f, 1f, 0.3f)]
        [TestCase(1f, 0.3f, 1f)]
        public void HoldScale_HoldsOnlyWhatTheSimDidNotSlow(float fx, float sim, float expected)
        {
            Assert.AreEqual(expected, EnemyFxCatalog.HoldScale(fx, sim), 1e-5f);
        }

        /// <summary>F2: 시뮬레이션이 이미 0.3으로 느려졌으면 뷰는 더 붙잡지 않는다 — 예전 경로는 0.3 × 0.3 = 0.09배로 두 번 느려졌다</summary>
        [Test]
        public void SlowMotion_IsAppliedOnce()
        {
            var simDelta = new Vector2(0.3f, 0f); // 시뮬레이션이 0.3배로 이번 프레임 걸은 거리 (보통 속도 1)
            float dt = 1f / 60f;
            var held = EnemyFxCatalog.HoldOffset(Vector2.zero, simDelta, EnemyFxCatalog.HoldScale(0.3f, 0.3f), dt);
            Assert.AreEqual(0.3f, (simDelta + held).x, 1e-5f, "보이는 걸음 = 0.3배 한 번");
            var doubled = EnemyFxCatalog.HoldOffset(Vector2.zero, simDelta, 0.3f, dt);
            Assert.AreEqual(0.09f, (simDelta + doubled).x, 1e-5f, "예전: 0.09배 (두 번)");
        }
    }
}
