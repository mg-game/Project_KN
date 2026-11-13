using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    /// <summary>
    /// 곡선 패키지 P4-C (PROGRESSION-SIM §0 · §8, PHASE4-ROADMAP RD-1 / 델타 1, SPEC5 §4.1~4.2) — 데이터만 바꾼다.
    ///   · 적 성장 두 번째 완만 구간: BattleConfig.stageGrowthTaperStage2 151 · 체력 ×1.02 · 피해 ×1.015.
    ///     첫 완만 구간(30)과 같은 규칙이라 151스테이지 값까지는 예전 곡선 그대로 끊김 없이 이어지고(150 → 151은 ×1.07 / ×1.05),
    ///     151 → 152가 첫 ×1.02 / ×1.015 걸음이다. 그 뒤 모든 걸음이 같다.
    ///   · 1~300스테이지가 시뮬레이션 하네스(scratchpad/phase4b/sim)가 돌린 곡선과 같다: 걸음마다 곱하는 독립 계산 +
    ///     하네스가 찍어 둔 값(sim/golden/curve_harness_t151_102_1015.csv, 패치한 BattleConfig에 훅 151 · 1.02 · 1.015).
    ///   · stageGrowthTaperStage2 = 0이면 오늘(두 구간) 곡선 그대로 (sim/golden/curve_today_taper2_off.csv).
    ///   · 에셋이 같은 곡선을 쓴다: Data/BattleConfig.asset (IdleDataBuilder는 적 목록만 덮어쓰고 성장 수치는 에셋 값을 유지한다).
    ///   · CR-07 경제 재조정: 수선점 초당 단추 성장 1.10 → 1.06 (C# 기본값과 Data/GameConfig.asset 둘 다),
    ///     수선 바구니 스테이지 배율 1.02 → 1.0025 (코드 원본 MetaContent.IdleChest).
    /// 이 수치를 바꾸면 하네스를 다시 돌린다 (PROGRESSION-SIM §8.5).
    /// </summary>
    public class CurvePackageTests
    {
        const string BattleAssetPath = "Assets/_Project/Data/BattleConfig.asset";
        const string ConfigAssetPath = "Assets/_Project/Data/GameConfig.asset";
        /// <summary>상대 오차 — float 반올림(≈6e-8) 몇 번 몫. 런타임마다 Math.Pow 마지막 자리가 달라도 넉넉하다</summary>
        const double Tolerance = 1e-6;

        /// <summary>하네스 곡선 { 스테이지, 체력 배율, 피해 배율 } — curve_harness_t151_102_1015.csv에서 그대로 옮김</summary>
        static readonly (int stage, float hp, float damage)[] HarnessCurve =
        {
            (1, 1f, 1f),
            (2, 1.11f, 1.08f),
            (29, 18.579908f, 8.627116f),
            (30, 20.6237f, 9.317286f),
            (31, 22.067358f, 9.78315f),
            (100, 2350.8909f, 283.49078f),
            (149, 64720.016f, 3096.0906f),
            (150, 69250.42f, 3250.895f),
            (151, 74097.95f, 3413.4395f),
            (152, 75579.914f, 3464.641f),
            (153, 77091.51f, 3516.6106f),
            (160, 88553.9f, 3902.892f),
            (200, 195530.38f, 7079.914f),
            (250, 526286.75f, 14904.925f),
            (299, 1388770.4f, 30914.736f),
            (300, 1416545.8f, 31378.457f),
        };

        /// <summary>오늘 곡선(두 번째 완만 구간 없음) { 스테이지, 체력 배율, 피해 배율 } — curve_today_taper2_off.csv</summary>
        static readonly (int stage, float hp, float damage)[] TodayCurve =
        {
            (151, 74097.95f, 3413.4395f),
            (152, 79284.81f, 3584.1113f),
            (200, 2039916.4f, 37279.227f),
            (300, 1.7700774E+09f, 4902243f),
        };

        BattleConfig _battle;

        [SetUp]
        public void SetUp()
        {
            _battle = ScriptableObject.CreateInstance<BattleConfig>();
            _battle.ApplyDefaults();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_battle);

        [Test]
        public void Defaults_ArePackageConstants()
        {
            Assert.AreEqual(151, _battle.stageGrowthTaperStage2, "16-1부터 (저작 챕터 1~15 뒤)");
            Assert.AreEqual(1.02f, _battle.stageHpGrowthLate2);
            Assert.AreEqual(1.015f, _battle.stageDamageGrowthLate2);
            // 하네스 곡선이 깔고 있는 앞 두 구간 (바뀌지 않음)
            Assert.AreEqual(1.11f, _battle.stageHpGrowth);
            Assert.AreEqual(1.08f, _battle.stageDamageGrowth);
            Assert.AreEqual(30, _battle.stageGrowthTaperStage);
            Assert.AreEqual(1.07f, _battle.stageHpGrowthLate);
            Assert.AreEqual(1.05f, _battle.stageDamageGrowthLate);
        }

        [Test]
        public void SecondTaper_ContinuousAt150To151_ThenSteps102And1015()
        {
            for (int s = 1; s <= 151; s++)
            {
                Assert.AreEqual(TwoSegment(_battle.stageHpGrowth, _battle.stageHpGrowthLate, s), _battle.GetStageHpScale(s), $"체력 {s}: 151까지는 오늘 곡선 그대로");
                Assert.AreEqual(TwoSegment(_battle.stageDamageGrowth, _battle.stageDamageGrowthLate, s), _battle.GetStageDamageScale(s), $"피해 {s}");
            }
            AssertStep(150, 1.07f, 1.05f); // 첫 완만 구간의 마지막 걸음 — 끊김 없음
            for (int s = 151; s < 300; s++) AssertStep(s, 1.02f, 1.015f); // 151 → 152부터 두 번째 완만 구간
        }

        [Test]
        public void SecondTaper_FollowsTheField()
        {
            _battle.stageGrowthTaperStage2 = 61;
            _battle.stageHpGrowthLate2 = 1.04f;
            _battle.stageDamageGrowthLate2 = 1.03f;
            Assert.AreEqual(TwoSegment(_battle.stageHpGrowth, _battle.stageHpGrowthLate, 61), _battle.GetStageHpScale(61));
            AssertStep(60, 1.07f, 1.05f);
            AssertStep(61, 1.04f, 1.03f);
            AssertStep(120, 1.04f, 1.03f);

            _battle.stageGrowthTaperStage2 = 20; // 첫 완만 구간보다 앞이면 30부터 바로 두 번째 배율
            AssertStep(29, 1.11f, 1.08f);
            AssertStep(30, 1.04f, 1.03f);
        }

        [Test]
        public void MatchesHarnessCurve_Stages1To300()
        {
            for (int s = 1; s <= 300; s++)
            {
                AssertClose(PerStep(s, 1.11f, 1.07f, 1.02f), _battle.GetStageHpScale(s), $"체력 {s}");
                AssertClose(PerStep(s, 1.08f, 1.05f, 1.015f), _battle.GetStageDamageScale(s), $"피해 {s}");
            }
            foreach (var (stage, hp, damage) in HarnessCurve)
            {
                AssertClose(hp, _battle.GetStageHpScale(stage), $"하네스 체력 {stage}");
                AssertClose(damage, _battle.GetStageDamageScale(stage), $"하네스 피해 {stage}");
            }
        }

        [Test]
        public void TaperStage2Zero_ReproducesTodaysCurve()
        {
            _battle.stageGrowthTaperStage2 = 0;
            _battle.stageHpGrowthLate2 = 5f; // 꺼져 있으면 두 번째 배율은 읽지 않는다
            _battle.stageDamageGrowthLate2 = 5f;
            foreach (int s in new[] { -5, 0, 1000, 2000, 100000, int.MaxValue }) AssertToday(s);
            for (int s = 1; s <= 300; s++) AssertToday(s);
            foreach (var (stage, hp, damage) in TodayCurve)
            {
                AssertClose(hp, _battle.GetStageHpScale(stage), $"오늘 체력 {stage}");
                AssertClose(damage, _battle.GetStageDamageScale(stage), $"오늘 피해 {stage}");
            }

            _battle.stageGrowthTaperStage2 = -1; // 음수도 꺼짐
            AssertToday(300);
        }

        [Test]
        public void StaysFinite_CappedAt1e30()
        {
            foreach (int s in new[] { 1000, 3000, 5000, 100000, int.MaxValue })
            {
                foreach (float scale in new[] { _battle.GetStageHpScale(s), _battle.GetStageDamageScale(s) })
                {
                    Assert.IsFalse(float.IsNaN(scale) || float.IsInfinity(scale), $"스테이지 {s}");
                    Assert.LessOrEqual(scale, (float)1e30, $"스테이지 {s}");
                    Assert.GreaterOrEqual(scale, 1f, $"스테이지 {s}");
                }
            }
            Assert.AreEqual(1f, _battle.GetStageHpScale(0), "0 이하 스테이지는 1스테이지로 본다");
            Assert.AreEqual(1f, _battle.GetStageDamageScale(-3));
        }

        [Test]
        public void BattleConfigAsset_UsesThePackageCurve()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BattleConfig>(BattleAssetPath);
            if (asset == null) Assert.Ignore(BattleAssetPath + " 없음 (데이터를 아직 만들지 않은 프로젝트)");
            Assert.AreEqual(151, asset.stageGrowthTaperStage2, "에셋에 필드가 없으면 C# 기본값을 읽고, 데이터를 다시 만들면 그 값으로 저장된다");
            Assert.AreEqual(1.02f, asset.stageHpGrowthLate2);
            Assert.AreEqual(1.015f, asset.stageDamageGrowthLate2);
            for (int s = 1; s <= 300; s++)
            {
                Assert.AreEqual(_battle.GetStageHpScale(s), asset.GetStageHpScale(s), $"체력 {s}: 에셋 성장 수치가 하네스 곡선과 다르다");
                Assert.AreEqual(_battle.GetStageDamageScale(s), asset.GetStageDamageScale(s), $"피해 {s}");
            }
        }

        [Test]
        public void ShopGoldGrowth_Is106_InCodeAndAsset()
        {
            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(1.06, fresh.goldPerSecondGrowth, 1e-12, "C# 기본값 (새 에셋 · 시험 · 하네스)");
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            if (asset == null) Assert.Ignore(ConfigAssetPath + " 없음");
            Assert.AreEqual(1.06, asset.goldPerSecondGrowth, 1e-12,
                "게임이 읽는 Data/GameConfig.asset — IdleDataBuilder는 인스펙터 값을 유지하므로 YAML 값을 직접 바꾼다 (1.10이면 단추가 저절로 불어난다)");
        }

        [Test]
        public void IdleChestStageGrowth_Is10025_InContent()
        {
            var db = ScriptableObject.CreateInstance<MetaDatabase>();
            try
            {
                ApplyMetaContent(db);
                Assert.IsNotNull(db.idleChest);
                Assert.AreEqual(1.0025, db.idleChest.stageGrowth, 1e-12, "수선 바구니 스테이지 배율 (×1.02면 150스테이지 ×19)");
            }
            finally
            {
                Object.DestroyImmediate(db);
            }
        }

        // ───────────────────────── 도우미 ─────────────────────────

        /// <summary>P4-C 전 TaperedScale(두 구간) 그대로</summary>
        float TwoSegment(float growth, float lateGrowth, int stage)
        {
            int steps = Mathf.Max(1, stage) - 1;
            int taper = Mathf.Max(1, _battle.stageGrowthTaperStage) - 1;
            int early = Mathf.Min(steps, taper);
            int late = steps - early;
            double scale = Math.Pow(Mathf.Max(1f, growth), early) * Math.Pow(Mathf.Max(1f, lateGrowth), late);
            return (float)Math.Min(scale, 1e30);
        }

        /// <summary>
        /// 독립 계산 (PROGRESSION-SIM §0의 말 그대로, 구간별 거듭제곱을 쓰지 않는다): 스테이지 s → s+1 걸음마다 곱한다 —
        /// s가 30보다 작으면 early, 151보다 작으면 late, 그 뒤로는 late2.
        /// </summary>
        static double PerStep(int stage, float early, float late, float late2)
        {
            double scale = 1.0;
            for (int s = 1; s < stage; s++) scale *= s < 30 ? early : s < 151 ? late : late2;
            return scale;
        }

        void AssertToday(int stage)
        {
            Assert.AreEqual(TwoSegment(_battle.stageHpGrowth, _battle.stageHpGrowthLate, stage), _battle.GetStageHpScale(stage), $"체력 {stage}");
            Assert.AreEqual(TwoSegment(_battle.stageDamageGrowth, _battle.stageDamageGrowthLate, stage), _battle.GetStageDamageScale(stage), $"피해 {stage}");
        }

        /// <summary>from → from+1 한 걸음의 배율 (float 정밀도 안에서 정확히)</summary>
        void AssertStep(int from, float hpGrowth, float damageGrowth)
        {
            double hp = (double)_battle.GetStageHpScale(from + 1) / _battle.GetStageHpScale(from);
            double damage = (double)_battle.GetStageDamageScale(from + 1) / _battle.GetStageDamageScale(from);
            Assert.AreEqual(hpGrowth, hp, hpGrowth * Tolerance, $"체력 {from} → {from + 1}");
            Assert.AreEqual(damageGrowth, damage, damageGrowth * Tolerance, $"피해 {from} → {from + 1}");
        }

        static void AssertClose(double expected, float actual, string message) =>
            Assert.AreEqual(expected, actual, Math.Abs(expected) * Tolerance, message);

        /// <summary>
        /// 코드 원본 MetaContent.Apply(db, null) — 에디터 어셈블리(IdleGame.Editor)라 시험 어셈블리가 참조하지 않으므로
        /// 불러온 어셈블리에서 찾는다 (ContentSourceTests와 같은 방식). content가 없으면 GameConfig 기본값(8시간 / 0.5 / 60초)을 쓴다.
        /// </summary>
        static void ApplyMetaContent(MetaDatabase db)
        {
            Type content = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IdleGame.Editor") continue;
                content = assembly.GetType("IdleGame.EditorTools.MetaContent", false);
                break;
            }
            Assert.IsNotNull(content, "IdleGame.Editor 어셈블리의 MetaContent를 찾을 수 없다");
            var apply = content.GetMethod("Apply", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(MetaDatabase), typeof(GameContent) }, null);
            Assert.IsNotNull(apply, "MetaContent.Apply(MetaDatabase, GameContent)");
            apply.Invoke(null, new object[] { db, null });
        }
    }
}
