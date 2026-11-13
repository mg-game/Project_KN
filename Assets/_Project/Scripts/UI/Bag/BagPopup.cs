using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 가방: 진화 재료 도감형 목록. 모든 재료를 보여주고 없는 재료는 흐리게 (수량 0은 SubText, 가진 수는 금빛 그대로 — M70) 표시한다.
    /// 재료를 누르면 설명과 획득처(가기 버튼)가 있는 정보 팝업을 연다 — 획득처로 건너뛰면 시트 머리에 '‹ 가방으로' 칩 (다시 누르면 가방 → 그 재료).
    /// 창 아래에는 늘 '재료 모으는 곳' 칸 (M70 — 1060 창의 빈 자리를 채운다): 안내 '재료는 원정·던전·상점에서 모을 수 있어요' +
    /// 바로 가기 3칸 [원정 ›][잔향이 던전 ›][상점 ›]. 잠긴 곳은 흐리게(α0.55) + 자물쇠, 누르면 여는 조건을 토스트로 알려 주고 창은 그대로 (M17).
    /// 열린 곳은 '‹ 가방으로' 돌아가기 칸을 넣고 그 탭으로 간다.
    /// </summary>
    public class BagPopup : UIPopup
    {
        const string ReturnLabel = "‹ 가방으로";
        const float LockedAlpha = 0.55f;

        /// <summary>바로 가기 순서 (빌더의 SourceButton_* 순서와 같다): 원정 · 잔향이 던전 · 상점</summary>
        static readonly ItemSourceType[] SourceTypes = { ItemSourceType.Expedition, ItemSourceType.Dungeon, ItemSourceType.Shop };
        static readonly FeatureId[] SourceFeatures = { FeatureId.Expedition, FeatureId.Dungeon, FeatureId.Shop };
        static readonly string[] SourceNames = { "원정", "잔향이 던전", "상점" };

        [SerializeField] RectTransform _content;
        [SerializeField] RewardSlotView _slotPrefab;
        [SerializeField] TMP_Text _summaryText;
        [SerializeField] GameObject _emptyLabel;
        [Header("재료 모으는 곳 (원정 · 잔향이 던전 · 상점 순서)")]
        [SerializeField] Button[] _sourceButtons;
        [Tooltip("잠긴 곳 흐림 (α0.55 — 누르기는 그대로, 이유를 토스트로)")]
        [SerializeField] CanvasGroup[] _sourceGroups;
        [SerializeField] GameObject[] _sourceLocks;

        readonly List<ItemDef> _items = new();
        UIListPool<RewardSlotView> _pool;
        bool _subscribed;

        protected override void Awake()
        {
            base.Awake();
            _pool = new UIListPool<RewardSlotView>(_slotPrefab, _content);
            if (_sourceButtons == null) return;
            for (int i = 0; i < _sourceButtons.Length && i < SourceTypes.Length; i++)
            {
                int index = i;
                if (_sourceButtons[i] != null) _sourceButtons[i].onClick.AddListener(() => GoSource(index));
            }
        }

        protected override void OnOpen()
        {
            if (!_subscribed)
            {
                Game.Inventory.Changed += Refresh;
                _subscribed = true;
            }
            Refresh();
            RefreshSources();
        }

        protected override void OnClose() => Unsubscribe();

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (Game.IsReady) Game.Inventory.Changed -= Refresh;
        }

        void Refresh()
        {
            CollectItems();
            int owned = 0;
            int materials = 0;
            _pool.SetCount(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                var def = _items[i];
                int count = Game.Inventory.GetCount(def.id);
                if (def.category == ItemCategory.Material)
                {
                    materials++;
                    if (count > 0) owned++;
                }

                var slot = _pool[i];
                slot.SetItem(def, UIFormat.Times + NumberFormatter.FormatInt(count));
                slot.SetAmountColor(count > 0 ? null : UITheme.SubText);
                slot.SetDimmed(count <= 0);
                slot.SetClick(() => OpenInfo(def));
            }

            _summaryText.text = $"모은 재료 <color={UITheme.ColorTag(UITheme.Gold)}>{owned}</color> / {materials}종";
            _emptyLabel.SetActive(_items.Count == 0);
        }

        /// <summary>바로 가기 잠김 모양 (열 때만 — 기능이 열리는 순간은 창이 닫혀 있다)</summary>
        void RefreshSources()
        {
            if (_sourceButtons == null) return;
            for (int i = 0; i < _sourceButtons.Length && i < SourceFeatures.Length; i++)
            {
                bool open = IsSourceOpen(SourceFeatures[i]);
                if (_sourceGroups != null && i < _sourceGroups.Length && _sourceGroups[i] != null) _sourceGroups[i].alpha = open ? 1f : LockedAlpha;
                if (_sourceLocks != null && i < _sourceLocks.Length && _sourceLocks[i] != null) _sourceLocks[i].SetActive(!open);
            }
        }

        /// <summary>기능 잠금 확인 (메타가 꺼진 빌드 = 늘 열림)</summary>
        static bool IsSourceOpen(FeatureId feature) => Game.Features == null || Game.Features.IsUnlocked(feature);

        /// <summary>재료 정보 (획득처로 건너뛰면 '‹ 가방으로' — 가방을 다시 열고 그 재료 정보를 다시 연다)</summary>
        void OpenInfo(ItemDef def)
        {
            var info = UIManager.Instance.Get<ItemInfoPopup>();
            if (info != null) info.Show(def, ReturnLabel, () => Reopen(def));
        }

        static void Reopen(ItemDef def)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            var bag = ui.Get<BagPopup>();
            if (bag == null) return;
            bag.Open();
            bag.OpenInfo(def);
        }

        /// <summary>
        /// 바로 가기: 잠겼으면 '잔향이 던전은 스테이지 1-10을 클리어하면 열려요' 토스트만 (창은 그대로, M17),
        /// 열렸으면 '‹ 가방으로' 돌아가기 칸을 넣고 그곳으로 (SourceNavigator가 팝업을 닫고 탭 · 쪽을 고른다)
        /// </summary>
        static void GoSource(int index)
        {
            var feature = SourceFeatures[index];
            if (!IsSourceOpen(feature))
            {
                string reason = Game.Features.GetRequirementText(feature);
                if (UIManager.Instance != null) UIManager.Instance.ShowToast($"{MenuText.Topic(SourceNames[index])} {reason}");
                return;
            }
            UINav.PushReturn(ReturnLabel, ReopenBag);
            UINav.GoToSource(SourceTypes[index], null);
        }

        static void ReopenBag()
        {
            var bag = UIManager.Instance != null ? UIManager.Instance.Get<BagPopup>() : null;
            if (bag != null) bag.Open();
        }

        /// <summary>모든 재료(보유 여부 무관) + 보유한 그 밖의 아이템. 가진 것 먼저, 높은 등급 먼저.</summary>
        void CollectItems()
        {
            _items.Clear();
            foreach (var def in Game.Items.items)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                if (def.category == ItemCategory.Material || Game.Inventory.GetCount(def.id) > 0) _items.Add(def);
            }
            _items.Sort((a, b) =>
            {
                bool ownedA = Game.Inventory.GetCount(a.id) > 0;
                bool ownedB = Game.Inventory.GetCount(b.id) > 0;
                if (ownedA != ownedB) return ownedA ? -1 : 1;
                return a.grade != b.grade ? b.grade.CompareTo(a.grade) : string.CompareOrdinal(a.id, b.id);
            });
        }
    }
}
