using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 진행 기록기 (F-02): 카운터 3벌 + 합계 키, 일/주 초기화, 기존 시스템 이벤트 공급(전투·강화·소환·원정·던전 추론),
    /// 조건 종류별 값/목표/충족/표기, 프레임 묶음(Flush), 다시 만들 때 중복 구독 없음.
    /// </summary>
    public class ProgressTrackerTests : MetaSystemsTestBase
    {
        void SetEnemies(float hp, float damage)
        {
            foreach (var enemy in BattleCfg.enemies)
            {
                enemy.hp = hp;
                enemy.damage = damage;
            }
        }

        // ───────────── 카운터 ─────────────

        [Test]
        public void Increment_AddsToAllScopes_AndIgnoresNonPositive()
        {
            Progress.Increment(CounterKey.EchoSummon, 3);
            Progress.Increment(CounterKey.EchoSummon);
            Progress.Increment(CounterKey.EchoSummon, 0);
            Progress.Increment(CounterKey.EchoSummon, -5);

            Assert.AreEqual(4, Progress.GetLifetime(CounterKey.EchoSummon));
            Assert.AreEqual(4, Progress.GetDaily(CounterKey.EchoSummon));
            Assert.AreEqual(4, Progress.GetWeekly(CounterKey.EchoSummon));
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.LampLit));
        }

        [Test]
        public void Increment_AlsoAddsAggregateKeys()
        {
            var seen = new List<CounterKey>();
            Progress.Incremented += (key, _) => seen.Add(key);

            Progress.Increment(CounterKey.ShopLevelUp, 2);
            Progress.Increment(CounterKey.LineageLevelUp);
            Progress.Increment(CounterKey.ResonanceUp);
            Progress.Increment(CounterKey.EvolutionCraft);
            Progress.Increment(CounterKey.CostumeUnlock);
            Progress.Increment(CounterKey.DungeonChallenge);
            Progress.Increment(CounterKey.DungeonSweep, 2);

            Assert.AreEqual(3, Progress.GetDaily(CounterKey.AnyUpgrade), "수선점 강화 + 친구 레벨업");
            Assert.AreEqual(3, Progress.GetDaily(CounterKey.AnyGrowthMilestone), "진화 + 공명 + 의상");
            Assert.AreEqual(3, Progress.GetWeekly(CounterKey.DungeonRun), "도전 + 소탕");
            CollectionAssert.AreEqual(new[] { CounterKey.ShopLevelUp, CounterKey.AnyUpgrade }, seen.GetRange(0, 2));
        }

        [Test]
        public void DailyCounters_ResetAtFiveAm_EvenWithoutRefresh()
        {
            SetUtc(2026, 11, 3, 4, 50);
            Progress.Increment(CounterKey.EnemyKill, 10);

            SetUtc(2026, 11, 3, 5, 0);
            Assert.AreEqual(0, Progress.GetDaily(CounterKey.EnemyKill), "읽을 때 키가 다르면 0");
            Assert.AreEqual(10, Progress.GetWeekly(CounterKey.EnemyKill), "같은 주");
            Assert.AreEqual(10, Progress.GetLifetime(CounterKey.EnemyKill));

            Progress.Increment(CounterKey.EnemyKill, 2);
            Assert.AreEqual(2, Progress.GetDaily(CounterKey.EnemyKill), "새 날은 0부터");
            Assert.AreEqual(12, Progress.GetWeekly(CounterKey.EnemyKill));
            Assert.AreEqual("2026-11-03", Data.meta.counters.dayKey);
        }

        [Test]
        public void DayAndWeekChanged_ClearPeriodCounters()
        {
            SetUtc(2026, 11, 8, 12, 0); // 일요일
            DailyClock.Refresh();
            Progress.Increment(CounterKey.LampLit, 7);
            Progress.Flush();
            int changed = 0;
            Progress.Changed += () => changed++;

            SetUtc(2026, 11, 9, 5, 30); // 월요일 05:30
            DailyClock.Refresh();

            Assert.AreEqual(0, Data.meta.counters.daily.Count, "날이 바뀌면 오늘 값을 비운다");
            Assert.AreEqual(0, Data.meta.counters.weekly.Count, "월요일 05:00에 이번 주 값도 비운다");
            Assert.AreEqual(7, Progress.GetLifetime(CounterKey.LampLit));
            Progress.Flush();
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void Flush_RaisesChangedOncePerBatch()
        {
            int changed = 0;
            Progress.Changed += () => changed++;

            Progress.Flush();
            Assert.AreEqual(0, changed, "깨끗하면 울리지 않는다");

            for (int i = 0; i < 50; i++) Progress.Increment(CounterKey.EnemyKill);
            Assert.IsTrue(Progress.IsDirty);
            Progress.Flush();
            Progress.Flush();
            Assert.AreEqual(1, changed, "처치 50번이어도 한 번");

            Progress.MarkDirty();
            Progress.Flush();
            Assert.AreEqual(2, changed);
        }

        [Test]
        public void Flags_SetOnce()
        {
            Assert.IsFalse(Progress.HasFlag(MetaFlags.SynergyDetailViewed));
            Assert.IsTrue(Progress.SetFlag(MetaFlags.SynergyDetailViewed));
            Assert.IsFalse(Progress.SetFlag(MetaFlags.SynergyDetailViewed), "두 번째는 false");
            Assert.IsFalse(Progress.SetFlag(null));
            Assert.IsTrue(Progress.HasFlag(MetaFlags.SynergyDetailViewed));
            Assert.IsTrue(Data.flags.Contains(MetaFlags.SynergyDetailViewed));
        }

        [Test]
        public void RecreatingMetaSystems_DoesNotDoubleCount()
        {
            Rebuild();
            Rebuild();
            Currency.Add(CurrencyType.Gold, 1_000_000);

            Growth.TryLevelUp(2);

            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.ShopLevelUp), "앞의 진행 기록기는 구독을 풀었다");
        }

        // ───────────── 기존 시스템 이벤트 ─────────────

        [Test]
        public void Battle_CountsKillsAndStageResults()
        {
            SetEnemies(1f, 0f);
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");
            var outcomes = new List<BattleOutcome>();
            Battle.StageEnded += (outcome, _) => outcomes.Add(outcome);

            for (float t = 0f; t < 300f && outcomes.Count == 0; t += 0.1f) Battle.Tick(0.1f);

            Assert.AreEqual(BattleOutcome.Cleared, outcomes[0]);
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.StageClear));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.BossKill));
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.StageFail));
            Assert.Greater(Progress.GetLifetime(CounterKey.EnemyKill), 0);
            Assert.AreEqual(Data.battle.totalKills, Progress.GetLifetime(CounterKey.EnemyKill), "처치 수 = 기존 누적 처치 수");
        }

        [Test]
        public void ShopLevelUp_CountsLevelsGained()
        {
            Currency.Add(CurrencyType.Gold, 1_000_000);
            Growth.TryLevelUp(5);
            Growth.TryLevelUp();

            Assert.AreEqual(6, Progress.GetDaily(CounterKey.ShopLevelUp));
            Assert.AreEqual(6, Progress.GetDaily(CounterKey.AnyUpgrade));
        }

        [Test]
        public void CharacterLevelUpAndResonance_AreCounted()
        {
            Characters.Grant("sock");
            Currency.Add(CurrencyType.LineageXp, 1_000_000);
            Assert.IsTrue(Characters.TryLevelUp("sock"));
            Assert.IsTrue(Characters.TryLevelUp("sock"));
            Characters.AddShards("sock", 1000);
            Assert.IsTrue(Characters.TryResonance("sock"));

            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.LineageLevelUp));
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.AnyUpgrade));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ResonanceUp));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.AnyGrowthMilestone));
        }

        [Test]
        public void GachaPulls_CountResults_IncludingGuaranteed()
        {
            Currency.Add(CurrencyType.Gem, 10_000);
            Assert.IsTrue(Gacha.TryPull(Table, 10, out _));
            Assert.IsTrue(Gacha.TryPull(Table, 1, out _));
            Assert.IsNotNull(Gacha.PullGuaranteed(Table, "candle"));

            Assert.AreEqual(12, Progress.GetLifetime(CounterKey.GachaPull));
        }

        [Test]
        public void CostumeUnlockAndEquip_AreCounted()
        {
            Characters.Grant("sock");
            Costumes.Unlock("sock_event");
            Assert.IsTrue(Costumes.TryEquip("sock", "sock_event"));

            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.CostumeUnlock));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.CostumeEquip));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.AnyGrowthMilestone));
        }

        [Test]
        public void Expedition_StartAndClaim_AreInferredFromChanges()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ExpeditionStart));
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.ExpeditionClaim));

            Clock.Advance(TimeSpan.FromMinutes(11));
            Assert.IsNotNull(Expedition.Claim("exp_1_1"));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ExpeditionClaim));

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "candle" }));
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.ExpeditionStart));
            Assert.IsTrue(Expedition.TryCancel("exp_1_1"));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ExpeditionStart), "취소한 원정은 보낸 것으로 치지 않는다");
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.ExpeditionStart));
            Assert.AreEqual(1, Progress.GetWeekly(CounterKey.ExpeditionStart));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ExpeditionClaim), "취소는 받기가 아니다");
        }

        [Test]
        public void Expedition_SendAndCancelSpam_DoesNotFillMissions()
        {
            Characters.Grant("sock");

            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }));
                Assert.IsTrue(Expedition.TryCancel("exp_1_1"));
            }

            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.ExpeditionStart), "보내고 바로 취소하기를 되풀이해도 '원정 N번'이 차지 않는다");
            Assert.AreEqual(0, Progress.GetDaily(CounterKey.ExpeditionStart));
            Assert.AreEqual(0, Progress.GetWeekly(CounterKey.ExpeditionStart));
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.ExpeditionClaim));
        }

        [Test]
        public void Expedition_CancelNextDay_OnlyUndoesThePeriodItWasCountedIn()
        {
            Data.battle.highestStage = 3; // exp_1_2 열림
            Characters.Grant("sock");
            Characters.Grant("candle");
            SetUtc(2026, 11, 4, 4, 55); // 수요일 04:55 = 화요일(11-03) 게임 날
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" })); // 05:05에 끝남
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.ExpeditionStart));

            SetUtc(2026, 11, 4, 5, 1); // 수요일 게임 날, 같은 주
            Assert.IsTrue(Expedition.TryStart("exp_1_2", new[] { "candle" }));
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.ExpeditionStart));
            Assert.AreEqual(2, Progress.GetWeekly(CounterKey.ExpeditionStart));

            Assert.IsTrue(Expedition.TryCancel("exp_1_1"));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ExpeditionStart));
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.ExpeditionStart), "어제 보낸 원정을 오늘 취소해도 오늘 값은 그대로");
            Assert.AreEqual(1, Progress.GetWeekly(CounterKey.ExpeditionStart), "같은 주에 센 것이라 이번 주 값에서는 뺀다");

            Assert.IsTrue(Expedition.TryCancel("exp_1_2"));
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.ExpeditionStart));
            Assert.AreEqual(0, Progress.GetDaily(CounterKey.ExpeditionStart));
            Assert.AreEqual(0, Progress.GetWeekly(CounterKey.ExpeditionStart));
        }

        [Test]
        public void Expedition_CancelAfterReload_UndoesStartFromStartTime()
        {
            Characters.Grant("sock");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }));
            Rebuild(); // 앱을 다시 켬: 이미 있던 원정은 보낸 시각으로 센 날을 안다

            Assert.IsTrue(Expedition.TryCancel("exp_1_1"));

            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.ExpeditionStart), "다시 켜고 취소해도 되돌린다");
            Assert.AreEqual(0, Progress.GetDaily(CounterKey.ExpeditionStart));
            Assert.AreEqual(0, Progress.GetWeekly(CounterKey.ExpeditionStart));
        }

        [Test]
        public void Expedition_ClaimAfterClockShift_IsStillAClaim()
        {
            Characters.Grant("sock");
            SetUtc(2026, 11, 4, 9, 0);
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" })); // 09:10에 끝남

            // 시계가 시작보다 2분 뒤로 감 → ExpeditionSystem이 시작·끝을 2분 당긴다 (09:08에 끝남)
            SetUtc(2026, 11, 4, 8, 58);
            Expedition.GetRemaining(Expedition.GetActive("exp_1_1"));
            SetUtc(2026, 11, 4, 9, 9);
            Assert.IsNotNull(Expedition.Claim("exp_1_1"));

            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ExpeditionClaim), "당겨진 끝 시각으로 받기 판정");
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ExpeditionStart), "받은 원정은 되돌리지 않는다");
        }

        [Test]
        public void Dungeon_SweepChallengeAndFailure_AreInferred()
        {
            Characters.Grant("button");
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Formation.TryPlace(CharacterRole.Echo, 1, "sock_a");

            SetEnemies(1f, 1f);
            var cleared = Dungeon.Challenge();
            Assert.IsTrue(cleared.Cleared);
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.DungeonChallenge));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.DungeonClear));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.DungeonRun));

            Assert.IsNotNull(Dungeon.Sweep(1));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.DungeonSweep));
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.DungeonRun));

            SetEnemies(1e6f, 1e6f);
            var failed = Dungeon.Challenge();
            Assert.IsFalse(failed.Cleared);
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.DungeonChallenge), "실패한 도전도 도전");
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.DungeonClear));
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.DungeonRun), "공짜 실패는 '던전 N번' 미션에 넣지 않는다");
        }

        [Test]
        public void Dungeon_SweepMany_CountsEveryTicket()
        {
            Data.dungeon.highestFloor = 5;
            Rebuild(); // 진행 기록기가 5층을 기준으로 다시 찍는다 (층이 오른 것으로 세지 않게)

            Assert.IsNotNull(Dungeon.SweepMany(5, 2));
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.DungeonSweep), "SPEC4 §5.2 — n장 = n번 (그날 첫 소탕)");
            Assert.IsNotNull(Dungeon.SweepMany(5, 3), "남은 1장으로 잘림");
            Assert.AreEqual(3, Progress.GetLifetime(CounterKey.DungeonSweep), "같은 날 이어서 쓴 장만 센다");
            Assert.AreEqual(3, Progress.GetLifetime(CounterKey.DungeonRun));
            Assert.AreEqual(3, Progress.GetDaily(CounterKey.DungeonRun), "일일 · 주간 '던전 N번' 미션도 n");
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.DungeonChallenge), "소탕은 도전이 아니다");
        }

        [Test]
        public void Dungeon_BlessingPickAndRespec_AreNotChallenges()
        {
            Data.dungeon.highestFloor = 3;
            Rebuild(); // 진행 기록기가 3층을 기준으로 다시 찍는다
            Dungeon.BlessingPool = new List<BlessingDef>
            {
                new BlessingDef { id = "b_attack", name = "b_attack", desc = "b_attack", stat = StatType.AttackPercent, value = 15 },
                new BlessingDef { id = "b_shield", name = "b_shield", desc = "b_shield", stat = StatType.ShieldPowerPercent, value = 20 },
                new BlessingDef { id = "b_crit", name = "b_crit", desc = "b_crit", stat = StatType.CritChancePercent, value = 8 },
            };

            var offer = Dungeon.GetBlessingOffer();
            Assert.IsTrue(Dungeon.TryPickBlessing(offer[0]));
            Assert.IsTrue(Dungeon.Respec());
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.DungeonChallenge), "축복 고르기 · 다시 고르기는 실패한 도전이 아니다");
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.DungeonRun));

            SetEnemies(1e6f, 1e6f);
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            var failed = Dungeon.Challenge();
            Assert.IsNotNull(failed);
            Assert.IsFalse(failed.Cleared);
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.DungeonChallenge), "그 뒤 실패한 도전은 그대로 센다");
        }

        [Test]
        public void ShopPurchase_AndMailClaim_AreCounted()
        {
            Mail.Send("a", "a", new[] { RewardData.Currency(CurrencyType.Gem, 1) });
            Mail.Send("b", "b", new[] { RewardData.Currency(CurrencyType.Gem, 1) });
            Mail.ClaimAll();
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.MailClaim));

            var product = FindBuyableProduct();
            if (product.shop == null) Assert.Inconclusive("살 수 있는 상품이 없는 상점 데이터");
            Currency.Add(CurrencyType.Gold, 1e12);
            Currency.Add(CurrencyType.Gem, 1e9);
            Currency.Add(CurrencyType.EventToken, 1e9);
            Assert.AreEqual(ShopBuyResult.Ok, Shop.TryBuy(product.shop, product.id));
            Assert.AreEqual(1, Progress.GetLifetime(CounterKey.ShopBuy));
        }

        (string shop, string id) FindBuyableProduct()
        {
            foreach (var shop in ShopDb.shops)
            {
                foreach (var p in shop.products)
                {
                    if (p != null && string.IsNullOrEmpty(p.costumeId) && p.rewards != null && p.rewards.Count > 0) return (shop.id, p.id);
                }
            }
            return (null, null);
        }

        // ───────────── 조건 ─────────────

        [Test]
        public void StageCleared_UsesHighestReachedMinusOne()
        {
            var condition = MetaCondition.StageCleared(1, 4);
            Assert.AreEqual(4, condition.target);

            SetCleared(3);
            Assert.AreEqual(3, Progress.GetValue(condition));
            Assert.IsFalse(Progress.IsMet(condition));
            Assert.AreEqual("1-3/1-4", Progress.FormatProgress(condition));
            Assert.AreEqual(0.75f, Progress.GetRatio(condition), 1e-4f);

            SetCleared(4);
            Assert.IsTrue(Progress.IsMet(condition));
            Assert.AreEqual("1-4/1-4", Progress.FormatProgress(condition));

            Data.battle.highestStage = 1;
            Assert.AreEqual("-/1-4", Progress.FormatProgress(condition), "아무것도 클리어하지 않음");
            Assert.AreEqual("스테이지 1-4 클리어", Progress.Describe(condition));
            Assert.AreEqual("스테이지 2-1 클리어", Progress.Describe(MetaCondition.StageCleared(2, 1)));
        }

        [Test]
        public void CounterConditions_UseScopes_AndClampProgressText()
        {
            Progress.Increment(CounterKey.EchoSummon, 40);
            var lifetime = MetaCondition.Count(CounterKey.EchoSummon, 30);
            var daily = MetaCondition.Count(CounterKey.EchoSummon, 50, CounterScope.Daily);
            var weekly = MetaCondition.Count(CounterKey.EchoSummon, 30, CounterScope.Weekly);

            Assert.IsTrue(Progress.IsMet(lifetime));
            Assert.AreEqual("30/30", Progress.FormatProgress(lifetime), "충족하면 목표 기준");
            Assert.IsFalse(Progress.IsMet(daily));
            Assert.AreEqual("40/50", Progress.FormatProgress(daily));
            Assert.IsTrue(Progress.IsMet(weekly));
            Assert.AreEqual("잔향이 30번 부르기", Progress.Describe(lifetime));
            Assert.AreEqual("오늘 잔향이 50번 부르기", Progress.Describe(daily));
            Assert.AreEqual("적 1,000 처치", Progress.Describe(MetaCondition.Count(CounterKey.EnemyKill, 1000)));
        }

        [Test]
        public void LineageConditions_ReadSaves()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Data.lineages["sock"].level = 7;
            Data.lineages["candle"].level = 3;
            Characters.UnlockForm("sock_a");

            Assert.AreEqual(7, Progress.GetValue(MetaCondition.Of(MetaConditionType.LineageLevel, 5, "sock")));
            Assert.IsTrue(Progress.IsMet(MetaCondition.Of(MetaConditionType.LineageLevel, 5, "sock")));
            Assert.IsFalse(Progress.IsMet(MetaCondition.Of(MetaConditionType.LineageLevel, 5, "candle")));
            Assert.AreEqual(0, Progress.GetValue(MetaCondition.Of(MetaConditionType.LineageLevel, 5, "button")), "미보유 0");
            Assert.AreEqual(7, Progress.GetValue(MetaCondition.Of(MetaConditionType.LineageLevel, 5)), "비면 가장 높은 계보");
            Assert.AreEqual(10, Progress.GetValue(MetaCondition.Of(MetaConditionType.LineageLevelSum, 40)));
            Assert.AreEqual(2, Progress.GetValue(MetaCondition.Of(MetaConditionType.OwnedLineages, 3)));
            Assert.AreEqual(3, Progress.GetValue(MetaCondition.Of(MetaConditionType.UnlockedForms, 5)), "기본형 2 + 진화형 1");
            Assert.AreEqual("양말 레벨 5", Progress.Describe(MetaCondition.Of(MetaConditionType.LineageLevel, 5, "sock")));
        }

        [Test]
        public void FormationConditions_CountPlacedUnits()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 1, "candle_base");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");

            Assert.AreEqual(2, Progress.GetValue(MetaCondition.Of(MetaConditionType.GuardiansPlaced, 4)));
            Assert.AreEqual(1, Progress.GetValue(MetaCondition.Of(MetaConditionType.EchoesPlaced, 1)));
            Assert.IsTrue(Progress.IsMet(MetaCondition.Of(MetaConditionType.EchoesPlaced, 1)));
        }

        [Test]
        public void SynergyConditions_FollowActiveSynergies()
        {
            SynergyDb.synergies = new List<SynergyDef>
            {
                new SynergyDef
                {
                    id = "sock_candle", name = "양말과 촛불", kind = SynergyKind.Combo,
                    lineageIds = new List<string> { "sock", "candle" },
                    tiers = new List<SynergyTier> { new SynergyTier { count = 2, membersOnly = true } },
                },
            };
            Characters.Grant("sock");
            Characters.Grant("candle");
            var active = MetaCondition.Of(MetaConditionType.ActiveSynergies, 1);
            var tier = MetaCondition.Of(MetaConditionType.SynergyTier, 1, "sock_candle");
            Assert.AreEqual(0, Progress.GetValue(active));

            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 1, "candle_base");

            Assert.AreEqual(1, Progress.GetValue(active), "Synergy.Changed 뒤 다시 계산");
            Assert.AreEqual(1, Progress.GetValue(tier));
            Assert.AreEqual(1, Progress.GetValue(MetaCondition.Of(MetaConditionType.SynergyTier, 2)), "비면 가장 높은 단계");
            Assert.AreEqual("양말과 촛불 1단계 달성", Progress.Describe(tier));
        }

        [Test]
        public void MiscConditions_FlagFtueInstallFeatureJournalDungeonShop()
        {
            Assert.IsTrue(Progress.IsMet(null));
            Assert.IsTrue(Progress.IsMet(MetaCondition.Always()));

            var flag = MetaCondition.Flag(MetaFlags.EvolutionTreeViewed);
            Assert.IsFalse(Progress.IsMet(flag));
            Progress.SetFlag(MetaFlags.EvolutionTreeViewed);
            Assert.IsTrue(Progress.IsMet(flag));
            Assert.AreEqual("진화 기억도 열어 보기", Progress.Describe(flag));

            var step = MetaCondition.Of(MetaConditionType.FtueStep, 6);
            var done = MetaCondition.Of(MetaConditionType.FtueDone, 1);
            Data.meta.ftue.step = 5;
            Assert.IsFalse(Progress.IsMet(step));
            Data.meta.ftue.step = 6;
            Assert.IsTrue(Progress.IsMet(step));
            Assert.IsFalse(Progress.IsMet(done));
            Data.meta.ftue.step = 0;
            Data.meta.ftue.done = true;
            Assert.IsTrue(Progress.IsMet(step), "졸업하면 단계 조건은 모두 충족");
            Assert.IsTrue(Progress.IsMet(done));

            SetUtc(2026, 11, 3, 6, 0);
            DailyClock.Refresh();
            var install = MetaCondition.Of(MetaConditionType.InstallDays, 3);
            Assert.IsFalse(Progress.IsMet(install));
            SetUtc(2026, 11, 5, 5, 0);
            Assert.IsTrue(Progress.IsMet(install), "설치 3일째");

            var feature = MetaCondition.Of(MetaConditionType.FeatureUnlocked, 1, nameof(FeatureId.Gacha));
            Assert.IsTrue(Progress.IsMet(feature), "정의 없는 기능은 열림");
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            Rebuild();
            Assert.IsFalse(Progress.IsMet(feature));
            Assert.IsFalse(Progress.IsMet(MetaCondition.Of(MetaConditionType.FeatureUnlocked, 1, "NoSuchFeature")));
            Assert.AreEqual("잃어버린 친구 찾기 열기", Progress.Describe(feature));

            Data.meta.missions.daily.periodKey = DailyClock.DayKey;
            Data.meta.missions.daily.points = 30;
            Assert.AreEqual(30, Progress.GetValue(MetaCondition.Of(MetaConditionType.JournalPoints, 20)));
            Data.meta.missions.daily.periodKey = "2000-01-01";
            Assert.AreEqual(0, Progress.GetValue(MetaCondition.Of(MetaConditionType.JournalPoints, 20)), "지난 날 점수는 0");

            Data.dungeon.highestFloor = 2;
            Assert.IsTrue(Progress.IsMet(MetaCondition.Of(MetaConditionType.DungeonFloor, 1)));
            Data.level = 15;
            Assert.IsTrue(Progress.IsMet(MetaCondition.Of(MetaConditionType.ShopLevel, 15)));
            Assert.AreEqual("수선점 Lv.15", Progress.Describe(MetaCondition.Of(MetaConditionType.ShopLevel, 15)));
        }

        [Test]
        public void PowerCondition_ReadsTeamPower()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            long power = Power.TeamPower;

            Assert.AreEqual(power, Progress.GetValue(MetaCondition.Of(MetaConditionType.Power, 1)));
            Assert.IsTrue(Progress.IsMet(MetaCondition.Of(MetaConditionType.Power, power)));
            Assert.IsFalse(Progress.IsMet(MetaCondition.Of(MetaConditionType.Power, power + 1)));
        }

        [Test]
        public void OtherSystemChanges_MarkDirty()
        {
            Progress.Flush();
            Characters.Grant("sock");
            Assert.IsTrue(Progress.IsDirty, "친구 변화");
            Progress.Flush();

            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Assert.IsTrue(Progress.IsDirty, "편성 변화");
        }
    }
}
