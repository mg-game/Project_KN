using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 백업 알림 (SAVE-01 · 델타 17, RM-2 SB-1 → P4-2): 백업 없이 14개 날 키가 지나면 우편 한 통 (SendOnce "backup:" + 날 키, 보상 없음),
    /// 14일에 한 번까지, 백업하면 다시 셈, days ≤ 0이면 꺼짐, 되돌림 중에는 없음, 콘텐츠 문구가 비면 기본 문구.
    /// </summary>
    public class BackupReminderTests : MetaSystemsTestBase
    {
        void Setup(int days = 14, string title = "", string body = "")
        {
            MetaDb.backupReminder = new BackupReminderConfig { days = days, mailTitle = title, mailBody = body };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start(); // 설치한 날 = 11-04
        }

        void NextDay(int days = 1)
        {
            Advance(24 * days);
            DailyClock.Refresh(); // 세션의 날 바뀜 → 모듈 OnDayChanged
        }

        int ReminderMails() => Data.mails.FindAll(m => m.title == BackupReminderSystem.DefaultMailTitle || m.title == "백업 챙기기").Count;

        [Test]
        public void FourteenDayKeysWithoutBackup_SendsOneMail()
        {
            Setup();
            Assert.AreEqual(0, BackupReminder.DaysSinceBackup);
            NextDay(13);
            Assert.AreEqual(13, BackupReminder.DaysSinceBackup);
            Assert.AreEqual(0, ReminderMails());

            NextDay();
            Assert.AreEqual(14, BackupReminder.DaysSinceBackup);
            Assert.AreEqual(1, ReminderMails());
            var mail = Data.mails.Find(m => m.title == BackupReminderSystem.DefaultMailTitle);
            Assert.AreEqual(BackupReminderSystem.DefaultMailBody, mail.body, "콘텐츠 문구가 비면 기본 문구");
            Assert.IsFalse(mail.HasRewards);
            Assert.AreEqual(DailyClock.DayKey, Data.meta.backup.lastReminderDayKey);
            Assert.IsTrue(Data.sentOnceMailKeys.Contains(BackupReminderSystem.MailKeyPrefix + DailyClock.DayKey));

            BackupReminder.OnDayChanged(DailyClock.DayKey);
            BackupReminder.Connect();
            Assert.AreEqual(1, ReminderMails(), "같은 날 다시 불러도 한 통");
        }

        [Test]
        public void AtMostOncePerFourteenDays()
        {
            Setup();
            NextDay(14);
            Assert.AreEqual(1, ReminderMails());
            NextDay(13);
            Assert.AreEqual(1, ReminderMails(), "13일 뒤 = 아직");
            NextDay();
            Assert.AreEqual(2, ReminderMails(), "다음 14일");
        }

        [Test]
        public void MarkBackedUp_RestartsTheCount()
        {
            Setup();
            NextDay(10);
            BackupReminder.MarkBackedUp();
            Assert.AreEqual(DailyClock.DayKey, Data.meta.backup.lastExportDayKey);
            Assert.AreEqual(0, BackupReminder.DaysSinceBackup);
            NextDay(13);
            Assert.AreEqual(0, ReminderMails());
            NextDay();
            Assert.AreEqual(1, ReminderMails(), "백업한 날부터 14일");
        }

        [Test]
        public void ContentCopy_Used()
        {
            Setup(title: "백업 챙기기", body: "세이브 코드를 복사해 두세요");
            NextDay(14);
            var mail = Data.mails.Find(m => m.title == "백업 챙기기");
            Assert.IsNotNull(mail);
            Assert.AreEqual("세이브 코드를 복사해 두세요", mail.body);
        }

        [Test]
        public void DaysZero_IsOff()
        {
            Setup(days: 0);
            Assert.IsFalse(BackupReminder.IsEnabled);
            NextDay(30);
            Assert.AreEqual(0, ReminderMails());
            Assert.IsNull(Data.meta.backup.lastReminderDayKey);
        }

        [Test]
        public void Tampered_NoMail()
        {
            Setup();
            NextDay(20);
            Data.mails.Clear();
            Data.meta.backup.lastReminderDayKey = null;
            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(BackupReminder.IsReminderDue);
            BackupReminder.Connect();
            Assert.AreEqual(0, ReminderMails());
        }
    }
}
