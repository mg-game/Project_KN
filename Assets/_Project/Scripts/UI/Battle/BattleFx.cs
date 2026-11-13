using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 연출 전담: 빛 입자, 화면 번쩍임·어둡게·흔들림(trauma)·킥·줌, 불 밝히기 빛 고리, 밤거리 반딧불.
    /// 기본 공격(계보 투사체·명중, 잔향이 베기)은 BattleFx.Attack.cs, 피해 숫자 문법은 BattleFx.Numbers.cs,
    /// 스킬/상태/기믹 연출은 BattleFx.Skills.cs, 적 공격·수선점 피격·처치 4갈래·등장·보상 단추는 BattleFx.Enemy.cs, 지원·힐 맥박은 BattleFx.Pulse.cs,
    /// 손맛 기본 도구(층 쌓기 · 화면 · 밀도 관리 · 이음매)는 BattleFx.Juice.cs (모두 같은 클래스).
    ///
    /// ══════ FX-JUICE 코어 API (FX-JUICE-SPEC §3.6 — 정확한 시그니처, 구현은 BattleFx.Juice.cs) ══════
    /// 좌표 = 전장 로컬 px (BattleFieldMapper.WorldToField 결과, 입자 레이어 가운데 기준). delay = 그 순간까지 남은 연출 시계(초).
    /// 화면 효과의 지속 시간은 실제 시간. 색 알파 = 불투명도 (add &gt; 0이면 '빛 세기'). add = 가산 비율 0(보통 알파) ~ 1(순수 빛).
    /// 모든 기본 도구는 품질 예산·층(별빛 → 곁들임 → 꼬리 → 코어 순서로 버림)·번쩍임 줄이기를 지키고, 새 그림이 없으면 안에서 예전 그림으로 대신한다.
    /// 반환 int = 입자 칸 번호 (못 만들면 −1) — 같은 클래스의 연출은 _particles[index]로 더 다듬을 수 있다.
    ///   설정·배치:  FxQuality Quality · bool ReduceFlashOn · static bool ReduceFlashNow · float ShakeScaleNow · float SafeTop · JuiceArt Juice ·
    ///              float Clock · float RealClock · int MaxNumbersNow · bool PremulOn · FxDevSettings DevOverride { get; set; } · FxFrameStats Stats
    ///   기본 도구:  int SpawnFx(Sprite s, Color c, Vector2 at, float size0, float size1, float life, float delay, float add = 0f, bool ground = false)
    ///              int ImpactArt(Sprite s, Color c, Vector2 at, float settled, in TierFeel f, float life, float delay, float endScale = 0.92f, float add = 0f)
    ///              int CoreStar(Vector2 at, Color tint, float size0, float size1, float life, float delay, float intensity = 1f)
    ///              int Halo(Vector2 at, Color tint, float size, float life, float delay, float intensity)
    ///              int InkBacking(Vector2 at, float size, float alpha, float life, float delay)
    ///              int Shockwave(Vector2 at, Color tint, float from, float to, float life, float delay, float add = 0.85f, float intensity = 0.8f,
    ///                            float aspect = 1f, bool ground = false, Sprite sprite = null)
    ///              void Streaks(Vector2 at, Vector2 dir, int count, float coneDeg, float speedMin, float speedMax, Color tint, float delay,
    ///                           float length = 22f, float thickness = 5f, float add = 1f, EnemyView anchor = null, Sprite sprite = null)
    ///              int Streak(Vector2 at, Vector2 velocity, float length, float thickness, float life, Color tint, float delay, float add = 1f,
    ///                         float drag = 9f, float gravity = 0f, Sprite sprite = null)
    ///              int Flare(Vector2 at, Color tint, float length, float thickness, float angleDeg, float life, float delay, float intensity = 0.9f)
    ///              int Speedlines(Vector2 at, Color tint, float size, float life, float delay, float intensity = 0.5f)
    ///              void Confetti(Vector2 at, ConfettiSet set, int count, float power, float delay)
    ///              void Twinkles(Vector2 at, float spread, int count, Color tint, float delay)
    ///              int Decal(Vector2 groundAt, Sprite s, Color tint, float size, float delay)
    ///              void AnchorFx(int index, EnemyView enemy) · void AnchorFx(int index, EchoView echo) · void AnchorFxUntilShown(int index, EnemyView enemy, float delay)
    ///   화면:      void Kick(Vector2 dir, float px, KickChannel channel, float delay = 0f)
    ///              void ZoomPunch(float peak, Vector2 pivotField, float delay = 0f, bool force = false)
    ///              void FieldDim(float alpha, float fadeIn, float hold, float fadeOut, float delay = 0f)
    ///              void LightFlash(Color color, float intensity, float duration, float delay = 0f, bool force = false)
    ///              void ImpactFrame(Graphic body, float delay, bool misprint, bool force = false)
    ///              void Misprint(Graphic body, float delay, float offsetPx, float life, float alpha)
    ///              void AddSkillTrauma(float amount, Vector2 direction = default) · void Haptic(HapticKind kind, float delay = 0f)
    ///              (그대로) ScreenFlash · FieldFlash · AddTrauma · AddShopTrauma · GlobalHitStop · BossKillMoment(delay[, point, body])
    ///   밀도 관리:  bool TryFullImpact(int targetId, float delay) (+ bool RhythmAccent, int RhythmChain) · int Density(float delay) · void NoteHit(float delay) ·
    ///              float BigBurstDelay(float delay) (−1 = 작게) · bool TryKillBurst(float delay) · int KillStreak · int MultiKill · Vector2 MultiKillCentroid ·
    ///              Vector2 ClampToSafe(Vector2 at, float radius) · bool TrySpotlight() · float SinceBossMoment · float SinceLamp
    ///   이음매:    void NoteKill(int enemyId, Vector2 at, float delay, bool boss, HitTier byTier) → partial OnKillNoted(...) (show)
    ///              void SkillHit(in SkillHitFx r) → 코어 기본 명중 → partial OnSkillHit(SkillHitFx r) (show)
    /// 수치는 HitFeel(등급 표 Feel / ForBoss / ForEcho / ForGuarded, 예산, 흔들림), 곡선은 JuiceEase, 색은 JuiceColors, 품질 문은 JuiceGates.
    ///   같은 클래스 안 도구 (partial 연출이 _particles[index]로 다듬을 때): Particle.Add(가산 0~1) · Hold/Settle/EndScale(SizeCurve.Impact) ·
    ///   Drag/Gravity(Motion.Streak = 속도 방향으로 눕는 빛줄기, Motion.Ballistic = 도는 색종이) · Warp(Motion.Shot 끝에서 쏙 꽂힘, 0.25) ·
    ///   FadeKind(FadeInQuad = 오래 버티다 빨리 꺼짐) · Pulse/PulseHz(반짝 맥동) · Frames/FrameRate(플립북) · SizeCurve.Quart(고리) ·
    ///   Arrival.ShopGlint(기억 한 톨이 문에 닿으면 코어 별 + 수선점 맥동) · Allow(FxPriority.Sparkle/Extra/Trail/Core) — 층 예산 (품질별 프레임 상한).
    ///   색 알파 규칙: Add 0이면 불투명도, Add &gt; 0이면 빛 세기 — 가산 층을 흐리게 하려면 알파(세기)를 줄인다.
    /// ══════════════════════════════════════════════════════════════════════════════════════════
    ///
    /// 입자는 미리 만든 Image 풀을 구조체 배열로 돌려 쓴다 (매 프레임 할당 없음). 머티리얼 하나(UI/FxPremul, 미리 곱한 알파)로 보통·가산 입자가 함께
    /// 배치된다 — 셰이더가 없으면 보통 알파로 그린다(가산 층은 반투명 빛으로). 만든 순서 = 그리는 순서 (Spawn이 맨 뒤로 보낸다).
    /// 입자는 액터 위 레이어와 바닥 레이어(장판·잉크 받침·자국, 액터 아래) 두 묶음 (각 레이어는 하위 Canvas). 바닥 입자는 바닥 예산이 차면
    /// 만들지 않는다 (위쪽 레이어로 넘기면 맞은 적·보스·수선점을 덮는다).
    /// 메시를 매 프레임 다시 만들지 않게: 처음 보이는 프레임에 sizeDelta를 SizeBase로 한 번 고정하고 그 뒤 크기는 localScale(좌우 뒤집기 포함),
    /// 투명도·빛 세기는 CanvasRenderer 색으로 곱한다 (정점 색 = 색 + 가산 비율, 바뀔 때만). 그래서 Spawn 뒤 같은 호출 안에서만
    /// p.Rect.sizeDelta(첫 프레임 크기)·localScale(FaceArt 뒤집기)을 써도 되고, 그 뒤로는 Size0/Size1/Aspect를 바꾼다.
    /// 예산: 별빛 층(Drift · Streak · 색종이 · 여운 반짝)은 마지막 HitFeel.HeroReserve칸을 남긴다 — 투사체 머리·명중 그림·처치 꽃·코어 별 몫.
    /// 번쩍임은 세 가지: 빛(스크린 블렌드 — 밝힌다) / 틴트(알파 — 위험·기믹) / 어둡게(FieldDim, Fx 아래 — 연출만 밝게 남는다). 전장 전체 번쩍임은 1초에 하나.
    /// 흔들림은 trauma(잡음) + 킥(방향 한 번) + 줌 펀치 — 전장(ShakeRoot)만, 설정 배율 0이면 전혀 움직이지 않는다.
    /// 시계: 입자·숫자는 연출 시계(_clock, 전체 정지·느린 화면이 걸린다), 화면 효과는 실제 시간(_realClock). Time.time은 쓰지 않는다 (캡처가 Advance를 직접 부른다).
    /// </summary>
    [DefaultExecutionOrder(50)] // perf PF-2 F3: BattleView(-50)가 액터를 놓은 뒤 — 적 · 잔향이를 따라가는 입자 · 숫자가 같은 프레임 자리를 읽는다
    public partial class BattleFx : MonoBehaviour
    {
        enum Motion : byte
        {
            /// <summary>P0에서 P1만큼 미끄러지고 P2(중력 오프셋)만큼 휜다</summary>
            Drift,
            /// <summary>P0 → P1(제어점) → P2 2차 곡선 (진행 SmoothStep, Particle.EaseCubic이면 InOutCubic — 기억 한 톨처럼 천천히 떠나 빠르게 날고 문 앞에서 내려앉는다)</summary>
            Curve,
            /// <summary>P0에서 대상 적을 따라가 맞힌다 (P1.y = 포물선 높이, P2 = 마지막으로 본 위치)</summary>
            Homing,
            /// <summary>P0 둘레를 돈다 (P1 = 가로/세로 반지름, P2 = 시작 각 / 각속도(rad/s), P3 = 반지름 배율 시작/끝)</summary>
            Orbit,
            /// <summary>P0 → P2 일정한 빠르기로 곧게 (레인을 꿰뚫는 바늘: 지나가는 순간을 계산할 수 있게)</summary>
            Line,
            /// <summary>
            /// 기본 공격 투사체: P0 → 대상(P2, 따라감). P1 = (호 높이, 좌우 살랑임 폭), P3 = (살랑임 횟수, 통통 튀는 횟수).
            /// 살랑임은 닿을수록 잦아들어 끝이 명중점에 정확히 닿는다. Warp &gt; 0이면 진행도 k' = k·(1 − Warp + Warp·k) — 끝에서 대상에 쏙 꽂힌다
            /// </summary>
            Shot,
            /// <summary>바닥에 한 번 튀는 작은 조각: P0 시작, P1 처음 속도(px/s), P2 = (바닥 y, 중력 px/s²), P3.x = 튀는 탄성</summary>
            Bounce,
            /// <summary>
            /// 빛줄기: P0 시작, P1 처음 속도(px/s), Drag(1/s)로 느려지고 Gravity(px/s², 아래로)로 휜다. 늘 속도 방향으로 눕고
            /// 가로 길이 = 크기 × (1 + |v|/600) (빠를수록 길게), 세로 = Height0 → Height1
            /// </summary>
            Streak,
            /// <summary>색종이: Streak와 같은 길(속도·끌림·중력)이지만 눕지 않고 Spin으로 돈다 (Wobble로 팔랑임)</summary>
            Ballistic,
        }

        enum Arrival : byte
        {
            None,
            /// <summary>수선점에 빛이 스며듦 (수선점 맥동)</summary>
            ShopAbsorb,
            /// <summary>투사체 명중 번쩍임</summary>
            Impact,
            /// <summary>스킬 투사체 명중: 같은 그림이 톡 터지며 별빛이 튄다</summary>
            SkillImpact,
            /// <summary>빗방울이 바닥에 닿아 작은 물결</summary>
            Splash,
            /// <summary>기억 한 톨이 수선점 문에 닿음: 문에 코어 별 반짝(40px, 60ms, 가산) + 수선점 맥동 0.22</summary>
            ShopGlint,
        }

        enum SizeCurve : byte
        {
            /// <summary>Size0 → Size1, 빠르게 시작해 천천히 (OutCubic)</summary>
            Out,
            /// <summary>0에서 Size0까지 통 튀어 오른 뒤 Size1로 천천히</summary>
            Pop,
            /// <summary>처음 30% 안에 Size1까지 (빛줄기가 뻗는 순간)</summary>
            Snap,
            Linear,
            /// <summary>Size0에서 Size1을 살짝 넘었다가 돌아온다 (명중 그림이 톡 튀어 오름, 앞 45%)</summary>
            Back,
            /// <summary>
            /// 첫 프레임이 가장 크다 (JuiceEase.ImpactSize): Hold 동안 Size0(= 자리 크기 × FirstFrame), Settle 동안 Size1로, 그 뒤 Size1 × EndScale로
            /// </summary>
            Impact,
            /// <summary>Size0 → Size1 OutQuart (충격 고리 — 아주 빠르게 퍼지고 길게 잦아든다)</summary>
            Quart,
        }

        /// <summary>사라지는 모양: Smooth = 예전 SmoothStep, InQuad = 오래 버티다 빨리 꺼진다 (빛 층)</summary>
        const byte FadeSmooth = 0;
        const byte FadeInQuad = 1;
        /// <summary>입자 표식 (살아 있는 후광·자국 수를 센다)</summary>
        const byte FlagHalo = 1;
        const byte FlagDecal = 2;

        struct Particle
        {
            public RectTransform Rect;
            public Image Image;
            public bool Alive;
            /// <summary>바닥 레이어 입자 (반납할 묶음 구분)</summary>
            public bool Ground;
            public Motion Motion;
            public Arrival Arrival;
            public SizeCurve Curve;
            public float Delay;
            public float Age;
            public float Life;
            public Vector2 P0;
            public Vector2 P1;
            public Vector2 P2;
            public Vector2 P3;
            public float Size0;
            public float Size1;
            /// <summary>세로/가로 비율 (빛 꼬리 0.25, 바닥 고리 = 눌림)</summary>
            public float Aspect;
            /// <summary>끝날 때의 세로/가로 비율 (음수 = Aspect 유지)</summary>
            public float AspectEnd;
            /// <summary>0보다 크면 세로 길이를 픽셀로 고정 (빛줄기 두께)</summary>
            public float Height0;
            public float Height1;
            public float Rotation;
            public float Spin;
            public bool AlignToPath;
            public float FadeIn;
            /// <summary>사라지기 시작하는 진행도 (음수 = 움직임별 기본값)</summary>
            public float FadeOut;
            /// <summary>좌우로 살랑이는 폭(px)</summary>
            public float Wobble;
            public float Seed;
            /// <summary>색 (알파 = 불투명도, Add &gt; 0이면 빛 세기)</summary>
            public Color Color;
            /// <summary>명중 시 튀는 별빛 색</summary>
            public Color Color2;
            public EnemyView Target;
            public int TargetId;
            /// <summary>따라다닐 액터 (머리 위 표식 등). 다른 개체로 재사용되면 그 자리에 멈춘다</summary>
            public EnemyView AnchorEnemy;
            public EchoView AnchorEcho;
            public int AnchorId;
            public Vector2 AnchorStart;
            public Vector2 AnchorOffset;
            /// <summary>지연(Delay) 동안만 따라가다가 나타나는 순간의 자리에서 제 길로 (튀는 단추·촛농·물방울·하트·음표·명중 별빛)</summary>
            public bool AnchorUntilShown;
            /// <summary>그림 반쪽만 (0 = 전체, 1 = 왼쪽, 2 = 오른쪽 — 나방 날개가 좌우로 갈라질 때). 재사용할 때 Simple로 되돌린다</summary>
            public byte Half;

            // ── FX-JUICE §3.3 ──
            /// <summary>가산 비율 0(보통 알파) ~ 1(순수 빛)</summary>
            public float Add;
            /// <summary>SizeCurve.Impact: 첫 프레임 크기 버팀 / 줄어드는 시간 / 끝 배율</summary>
            public float Hold;
            public float Settle;
            public float EndScale;
            /// <summary>Motion.Streak / Ballistic: 끌림(1/s) · 중력(px/s², 아래로)</summary>
            public float Drag;
            public float Gravity;
            /// <summary>Motion.Shot 진행도 휨 (0.25 = 끝에서 쏙 꽂힘)</summary>
            public float Warp;
            /// <summary>사라지는 모양 (FadeSmooth / FadeInQuad)</summary>
            public byte FadeKind;
            /// <summary>FlagHalo / FlagDecal</summary>
            public byte Flags;
            /// <summary>반짝임 크기 맥동 (1 ± Pulse, PulseHz) — 여운 반짝이</summary>
            public float Pulse;
            public float PulseHz;
            /// <summary>플립북 (P2): 칸 배열(JuiceArt가 쥔 것 — 할당 없음)과 초당 칸 수</summary>
            public Sprite[] Frames;
            public float FrameRate;
            public int Frame;
            /// <summary>크기 틀(sizeDelta = SizeBase)을 고정했는지 — 그 뒤로 크기는 localScale로만</summary>
            public bool Scaled;
            /// <summary>좌우로 뒤집어 그리는지 (FaceArt가 Spawn 직후 localScale.x = −1로 둔 것을 처음 보이는 프레임에 기억한다)</summary>
            public bool Mirror;
            /// <summary>Motion.Curve 진행을 InOutCubic으로 (기본 SmoothStep)</summary>
            public bool EaseCubic;
        }

        [SerializeField] BattleFieldMapper _mapper;
        [SerializeField] ShopView _shop;
        [SerializeField] RectTransform _layer;
        [Tooltip("바닥 장판/마법진 레이어 (액터 아래)")]
        [SerializeField] RectTransform _groundLayer;
        [Tooltip("피해 숫자 레이어 (입자보다 위)")]
        [SerializeField] RectTransform _numberLayer;
        [SerializeField] RectTransform _shakeRoot;
        [SerializeField] Image _particleTemplate;
        [SerializeField] TMP_Text _numberTemplate;
        [SerializeField] int _particleCapacity = 440;
        [SerializeField] int _groundCapacity = 96;
        [SerializeField] int _numberCapacity = 48;
        [Tooltip("스킬 연출 그림 / 흰색 모양")]
        [SerializeField] SkillVfxLibrary _library;
        [Tooltip("계보별 기본 공격 그림 (atk_{계보}_shot/hit, atk_slash, atk_snipe_trail)")]
        [SerializeField] AttackFxCatalog _catalog;

        [Header("스프라이트 (흰색 + 알파)")]
        [SerializeField] Sprite _sparkle;
        [SerializeField] Sprite _glow;
        [SerializeField] Sprite _trail;
        [SerializeField] Sprite _ring;
        [Tooltip("부드러운 원 (circle_soft): 그림자·바닥 웅덩이·잉크 받침·가는 실 땀처럼 모양이 없어야 하는 곳만")]
        [SerializeField] Sprite _soft;
        [Tooltip("연기·먼지 뭉게구름 (fx_puff, 흰색 + 외곽선). 눈에 보이는 퍽·구름·먼지 입자는 이것으로. 없으면 _soft")]
        [SerializeField] Sprite _puff;

        [Header("큰 연출")]
        [Tooltip("앱 전체 번쩍임 (스테이지 클리어/실패, 보스 등장 같은 드문 순간만)")]
        [SerializeField] Image _screenFlash;
        [Tooltip("전장 안에서만 번쩍이는 덮개 (빛 = 스크린 블렌드 / 틴트 = 알파). 상단 바/탭 패널은 덮지 않는다")]
        [SerializeField] Image _fieldFlash;
        [SerializeField] Image _lampRing;
        [SerializeField] Image _lampPillar;
        [SerializeField] Image[] _fireflies;

        [Header("손맛 v2 (FX-JUICE)")]
        [Tooltip("새 손맛 그림 묶음 (없으면 기본 도구가 예전 그림으로 대신한다)")]
        [SerializeField] JuiceArt _juice;
        [Tooltip("ATK-08 녹 얼룩 그림 (Art/VFX/fx_rust_stain — 빌더가 있을 때만 굽는다). 없으면 흰 얼룩 모양 + 주황")]
        [SerializeField] Sprite _rustStainArt;
        [Tooltip("ATK-08 좀나방 구멍 그림 (Art/VFX/fx_moth_hole). 없으면 흰 찢김 모양 + 분홍빨강")]
        [SerializeField] Sprite _mothHoleArt;
        [Tooltip("전장 어둡게 (Fx 레이어 아래 — 액터·배경만 어두워지고 연출·숫자는 밝게 남는다)")]
        [SerializeField] Image _fieldDim;
        [Tooltip("입자 머티리얼 UI/FxPremul (미리 곱한 알파: 보통 + 가산 함께). 없으면 기본 UI 머티리얼 (가산 층은 반투명 빛)")]
        [SerializeField] Material _premulMaterial;
        [Tooltip("빛 번쩍임 머티리얼 UI/FxScreen (스크린 블렌드). 없으면 알파 덮개로 약하게")]
        [SerializeField] Material _screenMaterial;
        [Tooltip("실루엣 머티리얼 UI/WhiteFlash (보스 처치 순간의 크림 실루엣·인쇄 어긋남). 없으면 실루엣 없이")]
        [SerializeField] Material _flashMaterial;

        static readonly Vector2 CenterPivot = new(0.5f, 0.5f);
        static readonly Vector2 StreakPivot = new(0.85f, 0.5f);
        const float PopTime = 0.22f;
        const float AlphaFloor = 1f / 255f;
        /// <summary>처음 보이는 프레임에 고정하는 입자 크기 틀 (px) — 실제 크기 = SizeBase × localScale</summary>
        const float SizeBase = 100f;
        const float InvSizeBase = 1f / SizeBase;
        static readonly Vector2 SizeBaseBox = new(SizeBase, SizeBase);
        static readonly Vector3 MirrorScale = new(-1f, 1f, 1f);

        // ── 불 밝히기 세기 (E17, 캡처로 맞춘 값: 가운데가 하얗게 날아가 수선점이 사라지지 않게) ──
        // 2차 조율 (r1 비평: +250ms에 전장 띠의 32%가 하얗게 날아가고 +33ms 대비가 깨끗한 화면의 75% — 우유빛 막):
        // 빛 번쩍임은 등불색 옅은 막으로 0.28 · 0.45초, 바닥 빛 물결은 (1 − k)^1.5로 일찍 옅어지고 가산 0.75, 안쪽 고리 0.35, 등불 꽃 0.42 · 0.34초.
        // 목표: +250ms 거의 흰 화소 ≤ 15% · +33ms 대비 ≥ 깨끗한 화면의 85% · 바뀐 넓이 ≥ 45% · 줌 그대로 (빛살·별이 볼거리로 남는다)
        /// <summary>
        /// 터짐 빛 번쩍임 세기 / 실제 시간 (스크린 블렌드, 등불색 — 우유빛이 아니라 따뜻한 막). 번쩍임 줄이기는 다른 빛 층처럼 ×0.5 (JuiceGates.LampLight) = 0.14.
        /// 3차 조율 (r2 비평: 줄이기 막이 LampWave.ReducedFlashPeak 0.25 그대로라 보통 막의 83% — 빛 층이 모두 ×0.5인 줄이기 화면에 막만 남아
        /// +33ms 대비가 깨끗한 화면의 80%, 평균 밝기 +32%로 보통 불 밝히기(대비 91.5%)보다 뿌옜다). 캡처 프레임에서 막을 걷고 다시 입혀 본 값:
        /// 0.18 → 대비 86.6% · 밝기 +22.9%, 0.15 → 89.3% · +18.9% (목표 대비 ≥ 88% · 밝기 ≤ +25% · +250ms 거의 흰 화소 ≤ 2%).
        /// LampWave.FlashPeak / ReducedFlashPeak는 설정별 상한으로 그대로 둔다 (소리·테스트가 읽는다)
        /// </summary>
        /// <remarks>
        /// Wave 3 (fx2 leftovers, 비평 nice): 0.30 → 0.28 — 보이는 전장 전체(1080×1464)로 재면 +33ms 대비 84.5%(빛살 밖 위쪽 거리는 막만 받는다),
        /// +100ms 거의 흰 화소 15.6%. 0.28이면 전장 대비 약 86% · 거의 흰 화소 ≤ 15%, 빛살 · 별 · 꽃은 그대로라 펀치는 남는다
        /// </remarks>
        const float LampLightFlash = 0.28f;
        const float LampLightFlashTime = 0.45f;
        /// <summary>수선점 피해 숫자가 뜨는 높이 (문 기준, 붉은 문 빛 Flash(문 + 40) 안 — 이름표를 가리지 않는다)</summary>
        const float ShopNumberLift = 36f;
        /// <summary>보스 처치 +50ms 등불색 빛 세기 (번쩍임 줄이기 0.15 — Wave 3: 크림 0.35 → 등불 0.30)</summary>
        const float BossKillLight = 0.30f;
        /// <summary>등불 꽃 크기 / 빛 세기 (순수 가산)</summary>
        const float LampBloomSize = 560f;
        const float LampBloomIntensity = 0.42f;
        const float LampBloomLife = 0.34f;
        /// <summary>바닥 빛 물결(_lampRing) 가산 비율 — 알파는 (1 − k)^1.5로 물결이 반쯤 퍼질 때 0.35까지 빨리 옅어진다</summary>
        const float LampRingAdd = 0.75f;
        /// <summary>안쪽 고리 (빛 물결 반지름 × 0.8을 따라감) 빛 세기</summary>
        const float LampInnerRingIntensity = 0.35f;
        /// <summary>빛살 세기 (순수 가산)</summary>
        const float LampRaysIntensity = 0.55f;
        /// <summary>빛기둥 세기 (순수 가산)</summary>
        const float LampPillarIntensity = 0.5f;
        /// <summary>적마다 금빛 별 자리 크기 (× U)</summary>
        const float LampStarSize = 84f;

        Particle[] _particles;
        int[] _freeParticles;
        int _freeParticleCount;
        int[] _freeGround;
        int _freeGroundCount;
        int _groundTotal;
        /// <summary>이펙트 품질 예산 (동시 위쪽 입자 수 상한, 높음 420 / 보통 240 / 낮음 130)</summary>
        int _budget = 420;
        int _groundBudget = 96;
        /// <summary>이번 프레임에 만든 입자 수 (별빛·곁들임은 프레임당 _frameSpawnCap까지)</summary>
        int _frameSpawns;
        int _frameSpawnCap = 48;
        int _haloCap = 6;
        int _decalCap = 12;
        int _maxNumbers = 24;
        int _halos;
        int _decals;
        /// <summary>입자 머티리얼이 미리 곱한 알파인지 (아니면 보통 알파로 쓴다)</summary>
        bool _premul;
        /// <summary>성능 대조(시험장)만: 예전처럼 매 프레임 sizeDelta · Image.color를 쓴다 (FxDevSettings.LegacyParticleWrites)</summary>
        bool _legacyWrites;
        /// <summary>지난 표본 뒤 예산이 차서 못 만든 입자 수 (별빛 층 / 영웅 층 / 바닥) — FxFrameStats</summary>
        int _rejectedSparkle;
        int _rejectedHero;
        int _rejectedGround;
        int _cueDroppedSeen;
        Sprite _lastRejectedHero;

        // 설정 (GameSettings.Changed 때만 읽는다 — PlayerPrefs를 매 프레임 읽지 않게)
        FxQuality _quality = FxQuality.High;
        bool _reduceFlash;
        /// <summary>설정의 흔들림 배율 (GameSettings.ShakeScale)</summary>
        float _shakeScale = 1f;
        FxDevSettings _dev;

        readonly TraumaShake _trauma = new();
        FxKick _kick;
        FxZoom _zoom;
        Vector2 _zoomPivot;
        FxSlowMotion _slowMotion;
        readonly FxGovernor _gov = new();
        bool _shaking;
        /// <summary>아직 닿지 않은 치명타의 흔들림까지 남은 시간 (없으면 음수, 예전 API)</summary>
        float _critTraumaTimer = -1f;
        /// <summary>연출 시계 (전체 정지·느린 화면이 걸린 시간). 숫자 합치기 창은 이 시계로 잰다</summary>
        float _clock;
        /// <summary>실제 시간 (Advance dt의 합). 화면 효과 간격·지속은 이 시계로</summary>
        float _realClock;
        float _timeScale = 1f;
        float _lastLampReal = -9999f;
        FxFrameStats _stats;
        /// <summary>이번 프레임 흔들림(trauma + 킥) / 줌 중심 보정 이동 (px, 표본용)</summary>
        float _shakeNow;
        float _zoomShiftNow;

        float _lampAge = -1f;
        Vector2 _lampCenter;
        int _lampInner = -1;

        /// <summary>불 밝히기 적 명중 하나 (한 프레임에 온 것을 모아 다음 Advance에서 도착 순서로 그린다)</summary>
        struct LampHitRequest
        {
            public Vector2 Position;
            /// <summary>빛 물결이 닿는 연출 시각</summary>
            public float At;
            public EnemyView Target;
            public int TargetId;
        }

        const int LampHitCapacity = 64;
        readonly LampHitRequest[] _lampHits = new LampHitRequest[LampHitCapacity];
        int _lampHitCount;
        /// <summary>이번 불 밝히기에서 그린 적 명중 수 (별비 수를 정할 때 처치 몫을 남긴다)</summary>
        int _lampHitTotal;
        /// <summary>별비 첫 방울 연출 시각 (음수 = 없음) / 방울 수 (−1 = 아직 안 정함) / 다음 방울 번호</summary>
        float _rainStart = -1f;
        int _rainCount = -1;
        int _rainNext;
        Material _numberMaterial;
        Vector2[] _fireflyBase;
        float[] _fireflySeed;
        bool _ready;
        /// <summary>BattleView.BeginFrame이 이번 프레임 연출 시계를 이미 옮겼다 (Advance가 다시 세지 않는다)</summary>
        bool _clockTicked;
        int _beganFrame = -1;

        public SkillVfxLibrary Library => _library;
        public AttackFxCatalog Catalog => _catalog;

        /// <summary>
        /// 이번 프레임 연출 시간 배율 (보스 처치·불 밝히기 전체 정지 0 → 느린 화면 0.3 → 보통 1).
        /// BattleView가 액터(적·잔향이·지킴이) 갱신 dt에 곱해 입자·숫자·피격 반응·처치 보류가 같은 시계로 흐르게 한다
        /// </summary>
        public float TimeScale => _timeScale;

        /// <summary>잠자기 모드 입자 예산 (IDLE-06, SPEC4 §3.6 — min(품질 예산, 60))</summary>
        public const int SleepParticleBudget = 60;

        bool _sleepMode;

        /// <summary>잠자기 모드인지 (AppSignals.SleepChanged → BattleView가 SetSleepMode)</summary>
        public bool SleepMode => _sleepMode;

        /// <summary>지금 위쪽 입자 예산 (품질 · 잠자기 반영 — 확인용)</summary>
        public int ParticleBudget => _budget;

        /// <summary>
        /// 잠자기 모드 (IDLE-06): 입자 예산 min(예산, 60), 피해 숫자 · 연속 알림 끔, 별빛 · 곁들임 층 버림 (투사체 · 명중 · 전장 덮개는 그대로).
        /// RI-6b: 잠들 때 떠 있는 피해 · 불 밝히기 숫자는 0.2초(실제 시간)에 걸쳐 사라지고, 아직 안 뜬 숫자와 예약된 불 밝히기 합계 알림은 버린다 (BattleFx.Numbers).
        /// aspect R4: 잠든 동안 열 전체 번쩍임(ScreenFlash — 상단 바 · 잠자기 카드까지 덮는다 · FieldFlash)은 없고, 잠드는 순간 켜져 있던 앱 전체 번쩍임은 끈다.
        /// 깨면 소리 없이 되돌린다 (토스트 없음). 같은 값이 두 번 와도 괜찮다. 할당 없음
        /// </summary>
        public void SetSleepMode(bool asleep)
        {
            if (_sleepMode == asleep) return;
            _sleepMode = asleep;
            ReadSettings();
            if (asleep)
            {
                BeginSleepNumberFade();
                StopScreenFlash();
            }
            else EndSleepNumberFade();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (asleep) Debug.Log($"[Verify] sleep_budget={_budget} numbers=off");
            else Debug.Log($"[Verify] sleep_wake budget={_budget}");
#endif
        }

        /// <summary>잠자기 입자 예산 (순수 — SleepSignalTests)</summary>
        public static int SleepBudget(int budget, bool asleep) => asleep ? Mathf.Min(budget, SleepParticleBudget) : budget;

        /// <summary>지금 쓸 수 있는 위쪽 입자 칸 수 (한꺼번에 많이 뿌리는 연출이 다른 연출 몫까지 쓰지 않도록, 품질 예산 기준)</summary>
        public int FreeParticles => _ready ? Mathf.Max(0, _budget - UsedParticles) : _particleCapacity;

        /// <summary>지금 살아 있는 위쪽 입자 수</summary>
        int UsedParticles => _particleCapacity - _freeParticleCount;

        /// <summary>지금 살아 있는 바닥 입자 수</summary>
        int UsedGround => _groundTotal - _freeGroundCount;

        /// <summary>입자 레이어 (입자 anchoredPosition = 전장 로컬 좌표 — 배치 캡처가 볼 자리를 화면 좌표로 바꿀 때)</summary>
        public RectTransform ParticleLayer => _layer;

        /// <summary>전장 흔들림 뿌리 (캡처 metrics)</summary>
        public RectTransform ShakeRoot => _shakeRoot;

        /// <summary>
        /// 보이는 띠의 위쪽 끝 (전장 로컬 y). 시트가 열려 전투가 올라가면 mapper.VisibleTop을 따른다 (UX-SPEC-IA §6.5 CR-01) —
        /// MAIN에서는 입자 레이어 위 끝(h/2)과 같다. 그 위는 전장 마스크에 잘리거나 상단 바에 가린다
        /// </summary>
        public float FieldTop => _mapper != null ? _mapper.VisibleTop : _layer != null ? _layer.rect.height * 0.5f : 312f;

        /// <summary>보이는 띠의 아래쪽 끝 (전장 로컬 y): MAIN은 덱 띠 위 끝(−h/2), 시트가 열리면 시트 위 끝 (mapper.VisibleBottom)</summary>
        public float FieldBottom => _mapper != null ? _mapper.VisibleBottom + DevSheetCover : -FieldTop;

        /// <summary>
        /// 연출 시험장의 SHEET 변형만 (개발 빌드): 시트가 전장 창 아래를 덮는 높이를 흉내 낸다 (836 − 156 = 680). UIFrame.SheetProgress는 SheetController만 쓰므로
        /// 시험장은 전투만 올리고(mapper.SetView) 연출 바닥(FieldBottom — 숫자 바닥 · 스테이지 클리어 바닥 · 큰 터짐 바닥)을 이만큼 올린다. 보통은 0
        /// </summary>
        internal float DevSheetCover { get; set; }

        /// <summary>입자 레이어의 가로 반폭 (전장 로컬)</summary>
        public float FieldHalfWidth => _layer != null ? _layer.rect.width * 0.5f : 540f;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_ready) return;
            _ready = true;

            // 입자 머티리얼: 미리 곱한 알파 셰이더를 쓸 수 있으면 템플릿에 넣고 (복제되는 모든 입자가 같은 머티리얼), 아니면 기본 UI 머티리얼
            _premul = _premulMaterial != null && _premulMaterial.shader != null && _premulMaterial.shader.isSupported;
            _particleTemplate.material = _premul ? _premulMaterial : null;

            int ground = _groundLayer != null ? Mathf.Max(0, _groundCapacity) : 0;
            _groundTotal = ground;
            int total = _particleCapacity + ground;
            _particles = new Particle[total];
            _freeParticles = new int[_particleCapacity];
            _freeGround = new int[Mathf.Max(1, ground)];
            _particleTemplate.enabled = false;
            for (int i = 0; i < total; i++)
            {
                bool isGround = i >= _particleCapacity;
                var image = Instantiate(_particleTemplate, isGround ? _groundLayer : _layer);
                image.name = isGround ? "GroundParticle" : "Particle";
                image.raycastTarget = false;
                image.enabled = false;
                // 순수 가산 입자(알파 ≈ 0)가 잘려 나가지 않게 (정점 알파 바닥 1/255와 함께)
                image.canvasRenderer.cullTransparentMesh = false;
                _particles[i] = new Particle { Image = image, Rect = image.rectTransform, Ground = isGround };
            }
            for (int i = 0; i < _particleCapacity; i++) _freeParticles[i] = _particleCapacity - 1 - i;
            _freeParticleCount = _particleCapacity;
            for (int i = 0; i < ground; i++) _freeGround[i] = total - 1 - i;
            _freeGroundCount = ground;

            ReadSettings();
            PrepareNumbers();
            PrepareSilhouettes();

            if (_screenFlash != null) _screenFlash.enabled = false;
            if (_fieldFlash != null) _fieldFlash.enabled = false;
            if (_fieldDim != null)
            {
                _fieldDim.enabled = false;
                _fieldDim.raycastTarget = false;
            }
            PrepareLamp();
            PrepareFireflies();
        }

        /// <summary>불 밝히기 고리·빛기둥: 새 빛 그림 + 미리 곱한 알파 머티리얼 (가산 0.75 / 순수 가산)</summary>
        void PrepareLamp()
        {
            if (_lampRing != null)
            {
                _lampRing.enabled = false;
                _lampRing.raycastTarget = false;
                var ring = Or(_juice != null ? _juice.LampWaveRing : null, _juice != null ? _juice.RingGlow : null);
                if (ring != null) _lampRing.sprite = ring;
                if (_premul)
                {
                    _lampRing.material = _premulMaterial;
                    _lampRing.canvasRenderer.cullTransparentMesh = false;
                }
            }
            if (_lampPillar != null)
            {
                _lampPillar.enabled = false;
                _lampPillar.raycastTarget = false;
                var pillar = _juice != null ? _juice.LightPillar : null;
                if (pillar != null) _lampPillar.sprite = pillar;
                if (_premul)
                {
                    _lampPillar.material = _premulMaterial;
                    _lampPillar.canvasRenderer.cullTransparentMesh = false;
                }
            }
        }

        void OnEnable() => GameSettings.Changed += ReadSettings;

        void OnDisable() => GameSettings.Changed -= ReadSettings;

        void OnDestroy()
        {
            if (_numberMaterial != null) Destroy(_numberMaterial);
        }

        void LateUpdate()
        {
            // 실루엣은 이번 프레임에 새로 만든 입자보다 늘 위에 (보스 처치 임팩트 프레임)
            if (_ready) KeepSilhouettesOnTop();
        }

        /// <summary>기기 설정(이펙트 품질·번쩍임 줄이기·흔들림·피해 숫자)을 다시 읽는다. 시험장 DevOverride가 켜져 있으면 그 값</summary>
        void ReadSettings()
        {
            if (_dev.Active)
            {
                _quality = _dev.Quality;
                _reduceFlash = _dev.ReduceFlash;
                _shakeScale = Mathf.Clamp01(_dev.ShakeScale);
                _numberMode = _dev.Numbers;
            }
            else
            {
                _quality = GameSettings.FxQuality;
                _reduceFlash = GameSettings.ReduceFlash;
                _shakeScale = Mathf.Clamp01(GameSettings.ShakeScale);
                _numberMode = GameSettings.DamageNumbers;
            }
            ReduceFlashNow = _reduceFlash;
            _legacyWrites = _dev.Active && _dev.LegacyParticleWrites;
            _budget = SleepBudget(Mathf.Clamp(HitFeel.ParticleBudget(_quality), 60, Mathf.Max(60, _particleCapacity)), _sleepMode);
            _groundBudget = Mathf.Min(HitFeel.GroundBudget(_quality), Mathf.Max(0, _groundCapacity));
            _frameSpawnCap = HitFeel.FrameSpawnCap(_quality);
            _haloCap = HitFeel.HaloCap(_quality);
            _decalCap = HitFeel.DecalCap(_quality);
            _maxNumbers = HitFeel.MaxNumbers(_quality);
        }

        /// <summary>
        /// 복잡한 전장 위에서도 읽히도록 피해 숫자에 진한 잉크 외곽선 + 아래 그림자를 두른 머티리얼 (런타임에 한 번 생성, 모든 숫자가 공유).
        /// 외곽선 0.28 · #2D2458 · 굵게 0.15 · 그림자 #140D2A 0.6 (아래 0.55, 번짐 0.2, 부드럽게 0.35)
        /// </summary>
        static Material CreateOutlineMaterial(TMP_Text template)
        {
            var source = template.fontSharedMaterial;
            if (source == null || !source.HasProperty(OutlineWidthId)) return null;
            var material = new Material(source) { name = source.name + " (Battle Number)" };
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat(OutlineWidthId, 0.28f);
            material.SetColor(OutlineColorId, JuiceColors.Ink);
            if (material.HasProperty(FaceDilateId)) material.SetFloat(FaceDilateId, 0.15f);
            if (material.HasProperty(UnderlayColorId))
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor(UnderlayColorId, new Color(0x14 / 255f, 0x0D / 255f, 0x2A / 255f, 0.6f));
                if (material.HasProperty(UnderlayOffsetXId)) material.SetFloat(UnderlayOffsetXId, 0f);
                if (material.HasProperty(UnderlayOffsetYId)) material.SetFloat(UnderlayOffsetYId, -0.55f);
                if (material.HasProperty(UnderlayDilateId)) material.SetFloat(UnderlayDilateId, 0.2f);
                if (material.HasProperty(UnderlaySoftnessId)) material.SetFloat(UnderlaySoftnessId, 0.35f);
            }
            // 외곽선·그림자 두께에 맞춰 SDF 스케일 비율을 다시 계산 (안 하면 외곽선이 잘리거나 번진다)
            ShaderUtilities.UpdateShaderRatios(material);
            return material;
        }

        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");

        /// <summary>
        /// BattleView가 액터를 놓기 전에 한 번 (perf PF-2 F3): 이번 프레임 연출 시간 배율(멈칫 · 느린 화면)을 먼저 정한다 — 같은 프레임의 Advance는
        /// 다시 세지 않는다. 같은 프레임에 두 번 불러도 한 번만 센다. 돌려주는 값 = TimeScale
        /// </summary>
        public float BeginFrame(float dt)
        {
            if (!_ready) Prepare();
            int frame = Time.frameCount;
            if (_beganFrame != frame)
            {
                _beganFrame = frame;
                _timeScale = _slowMotion.Tick(dt);
                _clockTicked = true;
            }
            return _timeScale;
        }

        void Update() => Advance(Time.deltaTime);

        /// <summary>
        /// 연출을 dt(실제 시간)만큼 진행한다. 보통은 Update가 부르고, 연출 시험장 배치 캡처(에디터, 재생 없이)는 일정한 간격으로 직접 부른다
        /// </summary>
        public void Advance(float dt)
        {
            if (!_ready) Prepare();
            _frameSpawns = 0;
            // 지난 프레임에 모인 불 밝히기 적 명중을 도착 순서로 (시계를 옮기기 전 — 지연 = 도착 시각 − 지금이 정확하다)
            FlushLampHits();
            _realClock += dt;
            // 보스 처치·불 밝히기 순간 연출이 잠깐 멈췄다가 느리게 (번쩍임·흔들림·어둡게는 실제 시간)
            // BattleView가 이번 프레임 BeginFrame으로 이미 옮겼으면 다시 세지 않는다 (시험장 배치 캡처 · BattleView 없이 돌 때는 여기서)
            if (_clockTicked) _clockTicked = false;
            else _timeScale = _slowMotion.Tick(dt);
            float fxDt = dt * _timeScale;
            _clock += fxDt;
            if (_critTraumaTimer >= 0f && (_critTraumaTimer -= fxDt) < 0f) _trauma.AddCrit();
            FireDueCues();
            TickLampRain();
            UpdateParticles(fxDt);
            // 잠들 때 숫자 사라짐은 실제 시간으로 (불 밝히기 · 보스 처치 멈춤 중에 잠들어도 0.2초에 끝난다)
            TickSleepNumbers(dt);
            UpdateNumbers(fxDt);
            UpdateOverlay(ref _fieldOverlay, _fieldFlash, dt);
            UpdateOverlay(ref _screenOverlay, _screenFlash, dt);
            UpdateDim(dt);
            UpdateSilhouettes(dt);
            UpdateShake(dt);
            UpdateLampRing(fxDt);
            UpdateFireflies();
            SampleStats();
        }

        // ───────────── 조합 연출 (코어) ─────────────

        /// <summary>
        /// 적 피격 (맥락 없는 명중 — 반사·지속 아닌 기타): 가산 빛 + 코어 별 + 빛줄기 2~4 + 별빛. target이 있으면 닿는 순간(delay)까지 그 적을 따라간다.
        /// 밀도 관리(§2.4)를 거친다: 이 도착 전 0.25초의 도착이 8 이하이고 같은 대상 90ms에 처음이면 풀, 아니면 미니(코어 별 + 작은 빛),
        /// 16을 넘으면 코어 별만. target이 없으면 대상별 간격은 세지 않는다 (부르는 쪽이 보이는 적을 넘기면 TryFullImpact까지).
        /// 스킬 명중은 SkillHit(in SkillHitFx)를 쓴다 (코어 기본 명중 + show 결)
        /// </summary>
        public void Hit(Vector2 position, Color color, bool big, float delay = 0f, EnemyView target = null)
        {
            if (!_ready) Prepare();
            int density = Density(delay);
            NoteHit(delay);
            bool full = density <= HitFeel.DensityMini && TryFullImpact(target != null ? target.Id : -1, delay);
            float unit = UnitScale;
            float scale = (big ? 1.5f : 1f) * unit;
            var coreTint = JuiceColors.TowardCream(color, 0.7f);
            if (!full)
            {
                int mini = CoreStar(position, coreTint, 40f * scale, 26f * scale, 0.05f, delay, 0.8f);
                AnchorTo(mini, target);
                if (density > HitFeel.DensityLean) return;
                int small = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(Color.Lerp(color, Color.white, 0.45f), 0.4f), position, 30f * scale, 70f * scale,
                    0.16f, delay, 1f);
                AnchorTo(small, target);
                return;
            }
            int glow = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(Color.Lerp(color, Color.white, 0.45f), 0.5f), position, 40f * scale, 100f * scale,
                0.2f, delay, 1f);
            AnchorTo(glow, target);
            int core = CoreStar(position, coreTint, 52f * scale, 34f * scale, 0.06f, delay, 0.9f);
            AnchorTo(core, target);
            Streaks(position, Vector2.up, big ? 4 : 2, 360f, 380f, 620f, BattleEase.WithAlpha(JuiceColors.TowardCream(color, 0.5f), 0.9f), delay,
                16f, 4f, 1f, target);
            if (!Allow(FxPriority.Sparkle)) return;
            int count = big ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.7f + 0.3f;
                int spark = Drift(_sparkle, i % 2 == 0 ? color : UITheme.Cream, position, dir * Random.Range(40f, 80f) * scale, Vector2.down * 24f,
                    Random.Range(14f, 22f) * scale, 4f, Random.Range(0.3f, 0.42f), delay);
                if (spark >= 0) _particles[spark].Add = 0.7f;
                AnchorUntilShown(spark, target, delay);
            }
        }

        /// <summary>
        /// 잔향이가 빛으로 풀려 수선점으로 흘러 들어가는 시그니처 연출.
        /// 몸이 실처럼 풀리며 별빛/빛 꼬리가 곡선을 그리며 수선점 문으로 스며든다 (빛 0.7, 고리 0.6, 실 가닥 0.5 가산).
        /// </summary>
        public void EchoDissolve(Vector2 from, Vector2 shop, Color color, int refund)
        {
            if (!_ready) Prepare();
            var warm = Color.Lerp(color, UITheme.Lamp, 0.35f);
            int flash = Flash(from + Vector2.up * 40f, GlowCoreSprite, BattleEase.WithAlpha(Color.Lerp(color, Color.white, 0.4f), 0.8f), 80f, 170f, 0.45f);
            if (flash >= 0) _particles[flash].Add = 0.7f;
            int ring = Ring(from, BattleEase.WithAlpha(color, 0.75f), 40f, 120f, 0.5f, GroundAspect);
            if (ring >= 0) _particles[ring].Add = 0.6f;

            const int strands = 14;
            for (int i = 0; i < strands; i++)
            {
                var start = from + new Vector2(Random.Range(-30f, 30f), Random.Range(10f, 80f));
                var end = shop + new Vector2(Random.Range(-14f, 14f), Random.Range(-8f, 10f));
                var mid = (start + end) * 0.5f;
                var away = (start - end).normalized;
                var side = new Vector2(-away.y, away.x) * Random.Range(-140f, 140f);
                var control = mid + side + Vector2.up * Random.Range(90f, 190f);
                bool isTrail = i % 3 == 0;
                int index = Curve(isTrail ? _trail : _sparkle, i % 2 == 0 ? color : warm, start, control, end,
                    isTrail ? Random.Range(70f, 96f) : Random.Range(20f, 34f), isTrail ? 30f : 8f,
                    Random.Range(0.8f, 1.2f), i * 0.035f + Random.Range(0f, 0.08f), true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Arrival = Arrival.ShopAbsorb;
                p.AlignToPath = isTrail;
                p.Aspect = isTrail ? 0.28f : 1f;
                p.Spin = isTrail ? 0f : Random.Range(-260f, 260f);
                p.Add = isTrail ? 0.5f : 0.7f;
            }

            if (refund > 0) Text(shop + new Vector2(0f, 120f), BattleEase.Plus(refund), UITheme.Resonance, 0.9f, 1.05f);
        }

        /// <summary>잔향이 소환: 수선점에 빛이 모였다가 형태가 잡힌다 (모이는 별빛 0.7 · 번쩍 0.8 · 고리 0.6 가산)</summary>
        public void Summon(Vector2 shop, Color color)
        {
            if (!_ready) Prepare();
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count + Random.Range(-0.2f, 0.2f);
                var start = shop + new Vector2(Mathf.Cos(angle) * 150f, Mathf.Sin(angle) * 80f + 40f);
                var control = Vector2.Lerp(start, shop, 0.5f) + new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * 50f;
                int index = Curve(_sparkle, color, start, control, shop + Vector2.up * 30f, 26f, 10f, 0.38f, Random.Range(0f, 0.06f), true);
                if (index < 0) continue;
                _particles[index].Spin = 300f;
                _particles[index].Add = 0.7f;
            }
            int flash = Flash(shop + Vector2.up * 30f, GlowCoreSprite, BattleEase.WithAlpha(Color.Lerp(color, Color.white, 0.5f), 0.85f), 60f, 200f, 0.4f, 0.32f);
            if (flash >= 0) _particles[flash].Add = 0.8f;
            CoreStar(shop + Vector2.up * 30f, JuiceColors.TowardCream(color, 0.6f), 90f, 60f, 0.08f, 0.32f, 0.8f);
            int ring = Ring(shop, BattleEase.WithAlpha(color, 0.8f), 40f, 220f, 0.5f, GroundAspect, 0.32f);
            if (ring >= 0) _particles[ring].Add = 0.6f;
        }

        /// <summary>경로를 따라 기우는 입자의 첫 방향 (첫 프레임부터 날아가는 쪽을 보게)</summary>
        void FaceTo(int index, Vector2 from, Vector2 to)
        {
            if (index < 0) return;
            var delta = to - from;
            if (delta.sqrMagnitude < 0.01f) return;
            ref var p = ref _particles[index];
            p.Rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);
        }

        /// <summary>떠다니는 잔향이가 남기는 작은 빛 부스러기</summary>
        public void Mote(Vector2 position, Color color)
        {
            int index = Drift(_sparkle, BattleEase.WithAlpha(color, 0.8f), position + new Vector2(Random.Range(-16f, 16f), Random.Range(0f, 30f)),
                new Vector2(Random.Range(-10f, 10f), Random.Range(14f, 30f)), Vector2.zero, Random.Range(8f, 14f), 2f, 0.6f);
            if (index >= 0) _particles[index].Add = 0.5f;
        }

        /// <summary>
        /// 수선점 내구도가 실제로 깎임: 문에 붉은 빛(알파 — 위험색은 가산하지 않는다) + 문 위 붉은 빛 안에서 "-N" (Danger, 합침).
        /// 숫자는 문 + (±30, +36)에서 떠올라 건물 위로 간다 — 예전 문 아래(−24)는 '수선점' 이름표 위에 떠서 '수선-5점'으로 읽혔다 (fx2 r2 비평).
        /// 적 종류별 공격 그림·나뭇조각·흔들림은 그 앞에 오는 적의 공격(EnemyStrike → ShopImpact)이 맡는다.
        /// </summary>
        public void ShopDamaged(Vector2 door, float damage)
        {
            if (!_ready) Prepare();
            Flash(door + Vector2.up * 40f, _glow, BattleEase.WithAlpha(UITheme.Danger, 0.8f), 120f, 240f, 0.3f);
            ShopDamageNumber(door + new Vector2(Random.Range(-30f, 30f), ShopNumberLift), damage);
        }

        /// <summary>수선점 회복: 민트 별빛(가산 0.5)과 하트 천 조각이 둥실 떠오른다 (delay: 힐 맥박의 민트 방울이 문에 닿는 순간까지). 순하게 — 코어 별·번쩍임 없음</summary>
        public void ShopHealed(Vector2 shop, float amount, float delay = 0f)
        {
            if (!_ready) Prepare();
            for (int i = 0; i < 5; i++)
            {
                int spark = Drift(_sparkle, UITheme.Positive, shop + new Vector2(Random.Range(-70f, 70f), Random.Range(0f, 60f)),
                    Vector2.up * Random.Range(50f, 90f), Vector2.zero, Random.Range(14f, 22f), 4f, Random.Range(0.6f, 0.8f), delay + i * 0.05f);
                if (spark >= 0) _particles[spark].Add = 0.5f;
            }
            var heart = Motif(VfxMotif.Heart);
            for (int i = 0; i < 3; i++)
            {
                int index = Drift(heart, i % 2 == 0 ? HealPink : UITheme.Positive, shop + new Vector2(Random.Range(-60f, 60f), Random.Range(30f, 90f)),
                    Vector2.up * Random.Range(60f, 100f), Vector2.zero, Random.Range(22f, 30f), 14f, Random.Range(0.8f, 1f), delay + 0.08f + i * 0.09f);
                if (index < 0) continue;
                _particles[index].Spin = Random.Range(-25f, 25f);
                _particles[index].Wobble = 8f;
                _particles[index].FadeOut = 0.6f;
            }
            int glow = Flash(shop + Vector2.up * 50f, GlowCoreSprite, BattleEase.WithAlpha(UITheme.Positive, 0.45f), 80f, 220f, 0.45f, delay);
            if (glow >= 0) _particles[glow].Add = 0.5f;
            if (amount >= 1f) HealNumber(ShopNumberId, shop + new Vector2(0f, 130f), amount, delay);
        }

        /// <summary>
        /// 수선점 불 밝히기 "불 밝히기" (FX-JUICE E17, 이 게임의 대표 순간, 1.3초 안).
        ///   ① 점화 (연출 0.2배 0.12초 — BattleView가 건다): 전장이 0.35로 어두워지고 문에 등불빛이 모이며 별빛 12 / 9 / 6개(품질별)가 빨려 든다.
        ///   ② 터짐 (연출 시계 LampWave.Start ≈ 실제 0.156초): 임팩트 프레임(어둡게만) · 빛살 · 등불 꽃(hero_lamp_bloom 0.42 · 0.34초) · 코어 · 가로 섬광 ·
        ///      바닥 빛 물결(_lampRing, 가산 0.75 · (1 − k)^1.5) + 안쪽 고리 0.35 · 빛기둥 · 속도선 · 튀는 빛 알갱이 16 / 12 / 8 · 별/꽃잎 비(높음 40 / 보통 24 / 낮음 10).
        ///      화면: 빛 번쩍임 등불색 0.28 · 0.45초(줄이기 0.14, 스크린 블렌드 — 우유빛 막 대신 따뜻한 막) · 줌 1.035(수선점 중심) · 흔들림 0.85 위로 · 진동 Medium.
        ///      빛살·등불 꽃·빛기둥은 순수 가산(칠하지 않고 밝힌다)이고 세기를 눌러 수선점이 빛 속에서도 보인다.
        ///      번쩍임 줄이기면 빛 층(점화 빛 · 빛 번쩍임 막 · 빛살 · 등불 꽃 · 빛기둥 · 바닥 빛 물결 · 안쪽 고리 · 속도선)을 모두 ×0.5 (JuiceGates.LampLight —
        ///      속도선·안쪽 고리는 기본 도구가 한 번 더 줄인다: Speedlines ×0.6, Shockwave 가산 비율만큼 JuiceGates.RingLight).
        ///      별비는 한꺼번에 만들지 않고 제 시각에 하나씩 (지연 동안 칸을 붙잡지 않는다) — 적 명중을 먼저 그린 뒤 남은 칸으로 수를 정한다.
        ///   ③ 적마다 물결이 닿는 순간(LampWaveDelay)에 LampHit.
        /// </summary>
        public void LampBurst(Vector2 shop)
        {
            if (!_ready) Prepare();
            _lastLampReal = _realClock;
            _lampAge = 0f;
            _lampCenter = _mapper != null ? _mapper.Origin : shop;
            float release = LampWave.Start;
            float light = JuiceGates.LampLight(_reduceFlash);
            if (_lampRing != null)
            {
                _lampRing.enabled = true;
                _lampRing.rectTransform.sizeDelta = Vector2.zero;
                WriteColor(_lampRing, UITheme.Cream, 0f, LampRingAdd);
            }
            if (_lampPillar != null) _lampPillar.enabled = true;

            var core = shop + Vector2.up * 50f;
            // ① 점화: 전장이 어두워지고 (실제 시간: 60ms에 어두워져 터질 때까지 버틴다) 문에 등불빛이 모인다
            FieldDim(0.35f, 0.06f, 0.1f, 0.2f);
            int ignite = SpawnFx(GlowCoreSprite, BattleEase.WithAlpha(UITheme.Lamp, 0.6f * light), core, 60f, 160f, release, 0f, 1f);
            if (ignite >= 0)
            {
                _particles[ignite].Curve = SizeCurve.Linear;
                _particles[ignite].FadeOut = 1f;
            }
            // 빨려 드는 별빛 (별빛 층, 품질별 12 / 9 / 6)
            int gather = _quality == FxQuality.Low ? 6 : _quality == FxQuality.Medium ? 9 : 12;
            for (int i = 0; i < gather; i++)
            {
                float angle = i * Mathf.PI * 2f / gather + Random.Range(-0.15f, 0.15f);
                var start = core + new Vector2(Mathf.Cos(angle) * 190f, Mathf.Sin(angle) * 120f);
                int index = Curve(_sparkle, i % 2 == 0 ? UITheme.Lamp : UITheme.Cream, start, Vector2.Lerp(start, core, 0.5f) + Vector2.up * 30f, core,
                    Random.Range(22f, 30f), 8f, release + 0.06f, 0f, true);
                if (index < 0) continue;
                _particles[index].Spin = 420f;
                _particles[index].Add = 0.8f;
            }

            // ② 터짐 — 화면
            ImpactFrame(null, release, false, true);
            // 등불색 옅은 막 (크림 막은 대비를 지워 우유빛이 된다) — 빛은 빛살·별·물결이 맡는다. 번쩍임 줄이기면 다른 빛 층처럼 ×0.5 (0.15)
            LightFlash(UITheme.Lamp, LampLightFlash * light, LampLightFlashTime, release, true);
            ZoomPunch(HitFeel.ZoomLamp, shop, release, true);
            // 사방으로 흔들림 (SPEC4 §13.5 c — 예전 Vector2.up 기울기는 위아래로만 출렁였다)
            QueueCue(FxCueKind.Trauma, release, false, Vector2.zero, HitFeel.LampTrauma, 0f, 0f, 0f, default, false, null, RadialTraumaCode);
            Haptic(HapticKind.Medium, release);

            // ② 터짐 — 층 (아래에서 위로)
            // 빛살: 순수 빛(가산)으로 — 칠하지 않고 밝힌다 (수선점·지킴이가 빛 사이로 보이게)
            var rays = _juice != null ? _juice.Rays : null;
            if (rays != null)
            {
                int ray = SpawnFx(rays, BattleEase.WithAlpha(UITheme.Lamp, LampRaysIntensity * light), core, 820f, 900f, 1f, release, 1f);
                if (ray >= 0)
                {
                    ref var p = ref _particles[ray];
                    p.Curve = SizeCurve.Linear;
                    p.Spin = 20f;
                    p.FadeIn = 0.08f;
                    p.FadeOut = 0.5f;
                    p.FadeKind = FadeInQuad;
                }
            }
            // 등불 꽃: 순수 빛으로 150ms에 활짝 — 가운데가 하얗게 날아가 수선점이 사라지지 않게 세기를 누른다
            var bloomArt = _juice != null ? _juice.HeroLampBloom : null;
            int bloom = SpawnFx(bloomArt != null ? bloomArt : GlowCoreSprite,
                BattleEase.WithAlpha(bloomArt != null ? Color.white : UITheme.Lamp, LampBloomIntensity * light), core, 0f, LampBloomSize, LampBloomLife, release, 1f);
            if (bloom >= 0)
            {
                ref var p = ref _particles[bloom];
                p.Curve = SizeCurve.Snap;
                p.Spin = 10f;
                p.FadeIn = 0f;
                p.FadeOut = 0.25f;
                p.FadeKind = FadeInQuad;
            }
            CoreStar(core, JuiceColors.CoreCream, 300f, 200f, 0.12f, release);
            Flare(core, Color.Lerp(UITheme.Lamp, UITheme.Cream, 0.5f), 900f, 60f, 0f, 0.15f, release);
            if (JuiceGates.Speedlines(_quality, true)) Speedlines(core, JuiceColors.CoreCream, 1400f, 0.2f, release, 0.4f * light);
            // 안쪽 고리: 빛 물결 반지름 × 0.8을 따라간다 (UpdateLampRing이 크기를 맞춘다)
            _lampInner = Shockwave(_lampCenter, UITheme.Cream, 1f, 1f, LampWave.Life, release, 0.5f, LampInnerRingIntensity * light, GroundAspect, true);
            if (_lampInner >= 0)
            {
                _particles[_lampInner].Curve = SizeCurve.Linear;
                _particles[_lampInner].FadeOut = 0.5f;
            }
            // 물결과 함께 바깥으로 튀는 빛 알갱이 (별빛 층, 품질별 16 / 12 / 8)
            int sparks = _quality == FxQuality.Low ? 8 : _quality == FxQuality.Medium ? 12 : 16;
            for (int i = 0; i < sparks; i++)
            {
                var dir = RandomDirection();
                dir.y *= 0.6f;
                int spark = Drift(_sparkle, i % 3 == 0 ? UITheme.Cream : UITheme.Lamp, core, dir * Random.Range(260f, 480f), Vector2.up * 30f,
                    Random.Range(22f, 36f), 6f, Random.Range(0.55f, 0.8f), release + Random.Range(0f, 0.06f));
                if (spark >= 0) _particles[spark].Add = 0.8f;
            }
            // 여운: 별·꽃잎이 1초 동안 전장 위쪽에서 천천히 내려앉는다 — TickLampRain이 제 시각에 하나씩 (수는 적 명중을 그린 뒤 정한다)
            _rainStart = _clock + release + 0.15f;
            _rainCount = -1;
            _rainNext = 0;
            _lampHitTotal = 0;
        }

        /// <summary>
        /// 불 밝히기 별비 (연출 시계): 첫 Advance에서 수를 정한다 — 품질별 40/24/10 (높음은 여유 50% 이하면 18), 여유가 (물결에 맞은 적 × 6)보다
        /// 적으면 10 (처치 연출 몫). 방울은 제 시각에 하나씩 만들고, 그 순간 별빛 층 여유가 없으면 그 방울은 건너뛴다
        /// </summary>
        void TickLampRain()
        {
            if (_rainStart < 0f) return;
            if (_rainCount < 0)
            {
                int free = FreeParticles;
                int rain = JuiceGates.LampRain(_quality, _budget > 0 ? free / (float)_budget : 0f);
                if (free < _lampHitTotal * HitFeel.LampKillReserve) rain = Mathf.Min(rain, 10);
                _rainCount = rain;
            }
            float interval = LampWave.SparkleRain / Mathf.Max(1, _rainCount);
            while (_rainNext < _rainCount && _clock >= _rainStart + _rainNext * interval)
            {
                if (Allow(FxPriority.Sparkle)) LampRainDrop(_rainNext);
                _rainNext++;
            }
            if (_rainNext >= _rainCount) _rainStart = -1f;
        }

        /// <summary>별비 한 방울 (i % 3: 별 · 꽃잎 · 반짝이): 전장 위쪽에서 살랑이며 내려앉는다</summary>
        void LampRainDrop(int i)
        {
            float top = FieldTop;
            float half = FieldHalfWidth;
            var from = new Vector2(Random.Range(-half * 0.92f, half * 0.92f), top - Random.Range(10f, 160f));
            var petal = _juice != null ? _juice.MotePetal : null;
            var sprite = i % 3 == 0 ? Motif(VfxMotif.Star) : i % 3 == 1 && petal != null ? petal : _sparkle;
            int index = Drift(sprite, i % 3 == 0 ? UITheme.Lamp : UITheme.Cream, from,
                new Vector2(Random.Range(-20f, 20f), -Random.Range(120f, 220f)), Vector2.down * 30f, Random.Range(14f, 24f), 8f, Random.Range(0.8f, 1.1f));
            if (index < 0) return;
            ref var p = ref _particles[index];
            p.Wobble = 10f;
            p.Spin = Random.Range(-90f, 90f);
            p.FadeIn = 0.2f;
            p.Add = 0.6f;
        }

        /// <summary>
        /// 불빛 물결이 적에 닿음 (E17, delay = LampWaveDelay). 한 프레임에 온 명중을 모아 두었다가 다음 Advance에서 도착 순서로 그린다 —
        /// 밀도 관리(§2.4)가 가까운 적부터 세므로 가까운 적은 풀, 한꺼번에 몰린 무리는 얇아진다 (FlushLampHits → DrawLampHit).
        /// 적 반응(Lamp 등급, 바깥으로 10px)은 부르는 쪽의 QueueImpact
        /// </summary>
        public void LampHit(Vector2 position, float delay, EnemyView target = null)
        {
            if (!_ready) Prepare();
            if (_lampHitCount >= LampHitCapacity) FlushLampHits();
            _lampHits[_lampHitCount++] = new LampHitRequest
            {
                Position = position, At = _clock + Mathf.Max(0f, delay), Target = target, TargetId = target != null ? target.Id : -1,
            };
        }

        /// <summary>모아 둔 불 밝히기 명중을 도착 시각 순서로 그린다 (삽입 정렬, 할당 없음)</summary>
        void FlushLampHits()
        {
            if (_lampHitCount == 0) return;
            for (int i = 1; i < _lampHitCount; i++)
            {
                var item = _lampHits[i];
                int j = i - 1;
                while (j >= 0 && _lampHits[j].At > item.At)
                {
                    _lampHits[j + 1] = _lampHits[j];
                    j--;
                }
                _lampHits[j + 1] = item;
            }
            int count = _lampHitCount;
            _lampHitCount = 0;
            for (int i = 0; i < count; i++)
            {
                ref var r = ref _lampHits[i];
                // 그새 다른 적으로 다시 쓰인 몸이면 따라가지 않는다
                var target = r.Target != null && r.Target.Id == r.TargetId ? r.Target : null;
                DrawLampHit(r.Position, Mathf.Max(0f, r.At - _clock), target);
                r.Target = null;
            }
            _lampHitTotal += count;
        }

        /// <summary>
        /// 불빛 물결 명중 하나 (밀도 관리 §2.4 — 이 도착 전 0.25초의 도착 수):
        /// 8 이하 = 풀 (코어 별 66px · 금빛 별이 1.3배로 터져 84px로 · 등불색 고리 40 → 150px(낮음 품질 제외) · 수선점 바깥쪽 빛줄기 3/2/2) ·
        /// 9~16 = 코어 별 + 금빛 별 · 그 이상 = 코어 별만
        /// </summary>
        void DrawLampHit(Vector2 position, float delay, EnemyView target)
        {
            int density = Density(delay);
            NoteHit(delay);
            float unit = UnitScale;
            int core = CoreStar(position, JuiceColors.CoreCream, 66f * unit, 44f * unit, 0.066f, delay);
            AnchorTo(core, target);
            if (density > HitFeel.DensityLean) return;
            var feel = HitFeel.Feel(HitTier.Lamp);
            // 금빛 별: 몸을 덮는 스티커가 아니라 빛나는 별로 (가산 0.5, 조금 위, 빨리 스민다) — 맞은 몸이 보이게
            int star = ImpactArt(Motif(VfxMotif.Star), BattleEase.WithAlpha(UITheme.Gold, 0.95f), position + Vector2.up * (14f * unit), LampStarSize * unit, feel,
                0.36f, delay, 0.85f, 0.5f);
            if (star >= 0)
            {
                _particles[star].Spin = 200f;
                _particles[star].FadeOut = 0.35f;
            }
            AnchorTo(star, target);
            if (density > HitFeel.DensityMini) return;
            if (_quality != FxQuality.Low)
            {
                int ring = Shockwave(position, UITheme.Lamp, 40f * unit, 150f * unit, 0.22f, delay, 0.8f, 0.8f);
                AnchorTo(ring, target);
            }
            var outward = position - (_shop != null ? _shop.FieldPosition : Vector2.zero);
            int streaks = Mathf.Max(1, Mathf.RoundToInt(feel.Streaks * JuiceGates.StreaksPerNormalHit(_quality) / 4f));
            Streaks(position, outward, streaks, feel.StreakCone, feel.StreakSpeedMin, feel.StreakSpeedMax,
                BattleEase.WithAlpha(JuiceColors.TowardCream(UITheme.Lamp, 0.5f), 0.9f), delay, 20f, 5f, 1f, target);
        }

        /// <summary>
        /// 불 밝히기 빛 물결(바닥 고리, 수선점에서 퍼짐)이 전장 위치에 닿기까지 걸리는 시간 (연출 시계).
        /// 고리 반지름은 LampWave.Radius(점화 뒤 경과 시간) 월드 — 적마다 이 시간에 맞춰 명중·숫자를 띄우면 물결이 거리순으로 적을 훑고 지나간다.
        /// </summary>
        public float LampWaveDelay(Vector2 fieldPosition)
        {
            if (_mapper == null) return LampWave.Start + 0.1f;
            var local = fieldPosition - _mapper.Origin;
            float ppu = Mathf.Max(1f, _mapper.Ppu);
            float squash = Mathf.Max(0.1f, _mapper.Squash);
            float world = new Vector2(local.x / ppu, local.y / (ppu * squash)).magnitude;
            return LampWave.Start + LampWave.Delay(world);
        }

        // ───────────── 화면 연출 ─────────────

        // ── 앱 전체 빛 번쩍임 (스테이지 클리어) — 2차 조율 (r2 비평): 크림 0.3 막은 ClampLight 상한(줄이기 0.3)과 같아서 번쩍임 줄이기를 켜도 그대로였고,
        // stage_clear +33ms 대비가 깨끗한 화면의 70.8% · 평균 밝기 +43.5% (보이는 전장 전체 +79%) — 예전 불 밝히기의 우유빛 막과 같은 문제.
        // 이제 등불색 쪽으로 반(우유빛이 아니라 따뜻한 막) · 세기 0.15까지 · 줄이기 ×0.5 (= 0.075).
        // 목표: +33ms 대비 ≥ 86% · 밝기 ≈ +20% (줄이기 ≈ 93% · +10%). 축하는 수선점 등불 · 금빛 후광 · 고리 · 색종이가 맡는다
        /// <summary>앱 전체 빛 번쩍임 세기 상한 (스크린 블렌드)</summary>
        const float ScreenLightMaxPeak = 0.15f;
        /// <summary>앱 전체 빛 번쩍임 색을 등불색(UITheme.Lamp) 쪽으로 옮기는 비율</summary>
        const float ScreenLightToLamp = 0.5f;
        /// <summary>번쩍임 줄이기일 때 앱 전체 빛 번쩍임 배율</summary>
        const float ScreenLightReduced = 0.5f;

        /// <summary>
        /// 앱 전체(상단 바·탭까지) 번쩍임. 스테이지 클리어/실패, 보스 등장처럼 드문 순간에만 쓴다.
        /// 밝은 색(밝기 0.6 초과)은 빛 번쩍임: 스크린 블렌드, 색은 등불색 쪽으로 반, 세기 ScreenLightMaxPeak(0.15)까지 · 번쩍임 줄이기 ×0.5.
        /// 어둡거나 진한 색은 틴트(알파 0.3까지, 그대로). 전장 전체 번쩍임 1초 규칙을 따른다.
        /// 잠자기 모드에서는 없다 (aspect R4 — 팝업 바로 아래 층이라 잠자기 덮개와 카드까지 물들였다)
        /// </summary>
        public void ScreenFlash(Color color, float peak, float duration)
        {
            if (_sleepMode) return;
            if (!_ready) Prepare();
            if (_screenFlash == null) return;
            bool light = JuiceColors.Luminance(color) > 0.6f;
            if (!_gov.TryFieldFlash(_realClock, false)) return;
            if (light)
            {
                color = Color.Lerp(color, UITheme.Lamp, ScreenLightToLamp);
                peak = ClampLight(Mathf.Min(peak, ScreenLightMaxPeak) * (_reduceFlash ? ScreenLightReduced : 1f));
            }
            else peak = ClampTint(peak);
            StartOverlay(ref _screenOverlay, _screenFlash, color, peak, duration, light);
        }

        /// <summary>
        /// 전장 안에서만 틴트 번쩍임 (스테이지 기믹 알림·위험). 이미 더 밝게 번쩍이는 중이면 덮어쓰지 않는다.
        /// 밝게 빛나야 하는 순간은 LightFlash (스크린 블렌드)를 쓴다. 잠자기 모드에서는 없다 (aspect R4 — 열 전체 틴트)
        /// </summary>
        public void FieldFlash(Color color, float peak, float duration)
        {
            if (_sleepMode) return;
            if (!_ready) Prepare();
            if (_fieldFlash == null) return;
            peak = ClampTint(peak);
            if (_fieldOverlay.Active && OverlayValue(in _fieldOverlay) > peak) return;
            if (!_gov.TryFieldFlash(_realClock, false)) return;
            StartOverlay(ref _fieldOverlay, _fieldFlash, color, peak, duration, false);
        }

        /// <summary>앱 전체 번쩍임이 켜져 있는지 (확인용 — SleepSignalTests)</summary>
        public bool ScreenFlashActive => _screenOverlay.Active;

        /// <summary>전장 번쩍임(틴트 · 빛)이 켜져 있는지 (확인용 — SleepSignalTests)</summary>
        public bool FieldFlashActive => _fieldOverlay.Active;

        /// <summary>켜져 있는 앱 전체 번쩍임을 바로 끈다 (잠드는 순간 — aspect R4. 전장 덮개는 그대로). 할당 없음</summary>
        void StopScreenFlash()
        {
            _screenOverlay = default;
            if (_screenFlash != null) _screenFlash.enabled = false;
        }

        /// <summary>
        /// 흔들림 예산에 trauma를 더한다 (FX-02). 값(HitFeel): 수선점 일반 0.3(초당 0.6까지) · 보스 포위 0.55 · 보스 등장 0.62 · 실패 0.65 ·
        /// 불 밝히기 0.85 · 보스 처치 0.68 · 범위 스킬 0.4(AddSkillTrauma) · 분노 0.45. direction = 흔들림이 기우는 쪽 (전장 로컬)
        /// </summary>
        public void AddTrauma(float amount, Vector2 direction = default) => _trauma.Add(amount, direction);

        /// <summary>수선점 일반 피격 흔들림 (초당 합계 HitFeel.ShopTraumaPerSecond까지)</summary>
        public void AddShopTrauma(Vector2 direction) => _trauma.AddShopHit(HitFeel.ShopHitTrauma, direction);

        /// <summary>예전 치명타 흔들림 (v2: CritTrauma = 0 — 치명타는 Kick(dir, KickCrit, KickChannel.Crit)으로). 부르던 곳을 위해 남긴다</summary>
        public void AddCritTrauma(float delay = 0f)
        {
            if (delay <= 0.01f)
            {
                _trauma.AddCrit();
                return;
            }
            _critTraumaTimer = _critTraumaTimer >= 0f ? Mathf.Min(_critTraumaTimer, delay) : delay;
        }

        /// <summary>예전 진폭(px) 흔들림 호환: trauma로 바꿔 더한다 (진폭 = MaxOffset × trauma²). 새 코드는 AddTrauma를 쓴다</summary>
        public void Shake(float amplitude, float duration) => AddTrauma(Mathf.Sqrt(Mathf.Clamp01(amplitude / TraumaShake.MaxOffset)));

        /// <summary>보스 처치 결정적 순간 (몸 그림·터지는 자리 없이 — 예전 호출). 새 코드는 BossKillMoment(delay, point, body)</summary>
        public void BossKillMoment(float delay) => BossKillMoment(delay, Vector2.zero, null);

        /// <summary>
        /// 보스 처치 결정적 순간 (FX-JUICE E13, 1.5초에 한 번): delay(연출 시간 — 처치 꽃이 터지는 순간) 뒤에
        /// 전체 정지 0.12초 → 0.3배 0.18초 · 임팩트 프레임(어둡게 0.45 + 보스 크림 실루엣 + 분홍/하늘 인쇄 어긋남 ±6px) ·
        /// +50ms 등불색 빛 번쩍임 0.30(줄이기 0.15) · 흔들림 HitFeel.BossKillTrauma · 줌 1.03(터지는 자리 중심) · 진동 Heavy. body = 보스 몸 그림(EnemyView.Body), point = 터지는 자리
        /// </summary>
        public void BossKillMoment(float delay, Vector2 point, Graphic body)
        {
            if (!_ready) Prepare();
            if (delay <= 0.001f)
            {
                FireBossKillMoment(point, body);
                return;
            }
            QueueCue(FxCueKind.BossMoment, delay, false, point, 0f, 0f, 0f, 0f, default, false, body);
        }

        void FireBossKillMoment(Vector2 point, Graphic body)
        {
            if (!_gov.TryBossMoment(_realClock)) return;
            GlobalHitStop();
            ImpactFrame(body, 0f, true, true);
            // +50ms 빛: 등불색 0.30 (줄이기 0.15) — 크림 0.35는 전장 전체를 우유빛으로 덮었다 (fx2 r2 비평: 평균 밝기 +90% · 바뀐 넓이 90%).
            // 등불색이면 막 밝기 약 −23%에 따뜻한 금빛 물결 (어둡게 · 줌 · 정지는 그대로)
            QueueCue(FxCueKind.Light, 0.05f, true, Vector2.zero, _reduceFlash ? 0.15f : BossKillLight, 0.5f, 0f, 0f, UITheme.Lamp, true);
            AddTrauma(HitFeel.BossKillTrauma);
            ZoomPunch(HitFeel.ZoomBossKill, point, 0f, true);
            Haptic(HapticKind.Heavy);
        }

        /// <summary>
        /// 보스 처치·불 밝히기 같은 결정적 순간: 연출이 잠깐 멈췄다가 느리게 (FX-01 T5). 입자·숫자·액터 애니메이션(TimeScale)만 —
        /// 시뮬레이션(BattleSystem)과 HUD·Time.timeScale은 그대로 흐른다. 멈춤이 있는 정지는 1.5초에 한 번 (불 밝히기 점화처럼 멈춤 없이 느려지기만 하면 늘 건다)
        /// </summary>
        public void GlobalHitStop(float stop = HitFeel.GlobalStop, float slowScale = HitFeel.SlowScale, float slowTime = HitFeel.SlowTime)
        {
            if (!_ready) Prepare();
            if (stop > 0f && !_gov.TryGlobalStop(_realClock)) return;
            _slowMotion.Trigger(stop, slowScale, slowTime);
        }

        /// <summary>남아 있는 입자·숫자·화면 효과를 모두 지운다 (연출 시험장을 열고 닫을 때, 항목을 따로 찍을 때)</summary>
        public void ClearParticles()
        {
            if (!_ready) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i].Alive) Release(i);
            }
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (_numbers[i].Alive) HideNumber(i);
            }
            ClearNumberStacks();
            _trauma.Clear();
            _kick.Clear();
            _zoom.Clear();
            _slowMotion.Clear();
            _timeScale = 1f;
            _gov.Clear();
            ClearCues();
            ClearScreenEffects();
            _coinsInFlight = 0;
            _critTraumaTimer = -1f;
            _telegraphDisc = -1;
            _telegraphRing = -1;
            _lampInner = -1;
            _lampAge = -1f;
            for (int i = 0; i < _lampHitCount; i++) _lampHits[i].Target = null;
            _lampHitCount = 0;
            _lampHitTotal = 0;
            _rainStart = -1f;
            _rainCount = -1;
            if (_lampRing != null) _lampRing.enabled = false;
            if (_lampPillar != null) _lampPillar.enabled = false;
            _halos = 0;
            _decals = 0;
            _rejectedSparkle = 0;
            _rejectedHero = 0;
            _rejectedGround = 0;
            _cueDroppedSeen = 0;
            _lastRejectedHero = null;
            _shakeNow = 0f;
            _zoomShiftNow = 0f;
            if (_shakeRoot != null)
            {
                _shakeRoot.anchoredPosition = Vector2.zero;
                _shakeRoot.localScale = Vector3.one;
                _shakeRoot.localRotation = Quaternion.identity;
            }
            _shaking = false;
        }

        // ───────────── 입자 생성 ─────────────

        /// <summary>
        /// 입자 하나를 예산 안에서 꺼낸다 (못 꺼내면 −1).
        /// ground = 바닥 레이어(액터 아래) — 바닥 레이어가 있으면 바닥 묶음·바닥 예산에서만 꺼낸다 (모자라도 위쪽 레이어로 넘기지 않는다).
        /// sparkle = 별빛 층(반짝이·빛줄기·색종이·여운) — 위쪽 예산의 마지막 HitFeel.HeroReserve칸은 쓰지 못한다 (그 칸은 투사체 머리·명중 그림·
        /// 처치 꽃·코어 별처럼 공격이 읽히는 층 몫). 예산 탈락은 FxFrameStats(RejectedUpper/Hero/Ground)로 센다
        /// </summary>
        int Spawn(Sprite sprite, Color color, Vector2 position, float size0, float size1, float life, float delay, bool ground = false, bool sparkle = false)
        {
            if (!_ready) Prepare();
            if (sprite == null) return -1;
            int index;
            if (ground && _groundTotal > 0)
            {
                if (_freeGroundCount <= 0 || UsedGround >= _groundBudget)
                {
                    _rejectedGround++;
                    return -1;
                }
                index = _freeGround[--_freeGroundCount];
            }
            else
            {
                int cap = sparkle ? _budget - HitFeel.HeroReserve : _budget;
                if (_freeParticleCount <= 0 || UsedParticles >= cap)
                {
                    if (sparkle) _rejectedSparkle++;
                    else
                    {
                        _rejectedHero++;
                        _lastRejectedHero = sprite;
                    }
                    return -1;
                }
                index = _freeParticles[--_freeParticleCount];
            }
            _frameSpawns++;
            ref var p = ref _particles[index];
            p.Alive = true;
            p.Motion = Motion.Drift;
            p.Arrival = Arrival.None;
            p.Curve = SizeCurve.Out;
            p.Delay = delay;
            p.Age = 0f;
            p.Life = Mathf.Max(0.02f, life);
            p.P0 = position;
            p.P1 = Vector2.zero;
            p.P2 = Vector2.zero;
            p.P3 = Vector2.one;
            p.Size0 = size0;
            p.Size1 = size1;
            p.Aspect = 1f;
            p.AspectEnd = -1f;
            p.Height0 = 0f;
            p.Height1 = 0f;
            p.Rotation = Random.Range(0f, 360f);
            p.Spin = 0f;
            p.AlignToPath = false;
            p.FadeIn = 0.12f;
            p.FadeOut = -1f;
            p.Wobble = 0f;
            p.Seed = Random.Range(0f, 100f);
            p.Color = color;
            p.Color2 = color;
            p.Target = null;
            p.TargetId = -1;
            p.AnchorEnemy = null;
            p.AnchorEcho = null;
            p.AnchorId = -1;
            p.AnchorOffset = Vector2.zero;
            p.AnchorUntilShown = false;
            p.Add = 0f;
            p.Hold = 0f;
            p.Settle = 0f;
            p.EndScale = 1f;
            p.Drag = 0f;
            p.Gravity = 0f;
            p.Warp = 0f;
            p.FadeKind = FadeSmooth;
            p.Flags = 0;
            p.Pulse = 0f;
            p.PulseHz = 0f;
            p.Frames = null;
            p.FrameRate = 0f;
            p.Frame = -1;
            p.Scaled = false;
            p.Mirror = false;
            p.EaseCubic = false;
            if (p.Half != 0)
            {
                p.Half = 0;
                p.Image.type = Image.Type.Simple;
            }
            p.Image.sprite = sprite;
            // 첫 갱신 전에는 보이지 않는다 (지난 삶의 색이 한 프레임 비치지 않게)
            if (_legacyWrites)
            {
                p.Image.canvasRenderer.SetColor(Color.white);
                WriteColor(p.Image, color, 0f, 0f);
            }
            else p.Image.canvasRenderer.SetColor(Color.clear);
            p.Image.enabled = delay <= 0f;
            p.Rect.pivot = CenterPivot;
            p.Rect.anchoredPosition = position;
            // 첫 프레임 크기 (처음 보이는 갱신에서 SizeBase 틀 + 배율로 바뀐다). 좌우로 뒤집어 그렸던 입자(FaceArt)·배율은 되돌린다
            p.Rect.sizeDelta = new Vector2(size0, size0);
            p.Rect.localRotation = Quaternion.identity;
            p.Rect.localScale = Vector3.one;
            // 만든 순서 = 그리는 순서 (연출 표의 아래 줄부터 만들면 위 줄이 위에 그려진다)
            p.Rect.SetAsLastSibling();
            return index;
        }

        /// <summary>떠다니는 작은 조각 (별빛 층 — 반짝이·불씨·먼지·조각·음표·하트): 위쪽 예산의 영웅 층 몫(HeroReserve)은 쓰지 않는다</summary>
        int Drift(Sprite sprite, Color color, Vector2 position, Vector2 travel, Vector2 bend, float size0, float size1, float life, float delay = 0f)
        {
            int index = Spawn(sprite, color, position, size0, size1, life, delay, false, true);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.P1 = travel;
            p.P2 = bend;
            p.Spin = Random.Range(-180f, 180f);
            return index;
        }

        /// <summary>
        /// from → control → to 2차 곡선으로 빨려 드는 입자. sparkle = 별빛 층(장식 별빛·실 가닥 — 영웅 층 몫은 쓰지 않는다).
        /// easeCubic = 진행을 InOutCubic으로 (기본 SmoothStep보다 더 천천히 떠나 가운데서 빠르고 끝에서 내려앉는다 — 기억 한 톨 E10)
        /// </summary>
        int Curve(Sprite sprite, Color color, Vector2 from, Vector2 control, Vector2 to, float size0, float size1, float life, float delay, bool sparkle = false,
            bool easeCubic = false)
        {
            int index = Spawn(sprite, color, from, size0, size1, life, delay, false, sparkle);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Motion = Motion.Curve;
            p.P1 = control;
            p.P2 = to;
            p.FadeIn = 0.15f;
            p.EaseCubic = easeCubic;
            return index;
        }

        /// <summary>from → to 일정한 빠르기로 곧게 날아가는 입자</summary>
        int Line(Sprite sprite, Color color, Vector2 from, Vector2 to, float size0, float size1, float life, float delay)
        {
            int index = Spawn(sprite, color, from, size0, size1, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Motion = Motion.Line;
            p.P2 = to;
            p.FadeIn = 0.04f;
            return index;
        }

        int Homing(Sprite sprite, Color color, Vector2 from, EnemyView target, float arc, float life, float size, float aspect)
        {
            int index = Spawn(sprite, color, from, size, size * 0.8f, life, 0f);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Motion = Motion.Homing;
            p.Target = target;
            p.TargetId = target.Id;
            p.P1 = new Vector2(0f, arc);
            p.P2 = target.HitPoint;
            p.Aspect = aspect;
            p.FadeIn = 0.01f;
            return index;
        }

        /// <summary>center 둘레를 도는 입자 (반지름은 radius × 시작/끝 배율로 변한다)</summary>
        int Orbit(Sprite sprite, Color color, Vector2 center, Vector2 radius, float startAngle, float angularSpeed, float radiusFrom, float radiusTo,
            float size0, float size1, float life, float delay = 0f, bool ground = false)
        {
            int index = Spawn(sprite, color, center, size0, size1, life, delay, ground);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Motion = Motion.Orbit;
            p.P1 = radius;
            p.P2 = new Vector2(startAngle, angularSpeed);
            p.P3 = new Vector2(radiusFrom, radiusTo);
            p.Rect.anchoredPosition = center + new Vector2(Mathf.Cos(startAngle) * radius.x * radiusFrom, Mathf.Sin(startAngle) * radius.y * radiusFrom);
            return index;
        }

        /// <summary>제자리에서 커지며 사라지는 번쩍임</summary>
        int Flash(Vector2 position, Sprite sprite, Color color, float size0, float size1, float life, float delay = 0f)
        {
            int index = Spawn(sprite, color, position, size0, size1, life, delay);
            if (index >= 0) _particles[index].FadeIn = 0.01f;
            return index;
        }

        /// <summary>바닥에 납작하게 퍼지는 고리 (aspect = 세로 눌림)</summary>
        int Ring(Vector2 position, Color color, float size0, float size1, float life, float aspect, float delay = 0f)
        {
            int index = Spawn(_ring, color, position, size0, size1, life, delay);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Aspect = aspect;
            p.Rotation = 0f;
            p.FadeIn = 0.01f;
            return index;
        }

        /// <summary>입자가 적을 따라다니게 한다 (머리 위 표식, 발밑 장판)</summary>
        void AnchorTo(int index, EnemyView enemy)
        {
            if (index < 0 || enemy == null) return;
            ref var p = ref _particles[index];
            p.AnchorEnemy = enemy;
            p.AnchorId = enemy.Id;
            p.AnchorStart = enemy.Rect.anchoredPosition;
        }

        /// <summary>
        /// 지연된 입자가 나타나는 순간까지만 적을 따라간다 (그 순간 적이 있는 자리에서 제 길로 — 튀는 조각·떠오르는 하트는 몸에 붙어 다니지 않는다).
        /// 지연이 없으면 아무것도 하지 않는다
        /// </summary>
        void AnchorUntilShown(int index, EnemyView enemy, float delay)
        {
            if (index < 0 || enemy == null || delay <= 0f) return;
            AnchorTo(index, enemy);
            _particles[index].AnchorUntilShown = true;
        }

        /// <summary>입자가 잔향이를 따라다니게 한다</summary>
        void AnchorTo(int index, EchoView echo)
        {
            if (index < 0 || echo == null) return;
            ref var p = ref _particles[index];
            p.AnchorEcho = echo;
            p.AnchorId = echo.Id;
            p.AnchorStart = echo.Rect.anchoredPosition;
        }

        void Release(int index)
        {
            ref var p = ref _particles[index];
            p.Alive = false;
            p.Target = null;
            p.AnchorEnemy = null;
            p.AnchorEcho = null;
            p.Frames = null;
            p.Image.enabled = false;
            if ((p.Flags & FlagHalo) != 0) _halos = Mathf.Max(0, _halos - 1);
            if ((p.Flags & FlagDecal) != 0) _decals = Mathf.Max(0, _decals - 1);
            p.Flags = 0;
            if (index == _lampInner) _lampInner = -1;
            if (p.Ground) _freeGround[_freeGroundCount++] = index;
            else _freeParticles[_freeParticleCount++] = index;
        }

        /// <summary>
        /// 입자 색을 쓴다 — 정점 색을 쓰는 곳은 여기뿐 (FX-JUICE §3.1). 미리 곱한 알파 머티리얼이면 rgb × o, a = max(1/255, o × (1 − add))
        /// (가산 몫은 알파가 0에 가까워도 잘리지 않게 바닥 1/255). 기본 머티리얼이면 보통 알파 (가산 층은 반투명 빛).
        /// o = 불투명도(add 0) 또는 빛 세기(add &gt; 0) × 페이드
        /// </summary>
        void WriteColor(Image image, Color color, float opacity, float add)
        {
            if (_premul)
            {
                float a = Mathf.Max(AlphaFloor, opacity * (1f - Mathf.Clamp01(add)));
                image.color = new Color(color.r * opacity, color.g * opacity, color.b * opacity, a);
            }
            else image.color = new Color(color.r, color.g, color.b, opacity);
        }

        /// <summary>
        /// 입자 색 (WriteColor와 같은 결과, 메시를 다시 만들지 않게 둘로 나눈다): 정점 색 = (r, g, b, max(1/255, 1 − add)) — 색·가산 비율이 바뀔 때만
        /// 메시가 바뀐다 (Graphic.color는 같은 값이면 무시한다) / CanvasRenderer 색 = (o, o, o, o) — 매 프레임 투명도.
        /// 둘을 곱하면 미리 곱한 알파 (r·o, g·o, b·o, o·(1 − add)). 기본 머티리얼이면 정점 (r, g, b, 1) × 렌더러 (1, 1, 1, o)
        /// </summary>
        void PaintParticle(ref Particle p, float opacity)
        {
            var c = p.Color;
            float o = opacity > 0f ? opacity : 0f;
            if (_premul)
            {
                p.Image.color = new Color(c.r, c.g, c.b, Mathf.Max(AlphaFloor, 1f - Mathf.Clamp01(p.Add)));
                p.Image.canvasRenderer.SetColor(new Color(o, o, o, o));
            }
            else
            {
                p.Image.color = new Color(c.r, c.g, c.b, 1f);
                p.Image.canvasRenderer.SetColor(new Color(1f, 1f, 1f, o));
            }
        }

        // ───────────── 갱신 ─────────────

        void UpdateParticles(float dt)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                ref var p = ref _particles[i];
                if (!p.Alive) continue;
                if (p.Delay > 0f)
                {
                    p.Delay -= dt;
                    // 지연 동안만 따라가는 입자: 적이 옮겨 간 만큼을 담아 두다가 (적이 사라지면 마지막 자리)
                    if (p.AnchorUntilShown) FollowAnchor(ref p);
                    if (p.Delay > 0f) continue;
                    // 나타나야 했던 순간부터 지난 만큼만 나이를 먹는다 (첫 프레임이 가장 큰 명중이 첫 보이는 프레임에서 꼭대기)
                    p.Age = -p.Delay;
                    p.Delay = 0f;
                    p.Image.enabled = true;
                    if (p.AnchorUntilShown)
                    {
                        // 나타나는 순간의 자리에서 제 길로 — 그 뒤로는 따라가지 않는다
                        p.AnchorEnemy = null;
                        p.AnchorUntilShown = false;
                    }
                }
                else p.Age += dt;

                float k = p.Age / p.Life;
                if (k >= 1f)
                {
                    Arrive(ref p);
                    Release(i);
                    continue;
                }

                FollowAnchor(ref p);
                var previous = p.Rect.anchoredPosition;
                var position = Evaluate(ref p, k) + p.AnchorOffset;
                if (p.Wobble != 0f) position.x += Mathf.Sin(p.Age * 5.5f + p.Seed) * p.Wobble;
                p.Rect.anchoredPosition = position;

                float eased = p.Curve switch
                {
                    SizeCurve.Snap => BattleEase.OutCubic(k / 0.3f),
                    SizeCurve.Linear => k,
                    SizeCurve.Pop => Mathf.SmoothStep(0f, 1f, k),
                    SizeCurve.Back => BattleEase.OutBack(k / 0.45f, 2.2f),
                    SizeCurve.Quart => JuiceEase.OutQuart(k),
                    _ => BattleEase.OutCubic(k),
                };
                float size;
                if (p.Curve == SizeCurve.Impact) size = JuiceEase.ImpactSize(p.Age, p.Life, p.Hold, p.Settle, p.Size0, p.Size1, p.EndScale);
                else if (p.Curve == SizeCurve.Pop)
                    size = k < PopTime
                        ? p.Size0 * BattleEase.OutBack(k / PopTime, 2.6f)
                        : Mathf.Lerp(p.Size0, p.Size1, Mathf.SmoothStep(0f, 1f, (k - PopTime) / (1f - PopTime)));
                else size = Mathf.LerpUnclamped(p.Size0, p.Size1, eased);
                if (p.Pulse > 0f) size *= 1f + p.Pulse * Mathf.Sin((p.Age * p.PulseHz + p.Seed) * Mathf.PI * 2f);

                float width = size;
                float height;
                if (p.Motion == Motion.Streak)
                {
                    // 빛줄기: 빠를수록 길고, 속도 방향으로 눕는다
                    var velocity = StreakVelocity(in p);
                    float speed = velocity.magnitude;
                    width = size * (1f + speed / 600f);
                    if (speed > 0.01f) p.Rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                }
                if (p.Height0 > 0f) height = Mathf.Lerp(p.Height0, p.Height1, eased);
                else height = size * (p.AspectEnd >= 0f ? Mathf.Lerp(p.Aspect, p.AspectEnd, eased) : p.Aspect);
                if (_legacyWrites)
                {
                    if (p.Scaled)
                    {
                        p.Rect.localScale = p.Mirror ? MirrorScale : Vector3.one;
                        p.Scaled = false;
                    }
                    p.Rect.sizeDelta = new Vector2(width, height);
                }
                else
                {
                    if (!p.Scaled)
                    {
                        // 처음 보이는 갱신: 크기 틀을 한 번만 고정한다 (sizeDelta를 바꾸면 메시를 다시 만들고 레이아웃을 찾는다 — 매 프레임 수백 개면
                        // 그것이 연출의 가장 큰 CPU 비용). FaceArt가 둔 좌우 뒤집기는 여기서 기억한다
                        p.Mirror = p.Rect.localScale.x < 0f;
                        p.Rect.sizeDelta = SizeBaseBox;
                        p.Scaled = true;
                    }
                    float scaleX = width * InvSizeBase;
                    p.Rect.localScale = new Vector3(p.Mirror ? -scaleX : scaleX, height * InvSizeBase, 1f);
                }

                if (p.Motion != Motion.Streak)
                {
                    if (p.AlignToPath)
                    {
                        var delta = position - previous;
                        if (delta.sqrMagnitude > 0.01f) p.Rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    }
                    else
                    {
                        p.Rotation += p.Spin * dt;
                    }
                }
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation);

                if (p.Frames != null && p.FrameRate > 0f)
                {
                    int frame = Mathf.Min(p.Frames.Length - 1, (int)(p.Age * p.FrameRate));
                    if (frame != p.Frame && frame >= 0)
                    {
                        p.Frame = frame;
                        if (p.Frames[frame] != null) p.Image.sprite = p.Frames[frame];
                    }
                }

                float fadeIn = p.FadeIn > 0f ? Mathf.Clamp01(k / p.FadeIn) : 1f;
                float fadeStart = p.FadeOut >= 0f ? p.FadeOut : p.Motion == Motion.Drift ? 0.45f : 0.82f;
                float fadeK = Mathf.InverseLerp(fadeStart, 1f, k);
                float fadeOut = p.FadeKind == FadeInQuad ? 1f - fadeK * fadeK : 1f - Mathf.SmoothStep(0f, 1f, fadeK);
                float opacity = p.Color.a * fadeIn * fadeOut;
                if (_legacyWrites) WriteColor(p.Image, p.Color, opacity, p.Add);
                else PaintParticle(ref p, opacity);
            }
        }

        /// <summary>따라다니는 액터가 움직인 만큼 옮긴다 (다른 개체로 재사용되면 마지막 자리에 멈춘다)</summary>
        static void FollowAnchor(ref Particle p)
        {
            if (p.AnchorEnemy != null)
            {
                if (p.AnchorEnemy.Id == p.AnchorId && p.AnchorEnemy.isActiveAndEnabled) p.AnchorOffset = p.AnchorEnemy.Rect.anchoredPosition - p.AnchorStart;
                else p.AnchorEnemy = null;
            }
            else if (p.AnchorEcho != null)
            {
                if (p.AnchorEcho.Id == p.AnchorId && p.AnchorEcho.isActiveAndEnabled) p.AnchorOffset = p.AnchorEcho.Rect.anchoredPosition - p.AnchorStart;
                else p.AnchorEcho = null;
            }
        }

        Vector2 Evaluate(ref Particle p, float k)
        {
            switch (p.Motion)
            {
                case Motion.Curve:
                    // 처음엔 천천히 풀리다가 수선점 쪽으로 빨려 들어간다 (EaseCubic이면 InOutCubic)
                    float eased = p.EaseCubic ? JuiceEase.InOutCubic(k) : k * k * (3f - 2f * k);
                    return BattleEase.Bezier(p.P0, p.P1, p.P2, eased);
                case Motion.Homing:
                    if (p.Target != null && p.Target.Id == p.TargetId && p.Target.IsTargetable) p.P2 = p.Target.HitPoint;
                    float t = BattleEase.InOutSine(k);
                    return Vector2.Lerp(p.P0, p.P2, t) + Vector2.up * (p.P1.y * Mathf.Sin(t * Mathf.PI));
                case Motion.Orbit:
                    float angle = p.P2.x + p.P2.y * p.Age;
                    float radius = Mathf.Lerp(p.P3.x, p.P3.y, BattleEase.OutCubic(k));
                    return p.P0 + new Vector2(Mathf.Cos(angle) * p.P1.x * radius, Mathf.Sin(angle) * p.P1.y * radius);
                case Motion.Line:
                    return Vector2.LerpUnclamped(p.P0, p.P2, k);
                case Motion.Shot:
                    if (p.Target != null && p.Target.Id == p.TargetId && p.Target.IsTargetable) p.P2 = p.Target.HitPoint;
                    // 진행도 휨: 끝으로 갈수록 빨라져 대상에 쏙 꽂힌다 (도착 시각은 그대로)
                    float shot = p.Warp > 0f ? k * (1f - p.Warp + p.Warp * k) : k;
                    return ShotPosition(p.P0, p.P2, p.P1, p.P3, shot);
                case Motion.Bounce:
                    return BouncePosition(p.P0, p.P1, p.P2.x, p.P2.y, p.P3.x, p.Age);
                case Motion.Streak:
                case Motion.Ballistic:
                    return BallisticPosition(p.P0, p.P1, p.Drag, p.Gravity, p.Age);
                default:
                    return p.P0 + p.P1 * BattleEase.OutCubic(k) + p.P2 * (k * k);
            }
        }

        /// <summary>x(t) = P0 + v0·(1 − e^(−d·t))/d + (0, −½·g·t²) (d ≈ 0이면 v0·t)</summary>
        static Vector2 BallisticPosition(Vector2 start, Vector2 velocity, float drag, float gravity, float time)
        {
            var travel = drag > 0.001f ? velocity * ((1f - Mathf.Exp(-drag * time)) / drag) : velocity * time;
            return start + travel + new Vector2(0f, -0.5f * gravity * time * time);
        }

        /// <summary>v(t) = v0·e^(−d·t) + (0, −g·t)</summary>
        static Vector2 StreakVelocity(in Particle p)
        {
            var v = p.Drag > 0.001f ? p.P1 * Mathf.Exp(-p.Drag * p.Age) : p.P1;
            v.y -= p.Gravity * p.Age;
            return v;
        }

        void Arrive(ref Particle p)
        {
            switch (p.Arrival)
            {
                case Arrival.ShopAbsorb:
                    if (_shop != null) _shop.Absorb(0.22f);
                    break;
                case Arrival.ShopGlint:
                    CoreStar(p.Rect.anchoredPosition, JuiceColors.CoreCream, 40f, 28f, 0.06f, 0f);
                    if (_shop != null) _shop.Absorb(0.22f);
                    break;
                case Arrival.Impact:
                    Flash(p.P2, _glow, Color.Lerp(p.Color, Color.white, 0.4f), 30f, 90f, 0.18f);
                    break;
                case Arrival.SkillImpact:
                    SkillImpactAt(p.Rect.anchoredPosition, p.Image.sprite, p.Color, p.Color2, p.Size0);
                    break;
                case Arrival.Splash:
                    var at = p.Rect.anchoredPosition;
                    int ring = Ring(at, BattleEase.WithAlpha(p.Color2, 0.8f), 6f, 46f, 0.32f, GroundAspect);
                    if (ring >= 0) _particles[ring].Add = 0.5f;
                    for (int i = 0; i < 2; i++)
                    {
                        Drift(Motif(VfxMotif.Drop), BattleEase.WithAlpha(p.Color2, 0.8f), at, new Vector2(i == 0 ? -16f : 16f, 18f), Vector2.down * 20f, 7f, 3f, 0.3f);
                    }
                    break;
            }
        }

        /// <summary>
        /// trauma 흔들림 + 킥 + 줌 펀치: 전장(ShakeRoot)만 옮기고 살짝 돌리고 키운다 (설정 배율 0이면 전혀 움직이지 않는다).
        /// 움직이는 동안만 쓰고, 끝나면 한 번 제자리로 돌려놓고 더는 건드리지 않는다
        /// </summary>
        void UpdateShake(float dt)
        {
            _trauma.Tick(dt, _shakeScale, out var offset, out float rotation);
            var kick = _kick.Tick(dt);
            float zoom = _zoom.Tick(dt);
            HitFeel.CameraPose(offset, kick, zoom, _shakeScale, _zoomPivot, out var position, out float scale);
            // 표본: 흔들림(trauma + 킥)과 줌 중심 보정 이동을 따로 (줌만 있는 순간을 흔들림으로 세지 않게)
            var zoomShift = (1f - scale) * _zoomPivot;
            _shakeNow = (position - zoomShift).magnitude;
            _zoomShiftNow = zoomShift.magnitude;
            if (_shakeRoot == null) return;
            bool moving = position.sqrMagnitude > 1e-8f || rotation != 0f || Mathf.Abs(scale - 1f) > 1e-5f;
            if (!moving && !_shaking) return;
            _shaking = moving;
            _shakeRoot.anchoredPosition = moving ? position : Vector2.zero;
            _shakeRoot.localScale = moving ? new Vector3(scale, scale, 1f) : Vector3.one;
            _shakeRoot.localRotation = moving && rotation != 0f ? Quaternion.Euler(0f, 0f, rotation) : Quaternion.identity;
        }

        /// <summary>
        /// 불 밝히기 바닥 빛 물결(가산 0.75, 알파 (1 − k)^1.5 — 반쯤 퍼지면 0.35로 일찍 옅어져 받침대·광장이 하얗게 날아가지 않는다) ·
        /// 안쪽 고리(물결 × 0.8) · 빛기둥(순수 가산). 번쩍임 줄이기면 물결·빛기둥 세기 ×0.5
        /// </summary>
        void UpdateLampRing(float dt)
        {
            if (_lampAge < 0f) return;
            _lampAge += dt;
            const float pillarLife = 0.9f;
            float ppu = _mapper != null ? _mapper.Ppu : 200f;
            float wave = _lampAge - LampWave.Start;
            float radius = LampWave.Radius(Mathf.Max(0f, wave)) * ppu;
            float light = JuiceGates.LampLight(_reduceFlash);

            if (_lampRing != null)
            {
                float k = wave / LampWave.Life;
                if (k >= 1f) _lampRing.enabled = false;
                else
                {
                    _lampRing.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
                    // (1 − k)^1.5 = u·√u (Pow 없이)
                    float u = 1f - Mathf.Clamp01(k);
                    float alpha = k < 0f ? 0f : u * Mathf.Sqrt(u);
                    WriteColor(_lampRing, Color.Lerp(UITheme.Cream, UITheme.Lamp, Mathf.Clamp01(k * 1.5f)), alpha * light, LampRingAdd);
                }
            }
            if (_lampInner >= 0 && _lampInner < _particles.Length && _particles[_lampInner].Alive)
            {
                ref var p = ref _particles[_lampInner];
                float diameter = Mathf.Max(2f, radius * 2f * 0.8f);
                p.Size0 = diameter;
                p.Size1 = diameter;
                p.P0 = _lampCenter;
            }
            if (_lampPillar != null)
            {
                float k = _lampAge / pillarLife;
                if (k >= 1f) _lampPillar.enabled = false;
                else
                {
                    // 빛기둥: 점화 동안 솟고, 물결이 퍼지는 동안 가늘어지며 스민다
                    float rise = BattleEase.OutCubic(Mathf.Clamp01(k / 0.25f));
                    _lampPillar.rectTransform.localScale = new Vector3(1.15f - k * 0.65f, rise, 1f);
                    // 솟는 순간 가장 밝고 빨리 옅어진다 (수선점이 빛기둥 속에 오래 묻히지 않게)
                    WriteColor(_lampPillar, UITheme.Cream, LampPillarIntensity * light * (1f - JuiceEase.OutQuad(k)), 1f);
                }
            }
            if (_lampAge > Mathf.Max(LampWave.Start + LampWave.Life, pillarLife)) _lampAge = -1f;
        }

        // ───────────── 반딧불 (분위기) ─────────────

        void PrepareFireflies()
        {
            if (_fireflies == null) return;
            _fireflyBase = new Vector2[_fireflies.Length];
            _fireflySeed = new float[_fireflies.Length];
            for (int i = 0; i < _fireflies.Length; i++)
            {
                _fireflyBase[i] = new Vector2(Random.Range(-0.48f, 0.48f), Random.Range(-0.45f, 0.45f));
                _fireflySeed[i] = Random.Range(0f, 100f);
            }
        }

        /// <summary>
        /// 반딧불: 보이는 띠 [FieldBottom, FieldTop] 안에서 아주 느리게 떠오르고 위를 넘으면 아래에서 다시 (UX-SPEC-IA §6.5 — 시트가 열리면 올라간 전투의
        /// 보이는 띠로 모인다. MAIN은 예전처럼 전장 전체 높이)
        /// </summary>
        void UpdateFireflies()
        {
            if (_fireflies == null || _fireflyBase == null) return;
            float width = _layer.rect.width;
            float top = FieldTop;
            float bottom = FieldBottom;
            float middle = (top + bottom) * 0.5f;
            float span = Mathf.Max(1f, top - bottom);
            float time = _realClock;
            for (int i = 0; i < _fireflies.Length; i++)
            {
                var image = _fireflies[i];
                if (image == null) continue;
                float seed = _fireflySeed[i];
                // 아주 느리게 위로 떠오르며 띠 위를 넘으면 아래에서 다시 나타난다
                float rise = Mathf.Repeat(_fireflyBase[i].y + 0.5f + time * 0.012f * (1f + seed % 1f), 1f) - 0.5f;
                var position = new Vector2(
                    _fireflyBase[i].x * width + Mathf.Sin(time * 0.6f + seed) * 26f,
                    middle + rise * span + Mathf.Cos(time * 0.8f + seed * 1.3f) * 14f);
                image.rectTransform.anchoredPosition = position;
                float twinkle = 0.5f + 0.5f * Mathf.Sin(time * (1.6f + seed % 1.2f) + seed);
                float edgeFade = 1f - Mathf.SmoothStep(0.38f, 0.5f, Mathf.Abs(rise));
                // 반짝임은 CanvasRenderer 알파로 (perf PF-2 F8): Graphic.color를 매 프레임 쓰면 반딧불 16개의 메시를 매 프레임 다시 만든다 (측정 c: 프레임당 16.0)
                if (image.color.a != 1f) image.color = BattleEase.WithAlpha(image.color, 1f);
                image.canvasRenderer.SetAlpha((0.15f + 0.6f * twinkle * twinkle) * edgeFade);
            }
        }

        /// <summary>바닥에 퍼지는 고리의 세로 비율 (전장 눌림)</summary>
        float GroundAspect => _mapper != null ? _mapper.Squash : 0.7f;

        static Vector2 RandomDirection()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        static Sprite Or(Sprite a, Sprite b) => a != null ? a : b;
    }
}
