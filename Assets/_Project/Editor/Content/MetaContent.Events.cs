using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// EVT-01 이벤트 달력 (SPEC4 §8.3 · 부록 A.7): 날 키(05:00) 기준으로 기기마다 같은 일정, 밤의 수선점만 현지 시각.
    ///   · 주말 원정 축제 — 토 · 일 원정 재료 ×2 (원정 받기 때) · 효과 칩 '원정 재료 ×2'
    ///   · 수요 던전 데이 — 수요일 던전 입장권 +2 · '던전 입장권 +2'
    ///   · 별이 머문 밤 산책 — 매달 1~14일 스테이지 클리어 별빛 사탕 ×2 + 바구니 사탕 줄 · 미션 6 + 보너스 1 · 도장 7칸 · '별빛 사탕 ×2'
    ///   · 밤의 수선점 — 매일 21시 ~ 새벽 2시(현지) 바구니 재료 · 별조각 줄 +20% (단추 제외) · '밤 9시~새벽 2시 바구니 +20%'
    ///   효과 칩 글자는 종류 · 배율에서 만든다 (EventDef에 칸이 없다 — EventCalendarSystem / 이벤트 모아 보기).
    ///   · 시작 카드(회차마다 한 번)는 별이 머문 밤 산책 · 주말 원정 축제만 (매주 수요일 · 매일 밤 카드는 소음이라 뺐다).
    ///   · 별이 머문 밤 산책 별조각: 보너스 미션 100 + 7일째 도장 150 = 한 달 250 ≤ 300 (§11.2).
    ///   · 보너스 미션은 '미션 6개 모두 받기' = 이벤트 미션 받기 카운터(회차 기준값부터) 6.
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        internal const string EventWeekendExpedition = "evt_weekend_expedition";
        internal const string EventWednesdayDungeon = "evt_wednesday_dungeon";
        internal const string EventStarlitWalk = "evt_starlit_walk";
        internal const string EventNightShop = "evt_night_shop";

        /// <summary>요일 비트 (bit0 = 월요일 … bit6 = 일요일, 날 키의 요일)</summary>
        const int Wednesday = 1 << 2, Saturday = 1 << 5, Sunday = 1 << 6;

        static partial void FillEvents(MetaDatabase db, GameContent content)
        {
            var list = db.events;

            list.Add(new EventDef
            {
                id = EventWeekendExpedition,
                name = "주말 원정 축제",
                shortDesc = "토·일요일에는 원정에서 재료를 두 배로 가져와요",
                kind = EventKind.DropBoost,
                scheduleType = EventScheduleType.Weekdays,
                weekdayMask = Saturday | Sunday,
                multiplier = 2,
                startCard = true,
                banner = Pick(ArtLib.UI("zone_exp_2_1"), ArtLib.UI("zone_exp_1_1")),
                icon = Pick(ArtLib.UI("tab_expedition"), ArtLib.Icon("star")),
            });

            list.Add(new EventDef
            {
                id = EventWednesdayDungeon,
                name = "수요 던전 데이",
                shortDesc = "수요일에는 잔향이 던전 입장권이 2장 더 생겨요",
                kind = EventKind.TicketBonus,
                scheduleType = EventScheduleType.Weekdays,
                weekdayMask = Wednesday,
                bonus = 2,
                startCard = false,
                banner = ArtLib.UI("dungeon_banner"),
                icon = Pick(ArtLib.Icon("echo"), ArtLib.Icon("star")),
            });

            var starlit = new EventDef
            {
                id = EventStarlitWalk,
                name = "별이 머문 밤 산책",
                shortDesc = "매달 1일부터 14일까지, 별빛 사탕이 두 배로 쏟아져요",
                kind = EventKind.Festival,
                scheduleType = EventScheduleType.MonthDays,
                fromDay = 1,
                toDay = 14,
                multiplier = 2,
                startCard = true,
                banner = ArtLib.UI("shop_event_banner"),
                icon = Pick(ArtLib.Icon("currency_event"), ArtLib.Icon("star")),
            };
            starlit.missions.Add(EventMission("sw_stage", "스테이지 50번 클리어", CounterKey.StageClear, 50, Candy(60)));
            starlit.missions.Add(EventMission("sw_lamp", "불 밝히기 30번", CounterKey.LampLit, 30, Candy(40)));
            starlit.missions.Add(EventMission("sw_expedition", "원정 6번 보내기", CounterKey.ExpeditionStart, 6, Candy(40)));
            starlit.missions.Add(EventMission("sw_dungeon", "잔향이 던전 3번", CounterKey.DungeonRun, 3, Candy(40)));
            starlit.missions.Add(EventMission("sw_gacha", "잃어버린 친구 찾기 5번", CounterKey.GachaPull, 5, Candy(50)));
            starlit.missions.Add(EventMission("sw_basket", "수선 바구니 5번 받기", CounterKey.ChestClaim, 5, Candy(40)));
            var bonus = EventMission("sw_bonus", "미션 6개 모두 받기", CounterKey.EventMissionClaim, StarlitRegularMissions, Gem(100), Candy(100));
            bonus.isBonus = true;
            starlit.missions.Add(bonus);

            starlit.stamps.Add(EventStamp(1, false, Candy(30)));
            starlit.stamps.Add(EventStamp(2, false, MetaReward.QuickIdleTicket(1)));
            starlit.stamps.Add(EventStamp(3, false, Candy(50)));
            starlit.stamps.Add(EventStamp(4, false, Mat(MaterialIds.Ribbon, 15)));
            starlit.stamps.Add(EventStamp(5, false, Candy(70)));
            starlit.stamps.Add(EventStamp(6, false, MetaReward.DoubleTicket(1)));
            starlit.stamps.Add(EventStamp(7, true, Gem(150), Candy(100)));
            list.Add(starlit);

            list.Add(new EventDef
            {
                id = EventNightShop,
                name = "밤의 수선점",
                shortDesc = "밤에는 바구니에 선물이 조금 더 쌓여요",
                kind = EventKind.NightBonus,
                scheduleType = EventScheduleType.DailyHours,
                fromHour = 21,
                toHour = 2,
                multiplier = 1.2,
                startCard = false,
                banner = Pick(ArtLib.UI("bg_night"), ArtLib.UI("moon")),
                icon = Pick(ArtLib.UI("moon"), ArtLib.Icon("lamp")),
            });
        }

        /// <summary>별이 머문 밤 산책 보통 미션 수 (보너스 미션 목표)</summary>
        const int StarlitRegularMissions = 6;

        /// <summary>별빛 사탕 (이벤트 재화)</summary>
        static MetaReward Candy(double amount) => MetaReward.Currency(CurrencyType.EventToken, amount);

        static EventMissionDef EventMission(string id, string text, CounterKey counter, long target, params MetaReward[] rewards) => new()
        {
            id = id,
            text = text,
            counter = counter,
            target = target,
            rewards = Rewards(rewards),
        };

        static EventStampDef EventStamp(int day, bool highlight, params MetaReward[] rewards) => new()
        {
            day = day,
            highlight = highlight,
            rewards = Rewards(rewards),
        };
    }
}
