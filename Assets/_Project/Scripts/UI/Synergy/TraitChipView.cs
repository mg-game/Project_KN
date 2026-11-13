using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 특성 칩 (아이콘 + "천"). 편성 카드 왼쪽 위에 계보 특성 2개를 보여준다.
    /// 그 특성 시너지가 지금 켜져 있으면 금빛 테두리로 밝힌다.
    /// </summary>
    public class TraitChipView : MonoBehaviour
    {
        [SerializeField] Image _bg;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _iconFallback;
        [SerializeField] TMP_Text _label;
        [SerializeField] GameObject _litRing;

        public TraitType Trait { get; private set; }

        public void Set(TraitType trait)
        {
            Trait = trait;
            var color = UITheme.TraitColor(trait);
            var name = UITheme.TraitName(trait);
            _bg.color = color;
            _label.text = name;
            SynergyIcons.Apply(_icon, _iconFallback, SynergyIcons.Trait(trait), Color.Lerp(color, UITheme.Ink, 0.35f), name.Substring(0, 1));
            // 아트가 없으면 글자가 이미 칩에 있으므로 원은 작은 점으로만 보인다
            if (_iconFallback != null) _iconFallback.gameObject.SetActive(false);
        }

        /// <summary>이 특성 시너지가 켜져 있는지</summary>
        public void SetLit(bool lit)
        {
            if (_litRing != null) _litRing.SetActive(lit);
        }
    }
}
