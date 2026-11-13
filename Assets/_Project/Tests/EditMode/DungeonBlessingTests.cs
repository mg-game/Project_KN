using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 잔향 축복 (PRG-07, SPEC4 §8.6 — 확장 항목, 표가 비면 꺼짐): 3 · 6 · 9층마다 고르기 (최대 10), 제안은 다시 열어도 같다,
    /// 2겹까지, 던전 덱 능력치에 더한다, 주 키마다 한 번 무료 다시 고르기, 던전 잠김이면 없음.
    /// </summary>
    public class DungeonBlessingTests : SystemsTestBase
    {
        static BlessingDef Def(string id, StatType stat, float value) =>
            new BlessingDef { id = id, name = id, desc = id, stat = stat, value = value, maxStacks = 2, iconKey = "skill_" + id };

        static List<BlessingDef> Pool(int count)
        {
            var all = new List<BlessingDef>
            {
                Def("b_attack", StatType.AttackPercent, 15),
                Def("b_shield", StatType.ShieldPowerPercent, 20),
                Def("b_crit", StatType.CritChancePercent, 8),
                Def("b_boss", StatType.BossDamagePercent, 20),
                Def("b_heal", StatType.HealPowerPercent, 25),
            };
            return all.GetRange(0, count);
        }

        [Test]
        public void Off_WhenPoolEmpty()
        {
            Data.dungeon.highestFloor = 9;
            Assert.IsFalse(Dungeon.IsBlessingOn);
            Assert.AreEqual(0, Dungeon.BlessingPicksPending);
            Assert.IsEmpty(Dungeon.GetBlessingOffer());
            Assert.IsFalse(Dungeon.TryPickBlessing("b_attack"));
            Assert.IsFalse(Dungeon.CanRespec);

            Dungeon.BlessingPool = new List<BlessingDef>();
            Assert.AreEqual(0, Dungeon.BlessingPicksPending, "빈 표 = 꺼짐 (콘텐츠 끄개)");
        }

        [Test]
        public void Picks_EveryThreeFloors_MaxTen()
        {
            Dungeon.BlessingPool = Pool(5);
            Data.dungeon.highestFloor = 2;
            Assert.AreEqual(0, Dungeon.BlessingPicksPending);
            Data.dungeon.highestFloor = 3;
            Assert.AreEqual(1, Dungeon.BlessingPicksPending);
            Data.dungeon.highestFloor = 6;
            Assert.AreEqual(2, Dungeon.BlessingPicksPending);
            Data.dungeon.highestFloor = 9;
            Assert.AreEqual(3, Dungeon.BlessingPicksPending);
            Data.dungeon.highestFloor = 40;
            Assert.AreEqual(10, Dungeon.BlessingPicksPending, "최대 10개");

            Dungeon.FeatureResolver = f => f != FeatureId.Dungeon;
            Assert.AreEqual(0, Dungeon.BlessingPicksPending, "던전 잠김");
        }

        [Test]
        public void Offer_ThreeDistinct_StableAcrossReopenAndReload()
        {
            Dungeon.BlessingPool = Pool(5);
            Data.dungeon.highestFloor = 6;
            var first = new List<string>(Dungeon.GetBlessingOffer());
            Assert.AreEqual(3, first.Count);
            CollectionAssert.AllItemsAreUnique(first);
            CollectionAssert.AreEqual(first, Dungeon.GetBlessingOffer(), "다시 열어도 같은 3개");
            CollectionAssert.AreEqual(first, Data.dungeon.blessingOffer, "저장된다");

            var reloaded = new DungeonSystem(Data, BattleCfg, Characters, Formation, Synergy, Rewards, Clock) { BlessingPool = Pool(5) };
            CollectionAssert.AreEqual(first, reloaded.GetBlessingOffer(), "다시 켜도 같다");

            Assert.IsFalse(Dungeon.TryPickBlessing("b_missing"));
            string notOffered = Pool(5).Find(d => !first.Contains(d.id)).id;
            Assert.IsFalse(Dungeon.TryPickBlessing(notOffered), "제안에 없는 축복은 고를 수 없다");

            int changed = 0;
            Dungeon.Changed += () => changed++;
            Assert.IsTrue(Dungeon.TryPickBlessing(first[1]));
            Assert.AreEqual(1, changed);
            CollectionAssert.AreEqual(new[] { first[1] }, Dungeon.Blessings);
            Assert.AreEqual(1, Dungeon.BlessingPicksPending);
            Assert.AreEqual(1, Data.dungeon.blessingPicksPending, "저장 칸도 맞춘다");
            Assert.AreEqual(3, Dungeon.GetBlessingOffer().Count, "다음 고르기는 새 제안");
        }

        [Test]
        public void Stacks_AtMostTwo()
        {
            Dungeon.BlessingPool = Pool(3); // 고를 수 있는 축복이 3개뿐이라 제안 = 전부
            Data.dungeon.highestFloor = 30;
            Assert.IsTrue(Dungeon.TryPickBlessing("b_attack"));
            Assert.IsTrue(Dungeon.TryPickBlessing("b_attack"));
            Assert.AreEqual(2, Dungeon.GetBlessingStacks("b_attack"));
            var offer = Dungeon.GetBlessingOffer();
            Assert.AreEqual(2, offer.Count);
            CollectionAssert.DoesNotContain(offer, "b_attack", "이미 2겹이면 빠진다");
            Assert.IsFalse(Dungeon.TryPickBlessing("b_attack"));

            Assert.IsTrue(Dungeon.TryPickBlessing("b_shield"));
            Assert.IsTrue(Dungeon.TryPickBlessing("b_shield"));
            Assert.IsTrue(Dungeon.TryPickBlessing("b_crit"));
            Assert.IsTrue(Dungeon.TryPickBlessing("b_crit"));
            Assert.AreEqual(4, Dungeon.BlessingPicksPending, "고르기는 남았지만");
            Assert.IsEmpty(Dungeon.GetBlessingOffer(), "고를 축복이 없다");
        }

        [Test]
        public void DeckSetup_IncludesBlessings()
        {
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            float before = Dungeon.CreateSetup(1).Modifiers.Team[StatType.AttackPercent];

            Dungeon.BlessingPool = Pool(3);
            Data.dungeon.highestFloor = 6;
            Assert.IsTrue(Dungeon.TryPickBlessing("b_attack"));
            Assert.IsTrue(Dungeon.TryPickBlessing("b_attack"));
            var setup = Dungeon.CreateSetup(1);
            Assert.AreEqual(before + 30f, setup.Modifiers.Team[StatType.AttackPercent], 1e-4, "15% × 2겹");

            Dungeon.BlessingPool = null;
            Assert.AreEqual(before, Dungeon.CreateSetup(1).Modifiers.Team[StatType.AttackPercent], 1e-4, "꺼지면 능력치도 없다");
        }

        [Test]
        public void Respec_OncePerWeekKey_ReturnsPicks()
        {
            Dungeon.BlessingPool = Pool(5);
            Data.dungeon.highestFloor = 9;
            Assert.IsFalse(Dungeon.CanRespec, "고른 축복이 없으면 다시 고를 것도 없다");
            Assert.IsTrue(Dungeon.TryPickBlessing(Dungeon.GetBlessingOffer()[0]));
            Assert.IsTrue(Dungeon.TryPickBlessing(Dungeon.GetBlessingOffer()[0]));
            Assert.AreEqual(1, Dungeon.BlessingPicksPending);

            Assert.IsTrue(Dungeon.CanRespec);
            Assert.IsTrue(Dungeon.Respec());
            Assert.IsEmpty(Dungeon.Blessings);
            Assert.AreEqual(3, Dungeon.BlessingPicksPending, "고르기를 돌려받는다 (= 층 / 3)");

            Assert.IsTrue(Dungeon.TryPickBlessing(Dungeon.GetBlessingOffer()[0]));
            Assert.IsFalse(Dungeon.CanRespec, "같은 주에는 한 번");
            Assert.IsFalse(Dungeon.Respec());

            Clock.Advance(TimeSpan.FromDays(7));
            Assert.IsTrue(Dungeon.CanRespec, "다음 주");
        }
    }
}
