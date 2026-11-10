using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 재화 칩 하나 (M47 · M59): 쓰는 팝업의 잔액 칩(BalanceChip — SceneParts.PopupBalanceChip)과 상단 바 재화 칩(SceneParts.CurrencyChip)이 쓴다.
    ///   · 보유량: _amount가 있으면 켜질 때 한 번 쓰고 Currency.Changed를 받아 화면에 보이는 숫자가 바뀔 때만 다시 쓴다
    ///     (단추는 매 프레임 늘어나므로 표시 키 비교 — 100만 미만 내림 정수, 그 위 유효 숫자 3자리). 표기는 상단 바와 같은 RewardService.FormatAmount.
    ///     _amount가 비면 숫자를 쓰지 않는다 (상단 바는 TopBarView가 카운트업으로 쓴다).
    ///   · 안내 말풍선: 버튼이 없는 칩은 누르면, 버튼이 있는 칩(상단 바 칩을 버튼으로 만든 경우)은 0.35초 길게 누르면
    ///     InfoTooltipView로 재화 이름 · 쓰는 곳 · 얻는 곳(CurrencySources)을 보여 준다. 길게 눌러 띄우면 뗄 때 버튼 클릭은 막는다.
    ///     끌기(스크롤)로 손가락이 움직이면 취소한다 — IBeginDragHandler는 쓰지 않는다 (부모 ScrollRect가 끌기를 그대로 받게).
    ///     창 포커스를 잃으면 누르기를 취소한다 (M01). 길게 누르기를 재는 동안만 코루틴이 돈다 (Update 없음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurrencyStripView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] CurrencyType _type;
        [Tooltip("보유량 글자 (비면 숫자를 쓰지 않는다 — 상단 바는 TopBarView가 쓴다)")]
        [SerializeField] TMP_Text _amount;
        [Tooltip("누르면(버튼이 있으면 길게 누르면) 재화 설명과 얻는 곳을 안내 말풍선으로 보여 준다")]
        [SerializeField] bool _tooltip = true;
        [Tooltip("말풍선이 가리킬 자리 (비면 이 칩)")]
        [SerializeField] RectTransform _anchor;

        double _shownKey = double.NaN;
        bool _subscribed;
        Selectable _selectable;
        bool _selectableKnown;
        Coroutine _press;
        PointerEventData _pressEvent;
        Vector2 _pressPosition;

        public CurrencyType Type => _type;

        /// <summary>재화를 바꾼다 (같은 칩을 다른 재화로 다시 쓸 때)</summary>
        public void SetType(CurrencyType type)
        {
            if (_type == type) return;
            _type = type;
            _shownKey = double.NaN;
            Refresh();
        }

        void OnEnable()
        {
            if (_amount != null && Game.IsReady && Game.Currency != null && !_subscribed)
            {
                Game.Currency.Changed += OnCurrencyChanged;
                _subscribed = true;
            }
            _shownKey = double.NaN;
            Refresh();
        }

        void OnDisable()
        {
            if (_subscribed)
            {
                if (Game.Currency != null) Game.Currency.Changed -= OnCurrencyChanged;
                _subscribed = false;
            }
            CancelPress();
        }

        void OnCurrencyChanged(CurrencyType type, double value)
        {
            if (type == _type) Show(value);
        }

        /// <summary>보유량을 다시 읽어 쓴다 (숫자 칸이 없거나 게임이 준비 안 됐으면 아무것도 안 함)</summary>
        public void Refresh()
        {
            if (_amount == null || !Game.IsReady || Game.Currency == null) return;
            Show(Game.Currency.Get(_type));
        }

        void Show(double amount)
        {
            double key = DisplayKey(amount);
            if (key.Equals(_shownKey)) return;
            _shownKey = key;
            _amount.text = RewardService.FormatAmount(_type, amount);
        }

        /// <summary>화면에 보이는 숫자가 같으면 같은 값 (100만 미만 내림 정수 · 그 위 유효 숫자 3자리 — FormatCurrency와 같은 경계)</summary>
        static double DisplayKey(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount)) return amount;
            double abs = Math.Abs(amount);
            if (abs < NumberFormatter.CurrencyCommaLimit) return Math.Floor(amount);
            double step = Math.Pow(10, Math.Floor(Math.Log10(abs)) - 2);
            return Math.Floor(amount / step + 1e-9) * step;
        }

        // ───────────── 안내 말풍선 ─────────────

        /// <summary>같은 오브젝트에 누를 수 있는 버튼이 있는지 (있으면 안내는 길게 누르기로)</summary>
        bool HasButton
        {
            get
            {
                if (!_selectableKnown)
                {
                    _selectable = GetComponent<Selectable>();
                    _selectableKnown = true;
                }
                return _selectable != null && _selectable.enabled;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_tooltip || !HasButton || !isActiveAndEnabled) return;
            CancelPress();
            _pressEvent = eventData;
            _pressPosition = eventData.position;
            _press = StartCoroutine(WatchLongPress());
        }

        public void OnPointerUp(PointerEventData eventData) => CancelPress();

        public void OnPointerExit(PointerEventData eventData) => CancelPress();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_tooltip && !HasButton) ShowTooltip();
        }

        /// <summary>창 포커스를 잃으면 길게 누르기를 취소한다 (M01 — 게임은 멈추지 않는다)</summary>
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
            // 뗄 때 버튼 클릭이 나가지 않게 (길게 누르기는 안내만)
            if (_pressEvent != null) _pressEvent.eligibleForClick = false;
            _press = null;
            _pressEvent = null;
            ShowTooltip();
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

        void ShowTooltip()
        {
            var anchor = _anchor != null ? _anchor : transform as RectTransform;
            InfoTooltipView.Show(anchor, CurrencySources.Title(_type), CurrencySources.Body(_type), CurrencySources.SourceLine(_type));
        }
    }
}
