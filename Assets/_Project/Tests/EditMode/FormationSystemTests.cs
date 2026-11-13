using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    public class FormationSystemTests : SystemsTestBase
    {
        int _changed;

        [SetUp]
        public void SetUpFormation()
        {
            _changed = 0;
            Formation.Changed += () => _changed++;
        }

        void GrantAll()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
        }

        [Test]
        public void Place_Ok_StoresFormAndNotifies()
        {
            Characters.Grant("sock");

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 2, "sock_base"));

            Assert.AreEqual("sock_base", Formation.GetGuardian(2));
            Assert.AreEqual("sock_base", Data.formation.guardians[2]);
            Assert.AreEqual(1, _changed);
            Assert.IsTrue(Formation.IsPlaced("sock_base", out var role, out var slot));
            Assert.AreEqual(CharacterRole.Guardian, role);
            Assert.AreEqual(2, slot);
            Assert.IsTrue(Formation.IsLineagePlaced("sock"));
        }

        [Test]
        public void Place_InvalidSlot()
        {
            Characters.Grant("sock");

            Assert.AreEqual(PlaceResult.InvalidSlot, Formation.TryPlace(CharacterRole.Guardian, -1, "sock_base"));
            Assert.AreEqual(PlaceResult.InvalidSlot, Formation.TryPlace(CharacterRole.Guardian, FormationSystem.GuardianSlotCount, "sock_base"));
            Assert.AreEqual(PlaceResult.InvalidSlot, Formation.TryPlace(CharacterRole.Echo, FormationSystem.EchoSlotCount, "sock_base"));
            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Place_LastEchoSlot_LockedUntilStage20()
        {
            Characters.Grant("button");
            Assert.AreEqual(5, Formation.UnlockedEchoSlots);
            // 최고 스테이지 20 도달 = 2-9 클리어 (M39: "클리어" 기준 문구로 통일)
            StringAssert.Contains("스테이지 2-9", Formation.EchoSlotUnlockHint);
            Assert.AreEqual("스테이지 2-9 클리어 시\n열려요", Formation.EchoSlotUnlockHint, "카드 자물쇠는 두 줄");
            Assert.AreEqual("스테이지 2-9를 클리어하면 열려요", Formation.EchoSlotUnlockSentence, "토스트 문장");
            Assert.AreEqual("잠긴 칸이에요. 스테이지 2-9를 클리어하면 열려요.", FormationSystem.Describe(PlaceResult.SlotLocked),
                "다른 Describe 문구처럼 마침표로 끝난다");
            // 아주 작은 자물쇠(전투 덱 잠금 알약)는 클리어 스테이지 코드만 — FormatStage(EchoSlotUnlockStage) = "2-10"이 아니다
            Assert.AreEqual("2-9", FormationSystem.EchoSlotUnlockCode);
            Assert.AreEqual(BattleSystem.FormatStage(MetaCondition.StageIndex(2, 9)), FormationSystem.EchoSlotUnlockCode,
                "FeatureId.EchoSlot6 조건(2-9 클리어)과 같은 스테이지");
            Assert.AreNotEqual(BattleSystem.FormatStage(FormationSystem.EchoSlotUnlockStage), FormationSystem.EchoSlotUnlockCode);
            StringAssert.Contains(FormationSystem.EchoSlotUnlockCode, Formation.EchoSlotUnlockSentence, "토스트와 같은 코드");

            Assert.AreEqual(PlaceResult.SlotLocked, Formation.TryPlace(CharacterRole.Echo, 5, "button_base"));

            Data.battle.highestStage = 20;
            Assert.AreEqual(6, Formation.UnlockedEchoSlots);
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Echo, 5, "button_base"));
            Assert.AreEqual("button_base", Formation.GetEcho(5));
        }

        [Test]
        public void Place_NotOwned()
        {
            Assert.AreEqual(PlaceResult.NotOwned, Formation.TryPlace(CharacterRole.Guardian, 0, "candle_base"));
            Assert.AreEqual(PlaceResult.NotOwned, Formation.TryPlace(CharacterRole.Guardian, 0, "no_such_form"));
            Assert.AreEqual(PlaceResult.NotOwned, Formation.TryPlace(CharacterRole.Guardian, 0, null));
            Assert.IsNull(Formation.GetGuardian(0));
        }

        [Test]
        public void Place_FormLocked_UntilEvolved()
        {
            Characters.Grant("sock");

            Assert.AreEqual(PlaceResult.FormLocked, Formation.TryPlace(CharacterRole.Echo, 0, "sock_a"));

            Characters.UnlockForm("sock_a");
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Echo, 0, "sock_a"));
        }

        [Test]
        public void Place_SameFormOnOtherSide_IsBlocked()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");

            Assert.AreEqual(PlaceResult.OnOtherSide, Formation.TryPlace(CharacterRole.Echo, 0, "sock_base"));
            Assert.AreEqual("지금은 직접 나가 있어요. 이미 반대편에 등록된 캐릭터예요.", FormationSystem.Describe(PlaceResult.OnOtherSide));
            Assert.IsNull(Formation.GetEcho(0));
        }

        [Test]
        public void Place_LineageLimit_OneGuardianFormAndOneEchoForm()
        {
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Characters.UnlockForm("sock_b");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");

            Assert.AreEqual(PlaceResult.LineageLimit, Formation.TryPlace(CharacterRole.Guardian, 1, "sock_a"));
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Echo, 0, "sock_a"));
            Assert.AreEqual(PlaceResult.LineageLimit, Formation.TryPlace(CharacterRole.Echo, 1, "sock_b"));

            // 같은 칸에 같은 계보의 다른 형태를 올리면 교체
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Echo, 0, "sock_b"));
            Assert.AreEqual("sock_b", Formation.GetEcho(0));
            Assert.IsFalse(Formation.IsPlaced("sock_a", out _, out _));
        }

        [Test]
        public void Place_DispatchedLineage_IsAllowed()
        {
            // M05: 원정과 편성은 따로 — 원정 중에도 편성할 수 있고 함께 싸운다
            Characters.Grant("candle");
            Data.expeditions.Add(new ExpeditionSave { zoneId = "exp_1_1", lineageIds = new List<string> { "candle" } });

            Assert.IsTrue(Formation.IsDispatched("candle"));
            Assert.AreEqual(PlaceResult.Ok, Formation.CanPlace(CharacterRole.Guardian, 0, "candle_base"));
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 0, "candle_base"));
            Assert.AreEqual("candle_base", Formation.GetGuardian(0));
            Assert.IsTrue(Formation.IsDispatched("candle"), "편성해도 원정은 그대로");
            Assert.IsNotEmpty(FormationSystem.Describe(PlaceResult.Dispatched), "열거값은 호환용으로 남는다");
        }

        [Test]
        public void MoveWithinSide_SwapsOrMoves()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 1, "candle_base");

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 1, "sock_base"));
            Assert.AreEqual("candle_base", Formation.GetGuardian(0));
            Assert.AreEqual("sock_base", Formation.GetGuardian(1));

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 3, "sock_base"));
            Assert.IsNull(Formation.GetGuardian(1));
            Assert.AreEqual("sock_base", Formation.GetGuardian(3));
        }

        [Test]
        public void Remove_ClearsSlotAndNotifies()
        {
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 2, "button_base");
            _changed = 0;

            Formation.Remove(CharacterRole.Echo, 2);
            Formation.Remove(CharacterRole.Echo, 2);
            Formation.Remove(CharacterRole.Echo, 99);

            Assert.IsNull(Formation.GetEcho(2));
            Assert.AreEqual(1, _changed);
        }

        [Test]
        public void AutoFormation_FollowsRules_AndPrefersRecommendedRole()
        {
            GrantAll();
            Characters.UnlockForm("sock_a");

            Formation.AutoFormation();

            var guardians = new List<string>();
            var echoes = new List<string>();
            for (int i = 0; i < FormationSystem.GuardianSlotCount; i++) if (Formation.GetGuardian(i) != null) guardians.Add(Formation.GetGuardian(i));
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++) if (Formation.GetEcho(i) != null) echoes.Add(Formation.GetEcho(i));

            CollectionAssert.AreEquivalent(new[] { "sock_base", "candle_base" }, guardians, "방어/저격 = 지킴이 추천");
            CollectionAssert.AreEquivalent(new[] { "sock_a", "button_base" }, echoes, "돌격 = 잔향이 추천");
            Assert.AreEqual(1, _changed);
        }

        [Test]
        public void AutoFormation_IncludesDispatchedLineage()
        {
            GrantAll();
            Data.expeditions.Add(new ExpeditionSave { zoneId = "exp_1_1", lineageIds = new List<string> { "sock" } });

            Formation.AutoFormation();

            Assert.IsTrue(Formation.IsLineagePlaced("sock"), "원정 중이어도 자동 편성에 들어간다 (M05)");
            Assert.IsTrue(Formation.IsLineagePlaced("candle"));
            Assert.IsTrue(Formation.IsLineagePlaced("button"));
        }

        [Test]
        public void PlacementsDone_CountsOnlyRealPlacements()
        {
            GrantAll();
            Assert.AreEqual(0, Formation.PlacementsDone, "새 세이브는 0");

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base"));
            Assert.AreEqual(1, Formation.PlacementsDone);
            Assert.AreEqual(1, Data.formation.placementsDone, "세이브에 바로 쓴다");

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base"));
            Assert.AreEqual(1, Formation.PlacementsDone, "이미 그 칸이면 세지 않는다");
            Formation.TryPlace(CharacterRole.Echo, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Guardian, 99, "candle_base");
            Assert.AreEqual(1, Formation.PlacementsDone, "실패는 세지 않는다");

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 3, "sock_base"));
            Assert.AreEqual(2, Formation.PlacementsDone, "같은 쪽 옮기기도 직접 놓기");

            Formation.Remove(CharacterRole.Guardian, 3);
            Formation.AutoFormation();
            Formation.ClearAll();
            Assert.AreEqual(2, Formation.PlacementsDone, "빼기·자동 편성·비우기는 세지 않는다");
        }

        [Test]
        public void PlacementsDone_ChangedListenersSeeNewCount()
        {
            Characters.Grant("sock");
            int seen = -1;
            Formation.Changed += () => seen = Formation.PlacementsDone;

            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");

            Assert.AreEqual(1, seen, "Changed를 받는 쪽(안내 줄)이 늘어난 값을 본다");
        }

        [Test]
        public void FormationSave_PlacementsDone_DefaultsAndSanitize()
        {
            var old = Newtonsoft.Json.JsonConvert.DeserializeObject<FormationSave>("{\"guardians\":[null,null,null,null,null],\"echoes\":[]}");
            old.Sanitize();
            Assert.AreEqual(0, old.placementsDone, "필드가 없는 옛 세이브는 0");
            Assert.AreEqual(FormationSave.EchoSlots, old.echoes.Length);

            var broken = new FormationSave { placementsDone = -5 };
            broken.Sanitize();
            Assert.AreEqual(0, broken.placementsDone);
        }

        /// <summary>BuildAutoFormation(미리보기) = AutoFormation이 쓰는 편성, 고르기만 하고 세이브 · 이벤트는 그대로 (RM-2 P4-1, PowerSystem.PreviewAutoFormation)</summary>
        [Test]
        public void AutoFormation_PreviewMatchesApply()
        {
            GrantAll();
            Characters.UnlockForm("sock_a");
            Characters.UnlockForm("candle_a");
            Data.battle.highestStage = FormationSystem.EchoSlotUnlockStage; // 덱 6칸
            Formation.TryPlace(CharacterRole.Echo, 3, "candle_base");
            int changedBefore = _changed;
            int placements = Formation.PlacementsDone;
            var savedGuardians = (string[])Data.formation.guardians.Clone();
            var savedEchoes = (string[])Data.formation.echoes.Clone();

            var guardians = new string[FormationSystem.GuardianSlotCount];
            var echoes = new string[FormationSystem.EchoSlotCount];
            guardians[1] = "junk";
            Formation.BuildAutoFormation(guardians, echoes);

            Assert.AreEqual(changedBefore, _changed, "미리보기는 Changed를 울리지 않는다");
            CollectionAssert.AreEqual(savedGuardians, Data.formation.guardians, "세이브 지킴이 그대로");
            CollectionAssert.AreEqual(savedEchoes, Data.formation.echoes, "세이브 덱 그대로");
            CollectionAssert.DoesNotContain(guardians, "junk", "배열은 비우고 채운다");

            Formation.AutoFormation();
            for (int i = 0; i < FormationSystem.GuardianSlotCount; i++) Assert.AreEqual(guardians[i], Formation.GetGuardian(i), $"지킴이 {i}");
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++) Assert.AreEqual(echoes[i], Formation.GetEcho(i), $"덱 {i}");
            Assert.AreEqual(changedBefore + 1, _changed);
            Assert.AreEqual(placements, Formation.PlacementsDone, "자동 편성은 직접 놓기로 세지 않는다");

            // 이미 자동 편성이면 다시 눌러도 같고 Changed 없음
            Formation.BuildAutoFormation(guardians, echoes);
            Formation.AutoFormation();
            Assert.AreEqual(changedBefore + 1, _changed);
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++) Assert.AreEqual(echoes[i], Formation.GetEcho(i));
        }

        [Test]
        public void BuildAutoFormation_IgnoresShortOrNullArrays()
        {
            GrantAll();
            Assert.DoesNotThrow(() => Formation.BuildAutoFormation(null, new string[FormationSystem.EchoSlotCount]));
            var shortGuardians = new string[2];
            Assert.DoesNotThrow(() => Formation.BuildAutoFormation(shortGuardians, new string[FormationSystem.EchoSlotCount]));
            CollectionAssert.AreEqual(new string[2], shortGuardians, "짧은 배열은 건드리지 않는다");

            var echoes = new string[FormationSystem.EchoSlotCount];
            Formation.BuildAutoFormation(new string[FormationSystem.GuardianSlotCount], echoes);
            Assert.IsNull(echoes[FormationSystem.EchoSlotCount - 1], "잠긴 마지막 덱 칸은 비운다 (2-9 전)");
        }

        [Test]
        public void AutoFormation_SingleCharacter_GoesToRecommendedSide()
        {
            // 돌격형 기본형 → 잔향이 추천
            Characters.Grant("button");

            Formation.AutoFormation();

            Assert.IsTrue(Formation.IsPlaced("button_base", out var role, out _));
            Assert.AreEqual(CharacterRole.Echo, role);
        }

        [Test]
        public void Synergy_WhenLineageOnBothSides()
        {
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Assert.IsEmpty(Formation.GetSynergies());
            Assert.AreEqual(0f, Formation.TeamAttackBonusPercent);

            Formation.TryPlace(CharacterRole.Echo, 0, "sock_a");
            var synergies = Formation.GetSynergies();

            Assert.AreEqual(1, synergies.Count);
            Assert.AreEqual("sock", synergies[0].Lineage.id);
            Assert.AreEqual(0, synergies[0].ResonanceLevel);
            Assert.AreEqual("양말 계보 공명 Lv.0", synergies[0].Label);
            Assert.AreEqual(FormationSystem.SynergyBaseBonusPercent, Formation.TeamAttackBonusPercent, 1e-4);

            Data.lineages["sock"].resonance = 2;
            synergies = Formation.GetSynergies();
            Assert.AreEqual("양말 계보 공명 Lv.2", synergies[0].Label);
            float expected = FormationSystem.SynergyBaseBonusPercent + FormationSystem.SynergyBonusPerResonance * 2;
            Assert.AreEqual(expected, synergies[0].AttackBonusPercent, 1e-4);
            Assert.AreEqual(expected, Formation.TeamAttackBonusPercent, 1e-4);
        }

        [Test]
        public void Describe_HasKoreanTextForEveryResult()
        {
            foreach (PlaceResult result in System.Enum.GetValues(typeof(PlaceResult)))
            {
                Assert.IsNotEmpty(FormationSystem.Describe(result), result.ToString());
                StringAssert.DoesNotContain("개방", FormationSystem.Describe(result), "플레이어 문구에 '개방'을 쓰지 않는다 (M39)");
            }
            StringAssert.DoesNotContain("개방", Formation.EchoSlotUnlockHint);
        }
    }
}
