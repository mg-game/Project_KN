using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    /// <summary>
    /// 장면 정적 검사 (UX-SPEC §4.3 WP-9 · §5.3, UX-SPEC-IA §8.7 — Main.unity + UI 프리팹, 장면을 만든 뒤에 돈다):
    ///   · 코치 · FTUE 대상 경로(CoachTargets — TabMain · MenuButton · EventButton 포함 · 덱 칸 · MetaContent.Targets · 메타 콘텐츠의 대상)가 모두 있다.
    ///   · 고정 이름 · 경로(IA §5.8 시트 머리 · ≡ 메뉴 칸 · 전장 · 스모크 테스트 · AutoShot이 찾는 이름)가 그대로다.
    ///   · 그림 없는 Filled Image가 없다 (M02 — 채움이 줄지 않는다).
    ///   · 글자 기본 크기 ≥ 16, 자동 크기 최소 ≥ 16 (§1.3 — 18 아래는 메모로 세기만).
    ///   · 버튼 누르는 자리(그림 + raycastPadding) ≥ 88 x 88 (§1.5 — 기준 1080x1920 앵커로 잴 수 있는 것만).
    ///   · 화면 글에 '개방' · '습니다' · '하시겠'이 없다 (§1.10 해요체).
    ///   · 화면 글의 모든 글자가 Noto Sans KR(+ 대신 글꼴)에 있다.
    ///   · (SPEC4, RM-2 V2) 플레이어가 보는 문자열 상수에 '장비'가 없다 (짝짝이 이야기 줄만) · Scripts/UI 코드에 'GetField('가 없다 (FtueBattleGuard는 S-9까지 빚).
    /// 규칙 · 알려진 예외(한 곳, 이유 · 담당)는 Verify.Editor의 IdleGame.Verify.UiSceneLint에 있다 — 이 어셈블리는 그 어셈블리(TextMeshPro · uGUI를
    /// 타입으로 쓰는 검사기)를 참조하지 않아 이름으로 부른다. 예외 메모(빚 · stale)는 테스트 출력에 남긴다.
    /// 장면은 한 번만 연다 (이미 열려 있으면 그대로 쓰고, 아니면 덧붙여 열었다가 닫는다).
    /// </summary>
    public class UiSceneLintTests
    {
        const string LintType = "IdleGame.Verify.UiSceneLint, Verify.Editor";
        static readonly string[] Rules = { "targets", "names", "filled", "font", "hit", "copy", "glyph", "gear", "reflect" };

        static Dictionary<string, (string failures, string notes)> s_results;
        static string s_error;

        [OneTimeSetUp]
        public void RunLint()
        {
            s_results = null;
            s_error = null;
            var type = Type.GetType(LintType);
            if (type == null)
            {
                s_error = $"{LintType} 형식을 찾지 못했다 (Verify.Editor 어셈블리가 컴파일되지 않았나?)";
                return;
            }
            var method = type.GetMethod("RunMany", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                s_error = "UiSceneLint.RunMany(string[]) 없음";
                return;
            }
            // 장면을 열면 편집 모드에서 도는 컴포넌트가 로그를 남길 수 있다 — 이 검사는 결과 목록만 본다
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var output = (string[])method.Invoke(null, new object[] { Rules });
                s_results = new Dictionary<string, (string, string)>();
                for (int i = 0; i < Rules.Length; i++) s_results[Rules[i]] = (output[i * 2], output[i * 2 + 1]);
            }
            catch (TargetInvocationException e)
            {
                s_error = $"UiSceneLint.RunMany 예외: {e.InnerException?.GetType().Name}: {e.InnerException?.Message}";
            }
        }

        [SetUp]
        public void IgnoreSceneLogs() => LogAssert.ignoreFailingMessages = true;

        static void AssertRule(string rule, string what)
        {
            Assert.IsNull(s_error, s_error);
            Assert.IsNotNull(s_results, "검사 결과 없음");
            Assert.IsTrue(s_results.TryGetValue(rule, out var result), $"규칙 {rule} 결과 없음");
            if (!string.IsNullOrEmpty(result.notes)) TestContext.WriteLine(result.notes);
            Assert.IsTrue(string.IsNullOrEmpty(result.failures), $"{what}:\n{result.failures}");
            StringAssert.DoesNotContain("checked=0\n", result.notes + "\n", $"{what}: 검사한 것이 하나도 없다 (장면 · 이름이 바뀌었나?)");
        }

        [Test]
        public void CoachAndFtueTargets_ResolveInMainScene() =>
            AssertRule("targets", "코치 · FTUE 대상 경로가 Canvas 아래에 없다 (CoachTargets · MetaContent.Targets · 메타 콘텐츠)");

        [Test]
        public void FrozenNamesAndPaths_StayInMainScene() =>
            AssertRule("names", "고정 이름 · 경로가 없다 (UX-SPEC-IA §5.8 · 스모크 테스트 · AutoShot)");

        [Test]
        public void FilledImages_AlwaysHaveASprite() =>
            AssertRule("filled", "그림 없는 Filled Image (M02 — 채움이 줄지 않는다)");

        [Test]
        public void TextSizes_StayAtOrAboveSixteen() =>
            AssertRule("font", "글자 크기 16 미만 (UX-SPEC §1.3 — 예외는 UiSceneLint.Exemptions)");

        [Test]
        public void ButtonHitTargets_AreAtLeast88() =>
            AssertRule("hit", "누르는 자리 88 미만 (UX-SPEC §1.5 — UIKit.HitPad로 넓힌다, 예외는 UiSceneLint.Exemptions)");

        [Test]
        public void ScreenCopy_UsesHaeyoStyle_WithoutGaebang() =>
            AssertRule("copy", "화면 글에 '개방' · '습니다' · '하시겠' (UX-SPEC §1.10)");

        [Test]
        public void ScreenText_GlyphsExistInNotoSansKr() =>
            AssertRule("glyph", "글꼴에 없는 글자 (□로 보인다 — 그림 · 다른 글자로 바꾼다)");

        [Test]
        public void PlayerStrings_DoNotPromiseEquipment() =>
            AssertRule("gear", "문자열 상수에 '장비' (SPEC4 §11.4 · §13.5 f — 장비는 이번 범위가 아니다, 예외는 UiSceneLint.Exemptions)");

        [Test]
        public void UiCode_DoesNotReflectIntoFields() =>
            AssertRule("reflect", "Scripts/UI 코드에 'GetField(' (SPEC4 §13.3 — 전투 훅을 쓴다, 예외는 UiSceneLint.Exemptions)");
    }
}
