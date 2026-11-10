using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 기능 개방 속도 조절 (SPEC5 §2.8 핵심을 4차 P4-0이 넣음 — 로드맵 Q2, 델타 15):
    /// FeatureUnlockDef.alsoRequire (AND), CounterKey.PlayMinutes (1분에 한 번, 잠자기 포함 · 일시정지 · 복귀 첫 프레임 제외),
    /// 개방 카드 간격 180초 (같은 평가에서 열린 카드는 이어서, 이미 들어가 본 기능의 카드는 버림), 잠김 안내 문구.
    /// 지금 콘텐츠에는 alsoRequire가 없고 발표자가 CardReady를 묻지 않으므로 게임은 그대로다 — 장치만 확인한다.
    /// </summary>
    public class FeaturePacingTests : MetaSystemsTestBase
    {
        readonly List<CounterKey> _increments = new();

        [SetUp]
        public void ClearRecords()
        {
            _increments.Clear();
            AppSignals.ResetState();
        }

        [TearDown]
        public void ResetSignals() => AppSignals.ResetState();

        FeatureUnlockDef AddPacedFeature(FeatureId feature, MetaCondition condition, long playMinutes)
        {
            var def = AddFeature(feature, condition);
            def.alsoRequire = new List<MetaCondition> { MetaCondition.Count(CounterKey.PlayMinutes, playMinutes) };
            MetaDb.ClearCache();
            return def;
        }

        void ListenPlayMinutes() => Progress.Incremented += (key, _) =>
        {
            if (key == CounterKey.PlayMinutes) _increments.Add(key);
        };

        void TickSeconds(int seconds)
        {
            for (int i = 0; i < seconds; i++) MetaSession.Tick(1f);
        }

        // ───────────── alsoRequire (AND) ─────────────

        [Test]
        public void AlsoRequire_NeedsConditionAndPlayFloor()
        {
            AddPacedFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8), 20);
            Rebuild();
            var opened = new List<FeatureId>();
            Features.Unlocked += f => opened.Add(f);

            SetCleared(8);
            Features.Evaluate();
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Shop), "스테이지만 넘어서는 안 열린다");

            Progress.Increment(CounterKey.PlayMinutes, 19);
            Features.Evaluate();
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Shop), "19분");

            Progress.Increment(CounterKey.PlayMinutes, 1);
            Progress.Flush(); // Progress.Changed → Evaluate
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Shop), "스테이지 + 20분");
            CollectionAssert.AreEqual(new[] { FeatureId.Shop }, opened);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.FeatureUnlock), "카드는 그대로 큐에");
        }

        [Test]
        public void AlsoRequire_AloneDoesNotOpen()
        {
            AddPacedFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8), 20);
            Rebuild();

            Progress.Increment(CounterKey.PlayMinutes, 300);
            Features.Evaluate();

            Assert.IsFalse(Features.IsUnlocked(FeatureId.Shop), "AND 조건만으로는 열리지 않는다 (conditions 중 하나가 먼저)");
        }

        [Test]
        public void EmptyAlsoRequire_KeepsTodaysRule()
        {
            AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            Rebuild();

            SetCleared(8);
            Features.Evaluate();

            Assert.IsTrue(Features.IsUnlocked(FeatureId.Shop));
        }

        [Test]
        public void AlsoRequire_IgnoredBySilentSyncAndForceUnlock()
        {
            AddPacedFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8), 20);
            AddPacedFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3), 20);
            Rebuild();

            // 옛 세이브 조용한 동기화: 이미 지난 기능은 바닥과 상관없이 연다
            Data.meta.features.silentSyncPending = true;
            SetCleared(8);
            Features.Evaluate();
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Shop));
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Gacha));
            Assert.IsFalse(Popups.Contains(MetaPopupKind.FeatureUnlock), "조용히");

            AddPacedFeature(FeatureId.Dungeon, MetaCondition.StageCleared(1, 10), 90);
            Rebuild();
            Features.ForceUnlock(FeatureId.Dungeon, showCard: false);
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Dungeon), "강제 개방은 바닥을 보지 않는다");
        }

        [Test]
        public void LockedText_WhenOnlyPlayFloorIsMissing()
        {
            AddPacedFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8), 20);
            Rebuild();

            Assert.IsTrue(Features.GetRequirementText(FeatureId.Shop).Contains("1-8"), "스테이지 전에는 스테이지 안내");

            SetCleared(8);
            Features.Evaluate();
            Assert.AreEqual(FeatureGateSystem.PlayFloorMessage, Features.GetRequirementText(FeatureId.Shop));
            Assert.AreEqual(string.Empty, Features.GetLockedHint(FeatureId.Shop), "지난 스테이지를 잠김 표시로 보여 주지 않는다");
            Assert.IsFalse(Features.TryUse(FeatureId.Shop, out string toast));
            Assert.AreEqual(FeatureGateSystem.PlayFloorMessage, toast);
        }

        // ───────────── PlayMinutes ─────────────

        [Test]
        public void PlayMinutes_IncrementsOncePerWholeMinute()
        {
            MetaSession.Start();
            ListenPlayMinutes();

            TickSeconds(59);
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.PlayMinutes));
            Assert.AreEqual(0, _increments.Count);

            TickSeconds(1);
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.PlayMinutes));
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.PlayMinutes));
            Assert.AreEqual(1, _increments.Count, "1분에 Increment 한 번");

            TickSeconds(120);
            Assert.AreEqual(3, Progress.GetLifetime(CounterKey.PlayMinutes));
            Assert.AreEqual(3, _increments.Count);
        }

        [Test]
        public void PlayMinutes_NotCountedBeforeStart()
        {
            for (int i = 0; i < 90; i++) MetaSession.Tick(1f);

            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.PlayMinutes));
        }

        [Test]
        public void PlayMinutes_PauseExcluded_ResumeFrameSkipped()
        {
            MetaSession.Start();
            TickSeconds(30);

            MetaSession.OnPause();
            TickSeconds(120); // 백그라운드 (실제로는 Update가 돌지 않지만, 돌아도 세지 않는다)
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.PlayMinutes), "일시정지 중은 세지 않는다");

            MetaSession.OnResume();
            MetaSession.Tick(9f); // 복귀 첫 프레임 (백그라운드 시간을 품을 수 있다) — 버린다
            Assert.AreEqual(30f, MetaSession.PendingPlaySeconds, 0.001f);

            TickSeconds(30);
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.PlayMinutes), "앞에 있던 30초 + 30초");
        }

        [Test]
        public void PlayMinutes_SleepIsCounted()
        {
            MetaSession.Start();
            AppSignals.RaiseSleep(true);

            TickSeconds(60);

            Assert.IsTrue(AppSignals.IsAsleep);
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.PlayMinutes), "잠자기 모드도 앱을 켜 둔 시간");
        }

        [Test]
        public void PlayMinutes_LongFrameIsClamped()
        {
            MetaSession.Start();

            MetaSession.Tick(3600f);

            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.PlayMinutes));
            Assert.AreEqual(MetaSession.MaxPlayDeltaSeconds, MetaSession.PendingPlaySeconds, 0.001f, "한 프레임은 최대 10초");
        }

        [Test]
        public void PlayMinutes_OpensPacedFeatureThroughSession()
        {
            AddPacedFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8), 2);
            Rebuild();
            SetCleared(8);
            MetaSession.Start();
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Shop));

            TickSeconds(120);

            Assert.IsTrue(Features.IsUnlocked(FeatureId.Shop), "2분 켜 두면 열린다 (Progress.Changed → Evaluate)");
        }

        // ───────────── 카드 간격 ─────────────

        [Test]
        public void CardSpacing_FirstCardReady_NextWaits180Seconds()
        {
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            Rebuild();

            SetCleared(3);
            Features.Evaluate();
            Assert.IsTrue(Features.CardReady(FeatureId.Gacha), "켜자마자 첫 카드는 바로");
            Features.NotifyCardClosed(FeatureId.Gacha);

            SetCleared(4);
            Features.Evaluate(); // 다른 평가
            Assert.IsFalse(Features.CardReady(FeatureId.Expedition));
            Assert.AreEqual(FeatureGateSystem.CardSpacingSeconds, Features.SecondsUntilNextCard, 0.001f);

            Features.TickCards(179f);
            Assert.IsFalse(Features.CardReady(FeatureId.Expedition), "179초");
            Features.TickCards(1f);
            Assert.IsTrue(Features.CardReady(FeatureId.Expedition), "180초");
            Assert.AreEqual(0f, Features.SecondsUntilNextCard, 0.001f);
        }

        [Test]
        public void CardSpacing_SameEvaluation_ShowsBackToBack()
        {
            AddFeature(FeatureId.Dungeon, MetaCondition.StageCleared(1, 10));
            AddFeature(FeatureId.Evolution, MetaCondition.StageCleared(1, 10));
            AddFeature(FeatureId.Speed2x, MetaCondition.StageCleared(2, 1));
            Rebuild();

            SetCleared(10);
            Features.Evaluate(); // 던전 + 진화 = 한 묶음
            Assert.IsTrue(Features.CardReady(FeatureId.Dungeon));
            Features.NotifyCardClosed(FeatureId.Dungeon);
            Assert.IsTrue(Features.CardReady(FeatureId.Evolution), "같은 평가에서 열린 두 번째 카드는 바로 이어서");
            Features.NotifyCardClosed(FeatureId.Evolution);

            SetCleared(11);
            Features.Evaluate();
            Assert.IsFalse(Features.CardReady(FeatureId.Speed2x), "다음 평가의 카드는 간격을 기다린다");
        }

        [Test]
        public void CardSpacing_SeenFeatureDropsItsCard()
        {
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            Rebuild();

            SetCleared(3);
            Features.Evaluate();
            Assert.IsFalse(Features.ShouldDropCard(FeatureId.Gacha), "아직 안 가 봄 → 띄운다");

            Features.MarkSeen(FeatureId.Gacha);
            Assert.IsTrue(Features.ShouldDropCard(FeatureId.Gacha), "이미 들어가 본 기능의 카드는 버린다");
        }

        [Test]
        public void CardSpacing_ClockCountsForegroundOnly()
        {
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            AddFeature(FeatureId.Expedition, MetaCondition.StageCleared(1, 4));
            Rebuild();
            SetCleared(3);
            MetaSession.Start();
            Features.NotifyCardClosed(FeatureId.Gacha);
            SetCleared(4);
            Features.Evaluate();

            TickSeconds(179);
            MetaSession.OnPause();
            TickSeconds(600);
            MetaSession.OnResume();
            MetaSession.Tick(600f); // 복귀 첫 프레임은 버림
            Assert.IsFalse(Features.CardReady(FeatureId.Expedition), "백그라운드 시간은 간격에 넣지 않는다");

            MetaSession.Tick(1f);
            Assert.IsTrue(Features.CardReady(FeatureId.Expedition));
        }
    }
}
