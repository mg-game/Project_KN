using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>적 처치 연출 갈래 (ATK-04). 처치 그림 Art/VFX/enemy_death_{dust|moth|metal|shadow}</summary>
    public enum EnemyDeathFamily : byte
    {
        /// <summary>먼지·실뭉치: 먼지 구름이 부풀며 흩어지고 크림 별빛이 위로 ("잊힘이 기억으로 풀림")</summary>
        Dust,
        /// <summary>좀나방·여왕: 날개 두 쪽이 좌우로 갈라지며 기울어 떨어지고 비늘 가루가 날린다</summary>
        Moth,
        /// <summary>녹얼룩·골무·태엽쥐: 가운데가 번쩍하고 나사·톱니·용수철이 튀어 한 번 통 튄다</summary>
        Metal,
        /// <summary>그림자·큰그림자: 그림자가 실처럼 위로 풀리고 끝의 빛 구슬이 수선점으로 흘러 들어간다</summary>
        Shadow,
    }

    /// <summary>적 공격 그림(enemy_atk_*)이 움직이는 방식 (ATK-03 적 표)</summary>
    public enum EnemyStrikeMotion : byte
    {
        /// <summary>먼지 덩어리 퍽: 닿은 자리에서 부풀며 흩어진다</summary>
        Puff,
        /// <summary>날개 가루 휘두르기: 적 → 대상 쪽으로 쓸고 지나간다</summary>
        Sweep,
        /// <summary>녹물 튀김: 톡 튀고 방울이 바닥에 떨어져 얼룩</summary>
        Splash,
        /// <summary>실 채찍: 적에서 대상까지 늘어나고(0.12초) 끝이 번쩍</summary>
        Whip,
        /// <summary>세 줄 할퀴기: 대각으로 긁고 지나간다</summary>
        Claw,
        /// <summary>금속 별 충격: 쾅 하고 쇳조각이 튄다</summary>
        Bang,
        /// <summary>태엽 스프링: 퉁 늘었다 줄고 톱니가 튄다</summary>
        Spring,
        /// <summary>그림자 파도 (보스): 크게 일어나 덮친다</summary>
        Wave,
        /// <summary>날개가루 소용돌이 (보스): 빙글 돌며 커진다</summary>
        Vortex,
    }

    /// <summary>적 하나의 공격 모양새 (크기는 기준 화면 가로 1080 픽셀)</summary>
    public readonly struct EnemyAttackLook
    {
        /// <summary>EnemyDef.id (모르는 적이면 null)</summary>
        public readonly string Id;
        /// <summary>공격 그림 이름 (Art/VFX/enemy_atk_*)</summary>
        public readonly string ArtKey;
        public readonly EnemyStrikeMotion Motion;
        public readonly EnemyDeathFamily Family;
        /// <summary>잔향이를 칠 때 그림 크기 (px)</summary>
        public readonly float EchoSize;
        /// <summary>수선점을 칠 때 그림 크기 (px)</summary>
        public readonly float ShopSize;
        /// <summary>수선점 문에서 튀는 나뭇조각(enemy_hit_shop) 크기 (px)</summary>
        public readonly float ShopHitSize;
        /// <summary>공격 그림 수명 (초)</summary>
        public readonly float Life;
        /// <summary>탁한 주색 (아군의 밝은 파스텔과 색만으로 구분되게: 회보라 먼지 · 녹갈 · 심보라 그림자)</summary>
        public readonly Color Main;
        /// <summary>곁들임 입자 색</summary>
        public readonly Color Accent;
        public readonly bool Boss;
        /// <summary>효과음 키 (AUD-01 예약)</summary>
        public readonly string Sfx;
        /// <summary>
        /// 잔향이를 칠 때 공격 그림을 다시 그리기까지 (초, 1배속 기준 — ATK-03 주기). 시뮬레이터는 막힌 적마다 같은 간격으로 알리므로
        /// 보기에서 이 주기로 솎는다 (보스 2초: 큰 파도·소용돌이가 아군 연출을 덮지 않게)
        /// </summary>
        public readonly float Interval;

        public EnemyAttackLook(string id, string artKey, EnemyStrikeMotion motion, EnemyDeathFamily family, float echoSize, float shopSize, float shopHitSize,
            float life, Color main, Color accent, bool boss, string sfx, float interval)
        {
            Id = id;
            ArtKey = artKey;
            Motion = motion;
            Family = family;
            EchoSize = echoSize;
            ShopSize = shopSize;
            ShopHitSize = shopHitSize;
            Life = life;
            Main = main;
            Accent = accent;
            Boss = boss;
            Sfx = sfx;
            Interval = interval;
        }
    }

    /// <summary>
    /// 적 공격·처치·등장 연출 목록 (ATK-03 / ATK-04 / ATK-15). 적 ID → 공격 그림 이름·움직임·크기·탁한 색, 처치 갈래.
    /// 그림은 SkillVfxLibrary(enemy_atk_*, enemy_death_*, enemy_hit_shop)에서 찾고, 없으면 BattleFx가 흰색 모양에 이 색을 입혀 대체한다.
    /// 모양새 표는 데이터만 (MonoBehaviour 없이 테스트할 수 있게 static). 문자열은 미리 만들어 둔다 (연출 중 이어 붙이기 없음).
    /// </summary>
    public static class EnemyFxCatalog
    {
        /// <summary>정식 적 9종 (BattleConfig.ApplyDefaults)</summary>
        public static readonly string[] KnownEnemies =
        {
            "enemy_dust", "enemy_moth", "enemy_rust", "enemy_tangle", "enemy_wisp", "enemy_thimble", "enemy_windup", "boss_shadow", "boss_mothqueen",
        };

        // 잔향이를 칠 때 그림 크기는 FX-JUICE E9(적 공격 비대칭)대로 110px 이하 — 몸을 16px 내미는 동작과 함께 읽히게 조금 키웠다 (예전 90~100)
        static readonly EnemyAttackLook[] Looks =
        {
            new("enemy_dust", "enemy_atk_dust", EnemyStrikeMotion.Puff, EnemyDeathFamily.Dust, 100f, 120f, 140f, 0.34f,
                Hex(0x8C82A8), Hex(0xC9BFD9), false, "enemy_puff", 1.0f),
            new("enemy_moth", "enemy_atk_moth", EnemyStrikeMotion.Sweep, EnemyDeathFamily.Moth, 106f, 120f, 140f, 0.3f,
                Hex(0x9C8AA6), Hex(0xD9C8B0), false, "enemy_flutter", 0.6f),
            new("enemy_rust", "enemy_atk_rust", EnemyStrikeMotion.Splash, EnemyDeathFamily.Metal, 106f, 140f, 140f, 0.36f,
                Hex(0xB5653A), Hex(0x7A4A2E), false, "enemy_splat", 1.4f),
            new("enemy_tangle", "enemy_atk_tangle", EnemyStrikeMotion.Whip, EnemyDeathFamily.Dust, 104f, 130f, 140f, 0.3f,
                Hex(0x9E5B7A), Hex(0xD08CB0), false, "enemy_whip", 0.9f),
            new("enemy_wisp", "enemy_atk_wisp", EnemyStrikeMotion.Claw, EnemyDeathFamily.Shadow, 106f, 120f, 140f, 0.28f,
                Hex(0x4A3B7C), Hex(0x8E6BD8), false, "enemy_scratch", 0.8f),
            new("enemy_thimble", "enemy_atk_thimble", EnemyStrikeMotion.Bang, EnemyDeathFamily.Metal, 100f, 130f, 160f, 0.3f,
                Hex(0xB8B4C8), Hex(0xE8D9A0), false, "enemy_clang", 1.2f),
            new("enemy_windup", "enemy_atk_windup", EnemyStrikeMotion.Spring, EnemyDeathFamily.Metal, 100f, 120f, 140f, 0.34f,
                Hex(0xC9A45C), Hex(0x8A9BB8), false, "enemy_boing", 1.0f),
            new("boss_shadow", "enemy_atk_boss_shadow", EnemyStrikeMotion.Wave, EnemyDeathFamily.Shadow, 220f, 320f, 220f, 0.55f,
                Hex(0x2D2458), Hex(0x9B6BE0), true, "boss_wave", 2.0f),
            new("boss_mothqueen", "enemy_atk_boss_mothqueen", EnemyStrikeMotion.Vortex, EnemyDeathFamily.Moth, 200f, 300f, 220f, 0.6f,
                Hex(0x7A5C8A), Hex(0xD9B8E0), true, "boss_vortex", 2.0f),
        };

        /// <summary>모르는 적: 먼지 덩어리처럼 (그림 이름 없음 → 흰색 모양 대체)</summary>
        static readonly EnemyAttackLook Generic = new(null, null, EnemyStrikeMotion.Puff, EnemyDeathFamily.Dust, 90f, 120f, 140f, 0.34f,
            Hex(0x8C82A8), Hex(0xC9BFD9), false, "enemy_puff", 1.0f);

        static readonly EnemyAttackLook GenericBoss = new(null, null, EnemyStrikeMotion.Wave, EnemyDeathFamily.Shadow, 220f, 320f, 220f, 0.55f,
            Hex(0x2D2458), Hex(0x9B6BE0), true, "boss_wave", 2.0f);

        static readonly string[] DeathKeys = { "enemy_death_dust", "enemy_death_moth", "enemy_death_metal", "enemy_death_shadow" };
        static readonly string[] FamilyKeys = { "dust", "moth", "metal", "shadow" };

        /// <summary>엉킨 실뭉치 처치 먼지에 섞는 와인색 (ATK-04: 먼지 그림을 0.4만큼 물들인다)</summary>
        public static readonly Color TangleWine = Hex(0x9E3F66);
        public const float TangleTint = 0.4f;

        // ── 수선점 피격 (ATK-03 공통) ──
        /// <summary>나뭇조각이 터지는 자리: 문 + 적이 들어온 방향 × 60px</summary>
        public const float ShopEdgeOffset = 60f;
        public const float ShopHitLife = 0.35f;
        /// <summary>방향 링 칸이 빨갛게 번쩍이는 시간</summary>
        public const float RingFlashTime = 0.6f;
        /// <summary>수선점 내구도가 이 비율 아래면 문에서 불씨가 피어오른다</summary>
        public const float LowHpRatio = 0.3f;

        // ── 등장 (ATK-15) ──
        public const float SpawnSwirlSize = 72f;
        public const float SpawnSwirlLife = 0.3f;
        public const float SpawnFadeTime = 0.2f;
        public const float BossPoolSize = 360f;
        public const float BossPoolLife = 0.6f;
        /// <summary>
        /// 일반 적 등장 그림자 웅덩이 (UX-SPEC-IA §6.5 CR-19): 보스 웅덩이(BossPoolSize)의 0.45배(162px)가 0.35초 동안 번지며 걷힌다.
        /// MAIN에서는 다섯 출발점이 모두 화면 안이라 몸이 거리 한가운데서 톡 나타나므로 — 발밑 어둠에서 솟아오른 것으로 읽히게
        /// </summary>
        public const float SpawnPoolScale = 0.45f;
        public const float SpawnPoolLife = 0.35f;
        /// <summary>보스가 부하를 부를 때 발밑 소용돌이</summary>
        public const float MinionVortexSize = 260f;
        public const float MinionVortexLife = 0.6f;

        // ── 처치 (ATK-04) ──
        /// <summary>보스 처치 갈래 그림 배율 (영웅 폭발 그림이 없을 때의 대체 · 갈래 곁들임 — 화면 약 360px)</summary>
        public const float BossDeathScale = 2f;
        /// <summary>일반 적 이만큼 처치할 때마다 단추가 상단 바로 날아간다 (동시에 MaxRewardCoins개까지)</summary>
        public const int RewardEvery = 5;
        public const int MaxRewardCoins = 2;
        public const float RewardCoinFlight = 0.7f;
        /// <summary>보스 처치 보상 단추 (상한과 별개, 80ms 간격)</summary>
        public const int BossRewardCoins = 3;

        // ── 처치 v2 = '풀려남' (FX-JUICE E10 / E13) ──
        /// <summary>
        /// 마지막 일격이 닿은 뒤 처치 꽃이 터지기까지 (연출 시계): 이 동안 몸이 하얗게 버티며 떨고 눌린다 (보스는 더 길게).
        /// 처치 보류 = max(예전 보류, 마지막 일격 도착 + 이 시간) — 하얀 버팀이 일격보다 먼저 시작되지 않게
        /// </summary>
        public const float KillFreeze = 0.08f;
        public const float BossKillFreeze = 0.12f;
        /// <summary>처치 보류 상한 (불 밝히기 물결이 먼 적에 닿는 시간까지 덮는다)</summary>
        public const float KillHoldLimit = 0.9f;
        /// <summary>보스 처치 버팀: 떨림 ±6px · 눌림 1.35 / 0.70 (일반은 HitFeel.KillShiver · KillSquashX/Y)</summary>
        public const float BossKillShiver = 6f;
        public const float BossKillSquashX = 1.35f;
        public const float BossKillSquashY = 0.70f;
        /// <summary>터지는 순간 몸이 고르게 부푸는 배율 (HitFeel.KillPop 40ms 동안 OutQuad)</summary>
        public const float KillPopScale = 1.15f;
        /// <summary>터진 뒤 몸이 사라지는 시간 (갈래별): 나방 120ms(날개 그림이 대신) · 쇠붙이 140ms(찌그러짐) · 먼지 160ms(InBack) · 그림자 260ms(위로 풀림)</summary>
        public const float ExitMoth = 0.12f;
        public const float ExitMetal = 0.14f;
        public const float ExitDust = 0.16f;
        public const float ExitShadow = 0.26f;
        /// <summary>기억 한 톨: 터진 뒤 150ms에 떠나 450ms 동안 수선점 문으로</summary>
        public const float MemoryMoteStart = 0.15f;
        public const float MemoryMoteFlight = 0.45f;
        /// <summary>보스 처치 영웅 층이 다 뜨려면 남아 있어야 하는 위쪽 입자 칸 (모자라면 색종이 12 · 별빛 8로 줄인다)</summary>
        public const int BossBurstParticles = 75;
        /// <summary>
        /// 보스 처치 폭발 가운데 (× B = HitFeel.BossBurstSize × U): 보이는 전장 띠 가장자리에서 0.3B 안쪽으로 당기되 보스 자리에서 0.3B 넘게는 옮기지 않는다
        /// (BossBurstCenter — 첫 프레임 반지름 0.65B 중 띠 밖으로 나가 잘리는 넓이는 22% 남짓)
        /// </summary>
        public const float BossBurstInset = 0.3f;
        public const float BossBurstMaxMove = 0.3f;
        /// <summary>
        /// 처치 빛 고리 (처치 표 6줄 · 미니 처치): 갈래 고리 색(FamilyRingColor)을 크림 쪽으로 0.45, 0.5 → 1.4 × 꽃 크기, 빛 세기 0.6 —
        /// 여럿이 한꺼번에 풀려도 똑같은 흰 비눗방울 고리가 아니라 갈래 색이 읽힌다 (예전 크림 · 1.6 · 0.8)
        /// </summary>
        public const float KillRingCream = 0.45f;
        public const float KillRingReach = 1.4f;
        public const float KillRingIntensity = 0.6f;
        /// <summary>처치 꽃이 사라지기 시작하는 수명 비율 (InQuad): 갈래 0.40 · 그림자 0.30</summary>
        public const float KillBloomFade = 0.4f;
        public const float ShadowBloomFade = 0.3f;
        /// <summary>
        /// 그림자 처치 꽃: 가산 0.20 + 라벤더 0.70 물들임 — 심보라 소용돌이가 잉크 얼룩이 아니라 빛으로 풀려난다.
        /// 가산만 주면 그림의 옅은 안개(덮인 칸의 44%가 밝기 220 이상)가 하얗게 타서 (라벤더 0.25: 가운데 거의 흰 칸 18~25% → 34~47%) 물들임으로 밝은 칸을
        /// 라벤더 크림에 머물게 한다. 가산 0.35 · 라벤더 0.60은 300px 칸 거의 흰 칸이 15~17% (+0~+50ms)로 치명타(10~14%)보다 하얬다 —
        /// 일반 처치가 치명타보다 번쩍이지 않게 가산 0.20 · 라벤더 0.70 (목표 ≤ 11%, 평균 밝기 변화는 양수로 남아 잉크로 돌아가지 않는다.
        /// 캡처 칸 다시 합성: 거의 흰 칸 4~6%, 어두운 칸(밝기 80 미만) 3~4% — 잉크였던 가산 0 · 라벤더 0.25는 6~9%)
        /// </summary>
        public const float ShadowBloomAdd = 0.2f;
        public const float ShadowBloomLavender = 0.7f;

        // ── 한꺼번에 처치 상한 (20마리를 쓸어도 처치 연출이 위쪽 입자 칸을 다 잡아 바로 다음 투사체·명중이 사라지지 않게) ──
        /// <summary>
        /// 0.2초(HitFeel.KillBurstWindow)에 미니 처치는 이만큼까지 (풀 처치 3개 다음): 높음 10 · 보통 6 · 낮음 4 (예산 420 / 240 / 130에 맞춰).
        /// 넘치면 마이크로 처치 — KillMicroScale 꽃 하나, 꼬리 여유(위쪽 20칸)도 없으면 몸의 하얀 버팀·톡(EnemyView)만으로 처치를 알린다
        /// </summary>
        public static int MiniKillsPerWindow(FxQuality quality) => quality switch
        {
            FxQuality.Low => 4,
            FxQuality.Medium => 6,
            _ => 10,
        };
        /// <summary>마이크로 처치 꽃 크기 (× K = HitFeel.KillBurstSize × U)</summary>
        public const float KillMicroScale = 0.5f;
        /// <summary>미니 처치 색종이 상한 (풀 처치는 품질 상한 10 / 6 / 4)</summary>
        public const int MiniKillConfetti = 4;
        /// <summary>
        /// 풀 처치 꽃을 그리려면 남아 있어야 하는 위쪽 입자 칸 — 모자라면 미니로 (그 아래에서는 곁들임 문(40칸)·별빛 문(60칸)이 어차피 거의 다 막아
        /// 풀 처치가 코어 층만 남는다 — 같은 칸으로 미니가 낫다)
        /// </summary>
        public const int FullKillFree = 48;
        /// <summary>처치 색종이는 이 칸 위로만 쓴다 (BattleFx 별빛 문 = 여유 60칸 초과와 같은 선)</summary>
        public const int KillSparkleReserve = 60;
        /// <summary>2배속에서 이만큼 이어서 처치하면 색종이·여운 반짝을 절반으로 (처치가 두 배로 잦아도 살아 있는 조각이 두 배가 되지 않게)</summary>
        public const int FastStreakKills = 5;

        // ── 보스 처치 · 스테이지 클리어 채움 비용 (느린 기기가 떨어지는 보통·낮음 품질 — 화면 크기 가산 사각형이 겹치면 프레임이 무너진다) ──
        /// <summary>보스 처치 고리가 1~2겹일 때의 빛 세기 (3겹은 BattleFx.BossRingIntensity 0.6 — 겹이 적으면 하나하나를 조금 밝게)</summary>
        public const float BossSparseRingIntensity = 0.75f;

        // ── 피격 반응 (FX-JUICE E9 / E12) ──
        /// <summary>눌림 용수철 5Hz · 감쇠 0.45 (112ms에 한 번 되튀고 250ms쯤 멈춘다)</summary>
        public const float SquashHz = 5f;
        public const float SquashZeta = 0.45f;
        /// <summary>치명타 뒤 물듦: 흰 번쩍임이 끝나면 공격자 강조색 알파 0.35로 60ms (번쩍임 줄이기면 없음)</summary>
        public const float BlushAlpha = 0.35f;
        public const float BlushTime = 0.06f;
        /// <summary>번쩍임 줄이기: 몸의 크림 번쩍임 세기 × 0.75 (치명타 1.0 → 0.75 · 일반 0.85 → 0.64 · 처치 버팀 1.0 → 0.75 — 치명타 물듦은 원래 꺼진다)</summary>
        public const float ReducedBodyFlash = 0.75f;
        /// <summary>HP 잔상: 버팀 → OutCubic으로 줄어듦 (일반 150 / 200ms, 보스 400 / 300ms)</summary>
        public const float GhostHold = 0.15f;
        public const float GhostDrain = 0.2f;
        public const float BossGhostHold = 0.4f;
        public const float BossGhostDrain = 0.3f;

        // ── 적 공격 몸짓 (FX-JUICE E9 비대칭) ──
        /// <summary>공격할 때 몸을 내미는 거리 (일반 16 / 보스 30px) — 앞 20%에 OutExpo로 튀어 나가고 나머지 동안 OutCubic으로 돌아온다</summary>
        public const float LungeReach = 16f;
        public const float BossLungeReach = 30f;
        public const float LungeOutShare = 0.2f;

        // ── 수선점 피격 v2 (FX-JUICE E14) ──
        /// <summary>건물 눌림 (용수철 5Hz · 감쇠 0.5): 일반 0.07 / 보스 포위 0.12</summary>
        public const float ShopHitSquash = 0.07f;
        public const float ShopSiegeSquash = 0.12f;
        public const float ShopSpringHz = 5f;
        public const float ShopSpringZeta = 0.5f;
        /// <summary>나뭇조각 수 (일반 3 / 보스 포위 7)</summary>
        public const int ShopSplinters = 3;
        public const int SiegeSplinters = 7;
        /// <summary>수선점 피격 나뭇조각 그림: 첫 프레임 ×1.12 → 끝 ×0.95 (예전 ×1.25 — 적의 일격이 우리 기본 명중보다 커 보였다)</summary>
        public const float ShopHitFirstFrame = 1.12f;
        public const float ShopHitEndScale = 0.95f;
        /// <summary>
        /// 수선점 피격 연출 크기 배율 (붉은 빛 · 나뭇조각 그림 · 먼지 퍽 · 보호막 물결 = ShopHitSize × U × 0.8): 적의 일격이 지킴이 기본 명중(2.2~2.7%)보다
        /// 크게 읽히지 않게 (공격 표의 ShopHitSize 220 / 160은 그대로). 첫 프레임 ×1.12와 함께 예전(×1.0 · 첫 프레임 ×1.25) 넓이의 약 51%.
        /// 0.9에서 먼지 수선점 피격 최대 넓이 2.49% (그중 약 0.6%p는 건물·문의 붉은 물듦 — '우리가 맞았다' 신호라 그대로) → 0.8에서 2.32%.
        /// Wave 3 (fx2 leftovers): 0.8 → 0.72 — 그림 몫 1.72% × 0.81 + 0.6 ≈ 2.0% (목표, 우리 기본 명중 2.23~2.34%와 같은 높이). 붉은 물듦은 그대로
        /// </summary>
        public const float ShopHitArtScale = 0.72f;

        // ── 보스 포위 예비 동작 ──
        /// <summary>문 앞 붉은 텔레그래프 원 알파 (circle_soft, Danger)</summary>
        public const float TelegraphAlpha = 0.35f;

        // ── 정전 빛 구멍 (ATK-14) ──
        public const float LightHoleRadius = 120f;
        public const float LightHoleTime = 0.4f;
        /// <summary>구멍 가운데의 어둠 덮개 알파 배율 (×0.4)</summary>
        public const float LightHoleDarkness = 0.4f;

        // ───────────── 조회 ─────────────

        /// <summary>적의 공격 모양새 (모르면 먼지 덩어리 / 보스면 그림자 파도)</summary>
        public static EnemyAttackLook Look(string enemyId, bool boss = false)
        {
            if (!string.IsNullOrEmpty(enemyId))
            {
                for (int i = 0; i < Looks.Length; i++)
                {
                    if (string.Equals(Looks[i].Id, enemyId, StringComparison.Ordinal)) return Looks[i];
                }
            }
            return boss ? GenericBoss : Generic;
        }

        /// <summary>표에 있는 적인지 (테스트·검사용)</summary>
        public static bool HasLook(string enemyId) => !string.IsNullOrEmpty(enemyId) && Look(enemyId).Id != null;

        /// <summary>적 → 처치 갈래: 먼지·실뭉치 = 먼지, 좀나방·여왕 = 나방, 녹·골무·태엽 = 쇠붙이, 그림자·큰그림자 = 그림자 (모르는 적은 먼지)</summary>
        public static EnemyDeathFamily Family(string enemyId) => enemyId switch
        {
            "enemy_moth" or "boss_mothqueen" => EnemyDeathFamily.Moth,
            "enemy_rust" or "enemy_thimble" or "enemy_windup" => EnemyDeathFamily.Metal,
            "enemy_wisp" or "boss_shadow" => EnemyDeathFamily.Shadow,
            _ => EnemyDeathFamily.Dust,
        };

        /// <summary>갈래 이름 (dust / moth / metal / shadow)</summary>
        public static string FamilyKey(EnemyDeathFamily family) => FamilyKeys[Mathf.Clamp((int)family, 0, FamilyKeys.Length - 1)];

        /// <summary>
        /// 적 전용 처치 그림 이름 (ATK-08 — 갈래는 그대로, 그림만): 엉킨 실뭉치 → enemy_death_yarn. 없으면 null.
        /// 그림 파일이 없으면 SkillVfxLibrary에 없으므로 갈래 그림(enemy_death_dust + 와인 틴트)을 그대로 쓴다
        /// </summary>
        public static string DeathKeyOverride(string enemyId) => enemyId == "enemy_tangle" ? "enemy_death_yarn" : null;

        /// <summary>갈래의 처치 그림 이름 (enemy_death_{갈래})</summary>
        public static string DeathArtKey(EnemyDeathFamily family) => DeathKeys[Mathf.Clamp((int)family, 0, DeathKeys.Length - 1)];

        /// <summary>등장 소용돌이·처치 가루 색 (탁한 색: 먼지 회보라 / 나방 갈보라 / 쇠붙이 녹갈 / 그림자 심보라)</summary>
        public static Color FamilyColor(EnemyDeathFamily family) => family switch
        {
            EnemyDeathFamily.Moth => new Color(0.66f, 0.58f, 0.7f, 1f),
            EnemyDeathFamily.Metal => new Color(0.69f, 0.54f, 0.42f, 1f),
            EnemyDeathFamily.Shadow => new Color(0.24f, 0.18f, 0.4f, 1f),
            _ => new Color(0.6f, 0.56f, 0.71f, 1f),
        };

        /// <summary>처치 그림에 입히는 색 (엉킨 실뭉치만 와인색 0.4, 나머지 흰색 = 그림 그대로)</summary>
        public static Color DeathTint(string enemyId) =>
            enemyId == "enemy_tangle" ? Color.Lerp(Color.white, TangleWine, TangleTint) : Color.white;

        // ───────────── 처치 v2 (FX-JUICE §1.22 갈래 표) ─────────────

        /// <summary>처치 바닥 고리 색 (§1.22): 먼지 라벤더 · 나방 더스티 핑크 · 쇠붙이 금 · 그림자 등불</summary>
        public static Color FamilyRingColor(EnemyDeathFamily family) => family switch
        {
            EnemyDeathFamily.Moth => UITheme.DustyPink,
            EnemyDeathFamily.Metal => UITheme.Gold,
            EnemyDeathFamily.Shadow => UITheme.Lamp,
            _ => UITheme.Lavender,
        };

        /// <summary>처치 빛 고리 색 = lerp(갈래 고리 색, CoreCream, 0.45) — 먼지 라벤더 · 나방 분홍 · 쇠붙이 금 · 그림자 등불이 크림빛으로 옅게</summary>
        public static Color KillRingColor(EnemyDeathFamily family) => Color.Lerp(FamilyRingColor(family), JuiceColors.CoreCream, KillRingCream);

        /// <summary>처치 꽃 가산 비율: 그림자 0.20 (빛으로 풀림) · 그 밖 0 (불투명 그림)</summary>
        public static float KillBloomAdd(EnemyDeathFamily family) => family == EnemyDeathFamily.Shadow ? ShadowBloomAdd : 0f;

        /// <summary>처치 꽃이 사라지기 시작하는 수명 비율: 그림자 0.30 · 그 밖 0.40</summary>
        public static float KillBloomFadeStart(EnemyDeathFamily family) => family == EnemyDeathFamily.Shadow ? ShadowBloomFade : KillBloomFade;

        /// <summary>처치 꽃 그림에 입히는 색: 그림자 갈래는 흰색 → 라벤더 0.70, 그 밖은 DeathTint (실뭉치 와인색 · 나머지 흰색 = 그림 그대로)</summary>
        public static Color KillBloomTint(string enemyId) =>
            Family(enemyId) == EnemyDeathFamily.Shadow ? Color.Lerp(Color.white, UITheme.Lavender, ShadowBloomLavender) : DeathTint(enemyId);

        /// <summary>몸의 크림 번쩍임 세기 (번쩍임 줄이기면 × ReducedBodyFlash 0.75)</summary>
        public static float BodyFlash(float alpha, bool reduceFlash) => reduceFlash ? alpha * ReducedBodyFlash : alpha;

        /// <summary>처치 색종이 묶음 (갈래와 같은 이름)</summary>
        public static ConfettiSet KillConfetti(EnemyDeathFamily family) => family switch
        {
            EnemyDeathFamily.Moth => ConfettiSet.Moth,
            EnemyDeathFamily.Metal => ConfettiSet.Metal,
            EnemyDeathFamily.Shadow => ConfettiSet.Shadow,
            _ => ConfettiSet.Dust,
        };

        /// <summary>처치 바닥 자국 (§1.22): 먼지·나방 반짝이 · 쇠붙이 단추 · 그림자 없음 (false)</summary>
        public static bool KillDecal(EnemyDeathFamily family, out JuiceDecal decal)
        {
            decal = family == EnemyDeathFamily.Metal ? JuiceDecal.Buttons : JuiceDecal.Glitter;
            return family != EnemyDeathFamily.Shadow;
        }

        /// <summary>터진 뒤 몸이 사라지는 시간 (나방 &lt; 쇠붙이 &lt; 먼지 &lt; 그림자)</summary>
        public static float ExitTime(EnemyDeathFamily family) => family switch
        {
            EnemyDeathFamily.Moth => ExitMoth,
            EnemyDeathFamily.Metal => ExitMetal,
            EnemyDeathFamily.Shadow => ExitShadow,
            _ => ExitDust,
        };

        /// <summary>날개 달린 갈래(나방·그림자)는 위아래로, 걷는 갈래(먼지·쇠붙이)는 좌우로 떤다 (멈춤 떨림·처치 버팀)</summary>
        public static bool ShiversVertically(EnemyDeathFamily family) => family == EnemyDeathFamily.Moth || family == EnemyDeathFamily.Shadow;

        /// <summary>
        /// 처치 보류 v2: 예전 보류(hold)와 '마지막 일격 도착(lastBlow) + 하얀 버팀(KillFreeze, 보스 BossKillFreeze)' 중 긴 쪽 (0.14 ~ 0.9초)
        /// </summary>
        public static float KillBlowHold(float hold, float lastBlow, bool boss) =>
            Mathf.Clamp(Mathf.Max(hold, Mathf.Max(0f, lastBlow) + (boss ? BossKillFreeze : KillFreeze)), AttackFxCatalog.KillHoldMin, KillHoldLimit);

        // ───────────── 한꺼번에 처치 · 큰 순간 채움 비용 — 순수 함수 ─────────────

        /// <summary>
        /// 처치 색종이 수: 상한(quota — 풀 처치 품질 상한 10 / 6 / 4, 미니 4)을 지금 여유 칸(free)에 맞춘다 = min(quota, (free − 60) / 4), 0 이상 —
        /// 별빛 문(60칸) 위 여유의 1/4만 쓰므로 처치가 몰려도 색종이가 투사체 몫을 먹지 않는다. fastStreak(2배속 5연속 이상)면 절반 (올림)
        /// </summary>
        public static int KillConfettiCount(int quota, int free, bool fastStreak)
        {
            int count = Mathf.Clamp((free - KillSparkleReserve) / 4, 0, Mathf.Max(0, quota));
            return fastStreak ? (count + 1) / 2 : count;
        }

        /// <summary>2배속(speed ≥ 2)에서 FastStreakKills(5)마리 이상 이어 처치하는 중인지 — 색종이·여운 반짝을 절반으로</summary>
        public static bool FastKillStreak(int speed, int streak) => speed >= 2 && streak >= FastStreakKills;

        /// <summary>보스 처치 충격 고리 겹 수: 높음 3 (크림 · 금 · 라벤더) · 보통 2 · 낮음 1</summary>
        public static int BossRingCount(FxQuality quality) => quality switch
        {
            FxQuality.Low => 1,
            FxQuality.Medium => 2,
            _ => 3,
        };

        /// <summary>보스 처치 충격 고리 끝 크기 (× B = HitFeel.BossBurstSize × U): 높음 3 · 보통 2.5 · 낮음 2</summary>
        public static float BossRingReach(FxQuality quality) => quality switch
        {
            FxQuality.Low => 2f,
            FxQuality.Medium => 2.5f,
            _ => 3f,
        };

        /// <summary>보스 처치 빛살(1.6B)·바닥 고리(2.4B): 낮음 품질에서는 없다 — 영웅 폭발 · 코어 · 고리 한 겹 · 섬광 · 어둡게 · 빛 번쩍임이 순간을 맡는다</summary>
        public static bool BossWideLayers(FxQuality quality) => quality != FxQuality.Low;

        /// <summary>
        /// 보스 처치 폭발 가운데 (FX-JUICE E13 · UX-SPEC-IA §6.5): 보이는 전장 띠 (x ±halfWidth · y bottom ~ top = BattleFx.FieldBottom ~ FieldTop,
        /// 시트가 열리면 시트 위 끝까지) 가장자리에서 inset만큼 안쪽으로 당기되, 보스 자리(point)에서 maxMove 넘게는 옮기지 않는다 —
        /// 위 가장자리 보스의 폭발이 보스 몸에서 떨어져 수선점 위로 내려앉지 않게 (예전 SafeTop 기준 당김은 238px 아래로 옮겼다).
        /// 띠가 inset × 2보다 좁으면 그 축의 가운데로 (옮김 상한은 그대로)
        /// </summary>
        public static Vector2 BossBurstCenter(Vector2 point, float inset, float halfWidth, float bottom, float top, float maxMove)
        {
            float r = Mathf.Max(0f, inset);
            float minX = -halfWidth + r;
            float maxX = halfWidth - r;
            if (minX > maxX) minX = maxX = 0f;
            float minY = bottom + r;
            float maxY = top - r;
            if (minY > maxY) minY = maxY = (bottom + top) * 0.5f;
            var move = new Vector2(Mathf.Clamp(point.x, minX, maxX), Mathf.Clamp(point.y, minY, maxY)) - point;
            float cap = Mathf.Max(0f, maxMove);
            if (move.sqrMagnitude > cap * cap) move *= cap / move.magnitude;
            return point + move;
        }

        /// <summary>스테이지 클리어 금빛 후광 끝 크기 (px × U): 높음 900 · 보통·낮음 560 (앱 전체 빛 번쩍임 아래에 깔리는 큰 가산 사각형)</summary>
        public static float RewardGlowSize(FxQuality quality) => quality == FxQuality.High ? 900f : 560f;

        /// <summary>스테이지 클리어 충격 고리 끝 크기 (px × U): 높음·보통 760 · 낮음 560</summary>
        public static float RewardRingSize(FxQuality quality) => quality == FxQuality.Low ? 560f : 760f;

        /// <summary>스테이지 클리어 영웅 그림 hero_stage_clear 크기 (px × U, 0에서 톡 피어 끝까지 ×1.07)</summary>
        public const float RewardHeroSize = 560f;
        /// <summary>스테이지 클리어 영웅 그림의 위쪽 반 (× RewardHeroSize × U) — 리본 아래 끝에서 적어도 이만큼 아래에 가운데를 둔다</summary>
        public const float RewardHeroInset = 0.36f;
        /// <summary>
        /// 보이는 띠 위 끝에서 스테이지 클리어 리본 아래 끝까지 (px): 리본 가운데 RibbonDrop 293 + 반높이 170 × 0.6 / 2 = 51 → 344
        /// (UX-SPEC-IA §8.3 M67 — 화면 372~474, 두 상태 같은 자리). WP-5A의 배너 상수를 그대로 읽어 리본이 옮겨 가면 따라간다
        /// </summary>
        public static float RewardRibbonBottom => BattleBannerView.RibbonDrop + BattleBannerView.BannerSize.y * BattleBannerView.RibbonScale * 0.5f;
        /// <summary>스테이지 클리어 가운데가 보이는 띠 아래 끝에서 적어도 이만큼 위 (× RewardHeroSize × U — 짧은 화면 지킴, 16:9에서는 걸리지 않는다)</summary>
        public const float RewardFloorInset = 0.25f;

        /// <summary>
        /// 스테이지 클리어 가운데 높이 (전장 로컬 y): 영웅 그림(등불)이 리본 아래에 오게 보이는 띠 위 끝(top = BattleFx.FieldTop)에서
        /// 344 + 0.36 × 560px × U 아래까지로 누른다 (Wave 3 · fx2 leftovers: 예전 0.38 × 560 아래는 MAIN에서 화면 374 — '스테이지 클리어!' 리본(372~474)과
        /// 추천 알약(142~222) 뒤에 등불이 0.8초 내내 숨었다). 이제 두 상태 모두 화면 ≈ 676 (MAIN 전장 y 186 · SHEET −174, 아래 잘림 ≈ 11%):
        /// 등불은 수선점 위 광장에서 피어오르고, 후광 · 고리 · 색종이 · 반짝 분수가 그 자리를 따르며 보상 단추는 그대로 상단 바로 난다
        /// </summary>
        public static float RewardCenterY(float y, float top, float unit) =>
            Mathf.Min(y, top - (RewardRibbonBottom + RewardHeroInset * RewardHeroSize * unit));

        // ───────────── 전체 정지 동안 액터 자리 (FX-01) — 순수 함수 ─────────────

        /// <summary>붙잡았던 자리를 따라잡는 빠르기 (1/s, 연출 시계): 55ms에 63% · 0.17초에 95%</summary>
        public const float ActorCatchUp = 18f;
        /// <summary>붙잡는 거리 상한 (전장 px) — 느린 화면이 길게 이어져도 몸이 시뮬레이터 자리에서 너무 멀어지지 않게</summary>
        public const float ActorHoldMax = 96f;

        /// <summary>
        /// 전체 정지·느린 화면 동안 액터 자리 붙잡기: 연출 시간(입자·걷기 흔들림·눌림)만 멈추고 시뮬레이터는 계속 걸으므로,
        /// 이번 프레임에 시뮬레이터가 움직인 양(simDelta) 중 (1 − timeScale)만큼을 붙잡아 몸도 함께 멈추거나 느려진다 (미끄러지지 않게).
        /// timeScale이 1로 돌아오면 붙잡은 거리를 ActorCatchUp으로 따라잡는다 (dt = 연출 시계). 돌려주는 값이 새 offset —
        /// 보이는 자리 = 시뮬레이터 자리 + offset (|offset| ≤ ActorHoldMax)
        /// </summary>
        public static Vector2 HoldOffset(Vector2 offset, Vector2 simDelta, float timeScale, float dt)
        {
            float hold = 1f - Mathf.Clamp01(timeScale);
            if (hold > 0.001f) offset -= simDelta * hold;
            else
            {
                if (offset.sqrMagnitude < 0.0025f) return Vector2.zero;
                offset *= Mathf.Exp(-ActorCatchUp * Mathf.Max(0f, dt));
            }
            return offset.sqrMagnitude > ActorHoldMax * ActorHoldMax ? offset.normalized * ActorHoldMax : offset;
        }

        /// <summary>
        /// 뷰가 몸 자리를 붙잡을 배율 (perf PF-2 F2) = HoldOffset의 timeScale 인자. 시뮬레이션이 이미 simScale로 느려졌으므로(FX-01 PresentationScale)
        /// 연출 배율 fxScale 가운데 시뮬레이션이 덜 느려진 몫만 붙잡는다: fx 0.3 · sim 0.3 → 1 (붙잡지 않음 — 느린 화면이 두 번 걸리지 않는다),
        /// fx 0 · sim 0.05(바닥) → 0 (전체 정지는 그대로 멈춤), fx 0.3 · sim 1 (배율이 바뀐 첫 프레임) → 0.3
        /// </summary>
        public static float HoldScale(float fxScale, float simScale) =>
            Mathf.Clamp01(Mathf.Clamp01(fxScale) / Mathf.Max(0.05f, simScale));

        // ───────────── 피격 반응 · 적 공격 (FX-JUICE E9) — 순수 함수 ─────────────

        /// <summary>
        /// 대상 정지 예산 (새는 양동이): 지금 쌓인 양(load)에서 요청(request)을 얼마나 내줄지 = min(request, 0.15 − load), 0 이상.
        /// 쌓인 양은 초당 FreezeLeak(0.3)씩 샌다 — 어느 0.5초에도 멈춤은 30% 남짓까지
        /// </summary>
        public static float FreezeGrant(float load, float request) => Mathf.Clamp(Mathf.Min(request, HitFeel.FreezeBudget - load), 0f, Mathf.Max(0f, request));

        /// <summary>정지 예산이 새는 빠르기 (초당) = FreezeBudget / FreezeWindow</summary>
        public const float FreezeLeak = HitFeel.FreezeBudget / HitFeel.FreezeWindow;

        /// <summary>
        /// 공격 내밀기 모양 (progress 0 → 1, 시뮬레이터 AttackAnim 1 → 0): 앞 20%에 OutExpo로 끝까지 튀어 나가고, 나머지 동안 OutCubic으로 돌아온다 (0~1)
        /// </summary>
        public static float LungeProfile(float progress)
        {
            float p = Mathf.Clamp01(progress);
            if (p <= 0f || p >= 1f) return 0f;
            return p < LungeOutShare ? JuiceEase.OutExpo(p / LungeOutShare) : 1f - JuiceEase.OutCubic((p - LungeOutShare) / (1f - LungeOutShare));
        }

        /// <summary>HP 잔상 비율: 맞은 뒤 age초 — 버티는 동안 from 그대로, 그 뒤 OutCubic으로 to까지 (보스는 더 오래 버티고 천천히)</summary>
        public static float GhostRatio(float from, float to, float age, bool boss)
        {
            float hold = boss ? BossGhostHold : GhostHold;
            if (age <= hold || from <= to) return Mathf.Max(from, to);
            float drain = boss ? BossGhostDrain : GhostDrain;
            return Mathf.Lerp(from, to, JuiceEase.OutCubic((age - hold) / drain));
        }

        /// <summary>
        /// 적 공격 그림 알파: 먼지 폭풍이면 ×0.85 (아군 연출이 먼저 읽히게, ATK-14), 숨어 있는 그림자(Phasing)는 반투명 ×0.5
        /// </summary>
        public static float StrikeAlpha(StageModifierType modifier, bool hidden) =>
            (modifier == StageModifierType.DustStorm ? 0.85f : 1f) * (hidden ? 0.5f : 1f);

        /// <summary>수선점 피격 흔들림: 보스 포위 0.55 / 일반 0.30 (일반은 수선점 채널 초당 0.6까지, 보호막에 다 막히면 절반)</summary>
        public static float ShopStrikeTrauma(bool boss, bool absorbed) => (boss ? HitFeel.BossSiegeTrauma : HitFeel.ShopHitTrauma) * (absorbed ? 0.5f : 1f);

        /// <summary>
        /// 수선점 피격 칸 (적이 들어온 방향 = 맞은 자리의 월드 각도에 가장 가까운 방위 슬롯). 위치가 원점이면 -1
        /// </summary>
        public static int ShopStrikeSlot(Vector2 worldPosition)
        {
            if (worldPosition.sqrMagnitude < 1e-6f) return -1;
            return BattleGeometry.NearestSlot(Mathf.Atan2(worldPosition.y, worldPosition.x));
        }

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    /// <summary>
    /// 시각 창 상한 (작은 고리 버퍼, 순수 C# — 만들 때 한 번만 할당): 시각 at 앞뒤 window 안에 이미 기록된 수가 limit보다 적으면 at을 기록하고 true.
    /// 처치는 터지는 순서와 부르는 순서가 다르므로(처치 보류 0.14 ~ 0.9초) 앞뒤를 함께 센다 (FxGovernor.TryKillBurst와 같은 셈) — 미니 처치 상한
    /// </summary>
    public sealed class KillWindow
    {
        const float Never = -9999f;
        readonly float[] _times;
        int _head;

        public KillWindow(int slots)
        {
            _times = new float[Mathf.Max(1, slots)];
            Clear();
        }

        public bool TryAdd(float at, float window, int limit)
        {
            int near = 0;
            for (int i = 0; i < _times.Length; i++)
            {
                if (Mathf.Abs(_times[i] - at) < window) near++;
            }
            if (near >= limit) return false;
            _times[_head] = at;
            _head = (_head + 1) % _times.Length;
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _times.Length; i++) _times[i] = Never;
            _head = 0;
        }
    }
}
