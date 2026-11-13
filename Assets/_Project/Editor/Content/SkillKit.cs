using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 스킬 데이터를 짧게 적기 위한 도우미 (형태 고유 스킬 / 시너지 보너스 스킬 공용).
    ///   Make("skill_jjak_medic", "천 덧대기", SkillVfx.PatchHeal, "꿰매 줄게!", Every(10f, 4f), Shop, "이야기 문장", Heal(3f), Shield(1.5f, 6f))
    /// 발동 시점(When) · 대상(Aim) · 효과(SkillEffectDef)를 조합하고, 설명은 "이야기 문장 + 수치 문장"으로 만든다.
    /// 수치 문장은 SkillText가 효과 값에서 만들기 때문에 숫자를 바꿔도 설명이 어긋나지 않는다.
    /// 효과 값의 의미는 SkillEffectDef 주석을 따른다 (배율 = 시전자 공격력 × 값, %는 그대로).
    /// </summary>
    internal static class SkillKit
    {
        // ───────────────────────── 발동 시점 ─────────────────────────

        /// <summary>발동 시점 + 쿨타임/최소 간격 + 첫 시전 지연</summary>
        public readonly struct When
        {
            public readonly SkillTrigger Trigger;
            public readonly float Cooldown;
            public readonly float InitialDelay;

            public When(SkillTrigger trigger, float cooldown = 0f, float initialDelay = 0f)
            {
                Trigger = trigger;
                Cooldown = cooldown;
                InitialDelay = initialDelay;
            }
        }

        /// <summary>항상 적용 (AttackRamp / Execute 전용)</summary>
        public static readonly When Always = new(SkillTrigger.Passive);
        /// <summary>잔향이로 소환될 때 1회</summary>
        public static readonly When Summoned = new(SkillTrigger.OnSummon);
        /// <summary>잔향이가 빛으로 풀릴 때 1회</summary>
        public static readonly When Dissolved = new(SkillTrigger.OnDissolve);
        /// <summary>수선점 불 밝히기가 발동될 때</summary>
        public static readonly When LampLit = new(SkillTrigger.OnLampLit);
        /// <summary>전투 중 1회: 수선점 내구도 30% 아래</summary>
        public static readonly When ShopLow = new(SkillTrigger.OnShopLowHp);
        /// <summary>전투(스테이지) 시작 시 1회</summary>
        public static readonly When BattleStart = new(SkillTrigger.OnBattleStart);

        /// <param name="firstDelay">전투 시작 후 첫 시전까지(초). 0이면 쿨타임의 절반</param>
        public static When Every(float seconds, float firstDelay = 0f) => new(SkillTrigger.Cooldown, seconds, firstDelay);
        /// <param name="minInterval">다시 터지기까지 최소 간격(초)</param>
        public static When OnKill(float minInterval) => new(SkillTrigger.OnKill, minInterval);
        /// <param name="minInterval">다시 터지기까지 최소 간격(초)</param>
        public static When ShopHit(float minInterval) => new(SkillTrigger.OnShopDamaged, minInterval);

        // ───────────────────────── 대상 ─────────────────────────

        /// <summary>대상 범위 + 반지름 (방위 링 = 1, 적 생성 거리 = 2.5)</summary>
        public readonly struct Aim
        {
            public readonly SkillTargetType Target;
            public readonly float Radius;

            public Aim(SkillTargetType target, float radius)
            {
                Target = target;
                Radius = radius;
            }
        }

        /// <summary>SkillDef 기본 반지름 (대상이 반지름을 쓰지 않을 때도 이 값을 둔다)</summary>
        const float DefaultRadius = 0.6f;
        /// <summary>전장 전체를 덮는 반지름 (드러냄 등 반지름을 쓰는 효과를 전체에 걸 때)</summary>
        const float FieldRadius = 3f;

        public static readonly Aim Self = new(SkillTargetType.Self, DefaultRadius);
        public static readonly Aim Shop = new(SkillTargetType.Shop, DefaultRadius);
        public static readonly Aim Strongest = new(SkillTargetType.StrongestEnemy, DefaultRadius);
        public static readonly Aim Weakest = new(SkillTargetType.WeakestEnemy, DefaultRadius);
        public static readonly Aim AllFoes = new(SkillTargetType.AllEnemies, FieldRadius);
        public static readonly Aim Lane = new(SkillTargetType.Lane, DefaultRadius);
        public static readonly Aim AllEchoes = new(SkillTargetType.AllEchoes, DefaultRadius);
        public static readonly Aim Neighbors = new(SkillTargetType.AdjacentGuardians, DefaultRadius);
        public static readonly Aim AllGuards = new(SkillTargetType.AllGuardians, DefaultRadius);
        public static readonly Aim Everyone = new(SkillTargetType.AllAllies, DefaultRadius);

        /// <summary>지킴이: 자기 부채꼴 안 / 잔향이: radius 안의 적</summary>
        public static Aim InRange(float radius = DefaultRadius) => new(SkillTargetType.EnemiesInRange, radius);
        /// <summary>시전자 주변 radius 안의 적</summary>
        public static Aim Around(float radius) => new(SkillTargetType.EnemiesNearSelf, radius);
        /// <summary>현재 공격 대상 주변 radius 안의 적</summary>
        public static Aim AroundTarget(float radius) => new(SkillTargetType.EnemiesNearTarget, radius);
        /// <summary>시전자 주변 radius 안의 아군 잔향이</summary>
        public static Aim EchoesNear(float radius) => new(SkillTargetType.AlliesNearSelf, radius);

        // ───────────────────────── 효과 ─────────────────────────

        /// <param name="ratio">공격력 배율 (3 = 공격력 300%)</param>
        public static SkillEffectDef Damage(float ratio) => Effect(SkillEffectType.Damage, ratio);
        public static SkillEffectDef Heal(float ratio) => Effect(SkillEffectType.Heal, ratio);
        public static SkillEffectDef Shield(float ratio, float seconds) => Effect(SkillEffectType.Shield, ratio, seconds);
        /// <param name="power">이동 속도 감소 비율 (0~1)</param>
        public static SkillEffectDef Slow(float power, float seconds) => Effect(SkillEffectType.Slow, power, seconds);
        public static SkillEffectDef Stun(float seconds) => Effect(SkillEffectType.Stun, 0f, seconds);
        /// <param name="distance">수선점 바깥쪽으로 밀어내는 거리 (월드 단위)</param>
        public static SkillEffectDef Knockback(float distance) => Effect(SkillEffectType.Knockback, distance);
        /// <param name="distance">시전자/무리 중심 쪽으로 끌어당기는 거리 (월드 단위)</param>
        public static SkillEffectDef Pull(float distance) => Effect(SkillEffectType.Pull, distance);
        /// <param name="percent">받는 피해 증가 %</param>
        public static SkillEffectDef Mark(float percent, float seconds) => Effect(SkillEffectType.Mark, percent, seconds);
        public static SkillEffectDef BuffAttack(float percent, float seconds) => Effect(SkillEffectType.BuffAttack, percent, seconds);
        public static SkillEffectDef BuffSpeed(float percent, float seconds) => Effect(SkillEffectType.BuffAttackSpeed, percent, seconds);
        /// <param name="statRatio">시전자 능력치 대비 토큰 능력치 (0.4 = 40%)</param>
        public static SkillEffectDef Summon(float statRatio, int count, float seconds) =>
            Effect(SkillEffectType.Summon, statRatio, seconds, count);
        public static SkillEffectDef Resonance(float amount) => Effect(SkillEffectType.ResonanceGain, amount);
        /// <param name="fill">불빛 게이지 (1 = 가득)</param>
        public static SkillEffectDef Lamp(float fill) => Effect(SkillEffectType.LampGain, fill);
        public static SkillEffectDef Reveal(float seconds) => Effect(SkillEffectType.Reveal, 0f, seconds);
        /// <param name="ratioPerSecond">초당 공격력 배율</param>
        public static SkillEffectDef Burn(float ratioPerSecond, float seconds) => Effect(SkillEffectType.DamageOverTime, ratioPerSecond, seconds);
        /// <param name="percent">체력 30% 이하 적에게 주는 피해 증가 % (Passive 전용)</param>
        public static SkillEffectDef Execute(float percent) => Effect(SkillEffectType.Execute, percent);
        public static SkillEffectDef Chain(float ratio, int bounces) => Effect(SkillEffectType.Chain, ratio, 0f, bounces);
        /// <param name="percentPerSecond">초당 공격력 증가 %</param>
        /// <param name="maxPercent">최대 증가 % (Passive 전용)</param>
        public static SkillEffectDef Ramp(float percentPerSecond, int maxPercent) =>
            Effect(SkillEffectType.AttackRamp, percentPerSecond, 0f, maxPercent);
        /// <param name="immuneSeconds">약화 제거 뒤 면역 시간</param>
        public static SkillEffectDef Cleanse(float immuneSeconds) => Effect(SkillEffectType.Cleanse, 0f, immuneSeconds);
        /// <param name="ratioPerSecond">초당 공격력 배율</param>
        public static SkillEffectDef Regen(float ratioPerSecond, float seconds) => Effect(SkillEffectType.HealOverTime, ratioPerSecond, seconds);

        static SkillEffectDef Effect(SkillEffectType type, float value, float duration = 0f, int count = 0) =>
            new() { type = type, value = value, duration = duration, count = count };

        // ───────────────────────── 스킬 ─────────────────────────

        /// <param name="vfx">SkillVfx 키</param>
        /// <param name="castText">시전 시 머리 위 대사</param>
        /// <param name="story">설명 앞부분의 이야기 문장 (숫자 없이). 뒤에 수치 문장이 자동으로 붙는다</param>
        public static SkillDef Make(string id, string name, string vfx, string castText, When when, Aim aim, string story,
            params SkillEffectDef[] effects)
        {
            var skill = new SkillDef
            {
                id = id,
                name = name,
                castText = castText,
                trigger = when.Trigger,
                cooldown = when.Cooldown,
                initialDelay = when.InitialDelay,
                target = aim.Target,
                radius = aim.Radius,
                effects = new List<SkillEffectDef>(effects),
                vfx = vfx,
            };
            string mechanics = SkillText.Mechanics(skill);
            skill.description = string.IsNullOrEmpty(story) ? mechanics : story + "\n" + mechanics;
            return skill;
        }
    }
}
