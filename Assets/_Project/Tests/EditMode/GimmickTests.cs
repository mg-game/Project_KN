using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>적 기믹(보호막·분열·보이지 않음·녹·좀나방·회복/가속 오라·분노·불빛 흡수)과 스테이지 기믹</summary>
    public class GimmickTests : SystemsTestBase
    {
        readonly List<BattleEvent> _log = new();

        [SetUp]
        public void SetUpLog() => _log.Clear();

        BattleSimulator StartSim(BattleSetup setup, int seed = 1) => SimKit.Start(BattleCfg, setup, _log, seed);

        static void Advance(BattleSimulator sim, float seconds) => SimKit.Advance(sim, seconds);

        static CharacterStats Stats(CombatStyle style = CombatStyle.Assault, float attack = 10f, float interval = 1f, float range = 0.1f,
            float arc = 72f, float hp = 100f, int cost = 1, float lifetime = 30f) =>
            SimKit.Stats(style, attack, hp, interval, range, arc, cost, lifetime);

        static SkillDef Nuke(string id, float value, float cooldown = 100f, float delay = 0.3f, SkillTargetType target = SkillTargetType.StrongestEnemy)
        {
            var skill = SimKit.Skill(id, SkillTrigger.Cooldown, target, SimKit.Fx(SkillEffectType.Damage, value));
            skill.cooldown = cooldown;
            skill.initialDelay = delay;
            return skill;
        }

        // ───────────────────────────── 보호막 / 분열 ─────────────────────────────

        [Test]
        public void Shielded_AbsorbsDamageFirst_ThenBreaks()
        {
            var thimble = SimKit.Enemy("thimble", hp: 100f, gimmicks: EnemyGimmick.Shielded);
            thimble.shieldAmount = 50f;
            SimKit.OnlyEnemies(BattleCfg, 1, thimble);
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), Nuke("poke", 3f, 1f));
            var sim = StartSim(setup);

            Advance(sim, 0.1f);
            var enemy = sim.Enemies[0];
            Assert.AreEqual(50f, enemy.Shield, 1e-3);
            Assert.AreEqual(50f, enemy.MaxShield, 1e-3);

            Advance(sim, 0.4f);
            Assert.AreEqual(20f, enemy.Shield, 1e-3);
            Assert.AreEqual(100f, enemy.Hp, 1e-3, "보호막이 먼저 받는다");
            Assert.AreEqual(30f, SimKit.Of(_log, BattleEventType.EnemyHit)[0].Value, 1e-3, "맞은 피해는 흡수분 포함");

            Advance(sim, 1f);
            Assert.AreEqual(0f, enemy.Shield);
            Assert.AreEqual(90f, enemy.Hp, 1e-3);
            var broken = SimKit.Of(_log, BattleEventType.ShieldBroken);
            Assert.AreEqual(1, broken.Count);
            Assert.AreEqual(enemy.Id, broken[0].TargetId);
        }

        [Test]
        public void Shielded_SurvivesLamp()
        {
            var thimble = SimKit.Enemy("thimble", hp: 100f, gimmicks: EnemyGimmick.Shielded);
            thimble.shieldAmount = 40f;
            SimKit.OnlyEnemies(BattleCfg, 1, thimble);
            BattleCfg.lampPerEchoDissolve = 1f;
            BattleCfg.lampDamageRatio = 1f;
            BattleCfg.startResonance = 10f;
            var setup = SimKit.Setup();
            setup.Deck.Add(SimKit.Unit("e", Stats(attack: 0f, lifetime: 1f)));
            var sim = StartSim(setup);
            Assert.IsTrue(sim.TrySummon(0, -Mathf.PI * 0.5f));
            Advance(sim, 1.5f);

            Assert.IsTrue(sim.TryLightLamp());

            Assert.AreEqual(1, sim.Enemies.Count, "껍데기가 불빛을 일부 막아 낸다");
            Assert.AreEqual(40f, sim.Enemies[0].Hp, 1e-3, "불빛 100 중 40은 껍데기가 받아 낸다");
        }

        [Test]
        public void Splitter_SplitsOnce_IntoSmallerEnemies()
        {
            var tangle = SimKit.Enemy("tangle", hp: 10f, gimmicks: EnemyGimmick.Splitter);
            tangle.splitInto = "tangle";
            tangle.splitCount = 2;
            SimKit.OnlyEnemies(BattleCfg, 1, tangle);
            BattleCfg.splitHpScale = 0.4f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), Nuke("snip", 1f, 1f));
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            var splits = SimKit.Of(_log, BattleEventType.EnemySplit);
            Assert.AreEqual(2, splits.Count);
            Assert.AreEqual(2, sim.Enemies.Count);
            int parentId = splits[0].SourceId;
            foreach (var child in sim.Enemies)
            {
                Assert.IsTrue(child.IsSplit);
                Assert.AreEqual(4f, child.MaxHp, 1e-3);
                Assert.AreNotEqual(parentId, child.Id);
                Assert.IsTrue(splits.Exists(s => s.TargetId == child.Id));
            }
            Assert.AreEqual(1 + 2, SimKit.Count(_log, BattleEventType.EnemySpawned));

            Advance(sim, 2f);

            Assert.AreEqual(0, sim.Enemies.Count);
            Assert.AreEqual(3, sim.Kills, "나뉜 적도 처치로 센다");
            Assert.AreEqual(2, SimKit.Count(_log, BattleEventType.EnemySplit), "나뉜 적은 다시 나뉘지 않는다");
        }

        // ───────────────────────────── 보이지 않는 적 ─────────────────────────────

        [Test]
        public void Phasing_OnlyLightUnitsCanTargetWhileHidden()
        {
            int Attacks(params TraitType[] traits)
            {
                var log = new List<BattleEvent>();
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("wisp", gimmicks: EnemyGimmick.Phasing));
                var setup = SimKit.Setup();
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 1f, range: 5f, arc: 360f), null, traits);
                var sim = SimKit.Start(BattleCfg, setup, log);
                Advance(sim, 0.1f);
                // 빛 특성은 첫 공격부터 맞힌 적을 드러낸다
                if (System.Array.IndexOf(traits, TraitType.Light) < 0) Assert.IsFalse(sim.Enemies[0].Revealed);
                Advance(sim, 2f);
                return SimKit.Count(log, BattleEventType.GuardianAttack);
            }

            Assert.AreEqual(0, Attacks(TraitType.Fabric), "보이지 않는 적은 노릴 수 없다");
            Assert.Greater(Attacks(TraitType.Light), 1, "빛 특성은 볼 수 있다");
        }

        [Test]
        public void Phasing_RevealSkillShowsHiddenEnemy_ForAWhile()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("wisp", speed: 0.02f, gimmicks: EnemyGimmick.Phasing));
            var reveal = SimKit.Skill("lantern", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, SimKit.Fx(SkillEffectType.Reveal, 0f, 1f));
            reveal.cooldown = 100f;
            reveal.initialDelay = 0.5f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("caster", Stats(), reveal);
            setup.Guardians[1] = SimKit.Unit("g", Stats(attack: 1f, range: 5f, arc: 360f));
            var sim = StartSim(setup);
            Advance(sim, 0.3f);
            var wisp = sim.Enemies[0];
            Assert.IsFalse(wisp.Revealed);
            Assert.AreEqual(0, SimKit.Count(_log, BattleEventType.GuardianAttack));

            Advance(sim, 0.5f);
            Assert.IsTrue(wisp.Revealed, "드러냄 스킬");
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.EnemyRevealed));
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.SkillCast, "lantern"), "숨은 적만 있어도 드러냄 스킬은 쓴다");
            Assert.Greater(SimKit.Count(_log, BattleEventType.GuardianAttack), 0, "드러난 동안은 누구나 공격");

            Advance(sim, 1.5f);
            Assert.IsFalse(wisp.Revealed, "시간이 지나면 다시 숨는다");
        }

        [Test]
        public void Phasing_RevealedWhenCloseToShop_AndNotBlockedByEchoes()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("wisp", speed: 0.5f, damage: 3f, gimmicks: EnemyGimmick.Phasing));
            BattleCfg.phasingRevealRadius = 0.85f;
            BattleCfg.startResonance = 10f;
            var setup = SimKit.Setup();
            // 그림자와 같은 속도라 드러난 뒤에도 따라잡지 못한다 → 막을 기회는 숨어 있는 동안 마주칠 때뿐
            setup.Deck.Add(SimKit.Unit("blocker", SimKit.Stats(CombatStyle.Defense, attack: 0f, hp: 1000f, range: 1.5f, move: 0.5f)));
            var sim = StartSim(setup);
            Advance(sim, 0.1f);
            var wisp = sim.Enemies[0];
            Assert.IsTrue(sim.TrySummon(0, Mathf.Atan2(wisp.Position.y, wisp.Position.x)));

            Advance(sim, 6f);

            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.EnemyRevealed), "수선점 불빛 가까이에서 드러난다");
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.ShopDamaged), "잔향이 사이로 스며 지나간다");
        }

        // ───────────────────────────── 녹 / 좀나방 ─────────────────────────────

        [Test]
        public void Rusting_SlowsMetalUnits_AndCleanseRemovesIt()
        {
            int Attacks(bool metal, bool cleanse)
            {
                var log = new List<BattleEvent>();
                var rust = SimKit.Enemy("rust", hp: 1e6f, speed: 0f, gimmicks: EnemyGimmick.Rusting);
                rust.auraRadius = 5f;
                rust.auraValue = 100f;
                SimKit.OnlyEnemies(BattleCfg, 1, rust);
                SkillDef skill = null;
                if (cleanse)
                {
                    skill = SimKit.Skill("polish", SkillTrigger.Cooldown, SkillTargetType.Self, SimKit.Fx(SkillEffectType.Cleanse, 0f, 100f));
                    skill.initialDelay = 0.5f;
                    skill.cooldown = 100f;
                }
                var setup = SimKit.Setup();
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 1f, range: 5f, arc: 360f), skill, metal ? TraitType.Metal : TraitType.Fabric);
                var sim = SimKit.Start(BattleCfg, setup, log);
                Advance(sim, 0.3f);
                if (metal && !cleanse)
                {
                    Assert.Greater(sim.Guardians[0].DebuffTimer, 0f);
                    var status = log.FindAll(e => e.Type == BattleEventType.StatusApplied);
                    Assert.AreEqual(1, status.Count);
                    Assert.AreEqual(0, status[0].TargetId);
                    Assert.AreEqual("guardian:" + SkillVfx.DebuffMark, status[0].Key);
                }
                Advance(sim, 7.6f);
                if (cleanse) Assert.AreEqual(0f, sim.Guardians[0].DebuffTimer, "정화 후 면역");
                return log.FindAll(e => e.Type == BattleEventType.GuardianAttack).Count;
            }

            Assert.AreEqual(8, Attacks(false, false));
            Assert.AreEqual(4, Attacks(true, false), "녹: 공격 속도 절반");
            Assert.That(Attacks(true, true), Is.InRange(7, 8), "정화하면 다시 제 속도");
        }

        [Test]
        public void FabricEater_GnawsFabricGuardians()
        {
            float LastHit(TraitType trait)
            {
                var log = new List<BattleEvent>();
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("moth", hp: 1e6f, speed: 0f, gimmicks: EnemyGimmick.FabricEater));
                BattleCfg.fabricEaterRadius = 5f;
                BattleCfg.fabricEaterAttackDownPercent = 25f;
                var setup = SimKit.Setup();
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f, range: 5f, arc: 360f), null, trait);
                var sim = SimKit.Start(BattleCfg, setup, log);
                Advance(sim, 2.5f);
                var attacks = log.FindAll(e => e.Type == BattleEventType.GuardianAttack);
                return attacks[attacks.Count - 1].Value;
            }

            Assert.AreEqual(16f, LastHit(TraitType.Metal), 1e-3);
            Assert.AreEqual(12f, LastHit(TraitType.Fabric), 1e-3, "좀나방이 천을 갉아먹어 공격력 -25%");
        }

        [Test]
        public void FabricEater_HurtsFabricEchoesMore()
        {
            float HpLost(TraitType trait)
            {
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("moth", hp: 1e6f, speed: 0.5f, damage: 10f, gimmicks: EnemyGimmick.FabricEater));
                BattleCfg.fabricEaterEchoScale = 1.5f;
                BattleCfg.fabricEaterRadius = 0f;
                BattleCfg.startResonance = 10f;
                var setup = SimKit.Setup();
                setup.Deck.Add(SimKit.Unit("e", Stats(CombatStyle.Defense, attack: 0f, range: 1.5f, hp: 1000f), null, trait));
                var sim = SimKit.Start(BattleCfg, setup, null);
                Advance(sim, 0.1f);
                var moth = sim.Enemies[0];
                Assert.IsTrue(sim.TrySummon(0, Mathf.Atan2(moth.Position.y, moth.Position.x)));
                var echo = sim.Echoes[0];
                Advance(sim, 5f);
                return echo.MaxHp - echo.Hp;
            }

            float plain = HpLost(TraitType.Metal);
            float fabric = HpLost(TraitType.Fabric);
            Assert.Greater(plain, 1f);
            Assert.AreEqual(1.5f, fabric / plain, 0.02f);
        }

        // ───────────────────────────── 오라 ─────────────────────────────

        [Test]
        public void Mender_HealsOtherEnemies()
        {
            var windup = SimKit.Enemy("windup", hp: 100f, gimmicks: EnemyGimmick.Mender);
            windup.auraRadius = 1f;
            windup.auraHealPercent = 10f;
            windup.auraValue = 99f;
            SimKit.OnlyEnemies(BattleCfg, 2, windup);
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), Nuke("poke", 3f, 100f, 0.3f));
            var sim = StartSim(setup);

            Advance(sim, 0.4f);
            var hurt = SimKit.Find(sim, e => e.Hp < e.MaxHp);
            Assert.IsNotNull(hurt);
            Assert.AreEqual(70f, hurt.Hp, 6f);

            Advance(sim, 3.5f);

            Assert.AreEqual(100f, hurt.Hp, 1e-3, "초당 최대 체력 10% (auraValue 대신 auraHealPercent)");
            var heals = SimKit.Of(_log, BattleEventType.StatusApplied).FindAll(e => e.Value == (int)SkillEffectType.Heal);
            Assert.That(heals.Count, Is.InRange(5, 7));
            Assert.AreEqual(hurt.Id, heals[0].TargetId);
            Assert.AreNotEqual(hurt.Id, heals[0].SourceId, "다른 적이 고쳐 준다");
        }

        [Test]
        public void Hastener_SpeedsUpNeighbours()
        {
            float Moved(int count)
            {
                var hastener = SimKit.Enemy("windup", speed: 0.2f, gimmicks: EnemyGimmick.Hastener);
                hastener.auraRadius = 1f;
                hastener.auraValue = 100f;
                SimKit.OnlyEnemies(BattleCfg, count, hastener);
                var setup = SimKit.Setup();
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 0f));
                var sim = SimKit.Start(BattleCfg, setup, null);
                Advance(sim, 0.2f);
                var first = sim.Enemies[0];
                float start = first.Position.magnitude;
                Advance(sim, 2f);
                return start - first.Position.magnitude;
            }

            Assert.AreEqual(0.4f, Moved(1), 0.02f, "자기 자신은 빨라지지 않는다");
            Assert.AreEqual(0.8f, Moved(2), 0.02f, "곁의 태엽쥐가 감아 준다");
        }

        // ───────────────────────────── 분노 / 불빛 흡수 ─────────────────────────────

        [Test]
        public void Enrage_BelowHalfHp_HitsHarder()
        {
            var brute = SimKit.Enemy("brute", hp: 100f, speed: 1f, damage: 10f, gimmicks: EnemyGimmick.Enrage);
            SimKit.OnlyEnemies(BattleCfg, 1, brute);
            BattleCfg.enrageThreshold = 0.5f;
            BattleCfg.enrageDamageScale = 1.4f;
            var setup = SimKit.Setup(100f);
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), Nuke("poke", 6f, 100f, 0.2f));
            var sim = StartSim(setup);

            Advance(sim, 0.3f);
            var enemy = sim.Enemies[0];
            Assert.IsTrue(enemy.Enraged);
            var enraged = SimKit.Of(_log, BattleEventType.EnemyEnraged);
            Assert.AreEqual(1, enraged.Count);
            Assert.AreEqual(enemy.Id, enraged[0].TargetId);

            Advance(sim, 3f);
            var hit = SimKit.Of(_log, BattleEventType.ShopDamaged);
            Assert.AreEqual(1, hit.Count);
            Assert.AreEqual(14f, hit[0].Value, 1e-3);
        }

        [Test]
        public void LampDrain_StealsGaugeNearShopAndOnHit()
        {
            var shadow = SimKit.Enemy("shadow", hp: 1e6f, speed: 0.25f, damage: 1f, gimmicks: EnemyGimmick.LampDrain);
            SimKit.OnlyEnemies(BattleCfg, 1, shadow);
            BattleCfg.lampPerEchoDissolve = 0.9f;
            BattleCfg.lampDrainRadius = 1.3f;
            BattleCfg.lampDrainInterval = 1f;
            BattleCfg.lampDrainPerTick = 0.05f;
            BattleCfg.lampDrainOnHit = 0.2f;
            BattleCfg.startResonance = 10f;
            var setup = SimKit.Setup();
            setup.Deck.Add(SimKit.Unit("e", Stats(attack: 0f, lifetime: 1f)));
            var sim = StartSim(setup);
            Advance(sim, 0.1f);
            float laneAngle = Mathf.Atan2(sim.Enemies[0].Position.y, sim.Enemies[0].Position.x);
            Assert.IsTrue(sim.TrySummon(0, laneAngle + Mathf.PI));
            Advance(sim, 1.5f);
            Assert.AreEqual(0.9f, sim.LampGauge, 1e-3);

            Advance(sim, 8f);

            var drained = SimKit.Of(_log, BattleEventType.LampDrained);
            Assert.Greater(drained.Count, 2);
            Assert.AreEqual(0.05f, drained[0].Value, 1e-4);
            Assert.AreEqual(1, SimKit.Count(_log, BattleEventType.ShopDamaged));
            Assert.IsTrue(drained.Exists(d => Mathf.Abs(d.Value - 0.2f) < 1e-4), "수선점에 닿으면 크게 빼앗는다");
            float total = 0f;
            foreach (var d in drained) total += d.Value;
            Assert.AreEqual(0.9f - total, sim.LampGauge, 1e-3);
        }

        // ───────────────────────────── 스테이지 기믹 ─────────────────────────────

        float MovedWith(StageModifierType modifier)
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("walker", speed: 0.3f));
            var setup = SimKit.Setup();
            setup.StageModifier = modifier;
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 0f));
            var sim = SimKit.Start(BattleCfg, setup, null);
            Advance(sim, 0.1f);
            float start = sim.Enemies[0].Position.magnitude;
            Advance(sim, 2f);
            return start - sim.Enemies[0].Position.magnitude;
        }

        [Test]
        public void StageModifier_RainSlowsAndWindHastensEnemies()
        {
            BattleCfg.rainEnemySpeedScale = 0.5f;
            BattleCfg.windEnemySpeedScale = 1.5f;
            Assert.AreEqual(0.6f, MovedWith(StageModifierType.None), 0.02f);
            Assert.AreEqual(0.3f, MovedWith(StageModifierType.Rain), 0.02f);
            Assert.AreEqual(0.9f, MovedWith(StageModifierType.Wind), 0.02f);
        }

        [Test]
        public void StageModifier_TraitBonuses()
        {
            float Hit(StageModifierType modifier, params TraitType[] traits)
            {
                var log = new List<BattleEvent>();
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
                BattleCfg.rainTraitBonusPercent = 25f;
                BattleCfg.rainLightPenaltyPercent = 10f;
                BattleCfg.blackoutLightBonusPercent = 30f;
                BattleCfg.festivalTraitBonusPercent = 15f;
                var setup = SimKit.Setup();
                setup.StageModifier = modifier;
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f, range: 5f, arc: 360f), null, traits);
                var sim = SimKit.Start(BattleCfg, setup, log);
                Advance(sim, 0.2f);
                return log.Find(e => e.Type == BattleEventType.GuardianAttack).Value;
            }

            Assert.AreEqual(16f, Hit(StageModifierType.None, TraitType.Rain), 1e-3);
            Assert.AreEqual(20f, Hit(StageModifierType.Rain, TraitType.Rain), 1e-3, "비 오는 날 우두커니");
            Assert.AreEqual(14.4f, Hit(StageModifierType.Rain, TraitType.Light), 1e-3);
            Assert.AreEqual(20.8f, Hit(StageModifierType.Blackout, TraitType.Light, TraitType.Night), 1e-3);
            Assert.AreEqual(18.4f, Hit(StageModifierType.Festival, TraitType.Charm, TraitType.Sound), 1e-3, "치장/소리 둘 다여도 한 번");
        }

        [Test]
        public void StageModifier_DustStorm_MoreDustAndBiggerWaves()
        {
            int Dust(StageModifierType modifier, out int total)
            {
                var log = new List<BattleEvent>();
                SimKit.OnlyEnemies(BattleCfg, 10, SimKit.Enemy("enemy_dust", hp: 1e6f), SimKit.Enemy("other", hp: 1e6f));
                BattleCfg.dustStormEnemyId = "enemy_dust";
                BattleCfg.dustStormWeightScale = 9f;
                BattleCfg.dustStormWaveSizeScale = 1.5f;
                BattleCfg.maxEnemies = 100;
                var setup = SimKit.Setup();
                setup.StageModifier = modifier;
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 0f));
                var sim = SimKit.Start(BattleCfg, setup, log, 3);
                Advance(sim, 2f);
                total = sim.Enemies.Count;
                return SimKit.CountEnemies(sim, e => e.Def.id == "enemy_dust");
            }

            int calmDust = Dust(StageModifierType.None, out int calm);
            int stormDust = Dust(StageModifierType.DustStorm, out int storm);
            Assert.AreEqual(10, calm);
            Assert.AreEqual(15, storm, "웨이브 1.5배");
            Assert.Greater(stormDust / (float)storm, calmDust / (float)calm, "먼지뭉치가 몰려온다");
            Assert.GreaterOrEqual(stormDust, 11);
        }

        [Test]
        public void StageModifier_FullMoon_LongerEchoesAndMoreLamp()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.startResonance = 10f;
            BattleCfg.fullMoonEchoLifetimePercent = 50f;
            BattleCfg.fullMoonLampGainPercent = 100f;
            BattleCfg.lampPerEchoDissolve = 0.1f;
            var setup = SimKit.Setup();
            setup.StageModifier = StageModifierType.FullMoon;
            setup.Deck.Add(SimKit.Unit("e", Stats(lifetime: 2f)));
            var sim = StartSim(setup);

            Assert.IsTrue(sim.TrySummon(0, 0f));
            Assert.AreEqual(3f, sim.Echoes[0].MaxLife, 1e-3);
            Advance(sim, 3.5f);
            Assert.AreEqual(0.2f, sim.LampGauge, 1e-3);
        }

        [Test]
        public void StageModifier_Blackout_ShrinksRevealRadius()
        {
            bool RevealedAt(StageModifierType modifier, float radius)
            {
                SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("wisp", speed: 0.5f, gimmicks: EnemyGimmick.Phasing));
                BattleCfg.phasingRevealRadius = 0.85f;
                BattleCfg.blackoutRevealRadius = 0.5f;
                var setup = SimKit.Setup();
                setup.StageModifier = modifier;
                setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 0f));
                var sim = SimKit.Start(BattleCfg, setup, null);
                var wisp = (EnemyState)null;
                for (int i = 0; i < 400; i++)
                {
                    sim.Tick(1f / 60f);
                    if (wisp == null && sim.Enemies.Count > 0) wisp = sim.Enemies[0];
                    if (wisp != null && wisp.Position.magnitude <= radius) return wisp.Revealed;
                }
                return false;
            }

            Assert.IsTrue(RevealedAt(StageModifierType.None, 0.8f));
            Assert.IsFalse(RevealedAt(StageModifierType.Blackout, 0.8f), "정전에는 더 가까이 와야 보인다");
            Assert.IsTrue(RevealedAt(StageModifierType.Blackout, 0.45f));
        }

        [Test]
        public void StageSchedule_FollowsChapterPattern()
        {
            Assert.AreEqual(StageModifierType.None, BattleCfg.GetStageModifier(1));
            Assert.AreEqual(StageModifierType.None, BattleCfg.GetStageModifier(2));
            Assert.AreEqual(StageModifierType.None, BattleCfg.GetStageModifier(3));
            Assert.AreEqual(StageModifierType.Rain, BattleCfg.GetStageModifier(4));
            Assert.AreEqual(StageModifierType.Wind, BattleCfg.GetStageModifier(5));
            Assert.AreEqual(StageModifierType.Blackout, BattleCfg.GetStageModifier(7));
            Assert.AreEqual(StageModifierType.DustStorm, BattleCfg.GetStageModifier(8));
            Assert.AreEqual(StageModifierType.FullMoon, BattleCfg.GetStageModifier(9));
            Assert.AreEqual(StageModifierType.Festival, BattleCfg.GetStageModifier(10));
            Assert.AreEqual(StageModifierType.None, BattleCfg.GetStageModifier(11));
            Assert.AreEqual(StageModifierType.None, BattleCfg.GetStageModifier(15));
            Assert.AreEqual(StageModifierType.Festival, BattleCfg.GetStageModifier(20));
            Assert.AreEqual(StageModifierType.None, BattleCfg.GetStageModifier(0));
            Assert.AreNotEqual(BattleCfg.GetStageModifier(12), BattleCfg.GetStageModifier(22), "챕터마다 순서가 밀린다");

            var seen = new HashSet<StageModifierType>();
            for (int stage = 1; stage <= 30; stage++) seen.Add(BattleCfg.GetStageModifier(stage));
            Assert.AreEqual(7, seen.Count, "모든 기믹이 나온다");
        }

        // ───────────────────────────── 기본 데이터 / 시스템 연결 ─────────────────────────────

        [Test]
        public void DefaultEnemies_HaveGimmicksAndNewEnemies()
        {
            Assert.AreEqual(EnemyGimmick.FabricEater, BattleCfg.GetEnemy("enemy_moth").gimmicks);
            var tangle = BattleCfg.GetEnemy("enemy_tangle");
            Assert.AreEqual(EnemyGimmick.Splitter, tangle.gimmicks);
            Assert.AreEqual("enemy_tangle", tangle.splitInto);
            Assert.AreEqual(EnemyGimmick.Rusting, BattleCfg.GetEnemy("enemy_rust").gimmicks);
            Assert.AreEqual(EnemyGimmick.Phasing, BattleCfg.GetEnemy("enemy_wisp").gimmicks);
            Assert.IsTrue((BattleCfg.GetEnemy("boss_shadow").gimmicks & EnemyGimmick.LampDrain) != 0);
            Assert.IsTrue((BattleCfg.GetEnemy("boss_mothqueen").gimmicks & EnemyGimmick.Enrage) != 0);

            var thimble = BattleCfg.GetEnemy("enemy_thimble");
            Assert.IsNotNull(thimble);
            Assert.AreEqual("골무 껍데기", thimble.name);
            Assert.AreEqual(EnemyGimmick.Shielded, thimble.gimmicks);
            Assert.Greater(thimble.shieldAmount, 0f);
            var windup = BattleCfg.GetEnemy("enemy_windup");
            Assert.IsNotNull(windup);
            Assert.AreEqual("태엽쥐", windup.name);
            Assert.AreEqual(EnemyGimmick.Hastener | EnemyGimmick.Mender, windup.gimmicks);
            Assert.Greater(windup.minStage, thimble.minStage);
            foreach (var enemy in BattleCfg.enemies) Assert.IsFalse(string.IsNullOrEmpty(enemy.description), enemy.id);
        }

        [Test]
        public void BattleSystem_PassesSkillsTraitsAndStageModifier()
        {
            var form = CharDb.GetForm("sock_base");
            form.skill = SimKit.Skill("sock_family", SkillTrigger.Cooldown, SkillTargetType.EnemiesInRange, SimKit.Fx(SkillEffectType.Damage, 1f));
            CharDb.GetLineage("sock").traits = new List<TraitType> { TraitType.Fabric, TraitType.Night };
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Data.battle.stage = 4;
            Data.battle.highestStage = 4;

            Battle.Tick(0.1f);

            var unit = Battle.Sim.Setup.Guardians[0];
            Assert.AreSame(form.skill, unit.Skill);
            CollectionAssert.AreEqual(new[] { TraitType.Fabric, TraitType.Night }, unit.Traits);
            Assert.AreEqual(StageModifierType.Rain, Battle.Sim.StageModifier);
            Assert.AreEqual(StageModifierType.Rain, Battle.CurrentStageModifier);
            Assert.IsNotNull(Battle.Sim.Setup.Modifiers);
            Assert.AreSame(form.skill, Battle.Sim.GetSkill("sock_family"));
        }

        [Test]
        public void BattleSystem_FestivalGivesMoreButtons()
        {
            Assert.AreEqual(0f, Battle.GetGoldBonusPercent(9));
            Assert.AreEqual(BattleCfg.festivalGoldPercent, Battle.GetGoldBonusPercent(10), 1e-4);
            double Amount(int stage, CurrencyType currency) =>
                Battle.GetClearRewards(stage).Find(r => r.type == RewardType.Currency && r.currency == currency).amount;
            double base10 = System.Math.Floor(BattleCfg.stageGoldBase * System.Math.Pow(BattleCfg.stageGoldGrowth, 9));
            // 축제 보너스는 기준 양 B에 곱해지므로 단추 · 실타래 둘 다 오른다 (안 D)
            double income = System.Math.Floor(base10 * (1 + BattleCfg.festivalGoldPercent / 100.0));
            Assert.AreEqual(System.Math.Floor(income * 0.5), Amount(10, CurrencyType.Gold), 1e-6);
            Assert.AreEqual(System.Math.Floor(income * 0.5), Amount(10, CurrencyType.LineageXp), 1e-6);
        }
    }
}
