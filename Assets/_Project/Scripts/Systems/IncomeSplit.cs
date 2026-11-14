using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 단추 · 실타래 나누기 (결정 2 안 D). 지금 단추가 나오는 모든 곳에서 지금 식으로 계산한 양(보너스 포함)을 B라 할 때
    /// 단추 = B × goldIncomeShare, 실타래 = B × lineageXpIncomeShare. 나누기는 지급 직전에 여기서 한 번만 한다.
    ///   · 정수로 주던 곳(보상 목록 · 메타 보상 · 바구니 · 우편)은 몫마다 내림 (GoldWhole · LineageXpWhole · AddCurrencyLines)
    ///   · 실수로 쌓던 곳(켜 둔 동안 초당 · 옛 오프라인)은 실수 그대로 (Gold · LineageXp)
    /// B는 나누기 전 기준 수입(GrowthSystem.IncomePerSecond)으로 만든다. 이미 나뉜 GoldPerSecond로 B를 만들어 다시 나누면 4분의 1이 된다.
    /// 몫은 Game.Setup이 GameConfig에서 넣는다 (Configure). 시험 · 진행 시뮬레이션은 Configure로 바꾸고 끝나면 되돌린다.
    /// </summary>
    public static class IncomeSplit
    {
        /// <summary>GameConfig 기본 몫 (단추 · 실타래 모두)</summary>
        public const double DefaultShare = 0.5;

        /// <summary>기준 양 B 중 단추 몫 (GameConfig.goldIncomeShare)</summary>
        public static double GoldShare { get; private set; } = DefaultShare;

        /// <summary>기준 양 B 중 실타래 몫 (GameConfig.lineageXpIncomeShare)</summary>
        public static double LineageXpShare { get; private set; } = DefaultShare;

        /// <summary>GameConfig의 몫을 넣는다 (null이면 기본 몫)</summary>
        public static void Configure(GameConfig config)
        {
            if (config == null) Configure(DefaultShare, DefaultShare);
            else Configure(config.goldIncomeShare, config.lineageXpIncomeShare);
        }

        /// <summary>몫을 넣는다. 음수 · NaN · ∞는 0으로 본다</summary>
        public static void Configure(double goldShare, double lineageXpShare)
        {
            GoldShare = Share(goldShare);
            LineageXpShare = Share(lineageXpShare);
        }

        /// <summary>B의 단추 몫 (실수, 초당 적립 · 옛 오프라인용)</summary>
        public static double Gold(double b) => Base(b) * GoldShare;

        /// <summary>B의 실타래 몫 (실수, 초당 적립 · 옛 오프라인용)</summary>
        public static double LineageXp(double b) => Base(b) * LineageXpShare;

        /// <summary>B의 단추 몫을 정수로 내림 (⌊B × 단추 몫⌋)</summary>
        public static double GoldWhole(double b) => Whole(Gold(b));

        /// <summary>B의 실타래 몫을 정수로 내림 (⌊B × 실타래 몫⌋)</summary>
        public static double LineageXpWhole(double b) => Whole(LineageXp(b));

        /// <summary>
        /// 보상 목록 끝에 단추 줄 → 실타래 줄을 따로 넣는다 (몫마다 내림, 1 미만인 줄은 넣지 않음).
        /// 보상 목록 순서 = 단추 → 실타래 → 별빛 사탕 → 재료이므로 부르는 쪽은 다른 줄보다 먼저 부른다
        /// </summary>
        public static void AddCurrencyLines(List<RewardData> into, double b)
        {
            if (into == null) return;
            double gold = GoldWhole(b);
            double lineageXp = LineageXpWhole(b);
            if (gold >= 1) into.Add(RewardData.Currency(CurrencyType.Gold, gold));
            if (lineageXp >= 1) into.Add(RewardData.Currency(CurrencyType.LineageXp, lineageXp));
        }

        /// <summary>단추 · 실타래 줄 두 개를 새 목록으로 (우편 등)</summary>
        public static RewardData[] CurrencyLines(double b)
        {
            var list = new List<RewardData>(2);
            AddCurrencyLines(list, b);
            return list.ToArray();
        }

        static double Base(double b) => double.IsNaN(b) || b <= 0 ? 0 : b;

        static double Share(double share) => double.IsNaN(share) || double.IsInfinity(share) || share < 0 ? 0 : share;

        static double Whole(double value) => double.IsNaN(value) || value <= 0 ? 0 : Math.Floor(value + 1e-9);
    }

    /// <summary>
    /// 켜 둔 동안 초당 단추 · 실타래 적립 (GameBootstrap이 프레임마다 부른다). 기준 수입 × 프레임 시간을 나눠 모아 두었다가
    /// MinInterval 이상 지나고 어느 한쪽이 1개 이상 모였을 때, 또는 MaxInterval이 지나면 한 번에 지급한다 (Currency.Changed가 매 프레임 울리지 않게).
    /// 저장 직전에도 Commit을 불러 저장에서 빠지지 않게 한다
    /// </summary>
    public sealed class IdleIncomeAccrual
    {
        /// <summary>이 간격 이상 지나고 1개 이상 모였으면 지급</summary>
        public const float MinInterval = 0.2f;
        /// <summary>1개가 다 모이지 않아도 이 간격이 지나면 지급</summary>
        public const float MaxInterval = 1f;

        double _gold;
        double _lineageXp;
        float _timer;

        /// <summary>dt초 동안의 기준 수입을 나눠 모으고, 간격이 차면 지급한다</summary>
        public void Accrue(GrowthSystem growth, CurrencySystem currency, float dt)
        {
            if (growth == null || !(dt > 0)) return;
            double income = growth.IncomePerSecond * dt;
            _gold += IncomeSplit.Gold(income);
            _lineageXp += IncomeSplit.LineageXp(income);
            _timer += dt;
            if (_timer >= MaxInterval || (_timer >= MinInterval && (_gold >= 1d || _lineageXp >= 1d))) Commit(currency);
        }

        /// <summary>모아 둔 단추 · 실타래를 지급한다 (단추 → 실타래 순, 0 이하/NaN은 Add가 무시)</summary>
        public void Commit(CurrencySystem currency)
        {
            _timer = 0f;
            double gold = _gold;
            double lineageXp = _lineageXp;
            _gold = 0;
            _lineageXp = 0;
            if (currency == null) return;
            currency.Add(CurrencyType.Gold, gold);
            currency.Add(CurrencyType.LineageXp, lineageXp);
        }
    }

    /// <summary>
    /// 첫 로드 1회 실타래 지급 (T7). 세이브에 실타래 키도 1회 표식(PlayerData.flags "lineage_xp_start")도 없을 때 한 번:
    /// 키를 0으로 먼저 만들고 표식을 적은 뒤(지급량 0이어도 다시 돌지 않게) 지급량을 더한다.
    /// 표식을 함께 두는 이유: 옛 빌드는 모르는 재화 키를 로드 때 버리므로(TolerantEnumKeyDictionaryConverter) 키만 보면 왕복마다 다시 준다.
    /// flags는 문자열 집합이라 옛 빌드도 그대로 지닌다. 키가 이미 있는 세이브는 표식만 더한다.
    /// 지급량 = 지금 튜토리얼 단계의 목록 위치로 정한다.
    ///   · 6단계(ftue_shop_upgrade)보다 앞 (새 게임 포함) → 0
    ///   · 6단계 또는 7단계(ftue_friend_level) → GameConfig.legacyFtueLineageXp (6단계 보상을 읽지 않는 고정 칸)
    ///   · 7단계를 마쳤거나 졸업 · 건너뜀 → GameConfig.startLineageXp
    ///   · 튜토리얼 판정을 할 수 없으면(메타가 꺼짐 · 튜토리얼 단계 데이터 없음) 진행 신호로: 수선점 강화 · 1-1 너머 도달 · 유실자 레벨업 중 하나라도 있으면
    ///     startLineageXp, 없으면(새 게임) 0 (PlayerData.HasLegacyProgress와 같은 신호)
    /// GameBootstrap이 세이브 로드 · 시스템 생성 뒤, 첫 화면을 그리기 전에 부른다.
    /// </summary>
    public static class LineageXpStartGrant
    {
        /// <summary>튜토리얼 6단계 id (수선점 강화, 단추 선물)</summary>
        public const string ShopUpgradeStepId = "ftue_shop_upgrade";
        /// <summary>튜토리얼 7단계 id (짝짝이 레벨 5)</summary>
        public const string FriendLevelStepId = "ftue_friend_level";
        /// <summary>1회 지급을 했다는 표식 (PlayerData.flags)</summary>
        public const string GrantedFlag = "lineage_xp_start";

        /// <summary>
        /// 실타래 키도 표식도 없으면 지급하고 true (지급량 0이어도 true). 키나 표식이 있으면 지급하지 않고 false (키가 있으면 표식만 더한다)
        /// </summary>
        public static bool TryGrant(PlayerData data, GameConfig config, CurrencySystem currency, TutorialSystem tutorial, MetaDatabase db,
            out double granted)
        {
            granted = 0;
            if (data == null || config == null || currency == null) return false;
            data.currencies ??= new Dictionary<CurrencyType, double>();
            data.flags ??= new HashSet<string>();
            if (data.currencies.ContainsKey(CurrencyType.LineageXp))
            {
                data.flags.Add(GrantedFlag);
                return false;
            }
            if (data.flags.Contains(GrantedFlag)) return false;
            data.currencies[CurrencyType.LineageXp] = 0;
            data.flags.Add(GrantedFlag);
            granted = AmountFor(data, config, tutorial, db);
            currency.Add(CurrencyType.LineageXp, granted);
            return true;
        }

        /// <summary>지금 튜토리얼 위치(없으면 진행 신호)에 맞는 지급량 (지급하지 않음)</summary>
        public static double AmountFor(PlayerData data, GameConfig config, TutorialSystem tutorial, MetaDatabase db)
        {
            if (config == null) return 0;
            double start = Math.Max(0, config.startLineageXp);
            if (tutorial == null || db == null || tutorial.StepCount == 0) return HasProgress(data) ? start : 0;
            if (!tutorial.IsActive) return start;
            int shop = db.IndexOfFtueStep(ShopUpgradeStepId);
            if (shop < 0) return 0;
            int friend = db.IndexOfFtueStep(FriendLevelStepId);
            int index = tutorial.StepIndex;
            if (index < shop) return 0;
            if (index == shop || index == friend) return Math.Max(0, config.legacyFtueLineageXp);
            return friend >= 0 && index > friend ? start : 0;
        }

        /// <summary>진행한 세이브인지 (PlayerData.HasLegacyProgress와 같은 신호: 수선점 강화 / 1-1 너머 도달 / 유실자 레벨업)</summary>
        static bool HasProgress(PlayerData data)
        {
            if (data == null) return false;
            if (data.level > 1) return true;
            if (data.battle != null && data.battle.highestStage > 1) return true;
            if (data.lineages == null) return false;
            foreach (var pair in data.lineages)
                if (pair.Value != null && pair.Value.level > 1) return true;
            return false;
        }
    }
}
