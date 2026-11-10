using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 원정 준비 팝업의 친구 칸: 초상화, 이름, 레벨(22), 고른 순서 표시, 보낼 수 없으면 사유와 함께 흐리게 (M05 — 다른 원정에 나간 친구만).
    ///   · 편성 중인 친구도 고를 수 있다 (원정 중에도 함께 싸운다) — 초상화 아래 '지킴이' / '잔향이' 알약으로 알려 준다.
    ///   · 테두리는 늘 등급 색 (C-06: 고른 칸을 금빛 테두리로 칠하면 고르지 않은 전설 친구와 같아 보였다) — 고르면 바깥 6px Lamp 테 + 순서 배지.
    /// </summary>
    public class ExpeditionMemberCellView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] Image _frame;
        [SerializeField] FormPortraitView _portrait;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _levelText;
        [SerializeField] GameObject _check;
        [SerializeField] TMP_Text _orderText;
        [SerializeField] GameObject _disabled;
        [SerializeField] TMP_Text _reasonText;
        [Tooltip("고른 칸 바깥 6px Lamp 테 (테두리 색은 등급 색 그대로)")]
        [SerializeField] GameObject _selectedRing;
        [Tooltip("편성 중 알약 ('지킴이' / '잔향이', 120x26 Cream 바탕 Ink 18)")]
        [SerializeField] GameObject _placedPill;
        [SerializeField] TMP_Text _placedText;

        Action<string> _onClick;
        string _lineageId;

        void Awake() => _button.onClick.AddListener(() => _onClick?.Invoke(_lineageId));

        /// <param name="order">선택 순서 (1부터, 선택 안 됨 = 0)</param>
        /// <param name="placedLabel">편성 중이면 '지킴이' / '잔향이' (아니면 null)</param>
        public void Set(LineageDef lineage, bool selectable, string reason, int order, string placedLabel, Action<string> onClick)
        {
            _lineageId = lineage.id;
            _onClick = onClick;

            _portrait.SetLineage(lineage);
            _portrait.SetAlpha(selectable ? 1f : 0.55f);
            _nameText.text = lineage.name;
            _levelText.text = $"Lv.{Game.Characters.GetLevel(lineage.id)}  {UITheme.Stars(lineage.grade)}";

            bool selected = order > 0;
            _check.SetActive(selected);
            _orderText.text = order.ToString();
            _frame.color = UITheme.GradeColor(lineage.grade);
            if (_selectedRing != null) _selectedRing.SetActive(selected);

            bool placed = !string.IsNullOrEmpty(placedLabel);
            if (_placedPill != null) _placedPill.SetActive(placed && selectable);
            if (placed && _placedText != null) _placedText.text = placedLabel;

            _disabled.SetActive(!selectable && !selected);
            _reasonText.text = reason;
        }
    }
}
