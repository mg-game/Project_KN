using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 오프라인 쿠폰 (EVT-02, SPEC4 §4.5 — 확장 항목): 다듬기, 해시가 늘 같음, 한 번만, 날 키 사용 기간, 5번 틀리면 60초 잠금, 되돌림,
    /// 보상은 우편(30일)으로, 쿠폰이 없으면 모두 Invalid.
    /// </summary>
    public class CouponTests : MetaSystemsTestBase
    {
        const string Salt = "lost-and-lovely-test";

        void Setup(string validFrom = null, string validTo = null)
        {
            MetaDb.couponSalt = Salt;
            MetaDb.coupons = new List<CouponDef>
            {
                new CouponDef
                {
                    hash = CouponSystem.Hash(Salt, "QATEST01"),
                    validFromDayKey = validFrom,
                    validToDayKey = validTo,
                    rewards = new List<RewardData> { RewardData.Currency(CurrencyType.Gem, 100), RewardData.Item("m_cloth", 5) },
                    mailTitle = "",
                    mailBody = "시험 쿠폰이에요",
                },
            };
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            DailyClock.Refresh();
        }

        [Test]
        public void Normalize_TrimsUppercasesAndValidates()
        {
            Assert.AreEqual("QATEST01", CouponSystem.Normalize("  qa-test 01 "));
            Assert.AreEqual("ABC123", CouponSystem.Normalize("abc-123"));
            Assert.IsNull(CouponSystem.Normalize("ABC12"), "6자 미만");
            Assert.IsNull(CouponSystem.Normalize("ABCDEFGHIJKLMNOPQ"), "16자 초과");
            Assert.IsNull(CouponSystem.Normalize("쿠폰번호예요"), "영문 · 숫자만");
            Assert.IsNull(CouponSystem.Normalize("ABC_123"));
            Assert.IsNull(CouponSystem.Normalize(null));
            Assert.IsNull(CouponSystem.Normalize("   "));
        }

        [Test]
        public void Hash_IsStableLowercaseHex()
        {
            string a = CouponSystem.Hash(Salt, "QATEST01");
            Assert.AreEqual(a, CouponSystem.Hash(Salt, "QATEST01"));
            Assert.AreEqual(64, a.Length);
            Assert.AreEqual(a.ToLowerInvariant(), a);
            Assert.AreNotEqual(a, CouponSystem.Hash("other-salt", "QATEST01"), "소금이 다르면 다른 해시");
            // 고정값 (python hashlib.sha256) — 식(소금 + ":" + 번호, UTF-8, 소문자 16진수)이 바뀌면 콘텐츠 해시가 모두 틀어진다
            Assert.AreEqual("a0601da2f95481bc1a3d0ecd3503f60060fc068e12526074c2c97077feef75ff", a);
            Assert.AreEqual("e7ac0786668e0ff0f02b62bd04f45ff636fd82db63b1104601c975dc005f3a67", CouponSystem.Hash("", ""));
        }

        [Test]
        public void Redeem_OnceOnly_SendsMail()
        {
            Setup();
            int before = Data.mails.Count;
            Assert.AreEqual(CouponResult.Ok, Coupons.TryRedeem("qa-test-01"));
            Assert.AreEqual(before + 1, Data.mails.Count);
            var mail = Data.mails[0];
            Assert.AreEqual(CouponSystem.DefaultMailTitle, mail.title, "제목이 비면 기본 제목");
            Assert.AreEqual("시험 쿠폰이에요", mail.body);
            Assert.AreEqual(2, mail.rewards.Count);
            Assert.AreEqual(NowUnix + 30 * 86400L, mail.expireAt, "30일 보관");
            Assert.IsTrue(Data.meta.coupons.redeemed.Contains(CouponSystem.Hash(Salt, "QATEST01")), "해시만 저장");

            Assert.AreEqual(CouponResult.AlreadyUsed, Coupons.TryRedeem("QATEST01"));
            Assert.AreEqual(before + 1, Data.mails.Count);
        }

        [Test]
        public void DateWindow_ByDayKey()
        {
            Setup(validFrom: "2026-11-05", validTo: "2026-11-06");
            Assert.AreEqual(CouponResult.OutOfPeriod, Coupons.TryRedeem("QATEST01"), "11-04");
            SetUtc(2026, 11, 5, 4, 59);
            DailyClock.Refresh();
            Assert.AreEqual(CouponResult.OutOfPeriod, Coupons.TryRedeem("QATEST01"), "05:00 전 = 아직 11-04");
            SetUtc(2026, 11, 7, 4, 59);
            DailyClock.Refresh();
            Assert.AreEqual(CouponResult.Ok, Coupons.TryRedeem("QATEST01"), "11-06 날 키 (양끝 포함)");
        }

        [Test]
        public void DateWindow_EndExclusiveAfterLastDay()
        {
            Setup(validTo: "2026-11-03");
            Assert.AreEqual(CouponResult.OutOfPeriod, Coupons.TryRedeem("QATEST01"));
            Assert.AreEqual(0, Data.meta.coupons.failStreak, "기간 밖은 틀린 번호로 세지 않는다");
        }

        [Test]
        public void FiveMisses_LockSixtySeconds()
        {
            Setup();
            for (int i = 0; i < 4; i++) Assert.AreEqual(CouponResult.Invalid, Coupons.TryRedeem("WRONG" + i + "X"));
            Assert.AreEqual(4, Data.meta.coupons.failStreak);
            Assert.AreEqual(CouponResult.Invalid, Coupons.TryRedeem("bad!"), "형식이 틀려도 한 번 틀린 것");
            Assert.AreEqual(CouponResult.Locked, Coupons.TryRedeem("QATEST01"), "5번 틀리면 잠금 — 맞는 번호도");
            Assert.AreEqual(TimeSpan.FromSeconds(60), Coupons.LockRemaining);

            AdvanceSeconds(59);
            Assert.AreEqual(CouponResult.Locked, Coupons.TryRedeem("QATEST01"));
            AdvanceSeconds(1);
            Assert.AreEqual(CouponResult.Ok, Coupons.TryRedeem("QATEST01"));
            Assert.AreEqual(0, Data.meta.coupons.failStreak);
        }

        [Test]
        public void Tampered_Blocks()
        {
            Setup();
            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.AreEqual(CouponResult.Tampered, Coupons.TryRedeem("QATEST01"));
            Assert.IsEmpty(Data.meta.coupons.redeemed);
        }

        [Test]
        public void NoCoupons_AllInvalid()
        {
            Assert.AreEqual(CouponResult.Invalid, Coupons.TryRedeem("QATEST01"));
        }
    }
}
