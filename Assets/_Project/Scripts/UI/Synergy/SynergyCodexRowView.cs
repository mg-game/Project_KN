using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 도감 한 줄: 큰 아이콘 · 이름 · 종류/상태 칩 · 단계 요약 · 관여 친구 얼굴 · 진행 막대(특성/역할) 또는 보유/편성 수(조합).
    /// 누르면 시너지 상세.
    /// </summary>
    public class SynergyCodexRowView : MonoBehaviour
    {
        const int MaxMembers = 7;
        /// <summary>이번 스테이지 기믹에서 강해지는 시너지 칩 앞말</summary>
        const string FavoredNowPrefix = "이번 스테이지 ";

        [SerializeField] Button _button;
        [SerializeField] Image _bg;
        [SerializeField] Image _frame;
        [SerializeField] Image _iconGlow;
        [SerializeField] Image _iconRing;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _iconFallback;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] Image _kindChip;
        [SerializeField] TMP_Text _kindText;
        [SerializeField] Image _stateChip;
        [SerializeField] TMP_Text _stateText;
        [SerializeField] TMP_Text _summaryText;
        [SerializeField] RectTransform _memberContent;
        [SerializeField] SynergyMiniMemberView _memberPrefab;
        [SerializeField] TMP_Text _moreText;
        [SerializeField] SynergyProgressBar _progress;
        [SerializeField] TMP_Text _comboText;
        [SerializeField] GameObject _favoredGroup;
        [SerializeField] Image _favoredIcon;
        [SerializeField] TMP_Text _favoredFallback;
        [SerializeField] TMP_Text _favoredText;

        static readonly Color InactiveTint = new(0.72f, 0.69f, 0.84f, 1f);
        static readonly List<SynergyMember> Members = new();
        static readonly StringBuilder Sb = new();
        UIListPool<SynergyMiniMemberView> _members;
        string _synergyId;

        void Awake()
        {
            _members = new UIListPool<SynergyMiniMemberView>(_memberPrefab, _memberContent);
            _button.onClick.AddListener(() => UINav.OpenSynergy(_synergyId));
        }

        public void Set(SynergyStatus status, StageModifierType stage)
        {
            var def = status.Def;
            _synergyId = def.id;
            var accent = SynergyUI.AccentColor(def);
            bool active = status.IsActive;

            // 바탕은 보라 바느질 판 그림 — 강조색을 살짝 섞거나 알파만 낮춘다 (테두리는 흰 틀이라 강조색 그대로)
            _bg.color = active ? Color.Lerp(Color.white, accent, 0.25f) : WithAlpha(Color.white, 0.72f);
            _frame.color = active ? accent : WithAlpha(UITheme.Divider, 0.8f);
            _iconGlow.gameObject.SetActive(active);
            _iconGlow.color = WithAlpha(accent, 0.75f);
            _iconRing.color = active ? Color.white : WithAlpha(Color.white, 0.65f);
            SynergyIcons.ApplyInRing(_icon, _iconFallback, def, 18f, 4f);
            // 꺼진 시너지 아이콘은 살짝 바랜 색으로
            if (!active && SynergyIcons.HasArt(_icon)) _icon.color = InactiveTint;

            _nameText.text = SynergyUI.DisplayName(def);
            _nameText.color = active ? UITheme.Cream : UITheme.Text;
            _kindChip.color = SynergyUI.KindColor(def.kind);
            _kindText.text = UITheme.SynergyKindName(def.kind);
            SetState(status, accent);

            _summaryText.text = Summary(status);
            SetMembers(def);

            bool combo = def.kind == SynergyKind.Combo;
            _progress.gameObject.SetActive(!combo);
            _comboText.gameObject.SetActive(combo);
            if (combo)
            {
                string gold = UITheme.ColorTag(UITheme.Gold);
                int need = SynergyUI.RequiredCount(def);
                _comboText.text = $"보유 <color={gold}>{SynergyUI.OwnedComboFriends(def)}</color>/{need}   편성 <color={gold}>{status.Count}</color>/{need}";
            }
            else _progress.Set(status);

            bool hasFavored = def.favoredModifier != StageModifierType.None;
            _favoredGroup.SetActive(hasFavored);
            if (hasFavored)
            {
                // 상태 칩 아래 기믹 칩 (M44): 이번 스테이지 기믹이면 '이번 스테이지 ×1.5' 금빛, 아니면 기믹 아이콘 + '×1.5' 차분하게
                SynergyIcons.ApplyStage(_favoredIcon, _favoredFallback, def.favoredModifier);
                bool now = SynergyUI.IsFavoredNow(def, stage);
                _favoredText.text = now ? FavoredNowPrefix + SynergyUI.Multiplier(def) : SynergyUI.Multiplier(def);
                _favoredText.color = now ? UITheme.Gold : UITheme.SubText;
            }
        }

        void SetState(SynergyStatus status, Color accent)
        {
            if (status.IsActive)
            {
                bool combo = status.Def.kind == SynergyKind.Combo;
                _stateChip.color = UITheme.Gold;
                _stateText.text = combo ? "조합 완성" : $"{status.TierIndex + 1}단계 발동";
                _stateText.color = UITheme.Ink;
            }
            else if (SynergyUI.IsClose(status))
            {
                _stateChip.color = WithAlpha(accent, 0.55f);
                _stateText.text = "한 명 더!";
                _stateText.color = UITheme.Ink;
            }
            else
            {
                _stateChip.color = UITheme.Secondary;
                _stateText.text = "꺼짐";
                _stateText.color = UITheme.SubText;
            }
        }

        /// <summary>지금 단계(없으면 첫 단계) 설명. 특성/역할은 문턱 "2 · 3 · 4"를 앞에. 수치는 이름과 붙여 둔다 (SynergyUI.KeepStatNumbers)</summary>
        static string Summary(SynergyStatus status)
        {
            var def = status.Def;
            int count = SynergyUI.TierCount(def);
            if (count == 0) return def.description ?? string.Empty;

            int shown = Mathf.Clamp(status.IsActive ? status.TierIndex : 0, 0, count - 1);
            var tier = def.tiers[shown];
            string text = tier != null && !string.IsNullOrEmpty(tier.description) ? tier.description : SynergyUI.ModifierLines(tier, Sb).Replace('\n', ' ');
            if (string.IsNullOrEmpty(text)) text = def.description ?? string.Empty;
            // 능력치 이름과 수치는 한 덩어리로 줄을 넘긴다 ('공격력' / '+8%'로 갈라지지 않게, M28)
            text = SynergyUI.KeepStatNumbers(text);
            if (def.kind == SynergyKind.Combo) return text;

            string thresholds = SynergyUI.Thresholds(def, status.TierIndex, Sb);
            return $"{thresholds}명  <color={UITheme.ColorTag(UITheme.Text)}>{text}</color>";
        }

        void SetMembers(SynergyDef def)
        {
            SynergyUI.CollectMembers(def, Members);
            int shown = Mathf.Min(Members.Count, MaxMembers);
            _members.SetCount(shown);
            for (int i = 0; i < shown; i++) _members[i].Set(Members[i]);
            int more = Members.Count - shown;
            _moreText.gameObject.SetActive(more > 0);
            if (more > 0) _moreText.text = $"+{more}";
        }

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
