using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 글꼴 아틀라스 미리 채우기 (perf PF-4 H3): 두 TMP 글꼴(NotoSansKR Regular · Bold SDF)은 Dynamic + 'Clear Dynamic Data On Build'라
    /// 빌드 · 에디터 종료 때 아틀라스가 비고, 게임에 처음 나오는 글자마다 실행 중에 SDF를 그려 4 MB 아틀라스를 다시 올린다
    /// (첫 시트 열기 109~589 ms · 첫 보스 분노 알림 55~70 ms의 유력한 몫 — 측정 b의 WillRenderCanvases 157~304 ms 가운데 TMP 글 만들기는 4~6 ms뿐).
    /// 이 메뉴는 게임이 쓰는 글자(코드 문자열 · 데이터 애셋 · ASCII)를 에디터에서 한 번에 채우고, 빌드 때 지우지 않게 바꾼다.
    /// 실행 중 OpenType 기능 읽기(TMP Settings m_GetFontFeaturesAtRuntime)도 끈다 — 짝 간격은 여기서 한 번 읽어 둔다.
    /// 글이 바뀌는 내용 묶음이 들어오면 다시 돌린다 (빌드 · 캡처 전). 배치: -executeMethod IdleGame.EditorTools.FontPrewarm.RunBatch
    /// </summary>
    public static class FontPrewarm
    {
        static readonly string[] FontPaths =
        {
            "Assets/_Project/Fonts/NotoSansKR-Regular SDF.asset",
            "Assets/_Project/Fonts/NotoSansKR-Bold SDF.asset",
        };

        static readonly string[] SourceRoots = { "Assets/_Project/Scripts", "Assets/_Project/Editor" };
        const string DataRoot = "Assets/_Project/Data";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>C# 문자열 · 글자 리터럴 (보간 문자열 포함 — 중괄호 안 코드의 글자도 들어가지만 ASCII라 상관없다)</summary>
        static readonly Regex Literal = new Regex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"|'((?:[^'\\\\\\n]|\\\\.))'", RegexOptions.Compiled);

        [MenuItem("IdleGame/Perf/글꼴 아틀라스 미리 채우기 (첫 표시 끊김 없애기)")]
        public static void Run() => Prewarm(true);

        /// <summary>배치 모드 (-executeMethod). 결과는 로그 한 줄씩</summary>
        public static void RunBatch() => Prewarm(false);

        /// <summary>채울 글자 모음 (ASCII 인쇄 가능 문자 + 코드 · 데이터에 나오는 ASCII 밖 글자, 제어 · 서로게이트 제외). 시험 · 확인용</summary>
        public static string CollectCharacters()
        {
            var set = new HashSet<char>();
            for (char c = ' '; c <= '~'; c++) set.Add(c);
            foreach (var root in SourceRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    foreach (Match m in Literal.Matches(File.ReadAllText(file)))
                    {
                        string text = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                        AddNonAscii(set, text);
                    }
                }
            }
            if (Directory.Exists(DataRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { DataRoot }))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null) AddNonAscii(set, EditorJsonUtility.ToJson(asset));
                }
            }
            var chars = new List<char>(set);
            chars.Sort();
            var sb = new StringBuilder(chars.Count);
            foreach (char c in chars) sb.Append(c);
            return sb.ToString();
        }

        static void AddNonAscii(HashSet<char> set, string text)
        {
            foreach (char c in text)
            {
                if (c <= '~' || char.IsControl(c) || char.IsSurrogate(c) || c == '﻿') continue;
                set.Add(c);
            }
        }

        static void Prewarm(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Fonts] 플레이 중에는 채우지 않아요 — 멈춘 뒤 다시 눌러 주세요");
                return;
            }
            string chars = CollectCharacters();
            var report = new StringBuilder();
            foreach (var path in FontPaths)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    report.Append($"\n  {path}: 없음");
                    continue;
                }
                var so = new SerializedObject(font);
                var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
                if (clear != null && clear.boolValue)
                {
                    clear.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                int before = font.characterTable.Count;
                font.TryAddCharacters(chars, out string missing, true);
                EditorUtility.SetDirty(font);
                report.Append($"\n  {font.name}: 글자 {before} → {font.characterTable.Count} · 아틀라스 {font.atlasTextureCount}장 · 글꼴에 없는 글자 {missing?.Length ?? 0}");
                if (!string.IsNullOrEmpty(missing)) report.Append($" ({Shorten(missing)})");
            }
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var features = so.FindProperty("m_GetFontFeaturesAtRuntime");
                if (features != null && features.boolValue)
                {
                    features.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Fonts] 미리 채우기 끝 — 모은 글자 {chars.Length}개{report}");
            if (interactive) EditorUtility.DisplayDialog("글꼴 미리 채우기", $"모은 글자 {chars.Length}개{report}", "확인");
        }

        static string Shorten(string s) => s.Length <= 40 ? s : s.Substring(0, 40) + "…";
    }
}
