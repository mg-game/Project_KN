using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 누를 수 없는 버튼을 눌렀을 때를 알려 준다 (F-03: 잠긴 기능 버튼은 꺼진 모양 그대로 두고, 눌리면 잠금 문장 토스트 + 약한 진동).
    /// 꺼진(interactable = false) Button은 누름을 버리지만 같은 오브젝트의 이 처리기는 그대로 받는다 — 버튼이 눌릴 수 있을 때는 아무것도 하지 않는다.
    /// 런타임에 Attach로 붙인다 (빌더 · 씬 다시 빌드 없이 — 지금 씬에서도 동작). 프레임마다 하는 일은 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockedTapRelay : MonoBehaviour, IPointerClickHandler
    {
        Button _button;
        Action _tapped;

        /// <summary>button에 붙인다 (이미 있으면 처리기만 바꾼다). button이 없으면 null</summary>
        public static LockedTapRelay Attach(Button button, Action onTappedWhileDisabled)
        {
            if (button == null) return null;
            if (!button.TryGetComponent<LockedTapRelay>(out var relay)) relay = button.gameObject.AddComponent<LockedTapRelay>();
            relay._button = button;
            relay._tapped = onTappedWhileDisabled;
            return relay;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (_button == null || _button.IsInteractable()) return;
            _tapped?.Invoke();
        }
    }
}
