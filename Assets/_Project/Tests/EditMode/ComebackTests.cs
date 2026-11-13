using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 다시 만난 날 (CR-06, SPEC4 §8.2): 71시간 → 없음, 72시간 → 시작, 14일 쉼, 평생 6번, 되돌림 · 튜토리얼 중 → 없음,
    /// 바구니 한 번짜리 24시간 상한이 비운 시간을 넣기 전에 걸리고 받으면 끝, 도장 하루 한 칸 (3일), 새 날 키 접속 때 카드 다시.
    /// </summary>
    public class ComebackTests : MetaSystemsTestBase
    {
        const long Hour = 3600;

        static ComebackDayDef Day(int day, params MetaReward[] rewards) => new ComebackDayDef { day = day, rewards = new List<MetaReward>(rewards) };

        void SetupComeback(bool ftueDone = true)
        {
            MetaDb.comeback = new ComebackConfig
            {
                minAwaySeconds = 72 * Hour,
                cooldownDays = 14,
                lifetimeMax = 6,
                bonusCapHours = 24,
                days = new List<ComebackDayDef>
                {
                    Day(1, MetaReward.Gem(300), MetaReward.QuickIdleTicket(2)),
                    Day(2, MetaReward.DoubleTicket(2), MetaReward.Item("m_cloth", 10)),
                    Day(3, MetaReward.SelectShards(20)),
                },
            };
            MetaDb.idleChest = new IdleChestConfig
            {
                baseCapHours = 8f,
                capSteps = new List<IdleCapStep>(),
                lines = new List<IdleChestLine> { new IdleChestLine { reward = MetaReward.Gem(3) } },
                stageGrowth = 1.0,
                minClaimSeconds = 60,
            };
            Rebuild();
            Data.meta.ftue.done = ftueDone;
            SetUtc(2026, 11, 4, 9, 0);
        }

        /// <summary>awayHours 비운 것처럼 기록을 맞추고 부팅 (MetaSession.Start: 모듈 BeforeAwayApplied → 바구니 → AfterStartup)</summary>
        void BootAfter(double awayHours)
        {
            long last = NowUnix - (long)(awayHours * Hour);
            Data.meta.clock.lastSessionEndUnix = last;
            Data.meta.clock.maxSeenUnix = last;
            Data.lastSaveUnix = last;
            Data.meta.idleChest.lastClaimUnix = last;
            MetaSession.Start();
        }

        void NextDay()
        {
            Advance(24);
            DailyClock.Refresh();
        }

        [Test]
        public void Away71Hours_Nothing()
        {
            SetupComeback();
            BootAfter(71);
            Assert.IsFalse(Comeback.IsActive);
            Assert.AreEqual(0, Comeback.Day);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.Comeback));
            Assert.AreEqual(0, Data.meta.comeback.count);
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Cap, "한 번짜리 상한 없음");
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.OfflineElapsed);
        }

        /// <summary>경계 그대로 (SPEC4 §8.2 '71 h → 없음, 72 h → 시작'): 259,199초 = 아직, 259,200초 = 시작 (≥ 규칙), 꼭 72시간 비우고 부팅하면 시작</summary>
        [Test]
        public void Away72HoursExactly_Starts_OneSecondLess_DoesNot()
        {
            SetupComeback();
            Assert.IsFalse(Comeback.CanTrigger(72 * Hour - 1), "259,199초");
            Assert.IsTrue(Comeback.CanTrigger(72 * Hour), "259,200초");

            BootAfter(72);
            Assert.IsTrue(Comeback.IsActive, "꼭 72시간");
            Assert.AreEqual(1, Comeback.Day);
            Assert.AreEqual(72 * Hour, Comeback.AwaySeconds);
            Assert.AreEqual(1, Data.meta.comeback.count);
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.Cap);
        }

        [Test]
        public void Away80Hours_Starts_BonusCapBeforeOfflineTime()
        {
            SetupComeback();
            BootAfter(80);

            Assert.IsTrue(Comeback.IsActive);
            Assert.AreEqual(1, Comeback.Day);
            Assert.IsTrue(Comeback.CanClaimToday);
            Assert.AreEqual(80 * Hour, Comeback.AwaySeconds);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.Comeback), "접속 카드");
            Assert.AreEqual(1, Data.meta.comeback.count);
            Assert.AreEqual(DailyClock.DayKey, Data.meta.comeback.lastDayKey);
            Assert.AreEqual(DailyClock.DayKey, Data.meta.comeback.startDayKey);

            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.Cap, "24시간 한 번짜리 상한");
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.OfflineElapsed, "비운 시간을 넣기 전에 상한이 걸려 24시간치가 들어갔다");
            Assert.AreEqual(TimeSpan.FromHours(24), IdleChest.Elapsed);

            var claim = IdleChest.Claim();
            Assert.AreEqual(TimeSpan.FromHours(24), claim.Duration);
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Cap, "받으면 상한은 원래대로");
        }

        /// <summary>비운 시간은 저장된다: 10일 비우고 시작한 뒤 도장 전에 앱을 닫았다 다시 켜도 카드는 '10일 만이에요' (P4-0 ComebackSave.awaySeconds)</summary>
        [Test]
        public void AwaySeconds_KeptAcrossRestart()
        {
            SetupComeback();
            BootAfter(240);
            Assert.IsTrue(Comeback.IsActive);
            Assert.AreEqual(240 * Hour, Comeback.AwaySeconds);
            Assert.AreEqual(240 * Hour, Data.meta.comeback.awaySeconds);

            Rebuild(); // 앱을 다시 켬 (같은 세이브, 새 시스템)
            Assert.IsTrue(Comeback.IsActive);
            Assert.IsTrue(Comeback.CanClaimToday);
            Assert.AreEqual(240 * Hour, Comeback.AwaySeconds, "다시 켜도 10일");

            for (int day = 1; day <= 3; day++)
            {
                Assert.IsNotNull(Comeback.TryClaimToday());
                NextDay();
            }
            Assert.IsFalse(Comeback.IsActive);
            Assert.AreEqual(0, Comeback.AwaySeconds, "끝나면 0");
        }

        [Test]
        public void TutorialNotDone_Nothing()
        {
            SetupComeback(ftueDone: false);
            BootAfter(100);
            Assert.IsFalse(Comeback.IsActive);
            Assert.AreEqual(TimeSpan.FromHours(8), IdleChest.Cap);
        }

        [Test]
        public void Tampered_Blocks()
        {
            SetupComeback();
            DailyClock.Refresh();
            Data.meta.clock.maxSeenUnix = NowUnix + 2 * Hour; // 시계를 2시간 되돌린 상태
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(Comeback.CanTrigger(100 * Hour));
            Comeback.BeforeAwayApplied(100 * Hour, true);
            Assert.IsFalse(Comeback.IsActive);
        }

        [Test]
        public void Cooldown14Days_AndLifetimeSix()
        {
            SetupComeback();
            DailyClock.Refresh();
            Comeback.BeforeAwayApplied(80 * Hour, true);
            Assert.IsTrue(Comeback.IsActive);
            Assert.AreEqual(1, Data.meta.comeback.count);

            Advance(13 * 24);
            DailyClock.Refresh();
            Assert.IsFalse(Comeback.CanTrigger(80 * Hour), "13일 뒤 = 아직 쉼");
            Advance(24);
            DailyClock.Refresh();
            Assert.IsTrue(Comeback.CanTrigger(80 * Hour), "14일 뒤");
            Comeback.BeforeAwayApplied(80 * Hour, false);
            Assert.AreEqual(2, Data.meta.comeback.count);

            Data.meta.comeback.count = 6;
            Data.meta.comeback.lastDayKey = null;
            Assert.IsFalse(Comeback.CanTrigger(80 * Hour), "평생 6번");
        }

        [Test]
        public void EmptyContent_IsOff()
        {
            SetupComeback();
            MetaDb.comeback.days.Clear();
            MetaDb.ClearCache();
            DailyClock.Refresh();
            Assert.IsFalse(Comeback.CanTrigger(100 * Hour));
            Comeback.BeforeAwayApplied(100 * Hour, true);
            Assert.IsFalse(Comeback.IsActive);
            Assert.AreEqual(0, Data.meta.idleChest.bonusCapSeconds);
        }

        [Test]
        public void Stamps_OncePerDayKey_ThreeDays_ThenDone()
        {
            SetupComeback();
            BootAfter(80);

            var day1 = Comeback.TryClaimToday();
            Assert.IsNotNull(day1);
            Assert.AreEqual(300, AmountOf(day1, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.AreEqual(2, MetaRewards.QuickIdleTickets);
            Assert.IsNull(Comeback.TryClaimToday(), "같은 날 두 번은 못 찍는다");
            Assert.IsFalse(Comeback.CanClaimToday);
            Assert.AreEqual(2, Comeback.Day, "다음에 찍을 칸");
            Assert.AreEqual(1, Comeback.StampsTaken);

            Advance(3); // 같은 날 키
            DailyClock.Refresh();
            Assert.IsNull(Comeback.TryClaimToday());

            NextDay();
            Assert.IsTrue(Comeback.CanClaimToday);
            Assert.IsNotNull(Comeback.TryClaimToday());
            Assert.AreEqual(2, MetaRewards.DoubleTickets);
            Assert.AreEqual(10, ItemCount("m_cloth"));

            NextDay();
            NextDay(); // 하루 빠져도 다음 접속에서 이어 찍는다
            Assert.AreEqual(3, Comeback.Day);
            Assert.IsNotNull(Comeback.TryClaimToday());
            Assert.AreEqual(20, MetaRewards.SelectShards);
            Assert.IsFalse(Comeback.IsActive, "3일 뒤 끝");
            Assert.AreEqual(0, Comeback.Day);
            Assert.IsNull(Comeback.TryClaimToday());
        }

        [Test]
        public void NewDayKey_RequeuesCard_WhileStampsRemain()
        {
            SetupComeback();
            BootAfter(80);
            Assert.IsTrue(Popups.Remove(MetaPopupKind.Comeback));
            Assert.IsNotNull(Comeback.TryClaimToday());

            Comeback.AfterStartup(false);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.Comeback), "오늘 찍었으면 카드 없음");

            NextDay(); // 세션의 날 바뀜 → 모듈 OnDayChanged
            Assert.IsTrue(Popups.Contains(MetaPopupKind.Comeback), "새 날 키 → 다음 도장 카드");
            Popups.Remove(MetaPopupKind.Comeback);
            Comeback.AfterStartup(false);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.Comeback), "같은 날에는 한 번");
        }
    }
}
