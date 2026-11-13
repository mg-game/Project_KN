using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// Q-01 가이드 퀘스트 30단계 + 반복 단계. 전투 화면 왼쪽 아래 한 줄로 "이제 뭐 하지?"에 답한다.
    ///   · 진행도는 시작 전 것도 인정한다. 1~3단계는 튜토리얼 2·4·5단계에서 이미 끝나 줄이 나타나자마자 "받기" (A5, 의도된 즉시 보상).
    ///   · 줄은 튜토리얼 첫 보스 뒤(FtueStep 6 = 0~5단계를 끝냄)부터 보인다.
    ///   · 1~30단계 별조각 합계 = 640 (22단계 100 — 1장 챕터 상자 별조각 100 + 재료는 챕터 상자 팝업이 준다, S-3 · SPEC4 §2.11 #7). 2챕터 중반에 10연속 소환 1회.
    ///   · 문구는 한 줄 30pt 막대(600px)에 들어가게 16자 안팎.
    /// 리서치 표에서 바꾼 것: 13단계 "원정 1-1 보내기" → "원정 1번 보내기" (조건이 아무 원정이라 문구를 맞춤),
    /// 17단계 1-6 → 1-7 클리어 (다음 18단계 일지 퀘스트가 1-7에서 열리는 일지라, 받자마자 바로 할 수 있게).
    ///   · 11단계 "시너지 1개 켜기" → "기다림의 끝 켜기", 30단계 "시너지 2단계 달성" → "열쇠와 자물쇠 켜기":
    ///     시작 편성(지킴이 짝짝이·똑단이·찰칵이 + 덱 몽당이)이 0분부터 천 ×2·밤 ×2·수선 듀오·별이 머문 밤 산책·수선점 식구들을 켜서
    ///     "시너지 1개"는 나타나기도 전에 끝나 있고, 1-3 무렵 세우는 우두커니(천/비)가 천 ×3(2단계)을 만들어
    ///     "시너지 2단계"도 20스테이지 넘게 일찍 끝난다. 그래서 특정 조합을 가리킨다 — 11단계는 첫 만남 우두커니 + 짝짝이
    ///     (튜토리얼 11단계 이야기), 30단계는 출석부 새싹 7일째 확정 루즈리 + 찰칵이 (2챕터에 새로 생기는 조합, "7일째 돌아올 이유").
    ///     MetaDataBuilder가 시작 편성(+첫 만남 친구)만으로 충족되는 시너지 퀘스트를 경고한다.
    ///   · 18단계 "수선 일지 20점"(오늘 점수) → "수선 일지 미션 2개 받기"(평생 MissionClaim): 가이드 퀘스트는 완료를 잠가 두지 않고
    ///     조건을 계속 다시 보므로, 오늘 점수는 05:00에 0으로 돌아가 받기 전 퀘스트가 되돌아간다.
    ///   · 손가락: 이동이 팝업을 여는 7(바구니)·15(빠른 수선)·18(일지) 단계는 팝업 안 버튼을 가리킨다 (팝업 밖 대상은 어둠 아래에 깔림).
    /// 담당: M2.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>11단계 조합 "기다림의 끝" (우두커니 + 짝짝이)</summary>
        const string WaitingEndCombo = "combo_waiting_end";

        /// <summary>30단계 조합 "열쇠와 자물쇠" (찰칵이 + 루즈리)</summary>
        const string KeyAndLockCombo = "combo_key_and_lock";

        static partial void FillGuideQuests(MetaDatabase db, GameContent content)
        {
            var q = db.guideQuests;
            const string Jjak = "jjakjjak";

            // ── 1챕터 첫 걸음 (튜토리얼과 나란히) ──
            q.Add(Quest(1, "잔향이를 불러 보세요", Count(CounterKey.EchoSummon, 1),
                FeatureId.Battle, CoachTargets.DeckCard0, ArtLib.Icon("echo"), Gem(20)));
            q.Add(Quest(2, "수선점에 불을 밝혀요", Count(CounterKey.LampLit, 1),
                FeatureId.Battle, CoachTargets.LampButton, ArtLib.Icon("lamp"), Gold(200)));
            q.Add(Quest(3, StageClearText(1, 1), MetaCondition.StageCleared(1, 1),
                FeatureId.Battle, null, StageIcon(), Gold(300)));
            q.Add(Quest(4, "수선점 강화 3회", Count(CounterKey.ShopLevelUp, 3),
                FeatureId.ShopUpgrade, Targets.GrowthLevelUp1, ArtLib.UI("tab_growth"), Gem(20)));
            q.Add(Quest(5, "짝짝이 레벨 5 달성", MetaCondition.Of(MetaConditionType.LineageLevel, 5, Jjak),
                FeatureId.ShopUpgrade, Targets.GrowthLineageList, LineageArt(content, Jjak), Mat(MaterialIds.Cloth, 5)));
            q.Add(Quest(6, StageClearText(1, 2), MetaCondition.StageCleared(1, 2),
                FeatureId.Battle, null, StageIcon(), Gold(500)));
            // 이동이 바구니 팝업을 열므로 손가락은 팝업의 [받기] (전투 화면 바구니 위젯은 팝업 어둠 아래)
            q.Add(Quest(7, "수선 바구니 받기", Count(CounterKey.ChestClaim, 1),
                FeatureId.IdleChest, Targets.IdleChestClaim, BasketIcon(), Gem(30)));
            q.Add(Quest(8, StageClearText(1, 3), MetaCondition.StageCleared(1, 3),
                FeatureId.Battle, null, StageIcon(), Gold(800)));
            q.Add(Quest(9, "잃어버린 친구 찾기 1회", Count(CounterKey.GachaPull, 1),
                FeatureId.Gacha, Targets.GachaPull1, ArtLib.UI("tab_gacha"), Gem(50)));
            q.Add(Quest(10, "새 친구를 지킴이로 세우기", MetaCondition.Of(MetaConditionType.GuardiansPlaced, 4),
                FeatureId.Formation, Targets.FormationCardList, ArtLib.Icon("guardian"), Gem(30)));
            // 첫 만남 우두커니 + 짝짝이 = "기다림의 끝" (튜토리얼 11단계 "새 친구 우두커니를 지킴이로"와 이어지는 이야기)
            q.Add(Quest(11, "기다림의 끝 켜기", MetaCondition.Of(MetaConditionType.SynergyTier, 1, WaitingEndCombo),
                FeatureId.Formation, Targets.FormationSynergy, Pick(SynergyArt(WaitingEndCombo), ArtLib.Icon("resonance")),
                Mat(MaterialIds.Ribbon, 5)));
            q.Add(Quest(12, StageClearText(1, 4), MetaCondition.StageCleared(1, 4),
                FeatureId.Battle, null, StageIcon(), Gold(1200)));
            q.Add(Quest(13, "원정 1번 보내기", Count(CounterKey.ExpeditionStart, 1),
                FeatureId.Expedition, Targets.ExpeditionZoneList, ArtLib.UI("tab_expedition"), MetaReward.QuickIdleTicket(1)));
            q.Add(Quest(14, "원정 보상 받기", Count(CounterKey.ExpeditionClaim, 1),
                FeatureId.Expedition, Targets.ExpeditionZoneList, ArtLib.UI("tab_expedition"), Mat(MaterialIds.Lace, 3)));
            // 이동이 바구니 팝업을 열므로 손가락은 팝업의 [빠른 수선] ([받기]와 나란히 있어 헷갈리기 쉽다)
            q.Add(Quest(15, "빠른 수선 1회", Count(CounterKey.QuickIdle, 1),
                FeatureId.QuickIdle, Targets.IdleChestQuick, BasketIcon(), Gem(30)));
            q.Add(Quest(16, "잔향이 30번 부르기", Count(CounterKey.EchoSummon, 30),
                FeatureId.Battle, CoachTargets.DeckCard0, ArtLib.Icon("echo"), Gold(2000)));
            q.Add(Quest(17, StageClearText(1, 7), MetaCondition.StageCleared(1, 7),
                FeatureId.Battle, null, StageIcon(), Gem(30)));
            // 평생 카운터라 05:00 초기화로 되돌아가지 않는다. 일지가 열리는 1-7이면 접속 미션(10점)은 늘 받을 수 있고
            // 잔향이·불 밝히기·처치 미션도 대개 끝나 있어 [모두 받기] 한 번이면 된다 (일지가 열리기 전에는 받기가 막혀 0에서 시작)
            q.Add(Quest(18, "수선 일지 미션 2개 받기", Count(CounterKey.MissionClaim, 2),
                FeatureId.Missions, Targets.JournalClaimAll, JournalIcon(), Gem(30)));
            q.Add(Quest(19, "수선점 Lv.15 달성", MetaCondition.Of(MetaConditionType.ShopLevel, 15),
                FeatureId.ShopUpgrade, Targets.GrowthLevelUp1, ArtLib.UI("tab_growth"), MetaReward.GoldMinutes(5)));
            q.Add(Quest(20, "상점에서 1번 사기", Count(CounterKey.ShopBuy, 1),
                FeatureId.Shop, Targets.ShopProductList, ArtLib.UI("tab_shop"), Mat(MaterialIds.Wax, 3)));
            q.Add(Quest(21, "친구 레벨 합계 40", MetaCondition.Of(MetaConditionType.LineageLevelSum, 40),
                FeatureId.ShopUpgrade, Targets.GrowthLineageList, ArtLib.Icon("star"), Gem(50)));

            // 22단계: 첫 챕터 보스. 별조각 100 (+ 챕터 상자가 아직 시스템에 없으면 1장 상자 내용을 합쳐 준다 — S-3, SPEC4 §2.11 #7)
            var chapterBoss = Quest(22, "첫 챕터 보스 1-10 클리어", MetaCondition.StageCleared(1, 10),
                FeatureId.Battle, null, BossIcon(), Gem(100));
            if (!ChapterChestMovedToSystem) MergeInto(chapterBoss.rewards, ChapterChestRewards(1));
            q.Add(chapterBoss);

            // ── 2챕터: 진화와 던전으로 이어진다 ──
            q.Add(Quest(23, "진화 기억도 열어 보기", MetaCondition.Flag(MetaFlags.EvolutionTreeViewed),
                FeatureId.Evolution, null, Pick(ArtLib.Character("jjak_caravan"), ArtLib.Icon("shards")), Mat(MaterialIds.Thread, 2)));
            q.Add(Quest(24, "잔향이 던전 1층 클리어", MetaCondition.Of(MetaConditionType.DungeonFloor, 1),
                FeatureId.Dungeon, null, ArtLib.Icon("echo"), Mat(MaterialIds.Thread, 3)));
            q.Add(Quest(25, "원정 5번 보내기", Count(CounterKey.ExpeditionStart, 5),
                FeatureId.Expedition, Targets.ExpeditionZoneList, ArtLib.UI("tab_expedition"), Mat(MaterialIds.Cloth, 10)));
            q.Add(Quest(26, "짝짝이 레벨 20 달성", MetaCondition.Of(MetaConditionType.LineageLevel, 20, Jjak),
                FeatureId.ShopUpgrade, Targets.GrowthLineageList, LineageArt(content, Jjak), Gem(50)));
            // 27단계 보상과 함께 2-5 클리어로 의상이 열린다 (개방 카드가 안내)
            q.Add(Quest(27, StageClearText(2, 5), MetaCondition.StageCleared(2, 5),
                FeatureId.Battle, null, StageIcon(), Gem(50)));
            q.Add(Quest(28, "아무 진화 1회 제작", Count(CounterKey.EvolutionCraft, 1),
                FeatureId.Evolution, null, ArtLib.Icon("shards"), Gem(100)));
            q.Add(Quest(29, "계보 공명 1회", Count(CounterKey.ResonanceUp, 1),
                FeatureId.Characters, Targets.CharacterGrid, ArtLib.Icon("resonance"), MetaReward.SelectShards(10)));
            // 2챕터 마무리: 출석부 새싹 7일째 확정 루즈리 + 시작 친구 찰칵이 = "열쇠와 자물쇠". 시작 편성과 우두커니로는 켜지지 않는
            // 조합 중 확정으로 얻는 친구만으로 되는 것 (라라벨 조합은 뽑기 운). 루즈리를 만나기 전이면 줄이 출석부를 기다리게 한다
            q.Add(Quest(30, "열쇠와 자물쇠 켜기", MetaCondition.Of(MetaConditionType.SynergyTier, 1, KeyAndLockCombo),
                FeatureId.Formation, Targets.FormationSynergy, Pick(SynergyArt(KeyAndLockCombo), ArtLib.Icon("resonance")), Gem(50)));

            // ── 31단계부터: 반복 ("스테이지 X 클리어" 2스테이지마다 / "수선점 Lv +10" / "친구 레벨 +5", 단추 5분치 + 별조각 20) ──
            db.guideRepeat = new GuideRepeatConfig
            {
                enabled = true,
                stageInterval = 2,
                shopLevelStep = 10,
                lineageLevelStep = 5,
                goldMinutes = 5,
                gem = 20,
                icon = Pick(MetaArt("quest_repeat"), ArtLib.Icon("quest"), ArtLib.Icon("star")),
            };

            // 줄은 튜토리얼 5단계(첫 보스)를 끝낸 뒤부터 보인다. 옛 세이브·건너뛰기는 튜토리얼이 끝난 것으로 보므로 바로 보인다
            db.guideVisible = MetaCondition.Of(MetaConditionType.FtueStep, FtueStepGuideVisible);
        }

        /// <summary>
        /// S-3 (SPEC4 §2.11 #7): 1장 챕터 상자가 ChapterChestSystem(P4-1, 챕터 상자 팝업)으로 옮겨 갔는지.
        /// false = 지금: 22단계가 1장 상자 내용(MetaContent.ChapterChests 부록 B.2)을 함께 준다.
        /// true = 22단계는 별조각 100만, 상자는 챕터 상자 팝업이 연다. P4-1의 챕터 상자가 들어오는 <b>같은 착지 창</b>에서만 바꾼다
        /// (혼자 먼저 바꾸면 1장 재료가 사라지고, 늦게 바꾸면 두 번 받는다)
        /// </summary>
        static readonly bool ChapterChestMovedToSystem = true; // S-3: P4-1 b6 (ChapterChestSystem) 2026-11-05 15:51 — 같은 창에서 바꿈

        static GuideQuestDef Quest(int number, string text, MetaCondition condition, FeatureId nav, string focus, Sprite icon,
            params MetaReward[] rewards) => new()
        {
            id = $"gq_{number:00}",
            text = text,
            condition = condition,
            rewards = Rewards(rewards),
            navFeature = nav,
            focusTarget = focus,
            icon = Pick(icon, ArtLib.Icon("quest"), ArtLib.Icon("star")),
        };

        static MetaCondition Count(CounterKey key, long target) => MetaCondition.Count(key, target);

        static Sprite StageIcon() => Pick(MetaArt("quest_stage"), ArtLib.Icon("lamp"), ArtLib.Icon("stage_calm"));

        static Sprite BasketIcon() => Pick(ArtLib.UI("idle_basket_1"), MetaArt("feature_idle_chest"), ArtLib.Icon("reward_generic"));

        static Sprite JournalIcon() => Pick(MetaArt("feature_missions"), ArtLib.Icon("journal"), ArtLib.Icon("quest"));

        /// <summary>보스 아이콘 (ART-UPGRADE #3: Meta/mission_boss → 큰그림자 → 스테이지)</summary>
        static Sprite BossIcon() => Pick(MetaArt("mission_boss"), ArtLib.Enemy("boss_shadow"), StageIcon());
    }
}
