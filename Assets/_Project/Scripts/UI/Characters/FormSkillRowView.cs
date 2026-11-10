using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 계보의 형태별 스킬 한 줄 (크림 카드 안): 형태 초상화 + 스킬 구슬 / 형태 이름·성향 / 스킬 이름·발동 시점 / 설명 / 해금 상태.
    /// 진화 기억도 노드와 같은 규칙으로 누르면 형태 정보 · 진화 레시피 · 단서 안내를 연다 (오른쪽 아래 'i' 표시가 누를 수 있음을 알린다).
    /// 칩 바탕은 InkLine (글자가 아닌 칠, M29), 글자는 InkSub.
    /// </summary>
    public class FormSkillRowView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] Image _background;
        [SerializeField] FriendPortrait _portrait;
        [SerializeField] CharacterSkillOrb _orb;
        [SerializeField] TMP_Text _formText;
        [SerializeField] TMP_Text _skillText;
        [SerializeField] TMP_Text _descText;
        [SerializeField] CharacterChip _stateChip;

        static readonly Color RowColor = new(0.91f, 0.84f, 0.71f, 0.5f);
        static readonly Color UnlockedRowColor = new(1f, 0.97f, 0.9f, 0.85f);

        CharacterFormDef _form;
        Action<CharacterFormDef> _onClick;

        void Awake() => _button.onClick.AddListener(OnClick);

        public void Set(CharacterFormDef form, LineageDef lineage, EvolutionState state, bool owned, Action<CharacterFormDef> onClick)
        {
            _form = form;
            _onClick = onClick;
            var skill = form.skill;
            bool unlocked = owned && state == EvolutionState.Unlocked;
            bool hidden = !owned || state == EvolutionState.Undiscovered;

            var tone = unlocked ? PortraitTone.Normal : hidden ? PortraitTone.Silhouette : PortraitTone.Clue;
            _portrait.Set(CharacterUI.Portrait(form), lineage, form.name, tone);
            _orb.Set(skill, !unlocked);
            _background.color = unlocked ? UnlockedRowColor : RowColor;

            string styleInk = UITheme.ColorTag(CharacterSkillUI.InkTint(UITheme.StyleColor(form.style)));
            _formText.text = $"{form.name}  <color={styleInk}>{UITheme.StyleName(form.style)}</color>  <size=85%>{CharacterUI.TierName(form.tier)}</size>";

            if (CharacterSkillUI.HasSkill(skill))
            {
                string trigger = UITheme.ColorTag(UITheme.InkSub);
                _skillText.text = $"<b>{skill.name}</b>  <size=78%><color={trigger}>{CharacterSkillUI.TriggerText(skill)}</color></size>";
                _descText.text = skill.description;
            }
            else
            {
                _skillText.text = "<b>???</b>";
                _descText.text = "스킬 정보가 아직 없어요.";
            }
            _skillText.color = unlocked ? UITheme.Ink : UITheme.InkSub;

            SetState(form, state, owned);
        }

        void SetState(CharacterFormDef form, EvolutionState state, bool owned)
        {
            if (form.tier == 0 && owned)
            {
                _stateChip.Set("기본형", UITheme.SoftBlue, UITheme.Ink);
                return;
            }
            if (!owned)
            {
                _stateChip.Set("미보유", CharacterUI.WithAlpha(UITheme.InkLine, 0.22f), UITheme.InkSub);
                return;
            }
            bool craftable = state == EvolutionState.ClueFound && CharacterUI.IsCraftable(form.id);
            if (craftable)
            {
                _stateChip.Set("제작 가능", UITheme.GoldAccent, UITheme.Ink);
                return;
            }
            switch (state)
            {
                case EvolutionState.Unlocked:
                    _stateChip.Set("해금", UITheme.PrimaryButton, Color.white);
                    break;
                case EvolutionState.ClueFound:
                    _stateChip.Set("단서 발견", UITheme.DustyPink, UITheme.Ink);
                    break;
                default:
                    _stateChip.Set("미발견", CharacterUI.WithAlpha(UITheme.InkLine, 0.22f), UITheme.InkSub);
                    break;
            }
        }

        void OnClick()
        {
            if (_form != null) _onClick?.Invoke(_form);
        }
    }
}
