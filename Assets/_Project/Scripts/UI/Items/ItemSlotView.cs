using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>아이템/캐릭터 칸 (가방, 보상, 소환 결과에서 공용)</summary>
    public class ItemSlotView : MonoBehaviour
    {
        [SerializeField] Image _frame;
        [SerializeField] Image _icon;
        [Tooltip("아이템 아이콘이 없을 때 등급 색으로 표시할 이미지")]
        [SerializeField] Sprite _placeholderIcon;
        [SerializeField] TMP_Text _gradeText;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _countText;
        [Tooltip("새로 얻음 표시 (금 NEW 알약)")]
        [SerializeField] GameObject _newBadge;

        int _shownCount = int.MinValue;

        public void Set(ItemDef def, int count, bool isNew) => Set(def.name, def.grade, def.icon, count, isNew);

        /// <summary>캐릭터 등 아이템이 아닌 대상 표시용. 등급 칸에는 별을 표시한다.</summary>
        public void Set(LineageDef lineage, Sprite portrait, bool isNew)
        {
            Set(lineage.name, lineage.grade, portrait, 0, isNew);
            _gradeText.text = UITheme.Stars(lineage.grade);
        }

        public void Set(string displayName, ItemGrade grade, Sprite icon, int count, bool isNew)
        {
            var gradeColor = UITheme.GradeColor(grade);
            _frame.color = gradeColor;
            _gradeText.text = UITheme.GradeName(grade);
            _gradeText.color = gradeColor;
            _nameText.text = displayName;

            bool hasIcon = icon != null;
            _icon.sprite = hasIcon ? icon : _placeholderIcon;
            // 자리표시(별 그림)는 색이 있는 그림이라 등급 색을 옅게만 섞고 살짝 투명하게 (어두운 색을 곱하면 탁해진다)
            var placeholderTint = Color.Lerp(Color.white, gradeColor, 0.35f);
            _icon.color = hasIcon ? Color.white : new Color(placeholderTint.r, placeholderTint.g, placeholderTint.b, 0.75f);

            _countText.gameObject.SetActive(count > 0);
            // 개수 = '×N' (M30 · §1.10, 전각 ×는 작게 — UIFormat.TimesSmall). 바뀔 때만 다시 쓴다
            if (count > 0 && count != _shownCount)
            {
                _shownCount = count;
                _countText.text = UIFormat.TimesSmall + count;
            }
            _newBadge.SetActive(isNew);
        }
    }
}
