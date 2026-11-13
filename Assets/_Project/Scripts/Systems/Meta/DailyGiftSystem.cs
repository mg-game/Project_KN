using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 오늘의 작은 선물 (NEW-02, SPEC4 §13.7): 상점(1-8)이 열린 뒤 날 키마다 한 번 무료 선물 (dailyGifts[요일], 월요일 = 0). 되돌림 중에는 막는다.
    /// 요일은 날 키의 날짜로 본다 (05:00 경계 — 월요일 04:59는 아직 일요일 선물). 콘텐츠가 없으면(dailyGifts 비었거나 그 요일 줄이 없음) 받을 것이 없다.
    /// 빨간 점 RedDotPaths.ShopFree (tab.shop.free — 이 시스템이 처음으로 등록한다). 받으면 dailyGift.lastDayKey = 오늘, 플레이 기록 'daily_gift' 한 줄
    /// (여기서 PlayLog.Begin으로 쓴다 — 기록이 닫혀 있으면 아무것도 안 함), Claimed.
    /// </summary>
    public class DailyGiftSystem : IMetaModule
    {
        static readonly List<MetaReward> NoRewards = new();

        /// <summary>받음 · 날이 바뀜</summary>
        public event Action Changed;
        /// <summary>오늘 선물을 받음 (받은 목록) — 보상 날아가기 등 (플레이 기록 'daily_gift' 줄은 TryClaim이 직접 쓴다)</summary>
        public event Action<List<MetaReward>> Claimed;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;

        RedDotSystem _dots;

        public DailyGiftSystem(PlayerData data, MetaDatabase db, DailyClock clock, MetaRewardService rewards, FeatureGateSystem features)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _rewards = rewards;
            _features = features;
        }

        DailyGiftSave Save => (_data.meta ??= new MetaSave()).dailyGift ??= new DailyGiftSave();

        bool Tampered => _clock != null && _clock.IsTampered;

        /// <summary>상점이 열렸는지 (FeatureId.Shop, 정의 없음 = 열림)</summary>
        public bool IsUnlocked => _features == null || _features.IsUnlocked(FeatureId.Shop);

        /// <summary>오늘 이미 받았는지</summary>
        public bool ClaimedToday
        {
            get
            {
                string last = Save.lastDayKey;
                return _clock != null && last != null && last == _clock.DayKey;
            }
        }

        /// <summary>오늘 선물을 아직 안 받았고 받을 수 있는지 (열림 · 되돌림 아님 · 오늘 줄이 있음)</summary>
        public bool CanClaim => IsUnlocked && !Tampered && _clock != null && !ClaimedToday && Today.Count > 0;

        /// <summary>오늘의 선물 내용 (없으면 빈 목록). 할당 없음</summary>
        public IReadOnlyList<MetaReward> Today
        {
            get
            {
                if (_clock == null || _db == null) return NoRewards;
                var def = _db.GetDailyGift(DailyClock.WeekdayOf(_clock.DayDate));
                return def?.rewards != null ? def.rewards : NoRewards;
            }
        }

        /// <summary>다음 선물까지 (다음 05:00)</summary>
        public TimeSpan TimeUntilNext => _clock != null ? _clock.TimeUntilDailyReset : TimeSpan.Zero;

        /// <summary>오늘 선물을 받는다. 받을 수 없으면 null</summary>
        public List<MetaReward> TryClaim()
        {
            if (!CanClaim) return null;
            var rewards = Today;
            Save.lastDayKey = _clock.DayKey;
            var granted = _rewards != null ? _rewards.Grant(rewards) : MetaRewardService.Merge(rewards);
            PlayLog.Begin("daily_gift").Int("weekday", DailyClock.WeekdayOf(_clock.DayDate)).End();
            Claimed?.Invoke(granted);
            NotifyChanged();
            return granted;
        }

        // ── IMetaModule ──

        public void Connect()
        {
            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
        }

        public void OnDayChanged(string dayKey) => NotifyChanged();

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            if (dots == null) return;
            _dots = dots;
            dots.Register(RedDotPaths.ShopFree, () => CanClaim);
        }

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature == FeatureId.Shop) NotifyChanged();
        }

        void NotifyChanged()
        {
            _dots?.MarkDirty(RedDotPaths.ShopFree);
            Changed?.Invoke();
        }
    }
}
