using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 첫 10분 튜토리얼 상태 기계 (FTUE-01): 단계 진행, trigger 종류, 재시작 이어 하기, 건너뛰기(졸업 선물 지급·"새로 열림" 점 하나만),
    /// 옛 세이브, FtueStep 조건 기능 개방.
    /// 조건 판정·기능 개방·팝업 큐·보상 지급은 M1의 메타 시스템을 그대로 쓴다.
    /// </summary>
    public class TutorialSystemTests : SystemsTestBase
    {
        readonly List<string> _started = new();
        readonly List<string> _completed = new();
        int _finished;

        static FtueStepDef Step(string id, MetaCondition complete, FtueTrigger trigger = FtueTrigger.Immediate, string triggerArg = null,
            FtueAction action = FtueAction.None, string actionArg = null, params MetaReward[] rewards) => new FtueStepDef
        {
            id = id,
            trigger = trigger,
            triggerArg = triggerArg,
            text = id,
            complete = complete,
            action = action,
            actionArg = actionArg,
            rewards = new List<MetaReward>(rewards),
        };

        /// <summary>MetaDb에 단계를 넣고 메타 시스템을 다시 만든 뒤 이벤트를 기록한다 (Data는 그대로)</summary>
        void Setup(params FtueStepDef[] steps)
        {
            MetaDb.ftueSteps = new List<FtueStepDef>(steps);
            MetaDb.ClearCache();
            Rebuild();
        }

        void Rebuild()
        {
            CreateMetaSystems();
            _started.Clear();
            _completed.Clear();
            _finished = 0;
            Tutorial.StepStarted += s => _started.Add(s.id);
            Tutorial.StepCompleted += s => _completed.Add(s.id);
            Tutorial.Finished += () => _finished++;
        }

        /// <summary>인트로(플래그) → 적 3 처치 → 졸업(별조각 300)</summary>
        void SetupBasic() => Setup(
            Step("intro", MetaCondition.Flag(MetaFlags.IntroSeen), action: FtueAction.StoryIntro),
            Step("kills", MetaCondition.Count(CounterKey.EnemyKill, 3), action: FtueAction.BattleProtection),
            Step("graduate", MetaCondition.Always(), action: FtueAction.Graduation, rewards: MetaReward.Gem(300)));

        [Test]
        public void NoSteps_IsDoneAndNeverActive()
        {
            Setup();
            Tutorial.Begin();

            Assert.IsTrue(Tutorial.IsDone);
            Assert.IsFalse(Tutorial.IsActive);
            Assert.IsNull(Tutorial.Current);
            Assert.IsFalse(Tutorial.IsStepRunning);
            Assert.IsEmpty(_started);
        }

        [Test]
        public void NewGame_BeginStartsFirstStep()
        {
            SetupBasic();
            Tutorial.Begin();

            Assert.IsTrue(Tutorial.IsActive);
            Assert.AreEqual(0, Tutorial.StepIndex);
            Assert.AreEqual("intro", Tutorial.Current.id);
            Assert.IsTrue(Tutorial.IsStepRunning);
            CollectionAssert.AreEqual(new[] { "intro" }, _started);

            // 두 번 불러도 다시 시작하지 않는다
            Tutorial.Begin();
            Assert.AreEqual(1, _started.Count);
        }

        [Test]
        public void Signal_CompletesFlagStep_AndStartsNextImmediately()
        {
            SetupBasic();
            Tutorial.Begin();

            Tutorial.Signal(MetaFlags.IntroSeen);

            Assert.IsTrue(Progress.HasFlag(MetaFlags.IntroSeen));
            CollectionAssert.AreEqual(new[] { "intro" }, _completed);
            Assert.AreEqual(1, Tutorial.StepIndex);
            Assert.AreEqual("kills", Tutorial.Current.id);
            Assert.IsTrue(Tutorial.IsStepRunning);
            CollectionAssert.AreEqual(new[] { "intro", "kills" }, _started);
        }

        [Test]
        public void CounterStep_CompletesOnRefresh_ThenGraduates()
        {
            SetupBasic();
            Tutorial.Begin();
            Tutorial.Signal(MetaFlags.IntroSeen);

            Progress.Increment(CounterKey.EnemyKill, 2);
            Tutorial.Refresh(false);
            Assert.AreEqual("kills", Tutorial.Current.id, "아직 2마리");

            Progress.Increment(CounterKey.EnemyKill);
            Tutorial.Refresh(false);

            Assert.IsTrue(Tutorial.IsDone);
            Assert.IsFalse(Tutorial.IsActive);
            Assert.IsTrue(Data.meta.ftue.done);
            Assert.IsFalse(Data.meta.ftue.skipped);
            Assert.AreEqual(3, Tutorial.StepIndex);
            CollectionAssert.AreEqual(new[] { "intro", "kills", "graduate" }, _completed);
            Assert.AreEqual(1, _finished);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.FtueGraduation), "졸업 카드가 큐에 들어감");
        }

        [Test]
        public void StepRewards_AreGrantedOnComplete()
        {
            SetupBasic();
            Tutorial.Begin();
            Tutorial.Signal(MetaFlags.IntroSeen);
            Progress.Increment(CounterKey.EnemyKill, 3);
            Tutorial.Refresh(false);

            Assert.AreEqual(300, Currency.Get(CurrencyType.Gem), 0.001);
        }

        [Test]
        public void BetweenStagesTrigger_WaitsForBetweenStages()
        {
            Setup(
                Step("boss", MetaCondition.Count(CounterKey.StageClear, 1)),
                Step("after", MetaCondition.Count(CounterKey.ShopLevelUp, 1), FtueTrigger.BetweenStages));
            Tutorial.Begin();
            Progress.Increment(CounterKey.StageClear);

            Tutorial.Refresh(false);
            Assert.AreEqual("after", Tutorial.Current.id);
            Assert.IsFalse(Tutorial.IsStepRunning, "전투 중에는 시작하지 않음");

            Tutorial.Refresh(true);
            Assert.IsTrue(Tutorial.IsStepRunning);
            CollectionAssert.AreEqual(new[] { "boss", "after" }, _started);
        }

        [Test]
        public void FeatureUnlockedTrigger_StartsWhenFeatureOpens()
        {
            MetaDb.features = new List<FeatureUnlockDef>
            {
                new FeatureUnlockDef
                {
                    feature = FeatureId.AutoBattle,
                    conditions = new List<MetaCondition> { MetaCondition.StageCleared(1, 2) },
                    showCard = false,
                },
            };
            Setup(Step("auto", MetaCondition.Always(), FtueTrigger.FeatureUnlocked, nameof(FeatureId.AutoBattle), FtueAction.EnableAutoBattle),
                Step("next", MetaCondition.Count(CounterKey.QuickIdle, 1)));
            Tutorial.Begin();

            Assert.IsFalse(Tutorial.IsStepRunning, "자동 전투가 아직 잠김");
            Assert.IsEmpty(_started);

            Features.ForceUnlock(FeatureId.AutoBattle, false);
            Tutorial.Refresh(false);

            CollectionAssert.AreEqual(new[] { "auto", "next" }, _started);
            CollectionAssert.AreEqual(new[] { "auto" }, _completed);
        }

        [Test]
        public void AfterDelayTrigger_StartsAfterSeconds()
        {
            Setup(
                Step("first", MetaCondition.Count(CounterKey.EchoSummon, 1)),
                Step("later", MetaCondition.Count(CounterKey.LampLit, 1), FtueTrigger.AfterDelay, "2"));
            Tutorial.Begin();
            Progress.Increment(CounterKey.EchoSummon);
            Tutorial.Refresh(false);
            Assert.AreEqual("later", Tutorial.Current.id);
            Assert.IsFalse(Tutorial.IsStepRunning);

            Tutorial.Tick(1.5f);
            Assert.IsFalse(Tutorial.IsStepRunning);
            Tutorial.Tick(0.6f);
            Assert.IsTrue(Tutorial.IsStepRunning);
        }

        [Test]
        public void HintOnlyStep_WithNoCondition_PassesStraightThrough()
        {
            var hint = Step("hint", MetaCondition.Always());
            hint.hintOnly = true;
            Setup(Step("summon", MetaCondition.Count(CounterKey.EchoSummon, 1)), hint,
                Step("lamp", MetaCondition.Count(CounterKey.LampLit, 1), action: FtueAction.ForceLampFull));
            Tutorial.Begin();

            Progress.Increment(CounterKey.EchoSummon);
            Tutorial.Refresh(false);

            CollectionAssert.AreEqual(new[] { "summon", "hint", "lamp" }, _started);
            Assert.AreEqual("lamp", Tutorial.Current.id);
            Assert.IsTrue(Tutorial.IsStepRunning);
        }

        [Test]
        public void Restart_ResumesSameStep()
        {
            SetupBasic();
            Tutorial.Begin();
            Tutorial.Signal(MetaFlags.IntroSeen);
            Progress.Increment(CounterKey.EnemyKill, 1);

            // 앱을 껐다 켬: 같은 세이브로 시스템을 다시 만든다
            Rebuild();
            Tutorial.Begin();

            Assert.AreEqual(1, Tutorial.StepIndex);
            Assert.AreEqual("kills", Tutorial.Current.id);
            Assert.IsTrue(Tutorial.IsStepRunning);
            CollectionAssert.AreEqual(new[] { "kills" }, _started, "끝낸 인트로는 다시 하지 않는다");
        }

        [Test]
        public void Restart_ResumesStartedBetweenStagesStep_WithoutWaitingAgain()
        {
            Setup(
                Step("boss", MetaCondition.Count(CounterKey.StageClear, 1)),
                Step("after", MetaCondition.Count(CounterKey.ShopLevelUp, 3), FtueTrigger.BetweenStages));
            Tutorial.Begin();
            Progress.Increment(CounterKey.StageClear);
            Tutorial.Refresh(true);
            Assert.IsTrue(Tutorial.IsStepRunning);

            Rebuild();
            Tutorial.Begin();

            Assert.AreEqual("after", Tutorial.Current.id);
            Assert.IsTrue(Tutorial.IsStepRunning, "시작했던 단계는 스테이지 사이를 다시 기다리지 않는다");
        }

        [Test]
        public void Restart_DoesNotResumeStepThatHadNotStarted()
        {
            Setup(
                Step("boss", MetaCondition.Count(CounterKey.StageClear, 1)),
                Step("after", MetaCondition.Count(CounterKey.ShopLevelUp, 3), FtueTrigger.BetweenStages));
            Tutorial.Begin();
            Progress.Increment(CounterKey.StageClear);
            Tutorial.Refresh(false);
            Assert.IsFalse(Tutorial.IsStepRunning);

            Rebuild();
            Tutorial.Begin();

            Assert.IsFalse(Tutorial.IsStepRunning);
        }

        [Test]
        public void Skip_FinishesQuietly_AndOpensFtueGatedFeaturesWithoutCard()
        {
            MetaDb.features = new List<FeatureUnlockDef>
            {
                new FeatureUnlockDef
                {
                    feature = FeatureId.Formation,
                    conditions = new List<MetaCondition> { MetaCondition.Of(MetaConditionType.FtueStep, 11), MetaCondition.StageCleared(1, 3) },
                    showCard = true,
                    cardText = "친구를 끌어 다른 방위에 세워 보세요",
                },
                new FeatureUnlockDef
                {
                    feature = FeatureId.Shop,
                    conditions = new List<MetaCondition> { MetaCondition.StageCleared(1, 8) },
                },
            };
            SetupBasic();
            Tutorial.Begin();
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Formation));

            Tutorial.Skip();

            Assert.IsTrue(Tutorial.IsDone);
            Assert.IsTrue(Data.meta.ftue.done);
            Assert.IsTrue(Data.meta.ftue.skipped);
            Assert.AreEqual(1, _finished);
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Formation), "튜토리얼 단계로 열리는 기능은 연다");
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Shop), "스테이지로 열리는 기능은 그대로");
            Assert.IsFalse(Popups.Contains(MetaPopupKind.FeatureUnlock), "건너뛰기는 카드 없이");
            Assert.IsFalse(Popups.Contains(MetaPopupKind.FtueGraduation), "건너뛰면 졸업 카드 없음");
            Assert.AreEqual(300, Currency.Get(CurrencyType.Gem), 0.001, "건너뛰어도 졸업 선물(마지막 단계 보상)은 받는다 (M74)");
            Assert.AreEqual("튜토리얼을 건너뛰었어요 · 별조각 300을 받았어요", Tutorial.LastSkipMessage);
            Assert.IsFalse(Features.IsNew(FeatureId.Formation), "가이드 퀘스트 대상·소환이 아닌 기능은 봤음 처리 (M09)");

            // 두 번 불러도 그대로 (선물도 한 번만)
            var again = Tutorial.Skip();
            Assert.AreEqual(1, _finished);
            Assert.IsEmpty(again);
            Assert.AreEqual(300, Currency.Get(CurrencyType.Gem), 0.001);
        }

        [Test]
        public void Skip_ReturnsGraduationGift_AndDescribesItBeforehand()
        {
            SetupBasic();
            Tutorial.Begin();
            Assert.AreEqual("별조각 300", Tutorial.GraduationGiftText, "설정 확인 창 \"졸업 선물(별조각 300)은 그대로 받아요\"");

            var granted = Tutorial.Skip();

            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual(MetaRewardType.Currency, granted[0].type);
            Assert.AreEqual(CurrencyType.Gem, granted[0].currency);
            Assert.AreEqual(300, granted[0].amount, 0.001);
            Assert.AreEqual(string.Empty, Tutorial.GraduationGiftText, "끝난 뒤에는 빈 문자열");
        }

        [Test]
        public void Skip_WithoutGift_ShowsPlainMessage()
        {
            Setup(Step("intro", MetaCondition.Flag(MetaFlags.IntroSeen)), Step("end", MetaCondition.Always()));
            Tutorial.Begin();

            Assert.IsEmpty(Tutorial.Skip());

            Assert.AreEqual(TutorialSystem.SkipMessage, Tutorial.LastSkipMessage);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gem), 0.001);
        }

        /// <summary>튜토리얼 단계(FtueStep)로 열리는 기능 셋 + 스테이지로만 열리는 기능 (건너뛰기 점 정리용)</summary>
        void SetupSkipFeatures()
        {
            MetaDb.features = new List<FeatureUnlockDef>
            {
                new FeatureUnlockDef
                {
                    feature = FeatureId.Gacha,
                    conditions = new List<MetaCondition> { MetaCondition.Of(MetaConditionType.FtueStep, 10), MetaCondition.StageCleared(1, 3) },
                },
                new FeatureUnlockDef
                {
                    feature = FeatureId.Formation,
                    conditions = new List<MetaCondition> { MetaCondition.Of(MetaConditionType.FtueStep, 11), MetaCondition.StageCleared(1, 3) },
                },
                new FeatureUnlockDef
                {
                    feature = FeatureId.Synergy,
                    conditions = new List<MetaCondition> { MetaCondition.Of(MetaConditionType.FtueStep, 11) },
                },
                new FeatureUnlockDef
                {
                    feature = FeatureId.Shop,
                    conditions = new List<MetaCondition> { MetaCondition.StageCleared(1, 8) },
                },
            };
            SetupBasic();
        }

        [Test]
        public void Skip_KeepsOnlyGachaNew_WhenGuideQuestHasNoTarget()
        {
            SetupSkipFeatures();
            Tutorial.Begin();

            Tutorial.Skip();

            Assert.IsTrue(Features.IsUnlocked(FeatureId.Gacha));
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Formation));
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Synergy));
            Assert.IsTrue(Features.IsNew(FeatureId.Gacha), "가이드 퀘스트 대상이 없으면 소환만 \"새로 열림\"으로 남긴다");
            Assert.IsFalse(Features.IsNew(FeatureId.Formation));
            Assert.IsFalse(Features.IsNew(FeatureId.Synergy));
            var fresh = new List<FeatureId>();
            Features.GetNewFeatures(fresh);
            CollectionAssert.AreEqual(new[] { FeatureId.Gacha }, fresh, "건너뛴 뒤 탭 점은 하나");
        }

        [Test]
        public void Skip_KeepsGuideQuestTargetNew()
        {
            SetupSkipFeatures();
            MetaDb.guideQuests = new List<GuideQuestDef>
            {
                new GuideQuestDef { id = "gq_01", text = "편성해 보세요", condition = MetaCondition.Always(), navFeature = FeatureId.Formation },
            };
            MetaDb.ClearCache();
            // Game.SetupMeta처럼 가이드 퀘스트를 넘긴 튜토리얼
            Tutorial = new TutorialSystem(Data, MetaDb, Progress, Features, MetaRewards, Popups, GuideQuest);
            Tutorial.Begin();

            Tutorial.Skip();

            Assert.IsTrue(Features.IsNew(FeatureId.Formation), "가이드 퀘스트가 가리키는 기능은 남긴다");
            Assert.IsFalse(Features.IsNew(FeatureId.Gacha), "대상이 있으면 소환도 봤음 처리");
            Assert.IsFalse(Features.IsNew(FeatureId.Synergy));
        }

        [Test]
        public void Skip_DoesNotCompleteStepsWhileOpeningFeatures()
        {
            // 열리는 기능을 trigger로 기다리는 단계가 있어도 건너뛰는 도중 단계가 끝나 보상이 나가지 않는다
            MetaDb.features = new List<FeatureUnlockDef>
            {
                new FeatureUnlockDef
                {
                    feature = FeatureId.Formation,
                    conditions = new List<MetaCondition> { MetaCondition.Of(MetaConditionType.FtueStep, 5) },
                },
            };
            Setup(
                Step("wait", MetaCondition.Always(), FtueTrigger.FeatureUnlocked, nameof(FeatureId.Formation), rewards: MetaReward.Gold(500)),
                Step("graduate", MetaCondition.Always(), action: FtueAction.Graduation, rewards: MetaReward.Gem(300)));
            Tutorial.Begin();
            Features.Changed += () => Tutorial.Refresh(false); // MetaSession.OnFeaturesChanged와 같은 연결

            Tutorial.Skip();

            Assert.IsEmpty(_completed, "건너뛰기 중에는 단계를 끝내지 않는다");
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gold), 0.001, "중간 단계 보상 없음");
            Assert.AreEqual(300, Currency.Get(CurrencyType.Gem), 0.001, "졸업 선물만");
        }

        [Test]
        public void LegacySave_IsDoneFromTheStart()
        {
            SetupBasic();
            Data.meta.MigrateFromLegacy(hasProgress: true);
            Rebuild();

            Tutorial.Begin();
            Tutorial.Refresh(true);

            Assert.IsTrue(Tutorial.IsDone);
            Assert.IsFalse(Tutorial.IsActive);
            Assert.IsEmpty(_started);
            Assert.IsFalse(Tutorial.WantsBattleProtection);
        }

        [Test]
        public void FtueStepCondition_OpensFeatureWhenStepReached()
        {
            MetaDb.features = new List<FeatureUnlockDef>
            {
                new FeatureUnlockDef
                {
                    feature = FeatureId.Formation,
                    conditions = new List<MetaCondition> { MetaCondition.Of(MetaConditionType.FtueStep, 2) },
                    showCard = true,
                },
            };
            Setup(
                Step("a", MetaCondition.Count(CounterKey.EchoSummon, 1)),
                Step("b", MetaCondition.Count(CounterKey.LampLit, 1)),
                Step("c", MetaCondition.Count(CounterKey.StageClear, 1)));
            Tutorial.Begin();

            Progress.Increment(CounterKey.EchoSummon);
            Tutorial.Refresh(false);
            Progress.Flush();
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Formation), "1단계만 끝냄");

            Progress.Increment(CounterKey.LampLit);
            Tutorial.Refresh(false);
            Progress.Flush();
            Assert.IsTrue(Features.IsUnlocked(FeatureId.Formation), "2단계까지 끝내면 열림");
        }

        [Test]
        public void WantsBattleProtection_FromProtectionStepUntilDone()
        {
            SetupBasic();
            Tutorial.Begin();
            Assert.IsFalse(Tutorial.WantsBattleProtection, "인트로 중에는 아직");

            Tutorial.Signal(MetaFlags.IntroSeen);
            Assert.IsTrue(Tutorial.WantsBattleProtection);

            Progress.Increment(CounterKey.EnemyKill, 3);
            Tutorial.Refresh(false);
            Assert.IsTrue(Tutorial.IsDone);
            Assert.IsFalse(Tutorial.WantsBattleProtection, "졸업하면 끔");
        }

        [Test]
        public void HintFlags_AreSavedInFtueFlags()
        {
            SetupBasic();
            Assert.IsFalse(Tutorial.HasHintFlag("hint.drag_summon"));

            Tutorial.SetHintFlag("hint.drag_summon");

            Assert.IsTrue(Tutorial.HasHintFlag("hint.drag_summon"));
            Assert.IsTrue(Data.meta.ftue.flags.Contains("hint.drag_summon"));
            Rebuild();
            Assert.IsTrue(Tutorial.HasHintFlag("hint.drag_summon"));
        }

        [Test]
        public void StepPastContent_FinishesWithoutCard()
        {
            SetupBasic();
            Data.meta.ftue.step = 10;
            Rebuild();

            Tutorial.Begin();

            Assert.IsTrue(Tutorial.IsDone);
            Assert.AreEqual(1, _finished);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.FtueGraduation));
        }
    }
}

namespace IdleGame.Tests
{
    /// <summary>
    /// FTUE 1-1~1-3 "질 수 없는 전투" (UI/Tutorial/FtueBattleGuard → 전투 훅, SPEC4 §13.3 · RM-2 S-9): 가드가 BattleSystem.ProtectionResolver를 이어
    /// 시뮬레이터가 적 체력 60%(등장할 때 한 번) · 수선점 최소 1을 걸고, 첫 불 밝히기 게이지는 BattleSystem.ForceLampFull로 채운다.
    /// 보호 자체는 BattleProtectionTests, 가드 → 전투 API는 UiWave2_WP8Tests의 FtueBattleApiTests가 더 본다.
    /// </summary>
    public class FtueBattleGuardTests : SystemsTestBase
    {
        readonly System.Collections.Generic.List<BattleOutcome> _ended = new();
        UI.FtueBattleGuard _guard;

        [SetUp]
        public void SetUpGuard()
        {
            _ended.Clear();
            Battle.StageEnded += (outcome, _) => _ended.Add(outcome);
            MetaDb.ftueSteps = new System.Collections.Generic.List<FtueStepDef>
            {
                new FtueStepDef { id = "intro", complete = MetaCondition.Flag(MetaFlags.IntroSeen), action = FtueAction.StoryIntro },
                new FtueStepDef { id = "battle", complete = MetaCondition.Count(CounterKey.StageClear, 99), action = FtueAction.BattleProtection },
            };
            MetaDb.ClearCache();
            CreateMetaSystems();
            Tutorial.Begin();
            _guard = new UI.FtueBattleGuard { RequireMetaActive = false };
        }

        [TearDown]
        public void TearDownGuard() => _guard.Detach();

        void SetEnemies(float hp, float damage)
        {
            foreach (var enemy in BattleCfg.enemies)
            {
                enemy.hp = hp;
                enemy.damage = damage;
            }
        }

        /// <summary>지킴이 없이 잔향이 한 칸만 (자동 소환 끔) — 첫 적에 수선점이 무너지는 판</summary>
        void SetUpLosingBattle()
        {
            SetEnemies(1e6f, 1e6f);
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.AutoMode = false;
        }

        void TickSeconds(float seconds)
        {
            for (float t = 0f; t < seconds && _ended.Count == 0; t += 0.1f) Battle.Tick(0.1f);
        }

        [Test]
        public void WithoutGuard_LosingBattleFails()
        {
            SetUpLosingBattle();
            TickSeconds(120f);
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(BattleOutcome.Failed, _ended[0]);
        }

        [Test]
        public void Guard_KeepsShopStanding_DuringFtue()
        {
            SetUpLosingBattle();
            _guard.Attach(Battle, Tutorial);
            Assert.IsTrue(_guard.IsProtecting, "인트로 중에도 지킨다");

            TickSeconds(120f);

            Assert.IsEmpty(_ended, "FTUE 1-1은 질 수 없다");
            Assert.GreaterOrEqual(Battle.Sim.ShopHp, UI.FtueBattleGuard.ShopFloorHp - 0.001f);
        }

        [Test]
        public void Guard_EnemiesWeakenedOnce_BySimulatorProtection()
        {
            SetEnemies(100f, 1f);
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            float before = -1f, after = -1f;
            // 지킴이 훅 앞뒤에서 같은 적의 최대 체력을 읽는다 (이벤트는 구독 순서대로)
            Battle.Event += e =>
            {
                if (e.Type == BattleEventType.EnemySpawned && before < 0f) before = Find(e.SourceId).MaxHp;
            };
            _guard.Attach(Battle, Tutorial);
            Battle.Event += e =>
            {
                if (e.Type == BattleEventType.EnemySpawned && after < 0f) after = Find(e.SourceId).MaxHp;
            };

            for (float t = 0f; t < 30f && after < 0f; t += 0.1f) Battle.Tick(0.1f);

            Assert.Greater(before, 0f, "적이 나왔다");
            Assert.AreEqual(before, after, before * 0.001f, "가드는 이벤트 안에서 더 깎지 않는다 (두 번 약해지지 않음)");
            Assert.IsTrue(Battle.Sim.IsProtected, "등장할 때 × 0.6은 시뮬레이터 보호가");
            Assert.AreEqual(UI.FtueBattleGuard.EnemyHpScale, Battle.Sim.Protection.EnemyHpScale, 1e-4f);
        }

        [Test]
        public void Guard_IsOff_AfterFtueAndAboveStage3()
        {
            SetUpLosingBattle();
            _guard.Attach(Battle, Tutorial);
            Tutorial.Skip();
            Assert.IsFalse(_guard.IsProtecting, "튜토리얼이 끝나면 끔");

            TickSeconds(120f);
            Assert.AreEqual(BattleOutcome.Failed, _ended[0]);

            Data.meta.ftue.done = false;
            Data.meta.ftue.skipped = false;
            Data.battle.stage = 4;
            Data.battle.highestStage = 4;
            Assert.IsFalse(_guard.IsProtecting, "1-4부터는 보호하지 않는다");
        }

        [Test]
        public void TryFillLamp_FillsGaugeOnce_WhenBattleRuns()
        {
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            _guard.Attach(Battle, Tutorial);
            Battle.Tick(0.1f);
            Assert.IsTrue(UI.FtueBattleGuard.CanFillLamp, "전투 훅(ForceLampFull)이 있어 늘 채울 수 있다");
            Assert.IsFalse(Battle.Sim.CanLightLamp);

            Assert.IsTrue(_guard.TryFillLamp());

            Assert.IsTrue(Battle.Sim.CanLightLamp);
            Assert.IsFalse(_guard.TryFillLamp(), "이미 가득");
        }

        EnemyState Find(int id)
        {
            var enemies = Battle.Sim.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Id == id) return enemies[i];
            }
            return null;
        }
    }
}
