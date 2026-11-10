using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    public class BattleSimulatorTests : SystemsTestBase
    {
        static CharacterStats Stats(CombatStyle style, float attack = 10f, float hp = 100f, float interval = 1f,
            float range = 1.6f, float arc = 72f, int cost = 5, float lifetime = 12f) => new CharacterStats
        {
            style = style,
            attack = attack,
            hp = hp,
            attackInterval = interval,
            range = range,
            arcDegrees = arc,
            moveSpeed = 0.8f,
            summonCost = cost,
            lifetime = lifetime,
        };

        static UnitSetup Unit(string id, CharacterStats stats) => new UnitSetup { FormId = id, LineageId = id, Stats = stats };

        static BattleSetup Setup(float shopHp = 1e6f)
        {
            return new BattleSetup { Mode = BattleMode.Stage, Level = 1, ShopMaxHp = shopHp };
        }

        BattleSimulator Start(BattleSetup setup, int seed = 1)
        {
            var sim = new BattleSimulator(BattleCfg, seed);
            sim.Start(setup);
            return sim;
        }

        /// <summary>Tick은 한 번에 최대 MaxTickSeconds까지만 진행하므로 잘게 나눠 부른다</summary>
        static void Advance(BattleSimulator sim, float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += 0.25f) sim.Tick(Mathf.Min(0.25f, seconds - t));
        }

        static void RunUntilEnd(BattleSimulator sim, float maxSeconds)
        {
            while (sim.Outcome == BattleOutcome.Running && sim.ElapsedTime < maxSeconds) sim.Tick(0.25f);
        }

        /// <summary>적이 오래 나오지 않게 해서 잔향/게이지만 시험한다</summary>
        void NoEnemies()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
        }

        [Test]
        public void SameSeed_ProducesSameBattle()
        {
            List<string> Run(int seed)
            {
                var setup = Setup(400f);
                setup.Guardians[0] = Unit("g0", Stats(CombatStyle.Defense));
                setup.Guardians[2] = Unit("g2", Stats(CombatStyle.Snipe, attack: 14f));
                setup.Deck.Add(Unit("e0", Stats(CombatStyle.Assault, cost: 5)));
                setup.Deck.Add(Unit("e1", Stats(CombatStyle.Capture, cost: 6)));
                var log = new List<string>();
                var sim = new BattleSimulator(BattleCfg, seed) { AutoMode = true };
                sim.Event += e => log.Add($"{e.Type}:{e.SourceId}:{e.TargetId}:{e.Value:0.###}:{e.Position.x:0.###},{e.Position.y:0.###}");
                sim.Start(setup);
                for (int i = 0; i < 400 && sim.Outcome == BattleOutcome.Running; i++) sim.Tick(0.1f);
                log.Add($"end {sim.Outcome} {sim.Kills} {sim.ShopHp:0.###} {sim.Resonance:0.###} {sim.ElapsedTime:0.###}");
                return log;
            }

            var a = Run(42);
            var b = Run(42);
            CollectionAssert.AreEqual(a, b);
            Assert.Greater(a.Count, 50, "전투가 실제로 진행돼야 한다");
            CollectionAssert.AreNotEqual(a, Run(43), "다른 시드면 다른 전투");
        }

        [Test]
        public void Guardian_OnlyHitsInsideItsFan()
        {
            var setup = Setup();
            var stats = Stats(CombatStyle.Defense, attack: 5f);
            setup.Guardians[0] = Unit("g0", stats);
            var sim = Start(setup, 7);

            int attacks = 0;
            int otherLaneSpawns = 0;
            float halfArc = stats.arcDegrees * 0.5f * Mathf.Deg2Rad + 0.001f;
            sim.Event += e =>
            {
                if (e.Type == BattleEventType.EnemySpawned)
                {
                    float spawnAngle = Mathf.Atan2(e.Position.y, e.Position.x);
                    if (BattleGeometry.NearestSlot(spawnAngle) != 0) otherLaneSpawns++;
                }
                if (e.Type != BattleEventType.GuardianAttack) return;
                attacks++;
                Assert.AreEqual(0, e.SourceId);
                float dist = e.Position.magnitude;
                if (dist <= BattleCfg.shopGuardRadius + 0.001f) return; // 수선점에 붙은 적은 모든 지킴이가 공격
                Assert.LessOrEqual(dist, stats.range + 0.001f, "요격 반경 밖");
                float angle = Mathf.Atan2(e.Position.y, e.Position.x);
                Assert.LessOrEqual(Mathf.Abs(BattleGeometry.DeltaAngle(angle, BattleGeometry.SlotAngle(0))), halfArc, "부채꼴 밖");
            };

            for (int i = 0; i < 600; i++) sim.Tick(0.1f);

            Assert.Greater(attacks, 0);
            Assert.Greater(otherLaneSpawns, 0, "다른 방향에서도 적이 나와야 의미 있는 시험");
        }

        [Test]
        public void Summon_SpendsResonance_AndFailsWithoutSpendingWhenInsufficient()
        {
            NoEnemies();
            BattleCfg.startResonance = 10f;
            var setup = Setup();
            setup.Deck.Add(Unit("cheap", Stats(CombatStyle.Assault, cost: 5)));
            setup.Deck.Add(Unit("pricey", Stats(CombatStyle.Assault, cost: 50)));
            setup.Deck.Add(null);
            var sim = Start(setup);
            int summoned = 0;
            sim.Event += e => { if (e.Type == BattleEventType.EchoSummoned) summoned++; };

            Assert.AreEqual(10f, sim.Resonance, 1e-4);
            Assert.IsTrue(sim.TrySummon(0));
            Assert.AreEqual(5f, sim.Resonance, 1e-4);
            Assert.AreEqual(1, sim.Echoes.Count);
            Assert.AreEqual(1, summoned);
            Assert.IsFalse(sim.CanSummon(0), "재소환 대기 중");
            Assert.Greater(sim.Deck[0].Cooldown, 0f);

            Assert.IsFalse(sim.CanSummon(1));
            Assert.IsFalse(sim.TrySummon(1, 1f));
            Assert.IsFalse(sim.TrySummon(2), "빈 덱 칸");
            Assert.IsFalse(sim.TrySummon(-1));
            Assert.IsFalse(sim.TrySummon(99));
            Assert.AreEqual(5f, sim.Resonance, 1e-4, "실패한 소환은 잔향을 쓰지 않는다");
            Assert.AreEqual(1, sim.Echoes.Count);
            Assert.AreEqual(1, summoned);
        }

        [Test]
        public void Summon_WithAngle_MovesThatWay()
        {
            NoEnemies();
            var setup = Setup();
            setup.Deck.Add(Unit("e", Stats(CombatStyle.Assault)));
            var sim = Start(setup);
            float angle = BattleGeometry.SlotAngle(3);

            Assert.IsTrue(sim.TrySummon(0, angle));
            Advance(sim, 1f);

            var echo = sim.Echoes[0];
            Assert.Greater(echo.Position.magnitude, 0.5f);
            Assert.Less(Mathf.Abs(BattleGeometry.DeltaAngle(Mathf.Atan2(echo.Position.y, echo.Position.x), angle)), 0.05f);
        }

        [Test]
        public void EchoDissolve_RefundsPartOfCost_AndChargesLamp()
        {
            NoEnemies();
            BattleCfg.startResonance = 10f;
            BattleCfg.echoRefundRate = 0.3f;
            BattleCfg.lampPerEchoDissolve = 0.05f;
            var setup = Setup();
            setup.Deck.Add(Unit("e", Stats(CombatStyle.Assault, cost: 5, lifetime: 1f)));
            var sim = Start(setup);
            BattleEvent? dissolved = null;
            sim.Event += e => { if (e.Type == BattleEventType.EchoDissolved) dissolved = e; };

            Assert.IsTrue(sim.TrySummon(0, 0f));
            var state = sim.Echoes[0];
            Advance(sim, 1.5f);

            Assert.IsTrue(dissolved.HasValue);
            Assert.AreEqual(state.Id, dissolved.Value.SourceId);
            Assert.AreEqual(1.5f, dissolved.Value.Value, 1e-4);
            Assert.Greater(dissolved.Value.Position.magnitude, 0f, "사라진 위치 전달");
            Assert.IsFalse(state.Alive);
            Assert.AreEqual(0, sim.Echoes.Count);
            Assert.AreEqual(6.5f, sim.Resonance, 1e-3);
            Assert.AreEqual(0.05f, sim.LampGauge, 1e-4);
        }

        [Test]
        public void Lamp_RequiresFullGauge_AndHitsAllEnemies()
        {
            BattleCfg.firstWaveDelay = 0f;
            BattleCfg.lampPerEchoDissolve = 1f;
            BattleCfg.lampDamageRatio = 1f;
            var setup = Setup();
            setup.Deck.Add(Unit("e", Stats(CombatStyle.Assault, cost: 1, lifetime: 1f)));
            var sim = Start(setup);
            int lit = 0;
            sim.Event += e => { if (e.Type == BattleEventType.LampLit) lit++; };

            Assert.IsFalse(sim.CanLightLamp);
            Assert.IsFalse(sim.TryLightLamp());
            Assert.AreEqual(0, lit);

            // 적이 없는 방향(아래)으로 보내 금방 사라지게 한다
            Assert.IsTrue(sim.TrySummon(0, -Mathf.PI * 0.5f));
            Advance(sim, 1.5f);
            Assert.AreEqual(1f, sim.LampGauge, 1e-4);
            Assert.IsTrue(sim.CanLightLamp);
            Assert.Greater(sim.Enemies.Count, 0, "적이 나와 있어야 한다");
            int killsBefore = sim.Kills;
            int enemies = sim.Enemies.Count;

            Assert.IsTrue(sim.TryLightLamp());

            Assert.AreEqual(1, lit);
            Assert.AreEqual(0f, sim.LampGauge);
            Assert.IsFalse(sim.CanLightLamp);
            Assert.AreEqual(0, sim.Enemies.Count, "일반 적은 모두 쓰러진다");
            Assert.AreEqual(killsBefore + enemies, sim.Kills);
            Assert.AreEqual(0f, sim.LampGauge, "불 밝히기로 쓰러뜨린 적은 게이지를 채우지 않는다");
        }

        [Test]
        public void BossKill_ClearsStage()
        {
            BattleCfg.killsToBossBase = 1;
            BattleCfg.killsToBossPerStage = 0f;
            var setup = Setup();
            for (int slot = 0; slot < BattleGeometry.SlotCount; slot++)
                setup.Guardians[slot] = Unit("g" + slot, Stats(CombatStyle.Defense, attack: 100000f, interval: 0.1f));
            var sim = Start(setup);
            int bossEvents = 0, cleared = 0;
            sim.Event += e =>
            {
                if (e.Type == BattleEventType.BossSpawned) bossEvents++;
                if (e.Type == BattleEventType.StageCleared) cleared++;
            };

            RunUntilEnd(sim, 120f);

            Assert.AreEqual(BattleOutcome.Cleared, sim.Outcome);
            Assert.IsTrue(sim.BossSpawned);
            Assert.AreEqual(1, bossEvents);
            Assert.AreEqual(1, cleared);
            Assert.GreaterOrEqual(sim.Kills, 2);
            Assert.IsNull(sim.Boss);
        }

        [Test]
        public void ShopHpZero_FailsStage_AndStops()
        {
            var setup = Setup(1f);
            setup.Deck.Add(Unit("idle", Stats(CombatStyle.Assault))); // 덱만 있고 소환하지 않는다
            var sim = Start(setup);
            int failed = 0, shopHits = 0;
            sim.Event += e =>
            {
                if (e.Type == BattleEventType.StageFailed) failed++;
                if (e.Type == BattleEventType.ShopDamaged) shopHits++;
            };

            RunUntilEnd(sim, 120f);

            Assert.AreEqual(BattleOutcome.Failed, sim.Outcome);
            Assert.AreEqual(0f, sim.ShopHp);
            Assert.AreEqual(1, failed);
            Assert.AreEqual(1, shopHits);
            float time = sim.ElapsedTime;
            sim.Tick(1f);
            Assert.AreEqual(time, sim.ElapsedTime, "끝난 전투는 더 진행하지 않는다");
            Assert.IsFalse(sim.TrySummon(0));
        }

        [Test]
        public void EmptySetup_SpawnsNothing()
        {
            var sim = Start(Setup());
            for (int i = 0; i < 30; i++) sim.Tick(1f);

            Assert.IsFalse(sim.HasDefenders);
            Assert.AreEqual(BattleOutcome.Running, sim.Outcome);
            Assert.AreEqual(0, sim.Enemies.Count);
            Assert.AreEqual(sim.ShopMaxHp, sim.ShopHp);
        }

        [Test]
        public void BadInput_DoesNotBreakSimulation()
        {
            var sim = new BattleSimulator(BattleCfg, 3);
            Assert.DoesNotThrow(() => sim.Tick(1f));
            Assert.DoesNotThrow(() => sim.Start(null));

            var setup = new BattleSetup { Guardians = new UnitSetup[2], Deck = null, ShopMaxHp = float.NaN, AttackMultiplier = -1f };
            setup.Guardians[0] = Unit("zero", default); // 능력치 0 (공격 간격 0 등)
            sim.Start(setup);
            sim.AutoMode = true;
            for (int i = 0; i < 20; i++) sim.Tick(1f);
            sim.Tick(float.NaN);
            sim.Tick(-1f);

            Assert.Greater(sim.ShopMaxHp, 0f);
            Assert.IsFalse(float.IsNaN(sim.GetDangerAngle()));
            Assert.AreEqual(0f, sim.GetDanger(-1));
            Assert.AreEqual(0f, sim.GetDanger(BattleGeometry.SlotCount));
        }

        [Test]
        public void AutoMode_SummonsTowardDanger()
        {
            var setup = Setup(300f);
            setup.Deck.Add(Unit("e0", Stats(CombatStyle.Assault, cost: 5)));
            var sim = new BattleSimulator(BattleCfg, 5) { AutoMode = true };
            int summons = 0;
            sim.Event += e => { if (e.Type == BattleEventType.EchoSummoned) summons++; };
            sim.Start(setup);

            for (int i = 0; i < 300; i++) sim.Tick(0.1f);

            Assert.Greater(summons, 0);
        }

        [Test]
        public void RefreshUnits_KeepsBattleWhenFormationSame()
        {
            var setup = Setup();
            setup.Guardians[0] = Unit("g0", Stats(CombatStyle.Defense, attack: 10f));
            var sim = Start(setup);
            Advance(sim, 3f);
            float time = sim.ElapsedTime;

            var stronger = Setup(2e6f);
            stronger.Guardians[0] = Unit("g0", Stats(CombatStyle.Defense, attack: 20f));
            Assert.IsTrue(sim.RefreshUnits(stronger));
            Assert.AreEqual(20f, sim.Guardians[0].Stats.attack);
            Assert.AreEqual(2e6f, sim.ShopMaxHp);
            Assert.AreEqual(time, sim.ElapsedTime);

            var different = Setup();
            different.Guardians[1] = Unit("g1", Stats(CombatStyle.Defense));
            Assert.IsFalse(sim.RefreshUnits(different), "편성 구성이 다르면 다시 시작해야 한다");
        }

        // ───────────────────────────── 공격 연출 이벤트 (EnemyStrike / GuardianPulse) ─────────────────────────────

        /// <summary>
        /// 근접 방어 잔향이 하나를 적 하나 쪽으로 불러 발을 묶는다 (적은 멈춰서 잔향이를 때린다).
        /// brute는 strikeInterval이 비어 있어 기본 간격 enemyStrikeInterval(0.5초)을 쓴다
        /// </summary>
        BattleSimulator StartBlockedBrute(List<BattleEvent> log, List<float> strikeTimes, List<float> strikeAnims) =>
            StartBlocked(SimKit.Enemy("brute", hp: 1e6f, speed: 0.5f, damage: 10f), 100f, log, strikeTimes, strikeAnims);

        /// <summary>근접 방어 잔향이 하나(체력 echoHp)를 적 def 하나(보스면 바로 보스로 나온다) 쪽으로 불러 발을 묶는다</summary>
        BattleSimulator StartBlocked(EnemyDef def, float echoHp, List<BattleEvent> log, List<float> strikeTimes, List<float> strikeAnims)
        {
            SimKit.OnlyEnemies(BattleCfg, 1, def);
            BattleCfg.startResonance = 10f;
            BattleCfg.enemyStrikeInterval = 0.5f;
            var setup = Setup();
            setup.Deck.Add(SimKit.Unit("e", SimKit.Stats(CombatStyle.Defense, attack: 0f, hp: echoHp, range: 1.5f, lifetime: 100f)));
            var sim = new BattleSimulator(BattleCfg, 1);
            sim.Event += e =>
            {
                log.Add(e);
                if (e.Type != BattleEventType.EnemyStrike) return;
                strikeTimes.Add(sim.ElapsedTime);
                var enemy = SimKit.Find(sim, x => x.Id == e.SourceId);
                strikeAnims.Add(enemy != null ? enemy.AttackAnim : -1f);
            };
            sim.Start(setup);
            Advance(sim, 0.1f);
            var target = sim.Enemies[0];
            Assert.IsTrue(sim.TrySummon(0, Mathf.Atan2(target.Position.y, target.Position.x)));
            return sim;
        }

        [Test]
        public void EnemyStrike_OnEcho_OncePerIntervalPerEnemy_WithSummedDamage()
        {
            var log = new List<BattleEvent>();
            var times = new List<float>();
            var anims = new List<float>();
            var sim = StartBlockedBrute(log, times, anims);
            var echo = sim.Echoes[0];
            var brute = sim.Enemies[0];

            Advance(sim, 6f);

            var strikes = log.FindAll(e => e.Type == BattleEventType.EnemyStrike);
            Assert.GreaterOrEqual(strikes.Count, 6, "몇 초 동안 붙어 싸웠다");
            Assert.IsTrue(strikes.TrueForAll(e => e.SourceId == brute.Id && e.TargetId == echo.Id && e.Key == "brute"));
            for (int i = 1; i < times.Count; i++)
            {
                float gap = times[i] - times[i - 1];
                Assert.GreaterOrEqual(gap, 0.5f - 1e-3f, "적 하나당 0.5초에 한 번까지");
                Assert.LessOrEqual(gap, 0.5f + BattleSimulator.StepSeconds + 1e-3f, "붙어 있는 동안은 간격마다");
            }
            Assert.Less(strikes[0].Value, strikes[1].Value * 0.1f, "붙자마자 바로 한 번 (닿은 순간 분량)");
            Assert.AreEqual(strikes[1].Value, strikes[2].Value, strikes[1].Value * 0.01f, "그 뒤로는 0.5초 분량씩");

            float lost = echo.MaxHp - echo.Hp;
            float reported = 0f;
            foreach (var strike in strikes) reported += strike.Value;
            Assert.LessOrEqual(reported, lost + 0.05f, "이벤트 값 합계는 실제 피해를 넘지 않는다");
            Assert.GreaterOrEqual(reported, lost - strikes[1].Value - 0.05f, "남는 것은 마지막 간격 분량뿐");
            Assert.LessOrEqual(Vector2.Distance(strikes[strikes.Count - 1].Position, brute.Position), BattleCfg.echoBlockRadius,
                "적과 잔향이 사이에서 맞는다");
            Assert.IsTrue(anims.TrueForAll(a => a == 1f), "공격할 때 AttackAnim = 1");
            Assert.IsFalse(log.Exists(e => e.Type == BattleEventType.ShopDamaged));
        }

        [Test]
        public void DefaultEnemies_StrikeWithTheirOwnRhythm()
        {
            // ATK-03 적 표의 주기 — 적마다 공격 리듬이 달라야 무엇이 때리는지 읽힌다
            var expected = new Dictionary<string, float>
            {
                { "enemy_dust", 1f }, { "enemy_moth", 0.6f }, { "enemy_rust", 1.4f }, { "enemy_tangle", 0.9f }, { "enemy_wisp", 0.8f },
                { "enemy_thimble", 1.2f }, { "enemy_windup", 1f }, { "boss_shadow", 2f }, { "boss_mothqueen", 2f },
            };
            Assert.AreEqual(expected.Count, BattleCfg.enemies.Count);
            foreach (var def in BattleCfg.enemies)
            {
                Assert.IsTrue(expected.TryGetValue(def.id, out float interval), def.id + ": 새 적은 공격 리듬(strikeInterval)을 정해야 한다");
                Assert.AreEqual(interval, def.strikeInterval, 1e-4f, def.id);
            }
        }

        [TestCase("boss_shadow", 2f)]
        [TestCase("enemy_rust", 1.4f)]
        public void EnemyStrike_OnEcho_FollowsEachEnemysOwnInterval(string enemyId, float interval)
        {
            // 정식 적 정의 그대로 (오래 붙어 있게 체력만, 빨리 만나게 속도·등장 단계만 바꾼다)
            var def = BattleCfg.enemies.Find(x => x.id == enemyId);
            Assert.IsNotNull(def, enemyId);
            def.hp = 1e6f;
            def.speed = 0.5f;
            def.minStage = 1;
            var log = new List<BattleEvent>();
            var times = new List<float>();
            var sim = StartBlocked(def, 1e5f, log, times, new List<float>());
            var echo = sim.Echoes[0];
            var enemy = sim.Enemies[0];
            Assert.AreEqual(def.isBoss, enemy.IsBoss);

            Advance(sim, interval * 4f + 2f);

            var strikes = log.FindAll(e => e.Type == BattleEventType.EnemyStrike);
            Assert.GreaterOrEqual(strikes.Count, 3, "붙어 있는 동안 계속 때린다");
            Assert.IsTrue(strikes.TrueForAll(e => e.SourceId == enemy.Id && e.TargetId == echo.Id && e.Key == enemyId),
                "잔향이에게 발이 묶여 수선점까지 가지 못한다");
            for (int i = 1; i < times.Count; i++)
            {
                float gap = times[i] - times[i - 1];
                Assert.GreaterOrEqual(gap, interval - 1e-3f, $"{enemyId}: 기본 0.5초가 아니라 자기 주기 {interval}초에 한 번까지");
                Assert.LessOrEqual(gap, interval + BattleSimulator.StepSeconds + 1e-3f, "붙어 있는 동안은 자기 주기마다");
            }
        }

        [Test]
        public void EnemyStrike_DoesNotChangeTheBattle()
        {
            float HpAfter(float interval)
            {
                var log = new List<BattleEvent>();
                var sim = StartBlockedBrute(log, new List<float>(), new List<float>());
                BattleCfg.enemyStrikeInterval = interval;
                var echo = sim.Echoes[0];
                Advance(sim, 4f);
                return echo.Hp;
            }

            Assert.AreEqual(HpAfter(0.5f), HpAfter(2f), 1e-3, "이벤트 간격은 연출만 바꾼다");
        }

        [Test]
        public void EnemyStrike_OnShop_ComesFirst_EvenWhenShieldAbsorbsEverything()
        {
            var log = new List<BattleEvent>();
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 10f));
            var setup = Setup(100f);
            setup.Guardians[0] = SimKit.Unit("g", SimKit.Stats(CombatStyle.Assault, attack: 0f));
            var sim = SimKit.Start(BattleCfg, setup, log);

            Advance(sim, 2f);

            int strike = log.FindIndex(e => e.Type == BattleEventType.EnemyStrike);
            int damaged = log.FindIndex(e => e.Type == BattleEventType.ShopDamaged);
            Assert.GreaterOrEqual(strike, 0);
            Assert.Greater(damaged, strike, "공격 연출이 먼저");
            var e0 = log[strike];
            Assert.AreEqual(-1, e0.TargetId, "수선점");
            Assert.AreEqual(log[damaged].SourceId, e0.SourceId);
            Assert.AreEqual(10f, e0.Value, 1e-3);
            Assert.AreEqual("runner", e0.Key);
            Assert.AreEqual(BattleGeometry.ShopRadius, e0.Position.magnitude, 1e-3, "적이 들어온 방향의 수선점 가장자리");

            // 수선점 보호막이 다 막아도 공격 연출은 나온다
            log.Clear();
            var wall = SimKit.Skill("wall", SkillTrigger.OnBattleStart, SkillTargetType.Shop, SimKit.Fx(SkillEffectType.Shield, 10f, 100f));
            var shielded = Setup(100f);
            shielded.Guardians[0] = SimKit.Unit("g", SimKit.Stats(CombatStyle.Assault, attack: 10f, range: 0.1f), wall);
            sim = SimKit.Start(BattleCfg, shielded, log);
            Advance(sim, 2f);

            Assert.Greater(sim.ShopShield, 0f);
            Assert.AreEqual(100f, sim.ShopHp, 1e-3);
            Assert.AreEqual(0, SimKit.Count(log, BattleEventType.ShopDamaged));
            var strikes = SimKit.Of(log, BattleEventType.EnemyStrike);
            Assert.AreEqual(1, strikes.Count);
            Assert.AreEqual(-1, strikes[0].TargetId);
            Assert.AreEqual(10f, strikes[0].Value, 1e-3);
        }

        [Test]
        public void GuardianPulse_FiresEveryPulse_EvenAtFullShopHp_AndPrecedesItsHeal()
        {
            var log = new List<BattleEvent>();
            NoEnemies();
            var setup = Setup(100f);
            setup.Guardians[0] = SimKit.Unit("sup", SimKit.Stats(CombatStyle.Support, attack: 10f));
            setup.Guardians[2] = SimKit.Unit("med", SimKit.Stats(CombatStyle.Heal, attack: 10f));
            setup.Guardians[1] = SimKit.Unit("atk", SimKit.Stats(CombatStyle.Assault, attack: 10f));
            var sim = SimKit.Start(BattleCfg, setup, log);

            Advance(sim, 9f);

            var pulses = SimKit.Of(log, BattleEventType.GuardianPulse);
            var support = pulses.FindAll(e => e.SourceId == 0);
            var heal = pulses.FindAll(e => e.SourceId == 2);
            Assert.AreEqual(3, support.Count, "0.5 · 4.5 · 8.5초");
            Assert.AreEqual(3, heal.Count, "0.5 · 4 · 7.5초");
            Assert.AreEqual(support.Count + heal.Count, pulses.Count, "지원/힐 지킴이만");
            Assert.IsTrue(support.TrueForAll(e => e.Key == BattleEventKeys.PulseSupport && (int)e.Value == (int)CombatStyle.Support));
            Assert.IsTrue(heal.TrueForAll(e => e.Key == BattleEventKeys.PulseHeal && (int)e.Value == (int)CombatStyle.Heal));
            Assert.AreEqual("support", BattleEventKeys.PulseSupport);
            Assert.AreEqual("heal", BattleEventKeys.PulseHeal);
            Assert.AreEqual(BattleGeometry.GuardianPosition(2), heal[0].Position);
            Assert.AreEqual(-1, heal[0].TargetId);
            Assert.AreEqual(0, SimKit.Count(log, BattleEventType.ShopHealed), "수선점이 가득 차 있어도 맥박은 보인다");
            Assert.Greater(sim.Guardians[1].BuffTimer, 0f, "지원 강화(양옆 지킴이)는 상태로");
        }

        [Test]
        public void GuardianPulse_HealResultFollowsThePulse()
        {
            var log = new List<BattleEvent>();
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 40f));
            var setup = Setup(100f);
            setup.Guardians[0] = SimKit.Unit("sup", SimKit.Stats(CombatStyle.Support, attack: 2f, range: 0.1f));
            setup.Guardians[2] = SimKit.Unit("med", SimKit.Stats(CombatStyle.Heal, attack: 2f, range: 0.1f));
            var sim = SimKit.Start(BattleCfg, setup, log);

            Advance(sim, 9f);

            int healedFromGuardian = 0;
            for (int i = 0; i < log.Count; i++)
            {
                var e = log[i];
                if (e.Type != BattleEventType.ShopHealed || e.SourceId < 0) continue;
                healedFromGuardian++;
                Assert.Greater(i, 0);
                Assert.AreEqual(BattleEventType.GuardianPulse, log[i - 1].Type, "맥박 바로 뒤에 그 맥박의 회복");
                Assert.AreEqual(e.SourceId, log[i - 1].SourceId);
            }
            Assert.Greater(healedFromGuardian, 0);
            Assert.Less(sim.ShopHp, 100f + 1e-3f);
        }

        [Test]
        public void BossAtShop_StrikesEachSiege_WithWindupBeforeIt()
        {
            var log = new List<BattleEvent>();
            var boss = SimKit.Enemy("boss_big", hp: 1e6f, speed: 1f, damage: 5f);
            boss.isBoss = true;
            SimKit.OnlyEnemies(BattleCfg, 1, boss);
            BattleCfg.bossSiegeInterval = 2f;
            BattleCfg.bossSiegeWindupSeconds = 0.35f;
            var setup = Setup(1000f);
            setup.Guardians[0] = SimKit.Unit("g", SimKit.Stats(CombatStyle.Assault, attack: 0f));
            var sim = SimKit.Start(BattleCfg, setup, log);

            // 수선점에 붙자마자 첫 공격 (예비 동작 없음)
            for (int i = 0; i < 600 && SimKit.Count(log, BattleEventType.EnemyStrike) == 0; i++) sim.Tick(BattleSimulator.StepSeconds);
            Assert.AreEqual(1, SimKit.Count(log, BattleEventType.EnemyStrike));
            var state = sim.Boss;
            Assert.IsNotNull(state);
            Assert.AreEqual(0f, state.SiegeWindup);
            Assert.AreEqual(1f, state.AttackAnim, "공격할 때 AttackAnim = 1");

            sim.Tick(1f);
            Assert.AreEqual(0f, state.SiegeWindup, "다음 공격까지 1초 남음 → 아직");
            Assert.AreEqual(0f, state.AttackAnim, "0.25초 만에 0으로");
            SimKit.Advance(sim, 0.8f);
            Assert.AreEqual(1f - 0.2f / 0.35f, state.SiegeWindup, 0.06f, "남은 0.2초 / 예비 동작 0.35초");
            Assert.AreEqual(1, SimKit.Count(log, BattleEventType.EnemyStrike));

            SimKit.Advance(sim, 0.25f);
            var strikes = SimKit.Of(log, BattleEventType.EnemyStrike);
            Assert.AreEqual(2, strikes.Count, "포위 공격 간격마다");
            Assert.Less(state.SiegeWindup, 0.01f, "공격하면 다시 0부터");
            Assert.IsTrue(strikes.TrueForAll(e => e.TargetId == -1 && e.Key == "boss_big" && e.SourceId == state.Id));
            Assert.AreEqual(2, SimKit.Count(log, BattleEventType.ShopDamaged));
        }

        [Test]
        public void EchoDungeon_IgnoresGuardians_AndStartsWithMoreResonance()
        {
            var setup = new BattleSetup { Mode = BattleMode.EchoDungeon, Level = 3, ShopMaxHp = 300f };
            setup.Guardians[0] = Unit("g0", Stats(CombatStyle.Defense));
            setup.Deck.Add(Unit("e0", Stats(CombatStyle.Assault)));
            var sim = Start(setup);

            Assert.IsNull(sim.Guardians[0]);
            Assert.AreEqual(BattleCfg.dungeonStartResonance, sim.Resonance, 1e-4);
            Assert.AreEqual(BattleCfg.GetDungeonKillsToBoss(3), sim.KillsToBoss);
            Assert.IsTrue(sim.HasDefenders);
        }
    }
}
