using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>이벤트 하나의 상태 (EventCalendarSystem.GetActive · GetAll이 채운다, 목록 재사용 가능)</summary>
    public sealed class ActiveEventStatus
    {
        public EventDef Def;
        public string Name;
        public EventKind Kind;
        /// <summary>효과 한 줄 (예: '원정 재료 ×2')</summary>
        public string EffectLabel;
        /// <summary>진행 중이면 끝까지 남은 시간 (아니면 0)</summary>
        public TimeSpan TimeLeft;
        /// <summary>회차 키 {eventId}@{startDayKey} (미션 · 도장이 없는 밤 이벤트는 null)</summary>
        public string InstanceKey;
        /// <summary>지금 진행 중인지 (GetActive는 늘 true)</summary>
        public bool IsActive;
        /// <summary>다음 시작 시각 (유닉스 초, 진행 중이거나 다음이 없으면 0)</summary>
        public long NextStartUnix;
        /// <summary>다음 시작 한 줄 (예: '토요일 새벽 5시부터', 진행 중이면 빈 문자열)</summary>
        public string NextStartText = string.Empty;
    }

    /// <summary>이벤트 미션 하나 (EventCalendarSystem.GetMissions가 채운다, 목록 재사용 가능)</summary>
    public sealed class EventMissionStatus
    {
        public EventMissionDef Def;
        /// <summary>평생 카운터 − 회차 기준값 (목표에서 자름). 보너스 미션은 받은 미션 수</summary>
        public long Progress;
        public long Target;
        public bool IsComplete;
        public bool IsClaimed;
        public bool CanClaim => IsComplete && !IsClaimed;
    }

    /// <summary>
    /// 이벤트 달력 (EVT-01, SPEC4 §8.3): DailyClock 날 키로 정해지는 이벤트 (주말 원정 · 수요 던전 · 별이 머문 밤 산책 · 밤의 수선점),
    /// 회차별 미션 · 도장, 시작 카드. FeatureId.Events (3-1 또는 설치 3일째) 뒤에만, 콘텐츠(events)가 비어 있으면 아무것도 하지 않는다.
    /// <para>
    /// 일정 (결정적 — 같은 날이면 어느 기기에서나 같다):
    ///   · Weekdays (weekdayMask bit0 = 월요일) · MonthDays (fromDay~toDay, 양끝 포함) · Dates (fromMonth/fromDay~toMonth/toDay, 해를 넘을 수 있음 — 예약)
    ///     는 날 키의 날짜(현지 05:00 경계, 유효 시각 = max(지금, 본 가장 늦은 시각))로 본다.
    ///   · DailyHours (fromHour~toHour, 끝 포함 안 함, 자정을 넘을 수 있음)만 현지 시계의 시(hour)로 본다. 서머타임으로 없는 시각은 DailyClock.LocalToUnix가 민다.
    /// </para>
    /// <para>
    /// 효과 (잠겼으면 모두 없음): DropBoost → 원정 재료 배율(Expedition.DropMultiplier, 받을 때), TicketBonus → 그날 던전 입장권 더하기(Dungeon.BonusTickets),
    /// Festival → 스테이지 클리어 별빛 사탕 배율(Battle.CandyMultiplier) + 바구니 이벤트 전용 줄 시간(IdleChest.EventLineHours),
    /// NightBonus → 바구니 재료 · 별조각 줄에 겹친 시간(IdleChest.NightBonusHours = NightBonusHours, 순수 겹침 — SPEC4 §8.3) × (배율 − 1)
    /// (IdleChest.NightBonusMultiplier = NightBonusMultiplier)의 덤 시간 — 곱하기는 바구니가 한다. 배율은 겹쳐도 곱하지 않고 큰 값 하나 (SPEC5 — ×4 없음).
    /// 훅은 Connect에서 한 번 잇는다 (캐시한 델리게이트). 던전은 메타 데이터를 모르므로 잔향 축복 표(Dungeon.BlessingPool)도 여기서 넣는다.
    /// </para>
    /// <para>
    /// 회차: 날 기준 이벤트가 켜져 있는 동안 키 {id}@{시작 날 키}(이어진 켜진 날들의 첫날 — 주말 = 토요일, 매달 1~14일 = 1일)로 하나.
    /// 처음 켜질 때(Connect 뒤 · 날 바뀜 · 개방 · 1초 확인) 미션 카운터마다 기준값 = 평생 값. 미션 진행 = 평생 − 기준값 (목표에서 자름),
    /// 보너스 미션(isBonus)은 다른 미션을 모두 받으면 완료. 도장은 날 키마다 하나, 최대 min(7, 도장 칸 수). 받기 · 도장은 진행 중인 회차만, 되돌림 중에는 막는다.
    /// startCard면 회차마다 한 번 EventStart(키)를 큐에 넣는다 (접속 종류 — 상한에 걸려도 남는다, PopupQueue).
    /// 빨간 점: EventClaim (받을 미션 · 찍을 도장), EventNew (열어 보지 않은 회차). 회차 저장은 EventSave.KeepDays(60일)가 지나면 지운다.
    /// 플레이 기록: 미션을 받으면 'event_claim' {id = 이벤트 id, mission}, 도장을 찍으면 'event_claim' {id, stamp = 몇 번째} (SPEC4 §4.4, 기록이 닫혀 있으면 아무것도 안 함).
    /// Tick은 1초마다 켜진 이벤트 묶음(비트)만 비교한다 (할당 없음).
    /// </para>
    /// </summary>
    public class EventCalendarSystem : IMetaModule
    {
        /// <summary>진행 중인 이벤트 · 미션 · 도장이 바뀜</summary>
        public event Action Changed;

        /// <summary>도장 최대 칸</summary>
        public const int MaxStamps = 7;
        /// <summary>회차 시작 · 끝 · 다음 시작을 찾을 때 훑는 최대 날 수</summary>
        const int MaxScanDays = 400;
        const float TickInterval = 1f;

        static readonly string[] WeekdayNames = { "월요일", "화요일", "수요일", "목요일", "금요일", "토요일", "일요일" };

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;
        readonly PopupQueue _popups;
        readonly BattleSystem _battle;
        readonly ExpeditionSystem _expedition;
        readonly DungeonSystem _dungeon;
        readonly IdleChestSystem _idleChest;

        // 훅 델리게이트 (한 번만 만든다)
        readonly Func<double> _dropHook;
        readonly Func<double> _candyHook;
        readonly Func<int> _ticketHook;
        readonly Func<long, long, double> _nightHook;
        readonly Func<double> _nightRateHook;
        readonly Func<long, long, double> _eventLineHook;

        RedDotSystem _dots;
        bool _connected;
        float _tickTimer;
        int _lastActiveMask = -1;

        // 오늘 캐시 (날짜가 바뀌거나 이벤트 목록이 바뀔 때만 다시 만든다)
        DateTime _cacheDate = DateTime.MinValue;
        List<EventDef> _cacheList;
        int _cacheCount = -1;
        bool[] _activeToday = Array.Empty<bool>();
        string[] _todayKeys = Array.Empty<string>();

        public EventCalendarSystem(PlayerData data, MetaDatabase db, DailyClock clock, ProgressTracker progress, MetaRewardService rewards,
            FeatureGateSystem features, PopupQueue popups, BattleSystem battle, ExpeditionSystem expedition, DungeonSystem dungeon,
            IdleChestSystem idleChest)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _progress = progress;
            _rewards = rewards;
            _features = features;
            _popups = popups;
            _battle = battle;
            _expedition = expedition;
            _dungeon = dungeon;
            _idleChest = idleChest;

            _dropHook = () => ExpeditionDropMultiplier;
            _candyHook = () => StageCandyMultiplier;
            _ticketHook = () => BonusDungeonTickets;
            _nightHook = NightBonusHours;
            _nightRateHook = () => NightBonusMultiplier;
            _eventLineHook = EventLineHours;
        }

        EventSave Save
        {
            get
            {
                var save = (_data.meta ??= new MetaSave()).events ??= new EventSave();
                save.instances ??= new Dictionary<string, EventInstanceSave>();
                return save;
            }
        }

        List<EventDef> Events => _db != null ? _db.events : null;

        bool HasEvents => Events != null && Events.Count > 0;

        bool Tampered => _clock != null && _clock.IsTampered;

        long Now => _clock != null ? _clock.EffectiveUnix : 0;

        /// <summary>이벤트가 열렸는지 (FeatureId.Events, 정의 없음 = 열림) — 이벤트 콘텐츠가 있고 일일 시계가 있을 때만</summary>
        public bool IsUnlocked => HasEvents && _clock != null && (_features == null || _features.IsUnlocked(FeatureId.Events));

        // ───────────── 효과 ─────────────

        /// <summary>원정 재료 배율 (주말 원정 축제, 받을 때 적용). 효과 없음 = 1</summary>
        public double ExpeditionDropMultiplier => MaxMultiplierNow(EventKind.DropBoost);

        /// <summary>오늘 더해지는 던전 입장권 (수요 던전 데이). 없음 = 0</summary>
        public int BonusDungeonTickets
        {
            get
            {
                if (!IsUnlocked) return 0;
                var events = Events;
                int bonus = 0;
                for (int i = 0; i < events.Count; i++)
                {
                    var def = events[i];
                    if (def != null && def.kind == EventKind.TicketBonus && def.bonus > 0 && IsActiveAt(def, Now)) bonus += def.bonus;
                }
                return bonus;
            }
        }

        /// <summary>스테이지 클리어 별빛 사탕 배율 (별이 머문 밤 산책). 없음 = 1</summary>
        public double StageCandyMultiplier => MaxMultiplierNow(EventKind.Festival);

        /// <summary>
        /// [fromUnix, toUnix)가 밤의 수선점(NightBonus, 배율 &gt; 1)과 겹친 시간 (시간 단위 — 순수 겹침 계산, SPEC4 §8.3. 예: 20:00~03:00 → 21~02 = 5시간).
        /// 밤 이벤트가 여럿이면 가장 긴 겹침. 바구니 덤 시간은 이 값 × (NightBonusMultiplier − 1)이고 곱하기는 IdleChestSystem이 한다. 잠겼으면 0
        /// </summary>
        public double NightBonusHours(long fromUnix, long toUnix)
        {
            if (!IsUnlocked || toUnix <= fromUnix) return 0.0;
            var events = Events;
            double hours = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var def = events[i];
                if (def == null || def.kind != EventKind.NightBonus || !(def.multiplier > 1.0)) continue;
                double overlap = OverlapHours(def, fromUnix, toUnix);
                if (overlap > hours) hours = overlap;
            }
            return hours;
        }

        /// <summary>
        /// 밤의 수선점 바구니 배율 (예 1.2 = 겹친 시간의 20%를 덤으로). 지금 켜졌는지와 무관한 콘텐츠 값 — 지난 구간에도 쓰므로.
        /// 밤 이벤트가 여럿이면 큰 값 하나 (곱하지 않음). 잠겼거나 없으면 1
        /// </summary>
        public double NightBonusMultiplier
        {
            get
            {
                if (!IsUnlocked) return 1.0;
                var events = Events;
                double best = 1.0;
                for (int i = 0; i < events.Count; i++)
                {
                    var def = events[i];
                    if (def == null || def.kind != EventKind.NightBonus || !(def.multiplier > best) || double.IsInfinity(def.multiplier)) continue;
                    best = def.multiplier;
                }
                return best;
            }
        }

        /// <summary>[fromUnix, toUnix) 중 이벤트 전용 바구니 줄(별빛 사탕)이 쌓이는 시간 = 축제(Festival) 이벤트와 겹친 시간 (시간 단위 — 순수 계산). 잠겼으면 0</summary>
        public double EventLineHours(long fromUnix, long toUnix)
        {
            if (!IsUnlocked || toUnix <= fromUnix) return 0.0;
            var events = Events;
            double hours = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var def = events[i];
                if (def == null || def.kind != EventKind.Festival) continue;
                double overlap = OverlapHours(def, fromUnix, toUnix);
                if (overlap > hours) hours = overlap;
            }
            return hours;
        }

        /// <summary>
        /// 이벤트와 [fromUnix, toUnix)가 겹친 시간 (시간 단위, 잠금과 무관한 순수 일정 계산). 날 기준 이벤트는 켜진 날의 [05:00, 다음 날 05:00),
        /// 시 기준 이벤트는 현지 [fromHour, toHour) 창 (자정을 넘으면 다음 날 toHour까지)
        /// </summary>
        public double OverlapHours(EventDef def, long fromUnix, long toUnix)
        {
            if (def == null || _clock == null || toUnix <= fromUnix) return 0.0;
            long total = 0;
            if (def.scheduleType == EventScheduleType.DailyHours)
            {
                if (def.fromHour == def.toHour) return 0.0;
                var day = _clock.ToLocal(fromUnix).Date.AddDays(-1);
                var last = _clock.ToLocal(toUnix).Date;
                for (int guard = 0; day <= last && guard < MaxScanDays; guard++, day = day.AddDays(1))
                {
                    GetHourWindow(def, day, out long start, out long end);
                    total += Overlap(fromUnix, toUnix, start, end);
                }
            }
            else
            {
                var day = _clock.GameDateOf(fromUnix);
                var last = _clock.GameDateOf(toUnix - 1);
                for (int guard = 0; day <= last && guard < MaxScanDays; guard++, day = day.AddDays(1))
                {
                    if (!IsActiveOnDate(def, day)) continue;
                    total += Overlap(fromUnix, toUnix, _clock.DayStartUnix(day), _clock.DayStartUnix(day.AddDays(1)));
                }
            }
            return total / 3600.0;
        }

        // ───────────── 진행 중인 이벤트 ─────────────

        /// <summary>지금 진행 중인 이벤트 (into는 비우고 채움). 잠겼으면 빈 목록</summary>
        public void GetActive(List<ActiveEventStatus> into) => Fill(into, activeOnly: true);

        /// <summary>모든 이벤트 (진행 중 + 다음 시작 — 모아 보기의 꺼진 효과 칩 '토요일 새벽 5시부터'). 잠겼으면 빈 목록</summary>
        public void GetAll(List<ActiveEventStatus> into) => Fill(into, activeOnly: false);

        /// <summary>그 이벤트가 지금 진행 중인지 (잠금과 무관한 일정, 유효 시각)</summary>
        public bool IsActiveNow(EventDef def) => def != null && _clock != null && IsActiveAt(def, Now);

        /// <summary>진행 중인 날 기준 이벤트의 오늘 회차 키 (아니면 null). 같은 날이면 같은 문자열</summary>
        public string GetInstanceKey(string eventId)
        {
            if (_clock == null || !HasEvents) return null;
            EnsureToday();
            var events = Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null && events[i].id == eventId) return _todayKeys[i];
            }
            return null;
        }

        // ───────────── 미션 · 도장 ─────────────

        /// <summary>회차의 미션 (into는 비우고 채움). 없는 회차면 빈 목록</summary>
        public void GetMissions(string instanceKey, List<EventMissionStatus> into)
        {
            if (into == null) return;
            into.Clear();
            var instance = FindInstance(instanceKey);
            var def = instance != null ? _db?.GetEvent(instance.eventId) : null;
            if (def?.missions == null) return;
            for (int i = 0; i < def.missions.Count; i++)
            {
                var mission = def.missions[i];
                if (mission == null || string.IsNullOrEmpty(mission.id)) continue;
                GetMissionProgress(def, instance, mission, out long progress, out long target);
                into.Add(new EventMissionStatus
                {
                    Def = mission,
                    Progress = progress,
                    Target = target,
                    IsComplete = progress >= target,
                    IsClaimed = instance.claimed.Contains(mission.id),
                });
            }
        }

        /// <summary>미션 하나를 받는다 (진행 중인 회차 · 완료 · 아직 안 받음 · 되돌림 아님). 받을 수 없으면 null</summary>
        public List<MetaReward> TryClaimMission(string instanceKey, string missionId)
        {
            if (!CanAct(instanceKey, out var instance, out var def) || def.missions == null || string.IsNullOrEmpty(missionId)) return null;
            if (instance.claimed.Contains(missionId)) return null;
            EventMissionDef mission = null;
            for (int i = 0; i < def.missions.Count && mission == null; i++)
            {
                if (def.missions[i] != null && def.missions[i].id == missionId) mission = def.missions[i];
            }
            if (mission == null) return null;
            GetMissionProgress(def, instance, mission, out long progress, out long target);
            if (progress < target) return null;

            instance.claimed.Add(missionId);
            var granted = Grant(mission.rewards);
            _progress?.Increment(CounterKey.EventMissionClaim);
            _progress?.MarkDirty();
            PlayLog.Begin("event_claim").Str("id", def.id).Str("mission", missionId).End();
            NotifyChanged();
            return granted;
        }

        /// <summary>오늘 도장을 찍을 수 있는지 (진행 중인 회차 · 오늘 아직 안 찍음 · 도장 칸이 남음 · 되돌림 아님)</summary>
        public bool CanStamp(string instanceKey)
        {
            if (!CanAct(instanceKey, out var instance, out var def)) return false;
            return CanStamp(instance, def);
        }

        /// <summary>오늘 도장을 찍고 그 칸 보상을 받는다. 찍을 수 없으면 null</summary>
        public List<MetaReward> TryStamp(string instanceKey)
        {
            if (!CanAct(instanceKey, out var instance, out var def) || !CanStamp(instance, def)) return null;
            var stamp = GetStampDef(def, instance.stamps + 1);
            instance.stamps++;
            instance.lastStampDayKey = _clock.DayKey;
            var granted = Grant(stamp?.rewards);
            _progress?.MarkDirty();
            PlayLog.Begin("event_claim").Str("id", def.id).Int("stamp", instance.stamps).End();
            NotifyChanged();
            return granted;
        }

        /// <summary>그 회차에서 찍은 도장 수</summary>
        public int GetStampCount(string instanceKey) => FindInstance(instanceKey)?.stamps ?? 0;

        /// <summary>
        /// 그 회차에서 찍은 도장 수를 0…MaxStamps로 잘라서 (화면 도장 칸용). 저장을 읽기만 한다 — 저장을 만들거나 고치지 않고,
        /// 잠김 · 되돌림과 상관없이 찍은 만큼을 보여 준다. 키가 비었거나 없는 회차면 0. 할당 없음, 던지지 않음
        /// </summary>
        public int GetStampsTaken(string instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey)) return 0;
            var instances = _data?.meta?.events?.instances;
            if (instances == null || !instances.TryGetValue(instanceKey, out var instance) || instance == null) return 0;
            return Math.Max(0, Math.Min(MaxStamps, instance.stamps));
        }

        /// <summary>받을 미션이나 찍을 도장이 있는지 (진행 중인 회차, 되돌림 아님). 할당 없음</summary>
        public bool HasClaimable
        {
            get
            {
                if (!IsUnlocked || Tampered) return false;
                EnsureToday();
                var events = Events;
                for (int i = 0; i < events.Count; i++)
                {
                    string key = _todayKeys[i];
                    if (key == null) continue;
                    var instance = FindInstance(key);
                    var def = events[i];
                    if (instance == null || def == null) continue;
                    if (CanStamp(instance, def)) return true;
                    if (def.missions == null) continue;
                    for (int m = 0; m < def.missions.Count; m++)
                    {
                        var mission = def.missions[m];
                        if (mission == null || string.IsNullOrEmpty(mission.id) || instance.claimed.Contains(mission.id)) continue;
                        GetMissionProgress(def, instance, mission, out long progress, out long target);
                        if (progress >= target) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>열어 보지 않은 진행 중 회차가 있는지 (top.event.new). 할당 없음</summary>
        public bool HasUnseen
        {
            get
            {
                if (!IsUnlocked) return false;
                EnsureToday();
                for (int i = 0; i < _todayKeys.Length; i++)
                {
                    string key = _todayKeys[i];
                    if (key == null) continue;
                    var instance = FindInstance(key);
                    if (instance != null && !instance.seen) return true;
                }
                return false;
            }
        }

        /// <summary>회차를 열어 봄 (top.event.new 끄기). null이면 진행 중인 회차를 모두</summary>
        public void MarkSeen(string instanceKey)
        {
            bool changed = false;
            if (instanceKey == null)
            {
                EnsureToday();
                for (int i = 0; i < _todayKeys.Length; i++)
                {
                    var instance = FindInstance(_todayKeys[i]);
                    if (instance != null && !instance.seen)
                    {
                        instance.seen = true;
                        changed = true;
                    }
                }
            }
            else
            {
                var instance = FindInstance(instanceKey);
                if (instance != null && !instance.seen)
                {
                    instance.seen = true;
                    changed = true;
                }
            }
            if (changed) NotifyChanged();
        }

        // ── IMetaModule ──

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_expedition != null) _expedition.DropMultiplier = _dropHook;
            if (_dungeon != null)
            {
                _dungeon.BonusTickets = _ticketHook;
                if (_db != null) _dungeon.BlessingPool = _db.blessings;
            }
            if (_battle != null) _battle.CandyMultiplier = _candyHook;
            if (_idleChest != null)
            {
                _idleChest.NightBonusHours = _nightHook;
                _idleChest.NightBonusMultiplier = _nightRateHook;
                _idleChest.EventLineHours = _eventLineHook;
            }
            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
            EnsureInstances();
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
            bool created = EnsureInstances();
            QueueStartCards();
            _lastActiveMask = ActiveMask();
            if (created) NotifyChanged();
        }

        public void OnDayChanged(string dayKey)
        {
            Save.Sanitize(dayKey);
            EnsureInstances();
            QueueStartCards();
            _lastActiveMask = ActiveMask();
            NotifyChanged();
        }

        public void Tick(float unscaledDeltaTime)
        {
            _tickTimer += unscaledDeltaTime > 0f && !float.IsInfinity(unscaledDeltaTime) ? unscaledDeltaTime : 0f;
            if (_tickTimer < TickInterval) return;
            _tickTimer = 0f;
            if (!HasEvents) return;
            int mask = ActiveMask();
            if (mask != _lastActiveMask)
            {
                _lastActiveMask = mask;
                EnsureInstances();
                QueueStartCards();
                NotifyChanged();
                return;
            }
            // 미션 진행은 전투 중 계속 바뀐다 — 점은 1초에 한 번만 다시 본다
            _dots?.MarkDirty(RedDotPaths.EventClaim);
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            if (dots == null) return;
            _dots = dots;
            dots.Register(RedDotPaths.EventClaim, () => HasClaimable);
            dots.Register(RedDotPaths.EventNew, () => HasUnseen);
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        void NotifyChanged()
        {
            if (_dots != null)
            {
                _dots.MarkDirty(RedDotPaths.EventClaim);
                _dots.MarkDirty(RedDotPaths.EventNew);
            }
            Changed?.Invoke();
        }

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature != FeatureId.Events) return;
            EnsureInstances();
            QueueStartCards();
            _lastActiveMask = ActiveMask();
            NotifyChanged();
        }

        List<MetaReward> Grant(List<MetaReward> rewards)
        {
            if (rewards == null) return new List<MetaReward>();
            return _rewards != null ? _rewards.Grant(rewards) : MetaRewardService.Merge(rewards);
        }

        EventInstanceSave FindInstance(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var instance = Save.instances.TryGetValue(key, out var value) ? value : null;
            if (instance == null) return null;
            instance.baseline ??= new Dictionary<CounterKey, long>();
            instance.claimed ??= new HashSet<string>();
            return instance;
        }

        /// <summary>받기 · 도장을 할 수 있는 회차인지: 열림 · 되돌림 아님 · 오늘 진행 중인 회차 키 · 저장된 회차 · 정의 있음</summary>
        bool CanAct(string key, out EventInstanceSave instance, out EventDef def)
        {
            instance = null;
            def = null;
            if (!IsUnlocked || Tampered || string.IsNullOrEmpty(key) || !IsTodayKey(key)) return false;
            instance = FindInstance(key);
            def = instance != null ? _db.GetEvent(instance.eventId) : null;
            return instance != null && def != null;
        }

        bool IsTodayKey(string key)
        {
            EnsureToday();
            for (int i = 0; i < _todayKeys.Length; i++)
            {
                if (_todayKeys[i] != null && _todayKeys[i] == key) return true;
            }
            return false;
        }

        bool CanStamp(EventInstanceSave instance, EventDef def)
        {
            int max = Math.Min(MaxStamps, def.stamps != null ? def.stamps.Count : 0);
            if (max <= 0 || instance.stamps >= max) return false;
            string today = _clock.DayKey;
            return instance.lastStampDayKey == null || instance.lastStampDayKey != today;
        }

        static EventStampDef GetStampDef(EventDef def, int day)
        {
            var stamps = def.stamps;
            if (stamps == null) return null;
            for (int i = 0; i < stamps.Count; i++)
            {
                if (stamps[i] != null && stamps[i].day == day) return stamps[i];
            }
            return day >= 1 && day <= stamps.Count ? stamps[day - 1] : null;
        }

        /// <summary>미션 진행 (할당 없음): 보너스 = 받은 일반 미션 수 / 일반 미션 수, 나머지 = 평생 − 기준값 (목표에서 자름)</summary>
        void GetMissionProgress(EventDef def, EventInstanceSave instance, EventMissionDef mission, out long progress, out long target)
        {
            if (mission.isBonus)
            {
                long regular = 0, claimed = 0;
                for (int i = 0; i < def.missions.Count; i++)
                {
                    var other = def.missions[i];
                    if (other == null || other.isBonus || string.IsNullOrEmpty(other.id)) continue;
                    regular++;
                    if (instance.claimed.Contains(other.id)) claimed++;
                }
                target = Math.Max(1, regular);
                progress = Math.Min(target, claimed);
                return;
            }
            target = Math.Max(1, mission.target);
            long lifetime = _progress != null ? _progress.GetLifetime(mission.counter) : 0;
            long baseline = instance.baseline.TryGetValue(mission.counter, out long value) ? value : lifetime;
            progress = Math.Max(0, Math.Min(target, lifetime - baseline));
        }

        /// <summary>오늘 진행 중인 날 기준 이벤트마다 회차를 만든다 (처음 켜질 때 미션 카운터 기준값 = 평생 값). 새로 만들었으면 true</summary>
        bool EnsureInstances()
        {
            if (!IsUnlocked) return false;
            EnsureToday();
            var events = Events;
            var instances = Save.instances;
            bool created = false;
            for (int i = 0; i < events.Count; i++)
            {
                string key = _todayKeys[i];
                var def = events[i];
                if (key == null || def == null || instances.ContainsKey(key)) continue;
                var instance = new EventInstanceSave
                {
                    eventId = def.id,
                    startDayKey = key.Substring(key.IndexOf('@') + 1),
                };
                if (def.missions != null)
                {
                    for (int m = 0; m < def.missions.Count; m++)
                    {
                        var mission = def.missions[m];
                        if (mission == null || mission.isBonus || instance.baseline.ContainsKey(mission.counter)) continue;
                        instance.baseline[mission.counter] = _progress != null ? _progress.GetLifetime(mission.counter) : 0;
                    }
                }
                instances[key] = instance;
                created = true;
            }
            return created;
        }

        /// <summary>시작 카드 (startCard) — 진행 중인 회차마다 한 번</summary>
        void QueueStartCards()
        {
            if (_popups == null || !IsUnlocked) return;
            EnsureToday();
            var events = Events;
            for (int i = 0; i < events.Count; i++)
            {
                string key = _todayKeys[i];
                var def = events[i];
                if (key == null || def == null || !def.startCard) continue;
                var instance = FindInstance(key);
                if (instance == null || instance.startCardShown) continue;
                instance.startCardShown = true;
                _popups.Enqueue(MetaPopupRequest.EventStart(key));
            }
        }

        /// <summary>지금 켜진 이벤트 비트 (앞 31개, 할당 없음). 잠겼으면 0</summary>
        int ActiveMask()
        {
            if (!IsUnlocked) return 0;
            var events = Events;
            long now = Now;
            int mask = 0;
            for (int i = 0; i < events.Count && i < 31; i++)
            {
                if (events[i] != null && IsActiveAt(events[i], now)) mask |= 1 << i;
            }
            return mask;
        }

        double MaxMultiplierNow(EventKind kind)
        {
            if (!IsUnlocked) return 1.0;
            var events = Events;
            long now = Now;
            double best = 1.0;
            for (int i = 0; i < events.Count; i++)
            {
                var def = events[i];
                if (def == null || def.kind != kind || !(def.multiplier > best) || double.IsInfinity(def.multiplier)) continue;
                if (IsActiveAt(def, now)) best = def.multiplier;
            }
            return best;
        }

        /// <summary>그 시각에 켜져 있는지: 날 기준 = 그 시각의 날 키 날짜 (지금이면 DailyClock.DayDate — 시간대를 바꿔도 거꾸로 가지 않는 날), 시 기준 = 현지 시</summary>
        bool IsActiveAt(EventDef def, long unix)
        {
            if (def.scheduleType == EventScheduleType.DailyHours)
                return InHourWindow(_clock.ToLocal(unix).Hour, def.fromHour, def.toHour);
            var date = unix == Now ? _clock.DayDate : _clock.GameDateOf(unix);
            return IsActiveOnDate(def, date);
        }

        /// <summary>날 기준 일정이 그 게임 날짜에 켜져 있는지 (DailyHours는 false — 시 기준)</summary>
        public static bool IsActiveOnDate(EventDef def, DateTime gameDate)
        {
            if (def == null) return false;
            switch (def.scheduleType)
            {
                case EventScheduleType.Weekdays:
                    return (def.weekdayMask & (1 << DailyClock.WeekdayOf(gameDate))) != 0;
                case EventScheduleType.MonthDays:
                {
                    int day = gameDate.Day;
                    if (def.fromDay <= 0 || def.toDay <= 0) return false;
                    return def.fromDay <= def.toDay ? day >= def.fromDay && day <= def.toDay : day >= def.fromDay || day <= def.toDay;
                }
                case EventScheduleType.Dates:
                {
                    if (def.fromMonth < 1 || def.fromMonth > 12 || def.toMonth < 1 || def.toMonth > 12) return false;
                    int value = gameDate.Month * 100 + gameDate.Day;
                    int from = def.fromMonth * 100 + Math.Max(1, def.fromDay);
                    int to = def.toMonth * 100 + Math.Max(1, def.toDay);
                    return from <= to ? value >= from && value <= to : value >= from || value <= to;
                }
                default:
                    return false;
            }
        }

        /// <summary>현지 시가 [from, to) 창 안인지 (자정을 넘는 창: from &gt; to). from == to면 없음</summary>
        public static bool InHourWindow(int hour, int fromHour, int toHour)
        {
            if (fromHour == toHour) return false;
            return fromHour < toHour ? hour >= fromHour && hour < toHour : hour >= fromHour || hour < toHour;
        }

        /// <summary>현지 날짜 day에 시작하는 시 기준 창 [시작, 끝) (유닉스 초)</summary>
        void GetHourWindow(EventDef def, DateTime day, out long start, out long end)
        {
            start = _clock.LocalToUnix(day.AddHours(Clamp(def.fromHour, 0, 23)));
            var endDay = def.toHour > def.fromHour ? day : day.AddDays(1);
            end = _clock.LocalToUnix(endDay.AddHours(Clamp(def.toHour, 0, 24)));
        }

        static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        static long Overlap(long a0, long a1, long b0, long b1)
        {
            long start = a0 > b0 ? a0 : b0;
            long end = a1 < b1 ? a1 : b1;
            return end > start ? end - start : 0;
        }

        /// <summary>오늘 날짜 캐시: 날 기준 이벤트마다 오늘 켜졌는지와 회차 키 (날이 바뀌거나 목록이 바뀔 때만 다시 만든다)</summary>
        void EnsureToday()
        {
            var events = Events;
            int count = events != null ? events.Count : 0;
            if (_clock == null)
            {
                if (_todayKeys.Length != count)
                {
                    _todayKeys = new string[count];
                    _activeToday = new bool[count];
                }
                return;
            }
            var today = _clock.DayDate;
            if (today == _cacheDate && ReferenceEquals(events, _cacheList) && count == _cacheCount) return;
            _cacheDate = today;
            _cacheList = events;
            _cacheCount = count;
            if (_todayKeys.Length != count)
            {
                _todayKeys = new string[count];
                _activeToday = new bool[count];
            }
            for (int i = 0; i < count; i++)
            {
                var def = events[i];
                bool active = def != null && !string.IsNullOrEmpty(def.id) && def.scheduleType != EventScheduleType.DailyHours && IsActiveOnDate(def, today);
                _activeToday[i] = active;
                _todayKeys[i] = active ? def.id + "@" + DailyClock.DayKeyOfDate(BlockStart(def, today)) : null;
            }
        }

        /// <summary>오늘이 속한 이어진 켜진 날들의 첫날 (주말 → 토요일, 매달 1~14일 → 1일)</summary>
        static DateTime BlockStart(EventDef def, DateTime today)
        {
            var day = today;
            for (int guard = 0; guard < MaxScanDays && IsActiveOnDate(def, day.AddDays(-1)); guard++) day = day.AddDays(-1);
            return day;
        }

        void Fill(List<ActiveEventStatus> into, bool activeOnly)
        {
            if (into == null) return;
            into.Clear();
            if (!IsUnlocked) return;
            EnsureToday();
            var events = Events;
            long now = Now;
            for (int i = 0; i < events.Count; i++)
            {
                var def = events[i];
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                bool active = IsActiveAt(def, now);
                if (activeOnly && !active) continue;
                var status = new ActiveEventStatus
                {
                    Def = def,
                    Name = def.name,
                    Kind = def.kind,
                    EffectLabel = EffectLabelOf(def),
                    IsActive = active,
                    InstanceKey = active ? _todayKeys[i] : null,
                };
                if (active)
                {
                    long end = ActiveEnd(def, now);
                    status.TimeLeft = TimeSpan.FromSeconds(Math.Max(0, end - now));
                }
                else
                {
                    long next = NextStart(def, now);
                    status.NextStartUnix = next;
                    status.NextStartText = next > 0 ? NextStartTextOf(def, next, now) : string.Empty;
                }
                into.Add(status);
            }
        }

        /// <summary>진행 중인 창이 끝나는 시각</summary>
        long ActiveEnd(EventDef def, long now)
        {
            if (def.scheduleType == EventScheduleType.DailyHours)
            {
                var local = _clock.ToLocal(now).Date;
                for (int back = 1; back >= 0; back--)
                {
                    GetHourWindow(def, local.AddDays(-back), out long start, out long end);
                    if (now >= start && now < end) return end;
                }
                return now;
            }
            var day = _clock.DayDate;
            for (int guard = 0; guard < MaxScanDays && IsActiveOnDate(def, day.AddDays(1)); guard++) day = day.AddDays(1);
            return _clock.DayStartUnix(day.AddDays(1));
        }

        /// <summary>다음 시작 시각 (없으면 0)</summary>
        long NextStart(EventDef def, long now)
        {
            if (def.scheduleType == EventScheduleType.DailyHours)
            {
                if (def.fromHour == def.toHour) return 0;
                var local = _clock.ToLocal(now).Date;
                for (int add = 0; add <= 1; add++)
                {
                    GetHourWindow(def, local.AddDays(add), out long start, out _);
                    if (start > now) return start;
                }
                return 0;
            }
            var day = _clock.DayDate;
            for (int guard = 0; guard < MaxScanDays; guard++)
            {
                day = day.AddDays(1);
                if (IsActiveOnDate(def, day)) return _clock.DayStartUnix(day);
            }
            return 0;
        }

        string NextStartTextOf(EventDef def, long next, long now)
        {
            var local = _clock.ToLocal(next);
            string hour = HourText(local.Hour);
            if (def.scheduleType == EventScheduleType.DailyHours) return hour + "부터";
            int days = (int)Math.Round((_clock.GameDateOf(next) - _clock.DayDate).TotalDays);
            return days >= 1 && days <= 6
                ? $"{WeekdayNames[DailyClock.WeekdayOf(local.Date)]} {hour}부터"
                : $"{local.Month}월 {local.Day}일 {hour}부터";
        }

        /// <summary>효과 칩 한 줄 (SPEC4 A.7): '원정 재료 ×2' · '던전 입장권 +2' · '별빛 사탕 ×2' · '밤 9시~새벽 2시 바구니 +20%'</summary>
        public static string EffectLabelOf(EventDef def)
        {
            if (def == null) return string.Empty;
            switch (def.kind)
            {
                case EventKind.DropBoost:
                    return "원정 재료 ×" + Multiplier(def.multiplier);
                case EventKind.TicketBonus:
                    return "던전 입장권 +" + Math.Max(0, def.bonus);
                case EventKind.Festival:
                    return "별빛 사탕 ×" + Multiplier(def.multiplier);
                case EventKind.NightBonus:
                {
                    int percent = (int)Math.Round((def.multiplier - 1.0) * 100.0);
                    return $"{HourText(def.fromHour)}~{HourText(def.toHour)} 바구니 +{percent}%";
                }
                default:
                    return def.shortDesc ?? string.Empty;
            }
        }

        static string Multiplier(double value) =>
            Math.Abs(value - Math.Round(value)) < 1e-9 ? ((long)Math.Round(value)).ToString() : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>현지 시 → 말 ('새벽 2시' · '아침 7시' · '낮 12시' · '오후 3시' · '밤 9시' · '자정')</summary>
        public static string HourText(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            if (hour == 0) return "자정";
            if (hour < 5) return $"새벽 {hour}시";
            if (hour == 5) return "새벽 5시";
            if (hour < 12) return $"아침 {hour}시";
            if (hour == 12) return "낮 12시";
            if (hour < 18) return $"오후 {hour - 12}시";
            return $"밤 {hour - 12}시";
        }
    }
}
