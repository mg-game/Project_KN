using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 재료 정보: 아이콘, 등급, 보유 수, 설명, 획득처 목록 ('가기 ›' → 해당 원정/던전/상점, M17 · M72 · M76).
    ///   · 획득처 줄은 갈 수 있는 곳 먼저(콘텐츠 순서, 스테이지는 그 맨 뒤), 그다음 잠긴 곳을 열리는 순서로 (SourceAvailability.SortForDisplay).
    ///   · '가기 ›'를 누르면 닫기 전에 다시 확인한다: 잠겼으면 '{재료}은(는) {이유}' 토스트만 (팝업은 그대로), 스테이지는 '전투(스테이지)에서 가끔…' 토스트만.
    ///   · 갈 수 있으면 돌아가기 칸을 먼저 넣고(returnLabel이 있으면 — 가방에서 열면 '‹ 가방으로') 획득처로 이동한다 (SourceNavigator가 팝업을 닫는다).
    /// </summary>
    public class ItemInfoPopup : UIPopup
    {
        [SerializeField] RewardSlotView _icon;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _gradeText;
        [SerializeField] TMP_Text _countText;
        [SerializeField] TMP_Text _descText;
        [SerializeField] RectTransform _sourceContent;
        [SerializeField] ItemSourceRowView _sourcePrefab;
        [SerializeField] GameObject _noSourceLabel;

        readonly List<ItemSource> _sources = new();
        readonly List<SourceCheck> _checks = new();
        UIListPool<ItemSourceRowView> _pool;
        System.Action<ItemSource> _onGo;
        ItemDef _item;
        string _returnLabel;
        System.Action _reopen;

        protected override void Awake()
        {
            base.Awake();
            _pool = new UIListPool<ItemSourceRowView>(_sourcePrefab, _sourceContent);
            _onGo = Go;
        }

        /// <summary>재료 정보를 연다 (예전 Show(def)도 그대로 — 새 인자는 모두 고를 수 있다).</summary>
        /// <param name="returnLabel">
        /// 획득처 '가기 ›'로 건너뛸 때 시트 머리 돌아가기 칩에 넣을 글 ('‹ 가방으로' — 보내는 쪽이 '‹'까지 넣는다). null이면 칩을 넣지 않는다.
        /// </param>
        /// <param name="reopen">돌아가기 칩을 누르면 부를 다시 열기 (원래 화면 → 이 재료 정보). returnLabel과 함께 준다.</param>
        public void Show(ItemDef def, string returnLabel = null, System.Action reopen = null)
        {
            if (def == null) return;
            _item = def;
            _returnLabel = returnLabel;
            _reopen = reopen;
            Open();

            int count = Game.Inventory.GetCount(def.id);
            _icon.SetItem(def, null);
            _icon.SetClick(null);
            _nameText.text = def.name;
            _nameText.color = UITheme.GradeColor(def.grade);
            _gradeText.text = $"{UITheme.GradeName(def.grade)} 재료";
            _countText.text = $"보유 <color={UITheme.ColorTag(count > 0 ? UITheme.Gold : UITheme.SubText)}>{NumberFormatter.FormatInt(count)}</color>개";
            _descText.text = string.IsNullOrEmpty(def.description) ? "잊힌 물건에서 얻은 소중한 재료예요." : def.description;

            // 갈 수 있는 곳 먼저, 잠긴 곳은 열리는 순서로 (열 때 한 번만 확인 — 누를 때 다시 확인한다)
            SourceAvailability.SortForDisplay(def, _sources, _checks);
            _pool.SetCount(_sources.Count);
            for (int i = 0; i < _sources.Count; i++) _pool[i].Set(_sources[i], _checks[i], _onGo);
            _noSourceLabel.SetActive(_sources.Count == 0);
        }

        protected override void OnClose()
        {
            _item = null;
            _reopen = null;
            _returnLabel = null;
        }

        /// <summary>획득처 누름: 다시 확인 → 스테이지 / 잠김은 토스트만, 갈 수 있으면 돌아가기 칸을 넣고 이동</summary>
        void Go(ItemSource source)
        {
            if (source == null || _item == null) return;
            if (source.type == ItemSourceType.Stage)
            {
                UIManager.Instance.ShowToast(SourceAvailability.StageHint);
                return;
            }
            var check = SourceAvailability.Check(source, _item.id);
            if (!check.Available)
            {
                UIManager.Instance.ShowToast($"{MenuText.Topic(_item.name)} {check.Reason}");
                return;
            }
            if (!string.IsNullOrEmpty(_returnLabel) && _reopen != null) UINav.PushReturn(_returnLabel, _reopen);
            UINav.GoToSource(source);
        }
    }
}
