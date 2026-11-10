using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// UX Wave 3 WP-5A 검증(r1) 고침 — 화면 없이 순수 규칙과 상수만 본다.
    ///   · M13 도전해 볼 만함: 10분의 1 단위 정수 비교 (float 1.1f를 넓힌 오차로 '딱 10% 강해짐'이 빠지던 것), 아주 큰 힘에서도 넘치지 않는다.
    ///   · M11 수선점 내구도 받침: 글자 폭 + 28, 96 이상 · 막대 안쪽(254 − 10) 이하, 자릿수가 바뀔 때만 다시 잰다.
    ///   · M67 배너 부제 받침: 리본 아래 금실(배너 y 44~50) 바로 밑에 매달리고, 작은 리본에서는 화면 474 아래로 조금만,
    ///     가운데 배너에서는 보상 줄 0번(위 끝 y −14)과 겹치지 않으며, 폭은 글 + 48 (220~828, 배너 860 안).
    /// </summary>
    public class UiWave3_WP5AVerifyFixTests
    {
        // ───────────── M13 도전해 볼 만함 ─────────────

        [Test]
        public void ChallengeReady_ExactTenthBoundaries()
        {
            Assert.IsTrue(BattleHud.IsChallengeReady(1100, 0, 1000), "실패 때 1,000 → 딱 1,100이면 준비");
            Assert.IsFalse(BattleHud.IsChallengeReady(1099, 0, 1000));
            Assert.IsTrue(BattleHud.IsChallengeReady(11, 0, 10));
            Assert.IsFalse(BattleHud.IsChallengeReady(10, 0, 10), "같은 힘은 아직");
            Assert.IsTrue(BattleHud.IsChallengeReady(9, 10, 0), "권장 10의 90% = 9");
            Assert.IsFalse(BattleHud.IsChallengeReady(8, 10, 0));
            Assert.IsTrue(BattleHud.IsChallengeReady(10, 11, 0), "권장 11의 90% = 9.9 → 10이면 준비");
            Assert.IsFalse(BattleHud.IsChallengeReady(9, 11, 0));
        }

        [Test]
        public void ChallengeReady_HugePower_NoOverflow()
        {
            const long huge = long.MaxValue / 2; // × 10 · × 11이면 long을 넘친다
            Assert.IsTrue(BattleHud.IsChallengeReady(huge, huge, 0));
            Assert.IsFalse(BattleHud.IsChallengeReady(huge / 2, huge, 0));
            Assert.IsTrue(BattleHud.IsChallengeReady(long.MaxValue, 0, huge), "1.1배를 넘는 힘");
            Assert.IsFalse(BattleHud.IsChallengeReady(huge, 0, huge), "같은 힘은 아직");
        }

        [Test]
        public void ChallengeReady_NothingKnown_NeverReady()
        {
            Assert.IsFalse(BattleHud.IsChallengeReady(1000, 0, 0));
            Assert.IsFalse(BattleHud.IsChallengeReady(0, 0, 0));
        }

        // ───────────── M11 수선점 내구도 받침 ─────────────

        [Test]
        public void HpPlate_FitsTextInsideBar()
        {
            const float limit = 254f - 2f * 5f;
            Assert.AreEqual(BattleHud.HpPlateMin, BattleHud.HpPlateWidth(0f, limit), 1e-4f, "짧은 글도 최소 폭");
            Assert.AreEqual(100f + BattleHud.HpPlatePad, BattleHud.HpPlateWidth(100f, limit), 1e-4f, "'422 / 422' ≈ 100 → 128");
            Assert.AreEqual(limit, BattleHud.HpPlateWidth(400f, limit), 1e-4f, "막대 안쪽을 넘지 않는다");
            Assert.AreEqual(0f, BattleHud.HpPlateWidth(50f, -10f), 1e-4f, "막대 폭이 이상해도 음수가 되지 않는다");
        }

        [TestCase(0, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(422, 3)]
        [TestCase(99999, 5)]
        [TestCase(int.MaxValue, 10)]
        [TestCase(-5, 1)]
        public void HpPlate_Digits(int value, int expected)
        {
            Assert.AreEqual(expected, BattleHud.Digits(value));
        }

        // ───────────── M67 배너 부제 받침 ─────────────

        [Test]
        public void SubtitlePlate_HangsJustBelowRibbonGoldEdge()
        {
            // 리본(배너 y 16~154, 9-slice 배율 1.4): 아래 금실 y 44~50, 그 밑 그림자 끝 ≈ 42 — 받침 위 끝은 그 바로 밑
            Assert.LessOrEqual(BattleBannerView.SubtitlePlateTop, 42f, "금실 · 바느질 선에 걸치지 않는다");
            Assert.GreaterOrEqual(BattleBannerView.SubtitlePlateTop, 36f, "리본 아래 끝에서 떨어져 떠 있지 않는다");
        }

        [Test]
        public void SubtitlePlate_Ribbon_StaysJustBelowRibbonZone()
        {
            // 작은 리본 = 화면 372~474 (배너 아래 끝 = 474). 받침 아래 끝 = 474 + (높이 − 위 끝) × 0.6
            float below = (BattleBannerView.RibbonSubtitlePlateHeight - BattleBannerView.SubtitlePlateTop) * BattleBannerView.RibbonScale;
            Assert.LessOrEqual(below, 12f, "화면 474 아래로 12 이내 (≈ 8.4)");
            // 부제 37 (화면 ≈ 22)이 받침(화면 ≈ 32) 안에 위아래 여유를 두고 들어간다
            Assert.GreaterOrEqual(BattleBannerView.RibbonSubtitlePlateHeight * BattleBannerView.RibbonScale,
                BattleBannerView.RibbonSubtitleSize * BattleBannerView.RibbonScale + 8f);
        }

        [Test]
        public void SubtitlePlate_CentreBanner_ClearOfRewardLines()
        {
            // 보상 줄 0번 = TopCenter(0, −184) → 배너 아래 끝 기준 위 끝 170 − 184 = −14 (빌더)
            float plateBottom = BattleBannerView.SubtitlePlateTop - BattleBannerView.SubtitlePlateHeight;
            Assert.Greater(plateBottom, BattleBannerView.BannerSize.y - 184f, "받침 아래 끝(−4)이 보상 줄 위 끝(−14)보다 위");
        }

        [Test]
        public void SubtitlePlate_WidthFitsTextWithinBanner()
        {
            Assert.AreEqual(BattleBannerView.SubtitlePlateMin, BattleBannerView.SubtitlePlateWidth(0f), 1e-4f, "짧은 부제도 최소 폭");
            Assert.AreEqual(500f + BattleBannerView.SubtitlePlatePad, BattleBannerView.SubtitlePlateWidth(500f), 1e-4f);
            Assert.AreEqual(BattleBannerView.SubtitlePlateMax, BattleBannerView.SubtitlePlateWidth(780f), 1e-4f, "칸 780 가득 = 828");
            Assert.AreEqual(BattleBannerView.SubtitlePlateMax, BattleBannerView.SubtitlePlateWidth(5000f), 1e-4f);
            Assert.LessOrEqual(BattleBannerView.SubtitlePlateMax, BattleBannerView.BannerSize.x, "배너 폭 860 안");
        }

        // ───────────── 큰 배너 제목 칸 (검증 rm2 r1 — spec4_early/01 · 07) ─────────────

        [Test]
        public void BannerTitle_StaysOnFlatRibbonBand()
        {
            // 예전 좌우 30: '수선점이 위험해요… 다시 도전!'의 '!'가 오른쪽 접힘 · 금별에 걸리고 '아쉬워요! 1-9에서 힘을 모아요'의 왼쪽 끝이 꼬리에 닿았다
            Assert.GreaterOrEqual(BattleBannerView.TitleSideInset, BattleBannerView.RibbonTailWidth + 16f, "꼬리 · 접힘 · 별 밖, 금테에서 16 이상");
            float band = BattleBannerView.BannerSize.x - 2f * BattleBannerView.TitleSideInset;
            // 가장 긴 제목 '아쉬워요! 12-10에서 힘을 모아요' ≈ 13.7 em (r1 캡처에서 잰 굵은 글꼴 폭) → 하한 크기에서도 칸에 든다
            Assert.GreaterOrEqual(band, 14f * BattleBannerView.TitleMinSize, "제목 칸 ≥ 14 em × 하한");
            Assert.AreEqual(UIType.PopupTitle, BattleBannerView.TitleMinSize, 1e-4f, "하한 = 팝업 제목 크기");
            Assert.LessOrEqual(BattleBannerView.TitleMinSize, 44f, "안내 배너(최대 44)도 하한 ≤ 최대");
            Assert.GreaterOrEqual(BattleBannerView.TitleMinSize * BattleBannerView.RibbonScale, UIType.AutoSizeFloor, "작은 리본(0.6배)에서도 16px 이상");
        }

        [Test]
        public void BannerTitle_InsetWidensOldSceneMargins()
        {
            var go = new GameObject("Title", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(30f, 56f);
                rt.offsetMax = new Vector2(-30f, -18f);
                Assert.IsTrue(BattleBannerView.ApplyTitleInset(rt, BattleBannerView.TitleSideInset), "다시 굽기 전 씬(좌우 30) → 넓힌다");
                Assert.AreEqual(BattleBannerView.TitleSideInset, rt.offsetMin.x, 1e-4f);
                Assert.AreEqual(-BattleBannerView.TitleSideInset, rt.offsetMax.x, 1e-4f);
                Assert.AreEqual(56f, rt.offsetMin.y, 1e-4f, "세로는 그대로");
                Assert.AreEqual(-18f, rt.offsetMax.y, 1e-4f);
                Assert.IsFalse(BattleBannerView.ApplyTitleInset(rt, BattleBannerView.TitleSideInset), "두 번째는 그대로");

                rt.offsetMin = new Vector2(120f, 56f);
                rt.offsetMax = new Vector2(-30f, -18f);
                Assert.IsTrue(BattleBannerView.ApplyTitleInset(rt, 100f));
                Assert.AreEqual(120f, rt.offsetMin.x, 1e-4f, "이미 넓은 쪽은 그대로");
                Assert.AreEqual(-100f, rt.offsetMax.x, 1e-4f);

                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(-400f, 56f);
                rt.offsetMax = new Vector2(400f, -18f);
                Assert.IsFalse(BattleBannerView.ApplyTitleInset(rt, 100f), "가로로 늘어나지 않는 칸은 건드리지 않는다");
                Assert.AreEqual(-400f, rt.offsetMin.x, 1e-4f);
                Assert.IsFalse(BattleBannerView.ApplyTitleInset(null, 100f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
