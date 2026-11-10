using System.Collections.Generic;
using System.Text;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 시너지 단계 설명/능력치 문구를 수치에서 만든다 (콘텐츠 빌드 시 1회).
    /// 예) "천 특성 친구 체력 +15%, 보호막량 +10%", "모든 친구 공격력 +15% / 단추 획득량 +25%"
    /// 능력치 한 줄은 SynergySystem.DescribeModifier를 그대로 써서, 단계 설명(도감 · 캐릭터 화면)과
    /// 시너지 단계 줄(능력치 목록)이 같은 이름 · 부호를 보여 준다. 여기서는 구성원/팀 머리말만 붙인다.
    /// </summary>
    internal static class SynergyText
    {
        /// <summary>
        /// 팀 전체에만 의미가 있는 능력치 (수선점/잔향/불빛/보상). 구성원 전용(membersOnly) 단계에는 넣지 않는다
        /// — 계보별 능력치 묶음에 들어가면 전투가 읽지 않을 수 있다 (ContentValidator가 경고).
        /// </summary>
        public static bool IsTeamStat(StatType stat) => stat switch
        {
            StatType.ShopMaxHpPercent => true,
            StatType.ShopDamageReductionPercent => true,
            StatType.ShopRegenPercentPerSecond => true,
            StatType.ResonanceRegenPercent => true,
            StatType.StartResonanceFlat => true,
            StatType.EchoCostReductionFlat => true,
            StatType.LampGainPercent => true,
            StatType.LampDamagePercent => true,
            StatType.GoldGainPercent => true,
            _ => false,
        };

        /// <param name="memberLabel">구성원 호칭. 예) "천 특성 친구", "돌격 친구"</param>
        public static string DescribeTier(SynergyTier tier, string memberLabel)
        {
            var unit = new List<string>();
            var team = new List<string>();
            foreach (var modifier in tier.modifiers)
            {
                if (modifier == null) continue;
                (IsTeamStat(modifier.stat) ? team : unit).Add(StatLine(modifier.stat, modifier.value));
            }

            var sb = new StringBuilder();
            if (unit.Count > 0) sb.Append(tier.membersOnly ? memberLabel : "모든 친구").Append(' ').Append(string.Join(", ", unit));
            if (team.Count > 0)
            {
                if (sb.Length > 0) sb.Append(" / ");
                sb.Append(string.Join(", ", team));
            }
            return sb.ToString();
        }

        /// <summary>예) "공격력 +15%", "시작 잔향 +5", "수선점 받는 피해 -10%" (SynergySystem.DescribeModifier와 같은 문구)</summary>
        public static string StatLine(StatType stat, float value) => SynergySystem.DescribeModifier(stat, value);

        /// <summary>
        /// 보너스 스킬을 누가 쓰는지 알려 주는 문장. 보너스 스킬은 구성원 유닛(지킴이 · 잔향이)마다 따로 시전되므로
        /// (SynergySystem.BuildModifiers → BattleSimulator.CollectSkillInfos), 설명의 수치가 "한 번 시전" 값임을 밝힌다.
        /// 특성/조합 시너지는 구성원 계보의 유닛마다, 역할 시너지는 그 성향 형태의 유닛마다만 쓴다 (FormBonusSkills — 같은 계보의 다른 성향 형태는 쓰지 않는다).
        /// 예) "빛 특성 친구마다 따로 써요.", "돌격 친구마다 따로 써요.", "짝짝이·똑단이·찰칵이·몽당이 지킴이마다 한 번씩 써요."
        /// </summary>
        /// <param name="group">구성원 묶음 이름. 예) "빛 특성", "돌격"(역할), "우두커니·몽당이"</param>
        public static string CasterLine(string group, SkillTrigger trigger)
        {
            string who = trigger switch
            {
                SkillTrigger.OnBattleStart => "지킴이", // 전투 시작 스킬은 지킴이만 쓴다
                SkillTrigger.OnSummon => "잔향이",
                SkillTrigger.OnDissolve => "잔향이",
                _ => "친구",
            };
            string how = trigger switch
            {
                SkillTrigger.Passive => "따로 적용돼요",
                SkillTrigger.OnBattleStart => "한 번씩 써요",
                SkillTrigger.OnShopLowHp => "한 번씩 써요",
                _ => "따로 써요",
            };
            return $"{group} {who}마다 {how}.";
        }

        /// <summary>조합 계보 이름 묶음. 넷까지는 "우두커니·몽당이"처럼 이름을 잇고, 더 많으면 "일곱 계보"</summary>
        public static string ComboGroup(IReadOnlyList<string> names)
        {
            if (names.Count == 0) return "조합 계보";
            return names.Count <= 4 ? string.Join("·", names) : $"{CountWord(names.Count)} 계보";
        }

        /// <summary>UITheme.TraitName과 같은 표기</summary>
        public static string TraitName(TraitType trait) => trait switch
        {
            TraitType.Fabric => "천",
            TraitType.Metal => "금속",
            TraitType.Light => "빛",
            TraitType.Sound => "소리",
            TraitType.Night => "밤",
            TraitType.Rain => "비",
            TraitType.Charm => "치장",
            _ => trait.ToString(),
        };

        /// <summary>UITheme.StyleName과 같은 표기</summary>
        public static string StyleName(CombatStyle style) => style switch
        {
            CombatStyle.Defense => "방어",
            CombatStyle.Assault => "돌격",
            CombatStyle.Capture => "포획",
            CombatStyle.Snipe => "저격",
            CombatStyle.Support => "지원",
            CombatStyle.Heal => "힐",
            _ => style.ToString(),
        };

        /// <summary>UITheme.StageModifierName과 같은 표기</summary>
        public static string StageModifierName(StageModifierType modifier) => modifier switch
        {
            StageModifierType.Rain => "비 오는 밤",
            StageModifierType.Blackout => "정전",
            StageModifierType.FullMoon => "보름달",
            StageModifierType.Wind => "바람 부는 밤",
            StageModifierType.DustStorm => "먼지 폭풍",
            StageModifierType.Festival => "축제의 밤",
            _ => "",
        };

        /// <summary>예) "정전 스테이지에서는 효과가 1.5배가 돼요."</summary>
        public static string FavoredLine(StageModifierType modifier, float multiplier) =>
            modifier == StageModifierType.None
                ? ""
                : $"{StageModifierName(modifier)} 스테이지에서는 효과가 {SkillText.Num(multiplier)}배가 돼요.";

        static string CountWord(int n) => n switch
        {
            5 => "다섯",
            6 => "여섯",
            7 => "일곱",
            8 => "여덟",
            _ => n.ToString(),
        };

        /// <summary>
        /// 조합 조건 문장. 예) "짝짝이와 똑단이가 함께 편성되면 켜져요.", "짝짝이, 라라벨과 루즈리가 모두 편성되면 켜져요."
        /// </summary>
        public static string ComboCondition(IReadOnlyList<string> names)
        {
            if (names.Count == 0) return "";
            if (names.Count == 1) return $"{KoreanText.WithSubject(names[0])} 편성되면 켜져요.";
            if (names.Count >= 5) return $"{CountWord(names.Count)} 계보가 모두 한자리에 모이면 켜져요.";

            var sb = new StringBuilder();
            for (int i = 0; i < names.Count - 2; i++) sb.Append(names[i]).Append(", ");
            sb.Append(Korean.And(names[names.Count - 2])).Append(' ').Append(KoreanText.WithSubject(names[names.Count - 1]));
            sb.Append(names.Count == 2 ? " 함께 편성되면 켜져요." : " 모두 편성되면 켜져요.");
            return sb.ToString();
        }
    }
}
