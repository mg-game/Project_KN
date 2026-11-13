using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 이벤트 달력 (EVT-01, SPEC4 §8.3) — 결정적 일정을 UTC 시계와 +09:00 시계 둘 다로: 주말 경계 05:00 (금 04:59 아님 · 토 05:00 맞음),
    /// 매달 1~14일 (양끝 포함, 날 키), 밤 창이 자정을 넘음 (20:59 아님 · 21:00 · 01:59 맞음 · 02:00 아님), 날을 넘는 겹친 시간,
    /// 회차 기준값은 한 번, 미션은 목표에서 자름, 도장 하루 1개 · 최대 7, 되돌림은 받기를 막음, 시작 카드는 회차마다 한 번, 잠기면 효과 없음.
    /// 2026-11-04는 수요일 (11-07 토, 11-08 일, 11-09 월).
    /// </summary>
    public class EventCalendarTests : MetaSystemsTestBase
    {
        const string WeekendId = "evt_weekend_expedition";
        const string WednesdayId = "evt_wednesday_dungeon";
        const string StarlitId = "evt_starlit_walk";
        const string NightId = "evt_night_shop";

        static MetaReward Candy(double amount) => MetaReward.Currency(CurrencyType.EventToken, amount);

        static List<EventDef> Content()
        {
            var stamps = new List<EventStampDef>();
            for (int day = 1; day <= 7; day++) stamps.Add(new EventStampDef { day = day, rewards = new List<MetaReward> { Candy(10 * day) } });
            return new List<EventDef>
            {
                new EventDef
                {
                    id = WeekendId, name = "주말 원정 축제", kind = EventKind.DropBoost, scheduleType = EventScheduleType.Weekdays,
                    weekdayMask = (1 << 5) | (1 << 6), multiplier = 2,
                },
                new EventDef
                {
                    id = WednesdayId, name = "수요 던전 데이", kind = EventKind.TicketBonus, scheduleType = EventScheduleType.Weekdays,
                    weekdayMask = 1 << 2, bonus = 2,
                },
                new EventDef
                {
                    id = StarlitId, name = "별이 머문 밤 산책", kind = EventKind.Festival, scheduleType = EventScheduleType.MonthDays,
                    fromDay = 1, toDay = 14, multiplier = 2, startCard = true,
                    missions = new List<EventMissionDef>
                    {
                        new EventMissionDef { id = "sw_stage", text = "스테이지 50번 클리어", counter = CounterKey.StageClear, target = 50, rewards = { Candy(60) } },
                        new EventMissionDef { id = "sw_lamp", text = "불 밝히기 30번", counter = CounterKey.LampLit, target = 30, rewards = { Candy(40) } },
                        new EventMissionDef { id = "sw_bonus", text = "미션 2개 모두 받기", isBonus = true, rewards = { MetaReward.Gem(100), Candy(100) } },
                    },
                    stamps = stamps,
                },
                new EventDef
                {
                    id = NightId, name = "밤의 수선점", kind = EventKind.NightBonus, scheduleType = EventScheduleType.DailyHours,
                    fromHour = 21, toHour = 2, multiplier = 1.2,
                },
            };
        }

        EventDef Def(string id) => MetaDb.GetEvent(id);

        void SetupEvents()
        {
            MetaDb.events = Content();
            Rebuild();
        }

        static TimeZoneInfo Kst() => TimeZoneInfo.CreateCustomTimeZone("KST9", TimeSpan.FromHours(9), "KST9", "KST9");

        /// <summary>+09:00 시계로 만든 이벤트 달력 (같은 데이터 · 같은 시스템, 시계만 다름)</summary>
        EventCalendarSystem CreateKst(out DailyClock clock)
        {
            clock = new DailyClock(Data, Clock, Kst());
            return new EventCalendarSystem(Data, MetaDb, clock, Progress, MetaRewards, Features, Popups, Battle, Expedition, Dungeon, IdleChest);
        }

        static long Unix(int year, int month, int day, int hour, int minute = 0) =>
            TimeUtil.ToUnix(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc));

        // ───────────── 일정 ─────────────

        [Test]
        public void Weekend_StartsSaturdayAtFive_Utc()
        {
            SetupEvents();
            SetUtc(2026, 11, 7, 4, 59);
            Assert.AreEqual(1.0, Events.ExpeditionDropMultiplier, "토 04:59 = 아직 금요일");
            SetUtc(2026, 11, 7, 5, 0);
            Assert.AreEqual(2.0, Events.ExpeditionDropMultiplier, "토 05:00 = 주말");
            SetUtc(2026, 11, 9, 4, 59);
            Assert.AreEqual(2.0, Events.ExpeditionDropMultiplier, "월 04:59 = 아직 일요일");
            SetUtc(2026, 11, 9, 5, 0);
            Assert.AreEqual(1.0, Events.ExpeditionDropMultiplier);
        }

        [Test]
        public void Weekend_StartsSaturdayAtFive_PlusNine()
        {
            SetupEvents();
            var kst = CreateKst(out _);
            SetUtc(2026, 11, 6, 19, 59); // KST 토 04:59
            Assert.AreEqual(1.0, kst.ExpeditionDropMultiplier, "KST 토 04:59 = 금요일");
            SetUtc(2026, 11, 6, 20, 0);  // KST 토 05:00
            Assert.AreEqual(2.0, kst.ExpeditionDropMultiplier, "KST 토 05:00 = 주말");
            Assert.AreEqual(1.0, Events.ExpeditionDropMultiplier, "같은 순간 UTC 시계는 아직 금요일");
            SetUtc(2026, 11, 8, 19, 59); // KST 월 04:59
            Assert.AreEqual(2.0, kst.ExpeditionDropMultiplier);
            SetUtc(2026, 11, 8, 20, 0);
            Assert.AreEqual(1.0, kst.ExpeditionDropMultiplier);
        }

        [Test]
        public void Wednesday_AddsTwoTickets_Utc_AndPlusNine()
        {
            SetupEvents();
            SetUtc(2026, 11, 4, 9, 0);
            Assert.AreEqual(2, Events.BonusDungeonTickets);
            SetUtc(2026, 11, 5, 4, 59);
            Assert.AreEqual(2, Events.BonusDungeonTickets, "목 04:59 = 아직 수요일");
            SetUtc(2026, 11, 5, 5, 0);
            Assert.AreEqual(0, Events.BonusDungeonTickets);

            var kst = CreateKst(out _);
            SetUtc(2026, 11, 3, 19, 59); // KST 수 04:59
            Assert.AreEqual(0, kst.BonusDungeonTickets);
            SetUtc(2026, 11, 3, 20, 0);
            Assert.AreEqual(2, kst.BonusDungeonTickets);
        }

        [Test]
        public void MonthDays_OneToFourteenInclusive_ByDayKey()
        {
            SetupEvents();
            SetUtc(2026, 12, 1, 4, 59);
            Assert.AreEqual(1.0, Events.StageCandyMultiplier, "12-01 04:59 = 11-30");
            SetUtc(2026, 12, 1, 5, 0);
            Assert.AreEqual(2.0, Events.StageCandyMultiplier, "1일");
            SetUtc(2026, 12, 15, 4, 59);
            Assert.AreEqual(2.0, Events.StageCandyMultiplier, "14일 (양끝 포함)");
            SetUtc(2026, 12, 15, 5, 0);
            Assert.AreEqual(1.0, Events.StageCandyMultiplier, "15일");

            var kst = CreateKst(out _);
            SetUtc(2026, 11, 30, 19, 59); // KST 12-01 04:59
            Assert.AreEqual(1.0, kst.StageCandyMultiplier);
            SetUtc(2026, 11, 30, 20, 0);
            Assert.AreEqual(2.0, kst.StageCandyMultiplier);
        }

        [Test]
        public void NightWindow_WrapsMidnight_Utc()
        {
            SetupEvents();
            var night = Def(NightId);
            SetUtc(2026, 11, 4, 20, 59);
            Assert.IsFalse(Events.IsActiveNow(night), "20:59");
            SetUtc(2026, 11, 4, 21, 0);
            Assert.IsTrue(Events.IsActiveNow(night), "21:00");
            SetUtc(2026, 11, 5, 1, 59);
            Assert.IsTrue(Events.IsActiveNow(night), "01:59");
            SetUtc(2026, 11, 5, 2, 0);
            Assert.IsFalse(Events.IsActiveNow(night), "02:00");
            Assert.IsTrue(EventCalendarSystem.InHourWindow(23, 21, 2));
            Assert.IsFalse(EventCalendarSystem.InHourWindow(12, 21, 2));
            Assert.IsTrue(EventCalendarSystem.InHourWindow(9, 9, 17));
            Assert.IsFalse(EventCalendarSystem.InHourWindow(17, 9, 17));
        }

        [Test]
        public void NightWindow_WrapsMidnight_PlusNine()
        {
            SetupEvents();
            var kst = CreateKst(out _);
            var night = Def(NightId);
            SetUtc(2026, 11, 4, 11, 59); // KST 20:59
            Assert.IsFalse(kst.IsActiveNow(night));
            SetUtc(2026, 11, 4, 12, 0);  // KST 21:00
            Assert.IsTrue(kst.IsActiveNow(night));
            SetUtc(2026, 11, 4, 16, 59); // KST 01:59
            Assert.IsTrue(kst.IsActiveNow(night));
            SetUtc(2026, 11, 4, 17, 0);  // KST 02:00
            Assert.IsFalse(kst.IsActiveNow(night));
        }

        [Test]
        public void OverlapMath_AcrossDays()
        {
            SetupEvents();
            var night = Def(NightId);
            Assert.AreEqual(5.0, Events.OverlapHours(night, Unix(2026, 11, 4, 20), Unix(2026, 11, 5, 3)), 1e-9, "21~02 = 5시간");
            Assert.AreEqual(10.0, Events.OverlapHours(night, Unix(2026, 11, 4, 22), Unix(2026, 11, 6, 22)), 1e-9, "4 + 5 + 1");
            Assert.AreEqual(0.0, Events.OverlapHours(night, Unix(2026, 11, 5, 3), Unix(2026, 11, 5, 20)), 1e-9);
            Assert.AreEqual(0.5, Events.OverlapHours(night, Unix(2026, 11, 5, 1, 30), Unix(2026, 11, 5, 4)), 1e-9);
            Assert.AreEqual(5.0, Events.NightBonusHours(Unix(2026, 11, 4, 20), Unix(2026, 11, 5, 3)), 1e-9, "순수 겹침 5시간 (SPEC4 §8.3 — 덤 배율은 바구니가 곱한다)");
            Assert.AreEqual(0.5, Events.NightBonusHours(Unix(2026, 11, 5, 1, 30), Unix(2026, 11, 5, 4)), 1e-9);
            Assert.AreEqual(1.2, Events.NightBonusMultiplier, 1e-9, "밤 바구니 배율 (켜졌는지와 무관한 콘텐츠 값)");

            var starlit = Def(StarlitId);
            Assert.AreEqual(9.0, Events.OverlapHours(starlit, Unix(2026, 11, 14, 20), Unix(2026, 11, 15, 8)), 1e-9, "14일은 15일 05:00까지");
            Assert.AreEqual(9.0, Events.EventLineHours(Unix(2026, 11, 14, 20), Unix(2026, 11, 15, 8)), 1e-9);
            Assert.AreEqual(48.0, Events.OverlapHours(Def(WeekendId), Unix(2026, 11, 7, 5), Unix(2026, 11, 9, 5)), 1e-9, "토 05:00 ~ 월 05:00");

            var kst = CreateKst(out _);
            // KST 11-04 20:00 ~ 11-05 03:00 = UTC 11:00 ~ 18:00 → 밤 창 5시간
            Assert.AreEqual(5.0, kst.OverlapHours(night, Unix(2026, 11, 4, 11), Unix(2026, 11, 4, 18)), 1e-9);
            Assert.AreEqual(0.0, kst.OverlapHours(night, Unix(2026, 11, 4, 20), Unix(2026, 11, 5, 3)), 1e-9, "UTC 밤 = KST 아침");
        }

        [Test]
        public void Locked_NoEffects_NoInstances()
        {
            MetaDb.events = Content();
            AddFeature(FeatureId.Events, MetaCondition.StageCleared(3, 1));
            Rebuild();
            SetUtc(2026, 11, 7, 22, 0); // 토요일 밤, 별빛 산책 기간
            Features.Evaluate();
            Assert.IsFalse(Events.IsUnlocked);
            Events.Connect();
            Events.AfterStartup(true);
            Assert.AreEqual(1.0, Events.ExpeditionDropMultiplier);
            Assert.AreEqual(1.0, Events.StageCandyMultiplier);
            Assert.AreEqual(0, Events.BonusDungeonTickets);
            Assert.AreEqual(0.0, Events.NightBonusHours(Unix(2026, 11, 7, 20), Unix(2026, 11, 8, 3)));
            Assert.AreEqual(1.0, Events.NightBonusMultiplier);
            Assert.AreEqual(0.0, Events.EventLineHours(Unix(2026, 11, 7, 0), Unix(2026, 11, 8, 0)));
            var list = new List<ActiveEventStatus>();
            Events.GetActive(list);
            Assert.IsEmpty(list);
            Assert.IsEmpty(Data.meta.events.instances);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.EventStart));
            Assert.IsFalse(Events.HasClaimable);
        }

        [Test]
        public void EmptyContent_IsOff()
        {
            Assert.IsFalse(Events.IsUnlocked, "이벤트 콘텐츠가 없으면 꺼짐");
            Assert.AreEqual(1.0, Events.ExpeditionDropMultiplier);
            Assert.DoesNotThrow(() =>
            {
                Events.Connect();
                Events.AfterStartup(true);
                Events.OnDayChanged("2026-11-04");
                Events.Tick(2f);
            });
        }

        // ───────────── 회차 · 미션 · 도장 · 카드 ─────────────

        [Test]
        public void InstanceKeys_WeekendSaturday_MonthlyFirst_NightNone()
        {
            SetupEvents();
            SetUtc(2026, 11, 8, 10, 0); // 일요일
            Assert.AreEqual(WeekendId + "@2026-11-07", Events.GetInstanceKey(WeekendId), "주말 회차 = 토요일 날 키");
            Assert.AreEqual(StarlitId + "@2026-11-01", Events.GetInstanceKey(StarlitId), "매달 1일");
            Assert.IsNull(Events.GetInstanceKey(NightId), "밤 이벤트는 회차 없음");
            Assert.IsNull(Events.GetInstanceKey(WednesdayId), "오늘 아님");
            SetUtc(2026, 11, 4, 10, 0);
            Assert.AreEqual(WednesdayId + "@2026-11-04", Events.GetInstanceKey(WednesdayId));
        }

        [Test]
        public void Baseline_CapturedOncePerInstance_MissionsCappedAndClaimedOnce()
        {
            SetupEvents();
            SetUtc(2026, 11, 3, 9, 0);
            DailyClock.Refresh();
            Progress.Increment(CounterKey.StageClear, 30); // 회차 전 진행은 세지 않는다
            Events.Connect();
            string key = Events.GetInstanceKey(StarlitId);
            Assert.IsTrue(Data.meta.events.instances.ContainsKey(key));
            Assert.AreEqual(30, Data.meta.events.instances[key].baseline[CounterKey.StageClear]);

            var missions = new List<EventMissionStatus>();
            Events.GetMissions(key, missions);
            Assert.AreEqual(3, missions.Count);
            Assert.AreEqual(0, missions[0].Progress);

            Progress.Increment(CounterKey.StageClear, 70);
            Events.OnDayChanged(DailyClock.DayKey); // 다시 훑어도 기준값은 그대로
            Assert.AreEqual(30, Data.meta.events.instances[key].baseline[CounterKey.StageClear]);
            Events.GetMissions(key, missions);
            Assert.AreEqual(50, missions[0].Progress, "목표에서 자름");
            Assert.IsTrue(missions[0].CanClaim);
            Assert.IsFalse(missions[2].IsComplete, "보너스는 다른 미션을 다 받아야");
            Assert.IsTrue(Events.HasClaimable);

            var granted = Events.TryClaimMission(key, "sw_stage");
            Assert.IsNotNull(granted);
            Assert.AreEqual(60, AmountOf(granted, MetaRewardType.Currency, CurrencyType.EventToken));
            Assert.IsNull(Events.TryClaimMission(key, "sw_stage"), "한 번만");
            Assert.IsNull(Events.TryClaimMission(key, "sw_lamp"), "아직 안 함");
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.EventMissionClaim));

            Progress.Increment(CounterKey.LampLit, 30);
            Assert.IsNotNull(Events.TryClaimMission(key, "sw_lamp"));
            Events.GetMissions(key, missions);
            Assert.IsTrue(missions[2].CanClaim, "보너스 완료");
            var bonus = Events.TryClaimMission(key, "sw_bonus");
            Assert.AreEqual(100, AmountOf(bonus, MetaRewardType.Currency, CurrencyType.Gem));
        }

        [Test]
        public void Stamps_OnePerDay_SevenMax()
        {
            SetupEvents();
            SetUtc(2026, 11, 3, 9, 0);
            DailyClock.Refresh();
            Events.Connect();
            string key = Events.GetInstanceKey(StarlitId);
            for (int day = 1; day <= 7; day++)
            {
                Assert.IsTrue(Events.CanStamp(key), "날 " + day);
                var granted = Events.TryStamp(key);
                Assert.AreEqual(10 * day, AmountOf(granted, MetaRewardType.Currency, CurrencyType.EventToken));
                Assert.IsFalse(Events.CanStamp(key), "하루 하나");
                Assert.IsNull(Events.TryStamp(key));
                Advance(24);
                DailyClock.Refresh();
            }
            Assert.AreEqual(7, Events.GetStampCount(key));
            Assert.AreEqual(7, Events.GetStampsTaken(key));
            Assert.IsFalse(Events.CanStamp(key), "최대 7");
        }

        [Test]
        public void StampsTaken_ClampedToMaxStamps_ReadOnly_NullSafe()
        {
            SetupEvents();
            SetUtc(2026, 11, 3, 9, 0);
            DailyClock.Refresh();
            Events.Connect();
            string key = Events.GetInstanceKey(StarlitId);
            Assert.AreEqual(0, Events.GetStampsTaken(key), "아직 안 찍음");
            Assert.IsNotNull(Events.TryStamp(key));
            Assert.AreEqual(1, Events.GetStampsTaken(key));
            Assert.AreEqual(Events.GetStampCount(key), Events.GetStampsTaken(key));

            var instance = Data.meta.events.instances[key];
            instance.stamps = 99;
            Assert.AreEqual(EventCalendarSystem.MaxStamps, Events.GetStampsTaken(key), "망가진 저장은 칸 수에서 자름");
            instance.stamps = -3;
            Assert.AreEqual(0, Events.GetStampsTaken(key));

            int before = Data.meta.events.instances.Count;
            Assert.AreEqual(0, Events.GetStampsTaken(null));
            Assert.AreEqual(0, Events.GetStampsTaken(""));
            Assert.AreEqual(0, Events.GetStampsTaken("evt_none@2026-11-01"));
            Assert.AreEqual(before, Data.meta.events.instances.Count, "읽기만 — 회차를 만들지 않음");

            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            instance.stamps = 2;
            Assert.AreEqual(2, Events.GetStampsTaken(key), "되돌림이어도 찍은 칸은 보여 줌");

            Data.meta.events = null;
            Assert.AreEqual(0, Events.GetStampsTaken(key), "저장이 없으면 0");
            Assert.IsNull(Data.meta.events, "저장을 만들지 않음");
        }

        [Test]
        public void Claims_OnlyForTodaysInstance()
        {
            SetupEvents();
            SetUtc(2026, 11, 14, 9, 0);
            DailyClock.Refresh();
            Events.Connect();
            string key = Events.GetInstanceKey(StarlitId);
            Assert.IsTrue(Events.CanStamp(key));
            Advance(24); // 15일 → 회차 끝
            DailyClock.Refresh();
            Assert.IsNull(Events.GetInstanceKey(StarlitId));
            Assert.IsFalse(Events.CanStamp(key), "끝난 회차는 찍지 않는다");
            Assert.IsNull(Events.TryStamp(key));
        }

        [Test]
        public void Tamper_BlocksClaims()
        {
            SetupEvents();
            SetUtc(2026, 11, 3, 9, 0);
            DailyClock.Refresh();
            Events.Connect();
            string key = Events.GetInstanceKey(StarlitId);
            Progress.Increment(CounterKey.StageClear, 50);
            Assert.IsTrue(Events.HasClaimable);

            Data.meta.clock.maxSeenUnix = NowUnix + 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(Events.CanStamp(key));
            Assert.IsNull(Events.TryStamp(key));
            Assert.IsNull(Events.TryClaimMission(key, "sw_stage"));
            Assert.IsFalse(Events.HasClaimable);
        }

        [Test]
        public void StartCard_QueuedOncePerInstance()
        {
            SetupEvents();
            SetUtc(2026, 11, 3, 9, 0);
            DailyClock.Refresh();
            Events.Connect();
            Events.AfterStartup(true);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.EventStart));
            string key = Events.GetInstanceKey(StarlitId);
            Assert.IsTrue(Data.meta.events.instances[key].startCardShown);

            Popups.Remove(MetaPopupKind.EventStart);
            Events.AfterStartup(false);
            Events.OnDayChanged(DailyClock.DayKey);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.EventStart), "회차마다 한 번");

            SetUtc(2026, 12, 2, 9, 0);
            DailyClock.Refresh();
            Events.OnDayChanged(DailyClock.DayKey);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.EventStart), "다음 달 새 회차");
        }

        [Test]
        public void Seen_ClearsNewDot()
        {
            SetupEvents();
            SetUtc(2026, 11, 7, 9, 0);
            DailyClock.Refresh();
            Events.Connect();
            Assert.IsTrue(Events.HasUnseen);
            Events.MarkSeen(Events.GetInstanceKey(WeekendId));
            Assert.IsTrue(Events.HasUnseen, "별빛 산책 회차는 아직");
            Events.MarkSeen(null);
            Assert.IsFalse(Events.HasUnseen);
        }

        [Test]
        public void GetActive_And_GetAll_ListEffectsAndTimes()
        {
            SetupEvents();
            SetUtc(2026, 11, 7, 10, 0); // 토요일 10:00
            DailyClock.Refresh();
            var list = new List<ActiveEventStatus>();
            Events.GetActive(list);
            Assert.AreEqual(2, list.Count, "주말 + 별빛 산책");
            var weekend = list.Find(s => s.Def.id == WeekendId);
            Assert.AreEqual("원정 재료 ×2", weekend.EffectLabel);
            Assert.AreEqual(TimeSpan.FromHours(43), weekend.TimeLeft, "월 05:00까지");
            Assert.AreEqual(WeekendId + "@2026-11-07", weekend.InstanceKey);
            var starlit = list.Find(s => s.Def.id == StarlitId);
            Assert.AreEqual("별빛 사탕 ×2", starlit.EffectLabel);
            Assert.AreEqual(TimeSpan.FromHours(7 * 24 + 19), starlit.TimeLeft, "15일 05:00까지");

            Events.GetAll(list);
            Assert.AreEqual(4, list.Count);
            var wednesday = list.Find(s => s.Def.id == WednesdayId);
            Assert.IsFalse(wednesday.IsActive);
            Assert.AreEqual("던전 입장권 +2", wednesday.EffectLabel);
            Assert.AreEqual(Unix(2026, 11, 11, 5), wednesday.NextStartUnix);
            Assert.AreEqual("수요일 새벽 5시부터", wednesday.NextStartText);
            var night = list.Find(s => s.Def.id == NightId);
            Assert.AreEqual("밤 9시~새벽 2시 바구니 +20%", night.EffectLabel);
            Assert.AreEqual("밤 9시부터", night.NextStartText);
            Assert.AreEqual(Unix(2026, 11, 7, 21), night.NextStartUnix);
        }

        [Test]
        public void Hooks_WiredOnConnect_ToExpeditionDungeonBattleBasket()
        {
            SetupEvents();
            SetUtc(2026, 11, 4, 9, 0); // 수요일, 별빛 산책 기간
            DailyClock.Refresh();
            Events.Connect();
            Assert.AreEqual(1.0, Expedition.DropMultiplier());
            Assert.AreEqual(2, Dungeon.BonusTickets());
            Assert.AreEqual(5, Dungeon.TicketsLeft, "수요 던전 데이 3 + 2");
            Assert.AreEqual(2.0, Battle.CandyMultiplier());
            Assert.IsNotNull(IdleChest.NightBonusHours);
            Assert.IsNotNull(IdleChest.NightBonusMultiplier);
            Assert.AreEqual(1.2, IdleChest.NightBonusMultiplier(), 1e-9, "낮에도 콘텐츠 배율 (지난 밤 구간에 쓴다)");
            Assert.AreEqual(5.0, IdleChest.NightBonusHours(Unix(2026, 11, 4, 20), Unix(2026, 11, 5, 3)), 1e-9, "바구니 훅도 순수 겹침");
            Assert.IsNotNull(IdleChest.EventLineHours);
            SetUtc(2026, 11, 7, 9, 0);
            Assert.AreEqual(2.0, Expedition.DropMultiplier());
        }

        [Test]
        public void NightShop_AddsTwentyPercentToBasket()
        {
            MetaDb.events = Content();
            MetaDb.idleChest = new IdleChestConfig
            {
                baseCapHours = 12f,
                capSteps = new List<IdleCapStep>(),
                lines = new List<IdleChestLine> { new IdleChestLine { reward = MetaReward.Gem(3) } },
                stageGrowth = 1.0,
                minClaimSeconds = 60,
            };
            Rebuild();
            SetUtc(2026, 11, 16, 4, 0); // 별빛 산책 밖
            Data.meta.idleChest.lastClaimUnix = Unix(2026, 11, 15, 20);
            Events.Connect();
            var pending = new List<MetaReward>();
            IdleChest.GetPending(pending);
            Assert.AreEqual(3 * (8 + 1), AmountOf(pending, MetaRewardType.Currency, CurrencyType.Gem), "8시간 + (21~02 = 5시간 × 20%)");
        }

        [Test]
        public void MissionAndStampClaims_WriteEventClaimPlayLogLines()
        {
            SetupEvents();
            SetUtc(2026, 11, 3, 9, 0);
            DailyClock.Refresh();
            Events.Connect();
            string key = Events.GetInstanceKey(StarlitId);
            Progress.Increment(CounterKey.StageClear, 50);
            var lines = PlayLogLines("event_claim", () =>
            {
                Assert.IsNotNull(Events.TryClaimMission(key, "sw_stage"));
                Assert.IsNull(Events.TryClaimMission(key, "sw_stage"), "다시 받지 못함 → 줄 없음");
                Assert.IsNull(Events.TryClaimMission(key, "sw_lamp"), "아직 안 함 → 줄 없음");
                Assert.IsNotNull(Events.TryStamp(key));
                Assert.IsNull(Events.TryStamp(key), "하루 하나 → 줄 없음");
            });
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(StarlitId, lines[0].Value<string>("id"));
            Assert.AreEqual("sw_stage", lines[0].Value<string>("mission"));
            Assert.AreEqual(StarlitId, lines[1].Value<string>("id"));
            Assert.AreEqual(1, lines[1].Value<int>("stamp"));
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
