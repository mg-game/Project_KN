using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// WP-5A 전투 HUD · 아래 띠 (UX Wave 3) 순수 규칙 — 장면 없이 도우미 함수만 본다.
    ///   · M67 · IA §8.3: 작은 리본은 배너 띠 위 끝에서 293 아래 (MAIN · 시트 모두 기준 화면 372~474), 516x102.
    ///   · 배너 띠(keep-out): 배너가 없으면 겹침 없음 · TryGetKeepOut false.
    ///   · M11: 보스 남은 체력 % (올림, 0~100).
    ///   · M67: 작은 리본 부제는 칸(780)에 맞춰 37 → 27까지 줄인다 (× 0.6 ≥ 16). M23: 알림 띠 설명은 16에서도 넘칠 때만 두 줄.
    ///   · M13: 도전해 볼 만함(권장 90% · 실패 때 힘 110%), 둘레 빛은 한 번 두 번 숨쉬고 옅게, 내려감 배너 글,
    ///     스테이지 고르기 목록(최근에 깬 10개 — 앞 스테이지 빼고), 자동 보스 도전 토스트 '세 판'.
    ///   · M36: 덱 안내 줄을 거두는 조건 (10번 + 끌어 소환), 4성 이상 줄임 별 '★4'.
    ///   · M43: 요약 칩 폭 200~280 · '시너지 N' 글자 (N마다 한 번 만든다), 줄 오른쪽 끝 x 540 안.
    ///   · CR-18: 다 열려 멈춘 시트일 때만 가려진 곳을 자른다 (680).
    ///   · M24: BattleEase 이징 = UIMotion.
    /// </summary>
    public class UiWave3_WP5ATests
    {
        // ───────────── M67 작은 리본 자리 ─────────────

        [Test]
        public void Ribbon_CentredRibbonDropBelowBandTop_MainAndSheet()
        {
            // MAIN: 배너 띠 = 1620 − 156 = 1464, 띠 위 끝 = 기준 화면 130
            var main = BattleBannerView.RibbonRectInArea(1464f);
            float mainTop = 130f + (1464f * 0.5f - main.yMax);
            float mainBottom = 130f + (1464f * 0.5f - main.yMin);
            Assert.AreEqual(372f, mainTop, 0.01f, "리본 위 끝 = 화면 372");
            Assert.AreEqual(474f, mainBottom, 0.01f, "리본 아래 끝 = 화면 474");
            Assert.AreEqual(516f, main.width, 0.01f);
            Assert.AreEqual(102f, main.height, 0.01f);
            Assert.AreEqual(0f, main.center.x, 0.01f, "가로 가운데 (282~798)");

            // 시트: 띠 아래 여백 836 → 높이 1620 − 836 = 784, 위 끝은 그대로
            var sheet = BattleBannerView.RibbonRectInArea(784f);
            Assert.AreEqual(372f, 130f + (784f * 0.5f - sheet.yMax), 0.01f, "시트에서도 같은 화면 자리");
        }

        [Test]
        public void Ribbon_UnknownAreaHeight_FallsBack()
        {
            Assert.AreEqual(-50f, BattleBannerView.RibbonCenterY(0f), 0.001f);
        }

        [Test]
        public void Ribbon_ClearOfHudBand()
        {
            // HUD 아래 끝(시너지 줄) = 로컬 218, 간격 24 이상 (§2.1.7)
            float ribbonTopLocal = BattleBannerView.RibbonDrop - BattleBannerView.BannerSize.y * BattleBannerView.RibbonScale * 0.5f;
            Assert.GreaterOrEqual(ribbonTopLocal - 218f, 24f);
        }

        [Test]
        public void RibbonSubtitle_ShrinksToFitButStaysAboveFloorOnScreen()
        {
            // 0.6배로 줄어든 화면에서도 하한 16 이상
            Assert.GreaterOrEqual(BattleBannerView.RibbonSubtitleMin * BattleBannerView.RibbonScale, UIType.AutoSizeFloor - 0.001f);
            float max = BattleBannerView.RibbonSubtitleSize;
            float min = BattleBannerView.RibbonSubtitleMin;
            Assert.AreEqual(max, BattleBannerView.FitFontSize(500f, 780f, max, min), 1e-4f, "들어가면 그대로");
            // 보상 넷(37에서 ≈ 1,020) → 780 안으로
            float size = BattleBannerView.FitFontSize(1020f, 780f, max, min);
            Assert.LessOrEqual(1020f * size / max, 780f + 0.01f);
            Assert.GreaterOrEqual(size, min);
            Assert.AreEqual(min, BattleBannerView.FitFontSize(5000f, 780f, max, min), 1e-4f, "하한 아래로는 안 줄인다");
            Assert.AreEqual(max, BattleBannerView.FitFontSize(0f, 780f, max, min), 1e-4f);
        }

        // ───────────── M23 알림 띠 설명 ─────────────

        [Test]
        public void Notice_TwoLinesOnlyWhenFloorSizeStillOverflows()
        {
            // 칸 폭 262 (336 − 62 − 12), 18 최대 · 16 하한
            Assert.IsFalse(BattleNoticeView.NeedsTwoLines(250f, 18f, 16f, 262f), "18에서 들어감");
            Assert.IsFalse(BattleNoticeView.NeedsTwoLines(290f, 18f, 16f, 262f), "16으로 줄이면 한 줄 (290 × 16/18 ≈ 258)");
            Assert.IsTrue(BattleNoticeView.NeedsTwoLines(323f, 18f, 16f, 262f), "'그림자가 숨어요 · 빛 특성이 크게 강해져요' (18에서 ≈ 323)");
            Assert.IsTrue(BattleNoticeView.NeedsTwoLines(496f, 18f, 16f, 262f), "좀나방 안내");
            Assert.IsFalse(BattleNoticeView.NeedsTwoLines(0f, 18f, 16f, 262f));
        }

        [Test]
        public void KeepOut_NoBanner_NeverOverlaps()
        {
            if (BattleBannerView.KeepOutActive) Assert.Ignore("다른 테스트가 배너를 띄운 상태");
            Assert.IsFalse(BattleBannerView.OverlapsKeepOut(new Rect(-10000f, -10000f, 20000f, 20000f)));
            Assert.IsFalse(BattleBannerView.TryGetKeepOut(null, out _));
        }

        // ───────────── M11 보스 체력 % ─────────────

        [TestCase(1f, 100)]
        [TestCase(0.995f, 100)]
        [TestCase(0.5f, 50)]
        [TestCase(0.001f, 1)]
        [TestCase(0f, 0)]
        [TestCase(-1f, 0)]
        [TestCase(2f, 100)]
        public void BossPercent_CeilsAndClamps(float ratio, int expected)
        {
            Assert.AreEqual(expected, BattleHud.BossPercent(ratio));
        }

        // ───────────── M13 파밍 · 보스 도전 ─────────────

        [Test]
        public void ChallengeReady_NinetyPercentOfRecommended()
        {
            Assert.IsTrue(BattleHud.IsChallengeReady(900, 1000, 0));
            Assert.IsFalse(BattleHud.IsChallengeReady(899, 1000, 0));
        }

        [Test]
        public void ChallengeReady_TenPercentStrongerThanAtFail()
        {
            Assert.IsTrue(BattleHud.IsChallengeReady(1100, 5000, 1000));
            Assert.IsFalse(BattleHud.IsChallengeReady(1099, 5000, 1000));
            Assert.IsFalse(BattleHud.IsChallengeReady(1099, 5000, 0), "실패 때 힘을 모르면 권장만 본다");
        }

        [Test]
        public void StepBackTitle_UsesLoweredStage()
        {
            Assert.AreEqual("아쉬워요! 1-9에서 힘을 모아요", BattleHud.StepBackTitle(9));
            Assert.AreEqual("아쉬워요! 2-10에서 힘을 모아요", BattleHud.StepBackTitle(20));
        }

        [Test]
        public void StageList_LastTenCleared_NewestFirst()
        {
            // HighestStage는 아직 깨지 않은 앞 스테이지 (마지막 클리어 + 1) — 목록은 그 아래 깬 스테이지부터 (§2.1.5)
            var buffer = new int[BattleHud.StageListCount];
            Assert.AreEqual(10, BattleHud.StageListStages(13, buffer));
            Assert.AreEqual(12, buffer[0], "가장 최근에 깬 스테이지부터");
            Assert.AreEqual(3, buffer[9]);
            Assert.AreEqual(2, BattleHud.StageListStages(3, buffer));
            CollectionAssert.AreEqual(new[] { 2, 1 }, new[] { buffer[0], buffer[1] });
            Assert.AreEqual(0, BattleHud.StageListStages(1, buffer), "아직 깬 스테이지가 없다");
            Assert.AreEqual(0, BattleHud.StageListStages(0, buffer));
            Assert.AreEqual(0, BattleHud.StageListStages(5, null));
        }

        [Test]
        public void StagePanel_RoutesToChapterMap_ListStaysFallback()
        {
            // CR-05 · RM-2 S-7: 받는 쪽(UINav.HasChapterMap)이 있으면 챕터 지도, 없으면 예전 스테이지 목록. 튜토리얼 · 팝업 위면 아무것도, 목록이 열려 있으면 닫기
            Assert.AreEqual(BattleHud.StagePanelAction.ChapterMap, BattleHud.RouteStagePanel(false, false, true));
            Assert.AreEqual(BattleHud.StagePanelAction.StageList, BattleHud.RouteStagePanel(false, false, false), "지도가 없으면 목록 (대신 길)");
            Assert.AreEqual(BattleHud.StagePanelAction.CloseList, BattleHud.RouteStagePanel(false, true, true), "열린 목록은 먼저 닫는다");
            Assert.AreEqual(BattleHud.StagePanelAction.None, BattleHud.RouteStagePanel(true, false, true), "튜토리얼 · 팝업 위");
            Assert.AreEqual(BattleHud.StagePanelAction.None, BattleHud.RouteStagePanel(true, true, false));
            // SB-2: 구독하면 true, 떼면 false (테스트가 남기지 않게 바로 뗀다)
            System.Action<int> receiver = _ => { };
            bool before = UINav.HasChapterMap;
            UINav.ChapterMapRequested += receiver;
            try
            {
                Assert.IsTrue(UINav.HasChapterMap);
            }
            finally
            {
                UINav.ChapterMapRequested -= receiver;
            }
            Assert.AreEqual(before, UINav.HasChapterMap);
        }

        [Test]
        public void StageList_ChoiceNeedsAStageOtherThanCurrent()
        {
            var buffer = new int[BattleHud.StageListCount];
            int count = BattleHud.StageListStages(2, buffer);
            Assert.IsTrue(BattleHud.HasStageChoice(buffer, count, 2), "앞 스테이지(2)에서 1로 돌아갈 수 있다");
            Assert.IsFalse(BattleHud.HasStageChoice(buffer, count, 1), "1만 깼고 지금 1이면 고를 것이 없다");
            Assert.IsFalse(BattleHud.HasStageChoice(buffer, 0, 1));
            Assert.IsFalse(BattleHud.HasStageChoice(null, 3, 1));
        }

        [Test]
        public void AutoChallengeToast_MatchesEveryThreeClears()
        {
            Assert.AreEqual(3, BattleSystem.AutoChallengeEvery, "토스트의 '세 판'을 같이 고친다");
            StringAssert.Contains("세 판을 이기면", BattleHud.AutoChallengeOnText);
        }

        [Test]
        public void BossGlow_PulsesTwiceThenRestsDim()
        {
            Assert.AreEqual(0f, BattleHud.GlowPulseAlpha(0f), 1e-4f, "0에서 시작");
            Assert.AreEqual(BattleHud.GlowRestAlpha, BattleHud.GlowPulseAlpha(BattleHud.GlowPulseTime), 1e-4f);
            Assert.AreEqual(BattleHud.GlowRestAlpha, BattleHud.GlowPulseAlpha(100f), 1e-4f, "끝나면 옅은 빛으로 멈춘다");
            // 두 번 숨쉼: 1/4 · 3/4 지점이 꼭대기
            Assert.Greater(BattleHud.GlowPulseAlpha(BattleHud.GlowPulseTime * 0.25f), 0.75f);
            Assert.Greater(BattleHud.GlowPulseAlpha(BattleHud.GlowPulseTime * 0.75f), 0.75f);
            Assert.Less(BattleHud.GlowPulseAlpha(BattleHud.GlowPulseTime * 0.5f), 0.3f);
            // 끝 무렵은 쉼 값으로 이어진다 (튐 없음)
            Assert.AreEqual(BattleHud.GlowRestAlpha, BattleHud.GlowPulseAlpha(BattleHud.GlowPulseTime - 0.001f), 0.01f);
        }

        // ───────────── M36 덱 ─────────────

        [Test]
        public void DeckHint_RetiresAfterTenSummonsAndADragSummon()
        {
            Assert.IsTrue(EchoDeckView.ShouldRetireHint(EchoDeckView.HintSummonCount, true));
            Assert.IsFalse(EchoDeckView.ShouldRetireHint(EchoDeckView.HintSummonCount - 1, true));
            Assert.IsFalse(EchoDeckView.ShouldRetireHint(1000, false), "끌어서 방향 소환을 해 봐야 거둔다");
        }

        [Test]
        public void CardStars_FourAndUpCollapseToOneStarAndNumber()
        {
            Assert.AreEqual(string.Empty, EchoCardView.StarsLabel(0));
            Assert.AreEqual(UITheme.Stars(3), EchoCardView.StarsLabel(3));
            Assert.AreEqual(UITheme.StarSprite + "4", EchoCardView.StarsLabel(4));
            Assert.AreEqual(UITheme.StarSprite + "5", EchoCardView.StarsLabel(5));
            Assert.AreSame(EchoCardView.StarsLabel(5), EchoCardView.StarsLabel(5), "줄임 글자는 한 번만 만든다");
        }

        // ───────────── M43 시너지 요약 칩 ─────────────

        [Test]
        public void SummaryWidth_ClampedTo200To280()
        {
            Assert.AreEqual(200f, SynergyHudView.SummaryWidth(0f), 0.01f);
            Assert.AreEqual(248f, SynergyHudView.SummaryWidth(100f), 0.01f, "114 + 100 + 34");
            Assert.AreEqual(280f, SynergyHudView.SummaryWidth(500f), 0.01f);
        }

        [Test]
        public void SummaryText_GoldCount_Cached()
        {
            string text = SynergyHudView.SummaryText(3);
            StringAssert.StartsWith("시너지 ", text);
            // 금빛은 UITheme.Gold 토큰에서 (색 값을 박지 않는다 — §1.2)
            Assert.AreEqual("<color=#" + ColorUtility.ToHtmlStringRGB(UITheme.Gold) + ">", SynergyHudView.GoldTag);
            StringAssert.Contains(SynergyHudView.GoldTag, text);
            StringAssert.Contains(">3<", text);
            Assert.AreSame(text, SynergyHudView.SummaryText(3));
            StringAssert.Contains(">40<", SynergyHudView.SummaryText(40), "캐시 밖 수도 만든다");
        }

        [Test]
        public void SynergyRow_RightEdgeStaysLeftOf540()
        {
            // 가장 긴 기믹 칩(왼쪽 56 + 글자 ≤ 6글자 × 24 + 오른쪽 18)에 가장 넓은 요약 칩(280)을 붙여도 x 540 안 (§2.1.6)
            const float rowLeft = 16f;
            const float modifierWidth = 56f + 6 * 24f + 18f;
            float right = rowLeft + modifierWidth + SynergyHudView.ChipGap + SynergyHudView.SummaryMaxWidth;
            Assert.LessOrEqual(right, 540f);
        }

        // ───────────── CR-18 가려진 곳 자르기 ─────────────

        [Test]
        public void Cull_OnlyWhenSheetFullyOpenAndSettled()
        {
            Assert.IsTrue(BattleFrameDriver.ShouldCull(1f, true, true));
            Assert.IsFalse(BattleFrameDriver.ShouldCull(0.99f, true, true));
            Assert.IsFalse(BattleFrameDriver.ShouldCull(1f, false, true), "움직이는 중");
            Assert.IsFalse(BattleFrameDriver.ShouldCull(1f, true, false), "닫기 목표");
            Assert.AreEqual(680f, BattleFrameDriver.CoveredFieldHeight, 0.01f);
        }

        // ───────────── M24 이징 ─────────────

        [Test]
        public void BattleEase_DelegatesToUIMotion()
        {
            for (float t = -0.25f; t <= 1.25f; t += 0.125f)
            {
                Assert.AreEqual(UIMotion.OutCubic(t), BattleEase.OutCubic(t), 1e-6f);
                Assert.AreEqual(UIMotion.InCubic(t), BattleEase.InCubic(t), 1e-6f);
                Assert.AreEqual(UIMotion.InOutSine(t), BattleEase.InOutSine(t), 1e-6f);
                Assert.AreEqual(UIMotion.OutBack(t), BattleEase.OutBack(t), 1e-6f);
                Assert.AreEqual(UIMotion.OutBack(t, 2.4f), BattleEase.OutBack(t, 2.4f), 1e-6f);
            }
        }
    }
}
