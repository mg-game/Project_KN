using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 비용 버튼 세 상태 (M07). 색만 바꾸던 비용 글자를 넉넉함 / 부족함 / 쓸 수 없음 세 모양으로 나눈다. 빌더는 UIKit.CostBtn으로 붙인다.
    ///   · Affordable (넉넉함): 원래 그림. 비용 글자 = 어두운 버튼 Text, 금 버튼 · 양피지 Ink. 알약 숨김, 아이콘 그대로.
    ///   · Short (부족함): 그림은 그대로 두고 색만 (0.78, 0.78, 0.82)배로 살짝 어둡게, 이름 알파 0.9,
    ///     비용 뒤에 어두운 CostPill(양피지는 알약 없음) + 비용 글자 UITheme.CostShort, 아이콘 알파 0.5. 누를 수 있다 —
    ///     부르는 쪽이 눌림을 처리하다 모자라면 Deny()를 부르고 부족 안내로 보낸다 (단추 = 토스트, 별조각 · 이벤트 재화 = ShortfallPopup, §3.8).
    ///   · Unavailable (쓸 수 없음: 최대 레벨 · 품절 · 보유 중 · 받을 보상 없음): interactable=false, 비용 줄 숨김, 이름 = unavailableLabel.
    ///     그림 버튼(스프라이트 교체)은 버튼의 비활성 그림(ui_button_disabled_9s)이 그대로 보이고 이름은 Text,
    ///     단색 버튼은 바탕을 _unavailableSprite + UITheme.Disabled로, 이름은 DisabledText로 칠한다 (UIKit.CostBtn이 disabledColor를 흰색으로 둔다).
    ///     Unavailable에서 끈 것(누르기 · 비용 줄 · 이름)만 다른 상태로 갈 때 되돌린다 — 부르는 쪽이 따로 끈 것은 건드리지 않는다.
    /// 상태는 부르는 쪽의 기존 Refresh에서만 다시 정한다 (매 프레임 확인하지 않는다). 같은 상태로 다시 부르면 아무것도 하지 않는다.
    /// 넉넉함에서 다른 상태로 갈 때 그때 모양(그림 · 이름 색 · 아이콘 색)을 '보통'으로 기억했다가 돌아올 때 되돌린다.
    /// 막힘 흔들기(Deny)는 이름 · 비용 줄에만 x 오프셋을 더한다 (버튼 루트는 PressScale · 레이아웃 몫). 흔드는 동안만 Update가 켜진다.
    /// 쓰기 성공은 부르는 쪽이 재화를 실제로 쓴 뒤 Spent()를 부른다 — Spend 소리 (AUDIO-SPEC §8.1, 길게 눌러 반복하는 동안은 처음 한 번).
    /// 성공 소리를 따로 내는 곳(상점 구매 = Purchase)은 부르지 않는다 — 한 번 누름에 '샀다' 소리가 둘 겹친다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CostButtonView : MonoBehaviour
    {
        public enum State
        {
            Affordable = 0,
            Short = 1,
            Unavailable = 2,
        }

        /// <summary>부족할 때 그림 색 배율 (Lerp(흰색, 어두운 색, 0.35) 안 — 아트는 살짝만 어둡게)</summary>
        static readonly Color ShortSkinTint = new(0.78f, 0.78f, 0.82f, 1f);
        const float ShortLabelAlpha = 0.9f;
        const float ShortIconAlpha = 0.5f;
        /// <summary>쓰는 소리 사이 최소 쉼 (초) — 길게 눌러 반복(0.17 → 0.05초)하는 동안 '착착착'이 쌓이지 않게 처음 한 번만</summary>
        const float SpendQuiet = 0.25f;

        [SerializeField] Button _button;
        [Tooltip("버튼 바탕 그림 (보통 targetGraphic)")]
        [SerializeField] Image _skin;
        [Tooltip("버튼 이름 (강화 x1 등)")]
        [SerializeField] TMP_Text _label;
        [SerializeField] TMP_Text _costText;
        [Tooltip("비용 줄의 재화 아이콘 (없어도 됨)")]
        [SerializeField] Image _costIcon;
        [Tooltip("부족할 때 비용 뒤에 까는 어두운 알약 (없어도 됨)")]
        [SerializeField] Image _costPill;
        [Tooltip("아이콘 + 비용 줄 (쓸 수 없음일 때 숨기고, 막힘 흔들기에 같이 움직인다)")]
        [SerializeField] RectTransform _costRow;
        [SerializeField] CostSurface _surface;
        [Tooltip("쓸 수 없음 바탕 (단색 버튼일 때만 — 스프라이트 교체 버튼은 버튼의 비활성 그림을 쓴다)")]
        [SerializeField] Sprite _unavailableSprite;

        /// <summary>Deny()가 불렸다 (흔들기 · 소리 · 진동 뒤). 부족 안내를 한곳에서 띄우고 싶을 때 구독한다</summary>
        public event Action Denied;

        State _state;
        bool _applied;
        bool _initialized;

        Sprite _normalSprite;
        Color _normalSkinColor = Color.white;
        Color _normalLabelColor = Color.white;
        Color _normalIconColor = Color.white;

        /// <summary>Unavailable이 끈 것 (돌아갈 때 이것만 되돌린다)</summary>
        bool _disabledButton;
        bool _hidRow;
        /// <summary>쓸 수 없음으로 바꾸기 전 이름 (돌아올 때 그 말이 그대로면 되돌린다)</summary>
        string _labelBeforeUnavailable;
        string _unavailableText;

        float _denyTime;
        bool _shaking;
        Vector2 _labelBase;
        Vector2 _rowBase;
        float _lastSpent = float.NegativeInfinity;

        /// <summary>지금 상태 (처음 SetState 전에는 Affordable)</summary>
        public State CurrentState => _state;
        /// <summary>부족함 상태인지 (누름 처리에서 Deny로 보낼지 고를 때)</summary>
        public bool IsShort => _applied && _state == State.Short;
        public CostSurface Surface => _surface;
        public Button Button => _button;
        public TMP_Text CostText => _costText;

        void Awake()
        {
            EnsureInit();
            // 흔들 때만 Update를 켠다
            if (!_shaking) enabled = false;
        }

        void OnDisable() => StopShake();

        void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            if (_button == null) _button = GetComponent<Button>();
            if (_skin == null && _button != null) _skin = _button.targetGraphic as Image;
            if (_costRow == null && _costText != null) _costRow = _costText.rectTransform.parent as RectTransform;
            CaptureNormal();
        }

        /// <summary>지금 모양을 '보통'으로 기억한다 (처음, 그리고 넉넉함에서 떠날 때)</summary>
        void CaptureNormal()
        {
            if (_skin != null)
            {
                _normalSprite = _skin.sprite;
                _normalSkinColor = _skin.color;
            }
            if (_label != null) _normalLabelColor = _label.color;
            if (_costIcon != null) _normalIconColor = _costIcon.color;
        }

        /// <summary>
        /// 상태를 정한다. unavailableLabel은 Unavailable일 때 이름 자리에 쓸 말 ('최대 레벨' / '품절' / '보유 중' / '받을 보상 없음', null이면 이름 그대로).
        /// 같은 상태 · 같은 말이면 아무것도 하지 않는다 (재화가 바뀔 때마다 불러도 된다).
        /// </summary>
        public void SetState(State state, string unavailableLabel = null)
        {
            EnsureInit();
            bool textChanged = state == State.Unavailable && unavailableLabel != null
                && !string.Equals(unavailableLabel, _unavailableText, StringComparison.Ordinal);
            if (_applied && state == _state && !textChanged) return;

            var previous = _state;
            bool wasApplied = _applied;
            if (wasApplied && previous == State.Affordable && state != State.Affordable) CaptureNormal();
            _state = state;
            _applied = true;

            if (wasApplied && previous == State.Unavailable && state != State.Unavailable) LeaveUnavailable();

            switch (state)
            {
                case State.Affordable:
                    ApplySkin(_normalSkinColor, false);
                    ApplyLabelColor(_normalLabelColor);
                    ApplyCost(UITheme.CostAffordable(_surface), false, 1f);
                    break;
                case State.Short:
                    ApplySkin(_normalSkinColor * ShortSkinTint, false);
                    ApplyLabelColor(UITheme.WithAlpha(_normalLabelColor, _normalLabelColor.a * ShortLabelAlpha));
                    ApplyCost(UITheme.CostShort(_surface), _surface != CostSurface.Parchment, ShortIconAlpha);
                    break;
                default:
                    EnterUnavailable();
                    bool swaps = UsesDisabledSprite;
                    ApplySkin(swaps ? _normalSkinColor : UITheme.Disabled, !swaps);
                    ApplyLabelColor(swaps ? UITheme.Text : UITheme.DisabledText);
                    ApplyCost(UITheme.CostAffordable(_surface), false, 1f);
                    if (unavailableLabel != null) ApplyUnavailableText(unavailableLabel);
                    break;
            }
        }

        /// <summary>넉넉한지로 Affordable / Short를 고른다 (쓸 수 없음이 아닐 때의 지름길)</summary>
        public void SetAffordable(bool affordable) => SetState(affordable ? State.Affordable : State.Short);

        /// <summary>
        /// 막힘: 이름 · 비용 줄을 0.26초 흔들고(UIMotion.DenyOffset) 막힘 소리 + 약한 진동을 낸 뒤 Denied를 알린다.
        /// 부르는 쪽이 모자람을 확인한 뒤 부른다 (부족 안내 토스트 · 팝업은 부르는 쪽 몫).
        /// </summary>
        public void Deny()
        {
            EnsureInit();
            if (!_shaking)
            {
                if (_label != null) _labelBase = _label.rectTransform.anchoredPosition;
                if (_costRow != null) _rowBase = _costRow.anchoredPosition;
            }
            _shaking = true;
            _denyTime = 0f;
            enabled = true;
            DenySound.Play(UISfxKey.Deny);
            Haptics.Play(HapticKind.Light);
            Denied?.Invoke();
        }

        /// <summary>
        /// 재화를 썼다 (강화 · 레벨업 · 그 밖의 쓰기 성공): 쓰는 소리 Spend. 부르는 쪽이 실제로 쓴 뒤에 부른다 (모자라면 Deny, 확인 창이 있으면 확인한 뒤).
        /// 자기 성공 소리가 있는 곳(상점 구매 = Purchase, AUDIO-SPEC §8.1)에서는 부르지 않는다 — Purchase · Spend가 한 번 누름에 겹친다.
        /// 길게 눌러 반복할 때는 Step마다 불러도 된다 — 0.25초 넘게 쉬었다 온 것만 울린다. 진동 · 흔들기 없음.
        /// </summary>
        public void Spent()
        {
            float now = Time.unscaledTime;
            bool fresh = now - _lastSpent >= SpendQuiet;
            _lastSpent = now;
            if (fresh) UISfx.Play(UISfxKey.Spend);
        }

        void Update()
        {
            if (!_shaking)
            {
                enabled = false;
                return;
            }
            _denyTime += Time.unscaledDeltaTime;
            float t = _denyTime / UIMotion.Deny;
            if (t >= 1f)
            {
                StopShake();
                enabled = false;
                return;
            }
            ApplyShake(UIMotion.DenyOffset(t));
        }

        void StopShake()
        {
            if (!_shaking) return;
            _shaking = false;
            ApplyShake(0f);
        }

        void ApplyShake(float x)
        {
            if (_label != null) _label.rectTransform.anchoredPosition = _labelBase + new Vector2(x, 0f);
            if (_costRow != null) _costRow.anchoredPosition = _rowBase + new Vector2(x, 0f);
        }

        /// <summary>스프라이트 교체 버튼이라 비활성일 때 버튼이 비활성 그림을 직접 보여 주는지</summary>
        bool UsesDisabledSprite =>
            _button != null && _button.transition == Selectable.Transition.SpriteSwap && _button.spriteState.disabledSprite != null;

        void EnterUnavailable()
        {
            if (_button != null && _button.interactable)
            {
                _button.interactable = false;
                _disabledButton = true;
            }
            if (_costRow != null && _costRow.gameObject.activeSelf)
            {
                _costRow.gameObject.SetActive(false);
                _hidRow = true;
            }
        }

        void LeaveUnavailable()
        {
            if (_disabledButton && _button != null) _button.interactable = true;
            _disabledButton = false;
            if (_hidRow && _costRow != null) _costRow.gameObject.SetActive(true);
            _hidRow = false;
            if (_label != null && _unavailableText != null && _labelBeforeUnavailable != null
                && string.Equals(_label.text, _unavailableText, StringComparison.Ordinal))
            {
                _label.text = _labelBeforeUnavailable;
            }
            _unavailableText = null;
            _labelBeforeUnavailable = null;
        }

        void ApplySkin(Color color, bool unavailableSprite)
        {
            if (_skin == null) return;
            var sprite = unavailableSprite && _unavailableSprite != null ? _unavailableSprite : _normalSprite;
            if (_skin.sprite != sprite) _skin.sprite = sprite;
            if (_skin.color != color) _skin.color = color;
        }

        void ApplyLabelColor(Color color)
        {
            if (_label != null && _label.color != color) _label.color = color;
        }

        void ApplyCost(Color textColor, bool pill, float iconAlpha)
        {
            if (_costText != null && _costText.color != textColor) _costText.color = textColor;
            if (_costPill != null && _costPill.gameObject.activeSelf != pill) _costPill.gameObject.SetActive(pill);
            if (_costIcon != null)
            {
                var color = UITheme.WithAlpha(_normalIconColor, _normalIconColor.a * iconAlpha);
                if (_costIcon.color != color) _costIcon.color = color;
            }
        }

        void ApplyUnavailableText(string text)
        {
            if (_label == null) return;
            // 처음 바꿀 때(또는 부르는 쪽이 그사이 이름을 바꿨을 때)만 원래 이름을 기억한다
            if (_unavailableText == null || !string.Equals(_label.text, _unavailableText, StringComparison.Ordinal))
                _labelBeforeUnavailable = _label.text;
            _unavailableText = text;
            if (!string.Equals(_label.text, text, StringComparison.Ordinal)) _label.text = text;
        }
    }

    /// <summary>
    /// 막힘 소리 하나 (CostButtonView.Deny = Deny · 오류 토스트 = Error — 둘 다 '툭툭' 클립을 쓴다). 0.1초 안에 이미 냈으면 조용히 지나간다:
    /// 부족 흔들기와 부족 토스트가 같은 순간에 오면 (UX-SPEC §3.8) 같은 두드림이 두 번 겹쳐 들리지 않게 — 같은 키끼리는 UISfxGate가 막지만 두 키는 다르다.
    /// </summary>
    internal static class DenySound
    {
        const float Merge = 0.1f;
        static float s_last = float.NegativeInfinity;

        public static void Play(UISfxKey key)
        {
            float now = Time.unscaledTime;
            float since = now - s_last;
            // 재생을 다시 시작해 시간이 0으로 돌아갔으면 (도메인 다시 불러오기를 끈 에디터) 막지 않는다
            if (since >= 0f && since < Merge) return;
            s_last = now;
            UISfx.Play(key);
        }
    }
}
