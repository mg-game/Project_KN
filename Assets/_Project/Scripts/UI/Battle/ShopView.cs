using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전장 가운데의 수선점. 불빛 게이지만큼 창이 따뜻하게 밝아지고, 잔향이 빛을 흡수하면 맥동,
    /// 적에게 맞으면 흔들리며 붉게 — 건물이 말랑하게 눌렸다가 용수철(5Hz · 감쇠 0.5)로 되튄다 (일반 0.07, 보스 포위 0.12, FX-JUICE E14) —
    /// (건물 떨림 6px는 설정 흔들림 배율 0 / 50 / 100%를 따른다 — BattleFx가 수선점 피격마다 ShakeScale을 넘긴다),
    /// 불 밝히기 때는 크게 빛나며 불빛이 0.4초 동안 12Hz로 일렁인다 (E17 / E18 — 번쩍임 줄이기면 일렁임 없이 부풀기만).
    /// 건물(_root)은 액터 레이어 안에서 발(문 앞 바닥) 높이로 다른 액터와 앞뒤 정렬되고(뒤쪽 레인의 적은 건물에 가려짐),
    /// 뒤 후광(_backRoot)만 액터 뒤 레이어에 깔린다.
    /// 수선점 보호막이 있으면 건물 위로 반투명 돔(단추·우산·거품 그림)이 씌워져 숨 쉬듯 반짝이고, 깨지면 하얗게 번쩍이며 터진다.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [SerializeField] BattleFieldMapper _mapper;
        [SerializeField] RectTransform _root;
        [Tooltip("액터 뒤 레이어에 깔리는 후광 묶음 (건물과 같은 자리·크기로 맞춘다)")]
        [SerializeField] RectTransform _backRoot;
        [Tooltip("흔들림/맥동이 적용되는 건물 본체")]
        [SerializeField] RectTransform _building;
        [SerializeField] Image _buildingImage;
        [Tooltip("건물 뒤 따뜻한 후광")]
        [SerializeField] Image _backGlow;
        [Tooltip("빛을 흡수할 때 건물 위로 번지는 빛")]
        [SerializeField] Image _pulseGlow;
        [Tooltip("바닥 평면에 깔리는 불빛 웅덩이")]
        [SerializeField] RectTransform _groundLight;
        [SerializeField] Image _groundLightImage;
        [Tooltip("월드 단위 수선점 표시 크기 (가로)")]
        [SerializeField] float _worldWidth = 1f;
        [Tooltip("잔향이 빛이 스며드는 문 위치 (바닥 기준, 건물 높이 비율)")]
        [SerializeField] float _doorHeight = 0.04f;

        [Header("보호막 돔")]
        [SerializeField] Image _dome;
        [Tooltip("돔 아래 바닥 고리")]
        [SerializeField] Image _domeRim;
        [Tooltip("돔 표면 반짝임")]
        [SerializeField] Image _domeShine;
        [SerializeField] Color _domeTint = new(0.75f, 0.89f, 1f, 1f);

        [Header("금 간 덮개 (ATK-08)")]
        [Tooltip("shop_crack_1..3 (건물 그림의 자식, 같은 자리, 입력 없음). 그림이 없으면 빌더가 만들지 않는다 → 지금의 붉은 틴트 · 불티만")]
        [SerializeField] Image[] _crackOverlays;

        /// <summary>금 간 덮개 단계: 내구도 ≤ 66% / 33% / 10%. 0.3초에 나타나고, 문턱보다 5% 넘게 회복해야 사라진다 (깜박이지 않게)</summary>
        public static readonly float[] CrackAt = { 0.66f, 0.33f, 0.10f };
        public const float CrackHysteresis = 0.05f;
        public const float CrackFade = 0.3f;
        readonly bool[] _crackOn = new bool[3];
        readonly float[] _crackAlpha = new float[3];

        /// <summary>불 밝히기 불빛 일렁임: 0.4초 동안 12Hz, 폭 0.08 (줄어든다)</summary>
        const float FlickerTime = 0.4f;
        const float FlickerHz = 12f;
        const float FlickerAmount = 0.08f;
        /// <summary>불 밝히기 때 뒤 후광이 더 커지는 양</summary>
        const float BurstGlowScale = 0.4f;
        /// <summary>눌림 용수철이 사실상 멈추는 시간</summary>
        const float SquashRest = 0.5f;
        /// <summary>피격 때 건물이 떠는 폭 (px, 0.3초 동안 줄어든다) × 설정 흔들림 배율</summary>
        const float ShakePx = 6f;
        const float ShakeTime = 0.3f;
        /// <summary>이름표 오브젝트 이름 (BattleSceneBuilder.BuildShop)</summary>
        const string NamePlateName = "NamePlate";

        int _layoutVersion = -1;
        float _lamp;
        float _absorb;
        float _hit;
        float _heal;
        float _burst;
        float _shakeTime;
        /// <summary>이번 피격의 떨림 폭 (px = ShakePx × 흔들림 배율, Hit 때 정한다)</summary>
        float _shakeAmount;
        /// <summary>흔들림 배율 0~1 (BattleFx.ShakeScaleNow — 시험장 DevOverride 포함). 음수 = 아직 받지 못함 → 기기 설정(GameSettings.ShakeScale)</summary>
        float _shakeScale = -1f;
        float _squashAge = 9f;
        float _squashAmount;
        float _flickerAge = 9f;
        float _lowHp;
        float _shield;
        float _shieldPop;
        float _shieldBreak;
        Color _domeColor = Color.white;
        Sprite _defaultDome;
        bool _domePrepared;
        RectTransform _namePlate;
        bool _namePlateLookedUp;

        /// <summary>수선점 바닥 중심 (전장 로컬 좌표)</summary>
        public Vector2 FieldPosition => _mapper.WorldToField(Vector2.zero);

        /// <summary>잔향이 빛이 흘러 들어가는 문 위치</summary>
        public Vector2 DoorPosition => FieldPosition + Vector2.up * (BuildingHeight * _doorHeight);

        float BuildingHeight => _building != null ? _building.rect.height : 200f;

        /// <summary>건물 위 끝 가운데 (전장 로컬 좌표 — 불 밝히기 합계 알림이 그 위에 뜬다)</summary>
        public Vector2 RoofPosition => FieldPosition + Vector2.up * BuildingHeight;

        /// <summary>돔 가운데 (보호막 연출 중심, 전장 로컬 좌표)</summary>
        public Vector2 DomeCenter => FieldPosition + Vector2.up * (BuildingHeight * 0.36f);

        /// <summary>돔 폭 (px)</summary>
        public float DomeWidth => _root != null ? _root.rect.width * 1.45f : 300f;

        /// <summary>
        /// 건물 아래 '수선점' 이름표 (빌더가 건물 묶음 _root 아래 'NamePlate'로 만든다 — 처음 물을 때 한 번 찾아 둔다, 없으면 null).
        /// 말풍선이 덮지 않게 BattleFx.ShopPlateKeepOut이 이 자리를 잰다 (Wave 3 r1 — SHEET에서 앞줄 친구 말풍선이 이름표를 가렸다)
        /// </summary>
        public RectTransform NamePlate
        {
            get
            {
                if (_namePlateLookedUp) return _namePlate;
                _namePlateLookedUp = true;
                var root = _root != null ? _root : transform;
                _namePlate = root.Find(NamePlateName) as RectTransform;
                return _namePlate;
            }
        }

        /// <summary>금 간 덮개 단계가 켜져 있어야 하는지 (순수): 켜짐이면 문턱 + 5%까지 유지, 꺼짐이면 문턱 이하에서 켠다</summary>
        public static bool CrackState(bool on, float hpRatio, float at) => on ? hpRatio <= at + CrackHysteresis : hpRatio <= at;

        /// <summary>새 전투: 금 간 덮개를 모두 바로 지운다</summary>
        public void ResetCracks()
        {
            for (int i = 0; i < _crackOn.Length; i++)
            {
                _crackOn[i] = false;
                _crackAlpha[i] = 0f;
                var image = _crackOverlays != null && i < _crackOverlays.Length ? _crackOverlays[i] : null;
                if (image != null) image.enabled = false;
            }
        }

        void TickCracks(float hpRatio, float dt)
        {
            if (_crackOverlays == null || _crackOverlays.Length == 0) return;
            for (int i = 0; i < _crackOn.Length && i < _crackOverlays.Length; i++)
            {
                var image = _crackOverlays[i];
                if (image == null) continue;
                _crackOn[i] = CrackState(_crackOn[i], hpRatio, CrackAt[i]);
                float target = _crackOn[i] ? 1f : 0f;
                if (Mathf.Approximately(_crackAlpha[i], target) && image.enabled == (target > 0f)) continue;
                _crackAlpha[i] = Mathf.MoveTowards(_crackAlpha[i], target, dt / CrackFade);
                bool show = _crackAlpha[i] > 0.001f;
                if (image.enabled != show) image.enabled = show;
                if (show) image.color = new Color(1f, 1f, 1f, _crackAlpha[i]);
            }
        }

        public void Tick(BattleSimulator sim, float dt)
        {
            if (_mapper.Version != _layoutVersion) Layout();

            float lampTarget = sim != null ? Mathf.Clamp01(sim.LampGauge) : 0f;
            _lamp = BattleEase.Damp(_lamp, lampTarget, 4f, dt);
            float hpRatio = sim != null && sim.ShopMaxHp > 0f ? sim.ShopHp / sim.ShopMaxHp : 1f;
            _lowHp = BattleEase.Damp(_lowHp, hpRatio < 0.3f ? 1f : 0f, 3f, dt);
            TickCracks(hpRatio, dt);

            _absorb = Mathf.Max(0f, _absorb - dt * 1.6f);
            _hit = Mathf.Max(0f, _hit - dt * 3f);
            _heal = Mathf.Max(0f, _heal - dt * 1.5f);
            _burst = Mathf.Max(0f, _burst - dt * 0.9f);
            _shakeTime = Mathf.Max(0f, _shakeTime - dt);

            float time = Time.time;
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 1.7f);
            // 불 밝히기: 불빛이 12Hz로 일렁이다 잦아든다 (1 ± 0.08). 12Hz 깜박임은 광과민 대역이라 번쩍임 줄이기면 일렁이지 않는다 (후광이 부푸는 것만)
            float flicker = BattleFx.ReduceFlashNow ? 1f : 1f + Flicker(_flickerAge);

            // 건물: 흡수 맥동 + 피격 용수철 눌림(가로로 퍼지고 세로로 눌렸다 한 번 되튄다 — 맞은 프레임이 가장 납작하다) + 흔들림
            float pulse = _absorb * 0.05f + _burst * 0.06f;
            float squash = Squash(_squashAmount, _squashAge);
            if (_squashAge < SquashRest) _squashAge += dt;
            if (_flickerAge < FlickerTime) _flickerAge += dt;
            _building.localScale = new Vector3(1f + pulse + squash, 1f + pulse - squash, 1f);
            var shake = _shakeTime > 0f && _shakeAmount > 0f
                ? new Vector2(Mathf.Sin(time * 70f), Mathf.Cos(time * 53f)) * (_shakeAmount * _shakeTime / ShakeTime)
                : Vector2.zero;
            _building.anchoredPosition = shake;

            var hitTint = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.66f), _hit);
            var dangerTint = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.86f), _lowHp * (0.5f + 0.5f * Mathf.Sin(time * 5f)));
            _buildingImage.color = hitTint * dangerTint;

            if (_backGlow != null)
            {
                float alpha = (0.22f + 0.38f * _lamp + 0.12f * breathe + 0.4f * _absorb + 0.5f * _burst) * flicker;
                var color = Color.Lerp(UITheme.Lamp, UITheme.Positive, _heal * 0.6f);
                _backGlow.color = BattleEase.WithAlpha(color, Mathf.Clamp01(alpha));
                float scale = (1f + 0.08f * breathe + BurstGlowScale * _burst + 0.1f * _absorb) * flicker;
                _backGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            if (_pulseGlow != null)
            {
                float alpha = Mathf.Clamp01((_absorb * 0.9f + _burst * 0.8f + _heal * 0.4f) * flicker);
                _pulseGlow.enabled = alpha > 0.01f;
                _pulseGlow.color = BattleEase.WithAlpha(Color.Lerp(UITheme.EchoGlow, UITheme.Cream, _burst), alpha);
            }
            if (_groundLightImage != null)
            {
                float alpha = (0.18f + 0.32f * _lamp + 0.08f * breathe + 0.4f * _burst) * flicker;
                _groundLightImage.color = BattleEase.WithAlpha(UITheme.Lamp, Mathf.Clamp01(alpha));
            }
            TickDome(sim, dt, time);
        }

        /// <summary>건물 눌림 (가로 +, 세로 −): amount × 감쇠 용수철 변위 (5Hz · 감쇠 0.5)</summary>
        static float Squash(float amount, float age) =>
            amount <= 0f || age >= SquashRest ? 0f : amount * JuiceEase.SpringDisplacement(age, EnemyFxCatalog.ShopSpringHz, EnemyFxCatalog.ShopSpringZeta);

        /// <summary>불빛 일렁임 (−0.08 ~ +0.08, 0.4초 동안 줄어든다)</summary>
        static float Flicker(float age) =>
            age >= FlickerTime ? 0f : FlickerAmount * (1f - age / FlickerTime) * Mathf.Sin(age * FlickerHz * Mathf.PI * 2f);

        void PrepareDome()
        {
            if (_domePrepared) return;
            _domePrepared = true;
            if (_dome != null)
            {
                _defaultDome = _dome.sprite;
                _domeColor = _domeTint;
                _dome.enabled = false;
            }
            if (_domeRim != null) _domeRim.enabled = false;
            if (_domeShine != null) _domeShine.enabled = false;
        }

        void TickDome(BattleSimulator sim, float dt, float time)
        {
            PrepareDome();
            if (_dome == null) return;
            bool shielded = sim != null && sim.ShopShield > 0.01f;
            _shield = BattleEase.Damp(_shield, shielded ? 1f : 0f, shielded ? 8f : 5f, dt);
            _shieldPop = Mathf.Max(0f, _shieldPop - dt * 2.2f);
            _shieldBreak = Mathf.Max(0f, _shieldBreak - dt * 2.4f);

            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2.1f);
            float alpha = _shield * (0.4f + 0.12f * breathe) + 0.45f * _shieldPop + 0.8f * _shieldBreak;
            bool visible = alpha > 0.01f;
            if (_dome.enabled != visible) _dome.enabled = visible;
            if (_domeRim != null && _domeRim.enabled != visible) _domeRim.enabled = visible;
            if (_domeShine != null && _domeShine.enabled != visible) _domeShine.enabled = visible;
            if (!visible) return;

            var color = Color.Lerp(_domeColor, Color.white, _shieldBreak);
            _dome.color = BattleEase.WithAlpha(color, Mathf.Clamp01(alpha));
            // 씌워질 때 통 튀고, 깨질 때는 부풀며 흩어진다
            float scale = 1f + 0.02f * breathe + 0.12f * BattleEase.Punch(1f - _shieldPop) + 0.25f * (1f - _shieldBreak) * (_shieldBreak > 0f ? 1f : 0f);
            _dome.rectTransform.localScale = new Vector3(scale, scale, 1f);
            if (_domeRim != null)
            {
                _domeRim.color = BattleEase.WithAlpha(_domeTint, Mathf.Clamp01(alpha * 1.3f));
                _domeRim.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            if (_domeShine != null)
            {
                // 반짝임이 돔 표면을 따라 천천히 돈다 (돔의 자식, 돔 가운데 기준)
                float angle = time * 1.3f;
                var size = _dome.rectTransform.rect.size;
                _domeShine.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * size.x * 0.34f, Mathf.Abs(Mathf.Sin(angle)) * size.y * 0.3f);
                _domeShine.color = BattleEase.WithAlpha(UITheme.Cream, Mathf.Clamp01(alpha * 1.6f) * (0.5f + 0.5f * breathe));
            }
        }

        /// <summary>보호막이 씌워짐. sprite가 있으면 그 그림(단추·우산·거품)으로 돔을 바꾼다 (isArt면 흰색, 아니면 tint)</summary>
        public void ShieldUp(Sprite sprite, bool isArt, Color tint)
        {
            PrepareDome();
            if (_dome == null) return;
            _dome.sprite = sprite != null ? sprite : _defaultDome;
            _domeColor = sprite != null && isArt ? Color.white : tint;
            _shieldPop = 1f;
        }

        /// <summary>보호막이 깨짐</summary>
        public void ShieldBreak()
        {
            _shieldBreak = 1f;
            _hit = Mathf.Max(_hit, 0.5f);
            Press(EnemyFxCatalog.ShopHitSquash * 0.6f);
        }

        /// <summary>잔향이 빛이 스며들었을 때 (여러 번 겹치면 더 밝아진다)</summary>
        public void Absorb(float strength) => _absorb = Mathf.Min(1.4f, _absorb + strength);

        /// <summary>내구도가 깎임: 붉게 흔들리며 눌렸다 되튄다 (일반 0.07)</summary>
        public void Hit() => Hit(EnemyFxCatalog.ShopHitSquash);

        /// <summary>
        /// 내구도가 깎임 (squash = 눌림 폭 — 보스 포위는 EnemyFxCatalog.ShopSiegeSquash 0.12). 지금 남은 눌림이 더 크면 그대로 둔다.
        /// 떨림 6px는 흔들림 배율만큼 (0%면 떨지 않고 눌림·붉어짐만)
        /// </summary>
        public void Hit(float squash)
        {
            _hit = 1f;
            _shakeTime = ShakeTime;
            _shakeAmount = ShakePx * ShakeScale;
            Press(squash);
        }

        /// <summary>
        /// 흔들림 배율 0~1 (설정 0 / 50 / 100% — BattleFx.ShopImpact가 수선점 피격마다 ShakeScaleNow를 넣는다, 시험장 DevOverride 포함.
        /// 뒤따르는 Hit()의 떨림이 이 값을 따른다). 아직 받지 못했으면 기기 설정
        /// </summary>
        public float ShakeScale
        {
            get => _shakeScale >= 0f ? _shakeScale : GameSettings.ShakeScale;
            set => _shakeScale = Mathf.Clamp01(value);
        }

        /// <summary>적의 공격을 보호막 돔이 받아 냄 (내구도는 그대로): 돔이 출렁이며 밝아지고 건물은 살짝만 움찔</summary>
        public void DomeHit()
        {
            _shieldPop = Mathf.Max(_shieldPop, 0.7f);
            _hit = Mathf.Max(_hit, 0.25f);
            Press(EnemyFxCatalog.ShopHitSquash * 0.3f);
        }

        public void Heal() => _heal = 1f;

        /// <summary>불 밝히기·스테이지 클리어: 후광이 크게 부풀고 불빛이 0.4초 동안 일렁인다</summary>
        public void LampBurst()
        {
            _burst = 1f;
            _flickerAge = 0f;
        }

        /// <summary>눌림 용수철을 건다 (지금 남은 눌림보다 클 때만 — 같은 프레임의 일반 피격이 보스 포위 눌림을 줄이지 않게)</summary>
        void Press(float squash)
        {
            if (!(squash > 0f)) return;
            if (squash <= Mathf.Abs(Squash(_squashAmount, _squashAge))) return;
            _squashAmount = squash;
            _squashAge = 0f;
        }

        void Layout()
        {
            _layoutVersion = _mapper.Version;
            float width = _worldWidth * _mapper.Ppu;
            _root.anchoredPosition = FieldPosition;
            _root.sizeDelta = new Vector2(width, width);
            if (_backRoot != null)
            {
                _backRoot.anchoredPosition = FieldPosition;
                _backRoot.sizeDelta = new Vector2(width, width);
            }
            if (_groundLight != null)
            {
                float diameter = 1.5f * _mapper.Ppu;
                _groundLight.sizeDelta = new Vector2(diameter * 1.4f, diameter * 1.4f);
            }
        }
    }
}
