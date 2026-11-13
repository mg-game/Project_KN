using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>수선점 기록 한 줄의 상태 (AchievementSystem.GetLines가 채운다, 목록 재사용 가능)</summary>
    public sealed class AchievementLineStatus
    {
        /// <summary>보여 줄 단계 (가장 낮은 안 받은 단계, 다 받았으면 마지막 단계)</summary>
        public AchievementDef Current;
        /// <summary>Current의 줄 안 순서 (0부터)</summary>
        public int TierIndex;
        public int TierCount;
        /// <summary>조건 현재 값 · 목표 (ProgressTracker.GetValue / GetTarget)</summary>
        public long Value;
        public long Target;
        public bool Claimable;
        public bool AllDone;
        /// <summary>숨은 기록이고 아직 찾지 못함 ('???' + 힌트)</summary>
        public bool HiddenUnrevealed;
    }

    /// <summary>
    /// 수선점 기록 (Q-04, SPEC4 §6.1): 17줄 62단계 (콘텐츠 MetaDatabase.achievements). 진행은 ProgressTracker.GetValue라 설치 때부터 쌓이고,
    /// 받기는 FeatureId.Achievements(2-3) 개방 뒤 · 시계 되돌림(DailyClock.IsTampered) 아닐 때만. 그래서 처음 열면 앞 단계 여러 개를 한꺼번에 받는다.
    /// 한 줄은 낮은 단계부터 하나씩 받는다 (TryClaim은 그 줄의 가장 낮은 안 받은 단계만). 단, 단계가 모두 숨은 기록인 줄(ach_hidden — 플래그 5개가
    /// 서로 따로)은 찬 단계를 순서와 상관없이 받는다. 받기 = MetaRewards.Grant + CounterKey.AchievementClaim.
    /// <para>
    /// 숨은 기록 플래그 (ProgressTracker.SetFlag, 조건은 MetaConditionType.Flag):
    /// h1 ach.hidden.rain_udu 비 스테이지를 우두커니가 지킨 채 클리어 · h2 ach.hidden.clean_boss 챕터 보스(x-10)를 수선점이 다치지 않고 클리어 ·
    /// h3 ach.hidden.three_lamps 한 스테이지에서 불 밝히기 3번 · h4 ach.hidden.all_socks 짝짝이 형태 11종 해금 ·
    /// h5 ach.hidden.midnight 현지 00:00~05:00에 출석 도장. 빨간 점 RedDotPaths.JournalRecords = 받을 단계 수.
    /// </para>
    /// </summary>
    public class AchievementSystem : IMetaModule
    {
        /// <summary>받은 단계 · 받을 수 있는 수가 바뀜</summary>
        public event Action Changed;

        public const string FlagRainUdu = "ach.hidden.rain_udu";
        public const string FlagCleanBoss = "ach.hidden.clean_boss";
        public const string FlagThreeLamps = "ach.hidden.three_lamps";
        public const string FlagAllSocks = "ach.hidden.all_socks";
        public const string FlagMidnight = "ach.hidden.midnight";

        /// <summary>h1의 우두커니 계보 · h4의 짝짝이 계보와 형태 수 (콘텐츠 CharacterContent)</summary>
        public const string UdukeoniLineageId = "udukeoni";
        public const string JjakjjakLineageId = "jjakjjak";
        public const int JjakjjakFormCount = 11;
        /// <summary>h3: 한 스테이지에서 불 밝히기 이 횟수</summary>
        public const int ThreeLampsCount = 3;
        /// <summary>h5: 현지 이 시각 전 (자정부터)</summary>
        public const int MidnightEndHour = 5;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;
        readonly CodexSystem _codex;
        readonly BattleSystem _battle;
        readonly DailyClock _clock;

        bool _connected;
        bool _countDirty = true;
        int _claimable;
        bool _countedCanClaim;
        RedDotSystem _dots;

        // 한 스테이지 안의 기록 (h2 · h3) — 전투가 시작될 때마다 비운다
        int _stageShopDamage;
        int _stageLamps;

        public AchievementSystem(PlayerData data, MetaDatabase db, ProgressTracker progress, MetaRewardService rewards, FeatureGateSystem features,
            CodexSystem codex, BattleSystem battle, DailyClock clock)
        {
            _data = data;
            _db = db;
            _progress = progress;
            _rewards = rewards;
            _features = features;
            _codex = codex;
            _battle = battle;
            _clock = clock;
        }

        AchievementSave Save
        {
            get
            {
                if (_data == null) return null;
                _data.meta ??= new MetaSave();
                _data.meta.achievements ??= new AchievementSave();
                _data.meta.achievements.Sanitize();
                return _data.meta.achievements;
            }
        }

        /// <summary>기록을 받을 수 있는 기능이 열렸는지 (FeatureId.Achievements 2-3, 정의가 없으면 열림)</summary>
        public bool IsUnlocked
        {
            get
            {
                if (_features == null) return true;
                try
                {
                    return _features.IsUnlocked(FeatureId.Achievements);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[Achievements] 기능 개방 확인 실패: {e.Message}");
                    return false;
                }
            }
        }

        /// <summary>지금 받을 수 있는지 (열림 · 시계 되돌림 아님)</summary>
        bool CanClaim => IsUnlocked && (_clock == null || !_clock.IsTampered);

        /// <summary>줄마다 하나씩 채운다 (into는 비우고 채움). 순서: 받을 것 → 진행 중(비율 높은 순) → 완료 → 숨은 기록(못 찾음)</summary>
        public void GetLines(List<AchievementLineStatus> into)
        {
            if (into == null) return;
            into.Clear();
            if (_db == null) return;
            var save = Save;
            bool canClaim = CanClaim;
            var lineIds = _db.AchievementLineIds;
            var order = new List<(AchievementLineStatus status, int group, float ratio, int index)>();
            for (int i = 0; i < lineIds.Count; i++)
            {
                var tiers = _db.GetAchievementLine(lineIds[i]);
                if (tiers == null || tiers.Count == 0) continue;
                bool independent = IsIndependentLine(tiers);
                int current = independent ? FirstClaimable(tiers, save) : -1;
                if (current < 0) current = LowestUnclaimed(tiers, save);
                bool allDone = current < 0;
                int index = allDone ? tiers.Count - 1 : current;
                var def = tiers[index];
                if (def == null) continue;
                bool met = IsMet(def);
                var status = new AchievementLineStatus
                {
                    Current = def,
                    TierIndex = index,
                    TierCount = tiers.Count,
                    Value = _progress != null ? _progress.GetValue(def.condition) : 0,
                    Target = _progress != null ? _progress.GetTarget(def.condition) : 1,
                    AllDone = allDone,
                    Claimable = !allDone && canClaim && met,
                    HiddenUnrevealed = def.hidden && !allDone && !met,
                };
                int group = status.Claimable ? 0 : status.HiddenUnrevealed ? 3 : allDone ? 2 : 1;
                float ratio = _progress != null ? _progress.GetRatio(def.condition) : 0f;
                order.Add((status, group, ratio, i));
            }
            order.Sort((a, b) =>
            {
                if (a.group != b.group) return a.group.CompareTo(b.group);
                if (a.group == 1 && a.ratio != b.ratio) return b.ratio.CompareTo(a.ratio);
                return a.index.CompareTo(b.index);
            });
            foreach (var item in order) into.Add(item.status);
        }

        /// <summary>지금 받을 수 있는 단계 수 (줄마다 여러 단계가 한꺼번에 찰 수 있다). 잠겼거나 시계를 되돌렸으면 0</summary>
        public int ClaimableCount
        {
            get
            {
                bool canClaim = CanClaim;
                if (!_countDirty && canClaim == _countedCanClaim) return _claimable;
                _countDirty = false;
                _countedCanClaim = canClaim;
                _claimable = canClaim ? CountClaimable() : 0;
                return _claimable;
            }
        }

        /// <summary>한 단계를 받는다 (그 줄의 가장 낮은 안 받은 단계여야 한다). 잠겼거나 받을 수 없으면 null</summary>
        public List<MetaReward> TryClaim(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId) || _db == null || !CanClaim) return null;
            var def = _db.GetAchievement(achievementId);
            var save = Save;
            if (def == null || save == null || save.claimed.Contains(def.id)) return null;
            var tiers = _db.GetAchievementLine(def.lineId);
            if (!IsMet(def)) return null;
            if (!IsIndependentLine(tiers))
            {
                int current = LowestUnclaimed(tiers, save);
                if (current < 0 || tiers[current] != def) return null;
            }
            save.claimed.Add(def.id);
            var granted = Grant(def.rewards);
            _progress?.Increment(CounterKey.AchievementClaim);
            AfterClaims();
            return granted;
        }

        /// <summary>받을 단계를 줄마다 단계 순서대로 모두 받는다 (보상은 합쳐서 한 번에, 받은 것이 없으면 빈 목록)</summary>
        public List<MetaReward> ClaimAll()
        {
            var rewards = new List<MetaReward>();
            if (_db == null || !CanClaim) return rewards;
            var save = Save;
            if (save == null) return rewards;
            int claimed = 0;
            var lineIds = _db.AchievementLineIds;
            for (int i = 0; i < lineIds.Count; i++)
            {
                var tiers = _db.GetAchievementLine(lineIds[i]);
                bool independent = IsIndependentLine(tiers);
                for (int t = 0; t < tiers.Count; t++)
                {
                    var def = tiers[t];
                    if (def == null || save.claimed.Contains(def.id)) continue;
                    if (!IsMet(def))
                    {
                        if (independent) continue;
                        break; // 낮은 단계부터 하나씩 — 못 채운 단계에서 그 줄은 멈춘다
                    }
                    save.claimed.Add(def.id);
                    if (def.rewards != null) rewards.AddRange(def.rewards);
                    claimed++;
                }
            }
            if (claimed == 0) return rewards;
            var granted = Grant(MetaRewardService.Merge(rewards));
            _progress?.Increment(CounterKey.AchievementClaim, claimed);
            AfterClaims();
            return granted;
        }

        /// <summary>받은 단계 수 (요약 카드 '모은 기록 {n}/{62}')</summary>
        public int ClaimedCount
        {
            get
            {
                var save = Save;
                return save != null ? save.claimed.Count : 0;
            }
        }

        /// <summary>전체 단계 수 (콘텐츠)</summary>
        public int TierCount => _db != null && _db.achievements != null ? _db.achievements.Count : 0;

        // ───────────────────────────── IMetaModule ─────────────────────────────

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_progress != null)
            {
                _progress.Changed += MarkCountDirty;
                _progress.Incremented += OnCounterIncremented;
            }
            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
            if (_codex != null) _codex.Changed += CheckAllSocks;
            if (_battle != null)
            {
                _battle.Event += OnBattleEvent;
                _battle.StageChanged += ResetStageRecord;
                _battle.StageEnded += OnStageEnded;
            }
            CheckAllSocks();
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
        }

        public void OnDayChanged(string dayKey) => MarkCountDirty();

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            _dots = dots;
            dots?.RegisterCount(RedDotPaths.JournalRecords, () => ClaimableCount);
        }

        // ───────────────────────────── 숨은 기록 ─────────────────────────────

        /// <summary>전투 이벤트: 수선점 피해 · 불 밝히기를 이번 스테이지에 센다 (할당 없음)</summary>
        internal void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.ShopDamaged:
                    _stageShopDamage++;
                    break;
                case BattleEventType.LampLit:
                    _stageLamps++;
                    if (_stageLamps >= ThreeLampsCount) SetFlag(FlagThreeLamps);
                    break;
            }
        }

        /// <summary>새 스테이지가 시작됨 → 이번 스테이지 기록을 비운다</summary>
        internal void ResetStageRecord()
        {
            _stageShopDamage = 0;
            _stageLamps = 0;
        }

        /// <summary>스테이지 끝: h1 (비 + 우두커니 지킴이) · h2 (챕터 보스, 수선점 피해 0). 그 뒤 기록을 비운다</summary>
        internal void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            try
            {
                if (outcome != BattleOutcome.Cleared) return;
                var sim = _battle?.Sim;
                int stage = sim != null ? Math.Max(1, sim.Level) : Math.Max(1, _battle?.Stage ?? 1);
                if (stage % 10 == 0 && _stageShopDamage == 0) SetFlag(FlagCleanBoss);
                if (_battle != null && _battle.GetStageModifier(stage) == StageModifierType.Rain && HasGuardianOf(sim, UdukeoniLineageId))
                    SetFlag(FlagRainUdu);
            }
            finally
            {
                ResetStageRecord();
            }
        }

        static bool HasGuardianOf(BattleSimulator sim, string lineageId)
        {
            var guardians = sim?.Guardians;
            if (guardians == null) return false;
            for (int i = 0; i < guardians.Count; i++)
            {
                var guardian = guardians[i];
                if (guardian != null && guardian.LineageId == lineageId) return true;
            }
            return false;
        }

        /// <summary>h5: 출석 도장이 현지 00:00~05:00이면</summary>
        void OnCounterIncremented(CounterKey key, long amount)
        {
            if (key != CounterKey.AttendanceCheckIn || _clock == null) return;
            if (_clock.ToLocal(_clock.NowUnix).Hour < MidnightEndHour) SetFlag(FlagMidnight);
        }

        /// <summary>h4: 짝짝이 형태를 모두(11종) 해금 (기본형 포함)</summary>
        void CheckAllSocks()
        {
            if (_data?.lineages == null || !_data.lineages.TryGetValue(JjakjjakLineageId, out var save) || save?.unlockedForms == null) return;
            if (save.unlockedForms.Count >= JjakjjakFormCount) SetFlag(FlagAllSocks);
        }

        void SetFlag(string flag)
        {
            if (_progress == null || _progress.HasFlag(flag)) return;
            if (_progress.SetFlag(flag)) MarkCountDirty();
        }

        // ───────────────────────────── 계산 ─────────────────────────────

        bool IsMet(AchievementDef def) => def != null && (_progress == null ? false : _progress.IsMet(def.condition));

        /// <summary>그 줄의 가장 낮은 안 받은 단계 (다 받았으면 -1)</summary>
        static int LowestUnclaimed(IReadOnlyList<AchievementDef> tiers, AchievementSave save)
        {
            for (int t = 0; t < tiers.Count; t++)
            {
                var def = tiers[t];
                if (def != null && (save == null || !save.claimed.Contains(def.id))) return t;
            }
            return -1;
        }

        /// <summary>단계가 모두 숨은 기록인 줄 (서로 따로 받는다 — ach_hidden)</summary>
        static bool IsIndependentLine(IReadOnlyList<AchievementDef> tiers)
        {
            if (tiers == null || tiers.Count == 0) return false;
            for (int t = 0; t < tiers.Count; t++)
            {
                if (tiers[t] == null || !tiers[t].hidden) return false;
            }
            return true;
        }

        /// <summary>그 줄에서 찼지만 안 받은 첫 단계 (없으면 -1)</summary>
        int FirstClaimable(IReadOnlyList<AchievementDef> tiers, AchievementSave save)
        {
            for (int t = 0; t < tiers.Count; t++)
            {
                var def = tiers[t];
                if (def != null && (save == null || !save.claimed.Contains(def.id)) && IsMet(def)) return t;
            }
            return -1;
        }

        /// <summary>받을 단계 수 (줄마다 낮은 단계부터 이어서 찬 만큼). 할당 없음</summary>
        int CountClaimable()
        {
            if (_db == null) return 0;
            var save = Save;
            int count = 0;
            var lineIds = _db.AchievementLineIds;
            for (int i = 0; i < lineIds.Count; i++)
            {
                var tiers = _db.GetAchievementLine(lineIds[i]);
                bool independent = IsIndependentLine(tiers);
                for (int t = 0; t < tiers.Count; t++)
                {
                    var def = tiers[t];
                    if (def == null || (save != null && save.claimed.Contains(def.id))) continue;
                    if (!IsMet(def))
                    {
                        if (independent) continue;
                        break;
                    }
                    count++;
                }
            }
            return count;
        }

        void MarkCountDirty()
        {
            _countDirty = true;
        }

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature != FeatureId.Achievements) return;
            MarkCountDirty();
            _dots?.MarkDirty(RedDotPaths.JournalRecords);
            Changed?.Invoke();
        }

        void AfterClaims()
        {
            MarkCountDirty();
            _dots?.MarkDirty(RedDotPaths.JournalRecords);
            Changed?.Invoke();
        }

        List<MetaReward> Grant(List<MetaReward> rewards)
        {
            if (rewards == null || rewards.Count == 0) return new List<MetaReward>();
            if (_rewards == null) return new List<MetaReward>(rewards);
            return _rewards.Grant(rewards) ?? new List<MetaReward>();
        }
    }
}
