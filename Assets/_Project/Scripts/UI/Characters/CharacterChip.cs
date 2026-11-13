using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 아이콘 + 글자 칩 (역할 / 성향 / 특성 / 스킬 발동 시점 / 시너지 종류).
    /// 가로 레이아웃으로 글자 길이에 맞춰 폭이 정해진다. 아이콘 아트가 없으면 아이콘 칸을 숨긴다.
    /// 바탕은 흰색 틴트용 알약(ui_pill_white_9s)에 칩 색을 입히고, 초상화/밤 패널 위(glass)에서는
    /// 밤색 알약 그림(_glassSprite, ui_pill_night_9s)으로 바꿔 흰색 그대로 그린다 (어두운 색을 곱하면 탁해진다).
    /// <para>
    /// 안내 말풍선 (M59): 친구 상세 · 형태 정보 머리 칩만 EnableTooltip()으로 켠다 — 누르면 InfoTooltipView로 특성(같은 특성 시너지 인원 · 첫 단계 효과),
    /// 성향(전투 방식), 역할(지킴이 · 잔향이 설명)을 보여 준다. 목록 카드의 칩은 켜지 않아 카드 누름이 그대로 간다 (바탕이 누름을 받지 않는다).
    /// 시너지 줄의 종류 칩(SetSynergyKind)은 줄 자체가 버튼이라 켜지 않고, 줄(LineageSynergyRowView)이 0.35초 길게 누르면 ShowTooltip()을 부른다.
    /// 끌면(스크롤) 누름이 아니므로 뜨지 않는다. 글은 누를 때만 만든다.
    /// </para>
    /// </summary>
    public class CharacterChip : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>칩이 무엇을 보여 주는지 (말풍선 내용)</summary>
        enum ChipKind : byte
        {
            None = 0,
            Trait = 1,
            Style = 2,
            Role = 3,
            /// <summary>시너지 종류 (특성 · 역할 · 조합)</summary>
            Synergy = 4,
        }

        [SerializeField] Image _background;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _label;
        [SerializeField] CharacterIconLibrary _icons;
        /// <summary>glass 칩 바탕 그림 (없으면 기본 바탕에 반투명 밤색을 입힌다)</summary>
        [SerializeField] Sprite _glassSprite;

        /// <summary>밤 패널/초상화 위의 반투명 칩 바탕 (glass 그림이 없을 때)</summary>
        static readonly Color GlassBackground = new(0.07f, 0.05f, 0.15f, 0.74f);
        /// <summary>glass 그림의 투명도 (뒤 초상화가 살짝 비친다)</summary>
        const float GlassArtAlpha = 0.9f;

        Sprite _tintSprite;
        bool _tintSpriteCached;
        bool _tooltip;
        ChipKind _kind;
        int _value;
        SynergyDef _synergy;

        public TMP_Text Label => _label;

        /// <summary>그냥 글자 칩 (말풍선 없음)</summary>
        public void Set(string text, Color background, Color textColor, Sprite icon = null)
        {
            Apply(text, background, textColor, icon, false);
            _kind = ChipKind.None;
            _synergy = null;
        }

        /// <summary>시너지 종류 칩 '특성 / 역할 / 조합' (잉크 글자). 말풍선은 ShowTooltip()으로 띄운다</summary>
        public void SetSynergyKind(SynergyDef def, Color background)
        {
            Apply(def != null ? UITheme.SynergyKindName(def.kind) : string.Empty, background, UITheme.Ink, null, false);
            _kind = def != null ? ChipKind.Synergy : ChipKind.None;
            _synergy = def;
        }

        /// <summary>누르면 설명 말풍선 (M59 — 친구 상세 · 형태 정보 머리 칩만). 바탕이 누름을 받게 한다</summary>
        public void EnableTooltip()
        {
            _tooltip = true;
            if (_background != null) _background.raycastTarget = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_tooltip || eventData.button != PointerEventData.InputButton.Left) return;
            ShowTooltip();
        }

        /// <summary>이 칩의 설명 말풍선을 띄운다 (글은 이때만 만든다). 보여 줄 것이 없거나 말풍선이 씬에 없으면 false</summary>
        public bool ShowTooltip()
        {
            var anchor = transform as RectTransform;
            switch (_kind)
            {
                case ChipKind.Trait:
                {
                    var trait = (TraitType)_value;
                    var def = FindSynergy(SynergyKind.Trait, trait, default);
                    return InfoTooltipView.Show(anchor, $"{UITheme.TraitName(trait)} 특성", CountLine("특성", def), FirstTier(def));
                }
                case ChipKind.Style:
                {
                    var style = (CombatStyle)_value;
                    var def = FindSynergy(SynergyKind.Style, default, style);
                    return InfoTooltipView.Show(anchor, $"{UITheme.StyleName(style)} 성향", CharacterUI.StyleHint(style),
                        def != null ? CountLine("성향", def) : null);
                }
                case ChipKind.Role:
                {
                    var role = (CharacterRole)_value;
                    return InfoTooltipView.Show(anchor, UITheme.RoleName(role), CharacterUI.RoleHint(role));
                }
                case ChipKind.Synergy:
                {
                    var def = _synergy;
                    if (def == null) return false;
                    return InfoTooltipView.Show(anchor, $"{UITheme.SynergyKindName(def.kind)} 시너지", SynergyKindLine(def), FirstTier(def) ?? NullIfEmpty(def.description));
                }
            }
            return false;
        }

        /// <summary>시너지 종류 설명: 특성 · 역할은 인원 단계, 조합은 정해진 친구 수</summary>
        static string SynergyKindLine(SynergyDef def)
        {
            switch (def.kind)
            {
                case SynergyKind.Trait:
                    return CountLine("특성", def);
                case SynergyKind.Style:
                    return CountLine("성향", def);
                default:
                {
                    int members = def.lineageIds?.Count ?? 0;
                    return members > 0
                        ? $"정해진 친구 {members}명을 모두 함께 편성하면 시너지가 켜져요"
                        : "정해진 친구들을 모두 함께 편성하면 시너지가 켜져요";
                }
            }
        }

        static string NullIfEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;

        /// <summary>그 특성 · 성향 시너지 정의 (없으면 null). 누를 때만 찾는다</summary>
        static SynergyDef FindSynergy(SynergyKind kind, TraitType trait, CombatStyle style)
        {
            var db = Game.IsReady && Game.Synergy != null ? Game.Synergy.Db : null;
            if (db == null || db.synergies == null) return null;
            foreach (var def in db.synergies)
            {
                if (def == null || def.kind != kind) continue;
                if (kind == SynergyKind.Trait ? def.trait == trait : def.style == style) return def;
            }
            return null;
        }

        /// <summary>'같은 특성 친구를 2 · 3 · 4명 함께 편성하면 시너지가 켜져요' (단계가 없으면 인원 없이)</summary>
        static string CountLine(string what, SynergyDef def)
        {
            if (def?.tiers == null || def.tiers.Count == 0) return $"같은 {what} 친구를 함께 편성하면 시너지가 켜져요";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < def.tiers.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(def.tiers[i].count);
            }
            return $"같은 {what} 친구를 {sb}명 함께 편성하면 시너지가 켜져요";
        }

        /// <summary>첫 단계 효과 (없으면 null)</summary>
        static string FirstTier(SynergyDef def)
        {
            if (def?.tiers == null || def.tiers.Count == 0) return null;
            var tier = def.tiers[0];
            return string.IsNullOrEmpty(tier.description) ? null : $"{tier.count}명: {tier.description}";
        }

        void Apply(string text, Color background, Color textColor, Sprite icon, bool glass)
        {
            _label.text = text;
            _label.color = textColor;
            SetBackground(background, glass);
            if (_icon == null) return;
            _icon.sprite = icon;
            _icon.color = Color.white;
            _icon.gameObject.SetActive(icon != null);
        }

        /// <summary>특성 칩. glass = 초상화/밤 패널 위 반투명 바탕 + 특성 색 글자, 아니면 특성 색 바탕 + 잉크 글자</summary>
        public void SetTrait(TraitType trait, bool glass)
        {
            var color = UITheme.TraitColor(trait);
            var icon = _icons != null ? _icons.Trait(trait) : null;
            if (glass) Apply(UITheme.TraitName(trait), GlassBackground, color, icon, true);
            else Apply(UITheme.TraitName(trait), color, UITheme.Ink, icon, false);
            _kind = ChipKind.Trait;
            _value = (int)trait;
        }

        /// <summary>바탕 칠하기: glass면 밤색 알약 그림(흰색), 아니면 기본(흰색 틴트용) 그림에 칩 색</summary>
        void SetBackground(Color background, bool glass)
        {
            if (_background == null) return;
            if (!_tintSpriteCached)
            {
                _tintSprite = _background.sprite;
                _tintSpriteCached = true;
            }
            bool glassArt = glass && _glassSprite != null;
            _background.sprite = glassArt ? _glassSprite : _tintSprite;
            _background.color = glassArt ? CharacterUI.WithAlpha(Color.white, GlassArtAlpha) : background;
        }

        /// <summary>성향 칩 (방어 / 돌격 / 포획 / 저격 / 지원 / 힐)</summary>
        public void SetStyle(CombatStyle style)
        {
            Apply(UITheme.StyleName(style), UITheme.StyleColor(style), UITheme.Ink, _icons != null ? _icons.Style(style) : null, false);
            _kind = ChipKind.Style;
            _value = (int)style;
        }

        /// <summary>역할 칩 (지킴이 / 잔향이). suffix 예: " 추천"</summary>
        public void SetRole(CharacterRole role, string suffix = null)
        {
            Apply(UITheme.RoleName(role) + suffix, CharacterUI.RoleColor(role), UITheme.Ink, _icons != null ? _icons.Role(role) : null, false);
            _kind = ChipKind.Role;
            _value = (int)role;
        }

        /// <summary>칩 전체 투명도 (미보유 계보를 흐리게)</summary>
        public void SetAlpha(float alpha)
        {
            if (_background != null) _background.color = CharacterUI.WithAlpha(_background.color, _background.color.a * alpha);
            _label.alpha = alpha;
            if (_icon != null) _icon.color = CharacterUI.WithAlpha(Color.white, alpha);
        }
    }
}
