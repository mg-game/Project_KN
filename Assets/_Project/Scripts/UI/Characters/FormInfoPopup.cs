using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 해금된 형태 정보: 성향 / 추천 역할 / 특성 칩(누르면 설명 말풍선, M59) + 고유 스킬 카드 / 이 형태로 편성하면 함께 켜지는 시너지 /
    /// 설명 / 능력치(지킴이·잔향이 각각) + "편성하러 가기" (보라 — 이동 버튼, M26)
    /// </summary>
    public class FormInfoPopup : UIPopup
    {
        [SerializeField] TMP_Text _titleText;
        [SerializeField] FriendPortrait _portrait;
        [SerializeField] Image _glow;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _tierText;
        [SerializeField] CharacterChip _styleChip;
        [SerializeField] CharacterChip _roleChip;
        [SerializeField] CharacterChip[] _traitChips;
        [SerializeField] TMP_Text _styleHintText;
        [SerializeField] TMP_Text _powerText;

        [Header("본문 (스크롤)")]
        [SerializeField] ScrollRect _scroll;
        [SerializeField] CharacterSkillCard _skillCard;
        [SerializeField] TMP_Text _synergyTitle;
        [SerializeField] RectTransform _synergyContent;
        [SerializeField] LineageSynergyRowView _synergyPrefab;
        [SerializeField] GameObject _synergyEmpty;
        [SerializeField] TMP_Text _descText;
        [SerializeField] TMP_Text _commonStatsText;
        [SerializeField] TMP_Text _guardianStatsText;
        [SerializeField] TMP_Text _echoStatsText;
        [SerializeField] Button _formationButton;

        UIListPool<LineageSynergyRowView> _synergies;
        readonly List<SynergyDef> _defs = new();
        readonly Dictionary<string, SynergyStatus> _statuses = new();
        string _formId;
        bool _subscribed;

        protected override void Awake()
        {
            base.Awake();
            _synergies = new UIListPool<LineageSynergyRowView>(_synergyPrefab, _synergyContent);
            _formationButton.onClick.AddListener(GoToFormation);
            if (_styleChip != null) _styleChip.EnableTooltip();
            if (_roleChip != null) _roleChip.EnableTooltip();
            if (_traitChips != null)
            {
                foreach (var chip in _traitChips)
                {
                    if (chip != null) chip.EnableTooltip();
                }
            }
            if (Game.Synergy != null)
            {
                Game.Synergy.Changed += OnSynergyChanged;
                _subscribed = true;
            }
        }

        void OnDestroy()
        {
            if (_subscribed && Game.IsReady && Game.Synergy != null) Game.Synergy.Changed -= OnSynergyChanged;
        }

        public void Show(string formId)
        {
            bool switched = formId != _formId;
            _formId = formId;
            Open();
            if (switched) _scroll.verticalNormalizedPosition = 1f;
        }

        void OnSynergyChanged()
        {
            if (!IsOpen) return;
            var db = Game.Characters.Db;
            var form = db.GetForm(_formId);
            var lineage = db.GetLineageOfForm(_formId);
            if (form != null && lineage != null) RefreshSynergies(form, lineage);
        }

        protected override void OnOpen()
        {
            var db = Game.Characters.Db;
            var form = db.GetForm(_formId);
            var lineage = db.GetLineageOfForm(_formId);
            if (form == null || lineage == null) return;

            var stats = Game.Characters.GetStats(form.id);
            _titleText.text = $"{lineage.name} 계보 · {CharacterUI.TierName(form.tier)}";
            _portrait.Set(CharacterUI.Portrait(form), lineage, form.name, PortraitTone.Normal);
            _glow.color = CharacterUI.WithAlpha(UITheme.StyleColor(form.style), _glow.color.a);
            _nameText.text = CharacterUI.DisplayName(form);
            _tierText.text = $"{CharacterUI.TierName(form.tier)}  ·  {CharacterUI.StarsText(lineage.grade)}";

            _styleChip.SetStyle(form.style);
            _roleChip.SetRole(form.recommendedRole, " 추천");
            CharacterUI.SetTraitChips(_traitChips, lineage, false);
            _styleHintText.text = $"{CharacterUI.StyleHint(form.style)}\n<color={UITheme.ColorTag(UITheme.SubText)}>{CharacterUI.RoleHint(form.recommendedRole)}</color>";
            _powerText.text = $"전투력 <b>{stats.Power:N0}</b>";

            _skillCard.Show(form.skill, $"{form.name}의 고유 스킬");
            RefreshSynergies(form, lineage);
            _descText.text = string.IsNullOrEmpty(form.description) ? lineage.personality : form.description;

            string sub = UITheme.ColorTag(UITheme.InkSub);
            _commonStatsText.text =
                $"<color={sub}>공격력</color>  <b>{stats.attack:N0}</b>\n" +
                $"<color={sub}>체력</color>  <b>{stats.hp:N0}</b>\n" +
                $"<color={sub}>공격 간격</color>  <b>{stats.attackInterval:0.##}초</b>";
            _guardianStatsText.text =
                $"<color={sub}>요격 반경</color>  <b>{stats.range:0.##}</b>\n" +
                $"<color={sub}>요격 각도</color>  <b>{stats.arcDegrees:0}°</b>";
            _echoStatsText.text =
                $"<color={sub}>사거리</color>  <b>{stats.range:0.##}</b>\n" +
                $"<color={sub}>이동 속도</color>  <b>{stats.moveSpeed:0.##}</b>\n" +
                $"<color={sub}>소환 비용</color>  <b>잔향 {stats.summonCost}</b>\n" +
                $"<color={sub}>유지 시간</color>  <b>{stats.lifetime:0.#}초</b>";
        }

        /// <summary>이 형태로 편성했을 때 관여하는 시너지 (다른 성향의 역할 시너지는 뺀다)</summary>
        void RefreshSynergies(CharacterFormDef form, LineageDef lineage)
        {
            CharacterUI.CollectSynergies(lineage.id, form.style, _defs, _statuses);
            _synergies.SetCount(_defs.Count);
            for (int i = 0; i < _defs.Count; i++)
            {
                _statuses.TryGetValue(_defs[i].id ?? string.Empty, out var status);
                _synergies[i].Set(_defs[i], status, lineage.id);
            }
            _synergyEmpty.SetActive(_defs.Count == 0);
            _synergyTitle.text = $"{CharacterUI.WithDirection(form.name)} 편성하면  <size=80%><color={UITheme.ColorTag(UITheme.InkSub)}>시너지 {_defs.Count}종</color></size>";
        }

        static void GoToFormation()
        {
            CharacterUI.CloseAllPopups();
            UINav.GoToTab(MainTab.Formation);
        }
    }
}
