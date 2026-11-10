using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 도감 (Q-05, SPEC4 §6.2): 찾기 트리거 · 채우기(backfill) · 종류별 보상 · 개방 뒤에만 받기 · 영구 모음 보너스 · 본 스킬 · 처치 길 할당 없음.
    /// 픽스처: 형태 7 (sock 4 · candle 2 · button 1), 의상 3, 적 9 (BattleConfig 기본), 재료 2 (m_cloth · m_thread).
    /// </summary>
    public class CodexTests : MetaSystemsTestBase
    {
        static SynergyDef Combo(string id, int tierCount)
        {
            var def = new SynergyDef { id = id, name = id, kind = SynergyKind.Combo, lineageIds = new List<string> { "sock", "candle" } };
            for (int i = 0; i < tierCount; i++)
                def.tiers.Add(new SynergyTier { count = 2, modifiers = new List<StatModifier> { new StatModifier { stat = StatType.AttackPercent, value = 1 } } });
            return def;
        }

        void Connect()
        {
            Rebuild();
            Codex.Connect();
        }

        bool Found(CodexCategory category, string id) => Data.meta.codex.found.Contains(CodexSystem.Key(category, id));

        [Test]
        public void EachTrigger_RecordsFound()
        {
            SynergyDb.synergies = new List<SynergyDef> { Combo("sock_candle", 2) };
            Connect();
            int changed = 0;
            Codex.Changed += () => changed++;

            Characters.Grant("sock");
            Assert.IsTrue(Found(CodexCategory.Friend, "sock_base"), "친구: Granted 기본형");

            Data.lineages["sock"].level = 5;
            Data.lineages["sock"].shards = 1000;
            Inventory.Add("m_cloth", 3);
            Assert.IsTrue(Found(CodexCategory.Material, "m_cloth"), "재료: ItemAdded");
            Inventory.Add("c1");
            Assert.IsFalse(Found(CodexCategory.Material, "c1"), "재료가 아닌 아이템은 칸이 없다");
            Evolution.TryAccelerate("sock_a", 0);
            Assert.IsTrue(Evolution.TryCraft("sock_a"));
            Assert.IsTrue(Found(CodexCategory.Friend, "sock_a"), "친구: Evolution.FormUnlocked");

            Costumes.Unlock("sock_event");
            Assert.IsTrue(Found(CodexCategory.Costume, "sock_event"), "의상: CostumeUnlocked");

            Codex.NoteEnemy("enemy_moth");
            Assert.IsTrue(Found(CodexCategory.Enemy, "enemy_moth"), "적: 첫 처치");

            Codex.NoteSynergy("sock_candle", 0);
            Assert.IsTrue(Found(CodexCategory.Synergy, "sock_candle"), "시너지: 처음 켜짐");
            Assert.IsFalse(Data.meta.codex.found.Contains(CodexSystem.SynergyMaxKey("sock_candle")), "첫 단계는 최고 단계가 아니다");
            Codex.NoteSynergy("sock_candle", 1);
            Assert.IsTrue(Data.meta.codex.found.Contains(CodexSystem.SynergyMaxKey("sock_candle")), "마지막 단계 = synergymax");

            Characters.UnlockForm("sock_b"); // 진화 제작이 아닌 길 → 다음 Tick에 훑기
            Assert.IsFalse(Found(CodexCategory.Friend, "sock_b"));
            Codex.Tick(0.1f);
            Assert.IsTrue(Found(CodexCategory.Friend, "sock_b"), "다른 길로 해금된 형태도 훑어서 찾는다");
            Assert.Greater(changed, 0);

            Assert.AreEqual(3, Codex.FoundCount(CodexCategory.Friend));
            Assert.AreEqual(7, Codex.TotalCount(CodexCategory.Friend));
            Assert.AreEqual(9, Codex.TotalCount(CodexCategory.Enemy));
            Assert.AreEqual(2, Codex.TotalCount(CodexCategory.Material));
            Assert.AreEqual(1, Codex.TotalCount(CodexCategory.Synergy), "synergymax는 칸이 아니다");
        }

        [Test]
        public void StoryChapter_FoundWhenSeen_WithoutReward()
        {
            MetaDb.storyChapters.Add(new StoryChapterDef { chapter = 1, title = "첫 밤" });
            Connect();
            Assert.AreEqual(1, Codex.TotalCount(CodexCategory.Story));
            Assert.AreEqual(0, Codex.FoundCount(CodexCategory.Story));

            Data.meta.story.seenChapters.Add(1);
            Codex.Tick(1.1f);

            Assert.AreEqual(1, Codex.FoundCount(CodexCategory.Story), "동화책을 펼치면 이야기 칸");
            Assert.AreEqual(0, Codex.ClaimableCount(CodexCategory.Story), "이야기 칸은 보상이 없다 (동화책이 준다)");
        }

        [Test]
        public void Backfill_MarksOwned_IsIdempotent_AndDoesNotClaim()
        {
            SynergyDb.synergies = new List<SynergyDef> { Combo("sock_candle", 1) };
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.UnlockForm("sock_a");
            Inventory.Add("m_thread", 1);
            Costumes.Unlock("sock_event");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 1, "candle_base");
            Data.battle.highestStage = 4; // minStage < 4: dust 1, moth 2, tangle 3, boss_shadow 1, boss_mothqueen 3
            Data.meta.codex.backfillPending = true;
            Data.meta.codex.found.Add(CodexSystem.Key(CodexCategory.Enemy, "enemy_windup")); // 이미 있던 기록은 그대로
            double gems = Gem;

            Connect();

            Assert.IsFalse(Data.meta.codex.backfillPending, "채운 뒤 끈다");
            Assert.IsTrue(Found(CodexCategory.Friend, "sock_base"));
            Assert.IsTrue(Found(CodexCategory.Friend, "sock_a"));
            Assert.IsTrue(Found(CodexCategory.Friend, "candle_base"));
            Assert.IsTrue(Found(CodexCategory.Material, "m_thread"));
            Assert.IsTrue(Found(CodexCategory.Costume, "sock_event"));
            Assert.IsTrue(Found(CodexCategory.Synergy, "sock_candle"), "지금 켜진 시너지");
            Assert.IsTrue(Data.meta.codex.found.Contains(CodexSystem.SynergyMaxKey("sock_candle")), "한 단계뿐이면 이미 최고 단계");
            Assert.AreEqual(6, Codex.FoundCount(CodexCategory.Enemy), "minStage < 최고 스테이지 5종 + 이미 있던 1종");
            Assert.IsFalse(Found(CodexCategory.Enemy, "enemy_rust"), "minStage 4 = 최고 스테이지면 아직");
            Assert.AreEqual(0, Data.meta.codex.claimed.Count, "받기는 자동이 아니다");
            Assert.AreEqual(gems, Gem);

            int before = Data.meta.codex.found.Count;
            Codex.Backfill();
            Codex.Backfill();
            Assert.AreEqual(before, Data.meta.codex.found.Count, "여러 번 불러도 같다");
        }

        [Test]
        public void Rewards_ByCategoryAndTier()
        {
            SynergyDb.synergies = new List<SynergyDef> { Combo("sock_candle", 2) };
            Connect();
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Characters.UnlockForm("sock_a2");
            Codex.Tick(0.1f);
            Costumes.Unlock("sock_event");
            Codex.NoteEnemy("enemy_dust");
            Inventory.Add("m_cloth");
            Codex.NoteSynergy("sock_candle", 1);

            Assert.AreEqual(20, GemOf(CodexCategory.Friend, "sock_base"), "기본형 20");
            Assert.AreEqual(30, GemOf(CodexCategory.Friend, "sock_a"), "1차 30");
            Assert.AreEqual(50, GemOf(CodexCategory.Friend, "sock_a2"), "2차 50");
            Assert.AreEqual(30, GemOf(CodexCategory.Costume, "sock_event"));
            Assert.AreEqual(10, GemOf(CodexCategory.Enemy, "enemy_dust"));
            Assert.AreEqual(5, GemOf(CodexCategory.Material, "m_cloth"));
            Assert.AreEqual(20, GemOf(CodexCategory.Synergy, "sock_candle"));

            double before = Gem;
            var granted = Codex.TryClaim(SynergyMax("sock_candle"));
            Assert.IsNotNull(granted);
            Assert.AreEqual(30, Gem - before, "시너지 최고 단계 30");

            Assert.AreEqual(7, Codex.ClaimableCount(), "남은 받을 칸 (형태 3 · 의상 · 적 · 재료 · 시너지)");
            before = Gem;
            var all = Codex.ClaimAll(CodexCategory.Friend);
            Assert.AreEqual(100, Gem - before, "친구 탭만 모두 받기 20 + 30 + 50");
            Assert.AreEqual(100, AmountOf(all, MetaRewardType.Currency, CurrencyType.Gem), "합쳐서 돌려준다");
            Assert.AreEqual(0, Codex.ClaimableCount(CodexCategory.Friend));
            Assert.AreEqual(4, Codex.ClaimableCount());
            Assert.AreEqual(4, Progress.GetLifetime(CounterKey.CodexClaim), "받은 칸마다 1");
            Assert.IsEmpty(Codex.ClaimAll(CodexCategory.Friend), "받을 것이 없으면 빈 목록");
        }

        static string SynergyMax(string synergyId) => CodexSystem.SynergyMaxKey(synergyId);

        int GemOf(CodexCategory category, string id)
        {
            var entries = new List<CodexEntryStatus>();
            Codex.GetEntries(category, entries);
            var entry = entries.Find(e => e.Id == id);
            Assert.IsNotNull(entry, id);
            Assert.IsTrue(entry.Found, id);
            Assert.IsTrue(entry.Claimable, id);
            return (int)entry.Reward[0].amount;
        }

        [Test]
        public void Claim_OnlyAfterUnlock()
        {
            AddFeature(FeatureId.Codex, MetaCondition.StageCleared(1, 10));
            Connect();
            Characters.Grant("sock");
            string key = CodexSystem.Key(CodexCategory.Friend, "sock_base");

            Assert.IsFalse(Codex.IsUnlocked);
            Assert.IsTrue(Found(CodexCategory.Friend, "sock_base"), "찾기는 잠겨 있어도 쌓인다");
            Assert.AreEqual(0, Codex.ClaimableCount());
            Assert.IsNull(Codex.TryClaim(key));
            Assert.IsEmpty(Codex.ClaimAll());

            SetCleared(10);
            Features.Evaluate();
            Assert.IsTrue(Codex.IsUnlocked);
            Assert.AreEqual(1, Codex.ClaimableCount());
            double before = Gem;
            Assert.IsNotNull(Codex.TryClaim(key));
            Assert.AreEqual(20, Gem - before);
            Assert.IsNull(Codex.TryClaim(key), "한 번만");
            Assert.IsNull(Codex.TryClaim(CodexSystem.Key(CodexCategory.Friend, "candle_base")), "못 찾은 칸");
            Assert.IsNull(Codex.TryClaim("nope"));
        }

        [Test]
        public void CollectionBonus_ExactTiers_AndAbsentBeforeUnlock()
        {
            // 형태 20종: 픽스처 7 + bulk 13
            var bulk = new LineageDef { id = "bulk", name = "bulk", grade = ItemGrade.Common, baseFormId = "bulk_0" };
            for (int i = 0; i < 13; i++) bulk.forms.Add(TestContent.Form("bulk_" + i, i == 0 ? null : "bulk_0", i == 0 ? 0 : 1, CombatStyle.Assault));
            UnityEngine.Object.DestroyImmediate(CharDb);
            CharDb = TestContent.CreateCharacters();
            CharDb.lineages.Add(bulk);
            CreateSystems(new Random(1234));
            MetaDb.codex.formTiers = new List<CodexBonusTier>
            {
                new CodexBonusTier { count = 5, percent = 1 }, new CodexBonusTier { count = 10, percent = 3 }, new CodexBonusTier { count = 15, percent = 5 },
                new CodexBonusTier { count = 20, percent = 7 }, new CodexBonusTier { count = 25, percent = 8 }, new CodexBonusTier { count = 29, percent = 10 },
            };
            MetaDb.codex.synergyTiers = new List<CodexBonusTier> { new CodexBonusTier { count = 1, percent = 5 } };
            MetaDb.codex.allEnemiesShopHpPercent = 5;
            AddFeature(FeatureId.Codex, MetaCondition.StageCleared(1, 10));
            Connect();

            foreach (var lineage in CharDb.lineages)
            {
                Characters.Grant(lineage.id);
                foreach (var form in lineage.forms) Characters.UnlockForm(form.id);
            }
            Codex.Tick(0.1f);
            Assert.AreEqual(20, Codex.FoundCount(CodexCategory.Friend));

            var team = new StatBlock();
            Codex.AddCollectionBonus(team);
            Assert.IsTrue(team.IsZero, "도감이 열리기 전에는 보너스 없음");
            Assert.AreEqual(0f, Synergy.BuildModifiers().Team[StatType.AttackPercent], 1e-4f);

            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            long powerBefore = Power.TeamPower;
            SetCleared(10);
            Features.Evaluate();

            var mods = Synergy.BuildModifiers();
            Assert.AreEqual(7f, mods.Team[StatType.AttackPercent], 1e-4f, "20종 = +7% (더하지 않은 그 단계 값)");
            Assert.AreEqual(7f, mods.Team[StatType.HpPercent], 1e-4f);
            Assert.AreEqual(0f, mods.Team[StatType.ShopMaxHpPercent], 1e-4f, "적을 다 찾기 전");
            Assert.Greater(Power.TeamPower, powerBefore, "전투력이 보너스를 받는다 (Synergy 더러움)");

            foreach (var enemy in BattleCfg.enemies) Codex.NoteEnemy(enemy.id);
            Codex.NoteSynergy("missing", 0);
            mods = Synergy.BuildModifiers();
            Assert.AreEqual(5f, mods.Team[StatType.ShopMaxHpPercent], 1e-4f, "적 9종 모두 → 수선점 내구도 +5%");
            Assert.AreEqual(0f, mods.Team[StatType.LampGainPercent], 1e-4f, "도감에 없는 시너지는 세지 않는다");
        }

        [Test]
        public void SynergyKindsSeen_AnsweredForProgress_AndChained()
        {
            SynergyDb.synergies = new List<SynergyDef> { Combo("a", 1), Combo("b", 1) };
            Rebuild();
            Progress.ExtraConditionValue = c => c.type == MetaConditionType.AffinityLevelCount ? 5 : (long?)null;
            Codex.Connect();
            Codex.NoteSynergy("a", 0);
            Codex.NoteSynergy("b", 0);

            Assert.AreEqual(2, Progress.GetValue(MetaCondition.Of(MetaConditionType.SynergyKindsSeen, 1)));
            Assert.AreEqual(5, Progress.GetValue(MetaCondition.Of(MetaConditionType.AffinityLevelCount, 1, "10")), "모르는 조건은 앞 모듈이 답한다 (이어 부르기)");
        }

        [Test]
        public void SeenSkills_Persist()
        {
            Assert.IsFalse(Codex.HasSeenSkill("skill_a"));
            Codex.MarkSkillSeen("skill_a");
            Codex.MarkSkillSeen(null);
            Assert.IsTrue(Codex.HasSeenSkill("skill_a"));
            Assert.IsTrue(Data.meta.codex.seenSkills.Contains("skill_a"), "세이브에 남는다");

            Rebuild();
            Assert.IsTrue(Codex.HasSeenSkill("skill_a"), "다시 만든 시스템도 본다");
            Assert.IsFalse(Codex.HasSeenSkill(null));
        }

        [Test]
        public void CompletionPercent_AndHints()
        {
            Connect();
            Assert.AreEqual(0f, Codex.CompletionPercent);
            Characters.Grant("button");
            int total = 7 + 3 + 9 + 2;
            Assert.AreEqual(100f / total, Codex.CompletionPercent, 1e-3f);

            var entries = new List<CodexEntryStatus>();
            Codex.GetEntries(CodexCategory.Friend, entries);
            Assert.AreEqual(7, entries.Count);
            var unowned = entries.Find(e => e.Id == "sock_base");
            Assert.IsFalse(unowned.Found);
            Assert.AreEqual("잃어버린 친구 찾기에서 만날 수 있어요", unowned.Hint);
            Codex.GetEntries(CodexCategory.Enemy, entries);
            Assert.AreEqual("스테이지 1-2부터 나타나요", entries.Find(e => e.Id == "enemy_moth").Hint);
            foreach (var entry in entries) StringAssert.DoesNotContain("습니다", entry.Hint ?? string.Empty);
        }

        [Test]
        public void EnemyKillPath_DoesNotAllocate()
        {
            Connect();
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            for (float t = 0f; t < 20f && Battle.Sim.Enemies.Count == 0; t += 0.1f) Battle.Tick(0.1f);
            Assert.Greater(Battle.Sim.Enemies.Count, 0, "적이 나왔다");
            var kill = new BattleEvent(BattleEventType.EnemyKilled, -1, Battle.Sim.Enemies[0].Id);

            string defId = Battle.Sim.GetEnemyDefId(Battle.Sim.Enemies[0].Id);
            Codex.OnBattleEvent(kill); // 처음 = 찾음 (여기서는 할당해도 된다)
            Assert.IsTrue(Found(CodexCategory.Enemy, defId));
            for (int i = 0; i < 50; i++) Codex.OnBattleEvent(kill); // 데우기

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) Codex.OnBattleEvent(kill);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "이미 찾은 적 처치 1,000번 = 할당 0");
        }

        [Test]
        public void Connect_IsOnce_AndBonusHookIsChained()
        {
            Connect();
            Codex.Connect();
            int calls = 0;
            Synergy.ExtraTeamModifiers += _ => calls++;
            Synergy.BuildModifiers();
            Assert.AreEqual(1, calls, "다른 모듈의 훅도 함께 불린다 (+=)");
            Assert.DoesNotThrow(() => Characters.Grant("sock"), "Connect를 두 번 불러도 안전");
            Assert.AreEqual(1, Data.meta.codex.found.Count);
        }
    }
}
