using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 명중 등급 하나의 손맛 수치 묶음 (FX-JUICE §2.1 TierFeel 표 — 하나뿐인 기준, 모든 연출이 여기서 읽는다).
    /// HitFeel.Feel(tier)로 얻고, 보스·잔향이·보호막 보정은 HitFeel.ForBoss / ForEcho / ForGuarded.
    ///   크기 배율(Core, BackingSize)은 그 명중의 '자리 잡은 크기' S에 곱한다 (S × SizeScale — 잔향이 0.65, 보호막 0.7).
    ///   시간은 초 (연출 시계). 명중 그림 수명(Life)·코어 수명은 부르는 쪽이 PopScale(2배속 0.8)을 곱한다.
    ///   FirstFrame: 닿는 첫 프레임 크기 = S × FirstFrame, Hold 동안 버티고 Settle 동안 S로 줄어든다 (첫 프레임이 가장 크다).
    /// </summary>
    public readonly struct TierFeel
    {
        public readonly HitTier Tier;
        /// <summary>첫 프레임 크기 배율 (1.30 = 닿는 순간 S의 1.3배)</summary>
        public readonly float FirstFrame;
        /// <summary>첫 프레임 크기를 버티는 시간</summary>
        public readonly float Hold;
        /// <summary>S로 줄어드는 시간 (OutCubic)</summary>
        public readonly float Settle;
        /// <summary>명중 그림 수명 (×PopScale)</summary>
        public readonly float Life;
        /// <summary>가산 코어 별 크기 (× S) / 수명</summary>
        public readonly float Core;
        public readonly float CoreLife;
        /// <summary>어두운 잉크 받침 알파 / 크기 (× S). 0이면 받침 없음</summary>
        public readonly float Backing;
        public readonly float BackingSize;
        /// <summary>충격 고리 수</summary>
        public readonly int Rings;
        /// <summary>빛줄기 수 / 퍼지는 원뿔 각(도) / 빠르기(px/s)</summary>
        public readonly int Streaks;
        public readonly float StreakCone;
        public readonly float StreakSpeedMin;
        public readonly float StreakSpeedMax;
        /// <summary>몸 흰 번쩍임 알파 / 버팀 / 전체 시간</summary>
        public readonly float FlashAlpha;
        public readonly float FlashHold;
        public readonly float FlashLife;
        /// <summary>눌림 꼭대기 (가로 X / 세로 Y, 용수철로 한 번 되튀며 돌아온다)</summary>
        public readonly float SquashX;
        public readonly float SquashY;
        /// <summary>넉백 (px, 대상 정지가 끝난 뒤에 밀린다)</summary>
        public readonly float Knockback;
        /// <summary>대상만 멈추는 시간 / 그동안 떨림 폭(px)</summary>
        public readonly float Freeze;
        public readonly float Shiver;
        /// <summary>숫자 크기 (× 46pt)</summary>
        public readonly float NumberScale;
        /// <summary>자리 잡은 크기 S에 곱할 배율 (기본 1 · 잔향이 0.65 · 보호막 0.7). 부르는 쪽이 잔향이/보호막 배율을 따로 곱하지 않는다</summary>
        public readonly float SizeScale;

        public TierFeel(HitTier tier, float firstFrame, float hold, float settle, float life, float core, float coreLife, float backing, float backingSize,
            int rings, int streaks, float streakCone, float streakSpeedMin, float streakSpeedMax, float flashAlpha, float flashHold, float flashLife,
            float squashX, float squashY, float knockback, float freeze, float shiver, float numberScale, float sizeScale = 1f)
        {
            Tier = tier;
            FirstFrame = firstFrame;
            Hold = hold;
            Settle = settle;
            Life = life;
            Core = core;
            CoreLife = coreLife;
            Backing = backing;
            BackingSize = backingSize;
            Rings = rings;
            Streaks = streaks;
            StreakCone = streakCone;
            StreakSpeedMin = streakSpeedMin;
            StreakSpeedMax = streakSpeedMax;
            FlashAlpha = flashAlpha;
            FlashHold = flashHold;
            FlashLife = flashLife;
            SquashX = squashX;
            SquashY = squashY;
            Knockback = knockback;
            Freeze = freeze;
            Shiver = shiver;
            NumberScale = numberScale;
            SizeScale = sizeScale;
        }
    }

    /// <summary>화면 킥 채널 (채널마다 최소 간격 HitFeel.KickInterval)</summary>
    public enum KickChannel : byte
    {
        Crit,
        Snipe,
        Shop,
        Kill,
        Skill,
        Shield,
        Siege,
    }

    /// <summary>처치·축하 색종이 묶음 (BattleFx.Confetti)</summary>
    public enum ConfettiSet : byte
    {
        /// <summary>먼지 갈래: 회보라 먼지 · 크림/금 별 · 작은 퍽</summary>
        Dust,
        /// <summary>나방 갈래: 비늘 가루 · 꽃잎</summary>
        Moth,
        /// <summary>쇠붙이 갈래: 톱니 · 나사 · 용수철(튐) · 금 단추</summary>
        Metal,
        /// <summary>그림자 갈래: 실 조각 · 등불색 별</summary>
        Shadow,
        /// <summary>스테이지 클리어: 단추 · 리본 · 별 · 실 · 별 모양</summary>
        Celebration,
        /// <summary>보스 처치: 단추 · 리본 · 별 · 실 · 별 모양 + 금</summary>
        Boss,
    }

    /// <summary>
    /// 손맛 수치 (FX-JUICE §2 HitFeel v2 / FX-01 타격 사다리). 자동 전투에서 유닛 10여 개가 동시에 때리므로 전체 정지는 드문 순간
    /// (보스 처치 1.5초에 한 번, 불 밝히기)만, 나머지는 맞은 적만 멈춘다. 흰 번쩍임은 UI/WhiteFlash 덧그림(EnemyView), 넉백은 보기 전용 오프셋.
    /// 등급 표(Feel)가 하나뿐인 기준이고, 예전 함수(FlashTime·FlashStrength·TargetStop·Knockback)는 그 표에서 읽는다.
    /// </summary>
    public static partial class HitFeel
    {
        // ───────────── 등급 표 (§2.1) ─────────────

        static readonly TierFeel NormalFeel = new(HitTier.Normal, 1.30f, 0.033f, 0.070f, 0.24f, 0.50f, 0.050f, 0.24f, 0.90f,
            1, 4, 100f, 450f, 700f, 0.85f, 0.033f, 0.07f, 1.16f, 0.86f, 8f, 0f, 0f, 1.0f);
        static readonly TierFeel HeavyFeel = new(HitTier.Heavy, 1.35f, 0.033f, 0.070f, 0.27f, 0.60f, 0.066f, 0.32f, 0.95f,
            1, 6, 70f, 700f, 1000f, 0.95f, 0.033f, 0.08f, 1.20f, 0.82f, 12f, 0.033f, 3f, 1.15f);
        static readonly TierFeel CritFeel = new(HitTier.Crit, 1.45f, 0.050f, 0.080f, 0.33f, 0.75f, 0.083f, 0.40f, 1.0f,
            2, 8, 360f, 600f, 1000f, 1.0f, 0.05f, 0.10f, 1.26f, 0.78f, 16f, 0.066f, 5f, 1.6f);
        static readonly TierFeel SkillFeel = new(HitTier.Skill, 1.35f, 0.033f, 0.070f, 0.27f, 0.60f, 0.066f, 0.34f, 1.0f,
            1, 6, 70f, 600f, 900f, 0.9f, 0.033f, 0.08f, 1.20f, 0.82f, 12f, 0.05f, 3f, 1.2f);
        static readonly TierFeel LampFeel = new(HitTier.Lamp, 1.30f, 0.033f, 0.070f, 0.40f, 0.60f, 0.066f, 0f, 0f,
            1, 3, 40f, 500f, 800f, 0.85f, 0.033f, 0.08f, 1.12f, 0.90f, 10f, 0f, 0f, 1.1f);
        /// <summary>지속 피해 틱: 명중 그림 없음 (불씨 하나 + 옅은 번쩍임)</summary>
        static readonly TierFeel TickFeel = new(HitTier.Tick, 1.0f, 0f, 0.05f, 0.2f, 0f, 0f, 0f, 0f,
            0, 0, 0f, 0f, 0f, 0.35f, 0f, 0.05f, 1.04f, 0.97f, 0f, 0f, 0f, 0.85f);

        /// <summary>등급의 손맛 수치 (§2.1 표)</summary>
        public static TierFeel Feel(HitTier tier) => tier switch
        {
            HitTier.Heavy => HeavyFeel,
            HitTier.Crit => CritFeel,
            HitTier.Skill => SkillFeel,
            HitTier.Lamp => LampFeel,
            HitTier.Tick => TickFeel,
            _ => NormalFeel,
        };

        /// <summary>
        /// 보스가 맞음: 번쩍임 ×0.82 (일반은 0.7 상한) · 눌림 폭 ×0.35 · 넉백 3 · 정지는 강타·치명타만 최대 0.033초(떨림 2) · 첫 프레임 −0.05
        /// </summary>
        public static TierFeel ForBoss(in TierFeel f)
        {
            float flash = f.FlashAlpha * 0.82f;
            if (f.Tier == HitTier.Normal) flash = Mathf.Min(flash, 0.7f);
            float freeze = f.Tier == HitTier.Heavy || f.Tier == HitTier.Crit ? Mathf.Min(0.033f, f.Freeze) : 0f;
            return new TierFeel(f.Tier, Mathf.Max(1f, f.FirstFrame - 0.05f), f.Hold, f.Settle, f.Life, f.Core, f.CoreLife, f.Backing, f.BackingSize,
                f.Rings, f.Streaks, f.StreakCone, f.StreakSpeedMin, f.StreakSpeedMax, flash, f.FlashHold, f.FlashLife,
                1f + (f.SquashX - 1f) * 0.35f, 1f - (1f - f.SquashY) * 0.35f, f.Tier == HitTier.Tick ? 0f : 3f, freeze, freeze > 0f ? 2f : 0f,
                f.NumberScale, f.SizeScale);
        }

        /// <summary>
        /// 잔향이 공격: 크기 ×0.65 · 빛줄기 절반(최소 1) · 번쩍임 ×0.5 (버팀 17ms, 전체 60ms까지) · 눌림 폭 ×0.65 · 넉백 ×0.6 · 정지 없음
        /// </summary>
        public static TierFeel ForEcho(in TierFeel f)
        {
            int streaks = f.Streaks > 0 ? Mathf.Max(1, f.Streaks / 2) : 0;
            return new TierFeel(f.Tier, f.FirstFrame, f.Hold, f.Settle, f.Life, f.Core, f.CoreLife, f.Backing, f.BackingSize,
                f.Rings, streaks, f.StreakCone, f.StreakSpeedMin, f.StreakSpeedMax, f.FlashAlpha * 0.5f, Mathf.Min(f.FlashHold, 0.017f),
                Mathf.Min(f.FlashLife, 0.06f), 1f + (f.SquashX - 1f) * 0.65f, 1f - (1f - f.SquashY) * 0.65f, f.Knockback * 0.6f, 0f, 0f,
                f.NumberScale, f.SizeScale * 0.65f);
        }

        /// <summary>보호막에 막힌 명중: 크기 ×0.7 · 번쩍임 ×0.55 · 넉백 없음</summary>
        public static TierFeel ForGuarded(in TierFeel f) =>
            new(f.Tier, f.FirstFrame, f.Hold, f.Settle, f.Life, f.Core, f.CoreLife, f.Backing, f.BackingSize,
                f.Rings, f.Streaks, f.StreakCone, f.StreakSpeedMin, f.StreakSpeedMax, f.FlashAlpha * 0.55f, f.FlashHold, f.FlashLife,
                f.SquashX, f.SquashY, 0f, f.Freeze, f.Shiver, f.NumberScale, f.SizeScale * 0.7f);

        // ───────────── 예전 함수 (표에서 읽는다) ─────────────

        /// <summary>흰 번쩍임 전체 길이 (초) = Feel(tier).FlashLife</summary>
        public static float FlashTime(HitTier tier) => Feel(tier).FlashLife;

        /// <summary>흰 번쩍임 세기 (덧그림 알파) = Feel(tier).FlashAlpha</summary>
        public static float FlashStrength(HitTier tier) => Feel(tier).FlashAlpha;

        /// <summary>맞은 적만 멈추는 시간 (초): 강타 0.033 / 치명타 0.066 / 스킬 0.05 = Feel(tier).Freeze</summary>
        public static float TargetStop(HitTier tier) => Feel(tier).Freeze;

        /// <summary>넉백 (px): 일반 8 / 강타 12 / 치명 16 / 스킬 12 / 불빛 10, 보스는 3으로 묵직하게. 지속 피해는 없음</summary>
        public static float Knockback(HitTier tier, bool boss) => boss && tier != HitTier.Tick ? 3f : Feel(tier).Knockback;

        /// <summary>넉백이 밀려 나가는 시간 (OutExpo) / 돌아오는 시간 (OutCubic)</summary>
        public const float KnockOut = 0.04f;
        public const float KnockReturn = 0.12f;
        /// <summary>넉백 전체 시간 (밀림 + 돌아옴)</summary>
        public const float KnockbackTime = KnockOut + KnockReturn;

        /// <summary>대상 정지 예산: FreezeWindow(0.5초)마다 FreezeBudget(0.15초)까지 (새는 양동이, EnemyView)</summary>
        public const float FreezeBudget = 0.15f;
        public const float FreezeWindow = 0.5f;

        // ───────────── 처치 (§2.1) ─────────────

        public const float KillFlashAlpha = 1f;
        public const float KillShiver = 4f;
        public const float KillSquashX = 1.30f;
        public const float KillSquashY = 0.72f;
        public const float KillPop = 0.04f;
        /// <summary>일반 처치 꽃 크기 K / 보스 처치 영웅 폭발 B (px × ActorScale)</summary>
        public const float KillBurstSize = 200f;
        public const float BossBurstSize = 420f;

        /// <summary>
        /// 색종이 조각 크기 배율 (BattleFx.Confetti — 조각 기준 px × 이 값 × U): 처치 갈래(먼지 · 나방 · 쇠붙이 · 그림자) ×1.45 · 보스 ×1.15 · 축하 ×1.
        /// 540x960 기기 화면에서 단추·톱니·실·꽃잎이 7~11px 점으로 보이던 처치 '풀려남'을 읽히게 키운다 (수는 품질 표 그대로)
        /// </summary>
        public static float ConfettiSizeScale(ConfettiSet set) => set switch
        {
            ConfettiSet.Boss => 1.15f,
            ConfettiSet.Celebration => 1f,
            _ => 1.45f,
        };
        /// <summary>명중 그림을 공격자 쪽으로 당기는 거리 (px × U — 맞은 몸이 가려지지 않게)</summary>
        public const float ImpactTowardAttacker = 12f;
        /// <summary>숫자는 명중 33ms 뒤에 뜬다 (빛이 먼저, 숫자가 다음)</summary>
        public const float NumberLag = 0.033f;

        // ───────────── 튜토리얼 연출 (FTUE-01, SPEC4 §13.3 — AppSignals 표시를 BattleView가 읽는다) ─────────────

        /// <summary>첫 불 밝히기 (AppSignals.FtueFirstLampBeat): 점화 연출 배율 0.2를 0.6초 (평소 LampWave.IgniteTime 0.12초) — 시뮬레이션도 PresentationScale로 같이 느려진다</summary>
        public const float FirstLampIgniteScale = 0.2f;
        public const float FirstLampIgniteTime = 0.6f;
        /// <summary>첫 전투 첫인상 (AppSignals.FtueFirstImpression): 지킴이 명중 그림 ×1.3 + 반짝임 2개 더 (숫자 · 흔들림은 그대로)</summary>
        public const float FirstImpressionScale = 1.3f;
        public const int FirstImpressionSparkles = 2;

        // ───────────── 화면 흔들림 trauma (§2.2) — 실효 꼭대기 ≈ 0.6 × 30 × trauma² px ─────────────

        /// <summary>치명타는 trauma 대신 킥 (KickCrit)</summary>
        public const float CritTrauma = 0f;
        public const float CritTraumaInterval = 0.3f;
        /// <summary>범위 스킬 (스킬 채널, 초당 SkillTraumaPerSecond까지) ≈ 2.9px</summary>
        public const float AreaSkillTrauma = 0.40f;
        public const float SkillTraumaPerSecond = 0.5f;
        /// <summary>수선점 일반 피격 (초당 ShopTraumaPerSecond까지) ≈ 1.6px</summary>
        public const float ShopHitTrauma = 0.30f;
        public const float ShopTraumaPerSecond = 0.60f;
        /// <summary>수선점 보호막 깨짐 ≈ 2.2px</summary>
        public const float ShopShieldBreakTrauma = 0.35f;
        /// <summary>보스 포위 ≈ 5.4px</summary>
        public const float BossSiegeTrauma = 0.55f;
        /// <summary>적 분노 ≈ 3.6px</summary>
        public const float BossEnrageTrauma = 0.45f;
        /// <summary>보스 등장 ≈ 6.9px</summary>
        public const float BossSpawnTrauma = 0.62f;
        /// <summary>실패 ≈ 7.6px</summary>
        public const float FailTrauma = 0.65f;
        /// <summary>
        /// 보스 처치 ≈ 8.7px (목표 띠 8~14px). 0.6은 평균이지 꼭대기가 아니라 0.8이면 20px까지 갔다.
        /// Wave 3 (fx2 leftovers, 비평 nice): 0.68 → 0.72 — 0.68의 그림자 보스 처치 캡처가 7.81px로 띠 바로 아래였다 (나방 여왕 9.41px).
        /// 잡음이 ±1에 닿는 이론 꼭대기는 30 × 0.72² ≈ 15.6px지만 Perlin은 거기까지 가지 않는다 (캡처 기준 ≈ 8.7px)
        /// </summary>
        public const float BossKillTrauma = 0.72f;
        /// <summary>불 밝히기 ≈ 13px</summary>
        public const float LampTrauma = 0.85f;

        // ───────────── 킥 (방향 있는 한 번 밀림, 잡음 아님) ─────────────
        // 2차 조율: 치명타 3 → 4 · 저격 2 → 3 · 상한 4 → 5 (3px는 540 폭 기기 화면에서 1.5px — '타격감이 밋밋하다'에 비해 너무 작다). 채널 간격은 그대로

        public const float KickCrit = 4f;
        public const float KickBossCrit = 2f;
        public const float KickSnipe = 3f;
        public const float KickShop = 2f;
        public const float KickSiege = 4f;
        public const float KickKill = 2f;
        public const float KickSkill = 2f;
        public const float KickShield = 2f;
        /// <summary>킥 크기 상한 (px)</summary>
        public const float KickMax = 5f;

        /// <summary>채널별 킥 최소 간격 (초): 치명타 0.5 · 저격 0.6 · 수선점 0.25 · 처치 0.5 · 스킬 0.4 · 보호막 0.5 · 포위 0.5</summary>
        public static float KickInterval(KickChannel channel) => channel switch
        {
            KickChannel.Crit => 0.5f,
            KickChannel.Snipe => 0.6f,
            KickChannel.Shop => 0.25f,
            KickChannel.Kill => 0.5f,
            KickChannel.Skill => 0.4f,
            KickChannel.Shield => 0.5f,
            KickChannel.Siege => 0.5f,
            _ => 0.5f,
        };

        // ───────────── 줌 펀치 (전장만 확대) ─────────────

        public const float ZoomDefault = 1.02f;
        public const float ZoomBossKill = 1.03f;
        public const float ZoomLamp = 1.035f;
        public const float ZoomStageClear = 1.015f;
        public const float ZoomMultiKill = 1.012f;

        // ───────────── 간격 / 밀도 규칙 (§2.4) ─────────────

        /// <summary>전체 정지 / 전장 전체 번쩍임(빛·틴트·임팩트 프레임) / 줌 펀치 최소 간격 (실제 시간 초)</summary>
        public const float GlobalStopInterval = 1.5f;
        public const float FieldFlashInterval = 1.0f;
        public const float ZoomInterval = 0.8f;
        /// <summary>밀도: 이 도착 전 0.25초 안의 예약된 도착 수 — 8 이하 풀 / 9~16 미니 / 그 이상 린(미니 그림 + 코어만)</summary>
        public const float DensityWindow = 0.25f;
        public const int DensityMini = 8;
        public const int DensityLean = 16;
        /// <summary>같은 대상 풀 명중은 90ms에 하나 (나머지는 미니). 0.6초 안에 이어지는 명중 사슬의 3번째마다 리듬 강조</summary>
        public const float FullImpactInterval = 0.09f;
        public const float RhythmChainGap = 0.6f;
        public const int RhythmEvery = 3;
        /// <summary>큰 터짐(치명타·처치·스킬 폭발)은 0.1초에 2개까지 시작 (넘치면 +33ms, +50ms 미루고 그 뒤로는 작게)</summary>
        public const float BigBurstWindow = 0.1f;
        public const int BigBurstsPerWindow = 2;
        /// <summary>풀 처치 꽃은 0.2초에 3개까지 (넘치면 미니 처치)</summary>
        public const float KillBurstWindow = 0.2f;
        public const int KillBurstsPerWindow = 3;
        /// <summary>연속 처치는 1.2초 안에 이어져야 하고, 0.25초 안에 터진 처치들은 '한꺼번에'</summary>
        public const float StreakGap = 1.2f;
        public const float MultiKillWindow = 0.25f;

        /// <summary>연속 처치 알림 단계: 5 · 10 · 20 · 40, 그 뒤로 20마다</summary>
        public static bool IsStreakMilestone(int streak) => streak == 5 || streak == 10 || streak == 20 || (streak >= 40 && streak % 20 == 0);

        // ───────────── 품질별 예산 (§2.5 / §3.4) ─────────────

        /// <summary>위쪽 입자 동시 상한: 높음 420 / 보통 240 / 낮음 130 (최소 60)</summary>
        public static int ParticleBudget(FxQuality quality) => Mathf.Max(60, quality switch
        {
            FxQuality.Low => 130,
            FxQuality.Medium => 240,
            _ => 420,
        });

        /// <summary>바닥 입자 동시 상한: 96 / 64 / 32</summary>
        public static int GroundBudget(FxQuality quality) => quality switch
        {
            FxQuality.Low => 32,
            FxQuality.Medium => 64,
            _ => 96,
        };

        /// <summary>화면에 떠 있는 숫자 상한: 24 / 16 / 10</summary>
        public static int MaxNumbers(FxQuality quality) => quality switch
        {
            FxQuality.Low => 10,
            FxQuality.Medium => 16,
            _ => 24,
        };

        /// <summary>살아 있는 큰 가산 후광(크기 1.8배 이상) 상한: 6 / 4 / 2</summary>
        public static int HaloCap(FxQuality quality) => quality switch
        {
            FxQuality.Low => 2,
            FxQuality.Medium => 4,
            _ => 6,
        };

        /// <summary>바닥 자국(데칼) 상한: 12 / 6 / 0</summary>
        public static int DecalCap(FxQuality quality) => quality switch
        {
            FxQuality.Low => 0,
            FxQuality.Medium => 6,
            _ => 12,
        };

        /// <summary>프레임당 별빛·곁들임 생성 상한: 48 / 36 / 24</summary>
        public static int FrameSpawnCap(FxQuality quality) => quality switch
        {
            FxQuality.Low => 24,
            FxQuality.Medium => 36,
            _ => 48,
        };

        /// <summary>
        /// 영웅 층 몫 (위쪽 예산의 마지막 칸): 별빛 층(반짝이 Drift · 빛줄기 Streak · 색종이 · 여운 반짝)은 이 칸을 쓰지 못한다 —
        /// 투사체 머리 · 명중 그림 · 처치 꽃 · 코어 별처럼 공격이 읽히는 층이 꽉 찬 전장에서도 뜨게 (§3.4 버리는 순서의 마지막 방어선)
        /// </summary>
        public const int HeroReserve = 16;

        /// <summary>바닥 예산의 마지막 칸: 곁들임 바닥 층(잉크 받침 · 자국)은 이 칸을 쓰지 못한다 (포위 텔레그래프 · 보스 웅덩이 · 처치 고리 몫)</summary>
        public const int GroundReserve = 8;

        /// <summary>불 밝히기 별비: 물결에 맞은 적마다 처치 연출 몫으로 남겨 둘 칸 — 여유가 (맞은 적 × 이 값)보다 적으면 별비를 10개로</summary>
        public const int LampKillReserve = 6;

        // ───────────── 전체 정지 (보스 처치·불 밝히기) ─────────────

        /// <summary>보스 처치·불 밝히기 전체 정지 (초, 실제 시간) → 느린 화면 배율 / 시간</summary>
        public const float GlobalStop = 0.12f;
        public const float SlowScale = 0.3f;
        public const float SlowTime = 0.18f;

        /// <summary>
        /// 전장(ShakeRoot) 자세: offset = trauma + kick × 배율, 확대 z = 1 + (zoom − 1) × 배율, 확대 중심 pivot이 제자리에 남도록 (1 − z) × pivot을 더한다.
        /// 배율(흔들림 설정 0/50/100%)이 0이면 움직이지도 커지지도 않는다 (trauma는 TraumaShake.Tick이 이미 배율을 곱해 넘긴다)
        /// </summary>
        public static void CameraPose(Vector2 traumaOffset, Vector2 kick, float zoom, float shakeScale, Vector2 pivot, out Vector2 position, out float scale)
        {
            float s = Mathf.Clamp01(shakeScale);
            scale = 1f + (zoom - 1f) * s;
            position = traumaOffset + kick * s + (1f - scale) * pivot;
        }
    }

    /// <summary>
    /// 흔들림 예산 (FX-02, Eiserloh trauma 모델). 흔들림 = 최대 30px × trauma², 회전 1° × trauma², 축마다 다른 Perlin 잡음,
    /// trauma는 초당 1.6씩 줄어든다 (잦은 사건이 계속 흔들리는 화면으로 쌓이지 않게). 방향을 주면 그 축으로 더 크게 흔들린다.
    /// 수선점 일반 피격(초당 0.6)·스킬(초당 0.5)은 따로 상한이 있는 채널. 설정 배율(0/50/100%)을 곱한다. 순수 C# (할당 없음).
    /// </summary>
    public sealed class TraumaShake
    {
        public const float MaxOffset = 30f;
        public const float MaxRotation = 1.0f;
        public const float Decay = 1.6f;
        const float Frequency = 22f;

        float _trauma;
        float _time;
        Vector2 _direction;
        float _shopBudget;
        float _skillBudget;
        float _critCooldown;
        readonly float _seedX;
        readonly float _seedY;
        readonly float _seedR;

        public TraumaShake(float seed = 0f)
        {
            _seedX = 11.3f + seed;
            _seedY = 47.9f + seed;
            _seedR = 83.1f + seed;
        }

        public float Trauma => _trauma;

        /// <summary>trauma를 더한다 (0~1). direction이 있으면 그 축으로 흔들림이 기운다</summary>
        public void Add(float amount, Vector2 direction = default)
        {
            if (!(amount > 0f)) return;
            float before = _trauma;
            _trauma = Mathf.Clamp01(_trauma + amount);
            if (direction.sqrMagnitude > 0.0001f)
            {
                float weight = amount / Mathf.Max(0.0001f, before + amount);
                _direction = Vector2.Lerp(_direction, direction.normalized, weight);
            }
        }

        /// <summary>
        /// 사방으로 흔들리는 trauma (SPEC4 §13.5 c — 불 밝히기): 더하면서 기울던 방향을 지운다. 흔들림 방향이 축마다 다른 잡음을 그대로 따른다
        /// (예전에는 Vector2.up으로 기울어 위아래로만 출렁였다). 뒤에 오는 방향 있는 사건(수선점 피격 등)은 다시 기울인다
        /// </summary>
        public void AddRadial(float amount)
        {
            if (!(amount > 0f)) return;
            _trauma = Mathf.Clamp01(_trauma + amount);
            _direction = Vector2.zero;
        }

        /// <summary>지금 흔들림이 한 방향으로 기울어 있는지 (AddRadial 뒤에는 false)</summary>
        public bool IsDirectional => _direction.sqrMagnitude > 0.0001f;

        /// <summary>수선점 일반 피격: 초당 합계 ShopTraumaPerSecond까지만 (여러 적이 한꺼번에 닿아도 화면이 계속 떨리지 않게)</summary>
        public void AddShopHit(float amount, Vector2 direction)
        {
            float allowed = Mathf.Min(amount, Mathf.Max(0f, HitFeel.ShopTraumaPerSecond - _shopBudget));
            if (allowed <= 0f) return;
            _shopBudget += allowed;
            Add(allowed, direction);
        }

        /// <summary>범위 스킬: 초당 합계 SkillTraumaPerSecond까지만 (20마리를 한꺼번에 쳐도 흔들림은 한 번 크기)</summary>
        public void AddSkill(float amount, Vector2 direction = default)
        {
            float allowed = Mathf.Min(amount, Mathf.Max(0f, HitFeel.SkillTraumaPerSecond - _skillBudget));
            if (allowed <= 0f) return;
            _skillBudget += allowed;
            Add(allowed, direction);
        }

        /// <summary>치명타 흔들림 (0.3초에 한 번만). v2에서 CritTrauma = 0 — 치명타는 킥으로 대신한다</summary>
        public void AddCrit()
        {
            if (_critCooldown > 0f) return;
            _critCooldown = HitFeel.CritTraumaInterval;
            Add(HitFeel.CritTrauma);
        }

        /// <summary>한 프레임 진행. 흔들림 오프셋(px)과 회전(도)을 돌려준다. scale = 설정 배율 0~1</summary>
        public void Tick(float dt, float scale, out Vector2 offset, out float rotation)
        {
            _time += dt;
            _shopBudget = Mathf.Max(0f, _shopBudget - HitFeel.ShopTraumaPerSecond * dt);
            _skillBudget = Mathf.Max(0f, _skillBudget - HitFeel.SkillTraumaPerSecond * dt);
            _critCooldown = Mathf.Max(0f, _critCooldown - dt);
            if (_trauma <= 0f)
            {
                offset = Vector2.zero;
                rotation = 0f;
                _direction = Vector2.zero;
                return;
            }
            _trauma = Mathf.Max(0f, _trauma - Decay * dt);
            float shake = _trauma * _trauma * Mathf.Clamp01(scale);
            float t = _time * Frequency;
            float nx = Mathf.PerlinNoise(_seedX, t) * 2f - 1f;
            float ny = Mathf.PerlinNoise(_seedY, t) * 2f - 1f;
            float nr = Mathf.PerlinNoise(_seedR, t) * 2f - 1f;
            var free = new Vector2(nx, ny);
            if (_direction.sqrMagnitude > 0.0001f)
            {
                // 주어진 방향으로 1.0, 옆으로 0.45 — 맞은 쪽으로 밀려났다 돌아오는 느낌
                var along = _direction.normalized;
                var side = new Vector2(-along.y, along.x);
                free = along * nx + side * (ny * 0.45f);
            }
            offset = free * (MaxOffset * shake);
            rotation = nr * MaxRotation * shake;
        }

        /// <summary>
        /// 모두 비운다. 잡음 시계도 0으로 — 같은 사건이면 같은 흔들림 (부르는 곳은 BattleFx.ClearParticles = 연출 시험장뿐: 캡처의 흔들림 꼭대기가
        /// 앞 항목들이 흘린 시간에 따라 달라지지 않게)
        /// </summary>
        public void Clear()
        {
            _trauma = 0f;
            _time = 0f;
            _direction = Vector2.zero;
            _shopBudget = 0f;
            _skillBudget = 0f;
            _critCooldown = 0f;
        }
    }

    /// <summary>
    /// 화면 킥 (§2.2): 잡음이 아닌 방향 있는 한 번 밀림. Add(dir, px) → 30ms 동안 OutQuad로 v = dir × px(최대 HitFeel.KickMax 5px)까지 밀리고,
    /// 임계 감쇠 g(τ) = (1 + 34τ)·e^(−34τ)로 돌아온다 (꼭대기 뒤 140ms에 ≈ 0.05). 여러 킥은 더해지지 않는다 — 지금 더 큰 쪽이 산다
    /// (새 킥은 지금 자리에서 출발해 끊기지 않는다). 순수 C# (할당 없음, 테스트 가능).
    /// </summary>
    public struct FxKick
    {
        const float Rise = 0.03f;
        const float Damping = 34f;
        const float End = 0.32f;

        Vector2 _from;
        Vector2 _target;
        float _age;
        bool _active;

        public bool Active => _active;

        /// <summary>지금 밀린 양 (px)</summary>
        public Vector2 Current => _active ? Evaluate(_age) : Vector2.zero;

        /// <summary>dir 쪽으로 px만큼 밀기 (지금 밀린 양이 더 크면 무시하고 false)</summary>
        public bool Add(Vector2 dir, float px)
        {
            if (!(px > 0f) || dir.sqrMagnitude < 1e-6f) return false;
            var target = dir.normalized * Mathf.Min(px, HitFeel.KickMax);
            var current = Current;
            if (_active && target.magnitude < current.magnitude) return false;
            _from = current;
            _target = target;
            _age = 0f;
            _active = true;
            return true;
        }

        /// <summary>dt 진행 후 밀린 양 (px)</summary>
        public Vector2 Tick(float dt)
        {
            if (!_active) return Vector2.zero;
            _age += Mathf.Max(0f, dt);
            if (_age >= End)
            {
                _active = false;
                return Vector2.zero;
            }
            return Evaluate(_age);
        }

        public void Clear()
        {
            _active = false;
            _age = 0f;
            _from = Vector2.zero;
            _target = Vector2.zero;
        }

        Vector2 Evaluate(float age)
        {
            if (age < Rise) return Vector2.LerpUnclamped(_from, _target, JuiceEase.OutQuad(age / Rise));
            float tau = age - Rise;
            return _target * ((1f + Damping * tau) * Mathf.Exp(-Damping * tau));
        }

        /// <summary>킥 모양 g (0~1): 30ms OutQuad로 오르고 임계 감쇠로 돌아온다 (테스트·확인용)</summary>
        public static float Profile(float age)
        {
            if (age <= 0f) return 0f;
            if (age < Rise) return JuiceEase.OutQuad(age / Rise);
            float tau = age - Rise;
            return (1f + Damping * tau) * Mathf.Exp(-Damping * tau);
        }
    }

    /// <summary>
    /// 줌 펀치 (§2.2): z(t) = 1 + (peak − 1)·h(t), h는 60ms OutQuad로 0 → 1, 220ms OutCubic으로 1 → 0.
    /// 새 펀치는 그 순간 지금보다 클 때만 바꾼다 (지금 크기에서 이어서 오른다). 전장(ShakeRoot)만 키운다. 순수 C#.
    /// </summary>
    public struct FxZoom
    {
        const float Rise = 0.06f;
        const float Fall = 0.22f;

        float _from;
        float _amount;
        float _age;
        bool _active;

        public bool Active => _active;

        /// <summary>지금 확대 (1 = 보통)</summary>
        public float Current => _active ? 1f + Evaluate(_age) : 1f;

        /// <summary>peak까지 확대 (지금 확대가 더 크면 무시하고 false)</summary>
        public bool Punch(float peak)
        {
            float amount = peak - 1f;
            if (!(amount > 0f)) return false;
            float current = _active ? Evaluate(_age) : 0f;
            if (_active && amount <= current) return false;
            _from = current;
            _amount = amount;
            _age = 0f;
            _active = true;
            return true;
        }

        public float Tick(float dt)
        {
            if (!_active) return 1f;
            _age += Mathf.Max(0f, dt);
            if (_age >= Rise + Fall)
            {
                _active = false;
                return 1f;
            }
            return 1f + Evaluate(_age);
        }

        public void Clear()
        {
            _active = false;
            _age = 0f;
            _from = 0f;
            _amount = 0f;
        }

        float Evaluate(float age)
        {
            if (age < Rise) return Mathf.LerpUnclamped(_from, _amount, JuiceEase.OutQuad(age / Rise));
            return _amount * (1f - JuiceEase.OutCubic((age - Rise) / Fall));
        }
    }

    /// <summary>
    /// 연출 전용 시간 배율: 보스 처치·불 밝히기 순간 전체 정지(GlobalStop) 뒤 느린 화면(SlowScale, SlowTime).
    /// Time.timeScale은 건드리지 않는다 (시뮬레이션·오프라인 계산·UI 트윈과 무관). 실제 시간 dt로 진행한다.
    /// </summary>
    public struct FxSlowMotion
    {
        float _stop;
        float _slow;
        float _scale;

        /// <summary>정지 → 느린 화면 시작 (이미 더 길게 걸려 있으면 길게 남긴다)</summary>
        public void Trigger(float stop, float slowScale, float slowTime)
        {
            _stop = Mathf.Max(_stop, stop);
            _slow = Mathf.Max(_slow, slowTime);
            _scale = Mathf.Clamp01(slowScale);
        }

        /// <summary>이번 프레임 연출 시간 배율 (0 = 멈춤, 1 = 보통)</summary>
        public float Tick(float realDt)
        {
            if (_stop > 0f)
            {
                _stop -= realDt;
                return 0f;
            }
            if (_slow > 0f)
            {
                _slow -= realDt;
                return _scale;
            }
            return 1f;
        }

        public bool Active => _stop > 0f || _slow > 0f;

        public void Clear()
        {
            _stop = 0f;
            _slow = 0f;
            _scale = 1f;
        }
    }

    /// <summary>
    /// 불 밝히기 빛 물결 시간표 (ATK-05). 점화(연출 0.2배 0.12초) 직후 수선점에서 바닥 고리가 0.5초 동안 월드 반지름 3.4
    /// (기준 화면 지름 약 1400px)까지 OutQuad로 퍼진다 — 처음엔 빠르게 터져 나가고 끝에서 잦아든다.
    /// 적마다 고리가 닿는 순간(Delay)에 명중·숫자를 띄우므로 물결이 거리순으로 적을 훑는다. 모든 시간은 연출 시계(초).
    /// </summary>
    public static class LampWave
    {
        /// <summary>점화: 연출 시간 배율 / 실제 시간</summary>
        public const float IgniteScale = 0.2f;
        public const float IgniteTime = 0.12f;
        /// <summary>점화 뒤 물결이 출발하기까지 (연출 시계) — 불이 '터지는' 순간</summary>
        public const float Start = 0.06f;
        /// <summary>물결이 끝까지 퍼지는 시간</summary>
        public const float Life = 0.5f;
        /// <summary>물결 끝 반지름 (월드 단위, 방위 링 = 1)</summary>
        public const float MaxRadius = 3.4f;
        /// <summary>
        /// 빛 번쩍임 세기 상한 / 번쩍임 줄이기 켰을 때의 상한 (소리·테스트가 읽는 규칙 값). 실제 불 밝히기 막은 이보다 옅다:
        /// BattleFx.LampLightFlash 0.28, 줄이기면 다른 빛 층과 같이 ×0.5 = 0.14 (우유빛 막이 되지 않게)
        /// </summary>
        public const float FlashPeak = 0.45f;
        public const float ReducedFlashPeak = 0.25f;
        /// <summary>여운으로 크림 별빛이 내려앉는 시간</summary>
        public const float SparkleRain = 1f;

        /// <summary>출발 뒤 elapsed초의 물결 반지름 (월드)</summary>
        public static float Radius(float elapsed)
        {
            float k = Mathf.Clamp01(elapsed / Life);
            return MaxRadius * (1f - (1f - k) * (1f - k));
        }

        /// <summary>물결이 이 거리(월드)에 닿기까지 걸리는 시간 (출발 기준, Radius의 역함수). 0.02 ~ Life</summary>
        public static float Delay(float worldDistance)
        {
            float x = Mathf.Clamp01(worldDistance / MaxRadius);
            float k = 1f - Mathf.Sqrt(1f - x);
            return Mathf.Clamp(k * Life, 0.02f, Life);
        }

        /// <summary>설정에 맞춘 빛 번쩍임 세기 상한 (불 밝히기 막은 이 아래 — BattleFx.LampLightFlash)</summary>
        public static float FlashPeakFor(bool reduceFlash) => reduceFlash ? ReducedFlashPeak : FlashPeak;
    }
}
