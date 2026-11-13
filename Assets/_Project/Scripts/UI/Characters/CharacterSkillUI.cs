using System.Text;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 스킬 표기 도우미 (캐릭터 상세 / 형태 정보 / 진화 레시피 / 소환 결과 공용).
    /// 발동 시점("8초마다", "잔향이로 소환될 때", "수선점이 위험할 때 1회"), 대상, 효과 요약 문구와 색을 만든다.
    /// 화면을 갱신할 때만 부른다 (매 프레임 호출 금지).
    /// </summary>
    public static class CharacterSkillUI
    {
        public static bool HasSkill(SkillDef skill) => skill != null && !string.IsNullOrEmpty(skill.name);

        /// <summary>잔향이로 배치됐을 때만 발동하는 스킬</summary>
        public static bool IsEchoOnly(SkillTrigger trigger) => trigger == SkillTrigger.OnSummon || trigger == SkillTrigger.OnDissolve;

        public static string Seconds(float seconds) => $"{seconds:0.#}초";

        /// <summary>발동 시점 한 줄. 예) "8초마다", "적을 처치할 때 · 3초에 한 번", "수선점이 위험할 때 1회"</summary>
        public static string TriggerText(SkillDef skill)
        {
            if (skill == null) return string.Empty;
            float cooldown = skill.cooldown;
            bool hasCooldown = cooldown > 0.01f;
            return skill.trigger switch
            {
                SkillTrigger.Passive => "항상 발동",
                SkillTrigger.Cooldown => hasCooldown ? $"{Seconds(cooldown)}마다" : "쉬지 않고",
                SkillTrigger.OnSummon => "잔향이로 소환될 때",
                SkillTrigger.OnDissolve => "빛으로 풀려 사라질 때",
                SkillTrigger.OnKill => hasCooldown ? $"적을 처치할 때 · {Seconds(cooldown)}에 한 번" : "적을 처치할 때마다",
                SkillTrigger.OnLampLit => "수선점 불을 밝힐 때",
                SkillTrigger.OnShopDamaged => hasCooldown ? $"수선점이 공격받을 때 · {Seconds(cooldown)}에 한 번" : "수선점이 공격받을 때",
                SkillTrigger.OnShopLowHp => "수선점이 위험할 때 1회",
                SkillTrigger.OnBattleStart => "전투 시작 시 1회",
                _ => string.Empty,
            };
        }

        /// <summary>발동 시점 칩 색 (밤 패널/크림 카드 모두에서 읽히는 파스텔)</summary>
        public static Color TriggerColor(SkillTrigger trigger) => trigger switch
        {
            SkillTrigger.Passive => UITheme.Lavender,
            SkillTrigger.Cooldown => UITheme.SoftBlue,
            SkillTrigger.OnSummon => UITheme.EchoGlow,
            SkillTrigger.OnDissolve => UITheme.EchoGlow,
            SkillTrigger.OnKill => UITheme.DustyPink,
            SkillTrigger.OnLampLit => UITheme.Lamp,
            SkillTrigger.OnShopDamaged => new Color32(0xFF, 0xB0, 0x9E, 0xFF),
            SkillTrigger.OnShopLowHp => new Color32(0xFF, 0x9F, 0xA8, 0xFF),
            SkillTrigger.OnBattleStart => UITheme.Gold,
            _ => UITheme.ParchmentDark,
        };

        public static string TargetName(SkillTargetType target) => target switch
        {
            SkillTargetType.Self => "자신",
            SkillTargetType.Shop => "수선점",
            SkillTargetType.EnemiesInRange => "범위 안의 적",
            SkillTargetType.EnemiesNearSelf => "주변의 적",
            SkillTargetType.EnemiesNearTarget => "대상 주변의 적",
            SkillTargetType.StrongestEnemy => "가장 강한 적",
            SkillTargetType.WeakestEnemy => "가장 약한 적",
            SkillTargetType.AllEnemies => "모든 적",
            SkillTargetType.AlliesNearSelf => "주변 잔향이",
            SkillTargetType.AllEchoes => "모든 잔향이",
            SkillTargetType.AdjacentGuardians => "양옆 지킴이",
            SkillTargetType.AllGuardians => "모든 지킴이",
            SkillTargetType.AllAllies => "모든 친구",
            SkillTargetType.Lane => "한 방향의 모든 적",
            _ => string.Empty,
        };

        public static string EffectName(SkillEffectType type) => type switch
        {
            SkillEffectType.Damage => "피해",
            SkillEffectType.Heal => "회복",
            SkillEffectType.Shield => "보호막",
            SkillEffectType.Slow => "감속",
            SkillEffectType.Stun => "기절",
            SkillEffectType.Knockback => "밀쳐내기",
            SkillEffectType.Pull => "끌어당기기",
            SkillEffectType.Mark => "표식",
            SkillEffectType.BuffAttack => "공격력 강화",
            SkillEffectType.BuffAttackSpeed => "공격 속도 강화",
            SkillEffectType.Summon => "소환",
            SkillEffectType.ResonanceGain => "잔향 충전",
            SkillEffectType.LampGain => "불빛 충전",
            SkillEffectType.Reveal => "드러내기",
            SkillEffectType.DamageOverTime => "지속 피해",
            SkillEffectType.Execute => "마무리 일격",
            SkillEffectType.Chain => "연쇄",
            SkillEffectType.AttackRamp => "점점 강해짐",
            SkillEffectType.Cleanse => "정화",
            SkillEffectType.HealOverTime => "지속 회복",
            _ => type.ToString(),
        };

        /// <summary>효과 계열 색: 공격 분홍 / 묶기 라벤더 / 회복 핑크 / 보호막 하늘 / 강화 금색 / 소환·잔향 보라 / 불빛 등불색</summary>
        public static Color EffectColor(SkillEffectType type) => type switch
        {
            SkillEffectType.Damage or SkillEffectType.DamageOverTime or SkillEffectType.Chain
                or SkillEffectType.Execute or SkillEffectType.AttackRamp => UITheme.StyleColor(CombatStyle.Assault),
            SkillEffectType.Stun or SkillEffectType.Slow or SkillEffectType.Pull or SkillEffectType.Knockback
                or SkillEffectType.Mark or SkillEffectType.Reveal => UITheme.StyleColor(CombatStyle.Capture),
            SkillEffectType.Heal or SkillEffectType.HealOverTime or SkillEffectType.Cleanse => UITheme.StyleColor(CombatStyle.Heal),
            SkillEffectType.Shield => UITheme.StyleColor(CombatStyle.Defense),
            SkillEffectType.BuffAttack or SkillEffectType.BuffAttackSpeed => UITheme.Gold,
            SkillEffectType.Summon => UITheme.EchoGlow,
            SkillEffectType.ResonanceGain => UITheme.Resonance,
            SkillEffectType.LampGain => UITheme.Lamp,
            _ => UITheme.Lavender,
        };

        /// <summary>스킬 대표 색 (첫 효과 계열). 스킬 아이콘 고리/빛에 쓴다.</summary>
        public static Color AccentColor(SkillDef skill)
        {
            if (skill == null || skill.IsEmpty || skill.effects[0] == null) return UITheme.Lavender;
            return EffectColor(skill.effects[0].type);
        }

        /// <summary>
        /// 크림 카드 위에서 읽히도록 파스텔을 잉크 쪽으로 진하게 (M29: 24px 이하 글자 4.5:1).
        /// 0.68 = 가장 밝은 파스텔(빛 특성 · 등불 #FFD98A)도 양피지 #F6EDDA 위 4.7:1, 성향 · 역할 색은 5.0:1 이상 (예전 0.52는 3.5~4.1:1)
        /// </summary>
        public static Color InkTint(Color pastel) => Color.Lerp(pastel, UITheme.Ink, InkTintAmount);

        /// <summary>InkTint가 잉크 쪽으로 섞는 정도</summary>
        public const float InkTintAmount = 0.68f;

        /// <summary>
        /// 대상 + 효과 요약 (리치 텍스트). 예) "대상 가장 강한 적  ·  기절 2초  ·  표식 +20% 5초"
        /// onParchment = 크림 카드 위(진한 잉크색), false = 밤 패널 위(파스텔).
        /// </summary>
        public static string EffectsLine(SkillDef skill, StringBuilder sb, bool onParchment = true)
        {
            sb.Clear();
            if (skill == null) return string.Empty;
            string sub = UITheme.ColorTag(onParchment ? UITheme.InkSub : UITheme.SubText);
            string separator = $"<color={sub}>  ·  </color>";

            sb.Append("<color=").Append(sub).Append(">대상</color> <b>").Append(TargetName(skill.target)).Append("</b>");
            if (skill.effects == null) return sb.ToString();
            foreach (var effect in skill.effects)
            {
                if (effect == null) continue;
                var color = EffectColor(effect.type);
                if (onParchment) color = InkTint(color);
                sb.Append(separator).Append("<color=").Append(UITheme.ColorTag(color)).Append("><b>").Append(EffectName(effect.type)).Append("</b>");
                string detail = EffectDetail(effect);
                if (detail.Length > 0) sb.Append(' ').Append(detail);
                sb.Append("</color>");
            }
            return sb.ToString();
        }

        /// <summary>효과 수치. 배율은 "공격력의 N%"를 짧게 "N%"로 적는다.</summary>
        public static string EffectDetail(SkillEffectDef effect)
        {
            float v = effect.value;
            float d = effect.duration;
            string time = d > 0.01f ? $" {Seconds(d)}" : string.Empty;
            return effect.type switch
            {
                SkillEffectType.Damage => $"{v * 100f:0}%",
                SkillEffectType.Heal => $"{v * 100f:0}%",
                SkillEffectType.Shield => $"{v * 100f:0}%{time}",
                SkillEffectType.Slow => $"{v * 100f:0}%{time}",
                SkillEffectType.Stun => d > 0.01f ? Seconds(d) : string.Empty,
                SkillEffectType.Knockback => string.Empty,
                SkillEffectType.Pull => string.Empty,
                SkillEffectType.Mark => $"+{v:0.#}%{time}",
                SkillEffectType.BuffAttack => $"+{v:0.#}%{time}",
                SkillEffectType.BuffAttackSpeed => $"+{v:0.#}%{time}",
                SkillEffectType.Summon => $"{Mathf.Max(1, effect.count)}마리{time}",
                SkillEffectType.ResonanceGain => $"+{v:0.#}",
                SkillEffectType.LampGain => $"+{v * 100f:0}%",
                SkillEffectType.Reveal => d > 0.01f ? Seconds(d) : string.Empty,
                SkillEffectType.DamageOverTime => $"초당 {v * 100f:0}%{time}",
                SkillEffectType.Execute => $"+{v:0.#}%",
                SkillEffectType.Chain => $"{Mathf.Max(1, effect.count)}회 {v * 100f:0}%",
                SkillEffectType.AttackRamp => effect.count > 0 ? $"초당 +{v:0.#}% (최대 {effect.count}%)" : $"초당 +{v:0.#}%",
                SkillEffectType.Cleanse => d > 0.01f ? $"면역{time}" : string.Empty,
                SkillEffectType.HealOverTime => $"초당 {v * 100f:0}%{time}",
                _ => string.Empty,
            };
        }

        /// <summary>시전 대사 (따옴표 포함, 없으면 빈 문자열)</summary>
        public static string Quote(SkillDef skill) =>
            skill == null || string.IsNullOrEmpty(skill.castText) ? string.Empty : $"“{skill.castText.Trim('"', '“', '”')}”";
    }
}
