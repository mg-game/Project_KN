using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>가이드 퀘스트 (Q-01): 미리 한 진행 인정(A5 즉시 받기), 받기 → 보상·카운터·다음, 보임 조건, 반복 단계(스테이지 +2 / 수선점 +10 / 친구 +5)</summary>
    public class GuideQuestTests : MetaSystemsTestBase
    {
        static GuideQuestDef Quest(string id, MetaCondition condition, params MetaReward[] rewards) => new GuideQuestDef
        {
            id = id,
            text = id,
            condition = condition,
            rewards = new List<MetaReward>(rewards),
        };

        void SetupChain()
        {
            MetaDb.guideQuests = new List<GuideQuestDef>
            {
                Quest("gq_01", MetaCondition.Count(CounterKey.EchoSummon, 1), MetaReward.Gem(20)),
                Quest("gq_02", MetaCondition.Count(CounterKey.LampLit, 1), MetaReward.Gold(200)),
                Quest("gq_03", MetaCondition.StageCleared(1, 1), MetaReward.Gold(300)),
                Quest("gq_04", MetaCondition.Count(CounterKey.ShopLevelUp, 3), MetaReward.Gem(20)),
            };
            Rebuild();
        }

        [Test]
        public void EarlierProgress_CountsImmediately()
        {
            Progress.Increment(CounterKey.EchoSummon);
            Progress.Increment(CounterKey.LampLit);
            SetupChain();

            Assert.AreEqual("gq_01", GuideQuest.Current.id);
            Assert.IsTrue(GuideQuest.IsComplete, "FTUE에서 이미 한 일은 바로 받기 (A5)");
            Assert.IsTrue(GuideQuest.TryClaim(out _));
            Assert.IsTrue(GuideQuest.IsComplete, "2단계도 이미 끝남");
        }

        [Test]
        public void TryClaim_GrantsRewards_CountsAndAdvances()
        {
            SetupChain();
            GuideQuestDef claimedDef = null;
            List<MetaReward> claimedRewards = null;
            GuideQuest.Claimed += (def, rewards) => { claimedDef = def; claimedRewards = rewards; };

            Assert.IsFalse(GuideQuest.IsComplete);
            Assert.IsFalse(GuideQuest.TryClaim(out var none), "완료 전에는 못 받음");
            Assert.IsNull(none);
            Assert.AreEqual("0/1", GuideQuest.ProgressText);

            Progress.Increment(CounterKey.EchoSummon);
            Assert.IsTrue(GuideQuest.IsComplete);
            Assert.AreEqual("1/1", GuideQuest.ProgressText);
            Assert.IsTrue(GuideQuest.TryClaim(out var granted));

            Assert.AreEqual(20, Gem);
            Assert.AreEqual(20, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.AreEqual("gq_01", claimedDef.id);
            Assert.AreSame(granted, claimedRewards);
            Assert.AreEqual(1, GuideQuest.Index);
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.GuideQuestClaim));
            Assert.AreEqual("gq_02", GuideQuest.Current.id);
            Assert.IsFalse(GuideQuest.IsComplete);
        }

        [Test]
        public void StageQuest_ShowsStageProgress()
        {
            SetupChain();
            Data.meta.guide.index = 2;

            Assert.AreEqual("-/1-1", GuideQuest.ProgressText);
            SetCleared(1);
            Assert.IsTrue(GuideQuest.IsComplete);
            Assert.AreEqual("1-1/1-1", GuideQuest.ProgressText);
        }

        [Test]
        public void Visibility_FollowsGuideVisibleCondition()
        {
            SetupChain();
            MetaDb.guideVisible = MetaCondition.Of(MetaConditionType.FtueStep, 6);
            Rebuild();

            Assert.IsFalse(GuideQuest.IsVisible, "첫 보스 전에는 숨김");
            Data.meta.ftue.step = 6;
            Assert.IsTrue(GuideQuest.IsVisible);

            Data.meta.guide.index = 4;
            MetaDb.guideRepeat.enabled = false;
            Assert.IsNull(GuideQuest.Current, "목록이 끝났고 반복도 꺼짐");
            Assert.IsFalse(GuideQuest.IsVisible);
        }

        [Test]
        public void Refresh_RaisesChangedOnlyWhenStateChanges()
        {
            SetupChain();
            int changed = 0;
            GuideQuest.Changed += () => changed++;
            Progress.MarkDirty();
            Progress.Flush();
            int baseline = changed;

            Progress.MarkDirty();
            Progress.Flush();
            Assert.AreEqual(baseline, changed, "그대로면 울리지 않는다");

            Progress.Increment(CounterKey.EchoSummon);
            Progress.Flush();
            Assert.AreEqual(baseline + 1, changed, "완료로 바뀜");
        }

        [Test]
        public void RepeatSteps_RotateStageShopLineage_WithBaseline()
        {
            SetupChain();
            Characters.Grant("sock");
            Data.lineages["sock"].level = 7;
            Data.level = 12;
            SetCleared(5);
            Data.meta.guide.index = MetaDb.guideQuests.Count;

            // 1) 스테이지: 지금 클리어 5 → 목표 1-7 (+2)
            Assert.IsTrue(GuideQuest.IsRepeat);
            var repeat = GuideQuest.Current;
            Assert.AreEqual("스테이지 1-7 클리어", repeat.text);
            Assert.AreEqual(5, Data.meta.guide.repeatBaseline);
            Assert.AreEqual(0, GuideQuest.CurrentValue);
            Assert.AreEqual(2, GuideQuest.CurrentTarget);
            Assert.AreEqual("0/2", GuideQuest.ProgressText);
            SetCleared(6);
            Assert.AreEqual("1/2", GuideQuest.ProgressText);
            Assert.IsFalse(GuideQuest.IsComplete);
            SetCleared(7);
            Assert.IsTrue(GuideQuest.IsComplete);
            Assert.IsTrue(GuideQuest.TryClaim(out var granted));
            Assert.Greater(AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gold), 0, "단추 5분치");
            Assert.AreEqual(20, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem), "별조각 20");

            // 2) 수선점 Lv +10
            Assert.AreSame(repeat, GuideQuest.Current, "반복 정의는 같은 인스턴스를 재사용");
            Assert.AreEqual("수선점 Lv.22 달성", repeat.text);
            Assert.AreEqual(MetaConditionType.ShopLevel, repeat.condition.type);
            Data.level = 21;
            Assert.IsFalse(GuideQuest.IsComplete);
            Data.level = 22;
            Assert.IsTrue(GuideQuest.TryClaim(out _));

            // 3) 친구 레벨 +5 (가장 높은 계보)
            Assert.AreEqual("친구 레벨 12 달성", GuideQuest.Current.text);
            Data.lineages["sock"].level = 12;
            Assert.IsTrue(GuideQuest.TryClaim(out _));

            // 4) 다시 스테이지
            Assert.AreEqual("스테이지 1-9 클리어", GuideQuest.Current.text);
            Assert.AreEqual(3, Progress.GetLifetime(CounterKey.GuideQuestClaim));
        }

        [Test]
        public void RepeatReward_GoldMinutesUseGoldPerSecond()
        {
            SetupChain();
            Data.meta.guide.index = MetaDb.guideQuests.Count;
            SetCleared(0);
            _ = GuideQuest.Current; // 반복 단계에 들어가며 기준값을 정한다
            SetCleared(2);
            double gps = Growth.GoldPerSecond;

            Assert.IsTrue(GuideQuest.TryClaim(out var granted));

            Assert.AreEqual(System.Math.Floor(gps * 60 * MetaDb.guideRepeat.goldMinutes), AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gold));
        }

        [Test]
        public void RepeatClaimed_PassesSnapshotOfStep()
        {
            SetupChain();
            Data.meta.guide.index = MetaDb.guideQuests.Count;
            SetCleared(0);
            _ = GuideQuest.Current; // 반복 단계에 들어가며 기준값을 정한다
            SetCleared(2);
            GuideQuestDef claimed = null;
            GuideQuest.Claimed += (def, _) => claimed = def;

            GuideQuest.TryClaim(out _);

            Assert.AreEqual("스테이지 1-2 클리어", claimed.text, "받은 순간의 내용");
            Assert.AreNotSame(claimed, GuideQuest.Current);
        }

        [Test]
        public void MaxLevelRepeat_DoesNotStall()
        {
            SetupChain();
            Characters.Grant("sock");
            Data.lineages["sock"].level = Characters.MaxLevel;
            Data.meta.guide.index = MetaDb.guideQuests.Count + 2; // 친구 레벨 단계

            Assert.AreEqual(0, GuideQuest.CurrentTarget, "최대 레벨이면 더 올릴 것이 없다");
            Assert.IsTrue(GuideQuest.IsComplete, "멈추지 않고 넘어갈 수 있다");
        }

        /// <summary>
        /// S-3 (SPEC4 §2.11 #7 "두 번 받지 않는다"), 코드 원본 콘텐츠(아이템 · 메타)로: 1-10을 깨고 22단계를 받은 뒤 챕터 상자 팝업을 연다 —
        /// 22단계 몫은 별조각 100뿐, 1장 상자 내용(부록 B.2)은 상자에서 정확히 한 번, 두 번째 열기는 없음.
        /// 새 세이브가 부팅한 뒤의 흐름이다 (옛 콘텐츠에서 22단계를 받은 세이브는 ChapterChestTests.OldSave_ThatClaimedQuest22_DoesNotGetChapterOneTwice)
        /// </summary>
        [Test]
        public void BuiltContent_Quest22_AndChapterOneChest_PayChapterOneOnce()
        {
            UnityEngine.Object.DestroyImmediate(Items);
            Items = UnityEngine.ScriptableObject.CreateInstance<ItemDatabase>();
            Items.items = ContentMetaTests.BuildItems();
            ContentMetaTests.ApplyMetaContent(MetaDb, null, false);
            CreateSystems(new System.Random(1234)); // 실제 재료 표로 인벤토리 · 보상 · 메타 시스템을 다시 만든다
            // 부팅 (MetaSession이 모듈마다 부른다): 상자 줄이 있는 콘텐츠로 처음 도는 새 세이브(22단계 전)는 여기서 S-3 옛 세이브 확인 표시만 세운다
            ChapterChests.AfterStartup(true);
            Assert.IsFalse(ChapterChests.IsChapterChestOpened(1), "새 세이브의 1장 상자는 닫힌 채");

            var chest = MetaDb.GetChapterChest(1);
            Assert.IsNotNull(chest, "1장 상자 줄");
            double chestGem = 0;
            var chestItems = new Dictionary<string, int>();
            foreach (var reward in chest.rewards)
            {
                if (reward.type == MetaRewardType.Currency && reward.currency == CurrencyType.Gem) chestGem += reward.amount;
                if (reward.type == MetaRewardType.Item) chestItems[reward.itemId] = (chestItems.TryGetValue(reward.itemId, out int n) ? n : 0) + (int)reward.amount;
            }
            Assert.Greater(chestGem, 0.0, "1장 상자 별조각");
            Assert.IsNotEmpty(chestItems, "1장 상자 재료");
            var before = new Dictionary<string, int>();
            foreach (var pair in chestItems) before[pair.Key] = ItemCount(pair.Key);

            Data.meta.guide.index = 21;
            Assert.AreEqual("gq_22", GuideQuest.Current.id);
            Assert.IsFalse(GuideQuest.IsComplete, "1-10 전");
            SetCleared(10);
            Assert.IsTrue(GuideQuest.IsComplete, "1-10 클리어");

            double gems = Gem;
            Assert.IsTrue(GuideQuest.TryClaim(out _));
            Assert.AreEqual(100, Gem - gems, 1e-9, "22단계 몫은 별조각 100 (S-3 뒤 — 상자 내용은 챕터 상자 팝업)");
            foreach (var pair in chestItems) Assert.AreEqual(before[pair.Key], ItemCount(pair.Key), $"{pair.Key}: 1장 재료는 22단계에 없다");

            // 챕터 보스 클리어 → 상자 팝업 → [받기]
            Data.battle.stage = 10;
            Battle.Restart();
            ChapterChests.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
            Assert.IsTrue(Popups.Contains(MetaPopupKind.ChapterChest), "1장 상자 팝업");
            Assert.IsNotNull(ChapterChests.OpenChapterChest(1));
            Assert.IsNull(ChapterChests.OpenChapterChest(1), "두 번째 열기는 없음");

            Assert.AreEqual(100 + chestGem, Gem - gems, 1e-9, "별조각: 22단계 100 + 1장 상자 한 번");
            foreach (var pair in chestItems) Assert.AreEqual(before[pair.Key] + pair.Value, ItemCount(pair.Key), $"{pair.Key}: 1장 상자 한 번만");
        }
    }
}
