using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// Q-06 별빛 산책 수첩 (SPEC4 §8.4 · 부록 A.8, 확장 P2): 28일 · 30칸 · 칸마다 90 경험치 (일일 미션 일지 점수 1점 = 1).
    ///   · ★ 5/10/15/20/25/30 강조. 30칸 = 시즌 한정 의상 '별빛 산책 짝짝이' (jjak_starwalk, 그림 A14).
    ///     그 의상 줄이 없으면(수첩이 꺼졌거나 그림이 아직 없음 — CharacterContent.HasSeasonCostume) 30칸은 처음부터 대신 보상을 준다.
    ///     가진 뒤(다음 시즌부터)도 대신 보상 = 별조각 50 + 선택 기억편 30 (C-06).
    ///   · 시즌마다 별조각 390 (대신 보상이면 440) ≤ 450 (§11.2).
    ///   · 끈 동안(<see cref="Stretch.Season"/>) 칸이 비어 있다 — SeasonPassSystem은 빈 칸을 "꺼짐"으로 본다.
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>수첩 30칸 시즌 한정 의상 (CostumeUnlockType.Season)</summary>
        internal const string StarwalkCostumeId = "jjak_starwalk";

        /// <summary>수첩 칸 수 · 칸마다 경험치 · 시즌 날 수</summary>
        internal const int SeasonTierCount = 30, SeasonXpPerTier = 90, SeasonDays = 28;

        static partial void FillSeason(MetaDatabase db, GameContent content)
        {
            var season = db.season = new SeasonConfig
            {
                name = "별빛 산책 수첩",
                days = SeasonDays,
                xpPerTier = SeasonXpPerTier,
                banner = UiArt.SeasonBanner,
                tiers = new List<SeasonTierDef>(),
            };
            if (!On(Stretch.Season)) return;

            var t = season.tiers;
            t.Add(SeasonTier(1, false, Gem(30)));
            t.Add(SeasonTier(2, false, Mat(MaterialIds.Cloth, 10)));
            t.Add(SeasonTier(3, false, MetaReward.QuickIdleTicket(1)));
            t.Add(SeasonTier(4, false, Gem(30)));
            t.Add(SeasonTier(5, true, MetaReward.SelectShards(10)));
            t.Add(SeasonTier(6, false, Mat(MaterialIds.Ribbon, 10)));
            t.Add(SeasonTier(7, false, Gem(30)));
            t.Add(SeasonTier(8, false, Mat(MaterialIds.Wax, 10)));
            t.Add(SeasonTier(9, false, MetaReward.DoubleTicket(1)));
            t.Add(SeasonTier(10, true, Gem(80)));
            t.Add(SeasonTier(11, false, Mat(MaterialIds.Lace, 5)));
            t.Add(SeasonTier(12, false, Gem(30)));
            t.Add(SeasonTier(13, false, MetaReward.QuickIdleTicket(1)));
            t.Add(SeasonTier(14, false, Mat(MaterialIds.Thread, 3)));
            t.Add(SeasonTier(15, true, MetaReward.SelectShards(15)));
            t.Add(SeasonTier(16, false, Gem(30)));
            t.Add(SeasonTier(17, false, Mat(MaterialIds.Rib, 5)));
            t.Add(SeasonTier(18, false, MetaReward.DoubleTicket(1)));
            t.Add(SeasonTier(19, false, Gem(30)));
            t.Add(SeasonTier(20, true, Mat(MaterialIds.Moonlight, 1)));
            t.Add(SeasonTier(21, false, Mat(MaterialIds.Cloth, 20)));
            t.Add(SeasonTier(22, false, Gem(40)));
            t.Add(SeasonTier(23, false, MetaReward.QuickIdleTicket(2)));
            t.Add(SeasonTier(24, false, Mat(MaterialIds.Thread, 5)));
            t.Add(SeasonTier(25, true, MetaReward.SelectShards(20)));
            t.Add(SeasonTier(26, false, Gem(40)));
            t.Add(SeasonTier(27, false, Mat(MaterialIds.Spring, 2)));
            t.Add(SeasonTier(28, false, Gem(50)));
            t.Add(SeasonTier(29, false, MetaReward.Currency(CurrencyType.EventToken, 100)));

            // 30칸: 의상 줄이 있으면 의상(가졌으면 대신 보상), 없으면 처음부터 대신 보상
            var finalTier = new SeasonTierDef
            {
                tier = SeasonTierCount,
                highlight = true,
                fallback = Rewards(SeasonCostumeFallback()),
            };
            finalTier.rewards = CharacterContent.HasSeasonCostume
                ? Rewards(MetaReward.Costume(StarwalkCostumeId))
                : Rewards(SeasonCostumeFallback());
            t.Add(finalTier);
        }

        /// <summary>30칸 대신 보상 (의상을 가졌거나 아직 없을 때): 별조각 50 + 선택 기억편 30</summary>
        static MetaReward[] SeasonCostumeFallback() => new[] { Gem(50), MetaReward.SelectShards(30) };

        static SeasonTierDef SeasonTier(int tier, bool highlight, params MetaReward[] rewards) => new()
        {
            tier = tier,
            highlight = highlight,
            rewards = Rewards(rewards),
        };
    }
}
