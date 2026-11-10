using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>바구니 한 줄의 시간당 양 (GetRates가 채운다)</summary>
    public struct IdleChestRate
    {
        /// <summary>Currency/Item, amount = 지금 최고 스테이지 기준 시간당 양</summary>
        public MetaRewardType Type;
        public CurrencyType Currency;
        public string ItemId;
        public double PerHour;
        /// <summary>단추 줄: 앱을 닫아 둔 동안만 쌓임</summary>
        public bool OfflineOnly;
    }

    /// <summary>바구니 받기 / 빠른 수선 결과</summary>
    public sealed class IdleChestClaimResult
    {
        /// <summary>실제로 받은 보상 (같은 종류 합침)</summary>
        public List<MetaReward> Rewards = new();
        /// <summary>재료·별조각 줄이 쌓인 시간 (빠른 수선은 quickIdleHours)</summary>
        public TimeSpan Duration;
        /// <summary>단추 줄이 쌓인 시간 (앱을 닫아 둔 시간, 상한까지)</summary>
        public TimeSpan OfflineDuration;
        public bool Doubled;
        /// <summary>두 배에 두 배 수선권을 썼는지 (false면 무료 두 배)</summary>
        public bool UsedDoubleTicket;
        public bool IsQuickIdle;
        /// <summary>빠른 수선에 쓴 별조각 (0 = 무료 또는 수선권)</summary>
        public double GemSpent;
        public bool UsedQuickTicket;
    }

    /// <summary>
    /// 수선 바구니 (IDLE-01) + 빠른 수선 (IDLE-02) + 복귀 화면 두 배 받기 (IDLE-03). 기존 OfflineRewardSystem의 단추 보상을 대신한다.
    ///   · 재료·별조각 줄(IdleChestConfig.lines)은 항상 쌓인다: 마지막 받기(meta.idleChest.lastClaimUnix)부터 상한(Cap)까지.
    ///     첫 개방 때 lastClaimUnix가 0이면 그때부터 시작한다.
    ///   · 단추 줄은 앱을 닫아 둔 동안만 쌓인다 (AddOfflineTime, 상한까지): 초당 단추 × 초 × offlineGoldRate. 켜 둔 동안은 이미 초당 단추를 받는다.
    ///   · 시간당 양 = 줄 amount × stageGrowth^(최고 스테이지 − 1), 받는 순간의 최고 스테이지 기준. minStage 미만 줄은 0, eventOnly 줄은 이벤트 전까지 0.
    ///     줄에 maxStageMultiplier(0 = 없음)가 있으면 스테이지 배율을 그 값에서 자른다 — 별조각 줄 1.5 (SPEC4 §13.4 C-01). 빠른 수선 · 복귀 두 배 ·
    ///     다시 만난 날 24시간 상한도 모두 이 시간당 양을 읽으므로 같은 천장을 따른다.
    ///     소수점은 줄마다 carry로 넘겨 결정적으로 처리한다 (재료·별조각은 정수로 지급).
    ///   · 상한 = baseCapHours와, capSteps 중 0이 아닌 조건(stageCleared · shopLevel)이 모두 맞는 단계의 capHours 가운데 가장 큰 값
    ///     (8시간 → 2-1 클리어 12시간 → 수선점 Lv.200 16시간). 다시 만난 날(CR-06)이 GrantBonusCap으로 한 번짜리 상한을 주면
    ///     다음 받기 때까지 상한 = max(단계 상한, 그 값) (idleChest.bonusCapSeconds, 받으면 0).
    ///   · 이벤트 (EVT-01, EventCalendarSystem이 훅을 넣는다): 실제로 쌓인 구간 [마지막 받기, 마지막 받기 + 쌓인 시간)에 대해
    ///     밤의 수선점 덤 시간 = 겹친 시간(NightBonusHours) × (배율 − 1)(NightBonusMultiplier, 1.2 → 20%)을 재료 · 별조각 줄(단추 재화 줄 · 이벤트 전용 줄 제외)에 더하고,
    ///     이벤트 전용 줄(eventOnly, 별빛 사탕)은 별이 머문 밤 산책과 겹친 시간(EventLineHours)만큼 쌓인다. 빠른 수선에는 붙지 않는다.
    ///   · 빠른 수선: 모든 줄의 quickIdleHours시간치 (단추 줄도 quickIdleHours × 초당 단추 × offlineGoldRate)를 바로 받고, 바구니 타이머는 그대로.
    ///     하루 quickIdleGemCosts.Length번 (1번째 무료, 2번째 50, 3번째 100 별조각). 빠른 수선권은 하루 제한과 별개.
    ///   · 두 배(복귀 화면): 하루 freeDoublePerDay번 무료 (meta.returnScreen.doubleClaimedDayKey), 그 뒤엔 두 배 수선권 1장. 별조각은 두 배에서 제외.
    ///   · 받기 = ChestClaim 카운터, 빠른 수선 = QuickIdle 카운터. 되돌림(IsTampered) 중에는 받기·무료 빠른 수선을 막는다.
    ///   · 수선 바구니(FeatureId.IdleChest)가 잠겨 있으면 쌓이지 않는다 (lastClaimUnix를 0으로 두고, 열리는 순간 시작).
    /// UI 타이머는 Elapsed / FillRatio를 폴링한다 (초마다 이벤트 없음). Changed는 받기·빠른 수선·오프라인 시간 추가·상한 변화 때만.
    /// <para>
    /// 시각은 DailyClock.EffectiveUnix(= max(기기 시각, 본 적 있는 가장 늦은 시각))를 쓴다 — 시계를 되돌리면 바구니가 멈춰 있고
    /// 앞으로 돌려도 상한에서 멈춘다. "최고 스테이지"는 도달한 최고 스테이지(highestStage)다.
    /// 받을 것이 하나도 없으면(모든 줄이 1개 미만이고 단추 줄도 없음) CanClaim은 false — 빈 받기로 "바구니 받기" 미션을 채울 수 없다.
    /// 빠른 수선도 같은 carry를 쓴다 (기억의 실처럼 2시간에 1개가 안 되는 줄도 언젠가 채워진다).
    /// 무료 두 배 하루 횟수는 freeDoublePerDay가 1 이상이면 하루 1번 (저장 칸이 날 키 하나뿐).
    /// </para>
    /// </summary>
    public class IdleChestSystem
    {
        /// <summary>바구니 상태가 바뀜 (받기, 빠른 수선, 오프라인 시간 추가, 상한 변화, 개방)</summary>
        public event Action Changed;
        /// <summary>받음 (보상 날아가기 / 바구니 흔들림)</summary>
        public event Action<IdleChestClaimResult> Claimed;

        /// <summary>빠른 수선 문구</summary>
        public const string QuickIdleMessage = "친구들이 밤새 할 일을 지금 바로 끝냈어요!";
        /// <summary>단추 줄 carry 키</summary>
        const string GoldCarryKey = "offline:Gold";
        /// <summary>한 번짜리 상한(GrantBonusCap)의 최대 (시간) — 콘텐츠는 24</summary>
        const double MaxBonusCapHours = 72;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly GameConfig _config;
        readonly DailyClock _clock;
        readonly GrowthSystem _growth;
        readonly CurrencySystem _currency;
        readonly MetaRewardService _rewards;
        readonly ProgressTracker _progress;
        readonly FeatureGateSystem _features;

        IdleChestConfig _fallbackConfig;
        long _lastCapSeconds = -1;
        bool _lastUnlocked;
        readonly List<MetaReward> _scratch = new();

        public IdleChestSystem(PlayerData data, MetaDatabase db, GameConfig config, DailyClock clock, GrowthSystem growth,
            CurrencySystem currency, MetaRewardService rewards, ProgressTracker progress, FeatureGateSystem features)
        {
            _data = data;
            _db = db;
            _config = config;
            _clock = clock;
            _growth = growth;
            _currency = currency;
            _rewards = rewards;
            _progress = progress;
            _features = features;

            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
            if (_progress != null) _progress.Changed += OnProgressChanged;
            if (_clock != null)
            {
                _clock.DayChanged += OnDayChanged;
                _clock.TamperChanged += OnTamperChanged;
            }
        }

        IdleChestSave Save
        {
            get
            {
                var save = (_data.meta ??= new MetaSave()).idleChest ??= new IdleChestSave();
                save.carry ??= new Dictionary<string, double>();
                return save;
            }
        }

        /// <summary>바구니 설정 (콘텐츠가 없으면 GameConfig 오프라인 값으로 만든 기본값)</summary>
        IdleChestConfig Cfg
        {
            get
            {
                if (_db != null && _db.idleChest != null) return _db.idleChest;
                if (_fallbackConfig == null)
                {
                    _fallbackConfig = new IdleChestConfig();
                    if (_config != null)
                    {
                        _fallbackConfig.baseCapHours = Math.Max(0.1f, _config.offlineMaxHours);
                        _fallbackConfig.offlineGoldRate = _config.offlineRewardRate;
                        _fallbackConfig.minClaimSeconds = _config.offlineMinSeconds;
                    }
                }
                return _fallbackConfig;
            }
        }

        long Now => _clock != null ? _clock.EffectiveUnix : 0;

        bool Tampered => _clock != null && _clock.IsTampered;

        int HighestStage => Math.Max(1, _data.battle?.highestStage ?? 1);

        /// <summary>수선 바구니가 열렸는지 (FeatureId.IdleChest)</summary>
        public bool IsUnlocked => _features == null || _features.IsUnlocked(FeatureId.IdleChest);

        // ── 바구니 ──

        /// <summary>지금 상한</summary>
        public TimeSpan Cap => TimeSpan.FromSeconds(CapSeconds);

        /// <summary>재료·별조각 줄이 쌓인 시간 (상한에서 자름). 위젯 "6:12"</summary>
        public TimeSpan Elapsed => TimeSpan.FromSeconds(ElapsedSeconds);

        /// <summary>단추 줄이 쌓인 시간 (앱을 닫아 둔 시간, 상한에서 자름)</summary>
        public TimeSpan OfflineElapsed => TimeSpan.FromSeconds(OfflineSeconds);

        /// <summary>Elapsed / Cap (0~1). 위젯 채움 막대</summary>
        public float FillRatio
        {
            get
            {
                long cap = CapSeconds;
                return cap <= 0 ? 0f : (float)Math.Max(0, Math.Min(1, (double)ElapsedSeconds / cap));
            }
        }

        public bool IsFull => IsUnlocked && CapSeconds > 0 && ElapsedSeconds >= CapSeconds;

        /// <summary>바구니 그림 단계 0~3 (artStageThresholds 기준) — Art/UI/idle_basket_{0..3}.png</summary>
        public int ArtStage
        {
            get
            {
                var thresholds = Cfg.artStageThresholds;
                if (thresholds == null || thresholds.Length == 0) return 0;
                float ratio = FillRatio;
                int stage = 0;
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (ratio >= thresholds[i] - 1e-6f) stage = i + 1;
                }
                return Math.Min(3, stage);
            }
        }

        /// <summary>
        /// 빨간 점 조건 (M32): 열림 + 되돌림 아님 + (가득 또는 redDotHours 이상 쌓임).
        /// 무료 빠른 수선은 점 대신 위젯의 "무료" 표(ShowsFreeTag)로 알린다 — 받을 것이 없는데 점이 켜져 헛걸음하지 않게.
        /// </summary>
        public bool HasRedDot
        {
            get
            {
                if (!IsUnlocked || Tampered) return false;
                return IsFull || ElapsedSeconds >= Cfg.redDotHours * 3600.0;
            }
        }

        /// <summary>위젯 "무료" 표 (오늘 무료 빠른 수선이 남았고 빨간 점은 꺼져 있음 — 표는 한 번에 하나만)</summary>
        public bool ShowsFreeTag => IsUnlocked && HasFreeQuickIdle && !HasRedDot;

        /// <summary>
        /// 지금 받으면 들어올 단추 (단추 줄 + 단추 재화 줄, 내림 정수, 할당 없음). 되돌림 중이거나 잠겼으면 0.
        /// 단추 부족 안내의 " · 바구니를 받아 보세요"(MetaText.GoldShortfall)가 읽는다.
        /// </summary>
        public double PendingGold
        {
            get
            {
                if (!IsUnlocked || Tampered) return 0;
                double gold = OfflineGoldAmount();
                var lines = Cfg.lines;
                if (lines == null) return gold;
                long elapsed = ElapsedSeconds;
                if (elapsed <= 0) return gold;
                double hours = elapsed / 3600.0;
                double eventHours = WindowEventHours(elapsed);
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line?.reward == null || line.reward.type != MetaRewardType.Currency || line.reward.currency != CurrencyType.Gold) continue;
                    double perHour = PerHour(line);
                    if (perHour <= 0) continue;
                    double lineHours = line.eventOnly ? eventHours : hours;
                    if (lineHours <= 0) continue;
                    gold += Math.Floor(perHour * lineHours + Carry(line.Key) + 1e-9);
                }
                return gold;
            }
        }

        /// <summary>다음 스테이지를 클리어하면 시간당 양이 늘어나는 비율(%) — "다음 스테이지 클리어 시 +2%"</summary>
        public float NextStageBonusPercent => (float)Math.Max(0, (Cfg.stageGrowth - 1.0) * 100.0);

        /// <summary>
        /// 스테이지 배율 천장(maxStageMultiplier)에 닿은 줄이 있는지 (C-01 — 별조각 줄). 그러면 성장 문구가
        /// '다음 스테이지 클리어 시 재료 +2%'가 된다 (P4-6). 할당 없음
        /// </summary>
        public bool AnyLineAtStageCap
        {
            get
            {
                var lines = Cfg.lines;
                if (lines == null) return false;
                double multiplier = StageMultiplier;
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line != null && line.maxStageMultiplier > 0 && multiplier >= line.maxStageMultiplier) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 아직 맞지 않은 상한 단계 중 수선점 레벨만 모자란 다음 단계 (상한 안내 '수선점 Lv.200이 되면 바구니가 16시간까지 늘어나요', SPEC4 C.6).
        /// 지금 단계 상한보다 긴 단계 중 shopLevel이 가장 낮은 것. 없으면 false. 할당 없음
        /// </summary>
        public bool TryGetNextShopLevelCap(out int shopLevel, out float capHours)
        {
            shopLevel = 0;
            capHours = 0f;
            var steps = Cfg.capSteps;
            if (steps == null) return false;
            double current = StepCapHours();
            int cleared = HighestStage - 1;
            int level = ShopLevel;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null || step.shopLevel <= 0 || level >= step.shopLevel || step.capHours <= current) continue;
                if (step.stageCleared > 0 && cleared < step.stageCleared) continue;
                if (shopLevel == 0 || step.shopLevel < shopLevel)
                {
                    shopLevel = step.shopLevel;
                    capHours = step.capHours;
                }
            }
            return shopLevel > 0;
        }

        // ───────────── 4차 훅 (SPEC4 §2.5 · §8.2 · §8.3) ─────────────

        /// <summary>
        /// [시작, 끝) 유닉스 초가 밤의 수선점과 겹친 시간 (시간 단위 — 순수 겹침, EventCalendarSystem.NightBonusHours가 넣는다). null = 0 (지금).
        /// 덤 시간 = 겹친 시간 × (NightBonusMultiplier − 1) (예: 겹친 5시간 × 0.2 = 1시간)을 재료 · 별조각 줄(단추 재화 줄 · 이벤트 전용 줄 제외)의 쌓인 시간에 더한다
        /// </summary>
        public Func<long, long, double> NightBonusHours;

        /// <summary>밤의 수선점 바구니 배율 (예 1.2, EventCalendarSystem.NightBonusMultiplier가 넣는다). null 또는 1 이하 = 덤 없음</summary>
        public Func<double> NightBonusMultiplier;

        /// <summary>[시작, 끝) 중 이벤트 전용 줄(eventOnly)이 쌓이는 시간 (시간 단위, EventCalendarSystem.EventLineHours가 넣는다). null = 0 (지금처럼 쌓이지 않음)</summary>
        public Func<long, long, double> EventLineHours;

        /// <summary>지금 걸린 다시 만난 날 한 번짜리 상한 (0 = 없음, 다음 받기 때 0)</summary>
        public TimeSpan BonusCap => TimeSpan.FromSeconds(Math.Max(0, Save.bonusCapSeconds));

        /// <summary>
        /// 다시 만난 날의 한 번짜리 상한 (CR-06): 다음 받기 때까지 상한 = max(단계 상한, hours) — 비운 시간을 넣기 전에 부른다 (idleChest.bonusCapSeconds).
        /// 이미 더 긴 한 번짜리 상한이 있으면 그대로 둔다. 잠겼거나 0 이하면 무시
        /// </summary>
        public void GrantBonusCap(double hours)
        {
            if (!(hours > 0) || double.IsInfinity(hours) || !IsUnlocked) return;
            EnsureStarted();
            var save = Save;
            long seconds = (long)Math.Round(Math.Min(hours, MaxBonusCapHours) * 3600.0);
            if (seconds <= save.bonusCapSeconds) return;
            save.bonusCapSeconds = seconds;
            _lastCapSeconds = CapSeconds;
            Changed?.Invoke();
        }

        /// <summary>
        /// 앱을 닫아 둔 시간을 더한다 (단추 줄). MetaSession이 부팅·복귀 때 비운 시간으로 부른다.
        /// 열리기 전이거나 0 이하면 무시. 상한을 넘는 양은 버린다.
        /// </summary>
        public void AddOfflineTime(long seconds)
        {
            if (seconds <= 0 || !IsUnlocked) return;
            EnsureStarted();
            var save = Save;
            double cap = CapSeconds;
            double before = save.offlineSeconds;
            save.offlineSeconds = Math.Min(cap, Math.Max(0, before) + seconds);
            if (save.offlineSeconds != before) Changed?.Invoke();
        }

        /// <summary>줄마다 지금 시간당 양을 채운다 (into를 비우고). 팝업 "시간당 양" 표. 이벤트 전용 줄은 그 이벤트가 지금 열려 있을 때만</summary>
        public void GetRates(List<IdleChestRate> into)
        {
            if (into == null) return;
            into.Clear();
            var lines = Cfg.lines;
            if (lines != null)
            {
                bool eventLineOn = EventLineActiveNow;
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line != null && line.eventOnly && !eventLineOn) continue;
                    double perHour = PerHour(line);
                    if (perHour <= 0) continue;
                    into.Add(new IdleChestRate
                    {
                        Type = line.reward.type,
                        Currency = line.reward.currency,
                        ItemId = line.reward.itemId,
                        PerHour = perHour,
                    });
                }
            }
            double goldPerHour = GoldPerHour;
            if (goldPerHour > 0)
            {
                into.Add(new IdleChestRate
                {
                    Type = MetaRewardType.Currency,
                    Currency = CurrencyType.Gold,
                    PerHour = goldPerHour,
                    OfflineOnly = true,
                });
            }
        }

        /// <summary>지금 받으면 받을 것 (지급하지 않음, into를 비우고 채움, 정수 내림 미리보기)</summary>
        public void GetPending(List<MetaReward> into)
        {
            if (into == null) return;
            into.Clear();
            if (!IsUnlocked) return;
            long elapsed = ElapsedSeconds;
            Collect(elapsed / 3600.0, WindowNightHours(elapsed), WindowEventHours(elapsed), OfflineSeconds, into, commitCarry: false);
        }

        /// <summary>받을 것이 있는지 (열림 + 되돌림 아님 + minClaimSeconds 이상 쌓임 또는 단추 줄이 있음)</summary>
        public bool CanClaim
        {
            get
            {
                if (!IsUnlocked || Tampered) return false;
                if (OfflineGoldAmount() >= 1) return true;
                long elapsed = ElapsedSeconds;
                return elapsed > 0 && elapsed >= Cfg.minClaimSeconds
                       && HasAnyLine(elapsed / 3600.0, WindowNightHours(elapsed), WindowEventHours(elapsed));
            }
        }

        /// <summary>
        /// 바구니를 받는다. doubled면 두 배 (무료 두 배 → 없으면 두 배 수선권, 둘 다 없으면 1배로 받고 Doubled=false).
        /// 받을 수 없으면 null. 받으면 lastClaimUnix = 지금, offlineSeconds = 0.
        /// </summary>
        public IdleChestClaimResult Claim(bool doubled = false)
        {
            if (!CanClaim) return null;
            var cfg = Cfg;
            var save = Save;
            var result = new IdleChestClaimResult
            {
                Duration = Elapsed,
                OfflineDuration = OfflineElapsed,
            };

            if (doubled)
            {
                if (IsDoubleFree)
                {
                    (_data.meta.returnScreen ??= new ReturnSave()).doubleClaimedDayKey = _clock?.DayKey;
                    result.Doubled = true;
                }
                else if (_rewards != null && _rewards.TrySpendDoubleTicket())
                {
                    result.Doubled = true;
                    result.UsedDoubleTicket = true;
                }
            }

            _scratch.Clear();
            long elapsed = ElapsedSeconds;
            Collect(elapsed / 3600.0, WindowNightHours(elapsed), WindowEventHours(elapsed), OfflineSeconds, _scratch, commitCarry: true);
            save.lastClaimUnix = Now;
            save.offlineSeconds = 0;
            save.bonusCapSeconds = 0; // 다시 만난 날 한 번짜리 상한은 이번 받기로 끝
            _lastCapSeconds = CapSeconds;

            double multiplier = result.Doubled ? 2 : 1;
            result.Rewards = _rewards != null ? _rewards.Grant(_scratch, multiplier, cfg.doubleExcludesGem) : MetaRewardService.Merge(_scratch);
            _scratch.Clear();

            _progress?.Increment(CounterKey.ChestClaim);
            _progress?.MarkDirty();
            Claimed?.Invoke(result);
            Changed?.Invoke();
            return result;
        }

        // ── 두 배 (복귀 화면) ──

        /// <summary>오늘 무료 두 배가 남음</summary>
        public bool IsDoubleFree
        {
            get
            {
                if (Cfg.freeDoublePerDay <= 0 || Tampered) return false;
                string used = _data.meta?.returnScreen?.doubleClaimedDayKey;
                return string.IsNullOrEmpty(used) || _clock == null || used != _clock.DayKey;
            }
        }

        /// <summary>두 배로 받을 수 있음 (무료 또는 두 배 수선권)</summary>
        public bool CanDouble => IsDoubleFree || (_rewards != null && _rewards.DoubleTickets > 0);

        // ── 빠른 수선 ──

        /// <summary>빠른 수선이 열렸는지 (FeatureId.QuickIdle)</summary>
        public bool IsQuickIdleUnlocked => _features == null || _features.IsUnlocked(FeatureId.QuickIdle);

        /// <summary>오늘 쓴 횟수 (날이 바뀌면 0)</summary>
        public int QuickIdleUsedToday
        {
            get
            {
                var save = Save;
                if (_clock == null) return Math.Max(0, save.quickUsedToday);
                return save.quickDayKey == _clock.DayKey ? Math.Max(0, save.quickUsedToday) : 0;
            }
        }

        /// <summary>하루 최대 횟수 (quickIdleGemCosts.Length)</summary>
        public int QuickIdleMaxPerDay => Cfg.quickIdleGemCosts?.Length ?? 0;

        public int QuickIdleLeftToday => Math.Max(0, QuickIdleMaxPerDay - QuickIdleUsedToday);

        /// <summary>다음 빠른 수선 별조각 비용 (0 = 무료, 남은 횟수가 없으면 -1)</summary>
        public double NextQuickIdleCost
        {
            get
            {
                int used = QuickIdleUsedToday;
                var costs = Cfg.quickIdleGemCosts;
                if (costs == null || used >= costs.Length) return -1;
                return Math.Max(0, costs[used]);
            }
        }

        /// <summary>오늘 무료 빠른 수선이 남음 (빨간 점이 아니라 위젯 "무료" 표로 알린다 — ShowsFreeTag)</summary>
        public bool HasFreeQuickIdle => IsQuickIdleUnlocked && !Tampered && NextQuickIdleCost == 0;

        /// <summary>빠른 수선권 보유 수</summary>
        public int QuickIdleTickets => _rewards != null ? _rewards.QuickIdleTickets : 0;

        /// <summary>빠른 수선을 할 수 있는지와 안 되는 이유 (토스트용: "오늘 빠른 수선을 모두 썼어요", "별조각이 부족해요", 되돌림 문구)</summary>
        public bool CanQuickIdle(bool useTicket, out string reason)
        {
            reason = string.Empty;
            if (!IsQuickIdleUnlocked)
            {
                reason = _features != null ? _features.GetRequirementText(FeatureId.QuickIdle) : "아직 열리지 않았어요";
                return false;
            }
            if (useTicket)
            {
                if (QuickIdleTickets > 0) return true;
                reason = "빠른 수선권이 없어요";
                return false;
            }
            double cost = NextQuickIdleCost;
            if (cost < 0)
            {
                reason = "오늘 빠른 수선을 모두 썼어요";
                return false;
            }
            if (cost == 0 && Tampered)
            {
                reason = DailyClock.TamperedMessage;
                return false;
            }
            if (cost > 0 && (_currency == null || !_currency.Has(CurrencyType.Gem, cost)))
            {
                reason = "별조각이 부족해요";
                return false;
            }
            return true;
        }

        /// <summary>빠른 수선 1번에 받을 것 미리보기 (into를 비우고 채움)</summary>
        public void GetQuickIdlePreview(List<MetaReward> into)
        {
            if (into == null) return;
            into.Clear();
            float hours = Math.Max(0f, Cfg.quickIdleHours);
            Collect(hours, 0, 0, hours * 3600.0, into, commitCarry: false);
        }

        /// <summary>
        /// 빠른 수선. useTicket이면 빠른 수선권 1장 (하루 횟수 안 씀), 아니면 하루 횟수 + 비용(무료/별조각).
        /// 실패하면 null. 문구: "친구들이 밤새 할 일을 지금 바로 끝냈어요!"
        /// </summary>
        public IdleChestClaimResult TryQuickIdle(bool useTicket)
        {
            if (!CanQuickIdle(useTicket, out _)) return null;
            var cfg = Cfg;
            var result = new IdleChestClaimResult { IsQuickIdle = true };

            if (useTicket)
            {
                if (_rewards == null || !_rewards.TrySpendQuickIdleTicket()) return null;
                result.UsedQuickTicket = true;
            }
            else
            {
                double cost = NextQuickIdleCost;
                if (cost > 0)
                {
                    if (_currency == null || !_currency.TrySpend(CurrencyType.Gem, cost)) return null;
                    result.GemSpent = cost;
                }
                var save = Save;
                int used = QuickIdleUsedToday;
                save.quickDayKey = _clock?.DayKey;
                save.quickUsedToday = used + 1;
            }

            float hours = Math.Max(0f, cfg.quickIdleHours);
            result.Duration = TimeSpan.FromHours(hours);
            result.OfflineDuration = TimeSpan.FromHours(hours);
            _scratch.Clear();
            Collect(hours, 0, 0, hours * 3600.0, _scratch, commitCarry: true);
            result.Rewards = _rewards != null ? _rewards.Grant(_scratch) : MetaRewardService.Merge(_scratch);
            _scratch.Clear();

            _progress?.Increment(CounterKey.QuickIdle);
            _progress?.MarkDirty();
            Claimed?.Invoke(result);
            Changed?.Invoke();
            return result;
        }

        /// <summary>
        /// 튜토리얼(FTUE 9단계)용: 바구니에 hours시간치가 이미 쌓인 것처럼 시작 시각을 당긴다 (한 번만, MetaFlags.IdleChestPrefilled).
        /// </summary>
        public void Prefill(float hours)
        {
            if (!(hours > 0) || !IsUnlocked) return;
            if (_progress != null ? _progress.HasFlag(MetaFlags.IdleChestPrefilled) : false) return;
            EnsureStarted();
            var save = Save;
            long cap = CapSeconds;
            long seconds = (long)Math.Min(cap, Math.Round(hours * 3600.0));
            long start = Now - seconds;
            if (save.lastClaimUnix <= 0 || start < save.lastClaimUnix) save.lastClaimUnix = start;
            save.offlineSeconds = Math.Min(cap, Math.Max(save.offlineSeconds, seconds));
            _progress?.SetFlag(MetaFlags.IdleChestPrefilled);
            Changed?.Invoke();
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>열렸는데 아직 시작 시각이 없으면 지금부터 쌓기 시작</summary>
        void EnsureStarted()
        {
            var save = Save;
            if (save.lastClaimUnix > 0 || !IsUnlocked) return;
            save.lastClaimUnix = Now;
        }

        /// <summary>
        /// 단계 상한 (시간): baseCapHours와, 0이 아닌 조건(stageCleared = 클리어한 스테이지 번호, shopLevel = 수선점 레벨)이 모두 맞는 단계의
        /// capHours 중 가장 큰 값 (SPEC4 §13.4). 조건이 모두 0인 단계는 늘 맞는다. 할당 없음
        /// </summary>
        double StepCapHours()
        {
            var cfg = Cfg;
            double hours = Math.Max(0, cfg.baseCapHours);
            var steps = cfg.capSteps;
            if (steps == null) return hours;
            int cleared = HighestStage - 1;
            int level = ShopLevel;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) continue;
                if (step.stageCleared > 0 && cleared < step.stageCleared) continue;
                if (step.shopLevel > 0 && level < step.shopLevel) continue;
                if (step.capHours > hours) hours = step.capHours;
            }
            return hours;
        }

        /// <summary>상한 (초) = max(단계 상한, 다시 만난 날 한 번짜리 상한)</summary>
        long CapSeconds
        {
            get
            {
                long step = (long)Math.Max(0, Math.Round(StepCapHours() * 3600.0));
                long bonus = Save.bonusCapSeconds;
                return bonus > step ? bonus : step;
            }
        }

        int ShopLevel => Math.Max(1, _data.level);

        /// <summary>
        /// 지금 쌓인 구간 [마지막 받기, 마지막 받기 + elapsed)의 밤의 수선점 덤 시간 = 겹친 시간(구간에서 자름) × (배율 − 1).
        /// 훅이 하나라도 없거나 배율이 1 이하면 0
        /// </summary>
        double WindowNightHours(long elapsed)
        {
            var hook = NightBonusHours;
            var rateHook = NightBonusMultiplier;
            if (hook == null || rateHook == null || elapsed <= 0) return 0;
            double rate = rateHook() - 1.0;
            if (!(rate > 0) || double.IsInfinity(rate)) return 0;
            long start = Save.lastClaimUnix;
            if (start <= 0) return 0;
            double overlap = hook(start, start + elapsed);
            if (!(overlap > 0) || double.IsInfinity(overlap)) return 0;
            return Math.Min(overlap, elapsed / 3600.0) * rate;
        }

        /// <summary>지금 쌓인 구간의 이벤트 전용 줄 시간 (훅이 없으면 0 = 쌓이지 않음)</summary>
        double WindowEventHours(long elapsed)
        {
            var hook = EventLineHours;
            if (hook == null || elapsed <= 0) return 0;
            long start = Save.lastClaimUnix;
            if (start <= 0) return 0;
            double hours = hook(start, start + elapsed);
            return hours > 0 && !double.IsInfinity(hours) ? Math.Min(hours, elapsed / 3600.0) : 0;
        }

        /// <summary>이벤트 전용 줄이 지금 쌓이는 중인지 (시간당 양 표에 보일지)</summary>
        bool EventLineActiveNow
        {
            get
            {
                var hook = EventLineHours;
                if (hook == null) return false;
                long now = Now;
                return hook(now - 60, now) > 0;
            }
        }

        /// <summary>줄 하나가 이번 구간에 쌓인 시간: 이벤트 전용 줄 = 이벤트 시간, 단추 재화 줄 = 그대로, 나머지 = 그대로 + 밤 덤</summary>
        static double LineHours(IdleChestLine line, double hours, double nightHours, double eventHours)
        {
            if (line.eventOnly) return eventHours;
            if (line.reward.type == MetaRewardType.Currency && line.reward.currency == CurrencyType.Gold) return hours;
            return hours + nightHours;
        }

        long ElapsedSeconds
        {
            get
            {
                if (!IsUnlocked) return 0;
                EnsureStarted();
                long start = Save.lastClaimUnix;
                if (start <= 0) return 0;
                return Math.Max(0, Math.Min(CapSeconds, Now - start));
            }
        }

        double OfflineSeconds => IsUnlocked ? Math.Max(0, Math.Min(CapSeconds, Save.offlineSeconds)) : 0;

        double StageMultiplier => Math.Pow(Math.Max(1.0, Cfg.stageGrowth), HighestStage - 1);

        double GoldPerHour => _growth != null ? Math.Max(0, _growth.GoldPerSecond) * 3600.0 * Math.Max(0f, Cfg.offlineGoldRate) : 0;

        /// <summary>
        /// 줄 하나의 지금 시간당 양 (조건이 안 되면 0). 스테이지 배율은 줄의 maxStageMultiplier(0 = 없음)에서 자른다 (C-01).
        /// 이벤트 전용 줄도 양은 같은 식이고, 쌓이는 시간만 이벤트와 겹친 시간이다 (LineHours)
        /// </summary>
        double PerHour(IdleChestLine line)
        {
            if (line?.reward == null || HighestStage < line.minStage) return 0;
            if (line.reward.type != MetaRewardType.Currency && line.reward.type != MetaRewardType.Item) return 0;
            if (line.reward.type == MetaRewardType.Item && string.IsNullOrEmpty(line.reward.itemId)) return 0;
            double multiplier = StageMultiplier;
            if (line.maxStageMultiplier > 0 && multiplier > line.maxStageMultiplier) multiplier = line.maxStageMultiplier;
            return Math.Max(0, line.reward.amount) * multiplier;
        }

        double OfflineGoldAmount() => Math.Floor(GoldPerHour * OfflineSeconds / 3600.0 + Carry(GoldCarryKey) + 1e-9);

        double Carry(string key) => Save.carry.TryGetValue(key, out double value) && value > 0 && value < 1 ? value : 0;

        /// <summary>받을 줄이 하나라도 1개 이상인지 (할당 없음)</summary>
        bool HasAnyLine(double hours, double nightHours, double eventHours)
        {
            var lines = Cfg.lines;
            if (lines == null) return false;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                double perHour = PerHour(line);
                if (perHour <= 0) continue;
                double lineHours = LineHours(line, hours, nightHours, eventHours);
                if (lineHours <= 0) continue;
                if (Math.Floor(perHour * lineHours + Carry(line.Key) + 1e-9) >= 1) return true;
            }
            return false;
        }

        /// <summary>
        /// 줄마다 쌓인 시간(LineHours: hours + 밤 덤 / 이벤트 시간)치 + carry를 정수로 내려 into에 넣는다. 단추 줄은 offlineSeconds 기준.
        /// commitCarry면 남은 소수점을 carry에 저장한다 (실제로 받을 때만). 빠른 수선은 밤 덤 · 이벤트 시간을 0으로 넘긴다.
        /// </summary>
        void Collect(double hours, double nightHours, double eventHours, double offlineSeconds, List<MetaReward> into, bool commitCarry)
        {
            var save = Save;
            var lines = Cfg.lines;
            if (lines != null && hours > 0)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    double perHour = PerHour(line);
                    if (perHour <= 0) continue;
                    double lineHours = LineHours(line, hours, nightHours, eventHours);
                    if (lineHours <= 0) continue;
                    string key = line.Key;
                    double total = perHour * lineHours + Carry(key);
                    double whole = Math.Floor(total + 1e-9);
                    if (commitCarry) save.carry[key] = Math.Max(0, total - whole);
                    if (whole < 1) continue;
                    into.Add(line.reward.type == MetaRewardType.Currency
                        ? MetaReward.Currency(line.reward.currency, whole)
                        : MetaReward.Item(line.reward.itemId, whole));
                }
            }

            double goldTotal = GoldPerHour * Math.Max(0, offlineSeconds) / 3600.0 + Carry(GoldCarryKey);
            double gold = Math.Floor(goldTotal + 1e-9);
            if (commitCarry && offlineSeconds > 0) save.carry[GoldCarryKey] = Math.Max(0, goldTotal - gold);
            if (gold >= 1) into.Add(MetaReward.Gold(gold));
        }

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature == FeatureId.IdleChest)
            {
                EnsureStarted();
                _lastUnlocked = true;
                Changed?.Invoke();
            }
            else if (feature == FeatureId.QuickIdle)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>상한이 바뀌었거나(2-1 클리어) 조용히 열렸으면(옛 세이브) Changed</summary>
        void OnProgressChanged()
        {
            bool unlocked = IsUnlocked;
            long cap = CapSeconds;
            bool changed = false;
            if (unlocked != _lastUnlocked)
            {
                _lastUnlocked = unlocked;
                if (unlocked) EnsureStarted();
                changed = true;
            }
            if (cap != _lastCapSeconds)
            {
                changed |= _lastCapSeconds >= 0;
                _lastCapSeconds = cap;
            }
            if (changed) Changed?.Invoke();
        }

        void OnDayChanged(string dayKey) => Changed?.Invoke();

        /// <summary>되돌림이 생기거나 풀리면 받기·무료 빠른 수선 가능 여부가 바뀐다</summary>
        void OnTamperChanged(bool tampered) => Changed?.Invoke();
    }
}
