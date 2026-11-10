using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 한 줄 (크림 카드 안): 아이콘 / 종류 칩(특성·역할·조합) / 이름 / 단계·조합 계보 / 현재 편성 상태.
    /// 누르면 시너지 도감(UINav.OpenSynergy)으로 이동한다.
    /// 0.35초 길게 누르면(M59 · S §3.6) 종류 칩에 '특성 / 역할 / 조합 시너지' 안내 말풍선을 띄우고, 그때는 뗄 때 도감으로 가지 않는다.
    /// 줄 어디를 길게 눌러도 된다 (종류 칩 30px은 누르는 자리 88보다 작다). 끌면(스크롤) · 손가락이 움직이면 취소. 글은 말풍선을 띄울 때만 만든다.
    /// </summary>
    public class LineageSynergyRowView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] Button _button;
        [SerializeField] Image _background;
        [SerializeField] Image _iconBackground;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _iconLetter;
        [SerializeField] CharacterChip _kindChip;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _detailText;
        [SerializeField] CharacterChip _stateChip;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] CharacterIconLibrary _icons;

        static readonly Color RowColor = new(0.91f, 0.84f, 0.71f, 0.55f);
        static readonly Color ActiveRowColor = new(0.91f, 0.75f, 0.42f, 0.34f);

        readonly StringBuilder _sb = new();
        string _synergyId;
        Coroutine _press;
        PointerEventData _pressEvent;
        Vector2 _pressPosition;

        void Awake() => _button.onClick.AddListener(OnClick);

        void OnDisable() => CancelPress();

        /// <param name="status">현재 편성 기준 상태 (없으면 null)</param>
        /// <param name="focusLineageId">조합 계보 이름 중 굵게 표시할 계보</param>
        public void Set(SynergyDef def, SynergyStatus status, string focusLineageId)
        {
            _synergyId = def.id;
            var color = KindColor(def);
            bool active = status != null && status.IsActive;

            _background.color = active ? ActiveRowColor : RowColor;
            var icon = _icons != null ? _icons.Synergy(def) : null;
            bool hasArt = icon != null;
            // 받침(ui_plate_round)은 흰색 → 종류 색으로 조금만 물들인다 (아트를 탁하게 만들지 않게)
            _iconBackground.color = CharacterUI.WithAlpha(Color.Lerp(Color.white, color, hasArt ? 0.35f : 0.45f), hasArt ? 0.9f : 1f);
            _icon.sprite = icon;
            _icon.enabled = hasArt;
            _iconLetter.gameObject.SetActive(!hasArt);
            _iconLetter.text = FallbackLetter(def);

            _kindChip.SetSynergyKind(def, color);
            _nameText.text = string.IsNullOrEmpty(def.name) ? def.id : def.name;
            _detailText.text = BuildDetail(def, status, focusLineageId);
            RefreshState(def, status, active);
        }

        void RefreshState(SynergyDef def, SynergyStatus status, bool active)
        {
            _stateChip.gameObject.SetActive(active);
            if (status == null)
            {
                _progressText.text = string.Empty;
                return;
            }
            if (active)
            {
                _stateChip.Set("발동 중", UITheme.GoldAccent, UITheme.Ink);
                _progressText.text = def.tiers != null && def.tiers.Count > 1 ? $"{status.TierIndex + 1}단계" : string.Empty;
                _progressText.color = CharacterUI.InkPositive;
                return;
            }
            int need = def.kind == SynergyKind.Combo
                ? def.lineageIds?.Count ?? 0
                : status.NextTier != null ? status.NextTier.count : FirstCount(def);
            _progressText.text = need > 0 ? $"편성 {status.Count}/{need}" : string.Empty;
            _progressText.color = UITheme.InkSub;
        }

        /// <summary>특성/역할: "2 · 3 · 4명  지금 단계 설명", 조합: "짝짝이 + 똑단이  설명"</summary>
        string BuildDetail(SynergyDef def, SynergyStatus status, string focusLineageId)
        {
            _sb.Clear();
            string ink = UITheme.ColorTag(UITheme.Ink);
            if (def.kind == SynergyKind.Combo) AppendMembers(def, focusLineageId, ink);
            else AppendCounts(def, ink);

            string description = TierDescription(def, status);
            if (description.Length > 0)
            {
                if (_sb.Length > 0) _sb.Append("   ");
                _sb.Append(description);
            }
            return _sb.ToString();
        }

        void AppendCounts(SynergyDef def, string ink)
        {
            if (def.tiers == null || def.tiers.Count == 0) return;
            _sb.Append("<color=").Append(ink).Append("><b>");
            for (int i = 0; i < def.tiers.Count; i++)
            {
                if (i > 0) _sb.Append(" · ");
                _sb.Append(def.tiers[i].count);
            }
            _sb.Append("명</b></color>");
        }

        void AppendMembers(SynergyDef def, string focusLineageId, string ink)
        {
            if (def.lineageIds == null) return;
            var db = Game.Characters.Db;
            string sub = UITheme.ColorTag(CharacterUI.WithAlpha(UITheme.InkSub, 0.8f));
            for (int i = 0; i < def.lineageIds.Count; i++)
            {
                string id = def.lineageIds[i];
                var lineage = db.GetLineage(id);
                if (i > 0) _sb.Append("<color=").Append(sub).Append("> + </color>");
                bool owned = Game.Characters.IsOwned(id);
                _sb.Append("<color=").Append(owned ? ink : sub).Append('>');
                if (id == focusLineageId) _sb.Append("<b>");
                _sb.Append(lineage != null ? lineage.name : id);
                if (id == focusLineageId) _sb.Append("</b>");
                _sb.Append("</color>");
            }
        }

        /// <summary>켜진 단계 설명, 꺼져 있으면 다음(첫) 단계 설명, 없으면 시너지 설명</summary>
        static string TierDescription(SynergyDef def, SynergyStatus status)
        {
            SynergyTier tier = null;
            if (status != null) tier = status.ActiveTier ?? status.NextTier;
            if (tier == null && def.tiers != null && def.tiers.Count > 0) tier = def.tiers[0];
            if (tier != null && !string.IsNullOrEmpty(tier.description)) return tier.description;
            return def.description ?? string.Empty;
        }

        static int FirstCount(SynergyDef def) => def.tiers != null && def.tiers.Count > 0 ? def.tiers[0].count : 0;

        static Color KindColor(SynergyDef def) => def.kind switch
        {
            SynergyKind.Trait => UITheme.TraitColor(def.trait),
            SynergyKind.Style => UITheme.StyleColor(def.style),
            _ => UITheme.GoldAccent,
        };

        static string FallbackLetter(SynergyDef def)
        {
            string text = def.kind switch
            {
                SynergyKind.Trait => UITheme.TraitName(def.trait),
                SynergyKind.Style => UITheme.StyleName(def.style),
                _ => def.name,
            };
            return string.IsNullOrEmpty(text) ? "?" : text.Substring(0, 1);
        }

        void OnClick()
        {
            if (!string.IsNullOrEmpty(_synergyId)) UINav.OpenSynergy(_synergyId);
        }

        // ───────────── 길게 누르기 → 종류 칩 말풍선 (M59) ─────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !isActiveAndEnabled) return;
            if (_kindChip == null || string.IsNullOrEmpty(_synergyId) || !_button.IsInteractable()) return;
            CancelPress();
            _pressEvent = eventData;
            _pressPosition = eventData.position;
            _press = StartCoroutine(WatchLongPress());
        }

        public void OnPointerUp(PointerEventData eventData) => CancelPress();

        public void OnPointerExit(PointerEventData eventData) => CancelPress();

        /// <summary>창 포커스를 잃으면 길게 누르기를 취소한다</summary>
        void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelPress();
        }

        IEnumerator WatchLongPress()
        {
            float held = 0f;
            float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10f;
            float thresholdSqr = threshold * threshold;
            while (held < UIMotion.TooltipLongPress)
            {
                yield return null;
                held += Time.unscaledDeltaTime;
                var ev = _pressEvent;
                if (ev == null || ev.dragging || (ev.position - _pressPosition).sqrMagnitude > thresholdSqr)
                {
                    _press = null;
                    _pressEvent = null;
                    yield break;
                }
            }
            var press = _pressEvent;
            _press = null;
            _pressEvent = null;
            // 말풍선이 떴으면 뗄 때 도감으로 가지 않게 (길게 누르기는 안내만). 못 띄웠으면 누름을 그대로 둔다
            if (_kindChip.ShowTooltip() && press != null) press.eligibleForClick = false;
        }

        void CancelPress()
        {
            if (_press != null)
            {
                StopCoroutine(_press);
                _press = null;
            }
            _pressEvent = null;
        }
    }
}
