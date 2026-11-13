using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// UX Wave 3 WP-5B 리뷰 고침: 보스 이름표 비키기(BattleKeepOut.NumberPlateClear — 그 보스의 치명타는 옆으로 밀지 않는다),
    /// 치명타도 끊기지 않는 떠오름 바꾸기(BattleFx.RetargetRise — 버팀 곡선 그대로), 불 밝히기 합계 · HUD 옆 밀기 값.
    /// 좌표는 전장 카메라 로컬 (가운데 원점, 위로 +). 화면 없이 순수 규칙과 상수만 확인한다.
    /// </summary>
    public class UiWave3_WP5BFixTests
    {
        // 보스 (ActorScale 1, 크기 156): 이름표 + HP 바 묶음 = 발 위 152.9 ~ 212.9, 반폭 113 / 5글자 치명타 반폭 ≈ 107 · 반높이 42 (1.6 × 1.25)
        static readonly Rect BossPlate = Rect.MinMaxRect(-113f, 152.9f, 113f, 212.9f);
        static readonly Vector2 CritHalf = new(107f, 42f);
        const float Gap = 4f;
        const float MinRise = 16f;
        const float Limit = 540f - 107f - 8f;

        // ───────────── 보스 이름표 비키기 ─────────────

        [Test]
        public void PlateClear_OwnBossCrit_GoesAbovePlate_StaysOnBoss()
        {
            float rise = 90f;
            var spawn = new Vector2(0f, 140f);
            var moved = BattleKeepOut.NumberPlateClear(spawn, ref rise, 0f, CritHalf, BossPlate, true, Gap, MinRise, 600f, 640f, Limit);
            Assert.AreEqual(0f, moved.x, 1e-4f, "옆으로 밀지 않는다 (보스 몸 반폭 78 안)");
            Assert.GreaterOrEqual(moved.y - CritHalf.y, BossPlate.yMax + Gap - 1e-3f, "이름표 위");
            Assert.AreEqual(90f, rise, 1e-4f, "자리가 넉넉하면 다 떠오른다");
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, CritHalf, BossPlate, Gap - 0.01f));
        }

        [Test]
        public void PlateClear_OwnBossCrit_NoRoomAbove_SitsJustUnderPlate()
        {
            float rise = 90f;
            var spawn = new Vector2(0f, 140f);
            // 보스가 보이는 띠 위 끝 가까이 (이름표 위로 올리면 안전선을 넘는다)
            var moved = BattleKeepOut.NumberPlateClear(spawn, ref rise, 0f, CritHalf, BossPlate, true, Gap, MinRise, 230f, 260f, Limit);
            Assert.AreEqual(0f, moved.x, 1e-4f, "그래도 보스 위에 남는다");
            Assert.AreEqual(MinRise, rise, 1e-4f);
            Assert.LessOrEqual(moved.y + rise + CritHalf.y, BossPlate.yMin - Gap + 1e-3f, "떠오른 끝도 이름표 아래");
        }

        [Test]
        public void PlateClear_OtherTargetCrit_StepsAside()
        {
            float rise = 90f;
            var moved = BattleKeepOut.NumberPlateClear(new Vector2(20f, 140f), ref rise, 0f, CritHalf, BossPlate, false, Gap, MinRise, 600f, 640f, Limit);
            Assert.GreaterOrEqual(Mathf.Abs(moved.x) - CritHalf.x, BossPlate.xMax + Gap - 1e-3f, "다른 적의 숫자는 이름표 옆으로");
            Assert.AreEqual(140f, moved.y, 1e-4f);
        }

        [Test]
        public void PlateClear_BelowPlate_CapsRiseOnly()
        {
            var half = new Vector2(60f, 21f);
            float rise = 90f;
            var moved = BattleKeepOut.NumberPlateClear(new Vector2(0f, 60f), ref rise, 0f, half, BossPlate, true, Gap, MinRise, 600f, 640f, Limit);
            Assert.AreEqual(new Vector2(0f, 60f), moved);
            Assert.AreEqual(BossPlate.yMin - Gap - half.y - 60f, rise, 1e-3f, "떠오른 끝이 이름표 아래 끝 − 4px");
            // 겹치지 않으면 그대로
            rise = 30f;
            moved = BattleKeepOut.NumberPlateClear(new Vector2(400f, 60f), ref rise, 0f, half, BossPlate, false, Gap, MinRise, 600f, 640f, Limit);
            Assert.AreEqual(new Vector2(400f, 60f), moved);
            Assert.AreEqual(30f, rise, 1e-6f);
        }

        // ───────────── 떠오름 바꾸기 (치명타 포함) ─────────────

        static void AssertContinuous(bool crit, float age, float life, float rise, float endY)
        {
            float position = 0f;
            float r = rise;
            float before = position + BattleFx.RiseAt(crit, age, life, r);
            BattleFx.RetargetRise(crit, age, life, ref position, ref r, endY);
            float after = position + BattleFx.RiseAt(crit, age, life, r);
            string label = $"crit={crit} age={age} rise={rise} endY={endY}";
            Assert.AreEqual(before, after, 0.01f, label + ": 그린 높이가 끊기지 않는다");
            Assert.AreEqual(Mathf.Max(endY, before), position + r, 0.01f, label + ": 떠오른 끝 = 목표 (이미 넘었으면 그 자리)");
            float late = position + BattleFx.RiseAt(crit, life, life, r);
            Assert.LessOrEqual(late, Mathf.Max(endY, before) + 0.01f, label + ": 끝까지 목표를 넘지 않는다");
        }

        [Test]
        public void RetargetRise_Crit_KeepsHoldCurveContinuous()
        {
            const float life = 1.15f;
            foreach (float age in new[] { 0.05f, 0.14f, 0.27f, 0.3f, 0.5f, 0.8f })
            {
                foreach (float endY in new[] { 10f, 30f, 55f, 72f, 85f })
                    AssertContinuous(true, age, life, 90f, endY);
            }
        }

        [Test]
        public void RetargetRise_Normal_MatchesOutCubic()
        {
            const float life = 0.75f;
            foreach (float age in new[] { 0.05f, 0.2f, 0.4f })
            {
                foreach (float endY in new[] { 10f, 30f, 50f })
                    AssertContinuous(false, age, life, 64f, endY);
            }
        }

        [Test]
        public void RetargetRise_FreshAndAlreadyLow_Untouched()
        {
            // 막 뜬 숫자 (진행 0): 자리는 그대로, 떠오름은 목표까지 (최소 16px)
            float position = 0f;
            float rise = 90f;
            BattleFx.RetargetRise(true, 0f, 1.15f, ref position, ref rise, 40f);
            Assert.AreEqual(0f, position, 1e-6f);
            Assert.AreEqual(40f, rise, 1e-4f);
            BattleFx.RetargetRise(false, 0f, 0.75f, ref position, ref rise, 5f);
            Assert.AreEqual(MinRise, rise, 1e-4f);
            // 이미 목표 아래에서 끝나면 그대로
            rise = 30f;
            BattleFx.RetargetRise(true, 0.5f, 1.15f, ref position, ref rise, 100f);
            Assert.AreEqual(30f, rise, 1e-6f);
        }

        // ───────────── 값 ─────────────

        [Test]
        public void LampSumAndSidePush_Pins()
        {
            Assert.AreEqual(120f, BattleFx.SidePushMax, 1e-6f, "빠른 줄 칸 104px + 틈");
            Assert.AreEqual(0.12f, BattleFx.LampSumDelay, 1e-6f);
            Assert.AreEqual(0.9f, BattleFx.LampSumLife, 1e-6f, "합계 알림 0.9초 (fx2 leftovers)");
            Assert.AreEqual(1.2f, BattleFx.LampSumScale, 1e-6f);
            Assert.AreEqual(8, BattleFx.LampWallMin);
        }
    }
}
