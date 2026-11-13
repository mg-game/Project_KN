using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 스킬 효과 값에서 설명 문장을 만든다 (콘텐츠 빌드 시 1회).
    /// 예) "10초마다 수선점 내구도를 공격력 300%만큼 회복하고, 공격력 150%만큼 피해를 막는 보호막을 6초 동안 씌워요."
    /// 대상 이름은 효과 편(적/아군)마다 처음 한 번만 붙이고, 조사(을/를, 에/에게)는 받침과 대상 종류로 고른다.
    /// 회복은 대상 이름을 앞에서 말했어도 목적어("내구도를"/"체력을")를 남긴다 ("…보호막을 씌우고, 내구도를 …만큼 회복해요").
    /// 효과 문장은 "~하고," 로 잇고 마지막만 "~해요." 로 끝낸다.
    /// </summary>
    internal static class SkillText
    {
        /// <summary>OnShopLowHp 발동 기준 (GameEnums.SkillTrigger 주석)</summary>
        const int ShopLowHpPercent = 30;
        /// <summary>Execute 기준 체력 (SkillEffectDef 주석)</summary>
        const int ExecuteHpPercent = 30;

        public static string Mechanics(SkillDef skill)
        {
            if (skill == null || skill.IsEmpty) return string.Empty;

            var target = new TargetWriter(skill.target);
            var sb = new StringBuilder();
            string trigger = TriggerPhrase(skill);
            if (trigger.Length > 0) sb.Append(trigger).Append(' ');

            var phrases = new List<(string and, string end)>();
            foreach (var effect in skill.effects)
            {
                if (effect == null) continue;
                target.Begin(effect.type);
                phrases.Add(EffectPhrase(effect, target));
            }
            for (int i = 0; i < phrases.Count; i++)
            {
                sb.Append(i < phrases.Count - 1 ? phrases[i].and + ", " : phrases[i].end);
            }
            sb.Append('.');
            return sb.ToString();
        }

        // ───────────────────────── 발동 시점 ─────────────────────────

        static string TriggerPhrase(SkillDef s) => s.trigger switch
        {
            SkillTrigger.Passive => "",
            SkillTrigger.Cooldown => $"{Num(s.cooldown)}초마다",
            SkillTrigger.OnSummon => "잔향이로 소환되면",
            SkillTrigger.OnDissolve => "빛으로 풀려 사라질 때",
            SkillTrigger.OnKill => s.cooldown > 0f ? $"적을 쓰러뜨리면({Num(s.cooldown)}초에 한 번까지)" : "적을 쓰러뜨릴 때마다",
            SkillTrigger.OnLampLit => "수선점에 불을 밝히면",
            SkillTrigger.OnShopDamaged => s.cooldown > 0f ? $"수선점이 공격받으면({Num(s.cooldown)}초에 한 번까지)" : "수선점이 공격받을 때마다",
            SkillTrigger.OnShopLowHp => $"수선점 내구도가 {ShopLowHpPercent}% 아래로 떨어지면(전투마다 한 번)",
            SkillTrigger.OnBattleStart => "전투가 시작되면",
            _ => "",
        };

        // ───────────────────────── 효과 ─────────────────────────

        /// <returns>("~하고" 이어지는 꼴, "~해요" 끝나는 꼴)</returns>
        static (string and, string end) EffectPhrase(SkillEffectDef e, TargetWriter t)
        {
            float v = e.value;
            string d = Num(e.duration);
            switch (e.type)
            {
                case SkillEffectType.Damage:
                    return Verb($"{t.To()}공격력 {Pct(v)} 피해를 ", "주고", "줘요");
                case SkillEffectType.Heal:
                    return Verb($"{t.Health()}공격력 {Pct(v)}만큼 ", "회복하고", "회복해요");
                case SkillEffectType.Shield:
                    return Verb($"{t.ShieldTo()}공격력 {Pct(v)}만큼 피해를 막는 보호막을 {d}초 동안 ", "씌우고", "씌워요");
                case SkillEffectType.Slow:
                    return Verb($"{t.Obj()}{d}초 동안 {Pct(v)} 느리게 ", "만들고", "만들어요");
                case SkillEffectType.Stun:
                    return Verb($"{t.Obj()}{d}초 동안 ", "기절시키고", "기절시켜요");
                case SkillEffectType.Knockback:
                    string how = v >= 0.4f ? "멀리" : v <= 0.2f ? "살짝" : "바깥으로";
                    return Verb($"{t.Obj()}{how} ", "밀어내고", "밀어내요");
                case SkillEffectType.Pull:
                    return Verb($"{t.Obj()}한데 ", "끌어당기고", "끌어당겨요");
                case SkillEffectType.Mark:
                    return Verb($"{t.To()}표식을 남겨 {d}초 동안 받는 피해를 {Num(v)}% ", "늘리고", "늘려요");
                case SkillEffectType.BuffAttack:
                    return Verb($"{t.AllyOf()}공격력을 {d}초 동안 {Num(v)}% ", "올리고", "올려요");
                case SkillEffectType.BuffAttackSpeed:
                    return Verb($"{t.AllyOf()}공격 속도를 {d}초 동안 {Num(v)}% ", "올리고", "올려요");
                case SkillEffectType.Summon:
                    return Verb($"능력치 {Pct(v)}의 작은 친구 {e.count}마리를 불러 {d}초 동안 함께 ", "싸우고", "싸워요");
                case SkillEffectType.ResonanceGain:
                    return Verb($"잔향을 {Num(v)} ", "채우고", "채워요");
                case SkillEffectType.LampGain:
                    return Verb($"불빛 게이지를 {Pct(v)} ", "채우고", "채워요");
                case SkillEffectType.Reveal:
                    return Verb($"숨어 있는 적을 {d}초 동안 ", "드러내고", "드러내요");
                case SkillEffectType.DamageOverTime:
                    return Verb($"{t.To()}{d}초 동안 초당 공격력 {Pct(v)} 화상 피해를 ", "주고", "줘요");
                case SkillEffectType.Execute:
                    return Verb($"체력이 {ExecuteHpPercent}% 이하인 적에게 주는 피해가 {Num(v)}% ", "늘어나고", "늘어나요");
                case SkillEffectType.Chain:
                    return Verb($"적 사이를 {e.count}번 튀는 공격력 {Pct(v)} 연쇄 피해를 ", "주고", "줘요");
                case SkillEffectType.AttackRamp:
                    return Verb($"살아 있는 동안 1초마다 공격력이 {Num(v)}%씩 올라 최대 {e.count}%까지 ", "커지고", "커져요");
                case SkillEffectType.Cleanse:
                    return Verb($"{t.AllyTo()}걸린 약화를 씻어 내 {d}초 동안 다시 걸리지 않게 ", "하고", "해요");
                case SkillEffectType.HealOverTime:
                    return Verb($"{t.Health()}{d}초 동안 초당 공격력 {Pct(v)}씩 ", "회복하고", "회복해요");
                default:
                    return Verb("", e.type.ToString(), e.type.ToString());
            }
        }

        static (string, string) Verb(string stem, string and, string end) => (stem + and, stem + end);

        /// <summary>
        /// 대상 이름을 효과 편(적 / 아군)마다 처음 한 번만 조사와 함께 내준다 (이후엔 이름 없이 이어 쓴다).
        /// 대상과 편이 엇갈리는 효과는 전투 규칙(BattleSimulator.Skills)대로 적는다:
        /// 적 대상 스킬의 아군 효과 → "자신", 아군 대상 스킬의 적 효과 → 시전 중심(수선점 또는 시전자) 주변의 적.
        /// 지킴이는 수선점과 한 몸이라 지킴이 대상 회복/보호막은 수선점으로 적는다.
        /// </summary>
        sealed class TargetWriter
        {
            readonly string _enemyNoun;
            readonly string _allyNoun;
            /// <summary>대상이 수선점 자체 (장소라 "에", 회복은 "내구도")</summary>
            readonly bool _allyIsShop;
            /// <summary>지킴이 대상: 회복/보호막/지속 회복이 수선점으로 간다</summary>
            readonly bool _allyToShop;
            bool _enemyUsed;
            bool _allyUsed;
            SkillEffectType? _current;
            SkillEffectType? _previous;

            public TargetWriter(SkillTargetType target)
            {
                bool enemyTarget = IsEnemyTarget(target);
                string noun = TargetNoun(target);
                _enemyNoun = enemyTarget ? noun : NearbyEnemyNoun(target);
                _allyNoun = enemyTarget ? TargetNoun(SkillTargetType.Self) : noun;
                _allyIsShop = target == SkillTargetType.Shop;
                _allyToShop = target == SkillTargetType.AllGuardians || target == SkillTargetType.AdjacentGuardians;
            }

            /// <summary>효과 문장 하나를 쓰기 전에 부른다 (바로 앞 효과가 회복이면 회복 대상을 다시 말하지 않는다)</summary>
            public void Begin(SkillEffectType type)
            {
                _previous = _current;
                _current = type;
            }

            // ── 적 효과 ──

            /// <summary>"모든 적을 "</summary>
            public string Obj() => TakeEnemy(Korean.Object(_enemyNoun));
            /// <summary>"모든 적에게 "</summary>
            public string To() => TakeEnemy(_enemyNoun + "에게");

            // ── 아군 효과 ──

            /// <summary>"모든 잔향이에게 " / "수선점에 " (정화)</summary>
            public string AllyTo() => TakeAlly(_allyNoun + (_allyIsShop ? "에" : "에게"));
            /// <summary>"모든 지킴이의 " (강화)</summary>
            public string AllyOf() => TakeAlly(_allyNoun + "의");

            /// <summary>보호막을 받는 쪽: 지킴이 대상이면 수선점 (지킴이 이름은 뒤 효과를 위해 아껴 둔다)</summary>
            public string ShieldTo() => _allyToShop ? "수선점에 " : AllyTo();

            /// <summary>
            /// 회복할 몫: "수선점 내구도를 " / "모든 잔향이의 체력을 ". 대상을 이미 말했으면 "내구도를 " / "체력을 "만 남기고,
            /// 바로 앞 효과도 회복이면 빈 문자열 ("…회복하고, 6초 동안 초당 …씩 회복해요").
            /// </summary>
            public string Health()
            {
                bool followsHeal = _previous == SkillEffectType.Heal || _previous == SkillEffectType.HealOverTime;
                if (_allyToShop) return followsHeal ? "" : "수선점 내구도를 ";
                if (string.IsNullOrEmpty(_allyNoun)) return "";
                if (!_allyUsed)
                {
                    _allyUsed = true;
                    return _allyIsShop ? "수선점 내구도를 " : _allyNoun + "의 체력을 ";
                }
                if (followsHeal) return "";
                return _allyIsShop ? "내구도를 " : "체력을 ";
            }

            string TakeEnemy(string phrase)
            {
                if (_enemyUsed || string.IsNullOrEmpty(_enemyNoun)) return "";
                _enemyUsed = true;
                return phrase + " ";
            }

            string TakeAlly(string phrase)
            {
                if (_allyUsed || string.IsNullOrEmpty(_allyNoun)) return "";
                _allyUsed = true;
                return phrase + " ";
            }
        }

        static string TargetNoun(SkillTargetType target) => target switch
        {
            SkillTargetType.Self => "자신",
            SkillTargetType.Shop => "수선점",
            SkillTargetType.EnemiesInRange => "요격 범위 안의 적",
            SkillTargetType.EnemiesNearSelf => "주변의 적",
            SkillTargetType.EnemiesNearTarget => "공격 대상 주변의 적",
            SkillTargetType.StrongestEnemy => "가장 강한 적(보스 우선)",
            SkillTargetType.WeakestEnemy => "체력이 가장 낮은 적",
            SkillTargetType.AllEnemies => "모든 적",
            SkillTargetType.AlliesNearSelf => "주변 잔향이",
            SkillTargetType.AllEchoes => "모든 잔향이",
            SkillTargetType.AdjacentGuardians => "양옆 지킴이",
            SkillTargetType.AllGuardians => "모든 지킴이",
            SkillTargetType.AllAllies => "모든 친구",
            SkillTargetType.Lane => "자기 방향(레인)의 모든 적",
            _ => "",
        };

        /// <summary>아군 대상 스킬의 적 효과가 가는 곳: 수선점 중심 대상이면 수선점 주변, 아니면 시전자 주변 (BattleSimulator.AllyCenter)</summary>
        static string NearbyEnemyNoun(SkillTargetType target) => target switch
        {
            SkillTargetType.Shop => "수선점 주변의 적",
            SkillTargetType.AllEchoes => "수선점 주변의 적",
            SkillTargetType.AllGuardians => "수선점 주변의 적",
            SkillTargetType.AllAllies => "수선점 주변의 적",
            _ => "주변의 적",
        };

        static bool IsEnemyTarget(SkillTargetType target) => target switch
        {
            SkillTargetType.EnemiesInRange or SkillTargetType.EnemiesNearSelf or SkillTargetType.EnemiesNearTarget
                or SkillTargetType.StrongestEnemy or SkillTargetType.WeakestEnemy or SkillTargetType.AllEnemies
                or SkillTargetType.Lane => true,
            _ => false,
        };

        // ───────────────────────── 숫자 ─────────────────────────

        /// <summary>1.5 → "1.5", 10 → "10"</summary>
        public static string Num(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>배율 → 백분율. 3 → "300%", 0.35 → "35%"</summary>
        public static string Pct(float ratio) => Num(ratio * 100f) + "%";
    }

    /// <summary>
    /// 한국어 조사 도우미. 주격(이/가)은 런타임 KoreanText.WithSubject를 쓰고, 여기서는 목적격(을/를) · 나열(와/과)을 고른다.
    /// 끝의 괄호 설명("가장 강한 적(보스 우선)")은 건너뛰고 그 앞 글자의 받침으로 판단한다.
    /// </summary>
    internal static class Korean
    {
        /// <summary>"수선점" → "수선점을", "잔향이" → "잔향이를"</summary>
        public static string Object(string word) => string.IsNullOrEmpty(word) ? word : word + (HasBatchim(word) ? "을" : "를");

        /// <summary>"라라벨" → "라라벨과", "짝짝이" → "짝짝이와"</summary>
        public static string And(string word) => string.IsNullOrEmpty(word) ? word : word + (HasBatchim(word) ? "과" : "와");

        public static bool HasBatchim(string word)
        {
            for (int i = word.Length - 1; i >= 0; i--)
            {
                char c = word[i];
                if (c == ')')
                {
                    int open = word.LastIndexOf('(', i);
                    if (open > 0)
                    {
                        i = open;
                        continue;
                    }
                }
                if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 != 0;
                if (char.IsDigit(c)) return "013678".IndexOf(c) >= 0; // 영·일·삼·육·칠·팔
                if (!char.IsWhiteSpace(c)) return false;
            }
            return false;
        }
    }
}
