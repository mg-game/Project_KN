using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 형태 고유 스킬 + 시너지 보너스 스킬 (BattleModifiers.BonusSkills[계보] — 특성/조합, FormBonusSkills[형태] — 역할).
    /// <para>
    /// 발동: Passive(처형/공격력 상승은 상시, 그 밖의 효과는 0.5초마다 조용히 다시 거는 오라), Cooldown(대상이 있을 때만, 없으면 잠시 뒤 재시도),
    /// OnSummon/OnDissolve(잔향이일 때만. 적만 노리는 소환 시 스킬은 대상이 없으면 1.5초 동안 기다렸다 쓴다),
    /// OnKill/OnShopDamaged(cooldown = 최소 간격), OnLampLit, OnShopLowHp(전투 중 1회, 수선점 30% 미만),
    /// OnBattleStart(첫 진행 때 1회: 지킴이는 자기 자리에서, 덱 칸의 스킬은 아직 소환 전이므로 수선점이 대신 쓴다.
    /// 아군 효과만 있는데 받을 아군이 아직 없으면 — 덱만 있는 던전 — 전투 시작 10초 안에 첫 아군이 나올 때 쓴다).
    /// 시전은 모두 대기열에 넣었다가 진행 단계 사이에 처리한다 (처치 중 처치 시 스킬 같은 겹침에도 대상 목록이 섞이지 않게).
    /// </para>
    /// <para>
    /// 팀 공용 스킬: 형태 고유 스킬이 아닌 보너스 스킬(시너지)이 팀 전체를 노리면(Shop/AllEchoes/AllGuardians/AllAllies/AllEnemies)
    /// 구성원 계보의 유닛이 여럿이어도 발동 한 번에 한 번만 나간다 — 시전자는 전장에 있는 구성원 중 공격력이 가장 높은 유닛,
    /// 전장에 구성원이 없으면 덱 칸 대신 수선점이(SourceId -1). 쿨타임·최소 간격·1회 제한도 팀이 함께 쓴다.
    /// </para>
    /// <para>
    /// 대상 규칙: EnemiesNearTarget은 처치 시 스킬이면 쓰러진 적 자리, 아니면 지금 노리는 적 주변.
    /// 적 효과(피해·감속·기절·넉백·끌어당김·표식·지속 피해·연쇄·드러냄)는 대상 종류가 적이면 그 적들,
    /// 아군 대상(Self/Shop/아군 범위)이면 그 중심(수선점 또는 시전자) radius 안의 적에게 간다.
    /// 아군 효과(회복·보호막·지속 회복·강화·정화)는 대상 종류가 아군이면 그 아군, 적이면 시전자 자신에게 간다.
    /// 지킴이는 체력이 없고 수선점과 한 몸이라, 지킴이에게 가는 회복/보호막/지속 회복은 수선점으로 간다 (한 번만).
    /// 아군 효과만 있는 스킬은 받을 아군이 없으면 시전하지 않는다 (쿨타임 스킬은 잠시 뒤 다시).
    /// 보이지 않는 적(Phasing)은 빛 특성 시전자만 노릴 수 있다 — 드러냄은 예외이고, 드러냄 뒤에 오는 효과는 방금 드러난 적에게도 간다.
    /// </para>
    /// <para>
    /// 이벤트: SkillCast(SourceId = 지킴이 슬롯 / 잔향이 ID / 수선점이 대신 쓰면 -1, Position = 효과 중심,
    /// 레인 스킬은 시전자 위치 — 방향은 수선점→Position), 적 상태는 StatusApplied(TargetId = 적),
    /// 아군 강화/정화/지속 회복은 StatusApplied(TargetId = 잔향이 ID 또는 지킴이 슬롯, Key 접두 "echo:"/"guardian:", 수선점이면 TargetId -1 + "shop:"),
    /// 연쇄는 맞는 순서대로 적마다 StatusApplied(Chain)가 먼저 나온 뒤 피해가 들어간다.
    /// StatusApplied의 Key는 스킬 연출 키(없으면 효과 종류 기본 키)다.
    /// </para>
    /// </summary>
    public partial class BattleSimulator
    {
        const float SkillRetrySeconds = 0.25f;
        const float PassivePulseSeconds = 0.5f;
        const int MaxCastsPerFlush = 256;
        const float DefaultSkillRadius = 0.6f;
        const float DefaultRampCap = 50f;
        /// <summary>적만 노리는 소환 시 스킬이 대상이 나타나길 기다리는 시간(초) — 웨이브 사이에 불러도 스킬이 헛돌지 않게</summary>
        const float SummonSkillHoldSeconds = 1.5f;
        /// <summary>
        /// 전투 시작 때 받을 아군이 없던 아군 스킬이 기다리는 시간(초, 전투 시작부터).
        /// 덱만 있는 던전처럼 시작할 때 아무도 없으면 첫 잔향이가 나올 때 쓴다 (퍼레이드 강화 등).
        /// </summary>
        const float BattleStartHoldSeconds = 10f;

        readonly Dictionary<string, SkillDef> _skillRegistry = new();
        readonly Dictionary<SkillDef, SkillInfo> _skillInfos = new();
        readonly List<SkillInfo> _infoScratch = new();
        readonly List<PendingCast> _castQueue = new();
        /// <summary>이번 발동에서 팀 공용 스킬마다 고른 시전자 (발동이 끝날 때 대기열로)</summary>
        readonly List<PendingCast> _sharedPicks = new();
        /// <summary>0보다 크면 여러 유닛의 발동을 한꺼번에 모으는 중 (팀 공용 스킬은 모은 뒤 한 번만)</summary>
        int _triggerBatch;
        /// <summary>받을 아군이 나타나길 기다리는 전투 시작 스킬 (BattleStartHoldSeconds까지)</summary>
        readonly List<PendingCast> _heldStartCasts = new();
        float _heldStartTimer;
        bool _flushing;
        bool _lowHpFired;
        int _chainStamp;

        // ── 시전 대상 (재사용 목록, 시전은 한 번에 하나만 처리되므로 겹치지 않는다) ──
        readonly List<EnemyUnit> _castEnemies = new();
        readonly List<EnemyUnit> _revealTargets = new();
        readonly List<EchoUnit> _castEchoes = new();
        readonly List<GuardianUnit> _castGuardians = new();
        bool _castShop;

        /// <summary>스킬 정의를 전투용으로 분석한 정보 (전투마다 SkillDef 하나당 하나)</summary>
        sealed class SkillInfo
        {
            public SkillDef Def;
            public string Key;
            public string Vfx;
            public string EchoKey;
            public string GuardianKey;
            public string ShopKey;
            public SkillTrigger Trigger;
            public float Radius;
            public bool IsFormSkill;
            public bool HasEnemyEffect;
            public bool HasAllyEffect;
            public bool HasReveal;
            /// <summary>적 효과만 있음 → 대상 적이 없으면 시전하지 않는다</summary>
            public bool EnemyOnly;
            /// <summary>회복류만 있음 → 다친 아군이 없으면 (쿨타임 시전을) 미룬다</summary>
            public bool PureHeal;
            /// <summary>아군 효과만 있음 (적 효과·잔향/불빛/소환 없음) → 받을 아군이 없으면 시전하지 않는다</summary>
            public bool AllyOnly;
            /// <summary>대상 종류가 팀 전체 (수선점 / 전체 잔향이 / 전체 지킴이 / 전체 아군 / 전체 적)</summary>
            public bool TeamWide;
            /// <summary>Passive인데 상시 효과(처형/공격력 상승) 외의 효과가 있음 → 오라로 주기 적용</summary>
            public bool HasAura;
            /// <summary>시전형 스킬의 Execute 효과 합계 (이 스킬 피해에만 적용)</summary>
            public float ExecuteBonus;

            // ── 팀 공용 스킬 상태 (전투마다 새로 만들어지므로 따로 비우지 않는다) ──
            /// <summary>마지막 발동 시각 (OnKill/OnShopDamaged 최소 간격)</summary>
            public float SharedLast = float.NegativeInfinity;
            /// <summary>1회 제한 발동(OnShopLowHp/OnBattleStart)을 이미 썼음</summary>
            public bool SharedUsed;
            /// <summary>쿨타임 스킬을 팀이 다시 쓸 수 있는 시각</summary>
            public float SharedReady = float.NegativeInfinity;

            /// <summary>팀 공용 스킬: 형태 고유 스킬이 아닌(시너지 보너스) 팀 전체 대상 스킬 → 구성원이 여럿이어도 발동마다 한 번</summary>
            public bool Shared => TeamWide && !IsFormSkill;
        }

        /// <summary>유닛 하나가 가진 스킬 하나의 진행 상태</summary>
        sealed class SkillSlot
        {
            public SkillInfo Info;
            public float Timer;
            public float Max;
            public bool Queued;
            public bool Used;
            public float LastTrigger = float.NegativeInfinity;
            /// <summary>소환 시 스킬이 대상을 기다리는 마감 시각 (0이면 기다리지 않음)</summary>
            public float HoldUntil;
        }

        /// <summary>
        /// 수선점이 대신 쓰는 시전자: 전장에 나와 있지 않은 덱 칸의 스킬(전투 시작 스킬, 팀 공용 스킬)을 수선점 자리에서 쓴다.
        /// 능력치는 그 덱 칸 형태의 것(레벨·시너지 반영, 쓰기 직전에 다시 계산), 이벤트 SourceId는 -1.
        /// </summary>
        sealed class ShopCaster : CombatUnit
        {
            public DeckSlotState Deck;

            public override int SourceId => -1;
            public override Vector2 Position => Vector2.zero;
            public override bool IsAlive => true;
            public override CharacterStats Stats => Deck.Stats;
            public override CombatStyle Style => Deck.Stats.style;
        }

        struct PendingCast
        {
            public CombatUnit Caster;
            public SkillSlot Slot;
            public SkillTrigger Trigger;
            public EnemyUnit Primary;
            public Vector2 Point;
            public bool HasPoint;
            public bool FromCooldown;
        }

        void ResetSkillState()
        {
            _skillRegistry.Clear();
            _skillInfos.Clear();
            _castQueue.Clear();
            _sharedPicks.Clear();
            _triggerBatch = 0;
            _heldStartCasts.Clear();
            _heldStartTimer = 0f;
            _flushing = false;
            _lowHpFired = false;
            _chainStamp = 0;
        }

        // ───────────────────────────── 스킬 구성 ─────────────────────────────

        /// <summary>
        /// 형태 고유 스킬 + 계보 보너스 스킬(특성/조합) + 형태 보너스 스킬(역할) 정보. 같은 스킬은 한 번만.
        /// output이 null이면 재사용 목록에 담아 돌려준다
        /// </summary>
        List<SkillInfo> CollectSkillInfos(SkillDef formSkill, string formId, string lineageId, List<SkillInfo> output)
        {
            var list = output ?? _infoScratch;
            list.Clear();
            if (formSkill != null && !formSkill.IsEmpty)
            {
                var info = GetInfo(formSkill, (formId ?? "unit") + ":skill");
                info.IsFormSkill = true;
                list.Add(info);
            }
            if (lineageId != null && _mods.BonusSkills.TryGetValue(lineageId, out var bonus))
                AddBonusInfos(list, bonus, lineageId + ":bonus");
            if (formId != null && _mods.FormBonusSkills.TryGetValue(formId, out var formBonus))
                AddBonusInfos(list, formBonus, formId + ":formbonus");
            return list;
        }

        void AddBonusInfos(List<SkillInfo> list, List<SkillDef> bonus, string keyPrefix)
        {
            if (bonus == null) return;
            for (int i = 0; i < bonus.Count; i++)
            {
                var def = bonus[i];
                if (def == null || def.IsEmpty) continue;
                var info = GetInfo(def, keyPrefix + i);
                if (!list.Contains(info)) list.Add(info);
            }
        }

        SkillInfo GetInfo(SkillDef def, string fallbackKey)
        {
            if (_skillInfos.TryGetValue(def, out var info)) return info;
            info = new SkillInfo
            {
                Def = def,
                Key = string.IsNullOrEmpty(def.id) ? fallbackKey : def.id,
                Trigger = def.trigger,
                Radius = def.radius > 0f ? def.radius : DefaultSkillRadius,
            };
            bool heal = false, otherAlly = false, global = false;
            foreach (var fx in def.effects)
            {
                if (fx == null) continue;
                switch (fx.type)
                {
                    case SkillEffectType.Damage:
                    case SkillEffectType.Slow:
                    case SkillEffectType.Stun:
                    case SkillEffectType.Knockback:
                    case SkillEffectType.Pull:
                    case SkillEffectType.Mark:
                    case SkillEffectType.DamageOverTime:
                    case SkillEffectType.Chain:
                        info.HasEnemyEffect = true;
                        break;
                    case SkillEffectType.Reveal:
                        info.HasEnemyEffect = true;
                        info.HasReveal = true;
                        break;
                    case SkillEffectType.Heal:
                    case SkillEffectType.HealOverTime:
                        info.HasAllyEffect = true;
                        heal = true;
                        break;
                    case SkillEffectType.Shield:
                    case SkillEffectType.BuffAttack:
                    case SkillEffectType.BuffAttackSpeed:
                    case SkillEffectType.Cleanse:
                        info.HasAllyEffect = true;
                        otherAlly = true;
                        break;
                    case SkillEffectType.Summon:
                    case SkillEffectType.ResonanceGain:
                    case SkillEffectType.LampGain:
                        global = true;
                        break;
                    case SkillEffectType.Execute:
                        if (def.trigger != SkillTrigger.Passive) info.ExecuteBonus += Mathf.Max(0f, fx.value);
                        break;
                }
                if (def.trigger == SkillTrigger.Passive && fx.type != SkillEffectType.Execute && fx.type != SkillEffectType.AttackRamp)
                    info.HasAura = true;
            }
            info.EnemyOnly = info.HasEnemyEffect && !info.HasAllyEffect && !global;
            info.AllyOnly = info.HasAllyEffect && !info.HasEnemyEffect && !global;
            info.PureHeal = heal && !otherAlly && !info.HasEnemyEffect && !global;
            info.TeamWide = IsTeamWideTarget(def.target);
            info.Vfx = !string.IsNullOrEmpty(def.vfx) ? def.vfx : DefaultVfx(def);
            info.EchoKey = "echo:" + info.Vfx;
            info.GuardianKey = "guardian:" + info.Vfx;
            info.ShopKey = "shop:" + info.Vfx;
            _skillInfos[def] = info;
            _skillRegistry.TryAdd(info.Key, def);
            return info;
        }

        static string DefaultVfx(SkillDef def)
        {
            var first = def.effects != null && def.effects.Count > 0 && def.effects[0] != null ? def.effects[0].type : SkillEffectType.Damage;
            return first switch
            {
                SkillEffectType.Heal => SkillVfx.HeartHeal,
                SkillEffectType.Shield => SkillVfx.ShieldBubble,
                SkillEffectType.Slow => SkillVfx.SlowYarn,
                SkillEffectType.Stun => SkillVfx.StunStars,
                SkillEffectType.Knockback => SkillVfx.SoundRing,
                SkillEffectType.Pull => SkillVfx.WaltzRibbon,
                SkillEffectType.Mark => SkillVfx.DebuffMark,
                SkillEffectType.BuffAttack => SkillVfx.BuffCrown,
                SkillEffectType.BuffAttackSpeed => SkillVfx.BuffCrown,
                SkillEffectType.Summon => SkillVfx.SummonCircle,
                SkillEffectType.ResonanceGain => SkillVfx.MusicNotes,
                SkillEffectType.LampGain => SkillVfx.CandleBurst,
                SkillEffectType.Reveal => SkillVfx.RevealLight,
                SkillEffectType.DamageOverTime => SkillVfx.CandleBurst,
                SkillEffectType.Chain => SkillVfx.KeyBeam,
                SkillEffectType.AttackRamp => SkillVfx.BuffCrown,
                SkillEffectType.Cleanse => SkillVfx.CleanseSparkle,
                SkillEffectType.HealOverTime => SkillVfx.MoonGlow,
                _ => SkillVfx.StarBurst,
            };
        }

        static bool IsTeamWideTarget(SkillTargetType target) => target switch
        {
            SkillTargetType.Shop => true,
            SkillTargetType.AllEchoes => true,
            SkillTargetType.AllGuardians => true,
            SkillTargetType.AllAllies => true,
            SkillTargetType.AllEnemies => true,
            _ => false,
        };

        /// <summary>
        /// 유닛에 스킬 칸을 만든다. 상시 패시브(처형/공격력 상승)는 여기서 바로 적용.
        /// 잔향이는 전투 시작 스킬 칸을 갖지 않는다 — 덱 칸의 전투 시작 스킬은 수선점이 대신 쓴다 (BuildShopCaster).
        /// </summary>
        void BuildUnitSkills(CombatUnit u, List<SkillInfo> infos)
        {
            u.Skills.Clear();
            u.Primary = null;
            bool guardian = u is GuardianUnit;
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                var def = info.Def;
                if (guardian && (info.Trigger == SkillTrigger.OnSummon || info.Trigger == SkillTrigger.OnDissolve)) continue;
                if (!guardian && info.Trigger == SkillTrigger.OnBattleStart) continue;
                var slot = new SkillSlot { Info = info };
                if (info.Trigger == SkillTrigger.Cooldown)
                {
                    slot.Max = Mathf.Max(0.5f, def.cooldown * u.CooldownScale);
                    float first = def.initialDelay > 0f ? def.initialDelay : def.cooldown * 0.5f;
                    slot.Timer = Mathf.Max(0f, first * u.CooldownScale);
                }
                else if (info.Trigger == SkillTrigger.Passive)
                {
                    ApplyPassiveTraits(u, def);
                    slot.Timer = 0.1f;
                }
                u.Skills.Add(slot);
                if (info.IsFormSkill && u.Primary == null) u.Primary = slot;
            }
            SyncSkillState(u);
        }

        /// <summary>
        /// 덱 칸의 수선점 대리 시전자. 전투 시작 스킬(전부)과, 전장에 구성원이 없어도 팀이 쓰는 팀 공용 스킬
        /// (불 밝힐 때 / 수선점 피격 시 / 수선점 위기 때)만 칸을 만든다. 쓸 스킬이 없으면 null.
        /// </summary>
        ShopCaster BuildShopCaster(DeckSlotState deck, int traitMask, List<SkillInfo> infos)
        {
            ShopCaster caster = null;
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                bool use = info.Trigger == SkillTrigger.OnBattleStart
                           || info.Shared && (info.Trigger == SkillTrigger.OnLampLit || info.Trigger == SkillTrigger.OnShopDamaged
                                              || info.Trigger == SkillTrigger.OnShopLowHp);
                if (!use) continue;
                if (caster == null)
                {
                    caster = new ShopCaster { Deck = deck, FormId = deck.FormId, LineageId = deck.LineageId };
                    SetTraits(caster, traitMask);
                }
                caster.Skills.Add(new SkillSlot { Info = info });
            }
            return caster;
        }

        static bool HasEnemyOnlySummonSkill(List<SkillInfo> infos)
        {
            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].Trigger == SkillTrigger.OnSummon && infos[i].EnemyOnly) return true;
            }
            return false;
        }

        static void ApplyPassiveTraits(CombatUnit u, SkillDef def)
        {
            foreach (var fx in def.effects)
            {
                if (fx == null) continue;
                if (fx.type == SkillEffectType.Execute) u.ExecutePercent += Mathf.Max(0f, fx.value);
                else if (fx.type == SkillEffectType.AttackRamp)
                {
                    u.RampPerSecond += Mathf.Max(0f, fx.value);
                    u.RampCap += fx.count > 0 ? fx.count : DefaultRampCap;
                }
            }
        }

        static void SyncSkillState(CombatUnit u)
        {
            var primary = u.Primary;
            bool timed = primary != null && primary.Info.Trigger == SkillTrigger.Cooldown;
            float remain = timed ? Mathf.Max(0f, primary.Timer) : 0f;
            float max = timed ? primary.Max : 0f;
            if (u is GuardianUnit g)
            {
                g.State.SkillCooldown = remain;
                g.State.SkillCooldownMax = max;
            }
            else if (u is EchoUnit e)
            {
                e.State.SkillCooldown = remain;
                e.State.SkillCooldownMax = max;
            }
        }

        // ───────────────────────────── 발동 ─────────────────────────────

        /// <summary>쿨타임 스킬 대기열 등록 + 패시브 오라 적용 (유닛 진행마다)</summary>
        void UpdateSkillTimers(CombatUnit u, float dt)
        {
            var skills = u.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                var slot = skills[i];
                var info = slot.Info;
                if (info.Trigger == SkillTrigger.Cooldown)
                {
                    if (slot.Queued) continue;
                    slot.Timer -= dt;
                    if (slot.Timer > 0f) continue;
                    slot.Queued = true;
                    _castQueue.Add(new PendingCast { Caster = u, Slot = slot, Trigger = SkillTrigger.Cooldown, FromCooldown = true });
                }
                else if (info.Trigger == SkillTrigger.Passive && info.HasAura)
                {
                    slot.Timer -= dt;
                    if (slot.Timer > 0f) continue;
                    slot.Timer = PassivePulseSeconds;
                    PassivePulse(u, slot);
                    if (_outcome != BattleOutcome.Running) return;
                }
                else if (info.Trigger == SkillTrigger.OnSummon && slot.HoldUntil > 0f)
                {
                    // 소환 때 대상이 없던 스킬: 잠깐 동안 대상이 나타나는지 다시 본다
                    slot.Timer -= dt;
                    if (slot.Timer > 0f) continue;
                    if (_time > slot.HoldUntil)
                    {
                        slot.HoldUntil = 0f;
                        continue;
                    }
                    slot.Timer = SkillRetrySeconds;
                    _castQueue.Add(new PendingCast { Caster = u, Slot = slot, Trigger = SkillTrigger.OnSummon });
                }
            }
            SyncSkillState(u);
        }

        /// <summary>
        /// 유닛의 trigger 스킬을 대기열에 넣는다 (최소 간격/1회 제한 확인).
        /// 팀 공용 스킬은 후보로만 올려 두고, 발동 묶음이 끝날 때(묶음 밖이면 바로) 가장 알맞은 시전자 하나로 넣는다.
        /// </summary>
        void QueueTrigger(CombatUnit u, SkillTrigger trigger, EnemyUnit primary, Vector2 point, bool hasPoint)
        {
            var skills = u.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                var slot = skills[i];
                var info = slot.Info;
                if (info.Trigger != trigger) continue;
                if (info.Shared)
                {
                    OfferShared(u, slot, trigger, primary, point, hasPoint);
                    continue;
                }
                switch (trigger)
                {
                    case SkillTrigger.OnKill:
                    case SkillTrigger.OnShopDamaged:
                        float interval = Mathf.Max(0f, info.Def.cooldown) * u.CooldownScale;
                        if (_time - slot.LastTrigger < interval) continue;
                        slot.LastTrigger = _time;
                        break;
                    case SkillTrigger.OnShopLowHp:
                    case SkillTrigger.OnBattleStart:
                        if (slot.Used) continue;
                        slot.Used = true;
                        break;
                }
                _castQueue.Add(new PendingCast { Caster = u, Slot = slot, Trigger = trigger, Primary = primary, Point = point, HasPoint = hasPoint });
            }
            if (_triggerBatch == 0) CommitShared();
        }

        /// <summary>
        /// 모든 지킴이와 살아 있는 잔향이의 trigger 스킬.
        /// 팀 공용 스킬은 한 번만 나가고, 전장에 구성원이 없으면 덱 칸을 대신해 수선점이 쓴다.
        /// </summary>
        void QueueTriggerAll(SkillTrigger trigger)
        {
            _triggerBatch++;
            for (int slot = 0; slot < _guardianUnits.Length; slot++)
            {
                var g = _guardianUnits[slot];
                if (g != null && g.Skills.Count > 0) QueueTrigger(g, trigger, null, Vector2.zero, false);
            }
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                var echo = _echoUnits[i];
                if (echo.State.Alive && echo.Skills.Count > 0) QueueTrigger(echo, trigger, null, Vector2.zero, false);
            }
            QueueDeckTrigger(trigger);
            _triggerBatch--;
            if (_triggerBatch == 0) CommitShared();
        }

        /// <summary>
        /// 덱 칸 스킬을 수선점이 대신 쓴다: 전투 시작 스킬은 전부, 그 밖에는 팀 공용 스킬만
        /// (전장에 같은 스킬을 가진 유닛이 있으면 그 유닛이 우선이라 이 후보는 밀린다).
        /// </summary>
        void QueueDeckTrigger(SkillTrigger trigger)
        {
            for (int i = 0; i < _deckRuntime.Count; i++)
            {
                var caster = _deckRuntime[i]?.Caster;
                if (caster == null || !HasTrigger(caster, trigger)) continue;
                RefreshCombatStats(caster);
                QueueTrigger(caster, trigger, null, Vector2.zero, false);
            }
        }

        static bool HasTrigger(CombatUnit u, SkillTrigger trigger)
        {
            var skills = u.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].Info.Trigger == trigger) return true;
            }
            return false;
        }

        /// <summary>팀 공용 스킬 후보: 같은 스킬 후보가 이미 있으면 더 알맞은 시전자로 바꾼다</summary>
        void OfferShared(CombatUnit u, SkillSlot slot, SkillTrigger trigger, EnemyUnit primary, Vector2 point, bool hasPoint)
        {
            var info = slot.Info;
            if (!SharedTriggerReady(info, trigger, u)) return;
            var offer = new PendingCast { Caster = u, Slot = slot, Trigger = trigger, Primary = primary, Point = point, HasPoint = hasPoint };
            for (int i = 0; i < _sharedPicks.Count; i++)
            {
                if (_sharedPicks[i].Slot.Info != info) continue;
                if (IsBetterCaster(u, _sharedPicks[i].Caster)) _sharedPicks[i] = offer;
                return;
            }
            _sharedPicks.Add(offer);
        }

        /// <summary>팀이 함께 쓰는 최소 간격 / 1회 제한 (Cooldown 트리거는 FlushCasts에서 따로 본다)</summary>
        bool SharedTriggerReady(SkillInfo info, SkillTrigger trigger, CombatUnit u)
        {
            switch (trigger)
            {
                case SkillTrigger.OnKill:
                case SkillTrigger.OnShopDamaged:
                    return _time - info.SharedLast >= Mathf.Max(0f, info.Def.cooldown) * u.CooldownScale;
                case SkillTrigger.OnShopLowHp:
                case SkillTrigger.OnBattleStart:
                    return !info.SharedUsed;
                default:
                    return true;
            }
        }

        /// <summary>전장에 있는 유닛이 수선점 대리보다 먼저, 그다음은 공격력(버프 포함)이 높은 쪽</summary>
        static bool IsBetterCaster(CombatUnit candidate, CombatUnit current)
        {
            bool candidateOnField = !(candidate is ShopCaster);
            bool currentOnField = !(current is ShopCaster);
            if (candidateOnField != currentOnField) return candidateOnField;
            return candidate.CurrentSkillAttack > current.CurrentSkillAttack + 1e-4f;
        }

        /// <summary>모아 둔 팀 공용 스킬을 스킬마다 한 번씩 대기열에 넣고 팀 공용 간격/1회 제한을 기록한다</summary>
        void CommitShared()
        {
            if (_sharedPicks.Count == 0) return;
            for (int i = 0; i < _sharedPicks.Count; i++)
            {
                var pick = _sharedPicks[i];
                var info = pick.Slot.Info;
                info.SharedLast = _time;
                if (pick.Trigger == SkillTrigger.OnShopLowHp || pick.Trigger == SkillTrigger.OnBattleStart) info.SharedUsed = true;
                _castQueue.Add(pick);
            }
            _sharedPicks.Clear();
        }

        /// <summary>대기 중인 시전을 차례로 처리한다 (시전이 부른 처치 시 스킬 등도 같은 차례에 이어서)</summary>
        void FlushCasts()
        {
            if (_flushing || _castQueue.Count == 0) return;
            _flushing = true;
            int i = 0;
            try
            {
                for (; i < _castQueue.Count && i < MaxCastsPerFlush; i++)
                {
                    if (_outcome != BattleOutcome.Running) break;
                    var pending = _castQueue[i];
                    var slot = pending.Slot;
                    var info = slot.Info;
                    if (pending.FromCooldown && info.Shared && _time < info.SharedReady)
                    {
                        // 팀 공용 스킬: 다른 구성원이 방금 썼으면 함께 쓰는 쿨타임이 끝날 때까지 기다린다
                        slot.Queued = false;
                        slot.Timer = info.SharedReady - _time;
                        continue;
                    }
                    bool cast = Cast(pending);
                    if (!pending.FromCooldown)
                    {
                        if (pending.Trigger == SkillTrigger.OnSummon) HoldSummonSkill(slot, cast);
                        else if (!cast && pending.Trigger == SkillTrigger.OnBattleStart && info.AllyOnly && _time <= BattleStartHoldSeconds)
                            _heldStartCasts.Add(pending);
                        continue;
                    }
                    slot.Queued = false;
                    slot.Timer = cast ? slot.Max : SkillRetrySeconds;
                    if (cast && info.Shared) info.SharedReady = _time + slot.Max;
                }
            }
            finally
            {
                // 처리하지 못한 쿨타임 시전은 잠시 뒤 다시
                for (; i < _castQueue.Count; i++)
                {
                    var pending = _castQueue[i];
                    if (!pending.FromCooldown) continue;
                    pending.Slot.Queued = false;
                    pending.Slot.Timer = SkillRetrySeconds;
                }
                _castQueue.Clear();
                _flushing = false;
            }
        }

        bool Cast(in PendingCast p)
        {
            var u = p.Caster;
            var info = p.Slot.Info;
            bool dissolving = p.Trigger == SkillTrigger.OnDissolve;
            if (!u.IsAlive && !dissolving) return false;
            var origin = dissolving && p.HasPoint ? p.Point : u.Position;
            ResolveTargets(u, info, p, origin, out var center, out var primary);

            bool anyEnemy = _castEnemies.Count > 0 || HasHiddenRevealTarget();
            if (info.EnemyOnly && !anyEnemy) return false;
            if (info.AllyOnly && !AnyCastAlly()) return false;
            if (p.FromCooldown)
            {
                if (!anyEnemy && !AnyEnemyAlive()) return false;
                if (info.PureHeal && !AnyCastAllyHurt()) return false;
            }

            Emit(new BattleEvent(BattleEventType.SkillCast, u.SourceId, primary != null ? primary.State.Id : -1, center, info.Radius, info.Key));
            float attack = u.CurrentSkillAttack;
            var effects = info.Def.effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var fx = effects[i];
                if (fx == null) continue;
                // 빛 특성이 아닌 시전자의 드러냄: 방금 드러난 적도 뒤따르는 효과(피해·연쇄 등)의 대상이 된다
                bool revealing = fx.type == SkillEffectType.Reveal && !u.IsLight && HasHiddenRevealTarget();
                ApplyEffect(u, info, fx, attack, center, primary, origin);
                if (_outcome != BattleOutcome.Running) break;
                if (revealing) RecollectAfterReveal(u, info, p, origin, ref primary);
            }
            return true;
        }

        /// <summary>드러냄 직후 대상 적을 다시 모은다 (효과 중심은 그대로, 노리던 적이 없으면 새로 고른다)</summary>
        void RecollectAfterReveal(CombatUnit u, SkillInfo info, in PendingCast p, Vector2 origin, ref EnemyUnit primary)
        {
            _castEnemies.Clear();
            var center = origin;
            EnemyUnit picked = null;
            CollectEnemies(u, info, p, origin, false, _castEnemies, ref center, ref picked);
            if (picked != null) primary = picked;
            else if (primary == null || !IsTargetable(u, primary, false)) primary = ClosestToShop(_castEnemies);
        }

        /// <summary>전투 시작 때 받을 아군이 없던 아군 스킬을 다시 시도한다 (진행마다, SkillRetrySeconds 간격, 기다림 시간이 지나면 버린다)</summary>
        void RetryHeldStartCasts(float dt)
        {
            if (_heldStartCasts.Count == 0) return;
            if (_time > BattleStartHoldSeconds)
            {
                _heldStartCasts.Clear();
                return;
            }
            _heldStartTimer -= dt;
            if (_heldStartTimer > 0f) return;
            _heldStartTimer = SkillRetrySeconds;
            _castQueue.AddRange(_heldStartCasts);
            _heldStartCasts.Clear();
        }

        /// <summary>소환 시 스킬이 대상이 없어 못 나갔으면 잠깐 기다렸다 다시 (적만 노리는 스킬만). 나가면 기다림 끝</summary>
        void HoldSummonSkill(SkillSlot slot, bool cast)
        {
            if (cast || !slot.Info.EnemyOnly)
            {
                slot.HoldUntil = 0f;
                return;
            }
            if (slot.HoldUntil <= 0f) slot.HoldUntil = _time + SummonSkillHoldSeconds;
            slot.Timer = SkillRetrySeconds;
        }

        // ───────────────────────────── 대상 ─────────────────────────────

        void ResolveTargets(CombatUnit u, SkillInfo info, in PendingCast p, Vector2 origin, out Vector2 center, out EnemyUnit primary)
        {
            _castEnemies.Clear();
            _revealTargets.Clear();
            _castEchoes.Clear();
            _castGuardians.Clear();
            _castShop = false;
            center = origin;
            primary = null;
            bool needEnemies = info.HasEnemyEffect || IsEnemyTarget(info.Def.target);
            if (needEnemies) CollectEnemies(u, info, p, origin, false, _castEnemies, ref center, ref primary);
            if (info.HasReveal)
            {
                var revealCenter = center;
                EnemyUnit unused = null;
                CollectEnemies(u, info, p, origin, true, _revealTargets, ref revealCenter, ref unused);
            }
            else if (!needEnemies)
            {
                center = AllyCenter(info.Def.target, origin);
            }
            if (info.HasAllyEffect) CollectAllies(u, info.Def.target, origin, info.Radius);
            if (primary == null) primary = ClosestToShop(_castEnemies);
        }

        static bool IsEnemyTarget(SkillTargetType target) => target switch
        {
            SkillTargetType.EnemiesInRange => true,
            SkillTargetType.EnemiesNearSelf => true,
            SkillTargetType.EnemiesNearTarget => true,
            SkillTargetType.StrongestEnemy => true,
            SkillTargetType.WeakestEnemy => true,
            SkillTargetType.AllEnemies => true,
            SkillTargetType.Lane => true,
            _ => false,
        };

        static Vector2 AllyCenter(SkillTargetType target, Vector2 origin) => target switch
        {
            SkillTargetType.Shop => Vector2.zero,
            SkillTargetType.AllEchoes => Vector2.zero,
            SkillTargetType.AllGuardians => Vector2.zero,
            SkillTargetType.AllAllies => Vector2.zero,
            _ => origin,
        };

        void CollectEnemies(CombatUnit u, SkillInfo info, in PendingCast p, Vector2 origin, bool includeHidden, List<EnemyUnit> output,
            ref Vector2 center, ref EnemyUnit primary)
        {
            float radius = info.Radius;
            switch (info.Def.target)
            {
                case SkillTargetType.Shop:
                case SkillTargetType.AllEchoes:
                case SkillTargetType.AllGuardians:
                case SkillTargetType.AllAllies:
                    center = Vector2.zero;
                    CollectNear(u, center, radius, includeHidden, output);
                    break;
                case SkillTargetType.EnemiesInRange:
                    if (u is GuardianUnit g)
                    {
                        center = g.Direction * Mathf.Lerp(BattleGeometry.RingRadius, g.Range, 0.5f);
                        CollectInFan(g, includeHidden, output);
                    }
                    else
                    {
                        center = origin;
                        CollectNear(u, origin, Mathf.Max(radius, u is EchoUnit e ? e.Range : 0f), includeHidden, output);
                    }
                    break;
                case SkillTargetType.EnemiesNearTarget:
                {
                    // 처치 시 스킬은 방금 쓰러진 적 자리에서 터진다
                    if (p.Trigger == SkillTrigger.OnKill && p.HasPoint)
                    {
                        center = p.Point;
                        CollectNear(u, center, radius, includeHidden, output);
                        break;
                    }
                    var target = CurrentTarget(u, p.Primary, includeHidden);
                    if (target != null)
                    {
                        center = target.State.Position;
                        primary = target;
                    }
                    else center = p.HasPoint ? p.Point : origin;
                    CollectNear(u, center, radius, includeHidden, output);
                    break;
                }
                case SkillTargetType.StrongestEnemy:
                case SkillTargetType.WeakestEnemy:
                {
                    var target = FindExtreme(u, info.Def.target == SkillTargetType.StrongestEnemy, includeHidden);
                    if (target == null) break;
                    output.Add(target);
                    center = target.State.Position;
                    primary = target;
                    break;
                }
                case SkillTargetType.AllEnemies:
                    center = Vector2.zero;
                    for (int i = 0; i < _enemyUnits.Count; i++)
                    {
                        var e = _enemyUnits[i];
                        if (e.State.Alive && (includeHidden || CanSee(u, e))) output.Add(e);
                    }
                    break;
                case SkillTargetType.Lane:
                    center = origin;
                    CollectLane(u, origin, radius, includeHidden, output);
                    break;
                default: // Self, EnemiesNearSelf, AlliesNearSelf, AdjacentGuardians
                    center = origin;
                    CollectNear(u, origin, radius, includeHidden, output);
                    break;
            }
        }

        void CollectNear(CombatUnit u, Vector2 point, float radius, bool includeHidden, List<EnemyUnit> output)
        {
            float radiusSq = radius * radius;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var e = _enemyUnits[i];
                if (!e.State.Alive || (!includeHidden && !CanSee(u, e))) continue;
                if ((e.State.Position - point).sqrMagnitude <= radiusSq) output.Add(e);
            }
        }

        void CollectInFan(GuardianUnit g, bool includeHidden, List<EnemyUnit> output)
        {
            float guardRadiusSq = _config.shopGuardRadius * _config.shopGuardRadius;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var e = _enemyUnits[i];
                if (!e.State.Alive || (!includeHidden && !CanSee(g, e))) continue;
                if (InFan(e.State.Position, g, guardRadiusSq)) output.Add(e);
            }
        }

        /// <summary>수선점에서 시전자 방향으로 뻗은 띠(폭 = radius) 위의 적</summary>
        void CollectLane(CombatUnit u, Vector2 origin, float width, bool includeHidden, List<EnemyUnit> output)
        {
            Vector2 dir;
            if (u is GuardianUnit g) dir = g.Direction;
            else if (origin.sqrMagnitude > 0.0004f) dir = origin.normalized;
            else dir = BattleGeometry.FromAngle(u is EchoUnit e ? e.GuardAngle : _dangerAngle, 1f);
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (!enemy.State.Alive || (!includeHidden && !CanSee(u, enemy))) continue;
                var pos = enemy.State.Position;
                float along = pos.x * dir.x + pos.y * dir.y;
                if (along <= 0f) continue;
                float side = Mathf.Abs(pos.x * dir.y - pos.y * dir.x);
                if (side <= width) output.Add(enemy);
            }
        }

        /// <summary>지금 노리는 적: 넘겨받은 적 → 시전자의 현재 공격 대상 → 지킴이 부채꼴/잔향이 주변에서 가장 가까운 적</summary>
        EnemyUnit CurrentTarget(CombatUnit u, EnemyUnit hint, bool includeHidden)
        {
            if (IsTargetable(u, hint, includeHidden)) return hint;
            if (u is GuardianUnit g)
            {
                float guardRadiusSq = _config.shopGuardRadius * _config.shopGuardRadius;
                if (IsTargetable(u, g.Target, includeHidden) && InFan(g.Target.State.Position, g, guardRadiusSq)) return g.Target;
                EnemyUnit best = null;
                float score = float.MaxValue;
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    var e = _enemyUnits[i];
                    if (!IsTargetable(u, e, includeHidden) || !InFan(e.State.Position, g, guardRadiusSq)) continue;
                    float s = TargetScore(g.State.Stats.style, e);
                    if (s < score)
                    {
                        score = s;
                        best = e;
                    }
                }
                return best;
            }
            if (u is EchoUnit echo)
            {
                if (IsTargetable(u, echo.Target, includeHidden)) return echo.Target;
                float aggro = Mathf.Max(echo.Range, _config.echoAggroRadius);
                float bestSq = aggro * aggro;
                EnemyUnit best = null;
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    var e = _enemyUnits[i];
                    if (!IsTargetable(u, e, includeHidden)) continue;
                    float d = (e.State.Position - echo.State.Position).sqrMagnitude;
                    if (d <= bestSq)
                    {
                        bestSq = d;
                        best = e;
                    }
                }
                return best;
            }
            return null;
        }

        static bool IsTargetable(CombatUnit u, EnemyUnit e, bool includeHidden) =>
            e != null && e.State.Alive && (includeHidden || CanSee(u, e));

        /// <summary>strongest = 체력이 가장 많은 적(보스 우선), 아니면 체력 비율이 가장 낮은 적</summary>
        EnemyUnit FindExtreme(CombatUnit u, bool strongest, bool includeHidden)
        {
            EnemyUnit best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var e = _enemyUnits[i];
                if (!IsTargetable(u, e, includeHidden)) continue;
                var s = e.State;
                float score = strongest ? s.Hp + s.Shield + (s.IsBoss ? 1e30f : 0f) : -(s.Hp / Mathf.Max(1f, s.MaxHp));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        static EnemyUnit ClosestToShop(List<EnemyUnit> list)
        {
            EnemyUnit best = null;
            float radius = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!e.State.Alive || e.Radius >= radius) continue;
                radius = e.Radius;
                best = e;
            }
            return best;
        }

        void CollectAllies(CombatUnit u, SkillTargetType target, Vector2 origin, float radius)
        {
            switch (target)
            {
                case SkillTargetType.Shop:
                    _castShop = true;
                    break;
                case SkillTargetType.AlliesNearSelf:
                {
                    float radiusSq = radius * radius;
                    for (int i = 0; i < _echoUnits.Count; i++)
                    {
                        var echo = _echoUnits[i];
                        if (echo.State.Alive && (echo.State.Position - origin).sqrMagnitude <= radiusSq) _castEchoes.Add(echo);
                    }
                    break;
                }
                case SkillTargetType.AllEchoes:
                    AddAllEchoes();
                    break;
                case SkillTargetType.AdjacentGuardians:
                {
                    // 잔향이는 가장 가까운 방위, 수선점이 대신 쓰면 가장 위험한 방위와 그 양옆
                    int slot = u is GuardianUnit g ? g.Slot
                        : BattleGeometry.NearestSlot(u is ShopCaster ? _dangerAngle : Mathf.Atan2(origin.y, origin.x));
                    if (!(u is GuardianUnit)) AddGuardian(slot);
                    AddGuardian((slot + 1) % BattleGeometry.SlotCount);
                    AddGuardian((slot + BattleGeometry.SlotCount - 1) % BattleGeometry.SlotCount);
                    break;
                }
                case SkillTargetType.AllGuardians:
                    for (int slot = 0; slot < _guardianUnits.Length; slot++) AddGuardian(slot);
                    break;
                case SkillTargetType.AllAllies:
                    for (int slot = 0; slot < _guardianUnits.Length; slot++) AddGuardian(slot);
                    AddAllEchoes();
                    break;
                default: // Self + 적 대상 스킬의 아군 효과 → 시전자 자신
                    AddSelf(u);
                    break;
            }
        }

        /// <summary>시전자 자신. 수선점 대리 시전자는 수선점 (회복·보호막이 수선점으로 간다)</summary>
        void AddSelf(CombatUnit u)
        {
            if (u is EchoUnit echo)
            {
                if (echo.State.Alive) _castEchoes.Add(echo);
            }
            else if (u is GuardianUnit g) _castGuardians.Add(g);
            else if (u is ShopCaster) _castShop = true;
        }

        void AddGuardian(int slot)
        {
            var g = _guardianUnits[slot];
            if (g != null && !_castGuardians.Contains(g)) _castGuardians.Add(g);
        }

        void AddAllEchoes()
        {
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                if (_echoUnits[i].State.Alive) _castEchoes.Add(_echoUnits[i]);
            }
        }

        bool HasHiddenRevealTarget()
        {
            for (int i = 0; i < _revealTargets.Count; i++)
            {
                if (!_revealTargets[i].State.Revealed) return true;
            }
            return false;
        }

        bool AnyEnemyAlive()
        {
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                if (_enemyUnits[i].State.Alive) return true;
            }
            return false;
        }

        bool AnyCastAlly() => _castShop || _castGuardians.Count > 0 || _castEchoes.Count > 0;

        bool AnyCastAllyHurt()
        {
            if ((_castShop || _castGuardians.Count > 0) && _shopHp < _shopMaxHp - 0.01f) return true;
            for (int i = 0; i < _castEchoes.Count; i++)
            {
                var s = _castEchoes[i].State;
                if (s.Alive && s.Hp < s.MaxHp - 0.01f) return true;
            }
            return false;
        }

        // ───────────────────────────── 효과 ─────────────────────────────

        static float Dur(SkillEffectDef fx, float fallback) => fx.duration > 0f ? fx.duration : fallback;

        void ApplyEffect(CombatUnit u, SkillInfo info, SkillEffectDef fx, float attack, Vector2 center, EnemyUnit primary, Vector2 origin)
        {
            int source = u.SourceId;
            switch (fx.type)
            {
                case SkillEffectType.Damage:
                    for (int i = 0; i < _castEnemies.Count; i++)
                    {
                        HitEnemy(_castEnemies[i], fx.value * attack, u, -1, true, info.ExecuteBonus);
                        if (_outcome != BattleOutcome.Running) return;
                    }
                    break;
                case SkillEffectType.Chain:
                    ChainHit(u, info, primary, fx.value * attack, Mathf.Max(0, fx.count));
                    break;
                case SkillEffectType.Slow:
                    for (int i = 0; i < _castEnemies.Count; i++)
                    {
                        var e = _castEnemies[i];
                        if (e.State.Alive && ApplySlow(e, fx.value * u.SlowPower, Dur(fx, 2f), true)) EmitEnemyStatus(e, fx.type, info, source);
                    }
                    break;
                case SkillEffectType.Stun:
                    for (int i = 0; i < _castEnemies.Count; i++)
                    {
                        var e = _castEnemies[i];
                        if (!e.State.Alive) continue;
                        Stun(e, Dur(fx, 1f), u);
                        EmitEnemyStatus(e, fx.type, info, source);
                    }
                    break;
                case SkillEffectType.Knockback:
                    for (int i = 0; i < _castEnemies.Count; i++)
                    {
                        var e = _castEnemies[i];
                        if (!e.State.Alive) continue;
                        Knockback(e, fx.value);
                        EmitEnemyStatus(e, fx.type, info, source);
                    }
                    break;
                case SkillEffectType.Pull:
                {
                    var point = u is EchoUnit ? origin : Centroid(_castEnemies, center);
                    for (int i = 0; i < _castEnemies.Count; i++)
                    {
                        var e = _castEnemies[i];
                        if (!e.State.Alive) continue;
                        Pull(e, point, fx.value);
                        EmitEnemyStatus(e, fx.type, info, source);
                    }
                    break;
                }
                case SkillEffectType.Mark:
                    for (int i = 0; i < _castEnemies.Count; i++)
                    {
                        var e = _castEnemies[i];
                        if (!e.State.Alive) continue;
                        ApplyMark(e, fx.value, Dur(fx, 4f));
                        EmitEnemyStatus(e, fx.type, info, source);
                    }
                    break;
                case SkillEffectType.DamageOverTime:
                    for (int i = 0; i < _castEnemies.Count; i++)
                    {
                        var e = _castEnemies[i];
                        if (!e.State.Alive) continue;
                        ApplyBurn(e, fx.value * attack, Dur(fx, 3f), u);
                        EmitEnemyStatus(e, fx.type, info, source);
                    }
                    break;
                case SkillEffectType.Reveal:
                    for (int i = 0; i < _revealTargets.Count; i++)
                    {
                        var e = _revealTargets[i];
                        if (!e.State.Alive || !e.Has(EnemyGimmick.Phasing)) continue;
                        Reveal(e, Dur(fx, 3f));
                        EmitEnemyStatus(e, fx.type, info, source);
                    }
                    break;
                case SkillEffectType.Heal:
                    HealTargets(u, fx.value * attack * u.HealPower, origin);
                    break;
                case SkillEffectType.HealOverTime:
                    HotTargets(u, info, fx.value * attack * u.HealPower, Dur(fx, 4f), true);
                    break;
                case SkillEffectType.Shield:
                    ShieldTargets(u, info, fx.value * attack * u.ShieldPower, fx.duration);
                    break;
                case SkillEffectType.BuffAttack:
                case SkillEffectType.BuffAttackSpeed:
                case SkillEffectType.Cleanse:
                    UnitStatusTargets(u, info, fx, Dur(fx, fx.type == SkillEffectType.Cleanse ? 2f : 5f), true);
                    break;
                case SkillEffectType.Summon:
                    SpawnTokens(u, fx.value, fx.count, fx.duration);
                    break;
                case SkillEffectType.ResonanceGain:
                    _resonance = Mathf.Clamp(_resonance + fx.value, 0f, _maxResonance);
                    break;
                case SkillEffectType.LampGain:
                    AddLampGain(fx.value);
                    break;
                // Execute(시전형은 ExecuteBonus로 이 스킬 피해에), AttackRamp(패시브 전용)는 따로 처리
            }
        }

        void EmitEnemyStatus(EnemyUnit e, SkillEffectType type, SkillInfo info, int sourceId) =>
            Emit(new BattleEvent(BattleEventType.StatusApplied, sourceId, e.State.Id, e.State.Position, (int)type, info.Vfx));

        static Vector2 Centroid(List<EnemyUnit> list, Vector2 fallback)
        {
            int count = 0;
            var sum = Vector2.zero;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].State.Alive) continue;
                sum += list[i].State.Position;
                count++;
            }
            return count > 0 ? sum / count : fallback;
        }

        /// <summary>첫 대상에서 가까운 적으로 hops번 튀며 피해 (같은 적은 한 번만)</summary>
        void ChainHit(CombatUnit u, SkillInfo info, EnemyUnit start, float damage, int hops)
        {
            if (start == null || !start.State.Alive) start = ClosestToShop(_castEnemies);
            if (start == null) return;
            int stamp = ++_chainStamp;
            float hopRadius = Mathf.Max(0.35f, info.Def.radius > 0f ? info.Def.radius : DefaultSkillRadius);
            float hopSq = hopRadius * hopRadius;
            var current = start;
            for (int hop = 0; hop <= hops && current != null; hop++)
            {
                current.ChainStamp = stamp;
                var from = current.State.Position;
                EmitEnemyStatus(current, SkillEffectType.Chain, info, u.SourceId);
                HitEnemy(current, damage, u, -1, true, info.ExecuteBonus);
                if (_outcome != BattleOutcome.Running) return;

                EnemyUnit next = null;
                float best = hopSq;
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    var e = _enemyUnits[i];
                    if (e.ChainStamp == stamp || !e.State.Alive || !CanSee(u, e)) continue;
                    float d = (e.State.Position - from).sqrMagnitude;
                    if (d <= best)
                    {
                        best = d;
                        next = e;
                    }
                }
                current = next;
            }
        }

        /// <summary>회복: 수선점(대상이 수선점이거나 지킴이) + 잔향이들</summary>
        void HealTargets(CombatUnit u, float amount, Vector2 origin)
        {
            if (!(amount > 0f)) return;
            if (_castShop || _castGuardians.Count > 0)
                HealShop(amount, u is GuardianUnit g ? g.Slot : -1, u is EchoUnit e ? e.State.Id : -1, origin);
            for (int i = 0; i < _castEchoes.Count; i++) HealEcho(_castEchoes[i], amount, u.SourceId);
        }

        void HotTargets(CombatUnit u, SkillInfo info, float perSecond, float duration, bool emit)
        {
            if (!(perSecond > 0f)) return;
            if (_castShop || _castGuardians.Count > 0)
            {
                AddShopHot(perSecond, duration);
                if (emit) Emit(new BattleEvent(BattleEventType.StatusApplied, u.SourceId, -1, Vector2.zero, (int)SkillEffectType.HealOverTime, info.ShopKey));
            }
            for (int i = 0; i < _castEchoes.Count; i++)
            {
                var echo = _castEchoes[i];
                AddEchoHot(echo, perSecond, duration, u.SourceId);
                if (emit)
                    Emit(new BattleEvent(BattleEventType.StatusApplied, u.SourceId, echo.State.Id, echo.State.Position, (int)SkillEffectType.HealOverTime,
                        info.EchoKey));
            }
        }

        void ShieldTargets(CombatUnit u, SkillInfo info, float amount, float duration)
        {
            if (!(amount > 0f)) return;
            if (_castShop || _castGuardians.Count > 0) AddShopShield(amount, duration, u.SourceId, info.Vfx);
            for (int i = 0; i < _castEchoes.Count; i++) AddEchoShield(_castEchoes[i], amount, duration, u.SourceId, info.Vfx);
        }

        /// <summary>
        /// 강화(공격력/공격 속도) · 정화: 대상 지킴이/잔향이. 시전자 자신은 대상 종류가 Self이거나 적 대상일 때만 받는다
        /// (CollectAllies가 이미 넣어 둔다). 전체 잔향이처럼 아군 무리를 노렸는데 아무도 없으면 시전자가 대신 받지 않는다.
        /// </summary>
        void UnitStatusTargets(CombatUnit u, SkillInfo info, SkillEffectDef fx, float duration, bool emit)
        {
            for (int i = 0; i < _castGuardians.Count; i++) ApplyUnitStatus(_castGuardians[i], info, fx, duration, emit);
            for (int i = 0; i < _castEchoes.Count; i++) ApplyUnitStatus(_castEchoes[i], info, fx, duration, emit);
        }

        void ApplyUnitStatus(CombatUnit target, SkillInfo info, SkillEffectDef fx, float duration, bool emit)
        {
            bool applied;
            switch (fx.type)
            {
                case SkillEffectType.BuffAttack:
                    applied = ApplyAttackBuff(target, fx.value, duration);
                    break;
                case SkillEffectType.BuffAttackSpeed:
                    applied = ApplySpeedBuff(target, fx.value, duration);
                    break;
                default:
                    Cleanse(target, duration);
                    applied = true;
                    break;
            }
            if (!applied || !emit) return;
            bool echo = target is EchoUnit;
            Emit(new BattleEvent(BattleEventType.StatusApplied, -1, target.SourceId, target.Position, (int)fx.type, echo ? info.EchoKey : info.GuardianKey));
        }

        /// <summary>
        /// 시전자 능력치 × scale의 작은 아군(토큰 잔향이) count마리. 지킴이가 부르면 그 방향을 지키고,
        /// 수선점이 대신 부르면 수선점 앞에서 생겨 가장 위험한 방향으로 나간다.
        /// </summary>
        void SpawnTokens(CombatUnit u, float scale, int count, float duration)
        {
            count = Mathf.Max(1, count);
            scale = scale > 0f ? scale : 0.5f;
            var stats = u.Stats;
            stats.attack *= scale;
            stats.hp *= scale;
            stats.lifetime = duration > 0f ? duration : Mathf.Max(1f, _config.tokenDefaultLifetime);
            stats.summonCost = 0;
            stats = SanitizeStats(stats);

            float angle;
            Vector2 basePosition;
            bool auto = false;
            if (u is GuardianUnit g)
            {
                angle = BattleGeometry.SlotAngle(g.Slot);
                basePosition = g.RingPosition * 0.9f;
            }
            else if (u is EchoUnit echo)
            {
                angle = echo.GuardAngle;
                auto = echo.AutoDirected;
                basePosition = echo.State.Position;
            }
            else
            {
                angle = _dangerAngle;
                auto = true;
                basePosition = BattleGeometry.FromAngle(angle, EchoSpawnRadius);
            }
            var side = BattleGeometry.FromAngle(angle + Mathf.PI * 0.5f, 1f);
            for (int i = 0; i < count; i++)
            {
                if (_tokenCount >= Mathf.Max(0, _config.maxTokens)) break;
                float offset = count > 1 ? (i - (count - 1) * 0.5f) * 0.12f : 0f;
                var position = basePosition + side * offset;
                var token = CreateEcho(u.FormId, u.LineageId, u.TraitMask, stats, -1, angle + offset, auto, position, true, 0);
                Emit(new BattleEvent(BattleEventType.TokenSummoned, u.SourceId, token.State.Id, position, i));
            }
        }

        /// <summary>패시브 오라: 0.5초마다 조용히(이벤트 없이) 효과를 다시 건다. 피해/회복/잔향/불빛 값은 초당 값</summary>
        void PassivePulse(CombatUnit u, SkillSlot slot)
        {
            var info = slot.Info;
            if (!u.IsAlive) return;
            var origin = u.Position;
            ResolveTargets(u, info, default, origin, out _, out _);
            float attack = u.CurrentSkillAttack;
            var effects = info.Def.effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var fx = effects[i];
                if (fx == null) continue;
                float duration = Mathf.Max(fx.duration, PassivePulseSeconds + 0.15f);
                switch (fx.type)
                {
                    case SkillEffectType.Damage:
                        for (int k = 0; k < _castEnemies.Count; k++)
                        {
                            HitEnemy(_castEnemies[k], fx.value * attack * PassivePulseSeconds, u, -1, false, info.ExecuteBonus);
                            if (_outcome != BattleOutcome.Running) return;
                        }
                        break;
                    case SkillEffectType.Slow:
                        for (int k = 0; k < _castEnemies.Count; k++) ApplySlow(_castEnemies[k], fx.value * u.SlowPower, duration, true);
                        break;
                    case SkillEffectType.Mark:
                        for (int k = 0; k < _castEnemies.Count; k++) ApplyMark(_castEnemies[k], fx.value, duration);
                        break;
                    case SkillEffectType.DamageOverTime:
                        for (int k = 0; k < _castEnemies.Count; k++) ApplyBurn(_castEnemies[k], fx.value * attack, duration, u);
                        break;
                    case SkillEffectType.Reveal:
                        for (int k = 0; k < _revealTargets.Count; k++) Reveal(_revealTargets[k], duration);
                        break;
                    case SkillEffectType.Heal:
                    case SkillEffectType.HealOverTime:
                        HotTargets(u, info, fx.value * attack * u.HealPower, duration, false);
                        break;
                    case SkillEffectType.BuffAttack:
                    case SkillEffectType.BuffAttackSpeed:
                    case SkillEffectType.Cleanse:
                        UnitStatusTargets(u, info, fx, duration, false);
                        break;
                    case SkillEffectType.ResonanceGain:
                        _resonance = Mathf.Clamp(_resonance + fx.value * PassivePulseSeconds, 0f, _maxResonance);
                        break;
                    case SkillEffectType.LampGain:
                        AddLampGain(fx.value * PassivePulseSeconds);
                        break;
                    // 보호막·기절·넉백·끌어당김·연쇄·소환은 상시 오라로 쓰지 않는다
                }
            }
        }
    }
}
