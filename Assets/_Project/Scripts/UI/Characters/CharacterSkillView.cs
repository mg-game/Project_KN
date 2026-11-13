using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 상세의 스킬·시너지 탭: 기본형 고유 스킬 카드 / 이 계보가 여는 시너지(특성·역할·조합, 누르면 시너지 도감) /
    /// 형태별 스킬 목록(진화하면 무엇을 얻는지 한눈에).
    /// </summary>
    public class CharacterSkillView : MonoBehaviour
    {
        [SerializeField] ScrollRect _scroll;
        [SerializeField] CharacterSkillCard _skillCard;
        [SerializeField] TMP_Text _synergyTitle;
        [SerializeField] RectTransform _synergyContent;
        [SerializeField] LineageSynergyRowView _synergyPrefab;
        [SerializeField] GameObject _synergyEmpty;
        [SerializeField] Button _codexButton;
        [SerializeField] TMP_Text _formsTitle;
        [SerializeField] RectTransform _formContent;
        [SerializeField] FormSkillRowView _formPrefab;

        UIListPool<LineageSynergyRowView> _synergies;
        UIListPool<FormSkillRowView> _forms;
        readonly List<SynergyDef> _defs = new();
        readonly Dictionary<string, SynergyStatus> _statuses = new();
        readonly List<CharacterFormDef> _formOrder = new();
        LineageDef _lineage;
        System.Action<CharacterFormDef> _onFormClicked;
        bool _initialized;

        void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            _synergies = new UIListPool<LineageSynergyRowView>(_synergyPrefab, _synergyContent);
            _forms = new UIListPool<FormSkillRowView>(_formPrefab, _formContent);
            _onFormClicked = OnFormClicked;
            _codexButton.onClick.AddListener(() => UINav.OpenSynergy(null));
        }

        public void Show(LineageDef lineage)
        {
            EnsureInit();
            bool switched = lineage != _lineage;
            _lineage = lineage;
            if (lineage == null) return;

            RefreshSkill();
            RefreshSynergies();
            RefreshForms(switched);
            if (switched) _scroll.verticalNormalizedPosition = 1f;
        }

        void RefreshSkill()
        {
            var baseForm = CharacterUI.BaseForm(_lineage);
            string owner = baseForm != null ? baseForm.name : _lineage.name;
            _skillCard.Show(baseForm?.skill, $"{owner}의 고유 스킬", !CharacterUI.IsOwned(_lineage));
        }

        void RefreshSynergies()
        {
            CharacterUI.CollectSynergies(_lineage.id, null, _defs, _statuses);
            int active = 0;
            _synergies.SetCount(_defs.Count);
            for (int i = 0; i < _defs.Count; i++)
            {
                var def = _defs[i];
                _statuses.TryGetValue(def.id ?? string.Empty, out var status);
                if (status != null && status.IsActive) active++;
                _synergies[i].Set(def, status, _lineage.id);
            }
            _synergyEmpty.SetActive(_defs.Count == 0);
            _synergyTitle.text = active > 0
                ? $"함께하면 강해지는 시너지  <size=80%><color={UITheme.ColorTag(CharacterUI.InkPositive)}>발동 중 {active}</color></size>"
                : $"함께하면 강해지는 시너지  <size=80%><color={UITheme.ColorTag(UITheme.InkSub)}>{_defs.Count}종</color></size>";
        }

        void RefreshForms(bool rebuildOrder)
        {
            if (rebuildOrder || _formOrder.Count == 0) BuildFormOrder();
            bool owned = CharacterUI.IsOwned(_lineage);
            int unlocked = 0;
            _forms.SetCount(_formOrder.Count);
            for (int i = 0; i < _formOrder.Count; i++)
            {
                var form = _formOrder[i];
                var state = Game.Evolution.GetState(form.id);
                if (owned && state == EvolutionState.Unlocked) unlocked++;
                _forms[i].Set(form, _lineage, state, owned, _onFormClicked);
            }
            _formsTitle.text = $"형태별 스킬  <size=80%><color={UITheme.ColorTag(UITheme.InkSub)}>익힌 스킬 {unlocked}/{_formOrder.Count}</color></size>";
        }

        /// <summary>기본형 → 1차 → 2차 순서 (같은 단계는 데이터 순서)</summary>
        void BuildFormOrder()
        {
            _formOrder.Clear();
            int maxTier = 0;
            foreach (var form in _lineage.forms)
            {
                if (form != null) maxTier = Mathf.Max(maxTier, form.tier);
            }
            for (int tier = 0; tier <= maxTier; tier++)
            {
                foreach (var form in _lineage.forms)
                {
                    if (form != null && form.tier == tier) _formOrder.Add(form);
                }
            }
        }

        void OnFormClicked(CharacterFormDef form) => CharacterUI.OpenForm(_lineage, form);
    }
}
