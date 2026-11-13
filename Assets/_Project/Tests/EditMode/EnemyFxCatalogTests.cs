using System;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 적 쪽 공격 연출 규칙 (ATK-03 적 공격 표 / ATK-04 처치 갈래 / ATK-05 불 밝히기 물결 / ATK-06 자동 품질 / ATK-14 빛 구멍).
    /// 화면 없이 순수 규칙만 확인한다.
    /// </summary>
    public class EnemyFxCatalogTests
    {
        // ───────────── 적 공격 표 ─────────────

        [Test]
        public void EveryKnownEnemy_HasOwnAttackLook()
        {
            Assert.AreEqual(9, EnemyFxCatalog.KnownEnemies.Length);
            foreach (string id in EnemyFxCatalog.KnownEnemies)
            {
                Assert.IsTrue(EnemyFxCatalog.HasLook(id), id);
                var look = EnemyFxCatalog.Look(id);
                Assert.AreEqual(id, look.Id);
                StringAssert.StartsWith("enemy_atk_", look.ArtKey, id);
                Assert.Greater(look.EchoSize, 60f, id);
                Assert.GreaterOrEqual(look.ShopSize, look.EchoSize, id + ": 수선점 공격 그림이 잔향이 공격보다 작으면 안 된다");
                Assert.Greater(look.ShopHitSize, 100f, id);
                Assert.Greater(look.Life, 0.1f, id);
                Assert.AreEqual(EnemyFxCatalog.Family(id), look.Family, id + ": 처치 갈래가 표와 규칙에서 같아야 한다");
            }
        }

        [Test]
        public void EveryDefaultEnemy_IsMappedToLookAndFamily()
        {
            var config = ScriptableObject.CreateInstance<BattleConfig>();
            try
            {
                config.ApplyDefaults();
                Assert.IsNotEmpty(config.enemies);
                foreach (var def in config.enemies)
                {
                    Assert.IsTrue(EnemyFxCatalog.HasLook(def.id), def.id + ": 새 적은 EnemyFxCatalog에 공격 모양새를 넣어야 한다");
                    Assert.AreEqual(def.isBoss, EnemyFxCatalog.Look(def.id).Boss, def.id);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EachEnemy_StrikesWithItsOwnMotion()
        {
            Assert.AreEqual(EnemyStrikeMotion.Puff, EnemyFxCatalog.Look("enemy_dust").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Sweep, EnemyFxCatalog.Look("enemy_moth").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Splash, EnemyFxCatalog.Look("enemy_rust").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Whip, EnemyFxCatalog.Look("enemy_tangle").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Claw, EnemyFxCatalog.Look("enemy_wisp").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Bang, EnemyFxCatalog.Look("enemy_thimble").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Spring, EnemyFxCatalog.Look("enemy_windup").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Wave, EnemyFxCatalog.Look("boss_shadow").Motion);
            Assert.AreEqual(EnemyStrikeMotion.Vortex, EnemyFxCatalog.Look("boss_mothqueen").Motion);
        }

        [TestCase("enemy_dust", 1.0f)]
        [TestCase("enemy_moth", 0.6f)]
        [TestCase("enemy_rust", 1.4f)]
        [TestCase("enemy_tangle", 0.9f)]
        [TestCase("enemy_wisp", 0.8f)]
        [TestCase("enemy_thimble", 1.2f)]
        [TestCase("enemy_windup", 1.0f)]
        [TestCase("boss_shadow", 2.0f)]
        [TestCase("boss_mothqueen", 2.0f)]
        public void EachEnemy_StrikesOnItsOwnCadence(string id, float interval)
        {
            // ATK-03 주기: 공격 그림을 이보다 자주 그리지 않는다 (보스 2초 — 큰 파도·소용돌이가 아군 연출을 덮지 않게)
            Assert.AreEqual(interval, EnemyFxCatalog.Look(id).Interval, 1e-4f);
            Assert.Greater(EnemyFxCatalog.Look(null).Interval, 0f);
            Assert.Greater(EnemyFxCatalog.Look(null, true).Interval, 1f);
        }

        [Test]
        public void Bosses_StrikeBiggerThanNormalEnemies()
        {
            float biggestNormal = 0f;
            foreach (string id in EnemyFxCatalog.KnownEnemies)
            {
                var look = EnemyFxCatalog.Look(id);
                if (!look.Boss) biggestNormal = Mathf.Max(biggestNormal, look.ShopSize);
            }
            Assert.Greater(EnemyFxCatalog.Look("boss_shadow").ShopSize, biggestNormal);
            Assert.Greater(EnemyFxCatalog.Look("boss_mothqueen").ShopSize, biggestNormal);
            Assert.AreEqual(220f, EnemyFxCatalog.Look("boss_shadow").ShopHitSize, 0.01f, "보스 수선점 피격 나뭇조각 220px");
            Assert.AreEqual(160f, EnemyFxCatalog.Look("enemy_thimble").ShopHitSize, 0.01f, "골무 껍데기 수선점 피격 160px");
        }

        [Test]
        public void UnknownEnemy_FallsBackToDustPuff_OrBossWave()
        {
            Assert.IsFalse(EnemyFxCatalog.HasLook(null));
            Assert.IsFalse(EnemyFxCatalog.HasLook("enemy_nothing"));
            var normal = EnemyFxCatalog.Look("enemy_nothing");
            Assert.IsNull(normal.Id);
            Assert.IsNull(normal.ArtKey);
            Assert.AreEqual(EnemyStrikeMotion.Puff, normal.Motion);
            var boss = EnemyFxCatalog.Look("boss_nothing", true);
            Assert.IsTrue(boss.Boss);
            Assert.AreEqual(EnemyStrikeMotion.Wave, boss.Motion);
            Assert.AreEqual(EnemyDeathFamily.Dust, EnemyFxCatalog.Family("enemy_nothing"));
        }

        // ───────────── 처치 갈래 (ATK-04) ─────────────

        [Test]
        public void DeathFamilies_FollowTheSpecTable()
        {
            Assert.AreEqual(EnemyDeathFamily.Dust, EnemyFxCatalog.Family("enemy_dust"));
            Assert.AreEqual(EnemyDeathFamily.Dust, EnemyFxCatalog.Family("enemy_tangle"));
            Assert.AreEqual(EnemyDeathFamily.Moth, EnemyFxCatalog.Family("enemy_moth"));
            Assert.AreEqual(EnemyDeathFamily.Moth, EnemyFxCatalog.Family("boss_mothqueen"));
            Assert.AreEqual(EnemyDeathFamily.Metal, EnemyFxCatalog.Family("enemy_rust"));
            Assert.AreEqual(EnemyDeathFamily.Metal, EnemyFxCatalog.Family("enemy_thimble"));
            Assert.AreEqual(EnemyDeathFamily.Metal, EnemyFxCatalog.Family("enemy_windup"));
            Assert.AreEqual(EnemyDeathFamily.Shadow, EnemyFxCatalog.Family("enemy_wisp"));
            Assert.AreEqual(EnemyDeathFamily.Shadow, EnemyFxCatalog.Family("boss_shadow"));
        }

        [Test]
        public void DeathArtKeys_MatchArtFiles()
        {
            Assert.AreEqual("enemy_death_dust", EnemyFxCatalog.DeathArtKey(EnemyDeathFamily.Dust));
            Assert.AreEqual("enemy_death_moth", EnemyFxCatalog.DeathArtKey(EnemyDeathFamily.Moth));
            Assert.AreEqual("enemy_death_metal", EnemyFxCatalog.DeathArtKey(EnemyDeathFamily.Metal));
            Assert.AreEqual("enemy_death_shadow", EnemyFxCatalog.DeathArtKey(EnemyDeathFamily.Shadow));
            foreach (string id in EnemyFxCatalog.KnownEnemies)
            {
                Assert.AreEqual("enemy_death_" + SkillVfxLibrary.DeathKind(id), EnemyFxCatalog.DeathArtKey(EnemyFxCatalog.Family(id)), id);
            }
        }

        [Test]
        public void TangleDeathArt_IsBakedThroughUiArtEnemyDeathYarn()
        {
            // ATK-08: 엉킨 실뭉치만 자기 처치 그림 키를 갖고, 그 키 = UiArt.EnemyDeathYarn의 파일 이름 (Art/VFX/enemy_death_yarn.png)
            string key = EnemyFxCatalog.DeathKeyOverride("enemy_tangle");
            Assert.AreEqual("enemy_death_yarn", key);
            Assert.IsNull(EnemyFxCatalog.DeathKeyOverride("enemy_dust"));
            Assert.IsNull(EnemyFxCatalog.DeathKeyOverride(null));

            // 빌더가 굽는 묶음(SkillArtSet.AttackArt → SkillVfxLibrary._attackArt)에 그 키가 있는 것은 접근자가 그림을 돌려줄 때뿐 (지금은 둘 다 없음)
            var any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var yarnProperty = EditorType("UiArt").GetProperty("EnemyDeathYarn", any);
            var attackArt = EditorType("SkillArtSet").GetMethod("AttackArt", any);
            Assert.IsNotNull(yarnProperty, "UiArt.EnemyDeathYarn");
            Assert.IsNotNull(attackArt, "SkillArtSet.AttackArt()");
            var yarn = yarnProperty.GetValue(null) as Sprite;
            var baked = attackArt.Invoke(null, null) as System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Sprite>>;
            Assert.IsNotNull(baked, "SkillArtSet.AttackArt() 목록");
            int index = baked.FindIndex(pair => pair.Key == key);
            Assert.AreEqual(yarn != null, index >= 0, "실뭉치 처치 그림은 UiArt.EnemyDeathYarn이 있을 때만 굽는다");
            if (index >= 0) Assert.AreSame(yarn, baked[index].Value, "같은 그림");
            Assert.AreEqual(index >= 0 ? 1 : 0, baked.FindAll(pair => pair.Key == key).Count, "한 번만");
        }

        /// <summary>에디터 어셈블리 형식 (시험 어셈블리가 참조하지 않아 불러온 어셈블리에서 찾는다 — UiArtAccessorTests와 같은 방식)</summary>
        static Type EditorType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IdleGame.Editor") continue;
                var type = assembly.GetType("IdleGame.EditorTools." + name, false);
                Assert.IsNotNull(type, $"IdleGame.Editor 어셈블리의 {name}를 찾을 수 없다");
                return type;
            }
            Assert.Fail("IdleGame.Editor 어셈블리를 찾을 수 없다");
            return null;
        }

        [Test]
        public void TangleDeath_IsTintedWine_OthersKeepTheirArt()
        {
            var tangle = EnemyFxCatalog.DeathTint("enemy_tangle");
            Assert.AreNotEqual(Color.white, tangle);
            Assert.Less(tangle.g, 1f);
            Assert.AreEqual(Color.white, EnemyFxCatalog.DeathTint("enemy_dust"));
        }

        [Test]
        public void ExitTime_MothFastest_ShadowSlowest()
        {
            foreach (EnemyDeathFamily family in Enum.GetValues(typeof(EnemyDeathFamily))) Assert.Greater(EnemyView.ExitTime(family), 0.05f, family.ToString());
            Assert.Less(EnemyView.ExitTime(EnemyDeathFamily.Moth), EnemyView.ExitTime(EnemyDeathFamily.Dust));
            Assert.Greater(EnemyView.ExitTime(EnemyDeathFamily.Shadow), EnemyView.ExitTime(EnemyDeathFamily.Dust));
        }

        // ───────────── 수선점 피격 / 기믹 가독성 ─────────────

        [Test]
        public void ShopStrikeSlot_IsTheDirectionTheEnemyCameFrom()
        {
            for (int slot = 0; slot < BattleGeometry.SlotCount; slot++)
            {
                var edge = BattleGeometry.FromAngle(BattleGeometry.SlotAngle(slot) + 0.2f, BattleGeometry.ShopRadius);
                Assert.AreEqual(slot, EnemyFxCatalog.ShopStrikeSlot(edge), "slot " + slot);
            }
            Assert.AreEqual(-1, EnemyFxCatalog.ShopStrikeSlot(Vector2.zero));
        }

        [Test]
        public void ShopStrikeTrauma_BossHeavier_ShieldHalves()
        {
            Assert.AreEqual(HitFeel.ShopHitTrauma, EnemyFxCatalog.ShopStrikeTrauma(false, false), 1e-5f);
            Assert.AreEqual(HitFeel.BossSiegeTrauma, EnemyFxCatalog.ShopStrikeTrauma(true, false), 1e-5f);
            Assert.AreEqual(HitFeel.BossSiegeTrauma * 0.5f, EnemyFxCatalog.ShopStrikeTrauma(true, true), 1e-5f);
        }

        [Test]
        public void StrikeAlpha_DustStormDims_HiddenShadowIsTranslucent()
        {
            Assert.AreEqual(1f, EnemyFxCatalog.StrikeAlpha(StageModifierType.None, false), 1e-5f);
            Assert.AreEqual(0.85f, EnemyFxCatalog.StrikeAlpha(StageModifierType.DustStorm, false), 1e-5f);
            Assert.AreEqual(0.5f, EnemyFxCatalog.StrikeAlpha(StageModifierType.Rain, true), 1e-5f);
        }

        [Test]
        public void LightHole_OpensThenCloses_AndDarkensToFortyPercent()
        {
            Assert.AreEqual(0f, StageAmbienceView.HoleStrength(0f), 1e-5f);
            Assert.AreEqual(0f, StageAmbienceView.HoleStrength(1f), 1e-5f);
            float peak = StageAmbienceView.HoleStrength(0.4f);
            Assert.AreEqual(1f - EnemyFxCatalog.LightHoleDarkness, peak, 1e-4f, "가운데 어둠 알파 ×0.4");
            Assert.Less(StageAmbienceView.HoleStrength(0.95f), peak);
            Assert.AreEqual(120f, EnemyFxCatalog.LightHoleRadius, 0.01f);
            Assert.AreEqual(0.4f, EnemyFxCatalog.LightHoleTime, 0.001f);
        }

        // ───────────── 불 밝히기 물결 (ATK-05) ─────────────

        [Test]
        public void LampWave_ExpandsToFullRadiusInHalfASecond()
        {
            Assert.AreEqual(0f, LampWave.Radius(0f), 1e-5f);
            Assert.AreEqual(LampWave.MaxRadius, LampWave.Radius(LampWave.Life), 1e-4f);
            Assert.AreEqual(0.5f, LampWave.Life, 1e-5f);
            float previous = -1f;
            for (int i = 0; i <= 10; i++)
            {
                float radius = LampWave.Radius(LampWave.Life * i / 10f);
                Assert.Greater(radius, previous);
                previous = radius;
            }
        }

        [Test]
        public void LampWaveDelay_IsTheInverseOfRadius_SoFartherEnemiesAreHitLater()
        {
            for (int i = 1; i < 10; i++)
            {
                float t = LampWave.Life * i / 10f;
                Assert.AreEqual(t, LampWave.Delay(LampWave.Radius(t)), 1e-3f, "t=" + t);
            }
            Assert.Less(LampWave.Delay(0.8f), LampWave.Delay(2.4f));
            Assert.GreaterOrEqual(LampWave.Delay(0f), 0.02f);
            Assert.AreEqual(LampWave.Life, LampWave.Delay(10f), 1e-5f);
        }

        [Test]
        public void LampFlash_IsFieldOnlyAndReducible()
        {
            Assert.AreEqual(0.45f, LampWave.FlashPeakFor(false), 1e-5f);
            Assert.AreEqual(0.25f, LampWave.FlashPeakFor(true), 1e-5f);
            Assert.LessOrEqual(LampWave.FlashPeakFor(true), GameSettings.ReducedFlashMaxAlpha);
            Assert.AreEqual(0.2f, LampWave.IgniteScale, 1e-5f);
            Assert.AreEqual(0.12f, LampWave.IgniteTime, 1e-5f);
        }

        // ───────────── 자동 품질 (ATK-06 / CR-08) ─────────────

        [Test]
        public void QualityMonitor_SmoothFrames_NeverLower()
        {
            var monitor = new FxQualityMonitor();
            for (int i = 0; i < 60 * 120; i++) Assert.IsFalse(monitor.Sample(1f / 60f));
            Assert.AreEqual(0, monitor.Strikes);
        }

        [Test]
        public void QualityMonitor_TwoSlowWindowsWithinAMinute_LowersOnce()
        {
            var monitor = new FxQualityMonitor();
            int lowered = 0;
            // 30fps(33ms) 11초 = 느린 5초 구간 두 번 (+ 여유)
            for (int i = 0; i < 30 * 11; i++)
            {
                if (monitor.Sample(1f / 30f)) lowered++;
            }
            Assert.AreEqual(1, lowered);
            Assert.AreEqual(0, monitor.Strikes, "낮춘 뒤에는 처음부터 다시 센다");
        }

        [Test]
        public void QualityMonitor_SlowWindowsFarApart_DoNotLower()
        {
            var monitor = new FxQualityMonitor();
            bool lowered = false;
            for (int i = 0; i < 30 * 5; i++) lowered |= monitor.Sample(1f / 30f);
            for (int i = 0; i < 60 * 70; i++) lowered |= monitor.Sample(1f / 60f);
            for (int i = 0; i < 30 * 5; i++) lowered |= monitor.Sample(1f / 30f);
            Assert.IsFalse(lowered, "60초보다 멀리 떨어진 느린 구간 두 번은 낮추지 않는다");
        }

        [Test]
        public void QualityMonitor_ChosenThirtyFpsCap_IsNotLag()
        {
            // 설정 30fps · 잠자기 모드: 33ms 프레임은 목표대로 도는 것 — 이펙트를 줄이지 않는다
            var monitor = new FxQualityMonitor();
            bool lowered = false;
            for (int i = 0; i < 30 * 120; i++) lowered |= monitor.Sample(1f / 30f, 30);
            Assert.IsFalse(lowered);
            Assert.AreEqual(0, monitor.Strikes);
            // 30fps 목표에서도 정말 느리면(20fps) 낮춘다
            int slow = 0;
            for (int i = 0; i < 20 * 11; i++)
            {
                if (monitor.Sample(1f / 20f, 30)) slow++;
            }
            Assert.AreEqual(1, slow);
        }

        [Test]
        public void QualityMonitor_TargetChange_StartsOver()
        {
            var monitor = new FxQualityMonitor();
            for (int i = 0; i < 30 * 6; i++) monitor.Sample(1f / 30f, 60);
            Assert.AreEqual(1, monitor.Strikes);
            monitor.Sample(1f / 30f, 30);
            Assert.AreEqual(0, monitor.Strikes, "프레임 목표가 바뀌면 처음부터 다시 센다");
            Assert.AreEqual(FxQualityMonitor.SlowFrame, FxQualityMonitor.SlowThreshold(60), 1e-6f);
            Assert.AreEqual(1.25f / 30f, FxQualityMonitor.SlowThreshold(30), 1e-6f);
        }

        [Test]
        public void QualityMonitor_IgnoresHitchesLongerThanAQuarterSecond()
        {
            var monitor = new FxQualityMonitor();
            bool lowered = false;
            for (int i = 0; i < 200; i++) lowered |= monitor.Sample(0.5f);
            Assert.IsFalse(lowered);
            Assert.AreEqual(0, monitor.Strikes);
        }

        // ───────────── 한꺼번에 처치 상한 · 큰 순간 채움 비용 ─────────────

        [Test]
        public void KillConfetti_ShrinksWithFreeSlots_AndHalvesInFastStreak()
        {
            Assert.AreEqual(10, EnemyFxCatalog.KillConfettiCount(10, 420, false), "여유가 넉넉하면 품질 상한 그대로");
            Assert.AreEqual(10, EnemyFxCatalog.KillConfettiCount(10, 100, false));
            Assert.AreEqual(5, EnemyFxCatalog.KillConfettiCount(10, 80, false), "(여유 − 60) / 4");
            Assert.AreEqual(0, EnemyFxCatalog.KillConfettiCount(10, EnemyFxCatalog.KillSparkleReserve, false), "별빛 문 아래로는 쓰지 않는다");
            Assert.AreEqual(0, EnemyFxCatalog.KillConfettiCount(10, 0, false));
            Assert.AreEqual(0, EnemyFxCatalog.KillConfettiCount(0, 420, false));
            Assert.AreEqual(5, EnemyFxCatalog.KillConfettiCount(10, 420, true), "2배속 연속 처치는 절반");
            Assert.AreEqual(2, EnemyFxCatalog.KillConfettiCount(EnemyFxCatalog.MiniKillConfetti, 420, true));
            Assert.AreEqual(3, EnemyFxCatalog.KillConfettiCount(10, 80, true), "절반은 올림");
        }

        [Test]
        public void FastKillStreak_OnlyAtDoubleSpeed()
        {
            Assert.IsFalse(EnemyFxCatalog.FastKillStreak(1, 20));
            Assert.IsFalse(EnemyFxCatalog.FastKillStreak(2, EnemyFxCatalog.FastStreakKills - 1));
            Assert.IsTrue(EnemyFxCatalog.FastKillStreak(2, EnemyFxCatalog.FastStreakKills));
            Assert.IsTrue(EnemyFxCatalog.FastKillStreak(3, 40));
        }

        [Test]
        public void KillWindow_LimitsBurstsPerWindow_CountingBothSides()
        {
            var window = new KillWindow(32);
            int limit = EnemyFxCatalog.MiniKillsPerWindow(FxQuality.Medium);
            Assert.AreEqual(6, limit);
            for (int i = 0; i < limit; i++) Assert.IsTrue(window.TryAdd(1f + i * 0.01f, HitFeel.KillBurstWindow, limit), "kill " + i);
            Assert.IsFalse(window.TryAdd(1.1f, HitFeel.KillBurstWindow, limit), "0.2초 안 일곱 번째는 마이크로로");
            Assert.IsFalse(window.TryAdd(0.9f, HitFeel.KillBurstWindow, limit), "먼저 터지는 처치(늦게 부른)도 같은 창으로 센다");
            Assert.IsTrue(window.TryAdd(1.4f, HitFeel.KillBurstWindow, limit), "창 밖은 다시 미니");
            window.Clear();
            Assert.IsTrue(window.TryAdd(1.1f, HitFeel.KillBurstWindow, limit));
            Assert.Less(EnemyFxCatalog.KillMicroScale, 0.6f, "마이크로 꽃은 미니(0.6K)보다 작다");
        }

        [Test]
        public void MiniKillCap_FollowsQualityBudget()
        {
            Assert.AreEqual(10, EnemyFxCatalog.MiniKillsPerWindow(FxQuality.High));
            Assert.AreEqual(6, EnemyFxCatalog.MiniKillsPerWindow(FxQuality.Medium));
            Assert.AreEqual(4, EnemyFxCatalog.MiniKillsPerWindow(FxQuality.Low));
            Assert.Greater(EnemyFxCatalog.MiniKillsPerWindow(FxQuality.Low), 0);
            Assert.Greater(EnemyFxCatalog.FullKillFree, EnemyFxCatalog.KillSparkleReserve - 20, "풀 처치 문은 별빛 문 가까이");
            Assert.Less(EnemyFxCatalog.FullKillFree, HitFeel.ParticleBudget(FxQuality.Low), "낮음 예산 안에서도 풀 처치가 뜰 수 있다");
        }

        [Test]
        public void BossDeathFillCost_FallsWithQuality()
        {
            Assert.AreEqual(3, EnemyFxCatalog.BossRingCount(FxQuality.High));
            Assert.AreEqual(2, EnemyFxCatalog.BossRingCount(FxQuality.Medium));
            Assert.AreEqual(1, EnemyFxCatalog.BossRingCount(FxQuality.Low));
            Assert.AreEqual(3f, EnemyFxCatalog.BossRingReach(FxQuality.High), 1e-5f);
            Assert.AreEqual(2.5f, EnemyFxCatalog.BossRingReach(FxQuality.Medium), 1e-5f);
            Assert.AreEqual(2f, EnemyFxCatalog.BossRingReach(FxQuality.Low), 1e-5f);
            Assert.IsTrue(EnemyFxCatalog.BossWideLayers(FxQuality.High));
            Assert.IsTrue(EnemyFxCatalog.BossWideLayers(FxQuality.Medium));
            Assert.IsFalse(EnemyFxCatalog.BossWideLayers(FxQuality.Low), "낮음은 빛살·바닥 고리 없음");
            Assert.AreEqual(900f, EnemyFxCatalog.RewardGlowSize(FxQuality.High), 0.01f);
            Assert.AreEqual(560f, EnemyFxCatalog.RewardGlowSize(FxQuality.Medium), 0.01f);
            Assert.AreEqual(560f, EnemyFxCatalog.RewardGlowSize(FxQuality.Low), 0.01f);
            Assert.Less(EnemyFxCatalog.RewardRingSize(FxQuality.Low), EnemyFxCatalog.RewardRingSize(FxQuality.Medium));
        }

        [Test]
        public void HoldOffset_FreezesDuringGlobalStop_ThenCatchesUp()
        {
            var step = new Vector2(10f, 0f);
            var offset = EnemyFxCatalog.HoldOffset(Vector2.zero, step, 0f, 0f);
            Assert.AreEqual(-10f, offset.x, 1e-4f, "전체 정지: 시뮬레이터가 걸은 만큼 붙잡아 보이는 자리는 그대로");
            offset = EnemyFxCatalog.HoldOffset(offset, step, HitFeel.SlowScale, 0.003f);
            Assert.AreEqual(-17f, offset.x, 1e-4f, "느린 화면: 0.3만큼만 움직인다");
            for (int i = 0; i < 10; i++) offset = EnemyFxCatalog.HoldOffset(offset, step, 1f, 1f / 60f);
            Assert.Less(offset.magnitude, 1f, "풀리면 0.17초 남짓에 따라잡는다");
            for (int i = 0; i < 30; i++) offset = EnemyFxCatalog.HoldOffset(offset, step, 1f, 1f / 60f);
            Assert.AreEqual(Vector2.zero, offset);
            var far = EnemyFxCatalog.HoldOffset(Vector2.zero, new Vector2(0f, 500f), 0f, 0f);
            Assert.AreEqual(EnemyFxCatalog.ActorHoldMax, far.magnitude, 1e-3f, "붙잡는 거리 상한");
        }

        // ───────────── 처치 v2 · 보스 폭발 자리 · 몸 번쩍임 · 수선점 피격 · 등장 (FX 2차 조율 규칙) ─────────────

        [Test]
        public void BossBurstCenter_PullsInsideVisibleBand_ButStaysOnTheBoss()
        {
            // B = HitFeel.BossBurstSize 420 → 띠 안쪽 당김 · 옮김 상한 0.3B = 126
            float inset = EnemyFxCatalog.BossBurstInset * HitFeel.BossBurstSize;
            float cap = EnemyFxCatalog.BossBurstMaxMove * HitFeel.BossBurstSize;
            Assert.AreEqual(126f, inset, 1e-3f);
            Assert.AreEqual(126f, cap, 1e-3f);
            // 예전 띠(±312): 위 끝 보스(y 304)는 띠 안쪽 186까지 118px만 내려온다 (예전 SafeTop 당김은 238px 아래 수선점 위로 옮겼다)
            AssertPoint(new Vector2(0f, 186f), EnemyFxCatalog.BossBurstCenter(new Vector2(0f, 304f), inset, 540f, -312f, 312f, cap), "띠 ±312");
            // 키 큰 새 띠(±732)에서는 같은 자리가 이미 안쪽이라 그대로
            AssertPoint(new Vector2(0f, 304f), EnemyFxCatalog.BossBurstCenter(new Vector2(0f, 304f), inset, 540f, -732f, 732f, cap), "띠 ±732");
            // 시트가 열린 띠(−412 ~ 372): 안쪽 246까지는 132px — 상한 126px에서 멈춘다
            AssertPoint(new Vector2(0f, 252f), EnemyFxCatalog.BossBurstCenter(new Vector2(0f, 378f), inset, 540f, -412f, 372f, cap), "시트 띠, 상한");
            // 가로: 반폭 540 − 126 = 414
            AssertPoint(new Vector2(414f, 0f), EnemyFxCatalog.BossBurstCenter(new Vector2(487f, 0f), inset, 540f, -312f, 312f, cap), "x 487");
            // 띠가 inset × 2보다 좁으면 그 축의 가운데로 — 옮김 상한은 그대로
            AssertPoint(new Vector2(0f, 30f), EnemyFxCatalog.BossBurstCenter(new Vector2(0f, 90f), inset, 540f, -60f, 120f, cap), "좁은 띠 → 가운데");
            AssertPoint(new Vector2(0f, 174f), EnemyFxCatalog.BossBurstCenter(new Vector2(0f, 300f), inset, 540f, -100f, 100f, cap), "좁은 띠, 상한");
            AssertPoint(Vector2.zero, EnemyFxCatalog.BossBurstCenter(new Vector2(100f, 0f), inset, 100f, -312f, 312f, cap), "좁은 가로 → 가운데");
        }

        [Test]
        public void KillRing_IsFamilyTintedCream_NotWhiteSoapBubble()
        {
            // 처치 빛 고리 = lerp(갈래 고리 색, 코어 크림, 0.45), 0.5 → 1.4 × 꽃 크기, 빛 세기 0.6 — 여럿이 한꺼번에 풀려도 똑같은 흰 고리가 아니라 갈래 색이 읽힌다
            foreach (EnemyDeathFamily family in Enum.GetValues(typeof(EnemyDeathFamily)))
            {
                var ring = EnemyFxCatalog.KillRingColor(family);
                AssertColor(Color.Lerp(EnemyFxCatalog.FamilyRingColor(family), JuiceColors.CoreCream, 0.45f), ring, family.ToString());
                Assert.IsFalse(Near(ring, JuiceColors.CoreCream), family + ": 처치 고리가 코어 크림 그대로 (흰 비눗방울)");
            }
            Assert.AreEqual(1.4f, EnemyFxCatalog.KillRingReach, 1e-6f);
            Assert.AreEqual(0.6f, EnemyFxCatalog.KillRingIntensity, 1e-6f);
        }

        [Test]
        public void KillBloom_ShadowReleasesAsLavenderLight_OthersKeepTheirArt()
        {
            // 그림자 갈래만 가산 + 라벤더 물들임 (심보라 소용돌이가 잉크 얼룩이 아니라 빛으로 풀린다) · 일찍 사라지기 시작 — 나머지는 불투명 그림 그대로
            foreach (EnemyDeathFamily family in Enum.GetValues(typeof(EnemyDeathFamily)))
            {
                bool shadow = family == EnemyDeathFamily.Shadow;
                Assert.AreEqual(shadow ? ShadowBloomAddPin : 0f, EnemyFxCatalog.KillBloomAdd(family), 1e-6f, family + " 가산");
                Assert.AreEqual(shadow ? 0.3f : 0.4f, EnemyFxCatalog.KillBloomFadeStart(family), 1e-6f, family + " 사라지기 시작");
            }
            var lavender = Color.Lerp(Color.white, UITheme.Lavender, ShadowBloomLavenderPin);
            AssertColor(lavender, EnemyFxCatalog.KillBloomTint("enemy_wisp"), "enemy_wisp");
            AssertColor(lavender, EnemyFxCatalog.KillBloomTint("boss_shadow"), "boss_shadow");
            AssertColor(EnemyFxCatalog.DeathTint("enemy_tangle"), EnemyFxCatalog.KillBloomTint("enemy_tangle"), "실뭉치 와인색");
            AssertColor(Color.white, EnemyFxCatalog.KillBloomTint("enemy_dust"), "enemy_dust");
            AssertColor(Color.white, EnemyFxCatalog.KillBloomTint("enemy_rust"), "enemy_rust");
            AssertColor(Color.white, EnemyFxCatalog.KillBloomTint("enemy_moth"), "enemy_moth");
        }

        /// <summary>그림자 처치 꽃 가산 · 라벤더 물들임 (2차 조율 최종값)</summary>
        const float ShadowBloomAddPin = 0.2f;
        const float ShadowBloomLavenderPin = 0.7f;

        [Test]
        public void BodyFlash_ReduceFlashTakesAQuarterOff()
        {
            // 번쩍임 줄이기: 몸의 크림 번쩍임 × 0.75 (치명타 1.0 → 0.75 · 일반 0.85 → 0.64 · 처치 버팀 1.0 → 0.75), 끄면 그대로
            Assert.AreEqual(0.75f, EnemyFxCatalog.ReducedBodyFlash, 1e-6f);
            Assert.AreEqual(1f, EnemyFxCatalog.BodyFlash(1f, false), 1e-6f);
            Assert.AreEqual(0.85f, EnemyFxCatalog.BodyFlash(0.85f, false), 1e-6f);
            Assert.AreEqual(0.75f, EnemyFxCatalog.BodyFlash(1f, true), 1e-6f, "치명타 · 처치 버팀");
            Assert.AreEqual(0.6375f, EnemyFxCatalog.BodyFlash(0.85f, true), 1e-5f, "일반 명중");
            Assert.AreEqual(0f, EnemyFxCatalog.BodyFlash(0f, true), 1e-6f);
        }

        [Test]
        public void ShopHit_ReadsSmallerThanOurOwnBasicHit()
        {
            // 적의 일격이 지킴이 기본 명중보다 크게 읽히지 않게: 나뭇조각 첫 프레임 ×1.12 (예전 ×1.25) · 연출 크기 배율 — 공격 표 ShopHitSize 220 / 160은 그대로 (위 시험)
            Assert.AreEqual(1.12f, EnemyFxCatalog.ShopHitFirstFrame, 1e-6f);
            Assert.AreEqual(ShopHitArtScalePin, EnemyFxCatalog.ShopHitArtScale, 1e-6f);
            Assert.Less(EnemyFxCatalog.ShopHitArtScale, 1f);
        }

        /// <summary>수선점 피격 연출 크기 배율 (Wave 3: 0.8 → 0.72 — 먼지 수선점 피격 최대 넓이 2.32% → 목표 ≈ 2.0%, fx2 leftovers)</summary>
        const float ShopHitArtScalePin = 0.72f;

        [Test]
        public void SpawnPuff_IsASmallerShorterBossPool()
        {
            // UX-SPEC-IA §6.5 CR-19: 일반 적 등장 그림자 웅덩이 = 보스 웅덩이(360px · 0.6초)의 0.45배 = 162px, 더 짧게 (0.35초)
            Assert.AreEqual(162f, EnemyFxCatalog.SpawnPoolScale * EnemyFxCatalog.BossPoolSize, 1e-3f);
            Assert.Less(EnemyFxCatalog.SpawnPoolLife, EnemyFxCatalog.BossPoolLife);
        }

        // ───────────── 모양 ─────────────

        [Test]
        public void MotifCount_MatchesEnum()
        {
            Assert.AreEqual(Enum.GetValues(typeof(VfxMotif)).Length, SkillVfxLook.MotifCount);
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        static void AssertColor(Color expected, Color actual, string label)
        {
            Assert.IsTrue(Near(expected, actual) && Mathf.Abs(expected.a - actual.a) < 0.004f, $"{label}: expected {expected}, got {actual}");
        }

        static void AssertPoint(Vector2 expected, Vector2 actual, string label)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, label + " x");
            Assert.AreEqual(expected.y, actual.y, 1e-3f, label + " y");
        }
    }
}
