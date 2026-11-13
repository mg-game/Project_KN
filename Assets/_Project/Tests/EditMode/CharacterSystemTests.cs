using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    public class CharacterSystemTests : SystemsTestBase
    {
        [SetUp]
        public void SetUpTuning()
        {
            Config.characterMaxLevel = 100;
            Config.characterLevelUpBaseCost = 50;
            Config.characterLevelUpCostGrowth = 1.1;
            Config.attackPerLevelPercent = 10;
            Config.hpPerLevelPercent = 5;
            Config.duplicateShardsByGrade = new[] { 5, 10, 20, 40, 80 };
            Config.maxResonanceLevel = 10;
            Config.resonanceCostPerLevel = 20;
            Config.resonanceBonusPercent = 5;
        }

        // ── 보유 ──

        [Test]
        public void Grant_NewLineage_OwnsWithBaseFormUnlocked()
        {
            var result = Characters.Grant("sock");

            Assert.IsTrue(result.IsNew);
            Assert.AreEqual(0, result.ShardsGained);
            Assert.AreEqual("sock", result.Lineage.id);
            Assert.IsTrue(Characters.IsOwned("sock"));
            Assert.IsTrue(Characters.IsFormUnlocked("sock_base"));
            Assert.IsFalse(Characters.IsFormUnlocked("sock_a"));
            Assert.AreEqual(1, Characters.GetLevel("sock"));
            Assert.AreEqual(NowUnix, Characters.GetSave("sock").obtainedUnix);
        }

        [Test]
        public void Grant_Duplicate_ConvertsToShardsByGrade()
        {
            foreach (var id in new[] { "sock", "candle", "button" }) Characters.Grant(id);

            var sock = Characters.Grant("sock");

            Assert.IsFalse(sock.IsNew);
            Assert.AreEqual(20, sock.ShardsGained, "영웅(3성)");
            Assert.AreEqual(10, Characters.Grant("candle").ShardsGained, "희귀(2성)");
            Assert.AreEqual(5, Characters.Grant("button").ShardsGained, "일반(1성)");
            Assert.AreEqual(20, Characters.GetShards("sock"));
            Assert.AreEqual(1, Characters.GetLevel("sock"), "중복은 레벨에 영향 없음");
        }

        [Test]
        public void Grant_UnknownLineage_ReturnsNull()
        {
            Assert.IsNull(Characters.Grant("nope"));
            Assert.IsNull(Characters.Grant(null));
            Assert.AreEqual(0, Data.lineages.Count);
        }

        [Test]
        public void OwnedLineages_SortedByGradeThenId()
        {
            Characters.Grant("button");
            Characters.Grant("candle");
            Characters.Grant("sock");

            CollectionAssert.AreEqual(new[] { "sock", "candle", "button" }, Characters.GetOwnedLineages().Select(l => l.id).ToArray());
        }

        [Test]
        public void UnlockedForms_IncludeBaseAndCraftedForms()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Characters.UnlockForm("sock_a");
            Characters.UnlockForm("candle_a");

            CollectionAssert.AreEqual(new[] { "sock_base", "sock_a", "button_base" },
                Characters.GetUnlockedForms().Select(f => f.id).ToArray());
            Assert.IsFalse(Characters.IsFormUnlocked("candle_a"), "미보유 계보의 형태는 해금되지 않는다");
        }

        [Test]
        public void StateChanges_FireChanged()
        {
            int changed = 0;
            Characters.Changed += () => changed++;
            Currency.Add(CurrencyType.Gold, 1e6);

            Characters.Grant("sock");
            Characters.Grant("sock");
            Characters.TryLevelUp("sock");
            Characters.UnlockForm("sock_a");
            Characters.UnlockForm("sock_a");
            Characters.TryResonance("sock");
            Characters.AddAffinity("sock", 3);

            Assert.AreEqual(6, changed, "중복 해금은 알리지 않는다");
        }

        // ── 레벨 ──

        [Test]
        public void LevelUp_SpendsGoldOnCostCurve()
        {
            Characters.Grant("sock");
            Assert.AreEqual(50, Characters.GetLevelUpCost("sock"), 1e-9);
            Currency.Add(CurrencyType.Gold, 105);

            Assert.IsTrue(Characters.TryLevelUp("sock"));
            Assert.AreEqual(55, Characters.GetLevelUpCost("sock"), 1e-9, "50 × 1.1, 부동소수 오차 없이 정수");
            Assert.IsTrue(Characters.TryLevelUp("sock"));

            Assert.AreEqual(3, Characters.GetLevel("sock"));
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gold), 1e-9);
            Assert.IsFalse(Characters.CanLevelUp("sock"));
            Assert.IsFalse(Characters.TryLevelUp("sock"));
            Assert.AreEqual(3, Characters.GetLevel("sock"));
        }

        [Test]
        public void LevelUp_StopsAtMaxLevel()
        {
            Config.characterMaxLevel = 2;
            Characters.Grant("sock");
            Currency.Add(CurrencyType.Gold, 1e6);

            Assert.IsTrue(Characters.TryLevelUp("sock"));
            Assert.IsTrue(Characters.IsMaxLevel("sock"));
            Assert.AreEqual(0, Characters.GetLevelUpCost("sock"), 1e-9);
            double gold = Currency.Get(CurrencyType.Gold);

            Assert.IsFalse(Characters.TryLevelUp("sock"));
            Assert.AreEqual(2, Characters.GetLevel("sock"));
            Assert.AreEqual(gold, Currency.Get(CurrencyType.Gold), 1e-9);
        }

        [Test]
        public void LevelUpCost_ForSeveralLevels_IsWhatThatManyLevelUpsSpend()
        {
            Characters.Grant("sock");
            Assert.AreEqual(50 + 55 + 61, Characters.GetLevelUpCost("sock", 3), 1e-9, "50 + 55 + ⌈60.5⌉ (강해지기 줄의 비용, PRG-03)");
            Assert.AreEqual(Characters.GetLevelUpCost("sock"), Characters.GetLevelUpCost("sock", 1), 1e-9);

            Currency.Add(CurrencyType.Gold, 1e6);
            Assert.AreEqual(Currency.Get(CurrencyType.Gold), Characters.AvailableGold, 1e-9, "가진 단추를 그대로 읽는다");
            double before = Characters.AvailableGold;
            double cost = Characters.GetLevelUpCost("sock", 5);
            for (int i = 0; i < 5; i++) Assert.IsTrue(Characters.TryLevelUp("sock"));
            Assert.AreEqual(before - cost, Characters.AvailableGold, 1e-6, "합 = 다섯 번 올리며 쓴 단추");
            Assert.AreEqual(6, Characters.GetLevel("sock"));
        }

        [Test]
        public void LevelUpCost_ForSeveralLevels_StopsAtMaxLevel_ZeroWhenNothingToBuy()
        {
            Config.characterMaxLevel = 3;
            Characters.Grant("sock");
            Assert.AreEqual(50 + 55, Characters.GetLevelUpCost("sock", 10), 1e-9, "Lv.3에서 멈춘다");
            Assert.AreEqual(0, Characters.GetLevelUpCost("sock", 0), 1e-9);
            Assert.AreEqual(0, Characters.GetLevelUpCost("sock", -2), 1e-9);
            Assert.AreEqual(0, Characters.GetLevelUpCost("candle", 5), 1e-9, "미보유");
            Assert.AreEqual(0, Characters.GetLevelUpCost(null, 5), 1e-9);
            Data.lineages["sock"].level = 3;
            Assert.AreEqual(0, Characters.GetLevelUpCost("sock", 5), 1e-9, "최대 레벨");
        }

        [Test]
        public void Level_IsSharedByAllFormsOfLineage()
        {
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Data.lineages["sock"].level = 4;

            Assert.AreEqual(Characters.GetStats("sock_base").attack, Characters.GetStats("sock_a").attack, 1e-4);
            Assert.AreEqual(4, Characters.GetProgress("sock", ProgressType.LineageLevel));
        }

        // ── 기억편 / 공명 ──

        [Test]
        public void TrySpendShards_RejectsInvalidAmounts()
        {
            Characters.Grant("sock");
            Data.lineages["sock"].shards = 10;

            Assert.IsFalse(Characters.TrySpendShards("sock", 0));
            Assert.IsFalse(Characters.TrySpendShards("sock", -5));
            Assert.IsFalse(Characters.TrySpendShards("sock", 11));
            Assert.IsFalse(Characters.TrySpendShards("candle", 1), "미보유 계보");
            Assert.AreEqual(10, Characters.GetShards("sock"));

            Assert.IsTrue(Characters.TrySpendShards("sock", 10));
            Assert.AreEqual(0, Characters.GetShards("sock"));
        }

        [Test]
        public void Resonance_CostGrowsAndStopsAtMax()
        {
            Config.maxResonanceLevel = 2;
            Characters.Grant("sock");
            Data.lineages["sock"].shards = 70;

            Assert.AreEqual(20, Characters.GetResonanceCost("sock"));
            Assert.IsTrue(Characters.TryResonance("sock"));
            Assert.AreEqual(40, Characters.GetResonanceCost("sock"));
            Assert.IsTrue(Characters.TryResonance("sock"));

            Assert.AreEqual(2, Characters.GetResonanceLevel("sock"));
            Assert.AreEqual(10, Characters.GetShards("sock"));
            Assert.AreEqual(10f, Characters.GetResonanceBonusPercent("sock"), 1e-4);
            Assert.AreEqual(0, Characters.GetResonanceCost("sock"), "최대 공명");
            Assert.IsFalse(Characters.TryResonance("sock"));
            Assert.AreEqual(10, Characters.GetShards("sock"));
        }

        [Test]
        public void ResonanceBonusAt_PreviewsNextLevel_ClampedToRange()
        {
            Config.maxResonanceLevel = 2;
            Characters.Grant("sock");

            // 상세 창 "+0% → +5%" (M60)
            Assert.AreEqual(0f, Characters.GetResonanceBonusPercentAt(0), 1e-4);
            Assert.AreEqual(5f, Characters.GetResonanceBonusPercentAt(Characters.GetResonanceLevel("sock") + 1), 1e-4);
            Assert.AreEqual(10f, Characters.GetResonanceBonusPercentAt(2), 1e-4);
            Assert.AreEqual(10f, Characters.GetResonanceBonusPercentAt(3), 1e-4, "최대 공명 위로는 최대 값");
            Assert.AreEqual(0f, Characters.GetResonanceBonusPercentAt(-1), 1e-4);

            Data.lineages["sock"].resonance = 1;
            Assert.AreEqual(Characters.GetResonanceBonusPercent("sock"), Characters.GetResonanceBonusPercentAt(1), 1e-4, "지금 값과 같은 식");
        }

        [Test]
        public void Resonance_NotEnoughShards_ChangesNothing()
        {
            Characters.Grant("sock");
            Data.lineages["sock"].shards = 19;

            Assert.IsFalse(Characters.CanResonance("sock"));
            Assert.IsFalse(Characters.TryResonance("sock"));
            Assert.AreEqual(0, Characters.GetResonanceLevel("sock"));
            Assert.AreEqual(19, Characters.GetShards("sock"));
        }

        // ── 능력치 / 표시 ──

        [Test]
        public void Stats_ScaleWithLevelResonanceAndOwnedCostumes()
        {
            Characters.Grant("sock");
            Data.lineages["sock"].level = 3;

            var stats = Characters.GetStats("sock_base");
            Assert.AreEqual(12f, stats.attack, 1e-3, "10 × (1 + 10% × 2)");
            Assert.AreEqual(110f, stats.hp, 1e-3, "100 × (1 + 5% × 2)");

            Data.lineages["sock"].shards = 20;
            Assert.IsTrue(Characters.TryResonance("sock"));
            Costumes.Unlock("sock_night");

            stats = Characters.GetStats("sock_base");
            Assert.AreEqual(12f * 1.10f, stats.attack, 1e-3, "공명 5% + 보유 의상 5%");
            Assert.AreEqual(110f * 1.05f, stats.hp, 1e-3, "체력에는 공명만");
            Assert.AreEqual(5f, Characters.GetCostumeAttackBonusPercent("sock"), 1e-4);
        }

        [Test]
        public void Stats_CopyCombatValuesFromForm()
        {
            Characters.Grant("sock");
            var form = CharDb.GetForm("sock_a");
            form.attackInterval = 0.5f;
            form.range = 2.2f;
            form.arcDegrees = 90f;
            form.moveSpeed = 1.2f;
            form.summonCost = 7;
            form.lifetime = 9f;

            var stats = Characters.GetStats("sock_a");

            Assert.AreEqual(CombatStyle.Assault, stats.style);
            Assert.AreEqual(0.5f, stats.attackInterval);
            Assert.AreEqual(2.2f, stats.range);
            Assert.AreEqual(90f, stats.arcDegrees);
            Assert.AreEqual(1.2f, stats.moveSpeed);
            Assert.AreEqual(7, stats.summonCost);
            Assert.AreEqual(9f, stats.lifetime);
        }

        [Test]
        public void DisplayName_EquippedCostumeAppliesToBaseFormOnly()
        {
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Costumes.Unlock("sock_night");
            Assert.IsTrue(Costumes.TryEquip("sock", "sock_night"));

            Assert.AreEqual("밤지기 양말", Characters.GetDisplayName("sock_base"));
            Assert.AreEqual("sock_a", Characters.GetDisplayName("sock_a"));

            Assert.IsTrue(Costumes.TryEquip("sock", null));
            Assert.AreEqual("sock_base", Characters.GetDisplayName("sock_base"));
        }

        [Test]
        public void Portrait_UsesCostumePortraitOnBaseForm()
        {
            var formSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            var costumeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            try
            {
                CharDb.GetForm("sock_base").portrait = formSprite;
                Characters.Grant("sock");
                Costumes.Unlock("sock_night");
                Costumes.Unlock("sock_event");
                CharDb.GetCostume("sock_night").portrait = costumeSprite;

                Costumes.TryEquip("sock", "sock_night");
                Assert.AreSame(costumeSprite, Characters.GetPortrait("sock_base"));

                Costumes.TryEquip("sock", "sock_event");
                Assert.AreSame(formSprite, Characters.GetPortrait("sock_base"), "의상 초상화가 없으면 형태 초상화");
                Assert.IsNull(Characters.GetPortrait("sock_a"));
            }
            finally
            {
                Object.DestroyImmediate(formSprite);
                Object.DestroyImmediate(costumeSprite);
            }
        }

        // ── 누적 기록 / 호감도 ──

        [Test]
        public void Progress_DerivedTypesReadCurrentValues()
        {
            Characters.Grant("sock");
            Data.lineages["sock"].level = 7;
            Data.battle.highestStage = 12;
            Data.dungeon.highestFloor = 4;

            Assert.AreEqual(7, Characters.GetProgress("sock", ProgressType.LineageLevel));
            Assert.AreEqual(12, Characters.GetProgress("sock", ProgressType.StageReached));
            Assert.AreEqual(4, Characters.GetProgress("sock", ProgressType.DungeonFloor));
            Assert.AreEqual(12, Characters.GetProgress("button", ProgressType.StageReached), "스테이지/던전은 전역 기록");
        }

        [Test]
        public void Progress_CountersAccumulatePerLineage_WithoutChanged()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            int progressEvents = 0, changed = 0;
            Characters.ProgressChanged += (_, _) => progressEvents++;
            Characters.Changed += () => changed++;

            Characters.AddProgress("sock", ProgressType.ExpeditionCount);
            Characters.AddProgress("sock", ProgressType.ExpeditionCount, 2);
            Characters.AddProgress("candle", ProgressType.GuardianKillCount, 5);
            Characters.AddProgress("sock", ProgressType.EchoDeployCount, 0);
            Characters.AddProgress("button", ProgressType.ExpeditionCount);
            Characters.AddProgress("sock", ProgressType.LineageLevel, 3);

            Assert.AreEqual(3, Characters.GetProgress("sock", ProgressType.ExpeditionCount));
            Assert.AreEqual(0, Characters.GetProgress("candle", ProgressType.ExpeditionCount));
            Assert.AreEqual(5, Characters.GetProgress("candle", ProgressType.GuardianKillCount));
            Assert.AreEqual(0, Characters.GetProgress("sock", ProgressType.EchoDeployCount));
            Assert.AreEqual(1, Characters.GetLevel("sock"), "레벨은 누적 기록으로 올릴 수 없다");
            Assert.AreEqual(3, progressEvents);
            Assert.AreEqual(0, changed, "누적 기록은 능력치와 무관하므로 Changed를 쓰지 않는다");
        }

        [Test]
        public void Affinity_AddsOnlyPositiveAmounts()
        {
            Characters.Grant("sock");

            Characters.AddAffinity("sock", 3);
            Characters.AddAffinity("sock", -10);
            Characters.AddAffinity("candle", 5);

            Assert.AreEqual(3, Characters.GetAffinity("sock"));
            Assert.AreEqual(0, Characters.GetAffinity("candle"));
        }

        [Test]
        public void UnknownIds_ReturnSafeDefaults()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.IsFalse(Characters.IsOwned(null));
                Assert.IsNull(Characters.GetSave("nope"));
                Assert.IsFalse(Characters.IsFormUnlocked(null));
                Assert.AreEqual(0, Characters.GetLevel("nope"));
                Assert.AreEqual(0, Characters.GetLevelUpCost(null), 1e-9);
                Assert.IsFalse(Characters.TryLevelUp("nope"));
                Assert.IsFalse(Characters.TryResonance(null));
                Assert.AreEqual(0, Characters.GetProgress(null, ProgressType.ExpeditionCount));
                Assert.AreEqual(0f, Characters.GetStats("nope").attack);
                Assert.AreEqual("nope", Characters.GetDisplayName("nope"));
                Assert.AreEqual(string.Empty, Characters.GetDisplayName(null));
                Assert.IsNull(Characters.GetPortrait(null));
                Assert.IsNull(Characters.GetEquippedCostume("nope"));
                Characters.UnlockForm("nope");
                Characters.AddAffinity(null, 1);
                Characters.AddProgress(null, ProgressType.ExpeditionCount);
                Characters.SetEquippedCostume(null, "sock_night");
            });
        }
    }
}
