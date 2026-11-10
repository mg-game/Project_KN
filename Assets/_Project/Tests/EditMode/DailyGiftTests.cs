using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 오늘의 작은 선물 (NEW-02, SPEC4 §13.7): 날 키마다 한 번, 04:59/05:00 경계, 요일 돌림 (날 키 요일, 월요일 = 0), 상점(1-8) 전 잠김, 되돌림 막음,
    /// 빨간 점 tab.shop.free. UTC와 +09:00 시계.
    /// </summary>
    public class DailyGiftTests : MetaSystemsTestBase
    {
        static List<DailyGiftDef> Rotation()
        {
            var list = new List<DailyGiftDef>();
            for (int weekday = 0; weekday < 7; weekday++)
                list.Add(new DailyGiftDef { weekday = weekday, rewards = new List<MetaReward> { MetaReward.Item("m_cloth", weekday + 1) } });
            return list;
        }

        void SetupGift()
        {
            AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            MetaDb.dailyGifts = Rotation();
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0); // 수요일
            DailyClock.Refresh();
            SetCleared(8);
            Features.Evaluate();
        }

        [Test]
        public void OncePerDayKey_FiveAmBoundary()
        {
            SetupGift();
            Assert.IsTrue(DailyGift.CanClaim);
            var granted = DailyGift.TryClaim();
            Assert.IsNotNull(granted);
            Assert.AreEqual(3, ItemCount("m_cloth"), "수요일 = 2번 줄 (3개)");
            Assert.IsFalse(DailyGift.CanClaim);
            Assert.IsNull(DailyGift.TryClaim(), "하루 한 번");
            Assert.AreEqual(TimeSpan.FromHours(20), DailyGift.TimeUntilNext);

            SetUtc(2026, 11, 5, 4, 59);
            DailyClock.Refresh();
            Assert.IsFalse(DailyGift.CanClaim, "04:59 = 아직 수요일");
            SetUtc(2026, 11, 5, 5, 0);
            DailyClock.Refresh();
            Assert.IsTrue(DailyGift.CanClaim, "05:00 = 목요일 선물");
            Assert.AreEqual(4, DailyGift.Today[0].amount);
        }

        [Test]
        public void Rotation_ByDayKeyWeekday_MondayZero()
        {
            SetupGift();
            SetUtc(2026, 11, 9, 9, 0); // 월요일
            Assert.AreEqual(1, DailyGift.Today[0].amount);
            SetUtc(2026, 11, 9, 4, 0); // 월 04:00 = 일요일 날 키
            Assert.AreEqual(7, DailyGift.Today[0].amount);
            SetUtc(2026, 11, 8, 12, 0); // 일요일
            Assert.AreEqual(7, DailyGift.Today[0].amount);

            var kst = new DailyClock(Data, Clock, TimeZoneInfo.CreateCustomTimeZone("KST9", TimeSpan.FromHours(9), "KST9", "KST9"));
            var kstGift = new DailyGiftSystem(Data, MetaDb, kst, MetaRewards, Features);
            SetUtc(2026, 11, 8, 19, 59); // KST 월 04:59 = 일요일
            Assert.AreEqual(7, kstGift.Today[0].amount);
            SetUtc(2026, 11, 8, 20, 0);  // KST 월 05:00
            Assert.AreEqual(1, kstGift.Today[0].amount);
        }

        [Test]
        public void LockedBeforeShop()
        {
            AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            MetaDb.dailyGifts = Rotation();
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            DailyClock.Refresh();
            SetCleared(7);
            Features.Evaluate();
            Assert.IsFalse(DailyGift.IsUnlocked);
            Assert.IsFalse(DailyGift.CanClaim);
            Assert.IsNull(DailyGift.TryClaim());
        }

        [Test]
        public void Tampered_Blocks()
        {
            SetupGift();
            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(DailyGift.CanClaim);
            Assert.IsNull(DailyGift.TryClaim());
        }

        [Test]
        public void RedDot_ShopFree_FollowsClaim()
        {
            SetupGift();
            MetaSession.Start();
            RedDots.Refresh();
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.ShopFree));
            DailyGift.TryClaim();
            RedDots.Refresh();
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.ShopFree));
        }

        [Test]
        public void EmptyContent_NothingToClaim()
        {
            Assert.IsTrue(DailyGift.IsUnlocked, "정의 없는 상점 = 열림");
            Assert.IsFalse(DailyGift.CanClaim);
            Assert.IsEmpty(DailyGift.Today);
        }

        [Test]
        public void Claim_WritesOneDailyGiftPlayLogLine()
        {
            SetupGift(); // 수요일 = 2
            var lines = PlayLogLines("daily_gift", () =>
            {
                Assert.IsNotNull(DailyGift.TryClaim());
                Assert.IsNull(DailyGift.TryClaim(), "두 번째는 받지 못함 → 줄 없음");
            });
            Assert.AreEqual(1, lines.Count, "받을 때 한 줄 (SPEC4 §13.7)");
            Assert.AreEqual(2, lines[0].Value<int>("weekday"));
            Assert.AreEqual(NowUnix, lines[0].Value<long>("ts"));
            Assert.DoesNotThrow(() => DailyGift.TryClaim(), "기록이 닫혀 있어도 그대로");
        }

        /// <summary>플레이 기록을 임시 폴더에 열고 body를 돌린 뒤 ev가 같은 줄들을 읽는다 (기록은 늘 닫고 폴더를 지운다)</summary>
        List<JObject> PlayLogLines(string ev, Action body)
        {
            string dir = Path.Combine(Path.GetTempPath(), "idle_p42_log_" + Guid.NewGuid().ToString("N"));
            var lines = new List<JObject>();
            PlayLog.Close();
            try
            {
                Assert.IsTrue(PlayLog.Open(dir, Clock));
                body();
                PlayLog.Flush();
                string path = Path.Combine(dir, PlayLog.FileName);
                if (File.Exists(path))
                {
                    foreach (var text in File.ReadAllLines(path))
                    {
                        if (text.Length == 0) continue;
                        var line = JObject.Parse(text);
                        if (line.Value<string>("ev") == ev) lines.Add(line);
                    }
                }
            }
            finally
            {
                PlayLog.Close();
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            return lines;
        }
    }
}
