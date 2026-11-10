namespace IdleGame
{
    /// <summary>
    /// 게임 진행에 따른 1회성 보상을 우편으로 보낸다.
    /// - 수선점 N레벨마다 레벨 달성 보상
    /// - 첫 계정 연동 보상
    /// </summary>
    public class RewardMailSystem
    {
        readonly MailSystem _mail;
        readonly GameConfig _config;

        public RewardMailSystem(GrowthSystem growth, AccountSystem account, MailSystem mail, GameConfig config)
        {
            _mail = mail;
            _config = config;
            growth.LevelChanged += OnLevelChanged;
            account.Linked += OnAccountLinked;
        }

        void OnLevelChanged(int oldLevel, int newLevel)
        {
            int interval = _config.levelRewardInterval;
            if (interval <= 0) return;

            for (int level = (oldLevel / interval + 1) * interval; level <= newLevel; level += interval)
            {
                _mail.SendOnce(
                    $"level_reward_{level}",
                    $"수선점 Lv.{level} 달성 보상",
                    $"수선점 Lv.{level} 달성을 축하해요!\n앞으로도 수선점의 불빛을 지켜 주세요.",
                    new[] { RewardData.Currency(CurrencyType.Gem, _config.levelRewardGem) });
            }
        }

        void OnAccountLinked(AuthProviderType provider)
        {
            if (_config.accountLinkRewardGem <= 0) return;

            _mail.SendOnce(
                "account_link_reward",
                "계정 연동 보상",
                $"{AccountSystem.ProviderName(provider)} 계정 연동을 완료해 주셔서 감사합니다!\n이제 기기를 바꿔도 안전하게 이어서 플레이할 수 있어요.",
                new[] { RewardData.Currency(CurrencyType.Gem, _config.accountLinkRewardGem) });
        }
    }
}
