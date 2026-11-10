using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 획득처 한 줄: 종류 태그 / 이름 / '가기 ›' 버튼 (M17 · M76 — 스테이지도 같은 말).
    /// 잠긴 곳(SourceAvailability.Check가 막음)은 레시피 획득처 줄과 같은 모양: 줄 전체 α0.55, 이름 아래 자물쇠 28 + '스테이지 2-9 클리어 시',
    /// 버튼은 보조 모양 '잠김' — 누를 수 있고 이유를 토스트로 알려 준다 (팝업은 닫지 않는다).
    /// 누름 처리(이동 · 토스트 · 돌아가기 칸)는 ItemInfoPopup이 한다.
    /// </summary>
    public class ItemSourceRowView : MonoBehaviour
    {
        const float LockedAlpha = 0.55f;
        /// <summary>잠긴 줄의 이름 칸 아래 여백 (자물쇠 줄 자리)</summary>
        const float LockedLabelBottom = 40f;

        [SerializeField] Image _typeChip;
        [SerializeField] TMP_Text _typeText;
        [SerializeField] TMP_Text _labelText;
        [SerializeField] Button _goButton;
        [SerializeField] TMP_Text _goLabel;
        [Header("잠김 (M17)")]
        [Tooltip("줄 전체 흐림 (α0.55 — 누르기는 그대로)")]
        [SerializeField] CanvasGroup _group;
        [Tooltip("이름 아래 자물쇠 + '스테이지 2-9 클리어 시'")]
        [SerializeField] GameObject _lockRow;
        [SerializeField] TMP_Text _lockText;
        [Tooltip("보조 모양 '잠김' 버튼 (누르면 이유 토스트)")]
        [SerializeField] Button _lockedButton;

        ItemSource _source;
        Action<ItemSource> _onGo;
        float _labelBottom;
        bool _labelKnown;

        public ItemSource Source => _source;

        void Awake()
        {
            _goButton.onClick.AddListener(Go);
            if (_lockedButton != null) _lockedButton.onClick.AddListener(Go);
        }

        /// <param name="check">SourceAvailability.Check 결과 (열 때 한 번)</param>
        /// <param name="onGo">'가기 ›' / '잠김'을 누르면 (ItemInfoPopup이 다시 확인하고 이동 · 토스트)</param>
        public void Set(ItemSource source, SourceCheck check, Action<ItemSource> onGo)
        {
            _source = source;
            _onGo = onGo;
            _typeText.text = MenuText.SourceTypeName(source.type);
            _typeChip.color = TypeColor(source.type);
            _labelText.text = string.IsNullOrEmpty(source.label) ? DefaultLabel(source) : source.label;
            _goLabel.text = "가기 ›";

            bool locked = !check.Available;
            if (_group != null) _group.alpha = locked ? LockedAlpha : 1f;
            _goButton.gameObject.SetActive(!locked || _lockedButton == null);
            if (_lockedButton != null) _lockedButton.gameObject.SetActive(locked);
            if (_lockRow != null) _lockRow.SetActive(locked);
            if (locked && _lockText != null) _lockText.text = LockLine(check);
            PlaceLabel(locked && _lockRow != null);
        }

        /// <summary>잠긴 줄의 짧은 이유: 스테이지로 열리면 '스테이지 2-9 클리어 시', 아니면 이유 문장 그대로</summary>
        static string LockLine(SourceCheck check)
        {
            bool byStage = check.RequiredStage > 1 && check.RequiredStage < int.MaxValue;
            return byStage ? MetaText.UnlockShort(check.RequiredStage) : check.Reason;
        }

        /// <summary>잠긴 줄은 이름을 위로 올려 아래에 자물쇠 줄 자리를 비운다</summary>
        void PlaceLabel(bool withLockRow)
        {
            var rect = _labelText.rectTransform;
            if (!_labelKnown)
            {
                _labelKnown = true;
                _labelBottom = rect.offsetMin.y;
            }
            float bottom = withLockRow ? _labelBottom + LockedLabelBottom : _labelBottom;
            if (!Mathf.Approximately(rect.offsetMin.y, bottom)) rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
        }

        void Go()
        {
            if (_source != null) _onGo?.Invoke(_source);
        }

        static string DefaultLabel(ItemSource source) => source.type switch
        {
            ItemSourceType.Stage => "스테이지 전투 보상",
            ItemSourceType.Dungeon => $"잔향이 던전 {source.targetId}층",
            _ => source.targetId,
        };

        static Color TypeColor(ItemSourceType type) => type switch
        {
            ItemSourceType.Stage => UITheme.DustyPink,
            ItemSourceType.Expedition => UITheme.SoftBlue,
            ItemSourceType.Dungeon => UITheme.Lavender,
            ItemSourceType.Shop => UITheme.Gold,
            _ => UITheme.EventToken,
        };
    }
}
