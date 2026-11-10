using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 튜토리얼 전투 보호 (FTUE-01, SPEC4 §13.3 — P4-3b): 등장할 때 적 체력 × 0.6 (보스 · 분열체 포함), 수선점 내구도 바닥 1,
    /// 훅이 없으면 꺼짐, 던전은 보지 않는다, 진행 중 보호가 바뀌면 바로 따른다 + 불빛 게이지 채우기(FillLamp)는 전투 중일 때만
    /// </summary>
    public class BattleProtectionTests : SystemsTestBase
    {
        readonly List<BattleEvent> _log = new();

        [SetUp]
        public void SetUpLog() => _log.Clear();

        static CharacterStats Stats(float attack = 10f, float lifetime = 30f) =>
            SimKit.Stats(CombatStyle.Assault, attack, 100f, 1f, 0.1f, 72f, 1, lifetime);

        static SkillDef Nuke(string id, float value, float cooldown = 1f, float delay = 0.3f)
        {
            var skill = SimKit.Skill(id, SkillTrigger.Cooldown, SkillTargetType.StrongestEnemy, SimKit.Fx(SkillEffectType.Damage, value));
            skill.cooldown = cooldown;
            skill.initialDelay = delay;
            return skill;
        }

        [Test]
        public void Protection_ScalesEnemyHpAtSpawn_IncludingSplitChildren()
        {
            var tangle = SimKit.Enemy("tangle", hp: 10f, gimmicks: EnemyGimmick.Splitter);
            tangle.splitInto = "tangle";
            tangle.splitCount = 2;
            SimKit.OnlyEnemies(BattleCfg, 1, tangle);
            BattleCfg.splitHpScale = 0.4f;
            var setup = SimKit.Setup();
            setup.Protection = BattleProtection.Tutorial;
            setup.Guardians[0] = SimKit.Unit("g", Stats(), Nuke("snip", 1f));
            var sim = SimKit.Start(BattleCfg, setup, _log);
            Assert.IsTrue(sim.IsProtected);

            SimKit.Advance(sim, 0.1f);
            Assert.AreEqual(1, sim.Enemies.Count);
            Assert.AreEqual(10f * 0.6f, sim.Enemies[0].MaxHp, 1e-3, "부모 10 × 0.6");
            Assert.AreEqual(sim.Enemies[0].MaxHp, sim.Enemies[0].Hp, 1e-3);

            SimKit.Advance(sim, 0.5f);
            Assert.AreEqual(2, SimKit.Count(_log, BattleEventType.EnemySplit));
            foreach (var child in sim.Enemies)
            {
                Assert.IsTrue(child.IsSplit);
                Assert.AreEqual(10f * 0.4f * 0.6f, child.MaxHp, 1e-3, "분열체도 배율이 한 번만");
            }
        }

        [Test]
        public void Protection_ScalesBossHp()
        {
            var mite = SimKit.Enemy("mite", hp: 1f);
            var boss = SimKit.Enemy("boss", hp: 200f);
            boss.isBoss = true;
            boss.spawnWeight = 0f;
            SimKit.OnlyEnemies(BattleCfg, 1, mite, boss);
            BattleCfg.killsToBossBase = 1;
            BattleCfg.killsToBossMax = 1;
            var setup = SimKit.Setup();
            setup.Protection = BattleProtection.Tutorial;
            setup.Guardians[0] = SimKit.Unit("g", Stats(), Nuke("snip", 1f, 1f, 0.05f));
            var sim = SimKit.Start(BattleCfg, setup, _log);

            for (int i = 0; i < 40 && sim.Boss == null; i++) SimKit.Advance(sim, 0.25f);

            Assert.IsNotNull(sim.Boss, "보스가 나왔다");
            Assert.AreEqual(200f * 0.6f, sim.Boss.MaxHp, 1e-3);
        }

        [Test]
        public void Protection_ShopFloorHolds_AtOne()
        {
            var brute = SimKit.Enemy("brute", hp: 1e6f, speed: 3f, damage: 1e6f);
            SimKit.OnlyEnemies(BattleCfg, 3, brute);
            BattleCfg.bossSiegeInterval = 0.2f;
            var setup = SimKit.Setup(shopHp: 50f);
            setup.Deck.Add(SimKit.Unit("e", Stats(attack: 0f, lifetime: 1f)));
            setup.Protection = BattleProtection.Tutorial;
            var sim = SimKit.Start(BattleCfg, setup, _log);

            SimKit.Advance(sim, 60f);

            Assert.AreEqual(BattleOutcome.Running, sim.Outcome, "보호 중에는 질 수 없다");
            Assert.Greater(SimKit.Count(_log, BattleEventType.ShopDamaged), 0, "맞기는 맞았다");
            Assert.AreEqual(1f, sim.ShopHp, 1e-4, "바닥 1에서 멈춘다");

            // 같은 판을 보호 없이: 첫 적에 무너진다
            _log.Clear();
            var open = SimKit.Setup(shopHp: 50f);
            open.Deck.Add(SimKit.Unit("e", Stats(attack: 0f, lifetime: 1f)));
            var sim2 = SimKit.Start(BattleCfg, open, _log);
            SimKit.Advance(sim2, 60f);
            Assert.AreEqual(BattleOutcome.Failed, sim2.Outcome);
            Assert.IsFalse(sim2.IsProtected);
        }

        [Test]
        public void Protection_DungeonIgnoresIt()
        {
            var mite = SimKit.Enemy("mite", hp: 100f);
            SimKit.OnlyEnemies(BattleCfg, 1, mite);
            var plain = SimKit.Setup();
            plain.Mode = BattleMode.EchoDungeon;
            plain.Deck.Add(SimKit.Unit("e", Stats(attack: 0f)));
            var simPlain = SimKit.Start(BattleCfg, plain, null);
            SimKit.Advance(simPlain, 0.1f);

            var guarded = SimKit.Setup();
            guarded.Mode = BattleMode.EchoDungeon;
            guarded.Deck.Add(SimKit.Unit("e", Stats(attack: 0f)));
            guarded.Protection = BattleProtection.Tutorial;
            var simGuarded = SimKit.Start(BattleCfg, guarded, null);
            SimKit.Advance(simGuarded, 0.1f);

            Assert.IsFalse(simGuarded.IsProtected, "던전은 보호를 보지 않는다");
            Assert.AreEqual(1, simGuarded.Enemies.Count);
            Assert.AreEqual(simPlain.Enemies[0].MaxHp, simGuarded.Enemies[0].MaxHp, 1e-3);
        }

        [Test]
        public void NullResolver_MeansOff_ResolverAppliesAndFollowsLive()
        {
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.ProtectionResolver = null;
            Battle.Tick(0.1f);
            Assert.IsFalse(Battle.Sim.IsProtected, "훅 없음 = 꺼짐");
            Assert.IsFalse(Battle.Sim.Setup.Protection.Enabled);

            bool on = true;
            int asked = -1;
            Battle.ProtectionResolver = stage =>
            {
                asked = stage;
                return on ? BattleProtection.Tutorial : BattleProtection.Off;
            };
            Battle.Tick(0.1f);
            Assert.AreEqual(1, asked, "메인 스테이지 번호를 묻는다");
            Assert.IsTrue(Battle.Sim.IsProtected, "진행 중에도 다음 Tick에 따른다");
            Assert.AreEqual(0.6f, Battle.Sim.Protection.EnemyHpScale, 1e-6f);

            Battle.Restart();
            Assert.IsTrue(Battle.Sim.Setup.Protection.Enabled, "다시 시작할 때 구성에 들어간다");

            on = false;
            Battle.Tick(0.1f);
            Assert.IsFalse(Battle.Sim.IsProtected, "보호가 끝나면 바로 끈다");
        }

        [Test]
        public void ThrowingResolver_IsDropped_BattleKeepsRunning()
        {
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.ProtectionResolver = _ => throw new System.InvalidOperationException("보호 훅 시험");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("보호 훅 시험"));

            Battle.Tick(0.1f);

            Assert.IsNull(Battle.ProtectionResolver, "예외를 낸 훅은 끈다");
            Assert.IsFalse(Battle.Sim.IsProtected);
            Assert.AreEqual(BattleOutcome.Running, Battle.Sim.Outcome);
        }

        [Test]
        public void FillLamp_OnlyWhileRunning()
        {
            var mite = SimKit.Enemy("mite", hp: 1e6f, speed: 3f, damage: 1e6f);
            SimKit.OnlyEnemies(BattleCfg, 1, mite);
            var sim = new BattleSimulator(BattleCfg, 1);
            sim.FillLamp();
            Assert.AreEqual(0f, sim.LampGauge, "시작 전에는 아무것도 하지 않는다");

            var setup = SimKit.Setup(shopHp: 5f);
            setup.Deck.Add(SimKit.Unit("e", Stats(attack: 0f)));
            sim.Start(setup);
            Assert.IsFalse(sim.CanLightLamp);
            sim.FillLamp();
            Assert.AreEqual(1f, sim.LampGauge);
            Assert.IsTrue(sim.CanLightLamp);
            Assert.IsTrue(sim.TryLightLamp());
            Assert.AreEqual(0f, sim.LampGauge);

            SimKit.Advance(sim, 30f);
            Assert.AreEqual(BattleOutcome.Failed, sim.Outcome, "보호 없음 → 무너졌다");
            sim.FillLamp();
            Assert.AreEqual(0f, sim.LampGauge, "끝난 전투에는 채우지 않는다");
            Assert.IsFalse(sim.CanLightLamp);
        }
    }
}
