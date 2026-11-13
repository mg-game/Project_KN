using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>픽스처 의상 (모두 sock 계보, 공격력 +5%): sock_night(야간 방문), sock_event(이벤트 상점), sock_stage(스테이지 5)</summary>
    public class CostumeSystemTests : SystemsTestBase
    {
        /// <summary>오늘 현지 시각 hour시를 UTC로 (기기 시간대와 무관하게 현지 기준 시각을 만든다)</summary>
        static DateTime LocalHourAsUtc(int hour) => DateTime.Today.AddHours(hour).ToUniversalTime();

        /// <summary>DB 캐시가 만들어지기 전에 픽스처를 고치기 위해 원본 목록에서 찾는다</summary>
        LineageDef RawLineage(string id) => CharDb.lineages.Find(l => l.id == id);

        /// <summary>야간 방문 의상(sock_night)은 지금 시각도 보므로, 기기 시간대와 무관하게 현지 낮 12시에서 시작한다</summary>
        [SetUp]
        public void StartAtLocalNoon() => Clock.UtcNow = LocalHourAsUtc(12);

        [Test]
        public void NightVisit_SetsFlagOnlyAtLocalNight()
        {
            Characters.Grant("sock");
            int changed = 0;
            Costumes.Changed += () => changed++;

            Clock.UtcNow = LocalHourAsUtc(12);
            Costumes.CheckNightVisit();
            Assert.IsFalse(Costumes.HasVisitedAtNight);
            Assert.IsFalse(Costumes.CanUnlock("sock_night"));
            Assert.AreEqual("밤 8시~새벽 6시에 수선점 방문", Costumes.GetProgressText("sock_night"));

            Clock.UtcNow = LocalHourAsUtc(22);
            Costumes.CheckNightVisit();
            Costumes.CheckNightVisit();

            Assert.IsTrue(Data.flags.Contains(CostumeSystem.NightVisitFlag));
            Assert.AreEqual(1, changed, "플래그는 한 번만 세운다");
            Assert.IsTrue(Costumes.CanUnlock("sock_night"));
            Assert.IsTrue(Costumes.TryUnlock("sock_night"));
            Assert.IsTrue(Costumes.IsUnlocked("sock_night"));
            Assert.AreEqual("보유 중", Costumes.GetProgressText("sock_night"));
        }

        [Test]
        public void NightVisit_CurrentNightCountsWithoutLaunchCheck()
        {
            // 저녁에 접속해 그대로 밤까지 플레이: 접속/복귀 시 CheckNightVisit는 낮에만 불렸다
            Characters.Grant("sock");
            Clock.UtcNow = LocalHourAsUtc(19);
            Costumes.CheckNightVisit();
            Assert.IsFalse(Costumes.CanUnlock("sock_night"));
            Assert.AreEqual(0, Costumes.CountUnlockable());

            Clock.UtcNow = LocalHourAsUtc(22);

            Assert.IsFalse(Costumes.HasVisitedAtNight);
            Assert.IsTrue(Costumes.IsNightNow);
            Assert.IsTrue(Costumes.CanUnlock("sock_night"));
            Assert.AreEqual(1, Costumes.CountUnlockable(), "빨간 점");
            Assert.AreEqual("지금 야간 방문 중", Costumes.GetProgressText("sock_night"));

            int changed = 0;
            Costumes.Changed += () => changed++;
            Assert.IsTrue(Costumes.TryUnlock("sock_night"));
            Assert.AreEqual(1, changed);
            Assert.IsTrue(Costumes.HasVisitedAtNight, "받을 때 야간 방문도 기록한다");

            Clock.UtcNow = LocalHourAsUtc(12);
            Assert.AreEqual("보유 중", Costumes.GetProgressText("sock_night"));
        }

        [Test]
        public void IsNight_Covers20To0559()
        {
            DateTime At(int hour) => new DateTime(2026, 11, 3, hour, 0, 0);

            Assert.IsTrue(CostumeSystem.IsNight(At(20)));
            Assert.IsTrue(CostumeSystem.IsNight(At(23)));
            Assert.IsTrue(CostumeSystem.IsNight(At(0)));
            Assert.IsTrue(CostumeSystem.IsNight(new DateTime(2026, 11, 3, 5, 59, 59)));
            Assert.IsFalse(CostumeSystem.IsNight(At(6)));
            Assert.IsFalse(CostumeSystem.IsNight(At(19)));
        }

        [Test]
        public void EventShopCostume_OnlyUnlocksThroughShop()
        {
            Characters.Grant("sock");

            Assert.IsFalse(Costumes.CanUnlock("sock_event"));
            Assert.IsFalse(Costumes.TryUnlock("sock_event"));
            Assert.AreEqual("'별이 머문 밤 산책' 이벤트 상점에서 구매", Costumes.GetProgressText("sock_event"));

            Costumes.Unlock("sock_event");
            Assert.IsTrue(Costumes.IsUnlocked("sock_event"));
        }

        [Test]
        public void StageCostume_TracksHighestStage()
        {
            Characters.Grant("sock");
            Data.battle.highestStage = 3;

            Assert.AreEqual("스테이지 1-3 / 1-5", Costumes.GetProgressText("sock_stage"), "챕터-번호 표기");
            Assert.IsFalse(Costumes.CanUnlock("sock_stage"));
            Assert.AreEqual(0, Costumes.CountUnlockable());

            Data.battle.highestStage = 6;
            Assert.AreEqual("스테이지 1-5 / 1-5", Costumes.GetProgressText("sock_stage"));
            Assert.IsTrue(Costumes.CanUnlock("sock_stage"));
            Assert.AreEqual(1, Costumes.CountUnlockable());
            Assert.AreEqual(1, Costumes.CountUnlockable("sock"));

            Assert.IsTrue(Costumes.TryUnlock("sock_stage"));
            Assert.AreEqual(0, Costumes.CountUnlockable());
            Assert.IsFalse(Costumes.TryUnlock("sock_stage"), "이미 보유");
        }

        [Test]
        public void ConditionTypes_UseLineageRecords()
        {
            var lineage = RawLineage("sock");
            lineage.costumes.Add(new CostumeDef { id = "sock_trip", name = "여행", unlockType = CostumeUnlockType.ExpeditionCount, unlockTarget = 2 });
            lineage.costumes.Add(new CostumeDef { id = "sock_res", name = "공명", unlockType = CostumeUnlockType.Resonance, unlockTarget = 1 });
            lineage.costumes.Add(new CostumeDef { id = "sock_evo", name = "진화", unlockType = CostumeUnlockType.FormUnlocked, unlockTargetId = "sock_a" });
            Characters.Grant("sock");

            Assert.AreEqual("원정 0/2회", Costumes.GetProgressText("sock_trip"));
            Assert.AreEqual("계보 공명 Lv.0/1", Costumes.GetProgressText("sock_res"));
            Assert.AreEqual("sock_a 진화 해금 필요", Costumes.GetProgressText("sock_evo"));
            Assert.AreEqual(0, Costumes.CountUnlockable());

            Characters.AddProgress("sock", ProgressType.ExpeditionCount, 2);
            Data.lineages["sock"].resonance = 1;
            Characters.UnlockForm("sock_a");

            Assert.IsTrue(Costumes.CanUnlock("sock_trip"));
            Assert.IsTrue(Costumes.CanUnlock("sock_res"));
            Assert.IsTrue(Costumes.CanUnlock("sock_evo"));
            Assert.AreEqual("sock_a 해금 완료", Costumes.GetProgressText("sock_evo"));
            Assert.AreEqual(3, Costumes.CountUnlockable());
        }

        [Test]
        public void DefaultCostume_GrantedWithLineage()
        {
            RawLineage("candle").costumes.Add(new CostumeDef { id = "candle_basic", name = "기본", unlockType = CostumeUnlockType.Default });

            Characters.Grant("candle");

            Assert.IsTrue(Costumes.IsUnlocked("candle_basic"));
        }

        [Test]
        public void Costumes_RequireOwnedLineage()
        {
            Data.battle.highestStage = 10;

            Assert.IsFalse(Costumes.CanUnlock("sock_stage"));
            Assert.IsFalse(Costumes.TryUnlock("sock_stage"));
            Assert.AreEqual("양말 획득 필요", Costumes.GetProgressText("sock_stage"));
            Assert.AreEqual(0, Costumes.CountUnlockable());
        }

        [Test]
        public void TryEquip_OnlyOwnedCostumeOfSameLineage()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");

            Assert.IsFalse(Costumes.TryEquip("sock", "sock_event"), "미보유 의상");
            Costumes.Unlock("sock_event");
            Assert.IsFalse(Costumes.TryEquip("candle", "sock_event"), "다른 계보 의상");
            Assert.IsFalse(Costumes.TryEquip("sock", "nope"));
            Assert.IsFalse(Costumes.TryEquip("button", null), "미보유 계보");

            Assert.IsTrue(Costumes.TryEquip("sock", "sock_event"));
            Assert.AreEqual("sock_event", Costumes.GetEquipped("sock"));
            Assert.IsNull(Costumes.GetEquipped("candle"));

            Assert.IsTrue(Costumes.TryEquip("sock", null));
            Assert.IsNull(Costumes.GetEquipped("sock"));
        }

        [Test]
        public void Unlock_RaisesCharacterChanged_ForCostumeBonus()
        {
            Characters.Grant("sock");
            float before = Characters.GetStats("sock_base").attack;
            int characterChanged = 0, costumeChanged = 0;
            Characters.Changed += () => characterChanged++;
            Costumes.Changed += () => costumeChanged++;

            Costumes.Unlock("sock_event");
            Costumes.Unlock("sock_event");

            Assert.AreEqual(1, characterChanged);
            Assert.AreEqual(1, costumeChanged);
            Assert.AreEqual(before * 1.05f, Characters.GetStats("sock_base").attack, 1e-3);
        }

        /// <summary>시즌 의상 (CostumeUnlockType.Season, 별빛 산책 수첩 30칸): 보상으로만 받는다 — 조건 해금 · 빨간 점 없음 (RM-2 P4-1 #11)</summary>
        [Test]
        public void SeasonCostume_IsRewardOnly_NeverAutoUnlocks()
        {
            RawLineage("sock").costumes.Add(new CostumeDef
            {
                id = "sock_starwalk", name = "별빛 산책 양말", unlockType = CostumeUnlockType.Season, unlockTarget = 1, attackBonusPercent = 5,
            });
            Characters.Grant("sock");
            Data.battle.highestStage = 999;
            Data.flags.Add(CostumeSystem.NightVisitFlag);

            Assert.IsFalse(Costumes.CanUnlock("sock_starwalk"), "조건으로 풀리지 않는다");
            Assert.IsFalse(Costumes.TryUnlock("sock_starwalk"));
            Assert.AreEqual("별빛 산책 수첩 보상", Costumes.GetProgressText("sock_starwalk"));
            StringAssert.DoesNotContain("개방", Costumes.GetProgressText("sock_starwalk"));
            int beforeDots = Costumes.CountUnlockable("sock");

            var granted = MetaRewards.Grant(new System.Collections.Generic.List<MetaReward> { MetaReward.Costume("sock_starwalk") });

            Assert.AreEqual(1, granted.Count, "MetaReward.Costume으로 받는다");
            Assert.IsTrue(Costumes.IsUnlocked("sock_starwalk"));
            Assert.AreEqual("보유 중", Costumes.GetProgressText("sock_starwalk"));
            Assert.AreEqual(beforeDots, Costumes.CountUnlockable("sock"), "빨간 점 수는 시즌 의상 때문에 바뀌지 않는다");
            Assert.IsTrue(Costumes.TryEquip("sock", "sock_starwalk"));
        }

        [Test]
        public void UnknownCostume_IsSafe()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.IsFalse(Costumes.IsUnlocked(null));
                Assert.IsFalse(Costumes.CanUnlock("nope"));
                Assert.IsFalse(Costumes.TryUnlock(null));
                Assert.AreEqual(string.Empty, Costumes.GetProgressText("nope"));
                Assert.IsNull(Costumes.Get("nope"));
                Assert.IsNull(Costumes.GetEquipped(null));
                Assert.AreEqual(0, Costumes.CountUnlockable("nope"));
                Costumes.Unlock("nope");
            });
            Assert.AreEqual(0, Data.unlockedCostumes.Count);
        }
    }
}
