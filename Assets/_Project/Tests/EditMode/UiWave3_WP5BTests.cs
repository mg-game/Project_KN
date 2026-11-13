using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// UX Wave 3 WP-5B (전장 읽힘 + FX 2차 남은 일): 겹침 비키기 순수 규칙(BattleKeepOut — 이름표 · HP 바 묶음, 말풍선 띄움),
    /// M41 말풍선 값, 스테이지 클리어 등불 자리, 2차 비평 조율 값. 화면 없이 순수 규칙과 상수만 확인한다.
    /// 좌표는 전장 카메라 로컬 (가운데 원점, 위로 +). 16:9 기준 화면 y → 로컬: MAIN ly = 862 − sy, SHEET ly = 502 − sy, lx = sx − 540.
    /// </summary>
    public class UiWave3_WP5BTests
    {
        const float MainTop = 732f;
        const float SheetTop = 372f;
        const float SheetBottom = -412f;

        /// <summary>화면 사각형 (sx0 ~ sx1 × sy0 ~ sy1, 위가 작은 y) → 카메라 로컬 (보이는 띠 위 끝 = 화면 130)</summary>
        static Rect Local(float sx0, float sx1, float sy0, float sy1, float top) =>
            Rect.MinMaxRect(sx0 - 540f, top - (sy1 - 130f), sx1 - 540f, top - (sy0 - 130f));

        // ───────────── BattleKeepOut.PushOut ─────────────

        [Test]
        public void PushOut_NoOverlap_Unchanged()
        {
            var r = new Rect(0f, 0f, 100f, 100f);
            var c = new Vector2(300f, 300f);
            Assert.AreEqual(c, BattleKeepOut.PushOut(c, new Vector2(20f, 20f), r, 6f, BattleKeepOut.Any, 1000f, -1000f, 1000f));
            Assert.IsFalse(BattleKeepOut.Overlaps(c, new Vector2(20f, 20f), r, 6f));
        }

        [Test]
        public void PushOut_PicksSmallestAllowedMove()
        {
            var r = Rect.MinMaxRect(0f, 0f, 100f, 100f);
            var half = new Vector2(20f, 10f);
            // 아래 끝 가까이: 아래로 가장 조금
            var moved = BattleKeepOut.PushOut(new Vector2(50f, 5f), half, r, 6f, BattleKeepOut.Any, 1000f, -1000f, 1000f);
            Assert.AreEqual(new Vector2(50f, -16f), moved);
            // 오른쪽 끝 가까이: 옆으로
            moved = BattleKeepOut.PushOut(new Vector2(95f, 50f), half, r, 6f, BattleKeepOut.Any, 1000f, -1000f, 1000f);
            Assert.AreEqual(new Vector2(126f, 50f), moved);
            // 위 끝 가까이지만 위가 금지면 아래·옆 중에서
            moved = BattleKeepOut.PushOut(new Vector2(50f, 95f), half, r, 6f, BattleKeepOut.Down | BattleKeepOut.Side, 1000f, -1000f, 1000f);
            Assert.IsFalse(BattleKeepOut.Overlaps(moved, half, r, 5.9f));
            Assert.LessOrEqual(moved.y, 95f, "위로는 옮기지 않는다");
        }

        [Test]
        public void PushOut_RespectsLimits()
        {
            var r = Rect.MinMaxRect(0f, 0f, 100f, 100f);
            var half = new Vector2(20f, 10f);
            // 오른쪽은 xLimit 밖, 아래는 yMin 밖 → 왼쪽으로
            var moved = BattleKeepOut.PushOut(new Vector2(95f, 5f), half, r, 6f, BattleKeepOut.Down | BattleKeepOut.Side, 110f, 0f, 1000f);
            Assert.AreEqual(new Vector2(-26f, 5f), moved);
            // 갈 곳이 없으면 그대로 (부르는 쪽이 다시 잰다)
            moved = BattleKeepOut.PushOut(new Vector2(50f, 50f), half, r, 6f, BattleKeepOut.Down, 1000f, 0f, 1000f);
            Assert.AreEqual(new Vector2(50f, 50f), moved);
        }

        // ───────────── 적 이름표 · HP 바 묶음 (Wave 2 r2 battle) ─────────────

        [Test]
        public void StatusClear_SheetBossPlate_LeavesSpeedAutoAndBasket()
        {
            // SHEET: 배속 794~914 / AUTO 926~1066 × 144~220, 바구니 906~1056 × 250~420 (반투명 0.5로 남는다)
            var rects = new[]
            {
                Local(794f, 914f, 144f, 220f, SheetTop),
                Local(926f, 1066f, 144f, 220f, SheetTop),
                Local(906f, 1056f, 250f, 420f, SheetTop),
            };
            // 오른쪽 위 레인 보스 (발 화면 (900, 360)): 이름표 + HP 바 묶음 = 발 위 153 ~ 213, 반폭 110 → 화면 147 ~ 207 (배속/AUTO 밑)
            var feet = new Vector2(360f, 502f - 360f);
            var center = new Vector2(feet.x, feet.y + 183f);
            var half = new Vector2(110f, 30f);
            Assert.IsTrue(BattleKeepOut.Overlaps(center, half, rects[0], 0f), "고치기 전: 배속 버튼 밑");
            var clear = BattleKeepOut.StatusClear(center, half, rects, rects.Length, SheetTop, SheetBottom, 540f, BattleFx.StatusGap);
            foreach (var r in rects) Assert.IsFalse(BattleKeepOut.Overlaps(clear, half, r, 0f), r.ToString());
            Assert.LessOrEqual(clear.y + half.y, SheetTop - BattleFx.StatusGap + 0.01f, "보이는 띠 위 끝 − 8px 아래");
            // 화면으로: 배속/AUTO 아래 끝(220) 밑, 바구니(906) 왼쪽
            float screenTop = 502f - (clear.y + half.y);
            Assert.GreaterOrEqual(screenTop, 220f);
            Assert.LessOrEqual(clear.x + half.x + 540f, 906f);
            Assert.Less((clear - center).magnitude, 200f, "이름표가 보스에서 너무 멀리 가지 않는다");
        }

        [Test]
        public void StatusClear_ClampsUnderVisibleTop_WithNoHud()
        {
            // SHEET 가운데 열 (추천 알약은 사라짐): 위쪽 레인 보스가 막 나타나 이름표가 띠 위로 나가면 띠 위 끝 − 8px에 붙는다
            var center = new Vector2(0f, SheetTop + 40f);
            var half = new Vector2(110f, 30f);
            var clear = BattleKeepOut.StatusClear(center, half, null, 0, SheetTop, SheetBottom, 540f, BattleFx.StatusGap);
            Assert.AreEqual(SheetTop - BattleFx.StatusGap - half.y, clear.y, 1e-4f);
            Assert.AreEqual(0f, clear.x, 1e-4f);
            // 이미 아래면 그대로
            var low = new Vector2(100f, 0f);
            Assert.AreEqual(low, BattleKeepOut.StatusClear(low, half, null, 0, SheetTop, SheetBottom, 540f, BattleFx.StatusGap));
        }

        [Test]
        public void StatusClear_MainCenterColumn_GoesBelowPowerPill()
        {
            // MAIN 추천 알약 352~652 × 142~222
            var rects = new[] { Local(352f, 652f, 142f, 222f, MainTop) };
            var center = new Vector2(0f, 862f - 200f);
            var half = new Vector2(110f, 30f);
            var clear = BattleKeepOut.StatusClear(center, half, rects, 1, MainTop, -MainTop, 540f, BattleFx.StatusGap);
            Assert.IsFalse(BattleKeepOut.Overlaps(clear, half, rects[0], 0f));
            Assert.AreEqual(0f, clear.x, 1e-3f, "가운데 열에서는 아래로 (옆은 더 멀다)");
        }

        // ───────────── 말풍선 (M41) ─────────────

        [Test]
        public void M41_Values()
        {
            Assert.AreEqual(16, SkillCalloutView.DedupeSlots, "RM-2: 15초 같은 대사 창을 담도록 8 → 16칸");
            Assert.AreEqual(2.5f, SkillCalloutView.DedupeWindow, 1e-6f);
            Assert.AreEqual(60f, SkillCalloutView.BubbleHeight, 1e-6f);
            Assert.AreEqual(6f, SkillCalloutView.LiftGap, 1e-6f);
            Assert.AreEqual(2, SkillCalloutView.LiftSteps);
            Assert.AreEqual(56f, SkillCalloutView.LiftStep, 1e-6f);
            Assert.AreEqual(96f, SkillCalloutView.MinWidth, 1e-6f);
            Assert.AreEqual(400f, SkillCalloutView.MaxWidth, 1e-6f);
            Assert.AreEqual(18f, SkillCalloutView.FontMin, 1e-6f);
            Assert.AreEqual(22f, SkillCalloutView.FontMax, 1e-6f);
        }

        static float Lift(Vector2 foot, float width, Vector3[] others, int count, float ceiling, out bool blocked) =>
            BattleKeepOut.BubbleLift(foot, width, SkillCalloutView.BubbleHeight, others, count, SkillCalloutView.LiftGap, SkillCalloutView.LiftSteps,
                SkillCalloutView.LiftSteps * SkillCalloutView.LiftStep, ceiling, out blocked);

        [Test]
        public void BubbleLift_NoNeighbours_StaysPut()
        {
            var others = new Vector3[4];
            Assert.AreEqual(0f, Lift(Vector2.zero, 200f, others, 0, 1000f, out bool blocked));
            Assert.IsFalse(blocked);
            // 가로로 떨어진 이웃 (두 반폭 + 6px 밖)
            others[0] = new Vector3(206f, 0f, 200f);
            Assert.AreEqual(0f, Lift(Vector2.zero, 200f, others, 1, 1000f, out blocked));
            Assert.IsFalse(blocked);
        }

        [Test]
        public void BubbleLift_StacksAboveOlder()
        {
            var others = new Vector3[4];
            others[0] = new Vector3(20f, 0f, 200f);
            float lift = Lift(Vector2.zero, 160f, others, 1, 1000f, out bool blocked);
            Assert.IsFalse(blocked);
            Assert.AreEqual(66f, lift, 1e-4f, "앞선 말풍선 위 끝(60) + 6px");
            // 위로 올라간 자리에서 다시 겹치지 않는다
            Assert.GreaterOrEqual(Mathf.Abs(lift - others[0].y), SkillCalloutView.BubbleHeight + SkillCalloutView.LiftGap - 1e-4f);
        }

        [Test]
        public void BubbleLift_BlockedWhenTooHighOrPastCeiling()
        {
            var others = new Vector3[4];
            others[0] = new Vector3(0f, 0f, 200f);
            others[1] = new Vector3(0f, 66f, 200f);
            float lift = Lift(Vector2.zero, 200f, others, 2, 1000f, out bool blocked);
            Assert.IsTrue(blocked, "두 번 올리면 132px — 합 112px을 넘는다");
            Assert.AreEqual(SkillCalloutView.LiftSteps * SkillCalloutView.LiftStep, lift, 1e-4f);

            lift = Lift(Vector2.zero, 200f, others, 1, 30f, out blocked);
            Assert.IsTrue(blocked, "천장을 넘으면 막힌다");
            Assert.AreEqual(30f, lift, 1e-4f);
        }

        // ───────────── 스테이지 클리어 등불 (fx2 leftovers) ─────────────

        [Test]
        public void RewardCenter_LanternSitsBelowRibbon_InBothStates()
        {
            Assert.AreEqual(344f, EnemyFxCatalog.RewardRibbonBottom, 0.01f, "리본 아래 끝 = 293 + 51 (UX-SPEC-IA §8.3)");
            float main = EnemyFxCatalog.RewardCenterY(MainTop * 2f / 3f, MainTop, 1f);
            float sheet = EnemyFxCatalog.RewardCenterY(SheetTop * 2f / 3f, SheetTop, 1f);
            float mainScreen = 130f + (MainTop - main);
            float sheetScreen = 130f + (SheetTop - sheet);
            Assert.AreEqual(mainScreen, sheetScreen, 0.01f, "두 상태 같은 화면 자리");
            Assert.That(mainScreen, Is.InRange(660f, 690f), "화면 ≈ 676");
            // 등불 위쪽 반(0.36 × 560)이 리본(화면 372~474) 아래
            Assert.GreaterOrEqual(mainScreen - EnemyFxCatalog.RewardHeroInset * EnemyFxCatalog.RewardHeroSize, 130f + 344f - 0.01f);
            // 이미 더 낮은 자리는 그대로
            Assert.AreEqual(-300f, EnemyFxCatalog.RewardCenterY(-300f, MainTop, 1f), 1e-4f);
        }

        // ───────────── 2차 비평 조율 값 · 회귀 ─────────────

        [Test]
        public void Round2Tuning_Pins()
        {
            Assert.AreEqual(0.72f, HitFeel.BossKillTrauma, 1e-6f, "보스 처치 흔들림 (캡처 ≈ 8.7px)");
            Assert.AreEqual(0.72f, EnemyFxCatalog.ShopHitArtScale, 1e-6f, "수선점 피격 넓이 ≈ 2.0%");
            Assert.AreEqual(1.2f, AttackFxCatalog.RhythmNumberScale, 1e-6f, "리듬 강조 숫자 (E6)");
            Assert.AreEqual(8, BattleFx.LampWallMin);
            Assert.AreEqual(0.85f, BattleFx.LampWallScale, 1e-6f);
        }

        [Test]
        public void LalabellSoundRing_IsLavenderNotWhite()
        {
            var tint = BattleFx.SoundRingTint;
            Assert.AreNotEqual(Color.white, tint);
            float max = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
            float min = Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b));
            Assert.GreaterOrEqual(max - min, 0.05f, "흰 비눗방울이 아니라 라벤더빛");
            Assert.AreEqual(tint.b, max, 1e-6f, "라벤더 (파랑이 가장 크다)");
            Assert.AreEqual(0.4f, BattleFx.SoundRingAdd, 1e-6f);
            Assert.AreEqual(0.1f, BattleFx.SoundRingBoost, 1e-6f);
        }

        [Test]
        public void LineageRingTints_AreNotCream_ExceptLalabell()
        {
            // 흰 고리('비눗방울')가 돌아오지 않게: 라라벨(음파 고리는 BattleFx.SoundRingTint로 따로 물든다) 말고는 고리 색 채널 차 ≥ 0.12
            foreach (string id in AttackFxCatalog.KnownLineages)
            {
                if (id == "lalabell") continue;
                var tint = AttackFxCatalog.Signature(id).RingTint;
                float max = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
                float min = Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b));
                Assert.GreaterOrEqual(max - min, 0.12f, id);
            }
        }

        [Test]
        public void KillRings_AreFamilyTintedTowardCream()
        {
            foreach (EnemyDeathFamily family in System.Enum.GetValues(typeof(EnemyDeathFamily)))
            {
                var expected = Color.Lerp(EnemyFxCatalog.FamilyRingColor(family), JuiceColors.CoreCream, EnemyFxCatalog.KillRingCream);
                var actual = EnemyFxCatalog.KillRingColor(family);
                Assert.AreEqual(expected.r, actual.r, 1e-5f, family.ToString());
                Assert.AreEqual(expected.g, actual.g, 1e-5f, family.ToString());
                Assert.AreEqual(expected.b, actual.b, 1e-5f, family.ToString());
                Assert.AreNotEqual(JuiceColors.CoreCream, actual, family + ": 크림 고리가 아니다");
            }
            Assert.AreEqual(1.4f, EnemyFxCatalog.KillRingReach, 1e-6f);
            Assert.AreEqual(0.6f, EnemyFxCatalog.KillRingIntensity, 1e-6f);
        }

        [Test]
        public void BossBurstCenter_PullsInsideBandButMovesAtMostCap()
        {
            const float b = 420f;
            float inset = EnemyFxCatalog.BossBurstInset * b;
            float cap = EnemyFxCatalog.BossBurstMaxMove * b;
            // 위 가장자리 보스: 안쪽으로 당기되 cap까지만
            var point = new Vector2(0f, MainTop - 10f);
            var center = EnemyFxCatalog.BossBurstCenter(point, inset, 540f, -MainTop, MainTop, cap);
            Assert.LessOrEqual(center.y, point.y);
            Assert.LessOrEqual((center - point).magnitude, cap + 1e-3f);
            // 띠 한가운데 보스는 그대로
            var middle = new Vector2(0f, 0f);
            Assert.AreEqual(middle, EnemyFxCatalog.BossBurstCenter(middle, inset, 540f, -MainTop, MainTop, cap));
        }

        // ───────────── RM-2 P4-3b: Wave 3 r3 남은 일 (a 숫자 vs 말풍선 · b 대사 도배 · c 이름표 옆 여백) ─────────────

        [Test]
        public void HealNumber_UnderOwnBubble_RisesNoHigherThanBubbleBottomMinusGap()
        {
            // 말하는 친구 머리 위 말풍선 (발밑 y 300, 띄움 0, 폭 220) — 그 친구 몸에서 뜬 회복 숫자가 80px 떠오르려 한다
            var bubble = SkillCalloutView.BubbleRect(new Vector2(0f, 300f), 0f, 220f);
            Assert.AreEqual(300f, bubble.yMin, 1e-4f);
            Assert.AreEqual(300f + SkillCalloutView.BubbleHeight, bubble.yMax, 1e-4f);
            var half = new Vector2(30f, 14f);
            float rise = 80f;
            var start = new Vector2(10f, 230f);
            var placed = BattleKeepOut.NumberPlateClear(start, ref rise, 0f, half, bubble, false, BattleFx.BubbleClearance, 16f, MainTop, MainTop, 400f);
            Assert.AreEqual(start, placed, "말풍선 밑에서 뜬 숫자는 제자리");
            Assert.LessOrEqual(placed.y + rise + half.y, bubble.yMin - BattleFx.BubbleClearance + 1e-3f, "떠오른 끝이 말풍선 아래 끝 − 6px 아래");
            Assert.GreaterOrEqual(rise, 16f);

            // 말풍선 높이에서 뜬 숫자는 옆으로 (다른 적의 이름표처럼)
            float rise2 = 40f;
            var beside = BattleKeepOut.NumberPlateClear(new Vector2(0f, 320f), ref rise2, 0f, half, bubble, false, BattleFx.BubbleClearance, 16f, MainTop, MainTop, 400f);
            Assert.IsFalse(BattleKeepOut.Overlaps(new Vector2(beside.x, beside.y), half, bubble, 0f), "옆으로 비켰다");
        }

        [Test]
        public void SolidBubble_OnlyWhileOpaqueAndYoung()
        {
            Assert.IsTrue(SkillCalloutView.IsSolid(1f, 0.1f));
            Assert.IsTrue(SkillCalloutView.IsSolid(0.5f, 0.74f));
            Assert.IsFalse(SkillCalloutView.IsSolid(0.49f, 0.1f), "옅어진 말풍선은 숫자가 비키지 않는다");
            Assert.IsFalse(SkillCalloutView.IsSolid(1f, 0.75f), "사라지기 시작하면");
        }

        [Test]
        public void CalloutSpam_SameTextOncePer15s_SpeakerOncePer8s()
        {
            const string line = "아직 심지가 남았는걸!";
            var texts = new string[SkillCalloutView.DedupeSlots];
            var times = new float[SkillCalloutView.DedupeSlots];
            var speakers = new int[SkillCalloutView.DedupeSlots];
            for (int i = 0; i < times.Length; i++)
            {
                times[i] = -99f;
                speakers[i] = -1;
            }
            texts[0] = line;
            times[0] = 0f;
            speakers[0] = 1003;

            Assert.IsTrue(SkillCalloutView.RecentlySaid(texts, times, speakers, line, 1007, 10f, false), "같은 대사 t=10 → 버린다");
            Assert.IsFalse(SkillCalloutView.RecentlySaid(texts, times, speakers, line, 1007, 16f, false), "t=16 → 보여 준다");
            Assert.IsTrue(SkillCalloutView.RecentlySaid(texts, times, speakers, "다른 말", 1003, 7f, false), "같은 친구 8초 안");
            Assert.IsFalse(SkillCalloutView.RecentlySaid(texts, times, speakers, "다른 말", 1003, 8.5f, false));
            Assert.IsFalse(SkillCalloutView.RecentlySaid(texts, times, speakers, "다른 말", -1, 3f, false), "말하는 친구를 모르면 8초 규칙 없음");
            Assert.IsFalse(SkillCalloutView.RecentlySaid(texts, times, speakers, line, 1003, 10f, true), "큰 스킬 · 불 밝히기 대사는 건너뛴다");
            Assert.IsTrue(SkillCalloutView.RecentlySaid(texts, times, speakers, line, 1003, 2f, true), "2.5초 같은 대사 규칙은 그대로");
            Assert.AreEqual(15f, SkillCalloutView.SameTextWindow, 1e-6f);
            Assert.AreEqual(8f, SkillCalloutView.SpeakerWindow, 1e-6f);
        }

        [Test]
        public void StatusClear_KeepsTwelvePixelsFromEachSide()
        {
            // 화면 왼쪽 끝을 걷는 보스 (x = −halfWidth + 10): 이름표 묶음이 화면 끝에서 12px 이상 안쪽
            var half = new Vector2(110f, 30f);
            var left = BattleKeepOut.StatusClear(new Vector2(-540f + 10f, 0f), half, null, 0, MainTop, -MainTop, 540f, BattleFx.StatusGap);
            Assert.GreaterOrEqual(left.x - half.x + 540f, BattleKeepOut.StatusSideMargin - 1e-3f);
            var right = BattleKeepOut.StatusClear(new Vector2(540f - 4f, 0f), half, new Rect[0], 0, MainTop, -MainTop, 540f, BattleFx.StatusGap);
            Assert.LessOrEqual(right.x + half.x, 540f - BattleKeepOut.StatusSideMargin + 1e-3f);
            Assert.AreEqual(12f, BattleKeepOut.StatusSideMargin, 1e-6f);
        }
    }
}
