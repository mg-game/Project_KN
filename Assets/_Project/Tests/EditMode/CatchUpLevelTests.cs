using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// PRG-05 새 친구 따라잡기 (SPEC4 §6.5): 새 친구는 max(1, floor(0.8 × 보유 계보 레벨 상위 3개 평균))에서 시작한다 (최대 레벨까지).
    /// 픽스처에 계보 둘(thimble · spoon)을 더해 다섯 계보로 시험한다.
    /// </summary>
    public class CatchUpLevelTests : MetaSystemsTestBase
    {
        [SetUp]
        public void AddLineages()
        {
            UnityEngine.Object.DestroyImmediate(CharDb);
            CharDb = TestContent.CreateCharacters();
            CharDb.lineages.Add(Simple("thimble", ItemGrade.Rare));
            CharDb.lineages.Add(Simple("spoon", ItemGrade.Common));
            CreateSystems(new System.Random(1234));
        }

        static LineageDef Simple(string id, ItemGrade grade) => new LineageDef
        {
            id = id, name = id, grade = grade, baseFormId = id + "_base",
            forms = new List<CharacterFormDef> { TestContent.Form(id + "_base", null, 0, CombatStyle.Assault) },
        };

        void Own(string lineageId, int level)
        {
            Characters.Grant(lineageId);
            Data.lineages[lineageId].level = level;
        }

        [Test]
        public void NewFriend_StartsAt80PercentOfTopThree()
        {
            Own("sock", 20);
            Own("candle", 18);
            Own("button", 16);
            Assert.AreEqual(14, Characters.CatchUpLevel(), "평균 18 × 0.8 = 14.4 → 14");

            var result = Characters.Grant("thimble");

            Assert.IsTrue(result.IsNew);
            Assert.AreEqual(14, result.StartLevel);
            Assert.AreEqual(14, Characters.GetLevel("thimble"));
        }

        [Test]
        public void OnlyTheTopThreeCount()
        {
            Own("sock", 20);
            Own("candle", 18);
            Own("button", 16);
            Own("thimble", 1);

            Assert.AreEqual(14, Characters.Grant("spoon").StartLevel, "낮은 넷째 계보는 평균에 들지 않는다");
        }

        [Test]
        public void FewerThanThreeOwned_AveragesTheOwned()
        {
            Own("sock", 10);
            Own("candle", 6);

            Assert.AreEqual(6, Characters.Grant("button").StartLevel, "평균 8 × 0.8 = 6.4 → 6");
        }

        [Test]
        public void NoneOwned_StartsAtOne()
        {
            Assert.AreEqual(1, Characters.CatchUpLevel());
            var first = Characters.Grant("sock");
            Assert.AreEqual(1, first.StartLevel, "시작 친구는 Lv.1");
            Assert.AreEqual(1, Characters.GetLevel("sock"));
            Assert.AreEqual(1, Characters.Grant("candle").StartLevel, "Lv.1 친구들 → 0.8 → 1");
        }

        [Test]
        public void Duplicate_KeepsLevel_AndStartLevelIsOne()
        {
            Own("sock", 20);
            Own("candle", 3);

            var duplicate = Characters.Grant("candle");

            Assert.IsFalse(duplicate.IsNew);
            Assert.AreEqual(1, duplicate.StartLevel, "중복은 1 (따라잡기 없음)");
            Assert.AreEqual(3, Characters.GetLevel("candle"), "레벨 그대로");
            Assert.Greater(duplicate.ShardsGained, 0);
        }

        [Test]
        public void ClampsToMaxLevel()
        {
            Config.characterMaxLevel = 20;
            Own("sock", 50); // 최대 레벨이 내려간 옛 세이브
            Own("candle", 50);
            Own("button", 50);

            Assert.AreEqual(20, Characters.Grant("thimble").StartLevel, "40 → 최대 20");
        }

        [Test]
        public void NoLevelUpEvent_AndGrantedSeesTheStartLevel()
        {
            Own("sock", 10);
            int levelUps = 0;
            int seen = -1;
            Characters.LevelUp += (id, level) => levelUps++;
            Characters.Granted += r => seen = Characters.GetLevel(r.Lineage.id);

            Characters.Grant("candle");

            Assert.AreEqual(0, levelUps, "가짜 레벨업 팡파르 없음");
            Assert.AreEqual(8, seen, "Granted를 받는 쪽은 이미 시작 레벨을 본다");
        }

        [Test]
        public void MetaRewardLineage_UsesTheSameRule()
        {
            Own("sock", 15);
            Own("candle", 15);
            Own("button", 15);

            MetaRewards.Grant(new List<MetaReward> { MetaReward.Lineage("spoon") });

            Assert.AreEqual(12, Characters.GetLevel("spoon"), "출석 · 첫 만남 · 보상도 Grant를 거친다");
        }

        [Test]
        public void IgnoresSavesOfUnknownLineages()
        {
            Own("sock", 10);
            Data.lineages["removed_lineage"] = new LineageSave { level = 99 };

            Assert.AreEqual(8, Characters.CatchUpLevel(), "콘텐츠에 없는 계보 세이브는 세지 않는다");
        }
    }
}
