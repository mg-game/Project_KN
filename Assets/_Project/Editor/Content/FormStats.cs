using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 형태 기본 능력치(레벨 1) 계산표. 형태마다 숫자를 따로 적지 않고 성향 · 진화 단계 · 계보 등급으로 정한다.
    ///
    /// 공격/체력 = 기준값 × 성향 배율 × 1.35^진화단계 × 1.12^등급단계
    ///   기준값 (1성 기본형): 공격 12, 체력 100.  등급단계: 1성 0 ~ 5성 4.
    ///
    /// | 성향         | 공격 | 체력 | 간격(초) | 요격 반경 | 부채꼴 | 이동 | 소환 비용 | 유지(초) |
    /// |--------------|------|------|----------|-----------|--------|------|-----------|----------|
    /// | 방어 Defense | ×0.8 | ×1.6 | 1.0      | 1.5       | 80°    | 0.70 | 5         | 14       |
    /// | 돌격 Assault | ×1.4 | ×1.0 | 0.8      | 1.3       | 60°    | 1.05 | 4         | 10       |
    /// | 포획 Capture | ×0.9 | ×1.1 | 1.0      | 1.6       | 72°    | 0.90 | 5         | 12       |
    /// | 저격 Snipe   | ×1.6 | ×0.75| 1.5      | 2.3       | 40°    | 0.80 | 5         | 11       |
    /// | 지원 Support | ×0.7 | ×0.9 | 1.0      | 1.4       | 90°    | 0.85 | 4         | 13       |
    /// | 힐 Heal      | ×0.75| ×1.0 | 1.1      | 1.4       | 100°   | 0.80 | 5         | 14       |
    ///
    /// 힐은 회복/보호막이 공격력 배율이라 공격 배율을 지원보다 조금 높게 두고, 오래 버티며 넓게 돌보도록
    /// 체력·부채꼴·유지 시간을 넉넉히 준다. 대신 소환 비용이 지원보다 1 비싸고 공격 간격이 길다.
    ///
    /// 진화 단계마다: 소환 비용 +2, 유지 +1초, 이동 +0.03.  4성 이상 계보: 소환 비용 +1.
    /// 잔향이 수치는 범위를 넘지 않게 자른다: 소환 비용 4~9, 유지 10~16초, 이동 0.7~1.1.
    /// 요격 반경(range)은 지킴이 기준값이다. 돌격형 잔향이의 근접 교전 거리(약 0.35)는 전투 쪽에서 성향으로 정한다.
    /// </summary>
    internal static class FormStats
    {
        const float BaseAttack = 12f;
        const float BaseHp = 100f;
        const float TierGrowth = 1.35f;
        const float GradeGrowth = 1.12f;

        const int CostPerTier = 2;
        const float LifetimePerTier = 1f;
        const float MoveSpeedPerTier = 0.03f;
        const int HighGradeExtraCost = 1;

        const int MinCost = 4, MaxCost = 9;
        const float MinLifetime = 10f, MaxLifetime = 16f;
        const float MinMoveSpeed = 0.7f, MaxMoveSpeed = 1.1f;

        readonly struct StyleProfile
        {
            public readonly float Attack, Hp, Interval, Range, Arc, MoveSpeed, Lifetime;
            public readonly int Cost;

            public StyleProfile(float attack, float hp, float interval, float range, float arc, float moveSpeed, int cost, float lifetime)
            {
                Attack = attack;
                Hp = hp;
                Interval = interval;
                Range = range;
                Arc = arc;
                MoveSpeed = moveSpeed;
                Cost = cost;
                Lifetime = lifetime;
            }
        }

        static readonly Dictionary<CombatStyle, StyleProfile> Profiles = new()
        {
            //                                     공격  체력   간격  반경  부채꼴 이동   비용 유지
            [CombatStyle.Defense] = new StyleProfile(0.8f, 1.6f,  1.0f, 1.5f, 80f, 0.70f, 5, 14f),
            [CombatStyle.Assault] = new StyleProfile(1.4f, 1.0f,  0.8f, 1.3f, 60f, 1.05f, 4, 10f),
            [CombatStyle.Capture] = new StyleProfile(0.9f, 1.1f,  1.0f, 1.6f, 72f, 0.90f, 5, 12f),
            [CombatStyle.Snipe]   = new StyleProfile(1.6f, 0.75f, 1.5f, 2.3f, 40f, 0.80f, 5, 11f),
            [CombatStyle.Support] = new StyleProfile(0.7f, 0.9f,  1.0f, 1.4f, 90f, 0.85f, 4, 13f),
            [CombatStyle.Heal]    = new StyleProfile(0.75f, 1.0f, 1.1f, 1.4f, 100f, 0.80f, 5, 14f),
        };

        /// <summary>형태의 성향/단계와 계보 등급으로 능력치 필드를 채운다.</summary>
        public static void Apply(CharacterFormDef form, ItemGrade lineageGrade)
        {
            var p = Profiles[form.style];
            int tier = Mathf.Max(0, form.tier);
            float growth = Mathf.Pow(TierGrowth, tier) * Mathf.Pow(GradeGrowth, (int)lineageGrade);

            form.attack = Round(BaseAttack * p.Attack * growth, 1);
            form.hp = Round(BaseHp * p.Hp * growth, 0);
            form.attackInterval = p.Interval;
            form.range = p.Range;
            form.arcDegrees = p.Arc;

            int gradeCost = lineageGrade >= ItemGrade.Legendary ? HighGradeExtraCost : 0;
            form.summonCost = Mathf.Clamp(p.Cost + tier * CostPerTier + gradeCost, MinCost, MaxCost);
            form.lifetime = Mathf.Clamp(p.Lifetime + tier * LifetimePerTier, MinLifetime, MaxLifetime);
            form.moveSpeed = Round(Mathf.Clamp(p.MoveSpeed + tier * MoveSpeedPerTier, MinMoveSpeed, MaxMoveSpeed), 2);
        }

        static float Round(float value, int digits)
        {
            float scale = Mathf.Pow(10f, digits);
            return Mathf.Round(value * scale) / scale;
        }
    }
}
