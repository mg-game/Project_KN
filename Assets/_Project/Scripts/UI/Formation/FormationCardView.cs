using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 편성 화면 캐릭터 목록 카드 (152x196, M45). 위로 끌면 배치(드래그 앤 드롭), 옆으로 끌면 목록 스크롤, 탭하면 선택.
    ///   · 오른쪽 위 'Lv.N' 알약, 그 아래 역할 점('지'/'잔' — 편성 밖일 때만, 추천 역할) 또는 '원정 중' 알약.
    ///   · 상태 띠: "지킴이 배치 중" / "잔향이 배치 중" (편성됐을 때만 — 코치가 편성 안 된 카드를 이 띠로 고른다).
    ///   · 원정 중인 친구도 흐리게 하지 않고 끌어 놓을 수 있다 (M05 — 원정 중에도 함께 싸운다). '원정 중'은 안내 알약일 뿐.
    ///   · 왼쪽 위 특성 칩(천/밤 등): 그 특성 시너지가 켜져 있으면 금빛 테두리. 아래 왼쪽 성향 칩 · 오른쪽 형태 전투력 (PowerSystem.GetFormPower, 아이콘과 함께).
    ///   · 다른 화면이 데려오면(FormationPanel.PulseRosterCard) 금빛 테 + 1.06배로 잠깐 알려 준다.
    /// 창 포커스를 잃으면 끌던 카드를 내려놓는다 (M01 — 게임은 멈추지 않는다).
    /// </summary>
    public class FormationCardView : MonoBehaviour, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>데려왔을 때 몸체를 키우는 배율 · 시간</summary>
        const float PulseScale = 1.06f;
        const float PulseIn = 0.18f;
        const float PulseOut = 0.15f;

        [Tooltip("선택 시 살짝 떠오르는 몸체 (루트는 레이아웃이 위치를 잡는다)")]
        [SerializeField] RectTransform _body;
        [Tooltip("금 테두리 덮개 (card_frame_9s) — 등급 색을 60%만 섞는다")]
        [SerializeField] Image _frame;
        [Tooltip("초상화 뒤 수선점 배경 (portrait_bg) — 계보 색을 옅게 (없어도 됨)")]
        [SerializeField] Image _portraitBg;
        [SerializeField] FormPortraitView _portrait;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _starsText;
        [SerializeField] Image _styleChip;
        [SerializeField] TMP_Text _styleText;
        [SerializeField] GameObject _stateBand;
        [SerializeField] Image _stateBandImage;
        [SerializeField] TMP_Text _stateText;
        [SerializeField] GameObject _selectedGlow;
        [Tooltip("계보 특성 칩 (계보당 2개)")]
        [SerializeField] TraitChipView[] _traitChips;
        [Tooltip("오른쪽 위 'Lv.N' 알약의 글자")]
        [SerializeField] TMP_Text _levelText;
        [Tooltip("편성 밖일 때 추천 역할 점 ('지' 지킴이 / '잔' 잔향이)")]
        [SerializeField] Image _roleDot;
        [SerializeField] TMP_Text _roleDotText;
        [Tooltip("원정 중 안내 알약 (끌어 놓기는 그대로 된다)")]
        [SerializeField] GameObject _expeditionPill;
        [Tooltip("형태 전투력 (아래 오른쪽, 아이콘과 함께)")]
        [SerializeField] TMP_Text _powerText;
        [Tooltip("데려왔을 때 잠깐 켜는 금빛 테")]
        [SerializeField] GameObject _pulseRing;

        FormationPanel _panel;
        ScrollRect _scroll;
        bool _forwardToScroll;
        bool _draggingCard;
        Coroutine _pulse;
        long _shownPower = -1;

        public string FormId { get; private set; }
        public string LineageId { get; private set; }
        public bool IsDispatched { get; private set; }
        public RectTransform Body => _body;

        public void Init(FormationPanel panel, ScrollRect scroll)
        {
            _panel = panel;
            _scroll = scroll;
        }

        public void Set(CharacterFormDef form, LineageDef lineage)
        {
            FormId = form.id;
            LineageId = lineage != null ? lineage.id : null;

            var grade = lineage != null ? lineage.grade : ItemGrade.Common;
            _frame.color = Color.Lerp(Color.white, UITheme.GradeColor(grade), 0.6f);
            if (_portraitBg != null) _portraitBg.color = Color.Lerp(Color.white, lineage != null ? lineage.mainColor : UITheme.Lavender, 0.3f);
            _portrait.SetForm(form.id);
            _nameText.text = Game.Characters.GetDisplayName(form.id);
            _starsText.text = UITheme.Stars(grade);
            // 양피지 위 별 (M83): 등급 색을 Ink 쪽으로 35%
            _starsText.color = Color.Lerp(UITheme.GradeColor(grade), UITheme.Ink, 0.35f);

            var stats = Game.Characters.GetStats(form.id);
            _styleChip.color = UITheme.StyleColor(stats.style);
            _styleText.text = UITheme.StyleName(stats.style);
            if (_roleDot != null)
            {
                bool guardian = form.recommendedRole == CharacterRole.Guardian;
                _roleDot.color = guardian ? UITheme.Guardian : UITheme.EchoGlow;
                if (_roleDotText != null) _roleDotText.text = guardian ? "지" : "잔";
            }
            if (_levelText != null) _levelText.text = lineage != null ? $"Lv.{Game.Characters.GetLevel(lineage.id)}" : string.Empty;

            var traits = lineage != null ? lineage.traits : null;
            for (int i = 0; i < _traitChips.Length; i++)
            {
                bool shown = traits != null && i < traits.Count;
                _traitChips[i].gameObject.SetActive(shown);
                if (shown) _traitChips[i].Set(traits[i]);
            }

            _shownPower = -1;
            RefreshPower();
            RefreshState();
        }

        /// <summary>켜진 특성 시너지 (비트 = TraitType)로 특성 칩을 밝힌다</summary>
        public void SetActiveTraits(int traitMask)
        {
            foreach (var chip in _traitChips)
            {
                if (chip.gameObject.activeSelf) chip.SetLit((traitMask & (1 << (int)chip.Trait)) != 0);
            }
        }

        /// <summary>형태 전투력 (값이 바뀔 때만 글을 다시 쓴다 — Power.Changed마다 불린다)</summary>
        public void RefreshPower()
        {
            if (_powerText == null || string.IsNullOrEmpty(FormId) || Game.Power == null) return;
            long power = Game.Power.GetFormPower(FormId);
            if (power == _shownPower) return;
            _shownPower = power;
            _powerText.text = PowerSystem.Format(power);
        }

        /// <summary>배치 상태 띠 · 원정 알약 · 역할 점만 다시 그린다</summary>
        public void RefreshState()
        {
            IsDispatched = LineageId != null && Game.Formation.IsDispatched(LineageId);
            bool placed = Game.Formation.IsPlaced(FormId, out var role, out _);

            _stateBand.SetActive(placed);
            if (placed)
            {
                _stateText.text = $"{UITheme.RoleName(role)} 배치 중";
                _stateBandImage.color = role == CharacterRole.Guardian ? UITheme.Guardian : UITheme.EchoGlow;
                _stateText.color = UITheme.Ink;
            }
            // 원정 중이면 역할 점 자리에 '원정 중' 알약 (Lv 알약 아래). 편성됐으면 역할 점을 숨긴다 (띠가 역할을 말해 준다)
            if (_expeditionPill != null) _expeditionPill.SetActive(IsDispatched);
            if (_roleDot != null) _roleDot.gameObject.SetActive(!placed && !IsDispatched);
        }

        public void SetSelected(bool selected)
        {
            _selectedGlow.SetActive(selected);
            _body.anchoredPosition = new Vector2(0f, selected ? 8f : 0f);
        }

        /// <summary>데려왔을 때: seconds 동안 금빛 테 + 1 → 1.06배 (움직임 줄이기면 테만)</summary>
        public void Pulse(float seconds)
        {
            if (!isActiveAndEnabled) return;
            MenuFx.Restart(this, ref _pulse, PulseRoutine(seconds));
        }

        IEnumerator PulseRoutine(float seconds)
        {
            if (_pulseRing != null) _pulseRing.SetActive(true);
            bool animate = !UIMotion.Reduced;
            float elapsed = 0f;
            if (animate)
            {
                for (; elapsed < PulseIn; elapsed += Time.unscaledDeltaTime)
                {
                    _body.localScale = Vector3.one * Mathf.LerpUnclamped(1f, PulseScale, UIMotion.OutBack(elapsed / PulseIn, UIMotion.ReleaseOvershoot));
                    yield return null;
                }
                _body.localScale = Vector3.one * PulseScale;
            }
            for (; elapsed < seconds - (animate ? PulseOut : 0f); elapsed += Time.unscaledDeltaTime) yield return null;
            if (animate)
            {
                for (float t = 0f; t < PulseOut; t += Time.unscaledDeltaTime)
                {
                    _body.localScale = Vector3.one * Mathf.Lerp(PulseScale, 1f, UIMotion.OutCubic(t / PulseOut));
                    yield return null;
                }
            }
            _body.localScale = Vector3.one;
            if (_pulseRing != null) _pulseRing.SetActive(false);
            _pulse = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_panel != null) _panel.OnCardClicked(this);
        }

        // ── 드래그: 세로로 끌면 카드 배치, 가로로 끌면 목록 스크롤 ──

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (_scroll != null) _scroll.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var move = eventData.position - eventData.pressPosition;
            bool vertical = Mathf.Abs(move.y) > Mathf.Abs(move.x) * 0.9f;
            _draggingCard = vertical && _panel != null && _panel.BeginCardDrag(this, eventData);
            _forwardToScroll = !_draggingCard && _scroll != null;
            if (_forwardToScroll) _scroll.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_draggingCard) _panel.DragCard(eventData);
            else if (_forwardToScroll) _scroll.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_draggingCard) _panel.EndCardDrag(eventData);
            else if (_forwardToScroll) _scroll.OnEndDrag(eventData);
            _draggingCard = false;
            _forwardToScroll = false;
        }

        /// <summary>창 포커스를 잃으면(알트탭 등) 끌던 카드를 내려놓는다 — 떼기를 놓쳐 고스트가 남지 않게 (M01, 게임은 멈추지 않는다)</summary>
        void OnApplicationFocus(bool focused)
        {
            if (focused || !_draggingCard) return;
            if (_panel != null) _panel.CancelCardDrag();
            _draggingCard = false;
        }

        void OnDisable()
        {
            // 드래그 중 탭 전환 등으로 꺼지면 고스트를 정리한다
            if (_draggingCard && _panel != null) _panel.CancelCardDrag();
            _draggingCard = false;
            _forwardToScroll = false;
            _pulse = null;
            if (_body != null) _body.localScale = Vector3.one;
            if (_pulseRing != null) _pulseRing.SetActive(false);
        }
    }
}
