using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 입장권 모아 두기 (NEW-04, SPEC4 §13.9 — 확장 항목, 상한 0이면 꺼짐): 지난 날마다 +3 (상한 9), 수요일 +2는 한 번 (그날 11),
    /// -1 옮기기 = 그날 남은 입장권, 실패한 도전은 입장권을 지킨다, 한 번에 소탕은 모아 둔 것을 다 쓸 수 있다. 일일 시계(05:00, UTC)
    /// </summary>
    public class DungeonTicketBankTests : SystemsTestBase
    {
        int _bonus;

        (DungeonSystem dungeon, DailyClock clock) CreateBankDungeon(DateTime utcNow, int cap = 9)
        {
            Clock.UtcNow = utcNow;
            var dailyClock = new DailyClock(Data, Clock, TimeZoneInfo.Utc);
            dailyClock.Refresh();
            var dungeon = new DungeonSystem(Data, BattleCfg, Characters, Formation, Synergy, Rewards, Clock, dailyClock)
            {
                TicketBankCap = () => cap,
                BonusTickets = () => _bonus,
            };
            return (dungeon, dailyClock);
        }

        [SetUp]
        public void ResetBonus() => _bonus = 0;

        void NextDay(DailyClock clock, int days = 1)
        {
            Clock.Advance(TimeSpan.FromDays(days));
            clock.Refresh();
        }

        [Test]
        public void CapZero_IsOff_DailyResetAsBefore()
        {
            var (dungeon, clock) = CreateBankDungeon(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), cap: 0);
            Data.dungeon.highestFloor = 3;
            Assert.IsFalse(dungeon.IsTicketBankOn);
            dungeon.Sweep(1);
            Assert.AreEqual(2, dungeon.TicketsLeft);
            NextDay(clock, 3);
            Assert.AreEqual(3, dungeon.TicketsLeft, "모아 두지 않는다");
            Assert.AreEqual(-1, Data.dungeon.ticketBank, "꺼져 있으면 옮기지 않는다");
        }

        [Test]
        public void Refill_PerSkippedDay_UpToCap()
        {
            var (dungeon, clock) = CreateBankDungeon(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc));
            Data.dungeon.highestFloor = 3;
            Assert.IsTrue(dungeon.IsTicketBankOn);
            Assert.AreEqual(3, dungeon.TicketsLeft, "처음 = 그날 남은 입장권");
            Assert.IsNotNull(dungeon.Sweep(1));
            Assert.AreEqual(2, dungeon.TicketsLeft);

            NextDay(clock);
            Assert.AreEqual(5, dungeon.TicketsLeft, "2 + 3");
            Assert.AreEqual(5, dungeon.TicketsLeft, "같은 날 다시 읽어도 그대로");
            NextDay(clock, 2);
            Assert.AreEqual(9, dungeon.TicketsLeft, "5 + 6 → 상한 9");
            NextDay(clock, 5);
            Assert.AreEqual(9, dungeon.TicketsLeft);
        }

        [Test]
        public void WednesdayBonus_AddedOnce_CapElevenThatDayOnly()
        {
            var (dungeon, clock) = CreateBankDungeon(new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc)); // 화요일
            Data.dungeon.highestFloor = 3;
            NextDay(clock, 0);
            Data.dungeon.ticketBank = 9;
            Data.dungeon.bankDayKey = clock.DayKey;
            Assert.AreEqual(9, dungeon.TicketsLeft);

            _bonus = 2;
            NextDay(clock); // 수요일
            Assert.AreEqual(11, dungeon.TicketsLeft, "min(9, 9 + 3) + 2");
            Assert.AreEqual(11, dungeon.TicketsLeft, "더하는 입장권은 그날 한 번");

            _bonus = 0;
            NextDay(clock); // 목요일
            Assert.AreEqual(9, dungeon.TicketsLeft, "다음 날은 다시 상한 9");
        }

        [Test]
        public void Migration_MinusOne_BecomesTodaysTicketsLeft()
        {
            var (dungeon, clock) = CreateBankDungeon(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc));
            Data.dungeon.highestFloor = 3;
            Data.dungeon.ticketDate = clock.DayKey;
            Data.dungeon.ticketsUsed = 2;
            Assert.AreEqual(-1, Data.dungeon.ticketBank);

            Assert.AreEqual(1, dungeon.TicketsLeft, "옛 세이브: 그날 3 − 2");
            Assert.AreEqual(1, Data.dungeon.ticketBank);
            Assert.AreEqual(clock.DayKey, Data.dungeon.bankDayKey);
            NextDay(clock);
            Assert.AreEqual(4, dungeon.TicketsLeft);
        }

        [Test]
        public void FailedChallenge_KeepsTicket()
        {
            var (dungeon, _) = CreateBankDungeon(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc));
            foreach (var enemy in BattleCfg.enemies)
            {
                enemy.hp = 1e9f;
                enemy.damage = 1e9f;
            }
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Assert.AreEqual(3, dungeon.TicketsLeft);

            var result = dungeon.Challenge();
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Cleared);
            Assert.AreEqual(3, dungeon.TicketsLeft, "실패한 도전은 입장권을 쓰지 않는다");
        }

        [Test]
        public void SweepMany_CanSpendTheWholeBank()
        {
            var (dungeon, clock) = CreateBankDungeon(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc));
            Data.dungeon.highestFloor = 4;
            Assert.AreEqual(3, dungeon.TicketsLeft);
            NextDay(clock, 3);
            Assert.AreEqual(9, dungeon.TicketsLeft);

            var result = dungeon.SweepMany(4, 99);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, dungeon.TicketsLeft);
            Assert.AreEqual(9, Data.dungeon.ticketsUsed, "진행 기록 추론용 그날 사용 수도 센다");
            Assert.IsNull(dungeon.Sweep(1));
        }
    }
}
