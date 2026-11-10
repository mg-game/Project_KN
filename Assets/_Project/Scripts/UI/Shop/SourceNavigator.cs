using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// "재료 수집하러 가기" 등 획득처 이동 요청(UINav.SourceRequested)을 받아 화면을 옮긴다.
    /// 열린 팝업을 모두 닫은 뒤 원정 구역 / 잔향이 던전 / 상점으로 이동해 대상을 강조한다.
    /// </summary>
    public class SourceNavigator : MonoBehaviour
    {
        [SerializeField] ExpeditionPanel _expedition;
        [SerializeField] ShopPanel _shop;

        void Awake() => UINav.SourceRequested += OnSourceRequested;

        void OnDestroy() => UINav.SourceRequested -= OnSourceRequested;

        void OnSourceRequested(ItemSourceType type, string targetId)
        {
            CloseAllPopups();
            switch (type)
            {
                case ItemSourceType.Expedition:
                    UINav.GoToTab(MainTab.Expedition);
                    if (_expedition != null) _expedition.FocusZone(targetId);
                    break;
                case ItemSourceType.Dungeon:
                    UINav.GoToTab(MainTab.Expedition);
                    if (_expedition != null) _expedition.FocusDungeon();
                    if (int.TryParse(targetId, out int floor) && floor > 0)
                    {
                        UIManager.Instance.ShowToast($"잔향이 던전 {floor}층부터 얻을 수 있어요.");
                    }
                    break;
                case ItemSourceType.Shop:
                case ItemSourceType.Event:
                    UINav.GoToTab(MainTab.Shop);
                    if (_shop != null) _shop.SelectShop(ResolveShopId(type, targetId));
                    break;
                default:
                    UIManager.Instance.ShowToast(SourceAvailability.StageHint);
                    break;
            }
        }

        static string ResolveShopId(ItemSourceType type, string targetId)
        {
            if (!string.IsNullOrEmpty(targetId)) return targetId;
            return type == ItemSourceType.Event ? ShopPanel.EventShopId : ShopPanel.NormalShopId;
        }

        /// <summary>열린 팝업을 위에서부터 모두 닫는다 (닫기 규칙 없이 — CloseTop을 되풀이하면 막는 팝업에서 멈춘다)</summary>
        static void CloseAllPopups()
        {
            var ui = UIManager.Instance;
            if (ui != null) ui.CloseAll();
        }
    }
}
