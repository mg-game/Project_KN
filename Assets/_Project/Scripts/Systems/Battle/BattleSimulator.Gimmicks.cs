using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 적 기믹(EnemyGimmick)과 스테이지 기믹(StageModifierType).
    /// <list type="bullet">
    /// <item>Shielded: 등장할 때 shieldAmount(체력과 같이 성장) 보호막. 불 밝히기도 보호막부터 깎는다</item>
    /// <item>Splitter: 처치되면 splitInto 적 splitCount마리(IsSplit, 작고 빠르고 약함)로 나뉜다. 나뉜 적은 다시 나뉘지 않는다</item>
    /// <item>Phasing: 평소엔 보이지 않음 — 빛 특성 유닛은 보고 공격할 수 있고, 빛 특성 유닛 주변/수선점 근처/드러냄 효과/불 밝히기로 드러난다.
    /// 보이지 않는 동안 근접 잔향이에게 막히지 않는다</item>
    /// <item>FabricEater: 천 특성 잔향이에게 주는 피해 증가, 가까운 천 특성 유닛 공격력 감소(갉아먹힘)</item>
    /// <item>Rusting: auraRadius 안 금속 특성 유닛 공격 속도 auraValue% 감소 (녹)</item>
    /// <item>Mender: auraRadius 안 다른 적을 초당 최대 체력의 auraHealPercent%(없으면 auraValue%) 회복</item>
    /// <item>Hastener: auraRadius 안 다른 적 이동 속도 auraValue% 증가</item>
    /// <item>Enrage: 체력 enrageThreshold 이하에서 분노 (이동·공격 빨라지고 피해 증가, 보스는 부하를 더 자주 부름)</item>
    /// <item>LampDrain: 수선점 가까이에서 버티는 동안 / 수선점에 닿을 때 불빛 게이지를 빨아들인다</item>
    /// </list>
    /// 스테이지 기믹 수치는 BattleConfig "스테이지 기믹" 항목. 특성 시너지 배율(favoredModifier)은 SynergySystem이 따로 적용한다.
    /// </summary>
    public partial class BattleSimulator
    {
        const int MaxLightPoints = 24;
        readonly Vector2[] _lightPoints = new Vector2[MaxLightPoints];
        int _lightCount;

        // ───────────────────────────── 적 기믹 ─────────────────────────────

        void InitEnemyGimmicks(EnemyUnit unit, float splitScale)
        {
            var s = unit.State;
            var def = s.Def;
            if (unit.Has(EnemyGimmick.Shielded) && def.shieldAmount > 0f)
                s.Shield = s.MaxShield = Mathf.Max(0f, def.shieldAmount * _hpScale * splitScale);
            if (unit.Has(EnemyGimmick.Phasing)) s.Revealed = false;
            unit.AuraTick = TickSeconds;
            unit.DrainTimer = Mathf.Max(0.5f, _config.lampDrainInterval);
        }

        /// <summary>오라(회복/가속/녹/갉아먹힘), 불빛 흡수, 보이지 않는 적 드러남 판정. 이동 전에 매 진행마다</summary>
        void UpdateEnemyGimmicks(float dt)
        {
            int count = _enemyUnits.Count;
            for (int i = 0; i < count; i++) _enemyUnits[i].HasteMul = 1f;
            BuildLightPoints();
            float revealRadius = _setup.StageModifier == StageModifierType.Blackout ? _config.blackoutRevealRadius : _config.phasingRevealRadius;
            float revealRadiusSq = revealRadius * revealRadius;

            for (int i = 0; i < count; i++)
            {
                var unit = _enemyUnits[i];
                var s = unit.State;
                if (!s.Alive || unit.Gimmicks == EnemyGimmick.None) continue;

                if (unit.Has(EnemyGimmick.Phasing)) UpdatePhasing(unit, revealRadiusSq);
                if (unit.Has(EnemyGimmick.Hastener)) HastenAround(unit, i);
                if (unit.Has(EnemyGimmick.Mender))
                {
                    unit.AuraTick -= dt;
                    if (unit.AuraTick <= 0f)
                    {
                        unit.AuraTick = TickSeconds;
                        MendAround(unit, i);
                    }
                }
                if (unit.Has(EnemyGimmick.Rusting) && s.StunTimer <= 0f)
                    DebuffUnitsNear(s.Position, s.Def.auraRadius, s.Def.auraValue, true);
                if (unit.Has(EnemyGimmick.FabricEater) && s.StunTimer <= 0f)
                    DebuffUnitsNear(s.Position, _config.fabricEaterRadius, _config.fabricEaterAttackDownPercent, false);
                if (unit.Has(EnemyGimmick.LampDrain) && unit.Radius <= _config.lampDrainRadius)
                {
                    unit.DrainTimer -= dt;
                    if (unit.DrainTimer <= 0f)
                    {
                        unit.DrainTimer = Mathf.Max(0.5f, _config.lampDrainInterval);
                        DrainLamp(unit, _config.lampDrainPerTick);
                    }
                }
            }
        }

        /// <summary>빛 특성 지킴이(방위 자리) / 빛 특성 잔향이 위치 — 이 주변의 보이지 않는 적이 드러난다</summary>
        void BuildLightPoints()
        {
            _lightCount = 0;
            for (int slot = 0; slot < _guardianUnits.Length; slot++)
            {
                var g = _guardianUnits[slot];
                if (g != null && g.IsLight && _lightCount < MaxLightPoints) _lightPoints[_lightCount++] = g.RingPosition;
            }
            for (int i = 0; i < _echoUnits.Count && _lightCount < MaxLightPoints; i++)
            {
                var echo = _echoUnits[i];
                if (echo.State.Alive && echo.IsLight) _lightPoints[_lightCount++] = echo.State.Position;
            }
        }

        void UpdatePhasing(EnemyUnit unit, float revealRadiusSq)
        {
            var s = unit.State;
            bool visible = unit.RevealTimer > 0f || s.Position.sqrMagnitude <= revealRadiusSq || NearLight(s.Position);
            if (visible == s.Revealed) return;
            s.Revealed = visible;
            if (visible) Emit(new BattleEvent(BattleEventType.EnemyRevealed, -1, s.Id, s.Position));
        }

        bool NearLight(Vector2 position)
        {
            float radiusSq = _config.lightRevealRadius * _config.lightRevealRadius;
            for (int i = 0; i < _lightCount; i++)
            {
                if ((_lightPoints[i] - position).sqrMagnitude <= radiusSq) return true;
            }
            return false;
        }

        void HastenAround(EnemyUnit source, int index)
        {
            var def = source.State.Def;
            float radiusSq = def.auraRadius * def.auraRadius;
            float mul = 1f + Mathf.Max(0f, def.auraValue) / 100f;
            var center = source.State.Position;
            for (int j = 0; j < _enemyUnits.Count; j++)
            {
                if (j == index) continue;
                var other = _enemyUnits[j];
                if (!other.State.Alive || (other.State.Position - center).sqrMagnitude > radiusSq) continue;
                if (mul > other.HasteMul) other.HasteMul = mul;
            }
        }

        void MendAround(EnemyUnit source, int index)
        {
            var def = source.State.Def;
            float percent = def.auraHealPercent > 0f ? def.auraHealPercent : def.auraValue;
            if (!(percent > 0f)) return;
            float radiusSq = def.auraRadius * def.auraRadius;
            var center = source.State.Position;
            for (int j = 0; j < _enemyUnits.Count; j++)
            {
                if (j == index) continue;
                var other = _enemyUnits[j];
                var s = other.State;
                if (!s.Alive || s.Hp >= s.MaxHp || (s.Position - center).sqrMagnitude > radiusSq) continue;
                float heal = s.MaxHp * percent / 100f * TickSeconds * (s.IsBoss ? Mathf.Clamp01(_config.bossMendScale) : 1f);
                if (!(heal > 0f)) continue;
                s.Hp = Mathf.Min(s.MaxHp, s.Hp + heal);
                Emit(new BattleEvent(BattleEventType.StatusApplied, source.State.Id, s.Id, s.Position, (int)SkillEffectType.Heal, SkillVfx.PatchHeal));
            }
        }

        /// <summary>rust = true: 금속 특성 공격 속도 감소(녹), false: 천 특성 공격력 감소(갉아먹힘)</summary>
        void DebuffUnitsNear(Vector2 center, float radius, float percent, bool rust)
        {
            if (!(radius > 0f) || !(percent > 0f)) return;
            float radiusSq = radius * radius;
            float linger = Mathf.Max(0.2f, _config.debuffLingerSeconds);
            for (int slot = 0; slot < _guardianUnits.Length; slot++)
            {
                var g = _guardianUnits[slot];
                if (g == null || (rust ? !g.IsMetal : !g.IsFabric)) continue;
                if ((g.RingPosition - center).sqrMagnitude > radiusSq) continue;
                if (rust) ApplyRust(g, percent, linger);
                else ApplyGnaw(g, percent, linger);
            }
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                var echo = _echoUnits[i];
                if (!echo.State.Alive || (rust ? !echo.IsMetal : !echo.IsFabric)) continue;
                if ((echo.State.Position - center).sqrMagnitude > radiusSq) continue;
                if (rust) ApplyRust(echo, percent, linger);
                else ApplyGnaw(echo, percent, linger);
            }
        }

        void DrainLamp(EnemyUnit unit, float amount)
        {
            float drained = Mathf.Min(_lamp, Mathf.Max(0f, amount));
            if (!(drained > 0f)) return;
            _lamp -= drained;
            Emit(new BattleEvent(BattleEventType.LampDrained, unit.State.Id, -1, unit.State.Position, drained));
        }

        void CheckEnrage(EnemyUnit unit)
        {
            var s = unit.State;
            if (s.Enraged || !unit.Has(EnemyGimmick.Enrage) || !s.Alive) return;
            if (s.Hp > s.MaxHp * Mathf.Clamp01(_config.enrageThreshold)) return;
            s.Enraged = true;
            Emit(new BattleEvent(BattleEventType.EnemyEnraged, -1, s.Id, s.Position));
        }

        /// <summary>분노한 적의 피해 배율</summary>
        float EnemyRageScale(EnemyUnit unit) => unit.State.Enraged ? Mathf.Max(1f, _config.enrageDamageScale) : 1f;

        /// <summary>엉킨 실뭉치처럼 처치되면 작은 적으로 나뉜다</summary>
        void Split(EnemyUnit parent)
        {
            var def = parent.State.Def;
            var child = FindEnemyDef(def.splitInto);
            if (child == null || child.isBoss || def.splitCount <= 0) return;
            float baseAngle = Mathf.Atan2(parent.OutDirection.y, parent.OutDirection.x);
            float spread = _config.splitSpreadDegrees * Mathf.Deg2Rad;
            float radius = Mathf.Clamp(parent.Radius + 0.05f, BattleGeometry.ShopRadius + 0.1f, BattleGeometry.SpawnRadius);
            int count = def.splitCount;
            for (int i = 0; i < count && _enemyUnits.Count < _config.maxEnemies; i++)
            {
                float offset = count > 1 ? (i - (count - 1) * 0.5f) * spread : 0f;
                var unit = SpawnEnemy(child, baseAngle + offset, radius, false, true);
                Emit(new BattleEvent(BattleEventType.EnemySplit, parent.State.Id, unit.State.Id, unit.State.Position));
            }
        }

        // ───────────────────────────── 스테이지 기믹 ─────────────────────────────

        StageModifierType Stage => _setup.StageModifier;

        /// <summary>비 = 느려짐, 바람 = 빨라짐</summary>
        float StageEnemySpeedScale() => Stage switch
        {
            StageModifierType.Rain => Mathf.Max(0.1f, _config.rainEnemySpeedScale),
            StageModifierType.Wind => Mathf.Max(0.1f, _config.windEnemySpeedScale),
            _ => 1f,
        };

        /// <summary>먼지 폭풍 = 웨이브가 커진다</summary>
        float StageWaveSizeScale() => Stage == StageModifierType.DustStorm ? Mathf.Max(1f, _config.dustStormWaveSizeScale) : 1f;

        /// <summary>먼지 폭풍 = 먼지뭉치 대량, 정전 = 보이지 않는 적 증가</summary>
        float StageSpawnWeightScale(EnemyDef def)
        {
            if (Stage == StageModifierType.DustStorm && def.id == _config.dustStormEnemyId) return Mathf.Max(0f, _config.dustStormWeightScale);
            if (Stage == StageModifierType.Blackout && (def.gimmicks & EnemyGimmick.Phasing) != 0) return Mathf.Max(0f, _config.blackoutPhasingWeightScale);
            return 1f;
        }

        /// <summary>스테이지 기믹이 특성에 주는 공격력·회복·보호막 보너스(%)</summary>
        float StageTraitBonusPercent(int mask)
        {
            bool Has(TraitType t) => (mask & (1 << (int)t)) != 0;
            switch (Stage)
            {
                case StageModifierType.Rain:
                    return (Has(TraitType.Rain) ? _config.rainTraitBonusPercent : 0f) - (Has(TraitType.Light) ? _config.rainLightPenaltyPercent : 0f);
                case StageModifierType.Blackout:
                    return Has(TraitType.Light) ? _config.blackoutLightBonusPercent : 0f;
                case StageModifierType.FullMoon:
                    return Has(TraitType.Night) ? _config.fullMoonNightBonusPercent : 0f;
                case StageModifierType.DustStorm:
                    return Has(TraitType.Fabric) ? _config.dustStormFabricBonusPercent : 0f;
                case StageModifierType.Festival:
                    return Has(TraitType.Charm) || Has(TraitType.Sound) ? _config.festivalTraitBonusPercent : 0f;
                default:
                    return 0f;
            }
        }

        float StageRangePercent() => Stage == StageModifierType.DustStorm ? -Mathf.Abs(_config.dustStormRangePenaltyPercent) : 0f;

        float StageEchoLifetimePercent() => Stage == StageModifierType.FullMoon ? _config.fullMoonEchoLifetimePercent : 0f;

        float StageEchoSpeedPercent() => Stage == StageModifierType.Wind ? _config.windEchoSpeedPercent : 0f;

        float StageLampGainPercent() => Stage == StageModifierType.FullMoon ? _config.fullMoonLampGainPercent : 0f;
    }
}
