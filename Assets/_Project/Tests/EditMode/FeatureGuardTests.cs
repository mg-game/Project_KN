using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// F-03 시스템 거절 (SPEC4 §5.1): 잠긴 기능은 UI가 새어도 시스템이 거절한다 — 친구 레벨업(1-1) · 진화 제작(1-10) · 의상(2-5).
    /// FeatureResolver가 null이면 열림이라 기존 테스트는 그대로다. MetaSession.Start가 FeatureGateSystem.IsUnlocked를 잇는다.
    /// </summary>
    public class FeatureGuardTests : MetaSystemsTestBase
    {
        static Func<FeatureId, bool> LockOnly(FeatureId locked) => feature => feature != locked;

        static DateTime LocalHourAsUtc(int hour) => DateTime.Today.AddHours(hour).ToUniversalTime();

        [Test]
        public void CharacterLevelUp_RefusedWhileLocked_WorksWhenOpen()
        {
            Characters.Grant("sock");
            Currency.Add(Characters.LevelUpCurrency, 1e9);
            double gold = Currency.Get(Characters.LevelUpCurrency);
            Characters.FeatureResolver = LockOnly(FeatureId.CharacterLevelUp);

            Assert.IsFalse(Characters.IsLevelUpUnlocked);
            Assert.IsFalse(Characters.CanLevelUp("sock"), "빨간 점 없음");
            Assert.IsFalse(Characters.TryLevelUp("sock"));
            Assert.AreEqual(1, Characters.GetLevel("sock"));
            Assert.AreEqual(gold, Currency.Get(Characters.LevelUpCurrency), "레벨업 재화(실타래)를 쓰지 않는다");

            Characters.FeatureResolver = LockOnly(FeatureId.Evolution);
            Assert.IsTrue(Characters.TryLevelUp("sock"), "다른 기능이 잠긴 것과는 무관");
            Characters.FeatureResolver = null;
            Assert.IsTrue(Characters.TryLevelUp("sock"), "null = 열림");
            Assert.AreEqual(3, Characters.GetLevel("sock"));
        }

        [Test]
        public void Evolution_CraftSubstituteAccelerate_RefusedWhileLocked()
        {
            Characters.Grant("sock");
            Data.lineages["sock"].level = 5; // sock_a · sock_b 단서 (단서 레벨 5)
            Data.lineages["sock"].shards = 100000;
            Inventory.Add("m_cloth", 3);
            Evolution.FeatureResolver = LockOnly(FeatureId.Evolution);

            Assert.IsFalse(Evolution.IsCraftUnlocked);
            Assert.AreEqual(EvolutionState.ClueFound, Evolution.GetState("sock_a"), "진화 기억도는 그대로 보인다");
            Assert.IsNotNull(Evolution.Check("sock_a"), "레시피도 그대로");
            Assert.IsFalse(Evolution.TryAccelerate("sock_a", 0), "가속 거절");
            Assert.IsFalse(Evolution.TrySubstitute("sock_b", "m_thread", 1), "재료 대체 거절");
            Assert.AreEqual(100000, Characters.GetShards("sock"), "기억편을 쓰지 않는다");

            Evolution.FeatureResolver = null;
            Assert.IsTrue(Evolution.TryAccelerate("sock_a", 0));
            Assert.IsTrue(Evolution.Check("sock_a").CanCraft);
            Evolution.FeatureResolver = LockOnly(FeatureId.Evolution);
            Assert.AreEqual(0, Evolution.CountCraftable(), "잠긴 동안 빨간 점 없음");
            Assert.AreEqual(0, Evolution.CountCraftable("sock"));
            Assert.IsFalse(Evolution.TryCraft("sock_a"), "제작 거절");
            Assert.IsFalse(Characters.IsFormUnlocked("sock_a"));
            Assert.AreEqual(3, Inventory.GetCount("m_cloth"), "재료를 쓰지 않는다");

            Evolution.FeatureResolver = LockOnly(FeatureId.Costumes);
            Assert.AreEqual(1, Evolution.CountCraftable());
            Assert.IsTrue(Evolution.TryCraft("sock_a"));
            Assert.IsTrue(Characters.IsFormUnlocked("sock_a"));
            Assert.IsTrue(Evolution.TrySubstitute("sock_b", "m_thread", 1), "열리면 대체도 된다");
        }

        [Test]
        public void Costumes_EquipAndUnlockRefusedWhileLocked_NightVisitStillRecorded()
        {
            Characters.Grant("sock");
            Costumes.FeatureResolver = LockOnly(FeatureId.Costumes);
            Clock.UtcNow = LocalHourAsUtc(22);

            Costumes.CheckNightVisit();
            Assert.IsTrue(Costumes.HasVisitedAtNight, "잠긴 동안에도 야간 방문은 기록한다");
            Assert.IsTrue(Costumes.CanUnlock("sock_night"), "조건은 그대로 보인다");
            Assert.AreEqual(0, Costumes.CountUnlockable(), "잠긴 동안 빨간 점 없음");
            Assert.AreEqual(0, Costumes.CountUnlockable("sock"));
            Assert.IsFalse(Costumes.TryUnlock("sock_night"), "받기 거절");
            Assert.IsFalse(Costumes.IsUnlocked("sock_night"));

            Costumes.Unlock("sock_event"); // 상점 구매 · 보상은 막지 않는다
            Assert.IsTrue(Costumes.IsUnlocked("sock_event"));
            Assert.IsFalse(Costumes.TryEquip("sock", "sock_event"), "입기 거절");
            Assert.IsFalse(Costumes.TryEquip("sock", null));
            Assert.IsNull(Costumes.GetEquipped("sock"));

            // 2-5 뒤: 낮이 되어도 기록한 방문으로 받는다
            Clock.UtcNow = LocalHourAsUtc(12);
            Costumes.FeatureResolver = null;
            Assert.AreEqual(1, Costumes.CountUnlockable());
            Assert.IsTrue(Costumes.TryUnlock("sock_night"));
            Assert.IsTrue(Costumes.TryEquip("sock", "sock_event"));
            Assert.AreEqual("sock_event", Costumes.GetEquipped("sock"));
        }

        [Test]
        public void ThrowingResolver_IsTreatedAsOpen()
        {
            Characters.Grant("sock");
            Currency.Add(Characters.LevelUpCurrency, 1e9);
            Func<FeatureId, bool> boom = _ => throw new InvalidOperationException("시험");
            Characters.FeatureResolver = boom;
            Evolution.FeatureResolver = boom;
            Costumes.FeatureResolver = boom;

            Assert.DoesNotThrow(() => Characters.TryLevelUp("sock"));
            Assert.AreEqual(2, Characters.GetLevel("sock"), "확인이 실패하면 열림으로 본다 (예외를 내지 않는다)");
            Assert.IsTrue(Evolution.IsCraftUnlocked);
            Assert.IsTrue(Costumes.IsFeatureUnlocked);
        }

        [Test]
        public void MetaSession_WiresFeatureGate()
        {
            AddFeature(FeatureId.CharacterLevelUp, MetaCondition.StageCleared(1, 1));
            AddFeature(FeatureId.Evolution, MetaCondition.StageCleared(1, 10));
            AddFeature(FeatureId.Costumes, MetaCondition.StageCleared(2, 5));
            Rebuild();
            MetaSession.Start();
            Characters.Grant("sock");
            Currency.Add(Characters.LevelUpCurrency, 1e9);

            Assert.IsFalse(Characters.TryLevelUp("sock"), "1-1 전에는 레벨업 거절");
            Assert.IsFalse(Evolution.IsCraftUnlocked);
            Assert.IsFalse(Costumes.IsFeatureUnlocked);

            SetCleared(1);
            Features.Evaluate();
            Assert.IsTrue(Characters.TryLevelUp("sock"), "1-1 클리어 뒤 열림");
            Assert.IsFalse(Evolution.IsCraftUnlocked, "진화는 1-10까지 잠김");

            SetCleared(15);
            Features.Evaluate();
            Assert.IsTrue(Evolution.IsCraftUnlocked);
            Assert.IsTrue(Costumes.IsFeatureUnlocked);
        }
    }
}
