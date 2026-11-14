using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 진행 시뮬레이션 (10_impl_spec_D §5-7, bal 04_bal_draft_v2.md §7 · 02_bal_draft.md §5를 줄인 판).
    /// 실제 시스템(GrowthSystem · CharacterSystem · BattleSystem.GetClearRewards · MetaRewardService · IdleChestSystem · IdleIncomeAccrual)으로
    /// 같은 일정의 수입을 넣고 "더 싼 것 먼저" 소비를 1초마다 돌려, 분리 전 설정(공유 단추, 수선점 · 유실자 비용 10 · 50, 나누기 없음,
    /// 튜토리얼 6단계 단추 250)과 안 D 설정(실타래 분리, 지금 GameConfig 기본 비용 = 수선점 12 · 유실자 50, 단추 · 실타래 반씩,
    /// 6단계 단추 250 + 실타래 125)을 같은 시점에 비교한다.
    /// 줄인 것 (그렇다고 적는다): 스테이지 진행 판정(전투력 ≥ 0.9 × 권장)은 넣지 않았다. 두 갈래가 같은 고정 일정으로 클리어하므로
    /// 스테이지 차는 늘 0이고 레벨만 비교한다. 원정 · 던전 · 출석 · 일지는 같은 시각의 N분치 · 고정 단추로 대신한다 (둘 다 안 D 규칙으로 나뉜다).
    /// 허용 범위 (B4): 시작 유실자 평균 레벨 ±10% · 수선점 레벨 −15% ~ +30%.
    /// </summary>
    public class ProgressionPacingTests : MetaSystemsTestBase
    {
        /// <summary>접속 패턴: A = 내내 켜 둠, B = 30분 켬 / 90분 닫음 반복</summary>
        public enum Pattern
        {
            A = 0,
            B = 1,
        }

        public struct Snapshot
        {
            public int Seconds;
            public int Stage;
            public int ShopLevel;
            public double LineageAverage;
            public double Gold;
            public double LineageXp;

            public override string ToString() =>
                $"t={Seconds}s stage={Stage} shop={ShopLevel} lineageAvg={LineageAverage:0.00} gold={Gold:0} lxp={LineageXp:0}";
        }

        static readonly int[] Checkpoints = { 600, 3600, 4 * 3600 };
        const double LineageTolerance = 0.10;
        const double ShopLow = -0.15;
        const double ShopHigh = 0.30;
        static readonly string[] Starters = { "sock", "candle", "button", "thimble" };

        CharacterDatabase _originalDb;
        /// <summary>안 D 갈래 값 = 지금 GameConfig 기본값 (코드 기본값이 바뀌면 시뮬레이션도 따라간다)</summary>
        double _shopCost, _lineageCost, _goldShare, _lineageShare;

        [SetUp]
        public void UseFourStarters()
        {
            _shopCost = Config.levelUpBaseCost;
            _lineageCost = Config.characterLevelUpBaseCost;
            _goldShare = Config.goldIncomeShare;
            _lineageShare = Config.lineageXpIncomeShare;
            _originalDb = CharDb;
            CharDb = TestContent.CreateCharacters();
            CharDb.lineages.Add(new LineageDef
            {
                id = "thimble", name = "골무", grade = ItemGrade.Common, baseFormId = "thimble_base",
                forms = new List<CharacterFormDef> { TestContent.Form("thimble_base", null, 0, CombatStyle.Defense) },
            });
        }

        [TearDown]
        public void RestoreDatabase()
        {
            IncomeSplit.Configure(IncomeSplit.DefaultShare, IncomeSplit.DefaultShare);
            if (CharDb != _originalDb)
            {
                UnityEngine.Object.DestroyImmediate(CharDb);
                CharDb = _originalDb;
            }
        }

        [TestCase(Pattern.A)]
        [TestCase(Pattern.B)]
        public void SplitD_StaysWithinPacingRange(Pattern pattern)
        {
            var before = Run(split: false, pattern);
            var after = Run(split: true, pattern);
            var report = new StringBuilder();
            var failures = new List<string>();
            for (int i = 0; i < Checkpoints.Length; i++)
            {
                var b = before[i];
                var a = after[i];
                double lineageDiff = a.LineageAverage / b.LineageAverage - 1.0;
                double shopDiff = (double)a.ShopLevel / b.ShopLevel - 1.0;
                report.AppendLine($"[{pattern}] {Checkpoints[i]}s 분리 전 {b} | 안 D {a} | 유실자 {lineageDiff:+0.0%;-0.0%} 수선점 {shopDiff:+0.0%;-0.0%}");
                if (Math.Abs(lineageDiff) > LineageTolerance + 1e-9) failures.Add($"{Checkpoints[i]}s 유실자 평균 레벨 {lineageDiff:+0.0%;-0.0%} (±10% 밖)");
                if (shopDiff < ShopLow - 1e-9 || shopDiff > ShopHigh + 1e-9) failures.Add($"{Checkpoints[i]}s 수선점 레벨 {shopDiff:+0.0%;-0.0%} (−15 ~ +30% 밖)");
            }
            TestContext.WriteLine(report.ToString());
            Debug.Log("[PacingSim]\n" + report);
            Assert.IsEmpty(failures, string.Join("\n", failures) + "\n" + report);
        }

        [Test]
        public void Simulation_IsDeterministic()
        {
            var first = Run(split: true, Pattern.B);
            var second = Run(split: true, Pattern.B);
            for (int i = 0; i < first.Length; i++) Assert.AreEqual(first[i].ToString(), second[i].ToString(), $"체크포인트 {i}");
        }

        // ───────────────────────────── 시뮬레이터 ─────────────────────────────

        /// <summary>
        /// 한 갈래를 돌려 체크포인트마다 상태를 돌려준다. split = 안 D(실타래 분리), 아니면 분리 전.
        /// 시스템은 갈래마다 새로 만든다 (CreateSystems). 시간 1초 간격, 닫은 동안은 한 번에 건너뛰고 바구니 닫은 시간에 더한다.
        /// </summary>
        Snapshot[] Run(bool split, Pattern pattern)
        {
            if (split)
            {
                Config.goldIncomeShare = _goldShare;
                Config.lineageXpIncomeShare = _lineageShare;
                Config.lineageLevelUpCurrency = CurrencyType.LineageXp;
                Config.levelUpBaseCost = _shopCost;
                Config.characterLevelUpBaseCost = _lineageCost;
            }
            else
            {
                Config.goldIncomeShare = 1;
                Config.lineageXpIncomeShare = 0;
                Config.lineageLevelUpCurrency = CurrencyType.Gold;
                Config.levelUpBaseCost = 10;
                Config.characterLevelUpBaseCost = 50;
            }
            Config.characterLevelUpCostGrowth = 1.09;
            Config.characterMaxLevel = 100;
            Config.levelUpCostGrowth = 1.12;
            Config.goldPerSecondGrowth = 1.06;
            Config.baseGoldPerSecond = 1;

            MetaDb.features.Clear();
            AddFeature(FeatureId.IdleChest, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.QuickIdle, MetaCondition.StageCleared(1, 5));
            MetaDb.idleChest = new IdleChestConfig
            {
                baseCapHours = 8f, stageGrowth = 1.0025, offlineGoldRate = 0.5f, minClaimSeconds = 60, quickIdleHours = 2f,
                quickIdleGemCosts = new[] { 0, 50, 100 }, maxQuickIdleTickets = 20, freeDoublePerDay = 1, doubleExcludesGem = true,
            };
            MetaDb.ClearCache();
            SetUtc(2026, 11, 4, 11, 0);
            CreateSystems(new System.Random(7));
            DailyClock.Refresh();
            foreach (var id in Starters) Characters.Grant(id);
            var accrual = new IdleIncomeAccrual();

            // 환영 우편 (t = 0): 단추 500 → 안 D 규칙대로 (분리 전 몫 1 · 0이면 단추 500 그대로)
            var welcome = new List<RewardData>();
            IncomeSplit.AddCurrencyLines(welcome, 500);
            Rewards.Grant(welcome);

            var snapshots = new Snapshot[Checkpoints.Length];
            int checkpoint = 0;
            int onSeconds = 0;
            long offSeconds = 0;
            int nextClear = ClearSeconds(1);
            int stage = 1;
            bool ftueDone = false;
            int ftueAt = -1;
            for (int t = 1; checkpoint < Checkpoints.Length; t++)
            {
                bool on = IsOn(pattern, t);
                if (!on)
                {
                    offSeconds++;
                }
                else
                {
                    if (offSeconds > 0)
                    {
                        // 다시 켬: 닫아 둔 시간을 바구니에 더하고 받는다
                        IdleChest.AddOfflineTime(offSeconds);
                        offSeconds = 0;
                        if (IdleChest.CanClaim) IdleChest.Claim();
                    }
                    onSeconds++;
                    accrual.Accrue(Growth, Currency, 1f);
                    if (onSeconds >= nextClear)
                    {
                        ClearStage(stage, ref ftueAt, onSeconds);
                        stage = Math.Min(stage + 1, MaxStage);
                        nextClear = onSeconds + ClearSeconds(stage);
                    }
                    GrantScheduled(onSeconds);
                    if (!ftueDone && ftueAt >= 0 && onSeconds >= ftueAt + 10) ftueDone = RunFtue(split);
                    if (ftueDone) Spend(split);
                }
                if (t == Checkpoints[checkpoint])
                {
                    accrual.Commit(Currency);
                    snapshots[checkpoint++] = Take(t);
                }
            }
            return snapshots;
        }

        const int MaxStage = 60;

        /// <summary>같은 고정 일정: 1장(1~10) 40초, 2 ~ 3장 60초, 그 뒤 90초 (스테이지 진행 판정 없음, 두 갈래 같음)</summary>
        static int ClearSeconds(int stage) => stage <= 10 ? 40 : stage <= 30 ? 60 : 90;

        static bool IsOn(Pattern pattern, int t)
        {
            if (t <= 0) return true;
            if (pattern == Pattern.A) return true;
            // B: 30분 켬 / 90분 닫음 반복
            return (t - 1) % 7200 < 1800;
        }

        void ClearStage(int stage, ref int ftueAt, int onSeconds)
        {
            Rewards.Grant(Battle.GetClearRewards(stage));
            Data.battle.highestStage = Math.Max(Data.battle.highestStage, stage + 1);
            Features.Evaluate();
            if (stage == 1) ftueAt = onSeconds;
            // 튜토리얼 9단계 바구니 미리 채움 2시간 (1-2 클리어) · 1-5 빠른 수선 무료 1회
            if (stage == 2)
            {
                IdleChest.Prefill(2f);
                if (IdleChest.CanClaim) IdleChest.Claim();
            }
            if (stage == 5) IdleChest.TryQuickIdle(useTicket: false);
        }

        /// <summary>
        /// 같은 시각의 메타 보상 (켜 둔 초 기준): 출석 1일 1시간치(60초), 일지 상자 30분치(7분), 가이드 퀘스트 고정 단추 200 · 300 · 500 · 800 (2 · 3 · 6 · 8분),
        /// 10분 뒤 5분마다 반복 퀘스트 5분치. 모두 MetaRewardService가 안 D 규칙으로 나눈다
        /// </summary>
        void GrantScheduled(int onSeconds)
        {
            switch (onSeconds)
            {
                case 60:
                    MetaRewards.Grant(new[] { MetaReward.GoldMinutes(60) });
                    return;
                case 120:
                    MetaRewards.Grant(new[] { MetaReward.Gold(200) });
                    return;
                case 180:
                    MetaRewards.Grant(new[] { MetaReward.Gold(300) });
                    return;
                case 360:
                    MetaRewards.Grant(new[] { MetaReward.Gold(500) });
                    return;
                case 420:
                    MetaRewards.Grant(new[] { MetaReward.GoldMinutes(30) });
                    return;
                case 480:
                    MetaRewards.Grant(new[] { MetaReward.Gold(800) });
                    return;
            }
            if (onSeconds > 600 && onSeconds % 300 == 0) MetaRewards.Grant(new[] { MetaReward.GoldMinutes(5) });
        }

        /// <summary>
        /// 튜토리얼 6 · 7단계: 수선점 3번 → 6단계 선물 → 첫 유실자 Lv.5. 다 하면 true.
        /// 선물 = 콘텐츠와 같은 값 (분리 전 단추 250, 안 D 단추 250 + 실타래 125 → 단추 125 · 실타래 250)
        /// </summary>
        bool RunFtue(bool split)
        {
            for (int i = 0; i < 3; i++) Growth.TryLevelUp();
            MetaRewards.Grant(split
                ? new[] { MetaReward.Gold(250), MetaReward.Currency(CurrencyType.LineageXp, 125) }
                : new[] { MetaReward.Gold(250) });
            while (Characters.GetLevel(Starters[0]) < 5 && Characters.TryLevelUp(Starters[0])) { }
            return true;
        }

        /// <summary>
        /// 더 싼 것 먼저: 분리 전 = 수선점과 유실자를 단추 하나로 비교해 가장 싼 것을 살 수 있으면 사고, 못 사면 모은다.
        /// 안 D = 단추는 수선점, 실타래는 가장 싼 유실자에 각각 살 수 있는 만큼
        /// </summary>
        void Spend(bool split)
        {
            for (int guard = 0; guard < 10000; guard++)
            {
                string cheapest = CheapestLineage(out double lineageCost);
                double shopCost = Growth.IsMaxLevel ? double.PositiveInfinity : Growth.GetLevelUpCost(Growth.Level);
                if (split)
                {
                    bool bought = false;
                    if (Currency.Has(Growth.LevelUpCurrency, shopCost)) bought |= Growth.TryLevelUp();
                    if (cheapest != null && Currency.Has(Characters.LevelUpCurrency, lineageCost)) bought |= Characters.TryLevelUp(cheapest);
                    if (!bought) return;
                }
                else
                {
                    bool shopFirst = shopCost <= lineageCost;
                    double cost = shopFirst ? shopCost : lineageCost;
                    if (double.IsPositiveInfinity(cost) || !Currency.Has(CurrencyType.Gold, cost)) return;
                    if (!(shopFirst ? Growth.TryLevelUp() : Characters.TryLevelUp(cheapest))) return;
                }
            }
        }

        string CheapestLineage(out double cost)
        {
            string best = null;
            cost = double.PositiveInfinity;
            foreach (var id in Starters)
            {
                if (Characters.IsMaxLevel(id)) continue;
                double next = Characters.GetLevelUpCost(id);
                if (next < cost)
                {
                    cost = next;
                    best = id;
                }
            }
            return best;
        }

        Snapshot Take(int seconds)
        {
            double sum = 0;
            foreach (var id in Starters) sum += Characters.GetLevel(id);
            return new Snapshot
            {
                Seconds = seconds,
                Stage = Data.battle.highestStage - 1,
                ShopLevel = Growth.Level,
                LineageAverage = sum / Starters.Length,
                Gold = Currency.Get(CurrencyType.Gold),
                LineageXp = Currency.Get(CurrencyType.LineageXp),
            };
        }
    }
}
