using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 상세의 의상 탭: 기본 외형 + 계보 의상 목록 (해금 / 장착 / 도감 보기).
    /// 의상이 잠긴 동안 (F-03, 2-5 전): 목록은 그대로 보이고 위 요약 줄이 잠금 문장 + '미리 볼 수 있어요', 장착 · 해금 버튼은 CostumeItemView가 잠근다.
    /// </summary>
    public class CostumeListView : MonoBehaviour
    {
        [SerializeField] RectTransform _listContent;
        [SerializeField] CostumeItemView _itemPrefab;
        [SerializeField] TMP_Text _summaryText;
        [SerializeField] GameObject _emptyLabel;
        [SerializeField] ScrollRect _scroll;

        UIListPool<CostumeItemView> _pool;
        LineageDef _lineage;

        public void Show(LineageDef lineage)
        {
            _pool ??= new UIListPool<CostumeItemView>(_itemPrefab, _listContent);
            bool switched = lineage != _lineage;
            _lineage = lineage;

            var costumes = lineage.costumes;
            bool hasCostumes = costumes.Count > 0;
            _emptyLabel.SetActive(!hasCostumes);
            _pool.SetCount(hasCostumes ? costumes.Count + 1 : 0);
            if (hasCostumes)
            {
                _pool[0].SetDefault(lineage);
                for (int i = 0; i < costumes.Count; i++) _pool[i + 1].Set(lineage, costumes[i]);
            }

            int unlocked = 0;
            foreach (var costume in costumes)
            {
                if (costume != null && Game.Costumes.IsUnlocked(costume.id)) unlocked++;
            }
            if (hasCostumes && !CharacterUI.IsUnlocked(FeatureId.Costumes))
                _summaryText.text = $"의상은 {CharacterUI.LockedSentence(FeatureId.Costumes)} · 미리 볼 수 있어요";
            else
                _summaryText.text = hasCostumes
                    ? $"보유 외형 <b>{unlocked}</b>/{costumes.Count}   ·   보유 효과 공격력 <color={UITheme.ColorTag(UITheme.Positive)}>+{CharacterUI.CostumeBonusPercent(lineage):0.#}%</color>"
                    : string.Empty;

            if (switched && _scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
