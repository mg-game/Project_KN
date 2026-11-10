using System.Collections.Generic;
using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 잠자기 신호 (IDLE-06, SPEC4 §3.6 — P4-3b): AppSignals.RaiseSleep 짝(들어감 · 깸)이 그대로 전해지고,
    /// BattleFx.SetSleepMode가 입자 예산을 min(예산, 60)으로 줄였다가 깨면 되돌린다 (같은 값이 두 번 와도 그대로).
    /// RM-3 FIX-P4-3b (RI-6b · aspect R4): 잠들 때 떠 있는 숫자는 0.2초(실제 시간)에 사라지고 예약된 합계 알림은 버린다 · 잠든 동안 열 전체 번쩍임 없음 · 할당 없음
    /// </summary>
    public class SleepSignalTests
    {
        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            AppSignals.ResetState();
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void RaiseAndUnraise_ArriveInPairs()
        {
            var got = new List<bool>();
            System.Action<bool> handler = got.Add;
            AppSignals.SleepChanged += handler;
            try
            {
                AppSignals.RaiseSleep(true);
                Assert.IsTrue(AppSignals.IsAsleep);
                AppSignals.RaiseSleep(false);
                Assert.IsFalse(AppSignals.IsAsleep);
            }
            finally
            {
                AppSignals.SleepChanged -= handler;
            }
            CollectionAssert.AreEqual(new[] { true, false }, got);
        }

        [Test]
        public void SleepBudget_IsMin60_AndWakeRestores()
        {
            Assert.AreEqual(60, BattleFx.SleepBudget(420, true));
            Assert.AreEqual(60, BattleFx.SleepBudget(130, true));
            Assert.AreEqual(60, BattleFx.SleepBudget(60, true));
            Assert.AreEqual(420, BattleFx.SleepBudget(420, false));
            Assert.AreEqual(60, BattleFx.SleepParticleBudget);
        }

        [Test]
        public void SetSleepMode_LowersBudget_WakeRestores_RepeatsAreHarmless()
        {
            _go = new GameObject("FxSleepTest");
            var fx = _go.AddComponent<BattleFx>();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, "[Verify] sleep_budget=60 numbers=off");
            fx.SetSleepMode(true);
            fx.SetSleepMode(false);
            int awake = fx.ParticleBudget;
            Assert.Greater(awake, BattleFx.SleepParticleBudget, "깬 예산은 품질 예산 (60보다 크다)");

            fx.SetSleepMode(true);
            Assert.IsTrue(fx.SleepMode);
            Assert.LessOrEqual(fx.ParticleBudget, BattleFx.SleepParticleBudget);
            fx.SetSleepMode(true);
            Assert.LessOrEqual(fx.ParticleBudget, BattleFx.SleepParticleBudget, "같은 값이 두 번 와도 그대로");

            fx.SetSleepMode(false);
            Assert.IsFalse(fx.SleepMode);
            Assert.AreEqual(awake, fx.ParticleBudget, "깨면 원래 예산");
        }

        // ───────────── RI-6b: 잠들 때 숫자 0.2초 사라짐 · 합계 알림 버림 / aspect R4: 잠든 동안 열 전체 번쩍임 없음 ─────────────

        [Test]
        public void SleepNumberAlpha_FadesOverPoint2Seconds()
        {
            Assert.AreEqual(0.2f, BattleFx.SleepNumberFade, 1e-6f);
            Assert.AreEqual(1f, BattleFx.SleepNumberAlpha(-1f), 1e-6f, "사라지는 중이 아니면 그대로");
            Assert.AreEqual(1f, BattleFx.SleepNumberAlpha(0f), 1e-6f, "잠든 순간은 그대로");
            Assert.AreEqual(0.5f, BattleFx.SleepNumberAlpha(0.1f), 1e-4f, "반쯤");
            Assert.AreEqual(0f, BattleFx.SleepNumberAlpha(0.2f), 1e-6f, "0.2초에 다 사라진다");
            Assert.AreEqual(0f, BattleFx.SleepNumberAlpha(1f), 1e-6f);
            float last = 1f;
            for (int i = 1; i <= 20; i++)
            {
                float a = BattleFx.SleepNumberAlpha(i * 0.01f);
                Assert.LessOrEqual(a, last, $"줄어들기만 한다 ({i * 0.01f:0.00}초)");
                last = a;
            }
        }

        [Test]
        public void SetSleepMode_FadesNumbers_DropsLampSum_WakeEndsFade()
        {
            _go = new GameObject("FxSleepNumbers");
            var fx = _go.AddComponent<BattleFx>();
            var pending = typeof(BattleFx).GetField("_lampSumPending", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(pending, "BattleFx._lampSumPending");
            pending.SetValue(fx, true);
            Assert.IsTrue(fx.LampSumPending);
            Assert.IsFalse(fx.SleepNumbersFading, "깨어 있으면 사라짐 없음");
            Assert.AreEqual(1f, fx.SleepNumberAlphaNow);

            fx.SetSleepMode(true);
            Assert.IsFalse(fx.LampSumPending, "잠들면 예약된 불 밝히기 합계 알림을 버린다");
            Assert.IsTrue(fx.SleepNumbersFading);
            Assert.AreEqual(1f, fx.SleepNumberAlphaNow, 1e-6f);
            fx.TickSleepNumbers(0.1f);
            Assert.AreEqual(0.5f, fx.SleepNumberAlphaNow, 1e-4f, "0.1초에 반");
            Assert.IsTrue(fx.SleepNumbersFading);
            fx.TickSleepNumbers(0.1f);
            Assert.IsFalse(fx.SleepNumbersFading, "0.2초에 끝 (남은 숫자는 숨긴다)");
            Assert.AreEqual(1f, fx.SleepNumberAlphaNow, 1e-6f, "끝나면 배율은 1로 (새 숫자는 잠든 동안 뜨지 않는다)");
            fx.TickSleepNumbers(0.1f);
            Assert.IsFalse(fx.SleepNumbersFading);

            fx.SetSleepMode(false);
            fx.SetSleepMode(true);
            fx.TickSleepNumbers(0.05f);
            Assert.Less(fx.SleepNumberAlphaNow, 1f);
            fx.SetSleepMode(false);
            Assert.IsFalse(fx.SleepNumbersFading, "사라지는 중에 깨면 사라짐을 끝낸다");
            Assert.AreEqual(1f, fx.SleepNumberAlphaNow, 1e-6f, "깨면 배율 1");
        }

        [Test]
        public void Asleep_NoScreenOrFieldFlash()
        {
            _go = new GameObject("FxSleepFlash");
            var fx = _go.AddComponent<BattleFx>();
            fx.SetSleepMode(true);
            // 잠든 동안은 준비(Prepare)도 하지 않고 바로 돌아간다 — 빌더가 채운 칸이 없는 이 BattleFx를 준비하면 예외가 난다
            Assert.DoesNotThrow(() => fx.ScreenFlash(Color.red, 0.3f, 0.6f), "앱 전체 번쩍임 (스테이지 실패 · 보스 등장)");
            Assert.DoesNotThrow(() => fx.FieldFlash(Color.red, 0.3f, 0.6f), "전장 틴트 번쩍임");
            Assert.IsFalse(fx.ScreenFlashActive, "잠든 동안 앱 전체 번쩍임 없음 (aspect R4)");
            Assert.IsFalse(fx.FieldFlashActive, "잠든 동안 전장 번쩍임 없음");
        }

        [Test]
        public void SleepFade_AndFlashGate_DoNotAllocate()
        {
            _go = new GameObject("FxSleepAlloc");
            var fx = _go.AddComponent<BattleFx>();
            fx.SetSleepMode(true);
            fx.TickSleepNumbers(0.001f);
            fx.ScreenFlash(Color.red, 0.3f, 0.6f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 40; i++)
            {
                fx.TickSleepNumbers(0.01f);
                fx.ScreenFlash(Color.red, 0.3f, 0.6f);
                fx.FieldFlash(Color.red, 0.3f, 0.6f);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "잠자기 숫자 사라짐 · 번쩍임 막기는 할당 없음");
        }
    }
}
