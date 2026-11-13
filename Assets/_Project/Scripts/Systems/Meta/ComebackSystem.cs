using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 다시 만난 날 (CR-06, SPEC4 §8.2): 튜토리얼 뒤 72시간 이상 비우고 돌아오면 (되돌림 아님 · 평생 6번 · 14일 쉼)
    /// 바구니를 한 번 24시간 상한으로 채우고(BeforeAwayApplied → IdleChest.GrantBonusCap) 3일 미니 도장 카드(접속 종류 Comeback)를 띄운다.
    /// <para>
    /// 시작 (BeforeAwayApplied — 비운 시간을 바구니에 넣기 직전, 부팅 · 복귀 모두): 비운 시간 ≥ minAwaySeconds, 튜토리얼 마침(ftue.done),
    /// 되돌림 아님, count &lt; lifetimeMax, 마지막 다시 만난 날(lastDayKey)이 없거나 cooldownDays일 이상 지남, 콘텐츠(days)가 있음.
    /// 그러면 count++, lastDayKey = startDayKey = 오늘, day = 1, lastStampDayKey = null, awaySeconds = 비운 시간(카드 문구용, 저장),
    /// 바구니 한 번짜리 상한 bonusCapHours (다음 받기까지).
    /// </para>
    /// <para>
    /// 도장 (하루 한 칸, 날 키): TryClaimToday가 day일째 보상을 주고 day++. 마지막 칸 뒤 day = 0 (끝). 빠진 날이 있어도 다음 접속에서 이어 찍는다.
    /// 카드: AfterStartup · 날 바뀜에서 오늘 찍을 도장이 있으면 Comeback을 큐에 넣는다 (같은 날 한 번, 큐가 중복을 막는다).
    /// 되돌림 중에는 시작 · 도장 모두 막는다. 콘텐츠가 비어 있으면(days 없음) 아무것도 하지 않는다.
    /// </para>
    /// </summary>
    public class ComebackSystem : IMetaModule
    {
        /// <summary>도장 · 진행 상태가 바뀜</summary>
        public event Action Changed;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly MetaRewardService _rewards;
        readonly IdleChestSystem _idleChest;
        readonly PopupQueue _popups;

        /// <summary>이 세션에서 카드를 큐에 넣은 날 키 (같은 날 두 번 넣지 않게)</summary>
        string _queuedDayKey;

        /// <param name="tutorial">계약 모양 그대로 받는다 (시작 조건은 저장된 ftue.done만 본다)</param>
        public ComebackSystem(PlayerData data, MetaDatabase db, DailyClock clock, MetaRewardService rewards, IdleChestSystem idleChest,
            PopupQueue popups, TutorialSystem tutorial)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _rewards = rewards;
            _idleChest = idleChest;
            _popups = popups;
        }

        ComebackSave Save => (_data.meta ??= new MetaSave()).comeback ??= new ComebackSave();

        ComebackConfig Cfg => _db != null ? _db.comeback : null;

        string Today => _clock?.DayKey;

        bool Tampered => _clock != null && _clock.IsTampered;

        bool FtueDone => _data.meta?.ftue != null && _data.meta.ftue.done;

        /// <summary>도장 칸 수 (콘텐츠 days 중 가장 큰 day, 없으면 0 = 꺼짐)</summary>
        public int StampCount
        {
            get
            {
                var days = Cfg?.days;
                if (days == null) return 0;
                int max = 0;
                for (int i = 0; i < days.Count; i++)
                {
                    if (days[i] != null && days[i].day > max) max = days[i].day;
                }
                return max;
            }
        }

        /// <summary>3일 미니 도장이 진행 중인지</summary>
        public bool IsActive
        {
            get
            {
                int day = Save.day;
                return day >= 1 && day <= StampCount;
            }
        }

        /// <summary>오늘 찍을 도장 일차 (1~3, 없으면 0)</summary>
        public int Day => IsActive ? Save.day : 0;

        /// <summary>오늘 도장을 찍을 수 있는지 (진행 중 · 오늘 아직 안 찍음 · 되돌림 아님 · 그날 칸이 있음)</summary>
        public bool CanClaimToday
        {
            get
            {
                if (!IsActive || Tampered || _db == null) return false;
                var save = Save;
                if (save.lastStampDayKey != null && save.lastStampDayKey == Today) return false;
                return _db.GetComebackDay(save.day) != null;
            }
        }

        /// <summary>이미 찍은 도장 수 (카드의 칸 상태)</summary>
        public int StampsTaken => IsActive ? Math.Max(0, Save.day - 1) : 0;

        /// <summary>day일째 칸의 보상 (카드 미리보기, 없으면 null)</summary>
        public IReadOnlyList<MetaReward> GetDayRewards(int day) => _db?.GetComebackDay(day)?.rewards;

        /// <summary>오늘 도장을 찍고 그날 보상을 받는다. 찍을 수 없으면 null</summary>
        public List<MetaReward> TryClaimToday()
        {
            if (!CanClaimToday) return null;
            var save = Save;
            var def = _db.GetComebackDay(save.day);
            var rewards = def.rewards ?? new List<MetaReward>();
            var granted = _rewards != null ? _rewards.Grant(rewards) : MetaRewardService.Merge(rewards);
            save.lastStampDayKey = Today;
            save.day++;
            if (save.day > StampCount) save.day = 0;
            Changed?.Invoke();
            return granted;
        }

        /// <summary>이번 다시 만난 날을 연 비운 시간 (초, 카드 문구 'n일 만이에요' — 저장됨, 다시 켜도 그대로). 진행 중이 아니면 0</summary>
        public long AwaySeconds => IsActive ? Math.Max(0, Save.awaySeconds) : 0;

        /// <summary>비운 시간이 다시 만난 날을 열 수 있는지 (BeforeAwayApplied가 쓰는 규칙 그대로, 상태를 바꾸지 않음)</summary>
        public bool CanTrigger(long awaySeconds)
        {
            var cfg = Cfg;
            if (cfg == null || StampCount <= 0 || _db.GetComebackDay(1) == null) return false;
            if (awaySeconds <= 0 || awaySeconds < Math.Max(1, cfg.minAwaySeconds)) return false;
            if (!FtueDone || Tampered || _clock == null) return false;
            var save = Save;
            if (save.count >= Math.Max(0, cfg.lifetimeMax)) return false;
            if (!string.IsNullOrEmpty(save.lastDayKey) && DailyClock.DaysBetween(save.lastDayKey, Today) < Math.Max(0, cfg.cooldownDays)) return false;
            return true;
        }

        // ── IMetaModule ──

        public void Connect()
        {
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
            if (!CanTrigger(awaySeconds)) return;
            var save = Save;
            string today = Today;
            save.count++;
            save.lastDayKey = today;
            save.startDayKey = today;
            save.day = 1;
            save.lastStampDayKey = null;
            save.awaySeconds = Math.Max(0, awaySeconds);
            _queuedDayKey = null;
            var cfg = Cfg;
            if (cfg.bonusCapHours > 0) _idleChest?.GrantBonusCap(cfg.bonusCapHours);
            Changed?.Invoke();
        }

        public void AfterStartup(bool boot) => QueueCard();

        public void OnDayChanged(string dayKey)
        {
            if (!IsActive) return;
            QueueCard();
            Changed?.Invoke();
        }

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            // 빨간 점 없음 (SPEC4 §2.7 — 다시 만난 날은 접속 카드로만 알린다)
        }

        /// <summary>오늘 찍을 도장이 있으면 카드를 큐에 넣는다 (같은 날 한 번)</summary>
        void QueueCard()
        {
            if (_popups == null || !CanClaimToday) return;
            string today = Today;
            if (_queuedDayKey != null && _queuedDayKey == today) return;
            _queuedDayKey = today;
            _popups.Enqueue(MetaPopupRequest.Comeback());
        }
    }
}
