using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 스킬 명중 한 번 (FX-JUICE E7 — 코어 이음매 SkillHit). 코어가 기본 명중(잉크 받침 · 코어 별 · 고리 · 빛줄기, 범위 스킬은 미니)을 그리고
    /// show가 OnSkillHit에서 방식별 결을 더한다. 좌표는 전장 로컬 px
    /// </summary>
    public struct SkillHitFx
    {
        /// <summary>맞은 자리 (적 몸 가운데)</summary>
        public Vector2 Point;
        public EnemyView Target;
        /// <summary>스킬 주색 / 강조색 (SkillLook)</summary>
        public Color Main;
        public Color Accent;
        public SkillVfxStyle Style;
        /// <summary>튀는 조각 모양 (SkillLook.Particle)</summary>
        public VfxMotif Shard;
        /// <summary>시전 방향 (시전자 → 대상, 정규화 안 해도 됨)</summary>
        public Vector2 Direction;
        /// <summary>그 연출이 닿는 순간까지 (CastDelay)</summary>
        public float Delay;
        public bool Boss;
        /// <summary>바닥 장판 틱 (명중 그림 없이 불씨 하나)</summary>
        public bool Tick;
    }

    /// <summary>
    /// 손맛 기본 도구 (FX-JUICE §3.6 — 코어). 층 쌓기(코어 별 · 후광 · 잉크 받침 · 충격 고리 · 빛줄기 · 섬광 · 속도선 · 색종이 · 여운 반짝 · 바닥 자국),
    /// 화면(킥 · 줌 · 어둡게 · 빛 번쩍임 · 임팩트 프레임 · 인쇄 어긋남 · 스킬 흔들림 · 진동), 밀도 관리, 이음매(NoteKill · SkillHit).
    /// 정확한 시그니처 목록은 BattleFx.cs 맨 위. 크기는 넘긴 값 그대로(px)지만 Streaks · Confetti · Twinkles는 기준 px에 액터 배율(U)을 안에서 곱한다.
    /// 화면 효과의 delay는 연출 시계(그 순간까지), 지속 시간은 실제 시간. 늦은 화면 효과는 16칸 순환 큐(ScreenCue)가 그 시각에 터뜨린다.
    /// </summary>
    public partial class BattleFx
    {
        /// <summary>입자 층 (예산이 모자라면 별빛 → 곁들임 → 꼬리 → 코어 순서로 버린다)</summary>
        enum FxPriority : byte
        {
            /// <summary>빛줄기 · 별빛 · 색종이 · 여운 반짝 · 흘리는 빛 부스러기</summary>
            Sparkle,
            /// <summary>잉크 받침 · 계보 곁들임 · 자국 · 일반 명중 고리</summary>
            Extra,
            /// <summary>투사체 아래 빛 · 잔상</summary>
            Trail,
            /// <summary>명중 그림 · 코어 별 · 치명타/처치/보스 영웅 층 · 숫자</summary>
            Core,
        }

        /// <summary>우선순위별 입자 여유 (꽉 차면 별빛 → 곁들임 → 꼬리 순서로 버리고 투사체·명중·숫자는 남긴다). 프레임당 생성 상한은 품질별 (48/36/24)</summary>
        bool Allow(FxPriority priority)
        {
            if (!_ready) Prepare();
            // 잠자기 모드: 별빛 · 곁들임 층은 버린다 (IDLE-06 — 투사체 · 명중 · 숫자 아닌 코어만)
            if (_sleepMode && priority <= FxPriority.Extra) return false;
            int free = _budget - UsedParticles;
            switch (priority)
            {
                case FxPriority.Sparkle:
                    return free > 60 && _frameSpawns < _frameSpawnCap;
                case FxPriority.Extra:
                    return free > 40 && _frameSpawns < _frameSpawnCap;
                case FxPriority.Trail:
                    return free > 20;
                default:
                    return free > 0;
            }
        }

        /// <summary>
        /// 바닥 레이어 여유 (잉크 받침 · 자국 · 장판): 곁들임 층은 바닥 예산의 마지막 HitFeel.GroundReserve칸을 남긴다 (포위 텔레그래프 · 보스 웅덩이 ·
        /// 처치 고리 몫). 바닥 레이어가 없으면 바닥 요청이 위쪽 묶음을 쓰므로 Allow와 같다
        /// </summary>
        bool AllowGround(FxPriority priority)
        {
            if (!_ready) Prepare();
            if (_groundTotal <= 0) return Allow(priority);
            int free = _groundBudget - UsedGround;
            switch (priority)
            {
                case FxPriority.Sparkle:
                    return free > HitFeel.GroundReserve * 2 && _frameSpawns < _frameSpawnCap;
                case FxPriority.Extra:
                    return free > HitFeel.GroundReserve && _frameSpawns < _frameSpawnCap;
                case FxPriority.Trail:
                    return free > HitFeel.GroundReserve / 2;
                default:
                    return free > 0;
            }
        }

        // ───────────── 설정 · 배치 (읽기 전용) ─────────────

        /// <summary>지금 이펙트 품질 (설정 또는 시험장 DevOverride)</summary>
        public FxQuality Quality => _quality;

        /// <summary>번쩍임 줄이기 (이 BattleFx가 담아 둔 값)</summary>
        public bool ReduceFlashOn => _reduceFlash;

        /// <summary>번쩍임 줄이기 (마지막으로 읽은 값 — EnemyView처럼 BattleFx를 모르는 곳이 매 프레임 PlayerPrefs를 읽지 않게)</summary>
        public static bool ReduceFlashNow { get; private set; }

        /// <summary>흔들림 배율 0~1 (설정 0/50/100%)</summary>
        public float ShakeScaleNow => _shakeScale;

        /// <summary>상단 HUD 아래 안전선 (전장 로컬 y) = FieldTop − JuiceLayout.TopHud. 숫자·알림·큰 터짐은 이 아래에</summary>
        public float SafeTop => FieldTop - JuiceLayout.TopHud;

        /// <summary>새 손맛 그림 묶음 (없으면 null — 기본 도구는 안에서 대신한다)</summary>
        public JuiceArt Juice => _juice;

        /// <summary>연출 시계 (전체 정지·느린 화면이 걸린 시간)</summary>
        public float Clock => _clock;

        /// <summary>실제 시간 (Advance dt의 합)</summary>
        public float RealClock => _realClock;

        /// <summary>화면에 띄울 숫자 상한 (품질별 24/16/10)</summary>
        public int MaxNumbersNow => _maxNumbers;

        /// <summary>입자 머티리얼이 미리 곱한 알파인지 (false면 가산 층이 반투명 빛으로 그려진다)</summary>
        public bool PremulOn
        {
            get
            {
                if (!_ready) Prepare();
                return _premul;
            }
        }

        /// <summary>이번 프레임 연출 상태 (시험장 캡처 metrics)</summary>
        public FxFrameStats Stats => _stats;

        /// <summary>
        /// 연출 시험장이 PlayerPrefs를 쓰지 않고 설정을 잠깐 바꾼다 (Active = false면 기기 설정으로 돌아간다). 진동은 시험장 동안 울리지 않는다
        /// </summary>
        public FxDevSettings DevOverride
        {
            get => _dev;
            set
            {
                _dev = value;
                if (_ready) ReadSettings();
            }
        }

        /// <summary>지난 불 밝히기 뒤 실제 시간 (알림을 1.3초 쉰다)</summary>
        public float SinceLamp => _realClock - _lastLampReal;

        /// <summary>지난 보스 처치 순간 뒤 실제 시간 (알림을 2초 쉰다)</summary>
        public float SinceBossMoment => _gov.SinceBossMoment(_realClock);

        // ───────────── 그림 (새 그림 → 예전 그림) ─────────────

        Sprite CoreStarSprite => Or(_juice != null ? _juice.CoreStar : null, _sparkle);
        Sprite GlowCoreSprite => Or(_juice != null ? _juice.GlowCore : null, _glow);
        Sprite ShockwaveSprite => Or(_juice != null ? _juice.Shockwave : null, Or(_juice != null ? _juice.RingGlow : null, _ring));
        Sprite StarRingSprite => Or(_juice != null ? _juice.StarRing : null, ShockwaveSprite);
        Sprite StreakSprite => Or(_juice != null ? _juice.Streak : null, _trail);
        Sprite SparkleClusterSprite => Or(_juice != null ? _juice.SparkleCluster : null, Or(_juice != null ? _juice.MoteTwinkle : null, _sparkle));
        Sprite AfterimageSprite => Or(_juice != null ? _juice.Afterimage : null, _trail);

        // ───────────── 기본 도구 ─────────────

        /// <summary>그림 하나 (add = 가산 비율, 알파 = 불투명도/빛 세기). 회전 0, 바로 보인다</summary>
        public int SpawnFx(Sprite s, Color c, Vector2 at, float size0, float size1, float life, float delay, float add = 0f, bool ground = false)
        {
            int index = Spawn(s, c, at, size0, size1, life, delay, ground);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = Mathf.Clamp01(add);
            p.FadeIn = 0.01f;
            p.Rotation = 0f;
            return index;
        }

        /// <summary>
        /// 명중 그림 (SizeCurve.Impact): 닿는 첫 프레임에 settled × f.FirstFrame으로 가장 크게 → f.Hold 버팀 → f.Settle 동안 settled로 → 끝까지 × endScale.
        /// 45%까지 불투명, 그 뒤 InQuad로 사라진다. life는 부르는 쪽이 정한다 (보통 f.Life × PopScale). 회전 0 (계보마다 부르는 쪽이 돌린다)
        /// </summary>
        public int ImpactArt(Sprite s, Color c, Vector2 at, float settled, in TierFeel f, float life, float delay, float endScale = 0.92f, float add = 0f)
        {
            int index = Spawn(s, c, at, settled * f.FirstFrame, settled, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Impact;
            p.Hold = f.Hold;
            p.Settle = f.Settle;
            p.EndScale = endScale;
            p.Add = Mathf.Clamp01(add);
            p.Rotation = 0f;
            p.FadeIn = 0f;
            p.FadeOut = 0.45f;
            p.FadeKind = FadeInQuad;
            // 첫 프레임부터 꼭대기 크기 (지연 없이 바로 보이는 경우)
            p.Rect.sizeDelta = new Vector2(p.Size0, p.Size0);
            return index;
        }

        /// <summary>
        /// 가산 코어 별 (fx_core_star → sparkle): 명중 순간의 하얀 불빛. 세기 = tint.a × intensity (번쩍임 줄이기 ×0.6), 62%까지 그대로 버티다 꺼진다
        /// </summary>
        public int CoreStar(Vector2 at, Color tint, float size0, float size1, float life, float delay, float intensity = 1f)
        {
            float strength = Mathf.Clamp01(tint.a * intensity * JuiceGates.CoreIntensity(_reduceFlash));
            int index = Spawn(CoreStarSprite, BattleEase.WithAlpha(tint, strength), at, size0, size1, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = 1f;
            p.FadeIn = 0f;
            p.FadeOut = 0.62f;
            p.FadeKind = FadeInQuad;
            p.Spin = Random.Range(-90f, 90f);
            p.Rect.sizeDelta = new Vector2(size0, size0);
            return index;
        }

        /// <summary>
        /// 큰 가산 후광 (fx_glow_core → glow_soft, 크기 0.8 → 1.0): 살아 있는 후광은 품질별 6/4/2개까지 (넘치면 −1). 번쩍임 줄이기면 세기 ×0.7
        /// </summary>
        public int Halo(Vector2 at, Color tint, float size, float life, float delay, float intensity)
        {
            if (!_ready) Prepare();
            if (_halos >= _haloCap) return -1;
            int index = Spawn(GlowCoreSprite, BattleEase.WithAlpha(tint, Mathf.Clamp01(intensity * JuiceGates.HaloIntensity(_reduceFlash))), at, size * 0.8f, size,
                life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = 1f;
            p.Rotation = 0f;
            p.FadeIn = 0f;
            p.FadeOut = 0.3f;
            p.FadeKind = FadeInQuad;
            p.Flags |= FlagHalo;
            _halos++;
            return index;
        }

        /// <summary>
        /// 잉크 받침 (circle_soft, #2D2458, 바닥 레이어 — 맞은 몸 뒤의 어두운 주머니라 파스텔 명중이 라벤더 광장 위에서 읽힌다).
        /// 크기 그대로, 30%부터 InQuad로 사라진다. 낮음 품질이면 없음 (보통 품질은 부르는 쪽이 JuiceGates.InkBacking으로 치명타·처치·보스만).
        /// 바닥 곁들임 층 — 바닥 여유(AllowGround)로 거른다 (바닥이 차면 위쪽으로 넘어가 맞은 몸을 덮지 않게)
        /// </summary>
        public int InkBacking(Vector2 at, float size, float alpha, float life, float delay)
        {
            if (!_ready) Prepare();
            if (_quality == FxQuality.Low || !(alpha > 0f) || !AllowGround(FxPriority.Extra)) return -1;
            int index = Spawn(_soft, BattleEase.WithAlpha(JuiceColors.Ink, alpha), at, size, size, life, delay, true);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Linear;
            p.Rotation = 0f;
            p.FadeIn = 0f;
            p.FadeOut = 0.3f;
            p.FadeKind = FadeInQuad;
            return index;
        }

        /// <summary>
        /// 충격 고리 (fx_shockwave → ring_glow → 실 고리): from → to px OutQuart, 30%부터 사라진다. aspect &lt; 1 = 바닥에 눕힌 고리 (회전 없음).
        /// intensity = 빛 세기, add = 가산 비율. 번쩍임 줄이기면 빛 고리는 가산 비율만큼 ×0.6까지 (JuiceGates.RingLight — 코어 별·섬광·속도선과 같은 문)
        /// </summary>
        public int Shockwave(Vector2 at, Color tint, float from, float to, float life, float delay, float add = 0.85f, float intensity = 0.8f,
            float aspect = 1f, bool ground = false, Sprite sprite = null)
        {
            float strength = Mathf.Clamp01(intensity * JuiceGates.RingLight(_reduceFlash, add));
            int index = Spawn(sprite != null ? sprite : ShockwaveSprite, BattleEase.WithAlpha(tint, strength), at, from, to, life, delay, ground);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = Mathf.Clamp01(add);
            p.Curve = SizeCurve.Quart;
            p.Aspect = aspect;
            p.Rotation = Mathf.Abs(aspect - 1f) < 0.001f ? Random.Range(0f, 360f) : 0f;
            p.FadeIn = 0f;
            p.FadeOut = 0.3f;
            p.Rect.sizeDelta = new Vector2(from, from * aspect);
            return index;
        }

        /// <summary>
        /// 빛줄기 count개 (fx_streak → trail, Motion.Streak): dir 둘레 coneDeg 원뿔(360 = 사방, 고르게)로 speedMin~speedMax px/s, 끌림 9/s로 느려지며
        /// 속도 방향으로 눕고 빠를수록 길다. 길이·두께·빠르기는 기준 px — 안에서 액터 배율(U)을 곱한다. 수명 0.14~0.18초 × PopScale.
        /// tint.a = 빛 세기 (보통 0.9). 별빛 층: 처음 2개는 꼬리 층 여유(20칸 초과)만 있으면, 나머지는 별빛 층 여유가 있을 때만 (영웅 층 몫은 쓰지 않는다).
        /// anchor가 있으면 나타나는 순간까지 그 적을 따라간다
        /// </summary>
        public void Streaks(Vector2 at, Vector2 dir, int count, float coneDeg, float speedMin, float speedMax, Color tint, float delay,
            float length = 22f, float thickness = 5f, float add = 1f, EnemyView anchor = null, Sprite sprite = null)
        {
            if (count <= 0) return;
            if (!_ready) Prepare();
            float unit = UnitScale;
            var forward = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up;
            float baseAngle = Mathf.Atan2(forward.y, forward.x);
            bool all = coneDeg >= 359f;
            float half = coneDeg * 0.5f * Mathf.Deg2Rad;
            float spin = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                if (!Allow(i < 2 ? FxPriority.Trail : FxPriority.Sparkle)) break;
                float angle = all
                    ? spin + (i + Random.Range(-0.3f, 0.3f)) * (Mathf.PI * 2f / count)
                    : baseAngle + (count == 1 ? 0f : Mathf.Lerp(-half, half, (i + Random.Range(0.15f, 0.85f)) / count));
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.Range(speedMin, speedMax) * unit);
                float life = Random.Range(0.14f, 0.18f) * PopScale;
                int index = Streak(at, velocity, length * Random.Range(0.85f, 1.15f) * unit, thickness * Random.Range(0.85f, 1.15f) * unit, life, tint, delay,
                    add, 9f, 0f, sprite);
                AnchorUntilShown(index, anchor, delay);
            }
        }

        /// <summary>
        /// 빛줄기 하나 (최종 px — 배율을 곱하지 않는다): velocity px/s로 나가 drag(1/s)로 느려지고 gravity(px/s², 아래)로 휜다.
        /// 길이 = length × (1 + |v|/600), 두께 thickness → 45%. 머리(그림 오른쪽 끝)가 자리를 앞서 간다. 별빛 층 (영웅 층 몫은 쓰지 않는다)
        /// </summary>
        public int Streak(Vector2 at, Vector2 velocity, float length, float thickness, float life, Color tint, float delay, float add = 1f,
            float drag = 9f, float gravity = 0f, Sprite sprite = null)
        {
            int index = Spawn(sprite != null ? sprite : StreakSprite, tint, at, length, length * 0.75f, life, delay, false, true);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Motion = Motion.Streak;
            p.P1 = velocity;
            p.Drag = Mathf.Max(0f, drag);
            p.Gravity = gravity;
            p.Height0 = Mathf.Max(1f, thickness);
            p.Height1 = Mathf.Max(1f, thickness * 0.45f);
            p.Add = Mathf.Clamp01(add);
            p.FadeIn = 0f;
            p.FadeOut = 0.35f;
            p.Rect.pivot = StreakPivot;
            p.Rotation = velocity.sqrMagnitude > 0.01f ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 0f;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
            p.Rect.sizeDelta = new Vector2(length, p.Height0);
            return index;
        }

        /// <summary>가로 섬광 (fx_flare_cross, 가산): length × thickness px가 0.8 → 1.1로 뻗고 40%부터 꺼진다. 그림이 없으면 이 층은 없다 (−1)</summary>
        public int Flare(Vector2 at, Color tint, float length, float thickness, float angleDeg, float life, float delay, float intensity = 0.9f)
        {
            var sprite = _juice != null ? _juice.FlareCross : null;
            if (sprite == null) return -1;
            int index = Spawn(sprite, BattleEase.WithAlpha(tint, Mathf.Clamp01(intensity * JuiceGates.CoreIntensity(_reduceFlash))), at, length * 0.8f,
                length * 1.1f, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = 1f;
            p.Height0 = thickness;
            p.Height1 = thickness * 0.6f;
            p.Rotation = angleDeg;
            p.FadeIn = 0f;
            p.FadeOut = 0.4f;
            p.FadeKind = FadeInQuad;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            p.Rect.sizeDelta = new Vector2(length * 0.8f, thickness);
            return index;
        }

        /// <summary>
        /// 집중 속도선 (fx_speedlines, 가산, 크기 0.9 → 1.05, 무작위 회전). 세기 = intensity × 코어 세기 (번쩍임 줄이기 ×0.6 — 코어 별·섬광과 같은 문:
        /// 치명타·보스 처치·불 밝히기의 하얀 줄무늬가 줄이기 설정에서 그대로 남지 않게). 그림이 없거나 품질이 끄면 −1 (부르는 쪽이 JuiceGates.Speedlines로 거른다)
        /// </summary>
        public int Speedlines(Vector2 at, Color tint, float size, float life, float delay, float intensity = 0.5f)
        {
            var sprite = _juice != null ? _juice.Speedlines : null;
            if (sprite == null) return -1;
            float strength = Mathf.Clamp01(intensity * JuiceGates.CoreIntensity(_reduceFlash));
            int index = Spawn(sprite, BattleEase.WithAlpha(tint, strength), at, size * 0.9f, size * 1.05f, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = 1f;
            p.FadeIn = 0f;
            p.FadeOut = 0.3f;
            p.FadeKind = FadeInQuad;
            return index;
        }

        /// <summary>
        /// 색종이 (처치 = '풀려남', 축하): 묶음(set)의 조각을 차례로 count개 — 위쪽 원뿔로 튀어 중력에 떨어지며 돌고, 리본·꽃잎·실은 팔랑인다.
        /// 쇠붙이 조각은 바닥에 한 번 튄다. power = 빠르기 배율 (1 = 보통). 크기·빠르기는 기준 px × U.
        /// 별빛 층: 처음 2개는 꼬리 층 여유(20칸 초과)만 있으면, 나머지는 별빛 층 여유가 있을 때만 (영웅 층 몫은 쓰지 않는다)
        /// </summary>
        public void Confetti(Vector2 at, ConfettiSet set, int count, float power, float delay)
        {
            if (count <= 0) return;
            if (!_ready) Prepare();
            var pieces = ConfettiPieces(set);
            ConfettiMotion(set, out float speedMin, out float speedMax, out float cone, out float gravity, out float lifeMin, out float lifeMax, out float sizeScale);
            float unit = UnitScale;
            float speedScale = Mathf.Max(0.2f, power) * unit;
            float half = cone * 0.5f * Mathf.Deg2Rad;
            int start = Random.Range(0, pieces.Length);
            for (int i = 0; i < count; i++)
            {
                if (!Allow(i < 2 ? FxPriority.Trail : FxPriority.Sparkle)) break;
                var piece = pieces[(start + i) % pieces.Length];
                var sprite = PieceSprite(piece.Art);
                if (sprite == null) continue;
                float angle = Mathf.PI * 0.5f + Random.Range(-half, half);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Random.Range(speedMin, speedMax) * speedScale);
                float size = Random.Range(piece.SizeMin, piece.SizeMax) * sizeScale * unit;
                float life = Random.Range(lifeMin, lifeMax);
                var color = piece.Gold ? (Random.value < 0.5f ? UITheme.Gold : UITheme.Cream) : piece.Color;
                int index = Spawn(sprite, color, at + Random.insideUnitCircle * (10f * unit), size, size * 0.85f, life, delay + i * 0.006f, false, true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Add = piece.Add;
                p.FadeIn = 0.02f;
                p.FadeOut = 0.7f;
                p.Spin = Random.Range(-540f, 540f);
                if (piece.Bounce)
                {
                    p.Motion = Motion.Bounce;
                    p.P1 = velocity * 0.6f;
                    p.P2 = new Vector2(at.y - Random.Range(36f, 60f) * unit, 900f);
                    p.P3 = new Vector2(0.45f, 0f);
                }
                else
                {
                    p.Motion = Motion.Ballistic;
                    p.P1 = velocity;
                    p.Drag = 1.6f;
                    p.Gravity = piece.Flutter ? gravity * 0.55f : gravity;
                    if (piece.Flutter)
                    {
                        p.Wobble = 12f * unit;
                        p.Spin *= 0.35f;
                    }
                }
            }
        }

        /// <summary>
        /// 여운 반짝 (fx_sparkle_cluster → fx_mote_twinkle, 가산 0.8): spread px 안에 count개가 100ms 뒤 피어나 30px 떠오르며 6Hz로 1 ± 0.15 반짝인다.
        /// 색은 tint / 크림 번갈아. 크기 기준 px × U. 별빛 층: 첫 하나는 꼬리 층 여유(20칸 초과)만 있으면, 나머지는 별빛 층 여유가 있을 때만
        /// </summary>
        public void Twinkles(Vector2 at, float spread, int count, Color tint, float delay)
        {
            if (count <= 0) return;
            if (!_ready) Prepare();
            float unit = UnitScale;
            bool cluster = _juice != null && _juice.SparkleCluster != null;
            var sprite = SparkleClusterSprite;
            for (int i = 0; i < count; i++)
            {
                if (!Allow(i < 1 ? FxPriority.Trail : FxPriority.Sparkle)) break;
                var position = at + Random.insideUnitCircle * spread;
                float size = (cluster ? Random.Range(28f, 40f) : Random.Range(16f, 24f)) * unit;
                int index = Spawn(sprite, i % 2 == 0 ? tint : UITheme.Cream, position, size, size * 0.9f, Random.Range(0.6f, 0.8f), delay + 0.1f + i * 0.04f,
                    false, true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Add = 0.8f;
                p.P1 = Vector2.up * (30f * unit);
                p.Pulse = 0.15f;
                p.PulseHz = 6f;
                p.FadeIn = 0.15f;
                p.FadeOut = 0.6f;
                p.Spin = Random.Range(-40f, 40f);
            }
        }

        /// <summary>
        /// 바닥 자국 (바닥 레이어, 바닥 눌림, 2.5초 — 마지막 1초에 사라진다): 살아 있는 자국은 품질별 12/6/0개까지. tint.a = 불투명도 (보통 0.45).
        /// 바닥 곁들임 층 — 바닥 여유(AllowGround)로 거른다
        /// </summary>
        public int Decal(Vector2 groundAt, Sprite s, Color tint, float size, float delay)
        {
            if (!_ready) Prepare();
            if (s == null || _decals >= _decalCap || !AllowGround(FxPriority.Extra)) return -1;
            int index = Spawn(s, tint, groundAt, size, size, 2.5f, delay, true);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Linear;
            p.Aspect = GroundAspect;
            p.Rotation = 0f;
            p.FadeIn = 0.04f;
            p.FadeOut = 0.6f;
            p.Flags |= FlagDecal;
            _decals++;
            return index;
        }

        /// <summary>입자가 적을 따라다니게 한다 (AnchorTo 공개판)</summary>
        public void AnchorFx(int index, EnemyView enemy) => AnchorTo(index, enemy);

        /// <summary>입자가 잔향이를 따라다니게 한다</summary>
        public void AnchorFx(int index, EchoView echo) => AnchorTo(index, echo);

        /// <summary>지연된 입자가 나타나는 순간까지만 적을 따라간다 (그 뒤로는 제 길로)</summary>
        public void AnchorFxUntilShown(int index, EnemyView enemy, float delay) => AnchorUntilShown(index, enemy, delay);

        // ───────────── 색종이 묶음 (§1.22, 할당 없이 한 번만 만든다) ─────────────

        enum PieceArt : byte
        {
            Button,
            Ribbon,
            Star,
            Thread,
            Dust,
            Petal,
            Twinkle,
            Puff,
            Scale,
            MotifStar,
            Gear,
            Bolt,
            Spring,
        }

        readonly struct ConfettiPiece
        {
            public readonly PieceArt Art;
            public readonly Color Color;
            public readonly float Add;
            public readonly float SizeMin;
            public readonly float SizeMax;
            public readonly bool Bounce;
            public readonly bool Flutter;
            /// <summary>금 / 크림 반반</summary>
            public readonly bool Gold;

            public ConfettiPiece(PieceArt art, Color color, float add = 0f, float sizeMin = 14f, float sizeMax = 22f, bool bounce = false, bool flutter = false,
                bool gold = false)
            {
                Art = art;
                Color = color;
                Add = add;
                SizeMin = sizeMin;
                SizeMax = sizeMax;
                Bounce = bounce;
                Flutter = flutter;
                Gold = gold;
            }
        }

        static readonly Color DustGrey = new(0.78f, 0.74f, 0.88f, 1f);
        static readonly Color PuffLavender = new(0.86f, 0.82f, 0.95f, 0.6f);

        static readonly ConfettiPiece[] DustPieces =
        {
            new(PieceArt.Dust, DustGrey), new(PieceArt.Star, UITheme.Cream, 0.5f, gold: true), new(PieceArt.Puff, PuffLavender, 0f, 16f, 24f),
            new(PieceArt.Dust, DustGrey), new(PieceArt.Star, UITheme.Cream, 0.5f), new(PieceArt.Dust, DustGrey),
            new(PieceArt.Puff, PuffLavender, 0f, 16f, 24f), new(PieceArt.Star, UITheme.Cream, 0.5f, gold: true), new(PieceArt.Dust, DustGrey),
            new(PieceArt.Puff, PuffLavender, 0f, 16f, 24f),
        };

        static readonly ConfettiPiece[] MothPieces =
        {
            new(PieceArt.Scale, new Color(0.85f, 0.79f, 0.7f, 1f), flutter: true), new(PieceArt.Petal, UITheme.DustyPink, flutter: true),
            new(PieceArt.Scale, UITheme.Lavender, flutter: true), new(PieceArt.Petal, UITheme.Cream, flutter: true),
            new(PieceArt.Scale, new Color(0.85f, 0.79f, 0.7f, 1f), flutter: true), new(PieceArt.Petal, UITheme.DustyPink, flutter: true),
            new(PieceArt.Scale, UITheme.Lavender, flutter: true), new(PieceArt.Petal, UITheme.Cream, flutter: true),
            new(PieceArt.Scale, new Color(0.85f, 0.79f, 0.7f, 1f), flutter: true), new(PieceArt.Petal, UITheme.DustyPink, flutter: true),
        };

        static readonly ConfettiPiece[] MetalPieces =
        {
            new(PieceArt.Gear, new Color(0.79f, 0.64f, 0.36f, 1f), bounce: true), new(PieceArt.Bolt, new Color(0.75f, 0.74f, 0.82f, 1f), bounce: true),
            new(PieceArt.Spring, new Color(0.71f, 0.4f, 0.23f, 1f), bounce: true), new(PieceArt.Button, UITheme.Gold),
            new(PieceArt.Gear, new Color(0.79f, 0.64f, 0.36f, 1f), bounce: true), new(PieceArt.Bolt, new Color(0.75f, 0.74f, 0.82f, 1f), bounce: true),
            new(PieceArt.Spring, new Color(0.71f, 0.4f, 0.23f, 1f), bounce: true), new(PieceArt.Button, UITheme.Gold),
        };

        static readonly ConfettiPiece[] ShadowPieces =
        {
            new(PieceArt.Thread, UITheme.Lavender, 0f, 16f, 24f, flutter: true), new(PieceArt.MotifStar, UITheme.Lamp, 0.5f),
            new(PieceArt.Thread, UITheme.Lamp, 0f, 16f, 24f, flutter: true), new(PieceArt.MotifStar, UITheme.Lamp, 0.5f),
            new(PieceArt.Thread, UITheme.Lavender, 0f, 16f, 24f, flutter: true), new(PieceArt.MotifStar, UITheme.Lamp, 0.5f),
            new(PieceArt.Thread, UITheme.Lamp, 0f, 16f, 24f, flutter: true), new(PieceArt.MotifStar, UITheme.Lamp, 0.5f),
            new(PieceArt.Thread, UITheme.Lavender, 0f, 16f, 24f, flutter: true), new(PieceArt.MotifStar, UITheme.Lamp, 0.5f),
        };

        static readonly ConfettiPiece[] CelebrationPieces =
        {
            new(PieceArt.Button, UITheme.Lavender, 0f, 18f, 28f), new(PieceArt.Ribbon, UITheme.DustyPink, 0f, 22f, 32f, flutter: true),
            new(PieceArt.Star, UITheme.Cream, 0.5f, 18f, 28f, gold: true), new(PieceArt.Thread, UITheme.SoftBlue, 0f, 18f, 28f, flutter: true),
            new(PieceArt.MotifStar, UITheme.Gold, 0.5f, 18f, 26f), new(PieceArt.Button, UITheme.SoftBlue, 0f, 18f, 28f),
            new(PieceArt.Ribbon, UITheme.Lavender, 0f, 22f, 32f, flutter: true), new(PieceArt.Star, UITheme.Cream, 0.5f, 18f, 28f),
            new(PieceArt.Thread, UITheme.DustyPink, 0f, 18f, 28f, flutter: true), new(PieceArt.Button, UITheme.Gold, 0f, 18f, 28f),
        };

        static readonly ConfettiPiece[] BossPieces =
        {
            new(PieceArt.Button, UITheme.Gold, 0f, 18f, 30f), new(PieceArt.Ribbon, UITheme.DustyPink, 0f, 24f, 34f, flutter: true),
            new(PieceArt.Star, UITheme.Cream, 0.5f, 18f, 30f, gold: true), new(PieceArt.Thread, UITheme.Lavender, 0f, 18f, 28f, flutter: true),
            new(PieceArt.MotifStar, UITheme.Gold, 0.5f, 18f, 28f), new(PieceArt.Button, UITheme.Lavender, 0f, 18f, 30f),
            new(PieceArt.Ribbon, UITheme.SoftBlue, 0f, 24f, 34f, flutter: true), new(PieceArt.Star, UITheme.Cream, 0.5f, 18f, 30f),
            new(PieceArt.Thread, UITheme.DustyPink, 0f, 18f, 28f, flutter: true), new(PieceArt.MotifStar, UITheme.Lamp, 0.5f, 18f, 28f),
            new(PieceArt.Button, UITheme.DustyPink, 0f, 18f, 30f), new(PieceArt.Star, UITheme.Gold, 0.5f, 18f, 30f),
        };

        static ConfettiPiece[] ConfettiPieces(ConfettiSet set) => set switch
        {
            ConfettiSet.Moth => MothPieces,
            ConfettiSet.Metal => MetalPieces,
            ConfettiSet.Shadow => ShadowPieces,
            ConfettiSet.Celebration => CelebrationPieces,
            ConfettiSet.Boss => BossPieces,
            _ => DustPieces,
        };

        /// <summary>
        /// 묶음별 움직임: 처치 220~420px/s 140° 중력 900 / 보스 380~760 160° / 축하 300~650 150° 중력 700.
        /// 조각 크기 배율은 HitFeel.ConfettiSizeScale (처치 갈래 ×1.45 · 보스 ×1.15 · 축하 ×1 — 수는 그대로)
        /// </summary>
        static void ConfettiMotion(ConfettiSet set, out float speedMin, out float speedMax, out float cone, out float gravity, out float lifeMin, out float lifeMax,
            out float sizeScale)
        {
            sizeScale = HitFeel.ConfettiSizeScale(set);
            switch (set)
            {
                case ConfettiSet.Boss:
                    speedMin = 380f;
                    speedMax = 760f;
                    cone = 160f;
                    gravity = 900f;
                    lifeMin = 0.8f;
                    lifeMax = 1.3f;
                    break;
                case ConfettiSet.Celebration:
                    speedMin = 300f;
                    speedMax = 650f;
                    cone = 150f;
                    gravity = 700f;
                    lifeMin = 0.9f;
                    lifeMax = 1.4f;
                    break;
                default:
                    speedMin = 220f;
                    speedMax = 420f;
                    cone = 140f;
                    gravity = 900f;
                    lifeMin = 0.45f;
                    lifeMax = 0.65f;
                    break;
            }
        }

        Sprite PieceSprite(PieceArt art)
        {
            var juice = _juice;
            switch (art)
            {
                case PieceArt.Button: return Or(juice != null ? juice.ConfettiButton : null, Motif(VfxMotif.Button));
                case PieceArt.Ribbon: return Or(juice != null ? juice.ConfettiRibbon : null, _trail);
                case PieceArt.Star: return Or(juice != null ? juice.ConfettiStar : null, Motif(VfxMotif.Star));
                case PieceArt.Thread: return Or(juice != null ? juice.ConfettiThread : null, _trail);
                case PieceArt.Dust: return Or(juice != null ? juice.MoteDust : null, Puff);
                case PieceArt.Petal: return Or(juice != null ? juice.MotePetal : null, Motif(VfxMotif.Heart));
                case PieceArt.Twinkle: return Or(juice != null ? juice.MoteTwinkle : null, _sparkle);
                case PieceArt.Puff: return Puff;
                case PieceArt.Scale: return Motif(VfxMotif.Scale);
                case PieceArt.MotifStar: return Motif(VfxMotif.Star);
                case PieceArt.Gear: return Motif(VfxMotif.Gear);
                case PieceArt.Bolt: return Motif(VfxMotif.Bolt);
                case PieceArt.Spring: return Motif(VfxMotif.Spring);
                default: return _sparkle;
            }
        }

        // ───────────── 화면 ─────────────

        /// <summary>
        /// 화면 킥: dir 쪽으로 px(최대 HitFeel.KickMax 5)만큼 30ms에 밀렸다가 돌아온다 (전장만, 흔들림 배율을 곱한다). 채널마다 최소 간격 (치명타 0.5초 …).
        /// delay = 그 순간까지 (연출 시계)
        /// </summary>
        public void Kick(Vector2 dir, float px, KickChannel channel, float delay = 0f)
        {
            if (!_ready) Prepare();
            if (delay > 0.001f)
            {
                QueueCue(FxCueKind.Kick, delay, false, dir, px, 0f, 0f, 0f, default, false, null, (byte)channel);
                return;
            }
            FireKick(dir, px, channel);
        }

        void FireKick(Vector2 dir, float px, KickChannel channel)
        {
            if (!_gov.TryKick(channel, _realClock)) return;
            _kick.Add(dir, px);
        }

        /// <summary>
        /// 줌 펀치: 전장만 peak배(1.02 기본 · 보스 처치 1.03 · 불 밝히기 1.035)로 60ms에 커졌다가 220ms에 돌아온다. pivotField(전장 로컬)가 제자리에 남는다.
        /// 0.8초에 한 번 (force = 보스 처치·불 밝히기). 흔들림 배율 0이면 커지지 않는다
        /// </summary>
        public void ZoomPunch(float peak, Vector2 pivotField, float delay = 0f, bool force = false)
        {
            if (!_ready) Prepare();
            if (delay > 0.001f)
            {
                QueueCue(FxCueKind.Zoom, delay, false, pivotField, peak, 0f, 0f, 0f, default, force);
                return;
            }
            FireZoom(peak, pivotField, force);
        }

        void FireZoom(float peak, Vector2 pivot, bool force)
        {
            if (!_gov.TryZoom(_realClock, force)) return;
            if (_zoom.Punch(peak)) _zoomPivot = pivot;
        }

        /// <summary>
        /// 전장 어둡게 (FieldDim — Fx 아래라 액터·배경만 어두워지고 연출·숫자는 밝게 남는다): fadeIn 동안 alpha까지 OutQuad, hold 버팀, fadeOut 동안 걷힌다
        /// (모두 실제 시간). 최대 0.45, 번쩍임 줄이기 0.2. 지금 더 어두우면 무시. delay = 시작까지 (연출 시계)
        /// </summary>
        public void FieldDim(float alpha, float fadeIn, float hold, float fadeOut, float delay = 0f)
        {
            if (!_ready) Prepare();
            if (delay > 0.001f)
            {
                QueueCue(FxCueKind.Dim, delay, false, Vector2.zero, alpha, fadeIn, hold, fadeOut);
                return;
            }
            StartDim(alpha, fadeIn, hold, fadeOut);
        }

        /// <summary>
        /// 전장 빛 번쩍임 (스크린 블렌드 — 우유빛 막이 아니라 밝아진다): 33ms에 오르고 duration(실제 시간) 동안 잦아든다.
        /// 최대 0.45 (번쩍임 줄이기 0.3), 전장 전체 번쩍임 1초에 하나 (force = 불 밝히기·보스 처치, 자리를 차지하고 1초 동안 막는다). 막히면 버린다
        /// </summary>
        public void LightFlash(Color color, float intensity, float duration, float delay = 0f, bool force = false)
        {
            if (!_ready) Prepare();
            if (delay > 0.001f)
            {
                QueueCue(FxCueKind.Light, delay, false, Vector2.zero, intensity, duration, 0f, 0f, color, force);
                return;
            }
            FireLight(color, intensity, duration, force);
        }

        void FireLight(Color color, float intensity, float duration, bool force)
        {
            if (_fieldFlash == null) return;
            if (!_gov.TryFieldFlash(_realClock, force)) return;
            StartOverlay(ref _fieldOverlay, _fieldFlash, color, ClampLight(intensity), duration, true);
        }

        /// <summary>
        /// 임팩트 프레임 (보스 처치·불 밝히기 터짐): 어둡게 0.45 두 프레임(33ms) → 0.2 한 프레임 → 150ms에 걷힘(body 없으면 200ms) +
        /// body가 있으면 그 몸이 크림 실루엣으로 두 프레임, 반투명으로 한 프레임 (misprint면 분홍/하늘 인쇄 어긋남 ±6px 100ms).
        /// 번쩍임 줄이기면 어둡게 0.2 100ms만. 전장 전체 번쩍임 1초 규칙 (force = 자리 차지). delay = 그 순간까지 (연출 시계)
        /// </summary>
        public void ImpactFrame(Graphic body, float delay, bool misprint, bool force = false)
        {
            if (!_ready) Prepare();
            if (delay > 0.001f)
            {
                QueueCue(FxCueKind.ImpactFrame, delay, false, Vector2.zero, misprint ? 1f : 0f, 0f, 0f, 0f, default, force, body);
                return;
            }
            FireImpactFrame(body, misprint, force);
        }

        void FireImpactFrame(Graphic body, bool misprint, bool force)
        {
            if (!_gov.TryFieldFlash(_realClock, force)) return;
            _impactDimAge = 0f;
            _impactDimReduced = _reduceFlash;
            _impactDimFade = body != null ? 0.15f : 0.2f;
            if (_reduceFlash || body == null) return;
            StartSilhouette(SilhouetteCream, body, JuiceColors.CoreCream, Vector2.zero, 1f, 0.033f, 0.5f, 0.05f);
            if (misprint && JuiceGates.Misprint(_quality, _reduceFlash)) StartMisprint(body, 6f, 0.1f, 0.5f);
        }

        /// <summary>
        /// 인쇄 어긋남 (동화책 색판 밀림): body의 분홍 사본이 (−offset, +offset/3), 하늘 사본이 (+offset, −offset/3)에 alpha로 life(실제 시간)만큼.
        /// 번쩍임 줄이기·낮음 품질에서는 없고, 1초에 한 번 (치명타 처치 P2: 3px, 0.4, 50ms). 전장 전체로는 절대 쓰지 않는다
        /// </summary>
        public void Misprint(Graphic body, float delay, float offsetPx, float life, float alpha)
        {
            if (!_ready) Prepare();
            if (delay > 0.001f)
            {
                QueueCue(FxCueKind.Misprint, delay, false, Vector2.zero, offsetPx, life, alpha, 0f, default, false, body);
                return;
            }
            if (body == null || !JuiceGates.Misprint(_quality, _reduceFlash) || !_gov.TryMisprint(_realClock)) return;
            StartMisprint(body, offsetPx, life, alpha);
        }

        void StartMisprint(Graphic body, float offset, float life, float alpha)
        {
            StartSilhouette(SilhouettePink, body, JuiceColors.MisprintPink, new Vector2(-offset, offset / 3f), alpha, life, alpha, life);
            StartSilhouette(SilhouetteBlue, body, JuiceColors.MisprintBlue, new Vector2(offset, -offset / 3f), alpha, life, alpha, life);
        }

        /// <summary>범위 스킬 흔들림 (스킬 채널, 초당 0.5까지 — 20마리를 한꺼번에 쳐도 한 번 크기). HitFeel.AreaSkillTrauma = 0.4</summary>
        public void AddSkillTrauma(float amount, Vector2 direction = default) => _trauma.AddSkill(amount, direction);

        /// <summary>짧은 진동 (Haptics 설정·간격 규칙을 따른다). delay = 그 순간까지 (연출 시계). 시험장 DevOverride 동안은 울리지 않는다</summary>
        public void Haptic(HapticKind kind, float delay = 0f)
        {
            if (!_ready) Prepare();
            if (delay > 0.001f)
            {
                QueueCue(FxCueKind.Haptic, delay, false, Vector2.zero, 0f, 0f, 0f, 0f, default, false, null, (byte)kind);
                return;
            }
            FireHaptic(kind);
        }

        void FireHaptic(HapticKind kind)
        {
            if (_dev.Active) return;
            Haptics.Play(kind);
        }

        // ───────────── 밀도 관리 (§2.4) ─────────────

        bool _rhythmAccent;
        int _rhythmChain;
        int _multiKill;
        Vector2 _multiKillCentroid;

        /// <summary>
        /// 이 대상에 delay 뒤 닿는 명중이 풀 명중인지 (같은 대상 90ms에 하나 — 아니면 미니). 사슬의 3번째마다 리듬 강조(RhythmAccent = true, 늘 풀).
        /// targetId가 음수면 늘 풀
        /// </summary>
        public bool TryFullImpact(int targetId, float delay) => _gov.TryFullImpact(targetId, _clock + delay, out _rhythmAccent, out _rhythmChain);

        /// <summary>마지막 TryFullImpact가 리듬 강조였는지 (코어 ×1.15 + 고리, 숫자 1.2배 톡)</summary>
        public bool RhythmAccent => _rhythmAccent;

        /// <summary>마지막 TryFullImpact 대상의 사슬 길이 (합쳐진 숫자 톡 1.1 + 0.03 × min(사슬, 5))</summary>
        public int RhythmChain => _rhythmChain;

        /// <summary>delay 뒤 도착 전 0.25초 안에 예약된 도착 수 (≤ 8 풀 · 9~16 미니 · 그 이상 린). 이 명중의 NoteHit 전에 묻는다</summary>
        public int Density(float delay) => _gov.Density(_clock + delay);

        /// <summary>delay 뒤 닿는 명중 하나를 밀도에 센다</summary>
        public void NoteHit(float delay) => _gov.NoteArrival(_clock + delay);

        /// <summary>
        /// 큰 터짐(치명타·처치·스킬 폭발) 시작 정리: 0.1초에 2개까지는 delay 그대로, 세 번째는 +0.033, 네 번째는 +0.05초 미룬 delay, 그 뒤로는 −1 (작게 그린다)
        /// </summary>
        public float BigBurstDelay(float delay)
        {
            float extra = _gov.BigBurstDelay(_clock + delay);
            return extra < 0f ? -1f : delay + extra;
        }

        /// <summary>풀 처치 꽃을 0.2초에 3개까지 (false면 미니 처치)</summary>
        public bool TryKillBurst(float delay) => _gov.TryKillBurst(_clock + delay);

        /// <summary>지금 연속 처치 수 (1.2초 안에 이어진 처치, NoteKill이 센다)</summary>
        public int KillStreak => _gov.Streak;

        /// <summary>마지막 NoteKill과 0.25초 안에 함께 터진 처치 수 (3 이상이면 '한꺼번에')</summary>
        public int MultiKill => _multiKill;

        /// <summary>그 처치들의 가운데 (전장 로컬)</summary>
        public Vector2 MultiKillCentroid => _multiKillCentroid;

        /// <summary>
        /// 큰 터짐 가운데를 전장 안으로 당긴다: 가장자리·상단 HUD(SafeTop)에서 radius만큼 안쪽 (radius = 0.3 × 크기면 잘림 40% 이하)
        /// </summary>
        public Vector2 ClampToSafe(Vector2 at, float radius)
        {
            float half = FieldHalfWidth;
            float r = Mathf.Max(0f, radius);
            float minX = -half + r;
            float maxX = half - r;
            if (minX > maxX) minX = maxX = 0f;
            float minY = FieldBottom + r;
            float maxY = SafeTop - r;
            if (minY > maxY) minY = maxY = (minY + maxY) * 0.5f;
            return new Vector2(Mathf.Clamp(at.x, minX, maxX), Mathf.Clamp(at.y, minY, maxY));
        }

        /// <summary>스킬 조명(전장 어둡게 0.15)을 써도 되는지 — 2초에 한 번</summary>
        public bool TrySpotlight() => _gov.TrySpotlight(_realClock);

        // ───────────── 이음매 ─────────────

        /// <summary>
        /// 처치 하나를 센다 (ally가 OnEnemyKilled에서): 연속 처치(1.2초)·한꺼번에(0.25초) 수를 갱신하고 show의 OnKillNoted(알림·마지막 일격 숫자)를 부른다.
        /// delay = 처치 꽃이 터지는 순간까지 (처치 보류), byTier = 처치한 일격의 등급
        /// </summary>
        public void NoteKill(int enemyId, Vector2 at, float delay, bool boss, HitTier byTier)
        {
            if (!_ready) Prepare();
            _gov.NoteKill(_clock + delay, at, out _multiKill, out _multiKillCentroid);
            OnKillNoted(enemyId, at, delay, boss, byTier);
        }

        /// <summary>
        /// 스킬 명중 하나 (E7, ally의 OnEnemyHit 시전 맥락에서 _fx.Hit 대신): 코어 기본 명중 —
        /// 한 대상 방식(투사체·광선)은 풀(잉크 받침 · 코어 별 0.6 · 고리 0.4 → 1.2 · 빛줄기 6), 범위 방식은 미니(코어 0.4 · 빛줄기 2), 장판 틱은 불씨 하나.
        /// 밀도가 8을 넘으면 미니, 16을 넘으면 코어만. 크기 S = 170 × U (보스 ×1.2). 그 뒤 show의 OnSkillHit이 방식별 결을 더한다
        /// </summary>
        public void SkillHit(in SkillHitFx r)
        {
            if (!_ready) Prepare();
            var target = r.Target;
            float unit = UnitScale;
            float size = 170f * unit * (r.Boss ? 1.2f : 1f);
            var at = r.Point;
            ClampToField(ref at);
            if (r.Tick)
            {
                int ember = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(JuiceColors.TowardCream(r.Main, 0.3f), 0.75f), at, 24f * unit, 14f * unit, 0.2f, r.Delay, 0.6f);
                AnchorTo(ember, target);
            }
            else
            {
                bool single = r.Style == SkillVfxStyle.Projectile || r.Style == SkillVfxStyle.Beam;
                int density = Density(r.Delay);
                NoteHit(r.Delay);
                bool full = single && density <= HitFeel.DensityMini && TryFullImpact(target != null ? target.Id : -1, r.Delay);
                var feel = HitFeel.Feel(HitTier.Skill);
                var coreTint = BattleEase.WithAlpha(JuiceColors.TowardCream(r.Main, 0.6f), 0.9f);
                var streakTint = BattleEase.WithAlpha(Color.Lerp(r.Main, UITheme.Cream, 0.4f), 0.9f);
                if (full)
                {
                    if (JuiceGates.InkBacking(_quality, true))
                    {
                        int backing = InkBacking(at, size * feel.BackingSize, feel.Backing, 0.18f, r.Delay);
                        AnchorTo(backing, target);
                    }
                    int core = CoreStar(at, coreTint, size * 0.6f, size * 0.42f, feel.CoreLife, r.Delay);
                    AnchorTo(core, target);
                    int ring = Shockwave(at, r.Main, size * 0.4f, size * 1.2f, 0.2f, r.Delay, 0.8f, 0.7f);
                    AnchorTo(ring, target);
                    Streaks(at, r.Direction, feel.Streaks, feel.StreakCone, feel.StreakSpeedMin, feel.StreakSpeedMax, streakTint, r.Delay, 24f, 5f, 1f, target);
                }
                else
                {
                    int core = CoreStar(at, coreTint, size * 0.4f, size * 0.3f, 0.04f, r.Delay);
                    AnchorTo(core, target);
                    if (density <= HitFeel.DensityLean) Streaks(at, r.Direction, 2, feel.StreakCone, feel.StreakSpeedMin, feel.StreakSpeedMax, streakTint, r.Delay, 20f, 4f, 1f, target);
                }
            }
            OnSkillHit(r);
        }

        /// <summary>show(BattleFx.Callouts.cs)가 채운다: 연속·한꺼번에 알림, 마지막 일격 숫자 +15%</summary>
        partial void OnKillNoted(int enemyId, Vector2 at, float delay, bool boss, HitTier byTier);

        /// <summary>show(BattleFx.Skills.cs)가 채운다: 스킬 방식별 명중 결 (조각·빛줄기·킥)</summary>
        partial void OnSkillHit(SkillHitFx r);

        // ───────────── 화면 효과 큐 (늦은 킥·줌·어둡게·빛·임팩트 프레임·인쇄 어긋남·진동·흔들림·보스 순간) ─────────────

        /// <summary>
        /// 32칸 (FxCueQueue): 막힐 것이 뻔한 같은 종류는 합치고, 가득 차면 우선순위가 낮은 큐를 밀어내거나 들어오는 큐를 버린다 —
        /// 어떤 큐도 제 시각보다 일찍 터지지 않는다 (예전처럼 가득 찰 때 지금 터뜨리면 보스 처치 순간이 터짐보다 먼저 와 진짜 순간을 막았다)
        /// </summary>
        readonly FxCueQueue _cues = new();

        /// <summary>흔들림 큐의 Code: 사방으로 (TraumaShake.AddRadial — 불 밝히기, SPEC4 §13.5 c). 0 = 방향 V로 기운다</summary>
        const byte RadialTraumaCode = 1;

        void QueueCue(FxCueKind kind, float delay, bool real, Vector2 v, float a = 0f, float b = 0f, float c = 0f, float d = 0f, Color color = default,
            bool force = false, Graphic body = null, byte code = 0)
        {
            var cue = new FxCue
            {
                Kind = kind, Real = real, At = (real ? _realClock : _clock) + Mathf.Max(0f, delay), V = v, A = a, B = b, C = c, D = d, Color = color,
                Force = force, Body = body, Code = code,
            };
            _cues.Enqueue(in cue);
        }

        /// <summary>시각이 된 큐를 이른 것부터 터뜨린다 (터뜨리는 동안 새로 들어온 큐가 있어도 한 프레임에 칸 수 × 2개까지만)</summary>
        void FireDueCues()
        {
            int guard = 0;
            while (guard++ < FxCueQueue.Capacity * 2 && _cues.TakeDue(_clock, _realClock, out var cue)) FireCue(in cue);
        }

        void FireCue(in FxCue cue)
        {
            switch (cue.Kind)
            {
                case FxCueKind.Kick:
                    FireKick(cue.V, cue.A, (KickChannel)cue.Code);
                    break;
                case FxCueKind.Zoom:
                    FireZoom(cue.A, cue.V, cue.Force);
                    break;
                case FxCueKind.Dim:
                    StartDim(cue.A, cue.B, cue.C, cue.D);
                    break;
                case FxCueKind.Light:
                    FireLight(cue.Color, cue.A, cue.B, cue.Force);
                    break;
                case FxCueKind.ImpactFrame:
                    FireImpactFrame(cue.Body, cue.A > 0.5f, cue.Force);
                    break;
                case FxCueKind.Misprint:
                    Misprint(cue.Body, 0f, cue.A, cue.B, cue.C);
                    break;
                case FxCueKind.Haptic:
                    FireHaptic((HapticKind)cue.Code);
                    break;
                case FxCueKind.Trauma:
                    if (cue.Code == RadialTraumaCode) _trauma.AddRadial(cue.A);
                    else _trauma.Add(cue.A, cue.V);
                    break;
                case FxCueKind.BossMoment:
                    FireBossKillMoment(cue.V, cue.Body);
                    break;
            }
        }

        void ClearCues() => _cues.Clear();

        // ───────────── 번쩍임 덮개 (빛 = 스크린 블렌드 / 틴트 = 알파) ─────────────

        struct Overlay
        {
            public bool Active;
            public bool Light;
            public Color Color;
            public float Peak;
            public float Age;
            public float Life;
            public float Attack;
            /// <summary>이번 프레임 세기 (metrics)</summary>
            public float Value;
        }

        Overlay _fieldOverlay;
        Overlay _screenOverlay;

        /// <summary>빛 세기 상한: 0.45 (번쩍임 줄이기 0.3)</summary>
        float ClampLight(float peak) => Mathf.Clamp(peak, 0f, _reduceFlash ? GameSettings.ReducedFlashMaxAlpha : 0.45f);

        /// <summary>틴트 알파 상한: 0.3</summary>
        static float ClampTint(float peak) => Mathf.Clamp(peak, 0f, GameSettings.ReducedFlashMaxAlpha);

        void StartOverlay(ref Overlay o, Image image, Color color, float peak, float duration, bool light)
        {
            if (image == null || !(peak > 0.001f)) return;
            o.Active = true;
            o.Light = light;
            o.Color = color;
            o.Peak = peak;
            o.Age = 0f;
            o.Life = Mathf.Max(0.05f, duration);
            o.Attack = light ? Mathf.Min(0.033f, o.Life * 0.1f) : o.Life * 0.12f;
            o.Value = 0f;
            // 빛은 스크린 블렌드 머티리얼로, 틴트는 기본 UI 머티리얼로 (시작할 때만 바꾼다)
            image.material = light && _screenMaterial != null ? _screenMaterial : null;
            image.raycastTarget = false;
            image.enabled = true;
            WriteOverlay(in o, image, 0f);
        }

        static float OverlayValue(in Overlay o)
        {
            if (!o.Active) return 0f;
            if (o.Age < o.Attack) return o.Peak * (o.Age / Mathf.Max(0.0001f, o.Attack));
            float u = Mathf.Clamp01((o.Age - o.Attack) / Mathf.Max(0.0001f, o.Life - o.Attack));
            // 빛: (1 − u)² 로 빨리 잦아든다 / 틴트: 예전 곡선 (1 − OutCubic)
            return o.Peak * (o.Light ? (1f - u) * (1f - u) : 1f - JuiceEase.OutCubic(u));
        }

        void UpdateOverlay(ref Overlay o, Image image, float dt)
        {
            if (!o.Active) return;
            o.Age += dt;
            if (image == null || o.Age >= o.Life)
            {
                o.Active = false;
                o.Value = 0f;
                if (image != null) image.enabled = false;
                return;
            }
            o.Value = OverlayValue(in o);
            WriteOverlay(in o, image, o.Value);
        }

        void WriteOverlay(in Overlay o, Image image, float value)
        {
            if (o.Light && _screenMaterial != null) image.color = new Color(o.Color.r, o.Color.g, o.Color.b, value);
            // 스크린 셰이더가 없으면 알파 덮개로 약하게 (우유빛 막이 짙어지지 않게 0.6배)
            else image.color = BattleEase.WithAlpha(o.Color, o.Light ? value * 0.6f : value);
        }

        // ───────────── 전장 어둡게 (FieldDim) ─────────────

        struct DimEnvelope
        {
            public bool Active;
            public float From;
            public float Alpha;
            public float FadeIn;
            public float Hold;
            public float FadeOut;
            public float Age;
        }

        DimEnvelope _dimEnvelope;
        float _impactDimAge = -1f;
        bool _impactDimReduced;
        float _impactDimFade = 0.15f;
        float _dimNow;

        void StartDim(float alpha, float fadeIn, float hold, float fadeOut)
        {
            alpha = Mathf.Clamp(alpha, 0f, _reduceFlash ? 0.2f : 0.45f);
            if (!(alpha > 0.001f)) return;
            float current = DimEnvelopeValue(in _dimEnvelope);
            if (_dimEnvelope.Active && current > alpha) return;
            _dimEnvelope = new DimEnvelope
            {
                Active = true, From = current, Alpha = alpha, FadeIn = Mathf.Max(0.001f, fadeIn), Hold = Mathf.Max(0f, hold), FadeOut = Mathf.Max(0.001f, fadeOut),
                Age = 0f,
            };
        }

        static float DimEnvelopeValue(in DimEnvelope e)
        {
            if (!e.Active) return 0f;
            if (e.Age < e.FadeIn) return Mathf.Lerp(e.From, e.Alpha, JuiceEase.OutQuad(e.Age / e.FadeIn));
            float t = e.Age - e.FadeIn;
            if (t < e.Hold) return e.Alpha;
            t -= e.Hold;
            return t < e.FadeOut ? e.Alpha * (1f - JuiceEase.OutQuad(t / e.FadeOut)) : 0f;
        }

        /// <summary>임팩트 프레임 어둡게: 0.45 33ms → 0.2 17ms → 걷힘 (번쩍임 줄이기: 0.2 100ms만). 끝나면 음수</summary>
        float ImpactDimValue()
        {
            float age = _impactDimAge;
            if (_impactDimReduced) return age < 0.1f ? 0.2f : -1f;
            if (age < 0.033f) return 0.45f;
            if (age < 0.05f) return 0.2f;
            float u = (age - 0.05f) / Mathf.Max(0.01f, _impactDimFade);
            return u < 1f ? 0.2f * (1f - JuiceEase.OutQuad(u)) : -1f;
        }

        void UpdateDim(float dt)
        {
            if (_dimEnvelope.Active)
            {
                _dimEnvelope.Age += dt;
                if (_dimEnvelope.Age >= _dimEnvelope.FadeIn + _dimEnvelope.Hold + _dimEnvelope.FadeOut) _dimEnvelope.Active = false;
            }
            float impact = 0f;
            if (_impactDimAge >= 0f)
            {
                _impactDimAge += dt;
                impact = ImpactDimValue();
                if (impact < 0f)
                {
                    impact = 0f;
                    _impactDimAge = -1f;
                }
            }
            float alpha = Mathf.Max(DimEnvelopeValue(in _dimEnvelope), impact);
            if (_reduceFlash) alpha = Mathf.Min(alpha, 0.2f);
            _dimNow = alpha;
            if (_fieldDim == null) return;
            if (alpha > 0.002f)
            {
                if (!_fieldDim.enabled) _fieldDim.enabled = true;
                _fieldDim.color = BattleEase.WithAlpha(JuiceColors.Dim, alpha);
            }
            else if (_fieldDim.enabled) _fieldDim.enabled = false;
        }

        // ───────────── 실루엣 (임팩트 프레임 · 인쇄 어긋남) ─────────────

        const int SilhouettePink = 0;
        const int SilhouetteBlue = 1;
        const int SilhouetteCream = 2;
        const int SilhouetteCount = 4;

        struct Silhouette
        {
            public Image Image;
            public Graphic Body;
            public bool Active;
            public float Age;
            public float Life;
            public float FullUntil;
            public float Alpha;
            public float AfterAlpha;
            public Vector2 Offset;
            public Color Color;
        }

        Silhouette[] _silhouettes;

        /// <summary>실루엣 4장 (UIWhiteFlash 머티리얼, 처음엔 꺼짐). 머티리얼이 없으면 만들지 않는다 (실루엣 없이)</summary>
        void PrepareSilhouettes()
        {
            if (_flashMaterial == null || _layer == null) return;
            _silhouettes = new Silhouette[SilhouetteCount];
            for (int i = 0; i < SilhouetteCount; i++)
            {
                var image = Instantiate(_particleTemplate, _layer);
                image.name = "Silhouette";
                image.material = _flashMaterial;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.enabled = false;
                image.type = Image.Type.Simple;
                _silhouettes[i] = new Silhouette { Image = image };
            }
        }

        void StartSilhouette(int slot, Graphic body, Color color, Vector2 offset, float alpha, float fullUntil, float afterAlpha, float life)
        {
            if (_silhouettes == null || body == null || slot < 0 || slot >= SilhouetteCount) return;
            ref var s = ref _silhouettes[slot];
            s.Body = body;
            s.Active = true;
            s.Age = 0f;
            s.Life = Mathf.Max(0.017f, life);
            s.FullUntil = fullUntil;
            s.Alpha = alpha;
            s.AfterAlpha = afterAlpha;
            s.Offset = offset;
            s.Color = color;
            CopyBody(ref s);
            s.Image.color = new Color(color.r, color.g, color.b, alpha);
            s.Image.enabled = true;
            KeepSilhouettesOnTop();
        }

        void UpdateSilhouettes(float dt)
        {
            if (_silhouettes == null) return;
            bool any = false;
            for (int i = 0; i < SilhouetteCount; i++)
            {
                ref var s = ref _silhouettes[i];
                if (!s.Active) continue;
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    s.Active = false;
                    s.Body = null;
                    s.Image.enabled = false;
                    continue;
                }
                CopyBody(ref s);
                float alpha = s.Age < s.FullUntil ? s.Alpha : s.AfterAlpha;
                s.Image.color = new Color(s.Color.r, s.Color.g, s.Color.b, alpha);
                any = true;
            }
            if (any) KeepSilhouettesOnTop();
        }

        /// <summary>켜진 실루엣을 맨 위로 (분홍 → 하늘 → 크림 순서라 크림이 가장 위)</summary>
        void KeepSilhouettesOnTop()
        {
            if (_silhouettes == null) return;
            for (int i = 0; i < SilhouetteCount; i++)
            {
                if (_silhouettes[i].Active) _silhouettes[i].Image.rectTransform.SetAsLastSibling();
            }
        }

        /// <summary>몸 그림의 자리·회전·기준점·크기·배율·그림을 그대로 베낀다 (몸이 사라졌으면 마지막 모습 그대로)</summary>
        void CopyBody(ref Silhouette s)
        {
            var body = s.Body;
            if (body == null || !body.isActiveAndEnabled) return;
            var source = body.rectTransform;
            var target = s.Image.rectTransform;
            if (body is Image image)
            {
                if (s.Image.sprite != image.sprite) s.Image.sprite = image.sprite;
                s.Image.preserveAspect = image.preserveAspect;
            }
            else if (s.Image.sprite != _soft) s.Image.sprite = _soft;
            target.pivot = source.pivot;
            target.sizeDelta = source.rect.size;
            target.anchoredPosition = (Vector2)_layer.InverseTransformPoint(source.position) + s.Offset;
            target.rotation = source.rotation;
            var lossy = source.lossyScale;
            var layer = _layer.lossyScale;
            target.localScale = new Vector3(Mathf.Abs(layer.x) > 1e-6f ? lossy.x / layer.x : 1f, Mathf.Abs(layer.y) > 1e-6f ? lossy.y / layer.y : 1f, 1f);
        }

        /// <summary>번쩍임·어둡게·실루엣을 모두 끈다 (ClearParticles)</summary>
        void ClearScreenEffects()
        {
            _fieldOverlay = default;
            _screenOverlay = default;
            if (_fieldFlash != null) _fieldFlash.enabled = false;
            if (_screenFlash != null) _screenFlash.enabled = false;
            _dimEnvelope = default;
            _impactDimAge = -1f;
            _dimNow = 0f;
            if (_fieldDim != null) _fieldDim.enabled = false;
            if (_silhouettes == null) return;
            for (int i = 0; i < SilhouetteCount; i++)
            {
                _silhouettes[i].Active = false;
                _silhouettes[i].Body = null;
                if (_silhouettes[i].Image != null) _silhouettes[i].Image.enabled = false;
            }
        }

        /// <summary>이번 프레임 상태를 담는다 (할당 없음). 예산 탈락·버린 큐는 지난 표본 뒤의 수 (Advance 밖에서 만든 입자 몫도 다음 표본에 든다)</summary>
        void SampleStats()
        {
            // 흔들림 = trauma 잡음 + 킥만 (줌 펀치가 중심을 제자리에 두려고 옮긴 양은 ZoomShift — 흔들림이 아니다)
            _stats.ShakeOffset = _shakeRoot != null ? _shakeNow : 0f;
            _stats.ZoomShift = _shakeRoot != null ? _zoomShiftNow : 0f;
            _stats.Zoom = _shakeRoot != null ? _shakeRoot.localScale.x : 1f;
            _stats.RejectedUpper = _rejectedSparkle + _rejectedHero;
            _stats.RejectedHero = _rejectedHero;
            _stats.RejectedGround = _rejectedGround;
            _stats.LastRejectedHero = _lastRejectedHero;
            _rejectedSparkle = 0;
            _rejectedHero = 0;
            _rejectedGround = 0;
            _lastRejectedHero = null;
            int dropped = _cues.Dropped;
            _stats.CuesDropped = Mathf.Max(0, dropped - _cueDroppedSeen);
            _cueDroppedSeen = dropped;
            _stats.Dim = _dimNow;
            _stats.Light = Mathf.Max(_fieldOverlay.Active && _fieldOverlay.Light ? _fieldOverlay.Value : 0f,
                _screenOverlay.Active && _screenOverlay.Light ? _screenOverlay.Value : 0f);
            _stats.Tint = Mathf.Max(_fieldOverlay.Active && !_fieldOverlay.Light ? _fieldOverlay.Value : 0f,
                _screenOverlay.Active && !_screenOverlay.Light ? _screenOverlay.Value : 0f);
            _stats.LiveUpper = UsedParticles;
            _stats.LiveGround = UsedGround;
            int numbers = 0;
            if (_numbers != null)
            {
                for (int i = 0; i < _numbers.Length; i++)
                {
                    if (_numbers[i].Alive && _numbers[i].Delay <= 0f) numbers++;
                }
            }
            _stats.VisibleNumbers = numbers;
            int silhouettes = 0;
            if (_silhouettes != null)
            {
                for (int i = 0; i < SilhouetteCount; i++)
                {
                    if (_silhouettes[i].Active) silhouettes++;
                }
            }
            _stats.Silhouettes = silhouettes;
        }
    }
}
