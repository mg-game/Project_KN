using System;
using System.Collections.Generic;
using NUnit.Framework;
using IdleGame.UI;

namespace IdleGame.Tests
{
    /// <summary>
    /// 실타래 분리 (결정 2 안 D, 10_impl_spec_D §5 새 시험 1 ~ 6): 단추가 나오는 모든 곳에서 기준 양 B를 단추 ⌊B × 0.5⌋ · 실타래 ⌊B × 0.5⌋로 한 번만 나눈다.
    /// 유실자 레벨업은 실타래만 · 수선점은 단추만, 비용 50 / 12, 첫 로드 1회 지급(T7 0 / 250 / 2,000), 강화 탭 빛(T5) · 모두 최대(T6) · 강해지기 대기 초(T3).
    /// 실타래로 직접 적은 보상은 나누지 않는다 (튜토리얼 6단계 실타래 125).
    /// </summary>
    public class LineageXpSplitTests : MetaSystemsTestBase
    {
        double LineageXp => Currency.Get(CurrencyType.LineageXp);

        static double Half(double b) => Math.Floor(b * 0.5 + 1e-9);

        static double AmountIn(List<RewardData> rewards, CurrencyType currency)
        {
            double total = 0;
            foreach (var reward in rewards)
                if (reward.type == RewardType.Currency && reward.currency == currency) total += reward.amount;
            return total;
        }

        // ───────────── 1. 나누기 (지급하는 모든 곳) ─────────────

        [Test]
        public void StageClear_GivesHalfButtons_HalfLineageXp_InOrder()
        {
            for (int stage = 1; stage <= 12; stage++)
            {
                double b = Math.Floor(Math.Floor(BattleCfg.stageGoldBase * Math.Pow(BattleCfg.stageGoldGrowth, stage - 1))
                                      * (1.0 + Battle.GetGoldBonusPercent(stage) / 100.0));
                var rewards = Battle.GetClearRewards(stage);
                Assert.AreEqual(Half(b), AmountIn(rewards, CurrencyType.Gold), 1e-9, $"스테이지 {stage} 단추 = ⌊B/2⌋");
                Assert.AreEqual(Half(b), AmountIn(rewards, CurrencyType.LineageXp), 1e-9, $"스테이지 {stage} 실타래 = ⌊B/2⌋");
                Assert.AreEqual(CurrencyType.Gold, rewards[0].currency, "순서 = 단추");
                Assert.AreEqual(CurrencyType.LineageXp, rewards[1].currency, "→ 실타래");
                Assert.AreEqual(CurrencyType.EventToken, rewards[2].currency, "→ 별빛 사탕");
            }
        }

        [Test]
        public void Dungeon_GivesHalfButtons_HalfLineageXp()
        {
            foreach (int floor in new[] { 1, 4, 9 })
            {
                double b = Math.Floor(BattleCfg.dungeonGoldBase * Math.Pow(BattleCfg.dungeonGoldGrowth, floor - 1));
                var rewards = Dungeon.GetRewards(floor);
                Assert.AreEqual(Half(b), AmountIn(rewards, CurrencyType.Gold), 1e-9, $"{floor}층 단추");
                Assert.AreEqual(Half(b), AmountIn(rewards, CurrencyType.LineageXp), 1e-9, $"{floor}층 실타래");
                Assert.AreEqual(CurrencyType.Gold, rewards[0].currency);
                Assert.AreEqual(CurrencyType.LineageXp, rewards[1].currency);
            }
        }

        [Test]
        public void Expedition_GivesHalfButtons_HalfLineageXp()
        {
            Characters.Grant("sock");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }));
            Clock.Advance(TimeSpan.FromMinutes(10));
            var result = Expedition.Claim("exp_1_1");

            Assert.AreEqual(150, AmountIn(result.Rewards, CurrencyType.Gold), 1e-9, "구역 단추 300 = B → 단추 150");
            Assert.AreEqual(150, AmountIn(result.Rewards, CurrencyType.LineageXp), 1e-9, "→ 실타래 150");
            Assert.AreEqual(150, Gold, 1e-9);
            Assert.AreEqual(150, LineageXp, 1e-9);
        }

        [Test]
        public void MetaFixedButtons_AreSplit_DirectLineageXpIsNot()
        {
            // 튜토리얼 6단계 데이터 = 단추 250 + 실타래 125: 단추만 나뉘고 실타래로 적은 보상은 그대로 → 단추 125 · 실타래 250
            var granted = MetaRewards.Grant(new[] { MetaReward.Gold(250), MetaReward.Currency(CurrencyType.LineageXp, 125), MetaReward.Gem(5) });
            Assert.AreEqual(125, Gold, 1e-9, "단추 250 → 단추 125");
            Assert.AreEqual(250, LineageXp, 1e-9, "실타래 125 (나뉜 몫) + 125 (직접) = 250, 7단계 짝짝이 Lv.1 → 5 비용 230을 덮는다");
            Assert.AreEqual(250, AmountOf(granted, MetaRewardType.Currency, CurrencyType.LineageXp), "같은 재화는 한 줄로 합친다");

            MetaRewards.Grant(new[] { MetaReward.Gold(101) });
            Assert.AreEqual(125 + 50, Gold, 1e-9, "홀수는 몫마다 내림");
            Assert.AreEqual(250 + 50, LineageXp, 1e-9);
        }

        [Test]
        public void Preview_ShowsTwoChips_SameAsGrant()
        {
            // 팀 결정: 미리보기 칩 = 실제 지급. UI 미리보기(MetaUi.Preview)는 ResolveAll을 쓰고, 지급(Grant)과 같은 나누기 함수를 거친다
            SetCleared(6);
            var data = new List<MetaReward>
            {
                MetaReward.Gold(251), MetaReward.Currency(CurrencyType.LineageXp, 125), MetaReward.GoldMinutes(30), MetaReward.Gem(3),
            };
            var preview = new List<MetaReward>();
            MetaRewards.ResolveAll(new[] { MetaReward.Gold(250) }, preview);
            Assert.AreEqual(2, preview.Count, "단추 250 미리보기 = 두 칩");
            Assert.AreEqual(CurrencyType.Gold, preview[0].currency);
            Assert.AreEqual(125, preview[0].amount);
            Assert.AreEqual(CurrencyType.LineageXp, preview[1].currency);
            Assert.AreEqual(125, preview[1].amount);

            MetaRewards.ResolveAll(data, preview);
            var shown = MetaRewardService.Merge(preview);
            var granted = MetaRewards.Grant(data);
            Assert.AreEqual(granted.Count, shown.Count);
            foreach (var currency in new[] { CurrencyType.Gold, CurrencyType.LineageXp, CurrencyType.Gem })
                Assert.AreEqual(AmountOf(granted, MetaRewardType.Currency, currency), AmountOf(shown, MetaRewardType.Currency, currency), $"{currency} 미리보기 = 지급");
            double minutes = Math.Floor(Growth.IncomePerSecond * 60 * 30 * 0.5 + 1e-9);
            Assert.AreEqual(125 + minutes, Gold, 1e-9, "단추 ⌊251/2⌋ + N분치 단추 몫");
            Assert.AreEqual(125 + 125 + minutes, LineageXp, 1e-9, "실타래 ⌊251/2⌋ + 직접 125 + N분치 실타래 몫");
        }

        [Test]
        public void PreviewAll_FixedButtonsTwoChips_MinutesStayAsText_Merged()
        {
            // 팀 결정 (코드 검토 반영): 미리보기는 고정 단추만 두 줄(⌊n/2⌋), N분치는 "단추 · 실타래 N분치" 글 그대로, 같은 재화는 합친다
            var preview = new List<MetaReward>();
            MetaRewards.PreviewAll(new[]
            {
                MetaReward.Gold(251), MetaReward.GoldMinutes(5), MetaReward.Currency(CurrencyType.LineageXp, 125), MetaReward.Gem(3),
            }, preview);

            Assert.AreEqual(4, preview.Count, "단추 · 실타래(합침) · N분치 · 별조각");
            Assert.AreEqual(CurrencyType.Gold, preview[0].currency);
            Assert.AreEqual(125, preview[0].amount, "⌊251/2⌋");
            Assert.AreEqual(CurrencyType.LineageXp, preview[1].currency);
            Assert.AreEqual(125 + 125, preview[1].amount, "나뉜 실타래 125 + 직접 적은 실타래 125 = 한 줄");
            Assert.AreEqual(MetaRewardType.GoldMinutes, preview[2].type, "N분치는 숫자로 바꾸지 않는다");
            Assert.AreEqual(5, preview[2].amount);
            Assert.AreEqual("단추 · 실타래 5분치", MetaRewards.Describe(preview[2]));
            Assert.AreEqual(0, Gold, "지급하지 않음");
        }

        [Test]
        public void FailRewards_SplitTheBaseIncome()
        {
            double clear = Math.Floor(BattleCfg.stageGoldBase * Math.Pow(BattleCfg.stageGoldGrowth, 4));
            double b = Math.Floor(clear * BattleCfg.failGoldRatio * 0.5f);
            var rewards = Battle.GetFailRewards(5, 0.5f);
            Assert.AreEqual(Half(b), AmountIn(rewards, CurrencyType.Gold), 1e-9, "실패 단추 = ⌊B/2⌋ (B = 클리어 식 × 실패 비율 × 진행률)");
            Assert.AreEqual(Half(b), AmountIn(rewards, CurrencyType.LineageXp), 1e-9, "실패 실타래 = ⌊B/2⌋");
            Assert.IsEmpty(Battle.GetFailRewards(5, 0f), "처치 0이면 보상 없음");
        }

        [Test]
        public void RewardFlyer_Reserve_CountsAsPending_UntilReleased()
        {
            // 1-1 개방 뒤로 미룬 실타래는 날아오는 양으로 먼저 잡아 꼬리표 숫자가 오르내리지 않게 한다 (T10)
            double before = RewardFlyer.PendingOf(CurrencyType.LineageXp);
            RewardFlyer.Reserve(CurrencyType.LineageXp, 30);
            try
            {
                Assert.AreEqual(before + 30, RewardFlyer.PendingOf(CurrencyType.LineageXp), 1e-9);
                Assert.AreEqual(0, RewardFlyer.PendingOf(CurrencyType.Gold), 1e-9, "다른 재화는 그대로");
            }
            finally
            {
                RewardFlyer.ReleaseReserved(CurrencyType.LineageXp, 30);
            }
            Assert.AreEqual(before, RewardFlyer.PendingOf(CurrencyType.LineageXp), 1e-9);
            RewardFlyer.ReleaseReserved(CurrencyType.LineageXp, 99);
            Assert.AreEqual(before, RewardFlyer.PendingOf(CurrencyType.LineageXp), 1e-9, "더 풀어도 0 아래로 안 간다");
        }

        [Test]
        public void IdleChest_OfflineLines_SplitTheBaseIncome_AndDouble()
        {
            AddFeature(FeatureId.IdleChest, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.QuickIdle, MetaCondition.StageCleared(1, 5));
            MetaDb.idleChest = new IdleChestConfig
            {
                baseCapHours = 8f, stageGrowth = 1.0025, offlineGoldRate = 0.5f, minClaimSeconds = 60, quickIdleHours = 2f,
                quickIdleGemCosts = new[] { 0, 50, 100 }, maxQuickIdleTickets = 20, freeDoublePerDay = 1, doubleExcludesGem = true,
            };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            DailyClock.Refresh();
            SetCleared(5);
            Features.Evaluate();

            IdleChest.AddOfflineTime(3600);
            double b = Growth.IncomePerSecond * 3600 * 0.5;
            Assert.AreEqual(Half(b), IdleChest.PendingGold, 1e-9, "단추 줄 = ⌊B × 0.5⌋ (B = 기준 초당 수입 × 시간 × 오프라인 비율)");
            Assert.AreEqual(Half(b), IdleChest.PendingLineageXp, 1e-9, "실타래 줄 = 같은 시간 · 같은 방식");

            var result = IdleChest.Claim(doubled: true);
            Assert.IsTrue(result.Doubled);
            Assert.AreEqual(2 * Half(b), Gold, 1e-9, "두 배 받기는 이미 나뉜 줄에 곱한다 (다시 나누지 않음)");
            Assert.AreEqual(2 * Half(b), LineageXp, 1e-9);

            var quick = IdleChest.TryQuickIdle(useTicket: false);
            Assert.IsNotNull(quick, "빠른 수선 (T9)");
            double quickB = Growth.IncomePerSecond * 2 * 3600 * 0.5;
            Assert.AreEqual(Half(quickB), AmountOf(quick.Rewards, MetaRewardType.Currency, CurrencyType.LineageXp), 1.0, "빠른 수선에도 실타래 줄");
        }

        [Test]
        public void OldOfflineReward_SplitsTheBaseIncome()
        {
            Offline.Calculate(NowUnix - 3600);
            double b = Growth.IncomePerSecond * 3600 * Config.offlineRewardRate;
            Assert.AreEqual(b * 0.5, Offline.PendingGold, 1e-6);
            Assert.AreEqual(b * 0.5, Offline.PendingLineageXp, 1e-6);
        }

        [Test]
        public void IncomeSplit_CurrencyLines_SkipsZeroShares()
        {
            var lines = IncomeSplit.CurrencyLines(500);
            Assert.AreEqual(2, lines.Length, "환영 우편 단추 500 → 단추 250 · 실타래 250");
            Assert.AreEqual(250, lines[0].amount);
            Assert.AreEqual(CurrencyType.LineageXp, lines[1].currency);
            Assert.AreEqual(250, lines[1].amount);
            Assert.AreEqual(0, IncomeSplit.CurrencyLines(1).Length, "몫이 1 미만이면 줄 없음");
        }

        // ───────────── 2. 중복 나누기 없음 ─────────────

        [Test]
        public void GoldMinutes_NoDoubleSplit()
        {
            SetCleared(7);
            double income = Growth.IncomePerSecond;
            Assert.AreEqual(income * 0.5, Growth.GoldPerSecond, 1e-12, "초당 단추 = 기준 × 0.5");

            MetaRewards.Grant(new[] { MetaReward.GoldMinutes(30) });

            Assert.AreEqual(Math.Floor(income * 60 * 30 * 0.5 + 1e-9), Gold, 1e-9, "단추 = ⌊기준 수입 × 60n × 0.5⌋");
            Assert.AreEqual(Math.Floor(income * 60 * 30 * 0.5 + 1e-9), LineageXp, 1e-9, "실타래도 같은 몫");
            Assert.Greater(Gold, Math.Floor(Growth.GoldPerSecond * 60 * 30 * 0.5) + 1, "이미 나뉜 초당 단추로 B를 만들어 다시 나누면(4분의 1) 이보다 작다");
        }

        // ───────────── 3. 초당 적립 ─────────────

        [Test]
        public void IdleAccrual_OneSecond_SplitsBaseIncome()
        {
            SetCleared(9);
            double income = Growth.IncomePerSecond;
            var accrual = new IdleIncomeAccrual();

            accrual.Accrue(Growth, Currency, 1f);

            Assert.AreEqual(income * 0.5, Gold, 1e-9, "1초에 단추 = 기준 × 0.5");
            Assert.AreEqual(income * 0.5, LineageXp, 1e-9, "1초에 실타래 = 기준 × 0.5");
            Assert.AreEqual(income * 0.5, Growth.LineageXpPerSecond, 1e-12);
        }

        [Test]
        public void IdleAccrual_WaitsForInterval_ThenCommitsBoth()
        {
            var accrual = new IdleIncomeAccrual();
            accrual.Accrue(Growth, Currency, 0.1f);
            Assert.AreEqual(0, Gold, 1e-12, "0.2초 전에는 모아만 둔다");
            accrual.Commit(Currency);
            Assert.AreEqual(Growth.IncomePerSecond * 0.1 * 0.5, Gold, 1e-9, "저장 직전 Commit");
            Assert.AreEqual(Growth.IncomePerSecond * 0.1 * 0.5, LineageXp, 1e-9);
        }

        // ───────────── 4. 소비처 · 비용 ─────────────

        [Test]
        public void LineageUsesOnlyLineageXp_ShopUsesOnlyButtons_DefaultCosts()
        {
            Assert.AreEqual(50, Config.characterLevelUpBaseCost, "유실자 기본 비용 50 (그대로)");
            Assert.AreEqual(12, Config.levelUpBaseCost, "수선점 기본 비용 12");
            Assert.AreEqual(CurrencyType.LineageXp, Characters.LevelUpCurrency);
            Assert.AreEqual(CurrencyType.Gold, Growth.LevelUpCurrency);

            Characters.Grant("sock");
            Assert.AreEqual(50, Characters.GetLevelUpCost("sock"), 1e-9);
            Assert.AreEqual(50 + 55 + 60 + 65, Characters.GetLevelUpCost("sock", 4), 1e-9, "짝짝이 Lv.1 → 5 = 230 (튜토리얼 선물 실타래 250이 덮는다)");

            Currency.Add(CurrencyType.Gold, 1e6);
            Assert.IsFalse(Characters.TryLevelUp("sock"), "단추로는 유실자 레벨업 못 함");
            Currency.Add(CurrencyType.LineageXp, 50);
            Assert.IsTrue(Characters.TryLevelUp("sock"));
            Assert.AreEqual(0, LineageXp, 1e-9);
            Assert.AreEqual(1e6, Gold, 1e-9, "단추는 그대로");

            Currency.TrySpend(CurrencyType.Gold, 1e6);
            Currency.Add(CurrencyType.LineageXp, 1e6);
            Assert.IsFalse(Growth.TryLevelUp(), "실타래로는 수선점 강화 못 함");
            Currency.Add(CurrencyType.Gold, 12);
            Assert.IsTrue(Growth.TryLevelUp());
            Assert.AreEqual(0, Gold, 1e-9);
            Assert.AreEqual(1e6, LineageXp, 1e-9, "실타래는 그대로");
        }

        // ───────────── 5. 첫 로드 1회 지급 (T7) ─────────────

        void SetupFtue(int step, bool done = false)
        {
            MetaDb.ftueSteps = new List<FtueStepDef>
            {
                new FtueStepDef { id = "ftue_intro" },
                new FtueStepDef { id = "ftue_first_boss" },
                new FtueStepDef { id = LineageXpStartGrant.ShopUpgradeStepId },
                new FtueStepDef { id = LineageXpStartGrant.FriendLevelStepId },
                new FtueStepDef { id = "ftue_auto_battle" },
            };
            Data.meta.ftue.step = step;
            Data.meta.ftue.done = done;
            Rebuild();
        }

        double GrantOnce()
        {
            Assert.IsTrue(LineageXpStartGrant.TryGrant(Data, Config, Currency, Tutorial, MetaDb, out double granted));
            Assert.IsTrue(Data.currencies.ContainsKey(CurrencyType.LineageXp), "키를 먼저 만든다");
            Assert.IsFalse(LineageXpStartGrant.TryGrant(Data, Config, Currency, Tutorial, MetaDb, out double again), "두 번 주지 않는다");
            Assert.AreEqual(0, again);
            return granted;
        }

        [Test]
        public void StartGrant_NewGameBeforeStep6_IsZero_KeyStillCreated()
        {
            SetupFtue(step: 0);
            Assert.AreEqual(0, GrantOnce());
            Assert.AreEqual(0, LineageXp);
            SetupFtue(step: 1);
            Assert.IsFalse(LineageXpStartGrant.TryGrant(Data, Config, Currency, Tutorial, MetaDb, out _), "지급량 0이어도 키가 있으니 다시 돌지 않는다");
        }

        [TestCase(2)]
        [TestCase(3)]
        public void StartGrant_DuringStep6Or7_IsLegacyFtueAmount(int step)
        {
            SetupFtue(step);
            Assert.AreEqual(250, GrantOnce());
            Assert.AreEqual(250, LineageXp);
        }

        [Test]
        public void StartGrant_FlagWithoutKey_DoesNotGrantAgain()
        {
            // 옛 빌드가 모르는 실타래 키를 버리고 저장해도(왕복) 1회 표식이 남아 다시 주지 않는다
            SetupFtue(step: 4, done: true);
            Assert.AreEqual(2000, GrantOnce());
            Assert.IsTrue(Data.flags.Contains(LineageXpStartGrant.GrantedFlag), "1회 표식");
            Data.currencies.Remove(CurrencyType.LineageXp);
            Assert.IsFalse(LineageXpStartGrant.TryGrant(Data, Config, Currency, Tutorial, MetaDb, out double again), "키 없음 + 표식 있음 = 지급 안 함");
            Assert.AreEqual(0, again);
            Assert.AreEqual(0, LineageXp);
        }

        [Test]
        public void StartGrant_KeyWithoutFlag_AddsFlagOnly()
        {
            SetupFtue(step: 4, done: true);
            Data.currencies[CurrencyType.LineageXp] = 7;
            Assert.IsFalse(LineageXpStartGrant.TryGrant(Data, Config, Currency, Tutorial, MetaDb, out _));
            Assert.IsTrue(Data.flags.Contains(LineageXpStartGrant.GrantedFlag), "키가 있는 세이브도 표식을 남긴다");
            Assert.AreEqual(7, LineageXp, "지급 없음");
        }

        [Test]
        public void StartGrant_NoTutorial_UsesProgressSignal()
        {
            // 메타가 꺼져 튜토리얼 시스템이 없으면: 새 게임 0, 진행한 세이브(수선점 강화 · 1-1 너머 · 유실자 레벨업)는 2,000
            Assert.IsTrue(LineageXpStartGrant.TryGrant(Data, Config, Currency, null, null, out double fresh));
            Assert.AreEqual(0, fresh, "새 게임");

            Data.currencies.Remove(CurrencyType.LineageXp);
            Data.flags.Remove(LineageXpStartGrant.GrantedFlag);
            Data.battle.highestStage = 5;
            Assert.IsTrue(LineageXpStartGrant.TryGrant(Data, Config, Currency, null, null, out double progressed));
            Assert.AreEqual(2000, progressed, "1-1 너머 도달한 세이브");
        }

        [Test]
        public void StartGrant_AfterStep7OrDone_IsStartAmount()
        {
            SetupFtue(step: 4);
            Assert.AreEqual(2000, GrantOnce(), "7단계를 마쳤으면 2,000");

            Data.currencies.Remove(CurrencyType.LineageXp);
            Data.flags.Remove(LineageXpStartGrant.GrantedFlag);
            SetupFtue(step: 4, done: true);
            Assert.AreEqual(2000, GrantOnce(), "졸업 · 건너뜀도 2,000");
        }

        // ───────────── 6. 강화 탭 빛 (T5) · 모두 최대 (T6) · 강해지기 대기 초 (T3) ─────────────

        [Test]
        public void GrowthGlow_LineageCondition_FollowsT5()
        {
            AddFeature(FeatureId.CharacterLevelUp, MetaCondition.StageCleared(1, 1));
            Rebuild();
            MetaSession.Start();
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Currency.Add(CurrencyType.LineageXp, 1e6);

            Assert.IsFalse(MainTabController.LineageGlowCondition(Characters, Formation, Growth, Currency), "1-1 전 (잠김) = 거짓");

            SetCleared(1);
            Features.Evaluate();
            Assert.IsTrue(Characters.IsLevelUpUnlocked);
            Assert.IsTrue(MainTabController.LineageGlowCondition(Characters, Formation, Growth, Currency), "잔액 ≥ 가장 싼 1레벨 · 수입 × 300초");

            double rate = Growth.LineageXpPerSecond;
            double cost = Characters.GetLevelUpCost("sock");
            double needed = Math.Max(cost, rate * MainTabController.LineageGlowIncomeSeconds);
            Currency.TrySpend(CurrencyType.LineageXp, LineageXp);
            Currency.Add(CurrencyType.LineageXp, needed - 1);
            Assert.IsFalse(MainTabController.LineageGlowCondition(Characters, Formation, Growth, Currency), "조건보다 1 모자람");
            Currency.TrySpend(CurrencyType.LineageXp, LineageXp);
            Currency.Add(CurrencyType.LineageXp, needed);
            Assert.IsTrue(MainTabController.LineageGlowCondition(Characters, Formation, Growth, Currency), "조건과 같으면 켜짐");

            Formation.Remove(CharacterRole.Guardian, 0);
            Assert.IsFalse(MainTabController.LineageGlowCondition(Characters, Formation, Growth, Currency), "편성 비어 있음 = 거짓 (편성 밖 보유 유실자는 보지 않는다)");
        }

        [Test]
        public void AllMax_T6_GlowOff_StrengthenRowGone_StillAccrues()
        {
            Config.characterMaxLevel = 2;
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Assert.IsFalse(Characters.AllOwnedMaxLevel);
            Currency.Add(CurrencyType.LineageXp, 1e6);
            Assert.IsTrue(Characters.TryLevelUp("sock"));

            Assert.IsTrue(Characters.AllOwnedMaxLevel, "보유 전원 최대 → '모두 최대'");
            Assert.IsFalse(MainTabController.LineageGlowCondition(Characters, Formation, Growth, Currency), "다음 레벨이 있는 편성 유실자 없음 = 빛 조건 거짓");
            var rows = new List<StrengthenRow>();
            Strengthen.GetRows(rows, 10);
            Assert.IsNull(rows.Find(r => r.Kind == StrengthenKind.LineageLevel), "강해지기 유실자 줄은 저절로 빠진다");

            double before = LineageXp;
            new IdleIncomeAccrual().Accrue(Growth, Currency, 1f);
            Assert.Greater(LineageXp, before, "적립은 그대로 (T6 ①)");

            Characters.Grant("candle");
            Assert.IsFalse(Characters.AllOwnedMaxLevel, "새 유실자가 합류하면 그 순간 거짓");
        }

        [Test]
        public void Strengthen_EachRowWaitsOnItsOwnCurrency()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            var rows = new List<StrengthenRow>();

            Strengthen.GetRows(rows, 10);
            var lineage = rows.Find(r => r.Kind == StrengthenKind.LineageLevel);
            var shop = rows.Find(r => r.Kind == StrengthenKind.ShopUpgrade);
            Assert.IsNotNull(lineage);
            Assert.IsNotNull(shop);
            Assert.AreEqual(Characters.GetLevelUpCost("sock") / Growth.LineageXpPerSecond, lineage.CostSeconds, 1e-6,
                "유실자 줄 대기 초 = 모자란 실타래 ÷ 실타래 수입 속도");
            Assert.AreEqual(Growth.GetLevelUpCost(Growth.Level, shop.Amount) / Math.Max(1.0, Growth.GoldPerSecond), shop.CostSeconds, 1e-6,
                "수선점 줄 대기 초 = 모자란 단추 ÷ 초당 단추(하한 1)");

            Currency.Add(CurrencyType.LineageXp, 1e9);
            Strengthen.GetRows(rows, 10);
            Assert.AreEqual(0, rows.Find(r => r.Kind == StrengthenKind.LineageLevel).CostSeconds, 1e-9, "실타래가 넉넉하면 유실자 줄 0초");
            Assert.Greater(rows.Find(r => r.Kind == StrengthenKind.ShopUpgrade).CostSeconds, 0, "두 재화가 다르면 수선점 줄은 단추를 읽는다");
        }

        // ───────────── 누락 방지 ─────────────

        [Test]
        public void EveryCurrency_HasNameColorSourcesAndLogKey()
        {
            foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            {
                Assert.AreNotEqual(type.ToString(), RewardService.CurrencyName(type), $"{type} 이름");
                Assert.AreNotEqual(UnityEngine.Color.white, UITheme.CurrencyColor(type), $"{type} 색");
                Assert.IsNotEmpty(CurrencySources.Body(type), $"{type} 쓰는 곳");
                Assert.IsNotEmpty(CurrencySources.SourceLine(type), $"{type} 얻는 곳");
                Assert.IsNotEmpty(CurrencySources.For(type), $"{type} 얻는 곳 줄");
                Assert.AreNotEqual("other", PlayLogRecorder.CurrencyKey(type), $"{type} 기록 키");
            }
            Assert.AreEqual("실타래", RewardService.CurrencyName(CurrencyType.LineageXp));
            Assert.AreEqual("lxp", PlayLogRecorder.CurrencyKey(CurrencyType.LineageXp));
            Assert.AreEqual(3, (int)CurrencyType.LineageXp, "열거형 맨 끝 (정수 저장 호환)");
        }
    }
}
