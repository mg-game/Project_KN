using IdleGame.UI;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 전투 소리 시간표 — 연출 쪽 시간 규칙의 읽기 전용 거울 (AUDIO-SPEC §5.3, 모두 연출 시계 초).
    /// 공개된 규칙은 그대로 부르고 (AttackFxCatalog.FlightTime · Windup · KillHold · SpeedFlightScale · SpeedPopScale, EnemyFxCatalog.KillBlowHold · Look(..).Interval,
    /// LampWave.Delay, BattleFx.StraightFlight · BeamReach · RingReachTime · OrbitPassTime · HealRingTime · RibbonTravel),
    /// 비공개 숫자는 출처를 적어 여기 베껴 둔다 — AudioTimingTests가 리플렉션으로 연출 쪽 값과 같은지 지킨다 (바뀌면 테스트가 실패한다).
    /// </summary>
    public static class BattleAudioTiming
    {
        // ── 베낀 숫자 (출처) ──

        /// <summary>BattleFx.EchoShotLife (BattleFx.Attack.cs, private const) — 잔향이 원거리 투사체</summary>
        public const float EchoShotLife = 0.14f;
        /// <summary>BattleFx.EchoSwing (BattleFx.Attack.cs, private const) — 잔향이 근접 베기 꼭대기</summary>
        public const float EchoSwing = 0.05f;
        /// <summary>BattleFx.SkillProjectile 리터럴 (vfx_thread_stitch 0.38, 그 밖 0.26)</summary>
        public const float ThreadStitchFlight = 0.38f;
        public const float SkillProjectileFlight = 0.26f;
        /// <summary>BattleView.Cast.HopTime / FirstHop / GiftTravel / GiftMinDistance (private const)</summary>
        public const float HopTime = 0.09f;
        public const float FirstHop = 0.08f;
        public const float GiftTravel = 0.3f;
        public const float GiftMinDistance = 60f;
        /// <summary>BattleView.Cast.CastDelay 광선 리터럴 (0.03 + BeamReach × 진행)</summary>
        public const float BeamLead = 0.03f;
        /// <summary>BattleView.Skills.SplashAllEnemies 리터럴 (0.08 + min(0.45, 거리 / 1400))</summary>
        public const float SplashBase = 0.08f;
        public const float SplashMax = 0.45f;
        public const float SplashPxPerSecond = 1400f;
        /// <summary>BattleFx.Skills BurstStagger / BurstStaggerMax / RainReachBase / RainReachSpread / SkillReachMax (private const)</summary>
        public const float BurstStagger = 0.025f;
        public const float BurstStaggerMax = 0.15f;
        public const float RainReachBase = 0.4f;
        public const float RainReachSpread = 0.1f;
        public const float SkillReachMax = 0.5f;
        /// <summary>GuardianSlotView.MuzzlePosition (발 + 위 78px × 액터 배율)</summary>
        public const float MuzzleLift = 78f;
        /// <summary>EnemyView 기본 크기 (_normalSize 78 / _bossSize 156 / 분열 0.66) × HitPoint 0.45</summary>
        public const float EnemySize = 78f;
        public const float BossSize = 156f;
        public const float SplitScale = 0.66f;
        public const float HitPointShare = 0.45f;
        /// <summary>BattleFieldMapper 기본값 (매퍼를 못 찾았을 때)</summary>
        public const float FallbackPpu = 205f;
        public const float FallbackSquash = 0.72f;
        /// <summary>BattleView.Enemy.StrikeArtDue 여유 (간격 ÷ 배속 − 0.03)</summary>
        public const float StrikeGapSlack = 0.03f;
        /// <summary>BattleView.Cast.EchoBodyLift (잔향이 발 → 몸 가운데)</summary>
        public const float EchoBodyLift = 50f;

        // ── 소리 쪽 규칙 ──

        /// <summary>전장 보이는 반폭 (월드, BattleFieldMapper._extentX) — 좌우 위치</summary>
        public const float FieldHalfWidth = 2.35f;
        public const float MaxPan = 0.35f;
        public const float BossPanScale = 0.5f;
        /// <summary>적 쪽 소리 가까움 강조: 등장 반지름 2.5에서 −2 dB → 방위 링 1.0에서 0 dB</summary>
        public const float ProximityFarDb = -2f;
        /// <summary>수선점 힐 맥박 방울이 문에 닿기까지 (BattleView OnShopHealed ≈ 0.4초)</summary>
        public const float PulseShopDrop = 0.4f;
        /// <summary>처치 단추가 상단 바에 닿기 전 출발 (BattleView.RewardKill +0.08)</summary>
        public const float KillCoinLead = 0.08f;
        /// <summary>기억 조각이 수선점 문에 닿는 순간 (처치 꽃 + 0.60 — FX-JUICE)</summary>
        public const float KillMoteDelay = 0.6f;

        /// <summary>지킴이 기본 공격이 닿는 시간 (AttackFxCatalog.FlightTime 그대로)</summary>
        public static float GuardianFlight(string lineageId, CombatStyle style, float distancePx, int speed, float actorScale) =>
            AttackFxCatalog.FlightTime(AttackFxCatalog.Look(lineageId), AttackFxCatalog.Style(style), distancePx, speed, actorScale);

        /// <summary>웅크림 W (총구 불꽃 = 쏘는 소리)</summary>
        public static float GuardianWindup(float flight, int speed) => AttackFxCatalog.Windup(flight, speed);

        /// <summary>잔향이 근접 명중 (베기 꼭대기)</summary>
        public static float EchoMeleeHit(int speed) => EchoSwing * AttackFxCatalog.SpeedPopScale(speed);

        /// <summary>잔향이 원거리 명중</summary>
        public static float EchoRangedHit(int speed) => EchoShotLife * AttackFxCatalog.SpeedFlightScale(speed);

        /// <summary>처치 꽃이 터지는 순간 (EnemyFxCatalog.KillBlowHold 그대로 — EnemyView.MarkKilled와 같다)</summary>
        public static float KillPop(float hold, float lastBlow, bool boss) => EnemyFxCatalog.KillBlowHold(hold, lastBlow, boss);

        /// <summary>날아오는 투사체가 있을 때의 처치 보류 (AttackFxCatalog.KillHold)</summary>
        public static float KillHold(float remainingFlight) => AttackFxCatalog.KillHold(remainingFlight);

        /// <summary>불 밝히기 빛 물결이 그 적에 닿는 순간 (LampWave.Start + LampWave.Delay(수선점에서의 월드 거리))</summary>
        public static float LampArrival(Vector2 enemyWorld) => LampWave.Start + LampWave.Delay(enemyWorld.magnitude);

        /// <summary>스킬 투사체 비행 (레인 투사체는 BattleFx.StraightFlight)</summary>
        public static float SkillFlight(string vfx, bool lane, Vector2 fromPx, Vector2 laneEndPx) =>
            lane ? BattleFx.StraightFlight(fromPx, laneEndPx) : vfx == SkillVfx.ThreadStitch ? ThreadStitchFlight : SkillProjectileFlight;

        /// <summary>광선이 그 적을 지나는 순간 (along = 시전자 → 끝 선분 위 비율 0~1)</summary>
        public static float BeamDelay(float along) => BeamLead + BattleFx.BeamReach * Mathf.Clamp01(along);

        /// <summary>적 전체 스킬 물결 (가운데 → 그 적 px)</summary>
        public static float SplashDelay(float distancePx) => SplashBase + Mathf.Min(SplashMax, Mathf.Max(0f, distancePx) / SplashPxPerSecond);

        /// <summary>
        /// 범위 스킬이 그 적에 닿는 순간 (BattleFx.SkillReachDelay 거울): 폭발 100px마다 25ms (최대 0.15) · 고리 반지름 · 회전 · 비 0.40 + 0.10 × 비율.
        /// offsetPx = 그 적 발 − 가운데 (전장 px), radiusPx = 가로 반지름, squash = 바닥 눌림
        /// </summary>
        public static float AreaDelay(SkillVfxStyle style, Vector2 offsetPx, float radiusPx, float squash)
        {
            float aspect = Mathf.Max(0.1f, squash);
            switch (style)
            {
                case SkillVfxStyle.Burst:
                    return Mathf.Min(BurstStaggerMax, BurstStagger * offsetPx.magnitude / 100f);
                case SkillVfxStyle.Ring:
                    return Mathf.Min(SkillReachMax, BattleFx.RingReachTime(new Vector2(offsetPx.x, offsetPx.y / aspect).magnitude, Mathf.Max(40f, radiusPx)));
                case SkillVfxStyle.Orbit:
                    return BattleFx.OrbitPassTime(Mathf.Atan2(offsetPx.y / Mathf.Max(1f, radiusPx * aspect), offsetPx.x / Mathf.Max(1f, radiusPx)));
                case SkillVfxStyle.Rain:
                {
                    float reach = new Vector2(offsetPx.x, offsetPx.y / aspect).magnitude;
                    return RainReachBase + RainReachSpread * Mathf.Clamp01(reach / Mathf.Max(1f, radiusPx));
                }
            }
            return 0f;
        }

        /// <summary>적 공격 소리 간격 (적마다 그림 주기 ÷ 배속 − 0.03, BattleView.Enemy.StrikeArtDue 거울)</summary>
        public static float StrikeGap(string enemyId, bool boss, int speed) =>
            EnemyFxCatalog.Look(enemyId, boss).Interval / Mathf.Max(1, speed) - StrikeGapSlack;

        /// <summary>힐 맥박 고리가 그 잔향이에게 닿기까지 (BattleFx.HealRingTime × 거리 비율)</summary>
        public static float HealRingDelay(float distanceShare) => BattleFx.HealRingTime * Mathf.Clamp01(distanceShare);

        /// <summary>좌우 위치: clamp(x / 2.35, −1, 1) × 0.35 (보스 ×0.5)</summary>
        public static float Pan(float worldX, bool boss = false) =>
            Mathf.Clamp(worldX / FieldHalfWidth, -1f, 1f) * MaxPan * (boss ? BossPanScale : 1f);

        /// <summary>적 쪽 소리 가까움 (dB): 등장 반지름 2.5에서 −2 → 방위 링 1.0에서 0</summary>
        public static float ProximityDb(float worldRadius)
        {
            float k = Mathf.Clamp01((worldRadius - BattleGeometry.RingRadius) / (BattleGeometry.SpawnRadius - BattleGeometry.RingRadius));
            return ProximityFarDb * k;
        }

        /// <summary>적 명중점 높이 (전장 px)</summary>
        public static float EnemyHitLift(bool boss, bool split, float actorScale) =>
            (boss ? BossSize : EnemySize) * HitPointShare * (split && !boss ? SplitScale : 1f) * Mathf.Max(0.5f, actorScale);
    }
}
