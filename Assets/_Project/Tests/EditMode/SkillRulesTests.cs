using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 스킬 발동 규칙: 덱 칸의 전투 시작 스킬(수선점 대리 시전), 팀 공용 보너스 스킬(발동마다 한 번), 아군 대상이 없을 때,
    /// 드러냄 뒤의 효과, 소환 시 스킬 대기, 힐/지원 성향 회복량, 수선점에서 밀려난 보스의 공격 간격.
    /// </summary>
    public class SkillRulesTests : SystemsTestBase
    {
        readonly List<BattleEvent> _log = new();

        [SetUp]
        public void SetUpLog() => _log.Clear();

        BattleSimulator StartSim(BattleSetup setup, int seed = 1) => SimKit.Start(BattleCfg, setup, _log, seed);

        static void Advance(BattleSimulator sim, float seconds) => SimKit.Advance(sim, seconds);

        static SkillDef Skill(string id, SkillTrigger trigger, SkillTargetType target, params SkillEffectDef[] effects) =>
            SimKit.Skill(id, trigger, target, effects);

        static SkillEffectDef Fx(SkillEffectType type, float value = 0f, float duration = 0f, int count = 0) => SimKit.Fx(type, value, duration, count);

        static CharacterStats Stats(CombatStyle style = CombatStyle.Assault, float attack = 10f, float interval = 1f, float range = 0.1f,
            float arc = 72f, float hp = 100f, int cost = 1, float lifetime = 30f) =>
            SimKit.Stats(style, attack, hp, interval, range, arc, cost, lifetime);

        List<BattleEvent> Casts(string key) => _log.FindAll(e => e.Type == BattleEventType.SkillCast && e.Key == key);

        // ───────────────────────────── 덱 칸의 전투 시작 스킬 ─────────────────────────────

        [Test]
        public void DeckBattleStartSkill_IsCastByTheShop_EvenWithoutGuardians()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            var wake = Skill("wake", SkillTrigger.OnBattleStart, SkillTargetType.Shop, Fx(SkillEffectType.LampGain, 0.3f));
            var setup = SimKit.Setup();
            setup.Mode = BattleMode.EchoDungeon;
            setup.Deck.Add(SimKit.Unit("e", Stats(), wake));
            var sim = StartSim(setup);

            Advance(sim, 1f);

            var casts = Casts("wake");
            Assert.AreEqual(1, casts.Count, "덱만 있는 던전에서도 전투 시작 스킬이 나간다");
            Assert.AreEqual(-1, casts[0].SourceId, "아직 소환 전이라 수선점이 대신 쓴다");
            Assert.AreEqual(0.3f, sim.LampGauge, 1e-4);
            Assert.AreEqual(0, sim.Echoes.Count, "잔향이를 불러내지는 않는다");
        }

        [Test]
        public void EchoOnlyComboBonus_BattleStart_BuffsGuardians_OnceFromTheShop()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            // 잠긴 오르골처럼 구성원이 둘 다 잔향이 덱에만 있는 조합 보너스
            var song = Skill("song", SkillTrigger.OnBattleStart, SkillTargetType.AllGuardians, Fx(SkillEffectType.BuffAttackSpeed, 100f, 100f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 1f, range: 5f, arc: 360f));
            setup.Deck.Add(SimKit.Unit("e1", Stats()));
            setup.Deck.Add(SimKit.Unit("e2", Stats()));
            setup.Modifiers.BonusSkills["e1"] = new List<SkillDef> { song };
            setup.Modifiers.BonusSkills["e2"] = new List<SkillDef> { song };
            var sim = StartSim(setup);

            Advance(sim, 4.95f);

            var casts = Casts("song");
            Assert.AreEqual(1, casts.Count, "구성원이 둘이어도 한 번");
            Assert.AreEqual(-1, casts[0].SourceId);
            Assert.AreEqual(10, SimKit.Count(_log, BattleEventType.GuardianAttack), "지킴이 공격 속도 두 배");
            Assert.AreEqual(1, _log.FindAll(e => e.Type == BattleEventType.StatusApplied && e.TargetId == 0).Count);
        }

        [Test]
        public void SharedBattleStartSkill_PrefersAGuardianOnTheField()
        {
            BattleCfg.firstWaveDelay = 1000f;
            var wall = Skill("wall", SkillTrigger.OnBattleStart, SkillTargetType.Shop, Fx(SkillEffectType.Shield, 1f, 30f));
            var setup = SimKit.Setup(1000f);
            setup.Guardians[1] = SimKit.Unit("g", Stats(attack: 10f));
            setup.Deck.Add(SimKit.Unit("g2", Stats(attack: 50f)));
            setup.Modifiers.BonusSkills["g"] = new List<SkillDef> { wall };
            setup.Modifiers.BonusSkills["g2"] = new List<SkillDef> { wall };
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            var casts = Casts("wall");
            Assert.AreEqual(1, casts.Count, "방어 친구가 여럿이어도 방벽은 한 번");
            Assert.AreEqual(1, casts[0].SourceId, "전장에 있는 지킴이가 수선점 대리보다 먼저");
            Assert.AreEqual(10f, sim.ShopShield, 1e-3);
        }

        [Test]
        public void BattleStartAllyBuff_WaitsForTheFirstEcho_WhenNobodyIsOnTheField()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            BattleCfg.dungeonStartResonance = 10f;
            // 퍼레이드처럼 모두를 강화하는 전투 시작 스킬 — 던전은 시작할 때 아무도 없다
            var parade = Skill("parade", SkillTrigger.OnBattleStart, SkillTargetType.AllAllies, Fx(SkillEffectType.BuffAttack, 25f, 20f));
            var setup = SimKit.Setup();
            setup.Mode = BattleMode.EchoDungeon;
            setup.Deck.Add(SimKit.Unit("e", Stats(cost: 1)));
            setup.Modifiers.BonusSkills["e"] = new List<SkillDef> { parade };
            var sim = StartSim(setup);

            Advance(sim, 2f);
            Assert.AreEqual(0, Casts("parade").Count, "받을 아군이 없으면 헛시전하지 않는다");

            Assert.IsTrue(sim.TrySummon(0, 0f));
            Advance(sim, 0.5f);

            Assert.AreEqual(1, Casts("parade").Count, "첫 잔향이가 나오면 그때 쓴다");
            Assert.Greater(sim.Echoes[0].BuffTimer, 15f);
            Advance(sim, 10f);
            Assert.AreEqual(1, Casts("parade").Count, "전투 시작 스킬은 한 번뿐");
        }

        // ───────────────────────────── 팀 공용 보너스 스킬 ─────────────────────────────

        [Test]
        public void SharedLampLitBonus_CastsOncePerLight_ByTheStrongestMember()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            BattleCfg.startResonance = 10f;
            var bless = Skill("bless", SkillTrigger.OnLampLit, SkillTargetType.AllAllies, Fx(SkillEffectType.BuffAttack, 25f, 10f));
            var fill = Skill("fill", SkillTrigger.OnBattleStart, SkillTargetType.Shop, Fx(SkillEffectType.LampGain, 1f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("a", Stats(attack: 10f));
            setup.Guardians[1] = SimKit.Unit("b", Stats(attack: 30f));
            setup.Guardians[2] = SimKit.Unit("c", Stats(attack: 20f));
            setup.Guardians[3] = SimKit.Unit("d", Stats(attack: 10f), fill);
            setup.Deck.Add(SimKit.Unit("a", Stats(attack: 5f, cost: 1)));
            foreach (var id in new[] { "a", "b", "c" }) setup.Modifiers.BonusSkills[id] = new List<SkillDef> { bless };
            var sim = StartSim(setup);
            Advance(sim, 0.1f);
            Assert.IsTrue(sim.TrySummon(0, 0f));
            Assert.IsTrue(sim.CanLightLamp);

            Assert.IsTrue(sim.TryLightLamp());

            var casts = Casts("bless");
            Assert.AreEqual(1, casts.Count, "일곱 빛의 축복처럼 모두를 노리는 보너스는 구성원 수와 상관없이 한 번");
            Assert.AreEqual(1, casts[0].SourceId, "공격력이 가장 높은 구성원이 쓴다");
            var buffs = _log.FindAll(e => e.Type == BattleEventType.StatusApplied && e.Value == (int)SkillEffectType.BuffAttack);
            Assert.AreEqual(5, buffs.Count, "아군마다 한 번씩만 (지킴이 4 + 잔향이 1)");
        }

        [Test]
        public void SharedLampLitBonus_FallsBackToTheShop_WhenNoMemberIsOnTheField()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            BattleCfg.startResonance = 10f;
            var chime = Skill("chime", SkillTrigger.OnLampLit, SkillTargetType.Shop, Fx(SkillEffectType.ResonanceGain, 5f));
            var fill = Skill("fill", SkillTrigger.OnBattleStart, SkillTargetType.Shop, Fx(SkillEffectType.LampGain, 1f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(), fill);
            setup.Deck.Add(SimKit.Unit("e", Stats(cost: 1)));
            setup.Modifiers.BonusSkills["e"] = new List<SkillDef> { chime };
            var sim = StartSim(setup);
            Advance(sim, 0.1f);

            Assert.IsTrue(sim.TryLightLamp());

            var casts = Casts("chime");
            Assert.AreEqual(1, casts.Count, "덱에만 있는 계보의 팀 보너스도 나간다");
            Assert.AreEqual(-1, casts[0].SourceId);
            Assert.AreEqual(15f, sim.Resonance, 1e-3);
        }

        [Test]
        public void SharedCooldownBonus_TeamSharesOneCooldown()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            var ring = Skill("ring", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 1f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("a", Stats(attack: 10f));
            setup.Guardians[1] = SimKit.Unit("b", Stats(attack: 10f));
            setup.Modifiers.BonusSkills["a"] = new List<SkillDef> { ring };
            setup.Modifiers.BonusSkills["b"] = new List<SkillDef> { ring };
            var sim = StartSim(setup);

            Advance(sim, 3f);

            Assert.AreEqual(2, Casts("ring").Count, "0.1초, 2.1초 — 구성원 둘이 따로 쓰지 않는다");
            Assert.AreEqual(1e6f - 20f, sim.Enemies[0].Hp, 0.1f);
        }

        [Test]
        public void FormSkills_AreNotShared_EvenWhenTheyHitEveryone()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            var ring = Skill("ring", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Damage, 1f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("a", Stats(attack: 10f), ring);
            setup.Guardians[1] = SimKit.Unit("b", Stats(attack: 10f), ring);
            var sim = StartSim(setup);

            Advance(sim, 0.5f);

            Assert.AreEqual(2, Casts("ring").Count, "형태 고유 스킬은 각자 쓴다");
        }

        [Test]
        public void SharedShopDamagedBonus_SharesTheMinimumInterval()
        {
            SimKit.OnlyEnemies(BattleCfg, 3, SimKit.Enemy("runner", hp: 1e6f, speed: 2f, damage: 10f));
            var patch = Skill("patch", SkillTrigger.OnShopDamaged, SkillTargetType.Shop, Fx(SkillEffectType.Heal, 0.5f));
            patch.cooldown = 30f;
            var setup = SimKit.Setup(100f);
            setup.Guardians[0] = SimKit.Unit("a", Stats(attack: 10f));
            setup.Guardians[1] = SimKit.Unit("b", Stats(attack: 10f));
            setup.Modifiers.BonusSkills["a"] = new List<SkillDef> { patch };
            setup.Modifiers.BonusSkills["b"] = new List<SkillDef> { patch };
            var sim = StartSim(setup);

            Advance(sim, 3f);

            Assert.AreEqual(3, SimKit.Count(_log, BattleEventType.ShopDamaged));
            var casts = Casts("patch");
            Assert.AreEqual(1, casts.Count, "최소 간격은 팀이 함께 쓴다");
            Assert.AreEqual(0, casts[0].SourceId, "공격력이 같으면 먼저 온 구성원");
            Assert.AreEqual(75f, sim.ShopHp, 1e-3, "100 - 10 + 5 - 10 - 10");
        }

        // ───────────────────────────── 아군 대상이 없을 때 ─────────────────────────────

        [Test]
        public void AllyOnlyCooldownSkill_WaitsForAllies_AndDoesNotFallBackToTheCaster()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1e6f, speed: 0f));
            BattleCfg.startResonance = 10f;
            // 양산 우두커니 '그늘'처럼 잔향이만 감싸는 스킬
            var shade = Skill("shade", SkillTrigger.Cooldown, SkillTargetType.AllEchoes,
                Fx(SkillEffectType.Shield, 1.5f, 6f), Fx(SkillEffectType.BuffAttack, 20f, 5f));
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 10f), shade);
            setup.Deck.Add(SimKit.Unit("e", Stats(attack: 0f, cost: 1)));
            var sim = StartSim(setup);

            Advance(sim, 1f);

            Assert.AreEqual(0, Casts("shade").Count, "잔향이가 없으면 헛시전(가짜 연출)하지 않는다");
            Assert.AreEqual(0f, sim.Guardians[0].BuffTimer, "시전자가 대신 강화를 받지 않는다");
            Assert.AreEqual(0, SimKit.Count(_log, BattleEventType.ShieldGained));

            Assert.IsTrue(sim.TrySummon(0, 0f));
            Advance(sim, 0.5f);

            Assert.AreEqual(1, Casts("shade").Count, "잔향이가 나오면 곧 쓴다");
            var echo = sim.Echoes[0];
            Assert.AreEqual(15f, echo.Shield, 1e-3);
            Assert.Greater(echo.BuffTimer, 0f);
            Assert.AreEqual(0f, sim.Guardians[0].BuffTimer);
        }

        // ───────────────────────────── 드러냄 ─────────────────────────────

        [Test]
        public void RevealFromNonLightCaster_LetsLaterEffectsHitTheRevealedEnemy()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("wisp", hp: 1000f, speed: 0.02f, gimmicks: EnemyGimmick.Phasing));
            // 마스터키 '모든 문 열기'처럼 드러낸 뒤 연쇄. 드러냄 앞의 피해는 아직 보이지 않아 빗나간다
            var master = Skill("master", SkillTrigger.Cooldown, SkillTargetType.AllEnemies,
                Fx(SkillEffectType.Damage, 1f), Fx(SkillEffectType.Reveal, 0f, 6f), Fx(SkillEffectType.Chain, 1.5f, 0f, 4));
            master.cooldown = 100f;
            master.initialDelay = 0.5f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("key", Stats(attack: 10f), master, TraitType.Metal, TraitType.Sound);
            var sim = StartSim(setup);
            Advance(sim, 0.3f);
            var wisp = sim.Enemies[0];
            Assert.IsFalse(wisp.Revealed);

            Advance(sim, 0.5f);

            Assert.AreEqual(1, Casts("master").Count);
            Assert.IsTrue(wisp.Revealed);
            Assert.AreEqual(1000f - 15f, wisp.Hp, 1e-3, "연쇄 1.5 × 공격력 10 (드러냄 앞의 피해 1.0은 빗나감)");
        }

        // ───────────────────────────── 소환 시 스킬 ─────────────────────────────

        [Test]
        public void EnemyOnlySummonSkill_WaitsBrieflyForATarget()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1000f, speed: 0f));
            BattleCfg.firstWaveDelay = 0.6f;
            BattleCfg.startResonance = 10f;
            var charge = Skill("charge", SkillTrigger.OnSummon, SkillTargetType.StrongestEnemy, Fx(SkillEffectType.Damage, 6f));
            var setup = SimKit.Setup();
            setup.Deck.Add(SimKit.Unit("rider", Stats(attack: 10f, cost: 1), charge));
            var sim = StartSim(setup);

            Assert.IsTrue(sim.TrySummon(0, 0f));
            Assert.AreEqual(0, Casts("charge").Count, "웨이브 사이라 노릴 적이 없다");

            Advance(sim, 1f);

            Assert.AreEqual(1, Casts("charge").Count, "적이 곧 나오면 그때 돌진한다");
            Assert.AreEqual(1000f - 60f, sim.Enemies[0].Hp, 1e-3);
            Advance(sim, 3f);
            Assert.AreEqual(1, Casts("charge").Count, "소환 시 스킬은 한 번뿐");
        }

        [Test]
        public void EnemyOnlySummonSkill_GivesUpAfterTheHoldWindow()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("dummy", hp: 1000f, speed: 0f));
            BattleCfg.firstWaveDelay = 3f;
            BattleCfg.startResonance = 10f;
            var charge = Skill("charge", SkillTrigger.OnSummon, SkillTargetType.StrongestEnemy, Fx(SkillEffectType.Damage, 6f));
            var setup = SimKit.Setup();
            setup.Deck.Add(SimKit.Unit("rider", Stats(attack: 10f, cost: 1, lifetime: 30f), charge));
            var sim = StartSim(setup);

            Assert.IsTrue(sim.TrySummon(0, 0f));
            Advance(sim, 5f);

            Assert.AreEqual(0, Casts("charge").Count, "한참 뒤에 나온 적에게는 쓰지 않는다");
            Assert.AreEqual(1000f, sim.Enemies[0].Hp, 1e-3);
        }

        [Test]
        public void AutoMode_DoesNotPreSummonEnemyOnlySummonSkills_BetweenWaves()
        {
            BattleCfg.firstWaveDelay = 1000f;
            BattleCfg.resonanceRegen = 0f;
            BattleCfg.startResonance = 99f;
            var charge = Skill("charge", SkillTrigger.OnSummon, SkillTargetType.StrongestEnemy, Fx(SkillEffectType.Damage, 6f));
            var setup = SimKit.Setup();
            setup.Deck.Add(SimKit.Unit("rider", Stats(cost: 1), charge));
            setup.Deck.Add(SimKit.Unit("plain", Stats(cost: 1)));
            var sim = StartSim(setup);
            sim.AutoMode = true;

            Advance(sim, 1f);

            var summons = SimKit.Of(_log, BattleEventType.EchoSummoned);
            Assert.AreEqual(1, summons.Count, "넘치는 잔향으로 미리 부르되");
            Assert.AreEqual(1f, summons[0].Value, "적을 노리는 소환 시 스킬이 있는 칸은 적이 나올 때까지 아껴 둔다");
        }

        // ───────────────────────────── 힐/지원 성향 회복량 ─────────────────────────────

        [Test]
        public void HealStylePulses_FollowAttackRamp_LikeSkills()
        {
            SimKit.OnlyEnemies(BattleCfg, 1, SimKit.Enemy("runner", hp: 1e6f, speed: 10f, damage: 1000f));
            BattleCfg.healInterval = 1f;
            BattleCfg.healShopScale = 1f;
            BattleCfg.startResonance = 10f;
            // 몽당이 '녹아드는 빛'처럼 오래 탈수록 세지는 패시브 — 회복도 더 따뜻해진다
            var melt = Skill("melt", SkillTrigger.Passive, SkillTargetType.Self, Fx(SkillEffectType.AttackRamp, 100f, 0f, 100));
            var setup = SimKit.Setup(1e5f);
            setup.Guardians[0] = SimKit.Unit("h", Stats(CombatStyle.Heal, attack: 10f), melt);
            setup.Deck.Add(SimKit.Unit("m", Stats(CombatStyle.Heal, attack: 10f, range: 5f, cost: 1), melt));
            var sim = StartSim(setup);
            Advance(sim, 0.3f);
            Assert.Less(sim.ShopHp, sim.ShopMaxHp, "수선점이 다쳐 있어야 회복이 보인다");
            Assert.IsTrue(sim.TrySummon(0, 0f));
            int echoId = sim.Echoes[0].Id;

            Advance(sim, 2.7f);

            var guardianHeals = _log.FindAll(e => e.Type == BattleEventType.ShopHealed && e.SourceId == 0);
            Assert.GreaterOrEqual(guardianHeals.Count, 2);
            Assert.AreEqual(20f, guardianHeals[guardianHeals.Count - 1].Value, 1e-3, "공격력 10 × (1 + 상승 100%)");
            var echoHeals = _log.FindAll(e => e.Type == BattleEventType.ShopHealed && e.TargetId == echoId);
            Assert.GreaterOrEqual(echoHeals.Count, 2);
            Assert.AreEqual(10f * 2f * BattleCfg.echoSupportHealScale, echoHeals[echoHeals.Count - 1].Value, 1e-3);
        }

        // ───────────────────────────── 밀려난 보스 ─────────────────────────────

        [Test]
        public void BossKnockedOffTheShop_ResumesItsSiegeTimer_InsteadOfHittingOnReturn()
        {
            var boss = SimKit.Enemy("boss", hp: 1e6f, speed: 1f, damage: 1f);
            boss.isBoss = true;
            SimKit.OnlyEnemies(BattleCfg, 1, boss);
            BattleCfg.bossSiegeInterval = 2f;
            BattleCfg.bossKnockbackScale = 0.35f;
            // 수선점에 붙은 지 0.8초 뒤 밀어낸다 (보스 0.35 밀림 → 0.35초 뒤 다시 붙음)
            var push = Skill("push", SkillTrigger.Cooldown, SkillTargetType.AllEnemies, Fx(SkillEffectType.Knockback, 1f));
            push.cooldown = 100f;
            push.initialDelay = 3f;
            var setup = SimKit.Setup();
            setup.Guardians[0] = SimKit.Unit("g", Stats(attack: 0f), push);
            var sim = new BattleSimulator(BattleCfg, 1);
            var hits = new List<float>();
            sim.Event += e =>
            {
                _log.Add(e);
                if (e.Type == BattleEventType.ShopDamaged) hits.Add(sim.ElapsedTime);
            };
            sim.Start(setup);

            Advance(sim, 5f);

            Assert.AreEqual(1, Casts("push").Count);
            Assert.AreEqual(2, hits.Count);
            float gap = hits[1] - hits[0];
            Assert.AreEqual(2f + 0.35f, gap, 0.1f, "남은 공격 간격은 밀려난 동안 멈췄다가 이어서 센다 (돌아오자마자 때리지 않는다)");
        }
    }
}
