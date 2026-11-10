using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 지킴이/잔향이/덱 카드가 같이 쓰는 캐릭터 그림 조회. 초상화가 없으면 계보 색 원 + 이름 첫 글자로 대체한다.
    /// 형태 고유 스킬과 상시 패시브(녹아드는 빛·벨벳 마무리) 조회도 여기서 한다. 데이터가 비어 있어도 예외를 내지 않는다.
    /// </summary>
    public static class BattleUnitArt
    {
        public static Sprite Portrait(string formId)
        {
            if (string.IsNullOrEmpty(formId) || !Game.IsReady || Game.Characters == null) return null;
            return Game.Characters.GetPortrait(formId);
        }

        public static LineageDef Lineage(string lineageId, string formId)
        {
            if (!Game.IsReady || Game.Characters?.Db == null) return null;
            var lineage = string.IsNullOrEmpty(lineageId) ? null : Game.Characters.Db.GetLineage(lineageId);
            if (lineage == null && !string.IsNullOrEmpty(formId)) lineage = Game.Characters.Db.GetLineageOfForm(formId);
            return lineage;
        }

        public static Color MainColor(string lineageId, string formId)
        {
            var lineage = Lineage(lineageId, formId);
            return lineage != null ? lineage.mainColor : UITheme.Lavender;
        }

        /// <summary>형태 단계 (0 기본형, 1 1차, 2 2차 진화형). 모르면 0</summary>
        public static int FormTier(string formId)
        {
            if (string.IsNullOrEmpty(formId) || !Game.IsReady || Game.Characters?.Db == null) return 0;
            var form = Game.Characters.Db.GetForm(formId);
            return form != null ? form.tier : 0;
        }

        /// <summary>신화 등급 계보인지 (라라벨: 기본 공격 명중에 별가루)</summary>
        public static bool IsMythic(string lineageId, string formId)
        {
            var lineage = Lineage(lineageId, formId);
            return lineage != null && lineage.grade == ItemGrade.Mythic;
        }

        public static string DisplayName(string formId)
        {
            if (string.IsNullOrEmpty(formId) || !Game.IsReady || Game.Characters == null) return string.Empty;
            return Game.Characters.GetDisplayName(formId) ?? string.Empty;
        }

        public static string Stars(string lineageId, string formId)
        {
            var lineage = Lineage(lineageId, formId);
            return lineage != null ? UITheme.Stars(lineage.grade) : string.Empty;
        }

        /// <summary>형태 고유 스킬 (없거나 효과가 비었으면 null)</summary>
        public static SkillDef FormSkill(string formId)
        {
            if (string.IsNullOrEmpty(formId) || !Game.IsReady || Game.Characters?.Db == null) return null;
            var form = Game.Characters.Db.GetForm(formId);
            return form != null && form.skill != null && !form.skill.IsEmpty ? form.skill : null;
        }

        /// <summary>상시(Passive) 스킬이 전투 내내 거는 효과: 공격력 상승(녹아드는 빛)·처형(벨벳 마무리)</summary>
        public static UnitPassive PassiveOf(SkillDef skill)
        {
            if (skill == null || skill.trigger != SkillTrigger.Passive || skill.effects == null) return default;
            float ramp = 0f;
            float cap = 0f;
            float execute = 0f;
            for (int i = 0; i < skill.effects.Count; i++)
            {
                var fx = skill.effects[i];
                if (fx == null) continue;
                if (fx.type == SkillEffectType.AttackRamp)
                {
                    ramp += Mathf.Max(0f, fx.value);
                    // 시뮬레이터와 같은 기본 상한 (count가 없으면 50%)
                    cap += fx.count > 0 ? fx.count : 50f;
                }
                else if (fx.type == SkillEffectType.Execute) execute += Mathf.Max(0f, fx.value);
            }
            return new UnitPassive(ramp, cap, execute, skill.vfx);
        }

        /// <summary>초상화 또는 대체 토큰(원 + 첫 글자)을 보여준다.</summary>
        public static void Apply(Image portrait, Image fallback, TMP_Text fallbackInitial, string formId, string lineageId)
        {
            var sprite = Portrait(formId);
            bool hasArt = sprite != null;
            if (portrait != null)
            {
                portrait.sprite = sprite;
                portrait.enabled = hasArt;
            }
            if (fallback != null)
            {
                fallback.gameObject.SetActive(!hasArt && !string.IsNullOrEmpty(formId));
                fallback.color = MainColor(lineageId, formId);
            }
            if (fallbackInitial != null && !hasArt)
            {
                string name = DisplayName(formId);
                fallbackInitial.text = name.Length > 0 ? name.Substring(0, 1) : "?";
            }
        }
    }

    /// <summary>유닛 하나의 상시 패시브 (연출용 값). 기본값 = 패시브 없음</summary>
    public readonly struct UnitPassive
    {
        /// <summary>처형이 붙는 적 체력 비율 (SkillEffectDef 주석: 체력 30% 이하)</summary>
        public const float ExecuteRatio = 0.3f;

        /// <summary>살아 있는 동안 초당 오르는 공격력 %</summary>
        public readonly float RampPerSecond;
        /// <summary>공격력 상승 상한 %</summary>
        public readonly float RampCap;
        /// <summary>약해진 적에게 주는 추가 피해 %</summary>
        public readonly float ExecutePercent;
        /// <summary>스킬 연출 키 (촛농 / 입맞춤 그림)</summary>
        public readonly string Vfx;

        public UnitPassive(float rampPerSecond, float rampCap, float executePercent, string vfx)
        {
            RampPerSecond = rampPerSecond;
            RampCap = rampCap;
            ExecutePercent = executePercent;
            Vfx = vfx;
        }

        public bool HasRamp => RampPerSecond > 0f && RampCap > 0f;
        public bool HasExecute => ExecutePercent > 0f;

        /// <summary>살아 있던 시간만큼 오른 공격력의 상한 대비 비율 (0~1)</summary>
        public float RampFraction(float aliveSeconds) => HasRamp ? Mathf.Clamp01(aliveSeconds * RampPerSecond / RampCap) : 0f;
    }
}
