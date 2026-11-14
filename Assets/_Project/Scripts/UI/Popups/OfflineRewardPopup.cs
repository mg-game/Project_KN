using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>오프라인 보상 (메타가 꺼졌을 때의 예전 화면). 받기(금 버튼)/닫기 어느 쪽이든 보상은 지급된다 (Success 토스트).</summary>
    public class OfflineRewardPopup : UIPopup
    {
        [SerializeField] TMP_Text _durationText;
        [SerializeField] TMP_Text _goldText;
        [Tooltip("실타래 줄 글 (결정 2 안 D: 단추와 함께 모인 실타래). 줄 묶음은 이 글의 부모")]
        [SerializeField] TMP_Text _lineageXpText;
        [SerializeField] TMP_Text _infoText;
        [SerializeField] Button _claimButton;

        protected override void Awake()
        {
            base.Awake();
            _claimButton.onClick.AddListener(Close);
        }

        protected override void OnOpen()
        {
            var reward = Game.OfflineReward;
            _durationText.text = $"{TimeUtil.FormatDuration(reward.PendingDuration)} 동안 모은 {MetaRewardService.IncomeLabel}";
            _goldText.text = RewardService.FormatAmount(CurrencyType.Gold, reward.PendingGold);
            if (_lineageXpText != null)
            {
                bool lineage = reward.PendingLineageXp >= 1;
                var row = _lineageXpText.transform.parent;
                if (row != null && row != transform && row.gameObject.activeSelf != lineage) row.gameObject.SetActive(lineage);
                if (lineage) _lineageXpText.text = RewardService.FormatAmount(CurrencyType.LineageXp, reward.PendingLineageXp);
            }
            _infoText.text = $"보상은 최대 {Game.Config.offlineMaxHours:0.#}시간까지 쌓여요.";
        }

        protected override void OnClose()
        {
            if (!Game.IsReady || !Game.OfflineReward.HasPending) return;
            double gold = Game.OfflineReward.Claim();
            UISfx.Play(UISfxKey.Claim);
            UIManager.Instance.ShowToast(ClaimMessage(gold, Game.OfflineReward.LastClaimedLineageXp), ToastKind.Success);
        }

        /// <summary>
        /// 백그라운드로 갈 때 미리 지급한 보상을 복귀 시 알린다.
        /// 새로 쌓인 보상이 없으면 이미 지급한 금액이 남아 있는 팝업을 닫는다. (받기를 눌러도 아무 일도 없는 상태 방지)
        /// </summary>
        public void NotifyClaimedInBackground(double gold, double lineageXp = 0)
        {
            if (IsOpen && Game.IsReady && !Game.OfflineReward.HasPending) Close();
            if (gold > 0 || lineageXp > 0) UIManager.Instance.ShowToast(ClaimMessage(gold, lineageXp), ToastKind.Success);
        }

        /// <summary>예: "단추 1,800 획득!"</summary>
        static string ClaimMessage(double gold, double lineageXp)
        {
            // 안 D: 실타래도 함께 들어온다 (OfflineRewardSystem.LastClaimedLineageXp)
            string lineage = lineageXp >= 1
                ? $" · {RewardService.CurrencyName(CurrencyType.LineageXp)} {RewardService.FormatAmount(CurrencyType.LineageXp, lineageXp)}"
                : string.Empty;
            return $"{RewardService.CurrencyName(CurrencyType.Gold)} {RewardService.FormatAmount(CurrencyType.Gold, gold)}{lineage} 획득!";
        }
    }
}
