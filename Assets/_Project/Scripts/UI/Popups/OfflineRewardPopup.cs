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
            _durationText.text = $"{TimeUtil.FormatDuration(reward.PendingDuration)} 동안 모은 {RewardService.CurrencyName(CurrencyType.Gold)}";
            _goldText.text = RewardService.FormatAmount(CurrencyType.Gold, reward.PendingGold);
            _infoText.text = $"보상은 최대 {Game.Config.offlineMaxHours:0.#}시간까지 쌓여요.";
        }

        protected override void OnClose()
        {
            if (!Game.IsReady || !Game.OfflineReward.HasPending) return;
            double gold = Game.OfflineReward.Claim();
            UISfx.Play(UISfxKey.Claim);
            UIManager.Instance.ShowToast(ClaimMessage(gold), ToastKind.Success);
        }

        /// <summary>
        /// 백그라운드로 갈 때 미리 지급한 보상을 복귀 시 알린다.
        /// 새로 쌓인 보상이 없으면 이미 지급한 금액이 남아 있는 팝업을 닫는다. (받기를 눌러도 아무 일도 없는 상태 방지)
        /// </summary>
        public void NotifyClaimedInBackground(double gold)
        {
            if (IsOpen && Game.IsReady && !Game.OfflineReward.HasPending) Close();
            if (gold > 0) UIManager.Instance.ShowToast(ClaimMessage(gold), ToastKind.Success);
        }

        /// <summary>예: "단추 1,800 획득!"</summary>
        static string ClaimMessage(double gold) =>
            $"{RewardService.CurrencyName(CurrencyType.Gold)} {RewardService.FormatAmount(CurrencyType.Gold, gold)} 획득!";
    }
}
