using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IdleGame.Verify
{
    /// <summary>
    /// 검증 도구가 함께 쓰는 순수 계산 (AutoShot · UiSceneLint · UiWave3_WP9Tests — UX-SPEC §5.1 도우미):
    /// 명암비(WCAG 상대 휘도 · 반투명 글자 섞기) · 글자 아래 바탕 밝기 어림 · 배지 묶음 수 · TMP 꾸밈 태그 빼기 · 쓰지 않는 말투 찾기 · 사각형 겹침.
    /// 장면도 상태도 갖지 않는다 (할당은 부르는 쪽이 넘긴 목록 안에서만).
    /// </summary>
    public static class VerifyMath
    {
        /// <summary>화면 글에 쓰지 않는 말 (§1.10 해요체 — '개방' 대신 '열려요', 합쇼체 '습니다' · '하시겠습니까' 대신 해요체)</summary>
        public static readonly string[] ForbiddenCopy = { "개방", "습니다", "하시겠" };

        /// <summary>글자 명암비 기준 (§1.2: 24px 이하 글자 ≥ 4.5:1)</summary>
        public const float ContrastText = 4.5f;

        /// <summary>sRGB 채널 하나 → 선형 (WCAG 2.x)</summary>
        public static float Linear(float channel) =>
            channel <= 0.04045f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        /// <summary>WCAG 상대 휘도 0~1 (알파는 보지 않는다 — 이미 섞인 화면 색을 넘긴다)</summary>
        public static float RelativeLuminance(Color color) =>
            0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);

        /// <summary>선형 → sRGB 채널 하나 (Linear의 반대, 0~1)</summary>
        public static float Encode(float linear) =>
            linear <= 0.0031308f ? linear * 12.92f : 1.055f * Mathf.Pow(linear, 1f / 2.4f) - 0.055f;

        /// <summary>
        /// 반투명 글자(색 face, 보이는 알파 alpha = 글자 α × 조상 CanvasGroup α)가 휘도 backgroundLuminance인 바탕 위에 섞여 보이는 상대 휘도.
        /// 바탕은 그 휘도의 회색으로 보고 감마 값에서 섞는다 (프로젝트가 Gamma 색 공간 — UI는 감마 값에서 섞인다). alpha ≥ 1이면 글자 색 그대로
        /// </summary>
        public static float BlendedLuminance(Color face, float alpha, float backgroundLuminance)
        {
            alpha = Mathf.Clamp01(alpha);
            if (alpha >= 1f || float.IsNaN(backgroundLuminance)) return RelativeLuminance(face);
            float gray = Encode(Mathf.Clamp01(backgroundLuminance));
            var mixed = new Color(Mathf.Lerp(gray, face.r, alpha), Mathf.Lerp(gray, face.g, alpha), Mathf.Lerp(gray, face.b, alpha), 1f);
            return RelativeLuminance(mixed);
        }

        /// <summary>WCAG 명암비 (밝은 쪽 + 0.05) / (어두운 쪽 + 0.05), 1~21</summary>
        public static float ContrastRatio(float luminanceA, float luminanceB)
        {
            float high = Mathf.Max(luminanceA, luminanceB);
            float low = Mathf.Min(luminanceA, luminanceB);
            return (high + 0.05f) / (low + 0.05f);
        }

        /// <summary>
        /// 글자 사각형 안에서 잰 화면 휘도들(samples, 순서를 바꾼다)로 바탕 휘도를 어림한다: 글자보다 바탕이 넓으니
        /// 글자가 가운데값보다 밝으면 어두운 쪽 30번째 백분위, 어두우면 밝은 쪽 70번째 백분위 (글자 획 · 안티에일리어싱을 피한다).
        /// 표본이 없으면 NaN
        /// </summary>
        public static float EstimateBackground(List<float> samples, float textLuminance)
        {
            if (samples == null || samples.Count == 0) return float.NaN;
            samples.Sort();
            float median = samples[samples.Count / 2];
            float percentile = textLuminance >= median ? 0.3f : 0.7f;
            int index = Mathf.Clamp(Mathf.RoundToInt(percentile * (samples.Count - 1)), 0, samples.Count - 1);
            return samples[index];
        }

        /// <summary>
        /// 외곽선 글자의 외곽선 휘도 어림 (samples, 순서를 바꾼다): 글자 반대쪽 끝 10번째 백분위 — 밝은 글자면 어두운 쪽 10%, 어두운 글자면 밝은 쪽 90%.
        /// 그려진 외곽선이 글 사각형의 10% 넘게 덮을 때만 바탕과 다른 값이 나온다 (외곽선이 없으면 바탕 · 안티에일리어싱 쪽 값). 표본이 없으면 NaN
        /// </summary>
        public static float EstimateStroke(List<float> samples, float textLuminance)
        {
            if (samples == null || samples.Count == 0) return float.NaN;
            samples.Sort();
            float median = samples[samples.Count / 2];
            float percentile = textLuminance >= median ? 0.1f : 0.9f;
            int index = Mathf.Clamp(Mathf.RoundToInt(percentile * (samples.Count - 1)), 0, samples.Count - 1);
            return samples[index];
        }

        /// <summary>
        /// 점들을 radius 안에서 이어지는 묶음으로 나눈 수 (배지 묶음 — 가까운 배지 둘은 한 덩어리로 읽힌다, M09 'MAIN ≤5 묶음').
        /// 단순 합집합-찾기 O(n²) (화면의 배지는 수십 개 이하)
        /// </summary>
        public static int CountClusters(IReadOnlyList<Vector2> points, float radius)
        {
            if (points == null || points.Count == 0) return 0;
            int count = points.Count;
            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;
            float limit = radius * radius;
            int clusters = count;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if ((points[i] - points[j]).sqrMagnitude > limit) continue;
                    int a = Root(parent, i), b = Root(parent, j);
                    if (a == b) continue;
                    parent[b] = a;
                    clusters--;
                }
            }
            return clusters;
        }

        static int Root(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        /// <summary>TMP 꾸밈 태그(&lt;b&gt; · &lt;color=…&gt; · &lt;size=…&gt; · &lt;nobr&gt; · &lt;sprite=…&gt; …)를 뺀 보이는 글. &lt;br&gt;은 줄바꿈</summary>
        public static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text ?? string.Empty;
            var plain = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    int reopen = text.IndexOf('<', i + 1);
                    if (close > i && (reopen < 0 || reopen > close))
                    {
                        string tag = text.Substring(i + 1, close - i - 1).Trim();
                        if (tag.Equals("br", StringComparison.OrdinalIgnoreCase)) plain.Append('\n');
                        i = close;
                        continue;
                    }
                }
                plain.Append(c);
            }
            return plain.ToString();
        }

        /// <summary>쓰지 않는 말이 들어 있으면 그 말 (ForbiddenCopy 순서로 첫 번째), 없으면 null. 꾸밈 태그는 빼고 본다</summary>
        public static string FindForbiddenCopy(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string plain = StripRichText(text);
            foreach (var word in ForbiddenCopy)
            {
                if (plain.IndexOf(word, StringComparison.Ordinal) >= 0) return word;
            }
            return null;
        }

        /// <summary>두 사각형이 slack(px)보다 많이 겹친다 (맞닿음 · 0.5px 이하 겹침은 겹침이 아니다). NaN이 있으면 false</summary>
        public static bool Overlaps(Rect a, Rect b, float slack = 0.5f) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > slack && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > slack;

        /// <summary>네 변이 모두 tolerance 안에서 같다</summary>
        public static bool Near(Rect a, Rect b, float tolerance) =>
            Mathf.Abs(a.xMin - b.xMin) <= tolerance && Mathf.Abs(a.xMax - b.xMax) <= tolerance &&
            Mathf.Abs(a.yMin - b.yMin) <= tolerance && Mathf.Abs(a.yMax - b.yMax) <= tolerance;

        /// <summary>
        /// 누르는 자리 = 그림 사각형에서 raycastPadding(왼 x · 아래 y · 오른 z · 위 w, 음수 = 넓힘)을 뺀 크기 (UIKit.HitPad 규칙, UX-SPEC §1.5)
        /// </summary>
        public static Vector2 HitSize(Vector2 graphicSize, Vector4 raycastPadding) =>
            new(graphicSize.x - raycastPadding.x - raycastPadding.z, graphicSize.y - raycastPadding.y - raycastPadding.w);
    }
}
