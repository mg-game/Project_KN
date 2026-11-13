using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 메타 기능 저장 데이터 (PlayerData.meta). 일일 시계, 진행 기록, 가이드 퀘스트, 수선 일지, 출석부,
    /// 수선 바구니, 기능 개방, 튜토리얼, 복귀 화면, 메타 보관함(빠른 수선권 등).
    /// "오늘/이번 주" 상태는 자기가 속한 날 키(periodKey/dayKey)를 함께 저장하고, 키가 지금과 다르면 비운다.
    /// 날 키는 DailyClock이 만든다: 하루 "yyyy-MM-dd" (현지 시각 − 5시간의 날짜), 주 = 그 주 월요일의 날 키.
    /// </summary>
    [Serializable]
    public class MetaSave
    {
        public MetaClockSave clock = new();
        public CounterSave counters = new();
        public GuideQuestSave guide = new();
        public MissionSave missions = new();
        public AttendanceSave attendance = new();
        public IdleChestSave idleChest = new();
        public FeatureSave features = new();
        public FtueSave ftue = new();
        public ReturnSave returnScreen = new();
        /// <summary>다시 만난 날 (CR-06 예약)</summary>
        public ComebackSave comeback = new();
        /// <summary>빠른 수선권 / 두 배 수선권 / 선택 기억편 / 친구 선택권</summary>
        public MetaWalletSave wallet = new();

        // ── 4차 (SPEC4 §2.1, 버전 4) — 옛 세이브는 빈 기본값으로 읽힌다 ──
        /// <summary>수선점 기록 (Q-04)</summary>
        public AchievementSave achievements = new();
        /// <summary>도감 (Q-05)</summary>
        public CodexSave codex = new();
        /// <summary>소환 마일스톤 (PRG-04)</summary>
        public GachaMilestoneSave gachaMilestones = new();
        /// <summary>이벤트 달력 (EVT-01)</summary>
        public EventSave events = new();
        /// <summary>별빛 산책 수첩 (Q-06)</summary>
        public SeasonSave season = new();
        /// <summary>동화책 (CR-05)</summary>
        public StorySave story = new();
        /// <summary>호감도 보상 · 쓰다듬기 (CR-10)</summary>
        public AffinitySave affinity = new();
        /// <summary>쿠폰 (EVT-02)</summary>
        public CouponSave coupons = new();
        /// <summary>챕터 상자 (PRG-06)</summary>
        public ChapterChestSave chapterChests = new();
        /// <summary>첫 주 (NEW-01)</summary>
        public StarterWeekSave starterWeek = new();
        /// <summary>오늘의 작은 선물 (NEW-02)</summary>
        public DailyGiftSave dailyGift = new();
        /// <summary>길 잃은 반딧불 (NEW-03)</summary>
        public FireflyDaySave firefly = new();
        /// <summary>친구들의 응원 (PRG-03)</summary>
        public CheerSave cheer = new();
        /// <summary>백업 알림 (SAVE-01 · 델타 17, RM-2 SB-1): 마지막 내보내기 · 알림 날 키. 더하기만 한 칸이라 버전은 그대로 4 (옛 세이브는 빈 기본값)</summary>
        public BackupSave backup = new();

        /// <summary>4차 마이그레이션이 튜토리얼을 마친 세이브에 넣는 PlayMinutes = min(PlayMinutesSeedCap, 이 값 × 설치 일수) (CR4-24)</summary>
        public const int PlayMinutesSeedPerDay = 30;
        /// <summary>4차 마이그레이션 PlayMinutes 상한 (5차 개방 바닥 270분과 같다)</summary>
        public const int PlayMinutesSeedCap = 270;

        /// <summary>
        /// 4차 마이그레이션의 PlayMinutes 채우기 둘째 단계가 남았는지 (CR4-24). 마이그레이션(PlayerData.Sanitize)에는 오늘 날짜가 없어
        /// 설치 ~ 마지막 세션 날로 먼저 채우고 이것을 켠다. 다음 MetaSession.Start가 시계를 새로 읽은 뒤
        /// 오늘까지 센 설치 일수(DailyClock.InstallDay — 조건 InstallDays와 같은 뜻)로 한 번 더 올리고 끈다 (ApplyPendingPlayMinutesSeed).
        /// 옛 세이브에는 없어 false로 읽힌다
        /// </summary>
        public bool playMinutesSeedPending;

        public void Sanitize()
        {
            clock ??= new MetaClockSave();
            clock.Sanitize();
            counters ??= new CounterSave();
            counters.Sanitize();
            guide ??= new GuideQuestSave();
            guide.Sanitize();
            missions ??= new MissionSave();
            missions.Sanitize();
            attendance ??= new AttendanceSave();
            attendance.Sanitize();
            idleChest ??= new IdleChestSave();
            idleChest.Sanitize();
            features ??= new FeatureSave();
            features.Sanitize();
            ftue ??= new FtueSave();
            ftue.Sanitize();
            returnScreen ??= new ReturnSave();
            returnScreen.Sanitize();
            comeback ??= new ComebackSave();
            comeback.Sanitize();
            wallet ??= new MetaWalletSave();
            wallet.Sanitize();

            achievements ??= new AchievementSave();
            achievements.Sanitize();
            codex ??= new CodexSave();
            codex.Sanitize();
            gachaMilestones ??= new GachaMilestoneSave();
            gachaMilestones.Sanitize();
            events ??= new EventSave();
            events.Sanitize(clock.lastDayKey);
            season ??= new SeasonSave();
            season.Sanitize();
            story ??= new StorySave();
            story.Sanitize();
            affinity ??= new AffinitySave();
            affinity.Sanitize();
            coupons ??= new CouponSave();
            coupons.Sanitize();
            chapterChests ??= new ChapterChestSave();
            chapterChests.Sanitize();
            starterWeek ??= new StarterWeekSave();
            starterWeek.Sanitize();
            dailyGift ??= new DailyGiftSave();
            firefly ??= new FireflyDaySave();
            firefly.Sanitize();
            cheer ??= new CheerSave();
            cheer.Sanitize();
            backup ??= new BackupSave();
        }

        /// <summary>
        /// 4차 세이브 이전 (버전 4 미만, Sanitize 뒤에 PlayerData가 부른다). 새 칸은 모두 빈 기본값이고 여기서는 두 가지만 한다:
        ///   · 도감 채우기 예약 (codex.backfillPending) — 도감 모듈이 다음 Connect에서 이미 가진 형태 · 의상 · 재료 · 만난 적을 "찾음"으로 표시한다 (보상은 자동으로 받지 않음).
        ///   · 튜토리얼을 마친 세이브면 앱을 켜 둔 시간(CounterKey.PlayMinutes 평생 값)을 min(270, 30 × 설치 일수)로 채운다 (CR4-24) —
        ///     오래 한 세이브가 기능 개방 바닥(20~270분) 때문에 이미 지난 카드를 처음부터 4시간 반 기다리지 않게. 이미 더 크면 그대로 둔다.
        /// 설치 일수는 두 단계로 센다 (값을 올리기만 한다):
        ///   ① 여기서 설치 날 키 ~ 마지막 날 키 (설치한 날 = 1일째 — 여기에는 오늘 날짜가 없다). 기록이 없으면(메타 이전의 옛 세이브) 오래 한 세이브로 보고 상한을 쓴다.
        ///   ② playMinutesSeedPending을 켜 두고, 다음 MetaSession.Start가 오늘까지 센 설치 일수(DailyClock.InstallDay)로 한 번 더 올린다 —
        ///      설치 둘째 날까지만 하고 두 달 뒤 돌아온 세이브도 InstallDays 조건과 같은 일수(→ 상한 270)를 받는다.
        /// </summary>
        public void MigrateToV4()
        {
            codex.backfillPending = true;
            if (ftue == null || !ftue.done) return;

            RaisePlayMinutesSeed(InstallDaysForMigration());
            playMinutesSeedPending = true;
        }

        /// <summary>
        /// 마이그레이션 채우기 둘째 단계 (MetaSession.Start가 DailyClock.Refresh 뒤에 부른다). 남아 있으면
        /// PlayMinutes를 min(270, 30 × installDay)까지 올리고(줄이지 않음) 끈다. 남은 것이 없으면 아무것도 하지 않는다. 올렸으면 true
        /// </summary>
        public bool ApplyPendingPlayMinutesSeed(int installDay)
        {
            if (!playMinutesSeedPending) return false;
            playMinutesSeedPending = false;
            return RaisePlayMinutesSeed(installDay);
        }

        /// <summary>PlayMinutes 평생 값을 min(PlayMinutesSeedCap, PlayMinutesSeedPerDay × installDays)까지 올린다 (이미 더 크면 그대로). 올렸으면 true</summary>
        bool RaisePlayMinutesSeed(int installDays)
        {
            long seed = Math.Min(PlayMinutesSeedCap, (long)PlayMinutesSeedPerDay * Math.Max(1, installDays));
            counters ??= new CounterSave();
            var lifetime = counters.lifetime ??= new Dictionary<CounterKey, long>();
            lifetime.TryGetValue(CounterKey.PlayMinutes, out long current);
            if (seed <= current) return false;
            lifetime[CounterKey.PlayMinutes] = seed;
            return true;
        }

        /// <summary>마이그레이션용 설치 일수 (1 이상). 날 키가 없으면 설치 시각 ~ 마지막으로 본 시각, 그것도 없으면 상한에 닿는 일수</summary>
        int InstallDaysForMigration()
        {
            int fallback = (PlayMinutesSeedCap + PlayMinutesSeedPerDay - 1) / PlayMinutesSeedPerDay;
            if (!string.IsNullOrEmpty(clock.installDayKey) && !string.IsNullOrEmpty(clock.lastDayKey))
                return Math.Max(1, DailyClock.DaysBetween(clock.installDayKey, clock.lastDayKey) + 1);
            if (clock.installUnix > 0 && clock.maxSeenUnix >= clock.installUnix)
                return (int)Math.Max(1, Math.Min(fallback, (clock.maxSeenUnix - clock.installUnix) / 86400 + 1));
            return fallback;
        }

        /// <summary>
        /// 메타 기능이 없던 세이브(버전 3 미만, 이미 진행한 플레이어)를 옮긴다:
        /// 튜토리얼은 끝난 것으로, 기능 개방은 다음 평가 때 카드 없이 조용히 맞춘다.
        /// </summary>
        public void MigrateFromLegacy(bool hasProgress)
        {
            features.silentSyncPending = true;
            if (hasProgress)
            {
                ftue.done = true;
                ftue.skipped = true;
            }
        }
    }

    /// <summary>일일 시계 (F-01). 시간 되돌림 방지와 세션 경계</summary>
    [Serializable]
    public class MetaClockSave
    {
        /// <summary>마지막으로 확인한 날 키 (바뀌면 DailyClock.DayChanged)</summary>
        public string lastDayKey;
        /// <summary>마지막으로 확인한 주 키 (바뀌면 DailyClock.WeekChanged)</summary>
        public string lastWeekKey;
        /// <summary>본 적 있는 가장 늦은 기기 시각 (유닉스 초). 지금 시각이 이보다 300초 넘게 이르면 되돌림으로 본다</summary>
        public long maxSeenUnix;
        /// <summary>마지막 세션이 끝난 시각 (백그라운드 진입/종료). 복귀 시간 = 지금 − max(이 값, lastSaveUnix)</summary>
        public long lastSessionEndUnix;
        /// <summary>처음 실행한 날 키 (설치 n일째 계산)</summary>
        public string installDayKey;
        /// <summary>처음 실행한 시각</summary>
        public long installUnix;

        public void Sanitize()
        {
            if (maxSeenUnix < 0) maxSeenUnix = 0;
            if (lastSessionEndUnix < 0) lastSessionEndUnix = 0;
            if (installUnix < 0) installUnix = 0;
        }
    }

    /// <summary>진행 기록기 카운터 (F-02). 평생 / 오늘 / 이번 주</summary>
    [Serializable]
    public class CounterSave
    {
        public Dictionary<CounterKey, long> lifetime = new();
        /// <summary>dayKey 날의 값. 날이 바뀌면 비운다</summary>
        public Dictionary<CounterKey, long> daily = new();
        /// <summary>weekKey 주의 값. 주가 바뀌면 비운다</summary>
        public Dictionary<CounterKey, long> weekly = new();
        public string dayKey;
        public string weekKey;

        public void Sanitize()
        {
            lifetime ??= new Dictionary<CounterKey, long>();
            daily ??= new Dictionary<CounterKey, long>();
            weekly ??= new Dictionary<CounterKey, long>();
            ClampNegative(lifetime);
            ClampNegative(daily);
            ClampNegative(weekly);
        }

        static readonly List<CounterKey> NegativeKeys = new();

        static void ClampNegative(Dictionary<CounterKey, long> map)
        {
            NegativeKeys.Clear();
            foreach (var pair in map)
            {
                if (pair.Value < 0) NegativeKeys.Add(pair.Key);
            }
            foreach (var key in NegativeKeys) map[key] = 0;
        }
    }

    /// <summary>가이드 퀘스트 (Q-01)</summary>
    [Serializable]
    public class GuideQuestSave
    {
        /// <summary>지금 퀘스트 위치 (0부터, = 받은 퀘스트 수). 콘텐츠 목록을 넘으면 반복 단계</summary>
        public int index;
        /// <summary>
        /// 반복 단계의 기준값 (예: "수선점 Lv +10"이 시작될 때의 수선점 레벨).
        /// -1 = 아직 정하지 않음 (GuideQuestSystem이 그 단계에 처음 들어갈 때 채운다)
        /// </summary>
        public long repeatBaseline = -1;
        public long lastClaimUnix;

        public void Sanitize()
        {
            if (index < 0) index = 0;
            if (repeatBaseline < -1) repeatBaseline = -1;
        }
    }

    /// <summary>수선 일지 (Q-02)</summary>
    [Serializable]
    public class MissionSave
    {
        public MissionPeriodSave daily = new();
        public MissionPeriodSave weekly = new();

        public MissionPeriodSave Get(MissionPeriod period) => period == MissionPeriod.Weekly ? weekly : daily;

        public void Sanitize()
        {
            daily ??= new MissionPeriodSave();
            daily.Sanitize();
            weekly ??= new MissionPeriodSave();
            weekly.Sanitize();
        }
    }

    /// <summary>하루 또는 한 주의 미션 기록. 진행도는 카운터(오늘/이번 주)에서 읽으므로 받은 것만 저장한다</summary>
    [Serializable]
    public class MissionPeriodSave
    {
        /// <summary>이 기록이 속한 날 키(일일) 또는 주 키(주간). 지금과 다르면 비운다</summary>
        public string periodKey;
        /// <summary>보상을 받은 미션 ID</summary>
        public HashSet<string> claimed = new();
        /// <summary>미션을 받아 모은 점수</summary>
        public int points;
        /// <summary>받은 점수 상자 (MissionDatabase 순서 인덱스, 같은 주기 안에서 0부터)</summary>
        public HashSet<int> chestsClaimed = new();

        public void Reset(string key)
        {
            periodKey = key;
            claimed.Clear();
            points = 0;
            chestsClaimed.Clear();
        }

        public void Sanitize()
        {
            claimed ??= new HashSet<string>();
            chestsClaimed ??= new HashSet<int>();
            if (points < 0) points = 0;
        }
    }

    /// <summary>출석부 (Q-03). 도장을 찍으면 그날 칸 보상을 바로 받는다 (누적 일수, 빠진 날이 있어도 초기화되지 않음)</summary>
    [Serializable]
    public class AttendanceSave
    {
        /// <summary>새싹 출석 도장 수 (0~7)</summary>
        public int starterDays;
        /// <summary>정규 출석 이번 회차 도장 수 (0~28). 28을 채운 다음 도장은 새 회차 1일째</summary>
        public int monthlyDays;
        /// <summary>정규 출석 회차 (0부터)</summary>
        public int monthlyCycle;
        /// <summary>마지막으로 도장을 찍은 날 키</summary>
        public string lastCheckInDayKey;
        /// <summary>평생 도장 수</summary>
        public int totalDays;

        public void Sanitize()
        {
            if (starterDays < 0) starterDays = 0;
            if (monthlyDays < 0) monthlyDays = 0;
            if (monthlyCycle < 0) monthlyCycle = 0;
            if (totalDays < 0) totalDays = 0;
        }
    }

    /// <summary>수선 바구니 (IDLE-01/02)</summary>
    [Serializable]
    public class IdleChestSave
    {
        /// <summary>마지막으로 바구니를 받은 시각 (재료·별조각 줄은 이때부터 상한까지 쌓인다). 0 = 아직 시작 안 함</summary>
        public long lastClaimUnix;
        /// <summary>앱을 닫아 둔 시간 합계 (초). 단추 줄은 이 시간만큼만 쌓인다 (켜 둔 동안은 초당 단추를 이미 받으므로)</summary>
        public double offlineSeconds;
        /// <summary>줄 키(IdleChestLine.Key) → 받고 남은 소수점. 다음 받기에 더해 결정적으로 처리한다</summary>
        public Dictionary<string, double> carry = new();
        /// <summary>오늘 쓴 빠른 수선 횟수 (quickDayKey 날 기준)</summary>
        public int quickUsedToday;
        public string quickDayKey;
        /// <summary>다시 만난 날(CR-06)의 한 번짜리 바구니 상한 (초, 0 = 없음). 다음 받기 때까지 상한 = max(단계 상한, 이 값)</summary>
        public long bonusCapSeconds;

        public void Sanitize()
        {
            carry ??= new Dictionary<string, double>();
            if (lastClaimUnix < 0) lastClaimUnix = 0;
            if (!(offlineSeconds >= 0)) offlineSeconds = 0;
            if (quickUsedToday < 0) quickUsedToday = 0;
            if (bonusCapSeconds < 0) bonusCapSeconds = 0;
        }
    }

    /// <summary>기능 개방 (F-03)</summary>
    [Serializable]
    public class FeatureSave
    {
        /// <summary>열린 기능</summary>
        public HashSet<FeatureId> unlocked = new();
        /// <summary>열린 뒤 한 번이라도 들어가 본 기능 (빨간 점 "새로 열림" 끄기)</summary>
        public HashSet<FeatureId> seen = new();
        /// <summary>개방 카드를 이미 큐에 넣은 기능 (카드 중복 방지)</summary>
        public HashSet<FeatureId> cardShown = new();
        /// <summary>true면 다음 평가 때 이미 조건을 넘은 기능을 카드 없이 열고 "봤음"으로 처리한다 (옛 세이브 이전)</summary>
        public bool silentSyncPending;

        public void Sanitize()
        {
            unlocked ??= new HashSet<FeatureId>();
            seen ??= new HashSet<FeatureId>();
            cardShown ??= new HashSet<FeatureId>();
        }
    }

    /// <summary>첫 10분 튜토리얼 (FTUE-01). 끄고 다시 켜도 같은 단계부터 이어 한다</summary>
    [Serializable]
    public class FtueSave
    {
        /// <summary>지금 단계 인덱스 (MetaDatabase.ftueSteps 순서, = 끝낸 단계 수)</summary>
        public int step;
        /// <summary>졸업 (또는 건너뜀)</summary>
        public bool done;
        /// <summary>설정에서 건너뜀 (옛 세이브 이전도 건너뜀으로 본다)</summary>
        public bool skipped;
        /// <summary>튜토리얼 안에서 한 번만 보여 줄 힌트 등 (예: "hint.drag_summon")</summary>
        public HashSet<string> flags = new();
        public long startedUnix;

        public void Sanitize()
        {
            flags ??= new HashSet<string>();
            if (step < 0) step = 0;
        }
    }

    /// <summary>복귀 화면 (IDLE-03)</summary>
    [Serializable]
    public class ReturnSave
    {
        /// <summary>무료 두 배 받기를 쓴 날 키 (하루 1번)</summary>
        public string doubleClaimedDayKey;
        /// <summary>마지막 복귀 때 비운 시간 (초, 복귀 화면 문구용)</summary>
        public long lastAwaySeconds;
        /// <summary>마지막으로 복귀 화면을 띄운 시각</summary>
        public long lastShownUnix;

        public void Sanitize()
        {
            if (lastAwaySeconds < 0) lastAwaySeconds = 0;
        }
    }

    /// <summary>다시 만난 날 (CR-06: 72시간 이상 비우면 3일 미니 도장, 평생 6번, 14일 쉼)</summary>
    [Serializable]
    public class ComebackSave
    {
        public int count;
        public string lastDayKey;
        /// <summary>진행 중인 미니 도장 일차 (0 = 없음)</summary>
        public int day;
        /// <summary>이번 미니 도장이 시작된 날 키</summary>
        public string startDayKey;
        /// <summary>마지막으로 도장을 찍은 날 키 (하루 한 칸)</summary>
        public string lastStampDayKey;
        /// <summary>이번 다시 만난 날을 연 비운 시간 (초, 카드 문구 'n일 만이에요' — 0 = 모름)</summary>
        public long awaySeconds;

        public void Sanitize()
        {
            if (count < 0) count = 0;
            if (day < 0) day = 0;
            if (awaySeconds < 0) awaySeconds = 0;
        }
    }

    /// <summary>메타 전용 보관함 (재화/재료가 아닌 보상)</summary>
    [Serializable]
    public class MetaWalletSave
    {
        /// <summary>빠른 수선권 (하루 제한과 별개로 쓸 수 있음, 보관 상한 IdleChestConfig.maxQuickIdleTickets)</summary>
        public int quickIdleTickets;
        /// <summary>두 배 수선권 (무료 두 배를 쓴 날 복귀 화면에서 1장 사용)</summary>
        public int doubleTickets;
        /// <summary>선택 기억편 (원하는 계보에 넣을 수 있는 기억편)</summary>
        public int selectShards;
        /// <summary>친구 선택권 (PRG-04, 전설 이하 계보 하나를 골라 받는다 — 고르기 전에는 줄지 않는다)</summary>
        public int friendSelectTickets;

        public void Sanitize()
        {
            if (quickIdleTickets < 0) quickIdleTickets = 0;
            if (doubleTickets < 0) doubleTickets = 0;
            if (selectShards < 0) selectShards = 0;
            if (friendSelectTickets < 0) friendSelectTickets = 0;
        }
    }

    // ═════════════════════════ 4차 (SPEC4 §2.1, 버전 4) ═════════════════════════

    /// <summary>수선점 기록 (Q-04). 진행도는 진행 기록기에서 읽으므로 받은 단계만 저장한다</summary>
    [Serializable]
    public class AchievementSave
    {
        /// <summary>받은 단계 ID ({lineId}_{tier}, 예: ach_stage_3)</summary>
        public HashSet<string> claimed = new();

        public void Sanitize()
        {
            claimed ??= new HashSet<string>();
        }
    }

    /// <summary>도감 (Q-05). 키는 CodexSystem.Key(종류, ID) — form:… / costume:… / enemy:… / synergy:… / synergymax:… / material:… / story:…</summary>
    [Serializable]
    public class CodexSave
    {
        /// <summary>찾은 항목 (설치 때부터 기록, 보상은 도감이 열린 뒤에)</summary>
        public HashSet<string> found = new();
        /// <summary>보상을 받은 항목</summary>
        public HashSet<string> claimed = new();
        /// <summary>한 번이라도 본 스킬 (스킬 이름 첫 표시, FX-06)</summary>
        public HashSet<string> seenSkills = new();
        /// <summary>true면 다음 Connect에서 이미 가진 것을 "찾음"으로 채운다 (버전 4 이전 세이브, 받기는 자동 아님). 채운 뒤 끈다</summary>
        public bool backfillPending;

        public void Sanitize()
        {
            found ??= new HashSet<string>();
            claimed ??= new HashSet<string>();
            seenSkills ??= new HashSet<string>();
        }
    }

    /// <summary>소환 마일스톤 (PRG-04)</summary>
    [Serializable]
    public class GachaMilestoneSave
    {
        /// <summary>누적 소환 수 (확정 소환 — 첫 만남 — 은 세지 않는다)</summary>
        public int pulls;
        /// <summary>받은 단계 수 (순서대로 하나씩)</summary>
        public int claimedSteps;

        public void Sanitize()
        {
            if (pulls < 0) pulls = 0;
            if (claimedSteps < 0) claimedSteps = 0;
        }
    }

    /// <summary>이벤트 달력 (EVT-01). 이벤트 회차마다 하나 ({eventId}@{startDayKey})</summary>
    [Serializable]
    public class EventSave
    {
        /// <summary>시작한 지 이 일수가 넘은 회차는 지운다</summary>
        public const int KeepDays = 60;

        public Dictionary<string, EventInstanceSave> instances = new();

        static readonly List<string> Expired = new();

        /// <summary>null 회차를 지우고, today(마지막 날 키)가 있으면 KeepDays일보다 오래된 회차를 지운다</summary>
        public void Sanitize(string today)
        {
            instances ??= new Dictionary<string, EventInstanceSave>();
            Expired.Clear();
            foreach (var pair in instances)
            {
                var instance = pair.Value;
                if (instance == null)
                {
                    Expired.Add(pair.Key);
                    continue;
                }
                instance.Sanitize();
                if (!string.IsNullOrEmpty(today) && !string.IsNullOrEmpty(instance.startDayKey)
                    && DailyClock.DaysBetween(instance.startDayKey, today) > KeepDays)
                    Expired.Add(pair.Key);
            }
            foreach (var key in Expired) instances.Remove(key);
            Expired.Clear();
        }
    }

    /// <summary>이벤트 한 회차 (EVT-01). 미션 진행도 = 평생 카운터 − 회차 시작 때 기준값</summary>
    [Serializable]
    public class EventInstanceSave
    {
        public string eventId;
        public string startDayKey;
        /// <summary>회차가 처음 켜질 때의 평생 카운터 값 (미션 카운터마다)</summary>
        public Dictionary<CounterKey, long> baseline = new();
        /// <summary>받은 미션 ID</summary>
        public HashSet<string> claimed = new();
        /// <summary>찍은 도장 수 (최대 7)</summary>
        public int stamps;
        public string lastStampDayKey;
        /// <summary>이벤트 모아 보기를 열어 봄 (top.event.new 끄기)</summary>
        public bool seen;
        /// <summary>시작 카드를 큐에 넣었음 (한 번만)</summary>
        public bool startCardShown;

        public void Sanitize()
        {
            baseline ??= new Dictionary<CounterKey, long>();
            claimed ??= new HashSet<string>();
            if (stamps < 0) stamps = 0;
        }
    }

    /// <summary>별빛 산책 수첩 (Q-06, 28일 시즌 · 30칸)</summary>
    [Serializable]
    public class SeasonSave
    {
        /// <summary>이번 시즌이 시작된 날 키 (null = 아직 시작 안 함)</summary>
        public string startDayKey;
        /// <summary>시즌 번호 (0부터)</summary>
        public int index;
        /// <summary>이번 시즌 경험치 (일일 미션 점수 1점 = 1)</summary>
        public long xp;
        /// <summary>받은 칸 (1부터)</summary>
        public HashSet<int> claimedTiers = new();

        public void Sanitize()
        {
            claimedTiers ??= new HashSet<int>();
            if (index < 0) index = 0;
            if (xp < 0) xp = 0;
        }
    }

    /// <summary>동화책 (CR-05)</summary>
    [Serializable]
    public class StorySave
    {
        /// <summary>펼쳐 본 장</summary>
        public HashSet<int> seenChapters = new();
        /// <summary>처음 읽은 선물을 받은 장</summary>
        public HashSet<int> rewardedChapters = new();

        public void Sanitize()
        {
            seenChapters ??= new HashSet<int>();
            rewardedChapters ??= new HashSet<int>();
        }
    }

    /// <summary>호감도 보상 · 쓰다듬기 (CR-10). 호감도 값 자체는 LineageSave.affinity</summary>
    [Serializable]
    public class AffinitySave
    {
        /// <summary>계보 ID → 보상을 받은 호감도 단계 (1 = 아직 없음)</summary>
        public Dictionary<string, int> claimedLevel = new();
        /// <summary>계보 ID → 오늘 쓰다듬은 횟수</summary>
        public Dictionary<string, PetDaySave> pets = new();

        static readonly List<string> Broken = new();

        public void Sanitize()
        {
            claimedLevel ??= new Dictionary<string, int>();
            pets ??= new Dictionary<string, PetDaySave>();
            Broken.Clear();
            foreach (var pair in pets)
            {
                if (pair.Value == null) Broken.Add(pair.Key);
                else pair.Value.Sanitize();
            }
            foreach (var key in Broken) pets.Remove(key);
            Broken.Clear();
        }
    }

    /// <summary>하루 쓰다듬기 횟수 (dayKey 날 기준)</summary>
    [Serializable]
    public class PetDaySave
    {
        public string dayKey;
        public int count;

        public void Sanitize()
        {
            if (count < 0) count = 0;
        }
    }

    /// <summary>쿠폰 (EVT-02). 쿠폰 번호는 저장하지 않고 해시만</summary>
    [Serializable]
    public class CouponSave
    {
        /// <summary>쓴 쿠폰 해시 (소문자 16진수)</summary>
        public HashSet<string> redeemed = new();
        /// <summary>연속으로 틀린 횟수 (5번이면 잠시 잠금)</summary>
        public int failStreak;
        /// <summary>이 시각(유닉스 초)까지 입력을 막는다</summary>
        public long lockUntilUnix;

        public void Sanitize()
        {
            redeemed ??= new HashSet<string>();
            if (failStreak < 0) failStreak = 0;
            if (lockUntilUnix < 0) lockUntilUnix = 0;
        }
    }

    /// <summary>챕터 상자 (PRG-06)</summary>
    [Serializable]
    public class ChapterChestSave
    {
        /// <summary>첫 클리어 상자를 받은 챕터 번호</summary>
        public HashSet<int> opened = new();

        public void Sanitize()
        {
            opened ??= new HashSet<int>();
        }
    }

    /// <summary>첫 주 (NEW-01, 7일 과제 · 14일 창)</summary>
    [Serializable]
    public class StarterWeekSave
    {
        /// <summary>시작한 날 키 (null = 아직 시작 안 함)</summary>
        public string startDayKey;
        /// <summary>한 번 조건을 넘어 완료로 굳은 과제 ID</summary>
        public HashSet<string> done = new();
        /// <summary>보상을 받은 과제 ID</summary>
        public HashSet<string> claimed = new();
        /// <summary>받은 상자 (순서 인덱스)</summary>
        public HashSet<int> chests = new();
        /// <summary>창이 끝남 (남은 보상은 자동으로 받음)</summary>
        public bool finished;
        /// <summary>이 세이브에서는 시작하지 않음 (개방 때 설치 7일째가 지남)</summary>
        public bool declined;

        public void Sanitize()
        {
            done ??= new HashSet<string>();
            claimed ??= new HashSet<string>();
            chests ??= new HashSet<int>();
        }
    }

    /// <summary>오늘의 작은 선물 (NEW-02)</summary>
    [Serializable]
    public class DailyGiftSave
    {
        /// <summary>마지막으로 받은 날 키</summary>
        public string lastDayKey;
    }

    /// <summary>길 잃은 반딧불 (NEW-03, 하루 잡은 수)</summary>
    [Serializable]
    public class FireflyDaySave
    {
        /// <summary>count가 속한 날 키</summary>
        public string dayKey;
        public int count;

        public void Sanitize()
        {
            if (count < 0) count = 0;
            if (count > 99) count = 99;
        }
    }

    /// <summary>친구들의 응원 (PRG-03, 장마다 한 번 · 10분)</summary>
    [Serializable]
    public class CheerSave
    {
        /// <summary>마지막으로 응원을 받은 장 (0 = 아직 없음)</summary>
        public int lastChapter;
        /// <summary>응원이 끝나는 시각 (유닉스 초, 앱을 닫아도 흐른다)</summary>
        public long endUnix;

        public void Sanitize()
        {
            if (lastChapter < 0) lastChapter = 0;
            if (endUnix < 0) endUnix = 0;
        }
    }

    /// <summary>백업 알림 (SAVE-01 · 델타 17, 규칙은 BackupReminderSystem). 날 키는 DailyClock 형식 "yyyy-MM-dd", null = 아직 없음</summary>
    [Serializable]
    public class BackupSave
    {
        /// <summary>마지막으로 세이브 코드를 내보낸(복사 · 공유) 날 키</summary>
        public string lastExportDayKey;
        /// <summary>마지막으로 백업 알림 우편을 보낸 날 키</summary>
        public string lastReminderDayKey;
    }
}
