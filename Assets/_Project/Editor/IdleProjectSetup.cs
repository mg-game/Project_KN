using System;
using System.IO;
using System.Linq;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 프로젝트 초기 세팅 메뉴.
    /// 1) 모바일 세로 화면 설정 + TMP 기본 리소스 임포트
    /// 2) 한글 폰트 에셋(보통 · 굵게), 데이터 에셋, UI 프리팹, 메인 씬 생성
    /// 배치 모드: -executeMethod IdleGame.EditorTools.IdleProjectSetup.BatchSetupProject / BatchBuildAll
    /// <para>
    /// 글꼴 (M27 · M81 · M28): Noto Sans KR 보통(Regular)과 진짜 굵게(Bold, SIL OFL — Fonts/NotoSansKR-Bold-OFL.txt)를 같은 설정의 동적 SDF로 만들고,
    /// 보통 글꼴의 굵기 표 7번(Bold)에 굵은 글꼴을 넣는다 → bold:true / FontStyles.Bold 글자가 가짜 굵게(번짐 + 자간) 대신 진짜 Bold 글리프로 그려진다.
    /// 외곽선 글자는 TMP_Settings.matchMaterialPreset(켜 둠)이 굵은 글꼴 아틀라스에도 같은 외곽선 재질을 복사해 쓴다.
    /// 공유 외곽선 재질(전장 맨글자용 · 빨간 배지 숫자용)과 한글 줄바꿈 규칙도 빌드마다 다시 맞춘다.
    /// </para>
    /// </summary>
    public static class IdleProjectSetup
    {
        public const string Root = "Assets/_Project";
        const string FontSourcePath = Root + "/Fonts/NotoSansKR-Regular.otf";
        const string FontAssetPath = Root + "/Fonts/NotoSansKR-Regular SDF.asset";
        const string FontBoldSourcePath = Root + "/Fonts/NotoSansKR-Bold.otf";
        const string FontBoldAssetPath = Root + "/Fonts/NotoSansKR-Bold SDF.asset";
        /// <summary>전장 위 맨글자용 공유 외곽선 재질 (M81, UIKit.Outlined)</summary>
        internal const string OutlineMaterialPath = Root + "/Fonts/NotoSansKR-Regular SDF Outline.mat";
        /// <summary>빨간 배지 위 흰 숫자 · NEW 외곽선 재질 (M82, SceneParts.BuildBadge)</summary>
        internal const string BadgeOutlineMaterialPath = Root + "/Fonts/NotoSansKR-Regular SDF Badge.mat";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>TMP 굵기 표에서 Bold(700)의 자리</summary>
        const int BoldWeightIndex = 7;
        /// <summary>TMP 기본 가짜 굵게 자간 (굵은 글꼴이 없을 때 되돌리는 값)</summary>
        const float FakeBoldSpacing = 7f;

        [MenuItem("IdleGame/Setup/1. 프로젝트 설정 + TMP 리소스 임포트", priority = 1)]
        public static void SetupProject()
        {
            ApplyPlayerSettings();
            ImportTmpEssentials();
        }

        [MenuItem("IdleGame/Setup/2. 데이터 + UI + 메인 씬 생성", priority = 2)]
        public static void BuildAll() => Build(interactive: !Application.isBatchMode);

        /// <summary>TMP 기본 리소스(TMP Settings)가 임포트되어 있는지</summary>
        public static bool HasTmpResources => AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null;

        /// <summary>
        /// 데이터 + UI + 메인 씬 생성. interactive면 덮어쓰기 확인과 수정된 씬 저장을 묻는다
        /// (자동 세팅 IdleAutoSetup은 저장 여부를 먼저 물은 뒤 false로 부른다)
        /// </summary>
        internal static void Build(bool interactive)
        {
            if (!HasTmpResources)
            {
                throw new InvalidOperationException("TMP 리소스가 없습니다. 'IdleGame/Setup/1'을 먼저 실행하세요.");
            }
            if (interactive)
            {
                if (File.Exists(IdleSceneBuilder.ScenePath) &&
                    !EditorUtility.DisplayDialog("메인 씬 재생성", "Main 씬과 UI 프리팹을 다시 생성합니다.\n직접 수정한 내용은 사라집니다. 계속할까요?", "생성", "취소"))
                {
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }

            // 백그라운드 실행 등 플레이어 설정도 함께 저장한다 (메뉴 1을 건너뛴 프로젝트 대비)
            ApplyPlayerSettings();

            // 새 씬을 열면 그 전에 로드한 에셋 참조가 끊기므로 씬을 먼저 만든다
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var font = EnsureFontAsset(FontSourcePath, FontAssetPath, true);
            // 아래는 빌드마다 (폰트 에셋이 이미 있어도) 다시 맞춘다
            var bold = EnsureFontAsset(FontBoldSourcePath, FontBoldAssetPath, false);
            EnsureBoldWeight(font, bold);
            EnsureOutlineMaterial(font);
            EnsureBadgeOutlineMaterial(font);
            SetTmpDefaultFont(font);

            var content = IdleDataBuilder.BuildContent();
            // 메타 데이터(기능 개방/퀘스트/미션/출석/바구니/튜토리얼)는 따로 만든다 (content.Meta)
            MetaDataBuilder.Apply(content);
            IdleSceneBuilder.Build(scene, font, content);

            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGame] 메인 씬 생성 완료: " + IdleSceneBuilder.ScenePath);
        }

        /// <summary>
        /// 배치 모드에서는 ImportPackage가 -quit 전에 끝나지 않으므로 TMP 리소스는
        /// 커맨드라인 -importPackage "&lt;ugui 패키지&gt;/Package Resources/TMP Essential Resources.unitypackage"로 넣는다.
        /// </summary>
        public static void BatchSetupProject()
        {
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
        }

        public static void BatchBuildAll() => BuildAll();

        static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // 포커스를 잃어도 계속 진행 (에디터 플레이 모드·PC 빌드)
            PlayerSettings.runInBackground = true;
            Debug.Log("[IdleGame] PlayerSettings: 세로 고정, Android IL2CPP/ARM64, 백그라운드 실행");
        }

        static void ImportTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null) return;

            var package = Directory
                .GetFiles(Path.GetFullPath("Packages/com.unity.ugui"), "TMP Essential Resources.unitypackage", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (package == null) throw new FileNotFoundException("TMP Essential Resources.unitypackage를 찾을 수 없습니다.");

            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
            Debug.Log("[IdleGame] TMP Essential Resources 임포트: " + package);
        }

        /// <summary>
        /// 한글 표시용 동적 SDF 폰트 에셋 (사용하는 글자만 런타임에 아틀라스에 추가됨). 보통 · 굵게 같은 설정:
        /// 샘플 64, 여백 6, SDFAA, 2048x2048, Dynamic, 여러 아틀라스 허용. 이미 있으면 그대로 쓴다.
        /// 원본 OTF가 없거나 TMP가 글꼴 면을 읽지 못하면(CreateFontAsset이 null — 깨진 파일 · 임포트 설정 Include Font Data 꺼짐)
        /// required면 예외, 아니면 경고 후 null (굵은 글꼴이 없으면 EnsureBoldWeight가 자리를 비워 예전처럼 가짜 굵게).
        /// </summary>
        static TMP_FontAsset EnsureFontAsset(string sourcePath, string assetPath, bool required)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null)
            {
                if (required) throw new FileNotFoundException("한글 폰트가 없습니다: " + sourcePath);
                Debug.LogWarning("[IdleGame] 폰트가 없어 이 굵기는 가짜 굵게로 그립니다: " + sourcePath);
                return null;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                // TMP가 "Unable to load font face" 경고를 이미 남긴다. 굵게는 빌드 전체를 멈추지 않고 가짜 굵게로 물러난다
                if (required) throw new InvalidOperationException("한글 폰트 에셋을 만들 수 없습니다 (폰트 임포트 설정의 Include Font Data 확인): " + sourcePath);
                Debug.LogWarning("[IdleGame] 폰트 에셋을 만들 수 없어 이 굵기는 가짜 굵게로 그립니다 (Include Font Data 확인): " + sourcePath);
                return null;
            }
            fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        /// <summary>
        /// 진짜 굵게 (M27, 빌드마다): 보통 글꼴의 굵기 표 Bold(7) 자리에 굵은 글꼴을 넣고 가짜 굵게 자간을 0으로 (굵은 글꼴을 쓸 때 TMP는 자간을 더하지 않지만
        /// 혹시 굵은 글꼴에 없는 글자가 보통 글꼴로 떨어져도 자간이 벌어지지 않게). 굵은 글꼴이 없으면 자리를 비우고 TMP 기본 자간(7)으로 되돌린다.
        /// 가짜 굵게 번짐(boldStyle 0.75)은 그대로 둔다. 굵은 글꼴 자신의 대체 목록 · TMP 기본 대체 목록에는 넣지 않는다.
        /// </summary>
        static void EnsureBoldWeight(TMP_FontAsset regular, TMP_FontAsset bold)
        {
            if (regular == null) return;
            var so = new SerializedObject(regular);
            var table = so.FindProperty("m_FontWeightTable");
            if (table != null && table.isArray)
            {
                if (table.arraySize <= BoldWeightIndex) table.arraySize = 10;
                var slot = table.GetArrayElementAtIndex(BoldWeightIndex).FindPropertyRelative("regularTypeface");
                if (slot != null) slot.objectReferenceValue = bold;
            }
            var spacing = so.FindProperty("boldSpacing");
            if (spacing != null) spacing.floatValue = bold != null ? 0f : FakeBoldSpacing;
            if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(regular);
            Debug.Log(bold != null
                ? "[IdleGame] 굵은 글자: " + bold.name + " (진짜 Bold 글리프)"
                : "[IdleGame] 굵은 글자: 굵은 글꼴이 없어 가짜 굵게");
        }

        /// <summary>
        /// 전장 위 맨글자용 공유 외곽선 재질 (M81): Fonts/NotoSansKR-Regular SDF Outline.mat — 보통 글꼴 재질을 복사해
        /// OUTLINE_ON, 두께 0.18, 색 Background(불투명), 부드러움 0, 팽창 0.05, 밑그림 없음. 빌드마다 값을 다시 입힌다 (나중에 고친 값 반영).
        /// </summary>
        internal static Material EnsureOutlineMaterial(TMP_FontAsset font) =>
            EnsureFontMaterial(font, OutlineMaterialPath, UITheme.WithAlpha(UITheme.Background, 1f), 0.18f, 0.05f);

        /// <summary>빨간 배지 위 흰 숫자 · NEW 외곽선 재질 (M82): BadgeTextOutline #8A1F3A, 두께 0.2, 팽창 0.1 (외곽선이 숫자를 먹지 않게)</summary>
        internal static Material EnsureBadgeOutlineMaterial(TMP_FontAsset font) =>
            EnsureFontMaterial(font, BadgeOutlineMaterialPath, UITheme.WithAlpha(UITheme.BadgeTextOutline, 1f), 0.2f, 0.1f);

        static Material EnsureFontMaterial(TMP_FontAsset font, string path, Color outline, float width, float dilate)
        {
            if (font == null || font.material == null) return null;
            var source = font.material;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                // 글꼴 아틀라스 · SDF 값은 원래 재질을 따른다 (글꼴을 다시 만들어도 맞게)
                material.shader = source.shader;
                CopyFloat(source, material, ShaderUtilities.ID_GradientScale);
                CopyFloat(source, material, ShaderUtilities.ID_TextureWidth);
                CopyFloat(source, material, ShaderUtilities.ID_TextureHeight);
                CopyFloat(source, material, ShaderUtilities.ID_WeightNormal);
                CopyFloat(source, material, ShaderUtilities.ID_WeightBold);
                if (source.HasProperty(ShaderUtilities.ID_MainTex))
                    material.SetTexture(ShaderUtilities.ID_MainTex, source.GetTexture(ShaderUtilities.ID_MainTex));
            }

            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            material.DisableKeyword(ShaderUtilities.Keyword_Glow);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CopyFloat(Material from, Material to, int id)
        {
            if (from.HasProperty(id) && to.HasProperty(id)) to.SetFloat(id, from.GetFloat(id));
        }

        /// <summary>
        /// TMP 설정 (빌드마다): 기본 글꼴, 한글 현대 줄바꿈 규칙(M28 — 낱말 가운데서 줄을 바꾸지 않는다),
        /// 대체 · 굵은 글꼴에 같은 재질 복사(matchMaterialPreset — 굵은 외곽선 글자가 외곽선을 잃지 않게, 켜 둔다).
        /// </summary>
        static void SetTmpDefaultFont(TMP_FontAsset font)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            var hangul = so.FindProperty("m_UseModernHangulLineBreakingRules");
            if (hangul != null) hangul.boolValue = true;
            var matchPreset = so.FindProperty("m_matchMaterialPreset");
            if (matchPreset != null) matchPreset.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
