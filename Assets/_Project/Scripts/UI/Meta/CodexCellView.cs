using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 도감 칸 하나 164×212 (Q-05, SPEC4 §6.2 — CodexPopup의 5열 격자): 등급 색 칸 틀(UiArt.SlotFrame9s) 위 그림 140 + 이름 (24, 양피지 카드 위 Ink).
    ///   · 못 찾음: 같은 그림을 (0.12, 0.09, 0.2, 0.9)로 칠한 그림자 + '???' — 누르면 힌트 말풍선 (CodexPopup이 띄운다).
    ///   · 받을 수 있음: 금 리본 120×36 '받기' (Ink 20 굵게) + 보상 칩 52.
    ///   · 받음: 오른쪽 위 체크 28.
    /// 누름 처리는 CodexPopup (받기 · 힌트 · 적 문구 · 이야기 다시 읽기).
    /// </summary>
    public class CodexCellView : MonoBehaviour
    {
        /// <summary>못 찾은 칸 그림자 색 (SPEC4 §6.2)</summary>
        public static readonly Color SilhouetteColor = new(0.12f, 0.09f, 0.2f, 0.9f);

        [SerializeField] Button _button;
        [SerializeField] Image _frame;
        [SerializeField] Image _portrait;
        [Tooltip("그림이 없을 때 (이야기 칸 · 아직 그림 없는 칸) 보이는 대신 그림")]
        [SerializeField] Sprite _fallbackSprite;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] GameObject _ribbon;
        [SerializeField] MetaRewardChip _reward;
        [SerializeField] GameObject _check;

        Action<CodexCellView> _onClick;

        /// <summary>묶인 칸 상태 (없으면 null)</summary>
        public CodexEntryStatus Status { get; private set; }

        public RectTransform Rect => (RectTransform)transform;

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(() => _onClick?.Invoke(this));
        }

        /// <summary>칸을 채운다 (열 때 · 탭을 바꿀 때 · 도감이 바뀔 때만)</summary>
        public void Bind(CodexEntryStatus status, Sprite sprite, string displayName, Color gradeTint, Action<CodexCellView> onClick)
        {
            Status = status;
            _onClick = onClick;
            bool found = status != null && status.Found;
            if (_portrait != null)
            {
                var art = sprite != null ? sprite : _fallbackSprite;
                _portrait.sprite = art;
                _portrait.enabled = art != null;
                _portrait.color = found ? Color.white : SilhouetteColor;
            }
            if (_frame != null) _frame.color = found ? gradeTint : MetaUi.Alpha(UITheme.SubText, 0.6f);
            if (_nameText != null) _nameText.text = found && !string.IsNullOrEmpty(displayName) ? displayName : "???";
            bool claimable = status != null && status.Claimable;
            if (_ribbon != null) _ribbon.SetActive(claimable);
            if (_reward != null)
            {
                var reward = claimable && status.Reward != null && status.Reward.Count > 0 ? status.Reward[0] : null;
                _reward.gameObject.SetActive(reward != null);
                if (reward != null) _reward.Set(reward);
            }
            if (_check != null) _check.SetActive(status != null && status.Claimed);
        }
    }
}
