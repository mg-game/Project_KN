namespace IdleGame.EditorTools
{
    /// <summary>
    /// NEW-02 오늘의 작은 선물 (SPEC4 §13.7 · 부록 C.3): 상점(1-8)이 열리면 날 키마다 한 번, 요일(월요일 = 0)마다 다른 선물.
    /// 월 수선 천 5 · 화 리본 조각 5 · 수 빠른 수선권 1 · 목 촛농 방울 3 · 금 별조각 20 · 토 레이스 조각 3 · 일 두 배 수선권 1.
    /// 주당 별조각 20 ≤ 30 (§11.2 — 금요일 줄이 조절 손잡이). 화면 문구 '오늘의 작은 선물' / '내일 새벽 5시에 새 선물이 와요'는 상점 카드(P4-4).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillDailyGifts(MetaDatabase db, GameContent content)
        {
            var list = db.dailyGifts;
            list.Add(DailyGift(0, Mat(MaterialIds.Cloth, 5)));
            list.Add(DailyGift(1, Mat(MaterialIds.Ribbon, 5)));
            list.Add(DailyGift(2, MetaReward.QuickIdleTicket(1)));
            list.Add(DailyGift(3, Mat(MaterialIds.Wax, 3)));
            list.Add(DailyGift(4, Gem(20)));
            list.Add(DailyGift(5, Mat(MaterialIds.Lace, 3)));
            list.Add(DailyGift(6, MetaReward.DoubleTicket(1)));
        }

        static DailyGiftDef DailyGift(int weekday, params MetaReward[] rewards) => new() { weekday = weekday, rewards = Rewards(rewards) };
    }
}
