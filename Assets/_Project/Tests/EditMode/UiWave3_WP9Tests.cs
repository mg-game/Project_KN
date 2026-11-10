using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// WP-9 검증 도구 (UX Wave 3) 순수 계산 — AutoShot · UiSceneLint가 함께 쓰는 IdleGame.Verify.VerifyMath (Verify.Runtime)와
    /// UiSceneLint.Exemptions 목록 모양. 이 어셈블리는 Verify 어셈블리를 참조하지 않아 이름으로 부른다.
    ///   · 명암비(WCAG): 흰/검 21:1, #767676/흰 ≈ 4.54:1, 글자 아래 바탕 어림(밝은 글자는 어두운 쪽 백분위), 반투명 글자 섞기(감마 값).
    ///   · 배지 묶음 수 (반지름 안 이어짐) — MAIN ≤ 5 묶음 검사.
    ///   · TMP 꾸밈 태그 빼기 · '개방' · '습니다' · '하시겠' 찾기 (태그 안은 보지 않는다).
    ///   · 사각형 겹침 (맞닿음은 아님) · 누르는 자리 = 그림 − raycastPadding.
    ///   · 예외 목록: 규칙 이름이 알려진 것 · 이유가 있음 · (규칙, 경로) 중복 없음.
    /// </summary>
    public class UiWave3_WP9Tests
    {
        const string MathType = "IdleGame.Verify.VerifyMath, Verify.Runtime";
        const string LintType = "IdleGame.Verify.UiSceneLint, Verify.Editor";

        static object Call(string type, string method, params object[] args)
        {
            var t = Type.GetType(type);
            Assert.IsNotNull(t, $"{type} 없음");
            var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(m, $"{type}.{method} 없음");
            try
            {
                return m.Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException ?? e;
            }
        }

        static float Luminance(Color c) => (float)Call(MathType, "RelativeLuminance", c);

        static float Contrast(Color a, Color b) => (float)Call(MathType, "ContrastRatio", Luminance(a), Luminance(b));

        // ───────────── 명암비 ─────────────

        [Test]
        public void ContrastRatio_MatchesWcagReferencePairs()
        {
            Assert.AreEqual(1f, Luminance(Color.white), 1e-4f);
            Assert.AreEqual(0f, Luminance(Color.black), 1e-4f);
            Assert.AreEqual(21f, Contrast(Color.white, Color.black), 0.01f);
            Assert.AreEqual(21f, Contrast(Color.black, Color.white), 0.01f, "순서와 상관없다");
            var gray = new Color(0x76 / 255f, 0x76 / 255f, 0x76 / 255f);
            Assert.AreEqual(4.54f, Contrast(gray, Color.white), 0.02f, "#767676 on white = 4.54:1 (WCAG 경계 예)");
            Assert.AreEqual(1f, Contrast(gray, gray), 1e-4f);
        }

        [Test]
        public void EstimateBackground_TakesTheSideOppositeTheText()
        {
            // 어두운 바탕 70% + 밝은 글자 획 30%
            var dark = new List<float>();
            for (int i = 0; i < 70; i++) dark.Add(0.02f);
            for (int i = 0; i < 30; i++) dark.Add(0.9f);
            Assert.AreEqual(0.02f, (float)Call(MathType, "EstimateBackground", dark, 0.95f), 1e-4f, "밝은 글자 → 어두운 쪽");

            // 밝은 바탕 70% + 어두운 글자 획 30%
            var light = new List<float>();
            for (int i = 0; i < 30; i++) light.Add(0.03f);
            for (int i = 0; i < 70; i++) light.Add(0.8f);
            Assert.AreEqual(0.8f, (float)Call(MathType, "EstimateBackground", light, 0.01f), 1e-4f, "어두운 글자 → 밝은 쪽");

            Assert.IsTrue(float.IsNaN((float)Call(MathType, "EstimateBackground", new List<float>(), 0.5f)), "표본 없음 = NaN");
        }

        /// <summary>외곽선 어림 (S-05-contrast 외곽선 길): 글자 반대쪽 끝 10% — 그려진 외곽선이 10%를 넘게 덮어야 바탕과 다른 값이 나온다</summary>
        [Test]
        public void EstimateStroke_TakesTheFarTenthOppositeTheText()
        {
            // 밝은 글자 + 어두운 외곽선 20% + 초록 바탕 60%
            var outlined = new List<float>();
            for (int i = 0; i < 20; i++) outlined.Add(0.01f);
            for (int i = 0; i < 60; i++) outlined.Add(0.45f);
            for (int i = 0; i < 20; i++) outlined.Add(0.9f);
            Assert.AreEqual(0.01f, (float)Call(MathType, "EstimateStroke", outlined, 0.9f), 1e-4f, "그려진 외곽선");
            Assert.AreEqual(0.45f, (float)Call(MathType, "EstimateBackground", outlined, 0.9f), 1e-4f, "바탕 어림은 초록 그대로");

            // 외곽선이 얇아(5%) 10%에 못 미치면 바탕 쪽 값
            var thin = new List<float>();
            for (int i = 0; i < 5; i++) thin.Add(0.01f);
            for (int i = 0; i < 75; i++) thin.Add(0.45f);
            for (int i = 0; i < 20; i++) thin.Add(0.9f);
            Assert.AreEqual(0.45f, (float)Call(MathType, "EstimateStroke", thin, 0.9f), 1e-4f, "얇은 외곽선은 외곽선으로 치지 않는다");

            // 어두운 글자면 밝은 쪽 90%
            var dark = new List<float>();
            for (int i = 0; i < 30; i++) dark.Add(0.02f);
            for (int i = 0; i < 50; i++) dark.Add(0.5f);
            for (int i = 0; i < 20; i++) dark.Add(0.95f);
            Assert.AreEqual(0.95f, (float)Call(MathType, "EstimateStroke", dark, 0.01f), 1e-4f, "어두운 글자 → 밝은 쪽 끝");
            Assert.IsTrue(float.IsNaN((float)Call(MathType, "EstimateStroke", new List<float>(), 0.5f)), "표본 없음 = NaN");
        }

        static float Blend(Color face, float alpha, float background) => (float)Call(MathType, "BlendedLuminance", face, alpha, background);

        /// <summary>반투명 글자 섞기 (S-05-contrast '보스까지' Cream@0.8): 불투명이면 글자 색, 투명이면 바탕, 그 사이는 감마 값에서 섞은 색의 휘도</summary>
        [Test]
        public void BlendedLuminance_MixesTranslucentTextOverBackground()
        {
            foreach (float v in new[] { 0f, 0.002f, 0.04f, 0.2f, 0.5f, 1f })
                Assert.AreEqual(v, (float)Call(MathType, "Linear", (float)Call(MathType, "Encode", v)), 1e-4f, $"Linear(Encode({v})) — Encode는 Linear의 반대");

            var cream = new Color(1f, 0.95f, 0.85f);
            float face = Luminance(cream);
            Assert.AreEqual(face, Blend(cream, 1f, 0.01f), 1e-5f, "불투명 = 글자 색 그대로");
            Assert.AreEqual(0.01f, Blend(cream, 0f, 0.01f), 1e-4f, "투명 = 바탕");
            float translucent = Blend(cream, 0.8f, 0.01f);
            Assert.Less(translucent, face, "어두운 바탕 위 반투명 크림은 더 어둡게 보인다");
            Assert.Greater(translucent, 0.01f);

            float gray = (float)Call(MathType, "Encode", 0.01f);
            var mixed = new Color(Mathf.Lerp(gray, cream.r, 0.8f), Mathf.Lerp(gray, cream.g, 0.8f), Mathf.Lerp(gray, cream.b, 0.8f));
            Assert.AreEqual(Luminance(mixed), translucent, 1e-5f, "바탕 휘도의 회색과 감마 값에서 섞는다 (Gamma 색 공간)");
        }

        // ───────────── 배지 묶음 ─────────────

        [Test]
        public void CountClusters_JoinsChainsWithinRadius()
        {
            var points = new List<Vector2> { new(0, 0), new(50, 0), new(100, 0), new(500, 0), new(500, 600) };
            Assert.AreEqual(3, (int)Call(MathType, "CountClusters", points, 60f), "0-50-100이 이어져 한 묶음 + 따로 둘");
            Assert.AreEqual(5, (int)Call(MathType, "CountClusters", points, 10f));
            Assert.AreEqual(1, (int)Call(MathType, "CountClusters", points, 1000f));
            Assert.AreEqual(0, (int)Call(MathType, "CountClusters", new List<Vector2>(), 60f));
        }

        // ───────────── 글 ─────────────

        [Test]
        public void StripRichText_RemovesTmpTags_AndKeepsText()
        {
            Assert.AreEqual("필요 900 · 보유 350", Call(MathType, "StripRichText", "<b>필요 900</b> <size=26><color=#9A90BE>· 보유 350</color></size>"));
            Assert.AreEqual("한 줄\n두 줄", Call(MathType, "StripRichText", "한 줄<br>두 줄"));
            Assert.AreEqual("<nobr>", Call(MathType, "StripRichText", "<<nobr>nobr>"), "닫히지 않은 '<'는 글자로 둔다");
            Assert.AreEqual("1 < 2", Call(MathType, "StripRichText", "1 < 2"));
            Assert.AreEqual(string.Empty, Call(MathType, "StripRichText", (object)null));
        }

        [Test]
        public void FindForbiddenCopy_FlagsGaebangAndFormalEndings()
        {
            Assert.AreEqual("개방", Call(MathType, "FindForbiddenCopy", "스테이지 2-9 클리어 시 개방"));
            Assert.AreEqual("습니다", Call(MathType, "FindForbiddenCopy", "<color=#FFF>받았습니다</color>"));
            Assert.AreEqual("하시겠", Call(MathType, "FindForbiddenCopy", "정말 삭제하시겠어요?"));
            Assert.IsNull(Call(MathType, "FindForbiddenCopy", "스테이지 2-9 클리어하면 열려요"));
            Assert.IsNull(Call(MathType, "FindForbiddenCopy", "<size=90%>계보 공명</size>"));
            Assert.IsNull(Call(MathType, "FindForbiddenCopy", string.Empty));
        }

        // ───────────── 사각형 ─────────────

        [Test]
        public void Overlaps_IgnoresTouchingEdges()
        {
            var a = Rect.MinMaxRect(16, 356, 246, 424);
            Assert.IsFalse((bool)Call(MathType, "Overlaps", a, Rect.MinMaxRect(246, 356, 400, 424), 0.5f), "맞닿음");
            Assert.IsFalse((bool)Call(MathType, "Overlaps", a, Rect.MinMaxRect(282, 372, 798, 474), 0.5f), "보스 버튼 · 리본은 x로 떨어져 있다 (IA §3.2)");
            Assert.IsTrue((bool)Call(MathType, "Overlaps", a, Rect.MinMaxRect(200, 300, 300, 400), 0.5f));
            Assert.IsTrue((bool)Call(MathType, "Near", a, Rect.MinMaxRect(17, 355, 245, 425), 2f));
            Assert.IsFalse((bool)Call(MathType, "Near", a, Rect.MinMaxRect(20, 356, 246, 424), 2f));
        }

        [Test]
        public void HitSize_SubtractsRaycastPadding()
        {
            var size = (Vector2)Call(MathType, "HitSize", new Vector2(132, 66), new Vector4(0, -11, 0, -11));
            Assert.AreEqual(new Vector2(132, 88), size, "위아래 −11 → 66 + 22 = 88");
            size = (Vector2)Call(MathType, "HitSize", new Vector2(76, 76), new Vector4(-12, -12, -12, -12));
            Assert.AreEqual(new Vector2(100, 100), size, "닫기 X 76 + 패딩 12 → 100 (§1.5)");
        }

        // ───────────── 예외 목록 ─────────────

        [Test]
        public void Exemptions_HaveKnownRule_Reason_AndNoDuplicates()
        {
            var type = Type.GetType(LintType);
            Assert.IsNotNull(type, $"{LintType} 없음");
            var rules = (string[])type.GetField("Rules", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var list = (IEnumerable)type.GetField("Exemptions", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var seen = new HashSet<string>();
            int count = 0;
            foreach (var exemption in list)
            {
                count++;
                var t = exemption.GetType();
                string rule = (string)t.GetField("Rule").GetValue(exemption);
                string path = (string)t.GetField("Path").GetValue(exemption);
                string reason = (string)t.GetField("Reason").GetValue(exemption);
                CollectionAssert.Contains(rules, rule, $"예외 {path}: 모르는 규칙 '{rule}'");
                Assert.IsFalse(string.IsNullOrWhiteSpace(path), "예외 경로가 비었다");
                Assert.IsFalse(string.IsNullOrWhiteSpace(reason), $"예외 {rule} {path}: 이유가 없다");
                Assert.IsTrue(seen.Add(rule + "|" + path), $"예외 중복: {rule} {path}");
                Assert.AreNotEqual("targets", rule, "코치 대상 · 고정 이름은 예외를 두지 않는다");
                Assert.AreNotEqual("names", rule, "코치 대상 · 고정 이름은 예외를 두지 않는다");
            }
            Assert.Greater(count, 0);
        }
    }
}
