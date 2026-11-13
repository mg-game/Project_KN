using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 강해지는 방법 (PRG-03, SPEC4 §13.2): 점수 순위 · 자동 편성 +5% 조건 · 한 명 모자란 시너지 · 음수 없음 · 최대 4줄 · 빈 경우 · 안내 줄은 뒤 ·
    /// 약한 방향(CR-01 LastDefeat) · 줄 글의 수(Amount) · 자리 줄의 쪽과 세울 형태(Role · CandidateId).
    /// </summary>
    public class StrengthenAdvisorTests : MetaSystemsTestBase
    {
        readonly List<StrengthenRow> _rows = new();

        [SetUp]
        public void ClearRows()
        {
            _rows.Clear();
            LastDefeat.Clear(); // 정적 기록 — 다른 테스트에 새지 않게
        }

        [TearDown]
        public void ClearLastDefeat() => LastDefeat.Clear();

        StrengthenRow Find(StrengthenKind kind) => _rows.Find(r => r.Kind == kind);

        static SynergyDef SockCandle() => new SynergyDef
        {
            id = "sock_candle", name = "양말과 촛불", kind = SynergyKind.Combo, lineageIds = new List<string> { "sock", "candle" },
            tiers = new List<SynergyTier>
            {
                new SynergyTier { count = 2, membersOnly = true, modifiers = new List<StatModifier> { new StatModifier { stat = StatType.AttackPercent, value = 50 } } },
            },
        };

        [Test]
        public void Empty_WhenNothingToDo()
        {
            Strengthen.GetRows(_rows);
            Assert.IsEmpty(_rows, "친구가 없으면 줄이 없다 (빈 편성 전투력 0 → 수선점 몫도 0)");
            Assert.DoesNotThrow(() => Strengthen.GetRows(null));
            Strengthen.GetRows(_rows, 0);
            Assert.IsEmpty(_rows);
        }

        [Test]
        public void AutoFormation_OnlyAtFivePercentOrMore()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.AutoFormation();
            Strengthen.GetRows(_rows, 10);
            Assert.IsNull(Find(StrengthenKind.AutoFormation), "이미 자동 편성 = 줄 없음");

            Formation.Remove(CharacterRole.Echo, 0); // 덱 120 빠짐 → 자동 편성이 +120 (≥ 5%)
            long team = Power.TeamPower;
            Strengthen.GetRows(_rows, 10);
            var auto = Find(StrengthenKind.AutoFormation);
            Assert.IsNotNull(auto);
            Assert.AreEqual(Power.PreviewAutoFormation() - team, auto.Gain);
            Assert.GreaterOrEqual(Power.PreviewAutoFormation(), 1.05 * team);
            Assert.AreEqual(FeatureId.Formation, auto.Nav);
            Assert.AreEqual(StrengthenAdvisor.FormationAutoPath, auto.Focus);
        }

        [Test]
        public void AutoFormation_BelowFivePercent_IsHidden()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.AutoFormation();
            MetaDb.power.shopHpWeight = 1000f; // 상한 없이 수선점 몫을 아주 크게 → 덱 한 칸 차이가 5% 아래
            MetaDb.power.shopShareCap = 0f;
            Power.MarkDirty();
            Formation.Remove(CharacterRole.Echo, 0);
            long team = Power.TeamPower;
            Assert.Less(Power.PreviewAutoFormation(), 1.05 * team, "차이가 5% 아래");
            Strengthen.GetRows(_rows, 10);
            Assert.IsNull(Find(StrengthenKind.AutoFormation));
        }

        [Test]
        public void SynergyOneShort_Detected()
        {
            SynergyDb.synergies = new List<SynergyDef> { SockCandle() };
            Characters.Grant("sock");
            Characters.Grant("candle");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Power.MarkDirty();

            Strengthen.GetRows(_rows, 10);

            var row = Find(StrengthenKind.SynergyOneShort);
            Assert.IsNotNull(row, "촛불 한 명만 더 세우면 조합이 켜진다");
            Assert.AreEqual("sock_candle", row.TargetId);
            Assert.AreEqual(Power.PreviewPlace(CharacterRole.Guardian, 1, "candle_base") - Power.TeamPower, row.Gain, "빈 칸에 넣은 미리보기");
            Assert.AreEqual(CharacterRole.Guardian, row.Role, "고른 자리의 쪽 (지킴이 1번 칸)");
            Assert.AreEqual("candle_base", row.CandidateId, "'{이름}{을/를} 세우면 켜져요'의 세울 형태");
        }

        [Test]
        public void FillSlot_CarriesTheRoleAndTheForm_AndReusedRowsForgetThem()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Power.MarkDirty();

            Strengthen.GetRows(_rows, 20);

            var guardian = _rows.Find(r => r.Kind == StrengthenKind.FillSlot && r.Role == CharacterRole.Guardian);
            var echo = _rows.Find(r => r.Kind == StrengthenKind.FillSlot && r.Role == CharacterRole.Echo);
            Assert.IsNotNull(guardian, "'빈 지킴이 자리 채우기' (지킴이 1번 칸이 비었다)");
            Assert.IsNotNull(echo, "'빈 잔향이 자리 채우기' (덱이 비었다)");
            long team = Power.TeamPower;
            Assert.IsNotNull(guardian.CandidateId);
            Assert.AreEqual(guardian.TargetId, guardian.CandidateId, "세울 형태 = 대상 형태");
            Assert.AreEqual(Power.PreviewPlace(CharacterRole.Guardian, 1, guardian.CandidateId) - team, guardian.Gain, "그 쪽의 첫 빈 칸에 그 형태를 세운 미리보기");
            Assert.IsNotNull(echo.CandidateId);
            Assert.AreEqual(echo.TargetId, echo.CandidateId);
            Assert.AreEqual(Power.PreviewPlace(CharacterRole.Echo, 0, echo.CandidateId) - team, echo.Gain);

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 1, "candle_base"));
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Echo, 0, "button_base"));
            Power.MarkDirty();
            Strengthen.GetRows(_rows, 20);
            Assert.IsNull(Find(StrengthenKind.FillSlot), "세울 친구가 남지 않으면 빈 자리 줄도 없다");
            Assert.IsNotEmpty(_rows);
            foreach (var row in _rows)
            {
                Assert.IsNull(row.CandidateId, "다시 쓴 줄에 지난 세울 형태가 남지 않는다");
                Assert.AreEqual(CharacterRole.Guardian, row.Role, "자리 줄이 아니면 기본값");
            }
        }

        [Test]
        public void Rows_RankedByScore_NoNegative_AtMostFour_InfoRowsLast()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Currency.Add(CurrencyType.Gold, 1e6);
            // 진화 가능하지만 편성에 없는 계보 → 안내 줄 (전투력 0)
            Data.lineages["candle"].level = 5;
            Inventory.Add("m_cloth", 1);
            Assert.IsTrue(Evolution.Check("candle_a").CanCraft);
            Power.MarkDirty();

            Strengthen.GetRows(_rows, 10);

            Assert.Greater(_rows.Count, 4);
            bool seenInfo = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                Assert.GreaterOrEqual(_rows[i].Gain, 0, "음수 줄 없음");
                if (_rows[i].Gain == 0) seenInfo = true;
                else Assert.IsFalse(seenInfo, "전투력 줄이 안내 줄보다 먼저");
                if (i > 0 && _rows[i].Gain > 0)
                    Assert.GreaterOrEqual(StrengthenAdvisor.Score(_rows[i - 1]), StrengthenAdvisor.Score(_rows[i]), "점수 큰 순");
            }
            var evolution = Find(StrengthenKind.EvolutionReady);
            Assert.IsNotNull(evolution);
            Assert.AreEqual(0, evolution.Gain, "편성에 없는 계보의 진화는 안내 줄");
            Assert.AreEqual("candle_a", evolution.TargetId);

            Strengthen.GetRows(_rows);
            Assert.AreEqual(4, _rows.Count, "기본 최대 4줄");
        }

        [Test]
        public void Score_GainOverOnePlusCostMinutes()
        {
            Assert.AreEqual(100, StrengthenAdvisor.Score(new StrengthenRow { Gain = 100 }), 1e-9);
            Assert.AreEqual(75, StrengthenAdvisor.Score(new StrengthenRow { Gain = 150, CostSeconds = 60 }), 1e-9, "150 / (1 + 1)");
            Assert.AreEqual(0, StrengthenAdvisor.Score(null));
        }

        [Test]
        public void Rows_AreReusedFromTheList_AndFullyRewritten()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            LastDefeat.Record(12, 2, 61);
            Strengthen.GetRows(_rows, 10);
            Assert.IsNotNull(Find(StrengthenKind.WeakDirection));
            var first = new List<StrengthenRow>(_rows);

            LastDefeat.Clear();
            Strengthen.GetRows(_rows, 10);
            Assert.IsNotEmpty(_rows);
            Assert.LessOrEqual(_rows.Count, first.Count);
            foreach (var row in _rows)
            {
                Assert.IsTrue(first.Contains(row), "목록에 있던 줄 객체를 다시 쓴다 (1초마다 새 줄을 만들지 않는다)");
                Assert.AreEqual(-1, row.Slot, "다시 쓴 줄도 값을 모두 새로 쓴다 (약한 방향 칸이 남지 않는다)");
                Assert.AreNotEqual(StrengthenKind.WeakDirection, row.Kind);
            }
        }

        [Test]
        public void TinyGains_AreDropped_AndLockedLevelUpHasNoRow()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Characters.FeatureResolver = f => f != FeatureId.CharacterLevelUp;
            Strengthen.GetRows(_rows, 10);
            Assert.IsNull(Find(StrengthenKind.LineageLevel), "친구 레벨업이 잠기면 줄 없음");

            long team = Power.TeamPower;
            foreach (var row in _rows)
                if (row.Gain > 0) Assert.GreaterOrEqual(row.Gain, (long)System.Math.Ceiling(team * StrengthenAdvisor.MinGainRatio), "0.5% 미만은 뺀다");
        }

        [Test]
        public void LevelAndShopRows_UsePreviewsAndCostSeconds()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Strengthen.GetRows(_rows, 10);

            var level = Find(StrengthenKind.LineageLevel);
            Assert.IsNotNull(level);
            Assert.AreEqual("sock", level.TargetId);
            Assert.AreEqual(Power.PreviewLevelUp("sock", 1), level.Gain, "단추가 없으면 +1 레벨 미리보기");
            Assert.Greater(level.CostSeconds, 0, "모으는 시간");

            var shop = Find(StrengthenKind.ShopUpgrade);
            Assert.IsNotNull(shop);
            Assert.Greater(shop.Gain, 0);
            Assert.AreEqual(FeatureId.ShopUpgrade, shop.Nav);
        }

        [Test]
        public void Amount_CarriesTheLevelCounts_ForTheRowText()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Strengthen.GetRows(_rows, 10);
            Assert.AreEqual(1, Find(StrengthenKind.LineageLevel).Amount, "단추가 없으면 +1 레벨");
            var shop = Find(StrengthenKind.ShopUpgrade);
            Assert.AreEqual(Power.PreviewShopLevels(shop.Amount), shop.Gain, "수선점 {n}번 강화의 n = 미리보기한 레벨 수");
            Assert.That(shop.Amount, Is.InRange(1, StrengthenAdvisor.MaxShopLevels));
            Assert.AreEqual(-1, shop.Slot, "방향 칸은 약한 방향 줄만");

            Currency.Add(CurrencyType.Gold, 1e9);
            Strengthen.GetRows(_rows, 10);
            var level = Find(StrengthenKind.LineageLevel);
            Assert.AreEqual(StrengthenAdvisor.LineageLevels, level.Amount, "살 수 있으면 +5");
            Assert.AreEqual(Power.PreviewLevelUp("sock", StrengthenAdvisor.LineageLevels), level.Gain);
            Assert.AreEqual(StrengthenAdvisor.MaxShopLevels, Find(StrengthenKind.ShopUpgrade).Amount, "단추가 넉넉하면 10번");
        }

        [Test]
        public void WeakDirection_ShownAfterAFailWithData_GoneAfterClear()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Strengthen.GetRows(_rows, 10);
            Assert.IsNull(Find(StrengthenKind.WeakDirection), "실패 기록이 없으면 줄 없음");

            LastDefeat.Record(12, 2, 61); // 2-2 실패, 왼쪽 아래가 61%
            Strengthen.GetRows(_rows, 10);
            var weak = Find(StrengthenKind.WeakDirection);
            Assert.IsNotNull(weak);
            Assert.AreEqual("왼쪽 아래", weak.TargetId, "'약한 방향 보강 · {방향}'");
            Assert.AreEqual(2, weak.Slot, "편성 칸 반짝 (UINav.PulseFormationSlot)");
            Assert.AreEqual(61, weak.Amount);
            Assert.AreEqual(0, weak.Gain, "안내 줄");
            Assert.AreEqual(0, weak.CostSeconds);
            Assert.AreEqual(FeatureId.Formation, weak.Nav);
            Assert.IsNull(weak.Focus);

            LastDefeat.Record(12, -1, 0); // 자료 없는 실패
            Strengthen.GetRows(_rows, 10);
            Assert.IsNull(Find(StrengthenKind.WeakDirection));

            LastDefeat.Record(12, 4, 40);
            LastDefeat.Clear(); // 클리어하면 전투 화면이 비운다
            Strengthen.GetRows(_rows, 10);
            Assert.IsNull(Find(StrengthenKind.WeakDirection));
        }

        [Test]
        public void WeakDirection_IsAnInfoRow_BetweenEvolutionAndMissingMaterial()
        {
            Characters.Grant("candle");
            Data.lineages["candle"].level = 5; // candle_a: m_cloth ×1 → 진화 가능 (편성에 없음 = 안내 줄)
            Inventory.Add("m_cloth", 1);
            Characters.Grant("sock");
            Data.lineages["sock"].level = 5; // sock_a(m_cloth ×3) · sock_b(m_thread ×1) 단서 — 재료 한 종만 모자람
            Assert.IsTrue(Evolution.Check("candle_a").CanCraft);
            LastDefeat.Record(5, 0, 100);

            Strengthen.GetRows(_rows, 20);

            int evolution = _rows.FindIndex(r => r.Kind == StrengthenKind.EvolutionReady && r.Gain == 0);
            int weak = _rows.FindIndex(r => r.Kind == StrengthenKind.WeakDirection);
            int missing = _rows.FindIndex(r => r.Kind == StrengthenKind.MissingMaterial);
            Assert.GreaterOrEqual(evolution, 0);
            Assert.Greater(weak, evolution, "진화 가능 → 약한 방향");
            Assert.Greater(missing, weak, "약한 방향 → 모자란 재료");
            Assert.AreEqual("위쪽", _rows[weak].TargetId);
            for (int i = 0; i < weak; i++)
                if (_rows[i].Gain == 0) Assert.AreNotEqual(StrengthenKind.MissingMaterial, _rows[i].Kind);
            for (int i = weak + 1; i < _rows.Count; i++) Assert.AreEqual(0, _rows[i].Gain, "전투력 줄은 모두 앞");

            Strengthen.GetRows(_rows, 1);
            Assert.AreEqual(1, _rows.Count, "최대 줄 수를 넘지 않는다");
        }
    }
}
