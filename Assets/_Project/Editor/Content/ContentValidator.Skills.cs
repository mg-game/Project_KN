using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 스킬 · 시너지 · 계보 특성 · 적 기믹 검사.
    ///   오류: 전투가 잘못 동작하는 데이터 (빈 스킬, 없는 연출 키, 패시브 전용 효과를 다른 발동에 씀, 패시브 오라가 다루지 않는 효과,
    ///         지속 시간/값/횟수 0, 스킬 ID 중복, 시너지 ID 중복, 없는 조합 계보, 단계가 오름차순이 아님, 특성/역할 시너지 누락, 없는 분열/부하 적 …)
    ///   경고: 동작은 하지만 손봐야 하는 것 (설명/대사 없음, 대상과 효과의 편이 엇갈림, 추천 역할에서 발동하지 않는 스킬,
    ///         연출 없이 조용히 걸리는 패시브 오라, 구성원마다 같은 효과가 겹쳐 반복되는 보너스 스킬,
    ///         도달할 수 없는 단계, 구성원 전용 단계의 팀 능력치, 전투에서 재생되지 않는 연출 키/쓰지 않는 발동 시점, 아트 누락 …)
    /// </summary>
    internal static partial class ContentValidator
    {
        static readonly HashSet<string> VfxKeys = new(SkillVfx.All);

        // ───────────────────────── 계보 특성 ─────────────────────────

        static void CheckTraits(ContentReport r, string at, LineageDef lineage)
        {
            var traits = Safe(lineage.traits).ToList();
            if (traits.Count == 0)
            {
                r.Error($"{at}: 특성(traits)이 없습니다. 특성 시너지에 쓰입니다.");
                return;
            }
            foreach (var trait in traits)
            {
                if (!Enum.IsDefined(typeof(TraitType), trait)) r.Error($"{at}: 알 수 없는 특성 값 {(int)trait}");
            }
            if (traits.Distinct().Count() != traits.Count) r.Error($"{at}: 같은 특성이 두 번 들어 있습니다 ({string.Join(", ", traits)}).");
        }

        // ───────────────────────── 형태 고유 스킬 ─────────────────────────

        static void CheckFormSkills(ContentReport r, GameContent c)
        {
            foreach (var form in AllForms(c))
            {
                string at = $"형태 '{form.id}' 스킬";
                var skill = form.skill;
                if (skill == null || skill.IsEmpty)
                {
                    r.Error($"{at}: 고유 스킬이 비어 있습니다 (SkillContent 표에 추가하세요).");
                    continue;
                }
                string expected = SkillContent.IdFor(form.id);
                if (skill.id != expected) r.Warn($"{at}: ID '{skill.id}'이(가) 규칙('{expected}')과 다릅니다.");
                CheckSkill(r, at, skill);

                if (form.recommendedRole == CharacterRole.Guardian && IsEchoOnly(skill.trigger))
                    r.Warn($"{at}: 추천 역할은 지킴이인데 잔향이일 때만 발동하는 {skill.trigger} 스킬입니다.");
                if (form.recommendedRole == CharacterRole.Echo && skill.trigger == SkillTrigger.OnBattleStart)
                    r.Warn($"{at}: 추천 역할은 잔향이인데 전투 시작 스킬은 지킴이만 발동합니다.");
            }
        }

        /// <summary>스킬 하나의 수치/연출/대상 검사 (형태 스킬 · 시너지 보너스 스킬 공용)</summary>
        static void CheckSkill(ContentReport r, string at, SkillDef skill)
        {
            if (string.IsNullOrEmpty(skill.id)) r.Error($"{at}: 스킬 ID가 비어 있습니다.");
            if (string.IsNullOrEmpty(skill.name)) r.Error($"{at}: 스킬 이름이 비어 있습니다.");
            if (string.IsNullOrEmpty(skill.description)) r.Warn($"{at}: 설명이 비어 있습니다.");
            if (string.IsNullOrEmpty(skill.castText)) r.Warn($"{at}: 시전 대사(castText)가 비어 있습니다.");
            if (string.IsNullOrEmpty(skill.vfx)) r.Error($"{at}: 연출 키(vfx)가 비어 있습니다.");
            else if (!VfxKeys.Contains(skill.vfx)) r.Error($"{at}: SkillVfx에 없는 연출 키 '{skill.vfx}'");

            if (!Enum.IsDefined(typeof(SkillTrigger), skill.trigger)) r.Error($"{at}: 알 수 없는 발동 시점 {(int)skill.trigger}");
            if (!Enum.IsDefined(typeof(SkillTargetType), skill.target)) r.Error($"{at}: 알 수 없는 대상 {(int)skill.target}");
            CheckTiming(r, at, skill);
            if (UsesRadius(skill.target) && skill.radius <= 0f) r.Error($"{at}: 대상 {skill.target}은(는) 반지름(radius)이 0보다 커야 합니다.");

            var effects = skill.effects ?? new List<SkillEffectDef>();
            if (effects.Count == 0) r.Error($"{at}: 효과가 없습니다.");
            if (skill.trigger == SkillTrigger.Passive && effects.Any(e => e != null && !IsPassiveOnly(e.type) && !IsNotAura(e.type)))
                r.Warn($"{at}: Passive 오라는 시전(SkillCast) 이벤트 없이 조용히 걸려, 전투에서 연출과 대사가 나오지 않습니다 " +
                       $"(연출 키 {skill.vfx} — 보이는 효과가 필요하면 쿨타임 스킬로 바꾸세요).");
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                string where = $"{at} 효과 {i + 1}";
                if (effect == null)
                {
                    r.Error($"{where}: 비어 있는(null) 효과");
                    continue;
                }
                if (!Enum.IsDefined(typeof(SkillEffectType), effect.type))
                {
                    r.Error($"{where}: 알 수 없는 효과 {(int)effect.type}");
                    continue;
                }
                CheckEffect(r, where, skill, effect);
            }
        }

        static void CheckTiming(ContentReport r, string at, SkillDef skill)
        {
            if (skill.cooldown < 0f || skill.initialDelay < 0f) r.Error($"{at}: 쿨타임/첫 시전 지연이 음수입니다.");
            switch (skill.trigger)
            {
                case SkillTrigger.Cooldown:
                    if (skill.cooldown <= 0f) r.Error($"{at}: 쿨타임 스킬의 cooldown이 0 이하입니다.");
                    break;
                case SkillTrigger.OnKill:
                case SkillTrigger.OnShopDamaged:
                    if (skill.cooldown <= 0f) r.Warn($"{at}: {skill.trigger} 스킬의 최소 간격(cooldown)이 0이라 매번 터집니다.");
                    break;
            }
        }

        static void CheckEffect(ContentReport r, string at, SkillDef skill, SkillEffectDef e)
        {
            var type = e.type;
            if (IsPassiveOnly(type) && skill.trigger != SkillTrigger.Passive)
                r.Error($"{at}: {type}은(는) Passive 스킬 전용인데 발동 시점이 {skill.trigger}입니다.");
            // Passive 스킬의 다른 효과는 전투가 0.5초마다 조용히 다시 거는 오라다 (BattleSimulator.PassivePulse).
            // 오라가 다루지 않는 효과(보호막 · 기절 · 넉백 · 끌어당김 · 연쇄 · 소환)는 아무 일도 일어나지 않는다.
            if (skill.trigger == SkillTrigger.Passive && IsNotAura(type))
                r.Error($"{at}: Passive 스킬의 {type}은(는) 상시 오라로 걸리지 않아 전투에서 아무 효과가 없습니다 (발동 시점을 바꾸세요).");

            if (NeedsDuration(type) && e.duration <= 0f) r.Error($"{at}: {type}의 지속 시간(duration)이 0 이하입니다.");
            if (NeedsValue(type) && e.value <= 0f) r.Error($"{at}: {type}의 값(value)이 0 이하입니다.");
            if (NeedsCount(type) && e.count <= 0) r.Error($"{at}: {type}의 횟수/최대치(count)가 0 이하입니다.");
            if (type == SkillEffectType.Slow && e.value > 1f) r.Error($"{at}: Slow 값 {e.value}은(는) 0~1 사이 비율이어야 합니다.");
            if (type == SkillEffectType.LampGain && e.value > 1f) r.Warn($"{at}: LampGain {e.value}은(는) 게이지 가득(1)보다 큽니다.");
            if (type == SkillEffectType.Reveal && skill.radius <= 0f) r.Error($"{at}: Reveal은 반지름(radius) 안의 적을 드러내므로 radius가 0보다 커야 합니다.");

            // 대상과 효과의 편이 엇갈리면 전투는 "중심 반지름 안의 적" / "시전자 자신"으로 바꿔 적용한다 → 의도 확인용 경고
            bool enemyTarget = IsEnemyTarget(skill.target);
            if (IsEnemyEffect(type) && !enemyTarget)
                r.Warn($"{at}: 적에게 거는 {type}을(를) 아군 대상({skill.target})에 써서, 시전 중심 반지름 {skill.radius} 안의 적에게 갑니다.");
            if (IsAllyEffect(type) && enemyTarget)
                r.Warn($"{at}: 아군에게 거는 {type}을(를) 적 대상({skill.target})에 써서, 시전자 자신에게 갑니다.");
            if (type == SkillEffectType.Cleanse && skill.target == SkillTargetType.Shop)
                r.Warn($"{at}: 수선점에는 약화가 걸리지 않아 Cleanse가 의미 없습니다.");
        }

        // ───────────────────────── 스킬 ID / 분포 ─────────────────────────

        /// <summary>형태 스킬 + 보너스 스킬 ID는 전투 중 SkillCast 이벤트의 Key라 전부 달라야 한다</summary>
        static void CheckSkillIds(ContentReport r, GameContent c)
        {
            var owners = new Dictionary<string, string>();
            foreach (var (owner, skill) in AllSkills(c))
            {
                if (string.IsNullOrEmpty(skill.id)) continue;
                if (owners.TryGetValue(skill.id, out var first)) r.Error($"스킬 ID 중복 '{skill.id}': {first} / {owner}");
                else owners[skill.id] = owner;
            }
        }

        /// <summary>
        /// 콘텐츠 설계 기준: 연출 키 33개를 모두 전투에서 재생되게 쓰고, 형태 스킬은 발동 시점을 고르게 섞는다 (경고).
        /// 패시브 스킬은 시전 이벤트가 없어 연출이 재생되지 않으므로(캐릭터 화면 아이콘으로만 쓰인다) 연출 키를 세지 않는다.
        /// </summary>
        static void CheckSkillCoverage(ContentReport r, GameContent c)
        {
            var skills = AllSkills(c).Select(x => x.skill).ToList();
            if (skills.Count == 0) return;

            var cast = skills.Where(s => s.trigger != SkillTrigger.Passive).ToList();
            var unusedVfx = SkillVfx.All.Where(key => !cast.Any(s => s.vfx == key)).ToList();
            if (unusedVfx.Count > 0)
            {
                var passiveOnly = unusedVfx.Where(key => skills.Any(s => s.vfx == key)).ToList();
                string note = passiveOnly.Count > 0 ? $" (패시브 스킬에만 쓰여 재생되지 않는 키: {string.Join(", ", passiveOnly)})" : "";
                r.Warn($"전투에서 재생되지 않는 스킬 연출 키 {unusedVfx.Count}개: {string.Join(", ", unusedVfx)}{note}");
            }

            var formSkills = AllForms(c).Select(f => f.skill).Where(s => s != null && !s.IsEmpty).ToList();
            var unusedTriggers = Enum.GetValues(typeof(SkillTrigger)).Cast<SkillTrigger>()
                .Where(t => !formSkills.Any(s => s.trigger == t)).ToList();
            if (formSkills.Count > 0 && unusedTriggers.Count > 0)
                r.Warn($"형태 스킬이 쓰지 않는 발동 시점: {string.Join(", ", unusedTriggers)}");
        }

        // ───────────────────────── 시너지 ─────────────────────────

        static void CheckSynergies(ContentReport r, GameContent c)
        {
            if (c.Synergies == null)
            {
                r.Error("SynergyDatabase 에셋이 없습니다.");
                return;
            }
            var synergies = Safe(c.Synergies.synergies).ToList();
            if (synergies.Count == 0)
            {
                r.Error("시너지가 하나도 없습니다.");
                return;
            }
            CheckUnique(r, "시너지", synergies, s => s.id);

            var lineages = Safe(c.Characters?.lineages).Where(l => l != null).ToList();
            var lineageIds = new HashSet<string>(lineages.Select(l => l.id));
            foreach (var def in synergies.Where(s => s != null))
            {
                string at = $"시너지 '{def.id}'";
                if (string.IsNullOrEmpty(def.name)) r.Error($"{at}: 이름이 비어 있습니다.");
                if (string.IsNullOrEmpty(def.description)) r.Warn($"{at}: 설명이 비어 있습니다.");
                if (!Enum.IsDefined(typeof(StageModifierType), def.favoredModifier)) r.Error($"{at}: 알 수 없는 스테이지 기믹 {(int)def.favoredModifier}");
                else if (def.favoredModifier != StageModifierType.None && def.favoredMultiplier <= 1f)
                    r.Warn($"{at}: {def.favoredModifier} 기믹 배율이 {def.favoredMultiplier}라 강화되지 않습니다.");

                CheckTiers(r, at, def);
                switch (def.kind)
                {
                    case SynergyKind.Trait: CheckTraitSynergy(r, at, def, lineages); break;
                    case SynergyKind.Style: CheckStyleSynergy(r, at, def, lineages); break;
                    case SynergyKind.Combo: CheckComboSynergy(r, at, def, lineageIds); break;
                    default: r.Error($"{at}: 알 수 없는 시너지 종류 {(int)def.kind}"); break;
                }
            }
            CheckSynergyCoverage(r, synergies);
        }

        static void CheckTiers(ContentReport r, string at, SynergyDef def)
        {
            var tiers = def.tiers ?? new List<SynergyTier>();
            if (tiers.Count == 0)
            {
                r.Error($"{at}: 단계(tiers)가 없습니다.");
                return;
            }

            int previous = 0;
            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                string where = $"{at} {i + 1}단계";
                if (tier == null)
                {
                    r.Error($"{where}: 비어 있는(null) 단계");
                    continue;
                }
                if (tier.count < 1) r.Error($"{where}: 필요 수(count)가 1보다 작습니다.");
                else if (tier.count <= previous) r.Error($"{where}: 필요 수 {tier.count}이(가) 앞 단계({previous})보다 커야 합니다 (오름차순).");
                previous = Math.Max(previous, tier.count);
                if (string.IsNullOrEmpty(tier.description)) r.Warn($"{where}: 단계 설명이 비어 있습니다.");

                var modifiers = tier.modifiers ?? new List<StatModifier>();
                foreach (var modifier in modifiers)
                {
                    if (modifier == null)
                    {
                        r.Error($"{where}: 비어 있는(null) 능력치");
                        continue;
                    }
                    if (!Enum.IsDefined(typeof(StatType), modifier.stat)) r.Error($"{where}: 알 수 없는 능력치 {(int)modifier.stat}");
                    else if (tier.membersOnly && SynergyText.IsTeamStat(modifier.stat))
                        r.Warn($"{where}: 구성원 전용 단계에 팀 전체 능력치 {modifier.stat}이(가) 들어 있습니다 (membersOnly = false로 두세요).");
                    if (modifier.value == 0f) r.Warn($"{where}: 능력치 {modifier.stat} 값이 0입니다.");
                }
                var stats = modifiers.Where(m => m != null).Select(m => m.stat).ToList();
                if (stats.Distinct().Count() != stats.Count) r.Warn($"{where}: 같은 능력치가 두 번 들어 있습니다.");

                bool hasBonus = HasBonusSkill(tier);
                if (hasBonus)
                {
                    CheckSkill(r, $"{where} 보너스 스킬", tier.bonusSkill);
                    CheckBonusSkillStacking(r, $"{where} 보너스 스킬", tier.bonusSkill);
                    // 역할 보너스 스킬은 형태 단위로 시전된다 — "…계보의 친구마다" 문구는 같은 계보의 다른 성향 형태도 쓰는 것처럼 읽힌다
                    if (def.kind == SynergyKind.Style && tier.bonusSkill.description != null && tier.bonusSkill.description.Contains("계보의"))
                        r.Warn($"{where} 보너스 스킬: 역할 보너스 스킬은 그 성향 형태만 쓰는데 설명이 계보 전체가 쓰는 것처럼 읽힙니다 ('계보의').");
                }
                if (modifiers.Count == 0 && !hasBonus) r.Warn($"{where}: 능력치도 보너스 스킬도 없습니다.");
            }
        }

        /// <summary>
        /// 보너스 스킬은 구성원 유닛마다 따로 시전된다 (SynergySystem.BuildModifiers → BattleSimulator.CollectSkillInfos) —
        /// 특성/조합 시너지는 구성원 계보의 유닛마다, 역할 시너지는 그 성향 형태의 유닛마다만 (FormBonusSkills, 형태 단위).
        /// 여러 유닛이 같은 순간(전투 시작 · 불 밝히기 · 피격 · 위기 · 같은 쿨타임 · 상시 오라)에 같은 대상(팀 전체 · 모든 적 · 가장 강한 적)에게
        /// 겹치지 않는 효과(강화 · 정화 · 지속 회복 · 표식 · 기절 · 감속 · 화상 · 드러냄)를 걸면, 효과는 한 번뿐인데 연출 · 대사만 여러 번 나온다 (경고).
        /// </summary>
        static void CheckBonusSkillStacking(ContentReport r, string at, SkillDef skill)
        {
            if (!FiresTogether(skill.trigger) || !IsSharedTarget(skill.target)) return;
            var repeated = Safe(skill.effects).Where(e => e != null && !Stacks(e.type)).Select(e => e.type).Distinct().ToList();
            if (repeated.Count == 0) return;
            r.Warn($"{at}: 구성원마다 같은 순간({skill.trigger})에 같은 대상({skill.target})으로 시전되는데, " +
                   $"{string.Join(", ", repeated)}은(는) 겹치지 않아 효과 없이 연출만 반복됩니다 " +
                   "(자기 범위 대상으로 바꾸거나 단계 능력치로 옮기세요).");
        }

        /// <summary>구성원 여럿이 같은 순간에 시전하게 되는 발동 시점 (처치 시 · 소환 시 · 소멸 시는 유닛마다 따로 일어난다)</summary>
        static bool FiresTogether(SkillTrigger trigger) => trigger switch
        {
            SkillTrigger.Cooldown or SkillTrigger.Passive or SkillTrigger.OnBattleStart or SkillTrigger.OnLampLit
                or SkillTrigger.OnShopDamaged or SkillTrigger.OnShopLowHp => true,
            _ => false,
        };

        /// <summary>시전자가 누구든 같은 대상이 되는 대상 종류</summary>
        static bool IsSharedTarget(SkillTargetType target) => target switch
        {
            SkillTargetType.Shop or SkillTargetType.AllAllies or SkillTargetType.AllGuardians or SkillTargetType.AllEchoes
                or SkillTargetType.AllEnemies or SkillTargetType.StrongestEnemy or SkillTargetType.WeakestEnemy => true,
            _ => false,
        };

        /// <summary>여러 번 걸면 그만큼 더해지는 효과 (나머지는 센 쪽 하나만 남거나 시간만 이어진다 — BattleSimulator.Status)</summary>
        static bool Stacks(SkillEffectType type) => type switch
        {
            SkillEffectType.Damage or SkillEffectType.Heal or SkillEffectType.Shield or SkillEffectType.Knockback
                or SkillEffectType.Pull or SkillEffectType.Chain or SkillEffectType.Summon or SkillEffectType.ResonanceGain
                or SkillEffectType.LampGain or SkillEffectType.Execute or SkillEffectType.AttackRamp => true,
            _ => false,
        };

        static void CheckTraitSynergy(ContentReport r, string at, SynergyDef def, List<LineageDef> lineages)
        {
            string expected = $"trait_{def.trait.ToString().ToLowerInvariant()}";
            if (def.id != expected) r.Warn($"{at}: 특성 시너지 ID는 '{expected}' 규칙을 따릅니다 (아이콘 Art/Icons/{expected}.png).");
            if (Safe(def.lineageIds).Any()) r.Warn($"{at}: 특성 시너지의 lineageIds는 쓰지 않습니다.");

            // 같은 계보는 지킴이 1 + 잔향이 1까지라 계보당 최대 2로 센다
            int max = lineages.Count(l => Safe(l.traits).Contains(def.trait)) * 2;
            if (max == 0) r.Warn($"{at}: {def.trait} 특성을 가진 계보가 없습니다.");
            else WarnUnreachable(r, at, def, max);
        }

        static void CheckStyleSynergy(ContentReport r, string at, SynergyDef def, List<LineageDef> lineages)
        {
            string expected = $"style_{def.style.ToString().ToLowerInvariant()}";
            if (def.id != expected) r.Warn($"{at}: 역할 시너지 ID는 '{expected}' 규칙을 따릅니다 (아이콘 Art/Icons/{expected}.png).");
            if (Safe(def.lineageIds).Any()) r.Warn($"{at}: 역할 시너지의 lineageIds는 쓰지 않습니다.");

            int max = lineages.Sum(l => Math.Min(2, Safe(l.forms).Count(f => f != null && f.style == def.style)));
            if (max == 0) r.Warn($"{at}: {def.style} 성향 형태가 없습니다.");
            else WarnUnreachable(r, at, def, max);
        }

        static void WarnUnreachable(ContentReport r, string at, SynergyDef def, int max)
        {
            foreach (var tier in Safe(def.tiers).Where(t => t != null && t.count > max))
                r.Warn($"{at}: {tier.count}단계는 도달할 수 없습니다 (최대 {max}).");
        }

        static void CheckComboSynergy(ContentReport r, string at, SynergyDef def, HashSet<string> lineageIds)
        {
            var ids = Safe(def.lineageIds).ToList();
            if (ids.Count < 2) r.Error($"{at}: 조합 계보(lineageIds)가 2개 이상이어야 합니다.");
            foreach (var id in ids)
            {
                if (!lineageIds.Contains(id ?? "")) r.Error($"{at}: CharacterDatabase에 없는 조합 계보 '{id}'");
            }
            if (ids.Distinct().Count() != ids.Count) r.Error($"{at}: 같은 계보가 두 번 들어 있습니다.");

            var tiers = Safe(def.tiers).ToList();
            if (tiers.Count > 1) r.Warn($"{at}: 조합은 첫 단계(tiers[0])만 씁니다 (현재 {tiers.Count}단계).");
            if (tiers.Count > 0 && tiers[0] != null && tiers[0].count != ids.Count)
                r.Warn($"{at}: 조합 단계 수({tiers[0].count})가 계보 수({ids.Count})와 다릅니다.");
        }

        /// <summary>모든 특성/성향에 시너지가 하나씩 있어야 한다 (편성 화면이 특성/역할 칩마다 시너지를 찾는다)</summary>
        static void CheckSynergyCoverage(ContentReport r, List<SynergyDef> synergies)
        {
            var valid = synergies.Where(s => s != null).ToList();
            foreach (TraitType trait in Enum.GetValues(typeof(TraitType)))
            {
                int count = valid.Count(s => s.kind == SynergyKind.Trait && s.trait == trait);
                if (count == 0) r.Error($"특성 {trait}의 시너지가 없습니다.");
                else if (count > 1) r.Warn($"특성 {trait}의 시너지가 {count}개입니다.");
            }
            foreach (CombatStyle style in Enum.GetValues(typeof(CombatStyle)))
            {
                int count = valid.Count(s => s.kind == SynergyKind.Style && s.style == style);
                if (count == 0) r.Error($"성향 {style}의 역할 시너지가 없습니다.");
                else if (count > 1) r.Warn($"성향 {style}의 역할 시너지가 {count}개입니다.");
            }

            var combos = valid.Where(s => s.kind == SynergyKind.Combo).ToList();
            var seen = new Dictionary<string, string>();
            foreach (var combo in combos)
            {
                string key = string.Join("+", Safe(combo.lineageIds).OrderBy(id => id));
                if (seen.TryGetValue(key, out var first)) r.Warn($"조합 '{first}'와 '{combo.id}'의 계보 구성이 같습니다.");
                else seen[key] = combo.id;
            }
        }

        // ───────────────────────── 적 기믹 ─────────────────────────

        static void CheckEnemyGimmicks(ContentReport r, string at, EnemyDef enemy, BattleConfig battle)
        {
            var g = enemy.gimmicks;
            if ((g & EnemyGimmick.Splitter) != 0)
            {
                // 나뉜 적(IsSplit)은 다시 나뉘지 않으므로 자기 자신으로 나뉘는 것(엉킨 실뭉치 → 작은 실뭉치)은 괜찮다
                var child = string.IsNullOrEmpty(enemy.splitInto) ? null : battle.GetEnemy(enemy.splitInto);
                if (string.IsNullOrEmpty(enemy.splitInto)) r.Error($"{at}: 분열(Splitter) 기믹인데 splitInto가 비어 있습니다.");
                else if (child == null) r.Error($"{at}: BattleConfig에 없는 분열 적 '{enemy.splitInto}'");
                else if (child.isBoss) r.Error($"{at}: 보스('{child.id}')로는 분열하지 않습니다.");
                if (enemy.splitCount <= 0) r.Error($"{at}: 분열 수(splitCount)가 0 이하입니다.");
            }
            if ((g & EnemyGimmick.Shielded) != 0 && enemy.shieldAmount <= 0f) r.Warn($"{at}: 보호막(Shielded) 기믹인데 shieldAmount가 0입니다.");

            var aura = EnemyGimmick.Mender | EnemyGimmick.Hastener | EnemyGimmick.Rusting;
            if ((g & aura) != 0 && (enemy.auraRadius <= 0f || enemy.auraValue <= 0f))
                r.Warn($"{at}: 오라 기믹({g & aura})인데 auraRadius/auraValue가 0 이하입니다.");

            if (!string.IsNullOrEmpty(enemy.minionId))
            {
                if (battle.GetEnemy(enemy.minionId) == null) r.Error($"{at}: BattleConfig에 없는 부하 적 '{enemy.minionId}'");
                if (enemy.minionCount <= 0 || enemy.minionInterval <= 0f) r.Warn($"{at}: 부하 수/간격이 0 이하라 부하가 나오지 않습니다.");
            }
        }

        // ───────────────────────── 아트 (경고) ─────────────────────────

        /// <summary>시너지 아이콘: 특성/역할은 Art/Icons/{trait_|style_}*.png, 조합은 Art/Synergy/{id}.png (콘텐츠 갱신 때 연결)</summary>
        static void WarnMissingSynergyIcons(ContentReport r, GameContent c)
        {
            var synergies = Safe(c.Synergies?.synergies).Where(s => s != null).ToList();
            var shared = synergies.Where(s => s.kind != SynergyKind.Combo).ToList();
            var combos = synergies.Where(s => s.kind == SynergyKind.Combo).ToList();
            WarnMissingArt(r, "특성/역할 아이콘", "Icons", shared.Count, shared.Where(s => s.icon == null).Select(s => s.id));
            WarnMissingArt(r, "조합 아이콘", "Synergy", combos.Count, combos.Where(s => s.icon == null).Select(s => s.id));
        }

        /// <summary>스킬 연출 / 스테이지 기믹 덮개·아이콘 (전투 화면이 직접 불러오고, 없으면 FX 흰색 스프라이트에 색을 입혀 대체)</summary>
        static void CheckSkillArt(ContentReport r, GameContent c)
        {
            var keys = AllSkills(c).Select(x => x.skill.vfx).Where(k => VfxKeys.Contains(k ?? "")).Distinct().OrderBy(k => k).ToList();
            WarnMissingArt(r, "스킬 연출", "VFX", keys.Count, keys.Where(k => ArtLib.Vfx(k) == null));

            var modifiers = Enum.GetValues(typeof(StageModifierType)).Cast<StageModifierType>().Where(m => m != StageModifierType.None).ToList();
            WarnMissingArt(r, "스테이지 덮개", "VFX", modifiers.Count,
                modifiers.Select(SkillVfx.Overlay).Where(key => ArtLib.Vfx(key) == null));
            WarnMissingArt(r, "스테이지 기믹 아이콘", "Icons", modifiers.Count,
                modifiers.Where(m => ArtLib.StageModifier(m) == null).Select(m => $"stage_{m.ToString().ToLowerInvariant()}"));
        }

        // ───────────────────────── 공통 ─────────────────────────

        static IEnumerable<CharacterFormDef> AllForms(GameContent c) =>
            Safe(c.Characters?.lineages).Where(l => l != null).SelectMany(l => Safe(l.forms)).Where(f => f != null);

        /// <summary>형태 고유 스킬 + 시너지 단계 보너스 스킬 (빈 스킬 제외). owner = 오류 문구용 위치</summary>
        static IEnumerable<(string owner, SkillDef skill)> AllSkills(GameContent c)
        {
            foreach (var form in AllForms(c))
            {
                if (form.skill != null && !form.skill.IsEmpty) yield return ($"형태 '{form.id}'", form.skill);
            }
            foreach (var def in Safe(c.Synergies?.synergies).Where(s => s != null))
            {
                var tiers = def.tiers ?? new List<SynergyTier>();
                for (int i = 0; i < tiers.Count; i++)
                {
                    if (tiers[i] != null && HasBonusSkill(tiers[i])) yield return ($"시너지 '{def.id}' {i + 1}단계", tiers[i].bonusSkill);
                }
            }
        }

        /// <summary>
        /// 보너스 스킬이 있는 단계인지. 유니티 직렬화는 null 필드도 빈 SkillDef로 채우므로 null 검사만으로는 부족하다.
        /// </summary>
        static bool HasBonusSkill(SynergyTier tier) =>
            tier.bonusSkill != null && (!tier.bonusSkill.IsEmpty || !string.IsNullOrEmpty(tier.bonusSkill.name));

        static bool IsEchoOnly(SkillTrigger trigger) => trigger == SkillTrigger.OnSummon || trigger == SkillTrigger.OnDissolve;

        static bool IsPassiveOnly(SkillEffectType type) => type == SkillEffectType.Execute || type == SkillEffectType.AttackRamp;

        /// <summary>Passive 상시 오라(BattleSimulator.PassivePulse)가 다루지 않는 효과 — Passive 스킬에 넣으면 아무 일도 없다</summary>
        static bool IsNotAura(SkillEffectType type) => type switch
        {
            SkillEffectType.Shield or SkillEffectType.Stun or SkillEffectType.Knockback or SkillEffectType.Pull
                or SkillEffectType.Chain or SkillEffectType.Summon => true,
            _ => false,
        };

        static bool UsesRadius(SkillTargetType target) =>
            target == SkillTargetType.EnemiesNearSelf || target == SkillTargetType.EnemiesNearTarget || target == SkillTargetType.AlliesNearSelf;

        static bool IsEnemyTarget(SkillTargetType target) => target switch
        {
            SkillTargetType.EnemiesInRange or SkillTargetType.EnemiesNearSelf or SkillTargetType.EnemiesNearTarget
                or SkillTargetType.StrongestEnemy or SkillTargetType.WeakestEnemy or SkillTargetType.AllEnemies
                or SkillTargetType.Lane => true,
            _ => false,
        };

        /// <summary>적에게만 의미가 있는 효과</summary>
        static bool IsEnemyEffect(SkillEffectType type) => type switch
        {
            SkillEffectType.Damage or SkillEffectType.Slow or SkillEffectType.Stun or SkillEffectType.Knockback
                or SkillEffectType.Pull or SkillEffectType.Mark or SkillEffectType.DamageOverTime or SkillEffectType.Chain => true,
            _ => false,
        };

        /// <summary>아군(수선점/잔향이/지킴이)에게만 의미가 있는 효과</summary>
        static bool IsAllyEffect(SkillEffectType type) => type switch
        {
            SkillEffectType.Heal or SkillEffectType.Shield or SkillEffectType.BuffAttack or SkillEffectType.BuffAttackSpeed
                or SkillEffectType.Cleanse or SkillEffectType.HealOverTime => true,
            _ => false,
        };

        /// <summary>지속 시간(duration)이 있어야 하는 효과</summary>
        static bool NeedsDuration(SkillEffectType type) => type switch
        {
            SkillEffectType.Shield or SkillEffectType.Slow or SkillEffectType.Stun or SkillEffectType.Mark
                or SkillEffectType.BuffAttack or SkillEffectType.BuffAttackSpeed or SkillEffectType.Summon
                or SkillEffectType.Reveal or SkillEffectType.DamageOverTime or SkillEffectType.Cleanse
                or SkillEffectType.HealOverTime => true,
            _ => false,
        };

        /// <summary>값(value)이 있어야 하는 효과 (Stun/Reveal/Cleanse는 지속 시간만 쓴다)</summary>
        static bool NeedsValue(SkillEffectType type) => type switch
        {
            SkillEffectType.Stun or SkillEffectType.Reveal or SkillEffectType.Cleanse => false,
            _ => true,
        };

        /// <summary>count가 있어야 하는 효과 (Summon 마리 수 / Chain 튀는 횟수 / AttackRamp 최대 %)</summary>
        static bool NeedsCount(SkillEffectType type) =>
            type == SkillEffectType.Summon || type == SkillEffectType.Chain || type == SkillEffectType.AttackRamp;
    }
}
