using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// UX Wave 3 WP-P 검증 고침 (r1):
    ///   · 장면이 내려가는 중(게임이 이미 꺼짐) RewardFlyer가 떠 있는 양을 비워도 상단 바가 보유량을 읽지 않는다 (MetaUi 스모크 SetUp NullReference).
    ///   · "+N" 받침 자리: 상단 바 우편 · ≡ 메뉴는 버튼 자기 이름표를 다 덮고(이웃 버튼으로 넘어가지 않는다),
    ///     수선 바구니는 바구니 그림 옆 화면 가운데 쪽 (아래 끝 '가득!' · 시간 알약을 덮지 않는다).
    /// 좌표는 RewardFlyer 층 로컬 (위로 +). 받침 = 높이 40 · 최소 폭 72 (UX-SPEC §2.2).
    /// </summary>
    public class UiWave3_WPPFixTests
    {
        const float PillHeight = 40f;
        const float PillMinWidth = 72f;
        const float ScreenCentre = 540f;
        const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;

        // ───────────── 장면이 내려갈 때 상단 바 ─────────────

        [Test]
        public void TopBar_ShownTarget_KeepsShownWhenGameIsDown()
        {
            Assume.That(Game.IsReady, Is.False, "다른 시험이 게임을 켜 둔 채면 볼 수 없다");
            var method = typeof(TopBarView).GetMethod("ShownTarget", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "TopBarView.ShownTarget(CurrencyType, double)");
            Assert.AreEqual(1234d, (double)method.Invoke(null, new object[] { CurrencyType.Gold, 1234d }), "보유량 대신 지금 보이는 값");
            Assert.AreEqual(0d, (double)method.Invoke(null, new object[] { CurrencyType.Gem, 0d }));
        }

        [Test]
        public void TopBar_PendingAndHeldSignals_IgnoredWhenGameIsDown()
        {
            Assume.That(Game.IsReady, Is.False, "다른 시험이 게임을 켜 둔 채면 볼 수 없다");
            var go = new GameObject("TopBar", typeof(RectTransform));
            try
            {
                var view = go.AddComponent<TopBarView>();
                typeof(TopBarView).GetField("_started", Fields).SetValue(view, true);
                var pending = typeof(TopBarView).GetMethod("OnPendingChanged", Fields);
                var held = typeof(TopBarView).GetMethod("OnHeldChanged", Fields);
                Assert.DoesNotThrow(() => pending.Invoke(view, new object[] { CurrencyType.Gold }), "떠 있는 양 비움 (층 꺼짐)");
                Assert.DoesNotThrow(() => pending.Invoke(view, new object[] { CurrencyType.Gem }));
                Assert.DoesNotThrow(() => held.Invoke(view, null), "보상 팝업 붙잡음 풀림");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ───────────── 상단 바 우편 · ≡ 메뉴 받침 ─────────────

        /// <summary>≡ 메뉴 (96x96, 오른쪽 12) · 이름표 = 버튼 아래 끝 26 줄 · 우편은 그 왼쪽 8px 틈 너머</summary>
        static readonly Rect MenuButton = new(972f, 1790f, 96f, 96f);
        static readonly Rect MenuLabel = new(972f, 1790f, 96f, 26f);
        static readonly Rect MailButton = new(868f, 1790f, 96f, 96f);

        [Test]
        public void TopIconPill_CoversOwnLabel_StaysOffNeighbour()
        {
            var anchor = RewardFlyer.TopIconLabelAnchor(MenuButton, MenuLabel, true);
            Assert.AreEqual(MenuLabel.center.x, anchor.x, 0.01f, "이름표 가운데");
            Assert.AreEqual(MenuLabel.center.y, anchor.y, 0.01f);
            float centre = RewardFlyer.LabelCenterX(anchor.x, 0f, PillMinWidth);
            Assert.AreEqual(anchor.x, centre, "가운데 기준 (옆으로 자라지 않는다)");

            var pill = new Rect(centre - PillMinWidth * 0.5f, anchor.y - PillHeight * 0.5f, PillMinWidth, PillHeight);
            Assert.LessOrEqual(pill.yMin, MenuLabel.yMin, "이름표 아래 끝까지 덮는다 ('메뉴' 위에 반쯤 얹히지 않게)");
            Assert.GreaterOrEqual(pill.yMax, MenuLabel.yMax, "이름표 위 끝까지 덮는다");
            Assert.GreaterOrEqual(pill.width, 52f, "두 글자 이름(20 · 외곽선 ≈ 50)을 옆으로도 덮는다");
            Assert.Greater(pill.xMin, MailButton.xMax, "우편 버튼(봉투 · 숫자 배지)으로 넘어가지 않는다");
            Assert.LessOrEqual(pill.xMax, MenuButton.xMax, "버튼 안");
        }

        [Test]
        public void TopIconPill_NoLabel_UsesBottomRow()
        {
            var anchor = RewardFlyer.TopIconLabelAnchor(MenuButton, default, false);
            Assert.AreEqual(MenuButton.center.x, anchor.x, 0.01f);
            Assert.AreEqual(MenuButton.yMin + 13f, anchor.y, 0.01f, "버튼 아래 끝 26 줄 가운데");
        }

        // ───────────── 수선 바구니 받침 ─────────────

        /// <summary>바구니 위젯 150x170 (오른쪽 24) · 바구니 그림 136x128 위 가운데 · '가득!' 아래 15에서 34 높이 · 시간 알약 아래 16에서 32</summary>
        static readonly Rect Widget = new(906f, 1500f, 150f, 170f);
        static readonly Rect Basket = new(Widget.center.x - 68f, Widget.yMax - 128f, 136f, 128f);

        [Test]
        public void BasketPill_BesideBasket_ClearOfFullTag()
        {
            var anchor = RewardFlyer.BesidePlateAnchor(Basket, ScreenCentre, out float side);
            Assert.AreEqual(-1f, side, "오른쪽 위젯은 왼쪽(화면 가운데)으로 자란다");
            Assert.AreEqual(Basket.xMin - 10f, anchor.x, 0.01f, "바구니 끝에서 10 띄운 가까운 끝");
            Assert.AreEqual(Basket.center.y, anchor.y, 0.01f, "바구니 가운데 높이");

            foreach (float width in new[] { PillMinWidth, 120f })
            {
                float centre = RewardFlyer.LabelCenterX(anchor.x, side, width);
                Assert.LessOrEqual(centre + width * 0.5f, Basket.xMin - 10f + 0.01f, $"폭 {width}: 바구니를 덮지 않는다");
                Assert.LessOrEqual(centre + width * 0.5f, Widget.xMin, $"폭 {width}: 위젯 밖 ('가득!' · 시간 알약은 위젯 안)");
            }
            float pillBottom = anchor.y - PillHeight * 0.5f;
            Assert.Greater(pillBottom, Widget.yMin + 15f + 34f, "'가득!' 높이보다 위");
        }

        [Test]
        public void BesidePlate_LeftSide_GrowsRight()
        {
            var plate = new Rect(28f, 1000f, 100f, 100f);
            var anchor = RewardFlyer.BesidePlateAnchor(plate, ScreenCentre, out float side);
            Assert.AreEqual(1f, side, "왼쪽 판은 오른쪽으로");
            Assert.AreEqual(plate.xMax + 10f, anchor.x, 0.01f);
            Assert.GreaterOrEqual(RewardFlyer.LabelCenterX(anchor.x, side, PillMinWidth) - PillMinWidth * 0.5f, plate.xMax, "판을 덮지 않는다");
        }
    }
}
