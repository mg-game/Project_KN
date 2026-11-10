using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 수선 바구니 (IDLE-01/02/03): 줄별 시간당 양(스테이지 복리)·carry, 상한 8h → 2-1 클리어 12h, 단추 줄은 오프라인만,
    /// 받기 초기화, 빠른 수선 0/50/100 + 수선권, 두 배 하루 1번 무료 → 두 배 수선권(별조각 제외), 되돌림 막음, 미리 채우기, 미개방은 쌓이지 않음.
    /// 4차 (SPEC4 §13.4 · §8.2 · §8.3): 별조각 줄 스테이지 배율 천장 ×1.5 (C-01), 수선점 Lv.200 16시간 단계 (0이 아닌 조건이 모두 맞아야),
    /// 다시 만난 날 한 번짜리 상한 (받으면 끝), 밤의 수선점 덤 시간 · 이벤트 전용 줄 시간 훅.
    /// </summary>
    public class IdleChestTests : MetaSystemsTestBase
    {
        /// <summary>콘텐츠와 같은 스테이지 배율 (MetaContent.IdleChest — CR-07 재조정 1.02 → 1.0025)</summary>
        const double StageGrowth = 1.0025;

        static IdleChestLine Line(MetaReward reward, int minStage = 1, bool eventOnly = false, double maxStageMultiplier = 0) =>
            new IdleChestLine { reward = reward, minStage = minStage, eventOnly = eventOnly, maxStageMultiplier = maxStageMultiplier };

        /// <summary>리서치 IDLE-01 표 일부 (테스트 아이템 m_cloth / m_thread)</summary>
        void SetupChest(int cleared = 2, double gemCap = 0)
        {
            AddFeature(FeatureId.IdleChest, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.QuickIdle, MetaCondition.StageCleared(1, 5));
            MetaDb.idleChest = new IdleChestConfig
            {
                baseCapHours = 8f,
                capSteps = new List<IdleCapStep> { new IdleCapStep { stageCleared = 11, capHours = 12f } },
                lines = new List<IdleChestLine>
                {
                    Line(MetaReward.Gem(3), maxStageMultiplier: gemCap),
                    Line(MetaReward.Item("m_cloth", 1.5)),
                    Line(MetaReward.Item("m_thread", 0.15), minStage: 10),
                    Line(MetaReward.Currency(CurrencyType.EventToken, 2), eventOnly: true),
                },
                stageGrowth = StageGrowth,
                offlineGoldRate = 0.5f,
                minClaimSeconds = 60,
                quickIdleHours = 2f,
                quickIdleGemCosts = new[] { 0, 50, 100 },
                maxQuickIdleTickets = 20,
                returnPopupMinSeconds = 600,
                freeDoublePerDay = 1,
                doubleExcludesGem = true,
            };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            DailyClock.Refresh();
            SetCleared(cleared);
            Features.Evaluate();
        }

        /// <summary>최고 스테이지 배율 = stageGrowth^(최고 스테이지 − 1)</summary>
        static double Mult(int highestStage) => Math.Pow(StageGrowth, highestStage - 1);

        List<MetaReward> Pending()
        {
            var list = new List<MetaReward>();
            IdleChest.GetPending(list);
            return list;
        }

        [Test]
        public void Locked_DoesNotAccumulate_StartsWhenOpened()
        {
            SetupChest(cleared: 1);
            Assert.IsFalse(IdleChest.IsUnlocked);
            Advance(3);
            Assert.AreEqual(TimeSpan.Zero, IdleChest.Elapsed);
            Assert.IsFalse(IdleChest.CanClaim);
            IdleChest.AddOfflineTime(3600);
            Assert.AreEqual(TimeSpan.Zero, IdleChest.OfflineElapsed, "열리기 전 오프라인 시간은 버린다");
            Assert.AreEqual(0, Data.meta.idleChest.lastClaimUnix);

            SetCleared(2);
            Features.Evaluate();
            Assert.AreEqual(NowUnix, Data.meta.idleChest.lastClaimUnix, "열리는 순간 시작");
            Advance(1);
            Assert.AreEqual(TimeSpan.FromHours(1), IdleChest.Elapsed);
        }

        [Test]
        public void Lines_AccrueByHour_WithStageGrowth()
        {
            SetupChest(cleared: 2); // 최고 스테이지 3
            Advance(2);

            var pending = Pending();
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 2), AmountOf(pending, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.AreEqual(Math.Floor(1.5 * Mult(3) * 2), AmountOf(pending, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.IsNull(Find(pending, MetaRewardType.Item, itemId: "m_thread"), "최고 스테이지 10 전에는 안 쌓임");
            Assert.IsNull(Find(pending, MetaRewardType.Currency, CurrencyType.EventToken), "이벤트 전용 줄");
            Assert.IsNull(Find(pending, MetaRewardType.Currency, CurrencyType.Gold), "켜 둔 시간은 단추 줄에 없다");

            var rates = new List<IdleChestRate>();
            IdleChest.GetRates(rates);
            Assert.AreEqual(3 * Mult(3), rates.Find(r => r.Type == MetaRewardType.Currency && r.Currency == CurrencyType.Gem).PerHour, 1e-9);
            Assert.IsTrue(rates.Find(r => r.Currency == CurrencyType.Gold && r.Type == MetaRewardType.Currency).OfflineOnly);
            Assert.IsFalse(rates.Exists(r => r.ItemId == "m_thread"));
            Assert.AreEqual(0.25f, IdleChest.NextStageBonusPercent, 1e-4f, "다음 스테이지마다 시간당 +0.25%");
        }

        [Test]
        public void Carry_KeepsFractionsAcrossClaims()
        {
            SetupChest(cleared: 2);
            Advance(1);
            var first = IdleChest.Claim();
            Advance(1);
            var second = IdleChest.Claim();

            double total = AmountOf(first.Rewards, MetaRewardType.Item, itemId: "m_cloth") + AmountOf(second.Rewards, MetaRewardType.Item, itemId: "m_cloth");
            Assert.AreEqual(Math.Floor(1.5 * Mult(3) * 2), total, "1시간씩 두 번 = 2시간 한 번 (소수점 이월)");
            Assert.AreEqual(1, AmountOf(first.Rewards, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.AreEqual((int)Math.Floor(1.5 * Mult(3) * 2), ItemCount("m_cloth"));
        }

        [Test]
        public void Cap_IsEightHours_TwelveAfterTwoOne()
        {
            SetupChest(cleared: 2);
            int changed = 0;
            IdleChest.Changed += () => changed++;
            Progress.MarkDirty();
            Progress.Flush();
            changed = 0;
            Advance(20);

            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Cap);
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Elapsed);
            Assert.IsTrue(IdleChest.IsFull);
            Assert.AreEqual(1f, IdleChest.FillRatio);
            Assert.AreEqual(3, IdleChest.ArtStage);
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 8), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gem), "상한까지만");

            SetCleared(11);
            Features.Evaluate(); // 빠른 수선도 함께 열린다 (그 알림은 세지 않는다)
            changed = 0;
            Progress.Flush();
            Assert.AreEqual(TimeSpan.FromHours(12), IdleChest.Cap, "2-1 클리어 → 12시간");
            Assert.AreEqual(1, changed, "상한 변화 알림");
            Assert.AreEqual(TimeSpan.FromHours(12), IdleChest.Elapsed);
        }

        [Test]
        public void ArtStage_FollowsFillThresholds()
        {
            SetupChest();
            Assert.AreEqual(0, IdleChest.ArtStage);
            Advance(2); // 25%
            Assert.AreEqual(1, IdleChest.ArtStage);
            Advance(3); // 62.5%
            Assert.AreEqual(2, IdleChest.ArtStage);
            Advance(3); // 100%
            Assert.AreEqual(3, IdleChest.ArtStage);
        }

        [Test]
        public void GoldLine_OnlyFromOfflineTime_UpToCap()
        {
            SetupChest(cleared: 2);
            double gps = Growth.GoldPerSecond;

            IdleChest.AddOfflineTime(3600);
            Assert.AreEqual(Math.Floor(gps * 3600 * 0.5), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gold));
            Assert.IsTrue(IdleChest.CanClaim, "단추 줄만 있어도 받을 수 있다");

            IdleChest.AddOfflineTime(100 * 3600);
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.OfflineElapsed, "상한을 넘는 양은 버린다");
            IdleChest.AddOfflineTime(0);
            IdleChest.AddOfflineTime(-50);
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.OfflineElapsed);
        }

        [Test]
        public void Claim_GrantsAndResets_AndCounts()
        {
            SetupChest(cleared: 2);
            IdleChestClaimResult claimed = null;
            IdleChest.Claimed += r => claimed = r;
            Advance(3);
            IdleChest.AddOfflineTime(2 * 3600);
            double gps = Growth.GoldPerSecond;
            double goldBefore = Gold;

            var result = IdleChest.Claim();

            Assert.AreSame(result, claimed);
            Assert.AreEqual(TimeSpan.FromHours(3), result.Duration);
            Assert.AreEqual(TimeSpan.FromHours(2), result.OfflineDuration);
            Assert.IsFalse(result.Doubled);
            Assert.AreEqual(Math.Floor(gps * 7200 * 0.5), Gold - goldBefore);
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 3), Gem);
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.ChestClaim));
            Assert.AreEqual(NowUnix, Data.meta.idleChest.lastClaimUnix);
            Assert.AreEqual(0, Data.meta.idleChest.offlineSeconds);
            Assert.AreEqual(TimeSpan.Zero, IdleChest.Elapsed);

            AdvanceSeconds(30);
            Assert.IsFalse(IdleChest.CanClaim, "받을 것이 없으면 못 받음");
            Assert.IsNull(IdleChest.Claim());
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.ChestClaim), "빈 받기로 미션을 채울 수 없다");
        }

        [Test]
        public void Double_FreeOncePerDay_ThenTicket_GemExcluded()
        {
            SetupChest(cleared: 2);
            Advance(4);
            IdleChest.AddOfflineTime(3600);
            var preview = Pending();
            Assert.IsTrue(IdleChest.IsDoubleFree);
            Assert.IsTrue(IdleChest.CanDouble);

            var first = IdleChest.Claim(doubled: true);
            Assert.IsTrue(first.Doubled);
            Assert.IsFalse(first.UsedDoubleTicket);
            Assert.AreEqual(AmountOf(preview, MetaRewardType.Item, itemId: "m_cloth") * 2, AmountOf(first.Rewards, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.AreEqual(AmountOf(preview, MetaRewardType.Currency, CurrencyType.Gold) * 2, AmountOf(first.Rewards, MetaRewardType.Currency, CurrencyType.Gold));
            Assert.AreEqual(AmountOf(preview, MetaRewardType.Currency, CurrencyType.Gem), AmountOf(first.Rewards, MetaRewardType.Currency, CurrencyType.Gem), "별조각은 두 배 제외");
            Assert.IsFalse(IdleChest.IsDoubleFree, "하루 1번");
            Assert.IsFalse(IdleChest.CanDouble);

            Advance(4);
            var noTicket = IdleChest.Claim(doubled: true);
            Assert.IsFalse(noTicket.Doubled, "무료도 수선권도 없으면 1배");

            MetaRewards.Grant(new[] { MetaReward.DoubleTicket(1) });
            Advance(4);
            var withTicket = IdleChest.Claim(doubled: true);
            Assert.IsTrue(withTicket.Doubled);
            Assert.IsTrue(withTicket.UsedDoubleTicket);
            Assert.AreEqual(0, MetaRewards.DoubleTickets);

            SetUtc(2026, 11, 5, 5, 0);
            DailyClock.Refresh();
            Assert.IsTrue(IdleChest.IsDoubleFree, "다음 날 다시 무료");
        }

        [Test]
        public void QuickIdle_FreeThenFiftyThenHundred_TicketsSeparate()
        {
            SetupChest(cleared: 5);
            Currency.Add(CurrencyType.Gem, 150);
            Advance(1);
            var elapsedBefore = IdleChest.Elapsed;
            Assert.AreEqual(0, IdleChest.NextQuickIdleCost);
            Assert.IsTrue(IdleChest.HasFreeQuickIdle);

            var preview = new List<MetaReward>();
            IdleChest.GetQuickIdlePreview(preview);
            double gps = Growth.GoldPerSecond;
            Assert.AreEqual(Math.Floor(gps * 7200 * 0.5), AmountOf(preview, MetaRewardType.Currency, CurrencyType.Gold), "단추 줄도 2시간치");

            var free = IdleChest.TryQuickIdle(useTicket: false);
            Assert.IsNotNull(free);
            Assert.IsTrue(free.IsQuickIdle);
            Assert.AreEqual(0, free.GemSpent);
            Assert.AreEqual(TimeSpan.FromHours(2), free.Duration);
            Assert.AreEqual(Math.Floor(1.5 * Mult(6) * 2), AmountOf(free.Rewards, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.AreEqual(elapsedBefore, IdleChest.Elapsed, "바구니 타이머는 그대로");
            Assert.IsFalse(IdleChest.HasFreeQuickIdle);

            Assert.AreEqual(50, IdleChest.NextQuickIdleCost);
            Assert.AreEqual(50, IdleChest.TryQuickIdle(false).GemSpent);
            Assert.AreEqual(100, IdleChest.NextQuickIdleCost);
            Assert.AreEqual(100, IdleChest.TryQuickIdle(false).GemSpent);
            Assert.AreEqual(3, IdleChest.QuickIdleUsedToday);
            Assert.AreEqual(0, IdleChest.QuickIdleLeftToday);
            Assert.AreEqual(-1, IdleChest.NextQuickIdleCost);

            Assert.IsFalse(IdleChest.CanQuickIdle(false, out var reason));
            Assert.AreEqual("오늘 빠른 수선을 모두 썼어요", reason);
            Assert.IsNull(IdleChest.TryQuickIdle(false));

            Assert.IsFalse(IdleChest.CanQuickIdle(true, out reason));
            Assert.AreEqual("빠른 수선권이 없어요", reason);
            MetaRewards.Grant(new[] { MetaReward.QuickIdleTicket(2) });
            var ticket = IdleChest.TryQuickIdle(useTicket: true);
            Assert.IsTrue(ticket.UsedQuickTicket, "수선권은 하루 제한과 별개");
            Assert.AreEqual(1, MetaRewards.QuickIdleTickets);
            Assert.AreEqual(3, IdleChest.QuickIdleUsedToday);
            Assert.AreEqual(4, Progress.GetDaily(CounterKey.QuickIdle));

            SetUtc(2026, 11, 5, 5, 0);
            DailyClock.Refresh();
            Assert.AreEqual(0, IdleChest.QuickIdleUsedToday, "05:00에 초기화");
            Assert.IsTrue(IdleChest.HasFreeQuickIdle);
        }

        [Test]
        public void QuickIdle_LockedOrNoGem_GivesReason()
        {
            SetupChest(cleared: 2);
            Assert.IsFalse(IdleChest.IsQuickIdleUnlocked);
            Assert.IsFalse(IdleChest.CanQuickIdle(false, out var reason));
            Assert.AreEqual("스테이지 1-5를 클리어하면 열려요", reason);

            SetCleared(5);
            Features.Evaluate();
            Assert.IsNotNull(IdleChest.TryQuickIdle(false));
            Assert.IsFalse(IdleChest.CanQuickIdle(false, out reason), "별조각 0");
            Assert.AreEqual("별조각이 부족해요", reason);
        }

        [Test]
        public void Tampered_BlocksClaimAndFreeQuickIdle_AndFreezesBasket()
        {
            SetupChest(cleared: 5);
            Advance(2);
            DailyClock.Refresh();
            var elapsed = IdleChest.Elapsed;

            Advance(-1);
            DailyClock.Refresh();

            Assert.IsTrue(DailyClock.IsTampered);
            Assert.AreEqual(elapsed, IdleChest.Elapsed, "되돌리면 바구니가 멈춘다");
            Assert.IsFalse(IdleChest.CanClaim);
            Assert.IsNull(IdleChest.Claim());
            Assert.IsFalse(IdleChest.HasRedDot);
            Assert.IsFalse(IdleChest.CanQuickIdle(false, out var reason));
            Assert.AreEqual(DailyClock.TamperedMessage, reason);
            Assert.IsFalse(IdleChest.IsDoubleFree);

            Advance(1.1);
            DailyClock.Refresh();
            Assert.IsTrue(IdleChest.CanClaim, "시간이 맞으면 다시");
        }

        [Test]
        public void RedDot_AfterOneHour_NotForFreeQuickIdle()
        {
            SetupChest(cleared: 2);
            Advance(0.5);
            Assert.IsFalse(IdleChest.HasRedDot);
            Advance(0.5);
            Assert.IsTrue(IdleChest.HasRedDot, "1시간 이상 쌓임");

            IdleChest.Claim();
            Assert.IsFalse(IdleChest.HasRedDot);
            SetCleared(5);
            Features.Evaluate();
            Assert.IsTrue(IdleChest.HasFreeQuickIdle);
            Assert.IsFalse(IdleChest.HasRedDot, "무료 빠른 수선만으로는 점을 켜지 않는다 (M32 — 받을 것이 없는데 점이 켜지지 않게)");
            Assert.IsTrue(IdleChest.ShowsFreeTag, "대신 위젯 \"무료\" 표");

            Advance(1);
            Assert.IsTrue(IdleChest.HasRedDot);
            Assert.IsFalse(IdleChest.ShowsFreeTag, "표는 한 번에 하나 (점이 켜지면 무료 표는 숨김)");
        }

        [Test]
        public void RedDot_WhenFull_EvenIfRedDotHoursIsLonger()
        {
            SetupChest(cleared: 2);
            MetaDb.idleChest.redDotHours = 100f; // 상한(8시간)보다 길게
            Advance(7.9);
            Assert.IsFalse(IdleChest.HasRedDot);
            Advance(0.2);
            Assert.IsTrue(IdleChest.IsFull);
            Assert.IsTrue(IdleChest.HasRedDot, "가득 차면 점");
        }

        [Test]
        public void PendingGold_CountsOfflineGoldLine_WithoutAllocating()
        {
            SetupChest(cleared: 2);
            Assert.AreEqual(0, IdleChest.PendingGold, 1e-9);

            IdleChest.AddOfflineTime(3600);
            double expected = Math.Floor(Growth.GoldPerSecond * 3600 * 0.5 + 1e-9);
            Assert.Greater(expected, 0);
            Assert.AreEqual(expected, IdleChest.PendingGold, 1e-9, "단추 줄 = 초당 단추 × 비운 시간 × 오프라인 비율");
            Assert.AreEqual(AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gold), IdleChest.PendingGold, 1e-9, "받으면 들어올 단추와 같다");

            Advance(-1);
            DailyClock.Refresh();
            Assert.AreEqual(0, IdleChest.PendingGold, 1e-9, "되돌림 중에는 받을 수 없어 0");
        }

        [Test]
        public void Prefill_FillsOnce()
        {
            SetupChest(cleared: 2);
            IdleChest.Prefill(2f);

            Assert.AreEqual(TimeSpan.FromHours(2), IdleChest.Elapsed);
            Assert.IsTrue(IdleChest.CanClaim);
            Assert.IsTrue(Progress.HasFlag(MetaFlags.IdleChestPrefilled));

            IdleChest.Claim();
            IdleChest.Prefill(2f);
            Assert.AreEqual(TimeSpan.Zero, IdleChest.Elapsed, "한 번만");
        }

        [Test]
        public void HighStage_UnlocksLaterLines()
        {
            SetupChest(cleared: 12); // 최고 스테이지 13
            Advance(8);
            var pending = Pending();
            Assert.AreEqual(Math.Floor(0.15 * Mult(13) * 8), AmountOf(pending, MetaRewardType.Item, itemId: "m_thread"), "기억의 실 (스테이지 10부터)");
            Assert.Greater(AmountOf(pending, MetaRewardType.Item, itemId: "m_thread"), 0);
            Assert.AreEqual(TimeSpan.FromHours(12), IdleChest.Cap);
        }

        // ───────────── 4차: C-01 별조각 천장 · 16시간 단계 · 한 번짜리 상한 · 이벤트 훅 ─────────────

        [Test]
        public void GemLine_CappedAtOnePointFive()
        {
            SetupChest(cleared: 199, gemCap: 1.5); // 최고 스테이지 200 → 배율 1.0025^199 ≈ 1.64 > 1.5
            Assert.Greater(Mult(200), 1.5);
            Advance(2);

            var pending = Pending();
            Assert.AreEqual(Math.Floor(3 * 1.5 * 2 + 1e-9), AmountOf(pending, MetaRewardType.Currency, CurrencyType.Gem), "별조각 = 3 × min(배율, 1.5)");
            Assert.AreEqual(Math.Floor(1.5 * Mult(200) * 2), AmountOf(pending, MetaRewardType.Item, itemId: "m_cloth"), "재료 줄은 천장 없음");
            Assert.IsTrue(IdleChest.AnyLineAtStageCap);

            var rates = new List<IdleChestRate>();
            IdleChest.GetRates(rates);
            var gemRate = rates.Find(r => r.Type == MetaRewardType.Currency && r.Currency == CurrencyType.Gem);
            Assert.AreEqual(4.5, gemRate.PerHour, 1e-9);

            var quick = new List<MetaReward>();
            IdleChest.GetQuickIdlePreview(quick);
            Assert.AreEqual(Math.Floor(4.5 * 2 + 1e-9), AmountOf(quick, MetaRewardType.Currency, CurrencyType.Gem), "빠른 수선도 같은 천장");
        }

        [Test]
        public void GemLine_BelowCap_Unchanged()
        {
            SetupChest(cleared: 2, gemCap: 1.5);
            Advance(2);
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 2), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gem));
            Assert.IsFalse(IdleChest.AnyLineAtStageCap);
        }

        [Test]
        public void CapStep_ShopLevel200_Gives16h()
        {
            SetupChest(cleared: 12);
            MetaDb.idleChest.capSteps.Add(new IdleCapStep { stageCleared = 0, shopLevel = 200, capHours = 16f });
            Data.level = 199;
            Assert.AreEqual(TimeSpan.FromHours(12), IdleChest.Cap, "Lv.199 → 12시간");
            Assert.IsTrue(IdleChest.TryGetNextShopLevelCap(out int level, out float hours));
            Assert.AreEqual(200, level);
            Assert.AreEqual(16f, hours);

            Data.level = 200;
            Assert.AreEqual(TimeSpan.FromHours(16), IdleChest.Cap, "Lv.200 → 16시간");
            Assert.IsFalse(IdleChest.TryGetNextShopLevelCap(out _, out _));
            Advance(20);
            Assert.AreEqual(TimeSpan.FromHours(16), IdleChest.Elapsed);

            SetCleared(2);
            Assert.AreEqual(TimeSpan.FromHours(16), IdleChest.Cap, "스테이지 조건이 0인 단계는 수선점 레벨만 본다");
        }

        [Test]
        public void CapStep_AllConditionsMustHold()
        {
            SetupChest(cleared: 5);
            MetaDb.idleChest.capSteps = new List<IdleCapStep> { new IdleCapStep { stageCleared = 11, shopLevel = 200, capHours = 16f } };
            Data.level = 200;
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Cap, "레벨만 맞음");
            Data.level = 150;
            SetCleared(11);
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Cap, "스테이지만 맞음");
            Data.level = 200;
            Assert.AreEqual(TimeSpan.FromHours(16), IdleChest.Cap, "둘 다");

            MetaDb.idleChest.capSteps.Add(new IdleCapStep { stageCleared = 11, capHours = 12f });
            Assert.AreEqual(TimeSpan.FromHours(16), IdleChest.Cap, "맞는 단계 중 가장 큰 값 (목록 순서와 무관)");
        }

        [Test]
        public void BonusCap_RaisesCapUntilNextClaim()
        {
            SetupChest(cleared: 2);
            Advance(1);
            IdleChest.Claim();
            int changed = 0;
            IdleChest.Changed += () => changed++;

            IdleChest.GrantBonusCap(24);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.Cap);
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.BonusCap);
            IdleChest.GrantBonusCap(10);
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.Cap, "더 짧은 한 번짜리 상한은 무시");

            Advance(80);
            IdleChest.AddOfflineTime(80 * 3600);
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.Elapsed, "72시간 넘게 비워도 24시간 바구니");
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.OfflineElapsed);
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 24 + 1e-9), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gem));

            var result = IdleChest.Claim();
            Assert.IsNotNull(result);
            Assert.AreEqual(TimeSpan.FromHours(24), result.Duration);
            Assert.AreEqual(0, Data.meta.idleChest.bonusCapSeconds, "받으면 끝");
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Cap);
        }

        [Test]
        public void BonusCap_IgnoredWhileLocked()
        {
            SetupChest(cleared: 1);
            Assert.IsFalse(IdleChest.IsUnlocked);
            IdleChest.GrantBonusCap(24);
            Assert.AreEqual(0, Data.meta.idleChest.bonusCapSeconds);
        }

        [Test]
        public void NightBonusHours_AddsToMaterialAndGemLines_NotQuickIdle()
        {
            SetupChest(cleared: 2);
            long seenFrom = 0, seenTo = 0;
            IdleChest.NightBonusHours = (from, to) =>
            {
                seenFrom = from;
                seenTo = to;
                return 9.0; // 순수 겹침 (SPEC4 §8.3) — 쌓인 구간 2시간에서 자른다
            };
            long start = Data.meta.idleChest.lastClaimUnix;
            Advance(2);

            Assert.AreEqual(Math.Floor(3 * Mult(3) * 2), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gem), "배율 훅이 없으면 덤 없음");
            IdleChest.NightBonusMultiplier = () => 1.0;
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 2), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gem), "배율 1 = 덤 없음");
            IdleChest.NightBonusMultiplier = () => 1.5;

            var pending = Pending();
            Assert.AreEqual(start, seenFrom);
            Assert.AreEqual(start + 7200, seenTo, "실제로 쌓인 구간 [마지막 받기, + 쌓인 시간)");
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 3), AmountOf(pending, MetaRewardType.Currency, CurrencyType.Gem), "2시간 + 겹친 2시간 × 0.5 = 덤 1시간");
            Assert.AreEqual(Math.Floor(1.5 * Mult(3) * 3), AmountOf(pending, MetaRewardType.Item, itemId: "m_cloth"));
            Assert.IsNull(Find(pending, MetaRewardType.Currency, CurrencyType.EventToken), "이벤트 전용 줄에는 밤 덤이 없다");

            var quick = new List<MetaReward>();
            IdleChest.GetQuickIdlePreview(quick);
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 2), AmountOf(quick, MetaRewardType.Currency, CurrencyType.Gem), "빠른 수선에는 붙지 않는다");

            var result = IdleChest.Claim();
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 3), AmountOf(result.Rewards, MetaRewardType.Currency, CurrencyType.Gem));
        }

        [Test]
        public void EventLineHours_AccruesEventOnlyLine_ForOverlapOnly()
        {
            SetupChest(cleared: 2);
            Advance(4);
            Assert.IsNull(Find(Pending(), MetaRewardType.Currency, CurrencyType.EventToken), "훅이 없으면 이벤트 줄은 쌓이지 않는다");
            var rates = new List<IdleChestRate>();
            IdleChest.GetRates(rates);
            Assert.IsFalse(rates.Exists(r => r.Currency == CurrencyType.EventToken));

            IdleChest.EventLineHours = (from, to) => Math.Min(1.5, (to - from) / 3600.0);
            Assert.AreEqual(Math.Floor(2 * Mult(3) * 1.5), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.EventToken), "겹친 1.5시간만");
            IdleChest.GetRates(rates);
            Assert.IsTrue(rates.Exists(r => r.Currency == CurrencyType.EventToken), "이벤트가 열려 있으면 시간당 양 표에 보인다");
            Assert.AreEqual(Math.Floor(3 * Mult(3) * 4), AmountOf(Pending(), MetaRewardType.Currency, CurrencyType.Gem), "다른 줄은 그대로");
        }
    }
}
