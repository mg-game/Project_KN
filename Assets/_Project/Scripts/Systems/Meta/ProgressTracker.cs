using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IdleGame
{
    /// <summary>
    /// 진행 기록기 (F-02). CounterKey마다 평생 / 오늘 / 이번 주 카운터를 세고(meta.counters),
    /// 메타 조건(MetaCondition)의 현재 값과 충족 여부를 판정하는 단일 창구다.
    ///
    /// 카운터 공급 — 생성자에서 기존 시스템 이벤트를 구독한다 (구현 담당 M1):
    ///   · Battle.Event: EnemyKilled → EnemyKill, EchoSummoned → EchoSummon, LampLit → LampLit
    ///     (전투 중 초당 수 회 — 할당 없이 메모리에서 더하고 Changed는 Flush에서 프레임당 1번)
    ///   · Battle.StageEnded: Cleared → StageClear + BossKill, Failed → StageFail
    ///   · Battle.Event SynergyActivated: 그 시너지의 최대 단계면 SynergyTierMax
    ///   · Growth.LevelChanged(이전, 새) → ShopLevelUp (+ AnyUpgrade) 차이만큼
    ///   · Characters.LevelUp → LineageLevelUp (+ AnyUpgrade), Characters.ResonanceUp → ResonanceUp (+ AnyGrowthMilestone)
    ///   · Evolution.FormUnlocked → EvolutionCraft (+ AnyGrowthMilestone)
    ///   · Costumes.CostumeUnlocked → CostumeUnlock (+ AnyGrowthMilestone), Costumes.CostumeEquipped → CostumeEquip
    ///   · Gacha.Pulled → GachaPull (뽑은 수만큼)
    ///   · Shop.Purchased → ShopBuy, Mail.Claimed → MailClaim
    ///   · Expedition.Changed: 원정 목록 스냅숏을 비교해 새로 생긴 원정 → ExpeditionStart, 완료 상태로 사라진 원정 → ExpeditionClaim,
    ///     끝나기 전에 사라진 원정(취소, 공짜로 친구가 바로 돌아옴) → 그 원정의 ExpeditionStart를 되돌린다 — 평생 값은 늘 빼고,
    ///     오늘/이번 주 값은 보낸 것을 센 날/주가 지금과 같을 때만 뺀다 (보내기·취소 반복으로 원정 미션·퀘스트를 채우지 않게).
    ///     (ExpeditionSystem은 다른 작업 중이라 전용 이벤트가 없다)
    ///   · Dungeon.Changed: 입장권 사용 수/최고 층 스냅숏 비교 → 층이 오르면 DungeonChallenge + DungeonClear,
    ///     층이 그대로인데 입장권만 줄면 쓴 장 수만큼 DungeonSweep (한 번에 소탕 n장 = n번, SPEC4 §5.2 — 둘 다 DungeonRun).
    ///     잔향 축복 고르기 · 다시 고르기(고른 축복 수 · 다시 고른 주 키가 바뀜)는 세지 않는다. 그 밖에 층도 입장권도 그대로면 실패한 도전
    ///     (DungeonSystem은 실패에 입장권을 쓰지 않는다) → DungeonChallenge만, DungeonRun에는 넣지 않는다 (공짜 반복으로 미션을 채우지 않게)
    ///   · 메타 시스템은 직접 Increment: Login(MetaSession), ChestClaim/QuickIdle(IdleChest), MissionClaim/JournalComplete(Mission),
    ///     GuideQuestClaim(GuideQuest), AttendanceCheckIn(Attendance)
    ///   · 합계 키(AnyUpgrade, AnyGrowthMilestone, DungeonRun)는 원래 키를 더할 때 함께 더한다.
    /// 오늘/이번 주 값은 DailyClock.DayChanged / WeekChanged에서 비우고, 읽을 때도 저장된 키가 지금과 다르면 0으로 본다.
    /// 기존 LineageSave.progress(진화 조건)와 BattleSave.totalKills는 건드리지 않는다.
    /// <para>
    /// 같은 PlayerData로 진행 기록기를 다시 만들면(테스트의 CreateMetaSystems) 앞의 것은 구독을 모두 풀어
    /// 같은 사건이 두 번 세어지지 않게 한다.
    /// 시너지 수/단계처럼 계산에 할당이 드는 값은 Synergy.Changed 때 더러움만 표시하고 처음 읽을 때 한 번 계산한다.
    /// </para>
    /// </summary>
    public class ProgressTracker
    {
        /// <summary>(키, 더한 양) — 더할 때마다 바로. 전투 중 자주 울리므로 구독자는 가벼워야 한다</summary>
        public event Action<CounterKey, long> Incremented;
        /// <summary>카운터·플래그·조건 값이 바뀜. Flush에서 프레임당 최대 1번 (UI·퀘스트·미션 갱신용)</summary>
        public event Action Changed;

        /// <summary>같은 PlayerData의 최신 진행 기록기 (다시 만들면 앞의 것은 구독을 푼다)</summary>
        static readonly ConditionalWeakTable<PlayerData, ProgressTracker> Active = new();

        readonly PlayerData _data;
        readonly DailyClock _clock;
        readonly BattleSystem _battle;
        readonly GrowthSystem _growth;
        readonly CharacterSystem _characters;
        readonly EvolutionSystem _evolution;
        readonly CostumeSystem _costumes;
        readonly GachaSystem _gacha;
        readonly ShopSystem _shop;
        readonly ExpeditionSystem _expedition;
        readonly DungeonSystem _dungeon;
        readonly FormationSystem _formation;
        readonly SynergySystem _synergy;
        readonly MailSystem _mail;
        readonly PowerSystem _power;

        bool _dirty;
        bool _attached;

        /// <summary>FeatureUnlocked 조건 판정 (FeatureGateSystem이 생성자에서 넣는다 — 정의 없는 기능 = 열림까지 맞추려고)</summary>
        internal Func<FeatureId, bool> FeatureResolver;

        /// <summary>
        /// 여기서 직접 셀 수 없는 조건 값을 4차 모듈이 답한다 (AffinityLevelCount · SynergyKindsSeen · CostumesOwned 등, SPEC4 §9.2).
        /// 값을 모르면 null을 돌려준다 → 아래 기본 계산(없으면 0). 여러 모듈이 답하면 앞의 것을 이어 부른다:
        /// <c>var previous = progress.ExtraConditionValue; progress.ExtraConditionValue = c =&gt; c.type == 내 종류 ? 값 : previous?.Invoke(c);</c>
        /// 조건을 읽을 때마다 불리므로 할당하지 않는다. 진행 기록기 파일은 P4-0 것이라 다른 패키지는 이 훅만 쓴다.
        /// </summary>
        public Func<MetaCondition, long?> ExtraConditionValue;

        // ── 원정 / 던전 스냅숏 ──
        struct ExpeditionSnapshot
        {
            public string ZoneId;
            public long StartUnix;
            public long EndUnix;
            /// <summary>원정 저장 객체 (ExpeditionSystem이 시계가 뒤로 가면 시작·끝 시각을 당기므로 받기/취소 판정에 지금 값을 본다)</summary>
            public ExpeditionSave Save;
            /// <summary>이 진행 기록기가 보낸 것을 셌음 (DayKey/WeekKey = 센 날·주). false = 불러온 세이브에 이미 있던 원정</summary>
            public bool Counted;
            public string DayKey;
            public string WeekKey;
        }

        readonly List<ExpeditionSnapshot> _expeditionSnapshot = new();
        readonly List<ExpeditionSnapshot> _expeditionNext = new();
        int _dungeonFloor;
        int _dungeonTicketsUsed;
        string _dungeonTicketDate;
        int _dungeonBlessings;
        string _dungeonRespecWeek;

        // ── 시너지 캐시 ──
        bool _synergyDirty = true;
        int _activeSynergies;
        int _maxSynergyTier;
        readonly Dictionary<string, int> _synergyTiers = new();

        // ── FeatureUnlocked 조건의 targetId 해석 캐시 ──
        readonly Dictionary<string, int> _featureIdCache = new();

        public ProgressTracker(PlayerData data, DailyClock clock, BattleSystem battle, GrowthSystem growth, CharacterSystem characters,
            EvolutionSystem evolution, CostumeSystem costumes, GachaSystem gacha, ShopSystem shop, ExpeditionSystem expedition,
            DungeonSystem dungeon, FormationSystem formation, SynergySystem synergy, MailSystem mail, PowerSystem power)
        {
            _data = data;
            _clock = clock;
            _battle = battle;
            _growth = growth;
            _characters = characters;
            _evolution = evolution;
            _costumes = costumes;
            _gacha = gacha;
            _shop = shop;
            _expedition = expedition;
            _dungeon = dungeon;
            _formation = formation;
            _synergy = synergy;
            _mail = mail;
            _power = power;

            if (_data == null) return;
            if (Active.TryGetValue(_data, out var previous))
            {
                previous.Detach();
                Active.Remove(_data);
            }
            Active.Add(_data, this);

            TakeExpeditionSnapshot();
            TakeDungeonSnapshot();
            Attach();
        }

        CounterSave Counters => (_data.meta ??= new MetaSave()).counters ??= new CounterSave();

        // ── 카운터 ──

        /// <summary>카운터를 더한다 (평생 + 오늘 + 이번 주, 합계 키 포함). 0 이하는 무시. 할당 없음</summary>
        public void Increment(CounterKey key, long amount = 1) => Increment(key, amount, withAggregate: true);

        void Increment(CounterKey key, long amount, bool withAggregate)
        {
            if (amount <= 0 || _data == null) return;
            var counters = Counters;
            EnsurePeriods(counters);
            AddRaw(counters, key, amount);
            _dirty = true;
            Incremented?.Invoke(key, amount);

            if (!withAggregate) return;
            var aggregate = AggregateOf(key);
            if (aggregate == key) return;
            AddRaw(counters, aggregate, amount);
            Incremented?.Invoke(aggregate, amount);
        }

        public long Get(CounterKey key, CounterScope scope = CounterScope.Lifetime)
        {
            if (_data == null) return 0;
            var counters = Counters;
            switch (scope)
            {
                case CounterScope.Daily:
                    if (_clock != null && counters.dayKey != _clock.DayKey) return 0;
                    return Read(counters.daily, key);
                case CounterScope.Weekly:
                    if (_clock != null && counters.weekKey != _clock.WeekKey) return 0;
                    return Read(counters.weekly, key);
                default:
                    return Read(counters.lifetime, key);
            }
        }

        public long GetLifetime(CounterKey key) => Get(key, CounterScope.Lifetime);

        public long GetDaily(CounterKey key) => Get(key, CounterScope.Daily);

        public long GetWeekly(CounterKey key) => Get(key, CounterScope.Weekly);

        // ── 플래그 (MetaConditionType.Flag) ──

        /// <summary>1회성 플래그 (PlayerData.flags). 처음 세우면 true + Changed 예약</summary>
        public bool SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || _data == null) return false;
            _data.flags ??= new HashSet<string>();
            if (!_data.flags.Add(flag)) return false;
            _dirty = true;
            return true;
        }

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && _data?.flags != null && _data.flags.Contains(flag);

        // ── 조건 ──

        /// <summary>조건의 현재 값 (예: StageCleared → 클리어한 최고 스테이지 = highestStage − 1, Flag → 0/1, None → 1)</summary>
        public long GetValue(MetaCondition condition)
        {
            if (condition == null || _data == null) return 1;
            switch (condition.type)
            {
                case MetaConditionType.None:
                    return 1;
                case MetaConditionType.Counter:
                    return Get(condition.counter, CounterScope.Lifetime);
                case MetaConditionType.DailyCounter:
                    return Get(condition.counter, CounterScope.Daily);
                case MetaConditionType.WeeklyCounter:
                    return Get(condition.counter, CounterScope.Weekly);
                case MetaConditionType.StageCleared:
                    return ClearedStage;
                case MetaConditionType.ShopLevel:
                    return Math.Max(1, _data.level);
                case MetaConditionType.LineageLevel:
                    return LineageLevel(condition.targetId);
                case MetaConditionType.LineageLevelSum:
                    return LineageLevelSum();
                case MetaConditionType.OwnedLineages:
                    return _data.lineages?.Count ?? 0;
                case MetaConditionType.UnlockedForms:
                    return UnlockedFormCount();
                case MetaConditionType.GuardiansPlaced:
                    return GuardianCount();
                case MetaConditionType.EchoesPlaced:
                    return EchoCount();
                case MetaConditionType.ActiveSynergies:
                    EnsureSynergies();
                    return _activeSynergies;
                case MetaConditionType.SynergyTier:
                    EnsureSynergies();
                    if (string.IsNullOrEmpty(condition.targetId)) return _maxSynergyTier;
                    return _synergyTiers.TryGetValue(condition.targetId, out int tier) ? tier : 0;
                case MetaConditionType.DungeonFloor:
                    return Math.Max(0, _data.dungeon?.highestFloor ?? 0);
                case MetaConditionType.JournalPoints:
                    return JournalPoints(condition.targetId == "weekly" ? MissionPeriod.Weekly : MissionPeriod.Daily);
                case MetaConditionType.Flag:
                    return HasFlag(condition.targetId) ? 1 : 0;
                case MetaConditionType.FtueStep:
                {
                    var ftue = _data.meta?.ftue;
                    if (ftue == null) return 0;
                    return ftue.done ? Math.Max(ftue.step, condition.target) : ftue.step;
                }
                case MetaConditionType.FtueDone:
                    return _data.meta?.ftue != null && _data.meta.ftue.done ? 1 : 0;
                case MetaConditionType.InstallDays:
                    return _clock != null ? _clock.InstallDay : 1;
                case MetaConditionType.FeatureUnlocked:
                    return IsFeatureUnlocked(condition.targetId) ? 1 : 0;
                case MetaConditionType.Power:
                    return _power != null ? _power.TeamPower : 0;
                default:
                {
                    // 4차 조건 (AffinityLevelCount · SynergyKindsSeen · CostumesOwned …): 모듈이 먼저, 모르면 세이브에서 셀 수 있는 것만
                    var extra = ExtraConditionValue;
                    if (extra != null)
                    {
                        long? value = extra(condition);
                        if (value.HasValue) return Math.Max(0, value.Value);
                    }
                    return FallbackValue(condition);
                }
            }
        }

        /// <summary>모듈이 답하지 않은 4차 조건의 기본 값 (세이브만 읽음, 할당 없음). 셀 수 없으면 0</summary>
        long FallbackValue(MetaCondition condition)
        {
            switch (condition.type)
            {
                case MetaConditionType.CostumesOwned:
                    return _data.unlockedCostumes?.Count ?? 0;
                case MetaConditionType.SynergyKindsSeen:
                {
                    var found = _data.meta?.codex?.found;
                    if (found == null) return 0;
                    long count = 0;
                    foreach (var key in found)
                    {
                        if (key != null && key.StartsWith(CodexSynergyPrefix, StringComparison.Ordinal)) count++;
                    }
                    return count;
                }
                default:
                    return 0;
            }
        }

        /// <summary>도감 시너지 항목 키 머리 (CodexSystem.Key(CodexCategory.Synergy, id) = "synergy:{id}" — "synergymax:"는 세지 않는다)</summary>
        const string CodexSynergyPrefix = "synergy:";

        /// <summary>조건 목표 값 (None/Flag/FtueDone → 1)</summary>
        public long GetTarget(MetaCondition condition)
        {
            if (condition == null) return 1;
            switch (condition.type)
            {
                case MetaConditionType.None:
                case MetaConditionType.Flag:
                case MetaConditionType.FtueDone:
                case MetaConditionType.FeatureUnlocked:
                    return 1;
                default:
                    return Math.Max(0, condition.target);
            }
        }

        /// <summary>조건 충족 여부. null 조건은 충족으로 본다</summary>
        public bool IsMet(MetaCondition condition)
        {
            if (condition == null) return true;
            switch (condition.type)
            {
                case MetaConditionType.None:
                    return true;
                case MetaConditionType.Flag:
                    return HasFlag(condition.targetId);
                case MetaConditionType.FtueDone:
                    return _data?.meta?.ftue != null && _data.meta.ftue.done;
                case MetaConditionType.FtueStep:
                {
                    var ftue = _data?.meta?.ftue;
                    return ftue != null && (ftue.done || ftue.step >= condition.target);
                }
                case MetaConditionType.FeatureUnlocked:
                    return IsFeatureUnlocked(condition.targetId);
                default:
                    return GetValue(condition) >= GetTarget(condition);
            }
        }

        /// <summary>진행 비율 0~1</summary>
        public float GetRatio(MetaCondition condition)
        {
            if (IsMet(condition)) return 1f;
            long target = GetTarget(condition);
            if (target <= 0) return 1f;
            double ratio = (double)Math.Max(0, GetValue(condition)) / target;
            return (float)Math.Max(0, Math.Min(1, ratio));
        }

        /// <summary>진행 표기 "2/3" (스테이지는 "1-3/1-4", 충족하면 목표값 기준)</summary>
        public string FormatProgress(MetaCondition condition)
        {
            if (condition == null) return "1/1";
            bool met = IsMet(condition);
            long target = GetTarget(condition);
            long value = met ? target : Math.Max(0, Math.Min(target, GetValue(condition)));
            if (condition.type == MetaConditionType.StageCleared)
            {
                string current = value >= 1 ? BattleSystem.FormatStage((int)Math.Min(int.MaxValue, value)) : "-";
                return $"{current}/{BattleSystem.FormatStage((int)Math.Min(int.MaxValue, Math.Max(1, target)))}";
            }
            return $"{MetaText.Comma(value)}/{MetaText.Comma(target)}";
        }

        /// <summary>조건 설명 기본 문구 (콘텐츠 문구가 없을 때, 예: "스테이지 1-4 클리어", "잔향이 30번 부르기")</summary>
        public string Describe(MetaCondition condition)
        {
            if (condition == null) return string.Empty;
            long n = condition.target;
            string c = MetaText.Comma(n);
            switch (condition.type)
            {
                case MetaConditionType.None:
                    return string.Empty;
                case MetaConditionType.Counter:
                    return MetaText.CounterText(condition.counter, n);
                case MetaConditionType.DailyCounter:
                    return "오늘 " + MetaText.CounterText(condition.counter, n);
                case MetaConditionType.WeeklyCounter:
                    return "이번 주 " + MetaText.CounterText(condition.counter, n);
                case MetaConditionType.StageCleared:
                    return $"스테이지 {BattleSystem.FormatStage((int)Math.Max(1, Math.Min(int.MaxValue, n)))} 클리어";
                case MetaConditionType.ShopLevel:
                    return $"수선점 Lv.{c}";
                case MetaConditionType.LineageLevel:
                {
                    string name = LineageName(condition.targetId);
                    return name != null ? $"{name} 레벨 {c}" : $"친구 레벨 {c}";
                }
                case MetaConditionType.LineageLevelSum:
                    return $"친구 레벨 합계 {c}";
                case MetaConditionType.OwnedLineages:
                    return $"친구 {c}명 모으기";
                case MetaConditionType.UnlockedForms:
                    return $"형태 {c}개 해금";
                case MetaConditionType.GuardiansPlaced:
                    return $"지킴이 {c}명 세우기";
                case MetaConditionType.EchoesPlaced:
                    return $"잔향이 덱 {c}칸 채우기";
                case MetaConditionType.ActiveSynergies:
                    return $"시너지 {c}개 켜기";
                case MetaConditionType.SynergyTier:
                {
                    var def = string.IsNullOrEmpty(condition.targetId) ? null : _synergy?.Db?.Get(condition.targetId);
                    return def != null && !string.IsNullOrEmpty(def.name) ? $"{def.name} {c}단계 달성" : $"시너지 {c}단계 달성";
                }
                case MetaConditionType.DungeonFloor:
                    return $"잔향이 던전 {c}층 클리어";
                case MetaConditionType.JournalPoints:
                    return condition.targetId == "weekly" ? $"이번 주 수선 일지 {c}점" : $"수선 일지 {c}점";
                case MetaConditionType.Flag:
                    return MetaText.FlagText(condition.targetId);
                case MetaConditionType.FtueStep:
                    return $"튜토리얼 {c}단계";
                case MetaConditionType.FtueDone:
                    return "튜토리얼 마치기";
                case MetaConditionType.InstallDays:
                    return $"설치 {c}일째";
                case MetaConditionType.FeatureUnlocked:
                    return TryParseFeature(condition.targetId, out var feature) ? $"{MetaText.FeatureName(feature)} 열기" : string.Empty;
                case MetaConditionType.Power:
                    return $"전투력 {PowerSystem.Format(n)}";
                case MetaConditionType.AffinityLevelCount:
                    return string.IsNullOrEmpty(condition.targetId) ? $"호감도 친구 {c}명" : $"호감도 Lv.{condition.targetId} 친구 {c}명";
                case MetaConditionType.SynergyKindsSeen:
                    return $"시너지 {c}종류 켜 보기";
                case MetaConditionType.CostumesOwned:
                    return $"의상 {c}벌 모으기";
                default:
                    return string.Empty;
            }
        }

        // ── 프레임 갱신 ──

        /// <summary>지난 Flush 뒤로 바뀐 것이 있는지</summary>
        public bool IsDirty => _dirty;

        /// <summary>바뀐 것이 있으면 Changed를 한 번 알린다. MetaSession.Tick이 프레임마다 부른다</summary>
        public void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// 카운터 말고 다른 값(스테이지/레벨/편성/시너지/전투력/튜토리얼/기능 개방)이 바뀌었을 수 있음을 알린다.
        /// 다음 Flush에서 Changed가 울린다. 메타 시스템이 자기 상태를 바꿨을 때도 부른다.
        /// </summary>
        public void MarkDirty() => _dirty = true;

        // ───────────────────────────── 카운터 내부 ─────────────────────────────

        /// <summary>합계 키 (없으면 자기 자신)</summary>
        static CounterKey AggregateOf(CounterKey key) => key switch
        {
            CounterKey.ShopLevelUp => CounterKey.AnyUpgrade,
            CounterKey.LineageLevelUp => CounterKey.AnyUpgrade,
            CounterKey.EvolutionCraft => CounterKey.AnyGrowthMilestone,
            CounterKey.ResonanceUp => CounterKey.AnyGrowthMilestone,
            CounterKey.CostumeUnlock => CounterKey.AnyGrowthMilestone,
            CounterKey.DungeonChallenge => CounterKey.DungeonRun,
            CounterKey.DungeonSweep => CounterKey.DungeonRun,
            _ => key,
        };

        static long Read(Dictionary<CounterKey, long> map, CounterKey key) =>
            map != null && map.TryGetValue(key, out long value) ? Math.Max(0, value) : 0;

        static void AddTo(Dictionary<CounterKey, long> map, CounterKey key, long amount)
        {
            map.TryGetValue(key, out long value);
            long next = value + amount;
            map[key] = next < value ? long.MaxValue : next; // 넘침 방지
        }

        static void AddRaw(CounterSave counters, CounterKey key, long amount)
        {
            AddTo(counters.lifetime, key, amount);
            AddTo(counters.daily, key, amount);
            AddTo(counters.weekly, key, amount);
        }

        /// <summary>오늘/이번 주 키가 지금과 다르면 비운다 (읽기 비교는 같은 문자열 참조로 끝나게 키를 맞춰 둔다)</summary>
        void EnsurePeriods(CounterSave counters)
        {
            counters.lifetime ??= new Dictionary<CounterKey, long>();
            counters.daily ??= new Dictionary<CounterKey, long>();
            counters.weekly ??= new Dictionary<CounterKey, long>();
            if (_clock == null) return;

            string day = _clock.DayKey;
            if (counters.dayKey != day)
            {
                counters.daily.Clear();
                counters.dayKey = day;
            }
            else if (!ReferenceEquals(counters.dayKey, day))
            {
                counters.dayKey = day;
            }

            string week = _clock.WeekKey;
            if (counters.weekKey != week)
            {
                counters.weekly.Clear();
                counters.weekKey = week;
            }
            else if (!ReferenceEquals(counters.weekKey, week))
            {
                counters.weekKey = week;
            }
        }

        void OnDayChanged(string dayKey)
        {
            var counters = Counters;
            if (counters.dayKey == dayKey) return;
            counters.daily ??= new Dictionary<CounterKey, long>();
            counters.daily.Clear();
            counters.dayKey = dayKey;
            _dirty = true;
        }

        void OnWeekChanged(string weekKey)
        {
            var counters = Counters;
            if (counters.weekKey == weekKey) return;
            counters.weekly ??= new Dictionary<CounterKey, long>();
            counters.weekly.Clear();
            counters.weekKey = weekKey;
            _dirty = true;
        }

        // ───────────────────────────── 조건 내부 ─────────────────────────────

        /// <summary>클리어한 최고 스테이지 (highestStage = 도달한 스테이지라 1을 뺀다)</summary>
        int ClearedStage => Math.Max(0, Math.Max(1, _data.battle?.highestStage ?? 1) - 1);

        long LineageLevel(string lineageId)
        {
            var lineages = _data.lineages;
            if (lineages == null) return 0;
            if (!string.IsNullOrEmpty(lineageId)) return lineages.TryGetValue(lineageId, out var save) && save != null ? save.level : 0;
            int best = 0;
            foreach (var pair in lineages)
            {
                if (pair.Value != null && pair.Value.level > best) best = pair.Value.level;
            }
            return best;
        }

        long LineageLevelSum()
        {
            var lineages = _data.lineages;
            if (lineages == null) return 0;
            long sum = 0;
            foreach (var pair in lineages)
            {
                if (pair.Value != null) sum += Math.Max(0, pair.Value.level);
            }
            return sum;
        }

        long UnlockedFormCount()
        {
            var lineages = _data.lineages;
            if (lineages == null) return 0;
            var db = _characters?.Db;
            long count = 0;
            foreach (var pair in lineages)
            {
                var save = pair.Value;
                if (save == null) continue;
                int forms = save.unlockedForms?.Count ?? 0;
                // 옛 세이브에는 기본형이 목록에 없을 수 있다 (기본형은 늘 해금)
                var def = db != null ? db.GetLineage(pair.Key) : null;
                if (def != null && !string.IsNullOrEmpty(def.baseFormId) && (save.unlockedForms == null || !save.unlockedForms.Contains(def.baseFormId)))
                    forms++;
                count += forms;
            }
            return count;
        }

        int GuardianCount()
        {
            if (_formation == null) return 0;
            int count = 0;
            for (int slot = 0; slot < FormationSystem.GuardianSlotCount; slot++)
            {
                if (_formation.GetGuardian(slot) != null) count++;
            }
            return count;
        }

        int EchoCount()
        {
            if (_formation == null) return 0;
            int count = 0;
            for (int slot = 0; slot < FormationSystem.EchoSlotCount; slot++)
            {
                if (_formation.GetEcho(slot) != null) count++;
            }
            return count;
        }

        long JournalPoints(MissionPeriod period)
        {
            var missions = _data.meta?.missions;
            if (missions == null) return 0;
            var save = missions.Get(period);
            if (save == null) return 0;
            if (_clock != null)
            {
                string key = period == MissionPeriod.Weekly ? _clock.WeekKey : _clock.DayKey;
                if (save.periodKey != key) return 0;
            }
            return Math.Max(0, save.points);
        }

        string LineageName(string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId)) return null;
            var def = _characters?.Db != null ? _characters.Db.GetLineage(lineageId) : null;
            return def != null && !string.IsNullOrEmpty(def.name) ? def.name : lineageId;
        }

        bool IsFeatureUnlocked(string featureName)
        {
            if (!TryParseFeature(featureName, out var feature)) return false;
            if (FeatureResolver != null) return FeatureResolver(feature);
            var unlocked = _data?.meta?.features?.unlocked;
            return unlocked != null && unlocked.Contains(feature);
        }

        /// <summary>FeatureId 이름 해석 (한 번 해석한 이름은 캐시 — 매번 Enum.TryParse 하지 않게)</summary>
        bool TryParseFeature(string name, out FeatureId feature)
        {
            feature = FeatureId.Battle;
            if (string.IsNullOrEmpty(name)) return false;
            if (!_featureIdCache.TryGetValue(name, out int value))
            {
                value = Enum.TryParse(name, out FeatureId parsed) && Enum.IsDefined(typeof(FeatureId), parsed) ? (int)parsed : -1;
                _featureIdCache[name] = value;
            }
            if (value < 0) return false;
            feature = (FeatureId)value;
            return true;
        }

        /// <summary>켜진 시너지 수 / 단계 캐시를 다시 만든다 (Synergy.Changed 뒤 처음 읽을 때만)</summary>
        void EnsureSynergies()
        {
            if (!_synergyDirty) return;
            _synergyDirty = false;
            _activeSynergies = 0;
            _maxSynergyTier = 0;
            _synergyTiers.Clear();
            if (_synergy == null) return;
            List<SynergyStatus> active;
            try
            {
                active = _synergy.GetActive();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Progress] 시너지 상태를 읽지 못했어요: {e.Message}");
                return;
            }
            if (active == null) return;
            foreach (var status in active)
            {
                if (status == null || !status.IsActive || status.Def == null) continue;
                int tier = status.TierIndex + 1;
                _activeSynergies++;
                if (tier > _maxSynergyTier) _maxSynergyTier = tier;
                if (!string.IsNullOrEmpty(status.Def.id))
                {
                    _synergyTiers.TryGetValue(status.Def.id, out int previous);
                    if (tier > previous) _synergyTiers[status.Def.id] = tier;
                }
            }
        }

        // ───────────────────────────── 구독 ─────────────────────────────

        void Attach()
        {
            if (_attached) return;
            _attached = true;
            if (_clock != null)
            {
                _clock.DayChanged += OnDayChanged;
                _clock.WeekChanged += OnWeekChanged;
            }
            if (_battle != null)
            {
                _battle.Event += OnBattleEvent;
                _battle.StageEnded += OnStageEnded;
            }
            if (_growth != null) _growth.LevelChanged += OnShopLevelChanged;
            if (_characters != null)
            {
                _characters.LevelUp += OnLineageLevelUp;
                _characters.ResonanceUp += OnResonanceUp;
                _characters.Changed += MarkDirty;
            }
            if (_evolution != null) _evolution.FormUnlocked += OnFormUnlocked;
            if (_costumes != null)
            {
                _costumes.CostumeUnlocked += OnCostumeUnlocked;
                _costumes.CostumeEquipped += OnCostumeEquipped;
            }
            if (_gacha != null) _gacha.Pulled += OnGachaPulled;
            if (_shop != null) _shop.Purchased += OnShopPurchased;
            if (_mail != null) _mail.Claimed += OnMailClaimed;
            if (_expedition != null) _expedition.Changed += OnExpeditionChanged;
            if (_dungeon != null) _dungeon.Changed += OnDungeonChanged;
            if (_formation != null) _formation.Changed += MarkDirty;
            if (_synergy != null) _synergy.Changed += OnSynergyChanged;
            if (_power != null) _power.Changed += OnPowerChanged;
        }

        /// <summary>구독을 모두 푼다 (같은 PlayerData로 새 진행 기록기가 만들어졌을 때)</summary>
        void Detach()
        {
            if (!_attached) return;
            _attached = false;
            if (_clock != null)
            {
                _clock.DayChanged -= OnDayChanged;
                _clock.WeekChanged -= OnWeekChanged;
            }
            if (_battle != null)
            {
                _battle.Event -= OnBattleEvent;
                _battle.StageEnded -= OnStageEnded;
            }
            if (_growth != null) _growth.LevelChanged -= OnShopLevelChanged;
            if (_characters != null)
            {
                _characters.LevelUp -= OnLineageLevelUp;
                _characters.ResonanceUp -= OnResonanceUp;
                _characters.Changed -= MarkDirty;
            }
            if (_evolution != null) _evolution.FormUnlocked -= OnFormUnlocked;
            if (_costumes != null)
            {
                _costumes.CostumeUnlocked -= OnCostumeUnlocked;
                _costumes.CostumeEquipped -= OnCostumeEquipped;
            }
            if (_gacha != null) _gacha.Pulled -= OnGachaPulled;
            if (_shop != null) _shop.Purchased -= OnShopPurchased;
            if (_mail != null) _mail.Claimed -= OnMailClaimed;
            if (_expedition != null) _expedition.Changed -= OnExpeditionChanged;
            if (_dungeon != null) _dungeon.Changed -= OnDungeonChanged;
            if (_formation != null) _formation.Changed -= MarkDirty;
            if (_synergy != null) _synergy.Changed -= OnSynergyChanged;
            if (_power != null) _power.Changed -= OnPowerChanged;
        }

        void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.EnemyKilled:
                    Increment(CounterKey.EnemyKill);
                    break;
                case BattleEventType.EchoSummoned:
                    Increment(CounterKey.EchoSummon);
                    break;
                case BattleEventType.LampLit:
                    Increment(CounterKey.LampLit);
                    break;
                case BattleEventType.SynergyActivated:
                    if (IsMaxTier(e.Key, (int)e.Value)) Increment(CounterKey.SynergyTierMax);
                    break;
            }
        }

        bool IsMaxTier(string synergyId, int tierIndex)
        {
            var def = string.IsNullOrEmpty(synergyId) ? null : _synergy?.Db?.Get(synergyId);
            return def?.tiers != null && def.tiers.Count > 0 && tierIndex >= def.tiers.Count - 1;
        }

        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            if (outcome == BattleOutcome.Cleared)
            {
                Increment(CounterKey.StageClear);
                Increment(CounterKey.BossKill);
            }
            else if (outcome == BattleOutcome.Failed)
            {
                Increment(CounterKey.StageFail);
            }
            _dirty = true;
        }

        void OnShopLevelChanged(int oldLevel, int newLevel)
        {
            if (newLevel > oldLevel) Increment(CounterKey.ShopLevelUp, newLevel - oldLevel);
            _dirty = true;
        }

        void OnLineageLevelUp(string lineageId, int level) => Increment(CounterKey.LineageLevelUp);

        void OnResonanceUp(string lineageId, int level) => Increment(CounterKey.ResonanceUp);

        void OnFormUnlocked(CharacterFormDef form) => Increment(CounterKey.EvolutionCraft);

        void OnCostumeUnlocked(string costumeId) => Increment(CounterKey.CostumeUnlock);

        void OnCostumeEquipped(string lineageId, string costumeId) => Increment(CounterKey.CostumeEquip);

        void OnGachaPulled(GachaTable table, IReadOnlyList<GachaResult> results)
        {
            if (results != null && results.Count > 0) Increment(CounterKey.GachaPull, results.Count);
        }

        void OnShopPurchased(ShopProductDef product) => Increment(CounterKey.ShopBuy);

        void OnMailClaimed(int count)
        {
            if (count > 0) Increment(CounterKey.MailClaim, count);
        }

        void OnSynergyChanged()
        {
            _synergyDirty = true;
            _dirty = true;
        }

        void OnPowerChanged(long oldPower, long newPower) => _dirty = true;

        // ── 원정 추론 ──

        /// <summary>처음 스냅숏 (생성자). 불러온 세이브에 이미 있던 원정은 이 기록기가 센 것이 아니라 Counted = false</summary>
        void TakeExpeditionSnapshot()
        {
            _expeditionSnapshot.Clear();
            var list = _data.expeditions;
            if (list == null) return;
            foreach (var expedition in list)
            {
                if (expedition == null || string.IsNullOrEmpty(expedition.zoneId)) continue;
                _expeditionSnapshot.Add(SnapshotOf(expedition));
            }
        }

        static ExpeditionSnapshot SnapshotOf(ExpeditionSave expedition) => new ExpeditionSnapshot
        {
            ZoneId = expedition.zoneId,
            StartUnix = expedition.startUnix,
            EndUnix = expedition.endUnix,
            Save = expedition,
        };

        /// <summary>
        /// 원정 목록이 바뀜: 새로 생긴 원정 = 보냄, 사라진 원정 중 끝날 시각이 지난 것 = 받음, 끝나기 전에 사라진 것 = 취소
        /// (취소는 보냄 기록을 되돌린다 — UndoExpeditionStart). 같은 구역이 남아 있어도 시작 시각이 이전 원정의 끝보다 늦으면
        /// 받고 다시 보낸 것으로 본다. 남은 원정은 보낸 것을 센 날·주를 다음 스냅숏으로 이어 받는다. 할당 없음.
        /// </summary>
        void OnExpeditionChanged()
        {
            long now = _clock != null ? _clock.NowUnix : 0;
            var list = _data.expeditions;
            int started = 0;
            int claimed = 0;

            for (int i = 0; i < _expeditionSnapshot.Count; i++)
            {
                var old = _expeditionSnapshot[i];
                var current = FindExpedition(list, old.ZoneId);
                bool gone = current == null || current.startUnix > old.EndUnix;
                if (!gone) continue;
                if (EndOf(old) <= now) claimed++;
                else UndoExpeditionStart(old);
            }

            // 새로 보낸 원정은 Increment가 셀 날·주(시계의 지금 키)를 기록해 둔다
            string dayKey = _clock?.DayKey;
            string weekKey = _clock?.WeekKey;
            _expeditionNext.Clear();
            if (list != null)
            {
                foreach (var expedition in list)
                {
                    if (expedition == null || string.IsNullOrEmpty(expedition.zoneId)) continue;
                    var entry = SnapshotOf(expedition);
                    int index = FindSnapshot(expedition.zoneId);
                    if (index < 0 || expedition.startUnix > _expeditionSnapshot[index].EndUnix)
                    {
                        started++;
                        entry.Counted = true;
                        entry.DayKey = dayKey;
                        entry.WeekKey = weekKey;
                    }
                    else
                    {
                        var previous = _expeditionSnapshot[index];
                        entry.Counted = previous.Counted;
                        entry.DayKey = previous.DayKey;
                        entry.WeekKey = previous.WeekKey;
                    }
                    _expeditionNext.Add(entry);
                }
            }
            _expeditionSnapshot.Clear();
            for (int i = 0; i < _expeditionNext.Count; i++) _expeditionSnapshot.Add(_expeditionNext[i]);
            _expeditionNext.Clear();

            if (started > 0) Increment(CounterKey.ExpeditionStart, started);
            if (claimed > 0) Increment(CounterKey.ExpeditionClaim, claimed);
            _dirty = true;
        }

        /// <summary>
        /// 원정이 끝나는 시각. 시계가 시작 시각보다 뒤로 가면 ExpeditionSystem이 저장 객체의 시작·끝을 당기므로
        /// 스냅숏 값과 저장 객체의 지금 값 중 이른 쪽 (당겨진 원정을 받았는데 취소로 보지 않게)
        /// </summary>
        static long EndOf(in ExpeditionSnapshot snapshot) =>
            snapshot.Save != null ? Math.Min(snapshot.EndUnix, snapshot.Save.endUnix) : snapshot.EndUnix;

        /// <summary>
        /// 취소된 원정의 보냄(ExpeditionStart)을 되돌린다. 평생 값은 늘 1 빼고, 오늘/이번 주 값은 보낸 것을 센 날/주가 지금과 같을 때만 뺀다
        /// (어제 보낸 원정을 오늘 취소해도 오늘 미션이 줄지 않게). 불러온 세이브에 있던 원정은 보낸 시각이 속한 날·주로 센 것으로 본다.
        /// 0 아래로는 내려가지 않는다.
        /// </summary>
        void UndoExpeditionStart(in ExpeditionSnapshot old)
        {
            if (_data == null) return;
            var counters = Counters;
            EnsurePeriods(counters);

            string day = old.DayKey;
            string week = old.WeekKey;
            if (!old.Counted && _clock != null)
            {
                // 취소할 때만 계산한다 (드묾 — 다른 날이면 문자열을 만든다)
                day = _clock.DayKeyOfUnix(old.StartUnix);
                week = DailyClock.WeekKeyOf(_clock.ToLocal(old.StartUnix));
            }

            SubtractFrom(counters.lifetime, CounterKey.ExpeditionStart, 1);
            if (_clock == null || day == counters.dayKey) SubtractFrom(counters.daily, CounterKey.ExpeditionStart, 1);
            if (_clock == null || week == counters.weekKey) SubtractFrom(counters.weekly, CounterKey.ExpeditionStart, 1);
            _dirty = true;
        }

        static void SubtractFrom(Dictionary<CounterKey, long> map, CounterKey key, long amount)
        {
            if (map == null || !map.TryGetValue(key, out long value) || value <= 0) return;
            map[key] = Math.Max(0, value - amount);
        }

        static ExpeditionSave FindExpedition(List<ExpeditionSave> list, string zoneId)
        {
            if (list == null) return null;
            foreach (var expedition in list)
            {
                if (expedition != null && expedition.zoneId == zoneId) return expedition;
            }
            return null;
        }

        int FindSnapshot(string zoneId)
        {
            for (int i = 0; i < _expeditionSnapshot.Count; i++)
            {
                if (_expeditionSnapshot[i].ZoneId == zoneId) return i;
            }
            return -1;
        }

        // ── 던전 추론 ──

        void TakeDungeonSnapshot()
        {
            var dungeon = _data.dungeon;
            _dungeonFloor = dungeon != null ? Math.Max(0, dungeon.highestFloor) : 0;
            _dungeonTicketsUsed = dungeon != null ? Math.Max(0, dungeon.ticketsUsed) : 0;
            _dungeonTicketDate = dungeon?.ticketDate;
            _dungeonBlessings = dungeon?.blessings?.Count ?? 0;
            _dungeonRespecWeek = dungeon?.respecWeekKey;
        }

        /// <summary>
        /// 던전이 바뀜: 층이 오르면 도전 + 돌파, 층은 그대로인데 입장권을 썼으면 쓴 장 수만큼 소탕 (SweepMany는 n장에 Changed 한 번),
        /// 잔향 축복을 고르거나 다시 골랐으면 아무것도 세지 않음, 모두 그대로면 실패한 도전
        /// (DungeonSystem은 실패한 도전에 입장권을 쓰지 않고, Changed는 도전 · 소탕 · 축복 고르기 · 다시 고르기 때 울린다).
        /// </summary>
        void OnDungeonChanged()
        {
            var dungeon = _data.dungeon;
            if (dungeon == null) return;
            int floor = Math.Max(0, dungeon.highestFloor);
            int used = Math.Max(0, dungeon.ticketsUsed);
            bool newTicketDay = dungeon.ticketDate != _dungeonTicketDate;
            bool ticketUsed = newTicketDay ? used > 0 : used > _dungeonTicketsUsed;

            if (floor > _dungeonFloor)
            {
                int cleared = floor - _dungeonFloor;
                TakeDungeonSnapshot();
                Increment(CounterKey.DungeonChallenge);
                Increment(CounterKey.DungeonClear, cleared);
            }
            else if (ticketUsed)
            {
                // 한 번에 소탕(IDLE-05)은 n장을 쓰고 Changed를 한 번 울린다 → n번 소탕 (SPEC4 §5.2, DungeonRun 미션 · 퀘스트도 n)
                int swept = newTicketDay ? used : used - _dungeonTicketsUsed;
                TakeDungeonSnapshot();
                Increment(CounterKey.DungeonSweep, Math.Max(1, swept));
            }
            else if ((dungeon.blessings?.Count ?? 0) != _dungeonBlessings || dungeon.respecWeekKey != _dungeonRespecWeek)
            {
                // 잔향 축복 고르기 · 다시 고르기 (PRG-07): 도전이 아니다
                TakeDungeonSnapshot();
            }
            else
            {
                // 실패한 도전은 입장권을 쓰지 않아 공짜로 되풀이할 수 있다 — 도전 기록만 남기고
                // "던전 N번" 미션(DungeonRun)에는 넣지 않는다 (DungeonSystem이 출격 기록을 세지 않는 것과 같은 이유)
                TakeDungeonSnapshot();
                Increment(CounterKey.DungeonChallenge, 1, withAggregate: false);
            }
            _dirty = true;
        }
    }
}
