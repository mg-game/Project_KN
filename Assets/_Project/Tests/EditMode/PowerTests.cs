using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 전투력 (PRG-01): 유닛 공식, 팀 가중치 1.0 / 0.6 / 0.3 + 수선점 몫 상한 (S5-24), 시너지·공명 반영, 권장 base×growth^(s−1) (+ 구간, PowerRatingTests),
    /// 던전 권장, 평가 1.0/0.8, 미리보기 (칸 · 레벨업 · 수선점 · 자동 편성), 변화 알림, 표기
    /// </summary>
    public class PowerTests : MetaSystemsTestBase
    {
        // TestContent 형태 기본값: 공격 10, 간격 1, 체력 100 → 10/1×10 + 100 = 200
        const long BaseUnit = 200;
        /// <summary>덱 잔향이 200 × 0.6</summary>
        const long EchoUnit = 120;
        static readonly long ShopHpPart = (long)System.Math.Round(GrowthSystem.CalcShopMaxHp(1) * 0.3);

        void PlaceBasicTeam()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
        }

        [Test]
        public void UnitFormula_MatchesSpec()
        {
            var stats = new CharacterStats { attack = 50, attackInterval = 0.5f, hp = 400 };
            Assert.AreEqual(50 / 0.5 * 10 + 400, PowerSystem.ComputeUnitPower(stats, null), 1e-6, "보정 없으면 기존 Power와 같다");
            Assert.AreEqual(stats.Power, PowerSystem.ComputeUnitPower(stats, null), 1e-3);

            var mods = new StatBlock();
            mods[StatType.AttackPercent] = 20;
            mods[StatType.AttackSpeedPercent] = 50;
            mods[StatType.HpPercent] = 10;
            mods[StatType.CritChancePercent] = 20;
            mods[StatType.CritDamagePercent] = 50;
            double expected = 50 * 1.2 / 0.5 * 1.5 * 10 * (1 + 0.2 * 0.5) + 400 * 1.1;
            Assert.AreEqual(expected, PowerSystem.ComputeUnitPower(stats, mods), 1e-6);
        }

        [Test]
        public void TeamPower_UsesWeights()
        {
            PlaceBasicTeam();

            Assert.AreEqual(2 * BaseUnit, Power.GuardianPower);
            Assert.AreEqual(BaseUnit, Power.EchoPower);
            Assert.AreEqual(2 * BaseUnit + EchoUnit + ShopHpPart, Power.TeamPower, "지킴이 ×1 + 덱 ×0.6 + 내구도 ×0.3");
        }

        [Test]
        public void EmptyTeam_IsZero_BecauseShopShareIsCapped_AndUnitsNeedOwnership()
        {
            Assert.AreEqual(0, Power.TeamPower, "빈 편성: 공격 몫 0 → 수선점 몫도 min(…, 0 × 1) = 0 (S5-24)");
            Assert.AreEqual(0, Power.GetUnitPower("sock_base"), "미보유 0");
            Assert.AreEqual(BaseUnit, Power.GetFormPower("sock_base"), "형태 능력치만 (도감 미리보기)");
            Assert.AreEqual(0, Power.GetLineagePower("sock"));
            Assert.AreEqual(0, Power.GetFormPower("nope"));

            Characters.Grant("sock");
            Assert.AreEqual(0, Power.GetUnitPower("sock_a"), "미해금 형태 0");
            Assert.AreEqual(BaseUnit, Power.GetUnitPower("sock_base"));
            Assert.AreEqual(BaseUnit, Power.GetLineagePower("sock"));
        }

        [Test]
        public void Synergy_RaisesMemberPower()
        {
            SynergyDb.synergies = new List<SynergyDef>
            {
                new SynergyDef
                {
                    id = "sock_candle", name = "양말과 촛불", kind = SynergyKind.Combo,
                    lineageIds = new List<string> { "sock", "candle" },
                    tiers = new List<SynergyTier>
                    {
                        new SynergyTier
                        {
                            count = 2, membersOnly = true,
                            modifiers = new List<StatModifier> { new StatModifier { stat = StatType.AttackPercent, value = 10 } },
                        },
                    },
                },
            };
            PlaceBasicTeam();
            Power.MarkDirty();

            Assert.AreEqual(210, Power.GetUnitPower("sock_base"), "공격 +10% → 110 + 100");
            Assert.AreEqual(BaseUnit, Power.GetUnitPower("button_base"), "구성원이 아니면 그대로");
            Assert.AreEqual(BaseUnit, Power.GetFormPower("sock_base"), "형태 전투력은 시너지 없이");
            Assert.AreEqual(420 + EchoUnit + ShopHpPart, Power.TeamPower);
        }

        [Test]
        public void SameLineageOnBothSides_AddsResonanceAttackBonus()
        {
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Echo, 0, "sock_a");
            float bonus = Formation.TeamAttackBonusPercent;
            Assert.Greater(bonus, 0f);

            long unit = (long)System.Math.Round(10 * (1 + bonus / 100.0) * 10 + 100);
            Assert.AreEqual(unit, Power.GetUnitPower("sock_base"), "편성 공명 공격력 배율은 전투와 같게");
        }

        [Test]
        public void PreviewPlace_AndLevelUp()
        {
            PlaceBasicTeam();
            long team = Power.TeamPower;

            Assert.AreEqual(team, Power.PreviewPlace(CharacterRole.Guardian, 1, "candle_base"), "같은 쪽에서 옮기면 합은 그대로");
            Assert.AreEqual(team, Power.PreviewPlace(CharacterRole.Guardian, 2, "candle_base"), "이미 그 칸");
            Assert.AreEqual(team, Power.PreviewPlace(CharacterRole.Guardian, 1, "nope"), "놓을 수 없으면 그대로");
            Assert.AreEqual(team, Power.PreviewPlace(CharacterRole.Echo, 1, "sock_base"), "반대편에 있는 형태는 놓을 수 없다");
            Assert.AreEqual(team, Power.TeamPower, "미리보기는 편성을 바꾸지 않는다");

            // 레벨 2: 공격·체력 +8% → 10.8×10 + 108 = 216
            Assert.AreEqual(16, Power.PreviewLevelUp("sock"));
            Assert.AreEqual(1, Data.lineages["sock"].level, "레벨은 그대로");
            Assert.AreEqual(0, Power.PreviewLevelUp("nope"));
        }

        [Test]
        public void PreviewPlace_EmptyDeckSlot_WithoutSynergy()
        {
            Characters.Grant("sock");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            long team = Power.TeamPower;

            Assert.AreEqual(team + EchoUnit, Power.PreviewPlace(CharacterRole.Echo, 1, "button_base"), "빈 덱 칸에 넣기 (덱 ×0.6)");
            Formation.TryPlace(CharacterRole.Echo, 1, "button_base");
            Assert.AreEqual(team + EchoUnit, Power.TeamPower);
        }

        /// <summary>미리보기 == 실제로 놓은 뒤 TeamPower, 편성 데이터·이벤트는 그대로 (M46)</summary>
        void AssertPreviewMatchesPlacement(CharacterRole role, int slot, string formId, string message)
        {
            int formationChanged = 0;
            void OnChanged() => formationChanged++;
            Formation.Changed += OnChanged;
            var guardians = (string[])Data.formation.guardians.Clone();
            var echoes = (string[])Data.formation.echoes.Clone();
            int placements = Formation.PlacementsDone;
            long before = Power.TeamPower;

            long preview = Power.PreviewPlace(role, slot, formId);

            Assert.AreEqual(0, formationChanged, message + ": 미리보기는 Changed를 울리지 않는다");
            CollectionAssert.AreEqual(guardians, Data.formation.guardians, message + ": 지킴이 되돌림");
            CollectionAssert.AreEqual(echoes, Data.formation.echoes, message + ": 덱 되돌림");
            Assert.AreEqual(placements, Formation.PlacementsDone, message + ": 직접 놓은 횟수는 세지 않는다");
            Assert.AreEqual(before, Power.TeamPower, message + ": 팀 전투력 캐시 그대로");

            Formation.Changed -= OnChanged;
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(role, slot, formId), message);
            Assert.AreEqual(Power.TeamPower, preview, message + ": 미리보기 = 놓은 뒤 팀 전투력");
        }

        static SynergyDef SockCandleCombo(float attackPercent) => new SynergyDef
        {
            id = "sock_candle", name = "양말과 촛불", kind = SynergyKind.Combo,
            lineageIds = new List<string> { "sock", "candle" },
            tiers = new List<SynergyTier>
            {
                new SynergyTier
                {
                    count = 2, membersOnly = true,
                    modifiers = new List<StatModifier> { new StatModifier { stat = StatType.AttackPercent, value = attackPercent } },
                },
            },
        };

        [Test]
        public void PreviewPlace_IncludesSynergyGained()
        {
            SynergyDb.synergies = new List<SynergyDef> { SockCandleCombo(50) };
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            long team = Power.TeamPower;
            Assert.AreEqual(BaseUnit + ShopHpPart, team);

            // candle이 들어오면 조합이 켜져 sock도 공격 +50% → 지킴이 각 (15×10 + 100) = 250
            long preview = Power.PreviewPlace(CharacterRole.Guardian, 2, "candle_base");
            Assert.AreEqual(2 * 250 + ShopHpPart, preview, "새로 켜지는 시너지까지 반영 (근사값 아님)");
            Assert.Greater(preview, team + BaseUnit, "옛 근사값(유닛만 더하기)보다 크다");
            AssertPreviewMatchesPlacement(CharacterRole.Guardian, 2, "candle_base", "시너지 켜짐");
        }

        [Test]
        public void PreviewPlace_IncludesSynergyLost()
        {
            SynergyDb.synergies = new List<SynergyDef> { SockCandleCombo(50) };
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");
            Assert.AreEqual(2 * 250 + ShopHpPart, Power.TeamPower);

            // candle 칸에 button을 올리면 candle이 빠져 조합이 꺼진다 → sock 200 + button 200
            long preview = Power.PreviewPlace(CharacterRole.Guardian, 2, "button_base");
            Assert.AreEqual(2 * BaseUnit + ShopHpPart, preview, "꺼지는 시너지까지 반영");
            AssertPreviewMatchesPlacement(CharacterRole.Guardian, 2, "button_base", "시너지 꺼짐");
        }

        [Test]
        public void PreviewPlace_IncludesLineageResonanceAndSwaps()
        {
            PlaceBasicTeam();
            Characters.UnlockForm("sock_a");

            // sock이 지킴이·잔향이 양쪽에 나가면 계보 공명으로 모든 유닛 공격력 배율이 오른다
            long preview = Power.PreviewPlace(CharacterRole.Echo, 1, "sock_a");
            Assert.Greater(preview, Power.TeamPower + EchoUnit, "공명 공격력 배율까지 반영");
            AssertPreviewMatchesPlacement(CharacterRole.Echo, 1, "sock_a", "공명 켜짐");

            // 같은 쪽 맞바꾸기 / 밀어내기도 TryPlace와 같은 규칙
            AssertPreviewMatchesPlacement(CharacterRole.Guardian, 2, "sock_base", "지킴이 맞바꾸기");
            AssertPreviewMatchesPlacement(CharacterRole.Echo, 0, "sock_a", "덱 맞바꾸기");
            AssertPreviewMatchesPlacement(CharacterRole.Echo, 3, "sock_a", "덱 빈칸으로 이동");
        }

        [Test]
        public void PreviewPlace_LockedEchoSlot_UsesSameRulesAsTryPlace()
        {
            Characters.Grant("button");
            Data.formation.echoes[5] = "button_base"; // 잠긴 칸에 남은 데이터 (FormationSystem은 무시한다)
            long team = Power.TeamPower;

            Assert.AreEqual(team, Power.PreviewPlace(CharacterRole.Echo, 5, "button_base"), "잠긴 칸은 놓을 수 없다");
            AssertPreviewMatchesPlacement(CharacterRole.Echo, 0, "button_base", "잠긴 칸 데이터가 있어도 빈칸에 넣기");
            Assert.AreEqual("button_base", Data.formation.echoes[5], "잠긴 칸 데이터는 건드리지 않는다");
        }

        /// <summary>
        /// 같은 (역할, 칸, 형태)를 다시 물으면 기억한 값 — 드래그 중 같은 칸 위에서 여러 번 불러도 다시 계산·할당하지 않는다 (M46).
        /// 다른 칸은 새로 계산하고, 전투력이 더러워지면(Characters/Formation/Synergy/Growth/Costumes.Changed) 잊는다.
        /// </summary>
        [Test]
        public void PreviewPlace_SameTargetIsCached_UntilPowerIsDirty()
        {
            SynergyDb.synergies = new List<SynergyDef> { SockCandleCombo(50) };
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            long levelOne = 2 * 250 + ShopHpPart;
            Assert.AreEqual(levelOne, Power.PreviewPlace(CharacterRole.Guardian, 2, "candle_base"));

            // 이벤트 없이 능력치만 바꾸면(실제로는 없는 경로) 같은 칸은 기억한 값 그대로 = 다시 계산하지 않았다
            Data.lineages["candle"].level = 5;
            Assert.AreEqual(levelOne, Power.PreviewPlace(CharacterRole.Guardian, 2, "candle_base"), "같은 칸 = 캐시");

            long otherSlot = Power.PreviewPlace(CharacterRole.Guardian, 3, "candle_base");
            Assert.Greater(otherSlot, levelOne, "다른 칸은 새로 계산 (레벨 5 촛불)");

            Characters.Grant("candle"); // 중복 획득 → Characters.Changed → 전투력 더러움
            long afterChange = Power.PreviewPlace(CharacterRole.Guardian, 2, "candle_base");
            Assert.AreEqual(otherSlot, afterChange, "더러워지면 잊고 새로 계산 (칸 위치는 전투력과 무관)");

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base"));
            Assert.AreEqual(afterChange, Power.TeamPower, "미리보기 = 놓은 뒤 팀 전투력");
            Assert.AreEqual(Power.TeamPower, Power.PreviewPlace(CharacterRole.Guardian, 2, "candle_base"), "놓은 뒤에는 이미 그 칸 → 지금 팀 전투력");
        }

        [Test]
        public void PreviewLevelUp_MatchesRealLevelUp_AndIsZeroWhenUnplacedOrMax()
        {
            Config.characterMaxLevel = 3;
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Currency.Add(CurrencyType.Gold, 1e9);

            Assert.AreEqual(0, Power.PreviewLevelUp("candle"), "편성에 없는 계보는 0 (카드 '미편성')");
            long before = Power.TeamPower;
            long preview = Power.PreviewLevelUp("sock");
            Assert.Greater(preview, 0);
            Assert.IsTrue(Characters.TryLevelUp("sock"));
            Assert.AreEqual(before + preview, Power.TeamPower, "미리보기 = 실제 증가");

            Assert.IsTrue(Characters.TryLevelUp("sock"));
            Assert.AreEqual(3, Characters.GetLevel("sock"));
            Assert.AreEqual(0, Power.PreviewLevelUp("sock"), "최대 레벨");
        }

        [Test]
        public void FormPower_IgnoresSynergy_ButFollowsLevel()
        {
            SynergyDb.synergies = new List<SynergyDef> { SockCandleCombo(50) };
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");

            Assert.AreEqual(BaseUnit, Power.GetFormPower("sock_base"), "편성 카드 PowerText = 시너지 없는 형태 전투력");
            Assert.AreEqual(250, Power.GetUnitPower("sock_base"));
            Data.lineages["sock"].level = 2;
            Assert.AreEqual(216, Power.GetFormPower("sock_base"), "레벨은 반영 (10.8×10 + 108)");
        }

        [Test]
        public void Recommended_AndRating()
        {
            Assert.AreEqual(300, Power.GetRecommendedPower(1));
            Assert.AreEqual(336, Power.GetRecommendedPower(2));
            Assert.AreEqual(376, Power.GetRecommendedPower(3), "300 × 1.12²");

            Assert.AreEqual(PowerRating.Comfortable, Power.GetRating(300, 300));
            Assert.AreEqual(PowerRating.Challenge, Power.GetRating(299, 300));
            Assert.AreEqual(PowerRating.Challenge, Power.GetRating(240, 300), "80%까지 도전");
            Assert.AreEqual(PowerRating.Hard, Power.GetRating(239, 300));
            Assert.AreEqual(PowerRating.Comfortable, Power.GetRating(1, 0));
            Assert.AreEqual("여유", PowerSystem.RatingName(PowerRating.Comfortable));
            Assert.AreEqual("도전", PowerSystem.RatingName(PowerRating.Challenge));
            Assert.AreEqual("어려움", PowerSystem.RatingName(PowerRating.Hard));

            MetaDb.power.recommendedBase = 1000;
            MetaDb.power.recommendedGrowth = 1.5;
            Assert.AreEqual(2250, Power.GetRecommendedPower(3), "MetaDatabase.power를 따른다");
            Assert.AreEqual((float)Power.TeamPower / 1000f, Power.GetRatio(1), 1e-5f);
        }

        [Test]
        public void Refresh_RaisesChangedOnlyOnRealChange_FirstIsBaseline()
        {
            var changes = new List<(long, long)>();
            Power.Changed += (a, b) => changes.Add((a, b));

            Power.Refresh();
            Assert.IsEmpty(changes, "첫 계산은 기준값");

            Characters.Grant("sock");
            Power.Refresh();
            Assert.IsEmpty(changes, "편성에 없으면 팀 전투력은 그대로");

            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Power.Refresh();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(0, changes[0].Item1, "빈 편성은 0 (수선점 몫 상한)");
            Assert.AreEqual(ShopHpPart + BaseUnit, changes[0].Item2);

            Power.Refresh();
            Assert.AreEqual(1, changes.Count, "깨끗하면 그대로");

            Currency.Add(CurrencyType.Gold, 1e9);
            Growth.TryLevelUp(10);
            Power.Refresh();
            Assert.AreEqual(2, changes.Count, "수선점 내구도가 오르면 바뀐다");
        }

        [Test]
        public void TaperedRecommended_ContinuousAtTaperStage()
        {
            MetaDb.power.recommendedBase = 1000;
            MetaDb.power.recommendedGrowth = 1.1;
            long before = Power.GetRecommendedPower(30);
            long early = Power.GetRecommendedPower(29);
            MetaDb.power.recommendedTaperStage = 30;
            MetaDb.power.recommendedGrowthLate = 1.05;

            Assert.AreEqual(before, Power.GetRecommendedPower(30), "T까지는 옛 식 그대로 (base × g^(T−1))");
            Assert.AreEqual(early, Power.GetRecommendedPower(29));
            Assert.AreEqual(System.Math.Round(1000 * System.Math.Pow(1.1, 29) * 1.05), Power.GetRecommendedPower(31), 1.0, "T → T+1 첫 걸음이 gL");
        }

        [Test]
        public void TaperedRecommended_UsesLateGrowthAfterTaper()
        {
            MetaDb.power.recommendedBase = 500;
            MetaDb.power.recommendedGrowth = 1.12;
            MetaDb.power.recommendedTaperStage = 10;
            MetaDb.power.recommendedGrowthLate = 1.04;

            double expected = 500 * System.Math.Pow(1.12, 9) * System.Math.Pow(1.04, 40);
            Assert.AreEqual(System.Math.Round(expected), Power.GetRecommendedPower(50), 1.0, "base × g^(T−1) × gL^(s−T)");
            Assert.Less(Power.GetRecommendedPower(50), (long)System.Math.Round(500 * System.Math.Pow(1.12, 49)), "완만해진다");

            MetaDb.power.recommendedGrowthLate = 0;
            Assert.AreEqual((long)System.Math.Round(500 * System.Math.Pow(1.12, 49)), Power.GetRecommendedPower(50), 1.0,
                "늦은 성장률이 비면(0) 앞 구간 성장률 그대로 — 숫자가 바뀌지 않는다");

            MetaDb.power.recommendedTaperStage = 0;
            MetaDb.power.recommendedGrowthLate = 1.01;
            Assert.AreEqual((long)System.Math.Round(500 * System.Math.Pow(1.12, 49)), Power.GetRecommendedPower(50), 1.0, "T = 0 → 지수 하나 (꺼짐)");
        }

        [Test]
        public void DungeonRecommended_IsOffAtZeroBase_AndRatesTheDeck()
        {
            Assert.AreEqual(0, Power.GetDungeonRecommended(1), "dungeonRecommendedBase 0 = 권장 줄 없음");
            Assert.AreEqual(0, Power.GetDungeonRecommended(30));

            MetaDb.power.dungeonRecommendedBase = 100;
            MetaDb.power.dungeonRecommendedGrowth = 1.2;
            Assert.AreEqual(100, Power.GetDungeonRecommended(1));
            Assert.AreEqual(100, Power.GetDungeonRecommended(0), "층은 1부터");
            Assert.AreEqual(144, Power.GetDungeonRecommended(3), "100 × 1.2²");

            Characters.Grant("sock");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Assert.AreEqual(BaseUnit, Power.EchoPower);

            MetaDb.power.dungeonRecommendedBase = BaseUnit;
            MetaDb.power.dungeonRecommendedGrowth = 1.0;
            Assert.AreEqual(PowerRating.Comfortable, Power.GetDungeonRating(5), "덱 200 / 권장 200 = 여유 (지킴이는 세지 않는다)");
            MetaDb.power.dungeonRecommendedBase = BaseUnit / 0.9;
            Assert.AreEqual(PowerRating.Challenge, Power.GetDungeonRating(5));
            MetaDb.power.dungeonRecommendedBase = BaseUnit * 2;
            Assert.AreEqual(PowerRating.Hard, Power.GetDungeonRating(5));
        }

        [Test]
        public void PreviewShopLevels_MatchesRealLevelUp_AndStopsAtCap()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 2, "candle_base");
            Currency.Add(CurrencyType.Gold, 1e12);

            Assert.AreEqual(0, Power.PreviewShopLevels(0));
            Assert.AreEqual(0, Power.PreviewShopLevels(-3));
            long before = Power.TeamPower;
            long preview = Power.PreviewShopLevels(10);
            Assert.Greater(preview, 0);
            Assert.AreEqual(before, Power.TeamPower, "미리보기는 그대로");
            Assert.AreEqual(1, Growth.Level);
            Assert.IsTrue(Growth.TryLevelUp(10));
            Assert.AreEqual(before + preview, Power.TeamPower, "미리보기 = 실제 증가");

            // 수선점 몫이 공격 몫을 넘으면 상한에 걸려 더 늘지 않는다
            MetaDb.power.shopHpWeight = 50f;
            Power.MarkDirty();
            Assert.AreEqual(2 * (2 * BaseUnit), Power.TeamPower, "공격 400 + 수선점 몫 min(…, 400)");
            Assert.AreEqual(0, Power.PreviewShopLevels(5), "상한에 걸린 수선점 레벨업은 전투력을 올리지 않는다");
        }

        [Test]
        public void PreviewAutoFormation_MatchesApply_AndGainIsNeverNegative()
        {
            SynergyDb.synergies = new List<SynergyDef> { SockCandleCombo(50) };
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            long team = Power.TeamPower;
            var guardians = (string[])Data.formation.guardians.Clone();
            var echoes = (string[])Data.formation.echoes.Clone();
            int changed = 0;
            void OnChanged() => changed++;
            Formation.Changed += OnChanged;

            long preview = Power.PreviewAutoFormation();
            long gain = Power.PreviewAutoFormationGain();

            Formation.Changed -= OnChanged;
            Assert.AreEqual(0, changed, "미리보기는 Changed를 울리지 않는다");
            CollectionAssert.AreEqual(guardians, Data.formation.guardians, "지킴이 그대로");
            CollectionAssert.AreEqual(echoes, Data.formation.echoes, "덱 그대로");
            Assert.AreEqual(team, Power.TeamPower);
            Assert.Greater(preview, team);
            Assert.AreEqual(preview - team, gain);

            Formation.AutoFormation();
            Assert.AreEqual(preview, Power.TeamPower, "미리보기 = 자동 편성 뒤 팀 전투력 (시너지 포함)");
            Assert.AreEqual(Power.TeamPower, Power.PreviewAutoFormation(), "이미 자동 편성 = 그대로");
            Assert.AreEqual(0, Power.PreviewAutoFormationGain());

            // 직접 더 센 편성을 만든 뒤에도 늘어날 값은 음수가 되지 않는다
            Formation.ClearAll();
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Assert.GreaterOrEqual(Power.PreviewAutoFormationGain(), 0);
        }

        [Test]
        public void PreviewAutoFormation_IsCachedUntilDirty()
        {
            Characters.Grant("sock");
            long first = Power.PreviewAutoFormation();
            Assert.AreEqual(BaseUnit + ShopHpPart, first);

            Characters.Grant("candle"); // Characters.Changed → 더러움 → 다시 고른다
            Assert.AreEqual(2 * BaseUnit + ShopHpPart, Power.PreviewAutoFormation());
            CollectionAssert.AreEqual(new string[FormationSystem.GuardianSlotCount], Data.formation.guardians, "세이브는 비어 있는 그대로");
        }

        [Test]
        public void Format_CommaUnderMillion()
        {
            Assert.AreEqual("12,345", PowerSystem.Format(12345));
            Assert.AreEqual("999,999", PowerSystem.Format(999_999));
            Assert.AreEqual(NumberFormatter.Format(1_234_567), PowerSystem.Format(1_234_567));
            Assert.AreEqual("0", PowerSystem.Format(0));
        }
    }
}
