using System;
using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 전투 소리 시간 거울 (AUDIO-SPEC §5.3): BattleAudioTiming이 연출 쪽 공개 규칙(AttackFxCatalog · EnemyFxCatalog · LampWave · BattleFx)과
    /// 같은 값을 내고, 베껴 둔 비공개 숫자가 연출 코드의 값과 같다 (리플렉션 — 연출 쪽이 숫자를 바꾸면 여기서 실패한다: 소리가 화면보다 앞서거나 늦는다).
    /// </summary>
    public class AudioTimingTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        static float Const(Type type, string name)
        {
            var field = type.GetField(name, Private | BindingFlags.Public);
            Assert.IsNotNull(field, $"{type.Name}.{name}이(가) 없어요 — 연출 쪽 이름이 바뀌었으면 BattleAudioTiming도 맞춰 주세요");
            return Convert.ToSingle(field.GetValue(null));
        }

        [Test]
        public void GuardianFlight_EqualsAttackFxCatalog_OverAGrid()
        {
            string[] lineages = { "jjakjjak", "ttokdan", "chalkak", "mongdang", "udukeoni", "rouge", "lalabell", null };
            var styles = (CombatStyle[])Enum.GetValues(typeof(CombatStyle));
            float[] distances = { 0f, 80f, 240f, 420f, 900f };
            foreach (var lineage in lineages)
            foreach (var style in styles)
            foreach (float distance in distances)
            for (int speed = 1; speed <= 2; speed++)
            foreach (float scale in new[] { 0.75f, 1f, 1.3f })
            {
                float expected = AttackFxCatalog.FlightTime(AttackFxCatalog.Look(lineage), AttackFxCatalog.Style(style), distance, speed, scale);
                Assert.AreEqual(expected, BattleAudioTiming.GuardianFlight(lineage, style, distance, speed, scale), 1e-6f);
                Assert.AreEqual(AttackFxCatalog.Windup(expected, speed), BattleAudioTiming.GuardianWindup(expected, speed), 1e-6f);
                Assert.LessOrEqual(expected, AttackFxCatalog.MaxFlight + 1e-6f, "마지막 일격은 처치 꽃보다 먼저");
            }
        }

        [Test]
        public void KillPop_EqualsTheEnemyViewRule()
        {
            float[] blows = { 0f, 0.05f, 0.12f, 0.28f, 0.6f, 1.2f };
            foreach (float blow in blows)
            foreach (bool boss in new[] { false, true })
            {
                float hold = blow > 0f ? AttackFxCatalog.KillHold(blow) : 0f;
                float expected = EnemyFxCatalog.KillBlowHold(hold, blow, boss);
                Assert.AreEqual(expected, BattleAudioTiming.KillPop(hold, blow, boss), 1e-6f);
                Assert.GreaterOrEqual(expected, EnemyView.KillHold - 1e-6f, "처치 꽃은 최소 0.14초 버틴 뒤");
                Assert.LessOrEqual(expected, EnemyFxCatalog.KillHoldLimit + 1e-6f);
            }
            // 보통 기본 공격 처치: 비행 + 하얀 버팀 0.08 (보스 0.12)
            Assert.AreEqual(0.2f + EnemyFxCatalog.KillFreeze, BattleAudioTiming.KillPop(AttackFxCatalog.KillHold(0.2f), 0.2f, false), 1e-5f);
            Assert.AreEqual(0.2f + EnemyFxCatalog.BossKillFreeze, BattleAudioTiming.KillPop(AttackFxCatalog.KillHold(0.2f), 0.2f, true), 1e-5f);
            Assert.AreEqual(AttackFxCatalog.KillHoldMin, EnemyView.KillHold, 1e-6f);
        }

        [Test]
        public void LampArrival_EqualsLampWave()
        {
            for (float d = 0f; d <= 3.6f; d += 0.2f)
            {
                var world = new Vector2(d * 0.6f, d * 0.8f);
                Assert.AreEqual(LampWave.Start + LampWave.Delay(d), BattleAudioTiming.LampArrival(world), 1e-5f);
            }
            Assert.Less(BattleAudioTiming.LampArrival(new Vector2(0.5f, 0f)), BattleAudioTiming.LampArrival(new Vector2(2f, 0f)), "가까운 적부터 차례로");
        }

        [Test]
        public void EchoTimes_FollowTheSpeedRules()
        {
            Assert.AreEqual(0.05f, BattleAudioTiming.EchoMeleeHit(1), 1e-6f);
            Assert.AreEqual(0.05f * AttackFxCatalog.SpeedPopScale(2), BattleAudioTiming.EchoMeleeHit(2), 1e-6f);
            Assert.AreEqual(0.14f, BattleAudioTiming.EchoRangedHit(1), 1e-6f);
            Assert.AreEqual(0.14f * AttackFxCatalog.SpeedFlightScale(2), BattleAudioTiming.EchoRangedHit(2), 1e-6f);
        }

        [Test]
        public void CopiedLiterals_MatchTheFxSide()
        {
            Assert.AreEqual(BattleAudioTiming.EchoShotLife, Const(typeof(BattleFx), "EchoShotLife"), 1e-6f, "BattleFx.EchoShotLife");
            Assert.AreEqual(BattleAudioTiming.EchoSwing, Const(typeof(BattleFx), "EchoSwing"), 1e-6f, "BattleFx.EchoSwing");
            Assert.AreEqual(BattleAudioTiming.BurstStagger, Const(typeof(BattleFx), "BurstStagger"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.BurstStaggerMax, Const(typeof(BattleFx), "BurstStaggerMax"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.RainReachBase, Const(typeof(BattleFx), "RainReachBase"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.RainReachSpread, Const(typeof(BattleFx), "RainReachSpread"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.SkillReachMax, Const(typeof(BattleFx), "SkillReachMax"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.HopTime, Const(typeof(BattleView), "HopTime"), 1e-6f, "BattleView.Cast.HopTime");
            Assert.AreEqual(BattleAudioTiming.FirstHop, Const(typeof(BattleView), "FirstHop"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.GiftTravel, Const(typeof(BattleView), "GiftTravel"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.GiftMinDistance, Const(typeof(BattleView), "GiftMinDistance"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.EchoBodyLift, Const(typeof(BattleView), "EchoBodyLift"), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.FallbackPpu, Const(typeof(BattleFieldMapper), "ReferencePpu"), 1e-6f, "BattleFieldMapper.ReferencePpu");
        }

        [Test]
        public void EnemyViewSizes_MatchTheHitPointMirror()
        {
            var go = new GameObject("EnemyViewProbe");
            try
            {
                var view = go.AddComponent<EnemyView>();
                float Field(string name)
                {
                    var field = typeof(EnemyView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.IsNotNull(field, "EnemyView." + name);
                    return (float)field.GetValue(view);
                }
                Assert.AreEqual(BattleAudioTiming.EnemySize, Field("_normalSize"), 1e-6f);
                Assert.AreEqual(BattleAudioTiming.BossSize, Field("_bossSize"), 1e-6f);
                Assert.AreEqual(BattleAudioTiming.SplitScale, Field("_splitScale"), 1e-6f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SkillDelays_UsePublicFxRules()
        {
            Assert.AreEqual(BattleAudioTiming.BeamLead + BattleFx.BeamReach * 0.5f, BattleAudioTiming.BeamDelay(0.5f), 1e-6f);
            Assert.AreEqual(BattleAudioTiming.SplashBase + 0.2f, BattleAudioTiming.SplashDelay(280f), 1e-5f);
            Assert.AreEqual(BattleAudioTiming.SplashBase + BattleAudioTiming.SplashMax, BattleAudioTiming.SplashDelay(5000f), 1e-5f);
            Assert.AreEqual(0.05f, BattleAudioTiming.AreaDelay(SkillVfxStyle.Burst, new Vector2(200f, 0f), 200f, 0.72f), 1e-5f);
            Assert.AreEqual(BattleAudioTiming.BurstStaggerMax, BattleAudioTiming.AreaDelay(SkillVfxStyle.Burst, new Vector2(2000f, 0f), 200f, 0.72f), 1e-5f);
            Assert.AreEqual(0.4f, BattleAudioTiming.AreaDelay(SkillVfxStyle.Rain, Vector2.zero, 200f, 0.72f), 1e-5f);
            Assert.AreEqual(0.5f, BattleAudioTiming.AreaDelay(SkillVfxStyle.Rain, new Vector2(400f, 0f), 200f, 0.72f), 1e-5f);
            Assert.AreEqual(0f, BattleAudioTiming.AreaDelay(SkillVfxStyle.Dome, new Vector2(400f, 0f), 200f, 0.72f));
            Assert.AreEqual(BattleFx.StraightFlight(Vector2.zero, new Vector2(800f, 0f)),
                BattleAudioTiming.SkillFlight(SkillVfx.KeyBeam, true, Vector2.zero, new Vector2(800f, 0f)), 1e-6f);
            Assert.AreEqual(0.38f, BattleAudioTiming.SkillFlight(SkillVfx.ThreadStitch, false, Vector2.zero, Vector2.zero), 1e-6f);
            Assert.AreEqual(0.26f, BattleAudioTiming.SkillFlight(SkillVfx.KeyBeam, false, Vector2.zero, Vector2.zero), 1e-6f);
        }

        [Test]
        public void Pan_AndProximity()
        {
            Assert.AreEqual(0f, BattleAudioTiming.Pan(0f), 1e-6f);
            Assert.AreEqual(0.35f, BattleAudioTiming.Pan(5f), 1e-6f, "좌우 끝 ±0.35");
            Assert.AreEqual(-0.175f, BattleAudioTiming.Pan(-5f, true), 1e-6f, "보스는 반만");
            Assert.AreEqual(0f, BattleAudioTiming.ProximityDb(1f), 1e-6f, "방위 링에서 0 dB");
            Assert.AreEqual(-2f, BattleAudioTiming.ProximityDb(2.5f), 1e-6f, "등장 반지름에서 −2 dB");
            float interval = EnemyFxCatalog.Look("enemy_moth").Interval;
            Assert.AreEqual(interval - BattleAudioTiming.StrikeGapSlack, BattleAudioTiming.StrikeGap("enemy_moth", false, 1), 1e-5f);
            Assert.AreEqual(interval / 2f - BattleAudioTiming.StrikeGapSlack, BattleAudioTiming.StrikeGap("enemy_moth", false, 2), 1e-5f, "2배속은 간격 절반");
        }
    }
}
