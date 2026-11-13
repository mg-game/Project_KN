using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 수선점 기록 (Q-04, SPEC4 §6.1): 잠김 · 개방 때 한꺼번에 받기 · 단계 순서 · 모두 받기 합계 · 숨은 기록 플래그 5개 · 시계 되돌림.
    /// 콘텐츠 합계(3,400 별조각, 단계가 줄마다 오름차순)는 EconomyBudgetTests가 실제 콘텐츠로 본다.
    /// </summary>
    public class AchievementTests : MetaSystemsTestBase
    {
        static AchievementDef Tier(string line, int tier, MetaCondition condition, int gem, bool hidden = false) => new AchievementDef
        {
            id = line + "_" + tier, lineId = line, tier = tier, title = line, condition = condition, hidden = hidden,
            rewards = new List<MetaReward> { MetaReward.Gem(gem) },
        };

        void AddContent()
        {
            MetaDb.achievements.Add(Tier("ach_kill", 1, MetaCondition.Count(CounterKey.EnemyKill, 1), 10));
            MetaDb.achievements.Add(Tier("ach_kill", 2, MetaCondition.Count(CounterKey.EnemyKill, 5), 20));
            MetaDb.achievements.Add(Tier("ach_kill", 3, MetaCondition.Count(CounterKey.EnemyKill, 10), 30));
            MetaDb.achievements.Add(Tier("ach_lamp", 1, MetaCondition.Count(CounterKey.LampLit, 2), 15));
            MetaDb.achievements.Add(Tier("ach_hidden", 1, MetaCondition.Flag(AchievementSystem.FlagCleanBoss), 50, hidden: true));
        }

        void Setup(bool locked = false)
        {
            AddContent();
            if (locked) AddFeature(FeatureId.Achievements, MetaCondition.StageCleared(2, 3));
            Rebuild();
            Achievements.Connect();
        }

        [Test]
        public void Locked_TryClaimIsNull_ProgressStillCounts_ThenBurstAtUnlock()
        {
            Setup(locked: true);
            Progress.Increment(CounterKey.EnemyKill, 7);
            Progress.Increment(CounterKey.LampLit, 2);

            Assert.IsFalse(Achievements.IsUnlocked);
            Assert.AreEqual(0, Achievements.ClaimableCount);
            Assert.IsNull(Achievements.TryClaim("ach_kill_1"));
            Assert.IsEmpty(Achievements.ClaimAll());

            SetCleared(MetaCondition.StageIndex(2, 3));
            Features.Evaluate();
            Assert.IsTrue(Achievements.IsUnlocked);
            Assert.AreEqual(3, Achievements.ClaimableCount, "설치 때부터 쌓인 진행으로 앞 단계를 한꺼번에 (kill 1·2 + lamp 1)");
        }

        [Test]
        public void TryClaim_OnlyTheLowestUnclaimedTier()
        {
            Setup();
            Progress.Increment(CounterKey.EnemyKill, 10);
            double before = Gem;

            Assert.IsNull(Achievements.TryClaim("ach_kill_2"), "낮은 단계부터");
            Assert.IsNotNull(Achievements.TryClaim("ach_kill_1"));
            Assert.IsNull(Achievements.TryClaim("ach_kill_1"), "한 번만");
            Assert.IsNotNull(Achievements.TryClaim("ach_kill_2"));
            Assert.AreEqual(30, Gem - before);
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.AchievementClaim));
            Assert.IsNull(Achievements.TryClaim("nope"));
            Assert.IsNull(Achievements.TryClaim("ach_lamp_1"), "아직 못 채움");
        }

        [Test]
        public void ClaimAll_InTierOrder_GrantsSum()
        {
            Setup();
            Progress.Increment(CounterKey.EnemyKill, 6);
            Progress.Increment(CounterKey.LampLit, 3);
            int changed = 0;
            Achievements.Changed += () => changed++;
            double before = Gem;

            var granted = Achievements.ClaimAll();

            Assert.AreEqual(45, Gem - before, "kill 1(10) + kill 2(20) + lamp 1(15), kill 3은 아직");
            Assert.AreEqual(45, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem), "합쳐서 한 번에");
            CollectionAssert.AreEquivalent(new[] { "ach_kill_1", "ach_kill_2", "ach_lamp_1" }, Data.meta.achievements.claimed);
            Assert.AreEqual(3, Progress.GetLifetime(CounterKey.AchievementClaim));
            Assert.AreEqual(1, changed);
            Assert.AreEqual(0, Achievements.ClaimableCount);
            Assert.IsEmpty(Achievements.ClaimAll());
        }

        [Test]
        public void GetLines_SortsClaimableProgressDoneHidden()
        {
            Setup();
            MetaDb.achievements.Add(Tier("ach_done", 1, MetaCondition.Always(), 5));
            Rebuild();
            Achievements.Connect();
            Assert.IsNotNull(Achievements.TryClaim("ach_done_1"));
            Progress.Increment(CounterKey.LampLit, 2);
            Progress.Increment(CounterKey.EnemyKill, 0);

            var lines = new List<AchievementLineStatus>();
            Achievements.GetLines(lines);

            Assert.AreEqual(4, lines.Count, "줄마다 하나");
            Assert.AreEqual("ach_lamp_1", lines[0].Current.id, "받을 것 먼저");
            Assert.IsTrue(lines[0].Claimable);
            Assert.AreEqual("ach_kill_1", lines[1].Current.id, "진행 중");
            Assert.AreEqual(0, lines[1].Value);
            Assert.AreEqual(1, lines[1].Target);
            Assert.AreEqual(3, lines[1].TierCount);
            Assert.AreEqual("ach_done_1", lines[2].Current.id, "완료");
            Assert.IsTrue(lines[2].AllDone);
            Assert.AreEqual("ach_hidden_1", lines[3].Current.id, "숨은 기록은 맨 뒤");
            Assert.IsTrue(lines[3].HiddenUnrevealed);
        }

        [Test]
        public void TamperedClock_BlocksClaims()
        {
            Setup();
            Progress.Increment(CounterKey.EnemyKill, 1);
            Data.meta.clock.maxSeenUnix = NowUnix + 3 * 24 * 3600;
            Assert.IsTrue(DailyClock.IsTampered);

            Assert.AreEqual(0, Achievements.ClaimableCount);
            Assert.IsNull(Achievements.TryClaim("ach_kill_1"));

            Data.meta.clock.maxSeenUnix = NowUnix;
            Assert.AreEqual(1, Achievements.ClaimableCount);
            Assert.IsNotNull(Achievements.TryClaim("ach_kill_1"));
        }

        /// <summary>숨은 기록 줄(ach_hidden)은 플래그 5개가 서로 따로 — 찬 단계를 순서와 상관없이 받는다 (P4-8 요청)</summary>
        [Test]
        public void HiddenLine_TiersAreIndependent()
        {
            MetaDb.achievements.Add(Tier("ach_hidden", 1, MetaCondition.Flag(AchievementSystem.FlagRainUdu), 50, hidden: true));
            MetaDb.achievements.Add(Tier("ach_hidden", 2, MetaCondition.Flag(AchievementSystem.FlagCleanBoss), 50, hidden: true));
            MetaDb.achievements.Add(Tier("ach_hidden", 3, MetaCondition.Flag(AchievementSystem.FlagThreeLamps), 50, hidden: true));
            Rebuild();
            Achievements.Connect();
            Progress.SetFlag(AchievementSystem.FlagThreeLamps);

            Assert.AreEqual(1, Achievements.ClaimableCount, "셋째 플래그만 찼다");
            var lines = new List<AchievementLineStatus>();
            Achievements.GetLines(lines);
            Assert.AreEqual("ach_hidden_3", lines[0].Current.id, "찬 단계를 보여 준다");
            Assert.IsTrue(lines[0].Claimable);
            Assert.IsFalse(lines[0].HiddenUnrevealed);

            Assert.IsNotNull(Achievements.TryClaim("ach_hidden_3"), "앞 단계를 받지 않아도 받는다");
            Assert.IsNull(Achievements.TryClaim("ach_hidden_1"), "못 찬 단계는 아님");
            Achievements.GetLines(lines);
            Assert.AreEqual("ach_hidden_1", lines[0].Current.id);
            Assert.IsTrue(lines[0].HiddenUnrevealed, "다음 숨은 기록은 여전히 '???'");

            Progress.SetFlag(AchievementSystem.FlagRainUdu);
            Progress.SetFlag(AchievementSystem.FlagCleanBoss);
            var granted = Achievements.ClaimAll();
            Assert.AreEqual(100, AmountOf(granted, MetaRewardType.Currency, CurrencyType.Gem));
            Achievements.GetLines(lines);
            Assert.IsTrue(lines[0].AllDone);
        }

        // ───────────── 숨은 기록 ─────────────

        [Test]
        public void Hidden_CleanBoss_ChapterBossWithoutShopDamage()
        {
            Setup();
            Data.battle.stage = 10;
            Data.battle.highestStage = 10;
            Assert.AreEqual(10, Battle.Sim.Level);

            Achievements.ResetStageRecord();
            Achievements.OnBattleEvent(new BattleEvent(BattleEventType.ShopDamaged, 1, -1, default, 3f));
            Achievements.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagCleanBoss), "한 번이라도 다치면 아님");

            Achievements.OnStageEnded(BattleOutcome.Failed, new List<RewardData>());
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagCleanBoss));
            Achievements.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
            Assert.IsTrue(Progress.HasFlag(AchievementSystem.FlagCleanBoss), "피해 0으로 챕터 보스 클리어");
            Assert.AreEqual(1, Achievements.ClaimableCount, "숨은 기록을 받을 수 있다");
        }

        [Test]
        public void Hidden_CleanBoss_NotOnNormalStage()
        {
            Setup();
            Data.battle.stage = 7;
            Assert.AreEqual(7, Battle.Sim.Level);
            Achievements.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagCleanBoss));
        }

        [Test]
        public void Hidden_ThreeLamps_InOneStage()
        {
            Setup();
            var lamp = new BattleEvent(BattleEventType.LampLit);
            Achievements.OnBattleEvent(lamp);
            Achievements.OnBattleEvent(lamp);
            Achievements.ResetStageRecord(); // 새 스테이지
            Achievements.OnBattleEvent(lamp);
            Achievements.OnBattleEvent(lamp);
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagThreeLamps), "스테이지를 넘어 세지 않는다");
            Achievements.OnBattleEvent(lamp);
            Assert.IsTrue(Progress.HasFlag(AchievementSystem.FlagThreeLamps));
        }

        [Test]
        public void Hidden_RainUdu_RainStageClearedWithUdukeoniGuardian()
        {
            UnityEngine.Object.DestroyImmediate(CharDb);
            CharDb = TestContent.CreateCharacters();
            CharDb.lineages.Add(new LineageDef
            {
                id = AchievementSystem.UdukeoniLineageId, name = "우두커니", grade = ItemGrade.Legendary, baseFormId = "udu_base",
                forms = new List<CharacterFormDef> { TestContent.Form("udu_base", null, 0, CombatStyle.Defense) },
            });
            CreateSystems(new System.Random(1234));
            Setup();
            int rain = 0;
            for (int s = 1; s <= 200 && rain == 0; s++)
                if (BattleCfg.GetStageModifier(s) == StageModifierType.Rain && s % 10 != 0) rain = s;
            Assert.Greater(rain, 0, "비 스테이지가 있다");

            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Data.battle.stage = rain;
            Data.battle.highestStage = rain;
            Battle.Restart();
            Achievements.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagRainUdu), "우두커니가 지키지 않으면 아님");

            Characters.Grant(AchievementSystem.UdukeoniLineageId);
            Formation.TryPlace(CharacterRole.Guardian, 1, "udu_base");
            Battle.Restart();
            Assert.AreEqual(rain, Battle.Sim.Level);
            Achievements.OnStageEnded(BattleOutcome.Cleared, new List<RewardData>());
            Assert.IsTrue(Progress.HasFlag(AchievementSystem.FlagRainUdu));
        }

        [Test]
        public void Hidden_AllSocks_ElevenJjakjjakForms()
        {
            var save = new LineageSave();
            for (int i = 0; i < AchievementSystem.JjakjjakFormCount - 1; i++) save.unlockedForms.Add("jjak_" + i);
            Data.lineages[AchievementSystem.JjakjjakLineageId] = save;
            Setup();
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagAllSocks), "10종");

            save.unlockedForms.Add("jjak_last");
            Rebuild();
            Achievements.Connect();
            Assert.IsTrue(Progress.HasFlag(AchievementSystem.FlagAllSocks), "11종 (Connect · 도감 Changed에서 본다)");
        }

        [Test]
        public void Hidden_Midnight_AttendanceBeforeFive()
        {
            Setup();
            SetUtc(2026, 11, 5, 14, 0);
            Progress.Increment(CounterKey.AttendanceCheckIn);
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagMidnight), "낮 도장");

            SetUtc(2026, 11, 6, 1, 30);
            Progress.Increment(CounterKey.EnemyKill);
            Assert.IsFalse(Progress.HasFlag(AchievementSystem.FlagMidnight), "다른 카운터는 아님");
            Progress.Increment(CounterKey.AttendanceCheckIn);
            Assert.IsTrue(Progress.HasFlag(AchievementSystem.FlagMidnight), "현지 01:30 도장");
        }
    }
}
