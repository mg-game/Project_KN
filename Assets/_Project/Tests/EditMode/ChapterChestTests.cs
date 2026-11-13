using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 챕터 상자 (PRG-06 핵심, SPEC4 §8.5 · App. B.2): 챕터 보스(x-10) 클리어 → 요청 (한 번만 열림), 6장부터 chapter 0 기본 줄, 내용이 없으면 없음,
    /// 동화책보다 먼저. 잃어버린 상자는 확장 항목이라 꺼져 있다 (늘 false).
    /// </summary>
    public class ChapterChestTests : MetaSystemsTestBase
    {
        void AddChests()
        {
            MetaDb.lostChest.chapterChests.Add(new ChapterChestDef
            {
                chapter = 1, rewards = new List<MetaReward> { MetaReward.Gem(100), MetaReward.Item("m_cloth", 10), MetaReward.Item("m_thread", 2) },
            });
            MetaDb.lostChest.chapterChests.Add(new ChapterChestDef { chapter = 0, rewards = new List<MetaReward> { MetaReward.Gem(150) } });
            MetaDb.storyChapters.Add(new StoryChapterDef { chapter = 1, title = "첫 밤", firstReadRewards = new List<MetaReward> { MetaReward.Gem(50) } });
            Rebuild();
        }

        void ClearStage(int stage)
        {
            Data.battle.stage = stage;
            Data.battle.highestStage = stage + 1;
            Battle.Restart();
            Assert.AreEqual(stage, Battle.Sim.Level);
            ChapterChests.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
            Story.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
        }

        [Test]
        public void ChapterBossClear_EnqueuesChest_OpensOnce()
        {
            AddChests();
            Data.meta.ftue.done = true;
            ClearStage(9);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.ChapterChest), "일반 스테이지는 아님");

            ClearStage(10);
            Assert.IsTrue(Popups.TryPeek(out var first));
            Assert.AreEqual(MetaPopupKind.ChapterChest, first.Kind, "챕터 상자가 동화책보다 먼저");
            Assert.AreEqual(1, first.Value);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.StoryPage));
            Assert.AreEqual(3, ChapterChests.PeekChapterChest(1).Count);

            double gems = Gem;
            var granted = ChapterChests.OpenChapterChest(1);
            Assert.IsNotNull(granted);
            Assert.AreEqual(100, Gem - gems);
            Assert.AreEqual(10, ItemCount("m_cloth"));
            Assert.AreEqual(2, ItemCount("m_thread"));
            Assert.IsTrue(ChapterChests.IsChapterChestOpened(1));
            Assert.IsNull(ChapterChests.OpenChapterChest(1), "한 번만");

            Popups.Remove(MetaPopupKind.ChapterChest);
            ClearStage(10);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.ChapterChest), "열린 상자는 다시 요청하지 않는다");
        }

        [Test]
        public void SixthChapterOn_UsesDefaultRow()
        {
            AddChests();
            Assert.AreEqual(150, ChapterChests.PeekChapterChest(6)[0].amount, "chapter 0 기본 줄");
            Assert.AreEqual(150, ChapterChests.PeekChapterChest(12)[0].amount);
            Assert.IsEmpty(ChapterChests.PeekChapterChest(0));

            ClearStage(60);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.ChapterChest));
            Assert.IsNotNull(ChapterChests.OpenChapterChest(6));
            Assert.IsNotNull(ChapterChests.OpenChapterChest(7), "기본 줄도 챕터마다 한 번씩");
        }

        [Test]
        public void NoContent_NoChest()
        {
            ClearStage(10);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.ChapterChest));
            Assert.IsNull(ChapterChests.OpenChapterChest(1));
            Assert.IsEmpty(ChapterChests.PeekChapterChest(1));
        }

        [Test]
        public void FailedClear_NoChest()
        {
            AddChests();
            Data.battle.stage = 10;
            Battle.Restart();
            ChapterChests.OnStageEnded(BattleOutcome.Failed, new List<RewardData>());
            Assert.IsFalse(Popups.Contains(MetaPopupKind.ChapterChest));
        }

        [Test]
        public void OldSave_ThatClaimedQuest22_DoesNotGetChapterOneTwice()
        {
            AddChests();
            Data.meta.ftue.done = true;
            Data.meta.guide.index = ChapterChestSystem.GuideQuestChapterOneClaimed; // 옛 콘텐츠에서 22단계(1장 상자 내용 포함)를 받은 세이브
            ClearStage(10); // 스테이지 목록 · 챕터 지도 · 2-1에서 밀려나 1-10을 다시 깸
            Assert.IsFalse(Popups.Contains(MetaPopupKind.ChapterChest), "22단계가 이미 준 1장 상자는 다시 주지 않는다");
            Assert.IsTrue(ChapterChests.IsChapterChestOpened(1));
            Assert.IsTrue(Data.flags.Contains(ChapterChestSystem.LegacyChapterOneFlag));
            double gems = Gem;
            Assert.IsNull(ChapterChests.OpenChapterChest(1));
            Assert.AreEqual(gems, Gem);

            ClearStage(20);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.ChapterChest), "다른 장 상자는 그대로");
            Assert.IsNotNull(ChapterChests.OpenChapterChest(2));
        }

        [Test]
        public void ChapterOneChest_StillComesBack_WhenQuest22WasClaimedAfterTheChestArrived()
        {
            AddChests();
            Data.meta.ftue.done = true;
            ChapterChests.AfterStartup(true); // 상자 줄이 있는 콘텐츠로 처음 돎 — 22단계 전
            Assert.IsTrue(Data.flags.Contains(ChapterChestSystem.LegacyChapterOneFlag));
            Assert.IsFalse(ChapterChests.IsChapterChestOpened(1));

            Data.meta.guide.index = ChapterChestSystem.GuideQuestChapterOneClaimed; // 그 뒤 22단계(별조각 100만)를 받았고 상자 팝업은 놓침
            ClearStage(10);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.ChapterChest), "1장 상자는 다시 클리어하면 받는다");
            Assert.IsNotNull(ChapterChests.OpenChapterChest(1));
        }

        [Test]
        public void OldContent_WithoutChestRows_WaitsForTheChestContent()
        {
            Data.meta.guide.index = ChapterChestSystem.GuideQuestChapterOneClaimed;
            ChapterChests.AfterStartup(true); // 상자 줄 없음 = 22단계가 아직 상자 내용을 주는 옛 콘텐츠
            Assert.IsFalse(Data.flags.Contains(ChapterChestSystem.LegacyChapterOneFlag), "표시를 세우지 않고 기다린다");
            Assert.IsFalse(ChapterChests.IsChapterChestOpened(1));

            AddChests(); // 상자 줄이 들어온 콘텐츠 (G3)
            ChapterChests.AfterStartup(true);
            Assert.IsTrue(ChapterChests.IsChapterChestOpened(1), "그때 22단계를 받은 세이브 = 1장 상자를 연 것으로");
            double gems = Gem;
            Assert.IsNull(ChapterChests.OpenChapterChest(1));
            Assert.AreEqual(gems, Gem, "다시 주지 않는다");
        }

        [Test]
        public void LostChest_IsOff()
        {
            var rewards = new List<RewardData>();
            Data.meta.ftue.done = true;
            MetaDb.lostChest.dropChance = 0f;
            AddLostTables();
            Assert.IsFalse(ChapterChests.TryRollLostChest(5, rewards), "확률 0 (G3 전 콘텐츠) — 지금 재료 굴림 그대로");
            Assert.IsEmpty(rewards);
            ChapterChests.Connect();
            Assert.IsNull(Battle.ClearDropHook, "확장 항목이라 훅을 아직 잇지 않는다");
        }

        void AddLostTables()
        {
            MetaDb.lostChest.tables.Clear();
            MetaDb.lostChest.tables.Add(new LostChestTable
            {
                fromStage = 1, toStage = 10,
                entries = new List<LostChestEntry>
                {
                    new LostChestEntry { itemId = "m_cloth", min = 1, max = 2, weight = 3 },
                    new LostChestEntry { itemId = "m_thread", min = 1, max = 1, weight = 1 },
                },
            });
            MetaDb.lostChest.tables.Add(new LostChestTable
            {
                fromStage = 11, toStage = 9999, entries = new List<LostChestEntry> { new LostChestEntry { itemId = "m_thread", min = 2, max = 3, weight = 1 } },
            });
        }

        [Test]
        public void LostChest_DropRate_SeededStatistics()
        {
            Data.meta.ftue.done = true;
            MetaDb.lostChest.dropChance = 0.6f;
            AddLostTables();
            var random = new System.Random(42);
            int drops = 0, cloth = 0;
            const int Rolls = 5000;
            for (int i = 0; i < Rolls; i++)
            {
                var rewards = new List<RewardData>();
                Assert.IsTrue(ChapterChests.TryRollLostChest(5, rewards, random), "켜져 있으면 기본 굴림을 대신한다");
                if (rewards.Count == 0) continue;
                drops++;
                Assert.AreEqual(1, rewards.Count, "한 칸");
                if (rewards[0].itemId == "m_cloth")
                {
                    cloth++;
                    Assert.That(rewards[0].amount, Is.InRange(1, 2));
                }
            }
            Assert.AreEqual(0.6, (double)drops / Rolls, 0.02, "확률 0.6 ± 2%");
            Assert.AreEqual(0.75, (double)cloth / drops, 0.03, "가중치 3 : 1");
        }

        [Test]
        public void LostChest_TableByStageRange_AndFtueOff()
        {
            MetaDb.lostChest.dropChance = 1f;
            AddLostTables();
            var rewards = new List<RewardData>();
            Assert.IsFalse(ChapterChests.TryRollLostChest(5, rewards, new System.Random(1)), "튜토리얼 전에는 false (지금 굴림)");

            Data.meta.ftue.done = true;
            Assert.IsTrue(ChapterChests.TryRollLostChest(25, rewards, new System.Random(1)));
            Assert.AreEqual("m_thread", rewards[0].itemId, "11~ 표");
            Assert.That(rewards[0].amount, Is.InRange(2, 3));

            MetaDb.lostChest.tables.Clear();
            Assert.IsFalse(ChapterChests.TryRollLostChest(25, new List<RewardData>(), new System.Random(1)), "표가 없으면 지금 굴림");
        }
    }
}
