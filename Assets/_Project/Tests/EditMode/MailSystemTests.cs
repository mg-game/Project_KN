using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    public class MailSystemTests : SystemsTestBase
    {
        static RewardData[] Gems(double amount) => new[] { RewardData.Currency(CurrencyType.Gem, amount) };

        [Test]
        public void Send_AddsNewestFirst()
        {
            Mail.Send("첫 번째", "");
            Mail.Send("두 번째", "");

            Assert.AreEqual(2, Mail.Count);
            Assert.AreEqual("두 번째", Mail.Mails[0].title);
        }

        [Test]
        public void Claim_GrantsRewardsAndRemovesMail()
        {
            var mail = Mail.Send("보상", "", new[]
            {
                RewardData.Currency(CurrencyType.Gem, 100),
                RewardData.Item("r1", 2),
            });

            var rewards = Mail.Claim(mail.id);

            Assert.AreEqual(2, rewards.Count);
            Assert.AreEqual(100, Currency.Get(CurrencyType.Gem), 1e-9);
            Assert.AreEqual(2, Inventory.GetCount("r1"));
            Assert.AreEqual(0, Mail.Count);
        }

        [Test]
        public void Claim_TextOnlyMail_JustRemoves()
        {
            var mail = Mail.Send("안내", "보상 없는 우편");

            var rewards = Mail.Claim(mail.id);

            Assert.IsNotNull(rewards);
            Assert.AreEqual(0, rewards.Count);
            Assert.AreEqual(0, Mail.Count);
        }

        [Test]
        public void ClaimAll_MergesRewards_AndKeepsTextOnlyMails()
        {
            Mail.Send("A", "", Gems(100));
            Mail.Send("B", "", Gems(50));
            Mail.Send("안내", "");

            var rewards = Mail.ClaimAll();

            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual(150, rewards[0].amount, 1e-9);
            Assert.AreEqual(150, Currency.Get(CurrencyType.Gem), 1e-9);
            Assert.AreEqual(1, Mail.Count);
        }

        [Test]
        public void ExpiredMail_IsRemovedAndCannotBeClaimed()
        {
            var mail = Mail.Send("만료", "", Gems(100), expireDays: 1);
            Clock.Advance(TimeSpan.FromDays(2));

            Assert.IsNull(Mail.Claim(mail.id));
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gem), 1e-9);
            Assert.AreEqual(0, Mail.Count);
        }

        [Test]
        public void RemoveExpired_KeepsPermanentMail()
        {
            Mail.Send("기간제", "", Gems(1), expireDays: 1);
            Mail.Send("무기한", "", Gems(1), expireDays: 0);
            Clock.Advance(TimeSpan.FromDays(1000));

            Assert.AreEqual(1, Mail.RemoveExpired());
            Assert.AreEqual("무기한", Mail.Mails[0].title);
        }

        [Test]
        public void SendOnce_DoesNotDuplicate()
        {
            Assert.IsTrue(Mail.SendOnce("welcome", "환영", "", Gems(100)));
            Assert.IsFalse(Mail.SendOnce("welcome", "환영", "", Gems(100)));
            Assert.AreEqual(1, Mail.Count);
        }
    }
}
