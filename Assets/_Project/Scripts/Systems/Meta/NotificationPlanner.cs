using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 알림 계획에 넣는 지금 상태 (일시정지 · 종료 때 GameBootstrap이 Capture로 채운다, NOTI-01). 시각은 모두 현지 시각.
    /// </summary>
    public struct NotificationContext
    {
        public DateTime NowLocal;
        public bool FtueDone;
        /// <summary>
        /// 종류별로 켜졌는지 (Capture: LocalNotifications.Effective == Granted && GameSettings.NotifyMaster && GetNotify(종류) — 게임 안에서 허락받기 전 ·
        /// 기기가 꺼 두었으면 보내지 않는다. 설정 창 권한 줄과 같은 규칙 LocalNotifications.EffectivePermission)
        /// </summary>
        public bool IdleChestEnabled, ExpeditionEnabled, DungeonEnabled, AttendanceEnabled;
        /// <summary>바구니가 가득 차는 시각 (잠겼거나 이미 가득이면 null)</summary>
        public DateTime? BasketFullLocal;
        /// <summary>진행 중인 원정 중 가장 늦게 끝나는 시각 (없으면 null)</summary>
        public DateTime? LastExpeditionDoneLocal;
        /// <summary>오늘 출석 도장을 아직 안 찍음</summary>
        public bool AttendancePending;
        /// <summary>오늘 던전 입장권을 하나도 안 씀 (1층 이상 돌파했을 때)</summary>
        public bool DungeonUnusedToday;
        /// <summary>입장권 모아 두기 (NEW-04): 모아 둔 수 · 상한 (상한 0 = 모아 두기 꺼짐 → DungeonUnusedToday 규칙)</summary>
        public int TicketBank, TicketBankCap;
        /// <summary>다음 05:00 초기화 (현지)</summary>
        public DateTime NextResetLocal;

        /// <summary>그 종류가 켜졌는지</summary>
        public bool IsEnabled(NotificationKind kind) => kind switch
        {
            NotificationKind.IdleChestFull => IdleChestEnabled,
            NotificationKind.ExpeditionDone => ExpeditionEnabled,
            NotificationKind.DungeonTickets => DungeonEnabled,
            NotificationKind.Attendance => AttendanceEnabled,
            _ => false,
        };

        /// <summary>
        /// 지금 게임 상태로 채운다 (GameBootstrap: 일시정지 · 종료 때). 시스템이 없으면(null) 그 줄은 비운다. 메타가 꺼졌으면 원정만.
        /// 시각: clock(UTC) → timeZone 현지 (null = 기기 시간대). 켜짐 = 권한(LocalNotifications.Effective — 기기 Denied면 꺼짐, 그 밖에는 게임 안에서 받은 허락)
        /// Granted && 전체 스위치 && 종류 스위치 (GameSettings, PlayerPrefs)
        /// </summary>
        public static NotificationContext Capture(ITimeProvider clock, TimeZoneInfo timeZone, bool metaActive, TutorialSystem tutorial,
            IdleChestSystem idleChest, ExpeditionSystem expedition, AttendanceSystem attendance, DungeonSystem dungeon, DailyClock dailyClock) =>
            Capture(clock, timeZone, metaActive, tutorial, idleChest, expedition, attendance, dungeon, dailyClock, LocalNotifications.Effective);

        /// <summary>Capture와 같되 권한을 직접 받는다 (permission = LocalNotifications.EffectivePermission의 결과 — 테스트가 PlayerPrefs를 건드리지 않게)</summary>
        internal static NotificationContext Capture(ITimeProvider clock, TimeZoneInfo timeZone, bool metaActive, TutorialSystem tutorial,
            IdleChestSystem idleChest, ExpeditionSystem expedition, AttendanceSystem attendance, DungeonSystem dungeon, DailyClock dailyClock,
            NotificationPermission permission)
        {
            var zone = timeZone ?? TimeZoneInfo.Local;
            var utcNow = clock != null ? DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc) : DateTime.UtcNow;
            var nowLocal = ToLocal(utcNow, zone);
            bool allowed = permission == NotificationPermission.Granted && GameSettings.NotifyMaster;
            var context = new NotificationContext
            {
                NowLocal = nowLocal,
                FtueDone = tutorial != null && tutorial.IsDone,
                IdleChestEnabled = allowed && GameSettings.GetNotify(NotificationKind.IdleChestFull),
                ExpeditionEnabled = allowed && GameSettings.GetNotify(NotificationKind.ExpeditionDone),
                DungeonEnabled = allowed && GameSettings.GetNotify(NotificationKind.DungeonTickets),
                AttendanceEnabled = allowed && GameSettings.GetNotify(NotificationKind.Attendance),
                NextResetLocal = dailyClock != null ? ToLocal(DateTime.SpecifyKind(dailyClock.NextDailyResetUtc, DateTimeKind.Utc), zone) : nowLocal.Date.AddDays(1).AddHours(DailyClock.ResetHour),
            };

            if (expedition != null)
            {
                var active = expedition.Active;
                TimeSpan longest = TimeSpan.Zero;
                for (int i = 0; active != null && i < active.Count; i++)
                {
                    var remaining = expedition.GetRemaining(active[i]);
                    if (remaining > longest) longest = remaining;
                }
                if (longest > TimeSpan.Zero) context.LastExpeditionDoneLocal = nowLocal + longest;
            }

            if (!metaActive) return context;

            if (idleChest != null && idleChest.IsUnlocked && !idleChest.IsFull)
            {
                var left = idleChest.Cap - idleChest.Elapsed;
                if (idleChest.Cap > TimeSpan.Zero && left > TimeSpan.Zero) context.BasketFullLocal = nowLocal + left;
            }
            context.AttendancePending = attendance != null && attendance.CanCheckIn;
            if (dungeon != null)
            {
                bool unlocked = dungeon.FeatureResolver == null || dungeon.FeatureResolver(FeatureId.Dungeon);
                int cap = dungeon.TicketBankCap != null ? Math.Max(0, dungeon.TicketBankCap()) : 0;
                context.TicketBankCap = unlocked ? cap : 0;
                context.TicketBank = unlocked && cap > 0 ? dungeon.TicketsLeft : 0;
                context.DungeonUnusedToday = unlocked && dungeon.HighestFloor >= 1 && dungeon.DailyTickets > 0
                                             && dungeon.TicketsLeft >= dungeon.DailyTickets;
            }
            return context;
        }

        static DateTime ToLocal(DateTime utc, TimeZoneInfo zone) =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, zone), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// 로컬 알림 계획 (NOTI-01, SPEC4 §4.3 — 순수, EditMode 테스트용). 우선순위: 바구니 가득(1001) → 원정 완료(1002) → 출석(1004) → 던전 입장권(1003).
    ///   · 튜토리얼 전에는 없음. 꺼진 종류는 없음.
    ///   · 조용한 시간 22:00~08:00 안이면 08:00으로 (22시 이후 = 다음 날 08:00).
    ///   · 바구니 · 원정: 조용한 시간을 옮긴 뒤 지금부터 15분 안이면 건너뜀 (SPEC4 표의 '≥15 min ahead'는 이 둘에만 있다).
    ///     출석(오늘 20:00, 20시가 지났으면 내일 20:00) · 던전(다음 초기화 → 08:00)은 지금보다 뒤이기만 하면 넣는다 — 19:50에 나가도 20:00 출석 알림이 온다.
    ///   · 3개보다 많으면 우선순위 높은 3개만. 남은 것을 시각 순으로 놓고 앞 알림과 30분 넘게 띄운다 (뒤 것을 민다, 밀려서 조용한 시간에 들면 08:00으로).
    /// 쉼 · 종료 때만 부른다 (into 목록만 채운다).
    /// </summary>
    public static class NotificationPlanner
    {
        /// <summary>최대 예약 수</summary>
        public const int MaxPending = 3;
        /// <summary>알림 사이 최소 간격 (분)</summary>
        public const int MinSpacingMinutes = 30;
        /// <summary>이보다 가까운(분) 바구니 · 원정 알림은 건너뛴다 (출석 · 던전은 지금보다 뒤이기만 하면 된다)</summary>
        public const int MinLeadMinutes = 15;
        /// <summary>조용한 시간 (현지 시, 시작 포함 · 끝 미포함)</summary>
        public const int QuietStartHour = 22, QuietEndHour = 8;
        /// <summary>출석 알림 시각 (현지 20:00)</summary>
        public const int AttendanceHour = 20;
        /// <summary>모아 두기: 다음 초기화에 +3장</summary>
        public const int TicketBankRefill = 3;

        public const int IdleChestId = 1001;
        public const int ExpeditionId = 1002;
        public const int DungeonId = 1003;
        public const int AttendanceId = 1004;

        public const string IdleChestTitle = "수선 바구니가 가득 찼어요";
        public const string IdleChestBody = "친구들이 모은 선물이 기다려요. 받으러 와 주세요!";
        public const string ExpeditionTitle = "원정 간 친구들이 돌아왔어요";
        public const string ExpeditionBody = "재료를 한 아름 안고 왔어요. 받아 볼까요?";
        public const string AttendanceTitle = "오늘 출석 도장을 아직 안 찍었어요";
        public const string AttendanceBody = "수선점에 들러 도장을 꾹 찍어 주세요.";
        public const string DungeonTitle = "잔향이 던전 입장권이 채워졌어요";
        public const string DungeonBankTitle = "잔향이 던전 입장권이 가득 찼어요";
        public const string DungeonBody = "오늘의 입장권으로 귀한 재료를 찾아요.";

        /// <summary>계획을 into에 채운다 (into는 비우고 채움, 시각 순). into가 null이면 아무것도 하지 않는다</summary>
        public static void Plan(in NotificationContext context, List<PlannedNotification> into)
        {
            if (into == null) return;
            into.Clear();
            if (!context.FtueDone) return;

            var now = context.NowLocal;

            // 우선순위 순서대로 후보를 넣는다 (넘치면 뒤가 빠진다)
            if (context.IsEnabled(NotificationKind.IdleChestFull) && context.BasketFullLocal.HasValue)
                TryAdd(into, now, true, NotificationKind.IdleChestFull, IdleChestId, context.BasketFullLocal.Value, IdleChestTitle, IdleChestBody);

            if (context.IsEnabled(NotificationKind.ExpeditionDone) && context.LastExpeditionDoneLocal.HasValue)
                TryAdd(into, now, true, NotificationKind.ExpeditionDone, ExpeditionId, context.LastExpeditionDoneLocal.Value, ExpeditionTitle,
                    ExpeditionBody);

            if (context.IsEnabled(NotificationKind.Attendance) && context.AttendancePending)
            {
                var fire = now.Date.AddHours(AttendanceHour);
                if (now >= fire) fire = fire.AddDays(1);
                TryAdd(into, now, false, NotificationKind.Attendance, AttendanceId, fire, AttendanceTitle, AttendanceBody);
            }

            if (context.IsEnabled(NotificationKind.DungeonTickets))
            {
                bool bank = context.TicketBankCap > 0;
                bool due = bank ? context.TicketBank + TicketBankRefill >= context.TicketBankCap : context.DungeonUnusedToday;
                if (due)
                    TryAdd(into, now, false, NotificationKind.DungeonTickets, DungeonId, context.NextResetLocal, bank ? DungeonBankTitle : DungeonTitle,
                        DungeonBody);
            }

            if (into.Count > MaxPending) into.RemoveRange(MaxPending, into.Count - MaxPending);

            // 시각 순 (같은 시각은 우선순위 순 — 넣은 순서) 뒤 30분 간격
            SortByFireTime(into);
            for (int i = 1; i < into.Count; i++)
            {
                var previous = into[i - 1].FireLocal;
                var item = into[i];
                if (item.FireLocal < previous.AddMinutes(MinSpacingMinutes))
                {
                    item.FireLocal = ShiftOutOfQuietHours(previous.AddMinutes(MinSpacingMinutes));
                    into[i] = item;
                }
            }
        }

        /// <summary>조용한 시간(22:00~08:00) 안이면 그다음 08:00으로 옮긴다 (밖이면 그대로)</summary>
        public static DateTime ShiftOutOfQuietHours(DateTime local)
        {
            int hour = local.Hour;
            if (hour >= QuietStartHour) return local.Date.AddDays(1).AddHours(QuietEndHour);
            if (hour < QuietEndHour) return local.Date.AddHours(QuietEndHour);
            return local;
        }

        /// <summary>
        /// 조용한 시간을 옮긴 시각으로 하나 넣는다. needsLead(바구니 · 원정)면 지금 + 15분보다 이르면 빼고, 아니면(출석 · 던전) 지금보다 뒤이기만 하면 넣는다
        /// </summary>
        static void TryAdd(List<PlannedNotification> into, DateTime now, bool needsLead, NotificationKind kind, int id, DateTime fireLocal, string title,
            string body)
        {
            var fire = ShiftOutOfQuietHours(fireLocal);
            if (needsLead ? fire < now.AddMinutes(MinLeadMinutes) : fire <= now) return;
            into.Add(new PlannedNotification { Kind = kind, Id = id, FireLocal = fire, Title = title, Body = body });
        }

        /// <summary>시각 순 삽입 정렬 (최대 4개, 같은 시각은 원래 순서 유지 — 안정 정렬)</summary>
        static void SortByFireTime(List<PlannedNotification> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var item = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].FireLocal > item.FireLocal)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = item;
            }
        }
    }
}
