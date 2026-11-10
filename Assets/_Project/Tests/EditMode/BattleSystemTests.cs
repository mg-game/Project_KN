using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    public class BattleSystemTests : SystemsTestBase
    {
        readonly List<BattleOutcome> _ended = new();
        int _stageChanged;

        [SetUp]
        public void SetUpBattle()
        {
            _ended.Clear();
            _stageChanged = 0;
            Battle.StageEnded += (outcome, _) => _ended.Add(outcome);
            Battle.StageChanged += () => _stageChanged++;
        }

        void SetEnemies(float hp, float damage)
        {
            foreach (var enemy in BattleCfg.enemies)
            {
                enemy.hp = hp;
                enemy.damage = damage;
            }
        }

        /// <summary>결과가 나올 때까지 진행 (최대 seconds)</summary>
        void TickUntilEnded(int count, float seconds = 300f)
        {
            for (float t = 0f; t < seconds && _ended.Count < count; t += 0.1f) Battle.Tick(0.1f);
        }

        void TickSeconds(float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.1f) Battle.Tick(0.1f);
        }

        void PlaceThreeGuardians()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");
            Formation.TryPlace(CharacterRole.Guardian, 3, "button_base");
        }

        [Test]
        public void FormatStage_UsesTenStagesPerChapter()
        {
            Assert.AreEqual("1-1", BattleSystem.FormatStage(1));
            Assert.AreEqual("1-10", BattleSystem.FormatStage(10));
            Assert.AreEqual("2-1", BattleSystem.FormatStage(11));
            Assert.AreEqual("2-3", BattleSystem.FormatStage(13));
            Assert.AreEqual("1-1", BattleSystem.FormatStage(0));
            Assert.AreEqual("1-1", Battle.StageLabel);
        }

        [Test]
        public void ClearRewards_GiveButtonsAndCandy()
        {
            var stage1 = Battle.GetClearRewards(1);
            var gold1 = stage1.Find(r => r.type == RewardType.Currency && r.currency == CurrencyType.Gold);
            var candy1 = stage1.Find(r => r.type == RewardType.Currency && r.currency == CurrencyType.EventToken);
            Assert.IsNotNull(gold1);
            Assert.AreEqual(BattleCfg.stageCandy, candy1.amount);

            var gold5 = Battle.GetClearRewards(5).Find(r => r.currency == CurrencyType.Gold && r.type == RewardType.Currency);
            Assert.Greater(gold5.amount, gold1.amount, "스테이지가 높을수록 단추가 많다");

            var candy10 = Battle.GetClearRewards(10).Find(r => r.type == RewardType.Currency && r.currency == CurrencyType.EventToken);
            Assert.AreEqual(BattleCfg.stageCandyMilestone, candy10.amount, "10스테이지마다 별빛 사탕 많이");
        }

        [Test]
        public void ClearRewards_CandyFollowsEventMultiplier()
        {
            // P4-2 R4: '별이 머문 밤 산책' ×2 (EventCalendarSystem.Connect가 CandyMultiplier를 넣는다). 1 이하 · 무한이면 그대로, 단추는 배율을 따르지 않는다
            double Candy(int stage) => Battle.GetClearRewards(stage).Find(r => r.type == RewardType.Currency && r.currency == CurrencyType.EventToken).amount;
            double Gold(int stage) => Battle.GetClearRewards(stage).Find(r => r.type == RewardType.Currency && r.currency == CurrencyType.Gold).amount;
            double gold1 = Gold(1);
            Battle.CandyMultiplier = () => 2.0;
            Assert.AreEqual(BattleCfg.stageCandy * 2, Candy(1));
            Assert.AreEqual(BattleCfg.stageCandyMilestone * 2, Candy(10), "10스테이지 사탕도 두 배");
            Assert.AreEqual(gold1, Gold(1), "단추는 그대로");
            Battle.CandyMultiplier = () => 0.5;
            Assert.AreEqual(BattleCfg.stageCandy, Candy(1), "1 아래로는 줄이지 않는다");
            Battle.CandyMultiplier = () => double.PositiveInfinity;
            Assert.AreEqual(BattleCfg.stageCandy, Candy(1), "무한은 무시");
            Battle.CandyMultiplier = () => throw new System.InvalidOperationException("candy hook");
            LogAssert.Expect(LogType.Exception, new Regex("candy hook"));
            Assert.AreEqual(BattleCfg.stageCandy, Candy(1), "훅이 예외를 내도 기본 사탕");
            Assert.IsNull(Battle.CandyMultiplier, "예외를 낸 훅은 떼어 낸다");
        }

        [Test]
        public void EmptyFormation_RunsWithoutEnemies()
        {
            Assert.DoesNotThrow(() => TickSeconds(30f));

            Assert.IsFalse(Battle.Sim.HasDefenders);
            Assert.AreEqual(BattleOutcome.Running, Battle.Sim.Outcome);
            Assert.AreEqual(0, Battle.Sim.Enemies.Count);
            Assert.IsEmpty(_ended);
        }

        [Test]
        public void Clear_GrantsRewards_RecordsProgress_AndAdvances()
        {
            SetEnemies(1f, 1f);
            BattleCfg.killsToBossBase = 3;
            BattleCfg.killsToBossPerStage = 0f;
            PlaceThreeGuardians();

            TickUntilEnded(1);

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(BattleOutcome.Cleared, _ended[0]);
            Assert.IsTrue(Battle.IsBetweenStages);
            Assert.AreEqual(1, Battle.Stage, "결과 배너 동안은 끝난 스테이지 표시");
            Assert.AreEqual(2, Battle.HighestStage);
            Assert.GreaterOrEqual(Currency.Get(CurrencyType.Gold), Battle.GetClearRewards(1)[0].amount);
            Assert.AreEqual(BattleCfg.stageCandy, Currency.Get(CurrencyType.EventToken), 1e-9);
            foreach (var id in new[] { "sock", "candle", "button" })
                Assert.AreEqual(BattleCfg.clearAffinity, Characters.GetAffinity(id), id);
            int guardianKills = 0;
            foreach (var id in new[] { "sock", "candle", "button" })
                guardianKills += Characters.GetProgress(id, ProgressType.GuardianKillCount);
            Assert.Greater(guardianKills, 0, "지킴이 처치 기록");

            int changedBefore = _stageChanged;
            TickSeconds(BattleCfg.stageEndPause + 0.2f);

            Assert.IsFalse(Battle.IsBetweenStages);
            Assert.AreEqual(2, Battle.Stage, "자동 진행");
            Assert.AreEqual("1-2", Battle.StageLabel);
            Assert.AreEqual(changedBefore + 1, _stageChanged);
            Assert.AreEqual(2, Battle.Sim.Level);
        }

        [Test]
        public void Clear_WithoutAutoAdvance_RepeatsStage()
        {
            SetEnemies(1f, 1f);
            BattleCfg.killsToBossBase = 2;
            BattleCfg.killsToBossPerStage = 0f;
            PlaceThreeGuardians();
            Battle.AutoAdvance = false;

            TickUntilEnded(1);
            TickSeconds(BattleCfg.stageEndPause + 0.2f);

            Assert.AreEqual(BattleOutcome.Cleared, _ended[0]);
            Assert.AreEqual(1, Battle.Stage);
            Assert.AreEqual(2, Battle.HighestStage);
        }

        [Test]
        public void TwoFailsInARow_StepBackOneStage()
        {
            SetEnemies(1e6f, 1e6f);
            Data.battle.stage = 3;
            Data.battle.highestStage = 3;
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.AutoMode = false; // 잔향이를 내보내지 않아 첫 적에 무너진다

            TickUntilEnded(1);
            Assert.AreEqual(BattleOutcome.Failed, _ended[0]);
            TickSeconds(BattleCfg.stageEndPause + 0.2f);
            Assert.AreEqual(3, Battle.Stage, "한 번 실패는 같은 스테이지 재도전");
            Assert.AreEqual(1, Battle.ConsecutiveFails);

            TickUntilEnded(2);
            Assert.AreEqual(BattleOutcome.Failed, _ended[1]);
            TickSeconds(BattleCfg.stageEndPause + 0.2f);
            Assert.AreEqual(2, Battle.Stage, "연속 실패 → 한 단계 아래");
            Assert.AreEqual(3, Battle.HighestStage);
        }

        // ───────────── 보스에 막힘: 파밍 / 보스 도전 (M13) ─────────────

        /// <summary>stage에서 연속 두 번 실패시킨다 (잔향이만 두고 자동 소환을 꺼서 첫 적에 무너진다). 끝나면 두 번째 결과 배너 대기 중</summary>
        void FailTwiceAt(int stage)
        {
            SetEnemies(1e6f, 1e6f);
            Data.battle.stage = stage;
            Data.battle.highestStage = stage;
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.AutoMode = false;
            TickUntilEnded(_ended.Count + 2);
            Assert.AreEqual(BattleOutcome.Failed, _ended[_ended.Count - 1]);
        }

        /// <summary>약한 적 + 지킴이 셋으로 바꿔 이제부터는 클리어되게 한다</summary>
        void SwitchToWinningTeam()
        {
            SetEnemies(1f, 1f);
            BattleCfg.killsToBossBase = 2;
            BattleCfg.killsToBossPerStage = 0f;
            Formation.Remove(CharacterRole.Echo, 0);
            PlaceThreeGuardians();
            Battle.AutoMode = true;
        }

        /// <summary>전투 하나를 끝까지 하고 결과 배너 대기도 넘긴다 (다음 전투 시작)</summary>
        BattleOutcome PlayOne()
        {
            int before = _ended.Count;
            TickUntilEnded(before + 1);
            Assert.AreEqual(before + 1, _ended.Count, "전투가 끝나야 한다");
            TickSeconds(BattleCfg.stageEndPause + 0.2f);
            return _ended[_ended.Count - 1];
        }

        void UnlockAutoChallenge() => Data.meta.features.unlocked.Add(FeatureId.AutoChallenge);

        [Test]
        public void StepBack_RecordsChallengeTarget_AndRaisesEventBeforeStageEnded()
        {
            var order = new List<string>();
            Battle.StepBack += (from, to) => order.Add($"step {from}->{to} target {Battle.ChallengeTarget}");
            Battle.StageEnded += (outcome, _) => order.Add($"ended {outcome} stepped {Battle.LastEndSteppedBack}");

            FailTwiceAt(3);

            CollectionAssert.AreEqual(new[]
            {
                "ended Failed stepped False",
                "step 3->2 target 3",
                "ended Failed stepped True",
            }, order);
            Assert.AreEqual(3, Battle.ChallengeTarget);
            Assert.AreEqual(3, Data.battle.challengeTarget, "세이브에 바로 쓴다");
            TickSeconds(BattleCfg.stageEndPause + 0.2f);
            Assert.AreEqual(2, Battle.Stage);
            Assert.IsFalse(Battle.IsFarming, "자동 보스 도전이 열리기 전에는 파밍 상태를 보여 주지 않는다 (배너 한 줄만)");
        }

        [Test]
        public void StepBack_NotRaisedOnFirstStage()
        {
            int steps = 0;
            Battle.StepBack += (_, _) => steps++;

            FailTwiceAt(1);
            TickSeconds(BattleCfg.stageEndPause + 0.2f);

            Assert.AreEqual(0, steps);
            Assert.AreEqual(0, Battle.ChallengeTarget);
            Assert.IsFalse(Battle.LastEndSteppedBack);
            Assert.AreEqual(1, Battle.Stage);
        }

        [Test]
        public void BeforeAutoChallengeUnlock_ClearGoesStraightBackToBoss()
        {
            Assert.IsFalse(Battle.IsAutoChallengeUnlocked);
            Battle.AutoChallenge = false;
            FailTwiceAt(3);
            SwitchToWinningTeam();

            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(3, Battle.Stage, "지금처럼 붙잡지 않고 바로 다시 도전 (새 플레이어·방치 진행 유지)");
            Assert.AreEqual(3, Battle.ChallengeTarget);

            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(4, Battle.Stage);
            Assert.AreEqual(0, Battle.ChallengeTarget, "막힌 스테이지를 넘으면 지운다");
            Assert.AreEqual(0, Data.battle.challengeTarget);
        }

        [Test]
        public void AfterUnlock_FarmHoldsUntilChallengeBoss()
        {
            UnlockAutoChallenge(); // 세이브의 열린 기능 목록 (FeatureResolver가 없을 때)
            Assert.IsTrue(Battle.IsAutoChallengeUnlocked);
            Battle.AutoChallenge = false; // 반복 토글을 끈 경우 (기본은 켜짐)
            FailTwiceAt(3);
            TickSeconds(BattleCfg.stageEndPause + 0.2f);
            Assert.AreEqual(2, Battle.Stage);
            Assert.IsTrue(Battle.IsFarming);

            SwitchToWinningTeam();
            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(2, Battle.Stage, "파밍 중 클리어 = 같은 스테이지 반복 (붙잡기)");
            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(2, Battle.Stage);
            Assert.AreEqual(0, Battle.FarmClears, "자동 보스 도전이 꺼져 있으면 세지 않는다");
            Assert.IsTrue(Battle.IsFarming);

            int changed = _stageChanged;
            Assert.IsTrue(Battle.ChallengeBoss());
            Assert.AreEqual(3, Battle.Stage);
            Assert.AreEqual(3, Battle.Sim.Level);
            Assert.AreEqual(changed + 1, _stageChanged, "바로 새 전투");
            Assert.IsFalse(Battle.IsFarming);
            Assert.IsFalse(Battle.ChallengeBoss(), "이미 막힌 스테이지");

            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(4, Battle.Stage);
            Assert.AreEqual(0, Battle.ChallengeTarget);
            Assert.IsFalse(Battle.ChallengeBoss(), "막힌 곳이 없다");
        }

        [Test]
        public void AutoChallenge_ReChallengesEveryThirdFarmClear()
        {
            Battle.FeatureResolver = feature => feature == FeatureId.AutoChallenge;
            Battle.AutoChallenge = false;
            Assert.IsFalse(Data.battle.autoChallenge, "저장됨");
            Battle.AutoChallenge = true;
            Assert.IsTrue(Data.battle.autoChallenge, "저장됨");
            FailTwiceAt(3);
            SwitchToWinningTeam();

            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(2, Battle.Stage);
            Assert.AreEqual(1, Battle.FarmClears);
            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(2, Battle.Stage);
            Assert.AreEqual(2, Battle.FarmClears);
            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(3, Battle.Stage, "AutoChallengeEvery(3)번째 클리어 뒤 막힌 스테이지에 다시 도전");
            Assert.AreEqual(0, Battle.FarmClears);
            Assert.IsFalse(Battle.IsFarming);
        }

        /// <summary>
        /// 기본값(자동 보스 도전 켜짐)이면 개방 뒤에도 진행이 멈추지 않는다: 버튼을 누르지 않아도 파밍 AutoChallengeEvery번 뒤 다시 도전하고
        /// 넘으면 다음 스테이지로 간다 (UI가 아직 ChallengeBoss를 부르지 않아도 막힌 플레이어가 갇히지 않게 — M13).
        /// </summary>
        [Test]
        public void AfterUnlock_DefaultAutoChallenge_KeepsProgressGoing()
        {
            UnlockAutoChallenge();
            Assert.IsTrue(Battle.AutoChallenge, "새 게임 기본값은 켜짐");
            FailTwiceAt(3);
            SwitchToWinningTeam();

            for (int i = 1; i < BattleSystem.AutoChallengeEvery; i++)
            {
                Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
                Assert.AreEqual(2, Battle.Stage, "파밍 붙잡기");
                Assert.IsTrue(Battle.IsFarming);
            }
            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(3, Battle.Stage, "버튼 없이도 다시 도전");
            Assert.AreEqual(BattleOutcome.Cleared, PlayOne());
            Assert.AreEqual(4, Battle.Stage, "막힌 보스를 넘으면 다음으로");
            Assert.AreEqual(0, Battle.ChallengeTarget);
        }

        /// <summary>
        /// StepBack·StageEnded 동안은 아직 막힌 스테이지라 IsFarming = false, 내려간 스테이지의 다음 StageChanged부터 true.
        /// HUD는 보스 도전 버튼을 StageChanged에서 다시 판단해야 한다 (StepBack 인자는 배너 문구용).
        /// </summary>
        [Test]
        public void StepBack_IsFarmingFalseDuringEvent_TrueFromNextStageChanged()
        {
            UnlockAutoChallenge();
            var seen = new List<string>();
            bool steppedBack = false;
            bool changedAfterStep = false;
            Battle.StepBack += (from, to) =>
            {
                steppedBack = true;
                seen.Add($"step {from}->{to} stage {Battle.Stage} farming {Battle.IsFarming}");
            };
            Battle.StageEnded += (outcome, _) =>
            {
                if (steppedBack) seen.Add($"ended stage {Battle.Stage} farming {Battle.IsFarming}");
            };
            Battle.StageChanged += () =>
            {
                if (!steppedBack || changedAfterStep) return;
                changedAfterStep = true;
                seen.Add($"changed stage {Battle.Stage} farming {Battle.IsFarming}");
            };

            FailTwiceAt(3);
            TickSeconds(BattleCfg.stageEndPause + 0.2f);

            CollectionAssert.AreEqual(new[]
            {
                "step 3->2 stage 3 farming False",
                "ended stage 3 farming False",
                "changed stage 2 farming True",
            }, seen);
        }

        /// <summary>Game.SetupMeta가 전투의 자동 보스 도전 개방 판정을 FeatureGateSystem.IsUnlocked로 잇는다 (정의 없음 = 열림 규칙까지 같게)</summary>
        [Test]
        public void GameSetup_WiresFeatureResolverToFeatureGate()
        {
            var content = new GameContent
            {
                Config = Config,
                Items = Items,
                Characters = CharDb,
                GachaTables = new[] { Table },
                Battle = BattleCfg,
                Expeditions = ExpeditionDb,
                Shops = ShopDb,
                Synergies = SynergyDb,
                Meta = MetaDb,
            };
            try
            {
                Game.Setup(PlayerData.CreateNew(Config), content, Clock, new System.Random(7));

                Assert.IsNotNull(Game.Battle.FeatureResolver, "Game.SetupMeta가 넣는다");
                // 빈 메타 데이터: 기능 개방은 "정의 없음 = 열림"인데, 세이브 목록만 보는 대체 경로는 닫힘으로 봤다
                Assert.IsTrue(Game.Features.IsUnlocked(FeatureId.AutoChallenge));
                Assert.IsTrue(Game.Battle.IsAutoChallengeUnlocked, "기능 개방과 같은 판정");
            }
            finally
            {
                Game.Shutdown();
            }
            Assert.IsFalse(Game.IsReady);
        }

        [Test]
        public void FeatureResolver_OverridesSaveList()
        {
            UnlockAutoChallenge();
            Battle.FeatureResolver = _ => false;
            Assert.IsFalse(Battle.IsAutoChallengeUnlocked);
            Battle.FeatureResolver = null;
            Assert.IsTrue(Battle.IsAutoChallengeUnlocked);
        }

        [Test]
        public void GoToStage_KeepsChallengeTarget()
        {
            UnlockAutoChallenge();
            FailTwiceAt(3);
            TickSeconds(BattleCfg.stageEndPause + 0.2f);

            Battle.GoToStage(1);

            Assert.AreEqual(1, Battle.Stage);
            Assert.AreEqual(3, Battle.ChallengeTarget, "더 아래로 가서 파밍해도 막힌 스테이지는 그대로");
            Assert.IsTrue(Battle.IsFarming);
            Assert.IsTrue(Battle.ChallengeBoss());
            Assert.AreEqual(3, Battle.Stage);
        }

        [Test]
        public void BattleSave_ChallengeFields_DefaultSafelyForOldSaves()
        {
            var old = Newtonsoft.Json.JsonConvert.DeserializeObject<BattleSave>("{\"stage\":7,\"highestStage\":9,\"autoMode\":true}");
            old.Sanitize();
            Assert.AreEqual(0, old.challengeTarget, "옛 세이브 = 막힌 곳 없음");
            Assert.IsTrue(old.autoChallenge, "옛 세이브도 자동 보스 도전 켜짐 (필드 초기값) — 개방 뒤 파밍에 갇히지 않게");
            Assert.IsTrue(new BattleSave().autoChallenge, "새 게임 기본값");

            var turnedOff = Newtonsoft.Json.JsonConvert.DeserializeObject<BattleSave>(
                Newtonsoft.Json.JsonConvert.SerializeObject(new BattleSave { autoChallenge = false }));
            Assert.IsFalse(turnedOff.autoChallenge, "끈 값은 저장·불러오기 뒤에도 꺼짐 (기본값이라고 빠지지 않는다)");

            var oldPlayer = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>("{\"battle\":{\"stage\":12,\"highestStage\":12}}");
            Assert.IsTrue(oldPlayer.battle.autoChallenge, "PlayerData로 읽어도 같다");

            var broken = new BattleSave { stage = 2, highestStage = 5, challengeTarget = 9 };
            broken.Sanitize();
            Assert.AreEqual(0, broken.challengeTarget, "도달한 적 없는 스테이지에 막혔을 수 없다");
            broken.challengeTarget = -1;
            broken.Sanitize();
            Assert.AreEqual(0, broken.challengeTarget);
            broken.challengeTarget = 5;
            broken.Sanitize();
            Assert.AreEqual(5, broken.challengeTarget);
        }

        [Test]
        public void Summon_RecordsEchoDeployCount()
        {
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.AutoMode = false;

            Assert.IsTrue(Battle.TrySummon(0));

            Assert.AreEqual(1, Characters.GetProgress("button", ProgressType.EchoDeployCount));
            Assert.AreEqual(1, Battle.Sim.Echoes.Count);
        }

        [Test]
        public void GuardianKill_IsWrittenToSaveImmediately()
        {
            SetEnemies(1f, 1f);
            PlaceThreeGuardians();
            int killEvents = 0;
            int recordedAtKill = -1;
            Battle.Event += e =>
            {
                if (e.Type != BattleEventType.EnemyKilled || e.SourceId < 0) return;
                killEvents++;
                recordedAtKill = 0;
                foreach (var id in new[] { "sock", "candle", "button" })
                    recordedAtKill += Data.lineages[id].progress.TryGetValue(ProgressType.GuardianKillCount, out int count) ? count : 0;
            };

            for (float t = 0f; t < 120f && killEvents == 0; t += 0.1f) Battle.Tick(0.1f);

            Assert.Greater(killEvents, 0);
            Assert.AreEqual(killEvents, recordedAtKill, "처치 기록은 모아 두지 않고 바로 세이브 데이터에 쓴다 (저장 직후 종료돼도 남게)");
        }

        [Test]
        public void FormationChange_RestartsOnNextTick()
        {
            Characters.Grant("sock");
            Battle.Tick(0.1f);
            Assert.IsNull(Battle.Sim.Guardians[1]);
            int changedBefore = _stageChanged;

            Formation.TryPlace(CharacterRole.Guardian, 1, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "sock_base");
            Assert.AreEqual(changedBefore, _stageChanged, "핸들러 안에서 바로 다시 시작하지 않는다");

            Battle.Tick(0.1f);

            Assert.AreEqual(changedBefore + 1, _stageChanged, "여러 번 바뀌어도 한 번만 다시 시작");
            Assert.IsNull(Battle.Sim.Guardians[1]);
            Assert.AreEqual("sock_base", Battle.Sim.Guardians[2].FormId);
        }

        [Test]
        public void CharacterLevelUp_RefreshesStatsWithoutRestart()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Battle.Tick(0.1f);
            float attack = Battle.Sim.Guardians[0].Stats.attack;
            int changedBefore = _stageChanged;
            float elapsed = Battle.Sim.ElapsedTime;

            Data.lineages["sock"].level = 5;
            Characters.Grant("sock"); // 중복 획득 → Changed 알림
            Battle.Tick(0.1f);

            Assert.AreEqual(changedBefore, _stageChanged, "능력치만 바뀌면 전투를 다시 시작하지 않는다");
            Assert.Greater(Battle.Sim.Guardians[0].Stats.attack, attack);
            Assert.Greater(Battle.Sim.ElapsedTime, elapsed);
        }

        [Test]
        public void Settings_ArePersisted()
        {
            Battle.Speed = 5;
            Assert.AreEqual(BattleSystem.MaxSpeed, Battle.Speed);
            Assert.AreEqual(BattleSystem.MaxSpeed, Data.battle.speed);
            Battle.Speed = 0;
            Assert.AreEqual(1, Data.battle.speed);

            Battle.AutoMode = false;
            Assert.IsFalse(Data.battle.autoMode);
            Assert.IsFalse(Battle.Sim.AutoMode);

            Battle.AutoAdvance = false;
            Assert.IsFalse(Data.battle.autoAdvance);
        }

        [Test]
        public void GoToStage_ClampsToHighest()
        {
            Data.battle.highestStage = 5;

            Battle.GoToStage(9);
            Assert.AreEqual(5, Battle.Stage);
            Battle.GoToStage(-3);
            Assert.AreEqual(1, Battle.Stage);
            Assert.AreEqual(1, Battle.Sim.Level);
        }

        [Test]
        public void ShopUpgrade_RaisesShopMaxHp()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Battle.Tick(0.1f);
            Assert.AreEqual(Growth.ShopMaxHp, Battle.Sim.ShopMaxHp, 1e-3);

            Currency.Add(CurrencyType.Gold, 1e6);
            Growth.TryLevelUp(10);
            Battle.Tick(0.1f);

            Assert.AreEqual(Growth.ShopMaxHp, Battle.Sim.ShopMaxHp, 1e-2);
            Assert.Greater(Battle.Sim.ShopMaxHp, GrowthSystem.ShopBaseHp);
        }

        // ───────────── 4차 전투 훅 (SPEC4 §13.3 · §7.2 · §13.2, P4-3b) ─────────────

        /// <summary>잔향이만 두고 자동 소환을 꺼서 첫 적에 무너지는 판 (결과 배너 대기 중에 끝난다)</summary>
        void FailOnce()
        {
            SetEnemies(1e6f, 1e6f);
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.AutoMode = false;
            TickUntilEnded(_ended.Count + 1);
            Assert.AreEqual(BattleOutcome.Failed, _ended[_ended.Count - 1]);
            Assert.IsTrue(Battle.IsBetweenStages);
        }

        [Test]
        public void PresentationScale_ScalesSimOnly()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Battle.Speed = 1;
            Battle.Tick(0.1f);
            float before = Battle.Sim.ElapsedTime;

            Battle.PresentationScale = 0.25f;
            for (int i = 0; i < 10; i++) Battle.Tick(0.1f);
            float slowed = Battle.Sim.ElapsedTime - before;

            Battle.PresentationScale = 1f;
            for (int i = 0; i < 10; i++) Battle.Tick(0.1f);
            float normal = Battle.Sim.ElapsedTime - before - slowed;

            Assert.AreEqual(0.25f, slowed, 0.03f, "1초 동안 0.25배");
            Assert.AreEqual(1f, normal, 0.03f, "다시 1배");
            Battle.PresentationScale = 0f;
            Assert.AreEqual(0.05f, Battle.PresentationScale, 1e-6f, "0.05 아래로 내려가지 않는다");
            Battle.PresentationScale = float.NaN;
            Assert.AreEqual(1f, Battle.PresentationScale);
        }

        [Test]
        public void PresentationScale_DoesNotScaleEndPause()
        {
            FailOnce();
            int changedBefore = _stageChanged;
            Battle.PresentationScale = 0.05f;

            TickSeconds(BattleCfg.stageEndPause + 0.2f);

            Assert.IsFalse(Battle.IsBetweenStages, "결과 배너 대기는 실제 시간");
            Assert.AreEqual(changedBefore + 1, _stageChanged);
        }

        [Test]
        public void ExtendEndPause_DelaysRestart_SkipEndPause_RestartsNow()
        {
            Battle.ExtendEndPause(4.8f);
            Assert.AreEqual(0f, Battle.NextStageDelay, "전투 중에는 아무것도 하지 않는다");

            FailOnce();
            Battle.ExtendEndPause(4.8f);
            Assert.AreEqual(4.8f, Battle.NextStageDelay, 0.11f, "끝난 순간부터 전체 4.8초");
            int changedBefore = _stageChanged;

            TickSeconds(BattleCfg.stageEndPause + 0.3f);
            Assert.IsTrue(Battle.IsBetweenStages, "기본 대기(1.5초)가 지나도 아직 다음 전투 전");
            Assert.AreEqual(changedBefore, _stageChanged);

            Battle.ExtendEndPause(2f);
            Assert.Greater(Battle.NextStageDelay, 2.5f, "더 짧게 부르면 줄이지 않는다");
            Battle.ExtendEndPause(60f);
            Assert.LessOrEqual(Battle.NextStageDelay, BattleSystem.MaxEndPause - BattleCfg.stageEndPause, "최대 6초");

            Battle.SkipEndPause();
            Battle.Tick(0.1f);
            Assert.IsFalse(Battle.IsBetweenStages, "바로 다시 도전: 다음 Tick에 시작");
            Assert.AreEqual(changedBefore + 1, _stageChanged);

            Battle.SkipEndPause();
            Assert.IsFalse(Battle.IsBetweenStages, "전투 중 Skip은 아무것도 하지 않는다");
        }

        [Test]
        public void TempAttackPercent_ScalesAttackMultiplier_OnRefresh()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Battle.Tick(0.1f);
            float baseMultiplier = Battle.Sim.Setup.AttackMultiplier;
            int changedBefore = _stageChanged;

            float cheer = 20f;
            Battle.TempAttackPercent = () => cheer;
            Battle.RequestUnitRefresh();
            Battle.Tick(0.1f);

            Assert.AreEqual(baseMultiplier * 1.2f, Battle.Sim.Setup.AttackMultiplier, 1e-4f, "응원 +20%");
            Assert.AreEqual(changedBefore, _stageChanged, "편성은 그대로 → 다시 시작하지 않는다");

            cheer = 0f;
            Battle.RequestUnitRefresh();
            Battle.Tick(0.1f);
            Assert.AreEqual(baseMultiplier, Battle.Sim.Setup.AttackMultiplier, 1e-4f, "응원이 끝나면 원래대로");

            Battle.TempAttackPercent = () => -50f;
            Battle.RequestUnitRefresh();
            Battle.Tick(0.1f);
            Assert.AreEqual(baseMultiplier, Battle.Sim.Setup.AttackMultiplier, 1e-4f, "음수는 0으로");
        }

        [Test]
        public void ClearDropHook_ReplacesMaterialRoll_WhenItRolls()
        {
            SetEnemies(1f, 1f);
            BattleCfg.killsToBossBase = 2;
            BattleCfg.killsToBossPerStage = 0f;
            BattleCfg.stageClothChance = 1f;
            BattleCfg.stageRibbonChance = 1f;
            PlaceThreeGuardians();
            int calls = 0;
            Battle.ClearDropHook = (stage, rewards) =>
            {
                calls++;
                rewards.Add(RewardData.Item("mat_cloth", 7));
                return true;
            };
            List<RewardData> granted = null;
            Battle.StageEnded += (_, rewards) => granted = rewards;

            TickUntilEnded(1);

            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, granted.FindAll(r => r.itemId == "mat_cloth").Count, "훅이 넣은 보상만");
            Assert.AreEqual(7, granted.Find(r => r.itemId == "mat_cloth").amount, 1e-9);
            Assert.IsFalse(granted.Exists(r => r.itemId == "mat_ribbon"), "훅이 굴렸으면 기본 재료 굴림은 건너뛴다");

            Battle.ClearDropHook = (stage, rewards) => false;
            TickSeconds(BattleCfg.stageEndPause + 0.2f);
            TickUntilEnded(2);
            Assert.IsTrue(granted.Exists(r => r.itemId == "mat_ribbon"), "false = 지금 굴림");
        }

        [Test]
        public void ForceLampFull_OnlyWhileRunning()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Battle.ForceLampFull();
            Battle.Tick(0.1f);
            Assert.Less(Battle.Sim.LampGauge, 1f);

            Battle.ForceLampFull();
            Assert.AreEqual(1f, Battle.Sim.LampGauge);
            Assert.IsTrue(Battle.Sim.CanLightLamp);
        }
    }
}
