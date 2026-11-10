using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 부족 팝업(ShortfallPopup)의 '얻는 곳' 한 줄: 보라 바느질 판 + 제목(28 굵게 Cream) · 설명(22 SubText) + [가기 ›] (Primary 160x72).
    /// 받지 않은 별조각 우편 줄은 금 테두리 + 금색 제목으로 강조한다. 잠긴 곳은 줄을 흐리게 하고 누르면 잠금 안내 토스트 (CurrencySources.Go).
    /// </summary>
    public class ShortfallSourceRow : MonoBehaviour
    {
        /// <summary>잠긴 줄 알파</summary>
        const float LockedAlpha = 0.55f;

        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _detail;
        [SerializeField] Button _button;
        [Tooltip("강조 테두리 (받지 않은 우편 줄만 켠다)")]
        [SerializeField] GameObject _highlight;
        [SerializeField] CanvasGroup _group;

        CurrencySource _source;
        Action<CurrencySource> _onGo;

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnGo);
        }

        public void Set(in CurrencySource source, Action<CurrencySource> onGo)
        {
            _source = source;
            _onGo = onGo;
            if (_title != null)
            {
                _title.text = source.Label;
                _title.color = source.Highlight ? UITheme.Gold : UITheme.Cream;
            }
            if (_detail != null)
            {
                bool hasDetail = !string.IsNullOrEmpty(source.Detail);
                _detail.gameObject.SetActive(hasDetail);
                if (hasDetail) _detail.text = source.Detail;
            }
            if (_highlight != null) _highlight.SetActive(source.Highlight);
            if (_group != null) _group.alpha = source.IsUnlocked ? 1f : LockedAlpha;
        }

        void OnGo() => _onGo?.Invoke(_source);
    }
}
