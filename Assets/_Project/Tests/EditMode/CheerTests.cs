using System.Reflection;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>친구들의 응원 (PRG-03, SPEC4 §13.2): 장마다 한 번 · 600초 뒤 끝 · 되돌림 · 튜토리얼 막음 · TempAttackPercent 값 · 시작과 끝에 RequestUnitRefresh</summary>
    public class CheerTests : MetaSystemsTestBase
    {
        static readonly FieldInfo RefreshPending = typeof(BattleSystem).GetField("_refreshPending", BindingFlags.NonPublic | BindingFlags.Instance);

        bool RefreshRequested
        {
            get
            {
                Assert.IsNotNull(RefreshPending, "BattleSystem._refreshPending (RequestUnitRefresh)");
                return (bool)RefreshPending.GetValue(Battle);
            }
        }

        void Start()
        {
            Data.meta.ftue.done = true;
            SetUtc(2026, 11, 5, 12, 0);
            Cheer.Connect();
            _ = Battle.Sim; // 전투 시작
            Battle.Tick(0.01f); // 시작 때 쌓인 요청을 비운다
        }

        [Test]
        public void OncePerChapter()
        {
            Start();
            Assert.IsTrue(Cheer.CanUse);
            Assert.IsTrue(Cheer.TryUse());
            Assert.IsTrue(Cheer.IsActive);
            Assert.IsFalse(Cheer.TryUse(), "켜져 있는 동안 다시 못 받는다");

            AdvanceSeconds(601);
            Cheer.Tick(1.1f);
            Assert.IsFalse(Cheer.IsActive);
            Assert.IsFalse(Cheer.CanUse, "이 장에서 한 번");

            Data.battle.stage = 11; // 2장
            Assert.AreEqual(2, Cheer.CurrentChapter);
            Assert.IsTrue(Cheer.CanUse, "다음 장에서 다시");
        }

        [Test]
        public void EarlierChapter_DoesNotReopen_AndNeitherDoesComingBack()
        {
            Start();
            Data.battle.stage = 15; // 2장에서 받음
            Assert.AreEqual(2, Cheer.CurrentChapter);
            Assert.IsTrue(Cheer.TryUse());
            AdvanceSeconds(601);
            Cheer.Tick(1.1f);
            Assert.IsFalse(Cheer.IsActive);

            Data.battle.stage = 9; // 스테이지 목록 · 챕터 지도로 1장에 감
            Assert.AreEqual(1, Cheer.CurrentChapter);
            Assert.IsFalse(Cheer.CanUse, "앞 장으로 가도 다시 열리지 않는다");
            Assert.IsFalse(Cheer.TryUse());

            Data.battle.stage = 15; // 받은 장으로 돌아옴
            Assert.IsFalse(Cheer.CanUse, "받은 장으로 돌아와도 그대로");
            Assert.IsFalse(Cheer.TryUse());

            Data.battle.stage = 21; // 3장
            Assert.IsTrue(Cheer.CanUse, "뒤 장에서만 다시");
        }

        [Test]
        public void ChapterOfChallengeTarget_WhileFarming()
        {
            Start();
            Data.battle.stage = 9;
            Data.battle.challengeTarget = 10;
            Assert.AreEqual(1, Cheer.CurrentChapter);
            Data.battle.stage = 18;
            Data.battle.challengeTarget = 21;
            Assert.AreEqual(3, Cheer.CurrentChapter, "막힌 보스의 장");
        }

        [Test]
        public void ExpiresAfter600Seconds_EvenWhileClosed()
        {
            Start();
            Assert.IsTrue(Cheer.TryUse());
            Assert.AreEqual(600f, Cheer.Remaining, 1f);
            AdvanceSeconds(300);
            Assert.AreEqual(300f, Cheer.Remaining, 1f, "실제 시간으로 흐른다");
            Assert.IsTrue(Cheer.IsActive);
            AdvanceSeconds(300);
            Assert.IsFalse(Cheer.IsActive, "600초에 끝");
            Assert.AreEqual(0f, Cheer.Remaining);
        }

        [Test]
        public void TamperAndFtue_Block()
        {
            SetUtc(2026, 11, 5, 12, 0);
            Cheer.Connect();
            Assert.IsFalse(Cheer.CanUse, "튜토리얼 전");
            Assert.IsFalse(Cheer.TryUse());

            Data.meta.ftue.done = true;
            Data.meta.clock.maxSeenUnix = NowUnix + 3 * 24 * 3600;
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.IsFalse(Cheer.CanUse, "시계 되돌림");
            Data.meta.clock.maxSeenUnix = NowUnix;
            Assert.IsTrue(Cheer.CanUse);
        }

        [Test]
        public void TempAttackPercent_IsTwentyWhileActive()
        {
            Start();
            Assert.IsNotNull(Battle.TempAttackPercent, "Connect가 훅을 잇는다");
            Assert.AreEqual(0f, Battle.TempAttackPercent());
            Cheer.TryUse();
            Assert.AreEqual(20f, Battle.TempAttackPercent());
            AdvanceSeconds(601);
            Assert.AreEqual(0f, Battle.TempAttackPercent());
        }

        [Test]
        public void RequestUnitRefresh_AtStartAndEnd()
        {
            Start();
            Assert.IsFalse(RefreshRequested);
            int changed = 0;
            Cheer.Changed += () => changed++;

            Assert.IsTrue(Cheer.TryUse());
            Assert.IsTrue(RefreshRequested, "시작");
            Assert.AreEqual(1, changed);
            Battle.Tick(0.01f);
            Assert.IsFalse(RefreshRequested);

            AdvanceSeconds(601);
            Cheer.Tick(1.1f);
            Assert.IsTrue(RefreshRequested, "끝");
            Assert.AreEqual(2, changed);
        }

        [Test]
        public void NotInPower()
        {
            Start();
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            long before = Power.TeamPower;
            Cheer.TryUse();
            Power.MarkDirty();
            Assert.AreEqual(before, Power.TeamPower, "전투력 표시에는 넣지 않는다");
        }
    }
}
