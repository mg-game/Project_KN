using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// NEW-01 첫 주 (SPEC4 §13.6 · 부록 C.2): 수선 일지(1-7)가 열릴 때 설치 7일째 이하면 시작, 14일(날 키) 동안 받을 수 있다.
    ///   · 하루 4과제 × 7일 = 28과제. 1~6일 5점 · 7일 15점 = 180점. 상자 30/60/90/120/150 (30점 여유 — Capybara 비율).
    ///   · 과제는 조건을 처음 넘은 순간 완료로 굳는다 (오늘 일지 점수처럼 초기화되는 조건도). 잠긴 기능 과제는 자물쇠만 달고 창 안에서 나중에 받는다.
    ///   · 별조각 300 한 번 ≤ 350 (§11.2). 과제 ID는 fw_d{일}_{이름} (이벤트 미션 sw_*와 겹치지 않게).
    ///   · 화면 문구(탭 '첫 주', 머리말, 토스트)는 이벤트 모아 보기 · 일지 화면(P4-6)이 쓴다 (부록 C.2).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>첫 주 과제 점수 (1~6일 / 7일)</summary>
        internal const int StarterTaskPoints = 5, StarterFinalDayPoints = 15;

        static partial void FillStarterWeek(MetaDatabase db, GameContent content)
        {
            var week = db.starterWeek = new StarterWeekConfig
            {
                windowDays = 14,
                maxInstallDays = 7,
                tasks = new List<StarterTaskDef>(),
                chests = new List<StarterChestDef>(),
            };
            var t = week.tasks;

            // 1일째
            t.Add(StarterTask(1, "stage", StageClearText(1, 10), MetaCondition.StageCleared(1, 10)));
            t.Add(StarterTask(1, "echo", "잔향이 50번 부르기", MetaCondition.Count(CounterKey.EchoSummon, 50),
                nav: FeatureId.Battle, focus: CoachTargets.DeckCard0));
            t.Add(StarterTask(1, "shop", "수선점 Lv.20", MetaCondition.Of(MetaConditionType.ShopLevel, 20),
                nav: FeatureId.ShopUpgrade, focus: Targets.GrowthLevelUp1));
            t.Add(StarterTask(1, "journal", "오늘 수선 일지 40점", MetaCondition.Of(MetaConditionType.JournalPoints, 40),
                FeatureId.Missions, FeatureId.Missions, null));
            // 2일째
            t.Add(StarterTask(2, "gacha", "잃어버린 친구 찾기 10번", MetaCondition.Count(CounterKey.GachaPull, 10),
                FeatureId.Gacha, FeatureId.Gacha, Targets.GachaPull1));
            t.Add(StarterTask(2, "expedition", "원정 3번 보내기", MetaCondition.Count(CounterKey.ExpeditionStart, 3),
                FeatureId.Expedition, FeatureId.Expedition, Targets.ExpeditionZoneList));
            t.Add(StarterTask(2, "levelsum", "친구 레벨 합계 60", MetaCondition.Of(MetaConditionType.LineageLevelSum, 60),
                nav: FeatureId.ShopUpgrade, focus: Targets.GrowthLineageList));
            t.Add(StarterTask(2, "basket", "수선 바구니 3번 받기", MetaCondition.Count(CounterKey.ChestClaim, 3),
                FeatureId.IdleChest, FeatureId.IdleChest, Targets.IdleChestClaim));
            // 3일째
            t.Add(StarterTask(3, "stage", StageClearText(2, 5), MetaCondition.StageCleared(2, 5)));
            t.Add(StarterTask(3, "dungeon", "잔향이 던전 3층 돌파", MetaCondition.Of(MetaConditionType.DungeonFloor, 3),
                FeatureId.Dungeon, FeatureId.Dungeon, null));
            t.Add(StarterTask(3, "lamp", "불 밝히기 30번", MetaCondition.Count(CounterKey.LampLit, 30),
                nav: FeatureId.Battle, focus: CoachTargets.LampButton));
            t.Add(StarterTask(3, "quick", "빠른 수선 3번", MetaCondition.Count(CounterKey.QuickIdle, 3),
                FeatureId.QuickIdle, FeatureId.QuickIdle, Targets.IdleChestQuick));
            // 4일째
            t.Add(StarterTask(4, "evolution", "진화 1번 만들기", MetaCondition.Count(CounterKey.EvolutionCraft, 1),
                FeatureId.Evolution, FeatureId.Evolution, null));
            t.Add(StarterTask(4, "synergy", "시너지 3개 켜기", MetaCondition.Of(MetaConditionType.ActiveSynergies, 3),
                FeatureId.Formation, FeatureId.Formation, Targets.FormationSynergy));
            t.Add(StarterTask(4, "buy", "상점에서 2번 사기", MetaCondition.Count(CounterKey.ShopBuy, 2),
                FeatureId.Shop, FeatureId.Shop, Targets.ShopProductList));
            t.Add(StarterTask(4, "shop", "수선점 Lv.40", MetaCondition.Of(MetaConditionType.ShopLevel, 40),
                nav: FeatureId.ShopUpgrade, focus: Targets.GrowthLevelUp1));
            // 5일째
            t.Add(StarterTask(5, "stage", StageClearText(2, 10), MetaCondition.StageCleared(2, 10)));
            t.Add(StarterTask(5, "expedition", "원정 10번 받기", MetaCondition.Count(CounterKey.ExpeditionClaim, 10),
                FeatureId.Expedition, FeatureId.Expedition, Targets.ExpeditionZoneList));
            t.Add(StarterTask(5, "kill", "적 3,000마리 물리치기", MetaCondition.Count(CounterKey.EnemyKill, 3000)));
            t.Add(StarterTask(5, "attendance", "출석 도장 5일", MetaCondition.Count(CounterKey.AttendanceCheckIn, 5),
                FeatureId.Attendance, FeatureId.Attendance, null));
            // 6일째
            t.Add(StarterTask(6, "resonance", "계보 공명 1번", MetaCondition.Count(CounterKey.ResonanceUp, 1),
                nav: FeatureId.Characters, focus: Targets.CharacterGrid));
            t.Add(StarterTask(6, "costume", "의상 1번 입히기", MetaCondition.Count(CounterKey.CostumeEquip, 1),
                FeatureId.Costumes, FeatureId.Costumes, null));
            t.Add(StarterTask(6, "levelsum", "친구 레벨 합계 120", MetaCondition.Of(MetaConditionType.LineageLevelSum, 120),
                nav: FeatureId.ShopUpgrade, focus: Targets.GrowthLineageList));
            t.Add(StarterTask(6, "dungeon", "잔향이 던전 5층 돌파", MetaCondition.Of(MetaConditionType.DungeonFloor, 5),
                FeatureId.Dungeon, FeatureId.Dungeon, null));
            // 7일째 (15점씩)
            t.Add(StarterTask(7, "stage", StageClearText(3, 5), MetaCondition.StageCleared(3, 5)));
            t.Add(StarterTask(7, "journal", "수선 일지 100점 3일", MetaCondition.Count(CounterKey.JournalComplete, 3),
                FeatureId.Missions, FeatureId.Missions, null));
            t.Add(StarterTask(7, "gacha", "잃어버린 친구 찾기 30번", MetaCondition.Count(CounterKey.GachaPull, 30),
                FeatureId.Gacha, FeatureId.Gacha, Targets.GachaPull1));
            t.Add(StarterTask(7, "forms", "형태 10종 해금", MetaCondition.Of(MetaConditionType.UnlockedForms, 10),
                FeatureId.Evolution, FeatureId.Evolution, null));

            var c = week.chests;
            c.Add(StarterChest(30, false, Gem(50), MetaReward.QuickIdleTicket(1)));
            c.Add(StarterChest(60, false, Mat(MaterialIds.Ribbon, 15), Mat(MaterialIds.Cloth, 15)));
            c.Add(StarterChest(90, false, Gem(100), Mat(MaterialIds.Thread, 3)));
            c.Add(StarterChest(120, false, MetaReward.SelectShards(20), MetaReward.DoubleTicket(1)));
            c.Add(StarterChest(150, true, Gem(150), Mat(MaterialIds.Moonlight, 2), MetaReward.SelectShards(20)));
        }

        /// <summary>첫 주 과제 (ID = fw_d{일}_{이름}, 7일째 15점)</summary>
        static StarterTaskDef StarterTask(int day, string name, string text, MetaCondition condition,
            FeatureId required = FeatureId.Battle, FeatureId nav = FeatureId.Battle, string focus = null) => new()
        {
            id = $"fw_d{day}_{name}",
            day = day,
            text = text,
            condition = condition,
            points = day >= 7 ? StarterFinalDayPoints : StarterTaskPoints,
            requiredFeature = required,
            navFeature = nav,
            focusTarget = focus,
        };

        static StarterChestDef StarterChest(int points, bool highlight, params MetaReward[] rewards) => new()
        {
            points = points,
            highlight = highlight,
            rewards = Rewards(rewards),
        };
    }
}
