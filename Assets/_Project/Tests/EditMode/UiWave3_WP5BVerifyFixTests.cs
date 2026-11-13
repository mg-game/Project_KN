using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// UX Wave 3 WP-5B 확인 실행(r1) 고침: 말풍선이 수선점 이름표('수선점' 알약)를 덮지 않는다 (BattleKeepOut.BubblePlateClear — 위 · 옆으로만,
    /// 띄움 한도 112px 안, 못 비키면 막힘), SHEET에서는 말풍선을 한꺼번에 2개까지 (SkillCalloutView.AliveCap).
    /// 좌표는 전장 카메라 로컬 (가운데 원점, 위로 +). SHEET 기준 화면 y → 로컬 ly = 502 − sy, lx = sx − 540.
    /// 화면 없이 순수 규칙과 상수만 확인한다.
    /// </summary>
    public class UiWave3_WP5BVerifyFixTests
    {
        // SHEET의 수선점 이름표 112x34 (화면 x 484~596 · y 568~602) / 말풍선 틈 6 / 전장 반폭 540 − 말풍선 반폭 − 8
        static readonly Rect ShopPlate = Rect.MinMaxRect(-56f, -100f, 56f, -66f);
        const float Gap = 6f;
        const float YMax = 400f;

        static float Limit(Vector2 half) => 540f - half.x - 8f;

        [Test]
        public void ShopPlate_FrontGuardianBubble_LiftsJustAbovePlate()
        {
            // 왼쪽 앞줄 친구 (머리 lx −120 · ly −110) 위 300폭 말풍선: 발밑 −100, 상자 −100 ~ −40 — 이름표를 덮는다 (26_tab_Gacha)
            var half = new Vector2(150f, SkillCalloutView.BubbleHeight * 0.5f);
            var c = new Vector2(-120f, -70f);
            Assert.IsTrue(BattleKeepOut.Overlaps(c, half, ShopPlate, 0f), "고치기 전에는 덮는다");
            var moved = BattleKeepOut.BubblePlateClear(c, half, ShopPlate, Gap, SkillCalloutView.ShopPlateMove, Limit(half), YMax, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(-120f, moved.x, 1e-4f, "옆(92px)보다 위(40px)가 가깝다 — 꼬리는 그대로 친구 위");
            Assert.AreEqual(ShopPlate.yMax + Gap, moved.y - half.y, 1e-3f, "말풍선 아래 끝 = 이름표 위 끝 + 6");
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, half, ShopPlate, 0f));
        }

        [Test]
        public void ShopPlate_NarrowBubbleAtEdge_StepsAside()
        {
            // 이름표 오른쪽 끝에 걸친 120폭 말풍선: 옆 22px < 위 40px
            var half = new Vector2(60f, SkillCalloutView.BubbleHeight * 0.5f);
            var c = new Vector2(100f, -70f);
            var moved = BattleKeepOut.BubblePlateClear(c, half, ShopPlate, Gap, SkillCalloutView.ShopPlateMove, Limit(half), YMax, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(ShopPlate.xMax + Gap + half.x, moved.x, 1e-3f);
            Assert.AreEqual(-70f, moved.y, 1e-4f);
        }

        [Test]
        public void ShopPlate_NeverPushesDown()
        {
            // 이름표 밑에서 위 끝이 걸친 말풍선: 아래로 5px이면 비키지만 아래는 허락하지 않는다 (꼬리가 말하는 친구 얼굴을 덮는다) — 위로
            var half = new Vector2(150f, SkillCalloutView.BubbleHeight * 0.5f);
            var c = new Vector2(0f, -125f);
            var moved = BattleKeepOut.BubblePlateClear(c, half, ShopPlate, Gap, SkillCalloutView.ShopPlateMove, Limit(half), YMax, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.Greater(moved.y, c.y, "위로만");
            Assert.LessOrEqual(moved.y - c.y, SkillCalloutView.ShopPlateMove + 1e-3f);
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, half, ShopPlate, 0f));
        }

        [Test]
        public void ShopPlate_NoRoom_BlockedAndUnmoved()
        {
            // 천장이 낮아 위로 못 가고 옆(212px)은 한도 112px을 넘는다 → 그대로 두고 막힘 (새 말풍선은 띄우지 않는다)
            var half = new Vector2(150f, SkillCalloutView.BubbleHeight * 0.5f);
            var c = new Vector2(0f, -125f);
            var moved = BattleKeepOut.BubblePlateClear(c, half, ShopPlate, Gap, SkillCalloutView.ShopPlateMove, Limit(half), -40f, out bool blocked);
            Assert.IsTrue(blocked);
            Assert.AreEqual(c, moved);
        }

        [Test]
        public void ShopPlate_GrazingGapOnly_NotBlocked()
        {
            // 틈(6px) 안에만 걸치고 이름표는 덮지 않는다: 옮길 곳이 없어도 막힘이 아니다
            var half = new Vector2(150f, SkillCalloutView.BubbleHeight * 0.5f);
            var c = new Vector2(0f, -133f);
            Assert.IsTrue(BattleKeepOut.Overlaps(c, half, ShopPlate, Gap));
            Assert.IsFalse(BattleKeepOut.Overlaps(c, half, ShopPlate, 0f));
            var moved = BattleKeepOut.BubblePlateClear(c, half, ShopPlate, Gap, SkillCalloutView.ShopPlateMove, Limit(half), -40f, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(c, moved);
        }

        [Test]
        public void ShopPlate_Clear_Untouched()
        {
            var half = new Vector2(150f, SkillCalloutView.BubbleHeight * 0.5f);
            var c = new Vector2(-300f, 100f);
            var moved = BattleKeepOut.BubblePlateClear(c, half, ShopPlate, Gap, SkillCalloutView.ShopPlateMove, Limit(half), YMax, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(c, moved);
        }

        [Test]
        public void AliveCap_SheetHoldsTwo()
        {
            Assert.AreEqual(2, SkillCalloutView.SheetMaxAlive);
            Assert.AreEqual(3, SkillCalloutView.AliveCap(3, false), "MAIN은 _maxAlive 그대로");
            Assert.AreEqual(2, SkillCalloutView.AliveCap(3, true), "SHEET는 2개까지");
            Assert.AreEqual(1, SkillCalloutView.AliveCap(1, true));
            Assert.AreEqual(1, SkillCalloutView.AliveCap(0, false), "최소 1");
            Assert.AreEqual(SkillCalloutView.LiftSteps * SkillCalloutView.LiftStep, SkillCalloutView.ShopPlateMove, 1e-6f);
        }
    }
}
