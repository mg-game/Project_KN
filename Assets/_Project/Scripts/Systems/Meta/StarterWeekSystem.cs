using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>첫 주 과제 하나의 상태 (StarterWeekSystem.GetTasks가 채운다, 목록 재사용 가능)</summary>
    public sealed class StarterTaskStatus
    {
        public StarterTaskDef Def;
        /// <summary>조건을 한 번 넘어 완료로 굳음 (starterWeek.done)</summary>
        public bool Done;
        public bool Claimed;
        /// <summary>그 과제의 기능이 아직 잠김 (잠금 표시, 창 안에서 나중에 받을 수 있음)</summary>
        public bool Locked;
        /// <summary>조건 현재 값 · 목표 (완료로 굳었으면 값 = 목표)</summary>
        public long Value;
        public long Target;
        public bool CanClaim => Done && !Claimed && !Locked;
    }

    /// <summary>
    /// 첫 주 (NEW-01, SPEC4 §13.6): 수선 일지(1-7)가 열릴 때 설치 7일째 이하면 시작, 날마다 과제 4개 (7일), 14일 창, 점수 상자 30/60/90/120/150.
    /// 창이 끝나면 받지 않은 보상은 자동으로 받는다. 되돌림 중에는 받기를 막는다.
    /// <para>
    /// 시작: 수선 일지(FeatureId.Missions)가 열려 있고 아직 시작 · 거절 전이면, 그때 설치 일수(DailyClock.InstallDay) ≤ maxInstallDays면
    /// startDayKey = 오늘 (Started 알림 — 토스트 '수선 일지에 「첫 주」가 열렸어요'), 아니면 declined = true (옛 세이브는 영영 보지 않는다).
    /// Connect · AfterStartup · Missions 개방에서 확인한다. 콘텐츠(tasks)가 비어 있으면 시작도 거절도 하지 않는다 (확장 전 콘텐츠 끄개).
    /// </para>
    /// <para>
    /// 날: day일째 과제는 시작 날 키부터 d − 1번째 날 키에 열린다 (05:00 경계). 창 = windowDays(14)개의 날 키 동안 모두 받을 수 있다.
    /// 완료: 열린 날의 과제 조건(ProgressTracker.IsMet — 평생 카운터 · 상태 조건 · 오늘의 일지 점수)이 처음 참이 되면 done에 굳는다
    /// (Progress.Changed마다 — 프레임당 한 번, 이미 모두 굳었으면 바로 끝). 기능이 잠긴 과제(requiredFeature)도 굳지만, 받기는 열린 뒤에 (창 안에서).
    /// 점수 = 받은 과제 점수 합 (5점, 7일째 15점). 상자 = 점수 ≥ chest.points.
    /// 끝: 창이 지나면(날 키 기준) 굳은 과제 · 닿은 상자 중 받지 않은 것을 자동으로 받고(우편은 메타 보관함 보상을 못 싣는다) finished = true,
    /// AutoGranted로 알린다 (토스트 '첫 주의 남은 선물을 받았어요'). 빨간 점 RedDotPaths.JournalStarter.
    /// </para>
    /// </summary>
    public class StarterWeekSystem : IMetaModule
    {
        /// <summary>과제 · 점수 · 상자 상태가 바뀜</summary>
        public event Action Changed;
        /// <summary>첫 주가 막 시작됨 (토스트 '수선 일지에 「첫 주」가 열렸어요' — 화면이 스테이지 사이에 한 번 띄운다)</summary>
        public event Action Started;
        /// <summary>창이 끝나 남은 보상을 자동으로 받음 (받은 목록 — 토스트 '첫 주의 남은 선물을 받았어요')</summary>
        public event Action<List<MetaReward>> AutoGranted;

        /// <summary>과제가 열리는 마지막 날 (콘텐츠 7일)</summary>
        public const int TaskDays = 7;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;

        RedDotSystem _dots;
        bool _connected;
        /// <summary>열린 날 과제가 모두 굳었는지 (그 날 키 · 열린 날 수에서) — 매 프레임 조건을 훑지 않게</summary>
        int _allDoneOpenDays = -1;

        public StarterWeekSystem(PlayerData data, MetaDatabase db, DailyClock clock, ProgressTracker progress, MetaRewardService rewards,
            FeatureGateSystem features)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _progress = progress;
            _rewards = rewards;
            _features = features;
        }

        StarterWeekSave Save
        {
            get
            {
                var save = (_data.meta ??= new MetaSave()).starterWeek ??= new StarterWeekSave();
                save.done ??= new HashSet<string>();
                save.claimed ??= new HashSet<string>();
                save.chests ??= new HashSet<int>();
                return save;
            }
        }

        StarterWeekConfig Cfg => _db != null ? _db.starterWeek : null;

        bool HasContent => Cfg?.tasks != null && Cfg.tasks.Count > 0 && _clock != null;

        int WindowDays => Math.Max(1, Cfg?.windowDays ?? 14);

        bool Tampered => _clock != null && _clock.IsTampered;

        /// <summary>시작 날 키부터 오늘까지 지난 날 수 (0 = 시작한 날, 시작 전이면 -1)</summary>
        int Offset
        {
            get
            {
                var save = Save;
                if (string.IsNullOrEmpty(save.startDayKey) || _clock == null) return -1;
                return Math.Max(0, DailyClock.DaysBetween(save.startDayKey, _clock.DayKey));
            }
        }

        /// <summary>시작했는지 (끝났어도 true)</summary>
        public bool IsStarted => !string.IsNullOrEmpty(Save.startDayKey);

        /// <summary>첫 주가 진행 중인지 (시작했고 14일 창이 끝나지 않음)</summary>
        public bool IsActive
        {
            get
            {
                if (!HasContent) return false;
                var save = Save;
                if (save.finished || save.declined || string.IsNullOrEmpty(save.startDayKey)) return false;
                int offset = Offset;
                return offset >= 0 && offset < WindowDays;
            }
        }

        /// <summary>오늘이 몇째 날인지 (1~7 = 과제가 열린 마지막 날, 진행 중이 아니면 0)</summary>
        public int DayIndex => IsActive ? Math.Min(TaskDays, Offset + 1) : 0;

        /// <summary>창이 끝나기까지 남은 날 (오늘 뒤로 남은 날 키 수 — 마지막 날 = 0, 'D-n'). 진행 중이 아니면 0</summary>
        public int DaysLeft => IsActive ? Math.Max(0, WindowDays - 1 - Offset) : 0;

        /// <summary>그 날의 과제가 열렸는지 (진행 중 · 1 ≤ day ≤ 오늘 일차)</summary>
        public bool IsDayOpen(int day) => day >= 1 && day <= DayIndex;

        /// <summary>모은 점수 (받은 과제 점수 합)</summary>
        public int Points
        {
            get
            {
                var save = Save;
                int points = 0;
                foreach (var id in save.claimed)
                {
                    var def = _db?.GetStarterTask(id);
                    if (def != null) points += Math.Max(0, def.points);
                }
                return points;
            }
        }

        /// <summary>점수 상자 (콘텐츠 순서)</summary>
        public IReadOnlyList<StarterChestDef> Chests => (IReadOnlyList<StarterChestDef>)Cfg?.chests ?? Array.Empty<StarterChestDef>();

        /// <summary>그 상자를 받았는지</summary>
        public bool IsChestClaimed(int index) => Save.chests.Contains(index);

        /// <summary>day일째 과제 (1~7, into는 비우고 채움). 열리지 않은 날도 채운다 (잠금 표시는 화면이 IsDayOpen으로)</summary>
        public void GetTasks(int day, List<StarterTaskStatus> into)
        {
            if (into == null) return;
            into.Clear();
            if (!HasContent || _db == null) return;
            var tasks = _db.GetStarterTasks(day);
            var save = Save;
            for (int i = 0; i < tasks.Count; i++)
            {
                var def = tasks[i];
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                bool done = save.done.Contains(def.id);
                long target = _progress != null ? _progress.GetTarget(def.condition) : 1;
                long value = done ? target : (_progress != null ? Math.Min(target, _progress.GetValue(def.condition)) : 0);
                into.Add(new StarterTaskStatus
                {
                    Def = def,
                    Done = done,
                    Claimed = save.claimed.Contains(def.id),
                    Locked = IsLocked(def),
                    Value = value,
                    Target = target,
                });
            }
        }

        /// <summary>과제 하나를 받는다 (점수만 — 보상은 상자). 열린 날 · 완료 · 아직 안 받음 · 기능 열림 · 되돌림 아님. 받으면 빈 목록, 받을 수 없으면 null</summary>
        public List<MetaReward> TryClaimTask(string taskId)
        {
            if (!CanAct()) return null;
            var def = _db.GetStarterTask(taskId);
            if (!CanClaimTask(def)) return null;
            Save.claimed.Add(def.id);
            _progress?.MarkDirty();
            NotifyChanged();
            return new List<MetaReward>();
        }

        /// <summary>점수 상자 하나를 받는다 (index = StarterWeekConfig.chests 순서, 점수가 닿음 · 아직 안 받음 · 되돌림 아님). 받을 수 없으면 null</summary>
        public List<MetaReward> TryClaimChest(int index)
        {
            if (!CanAct() || !CanClaimChest(index, Points)) return null;
            Save.chests.Add(index);
            var granted = Grant(Cfg.chests[index].rewards);
            _progress?.MarkDirty();
            NotifyChanged();
            return granted;
        }

        /// <summary>열린 날의 과제와 상자를 모두 받는다 (받은 것이 없으면 빈 목록)</summary>
        public List<MetaReward> ClaimAll()
        {
            var all = new List<MetaReward>();
            if (!CanAct()) return all;
            bool any = ClaimAllInto(all, includeLocked: false);
            if (any)
            {
                _progress?.MarkDirty();
                NotifyChanged();
            }
            return MetaRewardService.Merge(all);
        }

        /// <summary>받을 과제나 상자가 있는지 (진행 중 · 되돌림 아님). 할당 없음</summary>
        public bool HasClaimable
        {
            get
            {
                if (!CanAct()) return false;
                int open = DayIndex;
                for (int day = 1; day <= open; day++)
                {
                    var tasks = _db.GetStarterTasks(day);
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        if (CanClaimTask(tasks[i])) return true;
                    }
                }
                var chests = Cfg.chests;
                if (chests == null) return false;
                int points = Points;
                for (int i = 0; i < chests.Count; i++)
                {
                    if (CanClaimChest(i, points)) return true;
                }
                return false;
            }
        }

        // ── IMetaModule ──

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_progress != null) _progress.Changed += OnProgressChanged;
            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
            CheckEnd();
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
            TryStart();
            CheckEnd();
            LatchCompleted();
        }

        public void OnDayChanged(string dayKey)
        {
            _allDoneOpenDays = -1;
            if (CheckEnd()) return;
            LatchCompleted();
            NotifyChanged();
        }

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            if (dots == null) return;
            _dots = dots;
            dots.Register(RedDotPaths.JournalStarter, () => HasClaimable);
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        void NotifyChanged()
        {
            _dots?.MarkDirty(RedDotPaths.JournalStarter);
            Changed?.Invoke();
        }

        bool CanAct() => IsActive && !Tampered && _db != null;

        bool IsLocked(StarterTaskDef def) => def != null && _features != null && !_features.IsUnlocked(def.requiredFeature);

        bool CanClaimTask(StarterTaskDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id) || !IsDayOpen(def.day)) return false;
            var save = Save;
            return save.done.Contains(def.id) && !save.claimed.Contains(def.id) && !IsLocked(def);
        }

        bool CanClaimChest(int index, int points)
        {
            var chests = Cfg?.chests;
            if (chests == null || index < 0 || index >= chests.Count || chests[index] == null) return false;
            return points >= chests[index].points && !Save.chests.Contains(index);
        }

        List<MetaReward> Grant(List<MetaReward> rewards)
        {
            if (rewards == null) return new List<MetaReward>();
            return _rewards != null ? _rewards.Grant(rewards) : MetaRewardService.Merge(rewards);
        }

        /// <summary>받을 수 있는 과제 → 상자 순서로 모두 받아 into에 더한다. includeLocked면 기능 잠긴 과제 · 열리지 않은 날도 (창 끝 자동 받기)</summary>
        bool ClaimAllInto(List<MetaReward> into, bool includeLocked)
        {
            var save = Save;
            bool any = false;
            for (int day = 1; day <= TaskDays; day++)
            {
                if (!includeLocked && !IsDayOpen(day)) break;
                var tasks = _db.GetStarterTasks(day);
                for (int i = 0; i < tasks.Count; i++)
                {
                    var def = tasks[i];
                    if (def == null || string.IsNullOrEmpty(def.id) || !save.done.Contains(def.id) || save.claimed.Contains(def.id)) continue;
                    if (!includeLocked && IsLocked(def)) continue;
                    save.claimed.Add(def.id);
                    any = true;
                }
            }
            var chests = Cfg?.chests;
            if (chests != null)
            {
                int points = Points;
                for (int i = 0; i < chests.Count; i++)
                {
                    if (!CanClaimChest(i, points)) continue;
                    save.chests.Add(i);
                    into.AddRange(Grant(chests[i].rewards));
                    any = true;
                }
            }
            return any;
        }

        /// <summary>수선 일지가 열렸으면 시작하거나(설치 일수 ≤ maxInstallDays) 거절한다 (한 번)</summary>
        void TryStart()
        {
            if (!HasContent) return;
            var save = Save;
            if (!string.IsNullOrEmpty(save.startDayKey) || save.declined) return;
            if (_features != null && !_features.IsUnlocked(FeatureId.Missions)) return;
            int maxDays = Math.Max(1, Cfg.maxInstallDays);
            if (_clock.InstallDay > maxDays)
            {
                save.declined = true;
                return;
            }
            save.startDayKey = _clock.DayKey;
            _allDoneOpenDays = -1;
            LatchCompleted();
            Started?.Invoke();
            NotifyChanged();
        }

        /// <summary>창이 지났으면 남은 보상을 자동으로 받고 끝낸다. 끝냈으면 true</summary>
        bool CheckEnd()
        {
            if (!HasContent || _db == null) return false;
            var save = Save;
            if (save.finished || save.declined || string.IsNullOrEmpty(save.startDayKey) || Tampered) return false;
            if (Offset < WindowDays) return false;
            var granted = new List<MetaReward>();
            ClaimAllInto(granted, includeLocked: true);
            save.finished = true;
            var merged = MetaRewardService.Merge(granted);
            _progress?.MarkDirty();
            if (merged.Count > 0) AutoGranted?.Invoke(merged);
            NotifyChanged();
            return true;
        }

        void OnProgressChanged() => LatchCompleted();

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature == FeatureId.Missions) TryStart();
            else if (IsActive) NotifyChanged(); // 기능이 열려 잠긴 과제를 받을 수 있게 됨
        }

        /// <summary>열린 날의 과제 중 조건이 참인 것을 done에 굳힌다. 열린 날 과제가 모두 굳었으면 같은 날 동안 다시 훑지 않는다. 할당 없음</summary>
        void LatchCompleted()
        {
            if (!IsActive || _progress == null) return;
            int open = DayIndex;
            if (open == _allDoneOpenDays) return;
            var save = Save;
            bool allDone = true, changed = false;
            for (int day = 1; day <= open; day++)
            {
                var tasks = _db.GetStarterTasks(day);
                for (int i = 0; i < tasks.Count; i++)
                {
                    var def = tasks[i];
                    if (def == null || string.IsNullOrEmpty(def.id) || save.done.Contains(def.id)) continue;
                    if (_progress.IsMet(def.condition))
                    {
                        save.done.Add(def.id);
                        changed = true;
                    }
                    else
                    {
                        allDone = false;
                    }
                }
            }
            if (allDone) _allDoneOpenDays = open;
            if (changed) NotifyChanged();
        }
    }
}
