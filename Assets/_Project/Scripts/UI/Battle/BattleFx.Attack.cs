using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>기본 공격 연출 한 번의 입력 (전장 로컬 좌표, 픽셀)</summary>
    public struct AttackFxRequest
    {
        /// <summary>쏘는 자리 (지킴이 가슴 / 잔향이 몸 가운데)</summary>
        public Vector2 From;
        public EnemyView Target;
        public string LineageId;
        public CombatStyle Style;
        /// <summary>형태 단계 (2 = 2차 진화형: 이펙트 +10%에 금빛 반짝임 4개 + 엷은 고리)</summary>
        public int FormTier;
        /// <summary>신화 계보 (명중할 때 별가루 +3)</summary>
        public bool Mythic;
        /// <summary>잔향이 공격 (작고 반투명한 빛의 존재: 그림 65%, 아래에 잔향이 빛)</summary>
        public bool Echo;
        /// <summary>스킬로 불려 나온 작은 양말 토큰 (짝짝이 명중 45%)</summary>
        public bool Token;
        /// <summary>잔향이 빛 색 (EchoView.GlowColor)</summary>
        public Color Glow;
        /// <summary>튜토리얼 첫인상 (AppSignals.FtueFirstImpression — 지킴이만): 명중 그림 ×1.3 + 반짝임 2개 더</summary>
        public bool Impression;
    }

    /// <summary>
    /// 기본 공격 연출 v2 (FX-JUICE E1~E6 · E8 — "명중마다 작은 등불이 켜진다").
    ///   E1 쏘기: 지킴이가 W(비행 × 0.3, 0.02~0.05초) 웅크렸다가 놓는 순간 총구 — 코어 빛 24→40 · 계보 총구 불꽃(muzzle_{계보}, 가산 0.85) 40→96 ·
    ///      계보 빛줄기 2. 투사체는 W 뒤에 나가 비행 − W 동안 날아간다 (도착 시각 = 돌려주는 비행 시간 그대로).
    ///   E2 비행: 아래 빛(가산) · 빛 꼬리(저격·찰칵이, 가산 0.7) · 잔상 2(머리 그림, 30/60ms 뒤, 가산 0.5) · 머리(104px, 비행 중 가로 1.2/세로 0.85,
    ///      끝에서 쏙 꽂히는 진행도 휨) · 길 위에 흘리는 계보 조각 2.
    ///   E3 일반 명중: 잉크 받침(바닥) · 가운데가 열린 명중 그림(atk_{계보}_impact, 첫 프레임 ×1.30이 가장 크다) · 코어 별(가산) · 계보 고리 ·
    ///      나가는 쪽 빛줄기 4 · 사물 곁들임 · 반짝임 2. 공격자 쪽으로 12px 당겨 맞은 몸이 가려지지 않게.
    ///   E4 강타(저격·돌격): ×1.35 · 코어 0.6 · 고리 1.3 · 빛줄기 6(70°) · 돌격 초승달 베기(가산) · 저격 꿰뚫는 섬광 + 화면 킥 HitFeel.KickSnipe.
    ///   E5 치명타: 같은 프레임 명중을 atk_{계보}_crit(200px, 첫 프레임 ×1.45 → 0.97로 되튐)로 바꾸고 금빛 후광 · 크림 코어 · 가로 섬광 ·
    ///      크림/금 고리 2 · 속도선 · 사방 빛줄기 8 · 떠오르는 별 4 · 계보 특수(바늘땀 채찍 · 단추 딸깍 · 잠김 고리 · 솟는 불꽃 · 물왕관 · 입맞춤 · 세 음 부채) · 바닥 자국.
    ///   E6 밀도: 같은 대상 90ms 안 · 붐빌 때(0.25초 안 도착 8개 넘게)는 미니 명중, 16개 넘으면 그림 + 코어만. 사슬 3번째마다 리듬 강조 (코어 ×1.15 + 고리).
    ///   E8 잔향이: 초승달 베기(fx_slash_arc, 가산)가 볼록한 쪽을 대상으로 향해 쓸고, 그 꼭대기(50ms)에서 0.65배 명중 · 원거리는 62px 투사체.
    /// 명중 층은 대상에 붙어서 도착 순간에 핀다 (대상이 비행 중에 죽으면 버티는 자리에서). 튀는 조각 · 빛줄기는 닿는 순간까지만 따라간다.
    /// 새 그림이 없으면 예전 그림 · 흰색 모양으로 (코드는 새 그림이 꼭 있어야 하지 않는다). 입자가 모자라면 별빛 → 곁들임 → 꼬리 순서로 버린다.
    /// 할당 없음 (구조체 · 미리 만든 배열만).
    /// </summary>
    public partial class BattleFx
    {
        // ───────────── 크기 · 시간 (기준 px × 액터 배율 U) ─────────────

        /// <summary>지킴이 투사체 머리 (× 형태 × 저격 늘림 × 비행 중 가로 1.2 / 세로 0.85)</summary>
        const float GuardianShotSize = 104f;
        const float ShotFlightStretchX = 1.2f;
        const float ShotFlightStretchY = 0.85f;
        /// <summary>투사체 진행도 휨 k' = k·(0.75 + 0.25k) — 끝에서 대상에 쏙 꽂힌다 (도착 시각은 그대로)</summary>
        const float ShotWarp = 0.25f;
        /// <summary>
        /// 명중 크기 S = HitSize × ImpactScale × 성향 × 형태 × 보스 × U (E3). 스펙 1.2 → 1.35 (2차 조율: 기본 명중이 넓이 2% 바닥 아래
        /// 1.85~1.92%, 잔향이 근접이 1.07%로 작았다. 1.3은 빛줄기 · 반짝임 · 숫자가 커지지 않아 넓이가 +12~14%에 그쳐 2.11~2.18% · 1.18%였다 —
        /// 1.35로 기본 2.2% 넘게 · 잔향이 근접 ≈1.25%, busy_20 바뀐 넓이는 35% 아래)
        /// </summary>
        const float ImpactScale = 1.35f;
        const float EchoRangedScale = 0.6f;
        /// <summary>토큰 명중 (S 대비 — 잔향이는 HitFeel.ForEcho의 0.65)</summary>
        const float TokenHitScale = 0.45f;
        const float EchoShotLife = 0.14f;
        /// <summary>잔향이 근접 베기 길이 (스펙 110 → 140: 0.65배 명중에 거의 다 가려져 잔향이 명중이 1.2% 아래로 작았다)</summary>
        const float EchoSlashSize = 140f;
        /// <summary>
        /// 명중 층을 닿는 순간보다 이만큼 먼저 깨운다 — 지연이 실수 덧셈으로 도착 프레임에 아주 조금 남아 한 프레임 늦게 피는 일을 막는다
        /// (첫 프레임이 가장 큰 명중이 닿는 그 프레임에 보이게. 2ms는 한 프레임 안이라 눈에 보이지 않는다)
        /// </summary>
        const float ImpactLead = 0.002f;
        /// <summary>잔향이 근접 명중은 베기가 시작하고 이만큼(× PopScale) 뒤 — 베기 꼭대기</summary>
        const float EchoSwing = 0.05f;
        /// <summary>치명타 크기 S_c (보스 ×1.4, 잔향이 ×0.65)</summary>
        const float CritSize = 200f;
        const float EchoCritScale = 0.65f;
        /// <summary>치명타 그림은 0.97로 정착했다가 1.0으로 되튄다</summary>
        const float CritRebound = 0.97f;
        /// <summary>
        /// 바꾼 치명타 그림이 사라지기 시작하는 수명 비율 (스펙 0.55 → 0.35: 0.33초 중 115ms부터 걷힌다). 치명타 그림은 가운데가 찬 도장이라
        /// 오래 버티는 InQuad 대신 SmoothStep으로 걷는다 — 200ms에 0.66 · 250ms에 0.3 (InQuad면 0.85 · 0.6): 쾅 찍힌 뒤 넉백이 돌아오는 몸이 읽힌다
        /// </summary>
        const float PromotedCritFadeFrom = 0.35f;
        /// <summary>번쩍임 줄이기: 바꾼 치명타 그림은 0.22부터 걷힌다 (0.33초 중 73ms부터 — 도장 면이 보통보다 일찍 줄어든다)</summary>
        const float PromotedCritFadeFromReduced = 0.22f;
        /// <summary>
        /// 번쩍임 줄이기의 치명타 그림 색 (PromoteImpact가 바꾼 그림 · GenericCritArt 별 폭발 그림): 그림의 255 흰빛이 (214, 209, 224) ≈ 밝기 212로 내려앉아
        /// '거의 흰색'(밝기 235 · 가장 어두운 채널 210 이상)에서 빠진다. r2 비평: 줄이기 설정의 치명타가 보통보다 11~27%만 덜 하얬다 —
        /// 치명타 그림 자체가 거의 흰 넓이를 품고 있어 (atk_jjakjjak_crit 8.7% · vfx_star_burst 6.9%) 빛 층을 줄여도 그대로 남았다. 계보 모양은 그대로 읽히고 조금 어두워질 뿐
        /// </summary>
        static readonly Color ReducedCritTint = new(0.84f, 0.82f, 0.88f, 1f);
        /// <summary>치명타 X 바늘땀 도장 불투명도 (0.95 → 0.80: 도장 아래 맞은 몸이 비친다)</summary>
        const float StitchStampAlpha = 0.80f;
        /// <summary>
        /// 작은 치명타(큰 터짐 상한을 넘긴 치명타)는 닿는 시각 둘레 0.1초(HitFeel.BigBurstWindow)에 이만큼까지 그림 · 코어 · 고리 · 빛줄기를 그린다.
        /// 그 뒤로는 크림 코어 별만 (바꾼 명중 그림은 그대로) — 범위 스킬이 치명타 20개를 한꺼번에 내도 투사체 머리 · 명중 그림 몫이 남게
        /// </summary>
        const int SmallCritsPerWindow = 6;
        /// <summary>작은 치명타 도착 시각 고리 버퍼 칸 수 (창 하나에 SmallCritsPerWindow개까지만 적으므로 창 몇 개 분량)</summary>
        const int SmallCritSlots = 32;
        /// <summary>명중 그림이 전장 가장자리에서 떨어지는 여백 (ATK-16)</summary>
        const float EdgeInset = 24f;
        /// <summary>총구 불꽃 자리: 가슴에서 쏘는 쪽으로</summary>
        const float MuzzleOffset = 18f;
        /// <summary>fx_slash_arc의 볼록한 쪽이 그림 +x에서 −78° (아래) — 볼록한 쪽이 대상을 향하도록 돌린다</summary>
        const float SlashArcBulge = -78f;

        static readonly Vector2 TipPivot = new(AttackFxCatalog.ShotTipPivot, 0.5f);
        /// <summary>투사체 아래 빛: 머리 끝보다 조금 뒤에 가운데</summary>
        static readonly Vector2 GlowUnderPivot = new(0.8f, 0.5f);
        static readonly Vector2 FlameBasePivot = new(0.5f, 0.12f);
        /// <summary>몽당이 촛불 빛 (#FFB86B)</summary>
        static readonly Color FlameAmber = new(1f, 0.722f, 0.42f, 1f);

        int _battleSpeed = 1;
        StageModifierType _stageModifier;
        float _lastWindup;
        bool _lastShotAccent;
        int _miniHits;

        // ── 마지막 기본 공격 명중 (같은 프레임 치명타가 치명타 그림으로 바꾸고, 대신할 층을 거둔다) ──
        int _impactFrame = -1;
        float _impactClock;
        int _impactTarget = -1;
        int _impactArt = -1;
        int _impactSlash = -1;
        int _impactCore = -1;
        int _impactRing = -1;
        int _impactBacking = -1;
        readonly int[] _impactStreaks = new int[8];
        int _impactStreakCount;
        bool _impactTracking;
        string _impactLineage;
        bool _impactEcho;
        bool _impactBoss;
        Vector2 _impactPoint;
        Vector2 _impactDir;
        /// <summary>그 계보의 일반 명중 크기 S (치명타 특수의 'S')</summary>
        float _impactHitSize;

        // ── 치명타로 바뀐 명중 (CritImpact가 그림을 다시 그리지 않고 계보 특수를 붙인다) ──
        int _promotedFrame = -1;
        float _promotedClock;
        int _promotedTarget = -1;
        int _promotedArt = -1;
        bool _promotedHasArt;
        string _promotedLineage;
        bool _promotedEcho;
        Vector2 _promotedPoint;
        Vector2 _promotedDir;
        float _promotedHitSize;

        // ── 작은 치명타 무리 (닿는 연출 시각 — 범위 스킬은 적마다 닿는 순간이 달라 뒤섞여 온다) ──
        readonly float[] _smallCrits = NeverTimes(SmallCritSlots);
        int _smallCritHead;

        /// <summary>배속·스테이지 기믹 (BattleView가 매 프레임 넣는다). 2배속이면 비행 0.6배, 명중 팝 0.8배</summary>
        public void SetBattlePace(int speed, StageModifierType modifier)
        {
            _battleSpeed = Mathf.Max(1, speed);
            _stageModifier = modifier;
        }

        float UnitScale => _mapper != null ? _mapper.ActorScale : 1f;

        float PopScale => AttackFxCatalog.SpeedPopScale(_battleSpeed);

        /// <summary>마지막 GuardianShot의 웅크림 시간 W (BattleView가 GuardianSlotView.PlayAttack(target, W)에 넘긴다)</summary>
        public float LastWindup => _lastWindup;

        /// <summary>
        /// 마지막 GuardianShot / EchoAttack의 명중이 리듬 강조(사슬 3번째마다 풀 명중 · 코어 ×1.15)로 그려졌는지 (E6).
        /// BattleView가 그 피격 숫자를 AttackFxCatalog.RhythmNumberScale(1.2)로 키운다
        /// </summary>
        public bool LastShotAccent => _lastShotAccent;

        // ───────────── 지킴이 ─────────────

        /// <summary>
        /// 지킴이 기본 공격 (E1 · E2 · E3/E4). 투사체가 대상에 닿기까지의 시간(초)을 돌려준다 — 명중은 그때 핀다.
        /// 웅크림 W(LastWindup) 뒤에 총구 불꽃과 투사체가 나가고 비행 − W 동안 날아간다
        /// </summary>
        public float GuardianShot(in AttackFxRequest r)
        {
            _lastWindup = 0f;
            _lastShotAccent = false;
            var target = r.Target;
            if (target == null) return 0f;
            if (!_ready) Prepare();
            var look = AttackFxCatalog.Look(r.LineageId);
            var sig = AttackFxCatalog.Signature(r.LineageId);
            var mod = AttackFxCatalog.Style(r.Style);
            var to = target.HitPoint;
            float flight = AttackFxCatalog.FlightTime(look, mod, (to - r.From).magnitude, _battleSpeed, UnitScale);
            float windup = AttackFxCatalog.Windup(flight, _battleSpeed);
            _lastWindup = windup;
            float form = r.FormTier >= 2 ? 1.1f : 1f;

            var launch = LaunchShot(r.From, target, look, sig, mod, windup, flight - windup, GuardianShotSize * form * UnitScale, false, default, out var arrival);
            Muzzle(r.From, launch, look, sig, mod.Straight, windup);
            ScheduleImpact(r, look, sig, mod, flight, arrival, false, false);
            return flight;
        }

        // ───────────── 잔향이 ─────────────

        /// <summary>
        /// 잔향이 기본 공격 (E8). 근접(방어·돌격·포획): 초승달 베기가 볼록한 쪽을 대상으로 향해 −35°→+15°로 쓸고, 꼭대기(50ms × PopScale)에서 0.65배 명중.
        /// 원거리(저격·지원·힐): 62px 투사체 0.14초 — 비행 시간을 돌려준다. 토큰은 짝짝이 명중 45%. 모든 잔향이 공격 아래 잔향이 빛(가산 0.35)
        /// </summary>
        public float EchoAttack(in AttackFxRequest r)
        {
            _lastShotAccent = false;
            var target = r.Target;
            if (target == null) return 0f;
            if (!_ready) Prepare();
            string lineage = r.Token ? "jjakjjak" : r.LineageId;
            var look = AttackFxCatalog.Look(lineage);
            var sig = AttackFxCatalog.Signature(lineage);
            var mod = AttackFxCatalog.Style(r.Style);
            var glow = r.Glow.a > 0f ? r.Glow : UITheme.EchoGlow;
            var to = target.HitPoint;
            var dir = AttackDir(r.From, to, Vector2.down);

            if (r.Token)
            {
                EchoGlowUnder(to, glow, 0f, target);
                ScheduleImpact(r, look, sig, mod, 0f, dir, true, false);
                return 0f;
            }

            if (!mod.Ranged)
            {
                float swing = EchoSwing * PopScale;
                EchoSlash(dir, target, look, glow);
                EchoGlowUnder(to, glow, 0f, target);
                ScheduleImpact(r, look, sig, mod, swing, dir, true, true);
                return swing;
            }

            float flight = EchoShotLife * AttackFxCatalog.SpeedFlightScale(_battleSpeed);
            LaunchShot(r.From, target, look, sig, mod, 0f, flight, GuardianShotSize * EchoRangedScale * UnitScale, true, glow, out var arrival);
            ScheduleImpact(r, look, sig, mod, flight, arrival, true, false);
            EchoGlowUnder(to, glow, flight, target);
            return flight;
        }

        /// <summary>
        /// 잔향이 근접 베기 (E8a): fx_slash_arc 110px (가로 2:1), 색 = lerp(계보 주색, 잔향이 빛, 0.3), 가산 0.85.
        /// 볼록한 쪽이 대상을 향하고 −35° → +15°를 120ms × PopScale에 쓸며 50%부터 사라진다
        /// </summary>
        void EchoSlash(Vector2 dir, EnemyView target, in LineageAttackLook look, Color glow)
        {
            var arc = _juice != null ? _juice.SlashArc : null;
            var sprite = arc != null ? arc : SlashSprite();
            float unit = UnitScale;
            float facing = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (arc != null ? -SlashArcBulge : 0f);
            float life = 0.12f * PopScale;
            float size = EchoSlashSize * unit;
            var tint = Color.Lerp(look.Main, glow, 0.3f);
            var at = target.HitPoint - dir * (12f * unit);
            float aspect = arc != null ? SlashArcAspect : sprite != null ? 1f : 0.3f;
            // 쓸림 번짐: 같은 초승달이 조금 크고 부드럽게 아래에 (가산, 잔향이 빛 쪽) — 가는 선 하나보다 휘두른 궤적이 읽힌다
            if (arc != null && Allow(FxPriority.Trail))
            {
                int smear = SpawnFx(arc, BattleEase.WithAlpha(Color.Lerp(glow, Color.white, 0.35f), 0.45f), at, size * 1.18f, size * 1.24f, life * 1.15f, 0f, 1f);
                if (smear >= 0) SweepSlash(smear, facing - 41f, 56f, aspect * 1.25f, size * 1.18f, life * 1.15f, target);
            }
            int cut = SpawnFx(sprite != null ? sprite : _trail, BattleEase.WithAlpha(tint, 0.95f), at, size, size * 1.04f, life, 0f, 0.85f);
            if (cut >= 0) SweepSlash(cut, facing - 35f, 50f, aspect, size, life, target);
        }

        /// <summary>fx_slash_arc 가로:세로 = 2:1 그림을 조금 더 도톰하게 (0.62) — 가는 선이 아니라 초승달 날로 보이게</summary>
        const float SlashArcAspect = 0.62f;

        /// <summary>베기 입자: startDeg에서 sweepDeg만큼 수명 동안 고르게 쓸고 50%부터 사라진다, 대상에 붙는다</summary>
        void SweepSlash(int index, float startDeg, float sweepDeg, float aspect, float size, float life, EnemyView target)
        {
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Linear;
            p.Aspect = aspect;
            p.Rotation = startDeg;
            p.Spin = sweepDeg / Mathf.Max(0.01f, life);
            p.FadeOut = 0.5f;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
            p.Rect.sizeDelta = new Vector2(size, size * aspect);
            AnchorTo(index, target);
        }

        /// <summary>잔향이 공격 아래 깔리는 빛 (가산 0.35, 정전 +0.25) — 작고 반투명한 빛의 존재</summary>
        void EchoGlowUnder(Vector2 at, Color glow, float delay, EnemyView target)
        {
            if (!Allow(FxPriority.Trail)) return;
            float unit = UnitScale;
            float intensity = 0.35f + (_stageModifier == StageModifierType.Blackout ? 0.25f : 0f);
            int index = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(glow, intensity), at, 50f * unit, 110f * unit, 0.28f * PopScale, delay, 1f);
            if (index >= 0)
            {
                _particles[index].Curve = SizeCurve.Out;
                _particles[index].FadeOut = 0.4f;
            }
            AnchorTo(index, target);
        }

        // ───────────── 쏘기 (E1) ─────────────

        /// <summary>
        /// 놓는 순간(delay = W)의 총구: ① 코어 빛 fx_glow_core 24→40px 50ms (가산, 흰빛 0.9) ② 계보 총구 불꽃 muzzle_{계보} 40→96px 120ms,
        /// 쏘는 방향으로 돌려 40%부터 사라진다 (가산 0.85 — 그림이 없으면 코어 별에 강조색) ③ 계보 빛줄기 2 (높음·보통). 저격은 ×1.3
        /// </summary>
        void Muzzle(Vector2 from, Vector2 dir, in LineageAttackLook look, in LineageSignature sig, bool snipe, float delay)
        {
            float unit = UnitScale;
            float scale = unit * (snipe ? 1.3f : 1f);
            var at = from + dir * (MuzzleOffset * unit);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(Color.white, 0.9f), at, 24f * scale, 40f * scale, 0.05f, delay, 1f);

            var art = _juice != null ? _juice.Muzzle(look.Id) : null;
            int flare = SpawnFx(art != null ? art : CoreStarSprite, art != null ? Color.white : look.Accent, at, 40f * scale, 96f * scale, 0.12f, delay, 0.85f);
            if (flare >= 0)
            {
                ref var p = ref _particles[flare];
                p.Rotation = angle;
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
                p.FadeOut = 0.4f;
                p.FadeKind = FadeInQuad;
            }

            if (_quality == FxQuality.Low || !Allow(FxPriority.Sparkle)) return;
            var tint = BattleEase.WithAlpha(sig.Spark, 0.9f);
            for (int i = 0; i < 2; i++)
            {
                float a = (angle + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                var velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Random.Range(260f, 380f) * unit);
                Streak(at, velocity, Random.Range(12f, 16f) * unit, 3f * unit, 0.16f, tint, delay, 0.6f, 6f);
            }
        }

        // ───────────── 비행 (E2) ─────────────

        /// <summary>
        /// 투사체 층 (아래에서 위로): ① 아래 빛(가산, 강조색 · 잔향이 빛) ② 빛 꼬리(저격 · 찰칵이, 가산 0.7) ③ 잔상 2(60ms 뒤, 0.84배, 높음)
        /// ④ 잔상 1(30ms 뒤, 0.92배, 높음·보통) ⑤ 머리(끝 기준점, 진행 방향으로 기운다 — 그려지는 맨 위) ⑥ 길 위에 흘리는 계보 조각.
        /// 모두 같은 길(진행도 휨 0.25)을 delay 뒤 life 동안. 처음 날아가는 방향을 돌려주고, 닿는 방향은 arrival로
        /// </summary>
        Vector2 LaunchShot(Vector2 from, EnemyView target, in LineageAttackLook look, in LineageSignature sig, in StyleAttackMod mod, float delay, float life,
            float headSize, bool echo, Color echoGlow, out Vector2 arrival)
        {
            var to = target.HitPoint;
            float distance = (to - from).magnitude;
            ShotShape(look, mod, out var shape, out var rhythm);
            var firstAim = ShotPosition(from, to, shape, rhythm, Warped(0.08f));
            var initial = AttackDir(from, firstAim, to - from);
            arrival = AttackDir(ShotPosition(from, to, shape, rhythm, Warped(0.9f)), to, to - from);
            life = Mathf.Max(0.03f, life);
            bool blackout = _stageModifier == StageModifierType.Blackout;
            var art = ShotSprite(look.Id);
            // 도는 그림(똑단이 단추, §13.5 a)은 진행 방향 늘림을 하지 않는다 — 도는 축에 늘림을 걸면 찌그러진 타원이 돈다 (넓이는 1.2 × 0.85 ≈ 1과 같다)
            bool spinArt = look.ArtSpin != 0f;
            float headWidth = spinArt ? headSize : headSize * mod.ShotStretch * ShotFlightStretchX;
            float headAspect = spinArt ? 1f : ShotFlightStretchY / (mod.ShotStretch * ShotFlightStretchX);

            // ① 아래 빛 (정전 스테이지에서는 더 밝게 — 빛 계보가 어둠 속에서 읽히게)
            if (Allow(FxPriority.Trail))
            {
                float intensity = 0.45f + (blackout ? 0.25f : 0f);
                int glow = SpawnShot(GlowCoreSprite, BattleEase.WithAlpha(echo ? echoGlow : look.Accent, intensity), from, target, shape, rhythm, headSize * 0.7f, 1f,
                    life, delay);
                if (glow >= 0)
                {
                    ref var p = ref _particles[glow];
                    p.Add = 1f;
                    p.AlignToPath = true;
                    p.Rect.pivot = GlowUnderPivot;
                    FaceTo(glow, from, firstAim);
                }
            }

            // ② 빛 꼬리 (잔향이는 저격일 때만 — §13.5 b)
            bool streak = AttackFxCatalog.DrawsStreak(look, mod, echo);
            if (streak && Allow(FxPriority.Trail))
            {
                var trailArt = SnipeTrailSprite();
                float length = distance * (mod.SnipeTrail ? 0.6f : 0.35f) * (echo ? 0.5f : 1f);
                var color = trailArt != null ? BattleEase.WithAlpha(Color.white, echo ? 0.7f : 0.95f) : BattleEase.WithAlpha(look.Main, 0.85f);
                int tail = SpawnShot(trailArt != null ? trailArt : StreakSprite, color, from, target, shape, rhythm, Mathf.Max(24f, length * 0.25f), 0.25f, life, delay);
                if (tail >= 0)
                {
                    ref var t = ref _particles[tail];
                    t.Size1 = Mathf.Max(30f, length);
                    t.Curve = SizeCurve.Linear;
                    t.AlignToPath = true;
                    t.Rect.pivot = TipPivot;
                    t.FadeOut = 0.75f;
                    t.Add = 0.7f;
                    FaceTo(tail, from, firstAim);
                }
            }
            else if (art == null && Allow(FxPriority.Trail))
            {
                int tail = SpawnShot(StreakSprite, BattleEase.WithAlpha(look.Main, 0.8f), from, target, shape, rhythm, headSize * 0.8f, 0.3f, life, delay + 0.02f);
                if (tail >= 0)
                {
                    ref var t = ref _particles[tail];
                    t.AlignToPath = true;
                    t.Rect.pivot = new Vector2(0.9f, 0.5f);
                    t.Add = 0.6f;
                }
            }

            // ③ · ④ 잔상 (머리 그림이 30/60ms 늦게 같은 길을 — 진행 방향으로 번지는 꼬리)
            if (art != null)
            {
                int after = echo ? (_quality == FxQuality.High ? 1 : 0) : JuiceGates.Afterimages(_quality);
                if (after >= 2 && Allow(FxPriority.Trail))
                    Afterimage(art, look, from, firstAim, target, shape, rhythm, headWidth * 0.84f, headAspect, life, delay + Mathf.Min(0.06f, life * 0.5f), 0.22f);
                if (after >= 1 && Allow(FxPriority.Trail))
                    Afterimage(art, look, from, firstAim, target, shape, rhythm, headWidth * 0.92f, headAspect, life, delay + Mathf.Min(0.03f, life * 0.25f), 0.45f);
            }

            // ⑤ 머리 (비행 중 가로로 늘고 세로로 눌린다, 끝이 명중점에 닿도록 끝을 기준점으로 — 도는 그림(똑단이 단추, §13.5 a)은 가운데를 축으로 돈다)
            if (art != null)
            {
                int head = SpawnShot(art, echo ? BattleEase.WithAlpha(Color.white, 0.9f) : Color.white, from, target, shape, rhythm, headWidth, headAspect, life, delay);
                if (head >= 0) PoseArtShot(head, look, from, firstAim);
            }
            else FallbackShot(from, target, look, shape, rhythm, headSize, life, delay);

            // ⑥ 흘리는 계보 조각 (높음 2 / 보통 1)
            if (!echo) ShedMotes(from, target, shape, rhythm, sig, JuiceGates.ShedMotes(_quality), life, delay);

            // 비 오는 날: 다른 계보 투사체 꼬리에도 물방울 하나 (ATK-14)
            if (_stageModifier == StageModifierType.Rain && look.Extra != AttackExtra.Ripple && Allow(FxPriority.Sparkle))
            {
                int drop = SpawnShot(Motif(VfxMotif.Drop), BattleEase.WithAlpha(UITheme.SoftBlue, 0.85f), from, target, shape, rhythm, 14f * UnitScale, 1f, life,
                    delay + life * 0.15f);
                if (drop >= 0) _particles[drop].Rotation = 0f;
            }
            return initial;
        }

        /// <summary>계보 길 모양: shape = (호 높이, 살랑임 폭) px, rhythm = (살랑임 횟수, 튀는 횟수)</summary>
        void ShotShape(in LineageAttackLook look, in StyleAttackMod mod, out Vector2 shape, out Vector2 rhythm)
        {
            var path = mod.Straight ? AttackPath.Straight : look.Path;
            float arc = path == AttackPath.Straight ? 0f : look.ArcPx;
            // 곧은 계보라도 지원·힐·포획은 높게 던진다
            if (!mod.Straight && mod.ArcScale > 1f && arc <= 0f) arc = 24f;
            arc *= mod.Straight ? 0f : mod.ArcScale;
            float wobble = path == AttackPath.Wobble || path == AttackPath.Bounce ? look.WobblePx : 0f;
            shape = new Vector2(arc * UnitScale, wobble * UnitScale);
            rhythm = new Vector2(path == AttackPath.Wobble ? 2f : path == AttackPath.Bounce ? 3f : 0f, path == AttackPath.Bounce ? 2f : 1f);
        }

        /// <summary>시간 진행 k에서 투사체가 있는 길 위 진행도 (Motion.Shot의 Warp와 같은 식)</summary>
        static float Warped(float k) => k * (1f - ShotWarp + ShotWarp * k);

        /// <summary>잔상 하나: 머리 그림, 가산 0.5, 세기 intensity, 60%부터 사라진다 (자세는 머리와 같다 — PoseArtShot)</summary>
        void Afterimage(Sprite art, in LineageAttackLook look, Vector2 from, Vector2 firstAim, EnemyView target, Vector2 shape, Vector2 rhythm, float width, float aspect,
            float life, float delay, float intensity)
        {
            int index = SpawnShot(art, BattleEase.WithAlpha(Color.white, intensity), from, target, shape, rhythm, width, aspect, life, delay);
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Add = 0.5f;
            p.FadeOut = 0.6f;
            PoseArtShot(index, look, from, firstAim);
        }

        /// <summary>
        /// 그림 투사체 머리 · 잔상의 자세 (AttackFxCatalog.ArtShotPose): 보통은 끝 기준점 + 진행 방향을 보고, ArtSpin이 있는 계보(똑단이 720°/초)는
        /// 가운데 기준점에서 그 빠르기로 돈다 (SPEC4 §13.5 a — 예전에는 그림이 있으면 단추가 돌지 않았다)
        /// </summary>
        void PoseArtShot(int index, in LineageAttackLook look, Vector2 from, Vector2 firstAim)
        {
            AttackFxCatalog.ArtShotPose(look, out var pivot, out bool align, out float spin);
            ref var p = ref _particles[index];
            p.AlignToPath = align;
            p.Spin = spin;
            p.Rect.pivot = pivot;
            if (align) FaceTo(index, from, firstAim);
        }

        /// <summary>
        /// 비행 중 흘리는 계보 조각 (E2 6행): 머리가 k = 0.35 / 0.7(하나면 0.5)을 지나는 순간 그 자리에서 뒤로 30px 흘러가며 0.3초 (가산 0.4, 별빛 층)
        /// </summary>
        void ShedMotes(Vector2 from, EnemyView target, Vector2 shape, Vector2 rhythm, in LineageSignature sig, int count, float life, float delay)
        {
            if (count <= 0) return;
            var to = target.HitPoint;
            var back = AttackDir(to, from, Vector2.left);
            float unit = UnitScale;
            for (int i = 0; i < count; i++)
            {
                if (!Allow(FxPriority.Sparkle)) return;
                float k = count == 1 ? 0.5f : i == 0 ? 0.35f : 0.7f;
                var at = ShotPosition(from, to, shape, rhythm, Warped(k));
                var travel = (back * 30f + new Vector2(Random.Range(-8f, 8f), Random.Range(-6f, 6f))) * unit;
                int index = ShardMote(sig, at, travel, 0.3f, delay + k * life, 1f);
                if (index < 0) continue;
                _particles[index].Add = 0.4f;
                if (sig.Shard == LineageShard.NeedleTick)
                {
                    // 바늘 획은 길을 따라 눕는다
                    var tangent = ShotPosition(from, to, shape, rhythm, Warped(Mathf.Min(1f, k + 0.05f))) - at;
                    if (tangent.sqrMagnitude > 0.01f) _particles[index].Rotation = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                }
            }
        }

        /// <summary>
        /// 계보 조각 하나 (§1.21): 바늘 획 · 단추 · 금빛 반짝 · 불씨 · 물방울 · 하트 · 음표. travel만큼 흘러가며 계보 중력(음수 = 떠오름)으로 휜다.
        /// 크기 = 계보 조각 크기 × scale × U (±15%)
        /// </summary>
        int ShardMote(in LineageSignature sig, Vector2 at, Vector2 travel, float life, float delay, float scale)
        {
            float unit = UnitScale;
            float size = sig.ShardSize * Random.Range(0.85f, 1.15f) * scale * unit;
            var bend = Vector2.down * (0.5f * sig.ShardGravity * life * life * unit);
            var color = BattleEase.WithAlpha(sig.ShardColor, 0.85f);
            Sprite sprite;
            switch (sig.Shard)
            {
                case LineageShard.NeedleTick:
                    sprite = StreakSprite;
                    break;
                case LineageShard.Button:
                    sprite = Motif(VfxMotif.Button);
                    break;
                case LineageShard.Ember:
                    sprite = Motif(VfxMotif.Flame);
                    break;
                case LineageShard.Drop:
                    sprite = Motif(VfxMotif.Drop);
                    break;
                case LineageShard.Heart:
                    sprite = Motif(VfxMotif.Heart);
                    break;
                case LineageShard.Note:
                    sprite = Motif(VfxMotif.Note);
                    break;
                default:
                    sprite = _juice != null && _juice.MoteTwinkle != null ? _juice.MoteTwinkle : _sparkle;
                    break;
            }
            int index = Drift(sprite, color, at, travel, bend, size, size * 0.6f, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.FadeIn = 0.05f;
            p.FadeOut = 0.5f;
            switch (sig.Shard)
            {
                case LineageShard.NeedleTick:
                    p.Size0 = size * 1.2f;
                    p.Size1 = size;
                    p.Height0 = 3f * unit;
                    p.Height1 = 2f * unit;
                    p.Spin = 0f;
                    p.Rotation = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg;
                    break;
                case LineageShard.Button:
                    p.Spin = Random.Range(-540f, 540f);
                    break;
                case LineageShard.Twinkle:
                    p.Pulse = 0.2f;
                    p.PulseHz = 8f;
                    p.Spin = Random.Range(-90f, 90f);
                    break;
                default:
                    // 불씨 · 물방울 · 하트 · 음표는 똑바로 선 채 (살짝만 기운다)
                    p.Spin = Random.Range(-30f, 30f);
                    p.Rotation = Random.Range(-12f, 12f);
                    break;
            }
            return index;
        }

        /// <summary>그림이 없을 때: 계보 모양(바늘 빛줄기·단추·불꽃·물방울·하트·음표)에 계보 색 + 빛 구슬</summary>
        int FallbackShot(Vector2 from, EnemyView target, in LineageAttackLook look, Vector2 shape, Vector2 rhythm, float size, float life, float delay)
        {
            var motif = Motif(look.ShotMotif);
            bool streak = look.ShotMotif == VfxMotif.Trail;
            var head = Color.Lerp(look.Main, Color.white, 0.25f);
            int glow = SpawnShot(GlowCoreSprite, BattleEase.WithAlpha(Color.Lerp(look.Accent, Color.white, 0.35f), 0.9f), from, target, shape, rhythm, size * 0.5f, 1f,
                life, delay);
            if (glow >= 0) _particles[glow].Add = 1f;
            int index = SpawnShot(motif, streak ? look.Sub : head, from, target, shape, rhythm, streak ? size * 1.1f : size * 0.45f, streak ? 0.26f : 1f, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            // 빛줄기(바늘·열쇠)만 진행 방향으로 눕고, 단추는 팽이처럼 돌고, 나머지 모양(불꽃·물방울·하트·음표)은 똑바로 선 채 날아간다
            p.AlignToPath = streak;
            p.Spin = look.FallbackSpin;
            if (streak) p.Rect.pivot = new Vector2(0.92f, 0.5f);
            return index;
        }

        /// <summary>계보 길을 따라 대상을 쫓아가는 입자 (Motion.Shot, 진행도 휨 0.25). delay 뒤 life 동안</summary>
        int SpawnShot(Sprite sprite, Color color, Vector2 from, EnemyView target, Vector2 shape, Vector2 rhythm, float size, float aspect, float life, float delay)
        {
            int index = Spawn(sprite, color, from, size, size, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Motion = Motion.Shot;
            p.Target = target;
            p.TargetId = target.Id;
            p.P1 = shape;
            p.P2 = target.HitPoint;
            p.P3 = rhythm;
            p.Aspect = aspect;
            p.Rotation = 0f;
            p.FadeIn = 0.01f;
            p.FadeOut = 0.92f;
            p.Warp = ShotWarp;
            // 첫 갱신 전 한 프레임도 비율이 맞게 (가로로 긴 빛 꼬리가 네모로 보이지 않게)
            p.Rect.sizeDelta = new Vector2(size, size * aspect);
            return index;
        }

        /// <summary>
        /// 투사체 위치: 출발 → 대상으로 거의 일정한 빠르기 + 호(hops = 통통 튀는 횟수) + 옆 살랑임(닿을수록 잦아든다).
        /// shape = (호 높이, 살랑임 폭), rhythm = (살랑임 횟수, 튀는 횟수)
        /// </summary>
        static Vector2 ShotPosition(Vector2 from, Vector2 to, Vector2 shape, Vector2 rhythm, float k)
        {
            float t = Mathf.Clamp01(k);
            var delta = to - from;
            var position = from + delta * t;
            if (shape.x != 0f)
            {
                int hops = Mathf.Max(1, Mathf.RoundToInt(rhythm.y));
                float lift = hops <= 1 ? Mathf.Sin(t * Mathf.PI) : Mathf.Abs(Mathf.Sin(t * Mathf.PI * hops)) * (1f - 0.35f * t);
                position.y += shape.x * lift;
            }
            if (shape.y != 0f && rhythm.x > 0f && delta.sqrMagnitude > 1f)
            {
                var side = new Vector2(-delta.y, delta.x).normalized;
                position += side * (shape.y * Mathf.Sin(t * Mathf.PI * 2f * rhythm.x) * (1f - t));
            }
            return position;
        }

        /// <summary>한 번 튀는 조각: 첫 착지까지 포물선, 착지하면 탄성만큼 한 번 튀고 바닥에 멈춘다</summary>
        static Vector2 BouncePosition(Vector2 start, Vector2 velocity, float groundY, float gravity, float restitution, float time)
        {
            if (gravity <= 0f) return start + velocity * time;
            float height = Mathf.Max(0f, start.y - groundY);
            float vy = velocity.y;
            float land = (vy + Mathf.Sqrt(vy * vy + 2f * gravity * height)) / gravity;
            if (time <= land) return new Vector2(start.x + velocity.x * time, start.y + vy * time - 0.5f * gravity * time * time);
            float x = start.x + velocity.x * land;
            float up = -(vy - gravity * land) * Mathf.Clamp01(restitution);
            float after = time - land;
            float y = Mathf.Max(groundY, groundY + up * after - 0.5f * gravity * after * after);
            return new Vector2(x + velocity.x * 0.55f * after, y);
        }

        /// <summary>from → to 방향 (거의 같은 자리면 fallback 방향, 그것도 없으면 아래)</summary>
        static Vector2 AttackDir(Vector2 from, Vector2 to, Vector2 fallback)
        {
            var delta = to - from;
            if (delta.sqrMagnitude > 0.01f) return delta.normalized;
            return fallback.sqrMagnitude > 1e-4f ? fallback.normalized : Vector2.down;
        }

        // ───────────── 명중 (E3 · E4 · E6 · E8) ─────────────

        /// <summary>
        /// 도착 순간(delay)에 대상 몸에서 피는 명중. 크기 S = HitSize × 1.35(ImpactScale) × 성향 × 형태 × 보스(1.2) × U × (잔향이 0.65 · 보호막 0.7 · 토큰 0.45),
        /// 자리 = 맞은 자리 ± (10, 8) − 날아온 방향 × 12 (공격자 쪽). 등급 수치는 HitFeel.Feel(일반 · 강타)과 보정(ForEcho · ForGuarded · ForBoss).
        /// 풀 명중: ① 잉크 받침 ② 명중 그림 ③ 코어 별 ④ 계보 고리 ⑤ 빛줄기 ⑥ 성향 변형 · 곁들임 ⑦ 반짝임.
        /// 미니(같은 대상 90ms 안 · 밀도 8 넘게): 0.65배 그림(×1.20, 17ms) + 코어 0.35 + 빛줄기 2 + 곁들임은 두 번에 한 번. 린(밀도 16 넘게): 그림 + 코어만
        /// </summary>
        void ScheduleImpact(in AttackFxRequest r, in LineageAttackLook look, in LineageSignature sig, in StyleAttackMod mod, float delay, Vector2 dir, bool echo,
            bool melee)
        {
            var target = r.Target;
            float unit = UnitScale;
            float pop = PopScale;
            bool shielded = target.State != null && target.State.Shield > 0.01f;
            bool boss = target.IsBoss;
            bool rainy = _stageModifier == StageModifierType.Rain && look.Extra == AttackExtra.Ripple;
            var tier = echo ? HitTier.Normal : mod.Tier;
            bool heavy = tier == HitTier.Heavy;
            var feel = HitFeel.Feel(tier);
            if (echo) feel = HitFeel.ForEcho(feel);
            if (shielded) feel = HitFeel.ForGuarded(feel);
            if (boss) feel = HitFeel.ForBoss(feel);
            float hitSize = look.HitSize * ImpactScale * unit * (boss ? 1.2f : 1f);
            float baseSize = hitSize * mod.HitScale * (r.FormTier >= 2 ? 1.1f : 1f) * (rainy ? 1.2f : 1f) * (r.Impression ? HitFeel.FirstImpressionScale : 1f);
            float size = r.Token ? baseSize * TokenHitScale * (shielded ? 0.7f : 1f) : baseSize * feel.SizeScale;
            dir = AttackDir(Vector2.zero, dir, Vector2.down);

            var jitter = new Vector2(Random.Range(-10f, 10f), Random.Range(-8f, 8f)) * unit;
            var point = target.HitPoint + jitter - dir * (HitFeel.ImpactTowardAttacker * unit);
            if (!ClampToField(ref point))
            {
                // 전장 밖 명중 (긴 사거리 저격): 가장자리에 계보 강조색 쐐기만
                _impactFrame = -1;
                int wedge = Flash(point, _sparkle, look.Accent, 24f, 30f, 0.2f, delay);
                if (wedge >= 0)
                {
                    _particles[wedge].Rotation = 45f;
                    _particles[wedge].Add = 0.6f;
                }
                return;
            }

            // 밀도 · 대상별 풀 명중 · 리듬 강조 (§2.4 / E6)
            int density = Density(delay);
            NoteHit(delay);
            bool slot = TryFullImpact(target.Id, delay);
            bool accent = RhythmAccent;
            bool lean = density > HitFeel.DensityLean;
            bool full = !lean && (accent || (slot && density <= HitFeel.DensityMini));
            _lastShotAccent = full && accent;

            BeginImpactRecord(target.Id, look.Id, echo, boss, point, dir, hitSize);

            var art = LineageImpactSprite(look.Id);
            // 닿는 첫 프레임의 코어만 거의 하얗게 (크림 0.8 — 몽당이처럼 따뜻한 계보도 하얀 불빛이 읽히게), 빛줄기는 계보 색이 남게 크림 0.3
            var coreTint = BattleEase.WithAlpha(JuiceColors.TowardCream(look.Accent, 0.8f), 0.9f);
            var streakTint = BattleEase.WithAlpha(JuiceColors.TowardCream(look.Accent, 0.3f), 0.9f);
            // 잔향이 근접: 빛줄기가 베기 결(쓸리는 방향 = 나아가는 방향의 왼쪽 직각)을 따라간다
            var streakDir = melee ? new Vector2(-dir.y, dir.x) : dir;
            float streakCone = melee ? 40f : feel.StreakCone;

            // 명중 층은 닿는 프레임에 보이게 2ms 먼저 깨운다 (밀도·풀 명중 장부는 정확한 도착 시각으로)
            float at = Mathf.Max(0f, delay - ImpactLead);

            // 저격 킥: 닿는 순간 날아온 쪽으로 HitFeel.KickSnipe px (0.6초에 한 번)
            if (mod.Straight && !echo) Kick(dir, HitFeel.KickSnipe, KickChannel.Snipe, at);

            if (!full)
            {
                float miniSize = size * 0.65f;
                _impactArt = SpawnImpactArt(art, look, point, miniSize, 1.20f, 0.017f, 0.05f, 0.92f, 0.18f * pop, at, echo, target);
                _impactCore = CoreStar(point, coreTint, size * 0.35f, size * 0.25f, 0.033f * pop, at, echo ? 0.8f : 1f);
                AnchorTo(_impactCore, target);
                if (lean) return;
                _impactTracking = true;
                AtkStreaks(point, streakDir, 2, streakCone, feel.StreakSpeedMin, feel.StreakSpeedMax, streakTint, at, 20f, 4f, 10f, target, 0.12f, 0.15f);
                _impactTracking = false;
                if (shielded) ShieldRipple(point, target, at);
                if ((++_miniHits & 1) == 0 && Allow(FxPriority.Extra)) LineageExtra(look, point, target, miniSize, at, rainy, echo);
                if (r.Impression && _quality != FxQuality.Low) AtkSparkles(point, target, look, HitFeel.FirstImpressionSparkles, at);
                return;
            }

            // ① 잉크 받침 (바닥 — 파스텔 명중이 라벤더 광장 위에서 읽히는 어두운 주머니. 높음 모두 · 보통은 보스만 · 잔향이 없음)
            if (!echo && !r.Token && JuiceGates.InkBacking(_quality, boss))
            {
                _impactBacking = InkBacking(point, size * feel.BackingSize, feel.Backing, (heavy ? 0.2f : 0.16f) * pop, at);
                AnchorTo(_impactBacking, target);
            }

            // ② 명중 그림 (첫 프레임이 가장 크다)
            float artLife = (echo ? 0.2f : feel.Life) * pop;
            float settle = echo ? 0.06f : feel.Settle;
            _impactArt = SpawnImpactArt(art, look, point, size, feel.FirstFrame, feel.Hold, settle, 0.92f, artLife, at, echo, target);

            // ③ 코어 별 (가산 — 명중 순간의 하얀 불빛)
            float coreSize = (echo ? 0.4f * baseSize : feel.Core * size) * (accent ? 1.15f : 1f);
            _impactCore = CoreStar(point, coreTint, coreSize, coreSize * 0.7f, (echo ? 0.04f : feel.CoreLife) * pop, at, echo ? 0.8f : 1f);
            AnchorTo(_impactCore, target);

            // ④ 계보 고리 (일반: 높음 밀도 8 이하 · 보통은 리듬 강조만 / 강타: 낮음 빼고 모두)
            bool ring = !echo && !r.Token && (heavy ? _quality != FxQuality.Low : JuiceGates.NormalRing(_quality, density, accent));
            if (ring && Allow(FxPriority.Extra)) _impactRing = LineageRingFx(sig, point, target, size, heavy, at);

            // ⑤ 빛줄기 (나가는 쪽 원뿔 · 4 / 3 / 2, 강타 6, 잔향이 2)
            _impactTracking = true;
            AtkStreaks(point, streakDir, StreakCount(feel.Streaks), streakCone, feel.StreakSpeedMin, feel.StreakSpeedMax, streakTint, at, 22f, 5f, 10f, target,
                0.14f, 0.18f);
            _impactTracking = false;

            // ⑥ 성향 변형 · 보호막 · 사물 곁들임
            if (mod.Slash && !echo) _impactSlash = SlashOverlay(point, target, dir, look, size, at);
            if (mod.Straight && !echo) SnipePierce(point, dir, size, target, at);
            if (mod.Thread && Allow(FxPriority.Extra)) CaptureThread(point, target, look, at);
            if (mod.SlowRing && target.State != null && target.State.SlowTimer > 0f && Allow(FxPriority.Extra)) SlowRing(target, look, at);
            if (shielded) ShieldRipple(point, target, at);
            if (Allow(FxPriority.Extra)) LineageExtra(look, point, target, size, at, rainy, echo);
            if (r.FormTier >= 2) TierGlints(point, size, sig, target, at, echo);
            if (r.Mythic && Allow(FxPriority.Sparkle)) StarDust(point, target, at);

            // ⑦ 반짝임 (높음·보통: 일반 2 · 강타 3 · 잔향이 2)
            if (_quality != FxQuality.Low) AtkSparkles(point, target, look, (heavy ? 3 : 2) + (r.Impression ? HitFeel.FirstImpressionSparkles : 0), at);
        }

        /// <summary>이번 명중을 기억한다 (같은 프레임의 치명타가 그림을 바꾸고 대신할 층을 거둔다)</summary>
        void BeginImpactRecord(int targetId, string lineage, bool echo, bool boss, Vector2 point, Vector2 dir, float hitSize)
        {
            _impactFrame = Time.frameCount;
            _impactClock = _clock;
            _impactTarget = targetId;
            _impactArt = -1;
            _impactSlash = -1;
            _impactCore = -1;
            _impactRing = -1;
            _impactBacking = -1;
            _impactStreakCount = 0;
            _impactLineage = lineage;
            _impactEcho = echo;
            _impactBoss = boss;
            _impactPoint = point;
            _impactDir = dir;
            _impactHitSize = hitSize;
        }

        /// <summary>계보 명중 그림: v2(가운데가 열린 atk_{계보}_impact) → atk_{계보}_hit → null(흰색 모양)</summary>
        Sprite LineageImpactSprite(string lineageId)
        {
            var sprite = _juice != null ? _juice.Impact(lineageId) : null;
            return sprite != null ? sprite : HitSprite(lineageId);
        }

        /// <summary>
        /// 명중 그림 (SizeCurve.Impact): 첫 프레임 settled × first로 가장 크게 → hold 버팀 → settle 동안 settled로 → 끝까지 × end.
        /// 45%까지 불투명, 그 뒤 InQuad로 사라진다. ±15° 기울이고(똑바른 계보는 0) 수명 동안 −8° 비튼다. 그림이 없으면 계보 모양에 주색 + 강조색 고리
        /// </summary>
        int SpawnImpactArt(Sprite art, in LineageAttackLook look, Vector2 point, float settled, float first, float hold, float settle, float end, float life, float delay,
            bool echo, EnemyView target)
        {
            bool fallback = art == null;
            var sprite = fallback ? Motif(look.HitMotif) : art;
            float size = fallback ? settled * 0.62f : settled;
            var color = fallback ? BattleEase.WithAlpha(look.Main, echo ? 0.85f : 1f) : BattleEase.WithAlpha(Color.white, echo ? 0.92f : 1f);
            int index = SpawnImpactCurve(sprite, color, point, size, first, hold, settle, end, life, delay, 0.45f);
            if (index >= 0)
            {
                ref var p = ref _particles[index];
                p.Rotation = look.UprightHit ? 0f : Random.Range(-15f, 15f);
                p.Spin = look.UprightHit ? 0f : -8f / Mathf.Max(0.05f, life);
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
            }
            AnchorTo(index, target);
            if (fallback)
            {
                int ring = Shockwave(point, look.Accent, settled * 0.2f, settled * 0.8f, life, delay, 0.6f, 0.85f);
                AnchorTo(ring, target);
            }
            return index;
        }

        /// <summary>SizeCurve.Impact 입자 하나 (첫 프레임부터 꼭대기 크기, fadeFrom부터 InQuad로 사라진다)</summary>
        int SpawnImpactCurve(Sprite sprite, Color color, Vector2 point, float settled, float first, float hold, float settle, float end, float life, float delay,
            float fadeFrom)
        {
            int index = Spawn(sprite, color, point, settled * first, settled, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Impact;
            p.Hold = hold;
            p.Settle = settle;
            p.EndScale = end;
            p.Rotation = 0f;
            p.FadeIn = 0f;
            p.FadeOut = fadeFrom;
            p.FadeKind = FadeInQuad;
            p.Rect.sizeDelta = new Vector2(p.Size0, p.Size0);
            return index;
        }

        /// <summary>품질별 빛줄기 수: 등급 수(일반 4 · 강타 6 · 치명타 8, 잔향이는 절반)를 높음 4 / 보통 3 / 낮음 2 비율로</summary>
        int StreakCount(int perHigh)
        {
            if (perHigh <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(perHigh * JuiceGates.StreaksPerNormalHit(_quality) / 4f));
        }

        /// <summary>
        /// 빛줄기 count개 (fx_streak, Motion.Streak, 가산): dir 둘레 coneDeg(360 = 사방, 고르게)로 speedMin~speedMax px/s, drag로 느려지며 속도 방향으로 눕는다.
        /// 길이 · 두께 · 빠르기는 기준 px × U, 수명 life0~life1 × PopScale. 별빛 층이라 예산이 모자라면 2개까지만 (그 2개도 꼬리 층 여유 안에서만). 나타나는 순간까지 대상을 따라간다.
        /// 명중을 기억하는 동안(_impactTracking)은 칸 번호를 남긴다 (치명타가 거둔다)
        /// </summary>
        void AtkStreaks(Vector2 at, Vector2 dir, int count, float coneDeg, float speedMin, float speedMax, Color tint, float delay, float length, float thickness,
            float drag, EnemyView anchor, float life0, float life1)
        {
            if (count <= 0) return;
            float unit = UnitScale;
            float pop = PopScale;
            var forward = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up;
            float baseAngle = Mathf.Atan2(forward.y, forward.x);
            bool all = coneDeg >= 359f;
            float half = coneDeg * 0.5f * Mathf.Deg2Rad;
            float spin = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                // 처음 두 줄은 꼬리 층 여유까지 (예산이 거의 바닥이면 투사체 머리 · 명중 그림 몫 20칸을 남긴다), 나머지는 별빛 층
                if (!Allow(i < 2 ? FxPriority.Trail : FxPriority.Sparkle)) break;
                float angle = all
                    ? spin + (i + Random.Range(-0.3f, 0.3f)) * (Mathf.PI * 2f / count)
                    : baseAngle + (count == 1 ? 0f : Mathf.Lerp(-half, half, (i + Random.Range(0.15f, 0.85f)) / count));
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.Range(speedMin, speedMax) * unit);
                float life = Random.Range(life0, life1) * pop;
                int index = Streak(at, velocity, length * Random.Range(0.82f, 1.18f) * unit, thickness * Random.Range(0.8f, 1.2f) * unit, life, tint, delay, 1f, drag);
                AnchorUntilShown(index, anchor, delay);
                if (_impactTracking && index >= 0 && _impactStreakCount < _impactStreaks.Length) _impactStreaks[_impactStreakCount++] = index;
            }
        }

        /// <summary>
        /// 일반 명중 고리 (E3 4행 / §1.21): 짝짝이·똑단이 = 바늘땀 실 고리, 찰칵이 금 · 몽당이 촛불빛 · 루즈리 주색 충격 고리, 우두커니 = 발밑 물결,
        /// 라라벨 = 음파 고리. 0.4 S → 1.1 S (강타 1.3 S), 16ms 뒤 160ms (강타 180ms) × PopScale, OutQuart, 30%부터 사라진다.
        /// 색은 계보 고리 색 그대로 (크림을 섞지 않는다). 빛 충격 고리(찰칵이 · 몽당이 · 루즈리)는 가산 0.6 · 세기 0.75 (강타 0.9) — 2차 조율: 크림 0.2 ·
        /// 가산 0.85 · 0.6에서는 계보마다 같은 얇은 흰 가산 원('비눗방울')으로 보였다. 실 · 음파 · 바닥 고리 세기는 그대로 (올리면 오히려 더 하얗게 떴다).
        /// 하얀 빛은 닿는 첫 프레임의 코어 별만
        /// </summary>
        int LineageRingFx(in LineageSignature sig, Vector2 point, EnemyView target, float size, bool heavy, float delay)
        {
            float pop = PopScale;
            float to = size * (heavy ? 1.3f : 1.1f);
            float life = (heavy ? 0.18f : 0.16f) * pop;
            float intensity = heavy ? 0.8f : 0.6f;
            float shockIntensity = heavy ? 0.9f : 0.75f;
            float at = delay + 0.016f * pop;
            var tint = sig.RingTint;
            int index;
            switch (sig.Ring)
            {
                case LineageRing.Thread:
                    // 바늘땀 고리는 칠한 실 그림이라 반쯤만 빛으로
                    index = Shockwave(point, tint, size * 0.4f, to, life, at, 0.55f, Mathf.Min(1f, intensity + 0.25f), 1f, false, RingThreadSprite);
                    break;
                case LineageRing.SoundRing:
                    // 라라벨 음파 고리: 라벤더 섞은 흰빛(TierRingTint와 같은 집안) · 가산 0.4 · 세기 +0.1 (0.7 / 강타 0.9) — Wave 3 (fx2 leftovers):
                    // 흰색 · 가산 0.6 · +0.25는 +50~150ms에 두꺼운 흰 '비눗방울'로 남았다 (0~250ms 거의 흰 화소 합 36.7 / 40.2, 다른 계보 1.3~16.4).
                    // 흰빛은 첫 프레임 코어 별만
                    index = Shockwave(point, SoundRingTint, size * 0.4f, to, life, at, SoundRingAdd, Mathf.Min(1f, intensity + SoundRingBoost), 1f, false,
                        SoundRingSprite());
                    if (index >= 0) _particles[index].Rotation = 0f;
                    break;
                case LineageRing.Ground:
                    index = Shockwave(target.Rect.anchoredPosition, tint, size * 0.4f, to * 1.15f, life * 1.4f, at, 0.7f, intensity + 0.1f, GroundAspect, true);
                    break;
                default:
                    index = Shockwave(point, tint, size * 0.4f, to, life, at, 0.6f, shockIntensity);
                    break;
            }
            AnchorTo(index, target);
            return index;
        }

        /// <summary>라라벨 음파 고리 색 = 흰색 → 라벤더 35% (Wave 3 — 하얀 비눗방울 대신 소리 물결)</summary>
        public static readonly Color SoundRingTint = Color.Lerp(Color.white, UITheme.Lavender, 0.35f);
        /// <summary>라라벨 음파 고리 가산 비율 / 세기 덤 (예전 0.6 / +0.25)</summary>
        public const float SoundRingAdd = 0.4f;
        public const float SoundRingBoost = 0.1f;

        /// <summary>바늘땀 실 고리 그림 (fx_ring_thread → 빛 충격 고리)</summary>
        Sprite RingThreadSprite => Or(_juice != null ? _juice.RingThread : null, ShockwaveSprite);

        /// <summary>음파 고리 그림 (vfx_sound_ring, 없으면 null → 빛 충격 고리)</summary>
        Sprite SoundRingSprite()
        {
            Sprite art = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(SkillVfx.SoundRing, out art, out isArt);
            return isArt ? art : null;
        }

        /// <summary>반짝임 (E3 7행): fx_sparkle 14~22px, 위로 흩어지며 0.3~0.42초 (가산 0.7, 강조색 / 크림 번갈아). 보름달·축제 +1</summary>
        void AtkSparkles(Vector2 point, EnemyView target, in LineageAttackLook look, int count, float delay)
        {
            if (_stageModifier == StageModifierType.FullMoon || _stageModifier == StageModifierType.Festival) count++;
            float unit = UnitScale;
            for (int i = 0; i < count; i++)
            {
                if (!Allow(FxPriority.Sparkle)) return;
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.7f + 0.3f;
                float life = Random.Range(0.3f, 0.42f);
                int spark = Drift(_sparkle, i % 2 == 0 ? look.Accent : UITheme.Cream, point, dir * (Random.Range(60f, 110f) * life * 1.3f * unit),
                    Vector2.down * (15f * life * life * unit), Random.Range(14f, 22f) * unit, 4f * unit, life, delay);
                if (spark < 0) continue;
                _particles[spark].Add = 0.7f;
                AnchorUntilShown(spark, target, delay);
            }
        }

        /// <summary>
        /// 돌격 명중 (E4): 초승달 베기 fx_slash_arc (→ atk_slash) 1.05 S, 볼록한 쪽을 대상 ±20°로 두고 90ms에 +40° 쓴다 (가산 0.85, lerp(주색, 흰빛, 0.25)),
        /// 50%부터 사라진다
        /// </summary>
        int SlashOverlay(Vector2 point, EnemyView target, Vector2 dir, in LineageAttackLook look, float size, float delay)
        {
            var arc = _juice != null ? _juice.SlashArc : null;
            var sprite = arc != null ? arc : SlashSprite();
            if (sprite == null) return -1;
            float pop = PopScale;
            float facing = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (arc != null ? -SlashArcBulge : 0f);
            float life = 0.13f * pop;
            int cut = SpawnFx(sprite, BattleEase.WithAlpha(Color.Lerp(look.Main, Color.white, 0.25f), 0.95f), point, size * 0.92f, size * 1.05f, life, delay, 0.85f);
            if (cut < 0) return -1;
            ref var p = ref _particles[cut];
            p.Curve = SizeCurve.Snap;
            p.Aspect = arc != null ? SlashArcAspect : 1f;
            p.Rotation = facing + Random.Range(-20f, 20f) - 20f;
            p.Spin = 40f / (0.09f * pop);
            p.FadeOut = 0.5f;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
            p.Rect.sizeDelta = new Vector2(size * 0.92f, size * 0.92f * p.Aspect);
            AnchorTo(cut, target);
            return cut;
        }

        /// <summary>저격 명중 (E4): 날아온 방향을 따라 꿰뚫는 가로 섬광 1.6 S × 0.25 S, 70ms (가산, 크림 0.9). 다른 명중 층처럼 대상에 붙는다</summary>
        void SnipePierce(Vector2 point, Vector2 dir, float size, EnemyView target, float delay)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            AnchorTo(Flare(point, JuiceColors.CoreCream, 1.6f * size, 0.25f * size, angle, 0.07f * PopScale, delay, 0.9f), target);
        }

        /// <summary>포획 명중: 명중점에서 발밑까지 실 끈이 0.25초 묶였다 풀린다</summary>
        void CaptureThread(Vector2 point, EnemyView target, in LineageAttackLook look, float delay)
        {
            var feet = target.Rect.anchoredPosition;
            float length = Mathf.Max(16f, point.y - feet.y);
            int thread = Spawn(_trail, BattleEase.WithAlpha(look.GroundColor, 0.9f), new Vector2(point.x, (point.y + feet.y) * 0.5f), length, length, 0.25f, delay);
            if (thread < 0) return;
            ref var p = ref _particles[thread];
            p.Rotation = -90f;
            p.Height0 = 7f;
            p.Height1 = 4f;
            p.FadeIn = 0.02f;
            p.FadeOut = 0.4f;
            AnchorTo(thread, target);
        }

        /// <summary>방어 명중: 감속이 걸려 있으면 발밑에 보조색 납작 고리 0.35초 (바닥 레이어)</summary>
        void SlowRing(EnemyView target, in LineageAttackLook look, float delay)
        {
            var feet = target.Rect.anchoredPosition;
            int ring = Spawn(_ring, BattleEase.WithAlpha(look.GroundColor, 0.75f), feet, 40f * UnitScale, 96f * UnitScale, 0.35f, delay, true);
            if (ring < 0) return;
            ref var p = ref _particles[ring];
            p.Aspect = GroundAspect;
            p.Rotation = 0f;
            p.FadeIn = 0.01f;
            AnchorTo(ring, target);
        }

        /// <summary>보호막 적 명중 (ATK-11 / E15): 소프트 블루 육각 물결 고리 40 → 90px 0.18초 (가산 0.6, 육각 모양이 없으면 거품 테)</summary>
        void ShieldRipple(Vector2 point, EnemyView target, float delay)
        {
            var hex = _library != null ? _library.Motif(VfxMotif.HexRing) : null;
            int ring = Spawn(hex != null ? hex : Motif(VfxMotif.Bubble), BattleEase.WithAlpha(UITheme.SoftBlue, 0.9f), point, 40f * UnitScale, 90f * UnitScale, 0.18f,
                delay);
            if (ring < 0) return;
            ref var p = ref _particles[ring];
            p.Rotation = 0f;
            p.FadeIn = 0.01f;
            p.Add = 0.6f;
            AnchorTo(ring, target);
        }

        /// <summary>2차 진화형 금빛 반짝임 수 (높음 4 · 보통 3 · 낮음 2 — 명중 테두리를 고르게 두른다)</summary>
        int TierGlintCount => _quality == FxQuality.High ? 4 : _quality == FxQuality.Medium ? 3 : 2;

        /// <summary>
        /// 2차 진화형 (진화가 눈에 보이는 성장 보상 — 2차 조율: t0과 +50ms 모습이 같았다): 명중 테두리를 고르게 두르는 금빛 반짝임 4개 (보통 3 · 낮음 2,
        /// 44px × U → 8px, 0.4초, 30ms 간격, 가산 0.7, 별빛 층) + 30ms 뒤 엷은 고리 하나 (TierRingTint — 계보 고리 색에 크림 0.15, 0.5 S → 1.35 S,
        /// 180ms × PopScale, 가산 0.5 · 세기 0.6, 곁들임 층 · 낮음 품질 없음 — 강타 고리와 같은 문. 잔향이는 고리 없이 반짝임만: 잔향이 명중은 더 가볍다).
        /// r2 비평: lerp(강조색, 크림, 0.5) · 가산 0.8 · 세기 0.55는 강조색이 크림(루즈리) · 금(똑단이 · 우두커니 · 라라벨)이라 하얀 고리로 떠서
        /// t2의 거의 흰 넓이가 +33~83ms에 t0의 1.7~2.4배였다 — 계보 고리 색을 거의 그대로 두고 가산을 낮춰 칠한 고리로 (금빛 반짝임은 그대로)
        /// </summary>
        void TierGlints(Vector2 point, float size, in LineageSignature sig, EnemyView target, float delay, bool echo)
        {
            float unit = UnitScale;
            float pop = PopScale;
            if (!echo && _quality != FxQuality.Low && Allow(FxPriority.Extra))
                AnchorTo(Shockwave(point, TierRingTint(sig), 0.5f * size, 1.35f * size, 0.18f * pop, delay + 0.03f * pop, 0.5f, 0.6f), target);
            int count = TierGlintCount;
            float spin = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                if (!Allow(FxPriority.Sparkle)) return;
                float angle = spin + (i + Random.Range(-0.2f, 0.2f)) * (Mathf.PI * 2f / count);
                var at = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (size * 0.42f);
                int glint = Spawn(_sparkle, UITheme.Gold, at, 44f * unit, 8f * unit, 0.40f, delay + i * 0.03f, false, true);
                if (glint < 0) continue;
                ref var p = ref _particles[glint];
                p.FadeIn = 0.01f;
                p.Spin = 260f;
                p.Add = 0.7f;
                AnchorTo(glint, target);
            }
        }

        /// <summary>
        /// 진화 고리 색 = lerp(계보 고리 색, 크림, 0.15). 고리 색이 흰색이면 (라라벨 — 음파 고리는 그림 색 그대로 그려서 RingTint가 흰색) 라벤더로:
        /// 진화 고리는 흰 빛 충격 고리 그림에 색을 입히므로 흰색 그대로면 하얀 비눗방울이 된다
        /// </summary>
        static Color TierRingTint(in LineageSignature sig)
        {
            var tint = sig.RingTint;
            if (Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b)) > 0.9f) tint = UITheme.Lavender;
            return Color.Lerp(tint, UITheme.Cream, 0.15f);
        }

        /// <summary>신화 계보: 명중할 때 별가루 3개 (가산 0.5)</summary>
        void StarDust(Vector2 point, EnemyView target, float delay)
        {
            var star = Motif(VfxMotif.Star);
            for (int i = 0; i < 3; i++)
            {
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                int dust = Drift(star, i % 2 == 0 ? UITheme.Gold : UITheme.Cream, point, dir * Random.Range(50f, 90f) * UnitScale, Vector2.down * 50f,
                    Random.Range(12f, 18f) * UnitScale, 4f, Random.Range(0.4f, 0.55f), delay);
                if (dust >= 0) _particles[dust].Add = 0.5f;
                AnchorTo(dust, target);
            }
        }

        // ───────────── 사물 곁들임 (계보 표의 '덧붙임') ─────────────

        void LineageExtra(in LineageAttackLook look, Vector2 point, EnemyView target, float size, float delay, bool rainy, bool echo)
        {
            float unit = UnitScale * (echo ? 0.7f : 1f);
            switch (look.Extra)
            {
                case AttackExtra.Stitch:
                    for (int i = 0; i < 2; i++)
                    {
                        var at = point + new Vector2(i == 0 ? -14f : 12f, i == 0 ? 8f : -10f) * unit;
                        StitchMark(at, look.GroundColor, 22f * unit, target, delay + 0.03f);
                    }
                    break;
                case AttackExtra.Buttons:
                    ButtonBounce(point, target, look, unit, delay, 540f);
                    break;
                case AttackExtra.Wax:
                    WaxDrops(point, target, look, unit, delay);
                    break;
                case AttackExtra.Ripple:
                    WaterSplash(point, target, look, unit, delay, rainy);
                    break;
                case AttackExtra.Hearts:
                    FloatHearts(point, target, look, unit, delay, -1);
                    break;
                case AttackExtra.SoundRing:
                    SoundWave(point, target, look, size, delay);
                    break;
                case AttackExtra.LockRing:
                {
                    int ring = Spawn(_ring, BattleEase.WithAlpha(UITheme.Gold, 0.95f), point, size * 1.3f, size * 0.45f, 0.18f * PopScale, delay);
                    if (ring >= 0)
                    {
                        _particles[ring].Rotation = 0f;
                        _particles[ring].FadeIn = 0.01f;
                        _particles[ring].FadeOut = 0.6f;
                        _particles[ring].Add = 0.6f;
                    }
                    AnchorTo(ring, target);
                    break;
                }
            }
        }

        /// <summary>X 바늘땀 자국: 가는 실 두 가닥이 엇갈려 적 몸에 0.3초 붙어 있다</summary>
        void StitchMark(Vector2 at, Color color, float size, EnemyView target, float delay)
        {
            for (int i = 0; i < 2; i++)
            {
                int dash = Spawn(_soft, BattleEase.WithAlpha(color, 0.95f), at, size, size, 0.34f, delay);
                if (dash < 0) return;
                ref var p = ref _particles[dash];
                p.Aspect = 0.22f;
                p.Rotation = i == 0 ? 45f : -45f;
                p.FadeIn = 0.02f;
                p.FadeOut = 0.6f;
                AnchorTo(dash, target);
            }
        }

        /// <summary>작은 단추 3개가 바닥에 통통 튀었다 사라진다 (중력 900px/s², 한 번 튐). spin = 도는 빠르기 상한 (도/초)</summary>
        void ButtonBounce(Vector2 point, EnemyView target, in LineageAttackLook look, float unit, float delay, float spin)
        {
            var button = Motif(VfxMotif.Button);
            float ground = target.Rect.anchoredPosition.y;
            for (int i = 0; i < 3; i++)
            {
                float side = (i - 1) + Random.Range(-0.3f, 0.3f);
                var velocity = new Vector2(side * Random.Range(70f, 120f), Random.Range(120f, 210f)) * unit;
                int index = Spawn(button, i == 1 ? look.Accent : look.Main, point, Random.Range(14f, 18f) * unit, 12f * unit, Random.Range(0.42f, 0.5f), delay);
                if (index < 0) return;
                // 닿는 순간 적이 있는 자리에서 튀어 나간다 (비행 중에 적이 걸어가도)
                AnchorUntilShown(index, target, delay);
                ref var p = ref _particles[index];
                p.Motion = Motion.Bounce;
                p.P1 = velocity;
                p.P2 = new Vector2(ground - Random.Range(0f, 6f), 900f);
                p.P3 = new Vector2(0.45f, 0f);
                p.Spin = Random.Range(-spin, spin);
                p.FadeIn = 0.02f;
                p.FadeOut = 0.7f;
            }
        }

        /// <summary>촛농 방울 1~2개가 떨어져 바닥에 납작한 얼룩(0.5초) + 불씨 2개가 떠오른다 (가산 0.7)</summary>
        void WaxDrops(Vector2 point, EnemyView target, in LineageAttackLook look, float unit, float delay)
        {
            var feet = target.Rect.anchoredPosition;
            var wax = Color.Lerp(look.Main, look.Accent, 0.4f);
            int drops = Random.Range(1, 3);
            for (int i = 0; i < drops; i++)
            {
                var start = point + new Vector2(Random.Range(-12f, 12f), -6f) * unit;
                var velocity = new Vector2(Random.Range(-30f, 30f), Random.Range(20f, 60f)) * unit;
                float ground = feet.y - Random.Range(0f, 6f);
                const float gravity = 900f;
                float height = Mathf.Max(0f, start.y - ground);
                float fall = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * height)) / gravity;
                int drop = Spawn(Motif(VfxMotif.Drop), wax, start, 12f * unit, 10f * unit, fall, delay);
                AnchorUntilShown(drop, target, delay);
                if (drop >= 0)
                {
                    ref var p = ref _particles[drop];
                    p.Motion = Motion.Bounce;
                    p.P1 = velocity;
                    p.P2 = new Vector2(ground, gravity);
                    p.P3 = Vector2.zero;
                    p.Rotation = 0f;
                    p.FadeIn = 0.02f;
                    p.FadeOut = 0.95f;
                }
                var landing = new Vector2(start.x + velocity.x * fall, ground);
                int stain = Spawn(Motif(VfxMotif.Splat), BattleEase.WithAlpha(wax, 0.8f), landing, 12f * unit, 30f * unit, 0.5f, delay + fall, true);
                AnchorUntilShown(stain, target, delay + fall);
                if (stain >= 0)
                {
                    ref var s = ref _particles[stain];
                    s.Aspect = GroundAspect * 0.55f;
                    s.Rotation = 0f;
                    s.FadeIn = 0.02f;
                    s.FadeOut = 0.5f;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                int ember = Drift(_sparkle, i == 0 ? look.Accent : UITheme.Lamp, point + new Vector2(Random.Range(-14f, 14f), 4f) * unit,
                    new Vector2(Random.Range(-10f, 10f), Random.Range(40f, 70f)) * unit, Vector2.zero, 12f * unit, 4f, 0.5f, delay + 0.04f);
                AnchorUntilShown(ember, target, delay + 0.04f);
                if (ember >= 0) _particles[ember].Wobble = 5f;
            }
        }

        /// <summary>물방울 4~6개가 포물선으로 튀고 바닥에 물결 고리 (비 오는 날엔 고리 하나 더)</summary>
        void WaterSplash(Vector2 point, EnemyView target, in LineageAttackLook look, float unit, float delay, bool rainy)
        {
            var drop = Motif(VfxMotif.Drop);
            int count = Random.Range(4, 7);
            for (int i = 0; i < count; i++)
            {
                float side = (i / (count - 1f)) * 2f - 1f;
                var travel = new Vector2(side * Random.Range(40f, 70f), Random.Range(30f, 60f)) * unit;
                int index = Drift(drop, i % 2 == 0 ? look.Main : UITheme.Cream, point, travel, Vector2.down * 90f * unit, Random.Range(10f, 14f) * unit, 6f,
                    Random.Range(0.35f, 0.45f), delay);
                AnchorUntilShown(index, target, delay);
                if (index >= 0)
                {
                    _particles[index].Spin = 0f;
                    _particles[index].Rotation = side * -25f;
                }
            }
            var feet = target.Rect.anchoredPosition;
            int rings = rainy ? 2 : 1;
            for (int i = 0; i < rings; i++)
            {
                int ring = Spawn(_ring, BattleEase.WithAlpha(look.Main, 0.8f), feet, 20f * unit, (90f + i * 40f) * unit, 0.45f, delay + i * 0.1f, true);
                if (ring < 0) continue;
                ref var p = ref _particles[ring];
                p.Aspect = GroundAspect;
                p.Rotation = 0f;
                p.FadeIn = 0.01f;
                AnchorTo(ring, target);
            }
        }

        /// <summary>작은 하트가 떠오른다 (count &lt; 0이면 2~3개, 립스틱 하트 그림이 있으면 가끔 그 그림)</summary>
        void FloatHearts(Vector2 point, EnemyView target, in LineageAttackLook look, float unit, float delay, int count)
        {
            Sprite art = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(SkillVfx.LipstickHeart, out art, out isArt);
            var heart = Motif(VfxMotif.Heart);
            if (count < 0) count = Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                bool useArt = isArt && Random.value < 0.3f;
                int index = Drift(useArt ? art : heart, useArt ? Color.white : i % 2 == 0 ? look.Main : look.Accent,
                    point + new Vector2(Random.Range(-16f, 16f), Random.Range(-6f, 8f)) * unit,
                    new Vector2(Random.Range(-14f, 14f), Random.Range(46f, 72f)) * unit, Vector2.zero, Random.Range(14f, 20f) * unit * (useArt ? 1.4f : 1f), 8f,
                    Random.Range(0.5f, 0.65f), delay + i * 0.05f);
                if (index < 0) continue;
                AnchorUntilShown(index, target, delay + i * 0.05f);
                ref var p = ref _particles[index];
                p.Spin = Random.Range(-30f, 30f);
                p.Rotation = Random.Range(-15f, 15f);
                p.Wobble = 5f;
                p.FadeOut = 0.55f;
            }
        }

        /// <summary>작은 음파 고리 (vfx_sound_ring 0.3초, 가산 0.6) + 음표 하나가 떠오른다</summary>
        void SoundWave(Vector2 point, EnemyView target, in LineageAttackLook look, float size, float delay)
        {
            var art = SoundRingSprite();
            bool isArt = art != null;
            int ring = Spawn(isArt ? art : _ring, isArt ? BattleEase.WithAlpha(Color.white, 0.85f) : BattleEase.WithAlpha(look.Main, 0.9f), point, size * 0.3f,
                size * 1.1f, 0.3f * PopScale, delay);
            if (ring >= 0)
            {
                _particles[ring].Rotation = 0f;
                _particles[ring].FadeIn = 0.01f;
                _particles[ring].Add = 0.6f;
            }
            AnchorTo(ring, target);
            int note = Drift(Motif(VfxMotif.Note), Random.value < 0.5f ? look.Accent : look.Sub, point + new Vector2(Random.Range(-16f, 16f), 10f),
                new Vector2(Random.Range(-16f, 16f), 60f) * UnitScale, Vector2.zero, 20f * UnitScale, 12f, 0.6f, delay + 0.05f);
            AnchorUntilShown(note, target, delay + 0.05f);
            if (note >= 0)
            {
                _particles[note].Spin = Random.Range(-20f, 20f);
                _particles[note].Rotation = Random.Range(-12f, 12f);
                _particles[note].Wobble = 6f;
            }
        }

        // ───────────── 치명타 (E5) ─────────────

        /// <summary>
        /// 같은 프레임의 기본 공격 명중을 치명타로 바꾼다 (CriticalHit은 GuardianAttack/EchoAttack 뒤, EnemyHit 앞에 온다):
        /// 명중 그림을 atk_{계보}_crit(없으면 atk_{계보}_hit)으로 바꿔 S_c = 200 × U(보스 ×1.4, 잔향이 ×0.65)로, 곡선은 첫 프레임 ×1.45 · 50ms 버팀 ·
        /// 80ms에 0.97로 정착 → 1.0으로 되튐 · 35%부터 SmoothStep으로 사라짐 (스펙 55% InQuad — 2차 조율: 가운데가 찬 치명타 그림이 맞은 몸의
        /// 흰 번쩍임 · 떨림 · 넉백을 200ms까지 덮었다. 115ms부터 걷혀 몸 반응이 읽히게) (루즈리는 입맞춤 도장: ×1.5 · 16ms · 가로 1.1/세로 0.9 눌림).
        /// 번쩍임 줄이기면 그림을 ReducedCritTint(라벤더 회색)로 눌러 22%부터 걷는다 (r2 비평: 그림의 흰 면이 줄이기 설정에서 그대로 남았다). 일반 명중의 코어 별 · 고리 ·
        /// 빛줄기 · 잉크 받침은 거둔다 (치명타가 자기 것을 그린다). 돌격 베기는 ×1.25. 바꿨으면 true — 뒤따르는 CritImpact가 나머지 층을 붙인다
        /// </summary>
        public bool PromoteImpact(int targetId, float scale = 1.4f)
        {
            if (!SameMoment(_impactFrame, _impactClock) || _impactTarget != targetId) return false;
            _impactFrame = -1;
            if (!IsLive(_impactArt)) return false;
            var look = AttackFxCatalog.Look(_impactLineage);
            var sig = AttackFxCatalog.Signature(_impactLineage);
            float settled = CritSize * UnitScale * (_impactBoss ? 1.4f : 1f) * (_impactEcho ? EchoCritScale : 1f);
            var crit = _juice != null ? _juice.Crit(look.Id) : null;
            var fallback = crit == null ? HitSprite(look.Id) : null;
            float life = HitFeel.Feel(HitTier.Crit).Life * PopScale;

            ref var p = ref _particles[_impactArt];
            if (crit != null) p.Image.sprite = crit;
            else if (fallback != null) p.Image.sprite = fallback;
            else settled *= 0.62f;
            p.Color = _reduceFlash ? ReducedCritTint : Color.white;
            p.Curve = SizeCurve.Impact;
            p.Size1 = settled * CritRebound;
            p.Size0 = settled * (_impactBoss ? 1.40f : 1.45f);
            p.Hold = 0.05f;
            p.Settle = 0.08f;
            p.EndScale = 1f / CritRebound;
            p.Life = life;
            p.FadeIn = 0f;
            p.FadeOut = _reduceFlash ? PromotedCritFadeFromReduced : PromotedCritFadeFrom;
            p.FadeKind = FadeSmooth;
            p.Spin = look.UprightHit ? 0f : -6f / life;
            if (sig.Special == CritSpecial.KissStamp)
            {
                // 입맞춤 도장: 더 크게 쾅 찍히고 가로로 눌렸다가 펴진다
                p.Size0 = settled * 1.5f;
                p.Hold = 0.016f;
                p.Settle = 0.07f;
                p.Aspect = 0.82f;
                p.AspectEnd = 1f;
            }
            if (p.Delay <= 0f) p.Rect.sizeDelta = new Vector2(p.Size0, p.Size0 * p.Aspect);

            // 일반 명중 층 중 치명타가 대신 그리는 것들을 거둔다
            AtkCancel(ref _impactCore);
            AtkCancel(ref _impactRing);
            AtkCancel(ref _impactBacking);
            for (int i = 0; i < _impactStreakCount; i++) AtkCancel(ref _impactStreaks[i]);
            _impactStreakCount = 0;
            if (IsLive(_impactSlash)) Grow(_impactSlash, 1.25f);

            _promotedFrame = Time.frameCount;
            _promotedClock = _clock;
            _promotedTarget = targetId;
            _promotedArt = _impactArt;
            _promotedHasArt = crit != null;
            _promotedLineage = _impactLineage;
            _promotedEcho = _impactEcho;
            _promotedPoint = _impactPoint;
            _promotedDir = _impactDir;
            _promotedHitSize = _impactHitSize;
            return true;
        }

        bool Grow(int index, float scale)
        {
            if (!IsLive(index)) return false;
            ref var p = ref _particles[index];
            p.Size0 *= scale;
            p.Size1 *= scale;
            return true;
        }

        /// <summary>
        /// 치명타 층 (E5, 닿는 순간 delay): ① 잉크 받침 1.0 S_c ② 금빛 후광 2.2 S_c ③ 치명타 그림(PromoteImpact가 바꾼 명중 — 아니면 금빛 별 폭발) ④ 크림 코어 별
        /// ⑤ 가로 섬광 ⑥ 크림 고리 ⑦ 금 별 고리(+50ms) ⑧ 속도선 ⑨ 사방 빛줄기 8/6/4 ⑩ 떠오르는 별 4 ⑪ 계보 특수(기본 공격만, 높음·보통) ⑫ 바닥 자국(높음).
        /// 큰 터짐은 0.1초에 2개까지 (넘치면 33/50ms 늦게, 그 뒤로는 작은 치명타: 그림 · 코어 · 고리 1(곁들임 층) · 빛줄기 4).
        /// 작은 치명타도 0.1초에 SmallCritsPerWindow(6)개까지 — 그 뒤로는 크림 코어 별만 (바꾼 명중 그림은 그대로, 범위 스킬 치명타 무리).
        /// 모든 층은 닿는 순간까지 걸어간 대상에 붙는다. 화면 킥 · 숫자 · 반응은 부르는 쪽
        /// </summary>
        public void CritImpact(EnemyView target, float delay)
        {
            if (target == null) return;
            if (!_ready) Prepare();
            bool promoted = SameMoment(_promotedFrame, _promotedClock) && _promotedTarget == target.Id;
            _promotedFrame = -1;
            float unit = UnitScale;
            bool boss = target.IsBoss;
            bool echo = promoted && _promotedEcho;
            float size = CritSize * unit * (boss ? 1.4f : 1f) * (echo ? EchoCritScale : 1f);
            Vector2 point;
            Vector2 dir;
            if (promoted)
            {
                point = _promotedPoint;
                dir = _promotedDir;
            }
            else
            {
                point = target.HitPoint;
                ClampToField(ref point);
                dir = Vector2.zero;
            }
            string lineage = promoted ? _promotedLineage : null;
            var look = AttackFxCatalog.Look(lineage);
            var sig = AttackFxCatalog.Signature(lineage);
            var accent = promoted && look.Id != null ? look.Accent : UITheme.Gold;
            float pop = PopScale;
            float burst = BigBurstDelay(delay);
            bool small = burst < 0f;
            // 치명타 무리: 작은 치명타도 0.1초에 6개를 넘으면 코어 별만 (범위 스킬 치명타 20개가 투사체 머리 · 명중 그림 몫까지 쓰지 않게)
            bool crowd = small && !TrySmallCrit(_clock + delay);
            float at = Mathf.Max(0f, (small ? delay : burst) - ImpactLead);
            bool highOrMedium = _quality != FxQuality.Low;

            // ① 잉크 받침 · ② 금빛 후광
            if (!small && JuiceGates.InkBacking(_quality, true)) AnchorTo(InkBacking(point, size, 0.40f, 0.2f * pop, at), target);
            if (!small) AnchorTo(Halo(point, UITheme.Gold, 2.2f * size, 0.2f * pop, at, 0.3f), target);

            // ③ 치명타 그림: 바꾼 명중은 후광 위로 다시 올린다 (만든 순서 = 그리는 순서), 스킬 치명타 · 그림 없는 계보는 금빛 별 폭발 (무리에서는 없음)
            if (promoted && IsLive(_promotedArt)) _particles[_promotedArt].Rect.SetAsLastSibling();
            if (!crowd && (!promoted || !_promotedHasArt)) GenericCritArt(point, target, size * (promoted ? 0.75f : 1f), at);

            // ④ 크림 코어 별
            AnchorTo(CoreStar(point, JuiceColors.CoreCream, 0.75f * size, 0.5f * size, 0.083f * pop, at, 1f), target);

            if (small)
            {
                if (crowd) return;
                // 작은 치명타 고리는 곁들임 층 (큰 치명타의 고리 · 섬광은 영웅 층이라 그대로)
                if (Allow(FxPriority.Extra)) AnchorTo(Shockwave(point, JuiceColors.CoreCream, 0.3f * size, 1.2f * size, 0.2f * pop, at, 0.9f, 0.8f), target);
                CritStreaks(point, accent, StreakCount(4), at, target);
                return;
            }

            // ⑤ 가로 섬광 (높음·보통)
            if (highOrMedium)
                AnchorTo(Flare(point, Color.Lerp(UITheme.Gold, JuiceColors.CoreCream, 0.5f), 2.2f * size, 0.18f * size, Random.Range(-15f, 15f), 0.1f * pop, at, 0.8f),
                    target);
            // ⑥ 크림 고리 · ⑦ 금 별 고리 (+50ms, 높음·보통)
            AnchorTo(Shockwave(point, JuiceColors.CoreCream, 0.3f * size, 1.5f * size, 0.22f * pop, at, 0.9f, 0.8f), target);
            if (highOrMedium)
                AnchorTo(Shockwave(point, UITheme.Gold, 0.3f * size, 1.5f * size, 0.22f * pop, at + 0.05f * pop, 0.9f, 0.8f, 1f, false, StarRingSprite), target);
            // ⑧ 속도선 (높음, 큰 빛 상한 안)
            if (JuiceGates.Speedlines(_quality, false) && _halos < _haloCap) AnchorTo(Speedlines(point, JuiceColors.CoreCream, 2.6f * size, 0.12f * pop, at, 0.5f), target);
            // ⑨ 사방 빛줄기 (8 / 6 / 4)
            CritStreaks(point, accent, StreakCount(8), at, target);
            // ⑩ 떠오르는 별 4
            CritStars(point, at, target);
            // ⑪ 계보 특수 (낮음은 그림만)
            if (promoted && highOrMedium) LineageCritSpecial(look, sig, point, dir, target, at);
            // ⑫ 바닥 자국 (높음 · 자국 상한)
            if (promoted && _quality == FxQuality.High) CritDecal(sig, target, at);
        }

        /// <summary>
        /// 작은 치명타 하나를 센다 (at = 닿는 연출 시각). 그 둘레 0.1초에 이미 SmallCritsPerWindow개가 있으면 false — 세지 않고 코어 별만 그린다.
        /// 범위 스킬은 적마다 닿는 순간이 달라 시각이 뒤섞여 오므로 고리 버퍼에서 그 둘레만 센다 (BigBurstDelay와 같은 방식). 할당 없음
        /// </summary>
        bool TrySmallCrit(float at)
        {
            int near = 0;
            for (int i = 0; i < _smallCrits.Length; i++)
            {
                if (Mathf.Abs(_smallCrits[i] - at) < HitFeel.BigBurstWindow) near++;
            }
            if (near >= SmallCritsPerWindow) return false;
            _smallCrits[_smallCritHead] = at;
            _smallCritHead = (_smallCritHead + 1) % _smallCrits.Length;
            return true;
        }

        /// <summary>'아직 없음' 시각으로 채운 고리 버퍼 (한 번만 만든다)</summary>
        static float[] NeverTimes(int count)
        {
            var times = new float[count];
            for (int i = 0; i < count; i++) times[i] = -9999f;
            return times;
        }

        /// <summary>
        /// 금빛 별 폭발 (vfx_star_burst → 별 모양) — 스킬 치명타 · 치명타 그림이 없는 계보. 첫 프레임 ×1.45 · 50ms · 80ms 정착.
        /// 번쩍임 줄이기면 그림은 ReducedCritTint (금색 별 모양은 원래 거의 흰색이 아니라 그대로)
        /// </summary>
        void GenericCritArt(Vector2 point, EnemyView target, float size, float delay)
        {
            Sprite burst = _juice != null ? _juice.StarBurst : null;
            bool isArt = burst != null;
            if (!isArt && _library != null) _library.TryGetVfx(SkillVfx.StarBurst, out burst, out isArt);
            if (!isArt) burst = Motif(VfxMotif.Star);
            var tint = isArt ? (_reduceFlash ? ReducedCritTint : Color.white) : UITheme.Gold;
            int index = SpawnImpactCurve(burst, tint, point, size * 0.9f, 1.45f, 0.05f, 0.08f, 1.04f, 0.33f * PopScale, delay, 0.55f);
            if (index >= 0)
            {
                _particles[index].Rotation = Random.Range(-20f, 20f);
                _particles[index].Spin = -10f / 0.33f;
            }
            AnchorTo(index, target);
        }

        /// <summary>
        /// 치명타 사방 빛줄기: 길이 22~30 · 두께 5~7 · 600~1000px/s · 끌림 8 · 200ms (가산, lerp(강조색, 크림 코어, 0.5), 세기 0.9 — 번쩍임 줄이기면 ×0.6:
        /// 코어 별 · 섬광 · 속도선과 같은 문)
        /// </summary>
        void CritStreaks(Vector2 point, Color accent, int count, float delay, EnemyView target)
        {
            var tint = BattleEase.WithAlpha(JuiceColors.TowardCream(accent, 0.5f), 0.9f * JuiceGates.CoreIntensity(_reduceFlash));
            AtkStreaks(point, Vector2.up, count, 360f, 600f, 1000f, tint, delay, 26f, 6f, 8f, target, 0.16f, 0.2f);
        }

        /// <summary>치명타 별 4개: motif_star 20~28px가 70~110px/s로 흩어지며 떠오르고 돈다 (가산 0.5, 금 / 크림)</summary>
        void CritStars(Vector2 point, float delay, EnemyView target)
        {
            var star = Motif(VfxMotif.Star);
            float unit = UnitScale;
            for (int i = 0; i < 4; i++)
            {
                if (!Allow(FxPriority.Sparkle)) return;
                float angle = i * Mathf.PI * 0.5f + Random.Range(-0.35f, 0.35f) + Mathf.PI * 0.25f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float life = Random.Range(0.42f, 0.55f);
                int index = Drift(star, i % 2 == 0 ? UITheme.Gold : UITheme.Cream, point, dir * (Random.Range(70f, 110f) * life * 1.3f * unit),
                    Vector2.up * (30f * life * life * unit), Random.Range(20f, 28f) * unit, 10f * unit, life, delay);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Spin = Random.Range(-360f, 360f);
                p.Add = 0.5f;
                AnchorUntilShown(index, target, delay);
            }
        }

        /// <summary>
        /// 치명타 바닥 자국 (decal_{계보}, 0.45, 24~30px, 2.5초) — 그림이 없으면 없음. 닿는 순간의 발밑에 (그때까지 걸어간 대상을 따라갔다가
        /// 그 자리에 남는다 — 바닥 자국은 몸에 붙어 다니지 않는다)
        /// </summary>
        void CritDecal(in LineageSignature sig, EnemyView target, float delay)
        {
            if (!sig.HasDecal || _juice == null) return;
            var sprite = _juice.Decal(sig.Decal);
            if (sprite == null) return;
            float unit = UnitScale;
            var at = target.Rect.anchoredPosition + new Vector2(Random.Range(-10f, 10f), Random.Range(-4f, 2f)) * unit;
            AnchorUntilShown(Decal(at, sprite, BattleEase.WithAlpha(Color.white, 0.45f), Random.Range(24f, 30f) * unit, delay), target, delay);
        }

        // ───────────── 계보 치명타 특수 (§1.21) ─────────────

        /// <summary>계보 특수 (0~330ms 안). s = 그 계보의 일반 명중 크기 S</summary>
        void LineageCritSpecial(in LineageAttackLook look, in LineageSignature sig, Vector2 point, Vector2 dir, EnemyView target, float delay)
        {
            float s = _promotedHitSize > 1f ? _promotedHitSize : look.HitSize * ImpactScale * UnitScale;
            switch (sig.Special)
            {
                case CritSpecial.StitchWhip:
                    StitchWhip(look, point, dir, s, target, delay);
                    break;
                case CritSpecial.ButtonClack:
                    ButtonClack(look, point, s, target, delay);
                    break;
                case CritSpecial.KeyLock:
                    KeyLock(point, s, target, delay);
                    break;
                case CritSpecial.FlameFlare:
                    FlameFlare(look, point, s, target, delay);
                    break;
                case CritSpecial.SplashCrown:
                    SplashCrown(look, point, s, target, delay);
                    break;
                case CritSpecial.KissStamp:
                    KissStamp(look, point, s, target, delay);
                    break;
                case CritSpecial.ChordFan:
                    ChordFan(look, point, s, target, delay);
                    break;
            }
        }

        /// <summary>짝짝이: X 바늘땀 도장 2개 (40ms 간격, 1.6배, 첫 프레임 ×1.3) + 실 채찍 (보조색 빛줄기 1.6 S가 몸을 가로질러 120ms, 가산 0.4)</summary>
        void StitchWhip(in LineageAttackLook look, Vector2 point, Vector2 dir, float s, EnemyView target, float delay)
        {
            float unit = UnitScale;
            for (int i = 0; i < 2; i++)
            {
                var at = point + new Vector2(i == 0 ? -14f : 12f, i == 0 ? 8f : -10f) * (1.6f * unit);
                StitchStamp(at, look.GroundColor, 22f * 1.6f * unit, target, delay + i * 0.04f);
            }
            var across = dir.sqrMagnitude > 0.01f ? new Vector2(-dir.y, dir.x) : Vector2.right;
            if (Random.value < 0.5f) across = -across;
            var from = point - across * (0.9f * s);
            var to = point + across * (0.9f * s);
            float length = 1.6f * s;
            int whip = Line(StreakSprite, BattleEase.WithAlpha(Color.Lerp(look.Sub, Color.white, 0.15f), 0.95f), from, to, length * 0.6f, length, 0.12f, delay);
            if (whip < 0) return;
            ref var p = ref _particles[whip];
            p.Add = 0.4f;
            p.AlignToPath = true;
            p.Height0 = 10f * unit;
            p.Height1 = 4f * unit;
            p.FadeIn = 0f;
            p.FadeOut = 0.45f;
            p.Rect.pivot = StreakPivot;
            FaceTo(whip, from, to);
            AnchorTo(whip, target);
        }

        /// <summary>X 바늘땀 도장: 두 가닥이 엇갈려 첫 프레임 ×1.3으로 쾅 찍히고 16ms 버팀 · 50ms 정착, 0.34초 (불투명도 0.8 — 아래 몸이 비친다)</summary>
        void StitchStamp(Vector2 at, Color color, float size, EnemyView target, float delay)
        {
            for (int i = 0; i < 2; i++)
            {
                int dash = SpawnImpactCurve(_soft, BattleEase.WithAlpha(color, StitchStampAlpha), at, size, 1.3f, 0.016f, 0.05f, 1f, 0.34f, delay, 0.6f);
                if (dash < 0) return;
                ref var p = ref _particles[dash];
                p.Aspect = 0.22f;
                p.Rotation = i == 0 ? 45f : -45f;
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
                p.Rect.sizeDelta = new Vector2(p.Size0, p.Size0 * 0.22f);
                AnchorTo(dash, target);
            }
        }

        /// <summary>똑단이: 작은 단추 6개 튐 (±720°/s) + 금빛 바늘땀 고리 (fx_ring_thread, 가산 0.6)</summary>
        void ButtonClack(in LineageAttackLook look, Vector2 point, float s, EnemyView target, float delay)
        {
            float unit = UnitScale;
            ButtonBounce(point, target, look, unit * 1.15f, delay, 720f);
            if (Allow(FxPriority.Sparkle)) ButtonBounce(point, target, look, unit, delay + 0.03f, 720f);
            AnchorTo(Shockwave(point, UITheme.Gold, 0.4f * s, 1.5f * s, 0.26f, delay, 0.6f, 0.95f, 1f, false, RingThreadSprite), target);
        }

        /// <summary>찰칵이: 열쇠구멍 잠김 고리가 1.6 S → 0.5 S로 90ms에 오므라든다 (금, 가산 0.6) + 금빛 섬광이 120ms에 +45° 훑는다</summary>
        void KeyLock(Vector2 point, float s, EnemyView target, float delay)
        {
            int ring = Spawn(ShockwaveSprite, BattleEase.WithAlpha(UITheme.Gold, 0.95f), point, 1.6f * s, 0.5f * s, 0.3f, delay);
            if (ring >= 0)
            {
                ref var p = ref _particles[ring];
                p.Curve = SizeCurve.Snap;
                p.Rotation = 0f;
                p.FadeIn = 0.01f;
                p.FadeOut = 0.55f;
                p.Add = 0.6f;
            }
            AnchorTo(ring, target);
            var lockArt = Motif(VfxMotif.Keyhole);
            int keyhole = SpawnImpactCurve(lockArt, BattleEase.WithAlpha(UITheme.Gold, 0.9f), point, 0.32f * s, 1.3f, 0.03f, 0.06f, 0.9f, 0.3f, delay + 0.09f, 0.5f);
            if (keyhole >= 0) _particles[keyhole].Add = 0.35f;
            AnchorTo(keyhole, target);
            int glint = Flare(point, UITheme.Gold, 1.4f * s, 0.12f * s, Random.Range(-40f, -20f), 0.12f, delay + 0.03f, 0.9f);
            if (glint >= 0) _particles[glint].Spin = 45f / 0.12f;
            AnchorTo(glint, target);
        }

        /// <summary>몽당이: 촛불이 위로 1 → 1.6배 솟고 (120ms) 발밑에 따뜻한 빛 웅덩이(가산, 1.8 S, 300ms) + 떠오르는 불씨 6</summary>
        void FlameFlare(in LineageAttackLook look, Vector2 point, float s, EnemyView target, float delay)
        {
            float unit = UnitScale;
            var flame = Motif(VfxMotif.Flame);
            int up = Spawn(flame, Color.Lerp(look.Accent, UITheme.Cream, 0.25f), point + Vector2.down * (0.12f * s), 0.5f * s, 0.56f * s, 0.26f, delay);
            if (up >= 0)
            {
                ref var p = ref _particles[up];
                p.Curve = SizeCurve.Snap;
                p.Aspect = 1f;
                p.AspectEnd = 1.6f;
                p.Rotation = 0f;
                p.FadeIn = 0.02f;
                p.FadeOut = 0.5f;
                p.Add = 0.5f;
                p.Rect.pivot = FlameBasePivot;
            }
            AnchorTo(up, target);
            int pool = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(FlameAmber, 0.4f), target.Rect.anchoredPosition, 1.5f * s, 1.8f * s, 0.3f, delay, 1f, true);
            if (pool >= 0)
            {
                ref var p = ref _particles[pool];
                p.Aspect = GroundAspect;
                p.FadeOut = 0.4f;
                p.FadeKind = FadeInQuad;
            }
            AnchorTo(pool, target);
            for (int i = 0; i < 6; i++)
            {
                if (!Allow(FxPriority.Sparkle)) return;
                var at = point + new Vector2(Random.Range(-0.35f, 0.35f) * s, Random.Range(-0.1f, 0.25f) * s);
                var travel = new Vector2(Random.Range(-18f, 18f), Random.Range(50f, 100f)) * unit;
                int ember = Drift(_sparkle, i % 2 == 0 ? FlameAmber : UITheme.Lamp, at, travel, Vector2.up * (10f * unit), Random.Range(10f, 14f) * unit, 4f * unit,
                    Random.Range(0.4f, 0.55f), delay + i * 0.02f);
                if (ember < 0) continue;
                _particles[ember].Add = 0.8f;
                _particles[ember].Wobble = 4f;
                AnchorUntilShown(ember, target, delay + i * 0.02f);
            }
        }

        /// <summary>우두커니: 발밑 겹 물결 2 (0 · 100ms, 바닥, 가산 0.6) + 물방울 8이 포물선으로. 비 오는 날 모두 ×1.2</summary>
        void SplashCrown(in LineageAttackLook look, Vector2 point, float s, EnemyView target, float delay)
        {
            float unit = UnitScale;
            float rain = _stageModifier == StageModifierType.Rain ? 1.2f : 1f;
            var feet = target.Rect.anchoredPosition;
            for (int i = 0; i < 2; i++)
            {
                AnchorTo(Shockwave(feet, look.Main, 0.3f * s * rain, 1.6f * s * rain, 0.4f, delay + i * 0.1f, 0.6f, 0.85f, GroundAspect, true), target);
            }
            var drop = Motif(VfxMotif.Drop);
            for (int i = 0; i < 8; i++)
            {
                if (i >= 2 && !Allow(FxPriority.Sparkle)) return;
                float angle = Mathf.Lerp(20f, 160f, (i + Random.Range(0.2f, 0.8f)) / 8f) * Mathf.Deg2Rad;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.Range(170f, 300f) * unit * rain);
                float size = Random.Range(10f, 14f) * unit * rain;
                int index = Spawn(drop, i % 2 == 0 ? look.Main : UITheme.Cream, point, size, size * 0.8f, Random.Range(0.4f, 0.5f), delay);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Motion = Motion.Ballistic;
                p.P1 = velocity;
                p.Drag = 0.6f;
                p.Gravity = 900f;
                p.Spin = 0f;
                p.Rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
                p.FadeIn = 0.02f;
                p.FadeOut = 0.6f;
                AnchorUntilShown(index, target, delay);
            }
        }

        /// <summary>루즈리: 하트 3이 떠오르고 (×1.5) 발밑에 분홍 빛 웅덩이 (주색, 가산 0.3). 입맞춤 도장 곡선은 PromoteImpact가</summary>
        void KissStamp(in LineageAttackLook look, Vector2 point, float s, EnemyView target, float delay)
        {
            FloatHearts(point, target, look, UnitScale * 1.5f, delay, 3);
            int pool = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(look.Main, 0.3f), target.Rect.anchoredPosition, 1.4f * s, 1.7f * s, 0.3f, delay, 1f, true);
            if (pool >= 0)
            {
                ref var p = ref _particles[pool];
                p.Aspect = GroundAspect;
                p.FadeOut = 0.4f;
                p.FadeKind = FadeInQuad;
            }
            AnchorTo(pool, target);
        }

        /// <summary>라라벨: 세 음 부채 (motif_note 24px가 위쪽 −25° · 0° · +25°로 30ms 간격, 120px/s) + 금빛 오선 고리 (fx_star_ring, 가산 0.7)</summary>
        void ChordFan(in LineageAttackLook look, Vector2 point, float s, EnemyView target, float delay)
        {
            float unit = UnitScale;
            var note = Motif(VfxMotif.Note);
            for (int i = 0; i < 3; i++)
            {
                float tilt = (i - 1) * 25f;
                float angle = (90f - tilt) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var color = i == 0 ? look.Accent : i == 1 ? look.Sub : Color.Lerp(look.Main, UITheme.Cream, 0.3f);
                int index = Drift(note, color, point + dir * (12f * unit), dir * (120f * 0.5f * unit), Vector2.zero, 24f * unit, 20f * unit, 0.5f, delay + i * 0.03f);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Rotation = -tilt;
                p.Spin = 0f;
                p.Wobble = 3f;
                p.Add = 0.3f;
                p.FadeOut = 0.6f;
                AnchorUntilShown(index, target, delay + i * 0.03f);
            }
            AnchorTo(Shockwave(point, UITheme.Gold, 0.4f * s, 1.5f * s, 0.3f, delay, 0.7f, 0.9f, 1f, false, StarRingSprite), target);
        }

        // ───────────── 지킴이 축하 (E8d) ─────────────

        /// <summary>지킴이 축하 반짝임: 머리 위 18px 반짝임이 톡 피어 살짝 떠오르며 0.4초 (가산 0.8) — 처치 · 치명타가 보이는 순간(delay)에</summary>
        public void CelebrationGlint(Vector2 head, float delay)
        {
            if (!_ready) Prepare();
            if (!Allow(FxPriority.Sparkle)) return;
            float unit = UnitScale;
            int index = SpawnFx(_sparkle, BattleEase.WithAlpha(UITheme.Cream, 0.95f), head + Vector2.up * (8f * unit), 18f * unit, 14f * unit, 0.4f, delay, 0.8f);
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Pop;
            p.P1 = Vector2.up * (16f * unit);
            p.Pulse = 0.15f;
            p.PulseHz = 6f;
            p.Spin = 90f;
            p.FadeOut = 0.5f;
        }

        // ───────────── 그림 / 도움 ─────────────

        Sprite ShotSprite(string lineageId)
        {
            var sprite = _catalog != null ? _catalog.Shot(lineageId) : null;
            if (sprite == null && _library != null) sprite = _library.LineageShot(lineageId);
            return sprite;
        }

        Sprite HitSprite(string lineageId)
        {
            var sprite = _catalog != null ? _catalog.Hit(lineageId) : null;
            if (sprite == null && _library != null) sprite = _library.LineageHit(lineageId);
            return sprite;
        }

        Sprite SlashSprite()
        {
            var sprite = _catalog != null ? _catalog.Slash : null;
            if (sprite == null && _library != null) sprite = _library.AttackArt(SkillVfxLibrary.SlashId);
            return sprite;
        }

        Sprite SnipeTrailSprite()
        {
            var sprite = _catalog != null ? _catalog.SnipeTrail : null;
            if (sprite == null && _library != null) sprite = _library.AttackArt(SkillVfxLibrary.SnipeTrailId);
            return sprite;
        }

        /// <summary>같은 프레임 · 같은 연출 시계인지 (실제 전투는 프레임으로, 배치 캡처는 Advance 사이로 가른다)</summary>
        bool SameMoment(int frame, float clock) => frame == Time.frameCount && clock == _clock;

        bool IsLive(int index) => index >= 0 && index < _particles.Length && _particles[index].Alive;

        /// <summary>아직 살아 있는 입자를 거두고 번호를 지운다</summary>
        void AtkCancel(ref int index)
        {
            if (IsLive(index)) Release(index);
            index = -1;
        }

        /// <summary>명중 자리를 전장 안쪽 24px로 당긴다 (ATK-16). 대상이 완전히 밖(가장자리에서 60px 넘게)이면 false — 쐐기만 띄운다</summary>
        bool ClampToField(ref Vector2 point)
        {
            if (_layer == null) return true;
            var half = _layer.rect.size * 0.5f;
            if (half.x <= 1f || half.y <= 1f) return true;
            bool inside = Mathf.Abs(point.x) <= half.x + 60f && Mathf.Abs(point.y) <= half.y + 60f;
            point.x = Mathf.Clamp(point.x, -half.x + EdgeInset, half.x - EdgeInset);
            point.y = Mathf.Clamp(point.y, -half.y + EdgeInset, half.y - EdgeInset);
            return inside;
        }
    }
}
