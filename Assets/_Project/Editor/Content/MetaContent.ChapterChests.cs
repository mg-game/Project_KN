using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// PRG-06 챕터 상자 · 잃어버린 상자 (SPEC4 §8.5 · 부록 B.2 · B.3, RD-13 / 델타 29).
    ///   · 챕터 상자 (핵심 P1, 챕터 보스 x-10 첫 클리어 — ChapterChestSystem이 챕터 상자 팝업을 띄운다):
    ///     1장 별조각 100 · 수선 천 10 · 리본 조각 10 · 기억의 실 2 / 2장 100 · 우산살 6 · 레이스 조각 6 · 기억의 실 3 /
    ///     3장 120 · 오르골 태엽 2 · 기억의 실 4 · 달빛 조각 1 / 4장 120 · 기억의 실 5 · 레이스 조각 10 · 달빛 조각 1 /
    ///     5장 150 · 오르골 태엽 3 · 기억의 실 6 · 달빛 조각 1 (1~5장 별조각 590).
    ///   · 6장부터 (CR4-01): 재료(기억의 실 6 · 오르골 태엽 2 · 달빛 조각 1)는 장마다, 별조각 100은 5로 나누어떨어지는 장(10, 15, 20 …)에만 —
    ///     열 장마다 별조각 200 이하 (EconomyBudgetTests "chapter chests ch6+"). 장이 끝없이 이어지므로 별조각 줄은
    ///     <see cref="ChapterChestGemRowsUntil"/>장까지 줄로 두고, 나머지 장은 chapter 0 기본 줄(재료만)이 받는다 (MetaDatabase.GetChapterChest).
    ///   · 잃어버린 상자 (확장 P2): 튜토리얼 뒤 일반 클리어마다 0.6 확률로 그 스테이지 구간 표에서 한 칸 (지금 클리어 재료 굴림을 대신한다).
    ///     표 1~10 기대 재료 ≈0.83/클리어 (지금 0.78). 끈 동안(<see cref="Stretch.LostChest"/>) dropChance = 0 → 지금 재료 굴림 그대로.
    ///     표는 반딧불(NEW-03) "최고 스테이지 표에서 재료 하나"도 쓰므로 늘 채워 둔다.
    /// 1장 상자 내용은 챕터 상자 팝업(ChapterChestSystem)만 준다 — S-3 (2026-11-05) 뒤로 가이드 퀘스트 22단계는 별조각 100만
    /// (MetaContent.Quests.ChapterChestMovedToSystem = true; 그 전 22단계는 1장 상자 내용을 함께 줬다).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>6장 뒤 별조각이 붙는 장 간격 (5로 나누어떨어지는 장)</summary>
        internal const int ChapterChestGemEvery = 5;

        /// <summary>6장 뒤 챕터 상자 별조각</summary>
        internal const int LateChapterChestGem = 100;

        /// <summary>별조각 줄을 두는 마지막 장 (스테이지 2,000 — 성장 천장 약 55장보다 훨씬 뒤)</summary>
        internal const int ChapterChestGemRowsUntil = 200;

        static partial void FillChapterChests(MetaDatabase db, GameContent content)
        {
            var chest = db.lostChest = new LostChestConfig
            {
                dropChance = On(Stretch.LostChest) ? 0.6f : 0f,
                tables = new List<LostChestTable>
                {
                    LostTable(1, 10,
                        LostEntry(MaterialIds.Cloth, 1, 2, 40), LostEntry(MaterialIds.Ribbon, 1, 2, 35),
                        LostEntry(MaterialIds.Wax, 1, 1, 15), LostEntry(MaterialIds.Lace, 1, 1, 10)),
                    LostTable(11, 20,
                        LostEntry(MaterialIds.Cloth, 1, 2, 30), LostEntry(MaterialIds.Ribbon, 1, 2, 20),
                        LostEntry(MaterialIds.Wax, 1, 2, 15), LostEntry(MaterialIds.Lace, 1, 1, 15),
                        LostEntry(MaterialIds.Rib, 1, 1, 15), LostEntry(MaterialIds.Thread, 1, 1, 5)),
                    LostTable(21, int.MaxValue,
                        LostEntry(MaterialIds.Cloth, 2, 3, 25), LostEntry(MaterialIds.Lace, 1, 2, 20),
                        LostEntry(MaterialIds.Rib, 1, 2, 20), LostEntry(MaterialIds.Wax, 1, 2, 15),
                        LostEntry(MaterialIds.Thread, 1, 1, 15), LostEntry(MaterialIds.Spring, 1, 1, 5)),
                },
                chapterChests = new List<ChapterChestDef>(),
            };

            var rows = chest.chapterChests;
            for (int chapter = 1; chapter <= 5; chapter++) rows.Add(ChapterChestRow(chapter, ChapterChestRewards(chapter)));
            for (int chapter = 2 * ChapterChestGemEvery; chapter <= ChapterChestGemRowsUntil; chapter += ChapterChestGemEvery)
            {
                rows.Add(ChapterChestRow(chapter, ChapterChestRewards(chapter)));
            }
            rows.Add(ChapterChestRow(0, LateChapterMaterials())); // 줄이 없는 장의 기본 (재료만)
        }

        /// <summary>그 장 챕터 상자 내용 (부록 B.2 + RD-13). 1~5장은 표, 6장부터는 재료 + (5로 나누어떨어지면) 별조각 100</summary>
        internal static MetaReward[] ChapterChestRewards(int chapter)
        {
            switch (chapter)
            {
                case 1: return new[] { Gem(100), Mat(MaterialIds.Cloth, 10), Mat(MaterialIds.Ribbon, 10), Mat(MaterialIds.Thread, 2) };
                case 2: return new[] { Gem(100), Mat(MaterialIds.Rib, 6), Mat(MaterialIds.Lace, 6), Mat(MaterialIds.Thread, 3) };
                case 3: return new[] { Gem(120), Mat(MaterialIds.Spring, 2), Mat(MaterialIds.Thread, 4), Mat(MaterialIds.Moonlight, 1) };
                case 4: return new[] { Gem(120), Mat(MaterialIds.Thread, 5), Mat(MaterialIds.Lace, 10), Mat(MaterialIds.Moonlight, 1) };
                case 5: return new[] { Gem(150), Mat(MaterialIds.Spring, 3), Mat(MaterialIds.Thread, 6), Mat(MaterialIds.Moonlight, 1) };
            }
            var materials = LateChapterMaterials();
            if (chapter <= 5 || chapter % ChapterChestGemEvery != 0) return materials;
            var withGem = new MetaReward[materials.Length + 1];
            withGem[0] = Gem(LateChapterChestGem);
            for (int i = 0; i < materials.Length; i++) withGem[i + 1] = materials[i];
            return withGem;
        }

        /// <summary>6장부터 장마다 받는 재료</summary>
        static MetaReward[] LateChapterMaterials() =>
            new[] { Mat(MaterialIds.Thread, 6), Mat(MaterialIds.Spring, 2), Mat(MaterialIds.Moonlight, 1) };

        static ChapterChestDef ChapterChestRow(int chapter, MetaReward[] rewards) => new() { chapter = chapter, rewards = Rewards(rewards) };

        static LostChestTable LostTable(int fromStage, int toStage, params LostChestEntry[] entries) => new()
        {
            fromStage = fromStage,
            toStage = toStage,
            entries = new List<LostChestEntry>(entries),
        };

        static LostChestEntry LostEntry(string itemId, int min, int max, int weight) => new() { itemId = itemId, min = min, max = max, weight = weight };
    }
}
