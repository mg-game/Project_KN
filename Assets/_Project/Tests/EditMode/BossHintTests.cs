using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>챕터 보스 안내 줄 (PRG-02, SPEC4 §13.1 — P4-3b): 처음 만남이거나 다시 도전일 때만, 튜토리얼 전투 단계에는 안 보임</summary>
    public class BossHintTests
    {
        [Test]
        public void BossHint_FirstEncounterAndRetryOnly()
        {
            Assert.IsTrue(BossHintRule.ShouldShow(true, false, 0, false), "처음 만남");
            Assert.IsFalse(BossHintRule.ShouldShow(true, true, 0, false), "본 적 있고 실패 없음");
            Assert.IsTrue(BossHintRule.ShouldShow(true, true, 1, false), "다시 도전 (연속 실패 1)");
            Assert.IsTrue(BossHintRule.ShouldShow(true, true, 3, false));
            Assert.IsFalse(BossHintRule.ShouldShow(false, false, 2, false), "안내 문구가 없는 보스");
            Assert.IsFalse(BossHintRule.ShouldShow(true, false, 1, true), "튜토리얼 단계가 전투를 가리키는 동안");
            Assert.AreEqual(MetaFlags.BossHintPrefix + "boss_shadow", BossHintRule.FlagKey("boss_shadow"));
            Assert.IsNull(BossHintRule.FlagKey(null));
            Assert.AreEqual(1f, BossHintRule.Delay, 1e-6f);
            Assert.AreEqual(2.4f, BossHintRule.Duration, 1e-6f);
        }

        [Test]
        public void FarmNudge_After10Minutes_OncePerTarget()
        {
            Assert.IsFalse(BattleHud.ShouldNudge(10, 599f, 0), "10분 전");
            Assert.IsTrue(BattleHud.ShouldNudge(10, 600f, 0), "같은 막힌 스테이지 10분");
            Assert.IsFalse(BattleHud.ShouldNudge(10, 900f, 10), "그 스테이지에는 한 번만");
            Assert.IsTrue(BattleHud.ShouldNudge(20, 600f, 10), "다른 막힌 스테이지는 다시");
            Assert.IsFalse(BattleHud.ShouldNudge(0, 9999f, 0), "막힌 스테이지가 없으면 없음");
            Assert.AreEqual(600f, BattleHud.FarmNudgeAfter, 1e-6f);
            Assert.AreEqual(6f, BattleHud.FarmNudgeTime, 1e-6f);
        }

        [Test]
        public void DangerArrow_AvoidsGuideBar()
        {
            // 안내 줄 (로컬): x −524 ~ −56, y −64 ~ 20 — 왼쪽 위 레인 화살표가 그 위에 걸쳤다
            var bar = Rect.MinMaxRect(-524f, -64f, -56f, 20f);
            var half = new Vector2(40f, 40f);
            var moved = DangerArrowView.AvoidGuide(new Vector2(-490f, 0f), half, bar, DangerArrowView.GuideGap, 600f);
            Assert.GreaterOrEqual(moved.y - half.y, bar.yMax + DangerArrowView.GuideGap - 1e-3f, "줄 위 + 8px 위로");
            Assert.AreEqual(-490f, moved.x, 1e-4f, "가장자리를 따라 (가로 그대로)");
            var far = new Vector2(400f, 0f);
            Assert.AreEqual(far, DangerArrowView.AvoidGuide(far, half, bar, DangerArrowView.GuideGap, 600f), "겹치지 않으면 그대로");
            var capped = DangerArrowView.AvoidGuide(new Vector2(-490f, 0f), half, bar, DangerArrowView.GuideGap, 50f);
            Assert.AreEqual(50f, capped.y, 1e-4f, "위쪽 한계는 넘지 않는다");
        }

        [Test]
        public void ShopCracks_ThresholdsWithHysteresis()
        {
            Assert.IsFalse(ShopView.CrackState(false, 0.67f, ShopView.CrackAt[0]));
            Assert.IsTrue(ShopView.CrackState(false, 0.66f, ShopView.CrackAt[0]), "66% 이하에서 켠다");
            Assert.IsTrue(ShopView.CrackState(true, 0.70f, ShopView.CrackAt[0]), "5% 넘게 회복하기 전에는 남는다");
            Assert.IsFalse(ShopView.CrackState(true, 0.72f, ShopView.CrackAt[0]), "71% 넘게 회복하면 사라진다");
            Assert.IsTrue(ShopView.CrackState(false, 0.10f, ShopView.CrackAt[2]));
            CollectionAssert.AreEqual(new[] { 0.66f, 0.33f, 0.10f }, ShopView.CrackAt);
        }

        [Test]
        public void CheerChip_ClockFormat()
        {
            var buffer = new char[8];
            Assert.AreEqual("9:59", new string(buffer, 0, CheerChipView.FormatClock(599, buffer)));
            Assert.AreEqual("10:00", new string(buffer, 0, CheerChipView.FormatClock(600, buffer)));
            Assert.AreEqual("0:05", new string(buffer, 0, CheerChipView.FormatClock(5, buffer)));
            Assert.AreEqual("0:00", new string(buffer, 0, CheerChipView.FormatClock(-3, buffer)));
        }

        [Test]
        public void CheerChip_ClearsRailCellR1()
        {
            // V2 R6 · SPEC4 §11.4 비평 정적 검사: 칩(오른쪽 위 모서리 기준)은 R1 칸(StatsButton, 오른쪽에서 −116 = 화면 964)과 8px 넘게 떨어진다
            float chipRight = CheerChipView.AnchorX;
            Assert.LessOrEqual(chipRight, CheerChipView.RailCellLeftFromRight - CheerChipView.RailClearance, "칩 오른쪽 끝 ≤ R1 − 8");
            Assert.AreEqual(1080f - 116f, 1080f + CheerChipView.RailCellLeftFromRight, 1e-3f, "IA §3.3 R1 = 964–1068");
            // 바구니(IdleChestWidget 906–1056 × 250–420) 아래 — 칩 위 끝은 StageArea 위에서 310 (화면 440)
            Assert.AreEqual(-310f, CheerChipView.AnchorY, 1e-3f);
            Assert.AreEqual(112f, CheerChipView.Width, 1e-3f);
        }

        [Test]
        public void CheerChip_IsHudKeepOut()
        {
            // Wave 3 비키기 약속: 새 HUD 칸은 적 이름표 · 말풍선 · 피해 숫자가 비킨다 (꺼져 있으면 RefreshHudKeepOuts가 건너뛴다)
            CollectionAssert.Contains(BattleFx.HudKeepOutPaths, "CheerChip");
            CollectionAssert.Contains(BattleFx.HudKeepOutPaths, "MetaHud/IdleChestWidget", "기존 칸은 그대로");
        }

        [Test]
        public void PetBark_OncePer8s_ElseSqueak()
        {
            // AUDIO-SPEC §2.10: vox_{계보}_pet은 8초에 한 번까지, 아니면 pet_squeak (목소리 설정이 꺼졌거나 클립이 없어도 pet_squeak)
            Assert.AreEqual(8f, GuardianPetView.PetBarkInterval, 1e-4f);
            Assert.IsTrue(GuardianPetView.ShouldBark(true, true, 100f, -1000f), "처음");
            Assert.IsFalse(GuardianPetView.ShouldBark(true, true, 107.9f, 100f), "8초 안 → 삑");
            Assert.IsTrue(GuardianPetView.ShouldBark(true, true, 108f, 100f), "8초 뒤");
            Assert.IsFalse(GuardianPetView.ShouldBark(false, true, 200f, 100f), "목소리 설정 꺼짐");
            Assert.IsFalse(GuardianPetView.ShouldBark(true, false, 200f, 100f), "클립 없음");
            Assert.IsTrue(GuardianPetView.ShouldBark(true, true, 5f, 100f), "시계가 처음으로 돌아가면(새 재생) 다시 허용");
        }
    }
}
