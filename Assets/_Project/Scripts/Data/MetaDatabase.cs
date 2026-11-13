using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    // ═════════════════════════ 공용: 조건 / 보상 ═════════════════════════

    /// <summary>
    /// 메타 조건 하나 (가이드 퀘스트 / 기능 개방 / 튜토리얼 완료). 판정은 ProgressTracker.IsMet / GetValue.
    /// 필드 의미는 MetaConditionType 주석을 따른다.
    /// </summary>
    [Serializable]
    public class MetaCondition
    {
        public MetaConditionType type;
        [Tooltip("Counter / DailyCounter / WeeklyCounter일 때 셀 카운터")]
        public CounterKey counter;
        [Tooltip("계보 ID / 시너지 ID / 플래그 / FeatureId 이름 / \"weekly\" 등 (종류마다 다름)")]
        public string targetId;
        [Tooltip("목표 값 (스테이지는 (챕터-1)×10 + 번호)")]
        public long target;

        public static MetaCondition Always() => new() { type = MetaConditionType.None };

        public static MetaCondition Of(MetaConditionType type, long target, string targetId = null) =>
            new() { type = type, target = target, targetId = targetId };

        public static MetaCondition Count(CounterKey counter, long target, CounterScope scope = CounterScope.Lifetime) => new()
        {
            type = scope switch
            {
                CounterScope.Daily => MetaConditionType.DailyCounter,
                CounterScope.Weekly => MetaConditionType.WeeklyCounter,
                _ => MetaConditionType.Counter,
            },
            counter = counter,
            target = target,
        };

        /// <summary>메인 스테이지 "챕터-번호" 클리어. 예: StageCleared(1, 4) = 1-4 클리어</summary>
        public static MetaCondition StageCleared(int chapter, int number) =>
            Of(MetaConditionType.StageCleared, StageIndex(chapter, number));

        public static MetaCondition Flag(string flag) => Of(MetaConditionType.Flag, 1, flag);

        /// <summary>(챕터, 번호) → 스테이지 번호 (1-1 = 1, 2-1 = 11). BattleSystem.FormatStage의 역</summary>
        public static int StageIndex(int chapter, int number) => (Math.Max(1, chapter) - 1) * 10 + Math.Max(1, number);

        public MetaCondition Clone() => new() { type = type, counter = counter, targetId = targetId, target = target };
    }

    /// <summary>
    /// 메타 보상 하나. 재화/재료 외에 "단추 N분치", 빠른 수선권, 두 배 수선권, 선택 기억편, 친구 확정, 의상을 쓴다.
    /// 지급과 설명은 MetaRewardService.Grant / Describe. 같은 클래스를 지급 결과(풀린 값)에도 쓴다 — 결과에는 GoldMinutes가 없다.
    /// </summary>
    [Serializable]
    public class MetaReward
    {
        public MetaRewardType type;
        [Tooltip("type = Currency일 때")]
        public CurrencyType currency;
        [Tooltip("Item = 아이템 ID / Lineage = 계보 ID / Costume = 의상 ID")]
        public string itemId;
        [Tooltip("개수 (GoldMinutes는 분, 바구니 줄은 시간당 양)")]
        public double amount;

        public static MetaReward Currency(CurrencyType currency, double amount) =>
            new() { type = MetaRewardType.Currency, currency = currency, amount = amount };

        public static MetaReward Gold(double amount) => Currency(CurrencyType.Gold, amount);

        public static MetaReward Gem(double amount) => Currency(CurrencyType.Gem, amount);

        public static MetaReward Item(string itemId, double count) => new() { type = MetaRewardType.Item, itemId = itemId, amount = count };

        /// <summary>단추 N분치 (받는 순간의 초당 단추 기준)</summary>
        public static MetaReward GoldMinutes(double minutes) => new() { type = MetaRewardType.GoldMinutes, amount = minutes };

        public static MetaReward QuickIdleTicket(int count = 1) => new() { type = MetaRewardType.QuickIdleTicket, amount = count };

        public static MetaReward DoubleTicket(int count = 1) => new() { type = MetaRewardType.DoubleTicket, amount = count };

        public static MetaReward SelectShards(int count) => new() { type = MetaRewardType.SelectShards, amount = count };

        public static MetaReward Lineage(string lineageId) => new() { type = MetaRewardType.Lineage, itemId = lineageId, amount = 1 };

        public static MetaReward Costume(string costumeId) => new() { type = MetaRewardType.Costume, itemId = costumeId, amount = 1 };

        /// <summary>RewardService로 지급할 수 있는 재화/재료면 RewardData로 바꾼다. 메타 전용 보상이면 null</summary>
        public RewardData ToRewardData() => type switch
        {
            MetaRewardType.Currency => RewardData.Currency(currency, amount),
            MetaRewardType.Item => RewardData.Item(itemId, (int)Math.Round(amount)),
            _ => null,
        };

        public static MetaReward FromRewardData(RewardData reward) => reward == null
            ? null
            : reward.type == RewardType.Currency
                ? Currency(reward.currency, reward.amount)
                : Item(reward.itemId, reward.amount);

        public MetaReward Clone() => new() { type = type, currency = currency, itemId = itemId, amount = amount };

        /// <summary>같은 종류(합칠 수 있는) 보상인지</summary>
        public bool IsSameKind(MetaReward other) =>
            other != null && other.type == type && (type != MetaRewardType.Currency || other.currency == currency) && other.itemId == itemId;
    }

    // ═════════════════════════ F-03 기능 개방 ═════════════════════════

    [Serializable]
    public class FeatureUnlockDef
    {
        public FeatureId feature;
        [Tooltip("기능 이름 (예: 수선 바구니)")]
        public string name;
        [Tooltip("하나라도 충족하면 열린다. 비어 있으면 처음부터 열림")]
        public List<MetaCondition> conditions = new();
        [Tooltip("conditions 중 하나에 더해 이것이 모두 충족해야 열린다 (AND, 예: 앱을 켜 둔 시간 PlayMinutes ≥ 20 — SPEC5 §2.8). 비면 없음. 강제 개방 · 옛 세이브 조용한 동기화는 보지 않는다")]
        public List<MetaCondition> alsoRequire = new();
        [Tooltip("잠긴 탭/버튼 아래 작은 글자 (예: 1-4)")]
        public string lockedHint;
        [Tooltip("잠긴 기능을 눌렀을 때 토스트 (예: 스테이지 1-4를 클리어하면 열려요)")]
        public string lockedToast;
        [Tooltip("개방 카드를 띄울지 (끄면 빨간 점만)")]
        public bool showCard = true;
        [Tooltip("개방 카드 제목")]
        public string cardTitle = "새 기능이 열렸어요!";
        [Tooltip("개방 카드 한 줄 (예: 자리를 비워도 바구니에 선물이 쌓여요)")]
        [TextArea] public string cardText;
        public Sprite icon;
        [Tooltip("개방 카드 일러스트 (없으면 icon)")]
        public Sprite illustration;
        [Tooltip("열릴 때 받는 보상 (IDLE-05 자동화 해금 보상 등, 비어도 됨)")]
        public List<MetaReward> rewards = new();
    }

    // ═════════════════════════ Q-01 가이드 퀘스트 ═════════════════════════

    [Serializable]
    public class GuideQuestDef
    {
        [Tooltip("gq_01 …")]
        public string id;
        [Tooltip("한 줄 문구 (예: 잔향이를 불러 보세요)")]
        public string text;
        public MetaCondition condition = new();
        public List<MetaReward> rewards = new();
        [Tooltip("줄을 눌렀을 때 갈 기능 (Battle = 이동 없음)")]
        public FeatureId navFeature = FeatureId.Battle;
        [Tooltip("이동한 뒤 손가락을 3초 띄울 대상 (Canvas 기준 경로, CoachTargets 상수). 비면 없음")]
        public string focusTarget;
        public Sprite icon;
    }

    /// <summary>콘텐츠 목록(1~30단계)이 끝난 뒤 반복 단계 규칙</summary>
    [Serializable]
    public class GuideRepeatConfig
    {
        [Tooltip("반복 단계를 쓸지")]
        public bool enabled = true;
        [Tooltip("\"스테이지 X 클리어\": 지금 최고 스테이지에서 이만큼 앞")]
        public int stageInterval = 2;
        [Tooltip("\"수선점 Lv +N\"")]
        public int shopLevelStep = 10;
        [Tooltip("\"친구 레벨 +N\" (가장 높은 계보 기준)")]
        public int lineageLevelStep = 5;
        [Tooltip("반복 보상: 단추 N분치")]
        public double goldMinutes = 5;
        [Tooltip("반복 보상: 별조각")]
        public double gem = 20;
        public Sprite icon;
    }

    // ═════════════════════════ Q-02 수선 일지 ═════════════════════════

    [Serializable]
    public class MissionDef
    {
        [Tooltip("모든 미션을 통틀어 고유 (md_login, mw_boss …)")]
        public string id;
        public MissionPeriod period;
        [Tooltip("문구 (예: 잔향이 30번 부르기)")]
        public string text;
        [Tooltip("진행도 = 이 카운터의 오늘(일일)/이번 주(주간) 값")]
        public CounterKey counter;
        public long target = 1;
        [Tooltip("받으면 얻는 일지 점수")]
        public int points = 10;
        [Tooltip("점수 외 보상 (보통 비움, 상자가 보상)")]
        public List<MetaReward> rewards = new();
        [Tooltip("이 기능이 잠겨 있으면 미션을 숨기고 그날 총점에서 뺀다 (Battle = 항상 보임)")]
        public FeatureId requiredFeature = FeatureId.Battle;
        [Tooltip("[가기] 이동 기능")]
        public FeatureId navFeature = FeatureId.Battle;
        public string focusTarget;
        public Sprite icon;
    }

    [Serializable]
    public class MissionChestDef
    {
        public MissionPeriod period;
        [Tooltip("이 점수에 도달하면 받을 수 있음 (20/40/60/80/100)")]
        public int points = 20;
        public List<MetaReward> rewards = new();
    }

    // ═════════════════════════ Q-03 출석부 ═════════════════════════

    [Serializable]
    public class AttendanceDayDef
    {
        public AttendanceTrack track;
        [Tooltip("1부터 (새싹 1~7, 정규 1~28)")]
        public int day = 1;
        public List<MetaReward> rewards = new();
        [Tooltip("큰 칸으로 강조 (7일째 친구 확정, 7/14/21/28일 등)")]
        public bool highlight;
        [Tooltip("칸 아래 짧은 설명 (비면 보상 설명)")]
        public string label;
    }

    // ═════════════════════════ IDLE-01~03 수선 바구니 / 빠른 수선 / 복귀 화면 ═════════════════════════

    /// <summary>바구니 한 줄 (시간당 양). 받는 순간의 최고 스테이지로 계산한다</summary>
    [Serializable]
    public class IdleChestLine
    {
        [Tooltip("Currency 또는 Item. amount = 1스테이지 기준 시간당 양")]
        public MetaReward reward = new();
        [Tooltip("최고 스테이지(도달)가 이 값 이상일 때만 쌓임 (1 = 처음부터)")]
        public int minStage = 1;
        [Tooltip("이벤트 중에만 (EVT-01 전까지는 쌓이지 않음)")]
        public bool eventOnly;
        [Tooltip("스테이지 배율 상한 (0 = 없음, 지금). 별조각 줄은 1.5 — 시간당 양 = amount × min(스테이지 배율, 이 값) (SPEC4 §13.4 C-01)")]
        public double maxStageMultiplier;

        /// <summary>carry 저장 키 (예: "item:mat_cloth", "currency:Gem")</summary>
        public string Key => reward == null
            ? string.Empty
            : reward.type == MetaRewardType.Currency ? $"currency:{reward.currency}" : $"item:{reward.itemId}";
    }

    [Serializable]
    public class IdleCapStep
    {
        [Tooltip("이 스테이지를 클리어하면 (MetaCondition.StageIndex, 2-1 = 11)")]
        public int stageCleared = 11;
        [Tooltip("수선점 레벨이 이 값 이상이면 (0 = 보지 않음). 0이 아닌 조건이 모두 맞아야 이 단계가 적용된다 (SPEC4 §13.4, Lv.200 16시간)")]
        public int shopLevel;
        [Tooltip("바구니 상한 (시간)")]
        public float capHours = 12f;
    }

    [Serializable]
    public class IdleChestConfig
    {
        [Header("상한")]
        [Tooltip("기본 상한 (시간). GameConfig.offlineMaxHours와 맞춘다")]
        public float baseCapHours = 8f;
        [Tooltip("스테이지 클리어로 늘어나는 상한 (가장 높은 단계 하나만 적용)")]
        public List<IdleCapStep> capSteps = new();

        [Header("시간당 양")]
        public List<IdleChestLine> lines = new();
        [Tooltip("최고 스테이지 1단계마다 시간당 양 배율 (복리). 곡선 수정(P4-C) 뒤 1.0025 — MetaContent.IdleChest와 맞춘다")]
        public double stageGrowth = 1.0025;
        [Tooltip("단추 줄 = 초당 단추 × 비운 시간 × 이 비율 (앱을 닫아 둔 동안만, GameConfig.offlineRewardRate와 맞춘다)")]
        [Range(0f, 1f)] public float offlineGoldRate = 0.5f;
        [Tooltip("이 시간(초)보다 짧게 쌓였으면 받을 것이 없다고 본다")]
        public int minClaimSeconds = 60;

        [Header("바구니 그림 / 점")]
        [Tooltip("채움 비율 경계 → 그림 0~3단계 (idle_basket_0..3)")]
        public float[] artStageThresholds = { 0.25f, 0.6f, 1f };
        [Tooltip("이 시간 이상 쌓이면 빨간 점")]
        public float redDotHours = 1f;

        [Header("빠른 수선 (IDLE-02)")]
        [Tooltip("빠른 수선 1번 = 바구니 몇 시간치 (모든 줄, 단추 줄 포함)")]
        public float quickIdleHours = 2f;
        [Tooltip("하루 n번째 빠른 수선 별조각 비용 (0 = 무료). 길이 = 하루 최대 횟수")]
        public int[] quickIdleGemCosts = { 0, 50, 100 };
        [Tooltip("빠른 수선권 보관 상한")]
        public int maxQuickIdleTickets = 20;

        [Header("복귀 화면 (IDLE-03)")]
        [Tooltip("이 시간(초) 이상 비웠다 돌아오면 복귀 화면, 짧으면 토스트만")]
        public int returnPopupMinSeconds = 600;
        [Tooltip("무료 두 배 받기 하루 횟수")]
        public int freeDoublePerDay = 1;
        [Tooltip("두 배에서 별조각 제외")]
        public bool doubleExcludesGem = true;
    }

    /// <summary>복귀 화면의 계보 성격 한 줄 (가진 계보 중 무작위)</summary>
    [Serializable]
    public class ReturnLineDef
    {
        public string lineageId;
        [Tooltip("예: 문 앞 양말 친구들을 모두 가족이라 불렀어요")]
        public List<string> lines = new();
    }

    // ═════════════════════════ FTUE-01 첫 10분 ═════════════════════════

    [Serializable]
    public class FtueStepDef
    {
        [Tooltip("ftue_intro, ftue_first_summon … (고유)")]
        public string id;
        [Tooltip("시작 조건")]
        public FtueTrigger trigger;
        [Tooltip("FeatureUnlocked = FeatureId 이름 / AfterDelay = 초")]
        public string triggerArg;
        [Tooltip("손가락·구멍을 낼 대상 (Canvas 기준 경로, CoachTargets 상수). 비면 말풍선만")]
        public string coachTarget;
        [Tooltip("말풍선 문구 (최대 2줄, 한 줄 40자)")]
        [TextArea] public string text;
        [Tooltip("대상 밖 입력 막기")]
        public bool blockInput;
        [Tooltip("손가락 대신 가벼운 토스트 힌트로만 (선택 단계)")]
        public bool hintOnly;
        [Tooltip("이 조건이 충족되면 다음 단계로")]
        public MetaCondition complete = new();
        [Tooltip("시작할 때 할 일")]
        public FtueAction action;
        [Tooltip("action 인자 (계보 ID / 시간 / 분)")]
        public string actionArg;
        [Tooltip("끝낼 때 받는 보상")]
        public List<MetaReward> rewards = new();
    }

    // ═════════════════════════ PRG-01 전투력 ═════════════════════════

    [Serializable]
    public class PowerConfig
    {
        [Tooltip("전투력 = Σ지킴이 × guardianWeight + Σ덱 잔향이 × echoWeight + 수선점 최대 내구도 × shopHpWeight")]
        public float guardianWeight = 1f;
        public float echoWeight = 0.6f;
        public float shopHpWeight = 0.3f;
        [Tooltip("권장 전투력 rec(s) = base × growth^(s-1) (에디터 보정 메뉴가 갱신)")]
        public double recommendedBase = 300;
        public double recommendedGrowth = 1.12;
        [Tooltip("비율 ≥ 이 값이면 여유")]
        public float comfortableRatio = 1f;
        [Tooltip("비율 ≥ 이 값이면 도전, 아래면 어려움")]
        public float challengeRatio = 0.8f;

        [Header("4차: 권장 전투력 구간 · 던전 (PRG-01, S5-24) — 0이면 꺼짐 (지금 계산 그대로)")]
        [Tooltip("이 스테이지 뒤로는 recommendedGrowthLate로 자란다 (0 = 꺼짐, 지수 하나 = 지금)")]
        public int recommendedTaperStage;
        [Tooltip("recommendedTaperStage 뒤의 권장 전투력 성장률")]
        public double recommendedGrowthLate;
        [Tooltip("세 번째 구간 시작 스테이지 (0 = 꺼짐, SPEC5 §4.3 콘텐츠 151 — 적 두 번째 완만해짐과 같은 자리)")]
        public int recommendedTaperStage2;
        [Tooltip("recommendedTaperStage2 뒤의 권장 전투력 성장률 (보정 메뉴가 맞춘다)")]
        public double recommendedGrowthLate2;
        [Tooltip("수선점 몫 상한: 전투력 = 공격 + min(수선점 내구도 × shopHpWeight, 공격 × 이 값) (SPEC5 §2.3 · §4.3 기본 1 = 수선점 몫은 전체의 절반까지). 지금은 읽는 곳이 없다 — P4-1 PowerSystem이 쓴다")]
        public float shopShareCap = 1f;
        [Tooltip("던전 권장 전투력 = base × growth^(층-1) (0 = 던전 권장 줄 없음)")]
        public double dungeonRecommendedBase;
        public double dungeonRecommendedGrowth = 1.1;
    }

    // ═════════════════════════ 4차 정의 (SPEC4 §2.3 — 모양은 P4-0, 내용은 Editor/Content/MetaContent.*) ═════════════════════════

    /// <summary>수선점 기록 한 단계 (Q-04). id = {lineId}_{tier}. 줄마다 낮은 단계부터 하나씩 보여 준다</summary>
    [Serializable]
    public class AchievementDef
    {
        [Tooltip("ach_stage_3 … (고유)")]
        public string id;
        [Tooltip("같은 줄 단계들이 공유하는 줄 ID (ach_stage)")]
        public string lineId;
        [Tooltip("줄 안의 단계 (1부터, 오름차순)")]
        public int tier = 1;
        public AchievementCategory category;
        [Tooltip("줄 제목 (예: 스테이지 3-10 클리어)")]
        public string title;
        public MetaCondition condition = new();
        public List<MetaReward> rewards = new();
        [Tooltip("숨은 기록 (찾기 전에는 '???' + 힌트)")]
        public bool hidden;
        [Tooltip("숨은 기록 힌트 (해요체)")]
        public string hint;
        [Tooltip("숨은 기록을 찾은 뒤 보여 줄 한 줄 (SPEC4 부록 A.1 'Done descriptions', 해요체 — 비면 보이지 않음)")]
        public string doneText;
        [Tooltip("[가기] 이동 기능 (Battle = 이동 없음)")]
        public FeatureId navFeature = FeatureId.Battle;
        [Tooltip("이동한 뒤 손가락을 띄울 대상 (CoachTargets)")]
        public string focusTarget;
        public Sprite icon;
    }

    /// <summary>도감 보상 · 영구 모음 보너스 (Q-05)</summary>
    [Serializable]
    public class CodexConfig
    {
        [Header("처음 찾은 보상 (별조각)")]
        public int formBaseGem = 20;
        public int formTier1Gem = 30;
        public int formTier2Gem = 50;
        public int costumeGem = 30;
        public int enemyGem = 10;
        public int synergyFirstGem = 20;
        [Tooltip("시너지 최고 단계 (synergymax:*) 보상 (조정 #5: 30)")]
        public int synergyMaxGem = 30;
        public int materialGem = 5;

        [Header("영구 모음 보너스 (도감이 열린 뒤 저절로, SynergySystem.ExtraTeamModifiers)")]
        [Tooltip("찾은 형태 수 → 친구 공격력·체력 % (그 단계의 합계, 더하지 않음)")]
        public List<CodexBonusTier> formTiers = new();
        [Tooltip("적을 모두 찾으면 수선점 최대 내구도 %")]
        public float allEnemiesShopHpPercent = 5;
        [Tooltip("켜 본 시너지 종류 수 → 불빛 충전 % (합계)")]
        public List<CodexBonusTier> synergyTiers = new();

        [Header("문구")]
        public List<CodexHintDef> hints = new();
        public List<CodexEnemyText> enemyTexts = new();
    }

    /// <summary>도감 보너스 한 단계 (count개 이상이면 percent — 그 단계의 합계 값)</summary>
    [Serializable]
    public class CodexBonusTier
    {
        public int count;
        public float percent;
    }

    /// <summary>못 찾은 도감 칸 힌트 (key = CodexSystem.Key(...))</summary>
    [Serializable]
    public class CodexHintDef
    {
        public string key;
        [TextArea] public string hint;
    }

    /// <summary>찾은 적의 도감 문구</summary>
    [Serializable]
    public class CodexEnemyText
    {
        public string enemyId;
        [TextArea] public string text;
    }

    /// <summary>소환 마일스톤 한 단계 (PRG-04, 누적 소환 수 오름차순)</summary>
    [Serializable]
    public class GachaMilestoneDef
    {
        public int pulls;
        public List<MetaReward> rewards = new();
    }

    /// <summary>마지막 단계 뒤로 interval마다 되풀이되는 보상</summary>
    [Serializable]
    public class GachaRepeatMilestone
    {
        [Tooltip("마지막 단계 뒤 이 소환 수마다 (0 = 되풀이 없음)")]
        public int interval = 100;
        public List<MetaReward> rewards = new();
    }

    /// <summary>이벤트 하나 (EVT-01). 일정은 DailyClock 날 키 기준 (DailyHours만 현지 시각)</summary>
    [Serializable]
    public class EventDef
    {
        [Tooltip("evt_weekend_expedition … (고유)")]
        public string id;
        public string name;
        [Tooltip("시작 카드 · 모아 보기 한 줄")]
        public string shortDesc;
        public EventKind kind;
        public EventScheduleType scheduleType;
        [Tooltip("Weekdays: bit0 = 월요일 … bit6 = 일요일")]
        public int weekdayMask;
        [Tooltip("MonthDays / Dates: 시작 · 끝 날 (양끝 포함)")]
        public int fromDay, toDay;
        [Tooltip("Dates: 시작 · 끝 달 (1~12, 해를 넘을 수 있음 — 예약)")]
        public int fromMonth, toMonth;
        [Tooltip("DailyHours: 시작 · 끝 시 (현지, 끝은 포함 안 함, 자정을 넘을 수 있음)")]
        public int fromHour, toHour;
        [Tooltip("배율 효과 (원정 재료 ×2, 별빛 사탕 ×2, 바구니 +20% = 1.2)")]
        public double multiplier = 1;
        [Tooltip("더하기 효과 (던전 입장권 +2)")]
        public int bonus;
        public List<EventMissionDef> missions = new();
        public List<EventStampDef> stamps = new();
        [Tooltip("시작 카드를 띄울지 (회차마다 한 번)")]
        public bool startCard;
        public Sprite banner;
        public Sprite icon;
    }

    /// <summary>이벤트 미션 (진행도 = 평생 카운터 − 회차 시작 기준값)</summary>
    [Serializable]
    public class EventMissionDef
    {
        public string id;
        public string text;
        public CounterKey counter;
        public long target = 1;
        public List<MetaReward> rewards = new();
        [Tooltip("모든 미션을 마치면 받는 보너스 미션")]
        public bool isBonus;
    }

    /// <summary>이벤트 도장 한 칸 (하루 한 번, 최대 7)</summary>
    [Serializable]
    public class EventStampDef
    {
        public int day = 1;
        public List<MetaReward> rewards = new();
        public bool highlight;
    }

    /// <summary>별빛 산책 수첩 (Q-06, 28일 · 30칸 · 칸마다 90 경험치)</summary>
    [Serializable]
    public class SeasonConfig
    {
        public string name = "별빛 산책 수첩";
        public int days = 28;
        public int xpPerTier = 90;
        public List<SeasonTierDef> tiers = new();
        public Sprite banner;
    }

    /// <summary>수첩 한 칸 (fallback = 의상을 이미 가졌을 때 대신 받는 보상)</summary>
    [Serializable]
    public class SeasonTierDef
    {
        public int tier = 1;
        public List<MetaReward> rewards = new();
        public List<MetaReward> fallback = new();
        public bool highlight;
    }

    /// <summary>다시 만난 날 (CR-06)</summary>
    [Serializable]
    public class ComebackConfig
    {
        [Tooltip("이 시간(초) 이상 비우면 (72시간)")]
        public long minAwaySeconds = 259200;
        [Tooltip("다시 열리기까지 쉬는 날")]
        public int cooldownDays = 14;
        [Tooltip("평생 최대 횟수")]
        public int lifetimeMax = 6;
        [Tooltip("한 번짜리 바구니 상한 (시간)")]
        public float bonusCapHours = 24;
        public List<ComebackDayDef> days = new();
    }

    [Serializable]
    public class ComebackDayDef
    {
        public int day = 1;
        public List<MetaReward> rewards = new();
    }

    /// <summary>호감도 (CR-10). thresholds[n-1] = Lv.n이 되는 누적 호감도</summary>
    [Serializable]
    public class AffinityConfig
    {
        public long[] thresholds = { 0, 50, 150, 350, 700, 1200, 2000, 3200, 4800, 7000 };
        [Tooltip("단계 보상 별조각 = 이 값 × 단계 (조정 #6: 10)")]
        public int gemPerLevel = 10;
        [Tooltip("쓰다듬기 한 번 호감도")]
        public int petAmount = 5;
        [Tooltip("계보마다 하루 쓰다듬기 횟수")]
        public int petsPerDay = 3;
        [Tooltip("하트 바느질 카드 테두리 단계")]
        public int frameLevel = 5;
        [Tooltip("기다리는 몸짓 단계")]
        public int poseLevel = 10;
        public List<AffinityLineDef> lineages = new();
    }

    /// <summary>계보별 호감도 문구: 기억 조각 5줄 (Lv.2/4/6/8/10) + 쓰다듬기 말풍선 7줄</summary>
    [Serializable]
    public class AffinityLineDef
    {
        public string lineageId;
        public string[] story = new string[5];
        public string[] pet = new string[7];
    }

    /// <summary>동화책 한 장 (CR-05, 챕터 보스 첫 클리어 뒤)</summary>
    [Serializable]
    public class StoryChapterDef
    {
        public int chapter = 1;
        public string title;
        public string place;
        public List<StoryPanelDef> panels = new();
        [Tooltip("처음 읽고 닫을 때 받는 선물 (한 번)")]
        public List<MetaReward> firstReadRewards = new();
    }

    [Serializable]
    public class StoryPanelDef
    {
        public Sprite art;
        [Tooltip("그림 아래 한 줄 (40자 이하)")]
        public string text;
    }

    /// <summary>잃어버린 상자 (클리어마다 확률) + 챕터 상자 (챕터 보스 첫 클리어) — PRG-06</summary>
    [Serializable]
    public class LostChestConfig
    {
        [Tooltip("튜토리얼 뒤 일반 클리어마다 이 확률로 한 칸")]
        public float dropChance = 0.6f;
        public List<LostChestTable> tables = new();
        public List<ChapterChestDef> chapterChests = new();
    }

    /// <summary>스테이지 구간의 잃어버린 상자 표 (fromStage ~ toStage, 양끝 포함)</summary>
    [Serializable]
    public class LostChestTable
    {
        public int fromStage = 1, toStage = 10;
        public List<LostChestEntry> entries = new();
    }

    [Serializable]
    public class LostChestEntry
    {
        public string itemId;
        public int min = 1, max = 1;
        public int weight = 1;
    }

    /// <summary>챕터 상자 (chapter 0 = 줄이 없는 챕터의 기본)</summary>
    [Serializable]
    public class ChapterChestDef
    {
        public int chapter;
        public List<MetaReward> rewards = new();
    }

    /// <summary>쿠폰 (EVT-02). 번호는 저장하지 않고 해시만 (SHA256(couponSalt + ":" + 번호), 소문자 16진수)</summary>
    [Serializable]
    public class CouponDef
    {
        public string hash;
        [Tooltip("사용 기간 날 키 (비면 제한 없음)")]
        public string validFromDayKey;
        public string validToDayKey;
        [Tooltip("우편으로 보낼 보상 (재화 · 재료만 — 우편은 메타 보관함 보상을 못 싣는다)")]
        public List<RewardData> rewards = new();
        public string mailTitle;
        [TextArea] public string mailBody;
    }

    /// <summary>잔향 축복 (PRG-07, 던전 전용 영구 능력치)</summary>
    [Serializable]
    public class BlessingDef
    {
        public string id;
        public string name;
        [TextArea] public string desc;
        public StatType stat;
        public float value;
        public int maxStacks = 2;
        [Tooltip("Icons/{iconKey}")]
        public string iconKey;
    }

    /// <summary>첫 주 (NEW-01, 7일 과제 · 14일 창)</summary>
    [Serializable]
    public class StarterWeekConfig
    {
        [Tooltip("시작한 날부터 받을 수 있는 날 수")]
        public int windowDays = 14;
        [Tooltip("개방 때 설치 일수가 이 값 이하일 때만 시작")]
        public int maxInstallDays = 7;
        public List<StarterTaskDef> tasks = new();
        public List<StarterChestDef> chests = new();
    }

    /// <summary>첫 주 과제 (day일째 열림, 조건을 한 번 넘으면 완료로 굳는다)</summary>
    [Serializable]
    public class StarterTaskDef
    {
        public string id;
        public int day = 1;
        public string text;
        public MetaCondition condition = new();
        public int points = 5;
        [Tooltip("잠겨 있으면 잠금 표시 (창 안에서 나중에 받을 수 있음)")]
        public FeatureId requiredFeature = FeatureId.Battle;
        public FeatureId navFeature = FeatureId.Battle;
        public string focusTarget;
    }

    [Serializable]
    public class StarterChestDef
    {
        public int points = 30;
        public List<MetaReward> rewards = new();
        public bool highlight;
    }

    /// <summary>오늘의 작은 선물 (NEW-02, weekday = 날 키 요일, 월요일 = 0)</summary>
    [Serializable]
    public class DailyGiftDef
    {
        public int weekday;
        public List<MetaReward> rewards = new();
    }

    /// <summary>길 잃은 반딧불 (NEW-03)</summary>
    [Serializable]
    public class FireflyConfig
    {
        [Tooltip("다음 반딧불까지 (초, 앞에서 전투를 보는 시간)")]
        public float minInterval = 360, maxInterval = 720;
        [Tooltip("머무는 시간 (초)")]
        public float lifeSeconds = 8;
        [Tooltip("하루 최대 잡기")]
        public int perDay = 6;
        public List<FireflyRewardDef> rewards = new();
    }

    /// <summary>반딧불 보상 한 칸 (fromLostChestTable = 최고 스테이지의 잃어버린 상자 표에서 재료 하나)</summary>
    [Serializable]
    public class FireflyRewardDef
    {
        public MetaReward reward = new();
        public bool fromLostChestTable;
        public int weight = 1;
    }

    /// <summary>친구들의 응원 (PRG-03, 장마다 한 번)</summary>
    [Serializable]
    public class CheerConfig
    {
        public float attackPercent = 20;
        public int durationSeconds = 600;
    }

    /// <summary>보스 첫 만남 · 다시 도전 때 한 줄 힌트 (PRG-02)</summary>
    [Serializable]
    public class BossHintDef
    {
        public string enemyId;
        public string hint;
    }

    /// <summary>백업 알림 (SAVE-01 · 델타 17, RM-2 SB-1): 백업 없이 days일(날 키)이 지나면 우편 한 통. 규칙은 BackupReminderSystem(P4-2), 문구는 콘텐츠(P4-8)</summary>
    [Serializable]
    public class BackupReminderConfig
    {
        [Tooltip("마지막 백업(세이브 코드 내보내기) 뒤 우편까지 날 수 (날 키 기준)")]
        public int days = 14;
        [Tooltip("알림 우편 제목")]
        public string mailTitle = "";
        [Tooltip("알림 우편 본문")]
        public string mailBody = "";
    }

    // ═════════════════════════ 데이터베이스 ═════════════════════════

    /// <summary>
    /// 메타 기능 데이터 (기능 개방 / 가이드 퀘스트 / 수선 일지 / 출석부 / 수선 바구니 / 튜토리얼 / 전투력).
    /// 내용은 Editor/Content/MetaContent*.cs가 원본이고 Editor/MetaDataBuilder가 덮어쓴다.
    /// 비어 있어도 게임이 돈다: 정의 없는 기능은 열림, 퀘스트/미션/출석/튜토리얼은 없음.
    /// </summary>
    [CreateAssetMenu(fileName = "MetaDatabase", menuName = "IdleGame/Meta Database")]
    public class MetaDatabase : ScriptableObject
    {
        [Header("F-03 기능 개방")]
        public List<FeatureUnlockDef> features = new();

        [Header("Q-01 가이드 퀘스트")]
        public List<GuideQuestDef> guideQuests = new();
        public GuideRepeatConfig guideRepeat = new();
        [Tooltip("가이드 퀘스트 줄이 보이기 시작하는 조건 (FTUE 5단계 첫 보스 뒤)")]
        public MetaCondition guideVisible = new();

        [Header("Q-02 수선 일지")]
        public List<MissionDef> missions = new();
        public List<MissionChestDef> missionChests = new();

        [Header("Q-03 출석부")]
        public List<AttendanceDayDef> attendance = new();

        [Header("IDLE-01~03 수선 바구니")]
        public IdleChestConfig idleChest = new();
        public List<ReturnLineDef> returnLines = new();

        [Header("FTUE-01 첫 10분")]
        public List<FtueStepDef> ftueSteps = new();

        [Header("PRG-01 전투력")]
        public PowerConfig power = new();

        // ── 4차 (SPEC4 §2.3 · §13) — 비어 있으면 그 기능은 아무것도 하지 않는다 ──

        [Header("Q-04 수선점 기록")]
        public List<AchievementDef> achievements = new();

        [Header("Q-05 도감")]
        public CodexConfig codex = new();

        [Header("PRG-04 소환 마일스톤")]
        [Tooltip("누적 소환 수 오름차순")]
        public List<GachaMilestoneDef> gachaMilestones = new();
        public GachaRepeatMilestone gachaRepeat = new();

        [Header("EVT-01 이벤트 달력")]
        public List<EventDef> events = new();

        [Header("Q-06 별빛 산책 수첩")]
        public SeasonConfig season = new();

        [Header("CR-06 다시 만난 날")]
        public ComebackConfig comeback = new();

        [Header("CR-10 호감도")]
        public AffinityConfig affinity = new();

        [Header("CR-05 동화책")]
        public List<StoryChapterDef> storyChapters = new();

        [Header("PRG-06 잃어버린 상자 · 챕터 상자")]
        public LostChestConfig lostChest = new();

        [Header("EVT-02 쿠폰")]
        public List<CouponDef> coupons = new();
        [Tooltip("쿠폰 해시 소금 (번호는 저장하지 않는다)")]
        public string couponSalt = "";

        [Header("PRG-07 잔향 축복")]
        public List<BlessingDef> blessings = new();

        [Header("NEW-01 첫 주")]
        public StarterWeekConfig starterWeek = new();

        [Header("NEW-02 오늘의 작은 선물")]
        [Tooltip("7줄, weekday = 날 키 요일 (월요일 = 0)")]
        public List<DailyGiftDef> dailyGifts = new();

        [Header("NEW-03 길 잃은 반딧불")]
        public FireflyConfig firefly = new();

        [Header("PRG-03 친구들의 응원")]
        public CheerConfig cheer = new();

        [Header("PRG-02 보스 힌트")]
        public List<BossHintDef> bossHints = new();

        [Header("NEW-04 입장권 모아 두기")]
        [Tooltip("모아 둘 수 있는 던전 입장권 (0 = 끔 = 지금처럼 날마다 초기화, 콘텐츠 9). DungeonSystem.TicketBankCap 훅으로 읽는다")]
        public int dungeonTicketBankCap;

        [Header("SAVE-01 백업 알림")]
        public BackupReminderConfig backupReminder = new();

        // ── 조회 (캐시는 OnValidate에서 비운다) ──

        Dictionary<FeatureId, FeatureUnlockDef> _features;
        Dictionary<string, MissionDef> _missions;
        List<MissionDef>[] _missionsByPeriod;
        List<MissionChestDef>[] _chestsByPeriod;
        List<AttendanceDayDef>[] _attendanceByTrack;
        Dictionary<string, ReturnLineDef> _returnLines;

        /// <summary>테스트/폴백용 빈 데이터베이스</summary>
        public static MetaDatabase CreateEmpty()
        {
            var db = CreateInstance<MetaDatabase>();
            db.name = "MetaDatabase (empty)";
            return db;
        }

        /// <summary>기능 정의. 없으면 null (= 처음부터 열림)</summary>
        public FeatureUnlockDef GetFeature(FeatureId feature)
        {
            EnsureCache();
            return _features.TryGetValue(feature, out var def) ? def : null;
        }

        public MissionDef GetMission(string missionId)
        {
            EnsureCache();
            return missionId != null && _missions.TryGetValue(missionId, out var def) ? def : null;
        }

        /// <summary>주기별 미션 (콘텐츠 순서)</summary>
        public IReadOnlyList<MissionDef> GetMissions(MissionPeriod period)
        {
            EnsureCache();
            return _missionsByPeriod[(int)period];
        }

        /// <summary>주기별 점수 상자 (점수 오름차순). 인덱스가 MissionPeriodSave.chestsClaimed 키</summary>
        public IReadOnlyList<MissionChestDef> GetMissionChests(MissionPeriod period)
        {
            EnsureCache();
            return _chestsByPeriod[(int)period];
        }

        /// <summary>출석부 칸 (일차 오름차순)</summary>
        public IReadOnlyList<AttendanceDayDef> GetAttendance(AttendanceTrack track)
        {
            EnsureCache();
            return _attendanceByTrack[(int)track];
        }

        /// <summary>출석부 day일째 칸 (1부터). 없으면 null</summary>
        public AttendanceDayDef GetAttendanceDay(AttendanceTrack track, int day)
        {
            var days = GetAttendance(track);
            for (int i = 0; i < days.Count; i++)
            {
                if (days[i].day == day) return days[i];
            }
            return null;
        }

        public ReturnLineDef GetReturnLines(string lineageId)
        {
            EnsureCache();
            return lineageId != null && _returnLines.TryGetValue(lineageId, out var def) ? def : null;
        }

        public FtueStepDef GetFtueStep(int index) => index >= 0 && index < ftueSteps.Count ? ftueSteps[index] : null;

        public int IndexOfFtueStep(string stepId) => stepId == null ? -1 : ftueSteps.FindIndex(s => s != null && s.id == stepId);

        // ── 4차 조회 (캐시는 처음 읽을 때 한 번 만들고 ClearCache가 함께 비운다) ──

        Dictionary<string, AchievementDef> _achievements;
        Dictionary<string, List<AchievementDef>> _achievementLines;
        List<string> _achievementLineIds;
        List<GachaMilestoneDef> _gachaMilestonesSorted;
        Dictionary<string, EventDef> _events;
        Dictionary<int, SeasonTierDef> _seasonTiers;
        Dictionary<int, ComebackDayDef> _comebackDays;
        Dictionary<string, AffinityLineDef> _affinityLines;
        Dictionary<int, StoryChapterDef> _storyChapters;
        Dictionary<int, ChapterChestDef> _chapterChests;
        Dictionary<string, CouponDef> _coupons;
        Dictionary<string, BlessingDef> _blessings;
        Dictionary<string, StarterTaskDef> _starterTasks;
        List<StarterTaskDef>[] _starterTasksByDay;
        DailyGiftDef[] _dailyGiftsByWeekday;
        Dictionary<string, BossHintDef> _bossHints;
        Dictionary<string, CodexHintDef> _codexHints;
        Dictionary<string, CodexEnemyText> _codexEnemyTexts;

        static readonly List<AchievementDef> NoAchievements = new();
        static readonly List<StarterTaskDef> NoStarterTasks = new();

        public AchievementDef GetAchievement(string achievementId)
        {
            EnsureMeta4Cache();
            return achievementId != null && _achievements.TryGetValue(achievementId, out var def) ? def : null;
        }

        /// <summary>기록 줄 ID (콘텐츠에 처음 나온 순서)</summary>
        public IReadOnlyList<string> AchievementLineIds
        {
            get
            {
                EnsureMeta4Cache();
                return _achievementLineIds;
            }
        }

        /// <summary>한 줄의 단계 (tier 오름차순). 없으면 빈 목록</summary>
        public IReadOnlyList<AchievementDef> GetAchievementLine(string lineId)
        {
            EnsureMeta4Cache();
            return lineId != null && _achievementLines.TryGetValue(lineId, out var list) ? list : NoAchievements;
        }

        /// <summary>소환 마일스톤 (pulls 오름차순)</summary>
        public IReadOnlyList<GachaMilestoneDef> GetGachaMilestones()
        {
            EnsureMeta4Cache();
            return _gachaMilestonesSorted;
        }

        public EventDef GetEvent(string eventId)
        {
            EnsureMeta4Cache();
            return eventId != null && _events.TryGetValue(eventId, out var def) ? def : null;
        }

        public SeasonTierDef GetSeasonTier(int tier)
        {
            EnsureMeta4Cache();
            return _seasonTiers.TryGetValue(tier, out var def) ? def : null;
        }

        public ComebackDayDef GetComebackDay(int day)
        {
            EnsureMeta4Cache();
            return _comebackDays.TryGetValue(day, out var def) ? def : null;
        }

        public AffinityLineDef GetAffinityLines(string lineageId)
        {
            EnsureMeta4Cache();
            return lineageId != null && _affinityLines.TryGetValue(lineageId, out var def) ? def : null;
        }

        public StoryChapterDef GetStoryChapter(int chapter)
        {
            EnsureMeta4Cache();
            return _storyChapters.TryGetValue(chapter, out var def) ? def : null;
        }

        /// <summary>그 챕터의 상자 (줄이 없으면 chapter 0 기본 줄, 그것도 없으면 null)</summary>
        public ChapterChestDef GetChapterChest(int chapter)
        {
            EnsureMeta4Cache();
            if (_chapterChests.TryGetValue(chapter, out var def)) return def;
            return _chapterChests.TryGetValue(0, out def) ? def : null;
        }

        /// <summary>스테이지가 속한 잃어버린 상자 표 (없으면 null). 표는 몇 개 안 되어 그대로 훑는다 (할당 없음)</summary>
        public LostChestTable GetLostChestTable(int stage)
        {
            var tables = lostChest != null ? lostChest.tables : null;
            if (tables == null) return null;
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                if (table != null && stage >= table.fromStage && stage <= table.toStage) return table;
            }
            return null;
        }

        /// <summary>쿠폰 해시 (소문자 16진수)로 찾기</summary>
        public CouponDef GetCoupon(string hash)
        {
            EnsureMeta4Cache();
            return hash != null && _coupons.TryGetValue(hash, out var def) ? def : null;
        }

        public BlessingDef GetBlessing(string blessingId)
        {
            EnsureMeta4Cache();
            return blessingId != null && _blessings.TryGetValue(blessingId, out var def) ? def : null;
        }

        public StarterTaskDef GetStarterTask(string taskId)
        {
            EnsureMeta4Cache();
            return taskId != null && _starterTasks.TryGetValue(taskId, out var def) ? def : null;
        }

        /// <summary>첫 주 day일째 과제 (1~7, 콘텐츠 순서). 없으면 빈 목록</summary>
        public IReadOnlyList<StarterTaskDef> GetStarterTasks(int day)
        {
            EnsureMeta4Cache();
            return day >= 1 && day < _starterTasksByDay.Length ? _starterTasksByDay[day] : NoStarterTasks;
        }

        /// <summary>그 요일(월요일 = 0)의 작은 선물. 없으면 null</summary>
        public DailyGiftDef GetDailyGift(int weekday)
        {
            EnsureMeta4Cache();
            return weekday >= 0 && weekday < _dailyGiftsByWeekday.Length ? _dailyGiftsByWeekday[weekday] : null;
        }

        public BossHintDef GetBossHint(string enemyId)
        {
            EnsureMeta4Cache();
            return enemyId != null && _bossHints.TryGetValue(enemyId, out var def) ? def : null;
        }

        /// <summary>도감 칸 힌트 (key = CodexSystem.Key(...)). 없으면 null</summary>
        public CodexHintDef GetCodexHint(string key)
        {
            EnsureMeta4Cache();
            return key != null && _codexHints.TryGetValue(key, out var def) ? def : null;
        }

        public CodexEnemyText GetCodexEnemyText(string enemyId)
        {
            EnsureMeta4Cache();
            return enemyId != null && _codexEnemyTexts.TryGetValue(enemyId, out var def) ? def : null;
        }

        /// <summary>조회 캐시를 비운다 (콘텐츠를 코드로 덮어쓴 뒤)</summary>
        public void ClearCache()
        {
            _features = null;
            _achievements = null;
        }

        /// <summary>4차 조회 캐시 (_achievements가 null이면 전부 다시 만든다)</summary>
        void EnsureMeta4Cache()
        {
            if (_achievements != null) return;

            var achievementMap = new Dictionary<string, AchievementDef>();
            _achievementLines = new Dictionary<string, List<AchievementDef>>();
            _achievementLineIds = new List<string>();
            if (achievements != null)
            {
                foreach (var def in achievements)
                {
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    achievementMap[def.id] = def;
                    string line = string.IsNullOrEmpty(def.lineId) ? def.id : def.lineId;
                    if (!_achievementLines.TryGetValue(line, out var list))
                    {
                        _achievementLines[line] = list = new List<AchievementDef>();
                        _achievementLineIds.Add(line);
                    }
                    list.Add(def);
                }
            }
            foreach (var list in _achievementLines.Values) list.Sort((a, b) => a.tier.CompareTo(b.tier));

            _gachaMilestonesSorted = new List<GachaMilestoneDef>();
            if (gachaMilestones != null)
            {
                foreach (var def in gachaMilestones)
                {
                    if (def != null) _gachaMilestonesSorted.Add(def);
                }
            }
            _gachaMilestonesSorted.Sort((a, b) => a.pulls.CompareTo(b.pulls));

            _events = new Dictionary<string, EventDef>();
            if (events != null)
            {
                foreach (var def in events)
                {
                    if (def != null && !string.IsNullOrEmpty(def.id)) _events[def.id] = def;
                }
            }

            _seasonTiers = new Dictionary<int, SeasonTierDef>();
            if (season?.tiers != null)
            {
                foreach (var def in season.tiers)
                {
                    if (def != null) _seasonTiers[def.tier] = def;
                }
            }

            _comebackDays = new Dictionary<int, ComebackDayDef>();
            if (comeback?.days != null)
            {
                foreach (var def in comeback.days)
                {
                    if (def != null) _comebackDays[def.day] = def;
                }
            }

            _affinityLines = new Dictionary<string, AffinityLineDef>();
            if (affinity?.lineages != null)
            {
                foreach (var def in affinity.lineages)
                {
                    if (def != null && !string.IsNullOrEmpty(def.lineageId)) _affinityLines[def.lineageId] = def;
                }
            }

            _storyChapters = new Dictionary<int, StoryChapterDef>();
            if (storyChapters != null)
            {
                foreach (var def in storyChapters)
                {
                    if (def != null) _storyChapters[def.chapter] = def;
                }
            }

            _chapterChests = new Dictionary<int, ChapterChestDef>();
            if (lostChest?.chapterChests != null)
            {
                foreach (var def in lostChest.chapterChests)
                {
                    if (def != null) _chapterChests[def.chapter] = def;
                }
            }

            _coupons = new Dictionary<string, CouponDef>();
            if (coupons != null)
            {
                foreach (var def in coupons)
                {
                    if (def != null && !string.IsNullOrEmpty(def.hash)) _coupons[def.hash.ToLowerInvariant()] = def;
                }
            }

            _blessings = new Dictionary<string, BlessingDef>();
            if (blessings != null)
            {
                foreach (var def in blessings)
                {
                    if (def != null && !string.IsNullOrEmpty(def.id)) _blessings[def.id] = def;
                }
            }

            _starterTasks = new Dictionary<string, StarterTaskDef>();
            _starterTasksByDay = new List<StarterTaskDef>[8];
            for (int day = 0; day < _starterTasksByDay.Length; day++) _starterTasksByDay[day] = new List<StarterTaskDef>();
            if (starterWeek?.tasks != null)
            {
                foreach (var def in starterWeek.tasks)
                {
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    _starterTasks[def.id] = def;
                    if (def.day >= 1 && def.day < _starterTasksByDay.Length) _starterTasksByDay[def.day].Add(def);
                }
            }

            _dailyGiftsByWeekday = new DailyGiftDef[7];
            if (dailyGifts != null)
            {
                foreach (var def in dailyGifts)
                {
                    if (def != null && def.weekday >= 0 && def.weekday < _dailyGiftsByWeekday.Length) _dailyGiftsByWeekday[def.weekday] = def;
                }
            }

            _bossHints = new Dictionary<string, BossHintDef>();
            if (bossHints != null)
            {
                foreach (var def in bossHints)
                {
                    if (def != null && !string.IsNullOrEmpty(def.enemyId)) _bossHints[def.enemyId] = def;
                }
            }

            _codexHints = new Dictionary<string, CodexHintDef>();
            _codexEnemyTexts = new Dictionary<string, CodexEnemyText>();
            if (codex?.hints != null)
            {
                foreach (var def in codex.hints)
                {
                    if (def != null && !string.IsNullOrEmpty(def.key)) _codexHints[def.key] = def;
                }
            }
            if (codex?.enemyTexts != null)
            {
                foreach (var def in codex.enemyTexts)
                {
                    if (def != null && !string.IsNullOrEmpty(def.enemyId)) _codexEnemyTexts[def.enemyId] = def;
                }
            }

            // 마지막에 넣는다 — 위에서 예외가 나도 다음 조회가 다시 만들게
            _achievements = achievementMap;
        }

        void OnValidate() => ClearCache();

        void EnsureCache()
        {
            if (_features != null) return;

            _features = new Dictionary<FeatureId, FeatureUnlockDef>();
            foreach (var def in features)
            {
                if (def != null) _features[def.feature] = def;
            }

            _missions = new Dictionary<string, MissionDef>();
            _missionsByPeriod = new[] { new List<MissionDef>(), new List<MissionDef>() };
            foreach (var def in missions)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                _missions[def.id] = def;
                _missionsByPeriod[PeriodIndex(def.period)].Add(def);
            }

            _chestsByPeriod = new[] { new List<MissionChestDef>(), new List<MissionChestDef>() };
            foreach (var def in missionChests)
            {
                if (def != null) _chestsByPeriod[PeriodIndex(def.period)].Add(def);
            }
            foreach (var list in _chestsByPeriod) list.Sort((a, b) => a.points.CompareTo(b.points));

            _attendanceByTrack = new[] { new List<AttendanceDayDef>(), new List<AttendanceDayDef>() };
            foreach (var def in attendance)
            {
                if (def != null) _attendanceByTrack[def.track == AttendanceTrack.Monthly ? 1 : 0].Add(def);
            }
            foreach (var list in _attendanceByTrack) list.Sort((a, b) => a.day.CompareTo(b.day));

            _returnLines = new Dictionary<string, ReturnLineDef>();
            foreach (var def in returnLines)
            {
                if (def != null && !string.IsNullOrEmpty(def.lineageId)) _returnLines[def.lineageId] = def;
            }
        }

        static int PeriodIndex(MissionPeriod period) => period == MissionPeriod.Weekly ? 1 : 0;
    }
}
