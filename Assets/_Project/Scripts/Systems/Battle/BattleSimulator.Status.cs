using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 전투 수치와 상태: 시너지 능력치(StatBlock) 반영, 강화/약화 타이머, 보호막, 회복·지속 회복, 적 상태(감속·기절·표식·지속 피해·
    /// 넉백·끌어당김·드러냄), 치명타를 포함한 피해 계산.
    /// <para>
    /// 시너지 능력치 적용 규칙: 유닛 능력치(공격력·공격 속도·체력·사거리·스킬 대기·회복/보호막 세기·기절/감속 세기·보스 피해·치명타·
    /// 잔향이 비용/유지/이동)는 팀(Team) + 그 유닛 계보(Lineage) + 그 유닛 형태(Form, 역할 시너지 구성원 전용) 합계
    /// — BattleModifiers.For(계보, 형태). 수선점/잔향/불빛/단추 같은 전체 능력치는
    /// 팀 값 + 유닛 단위 블록(계보 칸, 계보 칸 + 형태 칸) 중 가장 큰 양수 + 가장 작은 음수 (구성원 전용 시너지가 구성원 수만큼 겹쳐 쌓이지 않게) — SumGlobal.
    /// </para>
    /// <para>
    /// 적의 공격 연출(EnemyStrike): 피해는 그대로 두고 이벤트만 묶는다 — 잔향이는 적 하나당 그 적의 strikeInterval(적별 리듬)마다, 수선점은 공격마다.
    /// </para>
    /// </summary>
    public partial class BattleSimulator
    {
        /// <summary>회복 · 지속 피해 · 오라를 몰아서 적용하는 간격(초). 이벤트가 매 진행마다 쏟아지지 않게 한다</summary>
        const float TickSeconds = 0.5f;
        /// <summary>처형(Execute)이 적용되는 체력 비율</summary>
        const float ExecuteThreshold = 0.3f;

        static readonly string GuardianDebuffKey = "guardian:" + SkillVfx.DebuffMark;
        static readonly string EchoDebuffKey = "echo:" + SkillVfx.DebuffMark;

        // ── 수선점 상태 ──
        float _shopShield;
        float _shopShieldTimer;
        float _shopHotRate;
        float _shopHotTimer;
        float _shopHealAcc;
        float _shopHealTick;

        // ── 전체 능력치 (시너지 + 스테이지 기믹) ──
        float _shopMaxHpScale = 1f;
        float _shopDamageReduction;
        float _shopRegenRatio;
        float _resonanceRegenScale = 1f;
        float _lampGainScale = 1f;
        float _lampDamageScale = 1f;
        float _startResonanceBonus;
        readonly StatBlock _modScratch = new();

        // ───────────────────────────── 능력치 ─────────────────────────────

        static int TraitMaskOf(List<TraitType> traits)
        {
            int mask = 0;
            if (traits == null) return mask;
            for (int i = 0; i < traits.Count; i++) mask |= 1 << (int)traits[i];
            return mask;
        }

        static void SetTraits(CombatUnit unit, int mask)
        {
            unit.TraitMask = mask;
            unit.IsLight = unit.HasTrait(TraitType.Light);
            unit.IsMetal = unit.HasTrait(TraitType.Metal);
            unit.IsFabric = unit.HasTrait(TraitType.Fabric);
        }

        /// <summary>팀 + 계보 + 형태 시너지 능력치를 target에 채운다 (할당 없음)</summary>
        void FillMods(StatBlock target, string lineageId, string formId) => _mods.FillFor(target, lineageId, formId);

        /// <summary>
        /// 수선점·잔향·불빛·단추처럼 유닛에 속하지 않는 능력치의 합계.
        /// 팀 값 + 유닛 단위 블록 중 가장 큰 양수 + 가장 작은 음수 (구성원 전용 시너지가 구성원 수만큼 겹치지 않게).
        /// 유닛 단위 블록 = 계보 칸 하나, 또는 형태 칸 + 그 형태의 계보 칸 (FormLineage).
        /// </summary>
        public static float SumGlobal(BattleModifiers modifiers, StatType stat)
        {
            if (modifiers == null) return 0f;
            float total = modifiers.Team[stat];
            float best = 0f, worst = 0f;
            foreach (var pair in modifiers.Lineage)
            {
                if (pair.Value == null) continue;
                float v = pair.Value[stat];
                if (v > best) best = v;
                if (v < worst) worst = v;
            }
            foreach (var pair in modifiers.Form)
            {
                if (pair.Value == null) continue;
                float v = pair.Value[stat];
                if (modifiers.FormLineage.TryGetValue(pair.Key, out var lineageId) && lineageId != null
                    && modifiers.Lineage.TryGetValue(lineageId, out var own) && own != null)
                    v += own[stat];
                if (v > best) best = v;
                if (v < worst) worst = v;
            }
            return total + best + worst;
        }

        float Global(StatType stat) => SumGlobal(_mods, stat);

        void ComputeGlobalStats()
        {
            _shopMaxHpScale = Mathf.Max(0.1f, 1f + Global(StatType.ShopMaxHpPercent) / 100f);
            _shopDamageReduction = Mathf.Clamp(Global(StatType.ShopDamageReductionPercent), 0f, Mathf.Clamp(_config.damageReductionCap, 0f, 95f)) / 100f;
            _shopRegenRatio = Mathf.Max(0f, Global(StatType.ShopRegenPercentPerSecond)) / 100f;
            _resonanceRegenScale = Mathf.Max(0f, 1f + Global(StatType.ResonanceRegenPercent) / 100f);
            _lampGainScale = Mathf.Max(0f, 1f + (Global(StatType.LampGainPercent) + StageLampGainPercent()) / 100f);
            _lampDamageScale = Mathf.Max(0f, 1f + Global(StatType.LampDamagePercent) / 100f);
            _startResonanceBonus = Global(StatType.StartResonanceFlat);
        }

        /// <summary>시너지·스테이지 기믹·공격력 배율을 유닛 수치에 반영 (버프/타이머는 건드리지 않는다)</summary>
        void RefreshCombatStats(CombatUnit u)
        {
            var mods = u.Mods;
            FillMods(mods, u.LineageId, u.FormId);
            var stats = u.Stats;
            float stageBonus = StageTraitBonusPercent(u.TraitMask);
            u.SkillAttack = Mathf.Max(0f, stats.attack * AttackMultiplier * Mathf.Max(0.1f, 1f + (mods[StatType.AttackPercent] + stageBonus) / 100f));
            u.HealPower = Mathf.Max(0f, 1f + (mods[StatType.HealPowerPercent] + stageBonus) / 100f);
            u.ShieldPower = Mathf.Max(0f, 1f + (mods[StatType.ShieldPowerPercent] + stageBonus) / 100f);
            u.StunPower = Mathf.Max(0f, 1f + mods[StatType.StunDurationPercent] / 100f);
            u.SlowPower = Mathf.Max(0f, 1f + mods[StatType.SlowPowerPercent] / 100f);
            u.BossDamage = Mathf.Max(0f, 1f + mods[StatType.BossDamagePercent] / 100f);
            u.CritChance = Mathf.Clamp01((_config.baseCritChance + mods[StatType.CritChancePercent]) / 100f);
            u.CritDamage = Mathf.Max(0f, (_config.baseCritDamage + mods[StatType.CritDamagePercent]) / 100f);
            float cdr = Mathf.Clamp(mods[StatType.SkillCooldownReductionPercent], 0f, Mathf.Clamp(_config.cooldownReductionCap, 0f, 90f));
            u.CooldownScale = 1f - cdr / 100f;
            u.AttackSpeedScale = Mathf.Max(0.2f, 1f + mods[StatType.AttackSpeedPercent] / 100f);
            u.RangeScale = Mathf.Max(0.3f, 1f + (mods[StatType.RangePercent] + StageRangePercent()) / 100f);
            if (u is EchoUnit echo)
            {
                echo.HpScale = Mathf.Max(0.1f, 1f + mods[StatType.HpPercent] / 100f);
                echo.LifetimeScale = Mathf.Max(0.1f, 1f + (mods[StatType.EchoLifetimePercent] + StageEchoLifetimePercent()) / 100f);
                echo.MoveScale = Mathf.Max(0.1f, 1f + (mods[StatType.EchoMoveSpeedPercent] + StageEchoSpeedPercent()) / 100f);
            }
        }

        /// <summary>시너지 소환 비용 감소 반영 (원래 1 이상이면 1 아래로 내려가지 않는다)</summary>
        int EchoCost(int baseCost, string lineageId, string formId)
        {
            FillMods(_modScratch, lineageId, formId);
            int reduction = Mathf.RoundToInt(_modScratch[StatType.EchoCostReductionFlat]);
            return Math.Max(Math.Min(baseCost, 1), baseCost - reduction);
        }

        static bool CanSee(CombatUnit unit, EnemyUnit enemy) => enemy.State.Revealed || unit.IsLight;

        // ───────────────────────────── 수선점 ─────────────────────────────

        void ResetShopStatus()
        {
            _shopShield = 0f;
            _shopShieldTimer = 0f;
            _shopHotRate = 0f;
            _shopHotTimer = 0f;
            _shopHealAcc = 0f;
            _shopHealTick = TickSeconds;
        }

        void UpdateShopStatus(float dt)
        {
            if (_shopShieldTimer > 0f)
            {
                _shopShieldTimer -= dt;
                if (_shopShieldTimer <= 0f)
                {
                    _shopShieldTimer = 0f;
                    _shopShield = 0f;
                }
            }
            if (_shopRegenRatio > 0f) _shopHealAcc += _shopMaxHp * _shopRegenRatio * dt;
            if (_shopHotTimer > 0f)
            {
                _shopHealAcc += _shopHotRate * Mathf.Min(dt, _shopHotTimer);
                _shopHotTimer -= dt;
                if (_shopHotTimer <= 0f)
                {
                    _shopHotTimer = 0f;
                    _shopHotRate = 0f;
                }
            }
            _shopHealTick -= dt;
            if (_shopHealTick > 0f) return;
            _shopHealTick = TickSeconds;
            if (_shopHealAcc > 0f) HealShop(_shopHealAcc, -1, -1, Vector2.zero);
            _shopHealAcc = 0f;
        }

        void HealShop(float amount, int guardianSlot, int echoId, Vector2 position)
        {
            float healed = Mathf.Min(Mathf.Max(0f, amount), _shopMaxHp - _shopHp);
            if (!(healed > 0f)) return;
            _shopHp += healed;
            Emit(new BattleEvent(BattleEventType.ShopHealed, guardianSlot, echoId, position, healed));
        }

        /// <summary>수선점 보호막 (최대 내구도 × shopShieldCapRatio까지 쌓인다)</summary>
        void AddShopShield(float amount, float duration, int sourceId, string vfx)
        {
            if (!(amount > 0f)) return;
            float cap = _shopMaxHp * Mathf.Max(0.05f, _config.shopShieldCapRatio);
            float before = _shopShield;
            _shopShield = Mathf.Min(cap, _shopShield + amount);
            _shopShieldTimer = Mathf.Max(_shopShieldTimer, duration > 0f ? duration : _config.defaultShieldSeconds);
            float gained = _shopShield - before;
            if (gained > 0f) Emit(new BattleEvent(BattleEventType.ShieldGained, sourceId, -1, Vector2.zero, gained, vfx));
        }

        void AddShopHot(float perSecond, float duration)
        {
            if (!(perSecond > 0f) || !(duration > 0f)) return;
            if (_shopHotTimer <= 0f || perSecond >= _shopHotRate) _shopHotRate = perSecond;
            _shopHotTimer = Mathf.Max(_shopHotTimer, duration);
        }

        /// <summary>
        /// 적이 수선점에 닿음: 피해 감소(시너지) → 보호막 흡수 → 내구도. 공격 연출(EnemyStrike)은 언제나 먼저 나오고,
        /// 내구도가 실제로 깎였을 때만 ShopDamaged와 '수선점 피격 시' 스킬이 나온다. 불빛을 빨아들이는 적(LampDrain)은 닿을 때마다 게이지를 빼앗는다.
        /// </summary>
        void DamageShop(EnemyUnit unit)
        {
            float damage = unit.Damage * EnemyRageScale(unit) * (1f - _shopDamageReduction);
            EmitStrike(unit, -1, unit.OutDirection * BattleGeometry.ShopRadius, Mathf.Max(0f, damage));
            if (_shopShield > 0f && damage > 0f)
            {
                float absorbed = Mathf.Min(_shopShield, damage);
                _shopShield -= absorbed;
                damage -= absorbed;
                if (_shopShield <= 0.0001f)
                {
                    _shopShield = 0f;
                    _shopShieldTimer = 0f;
                    Emit(new BattleEvent(BattleEventType.ShieldBroken, unit.State.Id, -1, unit.State.Position));
                }
            }
            if (unit.Has(EnemyGimmick.LampDrain)) DrainLamp(unit, _config.lampDrainOnHit);
            if (damage > 0f)
            {
                float next = _shopHp - damage;
                // 튜토리얼 보호: 실패 판정 전에 바닥(ShopFloorHp)에서 멈춘다 (이미 바닥 아래면 더 깎지 않는다)
                if (_protShopFloor > 0f && next < _protShopFloor) next = Mathf.Min(_shopHp, _protShopFloor);
                _shopHp = Mathf.Max(0f, next);
                Emit(new BattleEvent(BattleEventType.ShopDamaged, unit.State.Id, -1, unit.State.Position, damage));
                if (_shopHp <= 0f)
                {
                    Fail();
                    return;
                }
                QueueTriggerAll(SkillTrigger.OnShopDamaged);
                if (!_lowHpFired && _shopHp < _shopMaxHp * 0.3f)
                {
                    _lowHpFired = true;
                    QueueTriggerAll(SkillTrigger.OnShopLowHp);
                }
            }
        }

        // ───────────────────────────── 아군 상태 ─────────────────────────────

        /// <summary>강화/약화/정화 타이머, 공격력 상승(AttackRamp)</summary>
        static void UpdateUnitStatus(CombatUnit u, float dt)
        {
            if (u.AttackBuffTimer > 0f && (u.AttackBuffTimer -= dt) <= 0f)
            {
                u.AttackBuffTimer = 0f;
                u.AttackBuff = 0f;
            }
            if (u.SpeedBuffTimer > 0f && (u.SpeedBuffTimer -= dt) <= 0f)
            {
                u.SpeedBuffTimer = 0f;
                u.SpeedBuff = 0f;
            }
            if (u.RustTimer > 0f && (u.RustTimer -= dt) <= 0f)
            {
                u.RustTimer = 0f;
                u.RustPercent = 0f;
            }
            if (u.GnawTimer > 0f && (u.GnawTimer -= dt) <= 0f)
            {
                u.GnawTimer = 0f;
                u.GnawPercent = 0f;
            }
            if (u.CleanseTimer > 0f) u.CleanseTimer = Mathf.Max(0f, u.CleanseTimer - dt);
            if (u.RampPerSecond > 0f && u.Ramp < u.RampCap) u.Ramp = Mathf.Min(u.RampCap, u.Ramp + u.RampPerSecond * dt);
        }

        /// <summary>잔향이 보호막 유지 시간, 지속 회복</summary>
        void UpdateEchoStatus(EchoUnit unit, float dt)
        {
            var s = unit.State;
            if (unit.ShieldTimer > 0f && (unit.ShieldTimer -= dt) <= 0f)
            {
                unit.ShieldTimer = 0f;
                s.Shield = 0f;
            }
            if (unit.HotTimer <= 0f) return;
            unit.HotAcc += unit.HotRate * Mathf.Min(dt, unit.HotTimer);
            unit.HotTimer -= dt;
            unit.HotTick -= dt;
            if (unit.HotTick > 0f && unit.HotTimer > 0f) return;
            unit.HotTick = TickSeconds;
            HealEcho(unit, unit.HotAcc, unit.HealSourceId);
            unit.HotAcc = 0f;
            if (unit.HotTimer <= 0f)
            {
                unit.HotTimer = 0f;
                unit.HotRate = 0f;
            }
        }

        static bool ApplyAttackBuff(CombatUnit u, float percent, float duration)
        {
            if (!(percent > 0f) || !(duration > 0f)) return false;
            if (u.AttackBuffTimer > 0f && percent < u.AttackBuff) return false;
            if (u.AttackBuffTimer <= 0f || percent > u.AttackBuff) u.AttackBuffTimer = duration;
            else u.AttackBuffTimer = Mathf.Max(u.AttackBuffTimer, duration);
            u.AttackBuff = percent;
            return true;
        }

        static bool ApplySpeedBuff(CombatUnit u, float percent, float duration)
        {
            if (!(percent > 0f) || !(duration > 0f)) return false;
            if (u.SpeedBuffTimer > 0f && percent < u.SpeedBuff) return false;
            if (u.SpeedBuffTimer <= 0f || percent > u.SpeedBuff) u.SpeedBuffTimer = duration;
            else u.SpeedBuffTimer = Mathf.Max(u.SpeedBuffTimer, duration);
            u.SpeedBuff = percent;
            return true;
        }

        /// <summary>약화(녹·갉아먹힘) 제거 + duration초 면역</summary>
        static void Cleanse(CombatUnit u, float duration)
        {
            u.RustTimer = u.RustPercent = 0f;
            u.GnawTimer = u.GnawPercent = 0f;
            u.CleanseTimer = Mathf.Max(u.CleanseTimer, Mathf.Max(0.5f, duration));
        }

        /// <summary>녹: 금속 특성 유닛 공격 속도 감소</summary>
        void ApplyRust(CombatUnit u, float percent, float duration)
        {
            if (u.CleanseTimer > 0f || !(percent > 0f)) return;
            bool fresh = u.RustTimer <= 0f;
            u.RustPercent = fresh ? percent : Mathf.Max(u.RustPercent, percent);
            u.RustTimer = Mathf.Max(u.RustTimer, duration);
            if (fresh) EmitUnitStatus(u, SkillEffectType.Slow);
        }

        /// <summary>갉아먹힘: 천 특성 유닛 공격력 감소 (좀나방)</summary>
        void ApplyGnaw(CombatUnit u, float percent, float duration)
        {
            if (u.CleanseTimer > 0f || !(percent > 0f)) return;
            bool fresh = u.GnawTimer <= 0f;
            u.GnawPercent = fresh ? percent : Mathf.Max(u.GnawPercent, percent);
            u.GnawTimer = Mathf.Max(u.GnawTimer, duration);
            if (fresh) EmitUnitStatus(u, SkillEffectType.Mark);
        }

        void EmitUnitStatus(CombatUnit u, SkillEffectType type)
        {
            bool echo = u is EchoUnit;
            Emit(new BattleEvent(BattleEventType.StatusApplied, -1, u.SourceId, u.Position, (int)type, echo ? EchoDebuffKey : GuardianDebuffKey));
        }

        void HealEcho(EchoUnit unit, float amount, int sourceId)
        {
            var s = unit.State;
            if (!s.Alive) return;
            float healed = Mathf.Min(Mathf.Max(0f, amount), s.MaxHp - s.Hp);
            if (!(healed > 0f)) return;
            s.Hp += healed;
            Emit(new BattleEvent(BattleEventType.EchoHealed, sourceId, s.Id, s.Position, healed));
        }

        /// <summary>잔향이 보호막 (최대 체력까지 쌓인다)</summary>
        void AddEchoShield(EchoUnit unit, float amount, float duration, int sourceId, string vfx)
        {
            var s = unit.State;
            if (!s.Alive || !(amount > 0f)) return;
            float before = s.Shield;
            s.Shield = Mathf.Min(s.MaxHp, s.Shield + amount);
            unit.ShieldTimer = Mathf.Max(unit.ShieldTimer, duration > 0f ? duration : _config.defaultShieldSeconds);
            float gained = s.Shield - before;
            if (gained > 0f) Emit(new BattleEvent(BattleEventType.ShieldGained, sourceId, s.Id, s.Position, gained, vfx));
        }

        static void AddEchoHot(EchoUnit unit, float perSecond, float duration, int sourceId)
        {
            if (!unit.State.Alive || !(perSecond > 0f) || !(duration > 0f)) return;
            if (unit.HotTimer <= 0f || perSecond >= unit.HotRate)
            {
                unit.HotRate = perSecond;
                unit.HealSourceId = sourceId;
            }
            if (unit.HotTimer <= 0f) unit.HotTick = TickSeconds;
            unit.HotTimer = Mathf.Max(unit.HotTimer, duration);
        }

        void BuffEchoesNear(Vector2 center, float radius, float percent, float duration)
        {
            float radiusSq = radius * radius;
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                var echo = _echoUnits[i];
                if (echo.State.Alive && (echo.State.Position - center).sqrMagnitude <= radiusSq) ApplyAttackBuff(echo, percent, duration);
            }
        }

        void HealEchoesNear(Vector2 center, float radius, float amount, int sourceId)
        {
            float radiusSq = radius * radius;
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                var echo = _echoUnits[i];
                if (echo.State.Alive && (echo.State.Position - center).sqrMagnitude <= radiusSq) HealEcho(echo, amount, sourceId);
            }
        }

        /// <summary>
        /// 적이 잔향이에게 주는 피해 (보호막이 먼저 흡수). 좀나방(FabricEater)은 천 특성 잔향이에게 더 아프다.
        /// 발이 묶여 싸우는 동안 매 진행 불린다 — 공격 연출(EnemyStrike)은 TrackEchoStrike가 간격마다 묶어서 낸다.
        /// </summary>
        void DamageEcho(EchoUnit echo, EnemyUnit source, float amount)
        {
            if (source.Has(EnemyGimmick.FabricEater) && echo.IsFabric) amount *= Mathf.Max(1f, _config.fabricEaterEchoScale);
            if (!(amount > 0f)) return;
            var s = echo.State;
            TrackEchoStrike(source, s, amount);
            if (s.Shield > 0f)
            {
                float absorbed = Mathf.Min(s.Shield, amount);
                s.Shield -= absorbed;
                amount -= absorbed;
                if (s.Shield <= 0.0001f)
                {
                    s.Shield = 0f;
                    echo.ShieldTimer = 0f;
                    Emit(new BattleEvent(BattleEventType.ShieldBroken, source.State.Id, s.Id, s.Position));
                }
            }
            s.Hp -= amount;
        }

        /// <summary>
        /// 이 적의 잔향이 공격 연출 간격(초): 적 정의의 strikeInterval (ATK-03 적별 리듬 — 좀나방 0.6초 ~ 녹얼룩 1.4초, 보스 2초).
        /// 비었거나 잘못되면 BattleConfig.enemyStrikeInterval, 그것도 잘못되면 0.5초. 한 진행보다 짧아지지 않는다
        /// </summary>
        float StrikeIntervalOf(EnemyUnit enemy)
        {
            var def = enemy.State.Def;
            float fallback = SafePositive(_config.enemyStrikeInterval, 0.5f);
            float interval = def != null ? SafePositive(def.strikeInterval, fallback) : fallback;
            return Mathf.Max(StepSeconds, interval);
        }

        /// <summary>
        /// 발이 묶인 적의 잔향이 공격 연출 묶음: 피해는 쌓아 두고, 적 하나당 그 적의 간격(StrikeIntervalOf)에 한 번만 EnemyStrike를 낸다
        /// (붙자마자 — 지난 공격에서 간격이 지났으면 — 한 번, 그 뒤로 간격마다). 대상이 바뀌면 쌓인 피해를 비운다. 할당 없음
        /// </summary>
        void TrackEchoStrike(EnemyUnit enemy, EchoState echo, float amount)
        {
            if (enemy.StrikeEchoId != echo.Id)
            {
                enemy.StrikeEchoId = echo.Id;
                enemy.StrikeAcc = 0f;
            }
            enemy.StrikeAcc += amount;
            // 진행 간격 절반만큼 여유를 둬서 부동소수 오차로 한 진행 늦어지지 않게
            if (_time + StepSeconds * 0.5f < enemy.StrikeReadyTime) return;
            enemy.StrikeReadyTime = _time + StrikeIntervalOf(enemy);
            float value = enemy.StrikeAcc;
            enemy.StrikeAcc = 0f;
            EmitStrike(enemy, echo.Id, (enemy.State.Position + echo.Position) * 0.5f, value);
        }

        /// <summary>적의 공격 연출 이벤트 (TargetId = 잔향이 ID 또는 -1 수선점, Key = 적 정의 ID) + AttackAnim</summary>
        void EmitStrike(EnemyUnit enemy, int targetId, Vector2 point, float damage)
        {
            var e = enemy.State;
            e.AttackAnim = 1f;
            Emit(new BattleEvent(BattleEventType.EnemyStrike, e.Id, targetId, point, damage, e.Def != null ? e.Def.id : null));
        }

        // ───────────────────────────── 적 상태 ─────────────────────────────

        /// <summary>감속 (reduction = 줄어드는 비율 0~1). 더 센 감속이 덮어쓴다</summary>
        bool ApplySlow(EnemyUnit unit, float reduction, float duration, bool bossScale)
        {
            var s = unit.State;
            if (bossScale && s.IsBoss) reduction *= Mathf.Clamp01(_config.bossSlowScale);
            reduction = Mathf.Clamp(reduction, 0f, Mathf.Clamp(_config.maxSlow, 0f, 0.95f));
            if (!(reduction > 0f) || !(duration > 0f)) return false;
            float mul = 1f - reduction;
            if (s.SlowTimer <= 0f || mul < unit.SlowMul - 0.0001f)
            {
                unit.SlowMul = mul;
                s.SlowTimer = Mathf.Max(s.SlowTimer, duration);
                return true;
            }
            if (mul <= unit.SlowMul + 0.0001f) s.SlowTimer = Mathf.Max(s.SlowTimer, duration);
            return true;
        }

        /// <summary>기절 (보스는 bossStunScale 배, 시전자 StunDurationPercent 반영)</summary>
        void Stun(EnemyUnit unit, float seconds, CombatUnit source)
        {
            var s = unit.State;
            float stun = seconds * (s.IsBoss ? _config.bossStunScale : 1f) * (source?.StunPower ?? 1f);
            if (stun > 0f) s.StunTimer = Mathf.Max(s.StunTimer, stun);
        }

        static void ApplyMark(EnemyUnit unit, float percent, float duration)
        {
            var s = unit.State;
            if (!(percent > 0f) || !(duration > 0f)) return;
            if (s.MarkTimer > 0f && percent < unit.MarkPercent) return;
            unit.MarkPercent = percent;
            s.MarkTimer = Mathf.Max(s.MarkTimer, duration);
        }

        static void ApplyBurn(EnemyUnit unit, float perSecond, float duration, CombatUnit source)
        {
            var s = unit.State;
            if (!(perSecond > 0f) || !(duration > 0f)) return;
            if (s.BurnTimer <= 0f || perSecond >= unit.BurnDps)
            {
                unit.BurnDps = perSecond;
                unit.BurnSource = source;
            }
            if (s.BurnTimer <= 0f) unit.BurnTick = TickSeconds;
            s.BurnTimer = Mathf.Max(s.BurnTimer, duration);
        }

        /// <summary>
        /// 수선점 바깥쪽으로 밀어냄 (보스는 bossKnockbackScale 배). 수선점에 붙어 있던 보스도 떨어진다 —
        /// 남은 공격 간격(SiegeTimer)은 그대로 멈춰 두었다가 다시 붙으면 이어서 센다 (돌아오자마자 때리지 않는다).
        /// </summary>
        void Knockback(EnemyUnit unit, float distance)
        {
            if (unit.State.IsBoss) distance *= Mathf.Clamp01(_config.bossKnockbackScale);
            if (!(distance > 0f)) return;
            unit.Radius = Mathf.Min(BattleGeometry.SpawnRadius, unit.Radius + distance);
            unit.AtShop = false;
            unit.State.Position = unit.OutDirection * unit.Radius;
        }

        /// <summary>point 쪽으로 끌어당김. 적은 끌려간 자리에서 다시 수선점을 향해 직진한다 (보스의 공격 간격은 넉백과 같이 이어서 센다)</summary>
        void Pull(EnemyUnit unit, Vector2 point, float distance)
        {
            var s = unit.State;
            if (s.IsBoss) distance *= Mathf.Clamp01(_config.bossKnockbackScale);
            if (!(distance > 0f)) return;
            var delta = point - s.Position;
            float dist = delta.magnitude;
            if (dist < 0.02f) return;
            var next = s.Position + delta / dist * Mathf.Min(distance, dist * 0.9f);
            float radius = next.magnitude;
            float minRadius = BattleGeometry.ShopRadius + 0.08f;
            if (radius < minRadius)
            {
                next = radius > 0.0001f ? next / radius * minRadius : unit.OutDirection * minRadius;
                radius = minRadius;
            }
            unit.OutDirection = next / radius;
            unit.Radius = Mathf.Min(BattleGeometry.SpawnRadius, radius);
            unit.Lane = BattleGeometry.NearestSlot(Mathf.Atan2(unit.OutDirection.y, unit.OutDirection.x));
            unit.AtShop = false;
            s.Position = unit.OutDirection * unit.Radius;
        }

        /// <summary>보이지 않는 적(Phasing)을 duration초 드러냄</summary>
        void Reveal(EnemyUnit unit, float duration)
        {
            if (!unit.Has(EnemyGimmick.Phasing)) return;
            unit.RevealTimer = Mathf.Max(unit.RevealTimer, duration);
            var s = unit.State;
            if (s.Revealed) return;
            s.Revealed = true;
            Emit(new BattleEvent(BattleEventType.EnemyRevealed, -1, s.Id, s.Position));
        }

        /// <summary>감속·기절·표식·드러냄 타이머, 지속 피해</summary>
        void UpdateEnemyStatus(EnemyUnit unit, float dt)
        {
            var s = unit.State;
            if (s.SlowTimer > 0f && (s.SlowTimer = Mathf.Max(0f, s.SlowTimer - dt)) <= 0f) unit.SlowMul = 1f;
            if (s.StunTimer > 0f) s.StunTimer = Mathf.Max(0f, s.StunTimer - dt);
            if (s.MarkTimer > 0f && (s.MarkTimer = Mathf.Max(0f, s.MarkTimer - dt)) <= 0f) unit.MarkPercent = 0f;
            if (unit.RevealTimer > 0f) unit.RevealTimer = Mathf.Max(0f, unit.RevealTimer - dt);
            if (s.BurnTimer <= 0f) return;

            unit.BurnAcc += unit.BurnDps * Mathf.Min(dt, s.BurnTimer);
            s.BurnTimer = Mathf.Max(0f, s.BurnTimer - dt);
            unit.BurnTick -= dt;
            if (unit.BurnTick > 0f && s.BurnTimer > 0f) return;
            unit.BurnTick = TickSeconds;
            float damage = unit.BurnAcc;
            unit.BurnAcc = 0f;
            var source = unit.BurnSource;
            if (s.BurnTimer <= 0f)
            {
                unit.BurnDps = 0f;
                unit.BurnSource = null;
            }
            if (damage > 0f) HitEnemy(unit, damage, source, -1, false, 0f);
        }

        // ───────────────────────────── 피해 계산 ─────────────────────────────

        /// <summary>
        /// 유닛이 적에게 주는 피해: 보스 피해 → 처형(체력 30% 이하) → 치명타 → 표식 순으로 곱한 뒤 DamageEnemy.
        /// hitSourceId는 EnemyHit의 SourceId (지킴이 기본 공격만 슬롯, 나머지 -1).
        /// </summary>
        void HitEnemy(EnemyUnit target, float damage, CombatUnit source, int hitSourceId, bool canCrit, float extraExecute)
        {
            var s = target.State;
            if (!s.Alive || !(damage > 0f)) return;
            bool crit = false;
            if (source != null)
            {
                if (s.IsBoss) damage *= source.BossDamage;
                float execute = source.ExecutePercent + extraExecute;
                if (execute > 0f && s.Hp <= s.MaxHp * ExecuteThreshold) damage *= 1f + execute / 100f;
                if (canCrit && source.CritChance > 0f && _combatRandom.NextDouble() < source.CritChance)
                {
                    damage *= 1f + source.CritDamage;
                    crit = true;
                }
            }
            if (s.MarkTimer > 0f) damage *= 1f + target.MarkPercent / 100f;
            if (crit) Emit(new BattleEvent(BattleEventType.CriticalHit, source.SourceId, s.Id, s.Position, damage));
            DamageEnemy(target, damage, hitSourceId, source, false);
        }
    }
}
