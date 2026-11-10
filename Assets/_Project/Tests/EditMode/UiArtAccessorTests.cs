using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 계획된 아트 접근자 (PHASE4 RD-5 · SPEC4 §9.3 · ART-UPGRADE §4.3 #7, P4-10): 새 UiArt 접근자는 모두 EditMode에서 예외 없이 불리고,
    /// 계획된 파일이 있으면 그 그림, 없으면 정해진 대신 그림(또는 null)을 돌려준다. 임포트 규칙 v9(출석 칸 테두리 · 모바일 크기 제한)도 확인한다.
    /// (에디터 어셈블리는 시험 어셈블리가 참조하지 않아 불러온 어셈블리에서 찾는다 — ContentSourceTests와 같은 방식)
    /// </summary>
    public class UiArtAccessorTests
    {
        const string ArtRoot = "Assets/_Project/Art/";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>대신 그림 이름: "UiArt.X" = UiArt 속성, "UIKit.X" = UIKit 필드, null = null</summary>
        sealed class Case
        {
            public string Label;
            public Func<object> Invoke;
            public string[] Chain;
            public string Fallback;
        }

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

        static Type UiArt => EditorType("UiArt");

        static object Unwrap(Func<object> call, string label)
        {
            try
            {
                return call();
            }
            catch (TargetInvocationException e)
            {
                Assert.Fail($"{label}: 예외 {e.InnerException?.GetType().Name} {e.InnerException?.Message}");
                return null;
            }
        }

        static object Property(string name)
        {
            var property = UiArt.GetProperty(name, Any);
            Assert.IsNotNull(property, $"UiArt.{name} 속성이 없다");
            return Unwrap(() => property.GetValue(null), "UiArt." + name);
        }

        static object Method(string name, Type[] parameters, params object[] args)
        {
            var method = UiArt.GetMethod(name, Any, null, parameters, null);
            Assert.IsNotNull(method, $"UiArt.{name}({string.Join(", ", Array.ConvertAll(parameters, p => p.Name))}) 메서드가 없다");
            return Unwrap(() => method.Invoke(null, args), $"UiArt.{name}({string.Join(", ", args)})");
        }

        static object EnumArg(string enumName, int value)
        {
            var type = UiArt.GetNestedType(enumName, BindingFlags.Public);
            Assert.IsNotNull(type, $"UiArt.{enumName} 열거형이 없다");
            return Enum.ToObject(type, value);
        }

        static Type EnumType(string enumName) => UiArt.GetNestedType(enumName, BindingFlags.Public);

        static Sprite Load(string relativePath) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + relativePath);

        static object FallbackValue(string fallback)
        {
            if (fallback == null) return null;
            string[] parts = fallback.Split('.');
            if (parts[0] == "UiArt") return Property(parts[1]);
            var field = EditorType(parts[0]).GetField(parts[1], Any);
            Assert.IsNotNull(field, fallback);
            return field.GetValue(null);
        }

        /// <summary>일곱 계보의 기본 형태 (LineageDef.baseFormId — Lv10 자세 그림 A11의 이름 앞부분)</summary>
        static readonly string[] BaseForms = { "jjak_base", "ttok_base", "chal_base", "mong_base", "udu_base", "rouge_base", "lala_base" };

        /// <summary>계획된 이름 → 우선순위 경로 → 마지막 대신 그림 (UiArt.cs의 summary와 같은 표)</summary>
        static List<Case> Cases()
        {
            var cases = new List<Case>();
            void Prop(string name, string fallback, params string[] chain) =>
                cases.Add(new Case { Label = name, Invoke = () => Property(name), Chain = chain, Fallback = fallback });

            // ART-UPGRADE §4.3 #7 (B3b · B8b · B7b)
            Prop("ButtonSoft9s", "UiArt.PanelParchment9s", "UI/ui_button_soft_9s.png");
            Prop("TagGold9s", "UiArt.ButtonGold9s", "UI/ui_tag_gold_9s.png");
            Prop("TabOn9s", "UiArt.Tab9s", "UI/ui_tab_on_9s.png");
            Prop("TabOff9s", "UiArt.Tab9s", "UI/ui_tab_off_9s.png");
            Prop("SwitchOn9s", "UiArt.BarFrame9s", "UI/ui_switch_on_9s.png");
            Prop("SwitchOff9s", "UiArt.BarFrame9s", "UI/ui_switch_off_9s.png");
            Prop("AttendanceCell9s", "UiArt.PanelParchment9s", "UI/ui_attendance_cell_9s.png");
            Prop("AttendanceCellGold9s", "UiArt.PanelParchment9s", "UI/ui_attendance_cell_gold_9s.png");
            Prop("Nameplate9s", "UiArt.PillNight9s", "UI/ui_nameplate_9s.png");
            Prop("BagHeader", null, "UI/bag_header.png");
            Prop("PowerIcon", "UiArt.Star", "Icons/power.png", "Icons/guardian.png");

            // SPEC4 §9.3 / §10.1
            Prop("MapBg", null, "Story/map_bg.png", "UI/bg_night.png");
            Prop("AffinityFrame9s", "UiArt.SlotFrame9s", "UI/affinity_frame_9s.png");
            Prop("AffinityBadge", "UiArt.Star", "UI/affinity_badge.png", "Icons/affinity.png");
            Prop("HeartBurst", "UiArt.Star", "UI/fx_heart_burst.png", "VFX/motif_heart.png", "Icons/affinity.png");
            Prop("AffinityHalo", "UIKit.CircleSprite", "UI/affinity_halo.png", "FX/glow_soft.png");
            Prop("ComebackHeader", null, "UI/comeback_header.png", "UI/return_header.png");
            Prop("SeasonBanner", null, "UI/season_banner.png", "UI/shop_event_banner.png");
            Prop("EnemyDeathYarn", null, "VFX/enemy_death_yarn.png");
            Prop("RustStain", null, "VFX/fx_rust_stain.png");
            Prop("MothHole", null, "VFX/fx_moth_hole.png");
            // L7 (A14 · A11, RM-2 P4-10)
            Prop("StarwalkCostume", null, "Costumes/jjak_starwalk.png");
            var oneString = new[] { typeof(string) };
            foreach (string form in BaseForms)
            {
                string f = form;
                cases.Add(new Case { Label = $"Pose10({f})", Invoke = () => Method("Pose10", oneString, f), Chain = new[] { $"Characters/{f}_pose10.png" } });
            }

            var oneInt = new[] { typeof(int) };
            for (int stage = 1; stage <= 3; stage++)
            {
                int s = stage;
                cases.Add(new Case { Label = $"ShopCrack({s})", Invoke = () => Method("ShopCrack", oneInt, s), Chain = new[] { $"Battle/shop_crack_{s}.png" } });
            }
            var twoInts = new[] { typeof(int), typeof(int) };
            for (int chapter = 1; chapter <= 5; chapter++)
            {
                for (int panel = 1; panel <= 3; panel++)
                {
                    int c = chapter, p = panel;
                    cases.Add(new Case { Label = $"StoryPanel({c}, {p})", Invoke = () => Method("StoryPanel", twoInts, c, p), Chain = new[] { $"Story/ch{c}_{p}.png" } });
                }
            }

            string[] nodes = { "off", "on", "boss", "chest" };
            for (int i = 0; i < nodes.Length; i++)
            {
                int k = i;
                string[] chain = k switch
                {
                    2 => new[] { "UI/map_node_boss.png" },
                    3 => new[] { "UI/map_node_chest.png", "UI/journal_chest_closed.png" },
                    _ => new[] { $"UI/map_node_{nodes[k]}.png", "Icons/lamp.png" },
                };
                string fallback = k == 2 ? "UiArt.SkillOrbFrame" : "UiArt.Star";
                cases.Add(new Case { Label = $"MapNode({k})", Invoke = () => Method("MapNode", oneInt, k), Chain = chain, Fallback = fallback });
                cases.Add(new Case { Label = $"MapNode(MapNodeKind {k})", Invoke = () => Method("MapNode", new[] { EnumType("MapNodeKind") }, EnumArg("MapNodeKind", k)), Chain = chain, Fallback = fallback });
            }

            string[] bundles = { "closed", "glow", "open" };
            for (int i = 0; i < bundles.Length; i++)
            {
                int k = i;
                var chain = k == 0
                    ? new[] { "UI/gacha_bundle_closed.png", "UI/gacha_card_back.png" }
                    : new[] { $"UI/gacha_bundle_{bundles[k]}.png", "UI/gacha_bundle_closed.png", "UI/gacha_card_back.png" };
                cases.Add(new Case { Label = $"GachaBundle({k})", Invoke = () => Method("GachaBundle", oneInt, k), Chain = chain, Fallback = "UIKit.RoundSprite" });
                cases.Add(new Case { Label = $"GachaBundle(BundleState {k})", Invoke = () => Method("GachaBundle", new[] { EnumType("BundleState") }, EnumArg("BundleState", k)), Chain = chain, Fallback = "UIKit.RoundSprite" });
            }

            string[] medals = { "bronze", "silver", "gold", "hidden" };
            for (int i = 0; i < medals.Length; i++)
            {
                int k = i;
                var chain = new[] { $"UI/medal_{medals[k]}.png", k == 3 ? "Icons/lock.png" : "Icons/journal.png" };
                cases.Add(new Case { Label = $"Medal({k})", Invoke = () => Method("Medal", oneInt, k), Chain = chain, Fallback = "UiArt.Star" });
                cases.Add(new Case { Label = $"Medal(MedalKind {k})", Invoke = () => Method("Medal", new[] { EnumType("MedalKind") }, EnumArg("MedalKind", k)), Chain = chain, Fallback = "UiArt.Star" });
            }
            return cases;
        }

        [Test]
        public void EveryPlannedAccessor_ReturnsPlannedArtOrItsFallback()
        {
            var failures = new List<string>();
            foreach (var c in Cases())
            {
                var actual = c.Invoke() as Sprite;
                Sprite expected = null;
                string expectedFrom = null;
                foreach (string path in c.Chain)
                {
                    expected = Load(path);
                    if (expected != null) { expectedFrom = path; break; }
                }
                if (expected == null)
                {
                    expected = FallbackValue(c.Fallback) as Sprite;
                    expectedFrom = c.Fallback ?? "null";
                }
                if (actual != expected) failures.Add($"{c.Label}: {Describe(actual)} (기대: {expectedFrom} = {Describe(expected)})");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        static string Describe(Sprite sprite) => sprite == null ? "null" : AssetDatabase.GetAssetPath(sprite) is { Length: > 0 } path ? path : sprite.name;

        [Test]
        public void HasFlags_MatchFiles()
        {
            Assert.AreEqual(Load("UI/ui_tab_on_9s.png") != null && Load("UI/ui_tab_off_9s.png") != null, Property("HasTabSkin"), "HasTabSkin");
            Assert.AreEqual(Load("UI/ui_switch_on_9s.png") != null && Load("UI/ui_switch_off_9s.png") != null, Property("HasSwitchSkin"), "HasSwitchSkin");
            Assert.AreEqual(Load("UI/ui_attendance_cell_9s.png") != null, Property("HasAttendanceCell9s"), "HasAttendanceCell9s");
            Assert.AreEqual(Load("UI/ui_attendance_cell_gold_9s.png") != null, Property("HasAttendanceCellGold9s"), "HasAttendanceCellGold9s");
            Assert.AreEqual(Load("UI/affinity_frame_9s.png") != null, Property("HasAffinityFrame9s"), "HasAffinityFrame9s");
            Assert.AreEqual(Load("UI/affinity_halo.png") != null, Property("HasAffinityHalo"), "HasAffinityHalo");
            Assert.AreEqual(Load("Costumes/jjak_starwalk.png") != null, Property("HasStarwalkCostume"), "HasStarwalkCostume");
            foreach (string form in BaseForms)
                Assert.AreEqual(Load($"Characters/{form}_pose10.png") != null, Method("HasPose10", new[] { typeof(string) }, form), $"HasPose10({form})");
        }

        [Test]
        public void IndexedAccessors_OutOfRange_DoNotThrow()
        {
            var oneInt = new[] { typeof(int) };
            foreach (int stage in new[] { int.MinValue, -1, 0, 4, 99, int.MaxValue })
                Assert.IsNull(Method("ShopCrack", oneInt, stage), $"ShopCrack({stage})");

            var twoInts = new[] { typeof(int), typeof(int) };
            foreach (var (chapter, panel) in new[] { (0, 1), (1, 0), (-3, 2), (1, 10), (10000, 1), (int.MaxValue, int.MaxValue), (int.MinValue, 1) })
                Assert.IsNull(Method("StoryPanel", twoInts, chapter, panel), $"StoryPanel({chapter}, {panel})");
            // 아직 없는 뒤 장 (6장 이후)은 파일이 생길 때까지 null
            if (Load("Story/ch6_1.png") == null) Assert.IsNull(Method("StoryPanel", twoInts, 6, 1), "StoryPanel(6, 1)");

            // 범위 밖 번호는 가까운 값으로 (예외 없음)
            Assert.AreEqual(Method("MapNode", oneInt, 0), Method("MapNode", oneInt, -5), "MapNode(-5) = MapNode(0)");
            Assert.AreEqual(Method("MapNode", oneInt, 3), Method("MapNode", oneInt, 42), "MapNode(42) = MapNode(3)");
            Assert.AreEqual(Method("GachaBundle", oneInt, 0), Method("GachaBundle", oneInt, -1), "GachaBundle(-1) = GachaBundle(0)");
            Assert.AreEqual(Method("GachaBundle", oneInt, 2), Method("GachaBundle", oneInt, 7), "GachaBundle(7) = GachaBundle(2)");
            Assert.AreEqual(Method("Medal", oneInt, 0), Method("Medal", oneInt, int.MinValue), "Medal(min) = Medal(0)");
            Assert.AreEqual(Method("Medal", oneInt, 3), Method("Medal", oneInt, int.MaxValue), "Medal(max) = Medal(3)");
            Assert.AreEqual(Method("MapNode", oneInt, 3), Method("MapNode", new[] { EnumType("MapNodeKind") }, EnumArg("MapNodeKind", 99)), "MapNode((MapNodeKind)99)");

            // Lv10 자세: 빈 이름 · 경로 조각 · 없는 형태는 null (예외 없음), Has도 false
            var oneString = new[] { typeof(string) };
            foreach (string bad in new[] { null, "", " ", "../Icons/lamp", "Characters/jjak_base", "jjak_base.png", "jjak base", "\uC9DD\uC9DD\uC774", new string('a', 65), "__p410_missing_form__" })
            {
                Assert.IsNull(Method("Pose10", oneString, bad), $"Pose10({bad ?? "null"})");
                Assert.AreEqual(false, Method("HasPose10", oneString, bad), $"HasPose10({bad ?? "null"})");
            }
        }

        [Test]
        public void MissingFile_GivesFalseAndNull()
        {
            var planned = UiArt.GetMethod("Planned", Any, null, new[] { typeof(string), typeof(Sprite).MakeByRefType() }, null);
            Assert.IsNotNull(planned, "UiArt.Planned(string, out Sprite)");
            var args = new object[] { "UI/__p410_missing_for_test__.png", null };
            bool found = (bool)Unwrap(() => planned.Invoke(null, args), "Planned");
            Assert.IsFalse(found);
            Assert.IsNull(args[1]);
        }

        /// <summary>틴트용 흰 그림으로 보는 평균 채도 상한 (glow_soft · ui_slot_frame_9s는 0)</summary>
        const float MaxTintChroma = 0.03f;

        [Test]
        public void TintFallbacks_AreWhiteWhenFlagIsFalse()
        {
            // Has* 플래그가 false면 부르는 쪽이 대신 그림을 DustyPink로 칠한다 → 그 그림은 흰색 · 회색이어야 한다 (색 그림이면 분홍 위에 색이 겹친다).
            // 플래그가 true면 전용 아트(색 그림)를 흰색 그대로 그린다 → 접근자가 그 전용 파일을 돌려주는지 본다.
            foreach (var (flag, accessor, dedicated) in new[]
            {
                ("HasAffinityHalo", "AffinityHalo", "UI/affinity_halo.png"),
                ("HasAffinityFrame9s", "AffinityFrame9s", "UI/affinity_frame_9s.png"),
            })
            {
                var sprite = Property(accessor) as Sprite;
                if ((bool)Property(flag))
                {
                    Assert.AreEqual(Load(dedicated), sprite, $"{flag} = true → {accessor} = {dedicated}");
                    continue;
                }
                if (sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(sprite);
                if (string.IsNullOrEmpty(path))
                {
                    // 파일 없이 코드로 만든 자리표시 (UIKit 원 · 둥근 사각형 — 흰색)
                    Assert.IsFalse((bool)Method("IsArt", new[] { typeof(Sprite) }, sprite), $"{accessor}: 파일 없는 그림 {sprite.name}은 자리표시여야 한다");
                    continue;
                }
                Assert.IsTrue(path.EndsWith(".png", StringComparison.OrdinalIgnoreCase), $"{accessor}: 대신 그림 {path}이 PNG가 아니다");
                float chroma = MeanChroma(path);
                Assert.LessOrEqual(chroma, MaxTintChroma, $"{accessor}: {flag} = false인데 대신 그림 {path}의 평균 채도가 {chroma:F3} — 틴트용 흰 그림이어야 한다");
            }
        }

        /// <summary>불투명 픽셀(α ≥ 128)의 평균 채도 (max − min, 0~1). 임포트 설정(읽기 불가 · 압축)과 상관없이 PNG 파일을 직접 읽는다</summary>
        static float MeanChroma(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(System.IO.File.ReadAllBytes(assetPath)), $"{assetPath}를 읽을 수 없다");
                long sum = 0;
                int count = 0;
                foreach (var pixel in texture.GetPixels32())
                {
                    if (pixel.a < 128) continue;
                    sum += Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) - Math.Min(pixel.r, Math.Min(pixel.g, pixel.b));
                    count++;
                }
                return count == 0 ? 0f : sum / (255f * count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ───────────── 임포트 규칙 v9 ─────────────

        /// <summary>
        /// 계획된 그림(사다리 L0 · L1 · L2 · L4 · L7)이 들어오면 받을 임포트 규칙 (RM-2 P4-10, 그림 없이 경로만으로 확인):
        /// 종류(KindOf) · 큰 배경 밉맵 끄기(IsLargeBackdrop) · 9-slice 테두리(BorderOf) · 모바일 규칙(MobileRuleOf).
        /// 규칙이 바뀌어 계획된 그림이 다른 설정으로 들어오게 되면 여기서 먼저 깨진다 (그림이 오는 날 코드 수정 없이 들어오게)
        /// </summary>
        [Test]
        public void Importer_PlannedArtRules()
        {
            var type = EditorType("ArtImportPostprocessor");
            MethodInfo Find(string name)
            {
                var method = type.GetMethod(name, Any, null, new[] { typeof(string) }, null);
                Assert.IsNotNull(method, $"ArtImportPostprocessor.{name}(string)");
                return method;
            }
            var kindOf = Find("KindOf");
            var backdrop = Find("IsLargeBackdrop");
            var borderOf = Find("BorderOf");
            var mobileOf = Find("MobileRuleOf");
            var square = new Vector4(48, 48, 48, 48);
            var cell = new Vector4(44, 44, 44, 88);

            // (Art/ 아래 경로, 종류, 밉맵 없는 큰 배경인지(Default만), 9-slice 테두리(9-slice만), 모바일 규칙)
            var rows = new List<(string path, string kind, bool noMip, Vector4? border, string mobile)>
            {
                // L0
                ("UI/gacha_bundle_closed.png", "Default", false, null, "None"),
                ("Battle/shop_crack_1.png", "Default", false, null, "None"),
                ("VFX/enemy_death_yarn.png", "Vfx", false, null, "None"),
                ("VFX/fx_rust_stain.png", "Vfx", false, null, "None"),
                ("VFX/fx_moth_hole.png", "Vfx", false, null, "None"),
                // L1
                ("Story/ch1_1.png", "Default", true, null, "None"),
                ("Story/ch5_3.png", "Default", true, null, "None"),
                ("Story/map_bg.png", "Default", true, null, "None"),
                ("UI/map_node_on.png", "Default", false, null, "None"),
                ("UI/medal_hidden.png", "Default", false, null, "None"),
                ("UI/affinity_frame_9s.png", "NineSlice", false, square, "None"),
                ("UI/affinity_badge.png", "Default", false, null, "None"),
                ("UI/fx_heart_burst.png", "Default", false, null, "None"),
                ("UI/affinity_halo.png", "Default", false, null, "None"),
                // L2
                ("Meta/feature_speed_banner.png", "Default", false, null, "Banner"),
                ("Meta/feature_dungeon_batch_sweep.png", "Default", false, null, "None"),
                ("UI/bag_header.png", "Default", true, null, "None"),
                // L4
                ("UI/ui_button_soft_9s.png", "NineSlice", false, square, "None"),
                ("UI/ui_tab_on_9s.png", "NineSlice", false, square, "None"),
                ("UI/ui_tab_off_9s.png", "NineSlice", false, square, "None"),
                ("UI/ui_tag_gold_9s.png", "NineSlice", false, square, "None"),
                ("UI/ui_switch_on_9s.png", "NineSlice", false, square, "None"),
                ("UI/ui_switch_off_9s.png", "NineSlice", false, square, "None"),
                ("UI/ui_nameplate_9s.png", "NineSlice", false, square, "None"),
                ("UI/ui_attendance_cell_9s.png", "NineSlice", false, cell, "None"),
                ("UI/ui_attendance_cell_gold_9s.png", "NineSlice", false, cell, "None"),
                ("Icons/power.png", "Default", false, null, "None"),
                ("Icons/skill_firefly.png", "Default", false, null, "None"),
                ("Meta/mission_kill.png", "Default", false, null, "None"),
                ("Synergy/combo_parade.png", "Default", false, null, "None"),
                // L7
                ("UI/comeback_header.png", "Default", true, null, "None"),
                ("UI/season_banner.png", "Default", true, null, "None"),
                ("Characters/jjak_base_pose10.png", "Default", false, null, "Capped"),
                ("Costumes/jjak_starwalk.png", "Default", false, null, "Capped"),
                ("Items/mat_spring.png", "Default", false, null, "None"),
                ("Enemies/enemy_tangle.png", "Default", false, null, "None"),
                ("VFX/Juice/fb_light_burst_4x4.png", "Vfx", false, null, "None"),
            };

            var failures = new List<string>();
            foreach (var row in rows)
            {
                string path = ArtRoot + row.path;
                string kind = Unwrap(() => kindOf.Invoke(null, new object[] { path }), "KindOf " + row.path)?.ToString();
                if (kind != row.kind) failures.Add($"{row.path}: 종류 {kind} (기대 {row.kind})");
                if (row.kind == "Default")
                {
                    bool noMip = (bool)Unwrap(() => backdrop.Invoke(null, new object[] { path }), "IsLargeBackdrop " + row.path);
                    if (noMip != row.noMip) failures.Add($"{row.path}: 큰 배경(밉맵 없음) {noMip} (기대 {row.noMip})");
                }
                if (row.border.HasValue)
                {
                    var border = (Vector4)Unwrap(() => borderOf.Invoke(null, new object[] { path }), "BorderOf " + row.path);
                    if (border != row.border.Value) failures.Add($"{row.path}: 테두리 {border} (기대 {row.border.Value})");
                }
                string mobile = Unwrap(() => mobileOf.Invoke(null, new object[] { path }), "MobileRuleOf " + row.path)?.ToString();
                if (mobile != row.mobile) failures.Add($"{row.path}: 모바일 규칙 {mobile} (기대 {row.mobile})");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }


        [Test]
        public void Importer_AttendanceCellBorders()
        {
            var field = EditorType("ArtImportPostprocessor").GetField("BorderOverrides", Any);
            Assert.IsNotNull(field, "ArtImportPostprocessor.BorderOverrides");
            var borders = field.GetValue(null) as Dictionary<string, Vector4>;
            Assert.IsNotNull(borders);
            foreach (string name in new[] { "ui_attendance_cell_9s", "ui_attendance_cell_gold_9s" })
            {
                Assert.IsTrue(borders.TryGetValue(name, out var border), name);
                Assert.AreEqual(new Vector4(44, 44, 44, 88), border, $"{name}: 왼쪽 44 · 아래 44 · 오른쪽 44 · 위 88");
            }
        }

        [Test]
        public void Importer_VersionIncludesPhase4Rules()
        {
            var type = EditorType("ArtImportPostprocessor");
            var postprocessor = (AssetPostprocessor)Activator.CreateInstance(type);
            Assert.GreaterOrEqual(postprocessor.GetVersion(), 9u, "v9 = 출석 칸 테두리 · 모바일 크기 제한");
        }

        /// <summary>모바일 규칙 (ArtImportPostprocessor.MobileRuleOf와 같은 순서 — 먼저 맞는 것 하나)</summary>
        enum MobileRule { None, Capped, Banner, Hero }

        static MobileRule ExpectedMobileRule(string relative)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(relative);
            if (relative.StartsWith("Characters/", StringComparison.Ordinal) || relative.StartsWith("Costumes/", StringComparison.Ordinal)) return MobileRule.Capped;
            if (relative.StartsWith("Meta/", StringComparison.Ordinal) && name.EndsWith("_banner", StringComparison.Ordinal)) return MobileRule.Banner;
            if (name.StartsWith("hero_", StringComparison.Ordinal)) return MobileRule.Hero;
            return MobileRule.None;
        }

        /// <summary>
        /// 임포트 규칙 v9의 모바일 설정을 Art 아래 모든 PNG에서 확인한다:
        /// Characters/ · Costumes/ = Android · iOS 768 + ASTC 4x4, Meta/*_banner = 2048 + ASTC 6x6,
        /// hero_* = Android만 1024 + ASTC 4x4 (iOS는 덮어쓰지 않는다), 나머지(Icons/ · UI/ 등) = 덮어쓰지 않는다. PC(Standalone)는 모두 덮어쓰지 않는다.
        /// ("iPhone"은 API 이름, "iOS"는 .meta 이름 — 둘 다 같은 설정을 읽어야 한다)
        /// </summary>
        [Test]
        public void Importer_MobilePlatformRules()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot.TrimEnd('/') });
            var counts = new Dictionary<string, int>();
            var failures = new List<string>();
            var seen = new HashSet<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(ArtRoot, StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !seen.Add(path)) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                string relative = path.Substring(ArtRoot.Length);
                var rule = ExpectedMobileRule(relative);
                string group = rule switch
                {
                    MobileRule.Capped => relative.Substring(0, relative.IndexOf('/')),
                    MobileRule.None => relative.StartsWith("Icons/", StringComparison.Ordinal) ? "Icons" : relative.StartsWith("UI/", StringComparison.Ordinal) ? "UI" : "Other",
                    _ => rule.ToString(),
                };
                counts[group] = counts.TryGetValue(group, out int n) ? n + 1 : 1;

                foreach (string platform in new[] { "Android", "iPhone", "iOS" })
                {
                    bool android = platform == "Android";
                    var settings = importer.GetPlatformTextureSettings(platform);
                    (int size, TextureImporterFormat format)? expected = rule switch
                    {
                        MobileRule.Capped => (768, TextureImporterFormat.ASTC_4x4),
                        MobileRule.Banner => (2048, TextureImporterFormat.ASTC_6x6),
                        MobileRule.Hero when android => (1024, TextureImporterFormat.ASTC_4x4),
                        _ => null,
                    };
                    if (expected == null)
                    {
                        if (settings.overridden) failures.Add($"{relative} {platform}: 덮어쓰면 안 된다 ({settings.maxTextureSize}, {settings.format})");
                        continue;
                    }
                    if (!settings.overridden || settings.maxTextureSize != expected.Value.size || settings.format != expected.Value.format)
                        failures.Add($"{relative} {platform}: {(settings.overridden ? "덮어씀" : "덮어쓰지 않음")} {settings.maxTextureSize} {settings.format} (기대: 덮어씀 {expected.Value.size} {expected.Value.format})");
                    if (rule == MobileRule.Hero && settings.compressionQuality != 100)
                        failures.Add($"{relative} {platform}: 압축 품질 {settings.compressionQuality} (기대: 100)");
                }
                // PC는 그대로 (덮어쓰지 않는다)
                if (importer.GetPlatformTextureSettings("Standalone").overridden) failures.Add($"{relative} Standalone: 덮어쓰면 안 된다");
            }

            if (!counts.ContainsKey("Characters")) Assert.Ignore("Art/Characters에 그림이 없다");
            Assert.IsEmpty(failures, string.Join("\n", failures));
            // 규칙마다 실제로 확인한 그림이 있어야 한다 (규칙이 조용히 아무것도 맞히지 않는 회귀 — 예: 띠 그림 규칙이 Meta 배너를 놓침 — 를 잡는다)
            foreach (string group in new[] { "Characters", "Costumes", nameof(MobileRule.Banner), nameof(MobileRule.Hero), "Icons", "UI" })
                Assert.IsTrue(counts.TryGetValue(group, out int n) && n > 0, $"확인한 {group} 그림이 없다 ({string.Join(", ", counts)})");
        }
    }
}
