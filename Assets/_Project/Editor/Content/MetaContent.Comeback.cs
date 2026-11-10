using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// CR-06 다시 만난 날 (SPEC4 §8.2 · 부록 B.5): 튜토리얼 뒤 72시간 이상 비웠다 돌아오면 (14일 쉬고, 평생 6번까지)
    /// 한 번짜리 24시간 바구니 + 사흘 도장. 1일 별조각 300 + 빠른 수선권 2 · 2일 두 배 수선권 2 + 수선 천 10 · 3일 선택 기억편 20.
    /// 한 번에 별조각 300 ≤ 300 (§11.2).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillComeback(MetaDatabase db, GameContent content)
        {
            db.comeback = new ComebackConfig
            {
                minAwaySeconds = 72L * 3600,
                cooldownDays = 14,
                lifetimeMax = 6,
                bonusCapHours = 24f,
                days = new List<ComebackDayDef>
                {
                    ComebackDay(1, Gem(300), MetaReward.QuickIdleTicket(2)),
                    ComebackDay(2, MetaReward.DoubleTicket(2), Mat(MaterialIds.Cloth, 10)),
                    ComebackDay(3, MetaReward.SelectShards(20)),
                },
            };
        }

        static ComebackDayDef ComebackDay(int day, params MetaReward[] rewards) => new() { day = day, rewards = Rewards(rewards) };
    }
}
