using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// PRG-04 소환 마일스톤 (SPEC4 §6.4 · 부록 B.1): 누적 소환(첫 만남 확정 소환은 세지 않음) 10/30/50/100/200, 그 뒤 100회마다 별조각 300.
    ///   · 100회 = 친구 선택권 1장 (전설 이하 — §2.11 #4, 대부분의 플레이어에게는 "누구의 기억편을 받을지 고르기").
    ///   · 처음 200회 별조각 = 100 (예산 ≤ 150, §11.2). 확률 · 천장은 그대로 (공정함).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>친구 선택권으로 고를 수 있는 가장 높은 등급 (MetaRewardType.FriendSelectTicket의 itemId)</summary>
        internal const ItemGrade FriendTicketMaxGrade = ItemGrade.Legendary;

        static partial void FillGachaMilestones(MetaDatabase db, GameContent content)
        {
            var steps = db.gachaMilestones;
            steps.Add(Milestone(10, Gem(100)));
            steps.Add(Milestone(30, MetaReward.SelectShards(20)));
            steps.Add(Milestone(50, Mat(MaterialIds.Moonlight, 2)));
            steps.Add(Milestone(100, FriendTicket(1)));
            steps.Add(Milestone(200, MetaReward.SelectShards(40)));

            db.gachaRepeat = new GachaRepeatMilestone
            {
                interval = 100,
                rewards = Rewards(Gem(300)),
            };
        }

        static GachaMilestoneDef Milestone(int pulls, params MetaReward[] rewards) => new() { pulls = pulls, rewards = Rewards(rewards) };

        /// <summary>친구 선택권 n장 (전설 이하 — meta.wallet.friendSelectTickets, MetaRewardService와 같은 모양)</summary>
        static MetaReward FriendTicket(int count) => MetaRewardService.FriendTicket(count);
    }
}
