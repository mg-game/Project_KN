using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 패배 분석 (CR-01, SPEC4 §7.2 — P4-3b): 각도로 방향 칸 정하기, 칸마다 가장 많이 친 적, 스테이지가 바뀌면 비우기, % 반올림,
    /// 자료 없음 → 안 보임, 튜토리얼 중 → 안 보임, 같은 스테이지 10분 안 두 번째 실패 → 안 보임, 제목의 이/가
    /// </summary>
    public class DefeatAnalyzerTests
    {
        static Vector2 At(int slot) => BattleGeometry.FromAngle(BattleGeometry.SlotAngle(slot), BattleGeometry.ShopRadius);

        [Test]
        public void SlotMapping_ByAngle()
        {
            var a = new DefeatAnalyzer();
            a.AddShopStrike(Vector2.up, 10f, "enemy_moth");
            Assert.AreEqual(10f, a.SlotDamage(0), 1e-4f, "위쪽 = 0");
            a.AddShopStrike(At(1), 5f, "enemy_moth");
            Assert.AreEqual(5f, a.SlotDamage(1), 1e-4f, "왼쪽 위 = 1 (반시계)");
            a.AddShopStrike(At(4) + new Vector2(0.01f, 0f), 3f, null);
            Assert.AreEqual(3f, a.SlotDamage(4), 1e-4f);
            a.AddShopStrike(Vector2.zero, 99f, "x");
            Assert.AreEqual(18f, a.Total, 1e-4f, "가운데(0)는 방향을 모르니 무시");
            Assert.AreEqual("위쪽", DefeatAnalyzer.DirectionName(0));
            Assert.AreEqual("왼쪽 위", DefeatAnalyzer.DirectionName(1));
            Assert.AreEqual("왼쪽 아래", DefeatAnalyzer.DirectionName(2));
            Assert.AreEqual("오른쪽 아래", DefeatAnalyzer.DirectionName(3));
            Assert.AreEqual("오른쪽 위", DefeatAnalyzer.DirectionName(4));
        }

        [Test]
        public void Title_UsesSubjectParticleByFinalConsonant()
        {
            // '{방향}이' 고정이면 '왼쪽 위이'가 된다 — 받침 없는 끝(위 · 아래)은 '가'
            Assert.AreEqual("위쪽이 많이 뚫렸어요", DefeatAnalyzer.Title(0));
            Assert.AreEqual("왼쪽 위가 많이 뚫렸어요", DefeatAnalyzer.Title(1));
            Assert.AreEqual("왼쪽 아래가 많이 뚫렸어요", DefeatAnalyzer.Title(2));
            Assert.AreEqual("오른쪽 아래가 많이 뚫렸어요", DefeatAnalyzer.Title(3));
            Assert.AreEqual("오른쪽 위가 많이 뚫렸어요", DefeatAnalyzer.Title(4));
            Assert.AreEqual("어느 쪽이 많이 뚫렸어요", DefeatAnalyzer.Title(-1));
        }

        [Test]
        public void TopEnemy_PerSlot()
        {
            var a = new DefeatAnalyzer();
            a.AddSlotDamage(2, 4f, "enemy_moth");
            a.AddSlotDamage(2, 3f, "enemy_dust");
            a.AddSlotDamage(2, 3f, "enemy_dust");
            a.AddSlotDamage(3, 50f, "boss_shadow");
            Assert.AreEqual("enemy_dust", a.TopEnemy(2));
            Assert.AreEqual(3, a.TopSlot);
            Assert.AreEqual("boss_shadow", a.TopEnemy(3));
            Assert.IsNull(a.TopEnemy(0));
            StringAssert.Contains("불 밝히기", DefeatAnalyzer.Advice("boss_shadow"));
            StringAssert.Contains("감속", DefeatAnalyzer.Advice("enemy_moth"));
            Assert.IsNotEmpty(DefeatAnalyzer.Advice("unknown"));
        }

        [Test]
        public void Reset_OnStageChange()
        {
            var a = new DefeatAnalyzer();
            a.AddSlotDamage(1, 10f, "enemy_moth");
            Assert.IsTrue(a.HasData);
            a.Reset();
            Assert.IsFalse(a.HasData);
            Assert.AreEqual(-1, a.TopSlot);
            Assert.IsNull(a.TopEnemy(1));
        }

        [Test]
        public void Percent_Rounds()
        {
            var a = new DefeatAnalyzer();
            a.AddSlotDamage(0, 2f, null);
            a.AddSlotDamage(1, 1f, null);
            Assert.AreEqual(67, a.TopPercent, "2/3 = 66.7 → 67");
            Assert.AreEqual(50, DefeatAnalyzer.RoundPercent(0.5f));
            Assert.AreEqual(100, DefeatAnalyzer.RoundPercent(1.2f));
            Assert.AreEqual(0, DefeatAnalyzer.RoundPercent(-0.1f));
        }

        [Test]
        public void NoData_FtueAndRepeat_DoNotShow()
        {
            var a = new DefeatAnalyzer();
            Assert.IsFalse(a.ShouldShow(true, 4, 0f), "자료 없음");
            a.AddSlotDamage(0, 0.5f, null);
            Assert.IsFalse(a.ShouldShow(true, 4, 0f), "합 < 1");
            a.AddSlotDamage(0, 5f, null);
            Assert.IsFalse(a.ShouldShow(false, 4, 0f), "튜토리얼 중");
            Assert.IsTrue(a.ShouldShow(true, 4, 0f));

            a.MarkShown(4, 0f);
            Assert.IsFalse(a.ShouldShow(true, 4, 300f), "같은 스테이지 10분 안 두 번째 실패");
            Assert.IsTrue(a.ShouldShow(true, 5, 300f), "다른 스테이지는 보인다");
            Assert.IsTrue(a.ShouldShow(true, 4, 601f), "10분이 지나면 다시");
        }
    }
}
