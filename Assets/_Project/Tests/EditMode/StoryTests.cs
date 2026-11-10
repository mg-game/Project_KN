using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 동화책 (CR-05, SPEC4 §8.1): 챕터 보스 첫 클리어(펼쳐 본 적 없음)에만 · 챕터 상자 다음 · 선물은 한 번 · 튜토리얼 중이면 미룸 · 콘텐츠 없는 장은 건너뜀.
    /// </summary>
    public class StoryTests : MetaSystemsTestBase
    {
        void AddStory()
        {
            MetaDb.storyChapters.Add(new StoryChapterDef
            {
                chapter = 1, title = "첫 밤", place = "골목",
                panels = new List<StoryPanelDef> { new StoryPanelDef { text = "하나" }, new StoryPanelDef { text = "둘" }, new StoryPanelDef { text = "셋" } },
                firstReadRewards = new List<MetaReward> { MetaReward.Gem(50) },
            });
            Rebuild();
        }

        void Clear(int stage)
        {
            Data.battle.stage = stage;
            Battle.Restart();
            Story.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
        }

        [Test]
        public void FirstChapterBossClear_EnqueuesPage_OnlyUntilSeen()
        {
            AddStory();
            Data.meta.ftue.done = true;
            Clear(5);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.StoryPage), "x-10이 아니면 아님");

            Clear(10);
            Assert.IsTrue(Popups.TryPeek(out var page));
            Assert.AreEqual(MetaPopupKind.StoryPage, page.Kind);
            Assert.AreEqual(1, page.Value);
            Assert.AreEqual("첫 밤", Story.Get(1).title);

            Popups.Remove(MetaPopupKind.StoryPage);
            Story.MarkSeen(1);
            Assert.IsTrue(Story.IsSeen(1));
            Clear(10);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.StoryPage), "펼쳐 본 장은 다시 띄우지 않는다");
        }

        [Test]
        public void FirstReadReward_Once_AndReReadGivesNothing()
        {
            AddStory();
            double gems = Gem;
            Story.MarkSeen(1);
            var granted = Story.TryClaimFirstRead(1);
            Assert.IsNotNull(granted);
            Assert.AreEqual(50, Gem - gems, "처음 읽은 선물 별조각 50");
            Assert.IsNull(Story.TryClaimFirstRead(1), "한 번만 (다시 읽기 = 선물 없음)");
            Assert.IsNull(Story.TryClaimFirstRead(2), "콘텐츠 없음");
            Assert.IsTrue(Data.meta.story.rewardedChapters.Contains(1));
        }

        [Test]
        public void Tutorial_DefersUntilFtueDone()
        {
            AddStory();
            Data.meta.ftue.done = false;
            Clear(10);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.StoryPage), "튜토리얼 중에는 미룸");

            Story.Tick(1.1f);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.StoryPage));
            Data.meta.ftue.done = true;
            Story.Tick(0.5f);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.StoryPage), "1초마다 본다");
            Story.Tick(0.6f);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.StoryPage), "졸업하면 넣는다");
        }

        [Test]
        public void ChaptersWithoutContent_Skip()
        {
            AddStory();
            Data.meta.ftue.done = true;
            Clear(60);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.StoryPage), "6장부터는 콘텐츠가 없어 건너뜀");
            Assert.IsNull(Story.Get(6));
            Story.MarkSeen(6);
            Assert.IsFalse(Story.IsSeen(6), "콘텐츠 없는 장은 표시하지 않는다");
        }

        [Test]
        public void SeenChapter_IsFoundInCodex()
        {
            AddStory();
            Codex.Connect();
            Assert.AreEqual(0, Codex.FoundCount(CodexCategory.Story));
            Story.MarkSeen(1);
            Codex.Tick(1.1f);
            Assert.AreEqual(1, Codex.FoundCount(CodexCategory.Story), "도감 story:1");
        }
    }
}
