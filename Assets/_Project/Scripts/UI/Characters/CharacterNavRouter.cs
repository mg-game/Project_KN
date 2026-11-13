using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// UINav.OpenCharacter 요청을 받아 캐릭터 상세(진화 기억도) 팝업을 연다.
    /// 탭 패널/팝업은 꺼져 있을 수 있으므로 항상 켜져 있는 팝업 루트에 붙여 둔다.
    /// </summary>
    public class CharacterNavRouter : MonoBehaviour
    {
        void OnEnable() => UINav.CharacterOpenRequested += Open;

        void OnDisable() => UINav.CharacterOpenRequested -= Open;

        /// <summary>
        /// UINav.OpenCharacter(계보 ID, 탭) 처리기 (M73) — 새 이벤트 CharacterOpenRequested (구독자가 있으면 예전 CharacterRequested는 불리지 않는다).
        /// tab = CharacterDetailPopup.Tab* (-1이면 상세가 정한다: 다른 계보면 진화 기억도). 예) OpenCharacter("jjakjjak", CharacterDetailPopup.TabCostume) → 의상 쪽.
        /// 화면 틀은 바꾸지 않는다 — 상세 팝업은 MAIN · 시트 어디서든 위에 뜬다 (UX-SPEC-IA §8.4).
        /// </summary>
        static void Open(string lineageId, int tab)
        {
            if (!Game.IsReady || UIManager.Instance == null || string.IsNullOrEmpty(lineageId)) return;
            var popup = UIManager.Instance.Get<CharacterDetailPopup>();
            if (popup != null) popup.Show(lineageId, tab);
        }
    }
}
