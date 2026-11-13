using System.Collections.Generic;
using NUnit.Framework;
using IdleGame.UI;

namespace IdleGame.Tests
{
    /// <summary>
    /// 스킬 이름 · 대사 들여보내기 (FX-06, SPEC4 §3.4 — P4-3b): 1.2초 간격, 같은 스킬 10초, 높은 우선순위가 대기 칸을 바꾼다, 대기 0.6초 만료,
    /// 처음 본 스킬은 한 번만 기록, 설정을 끄면 모두 막힘, 기믹 안내는 상관없음
    /// </summary>
    public class CalloutGateTests
    {
        [Test]
        public void Spacing_OnePer1_2Seconds()
        {
            var gate = new CalloutGate();
            Assert.IsTrue(gate.TryAdmit("a", CalloutGate.P2, 0f, out bool queued));
            Assert.IsFalse(queued);
            Assert.IsFalse(gate.TryAdmit("b", CalloutGate.P2, 0.5f, out queued), "창 안");
            Assert.IsFalse(queued, "P2는 기다리지 않는다");
            Assert.IsFalse(gate.TryAdmit("b", CalloutGate.P2, 1.19f, out _));
            Assert.IsTrue(gate.TryAdmit("b", CalloutGate.P2, 1.2f, out _), "1.2초 뒤");
            Assert.AreEqual(1.2f, CalloutGate.Interval, 1e-6f);
        }

        [Test]
        public void SameSkill_BlockedFor10Seconds()
        {
            var gate = new CalloutGate();
            Assert.IsTrue(gate.TryAdmit("a", CalloutGate.P0, 0f, out _));
            Assert.IsFalse(gate.TryAdmit("a", CalloutGate.P0, 5f, out bool queued), "같은 스킬 10초 안");
            Assert.IsFalse(queued, "같은 스킬은 기다리지도 않는다");
            Assert.IsFalse(gate.TryAdmit("a", CalloutGate.P0, 9.9f, out _));
            Assert.IsTrue(gate.TryAdmit("a", CalloutGate.P0, 10.1f, out _));
        }

        [Test]
        public void HigherPriority_ReplacesPending()
        {
            var gate = new CalloutGate();
            Assert.IsTrue(gate.TryAdmit("x", CalloutGate.P2, 0f, out _));
            Assert.IsFalse(gate.TryAdmit("first", CalloutGate.P1, 0.7f, 7, out bool queued));
            Assert.IsTrue(queued, "P1은 창 안에서 기다린다");
            Assert.IsFalse(gate.TryAdmit("other", CalloutGate.P1, 0.8f, 8, out queued));
            Assert.IsFalse(queued, "같은 우선순위는 먼저 온 것이 남는다");
            Assert.IsFalse(gate.TryAdmit("ult", CalloutGate.P0, 0.9f, 9, out queued));
            Assert.IsTrue(queued, "P0이 기다리던 P1을 바꾼다");

            Assert.IsFalse(gate.TryTakePending(1.0f, out _), "아직 창 안");
            Assert.IsTrue(gate.TryTakePending(1.2f, out var c));
            Assert.AreEqual("ult", c.SkillId);
            Assert.AreEqual(CalloutGate.P0, c.Priority);
            Assert.AreEqual(9, c.Token);
            Assert.IsFalse(gate.HasPending);
        }

        [Test]
        public void Pending_ExpiresAfter0_6Seconds()
        {
            var gate = new CalloutGate();
            Assert.IsTrue(gate.TryAdmit("x", CalloutGate.P2, 0f, out _));
            Assert.IsFalse(gate.TryAdmit("late", CalloutGate.P1, 0.1f, out bool queued));
            Assert.IsTrue(queued);
            Assert.IsFalse(gate.TryTakePending(0.71f, out _), "0.6초 넘게 기다리면 버린다");
            Assert.IsFalse(gate.HasPending);
            Assert.IsFalse(gate.TryTakePending(2f, out _));
        }

        [Test]
        public void FirstSeen_MarksOnce_AndThenIsP2()
        {
            var seen = new HashSet<string>();
            int marks = 0;
            var gate = new CalloutGate
            {
                HasSeen = id => seen.Contains(id),
                MarkSeen = id =>
                {
                    marks++;
                    seen.Add(id);
                },
            };
            Assert.AreEqual(CalloutGate.P1, gate.PriorityFor("new", false), "처음 보는 스킬");
            Assert.AreEqual(CalloutGate.P0, gate.PriorityFor("new", true), "큰 스킬 · 전설 이상");
            Assert.AreEqual(0, marks, "묻기만 해서는 기록하지 않는다");

            Assert.IsTrue(gate.TryAdmit("new", gate.PriorityFor("new", false), 0f, out _));
            Assert.AreEqual(1, marks, "들여보낼 때 한 번 기록");
            Assert.AreEqual(CalloutGate.P2, gate.PriorityFor("new", false));
            Assert.IsTrue(gate.TryAdmit("new", CalloutGate.P1, 11f, out _));
            Assert.AreEqual(1, marks, "두 번 기록하지 않는다");

            // 도감이 없어도 이 판에서는 한 번만 처음
            var bare = new CalloutGate();
            Assert.AreEqual(CalloutGate.P1, bare.PriorityFor("s", false));
            Assert.IsTrue(bare.TryAdmit("s", CalloutGate.P1, 0f, out _));
            Assert.AreEqual(CalloutGate.P2, bare.PriorityFor("s", false));
        }

        [Test]
        public void OffSetting_BlocksAll()
        {
            var gate = new CalloutGate { Enabled = false };
            Assert.IsFalse(gate.TryAdmit("a", CalloutGate.P0, 0f, out bool queued));
            Assert.IsFalse(queued);
            Assert.IsFalse(gate.HasPending);

            gate.Enabled = true;
            Assert.IsTrue(gate.TryAdmit("a", CalloutGate.P2, 0f, out _));
            Assert.IsFalse(gate.TryAdmit("b", CalloutGate.P0, 0.5f, out queued));
            Assert.IsTrue(queued);
            gate.Enabled = false;
            Assert.IsFalse(gate.TryTakePending(1.3f, out _), "꺼지면 기다리던 것도 버린다");
        }

        [Test]
        public void GimmickNotice_Unaffected_SkillNoticeFollowsSetting()
        {
            Assert.IsTrue(CalloutGate.AllowsNotice(false, false), "기믹 안내 · 분노 알림 · 쓰다듬기는 설정과 상관없이");
            Assert.IsTrue(CalloutGate.AllowsNotice(false, true));
            Assert.IsFalse(CalloutGate.AllowsNotice(true, false), "스킬 이름 알림은 설정을 따른다");
            Assert.IsTrue(CalloutGate.AllowsNotice(true, true));
        }

        [Test]
        public void Notice_ConsumesTheWindow()
        {
            var gate = new CalloutGate();
            gate.NoteNotice("big", 0f);
            Assert.IsFalse(gate.TryAdmit("line", CalloutGate.P2, 0.6f, out _), "알림도 창을 쓴다");
            Assert.IsFalse(gate.TryAdmit("big", CalloutGate.P0, 1.5f, out _), "알림을 띄운 스킬은 10초 안에 대사로 다시 안 나온다");
            Assert.IsTrue(gate.TryAdmit("line", CalloutGate.P2, 1.3f, out _));

            gate.Reset();
            Assert.IsTrue(gate.TryAdmit("other", CalloutGate.P2, 1.4f, out _), "새 판은 창을 비운다");
        }
    }
}
