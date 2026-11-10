using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 로컬 알림 계획 (NOTI-01, SPEC4 §4.3): 조용한 시간 옮기기, 3개 · 30분 간격, 우선순위, 15분 안 건너뛰기(바구니 · 원정만), 20시 뒤 출석은 내일,
    /// 던전은 안 썼을 때만(모아 두기면 bank+3 ≥ cap), 튜토리얼 전 · 꺼진 종류 · 이미 가득 찬 바구니는 없음. 창구(LocalNotifications)는 예외를 내지 않는다.
    /// </summary>
    public class NotificationPlannerTests
    {
        static readonly DateTime Noon = new(2026, 11, 5, 12, 0, 0);

        static NotificationContext Context(DateTime now)
        {
            return new NotificationContext
            {
                NowLocal = now,
                FtueDone = true,
                IdleChestEnabled = true,
                ExpeditionEnabled = true,
                DungeonEnabled = true,
                AttendanceEnabled = true,
                NextResetLocal = now.Date.AddDays(now.Hour >= 5 ? 1 : 0).AddHours(5),
            };
        }

        static List<PlannedNotification> Plan(in NotificationContext context)
        {
            var list = new List<PlannedNotification> { new PlannedNotification { Id = -1 } };
            NotificationPlanner.Plan(in context, list);
            return list;
        }

        [Test]
        public void BasketFull_SchedulesAtFullTime_WithSpecCopy()
        {
            var context = Context(Noon);
            context.BasketFullLocal = Noon.AddHours(3);

            var plan = Plan(in context);

            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(NotificationKind.IdleChestFull, plan[0].Kind);
            Assert.AreEqual(1001, plan[0].Id);
            Assert.AreEqual(Noon.AddHours(3), plan[0].FireLocal);
            Assert.AreEqual("수선 바구니가 가득 찼어요", plan[0].Title);
            Assert.AreEqual("친구들이 모은 선물이 기다려요. 받으러 와 주세요!", plan[0].Body);
        }

        [Test]
        public void QuietHours_ShiftTo8AM()
        {
            var context = Context(Noon);
            context.BasketFullLocal = new DateTime(2026, 11, 5, 23, 30, 0);
            context.LastExpeditionDoneLocal = new DateTime(2026, 11, 6, 3, 0, 0);

            var plan = Plan(in context);

            Assert.AreEqual(new DateTime(2026, 11, 6, 8, 0, 0), plan[0].FireLocal, "23:30 → 다음 날 08:00");
            Assert.AreEqual(new DateTime(2026, 11, 6, 8, 30, 0), plan[1].FireLocal, "03:00 → 08:00, 앞 알림과 30분 띄움");
        }

        [TestCase(21, 59, 21, 59)]
        [TestCase(22, 0, 8, 0)]
        [TestCase(7, 59, 8, 0)]
        [TestCase(8, 0, 8, 0)]
        [TestCase(0, 0, 8, 0)]
        public void ShiftOutOfQuietHours_Boundaries(int hour, int minute, int expectedHour, int expectedMinute)
        {
            var local = new DateTime(2026, 11, 5, hour, minute, 0);
            var shifted = NotificationPlanner.ShiftOutOfQuietHours(local);

            Assert.AreEqual(expectedHour, shifted.Hour);
            Assert.AreEqual(expectedMinute, shifted.Minute);
            Assert.AreEqual(hour >= 22 ? 6 : 5, shifted.Day);
        }

        [Test]
        public void AtMostThree_DropsLowestPriority_AndSpaces30Minutes()
        {
            var context = Context(Noon);
            context.BasketFullLocal = Noon.AddHours(2);
            context.LastExpeditionDoneLocal = Noon.AddHours(2);
            context.AttendancePending = true; // 20:00
            context.DungeonUnusedToday = true; // 내일 05:00 → 08:00

            var plan = Plan(in context);

            Assert.AreEqual(NotificationPlanner.MaxPending, plan.Count);
            CollectionAssert.AreEqual(new[] { 1001, 1002, 1004 }, plan.Select(p => p.Id).ToArray(), "던전(가장 낮은 우선순위)이 빠진다");
            Assert.AreEqual(Noon.AddHours(2), plan[0].FireLocal, "같은 시각이면 우선순위 높은 쪽이 그대로");
            Assert.AreEqual(Noon.AddHours(2).AddMinutes(30), plan[1].FireLocal, "뒤 것은 30분 밀린다");
            Assert.AreEqual(Noon.Date.AddHours(20), plan[2].FireLocal);
            for (int i = 1; i < plan.Count; i++)
                Assert.GreaterOrEqual((plan[i].FireLocal - plan[i - 1].FireLocal).TotalMinutes, NotificationPlanner.MinSpacingMinutes);
        }

        [Test]
        public void Priority_DecidesWhichThreeSurvive_EvenWhenLowerOneIsEarlier()
        {
            var context = Context(new DateTime(2026, 11, 5, 4, 0, 0));
            context.NextResetLocal = new DateTime(2026, 11, 5, 5, 0, 0);
            context.DungeonUnusedToday = true; // 05:00 → 08:00 (가장 이름)
            context.BasketFullLocal = new DateTime(2026, 11, 5, 10, 0, 0);
            context.LastExpeditionDoneLocal = new DateTime(2026, 11, 5, 11, 0, 0);
            context.AttendancePending = true; // 20:00

            var plan = Plan(in context);

            CollectionAssert.AreEquivalent(new[] { 1001, 1002, 1004 }, plan.Select(p => p.Id).ToArray());
        }

        [Test]
        public void SpacingPush_IntoQuietHours_MovesTo8AM()
        {
            var context = Context(Noon);
            context.BasketFullLocal = new DateTime(2026, 11, 5, 21, 40, 0);
            context.LastExpeditionDoneLocal = new DateTime(2026, 11, 5, 21, 50, 0);

            var plan = Plan(in context);

            Assert.AreEqual(new DateTime(2026, 11, 5, 21, 40, 0), plan[0].FireLocal);
            Assert.AreEqual(new DateTime(2026, 11, 6, 8, 0, 0), plan[1].FireLocal, "22:10은 조용한 시간 → 08:00");
        }

        [Test]
        public void WithinFifteenMinutes_IsSkipped()
        {
            var context = Context(Noon);
            context.BasketFullLocal = Noon.AddMinutes(14);
            context.LastExpeditionDoneLocal = Noon.AddMinutes(15);

            var plan = Plan(in context);

            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(NotificationKind.ExpeditionDone, plan[0].Kind, "15분 뒤는 넣고 14분 뒤는 뺀다");
        }

        [Test]
        public void JustBeforeQuietHours_FiresNextMorning_InsteadOfBeingSkipped()
        {
            var context = Context(new DateTime(2026, 11, 5, 21, 55, 0));
            context.BasketFullLocal = new DateTime(2026, 11, 5, 22, 5, 0);

            var plan = Plan(in context);

            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(new DateTime(2026, 11, 6, 8, 0, 0), plan[0].FireLocal);
        }

        [Test]
        public void Attendance_Today8PM_OrTomorrowAfter8PM()
        {
            var before = Context(Noon);
            before.AttendancePending = true;
            var after = Context(new DateTime(2026, 11, 5, 20, 30, 0));
            after.AttendancePending = true;

            Assert.AreEqual(new DateTime(2026, 11, 5, 20, 0, 0), Plan(in before).Single().FireLocal);
            var tomorrow = Plan(in after).Single();
            Assert.AreEqual(new DateTime(2026, 11, 6, 20, 0, 0), tomorrow.FireLocal);
            Assert.AreEqual(1004, tomorrow.Id);
            Assert.AreEqual("오늘 출석 도장을 아직 안 찍었어요", tomorrow.Title);
        }

        [Test]
        public void Attendance_NotPending_NoNotification()
        {
            var context = Context(Noon);
            context.AttendancePending = false;

            Assert.IsEmpty(Plan(in context));
        }

        [Test]
        public void Attendance_At1950_StillFiresAt8PM_TheFifteenMinuteRuleIsOnlyForBasketAndExpedition()
        {
            // SPEC4 §4.3 표: '≥15 min ahead'는 바구니 · 원정에만. 출석은 '오늘 20:00 (20시가 지났으면 내일)'
            var context = Context(new DateTime(2026, 11, 5, 19, 50, 0));
            context.AttendancePending = true;
            context.BasketFullLocal = new DateTime(2026, 11, 5, 20, 0, 0); // 10분 뒤 → 뺀다

            var plan = Plan(in context).Single();

            Assert.AreEqual(NotificationKind.Attendance, plan.Kind);
            Assert.AreEqual(new DateTime(2026, 11, 5, 20, 0, 0), plan.FireLocal);
        }

        [Test]
        public void Attendance_JustBefore8PM_IsKept_AndSpacingStillPushesTheNextOne()
        {
            var context = Context(new DateTime(2026, 11, 5, 19, 59, 59));
            context.AttendancePending = true;
            context.LastExpeditionDoneLocal = new DateTime(2026, 11, 5, 20, 15, 0);

            var plan = Plan(in context);

            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(NotificationKind.Attendance, plan[0].Kind);
            Assert.AreEqual(new DateTime(2026, 11, 5, 20, 0, 0), plan[0].FireLocal, "1초 뒤라도 지금보다 뒤면 넣는다");
            Assert.AreEqual(NotificationKind.ExpeditionDone, plan[1].Kind);
            Assert.AreEqual(new DateTime(2026, 11, 5, 20, 30, 0), plan[1].FireLocal, "앞 알림과 30분 띄운다");
        }

        [Test]
        public void Dungeon_HasNoFifteenMinuteRule_ButMustBeAhead()
        {
            var soon = Context(Noon);
            soon.DungeonUnusedToday = true;
            soon.NextResetLocal = Noon.AddMinutes(10);
            var stale = Context(Noon);
            stale.DungeonUnusedToday = true;
            stale.NextResetLocal = Noon;

            Assert.AreEqual(Noon.AddMinutes(10), Plan(in soon).Single().FireLocal);
            Assert.IsEmpty(Plan(in stale), "지금이거나 지난 시각은 넣지 않는다");
        }

        [Test]
        public void Dungeon_OnlyWhenUnusedToday_AtResetShiftedTo8AM()
        {
            var used = Context(Noon);
            used.DungeonUnusedToday = false;
            var unused = Context(Noon);
            unused.DungeonUnusedToday = true;

            Assert.IsEmpty(Plan(in used));
            var plan = Plan(in unused).Single();
            Assert.AreEqual(1003, plan.Id);
            Assert.AreEqual(new DateTime(2026, 11, 6, 8, 0, 0), plan.FireLocal, "05:00 초기화 → 조용한 시간 → 08:00");
            Assert.AreEqual("잔향이 던전 입장권이 채워졌어요", plan.Title);
        }

        [Test]
        public void Dungeon_TicketBank_FiresWhenBankPlusThreeReachesCap()
        {
            var below = Context(Noon);
            below.TicketBankCap = 9;
            below.TicketBank = 5;
            below.DungeonUnusedToday = true; // 모아 두기가 켜지면 이 규칙 대신 bank 규칙
            var reach = Context(Noon);
            reach.TicketBankCap = 9;
            reach.TicketBank = 6;

            Assert.IsEmpty(Plan(in below));
            var plan = Plan(in reach).Single();
            Assert.AreEqual("잔향이 던전 입장권이 가득 찼어요", plan.Title);
            Assert.AreEqual("오늘의 입장권으로 귀한 재료를 찾아요.", plan.Body);
        }

        [Test]
        public void BeforeFtue_NoNotifications()
        {
            var context = Context(Noon);
            context.FtueDone = false;
            context.BasketFullLocal = Noon.AddHours(2);
            context.AttendancePending = true;

            Assert.IsEmpty(Plan(in context));
        }

        [Test]
        public void DisabledKind_NoNotification()
        {
            var context = Context(Noon);
            context.BasketFullLocal = Noon.AddHours(2);
            context.LastExpeditionDoneLocal = Noon.AddHours(3);
            context.IdleChestEnabled = false;

            var plan = Plan(in context);

            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(NotificationKind.ExpeditionDone, plan[0].Kind);
            Assert.IsFalse(context.IsEnabled(NotificationKind.IdleChestFull));
            Assert.IsFalse(context.IsEnabled((NotificationKind)99));
        }

        [Test]
        public void BasketAlreadyFull_NoNotification()
        {
            // Capture는 이미 가득 찬 바구니에 BasketFullLocal을 넣지 않는다 (null) — 지난 시각이 들어와도 15분 규칙으로 빠진다
            var context = Context(Noon);
            context.BasketFullLocal = null;
            Assert.IsEmpty(Plan(in context));
            context.BasketFullLocal = Noon.AddMinutes(-5);
            Assert.IsEmpty(Plan(in context));
        }

        [Test]
        public void Plan_NullList_DoesNotThrow_AndDefaultContextIsEmpty()
        {
            Assert.DoesNotThrow(() => NotificationPlanner.Plan(new NotificationContext(), null));
            var list = new List<PlannedNotification> { new PlannedNotification() };
            NotificationPlanner.Plan(new NotificationContext(), list);
            Assert.IsEmpty(list);
        }

        [Test]
        public void Copy_FollowsTheToneRules()
        {
            var context = Context(Noon);
            context.BasketFullLocal = Noon.AddHours(1);
            context.LastExpeditionDoneLocal = Noon.AddHours(2);
            context.AttendancePending = true;
            var bank = Context(Noon);
            bank.TicketBankCap = 9;
            bank.TicketBank = 9;
            var texts = Plan(in context).Concat(Plan(in bank)).SelectMany(p => new[] { p.Title, p.Body }).ToList();

            Assert.AreEqual(8, texts.Count);
            foreach (var text in texts)
            {
                StringAssert.DoesNotContain("습니다", text);
                StringAssert.DoesNotContain("개방", text);
                StringAssert.DoesNotContain("장비", text);
            }
        }

        // ── 창구 (LocalNotifications) ──

        sealed class FakeBackend : INotificationBackend
        {
            public int Initialized, CancelAlls, Cleared, Settings;
            public readonly List<PlannedNotification> Scheduled = new();
            public bool ThrowOnSchedule;
            public NotificationPermission Current = NotificationPermission.Unknown;

            public bool Initialize()
            {
                Initialized++;
                return true;
            }

            public NotificationPermission Permission => Current;

            public IEnumerator RequestPermission(Action<NotificationPermission> done)
            {
                Current = NotificationPermission.Granted;
                done(Current);
                yield break;
            }

            public void Schedule(in PlannedNotification notification)
            {
                if (ThrowOnSchedule) throw new InvalidOperationException("boom");
                Scheduled.Add(notification);
            }

            public void CancelAll()
            {
                CancelAlls++;
                Scheduled.Clear();
            }

            public void ClearDelivered() => Cleared++;

            public void OpenSystemSettings() => Settings++;
        }

        INotificationBackend _previous;

        [SetUp]
        public void SaveBackend() => _previous = LocalNotifications.Backend;

        [TearDown]
        public void RestoreBackend() => LocalNotifications.Backend = _previous;

        [Test]
        public void Facade_WithoutBackend_DoesNothing()
        {
            LocalNotifications.Backend = null;

            Assert.IsFalse(LocalNotifications.HasBackend);
            Assert.IsFalse(LocalNotifications.Initialize());
            Assert.IsFalse(LocalNotifications.SyncSavedPermission());
            Assert.DoesNotThrow(() =>
            {
                LocalNotifications.Reschedule(new List<PlannedNotification> { new PlannedNotification() });
                LocalNotifications.CancelAll();
                LocalNotifications.ClearDelivered();
                LocalNotifications.OpenSystemSettings();
            });
            Assert.AreEqual(NotificationPermission.Unknown, LocalNotifications.Permission);
        }

        [Test]
        public void Facade_Reschedule_CancelsThenSchedulesThePlan_InitializeOncePerBackend()
        {
            var backend = new FakeBackend();
            LocalNotifications.Backend = backend;
            var context = Context(Noon);
            context.BasketFullLocal = Noon.AddHours(1);
            context.AttendancePending = true;
            var plan = Plan(in context);

            Assert.IsTrue(LocalNotifications.Initialize());
            Assert.IsTrue(LocalNotifications.Initialize());
            LocalNotifications.Reschedule(plan);

            Assert.AreEqual(1, backend.Initialized);
            Assert.AreEqual(1, backend.CancelAlls);
            CollectionAssert.AreEqual(plan.Select(p => p.Id), backend.Scheduled.Select(p => p.Id));
        }

        [Test]
        public void Facade_BackendErrors_AreSwallowed()
        {
            var backend = new FakeBackend { ThrowOnSchedule = true };
            LocalNotifications.Backend = backend;

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[Notify\]"));
            Assert.DoesNotThrow(() => LocalNotifications.Reschedule(new List<PlannedNotification> { new PlannedNotification { Id = 1 } }));
        }

        // ── 권한 규칙 (LocalNotifications.EffectivePermission — 계획과 설정 창이 같이 쓴다) ──

        [TestCase(NotificationPermission.Granted, NotificationPermission.Unknown, NotificationPermission.Unknown)]
        [TestCase(NotificationPermission.Granted, NotificationPermission.SoftDeclined, NotificationPermission.SoftDeclined)]
        [TestCase(NotificationPermission.Granted, NotificationPermission.Granted, NotificationPermission.Granted)]
        [TestCase(NotificationPermission.Granted, NotificationPermission.Denied, NotificationPermission.Granted)]
        [TestCase(NotificationPermission.Denied, NotificationPermission.Granted, NotificationPermission.Denied)]
        [TestCase(NotificationPermission.Denied, NotificationPermission.Unknown, NotificationPermission.Denied)]
        [TestCase(NotificationPermission.Denied, NotificationPermission.SoftDeclined, NotificationPermission.Denied)]
        [TestCase(NotificationPermission.Unknown, NotificationPermission.Granted, NotificationPermission.Granted)]
        [TestCase(NotificationPermission.Unknown, NotificationPermission.SoftDeclined, NotificationPermission.SoftDeclined)]
        [TestCase(NotificationPermission.Unknown, NotificationPermission.Denied, NotificationPermission.Denied)]
        [TestCase(NotificationPermission.Unknown, NotificationPermission.Unknown, NotificationPermission.Unknown)]
        public void EffectivePermission_DeviceDeniedWins_DeviceGrantedStillNeedsInGameConsent(NotificationPermission device,
            NotificationPermission saved, NotificationPermission expected)
        {
            // Android 12 이하는 기기 권한이 늘 Allowed — 게임 안에서 허락(저장 Granted)받기 전에는 '아직 허용 전'
            Assert.AreEqual(expected, LocalNotifications.EffectivePermission(device, saved));
        }

        [Test]
        public void Effective_FollowsTheBackendAndTheSavedValue()
        {
            var backend = new FakeBackend { Current = NotificationPermission.Denied };
            LocalNotifications.Backend = backend;
            Assert.AreEqual(NotificationPermission.Denied, LocalNotifications.Effective, "기기가 꺼 두면 저장 값과 상관없이 Denied");

            backend.Current = NotificationPermission.Granted;
            Assert.AreEqual(LocalNotifications.EffectivePermission(NotificationPermission.Granted, GameSettings.NotifyPermission),
                LocalNotifications.Effective);

            LocalNotifications.Backend = null;
            Assert.AreEqual(LocalNotifications.EffectivePermission(NotificationPermission.Unknown, GameSettings.NotifyPermission),
                LocalNotifications.Effective, "백엔드가 없으면 저장 값");
        }
    }

    /// <summary>알림 상태 채우기 (NotificationContext.Capture): 원정 끝 · 바구니 가득 · 다음 초기화 시각을 시간대에 맞춰, 메타가 꺼지면 원정만</summary>
    public class NotificationContextCaptureTests : MetaSystemsTestBase
    {
        [Test]
        public void Capture_FillsTimes_FromTheSystems_InTheGivenZone()
        {
            SetUtc(2026, 11, 5, 12, 0);
            long now = TimeUtil.ToUnix(Clock.UtcNow);
            Data.expeditions.Add(new ExpeditionSave { zoneId = "z1", startUnix = now - 600, endUnix = now + 2 * 3600 });
            Data.expeditions.Add(new ExpeditionSave { zoneId = "z2", startUnix = now - 600, endUnix = now + 3600 });

            var context = NotificationContext.Capture(Clock, TimeZoneInfo.Utc, true, Tutorial, IdleChest, Expedition, Attendance, Dungeon, DailyClock);

            var nowLocal = new DateTime(2026, 11, 5, 12, 0, 0);
            Assert.AreEqual(nowLocal, context.NowLocal);
            Assert.AreEqual(nowLocal.AddHours(2), context.LastExpeditionDoneLocal, "가장 늦게 끝나는 원정");
            Assert.AreEqual(new DateTime(2026, 11, 6, 5, 0, 0), context.NextResetLocal);
            Assert.AreEqual(Tutorial.IsDone, context.FtueDone);
            if (IdleChest.IsUnlocked && !IdleChest.IsFull && IdleChest.Cap > TimeSpan.Zero)
                Assert.AreEqual(nowLocal + (IdleChest.Cap - IdleChest.Elapsed), context.BasketFullLocal);
            else
                Assert.IsNull(context.BasketFullLocal);
            Assert.AreEqual(Attendance.CanCheckIn, context.AttendancePending);
        }

        [Test]
        public void Capture_MetaOff_OnlyExpedition_AndNullSystemsAreTolerated()
        {
            SetUtc(2026, 11, 5, 12, 0);
            long now = TimeUtil.ToUnix(Clock.UtcNow);
            Data.expeditions.Add(new ExpeditionSave { zoneId = "z1", startUnix = now, endUnix = now + 1800 });

            var off = NotificationContext.Capture(Clock, TimeZoneInfo.Utc, false, Tutorial, IdleChest, Expedition, Attendance, Dungeon, DailyClock);
            var empty = NotificationContext.Capture(Clock, TimeZoneInfo.Utc, true, null, null, null, null, null, null);

            Assert.IsNull(off.BasketFullLocal);
            Assert.IsFalse(off.AttendancePending);
            Assert.IsFalse(off.DungeonUnusedToday);
            Assert.AreEqual(new DateTime(2026, 11, 5, 12, 30, 0), off.LastExpeditionDoneLocal);
            Assert.IsFalse(empty.FtueDone);
            Assert.IsNull(empty.LastExpeditionDoneLocal);
            Assert.AreEqual(new DateTime(2026, 11, 6, 5, 0, 0), empty.NextResetLocal, "일일 시계가 없으면 다음 날 05:00");
        }

        [TestCase(NotificationPermission.Unknown)]
        [TestCase(NotificationPermission.SoftDeclined)]
        public void Capture_DeviceGranted_ButNoInGameConsent_PlansNothing(NotificationPermission saved)
        {
            // Android 12 이하 · 기기 설정에서 먼저 켬: 기기 Granted + 저장 Unknown/SoftDeclined → 설정 창은 '허용하기', 계획은 비어 있다
            SetUtc(2026, 11, 5, 12, 0);
            long now = TimeUtil.ToUnix(Clock.UtcNow);
            Data.expeditions.Add(new ExpeditionSave { zoneId = "z1", startUnix = now, endUnix = now + 3600 });
            var permission = LocalNotifications.EffectivePermission(NotificationPermission.Granted, saved);

            var context = NotificationContext.Capture(Clock, TimeZoneInfo.Utc, true, Tutorial, IdleChest, Expedition, Attendance, Dungeon, DailyClock,
                permission);
            context.FtueDone = true;
            context.AttendancePending = true;
            context.DungeonUnusedToday = true;
            var plan = new List<PlannedNotification>();
            NotificationPlanner.Plan(in context, plan);

            Assert.AreNotEqual(NotificationPermission.Granted, permission);
            Assert.IsFalse(context.IdleChestEnabled || context.ExpeditionEnabled || context.DungeonEnabled || context.AttendanceEnabled);
            Assert.IsEmpty(plan);
        }

        [Test]
        public void Capture_InGameConsent_EnablesTheKindsThatAreSwitchedOn()
        {
            SetUtc(2026, 11, 5, 12, 0);
            long now = TimeUtil.ToUnix(Clock.UtcNow);
            Data.expeditions.Add(new ExpeditionSave { zoneId = "z1", startUnix = now, endUnix = now + 3600 });

            var context = NotificationContext.Capture(Clock, TimeZoneInfo.Utc, true, Tutorial, IdleChest, Expedition, Attendance, Dungeon, DailyClock,
                NotificationPermission.Granted);
            context.FtueDone = true;
            var plan = new List<PlannedNotification>();
            NotificationPlanner.Plan(in context, plan);

            // 전체 · 종류 스위치는 PlayerPrefs(GameSettings) — 이 테스트는 쓰지 않고 읽기만 한다
            bool expeditionOn = GameSettings.NotifyMaster && GameSettings.GetNotify(NotificationKind.ExpeditionDone);
            Assert.AreEqual(expeditionOn, context.ExpeditionEnabled);
            Assert.AreEqual(expeditionOn, plan.Any(p => p.Kind == NotificationKind.ExpeditionDone));
        }
    }
}
