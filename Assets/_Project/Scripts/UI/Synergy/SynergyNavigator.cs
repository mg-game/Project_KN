using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// UINav.OpenSynergy 요청을 받아 시너지 상세(ID) 또는 시너지 도감(null = 첫 화면)을 연다.
    /// 시너지 아이콘 스프라이트도 여기에 연결해 SynergyIcons에 등록한다.
    /// 탭 패널은 꺼져 있을 수 있으므로 항상 켜져 있는 팝업 루트에 붙여 둔다.
    /// </summary>
    public class SynergyNavigator : MonoBehaviour
    {
        [Tooltip("TraitType 순서 (Art/Icons/trait_*)")]
        [SerializeField] Sprite[] _traitIcons;
        [Tooltip("CombatStyle 순서 (Art/Icons/style_*)")]
        [SerializeField] Sprite[] _styleIcons;
        [Tooltip("StageModifierType 순서 (Art/Icons/stage_*), 0 = None")]
        [SerializeField] Sprite[] _stageIcons;
        [Tooltip("조합 아이콘 (Art/Synergy/{시너지 ID}) — 스프라이트 이름으로 찾는다")]
        [SerializeField] Sprite[] _comboIcons;
        [Tooltip("아트가 없을 때의 자리표시 (비우면 아이콘을 끄고 글자만)")]
        [SerializeField] Sprite _placeholder;
        [Tooltip("역할 아이콘 · 글자 자리표시 뒤 메달 홈 (Art/UI/ui_medallion_dark)")]
        [SerializeField] Sprite _socket;
        [Tooltip("계보 공명 초상화 뒤 둥근 받침 (Art/UI/ui_plate_round)")]
        [SerializeField] Sprite _plate;

        void Awake() => SynergyIcons.Register(_traitIcons, _styleIcons, _stageIcons, _comboIcons, _placeholder, _socket, _plate);

        void OnEnable() => UINav.SynergyOpenRequested += Open;

        void OnDisable() => UINav.SynergyOpenRequested -= Open;

        /// <summary>
        /// UINav.OpenSynergy(시너지 ID, 종류 힌트) 처리기 — 새 이벤트 SynergyOpenRequested (구독자가 있으면 예전 SynergyRequested는 불리지 않는다).
        /// 아는 ID면 시너지 상세, 아니면(null · 모르는 ID) 도감: 힌트가 있으면 그 종류 탭(전투 시너지 칩 M43 — 켜진 첫 시너지의 종류), 없으면 마지막으로 본 탭.
        /// 구독은 새 이벤트 하나뿐이다 (예전 SynergyRequested는 구독자가 없어 울리지 않는다).
        /// </summary>
        static void Open(string synergyId, SynergyKind? kindHint)
        {
            if (!Game.IsReady || UIManager.Instance == null) return;
            if (string.IsNullOrEmpty(synergyId) || Game.Synergy.Db == null || Game.Synergy.Db.Get(synergyId) == null)
            {
                var codex = UIManager.Instance.Get<SynergyCodexPopup>();
                if (codex == null) return;
                if (kindHint.HasValue) codex.Show(kindHint.Value);
                else codex.ShowFront();
                return;
            }
            var detail = UIManager.Instance.Get<SynergyDetailPopup>();
            if (detail != null) detail.Show(synergyId);
        }
    }
}
