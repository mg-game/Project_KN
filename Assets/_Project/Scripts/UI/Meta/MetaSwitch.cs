using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 켜기/끄기 스위치 (설정). 누르면 손잡이가 미끄러지고, 켜지면 홈 안쪽 채움이 보랏빛으로 차오른다.
    /// 홈·손잡이는 그림 그대로 두고 홈에는 보랏빛을 반까지만 섞는다. Changed는 사용자가 눌렀을 때만 울린다.
    /// 스위치 길 전용 그림 한 쌍(_trackOn · _trackOff — UiArt.SwitchOn9s/Off9s, ART-UPGRADE §4.3 #5)이 둘 다 있으면 색 대신 그림을 바꿔 끼우고
    /// 채움은 쓰지 않는다 (흰색 그대로). 하나라도 없으면 지금처럼 색으로 칠한다.
    /// </summary>
    public class MetaSwitch : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] Image _track;
        [Tooltip("홈 안쪽 채움 (켜지면 보이고 꺼지면 투명). 없어도 됨 — 그때는 홈 색으로만 보여 준다")]
        [SerializeField] Image _fill;
        [SerializeField] RectTransform _knob;
        [Tooltip("손잡이가 가운데에서 좌우로 움직이는 거리 (px)")]
        [SerializeField] float _travel = 26f;
        [Tooltip("켜진 길 그림 (없어도 됨 — 둘 다 있을 때만 쓴다)")]
        [SerializeField] Sprite _trackOn;
        [Tooltip("꺼진 길 그림 (없어도 됨)")]
        [SerializeField] Sprite _trackOff;

        /// <summary>그림 홈 (채움이 있을 때): 꺼짐 = 흰색 그대로, 켜짐 = 보랏빛 반</summary>
        static readonly Color ArtOnColor = Color.Lerp(Color.white, UITheme.Primary, 0.5f);
        static readonly Color ArtOffColor = Color.white;
        /// <summary>자리표시 홈 (그림 없음)</summary>
        static readonly Color OnColor = UITheme.Primary;
        static readonly Color OffColor = new(0.23f, 0.19f, 0.38f, 1f);
        static readonly Color FillOn = UITheme.Primary;
        static readonly Color FillOff = MetaUi.Alpha(UITheme.Primary, 0f);

        Coroutine _slide;

        /// <summary>사용자가 눌러 바뀜 (새 값)</summary>
        public event Action<bool> Changed;

        public bool IsOn { get; private set; }

        /// <summary>길 그림 한 쌍을 쓰는지 (둘 다 있고 서로 다름)</summary>
        bool Skinned => _trackOn != null && _trackOff != null && _trackOn != _trackOff;

        void Awake() => _button.onClick.AddListener(Toggle);

        void Toggle()
        {
            SetOn(!IsOn, true);
            Changed?.Invoke(IsOn);
        }

        /// <summary>값을 바꾼다 (알리지 않음)</summary>
        public void SetOn(bool on, bool animate = false)
        {
            IsOn = on;
            if (_slide != null) StopCoroutine(_slide);
            _slide = null;
            if (animate && isActiveAndEnabled)
            {
                _slide = StartCoroutine(Slide(on));
                return;
            }
            _knob.anchoredPosition = new Vector2(on ? _travel : -_travel, 0f);
            ApplyTrackSprite(on);
            _track.color = TrackColor(on);
            if (_fill != null) _fill.color = Skinned ? FillOff : on ? FillOn : FillOff;
        }

        Color TrackColor(bool on) =>
            Skinned ? Color.white : _fill != null ? (on ? ArtOnColor : ArtOffColor) : (on ? OnColor : OffColor);

        /// <summary>길 그림 한 쌍이 있으면 켜짐 · 꺼짐 그림으로 바꿔 끼운다</summary>
        void ApplyTrackSprite(bool on)
        {
            if (!Skinned) return;
            var sprite = on ? _trackOn : _trackOff;
            if (_track.sprite != sprite) _track.sprite = sprite;
        }

        IEnumerator Slide(bool on)
        {
            float from = _knob.anchoredPosition.x;
            float to = on ? _travel : -_travel;
            ApplyTrackSprite(on);
            var fromColor = _track.color;
            var toColor = TrackColor(on);
            var fromFill = _fill != null ? _fill.color : FillOff;
            var toFill = Skinned ? FillOff : on ? FillOn : FillOff;
            const float duration = 0.14f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = MetaUi.EaseOutCubic(t / duration);
                _knob.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k), 0f);
                _track.color = Color.Lerp(fromColor, toColor, k);
                if (_fill != null) _fill.color = Color.Lerp(fromFill, toFill, k);
                yield return null;
            }
            _knob.anchoredPosition = new Vector2(to, 0f);
            _track.color = toColor;
            if (_fill != null) _fill.color = toFill;
            _slide = null;
        }

        void OnDisable()
        {
            if (_slide == null) return;
            StopCoroutine(_slide);
            _slide = null;
            SetOn(IsOn);
        }
    }
}
