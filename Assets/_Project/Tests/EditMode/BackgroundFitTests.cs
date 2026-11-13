using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.Tests
{
    /// <summary>
    /// aspect AF-1: BackgroundFit — 덮기(Cover) · 넣기(Contain) · 그대로(Fill) 사각형, 초점(Focus) 자리, 꼭 남길 영역(Keep)의 배율 · 밀기,
    /// 늘림 없음(언제나 가로/세로 = 그림 비율), 덮음 판정, 그리고 컴포넌트가 그림을 바꿔 끼우면 새 비율로 다시 맞추는지.
    /// 그림 비율은 실제 아트: bg_night 1152×2048, field_tall 1080×2048, intro 1364×2048, content_bg 1024², gacha_banner_wide 1536×512, shop_event_banner 1344×504.
    /// </summary>
    public class BackgroundFitTests
    {
        const float Eps = 0.01f;
        static readonly Vector2 Center = new(0.5f, 0.5f);

        static void AssertRect(Rect r, float x, float y, float w, float h)
        {
            Assert.AreEqual(x, r.x, Eps, "x");
            Assert.AreEqual(y, r.y, Eps, "y");
            Assert.AreEqual(w, r.width, Eps, "w");
            Assert.AreEqual(h, r.height, Eps, "h");
        }

        [Test]
        public void Cover_ExactAspect_IsParent()
        {
            // bg_night (9:16) in the 1080×1920 column
            AssertRect(BackgroundFit.Compute(new Vector2(1080f, 1920f), 1152f / 2048f, BackgroundFitMode.Cover, Center, BackgroundFit.NoKeep), 0f, 0f, 1080f, 1920f);
        }

        [Test]
        public void Cover_ArtWider_FitsHeight_CropsSides()
        {
            // intro 1364×2048 in a 19.5:9 canvas column 1080×2340: height fits, 1558.4 wide, centred
            var r = BackgroundFit.Compute(new Vector2(1080f, 2340f), 1364f / 2048f, BackgroundFitMode.Cover, Center, BackgroundFit.NoKeep);
            AssertRect(r, (1080f - 2340f * 1364f / 2048f) * 0.5f, 0f, 2340f * 1364f / 2048f, 2340f);
            Assert.IsTrue(BackgroundFit.Covers(r, new Vector2(1080f, 2340f)));
        }

        [Test]
        public void Cover_ArtTaller_FitsWidth_CropsTopBottom()
        {
            // bg_night in 3:4 (1440×1920): width fits, 2560 tall, centred → 320 cut top and bottom
            AssertRect(BackgroundFit.Compute(new Vector2(1440f, 1920f), 1152f / 2048f, BackgroundFitMode.Cover, Center, BackgroundFit.NoKeep), 0f, -320f, 1440f, 2560f);
        }

        [Test]
        public void Cover_FocusTop_KeepsTop()
        {
            // old gacha banner 2:1 in 1020×340 with focus (0.5, 1): width fits (510 tall), the top edge stays at the top
            var r = BackgroundFit.Compute(new Vector2(1020f, 340f), 2f, BackgroundFitMode.Cover, new Vector2(0.5f, 1f), BackgroundFit.NoKeep);
            AssertRect(r, 0f, 340f - 510f, 1020f, 510f);
            Assert.AreEqual(340f, r.yMax, Eps);
        }

        [Test]
        public void Cover_FocusIsClamped()
        {
            var r = BackgroundFit.Compute(new Vector2(100f, 100f), 2f, BackgroundFitMode.Cover, new Vector2(5f, -3f), BackgroundFit.NoKeep);
            // focus (1, 0): right edge on the right, bottom on the bottom
            AssertRect(r, -100f, 0f, 200f, 100f);
        }

        [Test]
        public void Contain_ArtWider_FitsWidth_FocusPlaces()
        {
            // banner 3:1 inside a square 300: 300×100, centred vertically; focus y 1 → at the top
            AssertRect(BackgroundFit.Compute(new Vector2(300f, 300f), 3f, BackgroundFitMode.Contain, Center, BackgroundFit.NoKeep), 0f, 100f, 300f, 100f);
            AssertRect(BackgroundFit.Compute(new Vector2(300f, 300f), 3f, BackgroundFitMode.Contain, new Vector2(0.5f, 1f), BackgroundFit.NoKeep), 0f, 200f, 300f, 100f);
        }

        [Test]
        public void Contain_ArtTaller_FitsHeight()
        {
            var r = BackgroundFit.Compute(new Vector2(1920f, 1080f), 0.5625f, BackgroundFitMode.Contain, Center, BackgroundFit.NoKeep);
            AssertRect(r, (1920f - 607.5f) * 0.5f, 0f, 607.5f, 1080f);
            Assert.IsFalse(BackgroundFit.Covers(r, new Vector2(1920f, 1080f)));
        }

        [Test]
        public void Fill_And_UnknownAspect_AreParent()
        {
            AssertRect(BackgroundFit.Compute(new Vector2(1080f, 340f), 3f, BackgroundFitMode.Fill, Center, BackgroundFit.NoKeep), 0f, 0f, 1080f, 340f);
            AssertRect(BackgroundFit.Compute(new Vector2(1080f, 340f), 0f, BackgroundFitMode.Cover, Center, BackgroundFit.NoKeep), 0f, 0f, 1080f, 340f);
        }

        [Test]
        public void NeverDistorts_AcrossParentsAndAspects()
        {
            // 화면 · 그림 비율 격자 전부: Cover/Contain은 가로/세로 = 그림 비율 (≤ 0.1%), Cover는 덮고 Contain은 안에 든다
            float[] parentAspects = { 0.39f, 0.4167f, 0.45f, 0.5625f, 0.625f, 0.75f, 0.8f, 1f, 1.333f, 1.778f, 2.389f };
            float[] artAspects = { 0.527f, 0.5625f, 0.666f, 1f, 2.471f, 2.667f, 3f };
            foreach (float pa in parentAspects)
            {
                var parent = new Vector2(1920f * pa, 1920f);
                foreach (float aa in artAspects)
                {
                    var cover = BackgroundFit.Compute(parent, aa, BackgroundFitMode.Cover, Center, BackgroundFit.NoKeep);
                    var contain = BackgroundFit.Compute(parent, aa, BackgroundFitMode.Contain, Center, BackgroundFit.NoKeep);
                    Assert.AreEqual(aa, cover.width / cover.height, aa * 0.001f, $"cover distortion parent {pa} art {aa}");
                    Assert.AreEqual(aa, contain.width / contain.height, aa * 0.001f, $"contain distortion parent {pa} art {aa}");
                    Assert.IsTrue(BackgroundFit.Covers(cover, parent), $"cover gap parent {pa} art {aa}");
                    Assert.IsTrue(contain.xMin >= -Eps && contain.yMin >= -Eps && contain.xMax <= parent.x + Eps && contain.yMax <= parent.y + Eps,
                        $"contain overflow parent {pa} art {aa}");
                }
            }
        }

        [Test]
        public void Keep_FitsWithoutShrink_WhenItAlreadyFits()
        {
            // intro on a 2.4:1 phone (1080×2592): cover height 2592 (1726.3 wide), keep column 58% = 1001 ≤ 1080 → plain cover, keep inside
            var keep = new Rect(0.21f, 0f, 0.58f, 1f);
            float a = 1364f / 2048f;
            var r = BackgroundFit.Compute(new Vector2(1080f, 2592f), a, BackgroundFitMode.Cover, Center, keep);
            Assert.AreEqual(2592f, r.height, Eps);
            Assert.IsTrue(BackgroundFit.Covers(r, new Vector2(1080f, 2592f)));
            float keepLeft = r.x + keep.xMin * r.width;
            float keepRight = r.x + keep.xMax * r.width;
            Assert.GreaterOrEqual(keepLeft, -Eps);
            Assert.LessOrEqual(keepRight, 1080f + Eps);
        }

        [Test]
        public void Keep_ShrinksScale_WhenTooNarrow_AndStaysWhole()
        {
            // a 1080×3000 window (taller than any phone): the keep column would be 1159 wide → scale drops until it is exactly 1080
            var keep = new Rect(0.21f, 0f, 0.58f, 1f);
            float a = 1364f / 2048f;
            var parent = new Vector2(1080f, 3000f);
            var r = BackgroundFit.Compute(parent, a, BackgroundFitMode.Cover, Center, keep);
            Assert.AreEqual(1080f, keep.width * r.width, 0.05f, "keep width = parent width");
            Assert.AreEqual(a, r.width / r.height, a * 0.001f, "no distortion");
            Assert.IsFalse(BackgroundFit.Covers(r, parent), "backing shows above and below");
            Assert.AreEqual(0f, r.x + keep.xMin * r.width, 0.05f, "keep starts at the left edge");
        }

        [Test]
        public void Keep_SlidesCropToKeepRegionInside()
        {
            // 2:1 art in a square, focus at the left, keep x 0.6~0.9 → slid so the keep region ends at the right edge, still covering
            var keep = new Rect(0.6f, 0f, 0.3f, 1f);
            var parent = new Vector2(100f, 100f);
            var r = BackgroundFit.Compute(parent, 2f, BackgroundFitMode.Cover, new Vector2(0f, 0.5f), keep);
            AssertRect(r, -80f, 0f, 200f, 100f);
            Assert.IsTrue(BackgroundFit.Covers(r, parent));
        }

        [Test]
        public void Covers_Tolerance()
        {
            Assert.IsTrue(BackgroundFit.Covers(new Rect(0.4f, 0f, 99.2f, 100f), new Vector2(100f, 100f)));
            Assert.IsFalse(BackgroundFit.Covers(new Rect(2f, 0f, 98f, 100f), new Vector2(100f, 100f)));
        }

        // ───────────── 컴포넌트 (EditMode, 캔버스 없이 RectTransform만) ─────────────

        GameObject _root;
        Texture2D _tallTexture;
        Texture2D _wideTexture;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_tallTexture != null) Object.DestroyImmediate(_tallTexture);
            if (_wideTexture != null) Object.DestroyImmediate(_wideTexture);
        }

        Image MakeImage(Vector2 parentSize, out RectTransform parent)
        {
            _root = new GameObject("BackgroundFitTest", typeof(RectTransform));
            parent = (RectTransform)_root.transform;
            parent.anchorMin = parent.anchorMax = new Vector2(0.5f, 0.5f);
            parent.sizeDelta = parentSize;
            var child = new GameObject("Art", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Image>();
        }

        static Sprite MakeSprite(Texture2D texture) =>
            Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);

        [Test]
        public void Component_CoversParent_AndRefitsOnSpriteSwap()
        {
            _tallTexture = new Texture2D(9, 16);
            _wideTexture = new Texture2D(32, 9);
            var image = MakeImage(new Vector2(1440f, 1920f), out _);
            image.sprite = MakeSprite(_tallTexture);
            var fit = image.gameObject.AddComponent<BackgroundFit>().Configure(BackgroundFitMode.Cover, new Vector2(0.5f, 0.5f));

            var rect = image.rectTransform.rect;
            Assert.AreEqual(1440f, rect.width, 0.1f);
            Assert.AreEqual(2560f, rect.height, 0.1f);
            Assert.AreEqual(9f / 16f, fit.AppliedAspect, 1e-4f);

            // 비율이 다른 그림으로 바꿔 끼우면 (이야기 장 · 이벤트 배너처럼) 다시 맞춘다 — 늘어나지 않는다
            image.sprite = MakeSprite(_wideTexture);
            rect = image.rectTransform.rect;
            Assert.AreEqual(32f / 9f, fit.AppliedAspect, 1e-4f, "sprite swap re-read the aspect");
            Assert.AreEqual(32f / 9f, rect.width / rect.height, 1e-3f, "no stretch after swap");
            Assert.AreEqual(1920f, rect.height, 0.1f, "wide art fits the height");
        }

        [Test]
        public void Component_RefitsWhenParentResizes_AndTogglesBacking()
        {
            _tallTexture = new Texture2D(9, 16);
            var image = MakeImage(new Vector2(1080f, 1920f), out var parent);
            image.sprite = MakeSprite(_tallTexture);
            var backing = new GameObject("Backing", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            backing.transform.SetParent(parent, false);
            var fit = image.gameObject.AddComponent<BackgroundFit>().Configure(BackgroundFitMode.Contain, new Vector2(0.5f, 0.5f), 0f, backing);
            Assert.IsFalse(backing.enabled, "exact fit covers → no backing");

            parent.sizeDelta = new Vector2(1920f, 1080f);
            fit.Fit();
            var rect = image.rectTransform.rect;
            Assert.AreEqual(607.5f, rect.width, 0.1f);
            Assert.AreEqual(1080f, rect.height, 0.1f);
            Assert.IsTrue(backing.enabled, "contain leaves side gaps → backing on");
        }
    }
}
