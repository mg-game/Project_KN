using System.Linq;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 픽스처 레시피: sock_a = m_cloth x3 + 원정 2회 (단서 레벨 5), sock_b = m_thread x1 + 잔향이 출격 3회 (단서 레벨 5),
    /// sock_a2 = 부모 sock_a + 계보 레벨 10 (단서 레벨 10), candle_a = m_cloth x1 (조건 없음, 단서 레벨 5)
    /// </summary>
    public class EvolutionSystemTests : SystemsTestBase
    {
        [SetUp]
        public void SetUpTuning()
        {
            Config.substituteShardCostByGrade = new[] { 0, 5, 10, 20, 40 };
            Config.accelerateExpeditionCost = 4;
            Config.accelerateEchoDeployCost = 1;
            Config.accelerateGuardianKillCost = 0.2;
        }

        void Own(string lineageId, int level)
        {
            Characters.Grant(lineageId);
            Data.lineages[lineageId].level = level;
        }

        // ── 상태 ──

        [Test]
        public void State_FollowsOwnershipParentAndClueLevel()
        {
            Assert.AreEqual(EvolutionState.Undiscovered, Evolution.GetState("sock_base"), "미보유 계보");

            Characters.Grant("sock");
            Assert.AreEqual(EvolutionState.Unlocked, Evolution.GetState("sock_base"));
            Assert.AreEqual(EvolutionState.Undiscovered, Evolution.GetState("sock_a"), "레벨 1 < 단서 레벨 5");

            Data.lineages["sock"].level = 5;
            Assert.AreEqual(EvolutionState.ClueFound, Evolution.GetState("sock_a"));

            Data.lineages["sock"].level = 10;
            Assert.AreEqual(EvolutionState.Undiscovered, Evolution.GetState("sock_a2"), "부모(sock_a) 미해금");

            Characters.UnlockForm("sock_a");
            Assert.AreEqual(EvolutionState.Unlocked, Evolution.GetState("sock_a"));
            Assert.AreEqual(EvolutionState.ClueFound, Evolution.GetState("sock_a2"));
            Assert.AreEqual(EvolutionState.Undiscovered, Evolution.GetState("nope"));
            Assert.AreEqual(EvolutionState.Undiscovered, Evolution.GetState(null));
        }

        [Test]
        public void Check_ReportsMaterialsAndConditions()
        {
            Own("sock", 5);
            Inventory.Add("m_cloth", 2);
            Characters.AddProgress("sock", ProgressType.ExpeditionCount);

            var check = Evolution.Check("sock_a");

            Assert.AreEqual(EvolutionState.ClueFound, check.State);
            Assert.AreEqual("sock", check.Lineage.id);
            Assert.AreEqual("sock_a", check.Form.id);
            Assert.AreEqual(1, check.Materials.Count);
            Assert.AreEqual("m_cloth", check.Materials[0].ItemId);
            Assert.AreEqual(2, check.Materials[0].Have);
            Assert.AreEqual(3, check.Materials[0].Need);
            Assert.IsFalse(check.Materials[0].Enough);
            Assert.AreEqual(1, check.Conditions.Count);
            Assert.AreEqual("원정 2회", check.Conditions[0].Label);
            Assert.AreEqual(1, check.Conditions[0].Progress);
            Assert.IsFalse(check.Conditions[0].Done);
            Assert.IsFalse(check.CanCraft);
            Assert.IsNull(Evolution.Check("nope"));
        }

        [Test]
        public void Check_MergesDuplicateMaterialLines()
        {
            CharDb.GetForm("candle_a").materials.Add(TestContent.Cost("m_cloth", 2));
            Own("candle", 5);
            Inventory.Add("m_cloth", 2);

            var check = Evolution.Check("candle_a");

            Assert.AreEqual(1, check.Materials.Count);
            Assert.AreEqual(3, check.Materials[0].Need);
            Assert.IsFalse(Evolution.TryCraft("candle_a"), "합산 3개가 필요");
            Assert.AreEqual(2, Inventory.GetCount("m_cloth"));
        }

        [Test]
        public void ConditionProgress_IsClampedToTarget()
        {
            Own("sock", 5);
            Characters.AddProgress("sock", ProgressType.ExpeditionCount, 7);

            Assert.AreEqual(2, Evolution.GetConditionProgress("sock_a", 0));
            Assert.AreEqual(0, Evolution.GetConditionProgress("sock_a", 1), "없는 조건 인덱스");
            Assert.AreEqual(0, Evolution.GetConditionProgress("sock_a", -1));
        }

        [Test]
        public void ConditionLabels_AreKorean()
        {
            string Label(ProgressType type, int target) => EvolutionSystem.FormatCondition(TestContent.Cond(type, target));

            Assert.AreEqual("원정 10회", Label(ProgressType.ExpeditionCount, 10));
            Assert.AreEqual("잔향이 출격 30회", Label(ProgressType.EchoDeployCount, 30));
            Assert.AreEqual("캐릭터 레벨 20 달성", Label(ProgressType.LineageLevel, 20));
            Assert.AreEqual("스테이지 2-10 도달", Label(ProgressType.StageReached, 20), "챕터-번호 표기");
            Assert.AreEqual("스테이지 3-5 도달", Label(ProgressType.StageReached, 25));
            Assert.AreEqual("잔향이 던전 5층 돌파", Label(ProgressType.DungeonFloor, 5));
            Assert.AreEqual("지킴이 처치 100회", Label(ProgressType.GuardianKillCount, 100));
        }

        // ── 제작 ──

        [Test]
        public void TryCraft_ConsumesExactMaterials_AndUnlocksForm()
        {
            Own("sock", 5);
            Inventory.Add("m_cloth", 5);
            Characters.AddProgress("sock", ProgressType.ExpeditionCount, 2);
            CharacterFormDef unlocked = null;
            Evolution.FormUnlocked += form => unlocked = form;

            Assert.IsTrue(Evolution.Check("sock_a").CanCraft);
            Assert.IsTrue(Evolution.TryCraft("sock_a"));

            Assert.AreEqual(2, Inventory.GetCount("m_cloth"));
            Assert.AreEqual(EvolutionState.Unlocked, Evolution.GetState("sock_a"));
            Assert.AreEqual("sock_a", unlocked.id);
            Assert.IsTrue(Characters.GetUnlockedForms().Any(f => f.id == "sock_a"));
            Assert.AreEqual(5, Characters.GetLevel("sock"), "레벨은 계보 공유");

            Assert.IsFalse(Evolution.TryCraft("sock_a"), "이미 해금");
            Assert.AreEqual(2, Inventory.GetCount("m_cloth"));
        }

        [Test]
        public void TryCraft_MissingRequirement_ConsumesNothing()
        {
            Own("sock", 5);
            Inventory.Add("m_cloth", 5);

            Assert.IsFalse(Evolution.TryCraft("sock_a"), "원정 조건 미달");
            Assert.AreEqual(5, Inventory.GetCount("m_cloth"));

            Characters.AddProgress("sock", ProgressType.ExpeditionCount, 2);
            Data.lineages["sock"].level = 4;
            Assert.IsFalse(Evolution.TryCraft("sock_a"), "단서 미발견");
            Assert.AreEqual(5, Inventory.GetCount("m_cloth"));
            Assert.IsFalse(Evolution.TryCraft("nope"));
        }

        [Test]
        public void TryCraft_WithoutConditions_NeedsOnlyMaterials()
        {
            Own("candle", 5);
            Inventory.Add("m_cloth", 1);

            Assert.IsTrue(Evolution.TryCraft("candle_a"));
            Assert.AreEqual(0, Inventory.GetCount("m_cloth"));
            Assert.AreEqual(0, Inventory.GetOwnedItems().Count, "다 쓴 재료는 목록에서 빠진다");
        }

        // ── 기억편: 재료 대체 ──

        [Test]
        public void SubstituteCost_OnlyForRareOrHigherMaterials()
        {
            Assert.AreEqual(0, Evolution.GetSubstituteCost("m_cloth"), "일반 재료");
            Assert.AreEqual(5, Evolution.GetSubstituteCost("m_thread"), "희귀 재료");
            Assert.AreEqual(0, Evolution.GetSubstituteCost("r1"), "재료가 아닌 아이템");
            Assert.AreEqual(0, Evolution.GetSubstituteCost("nope"));
            Assert.AreEqual(0, Evolution.GetSubstituteCost(null));
        }

        [Test]
        public void TrySubstitute_SpendsLineageShardsForShortfall()
        {
            Own("sock", 5);
            Data.lineages["sock"].shards = 12;
            Assert.AreEqual(1, Evolution.GetShortfall("sock_b", "m_thread"));

            Assert.IsFalse(Evolution.TrySubstitute("sock_b", "m_thread", 2), "부족분(1개)보다 많이");
            Assert.IsTrue(Evolution.TrySubstitute("sock_b", "m_thread", 1));

            Assert.AreEqual(7, Characters.GetShards("sock"));
            Assert.AreEqual(0, Inventory.GetCount("m_thread"), "가방에는 들어가지 않는다");
            Assert.AreEqual(1, Evolution.GetSubstituted("sock_b", "m_thread"));
            Assert.AreEqual(0, Evolution.GetShortfall("sock_b", "m_thread"));
            var material = Evolution.Check("sock_b").Materials[0];
            Assert.AreEqual(1, material.Have, "레시피에는 대체분이 보유량으로 보인다");
            Assert.IsTrue(material.Enough);
            Assert.IsFalse(Evolution.TrySubstitute("sock_b", "m_thread", 1), "이미 충분");
            Assert.IsFalse(Evolution.TrySubstitute("sock_a", "m_cloth", 1), "일반 재료는 대체 불가");
            Assert.IsFalse(Evolution.TrySubstitute("sock_a", "m_thread", 1), "레시피에 없는 재료");
            Assert.AreEqual(7, Characters.GetShards("sock"));
        }

        [Test]
        public void TrySubstitute_RequiresClueAndShards()
        {
            Own("sock", 4);
            Data.lineages["sock"].shards = 100;
            Assert.IsFalse(Evolution.TrySubstitute("sock_b", "m_thread", 1), "단서 미발견");

            Data.lineages["sock"].level = 5;
            Data.lineages["sock"].shards = 4;
            Assert.IsFalse(Evolution.TrySubstitute("sock_b", "m_thread", 1), "기억편 부족");
            Assert.IsFalse(Evolution.TrySubstitute("sock_b", "m_thread", 0));

            Assert.AreEqual(0, Inventory.GetCount("m_thread"));
            Assert.AreEqual(4, Characters.GetShards("sock"));
        }

        [Test]
        public void SubstitutedMaterial_OnlyCountsForItsOwnRecipe()
        {
            // candle_a도 기억의 실을 쓰게 해서, sock 기억편으로 채운 실이 다른 계보 레시피로 새는지 본다
            CharDb.GetForm("candle_a").materials.Add(TestContent.Cost("m_thread", 1));
            Own("sock", 5);
            Own("candle", 5);
            Data.lineages["sock"].shards = 100;
            Inventory.Add("m_cloth", 1);

            Assert.IsTrue(Evolution.TrySubstitute("sock_b", "m_thread", 1));

            Assert.AreEqual(0, Evolution.GetSubstituted("candle_a", "m_thread"));
            Assert.AreEqual(1, Evolution.GetShortfall("candle_a", "m_thread"), "다른 레시피에는 보이지 않는다");
            Assert.IsFalse(Evolution.Check("candle_a").CanCraft);
            Assert.IsFalse(Evolution.TryCraft("candle_a"));

            // 실제로 모은 실을 다른 레시피에 써도 sock_b의 대체분은 그대로라 다시 대체할 수 없다
            Inventory.Add("m_thread", 1);
            Assert.IsTrue(Evolution.TryCraft("candle_a"));
            Assert.AreEqual(0, Inventory.GetCount("m_thread"));
            Assert.AreEqual(0, Evolution.GetShortfall("sock_b", "m_thread"));
            Assert.IsFalse(Evolution.TrySubstitute("sock_b", "m_thread", 1), "같은 부족분을 반복 대체할 수 없다");
            Assert.AreEqual(95, Characters.GetShards("sock"));
        }

        [Test]
        public void TryCraft_UsesSubstitutedMaterials_BeforeBag()
        {
            // sock_b: 기억의 실 3개 (1 + 2) + 잔향이 출격 3회
            CharDb.GetForm("sock_b").materials.Add(TestContent.Cost("m_thread", 2));
            Own("sock", 5);
            Data.lineages["sock"].shards = 100;
            Characters.AddProgress("sock", ProgressType.EchoDeployCount, 3);
            Inventory.Add("m_thread", 1);

            Assert.AreEqual(2, Evolution.GetShortfall("sock_b", "m_thread"));
            Assert.IsTrue(Evolution.TrySubstitute("sock_b", "m_thread", 2));
            Assert.AreEqual(90, Characters.GetShards("sock"));
            Assert.IsTrue(Evolution.Check("sock_b").CanCraft);

            // 대체 후 실을 더 모아도 대체분이 먼저 쓰이고 남는 실은 가방에 남는다
            Inventory.Add("m_thread", 2);
            Assert.IsTrue(Evolution.TryCraft("sock_b"));

            Assert.AreEqual(2, Inventory.GetCount("m_thread"), "필요 3 - 대체 2 = 가방에서 1개만 소모");
            Assert.AreEqual(0, Evolution.GetSubstituted("sock_b", "m_thread"));
            Assert.AreEqual(0, Data.lineages["sock"].conditionBonus.Count, "제작 후 대체 기록 정리");
        }

        [Test]
        public void Substitute_RaisesCharacterChanged_WithUpdatedRecipe()
        {
            Own("sock", 5);
            Data.lineages["sock"].shards = 10;
            bool enoughOnChanged = false;
            Characters.Changed += () => enoughOnChanged = Evolution.Check("sock_b").Materials[0].Enough;

            Assert.IsTrue(Evolution.TrySubstitute("sock_b", "m_thread", 1));

            Assert.IsTrue(enoughOnChanged, "기억편 소모 알림 시점에 대체분이 이미 반영돼 있다");
        }

        // ── 기억편: 진화 가속 ──

        [Test]
        public void Accelerate_FillsCountCondition_WithoutTouchingRecord()
        {
            Own("sock", 5);
            Data.lineages["sock"].shards = 10;
            Characters.AddProgress("sock", ProgressType.EchoDeployCount);

            Assert.AreEqual(2, Evolution.GetAccelerateCost("sock_b", 0), "남은 2회 × 1");
            Assert.IsTrue(Evolution.TryAccelerate("sock_b", 0));

            Assert.AreEqual(8, Characters.GetShards("sock"));
            Assert.AreEqual(3, Evolution.GetConditionProgress("sock_b", 0));
            Assert.AreEqual(1, Characters.GetProgress("sock", ProgressType.EchoDeployCount), "실제 기록은 그대로");
            Assert.AreEqual(0, Evolution.GetAccelerateCost("sock_b", 0));
            Assert.IsFalse(Evolution.TryAccelerate("sock_b", 0));

            Inventory.Add("m_thread");
            Assert.IsTrue(Evolution.TryCraft("sock_b"));
            Assert.AreEqual(0, Data.lineages["sock"].conditionBonus.Count, "제작 후 가속 기록 정리");
        }

        [Test]
        public void AccelerateCost_UsesPerUnitCostRoundedUp()
        {
            Own("sock", 5);
            Assert.AreEqual(8, Evolution.GetAccelerateCost("sock_a", 0), "원정 2회 × 4");

            Config.accelerateEchoDeployCost = 0.2;
            Assert.AreEqual(1, Evolution.GetAccelerateCost("sock_b", 0), "출격 3회 × 0.2 = 0.6 → 1");

            Config.accelerateEchoDeployCost = 0.4;
            Assert.AreEqual(2, Evolution.GetAccelerateCost("sock_b", 0), "1.2 → 2");

            CharDb.GetForm("sock_b").conditions[0] = TestContent.Cond(ProgressType.GuardianKillCount, 100);
            Assert.AreEqual(20, Evolution.GetAccelerateCost("sock_b", 0), "처치 100회 × 0.2 = 20 (오차로 21이 되지 않음)");
        }

        [Test]
        public void Accelerate_NotAllowedForCurrentValueConditions()
        {
            CharDb.GetForm("sock_a").conditions.Add(TestContent.Cond(ProgressType.StageReached, 10));
            Own("sock", 5);
            Data.lineages["sock"].shards = 100;

            Assert.IsFalse(Evolution.CanAccelerate(ProgressType.StageReached));
            Assert.IsFalse(Evolution.CanAccelerate(ProgressType.LineageLevel));
            Assert.IsFalse(Evolution.CanAccelerate(ProgressType.DungeonFloor));
            Assert.IsTrue(Evolution.CanAccelerate(ProgressType.GuardianKillCount));
            Assert.AreEqual(0, Evolution.GetAccelerateCost("sock_a", 1));
            Assert.IsFalse(Evolution.TryAccelerate("sock_a", 1));
            Assert.AreEqual(100, Characters.GetShards("sock"));
        }

        [Test]
        public void Accelerate_RequiresClueAndShards()
        {
            Own("sock", 4);
            Data.lineages["sock"].shards = 100;
            Assert.AreEqual(0, Evolution.GetAccelerateCost("sock_a", 0), "단서 미발견");
            Assert.IsFalse(Evolution.TryAccelerate("sock_a", 0));

            Data.lineages["sock"].level = 5;
            Data.lineages["sock"].shards = 7;
            Assert.IsFalse(Evolution.TryAccelerate("sock_a", 0), "8개 필요");
            Assert.IsFalse(Evolution.TryAccelerate("sock_a", 5), "없는 조건");
            Assert.AreEqual(0, Evolution.GetConditionProgress("sock_a", 0));
            Assert.AreEqual(7, Characters.GetShards("sock"));
        }

        // ── 빨간 점 ──

        [Test]
        public void CountCraftable_CountsReadyRecipes()
        {
            Own("sock", 5);
            Own("candle", 5);
            Assert.AreEqual(0, Evolution.CountCraftable());

            Inventory.Add("m_cloth", 3);
            Assert.AreEqual(1, Evolution.CountCraftable(), "candle_a (조건 없음)");

            Characters.AddProgress("sock", ProgressType.ExpeditionCount, 2);
            Assert.AreEqual(2, Evolution.CountCraftable(), "sock_a + candle_a");
            Assert.AreEqual(1, Evolution.CountCraftable("sock"));
            Assert.AreEqual(0, Evolution.CountCraftable("button"));
        }

        [Test]
        public void CraftGate_AsksOnlyForEvolution_AndIsReadLive()
        {
            Own("candle", 5);
            Inventory.Add("m_cloth", 1);
            var asked = new System.Collections.Generic.List<FeatureId>();
            bool open = false;
            Evolution.FeatureResolver = feature =>
            {
                asked.Add(feature);
                return open;
            };

            Assert.AreEqual(0, Evolution.CountCraftable(), "1-10 전 (F-03): 빨간 점 없음");
            Assert.IsFalse(Evolution.TryCraft("candle_a"));
            Assert.IsTrue(Evolution.Check("candle_a").CanCraft, "레시피 확인은 그대로");
            open = true; // 1-10 클리어 — 다시 잇지 않아도 바로 열린다
            Assert.AreEqual(1, Evolution.CountCraftable());
            Assert.IsTrue(Evolution.TryCraft("candle_a"));
            Assert.IsTrue(Characters.IsFormUnlocked("candle_a"));
            Assert.AreEqual(0, Inventory.GetCount("m_cloth"));
            CollectionAssert.IsNotEmpty(asked);
            Assert.IsTrue(asked.TrueForAll(f => f == FeatureId.Evolution), "진화 제작 기능만 묻는다");
        }
    }
}
