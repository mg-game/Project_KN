using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 소환 마일스톤 (PRG-04, SPEC4 §6.4): 확정 소환 제외 · 10회 소환 = 10 · 단계 순서 · 200 뒤 100마다 되풀이 · 친구 선택권 등급 상한 ·
    /// 중복 → 기억편 · 고르지 않으면 선택권 그대로.
    /// </summary>
    public class GachaMilestoneTests : MetaSystemsTestBase
    {
        void AddLadder()
        {
            MetaDb.gachaMilestones.Add(new GachaMilestoneDef { pulls = 10, rewards = new List<MetaReward> { MetaReward.Gem(100) } });
            MetaDb.gachaMilestones.Add(new GachaMilestoneDef { pulls = 30, rewards = new List<MetaReward> { MetaReward.SelectShards(20) } });
            MetaDb.gachaMilestones.Add(new GachaMilestoneDef { pulls = 100, rewards = new List<MetaReward> { MetaRewardService.FriendTicket() } });
            MetaDb.gachaMilestones.Add(new GachaMilestoneDef { pulls = 200, rewards = new List<MetaReward> { MetaReward.SelectShards(40) } });
            MetaDb.gachaRepeat = new GachaRepeatMilestone { interval = 100, rewards = new List<MetaReward> { MetaReward.Gem(300) } };
            Rebuild();
            GachaMilestones.Connect();
        }

        [Test]
        public void GuaranteedPull_NotCounted_TenPullCountsTen()
        {
            AddLadder();
            Gacha.PullGuaranteed(Table, "sock");
            Assert.AreEqual(0, GachaMilestones.Pulls, "첫 만남 확정 소환은 세지 않는다");

            Currency.Add(CurrencyType.Gem, 100000);
            Assert.IsTrue(Gacha.TryPull(Table, 10, out _));
            Assert.AreEqual(10, GachaMilestones.Pulls);
            Assert.IsTrue(Gacha.TryPull(Table, 1, out _));
            Assert.AreEqual(11, GachaMilestones.Pulls);
            Assert.AreEqual(11, Data.meta.gachaMilestones.pulls, "세이브에 쌓인다");
        }

        [Test]
        public void Steps_ClaimInOrder_OneAtATime()
        {
            AddLadder();
            Data.meta.gachaMilestones.pulls = 35;
            Assert.IsTrue(GachaMilestones.HasClaimable);
            Assert.IsTrue(GachaMilestones.TryGetNext(out var next));
            Assert.AreEqual(10, next.Pulls);
            Assert.IsTrue(next.CanClaim);

            double gems = Gem;
            Assert.IsNotNull(GachaMilestones.TryClaimNext());
            Assert.AreEqual(100, Gem - gems, "10회 별조각 100");
            Assert.IsNotNull(GachaMilestones.TryClaimNext(), "30회");
            Assert.AreEqual(20, MetaRewards.SelectShards);
            Assert.IsNull(GachaMilestones.TryClaimNext(), "100회는 아직");
            Assert.IsFalse(GachaMilestones.HasClaimable);
            Assert.AreEqual(2, GachaMilestones.ClaimedSteps);
        }

        [Test]
        public void ClaimAll_AndRepeatEveryHundredAfterTwoHundred()
        {
            AddLadder();
            Data.meta.gachaMilestones.pulls = 420;
            double gems = Gem;

            var granted = GachaMilestones.ClaimAll();

            Assert.AreEqual(6, GachaMilestones.ClaimedSteps, "10 · 30 · 100 · 200 + 300 · 400");
            Assert.AreEqual(100 + 300 + 300, Gem - gems, "되풀이 별조각 300 × 2");
            Assert.AreEqual(1, GachaMilestones.FriendTickets, "100회 친구 선택권");
            Assert.AreEqual(60, MetaRewards.SelectShards);
            Assert.AreEqual(700, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem));
            Assert.IsTrue(GachaMilestones.TryGetNext(out var next));
            Assert.AreEqual(500, next.Pulls);
            Assert.IsTrue(next.IsRepeat);
            Assert.IsFalse(next.Reached);

            var steps = new List<GachaMilestoneStep>();
            GachaMilestones.GetSteps(steps, repeatsAhead: 2);
            Assert.AreEqual(8, steps.Count, "콘텐츠 4 + 받은 되풀이 2 + 앞으로 2");
            Assert.AreEqual(600, steps[7].Pulls);
            Assert.IsTrue(steps[5].Claimed);
            Assert.IsFalse(steps[6].Claimed);
        }

        [Test]
        public void EmptyContent_IsOff()
        {
            Rebuild();
            Data.meta.gachaMilestones.pulls = 999;
            Assert.IsFalse(GachaMilestones.HasClaimable);
            Assert.IsFalse(GachaMilestones.TryGetNext(out _));
            Assert.IsNull(GachaMilestones.TryClaimNext());
            Assert.IsEmpty(GachaMilestones.ClaimAll());
            var steps = new List<GachaMilestoneStep>();
            GachaMilestones.GetSteps(steps);
            Assert.IsEmpty(steps);
        }

        [Test]
        public void FriendTicket_GradeCap_DuplicateShards_KeptWithoutPick()
        {
            UnityEngine.Object.DestroyImmediate(CharDb);
            CharDb = TestContent.CreateCharacters();
            CharDb.lineages.Add(new LineageDef
            {
                id = "myth", name = "신화", grade = ItemGrade.Mythic, baseFormId = "myth_base",
                forms = new List<CharacterFormDef> { TestContent.Form("myth_base", null, 0, CombatStyle.Assault) },
            });
            CreateSystems(new System.Random(1234));
            Assert.IsFalse(GachaMilestones.CanPickWithTicket("myth"));
            Assert.IsTrue(GachaMilestones.CanPickWithTicket("sock"));
            MetaRewards.Grant(new List<MetaReward> { MetaRewardService.FriendTicket(2) });
            Assert.AreEqual(2, GachaMilestones.FriendTickets);

            Assert.IsNull(GachaMilestones.TryUseFriendTicket("myth"), "전설보다 높은 등급은 고를 수 없다");
            Assert.IsNull(GachaMilestones.TryUseFriendTicket("nope"));
            Assert.AreEqual(2, GachaMilestones.FriendTickets, "고르지 않으면 줄지 않는다 (창을 닫아도 그대로)");

            var first = GachaMilestones.TryUseFriendTicket("sock");
            Assert.IsNotNull(first);
            Assert.IsTrue(first.IsNew);
            Assert.IsTrue(Characters.IsOwned("sock"));
            Assert.AreEqual(1, GachaMilestones.FriendTickets);

            var duplicate = GachaMilestones.TryUseFriendTicket("sock");
            Assert.IsNotNull(duplicate);
            Assert.IsFalse(duplicate.IsNew);
            Assert.Greater(duplicate.ShardsGained, 0, "중복 → 기억편");
            Assert.AreEqual(0, GachaMilestones.FriendTickets);
            Assert.IsNull(GachaMilestones.TryUseFriendTicket("candle"), "선택권이 없으면 null");
        }

        [Test]
        public void RedDot_WhenClaimableOrTicketLeft()
        {
            AddLadder();
            GachaMilestones.RegisterRedDots(RedDots);
            RedDots.Refresh();
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.GachaMilestone));

            Data.meta.gachaMilestones.pulls = 10;
            RedDots.MarkDirty();
            RedDots.Refresh();
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.GachaMilestone));
            GachaMilestones.ClaimAll();
            RedDots.Refresh();
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.GachaMilestone));
        }
    }
}
