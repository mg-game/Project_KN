using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>미션 한 줄의 현재 상태 (MissionSystem.GetMissions가 채운다, 목록 재사용 가능)</summary>
    public sealed class MissionStatus
    {
        public MissionDef Def;
        /// <summary>오늘(일일) / 이번 주(주간) 카운터 값 (목표에서 자름)</summary>
        public long Progress;
        public bool IsComplete;
        public bool IsClaimed;
        public bool CanClaim => IsComplete && !IsClaimed;
        public float Ratio => Def == null || Def.target <= 0 ? 1f : Math.Min(1f, (float)Progress / Def.target);
    }

    /// <summary>점수 상자 하나의 상태</summary>
    public sealed class MissionChestStatus
    {
        public MissionChestDef Def;
        /// <summary>MetaDatabase.GetMissionChests(period) 인덱스 (TryClaimChest 인자)</summary>
        public int Index;
        public bool IsReached;
        public bool IsClaimed;
        public bool CanClaim => IsReached && !IsClaimed;
    }

    /// <summary>
    /// 수선 일지 — 일일·주간 미션 (Q-02).
    ///   · 진행도 = 미션 카운터의 오늘/이번 주 값 (ProgressTracker). 받은 미션과 점수, 받은 상자는 meta.missions에 주기 키와 함께 저장.
    ///     주기 키가 DailyClock과 다르면(날·주가 바뀜) 비운다 (DayChanged/WeekChanged + 읽을 때 확인).
    ///   · 미션을 받으면 points만큼 일지 점수 + MissionClaim 카운터. 일일 점수가 처음 100 이상이 되면 JournalComplete 카운터 (주간 "일지 100점 5일").
    ///   · requiredFeature가 잠긴 미션은 숨기고 그 주기 총점에서 뺀다 (A4: 1-7에서 열릴 때 던전·상점 미션 숨김).
    ///   · 점수 상자(20/40/60/80/100)는 점수 도달 시 받기. 되돌림(IsTampered) 중에는 받기 막음 (DailyClock.TamperedMessage).
    ///   · 수선 일지(FeatureId.Missions)가 잠겨 있으면 받기를 막는다 (진행도는 계속 쌓임).
    /// <para>
    /// Changed는 받은 것이 바뀔 때, 주기가 바뀔 때, 그리고 Progress.Changed 중 보이는 미션의 진행(목표에서 자른 값)·개수가
    /// 실제로 바뀐 때만 울린다 (전투 중 처치마다 일지 화면이 다시 그려지지 않게, 할당 없이 비교).
    /// 받은 미션 표시는 숨겨져도 지워지지 않는다. GetClaimableCount / TotalClaimableCount는 일지가 잠겼거나 되돌림 중이면 0.
    /// </para>
    /// </summary>
    public class MissionSystem
    {
        /// <summary>미션/상자 상태가 바뀜 (진행도 변화는 Progress.Changed를 받아 프레임당 최대 1번)</summary>
        public event Action Changed;

        /// <summary>일일 점수가 이 값에 처음 닿으면 JournalComplete (주간 "일지 100점 5일")</summary>
        public const int JournalCompletePoints = 100;

        static readonly MissionPeriod[] Periods = { MissionPeriod.Daily, MissionPeriod.Weekly };

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;

        long _lastSignature = long.MinValue;

        public MissionSystem(PlayerData data, MetaDatabase db, DailyClock clock, ProgressTracker progress, MetaRewardService rewards,
            FeatureGateSystem features)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _progress = progress;
            _rewards = rewards;
            _features = features;

            if (_clock != null)
            {
                _clock.DayChanged += OnDayChanged;
                _clock.WeekChanged += OnWeekChanged;
                _clock.TamperChanged += OnTamperChanged;
            }
            if (_progress != null) _progress.Changed += OnProgressChanged;
            if (_features != null) _features.Changed += OnProgressChanged;
        }

        /// <summary>수선 일지가 열렸는지 (FeatureId.Missions)</summary>
        public bool IsUnlocked => _features == null || _features.IsUnlocked(FeatureId.Missions);

        /// <summary>받기가 막힌 이유 (잠김/되돌림). 받을 수 있으면 빈 문자열 — 받기 버튼 토스트용</summary>
        public string BlockedReason
        {
            get
            {
                if (!IsUnlocked) return _features != null ? _features.GetRequirementText(FeatureId.Missions) : string.Empty;
                if (_clock != null && _clock.IsTampered) return DailyClock.TamperedMessage;
                return string.Empty;
            }
        }

        bool CanClaimNow => IsUnlocked && (_clock == null || !_clock.IsTampered);

        /// <summary>
        /// 보이는 미션 상태를 콘텐츠 순서로 채운다. into에 이미 있는 MissionStatus 객체는 덮어써서 재사용하고
        /// (모자라면 새로 만들고, 남으면 잘라 냄) — UI는 같은 목록을 계속 넘겨 할당을 피한다.
        /// </summary>
        public void GetMissions(MissionPeriod period, List<MissionStatus> into)
        {
            if (into == null) return;
            int n = 0;
            if (_db != null)
            {
                var save = GetPeriodSave(period);
                var defs = _db.GetMissions(period);
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (!IsVisible(def)) continue;
                    MissionStatus status;
                    if (n < into.Count)
                    {
                        status = into[n] ?? (into[n] = new MissionStatus());
                    }
                    else
                    {
                        status = new MissionStatus();
                        into.Add(status);
                    }
                    Fill(status, def, save);
                    n++;
                }
            }
            if (into.Count > n) into.RemoveRange(n, into.Count - n);
        }

        /// <summary>미션 하나의 상태 (없으면 null). 새 객체를 만든다 — 목록 표시는 GetMissions를 쓸 것</summary>
        public MissionStatus GetStatus(string missionId)
        {
            var def = _db != null ? _db.GetMission(missionId) : null;
            if (def == null) return null;
            var status = new MissionStatus();
            Fill(status, def, GetPeriodSave(def.period));
            return status;
        }

        /// <summary>이 주기에 모은 일지 점수</summary>
        public int GetPoints(MissionPeriod period) => GetPeriodSave(period).points;

        /// <summary>이 주기에 얻을 수 있는 총점 (보이는 미션 합)</summary>
        public int GetMaxPoints(MissionPeriod period)
        {
            if (_db == null) return 0;
            int total = 0;
            var defs = _db.GetMissions(period);
            for (int i = 0; i < defs.Count; i++)
            {
                if (IsVisible(defs[i])) total += Math.Max(0, defs[i].points);
            }
            return total;
        }

        /// <summary>점수 막대 끝 값 (가장 큰 상자 점수, 보통 100)</summary>
        public int GetBarMax(MissionPeriod period)
        {
            var chests = _db != null ? _db.GetMissionChests(period) : null;
            if (chests == null || chests.Count == 0) return Math.Max(1, Math.Min(JournalCompletePoints, GetMaxPoints(period)));
            return Math.Max(1, chests[chests.Count - 1].points);
        }

        /// <summary>상자 상태를 점수 오름차순으로 채운다 (GetMissions와 같은 재사용 규칙)</summary>
        public void GetChests(MissionPeriod period, List<MissionChestStatus> into)
        {
            if (into == null) return;
            int n = 0;
            if (_db != null)
            {
                var save = GetPeriodSave(period);
                var chests = _db.GetMissionChests(period);
                for (int i = 0; i < chests.Count; i++)
                {
                    MissionChestStatus status;
                    if (n < into.Count)
                    {
                        status = into[n] ?? (into[n] = new MissionChestStatus());
                    }
                    else
                    {
                        status = new MissionChestStatus();
                        into.Add(status);
                    }
                    status.Def = chests[i];
                    status.Index = i;
                    status.IsReached = save.points >= chests[i].points;
                    status.IsClaimed = save.chestsClaimed.Contains(i);
                    n++;
                }
            }
            if (into.Count > n) into.RemoveRange(n, into.Count - n);
        }

        /// <summary>받을 수 있는 미션 + 상자 수 (상단 바 숫자 배지)</summary>
        public int GetClaimableCount(MissionPeriod period)
        {
            if (_db == null || !CanClaimNow) return 0;
            var save = GetPeriodSave(period);
            int count = 0;
            var defs = _db.GetMissions(period);
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (IsVisible(def) && !save.claimed.Contains(def.id) && CounterValue(def) >= Math.Max(1, def.target)) count++;
            }
            var chests = _db.GetMissionChests(period);
            for (int i = 0; i < chests.Count; i++)
            {
                if (save.points >= chests[i].points && !save.chestsClaimed.Contains(i)) count++;
            }
            return count;
        }

        public int TotalClaimableCount => GetClaimableCount(MissionPeriod.Daily) + GetClaimableCount(MissionPeriod.Weekly);

        /// <summary>미션 하나를 받는다. 점수와 (있으면) 보상을 준다. granted = 받은 보상 (점수 제외, 비어 있을 수 있음)</summary>
        public bool TryClaim(string missionId, out List<MetaReward> granted)
        {
            granted = new List<MetaReward>();
            if (!ClaimMission(missionId, granted)) return false;
            granted = MetaRewardService.Merge(granted);
            NotifyChanged();
            return true;
        }

        /// <summary>상자 하나를 받는다 (index = GetChests의 Index)</summary>
        public bool TryClaimChest(MissionPeriod period, int index, out List<MetaReward> granted)
        {
            granted = new List<MetaReward>();
            if (!ClaimChest(period, index, granted)) return false;
            granted = MetaRewardService.Merge(granted);
            NotifyChanged();
            return true;
        }

        /// <summary>[모두 받기]: 이 주기의 받을 수 있는 미션을 모두 받은 뒤, 도달한 상자도 모두 받는다. 받은 보상 합계 (같은 종류 합침)</summary>
        public List<MetaReward> ClaimAll(MissionPeriod period)
        {
            var all = new List<MetaReward>();
            if (_db == null || !CanClaimNow) return all;
            bool any = false;

            var defs = _db.GetMissions(period);
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null && ClaimMission(defs[i].id, all)) any = true;
            }
            var chests = _db.GetMissionChests(period);
            for (int i = 0; i < chests.Count; i++)
            {
                if (ClaimChest(period, i, all)) any = true;
            }
            if (any) NotifyChanged();
            return MetaRewardService.Merge(all);
        }

        /// <summary>초기화까지 남은 시간 (일일 → 다음 05:00, 주간 → 다음 월요일 05:00)</summary>
        public TimeSpan GetTimeUntilReset(MissionPeriod period)
        {
            if (_clock == null) return TimeSpan.Zero;
            return period == MissionPeriod.Weekly ? _clock.TimeUntilWeeklyReset : _clock.TimeUntilDailyReset;
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>그 주기의 기록. 저장된 주기 키가 지금과 다르면 비운다</summary>
        MissionPeriodSave GetPeriodSave(MissionPeriod period)
        {
            var missions = (_data.meta ??= new MetaSave()).missions ??= new MissionSave();
            missions.daily ??= new MissionPeriodSave();
            missions.weekly ??= new MissionPeriodSave();
            var save = missions.Get(period);
            save.claimed ??= new HashSet<string>();
            save.chestsClaimed ??= new HashSet<int>();
            if (_clock == null) return save;
            string key = period == MissionPeriod.Weekly ? _clock.WeekKey : _clock.DayKey;
            if (save.periodKey != key) save.Reset(key);
            else if (!ReferenceEquals(save.periodKey, key)) save.periodKey = key;
            return save;
        }

        bool IsVisible(MissionDef def) =>
            def != null && !string.IsNullOrEmpty(def.id)
            && (def.requiredFeature == FeatureId.Battle || _features == null || _features.IsUnlocked(def.requiredFeature));

        long CounterValue(MissionDef def)
        {
            if (_progress == null) return 0;
            return def.period == MissionPeriod.Weekly ? _progress.GetWeekly(def.counter) : _progress.GetDaily(def.counter);
        }

        void Fill(MissionStatus status, MissionDef def, MissionPeriodSave save)
        {
            long target = Math.Max(1, def.target);
            long value = CounterValue(def);
            status.Def = def;
            status.Progress = Math.Min(target, Math.Max(0, value));
            status.IsComplete = value >= target;
            status.IsClaimed = save.claimed.Contains(def.id);
        }

        /// <summary>미션 받기 본체 (Changed 없음). 보상은 into에 더한다</summary>
        bool ClaimMission(string missionId, List<MetaReward> into)
        {
            if (_db == null || !CanClaimNow) return false;
            var def = _db.GetMission(missionId);
            if (!IsVisible(def)) return false;
            var save = GetPeriodSave(def.period);
            if (save.claimed.Contains(def.id) || CounterValue(def) < Math.Max(1, def.target)) return false;

            save.claimed.Add(def.id);
            int before = save.points;
            save.points = Math.Max(0, before + Math.Max(0, def.points));
            if (def.rewards != null && def.rewards.Count > 0 && _rewards != null) into.AddRange(_rewards.Grant(def.rewards));

            _progress?.Increment(CounterKey.MissionClaim);
            // 4차 훅 (SPEC4 §2.5): 일일 미션으로 받은 일지 점수 = 별빛 산책 수첩 경험치 (Q-06, 기록은 지금부터 — 쓰는 것은 P4-2)
            if (def.period == MissionPeriod.Daily && def.points > 0) _progress?.Increment(CounterKey.JournalPoints, def.points);
            if (def.period == MissionPeriod.Daily && before < JournalCompletePoints && save.points >= JournalCompletePoints)
                _progress?.Increment(CounterKey.JournalComplete);
            _progress?.MarkDirty();
            return true;
        }

        /// <summary>상자 받기 본체 (Changed 없음). 받으면 플레이 기록 'mission_box' {period, pts} 한 줄 (SPEC4 §4.4 — 기록이 닫혀 있으면 아무것도 안 함)</summary>
        bool ClaimChest(MissionPeriod period, int index, List<MetaReward> into)
        {
            if (_db == null || !CanClaimNow) return false;
            var chests = _db.GetMissionChests(period);
            if (index < 0 || index >= chests.Count) return false;
            var chest = chests[index];
            if (chest == null) return false;
            var save = GetPeriodSave(period);
            if (save.points < chest.points || save.chestsClaimed.Contains(index)) return false;

            save.chestsClaimed.Add(index);
            if (chest.rewards != null && chest.rewards.Count > 0 && _rewards != null) into.AddRange(_rewards.Grant(chest.rewards));
            _progress?.MarkDirty();
            PlayLog.Begin("mission_box").Str("period", period == MissionPeriod.Weekly ? "weekly" : "daily").Int("pts", chest.points).End();
            return true;
        }

        void OnDayChanged(string dayKey)
        {
            var missions = _data.meta?.missions;
            if (missions?.daily != null && missions.daily.periodKey != dayKey) missions.daily.Reset(dayKey);
            NotifyChanged();
        }

        void OnWeekChanged(string weekKey)
        {
            var missions = _data.meta?.missions;
            if (missions?.weekly != null && missions.weekly.periodKey != weekKey) missions.weekly.Reset(weekKey);
            NotifyChanged();
        }

        /// <summary>되돌림이 생기거나 풀리면 받기 가능 여부가 바뀐다</summary>
        void OnTamperChanged(bool tampered) => NotifyChanged();

        /// <summary>진행 서명이 바뀌었을 때만 Changed (할당 없음)</summary>
        void OnProgressChanged()
        {
            long signature = ComputeSignature();
            if (signature == _lastSignature) return;
            _lastSignature = signature;
            Changed?.Invoke();
        }

        void NotifyChanged()
        {
            _lastSignature = ComputeSignature();
            Changed?.Invoke();
        }

        /// <summary>보이는 미션의 (자른) 진행 합 + 보이는 수 + 받을 수 있는 수 + 점수 + 잠김/되돌림 — 화면에 보이는 것이 바뀌었는지 가늠</summary>
        long ComputeSignature()
        {
            if (_db == null) return 0;
            long signature = IsUnlocked ? 1 : 0;
            if (_clock != null && _clock.IsTampered) signature += 2;
            for (int p = 0; p < Periods.Length; p++)
            {
                var period = Periods[p];
                var save = GetPeriodSave(period);
                var defs = _db.GetMissions(period);
                long progress = 0;
                int visible = 0;
                int done = 0;
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (!IsVisible(def)) continue;
                    visible++;
                    long target = Math.Max(1, def.target);
                    long value = Math.Min(target, Math.Max(0, CounterValue(def)));
                    progress += value;
                    if (value >= target) done++;
                }
                unchecked
                {
                    signature = signature * 1_000_003 + progress;
                    signature = signature * 1009 + visible;
                    signature = signature * 1009 + done;
                    signature = signature * 1009 + save.points;
                }
            }
            return signature;
        }
    }
}
