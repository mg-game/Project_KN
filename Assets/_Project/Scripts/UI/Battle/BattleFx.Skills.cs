using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>스킬 연출 한 번에 필요한 값 (모두 전장 로컬 좌표, 픽셀)</summary>
    public struct SkillFxRequest
    {
        public string Key;
        public SkillLook Look;
        /// <summary>연출 그림 (아트 또는 흰색 대체 모양)</summary>
        public Sprite Sprite;
        /// <summary>true면 색 있는 아트 → 흰색으로 그린다</summary>
        public bool IsArt;
        /// <summary>시전자 위치 (가슴 높이)</summary>
        public Vector2 From;
        /// <summary>주 대상 위치 (적 몸 가운데 또는 효과 중심 위)</summary>
        public Vector2 To;
        /// <summary>효과 중심 (바닥)</summary>
        public Vector2 Center;
        /// <summary>효과 반경 (가로, 세로 px — 세로는 전장 눌림 반영)</summary>
        public Vector2 Radius;
        /// <summary>빛줄기 끝 (Beam)</summary>
        public Vector2 BeamEnd;
        /// <summary>주 대상 적 (투사체가 따라가고 머리 위 표식이 붙는다)</summary>
        public EnemyView Target;
        /// <summary>대상 잔향이 (돔/머리 위 표식이 따라다닌다)</summary>
        public EchoView AnchorEcho;
        /// <summary>1 = 보통, 클수록 크고 화려하게</summary>
        public float Power;
        /// <summary>레인 관통 투사체: 휘거나 따라가지 않고 BeamEnd까지 일정한 빠르기로 곧게 날아간다</summary>
        public bool Straight;
    }

    /// <summary>
    /// 스킬 / 상태 / 기믹 연출 v2 (FX-JUICE E7 · E15 · E19). 키마다 정해진 방식(SkillVfxStyle)으로 아트(또는 흰색 대체 모양에 파스텔 색)를 쓰고,
    /// 빛(가산: 코어 별·빛 고리·빛줄기·광택)과 사물 모양 입자(하트·음표·단추·빗방울…)를 곁들인다. 아군 스킬은 밝은 파스텔 빛, 적 기믹은 알파로 차분하게.
    ///   시전: 기 모으기 120ms(별빛이 InQuad로 빨려 든다) → 방식별 연출. 폭발은 첫 프레임 범위 빛 + 그림 1.35배 쾅 + 바닥 고리, 투사체는 머리 아래 빛,
    ///   광선은 가산 심지, 고리는 안쪽 빛 테두리, 장판은 0.5초마다 맥박, 돔·떠오름·머리 위는 1.3배 톡 + 반짝(돔은 광택 훑기).
    ///   명중: 코어 SkillHit(기본 명중) 뒤 OnSkillHit이 방식별 결 — 조각 튐 + 킥 / 수직 빛줄기 + 킥 / 모양 톡 + 범위 흔들림 / 빗방울 첨벙.
    ///   범위 스킬은 적마다 닿는 순간이 다르다 (SkillReachDelay: 폭발 100px마다 25ms · 고리 반지름 · 회전 그림이 지나는 순간 · 빗방울).
    /// </summary>
    public partial class BattleFx
    {
        static readonly Color HealPink = new(1f, 0.7f, 0.79f, 1f);
        static readonly Color CritGold = new(1f, 0.84f, 0.42f, 1f);
        static readonly Color ShieldBlue = new(0.75f, 0.89f, 1f, 1f);
        static readonly Color EnrageRed = new(1f, 0.42f, 0.52f, 1f);
        static readonly Color ShadowPurple = new(0.3f, 0.22f, 0.48f, 1f);

        /// <summary>시전 기 모으기 (별빛이 빨려 드는 시간)</summary>
        const float CastChargeTime = 0.12f;
        /// <summary>퍼지는 고리: 지름 0.15R → 2.2R, 0.62초 OutCubic (SkillRing · SkillReachDelay가 같은 값)</summary>
        const float RingFrom = 0.15f;
        const float RingTo = 2.2f;
        const float RingLife = 0.62f;
        /// <summary>회전: 그림 셋이 120° 간격으로 8.5 rad/s (반경 × 0.85)</summary>
        const int OrbitCopies = 3;
        const float OrbitSpeed = 8.5f;
        const float OrbitRadiusScale = 0.85f;
        /// <summary>폭발 물결: 가운데에서 100px마다 25ms (최대 0.15초) / 범위 스킬이 닿는 가장 늦은 순간</summary>
        const float BurstStagger = 0.025f;
        const float BurstStaggerMax = 0.15f;
        const float SkillReachMax = 0.5f;
        /// <summary>
        /// 폭발 그림: 반지름 × 1.8 (130~340px, × 세기) · 0.5초 · 40%부터 스민다 · 끝까지 1.04배로 부푼다 — 크게 쾅 하고 바로 걷히는 터짐
        /// (예전 × 1.5 · 110~300px · 0.72초 · 55% · 1.08배는 작은 스티커가 오래 남는 것처럼 읽혔다: 별 폭발 최대 6.1%, +600ms에 그 56%가 남았다)
        /// </summary>
        const float BurstArtScale = 1.8f;
        const float BurstArtMin = 130f;
        const float BurstArtMax = 340f;
        const float BurstArtLife = 0.5f;
        const float BurstArtFade = 0.4f;
        const float BurstArtSwell = 1.04f;
        /// <summary>비: 그 적 머리 위 빗방울이 닿는 순간 0.40 + 0.10 × 거리 비율 / 떨어지는 시간</summary>
        const float RainReachBase = 0.4f;
        const float RainReachSpread = 0.1f;
        const float RainDropFall = 0.32f;
        /// <summary>보호막 깨짐 "깨짐!" (34pt 소프트 블루, 30px 떠오르며 0.6초)</summary>
        const string ShieldBreakText = "깨짐!";

        /// <summary>범위 스킬 흔들림 예약 (연출 시계, 없으면 음수): 시전의 첫 명중 순간에 스킬 채널 0.4 (초당 0.5까지)</summary>
        float _skillTraumaAt = -1f;
        Vector2 _skillTraumaDir;
        /// <summary>예약한 시전(그 프레임의 연출 시계) / 이미 흔든 시전 — 한 시전의 명중 20개가 한 번만 흔든다</summary>
        float _skillTraumaCast = -1f;
        float _skillTraumaDone = -1f;

        /// <summary>눈에 보이는 연기·먼지 퍽 (뭉게구름 그림, 없으면 부드러운 원)</summary>
        Sprite Puff => _puff != null ? _puff : _soft;

        /// <summary>흰색 모양 스프라이트 (없으면 기본 빛 입자로 대체)</summary>
        public Sprite Motif(VfxMotif motif)
        {
            var sprite = _library != null ? _library.Motif(motif) : null;
            if (sprite != null) return sprite;
            return motif switch
            {
                VfxMotif.Glow => _glow,
                VfxMotif.Soft => Puff,
                VfxMotif.Ring or VfxMotif.MagicCircle or VfxMotif.Bubble => _ring,
                VfxMotif.Trail => _trail,
                VfxMotif.Shard => _sparkle,
                _ => _sparkle,
            };
        }

        // ───────────── 스킬 명중 결 (E7 — 코어 SkillHit 뒤에 방식별로) ─────────────

        /// <summary>
        /// 스킬 방식별 명중 결 (코어 SkillHit이 기본 명중을 그린 뒤): 투사체 = 조각 셋이 앞쪽 위로 튀어 바닥에 한 번 튀고 킥 2px ·
        /// 광선 = 광선에 수직인 빛줄기 둘 + 킥 2px · 폭발/고리/회전 = 그 스킬 모양 하나가 1.35배로 톡 (폭발·고리는 첫 명중에 범위 흔들림 0.4) ·
        /// 비 = 그 적 머리 위로 빗방울 하나가 떨어져 명중 순간에 첨벙 (범위 흔들림) · 장판 틱 = 코어 불씨만
        /// </summary>
        partial void OnSkillHit(SkillHitFx r)
        {
            if (r.Tick) return;
            var target = r.Target;
            var at = r.Point;
            float unit = UnitScale;
            var dir = r.Direction.sqrMagnitude > 1e-4f ? r.Direction.normalized : Vector2.up;
            switch (r.Style)
            {
                case SkillVfxStyle.Projectile:
                    SkillShards(at, dir, r.Shard, r.Main, r.Accent, 3, r.Delay, target, unit * (r.Boss ? 1.2f : 1f));
                    Kick(dir, HitFeel.KickSkill, KickChannel.Skill, r.Delay);
                    break;
                case SkillVfxStyle.Beam:
                {
                    var side = new Vector2(-dir.y, dir.x);
                    var tint = BattleEase.WithAlpha(JuiceColors.TowardCream(r.Main, 0.5f), 0.9f);
                    float life = 0.16f * PopScale;
                    for (int i = 0; i < 2; i++)
                    {
                        var velocity = (i == 0 ? side : -side) * (Random.Range(640f, 820f) * unit);
                        int streak = Streak(at, velocity, 26f * unit, 5f * unit, life, tint, r.Delay, 1f, 9f);
                        AnchorUntilShown(streak, target, r.Delay);
                    }
                    Kick(dir, HitFeel.KickSkill, KickChannel.Skill, r.Delay);
                    break;
                }
                case SkillVfxStyle.Burst:
                case SkillVfxStyle.Ring:
                    SkillMotifPop(at, r, target, unit);
                    ScheduleSkillTrauma(r.Delay, dir);
                    break;
                case SkillVfxStyle.Orbit:
                    SkillMotifPop(at, r, target, unit);
                    break;
                case SkillVfxStyle.Rain:
                    SkillRainDrop(at, r, target, unit);
                    ScheduleSkillTrauma(r.Delay, Vector2.down);
                    break;
            }
        }

        /// <summary>스킬 조각 count개 (모양 motif, 15~22px × U): 맞은 자리에서 시전 방향 앞쪽 위로 튀어 바닥에 한 번 튄다 (중력 900, 가산 0.3)</summary>
        void SkillShards(Vector2 at, Vector2 dir, VfxMotif motif, Color main, Color accent, int count, float delay, EnemyView target, float unit)
        {
            var sprite = Motif(motif == VfxMotif.Trail || motif == VfxMotif.Soft || motif == VfxMotif.Glow ? VfxMotif.Shard : motif);
            float floor = at.y - 46f * unit;
            for (int i = 0; i < count; i++)
            {
                if (i >= 1 && !Allow(FxPriority.Sparkle)) break;
                float size = Random.Range(15f, 22f) * unit;
                var velocity = new Vector2(dir.x * 0.55f + Random.Range(-0.6f, 0.6f), Random.Range(0.75f, 1.15f)).normalized * (Random.Range(220f, 340f) * unit);
                int index = Spawn(sprite, i % 2 == 0 ? main : accent, at, size, size * 0.8f, Random.Range(0.45f, 0.6f), delay, sparkle: i >= 1);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Motion = Motion.Bounce;
                p.P1 = velocity;
                p.P2 = new Vector2(floor, 900f);
                p.P3 = new Vector2(0.4f, 0f);
                p.Spin = Random.Range(-540f, 540f);
                p.Add = 0.3f;
                p.FadeIn = 0.02f;
                p.FadeOut = 0.6f;
                AnchorUntilShown(index, target, delay);
            }
        }

        /// <summary>범위 스킬이 적 하나에 닿은 표시: 그 스킬 모양 하나가 맞은 자리 위에서 1.35배로 톡 → 34px, 위로 24px 떠오르며 0.36초 (가산 0.3)</summary>
        void SkillMotifPop(Vector2 at, in SkillHitFx r, EnemyView target, float unit)
        {
            if (!Allow(FxPriority.Extra)) return;
            var motif = r.Shard == VfxMotif.Trail || r.Shard == VfxMotif.Soft || r.Shard == VfxMotif.Glow ? VfxMotif.Sparkle : r.Shard;
            int pop = ImpactArt(Motif(motif), Color.Lerp(r.Main, UITheme.Cream, 0.3f), at + Vector2.up * (10f * unit), 34f * unit, HitFeel.Feel(HitTier.Skill),
                0.36f * PopScale, r.Delay, 0.9f, 0.3f);
            if (pop < 0) return;
            ref var p = ref _particles[pop];
            p.P1 = Vector2.up * (24f * unit);
            p.Spin = Random.Range(-90f, 90f);
            p.Rotation = Random.Range(-15f, 15f);
            AnchorUntilShown(pop, target, r.Delay);
        }

        /// <summary>비: 그 적 머리 위에서 빗방울 하나가 떨어져 명중 순간(delay)에 바닥에 닿아 첨벙 (Arrival.Splash — 작은 고리 가산 0.5 + 물방울)</summary>
        void SkillRainDrop(Vector2 at, in SkillHitFx r, EnemyView target, float unit)
        {
            if (r.Delay < 0.1f || !Allow(FxPriority.Sparkle)) return;
            float fall = Mathf.Min(RainDropFall, r.Delay);
            var land = at + Vector2.down * (10f * unit);
            var start = new Vector2(land.x + Random.Range(-8f, 8f), Mathf.Min(land.y + 300f * unit, FieldTop - 20f));
            int drop = Spawn(Motif(VfxMotif.Drop), Color.Lerp(r.Main, Color.white, 0.35f), start, 22f * unit, 18f * unit, fall, r.Delay - fall, sparkle: true);
            if (drop < 0) return;
            ref var p = ref _particles[drop];
            // Drift: P0 + P2 × k² — 빗방울처럼 점점 빨라진다
            p.P2 = land - start;
            p.Aspect = 1.35f;
            p.Rotation = 0f;
            p.FadeIn = 0.1f;
            p.FadeOut = 0.92f;
            p.Add = 0.2f;
            p.Arrival = Arrival.Splash;
            p.Color2 = r.Accent;
            AnchorUntilShown(drop, target, r.Delay - fall);
        }

        /// <summary>비 스킬의 첫 빗방울이 적에 닿는 순간 (BattleView가 범위 흔들림을 이때로 예약한다)</summary>
        public const float RainFirstReach = RainReachBase;

        /// <summary>
        /// 범위 스킬 흔들림 (스킬 채널 0.4, 초당 0.5까지)을 delay 뒤 첫 명중 순간으로 예약한다. 같은 시전(같은 프레임)의 명중들이 다시 불러도 한 번만 흔든다
        /// </summary>
        public void AreaSkillShake(float delay, Vector2 direction = default)
        {
            if (!_ready) Prepare();
            ScheduleSkillTrauma(delay, direction);
        }

        /// <summary>범위 스킬 흔들림을 그 시전의 첫 명중 순간으로 예약 (같은 시전의 다른 명중은 더 이른 순간만 당긴다)</summary>
        void ScheduleSkillTrauma(float delay, Vector2 direction)
        {
            if (_clock == _skillTraumaDone) return;
            float at = _clock + Mathf.Max(0f, delay);
            if (_skillTraumaAt >= 0f && _skillTraumaAt <= at) return;
            _skillTraumaAt = at;
            _skillTraumaDir = direction;
            _skillTraumaCast = _clock;
        }

        /// <summary>예약된 범위 스킬 흔들림을 그 순간에 (UpdateNumbers가 매 프레임 부른다 — 연출 시계)</summary>
        void TickSkillShow()
        {
            if (_skillTraumaAt < 0f || _clock < _skillTraumaAt) return;
            _skillTraumaAt = -1f;
            _skillTraumaDone = _skillTraumaCast;
            AddSkillTrauma(HitFeel.AreaSkillTrauma, _skillTraumaDir);
        }

        void ClearSkillShow()
        {
            _skillTraumaAt = -1f;
            _skillTraumaCast = -1f;
            _skillTraumaDone = -1f;
        }

        /// <summary>
        /// 범위 스킬이 그 적(발 위치 feet, 전장 로컬)에 닿는 순간 (E7 시간표 — BattleView가 결과 연출·숫자·반응을 이 시간에 맞춘다):
        /// 폭발 = 가운데에서 100px마다 25ms (최대 0.15초) · 퍼지는 고리 = 첫 고리 반지름이 닿는 순간 · 회전 = 도는 그림이 그 방향을 지나는 순간 ·
        /// 비 = 0.40 + 0.10 × 거리 비율 (그 적 머리 위 빗방울이 떨어져 닿는다). 그 밖의 방식은 0. 최대 0.5초
        /// </summary>
        public float SkillReachDelay(SkillVfxStyle style, Vector2 center, Vector2 radius, Vector2 feet)
        {
            if (!_ready) Prepare();
            var offset = feet - center;
            switch (style)
            {
                case SkillVfxStyle.Burst:
                    return Mathf.Min(BurstStaggerMax, BurstStagger * offset.magnitude / 100f);
                case SkillVfxStyle.Ring:
                {
                    var r = ClampRadius(radius);
                    float reach = new Vector2(offset.x, offset.y / Mathf.Max(0.1f, GroundAspect)).magnitude;
                    return Mathf.Min(SkillReachMax, RingReachTime(reach, r.x));
                }
                case SkillVfxStyle.Orbit:
                {
                    var r = ClampRadius(radius) * OrbitRadiusScale;
                    return OrbitPassTime(Mathf.Atan2(offset.y / Mathf.Max(1f, r.y), offset.x / Mathf.Max(1f, r.x)));
                }
                case SkillVfxStyle.Rain:
                {
                    var r = ClampRadius(radius);
                    float reach = new Vector2(offset.x, offset.y / Mathf.Max(0.1f, GroundAspect)).magnitude;
                    return RainReachBase + RainReachSpread * Mathf.Clamp01(reach / Mathf.Max(1f, r.x));
                }
            }
            return 0f;
        }

        /// <summary>퍼지는 고리(지름 0.15R → 2.2R, OutCubic 0.62초)의 반지름이 reach px에 닿는 시간 (밖이면 0.62)</summary>
        public static float RingReachTime(float reach, float radius)
        {
            if (radius <= 1f) return 0f;
            float y = Mathf.Clamp01((2f * reach / radius - RingFrom) / (RingTo - RingFrom));
            return RingLife * (1f - Mathf.Pow(1f - y, 1f / 3f));
        }

        /// <summary>회전 그림 셋(120° 간격에서 출발, 8.5 rad/s)이 각 angle(rad)을 처음 지나는 시간 (0 ~ 0.25초)</summary>
        public static float OrbitPassTime(float angle)
        {
            float best = float.MaxValue;
            for (int i = 0; i < OrbitCopies; i++)
            {
                float gap = Mathf.Repeat(angle - i * Mathf.PI * 2f / OrbitCopies, Mathf.PI * 2f);
                best = Mathf.Min(best, gap / OrbitSpeed);
            }
            return best;
        }

        // ───────────── 스킬 시전 ─────────────

        /// <summary>
        /// 스킬 시전 연출 (키별 방식). 주 대상(곧게 나는 투사체는 레인 끝)에 닿기까지 걸리는 시간(초)을 돌려준다:
        /// 투사체는 비행 시간, 광선은 뻗는 시간, 범위 스킬(폭발·고리·회전·비)은 주 대상(없으면 가운데)에 닿는 순간 (SkillReachDelay — 비는 첫 빗방울 0.4초),
        /// 나머지는 0 (그 자리에서 바로). 뷰는 이 시간에 맞춰 피해 숫자·상태 표시·조명을 늦춘다.
        /// </summary>
        public float SkillCast(in SkillFxRequest r)
        {
            if (!_ready) Prepare();
            var style = r.Look.Style;
            switch (style)
            {
                case SkillVfxStyle.Projectile: return SkillProjectile(r);
                case SkillVfxStyle.Beam: return SkillBeam(r);
                case SkillVfxStyle.Ring: SkillRing(r); break;
                case SkillVfxStyle.Ground: SkillGround(r); break;
                case SkillVfxStyle.Dome: SkillDome(r); break;
                case SkillVfxStyle.Rise: SkillRise(r); break;
                case SkillVfxStyle.OverHead: SkillOverHead(r); break;
                case SkillVfxStyle.Orbit: SkillOrbit(r); break;
                case SkillVfxStyle.Rain: SkillRain(r); break;
                default: SkillBurst(r); break;
            }
            bool staggered = style == SkillVfxStyle.Burst || style == SkillVfxStyle.Ring || style == SkillVfxStyle.Orbit || style == SkillVfxStyle.Rain;
            return staggered ? SkillReachDelay(style, r.Center, r.Radius, r.Target != null ? r.Target.Rect.anchoredPosition : r.Center) : 0f;
        }

        /// <summary>광선이 끝까지 뻗는 데 걸리는 시간 (Beam의 Snap 곡선: 수명의 앞 30%)</summary>
        public const float BeamReach = 0.16f;

        /// <summary>곧게 나는 레인 투사체의 비행 시간 (거리에 비례, 초당 약 1600px)</summary>
        public static float StraightFlight(Vector2 from, Vector2 to) => Mathf.Clamp((to - from).magnitude / 1600f, 0.2f, 0.5f);

        /// <summary>
        /// 시전자의 기 모으기 v2 (E7): 가슴 빛(fx_glow_core 가산, 0.6)과 발밑 빛 고리(가산 0.7)가 차오르고, 별빛 7개가 120ms 동안 InQuad로
        /// 가슴에 빨려 든 뒤(가산 0.8) 다 모인 순간 작은 코어 별이 톡 — 투사체가 날아가는 동안 겹치므로 시뮬레이션 시간은 그대로
        /// </summary>
        public void CastCharge(Vector2 chest, Vector2 feet, Color color, float power = 1f)
        {
            if (!_ready) Prepare();
            int glow = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(JuiceColors.TowardCream(color, 0.35f), 0.6f), chest, 40f * power, 150f * power, 0.32f, 0f, 1f);
            if (glow >= 0)
            {
                _particles[glow].FadeOut = 0.35f;
                _particles[glow].FadeKind = FadeInQuad;
            }
            Shockwave(feet, color, 30f * power, 130f * power, 0.4f, 0f, 0.7f, 0.8f, GroundAspect, true);
            const int sparks = 7;
            for (int i = 0; i < sparks; i++)
            {
                float angle = i * Mathf.PI * 2f / sparks + Random.Range(-0.2f, 0.2f);
                var start = chest + new Vector2(Mathf.Cos(angle) * 74f, Mathf.Sin(angle) * 54f) * power;
                // Drift: P0 + P2 × k² = InQuad로 빨려 든다
                int index = Drift(_sparkle, i % 2 == 0 ? color : UITheme.Cream, start, Vector2.zero, chest - start, 20f, 7f, CastChargeTime, i * 0.006f);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Spin = 360f;
                p.Add = 0.8f;
                p.FadeIn = 0.05f;
                p.FadeOut = 0.85f;
            }
            CoreStar(chest, JuiceColors.TowardCream(color, 0.6f), 48f * power, 30f * power, 0.07f, CastChargeTime, 0.8f);
        }

        /// <summary>
        /// 투사체(바늘·열쇠): 시전자 → 대상으로 날아가 같은 그림이 1.35배로 쾅 터진다 (SkillImpactAt). 머리 아래에는 빛(가산 0.45)이 따라가고
        /// 꼬리는 빛(가산 0.6). 아트는 오른쪽을 향해 그려져 있으므로 진행 방향으로 기운다.
        /// 레인 스킬(Straight)은 대상을 따라가지 않고 레인 끝까지 곧게 꿰뚫고 지나가며, 지나간 자리에 실 땀이 찍힌다. 비행 시간을 돌려준다.
        /// </summary>
        float SkillProjectile(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var tint = Tint(r);
            var sprite = MainSprite(r);
            bool thread = r.Key == SkillVfx.ThreadStitch;
            bool art = r.IsArt && r.Sprite != null;
            bool straight = r.Straight && r.BeamEnd != Vector2.zero;
            var to = straight ? r.BeamEnd : r.To;
            bool homing = !straight && r.Target != null && r.Target.IsTargetable;
            float arc = straight ? 0f : thread ? 90f : 34f;
            float life = straight ? StraightFlight(r.From, to) : thread ? 0.38f : 0.26f;
            // 아트는 가로로 긴 그림(바늘+실, 열쇠+빛 꼬리)이라 조금 크게
            float size = (art ? 96f : 62f) * power;
            // 투사체 포물선(꼭대기 = arc)과 같은 높이로 지나가도록 2차 곡선 제어점은 arc의 두 배
            var control = (r.From + to) * 0.5f + Vector2.up * arc * 2f;

            // 시전 순간: 손끝 빛 (가산) + 작은 코어 별
            SkillLightGlow(r.From, Color.Lerp(look.Main, Color.white, 0.4f), 34f, 110f * power, 0.22f, 0f, 0.7f);
            CoreStar(r.From, JuiceColors.TowardCream(look.Main, 0.6f), 54f * power, 36f * power, 0.06f, 0f, 0.8f);

            // 그리는 순서(아래 → 위): 빛 꼬리 → 머리 아래 빛 → 머리
            var trailTint = BattleEase.WithAlpha(look.Main, 0.85f);
            int tail;
            if (straight) tail = Line(_trail, trailTint, r.From, to, 140f * power, 110f * power, life, 0.02f);
            else if (homing) tail = Homing(_trail, trailTint, r.From, r.Target, arc, life, 110f * power, 0.3f);
            else tail = Curve(_trail, trailTint, r.From, control, to, 110f * power, 70f, life, 0f);
            if (tail >= 0)
            {
                ref var t = ref _particles[tail];
                t.AlignToPath = true;
                t.Aspect = 0.3f;
                t.Delay = 0.02f;
                t.Image.enabled = false;
                t.Add = 0.6f;
            }

            var underTint = BattleEase.WithAlpha(JuiceColors.TowardCream(look.Main, 0.3f), 0.45f);
            float underSize = size * 0.9f;
            int under;
            if (straight) under = Line(GlowCoreSprite, underTint, r.From, to, underSize, underSize, life, 0f);
            else if (homing) under = Homing(GlowCoreSprite, underTint, r.From, r.Target, arc, life, underSize, 1f);
            else under = Curve(GlowCoreSprite, underTint, r.From, control, to, underSize, underSize * 0.85f, life, 0f);
            if (under >= 0)
            {
                ref var g = ref _particles[under];
                g.Add = 1f;
                g.FadeIn = 0.02f;
                g.Spin = 0f;
                if (straight) g.FadeOut = 0.86f;
            }

            int head;
            if (straight) head = Line(sprite, tint, r.From, to, size, size, life, 0f);
            else if (homing) head = Homing(sprite, tint, r.From, r.Target, arc, life, size, 1f);
            else head = Curve(sprite, tint, r.From, control, to, size, size * 0.85f, life, 0f);
            if (head >= 0)
            {
                ref var p = ref _particles[head];
                // 레인 끝은 빈 땅이라 크게 터뜨리지 않고 스르르 스민다
                p.Arrival = straight ? Arrival.None : Arrival.SkillImpact;
                p.Color2 = look.Main;
                p.FadeIn = 0.02f;
                if (straight) p.FadeOut = 0.86f;
                p.AlignToPath = art;
                p.Spin = art || thread ? 0f : 220f;
                if (!art && thread) p.Rotation = -20f;
            }
            if (art) FaceTo(head, r.From, straight ? to : control);

            if (thread)
            {
                // 바늘땀: 지나간 자리에 실 땀이 차례로 찍혔다가 스민다 (레인이면 길게 한 줄로)
                int stitches = straight ? 12 : 8;
                for (int i = 1; i <= stitches; i++)
                {
                    float t = i / (stitches + 1f);
                    var a = BattleEase.Bezier(r.From, control, to, t);
                    var b = BattleEase.Bezier(r.From, control, to, t + 0.02f);
                    int dash = Spawn(_soft, i % 2 == 0 ? look.Main : look.Accent, a, 16f, 12f, 0.75f, t * life, sparkle: true);
                    if (dash < 0) continue;
                    ref var d = ref _particles[dash];
                    d.Aspect = 0.32f;
                    d.Rotation = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                    d.FadeIn = 0.05f;
                    d.FadeOut = 0.45f;
                }
            }
            else
            {
                // 열쇠 빛 화살: 가는 빛 부스러기가 뒤로 흩어진다 (가산)
                for (int i = 0; i < 4; i++)
                {
                    var t = 0.2f + i * 0.2f;
                    var at = Vector2.Lerp(r.From, to, t) + Vector2.up * (arc * Mathf.Sin(t * Mathf.PI));
                    int mote = Drift(_sparkle, i % 2 == 0 ? look.Accent : UITheme.Cream, at, RandomDirection() * 18f, Vector2.down * 16f, 16f, 4f, 0.4f, t * life);
                    if (mote >= 0) _particles[mote].Add = 0.6f;
                }
            }
            if (straight)
            {
                // 레인 끝에서 실이 팽팽히 당겨졌다가 풀리는 반짝임
                SkillLightGlow(to, look.Main, 30f, 120f * power, 0.35f, life * 0.9f, 0.6f);
                SkillLightSparkles(to, 36f, look.Accent, 4, life * 0.9f);
            }
            return life;
        }

        /// <summary>
        /// 광선: 바깥 빛(가산 0.6) · 강조색 띠(가산 0.6) · 가산 심지(가산 1, 0.9)가 0.16초에 뻗고, 시작에 코어 별, 광선을 따라 그림이 1.35배로 톡톡,
        /// 끝에 코어 별 0.6× + 빛 고리 + 바닥 고리
        /// </summary>
        float SkillBeam(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var from = r.From;
            var end = r.BeamEnd == Vector2.zero ? r.To : r.BeamEnd;
            var delta = end - from;
            float length = delta.magnitude;
            if (length < 10f)
            {
                SkillBurst(r);
                return 0f;
            }
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            int outer = Beam(from, angle, length, 96f * power, 50f * power, BattleEase.WithAlpha(look.Main, 0.5f), GlowCoreSprite, 0.62f, 0f);
            if (outer >= 0) _particles[outer].Add = 0.6f;
            int band = Beam(from, angle, length, 60f * power, 30f * power, BattleEase.WithAlpha(look.Accent, 0.8f), _trail, 0.42f, 0.03f);
            if (band >= 0) _particles[band].Add = 0.6f;
            int core = Beam(from, angle, length, 34f * power, 12f * power, BattleEase.WithAlpha(Color.Lerp(look.Main, Color.white, 0.6f), 0.9f), _soft, 0.5f, 0.02f);
            if (core >= 0) _particles[core].Add = 1f;

            SkillLightGlow(from, Color.Lerp(look.Main, Color.white, 0.5f), 50f, 170f * power, 0.35f, 0f, 0.8f);
            CoreStar(from, JuiceColors.TowardCream(look.Main, 0.6f), 70f * power, 46f * power, 0.07f, 0f, 0.9f);

            // 빛줄기를 따라 사물 그림이 첫 프레임부터 크게 톡톡 (Impact 1.35)
            var sprite = MainSprite(r);
            var tint = Tint(r);
            var feel = HitFeel.Feel(HitTier.Skill);
            for (int i = 1; i <= 3; i++)
            {
                var at = Vector2.Lerp(from, end, i / 3f);
                int pop = ImpactArt(sprite, tint, at, 78f * power, feel, 0.6f, 0.04f + i * 0.06f, 1f);
                if (pop < 0) continue;
                _particles[pop].Rotation = Random.Range(-10f, 10f);
                _particles[pop].FadeOut = 0.5f;
            }
            for (int i = 0; i < 7; i++)
            {
                var control = Vector2.Lerp(from, end, 0.5f) + Vector2.up * Random.Range(-30f, 30f);
                int index = Curve(_sparkle, i % 2 == 0 ? UITheme.Cream : look.Main, from, control, end, 22f, 8f, 0.32f, i * 0.035f);
                if (index < 0) continue;
                _particles[index].Spin = 400f;
                _particles[index].Add = 0.7f;
            }
            float endSize = 170f * UnitScale * power;
            SkillLightGlow(end, look.Main, 40f, 180f * power, 0.4f, 0.1f, 0.6f);
            CoreStar(end, JuiceColors.TowardCream(look.Main, 0.6f), endSize * 0.6f, endSize * 0.4f, 0.066f, 0.1f, 0.9f);
            Shockwave(end, look.Main, endSize * 0.4f, endSize * 1.2f, 0.2f, 0.1f, 0.8f, 0.7f);
            int ring = Ring(end, look.Accent, 20f, 160f * power, 0.45f, GroundAspect, 0.1f);
            if (ring >= 0) _particles[ring].Add = 0.5f;
            return BeamReach;
        }

        /// <summary>from에서 angle 방향으로 뻗는 빛줄기 (두께는 thickness0 → thickness1). 입자 칸 번호 (못 만들면 −1)</summary>
        int Beam(Vector2 from, float angle, float length, float thickness0, float thickness1, Color color, Sprite sprite, float life, float delay)
        {
            int index = Spawn(sprite, color, from, length * 0.12f, length, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Snap;
            p.Height0 = thickness0;
            p.Height1 = thickness1;
            p.Rotation = angle;
            p.FadeIn = 0.02f;
            p.FadeOut = 0.4f;
            p.Rect.pivot = new Vector2(0f, 0.5f);
            return index;
        }

        /// <summary>
        /// 폭발 v2 (촛불·입맞춤·별·먼지·그림자): 큰 터짐 규칙(0.1초에 2개, 넘치면 미루고 그 뒤로는 작게) 안에서 —
        /// 첫 프레임 범위 빛(fx_glow_core 지름 1.6 × 반지름, 가산 0.6, 66ms) · 바닥 고리(반지름까지 0.25초, 가산 0.7) · 그림이 1.35배로 쾅 → 80ms에 자리 →
        /// 0.5초 동안 1.04배로 부풀며 40%부터 스민다 (크게 터지고 바로 걷힌다 — 오래 남는 스티커처럼 보이지 않게, 그림 = 반지름 × 1.8, 130~340px) ·
        /// 코어 별 · 빛 고리 · 빛줄기 6 · 사물 모양 입자 · 반짝. 어두운 키(그림자·먼지)는 잉크 주머니(α 0.35) 위에 약한 빛(가산 0.3)
        /// </summary>
        void SkillBurst(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var radius = ClampRadius(r.Radius);
            var body = r.Center + Vector2.up * 40f;
            float size = Mathf.Clamp(radius.x * BurstArtScale, BurstArtMin, BurstArtMax) * power;
            bool dark = r.Key == SkillVfx.ShadowBurst || r.Key == SkillVfx.DustPoof;
            float start = BigBurstDelay(0f);
            bool small = start < 0f;
            if (small) start = 0f;
            var light = Color.Lerp(look.Main, Color.white, dark ? 0.1f : 0.45f);

            if (dark && !small)
            {
                int ink = InkBacking(r.Center, radius.x * 2.4f, 0.35f, 0.55f, start);
                if (ink >= 0) _particles[ink].Aspect = GroundAspect;
            }
            Shockwave(r.Center, look.Accent, radius.x * 0.5f, radius.x * 2f, 0.25f, start, 0.7f, 0.85f, GroundAspect, true);
            if (!small)
            {
                int flash = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(light, 0.6f), body, radius.x * 1.75f, radius.x * 1.6f, 0.066f, start, dark ? 0.3f : 1f);
                if (flash >= 0)
                {
                    _particles[flash].FadeOut = 0.5f;
                    _particles[flash].FadeKind = FadeInQuad;
                }
            }
            int glow = SpawnFx(dark ? Puff : GlowCoreSprite, BattleEase.WithAlpha(look.Main, dark ? 0.32f : 0.35f), body, radius.x * 0.5f, radius.x * 2f, 0.5f, start,
                dark ? 0f : 0.6f);
            if (glow >= 0) _particles[glow].FadeOut = 0.4f;

            float artSize = small ? size * 0.75f : size;
            int art = Spawn(MainSprite(r), Tint(r), body, artSize * (small ? 1.2f : 1.35f), artSize, BurstArtLife, start);
            if (art >= 0)
            {
                ref var p = ref _particles[art];
                p.Curve = SizeCurve.Impact;
                p.Hold = 0.033f;
                p.Settle = 0.08f;
                p.EndScale = BurstArtSwell;
                p.Rotation = Random.Range(-10f, 10f);
                p.Spin = Random.Range(-25f, 25f);
                p.FadeIn = 0f;
                p.FadeOut = BurstArtFade;
                p.FadeKind = FadeInQuad;
                p.Rect.sizeDelta = new Vector2(p.Size0, p.Size0);
            }
            AnchorTo(art, r.AnchorEcho);

            var streakTint = BattleEase.WithAlpha(JuiceColors.TowardCream(dark ? look.Accent : look.Main, 0.5f), dark ? 0.6f : 0.9f);
            CoreStar(body, JuiceColors.TowardCream(dark ? look.Accent : look.Main, 0.6f), size * 0.55f, size * 0.36f, 0.08f, start, dark ? 0.5f : 1f);
            if (!small && !dark) Shockwave(body, Color.Lerp(look.Main, UITheme.Cream, 0.4f), size * 0.35f, size * 1.45f, 0.26f, start, 0.85f, 0.7f);
            Streaks(body, Vector2.up, small ? 3 : dark ? 4 : 6, 360f, 520f, 820f, streakTint, start, 24f, 5f);

            var motif = Motif(look.Particle);
            bool soft = look.Particle == VfxMotif.Soft;
            int count = Mathf.RoundToInt((small ? 5f : 9f) * power);
            for (int i = 0; i < count; i++)
            {
                var dir = RandomDirection();
                dir.y = dir.y * 0.7f + 0.25f;
                int index = Drift(motif, BattleEase.WithAlpha(i % 2 == 0 ? look.Main : look.Accent, soft ? 0.6f : 1f), body + dir * 12f,
                    dir * radius.x * Random.Range(0.55f, 1.05f), Vector2.down * (soft ? -20f : 50f),
                    Random.Range(22f, 32f) * (soft ? 2.2f : 1f), soft ? 70f : 8f, Random.Range(0.55f, 0.8f), start + i * 0.012f);
                if (index < 0) continue;
                _particles[index].Spin = soft ? 0f : Random.Range(-160f, 160f);
                if (!soft) _particles[index].Add = 0.3f;
            }
            if (dark) Sparkles(body, radius.x * 0.9f, look.Accent, 6, start + 0.05f);
            else SkillLightSparkles(body, radius.x * 0.9f, UITheme.Cream, 6, start + 0.05f);
        }

        /// <summary>
        /// 퍼지는 고리 (음파·폭풍·드러냄): 가운데 빛(가산) · 바닥 고리 셋(가산 0.5, 2·3번째는 안쪽 빛 테두리 가산 0.8) · 바닥에 눕힌 그림(가산 0.5) ·
        /// 고리를 타고 퍼지는 입자. 첫 고리의 반지름이 적에 닿는 순간이 그 적의 명중 (SkillReachDelay)
        /// </summary>
        void SkillRing(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var radius = ClampRadius(r.Radius);
            float aspect = GroundAspect;
            bool swirl = r.Key == SkillVfx.StormSwirl;

            SkillLightGlow(r.Center + Vector2.up * 24f, look.Main, radius.x * 0.3f, radius.x * 1.2f, 0.4f, 0f, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                float delay = i * 0.12f;
                var color = i == 1 ? look.Accent : look.Main;
                int ring = Ring(r.Center, BattleEase.WithAlpha(color, 1f - i * 0.22f), radius.x * RingFrom, radius.x * RingTo, RingLife, aspect, delay);
                if (ring >= 0) _particles[ring].Add = 0.5f;
                if (i == 0) continue;
                int edge = Shockwave(r.Center, Color.Lerp(color, UITheme.Cream, 0.35f), radius.x * RingFrom * 0.9f, radius.x * RingTo * 0.92f, RingLife, delay, 0.8f, 0.55f,
                    aspect);
                if (edge < 0) continue;
                _particles[edge].Curve = SizeCurve.Out;
                _particles[edge].FadeOut = 0.5f;
            }

            // 바닥에 눕힌 그림이 퍼져 나간다 (음파·소용돌이·드러냄 빛)
            int art = Spawn(MainSprite(r), BattleEase.WithAlpha(Tint(r), r.IsArt ? 0.95f : 0.8f), r.Center + Vector2.up * 8f, radius.x * 0.4f, radius.x * 2.3f * power, 0.8f, 0f);
            if (art >= 0)
            {
                ref var p = ref _particles[art];
                p.Aspect = aspect;
                p.Rotation = 0f;
                p.Spin = swirl ? 160f : 0f;
                p.FadeIn = 0.04f;
                p.FadeOut = 0.5f;
                p.Add = 0.5f;
            }

            // 고리를 타고 퍼지는 입자 (음표·빛 꼬리·별빛)
            var motif = Motif(look.Particle);
            bool trail = look.Particle == VfxMotif.Trail;
            const int riders = 10;
            for (int i = 0; i < riders; i++)
            {
                float angle = i * Mathf.PI * 2f / riders + Random.Range(-0.15f, 0.15f);
                if (swirl)
                {
                    int orbit = Orbit(motif, BattleEase.WithAlpha(i % 2 == 0 ? look.Main : look.Accent, 0.9f), r.Center + Vector2.up * 20f, radius,
                        angle, 5.5f, 0.2f, 1.05f, 90f, 60f, 0.7f, i * 0.02f);
                    if (orbit < 0) continue;
                    _particles[orbit].AlignToPath = true;
                    _particles[orbit].Aspect = 0.28f;
                    _particles[orbit].Add = 0.5f;
                    continue;
                }
                var dir = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                int index = Drift(motif, i % 2 == 0 ? look.Main : look.Accent, r.Center + Vector2.up * 24f, dir * 1.05f, Vector2.up * 30f,
                    trail ? 70f : 28f, trail ? 40f : 12f, 0.7f, 0.04f);
                if (index < 0) continue;
                ref var q = ref _particles[index];
                q.Spin = trail ? 0f : Random.Range(-60f, 60f);
                q.AlignToPath = trail;
                q.Aspect = trail ? 0.28f : 1f;
                q.FadeOut = 0.55f;
                q.Add = 0.4f;
            }
        }

        /// <summary>바닥 장판 (촛농·실타래·마법진·달빛): 바닥 빛(가산 0.35)이 0.5초마다 +0.15 맥박치고, 그림이 깔렸다 스미며 입자가 피어오른다</summary>
        void SkillGround(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var radius = ClampRadius(r.Radius);
            float aspect = radius.y / Mathf.Max(1f, radius.x);
            bool circle = r.Key == SkillVfx.SummonCircle;
            bool moon = r.Key == SkillVfx.MoonGlow;

            int glow = Spawn(GlowCoreSprite, BattleEase.WithAlpha(look.Main, moon ? 0.6f : 0.45f), r.Center, radius.x * 1.4f, radius.x * 2.5f * power, 1.5f, 0f, true);
            if (glow >= 0)
            {
                ref var g = ref _particles[glow];
                g.Aspect = aspect;
                g.FadeOut = 0.55f;
                g.Rotation = 0f;
                g.Add = 0.35f;
            }
            // 0.5초마다 빛이 한 번 더 차오른다 (+0.15)
            for (int i = 1; i <= 2; i++)
            {
                int pulse = Spawn(GlowCoreSprite, BattleEase.WithAlpha(JuiceColors.TowardCream(look.Main, 0.3f), 0.3f), r.Center, radius.x * 2.1f * power,
                    radius.x * 2.4f * power, 0.32f, i * 0.5f, true);
                if (pulse < 0) continue;
                ref var p = ref _particles[pulse];
                p.Aspect = aspect;
                p.Rotation = 0f;
                p.Add = 0.5f;
                p.FadeIn = 0.35f;
                p.FadeOut = 0.45f;
            }

            int decal = Spawn(MainSprite(r), BattleEase.WithAlpha(Tint(r), r.IsArt ? 0.95f : 0.75f), r.Center, radius.x * 1.9f * power, radius.x * 2.1f * power, 1.9f, 0f, true);
            if (decal >= 0)
            {
                ref var d = ref _particles[decal];
                d.Curve = SizeCurve.Pop;
                d.Aspect = aspect;
                d.Rotation = 0f;
                d.Spin = circle ? 28f : 0f;
                d.FadeIn = 0.03f;
                d.FadeOut = 0.72f;
            }
            int ring = Ring(r.Center, look.Accent, radius.x * 0.5f, radius.x * 2.2f, 0.55f, aspect);
            if (ring >= 0) _particles[ring].Add = 0.5f;

            // 장판 위로 피어오르는 입자 (촛농 방울·실 보풀·달빛 별빛·마법진 불씨)
            var motif = Motif(look.Particle);
            int count = Mathf.RoundToInt(10f * power);
            for (int i = 0; i < count; i++)
            {
                var spot = RandomInEllipse(radius * 0.85f);
                int index = Drift(motif, i % 2 == 0 ? look.Main : look.Accent, r.Center + spot, Vector2.up * Random.Range(40f, 90f), Vector2.zero,
                    Random.Range(14f, 24f), 6f, Random.Range(0.8f, 1.2f), Random.Range(0f, 1f));
                if (index < 0) continue;
                _particles[index].Wobble = Random.Range(4f, 10f);
                _particles[index].Spin = Random.Range(-60f, 60f);
                _particles[index].FadeOut = 0.5f;
                _particles[index].Add = 0.3f;
            }
            if (circle)
            {
                for (int i = 0; i < 6; i++)
                {
                    int orbit = Orbit(_sparkle, i % 2 == 0 ? look.Accent : UITheme.Cream, r.Center, radius * 0.95f, i * Mathf.PI / 3f, 2.2f, 1f, 1f, 22f, 10f, 1.3f, 0.1f);
                    if (orbit >= 0) _particles[orbit].Add = 0.6f;
                }
            }
        }

        /// <summary>보호막 돔 (단추·우산·거품): 바닥 고리(가산 0.5) · 빛(가산 0.4) · 돔이 1.3배로 톡 → 자리 · 반짝 하나(가산 0.4) · 광택이 훑고 지나간다(가산 0.6)</summary>
        void SkillDome(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            float width = Mathf.Clamp(r.Radius.x * 2f, 120f, 340f) * power;
            var center = r.Center + Vector2.up * width * 0.32f;

            int ring = Ring(r.Center, look.Main, width * 0.4f, width * 1.15f, 0.55f, GroundAspect);
            if (ring >= 0) _particles[ring].Add = 0.5f;
            int light = SkillLightGlow(center, look.Accent, width * 0.3f, width * 1.1f, 0.5f, 0f, 0.4f);
            AnchorTo(light, r.AnchorEcho);
            int dome = Spawn(MainSprite(r), BattleEase.WithAlpha(Tint(r), r.IsArt ? 0.92f : 0.6f), center, width * 1.3f, width, 1.15f, 0f);
            if (dome >= 0) SkillImpactCurve(dome, 0.62f);
            AnchorTo(dome, r.AnchorEcho);
            AnchorTo(SkillGlint(center + new Vector2(-width * 0.22f, width * 0.18f), width * 0.3f, 0.05f), r.AnchorEcho);
            AnchorTo(SkillGlossSweep(center, width, 0.1f), r.AnchorEcho);

            // 돔 표면을 도는 반짝임
            for (int i = 0; i < 6; i++)
            {
                int shine = Orbit(_sparkle, i % 2 == 0 ? UITheme.Cream : look.Main, center, new Vector2(width * 0.46f, width * 0.3f),
                    i * Mathf.PI / 3f, 3.2f, 0.7f, 1f, 22f, 8f, 0.95f, 0.05f);
                if (shine >= 0) _particles[shine].Add = 0.5f;
                AnchorTo(shine, r.AnchorEcho);
            }
            // 단추·빗방울이 돔 둘레로 톡톡
            var motif = Motif(look.Particle);
            if (look.Particle != VfxMotif.Sparkle)
            {
                for (int i = 0; i < 5; i++)
                {
                    float angle = Mathf.PI * (0.15f + 0.175f * i);
                    var at = center + new Vector2(Mathf.Cos(angle) * width * 0.5f, Mathf.Sin(angle) * width * 0.42f);
                    int index = Drift(motif, i % 2 == 0 ? look.Main : look.Accent, at, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 24f, Vector2.down * 30f,
                        24f, 14f, 0.75f, 0.1f + i * 0.04f);
                    if (index >= 0) _particles[index].Spin = Random.Range(-90f, 90f);
                    AnchorTo(index, r.AnchorEcho);
                }
            }
        }

        /// <summary>떠오름 (음표·하트·반딧불): 바닥 빛·고리(가산) · 그림이 1.3배로 톡 → 자리로 떠오르고 반짝 하나(가산 0.4) · 입자가 둥실 (가산 0.3)</summary>
        void SkillRise(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var radius = ClampRadius(r.Radius);
            var body = r.Center + Vector2.up * 44f;

            int light = SkillLightGlow(r.Center + Vector2.up * 16f, look.Main, radius.x * 0.5f, radius.x * 1.7f, 0.65f, 0f, 0.5f);
            AnchorTo(light, r.AnchorEcho);
            int ring = Ring(r.Center, BattleEase.WithAlpha(look.Main, 0.8f), radius.x * 0.3f, radius.x * 1.8f, 0.6f, GroundAspect);
            if (ring >= 0) _particles[ring].Add = 0.5f;
            float size = Mathf.Clamp(radius.x * 1.2f, 90f, 220f) * power;
            int art = Drift(MainSprite(r), Tint(r), body, Vector2.up * 70f, Vector2.zero, size * 1.3f, size, 1.15f);
            if (art >= 0)
            {
                SkillImpactCurve(art, 0.62f);
                ref var p = ref _particles[art];
                p.Spin = 0f;
                p.Rotation = Random.Range(-8f, 8f);
                p.Wobble = 6f;
            }
            AnchorTo(art, r.AnchorEcho);
            AnchorTo(SkillGlint(body + new Vector2(size * 0.28f, size * 0.22f), size * 0.35f, 0.06f), r.AnchorEcho);

            var motif = Motif(look.Particle);
            int count = Mathf.RoundToInt(11f * power);
            bool glowMotes = look.Particle == VfxMotif.Glow;
            for (int i = 0; i < count; i++)
            {
                var spot = RandomInEllipse(radius);
                var color = (i % 3) switch { 0 => look.Main, 1 => look.Accent, _ => UITheme.Cream };
                int index = Drift(motif, color, r.Center + spot + Vector2.up * Random.Range(10f, 40f), new Vector2(Random.Range(-20f, 20f), Random.Range(80f, 150f)),
                    Vector2.zero, glowMotes ? Random.Range(26f, 40f) : Random.Range(22f, 32f), glowMotes ? 16f : 12f, Random.Range(0.85f, 1.25f), i * 0.045f);
                if (index < 0) continue;
                ref var q = ref _particles[index];
                q.Wobble = Random.Range(6f, 14f);
                q.Spin = look.Particle == VfxMotif.Note ? Random.Range(-40f, 40f) : Random.Range(-20f, 20f);
                q.Rotation = Random.Range(-15f, 15f);
                q.FadeOut = 0.6f;
                q.Add = glowMotes ? 0.7f : 0.3f;
                AnchorTo(index, r.AnchorEcho);
            }
            SkillLightSparkles(body, radius.x * 0.7f, UITheme.Cream, 5, 0.1f);
        }

        /// <summary>머리 위 (기절 별·왕관·자물쇠·하트): 빛(가산) · 그림이 1.3배로 톡 → 자리 · 반짝 하나(가산 0.4) · 모양 입자. 약화 표식은 차분하게 (알파)</summary>
        void SkillOverHead(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var head = r.Target != null ? r.Target.HeadPoint : r.To + Vector2.up * 60f;
            if (r.AnchorEcho != null) head = r.AnchorEcho.HeadPoint;
            bool muted = r.Key == SkillVfx.DebuffMark;

            int glow = muted
                ? Flash(head, _glow, BattleEase.WithAlpha(look.Main, 0.75f), 40f, 140f * power, 0.5f)
                : SkillLightGlow(head, look.Main, 40f, 140f * power, 0.5f, 0f, 0.6f);
            int art = Spawn(MainSprite(r), Tint(r), head, 84f * power * 1.3f, 84f * power, 1f, 0f);
            if (art >= 0)
            {
                SkillImpactCurve(art, 0.62f);
                _particles[art].P1 = Vector2.up * 16f;
            }
            int glint = muted ? -1 : SkillGlint(head + new Vector2(30f, 26f) * power, 40f * power, 0.05f);
            var motif = Motif(look.Particle);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.25f);
                int index = Drift(motif, i % 2 == 0 ? look.Main : look.Accent, head, dir * 60f * power, Vector2.down * 30f, 22f, 6f, 0.6f, 0.04f);
                if (index < 0) continue;
                _particles[index].Spin = Random.Range(-200f, 200f);
                if (!muted) _particles[index].Add = 0.3f;
                AnchorAll(index, r);
            }
            AnchorAll(glow, r);
            AnchorAll(art, r);
            AnchorAll(glint, r);
        }

        /// <summary>회전 (리본·회전 단추): 빛(가산) · 그림 셋이 120° 간격으로 시전자 둘레를 돌고(빛 리본 꼬리 가산 0.5) · 바닥 고리 · 반짝</summary>
        void SkillOrbit(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var radius = ClampRadius(r.Radius) * OrbitRadiusScale;
            var center = r.Center + Vector2.up * 36f;
            bool ribbon = r.Key == SkillVfx.WaltzRibbon;
            var sprite = MainSprite(r);
            var tint = Tint(r);

            SkillLightGlow(center, look.Main, radius.x * 0.4f, radius.x * 1.6f, 0.6f, 0f, 0.6f);
            for (int i = 0; i < OrbitCopies; i++)
            {
                float start = i * Mathf.PI * 2f / OrbitCopies;
                // 리본 꼬리 (그림 아래)
                int tail = Orbit(_trail, BattleEase.WithAlpha(i % 2 == 0 ? look.Main : look.Accent, 0.9f), center, radius, start - 0.25f, OrbitSpeed, 0.25f, 1f,
                    110f * power, 80f, 0.8f);
                if (tail >= 0)
                {
                    _particles[tail].AlignToPath = true;
                    _particles[tail].Aspect = ribbon ? 0.4f : 0.26f;
                    _particles[tail].Add = 0.5f;
                }
                int index = Orbit(sprite, tint, center, radius, start, OrbitSpeed, 0.25f, 1f, 54f * power, 64f * power, 0.8f);
                if (index >= 0)
                {
                    _particles[index].Spin = ribbon ? 120f : 720f;
                    _particles[index].FadeIn = 0.04f;
                }
            }
            int ring = Ring(r.Center, look.Accent, radius.x * 0.5f, radius.x * 2.3f, 0.5f, GroundAspect, 0.32f);
            if (ring >= 0) _particles[ring].Add = 0.5f;
            var motif = Motif(look.Particle == VfxMotif.Trail ? VfxMotif.Sparkle : look.Particle);
            for (int i = 0; i < 8; i++)
            {
                var dir = RandomDirection();
                int index = Drift(motif, i % 2 == 0 ? look.Main : UITheme.Cream, center, new Vector2(dir.x * radius.x, dir.y * radius.y) * 1.2f,
                    Vector2.down * 30f, 22f, 6f, 0.55f, 0.38f);
                if (index < 0) continue;
                _particles[index].Spin = Random.Range(-300f, 300f);
                _particles[index].Add = 0.5f;
            }
        }

        /// <summary>
        /// 내림: 구름이 끼고 빗방울이 떨어진다 (닿으면 작은 고리 가산 0.5). 구름은 전장 위쪽 끝(마스크) 안에 머물도록 높이를 자르고,
        /// 전장을 다 덮는 넓은 비는 구름을 전장 폭에 맞춰 납작하게, 빗줄기 그림은 여러 개로 나눠 건다.
        /// 적마다 그 머리 위 빗방울 하나가 명중 순간에 떨어진다 (OnSkillHit)
        /// </summary>
        void SkillRain(in SkillFxRequest r)
        {
            var look = r.Look;
            float power = Mathf.Max(0.6f, r.Power);
            var radius = ClampRadius(r.Radius);
            float top = FieldTop;
            float halfWidth = FieldHalfWidth;
            float cloudWidth = Mathf.Min(radius.x * 2.1f, halfWidth * 1.9f);
            float cloudHeight = Mathf.Min(cloudWidth * 0.42f, 150f);
            var cloud = r.Center + Vector2.up * (radius.y + 170f);
            cloud.y = Mathf.Min(cloud.y, top - cloudHeight * 0.5f - 12f);
            cloud.x = Mathf.Clamp(cloud.x, -halfWidth + cloudWidth * 0.5f, halfWidth - cloudWidth * 0.5f);

            int puff = Spawn(Puff, BattleEase.WithAlpha(Color.Lerp(look.Main, UITheme.Ground, 0.45f), 0.55f), cloud, cloudWidth * 0.72f, cloudWidth, 1.5f, 0f);
            if (puff >= 0)
            {
                _particles[puff].Curve = SizeCurve.Pop;
                _particles[puff].Aspect = cloudHeight / Mathf.Max(1f, cloudWidth);
                _particles[puff].Rotation = 0f;
                _particles[puff].FadeOut = 0.7f;
            }
            if (r.IsArt)
            {
                // 빗줄기 그림은 구름 아래에 매단다 (너무 크면 잘리므로 크기를 제한하고, 넓은 비는 여러 줄기로)
                float artSize = Mathf.Min(radius.x * 1.2f, 250f);
                int columns = cloudWidth > 560f ? 3 : cloudWidth > 320f ? 2 : 1;
                float artY = Mathf.Min(cloud.y - cloudHeight * 0.2f - artSize * 0.3f, top - artSize * 0.5f);
                for (int i = 0; i < columns; i++)
                {
                    float x = columns == 1 ? 0f : Mathf.Lerp(-0.32f, 0.32f, i / (columns - 1f)) * cloudWidth;
                    int art = Spawn(r.Sprite, Color.white, new Vector2(cloud.x + x, artY), artSize * 0.88f, artSize, 1.4f, 0.05f + i * 0.08f);
                    if (art < 0) continue;
                    _particles[art].Curve = SizeCurve.Pop;
                    _particles[art].Rotation = 0f;
                    _particles[art].FadeOut = 0.7f;
                    _particles[art].P1 = Vector2.down * 26f;
                }
            }

            var drop = Motif(VfxMotif.Drop);
            int count = Mathf.RoundToInt(18f * power);
            for (int i = 0; i < count; i++)
            {
                var land = r.Center + RandomInEllipse(radius);
                var start = new Vector2(land.x + Random.Range(-20f, 20f), cloud.y + Random.Range(-30f, 10f));
                int index = Spawn(drop, i % 3 == 0 ? Color.white : look.Main, start, 20f, 18f, Random.Range(0.36f, 0.48f), 0.1f + i * 0.05f, sparkle: true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.P2 = land - start;
                p.Aspect = 1.35f;
                p.Rotation = 0f;
                p.FadeIn = 0.1f;
                p.FadeOut = 0.92f;
                p.Arrival = Arrival.Splash;
                p.Color2 = look.Accent;
                p.Add = 0.2f;
            }
            int ring = Ring(r.Center, look.Main, radius.x * 0.4f, radius.x * 2.1f, 0.7f, GroundAspect, 0.5f);
            if (ring >= 0) _particles[ring].Add = 0.5f;
        }

        /// <summary>전체 공격 스킬이 적 하나에 닿는 순간: 빛(가산) 위에 같은 그림이 1.35배로 톡 피어나고 모양 입자가 튄다</summary>
        public void SkillSplash(in SkillFxRequest r, EnemyView enemy, float delay)
        {
            if (enemy == null) return;
            if (!_ready) Prepare();
            var at = enemy.HitPoint;
            var look = r.Look;
            int glow = SkillLightGlow(at, look.Main, 30f, 130f, 0.38f, delay, 0.7f);
            AnchorTo(glow, enemy);
            int pop = ImpactArt(MainSprite(r), Tint(r), at, 60f, HitFeel.Feel(HitTier.Skill), 0.55f, delay, 1.05f);
            if (pop >= 0)
            {
                _particles[pop].Rotation = Random.Range(-12f, 12f);
                _particles[pop].FadeOut = 0.5f;
            }
            AnchorTo(pop, enemy);
            var motif = Motif(look.Particle == VfxMotif.Trail ? VfxMotif.Sparkle : look.Particle);
            for (int i = 0; i < 3; i++)
            {
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.7f + 0.3f;
                int index = Drift(motif, i % 2 == 0 ? look.Main : look.Accent, at, dir * Random.Range(40f, 70f), Vector2.down * 50f, 20f, 6f, 0.5f, delay + 0.03f);
                if (index >= 0) _particles[index].Add = 0.3f;
                AnchorTo(index, enemy);
            }
        }

        // ───────────── 상태 / 회복 / 보호막 ─────────────

        /// <summary>
        /// 적에게 상태가 붙는 순간 (기절 별, 감속 실타래, 표식 하트, 불꽃, 넉백 먼지, 끌어당김 소용돌이, 드러냄 빛).
        /// delay: 투사체가 아직 날아가는 중이면 닿는 순간까지 늦춘다. 아군이 건 상태는 빛(가산)으로, 적끼리 감아 주는 회복은 차분하게.
        /// </summary>
        public void EnemyStatus(SkillEffectType type, EnemyView enemy, Sprite sprite, bool isArt, in SkillLook look, float delay = 0f)
        {
            if (enemy == null) return;
            if (!_ready) Prepare();
            var head = enemy.HeadPoint;
            var body = enemy.HitPoint;
            var feet = enemy.Rect.anchoredPosition;
            var tint = isArt ? Color.white : look.Main;
            switch (type)
            {
                case SkillEffectType.Stun:
                {
                    int pop = PopAt(sprite, tint, head + Vector2.up * 8f, 58f, 0.6f, delay);
                    AnchorTo(pop, enemy);
                    var star = Motif(VfxMotif.Star);
                    for (int i = 0; i < 4; i++)
                    {
                        var dir = new Vector2(Mathf.Cos(i * Mathf.PI * 0.5f + 0.4f), 0.6f);
                        int index = Drift(star, i % 2 == 0 ? UITheme.Lamp : UITheme.Cream, head, dir * 46f, Vector2.down * 40f, 22f, 8f, 0.5f, delay);
                        if (index >= 0) _particles[index].Add = 0.4f;
                        AnchorTo(index, enemy);
                    }
                    break;
                }
                case SkillEffectType.Slow:
                {
                    int web = Spawn(sprite, BattleEase.WithAlpha(tint, 0.85f), feet, 70f, 100f, 1.1f, delay, true);
                    if (web >= 0)
                    {
                        ref var p = ref _particles[web];
                        p.Curve = SizeCurve.Pop;
                        p.Aspect = GroundAspect;
                        p.Rotation = 0f;
                        p.Spin = 40f;
                        p.FadeOut = 0.6f;
                    }
                    AnchorTo(web, enemy);
                    SkillLightSparkles(body, 30f, UITheme.SoftBlue, 3, delay);
                    break;
                }
                case SkillEffectType.Mark:
                {
                    int pop = PopAt(sprite, tint, head + Vector2.up * 6f, 56f, 0.7f, delay);
                    AnchorTo(pop, enemy);
                    int glow = SkillLightGlow(head, look.Main, 30f, 110f, 0.4f, delay, 0.6f);
                    AnchorTo(glow, enemy);
                    break;
                }
                case SkillEffectType.DamageOverTime:
                {
                    var flame = Motif(VfxMotif.Flame);
                    for (int i = 0; i < 4; i++)
                    {
                        int index = Drift(flame, i % 2 == 0 ? new Color(1f, 0.72f, 0.42f, 1f) : UITheme.Lamp, body + new Vector2(Random.Range(-18f, 18f), Random.Range(-14f, 8f)),
                            Vector2.up * Random.Range(40f, 70f), Vector2.zero, Random.Range(22f, 30f), 8f, Random.Range(0.45f, 0.6f), delay + i * 0.05f);
                        if (index < 0) continue;
                        _particles[index].Spin = 0f;
                        _particles[index].Rotation = Random.Range(-10f, 10f);
                        _particles[index].Wobble = 4f;
                        _particles[index].Add = 0.35f;
                        AnchorTo(index, enemy);
                    }
                    break;
                }
                case SkillEffectType.Knockback:
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var dir = RandomDirection();
                        Drift(Puff, BattleEase.WithAlpha(UITheme.Lavender, 0.5f), feet + Vector2.up * 8f, new Vector2(dir.x * 40f, Mathf.Abs(dir.y) * 20f),
                            Vector2.up * 10f, 24f, 56f, 0.45f, delay);
                    }
                    int ring = Ring(feet, UITheme.Cream, 20f, 110f, 0.35f, GroundAspect, delay);
                    if (ring >= 0) _particles[ring].Add = 0.5f;
                    break;
                }
                case SkillEffectType.Pull:
                {
                    var swirl = Motif(VfxMotif.Swirl);
                    int index = Spawn(swirl, BattleEase.WithAlpha(look.Main, 0.85f), feet + Vector2.up * 10f, 110f, 30f, 0.5f, delay, true);
                    if (index >= 0)
                    {
                        _particles[index].Aspect = GroundAspect;
                        _particles[index].Rotation = 0f;
                        _particles[index].Spin = -420f;
                    }
                    AnchorTo(index, enemy);
                    for (int i = 0; i < 4; i++)
                    {
                        int orbit = Orbit(_sparkle, i % 2 == 0 ? look.Main : UITheme.Cream, body, new Vector2(60f, 34f), i * Mathf.PI * 0.5f, -9f, 1f, 0.1f, 20f, 8f, 0.45f, delay);
                        if (orbit >= 0) _particles[orbit].Add = 0.6f;
                        AnchorTo(orbit, enemy);
                    }
                    break;
                }
                case SkillEffectType.Reveal:
                    RevealFlash(enemy, delay);
                    break;
                case SkillEffectType.Heal:
                case SkillEffectType.HealOverTime:
                {
                    // 적끼리 감아 주는 회복 (태엽쥐): 작은 천 조각이 붙고 연둣빛이 피어오른다 (적 쪽이라 빛 없이)
                    int pop = PopAt(sprite, tint, head, 40f, 0.55f, delay);
                    AnchorTo(pop, enemy);
                    for (int i = 0; i < 3; i++)
                    {
                        int index = Drift(_sparkle, i % 2 == 0 ? UITheme.Positive : UITheme.Cream, body + new Vector2(Random.Range(-20f, 20f), Random.Range(-10f, 10f)),
                            Vector2.up * Random.Range(40f, 60f), Vector2.zero, 16f, 4f, 0.5f, delay + i * 0.06f);
                        AnchorTo(index, enemy);
                    }
                    break;
                }
                default:
                {
                    int pop = PopAt(sprite, tint, head, 52f, 0.6f, delay);
                    AnchorTo(pop, enemy);
                    break;
                }
            }
        }

        /// <summary>
        /// 연쇄 한 번 (열쇠 빛·광택 반사가 적에게서 적으로 튄다): start초 뒤 앞 대상(from)에서 이번 대상까지 빛 꼬리(가산)와 작은 스킬 그림이
        /// travel초 동안 튀어 가고, 닿는 순간 빛 위에 그림이 1.35배로 톡 피며 별빛이 튄다. link = false면 이어 주는 꼬리 없이 닿는 연출만.
        /// </summary>
        public void ChainHop(Vector2 from, EnemyView enemy, Sprite sprite, bool isArt, in SkillLook look, float start, float travel, bool link)
        {
            if (enemy == null) return;
            if (!_ready) Prepare();
            var to = enemy.HitPoint;
            var tint = isArt ? Color.white : look.Main;
            var icon = sprite != null ? sprite : _sparkle;
            if (link && (to - from).sqrMagnitude > 400f)
            {
                float distance = (to - from).magnitude;
                var control = (from + to) * 0.5f + Vector2.up * Mathf.Min(70f, distance * 0.35f);
                int streak = Curve(_trail, BattleEase.WithAlpha(look.Main, 0.95f), from, control, to, 92f, 52f, travel, start);
                if (streak >= 0)
                {
                    ref var s = ref _particles[streak];
                    s.AlignToPath = true;
                    s.Aspect = 0.28f;
                    s.FadeIn = 0.05f;
                    s.FadeOut = 0.9f;
                    s.Add = 0.6f;
                }
                int rider = Curve(icon, tint, from, control, to, 44f, 38f, travel, start);
                if (rider >= 0)
                {
                    ref var p = ref _particles[rider];
                    p.AlignToPath = isArt;
                    p.Spin = isArt ? 0f : 480f;
                    p.FadeIn = 0.05f;
                    p.FadeOut = 0.95f;
                }
                if (isArt) FaceTo(rider, from, control);
                int spark = Curve(_sparkle, UITheme.Cream, from, control, to, 26f, 16f, travel, start + 0.02f);
                if (spark >= 0)
                {
                    _particles[spark].Spin = 540f;
                    _particles[spark].Add = 0.8f;
                }
            }
            float at = start + travel;
            int glow = SkillLightGlow(to, Color.Lerp(look.Main, Color.white, 0.3f), 30f, 130f, 0.32f, at, 0.8f);
            AnchorTo(glow, enemy);
            int pop = ImpactArt(icon, tint, to + Vector2.up * 6f, 54f, HitFeel.Feel(HitTier.Skill), 0.5f, at, 1.05f);
            if (pop >= 0) _particles[pop].FadeOut = 0.6f;
            AnchorTo(pop, enemy);
            SkillLightSparkles(to, 34f, look.Accent, 3, at);
        }

        /// <summary>
        /// 아군(잔향이/지킴이)에게 상태가 붙는 순간 (강화 왕관, 약화 표식, 정화 반짝임, 보호막 거품, 지속 회복 하트).
        /// delay: 시전자에게서 선물이 날아가는 중이면 닿는 순간까지 늦춘다. 아군 강화는 따뜻한 빛(가산), 적이 건 약화는 차분하게.
        /// </summary>
        public void AllyStatus(SkillEffectType type, Vector2 head, EchoView echo, Sprite sprite, bool isArt, in SkillLook look, bool debuff = false, float delay = 0f)
        {
            if (!_ready) Prepare();
            var tint = isArt ? Color.white : look.Main;
            switch (debuff ? (SkillEffectType)(-1) : type)
            {
                case SkillEffectType.BuffAttack:
                case SkillEffectType.BuffAttackSpeed:
                {
                    int pop = PopAt(sprite, tint, head + Vector2.up * 10f, 60f, 0.85f, delay);
                    AnchorTo(pop, echo);
                    int glow = SkillLightGlow(head, UITheme.Lamp, 40f, 140f, 0.5f, delay, 0.55f);
                    AnchorTo(glow, echo);
                    for (int i = 0; i < 5; i++)
                    {
                        int index = Drift(_sparkle, i % 2 == 0 ? UITheme.Lamp : UITheme.Cream, head + new Vector2(Random.Range(-30f, 30f), Random.Range(-60f, -10f)),
                            Vector2.up * Random.Range(50f, 80f), Vector2.zero, 20f, 6f, 0.6f, delay + i * 0.05f);
                        if (index >= 0)
                        {
                            _particles[index].Spin = 0f;
                            _particles[index].Add = 0.6f;
                        }
                        AnchorTo(index, echo);
                    }
                    break;
                }
                case SkillEffectType.Cleanse:
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var start = head + new Vector2(Random.Range(-40f, 40f), Random.Range(30f, 70f));
                        int index = Drift(_sparkle, i % 2 == 0 ? new Color(0.91f, 1f, 0.96f, 1f) : UITheme.Positive, start, Vector2.down * Random.Range(60f, 100f),
                            Vector2.zero, Random.Range(16f, 24f), 6f, 0.6f, delay + i * 0.04f);
                        if (index >= 0) _particles[index].Add = 0.5f;
                        AnchorTo(index, echo);
                    }
                    int ring = Ring(head + Vector2.down * 50f, UITheme.Positive, 20f, 110f, 0.45f, GroundAspect, delay);
                    if (ring >= 0) _particles[ring].Add = 0.5f;
                    AnchorTo(ring, echo);
                    break;
                }
                case SkillEffectType.Shield:
                {
                    int bubble = PopAt(sprite, BattleEase.WithAlpha(tint, isArt ? 0.9f : 0.6f), head + Vector2.down * 30f, 96f, 0.7f, delay);
                    AnchorTo(bubble, echo);
                    AnchorTo(SkillGlint(head + new Vector2(-22f, -6f), 36f, delay + 0.06f), echo);
                    break;
                }
                case SkillEffectType.Heal:
                case SkillEffectType.HealOverTime:
                    HeartsAt(head + Vector2.down * 20f, echo, 2, delay);
                    break;
                default:
                {
                    // 약화 (녹 등): 보랏빛 표식이 내려앉는다
                    int pop = PopAt(sprite, tint, head + Vector2.up * 10f, 54f, 0.75f, delay);
                    if (pop >= 0) _particles[pop].P1 = Vector2.down * 14f;
                    AnchorTo(pop, echo);
                    int soft = Flash(head, Puff, BattleEase.WithAlpha(look.Main, 0.45f), 30f, 90f, 0.5f, delay);
                    AnchorTo(soft, echo);
                    break;
                }
            }
        }

        /// <summary>보호막이 씌워지는 순간 (수선점은 크게, 잔향이는 작게 따라다니며): 거품이 1.3배로 톡 · 바닥 고리(가산) · 반짝 하나 · 둘레를 도는 별빛</summary>
        /// <param name="lift">돔 가운데가 바닥에서 떠 있는 높이 (폭 비율)</param>
        /// <param name="delay">시전자에게서 선물이 날아가는 중이면 닿는 순간까지</param>
        public void ShieldPop(Vector2 baseCenter, float width, Sprite sprite, bool isArt, Color color, EchoView echo, float amount, float lift = 0.34f,
            float delay = 0f)
        {
            if (!_ready) Prepare();
            var center = baseCenter + Vector2.up * width * lift;
            int dome = Spawn(sprite != null ? sprite : Motif(VfxMotif.Bubble), BattleEase.WithAlpha(isArt ? Color.white : color, isArt ? 0.9f : 0.7f),
                center, width * 1.25f, width * 1.04f, 0.75f, delay);
            if (dome >= 0)
            {
                SkillImpactCurve(dome, 0.5f);
                _particles[dome].EndScale = 1f;
            }
            AnchorTo(dome, echo);
            int ring = Ring(baseCenter, color, width * 0.3f, width * 1.2f, 0.5f, GroundAspect, delay);
            if (ring >= 0) _particles[ring].Add = 0.5f;
            AnchorTo(ring, echo);
            AnchorTo(SkillGlint(center + new Vector2(-width * 0.2f, width * 0.16f), width * 0.28f, delay + 0.05f), echo);
            for (int i = 0; i < 5; i++)
            {
                int shine = Orbit(_sparkle, i % 2 == 0 ? UITheme.Cream : color, center, new Vector2(width * 0.48f, width * 0.32f), i * 1.256f, 3.5f, 0.6f, 1f, 20f, 6f, 0.7f,
                    delay);
                if (shine >= 0) _particles[shine].Add = 0.5f;
                AnchorTo(shine, echo);
            }
            if (amount >= 1f)
            {
                Text(center + Vector2.up * width * 0.45f, BattleEase.Plus(amount), ShieldBlue, echo != null ? 0.75f : 0.9f, delay);
            }
        }

        // ───────────── 아군 전체 스킬 / 헛손질 ─────────────

        /// <summary>
        /// 아군 전체(잔향이 모두·지킴이 모두) 스킬의 선물: 시전자에게서 받는 친구에게 작은 스킬 그림이 빛 꼬리(가산)를 끌며 포물선으로 날아간다 (travel초).
        /// 받는 쪽 연출은 호출하는 쪽이 travel만큼 늦춰 띄운다.
        /// </summary>
        public void Gift(Vector2 from, Vector2 to, Sprite sprite, bool isArt, in SkillLook look, float travel)
        {
            if (!_ready) Prepare();
            if ((to - from).sqrMagnitude < 900f) return;
            float distance = (to - from).magnitude;
            var control = (from + to) * 0.5f + Vector2.up * (50f + distance * 0.25f);
            int tail = Curve(_trail, BattleEase.WithAlpha(look.Main, 0.8f), from, control, to, 70f, 36f, travel, 0.02f);
            if (tail >= 0)
            {
                _particles[tail].AlignToPath = true;
                _particles[tail].Aspect = 0.28f;
                _particles[tail].FadeOut = 0.9f;
                _particles[tail].Add = 0.5f;
            }
            int head = Curve(sprite != null ? sprite : _sparkle, isArt ? Color.white : look.Main, from, control, to, 40f, 34f, travel, 0f);
            if (head >= 0)
            {
                ref var p = ref _particles[head];
                p.Rotation = Random.Range(-10f, 10f);
                p.Spin = Random.Range(-60f, 60f);
                p.FadeIn = 0.05f;
                p.FadeOut = 0.9f;
            }
            int spark = Curve(_sparkle, UITheme.Cream, from, control, to, 22f, 12f, travel, 0.05f);
            if (spark >= 0)
            {
                _particles[spark].Spin = 400f;
                _particles[spark].Add = 0.7f;
            }
        }

        /// <summary>시전자 머리 위에 스킬 그림이 작게 톡 (아군 전체 스킬은 큰 그림을 수선점에 그리지 않고 시전자에게서 시작한다)</summary>
        public void CasterEmblem(Vector2 head, Sprite sprite, bool isArt, in SkillLook look, EchoView echo)
        {
            if (!_ready) Prepare();
            int glow = SkillLightGlow(head, look.Main, 36f, 130f, 0.45f, 0f, 0.7f);
            AnchorTo(glow, echo);
            int pop = ImpactArt(sprite != null ? sprite : _sparkle, isArt ? Color.white : look.Main, head + Vector2.up * 6f, 66f, HitFeel.Feel(HitTier.Lamp), 0.7f, 0f, 1f);
            if (pop >= 0)
            {
                _particles[pop].P1 = Vector2.up * 14f;
                _particles[pop].FadeOut = 0.6f;
            }
            AnchorTo(pop, echo);
            SkillLightSparkles(head, 40f, look.Accent, 4, 0.04f);
        }

        /// <summary>받을 친구가 아무도 없어 헛손질한 스킬: 작은 빛이 피식 꺼지며 별빛이 힘없이 떨어진다</summary>
        public void SkillFizzle(Vector2 head, in SkillLook look, EchoView echo)
        {
            if (!_ready) Prepare();
            int puff = Flash(head, Puff, BattleEase.WithAlpha(Color.Lerp(look.Main, UITheme.SubText, 0.5f), 0.55f), 30f, 74f, 0.5f, 0.12f);
            AnchorTo(puff, echo);
            for (int i = 0; i < 3; i++)
            {
                int index = Drift(_sparkle, BattleEase.WithAlpha(i % 2 == 0 ? look.Main : UITheme.SubText, 0.8f), head + new Vector2((i - 1) * 16f, 4f),
                    new Vector2((i - 1) * 10f, -6f), Vector2.down * 46f, 16f, 6f, 0.6f, 0.14f + i * 0.05f);
                if (index >= 0) _particles[index].Spin = Random.Range(-90f, 90f);
                AnchorTo(index, echo);
            }
        }

        // ───────────── 상시 패시브 ─────────────

        /// <summary>
        /// 녹아드는 빛 (공격력 상승 패시브): 몸에서 촛농 방울이 떨어지고 머리 위 불씨가 너울거린다. strength(0~1)가 클수록 자주·크게.
        /// </summary>
        public void WaxDrip(Vector2 body, Vector2 feet, Vector2 head, float strength, EchoView echo)
        {
            if (!_ready) Prepare();
            var drop = Motif(VfxMotif.Drop);
            var wax = Color.Lerp(UITheme.Cream, new Color(1f, 0.72f, 0.42f, 1f), strength * 0.5f);
            int index = Spawn(drop, BattleEase.WithAlpha(wax, 0.9f), body + new Vector2(Random.Range(-16f, 16f), Random.Range(-6f, 10f)),
                10f + 6f * strength, 8f, 0.55f, 0f, sparkle: true);
            if (index >= 0)
            {
                ref var p = ref _particles[index];
                p.P2 = new Vector2(0f, feet.y - p.P0.y + 4f);
                p.Aspect = 1.3f;
                p.Rotation = 0f;
                p.FadeIn = 0.15f;
                p.FadeOut = 0.85f;
                p.Arrival = Arrival.Splash;
                p.Color2 = BattleEase.WithAlpha(wax, 0.7f);
            }
            AnchorTo(index, echo);
            if (strength < 0.15f) return;
            var flame = Motif(VfxMotif.Flame);
            int fire = Drift(flame, Color.Lerp(UITheme.Lamp, new Color(1f, 0.72f, 0.42f, 1f), Random.value), head + new Vector2(Random.Range(-10f, 10f), -6f),
                Vector2.up * (24f + 30f * strength), Vector2.zero, 14f + 16f * strength, 6f, 0.5f);
            if (fire >= 0)
            {
                _particles[fire].Spin = 0f;
                _particles[fire].Rotation = Random.Range(-8f, 8f);
                _particles[fire].Wobble = 3f;
                _particles[fire].Add = 0.35f;
            }
            AnchorTo(fire, echo);
        }

        /// <summary>녹아드는 빛이 가장 세졌을 때: 발밑에 촛농 웅덩이 그림이 퍼지고 불빛(가산)과 함께 불꽃이 확 일어난다</summary>
        public void MeltBloom(Vector2 feet, Vector2 body, Sprite art, bool isArt, in SkillLook look, EchoView echo)
        {
            if (!_ready) Prepare();
            int puddle = Spawn(art != null ? art : Motif(VfxMotif.Soft), BattleEase.WithAlpha(isArt ? Color.white : look.Main, 0.9f), feet, 50f, 120f, 1.1f, 0f, true);
            if (puddle >= 0)
            {
                ref var p = ref _particles[puddle];
                p.Curve = SizeCurve.Pop;
                p.Aspect = GroundAspect;
                p.Rotation = 0f;
                p.FadeOut = 0.6f;
            }
            AnchorTo(puddle, echo);
            int glow = SkillLightGlow(body, UITheme.Lamp, 40f, 170f, 0.5f, 0f, 0.7f);
            AnchorTo(glow, echo);
            var flame = Motif(VfxMotif.Flame);
            for (int i = 0; i < 5; i++)
            {
                int index = Drift(flame, i % 2 == 0 ? UITheme.Lamp : new Color(1f, 0.72f, 0.42f, 1f), body + new Vector2((i - 2) * 12f, 10f),
                    Vector2.up * Random.Range(60f, 100f), Vector2.zero, Random.Range(20f, 28f), 8f, 0.6f, i * 0.04f);
                if (index < 0) continue;
                _particles[index].Spin = 0f;
                _particles[index].Wobble = 5f;
                _particles[index].Add = 0.4f;
                AnchorTo(index, echo);
            }
        }

        /// <summary>벨벳 마무리 (처형 패시브): 약해진 적에게 짙은 입술 자국이 1.35배로 꾹 찍히고(빛 위) 하트가 튄다</summary>
        public void ExecuteKiss(EnemyView enemy, Sprite art, bool isArt, in SkillLook look, float delay)
        {
            if (enemy == null) return;
            if (!_ready) Prepare();
            var at = enemy.HitPoint;
            int glow = SkillLightGlow(at, look.Main, 30f, 140f, 0.35f, delay, 0.7f);
            AnchorTo(glow, enemy);
            int pop = ImpactArt(art != null ? art : Motif(VfxMotif.Heart), isArt ? Color.white : look.Main, at + Vector2.up * 4f, 72f, HitFeel.Feel(HitTier.Skill), 0.5f, delay,
                1.05f);
            if (pop >= 0)
            {
                _particles[pop].Rotation = Random.Range(-16f, 16f);
                _particles[pop].FadeOut = 0.5f;
            }
            AnchorTo(pop, enemy);
            var heart = Motif(VfxMotif.Heart);
            for (int i = 0; i < 3; i++)
            {
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.7f + 0.3f;
                int index = Drift(heart, i % 2 == 0 ? look.Main : look.Accent, at, dir * Random.Range(40f, 70f), Vector2.down * 40f, 18f, 8f, 0.5f, delay + 0.03f);
                if (index >= 0)
                {
                    _particles[index].Spin = Random.Range(-90f, 90f);
                    _particles[index].Add = 0.3f;
                }
                AnchorTo(index, enemy);
            }
        }

        /// <summary>보호막이 깨지는 순간 (깨짐 알림 글자 + 바깥쪽 킥까지, 적 보호막 기본값). delay: 깨는 일격이 닿는 순간까지</summary>
        public void ShieldShatter(Vector2 center, float size, Color color, float delay = 0f) =>
            ShieldShatter(center, size, color, delay, Vector2.zero, size >= 150f, true);

        /// <summary>
        /// 보호막 깨짐 v2 (E15): 하얀 코어 별(0.6 × 크기, 66ms, 가산) · 육각 껍질이 확 부풀며 흩어지고(가산 0.6) · 소프트 블루 충격파(0.4 → 1.6 × 크기, 200ms, 가산 0.85) ·
        /// 수정 조각 8개(보통 6, 낮음 4)가 300~520px/s로 튀어 바닥에 튄다(가산 0.3). chip = 적 보호막: "깨짐!" 글자(34pt, 30px 떠오르며 0.6초) +
        /// 킥(hitDirection, 2px · 보스 3px, 보호막 채널). hitDirection이 없으면 수선점에서 바깥쪽.
        /// target = 보호막이 깨진 적: 깨는 일격이 날아가는(delay) 동안 적이 걸어가도 거품이 터지는 몸 자리에서 — 코어 별·껍질·충격파는 몸을 따라가고,
        /// 조각은 터지는 순간의 자리에서 제 길로, "깨짐!"은 뜨는 순간 그 적 옆에 (EnemyView.ShieldBreak(delay)와 같은 자리)
        /// </summary>
        public void ShieldShatter(Vector2 center, float size, Color color, float delay, Vector2 hitDirection, bool boss, bool chip, EnemyView target = null)
        {
            if (!_ready) Prepare();
            float unit = UnitScale;
            int core = CoreStar(center, Color.white, size * 0.75f, size * 0.5f, 0.066f, delay, 0.9f);
            AnchorTo(core, target);
            var hex = Motif(VfxMotif.HexRing);
            int shell = Spawn(hex, BattleEase.WithAlpha(Color.Lerp(color, Color.white, 0.4f), 0.9f), center, size * 0.95f, size * 1.45f, 0.18f, delay);
            if (shell >= 0)
            {
                ref var p = ref _particles[shell];
                p.Rotation = 0f;
                p.Add = 0.6f;
                p.FadeIn = 0f;
                p.FadeOut = 0.3f;
                p.FadeKind = FadeInQuad;
            }
            AnchorTo(shell, target);
            int ring = Shockwave(center, color, size * 0.4f, size * 1.6f, 0.2f, delay, 0.85f, 0.8f);
            AnchorTo(ring, target);
            var shard = Motif(VfxMotif.Shard);
            int count = _quality == FxQuality.Low ? 4 : _quality == FxQuality.Medium ? 6 : 8;
            float floor = center.y - size * 0.55f;
            for (int i = 0; i < count; i++)
            {
                if (i >= 2 && !Allow(FxPriority.Sparkle)) break;
                float angle = i * Mathf.PI * 2f / count + Random.Range(-0.3f, 0.3f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.35f);
                float shardSize = Random.Range(14f, 22f) * unit;
                int index = Spawn(shard, i % 2 == 0 ? color : Color.white, center + dir * size * 0.2f, shardSize, shardSize * 0.85f, Random.Range(0.42f, 0.5f), delay,
                    sparkle: i >= 2);
                if (index < 0) continue;
                ref var q = ref _particles[index];
                q.Motion = Motion.Bounce;
                q.P1 = dir.normalized * (Random.Range(300f, 520f) * unit);
                q.P2 = new Vector2(floor, 900f);
                q.P3 = new Vector2(0.35f, 0f);
                q.Spin = Random.Range(-540f, 540f);
                q.Add = 0.3f;
                q.FadeIn = 0.01f;
                q.FadeOut = 0.6f;
                AnchorUntilShown(index, target, delay);
            }
            if (!chip) return;
            // "깨짐!"은 수선점 쪽 옆에 (바깥으로 흐르는 피해 숫자와 겹치지 않게)
            var shop = _shop != null ? _shop.FieldPosition : Vector2.zero;
            float inward = center.x >= shop.x ? -1f : 1f;
            int label = Chip(center + new Vector2(inward * (size * 0.55f + 24f), size * 0.2f), ShieldBreakText, UITheme.SoftBlue, 34f / NumberFontSize, 30f, 0.6f, delay);
            AnchorNumber(label, target);
            var kick = hitDirection.sqrMagnitude > 1e-4f ? hitDirection : center - shop;
            Kick(kick, boss ? 3f : HitFeel.KickShield, KickChannel.Shield, delay);
        }

        /// <summary>잔향이 회복: 순한 민트 빛(가산 0.5) + 하트가 둥실 + 민트 숫자 (delay: 시전자에게서 선물이 날아가는 중이면 닿는 순간까지)</summary>
        public void EchoHealed(EchoView echo, float amount, float delay = 0f)
        {
            if (echo == null) return;
            if (!_ready) Prepare();
            var body = echo.BodyPoint;
            int glow = SkillLightGlow(body, UITheme.Positive, 40f, 120f, 0.4f, delay, 0.5f);
            AnchorTo(glow, echo);
            HeartsAt(body, echo, 3, delay);
            if (amount >= 1f) HealNumber(echo.Id, echo.HeadPoint + Vector2.up * 10f, amount, delay);
        }

        void HeartsAt(Vector2 at, EchoView echo, int count, float delay = 0f)
        {
            var heart = Motif(VfxMotif.Heart);
            for (int i = 0; i < count; i++)
            {
                int index = Drift(heart, i % 2 == 0 ? HealPink : UITheme.Positive, at + new Vector2(Random.Range(-22f, 22f), Random.Range(-10f, 14f)),
                    Vector2.up * Random.Range(50f, 80f), Vector2.zero, Random.Range(20f, 26f), 12f, Random.Range(0.7f, 0.9f), delay + i * 0.08f);
                if (index < 0) continue;
                _particles[index].Spin = Random.Range(-20f, 20f);
                _particles[index].Rotation = Random.Range(-12f, 12f);
                _particles[index].Wobble = 7f;
                _particles[index].FadeOut = 0.55f;
                AnchorTo(index, echo);
            }
        }

        /// <summary>
        /// 치명타 겹침 v2 (보이지 않는 적의 치명타 — 보이는 적은 ally CritImpact): 금빛 후광 · 크림 코어 별 · 가로 섬광 · 크림 고리 → 50ms 뒤 금빛 별 고리 ·
        /// 사방 빛줄기 · 떠오르는 별 넷 (delay: 투사체가 닿는 순간)
        /// </summary>
        public void CritBurst(Vector2 point, bool boss, float delay = 0f)
        {
            if (!_ready) Prepare();
            float size = 200f * UnitScale * (boss ? 1.4f : 1f);
            Halo(point, CritGold, size * 2.2f, 0.2f, delay, 0.3f);
            CoreStar(point, JuiceColors.CoreCream, size * 0.75f, size * 0.5f, 0.083f, delay);
            Flare(point, Color.Lerp(UITheme.Gold, JuiceColors.CoreCream, 0.5f), size * 2.2f, size * 0.18f, Random.Range(-15f, 15f), 0.1f, delay, 0.8f);
            Shockwave(point, JuiceColors.CoreCream, size * 0.3f, size * 1.5f, 0.22f, delay, 0.9f, 0.8f);
            Shockwave(point, UITheme.Gold, size * 0.3f, size * 1.5f, 0.22f, delay + 0.05f, 0.9f, 0.8f, 1f, false, StarRingSprite);
            Streaks(point, Vector2.up, 8, 360f, 600f, 1000f, BattleEase.WithAlpha(JuiceColors.TowardCream(UITheme.Gold, 0.5f), 0.9f), delay, 26f, 6f);
            var star = Motif(VfxMotif.Star);
            for (int i = 0; i < 4; i++)
            {
                if (i >= 2 && !Allow(FxPriority.Sparkle)) break;
                float angle = i * Mathf.PI * 0.5f + Random.Range(-0.35f, 0.35f) + Mathf.PI * 0.25f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int index = Drift(star, i % 2 == 0 ? UITheme.Gold : UITheme.Cream, point, dir * Random.Range(70f, 110f), Vector2.up * 30f,
                    Random.Range(20f, 28f), 8f, Random.Range(0.42f, 0.55f), delay);
                if (index < 0) continue;
                _particles[index].Spin = Random.Range(-360f, 360f);
                _particles[index].Add = 0.5f;
            }
        }

        /// <summary>엉킨 실뭉치가 풀려 갈라짐: 실 가닥이 새 적 쪽으로 튄다 (적 기믹 — 차분하게)</summary>
        public void SplitPop(Vector2 from, Vector2 to, Color color)
        {
            if (!_ready) Prepare();
            Flash(from, _glow, BattleEase.WithAlpha(color, 0.7f), 40f, 140f, 0.35f);
            var control = (from + to) * 0.5f + Vector2.up * 70f;
            int strand = Curve(_trail, BattleEase.WithAlpha(color, 0.95f), from, control, to, 70f, 40f, 0.3f, 0f);
            if (strand >= 0)
            {
                _particles[strand].AlignToPath = true;
                _particles[strand].Aspect = 0.3f;
            }
            var yarn = Motif(VfxMotif.Yarn);
            int ball = Curve(yarn, color, from, control, to, 34f, 30f, 0.3f, 0f);
            if (ball >= 0) _particles[ball].Spin = 540f;
            for (int i = 0; i < 4; i++)
            {
                var dir = RandomDirection();
                Drift(Puff, BattleEase.WithAlpha(UITheme.Lavender, 0.5f), from, dir * 40f, Vector2.up * 10f, 20f, 50f, 0.4f);
            }
        }

        /// <summary>숨어 있던 적이 드러남: 크림빛(가산)과 등불색 고리가 퍼지고 별빛이 흩날린다</summary>
        public void RevealFlash(EnemyView enemy, float delay = 0f)
        {
            if (enemy == null) return;
            if (!_ready) Prepare();
            var body = enemy.HitPoint;
            int glow = SkillLightGlow(body, UITheme.Cream, 40f, 170f, 0.45f, delay, 0.7f);
            AnchorTo(glow, enemy);
            int ring = Ring(enemy.Rect.anchoredPosition, UITheme.Lamp, 20f, 150f, 0.5f, GroundAspect, delay);
            if (ring >= 0) _particles[ring].Add = 0.5f;
            AnchorTo(ring, enemy);
            SkillLightSparkles(body, 50f, UITheme.Cream, 6, delay);
        }

        /// <summary>분노 (적 — 가산·금색 없이 차분하게): 붉은 기운이 터지고 붉은 고리 둘이 퍼지며 그림자가 피어오른다</summary>
        public void EnrageBurst(EnemyView enemy)
        {
            if (enemy == null) return;
            if (!_ready) Prepare();
            float scale = enemy.IsBoss ? 2f : 1f;
            var body = enemy.HitPoint;
            int glow = Flash(body, _glow, BattleEase.WithAlpha(EnrageRed, 0.85f), 60f * scale, 220f * scale, 0.5f);
            AnchorTo(glow, enemy);
            int ring = Ring(enemy.Rect.anchoredPosition, EnrageRed, 30f * scale, 180f * scale, 0.45f, GroundAspect);
            AnchorTo(ring, enemy);
            int pulse = Shockwave(body, BattleEase.WithAlpha(EnrageRed, 0.75f), 40f * scale, 200f * scale, 0.32f, 0.08f, 0f, 0.75f);
            AnchorTo(pulse, enemy);
            for (int i = 0; i < 6; i++)
            {
                var dir = RandomDirection();
                int index = Drift(Puff, BattleEase.WithAlpha(ShadowPurple, 0.6f), body + dir * 20f * scale, new Vector2(dir.x * 40f, Mathf.Abs(dir.y) * 50f + 30f) * scale,
                    Vector2.up * 30f, 30f * scale, 80f * scale, 0.6f, i * 0.03f);
                AnchorTo(index, enemy);
            }
        }

        /// <summary>작은 양말 토큰 소환: 바닥 마법진이 돌고 빛(가산)과 별빛이 모인다</summary>
        public void TokenPop(Vector2 feet, Color color, Sprite circle, bool isArt)
        {
            if (!_ready) Prepare();
            int decal = Spawn(circle != null ? circle : Motif(VfxMotif.MagicCircle), BattleEase.WithAlpha(isArt ? Color.white : color, 0.9f), feet, 70f, 96f, 0.8f, 0f, true);
            if (decal >= 0)
            {
                _particles[decal].Curve = SizeCurve.Pop;
                _particles[decal].Aspect = GroundAspect;
                _particles[decal].Rotation = 0f;
                _particles[decal].Spin = 60f;
                _particles[decal].FadeOut = 0.6f;
            }
            SkillLightGlow(feet + Vector2.up * 30f, color, 30f, 110f, 0.35f, 0f, 0.7f);
            SkillLightSparkles(feet + Vector2.up * 30f, 30f, UITheme.Cream, 4, 0.05f);
        }

        /// <summary>
        /// 불빛을 빼앗김: 수선점 불빛이 어두운 실처럼 적에게 빨려 가고, 불 밝히기 버튼(button, 전장 로컬 — 전장 아래 띠)에서도
        /// 그림자 줄기가 불빛 알갱이를 물고 그 적에게 감긴다 (ATK-03 큰그림자 LampDrain). button이 0이면 버튼 줄기는 없다
        /// </summary>
        public void LampDrain(Vector2 shop, Vector2 to, bool hasTarget, Vector2 button = default)
        {
            if (!_ready) Prepare();
            if (hasTarget && button != Vector2.zero) DrainFromButton(button, to);
            Flash(shop + Vector2.up * 40f, Puff, BattleEase.WithAlpha(ShadowPurple, 0.7f), 80f, 200f, 0.5f);
            for (int i = 0; i < 8; i++)
            {
                var start = shop + new Vector2(Random.Range(-40f, 40f), Random.Range(20f, 80f));
                var end = hasTarget ? to + new Vector2(Random.Range(-10f, 10f), Random.Range(-10f, 10f)) : start + new Vector2(Random.Range(-60f, 60f), Random.Range(80f, 140f));
                var control = (start + end) * 0.5f + new Vector2(Random.Range(-60f, 60f), Random.Range(40f, 90f));
                bool isTrail = i % 2 == 0;
                int index = Curve(isTrail ? _trail : _sparkle, isTrail ? BattleEase.WithAlpha(UITheme.Lamp, 0.8f) : ShadowPurple, start, control, end,
                    isTrail ? 60f : 22f, isTrail ? 30f : 8f, Random.Range(0.45f, 0.6f), i * 0.04f);
                if (index < 0) continue;
                _particles[index].AlignToPath = isTrail;
                _particles[index].Aspect = isTrail ? 0.3f : 1f;
            }
        }

        /// <summary>불 밝히기 버튼 쪽(전장 아래 가장자리)에서 그림자 줄기가 불빛 알갱이를 끌고 적에게 감긴다</summary>
        void DrainFromButton(Vector2 button, Vector2 to)
        {
            // 버튼은 전장 아래 띠에 있으므로 전장 아래 가장자리 바로 안쪽에서 시작한다 (전장 마스크에 잘리지 않게)
            var start = new Vector2(Mathf.Clamp(button.x, -FieldHalfWidth + 30f, FieldHalfWidth - 30f), FieldBottom + 16f);
            for (int i = 0; i < 6; i++)
            {
                var from = start + new Vector2(Random.Range(-30f, 30f), Random.Range(0f, 20f));
                var end = to + new Vector2(Random.Range(-12f, 12f), Random.Range(-10f, 10f));
                var control = (from + end) * 0.5f + new Vector2(Random.Range(-90f, 90f), Random.Range(30f, 90f));
                bool strand = i % 2 == 0;
                int index = Curve(strand ? _trail : _sparkle, strand ? BattleEase.WithAlpha(ShadowPurple, 0.85f) : UITheme.Lamp, from, control, end,
                    strand ? 70f : 20f, strand ? 36f : 8f, Random.Range(0.5f, 0.65f), i * 0.05f);
                if (index < 0) continue;
                _particles[index].AlignToPath = strand;
                _particles[index].Aspect = strand ? 0.24f : 1f;
                if (!strand) _particles[index].Spin = 300f;
            }
        }

        // ───────────── 헬퍼 ─────────────

        /// <summary>그림이 통 튀어 오르며 나타났다가 스미는 연출 (머리 위 표식 등)</summary>
        int PopAt(Sprite sprite, Color color, Vector2 position, float size, float life, float delay = 0f)
        {
            int index = Spawn(sprite != null ? sprite : _sparkle, color, position, size, size * 1.1f, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Pop;
            p.Rotation = 0f;
            p.FadeIn = 0.01f;
            p.FadeOut = 0.6f;
            return index;
        }

        /// <summary>이미 만든 그림 입자를 첫 프레임이 가장 큰 곡선으로 (Size0 = 처음, Size1 = 자리 — 33ms 버팀, 70ms에 자리, 그 뒤 그대로)</summary>
        void SkillImpactCurve(int index, float fadeOut)
        {
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Impact;
            p.Hold = 0.033f;
            p.Settle = 0.07f;
            p.EndScale = 1f;
            p.Rotation = 0f;
            p.FadeIn = 0f;
            p.FadeOut = fadeOut;
            p.Rect.sizeDelta = new Vector2(p.Size0, p.Size0);
        }

        /// <summary>
        /// 스킬 투사체가 닿은 자리 v2: 빛(가산) 위에 같은 그림이 1.35배로 쾅 → 70ms에 자리(Impact) · 코어 별 0.6× · 바닥 고리(가산 0.7) · 반짝 (투사체 Arrive가 부른다)
        /// </summary>
        void SkillImpactAt(Vector2 at, Sprite sprite, Color tint, Color color, float size)
        {
            float settled = size * 1.3f;
            SkillLightGlow(at, Color.Lerp(color, Color.white, 0.4f), settled * 0.6f, settled * 1.5f, 0.28f, 0f, 0.6f);
            int pop = ImpactArt(sprite, BattleEase.WithAlpha(tint, 1f), at, settled, HitFeel.Feel(HitTier.Skill), 0.42f * PopScale, 0f, 1f);
            if (pop >= 0) _particles[pop].Rotation = Random.Range(-15f, 15f);
            CoreStar(at, JuiceColors.TowardCream(color, 0.6f), settled * 0.6f, settled * 0.4f, 0.066f, 0f, 0.9f);
            Shockwave(at + Vector2.down * 36f, color, settled * 0.2f, settled * 1.4f, 0.3f, 0f, 0.7f, 0.75f, GroundAspect);
            SkillLightSparkles(at, 40f, color, 5, 0f);
        }

        /// <summary>아군 빛 번짐 (fx_glow_core, 가산 add, 세기 intensity): size0 → size1, 30%부터 InQuad로 꺼진다. 입자 칸 번호</summary>
        int SkillLightGlow(Vector2 at, Color color, float size0, float size1, float life, float delay, float intensity, float add = 1f)
        {
            int index = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(color, Mathf.Clamp01(intensity)), at, size0, size1, life, delay, add);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Curve = SizeCurve.Out;
            p.FadeOut = 0.3f;
            p.FadeKind = FadeInQuad;
            return index;
        }

        /// <summary>반짝 하나 (가산 0.4, 0.3초): 그림 모서리에서 별빛이 톡 튀었다 스민다</summary>
        int SkillGlint(Vector2 at, float size, float delay)
        {
            int index = Spawn(_sparkle, JuiceColors.CoreCream, at, size, size * 0.4f, 0.3f, delay, sparkle: true);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = 0.4f;
            p.Curve = SizeCurve.Back;
            p.Rotation = 0f;
            p.Spin = 160f;
            p.FadeIn = 0.02f;
            p.FadeOut = 0.5f;
            return index;
        }

        /// <summary>돔 광택 훑기: vfx_gloss_shine이 돔 왼쪽 위 → 오른쪽 아래로 160ms에 지나간다 (가산 0.6)</summary>
        int SkillGlossSweep(Vector2 center, float width, float delay)
        {
            var from = center + new Vector2(-width * 0.38f, width * 0.12f);
            var to = center + new Vector2(width * 0.38f, -width * 0.04f);
            float size = Mathf.Clamp(width * 0.32f, 48f, 96f);
            int index = Line(GlossSprite(), BattleEase.WithAlpha(JuiceColors.CoreCream, 0.95f), from, to, size, size * 0.85f, 0.16f, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = 0.6f;
            p.Rotation = -20f;
            p.FadeIn = 0.25f;
            p.FadeOut = 0.55f;
            return index;
        }

        /// <summary>
        /// 흩날리는 별빛 (알파 — 적 공격처럼 차분해야 하는 곳도 함께 쓴다). 아군 빛 연출은 SkillLightSparkles
        /// </summary>
        void Sparkles(Vector2 center, float spread, Color color, int count, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                Drift(_sparkle, i % 2 == 0 ? color : UITheme.Cream, center + dir * spread * 0.3f, dir * spread * Random.Range(0.8f, 1.5f), Vector2.down * 40f,
                    Random.Range(14f, 24f), 4f, Random.Range(0.45f, 0.7f), delay + i * 0.02f);
            }
        }

        /// <summary>흩날리는 빛 별빛 (가산 0.7 — 아군 스킬·회복·드러냄)</summary>
        void SkillLightSparkles(Vector2 center, float spread, Color color, int count, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                if (i >= 2 && !Allow(FxPriority.Sparkle)) break;
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                int index = Drift(_sparkle, i % 2 == 0 ? color : UITheme.Cream, center + dir * spread * 0.3f, dir * spread * Random.Range(0.8f, 1.5f), Vector2.down * 40f,
                    Random.Range(14f, 24f), 4f, Random.Range(0.45f, 0.7f), delay + i * 0.02f);
                if (index >= 0) _particles[index].Add = 0.7f;
            }
        }

        void AnchorAll(int index, in SkillFxRequest r)
        {
            if (r.AnchorEcho != null) AnchorTo(index, r.AnchorEcho);
            else if (r.Target != null) AnchorTo(index, r.Target);
        }

        Sprite MainSprite(in SkillFxRequest r) => r.Sprite != null ? r.Sprite : Motif(r.Look.Fallback);

        static Color Tint(in SkillFxRequest r) => r.IsArt && r.Sprite != null ? Color.white : r.Look.Main;

        /// <summary>너무 작거나 큰 반경을 보기 좋게 자른다 (세로/가로 비율 유지)</summary>
        Vector2 ClampRadius(Vector2 radius)
        {
            float ppu = _mapper != null ? _mapper.Ppu : 205f;
            float x = Mathf.Clamp(radius.x, ppu * 0.28f, ppu * 2.4f);
            float ratio = radius.x > 0.01f ? radius.y / radius.x : GroundAspect;
            return new Vector2(x, x * Mathf.Clamp(ratio, 0.3f, 1f));
        }

        static Vector2 RandomInEllipse(Vector2 radius)
        {
            var point = Random.insideUnitCircle;
            return new Vector2(point.x * radius.x, point.y * radius.y);
        }
    }
}
