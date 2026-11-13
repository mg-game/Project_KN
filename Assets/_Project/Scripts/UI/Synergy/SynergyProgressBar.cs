using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 진행 막대: 지금 수만큼 채우고, 단계 문턱(2 · 3 · 4)마다 눈금과 숫자를 단다.
    /// 도달한 눈금은 금색, 지금 단계 눈금은 조금 크게.
    /// 채움은 9-slice 캡슐 그림이라 Filled 대신 너비(anchorMax.x)로 줄인다. 틀 안쪽 여백(offset)은 빌더가 정한 값을 그대로 둔다.
    /// </summary>
    public class SynergyProgressBar : MonoBehaviour
    {
        [SerializeField] RectTransform _fill;
        [SerializeField] Image _fillImage;
        [Tooltip("눈금 (최대 4). 앵커 x로 위치를 잡는다")]
        [SerializeField] RectTransform[] _ticks;
        [SerializeField] Image[] _tickDots;
        [SerializeField] TMP_Text[] _tickLabels;
        [SerializeField] TMP_Text _countText;

        public void Set(SynergyStatus status)
        {
            var def = status?.Def;
            if (def == null) return;
            int max = SynergyUI.MaxCount(def);
            int count = status.Count;
            var accent = SynergyUI.AccentColor(def);

            float fill = Mathf.Clamp01(count / (float)max);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(fill, 1f);
            _fill.gameObject.SetActive(fill > 0f);
            _fillImage.color = status.IsActive ? accent : Color.Lerp(accent, UITheme.SubText, 0.45f);

            int tiers = def.kind == SynergyKind.Combo ? 0 : SynergyUI.TierCount(def);
            for (int i = 0; i < _ticks.Length; i++)
            {
                bool shown = i < tiers;
                _ticks[i].gameObject.SetActive(shown);
                if (!shown) continue;
                int threshold = SynergyUI.TierThreshold(def, i);
                float x = Mathf.Clamp01(threshold / (float)max);
                _ticks[i].anchorMin = new Vector2(x, 0.5f);
                _ticks[i].anchorMax = new Vector2(x, 0.5f);
                _ticks[i].anchoredPosition = Vector2.zero;

                bool reached = count >= threshold;
                bool current = i == status.TierIndex;
                _tickDots[i].color = reached ? UITheme.Gold : UITheme.Divider;
                _tickDots[i].rectTransform.localScale = Vector3.one * (current ? 1.3f : 1f);
                _tickLabels[i].text = threshold.ToString();
                // 문턱 숫자 18 굵게 (M44 — 도달 Gold, 아직 SubText). 지금 단계는 눈금 점이 크게 보여 준다
                _tickLabels[i].color = reached ? UITheme.Gold : UITheme.SubText;
                if ((_tickLabels[i].fontStyle & FontStyles.Bold) == 0) _tickLabels[i].fontStyle |= FontStyles.Bold;
            }

            if (_countText != null)
            {
                _countText.text = SynergyUI.ProgressLabel(status, true, false);
                _countText.color = status.IsActive ? UITheme.Cream : UITheme.SubText;
            }
        }
    }
}
