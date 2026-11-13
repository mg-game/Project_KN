using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 길 잃은 반딧불 비행 수명 (RI-6a, SPEC4 §13.8 — FIX-P4-3b): 옆 화면 밖(반폭 + 60px)에서 들어와 반대쪽 같은 거리 밖까지 날아간 뒤 숨는다.
    /// 예전 고정 8초 × 140px/s = 1120px은 1080 폭에서 +520(화면 안)에서 사라졌다. 반딧불 켜기(Q2 스트레치)의 전제
    /// </summary>
    public class StrayFireflyViewTests
    {
        const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;
        GameObject _parent;

        [TearDown]
        public void TearDown()
        {
            if (_parent != null) Object.DestroyImmediate(_parent);
        }

        [Test]
        public void LifeFor_CoversBothEdges()
        {
            Assert.AreEqual(60f, StrayFireflyView.EdgeMargin);
            Assert.AreEqual(1200f / 140f, StrayFireflyView.LifeFor(540f), 1e-4f, "1080 폭: 1200px ÷ 140px/s");
            foreach (float half in new[] { 270f, 540f, 720f, 960f })
            {
                float travel = StrayFireflyView.LifeFor(half) * StrayFireflyView.Speed;
                Assert.AreEqual(2f * (half + StrayFireflyView.EdgeMargin), travel, 0.01f, $"반폭 {half}: 양쪽 화면 밖까지");
            }
            Assert.AreEqual(StrayFireflyView.LifeFor(0f), StrayFireflyView.LifeFor(-10f), 1e-6f, "음수 반폭은 0으로");
            // 예전 고정 수명은 화면 안에서 끝났다 (−600 + 1120 = +520 < 540)
            Assert.Less(-(540f + StrayFireflyView.EdgeMargin) + StrayFireflyView.Life * StrayFireflyView.Speed, 540f);
        }

        [Test]
        public void Flight_LeavesTheColumn_BeforeItHides([Values(1080f, 1440f)] float width)
        {
            var view = Build(width, out var root);
            view.Spawn(false);
            Assert.IsTrue(view.IsFlying, "Spawn 뒤 날고 있다");
            Assert.IsTrue(root.gameObject.activeSelf);
            float half = width * 0.5f;
            float startX = root.anchoredPosition.x;
            Assert.AreEqual(half + StrayFireflyView.EdgeMargin, Mathf.Abs(startX), 0.01f, "옆 화면 밖에서 들어온다");
            Assert.AreEqual(StrayFireflyView.LifeFor(half), view.FlightLife, 1e-4f, "수명은 이번 부모 폭으로");

            const float step = 1f / 30f;
            float lastX = startX;
            int frames = 0;
            while (view.IsFlying && frames < 3000)
            {
                view.Tick(step, false, false, false);
                if (view.IsFlying) lastX = root.anchoredPosition.x;
                frames++;
            }
            Assert.IsFalse(view.IsFlying, "수명이 끝나면 숨는다");
            Assert.IsFalse(root.gameObject.activeSelf, "숨으면 뿌리를 끈다");
            Assert.AreEqual(-Mathf.Sign(startX), Mathf.Sign(lastX), "반대쪽으로 날아갔다");
            // 마지막으로 그린 자리가 반대쪽 화면 밖 (한 프레임 이동 안) — 120px 빛무리까지 열 밖
            Assert.GreaterOrEqual(Mathf.Abs(lastX), half + StrayFireflyView.EdgeMargin - StrayFireflyView.Speed * step - 0.5f,
                $"사라지기 직전 x {lastX:0.#} (반폭 {half})");
            Assert.Greater(frames * step, StrayFireflyView.Life, "예전 고정 8초보다 오래 난다");
        }

        [Test]
        public void Spawn_WithoutRoot_DoesNothing()
        {
            _parent = new GameObject("FireflyNoRoot", typeof(RectTransform));
            var view = _parent.AddComponent<StrayFireflyView>();
            Assert.DoesNotThrow(() => view.Spawn(false));
            Assert.IsFalse(view.IsFlying, "빌더가 만들기 전(사용자 씬)에는 날지 않는다");
            Assert.DoesNotThrow(() => view.Tick(0.1f, false, false, false));
        }

        StrayFireflyView Build(float width, out RectTransform root)
        {
            _parent = new GameObject("FireflyParent", typeof(RectTransform));
            var parent = (RectTransform)_parent.transform;
            parent.sizeDelta = new Vector2(width, 1920f);
            var rootGo = new GameObject("FireflyRoot", typeof(RectTransform));
            root = (RectTransform)rootGo.transform;
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(120f, 120f);
            rootGo.SetActive(false);
            var view = _parent.AddComponent<StrayFireflyView>();
            var field = typeof(StrayFireflyView).GetField("_root", Fields);
            Assert.IsNotNull(field, "StrayFireflyView._root");
            field.SetValue(view, root);
            return view;
        }
    }
}
