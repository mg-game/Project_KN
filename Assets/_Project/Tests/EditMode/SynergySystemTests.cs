using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 시너지 계산 테스트. 픽스처:
    /// 특성 — sock [천, 밤] / candle [빛, 밤] / button [천, 치장]
    /// 성향 — sock_base 방어, sock_a 돌격, sock_b 포획, sock_a2 저격 / candle_base 저격, candle_a 지원 / button_base 돌격
    /// </summary>
    public class SynergySystemTests : SystemsTestBase
    {
        const string Fabric = "trait_fabric";
        const string Night = "trait_night";
        const string Rain = "trait_rain";
        const string Charm = "trait_charm";
        const string Assault = "style_assault";
        const string SockCandle = "combo_sock_candle";
        const string Defense = "style_defense";
        const string AllThree = "combo_all";

        SkillDef _fabricSkill;
        int _changed;

        [SetUp]
        public void SetUpSynergy()
        {
            CharDb.GetLineage("sock").traits = new List<TraitType> { TraitType.Fabric, TraitType.Night };
            CharDb.GetLineage("candle").traits = new List<TraitType> { TraitType.Light, TraitType.Night };
            // 중복 특성은 한 번만 센다
            CharDb.GetLineage("button").traits = new List<TraitType> { TraitType.Fabric, TraitType.Charm, TraitType.Fabric };

            _fabricSkill = new SkillDef
            {
                id = "bonus_fabric_patch",
                name = "천 덧대기",
                trigger = SkillTrigger.OnBattleStart,
                target = SkillTargetType.Shop,
                effects = new List<SkillEffectDef> { new SkillEffectDef { type = SkillEffectType.Shield, value = 1f, duration = 10f } },
                vfx = SkillVfx.PatchHeal,
            };

            // 종류를 섞어 둔 순서 (GetSynergiesFor가 특성 → 역할 → 조합으로 묶는지 확인)
            SynergyDb.synergies = new List<SynergyDef>
            {
                new SynergyDef
                {
                    id = Fabric, name = "천", kind = SynergyKind.Trait, trait = TraitType.Fabric,
                    favoredModifier = StageModifierType.DustStorm, favoredMultiplier = 1.5f,
                    tiers = new List<SynergyTier>
                    {
                        Tier(2, true, Mod(StatType.HpPercent, 15f)),
                        Tier(3, true, Mod(StatType.HpPercent, 30f)),
                    },
                },
                new SynergyDef
                {
                    id = Night, name = "밤", kind = SynergyKind.Trait, trait = TraitType.Night,
                    favoredModifier = StageModifierType.FullMoon, favoredMultiplier = 2f,
                    tiers = new List<SynergyTier> { Tier(2, false, Mod(StatType.LampGainPercent, 20f)) },
                },
                new SynergyDef
                {
                    id = SockCandle, name = "양말과 촛불", kind = SynergyKind.Combo,
                    lineageIds = new List<string> { "sock", "candle" },
                    tiers = new List<SynergyTier> { Tier(2, true, Mod(StatType.AttackPercent, 10f)) },
                },
                new SynergyDef
                {
                    id = Rain, name = "비", kind = SynergyKind.Trait, trait = TraitType.Rain,
                    tiers = new List<SynergyTier> { Tier(1, true, Mod(StatType.ShieldPowerPercent, 10f)) },
                },
                new SynergyDef
                {
                    // 단계 순서를 일부러 뒤집어 둔다 (높은 count 단계가 앞)
                    id = Charm, name = "치장", kind = SynergyKind.Trait, trait = TraitType.Charm,
                    tiers = new List<SynergyTier>
                    {
                        Tier(2, true, Mod(StatType.GoldGainPercent, 20f)),
                        Tier(1, true, Mod(StatType.GoldGainPercent, 10f)),
                    },
                },
                new SynergyDef
                {
                    id = Assault, name = "돌격", kind = SynergyKind.Style, style = CombatStyle.Assault,
                    tiers = new List<SynergyTier> { Tier(2, false, Mod(StatType.AttackSpeedPercent, 15f)) },
                },
                new SynergyDef
                {
                    // 조합은 tier.count와 상관없이 계보가 모두 편성돼야 켜진다 (count 2인데 계보는 3)
                    id = AllThree, name = "셋이서", kind = SynergyKind.Combo,
                    lineageIds = new List<string> { "sock", "candle", "button", "sock" },
                    tiers = new List<SynergyTier> { Tier(2, false, Mod(StatType.StartResonanceFlat, 5f)) },
                },
                new SynergyDef
                {
                    id = Defense, name = "방어", kind = SynergyKind.Style, style = CombatStyle.Defense,
                    tiers = new List<SynergyTier> { Tier(3, true, Mod(StatType.ShopDamageReductionPercent, 10f)) },
                },
                null,
                new SynergyDef { id = "empty_tiers", kind = SynergyKind.Trait, trait = TraitType.Metal },
            };
            SynergyDb.synergies[1].tiers[0].description = "밤 특성 2명: 불빛 게이지 충전 +20%";
            SynergyDb.synergies[0].tiers[1].bonusSkill = _fabricSkill;

            _changed = 0;
            Synergy.Changed += () => _changed++;
        }

        static SynergyTier Tier(int count, bool membersOnly, params StatModifier[] modifiers) => new SynergyTier
        {
            count = count,
            membersOnly = membersOnly,
            modifiers = new List<StatModifier>(modifiers),
        };

        static StatModifier Mod(StatType stat, float value) => new StatModifier { stat = stat, value = value };

        static SynergyStatus Find(List<SynergyStatus> statuses, string id) => statuses.Find(s => s.Def.id == id);

        SynergyStatus Status(string id) => Find(Synergy.GetStatuses(), id);

        void GrantAll()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
        }

        void Place(CharacterRole role, int slot, string formId) => Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(role, slot, formId), formId);

        /// <summary>지킴이 sock_base · candle_base, 잔향이 button_base → 천 2(sock, button) / 밤 2(sock, candle) / 치장 1 / 두 조합 모두</summary>
        void PlaceMixedTeam()
        {
            GrantAll();
            Place(CharacterRole.Guardian, 0, "sock_base");
            Place(CharacterRole.Guardian, 2, "candle_base");
            Place(CharacterRole.Echo, 0, "button_base");
        }

        // ───────────────────────────── 세기 ─────────────────────────────

        [Test]
        public void Count_GuardianAndEchoOfSameLineage_CountsTwo()
        {
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Place(CharacterRole.Guardian, 0, "sock_base");
            Place(CharacterRole.Echo, 0, "sock_a");

            var fabric = Status(Fabric);
            Assert.AreEqual(2, fabric.Count);
            Assert.IsTrue(fabric.IsActive);
            CollectionAssert.AreEquivalent(new[] { "sock_base", "sock_a" }, fabric.MemberFormIds);
            CollectionAssert.AreEqual(new[] { "sock" }, fabric.MemberLineageIds);
            Assert.AreEqual(2, Status(Night).Count);
        }

        [Test]
        public void Count_UsesLineageTraitsAndFormStyle()
        {
            PlaceMixedTeam();

            var statuses = Synergy.GetStatuses();
            Assert.AreEqual(2, Find(statuses, Fabric).Count);
            CollectionAssert.AreEquivalent(new[] { "sock", "button" }, Find(statuses, Fabric).MemberLineageIds);
            Assert.AreEqual(2, Find(statuses, Night).Count);
            CollectionAssert.AreEquivalent(new[] { "sock", "candle" }, Find(statuses, Night).MemberLineageIds);
            Assert.AreEqual(1, Find(statuses, Charm).Count, "button의 중복 특성(천)은 한 번만 센다");
            Assert.AreEqual(0, Find(statuses, Rain).Count);
            Assert.AreEqual(1, Find(statuses, Assault).Count);
            CollectionAssert.AreEqual(new[] { "button_base" }, Find(statuses, Assault).MemberFormIds);
            Assert.AreEqual(1, Find(statuses, Defense).Count);
            CollectionAssert.AreEqual(new[] { "sock_base" }, Find(statuses, Defense).MemberFormIds);
        }

        [Test]
        public void Count_IgnoresLockedEchoSlot_UnownedAndLockedForms()
        {
            Characters.Grant("button");
            Data.formation.echoes[5] = "button_base"; // 잠긴 6번째 칸
            Assert.AreEqual(0, Status(Charm).Count);

            Data.battle.highestStage = FormationSystem.EchoSlotUnlockStage;
            Assert.AreEqual(1, Status(Charm).Count, "칸이 열리면 센다");

            Data.formation.guardians[0] = "candle_base"; // 미보유 계보
            Data.formation.guardians[1] = "no_such_form";
            Characters.Grant("sock");
            Data.formation.guardians[2] = "sock_a"; // 해금 안 된 형태
            Assert.AreEqual(0, Status(Night).Count);
            Assert.AreEqual(1, Status(Fabric).Count, "button만");
        }

        // ───────────────────────────── 단계 ─────────────────────────────

        [Test]
        public void Tier_HighestSatisfied_AndNextTierProgress()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Characters.UnlockForm("sock_a");
            var tiers = SynergyDb.Get(Fabric).tiers;

            Place(CharacterRole.Guardian, 0, "sock_base");
            var fabric = Status(Fabric);
            Assert.AreEqual(1, fabric.Count);
            Assert.AreEqual(-1, fabric.TierIndex);
            Assert.IsFalse(fabric.IsActive);
            Assert.IsNull(fabric.ActiveTier);
            Assert.AreSame(tiers[0], fabric.NextTier);
            Assert.AreEqual(2, fabric.NextCount);
            Assert.AreEqual(3, fabric.MaxCount);
            Assert.AreEqual(1, fabric.Remaining);
            Assert.AreEqual("1/2", fabric.ProgressText);

            Place(CharacterRole.Echo, 0, "sock_a");
            fabric = Status(Fabric);
            Assert.AreEqual(0, fabric.TierIndex);
            Assert.AreSame(tiers[0], fabric.ActiveTier);
            Assert.AreSame(tiers[1], fabric.NextTier);
            Assert.AreEqual("2/3", fabric.ProgressText);
            Assert.IsFalse(fabric.IsMaxed);

            Place(CharacterRole.Guardian, 1, "button_base");
            fabric = Status(Fabric);
            Assert.AreEqual(3, fabric.Count);
            Assert.AreEqual(1, fabric.TierIndex);
            Assert.IsNull(fabric.NextTier);
            Assert.AreEqual(0, fabric.NextCount);
            Assert.IsTrue(fabric.IsMaxed);
            Assert.AreEqual(0, fabric.Remaining);
            Assert.AreEqual("3/3", fabric.ProgressText);
        }

        [Test]
        public void Tier_UnsortedTiers_PicksHighestCountNotIndex()
        {
            Characters.Grant("button");
            Place(CharacterRole.Echo, 0, "button_base");

            var charm = Status(Charm);
            var tiers = SynergyDb.Get(Charm).tiers;
            Assert.AreEqual(1, charm.TierIndex, "count 1 단계는 인덱스 1");
            Assert.AreSame(tiers[1], charm.ActiveTier);
            Assert.AreSame(tiers[0], charm.NextTier);
            Assert.AreEqual(2, charm.NextCount);
            Assert.AreEqual(2, charm.MaxCount);
        }

        [Test]
        public void Tier_SingleUnitTier_ActiveWithOne()
        {
            Characters.Grant("button");
            Assert.IsFalse(Status(Charm).IsActive, "0명이면 count 1 단계도 꺼짐");
            Place(CharacterRole.Guardian, 0, "button_base");
            Assert.IsTrue(Status(Charm).IsActive);
        }

        // ───────────────────────────── 조합 ─────────────────────────────

        [Test]
        public void Combo_RequiresEveryLineage_OnAnySide()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Place(CharacterRole.Guardian, 0, "sock_base");

            var combo = Status(SockCandle);
            Assert.IsFalse(combo.IsActive);
            Assert.AreEqual(1, combo.Count);
            Assert.AreEqual(2, combo.NextCount);
            Assert.AreSame(SynergyDb.Get(SockCandle).tiers[0], combo.NextTier);
            CollectionAssert.AreEqual(new[] { "candle" }, combo.MissingLineageIds);
            Assert.AreEqual("1/2", combo.ProgressText);

            Place(CharacterRole.Echo, 0, "candle_base");
            combo = Status(SockCandle);
            Assert.IsTrue(combo.IsActive);
            Assert.AreEqual(0, combo.TierIndex);
            Assert.IsNull(combo.NextTier);
            Assert.AreEqual(2, combo.Count);
            Assert.AreEqual("2/2", combo.ProgressText);
            CollectionAssert.IsEmpty(combo.MissingLineageIds);
            CollectionAssert.AreEquivalent(new[] { "sock_base", "candle_base" }, combo.MemberFormIds);
            CollectionAssert.AreEquivalent(new[] { "sock", "candle" }, combo.MemberLineageIds);
        }

        [Test]
        public void Combo_IgnoresTierCount_AndDuplicateLineageIds()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Place(CharacterRole.Guardian, 0, "sock_base");
            Place(CharacterRole.Guardian, 1, "candle_base");

            var all = Status(AllThree);
            Assert.IsFalse(all.IsActive, "tier.count = 2여도 계보 3개가 모두 있어야 한다");
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(3, all.NextCount);
            Assert.AreEqual(3, all.MaxCount);
            CollectionAssert.AreEqual(new[] { "button" }, all.MissingLineageIds);

            Characters.Grant("button");
            Place(CharacterRole.Echo, 0, "button_base");
            Assert.IsTrue(Status(AllThree).IsActive);
            Assert.AreEqual(3, Status(AllThree).Count);
        }

        // ───────────────────────────── 정렬 / 목록 ─────────────────────────────

        [Test]
        public void Statuses_ActiveFirst_ThenClosestToNextTier()
        {
            PlaceMixedTeam();

            var statuses = Synergy.GetStatuses();
            Assert.AreEqual(8, statuses.Count, "null / 단계 없는 시너지는 빠진다");
            AssertSorted(statuses);
            Assert.IsTrue(statuses[0].IsActive);

            // 켜진 것: 천(2/3, 남은 1), 밤(최고 단계), 치장(1/2, 남은 1), 양말과 촛불(최고), 셋이서(최고)
            var active = Synergy.GetActive();
            CollectionAssert.AreEquivalent(new[] { Fabric, Night, Charm, SockCandle, AllThree }, active.ConvertAll(s => s.Def.id));
            // 최고 단계(남은 0)가 먼저 → 수가 많은 순(셋이서 3) → 데이터 순서
            CollectionAssert.AreEqual(new[] { AllThree, Night, SockCandle, Fabric, Charm }, active.ConvertAll(s => s.Def.id));

            // 꺼진 것: 돌격 1/2(남은 1, 수 1) → 방어 1/3(남은 2, 수 1) / 비 0/1(남은 1, 수 0)
            var inactive = statuses.FindAll(s => !s.IsActive).ConvertAll(s => s.Def.id);
            CollectionAssert.AreEqual(new[] { Assault, Rain, Defense }, inactive);
        }

        [Test]
        public void Statuses_EmptyFormation_AllInactive()
        {
            var statuses = Synergy.GetStatuses();
            Assert.AreEqual(8, statuses.Count);
            Assert.IsTrue(statuses.TrueForAll(s => !s.IsActive && s.Count == 0 && s.NextTier != null));
            AssertSorted(statuses);
            CollectionAssert.IsEmpty(Synergy.GetActive());
        }

        static void AssertSorted(List<SynergyStatus> statuses)
        {
            for (int i = 1; i < statuses.Count; i++)
            {
                var a = statuses[i - 1];
                var b = statuses[i];
                Assert.IsFalse(!a.IsActive && b.IsActive, $"켜진 것이 먼저: {a.Def.id} → {b.Def.id}");
                if (a.IsActive != b.IsActive) continue;
                Assert.LessOrEqual(a.Remaining, b.Remaining, $"남은 수 순: {a.Def.id} → {b.Def.id}");
            }
        }

        [Test]
        public void GetSynergiesFor_GroupsTraitStyleCombo()
        {
            var sock = Synergy.GetSynergiesFor("sock").ConvertAll(s => s.id);
            CollectionAssert.AreEqual(new[] { Fabric, Night, Assault, Defense, SockCandle, AllThree }, sock);

            var button = Synergy.GetSynergiesFor("button").ConvertAll(s => s.id);
            CollectionAssert.AreEqual(new[] { Fabric, Charm, Assault, AllThree }, button);

            var candle = Synergy.GetSynergiesFor("candle").ConvertAll(s => s.id);
            CollectionAssert.AreEqual(new[] { Night, SockCandle, AllThree }, candle, "저격/지원 시너지는 픽스처에 없음");

            CollectionAssert.IsEmpty(Synergy.GetSynergiesFor("nobody"));
            CollectionAssert.IsEmpty(Synergy.GetSynergiesFor(null));
        }

        [Test]
        public void GetSynergiesForForm_UsesOnlyThatFormsStyle()
        {
            CollectionAssert.AreEqual(new[] { Fabric, Night, Assault, SockCandle, AllThree },
                Synergy.GetSynergiesForForm("sock_a").ConvertAll(s => s.id));
            CollectionAssert.AreEqual(new[] { Fabric, Night, Defense, SockCandle, AllThree },
                Synergy.GetSynergiesForForm("sock_base").ConvertAll(s => s.id));
            CollectionAssert.AreEqual(new[] { Night, SockCandle, AllThree },
                Synergy.GetSynergiesForForm("candle_a").ConvertAll(s => s.id), "지원 시너지는 픽스처에 없음");
            CollectionAssert.IsEmpty(Synergy.GetSynergiesForForm("no_such_form"));
            CollectionAssert.IsEmpty(Synergy.GetSynergiesForForm(null));
            CollectionAssert.IsEmpty(new SynergySystem(Data, null, Characters, Formation).GetSynergiesForForm("sock_a"));
        }

        // ───────────────────────────── 전투 능력치 ─────────────────────────────

        [Test]
        public void BuildModifiers_MembersOnlyGoesToLineage_TeamGoesToTeam()
        {
            PlaceMixedTeam();

            var mods = Synergy.BuildModifiers();
            // 천(구성원): sock, button 체력 +15
            Assert.AreEqual(15f, mods.Lineage["sock"][StatType.HpPercent]);
            Assert.AreEqual(15f, mods.Lineage["button"][StatType.HpPercent]);
            Assert.AreEqual(0f, mods.Team[StatType.HpPercent]);
            Assert.AreEqual(0f, mods.For("candle")[StatType.HpPercent]);
            // 밤(팀): 불빛 충전 +20
            Assert.AreEqual(20f, mods.Team[StatType.LampGainPercent]);
            Assert.AreEqual(20f, mods.For("button")[StatType.LampGainPercent]);
            // 치장(구성원, 1단계): button 단추 +10
            Assert.AreEqual(10f, mods.Lineage["button"][StatType.GoldGainPercent]);
            Assert.AreEqual(0f, mods.For("sock")[StatType.GoldGainPercent]);
            // 양말과 촛불(조합, 구성원): sock, candle 공격력 +10
            Assert.AreEqual(10f, mods.Lineage["sock"][StatType.AttackPercent]);
            Assert.AreEqual(10f, mods.Lineage["candle"][StatType.AttackPercent]);
            Assert.AreEqual(0f, mods.For("button")[StatType.AttackPercent]);
            // 셋이서(조합, 팀): 시작 잔향 +5
            Assert.AreEqual(5f, mods.Team[StatType.StartResonanceFlat]);
            // 꺼진 시너지는 없음
            Assert.AreEqual(0f, mods.Team[StatType.AttackSpeedPercent]);
            Assert.AreEqual(0f, mods.For("sock")[StatType.ShopDamageReductionPercent]);
            CollectionAssert.IsEmpty(mods.BonusSkills);
        }

        [Test]
        public void BuildModifiers_ListsActiveSynergiesWithTierIndex()
        {
            PlaceMixedTeam();

            var mods = Synergy.BuildModifiers();
            var expected = new List<KeyValuePair<string, int>>();
            foreach (var status in Synergy.GetActive()) expected.Add(new KeyValuePair<string, int>(status.Def.id, status.TierIndex));
            CollectionAssert.AreEqual(expected, mods.ActiveSynergies, "GetActive와 같은 순서");
            CollectionAssert.Contains(mods.ActiveSynergies, new KeyValuePair<string, int>(Charm, 1));
            CollectionAssert.Contains(mods.ActiveSynergies, new KeyValuePair<string, int>(Fabric, 0));
            Assert.IsFalse(mods.ActiveSynergies.Exists(p => p.Key == Assault));
        }

        [Test]
        public void BuildModifiers_FavoredStage_MultipliesOnlyThatSynergy()
        {
            PlaceMixedTeam();

            var dust = Synergy.BuildModifiers(StageModifierType.DustStorm);
            Assert.AreEqual(22.5f, dust.Lineage["sock"][StatType.HpPercent], 0.0001f);
            Assert.AreEqual(20f, dust.Team[StatType.LampGainPercent], 0.0001f, "밤은 먼지 폭풍 강화 대상이 아님");
            Assert.AreEqual(10f, dust.Lineage["sock"][StatType.AttackPercent], 0.0001f);

            var moon = Synergy.BuildModifiers(StageModifierType.FullMoon);
            Assert.AreEqual(40f, moon.Team[StatType.LampGainPercent], 0.0001f);
            Assert.AreEqual(15f, moon.Lineage["sock"][StatType.HpPercent], 0.0001f);

            var rain = Synergy.BuildModifiers(StageModifierType.Rain);
            Assert.AreEqual(15f, rain.Lineage["sock"][StatType.HpPercent], 0.0001f);
            Assert.AreEqual(20f, rain.Team[StatType.LampGainPercent], 0.0001f);

            Assert.AreEqual(1.5f, SynergySystem.FavoredMultiplier(SynergyDb.Get(Fabric), StageModifierType.DustStorm));
            Assert.AreEqual(1f, SynergySystem.FavoredMultiplier(SynergyDb.Get(Rain), StageModifierType.None), "None끼리는 배율 없음");
            Assert.AreEqual(1f, SynergySystem.FavoredMultiplier(null, StageModifierType.Rain));
        }

        [Test]
        public void BuildModifiers_EchoesOnly_CountsDeckOnly()
        {
            GrantAll();
            Characters.UnlockForm("sock_a");
            Place(CharacterRole.Guardian, 0, "candle_base");
            Place(CharacterRole.Echo, 0, "sock_a");
            Place(CharacterRole.Echo, 1, "button_base");

            var full = Synergy.BuildModifiers();
            Assert.AreEqual(20f, full.Team[StatType.LampGainPercent], "밤 2 (candle 지킴이 + sock 잔향이)");
            Assert.AreEqual(10f, full.Lineage["candle"][StatType.AttackPercent]);

            var echoes = Synergy.BuildModifiers(StageModifierType.None, echoesOnly: true);
            Assert.AreEqual(15f, echoes.Lineage["sock"][StatType.HpPercent], "천 2 (잔향이 둘)");
            Assert.AreEqual(15f, echoes.Lineage["button"][StatType.HpPercent]);
            Assert.AreEqual(0f, echoes.Team[StatType.LampGainPercent], "지킴이 candle은 세지 않음 → 밤 1");
            Assert.IsFalse(echoes.Lineage.ContainsKey("candle"));
            Assert.AreEqual(0f, echoes.For("sock")[StatType.AttackPercent], "조합도 잔향이만 보고 판단");
            Assert.IsFalse(echoes.ActiveSynergies.Exists(p => p.Key == Night || p.Key == SockCandle));
            Assert.IsTrue(echoes.ActiveSynergies.Exists(p => p.Key == Fabric));
        }

        [Test]
        public void BuildModifiers_BonusSkill_AddedToMemberLineagesOnce()
        {
            GrantAll();
            Characters.UnlockForm("sock_a");
            Place(CharacterRole.Guardian, 0, "sock_base");
            Place(CharacterRole.Echo, 0, "sock_a");
            Place(CharacterRole.Echo, 1, "button_base");
            Place(CharacterRole.Guardian, 1, "candle_base");

            var mods = Synergy.BuildModifiers();
            Assert.AreEqual(30f, mods.Lineage["sock"][StatType.HpPercent], "천 3단계");
            CollectionAssert.AreEqual(new[] { _fabricSkill }, mods.BonusSkills["sock"], "sock 형태가 둘이어도 한 번");
            CollectionAssert.AreEqual(new[] { _fabricSkill }, mods.BonusSkills["button"]);
            Assert.IsFalse(mods.BonusSkills.ContainsKey("candle"));
        }

        /// <summary>지킴이 sock_base(방어) + 잔향이 sock_a(돌격) · button_base(돌격) → 돌격 2명인데 sock 계보에 방어 형태가 섞여 있다</summary>
        void PlaceAssaultWithDefenseSibling()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Characters.UnlockForm("sock_a");
            Place(CharacterRole.Guardian, 0, "sock_base");
            Place(CharacterRole.Echo, 0, "sock_a");
            Place(CharacterRole.Echo, 1, "button_base");
        }

        [Test]
        public void StyleMembersOnly_GoesToMemberForms_NotOtherFormOfSameLineage()
        {
            SynergyDb.Get(Assault).tiers[0].membersOnly = true;
            PlaceAssaultWithDefenseSibling();

            var assault = Status(Assault);
            Assert.IsTrue(assault.IsActive);
            CollectionAssert.AreEqual(new[] { "sock_a", "button_base" }, assault.MemberFormIds);
            CollectionAssert.IsEmpty(assault.SharedFormIds, "형태 단위로 들어가므로 반대편 방어 지킴이는 받지 않는다");
            Assert.IsFalse(assault.HasSharedForms);

            var mods = Synergy.BuildModifiers();
            // 역할 시너지 구성원 전용 → 구성원 형태의 Form 칸
            Assert.AreEqual(15f, mods.Form["sock_a"][StatType.AttackSpeedPercent]);
            Assert.AreEqual(15f, mods.Form["button_base"][StatType.AttackSpeedPercent]);
            Assert.IsFalse(mods.Form.ContainsKey("sock_base"), "같은 계보의 방어 지킴이는 구성원이 아니다");
            Assert.AreEqual("sock", mods.FormLineage["sock_a"]);
            Assert.AreEqual("button", mods.FormLineage["button_base"]);
            Assert.AreEqual(0f, mods.Lineage["sock"][StatType.AttackSpeedPercent], "계보 칸에는 들어가지 않는다");
            Assert.AreEqual(0f, mods.Team[StatType.AttackSpeedPercent]);
            // 유닛 하나의 값 = For(계보, 형태): 특성(계보 단위) + 역할(형태 단위)
            Assert.AreEqual(15f, mods.For("sock", "sock_a")[StatType.AttackSpeedPercent]);
            Assert.AreEqual(0f, mods.For("sock", "sock_base")[StatType.AttackSpeedPercent]);
            Assert.AreEqual(30f, mods.For("sock", "sock_base")[StatType.HpPercent], "천 3단계는 계보 단위라 방어 지킴이도 받는다");
            Assert.AreEqual(30f, mods.For("sock", "sock_a")[StatType.HpPercent]);
            Assert.AreEqual(0f, mods.For("sock")[StatType.AttackSpeedPercent], "For(계보)는 형태 칸을 빼고 센다");

            // 특성은 계보 단위로 세므로 계보의 모든 형태가 구성원
            var fabric = Status(Fabric);
            CollectionAssert.AreEquivalent(new[] { "sock_base", "sock_a", "button_base" }, fabric.MemberFormIds);
            CollectionAssert.IsEmpty(fabric.SharedFormIds);
            CollectionAssert.IsEmpty(Status(Defense).SharedFormIds);
        }

        [Test]
        public void StyleBonusSkill_GoesToMemberForms_TraitBonusToLineage()
        {
            PlaceAssaultWithDefenseSibling();
            Assert.IsTrue(Status(Assault).IsActive);
            CollectionAssert.IsEmpty(Status(Assault).SharedFormIds, "팀 전체 능력치만 있으면 모두가 받으니 따로 알릴 것이 없다");

            var cheer = new SkillDef
            {
                id = "bonus_assault_cheer",
                name = "기세 등등",
                trigger = SkillTrigger.OnKill,
                target = SkillTargetType.Self,
                effects = new List<SkillEffectDef> { new SkillEffectDef { type = SkillEffectType.BuffAttack, value = 10f, duration = 3f } },
                vfx = SkillVfx.BuffCrown,
            };
            SynergyDb.Get(Assault).tiers[0].bonusSkill = cheer;
            CollectionAssert.IsEmpty(Status(Assault).SharedFormIds, "보너스 스킬도 형태 단위");
            var mods = Synergy.BuildModifiers();
            CollectionAssert.AreEqual(new[] { cheer }, mods.FormBonusSkills["sock_a"]);
            CollectionAssert.AreEqual(new[] { cheer }, mods.FormBonusSkills["button_base"]);
            Assert.IsFalse(mods.FormBonusSkills.ContainsKey("sock_base"), "방어 지킴이는 돌격 보너스 스킬을 쓰지 않는다");
            CollectionAssert.DoesNotContain(mods.BonusSkills["sock"], cheer);
            CollectionAssert.AreEqual(new[] { _fabricSkill }, mods.BonusSkills["sock"], "특성 보너스 스킬(천 3단계)은 계보 단위");
            CollectionAssert.AreEqual(new[] { _fabricSkill }, mods.BonusSkills["button"]);
            Assert.AreEqual("sock", mods.FormLineage["sock_a"], "능력치가 없어도 형태의 계보를 기록");

            // 구성원 전용인데 쓸 수 있는 능력치가 없으면(0 / 범위 밖) 형태 칸에 아무것도 안 들어간다
            var tier = SynergyDb.Get(Assault).tiers[0];
            tier.bonusSkill = null;
            tier.membersOnly = true;
            tier.modifiers = new List<StatModifier> { Mod(StatType.AttackPercent, 0f), Mod((StatType)999, 10f), null };
            var empty = Synergy.BuildModifiers();
            CollectionAssert.IsEmpty(empty.Form);
            CollectionAssert.IsEmpty(empty.FormBonusSkills);
            Assert.AreEqual(0f, empty.For("sock", "sock_a")[StatType.AttackPercent]);
        }

        [Test]
        public void StyleMembersOnly_GlobalStat_CountsOncePerUnit()
        {
            // 역할 시너지 구성원 전용 칸에 전체 능력치가 들어가도 구성원 수만큼 겹치지 않는다 (유닛 하나 = 계보 칸 + 형태 칸)
            SynergyDb.Get(Assault).tiers[0].membersOnly = true;
            SynergyDb.Get(Assault).tiers[0].modifiers = new List<StatModifier> { Mod(StatType.ShopMaxHpPercent, 10f) };
            SynergyDb.Get(Fabric).tiers[1].modifiers = new List<StatModifier> { Mod(StatType.ShopMaxHpPercent, 5f) };
            PlaceAssaultWithDefenseSibling();

            var mods = Synergy.BuildModifiers();
            Assert.AreEqual(10f, mods.Form["sock_a"][StatType.ShopMaxHpPercent]);
            Assert.AreEqual(5f, mods.Lineage["sock"][StatType.ShopMaxHpPercent]);
            Assert.AreEqual(15f, BattleSimulator.SumGlobal(mods, StatType.ShopMaxHpPercent), 1e-4, "sock_a = 천 5 + 돌격 10 (한 유닛)");
        }

        [Test]
        public void Shared_PreviewAndDescribe()
        {
            SynergyDb.Get(Assault).tiers[0].membersOnly = true;
            CharDb.GetForm("sock_base").name = "양말";
            Characters.Grant("sock");
            Characters.Grant("button");
            Characters.UnlockForm("sock_a");
            Place(CharacterRole.Echo, 0, "sock_a");
            Place(CharacterRole.Echo, 1, "button_base");
            CollectionAssert.IsEmpty(Status(Assault).SharedFormIds);
            Assert.AreEqual(string.Empty, Synergy.DescribeShared(Status(Assault)));

            var preview = Find(Synergy.PreviewPlace(CharacterRole.Guardian, 0, "sock_base"), Assault);
            CollectionAssert.AreEqual(new[] { "sock_a", "button_base" }, preview.MemberFormIds);
            CollectionAssert.IsEmpty(preview.SharedFormIds, "미리보기도 같은 규칙: 반대편 방어 지킴이는 받지 않는다");
            Assert.AreEqual(string.Empty, Synergy.DescribeShared(preview));
            Assert.AreEqual(string.Empty, Synergy.DescribeShared(null));

            // 문구 만들기 자체는 그대로 (호환용)
            var two = new SynergyStatus { SharedFormIds = { "sock_base", "no_such_form" } };
            Assert.AreEqual("같은 계보라서 양말, no_such_form도 효과를 함께 받아요", Synergy.DescribeShared(two), "이름이 없으면 ID");
        }

        [Test]
        public void BuildModifiers_DoesNotDuplicateLineageResonance()
        {
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Place(CharacterRole.Guardian, 0, "sock_base");
            Place(CharacterRole.Echo, 0, "sock_a");
            Assert.Greater(Formation.TeamAttackBonusPercent, 0f, "계보 공명은 FormationSystem이 그대로 계산");

            var mods = Synergy.BuildModifiers();
            Assert.AreEqual(0f, mods.Team[StatType.AttackPercent]);
            Assert.AreEqual(0f, mods.For("sock")[StatType.AttackPercent]);
            Assert.AreEqual(1, Formation.GetSynergies().Count);
        }

        [Test]
        public void BuildModifiers_EmptyFormation_IsEmpty()
        {
            var mods = Synergy.BuildModifiers(StageModifierType.Festival);
            Assert.IsTrue(mods.Team.IsZero);
            CollectionAssert.IsEmpty(mods.Lineage);
            CollectionAssert.IsEmpty(mods.Form);
            CollectionAssert.IsEmpty(mods.BonusSkills);
            CollectionAssert.IsEmpty(mods.FormBonusSkills);
            CollectionAssert.IsEmpty(mods.ActiveSynergies);
        }

        [Test]
        public void ExtraTeamModifiers_AddToTheTeam_AfterSynergies_ForEveryBuild()
        {
            PlaceMixedTeam();
            int changed = _changed;
            int calls = 0;
            Synergy.ExtraTeamModifiers += team =>
            {
                calls++;
                team.Add(StatType.AttackPercent, 4f); // 도감 모음 보너스 자리 (Q-05, P4-1)
            };

            var mods = Synergy.BuildModifiers();
            Assert.AreEqual(4f, mods.Team[StatType.AttackPercent], 1e-4f, "팀 칸에 더한다");
            Assert.AreEqual(20f, mods.Team[StatType.LampGainPercent], 1e-4f, "시너지 값은 그대로");
            Assert.AreEqual(14f, mods.For("sock")[StatType.AttackPercent], 1e-4f, "구성원 조합 +10 위에 팀 +4");
            Assert.AreEqual(4f, mods.For("button")[StatType.AttackPercent], 1e-4f, "조합 밖 구성원도 팀 몫은 받는다");

            var echoes = Synergy.BuildModifiers(StageModifierType.None, echoesOnly: true);
            Assert.AreEqual(4f, echoes.Team[StatType.AttackPercent], 1e-4f, "잔향이 던전에도");
            Assert.AreEqual(2, calls, "만들 때마다 한 번");
            Assert.AreEqual(changed, _changed, "훅을 부르는 것만으로는 Changed가 없다");

            Formation.Remove(CharacterRole.Guardian, 0);
            Formation.Remove(CharacterRole.Guardian, 2);
            Formation.Remove(CharacterRole.Echo, 0);
            var empty = Synergy.BuildModifiers();
            Assert.AreEqual(4f, empty.Team[StatType.AttackPercent], 1e-4f, "빈 편성에도 (모음 보너스는 편성과 무관)");
            CollectionAssert.IsEmpty(empty.ActiveSynergies);

            var noDb = new SynergySystem(Data, null, Characters, Formation);
            noDb.ExtraTeamModifiers = team => team.Add(StatType.HpPercent, 3f);
            Assert.AreEqual(3f, noDb.BuildModifiers().Team[StatType.HpPercent], 1e-4f, "시너지를 계산하지 못해도 훅은 불린다");
        }

        [Test]
        public void MarkDirty_FiresChanged_SoPowerAndBattleRebuild()
        {
            int before = _changed;
            Synergy.MarkDirty();
            Assert.AreEqual(before + 1, _changed, "시너지 밖 값(도감 보너스)이 바뀌면 Changed");
            Synergy.ExtraTeamModifiers = null;
            Assert.DoesNotThrow(() => Synergy.BuildModifiers());
        }

        // ───────────────────────────── 미리보기 ─────────────────────────────

        [Test]
        public void Preview_ReflectsPlacement_WithoutMutating()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Place(CharacterRole.Guardian, 0, "sock_base");
            int formationChanged = 0;
            Formation.Changed += () => formationChanged++;
            _changed = 0;

            var preview = Synergy.PreviewPlace(CharacterRole.Echo, 0, "button_base");
            Assert.AreEqual(2, Find(preview, Fabric).Count);
            Assert.IsTrue(Find(preview, Fabric).IsActive);
            Assert.IsTrue(Find(preview, Charm).IsActive);
            Assert.AreEqual(8, preview.Count, "꺼진 시너지도 포함");
            AssertSorted(preview);

            Assert.IsNull(Formation.GetEcho(0));
            Assert.AreEqual(0, formationChanged);
            Assert.AreEqual(0, _changed);
            Assert.AreEqual(1, Status(Fabric).Count, "실제 상태는 그대로");
        }

        [Test]
        public void Preview_ReplacesSlotOccupant()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Characters.UnlockForm("sock_a");
            Place(CharacterRole.Echo, 0, "button_base");

            var preview = Synergy.PreviewPlace(CharacterRole.Echo, 0, "sock_a");
            Assert.AreEqual(0, Find(preview, Charm).Count, "칸에 있던 button은 빠진다");
            Assert.AreEqual(1, Find(preview, Night).Count);
            Assert.AreEqual(1, Find(preview, Fabric).Count);
            CollectionAssert.AreEqual(new[] { "sock_a" }, Find(preview, Fabric).MemberFormIds);
            Assert.AreEqual("button_base", Formation.GetEcho(0));
        }

        [Test]
        public void Preview_MoveWithinSide_SwapsLikeTryPlace()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Place(CharacterRole.Guardian, 0, "sock_base");
            Place(CharacterRole.Guardian, 1, "button_base");

            var preview = Synergy.PreviewPlace(CharacterRole.Guardian, 1, "sock_base");
            Assert.AreEqual(2, Find(preview, Fabric).Count, "맞바꿔도 둘 다 남는다");
            Assert.AreEqual(1, Find(preview, Charm).Count);

            var moveToEmpty = Synergy.PreviewPlace(CharacterRole.Guardian, 4, "sock_base");
            Assert.AreEqual(2, Find(moveToEmpty, Fabric).Count, "빈칸으로 이동해도 중복으로 세지 않는다");
            Assert.AreEqual("sock_base", Formation.GetGuardian(0));
            Assert.IsNull(Formation.GetGuardian(4));
        }

        [Test]
        public void Preview_InvalidPlacement_ReturnsCurrent()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Place(CharacterRole.Guardian, 0, "sock_base");

            void AssertCurrent(List<SynergyStatus> preview, string because)
            {
                Assert.AreEqual(8, preview.Count, because);
                Assert.AreEqual(1, Find(preview, Fabric).Count, because);
                Assert.AreEqual(0, Find(preview, Charm).Count, because);
            }

            AssertCurrent(Synergy.PreviewPlace(CharacterRole.Echo, 0, "sock_base"), "반대편 등록");
            AssertCurrent(Synergy.PreviewPlace(CharacterRole.Guardian, 1, "candle_base"), "미보유");
            AssertCurrent(Synergy.PreviewPlace(CharacterRole.Guardian, 1, "sock_a"), "미해금");
            AssertCurrent(Synergy.PreviewPlace(CharacterRole.Echo, 5, "button_base"), "잠긴 칸");
            AssertCurrent(Synergy.PreviewPlace(CharacterRole.Guardian, 9, "button_base"), "없는 칸");
            AssertCurrent(Synergy.PreviewPlace(CharacterRole.Guardian, 1, null), "빈 ID");
        }

        // ───────────────────────────── 이벤트 / 안전성 ─────────────────────────────

        [Test]
        public void Changed_FiresOnFormationAndCharacterChanges()
        {
            Characters.Grant("sock");
            Assert.AreEqual(1, _changed, "Characters.Changed");

            Place(CharacterRole.Guardian, 0, "sock_base");
            Assert.AreEqual(2, _changed, "Formation.Changed");

            Formation.Remove(CharacterRole.Guardian, 0);
            Assert.AreEqual(3, _changed);

            Synergy.GetStatuses();
            Synergy.BuildModifiers();
            Assert.AreEqual(3, _changed, "조회는 이벤트를 내지 않는다");
        }

        [Test]
        public void NullDatabase_EverythingEmpty_NoThrow()
        {
            var system = new SynergySystem(Data, null, Characters, Formation);
            int changed = 0;
            system.Changed += () => changed++;
            Characters.Grant("sock");
            Place(CharacterRole.Guardian, 0, "sock_base");
            Assert.AreEqual(2, changed);

            Assert.IsNull(system.Db);
            CollectionAssert.IsEmpty(system.GetStatuses());
            CollectionAssert.IsEmpty(system.GetActive());
            CollectionAssert.IsEmpty(system.PreviewPlace(CharacterRole.Echo, 0, "sock_base"));
            CollectionAssert.IsEmpty(system.GetSynergiesFor("sock"));
            foreach (var echoesOnly in new[] { false, true })
            {
                var mods = system.BuildModifiers(StageModifierType.Rain, echoesOnly);
                Assert.IsNotNull(mods);
                Assert.IsTrue(mods.Team.IsZero);
                CollectionAssert.IsEmpty(mods.Lineage);
                CollectionAssert.IsEmpty(mods.BonusSkills);
                CollectionAssert.IsEmpty(mods.ActiveSynergies);
            }

            var bare = new SynergySystem(null, null, null, null);
            Assert.DoesNotThrow(() =>
            {
                bare.GetStatuses();
                bare.GetActive();
                bare.PreviewPlace(CharacterRole.Guardian, 0, "sock_base");
                bare.GetSynergiesFor("sock");
                bare.BuildModifiers(StageModifierType.DustStorm, true);
            });
        }

        [Test]
        public void NullDataInsideDatabase_IsSkipped()
        {
            SynergyDb.synergies = new List<SynergyDef>
            {
                null,
                new SynergyDef { id = "null_tiers", kind = SynergyKind.Trait, tiers = null },
                new SynergyDef { id = "null_tier", kind = SynergyKind.Style, tiers = new List<SynergyTier> { null } },
                new SynergyDef { id = "combo_no_lineages", kind = SynergyKind.Combo, tiers = new List<SynergyTier> { Tier(1, false) } },
                new SynergyDef
                {
                    id = "null_modifier", kind = SynergyKind.Style, style = CombatStyle.Defense,
                    tiers = new List<SynergyTier> { new SynergyTier { count = 1, modifiers = new List<StatModifier> { null } } },
                },
            };
            Characters.Grant("sock");
            Place(CharacterRole.Guardian, 0, "sock_base");

            var statuses = Synergy.GetStatuses();
            CollectionAssert.AreEqual(new[] { "null_modifier" }, statuses.ConvertAll(s => s.Def.id));
            Assert.IsTrue(statuses[0].IsActive);
            var mods = Synergy.BuildModifiers();
            Assert.IsTrue(mods.Team.IsZero);
            Assert.AreEqual(1, mods.ActiveSynergies.Count);
        }

        // ───────────────────────────── 표기 ─────────────────────────────

        [Test]
        public void DescribeModifier_NaturalKorean()
        {
            Assert.AreEqual("공격 속도 +15%", SynergySystem.DescribeModifier(Mod(StatType.AttackSpeedPercent, 15f)));
            Assert.AreEqual("공격력 +15%", SynergySystem.DescribeModifier(Mod(StatType.AttackPercent, 15f)));
            Assert.AreEqual("초당 수선점 회복 +0.5%", SynergySystem.DescribeModifier(Mod(StatType.ShopRegenPercentPerSecond, 0.5f)));
            Assert.AreEqual("시작 잔향 +5", SynergySystem.DescribeModifier(Mod(StatType.StartResonanceFlat, 5f)));
            Assert.AreEqual("잔향이 소환 비용 -1", SynergySystem.DescribeModifier(Mod(StatType.EchoCostReductionFlat, 1f)));
            Assert.AreEqual("수선점 받는 피해 -10%", SynergySystem.DescribeModifier(Mod(StatType.ShopDamageReductionPercent, 10f)));
            Assert.AreEqual("스킬 쿨타임 -12.5%", SynergySystem.DescribeModifier(Mod(StatType.SkillCooldownReductionPercent, 12.5f)));
            Assert.AreEqual("체력 -5%", SynergySystem.DescribeModifier(Mod(StatType.HpPercent, -5f)));
            Assert.AreEqual("보스에게 주는 피해 +20%", SynergySystem.DescribeModifier(StatType.BossDamagePercent, 20f));
            Assert.AreEqual(string.Empty, SynergySystem.DescribeModifier(null));
        }

        [Test]
        public void StatName_EveryStatHasKoreanName()
        {
            var names = new HashSet<string>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                string name = SynergySystem.StatName(stat);
                Assert.IsFalse(string.IsNullOrEmpty(name), stat.ToString());
                Assert.AreNotEqual(stat.ToString(), name, $"{stat}: 한국어 이름이 없음");
                Assert.IsTrue(names.Add(name), $"{stat}: 이름 중복 {name}");
                StringAssert.StartsWith(name, SynergySystem.DescribeModifier(stat, 1f));
            }
            Assert.AreEqual("공격 속도", SynergySystem.StatName(StatType.AttackSpeedPercent));
        }

        [Test]
        public void DescribeTier_AndActivatedText()
        {
            var tier = Tier(2, true, Mod(StatType.HpPercent, 15f), Mod(StatType.AttackSpeedPercent, 10f), null, Mod(StatType.GoldGainPercent, 0f));
            Assert.AreEqual("체력 +15%, 공격 속도 +10%", SynergySystem.DescribeModifiers(tier));
            Assert.AreEqual("체력 +22.5% · 공격 속도 +15%", SynergySystem.DescribeModifiers(tier, 1.5f, " · "));
            Assert.AreEqual(string.Empty, SynergySystem.DescribeModifiers(null));
            Assert.AreEqual("구성원", SynergySystem.ScopeName(tier));
            Assert.AreEqual("팀 전체", SynergySystem.ScopeName(Tier(2, false)));
            // 역할 시너지의 구성원 전용 단계도 형태 단위라 구성원만 받는다
            Assert.AreEqual("구성원", SynergySystem.ScopeName(SynergyDb.Get(Assault), tier), "역할 시너지도 구성원 형태만 받는다");
            Assert.AreEqual("팀 전체", SynergySystem.ScopeName(SynergyDb.Get(Assault), Tier(2, false)));
            Assert.AreEqual("구성원", SynergySystem.ScopeName(SynergyDb.Get(Fabric), tier), "특성은 계보 단위");
            Assert.AreEqual("구성원", SynergySystem.ScopeName(null, tier));

            var combo = new SynergyDef { id = "combo_mending_duo", name = "수선 듀오", kind = SynergyKind.Combo };
            Assert.AreEqual("'수선 듀오' 조합이 켜졌어요!", SynergySystem.ActivatedText(combo, 0));
            Assert.AreEqual("천 특성 2단계가 켜졌어요!", SynergySystem.ActivatedText(SynergyDb.Get(Fabric), 1));
            Assert.AreEqual("방어 역할 1단계가 켜졌어요!", SynergySystem.ActivatedText(SynergyDb.Get(Defense), 0));
            Assert.AreEqual(string.Empty, SynergySystem.ActivatedText(null, 0));
        }
    }
}
