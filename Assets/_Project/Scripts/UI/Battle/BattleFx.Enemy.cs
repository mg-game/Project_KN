using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>적 공격 연출 한 번의 입력 (전장 로컬 좌표, 픽셀)</summary>
    public struct EnemyStrikeRequest
    {
        /// <summary>EnemyDef.id (공격 그림·움직임·크기를 고른다)</summary>
        public string EnemyId;
        public bool Boss;
        /// <summary>때린 적 몸 가운데 (채찍이 뻗어 나오는 자리, 휘두르는 방향의 시작)</summary>
        public Vector2 From;
        /// <summary>맞은 자리 (잔향이 몸 가운데 / 수선점 문 가장자리)</summary>
        public Vector2 To;
        /// <summary>맞은 잔향이 (수선점이면 null) — 공격 그림이 따라간다</summary>
        public EchoView Echo;
        /// <summary>수선점 공격 (그림이 크다)</summary>
        public bool Shop;
        /// <summary>숨어 있는 그림자의 공격 (반투명)</summary>
        public bool Hidden;
        /// <summary>바닥 높이 (튀는 방울·조각이 떨어질 곳, 전장 로컬 y)</summary>
        public float Ground;
    }

    /// <summary>적 처치 연출 한 번의 입력 (전장 로컬 좌표)</summary>
    public struct EnemyDeathRequest
    {
        public string EnemyId;
        /// <summary>몸 가운데 (처치 그림이 피는 자리)</summary>
        public Vector2 Point;
        /// <summary>발밑</summary>
        public Vector2 Feet;
        /// <summary>그림이 없을 때 쓰는 몸 색</summary>
        public Color BodyColor;
        public bool Boss;
        /// <summary>처치 꽃이 터지는 순간까지 (EnemyView.KillDelay = 마지막 일격이 닿고 하얗게 버틴 뒤)</summary>
        public float Delay;
        /// <summary>수선점 문 (기억 한 톨이 흘러 들어가는 곳)</summary>
        public Vector2 Shop;
        /// <summary>처치되는 몸 그림 (EnemyView.Body — 보스 처치 순간 크림 실루엣·인쇄 어긋남이 이 모양을 따른다). 없으면 null</summary>
        public Graphic Body;
        /// <summary>처치한 일격의 등급 (EnemyView.KilledBy — 치명타·스킬 처치는 화면 킥)</summary>
        public HitTier KilledBy;
    }

    /// <summary>
    /// 적 쪽 연출 (ATK-03 적 공격·수선점 피격 / ATK-04 처치 4갈래 / ATK-15 등장 / ATK-14 기믹에서도 읽히게 — FX-JUICE v2 E10 · E13 · E14 · E18 · E19).
    /// 아군 연출은 밝은 파스텔과 빛(가산), 적 연출은 탁한 색(회보라 먼지 · 녹갈 쇠붙이 · 심보라 그림자)에 가산·흰 코어·금색 없이 —
    /// 붐벼도 색과 밝기만으로 누가 때렸는지 읽힌다. 그림(enemy_atk_*, enemy_death_*, enemy_hit_shop, Juice kill_* / hero_*)이 없으면
    /// 흰색 모양(VfxMotif)에 색을 입혀 대체한다 — 예외 없음.
    ///   적 공격: 적마다 움직임이 다르다 (먼지 퍽 · 날개 가루 휘두르기 · 녹물 튀김 · 실 채찍 · 세 줄 할퀴기 · 금속 별 쾅 · 태엽 퉁 · 보스 그림자 파도 · 날개가루 소용돌이).
    ///   수선점 피격 (E14): 붉은 빛(알파) → 나뭇조각 그림이 첫 프레임 ×1.12로 터져 바깥으로 밀리고 → 나뭇조각이 문 아래 바닥에 튄다 → 먼지 퍽
    ///     (모두 ShopHitSize × 0.8 — 우리 기본 명중보다 작게). 흔들림은 수선점 채널(초당 0.6)로 문 쪽, 킥 2px(보스 포위 흔들림 0.55 + 킥 4px),
    ///     건물은 용수철로 눌렸다 되튀고 설정 흔들림 배율만큼 떤다.
    ///   처치 = '풀려남' (E10): 잉크 받침 · 바닥 고리 · 후광 · 처치 꽃(kill_{갈래}, 첫 프레임 ×1.35 → 끝까지 ×1.30으로 부풂) · 코어 별 · 갈래 빛 고리 ·
    ///     갈래 곁들임 · 갈래 색종이 · 여운 반짝 · 기억 한 톨(수선점 문으로) · 바닥 자국. 0.2초에 풀 처치 꽃이 3개를 넘으면 미니 처치, 미니도 10 / 6 / 4개
    ///     (높음 / 보통 / 낮음)를 넘으면 마이크로 처치(0.5K 꽃 하나) — 한꺼번에 쓸어도 처치 연출이 투사체·명중 몫의 입자 칸을 먹지 않는다 (색종이는 여유 칸에 맞춰 줄고, 2배속 연속 처치는 절반).
    ///     치명타·스킬 처치는 수선점 바깥쪽으로 킥 2px. 10연속 이상이면 금빛 색종이가 섞인다.
    ///   보스 처치 (E13): 버팀 동안 전장이 0.2까지 어두워지고, 터지는 순간 420px 영웅 폭발(hero_boss_kill) · 빛살 · 속도선 · 코어 · 섬광 · 고리 3겹 ·
    ///     색종이 · 리본 · 별빛 · 여운 별 · 기억 한 톨 · 보상 단추 3 + 코어의 결정적 순간(전체 정지 · 임팩트 프레임 + 보스 크림 실루엣 · 빛 번쩍임 · 줌 · 진동).
    ///     폭발은 보스 몸에 붙어 있다 — 보이는 띠 가장자리에서 0.3B 안쪽으로만 당기고 0.3B 넘게 옮기지 않는다.
    ///     보통 품질은 고리 2겹(2.5B), 낮음은 1겹(2B)에 빛살·바닥 고리 없이 (느린 기기의 채움 비용).
    ///   스테이지 클리어 (E18): 금빛 후광 · 영웅 그림(hero_stage_clear) · 충격 고리 · 축하 색종이 · 반짝 분수 · 보상 단추 + 줌 1.015 (흔들림 없음).
    ///     후광은 보통·낮음에서 560px까지, 고리는 낮음에서 560px까지.
    ///   등장 (E19 · UX-SPEC-IA CR-19): 일반 적은 발밑 그림자 웅덩이(보스 웅덩이 0.45배, 0.35초) · 잉크 물결 · 갈래 색 소용돌이 · 피어오르는 어둠에서 솟는다.
    /// 입자가 모자라면 곁들임부터 버리고 공격·처치 그림은 남긴다 (장식 조각은 별빛 층 — 영웅 층 몫을 쓰지 않는다).
    /// 시계는 연출 시계(_clock)/실제 시간(_realClock)만 쓴다 (Time.time 없음).
    /// </summary>
    public partial class BattleFx
    {
        static readonly Color WoodBrown = new(0.79f, 0.55f, 0.31f, 1f);
        static readonly Color RustOrange = new(0.71f, 0.4f, 0.23f, 1f);
        static readonly Color GnawRed = new(0.88f, 0.38f, 0.48f, 1f);
        static readonly Color ScaleDust = new(0.85f, 0.79f, 0.7f, 1f);
        static readonly Color SteelGray = new(0.75f, 0.74f, 0.82f, 1f);
        /// <summary>보스 처치 고리 3겹 (크림 → 금 → 라벤더, 60ms 간격)</summary>
        static readonly Color[] BossRingColors = { JuiceColors.CoreCream, UITheme.Gold, UITheme.Lavender };
        /// <summary>보스 처치 리본 색</summary>
        static readonly Color[] RibbonColors = { UITheme.DustyPink, UITheme.Lavender, UITheme.Gold };

        // ── 처치 · 보스 처치 세기 (FX-JUICE E10 / E13 — 캡처로 맞추는 상수) ──
        /// <summary>
        /// 처치 꽃: 첫 프레임 ×1.35, 33ms 버팀, 70ms에 자리 크기로, 끝까지 ×1.30으로 부푼다 (수명 0.42초, 40%부터 사라짐 —
        /// 그림자 갈래는 빛(가산 0.20)으로 30%부터: EnemyFxCatalog.KillBloomAdd / KillBloomFadeStart)
        /// </summary>
        const float KillBloomFirst = 1.35f;
        const float KillBloomHold = 0.033f;
        const float KillBloomSettle = 0.07f;
        const float KillBloomEnd = 1.30f;
        const float KillBloomLife = 0.42f;
        /// <summary>처치 후광 세기 (가산, 1.8K)</summary>
        const float KillHaloIntensity = 0.35f;
        /// <summary>미니 처치 꽃 크기 (× K)</summary>
        const float KillMiniScale = 0.6f;
        /// <summary>이만큼 연속으로 처치하면 금빛 색종이가 섞인다</summary>
        const int GoldStreak = 10;
        /// <summary>보스 영웅 폭발: 첫 프레임 ×1.30, 50ms 버팀(전체 정지 동안 그대로), 80ms에 자리 크기로, 끝까지 ×1.25로 부푼다 (0.9초)</summary>
        const float BossBurstFirst = 1.30f;
        const float BossBurstHold = 0.05f;
        const float BossBurstSettle = 0.08f;
        const float BossBurstEnd = 1.25f;
        const float BossBurstLife = 0.9f;
        /// <summary>
        /// 보스 빛살 (rays.png): 가산 비율 / 세기 — 스펙(가산 0.35, 불투명 1)은 노란 막처럼 칠해져 코어가 불 밝히기에서 낮춘 것과 같은 결로
        /// 순수 빛(가산 1)에 세기 0.5 (칠하지 않고 밝힌다, 불 밝히기 빛살 0.55보다 약하게)
        /// </summary>
        const float BossRaysAdd = 1f;
        const float BossRaysIntensity = 0.5f;
        /// <summary>보스 속도선 가산 비율</summary>
        const float BossSpeedlinesAdd = 0.55f;
        /// <summary>
        /// 보스 고리 3겹 빛 세기 — 스펙 0.8은 3B(약 1260px)까지 퍼진 세 겹이 겹쳐 전장을 가로지르는 흰 띠가 되어(캡처 +500ms 평균 밝기 +68%)
        /// 물결이 하나하나 읽히도록 낮췄다
        /// </summary>
        const float BossRingIntensity = 0.6f;
        /// <summary>보스 처치: 버팀 동안 전장이 이만큼 어두워진다 (실제 시간, 터지는 순간 임팩트 프레임 0.45가 이어받는다)</summary>
        const float BossPreDim = 0.2f;

        [Header("적 연출")]
        [Tooltip("처치 보상으로 상단 바로 날아가는 단추 그림 (Art/Icons/currency_gold, 없으면 단추 모양)")]
        [SerializeField] Sprite _coinSprite;

        int _coinsInFlight;
        /// <summary>날아가는 처치 단추가 모두 도착하는 실제 시각 (_realClock)</summary>
        float _coinClear;
        /// <summary>미니 처치 꽃이 터지는 시각 (연출 시계 — 0.2초에 EnemyFxCatalog.MiniKillsPerWindow(품질)개까지, 넘치면 마이크로 처치)</summary>
        readonly KillWindow _miniKills = new(32);
        /// <summary>보스 포위 텔레그래프 바닥 원 / 바깥 고리 입자 칸 (없으면 -1). 매 프레임 SiegeTelegraph가 크기·알파를 맞춘다</summary>
        int _telegraphDisc = -1;
        int _telegraphRing = -1;
        /// <summary>텔레그래프 입자의 수명 표식 (스스로 끝나지 않게 아주 길게 — 끝은 EndSiegeTelegraph가 정한다)</summary>
        const float TelegraphHoldLife = 9999f;
        /// <summary>끊기거나 공격한 뒤 원이 걷히는 시간</summary>
        const float TelegraphFadeTime = 0.1f;

        // ───────────── 적 공격 (ATK-03) ─────────────

        /// <summary>
        /// 적이 잔향이·수선점을 때리는 순간 (EnemyStrike): 적 종류별 공격 그림이 맞은 자리에서 종류별 움직임으로 터진다.
        /// 먼지 폭풍에서는 알파 ×0.85, 숨은 그림자는 반투명. 적 연출은 탁한 색 그대로 (가산·흰 코어·금색 없음 — FX-JUICE E9 비대칭)
        /// </summary>
        public void EnemyStrike(in EnemyStrikeRequest r)
        {
            if (!_ready) Prepare();
            var look = EnemyFxCatalog.Look(r.EnemyId, r.Boss);
            float size = (r.Shop ? look.ShopSize : look.EchoSize) * UnitScale;
            float alpha = EnemyFxCatalog.StrikeAlpha(_stageModifier, r.Hidden);
            float life = look.Life * PopScale;
            var dir = r.To - r.From;
            if (dir.sqrMagnitude < 1f) dir = r.Shop ? Vector2.down : Vector2.right;
            dir.Normalize();
            var at = r.To;
            ClampToField(ref at);
            var art = _library != null ? _library.EnemyAttack(look.Id ?? r.EnemyId) : null;

            switch (look.Motion)
            {
                case EnemyStrikeMotion.Sweep:
                    StrikeSweep(art, look, at, dir, size, life, alpha, r.Echo);
                    break;
                case EnemyStrikeMotion.Splash:
                    StrikeSplash(art, look, at, size, life, alpha, r.Ground, r.Echo);
                    break;
                case EnemyStrikeMotion.Whip:
                    StrikeWhip(art, look, r.From, at, dir, size, life, alpha);
                    break;
                case EnemyStrikeMotion.Claw:
                    StrikeClaw(art, look, at, size, life, alpha, r.Echo);
                    break;
                case EnemyStrikeMotion.Bang:
                    StrikeBang(art, look, at, size, life, alpha, r.Echo);
                    break;
                case EnemyStrikeMotion.Spring:
                    StrikeSpring(art, look, at, size, life, alpha, r.Echo);
                    break;
                case EnemyStrikeMotion.Wave:
                    StrikeWave(art, look, at, dir, size, life, alpha, r.Ground);
                    break;
                case EnemyStrikeMotion.Vortex:
                    StrikeVortex(art, look, at, size, life, alpha);
                    break;
                default:
                    StrikePuff(art, look, at, dir, size, life, alpha, r.Echo);
                    break;
            }
        }

        /// <summary>먼지 덩어리 퍽: 맞은 자리에서 부풀며 맞은 쪽으로 밀리고, 회보라 먼지가 흩어진다</summary>
        void StrikePuff(Sprite art, in EnemyAttackLook look, Vector2 at, Vector2 dir, float size, float life, float alpha, EchoView echo)
        {
            int pop = StrikeArt(art, VfxMotif.Soft, look, at, size * 0.55f, size, life, alpha);
            if (pop >= 0)
            {
                ref var p = ref _particles[pop];
                p.Curve = SizeCurve.Pop;
                p.P1 = dir * 12f * UnitScale;
                p.Rotation = Random.Range(-15f, 15f);
            }
            AnchorTo(pop, echo);
            if (!Allow(FxPriority.Extra)) return;
            for (int i = 0; i < 3; i++)
            {
                var spread = RandomDirection();
                int puff = Drift(Puff, BattleEase.WithAlpha(look.Main, 0.5f * alpha), at + spread * 10f, (spread + dir * 0.6f) * Random.Range(30f, 60f),
                    Vector2.up * 16f, size * 0.25f, size * 0.6f, Random.Range(0.35f, 0.5f));
                if (puff >= 0) _particles[puff].FadeIn = 0.05f;
            }
        }

        /// <summary>날개 가루 휘두르기: 날개가 적 → 대상 쪽으로 60° 쓸고 지나가며 비늘 가루가 흩날린다</summary>
        void StrikeSweep(Sprite art, in EnemyAttackLook look, Vector2 at, Vector2 dir, float size, float life, float alpha, EchoView echo)
        {
            int wing = StrikeArt(art, VfxMotif.Scale, look, at - dir * 14f * UnitScale, size * 0.85f, size, life, alpha);
            if (wing >= 0)
            {
                ref var p = ref _particles[wing];
                p.Curve = SizeCurve.Snap;
                p.P1 = dir * 28f * UnitScale;
                // 그림 속 날개는 오른쪽 위(45°)를 향한다 — 맞는 쪽으로 기울여 -30° → +30° 쓸어 준다
                FaceArt(wing, dir, -45f - 30f);
                p.Spin = (p.Rect.localScale.x < 0f ? -60f : 60f) / Mathf.Max(0.05f, life);
            }
            AnchorTo(wing, echo);
            if (!Allow(FxPriority.Sparkle)) return;
            var scale = Motif(VfxMotif.Scale);
            for (int i = 0; i < 5; i++)
            {
                var spread = RandomDirection();
                int dust = Drift(scale, BattleEase.WithAlpha(i % 2 == 0 ? ScaleDust : look.Main, 0.85f * alpha), at + spread * 14f,
                    (dir * 0.8f + spread) * Random.Range(24f, 50f), Vector2.down * 60f, Random.Range(10f, 15f) * UnitScale, 6f, Random.Range(0.45f, 0.6f));
                if (dust < 0) continue;
                _particles[dust].Wobble = 6f;
                _particles[dust].Spin = Random.Range(-200f, 200f);
            }
        }

        /// <summary>녹물 튀김: 톡 튀고 녹물 방울이 바닥에 떨어져 얼룩이 진다</summary>
        void StrikeSplash(Sprite art, in EnemyAttackLook look, Vector2 at, float size, float life, float alpha, float ground, EchoView echo)
        {
            int pop = StrikeArt(art, VfxMotif.Splat, look, at, size * 0.5f, size, life, alpha);
            if (pop >= 0)
            {
                _particles[pop].Curve = SizeCurve.Back;
                _particles[pop].Rotation = Random.Range(-25f, 25f);
            }
            AnchorTo(pop, echo);
            if (!Allow(FxPriority.Extra)) return;
            var splat = Motif(VfxMotif.Splat);
            float floor = ground != 0f ? ground : at.y - 50f * UnitScale;
            for (int i = 0; i < 4; i++)
            {
                float side = (i / 3f) * 2f - 1f;
                var start = at + new Vector2(side * 10f, Random.Range(-4f, 6f)) * UnitScale;
                var velocity = new Vector2(side * Random.Range(60f, 110f), Random.Range(80f, 160f)) * UnitScale;
                float drop = Mathf.Max(0f, start.y - floor);
                const float gravity = 900f;
                float fall = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * drop)) / gravity;
                int bead = Spawn(splat, BattleEase.WithAlpha(i % 2 == 0 ? look.Main : look.Accent, alpha), start, 12f * UnitScale, 9f * UnitScale, fall, 0f, false, true);
                if (bead >= 0)
                {
                    ref var p = ref _particles[bead];
                    p.Motion = Motion.Bounce;
                    p.P1 = velocity;
                    p.P2 = new Vector2(floor, gravity);
                    p.P3 = Vector2.zero;
                    p.FadeIn = 0.02f;
                    p.FadeOut = 0.95f;
                    p.Spin = Random.Range(-200f, 200f);
                }
                var landing = new Vector2(start.x + velocity.x * fall, floor);
                int stain = Spawn(splat, BattleEase.WithAlpha(look.Accent, 0.7f * alpha), landing, 10f * UnitScale, 26f * UnitScale, 0.55f, fall, true);
                if (stain < 0) continue;
                ref var s = ref _particles[stain];
                s.Aspect = GroundAspect * 0.55f;
                s.FadeIn = 0.02f;
                s.FadeOut = 0.45f;
            }
        }

        /// <summary>실 채찍: 적에서 대상까지 0.12초 동안 늘어나고 끝이 탁한 강조색으로 번쩍인다 (그림 왼쪽 끝 = 손잡이 — 흰 코어 없음)</summary>
        void StrikeWhip(Sprite art, in EnemyAttackLook look, Vector2 from, Vector2 at, Vector2 dir, float size, float life, float alpha)
        {
            float length = Mathf.Clamp((at - from).magnitude * 1.08f, 40f, size * 2.6f);
            var root = at - dir * length / 1.08f;
            int whip = Spawn(art != null ? art : _trail, art != null ? BattleEase.WithAlpha(Color.white, alpha) : BattleEase.WithAlpha(look.Main, 0.9f * alpha),
                root, length * 0.08f, length, life, 0f);
            if (whip >= 0)
            {
                ref var p = ref _particles[whip];
                p.Curve = SizeCurve.Snap;
                p.Height0 = size * 0.55f;
                p.Height1 = size * 0.62f;
                p.FadeIn = 0.01f;
                p.FadeOut = 0.6f;
                p.Rect.pivot = new Vector2(0.04f, 0.5f);
                p.Rotation = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
            }
            // 끝이 닿는 순간 (늘어나는 시간 = 수명의 30%) 탁한 와인빛으로 짧게 번쩍
            float snap = life * 0.3f;
            Flash(at, _glow, BattleEase.WithAlpha(look.Accent, 0.55f * alpha), 20f, size * 0.7f, 0.2f, snap);
            int spark = Flash(at, _sparkle, BattleEase.WithAlpha(look.Accent, alpha), size * 0.6f, 10f, 0.22f, snap);
            if (spark >= 0) _particles[spark].Spin = 320f;
        }

        /// <summary>세 줄 할퀴기: 대각으로 긁고 지나간다 (그림자 갈래 — 어둠이 조금 피어오른다)</summary>
        void StrikeClaw(Sprite art, in EnemyAttackLook look, Vector2 at, float size, float life, float alpha, EchoView echo)
        {
            var slide = new Vector2(0.7f, -0.7f) * 22f * UnitScale;
            int claw = StrikeArt(art, VfxMotif.Trail, look, at - slide * 0.5f, size * 0.7f, size, life, alpha);
            if (claw >= 0)
            {
                ref var p = ref _particles[claw];
                p.Curve = SizeCurve.Snap;
                p.P1 = slide;
                p.Rotation = Random.Range(-10f, 10f);
                if (art == null) p.Aspect = 0.3f;
            }
            AnchorTo(claw, echo);
            if (!Allow(FxPriority.Extra)) return;
            for (int i = 0; i < 2; i++)
            {
                int wisp = Drift(Puff, BattleEase.WithAlpha(ShadowPurple, 0.45f * alpha), at + new Vector2(Random.Range(-18f, 18f), 0f),
                    new Vector2(Random.Range(-12f, 12f), Random.Range(30f, 50f)), Vector2.up * 10f, size * 0.2f, size * 0.45f, 0.45f, 0.04f);
                if (wisp >= 0) _particles[wisp].Wobble = 5f;
            }
        }

        /// <summary>금속 별 충격: 탁한 쇳빛으로 쾅 하고 (흰 코어 없음) 쇳조각이 튄다</summary>
        void StrikeBang(Sprite art, in EnemyAttackLook look, Vector2 at, float size, float life, float alpha, EchoView echo)
        {
            Flash(at, _glow, BattleEase.WithAlpha(Color.Lerp(look.Main, SteelGray, 0.5f), 0.55f * alpha), size * 0.2f, size * 0.7f, 0.14f);
            int bang = StrikeArt(art, VfxMotif.Star, look, at, size * 0.45f, size, life, alpha);
            if (bang >= 0)
            {
                _particles[bang].Curve = SizeCurve.Back;
                _particles[bang].Rotation = Random.Range(-20f, 20f);
            }
            AnchorTo(bang, echo);
            if (!Allow(FxPriority.Extra)) return;
            var shard = Motif(VfxMotif.Shard);
            for (int i = 0; i < 4; i++)
            {
                var spread = RandomDirection();
                int bit = Drift(shard, BattleEase.WithAlpha(i % 2 == 0 ? SteelGray : look.Accent, alpha), at + spread * 8f, spread * Random.Range(60f, 100f) * UnitScale,
                    Vector2.down * 80f, Random.Range(12f, 16f) * UnitScale, 6f, Random.Range(0.35f, 0.45f));
                if (bit >= 0) _particles[bit].Spin = Random.Range(-500f, 500f);
            }
        }

        /// <summary>태엽 스프링: 퉁 하고 늘었다 줄며 작은 톱니가 튄다</summary>
        void StrikeSpring(Sprite art, in EnemyAttackLook look, Vector2 at, float size, float life, float alpha, EchoView echo)
        {
            int coil = StrikeArt(art, VfxMotif.Spring, look, at, size * 0.6f, size, life, alpha);
            if (coil >= 0)
            {
                ref var p = ref _particles[coil];
                p.Curve = SizeCurve.Back;
                p.Rotation = Random.Range(-12f, 12f);
                // 퉁: 세로로 늘었다가 눌린다
                p.Aspect = 1.3f;
                p.AspectEnd = 0.85f;
            }
            AnchorTo(coil, echo);
            if (!Allow(FxPriority.Extra)) return;
            var gear = Motif(VfxMotif.Gear);
            for (int i = 0; i < 3; i++)
            {
                var spread = RandomDirection();
                spread.y = Mathf.Abs(spread.y) * 0.7f + 0.3f;
                int bit = Drift(gear, BattleEase.WithAlpha(i == 1 ? look.Accent : look.Main, alpha), at, spread * Random.Range(50f, 90f) * UnitScale,
                    Vector2.down * 110f, Random.Range(12f, 16f) * UnitScale, 8f, Random.Range(0.4f, 0.5f));
                if (bit >= 0) _particles[bit].Spin = Random.Range(-420f, 420f);
            }
        }

        /// <summary>보스 그림자 파도: 크게 일어나 맞은 쪽으로 덮치고, 바닥에 그림자가 번진다</summary>
        void StrikeWave(Sprite art, in EnemyAttackLook look, Vector2 at, Vector2 dir, float size, float life, float alpha, float ground)
        {
            int wave = StrikeArt(art, VfxMotif.Swirl, look, at - dir * size * 0.15f, size * 0.55f, size, life, alpha);
            if (wave >= 0)
            {
                ref var p = ref _particles[wave];
                p.Curve = SizeCurve.Pop;
                p.P1 = dir * size * 0.25f;
                // 그림 속 파도는 오른쪽으로 말린다 — 왼쪽으로 덮칠 때는 뒤집어 그린다 (거꾸로 서지 않게)
                FaceArt(wave, dir, 0f);
                p.FadeOut = 0.5f;
            }
            float floor = ground != 0f ? ground : at.y - size * 0.3f;
            int shadow = Spawn(_soft, BattleEase.WithAlpha(ShadowPurple, 0.55f * alpha), new Vector2(at.x, floor), size * 0.3f, size * 0.9f, life * 1.2f, 0f, true);
            if (shadow >= 0)
            {
                _particles[shadow].Aspect = GroundAspect * 0.5f;
                // 바닥에 눕힌 타원은 돌리지 않는다 (돌리면 비스듬히 선 타원이 된다)
                _particles[shadow].Rotation = 0f;
                _particles[shadow].FadeIn = 0.05f;
            }
            if (!Allow(FxPriority.Extra)) return;
            for (int i = 0; i < 5; i++)
            {
                var spread = RandomDirection();
                int puff = Drift(Puff, BattleEase.WithAlpha(i % 2 == 0 ? ShadowPurple : look.Main, 0.55f * alpha), at + spread * size * 0.15f,
                    (spread + dir) * Random.Range(40f, 80f), Vector2.up * 20f, size * 0.15f, size * 0.4f, Random.Range(0.45f, 0.65f), 0.05f);
                if (puff >= 0) _particles[puff].FadeIn = 0.05f;
            }
            Sparkles(at, size * 0.35f, look.Accent, 4, 0.06f);
        }

        /// <summary>날개가루 소용돌이 (좀나방 여왕): 빙글 돌며 커지고 비늘 가루가 둘레를 맴돈다</summary>
        void StrikeVortex(Sprite art, in EnemyAttackLook look, Vector2 at, float size, float life, float alpha)
        {
            int vortex = StrikeArt(art, VfxMotif.Swirl, look, at, size * 0.35f, size, life, alpha);
            if (vortex >= 0)
            {
                ref var p = ref _particles[vortex];
                p.Spin = -280f;
                p.FadeOut = 0.55f;
            }
            if (!Allow(FxPriority.Sparkle)) return;
            var scale = Motif(VfxMotif.Scale);
            for (int i = 0; i < 8; i++)
            {
                float start = i * Mathf.PI * 0.25f;
                int dust = Orbit(scale, BattleEase.WithAlpha(i % 2 == 0 ? ScaleDust : look.Accent, 0.85f * alpha), at, new Vector2(size * 0.45f, size * 0.32f),
                    start, -7f, 1f, 0.25f, 14f * UnitScale, 8f, life, 0f);
                if (dust >= 0) _particles[dust].Spin = 300f;
            }
        }

        /// <summary>적 공격 그림 하나 (그림이 없으면 흰색 모양에 탁한 주색). 그림 알파 = alpha</summary>
        int StrikeArt(Sprite art, VfxMotif fallback, in EnemyAttackLook look, Vector2 at, float size0, float size1, float life, float alpha)
        {
            bool isArt = art != null;
            int index = Spawn(isArt ? art : Motif(fallback), isArt ? BattleEase.WithAlpha(Color.white, alpha) : BattleEase.WithAlpha(look.Main, 0.9f * alpha),
                at, size0, size1, life, 0f);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.FadeIn = 0.01f;
            p.FadeOut = 0.55f;
            p.Rotation = 0f;
            return index;
        }

        /// <summary>
        /// 오른쪽을 향해 그린 그림을 dir 쪽으로 돌린다. 왼쪽을 향하면 좌우로 뒤집고 돌려 그림이 거꾸로 서지 않게 한다.
        /// baseAngle = 그림 속 기준 방향을 오른쪽에 맞추는 보정 각(도)
        /// </summary>
        void FaceArt(int index, Vector2 dir, float baseAngle)
        {
            if (index < 0) return;
            ref var p = ref _particles[index];
            bool mirror = dir.x < -0.05f;
            float angle = mirror ? Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            p.Rotation = angle + (mirror ? -baseAngle : baseAngle);
            p.Rect.localScale = mirror ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
        }

        // ───────────── 수선점 피격 (ATK-03 공통 / FX-JUICE E14) ─────────────

        /// <summary>
        /// 수선점 문이 맞음: 적이 들어온 방향의 문 가장자리(edge)에 붉은 빛(알파 — 위험색은 가산하지 않는다) → 나뭇조각·못·먼지 그림(enemy_hit_shop)이
        /// 첫 프레임 ×1.12로 터져 바깥으로 10px 밀리고 → 나뭇조각이 문 아래 바닥에 한 번 튀고(일반 3 / 보스 포위 7) → 라벤더 먼지 퍽.
        /// 크기는 hitSize × U × 0.8 (EnemyFxCatalog.ShopHitArtScale — 적의 일격이 우리 지킴이 기본 명중보다 크게 읽히지 않게).
        /// 흔들림은 문 쪽으로 (일반 0.3 — 수선점 채널 초당 0.6까지 + 킥 2px / 보스 포위 0.55 + 킥 4px), 건물은 용수철로 눌렸다 되튀고
        /// 설정 흔들림 배율(ShakeScaleNow — 시험장 DevOverride 포함)만큼 떤다.
        /// 보호막이 다 막았으면(absorbed) 나뭇조각 대신 돔 표면에 하늘색 육각 물결(가산 0.6) + 하늘색 코어 별(가산 0.8) — 흔들림 절반, 킥 없음
        /// </summary>
        public void ShopImpact(Vector2 edge, Vector2 outDirection, float hitSize, bool boss, bool absorbed)
        {
            if (!_ready) Prepare();
            var toDoor = -outDirection;
            if (boss) AddTrauma(EnemyFxCatalog.ShopStrikeTrauma(true, absorbed), toDoor);
            else _trauma.AddShopHit(EnemyFxCatalog.ShopStrikeTrauma(false, absorbed), toDoor);
            // 건물 떨림도 화면 흔들림과 같은 설정을 따른다 (뒤따르는 ShopDamaged의 ShopView.Hit()까지 — 피격마다 지금 값을 넘긴다)
            if (_shop != null) _shop.ShakeScale = ShakeScaleNow;
            float unit = UnitScale;
            float size = hitSize * unit * EnemyFxCatalog.ShopHitArtScale;
            if (absorbed)
            {
                var hex = _library != null ? _library.Motif(VfxMotif.HexRing) : null;
                int ripple = SpawnFx(hex != null ? hex : Motif(VfxMotif.Bubble), BattleEase.WithAlpha(UITheme.SoftBlue, 0.9f), edge, size * 0.3f, size, 0.3f, 0f, 0.6f);
                if (ripple >= 0) _particles[ripple].FadeOut = 0.4f;
                int core = CoreStar(edge, BattleEase.WithAlpha(UITheme.SoftBlue, 0.85f), size * 0.4f, size * 0.28f, 0.08f, 0f);
                if (core >= 0) _particles[core].Add = 0.8f;
                Sparkles(edge, 40f, UITheme.SoftBlue, 3, 0f);
                return;
            }
            Kick(toDoor, boss ? HitFeel.KickSiege : HitFeel.KickShop, boss ? KickChannel.Siege : KickChannel.Shop);
            if (_shop != null) _shop.Hit(boss ? EnemyFxCatalog.ShopSiegeSquash : EnemyFxCatalog.ShopHitSquash);

            // 1 붉은 빛 (알파)
            Flash(edge, _glow, BattleEase.WithAlpha(UITheme.Danger, 0.6f), size * 0.25f, size * 0.9f, 0.25f);
            // 2 나뭇조각 그림: 닿는 첫 프레임이 가장 크고 (×1.12, 33ms 버팀 → 70ms에 자리 크기) 바깥으로 10px 밀린다
            var art = _library != null ? _library.AttackArt(SkillVfxLibrary.ShopHitId) : null;
            int hit = PeakArt(art != null ? art : Motif(VfxMotif.Splinter), art != null ? Color.white : WoodBrown, edge, size, EnemyFxCatalog.ShopHitFirstFrame,
                0.033f, 0.07f, EnemyFxCatalog.ShopHitEndScale, EnemyFxCatalog.ShopHitLife, 0.03f, 0.45f);
            if (hit >= 0)
            {
                _particles[hit].P1 = outDirection * (10f * unit);
                Turn(hit, Random.Range(-20f, 20f));
            }
            // 3 나뭇조각: 바깥·위로 튀어 문 아래 바닥에 한 번 통 튄다
            if (Allow(FxPriority.Extra))
            {
                var splinter = Motif(VfxMotif.Splinter);
                int count = boss ? EnemyFxCatalog.SiegeSplinters : EnemyFxCatalog.ShopSplinters;
                float doorY = _shop != null ? _shop.DoorPosition.y : edge.y - 30f * unit;
                float floor = Mathf.Min(doorY - 20f * unit, edge.y - 24f * unit);
                for (int i = 0; i < count; i++)
                {
                    if (i >= 3 && !Allow(FxPriority.Extra)) break;
                    var spread = (outDirection + RandomDirection() * 0.6f).normalized;
                    spread.y = Mathf.Abs(spread.y) * 0.5f + 0.55f;
                    var velocity = spread.normalized * (Random.Range(140f, 260f) * unit);
                    float bit = Random.Range(14f, 20f) * (boss ? 1.4f : 1f) * unit;
                    int index = Spawn(splinter, i % 2 == 0 ? WoodBrown : UITheme.Cream, edge, bit, bit * 0.85f, 0.6f, 0.03f, false, true);
                    if (index < 0) continue;
                    ref var p = ref _particles[index];
                    p.Motion = Motion.Bounce;
                    p.P1 = velocity;
                    p.P2 = new Vector2(floor, 900f);
                    p.P3 = new Vector2(0.35f, 0f);
                    p.Spin = Random.Range(-480f, 480f);
                    p.FadeIn = 0.02f;
                    p.FadeOut = 0.75f;
                }
            }
            // 4 먼지 퍽
            int dust = Drift(Puff, BattleEase.WithAlpha(UITheme.Lavender, 0.35f), edge + Vector2.down * 20f, outDirection * 30f + Vector2.up * 10f,
                Vector2.up * 10f, size * 0.3f, size * 0.7f, 0.5f, 0.03f);
            if (dust >= 0) _particles[dust].FadeIn = 0.05f;
        }

        /// <summary>
        /// 보스 포위 예비 동작: 문 앞 바닥에 붉은 경고 마법진(motif_magic_circle, Danger 0.35)이 차오르고 바깥 고리가 오므라든다 — "곧 덮친다".
        /// BattleView가 매 프레임 시뮬레이터의 차오름(windup 0→1, EnemyState.SiegeWindup)을 넣는다: 원 하나를 붙잡아 두고
        /// 크기·알파를 차오름에 맞추므로 분노(빨라짐)·기절·넉백(0으로 되돌아감)에도 공격 순간과 어긋나지 않는다. windup이 0 이하면 걷는다
        /// </summary>
        public void SiegeTelegraph(Vector2 edgeOnGround, float size, float windup)
        {
            if (!_ready) Prepare();
            if (!(windup > 0f))
            {
                EndSiegeTelegraph();
                return;
            }
            float k = Mathf.Clamp01(windup);
            if (!HoldsTelegraph(_telegraphDisc))
            {
                // 붉은 경고 마법진 (바느질 원판 그림, 없으면 부드러운 원)
                _telegraphDisc = Spawn(Motif(VfxMotif.MagicCircle), BattleEase.WithAlpha(UITheme.Danger, 0f), edgeOnGround, size * 0.5f, size * 0.5f, TelegraphHoldLife, 0f, true);
                PrepareTelegraph(_telegraphDisc);
            }
            if (!HoldsTelegraph(_telegraphRing))
            {
                _telegraphRing = Spawn(_ring, BattleEase.WithAlpha(UITheme.Danger, 0f), edgeOnGround, size * 1.4f, size * 1.4f, TelegraphHoldLife, 0f, true);
                PrepareTelegraph(_telegraphRing);
            }
            // 원: 절반 크기에서 다 찰 때까지 커지고 처음 30%에 걸쳐 진해진다 / 고리: 1.4배에서 0.95배로 오므라든다
            DriveTelegraph(_telegraphDisc, edgeOnGround, size * Mathf.Lerp(0.5f, 1f, k), EnemyFxCatalog.TelegraphAlpha * Mathf.Clamp01(k / 0.3f));
            DriveTelegraph(_telegraphRing, edgeOnGround, size * Mathf.Lerp(1.4f, 0.95f, k), 0.75f * Mathf.Clamp01(k / 0.2f));
        }

        /// <summary>텔레그래프를 걷는다 (공격했거나 기절·넉백으로 끊겼을 때): 붙잡아 둔 원이 0.1초 만에 옅어지며 사라진다</summary>
        public void EndSiegeTelegraph()
        {
            if (!_ready) return;
            FadeTelegraph(_telegraphDisc);
            FadeTelegraph(_telegraphRing);
            _telegraphDisc = -1;
            _telegraphRing = -1;
        }

        bool HoldsTelegraph(int index) =>
            index >= 0 && index < _particles.Length && _particles[index].Alive && _particles[index].Life >= TelegraphHoldLife;

        void PrepareTelegraph(int index)
        {
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Linear;
            p.Aspect = GroundAspect;
            p.Rotation = 0f;
            p.FadeIn = 0f;
            p.FadeOut = 1f;
        }

        /// <summary>붙잡아 둔 텔레그래프 입자를 이번 프레임 크기·알파로 (수명이 길어 진행도는 0에 머문다)</summary>
        void DriveTelegraph(int index, Vector2 at, float size, float alpha)
        {
            if (!HoldsTelegraph(index)) return;
            ref var p = ref _particles[index];
            p.Age = 0f;
            p.P0 = at;
            p.Size0 = size;
            p.Size1 = size;
            p.Color = BattleEase.WithAlpha(UITheme.Danger, alpha);
        }

        void FadeTelegraph(int index)
        {
            if (!HoldsTelegraph(index)) return;
            ref var p = ref _particles[index];
            p.Age = 0f;
            p.Life = TelegraphFadeTime;
            p.FadeIn = 0f;
            p.FadeOut = 0f;
        }

        /// <summary>수선점 내구도 30% 미만: 문에서 불씨가 하나 피어오른다 (BattleView가 주기적으로 부른다)</summary>
        public void ShopEmber(Vector2 door)
        {
            if (!Allow(FxPriority.Sparkle)) return;
            bool flame = Random.value < 0.35f;
            int index = Drift(flame ? Motif(VfxMotif.Flame) : _sparkle, flame ? new Color(1f, 0.62f, 0.4f, 0.85f) : UITheme.Lamp,
                door + new Vector2(Random.Range(-46f, 46f), Random.Range(10f, 50f)), new Vector2(Random.Range(-10f, 10f), Random.Range(40f, 70f)), Vector2.zero,
                Random.Range(10f, 16f), 4f, Random.Range(0.7f, 0.95f));
            if (index < 0) return;
            _particles[index].Wobble = 6f;
            _particles[index].Spin = flame ? 0f : Random.Range(-120f, 120f);
            if (flame) _particles[index].Rotation = 0f;
        }

        // ───────────── 처치 = '풀려남' (ATK-04 / FX-JUICE E10) ─────────────

        /// <summary>
        /// 적 처치 (처치 꽃이 터지는 순간 = Delay, 마지막 일격이 닿고 몸이 하얗게 버틴 뒤). K = 200px × U, 아래에서 위로:
        ///   잉크 받침 1.1K(높음·보통) · 발밑 바닥 고리 0.3 → 1.6K(갈래 색) · 후광 1.8K(가산 0.35) ·
        ///   처치 꽃 kill_{갈래}(→ enemy_death_{갈래}) 첫 프레임 ×1.35로 터져 끝까지 ×1.30으로 부풂 (그림자 갈래는 라벤더 0.70 · 가산 0.20 · 30%부터 사라짐 —
        ///   잉크 얼룩이 아니라 빛으로 풀린다) · 코어 별 0.8 → 0.5K · 갈래 빛 고리 0.5 → 1.4K (갈래 고리 색을 크림 쪽으로 0.45, 세기 0.6, 높음·보통) ·
        ///   갈래 곁들임 (나방 날개 갈라짐 · 쇠붙이 반짝 · 그림자 실 가닥 · 먼지 퍽 2) · 갈래 색종이 (10 / 6 / 4, 쇠붙이는 나사·톱니 6~8 + 금 단추) ·
        ///   여운 반짝 · 기억 한 톨이 수선점 문으로 (그림자 갈래 3) · 바닥 자국 (그림이 있으면, 높음).
        /// 한꺼번에 처치 상한 (예산이 다른 연출 몫까지 차지하지 않게 — 처치 입자는 처치 보류 동안에도 칸을 잡고 있다):
        ///   풀 처치 꽃은 0.2초에 3개까지 (위쪽 여유 48칸 이상일 때) → 미니 처치 (0.6K 꽃 · 코어 + 곁들임 문 안에서 갈래 빛 고리 · 별빛 문 안에서 색종이 4)
        ///   0.2초에 10 / 6 / 4개 (높음 / 보통 / 낮음) →
        ///   그 뒤로는 마이크로 처치 (0.5K 꽃 하나). 꼬리 여유(20칸)도 없으면 그리지 않는다 — 몸의 하얀 버팀·톡이 처치를 알린다.
        ///   색종이는 여유 칸에 맞춰 줄고(EnemyFxCatalog.KillConfettiCount), 2배속 5연속 이상이면 색종이·여운 반짝이 절반, 처치 꽃 수명은 × PopScale.
        /// 치명타·스킬 처치는 수선점 바깥쪽으로 킥 2px. 10연속 이상이면 금빛 색종이 2와 금빛 여운 반짝. 보스는 BossDeath (E13).
        /// </summary>
        public void EnemyDeath(in EnemyDeathRequest r)
        {
            if (!_ready) Prepare();
            var family = EnemyFxCatalog.Family(r.EnemyId);
            var point = r.Point;
            ClampToField(ref point);
            if (r.Boss)
            {
                BossDeath(in r, family, point);
                return;
            }
            float delay = r.Delay;
            float unit = UnitScale;
            float k = HitFeel.KillBurstSize * unit;
            var cream = JuiceColors.CoreCream;
            var bloom = KillBloomSprite(family, out bool art, r.EnemyId);
            var bloomTint = art ? EnemyFxCatalog.KillBloomTint(r.EnemyId)
                : BattleEase.WithAlpha(Color.Lerp(EnemyFxCatalog.FamilyColor(family), UITheme.Cream, 0.35f), 0.9f);
            // 2배속: 처치 꽃도 명중 그림처럼 × PopScale(0.8) — 처치가 두 배로 잦아도 살아 있는 꽃이 두 배로 쌓이지 않게
            float bloomLife = KillBloomLife * PopScale;
            bool fast = EnemyFxCatalog.FastKillStreak(_battleSpeed, KillStreak);
            var set = EnemyFxCatalog.KillConfetti(family);
            KillKick(in r, point, delay);

            if (FreeParticles < EnemyFxCatalog.FullKillFree || !TryKillBurst(delay))
            {
                LesserKill(bloom, bloomTint, family, set, point, k, bloomLife, delay, fast);
                return;
            }

            bool gold = KillStreak >= GoldStreak;
            // 1 잉크 받침 · 2 발밑 바닥 고리 (바닥 레이어)
            if (JuiceGates.InkBacking(_quality, true)) InkBacking(point, k * 1.1f, 0.45f, 0.22f, delay);
            Shockwave(r.Feet, Color.Lerp(EnemyFxCatalog.FamilyRingColor(family), UITheme.Cream, 0.25f), k * 0.3f, k * 1.6f, 0.26f, delay, 0.6f, 0.7f, GroundAspect, true);
            // 3 후광
            Halo(point, Color.Lerp(UITheme.Lamp, UITheme.Cream, 0.5f), k * 1.8f, 0.22f, delay, KillHaloIntensity);
            // 4 처치 꽃
            KillFlower(bloom, bloomTint, family, point, k, bloomLife, delay);
            // 5 코어 별 (50ms 버팀)
            int star = CoreStar(point, cream, k * 0.8f, k * 0.5f, 0.1f, delay);
            if (star >= 0) _particles[star].FadeOut = 0.5f;
            // 6 갈래 빛 고리 (높음·보통): 갈래 고리 색을 크림 쪽으로 0.45, 1.4K, 세기 0.6 — 여럿이 함께 풀려도 흰 비눗방울이 아니라 갈래가 읽힌다
            if (_quality != FxQuality.Low)
                Shockwave(point, EnemyFxCatalog.KillRingColor(family), k * 0.5f, k * EnemyFxCatalog.KillRingReach, 0.24f, delay, 0.85f, EnemyFxCatalog.KillRingIntensity);
            // 7 갈래 곁들임 (곁들임 층)
            if (Allow(FxPriority.Extra)) FamilyLayer(family, point, unit, delay);
            // 8 갈래 색종이 (쇠붙이는 나사·톱니·용수철 6~8이 곧 색종이 — 여유 칸에 맞춰 줄고 2배속 연속 처치면 절반) + 10연속이면 금빛 2
            int confetti = EnemyFxCatalog.KillConfettiCount(JuiceGates.KillConfetti(_quality), FreeParticles, fast);
            if (family == EnemyDeathFamily.Metal) confetti = Mathf.Min(confetti, Random.Range(6, 9));
            Confetti(point, set, confetti, 1f, delay);
            if (gold) GoldConfetti(point, fast ? 1 : 2, delay);
            // 9 여운 반짝 (반짝 무리 2 / 반짝이 4 — 2배속 연속 처치면 절반, 별빛 문 안에서만)
            bool cluster = _juice != null && _juice.SparkleCluster != null;
            int twinkles = cluster ? 2 : 4;
            if (fast) twinkles /= 2;
            if (Allow(FxPriority.Sparkle)) Twinkles(point + Vector2.up * (k * 0.1f), k * 0.28f, twinkles, gold ? UITheme.Gold : UITheme.Lamp, delay);
            // 10 기억 한 톨 → 수선점 문 (도착하면 문에 코어 별 + 수선점 맥동)
            if (r.Shop != Vector2.zero)
            {
                bool shadow = family == EnemyDeathFamily.Shadow;
                int motes = shadow ? 3 : 1;
                for (int i = 0; i < motes; i++)
                {
                    if (!Allow(FxPriority.Sparkle)) break;
                    var from = shadow ? point + new Vector2(Random.Range(-20f, 20f), Random.Range(60f, 120f)) * unit : point + Vector2.up * (k * 0.1f);
                    RememberMote(from, r.Shop, delay + EnemyFxCatalog.MemoryMoteStart + i * 0.06f);
                }
            }
            // 12 바닥 자국
            KillDecals(family, r.Feet, unit, delay);
        }

        /// <summary>
        /// 풀 처치 꽃 자리가 없는 처치 (0.2초에 4번째부터 또는 위쪽 여유 48칸 미만): 꼬리 여유(20칸)가 없으면 그리지 않고,
        /// 미니 처치 칸이 남았으면 미니 (0.6K 꽃 · 코어 별 + 곁들임 문을 지나야 갈래 빛 고리 · 별빛 문을 지나야 색종이 — 문 없이 뜨는 곁들임은 없다),
        /// 미니도 0.2초에 품질 상한(10 / 6 / 4)을 넘었으면 마이크로 (0.5K 꽃 하나)
        /// </summary>
        void LesserKill(Sprite bloom, Color bloomTint, EnemyDeathFamily family, ConfettiSet set, Vector2 point, float k, float bloomLife, float delay, bool fast)
        {
            if (!Allow(FxPriority.Trail)) return;
            if (!_miniKills.TryAdd(_clock + delay, HitFeel.KillBurstWindow, EnemyFxCatalog.MiniKillsPerWindow(_quality)))
            {
                KillFlower(bloom, bloomTint, family, point, k * EnemyFxCatalog.KillMicroScale, bloomLife * 0.85f, delay);
                return;
            }
            float mini = k * KillMiniScale;
            KillFlower(bloom, bloomTint, family, point, mini, bloomLife, delay);
            int miniCore = CoreStar(point, JuiceColors.CoreCream, mini * 0.8f, mini * 0.5f, 0.08f, delay);
            if (miniCore >= 0) _particles[miniCore].FadeOut = 0.5f;
            if (Allow(FxPriority.Extra))
                Shockwave(point, EnemyFxCatalog.KillRingColor(family), mini * 0.5f, mini * EnemyFxCatalog.KillRingReach, 0.22f, delay, 0.85f, EnemyFxCatalog.KillRingIntensity);
            if (Allow(FxPriority.Sparkle)) Confetti(point, set, EnemyFxCatalog.KillConfettiCount(EnemyFxCatalog.MiniKillConfetti, FreeParticles, fast), 0.8f, delay);
        }

        /// <summary>
        /// 처치 꽃 하나 (SizeCurve.Impact: 첫 프레임 ×1.35 → 33ms 버팀 → 70ms에 size로 → 끝까지 ×1.30, −12~12°로 기울여 ±20°/s로 돈다).
        /// 갈래가 사라짐 시작(40%, 그림자 30%)과 가산 비율(그림자 0.20 — 잉크 얼룩이 아니라 빛으로 풀림)을 정한다
        /// </summary>
        int KillFlower(Sprite bloom, Color tint, EnemyDeathFamily family, Vector2 point, float size, float life, float delay)
        {
            int index = PeakArt(bloom, tint, point, size, KillBloomFirst, KillBloomHold, KillBloomSettle, KillBloomEnd, life, delay,
                EnemyFxCatalog.KillBloomFadeStart(family));
            if (index >= 0) _particles[index].Add = EnemyFxCatalog.KillBloomAdd(family);
            Turn(index, Random.Range(-12f, 12f), Random.Range(-20f, 20f));
            return index;
        }

        /// <summary>
        /// 처치 꽃 그림: 적 전용 그림(ATK-08 — EnemyFxCatalog.DeathKeyOverride, 예: 실뭉치 enemy_death_yarn, 있을 때만) → Juice kill_{갈래} →
        /// enemy_death_{갈래} → 흰색 모양 (art = 색 있는 그림인지)
        /// </summary>
        Sprite KillBloomSprite(EnemyDeathFamily family, out bool art, string enemyId = null)
        {
            var own = _library != null ? _library.AttackArt(EnemyFxCatalog.DeathKeyOverride(enemyId)) : null;
            if (own != null)
            {
                art = true;
                return own;
            }
            var bloom = _juice != null ? _juice.KillBloom(family) : null;
            if (bloom == null && _library != null) bloom = _library.EnemyDeath(family);
            art = bloom != null;
            if (art) return bloom;
            return Motif(family switch
            {
                EnemyDeathFamily.Moth => VfxMotif.Scale,
                EnemyDeathFamily.Metal => VfxMotif.Star,
                EnemyDeathFamily.Shadow => VfxMotif.Swirl,
                _ => VfxMotif.Soft,
            });
        }

        /// <summary>치명타·스킬 처치: 처치 꽃이 터지는 순간 수선점 바깥쪽으로 킥 2px (처치 채널 0.5초에 한 번)</summary>
        void KillKick(in EnemyDeathRequest r, Vector2 point, float delay)
        {
            if (r.KilledBy != HitTier.Crit && r.KilledBy != HitTier.Skill) return;
            var outward = point - (_shop != null ? _shop.FieldPosition : Vector2.zero);
            if (outward.sqrMagnitude < 1f) outward = Vector2.up;
            Kick(outward, HitFeel.KickKill, KickChannel.Kill, delay);
        }

        /// <summary>갈래 곁들임 (§1.22 '남기는 갈래 층'): 나방 = 날개 두 쪽이 갈라져 기울며 떨어짐 · 쇠붙이 = 반짝 · 그림자 = 실 가닥이 위로 풀림 · 먼지 = 퍽 2</summary>
        void FamilyLayer(EnemyDeathFamily family, Vector2 point, float scale, float delay)
        {
            switch (family)
            {
                case EnemyDeathFamily.Moth:
                    MothWings(_library != null ? _library.EnemyDeath(family) : null, point, scale, delay);
                    break;
                case EnemyDeathFamily.Metal:
                    int glint = Flash(point, _sparkle, BattleEase.WithAlpha(Color.white, 0.9f), 100f * scale, 30f * scale, 0.18f, delay);
                    if (glint >= 0)
                    {
                        _particles[glint].Add = 0.6f;
                        _particles[glint].Spin = 220f;
                    }
                    break;
                case EnemyDeathFamily.Shadow:
                    ShadowStrands(point, scale, delay);
                    break;
                default:
                    DustPuffs(point, scale, delay);
                    break;
            }
        }

        /// <summary>먼지 퍽 2: 회보라 뭉게구름이 부풀며 흩어진다</summary>
        void DustPuffs(Vector2 point, float scale, float delay)
        {
            var color = Color.Lerp(EnemyFxCatalog.FamilyColor(EnemyDeathFamily.Dust), UITheme.Background, 0.2f);
            for (int i = 0; i < 2; i++)
            {
                var dir = RandomDirection();
                int puff = Drift(Puff, BattleEase.WithAlpha(color, 0.5f), point + dir * 10f, dir * Random.Range(24f, 50f) * scale + Vector2.up * 16f, Vector2.up * 18f,
                    30f * scale, 70f * scale, Random.Range(0.4f, 0.55f), delay);
                if (puff >= 0) _particles[puff].FadeIn = 0.05f;
            }
        }

        /// <summary>나방 날개: 처치 그림을 좌우 반쪽으로 갈라 ±25° 기울며 떨어뜨린다 (그림이 없으면 비늘 모양 두 장)</summary>
        void MothWings(Sprite art, Vector2 point, float scale, float delay)
        {
            float size = 120f * scale;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                float sign = left ? -1f : 1f;
                int wing;
                if (art != null)
                {
                    wing = Spawn(art, Color.white, point, size, size * 1.05f, 0.6f, delay, false, true);
                    SetHalf(wing, left);
                }
                else
                {
                    wing = Spawn(Motif(VfxMotif.Scale), BattleEase.WithAlpha(EnemyFxCatalog.FamilyColor(EnemyDeathFamily.Moth), 0.9f),
                        point + new Vector2(sign * size * 0.22f, 0f), size * 0.45f, size * 0.5f, 0.6f, delay, false, true);
                }
                if (wing < 0) continue;
                ref var p = ref _particles[wing];
                p.Curve = SizeCurve.Linear;
                p.Rotation = 0f;
                p.Spin = sign * 25f / 0.6f;
                p.P1 = new Vector2(sign * 46f, 10f) * scale;
                p.P2 = Vector2.down * 46f * scale;
                p.FadeIn = 0.01f;
                p.FadeOut = 0.35f;
            }
        }

        /// <summary>그림자 실 가닥 5: 위로 길게 풀려 오른다</summary>
        void ShadowStrands(Vector2 point, float scale, float delay)
        {
            for (int i = 0; i < 5; i++)
            {
                var start = point + new Vector2(Random.Range(-26f, 26f), Random.Range(-10f, 10f)) * scale;
                var end = start + new Vector2(Random.Range(-30f, 30f), Random.Range(110f, 170f)) * scale;
                var control = (start + end) * 0.5f + new Vector2(Random.Range(-40f, 40f), 0f) * scale;
                int strand = Curve(_trail, BattleEase.WithAlpha(i % 2 == 0 ? ShadowPurple : new Color(0.55f, 0.42f, 0.85f, 1f), 0.8f), start, control, end,
                    40f * scale, 70f * scale, Random.Range(0.5f, 0.65f), delay + i * 0.03f, true);
                if (strand < 0) continue;
                _particles[strand].AlignToPath = true;
                _particles[strand].Aspect = 0.22f;
                _particles[strand].FadeOut = 0.5f;
            }
        }

        /// <summary>금빛 색종이 count개 (10연속 이상 — 처치 색종이에 금이 섞인다)</summary>
        void GoldConfetti(Vector2 at, int count, float delay)
        {
            var sprite = Or(_juice != null ? _juice.ConfettiStar : null, Motif(VfxMotif.Star));
            float unit = UnitScale;
            for (int i = 0; i < count; i++)
            {
                if (!Allow(FxPriority.Sparkle)) break;
                float angle = Mathf.PI * 0.5f + Random.Range(-1.2f, 1.2f);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.Range(220f, 420f) * unit);
                float size = Random.Range(16f, 22f) * unit;
                int index = Spawn(sprite, UITheme.Gold, at + Random.insideUnitCircle * (10f * unit), size, size * 0.85f, Random.Range(0.5f, 0.65f), delay + i * 0.01f,
                    false, true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Motion = Motion.Ballistic;
                p.P1 = velocity;
                p.Drag = 1.6f;
                p.Gravity = 900f;
                p.Add = 0.5f;
                p.FadeIn = 0.02f;
                p.FadeOut = 0.7f;
                p.Spin = Random.Range(-540f, 540f);
            }
        }

        /// <summary>처치 바닥 자국 2장 (높음 품질, decal_{glitter|buttons} 그림이 있을 때만 — 없으면 건너뛴다)</summary>
        void KillDecals(EnemyDeathFamily family, Vector2 feet, float unit, float delay)
        {
            if (_quality != FxQuality.High || _juice == null || !EnemyFxCatalog.KillDecal(family, out var kind)) return;
            var sprite = _juice.Decal(kind);
            if (sprite == null) return;
            for (int i = 0; i < 2; i++)
            {
                Decal(feet + new Vector2(Random.Range(-26f, 26f), Random.Range(-8f, 6f)) * unit, sprite, BattleEase.WithAlpha(Color.white, 0.45f),
                    Random.Range(18f, 30f) * unit, delay + 0.05f);
            }
        }

        /// <summary>
        /// 기억 한 톨 (잊힘이 기억으로): 빛 구슬(fx_glow_core, 가산)과 크림 반짝이가 곡선을 그리며 450ms 동안 수선점 문으로 날아가고,
        /// 닿으면 문에 코어 별 반짝 + 수선점 맥동 0.22 (Arrival.ShopGlint). 진행은 InOutCubic (FX-JUICE E10 10행 — 천천히 떠나 가로질러 날고 문 앞에 내려앉는다)
        /// </summary>
        void RememberMote(Vector2 from, Vector2 shop, float delay)
        {
            float unit = UnitScale;
            var end = shop + new Vector2(Random.Range(-12f, 12f), Random.Range(-6f, 10f));
            var mid = (from + end) * 0.5f;
            var away = (from - end).normalized;
            var control = mid + new Vector2(-away.y, away.x) * Random.Range(-90f, 90f) + Vector2.up * Random.Range(60f, 130f);
            float flight = EnemyFxCatalog.MemoryMoteFlight * Random.Range(0.92f, 1.08f);
            int glow = Curve(GlowCoreSprite, BattleEase.WithAlpha(UITheme.Lamp, 0.85f), from, control, end, 28f * unit, 18f * unit, flight, delay, true,
                easeCubic: true);
            if (glow >= 0)
            {
                ref var p = ref _particles[glow];
                p.Add = 1f;
                p.Arrival = Arrival.ShopGlint;
                p.FadeIn = 0.08f;
                p.FadeOut = 0.94f;
            }
            int spark = Curve(_sparkle, UITheme.Cream, from, control, end, 18f * unit, 10f * unit, flight, delay, true, easeCubic: true);
            if (spark >= 0)
            {
                ref var p = ref _particles[spark];
                p.Spin = 300f;
                p.Add = 0.8f;
                p.FadeIn = 0.08f;
                p.FadeOut = 0.94f;
            }
        }

        // ───────────── 보스 처치 (FX-JUICE E13) ─────────────

        /// <summary>
        /// 보스 처치 폭발 (Delay = 버팀 — 터지는 순간에 나타나 전체 정지 동안 첫 프레임 그대로 멈춘다). B = 420px × U,
        /// 가운데 = 보이는 전장 띠(FieldBottom ~ FieldTop — 시트가 열리면 시트 위 끝까지) 가장자리에서 0.3B 안쪽, 보스 자리에서 0.3B까지만 옮김
        /// (EnemyFxCatalog.BossBurstCenter — 상단 HUD 안전선(SafeTop)으로 당기면 위쪽 보스의 폭발이 238px 내려가 수선점 위에 터졌다. 첫 프레임 잘림 22% 남짓):
        ///   잉크 받침 1.2B · 바닥 고리 0.3 → 2.4B(등불) · 빛살 1.6B(+30°/s) · 속도선 2.4B(높음·보통) · 영웅 폭발 hero_boss_kill B(첫 프레임 ×1.30, 끝까지 ×1.25) ·
        ///   코어 1.0 → 0.6B · 가로 섬광 3B · 고리 3겹(크림 · 금 · 라벤더, 60ms 간격, 0.3 → 3B) · 갈래 곁들임(여왕 날개 · 그림자 실 가닥) ·
        ///   색종이 24/16/10 · 리본 6/4/2 · 별빛 18/12/6 · 여운 별 10/6/0 · 기억 한 톨 8(그림자)/5 · 보상 단추 3 (상한과 별개).
        /// 채움 비용 (화면 크기 가산 사각형 — 느린 기기는 보통·낮음으로 떨어진다): 고리는 높음 3겹 3B · 보통 2겹 2.5B · 낮음 1겹 2B (1~2겹은 조금 밝게),
        /// 낮음은 빛살·바닥 고리도 없다 (속도선·잉크 받침은 원래 높음·보통만).
        /// 위쪽 입자가 75칸보다 적게 남았으면 색종이 12 · 별빛 8로 줄인다 (영웅 층은 늘 뜬다).
        /// 버팀 동안 전장이 0.2까지 어두워지고 (실제 시간), 그 순간은 코어 BossKillMoment가 건다 (전체 정지 · 임팩트 프레임 + 보스 크림 실루엣 ·
        /// 인쇄 어긋남 · 빛 번쩍임 · 흔들림 0.8 · 줌 1.03(터지는 자리) · 진동)
        /// </summary>
        void BossDeath(in EnemyDeathRequest r, EnemyDeathFamily family, Vector2 point)
        {
            float delay = r.Delay;
            float unit = UnitScale;
            float b = HitFeel.BossBurstSize * unit;
            var center = EnemyFxCatalog.BossBurstCenter(point, EnemyFxCatalog.BossBurstInset * b, FieldHalfWidth, FieldBottom, FieldTop,
                EnemyFxCatalog.BossBurstMaxMove * b);
            var feet = r.Feet + (center - point);
            var cream = JuiceColors.CoreCream;
            bool rich = FreeParticles >= EnemyFxCatalog.BossBurstParticles;

            // 버팀 동안 전장이 어두워진다 — 터지는 순간 임팩트 프레임(0.45)이 이어받는다
            FieldDim(BossPreDim, Mathf.Max(0.05f, delay), 0f, 0.06f);

            // 1 잉크 받침 · 2 바닥 고리 (바닥 레이어 — 낮음 품질은 바닥 고리 없음)
            bool wide = EnemyFxCatalog.BossWideLayers(_quality);
            if (JuiceGates.InkBacking(_quality, true)) InkBacking(center, b * 1.2f, 0.5f, 0.4f, delay);
            if (wide) Shockwave(feet, UITheme.Lamp, b * 0.3f, b * 2.4f, 0.5f, delay, 0.6f, 0.7f, GroundAspect, true);
            // 3 빛살 (80ms에 들어오고 마지막 40%에 걷힌다 — 낮음 품질은 없음)
            var rays = wide && _juice != null ? _juice.Rays : null;
            if (rays != null)
            {
                int ray = SpawnFx(rays, BattleEase.WithAlpha(UITheme.Lamp, BossRaysIntensity), center, b * 1.6f, b * 1.68f, 1f, delay, BossRaysAdd);
                if (ray >= 0)
                {
                    ref var p = ref _particles[ray];
                    p.Curve = SizeCurve.Linear;
                    p.Spin = 30f;
                    p.FadeIn = 0.08f;
                    p.FadeOut = 0.6f;
                    p.FadeKind = FadeInQuad;
                    Turn(ray, Random.Range(0f, 360f), 30f);
                }
            }
            // 4 속도선 (높음 · 보통은 큰 순간만)
            if (JuiceGates.Speedlines(_quality, true))
            {
                int lines = Speedlines(center, cream, b * 2.4f, 0.26f, delay, 1f);
                if (lines >= 0)
                {
                    _particles[lines].Add = BossSpeedlinesAdd;
                    _particles[lines].Size1 = b * 2.4f * 1.1f;
                }
            }
            // 5 영웅 폭발 (그림이 없으면 별 폭발 + 갈래 처치 그림 2배)
            var hero = _juice != null ? _juice.HeroBossKill : null;
            if (hero != null)
            {
                int burst = PeakArt(hero, Color.white, center, b, BossBurstFirst, BossBurstHold, BossBurstSettle, BossBurstEnd, BossBurstLife, delay, 0.45f);
                Turn(burst, Random.Range(-6f, 6f), Random.Range(-8f, 8f));
            }
            else
            {
                Sprite starBurst = null;
                bool starArt = false;
                if (_library != null) _library.TryGetVfx(SkillVfx.StarBurst, out starBurst, out starArt);
                int burst = PeakArt(starBurst != null ? starBurst : Motif(VfxMotif.Star), starArt ? Color.white : UITheme.Gold, center, b * 0.85f, BossBurstFirst,
                    BossBurstHold, BossBurstSettle, BossBurstEnd, BossBurstLife, delay, 0.45f);
                Turn(burst, Random.Range(-10f, 10f), 12f);
                var bloom = KillBloomSprite(family, out bool bloomArt);
                int flower = PeakArt(bloom, bloomArt ? Color.white : BattleEase.WithAlpha(EnemyFxCatalog.FamilyColor(family), 0.9f), center,
                    HitFeel.KillBurstSize * unit * EnemyFxCatalog.BossDeathScale * 0.8f, KillBloomFirst, BossBurstHold, BossBurstSettle, KillBloomEnd, 0.7f, delay, 0.45f);
                Turn(flower, Random.Range(-12f, 12f), -10f);
            }
            // 6 코어 (80ms 버팀)
            int core = CoreStar(center, cream, b, b * 0.6f, 0.15f, delay);
            if (core >= 0) _particles[core].FadeOut = 0.53f;
            // 7 가로 섬광
            Flare(center, Color.Lerp(UITheme.Gold, UITheme.Cream, 0.5f), b * 3f, b * 0.15f, Random.Range(-8f, 8f), 0.12f, delay, 0.9f);
            // 8 고리 (크림 → 금 → 라벤더, 60ms 간격: 높음 3겹 3B · 보통 2겹 2.5B · 낮음 1겹 2B — 겹이 적으면 하나하나를 조금 밝게)
            int rings = Mathf.Min(EnemyFxCatalog.BossRingCount(_quality), BossRingColors.Length);
            float reach = EnemyFxCatalog.BossRingReach(_quality);
            float ringIntensity = rings >= BossRingColors.Length ? BossRingIntensity : EnemyFxCatalog.BossSparseRingIntensity;
            for (int i = 0; i < rings; i++)
                Shockwave(center, BossRingColors[i], b * 0.3f, b * reach, 0.4f, delay + i * 0.06f, 0.85f, ringIntensity);
            // 갈래 곁들임 (영웅 폭발 위 — 여왕 날개가 갈라지고 그림자 실 가닥이 풀려 오른다)
            if (Allow(FxPriority.Extra)) FamilyLayer(family, center, unit * EnemyFxCatalog.BossDeathScale, delay);
            // 9 색종이 · 10 리본 · 11 별빛 · 12 여운 별
            int confetti = JuiceGates.BossConfetti(_quality);
            int sparkles = JuiceGates.BossSparkles(_quality);
            if (!rich)
            {
                confetti = Mathf.Min(confetti, 12);
                sparkles = Mathf.Min(sparkles, 8);
            }
            Confetti(center, ConfettiSet.Boss, confetti, 1f, delay);
            BossRibbons(center, JuiceGates.BossRibbons(_quality), unit, delay);
            BossSparkles(center, sparkles, unit, delay);
            BossStars(center, b, _quality == FxQuality.High ? 10 : _quality == FxQuality.Medium ? 6 : 0, unit, delay);
            // 13 기억 한 톨 (그림자 8 / 그 밖 5)
            if (r.Shop != Vector2.zero)
            {
                int motes = family == EnemyDeathFamily.Shadow ? 8 : 5;
                for (int i = 0; i < motes; i++)
                {
                    if (FreeParticles <= 20) break;
                    var from = center + new Vector2(Random.Range(-0.3f, 0.3f) * b, Random.Range(0.05f, 0.35f) * b);
                    RememberMote(from, r.Shop, delay + 0.3f + i * 0.06f);
                }
            }
            // 14 보상 단추 3 (처치 단추 상한과 별개, 80ms 간격)
            for (int i = 0; i < EnemyFxCatalog.BossRewardCoins; i++) SpawnCoin(center, delay + 0.1f + i * 0.08f);
            // 그 순간 (코어): 전체 정지 · 임팩트 프레임 + 보스 크림 실루엣 · 인쇄 어긋남 · 빛 번쩍임 · 흔들림 · 줌(터지는 자리) · 진동
            BossKillMoment(delay, center, r.Body);
        }

        /// <summary>보스 처치 리본: 250~450px/s로 솟아 팔랑이며(12px) 천천히 떨어진다 (중력 500)</summary>
        void BossRibbons(Vector2 at, int count, float unit, float delay)
        {
            var ribbon = Or(_juice != null ? _juice.ConfettiRibbon : null, _trail);
            for (int i = 0; i < count; i++)
            {
                if (FreeParticles <= 24) break;
                float angle = Mathf.PI * 0.5f + Random.Range(-1.3f, 1.3f);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.Range(250f, 450f) * unit);
                float size = Random.Range(36f, 48f) * unit;
                int index = Spawn(ribbon, RibbonColors[i % RibbonColors.Length], at + Random.insideUnitCircle * (20f * unit), size, size * 0.9f,
                    Random.Range(1.2f, 1.4f), delay + i * 0.015f, false, true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Motion = Motion.Ballistic;
                p.P1 = velocity;
                p.Drag = 1.2f;
                p.Gravity = 500f;
                p.Wobble = 12f * unit;
                p.Spin = Random.Range(-200f, 200f);
                p.FadeIn = 0.02f;
                p.FadeOut = 0.7f;
            }
        }

        /// <summary>보스 처치 별빛 (금 / 크림, 가산 0.8): 12ms 간격으로 위쪽 원뿔로 튀어 떨어진다</summary>
        void BossSparkles(Vector2 at, int count, float unit, float delay)
        {
            var star = Motif(VfxMotif.Star);
            for (int i = 0; i < count; i++)
            {
                if (FreeParticles <= 20) break;
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                int spark = Drift(i % 4 == 0 ? star : _sparkle, i % 3 == 0 ? UITheme.Gold : UITheme.Cream, at, dir * (Random.Range(180f, 380f) * unit), Vector2.down * 90f,
                    Random.Range(18f, 30f) * unit, 6f, Random.Range(0.6f, 0.9f), delay + i * 0.012f);
                if (spark < 0) continue;
                _particles[spark].Spin = Random.Range(-300f, 300f);
                _particles[spark].Add = 0.8f;
            }
        }

        /// <summary>보스 처치 여운 별 (등불색, 가산 0.7): 폭발 둘레에서 200ms부터 40px 떠오르며 반짝인다</summary>
        void BossStars(Vector2 at, float burst, int count, float unit, float delay)
        {
            var star = Motif(VfxMotif.Star);
            for (int i = 0; i < count; i++)
            {
                if (FreeParticles <= 20) break;
                float size = Random.Range(16f, 24f) * unit;
                int index = Spawn(star, UITheme.Lamp, at + Random.insideUnitCircle * (burst * 0.55f), size, size * 0.85f, Random.Range(0.9f, 1.2f),
                    delay + 0.2f + i * 0.02f, false, true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.P1 = Vector2.up * (40f * unit);
                p.Pulse = 0.18f;
                p.PulseHz = 5f;
                p.Add = 0.7f;
                p.FadeIn = 0.15f;
                p.FadeOut = 0.6f;
                p.Spin = Random.Range(-40f, 40f);
            }
        }

        /// <summary>
        /// 첫 프레임이 가장 큰 그림 (SizeCurve.Impact, FX-JUICE §0.2-1): settled × first로 열어 hold 동안 버티고 settle 동안 settled로 →
        /// 끝까지 × end (end ≥ 1이면 OutQuart로 부푼다). fade부터 InQuad로 사라진다. 회전 0
        /// </summary>
        int PeakArt(Sprite sprite, Color color, Vector2 at, float settled, float first, float hold, float settle, float end, float life, float delay, float fade)
        {
            int index = Spawn(sprite, color, at, settled * first, settled, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Impact;
            p.Hold = hold;
            p.Settle = settle;
            p.EndScale = end;
            p.Rotation = 0f;
            p.FadeIn = 0f;
            p.FadeOut = fade;
            p.FadeKind = FadeInQuad;
            p.Rect.sizeDelta = new Vector2(p.Size0, p.Size0);
            return index;
        }

        /// <summary>입자를 degrees로 돌려 두고 spin(°/s)으로 돌린다</summary>
        void Turn(int index, float degrees, float spin = 0f)
        {
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Rotation = degrees;
            p.Spin = spin;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        /// <summary>그림의 왼쪽/오른쪽 반쪽만 그린다 (Image Filled 가로 0.5 — 나방 날개가 갈라질 때)</summary>
        void SetHalf(int index, bool left)
        {
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Half = left ? (byte)1 : (byte)2;
            p.Image.type = Image.Type.Filled;
            p.Image.fillMethod = Image.FillMethod.Horizontal;
            p.Image.fillOrigin = left ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            p.Image.fillAmount = 0.5f;
        }

        // ───────────── 처치 보상 반짝 ─────────────

        /// <summary>
        /// 일반 적 5마리째 처치마다: 단추가 톡 튀어 상단 바(단추 칩 쪽)로 날아간다 (동시에 2개까지, 실제 시간으로 잰다). 날렸으면 true
        /// </summary>
        public bool KillCoin(Vector2 from, float delay)
        {
            if (!_ready) Prepare();
            if (_realClock > _coinClear) _coinsInFlight = 0;
            if (_coinsInFlight >= EnemyFxCatalog.MaxRewardCoins) return false;
            if (!SpawnCoin(from, delay)) return false;
            _coinsInFlight++;
            _coinClear = _realClock + delay + EnemyFxCatalog.RewardCoinFlight;
            return true;
        }

        /// <summary>단추 하나가 상단 바로 (금빛 반짝 + 뒤따르는 반짝이, 가산 0.6). 상한을 보지 않는다</summary>
        bool SpawnCoin(Vector2 from, float delay)
        {
            var sprite = _coinSprite != null ? _coinSprite : Motif(VfxMotif.Button);
            var tint = _coinSprite != null ? Color.white : UITheme.Gold;
            float unit = UnitScale;
            var start = from + Vector2.up * 10f;
            // 단추 칩은 상단 바 가운데 조금 왼쪽 — 전장 위 가장자리를 넘어 날아가며 잘린다 (상단 바로 들어가는 느낌)
            var to = new Vector2(-FieldHalfWidth * 0.1f, FieldTop + 140f);
            var control = new Vector2((start.x + to.x) * 0.5f + Random.Range(-60f, 60f), Mathf.Max(start.y, to.y) + 60f);
            int coin = Curve(sprite, tint, start, control, to, 40f * unit, 30f, EnemyFxCatalog.RewardCoinFlight, delay);
            if (coin < 0) return false;
            _particles[coin].FadeIn = 0.05f;
            _particles[coin].FadeOut = 0.92f;
            _particles[coin].Rotation = 0f;
            int glow = Flash(start, _glow, BattleEase.WithAlpha(UITheme.Gold, 0.8f), 20f, 90f, 0.25f, delay);
            if (glow >= 0) _particles[glow].Add = 0.6f;
            int trail = Curve(_sparkle, UITheme.Lamp, start, control, to, 22f * unit, 8f, EnemyFxCatalog.RewardCoinFlight, delay + 0.04f, true);
            if (trail >= 0)
            {
                _particles[trail].Add = 0.6f;
                _particles[trail].Spin = 360f;
                _particles[trail].FadeOut = 0.8f;
            }
            return true;
        }

        /// <summary>
        /// 스테이지 클리어 (UX-01 / FX-JUICE E18): 리본 자리(from — 안전선 밖이어도 된다, 리본 배너 뒤)에서
        /// 금빛 후광 900px(가산 0.35) · 영웅 그림 hero_stage_clear가 0 → 560px로 톡 (→ 별 폭발 + 실 고리) · 충격 고리 100 → 760px ·
        /// 축하 색종이 30/20/10 · 반짝 분수 12 · 보상 단추 count개가 시차를 두고 상단 바로 (처치 단추 상한과 별개). 줌 1.015 (흔들림 없음).
        /// 앱 전체 빛 번쩍임 아래에 깔리는 큰 가산 사각형이라 후광은 보통·낮음에서 560px, 충격 고리는 낮음에서 560px까지 (느린 기기의 채움 비용).
        /// 가운데 높이는 보이는 띠 위 끝에서 리본 아래 끝(344) + 0.36 × 560px × U 아래까지 (EnemyFxCatalog.RewardCenterY): 두 상태 모두 화면 ≈ 676 —
        /// 등불이 '스테이지 클리어!' 리본 · 추천 알약 뒤에 숨지 않고 수선점 위 광장에서 피어오른다
        /// </summary>
        public void RewardShower(Vector2 from, int count)
        {
            if (!_ready) Prepare();
            float unit = UnitScale;
            from.y = EnemyFxCatalog.RewardCenterY(from.y, FieldTop, unit);
            // 낮은 화면(4:3 · 시트)에서 리본 아래로 누른 가운데가 보이는 띠 밑으로 빠지지 않게 — 16:9는 MAIN · SHEET 모두 이 바닥보다 위라 그대로
            from.y = Mathf.Max(from.y, FieldBottom + EnemyFxCatalog.RewardFloorInset * EnemyFxCatalog.RewardHeroSize * unit);
            // 1 금빛 후광 (80ms에 차오르고 잦아든다 — 0.91 → 1배)
            float glowSize = EnemyFxCatalog.RewardGlowSize(_quality) * unit;
            int glow = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(UITheme.Gold, 0.35f), from, glowSize * (820f / 900f), glowSize, 0.6f, 0f, 1f);
            if (glow >= 0)
            {
                ref var p = ref _particles[glow];
                p.FadeIn = 0.13f;
                p.FadeOut = 0.35f;
                p.FadeKind = FadeInQuad;
            }
            // 2 영웅 그림 (톡 — OutBack 2.6으로 22% 동안, 60%부터 사라짐)
            var hero = _juice != null ? _juice.HeroStageClear : null;
            if (hero != null) PopHero(hero, Color.white, from, EnemyFxCatalog.RewardHeroSize * unit, 0.8f);
            else
            {
                Sprite starBurst = null;
                bool starArt = false;
                if (_library != null) _library.TryGetVfx(SkillVfx.StarBurst, out starBurst, out starArt);
                PopHero(starBurst != null ? starBurst : Motif(VfxMotif.Star), starArt ? Color.white : UITheme.Gold, from, 420f * unit, 0.8f);
                var thread = _juice != null ? _juice.RingThread : null;
                Shockwave(from, UITheme.Gold, 120f * unit, 520f * unit, 0.6f, 0f, 0.5f, 0.85f, 1f, false, thread);
            }
            // 3 충격 고리 (금 → 크림)
            Shockwave(from, Color.Lerp(UITheme.Gold, UITheme.Cream, 0.35f), 100f * unit, EnemyFxCatalog.RewardRingSize(_quality) * unit, 0.45f, 0f, 0.7f, 0.8f);
            // 4 축하 색종이
            Confetti(from, ConfettiSet.Celebration, JuiceGates.CelebrationConfetti(_quality), 1f, 0f);
            // 5 반짝 분수 (위로 솟아 천천히 떨어진다)
            for (int i = 0; i < 12; i++)
            {
                if (FreeParticles <= 20) break;
                float angle = Mathf.PI * 0.5f + Random.Range(-0.6f, 0.6f);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.Range(140f, 260f) * unit);
                float size = Random.Range(16f, 26f) * unit;
                int spark = Spawn(_sparkle, i % 2 == 0 ? UITheme.Cream : UITheme.Lamp, from + new Vector2(Random.Range(-60f, 60f), Random.Range(-10f, 10f)) * unit,
                    size, size * 0.6f, Random.Range(0.55f, 0.75f), 0.1f + i * 0.04f, false, true);
                if (spark < 0) continue;
                ref var p = ref _particles[spark];
                p.Motion = Motion.Ballistic;
                p.P1 = velocity;
                p.Drag = 0.6f;
                p.Gravity = 200f;
                p.Add = 0.8f;
                p.Spin = Random.Range(-180f, 180f);
                p.FadeIn = 0.05f;
                p.FadeOut = 0.55f;
            }
            // 6 보상 단추
            var sprite = _coinSprite != null ? _coinSprite : Motif(VfxMotif.Button);
            var tint = _coinSprite != null ? Color.white : UITheme.Gold;
            var to = new Vector2(-FieldHalfWidth * 0.1f, FieldTop + 140f);
            for (int i = 0; i < count; i++)
            {
                var start = from + new Vector2(Random.Range(-120f, 120f), Random.Range(-20f, 20f));
                var control = new Vector2((start.x + to.x) * 0.5f + Random.Range(-80f, 80f), Mathf.Max(start.y, to.y) + 40f);
                int coin = Curve(sprite, tint, start, control, to, 36f, 28f, EnemyFxCatalog.RewardCoinFlight + Random.Range(-0.05f, 0.1f), 0.25f + i * 0.08f);
                if (coin < 0) continue;
                _particles[coin].FadeIn = 0.05f;
                _particles[coin].FadeOut = 0.92f;
                _particles[coin].Rotation = 0f;
            }
            ZoomPunch(HitFeel.ZoomStageClear, from);
        }

        /// <summary>큰 그림 하나가 0에서 size까지 톡 (SizeCurve.Pop) 피었다가 60%부터 사라진다</summary>
        void PopHero(Sprite sprite, Color color, Vector2 at, float size, float life)
        {
            int index = Spawn(sprite, color, at, size, size * 1.07f, life, 0f);
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Pop;
            p.Rotation = 0f;
            p.FadeIn = 0.02f;
            p.FadeOut = 0.6f;
            p.Rect.sizeDelta = Vector2.zero;
            p.Rect.localRotation = Quaternion.identity;
        }

        // ───────────── 등장 (ATK-15) ─────────────

        /// <summary>
        /// 적 등장 (FX-JUICE E19 · UX-SPEC-IA §6.5 CR-19 — MAIN에서는 다섯 출발점이 모두 보여 몸이 거리 한가운데서 나타난다):
        ///   일반 = 발밑 그림자 웅덩이(보스 웅덩이의 0.45배 = 162px, 0.35초 — 바닥 곁들임 층, 바닥 여유가 있을 때만) · 옅은 잉크 물결(0.3 → 1.1배) ·
        ///   처치 갈래 색 먼지 소용돌이 72px가 0.3초 돌고 · 갈래 색 먼지 퍽 · 피어오르는 어둠 2 (별빛 층) — 어둠에서 솟아오른 것으로 읽힌다.
        ///   보스 = 그림자 웅덩이 360px가 0.6초 번지고 어둠이 피어오르며 어두운 잉크 고리(알파 — 가산 아님)가 0.3 → 1.2배로 0.5초 동안 퍼진다.
        /// (몸은 EnemyView가 0.2초(보스 0.6초) 동안 또렷해진다). 분열로 생긴 작은 적은 부르지 않는다. 바닥에 눕힌 타원은 돌리지 않는다
        /// </summary>
        public void EnemySpawn(Vector2 feet, EnemyDeathFamily family, bool boss)
        {
            if (!_ready) Prepare();
            var color = EnemyFxCatalog.FamilyColor(family);
            float unit = UnitScale;
            if (!boss)
            {
                float size = EnemyFxCatalog.SpawnSwirlSize * unit;
                float pool = EnemyFxCatalog.BossPoolSize * EnemyFxCatalog.SpawnPoolScale * unit;
                // 1 그림자 웅덩이 · 잉크 물결 (바닥 레이어 — 바닥 예산의 예약 칸은 남긴다)
                if (AllowGround(FxPriority.Extra))
                {
                    int disc = Spawn(_soft, BattleEase.WithAlpha(Color.Lerp(ShadowPurple, color, 0.25f), 0.75f), feet, pool * 0.3f, pool,
                        EnemyFxCatalog.SpawnPoolLife, 0f, true);
                    if (disc >= 0)
                    {
                        ref var p = ref _particles[disc];
                        p.Aspect = GroundAspect * 0.55f;
                        p.Rotation = 0f;
                        p.FadeIn = 0.06f;
                        p.FadeOut = 0.45f;
                    }
                    if (AllowGround(FxPriority.Sparkle))
                        Shockwave(feet, JuiceColors.Ink, pool * 0.3f, pool * 1.1f, EnemyFxCatalog.SpawnPoolLife, 0f, 0f, 0.45f, GroundAspect, true);
                }
                // 2 갈래 색 먼지 소용돌이 (별빛 층 — 영웅 층 몫은 쓰지 않는다)
                int swirl = Spawn(Motif(VfxMotif.Swirl), BattleEase.WithAlpha(color, 0.85f), feet + Vector2.up * size * 0.4f, size * 0.4f, size,
                    EnemyFxCatalog.SpawnSwirlLife, 0f, false, true);
                if (swirl >= 0)
                {
                    ref var p = ref _particles[swirl];
                    p.Aspect = 0.75f;
                    p.Spin = -720f;
                    p.FadeIn = 0.05f;
                    p.FadeOut = 0.5f;
                }
                // 3 갈래 색 먼지 퍽 (바닥)
                int puff = Spawn(Puff, BattleEase.WithAlpha(color, 0.45f), feet, size * 0.3f, size * 1.1f, 0.35f, 0f, true);
                if (puff >= 0)
                {
                    _particles[puff].Aspect = GroundAspect * 0.5f;
                    _particles[puff].Rotation = 0f;
                }
                // 4 웅덩이에서 피어오르는 어둠 2 (별빛 층 여유가 있을 때만)
                if (!Allow(FxPriority.Sparkle)) return;
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    var at = feet + new Vector2(side * Random.Range(0.12f, 0.3f) * pool, Random.Range(-4f, 4f) * unit);
                    int wisp = Drift(Puff, BattleEase.WithAlpha(ShadowPurple, 0.5f), at, new Vector2(side * Random.Range(4f, 12f), Random.Range(50f, 80f)) * unit,
                        Vector2.up * (12f * unit), 16f * unit, 40f * unit, Random.Range(0.4f, 0.5f), 0.03f + i * 0.04f);
                    if (wisp < 0) continue;
                    _particles[wisp].Wobble = 4f * unit;
                    _particles[wisp].FadeIn = 0.15f;
                }
                return;
            }
            float bossPool = EnemyFxCatalog.BossPoolSize * unit;
            int bossDisc = Spawn(_soft, BattleEase.WithAlpha(ShadowPurple, 0.8f), feet, bossPool * 0.3f, bossPool, EnemyFxCatalog.BossPoolLife, 0f, true);
            if (bossDisc >= 0)
            {
                ref var p = ref _particles[bossDisc];
                p.Aspect = GroundAspect * 0.55f;
                p.Rotation = 0f;
                p.FadeIn = 0.1f;
                p.FadeOut = 0.6f;
            }
            int swirlPool = Spawn(Motif(VfxMotif.Swirl), BattleEase.WithAlpha(new Color(0.55f, 0.42f, 0.85f, 1f), 0.6f), feet, bossPool * 0.2f, bossPool * 0.75f,
                EnemyFxCatalog.BossPoolLife, 0f, true);
            if (swirlPool >= 0)
            {
                ref var p = ref _particles[swirlPool];
                p.Aspect = GroundAspect * 0.55f;
                p.Spin = 200f;
                p.FadeIn = 0.1f;
            }
            // 어두운 잉크 고리 (알파): 웅덩이 둘레로 0.3 → 1.2배, 0.5초
            Shockwave(feet, JuiceColors.Ink, bossPool * 0.3f, bossPool * 1.2f, 0.5f, 0f, 0f, 0.65f, GroundAspect, true);
            for (int i = 0; i < 6; i++)
            {
                var at = feet + new Vector2(Random.Range(-bossPool * 0.35f, bossPool * 0.35f), Random.Range(-10f, 10f));
                int wisp = Drift(Puff, BattleEase.WithAlpha(ShadowPurple, 0.6f), at, new Vector2(Random.Range(-10f, 10f), Random.Range(70f, 120f)),
                    Vector2.up * 20f, 30f, 80f, Random.Range(0.55f, 0.75f), 0.05f + i * 0.04f);
                if (wisp >= 0) _particles[wisp].Wobble = 6f;
            }
            Sparkles(feet + Vector2.up * 40f, 80f, new Color(0.62f, 0.42f, 0.88f, 1f), 5, 0.1f);
        }

        /// <summary>보스가 부하를 부름: 발밑에 날개가루 소용돌이 260px가 0.6초 돌고 그 안에서 부하가 나온다</summary>
        public void MinionSummon(Vector2 feet, EnemyDeathFamily family)
        {
            if (!_ready) Prepare();
            float size = EnemyFxCatalog.MinionVortexSize * UnitScale;
            var color = EnemyFxCatalog.FamilyColor(family);
            int vortex = Spawn(Motif(VfxMotif.Swirl), BattleEase.WithAlpha(color, 0.8f), feet, size * 0.3f, size, EnemyFxCatalog.MinionVortexLife, 0f, true);
            if (vortex >= 0)
            {
                ref var p = ref _particles[vortex];
                p.Aspect = GroundAspect * 0.6f;
                p.Spin = -260f;
                p.FadeIn = 0.1f;
                p.FadeOut = 0.6f;
            }
            if (!Allow(FxPriority.Sparkle)) return;
            var scale = Motif(VfxMotif.Scale);
            for (int i = 0; i < 6; i++)
            {
                int dust = Orbit(scale, BattleEase.WithAlpha(i % 2 == 0 ? ScaleDust : color, 0.85f), feet + Vector2.up * 20f, new Vector2(size * 0.4f, size * 0.22f),
                    i * Mathf.PI / 3f, -6f, 1f, 0.3f, 14f, 8f, EnemyFxCatalog.MinionVortexLife, 0f);
                if (dust >= 0) _particles[dust].Spin = 260f;
            }
        }

        // ───────────── 기믹 표시 ─────────────

        /// <summary>태엽쥐가 다른 적을 감아 줌 (Mender): 그 적 머리 위 태엽 열쇠가 돌고 발밑에 노란 고리</summary>
        public void WindUp(EnemyView target)
        {
            if (target == null) return;
            if (!_ready) Prepare();
            int key = Spawn(Motif(VfxMotif.WindKey), UITheme.Gold, target.HeadPoint + Vector2.up * 8f, 26f * UnitScale, 32f * UnitScale, 0.7f, 0f);
            if (key >= 0)
            {
                ref var p = ref _particles[key];
                p.Curve = SizeCurve.Pop;
                p.Rotation = 0f;
                p.Spin = 360f;
                p.FadeIn = 0.01f;
                p.FadeOut = 0.7f;
            }
            AnchorTo(key, target);
            int ring = Spawn(_ring, BattleEase.WithAlpha(UITheme.Gold, 0.7f), target.Rect.anchoredPosition, 30f * UnitScale, 96f * UnitScale, 0.45f, 0f, true);
            if (ring >= 0)
            {
                _particles[ring].Aspect = GroundAspect;
                _particles[ring].Rotation = 0f;
                _particles[ring].FadeIn = 0.01f;
            }
            AnchorTo(ring, target);
        }

        /// <summary>좀나방이 천 잔향이를 갉음 (FabricEater): 분홍빨강 구멍 스티커가 몸에 0.4초 붙는다</summary>
        public void GnawMark(Vector2 body, EchoView echo)
        {
            if (!_ready) Prepare();
            var at = body + new Vector2(Random.Range(-16f, 16f), Random.Range(-12f, 12f)) * UnitScale;
            // ATK-08: 좀나방 구멍 그림(fx_moth_hole)이 있으면 그것을 색 그대로, 없으면 지금의 흰 찢김 모양 + 분홍빨강
            bool art = _mothHoleArt != null;
            int hole = Spawn(art ? _mothHoleArt : Motif(VfxMotif.Tear), art ? Color.white : GnawRed, at, 18f * UnitScale, 28f * UnitScale, 0.4f, 0f);
            if (hole < 0) return;
            ref var p = ref _particles[hole];
            p.Curve = SizeCurve.Back;
            p.Rotation = Random.Range(-30f, 30f);
            p.FadeIn = 0.01f;
            p.FadeOut = 0.7f;
            AnchorTo(hole, echo);
        }

        /// <summary>녹이 슬음 (Rusting): 잔향이 몸 / 지킴이 발판에 주황 녹 얼룩이 duration 동안 (최대 2.5초) 붙어 있다</summary>
        public void RustStain(Vector2 at, float duration, EchoView echo, bool onGround)
        {
            if (!_ready) Prepare();
            float life = Mathf.Clamp(duration, 0.4f, 2.5f);
            float size = (onGround ? 64f : 30f) * UnitScale;
            // ATK-08: 녹 얼룩 그림(fx_rust_stain)이 있으면 그것을, 없으면 지금의 흰 얼룩 모양 + 주황 녹
            bool art = _rustStainArt != null;
            int stain = Spawn(art ? _rustStainArt : Motif(VfxMotif.Splat), BattleEase.WithAlpha(art ? Color.white : RustOrange, onGround ? 0.7f : 0.85f), at,
                size * 0.6f, size, life, 0f, onGround);
            if (stain < 0) return;
            ref var p = ref _particles[stain];
            p.Curve = SizeCurve.Back;
            p.Rotation = Random.Range(0f, 360f);
            if (onGround) p.Aspect = GroundAspect * 0.6f;
            p.FadeIn = 0.03f;
            p.FadeOut = 0.8f;
            AnchorTo(stain, echo);
        }

        /// <summary>
        /// 정전 스테이지에서 빛 특성 계보가 명중: 그 자리에 크림빛 웅덩이가 0.4초 열린다 (덮개 구멍은 StageAmbienceView.LightHole이 함께 연다).
        /// 셰이더가 없어 구멍을 못 뚫는 기기에서도 이 빛만으로 "빛이 어둠을 밀어냈다"가 읽힌다
        /// </summary>
        public void LightPool(Vector2 at, float delay)
        {
            if (!Allow(FxPriority.Extra)) return;
            float size = EnemyFxCatalog.LightHoleRadius * 2f * UnitScale;
            int pool = Flash(at, _glow, BattleEase.WithAlpha(UITheme.Cream, 0.32f), size * 0.4f, size, EnemyFxCatalog.LightHoleTime, delay);
            if (pool >= 0) _particles[pool].FadeOut = 0.5f;
        }
    }
}
