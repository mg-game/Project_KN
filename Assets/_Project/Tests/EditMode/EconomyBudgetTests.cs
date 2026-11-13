using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    /// <summary>
    /// 별조각 예산 (SPEC4 §11.2, 로드맵 delta 29 · RD-13): 코드 원본 콘텐츠(IdleGame.EditorTools.MetaContent.Apply + CharacterContent · ItemContent ·
    /// SynergyContent)를 실제 시스템(도감 · 기록 · 호감도 · 동화책 · 챕터 상자 · 소환 마일스톤 · 바구니)에 넣고, 받는 길로 다 받아 본 합계가 예산 안인지 본다.
    /// 확장 항목은 끈 채로 (G3 전 실제 상태). 수첩 · 별빛 산책 · 다시 만난 날 · 첫 주 · 작은 선물 · 반딧불 · 밤 상점 줄의 내용 합계는 ContentMetaTests(P4-8)가 본다.
    /// 에디터 어셈블리(IdleGame.Editor)는 시험 어셈블리가 참조하지 않아 불러온 어셈블리에서 찾는다 (ContentSourceTests와 같은 방식).
    /// </summary>
    public class EconomyBudgetTests : MetaSystemsTestBase
    {
        [SetUp]
        public void UseRealContent()
        {
            Object.DestroyImmediate(CharDb);
            Object.DestroyImmediate(Items);
            Object.DestroyImmediate(SynergyDb);
            Object.DestroyImmediate(MetaDb);
            CharDb = ScriptableObject.CreateInstance<CharacterDatabase>();
            CharDb.lineages = (List<LineageDef>)Invoke("IdleGame.EditorTools.CharacterContent", "Build", Type.EmptyTypes);
            Items = ScriptableObject.CreateInstance<ItemDatabase>();
            Items.items = (List<ItemDef>)Invoke("IdleGame.EditorTools.ItemContent", "Build", Type.EmptyTypes);
            SynergyDb = ScriptableObject.CreateInstance<SynergyDatabase>();
            SynergyDb.synergies = (List<SynergyDef>)Invoke("IdleGame.EditorTools.SynergyContent", "Build", new[] { typeof(IEnumerable<LineageDef>) }, CharDb.lineages);
            MetaDb = ScriptableObject.CreateInstance<MetaDatabase>();
            var content = new GameContent { Config = Config, Items = Items, Characters = CharDb, Battle = BattleCfg, Synergies = SynergyDb };
            var apply = EditorType("IdleGame.EditorTools.MetaContent").GetMethod("Apply", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(MetaDatabase), typeof(GameContent), typeof(bool) }, null);
            Assert.IsNotNull(apply, "MetaContent.Apply(MetaDatabase, GameContent, bool)");
            apply.Invoke(null, new object[] { MetaDb, content, false });
            CreateSystems(new System.Random(1234));
        }

        void OwnEverything()
        {
            foreach (var lineage in CharDb.lineages)
            {
                Characters.Grant(lineage.id);
                foreach (var form in lineage.forms) Characters.UnlockForm(form.id);
                foreach (var costume in lineage.costumes) Costumes.Unlock(costume.id);
            }
        }

        void OpenAllFeatures()
        {
            SetCleared(999);
            Data.meta.ftue.done = true;
            Features.Evaluate();
        }

        static double GemsOf(IEnumerable<MetaReward> rewards)
        {
            double sum = 0;
            if (rewards == null) return 0;
            foreach (var reward in rewards)
                if (reward != null && reward.type == MetaRewardType.Currency && reward.currency == CurrencyType.Gem) sum += reward.amount;
            return sum;
        }

        // ───────────────────────── 평생 ─────────────────────────

        [Test]
        public void Achievements_Lifetime_AtMost3500()
        {
            double total = 0;
            foreach (var def in MetaDb.achievements) total += GemsOf(def.rewards);
            Assert.AreEqual(3400, total, 1e-9, "SPEC4 A.1 = 3,400");
            Assert.LessOrEqual(total, 3500, "예산 ≤ 3,500");
        }

        [Test]
        public void Codex_Lifetime_AtMost2700_ThroughTheSystem()
        {
            Rebuild();
            OpenAllFeatures();
            Codex.Connect();
            OwnEverything();
            foreach (var item in Items.items) if (item.category == ItemCategory.Material) Inventory.Add(item.id);
            foreach (var enemy in BattleCfg.enemies) Codex.NoteEnemy(enemy.id);
            foreach (var synergy in SynergyDb.synergies) Codex.NoteSynergy(synergy.id, Math.Max(0, synergy.tiers.Count - 1));
            foreach (var chapter in MetaDb.storyChapters) Data.meta.story.seenChapters.Add(chapter.chapter);
            Codex.Tick(1.1f);
            Assert.IsTrue(Codex.IsUnlocked);
            Assert.AreEqual(100f, Codex.CompletionPercent, 1e-3f, "모두 찾음");

            double before = Gem;
            Codex.ClaimAll();
            double total = Gem - before;

            Assert.LessOrEqual(total, 2700, "도감 평생 ≤ 2,700 (SPEC4 2,615 — 시즌 의상 포함)");
            // SPEC4 §6.2 = 형태 900 + 의상 30 × 벌 수 + 적 90 + 시너지 1,250 + 재료 45. 시즌 의상(jjak_starwalk)은 수첩 확장 항목이 켜지고 그림이 있을 때만
            // 콘텐츠에 있다 (CharacterContent.HasSeasonCostume — 확장 항목은 G3까지 꺼짐) → 의상 11벌 2,615 / 10벌 2,585. 그 밖의 차이는 모두 실패
            bool season = CharDb.lineages.Exists(l => l.costumes != null && l.costumes.Exists(c => c != null && c.id == "jjak_starwalk"));
            Assert.AreEqual(season ? 2615 : 2585, total, 1e-9,
                season ? "SPEC4 §6.2 = 2,615 (시즌 의상 포함)" : "SPEC4 §6.2 = 2,615 − 시즌 의상 30 = 2,585 (수첩 확장 항목 꺼짐 — 시즌 의상은 선택)");
            Assert.AreEqual(0, Codex.ClaimableCount());
        }

        [Test]
        public void Affinity_Lifetime_AtMost4000_SevenLineages()
        {
            Rebuild();
            foreach (var lineage in CharDb.lineages) Characters.Grant(lineage.id);
            Assert.AreEqual(7, CharDb.lineages.Count, "7계보");
            double before = Gem;
            foreach (var lineage in CharDb.lineages)
            {
                Data.lineages[lineage.id].affinity = 1_000_000;
                Assert.IsNotNull(Affinity.TryClaim(lineage.id), lineage.id);
            }
            double total = Gem - before;
            Assert.AreEqual(3780, total, 1e-9, "7 × (20 + … + 100) = 3,780");
            Assert.LessOrEqual(total, 4000, "예산 ≤ 4,000");
        }

        [Test]
        public void StoryAndChapterChests_ChapterOneToFive_AtMost900()
        {
            Rebuild();
            double before = Gem;
            for (int chapter = 1; chapter <= 5; chapter++)
            {
                Story.MarkSeen(chapter);
                Story.TryClaimFirstRead(chapter);
                ChapterChests.OpenChapterChest(chapter);
            }
            double total = Gem - before;
            Assert.LessOrEqual(total, 900, "동화책 1~5장 + 챕터 상자 1~5장 ≤ 900 (SPEC4 250 + 590)");
            Assert.Greater(total, 0, "1~5장 내용이 있다");
        }

        [Test]
        public void ChapterChests_SixthChapterOn_GemsPerTenChapters_AtMost200()
        {
            Rebuild();
            const int Last = 300;
            var gems = new double[Last + 1];
            for (int chapter = 6; chapter <= Last; chapter++) gems[chapter] = GemsOf(ChapterChests.PeekChapterChest(chapter));
            for (int start = 6; start + 9 <= Last; start++)
            {
                double window = 0;
                for (int chapter = start; chapter < start + 10; chapter++) window += gems[chapter];
                Assert.LessOrEqual(window, 200, $"{start}~{start + 9}장 별조각 ≤ 200 (CR4-01)");
            }
            Assert.IsNotEmpty(ChapterChests.PeekChapterChest(Last), "6장부터도 상자는 있다 (재료)");
        }

        [Test]
        public void GachaMilestones_First200Pulls_AtMost150()
        {
            Rebuild();
            Data.meta.gachaMilestones.pulls = 200;
            double before = Gem;
            GachaMilestones.ClaimAll();
            double total = Gem - before;
            Assert.LessOrEqual(total, 150, "첫 200회 별조각 ≤ 150 (SPEC4 100 + 아이템)");
            Assert.Greater(GachaMilestones.ClaimedSteps, 0);
        }

        // ───────────────────────── 비평 줄 (C-01) ─────────────────────────

        [Test]
        public void BasketGemLine_IsBoundedAtStage1_20_50_100_200()
        {
            Rebuild();
            var config = MetaDb.idleChest;
            int quickIdles = config.quickIdleGemCosts != null ? config.quickIdleGemCosts.Length : 0;
            var rates = new List<IdleChestRate>();
            foreach (int stage in new[] { 1, 20, 50, 100, 200 })
            {
                Data.battle.highestStage = stage;
                IdleChest.GetRates(rates);
                double gemPerHour = 0;
                foreach (var rate in rates)
                    if (rate.Type == MetaRewardType.Currency && rate.Currency == CurrencyType.Gem) gemPerHour += rate.PerHour;
                double perDay = gemPerHour * 24 + gemPerHour * config.quickIdleHours * quickIdles;
                Assert.LessOrEqual(perDay, 140, $"스테이지 {stage}: 바구니 별조각 줄 하루 ≤ 140 (빠른 수선 포함, 지금 {perDay:0.#})");
            }
        }

        // ───────────────────────── 도우미 ─────────────────────────

        static object Invoke(string typeName, string method, Type[] types, params object[] args)
        {
            var info = EditorType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null);
            Assert.IsNotNull(info, $"{typeName}.{method}");
            return info.Invoke(null, args);
        }

        static Type EditorType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IdleGame.Editor") continue;
                var type = assembly.GetType(name, false);
                Assert.IsNotNull(type, $"IdleGame.Editor 어셈블리의 {name}");
                return type;
            }
            Assert.Fail("IdleGame.Editor 어셈블리를 찾을 수 없다");
            return null;
        }
    }
}
