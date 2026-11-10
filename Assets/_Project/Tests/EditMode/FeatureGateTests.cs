using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>기능 개방 (F-03): 조건(OR) 충족 → 열림 1회 + 카드 1회 + 보상 + 새로 열림 점, 옛 세이브 조용히, 안내 문구, 강제 개방</summary>
    public class FeatureGateTests : MetaSystemsTestBase
    {
        readonly List<FeatureId> _unlocked = new();

        /// <summary>NUnit은 픽스처 인스턴스 하나로 모든 테스트를 돌린다 — 기록은 테스트마다 비운다</summary>
        [SetUp]
        public void ClearUnlocked() => _unlocked.Clear();

        void Listen() => Features.Unlocked += f => _unlocked.Add(f);

        [Test]
        public void NoDefinitionOrNoConditions_IsOpenWithoutCard()
        {
            AddFeature(FeatureId.Characters);
            Rebuild();
            Listen();

            Assert.IsTrue(Features.IsUnlocked(FeatureId.Battle), "정의 없음 = 열림");
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Characters), "조건 없음 = 열림");
            Features.Evaluate();

            CollectionAssert.IsEmpty(_unlocked);
            Assert.IsTrue(Popups.IsEmpty);
            Assert.IsFalse(Features.IsNew(FeatureId.Characters), "처음부터 열린 기능에는 점이 없다");
            Assert.AreEqual(string.Empty, Features.GetLockedHint(FeatureId.Characters));
            Assert.IsTrue(Features.TryUse(FeatureId.Characters, out var message));
            Assert.AreEqual(string.Empty, message);
        }

        [Test]
        public void StageCondition_OpensOnce_WithCardDotAndRewards()
        {
            var def = AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            def.rewards.Add(MetaReward.Gem(50));
            Rebuild();
            Listen();

            SetCleared(3);
            Assert.AreEqual(0, Features.Evaluate());
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Expedition));

            SetCleared(4);
            Assert.AreEqual(1, Features.Evaluate());
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Expedition));
            CollectionAssert.AreEqual(new[] { FeatureId.Expedition }, _unlocked);
            Assert.IsTrue(Popups.TryPeek(out var card));
            Assert.AreEqual(MetaPopupKind.FeatureUnlock, card.Kind);
            Assert.AreEqual(FeatureId.Expedition, card.Feature);
            Assert.AreEqual(50, Gem, "개방 보상");
            Assert.IsTrue(Features.IsNew(FeatureId.Expedition));

            Assert.AreEqual(0, Features.Evaluate(), "한 번 열린 기능은 다시 열리지 않는다");
            Assert.AreEqual(1, _unlocked.Count);
            Assert.AreEqual(1, Popups.Count, "카드는 한 번");

            Features.MarkSeen(FeatureId.Expedition);
            Assert.IsFalse(Features.IsNew(FeatureId.Expedition));

            SetCleared(1);
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Expedition), "스테이지가 내려가도 닫히지 않는다");
        }

        [Test]
        public void AnyCondition_Opens()
        {
            AddFeature(FeatureId.Formation, MetaCondition.Of(MetaConditionType.FtueStep, 11), MetaCondition.StageCleared(1, 3));
            Rebuild();

            Data.meta.ftue.step = 11;
            Features.Evaluate();
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Formation), "튜토리얼 단계로");

            Data.meta.features.unlocked.Clear();
            Data.meta.ftue.step = 0;
            SetCleared(3);
            Features.Evaluate();
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Formation), "또는 1-3 클리어로");
        }

        [Test]
        public void ProgressFlush_TriggersEvaluate()
        {
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            Rebuild();
            Listen();

            SetCleared(3);
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Gacha), "평가 전");
            Progress.Increment(CounterKey.StageClear);
            Progress.Flush();

            Assert.IsTrue(Features.IsUnlocked(FeatureId.Gacha));
            CollectionAssert.AreEqual(new[] { FeatureId.Gacha }, _unlocked);
        }

        [Test]
        public void LegacySave_SyncsSilently_ThenNewUnlocksShowCards()
        {
            AddFeature(FeatureId.IdleChest, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            Rebuild();
            Listen();
            Data.meta.features.silentSyncPending = true;
            SetCleared(5);

            Assert.AreEqual(2, Features.Evaluate());
            Assert.IsTrue(Features.IsUnlocked(FeatureId.IdleChest));
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Gacha));
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Shop));
            CollectionAssert.IsEmpty(_unlocked, "조용한 동기화는 알리지 않는다");
            Assert.IsTrue(Popups.IsEmpty, "카드 없음");
            Assert.IsFalse(Features.IsNew(FeatureId.Gacha), "봤음 처리");
            Assert.IsFalse(Data.meta.features.silentSyncPending);

            SetCleared(8);
            Features.Evaluate();
            CollectionAssert.AreEqual(new[] { FeatureId.Shop }, _unlocked, "그 뒤로 열리는 기능은 평소처럼");
            Assert.IsTrue(Popups.Contains(MetaPopupKind.FeatureUnlock));
        }

        [Test]
        public void LegacyPlayerData_MigratesToSilentSync()
        {
            var legacy = new PlayerData { version = 2, meta = null, level = 12 };
            legacy.Sanitize();

            Assert.IsTrue(legacy.meta.features.silentSyncPending);
            Assert.IsTrue(legacy.meta.ftue.done, "진행한 플레이어는 튜토리얼을 끝낸 것으로");
            Assert.AreEqual(PlayerData.CurrentVersion, legacy.version);
        }

        [Test]
        public void LockedTexts_FromConditionOrContent()
        {
            AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            var shop = AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            shop.lockedHint = "상점";
            shop.lockedToast = "조금만 더 가면 상점이 열려요";
            AddFeature(FeatureId.Attendance, MetaCondition.Of(MetaConditionType.FtueDone, 1), MetaCondition.StageCleared(1, 5));
            AddFeature(FeatureId.Events, MetaCondition.Of(MetaConditionType.InstallDays, 3));
            Rebuild();

            Assert.AreEqual("1-4", Features.GetLockedHint(FeatureId.Expedition));
            Assert.AreEqual("스테이지 1-4를 클리어하면 열려요", Features.GetRequirementText(FeatureId.Expedition));
            Assert.AreEqual("스테이지 1-3을 클리어하면 열려요", Features.GetRequirementText(FeatureId.Gacha), "받침에 맞는 조사");
            Assert.AreEqual("상점", Features.GetLockedHint(FeatureId.Shop));
            Assert.AreEqual("조금만 더 가면 상점이 열려요", Features.GetRequirementText(FeatureId.Shop));
            Assert.AreEqual("스테이지 1-5를 클리어하면 열려요", Features.GetRequirementText(FeatureId.Attendance), "스테이지 조건을 먼저 안내");
            Assert.AreEqual("수선점을 연 지 3일째에 열려요", Features.GetRequirementText(FeatureId.Events));

            Assert.IsFalse(Features.TryUse(FeatureId.Expedition, out var toast));
            Assert.AreEqual("스테이지 1-4를 클리어하면 열려요", toast);
        }

        [Test]
        public void ForceUnlock_CardOnlyWhenAsked()
        {
            AddFeature(FeatureId.Dungeon, MetaCondition.StageCleared(1, 10));
            AddFeature(FeatureId.Evolution, MetaCondition.StageCleared(1, 10));
            Rebuild();
            Listen();

            Features.ForceUnlock(FeatureId.Dungeon, showCard: false);
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Dungeon));
            Assert.IsTrue(Popups.IsEmpty);
            Features.ForceUnlock(FeatureId.Evolution, showCard: true);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.FeatureUnlock));
            CollectionAssert.AreEqual(new[] { FeatureId.Dungeon, FeatureId.Evolution }, _unlocked);

            Features.ForceUnlock(FeatureId.Evolution, showCard: true);
            Assert.AreEqual(2, _unlocked.Count, "이미 열린 기능은 무시");
        }

        [Test]
        public void ShowCardFalse_OpensWithDotOnly()
        {
            var def = AddFeature(FeatureId.Speed2x, MetaCondition.StageCleared(1, 6));
            def.showCard = false;
            Rebuild();

            SetCleared(6);
            Features.Evaluate();

            Assert.IsTrue(Features.IsUnlocked(FeatureId.Speed2x));
            Assert.IsTrue(Popups.IsEmpty);
            Assert.IsTrue(Features.IsNew(FeatureId.Speed2x));
        }

        [Test]
        public void FeatureUnlockedCondition_ChainsInSameEvaluate()
        {
            AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            AddFeature(FeatureId.ExpeditionBatch, MetaCondition.Of(MetaConditionType.FeatureUnlocked, 1, nameof(FeatureId.Expedition)));
            Rebuild();

            SetCleared(4);
            Assert.AreEqual(2, Features.Evaluate());
            Assert.IsTrue(Features.IsUnlocked(FeatureId.ExpeditionBatch));
        }

        [Test]
        public void DungeonFloorCondition_OpensByFloor_NotByStage()
        {
            // IDLE-05 던전 한 번에 소탕 (콘텐츠 행: 던전 5층 돌파 — RM-1 요청으로 들어온 '정의 없음' 정리)
            var def = AddFeature(FeatureId.DungeonBatchSweep, MetaCondition.Of(MetaConditionType.DungeonFloor, 5));
            def.lockedHint = "던전 5층";
            def.lockedToast = "잔향이 던전 5층을 돌파하면 열려요";
            Rebuild();
            Listen();

            SetCleared(40);
            Data.dungeon.highestFloor = 4;
            Assert.AreEqual(0, Features.Evaluate(), "스테이지를 많이 깨도 던전 4층이면 잠김");
            Assert.IsFalse(Features.IsUnlocked(FeatureId.DungeonBatchSweep));
            Assert.AreEqual("던전 5층", Features.GetLockedHint(FeatureId.DungeonBatchSweep));
            Assert.IsFalse(Features.TryUse(FeatureId.DungeonBatchSweep, out var toast));
            Assert.AreEqual("잔향이 던전 5층을 돌파하면 열려요", toast);

            Data.dungeon.highestFloor = 5;
            Assert.AreEqual(1, Features.Evaluate());
            Assert.IsTrue(Features.IsUnlocked(FeatureId.DungeonBatchSweep));
            CollectionAssert.AreEqual(new[] { FeatureId.DungeonBatchSweep }, _unlocked);
        }

        [Test]
        public void NewFeaturesAndNextLocked()
        {
            AddFeature(FeatureId.IdleChest, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            Rebuild();

            Assert.IsTrue(Features.TryGetNextLocked(out var next));
            Assert.AreEqual(FeatureId.IdleChest, next.feature);

            SetCleared(3);
            Features.Evaluate();
            var fresh = new List<FeatureId>();
            Features.GetNewFeatures(fresh);
            CollectionAssert.AreEquivalent(new[] { FeatureId.IdleChest, FeatureId.Gacha }, fresh);

            Assert.IsTrue(Features.TryGetNextLocked(out next));
            Assert.AreEqual(FeatureId.Expedition, next.feature);
            SetCleared(4);
            Features.Evaluate();
            Assert.IsFalse(Features.TryGetNextLocked(out _));
        }
    }
}
