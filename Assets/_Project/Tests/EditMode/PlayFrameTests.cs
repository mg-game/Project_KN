using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// aspect AF-2: 세로 놀이 틀 (PlayFrame) 계산 — 화면 비율별 기둥(픽셀 · 캔버스 단위), 폰은 틀 없음, 안전 영역 ∩ 기둥 앵커,
    /// 틀에 묶인 부모 안의 안전 영역(겹친 앵커). 숫자는 S/aspect/PLAN.md §3 표와 같다.
    /// </summary>
    public class PlayFrameTests
    {
        const float Max = PlayFrame.DefaultMaxAspect;
        const float Eps = 1e-5f;

        // 화면 w×h → 기둥 픽셀 폭 · 왼쪽 여백 (0 = 틀 없음 = 화면 전체)
        [TestCase(1080, 1920, 1080, 0)]   // 9:16 기준
        [TestCase(1170, 2532, 1170, 0)]   // 19.5:9
        [TestCase(1080, 2400, 1080, 0)]   // 20:9
        [TestCase(1096, 2560, 1096, 0)]   // 21:9
        [TestCase(1080, 2592, 1080, 0)]   // 2.4:1
        [TestCase(904, 2316, 904, 0)]     // 폴드 겉화면 23.1:9
        [TestCase(1080, 1919, 1080, 0)]   // 반올림 오차 (SnapTolerance 안)
        [TestCase(1600, 2560, 1440, 80)]  // 16:10 태블릿
        [TestCase(1536, 2048, 1152, 192)] // 3:4 iPad
        [TestCase(1600, 2000, 1125, 237)] // 4:5
        [TestCase(1812, 1812, 1019, 396)] // 1:1 폴더블 안쪽
        [TestCase(1024, 768, 432, 296)]   // 4:3 가로 창
        [TestCase(1920, 1080, 608, 656)]  // 16:9 PC (607.5 → 짝수 반올림 608)
        [TestCase(3440, 1440, 810, 1315)] // 21:9 울트라와이드
        [TestCase(960, 540, 304, 328)]    // 캡처 창 16:9
        [TestCase(800, 800, 450, 175)]    // 캡처 창 1:1
        [TestCase(600, 800, 450, 75)]     // 캡처 창 3:4
        [TestCase(720, 900, 506, 107)]    // 캡처 창 4:5
        public void Column_PerAspect(int w, int h, int columnPixels, int left)
        {
            var column = PlayFrame.ColumnFor(w, h, Max);
            var pixels = PlayFrame.ColumnPixels(w, h, column);
            Assert.AreEqual(left, pixels.xMin, 0.01f, "왼쪽 여백 px");
            Assert.AreEqual(columnPixels, pixels.width, 0.01f, "기둥 폭 px");
            Assert.AreEqual(h, pixels.height, 0.01f, "기둥 높이 = 화면");
            // 가운데: 양옆 여백 차이 ≤ 1px
            Assert.LessOrEqual(Mathf.Abs(pixels.xMin - (w - pixels.xMax)), 1.01f, "가운데");
            bool framed = columnPixels < w;
            Assert.AreEqual(framed, column.x > 0f || column.y < 1f, "틀 여부");
        }

        [TestCase(1600, 2560)]
        [TestCase(1536, 2048)]
        [TestCase(1600, 2000)]
        [TestCase(1812, 1812)]
        [TestCase(1024, 768)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        [TestCase(3440, 1440)]
        public void FramedColumn_IsReferenceLayout_InCanvasUnits(int w, int h)
        {
            // 9:16보다 넓으면 CanvasScalerAdapter가 세로 기준(높이 1920)으로 맞춘다 → 기둥 = 캔버스 단위 1080 (±1, 픽셀 반올림), 비율 ≤ 9:16 + 0.1%
            var column = PlayFrame.ColumnFor(w, h, Max);
            float canvasWidth = 1920f * w / h;
            float columnCanvas = (column.y - column.x) * canvasWidth;
            Assert.AreEqual(1080f, columnCanvas, 1f, "캔버스 단위 기둥 폭");
            Assert.LessOrEqual(columnCanvas / 1920f, Max * 1.001f, "기둥 비율 상한");
        }

        [TestCase(0, 0)]
        [TestCase(0, 1920)]
        [TestCase(1080, 0)]
        public void DegenerateScreen_NoFrame(int w, int h)
        {
            Assert.AreEqual(PlayFrame.FullColumn, PlayFrame.ColumnFor(w, h, Max));
        }

        [Test]
        public void MaxAspectZero_DisablesFrame()
        {
            Assert.AreEqual(PlayFrame.FullColumn, PlayFrame.ColumnFor(1920, 1080, 0f));
        }

        [Test]
        public void SafeAnchors_NoFrame_RootParent_IsSafeAreaOverScreen()
        {
            // 예전 SafeArea와 같아야 한다: 19.5:9 폰 노치 (위 141, 아래 102)
            var safe = Rect.MinMaxRect(0f, 102f, 1170f, 2532f - 141f);
            PlayFrame.SafeAnchors(safe, 1170f, 2532f, PlayFrame.FullColumn, new Rect(0f, 0f, 1f, 1f), out var min, out var max);
            Assert.AreEqual(0f, min.x, Eps);
            Assert.AreEqual(102f / 2532f, min.y, Eps);
            Assert.AreEqual(1f, max.x, Eps);
            Assert.AreEqual((2532f - 141f) / 2532f, max.y, Eps);
        }

        [Test]
        public void SafeAnchors_Framed_FullSafeArea_IsColumn()
        {
            var column = PlayFrame.ColumnFor(1920, 1080, Max);
            PlayFrame.SafeAnchors(new Rect(0f, 0f, 1920f, 1080f), 1920f, 1080f, column, new Rect(0f, 0f, 1f, 1f), out var min, out var max);
            Assert.AreEqual(656f / 1920f, min.x, Eps);
            Assert.AreEqual(1264f / 1920f, max.x, Eps);
            Assert.AreEqual(0f, min.y, Eps);
            Assert.AreEqual(1f, max.y, Eps);
        }

        [Test]
        public void SafeAnchors_Framed_WithInsets_IsIntersection()
        {
            // 3:4 태블릿, 아래 내비 막대 48 · 왼쪽 컷아웃 120 (기둥 왼쪽 192 밖이라 기둥이 이긴다)
            var column = PlayFrame.ColumnFor(1536, 2048, Max);
            var safe = Rect.MinMaxRect(120f, 48f, 1536f, 2048f);
            PlayFrame.SafeAnchors(safe, 1536f, 2048f, column, new Rect(0f, 0f, 1f, 1f), out var min, out var max);
            Assert.AreEqual(192f / 1536f, min.x, Eps);
            Assert.AreEqual(1344f / 1536f, max.x, Eps);
            Assert.AreEqual(48f / 2048f, min.y, Eps);
            Assert.AreEqual(1f, max.y, Eps);
        }

        [Test]
        public void SafeAnchors_NestedInFramedLayer_IsRelativeToParent()
        {
            // 틀에 묶인 층(TutorialOverlay 등) 안의 SafeArea: 부모 = 기둥 → 안전 영역이 화면 전체면 부모 그대로 (0~1)
            var column = PlayFrame.ColumnFor(800, 800, Max);
            var parent = PlayFrame.Nest(new Vector2(column.x, 0f), new Vector2(column.y, 1f), new Rect(0f, 0f, 1f, 1f));
            PlayFrame.SafeAnchors(new Rect(0f, 0f, 800f, 800f), 800f, 800f, column, parent, out var min, out var max);
            Assert.AreEqual(Vector2.zero.x, min.x, Eps);
            Assert.AreEqual(0f, min.y, Eps);
            Assert.AreEqual(1f, max.x, Eps);
            Assert.AreEqual(1f, max.y, Eps);

            // 위 노치 40px이면 세로만 줄어든다
            PlayFrame.SafeAnchors(Rect.MinMaxRect(0f, 0f, 800f, 760f), 800f, 800f, column, parent, out min, out max);
            Assert.AreEqual(0f, min.x, Eps);
            Assert.AreEqual(1f, max.x, Eps);
            Assert.AreEqual(760f / 800f, max.y, Eps);
        }

        [Test]
        public void SafeAnchors_NoOverlap_FallsBackToColumn()
        {
            var column = PlayFrame.ColumnFor(1920, 1080, Max);
            // 안전 영역이 기둥과 안 겹치는 이상한 값 → 기둥 전체
            PlayFrame.SafeAnchors(Rect.MinMaxRect(0f, 0f, 100f, 1080f), 1920f, 1080f, column, new Rect(0f, 0f, 1f, 1f), out var min, out var max);
            Assert.AreEqual(column.x, min.x, Eps);
            Assert.AreEqual(column.y, max.x, Eps);
        }

        [Test]
        public void Nest_ComposesAnchors()
        {
            // 부모 앵커 x 0.25~0.75 안의 (0.5~1) → 0.5~0.75
            var r = PlayFrame.Nest(new Vector2(0.25f, 0f), new Vector2(0.75f, 1f), Rect.MinMaxRect(0.5f, 0f, 1f, 0.5f));
            Assert.AreEqual(0.5f, r.xMin, Eps);
            Assert.AreEqual(0.75f, r.xMax, Eps);
            Assert.AreEqual(0f, r.yMin, Eps);
            Assert.AreEqual(0.5f, r.yMax, Eps);
        }

        [Test]
        public void Intersect_Disjoint_IsEmpty()
        {
            var r = PlayFrame.Intersect(Rect.MinMaxRect(0f, 0f, 10f, 10f), Rect.MinMaxRect(20f, 0f, 30f, 10f));
            Assert.AreEqual(0f, r.width);
        }
    }
}
