using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>전투 시뮬레이터 스킬/기믹 시험용 도구 (적 등장을 통제해 결과를 정확히 잰다)</summary>
    internal static class SimKit
    {
        public static CharacterStats Stats(CombatStyle style, float attack = 10f, float hp = 100f, float interval = 1f, float range = 0.1f,
            float arc = 72f, int cost = 1, float lifetime = 30f, float move = 0.8f) => new CharacterStats
        {
            style = style,
            attack = attack,
            hp = hp,
            attackInterval = interval,
            range = range,
            arcDegrees = arc,
            moveSpeed = move,
            summonCost = cost,
            lifetime = lifetime,
        };

        /// <summary>기본 공격이 수선점 근처(0.8)에만 닿는 지킴이/잔향이 — 멀리 있는 적은 스킬로만 맞는다</summary>
        public static UnitSetup Unit(string id, CharacterStats stats, SkillDef skill = null, params TraitType[] traits) =>
            new UnitSetup { FormId = id, LineageId = id, Stats = stats, Skill = skill, Traits = new List<TraitType>(traits) };

        public static SkillEffectDef Fx(SkillEffectType type, float value = 0f, float duration = 0f, int count = 0) =>
            new SkillEffectDef { type = type, value = value, duration = duration, count = count };

        public static SkillDef Skill(string id, SkillTrigger trigger, SkillTargetType target, params SkillEffectDef[] effects) => new SkillDef
        {
            id = id,
            name = id,
            trigger = trigger,
            target = target,
            cooldown = 2f,
            initialDelay = 0.1f,
            radius = 0.6f,
            vfx = SkillVfx.StarBurst,
            effects = new List<SkillEffectDef>(effects),
        };

        public static EnemyDef Enemy(string id, float hp = 1000f, float speed = 0.02f, float damage = 1f,
            EnemyGimmick gimmicks = EnemyGimmick.None) => new EnemyDef
        {
            id = id,
            name = id,
            hp = hp,
            speed = speed,
            damage = damage,
            minStage = 1,
            spawnWeight = 1f,
            gimmicks = gimmicks,
        };

        /// <summary>
        /// 적 종류를 정하고 등장을 통제한다: 첫 진행에 count마리가 0.05초 간격으로 한 방향에서 나오고, 그 뒤로는 나오지 않는다.
        /// 보스 없음, 기본 치명타 0, 잔향 자연 회복 0.
        /// </summary>
        public static void OnlyEnemies(BattleConfig cfg, int count, params EnemyDef[] defs)
        {
            cfg.enemies = new List<EnemyDef>(defs);
            cfg.killsToBossBase = 100000;
            cfg.killsToBossMax = 100000;
            cfg.killsToBossPerStage = 0f;
            cfg.firstWaveDelay = 0f;
            cfg.laneJitterDegrees = 0f;
            cfg.pressureBias = 1f;
            cfg.twoPressureChance = 0f;
            cfg.waveSizeBase = count;
            cfg.waveSizePerStage = 0f;
            cfg.waveSizeMax = Mathf.Max(count, 1);
            cfg.spawnIntervalBase = 0.05f;
            cfg.spawnIntervalMin = 0.05f;
            cfg.waveRestBase = 100000f;
            cfg.waveRestMin = 100000f;
            cfg.baseCritChance = 0f;
            cfg.resonanceRegen = 0f;
        }

        public static BattleSetup Setup(float shopHp = 1e6f) => new BattleSetup { Mode = BattleMode.Stage, Level = 1, ShopMaxHp = shopHp };

        public static BattleSimulator Start(BattleConfig cfg, BattleSetup setup, List<BattleEvent> log, int seed = 1)
        {
            var sim = new BattleSimulator(cfg, seed);
            if (log != null) sim.Event += e => log.Add(e);
            sim.Start(setup);
            return sim;
        }

        public static void Advance(BattleSimulator sim, float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += 0.25f) sim.Tick(Mathf.Min(0.25f, seconds - t));
        }

        public static List<BattleEvent> Of(List<BattleEvent> log, BattleEventType type) => log.FindAll(e => e.Type == type);

        public static int Count(List<BattleEvent> log, BattleEventType type, string key = null) =>
            log.FindAll(e => e.Type == type && (key == null || e.Key == key)).Count;

        public static EnemyState Find(BattleSimulator sim, System.Predicate<EnemyState> match)
        {
            foreach (var enemy in sim.Enemies) if (match(enemy)) return enemy;
            return null;
        }

        public static int CountEnemies(BattleSimulator sim, System.Predicate<EnemyState> match)
        {
            int count = 0;
            foreach (var enemy in sim.Enemies) if (match(enemy)) count++;
            return count;
        }
    }

    public class SkillEngineTests : SystemsTestBase
    {
        readonly List<BattleEvent> _log = new();

        [SetUp]
        public void SetUpLog() => _log.Clear();

        BattleSimulator StartSim(BattleSetup setup, int seed = 1) => SimKit.Start(BattleCfg, setup, _log, seed);

        static SkillDef Skill(string id, SkillTrigger trigger, SkillTargetType target, params SkillEffectDef[] effects) =>
            SimKit.Skill(id, trigger, target, effects);

        static SkillEffectDef Fx(SkillEffectType type, float value = 0f, float duration = 0f, int count = 0) => SimKit.Fx(type, value, duration, count);

        static CharacterStats Stats(CombatStyle style = CombatStyle.Assault, float attack = 10f, float interval = 1f, float range = 0.1f,
            float arc = 72f, float hp = 100f, int cost = 1, float lifetime = 30f) =>
            SimKit.Stats(style, attack, hp, interval, range, arc, cost, lifetime);

        void Advance(BattleSimulator sim, float seconds) => SimKit.Advance(sim, seconds);

        // ───────────────────────────── 쿨타임 / 시전 ─────────────────────────────

        [Test]
        public void CooldownSkill_HitsTargets_EmitsSkillCast_AndIsRegistered()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var skill = Skill("test_blast", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 2f));
            skill.cooldown = 1f;
            skill.initialDelay = 0.2f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            var sim = StartSim(setup);

            Advance(sim, 1.5f);

            var casts = SimKit.Of(_log, BattleEventType.SkillCast);
            Assert.AreEqual(2, casts.Count, "0.2초에 첫 시전, 1초마다 다시");
            Assert.AreEqual("test_blast", casts[0].Key);
            Assert.AreEqual(0, casts[0].SourceId, "지킴이 슬롯");
            Assert.AreEqual(sim.Enemies[0].Id, casts[0].TargetId);
            Assert.AreSame(skill, sim.GetSkill("test_blast"));
            Assert.IsNull(sim.GetSkill("missing"));
            Assert.AreEqual(1000f - 2 * 20f, sim.Enemies[0].Hp, 1e-3, "피해 = value × 공격력");
            Assert.AreEqual(1f, sim.Guardians[0].SkillCooldownMax, 1e-4);
            Assert.That(sim.Guardians[0].SkillCooldown, Is.InRange(0f, 1f));
        }

        [Test]
        public void CooldownSkill_WaitsUntilATargetExists()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            BattleCfg.firstWaveDelay = 3f;
            var skill = Skill("blast", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 1f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            var sim = StartSim(setup);

            Advance(sim, 2.5f);
            Assert.AreEqual(0, SimKit.Count(_log, BattleEventType.SkillCast), "적이 없으면 시전하지 않는다");

            Advance(sim, 1f);
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast), "적이 나오면 바로 시전");
        }

        [Test]
        public void SkillCooldownReduction_ShortensCooldown()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var skill = Skill("blast", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 1f));
            skill.cooldown = 4f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            setup.Modifiers.GetOrCreate("g").Add(StatType.SkillCooldownReductionPercent, 50f);
            var sim = StartSim(setup);

            Assert.AreEqual(2f, sim.Guardians[0].SkillCooldownMax, 1e-4);
            Advance(sim, 4.3f);
            Assert.AreEqual(3, SimKit.Count(_log, BattleEventType.SkillCast), "0.05 + 2초 간격");
        }

        // ───────────────────────────── 수선점 보호막 / 회복 ─────────────────────────────

        [Test]
        public void ShopShield_AbsorbsHits_ThenBreaks()
        {
            SimKit.OnlyEnemies(BattleCfg, 3, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 10f));
            var skill = Skill("umbrella", SkillTrigger.OnBattleStart, SkillTargetType.Shop, Fx(SkillEffectType.Shield, 1.5f, 100f));
            var setup = SimKit.Setup(100f);
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), skill);
            var sim = StartSim(setup);

            Assert.AreEqual(0, SimKit.Count(_log, BattleEventType.ShieldGained), "전투 시작 스킬은 첫 진행 때");
            Advance(sim, 0.1f);
            var gained = SimKit.Of(_log, BattleEventType.ShieldGained);
            Assert.AreEqual(1, gained.Count);
            Assert.AreEqual(-1, gained[0].TargetId, "수선점");
            Assert.AreEqual(15f, gained[0].Value, 1e-3);
            Assert.AreEqual(SkillVfx.StarBurst, gained[0].Key);
            Assert.AreEqual(15f, sim.ShopShield, 1e-3);

            Advance(sim, 3f);

            Assert.AreEqual(0f, sim.ShopShield, 1e-4);
            Assert.AreEqual(1, SimKit.Of(_log, BattleEventType.ShieldBroken).FindAll(e => e.TargetId == -1).Count);
            var hits = SimKit.Of(_log, BattleEventType.ShopDamaged);
            Assert.AreEqual(2, hits.Count, "완전히 막은 첫 공격은 내구도 피해가 아니다");
            Assert.AreEqual(5f, hits[0].Value, 1e-3);
            Assert.AreEqual(10f, hits[1].Value, 1e-3);
            Assert.AreEqual(85f, sim.ShopHp, 1e-3);
        }

        [Test]
        public void ShopShield_ExpiresAfterDuration()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var skill = Skill("bubble", SkillTrigger.OnBattleStart, SkillTargetType.Shop, Fx(SkillEffectType.Shield, 1f, 2f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            var sim = StartSim(setup);

            Advance(sim, 1f);
            Assert.AreEqual(10f, sim.ShopShield, 1e-3);
            Advance(sim, 1.5f);
            Assert.AreEqual(0f, sim.ShopShield);
        }

        [Test]
        public void OnShopDamaged_HealsShop()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 10f));
            var skill = Skill("patch", SkillTrigger.OnShopDamaged, SkillTargetType.Shop, Fx(SkillEffectType.Heal, 0.5f));
            skill.cooldown = 0f;
            var setup = SimKit.Setup(100f);
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), skill);
            var sim = StartSim(setup);

            Advance(sim, 2f);

            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.ShopDamaged));
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "patch"));
            var healed = SimKit.Of(_log, BattleEventType.ShopHealed);
            Assert.AreEqual(1, healed.Count);
            Assert.AreEqual(5f, healed[0].Value, 1e-3);
            Assert.AreEqual(95f, sim.ShopHp, 1e-3);
        }

        [Test]
        public void OnShopLowHp_FiresOnlyOncePerBattle()
        {
            SimKit.OnlyEnemies(BattleCfg, 5, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 30f));
            var skill = Skill("last_note", SkillTrigger.OnShopLowHp, SkillTargetType.Shop, Fx(SkillEffectType.Heal, 6f));
            var setup = SimKit.Setup(100f);
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), skill);
            var sim = StartSim(setup);

            Advance(sim, 3f);

            // 100 → 70 → 40 → 10(30% 미만: +60 → 70) → 40 → 10 (두 번째로 내려가도 다시 터지지 않는다)
            Assert.AreEqual(5, SimKit.Count(_log, BattleEventType.ShopDamaged));
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "last_note"));
            Assert.AreEqual(10f, sim.ShopHp, 1e-3);
            Assert.AreEqual(BattleOutcome.Running, sim.Outcome);
        }

        [Test]
        public void HealOverTime_OnShop_HealsInTicks()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 50f));
            var skill = Skill("moon", SkillTrigger.OnShopDamaged, SkillTargetType.Shop, Fx(SkillEffectType.HealOverTime, 1f, 2f));
            skill.cooldown = 0f;
            var setup = SimKit.Setup(100f);
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), skill);
            var sim = StartSim(setup);

            Advance(sim, 5f);

            var status = SimKit.Of(_log, BattleEventType.StatusApplied).FindAll(e => e.Value == (int)SkillEffectType.HealOverTime);
            Assert.AreEqual(1, status.Count);
            Assert.AreEqual(-1, status[0].TargetId);
            Assert.AreEqual("shop:" + SkillVfx.StarBurst, status[0].Key);
            Assert.GreaterOrEqual(SimKit.Count(_log, BattleEventType.ShopHealed), 4, "0.5초마다 나눠 회복");
            Assert.AreEqual(70f, sim.ShopHp, 0.05f, "초당 10 × 2초");
        }

        // ───────────────────────────── 잔향이 스킬 ─────────────────────────────

        [Test]
        public void EchoSkills_OnSummonAndOnDissolve_RunOnlyForEchoes()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            BattleCfg.startResonance = 10f;
            BattleCfg.echoRefundRate = 0f;
            var onSummon = Skill("bubble", SkillTrigger.OnSummon, SkillTargetType.Self, Fx(SkillEffectType.Shield, 2f));
            var onDissolve = Skill("melt", SkillTrigger.OnDissolve, SkillTargetType.Shop, Fx(SkillEffectType.ResonanceGain, 7f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), onSummon);
            setup.Deck.Add(SimKit.Unit("e", Stats(lifetime: 1f, cost: 2), onSummon));
            setup.Modifiers.BonusSkills["e"] = new List<SkillDef> { onDissolve };
            var sim = StartSim(setup);

            Advance(sim, 0.5f);
            Assert.AreEqual(0, SimKit.Count(_log, BattleEventType.SkillCast), "지킴이는 소환 시 스킬을 쓰지 않는다");

            Assert.IsTrue(sim.TrySummon(0, 0f));
            var echo = sim.Echoes[0];
            var gained = SimKit.Of(_log, BattleEventType.ShieldGained);
            Assert.AreEqual(1, gained.Count, "소환 직후 바로 처리");
            Assert.AreEqual(echo.Id, gained[0].TargetId);
            Assert.GreaterOrEqual(gained[0].SourceId, BattleSimulator.EchoIdBase);
            Assert.AreEqual(20f, echo.Shield, 1e-3);
            Assert.AreEqual(8f, sim.Resonance, 1e-3);

            Advance(sim, 1.5f);

            Assert.IsFalse(echo.Alive);
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "melt"));
            Assert.AreEqual(15f, sim.Resonance, 1e-3, "빛으로 풀릴 때 잔향 +7");
        }

        [Test]
        public void PureHealSkill_WaitsForHurtAlly_ThenHealsEcho()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("biter", hp: 1e6f, speed: 0.4f, damage: 10f));
            var heal = Skill("lullaby", SkillTrigger.Cooldown, SkillTargetType.AllEchoes, Fx(SkillEffectType.Heal, 1f));
            heal.cooldown = 0.5f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), heal);
            setup.Deck.Add(SimKit.Unit("e", Stats(CombatStyle.Defense, attack: 0f, range: 1.5f, hp: 200f)));
            var sim = StartSim(setup);
            Advance(sim, 0.1f);
            float laneAngle = Mathf.Atan2(sim.Enemies[0].Position.y, sim.Enemies[0].Position.x);
            Assert.IsTrue(sim.TrySummon(0, laneAngle));
            int echoId = sim.Echoes[0].Id;

            Advance(sim, 1f);
            Assert.AreEqual(0, SimKit.Count(_log, BattleEventType.SkillCast), "다친 아군이 없으면 회복 스킬을 아낀다");

            Advance(sim, 5f);
            var healed = SimKit.Of(_log, BattleEventType.EchoHealed);
            Assert.Greater(healed.Count, 0, "막아선 잔향이가 다치면 회복");
            Assert.AreEqual(echoId, healed[0].TargetId);
            Assert.AreEqual(0, healed[0].SourceId);
        }

        // ───────────────────────────── 처치 / 불 밝히기 트리거 ─────────────────────────────

        [Test]
        public void OnKill_TriggersForTheKiller()
        {
            SimKit.OnlyEnemies(BattleCfg, 3, SimKit.Enemy("weak", hp: 5f, speed: 0.5f));
            BattleCfg.lampPerKill = 0.05f;
            var skill = Skill("ember", SkillTrigger.OnKill, SkillTargetType.Self, Fx(SkillEffectType.LampGain, 0.2f));
            skill.cooldown = 0f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 100f, range: 4f, arc: 360f, interval: 0.1f), skill);
            var sim = StartSim(setup);

            Advance(sim, 3f);

            var kills = SimKit.Of(_log, BattleEventType.EnemyKilled);
            Assert.AreEqual(3, kills.Count);
            Assert.IsTrue(kills.TrueForAll(k => k.SourceId == 0), "처치한 지킴이 슬롯");
            Assert.AreEqual(3, SimKit.Count(_log, BattleEventType.SkillCast, "ember"));
            Assert.AreEqual(3 * 0.25f, sim.LampGauge, 1e-3);
        }

        [Test]
        public void OnKill_RespectsMinimumInterval()
        {
            SimKit.OnlyEnemies(BattleCfg, 3, SimKit.Enemy("weak", hp: 5f, speed: 0.5f));
            var skill = Skill("ember", SkillTrigger.OnKill, SkillTargetType.Self, Fx(SkillEffectType.ResonanceGain, 1f));
            skill.cooldown = 30f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 100f, range: 4f, arc: 360f, interval: 0.1f), skill);
            var sim = StartSim(setup);

            Advance(sim, 3f);

            Assert.AreEqual(3, SimKit.Count(_log, BattleEventType.EnemyKilled));
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "ember"));
        }

        [Test]
        public void OnLampLit_TriggersImmediately()
        {
            SimKit.OnlyEnemies(BattleCfg, 2, SimKit.Enemy("dummy"));
            BattleCfg.lampPerEchoDissolve = 1f;
            BattleCfg.startResonance = 10f;
            BattleCfg.resonanceKillGain = 0f;
            var skill = Skill("chime", SkillTrigger.OnLampLit, SkillTargetType.Shop, Fx(SkillEffectType.ResonanceGain, 5f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            setup.Deck.Add(SimKit.Unit("e", Stats(lifetime: 1f, cost: 1)));
            var sim = StartSim(setup);
            Assert.IsTrue(sim.TrySummon(0, -Mathf.PI * 0.5f));
            Advance(sim, 1.5f);
            Assert.IsTrue(sim.CanLightLamp);
            float before = sim.Resonance;

            Assert.IsTrue(sim.TryLightLamp());

            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "chime"));
            Assert.AreEqual(before + 5f, sim.Resonance, 1e-3);
        }

        [Test]
        public void OnBattleStart_ResonanceAndLampGain()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            BattleCfg.startResonance = 10f;
            var skill = Skill("wake", SkillTrigger.OnBattleStart, SkillTargetType.Shop,
                Fx(SkillEffectType.ResonanceGain, 5f), Fx(SkillEffectType.LampGain, 0.3f));
            var setup = SimKit.Setup();
            setup.Guardians[2] = SimKit.Unit("g", Stats(), skill);
            var sim = StartSim(setup);

            Advance(sim, 5f);

            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "wake"), "전투 중 한 번");
            Assert.AreEqual(15f, sim.Resonance, 1e-3);
            Assert.AreEqual(0.3f, sim.LampGauge, 1e-3);
        }

        // ───────────────────────────── 적 상태 ─────────────────────────────

        [Test]
        public void Stun_StopsMovement()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("walker", speed: 0.3f));
            var skill = Skill("lock", SkillTrigger.Cooldown, SkillTargetType.StrongestEnemy, Fx(SkillEffectType.Stun, 0f, 2f));
            skill.cooldown = 100f;
            skill.initialDelay = 0.5f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            var sim = StartSim(setup);
            Advance(sim, 0.6f);
            var enemy = sim.Enemies[0];
            float radius = enemy.Position.magnitude;

            Advance(sim, 1.5f);

            Assert.Greater(enemy.StunTimer, 0f);
            Assert.AreEqual(radius, enemy.Position.magnitude, 1e-4, "기절 중에는 멈춘다");
            var status = SimKit.Of(_log, BattleEventType.StatusApplied);
            Assert.AreEqual(1, status.Count);
            Assert.AreEqual((int)SkillEffectType.Stun, (int)status[0].Value);
            Assert.AreEqual(enemy.Id, status[0].TargetId);
            Assert.AreEqual(SkillVfx.StarBurst, status[0].Key);
        }

        [Test]
        public void Slow_HalvesSpeed()
        {
            float Moved(bool slowed)
            {
                var log = new List<BattleEvent>();
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("walker", speed: 0.3f));
                var skill = Skill("wax", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Slow, slowed ? 0.5f : 0f, 5f));
                skill.cooldown = 100f;
                skill.initialDelay = 0.2f;
                var setup = SimKit.Setup();
                setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
                var sim = SimKit.Start(BattleCfg, setup, log);
                Advance(sim, 0.5f);
                float start = sim.Enemies[0].Position.magnitude;
                Advance(sim, 2f);
                return start - sim.Enemies[0].Position.magnitude;
            }

            float normal = Moved(false);
            float slowed = Moved(true);
            Assert.AreEqual(0.6f, normal, 0.02f);
            Assert.AreEqual(0.3f, slowed, 0.02f);
        }

        [Test]
        public void Knockback_PushesOutward()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("walker", speed: 1f));
            var skill = Skill("spin", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Knockback, 0.5f));
            skill.cooldown = 100f;
            skill.initialDelay = 1f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            var sim = StartSim(setup);
            Advance(sim, 0.9f);
            var enemy = sim.Enemies[0];
            float before = enemy.Position.magnitude;

            Advance(sim, 0.2f);

            Assert.AreEqual(before - 0.2f + 0.5f, enemy.Position.magnitude, 0.03f);
        }

        [Test]
        public void Pull_DrawsEnemyTowardEchoCaster()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("walker"));
            BattleCfg.startResonance = 10f;
            var skill = Skill("waltz", SkillTrigger.OnSummon, SkillTargetType.AllEnemies, Fx(SkillEffectType.Pull, 0.6f));
            var setup = SimKit.Setup();
            setup.Deck.Add(SimKit.Unit("e", Stats(attack: 0f), skill));
            var sim = StartSim(setup);
            Advance(sim, 0.1f);
            var enemy = sim.Enemies[0];
            float laneAngle = Mathf.Atan2(enemy.Position.y, enemy.Position.x);
            var before = enemy.Position;

            Assert.IsTrue(sim.TrySummon(0, laneAngle + Mathf.PI * 0.5f));
            var echo = sim.Echoes[0];

            Assert.AreEqual(0.6f, (before - enemy.Position).magnitude, 1e-3);
            Assert.AreEqual((before - echo.Position).magnitude - 0.6f, (enemy.Position - echo.Position).magnitude, 1e-3);
            Assert.AreEqual(1, SimKit.Of(_log, BattleEventType.StatusApplied).FindAll(e => e.Value == (int)SkillEffectType.Pull).Count);
        }

        [Test]
        public void Mark_IncreasesDamageTaken()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var skill = Skill("kiss", SkillTrigger.Cooldown, SkillTargetType.StrongestEnemy,
                Fx(SkillEffectType.Mark, 50f, 5f), Fx(SkillEffectType.Damage, 1f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), skill);
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            var hits = SimKit.Of(_log, BattleEventType.EnemyHit);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(15f, hits[0].Value, 1e-3);
            Assert.Greater(sim.Enemies[0].MarkTimer, 0f);
            Assert.AreEqual(-1, hits[0].SourceId, "스킬 피해는 투사체가 아니다");
        }

        [Test]
        public void DamageOverTime_TicksForDuration()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var skill = Skill("firefly", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.DamageOverTime, 1f, 2f));
            skill.cooldown = 100f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), skill);
            var sim = StartSim(setup);

            Advance(sim, 3f);

            var enemy = sim.Enemies[0];
            Assert.AreEqual(1000f - 20f, enemy.Hp, 0.05f, "초당 10 × 2초");
            Assert.AreEqual(0f, enemy.BurnTimer);
            Assert.That(SimKit.Count(_log, BattleEventType.EnemyHit), Is.InRange(4, 5), "0.5초 간격으로 묶어서");
        }

        [Test]
        public void Chain_BouncesBetweenDistinctEnemies()
        {
            SimKit.OnlyEnemies(BattleCfg, 4, SimKit.Enemy("dummy", hp: 100f));
            var skill = Skill("gloss", SkillTrigger.Cooldown, SkillTargetType.StrongestEnemy, Fx(SkillEffectType.Chain, 1f, 0f, 2));
            skill.cooldown = 100f;
            skill.initialDelay = 0.5f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), skill);
            var sim = StartSim(setup);

            Advance(sim, 1f);

            var hits = SimKit.Of(_log, BattleEventType.EnemyHit);
            var hitIds = new HashSet<int>();
            foreach (var hit in hits) hitIds.Add(hit.TargetId);
            Assert.AreEqual(3, hits.Count, "첫 대상 + 2번 튐");
            Assert.AreEqual(3, hitIds.Count, "같은 적은 한 번만");
            var chain = SimKit.Of(_log, BattleEventType.StatusApplied).FindAll(e => e.Value == (int)SkillEffectType.Chain);
            Assert.AreEqual(3, chain.Count);
            Assert.AreEqual(hits[0].TargetId, chain[0].TargetId, "맞는 순서대로");
            Assert.AreEqual(4, sim.Enemies.Count);
        }

        [Test]
        public void ExecutePassive_BoostsDamageOnLowHpEnemies()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 100f, speed: 0.3f));
            var skill = Skill("velvet", SkillTrigger.Passive, SkillTargetType.Self, Fx(SkillEffectType.Execute, 100f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f, range: 5f, arc: 360f), skill); // 돌격: 16
            var sim = StartSim(setup);

            Advance(sim, 10f);

            var hits = SimKit.Of(_log, BattleEventType.EnemyHit);
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.EnemyKilled));
            // 100 → 84 → 68 → 52 → 36 → 20 → (30% 이하라 두 배) 32
            Assert.AreEqual(6, hits.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual(16f, hits[i].Value, 1e-3);
            Assert.AreEqual(32f, hits[5].Value, 1e-3);
        }

        [Test]
        public void AttackRampPassive_GrowsUntilCap()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            var skill = Skill("melt", SkillTrigger.Passive, SkillTargetType.Self, Fx(SkillEffectType.AttackRamp, 10f, 0f, 50));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f, range: 5f, arc: 360f), skill);
            var sim = StartSim(setup);

            Advance(sim, 12f);

            var attacks = SimKit.Of(_log, BattleEventType.GuardianAttack);
            Assert.Greater(attacks.Count, 8);
            Assert.AreEqual(16f, attacks[0].Value, 0.1f);
            Assert.AreEqual(16f * 1.3f, attacks[3].Value, 0.2f, "3초 → +30%");
            Assert.AreEqual(16f * 1.5f, attacks[attacks.Count - 1].Value, 1e-3, "최대 +50%");
        }

        // ───────────────────────────── 아군 강화 / 소환 ─────────────────────────────

        [Test]
        public void BuffAttack_AppliesToAdjacentGuardiansOnly()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", speed: 0f));
            var buff = Skill("cheer", SkillTrigger.OnBattleStart, SkillTargetType.AdjacentGuardians, Fx(SkillEffectType.BuffAttack, 50f, 30f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("caster", Stats(), buff);
            setup.Guardians[1] = SimKit.Unit("left", Stats(attack: 10f, range: 5f, arc: 360f));
            setup.Guardians[2] = SimKit.Unit("far", Stats(attack: 10f, range: 5f, arc: 360f));
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            Assert.Greater(sim.Guardians[1].BuffTimer, 0f);
            Assert.AreEqual(0f, sim.Guardians[2].BuffTimer);
            Assert.AreEqual(0f, sim.Guardians[0].BuffTimer, "양옆만");
            var attacks = SimKit.Of(_log, BattleEventType.GuardianAttack);
            Assert.AreEqual(24f, attacks.Find(a => a.SourceId == 1).Value, 1e-3);
            Assert.AreEqual(16f, attacks.Find(a => a.SourceId == 2).Value, 1e-3);
            var status = SimKit.Of(_log, BattleEventType.StatusApplied);
            Assert.AreEqual(1, status.Count);
            Assert.AreEqual(1, status[0].TargetId);
            Assert.AreEqual("guardian:" + SkillVfx.StarBurst, status[0].Key);
        }

        [Test]
        public void BuffAttackSpeed_DoublesAttackRate()
        {
            int Attacks(float buff)
            {
                var log = new List<BattleEvent>();
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
                var skill = Skill("waltz", SkillTrigger.OnBattleStart, SkillTargetType.AllGuardians, Fx(SkillEffectType.BuffAttackSpeed, buff, 100f));
                var setup = SimKit.Setup();
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 1f, range: 5f, arc: 360f), skill);
                var sim = SimKit.Start(BattleCfg, setup, log);
                Advance(sim, 4.95f);
                return SimKit.Count(log, BattleEventType.GuardianAttack);
            }

            Assert.AreEqual(5, Attacks(0f));
            Assert.AreEqual(10, Attacks(100f));
        }

        [Test]
        public void Summon_CreatesTokens_ThatDoNotUseDeckLimitOrRefund()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            BattleCfg.startResonance = 10f;
            BattleCfg.maxEchoes = 1;
            BattleCfg.lampPerEchoDissolve = 0.1f;
            BattleCfg.tokenLampScale = 0.5f;
            var skill = Skill("family", SkillTrigger.OnBattleStart, SkillTargetType.Self, Fx(SkillEffectType.Summon, 0.5f, 2f, 2));
            var setup = SimKit.Setup();
            setup.Guardians[3] = SimKit.Unit("sock", Stats(attack: 10f, hp: 100f), skill);
            setup.Deck.Add(SimKit.Unit("e", Stats(cost: 1)));
            var sim = StartSim(setup);

            Advance(sim, 0.1f);

            var tokens = SimKit.Of(_log, BattleEventType.TokenSummoned);
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual(3, tokens[0].SourceId);
            Assert.AreEqual(2, sim.Echoes.Count);
            foreach (var token in sim.Echoes)
            {
                Assert.IsTrue(token.IsToken);
                Assert.AreEqual(-1, token.DeckSlot);
                Assert.AreEqual("sock", token.FormId);
                Assert.AreEqual(5f, token.Stats.attack, 1e-3);
                Assert.AreEqual(50f, token.MaxHp, 1e-3);
            }
            Assert.AreEqual(0, SimKit.Count(_log, BattleEventType.EchoSummoned), "토큰은 덱 소환이 아니다");
            Assert.IsTrue(sim.CanSummon(0), "토큰은 잔향이 수 제한에 들지 않는다");

            Advance(sim, 2.5f);

            Assert.AreEqual(0, sim.Echoes.Count);
            Assert.AreEqual(10f, sim.Resonance, 1e-3, "토큰은 잔향을 돌려주지 않는다");
            Assert.AreEqual(0.1f, sim.LampGauge, 1e-3, "토큰 하나당 게이지 절반");
        }

        // ───────────────────────────── 치명타 ─────────────────────────────

        [Test]
        public void Crit_AlwaysWhenChanceIsFull()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            BattleCfg.baseCritChance = 100f;
            BattleCfg.baseCritDamage = 50f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f, range: 5f, arc: 360f));
            var sim = StartSim(setup);

            Advance(sim, 3f);

            var hits = SimKit.Of(_log, BattleEventType.EnemyHit);
            var crits = SimKit.Of(_log, BattleEventType.CriticalHit);
            Assert.AreEqual(hits.Count, crits.Count);
            Assert.AreEqual(24f, hits[0].Value, 1e-3, "16 × 1.5");
            Assert.AreEqual(24f, crits[0].Value, 1e-3);
            Assert.AreEqual(0, crits[0].SourceId);
        }

        [Test]
        public void Crit_IsDeterministicWithSeed_AndFollowsLineageModifier()
        {
            List<string> Run(int seed)
            {
                var log = new List<BattleEvent>();
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e7f, speed: 0f));
                BattleCfg.baseCritChance = 0f;
                var setup = SimKit.Setup();
                setup.Guardians[0] = SimKit.Unit("lucky", Stats(attack: 10f, range: 5f, arc: 360f, interval: 0.2f));
                setup.Guardians[1] = SimKit.Unit("plain", Stats(attack: 10f, range: 5f, arc: 360f, interval: 0.2f));
                setup.Modifiers.GetOrCreate("lucky").Add(StatType.CritChancePercent, 50f);
                var sim = SimKit.Start(BattleCfg, setup, log, seed);
                Advance(sim, 20f);
                return log.FindAll(e => e.Type == BattleEventType.CriticalHit).ConvertAll(e => $"{e.SourceId}:{e.Value:0.###}");
            }

            var a = Run(9);
            Assert.Greater(a.Count, 20);
            Assert.IsTrue(a.TrueForAll(c => c.StartsWith("0:")), "치명타 시너지는 그 계보만");
            Assert.Less(a.Count, 90, "절반 정도");
            CollectionAssert.AreEqual(a, Run(9), "같은 시드 = 같은 치명타");
            CollectionAssert.AreNotEqual(a, Run(10));
        }

        // ───────────────────────────── 시너지 능력치 ─────────────────────────────

        [Test]
        public void Modifiers_AttackPercent_TeamAndLineage_StackWithAttackMultiplier()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            var setup = SimKit.Setup();
            setup.AttackMultiplier = 2f;
            setup.Guardians[0] = SimKit.Unit("a", Stats(attack: 10f, range: 5f, arc: 360f));
            setup.Guardians[1] = SimKit.Unit("b", Stats(attack: 10f, range: 5f, arc: 360f));
            setup.Modifiers.Team.Add(StatType.AttackPercent, 50f);
            setup.Modifiers.GetOrCreate("a").Add(StatType.AttackPercent, 50f);
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            var attacks = SimKit.Of(_log, BattleEventType.GuardianAttack);
            Assert.AreEqual(16f * 2f * 2f, attacks.Find(e => e.SourceId == 0).Value, 1e-3);
            Assert.AreEqual(16f * 2f * 1.5f, attacks.Find(e => e.SourceId == 1).Value, 1e-3);
        }

        [Test]
        public void Modifiers_ShopAndResonance()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.startResonance = 10f;
            BattleCfg.resonanceRegen = 1f;
            var setup = SimKit.Setup(200f);
            setup.Deck.Add(SimKit.Unit("e", Stats(cost: 5, hp: 100f, lifetime: 10f)));
            setup.Deck.Add(SimKit.Unit("cheap", Stats(cost: 1)));
            setup.Modifiers.Team.Add(StatType.ShopMaxHpPercent, 50f);
            setup.Modifiers.Team.Add(StatType.StartResonanceFlat, 5f);
            setup.Modifiers.Team.Add(StatType.ResonanceRegenPercent, 100f);
            var lineage = setup.Modifiers.GetOrCreate("e");
            lineage.Add(StatType.EchoCostReductionFlat, 2f);
            lineage.Add(StatType.EchoLifetimePercent, 100f);
            lineage.Add(StatType.HpPercent, 50f);
            setup.Modifiers.GetOrCreate("cheap").Add(StatType.EchoCostReductionFlat, 5f);
            var sim = StartSim(setup);

            Assert.AreEqual(300f, sim.ShopMaxHp, 1e-3);
            Assert.AreEqual(300f, sim.ShopHp, 1e-3);
            Assert.AreEqual(15f, sim.Resonance, 1e-3);
            Assert.AreEqual(3, sim.Deck[0].Cost);
            Assert.AreEqual(1, sim.Deck[1].Cost, "1 아래로는 내려가지 않는다");

            Assert.IsTrue(sim.TrySummon(0, 0f));
            var echo = sim.Echoes[0];
            Assert.AreEqual(20f, echo.MaxLife, 1e-3);
            Assert.AreEqual(150f, echo.MaxHp, 1e-3);

            Advance(sim, 1f);
            Assert.AreEqual(12f + 2f, sim.Resonance, 0.05f, "초당 회복 두 배");

            sim.SetShopMaxHp(400f);
            Assert.AreEqual(600f, sim.ShopMaxHp, 1e-3, "강화 후에도 시너지 배율 유지");
        }

        [Test]
        public void Modifiers_ShopDamageReductionAndRegen()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 40f));
            var setup = SimKit.Setup(100f);
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 0f));
            setup.Modifiers.Team.Add(StatType.ShopDamageReductionPercent, 50f);
            setup.Modifiers.Team.Add(StatType.ShopRegenPercentPerSecond, 1f);
            var sim = StartSim(setup);

            Advance(sim, 1.5f);
            var hit = SimKit.Of(_log, BattleEventType.ShopDamaged);
            Assert.AreEqual(1, hit.Count);
            Assert.AreEqual(20f, hit[0].Value, 1e-3);
            float hp = sim.ShopHp;

            Advance(sim, 5f);
            Assert.AreEqual(hp + 5f, sim.ShopHp, 0.6f, "초당 최대 내구도의 1%");
        }

        [Test]
        public void Modifiers_GlobalStatsDoNotStackPerMember()
        {
            var modifiers = new BattleModifiers();
            modifiers.Team.Add(StatType.ShopMaxHpPercent, 10f);
            modifiers.GetOrCreate("a").Add(StatType.ShopMaxHpPercent, 20f);
            modifiers.GetOrCreate("b").Add(StatType.ShopMaxHpPercent, 20f);
            modifiers.GetOrCreate("c").Add(StatType.ShopMaxHpPercent, -5f);

            Assert.AreEqual(25f, BattleSimulator.SumGlobal(modifiers, StatType.ShopMaxHpPercent), 1e-4);
            Assert.AreEqual(0f, BattleSimulator.SumGlobal(null, StatType.ShopMaxHpPercent));
        }

        [Test]
        public void Modifiers_FormBlock_OnlyThatForm_NotTheOtherFormOfTheLineage()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = new UnitSetup { FormId = "sock_base", LineageId = "sock", Stats = Stats(attack: 10f, range: 5f, arc: 360f) };
            setup.Guardians[1] = new UnitSetup { FormId = "sock_a", LineageId = "sock", Stats = Stats(attack: 10f, range: 5f, arc: 360f) };
            setup.Modifiers.GetOrCreate("sock").Add(StatType.AttackPercent, 50f);
            setup.Modifiers.GetOrCreateForm("sock_a", "sock").Add(StatType.AttackPercent, 100f);
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            var attacks = SimKit.Of(_log, BattleEventType.GuardianAttack);
            Assert.AreEqual(16f * 1.5f, attacks.Find(e => e.SourceId == 0).Value, 1e-3, "계보 칸만");
            Assert.AreEqual(16f * 2.5f, attacks.Find(e => e.SourceId == 1).Value, 1e-3, "계보 칸 + 형태 칸");
            Assert.AreEqual(150f, setup.Modifiers.For("sock", "sock_a")[StatType.AttackPercent], 1e-4);
            Assert.AreEqual(50f, setup.Modifiers.For("sock")[StatType.AttackPercent], 1e-4);
        }

        [Test]
        public void Modifiers_FormBlock_EchoCostAndLifetime()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.startResonance = 10f;
            var setup = SimKit.Setup();
            setup.Deck.Add(new UnitSetup { FormId = "e_a", LineageId = "e", Stats = Stats(cost: 5, lifetime: 10f) });
            setup.Deck.Add(new UnitSetup { FormId = "e_b", LineageId = "e", Stats = Stats(cost: 5, lifetime: 10f) });
            var form = setup.Modifiers.GetOrCreateForm("e_a", "e");
            form.Add(StatType.EchoCostReductionFlat, 2f);
            form.Add(StatType.EchoLifetimePercent, 100f);
            var sim = StartSim(setup);

            Assert.AreEqual(3, sim.Deck[0].Cost);
            Assert.AreEqual(5, sim.Deck[1].Cost, "같은 계보의 다른 형태는 받지 않는다");
            Assert.IsTrue(sim.TrySummon(0, 0f));
            Assert.AreEqual(20f, sim.Echoes[0].MaxLife, 1e-3);
        }

        [Test]
        public void FormBonusSkills_OnlyThatFormCasts()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var bonus = Skill("form_bonus", SkillTrigger.Cooldown, SkillTargetType.StrongestEnemy, Fx(SkillEffectType.Damage, 2f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = new UnitSetup { FormId = "sock_base", LineageId = "sock", Stats = Stats(attack: 10f) };
            setup.Guardians[1] = new UnitSetup { FormId = "sock_a", LineageId = "sock", Stats = Stats(attack: 10f) };
            setup.Modifiers.AddFormBonusSkill("sock_a", bonus);
            setup.Modifiers.AddFormBonusSkill("sock_a", bonus);
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            var casts = SimKit.Of(_log, BattleEventType.SkillCast).FindAll(e => e.Key == "form_bonus");
            Assert.AreEqual(1, casts.Count, "형태 보너스 스킬은 그 형태만 (같은 스킬은 한 번만 들어간다)");
            Assert.AreEqual(1, casts[0].SourceId);
            Assert.AreSame(bonus, sim.GetSkill("form_bonus"));
            Assert.AreEqual(1, setup.Modifiers.FormBonusSkills["sock_a"].Count);
        }

        [Test]
        public void Modifiers_GlobalStats_FormBlockCountsWithItsLineage()
        {
            var modifiers = new BattleModifiers();
            modifiers.GetOrCreate("a").Add(StatType.ShopMaxHpPercent, 20f);
            modifiers.GetOrCreateForm("a_1", "a").Add(StatType.ShopMaxHpPercent, 10f);
            modifiers.GetOrCreateForm("b_1", "b").Add(StatType.ShopMaxHpPercent, 25f);
            modifiers.GetOrCreateForm("x_1").Add(StatType.ShopMaxHpPercent, -5f);

            Assert.AreEqual(30f - 5f, BattleSimulator.SumGlobal(modifiers, StatType.ShopMaxHpPercent), 1e-4, "a_1 = 계보 a 20 + 형태 10 (한 유닛)");
            Assert.AreEqual("a", modifiers.FormLineage["a_1"]);
            Assert.IsFalse(modifiers.FormLineage.ContainsKey("x_1"));
        }

        [Test]
        public void Modifiers_LampGainAndLampDamage()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 100f));
            BattleCfg.lampPerEchoDissolve = 0.5f;
            BattleCfg.lampDamageRatio = 0.25f;
            BattleCfg.startResonance = 10f;
            var setup = SimKit.Setup();
            setup.Deck.Add(SimKit.Unit("e", Stats(attack: 0f, lifetime: 1f, cost: 1)));
            setup.Modifiers.Team.Add(StatType.LampGainPercent, 100f);
            setup.Modifiers.Team.Add(StatType.LampDamagePercent, 100f);
            var sim = StartSim(setup);
            Assert.IsTrue(sim.TrySummon(0, -Mathf.PI * 0.5f));

            Advance(sim, 1.5f);
            Assert.AreEqual(1f, sim.LampGauge, 1e-4, "0.5 × 2");

            Assert.IsTrue(sim.TryLightLamp());
            Assert.AreEqual(50f, sim.Enemies[0].Hp, 1e-3, "최대 체력 25% × 2");
        }

        [Test]
        public void BonusSkills_RunLikeExtraSkills()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var own = Skill("own", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 1f));
            var bonus = Skill("bonus", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 2f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), own);
            setup.Guardians[1] = SimKit.Unit("other", Stats(attack: 10f));
            setup.Modifiers.BonusSkills["g"] = new List<SkillDef> { bonus };
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "own"));
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "bonus"));
            Assert.AreSame(bonus, sim.GetSkill("bonus"));
            Assert.AreEqual(1000f - 30f, sim.Enemies[0].Hp, 1e-3);
            Assert.AreEqual(own.cooldown, sim.Guardians[0].SkillCooldownMax, 1e-4, "쿨타임 표시는 형태 고유 스킬");
        }

        [Test]
        public void SkillWithoutId_GetsGeneratedKey()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy"));
            var skill = Skill(null, SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 1f));
            skill.vfx = null;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), skill);
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            var cast = SimKit.Of(_log, BattleEventType.SkillCast)[0];
            Assert.IsFalse(string.IsNullOrEmpty(cast.Key));
            Assert.AreSame(skill, sim.GetSkill(cast.Key));
        }

        [Test]
        public void StartEvents_SynergiesAndStageModifier_OnFirstTick()
        {
            BattleCfg.firstWaveDelay = 1000f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats());
            setup.StageModifier = StageModifierType.FullMoon;
            setup.Modifiers.ActiveSynergies.Add(new KeyValuePair<string, int>("trait_night", 1));
            setup.Modifiers.ActiveSynergies.Add(new KeyValuePair<string, int>("combo_night_walk", 0));
            var sim = StartSim(setup);

            Assert.AreEqual(0, _log.Count, "뷰가 StageChanged를 받은 뒤 첫 진행에 나온다");
            Assert.AreEqual(StageModifierType.FullMoon, sim.StageModifier);
            sim.Tick(0.1f);

            var synergies = SimKit.Of(_log, BattleEventType.SynergyActivated);
            Assert.AreEqual(2, synergies.Count);
            Assert.AreEqual("trait_night", synergies[0].Key);
            Assert.AreEqual(1f, synergies[0].Value);
            Assert.AreEqual("combo_night_walk", synergies[1].Key);
            var stage = SimKit.Of(_log, BattleEventType.StageModifierStarted);
            Assert.AreEqual(1, stage.Count);
            Assert.AreEqual((int)StageModifierType.FullMoon, (int)stage[0].Value);
            Assert.AreEqual(SkillVfx.Overlay(StageModifierType.FullMoon), stage[0].Key);

            sim.Tick(1f);
            Assert.AreEqual(2, SimKit.Count(_log, BattleEventType.SynergyActivated), "한 번만");
        }

        [Test]
        public void SkillsAndModifiers_KeepBattleDeterministic()
        {
            List<string> Run(int seed)
            {
                var cfg = BattleCfg;
                cfg.ApplyDefaults();
                var log = new List<string>();
                var setup = SimKit.Setup(400f);
                setup.Level = 9;
                setup.StageModifier = StageModifierType.FullMoon;
                setup.Guardians[0] = SimKit.Unit("g0", SimKit.Stats(CombatStyle.Defense, 12f, 200f, 1f, 1.5f, 80f),
                    Skill("stitch", SkillTrigger.Cooldown, SkillTargetType.EnemiesInRange, Fx(SkillEffectType.Damage, 1.2f), Fx(SkillEffectType.Slow, 0.4f, 2f)),
                    TraitType.Fabric, TraitType.Night);
                setup.Guardians[2] = SimKit.Unit("g2", SimKit.Stats(CombatStyle.Snipe, 20f, 80f, 1.5f, 2.3f, 40f),
                    Skill("lock", SkillTrigger.Cooldown, SkillTargetType.StrongestEnemy, Fx(SkillEffectType.Stun, 0f, 1.2f), Fx(SkillEffectType.Mark, 20f, 4f)),
                    TraitType.Metal, TraitType.Sound);
                setup.Deck.Add(SimKit.Unit("e0", SimKit.Stats(CombatStyle.Support, 8f, 90f, 1f, 1.4f, cost: 4, lifetime: 13f),
                    Skill("glow", SkillTrigger.OnSummon, SkillTargetType.AllEchoes, Fx(SkillEffectType.HealOverTime, 0.5f, 4f), Fx(SkillEffectType.LampGain, 0.05f)),
                    TraitType.Light, TraitType.Night));
                setup.Modifiers.Team.Add(StatType.AttackPercent, 10f);
                var sim = new BattleSimulator(cfg, seed) { AutoMode = true };
                sim.Event += e => log.Add($"{e.Type}:{e.SourceId}:{e.TargetId}:{e.Value:0.###}:{e.Key}");
                sim.Start(setup);
                for (int i = 0; i < 600 && sim.Outcome == BattleOutcome.Running; i++) sim.Tick(0.1f);
                log.Add($"end {sim.Outcome} {sim.Kills} {sim.ShopHp:0.###}");
                return log;
            }

            var a = Run(5);
            CollectionAssert.AreEqual(a, Run(5));
            Assert.Greater(a.FindAll(s => s.StartsWith("SkillCast")).Count, 5, "스킬이 실제로 쓰인다");
        }
    }
}
