using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// Q-02 수선 일지. 일일 11개(05:00 초기화, 총 170점) + 주간 8개(월요일 05:00, 총 140점), 20점마다 상자.
    ///   · 잠긴 기능의 미션은 숨기고 그날 총점에서 뺀다 (A4). 일지가 열리는 1-7에는 던전(1-10)·상점(1-8)이 잠겨 있어
    ///     일일 140점 / 주간 120점이 남지만 100점 상자까지 닿는다 (MetaDataBuilder.Validate가 확인).
    ///   · 핵심 행동 20점 / 곁가지 10점. 100점보다 넉넉히 준비해 몇 개는 건너뛰어도 된다 (AFK Arena 방식).
    ///   · 경제: 일일 상자 별조각 100 + 주간 상자 별조각 300 → 일지만으로 주당 약 1,000 별조각.
    /// 담당: M2.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillMissions(MetaDatabase db, GameContent content)
        {
            var m = db.missions;

            // ── 일일 (MissionPeriod.Daily) ──
            m.Add(Daily("md_login", "수선점 들르기", CounterKey.Login, 1, 10,
                icon: Pick(MetaArt("mission_login"), ArtLib.Icon("mission_visit"), ArtLib.Battle("shop"))));
            // [가기]는 일지를 닫고 바구니 팝업을 연다 → 손가락은 팝업 안 버튼 (위젯·상단 바 버튼은 팝업 어둠 아래)
            m.Add(Daily("md_chest", "수선 바구니 2번 받기", CounterKey.ChestClaim, 2, 10, FeatureId.IdleChest,
                FeatureId.IdleChest, Targets.IdleChestClaim, BasketIcon()));
            m.Add(Daily("md_echo", "잔향이 30번 부르기", CounterKey.EchoSummon, 30, 20,
                nav: FeatureId.Battle, focus: CoachTargets.DeckCard0, icon: ArtLib.Icon("echo")));
            m.Add(Daily("md_lamp", "불 밝히기 5번", CounterKey.LampLit, 5, 20,
                nav: FeatureId.Battle, focus: CoachTargets.LampButton, icon: ArtLib.Icon("lamp")));
            m.Add(Daily("md_kill", "적 300마리 물리치기", CounterKey.EnemyKill, 300, 20,
                icon: Pick(MetaArt("mission_kill"), ArtLib.Enemy("enemy_dust"), ArtLib.Icon("guardian"))));
            m.Add(Daily("md_expedition", "원정 2번 보내기", CounterKey.ExpeditionStart, 2, 20, FeatureId.Expedition,
                FeatureId.Expedition, Targets.ExpeditionZoneList, ArtLib.UI("tab_expedition")));
            m.Add(Daily("md_dungeon", "잔향이 던전 1번 (도전 또는 소탕)", CounterKey.DungeonRun, 1, 20, FeatureId.Dungeon,
                FeatureId.Dungeon, null, Pick(MetaArt("feature_dungeon"), ArtLib.Icon("echo"))));
            m.Add(Daily("md_upgrade", "친구·수선점 강화 5번", CounterKey.AnyUpgrade, 5, 10,
                nav: FeatureId.ShopUpgrade, focus: Targets.GrowthLevelUp1, icon: ArtLib.UI("tab_growth")));
            m.Add(Daily("md_gacha", "잃어버린 친구 찾기 1번", CounterKey.GachaPull, 1, 10, FeatureId.Gacha,
                FeatureId.Gacha, Targets.GachaPull1, ArtLib.UI("tab_gacha")));
            m.Add(Daily("md_shop", "상점에서 1번 사기", CounterKey.ShopBuy, 1, 10, FeatureId.Shop,
                FeatureId.Shop, Targets.ShopProductList, ArtLib.UI("tab_shop")));
            m.Add(Daily("md_quick_idle", "빠른 수선 1번", CounterKey.QuickIdle, 1, 20, FeatureId.QuickIdle,
                FeatureId.QuickIdle, Targets.IdleChestQuick, Pick(ArtLib.Icon("ticket_quick"), ArtLib.UI("idle_basket_3"))));

            // ── 주간 (MissionPeriod.Weekly) ──
            m.Add(Weekly("mw_journal", "수선 일지 100점 5일", CounterKey.JournalComplete, 5, 30,
                icon: JournalIcon()));
            // [가기]가 출석부 팝업을 바로 연다 — 오늘 칸이 빛나므로 손가락은 두지 않는다 (상단 바 출석부 버튼은 팝업 어둠 아래)
            m.Add(Weekly("mw_attendance", "출석 도장 5일", CounterKey.AttendanceCheckIn, 5, 20, FeatureId.Attendance,
                FeatureId.Attendance, null, Pick(MetaArt("feature_attendance"), ArtLib.Icon("attendance"), ArtLib.Icon("star"))));
            m.Add(Weekly("mw_boss", "보스 15마리 물리치기", CounterKey.BossKill, 15, 20,
                icon: BossIcon()));
            m.Add(Weekly("mw_expedition", "원정 12번 보내기", CounterKey.ExpeditionStart, 12, 20, FeatureId.Expedition,
                FeatureId.Expedition, Targets.ExpeditionZoneList, ArtLib.UI("tab_expedition")));
            m.Add(Weekly("mw_dungeon", "잔향이 던전 10번", CounterKey.DungeonRun, 10, 20, FeatureId.Dungeon,
                FeatureId.Dungeon, null, Pick(MetaArt("feature_dungeon"), ArtLib.Icon("echo"))));
            m.Add(Weekly("mw_gacha", "잃어버린 친구 찾기 10번", CounterKey.GachaPull, 10, 10, FeatureId.Gacha,
                FeatureId.Gacha, Targets.GachaPull1, ArtLib.UI("tab_gacha")));
            // 공명은 처음부터 할 수 있어 기능 잠금 없이 보인다 (진화 1-10, 의상 2-5)
            m.Add(Weekly("mw_growth", "진화·공명·의상 중 1번", CounterKey.AnyGrowthMilestone, 1, 10,
                nav: FeatureId.Characters, focus: Targets.CharacterGrid, icon: ArtLib.Icon("shards")));
            m.Add(Weekly("mw_lamp", "불 밝히기 50번", CounterKey.LampLit, 50, 10,
                nav: FeatureId.Battle, focus: CoachTargets.LampButton, icon: ArtLib.Icon("lamp")));

            // ── 점수 상자 20 / 40 / 60 / 80 / 100 ──
            var c = db.missionChests;
            c.Add(Chest(MissionPeriod.Daily, 20, MetaReward.GoldMinutes(30)));
            c.Add(Chest(MissionPeriod.Daily, 40, Gem(30)));
            c.Add(Chest(MissionPeriod.Daily, 60, MetaReward.QuickIdleTicket(1)));
            c.Add(Chest(MissionPeriod.Daily, 80, Mat(MaterialIds.Cloth, 5), Mat(MaterialIds.Thread, 1)));
            c.Add(Chest(MissionPeriod.Daily, 100, Gem(70)));

            c.Add(Chest(MissionPeriod.Weekly, 20, Gem(50)));
            c.Add(Chest(MissionPeriod.Weekly, 40, MetaReward.QuickIdleTicket(2), MetaReward.DoubleTicket(1)));
            c.Add(Chest(MissionPeriod.Weekly, 60, Mat(MaterialIds.Moonlight, 1)));
            c.Add(Chest(MissionPeriod.Weekly, 80, Gem(150)));
            c.Add(Chest(MissionPeriod.Weekly, 100, MetaReward.SelectShards(20), Gem(100)));
        }

        static MissionDef Daily(string id, string text, CounterKey counter, long target, int points,
            FeatureId required = FeatureId.Battle, FeatureId nav = FeatureId.Battle, string focus = null, Sprite icon = null) =>
            Mission(MissionPeriod.Daily, id, text, counter, target, points, required, nav, focus, icon);

        static MissionDef Weekly(string id, string text, CounterKey counter, long target, int points,
            FeatureId required = FeatureId.Battle, FeatureId nav = FeatureId.Battle, string focus = null, Sprite icon = null) =>
            Mission(MissionPeriod.Weekly, id, text, counter, target, points, required, nav, focus, icon);

        static MissionDef Mission(MissionPeriod period, string id, string text, CounterKey counter, long target, int points,
            FeatureId required, FeatureId nav, string focus, Sprite icon) => new()
        {
            id = id,
            period = period,
            text = text,
            counter = counter,
            target = target,
            points = points,
            requiredFeature = required,
            navFeature = nav,
            focusTarget = focus,
            icon = Pick(icon, JournalIcon()),
        };

        static MissionChestDef Chest(MissionPeriod period, int points, params MetaReward[] rewards) => new()
        {
            period = period,
            points = points,
            rewards = Rewards(rewards),
        };
    }
}
