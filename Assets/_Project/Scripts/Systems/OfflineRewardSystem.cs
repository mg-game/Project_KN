using System;

namespace IdleGame
{
    /// <summary>
    /// 접속하지 않은 시간만큼 단추를 보상한다. (수선점 초당 단추 × 시간 × 보상 비율, 최대 시간 제한)
    /// 초당 단추는 수선점 레벨과 최고 스테이지를 모두 반영한다.
    /// </summary>
    public class OfflineRewardSystem
    {
        readonly GameConfig _config;
        readonly GrowthSystem _growth;
        readonly CurrencySystem _currency;
        readonly ITimeProvider _clock;

        public OfflineRewardSystem(GameConfig config, GrowthSystem growth, CurrencySystem currency, ITimeProvider clock)
        {
            _config = config;
            _growth = growth;
            _currency = currency;
            _clock = clock;
        }

        public double PendingGold { get; private set; }
        public TimeSpan PendingDuration { get; private set; }
        public bool HasPending => PendingGold > 0;
        public TimeSpan MaxDuration => TimeSpan.FromHours(_config.offlineMaxHours);

        /// <summary>마지막 저장 시각 기준으로 보상을 계산해 쌓는다. 아직 받지 않은 보상이 있으면 합산한다.</summary>
        public void Calculate(long lastSaveUnix)
        {
            if (lastSaveUnix <= 0) return;

            long elapsed = TimeUtil.ToUnix(_clock.UtcNow) - lastSaveUnix;
            if (elapsed < _config.offlineMinSeconds) return;

            var total = PendingDuration + TimeSpan.FromSeconds(elapsed);
            PendingDuration = total < MaxDuration ? total : MaxDuration;
            PendingGold = _growth.GoldPerSecond * PendingDuration.TotalSeconds * _config.offlineRewardRate;
        }

        /// <summary>쌓인 보상을 지급하고 지급량을 반환한다.</summary>
        public double Claim(double multiplier = 1)
        {
            double gold = PendingGold * multiplier;
            PendingGold = 0;
            PendingDuration = TimeSpan.Zero;
            _currency.Add(CurrencyType.Gold, gold);
            return gold;
        }
    }
}
