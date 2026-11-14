using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 재화 부족 팝업 (M08 · §2.10, UIManager.ShowShortfall). 820x700, SystemPopups 층 (확인 창처럼 튜토리얼 · 팡파르 위).
    ///   · 제목 '{재화}이/가 부족해요', 재화 아이콘 72 + '필요 900 · 보유 350' (Cream 34 굵게 / SubText 26) + '550 더 필요해요' (Negative 28).
    ///   · 얻는 곳 최대 4줄 (CurrencySources.For — 받지 않은 별조각 우편이 있으면 맨 위 강조 '우편함에 별조각 N개가 있어요 · 받기').
    ///     [가기 ›]를 누르면 이 팝업을 닫고 그곳으로 간다 (UINav · 우편함). 잠긴 곳은 닫지 않고 잠금 안내만.
    ///   · 얻는 곳 모드 (need ≤ 0, 상단 바 재화 칩 · 소환 탭 링크): 필요 · 부족 줄을 숨기고 제목 '{재화} 얻는 곳'.
    /// 줄은 빌더가 미리 만든 4칸을 켜고 끈다 (목록은 열 때 한 번만 만든다).
    /// </summary>
    public class ShortfallPopup : UIPopup
    {
        [SerializeField] TMP_Text _titleText;
        [Tooltip("필요 · 보유 · 부족 묶음 (얻는 곳 모드에서 숨긴다)")]
        [SerializeField] GameObject _needGroup;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _needText;
        [SerializeField] TMP_Text _gapText;
        [Tooltip("얻는 곳 줄 (위에서부터, 최대 4)")]
        [SerializeField] ShortfallSourceRow[] _rows;
        [Tooltip("줄 묶음 (얻는 곳 모드에서 위로 올린다)")]
        [SerializeField] RectTransform _rowsRoot;
        [Tooltip("재화 아이콘 (CurrencyType 순서: 단추 · 별조각 · 별빛 사탕)")]
        [SerializeField] Sprite[] _currencyIcons;
        [Tooltip("얻는 곳이 하나도 없을 때 문구")]
        [SerializeField] TMP_Text _emptyText;

        readonly List<CurrencySource> _sources = new(5);
        System.Action<CurrencySource> _onGo;
        float _rowsTopWithNeed = float.NaN;
        float _rowsTopSourcesOnly;

        public CurrencyType Currency { get; private set; }
        /// <summary>필요한 양 (0이면 얻는 곳 모드)</summary>
        public double Need { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _onGo = Go;
            if (_rowsRoot != null)
            {
                _rowsTopWithNeed = -_rowsRoot.offsetMax.y;
                _rowsTopSourcesOnly = 0f;
            }
        }

        /// <summary>need &gt; 0이면 부족 안내, 아니면 얻는 곳 모드</summary>
        public void Show(CurrencyType type, double need)
        {
            Currency = type;
            Need = need > 0d ? need : 0d;
            Open();
            Refresh();
        }

        void Refresh()
        {
            string name = RewardService.CurrencyName(Currency);
            bool sourcesOnly = !(Need > 0d);
            if (_titleText != null) _titleText.text = sourcesOnly ? $"{name} 얻는 곳" : $"{KoreanText.WithSubject(name)} 부족해요";

            if (_needGroup != null) _needGroup.SetActive(!sourcesOnly);
            if (!sourcesOnly) FillNeed();
            if (_rowsRoot != null && !float.IsNaN(_rowsTopWithNeed))
            {
                float top = sourcesOnly ? _rowsTopSourcesOnly : _rowsTopWithNeed;
                _rowsRoot.offsetMax = new Vector2(_rowsRoot.offsetMax.x, -top);
            }

            CurrencySources.For(Currency, _sources);
            int count = _rows != null ? Mathf.Min(_rows.Length, _sources.Count) : 0;
            for (int i = 0; _rows != null && i < _rows.Length; i++)
            {
                var row = _rows[i];
                if (row == null) continue;
                bool on = i < count;
                if (row.gameObject.activeSelf != on) row.gameObject.SetActive(on);
                if (on) row.Set(_sources[i], _onGo);
            }
            if (_emptyText != null) _emptyText.gameObject.SetActive(count == 0);
        }

        void FillNeed()
        {
            double have = Game.IsReady && Game.Currency != null ? Game.Currency.Get(Currency) : 0d;
            if (_icon != null)
            {
                int index = (int)Currency;
                var sprite = _currencyIcons != null && index >= 0 && index < _currencyIcons.Length ? _currencyIcons[index] : null;
                if (sprite != null)
                {
                    _icon.sprite = sprite;
                    _icon.color = UITheme.CurrencyIconTint(Currency);
                }
                _icon.enabled = _icon.sprite != null;
            }
            if (_needText != null)
            {
                _needText.text = $"<b>필요 {RewardService.FormatAmount(Currency, Need)}</b> "
                                 + $"<size=26><color={UITheme.ColorTag(UITheme.SubText)}>· 보유 {RewardService.FormatAmount(Currency, have)}</color></size>";
            }
            if (_gapText != null)
            {
                double gap = System.Math.Ceiling(Need - have - 1e-9);
                bool isShort = gap > 0d;
                _gapText.gameObject.SetActive(isShort);
                if (isShort) _gapText.text = $"{RewardService.FormatAmount(Currency, gap)} 더 필요해요";
            }
        }

        void Go(CurrencySource source)
        {
            // 잠긴 곳: 닫지 않고 잠금 안내만 (CurrencySources.Go가 토스트를 띄운다)
            if (!source.IsUnlocked)
            {
                CurrencySources.Go(source);
                return;
            }
            Close();
            CurrencySources.Go(source);
        }
    }
}
