using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 스테이지 기믹 분위기: 전장 위에 겹치는 덮개 그림(Art/VFX/overlay_*)과 색 번짐, 가장자리 어둠, 날씨 입자.
    ///   비 오는 밤 = 비스듬한 빗줄기 · 바람 = 가로로 스치는 바람결 · 먼지 폭풍 = 느릿한 먼지 구름 · 정전 = 깊은 어둠과 희미한 빛 알갱이
    ///   보름달 = 달빛 번짐과 반짝이는 별빛 · 축제의 밤 = 살랑 떨어지는 색종이.
    /// 덮개 그림은 두 장을 엇갈려 흘리며 서로 페이드해서 이음매 없이 계속 움직이는 것처럼 보인다.
    /// 기믹이 바뀌면 지금 분위기가 스르르 걷힌 뒤 새 분위기가 차오른다. 입자는 미리 만든 이미지를 돌려 쓴다.
    /// 정전에서는 빛 특성 계보가 명중한 자리의 어둠에 반경 120px 구멍이 0.4초 열린다 (LightHole, ATK-14 — 빛 시너지가 정전에서 유리하다는 것이
    /// 눈에 보이게). 구멍은 덮개 이미지들에 씌운 IdleGame/UI/AmbienceHole 머티리얼 사본이 뚫는다 (머티리얼이 없으면 구멍 없이 지나간다).
    /// 보이는 띠 (UX-SPEC-IA §6.5 CR-15): 날씨 입자는 전장 지도의 보이는 띠(VisibleBottom ~ VisibleTop — 시트가 열리면 시트 위 끝 ~ 상단 바 아래) 안에서
    /// 생기고 돌아오며, 입자 수는 띠 넓이 ÷ 기준 띠(1080×624)에 비례한다 (1464px MAIN에서 비가 57% 성기지 않게 — 빌더의 입자 칸이 모자라면 처음에 복제해 늘린다).
    /// 이펙트 품질별로 그 수의 1 / 0.75 / 0.5 (높음 / 보통 / 낮음 — 낮음 기기는 예전 전장과 비슷한 수만).
    /// 띠가 줄면 남는 입자는 띠 밖으로 나갈 때 물러나고, 띠가 늘면 새 입자가 띠 안에서 스며 나온다. 달은 보이는 띠 위 끝에서 띠 높이 20% 아래.
    /// </summary>
    public class StageAmbienceView : MonoBehaviour
    {
        [SerializeField] RectTransform _root;
        [SerializeField] Image _overlayA;
        [SerializeField] Image _overlayB;
        [Tooltip("전장 전체 색 번짐")]
        [SerializeField] Image _wash;
        [Tooltip("가장자리 어둠 (비네트)")]
        [SerializeField] Image _shade;
        [Tooltip("보름달 빛 (오른쪽 위)")]
        [SerializeField] Image _moon;
        [SerializeField] Image[] _motes;
        [SerializeField] SkillVfxLibrary _library;
        [Header("입자 스프라이트 (흰색)")]
        [SerializeField] Sprite _streak;
        [SerializeField] Sprite _soft;
        [SerializeField] Sprite _sparkle;
        [SerializeField] Sprite _glow;
        [Header("기믹별 입자 그림 (흰색 아트, 색은 코드로 — 없으면 위의 흰색 모양)")]
        [Tooltip("비 오는 밤: 세로로 긴 빗방울 (48x192)")]
        [SerializeField] Sprite _raindrop;
        [Tooltip("바람: 꽃잎 조각")]
        [SerializeField] Sprite _petal;
        [Tooltip("먼지 폭풍: 먼지 뭉치")]
        [SerializeField] Sprite _dustPuff;
        [Tooltip("정전: 반딧불")]
        [SerializeField] Sprite _firefly;
        [Tooltip("보름달: 반짝이는 별빛")]
        [SerializeField] Sprite _twinkle;
        [Tooltip("축제의 밤: 색종이 (리본 · 단추 · 별 · 실)")]
        [SerializeField] Sprite[] _confetti;
        [Tooltip("덮개 그림 원본 비율 (1080x810)")]
        [SerializeField] Vector2 _overlaySize = new(1080f, 810f);
        [Tooltip("어둠 덮개에 빛 구멍을 뚫는 머티리얼 (IdleGame/UI/AmbienceHole). 런타임에는 사본을 쓴다. 없으면 구멍 없이")]
        [SerializeField] Material _holeMaterial;

        struct Hole
        {
            /// <summary>전장 로컬 좌표</summary>
            public Vector2 Position;
            public float Radius;
            public float Delay;
            public float Age;
            public bool Active;
        }

        const int HoleCount = 4;
        static readonly int[] HoleIds =
        {
            Shader.PropertyToID("_Hole0"), Shader.PropertyToID("_Hole1"), Shader.PropertyToID("_Hole2"), Shader.PropertyToID("_Hole3"),
        };
        readonly Hole[] _holes = new Hole[HoleCount];
        /// <summary>지금 기믹의 입자가 아트 그림인지 (그림 비율 그대로) / 그 그림의 세로:가로 비율</summary>
        bool _moteArt;
        float _moteAspect = 1f;
        bool _festivalConfetti;
        Material _holeRuntime;
        Transform _canvasSpace;
        bool _holesShown;

        struct Mote
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Size;
            public float Aspect;
            public float Rotation;
            public float Spin;
            public float Phase;
            public float Alpha;
            public Color Color;
            /// <summary>띠 안에 흩어 놓은 시각 (Time.time — 0.35초에 스며 나온다, 가장자리에서 들어오면 아주 옛날) / 크기를 다시 써야 하는지 (새로 생길 때만 sizeDelta를 쓴다)</summary>
            public float Born;
            public bool Resize;
        }

        /// <summary>기믹별 입자 수를 정한 기준 띠 넓이 (예전 전장 1080×624)</summary>
        const float ReferenceBandArea = 1080f * 624f;
        /// <summary>입자 칸 상한 (빌더의 칸이 모자라면 처음 한 번 복제해 늘린다 — MAIN 1080×1464에서 비 34 × 2.35 ≈ 80)</summary>
        const int MaxMotes = 96;
        /// <summary>띠가 늘어 새로 생긴 입자가 스며 나오는 시간</summary>
        const float MoteFadeIn = 0.35f;
        /// <summary>달: 보이는 띠 위 끝에서 띠 높이의 이만큼 아래 (MAIN에서 예전 size.y × 0.3과 같은 자리)</summary>
        const float MoonBandDrop = 0.2f;

        /// <summary>
        /// 이펙트 품질별 날씨 입자 몫 (높음 1 · 보통 0.75 · 낮음 0.5 — 별빛 층 16 / 12 / 8과 같은 비율). 띠 넓이로 늘린 수(MAIN 비 80)를 낮음 기기에서도
        /// 그대로 돌리지 않게: 낮음 MAIN 비 40 (예전 624px 전장의 34개 남짓), 보통 60
        /// </summary>
        static float QualityShare(FxQuality quality) => quality == FxQuality.Low ? 0.5f : quality == FxQuality.Medium ? 0.75f : 1f;

        static readonly Color RainBlue = new(0.62f, 0.78f, 0.95f, 1f);
        static readonly Color WindMint = new(0.72f, 0.92f, 0.86f, 1f);
        static readonly Color Dust = new(0.79f, 0.7f, 0.6f, 1f);
        static readonly Color NightInk = new(0.04f, 0.02f, 0.11f, 1f);
        static readonly Color MoonCream = new(1f, 0.95f, 0.8f, 1f);
        static readonly Color FestivalPink = new(1f, 0.72f, 0.85f, 1f);
        static readonly Color[] Confetti =
        {
            new(1f, 0.66f, 0.8f, 1f), new(0.95f, 0.77f, 0.42f, 1f), new(0.77f, 0.7f, 0.95f, 1f), new(0.56f, 0.89f, 0.69f, 1f), new(0.58f, 0.78f, 0.94f, 1f),
        };

        Mote[] _state;
        StageModifierType _current;
        StageModifierType _target;
        float _level;
        /// <summary>돌리는 입자 칸 끝 (이 앞의 켜진 칸만 움직인다) / 지금 띠에 맞는 입자 수 (넘는 칸은 띠 밖에서 물러난다)</summary>
        int _active;
        int _wanted;
        /// <summary>지금 기믹의 기준 입자 수(1080×624 띠) · 입자 그림</summary>
        int _baseCount;
        Sprite _moteSprite;
        /// <summary>전장 지도 (보이는 띠) — 루트 위쪽에서 찾는다, 없으면 루트 전체</summary>
        BattleFieldMapper _mapper;
        /// <summary>이펙트 품질을 읽을 전투 연출 (지도 아래에서 찾는다 — 시험장 DevOverride 포함, 없으면 기기 설정) / 입자 수를 맞춘 품질</summary>
        BattleFx _fx;
        FxQuality _countQuality;
        /// <summary>보이는 띠 (루트 로컬 y, 가운데 원점) / 입자 수를 맞춘 띠 높이</summary>
        float _bandTop;
        float _bandBottom;
        float _countBand = -1f;
        float _overlayAlpha;
        Vector2 _overlayDrift;
        float _overlayPeriod = 2.4f;
        float _washAlpha;
        float _shadeAlpha;
        float _moonAlpha;
        Color _washColor;
        Color _shadeColor;
        Vector2 _layoutSize;
        bool _ready;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_ready) return;
            _ready = true;
            GrowMotes();
            _state = new Mote[_motes != null ? _motes.Length : 0];
            for (int i = 0; _motes != null && i < _motes.Length; i++)
            {
                if (_motes[i] != null) _motes[i].enabled = false;
            }
            _mapper = _root != null ? _root.GetComponentInParent<BattleFieldMapper>(true) : null;
            _fx = _mapper != null ? _mapper.GetComponentInChildren<BattleFx>(true) : null;
            SetEnabled(_overlayA, false);
            SetEnabled(_overlayB, false);
            SetEnabled(_wash, false);
            SetEnabled(_shade, false);
            SetEnabled(_moon, false);
            PrepareHoles();
        }

        void OnDestroy()
        {
            if (_holeRuntime != null) Destroy(_holeRuntime);
        }

        /// <summary>
        /// 입자 칸을 MaxMotes까지 늘린다: 빌더가 만든 첫 입자 이미지를 같은 레이어 맨 끝에 복제 (처음 한 번만 할당 — 덮개 그림 위에 그려진다)
        /// </summary>
        void GrowMotes()
        {
            int have = _motes != null ? _motes.Length : 0;
            if (have == 0 || have >= MaxMotes) return;
            Image template = null;
            for (int i = 0; i < have && template == null; i++) template = _motes[i];
            if (template == null) return;
            var grown = new Image[MaxMotes];
            for (int i = 0; i < have; i++) grown[i] = _motes[i];
            var parent = template.rectTransform.parent;
            string moteName = template.name;
            for (int i = have; i < MaxMotes; i++)
            {
                var image = Instantiate(template, parent);
                image.name = moteName;
                image.enabled = false;
                grown[i] = image;
            }
            _motes = grown;
        }

        /// <summary>구멍 머티리얼 사본을 어둠 덮개(번짐·가장자리·덮개 그림 두 장)에 씌운다 (에셋 머티리얼은 그대로 둔다)</summary>
        void PrepareHoles()
        {
            if (_holeMaterial == null || _root == null) return;
            _holeRuntime = new Material(_holeMaterial) { name = _holeMaterial.name + " (Runtime)" };
            for (int i = 0; i < HoleCount; i++) _holeRuntime.SetVector(HoleIds[i], Vector4.zero);
            SetMaterial(_wash);
            SetMaterial(_shade);
            SetMaterial(_overlayA);
            SetMaterial(_overlayB);
            // 정점 위치는 루트 캔버스 좌표 (RectMask2D의 _ClipRect와 같은 공간)
            var canvas = _root.GetComponentInParent<Canvas>();
            _canvasSpace = canvas != null ? canvas.rootCanvas.transform : null;
        }

        void SetMaterial(Image image)
        {
            if (image != null) image.material = _holeRuntime;
        }

        /// <summary>
        /// 정전 스테이지에서 빛이 어둠을 밀어냄: fieldPosition(전장 로컬) 둘레 radius(px)의 어둠이 delay 뒤 0.4초 동안 ×0.4로 옅어진다.
        /// 정전이 아니거나 구멍을 뚫을 수 없으면 false
        /// </summary>
        public bool LightHole(Vector2 fieldPosition, float radius, float delay)
        {
            if (_holeRuntime == null || _canvasSpace == null) return false;
            if (_current != StageModifierType.Blackout || _target != StageModifierType.Blackout || _level < 0.05f) return false;
            int slot = 0;
            float oldest = -1f;
            for (int i = 0; i < HoleCount; i++)
            {
                if (!_holes[i].Active)
                {
                    slot = i;
                    oldest = float.MaxValue;
                    break;
                }
                if (_holes[i].Age > oldest)
                {
                    oldest = _holes[i].Age;
                    slot = i;
                }
            }
            _holes[slot] = new Hole { Position = fieldPosition, Radius = Mathf.Max(10f, radius), Delay = Mathf.Max(0f, delay), Age = 0f, Active = true };
            return true;
        }

        /// <summary>구멍 세기 곡선: 0.06초에 열리고 잠깐 머물다 뒤 40%에서 닫힌다 (가운데 어둠 ×0.4 → 세기 0.6)</summary>
        public static float HoleStrength(float k)
        {
            if (k <= 0f || k >= 1f) return 0f;
            float open = Mathf.Clamp01(k / 0.15f);
            float close = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, k));
            return (1f - EnemyFxCatalog.LightHoleDarkness) * open * close;
        }

        void TickHoles(float dt)
        {
            if (_holeRuntime == null || _canvasSpace == null) return;
            bool any = false;
            float scale = _root.lossyScale.x / Mathf.Max(0.0001f, _canvasSpace.lossyScale.x);
            for (int i = 0; i < HoleCount; i++)
            {
                ref var hole = ref _holes[i];
                if (!hole.Active) continue;
                float strength = 0f;
                if (hole.Delay > 0f) hole.Delay -= dt;
                else
                {
                    hole.Age += dt;
                    float k = hole.Age / EnemyFxCatalog.LightHoleTime;
                    if (k >= 1f) hole.Active = false;
                    else strength = HoleStrength(k);
                }
                if (!hole.Active || strength <= 0f)
                {
                    _holeRuntime.SetVector(HoleIds[i], Vector4.zero);
                    continue;
                }
                any = true;
                var world = _root.TransformPoint(hole.Position);
                var local = _canvasSpace.InverseTransformPoint(world);
                _holeRuntime.SetVector(HoleIds[i], new Vector4(local.x, local.y, hole.Radius * scale, strength));
            }
            _holesShown = any;
        }

        /// <summary>구멍을 모두 닫는다 (기믹이 바뀔 때)</summary>
        void ClearHoles()
        {
            for (int i = 0; i < HoleCount; i++) _holes[i].Active = false;
            if (_holeRuntime == null || !_holesShown) return;
            for (int i = 0; i < HoleCount; i++) _holeRuntime.SetVector(HoleIds[i], Vector4.zero);
            _holesShown = false;
        }

        public StageModifierType Current => _target;

        /// <summary>이번 스테이지 기믹으로 분위기를 바꾼다 (같으면 그대로)</summary>
        public void SetModifier(StageModifierType modifier)
        {
            Prepare();
            _target = modifier;
        }

        void Update()
        {
            if (!_ready || _root == null) return;
            float dt = Time.deltaTime;
            var size = _root.rect.size;
            if (size != _layoutSize) Layout(size);
            ReadBand(size);

            // 다른 기믹으로 바뀌면 걷혔다가(0) 새로 차오른다(1)
            if (_target != _current)
            {
                _level = Mathf.MoveTowards(_level, 0f, dt * 1.6f);
                if (_level <= 0f)
                {
                    _current = _target;
                    ClearHoles();
                    Configure(_current, size);
                }
            }
            else if (_current != StageModifierType.None) _level = Mathf.MoveTowards(_level, 1f, dt * 0.8f);
            if (_current == StageModifierType.None && _level <= 0f) return;
            // 보이는 띠가 바뀌면 (시트 열림·닫힘, 화면 크기) 입자 수를 띠 넓이에 다시 맞춘다 — 열고 닫는 0.3초 동안 조금씩. 품질이 바뀌어도 (자동 낮춤 · 설정)
            if (Mathf.Abs(_bandTop - _bandBottom - _countBand) > RecountStep || (_fx != null && _fx.Quality != _countQuality)) SetMoteTarget(size);

            float level = Mathf.SmoothStep(0f, 1f, _level);
            float time = Time.time;
            TickOverlay(time, level);
            if (_wash != null) _wash.color = BattleEase.WithAlpha(_washColor, _washAlpha * level);
            if (_shade != null) _shade.color = BattleEase.WithAlpha(_shadeColor, _shadeAlpha * level);
            if (_moon != null && _moon.enabled)
            {
                float breathe = 0.85f + 0.15f * Mathf.Sin(time * 0.9f);
                _moon.color = BattleEase.WithAlpha(MoonCream, _moonAlpha * level * breathe);
                float scale = 1f + 0.04f * Mathf.Sin(time * 0.7f);
                _moon.rectTransform.localScale = new Vector3(scale, scale, 1f);
                PlaceMoon(size);
            }
            TickMotes(dt, time, size, level);
            TickHoles(dt);
        }

        void Layout(Vector2 size)
        {
            _layoutSize = size;
            if (size.x <= 1f || size.y <= 1f) return;
            // 덮개 그림은 전장을 덮도록(cover) + 흘러도 가장자리가 보이지 않게 조금 크게
            float cover = Mathf.Max(size.x / _overlaySize.x, size.y / _overlaySize.y) * 1.12f;
            var shown = _overlaySize * cover;
            if (_overlayA != null) _overlayA.rectTransform.sizeDelta = shown;
            if (_overlayB != null) _overlayB.rectTransform.sizeDelta = shown;
        }

        /// <summary>입자 수를 다시 맞추는 띠 높이 변화 (px)</summary>
        const float RecountStep = 32f;

        /// <summary>
        /// 보이는 띠 (루트 로컬 y, 가운데 원점 — 전장 카메라 로컬과 같다): 전장 지도의 VisibleBottom ~ VisibleTop
        /// (MAIN = 전장 전체 ±h/2, 시트 = 시트 위 끝 ~ 상단 바 아래 — 올리는 도중에는 카메라가 올라간 만큼 루트 아래로도 넓어진다). 지도가 없으면 루트 높이 전체
        /// </summary>
        void ReadBand(Vector2 size)
        {
            if (_mapper != null)
            {
                _bandTop = _mapper.VisibleTop;
                _bandBottom = _mapper.VisibleBottom;
            }
            else
            {
                _bandTop = size.y * 0.5f;
                _bandBottom = -size.y * 0.5f;
            }
            if (_bandTop - _bandBottom < 1f) _bandBottom = _bandTop - 1f;
        }

        /// <summary>달: 오른쪽 위, 보이는 띠 위 끝에서 띠 높이 20% 아래 (시트가 열려도 상단 바 뒤로 숨지 않는다 — MAIN에서는 예전 자리 그대로). 바뀔 때만 쓴다</summary>
        void PlaceMoon(Vector2 size)
        {
            var at = new Vector2(size.x * 0.32f, _bandTop - MoonBandDrop * (_bandTop - _bandBottom));
            var rect = _moon.rectTransform;
            if ((rect.anchoredPosition - at).sqrMagnitude > 0.01f) rect.anchoredPosition = at;
        }

        /// <summary>
        /// 지금 띠에 맞는 입자 수 = 기믹의 기준 수 × 띠 넓이 ÷ 1080×624 × 품질 몫(QualityShare) (칸 수까지). 모자라면 새 입자를 띠 안 아무 데나 띄워 스며 나오게 하고,
        /// 넘치면 수만 낮춰 둔다 — TickMotes가 넘는 칸을 띠 밖(보이지 않는 곳)에서 끈다 (보이는 자리에서 갑자기 사라지지 않게)
        /// </summary>
        void SetMoteTarget(Vector2 size)
        {
            float band = _bandTop - _bandBottom;
            _countBand = band;
            _countQuality = _fx != null ? _fx.Quality : GameSettings.FxQuality;
            int target = 0;
            if (_baseCount > 0 && _moteSprite != null && _state.Length > 0)
            {
                float share = QualityShare(_countQuality);
                target = Mathf.Clamp(Mathf.RoundToInt(_baseCount * share * Mathf.Max(1f, size.x) * band / ReferenceBandArea), 1, _state.Length);
            }
            _wanted = target;
            for (int i = 0; i < target; i++)
            {
                var image = _motes[i];
                if (image == null || image.enabled) continue;
                image.sprite = MoteSprite(i, _moteSprite);
                Spawn(ref _state[i], _current, size, true);
                image.enabled = true;
            }
            if (target > _active) _active = target;
        }

        void Configure(StageModifierType modifier, Vector2 size)
        {
            var overlay = _library != null ? _library.Overlay(modifier) : null;
            bool hasOverlay = overlay != null && modifier != StageModifierType.None;
            if (_overlayA != null) _overlayA.sprite = overlay;
            if (_overlayB != null) _overlayB.sprite = overlay;
            SetEnabled(_overlayA, hasOverlay);
            SetEnabled(_overlayB, hasOverlay);

            _moonAlpha = 0f;
            _overlayPeriod = 2.6f;
            Sprite moteSprite = _sparkle;
            int count = 0;
            switch (modifier)
            {
                case StageModifierType.Rain:
                    Look(RainBlue, 0.1f, UITheme.Background, 0.3f, 0.7f, new Vector2(-28f, -110f));
                    _overlayPeriod = 1.1f;
                    count = 34;
                    moteSprite = _raindrop != null ? _raindrop : _streak;
                    break;
                case StageModifierType.Wind:
                    Look(WindMint, 0.04f, UITheme.Background, 0.12f, 0.55f, new Vector2(140f, -6f));
                    _overlayPeriod = 1.8f;
                    count = 18;
                    moteSprite = _petal != null ? _petal : _streak;
                    break;
                case StageModifierType.DustStorm:
                    Look(Dust, 0.16f, new Color(0.3f, 0.23f, 0.18f, 1f), 0.4f, 0.6f, new Vector2(80f, 6f));
                    _overlayPeriod = 3f;
                    count = 16;
                    moteSprite = _dustPuff != null ? _dustPuff : _soft;
                    break;
                case StageModifierType.Blackout:
                    Look(NightInk, 0.32f, NightInk, 0.9f, 0.7f, new Vector2(0f, 8f));
                    _overlayPeriod = 4f;
                    count = 12;
                    moteSprite = _firefly != null ? _firefly : _glow;
                    break;
                case StageModifierType.FullMoon:
                    Look(MoonCream, 0.05f, UITheme.Background, 0.18f, 0.65f, new Vector2(0f, 10f));
                    _overlayPeriod = 4f;
                    // 보름달 덮개 그림에 달이 이미 그려져 있으므로 덮개가 있으면 따로 달을 띄우지 않는다
                    _moonAlpha = hasOverlay ? 0f : 0.55f;
                    count = 22;
                    moteSprite = _twinkle != null ? _twinkle : _sparkle;
                    break;
                case StageModifierType.Festival:
                    Look(FestivalPink, 0.05f, UITheme.Background, 0.08f, 0.7f, new Vector2(0f, -50f));
                    _overlayPeriod = 3f;
                    count = 30;
                    moteSprite = _soft;
                    break;
                default:
                    Look(Color.white, 0f, Color.black, 0f, 0f, Vector2.zero);
                    break;
            }
            SetEnabled(_wash, _washAlpha > 0f);
            SetEnabled(_shade, _shadeAlpha > 0f);
            SetEnabled(_moon, _moonAlpha > 0f);

            // 축제는 색종이 그림을 입자마다 돌려 쓴다 (없으면 흰색 모양)
            _festivalConfetti = modifier == StageModifierType.Festival && _confetti != null && _confetti.Length > 0 && _confetti[0] != null;
            var first = MoteSprite(0, moteSprite);
            // 아트 입자는 그림 비율 그대로 그린다 (예전 흰색 모양만 길게 눌러 그린다)
            _moteArt = first != null && first != _streak && first != _soft && first != _glow && first != _sparkle;
            _moteAspect = _moteArt && first.rect.width > 0f ? first.rect.height / first.rect.width : 1f;

            // 새 기믹: 입자를 모두 끄고 지금 보이는 띠 넓이에 맞는 수만큼 다시 띄운다 (기준 수는 1080×624 띠 기준)
            for (int i = 0; i < _state.Length; i++)
            {
                if (_motes[i] != null) _motes[i].enabled = false;
            }
            _active = 0;
            _wanted = 0;
            _baseCount = count;
            _moteSprite = moteSprite;
            SetMoteTarget(size);
        }

        /// <summary>입자 i의 그림 (축제는 색종이 네 모양을 번갈아)</summary>
        Sprite MoteSprite(int index, Sprite fallback)
        {
            if (!_festivalConfetti) return fallback;
            var sprite = _confetti[index % _confetti.Length];
            return sprite != null ? sprite : _confetti[0];
        }

        void Look(Color wash, float washAlpha, Color shade, float shadeAlpha, float overlayAlpha, Vector2 drift)
        {
            _washColor = wash;
            _washAlpha = washAlpha;
            _shadeColor = shade;
            _shadeAlpha = shadeAlpha;
            _overlayAlpha = overlayAlpha;
            _overlayDrift = drift;
        }

        /// <summary>
        /// 입자 하나를 새로 띄운다 (first면 보이는 띠 전체에 흩어 놓고, 아니면 띠로 들어오는 가장자리에서 — 비·색종이는 위, 바람·먼지는 왼쪽, 빛 알갱이는 아래)
        /// </summary>
        void Spawn(ref Mote m, StageModifierType modifier, Vector2 size, bool first)
        {
            float halfX = size.x * 0.5f;
            float top = _bandTop;
            float bottom = _bandBottom;
            m.Phase = Random.Range(0f, 100f);
            m.Spin = 0f;
            switch (modifier)
            {
                case StageModifierType.Rain:
                    m.Velocity = new Vector2(-150f, -Random.Range(820f, 1000f));
                    m.Size = Random.Range(44f, 74f);
                    m.Aspect = 0.09f;
                    m.Alpha = Random.Range(0.3f, 0.55f);
                    m.Color = RainBlue;
                    m.Position = first ? new Vector2(Random.Range(-halfX, halfX + 120f), Random.Range(bottom, top))
                        : new Vector2(Random.Range(-halfX, halfX + 160f), top + Random.Range(20f, 140f));
                    m.Rotation = Mathf.Atan2(m.Velocity.y, m.Velocity.x) * Mathf.Rad2Deg;
                    break;
                case StageModifierType.Wind:
                    m.Velocity = new Vector2(Random.Range(520f, 820f), Random.Range(-30f, 10f));
                    m.Size = Random.Range(70f, 130f);
                    m.Aspect = 0.06f;
                    m.Alpha = Random.Range(0.18f, 0.36f);
                    m.Color = WindMint;
                    m.Position = first ? new Vector2(Random.Range(-halfX, halfX), Random.Range(bottom, top))
                        : new Vector2(-halfX - Random.Range(40f, 300f), Random.Range(bottom, top));
                    m.Rotation = 0f;
                    break;
                case StageModifierType.DustStorm:
                    m.Velocity = new Vector2(Random.Range(70f, 150f), Random.Range(-12f, 12f));
                    m.Size = Random.Range(140f, 260f);
                    m.Aspect = Random.Range(0.45f, 0.7f);
                    m.Alpha = Random.Range(0.08f, 0.15f);
                    m.Color = Dust;
                    m.Position = first ? new Vector2(Random.Range(-halfX, halfX), Random.Range(bottom, top))
                        : new Vector2(-halfX - m.Size, Random.Range(bottom, top));
                    m.Rotation = Random.Range(-10f, 10f);
                    break;
                case StageModifierType.Blackout:
                    m.Velocity = new Vector2(Random.Range(-8f, 8f), Random.Range(4f, 14f));
                    m.Size = Random.Range(14f, 26f);
                    m.Aspect = 1f;
                    m.Alpha = Random.Range(0.18f, 0.35f);
                    m.Color = UITheme.Lavender;
                    m.Position = first ? new Vector2(Random.Range(-halfX, halfX), Random.Range(bottom, top))
                        : new Vector2(Random.Range(-halfX, halfX), bottom - 20f);
                    m.Rotation = 0f;
                    break;
                case StageModifierType.FullMoon:
                    m.Velocity = new Vector2(Random.Range(-4f, 4f), Random.Range(6f, 16f));
                    m.Size = Random.Range(12f, 24f);
                    m.Aspect = 1f;
                    m.Alpha = Random.Range(0.4f, 0.8f);
                    m.Color = Random.value < 0.7f ? MoonCream : UITheme.Lavender;
                    m.Position = first ? new Vector2(Random.Range(-halfX, halfX), Random.Range(bottom, top))
                        : new Vector2(Random.Range(-halfX, halfX), bottom - 20f);
                    m.Rotation = Random.Range(0f, 90f);
                    m.Spin = Random.Range(-30f, 30f);
                    break;
                case StageModifierType.Festival:
                    m.Velocity = new Vector2(Random.Range(-20f, 20f), -Random.Range(70f, 130f));
                    m.Size = Random.Range(10f, 18f);
                    m.Aspect = Random.Range(0.45f, 0.7f);
                    m.Alpha = Random.Range(0.6f, 0.9f);
                    m.Color = Confetti[Random.Range(0, Confetti.Length)];
                    m.Position = first ? new Vector2(Random.Range(-halfX, halfX), Random.Range(bottom, top))
                        : new Vector2(Random.Range(-halfX, halfX), top + Random.Range(10f, 80f));
                    m.Rotation = Random.Range(0f, 360f);
                    m.Spin = Random.Range(-240f, 240f);
                    break;
                default:
                    m.Alpha = 0f;
                    break;
            }
            if (_moteArt) ArtShape(ref m, modifier);
            // 띠 안에 흩어 놓은 입자만 스며 나온다 (가장자리 밖에서 들어오는 입자는 처음부터 제 알파 — 비가 위쪽에서 흐리게 들어오지 않게)
            m.Born = first ? Time.time : -1000f;
            m.Resize = true;
        }

        /// <summary>아트 입자 모양: 그림 비율 그대로, 기믹마다 알맞은 크기 · 회전</summary>
        void ArtShape(ref Mote m, StageModifierType modifier)
        {
            m.Aspect = _moteAspect;
            switch (modifier)
            {
                case StageModifierType.Rain:
                    // 세로로 긴 빗방울 그림(아래를 향함)을 떨어지는 방향으로 기울인다
                    m.Size = Random.Range(10f, 15f);
                    m.Rotation = Mathf.Atan2(m.Velocity.y, m.Velocity.x) * Mathf.Rad2Deg + 90f;
                    break;
                case StageModifierType.Wind:
                    // 바람에 날리는 꽃잎: 빙글빙글
                    m.Size = Random.Range(16f, 28f);
                    m.Alpha = Random.Range(0.4f, 0.7f);
                    m.Rotation = Random.Range(0f, 360f);
                    m.Spin = Random.Range(-260f, 260f);
                    break;
                case StageModifierType.DustStorm:
                    m.Size = Random.Range(48f, 110f);
                    m.Alpha = Random.Range(0.16f, 0.3f);
                    break;
                case StageModifierType.Blackout:
                    m.Size = Random.Range(18f, 30f);
                    m.Alpha = Random.Range(0.3f, 0.5f);
                    m.Color = Random.value < 0.5f ? UITheme.Lamp : UITheme.Lavender;
                    m.Rotation = Random.Range(-15f, 15f);
                    break;
                case StageModifierType.FullMoon:
                    m.Size = Random.Range(14f, 26f);
                    break;
                case StageModifierType.Festival:
                    m.Size = Random.Range(18f, 30f);
                    break;
            }
        }

        void TickOverlay(float time, float level)
        {
            if (_overlayA == null || !_overlayA.enabled) return;
            float period = Mathf.Max(0.3f, _overlayPeriod);
            float a = Mathf.Repeat(time / period, 1f);
            float b = Mathf.Repeat(time / period + 0.5f, 1f);
            Drive(_overlayA, a, level);
            Drive(_overlayB, b, level);
        }

        /// <summary>덮개 한 장: 한 주기 동안 흘러가며 가운데서 가장 진하고 양 끝에서 사라진다</summary>
        void Drive(Image overlay, float phase, float level)
        {
            if (overlay == null) return;
            overlay.rectTransform.anchoredPosition = _overlayDrift * (phase - 0.5f);
            overlay.color = BattleEase.WithAlpha(Color.white, _overlayAlpha * level * Mathf.Sin(phase * Mathf.PI));
        }

        /// <summary>
        /// 입자 움직임 (할당 없음): 띠 밖으로 나가면 들어오는 가장자리에서 다시 띄운다. 띠가 줄어 넘는 칸은 보이지 않는 곳(띠 밖)에 있을 때 끈다.
        /// 크기(sizeDelta)는 새로 띄울 때만 쓰고, 새 입자는 0.35초에 스며 나온다
        /// </summary>
        void TickMotes(float dt, float time, Vector2 size, float level)
        {
            float halfX = size.x * 0.5f;
            for (int i = 0; i < _active; i++)
            {
                var image = _motes[i];
                if (image == null || !image.enabled) continue;
                ref var m = ref _state[i];
                m.Position += m.Velocity * dt;
                float twinkle = 1f;
                switch (_current)
                {
                    case StageModifierType.Wind:
                        m.Position.y += Mathf.Sin(time * 3f + m.Phase) * 18f * dt;
                        break;
                    case StageModifierType.Festival:
                        m.Position.x += Mathf.Sin(time * 2.2f + m.Phase) * 30f * dt;
                        break;
                    case StageModifierType.FullMoon:
                    case StageModifierType.Blackout:
                        twinkle = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(time * 2.4f + m.Phase));
                        break;
                }
                m.Rotation += m.Spin * dt;
                bool outside = Outside(m.Position, halfX, m.Size);
                if (i >= _wanted && (outside || Hidden(m.Position, halfX, m.Size)))
                {
                    image.enabled = false;
                    continue;
                }
                if (outside) Spawn(ref m, _current, size, false);

                var rect = image.rectTransform;
                rect.anchoredPosition = m.Position;
                if (m.Resize)
                {
                    rect.sizeDelta = new Vector2(m.Size, m.Size * m.Aspect);
                    m.Resize = false;
                }
                rect.localRotation = Quaternion.Euler(0f, 0f, m.Rotation);
                float born = Mathf.Clamp01((time - m.Born) / MoteFadeIn);
                image.color = BattleEase.WithAlpha(m.Color, m.Alpha * twinkle * level * born);
            }
            // 끝에서부터 꺼진 칸은 더 돌리지 않는다
            while (_active > _wanted && (_motes[_active - 1] == null || !_motes[_active - 1].enabled)) _active--;
        }

        /// <summary>입자가 띠 밖으로 멀리 나갔는지 (들어오는 쪽 여유 포함 — 다시 띄울 때)</summary>
        bool Outside(Vector2 position, float halfX, float margin)
        {
            margin += 40f;
            return position.y < _bandBottom - margin || position.y > _bandTop + margin + 160f || position.x > halfX + margin + 300f || position.x < -halfX - margin - 320f;
        }

        /// <summary>입자가 보이는 띠 밖이라 보이지 않는지 (가장자리에 걸친 입자는 보이는 것으로)</summary>
        bool Hidden(Vector2 position, float halfX, float margin) =>
            position.y + margin < _bandBottom || position.y - margin > _bandTop || Mathf.Abs(position.x) - margin > halfX;

        static void SetEnabled(Image image, bool enabled)
        {
            if (image != null && image.enabled != enabled) image.enabled = enabled;
        }
    }
}
