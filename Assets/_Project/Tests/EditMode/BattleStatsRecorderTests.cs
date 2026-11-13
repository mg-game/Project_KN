using System.Collections.Generic;
using IdleGame.UI;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 전투 기록 (CR-03, SPEC4 §7.4 — P4-3b): 출처마다 더하기, 지난 60초 창이 지나면 빠짐, 같은 형태 잔향이는 하나로, 스테이지 합 비우기,
    /// 16명이 넘치면 가장 작은 친구를 뺀다 (스테이지가 바뀐 뒤에도 지금 싸우는 친구 대신 오래된 형태), 피격 주인 찾기(지속 피해 → 붙인 친구, 맥락은 같은 진행만),
    /// 회복 · 보호막 작은 막대는 칸의 가장 큰 친구 기준
    /// </summary>
    public class BattleStatsRecorderTests
    {
        readonly List<BattleStatsRecorder.UnitTotals> _rows = new();

        [Test]
        public void Attribution_PerKind()
        {
            var r = new BattleStatsRecorder();
            r.AddDamage("needle_base", false, 100f, 1f);
            r.AddHeal("candle_base", false, 30f, 1f);
            r.AddShield("button_base", false, 20f, 1.5f);
            r.AddKill("needle_base", false, 2f);
            r.AddDamage("rain_base", true, 40f, 2f);

            Assert.AreEqual(4, r.Fill(_rows, false, 2f));
            Assert.AreEqual("needle_base", _rows[0].FormId, "피해 많은 순");
            Assert.AreEqual(100d, _rows[0].Damage, 1e-6);
            Assert.AreEqual(1, _rows[0].Kills);
            Assert.AreEqual("rain_base", _rows[1].FormId);
            Assert.IsTrue(_rows[1].IsEcho);
            var candle = _rows.Find(x => x.FormId == "candle_base");
            Assert.AreEqual(30d, candle.Heal, 1e-6);
            var button = _rows.Find(x => x.FormId == "button_base");
            Assert.AreEqual(20d, button.Shield, 1e-6);
        }

        [Test]
        public void Window_ExpiresAfter60Seconds()
        {
            var r = new BattleStatsRecorder();
            r.AddDamage("a", false, 10f, 5f);
            r.AddDamage("a", false, 7f, 50f);
            r.Fill(_rows, true, 60f);
            Assert.AreEqual(17d, _rows[0].Damage, 1e-6, "60초 안");
            r.Fill(_rows, true, 66f);
            Assert.AreEqual(7d, _rows[0].Damage, 1e-6, "5초에 넣은 것은 66초에 빠진다");
            r.Fill(_rows, true, 200f);
            Assert.AreEqual(0, _rows.Count, "창 밖이면 없음");
            r.Fill(_rows, false, 200f);
            Assert.AreEqual(17d, _rows[0].Damage, 1e-6, "스테이지 합은 그대로");
        }

        [Test]
        public void Echoes_MergeByForm()
        {
            var r = new BattleStatsRecorder();
            r.AddDamage("sock_base", true, 5f, 1f);
            r.AddDamage("sock_base", true, 6f, 1.2f);
            r.AddKill("sock_base", true, 1.3f);
            Assert.AreEqual(1, r.Fill(_rows, false, 2f));
            Assert.AreEqual(11d, _rows[0].Damage, 1e-6);
            Assert.AreEqual(1, _rows[0].Kills);
            Assert.AreEqual(1, r.UnitCount);
        }

        [Test]
        public void StageReset_ClearsStageTotals()
        {
            var r = new BattleStatsRecorder();
            r.AddDamage("a", false, 10f, 1f);
            r.ResetStage();
            Assert.AreEqual(0, r.Fill(_rows, false, 2f), "이번 스테이지 합은 비었다");
            Assert.AreEqual(1, r.Fill(_rows, true, 2f), "지난 60초는 이어진다");
        }

        [Test]
        public void Overflow_DropsTheSmallest()
        {
            var r = new BattleStatsRecorder();
            for (int i = 0; i < BattleStatsRecorder.MaxUnits; i++) r.AddDamage("f" + i, false, 10f + i, 1f);
            Assert.AreEqual(BattleStatsRecorder.MaxUnits, r.UnitCount);
            r.AddDamage("big", false, 100f, 1f);
            Assert.AreEqual(BattleStatsRecorder.MaxUnits, r.UnitCount);
            r.Fill(_rows, false, 1f, BattleStatsRecorder.MaxUnits);
            Assert.IsTrue(_rows.Exists(x => x.FormId == "big"));
            Assert.IsFalse(_rows.Exists(x => x.FormId == "f0"), "가장 작은 친구가 빠졌다");
            r.AddDamage("tiny", false, 1f, 1f);
            r.Fill(_rows, false, 1f, BattleStatsRecorder.MaxUnits);
            Assert.IsFalse(_rows.Exists(x => x.FormId == "tiny"), "가장 작은 친구보다 작으면 들어오지 않는다");
            Assert.AreEqual(11, r.Fill(_rows, false, 1f), "기본 11줄");
        }

        [Test]
        public void Overflow_AfterStageReset_DropsTheStaleFormNotAnActiveOne()
        {
            // 16명이 찬 뒤 스테이지가 바뀌면 스테이지 합이 모두 0 — 예전에는 새 형태가 0번 칸(지금 싸우는 친구)을 뺐다
            var r = new BattleStatsRecorder();
            for (int i = 0; i < BattleStatsRecorder.MaxUnits - 1; i++) r.AddDamage("f" + i, false, 10f, 50f);
            r.AddDamage("stale", false, 500f, 1f); // 편성에서 빠진 형태: 크지만 60초 넘게 기록 없음
            Assert.AreEqual(BattleStatsRecorder.MaxUnits, r.UnitCount);
            r.ResetStage();
            r.AddDamage("new", false, 5f, 70f);
            r.Fill(_rows, true, 70f, BattleStatsRecorder.MaxUnits);
            Assert.IsTrue(_rows.Exists(x => x.FormId == "f0" && x.Damage == 10d), "지금 싸우는 친구의 지난 60초는 남는다");
            Assert.IsTrue(r.Contains("f0"));
            Assert.IsTrue(r.Contains("new"));
            Assert.IsFalse(r.Contains("stale"), "60초 안에 기록이 없던 형태가 빠졌다");
            Assert.AreEqual(BattleStatsRecorder.MaxUnits, r.UnitCount);

            // 점수가 모두 0이면 가장 오래 기록이 없던 친구부터
            var q = new BattleStatsRecorder();
            for (int i = 0; i < BattleStatsRecorder.MaxUnits; i++) q.AddDamage("g" + i, false, 1f, 100f - i);
            q.ResetStage();
            q.AddDamage("x", false, 1f, 400f);
            Assert.IsTrue(q.Contains("x"));
            Assert.IsFalse(q.Contains("g15"), "가장 오래된 g15(85초)가 빠졌다");
            Assert.IsTrue(q.Contains("g0"), "g0(100초)는 남았다");
            q.AddDamage("y", false, 1f, 400f);
            Assert.IsTrue(q.Contains("x"), "방금 기록한 x는 점수가 있어 남는다");
            Assert.IsFalse(q.Contains("g14"), "다음으로 오래된 g14");
            Assert.IsTrue(q.Contains("y"));
        }

        [Test]
        public void HitCredit_BurnTicksGoToTheBurner_ContextOnlyWithinTheSameStep()
        {
            var c = new HitCredit();
            Assert.AreEqual(3, c.Resolve(3, 7, HitCreditContext.None, false, -1, -1, -1, out bool burned), "SourceId 칸 = 지킴이 기본 공격");
            Assert.IsFalse(burned);
            Assert.AreEqual(-1, c.Resolve(-1, 7, HitCreditContext.None, false, -1, -1, -1, out burned), "맥락 · 불 없음 → 아무에게도");
            c.NoteBurn(7, 1002);
            Assert.AreEqual(1002, c.Resolve(-1, 7, HitCreditContext.None, false, -1, -1, -1, out burned), "지속 피해 틱 → 불을 붙인 친구");
            Assert.IsTrue(burned);
            // 2배속: 앞 진행 끝의 맥락(기본 공격 · 같은 대상 잔향이 공격 · 시전)이 열린 채 다음 진행 첫 틱이 와도 가져가지 않는다
            Assert.AreEqual(1002, c.Resolve(-1, 7, HitCreditContext.Basic, false, -1, -1, -1, out burned));
            Assert.AreEqual(1002, c.Resolve(-1, 7, HitCreditContext.EchoBasic, false, 1005, 7, -1, out burned));
            Assert.AreEqual(1002, c.Resolve(-1, 7, HitCreditContext.Cast, false, -1, -1, 4, out burned));
            Assert.AreEqual(1002, c.Resolve(-1, 7, HitCreditContext.Basic, true, -1, -1, -1, out burned), "기본 공격은 SourceId 칸으로만");
            // 같은 진행이면 맥락대로
            Assert.AreEqual(1005, c.Resolve(-1, 7, HitCreditContext.EchoBasic, true, 1005, 7, -1, out burned));
            Assert.IsFalse(burned);
            Assert.AreEqual(1002, c.Resolve(-1, 7, HitCreditContext.EchoBasic, true, 1005, 8, -1, out burned), "잔향이 공격은 그 대상만");
            Assert.AreEqual(4, c.Resolve(-1, 7, HitCreditContext.Cast, true, -1, -1, 4, out burned));
            Assert.AreEqual(-1, c.Resolve(-1, 7, HitCreditContext.Lamp, true, -1, -1, -1, out burned), "불 밝히기는 누구 몫도 아니다");
            c.Forget(7);
            Assert.AreEqual(-1, c.Resolve(-1, 7, HitCreditContext.None, false, -1, -1, -1, out burned), "불이 꺼지면 잊는다");
        }

        [Test]
        public void HitCredit_BurnTable_LastBurnerWins_OldestOverwrittenWhenFull()
        {
            var c = new HitCredit();
            c.NoteBurn(5, 0);
            c.NoteBurn(5, 2);
            Assert.IsTrue(c.TryGetBurn(5, out int source));
            Assert.AreEqual(2, source, "마지막으로 붙인 친구");
            Assert.AreEqual(1, c.BurnCount);
            c.NoteBurn(-1, 3);
            c.NoteBurn(6, -1);
            Assert.AreEqual(1, c.BurnCount, "적 · 출처가 없으면 적지 않는다");
            for (int i = 0; i < HitCredit.BurnSlots; i++) c.NoteBurn(100 + i, 1);
            Assert.AreEqual(HitCredit.BurnSlots, c.BurnCount);
            Assert.IsFalse(c.TryGetBurn(5, out _), "꽉 차면 가장 오래된 칸부터 덮는다");
            Assert.IsTrue(c.TryGetBurn(100, out _));
            Assert.IsTrue(c.TryGetBurn(100 + HitCredit.BurnSlots - 1, out _));
            c.Clear();
            Assert.AreEqual(0, c.BurnCount);
        }

        [Test]
        public void MiniBars_ScaleToTheColumnTop()
        {
            var list = new List<BattleStatsRecorder.UnitTotals>
            {
                new() { FormId = "dps", Damage = 1000d, Heal = 3d },
                new() { FormId = "healer", Damage = 10d, Heal = 50000d },
                new() { FormId = "tank", Damage = 5d, Shield = 200d },
            };
            BattleStatsPopup.ColumnTops(list, list.Count, out double healTop, out double shieldTop);
            Assert.AreEqual(50000d, healTop, 1e-9);
            Assert.AreEqual(200d, shieldTop, 1e-9);
            Assert.AreEqual(1f, BattleStatsPopup.MiniBarFill(list[1].Heal, healTop), 1e-6f);
            Assert.Less(BattleStatsPopup.MiniBarFill(list[0].Heal, healTop), 0.001f, "회복 3인 딜러는 거의 빈 막대 (줄마다 재면 꽉 찼다)");
            Assert.AreEqual(1f, BattleStatsPopup.MiniBarFill(list[2].Shield, shieldTop), 1e-6f);
            Assert.AreEqual(0f, BattleStatsPopup.MiniBarFill(list[0].Shield, shieldTop), 1e-6f);
            Assert.AreEqual(0.5f, BattleStatsPopup.MiniBarFill(0.5d, 0.5d), 1e-6f, "가장 큰 값이 1보다 작으면 1로 나눈다");
            BattleStatsPopup.ColumnTops(list, 1, out healTop, out _);
            Assert.AreEqual(3d, healTop, 1e-9, "앞 count줄만");
            BattleStatsPopup.ColumnTops(null, 3, out healTop, out shieldTop);
            Assert.AreEqual(0d, healTop, 1e-9);
            Assert.AreEqual(0d, shieldTop, 1e-9);
        }
    }
}
