using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 공격 연출 규칙 (ATK-07 #6): 계보·성향 모양새 표, 비행 시간 / 처치 보류 (ATK-09), 흔들림 예산 (FX-02),
    /// 타격 사다리 (FX-01), 피해 숫자 표기 (FX-03). 화면 없이 순수 규칙만 확인한다.
    /// </summary>
    public class AttackFxCatalogTests
    {
        // ───────────── ATK-07 5단계: 대체 모양 로그 (RM-3 FIX-P4-3b) ─────────────

        /// <summary>
        /// SkillVfxLibrary가 아트 없이 흰색 대체 모양을 쓰면(빌더가 구운 대체 모양 · 빈 칸 · 모르는 키) "[SkillVfxLibrary] fallback {키}"를 키마다 한 번 남긴다.
        /// 아트를 찾은 길과 이미 알린 키는 할당 없음 (빈 VFX 증명 — fx_proj에서 Art/VFX/*.png를 지운 빌드가 이 줄을 1개 이상 남겨야 한다)
        /// </summary>
        [Test]
        public void SkillVfxLibrary_LogsFallbackOncePerKey_NoAllocOnHit()
        {
            const string Prefix = "[SkillVfxLibrary] fallback ";
            var go = new GameObject("VfxLibraryFallback");
            var texture = new Texture2D(4, 4);
            var lines = new List<string>();
            Sprite art = null;
            Sprite baked = null;
            Application.LogCallback handler = (message, stack, type) =>
            {
                if (message != null && message.StartsWith(Prefix, StringComparison.Ordinal)) lines.Add(message);
            };
            Application.logMessageReceived += handler;
            try
            {
                art = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                baked = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
                var library = go.AddComponent<SkillVfxLibrary>();
                var field = typeof(SkillVfxLibrary).GetField("_vfx", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(field, "SkillVfxLibrary._vfx");
                field.SetValue(library, new[]
                {
                    new SkillVfxLibrary.VfxEntry { key = "vfx_test_art", sprite = art, isArt = true },
                    new SkillVfxLibrary.VfxEntry { key = "vfx_test_baked", sprite = baked, isArt = false },
                    new SkillVfxLibrary.VfxEntry { key = "vfx_test_empty", sprite = null, isArt = false },
                });

                Assert.IsTrue(library.TryGetVfx("vfx_test_art", out var sprite, out bool isArt));
                Assert.IsTrue(isArt);
                Assert.AreSame(art, sprite);
                Assert.AreEqual(0, lines.Count, "아트를 찾으면 알리지 않는다");

                Assert.IsTrue(library.TryGetVfx("vfx_test_baked", out sprite, out isArt), "빌더가 구운 대체 모양");
                Assert.IsFalse(isArt);
                Assert.AreSame(baked, sprite);
                library.TryGetVfx("vfx_test_baked", out _, out _);
                library.TryGetVfx("vfx_test_empty", out _, out _);
                library.TryGetVfx("vfx_test_unknown", out _, out _);
                library.TryGetVfx("vfx_test_unknown", out _, out _);
                library.TryGetVfx(null, out _, out _);
                CollectionAssert.AreEqual(new[] { Prefix + "vfx_test_baked", Prefix + "vfx_test_empty", Prefix + "vfx_test_unknown" }, lines,
                    "대체 키마다 한 번 (null 키는 알리지 않는다)");

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 100; i++)
                {
                    library.TryGetVfx("vfx_test_art", out _, out _);
                    library.TryGetVfx("vfx_test_baked", out _, out _);
                    library.IsArt("vfx_test_unknown");
                }
                Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "찾은 길 · 이미 알린 키는 할당 없음");
                Assert.AreEqual(3, lines.Count, "두 번째부터는 알리지 않는다");
            }
            finally
            {
                Application.logMessageReceived -= handler;
                UnityEngine.Object.DestroyImmediate(go);
                if (art != null) UnityEngine.Object.DestroyImmediate(art);
                if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ───────────── 모양새 표 ─────────────

        [Test]
        public void EveryKnownLineage_HasOwnLook()
        {
            Assert.AreEqual(7, AttackFxCatalog.KnownLineages.Length);
            foreach (string id in AttackFxCatalog.KnownLineages)
            {
                Assert.IsTrue(AttackFxCatalog.HasLook(id), id);
                var look = AttackFxCatalog.Look(id);
                Assert.AreEqual(id, look.Id);
                Assert.Greater(look.Life, 0.05f, id);
                Assert.Greater(look.HitSize, 60f, id);
                Assert.AreNotEqual(AttackExtra.None, look.Extra, id + ": 계보마다 사물 곁들임이 있어야 한다");
            }
        }

        [Test]
        public void EveryContentLineage_HasOwnLook()
        {
            // ATK-07 #6: 손으로 적은 KnownLineages가 아니라 실제 콘텐츠 계보 기준 — 새 계보가 회색 기본 모양으로 조용히 나가지 않게
            var ids = ContentLineageIds();
            Assert.IsNotEmpty(ids, "콘텐츠에 계보가 하나도 없다");
            foreach (string id in ids)
            {
                Assert.IsTrue(AttackFxCatalog.HasLook(id), id + ": 새 계보는 AttackFxCatalog에 기본 공격 모양새(투사체·명중 그림)를 넣어야 한다");
            }
            foreach (string id in AttackFxCatalog.KnownLineages)
            {
                CollectionAssert.Contains(ids, id, id + ": 콘텐츠에 없는 계보의 공격 그림은 쓰이지 않는다");
            }
        }

        [Test]
        public void EveryAttackArtFile_ExistsAsSprite()
        {
            // ATK-07 #6 / ATK-08: 공격 그림 30장 — 이름이 바뀌거나 지워지면 씬 빌더가 null로 굽고 흰색 모양으로 조용히 대체된다
            var keys = AttackArtKeys();
            Assert.AreEqual(30, keys.Count, string.Join(", ", keys));
            Assert.AreEqual(keys.Count, keys.Distinct().Count(), "그림 이름이 겹친다");

            var missing = keys.Where(k => !File.Exists(ArtPath(k))).ToList();
            Assert.IsEmpty(missing, $"{ArtVfxDir}에 없는 그림: {string.Join(", ", missing)}");
            var notSprite = keys.Where(k => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath(k)) == null).ToList();
            Assert.IsEmpty(notSprite, "Sprite로 불러올 수 없는 그림 (ArtLib.Vfx가 null → 흰색 모양): " + string.Join(", ", notSprite));

            // 저격 빛 꼬리는 가로로 긴 512x128 (끝이 오른쪽) — 정사각형 그림이 들어오면 꼬리가 뭉개진다
            var trail = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath(SkillVfxLibrary.SnipeTrailId));
            Assert.AreEqual(4f, trail.rect.width / trail.rect.height, 0.01f, SkillVfxLibrary.SnipeTrailId + " 가로:세로 = 4:1");
        }

        const string ArtVfxDir = "Assets/_Project/Art/VFX";

        static string ArtPath(string key) => $"{ArtVfxDir}/{key}.png";

        /// <summary>
        /// 공격 연출이 쓰는 그림 이름 30개: 계보 7종 atk_{계보}_shot/_hit · atk_slash · atk_snipe_trail ·
        /// 적 9종 enemy_atk_* · enemy_hit_shop · 처치 갈래 4종 enemy_death_{dust|moth|metal|shadow} (AttackFxBuilder / EnemyFxBuilder와 같은 규칙)
        /// </summary>
        static List<string> AttackArtKeys()
        {
            var keys = new List<string>();
            foreach (string id in AttackFxCatalog.KnownLineages)
            {
                keys.Add($"atk_{id}_shot");
                keys.Add($"atk_{id}_hit");
            }
            keys.Add(SkillVfxLibrary.SlashId);
            keys.Add(SkillVfxLibrary.SnipeTrailId);
            foreach (string id in EnemyFxCatalog.KnownEnemies) keys.Add(EnemyFxCatalog.Look(id).ArtKey);
            keys.Add(SkillVfxLibrary.ShopHitId);
            foreach (EnemyDeathFamily family in Enum.GetValues(typeof(EnemyDeathFamily))) keys.Add(EnemyFxCatalog.DeathArtKey(family));
            return keys;
        }

        /// <summary>
        /// 콘텐츠 계보 ID: 코드 원본 CharacterContent.Build() (에디터 어셈블리 — 시험 어셈블리가 참조하지 않으므로 불러온 어셈블리에서 찾는다)
        /// + 프로젝트에 구워 둔 CharacterDatabase 에셋 (있으면)
        /// </summary>
        static List<string> ContentLineageIds()
        {
            var ids = new List<string>();
            Type content = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IdleGame.Editor") continue;
                content = assembly.GetType("IdleGame.EditorTools.CharacterContent", false);
                break;
            }
            Assert.IsNotNull(content, "IdleGame.Editor 어셈블리의 CharacterContent를 찾을 수 없다");
            var build = content.GetMethod("Build", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            Assert.IsNotNull(build, "CharacterContent.Build()");
            var lineages = build.Invoke(null, null) as List<LineageDef>;
            Assert.IsNotNull(lineages, "CharacterContent.Build()는 List<LineageDef>를 돌려준다");
            foreach (var lineage in lineages)
            {
                if (lineage != null && !ids.Contains(lineage.id)) ids.Add(lineage.id);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CharacterDatabase)))
            {
                var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db == null || db.lineages == null) continue;
                foreach (var lineage in db.lineages)
                {
                    if (lineage != null && !ids.Contains(lineage.id)) ids.Add(lineage.id);
                }
            }
            return ids;
        }

        [Test]
        public void UnknownLineage_FallsBackToGenericLook()
        {
            Assert.IsFalse(AttackFxCatalog.HasLook(null));
            Assert.IsFalse(AttackFxCatalog.HasLook("no_such_lineage"));
            var look = AttackFxCatalog.Look("no_such_lineage");
            Assert.IsNull(look.Id);
            Assert.Greater(look.Life, 0f);
            Assert.Greater(look.HitSize, 0f);
        }

        [Test]
        public void LineageRingTints_AreColoured_NotWhite()
        {
            // 일반 명중 고리는 계보 색으로 읽혀야 한다 (r1 비평: 명중·처치마다 흰 고리가 겹쳐 '흰 비눗방울'처럼 보였다) —
            // 고리 색의 채널 폭(가장 큰 채널 − 가장 작은 채널) ≥ 0.12. 지금 가장 옅은 짝짝이 #DCCBF4가 0.16
            foreach (string id in AttackFxCatalog.KnownLineages)
            {
                var sig = AttackFxCatalog.Signature(id);
                if (id == "lalabell")
                {
                    // 라라벨 음파 고리는 그림을 제 색 그대로 그린다 (고리 색 흰색 = 곱해도 그림 그대로) — 그래서 흰색이어도 된다
                    Assert.AreEqual(LineageRing.SoundRing, sig.Ring, id);
                    continue;
                }
                var tint = sig.RingTint;
                float spread = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)) - Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b));
                Assert.GreaterOrEqual(spread, 0.12f, $"{id}: 고리 색 {tint}이 거의 흰색");
            }
        }

        [Test]
        public void EveryCombatStyle_HasMatchingMod()
        {
            foreach (CombatStyle style in Enum.GetValues(typeof(CombatStyle)))
            {
                var mod = AttackFxCatalog.Style(style);
                Assert.AreEqual(style, mod.Style);
                Assert.Greater(mod.HitScale, 0f);
            }
            Assert.IsTrue(AttackFxCatalog.Style(CombatStyle.Snipe).Straight);
            Assert.IsTrue(AttackFxCatalog.Style(CombatStyle.Snipe).SnipeTrail);
            Assert.IsTrue(AttackFxCatalog.Style(CombatStyle.Assault).Slash);
            Assert.AreEqual(HitTier.Heavy, AttackFxCatalog.Style(CombatStyle.Snipe).Tier);
            Assert.AreEqual(HitTier.Heavy, AttackFxCatalog.Style(CombatStyle.Assault).Tier);
            // 잔향이: 저격·지원·힐은 멀리서, 나머지는 근접 베기
            Assert.IsTrue(AttackFxCatalog.Style(CombatStyle.Support).Ranged);
            Assert.IsTrue(AttackFxCatalog.Style(CombatStyle.Heal).Ranged);
            Assert.IsFalse(AttackFxCatalog.Style(CombatStyle.Defense).Ranged);
            Assert.IsFalse(AttackFxCatalog.Style(CombatStyle.Capture).Ranged);
        }

        // ───────────── 비행 시간 / 처치 보류 (ATK-09) ─────────────

        [Test]
        public void Snipe_FlightIsShortAndDistanceIndependent()
        {
            var look = AttackFxCatalog.Look("chalkak");
            var mod = AttackFxCatalog.Style(CombatStyle.Snipe);
            Assert.AreEqual(AttackFxCatalog.SnipeLife, AttackFxCatalog.FlightTime(look, mod, 80f, 1), 1e-4f);
            Assert.AreEqual(AttackFxCatalog.SnipeLife, AttackFxCatalog.FlightTime(look, mod, 600f, 1), 1e-4f);
        }

        [Test]
        public void DoubleSpeed_ShortensFlightTo60Percent()
        {
            var look = AttackFxCatalog.Look("mongdang");
            var mod = AttackFxCatalog.Style(CombatStyle.Support);
            float normal = AttackFxCatalog.FlightTime(look, mod, 240f, 1);
            float fast = AttackFxCatalog.FlightTime(look, mod, 240f, 2);
            Assert.AreEqual(normal * 0.6f, fast, 1e-4f);
            Assert.AreEqual(0.8f, AttackFxCatalog.SpeedPopScale(2), 1e-4f);
        }

        [Test]
        public void Flight_GrowsGentlyWithDistance_AndIsClamped()
        {
            var look = AttackFxCatalog.Look("jjakjjak");
            var mod = AttackFxCatalog.Style(CombatStyle.Defense);
            float near = AttackFxCatalog.FlightTime(look, mod, 10f, 1);
            float mid = AttackFxCatalog.FlightTime(look, mod, AttackFxCatalog.ReferenceDistance, 1);
            float far = AttackFxCatalog.FlightTime(look, mod, 5000f, 1);
            Assert.AreEqual(look.Life, mid, 1e-4f);
            Assert.Less(near, mid);
            Assert.Greater(far, mid);
            Assert.AreEqual(look.Life * 0.75f, near, 1e-4f);
            Assert.AreEqual(look.Life * 1.3f, far, 1e-4f);
        }

        [TestCase(0f, 0.14f)]
        [TestCase(0.05f, 0.14f)]
        [TestCase(0.2f, 0.22f)]
        [TestCase(0.5f, 0.30f)]
        public void KillHold_WaitsForLastProjectile(float remaining, float expected)
        {
            Assert.AreEqual(expected, AttackFxCatalog.KillHold(remaining), 1e-4f);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void KillHold_CoversEveryGuardianFlight(int speed)
        {
            // 1배속·2배속 모두 가장 긴 지킴이 비행(먼 거리, 라라벨 힐처럼 길게 튀는 것)도 처치 보류(상한 0.30초) 안에 닿는다 — 투사체 없이 사라지는 적이 없게
            foreach (string id in AttackFxCatalog.KnownLineages)
            {
                foreach (CombatStyle style in Enum.GetValues(typeof(CombatStyle)))
                {
                    var look = AttackFxCatalog.Look(id);
                    var mod = AttackFxCatalog.Style(style);
                    foreach (float unit in new[] { 0.75f, 1f, 1.3f })
                    {
                        float flight = AttackFxCatalog.FlightTime(look, mod, 5000f, speed, unit);
                        Assert.LessOrEqual(flight + 0.02f, AttackFxCatalog.KillHoldMax + 1e-4f, $"{id}/{style} x{speed} unit {unit}");
                        Assert.GreaterOrEqual(AttackFxCatalog.KillHold(flight), flight + 0.02f - 1e-4f, $"{id}/{style} x{speed}: 처치 보류가 마지막 도착보다 짧다");
                    }
                }
            }
        }

        [Test]
        public void Flight_IsMeasuredInScreenScale()
        {
            // 큰 화면(액터 배율 1.25)에서 같은 자리의 적은 픽셀 거리도 1.25배 — 비행 시간은 같아야 한다
            var look = AttackFxCatalog.Look("jjakjjak");
            var mod = AttackFxCatalog.Style(CombatStyle.Defense);
            float reference = AttackFxCatalog.FlightTime(look, mod, 200f, 1, 1f);
            float big = AttackFxCatalog.FlightTime(look, mod, 250f, 1, 1.25f);
            Assert.AreEqual(reference, big, 1e-4f);
        }

        // ───────────── 타격 사다리 (FX-01) ─────────────

        [Test]
        public void HitLadder_GrowsWithTier()
        {
            // FX-JUICE §2.1 (Appendix A): 넉백 8 / 12 / 16, 보스 3, 지속 피해 0 · 대상 정지 0 / 0.033 / 0.066 / 스킬 0.05
            Assert.AreEqual(8f, HitFeel.Knockback(HitTier.Normal, false));
            Assert.AreEqual(12f, HitFeel.Knockback(HitTier.Heavy, false));
            Assert.AreEqual(16f, HitFeel.Knockback(HitTier.Crit, false));
            Assert.AreEqual(3f, HitFeel.Knockback(HitTier.Crit, true));
            Assert.AreEqual(0f, HitFeel.Knockback(HitTier.Tick, false));
            Assert.AreEqual(0f, HitFeel.TargetStop(HitTier.Normal));
            Assert.AreEqual(0.033f, HitFeel.TargetStop(HitTier.Heavy), 1e-4f);
            Assert.AreEqual(0.066f, HitFeel.TargetStop(HitTier.Crit), 1e-4f);
            Assert.AreEqual(0.05f, HitFeel.TargetStop(HitTier.Skill), 1e-4f);
            Assert.AreEqual(HitFeel.KnockOut + HitFeel.KnockReturn, HitFeel.KnockbackTime, 1e-5f);
        }

        // ───────────── 손맛 v2 (FX-JUICE §2 / §3 — 순수 규칙) ─────────────

        [Test]
        public void TierFeel_LadderMonotonic()
        {
            var normal = HitFeel.Feel(HitTier.Normal);
            var heavy = HitFeel.Feel(HitTier.Heavy);
            var crit = HitFeel.Feel(HitTier.Crit);
            Assert.Less(normal.FirstFrame, heavy.FirstFrame);
            Assert.Less(heavy.FirstFrame, crit.FirstFrame);
            Assert.Less(normal.Core, heavy.Core);
            Assert.Less(heavy.Core, crit.Core);
            Assert.Less(normal.Knockback, heavy.Knockback);
            Assert.Less(heavy.Knockback, crit.Knockback);
            Assert.Less(normal.Streaks, heavy.Streaks);
            Assert.Less(heavy.Streaks, crit.Streaks);
            Assert.Less(normal.FlashAlpha, heavy.FlashAlpha);
            Assert.Less(heavy.FlashAlpha, crit.FlashAlpha);
            // 첫 프레임이 가장 크다 (자리 크기 이상), 번쩍임 버팀 ≤ 전체, 예전 함수는 표에서 읽는다
            foreach (HitTier tier in Enum.GetValues(typeof(HitTier)))
            {
                var feel = HitFeel.Feel(tier);
                Assert.AreEqual(tier, feel.Tier, tier.ToString());
                Assert.GreaterOrEqual(feel.FirstFrame, 1f, tier.ToString());
                Assert.LessOrEqual(feel.FlashHold, feel.FlashLife, tier.ToString());
                Assert.AreEqual(HitFeel.FlashTime(tier), feel.FlashLife, 1e-6f, tier.ToString());
                Assert.AreEqual(HitFeel.FlashStrength(tier), feel.FlashAlpha, 1e-6f, tier.ToString());
            }
            Assert.AreEqual(1.6f, crit.NumberScale, 1e-5f);
            Assert.AreEqual(0, HitFeel.Feel(HitTier.Tick).Streaks);
        }

        [Test]
        public void TierFeel_BossEchoGuardedModifiers()
        {
            var normalBoss = HitFeel.ForBoss(HitFeel.Feel(HitTier.Normal));
            Assert.LessOrEqual(normalBoss.FlashAlpha, 0.7f + 1e-5f, "보스 일반 피격 번쩍임 0.7 상한");
            Assert.AreEqual(3f, normalBoss.Knockback);
            Assert.AreEqual(0f, normalBoss.Freeze, "보스는 일반 피격에 멈추지 않는다");
            var critBoss = HitFeel.ForBoss(HitFeel.Feel(HitTier.Crit));
            Assert.AreEqual(0.033f, critBoss.Freeze, 1e-5f);
            Assert.AreEqual(2f, critBoss.Shiver);
            Assert.Less(critBoss.SquashX - 1f, HitFeel.Feel(HitTier.Crit).SquashX - 1f);

            var echo = HitFeel.ForEcho(HitFeel.Feel(HitTier.Normal));
            Assert.AreEqual(0.65f, echo.SizeScale, 1e-5f);
            Assert.AreEqual(2, echo.Streaks);
            Assert.AreEqual(0f, echo.Freeze);
            Assert.AreEqual(HitFeel.Feel(HitTier.Normal).FlashAlpha * 0.5f, echo.FlashAlpha, 1e-5f);

            var guarded = HitFeel.ForGuarded(HitFeel.Feel(HitTier.Heavy));
            Assert.AreEqual(0f, guarded.Knockback, "보호막에 막힌 명중은 밀리지 않는다");
            Assert.AreEqual(0.7f, guarded.SizeScale, 1e-5f);
        }

        [Test]
        public void ImpactSize_PeaksOnFirstFrame()
        {
            var f = HitFeel.Feel(HitTier.Normal);
            float settled = 132f;
            float size0 = settled * f.FirstFrame;
            float life = f.Life;
            Assert.AreEqual(size0, JuiceEase.ImpactSize(0f, life, f.Hold, f.Settle, size0, settled, 0.92f), 1e-3f);
            Assert.AreEqual(size0, JuiceEase.ImpactSize(f.Hold, life, f.Hold, f.Settle, size0, settled, 0.92f), 1e-2f);
            Assert.AreEqual(settled, JuiceEase.ImpactSize(f.Hold + f.Settle, life, f.Hold, f.Settle, size0, settled, 0.92f), 1e-2f);
            Assert.AreEqual(settled * 0.92f, JuiceEase.ImpactSize(life, life, f.Hold, f.Settle, size0, settled, 0.92f), 1e-2f);
            // 처치 꽃처럼 끝 배율이 1보다 크면 끝에서 부푼다
            Assert.AreEqual(settled * 1.3f, JuiceEase.ImpactSize(0.42f, 0.42f, 0.033f, 0.07f, settled * 1.35f, settled, 1.3f), 1e-2f);
            // 끝 배율 < 1이면 첫 프레임 뒤로는 줄기만 한다
            float previous = float.MaxValue;
            for (int i = 0; i <= 24; i++)
            {
                float size = JuiceEase.ImpactSize(life * i / 24f, life, f.Hold, f.Settle, size0, settled, 0.92f);
                Assert.LessOrEqual(size, previous + 1e-3f, "t=" + i);
                previous = size;
            }
        }

        [Test]
        public void Spring_ReboundsOnceThenSettles()
        {
            Assert.AreEqual(1f, JuiceEase.SpringDisplacement(0f, 5f, 0.45f), 1e-5f);
            float rebound = JuiceEase.SpringDisplacement(0.112f, 5f, 0.45f);
            Assert.Less(rebound, -0.15f, "한 번 반대로 되튄다");
            Assert.Greater(rebound, -0.3f);
            Assert.Less(Mathf.Abs(JuiceEase.SpringDisplacement(0.3f, 5f, 0.45f)), 0.1f, "300ms면 거의 멈춘다");
        }

        [Test]
        public void Kick_DecaysAndCaps()
        {
            var kick = new FxKick();
            Assert.IsTrue(kick.Add(Vector2.right, 10f));
            float peak = 0f;
            for (int i = 0; i < 6; i++) peak = Mathf.Max(peak, kick.Tick(1f / 60f).magnitude);
            Assert.LessOrEqual(peak, HitFeel.KickMax + 1e-4f, "킥은 KickMax(5px)를 넘지 않는다");
            Assert.Greater(peak, 3.5f);
            // 더 작은 킥은 더해지지 않는다 (지금 밀린 양이 더 크면 무시)
            Assert.IsFalse(kick.Add(Vector2.up, 1f));
            for (int i = 0; i < 12; i++) kick.Tick(1f / 60f);
            Assert.Less(kick.Current.magnitude, 0.2f, "0.2초 뒤면 거의 돌아온다");
            for (int i = 0; i < 30; i++) kick.Tick(1f / 60f);
            Assert.IsFalse(kick.Active);
            Assert.AreEqual(Vector2.zero, kick.Current);
            // 꼭대기 뒤 140ms에 약 0.05
            Assert.AreEqual(0.05f, FxKick.Profile(0.03f + 0.14f), 0.01f);
        }

        [Test]
        public void Kick_Round2Strengths_ReadOnDevice()
        {
            // 2차 조율: 540 폭 기기 화면에서 1.5px(3px)는 보이지 않는다 → 치명타 4 · 저격 3 · 상한 5
            Assert.AreEqual(4f, HitFeel.KickCrit, 1e-6f);
            Assert.AreEqual(3f, HitFeel.KickSnipe, 1e-6f);
            Assert.AreEqual(5f, HitFeel.KickMax, 1e-6f);
            Assert.Greater(HitFeel.KickCrit, HitFeel.KickSnipe, "치명타가 저격보다 세다");
            Assert.Greater(HitFeel.KickCrit, HitFeel.KickBossCrit, "보스는 묵직하게 덜 밀린다");
            foreach (float px in new[]
                     {
                         HitFeel.KickCrit, HitFeel.KickBossCrit, HitFeel.KickSnipe, HitFeel.KickShop, HitFeel.KickSiege, HitFeel.KickKill, HitFeel.KickSkill,
                         HitFeel.KickShield,
                     })
                Assert.LessOrEqual(px, HitFeel.KickMax, "채널 킥은 상한 안");
            // 치명타 킥이 상한에 잘리지 않고 그대로 꼭대기까지 간다
            var kick = new FxKick();
            Assert.IsTrue(kick.Add(Vector2.up, HitFeel.KickCrit));
            float peak = 0f;
            for (int i = 0; i < 6; i++) peak = Mathf.Max(peak, kick.Tick(1f / 60f).magnitude);
            Assert.AreEqual(HitFeel.KickCrit, peak, 0.1f);
        }

        [Test]
        public void Zoom_PeaksAndReturns()
        {
            var zoom = new FxZoom();
            Assert.IsTrue(zoom.Punch(HitFeel.ZoomBossKill));
            float peak = 1f;
            for (int i = 0; i < 6; i++) peak = Mathf.Max(peak, zoom.Tick(1f / 60f));
            Assert.AreEqual(HitFeel.ZoomBossKill, peak, 0.002f);
            Assert.IsFalse(zoom.Punch(1.01f), "지금보다 작은 펀치는 바꾸지 않는다");
            for (int i = 0; i < 20; i++) zoom.Tick(1f / 60f);
            Assert.AreEqual(1f, zoom.Current, 1e-4f);
            Assert.IsFalse(zoom.Active);
        }

        [Test]
        public void ShakeScaleZero_NoCameraMotion()
        {
            var trauma = new TraumaShake();
            trauma.Add(1f, Vector2.up);
            var kick = new FxKick();
            kick.Add(Vector2.right, 4f);
            var zoom = new FxZoom();
            zoom.Punch(HitFeel.ZoomLamp);
            for (int i = 0; i < 30; i++)
            {
                trauma.Tick(1f / 60f, 0f, out var offset, out float rotation);
                var k = kick.Tick(1f / 60f);
                float z = zoom.Tick(1f / 60f);
                HitFeel.CameraPose(offset, k, z, 0f, new Vector2(120f, -40f), out var position, out float scale);
                Assert.AreEqual(Vector2.zero, position, "흔들림 0%면 전장이 움직이지 않는다");
                Assert.AreEqual(1f, scale, "흔들림 0%면 확대도 없다");
                Assert.AreEqual(0f, rotation);
            }
            // 배율 1이면 확대 중심이 제자리에 남는다: z·P + (1 − z)·P = P
            var pivot = new Vector2(200f, 100f);
            HitFeel.CameraPose(Vector2.zero, Vector2.zero, 1.03f, 1f, pivot, out var shift, out float full);
            Assert.AreEqual(1.03f, full, 1e-5f);
            Assert.Less((pivot * full + shift - pivot).magnitude, 1e-3f);
        }

        [Test]
        public void Budgets_PerQuality()
        {
            Assert.AreEqual(420, HitFeel.ParticleBudget(FxQuality.High));
            Assert.AreEqual(240, HitFeel.ParticleBudget(FxQuality.Medium));
            Assert.AreEqual(130, HitFeel.ParticleBudget(FxQuality.Low));
            Assert.AreEqual(24, HitFeel.MaxNumbers(FxQuality.High));
            Assert.AreEqual(16, HitFeel.MaxNumbers(FxQuality.Medium));
            Assert.AreEqual(10, HitFeel.MaxNumbers(FxQuality.Low));
            Assert.AreEqual(96, HitFeel.GroundBudget(FxQuality.High));
            Assert.AreEqual(6, HitFeel.HaloCap(FxQuality.High));
            Assert.AreEqual(2, HitFeel.HaloCap(FxQuality.Low));
            Assert.AreEqual(0, HitFeel.DecalCap(FxQuality.Low));
            Assert.AreEqual(48, HitFeel.FrameSpawnCap(FxQuality.High));
            Assert.AreEqual(24, HitFeel.FrameSpawnCap(FxQuality.Low));
            Assert.AreEqual(40, JuiceGates.LampRain(FxQuality.High, 0.8f));
            Assert.AreEqual(18, JuiceGates.LampRain(FxQuality.High, 0.3f));
            Assert.IsFalse(JuiceGates.Misprint(FxQuality.High, true), "번쩍임 줄이기면 인쇄 어긋남 없음");
            Assert.IsFalse(JuiceGates.Misprint(FxQuality.Low, false));
            Assert.AreEqual(0.6f, JuiceGates.CoreIntensity(true), 1e-5f);
        }

        [Test]
        public void Governor_FullImpactEvery90ms()
        {
            var gov = new FxGovernor();
            Assert.IsTrue(gov.TryFullImpact(7, 1.00f, out _, out int chain));
            Assert.AreEqual(1, chain);
            Assert.IsFalse(gov.TryFullImpact(7, 1.05f, out _, out chain), "같은 대상 90ms 안은 미니");
            Assert.AreEqual(2, chain);
            Assert.IsTrue(gov.TryFullImpact(8, 1.05f, out _, out _), "다른 대상은 따로 센다");
            // 사슬의 3번째는 리듬 강조 (늘 풀)
            Assert.IsTrue(gov.TryFullImpact(7, 1.07f, out bool accent, out chain));
            Assert.IsTrue(accent);
            Assert.AreEqual(3, chain);
            Assert.IsTrue(gov.TryFullImpact(7, 1.20f, out accent, out _));
            Assert.IsFalse(accent);
            // 0.6초 넘게 쉬면 사슬이 끊긴다
            gov.TryFullImpact(7, 2.5f, out _, out chain);
            Assert.AreEqual(1, chain);
            Assert.IsTrue(gov.TryFullImpact(-1, 2.5f, out _, out _), "모르는 대상은 늘 풀");
        }

        [Test]
        public void Governor_DensityCountsTheWindowBefore()
        {
            var gov = new FxGovernor();
            for (int i = 0; i < 10; i++) gov.NoteArrival(1f + i * 0.02f);
            Assert.AreEqual(10, gov.Density(1.2f));
            Assert.AreEqual(0, gov.Density(0.5f), "앞으로 올 도착은 세지 않는다");
            Assert.AreEqual(0, gov.Density(2f), "0.25초보다 오래된 도착은 세지 않는다");
        }

        [Test]
        public void Governor_KillBurstsCapped()
        {
            var gov = new FxGovernor();
            Assert.IsTrue(gov.TryKillBurst(1f));
            Assert.IsTrue(gov.TryKillBurst(1.02f));
            Assert.IsTrue(gov.TryKillBurst(1.05f));
            Assert.IsFalse(gov.TryKillBurst(1.08f), "0.2초에 풀 처치 꽃 3개까지");
            Assert.IsTrue(gov.TryKillBurst(1.5f));
            // 큰 터짐: 2개 그대로 → +33ms → +50ms → 작게(−1)
            Assert.AreEqual(0f, gov.BigBurstDelay(3f), 1e-5f);
            Assert.AreEqual(0f, gov.BigBurstDelay(3f), 1e-5f);
            Assert.AreEqual(0.033f, gov.BigBurstDelay(3f), 1e-5f);
            Assert.AreEqual(0.05f, gov.BigBurstDelay(3f), 1e-5f);
            Assert.AreEqual(-1f, gov.BigBurstDelay(3f));
        }

        [Test]
        public void Governor_StreakAndMultiKill()
        {
            var gov = new FxGovernor();
            int streak = 0;
            int multi = 0;
            for (int i = 0; i < 5; i++) streak = gov.NoteKill(1f + i * 0.04f, new Vector2(i * 10f, 0f), out multi, out _);
            Assert.AreEqual(5, streak);
            Assert.AreEqual(5, multi, "0.25초 안의 처치는 한꺼번에");
            streak = gov.NoteKill(2f, Vector2.zero, out multi, out _);
            Assert.AreEqual(6, streak, "1.2초 안이면 연속");
            Assert.AreEqual(1, multi);
            streak = gov.NoteKill(4f, Vector2.zero, out _, out _);
            Assert.AreEqual(1, streak, "1.2초가 지나면 처음부터");
        }

        [Test]
        public void Governor_ScreenGates()
        {
            var gov = new FxGovernor();
            Assert.IsTrue(gov.TryFieldFlash(1f, false));
            Assert.IsFalse(gov.TryFieldFlash(1.5f, false), "전장 전체 번쩍임은 1초에 하나");
            Assert.IsTrue(gov.TryFieldFlash(1.6f, true), "불 밝히기·보스 처치는 자리를 차지한다");
            Assert.IsFalse(gov.TryFieldFlash(2.4f, false), "차지한 뒤 1초 동안 막는다");
            Assert.IsTrue(gov.TryFieldFlash(2.7f, false));
            Assert.IsTrue(gov.TryGlobalStop(1f));
            Assert.IsFalse(gov.TryGlobalStop(2f), "전체 정지는 1.5초에 하나");
            Assert.IsTrue(gov.TryZoom(1f, false));
            Assert.IsFalse(gov.TryZoom(1.5f, false));
            Assert.IsTrue(gov.TryZoom(1.5f, true));
            Assert.IsTrue(gov.TryKick(KickChannel.Crit, 1f));
            Assert.IsFalse(gov.TryKick(KickChannel.Crit, 1.3f));
            Assert.IsTrue(gov.TryKick(KickChannel.Shop, 1.3f), "채널마다 따로");
        }

        [TestCase(5, true)]
        [TestCase(10, true)]
        [TestCase(20, true)]
        [TestCase(40, true)]
        [TestCase(60, true)]
        [TestCase(80, true)]
        [TestCase(15, false)]
        [TestCase(30, false)]
        [TestCase(50, false)]
        public void StreakMilestones(int streak, bool milestone)
        {
            Assert.AreEqual(milestone, HitFeel.IsStreakMilestone(streak));
        }

        [Test]
        public void SlowMotion_StopsThenSlowsThenRecovers()
        {
            var clock = new FxSlowMotion();
            clock.Trigger(0.12f, 0.3f, 0.18f);
            Assert.AreEqual(0f, clock.Tick(0.05f));
            Assert.AreEqual(0f, clock.Tick(0.05f));
            Assert.AreEqual(0f, clock.Tick(0.05f));
            Assert.AreEqual(0.3f, clock.Tick(0.1f), 1e-4f);
            Assert.AreEqual(0.3f, clock.Tick(0.1f), 1e-4f);
            Assert.AreEqual(1f, clock.Tick(0.1f));
            Assert.IsFalse(clock.Active);
        }

        // ───────────── 흔들림 예산 (FX-02) ─────────────

        [Test]
        public void Trauma_IsClampedAndDecays()
        {
            var shake = new TraumaShake();
            shake.Add(0.7f);
            shake.Add(0.7f);
            Assert.AreEqual(1f, shake.Trauma, 1e-4f);
            // 초당 1.4씩 줄어든다: 0.25초 뒤 약 0.65, 1초면 0
            for (int i = 0; i < 15; i++) shake.Tick(1f / 60f, 1f, out _, out _);
            Assert.AreEqual(1f - TraumaShake.Decay * 0.25f, shake.Trauma, 0.02f);
            for (int i = 0; i < 60; i++) shake.Tick(1f / 60f, 1f, out _, out _);
            Assert.AreEqual(0f, shake.Trauma, 1e-4f);
        }

        [Test]
        public void Trauma_OffsetStaysInsideBudget_AndRespectsSetting()
        {
            var shake = new TraumaShake();
            shake.Add(1f, Vector2.right);
            for (int i = 0; i < 30; i++)
            {
                shake.Tick(1f / 60f, 1f, out var offset, out float rotation);
                Assert.LessOrEqual(offset.magnitude, TraumaShake.MaxOffset * 1.5f + 1e-3f);
                // Perlin 잡음은 0~1을 아주 조금 넘을 수 있다
                Assert.LessOrEqual(Mathf.Abs(rotation), TraumaShake.MaxRotation * 1.1f);
            }
            var off = new TraumaShake();
            off.Add(1f);
            off.Tick(1f / 60f, 0f, out var still, out float noTurn);
            Assert.AreEqual(Vector2.zero, still, "흔들림 끄기면 전장이 움직이지 않는다");
            Assert.AreEqual(0f, noTurn);
        }

        [Test]
        public void ShopHits_AreCappedPerSecond()
        {
            var shake = new TraumaShake();
            // 한 프레임에 적 20마리가 동시에 닿아도 초당 합계 0.25까지
            for (int i = 0; i < 20; i++) shake.AddShopHit(HitFeel.ShopHitTrauma, Vector2.down);
            Assert.AreEqual(HitFeel.ShopTraumaPerSecond, shake.Trauma, 1e-4f);
        }

        [Test]
        public void CritTrauma_OncePerInterval()
        {
            var shake = new TraumaShake();
            shake.AddCrit();
            shake.AddCrit();
            Assert.AreEqual(HitFeel.CritTrauma, shake.Trauma, 1e-4f);
        }

        [Test]
        public void BossKillTrauma_PeakInsideBossBand()
        {
            // 흔들림 꼭대기 = 30 × trauma² (잡음 ±1 기준 — 이론 상한). 보스 처치 목표 띠 8~14px는 캡처로 잰 실제 흔들림:
            // Wave 3에서 0.68 → 0.72로 올렸다 (0.68은 캡처 7.81px로 띠 아래, 0.72 ≈ 8.7px). 이론 상한은 ≈ 15.6px — 16px 안 (0.8이면 19.2px로 넘쳤다)
            float cap = TraumaShake.MaxOffset * HitFeel.BossKillTrauma * HitFeel.BossKillTrauma;
            Assert.LessOrEqual(cap, 16f, "보스 처치 흔들림 이론 꼭대기 ≤ 16px");
            Assert.GreaterOrEqual(cap, 8f, "보스 처치 흔들림 꼭대기 ≥ 8px");
            Assert.Less(HitFeel.BossKillTrauma, HitFeel.LampTrauma, "불 밝히기가 가장 큰 순간");
            Assert.Greater(HitFeel.BossKillTrauma, HitFeel.BossSiegeTrauma, "처치가 포위보다 크다");
            // 실제 잡음으로도 넘지 않는다 (60fps 0.5초, 방향 없음)
            var shake = new TraumaShake();
            shake.Add(HitFeel.BossKillTrauma);
            float peak = 0f;
            for (int i = 0; i < 30; i++)
            {
                shake.Tick(1f / 60f, 1f, out var offset, out _);
                peak = Mathf.Max(peak, offset.magnitude);
            }
            Assert.LessOrEqual(peak, cap * 1.42f, "축마다 ±1 — 대각선이어도 √2배 안");
        }

        [Test]
        public void Trauma_ClearRestartsNoise_SameEventSameShake()
        {
            // 연출 시험장이 항목마다 ClearParticles → TraumaShake.Clear: 앞 항목들이 흘린 시간과 상관없이 같은 사건이면 같은 흔들림
            var fresh = new TraumaShake();
            var used = new TraumaShake();
            used.Add(0.5f);
            for (int i = 0; i < 97; i++) used.Tick(1f / 60f, 1f, out _, out _);
            used.Clear();
            fresh.Add(HitFeel.BossKillTrauma);
            used.Add(HitFeel.BossKillTrauma);
            for (int i = 0; i < 20; i++)
            {
                fresh.Tick(1f / 60f, 1f, out var a, out float ra);
                used.Tick(1f / 60f, 1f, out var b, out float rb);
                Assert.Less((a - b).magnitude, 1e-4f, "frame " + i);
                Assert.AreEqual(ra, rb, 1e-4f);
            }
        }

        [Test]
        public void Confetti_FamilyPiecesReadableOnDevice()
        {
            // 처치 '풀려남' 조각이 540x960에서 7~11px 점으로 보이던 것 → 처치 갈래 ×1.45 (14~22 → 20~32px), 보스 ×1.15, 축하 그대로
            Assert.AreEqual(1.45f, HitFeel.ConfettiSizeScale(ConfettiSet.Dust), 1e-6f);
            Assert.AreEqual(1.45f, HitFeel.ConfettiSizeScale(ConfettiSet.Moth), 1e-6f);
            Assert.AreEqual(1.45f, HitFeel.ConfettiSizeScale(ConfettiSet.Metal), 1e-6f);
            Assert.AreEqual(1.45f, HitFeel.ConfettiSizeScale(ConfettiSet.Shadow), 1e-6f);
            Assert.AreEqual(1.15f, HitFeel.ConfettiSizeScale(ConfettiSet.Boss), 1e-6f);
            Assert.AreEqual(1f, HitFeel.ConfettiSizeScale(ConfettiSet.Celebration), 1e-6f);
            Assert.GreaterOrEqual(14f * HitFeel.ConfettiSizeScale(ConfettiSet.Dust) * 0.5f, 10f, "가장 작은 조각도 기기 화면에서 10px 이상");
        }

        // ───────────── 피해 숫자 표기 (FX-03) ─────────────

        [TestCase(7, "7")]
        [TestCase(1234, "1,234")]
        [TestCase(9999, "9,999")]
        [TestCase(12345, "12.3A")]
        public void Numbers_UseCommaBelowTenThousand(int value, string expected)
        {
            Assert.AreEqual(expected, BattleEase.Int(value));
        }

        [Test]
        public void Numbers_HealShopAndBlockForms()
        {
            Assert.AreEqual("+1,200", BattleEase.Plus(1200));
            Assert.AreEqual("-35", BattleEase.Minus(35));
            Assert.AreEqual("막음 48", BattleEase.Blocked(48));
        }

        [Test]
        public void Numbers_AboveIntRange_DoNotOverflow()
        {
            // 후반 피해(2^31 이상)는 int로 바꾸지 않는다: 넘치거나 int 상한(2.14C)에 멈추지 않고 제 값으로 (A = 천, B = 백만, C = 십억)
            Assert.AreEqual("3.00C", BattleEase.Int(3e9));
            Assert.AreEqual("-3.00C", BattleEase.Minus(3e9));
            Assert.AreEqual(NumberFormatter.Format(1e30), BattleEase.Int(1e30));
            Assert.AreEqual(NumberFormatter.Format(4.56e12), BattleEase.Int(4.56e12));
            Assert.AreEqual("+" + NumberFormatter.Format(8.8e20), BattleEase.Plus(8.8e20));
            Assert.DoesNotThrow(() => BattleEase.Minus(int.MinValue));
            Assert.DoesNotThrow(() => BattleEase.Int(int.MinValue));
            Assert.AreEqual("-2.14C", BattleEase.Minus(int.MinValue));
            Assert.AreEqual("0", BattleEase.Int(double.NaN));
        }

        [Test]
        public void Numbers_LargeValues_AreCachedPerDisplayedValue()
        {
            // 같은 표시값(앞 3자리·단위)이면 같은 문자열 — 매 명중마다 새로 만들지 않는다
            Assert.AreSame(BattleEase.Int(12_345.0), BattleEase.Int(12_399.0));
            Assert.AreEqual("12.3A", BattleEase.Int(12_399.0));
            Assert.AreSame(BattleEase.Crit(7.77e15), BattleEase.Crit(7.77e15));
        }

        [Test]
        public void Numbers_LargeCache_HoldsMoreThanOneUnitGroup()
        {
            // 단위 표기 칸 캐시 (LargeSlots 4,096 — 이어진 표시값 4,096개는 서로 다른 칸): 1만 ~ 1,000만 사이 표시값 2,700개
            // (10.0A ~ 99.9A · 100A ~ 999A · 1.00B ~ 9.99B — 단위 두 개에 걸친다)를 한 번씩 찍은 뒤에도 처음 만든 "12.3A"(칸 3623)가 그대로다.
            // 예전 사전은 1,024개가 차면 통째로 비워 여기서 글자를 새로 만들었다 (후반 전투 숫자가 데워진 뒤에도 계속 할당)
            string first = BattleEase.Int(12_345.0);
            Assert.AreEqual("12.3A", first);
            for (int digits = 100; digits <= 999; digits++)
            {
                BattleEase.Int(digits * 100.0);
                BattleEase.Int(digits * 1_000.0);
                BattleEase.Int(digits * 10_000.0);
            }
            Assert.AreSame(first, BattleEase.Int(12_345.0));
            Assert.AreSame(first, BattleEase.Int(12_399.0));
        }

        [Test]
        public void Numbers_CritAndMarkCarryShapes_NotJustColour()
        {
            // 색만으로 뜻을 전하지 않는다 (ATK-17): 치명타 ★, 표식 증폭 ♥
            StringAssert.Contains("★", BattleEase.Crit(120));
            StringAssert.EndsWith("120", BattleEase.Crit(120));
            StringAssert.Contains("♥", BattleEase.Mark(64));
            StringAssert.EndsWith("64", BattleEase.Mark(64));
            StringAssert.Contains("♥", BattleEase.Mark(5e9));
        }

        [Test]
        public void NumberColors_GoldIsCritOnly()
        {
            foreach (DamageNumberKind kind in Enum.GetValues(typeof(DamageNumberKind)))
            {
                if (kind == DamageNumberKind.Crit) continue;
                Assert.AreNotEqual(UITheme.Gold, BattleFx.NumberColor(kind), kind.ToString());
            }
            Assert.AreEqual(UITheme.Gold, BattleFx.NumberColor(DamageNumberKind.Crit));
            Assert.AreEqual(1.6f, BattleFx.NumberScale(DamageNumberKind.Crit), 1e-4f);
            Assert.AreEqual(1.2f, BattleFx.NumberScale(DamageNumberKind.Boss), 1e-4f);
        }

        [Test]
        public void Numbers_RhythmAccentIsBigger_ButSmallerThanCrit()
        {
            // FX-JUICE E6: 같은 대상 사슬의 3번째마다(리듬 강조) 피해 숫자 ×1.2 — 치명타 쾅(×1.6)과 헷갈리지 않게 그보다 작다
            Assert.AreEqual(1.2f, AttackFxCatalog.RhythmNumberScale, 1e-6f);
            Assert.Less(AttackFxCatalog.RhythmNumberScale, BattleFx.NumberScale(DamageNumberKind.Crit));
        }

        [Test]
        public void NumberGradient_CritIsGold()
        {
            // §4.2: 치명타만 금빛 그라데이션 (FFE9A8 → F2C46B = UITheme.Gold). 다른 뜻은 어떤 물감(금빛 강조색 포함)을 받아도 치명타 금빛이 아니다
            BattleFx.NumberGradient(DamageNumberKind.Crit, default, out var critTop, out var critBottom);
            AssertColor(new Color(0xFF / 255f, 0xE9 / 255f, 0xA8 / 255f, 1f), critTop, "crit top");
            AssertColor(UITheme.Gold, critBottom, "crit bottom");
            var tints = new[] { default(Color), UITheme.Gold, UITheme.Lamp, UITheme.SoftBlue, UITheme.DustyPink };
            foreach (DamageNumberKind kind in Enum.GetValues(typeof(DamageNumberKind)))
            {
                if (kind == DamageNumberKind.Crit) continue;
                foreach (var tint in tints)
                {
                    BattleFx.NumberGradient(kind, tint, out var top, out var bottom);
                    string label = kind + " tint " + tint;
                    Assert.IsFalse(Near(top, critTop) || Near(top, critBottom), label + ": 위쪽이 치명타 금빛");
                    Assert.IsFalse(Near(bottom, critTop) || Near(bottom, critBottom), label + ": 아래쪽이 치명타 금빛");
                    Assert.IsFalse(Near(bottom, UITheme.Gold), label + ": 금색은 치명타 전용");
                }
            }
        }

        [Test]
        public void SkillNumberTint_GoldTurnsLavender()
        {
            // 스킬 숫자는 시전 계보 강조색으로 물들지만, 금빛 강조색(똑단이·우두커니·라라벨)과 '없음'은 라벤더 — 치명타와 헷갈리지 않게
            AssertColor(UITheme.Lavender, BattleFx.SkillNumberTint(UITheme.Gold), "gold");
            AssertColor(UITheme.Lavender, BattleFx.SkillNumberTint(UITheme.Lamp), "lamp");
            AssertColor(UITheme.Lavender, BattleFx.SkillNumberTint(default), "none");
            var blue = BattleFx.SkillNumberTint(BattleEase.WithAlpha(UITheme.SoftBlue, 0.5f));
            AssertColor(new Color(UITheme.SoftBlue.r, UITheme.SoftBlue.g, UITheme.SoftBlue.b, 1f), blue, "soft blue keeps its hue, opaque");
            AssertColor(UITheme.DustyPink, BattleFx.SkillNumberTint(UITheme.DustyPink), "dusty pink");
            BattleFx.NumberGradient(DamageNumberKind.Skill, UITheme.Gold, out _, out var skillBottom);
            Assert.Less(Mathf.Abs(skillBottom.b - UITheme.Lavender.b * 0.85f), 1e-3f, "금빛 강조색 스킬 숫자 아래쪽 = 라벤더 × 0.85");
        }

        [Test]
        public void NumberPrecedence()
        {
            // §4.2: 치명타 > 표식 > 스킬 > 불 밝히기 > 보스 > 강타 > 일반, 지속 피해 틱은 늘 Dot
            Assert.AreEqual(DamageNumberKind.Crit, AttackFxCatalog.NumberKind(HitTier.Crit, true, true));
            Assert.AreEqual(DamageNumberKind.Mark, AttackFxCatalog.NumberKind(HitTier.Skill, true, true));
            Assert.AreEqual(DamageNumberKind.Mark, AttackFxCatalog.NumberKind(HitTier.Lamp, true, false));
            Assert.AreEqual(DamageNumberKind.Skill, AttackFxCatalog.NumberKind(HitTier.Skill, false, true));
            Assert.AreEqual(DamageNumberKind.Lamp, AttackFxCatalog.NumberKind(HitTier.Lamp, false, true));
            Assert.AreEqual(DamageNumberKind.Boss, AttackFxCatalog.NumberKind(HitTier.Heavy, false, true));
            Assert.AreEqual(DamageNumberKind.Boss, AttackFxCatalog.NumberKind(HitTier.Normal, false, true));
            Assert.AreEqual(DamageNumberKind.Heavy, AttackFxCatalog.NumberKind(HitTier.Heavy, false, false));
            Assert.AreEqual(DamageNumberKind.Normal, AttackFxCatalog.NumberKind(HitTier.Normal, false, false));
            Assert.AreEqual(DamageNumberKind.Mark, AttackFxCatalog.NumberKind(HitTier.Normal, true, false));
            Assert.AreEqual(DamageNumberKind.Dot, AttackFxCatalog.NumberKind(HitTier.Tick, false, false));
            Assert.AreEqual(DamageNumberKind.Dot, AttackFxCatalog.NumberKind(HitTier.Tick, true, true), "지속 피해 틱은 표식·보스여도 Dot");
        }

        // ───────────── 예산 몫 · 번쩍임 줄이기 (§2.4 / §2.5 / §3.4) ─────────────

        [Test]
        public void Budgets_ReserveHeroAndGroundSlots()
        {
            // 별빛 층이 남기는 영웅 층 몫은 꼬리 층 문(20칸 초과)보다 작아야 첫 빛줄기·색종이가 몫을 먹지 않는다
            Assert.Greater(HitFeel.HeroReserve, 0);
            Assert.Less(HitFeel.HeroReserve, 20);
            Assert.Less(HitFeel.HeroReserve * 4, HitFeel.ParticleBudget(FxQuality.Low), "낮음 예산의 1/4 미만");
            Assert.Greater(HitFeel.GroundReserve, 0);
            Assert.Less(HitFeel.GroundReserve * 2, HitFeel.GroundBudget(FxQuality.Low));
            Assert.Greater(HitFeel.LampKillReserve, 0);
        }

        [Test]
        public void ReduceFlash_DimsLightRings_NotPaintedRings()
        {
            // 번쩍임 줄이기가 꺼져 있으면 고리 세기는 그대로
            Assert.AreEqual(1f, JuiceGates.RingLight(false, 0.9f), 1e-6f);
            Assert.AreEqual(1f, JuiceGates.RingLight(false, 0f), 1e-6f);
            // 켜면 빛(가산) 고리만 코어 별처럼 ×0.6까지 — 보통 알파 고리(칠)는 그대로
            Assert.AreEqual(JuiceGates.CoreIntensity(true), JuiceGates.RingLight(true, 1f), 1e-6f);
            Assert.AreEqual(1f, JuiceGates.RingLight(true, 0f), 1e-6f);
            Assert.AreEqual(0.64f, JuiceGates.RingLight(true, 0.9f), 1e-5f, "치명타 크림 고리 (가산 0.9)");
            Assert.Less(JuiceGates.RingLight(true, 0.85f), JuiceGates.HaloIntensity(true), "겹치는 순수 가산 고리는 후광보다 더 줄인다");
        }

        [Test]
        public void ReduceFlash_DimsLampLightLayers()
        {
            Assert.AreEqual(1f, JuiceGates.LampLight(false), 1e-6f);
            Assert.AreEqual(0.5f, JuiceGates.LampLight(true), 1e-6f);
            Assert.Less(JuiceGates.LampLight(true), JuiceGates.HaloIntensity(true), "불 밝히기 빛 층은 후광보다 더 줄인다 (겹치는 순수 가산 층)");
            Assert.AreEqual(10, JuiceGates.LampRain(FxQuality.Low, 1f));
            Assert.AreEqual(24, JuiceGates.LampRain(FxQuality.Medium, 1f));
        }

        // ───────────── 화면 효과 큐 (§3.3 ScreenCue — 합치기 · 우선순위 · 이르게 터뜨리지 않기) ─────────────

        static FxCue Cue(FxCueKind kind, float at, byte code = 0, float a = 0f, bool force = false) =>
            new() { Kind = kind, At = at, Code = code, A = a, Force = force };

        [Test]
        public void CueQueue_MergesKicksPerChannel_KeepsEarliest()
        {
            var queue = new FxCueQueue();
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Kick, 1.3f, (byte)KickChannel.Kill, 2f)));
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Kick, 1.2f, (byte)KickChannel.Kill, 2f)));
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Kick, 1.4f, (byte)KickChannel.Kill, 2f)));
            Assert.AreEqual(1, queue.Count, "같은 채널 간격(0.5초) 안의 킥은 하나");
            Assert.AreEqual(2, queue.Merged);
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Kick, 1.25f, (byte)KickChannel.Crit, 3f)));
            Assert.AreEqual(2, queue.Count, "채널마다 따로");
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Kick, 1.25f + HitFeel.KickInterval(KickChannel.Kill), (byte)KickChannel.Kill, 2f)));
            Assert.AreEqual(3, queue.Count, "간격보다 멀리 떨어진 킥은 둘 다 터질 수 있다");
            Assert.IsFalse(queue.TakeDue(1.19f, 0f, out _), "제 시각 전에는 꺼내지 않는다");
            Assert.IsTrue(queue.TakeDue(1.2f, 0f, out var first));
            Assert.AreEqual(1.2f, first.At, 1e-6f, "남은 처치 킥은 가장 이른 것");
            Assert.AreEqual((byte)KickChannel.Kill, first.Code);
        }

        [Test]
        public void CueQueue_MergesMisprintHapticAndZoom()
        {
            var queue = new FxCueQueue();
            queue.Enqueue(Cue(FxCueKind.Misprint, 2f));
            queue.Enqueue(Cue(FxCueKind.Misprint, 1.9f));
            queue.Enqueue(Cue(FxCueKind.Haptic, 1f, (byte)HapticKind.Heavy));
            queue.Enqueue(Cue(FxCueKind.Haptic, 1.1f, (byte)HapticKind.Heavy));
            queue.Enqueue(Cue(FxCueKind.Haptic, 1.1f, (byte)HapticKind.Medium));
            Assert.AreEqual(3, queue.Count, "인쇄 어긋남 하나 · 진동은 종류마다 하나");
            queue.Enqueue(Cue(FxCueKind.Zoom, 1f, 0, HitFeel.ZoomMultiKill, true));
            queue.Enqueue(Cue(FxCueKind.Zoom, 1.1f, 0, HitFeel.ZoomBossKill));
            Assert.AreEqual(4, queue.Count, "줌은 하나 (센 쪽)");
            int zooms = 0;
            while (queue.TakeDue(10f, 10f, out var cue))
            {
                if (cue.Kind == FxCueKind.Misprint) Assert.AreEqual(1.9f, cue.At, 1e-6f);
                if (cue.Kind != FxCueKind.Zoom) continue;
                zooms++;
                Assert.AreEqual(HitFeel.ZoomBossKill, cue.A, 1e-6f, "센 줌이 남는다");
                Assert.IsTrue(cue.Force, "강제(보스·불 밝히기)는 어느 쪽에서 왔든 남는다");
            }
            Assert.AreEqual(1, zooms);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void CueQueue_FullQueue_EvictsLowerPriority_NeverFiresEarly()
        {
            var queue = new FxCueQueue();
            // 큐를 서로 다른 채널·시각의 킥으로 가득 채운다 (합쳐지지 않게 1초 간격)
            for (int i = 0; i < FxCueQueue.Capacity; i++) Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Kick, 1f + i, (byte)(i % 7), 2f)));
            Assert.AreEqual(FxCueQueue.Capacity, queue.Count);
            // 보스 처치 순간은 가장 낮은(가장 늦은) 킥을 밀어내고 들어간다 — 지금 터지지 않는다
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.BossMoment, 0.5f)));
            Assert.AreEqual(1, queue.Evicted);
            Assert.AreEqual(FxCueQueue.Capacity, queue.Count);
            Assert.IsFalse(queue.TakeDue(0.49f, 0.49f, out _), "가득 차도 이르게 터뜨리지 않는다");
            Assert.IsTrue(queue.TakeDue(0.5f, 0.5f, out var boss));
            Assert.AreEqual(FxCueKind.BossMoment, boss.Kind);
            // 다시 가득 찬 큐에 같은 우선순위(킥)가 오면 들어오는 쪽을 버린다
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Light, 40f)));
            Assert.IsFalse(queue.Enqueue(Cue(FxCueKind.Kick, 50f, 0, 2f)), "낮지 않은 것을 밀어내지 않는다");
            Assert.IsFalse(queue.Enqueue(Cue(FxCueKind.Haptic, 50f)), "진동은 가장 낮다");
            Assert.AreEqual(2, queue.Dropped);
            // 빛은 킥을 밀어낸다, 남은 큐는 시각 순서로 나온다
            Assert.IsTrue(queue.Enqueue(Cue(FxCueKind.Light, 45f)));
            float last = float.MinValue;
            int count = 0;
            while (queue.TakeDue(100f, 100f, out var cue))
            {
                Assert.GreaterOrEqual(cue.At, last, "이른 것부터");
                last = cue.At;
                count++;
            }
            Assert.AreEqual(FxCueQueue.Capacity, count);
        }

        // ───────────── SPEC4 §13.5 a · b · c · j (RM-2 P4-3b Part B) ─────────────

        [Test]
        public void TtokdanArtShot_Spins()
        {
            // §13.5 a: 그림이 있는 똑단이 단추도 팽이처럼 돈다 — 가운데 기준점 · 진행 방향 안 봄 · 720°/초
            var ttokdan = AttackFxCatalog.Look("ttokdan");
            Assert.AreEqual(720f, ttokdan.ArtSpin, 1e-4f);
            AttackFxCatalog.ArtShotPose(ttokdan, out var pivot, out bool align, out float spin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), pivot, "가운데를 축으로");
            Assert.IsFalse(align, "진행 방향을 보지 않는다");
            Assert.AreEqual(720f, spin, 1e-4f);
            // 나머지 계보(와 모르는 계보)는 예전처럼 끝 기준점 + 진행 방향
            foreach (var id in AttackFxCatalog.KnownLineages.Where(l => l != "ttokdan").Append("no_such_lineage"))
            {
                var look = AttackFxCatalog.Look(id);
                Assert.AreEqual(0f, look.ArtSpin, id);
                AttackFxCatalog.ArtShotPose(look, out var tip, out bool aligned, out float none);
                Assert.AreEqual(AttackFxCatalog.ShotTipPivot, tip.x, 1e-4f, id);
                Assert.IsTrue(aligned, id);
                Assert.AreEqual(0f, none, id);
            }
        }

        [Test]
        public void EchoStreak_OnlyForSnipe()
        {
            // §13.5 b: 잔향이 원거리 투사체의 빛 꼬리는 저격일 때만 (찰칵이 열쇠 꼬리도 잔향이 지원 · 힐이면 없다). 지킴이는 그대로
            var chalkak = AttackFxCatalog.Look("chalkak");
            var mongdang = AttackFxCatalog.Look("mongdang");
            Assert.IsTrue(chalkak.Streak, "찰칵이는 꼬리 계보");
            foreach (CombatStyle style in Enum.GetValues(typeof(CombatStyle)))
            {
                var mod = AttackFxCatalog.Style(style);
                bool snipe = style == CombatStyle.Snipe;
                Assert.AreEqual(snipe, AttackFxCatalog.DrawsStreak(chalkak, mod, true), "잔향이 찰칵이 " + style);
                Assert.AreEqual(snipe, AttackFxCatalog.DrawsStreak(mongdang, mod, true), "잔향이 몽당이 " + style);
                Assert.IsTrue(AttackFxCatalog.DrawsStreak(chalkak, mod, false), "지킴이 찰칵이 " + style);
                Assert.AreEqual(snipe, AttackFxCatalog.DrawsStreak(mongdang, mod, false), "지킴이 몽당이 " + style);
            }
        }

        [Test]
        public void LampShake_IsRadial_FollowsNoise()
        {
            // §13.5 c: AddRadial은 기울기를 지운다 — 같은 trauma를 방향 없이 넣은 흔들림과 프레임마다 같다 (잡음 방향 그대로)
            var radial = new TraumaShake();
            radial.AddShopHit(HitFeel.ShopHitTrauma, Vector2.down);
            Assert.IsTrue(radial.IsDirectional);
            radial.AddRadial(HitFeel.LampTrauma);
            Assert.IsFalse(radial.IsDirectional, "불 밝히기는 사방으로");
            var free = new TraumaShake();
            free.Add(radial.Trauma);
            var leaning = new TraumaShake();
            leaning.Add(radial.Trauma, Vector2.up);
            float apart = 0f;
            for (int i = 0; i < 20; i++)
            {
                radial.Tick(1f / 60f, 1f, out var a, out float ra);
                free.Tick(1f / 60f, 1f, out var b, out float rb);
                leaning.Tick(1f / 60f, 1f, out var c, out _);
                Assert.Less((a - b).magnitude, 1e-4f, "frame " + i);
                Assert.AreEqual(ra, rb, 1e-4f);
                apart = Mathf.Max(apart, (a - c).magnitude);
            }
            Assert.Greater(apart, 0.1f, "예전 Vector2.up 기울기와는 다른 흔들림");
            // 뒤에 오는 방향 있는 사건은 다시 기울인다
            radial.AddShopHit(HitFeel.ShopHitTrauma, Vector2.left);
            Assert.IsTrue(radial.IsDirectional);
            Assert.Less(HitFeel.BossKillTrauma, HitFeel.LampTrauma, "세기는 FX-JUICE 값 그대로 (불 밝히기가 가장 큰 순간)");
        }

        [Test]
        public void KillArrivalProbe_CountsBloomsWithoutRecentHit()
        {
            // §13.5 j (ATK-09): 처치 꽃 순간 앞 0.30초 안에 명중 반응이 없었으면 센다
            KillArrivalProbe.Reset();
            try
            {
                KillArrivalProbe.Note(AttackFxCatalog.KillHoldMin);
                KillArrivalProbe.Note(EnemyFxCatalog.BossKillFreeze);
                KillArrivalProbe.Note(KillArrivalProbe.Window);
                Assert.AreEqual(3, KillArrivalProbe.Kills);
                Assert.AreEqual(0, KillArrivalProbe.WithoutArrival, "보류 · 하얀 버팀 · 창 끝은 닿은 것");
                KillArrivalProbe.Note(KillArrivalProbe.Window + 0.01f);
                KillArrivalProbe.Note(9999f);
                KillArrivalProbe.Note(float.NaN);
                Assert.AreEqual(6, KillArrivalProbe.Kills);
                Assert.AreEqual(3, KillArrivalProbe.WithoutArrival, "늦은 명중 · 명중 없음 · NaN");
                // 기본 공격 처치 보류의 가장 긴 버팀(마지막 투사체 도착 + 하얀 버팀)도 창 안이다
                float worst = EnemyFxCatalog.KillBlowHold(AttackFxCatalog.KillHold(AttackFxCatalog.MaxFlight), AttackFxCatalog.MaxFlight, true) -
                              AttackFxCatalog.MaxFlight;
                Assert.IsFalse(KillArrivalProbe.IsWithoutArrival(worst), "보스 하얀 버팀 " + worst);
            }
            finally
            {
                KillArrivalProbe.Reset();
            }
        }

        [Test]
        public void BossPlate_DimsOnlyWhenItCoversTheBody()
        {
            // UX Wave 3 이월 d: 위 끝에 눌려 내려온 이름표 묶음이 몸과 가로 · 세로 12px 넘게 겹치면 이름표를 0.75로
            var body = new Rect(-100f, 0f, 200f, 200f);
            Assert.IsTrue(EnemyView.PlateCoversBody(new Rect(-80f, 150f, 160f, 60f), body), "머리를 덮는다");
            Assert.IsFalse(EnemyView.PlateCoversBody(new Rect(-80f, 205f, 160f, 60f), body), "머리 위 (평소 자리)");
            Assert.IsFalse(EnemyView.PlateCoversBody(new Rect(-80f, 192f, 160f, 60f), body), "8px 스침은 덮은 것이 아니다");
            Assert.IsFalse(EnemyView.PlateCoversBody(new Rect(95f, 100f, 160f, 60f), body), "옆으로 비켜 5px");
            Assert.AreEqual(0.75f, EnemyView.PlateOverBodyAlpha, 1e-4f);
        }

        [Test]
        public void BossPlate_DimLeavesTheNameAtFullAlpha()
        {
            // 이월 d 고침: 받침(BossPlate 그림 · 밑줄 Edge)만 원래 알파 × 0.75 — 보스 이름 글자(받침의 자식)는 그대로
            var plate = new GameObject("BossPlate", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var edge = new GameObject("Edge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var name = new GameObject("Name", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            try
            {
                edge.transform.SetParent(plate.transform, false);
                name.transform.SetParent(plate.transform, false);
                var back = plate.GetComponent<UnityEngine.UI.Image>();
                var line = edge.GetComponent<UnityEngine.UI.Image>();
                var label = name.GetComponent<UnityEngine.UI.Image>();
                back.color = new Color(0.1f, 0.1f, 0.2f, 0.88f);
                line.color = new Color(1f, 0.3f, 0.3f, 0.9f);
                label.color = Color.white;

                EnemyView.ApplyPlateDim(back, 0.88f, line, 0.9f, EnemyView.PlateOverBodyAlpha);
                Assert.AreEqual(0.66f, back.color.a, 1e-4f, "받침 0.88 × 0.75");
                Assert.AreEqual(0.675f, line.color.a, 1e-4f, "밑줄 0.9 × 0.75");
                Assert.AreEqual(1f, label.color.a, 1e-4f, "이름 글자는 그대로");
                Assert.AreEqual(0.1f, back.color.r, 1e-4f, "색은 그대로, 알파만");

                EnemyView.ApplyPlateDim(back, 0.88f, line, 0.9f, 1f);
                Assert.AreEqual(0.88f, back.color.a, 1e-4f, "몸에서 벗어나면 원래 알파");
                Assert.AreEqual(0.9f, line.color.a, 1e-4f);
                EnemyView.ApplyPlateDim(null, 1f, null, 1f, 0.75f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plate);
                // 자식으로 붙었으면 받침과 함께 이미 지워졌다
                if (edge != null) UnityEngine.Object.DestroyImmediate(edge);
                if (name != null) UnityEngine.Object.DestroyImmediate(name);
            }
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        static void AssertColor(Color expected, Color actual, string label)
        {
            Assert.IsTrue(Near(expected, actual) && Mathf.Abs(expected.a - actual.a) < 0.004f, $"{label}: expected {expected}, got {actual}");
        }
    }
}
