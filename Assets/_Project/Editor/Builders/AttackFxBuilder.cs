using System;
using System.Collections.Generic;
using System.IO;
using IdleGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 전투 화면 공격 연출 굽기 (BattleSceneBuilder가 부른다):
    ///   AttackFxCatalog — 계보별 기본 공격 그림(Art/VFX/atk_{계보}_shot/_hit) + 초승달 베기(atk_slash) + 저격 빛 꼬리(atk_snipe_trail).
    ///     정식 7계보 + Art/VFX에 새로 넣은 atk_*_shot 그림의 계보까지. 그림이 없으면 null로 굽고 런타임이 흰색 모양에 계보 색으로 대체한다.
    ///   UI/WhiteFlash 머티리얼 (Sprites/Battle/UIWhiteFlash.mat) — 적/잔향이 템플릿의 흰 번쩍임 덧그림에 직렬화해
    ///     플레이어 빌드에서 셰이더가 빠지지 않게 한다 (ATK-12). 셰이더가 없거나 오류면 null → 런타임이 옅은 틴트로 대신한다.
    ///   연출 레이어 하위 Canvas (ATK-13) — 입자·숫자·바닥 장판이 움직여도 액터·받침대 메시를 다시 만들지 않는다.
    ///     override sorting을 끄고 GraphicRaycaster 없이 붙여 전장 RectMask2D에 그대로 잘리고 입력을 막지 않는다.
    ///   FX-JUICE 머티리얼 (§3.1) — UI/FxPremul(Sprites/Battle/UIFxPremul.mat: 입자 하나뿐인 머티리얼, 미리 곱한 알파로 보통·가산 입자가 함께) ·
    ///     UI/FxScreen(Sprites/Battle/UIFxScreen.mat: 빛 번쩍임 스크린 블렌드). 흰 번쩍임과 같은 규칙 — 셰이더가 없거나 오류면 null을 굽고
    ///     런타임이 기본 UI 머티리얼(보통 알파)로 대신한다.
    ///   계보 손맛 그림 (§5, JuiceArt._lineages) — Art/VFX/Juice/atk_{계보}_impact · _crit, Art/FX/Juice/muzzle_{계보}. 없으면 null (런타임이 예전 그림으로).
    /// </summary>
    internal static class AttackFxBuilder
    {
        const string ShaderPath = IdleProjectSetup.Root + "/Scripts/UI/Battle/Shaders/UIWhiteFlash.shader";
        const string ShaderName = "IdleGame/UI/WhiteFlash";
        const string MaterialDir = IdleProjectSetup.Root + "/Sprites/Battle";
        const string MaterialPath = MaterialDir + "/UIWhiteFlash.mat";
        const string PremulShaderPath = IdleProjectSetup.Root + "/Scripts/UI/Battle/Shaders/UIFxPremul.shader";
        const string PremulShaderName = "IdleGame/UI/FxPremul";
        const string PremulMaterialPath = MaterialDir + "/UIFxPremul.mat";
        const string ScreenShaderPath = IdleProjectSetup.Root + "/Scripts/UI/Battle/Shaders/UIFxScreen.shader";
        const string ScreenShaderName = "IdleGame/UI/FxScreen";
        const string ScreenMaterialPath = MaterialDir + "/UIFxScreen.mat";
        const string ShotPrefix = "atk_";
        const string ShotSuffix = "_shot";

        /// <summary>계보별 기본 공격 그림 묶음을 root 아래에 굽는다</summary>
        public static AttackFxCatalog BuildCatalog(RectTransform root)
        {
            var holder = UIKit.NewRect("AttackArt", root);
            var catalog = holder.gameObject.AddComponent<AttackFxCatalog>();

            var ids = LineageIds();
            var so = new SerializedObject(catalog);
            var lineages = so.FindProperty("_lineages") ?? throw new InvalidOperationException("AttackFxCatalog._lineages 필드를 찾을 수 없습니다.");
            lineages.arraySize = ids.Count;
            for (int i = 0; i < ids.Count; i++)
            {
                var element = lineages.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("lineageId").stringValue = ids[i];
                element.FindPropertyRelative("shot").objectReferenceValue = ArtLib.Vfx(ShotPrefix + ids[i] + ShotSuffix);
                element.FindPropertyRelative("hit").objectReferenceValue = ArtLib.Vfx(ShotPrefix + ids[i] + "_hit");
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            new Binder(catalog)
                .Set("_slash", ArtLib.Vfx(SkillVfxLibrary.SlashId))
                .Set("_snipeTrail", ArtLib.Vfx(SkillVfxLibrary.SnipeTrailId))
                .Apply();
            return catalog;
        }

        /// <summary>정식 7계보 + Art/VFX의 atk_{계보}_shot 그림에서 찾은 계보 (순서 고정, 중복 없음)</summary>
        static List<string> LineageIds()
        {
            var ids = new List<string>(AttackFxCatalog.KnownLineages);
            var seen = new HashSet<string>(ids, StringComparer.Ordinal);
            string folder = ArtLib.Root + "/VFX";
            if (!AssetDatabase.IsValidFolder(folder)) return ids;
            var found = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (!name.StartsWith(ShotPrefix, StringComparison.Ordinal) || !name.EndsWith(ShotSuffix, StringComparison.Ordinal)) continue;
                string id = name.Substring(ShotPrefix.Length, name.Length - ShotPrefix.Length - ShotSuffix.Length);
                if (id.Length > 0 && seen.Add(id)) found.Add(id);
            }
            found.Sort(StringComparer.Ordinal);
            ids.AddRange(found);
            return ids;
        }

        /// <summary>
        /// 흰 번쩍임 머티리얼 (없으면 만들고, 있으면 셰이더를 맞춘다). 셰이더를 못 찾거나 컴파일 오류면 null
        /// (분홍 오류 셰이더로 번쩍이는 것보다 번쩍임 없이 틴트로 대신하는 편이 낫다)
        /// </summary>
        public static Material WhiteFlashMaterial() => ShaderMaterial(ShaderPath, ShaderName, MaterialPath, "UIWhiteFlash", "흰 번쩍임");

        /// <summary>
        /// 입자 머티리얼 UI/FxPremul (없으면 만들고, 있으면 셰이더를 맞춘다). 셰이더를 못 찾거나 컴파일 오류면 null
        /// (런타임이 기본 UI 머티리얼로 그린다 — 가산 층은 반투명 빛으로)
        /// </summary>
        public static Material PremulMaterial() => ShaderMaterial(PremulShaderPath, PremulShaderName, PremulMaterialPath, "UIFxPremul", "가산 입자");

        /// <summary>빛 번쩍임 머티리얼 UI/FxScreen (스크린 블렌드). 셰이더를 못 찾거나 오류면 null (런타임이 알파 덮개로 약하게)</summary>
        public static Material ScreenMaterial() => ShaderMaterial(ScreenShaderPath, ScreenShaderName, ScreenMaterialPath, "UIFxScreen", "빛 번쩍임");

        /// <summary>셰이더 머티리얼 하나 (없으면 만들고, 있으면 셰이더를 맞춘다). 셰이더가 없거나 컴파일 오류면 경고 + null</summary>
        static Material ShaderMaterial(string shaderPath, string shaderName, string materialPath, string materialName, string label)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null) shader = Shader.Find(shaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogWarning($"[AttackFxBuilder] {shaderName} 셰이더를 쓸 수 없어 {label} 머티리얼 없이 만듭니다 ({shaderPath}).");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialDir);
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        /// <summary>
        /// 계보 손맛 그림을 JuiceArt._lineages에 굽는다 (FX-JUICE §5): 명중 v2 Art/VFX/Juice/atk_{계보}_impact · 치명타 atk_{계보}_crit ·
        /// 총구 불꽃 Art/FX/Juice/muzzle_{계보}. 정식 7계보 + Art/VFX의 새 계보. 없는 그림은 missing에 적고 null로 둔다 (씬을 다시 만들면 묶인다)
        /// </summary>
        public static void BindLineageJuice(JuiceArt juice, List<string> missing)
        {
            var ids = LineageIds();
            var so = new SerializedObject(juice);
            var lineages = so.FindProperty("_lineages") ?? throw new InvalidOperationException("JuiceArt._lineages 필드를 찾을 수 없습니다.");
            lineages.arraySize = ids.Count;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                bool known = Array.IndexOf(AttackFxCatalog.KnownLineages, id) >= 0;
                var element = lineages.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("lineageId").stringValue = id;
                element.FindPropertyRelative("impact").objectReferenceValue = JuiceArtBuilder.Load(JuiceArtBuilder.VfxJuice, ShotPrefix + id + "_impact", known ? missing : null);
                element.FindPropertyRelative("crit").objectReferenceValue = JuiceArtBuilder.Load(JuiceArtBuilder.VfxJuice, ShotPrefix + id + "_crit", known ? missing : null);
                element.FindPropertyRelative("muzzle").objectReferenceValue = JuiceArtBuilder.Load(JuiceArtBuilder.FxJuice, "muzzle_" + id, known ? missing : null);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 흰 번쩍임 덧그림 (같은 그림을 런타임에 넣는다, 처음엔 꺼짐). 머티리얼이 없으면 만들지 않는다 (null)
        /// </summary>
        public static Image FlashOverlay(Transform parent, string name, Material material)
        {
            if (material == null) return null;
            var image = UIKit.Img(parent, name, new Color(1f, 1f, 1f, 0f));
            image.material = material;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        /// <summary>연출 레이어에 하위 Canvas (override sorting 끔, 레이캐스터 없음)</summary>
        public static void AddLayerCanvas(RectTransform layer)
        {
            if (layer == null || layer.GetComponent<Canvas>() != null) return;
            var canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = false;
        }

        /// <summary>바인딩할 값이 없으면 건너뛰는 Set (선택 필드용)</summary>
        public static Binder SetIf(this Binder binder, string field, Object value) => value != null ? binder.Set(field, value) : binder;
    }
}
