using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 버튼을 누를 수 없는 동안 이름 글자를 알파 0.5로 흐리게 한다 (M25 · M82, 버튼 공장 SceneParts.Button / SkinButton이 붙인다).
    /// 비활성 모양은 그림 버튼이면 비활성 그림(ui_button_disabled_9s), 단색 버튼이면 disabledColor (0.62, 0.60, 0.70).
    /// 틴트로 칠한 바탕(위험 버튼처럼 흰색 틴트용 그림에 색을 입힌 것)은 비활성 그림으로 바뀌는 동안 흰색으로 돌려 회색 그림이 물들지 않게 한다.
    /// <para>
    /// 매 프레임 확인하지 않는다: 스프라이트 교체 버튼은 상태가 바뀌면 바탕 그림이 바뀌며 Graphic의 "다시 그림" 알림이 오므로 그때 확인한다
    /// (켜질 때 · 캔버스 그룹이 바뀔 때도). 그런 알림이 없는 단색(ColorTint) 버튼만 LateUpdate에서 값 하나를 비교한다 (아트가 없을 때의 자리표시).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonLabelDim : MonoBehaviour
    {
        [SerializeField] Selectable _button;
        [Tooltip("흐리게 할 이름 (TMP 글자도 Graphic)")]
        [SerializeField] Graphic _label;
        [Tooltip("누를 수 없을 때 이름 알파 배율")]
        [SerializeField, Range(0f, 1f)] float _dimAlpha = 0.5f;
        [Tooltip("색을 입힌 흰색 틴트용 바탕 (없으면 비움). 비활성 그림이 보이는 동안 흰색으로 돌린다")]
        [SerializeField] Graphic _tintedSkin;
        [SerializeField] Color _skinTint = Color.white;

        UnityAction _onDirty;
        Graphic _watched;
        bool _known;
        bool _shownOn;
        float _labelAlpha = 1f;
        bool _alphaKnown;

        void Awake()
        {
            if (_button == null) _button = GetComponent<Selectable>();
            _onDirty = Check;
            _watched = _button != null ? _button.targetGraphic : null;
            if (_watched != null) _watched.RegisterDirtyVerticesCallback(_onDirty);
            Check();
            // 스프라이트 교체 버튼은 알림으로 충분하다 — LateUpdate를 끈다
            if (!NeedsPolling) enabled = false;
        }

        void OnDestroy()
        {
            if (_watched != null && _onDirty != null) _watched.UnregisterDirtyVerticesCallback(_onDirty);
        }

        void OnEnable() => Check();

        void OnCanvasGroupChanged() => Check();

        void LateUpdate()
        {
            if (NeedsPolling) Check();
            else enabled = false;
        }

        /// <summary>다시 확인 (단색 버튼의 interactable을 바꾼 뒤 바로 반영하고 싶을 때)</summary>
        public void Refresh()
        {
            _known = false;
            Check();
        }

        bool NeedsPolling => _button != null && _button.transition != Selectable.Transition.SpriteSwap;

        void Check()
        {
            if (_button == null) return;
            bool on = _button.IsInteractable();
            if (_known && on == _shownOn) return;
            _known = true;
            _shownOn = on;

            if (_label != null)
            {
                if (!_alphaKnown)
                {
                    _labelAlpha = _label.color.a;
                    _alphaKnown = true;
                }
                var color = _label.color;
                color.a = on ? _labelAlpha : _labelAlpha * _dimAlpha;
                if (_label.color != color) _label.color = color;
            }

            if (_tintedSkin != null)
            {
                var skin = on ? _skinTint : Color.white;
                if (_tintedSkin.color != skin) _tintedSkin.color = skin;
            }
        }
    }
}
