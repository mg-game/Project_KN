using System.IO;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 전투 화면 적 쪽 연출 굽기 (BattleSceneBuilder가 부른다):
    ///   방위 링 칸 번쩍임 (ATK-03) — 바닥 평면의 붉은 72° 호 5개 + 가장자리 레이어의 "!" 표시 5개 (색만으로 뜻을 전하지 않게, ATK-17).
    ///   UI/AmbienceHole 머티리얼 (Sprites/Battle/UIAmbienceHole.mat) — 정전 어둠 덮개에 빛 구멍 (ATK-14). 셰이더가 없거나 오류면 null → 구멍 없이 빛 웅덩이만.
    ///   공격 연출 시험장 (ATK-17) — 개발 빌드에서 스테이지 이름표를 5번 누르면 열리는 조작 패널 + FxGalleryView + 이름표 손짓(FxGalleryTrigger).
    /// </summary>
    internal static class EnemyFxBuilder
    {
        const string HoleShaderPath = IdleProjectSetup.Root + "/Scripts/UI/Battle/Shaders/UIAmbienceHole.shader";
        const string HoleShaderName = "IdleGame/UI/AmbienceHole";
        const string MaterialDir = IdleProjectSetup.Root + "/Sprites/Battle";
        const string HoleMaterialPath = MaterialDir + "/UIAmbienceHole.mat";

        // ───────────── 아트 검사 ─────────────

        /// <summary>
        /// 적 공격·처치·수선점 피격 그림이 Art/VFX에 다 있는지 (ATK-07 에디터 검사). 빠진 그림은 경고만 남긴다 —
        /// 런타임은 흰색 모양에 적 색으로 대체하므로 씬 생성은 계속된다.
        /// </summary>
        public static void ReportMissingArt()
        {
            var missing = new System.Text.StringBuilder();
            foreach (string id in EnemyFxCatalog.KnownEnemies)
            {
                string key = EnemyFxCatalog.Look(id).ArtKey;
                if (ArtLib.Vfx(key) == null) missing.Append(' ').Append(key);
            }
            foreach (EnemyDeathFamily family in System.Enum.GetValues(typeof(EnemyDeathFamily)))
            {
                string key = EnemyFxCatalog.DeathArtKey(family);
                if (ArtLib.Vfx(key) == null) missing.Append(' ').Append(key);
            }
            if (ArtLib.Vfx(SkillVfxLibrary.ShopHitId) == null) missing.Append(' ').Append(SkillVfxLibrary.ShopHitId);
            if (missing.Length > 0) Debug.LogWarning("[EnemyFxBuilder] 적 연출 그림이 없어 흰색 모양으로 대체합니다:" + missing);
        }

        /// <summary>
        /// 처치 꽃 (FX-JUICE §5 / E10)을 JuiceArt._killBlooms에 EnemyDeathFamily 순서로 굽는다: Art/VFX/Juice/kill_{dust|moth|metal|shadow}.
        /// 없는 그림은 missing에 적고 null (런타임이 enemy_death_{갈래}로 대신한다)
        /// </summary>
        public static void BindKillBlooms(JuiceArt juice, System.Collections.Generic.List<string> missing)
        {
            var families = (EnemyDeathFamily[])System.Enum.GetValues(typeof(EnemyDeathFamily));
            var blooms = new Object[families.Length];
            for (int i = 0; i < families.Length; i++)
            {
                blooms[(int)families[i]] = JuiceArtBuilder.Load(JuiceArtBuilder.VfxJuice, "kill_" + EnemyFxCatalog.FamilyKey(families[i]), missing);
            }
            new Binder(juice).SetArray("_killBlooms", blooms).Apply();
        }

        // ───────────── 방위 링 칸 번쩍임 ─────────────

        /// <summary>바닥 평면의 붉은 링 칸 5개 (Radial360 Top 시계 방향 72°, 처음엔 꺼짐 — DirectionRingView가 크기·방향을 맞춘다)</summary>
        public static Image[] BuildSlotFlashes(RectTransform ground, Sprite ring, float diameter)
        {
            var flashes = new Image[BattleGeometry.SlotCount];
            for (int i = 0; i < flashes.Length; i++)
            {
                var image = Img(ground, $"RingFlash{i}", new Color(UITheme.Danger.r, UITheme.Danger.g, UITheme.Danger.b, 0f), ring);
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
                image.fillAmount = 1f / BattleGeometry.SlotCount;
                image.rectTransform.At(Center, Vector2.zero, Vector2.one * diameter * 1.02f);
                image.enabled = false;
                flashes[i] = image;
            }
            return flashes;
        }

        /// <summary>
        /// "!" 표시 5개 (빨간 배지 그림 ui_badge_red + 흰 느낌표 그림 motif_exclaim, 처음엔 꺼짐)를 가장자리 레이어에 만들고,
        /// 링 칸 번쩍임과 함께 DirectionRingView에 묶는다. 배지 그림이 없으면 예전 붉은 원 + 옅은 테
        /// </summary>
        public static void BindRingAlerts(DirectionRingView ringView, RectTransform edge, Image[] slotFlashes, Sprite exclaim)
        {
            var alerts = new Object[BattleGeometry.SlotCount];
            var badges = new Object[BattleGeometry.SlotCount];
            var marks = new Object[BattleGeometry.SlotCount];
            bool badgeArt = UiArt.IsArt(UiArt.BadgeRed);
            for (int i = 0; i < alerts.Length; i++)
            {
                var root = NewRect($"RingAlert{i}", edge).At(Center, Vector2.zero, new Vector2(44, 44));
                Image badge;
                if (badgeArt)
                {
                    badge = UiArt.Badge(root, "Badge", Center, Vector2.zero, 44f);
                    badge.rectTransform.Stretch();
                }
                else
                {
                    badge = Img(root, "Badge", UITheme.Danger, CircleSprite);
                    badge.rectTransform.Stretch();
                    var rim = Img(badge.transform, "Rim", new Color(1f, 1f, 1f, 0.35f), CircleSprite);
                    rim.rectTransform.Stretch(-3, -3, -3, -3);
                    rim.transform.SetAsFirstSibling();
                }
                Image mark;
                if (exclaim != null)
                {
                    mark = Img(root, "Mark", UITheme.Cream, exclaim);
                    mark.preserveAspect = true;
                    mark.rectTransform.Stretch(9, 9, 8, 8);
                }
                else
                {
                    // 모양 그림이 없으면 글자로 (폰트에 있는 기본 문자)
                    var text = Txt(root, "MarkText", "!", 30, UITheme.Cream, TextAlignmentOptions.Center, true);
                    text.rectTransform.Stretch();
                    mark = null;
                }
                root.gameObject.SetActive(false);
                alerts[i] = root;
                badges[i] = badge;
                marks[i] = mark;
            }
            new Binder(ringView)
                .SetArray("_slotFlash", slotFlashes)
                .SetArray("_alerts", alerts)
                .SetArray("_alertBadges", badges)
                .SetArray("_alertMarks", marks)
                .Apply();
            // 빨간 배지 그림은 흰색 그대로 (알파만 바꾼다), 예전 흰 원은 Danger
            var so = new SerializedObject(ringView);
            var tint = so.FindProperty("_alertBadgeTint");
            if (tint != null)
            {
                tint.colorValue = badgeArt ? Color.white : UITheme.Danger;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ───────────── 정전 빛 구멍 머티리얼 ─────────────

        /// <summary>
        /// 어둠 덮개 빛 구멍 머티리얼 (없으면 만들고, 있으면 셰이더를 맞춘다). 셰이더를 못 찾거나 컴파일 오류면 null
        /// (분홍 오류 셰이더로 덮개가 칠해지는 것보다 구멍 없이 지나가는 편이 낫다)
        /// </summary>
        public static Material AmbienceHoleMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(HoleShaderPath);
            if (shader == null) shader = Shader.Find(HoleShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogWarning($"[EnemyFxBuilder] {HoleShaderName} 셰이더를 쓸 수 없어 정전 빛 구멍 없이 만듭니다 ({HoleShaderPath}).");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(HoleMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialDir);
                material = new Material(shader) { name = "UIAmbienceHole" };
                AssetDatabase.CreateAsset(material, HoleMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        // ───────────── 공격 연출 시험장 ─────────────

        /// <summary>
        /// 공격 연출 시험장: 전투 화면 루트에 항상 켜진 FxGalleryView + 조작 패널(처음엔 꺼짐, 시험장 동안 쓰지 않는 아래 띠(덱·불 밝히기)를 덮는다 —
        /// 전장의 지킴이·허수아비를 가리지 않게), 스테이지 이름표에 5번 누르기 손짓. 허수아비 적은 액터 레이어(stage)에 세운다 (앞뒤 정렬을 함께 받는다)
        /// </summary>
        public static FxGalleryView BuildGallery(RectTransform root, float stripHeight, TMP_Text stageLabel, BattleFx fx, BattleFieldMapper mapper,
            EnemyViewPool enemies, ShopView shop, DirectionRingView ring, GuardianSlotView[] guardians, SkillVfxLibrary library,
            StageAmbienceView ambience, RectTransform stage)
        {
            var holder = NewRect("FxGallery", root).Stretch();
            var gallery = holder.gameObject.AddComponent<FxGalleryView>();

            // 금테 밤하늘 9-slice 판 (아트가 없으면 예전 둥근 사각형 + 금 줄)
            var panelArt = UiArt.PanelNight9s;
            var panel = UiArt.Panel(holder, "Panel", panelArt, 1.6f, null, new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.95f), true);
            panel.rectTransform.At(BottomCenter, new Vector2(0, 6f), new Vector2(1040, Mathf.Max(132f, stripHeight - 12f)));
            if (!UiArt.IsArt(panelArt)) Img(panel.transform, "Edge", UITheme.GoldAccent).rectTransform.Top(0, 3, 30, 30);
            var tag = Txt(panel.transform, "Tag", "공격 연출 시험장 (개발용)", 18, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            tag.rectTransform.Top(8, 26, 22, 200);
            var counter = Txt(panel.transform, "Counter", "1 / 1", 18, UITheme.SubText, TextAlignmentOptions.MidlineRight, true);
            counter.rectTransform.Top(8, 26, 600, 22);
            var title = Txt(panel.transform, "Title", "-", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            title.rectTransform.Top(36, 40, 22, 22);

            var prev = Btn(panel.transform, "Prev", "이전", UITheme.Secondary, 22);
            ((RectTransform)prev.transform).At(BottomLeft, new Vector2(22, 12), new Vector2(150, 50));
            var next = Btn(panel.transform, "Next", "다음", UITheme.Secondary, 22);
            ((RectTransform)next.transform).At(BottomLeft, new Vector2(184, 12), new Vector2(150, 50));
            var play = Btn(panel.transform, "Play", "멈춤", UITheme.Primary, 22, out var playLabel);
            ((RectTransform)play.transform).At(BottomLeft, new Vector2(346, 12), new Vector2(150, 50));
            var close = Btn(panel.transform, "Close", "닫기", UITheme.Danger, 22);
            ((RectTransform)close.transform).At(BottomRight, new Vector2(-22, 12), new Vector2(150, 50));
            // 단추 그림 (50px 높이 → 배율 3.5): 재생은 보라 단추, 나머지는 차분한 보조 단추
            float buttonPpu = UiArt.ButtonPpu(50f);
            UiArt.SetButtonArt(prev, UiArt.ButtonSecondary9s, buttonPpu);
            UiArt.SetButtonArt(next, UiArt.ButtonSecondary9s, buttonPpu);
            UiArt.SetButtonArt(play, UiArt.ButtonPrimary9s, buttonPpu);
            UiArt.SetButtonArt(close, UiArt.ButtonSecondary9s, buttonPpu);

            new Binder(gallery)
                .Set("_fx", fx)
                .Set("_mapper", mapper)
                .Set("_enemies", enemies)
                .Set("_shop", shop)
                .Set("_ring", ring)
                .SetArray("_guardians", guardians)
                .Set("_library", library)
                .Set("_ambience", ambience)
                .Set("_stage", stage)
                .Set("_panel", panel.gameObject)
                .Set("_title", title)
                .Set("_counter", counter)
                .Set("_prev", prev)
                .Set("_next", next)
                .Set("_play", play)
                .Set("_playLabel", playLabel)
                .Set("_close", close)
                .Apply();
            panel.gameObject.SetActive(false);

            // 숨은 손짓: 개발 빌드에서 스테이지 이름표를 2초 안에 5번 (릴리스 빌드에서는 컴포넌트가 스스로 꺼지고 입력을 받지 않는다)
            if (stageLabel != null)
            {
                stageLabel.raycastTarget = true;
                var trigger = stageLabel.gameObject.AddComponent<FxGalleryTrigger>();
                new Binder(trigger).Set("_gallery", gallery).Apply();
            }
            return gallery;
        }
    }
}
