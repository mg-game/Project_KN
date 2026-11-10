using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>수첩 한 칸의 상태 (SeasonPassSystem.GetTiers가 채운다, 목록 재사용 가능)</summary>
    public sealed class SeasonTierStatus
    {
        public SeasonTierDef Def;
        /// <summary>칸 번호 (1부터)</summary>
        public int Tier;
        /// <summary>실제로 받을 보상 (의상을 이미 가졌으면 fallback)</summary>
        public List<MetaReward> Rewards;
        public bool Reached;
        public bool Claimed;
        public bool CanClaim => Reached && !Claimed;
    }

    /// <summary>
    /// 별빛 산책 수첩 (Q-06, SPEC4 §8.4 — 확장 항목): 이벤트가 열린 뒤 28일 시즌, 일일 미션 점수 1점 = 경험치 1, 90마다 한 칸 (30칸),
    /// 30칸 = 시즌 한정 의상 jjak_starwalk (가졌으면 fallback). 시즌이 바뀌면 받지 않은 칸은 자동으로 받는다.
    /// <para>
    /// 열림 = FeatureId.Events가 열렸고 콘텐츠(season.tiers)가 있음 (비면 꺼짐 — 확장 항목의 콘텐츠 끄개). 첫 시즌은 처음 열린 것을 본 날 키에 시작하고,
    /// days(28)개의 날 키마다 다음 시즌이 저절로 시작한다 (오래 비웠으면 지나간 시즌 수만큼 번호가 오른다 — 시작 날은 28일 간격에 맞춘다).
    /// 경험치 = 시즌 동안 받은 CounterKey.JournalPoints (MissionSystem이 일일 미션에서만 더한다, Progress.Incremented). 되돌림 중에는 받기를 막는다.
    /// 넘김: 닿았지만 받지 않은 칸은 MetaRewards로 자동으로 받고 RolledOver(받은 목록)로 알린다 (토스트 '지난 수첩의 남은 선물을 받았어요').
    /// 의상 보상(MetaRewardType.Costume)은 이미 가졌거나 표에 없으면(그림이 아직 없음) fallback을 준다.
    /// </para>
    /// </summary>
    public class SeasonPassSystem : IMetaModule
    {
        /// <summary>경험치 · 받은 칸 · 시즌이 바뀜</summary>
        public event Action Changed;
        /// <summary>시즌이 넘어가며 남은 칸을 자동으로 받음 (받은 목록, 비면 알리지 않음)</summary>
        public event Action<List<MetaReward>> RolledOver;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;

        RedDotSystem _dots;
        bool _connected;

        public SeasonPassSystem(PlayerData data, MetaDatabase db, DailyClock clock, ProgressTracker progress, MetaRewardService rewards,
            FeatureGateSystem features)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _progress = progress;
            _rewards = rewards;
            _features = features;
        }

        SeasonSave Save
        {
            get
            {
                var save = (_data.meta ??= new MetaSave()).season ??= new SeasonSave();
                save.claimedTiers ??= new HashSet<int>();
                return save;
            }
        }

        SeasonConfig Cfg => _db != null ? _db.season : null;

        int Days => Math.Max(1, Cfg?.days ?? 28);

        int XpPerTier => Math.Max(1, Cfg?.xpPerTier ?? 90);

        bool Tampered => _clock != null && _clock.IsTampered;

        /// <summary>칸 수 (콘텐츠 tiers 중 가장 큰 tier)</summary>
        public int TierCount
        {
            get
            {
                var tiers = Cfg?.tiers;
                if (tiers == null) return 0;
                int max = 0;
                for (int i = 0; i < tiers.Count; i++)
                {
                    if (tiers[i] != null && tiers[i].tier > max) max = tiers[i].tier;
                }
                return max;
            }
        }

        /// <summary>수첩이 열렸는지 (이벤트와 함께, 칸 콘텐츠가 있을 때만)</summary>
        public bool IsUnlocked => TierCount > 0 && _clock != null && (_features == null || _features.IsUnlocked(FeatureId.Events));

        /// <summary>시즌 번호 (0부터)</summary>
        public int SeasonIndex => Save.index;

        /// <summary>이번 시즌 경험치</summary>
        public long Xp => Save.xp;

        /// <summary>닿은 칸 수 (0~30)</summary>
        public int Level => (int)Math.Min(TierCount, Math.Max(0, Save.xp) / XpPerTier);

        /// <summary>다음 칸까지 모은 경험치 (xp % 90, 마지막 칸이면 90)</summary>
        public long XpIntoTier => Level >= TierCount ? XpPerTier : Math.Max(0, Save.xp) % XpPerTier;

        /// <summary>칸마다 필요한 경험치</summary>
        public int XpPerTierValue => XpPerTier;

        /// <summary>이번 시즌 남은 시간 (시작 전이면 0)</summary>
        public TimeSpan TimeLeft
        {
            get
            {
                var save = Save;
                if (_clock == null || !DailyClock.TryParseDayKey(save.startDayKey, out var start)) return TimeSpan.Zero;
                long end = _clock.DayStartUnix(start.AddDays(Days));
                return TimeSpan.FromSeconds(Math.Max(0, end - _clock.EffectiveUnix));
            }
        }

        /// <summary>모든 칸 (into는 비우고 채움)</summary>
        public void GetTiers(List<SeasonTierStatus> into)
        {
            if (into == null) return;
            into.Clear();
            if (!IsUnlocked) return;
            var save = Save;
            int level = Level;
            int count = TierCount;
            for (int tier = 1; tier <= count; tier++)
            {
                var def = _db.GetSeasonTier(tier);
                if (def == null) continue;
                into.Add(new SeasonTierStatus
                {
                    Def = def,
                    Tier = tier,
                    Rewards = RewardsOf(def),
                    Reached = tier <= level,
                    Claimed = save.claimedTiers.Contains(tier),
                });
            }
        }

        /// <summary>닿았고 받지 않은 칸이 있는지 (빨간 점, 되돌림 아님)</summary>
        public bool HasClaimable
        {
            get
            {
                if (!IsUnlocked || Tampered || string.IsNullOrEmpty(Save.startDayKey)) return false;
                var save = Save;
                int level = Level;
                for (int tier = 1; tier <= level; tier++)
                {
                    if (!save.claimedTiers.Contains(tier) && _db.GetSeasonTier(tier) != null) return true;
                }
                return false;
            }
        }

        /// <summary>한 칸을 받는다 (닿았고 아직 안 받음 · 되돌림 아님). 받을 수 없으면 null</summary>
        public List<MetaReward> TryClaim(int tier)
        {
            if (!IsUnlocked || Tampered) return null;
            EnsureSeason();
            var granted = ClaimTier(tier);
            if (granted == null) return null;
            _progress?.MarkDirty();
            NotifyChanged();
            return granted;
        }

        /// <summary>닿은 칸을 모두 받는다 (받은 것이 없으면 빈 목록)</summary>
        public List<MetaReward> ClaimAll()
        {
            var all = new List<MetaReward>();
            if (!IsUnlocked || Tampered) return all;
            EnsureSeason();
            int level = Level;
            bool any = false;
            for (int tier = 1; tier <= level; tier++)
            {
                var granted = ClaimTier(tier);
                if (granted == null) continue;
                all.AddRange(granted);
                any = true;
            }
            if (any)
            {
                _progress?.MarkDirty();
                NotifyChanged();
            }
            return MetaRewardService.Merge(all);
        }

        // ── IMetaModule ──

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_progress != null) _progress.Incremented += OnCounterIncremented;
            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
            if (EnsureSeason()) NotifyChanged();
        }

        public void OnDayChanged(string dayKey)
        {
            EnsureSeason();
            NotifyChanged();
        }

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            if (dots == null) return;
            _dots = dots;
            dots.Register(RedDotPaths.Season, () => HasClaimable);
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        void NotifyChanged()
        {
            _dots?.MarkDirty(RedDotPaths.Season);
            Changed?.Invoke();
        }

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature == FeatureId.Events && EnsureSeason()) NotifyChanged();
        }

        /// <summary>일일 미션 점수 = 경험치 (시즌 안 · 되돌림 아님)</summary>
        void OnCounterIncremented(CounterKey key, long amount)
        {
            if (key != CounterKey.JournalPoints || amount <= 0 || !IsUnlocked || Tampered) return;
            EnsureSeason();
            var save = Save;
            if (string.IsNullOrEmpty(save.startDayKey)) return;
            int before = Level;
            save.xp = Math.Max(0, save.xp) + amount;
            if (Level != before) NotifyChanged();
            else Changed?.Invoke();
        }

        /// <summary>첫 시즌을 시작하거나, 지난 시즌을 넘긴다 (남은 칸 자동 받기). 바뀌었으면 true</summary>
        bool EnsureSeason()
        {
            if (!IsUnlocked || Tampered) return false;
            var save = Save;
            string today = _clock.DayKey;
            if (string.IsNullOrEmpty(save.startDayKey) || !DailyClock.TryParseDayKey(save.startDayKey, out _))
            {
                save.startDayKey = today;
                save.index = Math.Max(0, save.index);
                save.xp = 0;
                save.claimedTiers.Clear();
                return true;
            }
            int elapsed = DailyClock.DaysBetween(save.startDayKey, today);
            if (elapsed < Days) return false;

            // 남은 칸 자동 받기 (지난 시즌 경험치 기준)
            var granted = new List<MetaReward>();
            int level = Level;
            for (int tier = 1; tier <= level; tier++)
            {
                var rewards = ClaimTier(tier);
                if (rewards != null) granted.AddRange(rewards);
            }
            int seasons = elapsed / Days;
            save.index += seasons;
            save.startDayKey = DailyClock.AddDays(save.startDayKey, seasons * Days) ?? today;
            save.xp = 0;
            save.claimedTiers.Clear();
            _progress?.MarkDirty();
            var merged = MetaRewardService.Merge(granted);
            if (merged.Count > 0) RolledOver?.Invoke(merged);
            return true;
        }

        /// <summary>한 칸 지급 (닿음 · 안 받음 · 정의 있음). 의상 보상은 가졌거나 표에 없으면 fallback. 받을 수 없으면 null</summary>
        List<MetaReward> ClaimTier(int tier)
        {
            var save = Save;
            if (tier < 1 || tier > Level || save.claimedTiers.Contains(tier)) return null;
            var def = _db.GetSeasonTier(tier);
            if (def == null) return null;
            save.claimedTiers.Add(tier);
            var rewards = RewardsOf(def);
            var granted = Grant(rewards);
            if (ReferenceEquals(rewards, def.rewards) && HasCostume(def.rewards) && !GrantedCostume(granted) && def.fallback != null && def.fallback.Count > 0)
                granted.AddRange(Grant(def.fallback)); // 의상이 표에 없어 지급되지 않음 (그림 A14 전) → fallback
            _progress?.Increment(CounterKey.SeasonTierClaim);
            return MetaRewardService.Merge(granted);
        }

        /// <summary>받을 보상: 의상 보상이 있고 그 의상을 이미 가졌으면 fallback, 아니면 rewards</summary>
        List<MetaReward> RewardsOf(SeasonTierDef def)
        {
            var rewards = def.rewards ?? new List<MetaReward>();
            if (def.fallback == null || def.fallback.Count == 0) return rewards;
            var owned = _data.unlockedCostumes;
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward != null && reward.type == MetaRewardType.Costume && !string.IsNullOrEmpty(reward.itemId)
                    && owned != null && owned.Contains(reward.itemId))
                    return def.fallback;
            }
            return rewards;
        }

        static bool HasCostume(List<MetaReward> rewards)
        {
            if (rewards == null) return false;
            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i] != null && rewards[i].type == MetaRewardType.Costume) return true;
            }
            return false;
        }

        static bool GrantedCostume(List<MetaReward> granted)
        {
            for (int i = 0; i < granted.Count; i++)
            {
                if (granted[i] != null && granted[i].type == MetaRewardType.Costume) return true;
            }
            return false;
        }

        List<MetaReward> Grant(List<MetaReward> rewards)
        {
            if (rewards == null || rewards.Count == 0) return new List<MetaReward>();
            return _rewards != null ? _rewards.Grant(rewards) : MetaRewardService.Merge(rewards);
        }
    }
}
