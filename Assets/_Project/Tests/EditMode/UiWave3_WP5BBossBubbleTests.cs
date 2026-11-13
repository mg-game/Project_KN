using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// UX Wave 3 WP-5B 확인 실행(r2) 고침: 말풍선이 보스 머리와 몸을 덮지 않는다.
    /// SHEET에서 보스 이름표 묶음이 스테이지 카드 바로 밑(화면 9.6~229.6 × 284~344)에 있을 때, 옆 촛불 잔향이의 말풍선 '아직 심지가 남았는걸!'이
    /// 이름표에서 아래로(BattleKeepOut.Any) 밀려 '망각의 큰그림자'의 머리와 몸 위(화면 y 350~394)에 앉았다 (17_battle_boss_sheet · 41_reward_two_button).
    /// 이제 말풍선은 이름표 묶음 + 몸 자리(BattleFx.PlateBodyKeepOut = BattleKeepOut.Union(묶음, 몸, 6))를 BattleKeepOut.BubbleBossClear로 비킨다 —
    /// 위·옆 → 몸 아래 끝 밑, 112px 안 · HUD 칸 밖에서만, 못 비키면 막힘(새 대사는 띄우지 않는다).
    /// 좌표는 전장 카메라 로컬 (가운데 원점, 위로 +). SHEET 기준 화면 → 로컬: lx = sx − 540, ly = 502 − sy.
    /// 화면 없이 순수 규칙 · 상수와 EnemyView.BodyRect만 확인한다.
    /// </summary>
    public class UiWave3_WP5BBossBubbleTests
    {
        const float Gap = 6f;
        /// <summary>보스 이름표 묶음 (화면 9.6~229.6 × 284~344)</summary>
        static readonly Rect Plate = Rect.MinMaxRect(-530.4f, 158f, -310.4f, 218f);
        /// <summary>보스 몸 (발 lx −420.4 · ly 32, 배율 0.8 → 124.8 정사각, pivot y 0.06)</summary>
        static readonly Rect Body = new(-482.8f, 24.512f, 124.8f, 124.8f);
        /// <summary>스테이지 카드 (화면 14~344 × 142~276)</summary>
        static readonly Rect StagePanel = Rect.MinMaxRect(-526f, 226f, -196f, 360f);
        /// <summary>천장 302 (보이는 띠 위 372 − 70) / 바닥 −394 — 가운데 기준 한도는 반높이 30을 더한다</summary>
        const float YMax = 302f + 30f;
        const float YMin = -394f + 30f;

        static Rect Keep => BattleKeepOut.Union(Plate, Body, BattleFx.BodyKeepPad);
        static float Limit(Vector2 half) => 540f - half.x - 8f;
        static Vector2 Half(float width) => new(width * 0.5f, SkillCalloutView.BubbleHeight * 0.5f);

        [Test]
        public void Union_PadsBodyOnly()
        {
            var keep = Keep;
            Assert.AreEqual(Plate.xMin, keep.xMin, 1e-3f, "이름표가 몸보다 넓다");
            Assert.AreEqual(Plate.xMax, keep.xMax, 1e-3f);
            Assert.AreEqual(Body.yMin - BattleFx.BodyKeepPad, keep.yMin, 1e-3f, "아래 끝 = 몸 아래 끝 − 6");
            Assert.AreEqual(Plate.yMax, keep.yMax, 1e-3f, "위 끝 = 이름표 위 끝 (이름표는 넓히지 않는다)");
            var wide = BattleKeepOut.Union(new Rect(0f, 0f, 10f, 10f), new Rect(-20f, -5f, 100f, 30f), 6f);
            Assert.AreEqual(Rect.MinMaxRect(-26f, -11f, 86f, 31f), wide);
        }

        [Test]
        public void SheetBoss_CandleBubble_NoLongerLandsOnBody()
        {
            // 촛불 잔향이 머리 (lx −370 · ly 92) + 10 → 발밑 102, 288폭 말풍선 (12글자 × 21 + 36)
            var half = Half(288f);
            var c = new Vector2(-370f, 102f + half.y);
            // 예전: 이름표만 어느 쪽으로든 → 아래 10px (화면 y 350~410) — 보스 몸과 겹친다
            var old = BattleKeepOut.PushOut(c, half, Plate, Gap, BattleKeepOut.Any, Limit(half), YMin, YMax);
            Assert.IsTrue(BattleKeepOut.Overlaps(old, half, Body, 0f), "고치기 전에는 몸을 덮는다");
            // 이제: 위(122px — 게다가 스테이지 카드 밑) · 옆(209.6px) · 아래(149.5px) 모두 112px을 넘는다 → 그대로 두고 막힘 (띄우지 않는다)
            var moved = BattleKeepOut.BubbleBossClear(c, half, Keep, Gap, SkillCalloutView.BossMove, Limit(half), YMin, YMax,
                new[] { StagePanel }, 1, out bool blocked);
            Assert.IsTrue(blocked);
            Assert.AreEqual(c, moved);
        }

        [Test]
        public void BossClear_UpWhenClosest()
        {
            // 120폭 말풍선이 이름표 오른쪽 아래에 걸침: 위 64px < 옆 95.6px
            var half = Half(120f);
            var c = new Vector2(-340f, 190f);
            var moved = BattleKeepOut.BubbleBossClear(c, half, Keep, Gap, SkillCalloutView.BossMove, Limit(half), YMin, YMax, null, 0, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(-340f, moved.x, 1e-4f);
            Assert.AreEqual(Keep.yMax + Gap + half.y, moved.y, 1e-3f);
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, half, Keep, 0f));
        }

        [Test]
        public void BossClear_SkipsMoveIntoHud()
        {
            // 같은 말풍선인데 위는 스테이지 카드 밑 → 버리고 옆(95.6px)으로
            var half = Half(120f);
            var c = new Vector2(-340f, 190f);
            var moved = BattleKeepOut.BubbleBossClear(c, half, Keep, Gap, SkillCalloutView.BossMove, Limit(half), YMin, YMax,
                new[] { StagePanel }, 1, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(Keep.xMax + Gap + half.x, moved.x, 1e-3f);
            Assert.AreEqual(190f, moved.y, 1e-4f);
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, half, Keep, 0f));
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, half, StagePanel, 0f));
        }

        [Test]
        public void BossClear_DownOnlyWhenUpAndSideFail()
        {
            // 몸 아래 끝에 걸친 말풍선 (앞줄 친구): 위 234px · 옆 175.6px은 한도 밖 → 몸 아래 끝 − 6 밑으로 (37.5px)
            var half = Half(120f);
            var c = new Vector2(-420f, 20f);
            var moved = BattleKeepOut.BubbleBossClear(c, half, Keep, Gap, SkillCalloutView.BossMove, Limit(half), YMin, YMax, null, 0, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(-420f, moved.x, 1e-4f);
            Assert.AreEqual(Keep.yMin - Gap - half.y, moved.y, 1e-3f);
            Assert.Less(moved.y, c.y);
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, half, Keep, 0f));
        }

        [Test]
        public void BossClear_DownBelowFloor_Blocked()
        {
            // 바닥(시트 위 끝)이 높아 아래로도 못 간다 → 그대로 두고 막힘
            var half = Half(120f);
            var c = new Vector2(-420f, 20f);
            var moved = BattleKeepOut.BubbleBossClear(c, half, Keep, Gap, SkillCalloutView.BossMove, Limit(half), 0f, YMax, null, 0, out bool blocked);
            Assert.IsTrue(blocked);
            Assert.AreEqual(c, moved);
        }

        [Test]
        public void BossClear_Clear_Untouched()
        {
            var half = Half(288f);
            var c = new Vector2(100f, -200f);
            var moved = BattleKeepOut.BubbleBossClear(c, half, Keep, Gap, SkillCalloutView.BossMove, Limit(half), YMin, YMax,
                new[] { StagePanel }, 1, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(c, moved);
        }

        [Test]
        public void Constants()
        {
            Assert.AreEqual(SkillCalloutView.LiftSteps * SkillCalloutView.LiftStep, SkillCalloutView.BossMove, 1e-6f, "보스 자리 비키기 한도 = 띄움 한도 112px");
            Assert.AreEqual(SkillCalloutView.ShopPlateMove, SkillCalloutView.BossMove, 1e-6f);
            Assert.AreEqual(6f, BattleFx.BodyKeepPad, 1e-6f);
        }

        [Test]
        public void EnemyBodyRect_FeetPivotAndScale()
        {
            // 보스 크기 156 · 배율 0.8 · 발 (−420.4, 32) · 몸 pivot (0.5, 0.06) → Body 상자
            var root = new GameObject("BossBodyRectTest", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)root.transform;
                rt.anchoredPosition = new Vector2(-420.4f, 32f);
                rt.localScale = new Vector3(0.8f, 0.8f, 1f);
                var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
                body.SetParent(rt, false);
                body.pivot = new Vector2(0.5f, 0.06f);
                var view = root.AddComponent<EnemyView>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(EnemyView).GetField("_size", flags).SetValue(view, 156f);
                typeof(EnemyView).GetField("_body", flags).SetValue(view, body);
                view.BodyRect(out var rect);
                Assert.AreEqual(Body.xMin, rect.xMin, 1e-3f);
                Assert.AreEqual(Body.yMin, rect.yMin, 1e-3f);
                Assert.AreEqual(Body.width, rect.width, 1e-3f);
                Assert.AreEqual(Body.height, rect.height, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
