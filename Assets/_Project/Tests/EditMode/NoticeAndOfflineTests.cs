using System;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    public class NoticeSystemTests : SystemsTestBase
    {
        static NoticeData MakeNotice(string id, int order, string date, bool popup = false) =>
            new NoticeData { id = id, title = id, date = date, order = order, popup = popup };

        [Test]
        public void SetNotices_SortsByOrderThenDate()
        {
            Notice.SetNotices(new[]
            {
                MakeNotice("old", 0, "2026-01-01"),
                MakeNotice("pinned", 10, "2025-01-01"),
                MakeNotice("new", 0, "2026-02-01"),
            });

            Assert.AreEqual("pinned", Notice.Notices[0].id);
            Assert.AreEqual("new", Notice.Notices[1].id);
            Assert.AreEqual("old", Notice.Notices[2].id);
        }

        [Test]
        public void MarkRead_UpdatesUnreadCount()
        {
            Notice.SetNotices(new[] { MakeNotice("a", 0, "2026-01-01"), MakeNotice("b", 0, "2026-01-01") });
            Assert.AreEqual(2, Notice.UnreadCount);

            Notice.MarkRead(Notice.Notices[0]);

            Assert.AreEqual(1, Notice.UnreadCount);
        }

        [Test]
        public void HideForToday_HidesStartupNoticeUntilNextDay()
        {
            Notice.SetNotices(new[] { MakeNotice("normal", 10, "2026-01-01"), MakeNotice("event", 0, "2026-01-01", popup: true) });
            Assert.AreEqual("event", Notice.GetStartupNotice().id);

            Notice.HideForToday();
            Assert.IsNull(Notice.GetStartupNotice());

            Clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual("event", Notice.GetStartupNotice().id);
        }

        [Test]
        public void StartupNoticeToShow_IsFirstUnreadPopup_OnePerLaunch()
        {
            Notice.SetNotices(new[]
            {
                MakeNotice("plain", 30, "2026-01-03"),
                MakeNotice("first", 20, "2026-01-02", popup: true),
                MakeNotice("second", 10, "2026-01-01", popup: true),
            });

            Assert.AreEqual("first", Notice.GetStartupNoticeToShow().id, "정렬 순서에서 처음 나오는 팝업 공지 (팝업이 아닌 공지는 건너뜀)");
            Assert.AreEqual("first", Notice.GetStartupNoticeToShow().id, "읽기 전에는 몇 번 물어도 같은 공지");

            Notice.MarkRead(Notice.Notices[1]); // first
            Assert.AreEqual("second", Notice.GetStartupNoticeToShow().id, "다음 접속은 그다음 안 읽은 공지");

            Notice.MarkRead(Notice.Notices[2]); // second
            Assert.IsNull(Notice.GetStartupNoticeToShow(), "다 읽었으면 읽은 공지로 돌아가지 않는다");
            Assert.AreEqual(1, Notice.UnreadCount, "팝업이 아닌 공지만 남음");
        }

        [Test]
        public void StartupNoticeToShow_HiddenToday_IsNull()
        {
            Notice.SetNotices(new[] { MakeNotice("event", 0, "2026-01-01", popup: true) });
            Notice.HideForToday();

            Assert.IsNull(Notice.GetStartupNoticeToShow());

            Clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual("event", Notice.GetStartupNoticeToShow().id);
        }

        [Test]
        public void StartupNoticeToShow_NoNotices_IsNull()
        {
            Assert.IsNull(Notice.GetStartupNoticeToShow(), "불러오기 전");
            Notice.SetNotices(new[] { MakeNotice("plain", 0, "2026-01-01") });
            Assert.IsNull(Notice.GetStartupNoticeToShow(), "팝업 공지가 없음");
        }

        [Test]
        public void BundledNoticeJson_IsValid()
        {
            var asset = Resources.Load<TextAsset>("Data/notices");
            Assert.IsNotNull(asset, "Resources/Data/notices.json");

            Assert.IsTrue(NoticeLoader.TryParse(asset.text, out var notices));
            Assert.IsNotEmpty(notices);
            Assert.IsTrue(notices.TrueForAll(n => !string.IsNullOrEmpty(n.id) && !string.IsNullOrEmpty(n.title)));
        }
    }

    public class OfflineRewardTests : SystemsTestBase
    {
        [Test]
        public void NewPlayer_GetsNothing()
        {
            Offline.Calculate(0);
            Assert.IsFalse(Offline.HasPending);
        }

        [Test]
        public void ShortAbsence_GetsNothing()
        {
            Offline.Calculate(NowUnix - (Config.offlineMinSeconds - 1));
            Assert.IsFalse(Offline.HasPending);
        }

        [Test]
        public void OneHour_GivesRateOfIdleIncome()
        {
            Offline.Calculate(NowUnix - 3600);

            Assert.AreEqual(Growth.GoldPerSecond * 3600 * Config.offlineRewardRate, Offline.PendingGold, 1e-6);
            Assert.AreEqual(TimeSpan.FromHours(1), Offline.PendingDuration);
        }

        [Test]
        public void OfflineGold_IncludesStageBonus()
        {
            Data.battle.highestStage = 21;
            Offline.Calculate(NowUnix - 3600);

            double expected = Config.baseGoldPerSecond * (1 + GrowthSystem.StageGoldBonusPerStage * 20) * 3600 * Config.offlineRewardRate;
            Assert.AreEqual(expected, Offline.PendingGold, 1e-6);
        }

        [Test]
        public void LongAbsence_IsCapped()
        {
            Offline.Calculate(NowUnix - 100 * 3600);
            Assert.AreEqual(TimeSpan.FromHours(Config.offlineMaxHours), Offline.PendingDuration);
        }

        [Test]
        public void ClockMovedBackwards_GetsNothing()
        {
            Offline.Calculate(NowUnix + 3600);
            Assert.IsFalse(Offline.HasPending);
        }

        [Test]
        public void Claim_AddsGoldAndResets()
        {
            Offline.Calculate(NowUnix - 3600);
            double pending = Offline.PendingGold;

            Assert.AreEqual(pending, Offline.Claim(), 1e-9);
            Assert.AreEqual(pending, Currency.Get(CurrencyType.Gold), 1e-9);
            Assert.IsFalse(Offline.HasPending);
        }
    }
}
