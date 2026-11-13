using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 도감: 특성 / 역할 / 조합 탭에 모든 시너지를 진행도와 함께 보여준다.
    /// 위쪽 요약: 지금 켜진 시너지 수, 보유 친구로 만들 수 있는 조합 수, 이번 스테이지 기믹과 강해지는 시너지.
    /// 줄을 누르면 시너지 상세. UINav.OpenSynergy(null)로 첫 화면이 열린다.
    /// 열려 있는 동안 시너지 변화(편성/친구 — SynergySystem.Changed가 둘 다 전해 준다)와 스테이지 기믹 변화를 듣고,
    /// 한 프레임에 여러 번 와도(스테이지 클리어 호감도 등) LateUpdate에서 한 번만 다시 그린다.
    /// </summary>
    public class SynergyCodexPopup : UIPopup
    {
        static readonly SynergyKind[] TabKinds = { SynergyKind.Trait, SynergyKind.Style, SynergyKind.Combo };

        [SerializeField] TMP_Text _summaryText;
        [SerializeField] Image _stageIcon;
        [SerializeField] TMP_Text _stageFallback;
        [Tooltip("기믹 없는 스테이지 아이콘 (Art/Icons/stage_calm)")]
        [SerializeField] Sprite _calmIcon;
        [SerializeField] TMP_Text _stageText;
        [SerializeField] Button[] _tabButtons;
        [SerializeField] Image[] _tabBackgrounds;
        [SerializeField] TMP_Text[] _tabLabels;
        [Tooltip("탭마다 아래 6px 종류 색 밑줄 (고른 탭만 켜진다, M53)")]
        [SerializeField] Image[] _tabAccents = System.Array.Empty<Image>();
        [SerializeField] TMP_Text _tabHintText;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        [SerializeField] SynergyCodexRowView _rowPrefab;
        [SerializeField] GameObject _emptyLabel;

        readonly List<SynergyStatus> _shown = new();
        List<SynergyStatus> _statuses = new();
        UIListPool<SynergyCodexRowView> _rows;
        SynergyKind _kind = SynergyKind.Trait;
        bool _subscribed;
        /// <summary>다시 그려야 함 (변경 신호를 모아 LateUpdate에서 한 번)</summary>
        bool _dirty;
        /// <summary>마지막으로 그린 스테이지 기믹 (같은 기믹 스테이지로 넘어가면 다시 그리지 않는다)</summary>
        StageModifierType _drawnStage;
        /// <summary>요약 띠 '이번 스테이지' 글의 원래 자리 · 크기 (아이콘이 없을 때 왼쪽으로 당겼다가 되돌린다)</summary>
        Vector2 _stageTextPos;
        Vector2 _stageTextSize;
        bool _stageTextKnown;

        /// <summary>기믹 아이콘이 없을 때 '이번 스테이지' 글을 아이콘 자리만큼 왼쪽으로 (554 → 474, 폭 320 → 400)</summary>
        const float StageTextShift = 80f;

        protected override void Awake()
        {
            base.Awake();
            if (_stageText != null)
            {
                _stageTextPos = _stageText.rectTransform.anchoredPosition;
                _stageTextSize = _stageText.rectTransform.sizeDelta;
                _stageTextKnown = true;
            }
            _rows = new UIListPool<SynergyCodexRowView>(_rowPrefab, _content);
            for (int i = 0; i < _tabButtons.Length && i < TabKinds.Length; i++)
            {
                var kind = TabKinds[i];
                _tabButtons[i].onClick.AddListener(() => SelectTab(kind));
            }
        }

        /// <summary>도감 첫 화면 (마지막으로 본 탭)</summary>
        public void ShowFront() => Show(_kind);

        public void Show(SynergyKind kind)
        {
            _kind = kind;
            Open();
            Draw(true);
        }

        public void SelectTab(SynergyKind kind)
        {
            if (_kind == kind && IsOpen) return;
            _kind = kind;
            Draw(true);
        }

        protected override void OnOpen() => Subscribe(true);

        protected override void OnClose()
        {
            Subscribe(false);
            _dirty = false;
        }

        void OnDestroy() => Subscribe(false);

        void Subscribe(bool on)
        {
            if (on == _subscribed || !Game.IsReady) return;
            _subscribed = on;
            if (on)
            {
                Game.Synergy.Changed += MarkDirty;
                Game.Battle.StageChanged += OnStageChanged;
            }
            else
            {
                Game.Synergy.Changed -= MarkDirty;
                Game.Battle.StageChanged -= OnStageChanged;
            }
        }

        void MarkDirty() => _dirty = true;

        /// <summary>전투는 팝업 뒤에서도 계속된다 — 기믹이 바뀐 스테이지로 넘어가면 "이번 스테이지"와 ×1.5 표시를 고친다</summary>
        void OnStageChanged()
        {
            if (SynergyUI.CurrentStageModifier() != _drawnStage) _dirty = true;
        }

        void LateUpdate()
        {
            if (_dirty && IsOpen) Draw(false);
        }

        void Draw(bool resetScroll)
        {
            _dirty = false;
            _statuses = Game.Synergy.GetStatuses(true) ?? new List<SynergyStatus>();
            var stage = SynergyUI.CurrentStageModifier();
            _drawnStage = stage;
            DrawSummary(stage);
            DrawTabs();

            _shown.Clear();
            foreach (var status in _statuses)
            {
                if (status?.Def != null && status.Def.kind == _kind) _shown.Add(status);
            }
            _rows.SetCount(_shown.Count);
            for (int i = 0; i < _shown.Count; i++) _rows[i].Set(_shown[i], stage);
            _emptyLabel.SetActive(_shown.Count == 0);
            _tabHintText.text = TabHint(_kind);
            if (resetScroll && _scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        void DrawSummary(StageModifierType stage)
        {
            int active = 0, combos = 0, makeable = 0;
            foreach (var status in _statuses)
            {
                if (status?.Def == null) continue;
                if (status.IsActive) active++;
                if (status.Def.kind != SynergyKind.Combo) continue;
                combos++;
                if (SynergyUI.OwnedComboFriends(status.Def) >= SynergyUI.RequiredCount(status.Def)) makeable++;
            }
            string gold = UITheme.ColorTag(UITheme.Gold);
            string sub = UITheme.ColorTag(UITheme.SubText);
            // 셋째 줄 = 편성 규칙 전체 (편성 시너지 칸에는 짧게 '규칙 · 같은 계보 최대 2종'만 — M44)
            _summaryText.text = $"지금 켜진 시너지 <color={gold}>{active}</color>개\n" +
                                $"<size=82%><color={sub}>보유 친구로 만들 수 있는 조합</color> <color={gold}>{makeable}</color>/{combos}</size>\n" +
                                $"<size=70%><color={UITheme.ColorTag(UITheme.GoldAccent)}>규칙</color> <color={sub}>· 같은 계보 최대 2종 (지킴이 1 + 잔향이 1)</color></size>";

            SynergyIcons.ApplyStage(_stageIcon, _stageFallback, stage);
            if (stage == StageModifierType.None)
            {
                // 기믹 없는 밤: 고요한 마을 아이콘 (없으면 숨기고 글을 그 자리로 당긴다)
                _stageIcon.sprite = _calmIcon;
                _stageIcon.color = Color.white;
                _stageIcon.enabled = _calmIcon != null;
                if (_stageFallback != null) _stageFallback.gameObject.SetActive(false);
            }
            bool showIcon = stage != StageModifierType.None || _calmIcon != null;
            _stageIcon.gameObject.SetActive(showIcon);
            PlaceStageText(showIcon);
            if (stage == StageModifierType.None)
            {
                _stageText.text = $"이번 스테이지\n<size=82%><color={UITheme.ColorTag(UITheme.SubText)}>특별한 기믹 없음</color></size>";
                return;
            }
            string favored = FavoredNames(stage);
            string color = UITheme.ColorTag(UITheme.StageModifierColor(stage));
            _stageText.text = string.IsNullOrEmpty(favored)
                ? $"이번 스테이지 <color={color}>{UITheme.StageModifierName(stage)}</color>"
                : $"이번 스테이지 <color={color}>{UITheme.StageModifierName(stage)}</color>\n<size=82%><color={UITheme.ColorTag(UITheme.Lamp)}>{favored} 강화!</color></size>";
        }

        /// <summary>아이콘이 있으면 원래 자리, 없으면 아이콘 자리까지 왼쪽으로 늘린다 (기믹이 다시 생기면 되돌린다)</summary>
        void PlaceStageText(bool withIcon)
        {
            if (!_stageTextKnown) return;
            var rect = _stageText.rectTransform;
            var pos = withIcon ? _stageTextPos : _stageTextPos - new Vector2(StageTextShift, 0f);
            var size = withIcon ? _stageTextSize : _stageTextSize + new Vector2(StageTextShift, 0f);
            if (rect.anchoredPosition != pos) rect.anchoredPosition = pos;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }

        /// <summary>이번 스테이지 기믹에서 강해지는 시너지 짧은 이름. 예) "비 · 천"</summary>
        string FavoredNames(StageModifierType stage)
        {
            string result = null;
            foreach (var status in _statuses)
            {
                var def = status?.Def;
                if (def == null || !SynergyUI.IsFavoredNow(def, stage)) continue;
                var name = SynergyUI.ShortName(def);
                result = result == null ? name : $"{result} · {name}";
            }
            return result;
        }

        void DrawTabs()
        {
            for (int i = 0; i < _tabButtons.Length && i < TabKinds.Length; i++)
            {
                var kind = TabKinds[i];
                int total = 0, active = 0;
                foreach (var status in _statuses)
                {
                    if (status?.Def == null || status.Def.kind != kind) continue;
                    total++;
                    if (status.IsActive) active++;
                }
                bool selected = kind == _kind;
                // 쪽 탭 모양 (M53 Page): 고른 탭 = 양피지 + Ink 굵게 + 아래 6px 종류 색 밑줄, 아니면 TabNormal + SubText. 켜진 수는 고른 탭 InkSub · 아니면 Gold
                string countColor = UITheme.ColorTag(selected ? UITheme.InkSub : UITheme.Gold);
                _tabLabels[i].text = $"{UITheme.SynergyKindName(kind)} <size=78%><color={countColor}>{active}</color>/{total}</size>";
                var accent = i < _tabAccents.Length ? _tabAccents[i] : null;
                TabStyle.Apply(_tabBackgrounds[i], _tabLabels[i], selected, TabVariant.Page, accent, SynergyUI.KindColor(kind));
            }
        }

        static string TabHint(SynergyKind kind) => kind switch
        {
            SynergyKind.Trait => "같은 특성(천·금속·빛·소리·밤·비·치장)을 가진 친구를 모아 편성해요.",
            SynergyKind.Style => "방어·힐·지원·돌격·포획·저격 — 같은 역할을 모을수록 강해져요.",
            _ => "특별한 사이의 친구들이 모두 편성되면 조합 효과가 켜져요.",
        };
    }
}
