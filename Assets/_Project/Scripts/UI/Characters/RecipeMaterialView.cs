using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 진화 레시피 ① 필요 재료 카드: 아이콘 / 이름 / 보유·필요 / 희귀 재료 기억편 대체.
    /// 기억편 대체 버튼이 없는 카드는 아래에 '획득처 ›' 글자 링크 (M71 — 20px InkSub, 이동하지 않고 레시피의 그 재료 획득처 줄로 스크롤).
    /// 기억편은 재화가 아니라 비용 줄이 있는 쓰기 확인 대신 동사 확인 창 '대체'를 쓴다.
    /// 새 직렬화 필드(_sourceLink)는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// </summary>
    public class RecipeMaterialView : MonoBehaviour
    {
        [SerializeField] Image _iconBackground;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _iconLetter;
        [SerializeField] Image _gradeLine;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _countText;
        [SerializeField] GameObject _check;
        [SerializeField] Button _substituteButton;
        [SerializeField] TMP_Text _substituteCostText;
        [Tooltip("'획득처 ›' 글자 링크 (대체 버튼이 없을 때만 켠다)")]
        [SerializeField] Button _sourceLink;

        string _formId;
        string _itemId;
        int _missing;
        int _cost;
        Action<string> _onSourceLink;

        void Awake()
        {
            _substituteButton.onClick.AddListener(OnSubstitute);
            if (_sourceLink != null) _sourceLink.onClick.AddListener(OnSourceLink);
        }

        /// <param name="hasSourceRow">레시피에 이 재료의 획득처 줄이 있는지 ('획득처 ›' 링크는 갈 줄이 있을 때만)</param>
        /// <param name="onSourceLink">'획득처 ›'를 누르면 재료 ID로 부른다 (레시피가 그 줄로 스크롤)</param>
        public void Set(string formId, MaterialStatus status, bool hasSourceRow = false, Action<string> onSourceLink = null)
        {
            _formId = formId;
            _itemId = status.ItemId;
            _onSourceLink = onSourceLink;
            var item = Game.Items.Get(status.ItemId);

            CharacterUI.SetItemIcon(_icon, _iconBackground, _iconLetter, item);
            _gradeLine.color = UITheme.GradeColor(item != null ? item.grade : ItemGrade.Common);
            _nameText.text = item != null ? item.name : status.ItemId;
            var haveColor = status.Enough ? CharacterUI.InkPositive : CharacterUI.InkNegative;
            _countText.text = $"<color={UITheme.ColorTag(haveColor)}>{status.Have:N0}</color> / {status.Need:N0}";
            _check.SetActive(status.Enough);

            _missing = Mathf.Max(0, status.Need - status.Have);
            int unitCost = Game.Evolution.GetSubstituteCost(status.ItemId);
            _cost = unitCost * _missing;
            bool substitute = !status.Enough && unitCost > 0;
            _substituteButton.gameObject.SetActive(substitute);
            _substituteCostText.text = $"기억편 {_cost:N0}";

            if (_sourceLink == null) return;
            bool link = !substitute && hasSourceRow && onSourceLink != null;
            if (_sourceLink.gameObject.activeSelf != link) _sourceLink.gameObject.SetActive(link);
        }

        void OnSourceLink()
        {
            if (!string.IsNullOrEmpty(_itemId)) _onSourceLink?.Invoke(_itemId);
        }

        void OnSubstitute()
        {
            var db = Game.Characters.Db;
            var lineage = db.GetLineageOfForm(_formId);
            var item = Game.Items.Get(_itemId);
            if (lineage == null || item == null || _missing <= 0) return;

            int shards = Game.Characters.GetShards(lineage.id);
            if (shards < _cost)
            {
                CharacterUI.Toast($"{lineage.name} 기억편이 부족해요. (보유 {shards:N0} / 필요 {_cost:N0})");
                return;
            }

            string formId = _formId;
            string itemId = _itemId;
            int count = _missing;
            UIManager.Instance.ShowConfirm("희귀 재료 대체",
                $"{lineage.name} 기억편 {_cost:N0}개로\n{item.name} {count}개를 채울까요?",
                () =>
                {
                    if (Game.Evolution.TrySubstitute(formId, itemId, count)) CharacterUI.Toast($"{item.name} {count}개를 기억편으로 채웠어요!", ToastKind.Success);
                    else CharacterUI.Toast("지금은 대체할 수 없어요.");
                }, "대체");
        }
    }
}
