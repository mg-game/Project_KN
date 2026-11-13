namespace IdleGame.EditorTools
{
    /// <summary>
    /// PRG-07 잔향 축복 (SPEC4 §8.6 · 부록 B.4, 확장 P2): 던전 3층마다 셋 중 하나를 고르는 던전 전용 영구 능력치 12종 (종류마다 2번까지).
    /// 아이콘 = Icons/{iconKey} (스킬 아이콘). 끈 동안(<see cref="Stretch.Blessings"/>) 목록이 비어 있다 — DungeonSystem은 빈 목록을 "꺼짐"으로 본다.
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillBlessings(MetaDatabase db, GameContent content)
        {
            if (!On(Stretch.Blessings)) return;
            var list = db.blessings;
            list.Add(Blessing("b_shield", "보호막 실", StatType.ShieldPowerPercent, 20, "모든 잔향이 보호막 +20%", "skill_shield_bubble"));
            list.Add(Blessing("b_attack", "반짝이는 바늘", StatType.AttackPercent, 15, "잔향이 공격력 +15%", "skill_thread_stitch"));
            list.Add(Blessing("b_regen", "잔향 샘", StatType.ResonanceRegenPercent, 20, "잔향 회복 +20%", "skill_moon_glow"));
            list.Add(Blessing("b_cost", "가벼운 발걸음", StatType.EchoCostReductionFlat, 1, "잔향이 소환 비용 −1", "skill_summon_circle"));
            list.Add(Blessing("b_life", "오래가는 빛", StatType.EchoLifetimePercent, 25, "잔향이 머무는 시간 +25%", "skill_reveal_light"));
            list.Add(Blessing("b_crit", "별빛 눈", StatType.CritChancePercent, 8, "치명타 확률 +8%", "skill_star_burst"));
            list.Add(Blessing("b_boss", "큰그림자 사냥", StatType.BossDamagePercent, 20, "보스 피해 +20%", "skill_shadow_burst"));
            list.Add(Blessing("b_start", "아침 잔향", StatType.StartResonanceFlat, 15, "시작 잔향 +15", "skill_sound_ring"));
            list.Add(Blessing("b_speed", "바람 발", StatType.EchoMoveSpeedPercent, 15, "잔향이 이동 속도 +15%", "skill_storm_swirl"));
            list.Add(Blessing("b_heal", "포근한 손길", StatType.HealPowerPercent, 25, "회복량 +25%", "skill_heart_heal"));
            list.Add(Blessing("b_stun", "꼭 붙잡기", StatType.StunDurationPercent, 25, "기절 시간 +25%", "skill_stun_stars"));
            list.Add(Blessing("b_slow", "엉킨 실", StatType.SlowPowerPercent, 20, "감속 효과 +20%", "skill_slow_yarn"));
        }

        /// <summary>축복 하나를 쌓을 수 있는 최대 횟수</summary>
        internal const int BlessingMaxStacks = 2;

        static BlessingDef Blessing(string id, string name, StatType stat, float value, string desc, string iconKey) => new()
        {
            id = id,
            name = name,
            desc = desc,
            stat = stat,
            value = value,
            maxStacks = BlessingMaxStacks,
            iconKey = iconKey,
        };
    }
}
