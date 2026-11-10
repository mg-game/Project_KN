using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    /// <summary>빨간 점 트리 (F-04): 부모 합산, 개수 잎, 더러움 표시 뒤에만 평가, 수동 잎, 등록 해제, 평가 실패</summary>
    public class RedDotTests
    {
        RedDotSystem _dots;
        int _changed;

        [SetUp]
        public void SetUp()
        {
            _dots = new RedDotSystem();
            _changed = 0;
            _dots.Changed += () => _changed++;
        }

        [Test]
        public void Leaf_TurnsOnParents_AfterRefresh()
        {
            bool ready = false;
            _dots.Register(RedDotPaths.LineageEvolution("jjakjjak"), () => ready);
            _dots.Refresh();
            Assert.IsFalse(_dots.IsOn(RedDotPaths.TabCharacters));
            Assert.AreEqual(0, _changed, "처음 평가에서 꺼진 그대로면 바뀐 것이 없다");

            ready = true;
            Assert.IsFalse(_dots.IsOn(RedDotPaths.TabCharacters), "더러움 표시 전에는 다시 평가하지 않는다");
            _dots.MarkDirty();
            _dots.Refresh();

            Assert.IsTrue(_dots.IsOn("tab"));
            Assert.IsTrue(_dots.IsOn(RedDotPaths.TabCharacters));
            Assert.IsTrue(_dots.IsOn(RedDotPaths.Lineage("jjakjjak")));
            Assert.IsTrue(_dots.IsOn(RedDotPaths.LineageEvolution("jjakjjak")));
            Assert.IsFalse(_dots.IsOn(RedDotPaths.LineageResonance("jjakjjak")));
            Assert.IsFalse(_dots.IsOn(RedDotPaths.TabShop));
            Assert.AreEqual(1, _changed);
        }

        [Test]
        public void SimilarPrefix_DoesNotLeak()
        {
            _dots.Register("tab.shop", () => false);
            _dots.Register("tab.shopping.free", () => true);
            _dots.Refresh();

            Assert.IsFalse(_dots.IsOn("tab.shop"), "점 단위로만 부모");
            Assert.IsTrue(_dots.IsOn("tab.shopping"));
            Assert.IsTrue(_dots.IsOn("tab"));
        }

        [Test]
        public void CountLeaves_SumUnderParent()
        {
            int mail = 3;
            _dots.RegisterCount(RedDotPaths.TopMail, () => mail);
            _dots.RegisterCount(RedDotPaths.TopJournal, () => 2);
            _dots.Register(RedDotPaths.TopAttendance, () => true);
            _dots.Refresh();

            Assert.AreEqual(3, _dots.GetCount(RedDotPaths.TopMail));
            Assert.AreEqual(5, _dots.GetCount("top"), "켜짐/꺼짐 잎은 개수에 넣지 않는다");
            Assert.IsTrue(_dots.IsOn(RedDotPaths.TopAttendance));

            mail = 0;
            _dots.MarkDirty(RedDotPaths.TopMail);
            _dots.Refresh();
            Assert.IsFalse(_dots.IsOn(RedDotPaths.TopMail), "0이면 꺼짐");
            Assert.AreEqual(2, _dots.GetCount("top"));
        }

        [Test]
        public void MarkDirtyPath_OnlyReevaluatesSubtree()
        {
            int journalCalls = 0;
            int idleCalls = 0;
            _dots.Register(RedDotPaths.TopJournal, () => { journalCalls++; return false; });
            _dots.Register(RedDotPaths.HudIdleChest, () => { idleCalls++; return false; });
            _dots.Refresh();
            Assert.AreEqual(1, journalCalls);
            Assert.AreEqual(1, idleCalls);

            _dots.Refresh();
            Assert.AreEqual(1, journalCalls, "깨끗하면 평가하지 않는다");

            _dots.MarkDirty("hud");
            _dots.Refresh();
            Assert.AreEqual(1, journalCalls);
            Assert.AreEqual(2, idleCalls);

            _dots.MarkDirty("unknown.path");
            _dots.Refresh();
            Assert.AreEqual(2, idleCalls, "모르는 경로는 아무것도 하지 않는다");
        }

        [Test]
        public void Register_SamePathOverwrites()
        {
            _dots.Register(RedDotPaths.TopAttendance, () => true);
            _dots.Register(RedDotPaths.TopAttendance, () => false);
            _dots.Refresh();

            Assert.IsFalse(_dots.IsOn(RedDotPaths.TopAttendance));
            Assert.AreEqual(1, _dots.Paths.Count);
        }

        [Test]
        public void ManualLeaf_AndUnregister()
        {
            _dots.Set(RedDotPaths.TopSettingsNotice, true);
            Assert.IsTrue(_dots.IsOn(RedDotPaths.TopSettings), "Set은 바로 반영");
            _dots.Refresh();
            Assert.AreEqual(1, _changed, "Changed는 Refresh에서");

            _dots.Set(RedDotPaths.TopSettingsNotice, true);
            _dots.Refresh();
            Assert.AreEqual(1, _changed, "같은 값이면 바뀌지 않음");

            _dots.Unregister(RedDotPaths.TopSettingsNotice);
            Assert.IsFalse(_dots.IsOn(RedDotPaths.TopSettings));
            _dots.Refresh();
            Assert.AreEqual(2, _changed, "켜진 잎을 지우면 바뀜");
            CollectionAssert.DoesNotContain(_dots.Paths, RedDotPaths.TopSettingsNotice);
        }

        [Test]
        public void FailingEvaluator_IsLoggedAndKeepsOldValue()
        {
            bool fail = false;
            _dots.Register(RedDotPaths.TopAttendance, () => fail ? throw new InvalidOperationException("boom") : true);
            _dots.Refresh();
            Assert.IsTrue(_dots.IsOn(RedDotPaths.TopAttendance));

            fail = true;
            _dots.MarkDirty();
            // Unity는 LogException의 로그 메시지로 가장 안쪽 예외("InvalidOperationException: boom")를 쓰고,
            // 경로를 담은 바깥 예외는 스택에 "Rethrow as ..."로만 남긴다 → 안쪽 메시지로 맞춘다
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            Assert.DoesNotThrow(() => _dots.Refresh());
            Assert.IsTrue(_dots.IsOn(RedDotPaths.TopAttendance));
        }

        [Test]
        public void FeaturePaths_FollowParentTable()
        {
            Assert.AreEqual("tab.expedition.feature.Dungeon", RedDotPaths.FeatureDot(FeatureId.Dungeon));
            Assert.AreEqual("hud.idlechest.feature.QuickIdle", RedDotPaths.FeatureDot(FeatureId.QuickIdle));
            Assert.IsNull(RedDotPaths.FeatureDot(FeatureId.Battle));
            Assert.AreEqual("tab.expedition.done.exp_1_1", RedDotPaths.ExpeditionZone("exp_1_1"));
            Assert.AreEqual("tab.formation.empty", RedDotPaths.FormationEmpty, "편성 빈칸 잎은 편성 탭 아래 (M16)");
        }

        [Test]
        public void FormationEmptyLeaf_TurnsOnFormationTab()
        {
            bool vacancy = true;
            _dots.Register(RedDotPaths.FormationEmpty, () => vacancy);
            _dots.Refresh();
            Assert.IsTrue(_dots.IsOn(RedDotPaths.TabFormation));
            Assert.AreEqual(0, _dots.GetCount(RedDotPaths.TabFormation), "점이지 숫자가 아니다");

            vacancy = false;
            _dots.MarkDirty(RedDotPaths.FormationEmpty);
            _dots.Refresh();
            Assert.IsFalse(_dots.IsOn(RedDotPaths.TabFormation));
        }
    }
}
