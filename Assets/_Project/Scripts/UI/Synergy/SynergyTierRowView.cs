using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 상세의 단계 한 줄: 문턱 배지("3명") · 적용 범위(구성원만/팀 전체) · 상태(발동 중/달성/N명 더) ·
    /// 설명 · 능력치 줄(SynergySystem.DescribeModifier) · 보너스 스킬. 높이는 내용에 맞춰 늘어난다 (VerticalLayoutGroup).
    /// 설명이 능력치 줄을 그대로 풀어 쓴 문장이면(수치가 모두 들어 있으면) 같은 내용을 두 번 보이지 않게 설명은 숨긴다.
    /// </summary>
    public class SynergyTierRowView : MonoBehaviour
    {
        [SerializeField] Image _bg;
        [SerializeField] Image _frame;
        [SerializeField] Image _badge;
        [SerializeField] TMP_Text _badgeText;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] Image _scopeChip;
        [SerializeField] TMP_Text _scopeText;
        [SerializeField] Image _stateChip;
        [SerializeField] TMP_Text _stateText;
        [SerializeField] TMP_Text _descText;
        [SerializeField] TMP_Text _modifierText;
        [SerializeField] GameObject _skillGroup;
        [SerializeField] TMP_Text _skillText;

        public void Set(SynergyStatus status, int index, float favoredMultiplier, StringBuilder sb)
        {
            var def = status.Def;
            var tier = def.tiers[index];
            int threshold = SynergyUI.TierThreshold(def, index);
            bool combo = def.kind == SynergyKind.Combo;
            bool current = index == status.TierIndex;
            bool reached = status.TierIndex >= index;
            var accent = SynergyUI.AccentColor(def);

            // 바탕은 보라 바느질 판 그림 — 강조색을 살짝 섞거나 알파만 낮춘다 (테두리 · 배지 · 칩은 흰 틴트용 그림이라 색 그대로)
            _bg.color = current ? Color.Lerp(Color.white, accent, 0.28f) : reached ? WithAlpha(Color.white, 0.9f) : WithAlpha(Color.white, 0.6f);
            _frame.color = current ? UITheme.Gold : reached ? WithAlpha(accent, 0.55f) : WithAlpha(UITheme.Divider, 0.6f);
            _badge.color = reached ? accent : UITheme.Secondary;
            _badgeText.text = combo ? "완성" : threshold.ToString();
            _badgeText.color = reached ? UITheme.Ink : UITheme.SubText;

            _titleText.text = combo ? "친구가 모두 모이면" : $"{threshold}명 이상";
            _titleText.color = reached ? UITheme.Cream : UITheme.SubText;

            _scopeChip.color = tier.membersOnly ? WithAlpha(accent, 0.35f) : WithAlpha(UITheme.Gold, 0.35f);
            _scopeText.text = tier.membersOnly ? "구성원만" : "팀 전체";

            string state;
            Color stateColor;
            if (current)
            {
                state = "발동 중";
                stateColor = UITheme.Gold;
            }
            else if (reached)
            {
                state = "달성";
                stateColor = WithAlpha(accent, 0.8f);
            }
            else
            {
                int missing = combo ? SynergyUI.RequiredCount(def) - status.Count : threshold - status.Count;
                state = missing > 0 ? $"{missing}명 더" : "잠김";
                stateColor = UITheme.Secondary;
            }
            _stateChip.color = stateColor;
            _stateText.text = state;
            _stateText.color = current || reached ? UITheme.Ink : UITheme.SubText;

            bool hasDesc = !string.IsNullOrEmpty(tier.description) && !Restates(tier.description, tier);
            _descText.gameObject.SetActive(hasDesc);
            if (hasDesc)
            {
                // 능력치 이름과 수치는 한 덩어리로 줄을 넘긴다 (M28)
                _descText.text = SynergyUI.KeepStatNumbers(tier.description);
                _descText.color = reached ? UITheme.Text : UITheme.SubText;
            }

            var lines = SynergyUI.ModifierLines(tier, sb);
            if (current && favoredMultiplier > 1.001f)
            {
                // 지금 스테이지 기믹으로 강해진 값도 함께
                var boosted = SynergyUI.ModifierLines(tier, sb, favoredMultiplier);
                if (!string.IsNullOrEmpty(boosted))
                {
                    lines += $"\n<color={UITheme.ColorTag(UITheme.Lamp)}>지금 스테이지에서 ×{favoredMultiplier:0.##}</color>\n" +
                             $"<color={UITheme.ColorTag(UITheme.Lamp)}>{boosted}</color>";
                }
            }
            _modifierText.gameObject.SetActive(!string.IsNullOrEmpty(lines));
            _modifierText.text = SynergyUI.KeepStatNumbers(lines);
            _modifierText.color = reached ? UITheme.Positive : WithAlpha(UITheme.Positive, 0.55f);

            var skill = tier.bonusSkill;
            bool hasSkill = skill != null && !string.IsNullOrEmpty(skill.name);
            _skillGroup.SetActive(hasSkill);
            if (hasSkill)
            {
                string desc = string.IsNullOrEmpty(skill.description) ? string.Empty : $"\n<size=90%>{SynergyUI.KeepStatNumbers(skill.description)}</size>";
                _skillText.text = $"<color={UITheme.ColorTag(UITheme.Gold)}>보너스 스킬</color>  <b>{skill.name}</b>{desc}";
                _skillText.color = reached ? UITheme.Text : UITheme.SubText;
            }
        }

        /// <summary>설명에 모든 능력치 수치가 들어 있으면 능력치 줄을 다시 쓴 문장으로 본다</summary>
        static bool Restates(string description, SynergyTier tier)
        {
            if (tier.modifiers == null || tier.modifiers.Count == 0) return false;
            bool any = false;
            foreach (var modifier in tier.modifiers)
            {
                if (modifier == null || modifier.value == 0f) continue;
                any = true;
                string number = Mathf.Abs(modifier.value).ToString("0.##", CultureInfo.InvariantCulture);
                if (!description.Contains(number)) return false;
            }
            return any;
        }

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
